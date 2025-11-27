// ===========================================================================
// ReviewManagement.aspx.cs
// Review Management - Approve/reject reviews, add responses, view analytics
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using Take_Time_BangPhra.Class;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.CRM
{
    public partial class ReviewManagement : System.Web.UI.Page
    {
        private readonly string _connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;
        private ReviewService _reviewService;

        protected void Page_Load(object sender, EventArgs e)
        {
            _code = new code();
            _reviewService = new ReviewService(_connectionString);

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
                LoadStatistics();
                LoadReviews();
            }
        }

        private void LoadStatistics()
        {
            try
            {
                var analytics = _reviewService.GetAnalytics();

                if (analytics != null)
                {
                    lblTotalReviews.Text = analytics.TotalReviews.ToString("N0");
                    lblAvgRating.Text = analytics.AvgOverallRating.ToString("N1");
                    lblPending.Text = analytics.PendingReviews.ToString("N0");
                    lblApproved.Text = analytics.ApprovedReviews.ToString("N0");

                    // Calculate response rate from approved reviews with management response
                    var parameters = new System.Collections.Generic.Dictionary<string, object>();
                    DataTable dtWithResponse = _code.DatabaseQuerySafe(_connectionString,
                        "SELECT COUNT(*) AS Count FROM Guest_Reviews WHERE Status = 'APPROVED' AND ResponseText IS NOT NULL",
                        parameters);

                    int reviewsWithResponse = dtWithResponse.Rows.Count > 0 ? Convert.ToInt32(dtWithResponse.Rows[0]["Count"]) : 0;
                    decimal responseRate = analytics.TotalReviews > 0
                        ? (decimal)reviewsWithResponse / analytics.TotalReviews * 100
                        : 0;
                    lblResponseRate.Text = responseRate.ToString("N0");
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading statistics: " + ex.Message, "error");
            }
        }

        private void LoadReviews()
        {
            try
            {
                StringBuilder query = new StringBuilder(@"
                    SELECT
                        GR.ID,
                        GR.Reservation_ID,
                        GR.Customer_MobilePhone,
                        C.Name AS Customer_Name,
                        GR.OverallRating,
                        GR.CleanlinessRating,
                        GR.ServiceRating,
                        GR.ValueForMoneyRating,
                        GR.LocationRating,
                        GR.FacilitiesRating,
                        GR.ReviewText,
                        GR.SubmittedDate,
                        GR.Status,
                        GR.ResponseText,
                        GR.ResponseDate
                    FROM Guest_Reviews GR
                    LEFT JOIN Customer C ON C.MobilePhone = GR.Customer_MobilePhone
                    WHERE 1=1");

                var parameters = new Dictionary<string, object>();

                // Filter by status
                if (!string.IsNullOrEmpty(ddlStatusFilter.SelectedValue))
                {
                    query.Append(" AND GR.Status = @Status");
                    parameters["@Status"] = ddlStatusFilter.SelectedValue;
                }

                // Filter by rating
                if (ddlRatingFilter.SelectedValue != "0")
                {
                    int rating = Convert.ToInt32(ddlRatingFilter.SelectedValue);
                    query.Append(" AND GR.Overall_Rating >= @MinRating AND GR.Overall_Rating < @MaxRating");
                    parameters["@MinRating"] = rating;
                    parameters["@MaxRating"] = rating + 1;
                }

                // Search by customer
                if (!string.IsNullOrEmpty(txtSearchCustomer.Text))
                {
                    query.Append(" AND (C.Name LIKE @Search OR GR.Customer_MobilePhone LIKE @Search)");
                    parameters["@Search"] = "%" + txtSearchCustomer.Text + "%";
                }

                query.Append(" ORDER BY GR.SubmittedDate DESC");

                DataTable dtReviews = _code.DatabaseQuerySafe(_connectionString, query.ToString(), parameters);

                if (dtReviews.Rows.Count > 0)
                {
                    rptReviews.DataSource = dtReviews;
                    rptReviews.DataBind();
                    lblNoReviews.Visible = false;
                }
                else
                {
                    rptReviews.DataSource = null;
                    rptReviews.DataBind();
                    lblNoReviews.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading reviews: " + ex.Message, "error");
            }
        }

        protected void ddlStatusFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadReviews();
        }

        protected void ddlRatingFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadReviews();
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            LoadReviews();
        }

        protected void rptReviews_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            long reviewId = Convert.ToInt64(e.CommandArgument);
            short adminId = Session["UserID"] != null ? Convert.ToInt16(Session["UserID"]) : (short)1;

            if (e.CommandName == "Approve")
            {
                bool success = _reviewService.ApproveReview(reviewId, adminId);
                if (success)
                {
                    ShowAlert("Review approved successfully!", "success");
                    LoadStatistics();
                    LoadReviews();
                }
                else
                {
                    ShowAlert("Failed to approve review", "error");
                }
            }
            else if (e.CommandName == "Reject")
            {
                bool success = _reviewService.RejectReview(reviewId, adminId);
                if (success)
                {
                    ShowAlert("Review rejected", "success");
                    LoadStatistics();
                    LoadReviews();
                }
                else
                {
                    ShowAlert("Failed to reject review", "error");
                }
            }
            else if (e.CommandName == "Respond")
            {
                // Redirect to response page or show modal
                Response.Redirect($"ReviewResponse.aspx?reviewId={reviewId}");
            }
        }

        protected string GetStars(object rating)
        {
            if (rating == DBNull.Value) return "";

            decimal ratingValue = Convert.ToDecimal(rating);
            int fullStars = (int)Math.Floor(ratingValue);
            bool hasHalfStar = (ratingValue - fullStars) >= 0.5m;

            StringBuilder stars = new StringBuilder();
            for (int i = 0; i < fullStars; i++)
            {
                stars.Append("<i class='fa fa-star' style='color: #f39c12;'></i>");
            }
            if (hasHalfStar)
            {
                stars.Append("<i class='fa fa-star-half-o' style='color: #f39c12;'></i>");
            }

            return stars.ToString();
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
    }
}
