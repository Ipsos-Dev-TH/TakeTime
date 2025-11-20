using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Account
{
    public partial class SlipVerification : Page
    {
        private code codeInstance = new code();
        private string conn = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            // Check authentication
            if (Session["Name"] == null)
            {
                Response.Redirect("~/Login.aspx");
                return;
            }

            if (!IsPostBack)
            {
                // Set default dates
                txtStartDate.Text = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                txtEndDate.Text = DateTime.Now.ToString("yyyy-MM-dd");

                LoadData();
            }
        }

        /// <summary>
        /// Load slips data and summary
        /// </summary>
        private void LoadData()
        {
            try
            {
                // Get filter values
                string ocrStatus = ddlOCRStatus.SelectedValue;
                string verificationStatus = ddlVerificationStatus.SelectedValue;
                DateTime? startDate = string.IsNullOrEmpty(txtStartDate.Text) ? (DateTime?)null : DateTime.Parse(txtStartDate.Text);
                DateTime? endDate = string.IsNullOrEmpty(txtEndDate.Text) ? (DateTime?)null : DateTime.Parse(txtEndDate.Text);

                // Load slips
                DataTable dtSlips = GetSlips(ocrStatus, verificationStatus, startDate, endDate);
                gvSlips.DataSource = dtSlips;
                gvSlips.DataBind();

                // Load summary
                LoadSummary();
            }
            catch (Exception ex)
            {
                lblMessage.Text = "เกิดข้อผิดพลาด: " + ex.Message;
                lblMessage.ForeColor = System.Drawing.Color.Red;
                codeInstance.Logs(conn, "SlipVerification LoadData Error", ex.Message, "SYSTEM");
            }
        }

        /// <summary>
        /// Get slips with filters
        /// </summary>
        private DataTable GetSlips(string ocrStatus, string verificationStatus, DateTime? startDate, DateTime? endDate)
        {
            string query = @"
                SELECT
                    ps.ID AS SlipID,
                    ps.Reservation_ID AS ReservationID,
                    ps.SlipFileURL,
                    ps.UploadedDate,
                    ps.OCR_Amount,
                    ps.OCR_Status,
                    ps.OCR_Confidence,
                    ps.OCR_ErrorMessage,
                    ps.VerificationStatus,
                    ps.VerifiedDate,
                    ps.RejectionReason,
                    c.FullName AS CustomerName,
                    c.MobilePhone AS CustomerPhone,
                    r.Total AS PaymentAmount,
                    a.Username AS VerifiedByName,
                    uploader.Username AS UploadedByName
                FROM Payment_Slips ps
                LEFT JOIN Reservation r ON ps.Reservation_ID = r.ID
                LEFT JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                LEFT JOIN Admin a ON ps.VerifiedBy_ID = a.ID
                LEFT JOIN Admin uploader ON ps.UploadedBy_ID = uploader.ID
                WHERE ps.IsActive = 1
                  AND ps.Status = 1
                  AND (@OCRStatus = '' OR ps.OCR_Status = @OCRStatus)
                  AND (@VerificationStatus = '' OR ps.VerificationStatus = @VerificationStatus)
                  AND (@StartDate IS NULL OR ps.UploadedDate >= @StartDate)
                  AND (@EndDate IS NULL OR ps.UploadedDate <= DATEADD(day, 1, @EndDate))
                ORDER BY
                    CASE
                        WHEN ps.VerificationStatus = 'PENDING' AND ps.OCR_Status = 'MANUAL_REVIEW' THEN 1
                        WHEN ps.VerificationStatus = 'PENDING' AND ps.OCR_Status = 'FAILED' THEN 2
                        WHEN ps.VerificationStatus = 'PENDING' THEN 3
                        ELSE 4
                    END,
                    ps.UploadedDate DESC";

            var parameters = new Dictionary<string, object>
            {
                { "@OCRStatus", ocrStatus ?? "" },
                { "@VerificationStatus", verificationStatus ?? "" },
                { "@StartDate", startDate.HasValue ? (object)startDate.Value : DBNull.Value },
                { "@EndDate", endDate.HasValue ? (object)endDate.Value : DBNull.Value }
            };

            return codeInstance.DatabaseQuerySafe(conn, query, parameters);
        }

        /// <summary>
        /// Load summary counts
        /// </summary>
        private void LoadSummary()
        {
            string query = @"
                SELECT
                    COUNT(CASE WHEN VerificationStatus = 'PENDING' THEN 1 END) AS PendingCount,
                    COUNT(CASE WHEN OCR_Status = 'SUCCESS' THEN 1 END) AS SuccessCount,
                    COUNT(CASE WHEN OCR_Status = 'FAILED' THEN 1 END) AS FailedCount,
                    COUNT(CASE WHEN OCR_Status = 'MANUAL_REVIEW' THEN 1 END) AS ManualReviewCount
                FROM Payment_Slips
                WHERE IsActive = 1 AND Status = 1";

            DataTable dtSummary = codeInstance.DatabaseQuerySafe(conn, query, null);

            if (dtSummary.Rows.Count > 0)
            {
                DataRow row = dtSummary.Rows[0];
                lblPendingCount.Text = row["PendingCount"].ToString();
                lblSuccessCount.Text = row["SuccessCount"].ToString();
                lblFailedCount.Text = row["FailedCount"].ToString();
                lblManualReviewCount.Text = row["ManualReviewCount"].ToString();
            }
        }

        /// <summary>
        /// Apply filter button click
        /// </summary>
        protected void ApplyFilter(object sender, EventArgs e)
        {
            LoadData();
        }

        /// <summary>
        /// GridView row command (Approve/Reject)
        /// </summary>
        protected void gvSlips_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            try
            {
                long slipId = Convert.ToInt64(e.CommandArgument);

                if (e.CommandName == "ApproveSlip")
                {
                    ApproveSlip(slipId);
                    lblMessage.Text = "อนุมัติสลิปสำเร็จ";
                    lblMessage.ForeColor = System.Drawing.Color.Green;
                }
                else if (e.CommandName == "RejectSlip")
                {
                    // For now, simple rejection. Can enhance with modal dialog for reason
                    RejectSlip(slipId, "ปฏิเสธโดย Admin");
                    lblMessage.Text = "ปฏิเสธสลิปสำเร็จ";
                    lblMessage.ForeColor = System.Drawing.Color.Orange;
                }

                // Reload data
                LoadData();
            }
            catch (Exception ex)
            {
                lblMessage.Text = "เกิดข้อผิดพลาด: " + ex.Message;
                lblMessage.ForeColor = System.Drawing.Color.Red;
                codeInstance.Logs(conn, "SlipVerification RowCommand Error", ex.Message, "SYSTEM");
            }
        }

        /// <summary>
        /// Approve slip
        /// </summary>
        private void ApproveSlip(long slipId)
        {
            int adminId = Session["ID"] != null ? Convert.ToInt32(Session["ID"]) : 0;

            string query = @"
                UPDATE Payment_Slips
                SET VerificationStatus = 'APPROVED',
                    IsVerified = 1,
                    VerifiedBy_ID = @AdminId,
                    VerifiedDate = GETDATE()
                WHERE ID = @SlipId";

            var parameters = new Dictionary<string, object>
            {
                { "@SlipId", slipId },
                { "@AdminId", adminId }
            };

            codeInstance.DatabaseInsertSafe(conn, query, parameters);

            // Log action
            codeInstance.Logs(conn, "Slip Approved",
                $"SlipID: {slipId}, AdminID: {adminId}",
                Session["Name"]?.ToString() ?? "SYSTEM");
        }

        /// <summary>
        /// Reject slip
        /// </summary>
        private void RejectSlip(long slipId, string reason)
        {
            int adminId = Session["ID"] != null ? Convert.ToInt32(Session["ID"]) : 0;

            string query = @"
                UPDATE Payment_Slips
                SET VerificationStatus = 'REJECTED',
                    IsVerified = 1,
                    VerifiedBy_ID = @AdminId,
                    VerifiedDate = GETDATE(),
                    RejectionReason = @Reason
                WHERE ID = @SlipId";

            var parameters = new Dictionary<string, object>
            {
                { "@SlipId", slipId },
                { "@AdminId", adminId },
                { "@Reason", reason }
            };

            codeInstance.DatabaseInsertSafe(conn, query, parameters);

            // Log action
            codeInstance.Logs(conn, "Slip Rejected",
                $"SlipID: {slipId}, AdminID: {adminId}, Reason: {reason}",
                Session["Name"]?.ToString() ?? "SYSTEM");
        }

        /// <summary>
        /// Get OCR status text in Thai
        /// </summary>
        protected string GetOCRStatusText(string status)
        {
            switch (status)
            {
                case "PENDING": return "รอดำเนินการ";
                case "SUCCESS": return "สำเร็จ";
                case "FAILED": return "ล้มเหลว";
                case "MANUAL_REVIEW": return "ต้องตรวจสอบ";
                case "SKIPPED": return "ข้าม";
                default: return status;
            }
        }

        /// <summary>
        /// Get verification status text in Thai
        /// </summary>
        protected string GetVerificationStatusText(string status)
        {
            switch (status)
            {
                case "PENDING": return "รอตรวจสอบ";
                case "APPROVED": return "อนุมัติแล้ว";
                case "REJECTED": return "ปฏิเสธแล้ว";
                default: return status;
            }
        }
    }
}
