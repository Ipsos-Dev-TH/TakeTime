using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Class
{
    /// <summary>
    /// Helper class for customer data management and operations
    /// Centralizes customer logic from Product/Default.aspx.cs, Voucher/Default.aspx.cs, Reserve.aspx.cs
    /// </summary>
    public class CustomerHelper
    {
        private readonly code _code;
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of CustomerHelper
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        public CustomerHelper(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _code = new code();
        }

        /// <summary>
        /// Gets customer by mobile phone number
        /// Replaces: fillDataByPhone() from Product/Default.aspx.cs
        /// </summary>
        /// <param name="phone">Mobile phone number</param>
        /// <returns>DataTable with customer information</returns>
        /// <example>
        /// var helper = new CustomerHelper(conn);
        /// DataTable customer = helper.GetCustomerByPhone("0812345678");
        /// </example>
        public DataTable GetCustomerByPhone(string phone)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@Phone", phone ?? "" }
            };

            return _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT Customer.*, Customer_Type.TypeName, " +
                "Address.Province, Address.District, Address.SubDistrict, Address.PostalCode " +
                "FROM Customer " +
                "LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID " +
                "LEFT JOIN Address ON Address.ID = Address_ID " +
                "WHERE MobilePhone = @Phone",
                parameters);
        }

        /// <summary>
        /// Gets customer by Thai national ID number
        /// Replaces: fillDataFromCustomerTable() from Product/Default.aspx.cs
        /// </summary>
        /// <param name="idNumber">Thai national ID number (13 digits)</param>
        /// <returns>DataTable with customer information</returns>
        public DataTable GetCustomerByIdNumber(string idNumber)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@IdNumber", idNumber ?? "" }
            };

            return _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT Customer.*, Customer_Type.TypeName, " +
                "Address.Province, Address.District, Address.SubDistrict, Address.PostalCode " +
                "FROM Customer " +
                "LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID " +
                "LEFT JOIN Address ON Address.ID = Address_ID " +
                "WHERE IDNumber = @IdNumber",
                parameters);
        }

        /// <summary>
        /// Gets customer by ID
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <returns>DataTable with customer information</returns>
        public DataTable GetCustomerById(long customerId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@CustomerID", customerId }
            };

            return _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT Customer.*, Customer_Type.TypeName, " +
                "Address.Province, Address.District, Address.SubDistrict, Address.PostalCode " +
                "FROM Customer " +
                "LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID " +
                "LEFT JOIN Address ON Address.ID = Address_ID " +
                "WHERE Customer.ID = @CustomerID",
                parameters);
        }

        /// <summary>
        /// Searches customers by name or phone
        /// </summary>
        /// <param name="searchTerm">Search term (name, nickname, or phone)</param>
        /// <returns>DataTable with matching customers</returns>
        public DataTable SearchCustomers(string searchTerm)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@SearchTerm", "%" + (searchTerm ?? "") + "%" }
            };

            return _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT TOP 50 Customer.*, Customer_Type.TypeName " +
                "FROM Customer " +
                "LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID " +
                "WHERE Name LIKE @SearchTerm " +
                "OR NickName LIKE @SearchTerm " +
                "OR FullName LIKE @SearchTerm " +
                "OR MobilePhone LIKE @SearchTerm " +
                "ORDER BY Name",
                parameters);
        }

        /// <summary>
        /// Creates or updates customer record (UPSERT)
        /// Delegates to code.UpsertCustomer for secure customer management
        /// </summary>
        /// <param name="data">Customer data object</param>
        /// <returns>Customer ID (existing or newly created)</returns>
        /// <example>
        /// var customerData = new CustomerData
        /// {
        ///     MobilePhone = "0812345678",
        ///     Name = "สมชาย",
        ///     FullName = "นายสมชาย ใจดี",
        ///     Email = "somchai@example.com",
        ///     CustomerTypeId = 1,
        ///     AddressId = 123
        /// };
        /// long customerId = helper.UpsertCustomer(customerData);
        /// </example>
        public long UpsertCustomer(CustomerData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (string.IsNullOrWhiteSpace(data.MobilePhone))
                throw new ArgumentException("Mobile phone is required", nameof(data));

            return _code.UpsertCustomer(
                _connectionString,
                data.MobilePhone,
                data.Name ?? "",
                data.NickName ?? "",
                data.ComeFrom ?? "",
                data.Remark ?? "",
                data.FullName ?? "",
                data.Address ?? "",
                data.IdNumber ?? "",
                data.Email ?? "",
                data.CustomerTypeId,
                data.AddressId,
                data.Address1 ?? "",
                data.BranchNumber ?? ""
            );
        }

        /// <summary>
        /// Checks if a customer exists by phone number
        /// </summary>
        /// <param name="phone">Mobile phone number</param>
        /// <returns>True if customer exists</returns>
        public bool CustomerExistsByPhone(string phone)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@Phone", phone ?? "" }
            };

            DataTable dt = _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT COUNT(*) FROM Customer WHERE MobilePhone = @Phone",
                parameters);

            if (dt.Rows.Count > 0)
            {
                return Convert.ToInt32(dt.Rows[0][0]) > 0;
            }

            return false;
        }

        /// <summary>
        /// Checks if a customer exists by ID number
        /// </summary>
        /// <param name="idNumber">Thai national ID number</param>
        /// <returns>True if customer exists</returns>
        public bool CustomerExistsByIdNumber(string idNumber)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@IdNumber", idNumber ?? "" }
            };

            DataTable dt = _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT COUNT(*) FROM Customer WHERE IDNumber = @IdNumber AND IDNumber IS NOT NULL AND IDNumber != ''",
                parameters);

            if (dt.Rows.Count > 0)
            {
                return Convert.ToInt32(dt.Rows[0][0]) > 0;
            }

            return false;
        }

        /// <summary>
        /// Gets all customer types
        /// </summary>
        /// <returns>DataTable with customer type list</returns>
        public DataTable GetCustomerTypes()
        {
            return _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT * FROM Customer_Type ORDER BY TypeName",
                null);
        }

        /// <summary>
        /// Populates customer type dropdown
        /// </summary>
        /// <param name="dropdown">DropDownList control</param>
        public void PopulateCustomerTypeDropdown(DropDownList dropdown)
        {
            DataTable dt = GetCustomerTypes();

            dropdown.Items.Clear();
            dropdown.Items.Add(new ListItem("-- เลือกประเภท --", "0"));

            foreach (DataRow row in dt.Rows)
            {
                dropdown.Items.Add(new ListItem(
                    row["TypeName"].ToString(),
                    row["ID"].ToString()
                ));
            }
        }

        /// <summary>
        /// Gets customer reservation history
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="limit">Maximum number of records to return</param>
        /// <returns>DataTable with reservation history</returns>
        public DataTable GetCustomerReservations(long customerId, int limit = 10)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@CustomerID", customerId }
            };

            return _code.DatabaseQuerySafe(
                _connectionString,
                _code.AdaptSql($"SELECT TOP {limit} * FROM Reservation " +
                "WHERE Customer_MobilePhone IN (SELECT MobilePhone FROM Customer WHERE ID = @CustomerID) " +
                "ORDER BY CheckinDate DESC"),
                parameters);
        }

        /// <summary>
        /// Gets customer payment history
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="limit">Maximum number of records to return</param>
        /// <returns>DataTable with payment history</returns>
        public DataTable GetCustomerPayments(long customerId, int limit = 10)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@CustomerID", customerId }
            };

            return _code.DatabaseQuerySafe(
                _connectionString,
                _code.AdaptSql($"SELECT TOP {limit} AR.* FROM Account_Receipt AR " +
                "INNER JOIN Reservation R ON AR.Reservation_ID = R.ID " +
                "WHERE R.Customer_MobilePhone IN (SELECT MobilePhone FROM Customer WHERE ID = @CustomerID) " +
                "ORDER BY AR.Date DESC"),
                parameters);
        }

        /// <summary>
        /// Updates customer status (active/inactive)
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="status">Status (1 = active, 0 = inactive)</param>
        /// <returns>Number of rows affected</returns>
        public int UpdateCustomerStatus(long customerId, int status)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@CustomerID", customerId },
                { "@Status", status }
            };

            return _code.DatabaseInsertSafe(
                _connectionString,
                "UPDATE Customer SET Status = @Status WHERE ID = @CustomerID",
                parameters);
        }

        /// <summary>
        /// Adds a remark/note to customer record
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="remark">Remark text</param>
        /// <returns>Number of rows affected</returns>
        public int AddCustomerRemark(long customerId, string remark)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@CustomerID", customerId },
                { "@Remark", remark ?? "" }
            };

            return _code.DatabaseInsertSafe(
                _connectionString,
                "UPDATE Customer SET Remark = @Remark, UpdatedDate = GETDATE() WHERE ID = @CustomerID",
                parameters);
        }

        /// <summary>
        /// Gets customer statistics (total reservations, total spend, last visit)
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <returns>Dictionary with statistics</returns>
        public Dictionary<string, object> GetCustomerStatistics(long customerId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@CustomerID", customerId }
            };

            DataTable dt = _code.DatabaseQuerySafe(
                _connectionString,
                @"SELECT
                    COUNT(DISTINCT R.ID) AS TotalReservations,
                    ISNULL(SUM(AR.TotalAmount), 0) AS TotalSpend,
                    MAX(R.CheckoutDate) AS LastVisit,
                    MIN(R.CheckinDate) AS FirstVisit
                FROM Customer C
                LEFT JOIN Reservation R ON R.Customer_MobilePhone = C.MobilePhone
                LEFT JOIN Account_Receipt AR ON AR.Reservation_ID = R.ID
                WHERE C.ID = @CustomerID
                GROUP BY C.ID",
                parameters);

            var stats = new Dictionary<string, object>();

            if (dt.Rows.Count > 0)
            {
                DataRow row = dt.Rows[0];
                stats["TotalReservations"] = row["TotalReservations"];
                stats["TotalSpend"] = row["TotalSpend"];
                stats["LastVisit"] = row.IsNull("LastVisit") ? null : (DateTime?)row["LastVisit"];
                stats["FirstVisit"] = row.IsNull("FirstVisit") ? null : (DateTime?)row["FirstVisit"];
            }

            return stats;
        }

        /// <summary>
        /// Validates customer data before saving
        /// </summary>
        /// <param name="data">Customer data to validate</param>
        /// <returns>List of validation error messages (empty if valid)</returns>
        public List<string> ValidateCustomerData(CustomerData data)
        {
            var errors = new List<string>();

            if (data == null)
            {
                errors.Add("Customer data is required");
                return errors;
            }

            // Validate mobile phone
            if (string.IsNullOrWhiteSpace(data.MobilePhone))
            {
                errors.Add("Mobile phone number is required");
            }
            else if (!ValidationHelper.IsValidThaiMobilePhone(data.MobilePhone))
            {
                errors.Add("Invalid mobile phone format (must be 10 digits starting with 06, 08, or 09)");
            }

            // Validate email if provided
            if (!string.IsNullOrWhiteSpace(data.Email) && !ValidationHelper.IsValidEmail(data.Email))
            {
                errors.Add("Invalid email format");
            }

            // Validate ID number if provided
            if (!string.IsNullOrWhiteSpace(data.IdNumber) && !ValidationHelper.IsValidThaiIdNumber(data.IdNumber))
            {
                errors.Add("Invalid Thai national ID number (must be 13 digits with valid checksum)");
            }

            // Validate customer type
            if (data.CustomerTypeId <= 0)
            {
                errors.Add("Customer type is required");
            }

            return errors;
        }
    }

    /// <summary>
    /// Data class for customer information
    /// Simplifies customer data passing and validation
    /// </summary>
    public class CustomerData
    {
        public long Id { get; set; }
        public string MobilePhone { get; set; }
        public string Name { get; set; }
        public string NickName { get; set; }
        public string ComeFrom { get; set; }
        public string Remark { get; set; }
        public string FullName { get; set; }
        public string Address { get; set; }
        public string IdNumber { get; set; }
        public string Email { get; set; }
        public int CustomerTypeId { get; set; }
        public int AddressId { get; set; }
        public string Address1 { get; set; }
        public string BranchNumber { get; set; }

        /// <summary>
        /// Creates CustomerData from DataRow
        /// </summary>
        public static CustomerData FromDataRow(DataRow row)
        {
            if (row == null)
                return null;

            return new CustomerData
            {
                Id = row.Table.Columns.Contains("ID") && !row.IsNull("ID")
                    ? Convert.ToInt64(row["ID"]) : 0,
                MobilePhone = row.Table.Columns.Contains("MobilePhone")
                    ? row["MobilePhone"].ToString() : "",
                Name = row.Table.Columns.Contains("Name")
                    ? row["Name"].ToString() : "",
                NickName = row.Table.Columns.Contains("NickName")
                    ? row["NickName"].ToString() : "",
                ComeFrom = row.Table.Columns.Contains("ComeFrom")
                    ? row["ComeFrom"].ToString() : "",
                Remark = row.Table.Columns.Contains("Remark")
                    ? row["Remark"].ToString() : "",
                FullName = row.Table.Columns.Contains("FullName")
                    ? row["FullName"].ToString() : "",
                Address = row.Table.Columns.Contains("Address")
                    ? row["Address"].ToString() : "",
                IdNumber = row.Table.Columns.Contains("IDNumber")
                    ? row["IDNumber"].ToString() : "",
                Email = row.Table.Columns.Contains("Email")
                    ? row["Email"].ToString() : "",
                CustomerTypeId = row.Table.Columns.Contains("Customer_Type_ID") && !row.IsNull("Customer_Type_ID")
                    ? Convert.ToInt32(row["Customer_Type_ID"]) : 0,
                AddressId = row.Table.Columns.Contains("Address_ID") && !row.IsNull("Address_ID")
                    ? Convert.ToInt32(row["Address_ID"]) : 0,
                Address1 = row.Table.Columns.Contains("Address1")
                    ? row["Address1"].ToString() : "",
                BranchNumber = row.Table.Columns.Contains("Branch_Number")
                    ? row["Branch_Number"].ToString() : ""
            };
        }
    }
}
