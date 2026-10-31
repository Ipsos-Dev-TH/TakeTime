using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using Take_Time_BangPhra.API;
using Take_Time_BangPhra.Dining;

namespace Take_Time_BangPhra.Order
{
    /// <summary>
    /// หน้าลูกค้า QR สั่งอาหาร — landing + เมนู + ตะกร้า + ยืนยันสั่ง ในหน้าเดียว (ไม่ล็อกอิน, ไม่มี ScriptManager; HTML + fetch ไป API/DiningOrder.ashx)
    /// เข้าทาง route Order/{token} (QR ที่โต๊ะ) หรือ Order/Default (ไม่มี token = ต่อ session จาก cookie dnsess — ปุ่ม "สั่งเพิ่ม")
    /// สถานะหน้า: ok (เมนู) · off (ฟีเจอร์ปิด/ยังไม่รัน migration) · closed (โซนปิด — แสดงเวลาทำการ) · error (QR ผิด/โต๊ะปิดใช้/ไม่มี session)
    /// ข้อความทุกตัวจาก service เป็นไทยล้วน → HtmlEncode ก่อนลง HTML เสมอ (H())
    /// </summary>
    public partial class OrderDefault : Page
    {
        protected string State = "error";
        protected string Heading = "";
        protected string Message = "";
        protected string SubMessage = "";
        protected string TableLabel = "";
        protected string HoursText = "";
        protected string BootJson = "{}";
        protected bool HasStatusLink;
        protected string StatusUrl = "";
        protected string RescanUrl = "";

        protected static string H(string s) { return HttpUtility.HtmlEncode(s ?? ""); }

        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            StatusUrl = ResolveUrl("~/Order/Status");
            try
            {
                if (!DiningConfig.Enabled)
                {
                    State = "off"; Heading = "ยังไม่เปิดให้บริการ"; Message = "ระบบสั่งอาหารผ่าน QR ยังไม่เปิดใช้งาน กรุณาสั่งกับพนักงาน";
                    return;
                }
                string token = RouteData.Values["token"] as string;
                if (string.IsNullOrEmpty(token)) token = Request.QueryString["t"];
                token = (token ?? "").Trim();
                string ip = DiningGuest.ClientIp(Request);
                var tables = new DiningTableService();

                DiningTableSession session = null;
                string sessionToken = null;
                bool resumed = false;
                string tableToken = null;

                if (token.Length > 0)
                {
                    var rt = tables.ResolveTable(token);
                    if (!rt.Ok)
                    {
                        if (rt.Table != null) TableLabel = "โต๊ะ " + rt.Table.DisplayName + (rt.Zone != null ? " · " + rt.Zone.NameTh : "");
                        switch (rt.ErrorCode)
                        {
                            case DiningError.ZoneClosed:
                                State = "closed"; Heading = "ปิดรับออเดอร์ชั่วคราว"; Message = rt.Message;
                                HoursText = Hours(rt.Zone);
                                LinkToOpenSession(tables);
                                return;
                            case DiningError.FeatureOff:
                            case DiningError.NotReady:
                                State = "off"; Heading = "ยังไม่เปิดให้บริการ"; Message = rt.Message; return;
                            default:
                                State = "error"; Heading = "QR ใช้ไม่ได้"; Message = rt.Message; SubMessage = "ลองสแกน QR ที่โต๊ะอีกครั้ง หรือเรียกพนักงาน"; return;
                        }
                    }
                    var open = tables.OpenOrResumeSession(rt.Table.Id, DiningTokens.DeviceHash(Request.UserAgent, ip), ip);
                    if (!open.Ok)
                    {
                        State = "error"; Heading = "เปิดโต๊ะไม่สำเร็จ"; Message = open.Message; SubMessage = "ลองใหม่อีกครั้ง หรือเรียกพนักงาน"; return;
                    }
                    session = open.Session; sessionToken = open.SessionToken; resumed = open.Resumed; tableToken = token;
                    DiningGuest.SetCookie(Response, sessionToken);
                    DiningGuest.RememberTableToken(Response, token);
                }
                else
                {
                    string ck = DiningGuest.CookieToken(Request);
                    session = ck != null ? tables.GetSession(ck) : null;
                    if (session == null)
                    {
                        DiningGuest.ClearCookie(Response);
                        string rem = DiningGuest.RememberedTableToken(Request);
                        if (rem != null)
                        {
                            // โต๊ะปิดบิลแล้ว แต่เครื่องนี้จำ QR ของโต๊ะได้ → เปิด session ใหม่ให้เลย (ไม่ต้องสแกนซ้ำ)
                            Response.Redirect(ResolveUrl("~/Order/Default.aspx?t=" + HttpUtility.UrlEncode(rem)), false);
                            Context.ApplicationInstance.CompleteRequest();
                            return;
                        }
                        State = "error"; Heading = "โต๊ะนี้ปิดบิลแล้ว"; Message = "สแกน QR ที่โต๊ะอีกครั้งเพื่อเริ่มสั่งใหม่"; return;
                    }
                    sessionToken = session.SessionToken; resumed = true;
                    string zoneMsg;
                    var zone = tables.GetZone(session.ZoneId);
                    if (!DiningTableService.IsZoneOpen(zone, out zoneMsg))
                    {
                        TableLabel = session.Label;
                        State = "closed"; Heading = "ปิดรับออเดอร์ชั่วคราว"; Message = zoneMsg; HoursText = Hours(zone);
                        HasStatusLink = true;
                        return;
                    }
                    tableToken = DiningGuest.RememberedTableToken(Request);
                }

                TableLabel = "โต๊ะ " + (string.IsNullOrEmpty(session.TableName) ? session.TableCode : session.TableName)
                    + (string.IsNullOrEmpty(session.ZoneName) ? "" : " · " + session.ZoneName);
                var menu = new DiningMenuService().GetMenu(session.ZoneCode);
                string nonce = DiningGuest.IssueNonce(Session, sessionToken);
                var boot = new Dictionary<string, object>
                {
                    { "nonce", nonce },
                    { "api", ResolveUrl("~/API/DiningOrder.ashx") },
                    { "statusUrl", StatusUrl },
                    { "menuUrl", ResolveUrl("~/Order/Default") },
                    { "session", DiningGuest.SessionJson(session, true, null) },
                    { "menu", DiningGuest.MenuJson(menu, session.ZoneCode) },
                    { "minOrder", DiningConfig.MinOrder },
                    { "resumed", resumed },
                    { "tableToken", tableToken },
                    { "maxLines", DiningConfig.MaxLines },
                    { "maxQty", DiningConfig.MaxQtyPerLine }
                };
                BootJson = DiningGuest.EmbedJson(boot);
                State = "ok";
            }
            catch (Exception ex)
            {
                DiningGuest.Log("Dining Order page", ex.GetType().Name + ": " + ex.Message);
                State = "error"; Heading = "ระบบขัดข้องชั่วคราว"; Message = "กรุณาลองใหม่อีกครั้ง หรือสั่งกับพนักงาน";
            }
        }

        /// <summary>โซนปิดแต่ยังมี session เปิดอยู่ในเครื่องนี้ (สั่งไปแล้ว) → ให้ไปหน้าสถานะ/เช็คบิลได้</summary>
        private void LinkToOpenSession(DiningTableService tables)
        {
            try
            {
                string ck = DiningGuest.CookieToken(Request);
                HasStatusLink = ck != null && tables.GetSession(ck) != null;
            }
            catch { HasStatusLink = false; }
        }

        private static string Hours(DiningZone z)
        {
            if (z == null) return "";
            if (z.ManualMode == DiningStatus.ZoneMode.Closed) return "";
            if (!z.OpenTime.HasValue || !z.CloseTime.HasValue) return "";
            return "เวลาทำการ " + Fmt(z.OpenTime.Value) + " – " + Fmt(z.CloseTime.Value) + " น.";
        }

        private static string Fmt(TimeSpan t) { return new DateTime(2000, 1, 1).Add(t).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
