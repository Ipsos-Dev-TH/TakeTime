// CodeHelper.cs
using Npgsql;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading.Tasks;
using System.Web;

namespace Take_Time_BangPhra.Helpers
{
    public class CodeHelper
    {
        public DateTime? ParseDate(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return null;

            DateTime result;
            if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return result;

            if (DateTime.TryParse(dateString, out result))
                return result;

            return null;
        }

        public string AdaptSql(string sql)
        {
            return sql?.Replace("'", "''");
        }

        public void Log(string connStr, string action, string detail, string logby)
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
    }
}