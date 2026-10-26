using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using Take_Time_BangPhra.Payments.SlipCheck;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// ตั้งค่าตรวจสลิปโอน (PHASE19_34 / CLAUDE.md ข้อ 18) — SlipOK → อ่านยอดจากรูปเอง → ข้ามการตรวจ
    /// ค่าเก็บใน System_Config ผ่าน AppCfg (API key เข้ารหัสด้วย code.Crypt) · บัญชีรับเงินเก็บใน Account_Paid_How
    /// เฉพาะ Owner / Admin ที่มีสิทธิ์รับชำระเงิน
    /// </summary>
    public partial class SlipCheckSettings : Page
    {
        private readonly code _code = new code();
        private string Conn => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        private short? AdminId =>
            Session["UserID"] != null && short.TryParse(Session["UserID"].ToString(), out var v) ? v : (short?)null;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SysPayment)) return;
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            string role = Session["User"]?.ToString();
            if (role != "Owner" && role != "Admin")
            {
                Response.Redirect("~/Default", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            if (!IsPostBack)
            {
                SlipVerificationService.InvalidateSchema();
                LoadValues();
                if (Request.QueryString["saved"] != null) Msg(true, "บันทึกแล้ว — มีผลภายใน 30 วินาที");
            }
            RenderStatus();
            RenderReceivers();
        }

        private void LoadValues()
        {
            chkEnabled.Checked = AppCfg.GetBool(SlipVerificationService.CfgEnabled, false);
            txtBranchId.Text = AppCfg.Get(SlipVerificationService.CfgBranchId, "");
            txtApiKey.Attributes["placeholder"] = string.IsNullOrEmpty(AppCfg.Get(SlipVerificationService.CfgApiKey, ""))
                ? "ยังไม่ได้ตั้ง" : "•••••••• (มีค่าแล้ว — เว้นว่าง = คงเดิม, \"-\" = ล้าง)";
            txtQuota.Text = SlipVerificationService.MonthlyQuota.ToString(CultureInfo.InvariantCulture);
            txtTolerance.Text = SlipVerificationService.Tolerance.ToString("0.00", CultureInfo.InvariantCulture);
            chkReceiver.Checked = SlipVerificationService.CheckReceiver;
            chkLocalOcr.Checked = AppCfg.GetBool("SlipVerify_Local_Ocr", true);
            txtTessPath.Text = AppCfg.Get("SlipVerify_Tesseract_Path", "");
            txtTessdata.Text = AppCfg.Get("SlipVerify_Tessdata_Dir", "");
        }

        private void RenderStatus()
        {
            var sb = new StringBuilder("<div class='sc-status'>");
            bool schema = SlipVerificationService.SchemaReady;
            sb.Append(schema ? SettingsUi.Chip("ฐานข้อมูลพร้อม (PHASE19_34)", "ok")
                             : SettingsUi.Chip("ยังไม่ได้รัน PHASE19_Migration_34_Slip_Verification.sql — ยังไม่บันทึกผลตรวจ", "err"));
            sb.Append(' ').Append(SlipVerificationService.IsEnabled ? SettingsUi.Chip("เปิดตรวจสลิป", "ok") : SettingsUi.Chip("ปิด — พฤติกรรมเดิม", "off"));
            var vendor = SlipVerificationService.CreateVendor();
            sb.Append(' ').Append(vendor.IsConfigured ? SettingsUi.Chip("SlipOK ตั้งค่าแล้ว", "ok") : SettingsUi.Chip("SlipOK ยังไม่ได้ตั้งค่า (ใช้ระดับ ②/③)", "warn"));

            bool exhausted;
            int used = SlipVerificationService.GetUsage(SlipOkVerifier.ProviderName, SlipVerificationService.YearMonth(DateTime.Now), out exhausted);
            int quota = SlipVerificationService.MonthlyQuota;
            string usage = "SlipOK เดือนนี้ใช้ " + used + "/" + quota + (exhausted ? " · SlipOK แจ้งโควต้าหมด" : "");
            sb.Append(' ').Append(SettingsUi.Chip(usage, exhausted || used >= quota ? "warn" : "info"));

            bool qrOk;
            string qr = SlipQrDecoder.EngineStatus(out qrOk);
            sb.Append(' ').Append(SettingsUi.Chip("อ่าน QR: " + qr, qrOk ? "ok" : "err"));
            bool ocrOk;
            string ocr = LocalSlipAmountReader.EngineStatus(out ocrOk);
            sb.Append(' ').Append(SettingsUi.Chip("อ่านยอดจากรูป: " + ocr, ocrOk ? "ok" : "warn"));
            int receivers = SlipVerificationService.LoadReceiverAccounts().Count;
            sb.Append(' ').Append(receivers > 0 ? SettingsUi.Chip("บัญชีรับเงินที่ใช้ตรวจผู้รับ " + receivers + " บัญชี", "ok")
                                                : SettingsUi.Chip("ยังไม่มีเลขบัญชีรับเงิน — ข้ามการตรวจผู้รับ", "warn"));
            sb.Append("</div>");
            litStatus.Text = sb.ToString();
        }

        // ── บัญชีรับเงิน (Account_Paid_How) ───────────────────────────────────

        private bool HasBankCols()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(Conn, "SELECT COL_LENGTH('dbo.Account_Paid_How','Bank_Account_No') AS C", null);
                return dt.Rows.Count > 0 && dt.Rows[0]["C"] != DBNull.Value;
            }
            catch { return false; }
        }

        private DataTable LoadPaidHows(bool withNexa)
        {
            bool nexa = withNexa;
            try
            {
                DataTable c = _code.DatabaseQuerySafe(Conn, "SELECT COL_LENGTH('dbo.Account_Paid_How','Nexaacc_BankAccountId') AS C", null);
                nexa = c.Rows.Count > 0 && c.Rows[0]["C"] != DBNull.Value;
            }
            catch { nexa = false; }
            string sql = "SELECT ph.ID, ph.Paid_How, ph.Bank_Code, ph.Bank_Account_No, ph.PromptPay_Id, "
                + (nexa ? "nb.Account_Number AS Nexa_No, nb.Bank_Name AS Nexa_Bank FROM Account_Paid_How ph "
                          + "LEFT JOIN Accounting_Nexaacc_BankAccounts nb ON nb.Nexaacc_BankAccountId = ph.Nexaacc_BankAccountId"
                        : "NULL AS Nexa_No, NULL AS Nexa_Bank FROM Account_Paid_How ph")
                + " ORDER BY ph.ID";
            return _code.DatabaseQuerySafe(Conn, sql, null);
        }

        private void RenderReceivers()
        {
            if (!HasBankCols())
            {
                litReceivers.Text = "<div class='sc-alert err'>ยังไม่มีคอลัมน์ Bank_Code / Bank_Account_No / PromptPay_Id — รัน PHASE19_34 ก่อน</div>";
                return;
            }
            DataTable dt;
            try { dt = LoadPaidHows(true); }
            catch (Exception ex) { litReceivers.Text = "<div class='sc-alert err'>" + HttpUtility.HtmlEncode(ex.Message) + "</div>"; return; }
            var sb = new StringBuilder("<table class='sc-t'><tr><th>แหล่งเงิน</th><th style='width:90px'>รหัสธนาคาร</th>"
                + "<th>เลขบัญชี</th><th>พร้อมเพย์</th><th>เลขบัญชีจาก NextAcc</th></tr>");
            foreach (DataRow r in dt.Rows)
            {
                string id = Convert.ToString(r["ID"]);
                sb.Append("<tr><td>").Append(HttpUtility.HtmlEncode(Convert.ToString(r["Paid_How"]))).Append("</td>")
                  .Append("<td><input name='rb_code_").Append(id).Append("' maxlength='3' value='").Append(Attr(r["Bank_Code"])).Append("' /></td>")
                  .Append("<td><input name='rb_no_").Append(id).Append("' maxlength='30' value='").Append(Attr(r["Bank_Account_No"])).Append("' /></td>")
                  .Append("<td><input name='rb_pp_").Append(id).Append("' maxlength='20' value='").Append(Attr(r["PromptPay_Id"])).Append("' /></td>")
                  .Append("<td style='color:#607d8b'>").Append(HttpUtility.HtmlEncode((Convert.ToString(r["Nexa_Bank"]) + " " + Convert.ToString(r["Nexa_No"])).Trim()))
                  .Append("</td></tr>");
            }
            sb.Append("</table>");
            litReceivers.Text = sb.ToString();
        }

        private static string Attr(object o) => HttpUtility.HtmlAttributeEncode(o == null || o == DBNull.Value ? "" : o.ToString());

        // ── บันทึก ────────────────────────────────────────────────────────────

        protected void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                AppCfg.Set(SlipVerificationService.CfgEnabled, chkEnabled.Checked ? "1" : "0", AdminId);
                AppCfg.Set(SlipVerificationService.CfgBranchId, (txtBranchId.Text ?? "").Trim(), AdminId);
                string key = (txtApiKey.Text ?? "").Trim();
                if (key == "-") AppCfg.Set(SlipVerificationService.CfgApiKey, null, AdminId);
                else if (key.Length > 0) AppCfg.Set(SlipVerificationService.CfgApiKey, key, AdminId);   // AppCfg เข้ารหัสให้ (SecretKeys)

                int quota;
                if (int.TryParse(txtQuota.Text, out quota) && quota >= 0) AppCfg.Set(SlipVerificationService.CfgQuota, quota.ToString(CultureInfo.InvariantCulture), AdminId);
                decimal tol;
                if (decimal.TryParse(txtTolerance.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out tol) && tol >= 0m && tol <= 100m)
                    AppCfg.Set(SlipVerificationService.CfgTolerance, tol.ToString("0.00", CultureInfo.InvariantCulture), AdminId);
                AppCfg.Set(SlipVerificationService.CfgCheckReceiver, chkReceiver.Checked ? "1" : "0", AdminId);
                AppCfg.Set("SlipVerify_Local_Ocr", chkLocalOcr.Checked ? "1" : "0", AdminId);
                AppCfg.Set("SlipVerify_Tesseract_Path", (txtTessPath.Text ?? "").Trim(), AdminId);
                AppCfg.Set("SlipVerify_Tessdata_Dir", (txtTessdata.Text ?? "").Trim(), AdminId);

                int accounts = SaveReceivers();
                AppCfg.Invalidate();
                _code.Logs(Conn, "SlipVerify", "บันทึกตั้งค่าตรวจสลิป (เปิด=" + chkEnabled.Checked + ", บัญชีรับเงิน " + accounts + " แถว)",
                    Session["UserName"]?.ToString() ?? "ADMIN");
                Response.Redirect(Request.Path + "?saved=1", false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (Exception ex)
            {
                Msg(false, "บันทึกไม่สำเร็จ: " + ex.Message + SettingsUi.StartDirtyMarker);
            }
        }

        private int SaveReceivers()
        {
            if (!HasBankCols()) return 0;
            int n = 0;
            DataTable dt = LoadPaidHows(false);
            using (var con = new SqlConnection(Conn))
            {
                con.Open();
                foreach (DataRow r in dt.Rows)
                {
                    string id = Convert.ToString(r["ID"]);
                    if (Request.Form["rb_no_" + id] == null) continue;
                    string code3 = Digits(Request.Form["rb_code_" + id], 3);
                    string no = Digits(Request.Form["rb_no_" + id], 30);
                    string pp = Digits(Request.Form["rb_pp_" + id], 20);
                    using (var cmd = new SqlCommand("UPDATE Account_Paid_How SET Bank_Code = @c, Bank_Account_No = @n, PromptPay_Id = @p WHERE ID = @id", con))
                    {
                        cmd.Parameters.AddWithValue("@c", code3.Length == 0 ? (object)DBNull.Value : code3);
                        cmd.Parameters.AddWithValue("@n", no.Length == 0 ? (object)DBNull.Value : no);
                        cmd.Parameters.AddWithValue("@p", pp.Length == 0 ? (object)DBNull.Value : pp);
                        cmd.Parameters.AddWithValue("@id", r["ID"]);
                        n += cmd.ExecuteNonQuery();
                    }
                }
            }
            return n;
        }

        private static string Digits(string s, int max)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "") if (char.IsDigit(c) && sb.Length < max) sb.Append(c);
            return sb.ToString();
        }

        protected void btnResetQuota_Click(object sender, EventArgs e)
        {
            SlipVerificationService.ResetExhausted(SlipOkVerifier.ProviderName);
            Msg(true, "ล้างธงโควต้าหมดของเดือนนี้แล้ว — ครั้งถัดไปจะลองเรียก SlipOK อีกครั้ง");
            RenderStatus();
        }

        protected void btnQuota_Click(object sender, EventArgs e)
        {
            var v = SlipVerificationService.CreateVendor() as SlipOkVerifier;
            if (v == null || !v.IsConfigured) { Msg(false, "ยังไม่ได้ตั้ง SlipOK Branch ID / API key"); return; }
            string raw = v.CheckQuotaRaw() ?? "-";
            var q = SlipOkVerifier.ParseQuota(raw);
            if (!q.Ok)
            {
                litMsg.Text = "<div class='sc-alert err'>เช็คโควต้า SlipOK ไม่สำเร็จ: " + HttpUtility.HtmlEncode(q.Error.Length > 300 ? q.Error.Substring(0, 300) : q.Error) + "</div>";
                return;
            }
            // คงเหลือ 0 (รวมโควต้าพิเศษ) → ธงหมดของเดือนนี้ ระบบไปใช้ OCR ในเครื่องทันที (กันใช้เกินโควต้า = error 1004 / อาจมีค่าใช้จ่าย)
            if (q.Remaining <= 0) SlipVerificationService.MarkQuotaExhausted(SlipOkVerifier.ProviderName);
            var sbq = new StringBuilder("<div class='sc-alert ").Append(q.Remaining > 0 && q.OverQuota == 0 ? "ok" : "err").Append("'>โควต้า SlipOK คงเหลือ <b>")
                .Append(q.Quota).Append("</b>");
            if (q.SpecialQuota > 0 || !string.IsNullOrEmpty(q.SpecialEndDate))
                sbq.Append(" · โควต้าพิเศษ ").Append(q.SpecialQuota).Append(string.IsNullOrEmpty(q.SpecialEndDate) ? "" : " (หมดอายุ " + HttpUtility.HtmlEncode(q.SpecialEndDate) + ")");
            if (!string.IsNullOrEmpty(q.EndDate)) sbq.Append(" · แพ็กเกจหมดอายุ ").Append(HttpUtility.HtmlEncode(q.EndDate));
            if (q.OverQuota > 0) sbq.Append("<br>⚠ ใช้เกินโควต้าแล้ว ").Append(q.OverQuota).Append(" ครั้ง — ตรวจค่าใช้จ่ายในแพ็กเกจ SlipOK");
            if (q.Remaining <= 0) sbq.Append("<br>ตั้งธง \"โควต้าหมด\" ของเดือนนี้แล้ว — ระบบจะอ่านยอดจากรูปในเครื่องแทน");
            litMsg.Text = sbq.Append("</div>").ToString();
            RenderStatus();
        }

        protected void btnTest_Click(object sender, EventArgs e)
        {
            if (!fuTest.HasFile) { Msg(false, "เลือกรูปสลิปก่อน"); return; }
            decimal amt;
            decimal? expected = decimal.TryParse(txtTestAmount.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out amt) && amt > 0m ? amt : (decimal?)null;
            byte[] bytes = fuTest.FileBytes;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SlipInspection insp = SlipVerificationService.Inspect(bytes, fuTest.FileName, false);
            var r = SlipVerificationService.Evaluate(insp, expected, new SlipVerifyContext { SourceType = "TEST", User = Session["UserName"]?.ToString() });
            sw.Stop();

            var sb = new StringBuilder(r.ToHtml());
            sb.Append("<div class='sub' style='margin-top:8px'>เวลา ").Append(sw.ElapsedMilliseconds).Append(" ms · hash ")
              .Append(HttpUtility.HtmlEncode(insp.FileHash.Substring(0, 12))).Append("</div>");
            sb.Append("<pre class='sc-raw'>");
            sb.Append("QR: ").Append(HttpUtility.HtmlEncode(insp.Qr == null ? "ไม่พบ (" + (SlipQrDecoder.LastEngineError ?? "-") + ")"
                : (insp.Qr.IsPaymentQr ? "QR รับเงิน (ไม่ใช่สลิป)" : "ธนาคาร " + insp.Qr.SendingBank + " · transRef " + insp.Qr.TransRef
                   + " · CRC " + (insp.Qr.CrcValid ? "ถูก" : "ผิด"))));
            sb.Append("\nSlipOK: ").Append(HttpUtility.HtmlEncode(insp.Vendor == null ? "ไม่ได้เรียก — " + (insp.VendorSkipReason ?? "-")
                : insp.Vendor.Kind + " code=" + (insp.Vendor.Code ?? "-") + " " + (insp.Vendor.Message ?? "") + " (" + insp.Vendor.ElapsedMs + " ms)"));
            sb.Append("\nOCR ในเครื่อง: ").Append(HttpUtility.HtmlEncode(!insp.OcrAttempted ? "ไม่ได้ใช้ (SlipOK ได้ยอดแล้ว)"
                : (insp.OcrText != null ? "อ่านได้ " + insp.OcrText.Length + " ตัวอักษร" : "ล้ม — " + insp.OcrError)));
            if (insp.OcrResult != null)
            {
                // รายละเอียดสำหรับแอดมิน: ข้อความแต่ละรอบ, ป้าย, ผู้สมัคร, ROI, โหวต (ตัดสินด้วยยอดทดสอบที่กรอก)
                string diag = insp.OcrResult.Diagnostics(expected);
                sb.Append("\n──────── OCR ในเครื่อง: รอบ/ROI/โหวต ────────\n").Append(HttpUtility.HtmlEncode(diag.Length > 6000 ? diag.Substring(0, 6000) : diag));
            }
            else if (!string.IsNullOrEmpty(insp.OcrText))
                sb.Append("\n──────── ข้อความ OCR ────────\n").Append(HttpUtility.HtmlEncode(insp.OcrText.Length > 1500 ? insp.OcrText.Substring(0, 1500) : insp.OcrText));
            sb.Append("</pre>");
            if (insp.OcrResult != null && insp.OcrResult.MontagePng != null && insp.OcrResult.MontagePng.Length < 400000)
                sb.Append("<div class='sub' style='margin-top:8px'>ภาพ ROI ที่ส่งอ่านรอบ 2 (บน = เทา, ล่าง = ขาวดำ ของแต่ละกรอบ)</div>")
                  .Append("<img alt='ROI' style='max-width:100%;border:1px solid #dde;border-radius:6px;background:#fff' src='data:image/png;base64,")
                  .Append(Convert.ToBase64String(insp.OcrResult.MontagePng)).Append("' />");
            litTest.Text = sb.ToString();
            RenderStatus();
        }

        private void Msg(bool ok, string text)
        {
            litMsg.Text = "<div class='sc-alert " + (ok ? "ok" : "err") + "'>" + (ok ? HttpUtility.HtmlEncode(text) : text) + "</div>";
        }
    }
}
