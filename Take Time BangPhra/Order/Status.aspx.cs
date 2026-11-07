using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using Take_Time_BangPhra.API;
using Take_Time_BangPhra.Dining;

namespace Take_Time_BangPhra.Order
{
    /// <summary>
    /// หน้าสถานะ/เช็คบิลของโต๊ะ (ลูกค้า) — ทุกรอบที่สั่ง + timeline + ยอดค้าง + การชำระ; poll ทุก Dining_Guest_Poll_Sec วิ (API action=status แตะ TouchSession ให้)
    /// เข้าทาง Order/Status?s={sessionToken} (ลิงก์จาก QR/LINE) หรือ Order/Status (cookie dnsess)
    /// ปุ่ม: สั่งเพิ่ม · เรียกพนักงาน · เช็คบิล → เลือกวิธีจ่าย (เงินสดที่โต๊ะ / โอน+สลิป / ลงบิลห้องพัก)
    /// session ปิด/ไม่มี cookie → ลบ cookie + "โต๊ะนี้ปิดบิลแล้ว — สแกน QR ใหม่"
    /// </summary>
    public partial class OrderStatus : Page
    {
        protected string State = "closed";
        protected string Heading = "";
        protected string Message = "";
        protected string TableLabel = "";
        protected string BootJson = "{}";
        protected string MenuUrl = "";
        protected string AccountsHtml = "";
        protected string ContactHtml = "";

        protected static string H(string s) { return HttpUtility.HtmlEncode(s ?? ""); }

        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            MenuUrl = ResolveUrl("~/Order/Default");
            try
            {
                if (!DiningConfig.Enabled)
                {
                    State = "off"; Heading = "ยังไม่เปิดให้บริการ"; Message = "ระบบสั่งอาหารผ่าน QR ยังไม่เปิดใช้งาน กรุณาติดต่อพนักงาน";
                    return;
                }
                string q = (Request.QueryString["s"] ?? "").Trim();
                string token = DiningTokens.IsValidToken(q) ? q : DiningGuest.CookieToken(Request);
                if (token == null)
                {
                    DiningGuest.ClearCookie(Response);
                    State = "closed"; Heading = "โต๊ะนี้ปิดบิลแล้ว"; Message = "สแกน QR ที่โต๊ะใหม่เพื่อสั่งอีกครั้ง";
                    return;
                }
                var orders = new DiningOrderService();
                var sum = orders.GetSessionSummary(token);
                if (!sum.Ok)
                {
                    if (sum.ErrorCode == DiningError.SessionClosed || sum.ErrorCode == DiningError.BadToken)
                    {
                        DiningGuest.ClearCookie(Response);
                        State = "closed"; Heading = "โต๊ะนี้ปิดบิลแล้ว"; Message = "สแกน QR ที่โต๊ะใหม่เพื่อสั่งอีกครั้ง";
                        return;
                    }
                    State = "off"; Heading = "ยังไม่เปิดให้บริการ"; Message = sum.Message ?? "";
                    return;
                }
                if (DiningTokens.IsValidToken(q)) DiningGuest.SetCookie(Response, token);
                try { new DiningTableService().TouchSession(sum.Session.Id); } catch { }

                TableLabel = "โต๊ะ " + (string.IsNullOrEmpty(sum.Session.TableName) ? sum.Session.TableCode : sum.Session.TableName)
                    + (string.IsNullOrEmpty(sum.Session.ZoneName) ? "" : " · " + sum.Session.ZoneName);

                var cards = sum.Session.AllowTransfer ? DiningGuest.ReceiverCards() : new List<DiningGuest.ReceiverCard>();
                AccountsHtml = sum.Session.AllowTransfer ? DiningGuest.ReceiverCardsHtml(cards) : "";
                ContactHtml = DiningGuest.ContactHtml();
                var channels = new List<object>();
                foreach (var c in cards) channels.Add(new Dictionary<string, object> { { "id", c.PaidHowId }, { "name", c.Name } });

                string nonce = DiningGuest.IssueNonce(Session, token);
                var boot = new Dictionary<string, object>
                {
                    { "nonce", nonce },
                    { "api", ResolveUrl("~/API/DiningOrder.ashx") },
                    { "menuUrl", MenuUrl },
                    { "statusUrl", ResolveUrl("~/Order/Status") },
                    { "pollSec", DiningConfig.GuestPollSec },
                    { "summary", DiningGuest.SummaryJson(sum) },
                    { "channels", channels },
                    { "hasAccounts", cards.Count > 0 },
                    { "tableToken", DiningGuest.RememberedTableToken(Request) }
                };
                BootJson = DiningGuest.EmbedJson(boot);
                State = "ok";
            }
            catch (Exception ex)
            {
                DiningGuest.Log("Dining Status page", ex.GetType().Name + ": " + ex.Message);
                State = "off"; Heading = "ระบบขัดข้องชั่วคราว"; Message = "กรุณาลองใหม่อีกครั้ง หรือเรียกพนักงาน";
            }
        }
    }
}
