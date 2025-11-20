using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Take_Time_BangPhra.Helpers;

namespace Take_Time_BangPhra.Class
{
    /// <summary>
    /// Service for managing payment methods using the standardized lookup table
    /// </summary>
    public class PaymentMethodService
    {
        private readonly string connectionString;
        private readonly LoggingService loggingService;

        public PaymentMethodService()
        {
            this.connectionString = DatabaseHelper.GetConnectionString();
            this.loggingService = new LoggingService();
        }

        public PaymentMethodService(string connectionString)
        {
            this.connectionString = connectionString;
            this.loggingService = new LoggingService(connectionString);
        }

        #region Payment Method Model

        public class PaymentMethod
        {
            public int ID { get; set; }
            public string Code { get; set; }
            public string Name_TH { get; set; }
            public string Name_EN { get; set; }
            public string BankName { get; set; }
            public string AccountNumber { get; set; }
            public bool IsActive { get; set; }
            public int DisplayOrder { get; set; }
            public bool RequiresSlip { get; set; }
            public DateTime CreatedDate { get; set; }
            public DateTime UpdatedDate { get; set; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Get all active payment methods ordered by DisplayOrder
        /// </summary>
        public List<PaymentMethod> GetActivePaymentMethods()
        {
            try
            {
                var methods = new List<PaymentMethod>();

                using (var connection = new SqlConnection(connectionString))
                {
                    using (var command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = @"
                            SELECT ID, Code, Name_TH, Name_EN, BankName, AccountNumber,
                                   IsActive, DisplayOrder, RequiresSlip, CreatedDate, UpdatedDate
                            FROM Account_PaymentMethod
                            WHERE IsActive = 1
                            ORDER BY DisplayOrder, Name_TH";

                        connection.Open();
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                methods.Add(MapFromReader(reader));
                            }
                        }
                    }
                }

                return methods;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, LoggingService.LogCategory.Accounting,
                    "Failed to get active payment methods");
                throw;
            }
        }

        /// <summary>
        /// Get all payment methods (including inactive)
        /// </summary>
        public List<PaymentMethod> GetAllPaymentMethods()
        {
            try
            {
                var methods = new List<PaymentMethod>();

                using (var connection = new SqlConnection(connectionString))
                {
                    using (var command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = @"
                            SELECT ID, Code, Name_TH, Name_EN, BankName, AccountNumber,
                                   IsActive, DisplayOrder, RequiresSlip, CreatedDate, UpdatedDate
                            FROM Account_PaymentMethod
                            ORDER BY DisplayOrder, Name_TH";

                        connection.Open();
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                methods.Add(MapFromReader(reader));
                            }
                        }
                    }
                }

                return methods;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, LoggingService.LogCategory.Accounting,
                    "Failed to get all payment methods");
                throw;
            }
        }

        /// <summary>
        /// Get payment method by ID
        /// </summary>
        public PaymentMethod GetPaymentMethodById(int id)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    using (var command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = @"
                            SELECT ID, Code, Name_TH, Name_EN, BankName, AccountNumber,
                                   IsActive, DisplayOrder, RequiresSlip, CreatedDate, UpdatedDate
                            FROM Account_PaymentMethod
                            WHERE ID = @ID";

                        command.Parameters.AddWithValue("@ID", id);

                        connection.Open();
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return MapFromReader(reader);
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, LoggingService.LogCategory.Accounting,
                    $"Failed to get payment method by ID: {id}");
                throw;
            }
        }

        /// <summary>
        /// Get payment method by code
        /// </summary>
        public PaymentMethod GetPaymentMethodByCode(string code)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    using (var command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = @"
                            SELECT ID, Code, Name_TH, Name_EN, BankName, AccountNumber,
                                   IsActive, DisplayOrder, RequiresSlip, CreatedDate, UpdatedDate
                            FROM Account_PaymentMethod
                            WHERE Code = @Code";

                        command.Parameters.AddWithValue("@Code", code);

                        connection.Open();
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return MapFromReader(reader);
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, LoggingService.LogCategory.Accounting,
                    $"Failed to get payment method by code: {code}");
                throw;
            }
        }

        /// <summary>
        /// Map legacy payment method string to new payment method ID
        /// </summary>
        public int? MapLegacyPaymentMethod(string legacyPaymentMethod)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(legacyPaymentMethod))
                    return null;

                using (var connection = new SqlConnection(connectionString))
                {
                    using (var command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = @"
                            SELECT ID FROM Account_PaymentMethod
                            WHERE @LegacyMethod LIKE '%' + Name_TH + '%'
                               OR @LegacyMethod = Code
                            ORDER BY LEN(Name_TH) DESC";  // Match longest name first

                        command.Parameters.AddWithValue("@LegacyMethod", legacyPaymentMethod);

                        connection.Open();
                        var result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);
                        }
                    }
                }

                // If no match found, return "OTHER" payment method
                return GetPaymentMethodByCode("OTHER")?.ID;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, LoggingService.LogCategory.Accounting,
                    $"Failed to map legacy payment method: {legacyPaymentMethod}");
                return GetPaymentMethodByCode("OTHER")?.ID;
            }
        }

        /// <summary>
        /// Parse comma-separated payment methods from Account_Receipt.Paid_Type
        /// Returns list of payment method IDs
        /// </summary>
        public List<int> ParsePaymentMethodsFromPaidType(string paidType)
        {
            try
            {
                var methodIds = new List<int>();

                if (string.IsNullOrWhiteSpace(paidType))
                    return methodIds;

                // Split by comma
                var methods = paidType.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(m => m.Trim())
                                     .ToList();

                foreach (var method in methods)
                {
                    var methodId = MapLegacyPaymentMethod(method);
                    if (methodId.HasValue && !methodIds.Contains(methodId.Value))
                    {
                        methodIds.Add(methodId.Value);
                    }
                }

                return methodIds;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, LoggingService.LogCategory.Accounting,
                    $"Failed to parse payment methods from Paid_Type: {paidType}");
                return new List<int>();
            }
        }

        /// <summary>
        /// Get payment method name in Thai by ID
        /// </summary>
        public string GetPaymentMethodNameTH(int id)
        {
            var method = GetPaymentMethodById(id);
            return method?.Name_TH ?? "ไม่ระบุ";
        }

        /// <summary>
        /// Get payment method name in English by ID
        /// </summary>
        public string GetPaymentMethodNameEN(int id)
        {
            var method = GetPaymentMethodById(id);
            return method?.Name_EN ?? "Unknown";
        }

        /// <summary>
        /// Check if payment method requires slip upload
        /// </summary>
        public bool RequiresPaymentSlip(int paymentMethodId)
        {
            var method = GetPaymentMethodById(paymentMethodId);
            return method?.RequiresSlip ?? false;
        }

        /// <summary>
        /// Create new payment method
        /// </summary>
        public int CreatePaymentMethod(PaymentMethod method, int? userId = null)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    using (var command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = @"
                            INSERT INTO Account_PaymentMethod
                                (Code, Name_TH, Name_EN, BankName, AccountNumber,
                                 IsActive, DisplayOrder, RequiresSlip, CreatedDate, UpdatedDate)
                            VALUES
                                (@Code, @Name_TH, @Name_EN, @BankName, @AccountNumber,
                                 @IsActive, @DisplayOrder, @RequiresSlip, GETDATE(), GETDATE());

                            SELECT SCOPE_IDENTITY();";

                        command.Parameters.AddWithValue("@Code", method.Code);
                        command.Parameters.AddWithValue("@Name_TH", method.Name_TH);
                        command.Parameters.AddWithValue("@Name_EN", method.Name_EN ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@BankName", method.BankName ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@AccountNumber", method.AccountNumber ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@IsActive", method.IsActive);
                        command.Parameters.AddWithValue("@DisplayOrder", method.DisplayOrder);
                        command.Parameters.AddWithValue("@RequiresSlip", method.RequiresSlip);

                        connection.Open();
                        var newId = Convert.ToInt32(command.ExecuteScalar());

                        loggingService.LogAccountingOperation(
                            "CreatePaymentMethod",
                            $"Created payment method: {method.Code} - {method.Name_TH}",
                            true,
                            userId);

                        return newId;
                    }
                }
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, LoggingService.LogCategory.Accounting,
                    $"Failed to create payment method: {method?.Code}", userId);
                throw;
            }
        }

        /// <summary>
        /// Update existing payment method
        /// </summary>
        public bool UpdatePaymentMethod(PaymentMethod method, int? userId = null)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    using (var command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = @"
                            UPDATE Account_PaymentMethod
                            SET Code = @Code,
                                Name_TH = @Name_TH,
                                Name_EN = @Name_EN,
                                BankName = @BankName,
                                AccountNumber = @AccountNumber,
                                IsActive = @IsActive,
                                DisplayOrder = @DisplayOrder,
                                RequiresSlip = @RequiresSlip,
                                UpdatedDate = GETDATE()
                            WHERE ID = @ID";

                        command.Parameters.AddWithValue("@ID", method.ID);
                        command.Parameters.AddWithValue("@Code", method.Code);
                        command.Parameters.AddWithValue("@Name_TH", method.Name_TH);
                        command.Parameters.AddWithValue("@Name_EN", method.Name_EN ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@BankName", method.BankName ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@AccountNumber", method.AccountNumber ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@IsActive", method.IsActive);
                        command.Parameters.AddWithValue("@DisplayOrder", method.DisplayOrder);
                        command.Parameters.AddWithValue("@RequiresSlip", method.RequiresSlip);

                        connection.Open();
                        var rowsAffected = command.ExecuteNonQuery();

                        loggingService.LogAccountingOperation(
                            "UpdatePaymentMethod",
                            $"Updated payment method: {method.Code} - {method.Name_TH}",
                            rowsAffected > 0,
                            userId);

                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, LoggingService.LogCategory.Accounting,
                    $"Failed to update payment method: {method?.Code}", userId);
                throw;
            }
        }

        /// <summary>
        /// Deactivate payment method (soft delete)
        /// </summary>
        public bool DeactivatePaymentMethod(int id, int? userId = null)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    using (var command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = @"
                            UPDATE Account_PaymentMethod
                            SET IsActive = 0,
                                UpdatedDate = GETDATE()
                            WHERE ID = @ID";

                        command.Parameters.AddWithValue("@ID", id);

                        connection.Open();
                        var rowsAffected = command.ExecuteNonQuery();

                        loggingService.LogAccountingOperation(
                            "DeactivatePaymentMethod",
                            $"Deactivated payment method ID: {id}",
                            rowsAffected > 0,
                            userId);

                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, LoggingService.LogCategory.Accounting,
                    $"Failed to deactivate payment method ID: {id}", userId);
                throw;
            }
        }

        #endregion

        #region Private Methods

        private PaymentMethod MapFromReader(SqlDataReader reader)
        {
            return new PaymentMethod
            {
                ID = reader.GetInt32(reader.GetOrdinal("ID")),
                Code = reader.GetString(reader.GetOrdinal("Code")),
                Name_TH = reader.GetString(reader.GetOrdinal("Name_TH")),
                Name_EN = reader.IsDBNull(reader.GetOrdinal("Name_EN"))
                    ? null : reader.GetString(reader.GetOrdinal("Name_EN")),
                BankName = reader.IsDBNull(reader.GetOrdinal("BankName"))
                    ? null : reader.GetString(reader.GetOrdinal("BankName")),
                AccountNumber = reader.IsDBNull(reader.GetOrdinal("AccountNumber"))
                    ? null : reader.GetString(reader.GetOrdinal("AccountNumber")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                DisplayOrder = reader.GetInt32(reader.GetOrdinal("DisplayOrder")),
                RequiresSlip = reader.GetBoolean(reader.GetOrdinal("RequiresSlip")),
                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                UpdatedDate = reader.GetDateTime(reader.GetOrdinal("UpdatedDate"))
            };
        }

        #endregion
    }
}
