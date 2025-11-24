// ===========================================================================
// GuestProfile.aspx.cs
// Guest Profile 360° - Complete customer view with all information
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using System.Text;
using Take_Time_BangPhra.Class;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.CRM
{
    public partial class GuestProfile : System.Web.UI.Page
    {
        private readonly string _connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;
        private LoyaltyService _loyaltyService;

        protected void Page_Load(object sender, EventArgs e)
        {
            _code = new code();
            _loyaltyService = new LoyaltyService(_connectionString);

            // Check permissions
            try
            {
                if (Session["permission"]?.ToString() != "True" ||
                    (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
                {
                    Response.Redirect("/Default");
                    return;
                }
            }
            catch
            {
                Response.Redirect("/Default");
                return;
            }

            if (!IsPostBack)
            {
                // Check if phone number is passed via query string
                string phone = Request.QueryString["phone"];
                if (!string.IsNullOrEmpty(phone))
                {
                    txtSearchPhone.Text = phone;
                    LoadGuestProfile(phone);
                }
            }
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            string phone = txtSearchPhone.Text.Trim();
            string name = txtSearchName.Text.Trim();
            string email = txtSearchEmail.Text.Trim();

            if (string.IsNullOrEmpty(phone) && string.IsNullOrEmpty(name) && string.IsNullOrEmpty(email))
            {
                ShowAlert("Please enter at least one search criteria", "error");
                return;
            }

            // Try to find customer
            try
            {
                DataTable dtCustomer = FindCustomer(phone, name, email);

                if (dtCustomer.Rows.Count > 0)
                {
                    string customerPhone = dtCustomer.Rows[0]["MobilePhone"].ToString();
                    LoadGuestProfile(customerPhone);
                }
                else
                {
                    pnlProfile.Visible = false;
                    pnlNotFound.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error searching customer: " + ex.Message, "error");
            }
        }

        private DataTable FindCustomer(string phone, string name, string email)
        {
            var parameters = new Dictionary<string, object>();
            StringBuilder query = new StringBuilder("SELECT TOP 1 * FROM Customer WHERE 1=1");

            if (!string.IsNullOrEmpty(phone))
            {
                query.Append(" AND MobilePhone LIKE @Phone");
                parameters["@Phone"] = "%" + phone + "%";
            }

            if (!string.IsNullOrEmpty(name))
            {
                query.Append(" AND (Name LIKE @Name OR FullName LIKE @Name OR NickName LIKE @Name)");
                parameters["@Name"] = "%" + name + "%";
            }

            if (!string.IsNullOrEmpty(email))
            {
                query.Append(" AND Email LIKE @Email");
                parameters["@Email"] = "%" + email + "%";
            }

            return _code.DatabaseQuerySafe(_connectionString, query.ToString(), parameters);
        }

        private void LoadGuestProfile(string customerPhone)
        {
            try
            {
                // Load from Guest Profile 360 view
                var parameters = new Dictionary<string, object> { { "@Phone", customerPhone } };
                DataTable dtProfile = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM vw_Guest_Profile_360 WHERE Customer_MobilePhone = @Phone",
                    parameters);

                if (dtProfile.Rows.Count == 0)
                {
                    pnlProfile.Visible = false;
                    pnlNotFound.Visible = true;
                    return;
                }

                DataRow profile = dtProfile.Rows[0];

                // Show profile panel
                pnlProfile.Visible = true;
                pnlNotFound.Visible = false;

                // Customer Header
                string fullName = profile["Customer_FullName"]?.ToString() ?? profile["Customer_Name"]?.ToString() ?? "N/A";
                lblCustomerName.Text = fullName;
                lblInitials.Text = GetInitials(fullName);
                lblPhone.Text = profile["Customer_MobilePhone"]?.ToString() ?? "";
                lblEmail.Text = profile["Customer_Email"]?.ToString() ?? "Not provided";

                // Member Since
                if (profile["FirstReservationDate"] != DBNull.Value)
                {
                    lblMemberSince.Text = Convert.ToDateTime(profile["FirstReservationDate"]).ToString("MMM yyyy");
                }
                else
                {
                    lblMemberSince.Text = "New";
                }

                // Loyalty Badge
                string tierName = profile["Loyalty_TierName"]?.ToString() ?? "Member";
                lblTierName.Text = tierName;
                if (tierName == "VIP" || tierName == "สมาชิก VIP")
                {
                    pnlVipBadge.Visible = true;
                }

                // Statistics
                lblPoints.Text = profile["Loyalty_PointsBalance"]?.ToString() ?? "0";
                lblTotalBookings.Text = profile["TotalBookings"]?.ToString() ?? "0";

                decimal lifetimeValue = profile["TotalRevenue"] != DBNull.Value
                    ? Convert.ToDecimal(profile["TotalRevenue"])
                    : 0;
                lblLifetimeValue.Text = lifetimeValue.ToString("N0");

                decimal avgRating = profile["AverageRating"] != DBNull.Value
                    ? Convert.ToDecimal(profile["AverageRating"])
                    : 0;
                lblAvgRating.Text = avgRating.ToString("N1");

                // Basic Information
                lblFullName.Text = fullName;
                lblNickname.Text = profile["Customer_NickName"]?.ToString() ?? "N/A";
                lblIDNumber.Text = profile["Customer_IDNumber"]?.ToString() ?? "Not provided";
                lblAddress.Text = profile["Customer_Address"]?.ToString() ?? "Not provided";
                lblSegment.Text = profile["SegmentName"]?.ToString() ?? "Not segmented";

                // Loyalty Status
                lblCurrentTier.Text = tierName;
                lblCurrentPoints.Text = profile["Loyalty_PointsBalance"]?.ToString() ?? "0";

                // Calculate next tier
                int currentPoints = profile["Loyalty_PointsBalance"] != DBNull.Value
                    ? Convert.ToInt32(profile["Loyalty_PointsBalance"])
                    : 0;
                var nextTierInfo = GetNextTierInfo(currentPoints);
                lblNextTier.Text = nextTierInfo.Item1;
                lblPointsToNext.Text = nextTierInfo.Item2.ToString("N0");

                lblMultiplier.Text = profile["Loyalty_PointsMultiplier"]?.ToString() ?? "1.0";
                lblDiscountPercent.Text = profile["Loyalty_DiscountPercent"]?.ToString() ?? "0";

                // Load related data
                LoadBookingHistory(customerPhone);
                LoadReviews(customerPhone);
                LoadPreferences(customerPhone);
                LoadSpecialDates(customerPhone);
                LoadNotes(customerPhone);
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading profile: " + ex.Message, "error");
            }
        }

        private void LoadBookingHistory(string customerPhone)
        {
            try
            {
                var parameters = new Dictionary<string, object> { { "@Phone", customerPhone } };
                DataTable dtBookings = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 10
                        R.ID AS ReservationID,
                        R.CheckinDate,
                        R.CheckoutDate,
                        R.StayDays,
                        R.TotalPrice AS TotalAmount,
                        R.Status,
                        STUFF((
                            SELECT ', ' + A.Name
                            FROM Reservation_Accommodation RA
                            INNER JOIN Accommodation A ON A.ID = RA.Accommodation_ID
                            WHERE RA.Reservation_ID = R.ID
                            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS AccommodationNames
                      FROM Reservation R
                      WHERE R.Customer_MobilePhone = @Phone
                      ORDER BY R.CheckinDate DESC",
                    parameters);

                if (dtBookings.Rows.Count > 0)
                {
                    rptBookings.DataSource = dtBookings;
                    rptBookings.DataBind();
                    lblNoBookings.Visible = false;
                }
                else
                {
                    lblNoBookings.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading bookings: " + ex.Message, "error");
            }
        }

        private void LoadReviews(string customerPhone)
        {
            try
            {
                var parameters = new Dictionary<string, object> { { "@Phone", customerPhone } };
                DataTable dtReviews = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        GR.Reservation_ID AS ReservationID,
                        GR.Overall_Rating AS OverallRating,
                        GR.Review_Text AS ReviewText,
                        GR.Review_Date AS ReviewDate,
                        GR.Status
                      FROM Guest_Reviews GR
                      WHERE GR.Customer_MobilePhone = @Phone
                        AND GR.Status = 'APPROVED'
                      ORDER BY GR.Review_Date DESC",
                    parameters);

                if (dtReviews.Rows.Count > 0)
                {
                    rptReviews.DataSource = dtReviews;
                    rptReviews.DataBind();
                    lblNoReviews.Visible = false;
                }
                else
                {
                    lblNoReviews.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading reviews: " + ex.Message, "error");
            }
        }

        private void LoadPreferences(string customerPhone)
        {
            try
            {
                var parameters = new Dictionary<string, object> { { "@Phone", customerPhone } };
                DataTable dtPrefs = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        PreferenceType,
                        PreferenceValue,
                        Notes
                      FROM Guest_Preferences
                      WHERE Customer_MobilePhone = @Phone
                        AND IsActive = 1
                      ORDER BY PreferenceType",
                    parameters);

                if (dtPrefs.Rows.Count > 0)
                {
                    foreach (DataRow row in dtPrefs.Rows)
                    {
                        Panel prefItem = new Panel { CssClass = "preference-item" };

                        Label prefLabel = new Label
                        {
                            Text = row["PreferenceType"].ToString(),
                            CssClass = "preference-label"
                        };

                        Label prefValue = new Label
                        {
                            Text = row["PreferenceValue"].ToString(),
                            CssClass = "preference-value"
                        };

                        prefItem.Controls.Add(prefLabel);
                        prefItem.Controls.Add(new LiteralControl("<br />"));
                        prefItem.Controls.Add(prefValue);

                        pnlPreferences.Controls.Add(prefItem);
                    }
                    lblNoPreferences.Visible = false;
                }
                else
                {
                    lblNoPreferences.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading preferences: " + ex.Message, "error");
            }
        }

        private void LoadSpecialDates(string customerPhone)
        {
            try
            {
                var parameters = new Dictionary<string, object> { { "@Phone", customerPhone } };
                DataTable dtDates = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        DateType,
                        SpecialDate,
                        Notes
                      FROM Customer_Special_Dates
                      WHERE Customer_MobilePhone = @Phone
                        AND IsActive = 1
                      ORDER BY MONTH(SpecialDate), DAY(SpecialDate)",
                    parameters);

                if (dtDates.Rows.Count > 0)
                {
                    foreach (DataRow row in dtDates.Rows)
                    {
                        Panel dateItem = new Panel { CssClass = "info-row" };

                        Label dateLabel = new Label
                        {
                            Text = row["DateType"].ToString() + ":",
                            CssClass = "info-label"
                        };

                        DateTime specialDate = Convert.ToDateTime(row["SpecialDate"]);
                        Label dateValue = new Label
                        {
                            Text = specialDate.ToString("dd MMMM"),
                            CssClass = "info-value"
                        };

                        dateItem.Controls.Add(dateLabel);
                        dateItem.Controls.Add(dateValue);

                        pnlSpecialDates.Controls.Add(dateItem);
                    }
                    lblNoSpecialDates.Visible = false;
                }
                else
                {
                    lblNoSpecialDates.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading special dates: " + ex.Message, "error");
            }
        }

        private void LoadNotes(string customerPhone)
        {
            try
            {
                var parameters = new Dictionary<string, object> { { "@Phone", customerPhone } };
                DataTable dtNotes = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        GN.Note_Type AS NoteType,
                        GN.Note_Text AS NoteText,
                        GN.Created_Date AS CreatedDate,
                        ISNULL(U.Name, 'System') AS CreatedByName
                      FROM Guest_Notes GN
                      LEFT JOIN [User] U ON U.ID = GN.Created_By_ID
                      WHERE GN.Customer_MobilePhone = @Phone
                        AND GN.IsActive = 1
                      ORDER BY
                        CASE GN.Note_Type
                            WHEN 'WARNING' THEN 1
                            WHEN 'VIP' THEN 2
                            ELSE 3
                        END,
                        GN.Created_Date DESC",
                    parameters);

                if (dtNotes.Rows.Count > 0)
                {
                    rptNotes.DataSource = dtNotes;
                    rptNotes.DataBind();
                    lblNoNotes.Visible = false;
                }
                else
                {
                    lblNoNotes.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading notes: " + ex.Message, "error");
            }
        }

        #region Helper Methods

        private string GetInitials(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "?";

            var words = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
            {
                return (words[0][0].ToString() + words[1][0].ToString()).ToUpper();
            }
            else if (words.Length == 1 && words[0].Length >= 2)
            {
                return words[0].Substring(0, 2).ToUpper();
            }
            return words[0][0].ToString().ToUpper();
        }

        private Tuple<string, int> GetNextTierInfo(int currentPoints)
        {
            try
            {
                DataTable dtTiers = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TierName, MinPoints FROM Loyalty_Tiers " +
                    "WHERE MinPoints > @CurrentPoints AND IsActive = 1 " +
                    "ORDER BY MinPoints ASC",
                    new Dictionary<string, object> { { "@CurrentPoints", currentPoints } });

                if (dtTiers.Rows.Count > 0)
                {
                    string nextTier = dtTiers.Rows[0]["TierName"].ToString();
                    int minPoints = Convert.ToInt32(dtTiers.Rows[0]["MinPoints"]);
                    int pointsNeeded = minPoints - currentPoints;
                    return Tuple.Create(nextTier, pointsNeeded);
                }
                else
                {
                    return Tuple.Create("Max Tier Reached", 0);
                }
            }
            catch
            {
                return Tuple.Create("N/A", 0);
            }
        }

        protected string GetStatusColor(object status)
        {
            string statusStr = status?.ToString() ?? "";
            switch (statusStr.ToUpper())
            {
                case "CONFIRMED":
                case "CHECKIN":
                    return "#28a745";
                case "CHECKOUT":
                    return "#17a2b8";
                case "CANCELLED":
                    return "#dc3545";
                default:
                    return "#6c757d";
            }
        }

        protected string GetStarRating(object rating)
        {
            decimal ratingValue = rating != DBNull.Value ? Convert.ToDecimal(rating) : 0;
            int fullStars = (int)Math.Floor(ratingValue);
            bool hasHalfStar = (ratingValue - fullStars) >= 0.5m;
            int emptyStars = 5 - fullStars - (hasHalfStar ? 1 : 0);

            StringBuilder stars = new StringBuilder();
            for (int i = 0; i < fullStars; i++)
            {
                stars.Append("<i class='fa fa-star'></i>");
            }
            if (hasHalfStar)
            {
                stars.Append("<i class='fa fa-star-half-o'></i>");
            }
            for (int i = 0; i < emptyStars; i++)
            {
                stars.Append("<i class='fa fa-star-o'></i>");
            }
            return stars.ToString();
        }

        protected string GetNoteIcon(object noteType)
        {
            string type = noteType?.ToString() ?? "";
            switch (type.ToUpper())
            {
                case "WARNING":
                    return "<i class='fa fa-exclamation-triangle'></i>";
                case "VIP":
                    return "<i class='fa fa-star'></i>";
                case "COMPLAINT":
                    return "<i class='fa fa-frown-o'></i>";
                case "COMPLIMENT":
                    return "<i class='fa fa-smile-o'></i>";
                default:
                    return "<i class='fa fa-sticky-note'></i>";
            }
        }

        private void ShowAlert(string message, string type)
        {
            pnlAlert.Visible = true;
            lblAlertMessage.Text = message;

            string cssClass = "alert";
            switch (type.ToLower())
            {
                case "success":
                    cssClass += " alert-success";
                    break;
                case "error":
                    cssClass += " alert-error";
                    break;
            }

            alertBox.Attributes["class"] = cssClass;
        }

        #endregion
    }
}
