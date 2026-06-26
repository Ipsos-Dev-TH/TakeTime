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
            litResult.Text = "";
            if (!fuOcr.HasFile)
            {
                Msg(litStatus, "err", "กรุณาเลือกไฟล์ก่อน");
                return;
            }

            string tempPath = null;
            try
            {
                var cfg = new AccountingConfig(Conn);
                var client = new AccountingApiClient(cfg, Conn);

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
            hfDebitAcc.Value = r.SuggestedAccounts?.DebitAccountCode ?? "";
            hfHasWht.Value = r.HasWht ? "1" : "0";
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

            // สร้าง line จากยอดที่แก้ — เฉพาะเมื่อไม่มี WHT (มี WHT → คง line จาก OCR ไม่ทิ้งการตั้งหัก ณ ที่จ่าย)
            // ผังบัญชี: ใช้ที่ผู้ใช้เลือก (ddlChargeAccount) ก่อน, ไม่งั้น fallback บัญชีที่ OCR แนะนำ
            // VAT: เคลม (IsVatClaimable=true → Dr ภาษีซื้อ) / ไม่เคลม (false → NextAcc รวม VAT เข้าค่าใช้จ่าย §82/5)
            string chargeAcc = ddlChargeAccount.SelectedValue;
            if (string.IsNullOrEmpty(chargeAcc)) chargeAcc = hfDebitAcc.Value;
            bool hasWht = hfHasWht.Value == "1";
            if (!hasWht && !string.IsNullOrEmpty(chargeAcc)
                && decimal.TryParse(txtSubTotal.Text.Trim(), out var subTotal) && subTotal > 0)
            {
                decimal.TryParse(txtVat.Text.Trim(), out var vat);
                bool claimVat = ddlVatClaim.SelectedValue != "0";
                string desc = (txtVendorName.Text.Trim() + " " + docNo).Trim();
                if (string.IsNullOrEmpty(desc)) desc = "ค่าใช้จ่ายตามใบกำกับ (OCR)";
                upd.PricesIncludeVat = false; // UnitPrice = ยอดก่อน VAT, NextAcc บวก VAT ให้
                upd.Lines = new List<DocumentLineRequest>
                {
                    new DocumentLineRequest
                    {
                        Description = desc,
                        Quantity = 1,
                        UnitPrice = subTotal,
                        VatRate = vat > 0 ? 7m : 0m,
                        AccountCode = chargeAcc,
                        IsVatClaimable = claimVat,
                        VatNonClaimableReason = claimVat ? null : "ไม่ขอเครดิตภาษีซื้อ — รวม VAT เข้าค่าใช้จ่าย (§82/5)"
                    }
                };
                any = true;
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
