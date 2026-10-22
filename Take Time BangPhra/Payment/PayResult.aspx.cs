using System;
using System.Configuration;
using Take_Time_BangPhra.Payments;

namespace Take_Time_BangPhra.Payment
{
    /// <summary>
    /// หน้าที่ลูกค้ากลับมาหลังจ่ายเงินที่ระบบของผู้ให้บริการ
    ///
    /// ⚠ หน้านี้ "ไม่ใช่" หลักฐานการชำระเงิน — ลูกค้ากลับมาที่นี่ได้โดยที่ยังไม่ได้จ่าย
    ///    (กดปุ่มย้อนกลับ / ปิดหน้าจอกลางคัน) ระบบจึงไปถามสถานะจากเกตเวย์จริงเสมอ
    ///    การตัดสินว่า "จ่ายแล้ว" ยึดตามคำตอบของเกตเวย์ + การแจ้งกลับ (webhook) เท่านั้น
    /// </summary>
    public partial class PayResult : System.Web.UI.Page
    {
        private readonly string _conn =
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        private string TxnRef
        {
            get { return (string)ViewState["ref"]; }
            set { ViewState["ref"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                TxnRef = (Request.QueryString["ref"] ?? "").Trim();
                Show();
            }
        }

        protected void btnRecheck_Click(object sender, EventArgs e)
        {
            Show();
        }

        private void Show()
        {
            litRef.Text = string.IsNullOrEmpty(TxnRef) ? "" : "เลขอ้างอิง: " + Server.HtmlEncode(TxnRef);

            bool cancelled = Request.QueryString["cancelled"] == "1";

            var svc = new OnlinePaymentService(_conn);
            if (!svc.IsAvailable || string.IsNullOrEmpty(TxnRef))
            {
                Render("wait", "⏳", "ไม่พบข้อมูลการชำระเงิน",
                    "หากท่านได้ชำระเงินไปแล้ว กรุณาติดต่อเจ้าหน้าที่พร้อมแจ้งเลขอ้างอิง");
                return;
            }

            PaymentTransaction txn = svc.Store.GetByRef(TxnRef);
            if (txn == null)
            {
                Render("wait", "⏳", "ไม่พบรายการชำระเงินนี้",
                    "กรุณาติดต่อเจ้าหน้าที่พร้อมแจ้งเลขอ้างอิงด้านล่าง");
                return;
            }

            // ถามเกตเวย์ให้แน่ใจ ไม่เชื่อพารามิเตอร์ที่ติดกลับมากับ URL
            if (txn.Status != PaymentStatus.Paid)
            {
                try { svc.RefreshStatus(txn); } catch { }
                txn = svc.Store.GetByRef(TxnRef) ?? txn;
            }

            switch (txn.Status)
            {
                case PaymentStatus.Paid:
                    Render("ok", "✅", "ชำระเงินเรียบร้อยแล้ว",
                        "ยอดชำระ ฿" + txn.TotalPayable.ToString("N2")
                        + (string.IsNullOrEmpty(txn.ReceiptId) ? "" : "<br/>เลขที่ใบเสร็จ " + Server.HtmlEncode(txn.ReceiptId))
                        + "<br/>ขอบคุณที่ใช้บริการค่ะ");
                    break;

                case PaymentStatus.Failed:
                    Render("bad", "❌", "การชำระเงินไม่สำเร็จ",
                        string.IsNullOrEmpty(txn.FailReason)
                            ? "กรุณาลองใหม่อีกครั้ง หรือเลือกวิธีชำระเงินอื่น"
                            : Server.HtmlEncode(txn.FailReason));
                    ShowRetry(txn);
                    break;

                case PaymentStatus.Expired:
                    Render("bad", "⌛", "รายการหมดอายุแล้ว",
                        "กรุณาเริ่มรายการชำระเงินใหม่อีกครั้ง");
                    ShowRetry(txn);
                    break;

                case PaymentStatus.Cancelled:
                    Render("bad", "✖", "ยกเลิกการชำระเงิน",
                        "ท่านสามารถเริ่มรายการใหม่ได้ตลอดเวลา");
                    ShowRetry(txn);
                    break;

                default:
                    if (cancelled)
                    {
                        Render("wait", "✖", "ท่านยกเลิกการชำระเงิน",
                            "ยังไม่มีการตัดเงิน ท่านสามารถเริ่มรายการใหม่ได้");
                        ShowRetry(txn);
                    }
                    else
                        Render("wait", "⏳", "กำลังรอผลการชำระเงิน",
                            "ธนาคารอาจใช้เวลาสักครู่ กรุณากด \"ตรวจสอบอีกครั้ง\" ในอีก 1-2 นาที");
                    btnRecheck.Visible = true;
                    break;
            }
        }

        /// <summary>
        /// เปิดปุ่ม "เริ่มรายการใหม่" — เดิมหน้าจบแบบทางตัน ลูกค้าต้องโทรขอลิงก์ใหม่
        /// จากเจ้าหน้าที่ ทั้งที่ลิงก์หน้าเลือกวิธีจ่ายผูกกับ "รายการต้นทาง" อยู่แล้ว
        /// เปิดใหม่ได้ตลอด (ยอดคำนวณสดจากยอดค้างจริง)
        /// </summary>
        private void ShowRetry(PaymentTransaction txn)
        {
            try
            {
                if (txn == null || string.IsNullOrEmpty(txn.SourceType)
                    || string.IsNullOrEmpty(txn.SourceId)) return;

                // POS เป็นยอดลอย ๆ ที่พนักงานตั้งเอง เปิดซ้ำไม่ได้ (ไม่มีต้นทางให้อ้าง)
                if (txn.SourceType == PaymentSource.Pos || txn.SourceType == PaymentSource.Other) return;

                lnkRetry.NavigateUrl = PaymentUrls.SiteBase()
                    + "/Payment/Pay?src=" + Uri.EscapeDataString(txn.SourceType)
                    + "&id=" + Uri.EscapeDataString(txn.SourceId)
                    + "&ph=" + Uri.EscapeDataString(txn.CustomerPhone ?? "");
                lnkRetry.Visible = true;
            }
            catch { }
        }

        private void Render(string cls, string icon, string title, string detail)
        {
            pnlBox.CssClass = "card " + cls;
            litIcon.Text = icon;
            litTitle.Text = Server.HtmlEncode(title);
            litDetail.Text = detail;   // ผู้เรียกเข้ารหัสส่วนที่มาจากข้อมูลภายนอกมาแล้ว
        }
    }
}
