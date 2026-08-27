using System;
using System.Configuration;
using Take_Time_BangPhra.Payments;

namespace Take_Time_BangPhra.Payment
{
    /// <summary>
    /// จุดรับเงินออนไลน์ฝั่งพนักงาน — ใช้กับการขายหน้าร้าน (POS) และยอดทั่วไป
    /// + สร้างลิงก์วางวงเงินประกันความเสียหาย
    ///
    /// ทำไมไม่ผูกเข้า flow บันทึกขายตรง ๆ: หน้า POS บันทึกขาย = ออกเลขเอกสาร + ตัดสต๊อก +
    /// ส่งบัญชีทันที ไม่มีสถานะ "รอจ่าย" — เสียบการรอเงินเข้าไปกลางทางเสี่ยงพังทั้ง flow
    /// จุดนี้จึงทำงานแบบ "เก็บเงินก่อน แล้วพนักงานบันทึกขายตามปกติ" โดยเลือกแหล่งเงิน
    /// ของเกตเวย์ (Omise) ⇒ Product_Out / ใบเสร็จ / rollup / NextAcc เดินเส้นเดิมทั้งหมด
    /// รายการ POS จึงตั้งใจ "ไม่ auto-apply" — เงินรอจับคู่กับการบันทึกขายเสมอ
    /// </summary>
    public partial class Charge : System.Web.UI.Page
    {
        private readonly string _conn =
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected string CurrentTxnRefJs
        {
            get { return JsSafe((string)ViewState["qcRef"]); }
        }
        protected string CurrentHoldRefJs
        {
            get { return JsSafe((string)ViewState["qcHold"]); }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }
            if (!Perm.CanAccess(Perm.SalesPos) && !Perm.CanAccess(Perm.FinReceipt))
            {
                Response.Redirect("~/Default", false);
                System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }

            litPaidHowName.Text = Server.HtmlEncode(
                PaymentGatewayConfig.Get("Payment_PaidHow_Name", "Omise (จ่ายออนไลน์)"));

            var svc = new OnlinePaymentService(_conn);
            if (!svc.IsAvailable)
                Msg("err", "ระบบรับชำระเงินออนไลน์ยังไม่เปิดใช้งาน — เปิดได้ที่ ศูนย์ตั้งค่า → รับชำระเงินออนไลน์");
            else if (!PaymentGatewayConfig.ChannelEnabled(PaymentSource.Pos))
                Msg("err", "ช่องทาง \"ขายหน้าร้าน\" ถูกปิดรับชำระออนไลน์อยู่ — เปิดได้ที่หน้าตั้งค่าเกตเวย์");

            pnlHoldSection.Visible = new SecurityHoldService(_conn).IsAvailable
                || PaymentGatewayConfig.GetBool("Payment_SecurityHold_Enabled", false);

            if (!IsPostBack) BindToday();
        }

        // ── เก็บเงินหน้าร้าน ─────────────────────────────────────────────────

        protected void btnCharge_Click(object sender, EventArgs e)
        {
            decimal amount;
            if (!decimal.TryParse(txtAmount.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out amount) || amount <= 0)
            {
                Msg("err", "กรุณาใส่จำนวนเงินให้ถูกต้อง");
                BindToday();
                return;
            }

            var svc = new OnlinePaymentService(_conn);
            int? adminId = null;
            try { if (Session["UserID"] != null) adminId = Convert.ToInt32(Session["UserID"]); } catch { }

            var req = new PaymentChargeRequest
            {
                Method = PaymentGatewayConfig.MethodQr,   // QR ก่อน — ลิงก์บัตรแนบไปด้วยเสมอ
                SourceType = PaymentSource.Pos,
                SourceId = "POS-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-"
                         + Guid.NewGuid().ToString("N").Substring(0, 4),
                Amount = amount,
                Description = string.IsNullOrWhiteSpace(txtNote.Text) ? "ค่าสินค้า/บริการหน้าร้าน"
                            : txtNote.Text.Trim(),
                CreatedByAdminId = adminId
            };

            // ถ้า QR ไม่ได้เปิดไว้ ใช้บัตรเป็นหลักแทน
            var methods = svc.AvailableMethods(amount, PaymentSource.Pos);
            if (!methods.Contains(PaymentGatewayConfig.MethodQr))
            {
                if (methods.Contains(PaymentGatewayConfig.MethodCard))
                    req.Method = PaymentGatewayConfig.MethodCard;
                else
                {
                    Msg("err", "ไม่มีวิธีชำระออนไลน์ที่เปิดใช้ได้กับยอดนี้ — ตรวจ \"วิธีชำระที่เปิด\" ในหน้าตั้งค่า");
                    BindToday();
                    return;
                }
            }

            PaymentChargeResult r = svc.Start(req);
            if (!r.Success)
            {
                Msg("err", "สร้างรายการไม่สำเร็จ: " + Server.HtmlEncode(r.Message ?? "-"));
                BindToday();
                return;
            }

            ViewState["qcRef"] = r.TxnRef;
            pnlChargeResult.Visible = true;

            if (!string.IsNullOrEmpty(r.QrPayload))
            {
                litQr.Text = "<img src=\"" + Server.HtmlEncode(r.QrPayload) + "\" alt=\"QR พร้อมเพย์\" />";
                litQrCap.Text = "QR พร้อมเพย์ — ตัดยอดอัตโนมัติ";
            }
            else
            {
                litQr.Text = "";
                litQrCap.Text = "";
            }

            string cardLink = !string.IsNullOrEmpty(r.PaymentUrl)
                ? r.PaymentUrl
                : PaymentUrls.SiteBase() + "/Payment/Card?ref=" + Uri.EscapeDataString(r.TxnRef ?? "");
            txtPayLink.Text = cardLink;

            Msg("info", "สร้างรายการ " + Server.HtmlEncode(r.TxnRef ?? "") + " แล้ว — เงินเข้าเมื่อไหร่หน้าจอนี้จะขึ้น ✅ เอง "
                + "จากนั้นไปบันทึกการขายตามปกติ เลือกแหล่งเงิน \"" + litPaidHowName.Text + "\"");
            BindToday();
        }

        // ── วางวงเงินประกัน ──────────────────────────────────────────────────

        protected void btnHold_Click(object sender, EventArgs e)
        {
            int resId; decimal amount;
            if (!int.TryParse(txtHoldRes.Text, out resId) || resId <= 0)
            { Msg("err", "กรุณาใส่เลขที่การจอง"); BindToday(); return; }
            if (!decimal.TryParse(txtHoldAmount.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out amount) || amount <= 0)
            { Msg("err", "กรุณาใส่วงเงินประกันให้ถูกต้อง"); BindToday(); return; }

            int? adminId = null;
            try { if (Session["UserID"] != null) adminId = Convert.ToInt32(Session["UserID"]); } catch { }

            string error;
            string link = new SecurityHoldService(_conn).CreateHoldRequest(resId, amount, adminId, out error);
            if (link == null)
            {
                Msg("err", Server.HtmlEncode(error ?? "สร้างไม่สำเร็จ"));
                BindToday();
                return;
            }

            txtHoldLink.Text = link;
            pnlHoldResult.Visible = true;

            // ดึง hold ref จากลิงก์ไว้ให้ JS poll
            int i = link.IndexOf("hold=", StringComparison.OrdinalIgnoreCase);
            ViewState["qcHold"] = i >= 0 ? Uri.UnescapeDataString(link.Substring(i + 5)) : "";

            Msg("info", "ส่งลิงก์นี้ให้ลูกค้า หรือให้สแกน QR — กันวงเงินสำเร็จหน้าจอจะขึ้น ✅ เอง "
                + "ตอนเช็คเอาท์จัดการตัด/คืนได้ที่หน้าเช็คเอาท์ของการจองนี้");
            BindToday();
        }

        // ── รายการวันนี้ ─────────────────────────────────────────────────────

        private void BindToday()
        {
            try
            {
                var store = new PaymentTransactionStore(_conn);
                gvToday.DataSource = store.Search(DateTime.Today, DateTime.Today, "", "", "", 50);
                gvToday.DataBind();
            }
            catch { gvToday.DataSource = null; gvToday.DataBind(); }
        }

        protected string StatusThai(object s)
        {
            return Server.HtmlEncode(Payments.PaymentStatus.Thai(Convert.ToString(s)));
        }

        private void Msg(string cls, string html)
        {
            litMsg.Text = "<div class=\"qc-alert " + cls + "\">" + html + "</div>";
        }

        private static string JsSafe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
            return sb.ToString();
        }
    }
}
