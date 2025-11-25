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
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TTBP"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private Code _code;
        private int _reservationId;
        private string _guestMobilePhone;
        private int _accommodationId;

        protected void Page_Load(object sender, EventArgs e)
        {
            _guestPortalService = new GuestPortalService(_connectionString);
            _code = new Code();

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
            _reservationId = Convert.ToInt32(session["Reservation_ID"]);
            _guestMobilePhone = session["Customer_MobilePhone"].ToString();
            _accommodationId = Convert.ToInt32(session["Accommodation_ID"]);

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
                DataTable dtOrders = _guestPortalService.GetRoomServiceOrders(_reservationId);

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
