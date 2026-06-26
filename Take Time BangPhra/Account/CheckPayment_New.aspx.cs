using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Class;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Account
{
    public partial class CheckPayment_New : System.Web.UI.Page
    {
        private readonly string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code codeInstance = new code();
        private LoggingService loggingService;

        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                // Initialize services
                loggingService = new LoggingService(conn);

                if (Session["permission"]?.ToString() == "True" &&
                    (Session["User"]?.ToString() == "Owner" || Session["User"]?.ToString() == "Admin"))
                {
                    if (!IsPostBack)
                    {
                        InitializePage();

                        string syncedQ = Request.QueryString["synced"];
                        if (!string.IsNullOrEmpty(syncedQ))
                        {
                            string safeSynced = HttpUtility.JavaScriptStringEncode(syncedQ);
                            ScriptManager.RegisterStartupScript(this, GetType(), "syncOk",
                                $"alert('ส่งเข้าคิว sync สำเร็จ (Queue #{safeSynced})\\nดูสถานะได้ที่หน้า Accounting Integration');", true);
                        }
                    }
                }
                else
                {
                    Response.Redirect("/Default");
                }
            }
            catch (Exception ex)
            {
                loggingService?.LogException(ex, LoggingService.LogCategory.Accounting,
                    "Page load failed", GetCurrentUserId());
                Response.Redirect("/Default");
            }
        }

        private void InitializePage()
        {
            // Set default dates
            txtStartDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtEndDate.Text = DateTime.Now.ToString("yyyy-MM-dd");

            // Populate year dropdown
            string thisYear = DateTime.Now.Year > 2500 ?
                (DateTime.Now.Year - 543).ToString() : DateTime.Now.Year.ToString();
            string lastYear = DateTime.Now.AddYears(-1).Year > 2500 ?
                (DateTime.Now.AddYears(-1).Year - 543).ToString() : DateTime.Now.AddYears(-1).Year.ToString();

            ddlYear.Items.Clear();
            ddlYear.Items.Add(new ListItem(thisYear, thisYear));
            ddlYear.Items.Add(new ListItem(lastYear, lastYear));

            // Load expense types from database
            LoadExpenseTypes();
        }

        private void LoadExpenseTypes()
        {
            try
            {
                // Get distinct Paid_Type values from Account_Payment
                string query = "SELECT DISTINCT Paid_Type FROM Account_Payment WHERE Paid_Type IS NOT NULL AND Paid_Type != '' ORDER BY Paid_Type";
                var dt = codeInstance.DatabaseQuery(conn, query);

                ddlExpenseType.Items.Clear();
                ddlExpenseType.Items.Add(new ListItem("-- ทั้งหมด --", ""));

                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        string expenseType = row["Paid_Type"].ToString();
                        ddlExpenseType.Items.Add(new ListItem(expenseType, expenseType));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadExpenseTypes Error: {ex.Message}");
            }
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            try
            {
                DateTime startDate, endDate;

                // Determine date range
                if (ddlMonth.SelectedIndex > 0 && !string.IsNullOrEmpty(ddlYear.SelectedValue))
                {
                    // Use month/year selection
                    int month = Convert.ToInt32(ddlMonth.SelectedValue);
                    int year = Convert.ToInt32(ddlYear.SelectedValue);
                    startDate = new DateTime(year, month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);

                    System.Diagnostics.Debug.WriteLine($"🔍 Search Mode: Month/Year - {year}/{month}");
                }
                else
                {
                    if (!DateTime.TryParse(txtStartDate.Text, out startDate) || !DateTime.TryParse(txtEndDate.Text, out endDate))
                    {
                        ShowError("กรุณาระบุวันที่ให้ถูกต้อง (รูปแบบ: yyyy-MM-dd)");
                        return;
                    }
                }

                if (startDate > endDate)
                {
                    ShowError("วันที่เริ่มต้นต้องไม่เกินวันที่สิ้นสุด");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"📅 Date Range: {startDate:yyyy-MM-dd HH:mm:ss} to {endDate:yyyy-MM-dd HH:mm:ss}");

                // Show debug info on page
                lblDateRange.Text = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";

                // Log expense calculation request
                try
                {
                    loggingService.LogAccountingOperation(
                        "ExpenseCalculationRequest",
                        $"Date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                        true,
                        GetCurrentUserId());
                }
                catch { /* Ignore logging errors */ }

                // Get search filters
                string vendorSearch = txtVendorSearch.Text.Trim();
                string expenseType = ddlExpenseType.SelectedValue;
                decimal minAmount = 0;
                decimal.TryParse(txtMinAmount.Text.Trim(), out minAmount);

                System.Diagnostics.Debug.WriteLine($"⚙️ Calling LoadDetails...");
                LoadDetails(startDate, endDate, vendorSearch, expenseType, minAmount);
                System.Diagnostics.Debug.WriteLine($"✅ LoadDetails completed");
            }
            catch (Exception ex)
            {
                // Log exception
                try
                {
                    loggingService.LogException(ex, LoggingService.LogCategory.Accounting,
                        "Expense calculation failed", GetCurrentUserId());
                }
                catch { /* Ignore logging errors */ }

                ShowError("เกิดข้อผิดพลาดในการค้นหา กรุณาลองใหม่");
            }
        }

        private void CalculateExpensesFromData(DataTable allPayments)
        {
            decimal cashTotal = 0, kbankTotal = 0, ktbTotal = 0, directorTotal = 0, otherTotal = 0;
            int cashCount = 0, kbankCount = 0, ktbCount = 0, directorCount = 0, otherCount = 0;
            decimal totalVAT = 0;
            int docCount = 0;
            decimal grandTotal = 0;

            foreach (DataRow row in allPayments.Rows)
            {
                string status = row["Status"]?.ToString() ?? "";
                if (!status.Equals("Normal", StringComparison.OrdinalIgnoreCase)) continue;

                string paidHow = row["Paid_How"]?.ToString() ?? "";
                decimal amount = row["Total_Amount"] != DBNull.Value ? Convert.ToDecimal(row["Total_Amount"]) : 0;
                decimal vat = row["Vat"] != DBNull.Value ? Convert.ToDecimal(row["Vat"]) : 0;

                grandTotal += amount;

                if (paidHow.Contains("เงินสด") || paidHow.Contains("สด"))
                { cashTotal += amount; cashCount++; }
                else if (paidHow.Contains("กสิกร") || paidHow.Contains("KBANK"))
                { kbankTotal += amount; kbankCount++; }
                else if (paidHow.Contains("กรุงไทย") || paidHow.Contains("KTB"))
                { ktbTotal += amount; ktbCount++; }
                else if (paidHow.Contains("กรรมการ") || paidHow.Contains("Director"))
                { directorTotal += amount; directorCount++; }
                else
                { otherTotal += amount; otherCount++; }

                totalVAT += vat;
                docCount++;
            }

            lblCashTotal.Text = cashTotal.ToString("N2");
            lblCashCount.Text = cashCount.ToString();
            lblKBANKTotal.Text = kbankTotal.ToString("N2");
            lblKBANKCount.Text = kbankCount.ToString();
            lblKTBTotal.Text = ktbTotal.ToString("N2");
            lblKTBCount.Text = ktbCount.ToString();
            lblDirectorTotal.Text = directorTotal.ToString("N2");
            lblDirectorCount.Text = directorCount.ToString();

            int totalCount = cashCount + kbankCount + ktbCount + directorCount + otherCount;
            lblGrandTotal.Text = grandTotal.ToString("N2");
            lblTotalCount.Text = totalCount.ToString();
            lblTotalVAT.Text = totalVAT.ToString("N2");
            lblDocCount.Text = docCount.ToString();
        }

        private DataTable GetAllPayments(DateTime startDate, DateTime endDate, string status, string vendorSearch = "", string expenseType = "", decimal minAmount = 0)
        {
            // Get all payment vouchers in the date range
            // For payroll payments (เงินเดือน), show employee name from Payroll_Records via VoucherNumber
            string query = @"
                SELECT ap.ID, ap.[UID], ap.Created_Date, ap.Paid_How, ap.Paid_Type, ap.Total_Amount, ap.Vat,
                       ap.Status,
                       CASE
                           WHEN ap.Paid_Type = N'เงินเดือน' AND pr.EmployeeName IS NOT NULL THEN pr.EmployeeName
                           ELSE ISNULL(v.Name, '-')
                       END as Vendor_Name,
                       a.Username as Created_By
                FROM Account_Payment ap
                LEFT JOIN Vendor v ON ap.Vendor_ID = v.ID
                LEFT JOIN Admin a ON ap.Created_By_ID = a.ID
                LEFT JOIN Payroll_Records pr ON ap.ID = pr.VoucherNumber
                WHERE ap.Created_Date >= @StartDate
                  AND ap.Created_Date < DATEADD(day, 1, @EndDate)
                  AND ap.Status LIKE @Status";

            // Add vendor name filter if provided
            if (!string.IsNullOrWhiteSpace(vendorSearch))
            {
                query += @" AND (v.Name LIKE @VendorSearch
                           OR (ap.Paid_Type = N'เงินเดือน' AND pr.EmployeeName LIKE @VendorSearch))";
            }

            // Add expense type filter if provided
            if (!string.IsNullOrWhiteSpace(expenseType))
            {
                query += " AND ap.Paid_Type = @ExpenseType";
            }

            // Add minimum amount filter if provided
            if (minAmount > 0)
            {
                query += " AND ap.Total_Amount >= @MinAmount";
            }

            // Admin permission check: Hide employee-related expenses
            if (Session["User"]?.ToString() == "Admin")
            {
                query += " AND (v.Vendor_Group IS NULL OR v.Vendor_Group != N'01-พนักงานประจำ')";
                System.Diagnostics.Debug.WriteLine($"   🔒 Admin mode: Hiding employee expenses");
            }

            query += " ORDER BY ap.ID ASC";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDate", startDate },
                { "@EndDate", endDate },
                { "@Status", status }
            };

            // Add vendor search parameter if provided
            if (!string.IsNullOrWhiteSpace(vendorSearch))
            {
                parameters.Add("@VendorSearch", "%" + vendorSearch.Trim() + "%");
            }

            // Add expense type parameter if provided
            if (!string.IsNullOrWhiteSpace(expenseType))
            {
                parameters.Add("@ExpenseType", expenseType);
            }

            // Add minimum amount parameter if provided
            if (minAmount > 0)
            {
                parameters.Add("@MinAmount", minAmount);
            }

            System.Diagnostics.Debug.WriteLine($"📋 GetAllPayments Query:");
            System.Diagnostics.Debug.WriteLine($"   User: {Session["User"]?.ToString() ?? "Unknown"}");
            System.Diagnostics.Debug.WriteLine($"   @StartDate = {startDate:yyyy-MM-dd HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"   @EndDate = {endDate:yyyy-MM-dd HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"   @Status = {status}");
            System.Diagnostics.Debug.WriteLine($"   @VendorSearch = {vendorSearch}");
            System.Diagnostics.Debug.WriteLine($"   @ExpenseType = {expenseType}");
            System.Diagnostics.Debug.WriteLine($"   @MinAmount = {minAmount}");

            var result = codeInstance.DatabaseQuerySafe(conn, query, parameters);
            System.Diagnostics.Debug.WriteLine($"   ✅ Result: {result?.Rows.Count ?? 0} rows");

            return result;
        }

        private void LoadDetails(DateTime startDate, DateTime endDate, string vendorSearch = "", string expenseType = "", decimal minAmount = 0)
        {
            DataTable dt = null;
            try
            {
                System.Diagnostics.Debug.WriteLine($"📊 LoadDetails called:");
                System.Diagnostics.Debug.WriteLine($"   Start: {startDate:yyyy-MM-dd HH:mm:ss}");
                System.Diagnostics.Debug.WriteLine($"   End: {endDate:yyyy-MM-dd HH:mm:ss}");
                System.Diagnostics.Debug.WriteLine($"   VendorSearch: {vendorSearch}");
                System.Diagnostics.Debug.WriteLine($"   ExpenseType: {expenseType}");
                System.Diagnostics.Debug.WriteLine($"   MinAmount: {minAmount}");

                // Show all documents (both Normal and Cancel) with all filters
                dt = GetAllPayments(startDate, endDate, "%", vendorSearch, expenseType, minAmount);
                System.Diagnostics.Debug.WriteLine($"   Retrieved {dt?.Rows.Count ?? 0} rows");

                if (dt != null && dt.Rows.Count > 0)
                {
                    lblDateRange.Text += $" <span style='color: green; font-weight: bold;'>(✓ พบ {dt.Rows.Count} เอกสาร)</span>";
                    System.Diagnostics.Debug.WriteLine($"   ✅ Showing {dt.Rows.Count} documents");
                }
                else
                {
                    lblDateRange.Text += $" <span style='color: red; font-weight: bold;'>(⚠️ ไม่พบเอกสาร)</span>";
                    System.Diagnostics.Debug.WriteLine($"   ⚠️ No documents found!");
                }

                // คำนวณสรุปค่าใช้จ่ายจาก DataTable ที่ดึงมาแล้ว (ไม่ต้อง query ซ้ำ)
                if (dt != null) CalculateExpensesFromData(dt);

                var nextAccDocs = PrefetchNextAccDocuments(startDate, endDate);
                if (dt != null) MergeNextAccIntoGrid(dt, nextAccDocs);

                // เรียงทั้งตาราง (รวมเอกสารที่ดึง/สร้างบน NextAcc ซึ่งถูก append ท้าย) ตามเลขที่เอกสาร
                // ที่แสดง (DisplayDoc) — ใหม่สุดอยู่บน. แก้ปัญหาเอกสาร NextAcc ไม่เรียงตามเลขที่เอกสาร
                if (dt != null && dt.Columns.Contains("DisplayDoc"))
                {
                    dt.DefaultView.Sort = "DisplayDoc DESC";
                    dt = dt.DefaultView.ToTable();
                }

                if (dt != null) BuildUidCacheFromGrid(dt);

                gvDetails.DataSource = dt;
                gvDetails.DataBind();
                System.Diagnostics.Debug.WriteLine($"   ✅ GridView.DataBind() completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Error in LoadDetails: {ex.Message}");
                lblDateRange.Text += " <span style='color: red;'>[โหลดข้อมูลล้มเหลว]</span>";
                ShowError("เกิดข้อผิดพลาดในการโหลดข้อมูล กรุณาลองใหม่");
                try { loggingService.LogException(ex, LoggingService.LogCategory.Accounting, "LoadDetails failed", GetCurrentUserId()); } catch { }

                // Bind empty DataTable
                try
                {
                    gvDetails.DataSource = new DataTable();
                    gvDetails.DataBind();
                }
                catch { }
            }
        }

        protected void btnExport_Click(object sender, EventArgs e)
        {
            try
            {
                DateTime startDate, endDate;

                // Determine date range
                if (ddlMonth.SelectedIndex > 0 && !string.IsNullOrEmpty(ddlYear.SelectedValue))
                {
                    int month = Convert.ToInt32(ddlMonth.SelectedValue);
                    int year = Convert.ToInt32(ddlYear.SelectedValue);
                    startDate = new DateTime(year, month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                }
                else
                {
                    if (!DateTime.TryParse(txtStartDate.Text, out startDate) || !DateTime.TryParse(txtEndDate.Text, out endDate))
                    {
                        ShowError("กรุณาระบุวันที่ให้ถูกต้อง");
                        return;
                    }
                }

                if (startDate > endDate) { ShowError("วันที่เริ่มต้นต้องไม่เกินวันที่สิ้นสุด"); return; }

                // Create CSV content
                StringBuilder csv = new StringBuilder();
                csv.Append("\uFEFF"); // BOM for UTF-8

                // Header
                csv.AppendLine("สรุปค่าใช้จ่าย");
                csv.AppendLine($"ช่วงวันที่:,{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}");
                csv.AppendLine($"วันที่ออกรายงาน:,{DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                csv.AppendLine();

                // Summary table
                csv.AppendLine("วิธีชำระเงิน,ยอดรวม,จำนวนรายการ");
                csv.AppendLine($"เงินสด,{lblCashTotal.Text},{lblCashCount.Text}");
                csv.AppendLine($"โอนกสิกร,{lblKBANKTotal.Text},{lblKBANKCount.Text}");
                csv.AppendLine($"โอนกรุงไทย,{lblKTBTotal.Text},{lblKTBCount.Text}");
                csv.AppendLine($"เงินกรรมการ,{lblDirectorTotal.Text},{lblDirectorCount.Text}");
                csv.AppendLine($"รวมทั้งหมด,{lblGrandTotal.Text},{lblTotalCount.Text}");
                csv.AppendLine();

                // Detail records (with all filters)
                string vendorSearch = txtVendorSearch.Text.Trim();
                string expenseType = ddlExpenseType.SelectedValue;
                decimal minAmount = 0;
                decimal.TryParse(txtMinAmount.Text.Trim(), out minAmount);
                var dt = GetAllPayments(startDate, endDate, "%", vendorSearch, expenseType, minAmount);
                csv.AppendLine("รายละเอียดเอกสาร");
                csv.AppendLine("เลขที่เอกสาร,วันที่,ผู้รับเงิน,วิธีชำระ,ยอดรวม,VAT,สถานะ,ผู้สร้าง");

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        string docId = row["ID"]?.ToString() ?? "";
                        string date = row["Created_Date"] != DBNull.Value ? Convert.ToDateTime(row["Created_Date"]).ToString("dd/MM/yyyy HH:mm") : "";
                        string vendor = row["Vendor_Name"]?.ToString() ?? "-";
                        string paidHow = row["Paid_How"]?.ToString() ?? "";
                        string amount = row["Total_Amount"] != DBNull.Value ? Convert.ToDecimal(row["Total_Amount"]).ToString("N2") : "0.00";
                        string vat = row["Vat"] != DBNull.Value ? Convert.ToDecimal(row["Vat"]).ToString("N2") : "0.00";
                        string status = row["Status"]?.ToString() ?? "";
                        string createdBy = row["Created_By"]?.ToString() ?? "";

                        csv.AppendLine($"{CsvSafe(docId)},{CsvSafe(date)},{CsvSafe(vendor)},{CsvSafe(paidHow)},{amount},{vat},{CsvSafe(status)},{CsvSafe(createdBy)}");
                    }
                }

                // Send file
                Response.Clear();
                Response.ContentType = "text/csv";
                Response.ContentEncoding = Encoding.UTF8;
                Response.Charset = "UTF-8";
                Response.AddHeader("Content-Disposition", $"attachment;filename=รายงานค่าใช้จ่าย_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv");
                Response.Write(csv.ToString());
                Response.Flush();
                HttpContext.Current.ApplicationInstance.CompleteRequest();
            }
            catch (Exception ex)
            {
                if (!(ex is System.Threading.ThreadAbortException))
                    ShowError("เกิดข้อผิดพลาดในการ export กรุณาลองใหม่");
            }
        }

        protected void gvDetails_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            if (!chkEnableDelete.Checked)
            {
                ShowError("กรุณาเปิดใช้งานปุ่มลบก่อน");
                return;
            }

            try
            {
                var keys = gvDetails.DataKeys[e.RowIndex];
                if ((keys.Values["IsNextAccOnly"]?.ToString()) == "1")
                {
                    ShowError("เอกสารนี้สร้างบน NextAcc — กรุณาลบ/ยกเลิกที่ระบบ NextAcc");
                    return;
                }
                string docNum = keys.Values["ID"]?.ToString() ?? "";

                // ลบ local ได้เฉพาะเมื่อเอกสารถูกลบจาก NextAcc แล้ว (ผู้ใช้ต้องลบ/ยกเลิกบน NextAcc ก่อน)
                Server.ScriptTimeout = 300;
                try
                {
                    var syncChk = new Integration.AccountingSyncService(conn);
                    bool? gone = System.Threading.Tasks.Task.Run(() => syncChk.IsNextAccDocumentGoneAsync(docNum, "VOUCHER")).GetAwaiter().GetResult();
                    if (gone == false)
                    {
                        ShowError("เอกสารยังอยู่บน NextAcc — กรุณาลบ/ยกเลิกบน NextAcc ก่อน แล้วค่อยลบในระบบนี้");
                        return;
                    }
                    if (gone == null)
                    {
                        ShowError("ตรวจสอบสถานะเอกสารบน NextAcc ไม่ได้ (เครือข่าย/company endpoint ปิด) — ยังไม่ลบ ลองใหม่อีกครั้ง");
                        return;
                    }
                    // gone == true → NextAcc ไม่มีเอกสารแล้ว → ลบ local ได้
                }
                catch
                {
                    ShowError("ตรวจสอบสถานะเอกสารบน NextAcc ไม่ได้ — ยังไม่ลบ");
                    return;
                }

                string docType = docNum.Length >= 3 ? docNum.Substring(0, 3) : "";
                string docYear = docNum.Length >= 5 ? "20" + docNum.Substring(3, 2) : "";
                string docMonthPadded = docNum.Length >= 7 ? docNum.Substring(5, 2) : "";
                // Convert to non-padded month (PaymentVoucher stores as "1", "2", not "01", "02")
                string docMonth = int.TryParse(docMonthPadded, out int monthNum) ? monthNum.ToString() : docMonthPadded;

                if (docType == "PAY")
                {
                    string deletedBy = Session["username"]?.ToString() ?? Session["User"]?.ToString() ?? "UNKNOWN";
                    try
                    {
                        loggingService.LogAccountingOperation("PaymentDeleted",
                            $"DocNum={docNum}, DeletedBy={deletedBy}", true, GetCurrentUserId());
                    }
                    catch { }

                    string basePath = ConfigurationManager.AppSettings["PaymentFolderPath"];
                    string path = basePath + "\\" + docYear + "\\" + docMonth;
                    string pathPadded = basePath + "\\" + docYear + "\\" + docMonthPadded;

                    var deleteDetailsParams = new Dictionary<string, object>
                    {
                        { "@PaymentID", docNum }
                    };
                    codeInstance.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Account_Payment_Detail] WHERE Payment_ID = @PaymentID",
                        deleteDetailsParams);

                    // SECURE: Delete payment record with parameterized query
                    var deletePaymentParams = new Dictionary<string, object>
                    {
                        { "@ID", docNum }
                    };
                    codeInstance.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Account_Payment] WHERE ID = @ID",
                        deletePaymentParams);

                    // Delete payment files (check both non-padded and padded paths)
                    foreach (string checkPath in new[] { path, pathPadded })
                    {
                        if (Directory.Exists(checkPath))
                        {
                            string[] files = Directory.GetFiles(checkPath, docNum + "*");
                            foreach (string file in files)
                            {
                                File.Delete(file);
                            }
                        }
                    }

                    // NextAcc doc ถูกลบไปแล้ว (ตรวจก่อนลบด้านบน) → ไม่ต้อง enqueue void ซ้ำ
                    codeInstance.Logs(conn, "Accounting Sync", $"CheckPayment_New: ลบ local {docNum} (NextAcc ไม่มีเอกสารแล้ว — ผู้ใช้ลบบน NextAcc ก่อน)", "SYSTEM");

                    // Show success message then redirect
                    ClientScript.RegisterStartupScript(this.GetType(), "success",
                        "alert('✅ ลบใบสำคัญจ่ายเรียบร้อยแล้ว'); window.location.href='/Account/CheckPayment_New';", true);
                }
                else
                {
                    ShowError("เอกสารนี้ไม่ใช่ใบสำคัญจ่าย");
                }
            }
            catch (Exception ex)
            {
                try { loggingService.LogException(ex, LoggingService.LogCategory.Accounting, "Delete failed", GetCurrentUserId()); } catch { }
                ShowError("ลบเอกสารไม่สำเร็จ กรุณาลองใหม่");
            }
        }

        protected void gvDetails_SelectedIndexChanging(object sender, GridViewSelectEventArgs e)
        {
            try
            {
                var keys = gvDetails.DataKeys[e.NewSelectedIndex];
                string docNum = keys.Values["ID"]?.ToString() ?? "";
                string docStatus = keys.Values["Status"]?.ToString() ?? "";
                string viewUrl = keys.Values["NextAccViewUrl"]?.ToString() ?? "";
                bool isNextAccOnly = (keys.Values["IsNextAccOnly"]?.ToString()) == "1";

                System.Diagnostics.Debug.WriteLine($"📄 Opening document: {docNum}, Status: {docStatus}, NextAccUrl: {viewUrl}");

                // ── เอกสารที่มีบน NextAcc → เปิด PDF/ลิงก์ NextAcc ที่ cache ไว้ (เลขที่/รูปแบบตาม NextAcc) ──
                if (!string.IsNullOrEmpty(viewUrl))
                {
                    Response.Redirect(viewUrl);
                    return;
                }

                // เอกสารที่สร้างบน NextAcc แต่ไม่มีลิงก์ — ไม่มี PDF ฝั่งระบบเราให้เปิด
                if (isNextAccOnly)
                {
                    ShowError("เอกสารนี้สร้างบน NextAcc แต่ยังไม่มีไฟล์ให้เปิดดู (ไม่มี template PDF บน NextAcc)");
                    return;
                }

                // Parse document info
                string docType = docNum.Length >= 3 ? docNum.Substring(0, 3) : "";
                string docYear = docNum.Length >= 5 ? "20" + docNum.Substring(3, 2) : "";
                string docMonthPadded = docNum.Length >= 7 ? docNum.Substring(5, 2) : "";
                // Convert to non-padded month (PaymentVoucher stores as "1", "2", not "01", "02")
                string docMonth = int.TryParse(docMonthPadded, out int monthNum) ? monthNum.ToString() : docMonthPadded;

                System.Diagnostics.Debug.WriteLine($"   Parsed: Type={docType}, Year={docYear}, Month={docMonth} (from {docMonthPadded})");

                if (docType == "PAY")
                {
                    // SECURE: Get payment UID from database with parameterized query
                    string path = ConfigurationManager.AppSettings["PaymentFolderPath"];
                    var uidParams = new Dictionary<string, object>
                    {
                        { "@ID", docNum }
                    };
                    var uidResult = codeInstance.DatabaseQuerySafe(conn,
                        "SELECT [UID] FROM [dbo].[Account_Payment] WHERE ID = @ID",
                        uidParams);

                    string uid = "";
                    if (uidResult != null && uidResult.Rows.Count > 0 && uidResult.Rows[0][0] != DBNull.Value)
                    {
                        uid = uidResult.Rows[0][0].ToString();
                    }

                    // Build file paths - check both non-padded month (1,2,...) and padded (01,02,...)
                    List<string> filesToCheck = new List<string>();

                    if (docStatus == "Cancel")
                    {
                        if (!string.IsNullOrEmpty(uid))
                        {
                            filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}_{uid}_Cancel.pdf");
                            filesToCheck.Add($"{path}\\{docYear}\\{docMonthPadded}\\{docNum}_{uid}_Cancel.pdf");
                        }
                        filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}_Cancel.pdf");
                        filesToCheck.Add($"{path}\\{docYear}\\{docMonthPadded}\\{docNum}_Cancel.pdf");
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(uid))
                        {
                            filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}_{uid}.pdf");
                            filesToCheck.Add($"{path}\\{docYear}\\{docMonthPadded}\\{docNum}_{uid}.pdf");
                        }
                        filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}.pdf");
                        filesToCheck.Add($"{path}\\{docYear}\\{docMonthPadded}\\{docNum}.pdf");
                    }

                    // Check and redirect
                    foreach (var filePath in filesToCheck)
                    {
                        System.Diagnostics.Debug.WriteLine($"   Checking: {filePath}");
                        if (File.Exists(filePath))
                        {
                            string relativeUrl = filePath.Replace(path, "/Documents/Payment").Replace("\\", "/");
                            System.Diagnostics.Debug.WriteLine($"   ✅ Found! Redirecting to: {relativeUrl}");
                            Response.Redirect(relativeUrl);
                            return;
                        }
                    }

                    // PDF not found - redirect to edit mode to regenerate PDF
                    // This is especially important for payroll vouchers created programmatically
                    System.Diagnostics.Debug.WriteLine($"   ⚠️ PDF not found, redirecting to edit mode to regenerate");
                    Response.Redirect($"/Account/PaymentVoucher?command=edit&uid={uid}");
                    return;
                }
                else
                {
                    throw new Exception($"ประเภทเอกสารไม่ถูกต้อง: {docType}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Error: {ex.Message}");
                try { loggingService.LogException(ex, LoggingService.LogCategory.Accounting, "View document failed", GetCurrentUserId()); } catch { }
                ShowError("เปิดเอกสารไม่สำเร็จ กรุณาลองใหม่");
            }
        }

        protected void gvDetails_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "sync")
            {
                string docId = e.CommandArgument.ToString();
                HandleSyncVoucher(docId);
                return;
            }

            if (e.CommandName == "edit")
            {
                try
                {
                    int rowIndex = Convert.ToInt32(e.CommandArgument);
                    var keys = gvDetails.DataKeys[rowIndex];
                    if ((keys.Values["IsNextAccOnly"]?.ToString()) == "1")
                    {
                        ShowError("เอกสารนี้สร้างบน NextAcc — กรุณาแก้ไขที่ระบบ NextAcc");
                        return;
                    }
                    string docNum = keys.Values["ID"]?.ToString() ?? "";
                    string docType = docNum.Length >= 3 ? docNum.Substring(0, 3) : "";

                    if (docType == "PAY")
                    {
                        var uidParams = new Dictionary<string, object> { { "@ID", docNum } };
                        var uidResult = codeInstance.DatabaseQuerySafe(conn,
                            "SELECT [UID] FROM [dbo].[Account_Payment] WHERE ID = @ID", uidParams);
                        if (uidResult == null || uidResult.Rows.Count == 0 || uidResult.Rows[0][0] == DBNull.Value)
                        {
                            ShowError("ไม่พบเอกสารในระบบ");
                            return;
                        }
                        string uid = uidResult.Rows[0][0].ToString();
                        Response.Redirect("/Account/PaymentVoucher?command=edit&uid=" + uid);
                    }
                    else
                    {
                        ShowError("เอกสารนี้ไม่ใช่ใบสำคัญจ่าย");
                    }
                }
                catch (Exception ex)
                {
                    try { loggingService.LogException(ex, LoggingService.LogCategory.Accounting, "Edit redirect failed", GetCurrentUserId()); } catch { }
                    ShowError("แก้ไขเอกสารไม่สำเร็จ กรุณาลองใหม่");
                }
            }
        }

        // ──────────────────────────────────────────────
        // Accounting Sync Status
        // ──────────────────────────────────────────────

        private Dictionary<string, DataRow> _syncStatusCache;

        /// <summary>DataBinder.Eval ที่ไม่ throw ถ้าไม่มีคอลัมน์/เป็น DBNull</summary>
        private static string SafeEval(object dataItem, string field)
        {
            try
            {
                var v = DataBinder.Eval(dataItem, field);
                return v == null || v == DBNull.Value ? "" : v.ToString();
            }
            catch { return ""; }
        }

        /// <summary>
        /// ดึงเอกสารฝั่งจ่ายที่ออกบน NextAcc ในช่วงวันที่ที่ค้นหา (PDF + ไฟล์แนบ) มาเก็บที่ฝั่ง TakeTime
        /// อัตโนมัติ. ใช้ /api/integration/documents เป็นแหล่งข้อมูลหลัก → เจอเอกสารที่ออกบน NextAcc เสมอ
        /// </summary>
        private List<NextAccCachedDocument> PrefetchNextAccDocuments(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var config = new AccountingConfig(conn);
                if (!config.IsConfigured || !config.Enabled) return new List<NextAccCachedDocument>();

                string cn = conn;

                // 1) รันเบื้องหลัง (fire-and-forget): โหลด PDF/ไฟล์แนบ/WHT จาก NextAcc มาเก็บดิสก์ไว้ใช้รอบถัดไป
                System.Threading.Tasks.Task.Run(() =>
                {
                    try { new AccountingSyncService(cn).DownloadVoucherDocumentsForRangeAsync(fromDate, toDate, true, cacheFiles: true).Wait(); }
                    catch { }
                });

                // 2) แสดงผลเร็ว: ดึงเฉพาะ "รายการเอกสาร" (metadata) + ไฟล์ที่ cache ไว้บนดิสก์ — ไม่ยิง API ต่อเอกสาร
                //    → เอกสารที่สร้างบน NextAcc โดยตรง (เช่น PV-202606-0003) จะโผล่ในตารางทันทีพร้อมลิงก์เปิดดู
                var task = System.Threading.Tasks.Task.Run(() =>
                    new AccountingSyncService(cn).DownloadVoucherDocumentsForRangeAsync(fromDate, toDate, true, cacheFiles: false));

                if (task.Wait(TimeSpan.FromSeconds(12)))
                    return task.Result ?? new List<NextAccCachedDocument>();

                lblDateRange.Text += " <span style='color:#e67e22;'>(NextAcc: แสดงจากแคช — กำลังดึงรายการเบื้องหลัง ลองค้นหาอีกครั้ง)</span>";
                return new List<NextAccCachedDocument>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PrefetchNextAccDocuments error: " + ex.Message);
                lblDateRange.Text += $" <span style='color:#c0392b;'>(NextAcc error: {Server.HtmlEncode(ex.Message)})</span>";
                return new List<NextAccCachedDocument>();
            }
        }

        /// <summary>
        /// รวมเอกสาร NextAcc เข้ากับตาราง:
        ///   - แถวที่ sync จาก TakeTime (จับคู่ด้วย Reference == เลขใบสำคัญจ่าย) → แสดงเลขที่เอกสารตาม NextAcc + ลิงก์เปิดดู
        ///   - เอกสารที่สร้างบน NextAcc โดยตรง (ไม่มีคู่ในระบบ) → เพิ่มเป็นแถวใหม่
        /// เก็บข้อมูลที่ใช้ตอน postback ลง DataKeys (NextAccViewUrl, IsNextAccOnly)
        /// </summary>
        private void MergeNextAccIntoGrid(DataTable dt, List<NextAccCachedDocument> nextAccDocs)
        {
            if (!dt.Columns.Contains("DisplayDoc")) dt.Columns.Add("DisplayDoc", typeof(string));
            if (!dt.Columns.Contains("IsNextAccOnly")) dt.Columns.Add("IsNextAccOnly", typeof(string));
            if (!dt.Columns.Contains("HasNextAcc")) dt.Columns.Add("HasNextAcc", typeof(string));
            if (!dt.Columns.Contains("NextAccViewUrl")) dt.Columns.Add("NextAccViewUrl", typeof(string));
            if (!dt.Columns.Contains("NextAccDeepLink")) dt.Columns.Add("NextAccDeepLink", typeof(string));
            if (!dt.Columns.Contains("NextAccAttCount")) dt.Columns.Add("NextAccAttCount", typeof(int));
            if (!dt.Columns.Contains("NextAccAttUrls")) dt.Columns.Add("NextAccAttUrls", typeof(string));
            if (!dt.Columns.Contains("WhtCertUrl")) dt.Columns.Add("WhtCertUrl", typeof(string));

            nextAccDocs = nextAccDocs ?? new List<NextAccCachedDocument>();

            // index ตาม Reference (= เลขใบสำคัญจ่ายฝั่ง TakeTime) และตาม NextAcc Id
            var byRef = new Dictionary<string, NextAccCachedDocument>(StringComparer.OrdinalIgnoreCase);
            var byId = new Dictionary<Guid, NextAccCachedDocument>();
            foreach (var d in nextAccDocs)
            {
                string r = (d.Reference ?? "").Trim();
                if (!string.IsNullOrEmpty(r) && !byRef.ContainsKey(r)) byRef[r] = d;
                if (d.NextAccId != Guid.Empty && !byId.ContainsKey(d.NextAccId)) byId[d.NextAccId] = d;
            }

            // ใช้ sync queue ช่วยจับคู่: docNum → Nexaacc_Response_Id (Guid) → เอกสาร NextAcc
            if (_syncStatusCache == null) LoadSyncStatusCache();

            var matched = new HashSet<NextAccCachedDocument>();

            foreach (DataRow row in dt.Rows)
            {
                string id = row["ID"]?.ToString() ?? "";
                row["IsNextAccOnly"] = "0";

                NextAccCachedDocument nd = null;
                // 1) จับคู่ด้วย Reference
                byRef.TryGetValue(id, out nd);
                // 2) ถ้าไม่เจอ ใช้ Nexaacc_Response_Id จาก sync queue
                if (nd == null && _syncStatusCache != null && _syncStatusCache.TryGetValue(id, out var qrow))
                {
                    string respId = qrow.Table.Columns.Contains("Nexaacc_Response_Id") ? qrow["Nexaacc_Response_Id"]?.ToString() : null;
                    if (Guid.TryParse(respId, out var g)) byId.TryGetValue(g, out nd);
                }

                if (nd != null)
                {
                    matched.Add(nd);
                    row["DisplayDoc"] = !string.IsNullOrEmpty(nd.DocumentNumber) ? nd.DocumentNumber : id;
                    row["HasNextAcc"] = "1";
                    row["NextAccViewUrl"] = nd.BestViewUrl ?? "";
                    row["NextAccDeepLink"] = nd.DeepLinkUrl ?? "";
                    row["NextAccAttCount"] = nd.AttachmentCount;
                    row["NextAccAttUrls"] = nd.AttachmentRelativeUrls != null && nd.AttachmentRelativeUrls.Count > 0
                        ? string.Join("|", nd.AttachmentRelativeUrls) : "";
                    row["WhtCertUrl"] = nd.WhtCertPdfRelativeUrl ?? "";
                }
                else if (TryFillFromSyncQueueAndDisk(row, id))
                {
                    // เติมจาก sync queue (เลข EXP + ลิงก์) + disk cache (PDF/ไฟล์แนบ) — เชื่อถือได้ ไม่ต้องรอ API
                }
                else
                {
                    row["DisplayDoc"] = id;
                    row["HasNextAcc"] = "0";
                    row["NextAccViewUrl"] = "";
                    row["NextAccDeepLink"] = "";
                    row["NextAccAttCount"] = 0;
                    row["NextAccAttUrls"] = "";
                    row["WhtCertUrl"] = "";
                }
            }

            // เอกสารที่สร้างบน NextAcc โดยตรง (ไม่มีคู่ในระบบ) → เพิ่มแถวใหม่
            foreach (var nd in nextAccDocs)
            {
                if (matched.Contains(nd)) continue;
                var nr = dt.NewRow();
                nr["ID"] = !string.IsNullOrEmpty(nd.DocumentNumber) ? nd.DocumentNumber : nd.NextAccId.ToString();
                nr["DisplayDoc"] = nd.DocumentNumber;
                nr["Created_Date"] = nd.DocumentDate;
                nr["Vendor_Name"] = string.IsNullOrEmpty(nd.ContactName) ? "-" : nd.ContactName;
                nr["Paid_How"] = "";
                nr["Paid_Type"] = nd.DocumentTypeLabel ?? "";
                nr["Total_Amount"] = nd.TotalAmount;
                nr["Vat"] = nd.VatAmount;
                nr["Status"] = "NextAcc";
                nr["Created_By"] = "NextAcc";
                nr["IsNextAccOnly"] = "1";
                nr["HasNextAcc"] = "1";
                nr["NextAccViewUrl"] = nd.BestViewUrl ?? "";
                nr["NextAccDeepLink"] = nd.DeepLinkUrl ?? "";
                nr["NextAccAttCount"] = nd.AttachmentCount;
                nr["NextAccAttUrls"] = nd.AttachmentRelativeUrls != null && nd.AttachmentRelativeUrls.Count > 0
                    ? string.Join("|", nd.AttachmentRelativeUrls) : "";
                nr["WhtCertUrl"] = nd.WhtCertPdfRelativeUrl ?? "";
                dt.Rows.Add(nr);
            }
        }

        /// <summary>
        /// เติมข้อมูล NextAcc ให้แถวจาก sync queue (เลขเอกสาร EXP + ลิงก์เปิดใน NextAcc) และ disk cache
        /// (PDF/ไฟล์แนบ/ใบหัก ณ ที่จ่าย ที่เคยโหลดไว้) — ใช้เมื่อ API prefetch ไม่ทัน/timeout.
        /// คืน true ถ้ามีข้อมูล NextAcc ให้แสดง
        /// </summary>
        private bool TryFillFromSyncQueueAndDisk(DataRow row, string id)
        {
            if (_syncStatusCache == null || !_syncStatusCache.TryGetValue(id, out var sq))
                return false;

            string naDocNum = sq.Table.Columns.Contains("Nexaacc_Document_Number") ? sq["Nexaacc_Document_Number"]?.ToString() : "";
            string naId = sq.Table.Columns.Contains("Nexaacc_Response_Id") ? sq["Nexaacc_Response_Id"]?.ToString() : "";
            string naType = sq.Table.Columns.Contains("Nexaacc_Document_Type") ? sq["Nexaacc_Document_Type"]?.ToString() : "";
            string deepLink = BuildNexaaccLink(naId, naType);

            var disk = ReadCachedNextAccFromDisk(id);

            // ไม่มีทั้งลิงก์และไฟล์ cache → ถือว่าไม่มีข้อมูล NextAcc
            if (string.IsNullOrEmpty(deepLink) && disk == null && string.IsNullOrEmpty(naDocNum))
                return false;

            row["DisplayDoc"] = !string.IsNullOrEmpty(naDocNum) ? naDocNum : id;
            row["HasNextAcc"] = "1";
            row["NextAccViewUrl"] = (disk?.PdfUrl) ?? deepLink ?? "";
            row["NextAccDeepLink"] = deepLink ?? "";
            row["NextAccAttCount"] = disk?.AttUrls?.Count ?? 0;
            row["NextAccAttUrls"] = (disk != null && disk.AttUrls.Count > 0) ? string.Join("|", disk.AttUrls) : "";
            row["WhtCertUrl"] = disk?.WhtUrl ?? "";
            return true;
        }

        private void LoadSyncStatusCache()
        {
            _syncStatusCache = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var dt = codeInstance.DatabaseQuerySafe(conn,
                    @"SELECT ID, Entity_Type, Action_Type, Status, Error_Message, Payload, Created_Date,
                             Nexaacc_Response_Id, Nexaacc_Document_Number, Nexaacc_Document_Type
                      FROM Accounting_Sync_Queue
                      WHERE Entity_Type = 'VOUCHER' AND Action_Type = 'CREATE_VOUCHER_JOURNAL'
                      ORDER BY ID DESC",
                    null);

                if (dt == null) return;

                foreach (DataRow row in dt.Rows)
                {
                    string payload = row["Payload"]?.ToString() ?? "";
                    int startIdx = payload.IndexOf("\"documentNumber\":\"");
                    if (startIdx < 0) continue;
                    startIdx += 18;
                    int endIdx = payload.IndexOf("\"", startIdx);
                    if (endIdx <= startIdx) continue;
                    string docNum = payload.Substring(startIdx, endIdx - startIdx);
                    if (!_syncStatusCache.ContainsKey(docNum))
                        _syncStatusCache[docNum] = row;
                }
            }
            catch { }
        }

        protected void gvDetails_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;

            // UX: เพิ่ม confirm dialog ก่อนลบ
            foreach (var ctrl in e.Row.Cells[0].Controls)
            {
                if (ctrl is Button btnDel && btnDel.CommandName == "Delete")
                {
                    btnDel.OnClientClick = "return confirm('ยืนยันลบเอกสารนี้? (ลบถาวร ไม่สามารถกู้คืนได้)');";
                    break;
                }
            }

            string docId = DataBinder.Eval(e.Row.DataItem, "ID")?.ToString() ?? "";
            string docStatus = DataBinder.Eval(e.Row.DataItem, "Status")?.ToString() ?? "";
            string isNextAccOnly = SafeEval(e.Row.DataItem, "IsNextAccOnly");
            string hasNextAcc = SafeEval(e.Row.DataItem, "HasNextAcc");
            string viewUrl = SafeEval(e.Row.DataItem, "NextAccViewUrl");
            int attCount = 0; int.TryParse(SafeEval(e.Row.DataItem, "NextAccAttCount"), out attCount);

            // คอลัมน์ "เอกสาร NextAcc" — ลิงก์เปิด PDF จริงจาก NextAcc (cache มาฝั่งนี้แล้ว) + จำนวนไฟล์แนบ
            var litNext = (Literal)e.Row.FindControl("litNextAccDoc");
            if (litNext != null)
            {
                if (hasNextAcc == "1" && !string.IsNullOrEmpty(viewUrl))
                {
                    bool isLocalPdf = viewUrl.StartsWith("/");
                    string label = isLocalPdf ? "📄 ดูเอกสาร" : "📄 เปิดใน NextAcc ↗";
                    litNext.Text = $"<a href='{Server.HtmlEncode(viewUrl)}' target='_blank' rel='noopener' class='sync-badge completed' title='เปิดเอกสารจาก NextAcc'>{label}</a>";
                }
                else
                {
                    litNext.Text = "<span class='sync-badge none'>-</span>";
                }
            }

            // ── ไฟล์แนบ ──
            //   เอกสารที่สร้างจากระบบเรา (synced) → แสดงไฟล์ local อย่างเดียว (NextAcc มีไฟล์ชุดเดียวกันที่ sync ไป — ไม่ดึงมาซ้อน)
            //   เอกสารที่สร้างบน NextAcc โดยตรง (NextAcc-only) → แสดงไฟล์แนบจาก NextAcc
            var litAtt = (Literal)e.Row.FindControl("litAttachments");
            if (litAtt != null)
            {
                string whtUrl = SafeEval(e.Row.DataItem, "WhtCertUrl");
                bool isNaOnly = isNextAccOnly == "1";
                var localFiles = isNaOnly
                    ? new List<string>()
                    : GetLocalAttachments(docId, DataBinder.Eval(e.Row.DataItem, "Created_Date"));
                string naAtt = isNaOnly ? SafeEval(e.Row.DataItem, "NextAccAttUrls") : "";
                litAtt.Text = RenderAttachmentHtml(localFiles, naAtt, whtUrl);
            }

            var lblSync = (Label)e.Row.FindControl("lblSyncStatus");
            var btnSync = (Button)e.Row.FindControl("btnSync");
            if (lblSync == null || btnSync == null) return;

            // เอกสารที่สร้างบน NextAcc โดยตรง — ไม่มีสถานะ sync ของ TakeTime
            if (isNextAccOnly == "1")
            {
                lblSync.Text = "<span class='sync-badge completed'>บน NextAcc</span>";
                btnSync.Visible = false;
                return;
            }

            if (docStatus == "Cancel")
            {
                lblSync.Text = "<span class='sync-badge none'>-</span>";
                btnSync.Visible = false;
                return;
            }

            if (_syncStatusCache == null) LoadSyncStatusCache();

            if (_syncStatusCache.ContainsKey(docId))
            {
                var queueRow = _syncStatusCache[docId];
                string syncStatus = queueRow["Status"]?.ToString() ?? "";
                string queueId = queueRow["ID"]?.ToString() ?? "";

                switch (syncStatus)
                {
                    case "COMPLETED":
                        string nexaaccDocNum = queueRow.Table.Columns.Contains("Nexaacc_Document_Number")
                            ? queueRow["Nexaacc_Document_Number"]?.ToString() : "";
                        string nexaaccId = queueRow.Table.Columns.Contains("Nexaacc_Response_Id")
                            ? queueRow["Nexaacc_Response_Id"]?.ToString() : "";
                        string nexaaccDocType = queueRow.Table.Columns.Contains("Nexaacc_Document_Type")
                            ? queueRow["Nexaacc_Document_Type"]?.ToString() : "";
                        string displayLabel = !string.IsNullOrEmpty(nexaaccDocNum) ? nexaaccDocNum : $"#{queueId}";
                        string deepLink = BuildNexaaccLink(nexaaccId, nexaaccDocType);
                        if (!string.IsNullOrEmpty(deepLink))
                            lblSync.Text = $"<a href='{deepLink}' target='_blank' class='sync-badge completed' title='เปิดใน NextAcc'>✓ {Server.HtmlEncode(displayLabel)}</a>";
                        else
                            lblSync.Text = $"<span class='sync-badge completed' title='NextAcc ID: {Server.HtmlEncode(nexaaccId)}'>✓ {Server.HtmlEncode(displayLabel)}</span>";
                        btnSync.Visible = false;
                        break;
                    case "PENDING":
                    case "PROCESSING":
                        lblSync.Text = $"<span class='sync-badge pending'>รอดำเนินการ #{queueId}</span>";
                        btnSync.Visible = false;
                        break;
                    case "FAILED":
                        string err = queueRow["Error_Message"]?.ToString() ?? "";
                        if (err.Length > 40) err = err.Substring(0, 40) + "...";
                        lblSync.Text = $"<span class='sync-badge failed' title='{Server.HtmlEncode(err)}'>Failed #{queueId}</span>";
                        btnSync.Visible = true;
                        btnSync.Text = "🔄 Retry";
                        break;
                    default:
                        lblSync.Text = $"<span class='sync-badge pending'>{syncStatus} #{queueId}</span>";
                        btnSync.Visible = false;
                        break;
                }
            }
            else
            {
                lblSync.Text = "<span class='sync-badge none'>ยังไม่ sync</span>";
                btnSync.Visible = true;
            }
        }

        private Dictionary<string, string> _uidCache;

        private void BuildUidCacheFromGrid(DataTable gridData)
        {
            _uidCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (gridData == null || !gridData.Columns.Contains("UID")) return;
            foreach (DataRow r in gridData.Rows)
            {
                string id = r["ID"]?.ToString() ?? "";
                string uid = r["UID"] != DBNull.Value ? r["UID"]?.ToString() : "";
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(uid))
                    _uidCache[id] = uid;
            }
        }

        // ── NextAcc cache บนดิสก์ (PDF + ไฟล์แนบ + ใบหัก ณ ที่จ่าย) ที่ service เคยโหลดไว้ ──
        private class DiskNextAcc
        {
            public string PdfUrl;
            public List<string> AttUrls = new List<string>();
            public string WhtUrl;
            public bool HasAny => !string.IsNullOrEmpty(PdfUrl) || AttUrls.Count > 0 || !string.IsNullOrEmpty(WhtUrl);
        }

        /// <summary>
        /// อ่านไฟล์ NextAcc ที่ cache ไว้บนดิสก์ตามเลขเอกสาร TakeTime (ไม่เรียก API)
        /// โฟลเดอร์: {PaymentFolderPath}\NextAcc\{docId}\  (ตรงกับที่ AccountingSyncService เขียนไว้)
        /// </summary>
        private DiskNextAcc ReadCachedNextAccFromDisk(string docId)
        {
            try
            {
                if (string.IsNullOrEmpty(docId)) return null;
                string basePath = ConfigurationManager.AppSettings["PaymentFolderPath"];
                if (string.IsNullOrEmpty(basePath)) return null;

                string safe = docId;
                foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');

                string folder = Path.Combine(basePath, "NextAcc", safe);
                if (!Directory.Exists(folder)) return null;

                string relPrefix = "/Documents/Payment/NextAcc/" + safe;
                var result = new DiskNextAcc();

                string pdf = Path.Combine(folder, safe + ".pdf");
                if (File.Exists(pdf) && new FileInfo(pdf).Length > 0)
                    result.PdfUrl = relPrefix + "/" + safe + ".pdf";

                string wht = Path.Combine(folder, "wht.pdf");
                if (File.Exists(wht) && new FileInfo(wht).Length > 0)
                    result.WhtUrl = relPrefix + "/wht.pdf";

                foreach (var f in Directory.GetFiles(folder, "att*"))
                {
                    if (new FileInfo(f).Length > 0)
                        result.AttUrls.Add(relPrefix + "/" + Path.GetFileName(f));
                }

                return result.HasAny ? result : null;
            }
            catch { return null; }
        }

        private List<string> GetLocalAttachments(string docNum, object createdDateObj)
        {
            var urls = new List<string>();
            try
            {
                if (string.IsNullOrEmpty(docNum) || !docNum.StartsWith("PAY")) return urls;
                if (createdDateObj == null || createdDateObj == DBNull.Value) return urls;

                DateTime dt = Convert.ToDateTime(createdDateObj);
                string basePath = ConfigurationManager.AppSettings["PaymentFolderPath"];
                if (string.IsNullOrEmpty(basePath)) return urls;

                if (_uidCache == null) return urls;
                string uid;
                if (!_uidCache.TryGetValue(docNum, out uid) || string.IsNullOrEmpty(uid)) return urls;

                string year = dt.Year.ToString();
                string monthUnpadded = dt.Month.ToString();
                string monthPadded = dt.Month.ToString("00");
                string mainPdf = docNum + "_" + uid + ".pdf";
                string cancelPdf = docNum + "_" + uid + "_Cancel.pdf";

                foreach (string month in new[] { monthUnpadded, monthPadded })
                {
                    string folder = Path.Combine(basePath, year, month);
                    if (!Directory.Exists(folder)) continue;
                    foreach (string f in Directory.GetFiles(folder, docNum + "_" + uid + "*"))
                    {
                        string fn = Path.GetFileName(f);
                        if (fn.Equals(mainPdf, StringComparison.OrdinalIgnoreCase)) continue;
                        if (fn.Equals(cancelPdf, StringComparison.OrdinalIgnoreCase)) continue;
                        urls.Add("/Documents/Payment/" + year + "/" + month + "/" + fn);
                    }
                    if (urls.Count > 0) break;
                }
            }
            catch { }
            return urls;
        }

        private static bool IsImageFile(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string ext = Path.GetExtension(url).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".webp" || ext == ".bmp";
        }

        private static readonly System.Text.RegularExpressions.Regex _attachPrefixRx =
            new System.Text.RegularExpressions.Regex(
                @"^PAY\d+_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}_",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string StripAttachmentPrefix(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return fileName;
            string stripped = _attachPrefixRx.Replace(fileName, "");
            return string.IsNullOrEmpty(stripped) ? fileName : stripped;
        }

        private string RenderAttachmentHtml(List<string> localUrls, string nextAccAttUrlsCsv, string whtCertUrl = null)
        {
            var sb = new StringBuilder();
            var allItems = new List<KeyValuePair<string, string>>(); // url, source label

            foreach (var u in localUrls)
                allItems.Add(new KeyValuePair<string, string>(u, ""));

            if (!string.IsNullOrEmpty(nextAccAttUrlsCsv))
            {
                foreach (var u in nextAccAttUrlsCsv.Split('|'))
                {
                    if (!string.IsNullOrEmpty(u))
                        allItems.Add(new KeyValuePair<string, string>(u, "NextAcc"));
                }
            }

            bool hasWht = !string.IsNullOrEmpty(whtCertUrl);
            if (allItems.Count == 0 && !hasWht) return "<span style='color:#bbb;font-size:11px;'>-</span>";

            sb.Append("<div class='att-wrap'>");
            foreach (var item in allItems)
            {
                string url = item.Key;
                string src = item.Value;
                string encUrl = Server.HtmlEncode(url);
                string rawFileName = Path.GetFileName(url);
                string displayName = StripAttachmentPrefix(rawFileName);
                string srcBadge = !string.IsNullOrEmpty(src)
                    ? $"<span class='att-src-badge'>{Server.HtmlEncode(src)}</span>" : "";

                if (IsImageFile(url))
                {
                    sb.Append($"<div class='att-chip'><a href='{encUrl}' target='_blank' title='{Server.HtmlEncode(displayName)}'>"
                        + $"<img src='{encUrl}' class='att-thumb' alt='{Server.HtmlEncode(displayName)}' loading='lazy'/>"
                        + $"</a><span class='att-name'>{Server.HtmlEncode(displayName)}</span>{srcBadge}</div>");
                }
                else
                {
                    string icon = url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "📄" : "📎";
                    sb.Append($"<div class='att-chip'><a href='{encUrl}' target='_blank' class='att-link' title='{Server.HtmlEncode(displayName)}'>"
                        + $"{icon} <span class='att-name'>{Server.HtmlEncode(displayName)}</span>"
                        + $"</a>{srcBadge}</div>");
                }
            }

            if (hasWht)
            {
                string encWht = Server.HtmlEncode(whtCertUrl);
                sb.Append($"<div class='att-chip'><a href='{encWht}' target='_blank' class='att-link att-wht' title='ใบหัก ณ ที่จ่าย (50 ทวิ)'>"
                    + "🧾 <span class='att-name'>ใบหัก ณ ที่จ่าย</span></a></div>");
            }

            sb.Append("</div>");
            return sb.ToString();
        }

        private void HandleSyncVoucher(string docId)
        {
            try
            {
                // Anti-duplicate: in-session lock prevents re-POST/refresh from re-triggering
                string sessionKey = "SyncLock_" + docId;
                if (Session[sessionKey] is DateTime lockTime && (DateTime.Now - lockTime).TotalSeconds < 60)
                {
                    Response.Redirect(Request.RawUrl, false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
                Session[sessionKey] = DateTime.Now;

                var docParams = new Dictionary<string, object> { { "@ID", docId } };
                var dt = codeInstance.DatabaseQuerySafe(conn,
                    @"SELECT ap.ID, ap.Created_Date, ap.Total_Amount, ap.Vat, ap.Paid_How, ap.Paid_Type,
                             ISNULL(ap.WHT_Rate, 0) AS WHT_Rate, ISNULL(ap.WHT_Amount, 0) AS WHT_Amount,
                             ISNULL(v.Name, '-') AS Vendor_Name
                      FROM Account_Payment ap
                      LEFT JOIN Vendor v ON ap.Vendor_ID = v.ID
                      WHERE ap.ID = @ID", docParams);

                if (dt == null || dt.Rows.Count == 0)
                {
                    ShowError("ไม่พบเอกสาร: " + docId);
                    return;
                }

                var row = dt.Rows[0];
                decimal amount = Convert.ToDecimal(row["Total_Amount"]);
                string paymentMethod = row["Paid_How"]?.ToString() ?? "CASH";
                string expenseCategory = row["Paid_Type"]?.ToString() ?? "OTHER";
                string vendorName = row["Vendor_Name"]?.ToString() ?? "";
                DateTime docDate = Convert.ToDateTime(row["Created_Date"]);

                string description = "";
                DataTable detailDt = null;
                try
                {
                    detailDt = codeInstance.DatabaseQuerySafe(conn,
                        @"SELECT Number, Detail, Amount,
                                 ISNULL(Paid_Type_Name, N'') AS PaidTypeName,
                                 ISNULL(CAST(Nexaacc_AccountId AS NVARCHAR(50)), N'') AS NexaaccAccountId
                          FROM Account_Payment_Detail WHERE Payment_ID = @ID ORDER BY Number",
                        docParams);
                }
                catch
                {
                    detailDt = codeInstance.DatabaseQuerySafe(conn,
                        "SELECT Number, Detail, Amount FROM Account_Payment_Detail WHERE Payment_ID = @ID ORDER BY Number",
                        docParams);
                }
                if (detailDt?.Rows.Count > 0)
                    description = detailDt.Rows[0]["Detail"]?.ToString() ?? "";

                bool hasVat = Convert.ToDecimal(row["Vat"]) > 0;

                var sync = new AccountingSyncService(conn);
                // Cancel existing pending/failed entries so we always create fresh
                sync.PrepareResync(docId);

                var expenseLines = new List<Dictionary<string, object>>();
                if (detailDt != null && detailDt.Rows.Count > 0)
                {
                    bool hasPerLine = detailDt.Columns.Contains("PaidTypeName");
                    for (int i = 0; i < detailDt.Rows.Count; i++)
                    {
                        string lineCat = hasPerLine ? detailDt.Rows[i]["PaidTypeName"]?.ToString() : expenseCategory;
                        if (string.IsNullOrEmpty(lineCat)) lineCat = expenseCategory;
                        string lineAccId = hasPerLine ? detailDt.Rows[i]["NexaaccAccountId"]?.ToString() : null;
                        if (string.IsNullOrEmpty(lineAccId))
                            lineAccId = sync.LookupPaidTypeAccountId(lineCat);

                        expenseLines.Add(new Dictionary<string, object>
                        {
                            { "category", lineCat },
                            { "description", detailDt.Rows[i]["Detail"]?.ToString() ?? "" },
                            { "amount", Convert.ToDecimal(detailDt.Rows[i]["Amount"]) },
                            { "accountId", lineAccId ?? "" }
                        });
                    }
                }

                decimal cpWhtRate = row.Table.Columns.Contains("WHT_Rate") && row["WHT_Rate"] != DBNull.Value ? Convert.ToDecimal(row["WHT_Rate"]) : 0;
                decimal cpWhtAmount = row.Table.Columns.Contains("WHT_Amount") && row["WHT_Amount"] != DBNull.Value ? Convert.ToDecimal(row["WHT_Amount"]) : 0;

                long queueId = sync.EnqueuePaymentVoucher(0, expenseCategory, amount, paymentMethod,
                    docDate, description, vendorName, hasInputVat: hasVat,
                    whtRate: cpWhtRate, whtAmount: cpWhtAmount,
                    documentNumber: docId,
                    paymentAccountId: sync.LookupPaidHowAccountId(paymentMethod),
                    expenseAccountId: sync.LookupPaidTypeAccountId(expenseCategory),
                    expenseLines: expenseLines.Count > 0 ? expenseLines : null);

                if (queueId > 0)
                {
                    _syncStatusCache = null;
                    string sep = Request.RawUrl.Contains("?") ? "&" : "?";
                    Response.Redirect(Request.RawUrl + sep + "synced=" + queueId, false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
                else
                {
                    ShowError("ไม่สามารถสร้าง sync job ได้ — ตรวจสอบการตั้งค่า Accounting Integration");
                }
            }
            catch (Exception ex)
            {
                try { loggingService.LogException(ex, LoggingService.LogCategory.Accounting, $"Sync error: {docId}", GetCurrentUserId()); } catch { }
                ShowError("Sync ไม่สำเร็จ กรุณาลองใหม่");
            }
        }

        private void ShowError(string message)
        {
            string safe = HttpUtility.JavaScriptStringEncode(message);
            ScriptManager.RegisterStartupScript(this, GetType(), "error", $"alert('{safe}');", true);
        }

        private static string CsvSafe(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || value.StartsWith("=") || value.StartsWith("+") || value.StartsWith("-") || value.StartsWith("@"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private int? GetCurrentUserId()
        {
            try
            {
                if (Session["UserID"] != null)
                {
                    return Convert.ToInt32(Session["UserID"]);
                }
            }
            catch { }
            return null;
        }

        private string BuildNexaaccLink(string nexaaccId, string docType)
        {
            if (string.IsNullOrEmpty(nexaaccId)) return null;
            try
            {
                var config = new Integration.AccountingConfig(conn);
                if (!config.IsConfigured) return null;
                string baseUrl = config.RawBaseUrl.TrimEnd('/');
                string companyId = config.CompanyId.ToString();
                string path = "documents";
                switch ((docType ?? "").ToUpper())
                {
                    case "INVOICE": path = "invoices"; break;
                    case "EXPENSE": path = "expenses"; break;
                    case "JOURNAL": path = "journals"; break;
                    case "CREDIT_NOTE": path = "credit-notes"; break;
                    case "DEBIT_NOTE": path = "debit-notes"; break;
                }
                return $"{baseUrl}/{companyId}/{path}/{nexaaccId}";
            }
            catch { return null; }
        }
    }
}
