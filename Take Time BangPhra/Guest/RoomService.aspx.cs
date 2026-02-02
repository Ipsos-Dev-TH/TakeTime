using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class RoomService : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private code _code;
        private long _reservationId;
        private string _guestMobilePhone;
        private byte _accommodationId;

        protected void Page_Load(object sender, EventArgs e)
        {
            _guestPortalService = new GuestPortalService(_connectionString);
            _code = new code();

            // Check session
            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
            }

            if (!IsPostBack)
            {
                LoadProducts();
                LoadOrderHistory();
            }
        }

        /// <summary>
        /// Validate guest session
        /// </summary>
        private bool ValidateGuestSession()
        {
            string sessionToken = Request.Cookies["GuestSession"]?.Value ?? Session["GuestSessionToken"]?.ToString();

            if (string.IsNullOrEmpty(sessionToken))
            {
                return false;
            }

            DataTable dtSession = _guestPortalService.ValidateGuestSession(sessionToken);

            if (dtSession.Rows.Count == 0)
            {
                return false;
            }

            DataRow session = dtSession.Rows[0];
            _reservationId = Convert.ToInt64(session["Reservation_ID"]);
            _guestMobilePhone = session["Customer_MobilePhone"].ToString();
            _accommodationId = Convert.ToByte(session["Accommodation_ID"]);

            return true;
        }

        /// <summary>
        /// Load available products
        /// </summary>
        private void LoadProducts()
        {
            try
            {
                // Get products with available stock
                DataTable dtProducts = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID, Name, Price, Quantity
                      FROM Product
                      WHERE Quantity > 0
                        AND Status = 1
                      ORDER BY Name",
                    null);

                rptProducts.DataSource = dtProducts;
                rptProducts.DataBind();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading products: {ex.Message}");
            }
        }

        /// <summary>
        /// Load order history
        /// </summary>
        private void LoadOrderHistory()
        {
            try
            {
                DataTable dtOrders = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT * FROM Guest_Room_Service_Orders
                      WHERE Reservation_ID = @ReservationId
                      ORDER BY Order_Date DESC",
                    new Dictionary<string, object> { { "@ReservationId", _reservationId } });

                if (dtOrders.Rows.Count > 0)
                {
                    rptOrders.DataSource = dtOrders;
                    rptOrders.DataBind();
                    lblNoOrders.Visible = false;
                }
                else
                {
                    lblNoOrders.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading orders: {ex.Message}");
            }
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
        /// Get payment display text
        /// </summary>
        protected string GetPaymentText(string method)
        {
            switch (method)
            {
                case "CHARGE_TO_ROOM": return "ชาร์จเข้าห้อง";
                case "TRANSFER": return "โอนเงิน";
                case "CASH": return "เงินสด";
                default: return method;
            }
        }

        /// <summary>
        /// Get payment status text
        /// </summary>
        protected string GetPaymentStatusText(string status)
        {
            switch (status)
            {
                case "PENDING": return "รอชำระ";
                case "PAID": return "ชำระแล้ว";
                case "CHARGED": return "ชาร์จแล้ว";
                default: return status;
            }
        }

        /// <summary>
        /// Get timeline step class based on order status
        /// </summary>
        protected string GetTimelineClass(string orderStatus, string stepStatus)
        {
            if (orderStatus == "CANCELLED") return "cancelled";

            string[] statuses = { "PENDING", "CONFIRMED", "DELIVERED" };
            int orderIndex = Array.IndexOf(statuses, orderStatus);
            int stepIndex = Array.IndexOf(statuses, stepStatus);

            if (stepIndex < orderIndex) return "completed";
            if (stepIndex == orderIndex) return "active";
            return "";
        }

        /// <summary>
        /// Get order items HTML
        /// </summary>
        protected string GetOrderItemsHtml(object orderId)
        {
            try
            {
                DataTable dtItems = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM Guest_Room_Service_Items WHERE Order_ID = @OrderId",
                    new Dictionary<string, object> { { "@OrderId", Convert.ToInt64(orderId) } });

                if (dtItems.Rows.Count == 0) return "";

                string html = "";
                foreach (DataRow item in dtItems.Rows)
                {
                    html += $@"<div class='order-item-row'>
                        <span class='order-item-name'>{item["Product_Name"]}</span>
                        <span class='order-item-qty'>x{item["Quantity"]}</span>
                        <span class='order-item-subtotal'>฿{Convert.ToDecimal(item["Subtotal"]):N0}</span>
                    </div>";
                }
                return html;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Place room service order
        /// </summary>
        protected void btnPlaceOrder_Click(object sender, EventArgs e)
        {
            try
            {
                // Get cart items from hidden field
                string cartJson = hfCartItems.Value;

                if (string.IsNullOrEmpty(cartJson))
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                        "alert('Your cart is empty');", true);
                    return;
                }

                // Deserialize cart
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                List<Dictionary<string, object>> cartItems = serializer.Deserialize<List<Dictionary<string, object>>>(cartJson);

                if (cartItems == null || cartItems.Count == 0)
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                        "alert('Your cart is empty');", true);
                    return;
                }

                // Calculate total
                decimal totalAmount = 0;
                foreach (var item in cartItems)
                {
                    decimal price = Convert.ToDecimal(item["price"]);
                    int quantity = Convert.ToInt32(item["quantity"]);
                    totalAmount += price * quantity;
                }

                // Get payment method
                string paymentMethod = Request.Form["paymentMethod"] ?? "CHARGE_TO_ROOM";

                // Handle file upload if payment method is TRANSFER
                string paymentSlipPath = null;
                if (paymentMethod == "TRANSFER" && filePaymentSlip.HasFile)
                {
                    try
                    {
                        string fileName = $"RS_{DateTime.Now:yyyyMMddHHmmss}_{filePaymentSlip.FileName}";
                        string uploadFolder = Server.MapPath("~/Uploads/PaymentSlips/");

                        if (!Directory.Exists(uploadFolder))
                        {
                            Directory.CreateDirectory(uploadFolder);
                        }

                        string filePath = Path.Combine(uploadFolder, fileName);
                        filePaymentSlip.SaveAs(filePath);
                        paymentSlipPath = $"/Uploads/PaymentSlips/{fileName}";
                    }
                    catch (Exception ex)
                    {
                        ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                            $"alert('Error uploading payment slip: {ex.Message}');", true);
                        return;
                    }
                }

                // Create order
                long orderId = _guestPortalService.CreateRoomServiceOrder(
                    _reservationId,
                    _guestMobilePhone,
                    _accommodationId,
                    txtDeliveryInstructions.Text.Trim(),
                    totalAmount,
                    paymentMethod,
                    paymentSlipPath);

                if (orderId == 0)
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                        "alert('Failed to create order. Please try again.');", true);
                    return;
                }

                // Add items to order
                foreach (var item in cartItems)
                {
                    int productId = Convert.ToInt32(item["productId"]);
                    string productName = item["productName"].ToString();
                    int quantity = Convert.ToInt32(item["quantity"]);
                    decimal price = Convert.ToDecimal(item["price"]);

                    _guestPortalService.AddRoomServiceItem(orderId, productId, productName, quantity, price);
                }

                // If charge to room, mark as charged
                if (paymentMethod == "CHARGE_TO_ROOM")
                {
                    var parameters = new Dictionary<string, object>
                    {
                        { "@Order_ID", orderId }
                    };

                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Guest_Room_Service_Orders
                          SET Payment_Status = 'CHARGED'
                          WHERE ID = @Order_ID",
                        parameters);
                }

                // Clear cart and refresh
                ScriptManager.RegisterStartupScript(this, GetType(), "success",
                    @"alert('Order placed successfully!');
                      cart = [];
                      updateCartDisplay();
                      switchTab({currentTarget: document.querySelectorAll('.tab-btn')[1]}, 'history');",
                    true);

                txtDeliveryInstructions.Text = "";
                LoadOrderHistory();
            }
            catch (Exception ex)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                    $"alert('Error placing order: {ex.Message}');", true);
            }
        }
    }

    // Cart item class for deserialization
    public class CartItem
    {
        public int productId { get; set; }
        public string productName { get; set; }
        public decimal price { get; set; }
        public int quantity { get; set; }
    }
}
