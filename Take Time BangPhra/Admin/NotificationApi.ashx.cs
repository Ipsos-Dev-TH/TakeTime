using System;
using System.Collections.Generic;
using System.Data;
using System.Web;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Admin
{
    /// <summary>
    /// Notification API handler for chat and room service alerts.
    /// Uses .ashx to bypass FriendlyUrls redirect (which breaks .aspx WebMethod POST calls).
    /// </summary>
    public class NotificationApi : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json";
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);

            string action = context.Request.QueryString["action"] ?? "";
            var serializer = new JavaScriptSerializer();

            try
            {
                switch (action)
                {
                    case "chat":
                        context.Response.Write(serializer.Serialize(CheckUnreadMessages()));
                        break;
                    case "rs":
                        context.Response.Write(serializer.Serialize(GetPendingOrders()));
                        break;
                    case "keepalive":
                        context.Response.Write(serializer.Serialize(new { success = true }));
                        break;
                    default:
                        context.Response.Write(serializer.Serialize(new { error = "invalid action" }));
                        break;
                }
            }
            catch (Exception ex)
            {
                context.Response.Write(serializer.Serialize(new { error = ex.Message }));
            }
        }

        private object CheckUnreadMessages()
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                var db = new code();

                DataTable dtCount = db.DatabaseQuerySafe(connectionString,
                    @"SELECT COUNT(*) AS UnreadCount
                      FROM Guest_Chat
                      WHERE IsFromGuest = 1 AND (Is_Read = 0 OR Is_Read IS NULL)",
                    null);

                int unreadCount = dtCount.Rows.Count > 0 ? Convert.ToInt32(dtCount.Rows[0]["UnreadCount"]) : 0;

                string latestRoom = "";
                string latestGuest = "";
                string latestMessage = "";
                long latestReservationId = 0;

                if (unreadCount > 0)
                {
                    try
                    {
                        DataTable dtLatest = db.DatabaseQuerySafe(connectionString,
                            @"SELECT TOP 1
                                ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'ไม่ระบุห้อง') AS RoomName,
                                c.Name AS GuestName,
                                gc.Message AS LatestMessage,
                                gc.Reservation_ID AS ReservationId
                              FROM Guest_Chat gc
                              INNER JOIN Reservation r ON gc.Reservation_ID = r.ID
                              INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                              WHERE gc.IsFromGuest = 1 AND (gc.Is_Read = 0 OR gc.Is_Read IS NULL)
                              ORDER BY gc.Created_Date DESC",
                            null);

                        if (dtLatest.Rows.Count > 0)
                        {
                            latestRoom = dtLatest.Rows[0]["RoomName"].ToString();
                            latestGuest = dtLatest.Rows[0]["GuestName"].ToString();
                            latestMessage = dtLatest.Rows[0]["LatestMessage"].ToString();
                            latestReservationId = Convert.ToInt64(dtLatest.Rows[0]["ReservationId"]);
                            if (latestMessage.Length > 100)
                                latestMessage = latestMessage.Substring(0, 100) + "...";
                        }
                    }
                    catch
                    {
                        try
                        {
                            DataTable dtFallback = db.DatabaseQuerySafe(connectionString,
                                @"SELECT TOP 1
                                    c.Name AS GuestName,
                                    gc.Message AS LatestMessage,
                                    gc.Reservation_ID AS ReservationId
                                  FROM Guest_Chat gc
                                  INNER JOIN Reservation r ON gc.Reservation_ID = r.ID
                                  INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                                  WHERE gc.IsFromGuest = 1 AND (gc.Is_Read = 0 OR gc.Is_Read IS NULL)
                                  ORDER BY gc.Created_Date DESC",
                                null);

                            if (dtFallback.Rows.Count > 0)
                            {
                                latestRoom = "ห้องพัก";
                                latestGuest = dtFallback.Rows[0]["GuestName"].ToString();
                                latestMessage = dtFallback.Rows[0]["LatestMessage"].ToString();
                                latestReservationId = Convert.ToInt64(dtFallback.Rows[0]["ReservationId"]);
                                if (latestMessage.Length > 100)
                                    latestMessage = latestMessage.Substring(0, 100) + "...";
                            }
                        }
                        catch
                        {
                            latestMessage = "ข้อความใหม่";
                        }
                    }
                }

                return new
                {
                    hasUnread = unreadCount > 0,
                    count = unreadCount,
                    latestRoom = latestRoom,
                    latestGuest = latestGuest,
                    latestMessage = latestMessage,
                    latestReservationId = latestReservationId
                };
            }
            catch
            {
                return new { hasUnread = false, count = 0, latestRoom = "", latestGuest = "", latestMessage = "", latestReservationId = 0 };
            }
        }

        private object GetPendingOrders()
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                var db = new code();

                DataTable dtOrders = null;

                try
                {
                    dtOrders = db.DatabaseQuerySafe(connectionString,
                        @"SELECT
                            o.ID AS OrderId,
                            o.Order_Number,
                            o.Total_Amount,
                            ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'ไม่ระบุห้อง') AS RoomName,
                            c.Name AS GuestName,
                            (SELECT TOP 3 Product_Name + ' x' + CAST(Quantity AS NVARCHAR)
                             FROM Guest_Room_Service_Items i
                             WHERE i.Order_ID = o.ID
                             FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)') AS ItemsPreview
                          FROM Guest_Room_Service_Orders o
                          INNER JOIN Reservation r ON o.Reservation_ID = r.ID
                          INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                          WHERE o.Order_Status = 'PENDING'
                          ORDER BY o.Order_Date DESC",
                        null);
                }
                catch
                {
                    dtOrders = db.DatabaseQuerySafe(connectionString,
                        @"SELECT
                            o.ID AS OrderId,
                            o.Order_Number,
                            o.Total_Amount,
                            N'ห้องพัก' AS RoomName,
                            c.Name AS GuestName,
                            N'' AS ItemsPreview
                          FROM Guest_Room_Service_Orders o
                          INNER JOIN Reservation r ON o.Reservation_ID = r.ID
                          INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                          WHERE o.Order_Status = 'PENDING'
                          ORDER BY o.Order_Date DESC",
                        null);
                }

                var orders = new List<object>();
                foreach (DataRow row in dtOrders.Rows)
                {
                    string itemsPreview = row["ItemsPreview"] != DBNull.Value ? row["ItemsPreview"].ToString() : "";
                    if (itemsPreview.Length > 50) itemsPreview = itemsPreview.Substring(0, 50) + "...";

                    orders.Add(new
                    {
                        orderId = Convert.ToInt64(row["OrderId"]),
                        orderNumber = row["Order_Number"].ToString(),
                        roomName = row["RoomName"].ToString(),
                        guestName = row["GuestName"].ToString(),
                        itemsPreview = itemsPreview,
                        totalAmount = Convert.ToDecimal(row["Total_Amount"]).ToString("N0")
                    });
                }

                return new { success = true, orders = orders };
            }
            catch
            {
                return new { success = false, orders = new object[0] };
            }
        }

        public bool IsReusable
        {
            get { return false; }
        }
    }
}
