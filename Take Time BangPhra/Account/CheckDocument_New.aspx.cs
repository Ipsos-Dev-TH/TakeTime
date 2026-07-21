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
    public partial class CheckDocument_New : System.Web.UI.Page
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
                    // AJAX: ดึง JE + เอกสารของใบเสร็จจาก NextAcc มาแสดง (read-only) — ตอบ JSON แล้วจบ
                    if (string.Equals(Request.QueryString["action"], "viewJE", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleViewJE(Request.QueryString["doc"]);
                        return;
                    }

                    if (!IsPostBack)
                    {
                        if (Request.QueryString["deleted"] == "1")
                            ClientScript.RegisterStartupScript(this.GetType(), "delok", "alert('✅ ลบเอกสารเรียบร้อยแล้ว');", true);
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
            catch (System.Threading.ThreadAbortException) { throw; }   // Response.End/Redirect (เช่น viewJE JSON) — อย่ากลืน
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

        /// <summary>AJAX handler: ดึง "ทั้ง JE และเอกสาร" ของใบเสร็จจาก NextAcc → ตอบ JSON ให้ modal แสดง</summary>
        private void HandleViewJE(string doc)
        {
            Response.Clear();
            Response.ContentType = "application/json; charset=utf-8";
            try
            {
                if (string.IsNullOrWhiteSpace(doc))
                {
                    Response.Write("{\"success\":false,\"message\":\"ไม่ระบุเลขที่เอกสาร\"}");
                }
                else
                {
                    Server.ScriptTimeout = 120;
                    var svc = new Take_Time_BangPhra.Integration.AccountingSyncService(conn);
                    Take_Time_BangPhra.Integration.AccountingSyncService.ReceiptAccountingView view = null;
                    var task = System.Threading.Tasks.Task.Run(() => svc.GetReceiptAccountingViewAsync(doc.Trim()));
                    if (task.Wait(60000)) view = task.Result;
                    else view = new Take_Time_BangPhra.Integration.AccountingSyncService.ReceiptAccountingView
                    { Success = false, Message = "NextAcc ตอบช้าเกิน 60 วินาที — ลองใหม่อีกครั้ง" };

                    Response.Write(new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(view));
                }
            }
            catch (Exception ex)
            {
                Response.Write("{\"success\":false,\"message\":" +
                    new System.Web.Script.Serialization.JavaScriptSerializer().Serialize("ผิดพลาด: " + ex.Message) + "}");
            }
            Response.End();
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
                lblDateRange.Text = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy} <small style='color: #999;'>(Debug: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd})</small>";

                // Log revenue calculation request (gracefully handle if System_Logs doesn't exist)
                try
                {
                    loggingService.LogAccountingOperation(
                        "RevenueCalculationRequest",
                        $"Date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                        true,
                        GetCurrentUserId());
                }
                catch { /* Ignore logging errors */ }

                // Calculate revenue by category (always use Normal status, never include Cancel)
                try
                {
                    System.Diagnostics.Debug.WriteLine($"⚙️ Calling CalculateRevenue...");
                    CalculateRevenue(startDate, endDate);
                    System.Diagnostics.Debug.WriteLine($"✅ CalculateRevenue completed");
                }
                catch (Exception calcEx)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ CalculateRevenue failed: {calcEx.Message}");
                    lblDateRange.Text += $" <span style='color: red;'>[CalculateRevenue Error: {calcEx.Message}]</span>";
                    ShowError($"เกิดข้อผิดพลาดในการคำนวณรายได้:\n{calcEx.Message}\n\nStack:\n{calcEx.StackTrace}");
                    // Continue to LoadDetails even if CalculateRevenue fails
                }

                // Load details (show all documents including Cancel)
                System.Diagnostics.Debug.WriteLine($"⚙️ Calling LoadDetails...");
                LoadDetails(startDate, endDate);
                System.Diagnostics.Debug.WriteLine($"✅ LoadDetails completed");

                // Show validation
                try
                {
                    ValidateTotal();
                }
                catch (Exception valEx)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ ValidateTotal failed: {valEx.Message}");
                    // Ignore validation errors
                }
            }
            catch (Exception ex)
            {
                // Log exception (gracefully handle if System_Logs doesn't exist)
                try
                {
                    loggingService.LogException(ex, LoggingService.LogCategory.Revenue,
                        "Revenue calculation failed", GetCurrentUserId());
                }
                catch { /* Ignore logging errors */ }

                ShowError("เกิดข้อผิดพลาด: " + ex.Message + "\n\nDetails: " + ex.StackTrace);
            }
        }

        private void CalculateRevenue(DateTime startDate, DateTime endDate)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"💰 CalculateRevenue started");

                // Always calculate revenue for Normal status only (exclude Cancel)
                string status = "Normal";

                // Initialize all totals
                decimal cat1Cash = 0, cat1KBANK = 0, cat1KTB = 0, cat1Director = 0;
                decimal cat2Cash = 0, cat2KBANK = 0, cat2KTB = 0, cat2Director = 0;
                decimal cat3Cash = 0, cat3KBANK = 0, cat3KTB = 0, cat3Director = 0;
                decimal cat4Cash = 0, cat4KBANK = 0, cat4KTB = 0, cat4Director = 0;
                decimal totalVAT = 0;
                int docCount = 0;

                // Category 1: Payments made in date range for reservations that checked-in in date range
                try
                {
                    var cat1Data = GetCategory1Revenue(startDate, endDate, status);
                    System.Diagnostics.Debug.WriteLine($"Category 1 (Payment_History): {cat1Data?.Rows.Count ?? 0} rows");

                    // ⚠️ Fallback: ถ้า Payment_History ไม่มีข้อมูล ให้ใช้ Account_Receipt
                    if (cat1Data == null || cat1Data.Rows.Count == 0)
                    {
                        cat1Data = GetCategory1RevenueFallback(startDate, endDate, status);
                        System.Diagnostics.Debug.WriteLine($"Category 1 (Fallback Account_Receipt): {cat1Data?.Rows.Count ?? 0} rows");
                    }

                    if (cat1Data != null)
                    {
                        cat1Cash = GetAmountByPaymentMethod(cat1Data, 2);
                        cat1KBANK = GetAmountByPaymentMethod(cat1Data, 1);
                        cat1KTB = GetAmountByPaymentMethod(cat1Data, 4);
                        cat1Director = GetAmountByPaymentMethod(cat1Data, 3);
                        System.Diagnostics.Debug.WriteLine($"   Cat1: Cash={cat1Cash:N2}, KBANK={cat1KBANK:N2}, KTB={cat1KTB:N2}, Director={cat1Director:N2}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"   ❌ Category 1 failed: {ex.Message}");
                }

                // Category 2: Payments made in date range for reservations that checked-in OUTSIDE date range
                try
                {
                    var cat2Data = GetCategory2Revenue(startDate, endDate, status);
                    System.Diagnostics.Debug.WriteLine($"Category 2 (Payment_History): {cat2Data?.Rows.Count ?? 0} rows");

                    // ⚠️ Fallback: ถ้า Payment_History ไม่มีข้อมูล ให้ใช้ Account_Receipt
                    if (cat2Data == null || cat2Data.Rows.Count == 0)
                    {
                        cat2Data = GetCategory2RevenueFallback(startDate, endDate, status);
                        System.Diagnostics.Debug.WriteLine($"Category 2 (Fallback Account_Receipt): {cat2Data?.Rows.Count ?? 0} rows");
                    }

                    if (cat2Data != null)
                    {
                        cat2Cash = GetAmountByPaymentMethod(cat2Data, 2);
                        cat2KBANK = GetAmountByPaymentMethod(cat2Data, 1);
                        cat2KTB = GetAmountByPaymentMethod(cat2Data, 4);
                        cat2Director = GetAmountByPaymentMethod(cat2Data, 3);
                        System.Diagnostics.Debug.WriteLine($"   Cat2: Cash={cat2Cash:N2}, KBANK={cat2KBANK:N2}, KTB={cat2KTB:N2}, Director={cat2Director:N2}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"   ❌ Category 2 failed: {ex.Message}");
                }

                // Category 3: Product sales
                try
                {
                    var cat3Data = GetCategory3Revenue(startDate, endDate, status);
                    System.Diagnostics.Debug.WriteLine($"Category 3: {cat3Data?.Rows.Count ?? 0} rows");

                    if (cat3Data != null)
                    {
                        cat3Cash = GetAmountByPaymentMethod(cat3Data, 2);
                        cat3KBANK = GetAmountByPaymentMethod(cat3Data, 1);
                        cat3KTB = GetAmountByPaymentMethod(cat3Data, 4);
                        cat3Director = GetAmountByPaymentMethod(cat3Data, 3);
                        System.Diagnostics.Debug.WriteLine($"   Cat3: Cash={cat3Cash:N2}, KBANK={cat3KBANK:N2}, KTB={cat3KTB:N2}, Director={cat3Director:N2}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"   ❌ Category 3 failed: {ex.Message}");
                }

                // Category 4: Others
                try
                {
                    var cat4Data = GetCategory4Revenue(startDate, endDate, status);
                    System.Diagnostics.Debug.WriteLine($"Category 4: {cat4Data?.Rows.Count ?? 0} rows");

                    if (cat4Data != null)
                    {
                        cat4Cash = GetAmountByPaymentMethod(cat4Data, 2);
                        cat4KBANK = GetAmountByPaymentMethod(cat4Data, 1);
                        cat4KTB = GetAmountByPaymentMethod(cat4Data, 4);
                        cat4Director = GetAmountByPaymentMethod(cat4Data, 3);
                        System.Diagnostics.Debug.WriteLine($"   Cat4: Cash={cat4Cash:N2}, KBANK={cat4KBANK:N2}, KTB={cat4KTB:N2}, Director={cat4Director:N2}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"   ❌ Category 4 failed: {ex.Message}");
                }

                // Update UI - Category 1
                lblCat1Cash.Text = cat1Cash.ToString("N2");
                lblCat1KBANK.Text = cat1KBANK.ToString("N2");
                lblCat1KTB.Text = cat1KTB.ToString("N2");
                lblCat1Director.Text = cat1Director.ToString("N2");
                lblCat1Total.Text = (cat1Cash + cat1KBANK + cat1KTB + cat1Director).ToString("N2");

                // Update UI - Category 2
                lblCat2Cash.Text = cat2Cash.ToString("N2");
                lblCat2KBANK.Text = cat2KBANK.ToString("N2");
                lblCat2KTB.Text = cat2KTB.ToString("N2");
                lblCat2Director.Text = cat2Director.ToString("N2");
                lblCat2Total.Text = (cat2Cash + cat2KBANK + cat2KTB + cat2Director).ToString("N2");

                // Update UI - Category 3
                lblCat3Cash.Text = cat3Cash.ToString("N2");
                lblCat3KBANK.Text = cat3KBANK.ToString("N2");
                lblCat3KTB.Text = cat3KTB.ToString("N2");
                lblCat3Director.Text = cat3Director.ToString("N2");
                lblCat3Total.Text = (cat3Cash + cat3KBANK + cat3KTB + cat3Director).ToString("N2");

                // Update UI - Category 4
                lblCat4Cash.Text = cat4Cash.ToString("N2");
                lblCat4KBANK.Text = cat4KBANK.ToString("N2");
                lblCat4KTB.Text = cat4KTB.ToString("N2");
                lblCat4Director.Text = cat4Director.ToString("N2");
                lblCat4Total.Text = (cat4Cash + cat4KBANK + cat4KTB + cat4Director).ToString("N2");

                // Update UI - Totals
                decimal totalCash = cat1Cash + cat2Cash + cat3Cash + cat4Cash;
                decimal totalKBANK = cat1KBANK + cat2KBANK + cat3KBANK + cat4KBANK;
                decimal totalKTB = cat1KTB + cat2KTB + cat3KTB + cat4KTB;
                decimal totalDirector = cat1Director + cat2Director + cat3Director + cat4Director;

                lblTotalCash.Text = totalCash.ToString("N2");
                lblTotalKBANK.Text = totalKBANK.ToString("N2");
                lblTotalKTB.Text = totalKTB.ToString("N2");
                lblTotalDirector.Text = totalDirector.ToString("N2");
                lblGrandTotal.Text = (totalCash + totalKBANK + totalKTB + totalDirector).ToString("N2");

                System.Diagnostics.Debug.WriteLine($"   💵 Totals: Cash={totalCash:N2}, KBANK={totalKBANK:N2}, KTB={totalKTB:N2}, Director={totalDirector:N2}");
                System.Diagnostics.Debug.WriteLine($"   💰 Grand Total: {(totalCash + totalKBANK + totalKTB + totalDirector):N2}");

                // Get VAT and document count (count all documents regardless of status to match GridView)
                try
                {
                    var allData = GetAllReceipts(startDate, endDate, "%");
                    if (allData != null)
                    {
                        foreach (DataRow row in allData.Rows)
                        {
                            totalVAT += row["Vat"] != DBNull.Value ? Convert.ToDecimal(row["Vat"]) : 0;
                            docCount++;
                        }
                    }

                    lblTotalVAT.Text = totalVAT.ToString("N2");
                    lblDocCount.Text = docCount.ToString();

                    System.Diagnostics.Debug.WriteLine($"   📄 Documents: {docCount} (all status), VAT: {totalVAT:N2}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"   ⚠️ VAT/DocCount calculation failed: {ex.Message}");
                    lblTotalVAT.Text = "0.00";
                    lblDocCount.Text = "0";
                }

                // Log revenue calculation result (gracefully handle logging errors)
                try
                {
                    decimal grandTotal = totalCash + totalKBANK + totalKTB + totalDirector;
                    string breakdown = $"Category 1: {(cat1Cash + cat1KBANK + cat1KTB + cat1Director):N2}\n" +
                                     $"Category 2: {(cat2Cash + cat2KBANK + cat2KTB + cat2Director):N2}\n" +
                                     $"Category 3: {(cat3Cash + cat3KBANK + cat3KTB + cat3Director):N2}\n" +
                                     $"Category 4: {(cat4Cash + cat4KBANK + cat4KTB + cat4Director):N2}\n" +
                                     $"Total Cash: {totalCash:N2}\n" +
                                     $"Total KBANK: {totalKBANK:N2}\n" +
                                     $"Total KTB: {totalKTB:N2}\n" +
                                     $"Total Director: {totalDirector:N2}\n" +
                                     $"Document Count: {docCount}\n" +
                                     $"Total VAT: {totalVAT:N2}";

                    loggingService.LogRevenueCalculation(startDate, endDate, grandTotal, breakdown, GetCurrentUserId());
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"   ⚠️ Logging failed (ignored): {logEx.Message}");
                }

                System.Diagnostics.Debug.WriteLine($"✅ CalculateRevenue completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ CalculateRevenue FAILED: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   Stack: {ex.StackTrace}");
                throw; // Re-throw to be caught by outer try-catch in btnSearch_Click
            }
        }

        private DataTable GetCategory1Revenue(DateTime startDate, DateTime endDate, string status)
        {
            // Category 1: รายได้จากการจองพัก (เช็คอินในช่วง - ไม่รวมมัดจำ)
            // เงื่อนไขอันดับ 1: ใบกำกับภาษีออกในช่วงที่ค้นหา (Account_Receipt.Created_Date)
            // เงื่อนไขอันดับ 2: การจองมีวันเช็คอินในช่วงที่ค้นหา (Reservation.CheckinDate)
            // เงื่อนไขอันดับ 3: ไม่เป็นมัดจำ (IsDeposit = 0 หรือ NULL)
            string query = @"
                SELECT DISTINCT ph.ID as PaymentHistoryID, ph.PaymentMethod, ph.PaymentAmount, ar.ID as ReceiptID
                FROM Payment_History ph
                INNER JOIN Reservation r ON ph.Reservation_ID = r.ID
                INNER JOIN Account_Receipt ar ON ph.Receipt_ID = ar.ID
                WHERE CAST(ar.Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                  AND CAST(ar.Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                  AND CAST(r.CheckinDate AS DATE) >= CAST(@StartDate AS DATE)
                  AND CAST(r.CheckinDate AS DATE) <= CAST(@EndDate AS DATE)
                  AND (ar.IsDeposit = 0 OR ar.IsDeposit IS NULL)
                  AND ar.Status LIKE @Status
                  AND ph.Status = 'COMPLETED'
                  AND ph.Receipt_ID IS NOT NULL
                  AND ar.Reservation_ID > 0";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDate", startDate },
                { "@EndDate", endDate },
                { "@Status", status }
            };

            return codeInstance.DatabaseQuerySafe(conn, query, parameters);
        }

        private DataTable GetCategory2Revenue(DateTime startDate, DateTime endDate, string status)
        {
            // Category 2: รายได้จากมัดจำทั้งหมด (ไม่สนใจว่าเช็คอินเมื่อไหร่)
            // เงื่อนไข: ใบกำกับภาษีออกในช่วงที่ค้นหา AND เป็นมัดจำ (IsDeposit = 1)
            // ดูจากวันที่ออกใบกำกับภาษี (Account_Receipt.Created_Date) ไม่ใช่วันโอนเงิน
            string query = @"
                SELECT DISTINCT ph.ID as PaymentHistoryID, ph.PaymentMethod, ph.PaymentAmount, ar.ID as ReceiptID
                FROM Payment_History ph
                INNER JOIN Reservation r ON ph.Reservation_ID = r.ID
                INNER JOIN Account_Receipt ar ON ph.Receipt_ID = ar.ID
                WHERE CAST(ar.Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                  AND CAST(ar.Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                  AND ar.IsDeposit = 1
                  AND ar.Status LIKE @Status
                  AND ph.Status = 'COMPLETED'
                  AND ph.Receipt_ID IS NOT NULL
                  AND ar.Reservation_ID > 0";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDate", startDate },
                { "@EndDate", endDate },
                { "@Status", status }
            };

            return codeInstance.DatabaseQuerySafe(conn, query, parameters);
        }

        private DataTable GetCategory3Revenue(DateTime startDate, DateTime endDate, string status)
        {
            // Product sales (ProductType_ID = 3) - Non-reservation items
            // Note: For non-reservation items, we still use Account_Receipt since Payment_History
            // is only for reservations. We return individual payment methods split from Paid_Type.
            string query = @"
                SELECT ar.ID, ar.Paid_Type, ar.Total_Amount
                FROM Account_Receipt ar
                INNER JOIN Account_Receipt_Detail ard ON ar.ID = ard.Receipt_ID
                WHERE CAST(ar.Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                  AND CAST(ar.Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                  AND ard.ProductType_ID = 3
                  AND ar.Status LIKE @Status
                  AND (ar.Reservation_ID = 0 OR ar.Reservation_ID IS NULL)";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDate", startDate },
                { "@EndDate", endDate },
                { "@Status", status }
            };

            return codeInstance.DatabaseQuerySafe(conn, query, parameters);
        }

        private DataTable GetCategory4Revenue(DateTime startDate, DateTime endDate, string status)
        {
            // Others (not in categories 1-3) - Non-reservation items
            // Note: For non-reservation items, we still use Account_Receipt since Payment_History
            // is only for reservations. We return individual payment methods split from Paid_Type.
            string query = @"
                SELECT ar.ID, ar.Paid_Type, ar.Total_Amount
                FROM Account_Receipt ar
                LEFT JOIN Account_Receipt_Detail ard ON ar.ID = ard.Receipt_ID
                WHERE CAST(ar.Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                  AND CAST(ar.Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                  AND ar.Status LIKE @Status
                  AND (ar.Reservation_ID = 0 OR ar.Reservation_ID IS NULL)
                  AND (ard.ProductType_ID IS NULL OR ard.ProductType_ID != 3)";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDate", startDate },
                { "@EndDate", endDate },
                { "@Status", status }
            };

            return codeInstance.DatabaseQuerySafe(conn, query, parameters);
        }

        /// <summary>
        /// Fallback: ดึงข้อมูล Category 1 จาก Account_Receipt ถ้า Payment_History ไม่มีข้อมูล
        /// Note: Fallback uses Created_Date because Payment_History.PaymentDate is not available in old system
        /// </summary>
        private DataTable GetCategory1RevenueFallback(DateTime startDate, DateTime endDate, string status)
        {
            // Category 1 Fallback: รายได้จากการจองพัก (เช็คอินในช่วง - ไม่รวมมัดจำ)
            // ใช้ Account_Receipt สำหรับระบบเก่าที่ยังไม่มี Payment_History
            // เงื่อนไขอันดับ 1: ใบกำกับภาษีออกในช่วง (Created_Date)
            // เงื่อนไขอันดับ 2: วันเช็คอินในช่วง (CheckinDate)
            // เงื่อนไขอันดับ 3: ไม่เป็นมัดจำ (IsDeposit = 0 หรือ NULL)
            string query = @"
                SELECT ar.ID, ar.Paid_Type, ar.Total_Amount
                FROM Account_Receipt ar
                INNER JOIN Reservation r ON ar.Reservation_ID = r.ID
                WHERE CAST(ar.Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                  AND CAST(ar.Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                  AND CAST(r.CheckinDate AS DATE) >= CAST(@StartDate AS DATE)
                  AND CAST(r.CheckinDate AS DATE) <= CAST(@EndDate AS DATE)
                  AND (ar.IsDeposit = 0 OR ar.IsDeposit IS NULL)
                  AND ar.Status LIKE @Status
                  AND ar.Reservation_ID > 0";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDate", startDate },
                { "@EndDate", endDate },
                { "@Status", status }
            };

            return codeInstance.DatabaseQuerySafe(conn, query, parameters);
        }

        /// <summary>
        /// Fallback: ดึงข้อมูล Category 2 จาก Account_Receipt ถ้า Payment_History ไม่มีข้อมูล
        /// Note: Fallback uses Created_Date because Payment_History.PaymentDate is not available in old system
        /// </summary>
        private DataTable GetCategory2RevenueFallback(DateTime startDate, DateTime endDate, string status)
        {
            // Category 2 Fallback: รายได้จากมัดจำทั้งหมด (ไม่สนใจว่าเช็คอินเมื่อไหร่)
            // ใช้ Account_Receipt สำหรับระบบเก่าที่ยังไม่มี Payment_History
            // เงื่อนไข: ใบกำกับภาษีออกในช่วงที่ค้นหา AND เป็นมัดจำ (IsDeposit = 1)
            string query = @"
                SELECT ar.ID, ar.Paid_Type, ar.Total_Amount
                FROM Account_Receipt ar
                INNER JOIN Reservation r ON ar.Reservation_ID = r.ID
                WHERE CAST(ar.Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                  AND CAST(ar.Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                  AND ar.IsDeposit = 1
                  AND ar.Status LIKE @Status
                  AND ar.Reservation_ID > 0";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDate", startDate },
                { "@EndDate", endDate },
                { "@Status", status }
            };

            return codeInstance.DatabaseQuerySafe(conn, query, parameters);
        }

        private decimal GetAmountByPaymentMethod(DataTable dt, int paymentMethodID)
        {
            // Legacy payment method mapping (hard-coded, no table lookup needed)
            // 1 = KBANK (โอนกสิกร), 2 = CASH (เงินสด), 3 = DIRECTOR (เงินกรรมการ), 4 = KTB (โอนกรุงไทย)
            string paymentMethodName = GetPaymentMethodNameByLegacyId(paymentMethodID);
            if (string.IsNullOrEmpty(paymentMethodName))
            {
                return 0;
            }

            System.Diagnostics.Debug.WriteLine($"   🔍 GetAmountByPaymentMethod: ID={paymentMethodID}, Name='{paymentMethodName}', Rows={dt?.Rows.Count ?? 0}");

            decimal total = 0;
            HashSet<string> processedPayments = new HashSet<string>(); // Track processed payments to avoid duplicates
            int matchCount = 0;

            foreach (DataRow row in dt.Rows)
            {
                // Check if this is Payment_History data (has PaymentHistoryID column)
                if (dt.Columns.Contains("PaymentHistoryID"))
                {
                    // Category 1-2: Use Payment_History data (already split by payment method)
                    string paymentHistoryID = row["PaymentHistoryID"]?.ToString() ?? "";
                    string paymentMethod = row["PaymentMethod"]?.ToString() ?? "";

                    if (matchCount < 3) // Log first 3 rows for debugging
                    {
                        System.Diagnostics.Debug.WriteLine($"      Row PaymentMethod='{paymentMethod}', Looking for='{paymentMethodName}', Match={paymentMethod.Contains(paymentMethodName)}");
                    }

                    // Avoid counting same Payment_History row multiple times
                    if (!string.IsNullOrEmpty(paymentHistoryID) &&
                        !processedPayments.Contains(paymentHistoryID) &&
                        paymentMethod.Contains(paymentMethodName))
                    {
                        decimal amount = row["PaymentAmount"] != DBNull.Value ?
                            Convert.ToDecimal(row["PaymentAmount"]) : 0;
                        total += amount;
                        processedPayments.Add(paymentHistoryID);
                        matchCount++;
                    }
                }
                else
                {
                    // Category 3-4: Use Account_Receipt data (need to split if multiple payment methods)
                    string receiptId = row["ID"]?.ToString() ?? "";
                    string paidType = row["Paid_Type"]?.ToString() ?? "";
                    decimal receiptAmount = row["Total_Amount"] != DBNull.Value ?
                        Convert.ToDecimal(row["Total_Amount"]) : 0;

                    if (matchCount < 3) // Log first 3 rows for debugging
                    {
                        System.Diagnostics.Debug.WriteLine($"      Row Paid_Type='{paidType}', Looking for='{paymentMethodName}', Match={paidType.Contains(paymentMethodName)}");
                    }

                    // Check if this payment method is in the Paid_Type using simple string matching
                    if (!string.IsNullOrEmpty(paidType) && paidType.Contains(paymentMethodName))
                    {
                        // Only count this receipt once per payment method
                        string uniqueKey = $"{receiptId}_{paymentMethodName}";
                        if (!processedPayments.Contains(uniqueKey))
                        {
                            // If multiple payment methods (comma-separated), split the amount evenly
                            int methodCount = paidType.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Length;
                            decimal amountForThisMethod = methodCount > 1 ? receiptAmount / methodCount : receiptAmount;
                            total += amountForThisMethod;
                            processedPayments.Add(uniqueKey);
                            matchCount++;
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"      ✅ Matched {matchCount} rows, Total={total:N2}");
            return total;
        }

        private DataTable GetAllReceipts(DateTime startDate, DateTime endDate, string status)
        {
            // Show receipts created in the date range only
            // Note: This may not match revenue totals because:
            // - Category 1 includes receipts where CheckinDate is in range (even if Created_Date is outside)
            // - This detail list only shows receipts where Created_Date is in range

            // 🚀 PERFORMANCE FIX: Include slip data in main query to avoid N+1 query problem
            string query = @"
                SELECT ar.ID, ar.Reservation_ID, ar.Created_Date, ar.Paid_Type,
                       ar.Total_Amount, ar.Vat, ar.IsDeposit, ar.UseDeposit,
                       ar.Status,
                       c.FullName as CustomerName,
                       r.Customer_MobilePhone,
                       r.Remark,
                       a.Username as Created_By,
                       -- Slip information (prevents N+1 queries by including in main query)
                       ps.SlipFileURL,
                       CASE WHEN ps.SlipFileURL IS NOT NULL THEN 1 ELSE 0 END as HasSlip
                FROM Account_Receipt ar
                LEFT JOIN Reservation r ON ar.Reservation_ID = r.ID
                LEFT JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                LEFT JOIN Admin a ON ar.Created_By_ID = a.ID
                -- LEFT JOIN to get slip data directly from Payment_Slips by Account_Receipt_ID
                LEFT JOIN (
                    SELECT ps.Account_Receipt_ID, ps.SlipFileURL,
                           ROW_NUMBER() OVER (PARTITION BY ps.Account_Receipt_ID ORDER BY ps.UploadedDate DESC) as RowNum
                    FROM Payment_Slips ps
                    WHERE ps.SlipFileURL IS NOT NULL AND ps.IsActive = 1
                ) ps ON ar.ID = ps.Account_Receipt_ID AND ps.RowNum = 1
                WHERE CAST(ar.Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                  AND CAST(ar.Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                  AND ar.Status LIKE @Status
                ORDER BY ar.ID ASC";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDate", startDate },
                { "@EndDate", endDate },
                { "@Status", status }
            };

            System.Diagnostics.Debug.WriteLine($"📋 GetAllReceipts Query:");
            System.Diagnostics.Debug.WriteLine($"   @StartDate = {startDate:yyyy-MM-dd HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"   @EndDate = {endDate:yyyy-MM-dd HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"   @Status = {status}");

            var result = codeInstance.DatabaseQuerySafe(conn, query, parameters);
            System.Diagnostics.Debug.WriteLine($"   ✅ Result: {result?.Rows.Count ?? 0} rows");

            // If no results, try a simpler query to see if there's ANY data
            if (result == null || result.Rows.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"   ⚠️ No results from main query. Running diagnostic queries...");

                // Test 1: Count all receipts (no filters)
                var allQuery = "SELECT COUNT(*) as Total FROM Account_Receipt";
                var allResult = codeInstance.DatabaseQuerySafe(conn, allQuery, new Dictionary<string, object>());
                System.Diagnostics.Debug.WriteLine($"   🔍 Total receipts in database (no filter): {allResult?.Rows[0]["Total"]}");

                // Test 2: Count receipts in date range (any status)
                var testQuery = "SELECT COUNT(*) as Total FROM Account_Receipt ar WHERE CAST(ar.Created_Date AS DATE) >= CAST(@StartDate AS DATE) AND CAST(ar.Created_Date AS DATE) <= CAST(@EndDate AS DATE)";
                var testResult = codeInstance.DatabaseQuerySafe(conn, testQuery, parameters);
                System.Diagnostics.Debug.WriteLine($"   🔍 Total receipts in date range (any status): {testResult?.Rows[0]["Total"]}");

                // Test 3: Count receipts with matching status (any date)
                var statusQuery = "SELECT COUNT(*) as Total FROM Account_Receipt ar WHERE ar.Status LIKE @Status";
                var statusParams = new Dictionary<string, object> { { "@Status", status } };
                var statusResult = codeInstance.DatabaseQuerySafe(conn, statusQuery, statusParams);
                System.Diagnostics.Debug.WriteLine($"   🔍 Total receipts with status '{status}' (any date): {statusResult?.Rows[0]["Total"]}");

                // Test 4: Get sample receipts without date filter
                var sampleQuery = "SELECT TOP 5 ID, Created_Date, Status FROM Account_Receipt ORDER BY Created_Date DESC";
                var sampleResult = codeInstance.DatabaseQuerySafe(conn, sampleQuery, new Dictionary<string, object>());
                System.Diagnostics.Debug.WriteLine($"   🔍 Sample receipts (latest 5):");
                if (sampleResult != null)
                {
                    foreach (DataRow row in sampleResult.Rows)
                    {
                        System.Diagnostics.Debug.WriteLine($"      - ID: {row["ID"]}, Created: {row["Created_Date"]}, Status: {row["Status"]}");
                    }
                }

                // Test 5: Try simple query without JOINs
                var simpleQuery = @"
                    SELECT ar.ID, ar.Reservation_ID, ar.Created_Date, ar.Paid_Type,
                           ar.Total_Amount, ar.Vat, ar.Status
                    FROM Account_Receipt ar
                    WHERE CAST(ar.Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                      AND CAST(ar.Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                      AND ar.Status LIKE @Status
                    ORDER BY ar.ID ASC";
                var simpleResult = codeInstance.DatabaseQuerySafe(conn, simpleQuery, parameters);
                System.Diagnostics.Debug.WriteLine($"   🔍 Simple query (no JOINs): {simpleResult?.Rows.Count ?? 0} rows");

                // If simple query works but main query doesn't, it's a JOIN issue
                if (simpleResult != null && simpleResult.Rows.Count > 0 && (result == null || result.Rows.Count == 0))
                {
                    System.Diagnostics.Debug.WriteLine($"   ⚠️ JOIN is causing the issue! Using simple result instead.");
                    return simpleResult;
                }
            }

            return result;
        }

        private void LoadDetails(DateTime startDate, DateTime endDate)
        {
            DataTable dt = null;
            try
            {
                System.Diagnostics.Debug.WriteLine($"📊 LoadDetails called:");
                System.Diagnostics.Debug.WriteLine($"   Start: {startDate:yyyy-MM-dd HH:mm:ss}");
                System.Diagnostics.Debug.WriteLine($"   End: {endDate:yyyy-MM-dd HH:mm:ss}");

                // Always show all documents (both Normal and Cancel) in GridView
                try
                {
                    dt = GetAllReceipts(startDate, endDate, "%");
                    System.Diagnostics.Debug.WriteLine($"   Retrieved {dt?.Rows.Count ?? 0} rows from GetAllReceipts");
                }
                catch (Exception queryEx)
                {
                    System.Diagnostics.Debug.WriteLine($"   ❌ GetAllReceipts failed: {queryEx.Message}");
                    lblDateRange.Text += $" <span style='color: red;'>[Query Error: {queryEx.Message}]</span>";
                    throw;
                }

                // ── รวมเอกสารจาก NextAcc (รวมใบที่ยกเลิก/void) เข้าตาราง ──
                // ใบที่ถูก void ตอนแก้ไขไม่มีแถวใน local → ดึงจาก NextAcc มาแสดงเป็นแถว "NextAcc-only"
                // เพื่อดาวน์โหลด PDF ส่งบัญชีได้ครบ เลขที่เอกสารไม่ขาดช่วง. ล้มเหลว = คงตาราง local เดิม.
                if (dt != null) MergeNextAccReceiptDocs(dt, startDate, endDate);

                // โหลด set ใบเสร็จที่มี e-Tax แล้ว (โชว์ปุ่ม "ส่ง e-Tax" เฉพาะใบพวกนี้)
                try
                {
                    _etaxReceipts = new AccountingSyncService(conn).GetReceiptsWithEtax(startDate, endDate);
                }
                catch { _etaxReceipts = new HashSet<string>(StringComparer.OrdinalIgnoreCase); }

                // Debug: Add message to date range label (ALWAYS execute this)
                if (dt != null && dt.Rows.Count > 0)
                {
                    lblDateRange.Text += $" <span style='color: green; font-weight: bold;'>(✓ พบ {dt.Rows.Count} เอกสาร)</span>";
                    System.Diagnostics.Debug.WriteLine($"   ✅ Showing {dt.Rows.Count} documents in GridView");
                }
                else
                {
                    lblDateRange.Text += $" <span style='color: red; font-weight: bold;'>(⚠️ ไม่พบเอกสาร)</span>";
                    System.Diagnostics.Debug.WriteLine($"   ⚠️ No documents found!");

                    // Get diagnostic info to show on page
                    string diagInfo = "";
                    try
                    {
                        // Count all receipts
                        var allQuery = "SELECT COUNT(*) as Total FROM Account_Receipt";
                        var allResult = codeInstance.DatabaseQuerySafe(conn, allQuery, new Dictionary<string, object>());
                        int totalReceipts = allResult != null ? Convert.ToInt32(allResult.Rows[0]["Total"]) : 0;

                        // Count in date range
                        var testQuery = "SELECT COUNT(*) as Total FROM Account_Receipt ar WHERE CAST(ar.Created_Date AS DATE) >= CAST(@StartDate AS DATE) AND CAST(ar.Created_Date AS DATE) <= CAST(@EndDate AS DATE)";
                        var testParams = new Dictionary<string, object> { { "@StartDate", startDate }, { "@EndDate", endDate } };
                        var testResult = codeInstance.DatabaseQuerySafe(conn, testQuery, testParams);
                        int inRange = testResult != null ? Convert.ToInt32(testResult.Rows[0]["Total"]) : 0;

                        // Get latest receipt date
                        var latestQuery = "SELECT TOP 1 Created_Date FROM Account_Receipt ORDER BY Created_Date DESC";
                        var latestResult = codeInstance.DatabaseQuerySafe(conn, latestQuery, new Dictionary<string, object>());
                        string latestDate = latestResult != null && latestResult.Rows.Count > 0 ?
                            Convert.ToDateTime(latestResult.Rows[0]["Created_Date"]).ToString("dd/MM/yyyy") : "ไม่มี";

                        diagInfo = $"\n\nข้อมูลเพิ่มเติม:\n- มีเอกสารทั้งหมดในระบบ: {totalReceipts} รายการ\n- มีเอกสารในช่วง {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}: {inRange} รายการ\n- เอกสารล่าสุดสร้างวันที่: {latestDate}";

                        System.Diagnostics.Debug.WriteLine($"   📊 Diagnostic: Total={totalReceipts}, InRange={inRange}, Latest={latestDate}");
                    }
                    catch (Exception diagEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"   ⚠️ Diagnostic query failed: {diagEx.Message}");
                        diagInfo = "\n\n(ไม่สามารถดึงข้อมูลสถิติได้)";
                    }

                    // Show helpful message to user
                    ShowError($"ไม่พบเอกสารในช่วง {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}{diagInfo}\n\nกรุณาตรวจสอบ:\n1. เลือกช่วงวันที่ที่มีเอกสาร\n2. วันที่ที่เลือกถูกต้องหรือไม่\n3. ตรวจสอบ Debug Output สำหรับรายละเอียดเพิ่มเติม");
                }

                // Bind to GridView — เรียงตาม "วันที่เอกสาร" (Created_Date) เป็นหลัก, tie-break ด้วยเลขที่
                // (DisplayDoc) ให้เอกสารวันเดียวกันเรียงตามเลขต่อเนื่อง
                if (dt != null && dt.Columns.Contains("Created_Date"))
                {
                    string tie = dt.Columns.Contains("DisplayDoc") ? ", DisplayDoc ASC" : "";
                    dt.DefaultView.Sort = "Created_Date ASC" + tie;
                    gvDetails.DataSource = dt.DefaultView;
                }
                else if (dt != null && dt.Columns.Contains("DisplayDoc"))
                {
                    dt.DefaultView.Sort = "DisplayDoc ASC";
                    gvDetails.DataSource = dt.DefaultView;
                }
                else
                {
                    gvDetails.DataSource = dt;
                }
                gvDetails.DataBind();
                System.Diagnostics.Debug.WriteLine($"   ✅ GridView.DataBind() completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Error in LoadDetails: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   Stack: {ex.StackTrace}");
                lblDateRange.Text += $" <span style='color: red; font-weight: bold;'>[LoadDetails Error: {ex.Message}]</span>";
                ShowError($"เกิดข้อผิดพลาดในการโหลดข้อมูล:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}");

                // Try to bind empty DataTable to prevent further errors
                try
                {
                    gvDetails.DataSource = new DataTable();
                    gvDetails.DataBind();
                }
                catch { }
            }
        }

        /// <summary>
        /// รวมเอกสารฝั่งรับจาก NextAcc (รวมใบที่ยกเลิก/void) เข้าตาราง local:
        /// - เพิ่มคอลัมน์ IsNextAccOnly / NextAccId / NextAccViewUrl (ใช้เป็น DataKey ตอนกดดู PDF)
        /// - ใบที่ยกเลิกบน NextAcc → เพิ่มเป็นแถวสถานะ "Cancel" (เอกสารที่ถูก void ตอนแก้ไข = ดาวน์โหลดส่งบัญชีได้)
        /// - ใบที่สร้างบน NextAcc โดยตรง (ไม่มีคู่ใน local) → เพิ่มเป็นแถว "NextAcc"
        /// การกดดู PDF ของแถวเหล่านี้เปิดจาก NextAcc ด้วย GUID (gvDetails_SelectedIndexChanging)
        /// ล้มเหลว/timeout → คงตาราง local เดิม (คอลัมน์ที่เพิ่มยังอยู่ครบ เพื่อ DataKeyNames ไม่พัง)
        /// </summary>
        private void MergeNextAccReceiptDocs(DataTable dt, DateTime startDate, DateTime endDate)
        {
            // เพิ่มคอลัมน์ที่ DataKeyNames/grid อ้าง — ต้องมีเสมอแม้ดึง NextAcc ไม่สำเร็จ
            // DisplayDoc = เลขที่ที่แสดงในตาราง (เลข NextAcc ถ้า sync แล้ว, เลข local ถ้ายัง) —
            // เลขเดียวต่อเอกสาร ไม่ซ้อนกัน; ปุ่มแก้ไข/ลบ/ดู PDF ใช้เลข local จาก DataKeys["ID"] เสมอ
            if (!dt.Columns.Contains("DisplayDoc")) dt.Columns.Add("DisplayDoc", typeof(string));
            if (!dt.Columns.Contains("IsNextAccOnly")) dt.Columns.Add("IsNextAccOnly", typeof(string));
            if (!dt.Columns.Contains("NextAccId")) dt.Columns.Add("NextAccId", typeof(string));
            if (!dt.Columns.Contains("NextAccViewUrl")) dt.Columns.Add("NextAccViewUrl", typeof(string));

            foreach (DataRow r in dt.Rows)
            {
                r["DisplayDoc"] = r["ID"]?.ToString() ?? "";
                r["IsNextAccOnly"] = "0";
                r["NextAccId"] = "";
                r["NextAccViewUrl"] = "";
            }

            System.Collections.Generic.List<Take_Time_BangPhra.Integration.NextAccPaymentDoc> naDocs = null;
            try
            {
                var cfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                if (!cfg.IsConfigured || !cfg.Enabled) return;
                Server.ScriptTimeout = 300;
                var svc = new AccountingSyncService(conn);
                var task = System.Threading.Tasks.Task.Run(() => svc.FetchNextAccReceiptDocumentsAsync(startDate, endDate));
                if (task.Wait(20000)) naDocs = task.Result;
                else
                {
                    try { codeInstance.Logs(conn, "AccountingSync", "CheckDocument: ดึงเอกสาร NextAcc ไม่ทันใน 20 วิ → แสดง local อย่างเดียว", "SYSTEM"); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                try { codeInstance.Logs(conn, "AccountingSync", $"CheckDocument: ดึงเอกสาร NextAcc ล้มเหลว ({ex.Message}) → แสดง local อย่างเดียว", "SYSTEM"); }
                catch { }
                return;
            }

            if (naDocs == null || naDocs.Count == 0) return;

            bool IsVoidStatus(string s) =>
                string.Equals(s, "Voided", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Canceled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Rejected", StringComparison.OrdinalIgnoreCase);

            // index เอกสาร NextAcc: ตาม GUID และตามเลขเอกสาร (ใช้จับคู่ผ่าน sync queue)
            var byId = new Dictionary<Guid, Take_Time_BangPhra.Integration.NextAccPaymentDoc>();
            var byNum = new Dictionary<string, Take_Time_BangPhra.Integration.NextAccPaymentDoc>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in naDocs)
            {
                if (d.Id != Guid.Empty && !byId.ContainsKey(d.Id)) byId[d.Id] = d;
                if (!string.IsNullOrEmpty(d.DocumentNumber) && !byNum.ContainsKey(d.DocumentNumber)) byNum[d.DocumentNumber] = d;
            }

            if (_syncStatusCache == null) LoadSyncStatusCache();
            var matched = new HashSet<Take_Time_BangPhra.Integration.NextAccPaymentDoc>();

            // จับคู่แถว local → เอกสาร NextAcc: (1) sync queue (GUID/เลขเอกสารที่บันทึกไว้ตอน sync —
            // แม่นสุด ครอบคลุมเคส Reference = "RES-{resId}" ที่ไม่มีเลข local), (2) Reference มีเลข local
            // (เอกสาร active ก่อน — ใบ voided จากการแก้ไขต้องแสดงเป็นแถวยกเลิกแยกต่างหาก)
            foreach (DataRow r in dt.Rows)
            {
                string lid = r["ID"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(lid)) continue;

                Take_Time_BangPhra.Integration.NextAccPaymentDoc nd = null;
                if (_syncStatusCache != null && _syncStatusCache.TryGetValue(lid, out var qrow))
                {
                    string respId = qrow.Table.Columns.Contains("Nexaacc_Response_Id") ? qrow["Nexaacc_Response_Id"]?.ToString() : null;
                    if (Guid.TryParse(respId, out var g)) byId.TryGetValue(g, out nd);
                    if (nd == null)
                    {
                        string qnum = qrow.Table.Columns.Contains("Nexaacc_Document_Number") ? qrow["Nexaacc_Document_Number"]?.ToString() : null;
                        if (!string.IsNullOrEmpty(qnum)) byNum.TryGetValue(qnum, out nd);
                    }
                }
                if (nd == null)
                {
                    foreach (var d in naDocs)   // active ก่อน — ใบ voided คงไว้เป็นแถวประวัติแยก
                    {
                        if (IsVoidStatus(d.Status)) continue;
                        if ((d.Reference ?? "").IndexOf(lid, StringComparison.OrdinalIgnoreCase) >= 0) { nd = d; break; }
                    }
                }

                if (nd != null)
                {
                    matched.Add(nd);
                    r["DisplayDoc"] = !string.IsNullOrEmpty(nd.DocumentNumber) ? nd.DocumentNumber : lid;
                    r["NextAccId"] = nd.Id != Guid.Empty ? nd.Id.ToString() : "";
                }
            }

            // เอกสารที่ไม่มีคู่ local (สร้างบน NextAcc โดยตรง / ใบเก่าที่ถูก void ตอนแก้ไข) → เพิ่มแถวใหม่
            int added = 0;
            foreach (var nd in naDocs)
            {
                if (matched.Contains(nd)) continue;
                bool isVoid = IsVoidStatus(nd.Status);
                string naNum = !string.IsNullOrEmpty(nd.DocumentNumber) ? nd.DocumentNumber : nd.Id.ToString();

                var nr = dt.NewRow();
                nr["ID"] = naNum;
                nr["DisplayDoc"] = naNum;
                nr["Created_Date"] = nd.DocumentDate;
                if (dt.Columns.Contains("CustomerName")) nr["CustomerName"] = string.IsNullOrEmpty(nd.ContactName) ? "-" : nd.ContactName;
                if (dt.Columns.Contains("Paid_Type")) nr["Paid_Type"] = nd.DocumentTypeLabel ?? "";
                nr["Total_Amount"] = nd.TotalAmount;
                nr["Vat"] = nd.VatAmount;
                nr["Status"] = isVoid ? "Cancel" : "NextAcc";
                if (dt.Columns.Contains("Remark")) nr["Remark"] = nd.Notes ?? "";
                if (dt.Columns.Contains("Created_By")) nr["Created_By"] = "NextAcc";
                if (dt.Columns.Contains("HasSlip")) nr["HasSlip"] = 0;
                nr["IsNextAccOnly"] = "1";
                nr["NextAccId"] = nd.Id != Guid.Empty ? nd.Id.ToString() : "";
                nr["NextAccViewUrl"] = "";
                dt.Rows.Add(nr);
                added++;
            }

            System.Diagnostics.Debug.WriteLine($"   ➕ MergeNextAccReceiptDocs: matched {matched.Count}, added {added} NextAcc-only row(s) from {naDocs.Count} doc(s)");
            lblDateRange.Text += $" <span style='color:#8a5a00;'>(NextAcc: จับคู่ {matched.Count}, เพิ่ม {added})</span>";
        }

        /// <summary>ปุ่ม "🔍 ตรวจยอดชำระ NextAcc" — ตรวจทุกใบในช่วงวันที่ที่เลือก: รับเงินซ้อน (ชำระเกินยอด)
        /// และค้างชำระ (settle ไม่ครบ) แล้วรายงานเป็นรายใบ พร้อมวิธีแก้ (ใช้เคลียร์ "เอกสารเดิม")</summary>
        protected void btnAuditPayments_Click(object sender, EventArgs e)
        {
            try
            {
                // ช่วงวันที่: ใช้กติกาเดียวกับปุ่มค้นหา (เดือน/ปี ก่อน แล้วค่อยช่วงวันที่)
                DateTime startDate, endDate;
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

                var cfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                if (!cfg.IsConfigured || !cfg.Enabled)
                {
                    ShowError("ยังไม่ได้เปิดใช้ NextAcc — ตรวจยอดชำระไม่ได้");
                    return;
                }

                Server.ScriptTimeout = 300;
                var svc = new AccountingSyncService(conn);
                string report = null;
                var task = System.Threading.Tasks.Task.Run(() => svc.AuditNextAccReceiptPaymentsAsync(startDate, endDate));
                if (task.Wait(60000)) report = task.Result;

                ShowError(report ?? "NextAcc ตอบช้า — ลองกดตรวจอีกครั้งในสักครู่ (ผลถูกบันทึกใน log ด้วย)");
            }
            catch (Exception ex)
            {
                ShowError("ตรวจยอดชำระไม่สำเร็จ: " + ex.Message);
            }
        }

        private void ValidateTotal()
        {
            // Validate that sum of categories equals grand total
            decimal cat1 = Convert.ToDecimal(lblCat1Total.Text);
            decimal cat2 = Convert.ToDecimal(lblCat2Total.Text);
            decimal cat3 = Convert.ToDecimal(lblCat3Total.Text);
            decimal cat4 = Convert.ToDecimal(lblCat4Total.Text);
            decimal calculated = cat1 + cat2 + cat3 + cat4;

            decimal grandTotal = Convert.ToDecimal(lblGrandTotal.Text);

            pnlValidation.Visible = true;

            if (Math.Abs(calculated - grandTotal) < 0.01m)
            {
                pnlValidation.CssClass = "validation-box validation-success";
                lblValidationIcon.Text = "✅";
                lblValidationMessage.Text = $"ยอดถูกต้อง: ยอดรวมทั้งหมด {grandTotal:N2} บาท (หมวด 1+2+3+4 = {calculated:N2} บาท)";
            }
            else
            {
                pnlValidation.CssClass = "validation-box validation-error";
                lblValidationIcon.Text = "⚠️";
                lblValidationMessage.Text = $"ยอดไม่ตรง! ยอดรวม {grandTotal:N2} บาท แต่ผลรวมหมวด {calculated:N2} บาท (ต่าง {Math.Abs(calculated - grandTotal):N2} บาท)";
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

                // Add BOM for UTF-8 Excel compatibility
                csv.Append("\uFEFF");

                // Header
                csv.AppendLine("สรุปรายได้ตามหมวด");
                csv.AppendLine($"ช่วงวันที่:,{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}");
                csv.AppendLine($"การคำนวณยอด:,คำนวณเฉพาะเอกสารปกติ (ไม่รวมยกเลิก)");
                csv.AppendLine($"รายละเอียดเอกสาร:,แสดงทั้งหมด (รวมยกเลิก)");
                csv.AppendLine($"วันที่ออกรายงาน:,{DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                csv.AppendLine();

                // Summary table
                csv.AppendLine("หมวดรายได้,เงินสด,โอนกสิกร,โอนกรุงไทย,เงินกรรมการ,รวม");
                csv.AppendLine($"1. จองพัก (เช็คอินในช่วง),{lblCat1Cash.Text},{lblCat1KBANK.Text},{lblCat1KTB.Text},{lblCat1Director.Text},{lblCat1Total.Text}");
                csv.AppendLine($"2. จองพัก (โอนในช่วง),{lblCat2Cash.Text},{lblCat2KBANK.Text},{lblCat2KTB.Text},{lblCat2Director.Text},{lblCat2Total.Text}");
                csv.AppendLine($"3. ขายสินค้า,{lblCat3Cash.Text},{lblCat3KBANK.Text},{lblCat3KTB.Text},{lblCat3Director.Text},{lblCat3Total.Text}");
                csv.AppendLine($"4. อื่นๆ,{lblCat4Cash.Text},{lblCat4KBANK.Text},{lblCat4KTB.Text},{lblCat4Director.Text},{lblCat4Total.Text}");
                csv.AppendLine($"รวมทั้งหมด,{lblTotalCash.Text},{lblTotalKBANK.Text},{lblTotalKTB.Text},{lblTotalDirector.Text},{lblGrandTotal.Text}");
                csv.AppendLine();

                // Additional info
                csv.AppendLine($"จำนวนเอกสาร (เฉพาะปกติ):,{lblDocCount.Text}");
                csv.AppendLine($"ยอดรวม VAT (เฉพาะปกติ):,{lblTotalVAT.Text}");
                csv.AppendLine();

                // Detail records (show all including Cancel)
                var dt = GetAllReceipts(startDate, endDate, "%");
                csv.AppendLine("รายละเอียดเอกสาร");
                csv.AppendLine("เลขที่เอกสาร,รหัสจอง,วันที่,ชื่อลูกค้า,เบอร์โทร,วิธีชำระ,ยอดรวม,VAT,มัดจำ,ใช้มัดจำ,สถานะ,หมายเหตุ,ผู้สร้าง");

                foreach (DataRow row in dt.Rows)
                {
                    string docId = row["ID"]?.ToString() ?? "";
                    string reservationId = row["Reservation_ID"]?.ToString() ?? "";
                    string date = row["Created_Date"] != DBNull.Value ? Convert.ToDateTime(row["Created_Date"]).ToString("dd/MM/yyyy HH:mm") : "";
                    string customer = row["CustomerName"]?.ToString() ?? "-";
                    string phone = row["Customer_MobilePhone"]?.ToString() ?? "";
                    string paidType = row["Paid_Type"]?.ToString() ?? "";
                    string amount = row["Total_Amount"] != DBNull.Value ? Convert.ToDecimal(row["Total_Amount"]).ToString("N2") : "0.00";
                    string vat = row["Vat"] != DBNull.Value ? Convert.ToDecimal(row["Vat"]).ToString("N2") : "0.00";
                    string isDeposit = row["IsDeposit"]?.ToString() ?? "";
                    string useDeposit = row["UseDeposit"]?.ToString() ?? "";
                    string status = row["Status"]?.ToString() ?? "";
                    string remark = row["Remark"]?.ToString() ?? "";
                    string createdBy = row["Created_By"]?.ToString() ?? "";

                    csv.AppendLine($"{docId},{reservationId},{date},{customer},{phone},{paidType},{amount},{vat},{isDeposit},{useDeposit},{status},{remark},{createdBy}");
                }

                // Send file to browser
                Response.Clear();
                Response.ContentType = "text/csv";
                Response.ContentEncoding = Encoding.UTF8;
                Response.Charset = "UTF-8";
                Response.AddHeader("Content-Disposition", $"attachment;filename=รายงานรายได้_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv");
                Response.Write(csv.ToString());
                Response.End();
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาดในการ export: " + ex.Message);
            }
        }

        // GridView Event Handlers
        protected void gvDetails_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            // ✅ Check if delete is enabled (checkbox must be checked)
            if (!chkEnableDelete.Checked)
            {
                ShowError("กรุณาเปิดใช้งานปุ่มลบก่อน (ติ๊กที่ช่อง 'เปิดใช้งานปุ่มลบ')");
                return;
            }

            try
            {
                // เอกสาร NextAcc-only: ใบที่ยัง active → ยกเลิกได้ (enqueue void ไป NextAcc ด้วย GUID)
                // ใบที่ยกเลิกแล้ว → ไม่มีอะไรให้ทำ
                var dkDel = gvDetails.DataKeys[e.RowIndex];
                if (dkDel != null && dkDel["IsNextAccOnly"]?.ToString() == "1")
                {
                    string naStatus = dkDel["Status"]?.ToString() ?? "";
                    string naId = dkDel["NextAccId"]?.ToString() ?? "";
                    string naDocNum = dkDel["ID"]?.ToString() ?? "";
                    if (naStatus == "Cancel")
                    {
                        ShowError("เอกสารนี้ถูกยกเลิกบน NextAcc แล้ว");
                        return;
                    }
                    if (!Guid.TryParse(naId, out _))
                    {
                        ShowError("เอกสารนี้ไม่มีรหัส NextAcc สำหรับยกเลิก");
                        return;
                    }
                    var syncNa = new AccountingSyncService(conn);
                    long qid = syncNa.EnqueueVoidReceiptByNexaaccId(naId, naDocNum, "ยกเลิกจากหน้าเอกสาร TakeTime");
                    if (qid > 0)
                        ShowError($"✅ ส่งคำสั่งยกเลิก {naDocNum} เข้าคิวแล้ว (#{qid}) — สถานะจะเปลี่ยนเป็นยกเลิกเมื่อประมวลผลเสร็จ กดค้นหาใหม่เพื่อตรวจสอบ");
                    else
                        ShowError("ส่งคำสั่งยกเลิกไม่สำเร็จ — ตรวจสอบการตั้งค่า NextAcc");
                    return;
                }

                // ใช้เลข local จาก DataKeys — คอลัมน์เลขที่ในตารางแสดง DisplayDoc (เลข NextAcc) แล้ว
                // Column index: [0]=ลบ, [1]=ดูPDF, [2]=แก้ไข, [3]=ดูสลิป, [4]=เลขที่เอกสาร(DisplayDoc)
                string docNum = dkDel?["ID"]?.ToString() ?? gvDetails.Rows[e.RowIndex].Cells[4].Text;

                System.Diagnostics.Debug.WriteLine($"🗑️ Attempting to delete document: {docNum}");

                // ✅ Parse document number
                // Format: REC260401001 (REC + Year(2) + Month(2) + Day(2) + Number(3))
                // or PAY260401001 (PAY + Year(2) + Month(2) + Day(2) + Number(3))
                // Legacy: REC24100001 (REC + Year(2) + Month(2) + Number(4))
                string docType = docNum.Remove(3, docNum.Length - 3); // Get first 3 chars: REC or PAY
                string docYear = "20" + docNum.Remove(0, 3).Remove(2, docNum.Length - 5); // Get year: 24 -> 2024
                string docMonth = Convert.ToInt32(docNum.Remove(0, 5).Remove(2, docNum.Length - 7)).ToString(); // Get month: 10

                System.Diagnostics.Debug.WriteLine($"   Parsed: Type={docType}, Year={docYear}, Month={docMonth}");

                if (docType == "REC")
                {
                    string path = ConfigurationManager.AppSettings["ReceiptFolderPath"] + "\\" + docYear + "\\" + docMonth;
                    // Fallback: check padded month directory for files created with zero-padded month
                    if (!Directory.Exists(path))
                    {
                        string altPath = ConfigurationManager.AppSettings["ReceiptFolderPath"] + "\\" + docYear + "\\" + docMonth.PadLeft(2, '0');
                        if (docMonth.PadLeft(2, '0') != docMonth && Directory.Exists(altPath))
                            path = altPath;
                    }

                    System.Diagnostics.Debug.WriteLine($"   Receipt path: {path}");

                    // 🔧 FIX: Update Reservation.Deposit before deleting Payment_History
                    try
                    {
                        // Get payment amount and reservation ID from Payment_History (SECURE: Using parameterized query)
                        var paymentParams = new Dictionary<string, object>
                        {
                            { "@ReceiptID", docNum }
                        };

                        var paymentData = codeInstance.DatabaseQuerySafe(conn,
                            "SELECT ph.PaymentAmount, ph.Reservation_ID " +
                            "FROM [dbo].[Payment_History] ph " +
                            "WHERE ph.Receipt_ID = @ReceiptID",
                            paymentParams);

                        if (paymentData != null && paymentData.Rows.Count > 0)
                        {
                            foreach (DataRow row in paymentData.Rows)
                            {
                                decimal amount = row["PaymentAmount"] != DBNull.Value ? Convert.ToDecimal(row["PaymentAmount"]) : 0;
                                int reservationId = row["Reservation_ID"] != DBNull.Value ? Convert.ToInt32(row["Reservation_ID"]) : 0;

                                if (amount > 0 && reservationId > 0)
                                {
                                    // Reduce Reservation.Deposit by payment amount (SECURE: Using parameterized query)
                                    var updateParams = new Dictionary<string, object>
                                    {
                                        { "@Amount", amount },
                                        { "@ReservationID", reservationId }
                                    };

                                    codeInstance.DatabaseInsertSafe(conn,
                                        "UPDATE [dbo].[Reservation] SET Deposit = ISNULL(Deposit, 0) - @Amount WHERE ID = @ReservationID",
                                        updateParams);

                                    System.Diagnostics.Debug.WriteLine($"   ✅ Updated Reservation {reservationId}: Reduced Deposit by {amount:N2}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"   ⚠️ Error updating Reservation.Deposit: {ex.Message}");
                        // Continue with deletion even if update fails (data consistency issue but prevents stuck state)
                    }

                    // Delete Payment_History that references the Payment_Slips we're about to delete (SECURE)
                    System.Diagnostics.Debug.WriteLine($"   Deleting Payment_History records linked to Payment_Slips...");
                    var deleteParams1 = new Dictionary<string, object> { { "@DocNum", docNum } };
                    codeInstance.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Payment_History] " +
                        "WHERE PaymentSlip_ID IN (SELECT ID FROM [dbo].[Payment_Slips] WHERE Account_Receipt_ID = @DocNum)",
                        deleteParams1);

                    // Delete Payment_History by Receipt_ID (for records not linked via PaymentSlip_ID) (SECURE)
                    System.Diagnostics.Debug.WriteLine($"   Deleting Payment_History records by Receipt_ID...");
                    var deleteParams2 = new Dictionary<string, object> { { "@DocNum", docNum } };
                    codeInstance.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Payment_History] WHERE Receipt_ID = @DocNum",
                        deleteParams2);

                    // Delete Payment_Slips (has FK to Account_Receipt) (SECURE)
                    System.Diagnostics.Debug.WriteLine($"   Deleting Payment_Slips records...");
                    var deleteParams3 = new Dictionary<string, object> { { "@DocNum", docNum } };
                    codeInstance.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Payment_Slips] WHERE Account_Receipt_ID = @DocNum",
                        deleteParams3);

                    // Delete receipt details (SECURE)
                    System.Diagnostics.Debug.WriteLine($"   Deleting Account_Receipt_Detail records...");
                    var deleteParams4 = new Dictionary<string, object> { { "@DocNum", docNum } };
                    codeInstance.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Account_Receipt_Detail] WHERE Receipt_ID = @DocNum",
                        deleteParams4);

                    // Delete receipt record (SECURE)
                    System.Diagnostics.Debug.WriteLine($"   Deleting Account_Receipt record...");
                    var deleteParams5 = new Dictionary<string, object> { { "@DocNum", docNum } };
                    codeInstance.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Account_Receipt] WHERE ID = @DocNum",
                        deleteParams5);

                    // Delete receipt files
                    int filesDeleted = 0;
                    if (Directory.Exists(path))
                    {
                        string[] files = Directory.GetFiles(path, docNum + "*");
                        foreach (string file in files)
                        {
                            File.Delete(file);
                            filesDeleted++;
                        }
                        System.Diagnostics.Debug.WriteLine($"   Deleted {filesDeleted} file(s)");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"   ⚠️ Path does not exist: {path}");
                    }

                    try
                    {
                        var sync = new Integration.AccountingSyncService(conn);
                        sync.EnqueueVoidReceipt(docNum);
                    }
                    catch (Exception accEx)
                    {
                        codeInstance.Logs(conn, "Accounting Sync", $"Void receipt error (CheckDocument_New): docNum={docNum} {accEx.Message}", "SYSTEM");
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ Successfully deleted receipt: {docNum}");
                }
                else if (docType == "PAY")
                {
                    string path = ConfigurationManager.AppSettings["PaymentFolderPath"] + "\\" + docYear + "\\" + docMonth;
                    // Fallback: check padded month directory for files created with zero-padded month
                    if (!Directory.Exists(path))
                    {
                        string altPath = ConfigurationManager.AppSettings["PaymentFolderPath"] + "\\" + docYear + "\\" + docMonth.PadLeft(2, '0');
                        if (docMonth.PadLeft(2, '0') != docMonth && Directory.Exists(altPath))
                            path = altPath;
                    }

                    System.Diagnostics.Debug.WriteLine($"   Payment path: {path}");

                    // Delete payment details (SECURE)
                    System.Diagnostics.Debug.WriteLine($"   Deleting Account_Payment_Detail records...");
                    var deletePaymentParams1 = new Dictionary<string, object> { { "@DocNum", docNum } };
                    codeInstance.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Account_Payment_Detail] WHERE Payment_ID = @DocNum",
                        deletePaymentParams1);

                    // Delete payment record (SECURE)
                    System.Diagnostics.Debug.WriteLine($"   Deleting Account_Payment record...");
                    var deletePaymentParams2 = new Dictionary<string, object> { { "@DocNum", docNum } };
                    codeInstance.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Account_Payment] WHERE ID = @DocNum",
                        deletePaymentParams2);

                    // Delete payment files
                    int filesDeleted = 0;
                    if (Directory.Exists(path))
                    {
                        string[] files = Directory.GetFiles(path, docNum + "*");
                        foreach (string file in files)
                        {
                            File.Delete(file);
                            filesDeleted++;
                        }
                        System.Diagnostics.Debug.WriteLine($"   Deleted {filesDeleted} file(s)");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"   ⚠️ Path does not exist: {path}");
                    }

                    try
                    {
                        var sync = new Integration.AccountingSyncService(conn);
                        sync.EnqueueVoidPaymentVoucher(docNum);
                    }
                    catch (Exception accEx)
                    {
                        codeInstance.Logs(conn, "Accounting Sync", $"Void voucher error (CheckDocument_New): docNum={docNum} {accEx.Message}", "SYSTEM");
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ Successfully deleted payment: {docNum}");
                }
                else
                {
                    throw new Exception($"ประเภทเอกสารไม่ถูกต้อง: {docType} (ต้องเป็น REC หรือ PAY)");
                }

                // Log successful deletion
                try
                {
                    loggingService?.LogAccountingOperation(
                        "DocumentDeleted",
                        $"Deleted document: {docNum} (Type: {docType})",
                        true,
                        GetCurrentUserId());
                }
                catch { /* Ignore logging errors */ }

                // Server-side redirect — เชื่อถือได้กว่า ClientScript (ไม่ขึ้นกับ JS) → row หายจริง + แจ้งสำเร็จ
                Response.Redirect("~/Account/CheckDocument_New?deleted=1", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error deleting document: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   Stack: {ex.StackTrace}");

                // Log failed deletion
                try
                {
                    loggingService?.LogException(ex, LoggingService.LogCategory.Accounting,
                        $"Failed to delete document", GetCurrentUserId());
                }
                catch { /* Ignore logging errors */ }

                ShowError($"❌ ลบเอกสารไม่สำเร็จ:\n\n{ex.Message}\n\nกรุณาลองใหม่อีกครั้ง หรือติดต่อผู้ดูแลระบบ");
            }
        }

        protected void gvDetails_SelectedIndexChanging(object sender, GridViewSelectEventArgs e)
        {
            try
            {
                // เอกสาร NextAcc-only (รวมใบที่ยกเลิก/void) → เปิด PDF จาก NextAcc ด้วย GUID โดยตรง
                // (ต้องเช็คก่อน parser เลขเอกสารด้านล่าง เพราะเลข NextAcc เช่น REC-20260716-0005 จะ parse ไม่ได้)
                var dk = gvDetails.DataKeys[e.NewSelectedIndex];
                if (dk != null && (dk["IsNextAccOnly"]?.ToString() == "1"))
                {
                    string naId = dk["NextAccId"]?.ToString() ?? "";
                    string naDocNum = dk["ID"]?.ToString() ?? "";
                    // ใบยกเลิก → ขอ PDF ที่ประทับ "ยกเลิก" จาก NextAcc (ไม่งั้นได้ PDF รุ่น active เดิม)
                    bool naCancelled = (dk["Status"]?.ToString() == "Cancel");
                    if (Guid.TryParse(naId, out var gid) && gid != Guid.Empty)
                    {
                        try
                        {
                            Server.ScriptTimeout = 300;
                            var svc = new AccountingSyncService(conn);
                            var na = System.Threading.Tasks.Task.Run(() =>
                                svc.DownloadNextAccDocumentByIdAsync(gid, naDocNum, false, naCancelled)).GetAwaiter().GetResult();
                            if (na != null && na.Found && !string.IsNullOrEmpty(na.PdfRelativeUrl))
                            {
                                Response.Redirect(na.PdfRelativeUrl);
                                return;
                            }
                            ShowError("NextAcc ไม่มี PDF สำหรับเอกสารนี้: " + (na?.Message ?? "ไม่ทราบสาเหตุ"));
                        }
                        catch (System.Threading.ThreadAbortException) { throw; }
                        catch (Exception nex) { ShowError("เปิด PDF จาก NextAcc ไม่สำเร็จ: " + nex.Message); }
                        return;
                    }
                    ShowError("เอกสาร NextAcc นี้ไม่มีรหัสสำหรับเปิด PDF");
                    return;
                }

                // ใช้เลข local จาก DataKeys — คอลัมน์เลขที่ในตารางแสดง DisplayDoc (เลข NextAcc) แล้ว
                string docStatus = dk?["Status"]?.ToString() ?? gvDetails.Rows[e.NewSelectedIndex].Cells[14].Text;
                string docNum = dk?["ID"]?.ToString() ?? gvDetails.Rows[e.NewSelectedIndex].Cells[4].Text;

                System.Diagnostics.Debug.WriteLine($"📄 Opening document: {docNum}, Status: {docStatus}");

                // ✅ Parse document number (same logic as RowDeleting)
                // Format: REC260401001 (REC + Year(2) + Month(2) + Day(2) + Number(3))
                // or PAY260401001 (PAY + Year(2) + Month(2) + Day(2) + Number(3))
                // Legacy: REC24100001 (REC + Year(2) + Month(2) + Number(4))
                string docType = docNum.Remove(3, docNum.Length - 3); // Get first 3 chars: REC or PAY
                string docYear = "20" + docNum.Remove(0, 3).Remove(2, docNum.Length - 5); // Get year: 24 -> 2024
                string docMonth = Convert.ToInt32(docNum.Remove(0, 5).Remove(2, docNum.Length - 7)).ToString(); // Get month: 10

                System.Diagnostics.Debug.WriteLine($"   Parsed: Type={docType}, Year={docYear}, Month={docMonth}");

                if (docType == "REC")
                {
                    // ── DOCUMENT mode: เอกสารจริงออกบน NextAcc → เปิด PDF จาก NextAcc ก่อน ──
                    // (mirror ฝั่งจ่าย CheckPayment_New) — ใบเสร็จ/ใบกำกับที่ sync แล้วต้องแสดงเอกสาร
                    // ตามที่ NextAcc ออก (เลขที่/ยอด/รูปแบบทางการ) ไม่ใช่ PDF ที่ระบบ render เอง.
                    // smart-cache ในตัว: ครั้งแรกดึง ครั้งต่อไปเปิด local ทันที; ดึงใหม่หลัง edit/re-sync.
                    // ถ้ายังไม่ sync/ดึงไม่ได้ → ตกไปใช้ไฟล์ local เดิมด้านล่าง
                    bool naTimedOut = false;
                    try
                    {
                        var naCfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                        // ลอง NextAcc "ถ้าเปิดใช้ NextAcc" — ไม่ผูกกับ IsReceiptDocumentMode (โหมดปัจจุบัน) เพราะ
                        // เอกสารที่ sync ไปแล้วต้องแสดง PDF ทางการเสมอ แม้ config โหมดจะถูกสลับภายหลัง.
                        // DownloadReceiptPdfFromNextAccAsync คืน Found=false เองถ้าใบนี้ไม่มีเอกสาร NextAcc → ตก local
                        if (naCfg.IsConfigured && naCfg.Enabled)
                        {
                            Server.ScriptTimeout = 300;
                            var naSvc = new Take_Time_BangPhra.Integration.AccountingSyncService(conn);
                            // bound 25 วิ — NextAcc ช้าต้องไม่ทำให้ผู้ใช้ค้าง; task ดึงต่อเบื้องหลัง
                            // จนจบและเขียน cache เอง → กดใหม่รอบหน้าเปิดได้ทันที
                            Take_Time_BangPhra.Integration.NextAccCachedDocument naPdf = null;
                            var naTask = System.Threading.Tasks.Task.Run(() =>
                                naSvc.DownloadReceiptPdfFromNextAccAsync(docNum, docStatus == "Cancel"));
                            if (naTask.Wait(25000)) naPdf = naTask.Result;
                            else naTimedOut = true;
                            if (naPdf != null && naPdf.Found && !string.IsNullOrEmpty(naPdf.PdfRelativeUrl))
                            {
                                Response.Redirect(naPdf.PdfRelativeUrl);
                                return;
                            }
                            // เอกสารบน NextAcc ของเลขนี้ชนกับใบอื่น (ExternalRef ไม่ตรง) → หยุด ไม่ดึงต่อ
                            // ด้วย GUID (เป็น GUID ที่ชนตัวเดียวกัน) กันเปิดเอกสารผิดคน — แจ้งให้กด Retry
                            if (naPdf != null && naPdf.MismatchedIdentity)
                            {
                                ShowError(naPdf.Message ?? "เอกสารบน NextAcc ของเลขนี้ชนกับใบอื่น — กด Retry เพื่อออกเอกสารของใบนี้เอง");
                                return;
                            }

                            // fallback ชั้น 2: การจับคู่ตอนโหลดตาราง (merge) เก็บ GUID เอกสาร NextAcc ไว้ใน
                            // DataKeys["NextAccId"] แล้ว → ดึง PDF ตรงด้วย GUID (ไม่พึ่งการ lookup จากคิว
                            // ที่อาจหาไม่เจอ เช่น payload คนละรูปแบบ/คิวถูกล้าง). ข้ามถ้าชั้นแรก timeout
                            // (NextAcc ช้าทั้งระบบ — ยิงซ้ำมีแต่รอเพิ่ม)
                            string rowNaId = dk?["NextAccId"]?.ToString() ?? "";
                            if (!naTimedOut && Guid.TryParse(rowNaId, out var rowGid) && rowGid != Guid.Empty)
                            {
                                Take_Time_BangPhra.Integration.NextAccCachedDocument byId = null;
                                var byIdTask = System.Threading.Tasks.Task.Run(() =>
                                    naSvc.DownloadNextAccDocumentByIdAsync(rowGid, docNum, false, docStatus == "Cancel"));
                                if (byIdTask.Wait(15000)) byId = byIdTask.Result;
                                else naTimedOut = true;
                                if (byId != null && byId.Found && !string.IsNullOrEmpty(byId.PdfRelativeUrl))
                                {
                                    Response.Redirect(byId.PdfRelativeUrl);
                                    return;
                                }
                            }

                            // ดึง NextAcc ไม่สำเร็จ → log เหตุผล (แทนกลืนเงียบ) เพื่อรู้ว่าทำไมตก local
                            try
                            {
                                codeInstance.Logs(conn, "AccountingSync",
                                    $"CheckDocument ดู PDF: receipt={docNum} → NextAcc ไม่คืน PDF ({(naTimedOut ? "timeout" : naPdf?.Message ?? "unknown")}, byId={rowNaId}) → ใช้ไฟล์ local", "SYSTEM");
                            }
                            catch { }
                        }
                    }
                    catch (System.Threading.ThreadAbortException) { throw; }   // อย่ากลืน redirect
                    catch (Exception naEx)
                    {
                        try { codeInstance.Logs(conn, "AccountingSync", $"CheckDocument ดู PDF: receipt={docNum} NextAcc error: {naEx.Message} → ใช้ไฟล์ local", "SYSTEM"); }
                        catch { }
                    }

                    // Get receipt UID from database (SECURE)
                    string path = ConfigurationManager.AppSettings["ReceiptFolderPath"];
                    var uidParams = new Dictionary<string, object> { { "@DocNum", docNum } };
                    var uidResult = codeInstance.DatabaseQuerySafe(conn,
                        "SELECT [UID] FROM [dbo].[Account_Receipt] WHERE ID = @DocNum",
                        uidParams);

                    string uid = "";
                    if (uidResult != null && uidResult.Rows.Count > 0 && uidResult.Rows[0][0] != DBNull.Value)
                    {
                        uid = uidResult.Rows[0][0].ToString();
                    }

                    System.Diagnostics.Debug.WriteLine($"   UID from DB: '{uid}'");

                    // Build file paths in priority order
                    List<string> filesToCheck = new List<string>();

                    if (docStatus == "Cancel")
                    {
                        // For Cancel status: Try UID version first, then fallback
                        if (!string.IsNullOrEmpty(uid))
                        {
                            filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}_{uid}_Cancel.pdf");
                        }
                        filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}_Cancel.pdf");
                    }
                    else
                    {
                        // For Normal status: Try UID version first, then fallback
                        if (!string.IsNullOrEmpty(uid))
                        {
                            filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}_{uid}.pdf");
                        }
                        filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}.pdf");
                    }

                    // Also check padded month format (for files created with zero-padded month)
                    if (docMonth.PadLeft(2, '0') != docMonth)
                    {
                        string pm = docMonth.PadLeft(2, '0');
                        if (docStatus == "Cancel")
                        {
                            if (!string.IsNullOrEmpty(uid))
                                filesToCheck.Add($"{path}\\{docYear}\\{pm}\\{docNum}_{uid}_Cancel.pdf");
                            filesToCheck.Add($"{path}\\{docYear}\\{pm}\\{docNum}_Cancel.pdf");
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(uid))
                                filesToCheck.Add($"{path}\\{docYear}\\{pm}\\{docNum}_{uid}.pdf");
                            filesToCheck.Add($"{path}\\{docYear}\\{pm}\\{docNum}.pdf");
                        }
                    }

                    // Check each file and redirect to the first one that exists
                    foreach (var filePath in filesToCheck)
                    {
                        System.Diagnostics.Debug.WriteLine($"   Checking: {filePath}");
                        if (File.Exists(filePath))
                        {
                            string relativeUrl = filePath.Replace(path, "/Documents/Receipt").Replace("\\", "/");
                            System.Diagnostics.Debug.WriteLine($"   ✅ Found! Redirecting to: {relativeUrl}");
                            Response.Redirect(relativeUrl);
                            return;
                        }
                    }

                    // If no file found, show error — แยกเคส NextAcc ช้า (ไฟล์กำลังเตรียมเบื้องหลัง กดใหม่ได้)
                    if (naTimedOut)
                        throw new Exception($"NextAcc ตอบช้า — ระบบกำลังเตรียมไฟล์ {docNum} อยู่เบื้องหลัง กรุณากดดูอีกครั้งใน 15-30 วินาที");
                    throw new Exception($"ไม่พบไฟล์ PDF สำหรับเอกสาร {docNum}\n\nตรวจสอบแล้ว:\n{string.Join("\n", filesToCheck)}");
                }
                else if (docType == "PAY")
                {
                    // Get payment UID from database (SECURE)
                    string path = ConfigurationManager.AppSettings["PaymentFolderPath"];
                    var uidParams = new Dictionary<string, object> { { "@DocNum", docNum } };
                    var uidResult = codeInstance.DatabaseQuerySafe(conn,
                        "SELECT [UID] FROM [dbo].[Account_Payment] WHERE ID = @DocNum",
                        uidParams);

                    string uid = "";
                    if (uidResult != null && uidResult.Rows.Count > 0 && uidResult.Rows[0][0] != DBNull.Value)
                    {
                        uid = uidResult.Rows[0][0].ToString();
                    }

                    System.Diagnostics.Debug.WriteLine($"   UID from DB: '{uid}'");

                    // Build file paths in priority order
                    List<string> filesToCheck = new List<string>();

                    if (docStatus == "Cancel")
                    {
                        // For Cancel status: Try UID version first, then fallback
                        if (!string.IsNullOrEmpty(uid))
                        {
                            filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}_{uid}_Cancel.pdf");
                        }
                        filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}_Cancel.pdf");
                    }
                    else
                    {
                        // For Normal status: Try UID version first, then fallback
                        if (!string.IsNullOrEmpty(uid))
                        {
                            filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}_{uid}.pdf");
                        }
                        filesToCheck.Add($"{path}\\{docYear}\\{docMonth}\\{docNum}.pdf");
                    }

                    // Also check padded month format (for files created with zero-padded month)
                    if (docMonth.PadLeft(2, '0') != docMonth)
                    {
                        string pm = docMonth.PadLeft(2, '0');
                        if (docStatus == "Cancel")
                        {
                            if (!string.IsNullOrEmpty(uid))
                                filesToCheck.Add($"{path}\\{docYear}\\{pm}\\{docNum}_{uid}_Cancel.pdf");
                            filesToCheck.Add($"{path}\\{docYear}\\{pm}\\{docNum}_Cancel.pdf");
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(uid))
                                filesToCheck.Add($"{path}\\{docYear}\\{pm}\\{docNum}_{uid}.pdf");
                            filesToCheck.Add($"{path}\\{docYear}\\{pm}\\{docNum}.pdf");
                        }
                    }

                    // Check each file and redirect to the first one that exists
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

                    // If no file found, show error
                    throw new Exception($"ไม่พบไฟล์ PDF สำหรับเอกสาร {docNum}\n\nตรวจสอบแล้ว:\n{string.Join("\n", filesToCheck)}");
                }
                else
                {
                    throw new Exception($"ประเภทเอกสารไม่ถูกต้อง: {docType}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Error: {ex.Message}");
                ShowError("เปิดเอกสารไม่สำเร็จ:\n" + ex.Message);
            }
        }

        protected void gvDetails_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "sync")
            {
                string docId = e.CommandArgument.ToString();
                HandleSyncDocument(docId);
                return;
            }

            // ปุ่ม "📧 ส่ง e-Tax" — เปิดหน้าตรวจ/ส่งอีเมล (pre-fill ผู้รับ/CC/หัวข้อ/เนื้อหา)
            if (e.CommandName == "sendetax")
            {
                try
                {
                    int rowIndex = Convert.ToInt32(e.CommandArgument);
                    var dkE = gvDetails.DataKeys[rowIndex];
                    string docNum = dkE?["ID"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(docNum)) { ShowError("ไม่พบเลขที่เอกสาร"); return; }
                    Response.Redirect("/Account/SendEtax?receipt=" + Server.UrlEncode(docNum));
                }
                catch (System.Threading.ThreadAbortException) { throw; }
                catch (Exception ex) { ShowError("เปิดหน้าส่ง e-Tax ไม่สำเร็จ: " + ex.Message); }
                return;
            }

            // ปุ่ม "🔄 ดึงล่าสุด" — บังคับดึง PDF สดจาก NextAcc ข้าม cache ทุกชั้น (ใช้หลังแก้ไข/
            // ยกเลิกเอกสารแล้วอยากได้ไฟล์รุ่นปัจจุบันทันที ไม่รอ cache หมดอายุ)
            if (e.CommandName == "refreshpdf")
            {
                try
                {
                    int rowIndex = Convert.ToInt32(e.CommandArgument);
                    var dkR = gvDetails.DataKeys[rowIndex];
                    string docNum = dkR?["ID"]?.ToString() ?? "";
                    bool cancelled = (dkR?["Status"]?.ToString() ?? "") == "Cancel";
                    bool isNaOnly = dkR?["IsNextAccOnly"]?.ToString() == "1";
                    string naId = dkR?["NextAccId"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(docNum)) { ShowError("ไม่พบเลขที่เอกสารของแถวนี้"); return; }

                    var naCfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                    if (!naCfg.IsConfigured || !naCfg.Enabled)
                    {
                        ShowError("ยังไม่ได้เปิดใช้ NextAcc — ดึงไฟล์ล่าสุดไม่ได้");
                        return;
                    }

                    Server.ScriptTimeout = 300;
                    var svc = new AccountingSyncService(conn);
                    Take_Time_BangPhra.Integration.NextAccCachedDocument fresh = null;
                    bool timedOut = false;

                    if (isNaOnly && Guid.TryParse(naId, out var gNa) && gNa != Guid.Empty)
                    {
                        // แถว NextAcc-only → ดึงตรงด้วย GUID (forceRefresh)
                        var t = System.Threading.Tasks.Task.Run(() =>
                            svc.DownloadNextAccDocumentByIdAsync(gNa, docNum, true, cancelled));
                        if (t.Wait(45000)) fresh = t.Result; else timedOut = true;
                    }
                    else
                    {
                        // ใบ local ที่ sync แล้ว → เส้นทางเลขใบเสร็จ (forceRefresh) → fallback GUID จากตาราง
                        var t = System.Threading.Tasks.Task.Run(() =>
                            svc.DownloadReceiptPdfFromNextAccAsync(docNum, cancelled, forceRefresh: true));
                        if (t.Wait(45000)) fresh = t.Result; else timedOut = true;

                        if (!timedOut && (fresh == null || !fresh.Found)
                            && Guid.TryParse(naId, out var g2) && g2 != Guid.Empty)
                        {
                            var t2 = System.Threading.Tasks.Task.Run(() =>
                                svc.DownloadNextAccDocumentByIdAsync(g2, docNum, true, cancelled));
                            if (t2.Wait(30000)) fresh = t2.Result; else timedOut = true;
                        }
                    }

                    if (fresh != null && fresh.Found && !string.IsNullOrEmpty(fresh.PdfRelativeUrl))
                    {
                        Response.Redirect(fresh.PdfRelativeUrl);
                        return;
                    }
                    ShowError(timedOut
                        ? $"NextAcc ตอบช้า — ระบบกำลังดึงไฟล์ {docNum} ต่อเบื้องหลัง กรุณากดดู PDF อีกครั้งใน 15-30 วินาที"
                        : $"ดึงไฟล์ล่าสุดไม่สำเร็จ: {fresh?.Message ?? "ไม่ทราบสาเหตุ"}");
                }
                catch (System.Threading.ThreadAbortException) { throw; }
                catch (Exception rex)
                {
                    ShowError("ดึงไฟล์ล่าสุดไม่สำเร็จ: " + rex.Message);
                }
                return;
            }

            if (e.CommandName == "edit")
            {
                try
                {
                    int rowIndex = Convert.ToInt32(e.CommandArgument);

                    // เอกสาร NextAcc-only (สร้างบน NextAcc โดยตรง/ใบยกเลิก) — ไม่มีใบ local ให้แก้
                    var dkEdit = gvDetails.DataKeys[rowIndex];
                    if (dkEdit != null && dkEdit["IsNextAccOnly"]?.ToString() == "1")
                    {
                        ShowError("เอกสารนี้อยู่บน NextAcc (ไม่มีใบในระบบ) — แก้ไขรายละเอียดที่ระบบ NextAcc");
                        return;
                    }

                    // ใช้เลข local จาก DataKeys — คอลัมน์เลขที่ในตารางแสดง DisplayDoc (เลข NextAcc) แล้ว
                    string docNum = dkEdit?["ID"]?.ToString() ?? gvDetails.Rows[rowIndex].Cells[4].Text;

                    System.Diagnostics.Debug.WriteLine($"📝 Edit document: {docNum}, RowIndex: {rowIndex}");

                    // Parse document type
                    string docType = docNum.Length >= 3 ? docNum.Substring(0, 3) : "";

                    if (docType == "REC")
                    {
                        var uidParams = new Dictionary<string, object> { { "@DocNum", docNum } };
                        var uidResult = codeInstance.DatabaseQuerySafe(conn,
                            "SELECT [UID] FROM [Taketime].[dbo].[Account_Receipt] WHERE ID = @DocNum",
                            uidParams);
                        if (uidResult != null && uidResult.Rows.Count > 0)
                        {
                            string uid = uidResult.Rows[0][0].ToString();
                            System.Diagnostics.Debug.WriteLine($"   Redirecting to Receipt edit page, UID={uid}");
                            Response.Redirect("/Account/Receipt?command=edit&uid=" + uid);
                        }
                        else
                        {
                            throw new Exception($"ไม่พบใบเสร็จ {docNum} ในฐานข้อมูล");
                        }
                    }
                    else if (docType == "PAY")
                    {
                        var uidParams = new Dictionary<string, object> { { "@DocNum", docNum } };
                        var uidResult = codeInstance.DatabaseQuerySafe(conn,
                            "SELECT [UID] FROM [Taketime].[dbo].[Account_Payment] WHERE ID = @DocNum",
                            uidParams);
                        if (uidResult != null && uidResult.Rows.Count > 0)
                        {
                            string uid = uidResult.Rows[0][0].ToString();
                            System.Diagnostics.Debug.WriteLine($"   Redirecting to PaymentVoucher edit page, UID={uid}");
                            Response.Redirect("/Account/PaymentVoucher?command=edit&uid=" + uid);
                        }
                        else
                        {
                            throw new Exception($"ไม่พบใบสำคัญจ่าย {docNum} ในฐานข้อมูล");
                        }
                    }
                    else
                    {
                        throw new Exception($"ประเภทเอกสารไม่ถูกต้อง: {docType}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"   ❌ Error editing document: {ex.Message}");
                    ShowError("แก้ไขเอกสารไม่สำเร็จ: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Handle RowEditing event (for compatibility with CommandField EditButton)
        /// Redirects to RowCommand handler
        /// </summary>
        protected void gvDetails_RowEditing(object sender, GridViewEditEventArgs e)
        {
            try
            {
                int rowIndex = e.NewEditIndex;
                // ✅ FIX: Get document number from correct column (Cells[4], not Cells[3])
                string docNum = gvDetails.Rows[rowIndex].Cells[4].Text;

                System.Diagnostics.Debug.WriteLine($"📝 RowEditing triggered: {docNum}, RowIndex: {rowIndex}");

                // Parse document type
                string docType = docNum.Length >= 3 ? docNum.Substring(0, 3) : "";

                if (docType == "REC")
                {
                    // SECURE: Get receipt UID with parameterized query
                    var receiptUidParams = new Dictionary<string, object>
                    {
                        { "@ID", docNum }
                    };
                    var uidResult = codeInstance.DatabaseQuerySafe(conn,
                        "SELECT [UID] FROM [Taketime].[dbo].[Account_Receipt] WHERE ID = @ID",
                        receiptUidParams);

                    if (uidResult != null && uidResult.Rows.Count > 0)
                    {
                        string uid = uidResult.Rows[0][0].ToString();
                        System.Diagnostics.Debug.WriteLine($"   Redirecting to Receipt edit page, UID={uid}");
                        Response.Redirect("/Account/Receipt?command=edit&uid=" + uid);
                    }
                    else
                    {
                        throw new Exception($"ไม่พบใบเสร็จ {docNum} ในฐานข้อมูล");
                    }
                }
                else if (docType == "PAY")
                {
                    // SECURE: Get payment UID with parameterized query
                    var paymentUidParams = new Dictionary<string, object>
                    {
                        { "@ID", docNum }
                    };
                    var uidResult = codeInstance.DatabaseQuerySafe(conn,
                        "SELECT [UID] FROM [Taketime].[dbo].[Account_Payment] WHERE ID = @ID",
                        paymentUidParams);

                    if (uidResult != null && uidResult.Rows.Count > 0)
                    {
                        string uid = uidResult.Rows[0][0].ToString();
                        System.Diagnostics.Debug.WriteLine($"   Redirecting to PaymentVoucher edit page, UID={uid}");
                        Response.Redirect("/Account/PaymentVoucher?command=edit&uid=" + uid);
                    }
                    else
                    {
                        throw new Exception($"ไม่พบใบสำคัญจ่าย {docNum} ในฐานข้อมูล");
                    }
                }
                else
                {
                    throw new Exception($"ประเภทเอกสารไม่ถูกต้อง: {docType}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Error in RowEditing: {ex.Message}");
                ShowError("แก้ไขเอกสารไม่สำเร็จ: " + ex.Message);
                e.Cancel = true; // Cancel edit mode
            }
        }

        private void ShowError(string message)
        {
            // ต้อง escape ก่อนฝังใน alert('...') — ข้อความมี \n / ' / \ (เช่น รายการ path ที่ตรวจ)
            // จะทำให้ JS พังเงียบ ๆ → ผู้ใช้เห็นแค่หน้ารีโหลดเด้งขึ้นบนโดยไม่มีข้อความอะไรเลย
            // (หน้า CheckPayment_New ใช้วิธีเดียวกันอยู่แล้ว)
            string safe = System.Web.HttpUtility.JavaScriptStringEncode(message ?? "");
            ScriptManager.RegisterStartupScript(this, GetType(), "error", $"alert('{safe}');", true);
        }

        /// <summary>
        /// Get current user ID from session
        /// </summary>
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

        /// <summary>
        /// Get payment method name in Thai by legacy ID (hard-coded, no table lookup)
        /// Legacy mapping: 1=KBANK, 2=CASH, 3=DIRECTOR, 4=KTB
        /// Returns keyword to search (flexible matching)
        /// </summary>
        private string GetPaymentMethodNameByLegacyId(int legacyId)
        {
            // Use keywords that are likely to be in the Paid_Type string
            // Changed from exact match to keyword match for better flexibility
            switch (legacyId)
            {
                case 1: return "กสิกร"; // KBANK - keyword match (works for "โอนกสิกร", "โอน กสิกร", "โอนเงิน กสิกร" etc.)
                case 2: return "เงินสด"; // CASH - exact match
                case 3: return "กรรมการ"; // DIRECTOR - keyword match (works for "เงินกรรมการ", "เงิน กรรมการ" etc.)
                case 4: return "กรุงไทย"; // KTB - keyword match (works for "โอนกรุงไทย", "โอน กรุงไทย" etc.)
                default: return "";
            }
        }

        /// <summary>
        /// Check if receipt has a payment slip
        /// </summary>
        protected bool HasSlip(object receiptId)
        {
            try
            {
                if (receiptId == null || string.IsNullOrEmpty(receiptId.ToString()))
                    return false;

                string query = @"
                    SELECT COUNT(*) as SlipCount
                    FROM Payment_History ph
                    INNER JOIN Payment_Slips ps ON ph.PaymentSlip_ID = ps.ID
                    WHERE ph.Account_Receipt_ID = @ReceiptId
                      AND ps.SlipFileURL IS NOT NULL
                      AND ps.IsActive = 1";

                var parameters = new Dictionary<string, object>
                {
                    { "@ReceiptId", receiptId.ToString() }
                };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);
                if (dt.Rows.Count > 0)
                {
                    int slipCount = Convert.ToInt32(dt.Rows[0]["SlipCount"]);
                    return slipCount > 0;
                }
            }
            catch (Exception ex)
            {
                codeInstance.Logs(conn, "HasSlip Error", $"ReceiptID: {receiptId}, Error: {ex.Message}", "SYSTEM");
            }
            return false;
        }

        /// <summary>
        /// Get slip file URL for receipt
        /// Returns first slip if multiple slips exist
        /// </summary>
        protected string GetSlipURL(object receiptId)
        {
            try
            {
                if (receiptId == null || string.IsNullOrEmpty(receiptId.ToString()))
                    return "#";

                string query = @"
                    SELECT TOP 1 ps.SlipFileURL
                    FROM Payment_History ph
                    INNER JOIN Payment_Slips ps ON ph.PaymentSlip_ID = ps.ID
                    WHERE ph.Account_Receipt_ID = @ReceiptId
                      AND ps.SlipFileURL IS NOT NULL
                      AND ps.IsActive = 1
                    ORDER BY ph.PaymentDate DESC";

                var parameters = new Dictionary<string, object>
                {
                    { "@ReceiptId", receiptId.ToString() }
                };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);
                if (dt.Rows.Count > 0)
                {
                    string slipFileURL = dt.Rows[0]["SlipFileURL"].ToString();
                    if (!string.IsNullOrEmpty(slipFileURL))
                    {
                        // Return relative URL (already in correct format: Upload/Slip/xxx.jpg)
                        return ResolveUrl("~/" + slipFileURL);
                    }
                }
            }
            catch (Exception ex)
            {
                codeInstance.Logs(conn, "GetSlipURL Error", $"ReceiptID: {receiptId}, Error: {ex.Message}", "SYSTEM");
            }
            return "#";
        }

        // ──────────────────────────────────────────────
        // Accounting Sync Status
        // ──────────────────────────────────────────────

        private Dictionary<string, DataRow> _syncStatusCache;
        private HashSet<string> _etaxReceipts;   // เลขใบเสร็จที่มี e-Tax แล้ว (ในช่วงวันที่) → โชว์ปุ่มส่ง e-Tax

        private void LoadSyncStatusCache()
        {
            _syncStatusCache = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var dt = codeInstance.DatabaseQuerySafe(conn,
                    @"SELECT ID, Entity_Type, Action_Type, Status, Error_Message, Payload,
                             Nexaacc_Response_Id, Nexaacc_Document_Number, Nexaacc_Document_Type
                      FROM Accounting_Sync_Queue
                      WHERE (Entity_Type = 'RECEIPT' AND Action_Type = 'CREATE_RECEIPT_DOCUMENT')
                         OR (Entity_Type = 'VOUCHER' AND Action_Type = 'CREATE_VOUCHER_JOURNAL')
                         OR (Entity_Type = 'RESERVATION')
                      ORDER BY ID DESC",
                    null);

                if (dt == null) return;

                foreach (DataRow row in dt.Rows)
                {
                    string payload = row["Payload"]?.ToString() ?? "";

                    string key = ExtractJsonValue(payload, "receiptNumber");
                    if (string.IsNullOrEmpty(key))
                        key = ExtractJsonValue(payload, "documentNumber");

                    if (!string.IsNullOrEmpty(key) && !_syncStatusCache.ContainsKey(key))
                        _syncStatusCache[key] = row;
                }
            }
            catch { }
        }

        private string ExtractJsonValue(string json, string key)
        {
            string search = "\"" + key + "\":\"";
            int idx = json.IndexOf(search);
            if (idx < 0) return null;
            idx += search.Length;
            int end = json.IndexOf("\"", idx);
            return end > idx ? json.Substring(idx, end - idx) : null;
        }

        protected void gvDetails_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;

            var lblSync = (Label)e.Row.FindControl("lblSyncStatus");
            var btnSync = (Button)e.Row.FindControl("btnSync");
            if (lblSync == null || btnSync == null) return;

            string docId = DataBinder.Eval(e.Row.DataItem, "ID")?.ToString() ?? "";
            string docStatus = DataBinder.Eval(e.Row.DataItem, "Status")?.ToString() ?? "";

            // แถวเอกสาร NextAcc-only — คงปุ่ม "ดู PDF"; ใบ active คงปุ่มลบไว้เป็นปุ่ม "ยกเลิก" (enqueue
            // void ไป NextAcc); ใบที่ยกเลิกแล้วซ่อนปุ่มลบ; ซ่อนแก้ไข/sync เสมอ (ไม่มีใบ local)
            string isNaOnly = DataBinder.Eval(e.Row.DataItem, "IsNextAccOnly")?.ToString() ?? "0";
            if (isNaOnly == "1")
            {
                bool voided = docStatus == "Cancel";
                lblSync.Text = voided
                    ? "<span class='sync-badge failed' title='เอกสารถูกยกเลิกบน NextAcc'>❌ ยกเลิก (NextAcc)</span>"
                    : "<span class='sync-badge completed' title='เอกสารสร้างบน NextAcc'>NextAcc</span>";
                btnSync.Visible = false;
                try
                {
                    if (e.Row.Cells.Count > 0)
                        foreach (Control c in e.Row.Cells[0].Controls)
                            if (c is Button bDel)
                            {
                                if (voided) bDel.Visible = false;         // ยกเลิกแล้ว — ไม่มีอะไรให้ทำ
                                else bDel.Text = "🚫 ยกเลิก";             // active → ยกเลิกบน NextAcc ได้
                            }
                    var bEdit = e.Row.FindControl("btnEdit") as Button;
                    if (bEdit != null) bEdit.Visible = false;             // ปุ่มแก้ไข (ไม่มีใบ local)
                }
                catch { }
                if (voided) e.Row.Attributes["style"] = "background-color:#fff3f3;color:#a00;";
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

            // ปุ่ม "📧 ส่ง e-Tax" — โชว์เฉพาะใบที่มี e-Tax แล้ว (local rows; NextAcc-only ไม่มีใบในระบบ return ไปก่อนแล้ว)
            var btnEtax = e.Row.FindControl("btnSendEtax") as Button;
            if (btnEtax != null)
                btnEtax.Visible = _etaxReceipts != null && !string.IsNullOrEmpty(docId) && _etaxReceipts.Contains(docId);
        }

        private void HandleSyncDocument(string docId)
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

                string docType = docId.Length >= 3 ? docId.Substring(0, 3) : "";
                var sync = new AccountingSyncService(conn);
                long queueId = -1;

                // Cancel existing pending/failed entries so we always create fresh
                sync.PrepareResync(docId);

                if (docType == "REC")
                {
                    var docParams = new Dictionary<string, object> { { "@ID", docId } };
                    var dt = codeInstance.DatabaseQuerySafe(conn,
                        @"SELECT ar.Total_Amount, ar.Vat, ar.Created_Date, ar.Reservation_ID,
                                 ar.IsDeposit, ar.Paid_Type,
                                 ISNULL(c.FullName, '-') AS CustomerName
                          FROM Account_Receipt ar
                          LEFT JOIN Reservation res ON res.ID = ar.Reservation_ID
                          LEFT JOIN Customer c ON c.MobilePhone = res.Customer_MobilePhone
                          WHERE ar.ID = @ID", docParams);

                    if (dt?.Rows.Count > 0)
                    {
                        var r = dt.Rows[0];
                        int resId = r["Reservation_ID"] != DBNull.Value ? Convert.ToInt32(r["Reservation_ID"]) : 0;
                        bool isDeposit = r["IsDeposit"] != DBNull.Value && Convert.ToBoolean(r["IsDeposit"]);
                        string paidType = r["Paid_Type"]?.ToString() ?? "CASH";
                        string rcptPayAccId = sync.LookupPaidHowAccountId(paidType);
                        queueId = sync.EnqueueReceipt(resId, docId,
                            Convert.ToDecimal(r["Total_Amount"]),
                            Convert.ToDecimal(r["Vat"]),
                            Convert.ToDateTime(r["Created_Date"]),
                            r["CustomerName"]?.ToString() ?? "",
                            isDeposit: isDeposit, paymentMethod: paidType,
                            revenueType: "ROOM_REVENUE", paymentAccountId: rcptPayAccId);
                    }
                }
                else if (docType == "PAY")
                {
                    var docParams = new Dictionary<string, object> { { "@ID", docId } };
                    var dt = codeInstance.DatabaseQuerySafe(conn,
                        @"SELECT ap.Total_Amount, ap.Vat, ap.Paid_How, ap.Paid_Type, ap.Created_Date,
                                 ISNULL(ap.WHT_Rate, 0) AS WHT_Rate, ISNULL(ap.WHT_Amount, 0) AS WHT_Amount,
                                 ISNULL(ap.IsCredit, 0) AS IsCredit,
                                 ISNULL(v.Name, '-') AS Vendor_Name
                          FROM Account_Payment ap
                          LEFT JOIN Vendor v ON ap.Vendor_ID = v.ID
                          WHERE ap.ID = @ID", docParams);

                    if (dt?.Rows.Count > 0)
                    {
                        var r = dt.Rows[0];
                        string desc = "";
                        // Read all detail rows to rebuild per-line expense data
                        DataTable detDt = null;
                        try
                        {
                            detDt = codeInstance.DatabaseQuerySafe(conn,
                                @"SELECT Number, Detail, Amount,
                                         ISNULL(Paid_Type_Name, N'') AS PaidTypeName,
                                         ISNULL(CAST(Nexaacc_AccountId AS NVARCHAR(50)), N'') AS NexaaccAccountId
                                  FROM Account_Payment_Detail WHERE Payment_ID = @ID ORDER BY Number", docParams);
                        }
                        catch
                        {
                            detDt = codeInstance.DatabaseQuerySafe(conn,
                                "SELECT Number, Detail, Amount FROM Account_Payment_Detail WHERE Payment_ID = @ID ORDER BY Number", docParams);
                        }
                        if (detDt?.Rows.Count > 0) desc = detDt.Rows[0]["Detail"]?.ToString() ?? "";

                        string paidHow = r["Paid_How"]?.ToString() ?? "CASH";
                        string paidTypeV = r["Paid_Type"]?.ToString() ?? "OTHER";
                        string payVAccId = sync.LookupPaidHowAccountId(paidHow);
                        string expVAccId = sync.LookupPaidTypeAccountId(paidTypeV);

                        // Rebuild expenseLines from detail rows (preserve per-line categories)
                        var expenseLines = new List<Dictionary<string, object>>();
                        if (detDt != null && detDt.Rows.Count > 0)
                        {
                            bool hasPerLine = detDt.Columns.Contains("PaidTypeName");
                            for (int i = 0; i < detDt.Rows.Count; i++)
                            {
                                string lineCat = hasPerLine ? detDt.Rows[i]["PaidTypeName"]?.ToString() : paidTypeV;
                                if (string.IsNullOrEmpty(lineCat)) lineCat = paidTypeV;
                                string lineAccId = hasPerLine ? detDt.Rows[i]["NexaaccAccountId"]?.ToString() : null;
                                if (string.IsNullOrEmpty(lineAccId))
                                    lineAccId = sync.LookupPaidTypeAccountId(lineCat);

                                expenseLines.Add(new Dictionary<string, object>
                                {
                                    { "category", lineCat },
                                    { "description", detDt.Rows[i]["Detail"]?.ToString() ?? "" },
                                    { "amount", Convert.ToDecimal(detDt.Rows[i]["Amount"]) },
                                    { "accountId", lineAccId ?? "" }
                                });
                            }
                        }

                        decimal cdnWhtRate = r.Table.Columns.Contains("WHT_Rate") && r["WHT_Rate"] != DBNull.Value ? Convert.ToDecimal(r["WHT_Rate"]) : 0;
                        decimal cdnWhtAmount = r.Table.Columns.Contains("WHT_Amount") && r["WHT_Amount"] != DBNull.Value ? Convert.ToDecimal(r["WHT_Amount"]) : 0;

                        // Check IsCredit and auto-payment flags
                        bool cdnIsCredit = false;
                        try { cdnIsCredit = dt.Columns.Contains("IsCredit") && r["IsCredit"] != DBNull.Value && Convert.ToBoolean(r["IsCredit"]); } catch { }
                        bool cdnIsCashOrBank = sync.IsPaidHowCashOrBank(paidHow);

                        queueId = sync.EnqueuePaymentVoucher(0,
                            paidTypeV,
                            Convert.ToDecimal(r["Total_Amount"]),
                            paidHow,
                            Convert.ToDateTime(r["Created_Date"]),
                            desc,
                            r["Vendor_Name"]?.ToString() ?? "",
                            hasInputVat: Convert.ToDecimal(r["Vat"]) > 0,
                            whtRate: cdnWhtRate, whtAmount: cdnWhtAmount,
                            documentNumber: docId,
                            paymentAccountId: payVAccId, expenseAccountId: expVAccId,
                            expenseLines: expenseLines.Count > 0 ? expenseLines : null,
                            isCredit: cdnIsCredit,
                            autoRecordPayment: cdnIsCashOrBank && !cdnIsCredit);
                    }
                }

                if (queueId > 0)
                {
                    _syncStatusCache = null;
                    // Post-Redirect-Get: redirect with success flag so F5 doesn't re-POST
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

        // Helper method to generate slip link button HTML
        protected string GetSlipLinkButton(object slipFileURL, object hasSlip)
        {
            try
            {
                // Check if slip exists
                int hasSlipValue = 0;
                if (hasSlip != null && hasSlip != DBNull.Value)
                {
                    hasSlipValue = Convert.ToInt32(hasSlip);
                }

                if (hasSlipValue == 0 || slipFileURL == null || slipFileURL == DBNull.Value)
                {
                    return "<span style='color: #95a5a6; font-size: 12px;'>ไม่มีสลิป</span>";
                }

                string slipPath = slipFileURL.ToString();
                if (string.IsNullOrEmpty(slipPath))
                {
                    return "<span style='color: #95a5a6; font-size: 12px;'>ไม่มีสลิป</span>";
                }

                // Build full URL
                string fullUrl = ResolveUrl("~/" + slipPath);
                return $"<a href='{fullUrl}' target='_blank' class='btn-view-slip' style='display: inline-block; padding: 5px 10px; background-color: #3498db; color: white; text-decoration: none; border-radius: 3px; font-size: 12px;'>🔗 ดูสลิป</a>";
            }
            catch
            {
                return "<span style='color: #e74c3c; font-size: 12px;'>ข้อผิดพลาด</span>";
            }
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
