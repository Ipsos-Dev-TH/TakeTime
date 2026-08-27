using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    /// <summary>
    /// Guest Portal — จองกิจกรรมที่ต้องจองเวลา (เช่น โต๊ะปิงปอง):
    /// เลือกกิจกรรม → เลือกวัน/ช่วงเวลา (แสดงคิวที่เต็มแล้ว) → เลือกวิธีจ่าย
    /// (ชาร์จเข้าห้อง / โอนแนบสลิป / จ่ายที่เคาน์เตอร์) → ยืนยัน
    /// </summary>
    public partial class ActivityBooking : Page
    {
        private readonly string _conn =
            System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private ActivityService _svc;
        private GuestPortalService _portal;

        private int ReservationId => ViewState["ResId"] != null ? Convert.ToInt32(ViewState["ResId"]) : 0;
        private string GuestPhone => ViewState["Phone"]?.ToString();
        private string GuestName => ViewState["GuestName"]?.ToString();
        private byte AccomId => ViewState["AccomId"] != null ? Convert.ToByte(ViewState["AccomId"]) : (byte)0;
        private int SelectedActivityId
        {
            get => ViewState["ActId"] != null ? Convert.ToInt32(ViewState["ActId"]) : 0;
            set => ViewState["ActId"] = value;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Activities", "~/Guest/Dashboard")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            _svc = new ActivityService(_conn);
            _portal = new GuestPortalService(_conn);

            if (!IsPostBack)
            {
                if (!LoadGuestSession())
                {
                    Response.Redirect("~/Guest/Portal");
                    return;
                }
                BindActivityList();
                BindMyBookings();
            }
        }

        private bool LoadGuestSession()
        {
            try
            {
                string token = Request.Cookies["GuestSession"]?.Value ?? Session["GuestSessionToken"]?.ToString();
                if (string.IsNullOrEmpty(token)) return false;

                DataTable dt = _portal.ValidateGuestSession(token);
                if (dt == null || dt.Rows.Count == 0) return false;

                DataRow r = dt.Rows[0];
                ViewState["ResId"] = Convert.ToInt32(r["Reservation_ID"]);
                ViewState["Phone"] = r["Customer_MobilePhone"]?.ToString();
                ViewState["GuestName"] = r.Table.Columns.Contains("Customer_Name") ? r["Customer_Name"]?.ToString() : "";
                ViewState["AccomId"] = Convert.ToByte(r["Accommodation_ID"]);

                litRoom.Text = (r.Table.Columns.Contains("Accommodation_Name") ? r["Accommodation_Name"]?.ToString() : "")
                               + " · " + ViewState["GuestName"];
                return true;
            }
            catch { return false; }
        }

        // ── ขั้นที่ 1: รายการกิจกรรมที่จองได้ ──────────────────────────────────────
        private void BindActivityList()
        {
            DataTable dt = _svc.GetVisibleActivities("PORTAL");
            DataTable bookable = dt.Clone();
            foreach (DataRow r in dt.Rows)
                if (ToBool(r["IsBookable"])) bookable.ImportRow(r);

            rptActivities.DataSource = bookable;
            rptActivities.DataBind();
            pnlNoActivities.Visible = bookable.Rows.Count == 0;
        }

        private void BindMyBookings()
        {
            DataTable dt = _svc.GetBookingsForReservation(ReservationId);
            if (dt != null && dt.Rows.Count > 0)
            {
                rptMyBookings.DataSource = dt;
                rptMyBookings.DataBind();
                pnlMyBookings.Visible = true;
            }
            else pnlMyBookings.Visible = false;
        }

        protected void rptActivities_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName != "PickActivity") return;
            if (!int.TryParse(e.CommandArgument?.ToString(), out int actId)) return;

            SelectedActivityId = actId;
            var act = _svc.GetActivity(actId);
            if (act == null) { ShowMsg("ไม่พบกิจกรรมนี้", false); return; }

            litActivityName.Text = act["ActivityName"]?.ToString();

            string rules = act.Table.Columns.Contains("Rules") && act["Rules"] != DBNull.Value
                ? act["Rules"].ToString() : "";
            pnlRules.Visible = !string.IsNullOrWhiteSpace(rules);
            litRules.Text = Server.HtmlEncode(rules);

            int maxPart = act["MaxParticipants"] != DBNull.Value ? Convert.ToInt32(act["MaxParticipants"]) : 0;
            string mode = act["PricingMode"]?.ToString() ?? "FREE";
            pnlParticipants.Visible = maxPart > 0 || mode == "PER_PERSON";

            BuildDateOptions(act);
            RenderSlots();

            pnlPickActivity.Visible = false;
            pnlPickSlot.Visible = true;
            pnlPay.Visible = false;
        }

        private void BuildDateOptions(DataRow act)
        {
            int advance = act["AdvanceBookingDays"] != DBNull.Value ? Convert.ToInt32(act["AdvanceBookingDays"]) : 14;
            ddlDate.Items.Clear();
            for (int i = 0; i <= advance && i <= 30; i++)
            {
                var d = DateTime.Today.AddDays(i);
                string label = i == 0 ? "วันนี้" : i == 1 ? "พรุ่งนี้" : d.ToString("ddd d MMM", new System.Globalization.CultureInfo("th-TH"));
                ddlDate.Items.Add(new ListItem(label + $" ({d:dd/MM})", d.ToString("yyyy-MM-dd")));
            }
        }

        protected void ddlDate_Changed(object sender, EventArgs e)
        {
            hfSlots.Value = "";
            RenderSlots();
        }

        /// <summary>วาดตารางช่วงเวลา — ช่วงที่เต็ม/ผ่านไปแล้วกดไม่ได้</summary>
        private void RenderSlots()
        {
            if (SelectedActivityId <= 0) return;
            DateTime date = DateTime.TryParse(ddlDate.SelectedValue, out var d) ? d : DateTime.Today;

            var slots = _svc.GetDaySlots(SelectedActivityId, date);
            if (slots.Count == 0)
            {
                litSlots.Text = "<div class='empty'><i class='fas fa-clock'></i>ยังไม่ได้ตั้งเวลาให้บริการ</div>";
                return;
            }

            var sb = new StringBuilder("<div class='slot-grid'>");
            foreach (var s in slots)
            {
                string key = $"{s.Start:hh\\:mm}-{s.End:hh\\:mm}";
                string cls = "slot" + (s.Available ? "" : " disabled");
                string note = s.IsPast ? "ผ่านแล้ว"
                            : s.Booked >= s.Capacity ? "เต็ม"
                            : s.Capacity > 1 ? $"ว่าง {s.Remaining}" : "ว่าง";
                sb.Append($"<div class='{cls}' data-key='{key}' onclick=\"toggleSlot(this,'{key}')\">" +
                          $"{s.Label}<small>{note}</small></div>");
            }
            sb.Append("</div>");
            litSlots.Text = sb.ToString();
        }

        protected void btnBackToList_Click(object sender, EventArgs e)
        {
            pnlPickActivity.Visible = true;
            pnlPickSlot.Visible = false;
            pnlPay.Visible = false;
            BindActivityList();
        }

        // ── ขั้นที่ 2 → 3: สรุป + วิธีจ่าย ───────────────────────────────────────
        protected void btnNextToPay_Click(object sender, EventArgs e)
        {
            var (ok, start, end, msg) = ParseSelectedSlots();
            if (!ok) { ShowMsg(msg, false); return; }

            DateTime date = DateTime.TryParse(ddlDate.SelectedValue, out var d) ? d : DateTime.Today;
            var act = _svc.GetActivity(SelectedActivityId);
            if (act == null) { ShowMsg("ไม่พบกิจกรรมนี้", false); return; }

            var (avail, availMsg) = _svc.CheckAvailability(SelectedActivityId, date, start, end);
            if (!avail) { ShowMsg(availMsg, false); RenderSlots(); return; }

            int participants = 1;
            if (pnlParticipants.Visible) int.TryParse(txtParticipants.Text, out participants);
            if (participants < 1) participants = 1;

            decimal amount = _svc.CalculatePrice(act, start, end, participants);

            ViewState["Start"] = start.ToString();
            ViewState["End"] = end.ToString();
            ViewState["Date"] = date;
            ViewState["Amount"] = amount;
            ViewState["Participants"] = participants;

            litSumActivity.Text = Server.HtmlEncode(act["ActivityName"]?.ToString());
            litSumDate.Text = date.ToString("dd/MM/yyyy");
            litSumTime.Text = $"{start:hh\\:mm} - {end:hh\\:mm} น.";
            litSumAmount.Text = amount > 0 ? $"฿{amount:N2}" : "ไม่มีค่าใช้จ่าย";

            BuildPaymentOptions(amount);

            pnlPickSlot.Visible = false;
            pnlPay.Visible = true;
        }

        private void BuildPaymentOptions(decimal amount)
        {
            rblPayment.Items.Clear();
            pnlPayMethods.Visible = amount > 0;
            pnlSlip.Visible = false;
            if (amount <= 0) return;

            rblPayment.Items.Add(new ListItem("💳 ชาร์จเข้าห้องพัก — จ่ายรวมตอนเช็คเอาท์", "ROOM_CHARGE"));
            rblPayment.Items.Add(new ListItem("📤 โอนเงินแล้วแนบสลิป", "TRANSFER"));
            rblPayment.Items.Add(new ListItem("💵 จ่ายที่เคาน์เตอร์", "CASH"));

            // จ่ายออนไลน์ (บัตรเครดิต/QR ตัดยอดอัตโนมัติ) — โผล่เฉพาะเมื่อเปิดฟีเจอร์และเกตเวย์พร้อม
            // ปิดอยู่ = รายการนี้ไม่มี ตัวเลือกเดิมครบเหมือนเดิมทุกประการ
            if (OnlinePaymentOffered(amount))
                rblPayment.Items.Add(new ListItem("💳 จ่ายออนไลน์ด้วยบัตรเครดิต — ยืนยันทันที", "ONLINE"));

            rblPayment.SelectedIndex = 0;
        }

        /// <summary>เปิดให้จ่ายออนไลน์กับยอดนี้ไหม (เงียบสนิทถ้าฟีเจอร์ปิด)</summary>
        private bool OnlinePaymentOffered(decimal amount)
        {
            try
            {
                var svc = new Take_Time_BangPhra.Payments.OnlinePaymentService();
                foreach (string m in svc.AvailableMethods(amount))
                    if (m != Take_Time_BangPhra.Payments.PaymentGatewayConfig.MethodManualQr) return true;
                return false;
            }
            catch { return false; }
        }

        protected void rblPayment_Changed(object sender, EventArgs e)
        {
            pnlSlip.Visible = rblPayment.SelectedValue == "TRANSFER";
        }

        protected void btnBackToSlot_Click(object sender, EventArgs e)
        {
            pnlPay.Visible = false;
            pnlPickSlot.Visible = true;
            RenderSlots();
        }

        // ── ยืนยันการจอง ─────────────────────────────────────────────────────────
        protected void btnConfirm_Click(object sender, EventArgs e)
        {
            try
            {
                if (SelectedActivityId <= 0 || ViewState["Start"] == null) { ShowMsg("ข้อมูลการจองไม่ครบ", false); return; }

                var start = TimeSpan.Parse(ViewState["Start"].ToString());
                var end = TimeSpan.Parse(ViewState["End"].ToString());
                var date = Convert.ToDateTime(ViewState["Date"]);
                decimal amount = Convert.ToDecimal(ViewState["Amount"]);
                int participants = Convert.ToInt32(ViewState["Participants"]);

                string payMethod = amount > 0 ? (rblPayment.SelectedValue ?? "CASH") : "NONE";
                string slipUrl = null;
                if (payMethod == "TRANSFER" && fuSlip.HasFile)
                {
                    slipUrl = SaveSlip();
                    if (slipUrl == null) return;   // SaveSlip แสดง error เองแล้ว
                }

                var req = new ActivityService.BookingRequest
                {
                    ActivityId = SelectedActivityId,
                    ReservationId = ReservationId,
                    CustomerPhone = GuestPhone,
                    GuestName = GuestName,
                    AccommodationId = AccomId > 0 ? AccomId : (byte?)null,
                    Date = date,
                    Start = start,
                    End = end,
                    Participants = participants,
                    PaymentMethod = payMethod,
                    Notes = txtNotes.Text.Trim(),
                    SlipUrl = slipUrl,
                    BookedVia = "PORTAL"
                };

                var result = _svc.CreateBooking(req);
                ShowMsg(result.Message, result.Success);

                if (result.Success)
                {
                    // เลือกจ่ายออนไลน์ → พาไปหน้าชำระเงินทันที (การจองถูกบันทึกไว้แล้วเป็น "ยังไม่ชำระ")
                    if (payMethod == "ONLINE" && result.BookingId > 0)
                    {
                        Response.Redirect("~/Payment/Pay?src=ACTIVITY&id=" + result.BookingId, false);
                        Context.ApplicationInstance.CompleteRequest();
                        return;
                    }

                    // กลับไปหน้าแรกของ flow + refresh รายการจองของฉัน
                    pnlPay.Visible = false;
                    pnlPickSlot.Visible = false;
                    pnlPickActivity.Visible = true;
                    hfSlots.Value = "";
                    txtNotes.Text = "";
                    SelectedActivityId = 0;
                    BindActivityList();
                    BindMyBookings();
                }
            }
            catch (Exception ex)
            {
                ShowMsg("จองไม่สำเร็จ: " + ex.Message, false);
            }
        }

        private string SaveSlip()
        {
            try
            {
                string ext = Path.GetExtension(fuSlip.FileName).ToLowerInvariant();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
                if (Array.IndexOf(allowed, ext) < 0)
                { ShowMsg("รองรับเฉพาะรูปภาพหรือ PDF", false); return null; }
                if (fuSlip.PostedFile.ContentLength > 8 * 1024 * 1024)
                { ShowMsg("ไฟล์ใหญ่เกิน 8 MB", false); return null; }

                string folder = Server.MapPath("~/Images/ActivitySlips");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string fileName = $"slip_{ReservationId}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                fuSlip.SaveAs(Path.Combine(folder, fileName));
                return "/Images/ActivitySlips/" + fileName;
            }
            catch (Exception ex)
            {
                ShowMsg("อัปโหลดสลิปไม่สำเร็จ: " + ex.Message, false);
                return null;
            }
        }

        protected void rptMyBookings_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName != "CancelBooking") return;
            if (!long.TryParse(e.CommandArgument?.ToString(), out long id)) return;

            var (ok, msg) = _svc.CancelBooking(id, "ผู้เข้าพักยกเลิกเอง", null);
            ShowMsg(msg, ok);
            BindMyBookings();
            BindActivityList();
        }

        // ── ตัวช่วยแปลงช่วงเวลาที่เลือก ───────────────────────────────────────────
        /// <summary>รวมช่วงเวลาที่เลือกให้เป็นช่วงเดียว — ต้องติดกันเท่านั้น</summary>
        private (bool Ok, TimeSpan Start, TimeSpan End, string Msg) ParseSelectedSlots()
        {
            string raw = hfSlots.Value ?? "";
            var keys = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (keys.Length == 0) return (false, default, default, "กรุณาเลือกช่วงเวลาที่ต้องการ");

            var ranges = new List<(TimeSpan S, TimeSpan E)>();
            foreach (var k in keys)
            {
                var parts = k.Split('-');
                if (parts.Length != 2) continue;
                if (TimeSpan.TryParse(parts[0], out var s) && TimeSpan.TryParse(parts[1], out var en))
                    ranges.Add((s, en));
            }
            if (ranges.Count == 0) return (false, default, default, "ช่วงเวลาไม่ถูกต้อง");

            ranges = ranges.OrderBy(r => r.S).ToList();
            for (int i = 1; i < ranges.Count; i++)
                if (ranges[i].S != ranges[i - 1].E)
                    return (false, default, default, "กรุณาเลือกช่วงเวลาที่ติดกันเท่านั้น");

            return (true, ranges.First().S, ranges.Last().E, "");
        }

        private void ShowMsg(string msg, bool success)
        {
            pnlMsg.Visible = true;
            divMsg.Attributes["class"] = "msg " + (success ? "ok" : "err");
            string icon = success ? "fa-circle-check" : "fa-circle-exclamation";
            litMsg.Text = $"<i class='fas {icon}'></i> {Server.HtmlEncode(msg)}";
        }

        // ── formatters (ใช้จาก markup) ─────────────────────────────────────────────
        protected string ThumbStyle(object item)
        {
            var r = (DataRowView)item;
            string img = r["ImagePath"] != DBNull.Value ? r["ImagePath"].ToString() : "";
            return string.IsNullOrWhiteSpace(img) ? "" : $"background-image:url('{img}')";
        }

        protected string ThumbIcon(object item)
        {
            var r = (DataRowView)item;
            string img = r["ImagePath"] != DBNull.Value ? r["ImagePath"].ToString() : "";
            if (!string.IsNullOrWhiteSpace(img)) return "";
            string icon = r["IconClass"] != DBNull.Value && !string.IsNullOrWhiteSpace(r["IconClass"].ToString())
                ? r["IconClass"].ToString() : "fa-star";
            return $"<i class='fas {icon}'></i>";
        }

        protected string ActivityMeta(object item)
        {
            var r = (DataRowView)item;
            var bits = new List<string>();
            if (r["OpenTime"] != DBNull.Value && r["CloseTime"] != DBNull.Value)
                bits.Add($"{((TimeSpan)r["OpenTime"]):hh\\:mm} - {((TimeSpan)r["CloseTime"]):hh\\:mm} น.");
            if (r["Capacity"] != DBNull.Value && Convert.ToInt32(r["Capacity"]) > 1)
                bits.Add($"รองรับ {r["Capacity"]} คิว");
            if (r["Location"] != DBNull.Value && !string.IsNullOrWhiteSpace(r["Location"].ToString()))
                bits.Add(r["Location"].ToString());
            return Server.HtmlEncode(string.Join(" · ", bits));
        }

        protected string CostText(object item)
        {
            var r = (DataRowView)item;
            decimal price = r["Price"] != DBNull.Value ? Convert.ToDecimal(r["Price"]) : 0m;
            string mode = r["PricingMode"]?.ToString() ?? "FREE";
            if (price <= 0 || mode == "FREE") return "ฟรี";
            string suffix = mode == "PER_HOUR" ? "/ชม." : mode == "PER_PERSON" ? "/คน" : "";
            return $"฿{price:N0}<br/><small style='font-weight:400;'>{suffix}</small>";
        }

        protected string CostClass(object item)
        {
            var r = (DataRowView)item;
            decimal price = r["Price"] != DBNull.Value ? Convert.ToDecimal(r["Price"]) : 0m;
            string mode = r["PricingMode"]?.ToString() ?? "FREE";
            return (price <= 0 || mode == "FREE") ? "act-cost free" : "act-cost";
        }

        protected string SlotText(object item)
        {
            var r = (DataRowView)item;
            DateTime d = Convert.ToDateTime(r["BookingDate"]);
            var s = (TimeSpan)r["StartTime"];
            var en = (TimeSpan)r["EndTime"];
            return $"{d:dd/MM/yyyy} {s:hh\\:mm}-{en:hh\\:mm} น.";
        }

        protected string AmountText(object item)
        {
            var r = (DataRowView)item;
            decimal amt = Convert.ToDecimal(r["TotalAmount"]);
            return amt > 0 ? $"฿{amt:N2}" : "ไม่มีค่าใช้จ่าย";
        }

        protected string PaymentText(object item)
        {
            var r = (DataRowView)item;
            decimal amt = Convert.ToDecimal(r["TotalAmount"]);
            if (amt <= 0) return "";
            string m = r["PaymentMethod"]?.ToString();
            string s = r["PaymentStatus"]?.ToString();
            string method = m == "ROOM_CHARGE" ? "ชาร์จเข้าห้อง (จ่ายตอนเช็คเอาท์)"
                          : m == "TRANSFER" ? "โอนเงิน"
                          : m == "CASH" ? "จ่ายที่เคาน์เตอร์"
                          : m == "ONLINE" ? "จ่ายออนไลน์" : "";
            string status = s == "PAID" ? "ชำระแล้ว"
                          : s == "PENDING_VERIFY" ? "รอตรวจสอบสลิป"
                          : s == "UNPAID" ? "ยังไม่ชำระ" : "";
            string text = Server.HtmlEncode($"{method} · {status}");

            // เลือกจ่ายออนไลน์ไว้แต่ยังไม่จ่าย (ปิดหน้าไปกลางคัน) → ให้กดกลับไปจ่ายต่อได้
            if (m == "ONLINE" && s == "UNPAID")
                text += " · <a href=\"" + ResolveUrl("~/Payment/Pay?src=ACTIVITY&id=" + r["ID"])
                     + "\" style=\"color:#1b7a4b;font-weight:600\">จ่ายเลย →</a>";

            return text;
        }

        protected string StatusText(object item)
        {
            var r = (DataRowView)item;
            switch (r["Status"]?.ToString())
            {
                case "CONFIRMED": return "ยืนยันแล้ว";
                case "PENDING": return "รอยืนยัน";
                case "CANCELLED": return "ยกเลิก";
                case "COMPLETED": return "ใช้บริการแล้ว";
                default: return "";
            }
        }

        protected string StatusClass(object item)
        {
            var r = (DataRowView)item;
            switch (r["Status"]?.ToString())
            {
                case "CONFIRMED": return "st st-ok";
                case "PENDING": return "st st-wait";
                default: return "st st-no";
            }
        }

        protected bool CanCancel(object item)
        {
            var r = (DataRowView)item;
            if (r["Status"]?.ToString() == "CANCELLED") return false;
            if (r["PaymentStatus"]?.ToString() == "PAID") return false;
            // ยกเลิกได้ก่อนเวลาจองเท่านั้น
            DateTime d = Convert.ToDateTime(r["BookingDate"]);
            var s = (TimeSpan)r["StartTime"];
            return d.Date.Add(s) > DateTime.Now;
        }

        private static bool ToBool(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            if (v is bool b) return b;
            string s = v.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
