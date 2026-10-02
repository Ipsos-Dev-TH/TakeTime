using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    /// <summary>
    /// เบิกของใช้ในห้อง (Guest Portal) — ผู้เข้าพักเลือกของแล้วกดส่งคำขอ
    /// พนักงานได้รับแจ้งเตือนทันทีเหมือนออเดอร์รูมเซอร์วิส
    ///
    /// ราคาคิดที่เซิร์ฟเวอร์เสมอ (AmenityService.CreateRequest) — หน้าเว็บแสดงยอดให้ดูเฉย ๆ
    /// ถ้าผู้ใช้แก้ค่าในเบราว์เซอร์ ยอดจริงยังคิดจากฐานข้อมูล
    /// </summary>
    public partial class Amenities : Page
    {
        private readonly string _conn =
            System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _portal;
        private AmenityService _svc;

        private long _reservationId;
        private string _mobilePhone;
        private short _accommodationId;
        private string _roomName = "";
        private string _guestName = "";

        protected DataTable DtItems;
        protected DataTable DtRequests;
        protected Dictionary<int, int> Used = new Dictionary<int, int>();
        protected bool ServiceReady;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Amenities", "~/Guest/Dashboard")) return;
            _portal = new GuestPortalService(_conn);
            _svc = new AmenityService(_conn);

            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
            }

            ServiceReady = _svc.IsReady;
            // โหลดทุกครั้ง ไม่เฉพาะ !IsPostBack — markup ของ pnlOrder วนอ่าน DtItems ตรง ๆ
            // ถ้า postback ใดไม่ผ่าน LoadAll() มาก่อน DtItems จะเป็น null แล้วหน้าพังตอน render
            LoadAll();
        }

        private void LoadAll()
        {
            DtItems = _svc.GetItems();
            Used = _svc.GetUsedQuantities(_reservationId);
            DtRequests = _svc.GetRequests(_reservationId);

            // ใช้ Panel คุมสถานะแทน <% if %> รอบ server control — ปลอดภัยกว่าใน WebForms
            bool hasItems = ServiceReady && DtItems != null && DtItems.Rows.Count > 0;
            pnlNotReady.Visible = !ServiceReady;
            pnlNoItems.Visible = ServiceReady && !hasItems;
            pnlOrder.Visible = hasItems;

            rptRequests.DataSource = DtRequests;
            rptRequests.DataBind();
            pnlNoRequests.Visible = DtRequests == null || DtRequests.Rows.Count == 0;
        }

        /// <summary>ส่งคำขอ — รับ "itemId:qty,itemId:qty" จากฟอร์ม แล้วให้ service คิดราคาเอง</summary>
        protected void btnSubmit_Click(object sender, EventArgs e)
        {
            try
            {
                var quantities = ParseCart(hfCart.Value);
                if (quantities.Count == 0)
                {
                    Alert("กรุณาเลือกของที่ต้องการเบิกก่อน");
                    LoadAll();
                    return;
                }

                var result = _svc.CreateRequest(_reservationId, _mobilePhone, _accommodationId,
                                                quantities, txtNote.Text.Trim());
                if (!result.Ok)
                {
                    Alert(result.Error ?? "ส่งคำขอไม่สำเร็จ");
                    LoadAll();
                    return;
                }

                // แจ้งพนักงาน — ล้มเหลวไม่ควรทำให้คำขอที่บันทึกแล้วดูเหมือนไม่สำเร็จ
                try { _svc.NotifyNewRequest(result, _roomName, _guestName); }
                catch { }

                hfCart.Value = "";
                txtNote.Text = "";
                LoadAll();

                string msg = result.TotalAmount > 0
                    ? "ส่งคำขอเรียบร้อย (เลขที่ " + result.RequestNumber + ")\\n"
                      + "ยอดรวม " + result.TotalAmount.ToString("N0") + " บาท จะถูกคิดรวมกับค่าห้อง"
                    : "ส่งคำขอเรียบร้อย (เลขที่ " + result.RequestNumber + ")\\nไม่มีค่าใช้จ่าย";
                Alert(msg + "\\nพนักงานได้รับแจ้งแล้ว");

                // มียอดต้องจ่าย + เปิดช่องทางไว้ → เสนอจ่ายทันทีแทนการรอเช็คเอาท์
                OfferOnlinePay(result);
            }
            catch (Exception ex)
            {
                Alert("เกิดข้อผิดพลาด: " + ex.Message);
            }
        }

        private static Dictionary<int, int> ParseCart(string raw)
        {
            var result = new Dictionary<int, int>();
            if (string.IsNullOrWhiteSpace(raw)) return result;
            foreach (string part in raw.Split(','))
            {
                string[] kv = part.Split(':');
                if (kv.Length != 2) continue;
                int id, qty;
                if (!int.TryParse(kv[0].Trim(), out id)) continue;
                if (!int.TryParse(kv[1].Trim(), out qty)) continue;
                if (qty <= 0) continue;
                result[id] = qty;
            }
            return result;
        }

        // ── helper สำหรับ markup ──────────────────────────────────────────────────

        protected int UsedQty(object itemId)
        {
            int id;
            if (itemId == null || !int.TryParse(itemId.ToString(), out id)) return 0;
            return Used.ContainsKey(id) ? Used[id] : 0;
        }

        /// <summary>ข้อความเงื่อนไขค่าใช้จ่ายของรายการนั้น (ฟรี / ฟรีอีก N / ราคา)</summary>
        protected string PriceLabel(DataRow r)
        {
            bool isFree = ToBool(r["Is_Free"]);
            decimal price = r["Price"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Price"]);
            int quota = r["Free_Quota_Per_Stay"] == DBNull.Value ? 0 : Convert.ToInt32(r["Free_Quota_Per_Stay"]);
            string unit = r["Unit"] == DBNull.Value ? "" : r["Unit"].ToString();
            return AmenityService.PriceLabel(isFree, price, quota, UsedQty(r["ID"]), unit);
        }

        /// <summary>ราคาต่อหน่วยที่ "จะโดนคิดจริง" สำหรับชิ้นถัดไป — ใช้คำนวณยอดโดยประมาณบนหน้าเว็บ</summary>
        protected decimal NextUnitPrice(DataRow r)
        {
            if (ToBool(r["Is_Free"])) return 0m;
            return r["Price"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Price"]);
        }

        protected int FreeLeft(DataRow r)
        {
            if (ToBool(r["Is_Free"])) return int.MaxValue;
            int quota = r["Free_Quota_Per_Stay"] == DBNull.Value ? 0 : Convert.ToInt32(r["Free_Quota_Per_Stay"]);
            return Math.Max(0, quota - UsedQty(r["ID"]));
        }

        protected string Esc(object v)
        {
            return v == null || v == DBNull.Value ? "" : Server.HtmlEncode(v.ToString());
        }

        protected string StatusText(object status) { return AmenityService.StatusText(Esc(status)); }

        protected string StatusClass(object status)
        {
            switch ((status == null ? "" : status.ToString()).ToUpperInvariant())
            {
                case "PENDING": return "st-pending";
                case "ACCEPTED": return "st-accepted";
                case "DELIVERED": return "st-delivered";
                case "CANCELLED": return "st-cancelled";
                default: return "";
            }
        }

        private static bool ToBool(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            if (v is bool b) return b;
            string s = v.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// เสนอให้จ่ายค่าของใช้ทันที (สแกน QR / บัตร) แทนการรอไปจ่ายรวมตอนเช็คเอาท์
        ///
        /// เดิมค่าของใช้ถูกคิดเข้าห้องอย่างเดียว — ช่องทาง AMENITY ประกาศไว้แต่ไม่เคยมีใครใช้
        /// เงียบสนิทถ้าปิดฟีเจอร์/ปิดช่องทาง ⇒ หน้าเดิมทำงานเหมือนเดิมทุกประการ
        /// </summary>
        private void OfferOnlinePay(Take_Time_BangPhra.Services.AmenityRequestResult result)
        {
            try
            {
                if (result == null || result.TotalAmount <= 0 || result.RequestId <= 0) return;

                var svc = new Take_Time_BangPhra.Payments.OnlinePaymentService();
                if (svc.AvailableMethods(result.TotalAmount,
                        Take_Time_BangPhra.Payments.PaymentSource.Amenity).Count == 0) return;

                string url = Take_Time_BangPhra.Payments.PaymentUrls.SiteBase()
                    + "/Payment/Pay?src=" + Take_Time_BangPhra.Payments.PaymentSource.Amenity
                    + "&id=" + result.RequestId
                    + "&ph=" + Uri.EscapeDataString(_mobilePhone ?? "");

                ScriptManager.RegisterStartupScript(this, GetType(), "amPayNow",
                    "if(confirm('ต้องการชำระเงิน " + result.TotalAmount.ToString("N0")
                    + " บาท ตอนนี้เลยไหม?\\n(กดยกเลิก = คิดรวมกับค่าห้องตอนเช็คเอาท์เหมือนเดิม)'))"
                    + "{window.location='" + url.Replace("'", "\\'") + "';}", true);
            }
            catch { /* ไม่พร้อม = ไม่เสนอ ปล่อยให้คิดเข้าห้องตามเดิม */ }
        }

        private void Alert(string message)
        {
            ScriptManager.RegisterStartupScript(this, GetType(), "amAlert",
                "alert('" + message.Replace("'", "\\'").Replace("\r", "").Replace("\n", "\\n") + "');", true);
        }

        private bool ValidateGuestSession()
        {
            string token = Request.Cookies["GuestSession"]?.Value ?? Session["GuestSessionToken"]?.ToString();
            if (string.IsNullOrEmpty(token)) return false;
            try
            {
                DataTable dt = _portal.ValidateGuestSession(token);
                if (dt == null || dt.Rows.Count == 0) return false;

                DataRow s = dt.Rows[0];
                _reservationId = Convert.ToInt64(s["Reservation_ID"]);
                _mobilePhone = s["Customer_MobilePhone"].ToString();
                _accommodationId = Convert.ToInt16(s["Accommodation_ID"]);
                if (s.Table.Columns.Contains("Accommodation_Name") && s["Accommodation_Name"] != DBNull.Value)
                    _roomName = s["Accommodation_Name"].ToString();
                if (s.Table.Columns.Contains("Customer_Name") && s["Customer_Name"] != DBNull.Value)
                    _guestName = s["Customer_Name"].ToString();
                return true;
            }
            catch { return false; }
        }
    }
}
