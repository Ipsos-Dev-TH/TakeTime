// CustomerService.cs
// Centralized Customer Management Service
// Prevents duplicate customer data and ensures data consistency across all pages

using System;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using Take_Time_BangPhra.Helpers;

namespace Take_Time_BangPhra.Services
{
    public class CustomerService
    {
        private readonly SqlConnection _conn;

        public CustomerService(SqlConnection connection)
        {
            _conn = connection;
        }

        #region Customer CRUD Operations

        /// <summary>
        /// Upsert customer (Insert if new, Update if exists)
        /// Prevents duplicate customers and ensures data consistency
        /// </summary>
        public CustomerUpsertResult UpsertCustomer(CustomerData customer, short? changedById = null, string changeSource = null)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand("sp_UpsertCustomer", _conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 30;

                    // Input parameters
                    cmd.Parameters.AddWithValue("@MobilePhone", customer.MobilePhone ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Name", customer.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email", customer.Email ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TaxID", customer.TaxID ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Address", customer.Address ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@District", customer.District ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Subdistrict", customer.Subdistrict ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Province", customer.Province ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Postcode", customer.Postcode ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CustomerType_ID", customer.CustomerTypeID ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ChangedBy_ID", changedById ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ChangeSource", changeSource ?? (object)DBNull.Value);

                    // Get IP address
                    string ipAddress = GetClientIPAddress();
                    cmd.Parameters.AddWithValue("@IPAddress", ipAddress ?? (object)DBNull.Value);

                    // Output parameter
                    SqlParameter isNewParam = new SqlParameter("@IsNewCustomer", SqlDbType.Bit)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(isNewParam);

                    // Execute
                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    int returnValue = cmd.ExecuteNonQuery();

                    bool isNew = (bool)isNewParam.Value;

                    return new CustomerUpsertResult
                    {
                        Success = true,
                        IsNewCustomer = isNew,
                        MobilePhone = customer.MobilePhone,
                        Message = isNew ? "สร้างข้อมูลลูกค้าใหม่สำเร็จ" : "อัปเดตข้อมูลลูกค้าสำเร็จ"
                    };
                }
            }
            catch (SqlException ex)
            {
                // Handle specific errors
                if (ex.Message.Contains("Email already exists"))
                {
                    return new CustomerUpsertResult
                    {
                        Success = false,
                        Message = "อีเมลนี้ถูกใช้งานโดยลูกค้าท่านอื่นแล้ว"
                    };
                }
                else if (ex.Message.Contains("Tax ID already exists"))
                {
                    return new CustomerUpsertResult
                    {
                        Success = false,
                        Message = "เลขประจำตัวผู้เสียภาษีนี้ถูกใช้งานโดยลูกค้าท่านอื่นแล้ว"
                    };
                }
                else
                {
                    return new CustomerUpsertResult
                    {
                        Success = false,
                        Message = "เกิดข้อผิดพลาด: " + ex.Message
                    };
                }
            }
            catch (Exception ex)
            {
                return new CustomerUpsertResult
                {
                    Success = false,
                    Message = "เกิดข้อผิดพลาด: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Get customer by phone number
        /// </summary>
        public CustomerData GetCustomer(string mobilePhone)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand("sp_GetCustomer", _conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@MobilePhone", mobilePhone);

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new CustomerData
                            {
                                MobilePhone = reader["MobilePhone"].ToString(),
                                Name = reader["Name"].ToString(),
                                Email = reader["Email"] != DBNull.Value ? reader["Email"].ToString() : null,
                                TaxID = reader["TaxID"] != DBNull.Value ? reader["TaxID"].ToString() : null,
                                Address = reader["Address"] != DBNull.Value ? reader["Address"].ToString() : null,
                                District = reader["District"] != DBNull.Value ? reader["District"].ToString() : null,
                                Subdistrict = reader["Subdistrict"] != DBNull.Value ? reader["Subdistrict"].ToString() : null,
                                Province = reader["Province"] != DBNull.Value ? reader["Province"].ToString() : null,
                                Postcode = reader["Postcode"] != DBNull.Value ? reader["Postcode"].ToString() : null,
                                CustomerTypeID = reader["CustomerType_ID"] != DBNull.Value ? Convert.ToByte(reader["CustomerType_ID"]) : (byte?)null,
                                CustomerTypeName = reader["CustomerTypeName"] != DBNull.Value ? reader["CustomerTypeName"].ToString() : null,
                                Status = reader["Status"] != DBNull.Value && Convert.ToBoolean(reader["Status"]),
                                IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"]),
                                CreatedDate = reader["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedDate"]) : (DateTime?)null,
                                LastUpdated = reader["LastUpdated"] != DBNull.Value ? Convert.ToDateTime(reader["LastUpdated"]) : (DateTime?)null,
                                TotalReservations = reader["TotalReservations"] != DBNull.Value ? Convert.ToInt32(reader["TotalReservations"]) : 0,
                                LastReservationDate = reader["LastReservationDate"] != DBNull.Value ? Convert.ToDateTime(reader["LastReservationDate"]) : (DateTime?)null
                            };
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception("ไม่สามารถดึงข้อมูลลูกค้าได้: " + ex.Message);
            }
        }

        /// <summary>
        /// Search customers by name, phone, or email
        /// </summary>
        public DataTable SearchCustomers(string searchTerm, bool activeOnly = true, int maxResults = 50)
        {
            try
            {
                string sql = "SELECT TOP " + maxResults.ToString() + @"
                    C.MobilePhone,
                    C.FullName,
                    C.Email,
                    C.IDNumber,
                    C.Status,
                    CASE WHEN C.Status = 1 THEN 1 ELSE 0 END AS IsActive,
                    ISNULL(A.Province, '-') AS Province,
                    ISNULL(CT.Customer_Type, '-') AS CustomerTypeName,
                    ISNULL((SELECT COUNT(*) FROM Reservation R WHERE R.Customer_MobilePhone = C.MobilePhone), 0) AS TotalReservations
                FROM Customer C
                LEFT JOIN Address A ON A.ID = C.Address_ID
                LEFT JOIN Customer_Type CT ON CT.ID = C.Customer_Type_ID
                WHERE 1=1";

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    sql += @" AND (
                        C.MobilePhone LIKE @SearchTerm
                        OR C.FullName LIKE @SearchTerm
                        OR C.Email LIKE @SearchTerm
                        OR C.IDNumber LIKE @SearchTerm
                    )";
                }

                if (activeOnly)
                {
                    sql += " AND C.Status = 1";
                }

                sql += " ORDER BY C.FullName";

                using (SqlCommand cmd = new SqlCommand(sql, _conn))
                {
                    cmd.CommandType = CommandType.Text;
                    if (!string.IsNullOrEmpty(searchTerm))
                    {
                        cmd.Parameters.AddWithValue("@SearchTerm", "%" + searchTerm + "%");
                    }

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("ไม่สามารถค้นหาลูกค้าได้: " + ex.Message);
            }
        }

        /// <summary>
        /// Get customer audit history
        /// </summary>
        public DataTable GetCustomerAuditHistory(string mobilePhone, int maxResults = 100)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand("sp_GetCustomerAuditHistory", _conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@MobilePhone", mobilePhone);
                    cmd.Parameters.AddWithValue("@MaxResults", maxResults);

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("ไม่สามารถดึงประวัติการแก้ไขได้: " + ex.Message);
            }
        }

        /// <summary>
        /// Delete customer (soft delete if has reservations, hard delete otherwise)
        /// </summary>
        public bool DeleteCustomer(string mobilePhone, short? deletedById = null, string reason = null)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand("sp_DeleteCustomer", _conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@MobilePhone", mobilePhone);
                    cmd.Parameters.AddWithValue("@DeletedBy_ID", deletedById ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Reason", reason ?? (object)DBNull.Value);

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("ไม่สามารถลบลูกค้าได้: " + ex.Message);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Check if customer exists
        /// </summary>
        public bool CustomerExists(string mobilePhone)
        {
            try
            {
                string query = "SELECT COUNT(*) FROM Customer WHERE MobilePhone = @MobilePhone AND Status = 1";

                using (SqlCommand cmd = new SqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@MobilePhone", mobilePhone);

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get all customer types
        /// </summary>
        public DataTable GetCustomerTypes()
        {
            try
            {
                string query = "SELECT ID, CustomerTypeName FROM Customer_Type WHERE Status = 1 ORDER BY CustomerTypeName";

                using (SqlCommand cmd = new SqlCommand(query, _conn))
                {
                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("ไม่สามารถดึงประเภทลูกค้าได้: " + ex.Message);
            }
        }

        /// <summary>
        /// Get customer by reservation ID
        /// </summary>
        public CustomerData GetCustomerByReservation(long reservationId)
        {
            try
            {
                string query = @"
                    SELECT C.MobilePhone
                    FROM Reservation R
                    INNER JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                    WHERE R.ID = @ReservationID";

                using (SqlCommand cmd = new SqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@ReservationID", reservationId);

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    object result = cmd.ExecuteScalar();

                    if (result != null && result != DBNull.Value)
                    {
                        string mobilePhone = result.ToString();
                        return GetCustomer(mobilePhone);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception("ไม่สามารถดึงข้อมูลลูกค้าจากการจองได้: " + ex.Message);
            }
        }

        /// <summary>
        /// Get customer reservation history count
        /// </summary>
        public int GetCustomerReservationCount(string mobilePhone, string status = null)
        {
            try
            {
                string query = @"
                    SELECT COUNT(*)
                    FROM Reservation
                    WHERE Customer_MobilePhone = @MobilePhone";

                if (!string.IsNullOrEmpty(status))
                {
                    query += " AND Status = @Status";
                }

                using (SqlCommand cmd = new SqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@MobilePhone", mobilePhone);

                    if (!string.IsNullOrEmpty(status))
                    {
                        cmd.Parameters.AddWithValue("@Status", status);
                    }

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    return (int)cmd.ExecuteScalar();
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get client IP address
        /// </summary>
        private string GetClientIPAddress()
        {
            try
            {
                HttpContext context = HttpContext.Current;
                if (context != null)
                {
                    string ipAddress = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

                    if (!string.IsNullOrEmpty(ipAddress))
                    {
                        string[] addresses = ipAddress.Split(',');
                        if (addresses.Length != 0)
                        {
                            return addresses[0];
                        }
                    }

                    return context.Request.ServerVariables["REMOTE_ADDR"];
                }
            }
            catch
            {
                // Ignore
            }

            return null;
        }

        #endregion

        #region Validation Methods

        /// <summary>
        /// Validate phone number format
        /// </summary>
        public static bool IsValidPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            // Remove spaces and dashes
            phoneNumber = phoneNumber.Replace(" ", "").Replace("-", "");

            // Thai phone number: 10 digits starting with 0
            if (phoneNumber.Length == 10 && phoneNumber.StartsWith("0"))
                return true;

            // International format
            if (phoneNumber.Length >= 10 && phoneNumber.Length <= 15)
                return true;

            return false;
        }

        /// <summary>
        /// Validate email format
        /// </summary>
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validate Thai Tax ID (13 digits)
        /// </summary>
        public static bool IsValidTaxID(string taxId)
        {
            if (string.IsNullOrWhiteSpace(taxId))
                return false;

            // Remove spaces and dashes
            taxId = taxId.Replace(" ", "").Replace("-", "");

            // Must be 13 digits
            if (taxId.Length != 13)
                return false;

            // Must be all numbers
            if (!long.TryParse(taxId, out _))
                return false;

            return true;
        }

        #endregion
    }

    #region Data Transfer Objects

    /// <summary>
    /// Customer data model
    /// </summary>
    public class CustomerData
    {
        public string MobilePhone { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string TaxID { get; set; }
        public string Address { get; set; }
        public string District { get; set; }
        public string Subdistrict { get; set; }
        public string Province { get; set; }
        public string Postcode { get; set; }
        public byte? CustomerTypeID { get; set; }
        public string CustomerTypeName { get; set; }
        public bool Status { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdated { get; set; }
        public int TotalReservations { get; set; }
        public DateTime? LastReservationDate { get; set; }
    }

    /// <summary>
    /// Result of upsert operation
    /// </summary>
    public class CustomerUpsertResult
    {
        public bool Success { get; set; }
        public bool IsNewCustomer { get; set; }
        public string MobilePhone { get; set; }
        public string Message { get; set; }
    }

    #endregion
}
