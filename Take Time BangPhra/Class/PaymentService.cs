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

                // If rejected, send notification to customer
                if (!approved && !string.IsNullOrEmpty(rejectionReason))
                {
                    // TODO: Send rejection notification
                }

                return true;
            }
            catch
            {
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
            var reservation = _code.DatabaseQuerySafe(_connectionString,
                $"SELECT * FROM Reservation WHERE ID = {reservationId}",
                null);

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

            var result = _code.DatabaseQuerySafe(_connectionString,
                $"SELECT MAX(ID) as LastID FROM Account_Receipt WHERE ID LIKE '{datePrefix}%'",
                null);

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
            // TODO: Implement email sending
            // Use EmailService class if available
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
