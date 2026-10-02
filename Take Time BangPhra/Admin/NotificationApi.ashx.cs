using System;
using System.Data;
using System.Web;
using System.Web.SessionState;

namespace Take_Time_BangPhra.Admin
{
    // IReadOnlySessionState (not IRequiresSessionState): this handler is polled from
    // Site.Master on EVERY page every 5s. A read-write session lock here serializes all
    // requests sharing the same SessionID, which is the root cause of pages "ค้าง" while
    // a slow request holds the exclusive lock. Read-only access still resets the session
    // sliding timeout (keepalive keeps working) but takes only a shared lock.
    public class NotificationApi : IHttpHandler, IReadOnlySessionState
    {
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json";
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");

            string action = context.Request.QueryString["action"];
            if (action == null) action = "";

            try
            {
                if (action == "chat")
                {
                    context.Response.Write(GetChatJson());
                }
                else if (action == "rs")
                {
                    context.Response.Write(GetRoomServiceJson());
                }
                else if (action == "keepalive")
                {
                    context.Response.Write("{\"success\":true}");
                }
                else
                {
                    context.Response.Write("{\"error\":\"invalid action\"}");
                }
            }
            catch (Exception ex)
            {
                context.Response.Write("{\"error\":\"" + EscapeJson(ex.Message) + "\"}");
            }
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
                        "SELECT TOP 1 ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'ห้องพัก') AS RoomName, c.Name AS GuestName, gc.Message AS LatestMessage FROM Guest_Chat gc INNER JOIN Reservation r ON gc.Reservation_ID = r.ID INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone WHERE gc.IsFromGuest = 1 AND (gc.Is_Read = 0 OR gc.Is_Read IS NULL) ORDER BY gc.Created_Date DESC",
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
                            latestRoom = "ห้องพัก";
                            latestGuest = dtFb.Rows[0]["GuestName"].ToString();
                            latestMessage = dtFb.Rows[0]["LatestMessage"].ToString();
                            if (latestMessage.Length > 100)
                                latestMessage = latestMessage.Substring(0, 100) + "...";
                        }
                    }
                    catch
                    {
                        latestMessage = "ข้อความใหม่";
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
                        "SELECT o.ID AS OrderId, o.Order_Number, o.Total_Amount, ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'ห้องพัก') AS RoomName, c.Name AS GuestName FROM Guest_Room_Service_Orders o INNER JOIN Reservation r ON o.Reservation_ID = r.ID INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone WHERE o.Order_Status = 'PENDING' ORDER BY o.Order_Date DESC",
                        null);
                }
                catch
                {
                    dtOrders = db.DatabaseQuerySafe(conn,
                        "SELECT o.ID AS OrderId, o.Order_Number, o.Total_Amount, N'ห้องพัก' AS RoomName, c.Name AS GuestName FROM Guest_Room_Service_Orders o INNER JOIN Reservation r ON o.Reservation_ID = r.ID INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone WHERE o.Order_Status = 'PENDING' ORDER BY o.Order_Date DESC",
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

        private string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        public bool IsReusable
        {
            get { return false; }
        }
    }
}
