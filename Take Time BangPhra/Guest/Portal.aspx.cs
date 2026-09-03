using System;
using System.Data;
using System.Web;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class Portal : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private string _qrToken;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "GuestPortal", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            _guestPortalService = new GuestPortalService(_connectionString);

            if (!IsPostBack)
            {
                // Check if accessed via QR code
                _qrToken = Request.QueryString["qr"];

                if (!string.IsNullOrEmpty(_qrToken))
                {
                    // Store QR token in session for verification
                    Session["QR_Token"] = _qrToken;
                    pnlQRInfo.Visible = true;
                }
                else
                {
                    // Check if already has session
                    string sessionToken = Request.Cookies["GuestSession"]?.Value;
                    if (!string.IsNullOrEmpty(sessionToken))
                    {
                        // Validate existing session
                        DataTable dtSession = _guestPortalService.ValidateGuestSession(sessionToken);
                        if (dtSession.Rows.Count > 0)
                        {
                            // Valid session - redirect to dashboard
                            Response.Redirect("~/Guest/Dashboard");
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify guest access
        /// </summary>
        protected void btnVerify_Click(object sender, EventArgs e)
        {
            try
            {
                string mobilePhone = txtMobilePhone.Text.Trim();

                // Validate mobile phone
                if (string.IsNullOrEmpty(mobilePhone))
                {
                    ShowAlert("กรุณากรอกเบอร์มือถือ (Please enter your mobile phone number)", "error");
                    return;
                }

                // Clean phone number (remove dashes, spaces)
                mobilePhone = mobilePhone.Replace("-", "").Replace(" ", "");

                // Get QR token from session
                string qrToken = Session["QR_Token"]?.ToString();

                if (string.IsNullOrEmpty(qrToken))
                {
                    ShowAlert("Invalid QR code. Please scan the QR code in your room.", "error");
                    return;
                }

                // Verify guest access
                DataTable dtVerify = _guestPortalService.VerifyGuestAccess(qrToken, mobilePhone);

                if (dtVerify.Rows.Count == 0)
                {
                    ShowAlert("ไม่พบข้อมูลการจอง กรุณาตรวจสอบเบอร์มือถือ (No reservation found. Please check your mobile number)", "error");
                    return;
                }

                DataRow verifyResult = dtVerify.Rows[0];
                bool isValid = Convert.ToInt32(verifyResult["IsValid"]) == 1;

                if (!isValid)
                {
                    string errorMessage = verifyResult["ErrorMessage"]?.ToString() ?? "Access denied";
                    ShowAlert(errorMessage, "error");
                    return;
                }

                // Valid access - create session
                long reservationId = Convert.ToInt64(verifyResult["Reservation_ID"]);
                byte accommodationId = Convert.ToByte(verifyResult["Accommodation_ID"]);
                DateTime checkInDate = Convert.ToDateTime(verifyResult["CheckInDate"]);
                DateTime checkOutDate = Convert.ToDateTime(verifyResult["CheckOutDate"]);

                // Get client info
                string ipAddress = GetClientIP();
                string userAgent = Request.UserAgent ?? "";

                // Create guest portal session
                string sessionToken = _guestPortalService.CreateGuestSession(
                    reservationId, mobilePhone, accommodationId, qrToken,
                    checkInDate, checkOutDate, ipAddress, userAgent);

                if (!string.IsNullOrEmpty(sessionToken))
                {
                    // Store session in cookie (7 days)
                    HttpCookie sessionCookie = new HttpCookie("GuestSession", sessionToken)
                    {
                        Expires = DateTime.Now.AddDays(7),
                        HttpOnly = true,
                        Secure = Request.IsSecureConnection
                    };
                    Response.Cookies.Add(sessionCookie);

                    // Store session info in Session for immediate use
                    Session["GuestSessionToken"] = sessionToken;
                    Session["GuestReservationID"] = reservationId;
                    Session["GuestMobilePhone"] = mobilePhone;
                    Session["GuestAccommodationID"] = accommodationId;
                    Session["GuestName"] = verifyResult["Customer_Name"]?.ToString();
                    Session["GuestAccommodationName"] = verifyResult["Accommodation_Name"]?.ToString();
                    Session["GuestCheckOut"] = checkOutDate;

                    // Clear QR token from session
                    Session.Remove("QR_Token");

                    // Redirect to dashboard
                    Response.Redirect("~/Guest/Dashboard");
                }
                else
                {
                    ShowAlert("Failed to create session. Please try again.", "error");
                }
            }
            catch (Exception ex)
            {
                ShowAlert($"Error: {ex.Message}", "error");
            }
        }

        /// <summary>
        /// Get client IP address
        /// </summary>
        private string GetClientIP()
        {
            string ipAddress = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = Request.ServerVariables["REMOTE_ADDR"];
            }
            else
            {
                // In case of multiple IPs, take the first one
                string[] addresses = ipAddress.Split(',');
                if (addresses.Length > 0)
                {
                    ipAddress = addresses[0];
                }
            }

            return ipAddress ?? "Unknown";
        }

        /// <summary>
        /// Show alert message
        /// </summary>
        private void ShowAlert(string message, string type)
        {
            lblAlert.Text = message;
            pnlAlert.CssClass = type == "success" ? "alert alert-success" : "alert alert-error";
            pnlAlert.Visible = true;
        }
    }
}
