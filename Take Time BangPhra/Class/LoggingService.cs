using System;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Web;
using Take_Time_BangPhra.Helpers;

namespace Take_Time_BangPhra.Class
{
    /// <summary>
    /// Service for comprehensive error and audit logging for accounting operations
    /// </summary>
    public class LoggingService
    {
        private readonly string connectionString;

        public LoggingService()
        {
            this.connectionString = DatabaseHelper.GetConnectionString();
        }

        public LoggingService(string connectionString)
        {
            this.connectionString = connectionString;
        }

        #region Enums

        public enum LogLevel
        {
            Debug = 1,
            Info = 2,
            Warning = 3,
            Error = 4,
            Critical = 5
        }

        public enum LogCategory
        {
            General = 0,
            Accounting = 1,
            Payment = 2,
            Receipt = 3,
            Revenue = 4,
            Reconciliation = 5,
            DataIntegrity = 6,
            Performance = 7
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Log a general message
        /// </summary>
        public void Log(LogLevel level, LogCategory category, string message,
                       string details = null, int? userId = null, long? reservationId = null)
        {
            try
            {
                InsertLog(level, category, message, details, null, null, userId, reservationId, null);
            }
            catch (Exception ex)
            {
                // Fallback: log to file system if database logging fails
                LogToFileSystem(level, category, message, ex);
            }
        }

        /// <summary>
        /// Log an exception
        /// </summary>
        public void LogException(Exception ex, LogCategory category = LogCategory.General,
                                string additionalInfo = null, int? userId = null, long? reservationId = null)
        {
            try
            {
                var message = ex.Message;
                var details = BuildExceptionDetails(ex, additionalInfo);
                var stackTrace = ex.StackTrace;

                InsertLog(LogLevel.Error, category, message, details, stackTrace, null,
                         userId, reservationId, null);
            }
            catch (Exception logEx)
            {
                // Fallback: log to file system if database logging fails
                LogToFileSystem(LogLevel.Error, category, ex.Message, logEx);
            }
        }

        /// <summary>
        /// Log an accounting operation
        /// </summary>
        public void LogAccountingOperation(string operation, string details, bool success,
                                          int? userId = null, long? reservationId = null, string receiptId = null)
        {
            try
            {
                var level = success ? LogLevel.Info : LogLevel.Warning;
                var message = $"Accounting Operation: {operation}";
                var fullDetails = $"Operation: {operation}\nSuccess: {success}\nDetails: {details}";

                InsertLog(level, LogCategory.Accounting, message, fullDetails, null, null,
                         userId, reservationId, receiptId);
            }
            catch (Exception ex)
            {
                LogToFileSystem(LogLevel.Error, LogCategory.Accounting, operation, ex);
            }
        }

        /// <summary>
        /// Log a payment transaction
        /// </summary>
        public void LogPaymentTransaction(long paymentHistoryId, decimal amount, string paymentMethod,
                                         string status, int? userId = null, long? reservationId = null)
        {
            try
            {
                var message = $"Payment Transaction: {paymentMethod} - {amount:C}";
                var details = $"PaymentHistoryID: {paymentHistoryId}\n" +
                             $"Amount: {amount:C}\n" +
                             $"Method: {paymentMethod}\n" +
                             $"Status: {status}";

                InsertLog(LogLevel.Info, LogCategory.Payment, message, details, null, null,
                         userId, reservationId, null);
            }
            catch (Exception ex)
            {
                LogToFileSystem(LogLevel.Error, LogCategory.Payment, $"Payment {paymentHistoryId}", ex);
            }
        }

        /// <summary>
        /// Log a revenue calculation
        /// </summary>
        public void LogRevenueCalculation(DateTime startDate, DateTime endDate,
                                         decimal totalRevenue, string breakdown, int? userId = null)
        {
            try
            {
                var message = $"Revenue Calculation: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}";
                var details = $"Date Range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}\n" +
                             $"Total Revenue: {totalRevenue:C}\n" +
                             $"Breakdown:\n{breakdown}";

                InsertLog(LogLevel.Info, LogCategory.Revenue, message, details, null, null,
                         userId, null, null);
            }
            catch (Exception ex)
            {
                LogToFileSystem(LogLevel.Error, LogCategory.Revenue, "Revenue Calculation", ex);
            }
        }

        /// <summary>
        /// Log a data integrity issue
        /// </summary>
        public void LogDataIntegrityIssue(string issueType, string description,
                                         string affectedTable, string affectedRecordId = null)
        {
            try
            {
                var message = $"Data Integrity Issue: {issueType}";
                var details = $"Issue Type: {issueType}\n" +
                             $"Description: {description}\n" +
                             $"Affected Table: {affectedTable}\n" +
                             $"Affected Record ID: {affectedRecordId ?? "N/A"}";

                InsertLog(LogLevel.Critical, LogCategory.DataIntegrity, message, details,
                         null, affectedTable, null, null, affectedRecordId);
            }
            catch (Exception ex)
            {
                LogToFileSystem(LogLevel.Critical, LogCategory.DataIntegrity, issueType, ex);
            }
        }

        /// <summary>
        /// Log a reconciliation result
        /// </summary>
        public void LogReconciliation(DateTime startDate, DateTime endDate,
                                     int matchedCount, int mismatchCount, int missingCount,
                                     string details = null)
        {
            try
            {
                var message = $"Reconciliation: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}";
                var fullDetails = $"Date Range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}\n" +
                                 $"Matched: {matchedCount}\n" +
                                 $"Mismatched: {mismatchCount}\n" +
                                 $"Missing: {missingCount}\n" +
                                 $"Details: {details ?? "N/A"}";

                var level = mismatchCount > 0 || missingCount > 0 ? LogLevel.Warning : LogLevel.Info;

                InsertLog(level, LogCategory.Reconciliation, message, fullDetails, null, null,
                         null, null, null);
            }
            catch (Exception ex)
            {
                LogToFileSystem(LogLevel.Error, LogCategory.Reconciliation, "Reconciliation", ex);
            }
        }

        /// <summary>
        /// Get logs for a specific date range and category
        /// </summary>
        public DataTable GetLogs(DateTime? startDate = null, DateTime? endDate = null,
                                LogCategory? category = null, LogLevel? minLevel = null,
                                int? userId = null, long? reservationId = null,
                                int maxRecords = 1000)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    using (var command = new SqlCommand())
                    {
                        command.Connection = connection;
                        var sql = new StringBuilder(@"
                            SELECT TOP (@MaxRecords)
                                ID, CreatedDate, LogLevel, Category, Message, Details,
                                StackTrace, AffectedTable, UserID, ReservationID, ReceiptID
                            FROM System_Logs
                            WHERE 1=1");

                        command.Parameters.AddWithValue("@MaxRecords", maxRecords);

                        if (startDate.HasValue)
                        {
                            sql.Append(" AND CreatedDate >= @StartDate");
                            command.Parameters.AddWithValue("@StartDate", startDate.Value);
                        }

                        if (endDate.HasValue)
                        {
                            sql.Append(" AND CreatedDate <= @EndDate");
                            command.Parameters.AddWithValue("@EndDate", endDate.Value);
                        }

                        if (category.HasValue)
                        {
                            sql.Append(" AND Category = @Category");
                            command.Parameters.AddWithValue("@Category", (int)category.Value);
                        }

                        if (minLevel.HasValue)
                        {
                            sql.Append(" AND LogLevel >= @MinLevel");
                            command.Parameters.AddWithValue("@MinLevel", (int)minLevel.Value);
                        }

                        if (userId.HasValue)
                        {
                            sql.Append(" AND UserID = @UserID");
                            command.Parameters.AddWithValue("@UserID", userId.Value);
                        }

                        if (reservationId.HasValue)
                        {
                            sql.Append(" AND ReservationID = @ReservationID");
                            command.Parameters.AddWithValue("@ReservationID", reservationId.Value);
                        }

                        sql.Append(" ORDER BY CreatedDate DESC");

                        command.CommandText = sql.ToString();

                        using (var adapter = new SqlDataAdapter(command))
                        {
                            var dt = new DataTable();
                            adapter.Fill(dt);
                            return dt;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFileSystem(LogLevel.Error, LogCategory.General, "GetLogs failed", ex);
                return new DataTable();
            }
        }

        #endregion

        #region Private Methods

        private void InsertLog(LogLevel level, LogCategory category, string message,
                              string details, string stackTrace, string affectedTable,
                              int? userId, long? reservationId, string receiptId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                using (var command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = @"
                        INSERT INTO System_Logs
                            (CreatedDate, LogLevel, Category, Message, Details, StackTrace,
                             AffectedTable, UserID, ReservationID, ReceiptID)
                        VALUES
                            (@CreatedDate, @LogLevel, @Category, @Message, @Details, @StackTrace,
                             @AffectedTable, @UserID, @ReservationID, @ReceiptID)";

                    command.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
                    command.Parameters.AddWithValue("@LogLevel", (int)level);
                    command.Parameters.AddWithValue("@Category", (int)category);
                    command.Parameters.AddWithValue("@Message", message ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Details", details ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@StackTrace", stackTrace ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@AffectedTable", affectedTable ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@UserID", userId ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@ReservationID", reservationId ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@ReceiptID", receiptId ?? (object)DBNull.Value);

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }

        private string BuildExceptionDetails(Exception ex, string additionalInfo)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Exception Type: {ex.GetType().Name}");
            sb.AppendLine($"Message: {ex.Message}");

            if (!string.IsNullOrEmpty(additionalInfo))
            {
                sb.AppendLine($"Additional Info: {additionalInfo}");
            }

            if (ex.InnerException != null)
            {
                sb.AppendLine("\nInner Exception:");
                sb.AppendLine($"  Type: {ex.InnerException.GetType().Name}");
                sb.AppendLine($"  Message: {ex.InnerException.Message}");
            }

            // Add HTTP context info if available
            if (HttpContext.Current != null)
            {
                sb.AppendLine("\nHTTP Context:");
                sb.AppendLine($"  URL: {HttpContext.Current.Request.Url}");
                sb.AppendLine($"  User Agent: {HttpContext.Current.Request.UserAgent}");
                if (HttpContext.Current.User != null && HttpContext.Current.User.Identity.IsAuthenticated)
                {
                    sb.AppendLine($"  User: {HttpContext.Current.User.Identity.Name}");
                }
            }

            return sb.ToString();
        }

        private void LogToFileSystem(LogLevel level, LogCategory category, string message, Exception ex)
        {
            try
            {
                var logDir = HttpContext.Current?.Server.MapPath("~/Logs") ?? "Logs";
                if (!System.IO.Directory.Exists(logDir))
                {
                    System.IO.Directory.CreateDirectory(logDir);
                }

                var logFile = System.IO.Path.Combine(logDir, $"Accounting_{DateTime.Now:yyyyMMdd}.log");
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{category}] {message}\n";

                if (ex != null)
                {
                    logMessage += $"Exception: {ex.Message}\n{ex.StackTrace}\n";
                }

                logMessage += "----------------------------------------\n";

                System.IO.File.AppendAllText(logFile, logMessage);
            }
            catch
            {
                // Silent fail - can't log the logging error
            }
        }

        #endregion
    }
}
