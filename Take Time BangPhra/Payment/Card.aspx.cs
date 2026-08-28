using System;
using System.Configuration;
using Take_Time_BangPhra.Payments;

namespace Take_Time_BangPhra.Payment
{
    /// <summary>
    /// หน้ากรอกบัตรของลูกค้า — ใช้ Omise.js ส่งข้อมูลบัตรตรงเข้า vault ของ Omise
    /// ด้วย Public Key แล้วส่งกลับมาแค่ token ใช้ครั้งเดียว (ข้อมูลบัตรไม่ผ่านเราเลย)
    ///
    /// สองโหมด:
    ///   /Payment/Card?ref=TT-...            → ชำระเงินรายการที่สร้างไว้ (ตัดเงินจริง)
    ///   /Payment/Card?mode=HOLD&amp;hold=HOLD-... → วางวงเงินประกันความเสียหาย (กันวงเงิน ไม่ตัดเงิน)
    ///
    /// ลิงก์นี้คือสิ่งที่ส่งให้ลูกค้า "กรอกเอง" ได้ ทั้งทางแชท/SMS หรือให้สแกน QR
    /// </summary>
    public partial class Card : System.Web.UI.Page
    {
        private readonly string _conn =
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected string PublicKeyJs
        {
            get
            {
                // ใส่ลง JS string — กรองให้เหลืออักขระของคีย์จริงเท่านั้น กัน injection
                string k = OmiseGateway.PublicKey ?? "";
                var sb = new System.Text.StringBuilder();
                foreach (char c in k)
                    if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
                return sb.ToString();
            }
        }

        private bool IsHoldMode
        {
            get { return string.Equals(Request.QueryString["mode"], "HOLD", StringComparison.OrdinalIgnoreCase); }
        }
        private string RefParam
        {
            get { return (Request.QueryString[IsHoldMode ? "hold" : "ref"] ?? "").Trim(); }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack) RenderPage();
        }

        private void RenderPage()
        {
            if (OmiseGateway.IsTestKey && !string.IsNullOrEmpty(OmiseGateway.PublicKey))
                litTestBand.Text = "<div class=\"test-band\">โหมดทดสอบ — ยังไม่มีการตัดเงินจริง</div>";

            if (string.IsNullOrEmpty(OmiseGateway.PublicKey))
            { Fail("ระบบยังไม่พร้อมรับบัตร (ยังไม่ได้ตั้ง Public Key) กรุณาติดต่อเจ้าหน้าที่"); return; }

            if (IsHoldMode)
            {
                var holds = new SecurityHoldService(_conn);
                if (!holds.IsAvailable) { Fail("ระบบวงเงินประกันยังไม่เปิดใช้งาน"); return; }

                var h = holds.GetByRef(RefParam);
                if (h == null) { Fail("ไม่พบรายการวงเงินประกันนี้"); return; }

                // มี charge ที่เกตเวย์แล้วแต่สถานะฝั่งเราไม่ใช่ "กันวงเงินอยู่" — ถามตัวจริงก่อน
                // (เคยมีบั๊กที่ Omise authorize สำเร็จแต่เราบันทึกเป็นไม่สำเร็จ ⇒ วงเงินลอยค้าง)
                if (h.Status != HoldStatus.Held && !string.IsNullOrEmpty(h.ProviderChargeId))
                {
                    string real = holds.SyncFromGateway(RefParam);
                    if (!string.IsNullOrEmpty(real) && real != h.Status)
                        h = holds.GetByRef(RefParam) ?? h;
                }

                if (h.Status == HoldStatus.Held)
                {
                    litTitle.Text = "วางวงเงินประกันความเสียหาย";
                    litAmount.Text = "฿" + h.Amount.ToString("N2");
                    Done("กันวงเงินไว้เรียบร้อยแล้ว — ไม่มีการตัดเงินใด ๆ จนกว่าจะเช็คเอาท์");
                    return;
                }
                if (h.Status != HoldStatus.PendingCard)
                {
                    // สถานะปิดจริง ๆ (ยกเลิก/หมดอายุ) — บอกเหตุผลล่าสุดถ้ามี จะได้ไม่ต้องเดา
                    string why = holds.LastFailReason(RefParam);
                    Fail("รายการนี้ปิดไปแล้ว (" + HoldStatus.Thai(h.Status) + ")"
                        + (string.IsNullOrEmpty(why) ? "" : " — ครั้งล่าสุด: " + why));
                    return;
                }

                litTitle.Text = "วางวงเงินประกันความเสียหาย";
                litDesc.Text = "การจอง #" + h.ReservationId
                    + " — ระบบจะ<b>กันวงเงิน</b>ไว้บนบัตรเท่านั้น <b>ยังไม่ตัดเงิน</b>";
                litAmount.Text = "฿" + h.Amount.ToString("N2");
                litButton.Text = "🛡 กันวงเงินประกัน";
                litHoldNote.Text = "<div class=\"hold-note\">"
                    + "• เงินยังอยู่ในบัตรของท่าน เพียงถูกกันวงเงินไว้ชั่วคราว<br/>"
                    + "• เช็คเอาท์แล้วไม่มีความเสียหาย → วงเงินคืนอัตโนมัติ ไม่มีการตัดเงิน<br/>"
                    + "• หากมีความเสียหาย ที่พักจะตัดเฉพาะค่าเสียหายจริง ส่วนที่เหลือคืนทันที<br/>"
                    + "• วงเงินที่กันไว้จะหมดอายุเองภายใน 7 วันหากไม่มีการดำเนินการ</div>";

                // เคยลองแล้วไม่ผ่าน — บอกไปตรง ๆ ว่าติดอะไร จะได้เลือกบัตรให้ถูกใบ
                string prevFail = holds.LastFailReason(RefParam);
                if (!string.IsNullOrEmpty(prevFail))
                    litMsg.Text = "<div class=\"alert err\">ครั้งก่อนไม่สำเร็จ: "
                        + Server.HtmlEncode(prevFail) + "<br/>ลองใหม่ด้วยบัตรใบอื่นได้เลย</div>";
                return;
            }

            var svc = new OnlinePaymentService(_conn);
            if (!svc.IsAvailable) { Fail("ระบบชำระเงินออนไลน์ยังไม่เปิดใช้งาน"); return; }

            var txn = svc.Store.GetByRef(RefParam);
            if (txn == null) { Fail("ไม่พบรายการชำระเงินนี้"); return; }
            if (txn.Status == PaymentStatus.Paid)
            {
                litTitle.Text = "ชำระเงินด้วยบัตร";
                litAmount.Text = "฿" + txn.TotalPayable.ToString("N2");
                Done("รายการนี้ชำระเงินเรียบร้อยแล้ว");
                return;
            }
            if (PaymentStatus.IsFinal(txn.Status) || txn.IsExpired)
            {
                // ทางตันเดิม: บอกว่าปิดแล้วจบเลย ลูกค้าต้องโทรขอลิงก์ใหม่
                // ⇒ ชี้กลับไปหน้าเลือกวิธีจ่ายของรายการต้นทาง ซึ่งเปิดใหม่ได้ตลอด
                string again = RetryUrl(txn);
                Fail("รายการนี้ปิดไปแล้ว (" + PaymentStatus.Thai(txn.Status) + ")"
                    + (string.IsNullOrEmpty(again)
                        ? " กรุณาเริ่มใหม่"
                        : "<br/><a href=\"" + Server.HtmlEncode(again)
                          + "\" style=\"display:inline-block;margin-top:10px;padding:11px 18px;border-radius:10px;"
                          + "background:#1b7a4b;color:#fff;text-decoration:none;font-weight:600;\">"
                          + "↻ เริ่มรายการชำระเงินใหม่</a>"), true);
                return;
            }

            litTitle.Text = "ชำระเงินด้วยบัตร";
            litDesc.Text = Server.HtmlEncode(txn.Description ?? "");
            litAmount.Text = "฿" + txn.TotalPayable.ToString("N2");
            litButton.Text = "💳 ชำระเงิน ฿" + txn.TotalPayable.ToString("N2");
        }

        protected void btnServer_Click(object sender, EventArgs e)
        {
            string token = (hfToken.Value ?? "").Trim();
            hfToken.Value = "";

            bool ok; string authorizeUri; string msg;

            if (IsHoldMode)
                msg = new SecurityHoldService(_conn).PlaceHold(RefParam, token, out ok, out authorizeUri);
            else
                msg = new OnlinePaymentService(_conn).ProcessCardToken(RefParam, token, out ok, out authorizeUri);

            if (!string.IsNullOrEmpty(authorizeUri))
            {
                // 3-D Secure — พาไปยืนยันกับธนาคาร แล้วธนาคารจะส่งกลับมาหน้า PayResult
                Response.Redirect(authorizeUri, false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (ok && !IsHoldMode)
            {
                // จ่ายสำเร็จ → หน้าผลลัพธ์กลาง (ถามสถานะจริงจากเกตเวย์เสมอ)
                Response.Redirect("~/Payment/PayResult?ref=" + Uri.EscapeDataString(RefParam), false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (ok)
            {
                litTitle.Text = "วางวงเงินประกันความเสียหาย";
                Done(msg);
            }
            else
            {
                // ⚠ RenderPage() เขียนทับ litMsg เสมอ — เดิมเรียกหลังตั้งข้อความ ทำให้เหตุผลจริง
                // จากเกตเวย์ ("บัตรถูกปฏิเสธ…") หายไป เหลือข้อความรวม ๆ ที่บอกอะไรไม่ได้
                // ⇒ วาดหน้าก่อน แล้วค่อยวางข้อความจริงทับ
                RenderPage();   // วาดฟอร์มใหม่ให้ลองอีกครั้งด้วยบัตรใบอื่น
                if (!pnlDone.Visible)   // เว้นกรณีระหว่างนั้นสถานะกลายเป็นสำเร็จไปแล้ว
                    litMsg.Text = "<div class=\"alert err\">" + Server.HtmlEncode(msg) + "</div>";
            }
        }

        private void Done(string message)
        {
            litMsg.Text = "<div class=\"alert ok\">" + Server.HtmlEncode(message) + "</div>";
            pnlForm.Visible = false;
            pnlDone.Visible = true;
        }

        /// <param name="rawHtml">true = ข้อความมี HTML ที่ผู้เรียกประกอบมาเองแล้ว</param>
        private void Fail(string message, bool rawHtml = false)
        {
            litTitle.Text = "ไม่สามารถดำเนินการได้";
            litMsg.Text = "<div class=\"alert err\">"
                + (rawHtml ? message : Server.HtmlEncode(message)) + "</div>";
            pnlForm.Visible = false;
        }

        /// <summary>ลิงก์กลับไปหน้าเลือกวิธีจ่ายของรายการต้นทาง — null ถ้าอ้างต้นทางไม่ได้</summary>
        private string RetryUrl(PaymentTransaction txn)
        {
            try
            {
                if (txn == null || string.IsNullOrEmpty(txn.SourceType)
                    || string.IsNullOrEmpty(txn.SourceId)) return null;
                if (txn.SourceType == PaymentSource.Pos || txn.SourceType == PaymentSource.Other) return null;

                return PaymentUrls.SiteBase()
                    + "/Payment/Pay?src=" + Uri.EscapeDataString(txn.SourceType)
                    + "&id=" + Uri.EscapeDataString(txn.SourceId)
                    + "&ph=" + Uri.EscapeDataString(txn.CustomerPhone ?? "");
            }
            catch { return null; }
        }
    }
}
