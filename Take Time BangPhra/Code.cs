using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Configuration;
using System.Net;
using System.Data;
using System.IO;
using Take_Time_BangPhra;
using System.Text;
using Line.Messaging;
using Line.Messaging.Webhooks;
using System.Threading.Tasks;
using Microsoft.Reporting.Map.WebForms.BingMaps;
using System.Globalization;
using System.Text.RegularExpressions;
using Npgsql;

namespace Take_Time_BangPhra
{
    public class code
    {
        public string status_new = "NEW";
        public string status_approved = "APPROVED";
        public string status_declined = "DECLINED";
        public string status_preapproved = "PRE-APPROVED";
        public string status_predeclined = "PRE-DECLINED";
        public string status_deleted = "DELETED";

        public DateTime? ParseDate(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return null;

            DateTime result;

            // Use Gregorian calendar for consistent date handling
            // Database stores Christian year (2025), NOT Buddhist year (2568)
            // Using th-TH with default calendar would cause year conversion issues
            CultureInfo culture = new CultureInfo("th-TH");
            culture.DateTimeFormat.Calendar = new System.Globalization.GregorianCalendar();

            // Define date formats - THAI FORMAT FIRST (dd-MM-yyyy, dd/MM/yyyy)
            // This ensures Thai date format (day-month-year) is prioritized over US format
            // Prevents date/month confusion (e.g., "01-02-2025" = 1 Feb, not 2 Jan)
            string[] formats = {
        // Thai/European format (Day-Month-Year) - PRIORITY
        "dd-MM-yyyy", "d-MM-yyyy", "dd-M-yyyy", "d-M-yyyy",
        "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy", "d/M/yyyy",
        // ISO format (safe, unambiguous)
        "yyyy-MM-dd", "yyyy-M-d",
        // US format (Month-Day-Year) - LAST RESORT ONLY
        "MM-dd-yyyy", "M-dd-yyyy", "MM-d-yyyy", "M-d-yyyy",
        "MM/dd/yyyy", "M/dd/yyyy", "MM/d/yyyy", "M/d/yyyy"
    };

            // Try parsing with Gregorian calendar
            if (DateTime.TryParseExact(dateString, formats,
                culture, DateTimeStyles.None, out result))
            {
                return result;
            }

            // Fallback: Try parsing with Gregorian calendar
            if (DateTime.TryParse(dateString, culture, DateTimeStyles.None, out result))
            {
                return result;
            }

            return null;
        }

        public int CheckPermission(string conn, string colmnname, string Email)
        {
            int check = 0;

            var parameters = new Dictionary<string, object>
            {
                { "@Email", Email?.ToLower() ?? "" }
            };

            DataTable dt = DatabaseQuerySafe(conn,
                "SELECT * FROM [Payment].[dbo].[User] " +
                "INNER JOIN [Role] ON Role_ID = [Role].ID " +
                "WHERE [User] = @Email",
                parameters);
            try
            {
                if (dt.Rows.Count > 0)
                {
                    check = Convert.ToInt32(dt.Rows[0][colmnname].ToString());
                }
                else
                {
                    check = -1;
                }
            }
            catch { check = -1; }
            return check;

        }

        /// <summary>
        /// [DEPRECATED] Use DatabaseQuerySafe instead to prevent SQL Injection
        /// Legacy method - kept for backward compatibility
        /// </summary>
        public DataTable DatabaseQuery(string connStr, string cmd)
        {
            DataTable dt = new DataTable();
            string adaptedCmd = code.AdaptSql(cmd);

            string dbType = ConfigurationManager.AppSettings["DatabaseType"] ?? "MSSQL";

            try
            {
                adaptedCmd = adaptedCmd.Replace("&amp;", "&");
                adaptedCmd = adaptedCmd.Replace("&#39;", "''");
                adaptedCmd = adaptedCmd.Replace("&#160;", "");

                if (dbType.ToUpper() == "POSTGRESQL")
                {
                    using (NpgsqlConnection con = new NpgsqlConnection(connStr))
                    {
                        con.Open();
                        using (NpgsqlCommand command = new NpgsqlCommand(adaptedCmd, con))
                        {
                            using (NpgsqlDataAdapter adapter = new NpgsqlDataAdapter(command))
                            {
                                adapter.Fill(dt);
                            }
                        }
                    }
                }
                else // MSSQL
                {
                    using (SqlConnection con = new SqlConnection(connStr))
                    {
                        con.Open();
                        using (SqlCommand command = new SqlCommand(adaptedCmd, con))
                        {
                            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                            {
                                adapter.Fill(dt);
                            }
                        }
                    }
                }
            }
            catch { }

            return dt;
        }

        /// <summary>
        /// SECURE: Execute SQL query with parameterized values to prevent SQL Injection
        /// </summary>
        /// <param name="connStr">Connection string</param>
        /// <param name="query">SQL query with @param1, @param2 placeholders</param>
        /// <param name="parameters">Dictionary of parameters: key = "@param1", value = actual value</param>
        /// <returns>DataTable with results</returns>
        /// <example>
        /// var parameters = new Dictionary&lt;string, object&gt; {
        ///     { "@phone", TextBox1.Text },
        ///     { "@id", reservationId }
        /// };
        /// var dt = DatabaseQuerySafe(conn, "SELECT * FROM Reservation WHERE Customer_MobilePhone = @phone AND ID = @id", parameters);
        /// </example>
        public DataTable DatabaseQuerySafe(string connStr, string query, Dictionary<string, object> parameters = null)
        {
            DataTable dt = new DataTable();
            string dbType = ConfigurationManager.AppSettings["DatabaseType"] ?? "MSSQL";

            try
            {
                if (dbType.ToUpper() == "POSTGRESQL")
                {
                    using (NpgsqlConnection con = new NpgsqlConnection(connStr))
                    {
                        con.Open();
                        using (NpgsqlCommand cmd = new NpgsqlCommand(query, con))
                        {
                            if (parameters != null)
                            {
                                foreach (var param in parameters)
                                {
                                    cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                                }
                            }
                            using (NpgsqlDataAdapter adapter = new NpgsqlDataAdapter(cmd))
                            {
                                adapter.Fill(dt);
                            }
                        }
                    }
                }
                else // MSSQL
                {
                    using (SqlConnection con = new SqlConnection(connStr))
                    {
                        con.Open();
                        using (SqlCommand cmd = new SqlCommand(query, con))
                        {
                            if (parameters != null)
                            {
                                foreach (var param in parameters)
                                {
                                    cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                                }
                            }
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                adapter.Fill(dt);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error for debugging
                System.Diagnostics.Trace.TraceError($"DatabaseQuerySafe Error: {ex.Message}\nQuery: {query}");
                throw; // Re-throw to allow proper error handling upstream
            }

            return dt;
        }

        /// <summary>
        /// [DEPRECATED] Use DatabaseInsertSafe instead to prevent SQL Injection
        /// Legacy method - kept for backward compatibility
        /// </summary>
        public void DatabaseInsert(string connStr, string cmd)
        {
            string adaptedCmd = code.AdaptSql(cmd);
            string dbType = ConfigurationManager.AppSettings["DatabaseType"] ?? "MSSQL";

            if (dbType.ToUpper() == "POSTGRESQL")
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(connStr))
                {
                    connection.Open();
                    using (NpgsqlCommand command = new NpgsqlCommand(adaptedCmd, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            else // MSSQL
            {
                using (SqlConnection connection = new SqlConnection(connStr))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(adaptedCmd, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// SECURE: Execute INSERT/UPDATE/DELETE with parameterized values to prevent SQL Injection
        /// </summary>
        /// <param name="connStr">Connection string</param>
        /// <param name="query">SQL command with @param1, @param2 placeholders</param>
        /// <param name="parameters">Dictionary of parameters</param>
        /// <returns>Number of rows affected</returns>
        /// <example>
        /// var parameters = new Dictionary&lt;string, object&gt; {
        ///     { "@phone", TextBox1.Text },
        ///     { "@name", TextBox2.Text },
        ///     { "@email", TextBox3.Text }
        /// };
        /// DatabaseInsertSafe(conn, "UPDATE Customer SET Name = @name, Email = @email WHERE MobilePhone = @phone", parameters);
        /// </example>
        public int DatabaseInsertSafe(string connStr, string query, Dictionary<string, object> parameters = null)
        {
            string dbType = ConfigurationManager.AppSettings["DatabaseType"] ?? "MSSQL";
            int rowsAffected = 0;

            try
            {
                if (dbType.ToUpper() == "POSTGRESQL")
                {
                    using (NpgsqlConnection connection = new NpgsqlConnection(connStr))
                    {
                        connection.Open();
                        using (NpgsqlCommand cmd = new NpgsqlCommand(query, connection))
                        {
                            if (parameters != null)
                            {
                                foreach (var param in parameters)
                                {
                                    cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                                }
                            }
                            rowsAffected = cmd.ExecuteNonQuery();
                        }
                    }
                }
                else // MSSQL
                {
                    using (SqlConnection connection = new SqlConnection(connStr))
                    {
                        connection.Open();
                        using (SqlCommand cmd = new SqlCommand(query, connection))
                        {
                            if (parameters != null)
                            {
                                foreach (var param in parameters)
                                {
                                    cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                                }
                            }
                            rowsAffected = cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"DatabaseInsertSafe Error: {ex.Message}\nQuery: {query}");
                throw;
            }

            return rowsAffected;
        }

        /// <summary>
        /// [DEPRECATED] Use DatabaseInsertReturnSafe instead to prevent SQL Injection
        /// Legacy method - kept for backward compatibility
        /// </summary>
        public int DatabaseInsertReturn(string connStr, string cmd)
        {
            string adaptedCmd = code.AdaptSql(cmd);
            string dbType = ConfigurationManager.AppSettings["DatabaseType"] ?? "MSSQL";
            int result = 0;

            if (dbType.ToUpper() == "POSTGRESQL")
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(connStr))
                {
                    connection.Open();
                    using (NpgsqlCommand command = new NpgsqlCommand(adaptedCmd, connection))
                    {
                        object scalarResult = command.ExecuteScalar();
                        if (scalarResult != null)
                        {
                            result = Convert.ToInt32(scalarResult);
                        }
                    }
                }
            }
            else // MSSQL
            {
                using (SqlConnection connection = new SqlConnection(connStr))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(adaptedCmd, connection))
                    {
                        object scalarResult = command.ExecuteScalar();
                        if (scalarResult != null)
                        {
                            result = Convert.ToInt32(scalarResult);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// SECURE: Execute INSERT and return the new ID with parameterized values to prevent SQL Injection
        /// </summary>
        /// <param name="connStr">Connection string</param>
        /// <param name="query">INSERT query with @param placeholders. Must include SCOPE_IDENTITY() or RETURNING</param>
        /// <param name="parameters">Dictionary of parameters</param>
        /// <returns>New record ID</returns>
        /// <example>
        /// var parameters = new Dictionary&lt;string, object&gt; {
        ///     { "@phone", "0812345678" },
        ///     { "@name", "John Doe" },
        ///     { "@email", "john@example.com" }
        /// };
        /// int newId = DatabaseInsertReturnSafe(conn,
        ///     "INSERT INTO Customer (MobilePhone, Name, Email) VALUES (@phone, @name, @email); SELECT SCOPE_IDENTITY();",
        ///     parameters);
        /// </example>
        public int DatabaseInsertReturnSafe(string connStr, string query, Dictionary<string, object> parameters = null)
        {
            string dbType = ConfigurationManager.AppSettings["DatabaseType"] ?? "MSSQL";
            int result = 0;

            try
            {
                if (dbType.ToUpper() == "POSTGRESQL")
                {
                    using (NpgsqlConnection connection = new NpgsqlConnection(connStr))
                    {
                        connection.Open();
                        using (NpgsqlCommand cmd = new NpgsqlCommand(query, connection))
                        {
                            if (parameters != null)
                            {
                                foreach (var param in parameters)
                                {
                                    cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                                }
                            }
                            object scalarResult = cmd.ExecuteScalar();
                            if (scalarResult != null && scalarResult != DBNull.Value)
                            {
                                result = Convert.ToInt32(scalarResult);
                            }
                        }
                    }
                }
                else // MSSQL
                {
                    using (SqlConnection connection = new SqlConnection(connStr))
                    {
                        connection.Open();
                        using (SqlCommand cmd = new SqlCommand(query, connection))
                        {
                            if (parameters != null)
                            {
                                foreach (var param in parameters)
                                {
                                    cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                                }
                            }
                            object scalarResult = cmd.ExecuteScalar();
                            if (scalarResult != null && scalarResult != DBNull.Value)
                            {
                                result = Convert.ToInt32(scalarResult);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"DatabaseInsertReturnSafe Error: {ex.Message}\nQuery: {query}");
                throw;
            }

            return result;
        }

        /// <summary>
        /// SECURE: Upserts customer data using parameterized queries - inserts new customer or updates existing one
        /// Prevents duplicates and ensures latest data is always stored
        /// ALWAYS matches by MobilePhone (unique identifier) - ensures only 1 record per phone number
        /// If customer changes from Individual to Corporate (or vice versa), it updates the existing record
        /// </summary>
        /// <returns>Customer ID</returns>
        public long UpsertCustomer(
            string connStr,
            string mobilePhone,
            string name,
            string nickName,
            string comeFrom,
            string remark,
            string fullName,
            string address,
            string idNumber,
            string email,
            int customerTypeID,
            int addressID,
            string address1,
            string branchNumber)
        {
            string dbType = ConfigurationManager.AppSettings["DatabaseType"] ?? "MSSQL";
            long customerId = 0;

            // Create parameters dictionary
            var parameters = new Dictionary<string, object>
            {
                { "@MobilePhone", mobilePhone ?? "" },
                { "@Name", name ?? "" },
                { "@NickName", nickName ?? "" },
                { "@ComeFrom", comeFrom ?? "" },
                { "@Remark", remark ?? "" },
                { "@FullName", fullName ?? "" },
                { "@Address", address ?? "" },
                { "@IDNumber", idNumber ?? "" },
                { "@Email", email ?? "" },
                { "@CustomerTypeID", customerTypeID },
                { "@AddressID", addressID },
                { "@Address1", address1 ?? "" },
                { "@BranchNumber", branchNumber ?? "" }
            };

            if (dbType.ToUpper() == "POSTGRESQL")
            {
                // PostgreSQL version using INSERT ... ON CONFLICT with parameterized queries
                string mergeQuery = @"
                    INSERT INTO Customer (MobilePhone, Name, NickName, ComeFrom, Remark, FullName, Address, IDNumber, Email, Customer_Type_ID, Address_ID, Address1, Branch_Number, Status)
                    VALUES (@MobilePhone, @Name, @NickName, @ComeFrom, @Remark, @FullName, @Address, @IDNumber, @Email, @CustomerTypeID, @AddressID, @Address1, @BranchNumber, 1)
                    ON CONFLICT (MobilePhone)
                    DO UPDATE SET
                        Name = EXCLUDED.Name,
                        NickName = EXCLUDED.NickName,
                        ComeFrom = EXCLUDED.ComeFrom,
                        Remark = EXCLUDED.Remark,
                        FullName = EXCLUDED.FullName,
                        Address = EXCLUDED.Address,
                        IDNumber = EXCLUDED.IDNumber,
                        Email = EXCLUDED.Email,
                        Customer_Type_ID = EXCLUDED.Customer_Type_ID,
                        Address_ID = EXCLUDED.Address_ID,
                        Address1 = EXCLUDED.Address1,
                        Branch_Number = EXCLUDED.Branch_Number,
                        Status = 1
                    RETURNING ID";

                using (NpgsqlConnection connection = new NpgsqlConnection(connStr))
                {
                    connection.Open();
                    using (NpgsqlCommand command = new NpgsqlCommand(mergeQuery, connection))
                    {
                        foreach (var param in parameters)
                        {
                            command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                        object result = command.ExecuteScalar();
                        if (result != null)
                        {
                            customerId = Convert.ToInt64(result);
                        }
                    }
                }
            }
            else // MSSQL
            {
                // SQL Server version using MERGE statement with parameterized queries
                string mergeQuery = @"
                    MERGE INTO Customer AS target
                    USING (SELECT
                        @MobilePhone AS MobilePhone,
                        @Name AS Name,
                        @NickName AS NickName,
                        @ComeFrom AS ComeFrom,
                        @Remark AS Remark,
                        @FullName AS FullName,
                        @Address AS Address,
                        @IDNumber AS IDNumber,
                        @Email AS Email,
                        @CustomerTypeID AS Customer_Type_ID,
                        @AddressID AS Address_ID,
                        @Address1 AS Address1,
                        @BranchNumber AS Branch_Number
                    ) AS source
                    ON (target.MobilePhone = source.MobilePhone)
                    WHEN MATCHED THEN
                        UPDATE SET
                            Name = source.Name,
                            NickName = source.NickName,
                            ComeFrom = source.ComeFrom,
                            Remark = source.Remark,
                            FullName = source.FullName,
                            Address = source.Address,
                            IDNumber = source.IDNumber,
                            Email = source.Email,
                            Customer_Type_ID = source.Customer_Type_ID,
                            Address_ID = source.Address_ID,
                            Address1 = source.Address1,
                            Branch_Number = source.Branch_Number,
                            Status = 1
                    WHEN NOT MATCHED THEN
                        INSERT (MobilePhone, Name, NickName, ComeFrom, Remark, FullName, Address, IDNumber, Email, Customer_Type_ID, Address_ID, Address1, Branch_Number, Status)
                        VALUES (source.MobilePhone, source.Name, source.NickName, source.ComeFrom, source.Remark, source.FullName, source.Address, source.IDNumber, source.Email, source.Customer_Type_ID, source.Address_ID, source.Address1, source.Branch_Number, 1)
                    OUTPUT INSERTED.ID;";

                using (SqlConnection connection = new SqlConnection(connStr))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(mergeQuery, connection))
                    {
                        foreach (var param in parameters)
                        {
                            command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                        object result = command.ExecuteScalar();
                        if (result != null)
                        {
                            customerId = Convert.ToInt64(result);
                        }
                    }
                }
            }

            // Hook กลาง: ทุกจุดที่บันทึก/แก้ข้อมูลลูกค้าผ่านเมธอดนี้ (จอง/เช็คอิน/เช็คเอาท์/ใบเสร็จ/
            // ใบสำคัญจ่าย/ขายสินค้า) → enqueue sync ผู้ติดต่อไป NextAcc ให้ contact ตรงกับระบบเสมอ
            // (background queue — ไม่บล็อกหน้าเว็บ; TryEnqueue กลืน error ทุกชนิด ห้ามพังการเซฟลูกค้า)
            if (customerId > 0)
                Take_Time_BangPhra.Integration.AccountingSyncService.TryEnqueueCustomerContactSync(connStr, mobilePhone);

            return customerId;
        }

        string IPAddress = "";

        public string GetIPAddress()
        {
            try
            {
                IPHostEntry Host = default(IPHostEntry);
                string Hostname = null;
                Hostname = System.Environment.MachineName;
                Host = Dns.GetHostEntry(Hostname);
                foreach (IPAddress IP in Host.AddressList)
                {
                    if (IP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        IPAddress = Convert.ToString(IP);
                    }
                }
            }
            catch { }
            return IPAddress;
        }

        public void Logs(string connStr, string action, string detail, string logby)
        {
            string cmd = "INSERT INTO Logs(LogDateTime, LogAction, LogDetail, LogBy, LogFromComputerName, LogFromIP) VALUES (@a, @b, @c, @d, @e, @f)";
            string dbType = ConfigurationManager.AppSettings["DatabaseType"] ?? "MSSQL";

            if (dbType.ToUpper() == "POSTGRESQL")
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(connStr))
                {
                    connection.Open();
                    using (NpgsqlCommand command = new NpgsqlCommand(cmd, connection))
                    {
                        command.Parameters.AddWithValue("a", DateTime.Now);
                        command.Parameters.AddWithValue("b", action);
                        command.Parameters.AddWithValue("c", detail);
                        command.Parameters.AddWithValue("d", logby);
                        try
                        {
                            command.Parameters.AddWithValue("e", System.Net.Dns.GetHostEntry(HttpContext.Current.Request.UserHostName.ToString()).HostName);
                            command.Parameters.AddWithValue("f", HttpContext.Current.Request.UserHostName.ToString());
                        }
                        catch
                        {
                            command.Parameters.AddWithValue("e", "Online");
                            command.Parameters.AddWithValue("f", "Online");
                        }
                        command.ExecuteNonQuery();
                    }
                }
            }
            else // MSSQL
            {
                using (SqlConnection connection = new SqlConnection(connStr))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(cmd, connection))
                    {
                        command.Parameters.AddWithValue("@a", DateTime.Now);
                        command.Parameters.AddWithValue("@b", action);
                        command.Parameters.AddWithValue("@c", detail);
                        command.Parameters.AddWithValue("@d", logby);
                        try
                        {
                            command.Parameters.AddWithValue("@e", System.Net.Dns.GetHostEntry(HttpContext.Current.Request.UserHostName.ToString()).HostName);
                            command.Parameters.AddWithValue("@f", HttpContext.Current.Request.UserHostName.ToString());
                        }
                        catch
                        {
                            command.Parameters.AddWithValue("@e", "Online");
                            command.Parameters.AddWithValue("@f", "Online");
                        }
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public static string AdaptSql(string sql)
        {
            string dbType = ConfigurationManager.AppSettings["DatabaseType"] ?? "MSSQL";
            switch (dbType.ToUpper())
            {
                case "POSTGRESQL":
                    return AdaptToPostgreSQL(sql);
                default:
                    return sql; // For MSSQL, return the original SQL
            }
        }

        private static string AdaptToPostgreSQL(string sql)
        {
            // Replace 'SELECT SCOPE_IDENTITY();' with 'RETURNING id;'
            sql = Regex.Replace(sql, @"SELECT SCOPE_IDENTITY\(\);", "RETURNING id;", RegexOptions.IgnoreCase);

            // Replace 'INSERT INTO ... SELECT SCOPE_IDENTITY();' with 'INSERT INTO ... RETURNING id;'
            sql = Regex.Replace(sql, @"INSERT INTO\s+(.+?)\s+SELECT SCOPE_IDENTITY\(\);", "INSERT INTO $1 RETURNING id;", RegexOptions.IgnoreCase);

            // Replace TOP with LIMIT
            sql = Regex.Replace(sql, @"TOP\s+(\d+)", "LIMIT $1", RegexOptions.IgnoreCase);

            // Replace 'DELETE FROM ... WHERE' with 'DELETE FROM ... WHERE ... RETURNING *;'
            sql = Regex.Replace(sql, @"DELETE FROM\s+(.+?)\s+WHERE\s+(.+)", "DELETE FROM $1 WHERE $2 RETURNING *;", RegexOptions.IgnoreCase);

            // Replace 'N'' with just '''
            sql = sql.Replace("N'", "'");

            // Replace 'ISNULL' with 'COALESCE'
            sql = sql.Replace("ISNULL(", "COALESCE(");

            // Replace 'GETDATE()' with 'CURRENT_TIMESTAMP'
            sql = sql.Replace("GETDATE()", "CURRENT_TIMESTAMP");

            // Replace 'NEWID()' with 'uuid_generate_v4()' (requires uuid-ossp extension)
            sql = sql.Replace("NEWID()", "uuid_generate_v4()");

            // Replace 'DATEADD' with date arithmetic
            sql = Regex.Replace(sql, @"DATEADD\(([^,]+),\s*(\d+),\s*([^)]+)\)", "($3 + INTERVAL '$2 $1')", RegexOptions.IgnoreCase);

            // Replace 'DATEDIFF' with date arithmetic
            sql = Regex.Replace(sql, @"DATEDIFF\(([^,]+),\s*([^,]+),\s*([^)]+)\)", "EXTRACT(EPOCH FROM ($3 - $2) / CASE WHEN LOWER($1) = 'day' THEN 86400 WHEN LOWER($1) = 'hour' THEN 3600 WHEN LOWER($1) = 'minute' THEN 60 ELSE 1 END)::INTEGER", RegexOptions.IgnoreCase);

            // Replace 'CONVERT' with 'CAST'
            sql = Regex.Replace(sql, @"CONVERT\(([^,]+),\s*([^)]+)\)", "CAST($2 AS $1)", RegexOptions.IgnoreCase);

            // Replace 'LIKE' with 'ILIKE' for case-insensitive matches
            sql = sql.Replace("LIKE", "ILIKE");

            // Replace 'IDENTITY(1,1)' with 'SERIAL'
            sql = sql.Replace("IDENTITY(1,1)", "SERIAL");

            // Replace 'SET IDENTITY_INSERT' with a comment (PostgreSQL does not support this)
            sql = Regex.Replace(sql, @"SET IDENTITY_INSERT\s+(.+?)\s+(ON|OFF)", "-- SET IDENTITY_INSERT $1 $2", RegexOptions.IgnoreCase);

            // Replace 'RAISERROR' with 'RAISE EXCEPTION'
            sql = Regex.Replace(sql, @"RAISERROR\s*\(\s*'([^']*)'\s*,\s*\d+\s*,\s*\d+\s*\)", "RAISE EXCEPTION '$1'", RegexOptions.IgnoreCase);

            // Replace 'TRY_CAST' with 'CAST'
            sql = Regex.Replace(sql, @"TRY_CAST\(([^,]+)\s+AS\s+([^)]+)\)", "CAST($1 AS $2)", RegexOptions.IgnoreCase);

            // Replace 'WITH(NOLOCK)' with a comment (PostgreSQL does not support this)
            sql = Regex.Replace(sql, @"WITH\s*\(NOLOCK\)", "-- WITH(NOLOCK)", RegexOptions.IgnoreCase);

            // Replace 'DECLARE @variable datatype' with a comment (PostgreSQL uses different syntax for variables)
            sql = Regex.Replace(sql, @"DECLARE\s+@(\w+)\s+(\w+)", "-- DECLARE $1 $2", RegexOptions.IgnoreCase);

            // Replace 'SET @variable = value' with a comment
            sql = Regex.Replace(sql, @"SET\s+@(\w+)\s*=\s*(.+)", "-- SET $1 = $2", RegexOptions.IgnoreCase);

            // Replace 'BEGIN TRANSACTION' and 'COMMIT TRANSACTION' with 'BEGIN' and 'COMMIT'
            sql = sql.Replace("BEGIN TRANSACTION", "BEGIN");
            sql = sql.Replace("COMMIT TRANSACTION", "COMMIT");

            // Replace 'ROLLBACK TRANSACTION' with 'ROLLBACK'
            sql = sql.Replace("ROLLBACK TRANSACTION", "ROLLBACK");

            // Replace 'NVARCHAR' with 'VARCHAR'
            sql = sql.Replace("NVARCHAR", "VARCHAR");

            // Replace 'DATETIME' with 'TIMESTAMP'
            sql = sql.Replace("DATETIME", "TIMESTAMP");

            // Replace 'BIT' with 'BOOLEAN'
            sql = sql.Replace("BIT", "BOOLEAN");

            // Replace 'MONEY' with 'NUMERIC(19,4)'
            sql = sql.Replace("MONEY", "NUMERIC(19,4)");

            // Replace 'TEXT' with 'TEXT' (same in both, but added for completeness)
            sql = sql.Replace("TEXT", "TEXT");

            // Replace 'VARCHAR(MAX)' with 'TEXT'
            sql = sql.Replace("VARCHAR(MAX)", "TEXT");

            // Replace 'NVARCHAR(MAX)' with 'TEXT'
            sql = sql.Replace("NVARCHAR(MAX)", "TEXT");

            // Replace '%%' with '%' (SQL Server uses %% for LIKE, PostgreSQL uses %)
            sql = sql.Replace("%%", "%");

            // Replace 'LEN' with 'LENGTH'
            sql = sql.Replace("LEN(", "LENGTH(");

            // Replace 'CHARINDEX' with 'POSITION'
            sql = Regex.Replace(sql, @"CHARINDEX\(([^,]+),\s*([^)]+)\)", "POSITION($1 IN $2)", RegexOptions.IgnoreCase);

            // Replace 'SUBSTRING' with 'SUBSTR'
            sql = Regex.Replace(sql, @"SUBSTRING\(([^,]+),\s*(\d+),\s*(\d+)\)", "SUBSTR($1, $2, $3)", RegexOptions.IgnoreCase);

            return sql;
        }

        public void SendEmail(string from, string to, string cc, string subject, string body, Attachment[] data)
        {
            MailMessage mail = new MailMessage(from, to);
            SmtpClient client = new SmtpClient();
            client.Port = 25;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Host = "pmta.extranet.iext";
            try
            {
                mail.CC.Add(cc);
            }
            catch { }
            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = true;
            try
            {
                for (int i = 0; i < data.Length; i++)
                {
                    mail.Attachments.Add(data[i]);
                }

            }
            catch { }
            client.Send(mail);
        }

        public string Crypt(string text)
        {
            return Convert.ToBase64String((Encoding.Unicode.GetBytes(text)));
        }

        public string Derypt(string text)
        {
            return Encoding.Unicode.GetString((Convert.FromBase64String(text)));
        }

        public int CheckProjectCostQuantity(string conn, string Project_Cost_ID, DataTable dtInput, string quantitycolumnname)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@ProjectCostID", Project_Cost_ID }
            };

            DataTable dtQuantityLimit = DatabaseQuerySafe(conn,
                "SELECT * FROM [Payment].[dbo].[Project_Cost] WHERE ID = @ProjectCostID",
                parameters);

            var paymentParams = new Dictionary<string, object>
            {
                { "@ProjectCostID", Project_Cost_ID },
                { "@StatusDeleted", status_deleted },
                { "@StatusDeclined", status_declined },
                { "@StatusPreDeclined", status_predeclined }
            };

            DataTable dtPayment = DatabaseQuerySafe(conn,
                "SELECT * FROM [Payment].[dbo].[Payment] " +
                "WHERE Project_Cost_ID = @ProjectCostID " +
                "AND Status != @StatusDeleted " +
                "AND Status != @StatusDeclined " +
                "AND Status != @StatusPreDeclined",
                paymentParams);

            var worksheetParams = new Dictionary<string, object>
            {
                { "@ProjectCostID", Project_Cost_ID },
                { "@StatusDeleted", status_deleted },
                { "@StatusDeclined", status_declined }
            };

            DataTable dtWorkSheet = DatabaseQuerySafe(conn,
                "SELECT * FROM [Payment].[dbo].[TemporaryEmployee_Worksheet] " +
                "WHERE Project_Cost_ID = @ProjectCostID " +
                "AND Status != 'MAKE PAYMENT' " +
                "AND Status != @StatusDeleted " +
                "AND Status != @StatusDeclined",
                worksheetParams);

            int QuantityUsed = 0;
            for (int i = 0; i < dtPayment.Rows.Count; i++)
            {
                QuantityUsed += Int32.Parse(dtPayment.Rows[i]["Payment_Quantity"].ToString());
            }
            for (int i = 0; i < dtWorkSheet.Rows.Count; i++)
            {
                QuantityUsed += Int32.Parse(dtWorkSheet.Rows[i]["Quantity"].ToString());
            }
            for (int i = 0; i < dtInput.Rows.Count; i++)
            {
                QuantityUsed += Int32.Parse(dtInput.Rows[i][quantitycolumnname].ToString());
            }

            return QuantityUsed;
        }


        public void SendLineNotify(string Message)
        {
            try
            {
                string lineToken = ConfigurationManager.AppSettings["linetoken"].ToString();
                string message = Message;
                int stickerPackageID = 0;
                int stickerID = 0;
                //string pictureUrl = ConfigurationManager.AppSettings["prefixurl"].ToString() + HttpContext.Current.Request.Url.Authority + "/" + ConfigurationManager.AppSettings["virtualprefixpicturepath"].ToString() + "/Images/CheckIn_Display/" + ID + ".jpg";
                //string message = HttpUtility.UrlEncode(message, Encoding.UTF8);
                var request = (HttpWebRequest)WebRequest.Create(ConfigurationManager.AppSettings["lineurl"].ToString());
                var postData = string.Format("message={0}", message.Replace("*", "x").Replace("\"", ""));

                if (stickerPackageID > 0 && stickerID > 0)
                {
                    var stickerPackageId = string.Format("stickerPackageId={0}", stickerPackageID);
                    var stickerId = string.Format("stickerId={0}", stickerID);
                    postData += "&" + stickerPackageId.ToString() + "&" + stickerId.ToString();
                }
                //if (pictureUrl != "")
                //{
                //    var imageThumbnail = string.Format("imageThumbnail={0}", pictureUrl);
                //    var imageFullsize = string.Format("imageFullsize={0}", pictureUrl);
                //    postData += "&" + imageThumbnail.ToString() + "&" + imageFullsize.ToString();
                //}
                var data = Encoding.UTF8.GetBytes(postData);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = data.Length;
                request.Headers.Add("Authorization", "Bearer " + lineToken);
                using (var stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);
                var response = (HttpWebResponse)request.GetResponse();
                var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            }
            catch { }
        }

        public async Task SendLineMessageAPI(string userId, string msg, string imageurl, string sticker)
        {
            string status = "";
            try
            {
                string channelAccessToken = ConfigurationManager.AppSettings["linechannelaccesstokentaketime"]?.ToString() ?? "";
                var lineMessagingClient = new LineMessagingClient(channelAccessToken);

                // สร้างข้อความ
                var textMessage = new TextMessage(msg);
                var imageMessage = new ImageMessage(imageurl, imageurl);
                var stickerMessage = new StickerMessage(sticker, sticker);
                var messages = new List<ISendMessage>();
                // สร้างรายการข้อความ
                if (msg.Length > 0 && imageurl.Length > 0 && sticker.Length > 0)
                {
                    messages = new List<ISendMessage> { textMessage, imageMessage, stickerMessage };
                }
                else if (msg.Length > 0 && imageurl.Length > 0)
                {
                    messages = new List<ISendMessage> { textMessage, imageMessage };
                }
                else if (msg.Length > 0 && sticker.Length > 0)
                {
                    messages = new List<ISendMessage> { textMessage, stickerMessage };
                }
                else if (msg.Length > 0)
                {
                    messages = new List<ISendMessage> { textMessage };
                }
                status = "before";
                // ส่งข้อความ
                await lineMessagingClient.PushMessageAsync(userId, messages);
                status = "ok" ;
            }
            catch(Exception ex) {

            }

        }

    }
}