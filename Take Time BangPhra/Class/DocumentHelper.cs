using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Take_Time_BangPhra.Class
{
    /// <summary>
    /// Helper class for document number generation and document operations
    /// Centralizes document number logic from _Default.aspx.cs, _Default2.aspx.cs, DatabaseHelper.cs
    /// </summary>
    public class DocumentHelper
    {
        private readonly code _code;
        private readonly string _connectionString;

        // Whitelist of allowed table names for document number generation
        private static readonly string[] AllowedTables = {
            "Account_Receipt",
            "Account_Payment",
            "Reservation"
        };

        /// <summary>
        /// Initializes a new instance of DocumentHelper
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        public DocumentHelper(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _code = new code();
        }

        /// <summary>
        /// Generates a sequential document number in format: DOCYYMMDDNNN
        /// Example: REC2501150001, PAY2501150001, RSV2501150001
        ///
        /// Replaces: createDocNumber() from _Default.aspx.cs, _Default2.aspx.cs, DatabaseHelper.cs
        /// </summary>
        /// <param name="tableName">Table name (must be whitelisted)</param>
        /// <param name="documentType">Document type prefix (e.g., "REC", "PAY", "RSV")</param>
        /// <param name="date">Date for document number generation</param>
        /// <returns>Generated document number</returns>
        /// <exception cref="ArgumentException">If table name is not whitelisted</exception>
        /// <example>
        /// var helper = new DocumentHelper(conn);
        /// string receiptNumber = helper.CreateDocumentNumber("Account_Receipt", "REC", DateTime.Now);
        /// // Returns: REC2501150001
        /// </example>
        public string CreateDocumentNumber(string tableName, string documentType, DateTime date)
        {
            // SECURITY: Validate table name against whitelist
            if (!AllowedTables.Contains(tableName))
            {
                throw new ArgumentException(
                    $"Invalid table name: {tableName}. Allowed tables: {string.Join(", ", AllowedTables)}",
                    nameof(tableName));
            }

            if (string.IsNullOrWhiteSpace(documentType))
            {
                throw new ArgumentNullException(nameof(documentType), "Document type cannot be null or empty");
            }

            // Format date parts (YY MM DD)
            string year = date.Year.ToString().Substring(2, 2);  // Last 2 digits of year
            string month = date.Month.ToString("00");             // 2-digit month
            string day = date.Day.ToString("00");                 // 2-digit day

            // Create pattern for LIKE query: DOCYYMMDD%
            string pattern = documentType + year + month + day + "%";

            // SECURE: Query using parameterized query
            var parameters = new Dictionary<string, object>
            {
                { "@Pattern", pattern }
            };

            DataTable dt = _code.DatabaseQuerySafe(
                _connectionString,
                code.AdaptSql($"SELECT TOP 1 ID FROM [{tableName}] WHERE ID LIKE @Pattern ORDER BY ID DESC"),
                parameters);

            // Calculate next sequence number
            int nextSequence = 1;

            if (dt.Rows.Count > 0)
            {
                string lastDocNumber = dt.Rows[0][0].ToString();

                // Extract last 3 digits (sequence number)
                if (lastDocNumber.Length >= 3)
                {
                    string lastThreeDigits = lastDocNumber.Substring(lastDocNumber.Length - 3);

                    if (int.TryParse(lastThreeDigits, out int lastSequence))
                    {
                        nextSequence = lastSequence + 1;
                    }
                }
            }

            // Format sequence as 3-digit string (001, 002, ..., 999)
            string sequenceNumber = nextSequence.ToString("000");

            // Build final document number: DOCYYMMDDNNN
            return documentType + year + month + day + sequenceNumber;
        }

        /// <summary>
        /// Creates a receipt number for today's date
        /// </summary>
        /// <returns>Receipt number in format REC2501150001</returns>
        public string CreateReceiptNumber()
        {
            return CreateDocumentNumber("Account_Receipt", "REC", DateTime.Now);
        }

        /// <summary>
        /// Creates a payment voucher number for today's date
        /// </summary>
        /// <returns>Payment number in format PAY2501150001</returns>
        public string CreatePaymentNumber()
        {
            return CreateDocumentNumber("Account_Payment", "PAY", DateTime.Now);
        }

        /// <summary>
        /// Creates a reservation number for today's date
        /// </summary>
        /// <returns>Reservation number in format RSV2501150001</returns>
        public string CreateReservationNumber()
        {
            return CreateDocumentNumber("Reservation", "RSV", DateTime.Now);
        }

        /// <summary>
        /// Creates a document number for a specific date
        /// </summary>
        /// <param name="tableName">Table name</param>
        /// <param name="documentType">Document type prefix</param>
        /// <param name="year">Year</param>
        /// <param name="month">Month</param>
        /// <param name="day">Day</param>
        /// <returns>Generated document number</returns>
        [Obsolete("Use CreateDocumentNumber(tableName, documentType, DateTime) instead")]
        public string CreateDocumentNumber(string tableName, string documentType, string year, string month, string day)
        {
            // Convert string parameters to DateTime
            int y = int.Parse(year);
            int m = int.Parse(month);
            int d = int.Parse(day);

            DateTime date = new DateTime(y, m, d);
            return CreateDocumentNumber(tableName, documentType, date);
        }

        /// <summary>
        /// Validates if a document number already exists
        /// </summary>
        /// <param name="tableName">Table name</param>
        /// <param name="documentNumber">Document number to check</param>
        /// <returns>True if document exists</returns>
        public bool DocumentNumberExists(string tableName, string documentNumber)
        {
            // SECURITY: Validate table name
            if (!AllowedTables.Contains(tableName))
            {
                throw new ArgumentException($"Invalid table name: {tableName}", nameof(tableName));
            }

            var parameters = new Dictionary<string, object>
            {
                { "@DocumentNumber", documentNumber }
            };

            DataTable dt = _code.DatabaseQuerySafe(
                _connectionString,
                $"SELECT COUNT(*) FROM [{tableName}] WHERE ID = @DocumentNumber",
                parameters);

            if (dt.Rows.Count > 0)
            {
                return Convert.ToInt32(dt.Rows[0][0]) > 0;
            }

            return false;
        }

        /// <summary>
        /// Gets the next available sequence number for a document type on a specific date
        /// </summary>
        /// <param name="tableName">Table name</param>
        /// <param name="documentType">Document type prefix</param>
        /// <param name="date">Date to check</param>
        /// <returns>Next available sequence number</returns>
        public int GetNextSequenceNumber(string tableName, string documentType, DateTime date)
        {
            // SECURITY: Validate table name
            if (!AllowedTables.Contains(tableName))
            {
                throw new ArgumentException($"Invalid table name: {tableName}", nameof(tableName));
            }

            string year = date.Year.ToString().Substring(2, 2);
            string month = date.Month.ToString("00");
            string day = date.Day.ToString("00");
            string pattern = documentType + year + month + day + "%";

            var parameters = new Dictionary<string, object>
            {
                { "@Pattern", pattern }
            };

            DataTable dt = _code.DatabaseQuerySafe(
                _connectionString,
                code.AdaptSql($"SELECT COUNT(*) FROM [{tableName}] WHERE ID LIKE @Pattern"),
                parameters);

            if (dt.Rows.Count > 0)
            {
                return Convert.ToInt32(dt.Rows[0][0]) + 1;
            }

            return 1;
        }

        /// <summary>
        /// Parses a document number into its components
        /// </summary>
        /// <param name="documentNumber">Document number to parse</param>
        /// <returns>Dictionary with DocType, Year, Month, Day, Sequence</returns>
        /// <example>
        /// var parts = helper.ParseDocumentNumber("REC2501150001");
        /// // Returns: { DocType: "REC", Year: "25", Month: "01", Day: "15", Sequence: "001" }
        /// </example>
        public Dictionary<string, string> ParseDocumentNumber(string documentNumber)
        {
            if (string.IsNullOrWhiteSpace(documentNumber) || documentNumber.Length < 11)
            {
                throw new ArgumentException("Invalid document number format", nameof(documentNumber));
            }

            var result = new Dictionary<string, string>();

            // Assuming format: DOCYYMMDDNNN (min 11 chars)
            int docTypeLength = documentNumber.Length - 8; // Subtract YYMMDDNNN (8 chars)

            result["DocType"] = documentNumber.Substring(0, docTypeLength);
            result["Year"] = documentNumber.Substring(docTypeLength, 2);
            result["Month"] = documentNumber.Substring(docTypeLength + 2, 2);
            result["Day"] = documentNumber.Substring(docTypeLength + 4, 2);
            result["Sequence"] = documentNumber.Substring(docTypeLength + 6);

            return result;
        }

        /// <summary>
        /// Gets document creation date from document number
        /// </summary>
        /// <param name="documentNumber">Document number</param>
        /// <returns>Document date or null if invalid</returns>
        public DateTime? GetDocumentDate(string documentNumber)
        {
            try
            {
                var parts = ParseDocumentNumber(documentNumber);

                int year = 2000 + int.Parse(parts["Year"]);
                int month = int.Parse(parts["Month"]);
                int day = int.Parse(parts["Day"]);

                return new DateTime(year, month, day);
            }
            catch
            {
                return null;
            }
        }
    }
}
