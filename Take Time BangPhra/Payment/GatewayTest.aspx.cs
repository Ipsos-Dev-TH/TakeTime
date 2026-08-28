using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Payments;

namespace Take_Time_BangPhra.Payment
{
    /// <summary>
    /// คอนโซลทดสอบเกตเวย์ (sandbox) — ไล่ครบวงจรด้วยคีย์ test ก่อนใช้คีย์จริง:
    /// สร้าง QR/ลิงก์บัตร → จ่ายด้วยบัตรทดสอบ → ดูสถานะ/คำตอบดิบ → คืนเงิน
    /// และวงเงินประกัน: กัน → ตัดครึ่ง (ดูส่วนเหลือคืนอัตโนมัติ) → คืนทั้งหมด
    ///
    /// ใช้ service ตัวจริงทุกตัว — สิ่งที่ทดสอบคือของที่จะรันจริงเป๊ะ ๆ
    /// คีย์ LIVE: ปุ่มถูกล็อกจนกว่าจะติ๊กยืนยันว่ารู้ว่าเงินจะถูกตัดจริง
    /// </summary>
    public partial class GatewayTest : System.Web.UI.Page
    {
        private readonly string _conn =
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        private const string TestSourceId = "GWTEST";   // แยกรายการทดสอบออกจากของจริง

        private bool IsLive
        {
            get
            {
                return PaymentGatewayConfig.ActiveProvider == PaymentGatewayConfig.ProviderOmise
                    ? !OmiseGateway.IsTestKey
                    : !PaymentGatewayConfig.IsSandbox;
            }
        }

        private bool ButtonsAllowed { get { return !IsLive || chkLiveOk.Checked; } }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }
            if (!Perm.CanAccess(Perm.SysPayment) && !Perm.CanAccess(Perm.SysSettings))
            {
                Response.Redirect("~/Default", false);
                System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }

            var svc = new OnlinePaymentService(_conn);
            litProvider.Text = Server.HtmlEncode(svc.Gateway().DisplayName);
            litMode.Text = IsLive
                ? "<span class=\"gt-mode live\">LIVE — เงินจริง</span>"
                : "<span class=\"gt-mode test\">TEST — ไม่ตัดเงินจริง</span>";
            pnlLiveGuard.Visible = IsLive;
            ApplyGuard();

            if (!svc.IsAvailable)
                Msg("err", "ระบบชำระเงินยังไม่เปิด (Feature_OnlinePayment / Payment_Enabled) — เปิดก่อนจึงทดสอบได้");

            if (!IsPostBack) BindTest();
        }

        protected void chkLiveOk_Changed(object sender, EventArgs e) { ApplyGuard(); BindTest(); }

        private void ApplyGuard()
        {
            bool ok = ButtonsAllowed;
            btnTestQr.Enabled = ok;
            btnTestCard.Enabled = ok;
            btnTestHold.Enabled = ok;
        }

        // ── สร้างรายการทดสอบ ─────────────────────────────────────────────────

        private decimal ReadAmount()
        {
            decimal a;
            if (!decimal.TryParse(txtAmount.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out a) || a <= 0) a = 20m;
            return a;
        }

        private int? AdminId()
        {
            try { return Session["UserID"] != null ? (int?)Convert.ToInt32(Session["UserID"]) : null; }
            catch { return null; }
        }

        private void StartTest(string method)
        {
            if (!ButtonsAllowed) { Msg("err", "คีย์ LIVE — ติ๊กยืนยันก่อน"); return; }

            var svc = new OnlinePaymentService(_conn);
            var req = new PaymentChargeRequest
            {
                Method = method,
                SourceType = PaymentSource.Other,
                SourceId = TestSourceId + "-" + DateTime.Now.ToString("HHmmss"),
                Amount = ReadAmount(),
                Description = "[ทดสอบ] รายการทดสอบเกตเวย์",
                CreatedByAdminId = AdminId()
            };

            PaymentChargeResult r = svc.Start(req);
            if (!r.Success)
            {
                Msg("err", "สร้างไม่สำเร็จ: " + Server.HtmlEncode(r.Message ?? "-"));
                ShowRaw(r.RawRequest, r.RawResponse);
                BindTest();
                return;
            }

            pnlResult.Visible = true;
            litQr.Text = string.IsNullOrEmpty(r.QrPayload)
                ? "" : "<img src=\"" + Server.HtmlEncode(r.QrPayload) + "\" alt=\"QR\" />";
            txtLink.Text = !string.IsNullOrEmpty(r.PaymentUrl)
                ? r.PaymentUrl
                : PaymentUrls.SiteBase() + "/Payment/Card?ref=" + Uri.EscapeDataString(r.TxnRef ?? "");

            Msg("ok", "สร้างรายการ " + Server.HtmlEncode(r.TxnRef ?? "") + " แล้ว — "
                + (method == PaymentGatewayConfig.MethodQr
                    ? "สแกน QR ด้วยแอปธนาคาร (โหมด test ใช้ปุ่ม mark-as-paid ใน Omise Dashboard ได้)"
                    : "เปิดลิงก์แล้วกรอกบัตรทดสอบ 4242 4242 4242 4242"));
            ShowRaw(r.RawRequest, r.RawResponse);
            BindTest();
        }

        protected void btnTestQr_Click(object sender, EventArgs e) { StartTest(PaymentGatewayConfig.MethodQr); }
        protected void btnTestCard_Click(object sender, EventArgs e) { StartTest(PaymentGatewayConfig.MethodCard); }

        protected void btnTestHold_Click(object sender, EventArgs e)
        {
            if (!ButtonsAllowed) { Msg("err", "คีย์ LIVE — ติ๊กยืนยันก่อน"); return; }

            var holds = new SecurityHoldService(_conn);
            if (!holds.IsAvailable)
            {
                Msg("err", "ระบบวงเงินประกันยังไม่เปิด (Payment_SecurityHold_Enabled) — เปิดก่อนจึงทดสอบได้");
                return;
            }

            // ใช้เลขการจองปลอมติดลบไม่ได้ (คอลัมน์ INT ปกติ) — ใช้ 0 = รายการทดสอบ
            string error;
            string link = holds.CreateHoldRequest(0, ReadAmount(), AdminId(), out error);
            if (link == null) { Msg("err", Server.HtmlEncode(error ?? "-")); BindTest(); return; }

            pnlResult.Visible = true;
            litQr.Text = "";
            txtLink.Text = link;
            Msg("ok", "สร้างคำขอกันวงเงินทดสอบแล้ว — เปิดลิงก์ กรอกบัตรทดสอบ แล้วกลับมากด \"ตรวจสถานะ\" "
                + "จากนั้นลอง \"ตัดครึ่ง\" หรือ \"คืนวงเงิน\" จากตารางข้างล่าง");
            BindTest();
        }

        protected void btnConn_Click(object sender, EventArgs e)
        {
            var gw = new OnlinePaymentService(_conn).Gateway();
            pnlRaw.Visible = true;
            litRaw.Text = Server.HtmlEncode(gw.TestConnection());
            BindTest();
        }

        // ── ตารางรายการทดสอบ + ปุ่มจัดการ ────────────────────────────────────

        protected void gvTest_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            string key = Convert.ToString(e.CommandArgument) ?? "";
            var svc = new OnlinePaymentService(_conn);
            var holds = new SecurityHoldService(_conn);

            try
            {
                if (key.StartsWith("T:"))          // Payment_Transaction
                {
                    int id = int.Parse(key.Substring(2));
                    PaymentTransaction t = svc.Store.GetById(id);
                    if (t == null) { Msg("err", "ไม่พบรายการ"); BindTest(); return; }

                    switch (e.CommandName)
                    {
                        case "Check":
                            Msg("ok", Server.HtmlEncode(svc.RefreshStatus(t)));
                            break;
                        case "Raw":
                            ShowRaw(null, ReadRaw(id));
                            break;
                        case "Refund":
                            if (!ButtonsAllowed) { Msg("err", "คีย์ LIVE — ติ๊กยืนยันก่อน"); break; }
                            decimal refundable = t.TotalPayable - svc.Store.GetRefundedAmount(t.ID);
                            Msg("ok", Server.HtmlEncode(
                                svc.RefundTransaction(t.ID, refundable, "[ทดสอบ] คืนเงินรายการทดสอบ", AdminId())));
                            break;
                    }
                }
                else if (key.StartsWith("H:"))     // Payment_Security_Holds
                {
                    long id = long.Parse(key.Substring(2));
                    var h = holds.GetById(id);
                    if (h == null) { Msg("err", "ไม่พบรายการวงเงิน"); BindTest(); return; }

                    switch (e.CommandName)
                    {
                        case "Check":
                            Msg("ok", "สถานะ: " + Server.HtmlEncode(HoldStatus.Thai(h.Status))
                                + (h.ExpiresAt.HasValue
                                    ? " · หมดอายุ " + h.ExpiresAt.Value.ToString("dd/MM HH:mm") : ""));
                            break;
                        case "Raw":
                            ShowRaw(null, ReadHoldRaw(id));
                            break;
                        case "CapHalf":
                            if (!ButtonsAllowed) { Msg("err", "คีย์ LIVE — ติ๊กยืนยันก่อน"); break; }
                            Msg("ok", Server.HtmlEncode(holds.CaptureDamage(id,
                                Math.Round(h.Amount / 2m, 2), "[ทดสอบ] ตัดครึ่งวงเงิน", AdminId())));
                            break;
                        case "Release":
                            if (!ButtonsAllowed) { Msg("err", "คีย์ LIVE — ติ๊กยืนยันก่อน"); break; }
                            Msg("ok", Server.HtmlEncode(holds.Release(id, AdminId())));
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Msg("err", "ดำเนินการไม่สำเร็จ: " + Server.HtmlEncode(ex.Message));
            }
            BindTest();
        }

        private void BindTest()
        {
            var rows = new DataTable();
            rows.Columns.Add("Key", typeof(string));
            rows.Columns.Add("T", typeof(DateTime));
            rows.Columns.Add("Kind", typeof(string));
            rows.Columns.Add("Ref", typeof(string));
            rows.Columns.Add("Amount", typeof(decimal));
            rows.Columns.Add("StatusThai", typeof(string));
            rows.Columns.Add("CanRefund", typeof(bool));
            rows.Columns.Add("CanHoldOps", typeof(bool));

            try
            {
                var code = new code();
                // รายการชำระเงินทดสอบวันนี้ (SourceId ขึ้นต้น GWTEST)
                var t = code.DatabaseQuerySafe(_conn, @"
                    SELECT TOP 30 ID, Created_Date, Txn_Ref, Amount, Surcharge_Amount, [Status]
                      FROM Payment_Transaction
                     WHERE Source_ID LIKE @p AND Created_Date >= CAST(GETDATE() AS DATE)
                     ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@p", TestSourceId + "%" } });
                if (t != null)
                    foreach (DataRow r in t.Rows)
                        rows.Rows.Add("T:" + r["ID"], r["Created_Date"], "ชำระเงิน", r["Txn_Ref"],
                            Convert.ToDecimal(r["Amount"]) + Convert.ToDecimal(r["Surcharge_Amount"]),
                            Payments.PaymentStatus.Thai(Convert.ToString(r["Status"])),
                            Convert.ToString(r["Status"]) == Payments.PaymentStatus.Paid, false);

                // วงเงินทดสอบ (Reservation_ID = 0)
                var h = code.DatabaseQuerySafe(_conn, @"
                    SELECT TOP 30 ID, Created_Date, Hold_Ref, Amount, [Status]
                      FROM Payment_Security_Holds
                     WHERE Reservation_ID = 0 AND Created_Date >= CAST(GETDATE() AS DATE)
                     ORDER BY ID DESC", null);
                if (h != null)
                    foreach (DataRow r in h.Rows)
                        rows.Rows.Add("H:" + r["ID"], r["Created_Date"], "วงเงินประกัน", r["Hold_Ref"],
                            Convert.ToDecimal(r["Amount"]),
                            HoldStatus.Thai(Convert.ToString(r["Status"])),
                            false, Convert.ToString(r["Status"]) == HoldStatus.Held);
            }
            catch { }

            gvTest.DataSource = rows;
            gvTest.DataBind();
        }

        // ── raw viewer ───────────────────────────────────────────────────────

        private string ReadRaw(int txnId)
        {
            try
            {
                var dt = new code().DatabaseQuerySafe(_conn,
                    "SELECT Raw_Request, Raw_Response FROM Payment_Transaction WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", txnId } });
                if (dt == null || dt.Rows.Count == 0) return "(ไม่มีข้อมูล)";
                return "── คำขอ ──\n" + Convert.ToString(dt.Rows[0]["Raw_Request"])
                     + "\n\n── คำตอบ ──\n" + Convert.ToString(dt.Rows[0]["Raw_Response"]);
            }
            catch (Exception ex) { return ex.Message; }
        }

        private string ReadHoldRaw(long holdId)
        {
            try
            {
                var dt = new code().DatabaseQuerySafe(_conn,
                    "SELECT Raw_Response FROM Payment_Security_Holds WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", holdId } });
                if (dt == null || dt.Rows.Count == 0) return "(ไม่มีข้อมูล)";
                return Convert.ToString(dt.Rows[0]["Raw_Response"]);
            }
            catch (Exception ex) { return ex.Message; }
        }

        private void ShowRaw(string rawReq, string rawRes)
        {
            if (string.IsNullOrEmpty(rawReq) && string.IsNullOrEmpty(rawRes)) return;
            pnlRaw.Visible = true;
            string text = (string.IsNullOrEmpty(rawReq) ? "" : "── คำขอ ──\n" + rawReq + "\n\n")
                        + (string.IsNullOrEmpty(rawRes) ? "" : (rawReq == null ? "" : "── คำตอบ ──\n") + rawRes);
            litRaw.Text = Server.HtmlEncode(text);
        }

        private void Msg(string cls, string html)
        {
            litMsg.Text = "<div class=\"gt-alert " + cls + "\">" + html + "</div>";
        }
    }
}
