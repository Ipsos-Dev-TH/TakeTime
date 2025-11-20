using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// OCR Service for extracting payment amount from slip images
    /// NOTE: This is a placeholder implementation without Tesseract OCR
    /// To enable OCR, install Tesseract NuGet package and implement PerformOCR method
    /// </summary>
    public class SlipOCRService
    {
        private readonly string _tessDataPath;
        private readonly string _connectionString;

        public SlipOCRService(string tessDataPath, string connectionString)
        {
            _tessDataPath = tessDataPath;
            _connectionString = connectionString;
        }

        /// <summary>
        /// Result of OCR processing
        /// </summary>
        public class OCRResult
        {
            public bool Success { get; set; }
            public decimal? Amount { get; set; }
            public double Confidence { get; set; }
            public string RawText { get; set; }
            public string ErrorMessage { get; set; }
            public string Status { get; set; } // SUCCESS, FAILED, MANUAL_REVIEW, SKIPPED

            public OCRResult()
            {
                Success = false;
                Amount = null;
                Confidence = 0;
                RawText = "";
                ErrorMessage = "";
                Status = "SKIPPED";
            }
        }

        /// <summary>
        /// Process slip image and extract payment amount
        /// PLACEHOLDER: Returns SKIPPED status (OCR disabled)
        /// </summary>
        public OCRResult ProcessSlip(string imageFilePath)
        {
            var result = new OCRResult
            {
                Status = "SKIPPED",
                ErrorMessage = "OCR not enabled - Tesseract not installed",
                Success = false
            };

            // TODO: Implement OCR when Tesseract is installed
            // For now, skip OCR processing
            return result;

            /* TESSERACT IMPLEMENTATION (commented out until package installed):

            try
            {
                // Validate file exists
                if (!File.Exists(imageFilePath))
                {
                    result.ErrorMessage = "ไม่พบไฟล์รูปภาพ";
                    result.Status = "FAILED";
                    return result;
                }

                // Perform OCR
                string extractedText = PerformOCR(imageFilePath);
                result.RawText = extractedText;

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    result.ErrorMessage = "ไม่สามารถอ่านข้อความจากรูปภาพได้";
                    result.Status = "FAILED";
                    return result;
                }

                // Extract amount from text
                var amountExtractionResult = ExtractAmountFromText(extractedText);

                if (amountExtractionResult.Amount.HasValue)
                {
                    result.Amount = amountExtractionResult.Amount;
                    result.Confidence = amountExtractionResult.Confidence;
                    result.Success = true;

                    // Determine status based on confidence
                    if (result.Confidence >= 70)
                    {
                        result.Status = "SUCCESS";
                    }
                    else
                    {
                        result.Status = "MANUAL_REVIEW";
                        result.ErrorMessage = "ความมั่นใจต่ำ ต้องตรวจสอบด้วยตนเอง";
                    }
                }
                else
                {
                    result.ErrorMessage = "ไม่พบจำนวนเงินในสลิป";
                    result.Status = "MANUAL_REVIEW";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"เกิดข้อผิดพลาดในการประมวลผล: {ex.Message}";
                result.Status = "FAILED";
            }

            return result;
            */
        }

        /// <summary>
        /// Extract amount from OCR text using regex patterns
        /// </summary>
        private (decimal? Amount, double Confidence) ExtractAmountFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return (null, 0);

            // List of regex patterns for different slip formats
            var patterns = new List<(Regex Pattern, int Priority)>
            {
                // Pattern 1: "จำนวนเงิน 1,234.56 บาท" or "Amount 1,234.56 Baht"
                (new Regex(@"(?:จำนวนเงิน|Amount|ยอดเงิน|จ่าย)[\s:]*([0-9,]+\.?\d{0,2})[\s]*(?:บาท|Baht|THB)?",
                    RegexOptions.IgnoreCase), 90),

                // Pattern 2: "฿1,234.56" or "THB 1,234.56"
                (new Regex(@"(?:฿|THB|Baht)[\s]*([0-9,]+\.?\d{0,2})",
                    RegexOptions.IgnoreCase), 85),

                // Pattern 3: Standalone number with comma and decimal "1,234.56"
                (new Regex(@"\b([0-9]{1,3}(?:,[0-9]{3})*\.?\d{0,2})\b"), 80),

                // Pattern 4: Number without comma "1234.56"
                (new Regex(@"\b([0-9]+\.\d{2})\b"), 70),

                // Pattern 5: Number with "บาท" after
                (new Regex(@"([0-9,]+\.?\d{0,2})[\s]*บาท"), 85),
            };

            var foundAmounts = new List<(decimal Amount, int Priority)>();

            foreach (var (pattern, priority) in patterns)
            {
                var matches = pattern.Matches(text);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        string amountStr = match.Groups[1].Value.Replace(",", "").Trim();

                        if (decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
                        {
                            // Validate amount range (0.01 - 1,000,000)
                            if (amount >= 0.01m && amount <= 1000000m)
                            {
                                foundAmounts.Add((amount, priority));
                            }
                        }
                    }
                }
            }

            if (foundAmounts.Count == 0)
                return (null, 0);

            // Get highest priority amount
            var bestMatch = foundAmounts.OrderByDescending(x => x.Priority).First();

            // Calculate confidence
            double confidence = bestMatch.Priority;

            var duplicateMatches = foundAmounts.Count(x => x.Amount == bestMatch.Amount);
            if (duplicateMatches > 1)
            {
                confidence = Math.Min(100, confidence + 10);
            }

            return (bestMatch.Amount, confidence);
        }

        /// <summary>
        /// Save OCR result to database
        /// </summary>
        public void SaveOCRResult(long slipId, OCRResult result)
        {
            try
            {
                // Note: Payment_Slips table does NOT have OCR columns
                // (OCR_Amount, OCR_Status, OCR_Confidence, OCR_RawText, OCR_ErrorMessage, OCR_ProcessedDate)
                // Just log the result instead of saving to database

                var codeInstance = new code();
                var conn = _connectionString;

                string logMessage = result.Success
                    ? $"OCR Success - Amount: {result.Amount:N2}, Confidence: {result.Confidence:N2}%"
                    : $"OCR Failed - {result.ErrorMessage}";

                codeInstance.Logs(conn, "SlipOCR Result",
                    $"SlipID: {slipId}, Status: {result.Status}, {logMessage}", "SYSTEM");

                // If you want to store OCR results, you need to:
                // 1. Run migration to add OCR columns to Payment_Slips table
                // 2. Uncomment the code below:

                /*
                string query = @"
                    UPDATE Payment_Slips
                    SET OCR_Amount = @Amount,
                        OCR_Status = @Status,
                        OCR_Confidence = @Confidence,
                        OCR_RawText = @RawText,
                        OCR_ErrorMessage = @ErrorMessage,
                        OCR_ProcessedDate = GETDATE()
                    WHERE ID = @SlipId";

                var parameters = new Dictionary<string, object>
                {
                    { "@SlipId", slipId },
                    { "@Amount", result.Amount.HasValue ? (object)result.Amount.Value : DBNull.Value },
                    { "@Status", result.Status },
                    { "@Confidence", result.Confidence },
                    { "@RawText", result.RawText ?? "" },
                    { "@ErrorMessage", result.ErrorMessage ?? "" }
                };

                codeInstance.DatabaseInsertSafe(conn, query, parameters);
                */
            }
            catch (Exception ex)
            {
                // Log error
                var codeInstance = new code();
                codeInstance.Logs(_connectionString, "SlipOCRService.SaveOCRResult Error",
                    $"SlipID: {slipId}, Error: {ex.Message}", "SYSTEM");
            }
        }

        /// <summary>
        /// Verify if OCR amount matches declared amount
        /// </summary>
        public bool VerifyAmount(decimal ocrAmount, decimal declaredAmount, decimal tolerancePercent = 0)
        {
            if (tolerancePercent == 0)
            {
                return ocrAmount == declaredAmount;
            }

            decimal tolerance = declaredAmount * (tolerancePercent / 100);
            decimal lowerBound = declaredAmount - tolerance;
            decimal upperBound = declaredAmount + tolerance;

            return ocrAmount >= lowerBound && ocrAmount <= upperBound;
        }

        /// <summary>
        /// Get pending OCR slips (for batch processing)
        /// </summary>
        public DataTable GetPendingOCRSlips(int limit = 50)
        {
            try
            {
                var codeInstance = new code();
                var conn = _connectionString;

                // Note: Payment_Slips does NOT have OCR_Status column
                // Get all pending slips instead (VerificationStatus = 'PENDING')
                string query = @"
                    SELECT TOP (@Limit)
                        ID,
                        SlipFileURL,
                        Account_Receipt_ID,
                        Reservation_ID
                    FROM Payment_Slips
                    WHERE VerificationStatus = 'PENDING'
                      AND IsActive = 1
                      AND Status = 1
                    ORDER BY UploadedDate ASC";

                var parameters = new Dictionary<string, object>
                {
                    { "@Limit", limit }
                };

                return codeInstance.DatabaseQuerySafe(conn, query, parameters);
            }
            catch (Exception ex)
            {
                var codeInstance = new code();
                codeInstance.Logs(_connectionString, "SlipOCRService.GetPendingOCRSlips Error",
                    $"Error: {ex.Message}", "SYSTEM");
                return new DataTable();
            }
        }
    }
}
