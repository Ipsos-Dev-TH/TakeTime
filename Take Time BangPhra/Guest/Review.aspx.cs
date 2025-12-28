using System;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class Review : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private LoyaltyService _loyaltyService;
        private code _code;
        private long _reservationId;
        private string _guestMobilePhone;
        private string _customerName;

        protected void Page_Load(object sender, EventArgs e)
        {
            _guestPortalService = new GuestPortalService(_connectionString);
            _loyaltyService = new LoyaltyService(_connectionString);
            _code = new code();

            // Check session
            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
            }

            if (!IsPostBack)
            {
                LoadMemberStatus();
                LoadReviewHistory();
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

            // Get customer name
            try
            {
                DataTable dtCustomer = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Customer_Name FROM Customer WHERE Customer_MobilePhone = @Phone",
                    new System.Collections.Generic.Dictionary<string, object> { { "@Phone", _guestMobilePhone } });

                if (dtCustomer.Rows.Count > 0)
                {
                    _customerName = dtCustomer.Rows[0]["Customer_Name"].ToString();
                }
            }
            catch { }

            return true;
        }

        /// <summary>
        /// Load member status and points
        /// </summary>
        private void LoadMemberStatus()
        {
            try
            {
                lblGuestName.Text = !string.IsNullOrEmpty(_customerName) ? _customerName : "Guest";

                // Get loyalty points
                var memberInfo = _loyaltyService.GetLoyaltyInfo(_guestMobilePhone);

                if (memberInfo != null)
                {
                    int currentPoints = memberInfo.TotalPoints;
                    string currentTier = memberInfo.TierName ?? "Bronze";

                    lblCurrentPoints.Text = currentPoints.ToString("N0");
                    lblCurrentTier.Text = currentTier;

                    // Set badge style based on tier
                    switch (currentTier.ToLower())
                    {
                        case "silver":
                            memberBadge.Attributes["class"] = "member-badge silver";
                            lblNextTier.Text = "Gold";
                            lblPointsToNext.Text = (1000 - currentPoints).ToString("N0");
                            progressBar.Style["width"] = $"{Math.Min(100, (currentPoints - 500) * 100 / 500)}%";
                            tierSilver.Attributes["class"] = "tier-card silver current";
                            break;
                        case "gold":
                            memberBadge.Attributes["class"] = "member-badge gold";
                            lblNextTier.Text = "Platinum";
                            lblPointsToNext.Text = (2500 - currentPoints).ToString("N0");
                            progressBar.Style["width"] = $"{Math.Min(100, (currentPoints - 1000) * 100 / 1500)}%";
                            tierGold.Attributes["class"] = "tier-card gold current";
                            break;
                        case "platinum":
                            memberBadge.Attributes["class"] = "member-badge platinum";
                            lblNextTier.Text = "Max Level!";
                            lblPointsToNext.Text = "0";
                            progressBar.Style["width"] = "100%";
                            tierPlatinum.Attributes["class"] = "tier-card platinum current";
                            break;
                        default: // Bronze
                            memberBadge.Attributes["class"] = "member-badge bronze";
                            lblNextTier.Text = "Silver";
                            lblPointsToNext.Text = (500 - currentPoints).ToString("N0");
                            progressBar.Style["width"] = $"{Math.Min(100, currentPoints * 100 / 500)}%";
                            tierBronze.Attributes["class"] = "tier-card bronze current";
                            break;
                    }
                }
                else
                {
                    // New member
                    memberBadge.Attributes["class"] = "member-badge bronze";
                    tierBronze.Attributes["class"] = "tier-card bronze current";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading member status: {ex.Message}");
            }
        }

        /// <summary>
        /// Load review history
        /// </summary>
        private void LoadReviewHistory()
        {
            try
            {
                // Check if Guest_Review_Points table exists, if not use a simple query
                DataTable dtReviews = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 10
                        'Google Review' AS ReviewType,
                        Created_Date AS ReviewDate,
                        Points AS PointsEarned
                      FROM Customer_Loyalty_Points
                      WHERE Customer_MobilePhone = @Phone
                        AND Transaction_Type = 'REVIEW'
                      ORDER BY Created_Date DESC",
                    new System.Collections.Generic.Dictionary<string, object> { { "@Phone", _guestMobilePhone } });

                if (dtReviews.Rows.Count > 0)
                {
                    rptReviewHistory.DataSource = dtReviews;
                    rptReviewHistory.DataBind();
                    lblNoReviews.Visible = false;
                }
                else
                {
                    lblNoReviews.Visible = true;
                }
            }
            catch
            {
                // Table might not exist yet
                lblNoReviews.Visible = true;
            }
        }

        /// <summary>
        /// Confirm review and award points
        /// </summary>
        protected void btnConfirmReview_Click(object sender, EventArgs e)
        {
            try
            {
                // Re-validate session
                if (!ValidateGuestSession())
                {
                    Response.Redirect("~/Guest/Portal");
                    return;
                }

                // Check if already reviewed today
                DataTable dtExisting = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS ReviewCount
                      FROM Customer_Loyalty_Points
                      WHERE Customer_MobilePhone = @Phone
                        AND Transaction_Type = 'REVIEW'
                        AND CAST(Created_Date AS DATE) = CAST(GETDATE() AS DATE)",
                    new System.Collections.Generic.Dictionary<string, object> { { "@Phone", _guestMobilePhone } });

                if (dtExisting.Rows.Count > 0 && Convert.ToInt32(dtExisting.Rows[0]["ReviewCount"]) > 0)
                {
                    lblReviewStatus.Text = "<div class='alert alert-warning'><i class='fas fa-exclamation-triangle'></i> คุณได้รับแต้มจากการรีวิววันนี้แล้ว กรุณารอวันถัดไป</div>";
                    lblReviewStatus.CssClass = "";
                    return;
                }

                // Check if reviewed this reservation
                DataTable dtReservationReview = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS ReviewCount
                      FROM Customer_Loyalty_Points
                      WHERE Customer_MobilePhone = @Phone
                        AND Transaction_Type = 'REVIEW'
                        AND Reference_ID = @ReservationId",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@Phone", _guestMobilePhone },
                        { "@ReservationId", _reservationId.ToString() }
                    });

                if (dtReservationReview.Rows.Count > 0 && Convert.ToInt32(dtReservationReview.Rows[0]["ReviewCount"]) > 0)
                {
                    lblReviewStatus.Text = "<div class='alert alert-info'><i class='fas fa-info-circle'></i> คุณได้รีวิวการเข้าพักครั้งนี้แล้ว ขอบคุณสำหรับความคิดเห็นของคุณ!</div>";
                    lblReviewStatus.CssClass = "";
                    return;
                }

                // Award points for review
                int pointsToAward = 100;

                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@CustomerPhone", _guestMobilePhone },
                    { "@Points", pointsToAward },
                    { "@TransactionType", "REVIEW" },
                    { "@Description", "Google Review - Reservation #" + _reservationId },
                    { "@ReferenceId", _reservationId.ToString() },
                    { "@CreatedDate", DateTime.Now }
                };

                int result = _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO Customer_Loyalty_Points
                      (Customer_MobilePhone, Points, Transaction_Type, Description, Reference_ID, Created_Date)
                      VALUES
                      (@CustomerPhone, @Points, @TransactionType, @Description, @ReferenceId, @CreatedDate)",
                    parameters);

                if (result > 0)
                {
                    // Update total points in Customer table
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Customer
                          SET Loyalty_Points = ISNULL(Loyalty_Points, 0) + @Points
                          WHERE Customer_MobilePhone = @Phone",
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "@Points", pointsToAward },
                            { "@Phone", _guestMobilePhone }
                        });

                    // Check and update tier
                    UpdateMemberTier();

                    lblReviewStatus.Text = $"<div class='alert alert-success'><i class='fas fa-check-circle'></i> ยินดีด้วย! คุณได้รับ <strong>{pointsToAward} Points</strong> จากการรีวิว ขอบคุณที่แบ่งปันประสบการณ์ของคุณ!</div>";
                    lblReviewStatus.CssClass = "";

                    // Reload member status
                    LoadMemberStatus();
                    LoadReviewHistory();
                }
                else
                {
                    lblReviewStatus.Text = "<div class='alert alert-danger'><i class='fas fa-times-circle'></i> เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง</div>";
                    lblReviewStatus.CssClass = "";
                }
            }
            catch (Exception ex)
            {
                lblReviewStatus.Text = $"<div class='alert alert-danger'><i class='fas fa-times-circle'></i> เกิดข้อผิดพลาด: {ex.Message}</div>";
                lblReviewStatus.CssClass = "";
            }
        }

        /// <summary>
        /// Update member tier based on total points
        /// </summary>
        private void UpdateMemberTier()
        {
            try
            {
                // Get current total points
                DataTable dtPoints = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ISNULL(Loyalty_Points, 0) AS TotalPoints FROM Customer WHERE Customer_MobilePhone = @Phone",
                    new System.Collections.Generic.Dictionary<string, object> { { "@Phone", _guestMobilePhone } });

                if (dtPoints.Rows.Count > 0)
                {
                    int totalPoints = Convert.ToInt32(dtPoints.Rows[0]["TotalPoints"]);
                    string newTier = "BRONZE";

                    if (totalPoints >= 2500)
                        newTier = "PLATINUM";
                    else if (totalPoints >= 1000)
                        newTier = "GOLD";
                    else if (totalPoints >= 500)
                        newTier = "SILVER";

                    // Update tier in Customer table
                    _code.DatabaseInsertSafe(_connectionString,
                        "UPDATE Customer SET Loyalty_Tier = @Tier WHERE Customer_MobilePhone = @Phone",
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "@Tier", newTier },
                            { "@Phone", _guestMobilePhone }
                        });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating tier: {ex.Message}");
            }
        }
    }
}
