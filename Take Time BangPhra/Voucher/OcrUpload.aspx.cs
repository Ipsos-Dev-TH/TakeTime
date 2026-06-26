using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Voucher
{
    /// <summary>
    /// OCR-first ใบสำคัญจ่าย flow (Known gap #3): อัปโหลด → ocr/upload (autoCreate=false)
    /// → poll ผล → ให้ผู้ใช้ตรวจ/แก้ + เลือกแหล่งจ่ายเงิน → ocr/{id}/create-document?targetType=PaymentVoucher
    /// → PUT แก้ Draft (บังคับ PaymentAccountId/แหล่งเงิน + ค่าที่ผู้ใช้ยืนยัน) → approve.
    /// ใช้ company endpoints (/api/companies/*) ผ่าน X-Api-Key — รองรับทั้ง acc_/int_ (gate CanUseCompanyEndpoints).
    /// </summary>
    public partial class OcrUpload : Page
    {
        private string Conn => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        // รันงาน async บน threadpool เพื่อเลี่ยง deadlock กับ ASP.NET sync context
        private static T RunSync<T>(Func<Task<T>> f) => Task.Run(f).GetAwaiter().GetResult();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            if (!IsPostBack)
            {
                var cfg = new AccountingConfig(Conn);
                if (!cfg.IsConfigured || !cfg.Enabled)
                {
                    Msg(litStatus, "warn", "ยังไม่ได้ตั้งค่า/เปิดใช้งาน Accounting Integration — ไปที่ Admin → Settings → Accounting Integration ก่อน");
                    btnScan.Enabled = false;
                }
                else if (!cfg.CanUseCompanyEndpoints)
                {
                    Msg(litStatus, "warn", "OCR flow ใช้ company endpoints (/api/companies/*) — ต้องตั้ง Company ID และเปิด Nexaacc_Company_Endpoints. แนะนำตั้ง acc_ key แยกในหน้า Accounting Integration (ไม่งั้นจะใช้ int_ ผ่าน X-Api-Key fallback)");
                    btnScan.Enabled = false;
                }
            }
        }

        protected void btnScan_Click(object sender, EventArgs e)
        {
            // OCR ประมวลผล + poll นานได้ → ยืด request timeout ของ ASP.NET (default 110s) กัน "A task was canceled"
            Server.ScriptTimeout = 600;
            litResult.Text = "";
            if (!fuOcr.HasFile)
            {
                Msg(litStatus, "err", "กรุณาเลือกไฟล์ก่อน");
                return;
            }
            // NextAcc จำกัด 10 MB/ไฟล์ — กันส่งไปแล้วโดน reject
            if (fuOcr.PostedFile != null && fuOcr.PostedFile.ContentLength > 10 * 1024 * 1024)
            {
                Msg(litStatus, "err", "ไฟล์ใหญ่เกิน 10 MB — กรุณาบีบ/ลดขนาดไฟล์ก่อน");
                return;
            }

            string tempPath = null;
            try
            {
                var cfg = new AccountingConfig(Conn);
                var client = new AccountingApiClient(cfg, Conn);
                client.ActingUser = Session["username"]?.ToString();   // creator จริง → ลายเซ็น/audit ใน NextAcc

                // บันทึกไฟล์ชั่วคราว
                string ext = Path.GetExtension(fuOcr.FileName);
                tempPath = Path.Combine(Path.GetTempPath(), "ocr_" + Guid.NewGuid().ToString("N") + ext);
                fuOcr.SaveAs(tempPath);

                // อัปโหลดเข้า OCR inbox (ไม่สร้างเอกสารอัตโนมัติ)
                var up = RunSync(() => client.UploadOcrAsync(tempPath, autoCreate: false));
                if (up == null || up.data == null)
                {
                    Msg(litStatus, "err", "อัปโหลด OCR ไม่สำเร็จ: " + (up?.message ?? "ไม่มีข้อมูลตอบกลับ"));
                    return;
                }

                var result = up.data;
                Guid scanId = result.Id;

                // poll จนกว่าจะเสร็จ (สูงสุด ~40 วินาที)
                int tries = 0;
                while (!IsTerminal(result.ScanStatus) && tries < 20)
                {
                    Thread.Sleep(2000);
                    tries++;
                    var poll = RunSync(() => client.GetOcrResultAsync(scanId));
                    if (poll?.data != null) result = poll.data;
                }

                if (string.Equals(result.ScanStatus, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    Msg(litStatus, "err", "OCR ประมวลผลไม่สำเร็จ: " + (result.ProcessingNotes ?? "ไม่ทราบสาเหตุ"));
                    return;
                }
                if (!IsTerminal(result.ScanStatus))
                {
                    Msg(litStatus, "warn", "OCR ยังประมวลผลไม่เสร็จ (สถานะ: " + result.ScanStatus + ") — ลองกดสแกนใหม่อีกครั้งภายหลัง");
                    return;
                }

                BindReview(result);
                hfScanId.Value = scanId.ToString();
                Msg(litStatus, "ok", "สแกนเสร็จแล้ว — ตรวจสอบข้อมูลด้านล่างก่อนสร้างเอกสาร");
            }
            catch (AuthenticationFailedException ax)
            {
                Msg(litStatus, "err", "ยืนยันตัวตน API ล้มเหลว: " + ax.Message);
            }
            catch (AccountingApiException ae)
            {
                Msg(litStatus, "err", "OCR ผิดพลาด (" + ae.StatusCode + "): " + ae.Message);
            }
            catch (Exception ex)
            {
                Msg(litStatus, "err", "เกิดข้อผิดพลาด: " + ex.Message);
            }
            finally
            {
                if (tempPath != null && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* ไฟล์ชั่วคราว — ละเลยถ้าลบไม่ได้ */ }
                }
            }
        }

        protected void btnCreate_Click(object sender, EventArgs e)
        {
            Server.ScriptTimeout = 600;   // create+update+approve หลาย call → ยืด request timeout
            Guid scanId;
            if (!Guid.TryParse(hfScanId.Value, out scanId))
            {
                Msg(litResult, "err", "ไม่พบรายการสแกน — กรุณาอัปโหลด & สแกนใหม่");
                return;
            }

            try
            {
                var cfg = new AccountingConfig(Conn);
                var client = new AccountingApiClient(cfg, Conn);
                client.ActingUser = Session["username"]?.ToString();   // creator จริง → ลายเซ็นผู้จัดทำ/audit
                string target = ddlTargetType.SelectedValue;

                // สร้างเอกสาร Draft จากผล OCR (auto-create Contact จาก TaxId/Name)
                var created = RunSync(() => client.CreateDocumentFromOcrAsync(scanId, target));
                Guid? docId = created?.data?.CreatedDocumentId;
                if (docId == null || docId == Guid.Empty)
                {
                    Msg(litResult, "err", "สร้างเอกสารไม่สำเร็จ: " + (created?.message ?? "NextAcc ไม่คืนรหัสเอกสาร"));
                    return;
                }

                // บังคับแหล่งเงิน + ใช้ค่าที่ผู้ใช้ยืนยัน (PUT แก้ Draft ก่อน approve)
                var upd = BuildUpdateFromReview();

                // ผู้ใช้เลือกผู้ขายจากระบบ (กรณี OCR ไม่เจอชื่อ) → ผูก contact ใน NextAcc (set ContactId)
                if (int.TryParse(ddlVendor.SelectedValue, out var vendorId) && vendorId > 0)
                {
                    try
                    {
                        var sync = new AccountingSyncService(Conn);
                        Guid? contactId = RunSync(() => sync.EnsureVendorContactAsync(vendorId));
                        if (contactId.HasValue && contactId.Value != Guid.Empty)
                        {
                            if (upd == null) upd = new UpdateDocumentRequest();
                            upd.ContactId = contactId.Value;
                        }
                        else
                        {
                            Msg(litResult, "warn", "ผูกผู้ขายจากระบบไม่สำเร็จ (สร้าง contact ใน NextAcc ไม่ได้) — เอกสารจะใช้ผู้ติดต่อจาก OCR");
                        }
                    }
                    catch (Exception vex)
                    {
                        Msg(litResult, "warn", "ผูกผู้ขายจากระบบไม่สำเร็จ: " + vex.Message);
                    }
                }

                if (upd != null)
                {
                    try
                    {
                        RunSync(() => client.UpdateDocumentAsync(docId.Value, upd));
                    }
                    catch (Exception uex)
                    {
                        // เอกสาร Draft ถูกสร้างแล้ว แต่บังคับแหล่งเงิน/ค่าที่แก้ไม่สำเร็จ → ไม่ approve อัตโนมัติ
                        Msg(litResult, "warn", "สร้างเอกสาร (Draft) แล้ว แต่ปรับแหล่งเงิน/ค่าที่แก้ไม่สำเร็จ: " + uex.Message +
                            " — กรุณาตรวจ/อนุมัติใน NextAcc เอง");
                        pnlReview.Visible = false;
                        hfScanId.Value = "";
                        return;
                    }
                }

                // อนุมัติ (auto-post GL). ครั้งแรกถ้ามี soft warning จะได้ 422 → ยืนยันซ้ำ
                string approveMsg;
                try
                {
                    var ap = RunSync(() => client.ApproveDocumentAsync(docId.Value,
                        new ApproveDocumentRequest { AcknowledgeWarnings = false }));
                    approveMsg = ap?.data != null
                        ? "อนุมัติแล้ว เลขที่เอกสาร: " + (ap.data.DocumentNumber ?? docId.Value.ToString())
                        : "สร้างเอกสารแล้ว แต่ผลอนุมัติไม่ชัดเจน: " + (ap?.message ?? "");
                }
                catch (AccountingApiException ae) when (ae.StatusCode == 422)
                {
                    // soft warning → ยืนยันด้วย AcknowledgeWarnings=true
                    var ap2 = RunSync(() => client.ApproveDocumentAsync(docId.Value,
                        new ApproveDocumentRequest { AcknowledgeWarnings = true, Notes = "ยืนยันผ่าน OCR review" }));
                    approveMsg = ap2?.data != null
                        ? "อนุมัติแล้ว (ยืนยันคำเตือน) เลขที่เอกสาร: " + (ap2.data.DocumentNumber ?? docId.Value.ToString())
                        : "อนุมัติแล้ว (ยืนยันคำเตือน)";
                }

                pnlReview.Visible = false;
                hfScanId.Value = "";
                Msg(litResult, "ok", "สำเร็จ! " + approveMsg);
            }
            catch (AccountingApiException ae)
            {
                Msg(litResult, "err", "สร้าง/อนุมัติเอกสารไม่สำเร็จ (" + ae.StatusCode + "): " + ae.Message);
            }
            catch (Exception ex)
            {
                Msg(litResult, "err", "เกิดข้อผิดพลาด: " + ex.Message);
            }
        }

        private void BindReview(OcrResultResponse r)
        {
            pnlReview.Visible = true;
            LoadPaidHowOptions();
            LoadChargeAccountOptions();
            LoadVendorOptions();
            hfDebitAcc.Value = r.SuggestedAccounts?.DebitAccountCode ?? "";
            hfHasWht.Value = r.HasWht ? "1" : "0";
            txtWhtRate.Text = (r.HasWht && r.WhtRate.HasValue) ? r.WhtRate.Value.ToString("0.##", CultureInfo.InvariantCulture) : "";
            txtVendorName.Text = r.ExtractedVendorName ?? "";
            txtVendorTaxId.Text = r.ExtractedVendorTaxId ?? "";
            txtDocNumber.Text = r.ExtractedDocumentNumber ?? "";
            txtDocDate.Text = r.ExtractedDate.HasValue ? r.ExtractedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
            txtSubTotal.Text = r.ExtractedSubTotal.HasValue ? r.ExtractedSubTotal.Value.ToString("0.00") : "";
            txtVat.Text = r.ExtractedVatAmount.HasValue ? r.ExtractedVatAmount.Value.ToString("0.00") : "";
            txtTotal.Text = r.ExtractedTotalAmount.HasValue ? r.ExtractedTotalAmount.Value.ToString("0.00") : "";

            var meta = new StringBuilder();
            if (r.Quality != null)
                meta.Append("<span class=\"badge\" style=\"background:" + Server.HtmlEncode(r.Quality.Color ?? "#888") + "\">คุณภาพ " +
                    Server.HtmlEncode(r.Quality.Letter ?? "?") + " (" + r.Quality.Score + ")</span> ");
            if (!string.IsNullOrEmpty(r.TargetDocumentType))
                meta.Append("<span class=\"ocr-hint\">เอกสารที่ระบบแนะนำให้สร้าง: <b>" + Server.HtmlEncode(r.TargetDocumentType) + "</b></span> ");
            if (r.HasHandwriting)
                meta.Append("<div class=\"ocr-msg warn\">✋ พบลายมือเขียน — โปรดตรวจยอดเงินให้ละเอียด</div>");
            if (r.HasPotentialFixedAsset)
                meta.Append("<div class=\"ocr-msg warn\">พบรายการที่อาจเป็นสินทรัพย์ถาวร — ควรลงทะเบียนสินทรัพย์แทนค่าใช้จ่าย</div>");
            if (r.IsDuplicate)
                meta.Append("<div class=\"ocr-msg warn\">เอกสารนี้อาจซ้ำกับที่เคยสแกนแล้ว</div>");
            litMeta.Text = meta.ToString();

            var sg = new StringBuilder();
            if (r.SuggestedAccounts != null)
            {
                string aiBadge = r.GlAccountUsedAi ? "🤖 AI แนะนำ" : "ระบบแนะนำ (rule-based)";
                sg.Append("<div class=\"ocr-msg info\"><b>ผังบัญชีที่แนะนำ</b> (" + aiBadge + "):<br/>");
                sg.Append("Dr: " + Server.HtmlEncode(JoinAcc(r.SuggestedAccounts.DebitAccountCode, r.SuggestedAccounts.DebitAccountName)) + "<br/>");
                sg.Append("Cr: " + Server.HtmlEncode(JoinAcc(r.SuggestedAccounts.CreditAccountCode, r.SuggestedAccounts.CreditAccountName)));
                if (!string.IsNullOrEmpty(r.SuggestedAccounts.VatAccountCode))
                    sg.Append("<br/>VAT: " + Server.HtmlEncode(JoinAcc(r.SuggestedAccounts.VatAccountCode, r.SuggestedAccounts.VatAccountName)));
                sg.Append("</div>");
            }
            if (r.HasWht && r.WhtRate.HasValue)
                sg.Append("<div class=\"ocr-msg info\">ตรวจพบหัก ณ ที่จ่าย " + r.WhtRate.Value.ToString("0.##") + "%</div>");

            // แสดงรายการหลายบรรทัดที่ OCR แยกมา (read-only) — จะถูกสร้างเป็น multi-line ใน NextAcc
            // ตามนี้ ตราบใดที่ไม่ได้ "เลือกผังบัญชีเดียว/ไม่เคลม VAT/ใส่ WHT" (ซึ่งจะยุบเป็นบรรทัดเดียว)
            if (r.ExtractedItems != null && r.ExtractedItems.Count > 1)
            {
                sg.Append("<div class=\"ocr-msg info\"><b>รายการที่ OCR แยกได้ " + r.ExtractedItems.Count + " รายการ</b> "
                    + "(จะสร้างเป็นหลายบรรทัดตามนี้ ถ้าไม่เลือกผังบัญชีเดียว/ไม่เคลม VAT/ใส่ WHT):"
                    + "<table style=\"width:100%;border-collapse:collapse;margin-top:6px;font-size:12px;\">"
                    + "<tr style=\"background:#eee;\"><th style=\"text-align:left;padding:2px 6px;\">รายละเอียด</th>"
                    + "<th style=\"text-align:right;padding:2px 6px;\">จำนวนเงิน</th>"
                    + "<th style=\"text-align:left;padding:2px 6px;\">ผังบัญชี (OCR)</th></tr>");
                foreach (var it in r.ExtractedItems)
                {
                    decimal amt = it.Amount ?? ((it.Quantity ?? 1) * (it.UnitPrice ?? 0));
                    sg.Append("<tr>"
                        + "<td style=\"padding:2px 6px;border-top:1px solid #ddd;\">" + Server.HtmlEncode(it.Description ?? "") + "</td>"
                        + "<td style=\"padding:2px 6px;border-top:1px solid #ddd;text-align:right;\">" + amt.ToString("N2") + "</td>"
                        + "<td style=\"padding:2px 6px;border-top:1px solid #ddd;\">" + Server.HtmlEncode(it.SuggestedAccountCode ?? "-") + "</td>"
                        + "</tr>");
                }
                sg.Append("</table></div>");
            }
            litSuggested.Text = sg.ToString();
        }

        /// <summary>โหลดตัวเลือกแหล่งจ่ายเงินจาก Account_Paid_How (เฉพาะที่ map Nexaacc_AccountId ไว้)</summary>
        private void LoadPaidHowOptions()
        {
            ddlPaidHow.Items.Clear();
            ddlPaidHow.Items.Add(new ListItem("— ไม่บังคับ (ให้ NextAcc เลือกเอง) —", ""));
            try
            {
                var c = new Take_Time_BangPhra.code();
                var dt = c.DatabaseQuerySafe(Conn,
                    @"SELECT Paid_How, Nexaacc_AccountId FROM Account_Paid_How
                      WHERE Status = 'True' AND Nexaacc_AccountId IS NOT NULL AND LTRIM(RTRIM(Nexaacc_AccountId)) <> ''
                      ORDER BY Paid_How", null);
                if (dt != null)
                    foreach (DataRow row in dt.Rows)
                        ddlPaidHow.Items.Add(new ListItem(
                            row["Paid_How"]?.ToString() ?? "",
                            row["Nexaacc_AccountId"]?.ToString() ?? ""));
            }
            catch { /* ถ้าโหลดไม่ได้ → เหลือแค่ตัวเลือก "ไม่บังคับ" */ }
        }

        /// <summary>โหลดผังบัญชีค่าใช้จ่ายจาก Account_Paid_Type (value = Nexaacc_AccountCode)</summary>
        private void LoadChargeAccountOptions()
        {
            ddlChargeAccount.Items.Clear();
            ddlChargeAccount.Items.Add(new ListItem("— ใช้บัญชีที่ OCR แนะนำ —", ""));
            try
            {
                var c = new Take_Time_BangPhra.code();
                var dt = c.DatabaseQuerySafe(Conn,
                    @"SELECT Paid_Type, Nexaacc_AccountCode FROM Account_Paid_Type
                      WHERE Status = 'True' AND Nexaacc_AccountCode IS NOT NULL AND LTRIM(RTRIM(Nexaacc_AccountCode)) <> ''
                      ORDER BY Paid_Type", null);
                if (dt != null)
                    foreach (DataRow row in dt.Rows)
                    {
                        string code = row["Nexaacc_AccountCode"]?.ToString() ?? "";
                        string name = row["Paid_Type"]?.ToString() ?? "";
                        ddlChargeAccount.Items.Add(new ListItem($"{code} {name}".Trim(), code));
                    }
            }
            catch { /* เหลือแค่ "ใช้บัญชีที่ OCR แนะนำ" */ }
        }

        /// <summary>โหลดรายชื่อผู้ขายจากระบบ TakeTime (Vendor) — กรณี OCR ไม่เจอชื่อ</summary>
        private void LoadVendorOptions()
        {
            ddlVendor.Items.Clear();
            ddlVendor.Items.Add(new ListItem("— ใช้ชื่อจาก OCR —", ""));
            try
            {
                var c = new Take_Time_BangPhra.code();
                var dt = c.DatabaseQuerySafe(Conn,
                    "SELECT ID, Name FROM Vendor WHERE (Status = 'True' OR Status IS NULL) ORDER BY Name", null);
                if (dt != null)
                    foreach (DataRow row in dt.Rows)
                        ddlVendor.Items.Add(new ListItem(row["Name"]?.ToString() ?? "", row["ID"]?.ToString() ?? ""));
            }
            catch { /* เหลือแค่ "ใช้ชื่อจาก OCR" */ }
        }

        /// <summary>สร้าง UpdateDocumentRequest จากค่าในแผงตรวจสอบ: บังคับแหล่งเงิน + วันที่ + เลขที่เอกสาร
        /// + (ถ้าไม่มี WHT) สร้าง line เดียวจากยอดที่แก้. คืน null ถ้าไม่มีอะไรต้องอัปเดต.</summary>
        private UpdateDocumentRequest BuildUpdateFromReview()
        {
            var upd = new UpdateDocumentRequest();
            bool any = false;

            if (Guid.TryParse(ddlPaidHow.SelectedValue, out var payAcc) && payAcc != Guid.Empty)
            {
                upd.PaymentAccountId = payAcc;
                any = true;
            }

            if (DateTime.TryParse(txtDocDate.Text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                upd.DocumentDate = dt;
                any = true;
            }

            string docNo = txtDocNumber.Text.Trim();
            if (!string.IsNullOrEmpty(docNo))
            {
                upd.SupplierInvoiceNumber = docNo;
                upd.Reference = docNo;
                any = true;
            }

            // Lines: โดย default "ไม่ override" → คง multi-line ที่ create-from-OCR สร้างจาก ExtractedItems
            // (NextAcc แตกหลายบรรทัดพร้อมผังบัญชีต่อรายการให้แล้ว). จะ rebuild เป็นบรรทัดเดียวเฉพาะเมื่อ
            // ผู้ใช้ "สั่ง override" จริง: เลือกผังบัญชีเดียว / เลือกไม่เคลม VAT / ใส่ WHT / VAT ผสม
            // (กรณีเหล่านี้ต้องคุมทั้งใบเป็นบัญชี/อัตราเดียว เพราะ OCR ไม่ได้ให้ flag ราย line)
            decimal.TryParse(txtSubTotal.Text.Trim(), out var subTotal);
            decimal.TryParse(txtVat.Text.Trim(), out var vat);
            decimal whtRate = 0m;
            decimal.TryParse(txtWhtRate.Text.Trim(), out whtRate);
            bool userPickedAccount = !string.IsNullOrEmpty(ddlChargeAccount.SelectedValue);
            bool noClaim = ddlVatClaim.SelectedValue == "0";
            bool hasWht = whtRate > 0;
            decimal taxableNet = vat > 0 ? Math.Round(vat / 0.07m, 2, MidpointRounding.AwayFromZero) : 0m;
            decimal nonTaxableNet = Math.Round(subTotal - taxableNet, 2, MidpointRounding.AwayFromZero);
            bool mixedVat = vat > 0 && nonTaxableNet >= 0.01m && taxableNet > 0;
            bool needOverride = userPickedAccount || noClaim || hasWht || mixedVat;

            if (needOverride && subTotal > 0)
            {
                string chargeAcc = ddlChargeAccount.SelectedValue;
                if (string.IsNullOrEmpty(chargeAcc)) chargeAcc = hfDebitAcc.Value;
                if (!string.IsNullOrEmpty(chargeAcc))
                {
                    bool claimVat = !noClaim;
                    string reason = claimVat ? null : "ไม่ขอเครดิตภาษีซื้อ — รวม VAT เข้าค่าใช้จ่าย (§82/5)";

                    // รายการ (description): ถ้าผู้ใช้เลือกผังบัญชี → ใช้ "ชื่อผังบัญชีที่เลือก" เป็นรายการ
                    // (ตรงกับที่ผู้ใช้ต้องการ) + ต่อท้ายด้วยชื่อผู้ขายถ้ามี. ไม่งั้นใช้ ชื่อผู้ขาย+เลขเอกสาร
                    string desc;
                    if (userPickedAccount && ddlChargeAccount.SelectedItem != null)
                    {
                        desc = ddlChargeAccount.SelectedItem.Text;          // เช่น "51110 ซื้อสินค้า/วัตถุดิบ"
                        string code = ddlChargeAccount.SelectedValue;
                        if (!string.IsNullOrEmpty(code) && desc.StartsWith(code))
                            desc = desc.Substring(code.Length).Trim();      // → "ซื้อสินค้า/วัตถุดิบ"
                        string vn = txtVendorName.Text.Trim();
                        if (!string.IsNullOrEmpty(vn)) desc = desc + " - " + vn;
                    }
                    else
                    {
                        desc = (txtVendorName.Text.Trim() + " " + docNo).Trim();
                    }
                    if (string.IsNullOrEmpty(desc)) desc = "ค่าใช้จ่ายตามใบกำกับ (OCR)";
                    upd.PricesIncludeVat = false; // UnitPrice = ยอดก่อน VAT, NextAcc บวก VAT ให้

                    var lines = new List<DocumentLineRequest>();
                    if (mixedVat)
                    {
                        // VAT ผสม → 2 บรรทัด (ยุบเป็นบัญชีเดียวเพื่อให้ VAT ตรง)
                        lines.Add(new DocumentLineRequest { Description = desc + " (ส่วนมีภาษี)", Quantity = 1, UnitPrice = taxableNet, VatRate = 7m, WithholdingTaxRate = whtRate, AccountCode = chargeAcc, IsVatClaimable = claimVat, VatNonClaimableReason = reason });
                        lines.Add(new DocumentLineRequest { Description = desc + " (ส่วนไม่มีภาษี)", Quantity = 1, UnitPrice = nonTaxableNet, VatRate = 0m, WithholdingTaxRate = whtRate, AccountCode = chargeAcc, IsVatClaimable = true });
                    }
                    else
                    {
                        lines.Add(new DocumentLineRequest { Description = desc, Quantity = 1, UnitPrice = subTotal, VatRate = vat > 0 ? 7m : 0m, WithholdingTaxRate = whtRate, AccountCode = chargeAcc, IsVatClaimable = claimVat, VatNonClaimableReason = reason });
                    }
                    upd.Lines = lines;
                    any = true;
                }
            }

            return any ? upd : null;
        }

        private static string JoinAcc(string code, string name)
        {
            if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(name)) return "-";
            if (string.IsNullOrEmpty(name)) return code;
            if (string.IsNullOrEmpty(code)) return name;
            return code + " " + name;
        }

        private static bool IsTerminal(string status)
        {
            if (string.IsNullOrEmpty(status)) return false;
            return status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Failed", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Cached", StringComparison.OrdinalIgnoreCase);
        }

        private void Msg(System.Web.UI.WebControls.Literal lit, string kind, string text)
        {
            lit.Text = "<div class=\"ocr-msg " + kind + "\">" + Server.HtmlEncode(text) + "</div>";
        }
    }
}
