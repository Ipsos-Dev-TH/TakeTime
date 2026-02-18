using System;
using System.Data;
using System.Web.UI;
using System.Web.Services;
using System.Collections.Generic;

namespace Take_Time_BangPhra.Admin.RoomService
{
    public partial class OrderManagement : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;

        protected void Page_Load(object sender, EventArgs e)
        {
            _code = new code();

            // Check admin login
            if (Session["username"] == null)
            {
                Response.Redirect("~/Admin/Login.aspx");
                return;
            }

            if (!IsPostBack)
            {
                LoadOrderList();
                UpdatePendingBadge();
            }
        }

        /// <summary>
        /// Load list of all orders
        /// </summary>
        private void LoadOrderList()
        {
            try
            {
                DataTable dtOrders = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        o.ID,
                        o.Order_Number,
                        o.Reservation_ID,
                        o.Order_Date,
                        o.Total_Amount,
                        o.Payment_Method,
                        o.Payment_Status,
                        o.Order_Status,
                        ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'ไม่ระบุห้อง') AS RoomName,
                        c.Name AS GuestName
                      FROM Guest_Room_Service_Orders o
                      INNER JOIN Reservation r ON o.Reservation_ID = r.ID
                      INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                      WHERE o.Order_Date >= DATEADD(DAY, -7, GETDATE())
                      ORDER BY
                        CASE o.Order_Status
                            WHEN 'PENDING' THEN 1
                            WHEN 'CONFIRMED' THEN 2
                            WHEN 'PREPARING' THEN 3
                            ELSE 4
                        END,
                        o.Order_Date DESC",
                    null);

                if (dtOrders.Rows.Count > 0)
                {
                    rptOrderList.DataSource = dtOrders;
                    rptOrderList.DataBind();
                    pnlNoOrders.Visible = false;
                }
                else
                {
                    pnlNoOrders.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadOrderList error: {ex.Message}");
                pnlNoOrders.Visible = true;
            }
        }

        /// <summary>
        /// Update pending order badge
        /// </summary>
        private void UpdatePendingBadge()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT COUNT(*) AS PendingCount FROM Guest_Room_Service_Orders WHERE Order_Status = 'PENDING'",
                    null);

                int pendingCount = dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["PendingCount"]) : 0;

                if (pendingCount > 0)
                {
                    pnlPendingBadge.Visible = true;
                    lblPendingCount.Text = pendingCount.ToString();
                }
                else
                {
                    pnlPendingBadge.Visible = false;
                }
            }
            catch { }
        }

        /// <summary>
        /// Get status display text
        /// </summary>
        protected string GetStatusText(string status)
        {
            switch (status)
            {
                case "PENDING": return "รอรับออเดอร์";
                case "CONFIRMED": return "กำลังจัดเตรียม";
                case "DELIVERED": return "จัดส่งแล้ว";
                case "CANCELLED": return "ยกเลิก";
                default: return status;
            }
        }

        /// <summary>
        /// Load selected order
        /// </summary>
        protected void btnLoadOrder_Click(object sender, EventArgs e)
        {
            string selectedOrderId = hfSelectedOrder.Value;

            if (string.IsNullOrEmpty(selectedOrderId))
            {
                return;
            }

            LoadOrderDetail(Convert.ToInt64(selectedOrderId));
        }

        /// <summary>
        /// Load order detail
        /// </summary>
        private void LoadOrderDetail(long orderId)
        {
            try
            {
                // Get order info
                DataTable dtOrder = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        o.*,
                        ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'ไม่ระบุห้อง') AS RoomName,
                        c.Name AS GuestName,
                        c.MobilePhone AS Phone
                      FROM Guest_Room_Service_Orders o
                      INNER JOIN Reservation r ON o.Reservation_ID = r.ID
                      INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                      WHERE o.ID = @OrderId",
                    new Dictionary<string, object> { { "@OrderId", orderId } });

                if (dtOrder.Rows.Count > 0)
                {
                    DataRow order = dtOrder.Rows[0];

                    lblOrderNumber.Text = order["Order_Number"].ToString();
                    lblRoomName.Text = order["RoomName"].ToString();
                    lblGuestName.Text = order["GuestName"].ToString();
                    lblPhone.Text = order["Phone"].ToString();
                    lblOrderTime.Text = Convert.ToDateTime(order["Order_Date"]).ToString("dd/MM/yyyy HH:mm");
                    lblStatus.Text = GetStatusText(order["Order_Status"].ToString());
                    lblPayment.Text = GetPaymentText(order["Payment_Method"].ToString(), order["Payment_Status"].ToString());
                    lblDeliveryNote.Text = string.IsNullOrEmpty(order["Delivery_Instructions"].ToString()) ? "-" : order["Delivery_Instructions"].ToString();
                    lblTotalAmount.Text = Convert.ToDecimal(order["Total_Amount"]).ToString("N0");

                    // Update button visibility based on status
                    string status = order["Order_Status"].ToString();
                    btnClaim.Visible = status == "PENDING";
                    btnDelivered.Visible = status == "CONFIRMED";
                    btnCancel.Visible = status != "DELIVERED" && status != "CANCELLED";
                }

                // Get order items
                DataTable dtItems = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM Guest_Room_Service_Items WHERE Order_ID = @OrderId",
                    new Dictionary<string, object> { { "@OrderId", orderId } });

                rptOrderItems.DataSource = dtItems;
                rptOrderItems.DataBind();

                pnlOrderDetail.Visible = true;
                pnlSelectOrder.Visible = false;

                // Reload list to update counts
                LoadOrderList();
                UpdatePendingBadge();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadOrderDetail error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get payment display text
        /// </summary>
        private string GetPaymentText(string method, string status)
        {
            string methodText = method switch
            {
                "CHARGE_TO_ROOM" => "ชาร์จเข้าห้อง",
                "TRANSFER" => "โอนเงิน",
                "CASH" => "เงินสด",
                _ => method
            };

            string statusText = status switch
            {
                "PENDING" => "รอชำระ",
                "PAID" => "ชำระแล้ว",
                "CHARGED" => "ชาร์จแล้ว",
                _ => status
            };

            return $"{methodText} ({statusText})";
        }

        /// <summary>
        /// Claim order (confirm)
        /// </summary>
        protected void btnClaim_Click(object sender, EventArgs e)
        {
            UpdateOrderStatus("CONFIRMED");
        }

        /// <summary>
        /// Mark as delivered
        /// </summary>
        protected void btnDelivered_Click(object sender, EventArgs e)
        {
            UpdateOrderStatus("DELIVERED");
        }

        /// <summary>
        /// Cancel order
        /// </summary>
        protected void btnCancel_Click(object sender, EventArgs e)
        {
            UpdateOrderStatus("CANCELLED");
        }

        /// <summary>
        /// Update order status
        /// </summary>
        private void UpdateOrderStatus(string newStatus)
        {
            string orderId = hfSelectedOrder.Value;
            if (string.IsNullOrEmpty(orderId)) return;

            try
            {
                string staffName = Session["username"]?.ToString() ?? "Unknown";
                short staffId = Session["admin_id"] != null ? Convert.ToInt16(Session["admin_id"]) : (short)0;

                string updateQuery = "UPDATE Guest_Room_Service_Orders SET Order_Status = @Status";

                var parameters = new Dictionary<string, object>
                {
                    { "@OrderId", Convert.ToInt64(orderId) },
                    { "@Status", newStatus }
                };

                if (newStatus == "CONFIRMED")
                {
                    updateQuery += ", Confirmed_By = @StaffId, Confirmed_Date = GETDATE()";
                    parameters.Add("@StaffId", staffId);
                }
                else if (newStatus == "DELIVERED")
                {
                    updateQuery += ", Delivered_By = @StaffId, Delivered_Date = GETDATE()";
                    parameters.Add("@StaffId", staffId);
                }

                updateQuery += " WHERE ID = @OrderId";

                _code.DatabaseInsertSafe(_connectionString, updateQuery, parameters);

                // Reload order detail
                LoadOrderDetail(Convert.ToInt64(orderId));

                // Show success message
                string statusText = GetStatusText(newStatus);
                ScriptManager.RegisterStartupScript(this, GetType(), "statusUpdate",
                    $"alert('อัพเดทสถานะเป็น \"{statusText}\" เรียบร้อย');", true);
            }
            catch (Exception ex)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "error",
                    $"alert('เกิดข้อผิดพลาด: {ex.Message}');", true);
            }
        }

        /// <summary>
        /// Web method to get pending orders for notification
        /// </summary>
        [WebMethod]
        public static object GetPendingOrders()
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                var code = new code();

                DataTable dtOrders = null;

                // Try with fn_GetReservationRoomNames first
                try
                {
                    dtOrders = code.DatabaseQuerySafe(connectionString,
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
                    // Fallback without fn_GetReservationRoomNames
                    dtOrders = code.DatabaseQuerySafe(connectionString,
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
                    string itemsPreview = row["ItemsPreview"]?.ToString() ?? "";
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

        /// <summary>
        /// Web method to claim an order
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object ClaimOrder(long orderId)
        {
            try
            {
                var context = System.Web.HttpContext.Current;
                short staffId = context?.Session["admin_id"] != null ? Convert.ToInt16(context.Session["admin_id"]) : (short)0;

                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                var code = new code();

                code.DatabaseInsertSafe(connectionString,
                    @"UPDATE Guest_Room_Service_Orders
                      SET Order_Status = 'CONFIRMED', Confirmed_By = @StaffId, Confirmed_Date = GETDATE()
                      WHERE ID = @OrderId AND Order_Status = 'PENDING'",
                    new Dictionary<string, object>
                    {
                        { "@OrderId", orderId },
                        { "@StaffId", staffId }
                    });

                return new { success = true };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }
}
