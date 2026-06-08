using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
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

                        // Show success alert from Post-Redirect-Get sync
                        string syncedQ = Request.QueryString["synced"];
                        if (!string.IsNullOrEmpty(syncedQ))
                        {
                            ScriptManager.RegisterStartupScript(this, GetType(), "syncOk",
                                $"alert('ส่งเข้าคิว sync สำเร็จ (Queue #{syncedQ})\\nดูสถานะได้ที่หน้า Accounting Integration');", true);
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
                    // Use date range
                    startDate = Convert.ToDateTime(txtStartDate.Text);
                    endDate = Convert.ToDateTime(txtEndDate.Text);

                    System.Diagnostics.Debug.WriteLine($"🔍 Search Mode: Date Range");
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

                // Calculate expenses by payment method
                try
                {
                    System.Diagnostics.Debug.WriteLine($"⚙️ Calling CalculateExpenses...");
                    CalculateExpenses(startDate, endDate, vendorSearch, expenseType, minAmount);
                    System.Diagnostics.Debug.WriteLine($"✅ CalculateExpenses completed");
                }
                catch (Exception calcEx)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ CalculateExpenses failed: {calcEx.Message}");
                    lblDateRange.Text += $" <span style='color: red;'>[CalculateExpenses Error: {calcEx.Message}]</span>";
                    ShowError($"เกิดข้อผิดพลาดในการคำนวณค่าใช้จ่าย:\\n{calcEx.Message}");
                }

                // Load details
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

                ShowError("เกิดข้อผิดพลาด: " + ex.Message);
            }
        }

        private void CalculateExpenses(DateTime startDate, DateTime endDate, string vendorSearch = "", string expenseType = "", decimal minAmount = 0)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"💸 CalculateExpenses started");

                // Always calculate expenses for Normal status only (exclude Cancel)
                string status = "Normal";

                // Initialize totals
                decimal cashTotal = 0, kbankTotal = 0, ktbTotal = 0, directorTotal = 0, otherTotal = 0;
                int cashCount = 0, kbankCount = 0, ktbCount = 0, directorCount = 0, otherCount = 0;
                decimal totalVAT = 0;
                int docCount = 0;
                decimal grandTotal = 0;

                // Get all payments (with all filters)
                var payments = GetAllPayments(startDate, endDate, status, vendorSearch, expenseType, minAmount);

                if (payments != null && payments.Rows.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"   📊 Processing {payments.Rows.Count} payment records...");

                    foreach (DataRow row in payments.Rows)
                    {
                        string paidHow = row["Paid_How"]?.ToString() ?? ""; // วิธีชำระ
                        string paidType = row["Paid_Type"]?.ToString() ?? ""; // ประเภทค่าใช้จ่าย
                        decimal amount = row["Total_Amount"] != DBNull.Value ? Convert.ToDecimal(row["Total_Amount"]) : 0;
                        decimal vat = row["Vat"] != DBNull.Value ? Convert.ToDecimal(row["Vat"]) : 0;

                        System.Diagnostics.Debug.WriteLine($"   Record: Paid_How='{paidHow}', Paid_Type='{paidType}', Amount={amount:N2}");

                        // Add to grand total regardless
                        grandTotal += amount;

                        // Count payment methods based on Paid_How (not Paid_Type!)
                        bool categorized = false;

                        if (paidHow.Contains("เงินสด") || paidHow.Contains("สด"))
                        {
                            cashTotal += amount;
                            cashCount++;
                            categorized = true;
                        }
                        if (paidHow.Contains("กสิกร") || paidHow.Contains("KBANK"))
                        {
                            kbankTotal += amount;
                            kbankCount++;
                            categorized = true;
                        }
                        if (paidHow.Contains("กรุงไทย") || paidHow.Contains("KTB"))
                        {
                            ktbTotal += amount;
                            ktbCount++;
                            categorized = true;
                        }
                        if (paidHow.Contains("กรรมการ") || paidHow.Contains("Director"))
                        {
                            directorTotal += amount;
                            directorCount++;
                            categorized = true;
                        }

                        if (!categorized)
                        {
                            otherTotal += amount;
                            otherCount++;
                            System.Diagnostics.Debug.WriteLine($"   ⚠️ Uncategorized payment method: '{paidHow}'");
                        }

                        totalVAT += vat;
                        docCount++;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"   ⚠️ No payment records found!");
                }

                // Update UI
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

                System.Diagnostics.Debug.WriteLine($"   💵 Cash: {cashTotal:N2} ({cashCount})");
                System.Diagnostics.Debug.WriteLine($"   🏦 KBANK: {kbankTotal:N2} ({kbankCount})");
                System.Diagnostics.Debug.WriteLine($"   🏦 KTB: {ktbTotal:N2} ({ktbCount})");
                System.Diagnostics.Debug.WriteLine($"   👔 Director: {directorTotal:N2} ({directorCount})");
                System.Diagnostics.Debug.WriteLine($"   ❓ Other: {otherTotal:N2} ({otherCount})");
                System.Diagnostics.Debug.WriteLine($"   💰 Grand Total: {grandTotal:N2} ({totalCount})");

                // Log expense calculation result
                try
                {
                    string breakdown = $"Cash: {cashTotal:N2} ({cashCount})\n" +
                                     $"KBANK: {kbankTotal:N2} ({kbankCount})\n" +
                                     $"KTB: {ktbTotal:N2} ({ktbCount})\n" +
                                     $"Director: {directorTotal:N2} ({directorCount})\n" +
                                     $"Other: {otherTotal:N2} ({otherCount})\n" +
                                     $"Document Count: {docCount}\n" +
                                     $"Total VAT: {totalVAT:N2}";

                    loggingService.LogAccountingOperation(
                        "ExpenseCalculationResult",
                        $"Total: {grandTotal:N2}\n{breakdown}",
                        true,
                        GetCurrentUserId());
                }
                catch { /* Ignore logging errors */ }

                System.Diagnostics.Debug.WriteLine($"✅ CalculateExpenses completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ CalculateExpenses FAILED: {ex.Message}");
                throw;
            }
        }

        private DataTable GetAllPayments(DateTime startDate, DateTime endDate, string status, string vendorSearch = "", string expenseType = "", decimal minAmount = 0)
        {
            // Get all payment vouchers in the date range
            // For payroll payments (เงินเดือน), show employee name from Payroll_Records via VoucherNumber
            string query = @"
                SELECT ap.ID, ap.Created_Date, ap.Paid_How, ap.Paid_Type, ap.Total_Amount, ap.Vat,
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
                WHERE CAST(ap.Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                  AND CAST(ap.Created_Date AS DATE) <= CAST(@EndDate AS DATE)
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

                // ดึงเอกสาร + ไฟล์แนบจาก NextAcc มาเก็บที่ฝั่ง TakeTime อัตโนมัติตามช่วงที่ค้นหา
                // (cache ลงดิสก์ ใช้ซ้ำได้ — โหลดเฉพาะรายการที่ sync แล้วและยังไม่เคยโหลด)
                PrefetchNextAccDocuments(dt);

                // Bind to GridView
                gvDetails.DataSource = dt;
                gvDetails.DataBind();
                System.Diagnostics.Debug.WriteLine($"   ✅ GridView.DataBind() completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Error in LoadDetails: {ex.Message}");
                lblDateRange.Text += $" <span style='color: red;'>[LoadDetails Error: {ex.Message}]</span>";
                ShowError($"เกิดข้อผิดพลาดในการโหลดข้อมูล:\\n{ex.Message}");

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
                    startDate = Convert.ToDateTime(txtStartDate.Text);
                    endDate = Convert.ToDateTime(txtEndDate.Text);
                }

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
                        string paidHow = row["Paid_How"]?.ToString() ?? ""; // วิธีชำระ (payment method)
                        string amount = row["Total_Amount"] != DBNull.Value ? Convert.ToDecimal(row["Total_Amount"]).ToString("N2") : "0.00";
                        string vat = row["Vat"] != DBNull.Value ? Convert.ToDecimal(row["Vat"]).ToString("N2") : "0.00";
                        string status = row["Status"]?.ToString() ?? "";
                        string createdBy = row["Created_By"]?.ToString() ?? "";

                        csv.AppendLine($"{docId},{date},{vendor},{paidHow},{amount},{vat},{status},{createdBy}");
                    }
                }

                // Send file
                Response.Clear();
                Response.ContentType = "text/csv";
                Response.ContentEncoding = Encoding.UTF8;
                Response.Charset = "UTF-8";
                Response.AddHeader("Content-Disposition", $"attachment;filename=รายงานค่าใช้จ่าย_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv");
                Response.Write(csv.ToString());
                Response.End();
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาดในการ export: " + ex.Message);
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
                string docNum = gvDetails.Rows[e.RowIndex].Cells[3].Text;
                string docType = docNum.Length >= 3 ? docNum.Substring(0, 3) : "";
                string docYear = docNum.Length >= 5 ? "20" + docNum.Substring(3, 2) : "";
                string docMonthPadded = docNum.Length >= 7 ? docNum.Substring(5, 2) : "";
                // Convert to non-padded month (PaymentVoucher stores as "1", "2", not "01", "02")
                string docMonth = int.TryParse(docMonthPadded, out int monthNum) ? monthNum.ToString() : docMonthPadded;

                if (docType == "PAY")
                {
                    string basePath = ConfigurationManager.AppSettings["PaymentFolderPath"];
                    // Check both non-padded and padded paths
                    string path = basePath + "\\" + docYear + "\\" + docMonth;
                    string pathPadded = basePath + "\\" + docYear + "\\" + docMonthPadded;

                    // SECURE: Delete payment details with parameterized query
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

                    try
                    {
                        var sync = new Integration.AccountingSyncService(conn);
                        sync.EnqueueVoidPaymentVoucher(docNum);
                    }
                    catch (Exception accEx)
                    {
                        codeInstance.Logs(conn, "Accounting Sync", $"Void voucher error (CheckPayment_New): docNum={docNum} {accEx.Message}", "SYSTEM");
                    }

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
                ShowError("ลบเอกสารไม่สำเร็จ: " + ex.Message);
            }
        }

        protected void gvDetails_SelectedIndexChanging(object sender, GridViewSelectEventArgs e)
        {
            try
            {
                string docStatus = gvDetails.Rows[e.NewSelectedIndex].Cells[11].Text; // Status column (index เพิ่มเพราะเพิ่ม Paid_Type column)
                string docNum = gvDetails.Rows[e.NewSelectedIndex].Cells[3].Text; // ID column

                System.Diagnostics.Debug.WriteLine($"📄 Opening document: {docNum}, Status: {docStatus}");

                // Parse document info
                string docType = docNum.Length >= 3 ? docNum.Substring(0, 3) : "";
                string docYear = docNum.Length >= 5 ? "20" + docNum.Substring(3, 2) : "";
                string docMonthPadded = docNum.Length >= 7 ? docNum.Substring(5, 2) : "";
                // Convert to non-padded month (PaymentVoucher stores as "1", "2", not "01", "02")
                string docMonth = int.TryParse(docMonthPadded, out int monthNum) ? monthNum.ToString() : docMonthPadded;

                System.Diagnostics.Debug.WriteLine($"   Parsed: Type={docType}, Year={docYear}, Month={docMonth} (from {docMonthPadded})");

                if (docType == "PAY")
                {
                    // ── เอกสารอย่างเป็นทางการจาก NextAcc มาก่อน (ทั้งเอกสารปกติและยกเลิก) ──
                    // ดาวน์โหลด PDF จริงจาก NextAcc (+ ไฟล์แนบ) มาเก็บที่ฝั่ง TakeTime แล้วเปิดดู
                    // แทน PDF ที่ระบบ TakeTime ออกเอง
                    //   ปกติ → เอกสารใบสำคัญจ่ายต้นฉบับ
                    //   ยกเลิก → ใบเพิ่มหนี้/เอกสารยกเลิกจาก NextAcc (หรือต้นฉบับ + ลายน้ำ "ยกเลิก")
                    bool isCancelledDoc = docStatus == "Cancel";
                    try
                    {
                        var syncDoc = new AccountingSyncService(conn);
                        var cached = System.Threading.Tasks.Task.Run(() =>
                            syncDoc.DownloadVoucherDocumentFromNextAccAsync(docNum, false, isCancelledDoc)).Result;
                        if (cached != null && cached.Found && !string.IsNullOrEmpty(cached.PdfRelativeUrl))
                        {
                            System.Diagnostics.Debug.WriteLine($"   ✅ NextAcc PDF: {cached.PdfRelativeUrl} (cancelled={isCancelledDoc}, attachments={cached.AttachmentCount})");
                            Response.Redirect(cached.PdfRelativeUrl);
                            return;
                        }
                        System.Diagnostics.Debug.WriteLine($"   ⚠️ NextAcc PDF unavailable ({cached?.Message}) — fallback to local PDF");
                    }
                    catch (Exception nexEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"   ⚠️ NextAcc PDF fetch failed: {nexEx.Message} — fallback to local PDF");
                    }

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
                ShowError("เปิดเอกสารไม่สำเร็จ:\\n" + ex.Message);
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
                    string docNum = gvDetails.Rows[rowIndex].Cells[3].Text;
                    string docType = docNum.Length >= 3 ? docNum.Substring(0, 3) : "";

                    if (docType == "PAY")
                    {
                        // SECURE: Get payment UID with parameterized query
                        var uidParams = new Dictionary<string, object>
                        {
                            { "@ID", docNum }
                        };
                        string uid = codeInstance.DatabaseQuerySafe(conn,
                            "SELECT [UID] FROM [Taketime].[dbo].[Account_Payment] WHERE ID = @ID",
                            uidParams).Rows[0][0].ToString();
                        Response.Redirect("/Account/PaymentVoucher?command=edit&uid=" + uid);
                    }
                    else
                    {
                        ShowError("เอกสารนี้ไม่ใช่ใบสำคัญจ่าย");
                    }
                }
                catch (Exception ex)
                {
                    ShowError("แก้ไขเอกสารไม่สำเร็จ: " + ex.Message);
                }
            }
        }

        // ──────────────────────────────────────────────
        // Accounting Sync Status
        // ──────────────────────────────────────────────

        private Dictionary<string, DataRow> _syncStatusCache;
        private Dictionary<string, NextAccCachedDocument> _nextAccDocs;

        /// <summary>
        /// ดึงเอกสารฝั่งจ่ายอย่างเป็นทางการ (PDF + ไฟล์แนบ) จาก NextAcc มาเก็บที่ฝั่ง TakeTime
        /// อัตโนมัติสำหรับทุกใบสำคัญจ่ายในผลการค้นหา. โหลดแบบขนาน (จำกัด 3 พร้อมกัน) และ
        /// ใช้ cache บนดิสก์ — รายการที่โหลดแล้วจะไม่โหลดซ้ำ. ผลลัพธ์ใช้แสดงคอลัมน์ "เอกสาร NextAcc"
        /// </summary>
        private void PrefetchNextAccDocuments(DataTable dt)
        {
            _nextAccDocs = new Dictionary<string, NextAccCachedDocument>(StringComparer.OrdinalIgnoreCase);
            if (dt == null || dt.Rows.Count == 0) return;

            try
            {
                var config = new AccountingConfig(conn);
                if (!config.IsConfigured || !config.Enabled) return;

                var items = new List<KeyValuePair<string, bool>>();
                foreach (DataRow row in dt.Rows)
                {
                    string docNum = row["ID"]?.ToString() ?? "";
                    if (docNum.Length < 3 || docNum.Substring(0, 3) != "PAY") continue;
                    bool cancelled = (row["Status"]?.ToString() ?? "") == "Cancel";
                    items.Add(new KeyValuePair<string, bool>(docNum, cancelled));
                }
                if (items.Count == 0) return;

                var results = System.Threading.Tasks.Task.Run(async () =>
                {
                    var sync = new AccountingSyncService(conn);
                    var bag = new System.Collections.Concurrent.ConcurrentDictionary<string, NextAccCachedDocument>(StringComparer.OrdinalIgnoreCase);
                    using (var sem = new System.Threading.SemaphoreSlim(3))
                    {
                        var tasks = items.Select(async it =>
                        {
                            await sem.WaitAsync().ConfigureAwait(false);
                            try
                            {
                                var r = await sync.DownloadVoucherDocumentFromNextAccAsync(it.Key, false, it.Value).ConfigureAwait(false);
                                if (r != null) bag[it.Key] = r;
                            }
                            catch { /* per-doc failure ไม่ให้ล้มทั้งหน้า */ }
                            finally { sem.Release(); }
                        });
                        await System.Threading.Tasks.Task.WhenAll(tasks).ConfigureAwait(false);
                    }
                    return bag;
                }).Result;

                foreach (var kv in results) _nextAccDocs[kv.Key] = kv.Value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PrefetchNextAccDocuments error: " + ex.Message);
            }
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

            string docId = DataBinder.Eval(e.Row.DataItem, "ID")?.ToString() ?? "";
            string docStatus = DataBinder.Eval(e.Row.DataItem, "Status")?.ToString() ?? "";

            // คอลัมน์ "เอกสาร NextAcc" — ลิงก์เปิด PDF จริงจาก NextAcc (cache มาฝั่งนี้แล้ว) + จำนวนไฟล์แนบ
            var litNext = (Literal)e.Row.FindControl("litNextAccDoc");
            if (litNext != null)
            {
                if (_nextAccDocs != null && _nextAccDocs.TryGetValue(docId, out var nd)
                    && nd != null && nd.Found && !string.IsNullOrEmpty(nd.PdfRelativeUrl))
                {
                    string att = nd.AttachmentCount > 0
                        ? $" <span class='sync-badge none' title='ไฟล์แนบ {nd.AttachmentCount} ไฟล์'>📎{nd.AttachmentCount}</span>"
                        : "";
                    litNext.Text = $"<a href='{Server.HtmlEncode(nd.PdfRelativeUrl)}' target='_blank' rel='noopener' class='sync-badge completed' title='เปิดเอกสารจาก NextAcc'>📄 ดูเอกสาร</a>{att}";
                }
                else
                {
                    litNext.Text = "<span class='sync-badge none'>-</span>";
                }
            }

            var lblSync = (Label)e.Row.FindControl("lblSyncStatus");
            var btnSync = (Button)e.Row.FindControl("btnSync");
            if (lblSync == null || btnSync == null) return;

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
                ShowError("Sync error: " + ex.Message);
            }
        }

        private void ShowError(string message)
        {
            ScriptManager.RegisterStartupScript(this, GetType(), "error", $"alert('{message}');", true);
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
