using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;
using System.Text;

namespace Take_Time_BangPhra
{
    public partial class DisplayReserve : System.Web.UI.Page
    {
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            Page.MaintainScrollPositionOnPostBack = true;
            try
            {
                if (Session["permission"]?.ToString() == "True")
                {
                    if (!IsPostBack)
                    {
                        // โหลด dropdown ปีจากข้อมูลการจองจริง
                        LoadYearDropdown();

                        // ตั้งค่าปีและเดือนปัจจุบัน
                        int currentYear = DateTime.Now.Year;
                        int currentMonth = DateTime.Now.Month;

                        // เลือกปีปัจจุบันถ้ามีในรายการ
                        if (DropDownList1.Items.FindByValue(currentYear.ToString()) != null)
                        {
                            DropDownList1.SelectedValue = currentYear.ToString();
                        }
                        else if (DropDownList1.Items.Count > 0)
                        {
                            DropDownList1.SelectedIndex = 0;
                        }

                        DropDownList2.SelectedValue = currentMonth.ToString();

                        System.Diagnostics.Debug.WriteLine($"Initial Load: Year={DropDownList1.SelectedValue}, Month={currentMonth}");

                        // โหลดข้อมูล
                        LoadReservationData();
                    }
                }
                else
                {
                    Response.Redirect("~/Default.aspx");
                }
            }
            catch (Exception ex)
            {
                Response.Redirect("~/Default.aspx");
            }
        }

        /// <summary>
        /// โหลดรายการปีจากข้อมูลการจองจริง (distinct years from Reservation)
        /// </summary>
        private void LoadYearDropdown()
        {
            try
            {
                DropDownList1.Items.Clear();

                using (SqlConnection connection = new SqlConnection(conn))
                {
                    // ดึงปีที่มีข้อมูลการจองจริง รวมกับปีปัจจุบัน
                    string sql = @"
                        SELECT DISTINCT YEAR(CheckinDate) AS Year
                        FROM Reservation
                        WHERE CheckinDate IS NOT NULL
                        UNION
                        SELECT YEAR(GETDATE()) AS Year
                        ORDER BY Year DESC";

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string year = reader["Year"].ToString();
                                DropDownList1.Items.Add(new ListItem(year, year));
                            }
                        }
                    }
                }

                // ถ้าไม่มีข้อมูลเลย ให้ใส่ปีปัจจุบัน
                if (DropDownList1.Items.Count == 0)
                {
                    string currentYear = DateTime.Now.Year.ToString();
                    DropDownList1.Items.Add(new ListItem(currentYear, currentYear));
                }
            }
            catch (Exception ex)
            {
                // ถ้า error ให้ใส่ปีปัจจุบันเป็น fallback
                string currentYear = DateTime.Now.Year.ToString();
                DropDownList1.Items.Clear();
                DropDownList1.Items.Add(new ListItem(currentYear, currentYear));
                System.Diagnostics.Debug.WriteLine($"LoadYearDropdown Error: {ex.Message}");
            }
        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            int year = Convert.ToInt32(DropDownList1.SelectedValue);
            int month = Convert.ToInt32(DropDownList2.SelectedValue);

            System.Diagnostics.Debug.WriteLine($"Button Click: Year={year}, Month={month}");

            LoadReservationData();
        }

        private void LoadReservationData()
        {
            try
            {
                int year = Convert.ToInt32(DropDownList1.SelectedValue);
                int month = Convert.ToInt32(DropDownList2.SelectedValue);

                // Debug ข้อมูลก่อนคำนวณ
                DebugReservationData(year, month);

                // โหลดข้อมูลสรุปรายได้
                LoadRevenueSummary(year, month);

                // โหลดข้อมูลสำหรับ GridView
                LoadReservationGridView(year, month);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadReservationData: {ex.Message}");
                ShowErrorMessage($"เกิดข้อผิดพลาด: {ex.Message}");
            }
        }

        private void LoadRevenueSummary(int year, int month)
        {
            try
            {
                // ใช้ข้อมูลจาก Report โดยตรง
                GetExactReportData(year, month);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadRevenueSummary: {ex.Message}");
                Label4.Text = "0";
                Label5.Text = "0";
                Label6.Text = "0";
            }
        }

        private void GetExactReportData(int year, int month)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(conn))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand("sp_CalculateMonthlyRevenue", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@Year", year);
                        command.Parameters.AddWithValue("@Month", month);

                        decimal totalRevenue = 0;
                        decimal totalDeposit = 0;

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            // อ่านรายได้รวม
                            if (reader.Read())
                            {
                                totalRevenue = reader.GetDecimal(0);
                            }

                            // อ่านยอดมัดจำ
                            if (reader.NextResult() && reader.Read())
                            {
                                totalDeposit = reader.GetDecimal(0);
                            }
                        }

                        // คำนวณยอดค้างชำระ
                        decimal balanceDue = totalRevenue - totalDeposit;

                        System.Diagnostics.Debug.WriteLine($"=== STORED PROCEDURE RESULT ===");
                        System.Diagnostics.Debug.WriteLine($"Total Revenue: {totalRevenue:#,##0}");
                        System.Diagnostics.Debug.WriteLine($"Total Deposit: {totalDeposit:#,##0}");
                        System.Diagnostics.Debug.WriteLine($"Balance Due: {balanceDue:#,##0}");

                        // อัพเดท UI
                        Label4.Text = totalRevenue.ToString("#,##0"); // ยอดรวมทั้งหมด
                        Label5.Text = totalDeposit.ToString("#,##0"); // ยอดเงินมัดจำ
                        Label6.Text = balanceDue.ToString("#,##0");   // ยอดค้างชำระ
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetExactReportData with SP: {ex.Message}");

                // Fallback to direct query
                GetExactReportDataFallback(year, month);
            }
        }

        private void GetExactReportDataFallback(int year, int month)
        {
            try
            {
                DateTime startDate = new DateTime(year, month, 1);
                DateTime endDate = new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59);

                // ใช้ INNER JOIN แทน RIGHT JOIN เพื่อความเสถียร
                string revenueQuery = @"
            SELECT 
                ISNULL(SUM(RA.Price * R.StayDays), 0) as TotalRevenue
            FROM [Reservation] R 
            INNER JOIN Reservation_Accommodation RA ON RA.Reservation_ID = R.ID 
            WHERE R.CheckinDate >= @StartDate AND R.CheckoutDate <= @EndDate
            AND R.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')";

                string depositQuery = @"
            SELECT 
                ISNULL(SUM(R.Deposit), 0) as TotalDeposit
            FROM [Reservation] R 
            WHERE R.CheckinDate >= @StartDate AND R.CheckoutDate <= @EndDate
            AND R.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')";

                using (SqlConnection connection = new SqlConnection(conn))
                {
                    connection.Open();

                    decimal totalRevenue = 0;
                    decimal totalDeposit = 0;

                    using (SqlCommand command = new SqlCommand(revenueQuery, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate);
                        totalRevenue = Convert.ToDecimal(command.ExecuteScalar());
                    }

                    using (SqlCommand command = new SqlCommand(depositQuery, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate);
                        totalDeposit = Convert.ToDecimal(command.ExecuteScalar());
                    }

                    decimal balanceDue = totalRevenue - totalDeposit;

                    System.Diagnostics.Debug.WriteLine($"=== FALLBACK CALCULATION ===");
                    System.Diagnostics.Debug.WriteLine($"Total Revenue: {totalRevenue:#,##0}");
                    System.Diagnostics.Debug.WriteLine($"Total Deposit: {totalDeposit:#,##0}");
                    System.Diagnostics.Debug.WriteLine($"Balance Due: {balanceDue:#,##0}");

                    Label4.Text = totalRevenue.ToString("#,##0");
                    Label5.Text = totalDeposit.ToString("#,##0");
                    Label6.Text = balanceDue.ToString("#,##0");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in fallback: {ex.Message}");
                Label4.Text = "0";
                Label5.Text = "0";
                Label6.Text = "0";
            }
        }

        private void DebugReservationData(int year, int month)
        {
            try
            {
                DateTime startDate = new DateTime(year, month, 1);
                DateTime endDate = new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59);

                string debugQuery = @"
            SELECT 
                R.ID,
                R.CheckinDate,
                R.CheckoutDate,
                R.TotalPrice,
                R.Deposit,
                R.Status,
                RA.Price as PricePerNight,
                R.StayDays,
                (RA.Price * R.StayDays) as RoomRevenue
            FROM [Reservation] R 
            INNER JOIN Reservation_Accommodation RA ON RA.Reservation_ID = R.ID 
            WHERE R.CheckinDate >= @StartDate AND R.CheckoutDate <= @EndDate
            AND R.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
            ORDER BY R.ID";

                using (SqlConnection connection = new SqlConnection(conn))
                {
                    using (SqlCommand command = new SqlCommand(debugQuery, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate);

                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            int count = 0;
                            while (reader.Read())
                            {
                                count++;
                                decimal deposit = reader["Deposit"] != DBNull.Value ? Convert.ToDecimal(reader["Deposit"]) : 0;
                                decimal roomRevenue = reader["RoomRevenue"] != DBNull.Value ? Convert.ToDecimal(reader["RoomRevenue"]) : 0;

                                System.Diagnostics.Debug.WriteLine($"Reservation {reader["ID"]}: " +
                                    $"Deposit={deposit}, " +
                                    $"RoomRevenue={roomRevenue}, " +
                                    $"Status={reader["Status"]}");
                            }
                            System.Diagnostics.Debug.WriteLine($"Total reservations found: {count}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Debug error: {ex.Message}");
            }
        }

        private void VerifyRevenueCalculation(int year, int month)
        {
            try
            {
                DateTime startDate = new DateTime(year, month, 1);
                DateTime endDate = new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59);

                using (SqlConnection connection = new SqlConnection(conn))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand("sp_VerifyRevenueCalculation", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            // อ่านรายได้รวม
                            if (reader.Read())
                            {
                                decimal totalRevenue = reader.GetDecimal(0);
                                System.Diagnostics.Debug.WriteLine($"Verified Total Revenue: {totalRevenue:#,##0}");
                            }

                            // อ่านยอดมัดจำทั้งหมด
                            if (reader.NextResult() && reader.Read())
                            {
                                decimal totalDeposit = reader.GetDecimal(0);
                                System.Diagnostics.Debug.WriteLine($"Verified Total Deposit: {totalDeposit:#,##0}");
                            }

                            // อ่านยอดรับมาแล้ว
                            if (reader.NextResult() && reader.Read())
                            {
                                decimal totalReceived = reader.GetDecimal(0);
                                System.Diagnostics.Debug.WriteLine($"Verified Total Received: {totalReceived:#,##0}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in verification: {ex.Message}");
            }
        }
        private DataTable GetReservationData(int year, int month)
        {
            // ใช้ Stored Procedure แทน Query ยาว
            string query = "sp_GetReservationDetails";
            DataTable dt = new DataTable();

            using (SqlConnection connection = new SqlConnection(conn))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@Year", year);
                    command.Parameters.AddWithValue("@Month", month);

                    try
                    {
                        connection.Open();
                        using (SqlDataAdapter da = new SqlDataAdapter(command))
                        {
                            da.Fill(dt);
                        }

                        System.Diagnostics.Debug.WriteLine($"Found {dt.Rows.Count} reservations for {year}-{month}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error using SP, fallback to query: {ex.Message}");
                        // Fallback to optimized query
                        dt = GetOptimizedReservationData(year, month);
                    }
                }
            }

            return dt;
        }

        private DataTable GetOptimizedReservationData(int year, int month)
        {
            DateTime startDate = new DateTime(year, month, 1);
            DateTime endDate = new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59);

            string query = @"
        SELECT 
            R.ID,
            R.CheckinDate,
            R.CheckoutDate,
            R.TotalPrice,
            R.Deposit,
            R.Status,
            C.Name,
            C.NickName,
            C.MobilePhone,
            RA.Accommodation_ID,
            RA.Amount as PeopleStay,
            A.AccomName,
            R.StayDays,
            RA.Price as PricePerNight
        FROM Reservation R
        INNER JOIN Customer C ON R.Customer_MobilePhone = C.MobilePhone
        INNER JOIN Reservation_Accommodation RA ON R.ID = RA.Reservation_ID
        INNER JOIN Accommodation A ON RA.Accommodation_ID = A.ID
        WHERE 
            (R.CheckinDate <= @EndDate AND R.CheckoutDate >= @StartDate)
            AND R.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
        ORDER BY R.CheckinDate, A.AccomName";

            DataTable dt = new DataTable();

            using (SqlConnection connection = new SqlConnection(conn))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    connection.Open();
                    using (SqlDataAdapter da = new SqlDataAdapter(command))
                    {
                        da.Fill(dt);
                    }
                }
            }

            return dt;
        }

        private void LoadReservationGridView(int year, int month)
        {
            try
            {
                // โหลดข้อมูลห้องพัก
                DataTable dtAccom = code.DatabaseQuery(conn, "SELECT * FROM Accommodation WHERE Status = 1");

                if (dtAccom.Rows.Count == 0)
                {
                    ShowErrorMessage("ไม่พบข้อมูลห้องพัก");
                    return;
                }

                // สร้าง DataTable สำหรับแสดงผล
                DataTable dtShow = CreateReservationDataTable(dtAccom);

                // โหลดข้อมูลการจอง
                DataTable dtReservation = GetReservationData(year, month);

                // เติมข้อมูลลงในตาราง
                FillReservationData(dtShow, dtAccom, dtReservation, year, month);

                // แสดงผลใน GridView
                DisplayReservationGrid(dtShow, dtAccom);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadReservationGridView: {ex.Message}");
                ShowErrorMessage($"เกิดข้อผิดพลาดในการโหลดข้อมูล: {ex.Message}");
            }
        }

        private DataTable CreateReservationDataTable(DataTable dtAccom)
        {
            DataTable dtShow = new DataTable();
            dtShow.Columns.Add("Date", typeof(string));

            for (int i = 0; i < dtAccom.Rows.Count; i++)
            {
                dtShow.Columns.Add(dtAccom.Rows[i]["AccomName"].ToString(), typeof(string));
            }

            return dtShow;
        }

       
        private void FillReservationData(DataTable dtShow, DataTable dtAccom, DataTable dtReservation, int year, int month)
        {
            int daysInMonth = DateTime.DaysInMonth(year, month);

            // สร้าง Dictionary เพื่อเก็บข้อมูลการจองที่จัดกลุ่มแล้ว
            var reservationDict = new Dictionary<string, List<DataRow>>();

            // จัดกลุ่มการจองตามวันที่และห้องพัก
            foreach (DataRow reservation in dtReservation.Rows)
            {
                DateTime checkinDate = Convert.ToDateTime(reservation["CheckinDate"]);
                DateTime checkoutDate = Convert.ToDateTime(reservation["CheckoutDate"]);
                string accomId = reservation["Accommodation_ID"].ToString();
                string reservationId = reservation["ID"].ToString();

                // เพิ่มการจองนี้ในทุกวันที่เกี่ยวข้อง
                for (DateTime date = checkinDate; date < checkoutDate; date = date.AddDays(1))
                {
                    if (date.Year == year && date.Month == month)
                    {
                        string key = $"{date:yyyy-MM-dd}_{accomId}";

                        if (!reservationDict.ContainsKey(key))
                        {
                            reservationDict[key] = new List<DataRow>();
                        }

                        // ตรวจสอบว่าไม่มีการจองซ้ำ
                        if (!reservationDict[key].Any(r => r["ID"].ToString() == reservationId))
                        {
                            reservationDict[key].Add(reservation);
                        }
                    }
                }
            }

            for (int day = 0; day < daysInMonth; day++)
            {
                DataRow newRow = dtShow.NewRow();
                DateTime currentDate = new DateTime(year, month, day + 1);
                newRow["Date"] = currentDate.ToString("dd/MM/yyyy dddd");

                // เติมข้อมูลการจองสำหรับแต่ละห้อง
                for (int accomIndex = 0; accomIndex < dtAccom.Rows.Count; accomIndex++)
                {
                    string accomName = dtAccom.Rows[accomIndex]["AccomName"].ToString();
                    string accomId = dtAccom.Rows[accomIndex]["ID"].ToString();

                    StringBuilder cellContent = new StringBuilder();
                    string key = $"{currentDate:yyyy-MM-dd}_{accomId}";

                    if (reservationDict.ContainsKey(key))
                    {
                        var reservationsForDay = reservationDict[key];

                        foreach (DataRow reservation in reservationsForDay)
                        {
                            string customerInfo = FormatCustomerInfo(reservation);
                            cellContent.Append(customerInfo);
                        }
                    }

                    newRow[accomName] = cellContent.Length > 0 ? cellContent.ToString() : "<div class='empty-cell'>-</div>";
                }

                dtShow.Rows.Add(newRow);
            }
        }

        private string FormatCustomerInfo(DataRow reservation)
        {
            StringBuilder info = new StringBuilder();

            try
            {
                string name = reservation["Name"]?.ToString() ?? "";
                string nickname = reservation["NickName"]?.ToString() ?? "";
                string phone = reservation["MobilePhone"]?.ToString() ?? "";
                int peopleStay = reservation["PeopleStay"] != DBNull.Value ? Convert.ToInt32(reservation["PeopleStay"]) : 0;
                decimal pricePerNight = reservation["PricePerNight"] != DBNull.Value ? Convert.ToDecimal(reservation["PricePerNight"]) : 0;
                int stayDays = reservation["StayDays"] != DBNull.Value ? Convert.ToInt32(reservation["StayDays"]) : 0;
                decimal totalPrice = pricePerNight * stayDays;
                decimal deposit = reservation["Deposit"] != DBNull.Value ? Convert.ToDecimal(reservation["Deposit"]) : 0;
                string status = reservation["Status"]?.ToString() ?? "";

                info.Append($"<div class='customer-info'>");
                info.Append($"<div class='customer-name'>{name} ({nickname})</div>");
                info.Append($"<div class='customer-details'>เบอร์: {phone} | {peopleStay} คน | {stayDays} คืน</div>");
                info.Append($"<div class='payment-info'>คืนละ: {pricePerNight:#,##0} | รวม: {totalPrice:#,##0} | รับมาแล้ว: {deposit:#,##0}</div>");
                info.Append($"<div class='status-info'>สถานะ: {GetStatusText(status)}</div>");
                info.Append("</div>");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in FormatCustomerInfo: {ex.Message}");
                info.Append($"<div class='customer-info text-danger'>Error: {ex.Message}</div>");
            }

            return info.ToString();
        }

        private string GetStatusText(string status)
        {
            switch (status.ToLower())
            {
                case "confirmed": return "ยืนยันแล้ว";
                case "checkedin": return "เข้าพักแล้ว";
                case "completed": return "เสร็จสิ้น";
                case "cancelled": return "ยกเลิก";
                case "pending": return "รอการยืนยัน";
                default: return status;
            }
        }

        private void DisplayReservationGrid(DataTable dtShow, DataTable dtAccom)
        {
            try
            {
                // ล้างคอลัมน์เดิม
                GridView1.Columns.Clear();

                // เพิ่มคอลัมน์วันที่
                BoundField dateField = new BoundField();
                dateField.DataField = "Date";
                dateField.HeaderText = "วันที่";
                dateField.HeaderStyle.CssClass = "header-center";
                dateField.ItemStyle.CssClass = "date-column";
                dateField.ItemStyle.Width = Unit.Pixel(120);
                dateField.ItemStyle.Font.Bold = true;
                GridView1.Columns.Add(dateField);

                // เพิ่มคอลัมน์ห้องพัก
                for (int i = 0; i < dtAccom.Rows.Count; i++)
                {
                    BoundField accomField = new BoundField();
                    string accomName = dtAccom.Rows[i]["AccomName"].ToString();
                    accomField.DataField = accomName;
                    accomField.HeaderText = accomName;
                    accomField.HeaderStyle.CssClass = "header-center";
                    accomField.HeaderStyle.Font.Bold = true;
                    accomField.HtmlEncode = false;
                    accomField.ItemStyle.CssClass = "accommodation-cell";
                    accomField.ItemStyle.Width = Unit.Pixel(250);
                    accomField.ItemStyle.VerticalAlign = VerticalAlign.Top;
                    GridView1.Columns.Add(accomField);
                }

                GridView1.DataSource = dtShow;
                GridView1.DataBind();

                // แสดงจำนวนแถวที่พบ
                System.Diagnostics.Debug.WriteLine($"Displaying {dtShow.Rows.Count} days of data");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DisplayReservationGrid: {ex.Message}");
                throw;
            }
        }

        protected void GridView1_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                // ตั้งค่าการแสดงผลสำหรับแต่ละเซลล์
                for (int i = 1; i < e.Row.Cells.Count; i++)
                {
                    string cellText = e.Row.Cells[i].Text;
                    if (cellText.Contains("empty-cell") || string.IsNullOrWhiteSpace(cellText.Replace("-", "")))
                    {
                        e.Row.Cells[i].CssClass = "empty-cell";
                    }
                    else if (cellText.Contains("customer-info"))
                    {
                        e.Row.Cells[i].CssClass = "accommodation-cell";
                    }
                }

                // ตั้งค่าสีแถวสลับ
                if (e.Row.RowIndex % 2 == 0)
                {
                    e.Row.CssClass = "even-row";
                }
                else
                {
                    e.Row.CssClass = "odd-row";
                }
            }
            else if (e.Row.RowType == DataControlRowType.Header)
            {
                // ตั้งค่า header
                e.Row.CssClass = "grid-header";
            }
        }

        private void ShowInfoMessage(string message)
        {
            ScriptManager.RegisterStartupScript(this, this.GetType(), "showInfo",
                $"alert('{message}');", true);
        }

        private void ShowErrorMessage(string message)
        {
            ScriptManager.RegisterStartupScript(this, this.GetType(), "showError",
                $"alert('{message}');", true);
        }
    }
}