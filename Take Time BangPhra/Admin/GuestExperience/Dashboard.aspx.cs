using System;
using System.Data;
using System.Web;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.GuestExperience
{
    public partial class Dashboard : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.MgtDashboard)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            _code = new code();

            if (!IsPostBack)
            {
                LoadExperienceData();
                LoadReviewData();
            }
        }

        /// <summary>
        /// ส่วนรีวิว: ค่าจริงจาก ReviewQuery (ตัวคำนวณเดียวกับหน้า ReviewAnalytics) — เดิมเป็นข้อความตัวอย่างตายตัว.
        /// คะแนนเฉลี่ยไม่นับคะแนนที่ไม่ทราบ (NULL/0) และไม่นับรีวิวที่ "ตัดออก".
        /// </summary>
        private void LoadReviewData()
        {
            litRecentReviews.Text = "<div class='review-text'>ยังไม่มีรีวิว</div>";
            try
            {
                var kpi = Reviews.ReviewQuery.GetHeadline(_connectionString, 365);
                lblOverallScore.Text = kpi["avg5"] == null ? "-" : Convert.ToDouble(kpi["avg5"]).ToString("0.0");
                lblReviewCount.Text = string.Format("{0:N0} รีวิว (มีคะแนน {1:N0})", kpi["total"], kpi["rated"]);
                lblResponseRate.Text = kpi["responseRate"] == null ? "-" : Convert.ToDouble(kpi["responseRate"]).ToString("0.#") + "%";
                lblAvgResponse.Text = FormatHours(kpi["avgResponseHours"]);
                double rate = kpi["responseRate"] == null ? 0 : Convert.ToDouble(kpi["responseRate"]);
                respBar.Style["width"] = Math.Max(0, Math.Min(100, rate)).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";

                var sub = Reviews.ReviewQuery.GetInternalSubScores(_connectionString);
                lblCleanliness.Text = SubScore(sub, "CleanlinessRating");
                lblService.Text = SubScore(sub, "ServiceRating");
                lblValue.Text = SubScore(sub, "ValueForMoneyRating");
                lblLocation.Text = SubScore(sub, "LocationRating");

                var latest = Reviews.ReviewQuery.GetLatest(_connectionString, 3);
                if (latest.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var r in latest)
                    {
                        string text = Convert.ToString(r["text"] ?? "");
                        if (text.Length > 160) text = text.Substring(0, 160) + "…";
                        sb.Append("<div class='review-item'><div class='review-header'><span class='review-name'>")
                          .Append(HttpUtility.HtmlEncode(Convert.ToString(r["reviewer"] ?? "ไม่ระบุชื่อ")))
                          .Append(" <span style='font-weight:400;color:#999;font-size:12px'>· ").Append(HttpUtility.HtmlEncode(Convert.ToString(r["platform"]))).Append("</span></span>")
                          .Append("<span class='review-rating'>").Append(Stars(r["r5"])).Append("</span></div>")
                          .Append("<div class='review-text'>").Append(HttpUtility.HtmlEncode(text)).Append("</div>")
                          .Append("<div class='review-date'>").Append(HttpUtility.HtmlEncode(Ago(r["date"] as string))).Append("</div></div>");
                    }
                    litRecentReviews.Text = sb.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading review data: " + ex.Message);
            }
        }

        private static string SubScore(System.Collections.Generic.Dictionary<string, double?> d, string key)
        {
            double? v;
            return d.TryGetValue(key, out v) && v.HasValue ? v.Value.ToString("0.0") : "-";
        }

        private static string FormatHours(object h)
        {
            if (h == null) return "-";
            double v = Convert.ToDouble(h);
            if (v < 1) return Math.Round(v * 60) + " นาที";
            if (v < 48) return v.ToString("0.#") + " ชม.";
            return (v / 24).ToString("0.#") + " วัน";
        }

        private static string Stars(object r5)
        {
            if (r5 == null) return "<span style='color:#bbb;font-size:12px'>ไม่มีคะแนน</span>";
            int full = (int)Math.Round(Convert.ToDouble(r5), MidpointRounding.AwayFromZero);
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i <= 5; i++) sb.Append(i <= full ? "<i class='fas fa-star'></i>" : "<i class='far fa-star'></i>");
            return sb.ToString();
        }

        private static string Ago(string iso)
        {
            DateTime d;
            if (string.IsNullOrEmpty(iso) || !DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d)) return "";
            int days = (int)(DateTime.Today - d.Date).TotalDays;
            if (days <= 0) return "วันนี้";
            if (days == 1) return "เมื่อวาน";
            if (days < 60) return days + " วันที่แล้ว";
            return d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void LoadExperienceData()
        {
            try
            {
                // Load loyalty members count
                DataTable dtLoyalty = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS MemberCount
                      FROM Customer
                      WHERE ISNULL(Loyalty_Points, 0) > 0",
                    null);

                if (dtLoyalty.Rows.Count > 0)
                {
                    lblLoyaltyMembers.Text = dtLoyalty.Rows[0]["MemberCount"].ToString();
                }

                // Load returning guest percentage
                DataTable dtReturning = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        CASE WHEN TotalGuests > 0
                             THEN CAST(ReturningGuests * 100.0 / TotalGuests AS DECIMAL(5,1))
                             ELSE 0
                        END AS ReturningPercent
                      FROM (
                          SELECT
                              COUNT(DISTINCT Customer_MobilePhone) AS TotalGuests,
                              SUM(CASE WHEN BookingCount > 1 THEN 1 ELSE 0 END) AS ReturningGuests
                          FROM (
                              SELECT Customer_MobilePhone, COUNT(*) AS BookingCount
                              FROM Reservation
                              WHERE Status NOT IN (N'ยกเลิก')
                              GROUP BY Customer_MobilePhone
                          ) t
                      ) x",
                    null);

                if (dtReturning.Rows.Count > 0)
                {
                    lblReturning.Text = dtReturning.Rows[0]["ReturningPercent"].ToString() + "%";
                }

                // Load pending messages
                DataTable dtMessages = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS PendingCount
                      FROM Guest_Chat
                      WHERE IsFromGuest = 1 AND (Is_Read = 0 OR Is_Read IS NULL)",
                    null);

                if (dtMessages.Rows.Count > 0)
                {
                    lblPendingMessages.Text = dtMessages.Rows[0]["PendingCount"].ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading experience data: {ex.Message}");
            }
        }
    }
}
