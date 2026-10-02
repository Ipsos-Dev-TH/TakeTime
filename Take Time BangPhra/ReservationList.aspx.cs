using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra
{
    public partial class ReservationList : System.Web.UI.Page
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private const int PageSize = 50;

        /// <summary>มัดจำที่ใบเลื่อนในตัวกรองนี้ถือไว้ (คำนวณใน GetReservationsData — แสดงแยกจากยอดค้างชำระ)</summary>
        private decimal totalPostponedHeld;

        /// <summary>ยอดของแถวในหน้าปัจจุบัน (ApplyBalances) — ใช้ทำป้ายวิธีเก็บเงินใน RowDataBound</summary>
        private Dictionary<int, ReservationBalance> _rowBalances;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Check permission
                if (Session["permission"]?.ToString() != "True")
                {
                    Response.Redirect("~/Admin/Login", true);
                    return;
                }

                // เปิดหน้าครั้งแรก: กรองตาม "วันเข้าพัก" ย้อนหลัง 7 วัน → ล่วงหน้า 30 วัน (งานหน้างานใช้ช่วงนี้)
                // และยอดรวมด้านบนคำนวณจากช่วงนี้ — วันที่จองเว้นว่างไว้ (กรองเพิ่มได้)
                txtDateFrom.Text = "";
                txtDateTo.Text = "";
                txtStayFrom.Text = DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                txtStayTo.Text = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

                // Initialize
                ViewState["CurrentPage"] = 1;
                ViewState["FilterStatus"] = "";

                // Load stats and data
                LoadStats();
                LoadReservations();
            }
        }

        #region Stats Loading

        private void LoadStats()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Total reservations (last 90 days)
                    lblStatAll.Text = GetStatCount(conn, "").ToString();

                    // Today's reservations
                    lblStatToday.Text = GetTodayCount(conn).ToString();

                    // By status
                    lblStatDeposit.Text = GetStatCount(conn, "มัดจำแล้ว").ToString();
                    lblStatCheckin.Text = GetStatCount(conn, "เช็คอินแล้ว").ToString();
                    lblStatCheckout.Text = GetStatCount(conn, "เช็คเอาท์แล้ว").ToString();
                    lblStatComplete.Text = GetStatCount(conn, "เสร็จสิ้น").ToString();
                    lblStatCancel.Text = GetCancelCount(conn).ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadStats Error: " + ex.Message);
            }
        }

        private int GetStatCount(SqlConnection conn, string status)
        {
            string query = @"SELECT COUNT(*) FROM Reservation
                            WHERE Created_Date >= DATEADD(DAY, -90, GETDATE())";

            if (!string.IsNullOrEmpty(status))
            {
                query += " AND Status = @Status";
            }

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                if (!string.IsNullOrEmpty(status))
                {
                    cmd.Parameters.AddWithValue("@Status", status);
                }
                return (int)cmd.ExecuteScalar();
            }
        }

        private int GetTodayCount(SqlConnection conn)
        {
            string query = @"SELECT COUNT(*) FROM Reservation
                            WHERE CAST(Created_Date AS DATE) = CAST(GETDATE() AS DATE)";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                return (int)cmd.ExecuteScalar();
            }
        }

        /// <summary>ตัวกรอง "ยกเลิก (ทั้งหมด)" — ทุกสถานะที่ขึ้นต้น ยกเลิก/ลบ (ตรงข้ามกับ RescheduleService.SqlNotCancelled)</summary>
        private const string CancelledFilterSql = "(R.Status LIKE N'ยกเลิก%' OR R.Status LIKE N'ลบ%')";

        private int GetCancelCount(SqlConnection conn)
        {
            string query = @"SELECT COUNT(*) FROM Reservation R
                            WHERE R.Created_Date >= DATEADD(DAY, -90, GETDATE())
                            AND " + CancelledFilterSql;

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                return (int)cmd.ExecuteScalar();
            }
        }

        #endregion

        #region Data Loading

        private void LoadReservations()
        {
            try
            {
                int currentPage = ViewState["CurrentPage"] != null ? (int)ViewState["CurrentPage"] : 1;
                string filterStatus = ViewState["FilterStatus"]?.ToString() ?? "";

                DataTable dt = GetReservationsData(currentPage, out int totalRecords, out decimal totalAmount, out decimal totalDeposit, out decimal totalDue);

                // Bind data
                gvReservations.DataSource = dt;
                gvReservations.DataBind();

                // Update summary — ยอดรวมแสดงเฉพาะเมื่อมีช่วงวันที่ (ไม่งั้นแสดงคำแนะนำแทน)
                bool showTotals = HasDateRange();
                lblResultCount.Text = totalRecords.ToString("N0");
                lblTotalAmount.Text = totalAmount.ToString("N0");
                lblTotalDeposit.Text = totalDeposit.ToString("N0");
                lblTotalRemain.Text = totalDue.ToString("N0");
                lblTotalPostponedHeld.Text = totalPostponedHeld.ToString("N0");
                phPostponedHeld.Visible = showTotals && totalPostponedHeld > 0m;
                phTotals.Visible = showTotals;
                phTotalsHint.Visible = !showTotals;

                // Update pagination
                int totalPages = (int)Math.Ceiling((double)totalRecords / PageSize);
                lblCurrentPage.Text = currentPage.ToString();
                lblTotalPages.Text = totalPages.ToString();

                btnPrevPage.Enabled = currentPage > 1;
                btnNextPage.Enabled = currentPage < totalPages;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadReservations Error: " + ex.Message);
            }
        }

        private DataTable GetReservationsData(int page, out int totalRecords, out decimal totalAmount, out decimal totalDeposit, out decimal totalDue)
        {
            totalRecords = 0;
            totalAmount = 0;
            totalDeposit = 0;
            totalDue = 0;

            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Build WHERE clause
                StringBuilder whereClause = new StringBuilder("WHERE 1=1");
                List<SqlParameter> parameters = new List<SqlParameter>();

                // Search filter (use ISNULL for LEFT JOIN compatibility)
                string search = txtSearch.Text.Trim();
                if (!string.IsNullOrEmpty(search))
                {
                    whereClause.Append(@" AND (ISNULL(C.Name, '') LIKE @Search
                                          OR ISNULL(C.NickName, '') LIKE @Search
                                          OR R.Customer_MobilePhone LIKE @Search
                                          OR CAST(R.ID AS NVARCHAR) LIKE @Search)");
                    parameters.Add(new SqlParameter("@Search", "%" + search + "%"));
                }

                // Status filter
                string status = ddlStatus.SelectedValue;
                string filterStatus = ViewState["FilterStatus"]?.ToString() ?? "";

                if (!string.IsNullOrEmpty(filterStatus))
                {
                    status = filterStatus;
                }

                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "ยกเลิก")
                    {
                        whereClause.Append(" AND " + CancelledFilterSql);
                    }
                    else if (status == "postponed")
                    {
                        // ใบเลื่อนวันเข้าพัก (ยังไม่มีวัน) — เกณฑ์เดียวกับหน้ารายการเลื่อน
                        whereClause.Append(" AND " + PostponedRowSql());
                    }
                    else if (status == "today")
                    {
                        whereClause.Append(" AND CAST(R.Created_Date AS DATE) = CAST(GETDATE() AS DATE)");
                    }
                    else
                    {
                        whereClause.Append(" AND R.Status = @Status");
                        parameters.Add(new SqlParameter("@Status", status));
                    }
                }

                // Date range filter (use ParseExact for reliable parsing)
                DateTime dateFrom, dateTo;
                if (!string.IsNullOrEmpty(txtDateFrom.Text) && DateTime.TryParseExact(txtDateFrom.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateFrom))
                {
                    whereClause.Append(" AND R.Created_Date >= @DateFrom");
                    parameters.Add(new SqlParameter("@DateFrom", SqlDbType.DateTime) { Value = dateFrom });
                }

                if (!string.IsNullOrEmpty(txtDateTo.Text) && DateTime.TryParseExact(txtDateTo.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateTo))
                {
                    whereClause.Append(" AND R.Created_Date <= @DateTo");
                    parameters.Add(new SqlParameter("@DateTo", SqlDbType.DateTime) { Value = dateTo.AddDays(1).AddSeconds(-1) });
                }

                // ช่วงวันเข้าพัก (วันพักทับช่วงที่เลือก)
                AppendStayFilter(whereClause, parameters);

                // Get ORDER BY clause
                string orderBy = GetOrderByClause();

                // Count total records and calculate totals (use LEFT JOIN to include reservations without matching customer)
                string countQuery = $@"SELECT COUNT(*), ISNULL(SUM(R.TotalPrice), 0), ISNULL(SUM(ISNULL(R.Deposit, 0)), 0)
                                      FROM Reservation R
                                      LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                                      {whereClause}";

                using (SqlCommand cmdCount = new SqlCommand(countQuery, conn))
                {
                    cmdCount.Parameters.AddRange(parameters.ToArray());
                    using (SqlDataReader reader = cmdCount.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Use Convert to handle different SQL numeric types safely
                            totalRecords = Convert.ToInt32(reader[0]);
                            totalAmount = reader[1] != DBNull.Value ? Convert.ToDecimal(reader[1]) : 0;
                            totalDeposit = reader[2] != DBNull.Value ? Convert.ToDecimal(reader[2]) : 0;
                        }
                    }
                }
                totalDue = totalAmount - totalDeposit;   // ค่าเดิม (ใช้เมื่อคำนวณด้วยสูตรกลางไม่สำเร็จ)

                // ยอดสรุปด้วยสูตรกลาง ReservationBalance (รวมค่าใช้จ่ายในห้อง, Payment_History, Channel Collect)
                // ยอดรวม = Σ Total, รับแล้ว = Σ Received, ค้างชำระ = Σ Due (ไม่เอายอดจ่ายเกินของใบอื่นมาหักกลบ)
                // คำนวณเฉพาะเมื่อมีช่วงวันที่ — ไม่มีช่วง = ทุกใบในประวัติ (ช้า และตัวเลขไม่มีความหมาย) หน้าจอแสดงคำแนะนำแทน
                // ใบเลื่อน (ยังไม่มีวันเข้าพัก) ไม่นับในยอดค้างชำระ — มัดจำที่ถือไว้รวมแยกเป็น "มัดจำของใบเลื่อน"
                totalPostponedHeld = 0m;
                if (HasDateRange())
                {
                    try
                    {
                        Dictionary<int, ReservationBalance> sumBalances = LoadBalancesForFilter(whereClause.ToString(), parameters);
                        HashSet<int> postponedIds = GetPostponedIdsForFilter(whereClause.ToString(), parameters);
                        decimal sAmount = 0, sReceived = 0, sDue = 0;
                        foreach (ReservationBalance b in sumBalances.Values)
                        {
                            sAmount += b.Total;
                            sReceived += b.Received;
                            if (postponedIds.Contains(b.ReservationId))
                                totalPostponedHeld += HeldByGuest(b);
                            else
                                sDue += b.Due;
                        }
                        totalAmount = sAmount;
                        totalDeposit = sReceived;
                        totalDue = sDue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("ReservationList balance summary Error: " + ex.Message);
                    }
                }

                // Get paginated data
                int offset = (page - 1) * PageSize;

                string dataQuery = $@"
                    SELECT R.ID, R.Created_Date, R.Customer_MobilePhone,
                           ISNULL(C.Name, R.Customer_MobilePhone) as Name,
                           ISNULL(C.NickName, '') as NickName,
                           R.CheckinDate, R.CheckoutDate, R.StayDays, R.TotalPrice,
                           ISNULL(R.Deposit, 0) as Deposit, R.Status, R.Reserve_By, R.Remark,
                           {ExtraRowColumnsSql()}
                    FROM Reservation R
                    LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                    {whereClause}
                    ORDER BY {orderBy}
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                // Re-create parameters for data query
                List<SqlParameter> dataParams = new List<SqlParameter>();
                if (!string.IsNullOrEmpty(search))
                {
                    dataParams.Add(new SqlParameter("@Search", "%" + search + "%"));
                }
                if (!string.IsNullOrEmpty(status) && status != "ยกเลิก" && status != "today" && status != "postponed")
                {
                    dataParams.Add(new SqlParameter("@Status", status));
                }
                if (!string.IsNullOrEmpty(txtDateFrom.Text) && DateTime.TryParseExact(txtDateFrom.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateFrom))
                {
                    dataParams.Add(new SqlParameter("@DateFrom", SqlDbType.DateTime) { Value = dateFrom });
                }
                if (!string.IsNullOrEmpty(txtDateTo.Text) && DateTime.TryParseExact(txtDateTo.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateTo))
                {
                    dataParams.Add(new SqlParameter("@DateTo", SqlDbType.DateTime) { Value = dateTo.AddDays(1).AddSeconds(-1) });
                }
                AppendStayFilter(null, dataParams);   // พารามิเตอร์ชุดเดียวกับ whereClause ด้านบน
                dataParams.Add(new SqlParameter("@Offset", offset));
                dataParams.Add(new SqlParameter("@PageSize", PageSize));

                using (SqlCommand cmd = new SqlCommand(dataQuery, conn))
                {
                    cmd.Parameters.AddRange(dataParams.ToArray());

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                }
            }

            ApplyBalances(dt, null, null);
            return dt;
        }

        /// <summary>
        /// โหลดยอดเงิน (สูตรกลาง) ของทุกการจองที่ตรงกับตัวกรองของหน้านี้ — whereClause เป็นข้อความที่หน้านี้
        /// ประกอบเอง (ค่าจากผู้ใช้อยู่ใน parameters เท่านั้น) ใช้ alias R / C แบบเดียวกับ query หลัก
        /// </summary>
        private Dictionary<int, ReservationBalance> LoadBalancesForFilter(string whereClause, List<SqlParameter> parameters)
        {
            var p = new Dictionary<string, object>();
            if (parameters != null)
            {
                foreach (SqlParameter sp in parameters)
                    p[sp.ParameterName] = sp.Value;
            }
            string predicate = "r.ID IN (SELECT R.ID FROM Reservation R " +
                               "LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone " +
                               whereClause + ")";
            return ReservationBalance.LoadMany(connectionString, predicate, p);
        }

        /// <summary>
        /// ใส่ยอดจากสูตรกลางลงในแถว: TotalPrice = ยอดรวม (ค่าห้อง+ค่าใช้จ่ายในห้อง), Deposit = ยอดรับแล้ว,
        /// BalDue = คงเหลือ — ถ้าไม่ส่ง whereClause จะโหลดตาม ID ของแถวที่มีใน dt (หน้าละไม่เกิน PageSize)
        /// คำนวณไม่สำเร็จ → BalDue = TotalPrice − Deposit แบบเดิม
        /// </summary>
        private void ApplyBalances(DataTable dt, string whereClause, List<SqlParameter> parameters)
        {
            if (dt == null) return;
            if (!dt.Columns.Contains("BalDue")) dt.Columns.Add("BalDue", typeof(decimal));

            Dictionary<int, ReservationBalance> balances = null;
            try
            {
                if (whereClause != null)
                {
                    balances = LoadBalancesForFilter(whereClause, parameters);
                }
                else if (dt.Rows.Count > 0)
                {
                    var ids = new List<string>();
                    foreach (DataRow row in dt.Rows)
                    {
                        if (row["ID"] != DBNull.Value)
                            ids.Add(Convert.ToInt32(row["ID"]).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    if (ids.Count > 0)
                        balances = ReservationBalance.LoadMany(connectionString, "r.ID IN (" + string.Join(",", ids) + ")", null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReservationList ApplyBalances Error: " + ex.Message);
                balances = null;
            }
            if (whereClause == null) _rowBalances = balances;

            bool hasPostponedCol = dt.Columns.Contains("IsPostponedRow");
            foreach (DataRow row in dt.Rows)
            {
                ReservationBalance b = null;
                if (balances != null && row["ID"] != DBNull.Value)
                    balances.TryGetValue(Convert.ToInt32(row["ID"]), out b);

                // ใบเลื่อน: ยังไม่มีวันเข้าพัก → ไม่มียอด "ค้างชำระ" (มัดจำที่ถือไว้แสดงแยก)
                bool postponed = hasPostponedCol && row["IsPostponedRow"] != DBNull.Value && Convert.ToBoolean(row["IsPostponedRow"]);

                if (b != null)
                {
                    row["TotalPrice"] = b.Total;
                    row["Deposit"] = b.Received;
                    row["BalDue"] = postponed ? 0m : b.Due;
                }
                else
                {
                    decimal total = row["TotalPrice"] != DBNull.Value ? Convert.ToDecimal(row["TotalPrice"]) : 0m;
                    decimal deposit = row["Deposit"] != DBNull.Value ? Convert.ToDecimal(row["Deposit"]) : 0m;
                    row["BalDue"] = postponed ? 0m : total - deposit;
                }
            }
        }

        /// <summary>เงินที่ลูกค้าจ่ายโรงแรมจริง (ไม่รวมเงินที่ OTA ถือ) — ใช้เป็น "มัดจำของใบเลื่อน"</summary>
        private static decimal HeldByGuest(ReservationBalance b)
        {
            if (b == null) return 0m;
            if (b.LedgerRows > 0) return b.PaidLedger;
            return (b.IsChannelCollect || b.IsCollectUnknown) ? 0m : b.Deposit;
        }

        /// <summary>ID ของใบเลื่อนในตัวกรองเดียวกับหน้านี้ (whereClause ประกอบในโค้ด ค่าจากผู้ใช้อยู่ใน parameters)</summary>
        private HashSet<int> GetPostponedIdsForFilter(string whereClause, List<SqlParameter> parameters)
        {
            var ids = new HashSet<int>();
            var p = new Dictionary<string, object>();
            if (parameters != null)
            {
                foreach (SqlParameter sp in parameters)
                    p[sp.ParameterName] = sp.Value;
            }
            string sql = "SELECT R.ID FROM Reservation R " +
                         "LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone " +
                         whereClause + " AND " + PostponedRowSql();
            DataTable dt = new code().DatabaseQuerySafe(connectionString, sql, p);
            if (dt != null)
            {
                foreach (DataRow r in dt.Rows)
                    if (r["ID"] != DBNull.Value) ids.Add(Convert.ToInt32(r["ID"]));
            }
            return ids;
        }

        /// <summary>เงื่อนไข "ใบเลื่อนที่ยังมีผล" (alias R) — สถานะก่อนเช็คอิน + ไม่มีวันเข้าพัก/ธง IsPostponed</summary>
        private string PostponedRowSql()
        {
            return "(R.Status IN (N'มัดจำแล้ว', N'รอชำระเงิน') AND "
                 + RescheduleService.SqlIsPostponed("R", RescheduleService.HasIsPostponedColumn(connectionString)) + ")";
        }

        private static readonly object _petColLock = new object();
        private static bool? _hasPetCol;
        private static DateTime _petColCheckedAt = DateTime.MinValue;

        /// <summary>มีคอลัมน์ Reservation.Pet_Count (PHASE19 migration 23) — cache, ไม่มีตรวจใหม่ทุก 5 นาที</summary>
        private bool HasPetColumn()
        {
            lock (_petColLock)
            {
                if (_hasPetCol.HasValue && (_hasPetCol.Value || (DateTime.Now - _petColCheckedAt).TotalMinutes < 5))
                    return _hasPetCol.Value;
            }
            bool has = false;
            try
            {
                DataTable dt = new code().DatabaseQuerySafe(connectionString,
                    "SELECT COL_LENGTH('Reservation', 'Pet_Count') AS C");
                has = dt != null && dt.Rows.Count > 0 && dt.Rows[0]["C"] != DBNull.Value;
            }
            catch { has = false; }
            lock (_petColLock)
            {
                _hasPetCol = has;
                _petColCheckedAt = DateTime.Now;
            }
            return has;
        }

        /// <summary>คอลัมน์เสริมของแถว: ธงใบเลื่อน + จำนวนสัตว์เลี้ยง (ไม่มีคอลัมน์ = 0) — ชุดคอลัมน์เดียวกันเสมอ</summary>
        private string ExtraRowColumnsSql()
        {
            return "CAST(CASE WHEN " + PostponedRowSql() + " THEN 1 ELSE 0 END AS bit) AS IsPostponedRow, "
                 + (HasPetColumn() ? "ISNULL(R.Pet_Count, 0) AS PetCount" : "0 AS PetCount");
        }

        /// <summary>บรรทัด "ค้าง" ใต้ราคา — ใบเลื่อนแสดงเป็นมัดจำที่ถือไว้แทน (ไม่ใช่ยอดค้าง)</summary>
        protected string RemainHtml(object balDue, object isPostponed)
        {
            bool postponed = isPostponed != null && isPostponed != DBNull.Value && Convert.ToBoolean(isPostponed);
            if (postponed) return "<span class='price-postponed'>ใบเลื่อน · ไม่นับค้าง</span>";
            decimal due = balDue != null && balDue != DBNull.Value ? Convert.ToDecimal(balDue) : 0m;
            return "ค้าง: " + due.ToString("N0");
        }

        private static bool TryParseFilterDate(string text, out DateTime value)
        {
            return DateTime.TryParseExact((text ?? "").Trim(), "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out value);
        }

        /// <summary>มีช่วงวันที่ (วันเข้าพัก หรือ วันที่จอง ด้านใดด้านหนึ่ง) — ใช้ตัดสินว่าจะคำนวณยอดรวมหรือไม่</summary>
        private bool HasDateRange()
        {
            DateTime d;
            return TryParseFilterDate(txtStayFrom.Text, out d) || TryParseFilterDate(txtStayTo.Text, out d)
                || TryParseFilterDate(txtDateFrom.Text, out d) || TryParseFilterDate(txtDateTo.Text, out d);
        }

        /// <summary>
        /// ตัวกรองช่วงวันเข้าพัก: การจองที่ "วันพักทับช่วงที่เลือก" (เช็คอินไม่หลังวันสุดท้าย และเช็คเอาท์ไม่ก่อนวันแรก
        /// — รวมใบที่ออกในวันแรกของช่วง) where = null → เติมเฉพาะพารามิเตอร์ (สำหรับ query ที่ต้องสร้างพารามิเตอร์ชุดใหม่)
        /// </summary>
        private void AppendStayFilter(StringBuilder where, List<SqlParameter> ps)
        {
            // ใบเลื่อนไม่มีวันเข้าพัก (ค่าแทน 1990) — กรองช่วงวันเข้าพักแล้วจะไม่เจอเลย → ข้ามตัวกรองนี้
            string effStatus = ViewState["FilterStatus"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(effStatus)) effStatus = ddlStatus.SelectedValue;
            if (effStatus == "postponed") return;

            DateTime from, to;
            if (TryParseFilterDate(txtStayFrom.Text, out from))
            {
                if (where != null) where.Append(" AND R.CheckoutDate >= @StayFrom");
                ps.Add(new SqlParameter("@StayFrom", SqlDbType.DateTime) { Value = from.Date });
            }
            if (TryParseFilterDate(txtStayTo.Text, out to))
            {
                if (where != null) where.Append(" AND R.CheckinDate < @StayToExcl");
                ps.Add(new SqlParameter("@StayToExcl", SqlDbType.DateTime) { Value = to.Date.AddDays(1) });
            }
        }

        private string GetOrderByClause()
        {
            switch (ddlSortBy.SelectedValue)
            {
                case "created_asc":
                    return "R.Created_Date ASC";
                case "checkin_asc":
                    return "R.CheckinDate ASC";
                case "checkin_desc":
                    return "R.CheckinDate DESC";
                case "price_desc":
                    return "R.TotalPrice DESC";
                case "price_asc":
                    return "R.TotalPrice ASC";
                default:
                    return "R.Created_Date DESC";
            }
        }

        private Dictionary<int, string> GetAccommodationsForReservations(DataTable reservations)
        {
            Dictionary<int, string> accomDict = new Dictionary<int, string>();

            if (reservations.Rows.Count == 0) return accomDict;

            // Get all reservation IDs
            List<string> ids = new List<string>();
            foreach (DataRow row in reservations.Rows)
            {
                ids.Add(row["ID"].ToString());
            }

            string query = @"SELECT RA.Reservation_ID, A.AccomName
                            FROM Reservation_Accommodation RA
                            INNER JOIN Accommodation A ON A.ID = RA.Accommodation_ID
                            WHERE RA.Reservation_ID IN (" + string.Join(",", ids) + ")";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int resId = reader.GetInt32(0);
                            string accomName = reader.GetString(1);

                            if (accomDict.ContainsKey(resId))
                            {
                                accomDict[resId] += ", " + accomName;
                            }
                            else
                            {
                                accomDict[resId] = accomName;
                            }
                        }
                    }
                }
            }

            return accomDict;
        }

        #endregion

        #region Event Handlers

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            ViewState["CurrentPage"] = 1;
            ViewState["FilterStatus"] = "";
            LoadReservations();
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            ddlStatus.SelectedIndex = 0;
            txtDateFrom.Text = "";
            txtDateTo.Text = "";
            txtStayFrom.Text = "";
            txtStayTo.Text = "";
            ddlSortBy.SelectedIndex = 0;

            ViewState["CurrentPage"] = 1;
            ViewState["FilterStatus"] = "";

            LoadReservations();
        }

        protected void StatCard_Click(object sender, EventArgs e)
        {
            LinkButton btn = (LinkButton)sender;
            string filter = btn.CommandArgument;

            // Reset page
            ViewState["CurrentPage"] = 1;

            // Set filter based on clicked stat
            switch (filter)
            {
                case "all":
                    ViewState["FilterStatus"] = "";
                    ddlStatus.SelectedIndex = 0;
                    // "ทั้งหมด" = ไม่จำกัดวันเข้าพัก (ช่วงเริ่มต้น 7 วันก่อน–30 วันหน้า ถูกตั้งตอนเปิดหน้า)
                    txtStayFrom.Text = "";
                    txtStayTo.Text = "";
                    break;
                case "today":
                    ViewState["FilterStatus"] = "today";
                    ddlStatus.SelectedIndex = 0;
                    // "จองวันนี้" = การจองที่สร้างวันนี้ ไม่ว่าจะเข้าพักวันไหน → ล้างช่วงวันที่ทั้งสองแบบ
                    txtDateFrom.Text = "";
                    txtDateTo.Text = "";
                    txtStayFrom.Text = "";
                    txtStayTo.Text = "";
                    break;
                case "deposit":
                    ViewState["FilterStatus"] = "มัดจำแล้ว";
                    ddlStatus.SelectedValue = "มัดจำแล้ว";
                    break;
                case "checkin":
                    ViewState["FilterStatus"] = "เช็คอินแล้ว";
                    ddlStatus.SelectedValue = "เช็คอินแล้ว";
                    break;
                case "checkout":
                    ViewState["FilterStatus"] = "เช็คเอาท์แล้ว";
                    ddlStatus.SelectedValue = "เช็คเอาท์แล้ว";
                    break;
                case "complete":
                    ViewState["FilterStatus"] = "เสร็จสิ้น";
                    ddlStatus.SelectedValue = "เสร็จสิ้น";
                    break;
                case "cancel":
                    ViewState["FilterStatus"] = "ยกเลิก";
                    ddlStatus.SelectedValue = "ยกเลิก";
                    break;
            }

            LoadReservations();
        }

        protected void btnPrevPage_Click(object sender, EventArgs e)
        {
            int currentPage = (int)ViewState["CurrentPage"];
            if (currentPage > 1)
            {
                ViewState["CurrentPage"] = currentPage - 1;
                LoadReservations();
            }
        }

        protected void btnNextPage_Click(object sender, EventArgs e)
        {
            int currentPage = (int)ViewState["CurrentPage"];
            int totalPages = int.Parse(lblTotalPages.Text);
            if (currentPage < totalPages)
            {
                ViewState["CurrentPage"] = currentPage + 1;
                LoadReservations();
            }
        }

        protected void btnExport_Click(object sender, EventArgs e)
        {
            try
            {
                // Get all data without pagination
                ViewState["CurrentPage"] = 1;
                DataTable dt = GetAllReservationsForExport();

                if (dt.Rows.Count == 0)
                {
                    return;
                }

                // Create CSV
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("เลขที่จอง,วันที่จอง,ชื่อลูกค้า,เบอร์โทร,วันเช็คอิน,วันเช็คเอาท์,จำนวนคืน,ราคารวม,มัดจำ,ค้างชำระ,สถานะ,จองโดย,หมายเหตุ");

                foreach (DataRow row in dt.Rows)
                {
                    // ยอดจากสูตรกลาง (ใส่ไว้ใน GetAllReservationsForExport → ApplyBalances)
                    decimal total = row["TotalPrice"] != DBNull.Value ? Convert.ToDecimal(row["TotalPrice"]) : 0m;
                    decimal deposit = row["Deposit"] != DBNull.Value ? Convert.ToDecimal(row["Deposit"]) : 0m;
                    decimal remain = row["BalDue"] != DBNull.Value ? Convert.ToDecimal(row["BalDue"]) : total - deposit;

                    sb.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
                        row["ID"],
                        Convert.ToDateTime(row["Created_Date"]).ToString("dd/MM/yyyy HH:mm"),
                        EscapeCsvField(row["Name"]?.ToString()),
                        row["Customer_MobilePhone"],
                        FormatDateForExport(row["CheckinDate"]),
                        FormatDateForExport(row["CheckoutDate"]),
                        row["StayDays"],
                        total.ToString("N0"),
                        deposit.ToString("N0"),
                        remain.ToString("N0"),
                        row["Status"],
                        EscapeCsvField(row["Reserve_By"]?.ToString()),
                        EscapeCsvField(row["Remark"]?.ToString())
                    ));
                }

                // Export
                string fileName = $"Reservations_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                Response.Clear();
                Response.ContentType = "text/csv";
                Response.ContentEncoding = Encoding.UTF8;
                Response.AddHeader("Content-Disposition", $"attachment; filename={fileName}");
                Response.BinaryWrite(Encoding.UTF8.GetPreamble());
                Response.Write(sb.ToString());
                Response.End();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Export Error: " + ex.Message);
            }
        }

        private DataTable GetAllReservationsForExport()
        {
            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                StringBuilder whereClause = new StringBuilder("WHERE 1=1");
                List<SqlParameter> parameters = new List<SqlParameter>();

                // Apply same filters
                string search = txtSearch.Text.Trim();
                if (!string.IsNullOrEmpty(search))
                {
                    whereClause.Append(@" AND (ISNULL(C.Name, '') LIKE @Search
                                          OR ISNULL(C.NickName, '') LIKE @Search
                                          OR R.Customer_MobilePhone LIKE @Search
                                          OR CAST(R.ID AS NVARCHAR) LIKE @Search)");
                    parameters.Add(new SqlParameter("@Search", "%" + search + "%"));
                }

                // ตัวกรองสถานะชุดเดียวกับตาราง (รวมการ์ดสถิติที่กดไว้) — เดิมส่งออกตาม dropdown อย่างเดียว
                // และ "ยกเลิก" ส่งออกแค่ 2 สถานะ ไม่ตรงกับที่ตารางแสดง
                string status = ddlStatus.SelectedValue;
                string filterStatus = ViewState["FilterStatus"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(filterStatus)) status = filterStatus;
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "ยกเลิก")
                    {
                        whereClause.Append(" AND " + CancelledFilterSql);
                    }
                    else if (status == "postponed")
                    {
                        whereClause.Append(" AND " + PostponedRowSql());
                    }
                    else if (status == "today")
                    {
                        whereClause.Append(" AND CAST(R.Created_Date AS DATE) = CAST(GETDATE() AS DATE)");
                    }
                    else
                    {
                        whereClause.Append(" AND R.Status = @Status");
                        parameters.Add(new SqlParameter("@Status", status));
                    }
                }

                DateTime dateFrom, dateTo;
                if (!string.IsNullOrEmpty(txtDateFrom.Text) && DateTime.TryParseExact(txtDateFrom.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateFrom))
                {
                    whereClause.Append(" AND R.Created_Date >= @DateFrom");
                    parameters.Add(new SqlParameter("@DateFrom", SqlDbType.DateTime) { Value = dateFrom });
                }

                if (!string.IsNullOrEmpty(txtDateTo.Text) && DateTime.TryParseExact(txtDateTo.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateTo))
                {
                    whereClause.Append(" AND R.Created_Date <= @DateTo");
                    parameters.Add(new SqlParameter("@DateTo", SqlDbType.DateTime) { Value = dateTo.AddDays(1).AddSeconds(-1) });
                }

                AppendStayFilter(whereClause, parameters);

                string query = $@"
                    SELECT R.ID, R.Created_Date, R.Customer_MobilePhone,
                           ISNULL(C.Name, R.Customer_MobilePhone) as Name,
                           R.CheckinDate, R.CheckoutDate, R.StayDays, R.TotalPrice,
                           ISNULL(R.Deposit, 0) as Deposit, R.Status, R.Reserve_By, R.Remark,
                           {ExtraRowColumnsSql()}
                    FROM Reservation R
                    LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                    {whereClause}
                    ORDER BY R.Created_Date DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddRange(parameters.ToArray());
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                }

                // ยอดเงินสูตรกลาง — query เดียวด้วยตัวกรองเดียวกับการส่งออก
                ApplyBalances(dt, whereClause.ToString(), parameters);
            }

            return dt;
        }

        #endregion

        #region GridView Events

        protected void gvReservations_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                DataRowView drv = (DataRowView)e.Row.DataItem;

                // Set status badge
                Label lblStatus = (Label)e.Row.FindControl("lblStatus");
                if (lblStatus != null)
                {
                    string status = drv["Status"]?.ToString() ?? "";
                    lblStatus.Text = status;

                    if (status.Contains("มัดจำ"))
                    {
                        lblStatus.CssClass = "status-badge status-deposit";
                    }
                    else if (status.Contains("เช็คอินแล้ว"))
                    {
                        lblStatus.CssClass = "status-badge status-checkin";
                    }
                    else if (status.Contains("เช็คเอาท์"))
                    {
                        lblStatus.CssClass = "status-badge status-checkout";
                    }
                    else if (status.Contains("เสร็จสิ้น"))
                    {
                        lblStatus.CssClass = "status-badge status-complete";
                    }
                    else if (status.Contains("ยกเลิก") || status.Contains("ลบ"))
                    {
                        lblStatus.CssClass = "status-badge status-cancel";
                    }
                    else
                    {
                        lblStatus.CssClass = "status-badge status-postpone";
                    }
                }

                // ป้ายเสริม: ใบเลื่อน / วิธีเก็บเงินของใบ OTA / สัตว์เลี้ยง (ชุดสี-ข้อความเดียวกับตารางรายวัน)
                Literal litBadges = (Literal)e.Row.FindControl("litBadges");
                if (litBadges != null)
                {
                    var badges = new StringBuilder();
                    bool postponed = drv.Row.Table.Columns.Contains("IsPostponedRow")
                        && drv["IsPostponedRow"] != DBNull.Value && Convert.ToBoolean(drv["IsPostponedRow"]);
                    if (postponed)
                        badges.Append("<span class='mini-badge mb-postpone' title='ยังไม่กำหนดวันเข้าพัก — ไม่นับในยอดค้างชำระ'>⏸ เลื่อน</span>");

                    ReservationBalance rb = null;
                    if (_rowBalances != null && drv["ID"] != DBNull.Value)
                        _rowBalances.TryGetValue(Convert.ToInt32(drv["ID"]), out rb);
                    if (rb != null && rb.IsOta)
                    {
                        if (rb.IsChannelCollect)
                            badges.Append("<span class='mini-badge mb-channel' title='OTA เก็บเงินค่าห้องแล้ว (Channel Collect)'>● OTA เก็บแล้ว</span>");
                        else if (rb.CollectMode == ReservationBalance.ModeHotel)
                            badges.Append("<span class='mini-badge mb-hotel' title='โรงแรมเก็บเงินเอง (Hotel Collect)'>● เก็บหน้างาน</span>");
                        else if (rb.IsCollectUnknown)
                            badges.Append("<span class='mini-badge mb-unknown' title='ยังไม่ชัดว่าใครเก็บเงิน — ตรวจก่อนเก็บเงิน'>○ ยังไม่ชัดใครเก็บ</span>");
                    }

                    int pets = drv.Row.Table.Columns.Contains("PetCount") && drv["PetCount"] != DBNull.Value
                        ? Convert.ToInt32(drv["PetCount"]) : 0;
                    if (pets > 0)
                        badges.Append("<span class='mini-badge mb-pet' title='สัตว์เลี้ยงเข้าพัก'>🐾 " + pets + "</span>");

                    litBadges.Text = badges.Length > 0 ? "<div class='mini-badges'>" + badges + "</div>" : "";
                }

                // Load accommodations
                Literal litAccom = (Literal)e.Row.FindControl("litAccom");
                if (litAccom != null)
                {
                    int resId = Convert.ToInt32(drv["ID"]);
                    string accoms = GetAccommodationsForReservation(resId);

                    // Split and create tags
                    StringBuilder sb = new StringBuilder();
                    foreach (string accom in accoms.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        sb.AppendFormat("<span class='accom-tag'>{0}</span>", HttpUtility.HtmlEncode(accom));
                    }
                    litAccom.Text = sb.Length > 0 ? sb.ToString() : "<span class='accom-tag'>-</span>";
                }

                // Highlight today's check-in
                // (ใบเลื่อนบางใบ CheckinDate เป็น NULL — Convert.ToDateTime(DBNull) โยน exception ทั้งตาราง)
                if (drv["CheckinDate"] != DBNull.Value && Convert.ToDateTime(drv["CheckinDate"]).Date == DateTime.Today)
                {
                    e.Row.CssClass = "today-row";
                }
            }
        }

        private string GetAccommodationsForReservation(int reservationId)
        {
            StringBuilder sb = new StringBuilder();

            string query = @"SELECT A.AccomName
                            FROM Reservation_Accommodation RA
                            INNER JOIN Accommodation A ON A.ID = RA.Accommodation_ID
                            WHERE RA.Reservation_ID = @ResId";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ResId", reservationId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (sb.Length > 0) sb.Append(", ");
                            sb.Append(reader.GetString(0));
                        }
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString() : "-";
        }

        #endregion

        #region Helper Methods

        protected string FormatDate(object date)
        {
            if (date == null || date == DBNull.Value) return "-";

            DateTime dt = Convert.ToDateTime(date);
            if (dt.Year < 2000) return "-";

            return dt.ToString("dd/MM/yyyy");
        }

        private string FormatDateForExport(object date)
        {
            if (date == null || date == DBNull.Value) return "";

            DateTime dt = Convert.ToDateTime(date);
            if (dt.Year < 2000) return "";

            return dt.ToString("dd/MM/yyyy");
        }

        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";

            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }

        #endregion
    }
}
