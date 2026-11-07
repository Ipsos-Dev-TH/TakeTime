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
            if (!Perm.Guard(this, Perm.CrmCustomer)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
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
                    "SELECT * FROM vw_Guest_Profile_360 WHERE MobilePhone = @Phone",
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
                string fullName = profile["Name"]?.ToString() ?? "N/A";
                lblCustomerName.Text = fullName;
                lblInitials.Text = GetInitials(fullName);
                lblPhone.Text = profile["MobilePhone"]?.ToString() ?? "";
                lblEmail.Text = profile["Email"]?.ToString() ?? "Not provided";

                // Member Since
                if (profile["MemberSince"] != DBNull.Value)
                {
                    lblMemberSince.Text = Convert.ToDateTime(profile["MemberSince"]).ToString("MMM yyyy");
                }
                else if (profile["FirstStayDate"] != DBNull.Value)
                {
                    lblMemberSince.Text = Convert.ToDateTime(profile["FirstStayDate"]).ToString("MMM yyyy");
                }
                else
                {
                    lblMemberSince.Text = "New";
                }

                // Loyalty Badge
                string tierName = profile["LoyaltyTier"]?.ToString() ?? "Member";
                lblTierName.Text = tierName;
                if (tierName == "VIP" || tierName == "สมาชิก VIP")
                {
                    pnlVipBadge.Visible = true;
                }

                // Statistics
                lblPoints.Text = profile["AvailablePoints"]?.ToString() ?? "0";
                lblTotalBookings.Text = profile["TotalReservations"]?.ToString() ?? "0";

                decimal lifetimeValue = profile["TotalSpend"] != DBNull.Value
                    ? Convert.ToDecimal(profile["TotalSpend"])
                    : 0;
                lblLifetimeValue.Text = lifetimeValue.ToString("N0");

                decimal avgRating = profile["AvgRating"] != DBNull.Value
                    ? Convert.ToDecimal(profile["AvgRating"])
                    : 0;
                lblAvgRating.Text = avgRating.ToString("N1");

                // Basic Information
                lblFullName.Text = fullName;
                lblNickname.Text = "N/A"; // NickName not in view
                lblIDNumber.Text = profile["IDNumber"]?.ToString() ?? "Not provided";
                lblAddress.Text = profile["Address"]?.ToString() ?? "Not provided";
                lblSegment.Text = profile["Segments"]?.ToString() ?? "Not segmented";

                // Loyalty Status
                lblCurrentTier.Text = tierName;
                lblCurrentPoints.Text = profile["AvailablePoints"]?.ToString() ?? "0";

                // Calculate next tier
                int currentPoints = profile["AvailablePoints"] != DBNull.Value
                    ? Convert.ToInt32(profile["AvailablePoints"])
                    : 0;
                var nextTierInfo = GetNextTierInfo(currentPoints);
                lblNextTier.Text = nextTierInfo.Item1;
                lblPointsToNext.Text = nextTierInfo.Item2.ToString("N0");

                lblMultiplier.Text = "1.0"; // Not in view
                lblDiscountPercent.Text = "0"; // Not in view

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
                            SELECT ', ' + A.AccomName
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
                        GR.OverallRating,
                        GR.ReviewText,
                        GR.SubmittedDate AS ReviewDate,
                        GR.Status
                      FROM Guest_Reviews GR
                      WHERE GR.Customer_MobilePhone = @Phone
                        AND GR.Status = 'APPROVED'
                      ORDER BY GR.SubmittedDate DESC",
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
                // Check if table exists first
                var checkParams = new Dictionary<string, object>();
                DataTable dtCheck = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT COUNT(*) AS TableExists FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_Preferences'",
                    checkParams);

                if (dtCheck.Rows.Count == 0 || Convert.ToInt32(dtCheck.Rows[0]["TableExists"]) == 0)
                {
                    lblNoPreferences.Visible = true;
                    return;
                }

                var parameters = new Dictionary<string, object> { { "@Phone", customerPhone } };
                DataTable dtPrefs = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        PreferenceType,
                        PreferenceKey,
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
                            Text = row["PreferenceType"].ToString() + " - " + row["PreferenceKey"].ToString(),
                            CssClass = "preference-label"
                        };

                        Label prefValue = new Label
                        {
                            Text = Convert.ToString(row["PreferenceValue"]),
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
            catch
            {
                // Silently fail if table doesn't exist
                lblNoPreferences.Visible = true;
            }
        }

        private void LoadSpecialDates(string customerPhone)
        {
            try
            {
                // Check if table exists first
                var checkParams = new Dictionary<string, object>();
                DataTable dtCheck = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT COUNT(*) AS TableExists FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Customer_Special_Dates'",
                    checkParams);

                if (dtCheck.Rows.Count == 0 || Convert.ToInt32(dtCheck.Rows[0]["TableExists"]) == 0)
                {
                    lblNoSpecialDates.Visible = true;
                    return;
                }

                var parameters = new Dictionary<string, object> { { "@Phone", customerPhone } };
                DataTable dtDates = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        OccasionType,
                        OccasionName,
                        OccasionDate,
                        SpecialNotes
                      FROM Customer_Special_Dates
                      WHERE Customer_MobilePhone = @Phone
                      ORDER BY MONTH(OccasionDate), DAY(OccasionDate)",
                    parameters);

                if (dtDates.Rows.Count > 0)
                {
                    foreach (DataRow row in dtDates.Rows)
                    {
                        Panel dateItem = new Panel { CssClass = "info-row" };

                        string occasionName = Convert.ToString(row["OccasionName"]);
                        string occasionType = Convert.ToString(row["OccasionType"]);
                        string labelText = !string.IsNullOrEmpty(occasionName) ? occasionName : occasionType;

                        Label dateLabel = new Label
                        {
                            Text = labelText + ":",
                            CssClass = "info-label"
                        };

                        DateTime specialDate = Convert.ToDateTime(row["OccasionDate"]);
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
                // Check if Guest_Notes table exists first
                DataTable dtCheck = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_Notes'", null);

                if (dtCheck.Rows.Count == 0)
                {
                    // Table doesn't exist yet - show no notes message
                    lblNoNotes.Visible = true;
                    return;
                }

                var parameters = new Dictionary<string, object> { { "@Phone", customerPhone } };
                DataTable dtNotes = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        NoteType,
                        NoteText,
                        CreatedDate,
                        ISNULL(A.FirstName + ' ' + A.LastName, 'System') AS CreatedByName
                      FROM Guest_Notes GN
                      LEFT JOIN Admin A ON A.ID = GN.CreatedBy_AdminID
                      WHERE GN.CustomerPhone = @Phone
                        AND GN.IsActive = 1
                      ORDER BY
                        CASE GN.NoteType
                            WHEN 'WARNING' THEN 1
                            WHEN 'VIP' THEN 2
                            ELSE 3
                        END,
                        GN.CreatedDate DESC",
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
            catch (Exception)
            {
                // Table doesn't exist or has different schema - show no notes
                lblNoNotes.Visible = true;
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
