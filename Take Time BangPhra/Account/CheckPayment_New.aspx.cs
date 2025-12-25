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

                // Get vendor search filter
                string vendorSearch = txtVendorSearch.Text.Trim();

                // Calculate expenses by payment method
                try
                {
                    System.Diagnostics.Debug.WriteLine($"⚙️ Calling CalculateExpenses...");
                    CalculateExpenses(startDate, endDate, vendorSearch);
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
                LoadDetails(startDate, endDate, vendorSearch);
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

        private void CalculateExpenses(DateTime startDate, DateTime endDate, string vendorSearch = "")
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

                // Get all payments (with vendor filter if provided)
                var payments = GetAllPayments(startDate, endDate, status, vendorSearch);

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

        private DataTable GetAllPayments(DateTime startDate, DateTime endDate, string status, string vendorSearch = "")
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

            System.Diagnostics.Debug.WriteLine($"📋 GetAllPayments Query:");
            System.Diagnostics.Debug.WriteLine($"   User: {Session["User"]?.ToString() ?? "Unknown"}");
            System.Diagnostics.Debug.WriteLine($"   @StartDate = {startDate:yyyy-MM-dd HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"   @EndDate = {endDate:yyyy-MM-dd HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"   @Status = {status}");
            System.Diagnostics.Debug.WriteLine($"   @VendorSearch = {vendorSearch}");

            var result = codeInstance.DatabaseQuerySafe(conn, query, parameters);
            System.Diagnostics.Debug.WriteLine($"   ✅ Result: {result?.Rows.Count ?? 0} rows");

            return result;
        }

        private void LoadDetails(DateTime startDate, DateTime endDate, string vendorSearch = "")
        {
            DataTable dt = null;
            try
            {
                System.Diagnostics.Debug.WriteLine($"📊 LoadDetails called:");
                System.Diagnostics.Debug.WriteLine($"   Start: {startDate:yyyy-MM-dd HH:mm:ss}");
                System.Diagnostics.Debug.WriteLine($"   End: {endDate:yyyy-MM-dd HH:mm:ss}");
                System.Diagnostics.Debug.WriteLine($"   VendorSearch: {vendorSearch}");

                // Show all documents (both Normal and Cancel) with vendor filter
                dt = GetAllPayments(startDate, endDate, "%", vendorSearch);
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

                // Detail records (with vendor filter)
                string vendorSearch = txtVendorSearch.Text.Trim();
                var dt = GetAllPayments(startDate, endDate, "%", vendorSearch);
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
                string docMonth = docNum.Length >= 7 ? docNum.Substring(5, 2) : "";

                if (docType == "PAY")
                {
                    string path = ConfigurationManager.AppSettings["PaymentFolderPath"] + "\\" + docYear + "\\" + docMonth;

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

                    // Delete payment files
                    if (Directory.Exists(path))
                    {
                        string[] files = Directory.GetFiles(path, docNum + "*");
                        foreach (string file in files)
                        {
                            File.Delete(file);
                        }
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
                string docMonth = docNum.Length >= 7 ? docNum.Substring(5, 2) : "";

                System.Diagnostics.Debug.WriteLine($"   Parsed: Type={docType}, Year={docYear}, Month={docMonth}");

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

                    // Build file paths
                    List<string> filesToCheck = new List<string>();

                    if (docStatus == "Cancel")
                    {
                        if (!string.IsNullOrEmpty(uid))
                        {
                            filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}_{uid}_Cancel.pdf");
                        }
                        filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}_Cancel.pdf");
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(uid))
                        {
                            filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}_{uid}.pdf");
                        }
                        filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}.pdf");
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
    }
}
