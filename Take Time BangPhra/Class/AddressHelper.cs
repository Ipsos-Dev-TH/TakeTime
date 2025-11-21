using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Class
{
    /// <summary>
    /// Helper class for Thai address management and postal code operations
    /// Centralizes address logic from Product/Default.aspx.cs, Voucher/Default.aspx.cs, Reserve.aspx.cs
    /// </summary>
    public class AddressHelper
    {
        private readonly code _code;
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of AddressHelper
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        public AddressHelper(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _code = new code();
        }

        /// <summary>
        /// Gets Address.ID from postal code, province, district, and sub-district
        /// Replaces: CheckAddressID() from Product/Default.aspx.cs, Voucher/Default.aspx.cs
        /// </summary>
        /// <param name="postalCode">Thai postal code (5 digits)</param>
        /// <param name="province">Province name</param>
        /// <param name="district">District name</param>
        /// <param name="subDistrict">Sub-district name</param>
        /// <returns>Address ID or null if not found</returns>
        /// <example>
        /// var helper = new AddressHelper(conn);
        /// int? addressId = helper.GetAddressId("10110", "กรุงเทพมหานคร", "บางรัก", "สีลม");
        /// </example>
        public int? GetAddressId(string postalCode, string province, string district, string subDistrict)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@PostalCode", postalCode ?? "" },
                { "@Province", province ?? "" },
                { "@District", district ?? "" },
                { "@SubDistrict", subDistrict ?? "" }
            };

            DataTable dt = _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT ID FROM Address " +
                "WHERE PostalCode = @PostalCode " +
                "AND Province = @Province " +
                "AND District = @District " +
                "AND SubDistrict = @SubDistrict",
                parameters);

            if (dt.Rows.Count > 0)
            {
                return Convert.ToInt32(dt.Rows[0]["ID"]);
            }

            return null;
        }

        /// <summary>
        /// Gets Address.ID from postal code, province, district, and sub-district (string return version)
        /// For backward compatibility with existing code
        /// </summary>
        /// <returns>Address ID as string or empty string if not found</returns>
        public string GetAddressIdString(string postalCode, string province, string district, string subDistrict)
        {
            int? addressId = GetAddressId(postalCode, province, district, subDistrict);
            return addressId?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Gets distinct provinces by postal code
        /// </summary>
        /// <param name="postalCode">Postal code</param>
        /// <returns>List of province names</returns>
        public List<string> GetProvincesByPostalCode(string postalCode)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@PostalCode", postalCode ?? "" }
            };

            DataTable dt = _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT DISTINCT Province FROM Address WHERE PostalCode = @PostalCode ORDER BY Province",
                parameters);

            return DataTableToStringList(dt, "Province");
        }

        /// <summary>
        /// Gets distinct districts by postal code and province
        /// </summary>
        /// <param name="postalCode">Postal code</param>
        /// <param name="province">Province name</param>
        /// <returns>List of district names</returns>
        public List<string> GetDistrictsByPostalCodeAndProvince(string postalCode, string province)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@PostalCode", postalCode ?? "" },
                { "@Province", province ?? "" }
            };

            DataTable dt = _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT DISTINCT District FROM Address " +
                "WHERE PostalCode = @PostalCode AND Province = @Province " +
                "ORDER BY District",
                parameters);

            return DataTableToStringList(dt, "District");
        }

        /// <summary>
        /// Gets distinct sub-districts by postal code, province, and district
        /// </summary>
        /// <param name="postalCode">Postal code</param>
        /// <param name="province">Province name</param>
        /// <param name="district">District name</param>
        /// <returns>List of sub-district names</returns>
        public List<string> GetSubDistrictsByPostalCodeProvinceAndDistrict(
            string postalCode, string province, string district)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@PostalCode", postalCode ?? "" },
                { "@Province", province ?? "" },
                { "@District", district ?? "" }
            };

            DataTable dt = _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT DISTINCT SubDistrict FROM Address " +
                "WHERE PostalCode = @PostalCode AND Province = @Province AND District = @District " +
                "ORDER BY SubDistrict",
                parameters);

            return DataTableToStringList(dt, "SubDistrict");
        }

        /// <summary>
        /// Gets complete address hierarchy by postal code
        /// Replaces: LoadAddressDropdownsByPostalCode() from Product/Default.aspx.cs
        /// </summary>
        /// <param name="postalCode">Postal code</param>
        /// <returns>Dictionary with Provinces, Districts, SubDistricts lists</returns>
        /// <example>
        /// var addressData = helper.GetAddressHierarchyByPostalCode("10110");
        /// List&lt;string&gt; provinces = addressData["Provinces"];
        /// List&lt;string&gt; districts = addressData["Districts"];
        /// List&lt;string&gt; subDistricts = addressData["SubDistricts"];
        /// </example>
        public Dictionary<string, List<string>> GetAddressHierarchyByPostalCode(string postalCode)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@PostalCode", postalCode ?? "" }
            };

            // Get provinces
            DataTable dtProvince = _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT DISTINCT Province FROM Address WHERE PostalCode = @PostalCode ORDER BY Province",
                parameters);

            // Get districts
            DataTable dtDistrict = _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT DISTINCT District FROM Address WHERE PostalCode = @PostalCode ORDER BY District",
                parameters);

            // Get sub-districts
            DataTable dtSubDistrict = _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT DISTINCT SubDistrict FROM Address WHERE PostalCode = @PostalCode ORDER BY SubDistrict",
                parameters);

            return new Dictionary<string, List<string>>
            {
                { "Provinces", DataTableToStringList(dtProvince, "Province") },
                { "Districts", DataTableToStringList(dtDistrict, "District") },
                { "SubDistricts", DataTableToStringList(dtSubDistrict, "SubDistrict") }
            };
        }

        /// <summary>
        /// Populates DropDownList controls with address data by postal code
        /// Replaces: getAddress() from Product/Default.aspx.cs, Voucher/Default.aspx.cs
        /// </summary>
        /// <param name="postalCode">Postal code</param>
        /// <param name="provinceDropdown">Province dropdown control</param>
        /// <param name="districtDropdown">District dropdown control</param>
        /// <param name="subDistrictDropdown">Sub-district dropdown control</param>
        /// <example>
        /// var helper = new AddressHelper(conn);
        /// helper.PopulateAddressDropdowns("10110", DropDownList5, DropDownList6, DropDownList7);
        /// </example>
        public void PopulateAddressDropdowns(
            string postalCode,
            DropDownList provinceDropdown,
            DropDownList districtDropdown,
            DropDownList subDistrictDropdown)
        {
            var addressData = GetAddressHierarchyByPostalCode(postalCode);

            // Clear and populate Province dropdown
            provinceDropdown.Items.Clear();
            provinceDropdown.Items.Add(new ListItem("-- เลือกจังหวัด --", ""));
            foreach (string province in addressData["Provinces"])
            {
                provinceDropdown.Items.Add(new ListItem(province, province));
            }

            // Clear and populate District dropdown
            districtDropdown.Items.Clear();
            districtDropdown.Items.Add(new ListItem("-- เลือกอำเภอ/เขต --", ""));
            foreach (string district in addressData["Districts"])
            {
                districtDropdown.Items.Add(new ListItem(district, district));
            }

            // Clear and populate SubDistrict dropdown
            subDistrictDropdown.Items.Clear();
            subDistrictDropdown.Items.Add(new ListItem("-- เลือกตำบล/แขวง --", ""));
            foreach (string subDistrict in addressData["SubDistricts"])
            {
                subDistrictDropdown.Items.Add(new ListItem(subDistrict, subDistrict));
            }
        }

        /// <summary>
        /// Gets complete address information by Address ID
        /// </summary>
        /// <param name="addressId">Address ID</param>
        /// <returns>DataTable with address details</returns>
        public DataTable GetAddressById(int addressId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@AddressID", addressId }
            };

            return _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT * FROM Address WHERE ID = @AddressID",
                parameters);
        }

        /// <summary>
        /// Searches addresses by partial postal code
        /// </summary>
        /// <param name="partialPostalCode">Partial postal code (e.g., "101")</param>
        /// <returns>DataTable with matching addresses</returns>
        public DataTable SearchAddressesByPostalCode(string partialPostalCode)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@PostalCodePattern", partialPostalCode + "%" }
            };

            return _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT DISTINCT PostalCode, Province, District, SubDistrict " +
                "FROM Address " +
                "WHERE PostalCode LIKE @PostalCodePattern " +
                "ORDER BY PostalCode, Province, District, SubDistrict",
                parameters);
        }

        /// <summary>
        /// Validates if a postal code exists in the database
        /// </summary>
        /// <param name="postalCode">Postal code to validate</param>
        /// <returns>True if postal code exists</returns>
        public bool PostalCodeExists(string postalCode)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@PostalCode", postalCode ?? "" }
            };

            DataTable dt = _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT COUNT(*) FROM Address WHERE PostalCode = @PostalCode",
                parameters);

            if (dt.Rows.Count > 0)
            {
                return Convert.ToInt32(dt.Rows[0][0]) > 0;
            }

            return false;
        }

        /// <summary>
        /// Gets all provinces in Thailand
        /// </summary>
        /// <returns>List of all province names</returns>
        public List<string> GetAllProvinces()
        {
            DataTable dt = _code.DatabaseQuerySafe(
                _connectionString,
                "SELECT DISTINCT Province FROM Address ORDER BY Province",
                null);

            return DataTableToStringList(dt, "Province");
        }

        /// <summary>
        /// Gets formatted address string
        /// </summary>
        /// <param name="houseNumber">House/building number</param>
        /// <param name="subDistrict">Sub-district</param>
        /// <param name="district">District</param>
        /// <param name="province">Province</param>
        /// <param name="postalCode">Postal code</param>
        /// <returns>Formatted Thai address</returns>
        /// <example>
        /// string fullAddress = helper.FormatAddress("123/45", "สีลม", "บางรัก", "กรุงเทพมหานคร", "10110");
        /// // Returns: "123/45 ต.สีลม อ.บางรัก จ.กรุงเทพมหานคร 10110"
        /// </example>
        public string FormatAddress(
            string houseNumber,
            string subDistrict,
            string district,
            string province,
            string postalCode)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(houseNumber))
                parts.Add(houseNumber);

            if (!string.IsNullOrWhiteSpace(subDistrict))
                parts.Add("ต." + subDistrict);

            if (!string.IsNullOrWhiteSpace(district))
                parts.Add("อ." + district);

            if (!string.IsNullOrWhiteSpace(province))
                parts.Add("จ." + province);

            if (!string.IsNullOrWhiteSpace(postalCode))
                parts.Add(postalCode);

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Converts DataTable column to List of strings
        /// Helper method for internal use
        /// </summary>
        private List<string> DataTableToStringList(DataTable dt, string columnName)
        {
            var list = new List<string>();

            if (dt == null || dt.Rows.Count == 0)
                return list;

            foreach (DataRow row in dt.Rows)
            {
                if (!row.IsNull(columnName))
                {
                    string value = row[columnName].ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        list.Add(value);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Inserts a new address if it doesn't exist
        /// </summary>
        /// <param name="postalCode">Postal code</param>
        /// <param name="province">Province</param>
        /// <param name="district">District</param>
        /// <param name="subDistrict">Sub-district</param>
        /// <returns>Address ID (existing or newly created)</returns>
        public int UpsertAddress(string postalCode, string province, string district, string subDistrict)
        {
            // Check if address exists
            int? existingId = GetAddressId(postalCode, province, district, subDistrict);

            if (existingId.HasValue)
            {
                return existingId.Value;
            }

            // Insert new address
            var insertParams = new Dictionary<string, object>
            {
                { "@PostalCode", postalCode ?? "" },
                { "@Province", province ?? "" },
                { "@District", district ?? "" },
                { "@SubDistrict", subDistrict ?? "" }
            };

            return _code.DatabaseInsertReturnSafe(
                _connectionString,
                "INSERT INTO Address (PostalCode, Province, District, SubDistrict) " +
                "VALUES (@PostalCode, @Province, @District, @SubDistrict); " +
                "SELECT SCOPE_IDENTITY();",
                insertParams);
        }
    }
}
