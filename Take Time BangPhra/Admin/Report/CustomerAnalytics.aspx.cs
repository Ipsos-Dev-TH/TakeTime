using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.Admin.Report
{
    public partial class CustomerAnalytics : Page
    {
        private readonly string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code codeInstance = new code();

        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                // Check permission
                if (Session["permission"]?.ToString() != "True" ||
                    (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
                {
                    Response.Redirect("/Default");
                    return;
                }

                if (!IsPostBack)
                {
                    InitializePage();
                    LoadCustomerData();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ CustomerAnalytics Error: {ex.Message}");
                Response.Redirect("/Default");
            }
        }

        private void InitializePage()
        {
            // Populate year dropdown (last 5 years)
            int currentYear = DateTime.Now.Year;
            ddlYear.Items.Clear();

            for (int i = 0; i < 5; i++)
            {
                int year = currentYear - i;
                ddlYear.Items.Add(new ListItem(year.ToString(), year.ToString()));
            }

            ddlYear.SelectedIndex = 0;
            litSelectedYear.Text = currentYear.ToString();
        }

        private void LoadCustomerData()
        {
            try
            {
                int selectedYear = Convert.ToInt32(ddlYear.SelectedValue);
                string customerType = ddlCustomerType.SelectedValue;
                int limit = Convert.ToInt32(ddlLimit.SelectedValue);

                System.Diagnostics.Debug.WriteLine($"📊 Loading Customer Data: Year={selectedYear}, Type={customerType}, Limit={limit}");

                // Update year display
                litSelectedYear.Text = selectedYear.ToString();

                // Load summary stats
                LoadSummaryStats(selectedYear);

                // Load repeat customers (ตามที่ user ขอ!)
                LoadRepeatCustomers(selectedYear);

                // Load all customers
                LoadAllCustomers(selectedYear, customerType, limit);

                // Load chart data
                LoadChartData(selectedYear);

                // Load additional analytics
                LoadAdditionalStats(selectedYear);

                // Load monthly trend
                LoadMonthlyTrendData(selectedYear);

                System.Diagnostics.Debug.WriteLine($"✅ Customer data loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadCustomerData Error: {ex.Message}");
                ShowMessage($"เกิดข้อผิดพลาด: {ex.Message}", "error");
            }
        }

        #region Summary Stats

        private void LoadSummaryStats(int year)
        {
            try
            {
                string query = @"
                    SELECT
                        COUNT(DISTINCT c.MobilePhone) as TotalCustomers,
                        SUM(CASE WHEN BookingCount = 1 THEN 1 ELSE 0 END) as NewCustomers,
                        SUM(CASE WHEN BookingCount BETWEEN 2 AND 5 THEN 1 ELSE 0 END) as ReturningCustomers,
                        SUM(CASE WHEN BookingCount > 5 THEN 1 ELSE 0 END) as VIPCustomers
                    FROM (
                        SELECT
                            r.Customer_MobilePhone,
                            COUNT(*) as BookingCount
                        FROM Reservation r
                        WHERE YEAR(r.Created_Date) = @Year
                          AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                        GROUP BY r.Customer_MobilePhone
                    ) as CustomerStats
                    CROSS JOIN Customer c
                    WHERE c.MobilePhone = CustomerStats.Customer_MobilePhone";

                var parameters = new Dictionary<string, object> { { "@Year", year } };
                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    litTotalCustomers.Text = row["TotalCustomers"].ToString();
                    litNewCustomers.Text = row["NewCustomers"].ToString();
                    litReturningCustomers.Text = row["ReturningCustomers"].ToString();
                    litVIPCustomers.Text = row["VIPCustomers"].ToString();
                }
                else
                {
                    litTotalCustomers.Text = "0";
                    litNewCustomers.Text = "0";
                    litReturningCustomers.Text = "0";
                    litVIPCustomers.Text = "0";
                }

                System.Diagnostics.Debug.WriteLine($"📊 Summary Stats: Total={litTotalCustomers.Text}, New={litNewCustomers.Text}, Returning={litReturningCustomers.Text}, VIP={litVIPCustomers.Text}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadSummaryStats Error: {ex.Message}");
            }
        }

        #endregion

        #region Repeat Customers (ลูกค้าที่กลับมาพักซ้ำ - ตามที่ user ขอ!)

        private void LoadRepeatCustomers(int year)
        {
            try
            {
                string query = @"
                    SELECT
                        ISNULL(c.FullName, c.Name) as CustomerName,
                        c.MobilePhone as PhoneNumber,
                        COUNT(*) as TotalBookings,
                        SUM(r.TotalPrice) as TotalSpent,
                        AVG(r.TotalPrice) as AvgSpending,
                        MIN(r.Created_Date) as FirstVisit,
                        MAX(r.Created_Date) as LastVisit,
                        (
                            SELECT TOP 1 a.AccomName
                            FROM Reservation_Accommodation ra
                            INNER JOIN Accommodation a ON ra.Accommodation_ID = a.ID
                            WHERE ra.Reservation_ID IN (
                                SELECT ID FROM Reservation WHERE Customer_MobilePhone = c.MobilePhone
                            )
                            GROUP BY a.AccomName
                            ORDER BY COUNT(*) DESC
                        ) as PreferredAccommodation
                    FROM Reservation r
                    INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                    WHERE YEAR(r.Created_Date) = @Year
                      AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                    GROUP BY c.FullName, c.Name, c.MobilePhone
                    HAVING COUNT(*) > 1  -- เฉพาะลูกค้าที่มาซ้ำ (>1 ครั้ง)
                    ORDER BY TotalBookings DESC, TotalSpent DESC";

                var parameters = new Dictionary<string, object> { { "@Year", year } };
                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                gvRepeatCustomers.DataSource = dt;
                gvRepeatCustomers.DataBind();

                // Update count
                litRepeatCount.Text = dt != null ? dt.Rows.Count.ToString() : "0";

                System.Diagnostics.Debug.WriteLine($"🔄 Repeat Customers: {litRepeatCount.Text} found");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadRepeatCustomers Error: {ex.Message}");
                gvRepeatCustomers.DataSource = null;
                gvRepeatCustomers.DataBind();
                litRepeatCount.Text = "0";
            }
        }

        #endregion

        #region All Customers

        private void LoadAllCustomers(int year, string customerType, int limit)
        {
            try
            {
                // Build query based on customer type
                string whereClause = "";
                if (customerType == "NEW")
                {
                    whereClause = "HAVING COUNT(*) = 1";
                }
                else if (customerType == "RETURNING")
                {
                    whereClause = "HAVING COUNT(*) BETWEEN 2 AND 5";
                }
                else if (customerType == "VIP")
                {
                    whereClause = "HAVING COUNT(*) > 5";
                }

                string topClause = limit > 0 ? $"TOP {limit}" : "";

                string query = $@"
                    SELECT {topClause}
                        ISNULL(c.FullName, c.Name) as CustomerName,
                        c.MobilePhone as PhoneNumber,
                        ISNULL(c.Email, '-') as Email,
                        COUNT(*) as TotalBookings,
                        SUM(r.TotalPrice) as TotalSpent,
                        MAX(r.Created_Date) as LastVisit
                    FROM Reservation r
                    INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                    WHERE YEAR(r.Created_Date) = @Year
                      AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                    GROUP BY c.FullName, c.Name, c.MobilePhone, c.Email
                    {whereClause}
                    ORDER BY TotalBookings DESC, TotalSpent DESC";

                var parameters = new Dictionary<string, object> { { "@Year", year } };
                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                gvAllCustomers.DataSource = dt;
                gvAllCustomers.DataBind();

                // Update counts
                litDisplayCount.Text = dt != null ? dt.Rows.Count.ToString() : "0";

                // Get total count (without limit)
                string countQuery = $@"
                    SELECT COUNT(*) as Total
                    FROM (
                        SELECT c.MobilePhone, COUNT(*) as BookingCount
                        FROM Reservation r
                        INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                        WHERE YEAR(r.Created_Date) = @Year
                          AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                        GROUP BY c.MobilePhone
                        {whereClause}
                    ) as Counts";

                DataTable countDt = codeInstance.DatabaseQuerySafe(conn, countQuery, parameters);
                litTotalCount.Text = countDt != null && countDt.Rows.Count > 0 ? countDt.Rows[0]["Total"].ToString() : "0";

                System.Diagnostics.Debug.WriteLine($"📋 All Customers: Showing {litDisplayCount.Text} of {litTotalCount.Text}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadAllCustomers Error: {ex.Message}");
                gvAllCustomers.DataSource = null;
                gvAllCustomers.DataBind();
                litDisplayCount.Text = "0";
                litTotalCount.Text = "0";
            }
        }

        #endregion

        #region Charts Data

        private void LoadChartData(int year)
        {
            try
            {
                // Customer Segmentation Chart
                int newCustomers = Convert.ToInt32(litNewCustomers.Text);
                int returningCustomers = Convert.ToInt32(litReturningCustomers.Text);
                int vipCustomers = Convert.ToInt32(litVIPCustomers.Text);

                hfSegmentationData.Value = $"[{newCustomers}, {returningCustomers}, {vipCustomers}]";

                // Top 10 Customers Chart
                string query = @"
                    SELECT TOP 10
                        ISNULL(c.FullName, c.Name) as CustomerName,
                        SUM(r.TotalPrice) as TotalSpent
                    FROM Reservation r
                    INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                    WHERE YEAR(r.Created_Date) = @Year
                      AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                    GROUP BY c.FullName, c.Name, c.MobilePhone
                    ORDER BY TotalSpent DESC";

                var parameters = new Dictionary<string, object> { { "@Year", year } };
                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                if (dt != null && dt.Rows.Count > 0)
                {
                    List<string> names = new List<string>();
                    List<decimal> amounts = new List<decimal>();

                    foreach (DataRow row in dt.Rows)
                    {
                        string name = row["CustomerName"].ToString();
                        // Truncate name if too long
                        if (name.Length > 20)
                        {
                            name = name.Substring(0, 17) + "...";
                        }
                        names.Add($"\"{name}\"");
                        amounts.Add(Convert.ToDecimal(row["TotalSpent"]));
                    }

                    hfTopCustomersLabels.Value = "[" + string.Join(",", names) + "]";
                    hfTopCustomersData.Value = "[" + string.Join(",", amounts) + "]";
                }
                else
                {
                    hfTopCustomersLabels.Value = "['','','','','','','','','','']";
                    hfTopCustomersData.Value = "[0,0,0,0,0,0,0,0,0,0]";
                }

                System.Diagnostics.Debug.WriteLine($"📈 Chart Data: Segmentation={hfSegmentationData.Value}, TopCustomers={dt?.Rows.Count ?? 0}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadChartData Error: {ex.Message}");
                hfSegmentationData.Value = "[0,0,0]";
                hfTopCustomersLabels.Value = "['','','','','','','','','','']";
                hfTopCustomersData.Value = "[0,0,0,0,0,0,0,0,0,0]";
            }
        }

        #endregion

        #region Event Handlers

        protected void ddlYear_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadCustomerData();
        }

        protected void ddlCustomerType_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadCustomerData();
        }

        protected void ddlLimit_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadCustomerData();
        }

        protected void gvAllCustomers_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvAllCustomers.PageIndex = e.NewPageIndex;
            LoadCustomerData();
        }

        protected void gvAllCustomers_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ViewReservations")
            {
                string phoneNumber = e.CommandArgument.ToString();
                LoadReservationDetail(phoneNumber);
            }
        }

        protected void btnCloseDetail_Click(object sender, EventArgs e)
        {
            pnlReservationDetail.Visible = false;
        }

        private void LoadReservationDetail(string phoneNumber)
        {
            try
            {
                int selectedYear = Convert.ToInt32(ddlYear.SelectedValue);

                // Get customer name
                string nameQuery = @"SELECT ISNULL(FullName, Name) as CustomerName FROM Customer WHERE MobilePhone = @Phone";
                var nameParams = new Dictionary<string, object> { { "@Phone", phoneNumber } };
                DataTable nameDt = codeInstance.DatabaseQuerySafe(conn, nameQuery, nameParams);
                litSelectedCustomer.Text = nameDt != null && nameDt.Rows.Count > 0
                    ? nameDt.Rows[0]["CustomerName"].ToString() + " (" + phoneNumber + ")"
                    : phoneNumber;

                // Get all reservations for this customer
                string query = @"
                    SELECT
                        r.ID,
                        r.CheckIn_Date,
                        r.CheckOut_Date,
                        r.TotalPrice,
                        r.Status,
                        r.Created_Date,
                        ISNULL(r.Remark, '-') as Remark,
                        STUFF((
                            SELECT ', ' + a.AccomName
                            FROM Reservation_Accommodation ra
                            INNER JOIN Accommodation a ON ra.Accommodation_ID = a.ID
                            WHERE ra.Reservation_ID = r.ID
                            FOR XML PATH('')
                        ), 1, 2, '') as AccommodationList
                    FROM Reservation r
                    WHERE r.Customer_MobilePhone = @Phone
                      AND YEAR(r.Created_Date) = @Year
                    ORDER BY r.Created_Date DESC";

                var parameters = new Dictionary<string, object>
                {
                    { "@Phone", phoneNumber },
                    { "@Year", selectedYear }
                };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                gvReservationDetail.DataSource = dt;
                gvReservationDetail.DataBind();

                pnlReservationDetail.Visible = true;

                System.Diagnostics.Debug.WriteLine($"📝 Loaded {dt?.Rows.Count ?? 0} reservations for {phoneNumber}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadReservationDetail Error: {ex.Message}");
                pnlReservationDetail.Visible = false;
            }
        }

        protected void btnExport_Click(object sender, EventArgs e)
        {
            try
            {
                int year = Convert.ToInt32(ddlYear.SelectedValue);

                // Create CSV content
                StringBuilder csv = new StringBuilder();

                // Add BOM for UTF-8 Excel compatibility
                csv.Append("\uFEFF");

                // Header
                csv.AppendLine($"รายงานวิเคราะห์ลูกค้า - ปี {year}");
                csv.AppendLine($"วันที่ออกรายงาน:,{DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                csv.AppendLine($"ผู้ออกรายงาน:,{Session["User"]?.ToString() ?? "ผู้ใช้งาน"}");
                csv.AppendLine();

                // Summary
                csv.AppendLine("สรุปข้อมูลลูกค้า");
                csv.AppendLine($"ลูกค้าทั้งหมด:,{litTotalCustomers.Text} ราย");
                csv.AppendLine($"ลูกค้าใหม่:,{litNewCustomers.Text} ราย");
                csv.AppendLine($"ลูกค้าประจำ:,{litReturningCustomers.Text} ราย");
                csv.AppendLine($"ลูกค้า VIP:,{litVIPCustomers.Text} ราย");
                csv.AppendLine();

                // Repeat Customers Section
                csv.AppendLine("ลูกค้าที่กลับมาพักซ้ำ");
                csv.AppendLine("#,ชื่อลูกค้า,เบอร์โทร,จำนวนครั้ง,ประเภท,ยอดใช้จ่ายรวม,ค่าเฉลี่ย/ครั้ง,มาครั้งแรก,มาครั้งล่าสุด,ที่พักที่ชอบ");

                DataTable repeatDt = GetRepeatCustomersDataForExport(year);
                if (repeatDt != null)
                {
                    int rowNum = 1;
                    foreach (DataRow row in repeatDt.Rows)
                    {
                        int bookings = Convert.ToInt32(row["TotalBookings"]);
                        string type = GetCustomerTypeText(bookings);
                        decimal totalSpent = row["TotalSpent"] != DBNull.Value ? Convert.ToDecimal(row["TotalSpent"]) : 0;
                        decimal avgSpending = row["AvgSpending"] != DBNull.Value ? Convert.ToDecimal(row["AvgSpending"]) : 0;
                        string firstVisit = row["FirstVisit"] != DBNull.Value ? Convert.ToDateTime(row["FirstVisit"]).ToString("dd/MM/yyyy") : "";
                        string lastVisit = row["LastVisit"] != DBNull.Value ? Convert.ToDateTime(row["LastVisit"]).ToString("dd/MM/yyyy") : "";

                        csv.AppendLine($"{rowNum}," +
                            $"{row["CustomerName"]}," +
                            $"{row["PhoneNumber"]}," +
                            $"{bookings}," +
                            $"{type}," +
                            $"{totalSpent:N2}," +
                            $"{avgSpending:N2}," +
                            $"{firstVisit}," +
                            $"{lastVisit}," +
                            $"{row["PreferredAccommodation"]}");
                        rowNum++;
                    }
                }

                // Send file to browser
                Response.Clear();
                Response.ContentType = "text/csv";
                Response.ContentEncoding = Encoding.UTF8;
                Response.Charset = "UTF-8";
                Response.AddHeader("Content-Disposition", $"attachment;filename=CustomerAnalytics_{year}_{DateTime.Now:yyyyMMdd}.csv");
                Response.Write(csv.ToString());
                Response.End();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Export Error: {ex.Message}");
                ShowMessage($"เกิดข้อผิดพลาดในการ export: {ex.Message}", "error");
            }
        }

        #endregion

        #region Helper Methods

        protected string GetCustomerTypeBadge(int bookingCount)
        {
            if (bookingCount == 1)
            {
                return "<span class='badge badge-new'>ลูกค้าใหม่</span>";
            }
            else if (bookingCount >= 2 && bookingCount <= 5)
            {
                return "<span class='badge badge-returning'>ลูกค้าประจำ</span>";
            }
            else
            {
                return "<span class='badge badge-vip'>VIP</span>";
            }
        }

        private string GetCustomerTypeText(int bookingCount)
        {
            if (bookingCount == 1)
                return "ลูกค้าใหม่";
            else if (bookingCount >= 2 && bookingCount <= 5)
                return "ลูกค้าประจำ";
            else
                return "VIP";
        }

        private DataTable GetRepeatCustomersDataForExport(int year)
        {
            try
            {
                string query = @"
                    SELECT
                        ISNULL(c.FullName, c.Name) as CustomerName,
                        c.MobilePhone as PhoneNumber,
                        COUNT(*) as TotalBookings,
                        SUM(r.TotalPrice) as TotalSpent,
                        AVG(r.TotalPrice) as AvgSpending,
                        MIN(r.Created_Date) as FirstVisit,
                        MAX(r.Created_Date) as LastVisit,
                        (
                            SELECT TOP 1 a.AccomName
                            FROM Reservation_Accommodation ra
                            INNER JOIN Accommodation a ON ra.Accommodation_ID = a.ID
                            WHERE ra.Reservation_ID IN (
                                SELECT ID FROM Reservation WHERE Customer_MobilePhone = c.MobilePhone
                            )
                            GROUP BY a.AccomName
                            ORDER BY COUNT(*) DESC
                        ) as PreferredAccommodation
                    FROM Reservation r
                    INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                    WHERE YEAR(r.Created_Date) = @Year
                      AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                    GROUP BY c.FullName, c.Name, c.MobilePhone
                    HAVING COUNT(*) > 1
                    ORDER BY TotalBookings DESC, TotalSpent DESC";

                var parameters = new Dictionary<string, object> { { "@Year", year } };
                return codeInstance.DatabaseQuerySafe(conn, query, parameters);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetRepeatCustomersDataForExport Error: {ex.Message}");
                return null;
            }
        }

        private void ShowMessage(string message, string type)
        {
            string icon = type == "success" ? "✅" : "❌";
            ScriptManager.RegisterStartupScript(this, GetType(), "message",
                $"alert('{icon} {message}');", true);
        }

        protected string GetStatusBadgeClass(string status)
        {
            switch (status)
            {
                case "ยืนยันแล้ว":
                case "จองแล้ว":
                    return "status-badge status-confirmed";
                case "เช็คอินแล้ว":
                    return "status-badge status-checkedin";
                case "เช็คเอาท์แล้ว":
                    return "status-badge status-checkedout";
                case "ยกเลิกคืนเงิน":
                case "ยกเลิกไม่คืนเงิน":
                    return "status-badge status-cancelled";
                default:
                    return "status-badge";
            }
        }

        private void LoadAdditionalStats(int year)
        {
            try
            {
                string query = @"
                    SELECT
                        ISNULL(AVG(CustomerTotal), 0) as AvgSpending,
                        SUM(TotalReservations) as TotalReservations,
                        CASE WHEN COUNT(*) > 0 THEN
                            CAST(SUM(CASE WHEN TotalReservations > 1 THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS DECIMAL(5,2))
                        ELSE 0 END as ReturnRate
                    FROM (
                        SELECT
                            c.MobilePhone,
                            COUNT(*) as TotalReservations,
                            SUM(r.TotalPrice) as CustomerTotal
                        FROM Reservation r
                        INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                        WHERE YEAR(r.Created_Date) = @Year
                          AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                        GROUP BY c.MobilePhone
                    ) as CustomerStats";

                var parameters = new Dictionary<string, object> { { "@Year", year } };
                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    litAvgSpendingPerCustomer.Text = row["AvgSpending"] != DBNull.Value
                        ? Convert.ToDecimal(row["AvgSpending"]).ToString("N0") : "0";
                    litTotalReservations.Text = row["TotalReservations"] != DBNull.Value
                        ? row["TotalReservations"].ToString() : "0";
                    litReturnRate.Text = row["ReturnRate"] != DBNull.Value
                        ? Convert.ToDecimal(row["ReturnRate"]).ToString("N1") : "0";
                }

                // Calculate average days between visits for returning customers
                string daysQuery = @"
                    SELECT AVG(DaysBetween) as AvgDays
                    FROM (
                        SELECT
                            c.MobilePhone,
                            DATEDIFF(DAY, MIN(r.Created_Date), MAX(r.Created_Date)) / NULLIF(COUNT(*) - 1, 0) as DaysBetween
                        FROM Reservation r
                        INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                        WHERE YEAR(r.Created_Date) = @Year
                          AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                        GROUP BY c.MobilePhone
                        HAVING COUNT(*) > 1
                    ) as VisitStats
                    WHERE DaysBetween IS NOT NULL";

                DataTable daysDt = codeInstance.DatabaseQuerySafe(conn, daysQuery, parameters);
                if (daysDt != null && daysDt.Rows.Count > 0 && daysDt.Rows[0]["AvgDays"] != DBNull.Value)
                {
                    litAvgDaysBetweenVisits.Text = Convert.ToInt32(daysDt.Rows[0]["AvgDays"]).ToString();
                }
                else
                {
                    litAvgDaysBetweenVisits.Text = "-";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadAdditionalStats Error: {ex.Message}");
                litAvgSpendingPerCustomer.Text = "0";
                litTotalReservations.Text = "0";
                litReturnRate.Text = "0";
                litAvgDaysBetweenVisits.Text = "-";
            }
        }

        private void LoadMonthlyTrendData(int year)
        {
            try
            {
                string query = @"
                    SELECT
                        MONTH(r.Created_Date) as Month,
                        SUM(r.TotalPrice) as TotalSpent,
                        COUNT(DISTINCT r.Customer_MobilePhone) as CustomerCount
                    FROM Reservation r
                    WHERE YEAR(r.Created_Date) = @Year
                      AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                    GROUP BY MONTH(r.Created_Date)
                    ORDER BY Month";

                var parameters = new Dictionary<string, object> { { "@Year", year } };
                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                // Initialize arrays for all 12 months
                decimal[] monthlyAmounts = new decimal[12];
                int[] monthlyCustomers = new int[12];
                string[] monthLabels = { "ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.",
                                         "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค." };

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        int monthIndex = Convert.ToInt32(row["Month"]) - 1;
                        if (monthIndex >= 0 && monthIndex < 12)
                        {
                            monthlyAmounts[monthIndex] = Convert.ToDecimal(row["TotalSpent"]);
                            monthlyCustomers[monthIndex] = Convert.ToInt32(row["CustomerCount"]);
                        }
                    }
                }

                // Create JSON arrays
                List<string> labels = monthLabels.Select(l => $"\"{l}\"").ToList();
                hfMonthlyTrendLabels.Value = "[" + string.Join(",", labels) + "]";
                hfMonthlyTrendData.Value = "[" + string.Join(",", monthlyAmounts) + "]";
                hfMonthlyTrendCustomers.Value = "[" + string.Join(",", monthlyCustomers) + "]";

                System.Diagnostics.Debug.WriteLine($"📈 Monthly Trend: {dt?.Rows.Count ?? 0} months with data");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadMonthlyTrendData Error: {ex.Message}");
                hfMonthlyTrendLabels.Value = "";
                hfMonthlyTrendData.Value = "";
                hfMonthlyTrendCustomers.Value = "";
            }
        }

        #endregion
    }
}
