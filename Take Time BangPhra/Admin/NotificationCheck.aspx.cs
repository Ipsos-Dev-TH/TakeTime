using System;
using System.Collections.Generic;
using System.Data;
using System.Web;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin
{
    public partial class NotificationCheck : Page
    {
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            ViewStateUserKey = Session.SessionID;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            string action = Request.QueryString["action"];
            if (action == null) action = "";
            // action=dining (badge QR สั่งอาหาร) ใช้สิทธิ์ OPS_DINING — พนักงานร้านอาหารไม่จำเป็นต้องมีสิทธิ์ฐานข้อมูล; action อื่นคงกฎเดิม
            if (!Perm.Guard(this, action == "dining" ? Perm.OpsDining : Perm.SysDatabase)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            Response.Clear();
            Response.ContentType = "application/json";
            Response.Cache.SetCacheability(HttpCacheability.NoCache);

            // Admin-only: leaks guest names + chat content otherwise
            if (Session["permission"]?.ToString() != "True")
            {
                Response.StatusCode = 401;
                Response.Write("{\"error\":\"unauthorized\"}");
                FlushAndComplete();
                return;
            }

            try
            {
                if (action == "chat")
                {
                    Response.Write(GetChatJson());
                }
                else if (action == "rs")
                {
                    Response.Write(GetRoomServiceJson());
                }
                else if (action == "dining")
                {
                    Response.Write(GetDiningJson());
                }
                else if (action == "test")
                {
                    Response.Write("{\"test\":true,\"time\":\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"}");
                }
                else
                {
                    Response.Write("{\"error\":\"invalid action\"}");
                }
            }
            catch (Exception ex)
            {
                Response.Write("{\"error\":\"" + EscapeJson(ex.Message) + "\"}");
            }

            FlushAndComplete();
        }

        private void FlushAndComplete()
        {
            try
            {
                if (Response.IsClientConnected)
                {
                    Response.Flush();
                }
            }
            catch (HttpException)
            {
                // Client disconnected before flush completed (0x800704CD) - safe to ignore
            }
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }

        protected override void Render(HtmlTextWriter writer)
        {
            // Prevent any page HTML rendering - we already wrote JSON in Page_Load
        }

        private string GetChatJson()
        {
            try
            {
                string conn = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                Take_Time_BangPhra.code db = new Take_Time_BangPhra.code();

                DataTable dtCount = db.DatabaseQuerySafe(conn,
                    "SELECT COUNT(*) AS UnreadCount FROM Guest_Chat WHERE IsFromGuest = 1 AND (Is_Read = 0 OR Is_Read IS NULL)",
                    null);

                int unreadCount = 0;
                if (dtCount != null && dtCount.Rows.Count > 0)
                {
                    unreadCount = Convert.ToInt32(dtCount.Rows[0]["UnreadCount"]);
                }

                if (unreadCount == 0)
                {
                    return "{\"hasUnread\":false,\"count\":0,\"latestRoom\":\"\",\"latestGuest\":\"\",\"latestMessage\":\"\"}";
                }

                string latestRoom = "";
                string latestGuest = "";
                string latestMessage = "";

                try
                {
                    DataTable dtLatest = db.DatabaseQuerySafe(conn,
                        "SELECT TOP 1 ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'\u0E2B\u0E49\u0E2D\u0E07\u0E1E\u0E31\u0E01') AS RoomName, c.Name AS GuestName, gc.Message AS LatestMessage FROM Guest_Chat gc INNER JOIN Reservation r ON gc.Reservation_ID = r.ID INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone WHERE gc.IsFromGuest = 1 AND (gc.Is_Read = 0 OR gc.Is_Read IS NULL) ORDER BY gc.Created_Date DESC",
                        null);

                    if (dtLatest != null && dtLatest.Rows.Count > 0)
                    {
                        latestRoom = dtLatest.Rows[0]["RoomName"].ToString();
                        latestGuest = dtLatest.Rows[0]["GuestName"].ToString();
                        latestMessage = dtLatest.Rows[0]["LatestMessage"].ToString();
                        if (latestMessage.Length > 100)
                            latestMessage = latestMessage.Substring(0, 100) + "...";
                    }
                }
                catch
                {
                    try
                    {
                        DataTable dtFb = db.DatabaseQuerySafe(conn,
                            "SELECT TOP 1 c.Name AS GuestName, gc.Message AS LatestMessage FROM Guest_Chat gc INNER JOIN Reservation r ON gc.Reservation_ID = r.ID INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone WHERE gc.IsFromGuest = 1 AND (gc.Is_Read = 0 OR gc.Is_Read IS NULL) ORDER BY gc.Created_Date DESC",
                            null);

                        if (dtFb != null && dtFb.Rows.Count > 0)
                        {
                            latestRoom = "\u0E2B\u0E49\u0E2D\u0E07\u0E1E\u0E31\u0E01";
                            latestGuest = dtFb.Rows[0]["GuestName"].ToString();
                            latestMessage = dtFb.Rows[0]["LatestMessage"].ToString();
                            if (latestMessage.Length > 100)
                                latestMessage = latestMessage.Substring(0, 100) + "...";
                        }
                    }
                    catch
                    {
                        latestMessage = "\u0E02\u0E49\u0E2D\u0E04\u0E27\u0E32\u0E21\u0E43\u0E2B\u0E21\u0E48";
                    }
                }

                return "{\"hasUnread\":true,\"count\":" + unreadCount
                    + ",\"latestRoom\":\"" + EscapeJson(latestRoom)
                    + "\",\"latestGuest\":\"" + EscapeJson(latestGuest)
                    + "\",\"latestMessage\":\"" + EscapeJson(latestMessage) + "\"}";
            }
            catch (Exception ex)
            {
                return "{\"hasUnread\":false,\"count\":0,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
        }

        private string GetRoomServiceJson()
        {
            try
            {
                string conn = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                Take_Time_BangPhra.code db = new Take_Time_BangPhra.code();

                DataTable dtOrders = null;
                try
                {
                    dtOrders = db.DatabaseQuerySafe(conn,
                        "SELECT o.ID AS OrderId, o.Order_Number, o.Total_Amount, ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'\u0E2B\u0E49\u0E2D\u0E07\u0E1E\u0E31\u0E01') AS RoomName, c.Name AS GuestName FROM Guest_Room_Service_Orders o INNER JOIN Reservation r ON o.Reservation_ID = r.ID INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone WHERE o.Order_Status = 'PENDING' ORDER BY o.Order_Date DESC",
                        null);
                }
                catch
                {
                    dtOrders = db.DatabaseQuerySafe(conn,
                        "SELECT o.ID AS OrderId, o.Order_Number, o.Total_Amount, N'\u0E2B\u0E49\u0E2D\u0E07\u0E1E\u0E31\u0E01' AS RoomName, c.Name AS GuestName FROM Guest_Room_Service_Orders o INNER JOIN Reservation r ON o.Reservation_ID = r.ID INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone WHERE o.Order_Status = 'PENDING' ORDER BY o.Order_Date DESC",
                        null);
                }

                if (dtOrders == null || dtOrders.Rows.Count == 0)
                {
                    return "{\"success\":true,\"orders\":[]}";
                }

                string json = "{\"success\":true,\"orders\":[";
                for (int i = 0; i < dtOrders.Rows.Count; i++)
                {
                    DataRow row = dtOrders.Rows[i];
                    if (i > 0) json += ",";
                    json += "{\"orderId\":" + row["OrderId"].ToString()
                        + ",\"orderNumber\":\"" + EscapeJson(row["Order_Number"].ToString())
                        + "\",\"roomName\":\"" + EscapeJson(row["RoomName"].ToString())
                        + "\",\"guestName\":\"" + EscapeJson(row["GuestName"].ToString())
                        + "\",\"totalAmount\":\"" + Convert.ToDecimal(row["Total_Amount"]).ToString("N0") + "\"}";
                }
                json += "]}";
                return json;
            }
            catch
            {
                return "{\"success\":false,\"orders\":[]}";
            }
        }

        /// <summary>
        /// badge/modal QR สั่งอาหารทั่วระบบ (Site.Master checkDiningOrders): ตัวนับจาก DiningOrderService.GetNewOrdersSince(0)
        /// (ออเดอร์ NEW + สลิปรอตรวจ + โต๊ะเรียกพนักงาน + โต๊ะขอบิล) + ออเดอร์ใหม่ล่าสุด 1 รายการ + ชื่อโต๊ะที่เรียก/ขอบิล — ฟีเจอร์ปิด = total 0
        /// </summary>
        private string GetDiningJson()
        {
            try
            {
                var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
                var d = new Dictionary<string, object>();
                d["success"] = true;
                if (!Take_Time_BangPhra.Dining.DiningConfig.Enabled) { d["total"] = 0; return ser.Serialize(d); }

                var poll = new Take_Time_BangPhra.Dining.DiningOrderService().GetNewOrdersSince(0);
                d["newCount"] = poll.NewCount;
                d["pendingSlipCount"] = poll.PendingSlipCount;
                d["callStaffCount"] = poll.CallStaffCount;
                d["billRequestedCount"] = poll.BillRequestedCount;
                d["total"] = poll.NewCount + poll.PendingSlipCount + poll.CallStaffCount + poll.BillRequestedCount;
                if (poll.NewOrders != null && poll.NewOrders.Count > 0)
                {
                    var o = poll.NewOrders[0];   // ใหม่สุดก่อน
                    var l = new Dictionary<string, object>();
                    l["orderId"] = o.Id;
                    l["orderNo"] = o.OrderNo ?? "";
                    l["table"] = string.IsNullOrEmpty(o.TableName) ? (o.TableCode ?? "") : o.TableName;
                    l["zone"] = o.ZoneName ?? "";
                    l["itemsPreview"] = Take_Time_BangPhra.Dining.DiningStaffView.ItemsPreview(o, 3);
                    l["totalAmount"] = Take_Time_BangPhra.Dining.DiningStaffView.Money(o.TotalAmount);
                    l["round"] = o.RoundNo;
                    d["latest"] = l;
                }
                if (poll.CallStaffCount > 0 || poll.BillRequestedCount > 0)
                {
                    var call = new List<string>(); var bill = new List<string>();
                    foreach (var r in new Take_Time_BangPhra.Dining.DiningTableService().GetBoard(null))
                    {
                        if (r.Table == null) continue;
                        if (r.Table.Status == Take_Time_BangPhra.Dining.DiningStatus.Table.NeedsStaff && call.Count < 5) call.Add(r.Table.DisplayName);
                        if (r.BillRequested && bill.Count < 5) bill.Add(r.Table.DisplayName);
                    }
                    d["callTables"] = string.Join(", ", call.ToArray());
                    d["billTables"] = string.Join(", ", bill.ToArray());
                }
                return ser.Serialize(d);
            }
            catch (Exception ex)
            {
                return "{\"success\":false,\"total\":0,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
        }

        private string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}
