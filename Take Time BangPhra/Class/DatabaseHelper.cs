// DatabaseHelper.cs
using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Globalization;
using System.Collections.Generic;

namespace Take_Time_BangPhra.Helpers
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        }

        public DatabaseHelper(string connectionString)
        {
            _connectionString = connectionString;
        }

        public string ConnectionString
        {
            get { return _connectionString; }
        }

        /// <summary>
        /// Gets the default connection string from configuration
        /// </summary>
        public static string GetConnectionString()
        {
            return ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        }

        public int ExecuteInsert(string sqlCommand)
        {
            int id = 0;
            using (SqlConnection connection = new SqlConnection(_connectionString))
            using (SqlCommand command = new SqlCommand(CleanSqlCommand(sqlCommand), connection))
            {
                // Match the old method exactly
                string cmd = sqlCommand.Replace("&nbsp;", "");
                connection.Open();
                try
                {
                    var result = command.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        id = Convert.ToInt32(result);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError($"Database insert error: {ex.Message}");
                    // Don't throw, match old behavior
                }
            }
            return id;
        }

        public DataTable ExecuteQuery(string sqlCommand)
        {
            DataTable dt = new DataTable();
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                try
                {
                    // Match the old cleaning exactly
                    string cmd = sqlCommand.Replace("&amp;", "&");
                    cmd = cmd.Replace("&#39;", "''");
                    cmd = cmd.Replace("&nbsp;", "");

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd, con);
                    adapter.Fill(dt);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError($"Database query error: {ex.Message}");
                    // Don't throw, match old behavior
                }
            }
            return dt;
        }
        /// <summary>
        /// Executes a query with parameterized values to prevent SQL Injection
        /// </summary>
        /// <param name="query">SQL query with @parameter placeholders</param>
        /// <param name="parameters">Dictionary of parameter names and values</param>
        /// <returns>DataTable with query results</returns>
        public DataTable ExecuteQueryWithParams(string query, Dictionary<string, object> parameters = null)
        {
            DataTable dt = new DataTable();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                    }

                    try
                    {
                        conn.Open();
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError($"Database query error: {ex.Message}\nQuery: {query}");
                        throw; // Re-throw for proper error handling
                    }
                }
            }
            return dt;
        }

        /// <summary>
        /// Executes INSERT/UPDATE/DELETE with parameterized values and returns rows affected
        /// </summary>
        /// <param name="query">SQL command with @parameter placeholders</param>
        /// <param name="parameters">Dictionary of parameter names and values</param>
        /// <returns>Number of rows affected</returns>
        public int ExecuteNonQueryWithParams(string query, Dictionary<string, object> parameters = null)
        {
            int rowsAffected = 0;
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                    }

                    try
                    {
                        conn.Open();
                        rowsAffected = cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError($"Database command error: {ex.Message}\nQuery: {query}");
                        throw;
                    }
                }
            }
            return rowsAffected;
        }

        /// <summary>
        /// Executes INSERT and returns the new identity (ID)
        /// </summary>
        /// <param name="query">INSERT query with @parameter placeholders</param>
        /// <param name="parameters">Dictionary of parameter names and values</param>
        /// <returns>New record ID</returns>
        public int ExecuteInsertWithParams(string query, Dictionary<string, object> parameters = null)
        {
            int newId = 0;
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                    }

                    try
                    {
                        conn.Open();
                        // Execute and get identity
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            newId = Convert.ToInt32(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError($"Database insert error: {ex.Message}\nQuery: {query}");
                        throw;
                    }
                }
            }
            return newId;
        }

        /// <summary>
        /// Executes a scalar query with parameters (returns single value)
        /// </summary>
        /// <param name="query">SQL query with @parameter placeholders</param>
        /// <param name="parameters">Dictionary of parameter names and values</param>
        /// <returns>Scalar value or null</returns>
        public object ExecuteScalarWithParams(string query, Dictionary<string, object> parameters = null)
        {
            object result = null;
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                    }

                    try
                    {
                        conn.Open();
                        result = cmd.ExecuteScalar();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError($"Database scalar query error: {ex.Message}\nQuery: {query}");
                        throw;
                    }
                }
            }
            return result;
        }

        private string CleanSqlCommand(string command)
        {
            return command?.Replace("&amp;", "&")
                         .Replace("&#39;", "''")
                         .Replace("&nbsp;", "")
                         .Replace("'", "''") ?? string.Empty;
        }

        public string GenerateDocumentNumber(string tableName, string docType, DateTime documentDate)
        {
            // Whitelist allowed table names to prevent SQL injection
            var allowedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Account_Receipt", "Account_Payment", "Account_Receipt_Detail",
                "Account_Payment_Detail", "Reservation"
            };
            if (!allowedTables.Contains(tableName))
            {
                throw new ArgumentException($"Table name '{tableName}' is not allowed for document number generation.");
            }

            string year = documentDate.Year.ToString().Substring(2);
            string month = documentDate.Month.ToString("00");
            string day = documentDate.Day.ToString("00");

            string prefix = $"{docType}{year}{month}{day}";
            // tableName is validated above via whitelist, safe to use in query
            string query = $"SELECT TOP 1 ID FROM {tableName} WHERE ID LIKE @Prefix ORDER BY ID DESC";
            DataTable dt;
            using (SqlConnection connection = new SqlConnection(_connectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@Prefix", prefix + "%");
                connection.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    dt = new DataTable();
                    adapter.Fill(dt);
                }
            }

            int nextNumber = 1;
            if (dt.Rows.Count > 0)
            {
                string lastNumber = dt.Rows[0][0].ToString();
                if (lastNumber.Length >= 3)
                {
                    string lastThree = lastNumber.Substring(lastNumber.Length - 3);
                    nextNumber = Convert.ToInt32(lastThree) + 1;
                }
            }

            string numberStr = nextNumber.ToString("000");
            return $"{docType}{year}{month}{day}{numberStr}";
        }
    }
}