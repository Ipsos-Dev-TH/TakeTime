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
            if (!Feature.Guard(this, "Reviews", "~/Guest/Dashboard")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
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
                    // Headline = redeemable balance (คะแนนที่แลกได้)
                    lblCurrentPoints.Text = memberInfo.AvailablePoints.ToString("N0");
                    lblCurrentTier.Text = memberInfo.TierName ?? "Member";

                    // Tier progress is based on YEARLY (tier-qualifying) points
                    if (memberInfo.NextTierMinPoints.HasValue)
                    {
                        lblNextTier.Text = memberInfo.NextTierName ?? "";
                        lblPointsToNext.Text = memberInfo.PointsToNextTier.ToString("N0");
                    }
                    else
                    {
                        lblNextTier.Text = "Max Level!";
                        lblPointsToNext.Text = "0";
                    }
                    progressBar.Style["width"] = $"{memberInfo.TierProgressPercent}%";

                    // Badge + highlight the customer's current tier card
                    string tierEn = (memberInfo.TierNameEN ?? "").ToLower();
                    string badgeClass;
                    if (tierEn.Contains("silver")) { badgeClass = "silver"; tierSilver.Attributes["class"] = "tier-card silver current"; }
                    else if (tierEn.Contains("gold")) { badgeClass = "gold"; tierGold.Attributes["class"] = "tier-card gold current"; }
                    else if (tierEn.Contains("platinum")) { badgeClass = "platinum"; tierPlatinum.Attributes["class"] = "tier-card platinum current"; }
                    else if (tierEn.Contains("vip")) { badgeClass = "platinum"; tierPlatinum.Attributes["class"] = "tier-card platinum current"; }
                    else { badgeClass = "bronze"; tierBronze.Attributes["class"] = "tier-card bronze current"; }
                    memberBadge.Attributes["class"] = "member-badge " + badgeClass;
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

                // Review reward is limited to ONCE PER CALENDAR YEAR
                if (_loyaltyService.HasReviewedThisYear(_guestMobilePhone))
                {
                    lblReviewStatus.Text = $"<div class='alert alert-info'><i class='fas fa-info-circle'></i> คุณได้รับแต้มจากการรีวิวในปี {DateTime.Now.Year + 543} แล้ว สามารถรีวิวรับแต้มได้อีกครั้งในปีถัดไป ขอบคุณที่แบ่งปันประสบการณ์ของคุณ!</div>";
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

                    // Mark review timestamp (best-effort)
                    _loyaltyService.MarkReviewed(_guestMobilePhone);

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
