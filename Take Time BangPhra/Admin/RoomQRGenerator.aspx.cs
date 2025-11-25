using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Services;
using System.IO;

namespace Take_Time_BangPhra.Admin
{
    public partial class RoomQRGenerator : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TTBP"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private Code _code;

        protected void Page_Load(object sender, EventArgs e)
        {
            // Permission check
            if (Session["permission"]?.ToString() != "True" ||
                (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
            {
                Response.Redirect("/Default");
                return;
            }

            _guestPortalService = new GuestPortalService(_connectionString);
            _code = new Code();

            if (!IsPostBack)
            {
                LoadQRCodes();
            }
        }

        /// <summary>
        /// Load all QR codes
        /// </summary>
        private void LoadQRCodes()
        {
            try
            {
                DataTable dtQRCodes = _guestPortalService.GetAllRoomQRCodes();

                if (dtQRCodes.Rows.Count > 0)
                {
                    rptQRCodes.DataSource = dtQRCodes;
                    rptQRCodes.DataBind();
                    lblNoData.Visible = false;
                }
                else
                {
                    lblNoData.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowAlert($"Error loading QR codes: {ex.Message}", "error");
            }
        }

        /// <summary>
        /// Generate QR codes for all accommodations
        /// </summary>
        protected void btnGenerateAll_Click(object sender, EventArgs e)
        {
            try
            {
                // Get all active accommodations
                DataTable dtAccommodations = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ID, Name FROM Accommodation WHERE Status = 1 ORDER BY OrderID, Name", null);

                if (dtAccommodations.Rows.Count == 0)
                {
                    ShowAlert("No active accommodations found", "error");
                    return;
                }

                short adminId = Session["UserID"] != null ? Convert.ToInt16(Session["UserID"]) : (short)1;
                int count = 0;

                foreach (DataRow row in dtAccommodations.Rows)
                {
                    int accomId = Convert.ToInt32(row["ID"]);

                    // Generate QR code for this accommodation
                    DataTable result = _guestPortalService.GenerateRoomQRCode(accomId, adminId);

                    if (result.Rows.Count > 0)
                    {
                        count++;
                    }
                }

                ShowAlert($"Successfully generated {count} QR codes!", "success");
                LoadQRCodes();
            }
            catch (Exception ex)
            {
                ShowAlert($"Error generating QR codes: {ex.Message}", "error");
            }
        }

        /// <summary>
        /// Refresh QR codes list
        /// </summary>
        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadQRCodes();
        }

        /// <summary>
        /// Handle repeater item commands (Regenerate)
        /// </summary>
        protected void rptQRCodes_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName == "Regenerate")
            {
                try
                {
                    int accommodationId = Convert.ToInt32(e.CommandArgument);
                    short adminId = Session["UserID"] != null ? Convert.ToInt16(Session["UserID"]) : (short)1;

                    DataTable result = _guestPortalService.GenerateRoomQRCode(accommodationId, adminId);

                    if (result.Rows.Count > 0)
                    {
                        ShowAlert("QR code regenerated successfully!", "success");
                        LoadQRCodes();
                    }
                    else
                    {
                        ShowAlert("Failed to regenerate QR code", "error");
                    }
                }
                catch (Exception ex)
                {
                    ShowAlert($"Error regenerating QR code: {ex.Message}", "error");
                }
            }
        }

        /// <summary>
        /// Generate QR code image URL using Google Charts API
        /// </summary>
        protected string GenerateQRCodeImage(string qrData)
        {
            if (string.IsNullOrEmpty(qrData))
                return "";

            // Using Google Charts QR Code API
            // Size: 200x200, Error correction: L
            string encodedData = System.Web.HttpUtility.UrlEncode(qrData);
            return $"https://chart.googleapis.com/chart?cht=qr&chs=200x200&chl={encodedData}&choe=UTF-8";
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
