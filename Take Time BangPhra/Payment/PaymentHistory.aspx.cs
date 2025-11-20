using System;
using System.Configuration;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Payment
{
    public partial class PaymentHistory : System.Web.UI.Page
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["ATATB"].ConnectionString;
        private PaymentDataAccess paymentDataAccess;
        private code codeInstance = new code();

        protected void Page_Load(object sender, EventArgs e)
        {
            paymentDataAccess = new PaymentDataAccess(connectionString);

            if (!IsPostBack)
            {
                // Check if reservationId is passed via QueryString
                string reservationIdParam = Request.QueryString["reservationId"];
                if (!string.IsNullOrEmpty(reservationIdParam))
                {
                    // Auto-fill the reservation ID textbox
                    txtReservationID.Text = reservationIdParam;
                }

                LoadPaymentHistory();
                LoadSummary();
            }
        }

        private void LoadPaymentHistory()
        {
            try
            {
                var parameters = new System.Collections.Generic.Dictionary<string, object>();
                string whereClause = "WHERE 1=1";

                // Filter by Reservation ID
                if (!string.IsNullOrEmpty(txtReservationID.Text))
                {
                    whereClause += " AND ph.Reservation_ID = @reservationId";
                    parameters.Add("@reservationId", int.Parse(txtReservationID.Text));
                }

                // Filter by Phone
                if (!string.IsNullOrEmpty(txtPhoneNumber.Text))
                {
                    whereClause += " AND ph.PaidBy_CustomerPhone LIKE @phone";
                    parameters.Add("@phone", "%" + txtPhoneNumber.Text + "%");
                }

                // Filter by Date Range
                if (!string.IsNullOrEmpty(txtStartDate.Text))
                {
                    whereClause += " AND CAST(ph.PaymentDate AS DATE) >= @startDate";
                    parameters.Add("@startDate", DateTime.Parse(txtStartDate.Text));
                }

                if (!string.IsNullOrEmpty(txtEndDate.Text))
                {
                    whereClause += " AND CAST(ph.PaymentDate AS DATE) <= @endDate";
                    parameters.Add("@endDate", DateTime.Parse(txtEndDate.Text));
                }

                // Filter by Status
                if (!string.IsNullOrEmpty(ddlStatus.SelectedValue))
                {
                    whereClause += " AND ph.Status = @status";
                    parameters.Add("@status", ddlStatus.SelectedValue);
                }

                string query = $@"
                    SELECT
                        ph.ID,
                        ph.Reservation_ID,
                        ph.PaymentDate,
                        ph.PaymentAmount,
                        ph.PaymentType,
                        ph.PaymentMethod,
                        ph.Receipt_ID,
                        ph.PaymentSlip_ID,
                        ph.RemainingBalance,
                        ph.PaidBy_CustomerPhone AS Customer_MobilePhone,
                        ph.Status
                    FROM Payment_History ph
                    {whereClause}
                    ORDER BY ph.PaymentDate DESC";

                DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, parameters);

                if (dt.Rows.Count > 0)
                {
                    gvPaymentHistory.DataSource = dt;
                    gvPaymentHistory.DataBind();
                    pnlEmpty.Visible = false;
                }
                else
                {
                    gvPaymentHistory.DataSource = null;
                    gvPaymentHistory.DataBind();
                    pnlEmpty.Visible = true;
                }
            }
            catch (Exception ex)
            {
                // Handle error - possibly Payment_History table doesn't exist yet
                pnlEmpty.Visible = true;
                gvPaymentHistory.DataSource = null;
                gvPaymentHistory.DataBind();
            }
        }

        private void LoadSummary()
        {
            try
            {
                var parameters = new System.Collections.Generic.Dictionary<string, object>();

                string query = @"
                    SELECT
                        COUNT(*) AS TotalRecords,
                        ISNULL(SUM(PaymentAmount), 0) AS TotalAmount,
                        SUM(CASE WHEN Status = 'PENDING' THEN 1 ELSE 0 END) AS PendingCount,
                        SUM(CASE WHEN Status = 'COMPLETED' THEN 1 ELSE 0 END) AS CompletedCount
                    FROM Payment_History";

                DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    lblTotalRecords.Text = row["TotalRecords"].ToString();
                    lblTotalAmount.Text = Convert.ToDecimal(row["TotalAmount"]).ToString("N2");
                    lblPendingRecords.Text = row["PendingCount"].ToString();
                    lblCompletedRecords.Text = row["CompletedCount"].ToString();
                }
            }
            catch
            {
                // If table doesn't exist, show zeros
                lblTotalRecords.Text = "0";
                lblTotalAmount.Text = "0.00";
                lblPendingRecords.Text = "0";
                lblCompletedRecords.Text = "0";
            }
        }

        protected void btnFilter_Click(object sender, EventArgs e)
        {
            LoadPaymentHistory();
            LoadSummary();
        }

        protected void btnExport_Click(object sender, EventArgs e)
        {
            // TODO: Implement Excel export
            // Can use EPPlus or ClosedXML library
        }

        protected void gvPaymentHistory_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ViewSlip")
            {
                if (long.TryParse(e.CommandArgument.ToString(), out long slipId))
                {
                    // Redirect to slip viewer
                    string slipUrl = GetPaymentSlipUrl(slipId);
                    if (!string.IsNullOrEmpty(slipUrl))
                    {
                        Response.Redirect(slipUrl);
                    }
                }
            }
            else if (e.CommandName == "ViewReceipt")
            {
                string receiptId = e.CommandArgument.ToString();
                // Redirect to receipt viewer or generate PDF
                Response.Redirect($"~/ViewReceipt.aspx?id={receiptId}");
            }
        }

        private string GetPaymentSlipUrl(long slipId)
        {
            try
            {
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@slipId", slipId }
                };

                string query = "SELECT SlipFileURL FROM Payment_Slips WHERE ID = @slipId";
                DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, parameters);

                if (dt.Rows.Count > 0)
                {
                    return dt.Rows[0]["SlipFileURL"].ToString();
                }
            }
            catch
            {
                // Handle error
            }

            return null;
        }

        protected string GetStatusClass(string status)
        {
            switch (status)
            {
                case "COMPLETED":
                    return "badge-success";
                case "PENDING":
                    return "badge-warning";
                case "CANCELLED":
                    return "badge-danger";
                default:
                    return "badge-info";
            }
        }

        protected string GetStatusText(string status)
        {
            switch (status)
            {
                case "COMPLETED":
                    return "สำเร็จ";
                case "PENDING":
                    return "รอดำเนินการ";
                case "CANCELLED":
                    return "ยกเลิก";
                case "REFUNDED":
                    return "คืนเงินแล้ว";
                default:
                    return status;
            }
        }
    }
}
