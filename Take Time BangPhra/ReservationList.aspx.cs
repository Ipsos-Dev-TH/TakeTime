using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra
{
    public partial class ReservationList : System.Web.UI.Page
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private const int PageSize = 50;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Check permission
                if (Session["permission"]?.ToString() != "True")
                {
                    Response.Redirect("~/Admin/Login", true);
                    return;
                }

                // Don't set default date filter initially - let data show to match stats
                // User can filter by date if needed
                txtDateFrom.Text = "";
                txtDateTo.Text = "";

                // Initialize
                ViewState["CurrentPage"] = 1;
                ViewState["FilterStatus"] = "";

                // Load stats and data
                LoadStats();
                LoadReservations();
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

                    // Total reservations (last 90 days)
                    lblStatAll.Text = GetStatCount(conn, "").ToString();

                    // Today's reservations
                    lblStatToday.Text = GetTodayCount(conn).ToString();

                    // By status
                    lblStatDeposit.Text = GetStatCount(conn, "มัดจำแล้ว").ToString();
                    lblStatCheckin.Text = GetStatCount(conn, "เช็คอินแล้ว").ToString();
                    lblStatCheckout.Text = GetStatCount(conn, "เช็คเอาท์แล้ว").ToString();
                    lblStatComplete.Text = GetStatCount(conn, "เสร็จสิ้น").ToString();
                    lblStatCancel.Text = GetCancelCount(conn).ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadStats Error: " + ex.Message);
            }
        }

        private int GetStatCount(SqlConnection conn, string status)
        {
            string query = @"SELECT COUNT(*) FROM Reservation
                            WHERE Created_Date >= DATEADD(DAY, -90, GETDATE())";

            if (!string.IsNullOrEmpty(status))
            {
                query += " AND Status = @Status";
            }

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                if (!string.IsNullOrEmpty(status))
                {
                    cmd.Parameters.AddWithValue("@Status", status);
                }
                return (int)cmd.ExecuteScalar();
            }
        }

        private int GetTodayCount(SqlConnection conn)
        {
            string query = @"SELECT COUNT(*) FROM Reservation
                            WHERE CAST(Created_Date AS DATE) = CAST(GETDATE() AS DATE)";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                return (int)cmd.ExecuteScalar();
            }
        }

        private int GetCancelCount(SqlConnection conn)
        {
            string query = @"SELECT COUNT(*) FROM Reservation
                            WHERE Created_Date >= DATEADD(DAY, -90, GETDATE())
                            AND Status IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'ลบจากการเลื่อนวันเข้าพัก', N'ยกเลิกการเลื่อนวันเข้าพัก')";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                return (int)cmd.ExecuteScalar();
            }
        }

        #endregion

        #region Data Loading

        private void LoadReservations()
        {
            try
            {
                int currentPage = ViewState["CurrentPage"] != null ? (int)ViewState["CurrentPage"] : 1;
                string filterStatus = ViewState["FilterStatus"]?.ToString() ?? "";

                DataTable dt = GetReservationsData(currentPage, out int totalRecords, out decimal totalAmount, out decimal totalDeposit);

                // Bind data
                gvReservations.DataSource = dt;
                gvReservations.DataBind();

                // Update summary
                lblResultCount.Text = totalRecords.ToString("N0");
                lblTotalAmount.Text = totalAmount.ToString("N0");
                lblTotalDeposit.Text = totalDeposit.ToString("N0");
                lblTotalRemain.Text = (totalAmount - totalDeposit).ToString("N0");

                // Update pagination
                int totalPages = (int)Math.Ceiling((double)totalRecords / PageSize);
                lblCurrentPage.Text = currentPage.ToString();
                lblTotalPages.Text = totalPages.ToString();

                btnPrevPage.Enabled = currentPage > 1;
                btnNextPage.Enabled = currentPage < totalPages;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadReservations Error: " + ex.Message);
            }
        }

        private DataTable GetReservationsData(int page, out int totalRecords, out decimal totalAmount, out decimal totalDeposit)
        {
            totalRecords = 0;
            totalAmount = 0;
            totalDeposit = 0;

            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Build WHERE clause
                StringBuilder whereClause = new StringBuilder("WHERE 1=1");
                List<SqlParameter> parameters = new List<SqlParameter>();

                // Search filter (use ISNULL for LEFT JOIN compatibility)
                string search = txtSearch.Text.Trim();
                if (!string.IsNullOrEmpty(search))
                {
                    whereClause.Append(@" AND (ISNULL(C.Name, '') LIKE @Search
                                          OR ISNULL(C.NickName, '') LIKE @Search
                                          OR R.Customer_MobilePhone LIKE @Search
                                          OR CAST(R.ID AS NVARCHAR) LIKE @Search)");
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
                    if (status == "ยกเลิก")
                    {
                        whereClause.Append(" AND R.Status IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'ลบจากการเลื่อนวันเข้าพัก', N'ยกเลิกการเลื่อนวันเข้าพัก')");
                    }
                    else if (status == "today")
                    {
                        whereClause.Append(" AND CAST(R.Created_Date AS DATE) = CAST(GETDATE() AS DATE)");
                    }
                    else
                    {
                        whereClause.Append(" AND R.Status = @Status");
                        parameters.Add(new SqlParameter("@Status", status));
                    }
                }

                // Date range filter (use ParseExact for reliable parsing)
                DateTime dateFrom, dateTo;
                if (!string.IsNullOrEmpty(txtDateFrom.Text) && DateTime.TryParseExact(txtDateFrom.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateFrom))
                {
                    whereClause.Append(" AND R.Created_Date >= @DateFrom");
                    parameters.Add(new SqlParameter("@DateFrom", SqlDbType.DateTime) { Value = dateFrom });
                }

                if (!string.IsNullOrEmpty(txtDateTo.Text) && DateTime.TryParseExact(txtDateTo.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateTo))
                {
                    whereClause.Append(" AND R.Created_Date <= @DateTo");
                    parameters.Add(new SqlParameter("@DateTo", SqlDbType.DateTime) { Value = dateTo.AddDays(1).AddSeconds(-1) });
                }

                // Get ORDER BY clause
                string orderBy = GetOrderByClause();

                // Count total records and calculate totals (use LEFT JOIN to include reservations without matching customer)
                string countQuery = $@"SELECT COUNT(*), ISNULL(SUM(R.TotalPrice), 0), ISNULL(SUM(ISNULL(R.Deposit, 0)), 0)
                                      FROM Reservation R
                                      LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
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
                            totalAmount = reader[1] != DBNull.Value ? Convert.ToDecimal(reader[1]) : 0;
                            totalDeposit = reader[2] != DBNull.Value ? Convert.ToDecimal(reader[2]) : 0;
                        }
                    }
                }

                // Get paginated data
                int offset = (page - 1) * PageSize;

                string dataQuery = $@"
                    SELECT R.ID, R.Created_Date, R.Customer_MobilePhone,
                           ISNULL(C.Name, R.Customer_MobilePhone) as Name,
                           ISNULL(C.NickName, '') as NickName,
                           R.CheckinDate, R.CheckoutDate, R.StayDays, R.TotalPrice,
                           ISNULL(R.Deposit, 0) as Deposit, R.Status, R.Reserve_By, R.Remark
                    FROM Reservation R
                    LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                    {whereClause}
                    ORDER BY {orderBy}
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                // Re-create parameters for data query
                List<SqlParameter> dataParams = new List<SqlParameter>();
                if (!string.IsNullOrEmpty(search))
                {
                    dataParams.Add(new SqlParameter("@Search", "%" + search + "%"));
                }
                if (!string.IsNullOrEmpty(status) && status != "ยกเลิก" && status != "today")
                {
                    dataParams.Add(new SqlParameter("@Status", status));
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

        private string GetOrderByClause()
        {
            switch (ddlSortBy.SelectedValue)
            {
                case "created_asc":
                    return "R.Created_Date ASC";
                case "checkin_asc":
                    return "R.CheckinDate ASC";
                case "checkin_desc":
                    return "R.CheckinDate DESC";
                case "price_desc":
                    return "R.TotalPrice DESC";
                case "price_asc":
                    return "R.TotalPrice ASC";
                default:
                    return "R.Created_Date DESC";
            }
        }

        private Dictionary<int, string> GetAccommodationsForReservations(DataTable reservations)
        {
            Dictionary<int, string> accomDict = new Dictionary<int, string>();

            if (reservations.Rows.Count == 0) return accomDict;

            // Get all reservation IDs
            List<string> ids = new List<string>();
            foreach (DataRow row in reservations.Rows)
            {
                ids.Add(row["ID"].ToString());
            }

            string query = @"SELECT RA.Reservation_ID, A.AccomName
                            FROM Reservation_Accommodation RA
                            INNER JOIN Accommodation A ON A.ID = RA.Accommodation_ID
                            WHERE RA.Reservation_ID IN (" + string.Join(",", ids) + ")";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int resId = reader.GetInt32(0);
                            string accomName = reader.GetString(1);

                            if (accomDict.ContainsKey(resId))
                            {
                                accomDict[resId] += ", " + accomName;
                            }
                            else
                            {
                                accomDict[resId] = accomName;
                            }
                        }
                    }
                }
            }

            return accomDict;
        }

        #endregion

        #region Event Handlers

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            ViewState["CurrentPage"] = 1;
            ViewState["FilterStatus"] = "";
            LoadReservations();
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            ddlStatus.SelectedIndex = 0;
            txtDateFrom.Text = "";
            txtDateTo.Text = "";
            ddlSortBy.SelectedIndex = 0;

            ViewState["CurrentPage"] = 1;
            ViewState["FilterStatus"] = "";

            LoadReservations();
        }

        protected void StatCard_Click(object sender, EventArgs e)
        {
            LinkButton btn = (LinkButton)sender;
            string filter = btn.CommandArgument;

            // Reset page
            ViewState["CurrentPage"] = 1;

            // Set filter based on clicked stat
            switch (filter)
            {
                case "all":
                    ViewState["FilterStatus"] = "";
                    ddlStatus.SelectedIndex = 0;
                    break;
                case "today":
                    ViewState["FilterStatus"] = "today";
                    ddlStatus.SelectedIndex = 0;
                    // Clear date range for today
                    txtDateFrom.Text = "";
                    txtDateTo.Text = "";
                    break;
                case "deposit":
                    ViewState["FilterStatus"] = "มัดจำแล้ว";
                    ddlStatus.SelectedValue = "มัดจำแล้ว";
                    break;
                case "checkin":
                    ViewState["FilterStatus"] = "เช็คอินแล้ว";
                    ddlStatus.SelectedValue = "เช็คอินแล้ว";
                    break;
                case "checkout":
                    ViewState["FilterStatus"] = "เช็คเอาท์แล้ว";
                    ddlStatus.SelectedValue = "เช็คเอาท์แล้ว";
                    break;
                case "complete":
                    ViewState["FilterStatus"] = "เสร็จสิ้น";
                    ddlStatus.SelectedValue = "เสร็จสิ้น";
                    break;
                case "cancel":
                    ViewState["FilterStatus"] = "ยกเลิก";
                    ddlStatus.SelectedValue = "ยกเลิก";
                    break;
            }

            LoadReservations();
        }

        protected void btnPrevPage_Click(object sender, EventArgs e)
        {
            int currentPage = (int)ViewState["CurrentPage"];
            if (currentPage > 1)
            {
                ViewState["CurrentPage"] = currentPage - 1;
                LoadReservations();
            }
        }

        protected void btnNextPage_Click(object sender, EventArgs e)
        {
            int currentPage = (int)ViewState["CurrentPage"];
            int totalPages = int.Parse(lblTotalPages.Text);
            if (currentPage < totalPages)
            {
                ViewState["CurrentPage"] = currentPage + 1;
                LoadReservations();
            }
        }

        protected void btnExport_Click(object sender, EventArgs e)
        {
            try
            {
                // Get all data without pagination
                ViewState["CurrentPage"] = 1;
                DataTable dt = GetAllReservationsForExport();

                if (dt.Rows.Count == 0)
                {
                    return;
                }

                // Create CSV
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("เลขที่จอง,วันที่จอง,ชื่อลูกค้า,เบอร์โทร,วันเช็คอิน,วันเช็คเอาท์,จำนวนคืน,ราคารวม,มัดจำ,ค้างชำระ,สถานะ,จองโดย,หมายเหตุ");

                foreach (DataRow row in dt.Rows)
                {
                    decimal total = Convert.ToDecimal(row["TotalPrice"]);
                    decimal deposit = Convert.ToDecimal(row["Deposit"]);
                    decimal remain = total - deposit;

                    sb.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
                        row["ID"],
                        Convert.ToDateTime(row["Created_Date"]).ToString("dd/MM/yyyy HH:mm"),
                        EscapeCsvField(row["Name"]?.ToString()),
                        row["Customer_MobilePhone"],
                        FormatDateForExport(row["CheckinDate"]),
                        FormatDateForExport(row["CheckoutDate"]),
                        row["StayDays"],
                        total.ToString("N0"),
                        deposit.ToString("N0"),
                        remain.ToString("N0"),
                        row["Status"],
                        EscapeCsvField(row["Reserve_By"]?.ToString()),
                        EscapeCsvField(row["Remark"]?.ToString())
                    ));
                }

                // Export
                string fileName = $"Reservations_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
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

        private DataTable GetAllReservationsForExport()
        {
            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                StringBuilder whereClause = new StringBuilder("WHERE 1=1");
                List<SqlParameter> parameters = new List<SqlParameter>();

                // Apply same filters
                string search = txtSearch.Text.Trim();
                if (!string.IsNullOrEmpty(search))
                {
                    whereClause.Append(@" AND (ISNULL(C.Name, '') LIKE @Search
                                          OR ISNULL(C.NickName, '') LIKE @Search
                                          OR R.Customer_MobilePhone LIKE @Search
                                          OR CAST(R.ID AS NVARCHAR) LIKE @Search)");
                    parameters.Add(new SqlParameter("@Search", "%" + search + "%"));
                }

                string status = ddlStatus.SelectedValue;
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "ยกเลิก")
                    {
                        whereClause.Append(" AND R.Status IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')");
                    }
                    else
                    {
                        whereClause.Append(" AND R.Status = @Status");
                        parameters.Add(new SqlParameter("@Status", status));
                    }
                }

                DateTime dateFrom, dateTo;
                if (!string.IsNullOrEmpty(txtDateFrom.Text) && DateTime.TryParseExact(txtDateFrom.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateFrom))
                {
                    whereClause.Append(" AND R.Created_Date >= @DateFrom");
                    parameters.Add(new SqlParameter("@DateFrom", SqlDbType.DateTime) { Value = dateFrom });
                }

                if (!string.IsNullOrEmpty(txtDateTo.Text) && DateTime.TryParseExact(txtDateTo.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateTo))
                {
                    whereClause.Append(" AND R.Created_Date <= @DateTo");
                    parameters.Add(new SqlParameter("@DateTo", SqlDbType.DateTime) { Value = dateTo.AddDays(1).AddSeconds(-1) });
                }

                string query = $@"
                    SELECT R.ID, R.Created_Date, R.Customer_MobilePhone,
                           ISNULL(C.Name, R.Customer_MobilePhone) as Name,
                           R.CheckinDate, R.CheckoutDate, R.StayDays, R.TotalPrice,
                           ISNULL(R.Deposit, 0) as Deposit, R.Status, R.Reserve_By, R.Remark
                    FROM Reservation R
                    LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                    {whereClause}
                    ORDER BY R.Created_Date DESC";

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

        protected void gvReservations_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                DataRowView drv = (DataRowView)e.Row.DataItem;

                // Set status badge
                Label lblStatus = (Label)e.Row.FindControl("lblStatus");
                if (lblStatus != null)
                {
                    string status = drv["Status"]?.ToString() ?? "";
                    lblStatus.Text = status;

                    if (status.Contains("มัดจำ"))
                    {
                        lblStatus.CssClass = "status-badge status-deposit";
                    }
                    else if (status.Contains("เช็คอินแล้ว"))
                    {
                        lblStatus.CssClass = "status-badge status-checkin";
                    }
                    else if (status.Contains("เช็คเอาท์"))
                    {
                        lblStatus.CssClass = "status-badge status-checkout";
                    }
                    else if (status.Contains("เสร็จสิ้น"))
                    {
                        lblStatus.CssClass = "status-badge status-complete";
                    }
                    else if (status.Contains("ยกเลิก") || status.Contains("ลบ"))
                    {
                        lblStatus.CssClass = "status-badge status-cancel";
                    }
                    else
                    {
                        lblStatus.CssClass = "status-badge status-postpone";
                    }
                }

                // Load accommodations
                Literal litAccom = (Literal)e.Row.FindControl("litAccom");
                if (litAccom != null)
                {
                    int resId = Convert.ToInt32(drv["ID"]);
                    string accoms = GetAccommodationsForReservation(resId);

                    // Split and create tags
                    StringBuilder sb = new StringBuilder();
                    foreach (string accom in accoms.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        sb.AppendFormat("<span class='accom-tag'>{0}</span>", HttpUtility.HtmlEncode(accom));
                    }
                    litAccom.Text = sb.Length > 0 ? sb.ToString() : "<span class='accom-tag'>-</span>";
                }

                // Highlight today's check-in
                DateTime checkinDate = Convert.ToDateTime(drv["CheckinDate"]);
                if (checkinDate.Date == DateTime.Today)
                {
                    e.Row.CssClass = "today-row";
                }
            }
        }

        private string GetAccommodationsForReservation(int reservationId)
        {
            StringBuilder sb = new StringBuilder();

            string query = @"SELECT A.AccomName
                            FROM Reservation_Accommodation RA
                            INNER JOIN Accommodation A ON A.ID = RA.Accommodation_ID
                            WHERE RA.Reservation_ID = @ResId";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ResId", reservationId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (sb.Length > 0) sb.Append(", ");
                            sb.Append(reader.GetString(0));
                        }
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString() : "-";
        }

        #endregion

        #region Helper Methods

        protected string FormatDate(object date)
        {
            if (date == null || date == DBNull.Value) return "-";

            DateTime dt = Convert.ToDateTime(date);
            if (dt.Year < 2000) return "-";

            return dt.ToString("dd/MM/yyyy");
        }

        private string FormatDateForExport(object date)
        {
            if (date == null || date == DBNull.Value) return "";

            DateTime dt = Convert.ToDateTime(date);
            if (dt.Year < 2000) return "";

            return dt.ToString("dd/MM/yyyy");
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
