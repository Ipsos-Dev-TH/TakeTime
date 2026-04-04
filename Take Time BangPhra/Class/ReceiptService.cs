using System;
using System.Data;
using System.IO;
using System.Web;
using System.Net.Mail;
using System.Globalization;
using System.Threading.Tasks;
using Take_Time_BangPhra.Helpers;
using Take_Time_BangPhra.Integration;
using Microsoft.Reporting.WebForms;
using iTextSharp.text.pdf;
using ECertificateAPI;
using System.Configuration;

namespace Take_Time_BangPhra.Services
{
    public class ReceiptService
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly code _code;
        private readonly CodeHelper _codeHelper;
        private readonly EmailService _emailService;
        private readonly string _receiptFolderPath;
        private readonly string _imagesFolderPath;
        private readonly string _staffSignatureFolderPath;
        private readonly string _baseFolderPath;
        private readonly string _connectionString;

        public ReceiptService()
        {
            _dbHelper = new DatabaseHelper();
            _code = new code();
            _connectionString = DatabaseHelper.GetConnectionString();
            _codeHelper = new CodeHelper();
            _emailService = new EmailService();
            _receiptFolderPath = ConfigurationManager.AppSettings["ReceiptFolderPath"] ?? "~/Documents/Receipt";
            _imagesFolderPath = ConfigurationManager.AppSettings["ImagesFolderPath"] ?? "~/Images";
            _staffSignatureFolderPath = ConfigurationManager.AppSettings["StaffSignatureFolderPath"] ?? "~/Signatures";
            _baseFolderPath = ConfigurationManager.AppSettings["BaseFolderPath"] ?? "~/";
        }

        public async Task CancelReceipt(string receiptId, string uid)
        {
            try
            {
                // Update receipt status in database
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@receiptId", receiptId }
                };
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE [dbo].[Account_Receipt] SET [Status] = 'Cancel' WHERE ID = @receiptId",
                    parameters);

                // Stamp PDF with cancellation mark
                await StampPdfWithCancellation(receiptId, uid);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error canceling receipt {receiptId}: {ex.Message}");
                throw;
            }
        }

        private async Task StampPdfWithCancellation(string receiptId, string uid)
        {
            // Get receipt details to determine file paths
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@receiptId", receiptId }
            };
            DataTable dtRec = _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM Account_Receipt WHERE ID = @receiptId",
                parameters);

            if (dtRec.Rows.Count == 0) return;

            DateTime createdDate = Convert.ToDateTime(dtRec.Rows[0]["Created_Date"]);
            string year = createdDate.Year.ToString();
            string month = createdDate.Month.ToString("00");

            // Determine input and output file paths
            string inputPdfPath = GetPdfFilePath(receiptId, uid, year, month, false);
            string outputPdfPath = GetPdfFilePath(receiptId, uid, year, month, true);

            if (!File.Exists(inputPdfPath))
            {
                System.Diagnostics.Trace.TraceWarning($"PDF file not found: {inputPdfPath}");
                return;
            }

            string cancelImagePath = Path.Combine(_imagesFolderPath, "Cancel.png");
            if (!File.Exists(cancelImagePath))
            {
                System.Diagnostics.Trace.TraceWarning($"Cancel image not found: {cancelImagePath}");
                return;
            }

            await Task.Run(() =>
            {
                using (Stream inputPdfStream = new FileStream(inputPdfPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (Stream inputImageStream = new FileStream(cancelImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (Stream outputPdfStream = new FileStream(outputPdfPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var reader = new PdfReader(inputPdfStream);
                    var stamper = new PdfStamper(reader, outputPdfStream);
                    var pdfContentByte = stamper.GetOverContent(1);

                    iTextSharp.text.Image image = iTextSharp.text.Image.GetInstance(inputImageStream);
                    image.SetAbsolutePosition(100, 100);
                    pdfContentByte.AddImage(image);
                    stamper.Close();
                }

                // Delete original file
                File.Delete(inputPdfPath);

                // Try to delete etax version if exists
                string etaxFilePath = inputPdfPath.Replace(".pdf", "_etax.pdf");
                if (File.Exists(etaxFilePath))
                {
                    File.Delete(etaxFilePath);
                }
            });
        }

        private string GetPdfFilePath(string receiptId, string uid, string year, string month, bool isCancelled)
        {
            string basePath = Path.Combine(_receiptFolderPath, year, month);
            string fileName = isCancelled ?
                $"{receiptId}_{uid}_Cancel.pdf" :
                $"{receiptId}_{uid}.pdf";

            return Path.Combine(basePath, fileName);
        }

        public DataTable GetReceiptsByReservation(string reservationId)
        {
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };
            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM Account_Receipt WHERE Status = 'Normal' AND Reservation_ID = @reservationId",
                parameters);
        }

        public DataTable GetReceiptByUid(string uid)
        {
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@uid", uid }
            };
            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM Account_Receipt LEFT JOIN Reservation ON Reservation.ID = Reservation_ID WHERE Account_Receipt.UID = @uid",
                parameters);
        }

        public DataTable GetReceiptDetails(string receiptId)
        {
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@receiptId", receiptId }
            };
            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM Account_Receipt_Detail WHERE Receipt_ID = @receiptId ORDER BY Number ASC",
                parameters);
        }

        public string CreateReceiptDocumentNumber(DateTime documentDate)
        {
            return _dbHelper.GenerateDocumentNumber("Account_Receipt", "REC", documentDate);
        }

        public bool ValidateReceiptData(DataTable dtReserve, double expectedTotal)
        {
            double calculatedTotal = 0;

            foreach (DataRow row in dtReserve.Rows)
            {
                double pricePerPiece = Convert.ToDouble(row["Price_PerPeice"]);
                double productAmount = Convert.ToDouble(row["Product_Amount"]);
                double priceAmount = Convert.ToDouble(row["Price_Amount"]);

                double calculatedAmount = CalculateTwoDecimalPoints(pricePerPiece * productAmount);

                // Check if Price_Amount is calculated correctly
                if (Math.Abs(priceAmount - calculatedAmount) > 0.01) // Allow 0.01 tolerance
                {
                    // Fix the value
                    row["Price_Amount"] = calculatedAmount;
                }

                calculatedTotal += calculatedAmount;
            }

            // Check if total matches expected
            return Math.Abs(calculatedTotal - expectedTotal) <= 0.01;
        }

        private double CalculateTotalFromReserve(DataTable dtReserve)
        {
            double total = 0;
            foreach (DataRow row in dtReserve.Rows)
            {
                total += Convert.ToDouble(row["Price_Amount"]);
            }
            return CalculateTwoDecimalPoints(total);
        }

        /// <summary>
        /// ปรับสัดส่วนรายละเอียดใน dtReserve ให้ตรงกับยอดรวมที่กำหนด
        /// ใช้สำหรับกรณีที่มีการแก้ไขราคาห้องพัก แต่รายละเอียดยังเป็นราคาเดิม
        /// </summary>
        private void AdjustReserveDataToMatch(DataTable dtReserve, double expectedTotal, string reservationId)
        {
            if (dtReserve == null || dtReserve.Rows.Count == 0)
                return;

            // คำนวณยอดรวมปัจจุบันจาก dtReserve
            double currentTotal = CalculateTotalFromReserve(dtReserve);

            // ถ้ายอดตรงกันอยู่แล้ว (ผิดพลาดไม่เกิน 0.5 บาท) ไม่ต้องปรับ
            if (Math.Abs(currentTotal - expectedTotal) <= 0.5)
                return;

            // คำนวณอัตราส่วนการปรับ
            double adjustmentRatio = expectedTotal / currentTotal;

            // Log การปรับสัดส่วน
            _codeHelper.Log(_dbHelper.ConnectionString, "Receipt Amount Adjustment",
                $"Reservation {reservationId}: Adjusting details from {currentTotal:F2} to {expectedTotal:F2} (ratio: {adjustmentRatio:F4})",
                "SYSTEM");

            double adjustedTotal = 0;
            int lastIndex = dtReserve.Rows.Count - 1;

            // ปรับทุกแถวตามอัตราส่วน
            for (int i = 0; i < dtReserve.Rows.Count; i++)
            {
                DataRow row = dtReserve.Rows[i];
                double originalPricePerPiece = Convert.ToDouble(row["Price_PerPeice"]);
                double originalPriceAmount = Convert.ToDouble(row["Price_Amount"]);

                // ปรับราคาต่อหน่วยและราคารวมตามอัตราส่วน
                double adjustedPricePerPiece = CalculateTwoDecimalPoints(originalPricePerPiece * adjustmentRatio);
                double adjustedPriceAmount = CalculateTwoDecimalPoints(originalPriceAmount * adjustmentRatio);

                row["Price_PerPeice"] = adjustedPricePerPiece;
                row["Price_Amount"] = adjustedPriceAmount;

                adjustedTotal += adjustedPriceAmount;
            }

            // ปรับส่วนต่างจากการปัดเศษให้กับรายการสุดท้าย
            double difference = CalculateTwoDecimalPoints(expectedTotal - adjustedTotal);
            if (Math.Abs(difference) > 0.01)
            {
                DataRow lastRow = dtReserve.Rows[lastIndex];
                double lastPriceAmount = Convert.ToDouble(lastRow["Price_Amount"]);
                lastRow["Price_Amount"] = CalculateTwoDecimalPoints(lastPriceAmount + difference);

                // ปรับ Price_PerPeice ของรายการสุดท้ายด้วย
                double productAmount = Convert.ToDouble(lastRow["Product_Amount"]);
                if (productAmount > 0)
                {
                    lastRow["Price_PerPeice"] = CalculateTwoDecimalPoints((lastPriceAmount + difference) / productAmount);
                }
            }
        }

        public double CalculateTwoDecimalPoints(double num)
        {
            return Math.Round(num, 2);
        }

        public void CreateReceipt(string reservationId, double totalAmount, DataTable dtReserve, bool isDeposit,
                                DateTime docDate, bool etax, string paidType, string createdById, string customerId = "0")
        {
            try
            {
                if (totalAmount > 0)
                {
                    string receiptId = CreateReceiptDocumentNumber(docDate);
                    DataTable dtUseVat = _dbHelper.ExecuteQuery("SELECT Use_Vat FROM Business_Info");

                    double priceExcludeVat = totalAmount;
                    double vat = 0;

                    if (dtUseVat.Rows.Count > 0 && dtUseVat.Rows[0]["Use_Vat"].ToString() == "True")
                    {
                        priceExcludeVat = (totalAmount * 100) / 107;
                        vat = totalAmount - priceExcludeVat;
                        priceExcludeVat = CalculateTwoDecimalPoints(priceExcludeVat);
                        vat = CalculateTwoDecimalPoints(vat);
                    }

                    // ปรับสัดส่วนรายละเอียดให้ตรงกับยอดรวมก่อนบันทึก
                    if (!isDeposit)
                    {
                        AdjustReserveDataToMatch(dtReserve, totalAmount, reservationId);
                    }

                    if (isDeposit)
                    {
                        CreateDepositReceipt(receiptId, reservationId, docDate, totalAmount, vat, priceExcludeVat, paidType, createdById, etax, customerId);
                    }
                    else
                    {
                        CreateRegularReceipt(receiptId, reservationId, docDate, totalAmount, vat, priceExcludeVat, paidType, createdById, dtReserve, etax, customerId);
                    }

                    // Generate PDF report
                    GenerateReceiptPdf(receiptId, docDate);

                    // Handle e-Tax if enabled
                    if (etax)
                    {
                        GenerateETaxDocument(receiptId, docDate);
                    }

                    // Enqueue accounting sync to Nexaacc
                    EnqueueAccountingSync(reservationId, receiptId, totalAmount, vat, isDeposit, paidType, docDate, customerId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error creating receipt for reservation {reservationId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ส่งข้อมูลใบเสร็จไป Nexaacc Accounting System แบบ async ผ่าน queue
        /// ไม่ block การทำงานหลัก - ถ้า accounting sync ล้มเหลวจะ retry อัตโนมัติ
        /// </summary>
        private void EnqueueAccountingSync(string reservationId, string receiptId, double totalAmount,
            double vat, bool isDeposit, string paidType, DateTime docDate, string customerId)
        {
            try
            {
                var syncService = new AccountingSyncService();

                // ดึงชื่อลูกค้าจากการจอง
                string customerName = GetCustomerName(reservationId);
                int resId = 0;
                int.TryParse(reservationId, out resId);

                if (isDeposit)
                {
                    // มัดจำ: DR Cash/Bank, CR Advance Deposit (เงินรับล่วงหน้า)
                    syncService.EnqueueReservationDeposit(
                        resId,
                        (decimal)totalAmount,
                        paidType,
                        docDate,
                        customerName);
                }
                else
                {
                    // ชำระเต็ม: DR Cash/Bank, CR Room Revenue (+ VAT ถ้ามี)
                    bool hasVat = vat > 0;
                    syncService.EnqueueReservationPayment(
                        resId,
                        (decimal)totalAmount,
                        paidType,
                        docDate,
                        customerName,
                        hasVat);
                }

                // สร้าง Receipt Document ใน Nexaacc ด้วย
                syncService.EnqueueReceipt(
                    resId,
                    receiptId,
                    (decimal)totalAmount,
                    (decimal)vat,
                    docDate,
                    customerName);

                System.Diagnostics.Trace.TraceInformation(
                    $"Enqueued accounting sync for Receipt {receiptId} (Reservation {reservationId}, {(isDeposit ? "Deposit" : "Payment")})");
            }
            catch (Exception ex)
            {
                // ไม่ throw — accounting sync เป็น supplementary, ไม่ควรทำให้ receipt creation ล้มเหลว
                System.Diagnostics.Trace.TraceWarning(
                    $"Failed to enqueue accounting sync for Receipt {receiptId}: {ex.Message}");
            }
        }

        private string GetCustomerName(string reservationId)
        {
            try
            {
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT c.FullName FROM Reservation r
                      LEFT JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                      WHERE r.ID = @reservationId",
                    parameters);
                return dt.Rows.Count > 0 ? dt.Rows[0]["FullName"]?.ToString() ?? "ลูกค้าทั่วไป" : "ลูกค้าทั่วไป";
            }
            catch { return "ลูกค้าทั่วไป"; }
        }

        public void CreateDepositReceipt(string receiptId, string reservationId, DateTime docDate,
                                    double totalAmount, double vat, double priceExcludeVat,
                                    string paidType, string createdById, bool etax, string customerId = "0")
        {
            string uid = Guid.NewGuid().ToString("N"); // Generate unique identifier for receipt

            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@receiptId", receiptId },
                { "@uid", uid },
                { "@reservationId", reservationId },
                { "@docDate", docDate.ToString("yyyy-MM-dd") },
                { "@totalAmount", totalAmount },
                { "@vat", vat },
                { "@priceExcludeVat", priceExcludeVat },
                { "@paidType", paidType },
                { "@createdById", createdById },
                { "@etax", etax },
                { "@customerId", customerId }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO [dbo].[Account_Receipt]
                    (ID, UID, [Reservation_ID], [Created_Date], [Total_Amount], [Vat],
                     [Total_Amount_Exclude_Vat], [IsDeposit], [UseDeposit], Status,
                     Paid_Type, Created_By_ID, Etax, Customer_ID)
                    VALUES (@receiptId, @uid, @reservationId, @docDate,
                    @totalAmount, @vat, @priceExcludeVat, 'True', 'False', 'Normal',
                    @paidType, @createdById, @etax, @customerId)",
                parameters);

            // Insert receipt detail
            var detailParameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@receiptId", receiptId },
                { "@reservationId", reservationId },
                { "@totalAmount", totalAmount }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO [dbo].[Account_Receipt_Detail]
                  ([Number], [Receipt_ID], [ProductType_ID], [Product_ID],
                   [Product_Data], [Product_Amount], [Product_Unit],
                   [Price_PerPeice], [Price_Amount])
                  VALUES ('1', @receiptId, 1, 7,
                  N'ค่ามัดจำที่พักของหมายเลขการจอง ' + @reservationId + ' [' + @receiptId + ']',
                  '1', N'ครั้ง', @totalAmount, @totalAmount)",
                detailParameters);

            // 🔧 FIX: Create Payment_History record
            try
            {
                // Get remaining balance
                var balanceParams = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };

                var balanceResult = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        r.TotalPrice,
                        ISNULL(SUM(ph.PaymentAmount), 0) as TotalPaid
                    FROM Reservation r
                    LEFT JOIN Payment_History ph ON r.ID = ph.Reservation_ID AND ph.Status = 'COMPLETED'
                    WHERE r.ID = @reservationId
                    GROUP BY r.TotalPrice",
                    balanceParams);

                double remainingBalance = 0;

                if (balanceResult.Rows.Count > 0)
                {
                    double totalPrice = Convert.ToDouble(balanceResult.Rows[0]["TotalPrice"]);
                    double totalPaid = Convert.ToDouble(balanceResult.Rows[0]["TotalPaid"]);
                    remainingBalance = totalPrice - totalPaid - totalAmount; // After this payment
                }

                var paymentHistoryParams = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@reservationId", reservationId },
                    { "@docDate", docDate.ToString("yyyy-MM-dd") },
                    { "@totalAmount", totalAmount },
                    { "@paidType", paidType },
                    { "@receiptId", receiptId },
                    { "@remainingBalance", remainingBalance }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO [dbo].[Payment_History] (
                        Reservation_ID, PaymentDate, PaymentAmount, PaymentType, PaymentMethod,
                        Receipt_ID, RemainingBalance, Status
                    )
                    VALUES (
                        @reservationId, @docDate, @totalAmount, 'DEPOSIT', @paidType,
                        @receiptId, @remainingBalance, 'COMPLETED'
                    )",
                    paymentHistoryParams);

                System.Diagnostics.Trace.TraceInformation($"✅ Created Payment_History for Deposit Receipt {receiptId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"⚠️ Failed to create Payment_History for Receipt {receiptId}: {ex.Message}");
                // Continue - Receipt is already created, Payment_History is supplementary
            }
        }

        private void CreateRegularReceipt(string receiptId, string reservationId, DateTime docDate,
                                        double totalAmount, double vat, double priceExcludeVat,
                                        string paidType, string createdById, DataTable dtReserve,
                                        bool etax, string customerId)
        {
            string uid = Guid.NewGuid().ToString("N"); // Generate unique identifier for receipt

            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@receiptId", receiptId },
                { "@uid", uid },
                { "@reservationId", reservationId },
                { "@docDate", docDate.ToString("yyyy-MM-dd") },
                { "@totalAmount", totalAmount },
                { "@vat", vat },
                { "@priceExcludeVat", priceExcludeVat },
                { "@paidType", paidType },
                { "@createdById", createdById },
                { "@etax", etax },
                { "@customerId", customerId }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO [dbo].[Account_Receipt]
                    (ID, UID, [Reservation_ID], [Created_Date], [Total_Amount], [Vat],
                     [Total_Amount_Exclude_Vat], [IsDeposit], [UseDeposit], Status,
                     Paid_Type, Created_By_ID, Etax, Customer_ID)
                    VALUES (@receiptId, @uid, @reservationId, @docDate,
                    @totalAmount, @vat, @priceExcludeVat, 'False', 'False', 'Normal',
                    @paidType, @createdById, @etax, @customerId)",
                parameters);

            // Insert receipt details
            for (int i = 0; i < dtReserve.Rows.Count; i++)
            {
                double pricePerPiece = Convert.ToDouble(dtReserve.Rows[i]["Price_PerPeice"]);
                double productAmount = Convert.ToDouble(dtReserve.Rows[i]["Product_Amount"]);
                double calculatedAmount = CalculateTwoDecimalPoints(pricePerPiece * productAmount);

                var detailParameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@number", dtReserve.Rows[i]["Number"] },
                    { "@receiptId", receiptId },
                    { "@productTypeId", dtReserve.Rows[i]["ProductType_ID"] },
                    { "@productId", dtReserve.Rows[i]["Product_ID"] },
                    { "@productData", dtReserve.Rows[i]["Product_Data"] },
                    { "@productAmount", dtReserve.Rows[i]["Product_Amount"] },
                    { "@productUnit", dtReserve.Rows[i]["Product_Unit"] },
                    { "@pricePerPiece", pricePerPiece },
                    { "@calculatedAmount", calculatedAmount }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO [dbo].[Account_Receipt_Detail]
                      ([Number], [Receipt_ID], [ProductType_ID], [Product_ID],
                       [Product_Data], [Product_Amount], [Product_Unit],
                       [Price_PerPeice], [Price_Amount])
                      VALUES (@number, @receiptId, @productTypeId, @productId,
                      @productData, @productAmount, @productUnit,
                      @pricePerPiece, @calculatedAmount)",
                    detailParameters);
            }

            // 🔧 FIX: Create Payment_History record
            try
            {
                // Get remaining balance
                var balanceParams = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };

                var balanceResult = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        r.TotalPrice,
                        ISNULL(SUM(ph.PaymentAmount), 0) as TotalPaid
                    FROM Reservation r
                    LEFT JOIN Payment_History ph ON r.ID = ph.Reservation_ID AND ph.Status = 'COMPLETED'
                    WHERE r.ID = @reservationId
                    GROUP BY r.TotalPrice",
                    balanceParams);

                double remainingBalance = 0;

                if (balanceResult.Rows.Count > 0)
                {
                    double totalPrice = Convert.ToDouble(balanceResult.Rows[0]["TotalPrice"]);
                    double totalPaid = Convert.ToDouble(balanceResult.Rows[0]["TotalPaid"]);
                    remainingBalance = totalPrice - totalPaid - totalAmount; // After this payment
                }

                var paymentHistoryParams = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@reservationId", reservationId },
                    { "@docDate", docDate.ToString("yyyy-MM-dd") },
                    { "@totalAmount", totalAmount },
                    { "@paidType", paidType },
                    { "@receiptId", receiptId },
                    { "@remainingBalance", remainingBalance }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO [dbo].[Payment_History] (
                        Reservation_ID, PaymentDate, PaymentAmount, PaymentType, PaymentMethod,
                        Receipt_ID, RemainingBalance, Status
                    )
                    VALUES (
                        @reservationId, @docDate, @totalAmount, 'PAYMENT', @paidType,
                        @receiptId, @remainingBalance, 'COMPLETED'
                    )",
                    paymentHistoryParams);

                System.Diagnostics.Trace.TraceInformation($"✅ Created Payment_History for Regular Receipt {receiptId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"⚠️ Failed to create Payment_History for Receipt {receiptId}: {ex.Message}");
                // Continue - Receipt is already created, Payment_History is supplementary
            }
        }

        public void SendReceiptEmail(string toEmail, string receiptId, DateTime docDate, string pdfFilePath)
        {
            try
            {
                string docCreateThaiDate = docDate.ToString("ddMM") + (docDate.Year + 543).ToString();
                string subject = $"[{docCreateThaiDate}][INV][{receiptId}]";
                string body = @"เรียน ลูกค้าผู้มีอุปการะคุณ <br /><br />
                          หจก.แอม แฮปปี้เนส (Take Time) ได้แนบใบกำกับภาษี/ใบเสร็จรับเงินมาพร้อมกับอีเมล์ฉบับนี้ 
                          ท่านสามารถเปิดดูได้โดยคลิกไฟล์แนบ (PDF File)<br />
                          ขอแสดงความนับถือ<br />
                          หจก.แอม แฮปปี้เนส (Take Time)";

                byte[] bytes = System.IO.File.ReadAllBytes(pdfFilePath);
                var memoryStream = new System.IO.MemoryStream(bytes);
                var attachment = new System.Net.Mail.Attachment(memoryStream, $"{receiptId}_etax.pdf");

                _emailService.SendEmail(toEmail, subject, body, new System.Net.Mail.Attachment[] { attachment });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error sending receipt email for {receiptId}: {ex.Message}");
                throw;
            }
        }

        public void CreateDepositReceiptSimple(string reservationId, double totalAmount, DateTime docDate,
                                         string paidType, string createdById, bool etax, string customerId = "0")
        {
            string receiptId = CreateReceiptDocumentNumber(docDate);
            DataTable dtUseVat = _dbHelper.ExecuteQuery("SELECT Use_Vat FROM Business_Info");

            double priceExcludeVat = totalAmount;
            double vat = 0;

            if (dtUseVat.Rows.Count > 0 && dtUseVat.Rows[0]["Use_Vat"].ToString() == "True")
            {
                priceExcludeVat = (totalAmount * 100) / 107;
                vat = totalAmount - priceExcludeVat;
                priceExcludeVat = CalculateTwoDecimalPoints(priceExcludeVat);
                vat = CalculateTwoDecimalPoints(vat);
            }

            CreateDepositReceipt(receiptId, reservationId, docDate, totalAmount, vat, priceExcludeVat,
                               paidType, createdById, etax, customerId);
        }


        private void GenerateReceiptPdf(string receiptId, DateTime docDate)
        {
            try
            {
                string year = docDate.Year.ToString();
                string month = docDate.Month.ToString("00");
                string directoryPath = Path.Combine(_receiptFolderPath, year, month);

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Get receipt data for report
                DataTable dtReceipt = GetReceiptByUid(receiptId);
                DataTable dtReceiptDetail = GetReceiptDetails(receiptId);
                DataTable dtBusinessInfo = GetBusinessInfo();
                DataTable dtCustomer = GetCustomerForReceipt(dtReceipt.Rows[0]["Customer_MobilePhone"].ToString());
                DataTable dtSignature = GetSignatureData();

                // Generate PDF using Reporting Services (similar to original code)
                // This would need to be implemented based on your specific reporting requirements
                GeneratePdfReport(receiptId, dtReceipt, dtReceiptDetail, dtBusinessInfo, dtCustomer, dtSignature, directoryPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error generating PDF for receipt {receiptId}: {ex.Message}");
                throw;
            }
        }

        private void GenerateETaxDocument(string receiptId, DateTime docDate)
        {
            try
            {
                // Implementation for e-Tax document generation
                // This would mirror the XML generation and PDF/A-3 creation from the original code
                GenerateETaxXml(receiptId, docDate);
                CreatePDFA3Invoice(receiptId, docDate);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error generating e-Tax document for receipt {receiptId}: {ex.Message}");
                throw;
            }
        }

        // Helper methods for data retrieval
        private DataTable GetBusinessInfo()
        {
            return _dbHelper.ExecuteQuery(@"
                SELECT
                    Business_Info.*,
                    Customer_Type.Customer_Type,
                    Customer_Type.Customer_Code,
                    Address.Province AS Province,
                    Address.District AS District,
                    Address.SubDistrict AS SubDistrict,
                    Address.PostalCode AS PostalCode,
                    Business_Info.Address_ID,
                    Address.Address_Code
                FROM Business_Info
                LEFT JOIN Customer_Type ON Business_Type_ID = Customer_Type.ID
                LEFT JOIN Address ON Address.ID = Business_Info.Address_ID");
        }

        private DataTable GetCustomerForReceipt(string mobilePhone)
        {
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@mobilePhone", mobilePhone }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    Customer.*,
                    Customer_Type.Customer_Type,
                    Customer_Type.Customer_Code,
                    Address.Province AS Province,
                    Address.District AS District,
                    Address.SubDistrict AS SubDistrict,
                    Address.PostalCode AS PostalCode,
                    Customer.Address_ID,
                    Address.Address_Code
                FROM Customer
                LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID
                LEFT JOIN Address ON Address.ID = Customer.Address_ID
                WHERE MobilePhone = @mobilePhone",
                parameters);
        }

        private DataTable GetSignatureData()
        {
            // Use SignatureService for centralized signature management
            SignatureService signatureService = new SignatureService();
            // For service-based calls, use CEO signature for both approver and creator
            return signatureService.GetSignatureDataWithCEO(0);
        }

        private void GeneratePdfReport(string receiptId, DataTable dtReceipt, DataTable dtReceiptDetail,
                             DataTable dtBusinessInfo, DataTable dtCustomer, DataTable dtSignature,
                             string outputPath)
        {
            try
            {
                string uid = dtReceipt.Rows[0]["UID"].ToString();
                string pdfPath = Path.Combine(outputPath, $"{receiptId}_{uid}.pdf");

                // Create report viewer
                ReportViewer reportViewer = new ReportViewer();
                reportViewer.ProcessingMode = ProcessingMode.Local;

                // Load the report definition
                reportViewer.LocalReport.ReportPath = HttpContext.Current.Server.MapPath("~/Account/Report/ReceiptReport.rdlc");

                // Prepare data sources
                DataTable dtBusinessInfoReport = PrepareBusinessInfoData(dtBusinessInfo);
                DataTable dtCustomerReport = PrepareCustomerData(dtCustomer, dtReceipt);
                DataTable dtReceiptDetailReport = PrepareReceiptDetailData(dtReceiptDetail);
                DataTable dtReceiptReport = PrepareReceiptData(dtReceipt);
                DataTable dtSignatureReport = PrepareSignatureData(dtSignature);

                // Add data sources to report
                reportViewer.LocalReport.DataSources.Add(new ReportDataSource("DataSet1", dtBusinessInfoReport));
                reportViewer.LocalReport.DataSources.Add(new ReportDataSource("DataSet2", dtCustomerReport));
                reportViewer.LocalReport.DataSources.Add(new ReportDataSource("DataSet3", dtReceiptDetailReport));
                reportViewer.LocalReport.DataSources.Add(new ReportDataSource("DataSet4", dtReceiptReport));
                reportViewer.LocalReport.DataSources.Add(new ReportDataSource("DataSet5", dtSignatureReport));

                // Set report parameters if needed
                // ReportParameter[] parameters = new ReportParameter[1];
                // parameters[0] = new ReportParameter("Parameter1", "Value1");
                // reportViewer.LocalReport.SetParameters(parameters);

                // Render PDF
                Warning[] warnings;
                string[] streamIds;
                string mimeType;
                string encoding;
                string filenameExtension;

                byte[] bytes = reportViewer.LocalReport.Render(
                    "PDF",
                    null,
                    out mimeType,
                    out encoding,
                    out filenameExtension,
                    out streamIds,
                    out warnings);

                // Save PDF to file
                using (FileStream fs = new FileStream(pdfPath, FileMode.Create))
                {
                    fs.Write(bytes, 0, bytes.Length);
                }

                System.Diagnostics.Trace.TraceInformation($"PDF generated successfully: {pdfPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error generating PDF report for receipt {receiptId}: {ex.Message}");
                throw;
            }
        }

        private DataTable PrepareBusinessInfoData(DataTable dtBusinessInfo)
        {
            DataTable dtBusinessInfoReport = dtBusinessInfo.Copy();

            try
            {
                if (dtBusinessInfoReport.Rows.Count > 0)
                {
                    // Check if Address_ID exists - if not, use Address field as-is (complete address already stored)
                    if (dtBusinessInfoReport.Rows[0]["Address_ID"] == DBNull.Value ||
                        string.IsNullOrEmpty(dtBusinessInfoReport.Rows[0]["Address_ID"].ToString()))
                    {
                        // No Address_ID - address is complete in Address field, no need to modify
                        // Keep the original Address value
                    }
                    else
                    {
                        // Has Address_ID - build address from structured fields with appropriate prefixes
                        string address = dtBusinessInfoReport.Rows[0]["Address"].ToString();
                        string address1 = dtBusinessInfoReport.Rows[0]["Address1"].ToString();
                        string subDistrict = dtBusinessInfoReport.Rows[0]["SubDistrict"].ToString();
                        string district = dtBusinessInfoReport.Rows[0]["District"].ToString();
                        string province = dtBusinessInfoReport.Rows[0]["Province"].ToString();
                        string postalCode = dtBusinessInfoReport.Rows[0]["PostalCode"].ToString();

                        if (province.Contains("กรุงเทพ"))
                        {
                            dtBusinessInfoReport.Rows[0]["Address"] = $"{address} {address1} แขวง {subDistrict} เขต {district} {province} {postalCode}";
                        }
                        else
                        {
                            dtBusinessInfoReport.Rows[0]["Address"] = $"{address} {address1} ต.{subDistrict} อ.{district} จ.{province} {postalCode}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"Error preparing business info data: {ex.Message}");
            }

            return dtBusinessInfoReport;
        }

        private DataTable PrepareCustomerData(DataTable dtCustomer, DataTable dtReceipt)
        {
            DataTable dtCustomerReport = dtCustomer.Copy();

            try
            {
                if (dtCustomerReport.Rows.Count > 0)
                {
                    // Check if customer wants no receipt
                    bool noNameInReceipt = Convert.ToBoolean(dtReceipt.Rows[0]["NoNameinReceipt"]);

                    if (noNameInReceipt)
                    {
                        dtCustomerReport.Rows[0]["FullName"] = "ประสงค์ไม่รับใบกำกับภาษี";
                        dtCustomerReport.Rows[0]["Address"] = "";
                        dtCustomerReport.Rows[0]["IDNumber"] = "";
                        dtCustomerReport.Rows[0]["MobilePhone"] = "";
                        dtCustomerReport.Rows[0]["Email"] = "";
                    }
                    else
                    {
                        // Format customer name based on customer type
                        string customerTypeId = dtCustomerReport.Rows[0]["Customer_Type_ID"].ToString();
                        string branchNumber = dtCustomerReport.Rows[0]["Branch_Number"].ToString();
                        string fullName = dtCustomerReport.Rows[0]["FullName"].ToString();

                        if (customerTypeId == "1" && branchNumber != "00000")
                        {
                            dtCustomerReport.Rows[0]["FullName"] = $"{fullName} สาขาที่ {branchNumber}";
                        }

                        // Format address - check if Address_ID exists first
                        if (dtCustomerReport.Rows[0]["Address_ID"] == DBNull.Value ||
                            string.IsNullOrEmpty(dtCustomerReport.Rows[0]["Address_ID"].ToString()))
                        {
                            // No Address_ID - address is complete in Address field, no need to modify
                            // Keep the original Address value
                        }
                        else
                        {
                            // Has Address_ID - build address from structured fields with appropriate prefixes
                            string address = dtCustomerReport.Rows[0]["Address"].ToString();
                            string address1 = dtCustomerReport.Rows[0]["Address1"].ToString();
                            string subDistrict = dtCustomerReport.Rows[0]["SubDistrict"].ToString();
                            string district = dtCustomerReport.Rows[0]["District"].ToString();
                            string province = dtCustomerReport.Rows[0]["Province"].ToString();
                            string postalCode = dtCustomerReport.Rows[0]["PostalCode"].ToString();

                            if (province.Contains("กรุงเทพ"))
                            {
                                dtCustomerReport.Rows[0]["Address"] = $"{address} {address1} แขวง {subDistrict} เขต {district} {province} {postalCode}";
                            }
                            else
                            {
                                dtCustomerReport.Rows[0]["Address"] = $"{address} {address1} ต.{subDistrict} อ.{district} จ.{province} {postalCode}";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"Error preparing customer data: {ex.Message}");
            }

            return dtCustomerReport;
        }

        private DataTable PrepareReceiptDetailData(DataTable dtReceiptDetail)
        {
            // Just return the original data table as it's already properly formatted
            return dtReceiptDetail;
        }

        private DataTable PrepareReceiptData(DataTable dtReceipt)
        {
            // Just return the original data table
            return dtReceipt;
        }

        private DataTable PrepareSignatureData(DataTable dtSignature)
        {
            // Return the signature data as is
            return dtSignature;
        }

        private void GenerateETaxXml(string receiptId, DateTime docDate)
        {
            try
            {
                string uid = GetReceiptUid(receiptId);
                string year = docDate.Year.ToString();
                string month = docDate.Month.ToString();
                string xmlFilePath = Path.Combine(_receiptFolderPath, year, month, $"{receiptId}_{uid}.xml");

                // Get required data for XML
                DataTable dtReceipt = GetReceiptByUid(receiptId);
                DataTable dtBusinessInfo = GetBusinessInfo();
                DataTable dtCustomer = GetCustomerForReceipt(dtReceipt.Rows[0]["Customer_MobilePhone"].ToString());

                if (dtReceipt.Rows.Count == 0 || dtBusinessInfo.Rows.Count == 0 || dtCustomer.Rows.Count == 0)
                {
                    throw new Exception("Required data for e-Tax XML generation is missing");
                }

                // Read XML template
                string templatePath = Path.Combine(_baseFolderPath, "Resources", "template.xml");
                if (!File.Exists(templatePath))
                {
                    throw new FileNotFoundException($"e-Tax template not found: {templatePath}");
                }

                string xmlString = File.ReadAllText(templatePath);

                // Replace placeholders with actual data
                xmlString = ReplaceXmlPlaceholders(xmlString, receiptId, dtReceipt, dtBusinessInfo, dtCustomer, docDate);

                // Save XML file
                File.WriteAllText(xmlFilePath, xmlString, System.Text.Encoding.UTF8);

                System.Diagnostics.Trace.TraceInformation($"e-Tax XML generated successfully: {xmlFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error generating e-Tax XML for receipt {receiptId}: {ex.Message}");
                throw;
            }
        }

        private string ReplaceXmlPlaceholders(string xmlString, string receiptId, DataTable dtReceipt,
                                            DataTable dtBusinessInfo, DataTable dtCustomer, DateTime docDate)
        {
            try
            {
                DataRow receiptRow = dtReceipt.Rows[0];
                DataRow businessRow = dtBusinessInfo.Rows[0];
                DataRow customerRow = dtCustomer.Rows[0];

                // Basic receipt information
                xmlString = xmlString.Replace("*invoice_id", receiptId);
                xmlString = xmlString.Replace("*invoice_name", "ใบเสร็จรับเงิน/ใบกำกับภาษี");
                xmlString = xmlString.Replace("*invoice_typecode", "T03");
                xmlString = xmlString.Replace("*invoice_issue_date", docDate.ToString("yyyy-MM-dd") + "T00:00:00.000");
                xmlString = xmlString.Replace("*invoice_create_date", docDate.ToString("yyyy-MM-dd") + "T00:00:00.000");

                // Seller information (Business)
                string sellerType = businessRow["Customer_Code"].ToString();
                string sellerTaxId = businessRow["LegalEntity_Number"].ToString();

                if (sellerType == "TXID")
                {
                    sellerTaxId += businessRow["Branch_Number"].ToString();
                }

                xmlString = xmlString.Replace("*seller_type", sellerType);
                xmlString = xmlString.Replace("*seller_taxid", sellerTaxId);
                xmlString = xmlString.Replace("*seller_name", businessRow["Company_Name"].ToString());
                xmlString = xmlString.Replace("*seller_DefinedCITradeContact", businessRow["Email"].ToString());
                xmlString = xmlString.Replace("*seller_PhoneNumber", businessRow["Phone_Number"].ToString());
                xmlString = xmlString.Replace("*seller_zipcode", businessRow["PostalCode"].ToString());

                // Seller address
                string sellerAddress = FormatAddress(businessRow, true);
                xmlString = xmlString.Replace("*seller_address1", sellerAddress);
                xmlString = xmlString.Replace("*seller_cityname", GetAddressCode(businessRow["Address_Code"].ToString(), 4));
                xmlString = xmlString.Replace("*seller_city_subdivision_name", GetAddressCode(businessRow["Address_Code"].ToString(), 6));
                xmlString = xmlString.Replace("*seller_country", "TH");
                xmlString = xmlString.Replace("*sellercountry_subdivision_id", GetAddressCode(businessRow["Address_Code"].ToString(), 2));

                // Buyer information (Customer)
                string buyerType = customerRow["Customer_Code"].ToString();
                string buyerTaxId = customerRow["IDNumber"].ToString();

                if (buyerType == "TXID")
                {
                    string branchNumber = customerRow["Branch_Number"].ToString();
                    buyerTaxId += (branchNumber.Length == 5 ? branchNumber : "00000");
                }
                else
                {
                    buyerType = "NIDN"; // Default to National ID
                }

                xmlString = xmlString.Replace("*buyer_taxtype", buyerType);
                xmlString = xmlString.Replace("*buyer_taxid", buyerTaxId);
                xmlString = xmlString.Replace("*buyer_name", customerRow["FullName"].ToString());
                xmlString = xmlString.Replace("*buyer_DefinedCITradeContact", customerRow["Email"].ToString());
                xmlString = xmlString.Replace("*buyer_zipcode", customerRow["PostalCode"].ToString());

                // Buyer address
                string buyerAddress = FormatAddress(customerRow, false);
                xmlString = xmlString.Replace("*buyer_address", buyerAddress);
                xmlString = xmlString.Replace("*buyer_cityname", GetAddressCode(customerRow["Address_Code"].ToString(), 4));
                xmlString = xmlString.Replace("*buyer_city_subdivision_name", GetAddressCode(customerRow["Address_Code"].ToString(), 6));
                xmlString = xmlString.Replace("*buyer_country", "TH");
                xmlString = xmlString.Replace("*buyercountry_subdivision_id", GetAddressCode(customerRow["Address_Code"].ToString(), 2));

                // Amount information
                xmlString = xmlString.Replace("*currency", "THB");
                xmlString = xmlString.Replace("*invoice_tax_code", "VAT");
                xmlString = xmlString.Replace("*invoice_tax_rate", "7");
                xmlString = xmlString.Replace("*invoice_basis_amount", receiptRow["Total_Amount_Exclude_Vat"].ToString());
                xmlString = xmlString.Replace("*calculated_amount", receiptRow["Vat"].ToString());
                xmlString = xmlString.Replace("*invoice_line_total", receiptRow["Total_Amount_Exclude_Vat"].ToString());
                xmlString = xmlString.Replace("*tax_basis_total_amount", receiptRow["Total_Amount_Exclude_Vat"].ToString());
                xmlString = xmlString.Replace("*invoice_tax_total", receiptRow["Vat"].ToString());
                xmlString = xmlString.Replace("*invoice_grand_total", receiptRow["Total_Amount"].ToString());

                // Default values for unused fields
                xmlString = xmlString.Replace("*invoice_purpose", "");
                xmlString = xmlString.Replace("*invoice_Purpose_code", "");
                xmlString = xmlString.Replace("*invoice_remark", "");
                xmlString = xmlString.Replace("*seller_address2", "");
                xmlString = xmlString.Replace("*seller_building_name", businessRow["Address"].ToString());
                xmlString = xmlString.Replace("*buyer_address2", "");
                xmlString = xmlString.Replace("*buyer_building_name", customerRow["Address"].ToString());
                xmlString = xmlString.Replace("*reference", "");
                xmlString = xmlString.Replace("*buyer_contact_person", "");
                xmlString = xmlString.Replace("*invoice_discountallowance", "");
                xmlString = xmlString.Replace("*invoice_serviceallowance", "");
                xmlString = xmlString.Replace("*item", "ค่าที่พักหรือค่ามัดจำที่พัก");
                xmlString = xmlString.Replace("*invoice_billedquantity", "1");

                return xmlString;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error replacing XML placeholders for receipt {receiptId}: {ex.Message}");
                throw;
            }
        }

        private string FormatAddress(DataRow dataRow, bool isBusiness)
        {
            try
            {
                string address = dataRow["Address"].ToString();
                string address1 = dataRow["Address1"].ToString();
                string subDistrict = dataRow["SubDistrict"].ToString();
                string district = dataRow["District"].ToString();
                string province = dataRow["Province"].ToString();
                string postalCode = dataRow["PostalCode"].ToString();

                if (province.Contains("กรุงเทพ"))
                {
                    return $"{address} {address1} แขวง {subDistrict} เขต {district} {province} {postalCode}";
                }
                else
                {
                    return $"{address} {address1} ต.{subDistrict} อ.{district} จ.{province} {postalCode}";
                }
            }
            catch
            {
                return dataRow["Address"].ToString();
            }
        }

        private string GetAddressCode(string addressCode, int length)
        {
            if (string.IsNullOrEmpty(addressCode) || addressCode.Length < length)
            {
                return "000000"; // Default value
            }
            return addressCode.Substring(0, length);
        }

        private string GetReceiptUid(string receiptId)
        {
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@receiptId", receiptId }
            };

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                "SELECT UID FROM Account_Receipt WHERE ID = @receiptId",
                parameters);

            if (dt.Rows.Count > 0)
            {
                return dt.Rows[0]["UID"].ToString();
            }
            return string.Empty;
        }

        private void CreatePDFA3Invoice(string receiptId, DateTime docDate)
        {
            try
            {
                string uid = GetReceiptUid(receiptId);
                string year = docDate.Year.ToString();
                string month = docDate.Month.ToString();

                string pdfFilePath = Path.Combine(_receiptFolderPath, year, month, $"{receiptId}_{uid}.pdf");
                string xmlFilePath = Path.Combine(_receiptFolderPath, year, month, $"{receiptId}_{uid}.xml");
                string outputPath = Path.Combine(_receiptFolderPath, year, month, $"{receiptId}_{uid}_etax.pdf");

                if (!File.Exists(pdfFilePath) || !File.Exists(xmlFilePath))
                {
                    throw new FileNotFoundException($"Required files not found for PDF/A-3 creation: {pdfFilePath}, {xmlFilePath}");
                }

                // Use ECertificateAPI to create PDF/A-3 invoice
                PDFA3Invoice pdf = new PDFA3Invoice();
                string xmlFileName = "ETDA-invoice.xml";
                string xmlVersion = "1.0";
                string documentOID = "";

                pdf.CreatePDFA3Invoice(pdfFilePath, xmlFilePath, xmlFileName, xmlVersion, receiptId, documentOID, outputPath, "Tax Invoice");

                // Send email if e-Tax is enabled
                SendETaxEmail(receiptId, docDate, outputPath);

                System.Diagnostics.Trace.TraceInformation($"PDF/A-3 invoice created successfully: {outputPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error creating PDF/A-3 invoice for receipt {receiptId}: {ex.Message}");
                throw;
            }
        }

        private void SendETaxEmail(string receiptId, DateTime docDate, string pdfFilePath)
        {
            try
            {
                // Get customer email
                DataTable dtReceipt = GetReceiptByUid(receiptId);
                if (dtReceipt.Rows.Count == 0) return;

                string customerMobile = dtReceipt.Rows[0]["Customer_MobilePhone"].ToString();
                DataTable dtCustomer = GetCustomerForReceipt(customerMobile);

                if (dtCustomer.Rows.Count == 0) return;

                string customerEmail = dtCustomer.Rows[0]["Email"].ToString();
                if (string.IsNullOrEmpty(customerEmail) || !customerEmail.Contains("@")) return;

                // Prepare email content
                string thaiDate = FormatThaiDate(docDate);
                string subject = $"[{thaiDate}][INV][{receiptId}]";
                string body = @"เรียน ลูกค้าผู้มีอุปการะคุณ <br /><br />
                      หจก.แอม แฮปปี้เนส (Take Time) ได้แนบใบกำกับภาษี/ใบเสร็จรับเงินมาพร้อมกับอีเมล์ฉบับนี้ 
                      ท่านสามารถเปิดดูได้โดยคลิกไฟล์แนบ (PDF File)<br />
                      ขอแสดงความนับถือ<br />
                      หจก.แอม แฮปปี้เนส (Take Time)";

                // Create attachment
                byte[] pdfBytes = File.ReadAllBytes(pdfFilePath);
                using (var memoryStream = new System.IO.MemoryStream(pdfBytes))
                {
                    var attachment = new System.Net.Mail.Attachment(memoryStream, $"{receiptId}_etax.pdf");

                    // Send email using centralized EmailService
                    _emailService.SendEmail(customerEmail, subject, body, new System.Net.Mail.Attachment[] { attachment });
                }

                System.Diagnostics.Trace.TraceInformation($"e-Tax email sent successfully to {customerEmail}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error sending e-Tax email for receipt {receiptId}: {ex.Message}");
                // Don't throw - email failure shouldn't break the main process
            }
        }

        private string FormatThaiDate(DateTime date)
        {
            string day = date.Day.ToString("00");
            string month = date.Month.ToString("00");
            int year = date.Year + 543; // Convert to Buddhist year

            return $"{day}{month}{year}";
        }
    }
}