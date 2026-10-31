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
            if (!Perm.Guard(this, Perm.FinReceipt)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            // Check authentication
            // เดิมเช็ค Session["Name"] ซึ่งไม่มีที่ไหนตั้งค่าเลย และ redirect ไป ~/Login.aspx
            // ที่ไม่มีอยู่จริง → เข้าหน้านี้ไม่ได้ตลอด. ใช้เกณฑ์เดียวกับทั้งระบบ
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
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
            // สลิปที่ลูกค้าแนบเอง: แสดงยอดที่ลูกค้าแจ้ง (Claimed_Amount, PHASE19_25) — ยอดที่จะลงรับเงินเมื่ออนุมัติ
            bool hasClaim = new PaymentService(conn).HasSlipClaimColumn();
            // ผลตรวจสลิป (PHASE19_34) — ยังไม่รัน migration = คอลัมน์ว่าง
            bool hasVerify = Payments.SlipCheck.SlipVerificationService.SchemaReady;
            string verifyCols = hasVerify
                ? "ps.Slip_Amount, ps.Slip_TransRef, ps.Verify_Status, ps.Verify_Source, ps.Verify_Message, ps.Override_By, ps.Override_Reason,"
                : "CAST(NULL AS DECIMAL(18,2)) AS Slip_Amount, CAST(NULL AS NVARCHAR(40)) AS Slip_TransRef, CAST(NULL AS NVARCHAR(20)) AS Verify_Status, "
                  + "CAST(NULL AS NVARCHAR(20)) AS Verify_Source, CAST(NULL AS NVARCHAR(1000)) AS Verify_Message, "
                  + "CAST(NULL AS NVARCHAR(100)) AS Override_By, CAST(NULL AS NVARCHAR(500)) AS Override_Reason,";
            // ยอดล็อกตามสลิป + ลูกค้ากรอกยอดเอง (PHASE19_36) — ยังไม่รัน migration = คอลัมน์ว่าง
            verifyCols += Payments.SlipCheck.SlipVerificationService.AmountLockSchemaReady
                ? " ps.Amount_Locked, ps.Lock_Confidence, ps.Lock_Source,"
                : " CAST(NULL AS BIT) AS Amount_Locked, CAST(NULL AS INT) AS Lock_Confidence, CAST(NULL AS NVARCHAR(20)) AS Lock_Source,";
            verifyCols += Payments.SlipCheck.SlipVerificationService.ManualAmountSchemaReady
                ? " ps.Customer_Manual_Amount,"
                : " CAST(NULL AS BIT) AS Customer_Manual_Amount,";
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
                    " + verifyCols + @"
                    c.FullName AS CustomerName,
                    c.MobilePhone AS CustomerPhone,
                    " + (hasClaim ? "COALESCE(ps.Claimed_Amount, r.Total)" : "r.Total") + @" AS PaymentAmount,
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

                if (e.CommandName == "RecheckSlip")
                {
                    // ตรวจใหม่ (ข้าม cache) เทียบยอดที่ลูกค้าแจ้ง/ยอดของรายการ — บันทึกผลลงแถว ไม่อนุมัติ
                    var r = Payments.SlipCheck.SlipVerificationService.VerifySlipRow(slipId, LoadExpectedAmount(slipId), true, true);
                    lblMessage.Text = r == null ? "ไม่พบสลิป" : "ผลตรวจใหม่: " + r.ShortText() + (string.IsNullOrEmpty(r.Message) ? "" : " — " + r.Message);
                    lblMessage.ForeColor = r != null && !r.IsBlocking ? System.Drawing.Color.Green : System.Drawing.Color.Red;
                }
                else if (e.CommandName == "ApproveSlipAmount")
                {
                    // อนุมัติตามยอดในสลิป (แทนยอดที่ลูกค้าแจ้ง)
                    decimal? slipAmt = LoadSlipAmount(slipId);
                    string err = ApproveSlip(slipId, out string okMsg, slipAmt);
                    lblMessage.Text = err ?? okMsg ?? "อนุมัติสลิปสำเร็จ";
                    lblMessage.ForeColor = err == null ? System.Drawing.Color.Green : System.Drawing.Color.Red;
                }
                else if (e.CommandName == "ApproveSlip")
                {
                    string err = ApproveSlip(slipId, out string okMsg);
                    lblMessage.Text = err ?? okMsg ?? "อนุมัติสลิปสำเร็จ";
                    lblMessage.ForeColor = err == null ? System.Drawing.Color.Green : System.Drawing.Color.Red;
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
        /// Approve slip — คืนข้อความ error (null = สำเร็จ)
        /// สลิปที่ลูกค้าแนบเองจากหน้า /Payment/Pay (มี Claimed_Amount) = อนุมัติแล้วลงรับเงินจริง (Payment_History / ใบเสร็จ /
        /// บัญชี) + ยืนยันการจอง ผ่าน PaymentService.ApproveReservationSlip · สลิปอื่น (ลงเงินไปแล้ว) = เปลี่ยนสถานะอย่างเดียวแบบเดิม
        /// </summary>
        private string ApproveSlip(long slipId, out string okMessage, decimal? amountOverride = null)
        {
            okMessage = null;
            int adminId = Session["ID"] != null ? Convert.ToInt32(Session["ID"]) : 0;

            // เหตุผลยืนยันสลิปที่ตรวจไม่ผ่าน (ยอดไม่ตรง/ผู้รับผิด) — ใช้ได้เฉพาะ Owner/Admin (สลิปซ้ำยืนยันไม่ได้)
            string overrideReason = (txtOverrideReason.Text ?? "").Trim();
            if (overrideReason.Length == 0 || !Payments.SlipCheck.SlipVerificationService.CanOverrideCurrentUser()) overrideReason = null;

            PaymentResult pr = new PaymentService(conn).ApproveReservationSlip(slipId, adminId > 0 ? (int?)adminId : null,
                overrideReason, amountOverride);
            if (pr != null)
            {
                codeInstance.Logs(conn, pr.Success ? "Slip Approved" : "Slip Approve Failed",
                    $"SlipID: {slipId}, AdminID: {adminId}, {pr.Message}, Receipt: {pr.ReceiptId}",
                    Session["UserName"]?.ToString() ?? "SYSTEM");
                if (!pr.Success) return pr.Message;
                okMessage = "อนุมัติสลิปและลงรับเงินแล้ว"
                    + (string.IsNullOrEmpty(pr.ReceiptId) ? "" : " — ใบเสร็จ " + pr.ReceiptId);
                return null;
            }

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
            if (overrideReason != null)
                Payments.SlipCheck.SlipVerificationService.RecordOverride(slipId,
                    Payments.SlipCheck.SlipVerificationService.CurrentUserName(), overrideReason);
            Payments.SlipCheck.SlipVerificationService.ReleaseQueueForSlip(slipId);   // ปล่อยคิวบัญชีที่พักไว้ (AWAITING_SLIP)

            // Log action
            codeInstance.Logs(conn, "Slip Approved",
                $"SlipID: {slipId}, AdminID: {adminId}",
                Session["UserName"]?.ToString() ?? "SYSTEM");
            return null;
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
            Payments.SlipCheck.SlipVerificationService.MarkRejected(slipId);   // ปล่อยรหัสอ้างอิงสลิป (PHASE19_34)

            // Log action
            codeInstance.Logs(conn, "Slip Rejected",
                $"SlipID: {slipId}, AdminID: {adminId}, Reason: {reason}",
                Session["UserName"]?.ToString() ?? "SYSTEM");
        }

        /// <summary>ยอดที่ใช้เทียบตอนตรวจใหม่: ยอดที่ลูกค้าแจ้ง (Claimed_Amount) → ยอดใบเสร็จที่ผูก → ไม่มี</summary>
        private decimal? LoadExpectedAmount(long slipId)
        {
            try
            {
                bool hasClaim = new PaymentService(conn).HasSlipClaimColumn();
                DataTable dt = codeInstance.DatabaseQuerySafe(conn,
                    "SELECT " + (hasClaim ? "ps.Claimed_Amount" : "CAST(NULL AS DECIMAL(18,2))") + @" AS Claimed,
                            (SELECT TOP 1 ar.Total_Amount FROM Account_Receipt ar WHERE CAST(ar.ID AS NVARCHAR(50)) = CAST(ps.Account_Receipt_ID AS NVARCHAR(50))) AS RcptAmt
                       FROM Payment_Slips ps WHERE ps.ID = @id",
                    new Dictionary<string, object> { { "@id", slipId } });
                if (dt.Rows.Count == 0) return null;
                if (dt.Rows[0]["Claimed"] != DBNull.Value && Convert.ToDecimal(dt.Rows[0]["Claimed"]) > 0m) return Convert.ToDecimal(dt.Rows[0]["Claimed"]);
                if (dt.Rows[0]["RcptAmt"] != DBNull.Value) return Convert.ToDecimal(dt.Rows[0]["RcptAmt"]);
            }
            catch { }
            return null;
        }

        private decimal? LoadSlipAmount(long slipId)
        {
            var row = Payments.SlipCheck.SlipVerificationService.LoadSavedResult(slipId);
            return row != null && row["Slip_Amount"] != DBNull.Value ? Convert.ToDecimal(row["Slip_Amount"]) : (decimal?)null;
        }

        /// <summary>คอลัมน์ "ผลตรวจสลิป" ในตาราง</summary>
        protected string FormatVerify(object item)
        {
            try
            {
                var r = (DataRowView)item;
                string st = r["Verify_Status"] == DBNull.Value ? null : r["Verify_Status"].ToString();
                if (string.IsNullOrEmpty(st)) return "<span style='color:#999;'>ยังไม่ตรวจ</span>";
                string color = st == "VERIFIED" || st == "ALLOCATED" ? "#2e7d32"
                    : Payments.SlipCheck.SlipVerifyStatus.IsBlocking(st) ? "#c62828" : "#ef6c00";
                var sb = new System.Text.StringBuilder();
                sb.Append("<b style='color:").Append(color).Append("'>")
                  .Append(Server.HtmlEncode(Payments.SlipCheck.SlipVerifyStatus.Thai(st))).Append("</b>");
                if (r["Slip_Amount"] != DBNull.Value) sb.Append("<br/>สลิป ").Append(Convert.ToDecimal(r["Slip_Amount"]).ToString("N2"));
                if (IsAmountLocked(r))
                {
                    // 🔒 ยอดล็อกตามสลิป — อนุมัติแล้วลงรับเงินตามยอดสลิปเท่านั้น
                    bool bank = Convert.ToString(r["Lock_Source"]) == Payments.SlipCheck.SlipVerifySource.SlipOk;
                    sb.Append("<br/><span style='color:#1b5e20;font-weight:600'>🔒 ")
                      .Append(Server.HtmlEncode(bank ? "ยอดจากสลิป ✓ (SlipOK ธนาคารยืนยัน)"
                          : "ยอดจากสลิป (อ่านอัตโนมัติ " + Convert.ToString(r["Lock_Confidence"]) + "%)")).Append("</span>");
                }
                if (r.Row.Table.Columns.Contains("Customer_Manual_Amount") && r["Customer_Manual_Amount"] != DBNull.Value && Convert.ToBoolean(r["Customer_Manual_Amount"]))
                    sb.Append("<br/><span style='color:#e65100;font-weight:600'>✏️ ลูกค้ากรอกยอดเอง — ตรวจยอดกับสลิป/บัญชีธนาคาร</span>");
                sb.Append("<br/><small>").Append(Server.HtmlEncode(Payments.SlipCheck.SlipVerifySource.Thai(Convert.ToString(r["Verify_Source"])))).Append("</small>");
                if (r["Override_By"] != DBNull.Value)
                    sb.Append("<br/><small style='color:#6a1b9a'>ยืนยันโดย ").Append(Server.HtmlEncode(r["Override_By"].ToString()))
                      .Append(": ").Append(Server.HtmlEncode(Convert.ToString(r["Override_Reason"]))).Append("</small>");
                string msg = Convert.ToString(r["Verify_Message"]);
                if (!string.IsNullOrEmpty(msg))
                    sb.Append("<br/><small title='").Append(Server.HtmlEncode(msg)).Append("'>")
                      .Append(Server.HtmlEncode(msg.Length > 90 ? msg.Substring(0, 90) + "…" : msg)).Append("</small>");
                return sb.ToString();
            }
            catch { return "-"; }
        }

        protected bool SlipVerifyOn => Payments.SlipCheck.SlipVerificationService.IsEnabled;

        private static bool IsAmountLocked(DataRowView r)
        {
            try
            {
                return r.Row.Table.Columns.Contains("Amount_Locked") && r["Amount_Locked"] != DBNull.Value && Convert.ToBoolean(r["Amount_Locked"]);
            }
            catch { return false; }
        }

        /// <summary>แสดงปุ่ม "อนุมัติตามยอดสลิป" เมื่อยอดสลิปต่างจากยอดที่แจ้ง</summary>
        protected bool ShowApproveSlipAmount(object item)
        {
            try
            {
                var r = (DataRowView)item;
                if (r["VerificationStatus"].ToString() != "PENDING" || r["Slip_Amount"] == DBNull.Value || r["PaymentAmount"] == DBNull.Value) return false;
                if (IsAmountLocked(r)) return false;   // ยอดล็อกตามสลิป: ปุ่มอนุมัติปกติใช้ยอดสลิปอยู่แล้ว
                return Convert.ToString(r["Verify_Status"]) != "DUPLICATE"
                    && Math.Abs(Convert.ToDecimal(r["Slip_Amount"]) - Convert.ToDecimal(r["PaymentAmount"])) > 0.009m;
            }
            catch { return false; }
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
