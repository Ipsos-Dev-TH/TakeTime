using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

namespace Take_Time_BangPhra.Class
{
    /// <summary>
    /// Service for Web Analytics - analyzing access logs and usage patterns
    /// </summary>
    public class WebAnalyticsService
    {
        private readonly string connectionString;

        public WebAnalyticsService()
        {
            connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        }

        #region User Type Filter

        /// <summary>
        /// User type filter: All, Staff (logged in IPs or Internal IP), Customer (External IP never logged in)
        /// </summary>
        public enum UserType
        {
            All = 0,
            Staff = 1,      // IPs that have logged in OR Internal IPs
            Customer = 2    // External IPs that never logged in
        }

        /// <summary>
        /// Get CTE for Staff IPs - runs once per query for high performance
        /// </summary>
        private string GetStaffIPsCTE()
        {
            return @"StaffIPs AS (
                SELECT DISTINCT LogFromIP
                FROM Logs
                WHERE LogFromIP IS NOT NULL
                AND LogFromIP != ''
                AND LogBy IS NOT NULL
                AND LogBy != ''
            )";
        }

        /// <summary>
        /// Check if IP is internal (local network or localhost)
        /// </summary>
        private string GetInternalIPCheck(string ipColumn = "DeviceIP")
        {
            return $@"({ipColumn} LIKE '10.%'
                OR {ipColumn} LIKE '192.168.%'
                OR {ipColumn} LIKE '172.16.%' OR {ipColumn} LIKE '172.17.%' OR {ipColumn} LIKE '172.18.%'
                OR {ipColumn} LIKE '172.19.%' OR {ipColumn} LIKE '172.20.%' OR {ipColumn} LIKE '172.21.%'
                OR {ipColumn} LIKE '172.22.%' OR {ipColumn} LIKE '172.23.%' OR {ipColumn} LIKE '172.24.%'
                OR {ipColumn} LIKE '172.25.%' OR {ipColumn} LIKE '172.26.%' OR {ipColumn} LIKE '172.27.%'
                OR {ipColumn} LIKE '172.28.%' OR {ipColumn} LIKE '172.29.%' OR {ipColumn} LIKE '172.30.%'
                OR {ipColumn} LIKE '172.31.%'
                OR {ipColumn} LIKE '127.%'
                OR {ipColumn} = '::1'
                OR {ipColumn} = 'localhost')";
        }

        /// <summary>
        /// Build SQL WHERE clause for user type filter (use with CTE and LEFT JOIN)
        /// Staff = IP ที่เคย login เข้าระบบ (มีใน StaffIPs CTE) หรือ Internal IP
        /// Customer = IP ภายนอกที่ไม่เคย login
        /// </summary>
        private string GetUserTypeJoinFilter(UserType userType, string ipColumn = "DeviceIP", string staffIPsAlias = "sip")
        {
            string internalIPCheck = GetInternalIPCheck(ipColumn);
            switch (userType)
            {
                case UserType.Staff:
                    // IPs that have logged in (found in StaffIPs CTE) OR Internal IPs
                    return $@" AND ({staffIPsAlias}.LogFromIP IS NOT NULL OR {internalIPCheck})";
                case UserType.Customer:
                    // External IPs that never logged in (not in StaffIPs AND not internal)
                    return $@" AND ({staffIPsAlias}.LogFromIP IS NULL AND NOT {internalIPCheck})";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Get SQL CASE expression for user category (Staff/Customer) - use with LEFT JOIN StaffIPs
        /// </summary>
        private string GetUserCategoryJoinSql(string ipColumn = "DeviceIP", string staffIPsAlias = "sip")
        {
            string internalIPCheck = GetInternalIPCheck(ipColumn);
            return $@"CASE
                WHEN {staffIPsAlias}.LogFromIP IS NOT NULL OR {internalIPCheck}
                THEN 'Staff'
                ELSE 'Customer'
            END";
        }

        #endregion

        #region Dashboard Statistics

        /// <summary>
        /// Get overview statistics for the dashboard with user type filter
        /// Uses CTE for high performance Staff IP lookup
        /// </summary>
        public DataTable GetDashboardStats(DateTime startDate, DateTime endDate, UserType userType = UserType.All)
        {
            string staffIPsCTE = GetStaffIPsCTE();
            string userTypeFilter = GetUserTypeJoinFilter(userType, "la.DeviceIP", "sip");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;

                    if (userType == UserType.All)
                    {
                        // No filter needed - simple query without CTE
                        cmd.CommandText = @"
                            SELECT
                                (SELECT COUNT(*) FROM Logs_Access WHERE AccessDateTime BETWEEN @StartDate AND @EndDate) AS TotalVisits,
                                (SELECT COUNT(DISTINCT DeviceIP) FROM Logs_Access WHERE AccessDateTime BETWEEN @StartDate AND @EndDate) AS UniqueVisitors,
                                (SELECT COUNT(*) FROM Logs_Access WHERE AccessDateTime >= CAST(GETDATE() AS DATE)) AS TodayVisits,
                                (SELECT COUNT(*) FROM Logs_Access WHERE AccessDateTime >= DATEADD(DAY, -7, GETDATE())) AS WeeklyVisits,
                                (SELECT COUNT(*) FROM Logs_Access WHERE AccessDateTime >= DATEADD(DAY, -30, GETDATE())) AS MonthlyVisits,
                                (SELECT COUNT(*) FROM System_Logs WHERE LogLevel >= 4 AND CreatedDate BETWEEN @StartDate AND @EndDate) AS ErrorCount,
                                (SELECT COUNT(DISTINCT Browser) FROM Logs_Access WHERE AccessDateTime BETWEEN @StartDate AND @EndDate) AS UniqueBrowsers,
                                (SELECT TOP 1 Browser FROM Logs_Access WHERE AccessDateTime BETWEEN @StartDate AND @EndDate GROUP BY Browser ORDER BY COUNT(*) DESC) AS TopBrowser";
                    }
                    else
                    {
                        // Use CTE with LEFT JOIN for Staff/Customer filter
                        cmd.CommandText = $@"
                            ;WITH {staffIPsCTE}
                            SELECT
                                (SELECT COUNT(*) FROM Logs_Access la LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate {userTypeFilter}) AS TotalVisits,
                                (SELECT COUNT(DISTINCT la.DeviceIP) FROM Logs_Access la LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate {userTypeFilter}) AS UniqueVisitors,
                                (SELECT COUNT(*) FROM Logs_Access la LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP WHERE la.AccessDateTime >= CAST(GETDATE() AS DATE) {userTypeFilter}) AS TodayVisits,
                                (SELECT COUNT(*) FROM Logs_Access la LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP WHERE la.AccessDateTime >= DATEADD(DAY, -7, GETDATE()) {userTypeFilter}) AS WeeklyVisits,
                                (SELECT COUNT(*) FROM Logs_Access la LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP WHERE la.AccessDateTime >= DATEADD(DAY, -30, GETDATE()) {userTypeFilter}) AS MonthlyVisits,
                                (SELECT COUNT(*) FROM System_Logs WHERE LogLevel >= 4 AND CreatedDate BETWEEN @StartDate AND @EndDate) AS ErrorCount,
                                (SELECT COUNT(DISTINCT la.Browser) FROM Logs_Access la LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate {userTypeFilter}) AS UniqueBrowsers,
                                (SELECT TOP 1 la.Browser FROM Logs_Access la LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate {userTypeFilter} GROUP BY la.Browser ORDER BY COUNT(*) DESC) AS TopBrowser";
                    }

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        #endregion

        #region Access Logs

        /// <summary>
        /// Get access logs with pagination and filters
        /// Uses CTE for high performance Staff IP lookup
        /// </summary>
        public DataTable GetAccessLogs(DateTime startDate, DateTime endDate, string browser = "", string deviceIP = "", int pageSize = 100, int pageNumber = 1, UserType userType = UserType.All)
        {
            string staffIPsCTE = GetStaffIPsCTE();
            string userTypeFilter = GetUserTypeJoinFilter(userType, "la.DeviceIP", "sip");
            string userCategorySql = GetUserCategoryJoinSql("la.DeviceIP", "sip");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = $@"
                        ;WITH {staffIPsCTE}
                        SELECT
                            la.ID,
                            la.AccessDateTime,
                            la.DeviceName,
                            la.DeviceIP,
                            la.Browser,
                            {userCategorySql} AS UserCategory,
                            ROW_NUMBER() OVER (ORDER BY la.AccessDateTime DESC) AS RowNum
                        FROM Logs_Access la
                        LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP
                        WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate
                        AND (@Browser = '' OR la.Browser LIKE '%' + @Browser + '%')
                        AND (@DeviceIP = '' OR la.DeviceIP LIKE '%' + @DeviceIP + '%')
                        {userTypeFilter}
                        ORDER BY la.AccessDateTime DESC
                        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    cmd.Parameters.AddWithValue("@Browser", browser ?? "");
                    cmd.Parameters.AddWithValue("@DeviceIP", deviceIP ?? "");
                    cmd.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        /// <summary>
        /// Get total access log count for pagination
        /// Uses CTE for high performance Staff IP lookup
        /// </summary>
        public int GetAccessLogCount(DateTime startDate, DateTime endDate, string browser = "", string deviceIP = "", UserType userType = UserType.All)
        {
            string staffIPsCTE = GetStaffIPsCTE();
            string userTypeFilter = GetUserTypeJoinFilter(userType, "la.DeviceIP", "sip");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = $@"
                        ;WITH {staffIPsCTE}
                        SELECT COUNT(*)
                        FROM Logs_Access la
                        LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP
                        WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate
                        AND (@Browser = '' OR la.Browser LIKE '%' + @Browser + '%')
                        AND (@DeviceIP = '' OR la.DeviceIP LIKE '%' + @DeviceIP + '%')
                        {userTypeFilter}";

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    cmd.Parameters.AddWithValue("@Browser", browser ?? "");
                    cmd.Parameters.AddWithValue("@DeviceIP", deviceIP ?? "");

                    conn.Open();
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        #endregion

        #region Browser Analytics

        /// <summary>
        /// Get browser usage statistics
        /// Uses CTE for high performance Staff IP lookup
        /// </summary>
        public DataTable GetBrowserStats(DateTime startDate, DateTime endDate, UserType userType = UserType.All)
        {
            string staffIPsCTE = GetStaffIPsCTE();
            string userTypeFilter = GetUserTypeJoinFilter(userType, "la.DeviceIP", "sip");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;

                    if (userType == UserType.All)
                    {
                        cmd.CommandText = @"
                            SELECT
                                Browser,
                                COUNT(*) AS VisitCount,
                                CAST(COUNT(*) * 100.0 / NULLIF((SELECT COUNT(*) FROM Logs_Access WHERE AccessDateTime BETWEEN @StartDate AND @EndDate), 0) AS DECIMAL(5,2)) AS Percentage
                            FROM Logs_Access
                            WHERE AccessDateTime BETWEEN @StartDate AND @EndDate
                            AND Browser IS NOT NULL AND Browser != ''
                            GROUP BY Browser
                            ORDER BY VisitCount DESC";
                    }
                    else
                    {
                        cmd.CommandText = $@"
                            ;WITH {staffIPsCTE}
                            SELECT
                                la.Browser,
                                COUNT(*) AS VisitCount,
                                CAST(COUNT(*) * 100.0 / NULLIF((SELECT COUNT(*) FROM Logs_Access la2 LEFT JOIN StaffIPs sip2 ON la2.DeviceIP = sip2.LogFromIP WHERE la2.AccessDateTime BETWEEN @StartDate AND @EndDate {userTypeFilter.Replace("la.", "la2.").Replace("sip.", "sip2.")}), 0) AS DECIMAL(5,2)) AS Percentage
                            FROM Logs_Access la
                            LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP
                            WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate
                            AND la.Browser IS NOT NULL AND la.Browser != ''
                            {userTypeFilter}
                            GROUP BY la.Browser
                            ORDER BY VisitCount DESC";
                    }

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        #endregion

        #region Time-based Analytics

        /// <summary>
        /// Get visits by hour of day
        /// Uses CTE for high performance Staff IP lookup
        /// </summary>
        public DataTable GetVisitsByHour(DateTime startDate, DateTime endDate, UserType userType = UserType.All)
        {
            string staffIPsCTE = GetStaffIPsCTE();
            string userTypeFilter = GetUserTypeJoinFilter(userType, "la.DeviceIP", "sip");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;

                    if (userType == UserType.All)
                    {
                        cmd.CommandText = @"
                            SELECT
                                DATEPART(HOUR, AccessDateTime) AS Hour,
                                COUNT(*) AS VisitCount
                            FROM Logs_Access
                            WHERE AccessDateTime BETWEEN @StartDate AND @EndDate
                            GROUP BY DATEPART(HOUR, AccessDateTime)
                            ORDER BY Hour";
                    }
                    else
                    {
                        cmd.CommandText = $@"
                            ;WITH {staffIPsCTE}
                            SELECT
                                DATEPART(HOUR, la.AccessDateTime) AS Hour,
                                COUNT(*) AS VisitCount
                            FROM Logs_Access la
                            LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP
                            WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate
                            {userTypeFilter}
                            GROUP BY DATEPART(HOUR, la.AccessDateTime)
                            ORDER BY Hour";
                    }

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        /// <summary>
        /// Get visits by day of week
        /// Uses CTE for high performance Staff IP lookup
        /// </summary>
        public DataTable GetVisitsByDayOfWeek(DateTime startDate, DateTime endDate, UserType userType = UserType.All)
        {
            string staffIPsCTE = GetStaffIPsCTE();
            string userTypeFilter = GetUserTypeJoinFilter(userType, "la.DeviceIP", "sip");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;

                    if (userType == UserType.All)
                    {
                        cmd.CommandText = @"
                            SELECT
                                DATEPART(WEEKDAY, AccessDateTime) AS DayOfWeek,
                                DATENAME(WEEKDAY, AccessDateTime) AS DayName,
                                COUNT(*) AS VisitCount
                            FROM Logs_Access
                            WHERE AccessDateTime BETWEEN @StartDate AND @EndDate
                            GROUP BY DATEPART(WEEKDAY, AccessDateTime), DATENAME(WEEKDAY, AccessDateTime)
                            ORDER BY DayOfWeek";
                    }
                    else
                    {
                        cmd.CommandText = $@"
                            ;WITH {staffIPsCTE}
                            SELECT
                                DATEPART(WEEKDAY, la.AccessDateTime) AS DayOfWeek,
                                DATENAME(WEEKDAY, la.AccessDateTime) AS DayName,
                                COUNT(*) AS VisitCount
                            FROM Logs_Access la
                            LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP
                            WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate
                            {userTypeFilter}
                            GROUP BY DATEPART(WEEKDAY, la.AccessDateTime), DATENAME(WEEKDAY, la.AccessDateTime)
                            ORDER BY DayOfWeek";
                    }

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        /// <summary>
        /// Get daily visits trend
        /// Uses CTE for high performance Staff IP lookup
        /// </summary>
        public DataTable GetDailyVisitsTrend(DateTime startDate, DateTime endDate, UserType userType = UserType.All)
        {
            string staffIPsCTE = GetStaffIPsCTE();
            string userTypeFilter = GetUserTypeJoinFilter(userType, "la.DeviceIP", "sip");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;

                    if (userType == UserType.All)
                    {
                        cmd.CommandText = @"
                            SELECT
                                CAST(AccessDateTime AS DATE) AS VisitDate,
                                COUNT(*) AS VisitCount,
                                COUNT(DISTINCT DeviceIP) AS UniqueVisitors
                            FROM Logs_Access
                            WHERE AccessDateTime BETWEEN @StartDate AND @EndDate
                            GROUP BY CAST(AccessDateTime AS DATE)
                            ORDER BY VisitDate";
                    }
                    else
                    {
                        cmd.CommandText = $@"
                            ;WITH {staffIPsCTE}
                            SELECT
                                CAST(la.AccessDateTime AS DATE) AS VisitDate,
                                COUNT(*) AS VisitCount,
                                COUNT(DISTINCT la.DeviceIP) AS UniqueVisitors
                            FROM Logs_Access la
                            LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP
                            WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate
                            {userTypeFilter}
                            GROUP BY CAST(la.AccessDateTime AS DATE)
                            ORDER BY VisitDate";
                    }

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        /// <summary>
        /// Get monthly visits summary
        /// Uses CTE for high performance Staff IP lookup
        /// </summary>
        public DataTable GetMonthlyVisitsSummary(int year, UserType userType = UserType.All)
        {
            string staffIPsCTE = GetStaffIPsCTE();
            string userTypeFilter = GetUserTypeJoinFilter(userType, "la.DeviceIP", "sip");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;

                    if (userType == UserType.All)
                    {
                        cmd.CommandText = @"
                            SELECT
                                MONTH(AccessDateTime) AS Month,
                                DATENAME(MONTH, AccessDateTime) AS MonthName,
                                COUNT(*) AS VisitCount,
                                COUNT(DISTINCT DeviceIP) AS UniqueVisitors
                            FROM Logs_Access
                            WHERE YEAR(AccessDateTime) = @Year
                            GROUP BY MONTH(AccessDateTime), DATENAME(MONTH, AccessDateTime)
                            ORDER BY Month";
                    }
                    else
                    {
                        cmd.CommandText = $@"
                            ;WITH {staffIPsCTE}
                            SELECT
                                MONTH(la.AccessDateTime) AS Month,
                                DATENAME(MONTH, la.AccessDateTime) AS MonthName,
                                COUNT(*) AS VisitCount,
                                COUNT(DISTINCT la.DeviceIP) AS UniqueVisitors
                            FROM Logs_Access la
                            LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP
                            WHERE YEAR(la.AccessDateTime) = @Year
                            {userTypeFilter}
                            GROUP BY MONTH(la.AccessDateTime), DATENAME(MONTH, la.AccessDateTime)
                            ORDER BY Month";
                    }

                    cmd.Parameters.AddWithValue("@Year", year);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        #endregion

        #region IP Analytics

        /// <summary>
        /// Get top visitors by IP
        /// Uses CTE for high performance Staff IP lookup
        /// </summary>
        public DataTable GetTopVisitorsByIP(DateTime startDate, DateTime endDate, int topN = 20, UserType userType = UserType.All)
        {
            string staffIPsCTE = GetStaffIPsCTE();
            string userTypeFilter = GetUserTypeJoinFilter(userType, "la.DeviceIP", "sip");
            string userCategorySql = GetUserCategoryJoinSql("la.DeviceIP", "sip");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = $@"
                        ;WITH {staffIPsCTE}
                        SELECT TOP (@TopN)
                            la.DeviceIP,
                            la.DeviceName,
                            COUNT(*) AS VisitCount,
                            MIN(la.AccessDateTime) AS FirstVisit,
                            MAX(la.AccessDateTime) AS LastVisit,
                            COUNT(DISTINCT CAST(la.AccessDateTime AS DATE)) AS VisitDays,
                            {userCategorySql} AS UserCategory
                        FROM Logs_Access la
                        LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP
                        WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate
                        AND la.DeviceIP IS NOT NULL AND la.DeviceIP != ''
                        {userTypeFilter}
                        GROUP BY la.DeviceIP, la.DeviceName, sip.LogFromIP
                        ORDER BY VisitCount DESC";

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    cmd.Parameters.AddWithValue("@TopN", topN);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        /// <summary>
        /// Get new vs returning visitors
        /// Uses CTE for high performance Staff IP lookup
        /// </summary>
        public DataTable GetNewVsReturningVisitors(DateTime startDate, DateTime endDate, UserType userType = UserType.All)
        {
            string staffIPsCTE = GetStaffIPsCTE();
            string userTypeFilter = GetUserTypeJoinFilter(userType, "la.DeviceIP", "sip");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;

                    if (userType == UserType.All)
                    {
                        cmd.CommandText = @"
                            WITH VisitorFirstVisit AS (
                                SELECT DeviceIP, MIN(AccessDateTime) AS FirstVisit
                                FROM Logs_Access
                                GROUP BY DeviceIP
                            )
                            SELECT
                                CASE
                                    WHEN vfv.FirstVisit >= @StartDate THEN 'New Visitor'
                                    ELSE 'Returning Visitor'
                                END AS VisitorType,
                                COUNT(*) AS VisitCount
                            FROM Logs_Access la
                            INNER JOIN VisitorFirstVisit vfv ON la.DeviceIP = vfv.DeviceIP
                            WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate
                            GROUP BY CASE
                                WHEN vfv.FirstVisit >= @StartDate THEN 'New Visitor'
                                ELSE 'Returning Visitor'
                            END";
                    }
                    else
                    {
                        cmd.CommandText = $@"
                            ;WITH {staffIPsCTE},
                            VisitorFirstVisit AS (
                                SELECT la2.DeviceIP, MIN(la2.AccessDateTime) AS FirstVisit
                                FROM Logs_Access la2
                                LEFT JOIN StaffIPs sip2 ON la2.DeviceIP = sip2.LogFromIP
                                WHERE 1=1 {userTypeFilter.Replace("la.", "la2.").Replace("sip.", "sip2.")}
                                GROUP BY la2.DeviceIP
                            )
                            SELECT
                                CASE
                                    WHEN vfv.FirstVisit >= @StartDate THEN 'New Visitor'
                                    ELSE 'Returning Visitor'
                                END AS VisitorType,
                                COUNT(*) AS VisitCount
                            FROM Logs_Access la
                            LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP
                            INNER JOIN VisitorFirstVisit vfv ON la.DeviceIP = vfv.DeviceIP
                            WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate
                            {userTypeFilter}
                            GROUP BY CASE
                                WHEN vfv.FirstVisit >= @StartDate THEN 'New Visitor'
                                ELSE 'Returning Visitor'
                            END";
                    }

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        /// <summary>
        /// Get staff vs customer usage summary
        /// Staff = IP ที่เคย login หรือ Internal IP
        /// Customer = IP ภายนอกที่ไม่เคย login
        /// Uses CTE for high performance Staff IP lookup
        /// </summary>
        public DataTable GetStaffVsCustomerSummary(DateTime startDate, DateTime endDate)
        {
            string staffIPsCTE = GetStaffIPsCTE();
            string userCategorySql = GetUserCategoryJoinSql("la.DeviceIP", "sip");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = $@"
                        ;WITH {staffIPsCTE}
                        SELECT
                            {userCategorySql} AS UserCategory,
                            COUNT(*) AS VisitCount,
                            COUNT(DISTINCT la.DeviceIP) AS UniqueVisitors,
                            CAST(COUNT(*) * 100.0 / NULLIF((SELECT COUNT(*) FROM Logs_Access WHERE AccessDateTime BETWEEN @StartDate AND @EndDate), 0) AS DECIMAL(5,2)) AS Percentage
                        FROM Logs_Access la
                        LEFT JOIN StaffIPs sip ON la.DeviceIP = sip.LogFromIP
                        WHERE la.AccessDateTime BETWEEN @StartDate AND @EndDate
                        GROUP BY {userCategorySql}
                        ORDER BY VisitCount DESC";

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        #endregion

        #region System Logs Analytics

        /// <summary>
        /// Get system log summary by category
        /// </summary>
        public DataTable GetSystemLogsByCategory(DateTime startDate, DateTime endDate)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT
                            Category,
                            CASE Category
                                WHEN 0 THEN 'General'
                                WHEN 1 THEN 'Accounting'
                                WHEN 2 THEN 'Payment'
                                WHEN 3 THEN 'Receipt'
                                WHEN 4 THEN 'Revenue'
                                WHEN 5 THEN 'Reconciliation'
                                WHEN 6 THEN 'DataIntegrity'
                                WHEN 7 THEN 'Performance'
                                ELSE 'Unknown'
                            END AS CategoryName,
                            COUNT(*) AS LogCount,
                            SUM(CASE WHEN LogLevel >= 4 THEN 1 ELSE 0 END) AS ErrorCount
                        FROM System_Logs
                        WHERE CreatedDate BETWEEN @StartDate AND @EndDate
                        GROUP BY Category
                        ORDER BY LogCount DESC";

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        /// <summary>
        /// Get system log summary by level
        /// </summary>
        public DataTable GetSystemLogsByLevel(DateTime startDate, DateTime endDate)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT
                            LogLevel,
                            CASE LogLevel
                                WHEN 1 THEN 'Debug'
                                WHEN 2 THEN 'Info'
                                WHEN 3 THEN 'Warning'
                                WHEN 4 THEN 'Error'
                                WHEN 5 THEN 'Critical'
                                ELSE 'Unknown'
                            END AS LevelName,
                            COUNT(*) AS LogCount
                        FROM System_Logs
                        WHERE CreatedDate BETWEEN @StartDate AND @EndDate
                        GROUP BY LogLevel
                        ORDER BY LogLevel";

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        /// <summary>
        /// Get recent errors
        /// </summary>
        public DataTable GetRecentErrors(int topN = 50)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT TOP (@TopN)
                            ID,
                            CreatedDate,
                            CASE LogLevel
                                WHEN 4 THEN 'Error'
                                WHEN 5 THEN 'Critical'
                                ELSE 'Unknown'
                            END AS LevelName,
                            CASE Category
                                WHEN 0 THEN 'General'
                                WHEN 1 THEN 'Accounting'
                                WHEN 2 THEN 'Payment'
                                WHEN 3 THEN 'Receipt'
                                WHEN 4 THEN 'Revenue'
                                WHEN 5 THEN 'Reconciliation'
                                WHEN 6 THEN 'DataIntegrity'
                                WHEN 7 THEN 'Performance'
                                ELSE 'Unknown'
                            END AS CategoryName,
                            Message,
                            Details
                        FROM System_Logs
                        WHERE LogLevel >= 4
                        ORDER BY CreatedDate DESC";

                    cmd.Parameters.AddWithValue("@TopN", topN);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        #endregion

        #region User Activity Logs

        /// <summary>
        /// Get user activity logs from the Logs table
        /// </summary>
        public DataTable GetUserActivityLogs(DateTime startDate, DateTime endDate, string action = "", string logBy = "")
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT TOP 500
                            LogDateTime,
                            LogAction,
                            LogDetail,
                            LogBy,
                            LogFromComputerName,
                            LogFromIP
                        FROM Logs
                        WHERE LogDateTime BETWEEN @StartDate AND @EndDate
                        AND (@Action = '' OR LogAction LIKE '%' + @Action + '%')
                        AND (@LogBy = '' OR LogBy LIKE '%' + @LogBy + '%')
                        ORDER BY LogDateTime DESC";

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    cmd.Parameters.AddWithValue("@Action", action ?? "");
                    cmd.Parameters.AddWithValue("@LogBy", logBy ?? "");

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        /// <summary>
        /// Get action summary
        /// </summary>
        public DataTable GetActionSummary(DateTime startDate, DateTime endDate)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT
                            LogAction,
                            COUNT(*) AS ActionCount,
                            COUNT(DISTINCT LogBy) AS UniqueUsers
                        FROM Logs
                        WHERE LogDateTime BETWEEN @StartDate AND @EndDate
                        AND LogAction IS NOT NULL AND LogAction != ''
                        GROUP BY LogAction
                        ORDER BY ActionCount DESC";

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        /// <summary>
        /// Get active users summary
        /// </summary>
        public DataTable GetActiveUsersSummary(DateTime startDate, DateTime endDate)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT
                            LogBy AS Username,
                            COUNT(*) AS ActionCount,
                            MIN(LogDateTime) AS FirstActivity,
                            MAX(LogDateTime) AS LastActivity,
                            COUNT(DISTINCT LogAction) AS UniqueActions
                        FROM Logs
                        WHERE LogDateTime BETWEEN @StartDate AND @EndDate
                        AND LogBy IS NOT NULL AND LogBy != ''
                        GROUP BY LogBy
                        ORDER BY ActionCount DESC";

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        #endregion

        #region Comparison Analytics

        /// <summary>
        /// Compare two periods
        /// </summary>
        public DataTable ComparePeriods(DateTime period1Start, DateTime period1End, DateTime period2Start, DateTime period2End)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT
                            'Period 1' AS Period,
                            @P1Start AS StartDate,
                            @P1End AS EndDate,
                            (SELECT COUNT(*) FROM Logs_Access WHERE AccessDateTime BETWEEN @P1Start AND @P1End) AS TotalVisits,
                            (SELECT COUNT(DISTINCT DeviceIP) FROM Logs_Access WHERE AccessDateTime BETWEEN @P1Start AND @P1End) AS UniqueVisitors
                        UNION ALL
                        SELECT
                            'Period 2' AS Period,
                            @P2Start AS StartDate,
                            @P2End AS EndDate,
                            (SELECT COUNT(*) FROM Logs_Access WHERE AccessDateTime BETWEEN @P2Start AND @P2End) AS TotalVisits,
                            (SELECT COUNT(DISTINCT DeviceIP) FROM Logs_Access WHERE AccessDateTime BETWEEN @P2Start AND @P2End) AS UniqueVisitors";

                    cmd.Parameters.AddWithValue("@P1Start", period1Start);
                    cmd.Parameters.AddWithValue("@P1End", period1End);
                    cmd.Parameters.AddWithValue("@P2Start", period2Start);
                    cmd.Parameters.AddWithValue("@P2End", period2End);

                    conn.Open();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        #endregion

        #region Export

        /// <summary>
        /// Get all data for export
        /// </summary>
        public DataTable GetExportData(DateTime startDate, DateTime endDate, string exportType)
        {
            switch (exportType.ToLower())
            {
                case "access":
                    return GetAccessLogs(startDate, endDate, "", "", 10000, 1);
                case "browser":
                    return GetBrowserStats(startDate, endDate);
                case "daily":
                    return GetDailyVisitsTrend(startDate, endDate);
                case "hourly":
                    return GetVisitsByHour(startDate, endDate);
                case "visitors":
                    return GetTopVisitorsByIP(startDate, endDate, 100);
                default:
                    return new DataTable();
            }
        }

        #endregion
    }
}
