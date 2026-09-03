using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Helpers;
using System.Linq;
using System.Text;
using System.Threading;

namespace Take_Time_BangPhra.Admin.Report
{
    public partial class ReportMain : System.Web.UI.Page
    {
        private readonly DatabaseHelper databaseHelper = new DatabaseHelper();
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.MgtReport)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (!IsPostBack)
            {
                InitializePage();
            }
        }

        private void InitializePage()
        {
            try
            {
                // Check user permission
                if (Session["permission"]?.ToString() != "True")
                {
                    Response.Redirect("~/Default", true);
                    return;
                }

                // Set default dates
                txtStartDate.Text = DateTime.Now.ToString("yyyy-MM-01");
                SetEndOfMonthDate();

                // Display current user info
                DisplayUserInfo();

                HideAlert();
            }
            catch (Exception ex)
            {
                LogError(ex);
                ShowAlert("danger", "เกิดข้อผิดพลาดในการโหลดหน้า: " + ex.Message);
            }
        }

        protected void ddlReportType_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                ClearAllSections();
                HideAlert();

                if (string.IsNullOrEmpty(ddlReportType.SelectedValue))
                {
                    return;
                }

                // Check authorization for specific reports
                if (!IsUserAuthorizedForReport())
                {
                    ShowAlert("warning", "คุณไม่มีสิทธิ์เข้าถึงรายงานนี้");
                    return;
                }

                ShowDateRangeSection();
                UpdateReportTitle();

                // Load initial data for some reports
                if (ddlReportType.SelectedValue == "1")
                {
                    LoadLatestReservations();
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ShowAlert("danger", "เกิดข้อผิดพลาดในการโหลดรายงาน: " + ex.Message);
            }
        }

        protected void btnGenerateReport_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidateInputs())
                {
                    return;
                }

                GenerateSelectedReport();
                btnExport.Visible = true;
                btnPrint.Visible = true;
                UpdateReportTitle();
                ShowAlert("success", "สร้างรายงานสำเร็จแล้ว");
            }
            catch (Exception ex)
            {
                LogError(ex);
                ShowAlert("danger", "เกิดข้อผิดพลาดในการสร้างรายงาน: " + ex.Message);
            }
        }

        protected void txtStartDate_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtStartDate.Text) && txtStartDate.Text.Length >= 7)
            {
                SetEndOfMonthDate();
                UpdateDateInfo();
            }
        }

        protected void btnExport_Click(object sender, EventArgs e)
        {
            try
            {

                ExportToExcel();

                // ไม่ต้องแสดง Alert ที่นี่เพราะอาจรบกวนการดาวน์โหลดไฟล์
                // ShowAlert("success", "ส่งออกไฟล์ CSV สำเร็จแล้ว");

                updatePanel.Update();
            }
            catch (Exception ex)
            {
                LogError(ex);
                ShowAlert("danger", "เกิดข้อผิดพลาดในการส่งออกไฟล์: " + ex.Message);
                updatePanel.Update();
            }
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            ClearAllSections();
            HideAlert();
            ShowAlert("info", "ล้างข้อมูลรายงานแล้ว");
        }

        protected void gvReport_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                FormatGridViewRow(e.Row);
            }
            else if (e.Row.RowType == DataControlRowType.Header)
            {
                TranslateColumnHeaders(e.Row);
            }
            else if (e.Row.RowType == DataControlRowType.EmptyDataRow)
            {
                e.Row.Cells[0].HorizontalAlign = HorizontalAlign.Center;
            }
        }

        #region Report Generation Methods

        private void GenerateSelectedReport()
        {
            
            switch (ddlReportType.SelectedValue)
            {
                case "1":
                    GenerateLatestReservationsReport();
                    break;
                case "2":
                    GenerateEquipmentRentalReport();
                    break;
                case "3":
                    GenerateRoomReservationReport();
                    break;
                case "4":
                    GenerateOccupancyRateReport();
                    break;
                case "5":
                    GenerateSalesDetailReport();
                    break;
                case "6":
                    GenerateSalesSummaryReport();
                    break;
                case "7":
                    GenerateAffiliateReservationReport();
                    break;
                default:
                    throw new ArgumentException("ประเภทรายงานไม่ถูกต้อง");
            }
        }

        private void GenerateLatestReservationsReport()
        {
            string query = @"
        SELECT 
            R.ID AS 'เลขที่การจอง',
            STUFF((
                SELECT ', ' + A.AccomName
                FROM [Reservation_Accommodation] RA
                INNER JOIN [Accommodation] A ON A.ID = RA.Accommodation_ID
                WHERE RA.Reservation_ID = R.ID
                FOR XML PATH('')), 1, 2, '') AS 'ประเภทที่พัก',
            FORMAT(R.Created_Date, 'dd/MM/yyyy HH:mm') AS 'วันที่สร้าง',
            C.Name AS 'ชื่อลูกค้า',
            C.NickName AS 'ชื่อเล่น',
            R.Customer_MobilePhone AS 'เบอร์โทร',
            FORMAT(R.CheckinDate, 'dd MMMM yyyy') AS 'เช็คอิน',
            FORMAT(R.CheckoutDate, 'dd MMMM yyyy') AS 'เช็คเอาท์',
            R.StayDays AS 'จำนวนคืน',
            R.Status AS 'สถานะ',
            FORMAT(R.TotalPrice, 'N0') AS 'ราคารวม',
            FORMAT(R.Deposit, 'N0') AS 'มัดจำ',
            R.Remark AS 'หมายเหตุ',
            R.Reserve_By AS 'จองโดย'
        FROM [Reservation] R
        INNER JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone 
        WHERE R.Created_Date >= @StartDate AND R.Created_Date <= @EndDate
        ORDER BY R.ID DESC";

            var parameters = new Dictionary<string, object>
    {
        { "@StartDate", $"{txtStartDate.Text} 00:00:01" },
        { "@EndDate", $"{txtEndDate.Text} 23:59:59" }
    };

            BindReportData(query, parameters);
        }

        private void GenerateEquipmentRentalReport()
        {
            string query = @"
                SELECT 
                    I.ItemName AS 'ชื่ออุปกรณ์',
                    SUM(RI.Amount * R.StayDays) AS 'จำนวนที่เช่า',
                    FORMAT(SUM(RI.Price * R.StayDays), 'N0') AS 'ราคารวม'
                FROM [Reservation] R 
                RIGHT JOIN Reservation_Items RI ON RI.Reservation_ID = R.ID 
                INNER JOIN Items I ON I.ID = RI.Items_ID 
                WHERE R.CheckinDate >= @StartDate AND R.CheckoutDate <= @EndDate
                AND R.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                GROUP BY I.ID, I.ItemName
                ORDER BY SUM(RI.Price * R.StayDays) DESC";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDate", $"{txtStartDate.Text} 00:00:01" },
                { "@EndDate", $"{txtEndDate.Text} 23:59:59" }
            };

            var dt = databaseHelper.ExecuteQueryWithParams(query, parameters);
            Session["dtReport"] = dt;
            BindGridViewWithSummary(dt);

            decimal totalPrice = CalculateTotalFromDataTable(dt, "ราคารวม");
            ShowSummary($"ยอดรวมรายได้: {totalPrice:N0} บาท");
        }

        private void GenerateRoomReservationReport()
        {
            string query = @"
        SELECT 
            A.AccomName AS 'ประเภทที่พัก',
            SUM(R.StayDays) AS 'จำนวนคืน',
            SUM(RA.Amount) AS 'จำนวนผู้พัก',
            FORMAT(SUM(RA.Price * R.StayDays), 'N0') AS 'รายได้'
        FROM [Reservation] R 
        RIGHT JOIN Reservation_Accommodation RA ON RA.Reservation_ID = R.ID 
        INNER JOIN Accommodation A ON RA.Accommodation_ID = A.ID 
        WHERE R.CheckinDate >= @StartDate AND R.CheckoutDate <= @EndDate
        AND R.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
        GROUP BY A.ID, A.AccomName
        ORDER BY SUM(RA.Price * R.StayDays) DESC";

            var parameters = new Dictionary<string, object>
    {
        { "@StartDate", $"{txtStartDate.Text} 00:00:01" },
        { "@EndDate", $"{txtEndDate.Text} 23:59:59" }
    };

            var dt = databaseHelper.ExecuteQueryWithParams(query, parameters);
            Session["dtReport"] = dt;
            BindGridViewWithSummary(dt);

            // ใช้ method ใหม่ที่แสดงครบทุกประเภท
            CalculateAccommodationSummaryWithVilla(dt);
        }

        private void CalculateAccommodationSummaryWithVilla(DataTable dt)
        {
            decimal domeTotal = 0, roomTotal = 0, tentTotal = 0, campingTentTotal = 0, cabinTotal = 0, villaTotal = 0;
            int totalNights = 0, totalGuests = 0;

            foreach (DataRow row in dt.Rows)
            {
                string accomName = row["ประเภทที่พัก"].ToString();
                decimal price = Convert.ToDecimal(row["รายได้"].ToString().Replace(",", ""));
                int nights = Convert.ToInt32(row["จำนวนคืน"]);
                int guests = Convert.ToInt32(row["จำนวนผู้พัก"]);

                totalNights += nights;
                totalGuests += guests;

                // จัดกลุ่มตามประเภทห้องพัก
                if (accomName.Contains("กระโจม") || accomName.Contains("โดม"))
                    domeTotal += price;
                else if (accomName.Contains("กระจก"))
                    roomTotal += price;
                else if (accomName.Contains("วิลล่า") || accomName.Contains("Villa") || accomName.Contains("villa"))
                    villaTotal += price;
                else if (accomName.Contains("กางเต็นท์") || accomName.Contains("ไม่ค้างคืน"))
                    tentTotal += price;
                else if (accomName.Contains("แคมป์ปิ้ง"))
                    campingTentTotal += price;
                else if (accomName.Contains("เคบิ้น") || accomName.Contains("Cabin"))
                    cabinTotal += price;
                else
                    tentTotal += price; // default
            }

            decimal totalRevenue = domeTotal + roomTotal + villaTotal + tentTotal + campingTentTotal + cabinTotal;

            // Debug information
            System.Diagnostics.Debug.WriteLine($"=== ROOM REVENUE BREAKDOWN ===");
            System.Diagnostics.Debug.WriteLine($"Dome: {domeTotal:#,##0}");
            System.Diagnostics.Debug.WriteLine($"Room: {roomTotal:#,##0}");
            System.Diagnostics.Debug.WriteLine($"Villa: {villaTotal:#,##0}");
            System.Diagnostics.Debug.WriteLine($"Tent: {tentTotal:#,##0}");
            System.Diagnostics.Debug.WriteLine($"Camping Tent: {campingTentTotal:#,##0}");
            System.Diagnostics.Debug.WriteLine($"Cabin: {cabinTotal:#,##0}");
            System.Diagnostics.Debug.WriteLine($"TOTAL: {totalRevenue:#,##0}");

            // ตรวจสอบความถูกต้อง
            decimal calculatedTotal = domeTotal + roomTotal + villaTotal + tentTotal + campingTentTotal + cabinTotal;
            if (Math.Abs(calculatedTotal - totalRevenue) > 1)
            {
                System.Diagnostics.Debug.WriteLine($"WARNING: Totals don't match! Calculated: {calculatedTotal}, Reported: {totalRevenue}");
                totalRevenue = calculatedTotal;
            }

            // แสดงสรุปแบบใหม่ที่ครบทุกประเภท
            ShowCompleteAccommodationSummary(totalRevenue, domeTotal, roomTotal, villaTotal, tentTotal, campingTentTotal, cabinTotal);
        }

        private void ShowCompleteAccommodationSummary(decimal totalRevenue, decimal domeTotal, decimal roomTotal,
                                            decimal villaTotal, decimal tentTotal, decimal campingTentTotal, decimal cabinTotal)
        {
            summarySection.Visible = true;

            // แสดงยอดรวมใน header
            lblTotalRevenue.Text = $"ยอดรวม: {totalRevenue:N0} บาท";

            // สร้างรายการประเภทที่พักทั้งหมดพร้อมไอคอน - แก้ไขประเภทข้อมูล
            var accommodationTypes = new List<(Label label, decimal amount, string icon)>
    {
        (lblSummary1, domeTotal, "🏕️ กระโจม"),
        (lblSummary2, roomTotal, "🏠 ห้องกระจก"),
        (lblSummary3, villaTotal, "🏡 วิลล่า"),
        (lblSummary4, tentTotal, "⛺ กางเต็นท์"),
        (lblSummary5, campingTentTotal, "🎪 แคมป์ปิ้งเต็นท์"),
        (lblSummary6, cabinTotal, "🛖 เคบิ้น")
    };

            // แสดงทุกประเภท
            foreach (var item in accommodationTypes)
            {
                if (item.amount > 0)
                {
                    item.label.Text = $"{item.icon}<br><span class='summary-amount'>{item.amount:N0} บาท</span>";
                }
                else
                {
                    item.label.Text = $"{item.icon}<br><span class='text-muted'>-</span>";
                }
            }

            // ตรวจสอบและแสดงผลรวม
            decimal sumAllTypes = domeTotal + roomTotal + villaTotal + tentTotal + campingTentTotal + cabinTotal;

            // เพิ่มข้อมูล debug ใน console
            System.Diagnostics.Debug.WriteLine($"=== VERIFICATION ===");
            System.Diagnostics.Debug.WriteLine($"Sum of all types: {sumAllTypes:N0}");
            System.Diagnostics.Debug.WriteLine($"Reported total: {totalRevenue:N0}");
            System.Diagnostics.Debug.WriteLine($"Difference: {Math.Abs(sumAllTypes - totalRevenue):N0}");
        }


        private void ShowAccommodationSummaryOptimal(decimal totalRevenue, decimal domeTotal, decimal roomTotal,
                                                    decimal villaTotal, decimal tentTotal, decimal campingTentTotal, decimal cabinTotal)
        {
            summarySection.Visible = true;

            // วิธีที่เหมาะสม: แสดงประเภทหลักและรวมประเภทที่มียอดน้อย
            var summaries = new List<(decimal amount, string name, string icon)>
    {
        (domeTotal, "กระโจม", "🏕️"),
        (roomTotal, "ห้องกระจก", "🏠"),
        (villaTotal, "วิลล่า", "🏡"),
        (tentTotal, "กางเต็นท์", "⛺"),
        (campingTentTotal, "แคมป์ปิ้งเต็นท์", "🎪"),
        (cabinTotal, "เคบิ้น", "🛖")
    };

            // เรียงลำดับตามยอดรายได้ (มากไปน้อย)
            var sortedSummaries = summaries.OrderByDescending(x => x.amount).ToList();

            // แสดง 5 อันดับแรก + ยอดรวม (รวมเป็น 6 ช่อง)
            lblSummary1.Text = $"💰 ยอดรวม: {totalRevenue:N0} บาท";

            for (int i = 0; i < Math.Min(5, sortedSummaries.Count); i++)
            {
                var summary = sortedSummaries[i];
                if (summary.amount > 0) // แสดงเฉพาะที่มีรายได้
                {
                    switch (i)
                    {
                        case 0: lblSummary2.Text = $"{summary.icon} {summary.name}: {summary.amount:N0} บาท"; break;
                        case 1: lblSummary3.Text = $"{summary.icon} {summary.name}: {summary.amount:N0} บาท"; break;
                        case 2: lblSummary4.Text = $"{summary.icon} {summary.name}: {summary.amount:N0} บาท"; break;
                        case 3: lblSummary5.Text = $"{summary.icon} {summary.name}: {summary.amount:N0} บาท"; break;
                        case 4: lblSummary6.Text = $"{summary.icon} {summary.name}: {summary.amount:N0} บาท"; break;
                    }
                }
            }

            // ถ้ามีประเภทที่ 6 ที่มียอดแต่ไม่ได้แสดง (เพราะแสดงแค่ 5 อันดับแรก)
            if (sortedSummaries.Count > 5 && sortedSummaries[5].amount > 0)
            {
                // รวมยอดที่เหลือทั้งหมด
                decimal otherTotal = sortedSummaries.Skip(5).Sum(x => x.amount);
                lblSummary6.Text = $"📊 อื่นๆ: {otherTotal:N0} บาท";
            }
        }

        private void GenerateOccupancyRateReport()
        {
            string query = @"
                DECLARE @StartDate DATE = @StartDateParam;
                DECLARE @EndDate DATE = @EndDateParam;

                WITH TotalDays AS (
                    SELECT 
                        A.ID AS Accommodation_ID,
                        A.AccomName,
                        DATEDIFF(DAY, @StartDate, @EndDate) + 1 AS TotalPossibleDays
                    FROM [Accommodation] A
                    WHERE A.Status = 1
                ),
                OccupiedDays AS (
                    SELECT 
                        RA.Accommodation_ID,
                        COUNT(DISTINCT DR.Date) AS OccupiedDaysCount
                    FROM (
                        SELECT DATEADD(DAY, number, @StartDate) AS Date
                        FROM master.dbo.spt_values
                        WHERE type = 'P' 
                        AND number <= DATEDIFF(DAY, @StartDate, @EndDate)
                    ) DR
                    JOIN [Reservation_Accommodation] RA ON 1=1
                    JOIN [Reservation] R ON RA.Reservation_ID = R.ID
                    WHERE DR.Date BETWEEN R.CheckinDate AND DATEADD(DAY, -1, R.CheckoutDate)
                      AND R.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                      AND DR.Date BETWEEN @StartDate AND @EndDate
                    GROUP BY RA.Accommodation_ID
                )
                SELECT 
                    T.AccomName AS 'ประเภทที่พัก',
                    T.TotalPossibleDays AS 'จำนวนวันทั้งหมด',
                    ISNULL(O.OccupiedDaysCount, 0) AS 'จำนวนวันที่มีการจอง',
                    FORMAT(
                        CASE 
                            WHEN T.TotalPossibleDays > 0 
                            THEN CAST(ISNULL(O.OccupiedDaysCount, 0) AS FLOAT) / T.TotalPossibleDays * 100 
                            ELSE 0 
                        END, 'N2'
                    ) AS 'อัตราการจอง (%)'
                FROM TotalDays T
                LEFT JOIN OccupiedDays O ON T.Accommodation_ID = O.Accommodation_ID
                ORDER BY 
                    CASE 
                        WHEN T.TotalPossibleDays > 0 
                        THEN CAST(ISNULL(O.OccupiedDaysCount, 0) AS FLOAT) / T.TotalPossibleDays * 100 
                        ELSE 0 
                    END DESC, T.AccomName";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDateParam", txtStartDate.Text },
                { "@EndDateParam", txtEndDate.Text }
            };

            BindReportData(query, parameters);
        }

        private void GenerateSalesDetailReport()
        {
            string query = @"
                WITH ProductCost AS (
                    SELECT 
                        Product_ID,
                        PricePerUnit AS CostPrice,
                        ROW_NUMBER() OVER (PARTITION BY Product_ID ORDER BY DateTime_In DESC) AS RowNum
                    FROM [Product_In]
                )
                SELECT 
                    PO.ID AS 'เลขที่รายการ',
                    FORMAT(PO.DateTime_Out, 'dd/MM/yyyy HH:mm') AS 'วันที่ขาย',
                    P.Barcode AS 'บาร์โค้ด',
                    P.Product_Name AS 'ชื่อสินค้า',
                    CAT.Category_Name AS 'หมวดหมู่',
                    PO.Amount AS 'จำนวน',
                    FORMAT(PCOST.CostPrice, 'N2') AS 'ต้นทุน/หน่วย',
                    FORMAT(P.Sell_Price, 'N2') AS 'ราคาขาย/หน่วย',
                    FORMAT((PO.Amount * PCOST.CostPrice), 'N2') AS 'ต้นทุนรวม',
                    FORMAT((PO.Amount * P.Sell_Price), 'N2') AS 'รายได้',
                    FORMAT((PO.Amount * P.Sell_Price) - (PO.Amount * PCOST.CostPrice), 'N2') AS 'กำไร',
                    FORMAT(
                        CASE 
                            WHEN (PO.Amount * PCOST.CostPrice) = 0 THEN 0 
                            ELSE (((PO.Amount * P.Sell_Price) - (PO.Amount * PCOST.CostPrice)) / 
                                  (PO.Amount * PCOST.CostPrice)) * 100 
                        END, 'N2'
                    ) AS 'อัตรากำไร (%)',
                    PO.Account_Receipt_ID AS 'เลขที่ใบเสร็จ',
                    PO.Remark AS 'หมายเหตุ'
                FROM [Product_Out] PO
                INNER JOIN [Product] P ON PO.Product_ID = P.ID
                LEFT JOIN [Product_Category] CAT ON P.Category_ID = CAT.ID
                LEFT JOIN ProductCost PCOST ON PO.Product_ID = PCOST.Product_ID AND PCOST.RowNum = 1
                WHERE PO.DateTime_Out IS NOT NULL
                  AND P.Status = 1
                  AND (CAT.Status = 1 OR CAT.Status IS NULL)
                  AND PO.DateTime_Out >= @StartDate AND PO.DateTime_Out <= @EndDate
                ORDER BY PO.DateTime_Out DESC";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDate", $"{txtStartDate.Text} 00:00:01" },
                { "@EndDate", $"{txtEndDate.Text} 23:59:59" }
            };

            var dt = databaseHelper.ExecuteQueryWithParams(query, parameters);
            Session["dtReport"] = dt;
            BindGridViewWithSummary(dt);

            decimal totalRevenue = CalculateTotalFromDataTable(dt, "รายได้");
            decimal totalProfit = CalculateTotalFromDataTable(dt, "กำไร");

            ShowSummary(
                $"รายได้รวม: {totalRevenue:N2} บาท",
                $"กำไรรวม: {totalProfit:N2} บาท",
                $"อัตรากำไรเฉลี่ย: {((totalRevenue > 0) ? (totalProfit / (totalRevenue - totalProfit) * 100) : 0):N2}%"
            );
        }

        private void GenerateSalesSummaryReport()
        {
            string query = @"
                DECLARE @StartDate DATE = @StartDateParam;
                DECLARE @EndDate DATE = @EndDateParam;

                SELECT 
                    P.ID AS 'รหัสสินค้า',
                    P.Barcode AS 'บาร์โค้ด',
                    P.Product_Name AS 'ชื่อสินค้า',
                    PC.Category_Name AS 'หมวดหมู่',
                    FORMAT(P.Sell_Price, 'N2') AS 'ราคาขายปัจจุบัน',
                    FORMAT(
                        (SELECT TOP 1 PricePerUnit 
                         FROM [Product_In] 
                         WHERE Product_ID = P.ID 
                         ORDER BY DateTime_In DESC), 'N2'
                    ) AS 'ต้นทุนล่าสุด',
                    (SELECT ISNULL(SUM(Amount), 0) FROM [Product_In] WHERE Product_ID = P.ID) -
                    ISNULL((SELECT ISNULL(SUM(Amount), 0) FROM [Product_Out] 
                            WHERE Product_ID = P.ID 
                            AND DateTime_Out BETWEEN @StartDate AND @EndDate), 0) AS 'สต็อกคงเหลือ',
                    ISNULL((SELECT ISNULL(SUM(Amount), 0) FROM [Product_Out] 
                            WHERE Product_ID = P.ID 
                            AND DateTime_Out BETWEEN @StartDate AND @EndDate), 0) AS 'จำนวนที่ขาย',
                    FORMAT(
                        ISNULL((SELECT ISNULL(SUM(Amount), 0) FROM [Product_Out] 
                                WHERE Product_ID = P.ID 
                                AND DateTime_Out BETWEEN @StartDate AND @EndDate), 0) * P.Sell_Price, 'N2'
                    ) AS 'รายได้รวม',
                    FORMAT(
                        ISNULL((SELECT ISNULL(SUM(Amount), 0) FROM [Product_Out] 
                                WHERE Product_ID = P.ID 
                                AND DateTime_Out BETWEEN @StartDate AND @EndDate), 0) * 
                        (P.Sell_Price - ISNULL((SELECT TOP 1 PricePerUnit FROM [Product_In] 
                                         WHERE Product_ID = P.ID ORDER BY DateTime_In DESC), 0)), 'N2'
                    ) AS 'กำไรรวม',
                    FORMAT(
                        CASE 
                            WHEN ISNULL((SELECT TOP 1 PricePerUnit FROM [Product_In] 
                                  WHERE Product_ID = P.ID ORDER BY DateTime_In DESC), 0) = 0 THEN 0
                            ELSE ((P.Sell_Price - ISNULL((SELECT TOP 1 PricePerUnit FROM [Product_In] 
                                                   WHERE Product_ID = P.ID ORDER BY DateTime_In DESC), 0)) / 
                                 ISNULL((SELECT TOP 1 PricePerUnit FROM [Product_In] 
                                  WHERE Product_ID = P.ID ORDER BY DateTime_In DESC), 0)) * 100
                        END, 'N2'
                    ) AS 'อัตรากำไร (%)',
                    FORMAT(
                        (SELECT MAX(DateTime_Out) FROM [Product_Out] 
                         WHERE Product_ID = P.ID 
                         AND DateTime_Out BETWEEN @StartDate AND @EndDate), 'dd/MM/yyyy HH:mm'
                    ) AS 'ขายล่าสุด'
                FROM [Product] P
                LEFT JOIN [Product_Category] PC ON P.Category_ID = PC.ID
                WHERE P.Status = 'True'
                  AND EXISTS (
                      SELECT 1 FROM [Product_Out] PO 
                      WHERE PO.Product_ID = P.ID 
                      AND PO.DateTime_Out BETWEEN @StartDate AND @EndDate
                  )
                ORDER BY P.Product_Name";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDateParam", txtStartDate.Text },
                { "@EndDateParam", txtEndDate.Text }
            };

            var dt = databaseHelper.ExecuteQueryWithParams(query, parameters);
            Session["dtReport"] = dt;
            BindGridViewWithSummary(dt);
            CalculateSalesSummary(dt);
        }

        private void GenerateAffiliateReservationReport()
        {
            string query = @"
                SELECT 
                    ID AS 'เลขที่',
                    Affiliate_Name AS 'ชื่อ Affiliate',
                    Customer_Name AS 'ชื่อลูกค้า',
                    FORMAT(StayDate, 'dd/MM/yyyy') AS 'วันที่พัก',
                    Accommodation_Type AS 'ประเภทที่พัก',
                    Amount AS 'จำนวน',
                    FORMAT(Price, 'N0') AS 'ราคา',
                    Status AS 'สถานะ',
                    Remark AS 'หมายเหตุ'
                FROM [Affiliate_Reservation] 
                WHERE StayDate >= @StartDate AND StayDate <= @EndDate
                ORDER BY StayDate DESC";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDate", txtStartDate.Text },
                { "@EndDate", txtEndDate.Text }
            };

            BindReportData(query, parameters);
        }

        #endregion

        #region Helper Methods

        private void BindReportData(string query, Dictionary<string, object> parameters = null)
        {
            var dt = databaseHelper.ExecuteQueryWithParams(query, parameters);
            Session["dtReport"] = dt;
            BindGridViewWithSummary(dt);
        }

        private void BindGridViewWithSummary(DataTable dt)
        {
            gvReport.DataSource = dt;
            gvReport.DataBind();

            // Update record count
            lblRecordCount.Text = dt.Rows.Count > 0 ? $"(พบทั้งหมด {dt.Rows.Count} รายการ)" : "";
        }

        private decimal CalculateTotalFromDataTable(DataTable dt, string columnName)
        {
            decimal total = 0;
            foreach (DataRow row in dt.Rows)
            {
                if (row[columnName] != DBNull.Value && row[columnName] != null)
                {
                    string value = row[columnName].ToString().Replace(",", "");
                    if (decimal.TryParse(value, out decimal amount))
                    {
                        total += amount;
                    }
                }
            }
            return total;
        }

       

        private void CalculateSalesSummary(DataTable dt)
        {
            double totalCost = 0, totalRevenue = 0, totalProfit = 0;

            foreach (DataRow row in dt.Rows)
            {
                double lastCostPrice = 0;
                double totalPiecesSold = 0;

                if (row["ต้นทุนล่าสุด"] != DBNull.Value)
                {
                    double.TryParse(row["ต้นทุนล่าสุด"].ToString().Replace(",", ""), out lastCostPrice);
                }

                if (row["จำนวนที่ขาย"] != DBNull.Value)
                {
                    double.TryParse(row["จำนวนที่ขาย"].ToString(), out totalPiecesSold);
                }

                totalCost += lastCostPrice * totalPiecesSold;
                totalRevenue += Convert.ToDouble(row["รายได้รวม"].ToString().Replace(",", ""));

                if (row["กำไรรวม"] != DBNull.Value)
                {
                    double profit = Convert.ToDouble(row["กำไรรวม"].ToString().Replace(",", ""));
                    totalProfit += profit;
                }
            }

            double profitMargin = totalCost > 0 ? ((totalRevenue - totalCost) / totalCost) * 100 : 0;

            ShowSummary(
                $"ยอดขายรวม: {totalRevenue:N2} บาท",
                $"ต้นทุนรวม: {totalCost:N2} บาท",
                $"กำไรรวม: {totalProfit:N2} บาท",
                $"อัตรากำไร: {profitMargin:N2}%",
                $"จำนวนสินค้าที่ขาย: {dt.Rows.Count} รายการ"
            );
        }

        private void ShowSummary(params string[] summaries)
        {
            summarySection.Visible = true;

            // ล้างค่าทั้งหมดก่อน
            ClearSummaryLabels();

            // แสดงยอดรวมใน header
            if (summaries.Length > 0 && summaries[0].Contains("ยอดรวม"))
            {
                lblTotalRevenue.Text = summaries[0].Replace("ยอดรวม", "").Replace(":", "").Trim();
            }

            // แสดงตามจำนวน summaries ที่มี
            for (int i = 0; i < Math.Min(summaries.Length, 6); i++)
            {
                switch (i)
                {
                    case 0: lblSummary1.Text = summaries[i]; break;
                    case 1: lblSummary2.Text = summaries[i]; break;
                    case 2: lblSummary3.Text = summaries[i]; break;
                    case 3: lblSummary4.Text = summaries[i]; break;
                    case 4: lblSummary5.Text = summaries[i]; break;
                    case 5: lblSummary6.Text = summaries[i]; break;
                }
            }
        }

        private void ClearSummaryLabels()
        {
            lblSummary1.Text = "";
            lblSummary2.Text = "";
            lblSummary3.Text = "";
            lblSummary4.Text = "";
            lblSummary5.Text = "";
            lblSummary6.Text = "";
            lblTotalRevenue.Text = "";
        }

        private void LoadLatestReservations()
        {
            string query = @"
        SELECT TOP 50
            R.ID AS 'เลขที่การจอง',
            FORMAT(R.Created_Date, 'dd/MM/yyyy HH:mm') AS 'วันที่สร้าง',
            C.Name AS 'ชื่อลูกค้า',
            R.Customer_MobilePhone AS 'เบอร์โทร',
            STUFF((
                SELECT ', ' + A.AccomName
                FROM [Reservation_Accommodation] RA
                INNER JOIN [Accommodation] A ON A.ID = RA.Accommodation_ID
                WHERE RA.Reservation_ID = R.ID
                FOR XML PATH('')), 1, 2, '') AS 'ประเภทที่พัก',
            FORMAT(R.CheckinDate, 'dd MMMM yyyy') AS 'เช็คอิน',
            FORMAT(R.CheckoutDate, 'dd MMMM yyyy') AS 'เช็คเอาท์',
            R.Status AS 'สถานะ',
            FORMAT(R.TotalPrice, 'N0') AS 'ราคารวม'
        FROM [Reservation] R
        INNER JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone 
        ORDER BY R.ID DESC";

            BindReportData(query);
        }

        private void SetEndOfMonthDate()
        {
            if (DateTime.TryParse(txtStartDate.Text, out DateTime date))
            {
                int lastDay = DateTime.DaysInMonth(date.Year, date.Month);
                txtEndDate.Text = new DateTime(date.Year, date.Month, lastDay).ToString("yyyy-MM-dd");
                UpdateDateInfo();
            }
        }

        private void UpdateDateInfo()
        {
            if (DateTime.TryParse(txtStartDate.Text, out DateTime startDate) &&
                DateTime.TryParse(txtEndDate.Text, out DateTime endDate))
            {
                int days = (endDate - startDate).Days + 1;
                lblDateInfo.Text = $"ระยะเวลา: {days} วัน ({(days / 30.0):F1} เดือน)";
            }
        }

        private void UpdateReportTitle()
        {
            if (!string.IsNullOrEmpty(ddlReportType.SelectedValue))
            {
                string reportName = ddlReportType.SelectedItem.Text;
                string dateRange = $"{txtStartDate.Text} ถึง {txtEndDate.Text}";
                lblReportTitle.Text = $"{reportName} | {dateRange}";
            }
        }

        private void DisplayUserInfo()
        {
            // You can display user information here if needed
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrEmpty(ddlReportType.SelectedValue))
            {
                ShowAlert("warning", "กรุณาเลือกประเภทรายงาน");
                return false;
            }

            if (string.IsNullOrEmpty(txtStartDate.Text) || string.IsNullOrEmpty(txtEndDate.Text))
            {
                ShowAlert("warning", "กรุณาระบุวันที่เริ่มต้นและสิ้นสุด");
                return false;
            }

            if (!DateTime.TryParse(txtStartDate.Text, out DateTime startDate) ||
                !DateTime.TryParse(txtEndDate.Text, out DateTime endDate))
            {
                ShowAlert("warning", "รูปแบบวันที่ไม่ถูกต้อง");
                return false;
            }

            if (startDate > endDate)
            {
                ShowAlert("warning", "วันที่เริ่มต้นต้องไม่มากกว่าวันที่สิ้นสุด");
                return false;
            }

            return true;
        }

        private bool IsUserAuthorizedForReport()
        {
            string[] ownerOnlyReports = { "2", "3", "4", "5", "6", "7" };
            if (Array.Exists(ownerOnlyReports, report => report == ddlReportType.SelectedValue))
            {
                return Session["User"]?.ToString() == "Owner";
            }
            return true;
        }

        private void ShowDateRangeSection()
        {
            dateRangeSection.Visible = true;
        }

        private void ClearAllSections()
        {
            gvReport.DataSource = null;
            gvReport.DataBind();
            summarySection.Visible = false;
            btnExport.Visible = false;
            btnPrint.Visible = false;
            lblRecordCount.Text = "";
            lblReportTitle.Text = "";
            ClearSummaryLabels();
        }

        private void FormatGridViewRow(GridViewRow row)
        {
            foreach (TableCell cell in row.Cells)
            {
                string cellText = cell.Text;
                if (decimal.TryParse(cellText.Replace(",", ""), out decimal value))
                {
                    cell.CssClass = "text-end";
                    if (value < 0)
                    {
                        cell.CssClass += " text-danger";
                    }
                }

                // Add tooltip for long text
                if (cellText.Length > 50)
                {
                    cell.ToolTip = cellText;
                }
            }
        }

        private void TranslateColumnHeaders(GridViewRow row)
        {
            // Column headers are already in Thai from the SQL queries
        }

        private void ExportToExcel()
        {
            try
            {
                // ใช้ DataSource จาก Session
                DataTable dt = (DataTable)Session["dtReport"];

                if (dt == null || dt.Rows.Count == 0)
                {
                    ShowAlert("warning", "ไม่มีข้อมูลที่จะส่งออก");
                    return;
                }

                // Clear response ทั้งหมดก่อน
                Response.Clear();
                Response.ClearHeaders();
                Response.ClearContent();
                Response.Buffer = true;

                // ปิด ViewState เพื่อป้องกัน HTML ติดไป
                this.EnableViewState = false;

                Response.AddHeader("content-disposition",
                    $"attachment;filename=Report_{ddlReportType.SelectedItem.Text}_{DateTime.Now:yyyyMMddHHmmss}.csv");

                // ใช้ ContentType และ Encoding ที่ถูกต้องสำหรับภาษาไทย
                Response.ContentType = "text/csv; charset=utf-8";
                Response.Charset = "utf-8";
                Response.ContentEncoding = Encoding.UTF8;

                // สร้าง CSV content ใน memory ก่อน
                StringBuilder csvContent = new StringBuilder();

                // เขียน BOM (Byte Order Mark) สำหรับ UTF-8
                byte[] bom = Encoding.UTF8.GetPreamble();
                Response.BinaryWrite(bom);

                // เขียนหัวข้อรายงาน
                csvContent.AppendLine($"{CleanText(lblReportTitle.Text)}");
                csvContent.AppendLine();

                // เขียนหัวคอลัมน์
                var headers = new List<string>();
                foreach (DataColumn column in dt.Columns)
                {
                    headers.Add(EscapeCsv(column.ColumnName)); // ไม่ต้องใช้ CleanText ที่นี่
                }
                csvContent.AppendLine(string.Join(",", headers));

                // เขียนข้อมูล
                foreach (DataRow row in dt.Rows)
                {
                    var cells = new List<string>();
                    foreach (var item in row.ItemArray)
                    {
                        string cellText = item?.ToString() ?? "";
                        // ไม่ต้องใช้ CleanText ที่นี่เพื่อรักษาภาษาไทย
                        cells.Add(EscapeCsv(cellText));
                    }
                    csvContent.AppendLine(string.Join(",", cells));
                }

                // เขียนเนื้อหาลง Response ด้วย Encoding ที่ถูกต้อง
                byte[] data = Encoding.UTF8.GetBytes(csvContent.ToString());
                Response.OutputStream.Write(data, 0, data.Length);
                Response.Flush();

                // สิ้นสุดการประมวลผลทันที
                Response.End();
            }
            catch (ThreadAbortException)
            {
                // Ignore ThreadAbortException จาก Response.End()
            }
            catch (Exception ex)
            {
                LogError(ex);
                ShowAlert("danger", "เกิดข้อผิดพลาดในการส่งออกไฟล์: " + ex.Message);
                updatePanel.Update();
            }
        }


        // เมธอดทำความสะอาดข้อความจาก HTML
        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // ล้าง HTML tags
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]*>", string.Empty);

            // ล้าง HTML entities
            text = System.Net.WebUtility.HtmlDecode(text);

            // ล้าง character พิเศษ
            text = text.Replace("&nbsp;", " ")
                      .Replace("&amp;", "&")
                      .Replace("&lt;", "<")
                      .Replace("&gt;", ">")
                      .Replace("&quot;", "\"")
                      .Replace("&#39;", "'")
                      .Trim();

            return text;
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";

            // ถ้ามี comma, double quote, หรือ newline ต้อง escape
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\r") || value.Contains("\n"))
            {
                value = value.Replace("\"", "\"\"");
                return $"\"{value}\"";
            }

            return value;
        }

       

        private void ShowAlert(string type, string message)
        {
            alertMessage.Visible = true;
            alertMessage.Attributes["class"] = $"alert alert-{type} alert-dismissible fade show";
            litAlertMessage.Text = message;
        }

        private void HideAlert()
        {
            alertMessage.Visible = false;
        }

        private void LogError(Exception ex)
        {
            // Log error to file or database
            string errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.Message}\n{ex.StackTrace}\n";
            System.Diagnostics.Debug.WriteLine(errorMessage);
        }

        #endregion
    }
}