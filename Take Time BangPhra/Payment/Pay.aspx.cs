using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Web;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Payments;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Payment
{
    /// <summary>
    /// หน้าชำระเงินของลูกค้า — จุดเดียวที่รวม "สแกน QR แล้วแนบสลิป (แบบเดิม)"
    /// กับ "จ่ายด้วยบัตรเครดิต/QR ตัดยอดอัตโนมัติ ผ่านเกตเวย์"
    ///
    /// เรียกใช้:  /Payment/Pay?src=RESERVATION&amp;id=123&amp;ph=0812345678
    ///           /Payment/Pay?src=ACTIVITY&amp;id=45
    ///
    /// ⚠ ถ้าฟีเจอร์ "รับชำระเงินออนไลน์" ปิดอยู่ หน้านี้จะไม่ทำงานเลย และไม่มีหน้าใด
    ///   ในระบบเดิมลิงก์มาที่นี่ ⇒ ระบบทำงานเหมือนเดิมทุกประการ
    /// </summary>
    public partial class Pay : System.Web.UI.Page
    {
        private readonly string _conn =
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        private OnlinePaymentService _svc;
        private readonly code _code = new code();

        // ── ข้อมูลรายการที่กำลังจ่าย (เก็บใน ViewState เพื่อไม่ต้องอ่าน DB ซ้ำ) ──
        private string SourceType
        {
            get { return (string)ViewState["src"]; }
            set { ViewState["src"] = value; }
        }
        private string SourceId
        {
            get { return (string)ViewState["sid"]; }
            set { ViewState["sid"] = value; }
        }
        private decimal Amount
        {
            get { return ViewState["amt"] == null ? 0m : (decimal)ViewState["amt"]; }
            set { ViewState["amt"] = value; }
        }
        private string ItemText
        {
            get { return (string)ViewState["item"]; }
            set { ViewState["item"] = value; }
        }
        private string CustomerName
        {
            get { return (string)ViewState["cname"]; }
            set { ViewState["cname"] = value; }
        }
        private string CustomerPhone
        {
            get { return (string)ViewState["cphone"]; }
            set { ViewState["cphone"] = value; }
        }
        private string CustomerEmail
        {
            get { return (string)ViewState["cmail"]; }
            set { ViewState["cmail"] = value; }
        }
        private string TxnRef
        {
            get { return (string)ViewState["txn"]; }
            set { ViewState["txn"] = value; }
        }

        // ── ช่องทางที่ลูกค้าเลือกไว้ตอนจอง (&ch=<รหัสแคตตาล็อก> เช่น PAYSO_AMEX) ──
        private string ChannelCode
        {
            get { return (string)ViewState["ch"]; }
            set { ViewState["ch"] = value; }
        }
        /// <summary>วิธีเกตเวย์ (CARD/QR/INSTALLMENT) ของช่องทางที่เลือกไว้ — ว่าง = ไม่มี/ใช้ไม่ได้</summary>
        private string ChannelMethod
        {
            get { return (string)ViewState["chm"]; }
            set { ViewState["chm"] = value; }
        }
        /// <summary>ยี่ห้อบัตรที่ช่องทางนั้นหมายถึง (เช่น AMEX) — ส่งเป็น CardBrandHint ให้ลงบันทึกถูกแหล่งเงิน</summary>
        private string ChannelBrand
        {
            get { return (string)ViewState["chb"]; }
            set { ViewState["chb"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            _svc = new OnlinePaymentService(_conn);

            if (!IsPostBack)
            {
                if (!_svc.IsAvailable)
                {
                    Fail("ขณะนี้ยังไม่เปิดให้ชำระเงินผ่านหน้านี้ กรุณาติดต่อเจ้าหน้าที่");
                    return;
                }

                if (!LoadSource()) return;

                ChannelCode = SafeChannelCode(Request.QueryString["ch"]);

                pnlMain.Visible = true;
                RenderSummary();
                BuildMethods(false);
            }
        }

        // ── โหลดข้อมูลรายการต้นทาง ────────────────────────────────────────────

        private bool LoadSource()
        {
            string src = (Request.QueryString["src"] ?? "").Trim().ToUpperInvariant();
            string id = (Request.QueryString["id"] ?? "").Trim();

            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(id))
            {
                Fail("ลิงก์ชำระเงินไม่สมบูรณ์ (ไม่ระบุรายการที่จะชำระ)");
                return false;
            }

            SourceType = src;
            SourceId = id;

            switch (src)
            {
                case PaymentSource.Reservation: return LoadReservation(id);
                case PaymentSource.Activity: return LoadActivity(id);
                case PaymentSource.RoomService: return LoadRoomServiceOrder(id);
                case PaymentSource.Amenity: return LoadAmenityRequest(id);
                default:
                    Fail("ยังไม่รองรับการชำระเงินของรายการชนิดนี้");
                    return false;
            }
        }

        private bool LoadReservation(string idText)
        {
            int rid;
            if (!int.TryParse(idText, out rid)) { Fail("รหัสการจองไม่ถูกต้อง"); return false; }

            var dt = _code.DatabaseQuerySafe(_conn, @"
                SELECT r.ID, r.Customer_MobilePhone, r.CheckinDate, r.CheckoutDate,
                       r.TotalPrice, r.Status, c.Name AS CustomerName, c.Email AS CustomerEmail
                  FROM Reservation r
                  LEFT JOIN Customer c ON c.MobilePhone = r.Customer_MobilePhone
                 WHERE r.ID = @id",
                new Dictionary<string, object> { { "@id", rid } });

            if (dt == null || dt.Rows.Count == 0) { Fail("ไม่พบการจองนี้"); return false; }
            DataRow row = dt.Rows[0];

            string phone = row["Customer_MobilePhone"] == DBNull.Value ? "" : row["Customer_MobilePhone"].ToString();

            // ตรวจตัวตนอย่างน้อยด้วยเบอร์โทร — พนักงานที่ล็อกอินอยู่ข้ามได้
            if (!IsStaff())
            {
                string given = (Request.QueryString["ph"] ?? "").Trim();
                if (string.IsNullOrEmpty(given) || !SamePhone(given, phone))
                {
                    Fail("ลิงก์ชำระเงินไม่ถูกต้อง หรือหมดอายุแล้ว กรุณาขอลิงก์ใหม่จากเจ้าหน้าที่");
                    return false;
                }
            }

            string status = row["Status"] == DBNull.Value ? "" : row["Status"].ToString().Trim();
            // ระบบเขียนสถานะยกเลิกเป็นภาษาไทย ("ยกเลิก", "ยกเลิกคืนเงิน", ...) และ "ลบจากการเลื่อนวันเข้าพัก"
            if (status == "Cancel" || status == "CANCELLED"
                || status.StartsWith("ยกเลิก", StringComparison.Ordinal)
                || status.StartsWith("ลบ", StringComparison.Ordinal))
            { Fail("การจองนี้ถูกยกเลิกแล้ว"); return false; }

            // ยอดค้างจากสูตรกลาง (ค่าห้อง + ค่าใช้จ่ายในห้อง เช่น ค่าบริการสัตว์เลี้ยง) — เดิมใช้ค่าห้องล้วน
            // ⇒ ลูกค้าเลือกจ่ายเต็มจำนวนตอนจอง (รวมค่าสัตว์เลี้ยง) แต่ถูกตัดยอดเหลือแค่ค่าห้อง
            decimal remaining;
            decimal pendingCharges = 0m;
            ReservationBalance bal = null;
            try { bal = ReservationBalance.Load(_conn, rid); } catch { bal = null; }
            if (bal != null)
            {
                remaining = bal.Due;
                pendingCharges = bal.PendingCharges;
            }
            else
            {
                try
                {
                    var da = new PaymentDataAccess(_conn);
                    remaining = da.GetRemainingBalance(rid);
                }
                catch
                {
                    decimal total = row["TotalPrice"] == DBNull.Value ? 0m : Convert.ToDecimal(row["TotalPrice"]);
                    remaining = total;
                }
            }

            // ยอดที่ระบุมาในลิงก์ต้องไม่เกินยอดค้างจริง (ป้องกันการแก้ตัวเลขใน URL)
            decimal asked = ParseAmount(Request.QueryString["amt"]);
            Amount = asked > 0 && asked < remaining ? asked : remaining;

            if (Amount <= 0) { Fail("การจองนี้ชำระเงินครบแล้ว"); return false; }

            ItemText = (pendingCharges > 0m ? "ค่าที่พักและค่าบริการ การจอง #" : "ค่าที่พัก การจอง #") + rid;
            CustomerName = row["CustomerName"] == DBNull.Value ? "" : row["CustomerName"].ToString();
            CustomerPhone = phone;
            CustomerEmail = row["CustomerEmail"] == DBNull.Value ? "" : row["CustomerEmail"].ToString();
            return true;
        }

        private bool LoadActivity(string idText)
        {
            long bid;
            if (!long.TryParse(idText, out bid)) { Fail("รหัสการจองกิจกรรมไม่ถูกต้อง"); return false; }

            var svc = new ActivityService(_conn);
            DataRow b = svc.GetBooking(bid);
            if (b == null) { Fail("ไม่พบการจองกิจกรรมนี้"); return false; }

            string payStatus = b["PaymentStatus"] == DBNull.Value ? "" : b["PaymentStatus"].ToString();
            if (payStatus == "PAID" || payStatus == "WAIVED")
            { Fail("การจองกิจกรรมนี้ชำระเงินเรียบร้อยแล้ว"); return false; }

            string status = b["Status"] == DBNull.Value ? "" : b["Status"].ToString();
            if (status == "CANCELLED") { Fail("การจองกิจกรรมนี้ถูกยกเลิกแล้ว"); return false; }

            Amount = b["TotalAmount"] == DBNull.Value ? 0m : Convert.ToDecimal(b["TotalAmount"]);
            if (Amount <= 0) { Fail("รายการนี้ไม่มีค่าใช้จ่าย"); return false; }

            ItemText = (b["ActivityName"] == DBNull.Value ? "กิจกรรม" : b["ActivityName"].ToString())
                     + " (จอง #" + bid + ")";
            CustomerName = b["GuestName"] == DBNull.Value ? "" : b["GuestName"].ToString();
            CustomerPhone = b["Customer_MobilePhone"] == DBNull.Value ? "" : b["Customer_MobilePhone"].ToString();
            return true;
        }

        private bool LoadRoomServiceOrder(string idText)
        {
            long oid;
            if (!long.TryParse(idText, out oid)) { Fail("รหัสออเดอร์ไม่ถูกต้อง"); return false; }

            var dt = _code.DatabaseQuerySafe(_conn, @"
                SELECT TOP 1 o.ID, o.Total_Amount, ISNULL(o.Payment_Status,'') AS PS,
                       ISNULL(o.Order_Status,'') AS OS,
                       ISNULL(o.Customer_MobilePhone,'') AS GuestPhone,
                       ISNULL(c.Name,'') AS GuestName
                  FROM Guest_Room_Service_Orders o
                  LEFT JOIN Customer c ON c.MobilePhone = o.Customer_MobilePhone
                 WHERE o.ID = @id",
                new Dictionary<string, object> { { "@id", oid } });

            if (dt == null || dt.Rows.Count == 0) { Fail("ไม่พบออเดอร์นี้"); return false; }
            DataRow r = dt.Rows[0];

            string ps = Convert.ToString(r["PS"]);
            if (ps == "PAID") { Fail("ออเดอร์นี้ชำระเงินแล้ว"); return false; }
            if (ps == "CHARGED") { Fail("ออเดอร์นี้ถูกคิดรวมกับค่าห้องแล้ว — จ่ายรวมตอนเช็คเอาท์"); return false; }
            if (Convert.ToString(r["OS"]) == "CANCELLED") { Fail("ออเดอร์นี้ถูกยกเลิกแล้ว"); return false; }

            Amount = Convert.ToDecimal(r["Total_Amount"]);
            if (Amount <= 0) { Fail("ออเดอร์นี้ไม่มียอดต้องชำระ"); return false; }

            ItemText = "รูมเซอร์วิส ออเดอร์ #" + oid;
            CustomerName = Convert.ToString(r["GuestName"]);
            CustomerPhone = Convert.ToString(r["GuestPhone"]);
            return true;
        }

        /// <summary>
        /// ใบเบิกของใช้ — เดิมคิดรวมกับค่าห้องอย่างเดียว (CHARGE_TO_ROOM)
        /// เปิดให้จ่ายแยกได้ด้วย สำหรับลูกค้าที่อยากเคลียร์เป็นรายการ ๆ ไป
        /// </summary>
        private bool LoadAmenityRequest(string idText)
        {
            long aid;
            if (!long.TryParse(idText, out aid)) { Fail("รหัสใบเบิกไม่ถูกต้อง"); return false; }

            var dt = _code.DatabaseQuerySafe(_conn, @"
                SELECT TOP 1 a.ID, a.Request_Number, a.Total_Amount,
                       ISNULL(a.Payment_Method,'') AS PM, ISNULL(a.[Status],'') AS ST,
                       ISNULL(a.Customer_MobilePhone,'') AS GuestPhone,
                       ISNULL(c.Name,'') AS GuestName
                  FROM Guest_Amenity_Request a
                  LEFT JOIN Customer c ON c.MobilePhone = a.Customer_MobilePhone
                 WHERE a.ID = @id",
                new Dictionary<string, object> { { "@id", aid } });

            if (dt == null || dt.Rows.Count == 0) { Fail("ไม่พบใบเบิกนี้"); return false; }
            DataRow r = dt.Rows[0];

            string pm = Convert.ToString(r["PM"]);
            if (pm == "PAID") { Fail("ใบเบิกนี้ชำระเงินแล้ว"); return false; }
            if (pm == "FREE") { Fail("ใบเบิกนี้อยู่ในสิทธิ์ฟรี ไม่มียอดต้องชำระ"); return false; }
            if (Convert.ToString(r["ST"]) == "CANCELLED") { Fail("ใบเบิกนี้ถูกยกเลิกแล้ว"); return false; }

            Amount = Convert.ToDecimal(r["Total_Amount"]);
            if (Amount <= 0) { Fail("ใบเบิกนี้ไม่มียอดต้องชำระ"); return false; }

            ItemText = "เบิกของใช้ " + Convert.ToString(r["Request_Number"]);
            CustomerName = Convert.ToString(r["GuestName"]);
            CustomerPhone = Convert.ToString(r["GuestPhone"]);
            return true;
        }

        // ── วาดหน้าจอ ─────────────────────────────────────────────────────────

        private void RenderSummary()
        {
            litItem.Text = Server.HtmlEncode(ItemText ?? "");
            if (!string.IsNullOrEmpty(CustomerName))
            {
                phCustomer.Visible = true;
                litCustomer.Text = Server.HtmlEncode(CustomerName);
            }
            litAmount.Text = "฿" + Amount.ToString("N2");
        }

        /// <param name="showAll">true = ลูกค้ากด "เลือกวิธีอื่น" — ไม่ล็อกตามช่องทางที่เลือกไว้ตอนจองแล้ว</param>
        private void BuildMethods(bool showAll)
        {
            rblMethod.Items.Clear();
            litChannelNote.Text = "";
            btnAllMethods.Visible = false;
            List<string> methods = _svc.AvailableMethods(Amount, SourceType);

            if (methods.Count == 0)
            {
                Fail("ขณะนี้ยังไม่มีวิธีชำระเงินที่ใช้ได้กับยอดนี้ กรุณาติดต่อเจ้าหน้าที่");
                return;
            }

            // ── ลูกค้าเลือกช่องทางเกตเวย์มาจากหน้าจองแล้ว → ล็อกวิธีตามช่องนั้น (ยังกด "เลือกวิธีอื่น" ได้) ──
            // ไม่สร้างรายการที่เกตเวย์ทันทีที่เปิดหน้า — ลูกค้าต้องกดดำเนินการต่อเอง
            string chNote = null;
            PaymentChannel ch = showAll ? null : RequestedChannel(methods, out chNote);
            if (ch != null)
            {
                string cm = ch.GatewayChannelCode.Trim().ToUpperInvariant();
                ChannelMethod = cm;
                ChannelBrand = PaymentChannelCatalog.BrandHint(ch);

                string name = string.IsNullOrWhiteSpace(ch.Name) ? PaymentGatewayConfig.MethodName(cm) : ch.Name.Trim();
                string chLabel = Server.HtmlEncode(name);
                decimal chSur = PaymentGatewayConfig.SurchargeFor(cm, Amount);
                if (chSur > 0) chLabel += " (+ค่าธรรมเนียม ฿" + chSur.ToString("N2") + ")";
                rblMethod.Items.Add(new ListItem(chLabel, cm));
                rblMethod.SelectedIndex = 0;

                var sb = new System.Text.StringBuilder();
                sb.Append("<div class=\"note\">ช่องทางที่ท่านเลือกไว้ตอนจอง: <b>").Append(Server.HtmlEncode(name)).Append("</b>");
                if (!string.IsNullOrWhiteSpace(ch.Conditions))
                    sb.Append("<br/>").Append(Server.HtmlEncode(ch.Conditions.Trim()).Replace("\n", "<br/>"));
                if (!string.IsNullOrWhiteSpace(ch.Instructions))
                    sb.Append("<br/>").Append(Server.HtmlEncode(ch.Instructions.Trim()).Replace("\n", "<br/>"));
                sb.Append("</div>");
                litChannelNote.Text = sb.ToString();
                btnAllMethods.Visible = methods.Count > 1;
                return;
            }

            ChannelMethod = null;
            ChannelBrand = null;
            if (chNote != null)
                litChannelNote.Text = "<div class=\"alert err\">" + Server.HtmlEncode(chNote) + "</div>";

            foreach (string m in methods)
            {
                string label = PaymentGatewayConfig.MethodName(m);
                decimal sur = PaymentGatewayConfig.SurchargeFor(m, Amount);
                if (sur > 0) label += " (+ค่าธรรมเนียม ฿" + sur.ToString("N2") + ")";
                rblMethod.Items.Add(new ListItem(label, m));
            }

            string def = PaymentGatewayConfig.DefaultMethod(Amount);
            ListItem sel = rblMethod.Items.FindByValue(def);
            if (sel != null) sel.Selected = true; else rblMethod.SelectedIndex = 0;

            // มีทางเดียวและเป็นวิธีเดิม (ไม่ยิงออกนอกระบบ) = ข้ามขั้นตอนเลือกไปเลย
            // ⚠ ไม่ข้ามให้กับวิธีที่ต้องยิงเกตเวย์ เพราะแค่เปิดหน้าจะกลายเป็นการสร้างรายการจริง
            // (มีข้อความแจ้งว่าช่องทางที่เลือกไว้ใช้ไม่ได้ → ไม่ข้าม ให้ลูกค้าเห็นข้อความก่อน)
            if (methods.Count == 1 && methods[0] == PaymentGatewayConfig.MethodManualQr && chNote == null)
                ProceedWith(methods[0]);
        }

        /// <summary>
        /// ช่องทางจากพารามิเตอร์ &amp;ch= ที่ยังใช้ได้จริงตอนนี้ — ต้องเป็นช่องเกตเวย์ที่เปิด/พร้อม (GatewayLive),
        /// วิธีของช่องนั้นอยู่ในวิธีที่ใช้ได้กับยอดนี้ และ (ลูกค้า) อยู่ในรายการที่ลูกค้าเห็นได้
        /// ใช้ไม่ได้ → null + ข้อความบอกลูกค้า แล้วล้างค่าทิ้ง (แสดงทุกวิธีตามปกติ)
        /// </summary>
        private PaymentChannel RequestedChannel(List<string> methods, out string unavailableNote)
        {
            unavailableNote = null;
            string code = ChannelCode;
            if (string.IsNullOrEmpty(code)) return null;

            PaymentChannel ch = null;
            try { ch = PaymentChannelCatalog.Get(code); } catch { ch = null; }

            bool ok = ch != null && ch.Active && ch.IsGateway && ch.GatewayLive
                      && !string.IsNullOrWhiteSpace(ch.GatewayChannelCode)
                      && methods.Contains(ch.GatewayChannelCode.Trim().ToUpperInvariant());
            if (ok && !IsStaff())
            {
                bool listed = false;
                foreach (PaymentChannel c in PaymentChannelCatalog.ForCustomer(SourceType))
                    if (string.Equals(c.Code, ch.Code, StringComparison.OrdinalIgnoreCase)) { listed = true; break; }
                ok = listed;
            }
            if (ok) return ch;

            unavailableNote = "ช่องทางที่เลือกไว้"
                + (ch != null && !string.IsNullOrWhiteSpace(ch.Name) ? " (" + ch.Name.Trim() + ")" : "")
                + " ใช้ไม่ได้ในขณะนี้ — กรุณาเลือกวิธีชำระเงินด้านล่าง";
            ChannelCode = null;
            return null;
        }

        /// <summary>รหัสช่องทางจาก URL — รับเฉพาะ A-Z 0-9 _ - ยาวไม่เกิน 50 (นอกนั้น = ไม่มี)</summary>
        private static string SafeChannelCode(string raw)
        {
            string s = (raw ?? "").Trim();
            if (s.Length == 0 || s.Length > 50) return null;
            foreach (char c in s)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-')) return null;
            return s.ToUpperInvariant();
        }

        // ── ปุ่ม ──────────────────────────────────────────────────────────────

        protected void btnContinue_Click(object sender, EventArgs e)
        {
            string m = rblMethod.SelectedValue;
            if (string.IsNullOrEmpty(m)) { ShowInfo(pnlMethods, "กรุณาเลือกวิธีชำระเงิน", true); return; }
            pnlMain.Visible = true;
            ProceedWith(m);
        }

        /// <summary>ลูกค้าไม่ใช้ช่องทางที่เลือกไว้ตอนจอง → แสดงทุกวิธีที่ใช้ได้</summary>
        protected void btnAllMethods_Click(object sender, EventArgs e)
        {
            pnlMain.Visible = true;
            pnlManual.Visible = false;
            pnlGateway.Visible = false;
            pnlMethods.Visible = true;
            ChannelCode = null;
            RenderSummary();
            BuildMethods(true);
        }

        protected void btnBack_Click(object sender, EventArgs e)
        {
            pnlMain.Visible = true;
            pnlManual.Visible = false;
            pnlGateway.Visible = false;
            pnlMethods.Visible = true;
            RenderSummary();
        }

        private void ProceedWith(string method)
        {
            pnlMethods.Visible = false;
            RenderSummary();

            decimal sur = PaymentGatewayConfig.SurchargeFor(method, Amount);
            if (sur > 0)
            {
                phSurcharge.Visible = true;
                litBase.Text = "฿" + Amount.ToString("N2");
                litSurcharge.Text = "฿" + sur.ToString("N2");
                litAmount.Text = "฿" + (Amount + sur).ToString("N2");
            }

            if (method == PaymentGatewayConfig.MethodManualQr) { ShowManual(); return; }
            StartGatewayPayment(method);
        }

        // ── วิธีเดิม: สแกน QR แล้วแนบสลิป ────────────────────────────────────

        private void ShowManual()
        {
            pnlManual.Visible = true;

            string img = PaymentGatewayConfig.Get("ManualQr_Image_Url", "");
            litManualQr.Text = string.IsNullOrWhiteSpace(img)
                ? "<div class=\"note\">กรุณาโอนเงินตามข้อมูลบัญชีด้านล่าง</div>"
                : "<img src=\"" + Server.HtmlEncode(ResolveIfLocal(img)) + "\" alt=\"QR ชำระเงิน\" />";

            string bank = PaymentGatewayConfig.Get("ManualQr_Bank_Info", "");
            litBank.Text = string.IsNullOrWhiteSpace(bank)
                ? ""
                : "<div class=\"bank\">" + Server.HtmlEncode(bank) + "</div>";

            litManualNote.Text = Server.HtmlEncode(
                PaymentGatewayConfig.Get("ManualQr_Note", "โอนแล้วกรุณาแนบสลิปเพื่อยืนยันการชำระเงิน"));
        }

        protected void btnManualConfirm_Click(object sender, EventArgs e)
        {
            pnlMain.Visible = true;
            RenderSummary();
            pnlMethods.Visible = false;
            pnlManual.Visible = true;

            bool requireSlip = PaymentGatewayConfig.GetBool("ManualQr_Require_Slip", true);
            // ลูกค้าชำระค่าจองเอง: สลิปคือหลักฐานเดียวที่เจ้าหน้าที่ใช้ตรวจก่อนลงรับเงิน → บังคับแนบเสมอ
            bool customerReservation = SourceType == PaymentSource.Reservation && !IsStaff();
            if (customerReservation) requireSlip = true;
            if (requireSlip && !fuSlip.HasFile)
            {
                ShowInfo(pnlManual, "กรุณาแนบสลิปการโอนเงินก่อนกดยืนยัน", true);
                return;
            }

            string slipUrl = null;
            if (fuSlip.HasFile)
            {
                string err;
                slipUrl = SaveSlip(out err);
                if (slipUrl == null) { ShowInfo(pnlManual, err, true); return; }
            }

            try
            {
                if (SourceType == PaymentSource.Activity)
                {
                    long bid = long.Parse(SourceId);
                    var svc = new ActivityService(_conn);
                    var r = svc.AttachSlip(bid, slipUrl ?? "");
                    if (!r.Ok) { ShowInfo(pnlManual, r.Message, true); return; }
                    Done("ได้รับสลิปแล้ว เจ้าหน้าที่จะตรวจสอบและยืนยันให้โดยเร็วที่สุด");
                    return;
                }

                if (SourceType == PaymentSource.Reservation)
                {
                    var ps = new PaymentService(_conn);

                    // ลูกค้าแนบสลิปเอง → "รอตรวจสลิป": เก็บสลิป + ยอดที่แจ้ง ยังไม่ลงรับเงิน/ใบเสร็จ/บัญชี และไม่ยืนยันการจอง
                    // (ใบจองคง "รอชำระเงิน" — ตัวกวาดยกเลิกข้ามใบที่มีสลิปรอตรวจ) เจ้าหน้าที่อนุมัติที่ Account/SlipVerification
                    // → PaymentService.ApproveReservationSlip ลงรับเงิน + ใบเสร็จ + บัญชี + PromoteIfPending
                    if (customerReservation)
                    {
                        PaymentResult sr = ps.SubmitReservationSlipForVerification(
                            int.Parse(SourceId), Amount, fuSlip.PostedFile, CustomerPhone,
                            "ชำระผ่านหน้าชำระเงินออนไลน์ (สแกน QR แนบสลิป)");
                        if (sr != null)
                        {
                            if (!sr.Success) { ShowInfo(pnlManual, sr.Message, true); return; }
                            Done("ได้รับสลิปแล้ว เจ้าหน้าที่จะตรวจสอบและยืนยันการชำระเงินให้โดยเร็วที่สุด");
                            return;
                        }
                        // sr == null: ยังไม่รัน migration PHASE19_25 (ไม่มี Payment_Slips.Claimed_Amount) → ลงรับเงินแบบเดิม
                        // แต่ "ไม่" เลื่อนสถานะใบจองจากสลิปที่ยังไม่มีใครตรวจ (ดูด้านล่าง)
                        LogPay("ยังไม่รัน PHASE19_25 — สลิปลูกค้าการจอง " + SourceId + " ลงรับเงินทันที (ไม่ยืนยันการจองจนกว่าเจ้าหน้าที่ตรวจ)");
                    }

                    // พนักงาน: ได้ Payment_History / ใบเสร็จ / ส่งบัญชี — ยอดกลาง (ค่าห้อง + ค่าใช้จ่ายในห้อง)
                    // ครบยอด = ปิดค่าใช้จ่ายในห้อง + มีบรรทัดในใบเสร็จ / ไม่ครบก่อนเช็คอิน = ใบรับมัดจำ
                    PaymentResult pr = ps.ProcessReservationPayment(
                        int.Parse(SourceId), Amount, "โอนเงิน",
                        fuSlip.HasFile ? fuSlip.PostedFile : null,
                        null, CustomerPhone,
                        "ชำระผ่านหน้าชำระเงินออนไลน์ (สแกน QR แนบสลิป)");

                    if (pr == null || !pr.Success)
                    {
                        ShowInfo(pnlManual, pr == null ? "บันทึกไม่สำเร็จ" : pr.Message, true);
                        return;
                    }
                    // ใบจองที่ลูกค้าจองแล้วรอชำระ (เลือกจ่ายออนไลน์แต่มาโอนแทน) → เลื่อนเป็น "มัดจำแล้ว"
                    // เฉพาะพนักงานคีย์เอง (ตรวจเงินแล้ว) — สลิปที่ลูกค้าแนบเองยังไม่ผ่านการตรวจ ห้ามยืนยันการจอง
                    if (!customerReservation)
                        BookingPayment.PromoteIfPending(_conn, int.Parse(SourceId));
                    Done("บันทึกการชำระเงินเรียบร้อยแล้ว"
                        + (string.IsNullOrEmpty(pr.ReceiptId) ? "" : " เลขที่ใบเสร็จ " + pr.ReceiptId));
                    return;
                }

                ShowInfo(pnlManual, "ยังไม่รองรับรายการชนิดนี้", true);
            }
            catch (Exception ex)
            {
                LogPay("บันทึกการโอน/แนบสลิปไม่สำเร็จ " + SourceType + " " + SourceId + ": " + ex.Message);
                ShowInfo(pnlManual, IsStaff()
                    ? "เกิดข้อผิดพลาด: " + ex.Message
                    : "บันทึกการชำระเงินไม่สำเร็จ กรุณาลองใหม่อีกครั้ง หรือติดต่อเจ้าหน้าที่", true);
            }
        }

        private string SaveSlip(out string error)
        {
            error = null;
            try
            {
                string ext = Path.GetExtension(fuSlip.FileName ?? "").ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".pdf")
                { error = "รองรับเฉพาะไฟล์ JPG, PNG หรือ PDF"; return null; }
                if (fuSlip.PostedFile.ContentLength > 8 * 1024 * 1024)
                { error = "ไฟล์ใหญ่เกิน 8 MB"; return null; }

                string folder = Server.MapPath("~/Images/PaymentSlips");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string fileName = "PAY_" + DateTime.Now.ToString("yyyyMMddHHmmss")
                                + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ext;
                fuSlip.SaveAs(Path.Combine(folder, fileName));
                return "/Images/PaymentSlips/" + fileName;
            }
            catch (Exception ex)
            {
                error = "อัปโหลดสลิปไม่สำเร็จ: " + ex.Message;
                return null;
            }
        }

        // ── ผ่านเกตเวย์ ──────────────────────────────────────────────────────

        private void StartGatewayPayment(string method)
        {
            var req = new PaymentChargeRequest
            {
                Method = method,
                SourceType = SourceType,
                SourceId = SourceId,
                Amount = Amount,
                Description = ItemText,
                CustomerName = CustomerName,
                CustomerPhone = CustomerPhone,
                CustomerEmail = CustomerEmail
            };

            // ช่องทางที่ลูกค้าเลือกไว้ตอนจอง (เช่น PaySo AMEX) → ส่งรหัส + ยี่ห้อบัตรไปด้วย
            // ⇒ ลงบันทึกเข้าแหล่งเงินของช่องนั้นได้แม้เกตเวย์ไม่แจ้งยี่ห้อบัตรกลับมา
            if (!string.IsNullOrEmpty(ChannelMethod)
                && string.Equals(method, ChannelMethod, StringComparison.OrdinalIgnoreCase))
            {
                req.ChannelCode = ChannelCode;
                req.CardBrandHint = ChannelBrand;
            }

            PaymentChargeResult r = _svc.Start(req);
            TxnRef = r.TxnRef;

            pnlGateway.Visible = true;
            litRef.Text = Server.HtmlEncode(r.TxnRef ?? "-");

            if (r.Status == PaymentStatus.Paid)
            {
                Done("ชำระเงินเรียบร้อยแล้ว ขอบคุณค่ะ");
                return;
            }

            if (!r.Success)
            {
                // ลูกค้าไม่ต้องเห็นเหตุผลทางเทคนิค (ค่าตั้งเกตเวย์/คำตอบดิบ) — เก็บลง log ให้เจ้าหน้าที่
                LogPay("เริ่มรายการไม่สำเร็จ " + SourceType + " " + SourceId + " วิธี " + method
                    + " ref " + (r.TxnRef ?? "-") + ": " + (r.Message ?? "-"));
                litGwInfo.Text = "<div class=\"alert err\">เริ่มรายการชำระเงินไม่สำเร็จ<br/>"
                    + Server.HtmlEncode(IsStaff()
                        ? (r.Message ?? "")
                        : "ระบบชำระเงินออนไลน์ขัดข้องชั่วคราว กรุณาลองใหม่อีกครั้ง หรือกด \"เลือกวิธีอื่น\"")
                    + "</div>";
                btnCheck.Visible = false;
                return;
            }

            litGwInfo.Text = "<div class=\"alert info\">กรุณาชำระเงินภายใน "
                + PaymentGatewayConfig.ExpiryMinutes + " นาที</div>";

            if (!string.IsNullOrEmpty(r.PaymentUrl))
            {
                phGwLink.Visible = true;
                lnkPay.NavigateUrl = r.PaymentUrl;
            }

            if (!string.IsNullOrEmpty(r.QrPayload))
            {
                phGwQr.Visible = true;
                litGwQr.Text = RenderQr(r.QrPayload);
            }
        }

        protected void btnCheck_Click(object sender, EventArgs e)
        {
            pnlMain.Visible = true;
            pnlMethods.Visible = false;
            RenderSummary();

            PaymentTransaction txn = _svc.Store.GetByRef(TxnRef);
            if (txn == null)
            {
                pnlGateway.Visible = true;
                litGwInfo.Text = "<div class=\"alert err\">ไม่พบรายการชำระเงินนี้</div>";
                return;
            }

            string note = _svc.RefreshStatus(txn);
            txn = _svc.Store.GetByRef(TxnRef);

            if (txn != null && txn.Status == PaymentStatus.Paid)
            {
                Done("ชำระเงินเรียบร้อยแล้ว ขอบคุณค่ะ");
                return;
            }

            pnlGateway.Visible = true;
            litRef.Text = Server.HtmlEncode(TxnRef ?? "-");
            litGwInfo.Text = "<div class=\"alert info\">สถานะล่าสุด: "
                + Server.HtmlEncode(note ?? "") + "</div>";

            if (txn != null && !string.IsNullOrEmpty(txn.PaymentUrl))
            {
                phGwLink.Visible = true;
                lnkPay.NavigateUrl = txn.PaymentUrl;
            }
        }

        /// <summary>
        /// แสดง QR ที่เกตเวย์ส่งมา — ถ้าเป็นรูป/ลิงก์รูปก็แสดงตรง ๆ
        /// ถ้าเป็นข้อความ EMVCo ให้เบราว์เซอร์วาดให้ และแสดงข้อความสำรองไว้เสมอ
        /// </summary>
        private string RenderQr(string payload)
        {
            string p = payload.Trim();
            if (p.StartsWith("data:image", StringComparison.OrdinalIgnoreCase)
                || p.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || p.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                bool looksImage = p.StartsWith("data:image", StringComparison.OrdinalIgnoreCase)
                    || p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    || p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                    || p.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
                if (looksImage)
                    return "<img src=\"" + Server.HtmlEncode(p) + "\" alt=\"QR ชำระเงิน\" />";
            }

            string enc = Server.HtmlEncode(p);
            return "<div id=\"qrTarget\"></div>"
                 + "<div class=\"payload\">" + enc + "</div>"
                 + "<script src=\"https://cdn.jsdelivr.net/npm/qrcodejs@1.0.0/qrcode.min.js\"></script>"
                 + "<script>try{new QRCode(document.getElementById('qrTarget'),"
                 + "{text:" + Newtonsoft.Json.JsonConvert.ToString(p) + ",width:240,height:240});}catch(e){}</script>";
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private void Done(string message)
        {
            pnlMain.Visible = true;
            pnlMethods.Visible = false;
            pnlManual.Visible = false;
            pnlGateway.Visible = false;
            pnlDone.Visible = true;
            litDone.Text = Server.HtmlEncode(message);
        }

        private void Fail(string message)
        {
            pnlMain.Visible = false;
            pnlError.Visible = true;
            litError.Text = Server.HtmlEncode(message);
        }

        private void ShowInfo(Panel target, string message, bool isError)
        {
            var lit = new Literal();
            lit.Text = "<div class=\"alert " + (isError ? "err" : "ok") + "\">"
                     + Server.HtmlEncode(message ?? "") + "</div>";
            target.Controls.AddAt(0, lit);
        }

        private void LogPay(string detail)
        {
            try { _code.Logs(_conn, "OnlinePayment", "Pay: " + detail, IsStaff() ? "Staff" : "Customer"); }
            catch { }
        }

        private bool IsStaff()
        {
            try { return Session["permission"] != null && Session["permission"].ToString() == "True"; }
            catch { return false; }
        }

        private static bool SamePhone(string a, string b)
        {
            return Digits(a) == Digits(b) && Digits(a).Length >= 9;
        }

        private static string Digits(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) if (char.IsDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        private static decimal ParseAmount(string s)
        {
            decimal v;
            return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0m;
        }

        private string ResolveIfLocal(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (url.StartsWith("~/")) return ResolveUrl(url);
            return url;
        }
    }
}
