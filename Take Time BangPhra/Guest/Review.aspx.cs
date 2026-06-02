using System;
using System.Data;
using System.IO;
using System.Web;
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

        private bool ValidateGuestSession()
        {
            string sessionToken = Request.Cookies["GuestSession"]?.Value ?? Session["GuestSessionToken"]?.ToString();

            if (string.IsNullOrEmpty(sessionToken))
                return false;

            DataTable dtSession = _guestPortalService.ValidateGuestSession(sessionToken);

            if (dtSession.Rows.Count == 0)
                return false;

            DataRow session = dtSession.Rows[0];
            _reservationId = Convert.ToInt64(session["Reservation_ID"]);
            _guestMobilePhone = session["Customer_MobilePhone"].ToString();

            try
            {
                DataTable dtCustomer = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Name FROM Customer WHERE MobilePhone = @Phone",
                    new System.Collections.Generic.Dictionary<string, object> { { "@Phone", _guestMobilePhone } });

                if (dtCustomer.Rows.Count > 0)
                    _customerName = dtCustomer.Rows[0]["Name"].ToString();
            }
            catch { }

            return true;
        }

        private void LoadMemberStatus()
        {
            try
            {
                lblGuestName.Text = !string.IsNullOrEmpty(_customerName) ? _customerName : "Guest";

                var memberInfo = _loyaltyService.GetLoyaltyInfo(_guestMobilePhone);

                if (memberInfo != null)
                {
                    int currentPoints = memberInfo.TotalPoints;
                    string currentTier = memberInfo.TierName ?? "Bronze";

                    lblCurrentPoints.Text = currentPoints.ToString("N0");
                    lblCurrentTier.Text = currentTier;

                    switch (currentTier.ToLower())
                    {
                        case "silver":
                        case "สมาชิกเงิน":
                            memberBadge.Attributes["class"] = "member-badge silver";
                            lblNextTier.Text = "Gold";
                            lblPointsToNext.Text = Math.Max(0, 5000 - currentPoints).ToString("N0");
                            progressBar.Style["width"] = $"{Math.Min(100, (currentPoints - 1000) * 100 / 4000)}%";
                            tierSilver.Attributes["class"] = "tier-card silver current";
                            break;
                        case "gold":
                        case "สมาชิกทอง":
                            memberBadge.Attributes["class"] = "member-badge gold";
                            lblNextTier.Text = "Platinum";
                            lblPointsToNext.Text = Math.Max(0, 15000 - currentPoints).ToString("N0");
                            progressBar.Style["width"] = $"{Math.Min(100, (currentPoints - 5000) * 100 / 10000)}%";
                            tierGold.Attributes["class"] = "tier-card gold current";
                            break;
                        case "platinum":
                        case "สมาชิกแพลทินัม":
                            memberBadge.Attributes["class"] = "member-badge platinum";
                            lblNextTier.Text = "VIP";
                            lblPointsToNext.Text = Math.Max(0, 50000 - currentPoints).ToString("N0");
                            progressBar.Style["width"] = $"{Math.Min(100, (currentPoints - 15000) * 100 / 35000)}%";
                            tierPlatinum.Attributes["class"] = "tier-card platinum current";
                            break;
                        case "vip":
                        case "สมาชิก vip":
                            memberBadge.Attributes["class"] = "member-badge platinum";
                            lblNextTier.Text = "Max Level!";
                            lblPointsToNext.Text = "0";
                            progressBar.Style["width"] = "100%";
                            tierPlatinum.Attributes["class"] = "tier-card platinum current";
                            break;
                        default:
                            memberBadge.Attributes["class"] = "member-badge bronze";
                            lblNextTier.Text = "Silver";
                            lblPointsToNext.Text = Math.Max(0, 1000 - currentPoints).ToString("N0");
                            progressBar.Style["width"] = $"{Math.Min(100, currentPoints * 100 / 1000)}%";
                            tierBronze.Attributes["class"] = "tier-card bronze current";
                            break;
                    }
                }
                else
                {
                    memberBadge.Attributes["class"] = "member-badge bronze";
                    tierBronze.Attributes["class"] = "tier-card bronze current";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading member status: {ex.Message}");
            }
        }

        private void LoadReviewHistory()
        {
            try
            {
                DataTable dtReviews = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 10
                        'Google Review' AS ReviewType,
                        TransactionDate AS ReviewDate,
                        Points AS PointsEarned
                      FROM Loyalty_Transactions
                      WHERE Customer_MobilePhone = @Phone
                        AND TransactionType = 'EARN'
                        AND Description LIKE '%Review%'
                      ORDER BY TransactionDate DESC",
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
                lblNoReviews.Visible = true;
            }
        }

        protected void btnConfirmReview_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidateGuestSession())
                {
                    Response.Redirect("~/Guest/Portal");
                    return;
                }

                // Require screenshot proof
                if (!fuReviewScreenshot.HasFile)
                {
                    lblReviewStatus.Text = "<div class='alert alert-warning'><i class='fas fa-exclamation-triangle'></i> กรุณาแนบภาพหน้าจอรีวิวเพื่อยืนยัน</div>";
                    return;
                }

                // Validate file type
                string ext = Path.GetExtension(fuReviewScreenshot.FileName).ToLower();
                string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                if (Array.IndexOf(allowedExtensions, ext) < 0)
                {
                    lblReviewStatus.Text = "<div class='alert alert-warning'><i class='fas fa-exclamation-triangle'></i> กรุณาอัพโหลดไฟล์รูปภาพเท่านั้น (JPG, PNG, GIF, WEBP)</div>";
                    return;
                }

                if (fuReviewScreenshot.PostedFile.ContentLength > 10 * 1024 * 1024)
                {
                    lblReviewStatus.Text = "<div class='alert alert-warning'><i class='fas fa-exclamation-triangle'></i> ขนาดไฟล์ต้องไม่เกิน 10 MB</div>";
                    return;
                }

                // Check if already reviewed today
                DataTable dtExisting = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS ReviewCount
                      FROM Loyalty_Transactions
                      WHERE Customer_MobilePhone = @Phone
                        AND TransactionType = 'EARN'
                        AND Description LIKE '%Review%'
                        AND CAST(TransactionDate AS DATE) = CAST(GETDATE() AS DATE)",
                    new System.Collections.Generic.Dictionary<string, object> { { "@Phone", _guestMobilePhone } });

                if (dtExisting.Rows.Count > 0 && Convert.ToInt32(dtExisting.Rows[0]["ReviewCount"]) > 0)
                {
                    lblReviewStatus.Text = "<div class='alert alert-warning'><i class='fas fa-exclamation-triangle'></i> คุณได้รับแต้มจากการรีวิววันนี้แล้ว กรุณารอวันถัดไป</div>";
                    return;
                }

                // Check if already reviewed this reservation
                DataTable dtReservationReview = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS ReviewCount
                      FROM Loyalty_Transactions
                      WHERE Customer_MobilePhone = @Phone
                        AND TransactionType = 'EARN'
                        AND Description LIKE '%Review%'
                        AND Reservation_ID = @ReservationId",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@Phone", _guestMobilePhone },
                        { "@ReservationId", _reservationId }
                    });

                if (dtReservationReview.Rows.Count > 0 && Convert.ToInt32(dtReservationReview.Rows[0]["ReviewCount"]) > 0)
                {
                    lblReviewStatus.Text = "<div class='alert alert-info'><i class='fas fa-info-circle'></i> คุณได้รีวิวการเข้าพักครั้งนี้แล้ว ขอบคุณสำหรับความคิดเห็นของคุณ!</div>";
                    return;
                }

                // Save screenshot
                string uploadFolder = Server.MapPath("~/Uploads/ReviewScreenshots/");
                if (!Directory.Exists(uploadFolder))
                    Directory.CreateDirectory(uploadFolder);

                string fileName = $"review_{_guestMobilePhone}_{_reservationId}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                string filePath = Path.Combine(uploadFolder, fileName);
                fuReviewScreenshot.SaveAs(filePath);
                string relativePath = $"~/Uploads/ReviewScreenshots/{fileName}";

                // Award points via LoyaltyService
                int pointsToAward = 100;
                string description = $"Google Review - Reservation #{_reservationId}";

                var result = _loyaltyService.EarnPoints(
                    _guestMobilePhone,
                    pointsToAward,
                    _reservationId,
                    null,
                    description,
                    12,
                    null
                );

                if (result.Success)
                {
                    // Store screenshot reference in Guest_Reviews
                    try
                    {
                        _code.DatabaseInsertSafe(_connectionString,
                            @"INSERT INTO Guest_Reviews
                              (Reservation_ID, Customer_MobilePhone, OverallRating, ReviewTitle, ReviewText, Status, Source, SubmittedDate)
                              VALUES
                              (@ReservationId, @Phone, 5, @Title, @ScreenshotPath, 'PENDING', 'GOOGLE', GETDATE())",
                            new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "@ReservationId", _reservationId },
                                { "@Phone", _guestMobilePhone },
                                { "@Title", "Google Review" },
                                { "@ScreenshotPath", relativePath }
                            });
                    }
                    catch { }

                    lblReviewStatus.Text = $"<div class='alert alert-success'><i class='fas fa-check-circle'></i> ยินดีด้วย! คุณได้รับ <strong>{pointsToAward} Points</strong> จากการรีวิว ขอบคุณที่แบ่งปันประสบการณ์ของคุณ!</div>";

                    LoadMemberStatus();
                    LoadReviewHistory();
                }
                else
                {
                    lblReviewStatus.Text = $"<div class='alert alert-danger'><i class='fas fa-times-circle'></i> เกิดข้อผิดพลาด: {result.Message}</div>";
                }
            }
            catch (Exception ex)
            {
                lblReviewStatus.Text = $"<div class='alert alert-danger'><i class='fas fa-times-circle'></i> เกิดข้อผิดพลาด: {ex.Message}</div>";
            }
        }
    }
}
