using System;
using System.Collections.Generic;
using System.Data;
using System.Web;
using System.IO;
using System.Configuration;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// Payment Service - Business logic for payment processing
    /// </summary>
    public class PaymentService
    {
        private readonly string _connectionString;
        private readonly PaymentDataAccess _paymentDA;
        private readonly ReservationDataAccess _reservationDA;
        private readonly code _code;
        public PaymentService(string connectionString)
        {
            _connectionString = connectionString;
            _paymentDA = new PaymentDataAccess(connectionString);
            _reservationDA = new ReservationDataAccess(connectionString);
            _code = new code();
        }

        #region Payment Processing

        /// <summary>
        /// Process additional payment for a reservation
        /// </summary>
        public PaymentResult ProcessAdditionalPayment(
            int reservationId,
            decimal amount,
            string paymentMethod,
            HttpPostedFile slipFile = null,
            int? adminId = null,
            string customerPhone = null,
            string notes = null)
        {
            try
            {
                // 1. Validate reservation
                var reservation = _reservationDA.GetReservationByIdAndPhone(reservationId, customerPhone ?? "");
                if (reservation == null || reservation.Rows.Count == 0)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "ไม่พบการจองนี้"
                    };
                }

                // 2. Calculate remaining balance
                decimal remaining = _paymentDA.GetRemainingBalance(reservationId);
                if (amount > remaining + 0.01m) // Allow small rounding
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = $"จำนวนเงินเกินยอดค้างชำระ (ค้าง: {remaining:N2} บาท)"
                    };
                }

                if (amount <= 0)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "จำนวนเงินต้องมากกว่า 0"
                    };
                }

                // 3. Upload slip (if provided)
                long? slipId = null;
                if (slipFile != null && slipFile.ContentLength > 0)
                {
                    slipId = UploadPaymentSlip(reservationId, slipFile, customerPhone, adminId);
                }

                // 4. Create receipt
                string receiptId = CreatePaymentReceipt(
                    reservationId,
                    amount,
                    paymentMethod,
                    isDeposit: false
                );

                // 5. Record payment
                long paymentId = _paymentDA.RecordPaymentDirect(
                    reservationId,
                    amount,
                    "ADDITIONAL", // Payment type
                    paymentMethod,
                    receiptId,
                    slipId,
                    customerPhone,
                    adminId,
                    notes
                );

                // 6. Update reservation deposit amount
                decimal newDepositAmount = _paymentDA.GetTotalPaidAmount(reservationId);
                UpdateReservationDeposit(reservationId, newDepositAmount);

                // 7. Send email confirmation
                try
                {
                    SendPaymentConfirmationEmail(reservationId, receiptId, amount);
                }
                catch (Exception emailEx)
                {
                    // Log but don't fail the payment
                    _code.Logs(_connectionString, "Email Error", emailEx.Message, "SYSTEM");
                }

                return new PaymentResult
                {
                    Success = true,
                    Message = "ชำระเงินสำเร็จ",
                    PaymentId = paymentId,
                    ReceiptId = receiptId,
                    RemainingBalance = remaining - amount
                };
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Payment Error", ex.Message + " - " + ex.StackTrace, "SYSTEM");
                return new PaymentResult
                {
                    Success = false,
                    Message = "เกิดข้อผิดพลาด: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Process initial deposit payment
        /// </summary>
        public PaymentResult ProcessDepositPayment(
            int reservationId,
            decimal depositAmount,
            string paymentMethod,
            HttpPostedFile slipFile,
            string customerPhone,
            int? adminId = null)
        {
            try
            {
                // 1. Validate
                if (depositAmount <= 0)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "จำนวนเงินมัดจำต้องมากกว่า 0"
                    };
                }

                // 2. Upload slip (REQUIRED for deposit)
                if (slipFile == null || slipFile.ContentLength == 0)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "กรุณาแนบสลิปการชำระเงิน"
                    };
                }

                long slipId = UploadPaymentSlip(reservationId, slipFile, customerPhone, adminId);

                // 3. Create deposit receipt
                string receiptId = CreatePaymentReceipt(
                    reservationId,
                    depositAmount,
                    paymentMethod,
                    isDeposit: true
                );

                // 4. Record payment
                long paymentId = _paymentDA.RecordPaymentDirect(
                    reservationId,
                    depositAmount,
                    "DEPOSIT",
                    paymentMethod,
                    receiptId,
                    slipId,
                    customerPhone,
                    adminId,
                    "มัดจำการจอง"
                );

                // 5. Update reservation
                UpdateReservationDeposit(reservationId, depositAmount);
                UpdateReservationStatus(reservationId, "มัดจำแล้ว");

                // 6. Send confirmation
                try
                {
                    SendPaymentConfirmationEmail(reservationId, receiptId, depositAmount);
                }
                catch { }

                return new PaymentResult
                {
                    Success = true,
                    Message = "ชำระเงินมัดจำสำเร็จ",
                    PaymentId = paymentId,
                    ReceiptId = receiptId,
                    RemainingBalance = _paymentDA.GetRemainingBalance(reservationId)
                };
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Deposit Payment Error", ex.Message, "SYSTEM");
                return new PaymentResult
                {
                    Success = false,
                    Message = "เกิดข้อผิดพลาด: " + ex.Message
                };
            }
        }

        #endregion

        #region Payment Slip Management

        /// <summary>
        /// Upload payment slip and return slip ID
        /// </summary>
        private long UploadPaymentSlip(
            int reservationId,
            HttpPostedFile slipFile,
            string customerPhone,
            int? adminId)
        {
            // 1. Validate file
            if (slipFile == null || slipFile.ContentLength == 0)
            {
                throw new Exception("ไฟล์ไม่ถูกต้อง");
            }

            string fileExtension = Path.GetExtension(slipFile.FileName).ToLower();
            if (fileExtension != ".jpg" && fileExtension != ".jpeg" && fileExtension != ".png" && fileExtension != ".pdf")
            {
                throw new Exception("รองรับเฉพาะไฟล์ JPG, PNG, หรือ PDF เท่านั้น");
            }

            if (slipFile.ContentLength > 5 * 1024 * 1024) // 5MB limit
            {
                throw new Exception("ไฟล์ใหญ่เกินไป (สูงสุด 5MB)");
            }

            // 2. Generate unique filename
            string uniqueFileName = $"Slip_{reservationId}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}{fileExtension}";

            // 3. Create directory if not exists
            string uploadPath = HttpContext.Current.Server.MapPath("~/Documents/PaymentSlips/");
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            // 4. Save file
            string fullPath = Path.Combine(uploadPath, uniqueFileName);
            slipFile.SaveAs(fullPath);

            // 5. Insert to database
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@slipFileURL", "~/Documents/PaymentSlips/" + uniqueFileName },
                { "@fileName", slipFile.FileName },
                { "@fileType", slipFile.ContentType },
                { "@fileSize", slipFile.ContentLength },
                { "@uploadedByCustomer", customerPhone },
                { "@uploadedByAdmin", adminId },
                { "@verificationStatus", "PENDING" }
            };

            long slipId = _code.DatabaseInsertReturnSafe(_connectionString,
                @"INSERT INTO Payment_Slips (
                    Reservation_ID, SlipFileURL, FileName, FileType, FileSize,
                    UploadedDate, UploadedBy_CustomerPhone, UploadedBy_ID,
                    VerificationStatus, IsVerified
                  )
                  VALUES (
                    @reservationId, @slipFileURL, @fileName, @fileType, @fileSize,
                    GETDATE(), @uploadedByCustomer, @uploadedByAdmin,
                    @verificationStatus, 0
                  );
                  SELECT SCOPE_IDENTITY();",
                parameters);

            // 6. Process OCR asynchronously (best effort - don't fail if OCR fails)
            try
            {
                ProcessSlipOCR(slipId, fullPath);
            }
            catch (Exception ocrEx)
            {
                // Log OCR error but don't fail the upload
                _code.Logs(_connectionString, "OCR Processing Error",
                    $"SlipID: {slipId}, Error: {ocrEx.Message}", "SYSTEM");
            }

            return slipId;
        }

        /// <summary>
        /// Process OCR for uploaded slip
        /// </summary>
        private void ProcessSlipOCR(long slipId, string imageFilePath)
        {
            try
            {
                // Get tesseract data path from web.config
                string tessDataPath = ConfigurationManager.AppSettings["TesseractDataPath"] ??
                    HttpContext.Current.Server.MapPath("~/tessdata");

                var ocrService = new Take_Time_BangPhra.Services.SlipOCRService(tessDataPath, _connectionString);
                var ocrResult = ocrService.ProcessSlip(imageFilePath);

                // Save OCR result to database
                ocrService.SaveOCRResult(slipId, ocrResult);

                // Log result for monitoring
                string logMessage = ocrResult.Success
                    ? $"OCR Success - Amount: {ocrResult.Amount:N2}, Confidence: {ocrResult.Confidence:N2}%"
                    : $"OCR Failed - {ocrResult.ErrorMessage}";

                _code.Logs(_connectionString, "OCR Processing",
                    $"SlipID: {slipId}, {logMessage}", "SYSTEM");
            }
            catch (Exception ex)
            {
                // Update slip with error status
                var errorParams = new Dictionary<string, object>
                {
                    { "@slipId", slipId },
                    { "@errorMessage", ex.Message },
                    { "@status", "FAILED" }
                };

                // Note: Payment_Slips table does not have OCR columns
                // Just log the error, don't update non-existent columns
                _code.Logs(_connectionString, "OCR Processing Failed",
                    $"SlipID: {slipId}, Error: {ex.Message}", "SYSTEM");

                throw;
            }
        }

        /// <summary>
        /// Verify payment slip
        /// </summary>
        public bool VerifyPaymentSlip(long slipId, int adminId, bool approved, string rejectionReason = null)
        {
            try
            {
                string status = approved ? "APPROVED" : "REJECTED";
                _paymentDA.UpdateSlipVerification(slipId, status, adminId, rejectionReason);

                // Get reservation ID from slip
                var parameters = new Dictionary<string, object>
                {
                    { "@slipId", slipId }
                };

                var slipData = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Reservation_ID FROM Payment_Slips WHERE ID = @slipId",
                    parameters);

                if (slipData.Rows.Count > 0)
                {
                    int reservationId = Convert.ToInt32(slipData.Rows[0]["Reservation_ID"]);

                    // If rejected, send notification to customer
                    if (!approved && !string.IsNullOrEmpty(rejectionReason))
                    {
                        SendPaymentRejectionEmail(reservationId, rejectionReason);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "VerifyPaymentSlip Error", ex.Message, "SYSTEM");
                return false;
            }
        }

        #endregion

        #region Receipt Management

        /// <summary>
        /// Create payment receipt
        /// </summary>
        private string CreatePaymentReceipt(
            int reservationId,
            decimal amount,
            string paymentMethod,
            bool isDeposit)
        {
            // Generate receipt ID
            string receiptId = GenerateReceiptId();

            // Get reservation data
            var reservationParams = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            var reservation = _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM Reservation WHERE ID = @reservationId",
                reservationParams);

            if (reservation.Rows.Count == 0)
            {
                throw new Exception("ไม่พบข้อมูลการจอง");
            }

            // Insert receipt
            var parameters = new Dictionary<string, object>
            {
                { "@receiptId", receiptId },
                { "@reservationId", reservationId },
                { "@createdDate", DateTime.Now },
                { "@totalAmount", amount },
                { "@vat", 0 }, // Calculate if needed
                { "@totalExcludeVat", amount },
                { "@isDeposit", isDeposit },
                { "@useDeposit", false },
                { "@paidType", paymentMethod },
                { "@status", "Normal" },
                { "@etax", false }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO Account_Receipt (
                    ID, Reservation_ID, Created_Date, Total_Amount, Vat,
                    Total_Amount_Exclude_Vat, IsDeposit, UseDeposit, Paid_Type,
                    Status, Etax
                  )
                  VALUES (
                    @receiptId, @reservationId, @createdDate, @totalAmount, @vat,
                    @totalExcludeVat, @isDeposit, @useDeposit, @paidType,
                    @status, @etax
                  )",
                parameters);

            return receiptId;
        }

        /// <summary>
        /// Generate unique receipt ID (format: RECYYMMDDXXX)
        /// </summary>
        private string GenerateReceiptId()
        {
            string datePrefix = "REC" + DateTime.Now.ToString("yyMMdd");

            var parameters = new Dictionary<string, object>
            {
                { "@datePrefix", datePrefix + "%" }
            };

            var result = _code.DatabaseQuerySafe(_connectionString,
                "SELECT MAX(ID) as LastID FROM Account_Receipt WHERE ID LIKE @datePrefix",
                parameters);

            int sequenceNumber = 1;
            if (result.Rows.Count > 0 && result.Rows[0]["LastID"] != DBNull.Value)
            {
                string lastId = result.Rows[0]["LastID"].ToString();
                if (lastId.Length >= 12)
                {
                    string lastSeq = lastId.Substring(9);
                    if (int.TryParse(lastSeq, out int lastNum))
                    {
                        sequenceNumber = lastNum + 1;
                    }
                }
            }

            return datePrefix + sequenceNumber.ToString("D3");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Update reservation deposit amount
        /// </summary>
        private void UpdateReservationDeposit(int reservationId, decimal depositAmount)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@deposit", depositAmount }
            };

            _code.DatabaseInsertSafe(_connectionString,
                "UPDATE Reservation SET Deposit = @deposit WHERE ID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Update reservation status
        /// </summary>
        private void UpdateReservationStatus(int reservationId, string status)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@status", status }
            };

            _code.DatabaseInsertSafe(_connectionString,
                "UPDATE Reservation SET Status = @status WHERE ID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Send payment confirmation email
        /// </summary>
        private void SendPaymentConfirmationEmail(int reservationId, string receiptId, decimal amount)
        {
            try
            {
                // Get customer email from reservation
                var parameters = new Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };

                var customerData = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT c.Email, c.Customer_Name, r.CheckinDate, r.CheckoutDate, r.TotalPrice
                      FROM Reservation r
                      INNER JOIN Customer c ON r.Customer_MobilePhone = c.Customer_MobilePhone
                      WHERE r.ID = @reservationId",
                    parameters);

                if (customerData.Rows.Count == 0 || string.IsNullOrEmpty(customerData.Rows[0]["Email"]?.ToString()))
                {
                    _code.Logs(_connectionString, "Email Skipped", $"No email for reservation {reservationId}", "SYSTEM");
                    return;
                }

                string customerEmail = customerData.Rows[0]["Email"].ToString();
                string customerName = customerData.Rows[0]["Customer_Name"]?.ToString() ?? "ลูกค้า";
                DateTime checkInDate = Convert.ToDateTime(customerData.Rows[0]["CheckinDate"]);
                DateTime checkOutDate = Convert.ToDateTime(customerData.Rows[0]["CheckoutDate"]);
                decimal totalPrice = Convert.ToDecimal(customerData.Rows[0]["TotalPrice"]);
                decimal remaining = totalPrice - amount;

                // Build email content
                string subject = $"[Take Time] ยืนยันการชำระเงิน - ใบเสร็จ #{receiptId}";
                string body = $@"
                    <html>
                    <head>
                        <style>
                            body {{ font-family: 'Sarabun', Arial, sans-serif; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background: #2c3e50; color: white; padding: 20px; text-align: center; }}
                            .content {{ padding: 20px; background: #f9f9f9; }}
                            .amount {{ font-size: 24px; color: #27ae60; font-weight: bold; }}
                            .footer {{ padding: 20px; text-align: center; color: #666; font-size: 12px; }}
                            table {{ width: 100%; border-collapse: collapse; }}
                            td {{ padding: 10px; border-bottom: 1px solid #ddd; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Take Time Bang Phra</h1>
                                <p>ยืนยันการชำระเงิน</p>
                            </div>
                            <div class='content'>
                                <p>เรียน คุณ{customerName},</p>
                                <p>ขอบคุณสำหรับการชำระเงิน ระบบได้บันทึกการชำระเงินของท่านเรียบร้อยแล้ว</p>

                                <table>
                                    <tr><td><strong>เลขที่ใบเสร็จ:</strong></td><td>{receiptId}</td></tr>
                                    <tr><td><strong>เลขที่การจอง:</strong></td><td>{reservationId}</td></tr>
                                    <tr><td><strong>วันเช็คอิน:</strong></td><td>{checkInDate:dd/MM/yyyy}</td></tr>
                                    <tr><td><strong>วันเช็คเอาท์:</strong></td><td>{checkOutDate:dd/MM/yyyy}</td></tr>
                                    <tr><td><strong>จำนวนเงินที่ชำระ:</strong></td><td class='amount'>฿{amount:N2}</td></tr>
                                    <tr><td><strong>ยอดคงเหลือ:</strong></td><td>฿{remaining:N2}</td></tr>
                                </table>

                                <p style='margin-top: 20px;'>หากท่านมีข้อสงสัย กรุณาติดต่อเราได้ทุกช่องทาง</p>
                            </div>
                            <div class='footer'>
                                <p>Take Time Bang Phra</p>
                                <p>โทร: 038-XXX-XXX | อีเมล: taketime.bangphra@gmail.com</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                // Send email using EmailService
                var emailService = new Take_Time_BangPhra.Services.EmailService();
                emailService.SendEmail(customerEmail, subject, body);

                _code.Logs(_connectionString, "Email Sent", $"Payment confirmation sent to {customerEmail} for receipt {receiptId}", "SYSTEM");
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Email Error", $"Failed to send payment confirmation: {ex.Message}", "SYSTEM");
                throw;
            }
        }

        /// <summary>
        /// Send payment rejection notification email
        /// </summary>
        private void SendPaymentRejectionEmail(int reservationId, string rejectionReason)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };

                var customerData = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT c.Email, c.Customer_Name, r.TotalPrice, r.Deposit
                      FROM Reservation r
                      INNER JOIN Customer c ON r.Customer_MobilePhone = c.Customer_MobilePhone
                      WHERE r.ID = @reservationId",
                    parameters);

                if (customerData.Rows.Count == 0 || string.IsNullOrEmpty(customerData.Rows[0]["Email"]?.ToString()))
                {
                    return;
                }

                string customerEmail = customerData.Rows[0]["Email"].ToString();
                string customerName = customerData.Rows[0]["Customer_Name"]?.ToString() ?? "ลูกค้า";

                string subject = $"[Take Time] แจ้งเตือน - สลิปการชำระเงินไม่ผ่านการตรวจสอบ (การจอง #{reservationId})";
                string body = $@"
                    <html>
                    <head>
                        <style>
                            body {{ font-family: 'Sarabun', Arial, sans-serif; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background: #e74c3c; color: white; padding: 20px; text-align: center; }}
                            .content {{ padding: 20px; background: #f9f9f9; }}
                            .reason {{ background: #fff3cd; padding: 15px; border-left: 4px solid #ffc107; margin: 15px 0; }}
                            .footer {{ padding: 20px; text-align: center; color: #666; font-size: 12px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Take Time Bang Phra</h1>
                                <p>แจ้งเตือนการชำระเงิน</p>
                            </div>
                            <div class='content'>
                                <p>เรียน คุณ{customerName},</p>
                                <p>สลิปการชำระเงินของท่านสำหรับการจอง #{reservationId} ไม่ผ่านการตรวจสอบ</p>

                                <div class='reason'>
                                    <strong>เหตุผล:</strong> {rejectionReason}
                                </div>

                                <p>กรุณาอัปโหลดสลิปใหม่หรือติดต่อเจ้าหน้าที่เพื่อดำเนินการต่อ</p>
                                <p>หากท่านมีข้อสงสัย กรุณาติดต่อเราได้ทุกช่องทาง</p>
                            </div>
                            <div class='footer'>
                                <p>Take Time Bang Phra</p>
                                <p>โทร: 038-XXX-XXX | อีเมล: taketime.bangphra@gmail.com</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                var emailService = new Take_Time_BangPhra.Services.EmailService();
                emailService.SendEmail(customerEmail, subject, body);

                _code.Logs(_connectionString, "Email Sent", $"Payment rejection notification sent to {customerEmail}", "SYSTEM");
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Email Error", $"Failed to send rejection notification: {ex.Message}", "SYSTEM");
            }
        }

        #endregion

        #region Public Query Methods

        /// <summary>
        /// Get payment history for display
        /// </summary>
        public DataTable GetPaymentHistory(int reservationId)
        {
            return _paymentDA.GetPaymentHistory(reservationId);
        }

        /// <summary>
        /// Get payment summary
        /// </summary>
        public PaymentSummary GetPaymentSummary(int reservationId)
        {
            var summary = _paymentDA.GetPaymentSummary(reservationId);
            if (summary.Rows.Count > 0)
            {
                var row = summary.Rows[0];
                return new PaymentSummary
                {
                    ReservationId = reservationId,
                    TotalPrice = Convert.ToDecimal(row["TotalPrice"]),
                    TotalPaid = Convert.ToDecimal(row["TotalPaid"]),
                    DepositPaid = Convert.ToDecimal(row["DepositPaid"]),
                    AdditionalPaid = Convert.ToDecimal(row["AdditionalPaid"]),
                    RemainingBalance = Convert.ToDecimal(row["RemainingBalance"]),
                    PaymentStatus = row["PaymentStatus"].ToString()
                };
            }

            return null;
        }

        /// <summary>
        /// Check if can make additional payment
        /// </summary>
        public bool CanMakeAdditionalPayment(int reservationId)
        {
            return _paymentDA.GetRemainingBalance(reservationId) > 0.01m;
        }

        #endregion
    }

    #region Result Classes

    /// <summary>
    /// Payment operation result
    /// </summary>
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public long PaymentId { get; set; }
        public string ReceiptId { get; set; }
        public decimal RemainingBalance { get; set; }
    }

    /// <summary>
    /// Payment summary data
    /// </summary>
    public class PaymentSummary
    {
        public int ReservationId { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal DepositPaid { get; set; }
        public decimal AdditionalPaid { get; set; }
        public decimal RemainingBalance { get; set; }
        public string PaymentStatus { get; set; } // PAID/PARTIAL/UNPAID
    }

    #endregion
}
