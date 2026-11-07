using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Voucher
{
    public partial class VoucherList : System.Web.UI.Page
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private const int PageSize = 50;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SalesVoucher)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (!IsPostBack)
            {
                // Check permission
                if (Session["permission"]?.ToString() != "True")
                {
                    Response.Redirect("~/Admin/Login", true);
                    return;
                }

                // Don't set default date filter - show all vouchers to match stats
                // User can filter by date if needed
                txtDateFrom.Text = "";
                txtDateTo.Text = "";

                // Initialize
                ViewState["CurrentPage"] = 1;
                ViewState["FilterStatus"] = "";

                // Load data
                LoadStats();
                LoadVouchers();
            }
        }

        #region Stats Loading

        private void LoadStats()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Total vouchers
                    lblStatAll.Text = GetStatCount(conn, "all").ToString();

                    // Active vouchers (not used, not expired)
                    lblStatActive.Text = GetStatCount(conn, "active").ToString();

                    // Used vouchers
                    lblStatUsed.Text = GetStatCount(conn, "used").ToString();

                    // Expired vouchers
                    lblStatExpired.Text = GetStatCount(conn, "expired").ToString();

                    // Cancelled vouchers
                    lblStatCancelled.Text = GetStatCount(conn, "cancelled").ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadStats Error: " + ex.Message);
            }
        }

        private int GetStatCount(SqlConnection conn, string status)
        {
            string query = "SELECT COUNT(*) FROM Voucher WHERE 1=1";

            switch (status)
            {
                case "active":
                    query += " AND (Used_Status IS NULL OR CAST(Used_Status AS VARCHAR(10)) IN ('0','False')) AND (Expired_Date IS NULL OR Expired_Date >= GETDATE())";
                    break;
                case "used":
                    query += " AND CAST(Used_Status AS VARCHAR(10)) IN ('1','True')";
                    break;
                case "expired":
                    query += " AND (Used_Status IS NULL OR CAST(Used_Status AS VARCHAR(10)) IN ('0','False')) AND Expired_Date < GETDATE()";
                    break;
                case "cancelled":
                    query += " AND CAST(Used_Status AS VARCHAR(10)) = '-1'";
                    break;
            }

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                return (int)cmd.ExecuteScalar();
            }
        }

        #endregion

        #region Data Loading

        private void LoadVouchers()
        {
            try
            {
                int currentPage = ViewState["CurrentPage"] != null ? (int)ViewState["CurrentPage"] : 1;

                DataTable dt = GetVouchersData(currentPage, out int totalRecords, out decimal totalSales);

                // Bind data
                gvVouchers.DataSource = dt;
                gvVouchers.DataBind();

                // Update summary
                lblResultCount.Text = totalRecords.ToString("N0");
                lblTotalSales.Text = totalSales.ToString("N0");

                // Update pagination
                int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalRecords / PageSize));
                lblCurrentPage.Text = currentPage.ToString();
                lblTotalPages.Text = totalPages.ToString();

                btnPrevPage.Enabled = currentPage > 1;
                btnNextPage.Enabled = currentPage < totalPages;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadVouchers Error: " + ex.Message);
            }
        }

        private DataTable GetVouchersData(int page, out int totalRecords, out decimal totalSales)
        {
            totalRecords = 0;
            totalSales = 0;

            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Build WHERE clause
                StringBuilder whereClause = new StringBuilder("WHERE 1=1");
                List<SqlParameter> parameters = new List<SqlParameter>();

                // Search filter
                string search = txtSearch.Text.Trim();
                if (!string.IsNullOrEmpty(search))
                {
                    whereClause.Append(@" AND (V.Voucher_Number LIKE @Search
                                          OR ISNULL(C.Name, '') LIKE @Search
                                          OR ISNULL(C.MobilePhone, '') LIKE @Search)");
                    parameters.Add(new SqlParameter("@Search", "%" + search + "%"));
                }

                // Status filter
                string status = ddlStatus.SelectedValue;
                string filterStatus = ViewState["FilterStatus"]?.ToString() ?? "";

                if (!string.IsNullOrEmpty(filterStatus))
                {
                    status = filterStatus;
                }

                if (!string.IsNullOrEmpty(status))
                {
                    switch (status)
                    {
                        case "active":
                            whereClause.Append(" AND (V.Used_Status IS NULL OR CAST(V.Used_Status AS VARCHAR(10)) IN ('0','False')) AND (V.Expired_Date IS NULL OR V.Expired_Date >= GETDATE())");
                            break;
                        case "used":
                            whereClause.Append(" AND CAST(V.Used_Status AS VARCHAR(10)) IN ('1','True')");
                            break;
                        case "expired":
                            whereClause.Append(" AND (V.Used_Status IS NULL OR CAST(V.Used_Status AS VARCHAR(10)) IN ('0','False')) AND V.Expired_Date < GETDATE()");
                            break;
                        case "cancelled":
                            whereClause.Append(" AND CAST(V.Used_Status AS VARCHAR(10)) = '-1'");
                            break;
                    }
                }

                // Date range filter (use ParseExact for reliable parsing)
                DateTime dateFrom, dateTo;
                if (!string.IsNullOrEmpty(txtDateFrom.Text) && DateTime.TryParseExact(txtDateFrom.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateFrom))
                {
                    whereClause.Append(" AND V.Created_Date >= @DateFrom");
                    parameters.Add(new SqlParameter("@DateFrom", SqlDbType.DateTime) { Value = dateFrom });
                }

                if (!string.IsNullOrEmpty(txtDateTo.Text) && DateTime.TryParseExact(txtDateTo.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateTo))
                {
                    whereClause.Append(" AND V.Created_Date <= @DateTo");
                    parameters.Add(new SqlParameter("@DateTo", SqlDbType.DateTime) { Value = dateTo.AddDays(1).AddSeconds(-1) });
                }

                // Count total records and calculate totals
                string countQuery = $@"SELECT COUNT(*), ISNULL(SUM(V.Sell_Price), 0)
                                      FROM Voucher V
                                      LEFT JOIN Customer C ON C.ID = V.Customer_ID
                                      {whereClause}";

                using (SqlCommand cmdCount = new SqlCommand(countQuery, conn))
                {
                    cmdCount.Parameters.AddRange(parameters.ToArray());
                    using (SqlDataReader reader = cmdCount.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Use Convert to handle different SQL numeric types safely
                            totalRecords = Convert.ToInt32(reader[0]);
                            totalSales = reader[1] != DBNull.Value ? Convert.ToDecimal(reader[1]) : 0;
                        }
                    }
                }

                // Get paginated data
                int offset = (page - 1) * PageSize;

                string dataQuery = $@"
                    SELECT V.Voucher_Number, V.Created_Date, V.Sell_Price, V.Expired_Date,
                           V.Used_Status, V.Remark, V.Customer_ID, V.Receipt_ID,
                           ISNULL(C.Name, 'ไม่ระบุ') as CustomerName,
                           ISNULL(C.MobilePhone, '-') as CustomerPhone
                    FROM Voucher V
                    LEFT JOIN Customer C ON C.ID = V.Customer_ID
                    {whereClause}
                    ORDER BY V.Created_Date DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                // Re-create parameters for data query
                List<SqlParameter> dataParams = new List<SqlParameter>();
                if (!string.IsNullOrEmpty(search))
                {
                    dataParams.Add(new SqlParameter("@Search", "%" + search + "%"));
                }
                if (!string.IsNullOrEmpty(txtDateFrom.Text) && DateTime.TryParseExact(txtDateFrom.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateFrom))
                {
                    dataParams.Add(new SqlParameter("@DateFrom", SqlDbType.DateTime) { Value = dateFrom });
                }
                if (!string.IsNullOrEmpty(txtDateTo.Text) && DateTime.TryParseExact(txtDateTo.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateTo))
                {
                    dataParams.Add(new SqlParameter("@DateTo", SqlDbType.DateTime) { Value = dateTo.AddDays(1).AddSeconds(-1) });
                }
                dataParams.Add(new SqlParameter("@Offset", offset));
                dataParams.Add(new SqlParameter("@PageSize", PageSize));

                using (SqlCommand cmd = new SqlCommand(dataQuery, conn))
                {
                    cmd.Parameters.AddRange(dataParams.ToArray());

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                }
            }

            return dt;
        }

        #endregion

        #region Event Handlers

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            ViewState["CurrentPage"] = 1;
            ViewState["FilterStatus"] = "";
            LoadVouchers();
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            ddlStatus.SelectedIndex = 0;
            txtDateFrom.Text = "";
            txtDateTo.Text = "";

            ViewState["CurrentPage"] = 1;
            ViewState["FilterStatus"] = "";

            LoadVouchers();
        }

        protected void StatCard_Click(object sender, EventArgs e)
        {
            LinkButton btn = (LinkButton)sender;
            string filter = btn.CommandArgument;

            ViewState["CurrentPage"] = 1;

            switch (filter)
            {
                case "all":
                    ViewState["FilterStatus"] = "";
                    ddlStatus.SelectedIndex = 0;
                    break;
                case "active":
                    ViewState["FilterStatus"] = "active";
                    ddlStatus.SelectedValue = "active";
                    break;
                case "used":
                    ViewState["FilterStatus"] = "used";
                    ddlStatus.SelectedValue = "used";
                    break;
                case "expired":
                    ViewState["FilterStatus"] = "expired";
                    ddlStatus.SelectedValue = "expired";
                    break;
                case "cancelled":
                    ViewState["FilterStatus"] = "cancelled";
                    ddlStatus.SelectedValue = "cancelled";
                    break;
            }

            LoadVouchers();
        }

        protected void btnPrevPage_Click(object sender, EventArgs e)
        {
            int currentPage = (int)ViewState["CurrentPage"];
            if (currentPage > 1)
            {
                ViewState["CurrentPage"] = currentPage - 1;
                LoadVouchers();
            }
        }

        protected void btnNextPage_Click(object sender, EventArgs e)
        {
            int currentPage = (int)ViewState["CurrentPage"];
            int totalPages = int.Parse(lblTotalPages.Text);
            if (currentPage < totalPages)
            {
                ViewState["CurrentPage"] = currentPage + 1;
                LoadVouchers();
            }
        }

        protected void btnExport_Click(object sender, EventArgs e)
        {
            try
            {
                DataTable dt = GetAllVouchersForExport();

                if (dt.Rows.Count == 0)
                {
                    return;
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("เลข Voucher,วันที่ขาย,ชื่อลูกค้า,เบอร์โทร,ราคา,วันหมดอายุ,สถานะ,หมายเหตุ");

                foreach (DataRow row in dt.Rows)
                {
                    string status = GetStatusText(row["Used_Status"], row["Expired_Date"]);

                    sb.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7}",
                        row["Voucher_Number"],
                        Convert.ToDateTime(row["Created_Date"]).ToString("dd/MM/yyyy HH:mm"),
                        EscapeCsvField(row["CustomerName"]?.ToString()),
                        row["CustomerPhone"],
                        Convert.ToDecimal(row["Sell_Price"]).ToString("N0"),
                        row["Expired_Date"] != DBNull.Value ? Convert.ToDateTime(row["Expired_Date"]).ToString("dd/MM/yyyy") : "-",
                        status,
                        EscapeCsvField(row["Remark"]?.ToString())
                    ));
                }

                string fileName = $"Vouchers_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                Response.Clear();
                Response.ContentType = "text/csv";
                Response.ContentEncoding = Encoding.UTF8;
                Response.AddHeader("Content-Disposition", $"attachment; filename={fileName}");
                Response.BinaryWrite(Encoding.UTF8.GetPreamble());
                Response.Write(sb.ToString());
                Response.End();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Export Error: " + ex.Message);
            }
        }

        private DataTable GetAllVouchersForExport()
        {
            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                StringBuilder whereClause = new StringBuilder("WHERE 1=1");
                List<SqlParameter> parameters = new List<SqlParameter>();

                string search = txtSearch.Text.Trim();
                if (!string.IsNullOrEmpty(search))
                {
                    whereClause.Append(@" AND (V.Voucher_Number LIKE @Search
                                          OR ISNULL(C.Name, '') LIKE @Search
                                          OR ISNULL(C.MobilePhone, '') LIKE @Search)");
                    parameters.Add(new SqlParameter("@Search", "%" + search + "%"));
                }

                string status = ddlStatus.SelectedValue;
                if (!string.IsNullOrEmpty(status))
                {
                    switch (status)
                    {
                        case "active":
                            whereClause.Append(" AND (V.Used_Status IS NULL OR CAST(V.Used_Status AS VARCHAR(10)) IN ('0','False')) AND (V.Expired_Date IS NULL OR V.Expired_Date >= GETDATE())");
                            break;
                        case "used":
                            whereClause.Append(" AND CAST(V.Used_Status AS VARCHAR(10)) IN ('1','True')");
                            break;
                        case "expired":
                            whereClause.Append(" AND (V.Used_Status IS NULL OR CAST(V.Used_Status AS VARCHAR(10)) IN ('0','False')) AND V.Expired_Date < GETDATE()");
                            break;
                        case "cancelled":
                            whereClause.Append(" AND CAST(V.Used_Status AS VARCHAR(10)) = '-1'");
                            break;
                    }
                }

                DateTime dateFrom, dateTo;
                if (!string.IsNullOrEmpty(txtDateFrom.Text) && DateTime.TryParseExact(txtDateFrom.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateFrom))
                {
                    whereClause.Append(" AND V.Created_Date >= @DateFrom");
                    parameters.Add(new SqlParameter("@DateFrom", SqlDbType.DateTime) { Value = dateFrom });
                }

                if (!string.IsNullOrEmpty(txtDateTo.Text) && DateTime.TryParseExact(txtDateTo.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateTo))
                {
                    whereClause.Append(" AND V.Created_Date <= @DateTo");
                    parameters.Add(new SqlParameter("@DateTo", SqlDbType.DateTime) { Value = dateTo.AddDays(1).AddSeconds(-1) });
                }

                string query = $@"
                    SELECT V.Voucher_Number, V.Created_Date, V.Sell_Price, V.Expired_Date,
                           V.Used_Status, V.Remark,
                           ISNULL(C.Name, 'ไม่ระบุ') as CustomerName,
                           ISNULL(C.MobilePhone, '-') as CustomerPhone
                    FROM Voucher V
                    LEFT JOIN Customer C ON C.ID = V.Customer_ID
                    {whereClause}
                    ORDER BY V.Created_Date DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddRange(parameters.ToArray());
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                }
            }

            return dt;
        }

        #endregion

        #region GridView Events

        protected void gvVouchers_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                DataRowView drv = (DataRowView)e.Row.DataItem;

                // Set status badge
                Label lblStatus = (Label)e.Row.FindControl("lblStatus");
                if (lblStatus != null)
                {
                    object usedStatus = drv["Used_Status"];
                    object expiredDate = drv["Expired_Date"];

                    // Handle both string ('True'/'False') and int (0/1/-1) Used_Status values
                    string statusStr = usedStatus != DBNull.Value ? usedStatus.ToString().Trim() : "";
                    bool isUsed = statusStr == "1" || statusStr.Equals("True", StringComparison.OrdinalIgnoreCase);
                    bool isCancelled = statusStr == "-1";

                    if (isUsed)
                    {
                        lblStatus.Text = "ใช้แล้ว";
                        lblStatus.CssClass = "status-badge status-used";
                    }
                    else if (isCancelled)
                    {
                        lblStatus.Text = "ยกเลิก";
                        lblStatus.CssClass = "status-badge status-cancelled";
                    }
                    else if (expiredDate != DBNull.Value && Convert.ToDateTime(expiredDate) < DateTime.Now)
                    {
                        lblStatus.Text = "หมดอายุ";
                        lblStatus.CssClass = "status-badge status-expired";
                    }
                    else
                    {
                        lblStatus.Text = "ใช้งานได้";
                        lblStatus.CssClass = "status-badge status-active";
                    }
                }

                // Load rate plan
                Literal litRatePlan = (Literal)e.Row.FindControl("litRatePlan");
                if (litRatePlan != null)
                {
                    string voucherNumber = drv["Voucher_Number"]?.ToString();
                    litRatePlan.Text = GetRatePlanForVoucher(voucherNumber);
                }

                // Load used reservation with check-in date
                Literal litUsedReservation = (Literal)e.Row.FindControl("litUsedReservation");
                if (litUsedReservation != null)
                {
                    string voucherNumber = drv["Voucher_Number"]?.ToString();
                    var reservationInfo = GetUsedReservationWithCheckin(voucherNumber);
                    if (reservationInfo != null)
                    {
                        string checkinText = reservationInfo.Item2.HasValue
                            ? $"<br/><small style='color:#666;'>เข้าพัก: {reservationInfo.Item2.Value:dd/MM/yyyy}</small>"
                            : "";
                        litUsedReservation.Text = $"<a href='../Reserve.aspx?id={reservationInfo.Item1}' target='_blank'>#{reservationInfo.Item1}</a>{checkinText}";
                    }
                    else
                    {
                        litUsedReservation.Text = "-";
                    }
                }
            }
        }

        private string GetRatePlanForVoucher(string voucherNumber)
        {
            if (string.IsNullOrEmpty(voucherNumber)) return "-";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT DISTINCT G.Group_Name
                                    FROM Voucher_RatePlan_Group VR
                                    INNER JOIN Accommodation_RatePlan_Group G ON G.GroupID = VR.RatePlan_GroupID
                                    WHERE VR.Voucher_Number = @VoucherNumber";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@VoucherNumber", voucherNumber);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            List<string> plans = new List<string>();
                            while (reader.Read())
                            {
                                plans.Add(reader.GetString(0));
                            }
                            return plans.Count > 0 ? string.Join(", ", plans) : "-";
                        }
                    }
                }
            }
            catch
            {
                return "-";
            }
        }

        private Tuple<string, DateTime?> GetUsedReservationWithCheckin(string voucherNumber)
        {
            if (string.IsNullOrEmpty(voucherNumber)) return null;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Voucher table stores Reservation_ID when a voucher is used
                    string query = @"SELECT TOP 1 R.ID, R.CheckinDate
                                     FROM Voucher V
                                     INNER JOIN Reservation R ON R.ID = V.Reservation_ID
                                     WHERE V.Voucher_Number = @VoucherNumber
                                     AND V.Reservation_ID IS NOT NULL";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@VoucherNumber", voucherNumber);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string resId = reader["ID"]?.ToString();
                                DateTime? checkinDate = reader["CheckinDate"] != DBNull.Value
                                    ? (DateTime?)Convert.ToDateTime(reader["CheckinDate"])
                                    : null;
                                return Tuple.Create(resId, checkinDate);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }

        #endregion

        #region Helper Methods

        protected string FormatExpiryDate(object date)
        {
            if (date == null || date == DBNull.Value) return "<span style='color:#999;'>ไม่ระบุ</span>";

            DateTime dt = Convert.ToDateTime(date);
            DateTime today = DateTime.Today;

            if (dt < today)
            {
                return $"<span class='expired-text'>{dt:dd/MM/yyyy}</span>";
            }
            else if (dt <= today.AddDays(30))
            {
                return $"<span class='expired-soon'>{dt:dd/MM/yyyy}</span>";
            }

            return dt.ToString("dd/MM/yyyy");
        }

        private string GetStatusText(object usedStatus, object expiredDate)
        {
            // Handle both string ('True'/'False') and int (0/1/-1) Used_Status values
            string statusStr = usedStatus != DBNull.Value ? usedStatus.ToString().Trim() : "";
            bool isUsed = statusStr == "1" || statusStr.Equals("True", StringComparison.OrdinalIgnoreCase);
            bool isCancelled = statusStr == "-1";

            if (isUsed)
            {
                return "ใช้แล้ว";
            }
            else if (isCancelled)
            {
                return "ยกเลิก";
            }
            else if (expiredDate != DBNull.Value && Convert.ToDateTime(expiredDate) < DateTime.Now)
            {
                return "หมดอายุ";
            }
            return "ใช้งานได้";
        }

        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";

            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }

        #endregion
    }
}
