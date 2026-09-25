using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Globalization;
using System.Text;


namespace Take_Time_BangPhra
{
    /// <summary>
    /// รายการผู้เลื่อนเข้าพัก — ใบจองที่ยังไม่กำหนดวันเข้าพัก (CheckinDate = ค่าแทน 1990-01-01 / ว่าง)
    /// สถานะ "มัดจำแล้ว" (หรือ "รอชำระเงิน") ⇒ ไม่อยู่บนบอร์ด/ไม่กันห้อง แต่ "มัดจำที่รับไว้ยังเป็นหนี้ต่อลูกค้า"
    ///
    /// เดิมหน้านี้ครอบทุกอย่างด้วย try { } catch { Redirect("./Default") } — error ใด ๆ (คอลัมน์/ตารางจาก
    /// migration PHASE8 ยังไม่มี → query สำรอง SELECT * ไม่มีคอลัมน์ OrigCheckin ที่ GridView Eval → DataBinding
    /// HttpException) กลายเป็น "เด้งกลับหน้าแรก" เงียบ ๆ. ตอนนี้: ตรวจโครงสร้างฐานข้อมูลก่อนแล้วสร้าง query ที่มี
    /// คอลัมน์ครบเสมอ (ขาดอะไรใส่ NULL แทน), error แสดงเป็นแถบแจ้งเตือน + บันทึก Logs, หน้าไม่เด้ง
    /// </summary>
    public partial class PostponeList : System.Web.UI.Page
    {
        private readonly string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private RescheduleService _rescheduleService;

        private static readonly CultureInfo EnCulture = CultureInfo.InvariantCulture;

        /// <summary>อายุมัดจำของใบที่เลื่อน (วัน) — ตั้งได้ที่ System_Config คีย์ Postpone_Expire_Days (ค่าเริ่มต้น 365)</summary>
        protected int ExpireDays = 365;
        /// <summary>เตือน "ใกล้หมดอายุ" ก่อนครบกี่วัน — คีย์ Postpone_Expire_Warn_Days (ค่าเริ่มต้น 30)</summary>
        protected int WarnDays = 30;

        // ระดับอายุการเลื่อน (ใช้ทั้งตัวกรองและสีแถว)
        private const int LevelUnknown = -1;
        private const int LevelNormal = 0;
        private const int LevelOld90 = 1;
        private const int LevelOld180 = 2;
        private const int LevelExpiring = 3;
        private const int LevelExpired = 4;

        protected void Page_Load(object sender, EventArgs e)
        {
            Page.MaintainScrollPositionOnPostBack = true;
            _rescheduleService = new RescheduleService(conn);
            LoadPolicy();

            bool loggedIn = Session["permission"]?.ToString() == "True";

            // Handle AJAX request for reschedule history (ต้องล็อกอินก่อน — เดิมเปิดให้ทุกคนดูประวัติ/ชื่อพนักงานได้)
            if (Request.QueryString["action"] == "gethistory")
            {
                if (!loggedIn)
                {
                    Response.Clear();
                    Response.StatusCode = 401;
                    Response.End();
                    return;
                }
                HandleGetHistory();
                return;
            }

            // ยังไม่ล็อกอิน → ไปหน้าล็อกอิน (อยู่นอก try — ThreadAbortException ของ Redirect ไม่ถูกจับ/แปลงเป็นอย่างอื่น)
            if (!loggedIn)
            {
                Response.Redirect("~/Admin/Login", true);
                return;
            }

            if (!IsPostBack)
            {
                LoadPostponedReservations();
            }
        }

        private void LoadPolicy()
        {
            try
            {
                ExpireDays = AppCfg.GetInt("Postpone_Expire_Days", 365);
                WarnDays = AppCfg.GetInt("Postpone_Expire_Warn_Days", 30);
            }
            catch { /* ใช้ค่าเริ่มต้น */ }
            if (ExpireDays <= 0) ExpireDays = 365;
            if (WarnDays < 0) WarnDays = 0;
            if (WarnDays > ExpireDays) WarnDays = ExpireDays;
        }

        #region Schema detection (ครั้งเดียวแล้ว cache — ขาดบางอย่างตรวจใหม่ทุก 5 นาที เผื่อเพิ่งรัน migration)

        private sealed class SchemaInfo
        {
            public bool HasIsPostponed, HasPostponedDate, HasRescheduleCount, HasHistory, HasNickName;
            public DateTime CheckedAt;
            public bool Complete
            {
                get { return HasIsPostponed && HasPostponedDate && HasRescheduleCount && HasHistory && HasNickName; }
            }
        }

        private static readonly object _schemaLock = new object();
        private static SchemaInfo _schema;

        private SchemaInfo GetSchema()
        {
            lock (_schemaLock)
            {
                if (_schema != null && (_schema.Complete || DateTime.Now - _schema.CheckedAt < TimeSpan.FromMinutes(5)))
                    return _schema;
            }

            var s = new SchemaInfo { CheckedAt = DateTime.Now };
            DataTable dt = new code().DatabaseQuerySafe(conn,
                @"SELECT COL_LENGTH('Reservation', 'IsPostponed') AS IsP,
                         COL_LENGTH('Reservation', 'PostponedDate') AS PD,
                         COL_LENGTH('Reservation', 'RescheduleCount') AS RC,
                         OBJECT_ID('Reservation_Reschedule_History') AS RH,
                         COL_LENGTH('Customer', 'NickName') AS NN");
            if (dt != null && dt.Rows.Count > 0)
            {
                s.HasIsPostponed = dt.Rows[0]["IsP"] != DBNull.Value;
                s.HasPostponedDate = dt.Rows[0]["PD"] != DBNull.Value;
                s.HasRescheduleCount = dt.Rows[0]["RC"] != DBNull.Value;
                s.HasHistory = dt.Rows[0]["RH"] != DBNull.Value;
                s.HasNickName = dt.Rows[0]["NN"] != DBNull.Value;
            }
            lock (_schemaLock) { _schema = s; }
            return s;
        }

        private static void InvalidateSchema()
        {
            lock (_schemaLock) { _schema = null; }
        }

        #endregion

        /// <summary>
        /// สร้าง query ที่ให้คอลัมน์ชุดเดียวกันเสมอ ไม่ว่าจะรัน migration ครบหรือไม่ — คอลัมน์ที่ไม่มีใส่ NULL/0 แทน
        /// ⇒ GridView ผูกข้อมูลได้เสมอ. ใบที่เลื่อน = ยังไม่มีวันเข้าพัก (ค่าแทน 1990-01-01 / 0001-01-01 / ว่าง)
        /// ใช้ '19910101' (รูปแบบ ISO ไม่มีขีด) ⇒ ไม่ขึ้นกับ DATEFORMAT และไม่ล้นช่วงของคอลัมน์ datetime
        /// (เทียบ '0001-01-01' ตรง ๆ กับ datetime = error out-of-range)
        /// </summary>
        private static string BuildSql(SchemaInfo s)
        {
            bool h = s.HasHistory;
            var sb = new StringBuilder();
            sb.Append(@"
                SELECT
                    R.ID,
                    R.Customer_MobilePhone,
                    ISNULL(R.TotalPrice, 0) AS TotalPrice,
                    ISNULL(R.Deposit, 0) AS Deposit,
                    R.Remark,
                    R.Created_Date,
                    R.StayDays,
                    R.Status,");
            sb.Append(s.HasIsPostponed ? " CAST(ISNULL(R.IsPostponed, 0) AS bit) AS IsPostponed," : " CAST(0 AS bit) AS IsPostponed,");
            sb.Append(s.HasPostponedDate ? " R.PostponedDate," : " CAST(NULL AS datetime) AS PostponedDate,");
            sb.Append(s.HasRescheduleCount ? " ISNULL(R.RescheduleCount, 0) AS RescheduleCount," : " 0 AS RescheduleCount,");
            sb.Append(@"
                    C.Name,");
            sb.Append(s.HasNickName ? " C.NickName," : " CAST(NULL AS nvarchar(200)) AS NickName,");
            if (h)
            {
                sb.Append(@"
                    ISNULL(HC.TotalReschedules, 0) AS TotalReschedules,
                    HL.Reason AS LastRescheduleReason,
                    HL.RescheduledDate AS LastRescheduledDate,
                    HL.RescheduledBy_Name AS LastRescheduledBy,
                    HO.OldCheckinDate AS OrigCheckin,
                    HO.OldCheckoutDate AS OrigCheckout,
                    HO.OldStayDays AS OrigStayDays,
                    HE.EpisodeStart AS FirstPostponeDate");
            }
            else
            {
                sb.Append(@"
                    0 AS TotalReschedules,
                    CAST(NULL AS nvarchar(500)) AS LastRescheduleReason,
                    CAST(NULL AS datetime) AS LastRescheduledDate,
                    CAST(NULL AS nvarchar(200)) AS LastRescheduledBy,
                    CAST(NULL AS datetime) AS OrigCheckin,
                    CAST(NULL AS datetime) AS OrigCheckout,
                    CAST(NULL AS int) AS OrigStayDays,
                    CAST(NULL AS datetime) AS FirstPostponeDate");
            }

            // LEFT (OUTER APPLY TOP 1): ใบที่ลูกค้าถูกย้าย/เปลี่ยนเบอร์ยังต้องแสดง (เดิม INNER JOIN ทำให้ใบหายไปจากรายการ)
            // และเบอร์ซ้ำในตาราง Customer ไม่ทำให้ใบจองซ้ำเป็นหลายแถว
            sb.Append(@"
                FROM [dbo].[Reservation] R
                OUTER APPLY (SELECT TOP 1 Cx.Name" + (s.HasNickName ? ", Cx.NickName" : "") + @"
                               FROM [dbo].[Customer] Cx
                              WHERE Cx.MobilePhone = R.Customer_MobilePhone) C");
            if (h)
            {
                sb.Append(@"
                -- จำนวนครั้งที่เลื่อน = แถว POSTPONE แยกตามนาที (ข้อมูลเก่าปุ่มเลื่อนบันทึก 2 แถวในวินาทีเดียวกัน → นับครั้งเดียว)
                OUTER APPLY (SELECT COUNT(DISTINCT CASE WHEN RH.RescheduleType = 'POSTPONE'
                                                        THEN CONVERT(varchar(16), RH.RescheduledDate, 120) END) AS TotalReschedules
                               FROM [dbo].[Reservation_Reschedule_History] RH
                              WHERE RH.Reservation_ID = R.ID) HC
                -- การเลื่อนครั้งล่าสุด (วันที่ทำเรื่อง / โดยใคร / เหตุผล)
                OUTER APPLY (SELECT TOP 1 RH.Reason, RH.RescheduledDate, RH.RescheduledBy_Name
                               FROM [dbo].[Reservation_Reschedule_History] RH
                              WHERE RH.Reservation_ID = R.ID AND RH.RescheduleType = 'POSTPONE'
                              ORDER BY RH.RescheduledDate DESC, RH.ID DESC) HL
                -- การจองเดิมก่อนเลื่อน = แถวแรกที่มีวันจริง (ข้ามแถวที่ไม่มีวัน/ค่าแทน 1990)
                OUTER APPLY (SELECT TOP 1 RH.OldCheckinDate, RH.OldCheckoutDate, RH.OldStayDays
                               FROM [dbo].[Reservation_Reschedule_History] RH
                              WHERE RH.Reservation_ID = R.ID
                                AND RH.OldCheckinDate IS NOT NULL AND RH.OldCheckinDate >= '19910101'
                              ORDER BY RH.RescheduledDate ASC, RH.ID ASC) HO
                -- เริ่มเลื่อนรอบปัจจุบัน = POSTPONE แรกหลังการลงวันครั้งล่าสุด (นับอายุมัดจำ)
                OUTER APPLY (SELECT MIN(RH.RescheduledDate) AS EpisodeStart
                               FROM [dbo].[Reservation_Reschedule_History] RH
                              WHERE RH.Reservation_ID = R.ID AND RH.RescheduleType = 'POSTPONE'
                                AND RH.RescheduledDate > ISNULL((SELECT MAX(RD.RescheduledDate)
                                                                   FROM [dbo].[Reservation_Reschedule_History] RD
                                                                  WHERE RD.Reservation_ID = R.ID
                                                                    AND RD.RescheduleType = 'DATE_CHANGE'), '19000101')) HE");
            }
            sb.Append(@"
                WHERE R.Status IN (N'มัดจำแล้ว', N'รอชำระเงิน')
                  AND (R.CheckinDate IS NULL OR R.CheckinDate < '19910101')
                ORDER BY R.ID DESC");
            return sb.ToString();
        }

        private DataTable QueryPostponed()
        {
            var db = new code();
            SchemaInfo s;
            try
            {
                s = GetSchema();
            }
            catch (Exception ex)
            {
                LogError("PostponeList - Schema check error", ex);
                s = new SchemaInfo { CheckedAt = DateTime.MinValue };   // ถือว่าไม่มีส่วนเสริม → query พื้นฐาน
            }

            try
            {
                return db.DatabaseQuerySafe(conn, BuildSql(s));
            }
            catch (Exception ex)
            {
                // มีส่วนเสริมแต่ query พัง (เช่น migration รันไม่ครบ) → ลองแบบพื้นฐานอีกครั้ง ถ้ายังพังโยนต่อ
                if (!s.HasIsPostponed && !s.HasPostponedDate && !s.HasRescheduleCount && !s.HasHistory && !s.HasNickName)
                    throw;
                LogError("PostponeList - Query error (retry basic)", ex);
                InvalidateSchema();
                return db.DatabaseQuerySafe(conn, BuildSql(new SchemaInfo()));
            }
        }

        private void LoadPostponedReservations()
        {
            DataTable dt;
            try
            {
                dt = QueryPostponed() ?? new DataTable();
            }
            catch (Exception ex)
            {
                LogError("PostponeList - Load error", ex);
                ShowMessage("error", "โหลดรายการเลื่อนเข้าพักไม่สำเร็จ: " + ex.Message
                    + " — ตรวจว่ารัน Database/PHASE8_Migration_01_Reschedule_History.sql แล้ว (รายละเอียดอยู่ใน Logs)");
                BindGrid(null);
                SetSummary(0, 0, 0m, 0, 0, 0);
                return;
            }

            EnsureComputedColumns(dt);
            try
            {
                EnrichRows(dt);
            }
            catch (Exception ex)
            {
                // เสริมข้อมูลไม่สำเร็จ — ยังแสดงรายการได้ (คอลัมน์เสริมถูกเพิ่มไว้ก่อนแล้ว)
                LogError("PostponeList - Enrich error", ex);
                ShowMessage("warn", "คำนวณยอดมัดจำ/อายุการเลื่อนบางส่วนไม่สำเร็จ: " + ex.Message);
            }

            // สรุปจากทั้งหมด (ก่อนกรอง)
            int total = dt.Rows.Count;
            decimal held = 0m;
            int expiring = 0, expired = 0, oldest = 0;
            foreach (DataRow r in dt.Rows)
            {
                held += r["Held"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Held"]);
                int lv = r["Level"] == DBNull.Value ? LevelUnknown : Convert.ToInt32(r["Level"]);
                if (lv == LevelExpiring) expiring++;
                if (lv == LevelExpired) expired++;
                int age = r["AgeDays"] == DBNull.Value ? -1 : Convert.ToInt32(r["AgeDays"]);
                if (age > oldest) oldest = age;
            }

            DataTable view = ApplyFilter(dt);
            BindGrid(view);
            SetSummary(total, view.Rows.Count, held, expiring, expired, oldest);
        }

        /// <summary>
        /// เพิ่มคอลัมน์คำนวณ: ยอดมัดจำที่รับจริง (ReservationBalance — Payment_History ก่อน, ไม่มีค่อยใช้ Deposit),
        /// วันเริ่มเลื่อน, อายุ (วัน), ระดับ (ปกติ/เกิน 90/เกิน 180/ใกล้หมดอายุ/หมดอายุ)
        /// </summary>
        private static void EnsureComputedColumns(DataTable dt)
        {
            if (!dt.Columns.Contains("Held")) dt.Columns.Add("Held", typeof(decimal));
            if (!dt.Columns.Contains("HeldNote")) dt.Columns.Add("HeldNote", typeof(string));
            if (!dt.Columns.Contains("StartDate")) dt.Columns.Add("StartDate", typeof(DateTime));
            if (!dt.Columns.Contains("StartSource")) dt.Columns.Add("StartSource", typeof(string));
            if (!dt.Columns.Contains("AgeDays")) dt.Columns.Add("AgeDays", typeof(int));
            if (!dt.Columns.Contains("Level")) dt.Columns.Add("Level", typeof(int));
        }

        private void EnrichRows(DataTable dt)
        {
            EnsureComputedColumns(dt);

            // ยอดที่รับจริง — สูตรกลางเดียวกับบอร์ด/รายการจอง
            Dictionary<int, ReservationBalance> balances = null;
            var ids = new List<int>();
            foreach (DataRow r in dt.Rows)
            {
                if (r["ID"] != DBNull.Value) ids.Add(Convert.ToInt32(r["ID"]));
            }
            if (ids.Count > 0)
            {
                try
                {
                    // ids เป็นตัวเลขจากฐานข้อมูล (ไม่ใช่ข้อความผู้ใช้) — แบบเดียวกับ ReservationList
                    balances = ReservationBalance.LoadMany(conn,
                        "r.ID IN (" + string.Join(",", ids.Select(i => i.ToString(EnCulture))) + ")", null);
                }
                catch (Exception ex)
                {
                    LogError("PostponeList - ReservationBalance error", ex);
                    balances = null;
                }
            }

            DateTime today = DateTime.Today;
            foreach (DataRow r in dt.Rows)
            {
                int id = r["ID"] == DBNull.Value ? 0 : Convert.ToInt32(r["ID"]);
                decimal depositCol = r["Deposit"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Deposit"]);

                ReservationBalance b = null;
                if (balances != null) balances.TryGetValue(id, out b);
                if (b != null)
                {
                    if (b.IsChannelCollect)
                    {
                        // OTA เก็บเงินไว้ — ส่วนที่ลูกค้าจ่ายโรงแรมเอง + ยอดที่ OTA ถือแทน
                        r["Held"] = b.Received;
                        r["HeldNote"] = "OTA เก็บเงิน (Channel Collect)";
                    }
                    else if (b.LedgerRows > 0)
                    {
                        r["Held"] = b.PaidLedger;
                        r["HeldNote"] = Math.Abs(b.PaidLedger - depositCol) > 0.009m
                            ? "ตามรายการรับเงิน (ช่องมัดจำ " + depositCol.ToString("N0", EnCulture) + ")"
                            : "ตามรายการรับเงิน";
                    }
                    else
                    {
                        r["Held"] = b.Received;
                        r["HeldNote"] = b.IsCollectUnknown
                            ? "OTA ยังไม่ระบุว่าใครเก็บเงิน"
                            : "ตามยอดมัดจำ (ไม่มีรายการรับเงิน)";
                    }
                }
                else
                {
                    r["Held"] = depositCol;
                    r["HeldNote"] = "ตามยอดมัดจำ";
                }

                // วันเริ่มเลื่อนรอบนี้: วันที่ในประวัติ (POSTPONE แรกหลังลงวันล่าสุด) หรือ PostponedDate — เอาที่เก่ากว่า
                // (PostponedDate ของข้อมูลเก่าเคยถูกรีเซ็ตทุกครั้งที่มีคนบันทึกใบ) ไม่มีทั้งคู่ → วันที่จองเข้ามา
                DateTime? start = null;
                string src = "";
                DateTime? fromHist = AsDate(r["FirstPostponeDate"]);
                DateTime? fromFlag = AsDate(r["PostponedDate"]);
                if (fromHist.HasValue && (!fromFlag.HasValue || fromHist.Value <= fromFlag.Value))
                {
                    start = fromHist; src = "วันที่ขอเลื่อน";
                }
                else if (fromFlag.HasValue)
                {
                    start = fromFlag; src = "วันที่ขอเลื่อน";
                }
                else
                {
                    DateTime? created = AsDate(r["Created_Date"]);
                    if (created.HasValue) { start = created; src = "วันที่จอง (ไม่มีประวัติการเลื่อน)"; }
                }

                if (start.HasValue)
                {
                    int age = (int)Math.Floor((today - start.Value.Date).TotalDays);
                    if (age < 0) age = 0;
                    r["StartDate"] = start.Value;
                    r["StartSource"] = src;
                    r["AgeDays"] = age;
                    r["Level"] = LevelFor(age);
                }
                else
                {
                    r["StartDate"] = DBNull.Value;
                    r["StartSource"] = "";
                    r["AgeDays"] = DBNull.Value;
                    r["Level"] = LevelUnknown;
                }
            }
        }

        private int LevelFor(int ageDays)
        {
            if (ageDays >= ExpireDays) return LevelExpired;
            if (WarnDays > 0 && ageDays >= ExpireDays - WarnDays) return LevelExpiring;
            if (ageDays > 180) return LevelOld180;
            if (ageDays > 90) return LevelOld90;
            return LevelNormal;
        }

        /// <summary>กรองตามช่องค้นหา (เลขที่จอง / ชื่อ / ชื่อเล่น / เบอร์ / หมายเหตุ) + ตัวกรองอายุ</summary>
        private DataTable ApplyFilter(DataTable dt)
        {
            string q = (txtSearch.Text ?? "").Trim();
            string filter = ddlFilter.SelectedValue ?? "all";
            if (q.Length == 0 && filter == "all") return dt;

            string qDigits = new string(q.Where(char.IsDigit).ToArray());
            DataTable view = dt.Clone();
            foreach (DataRow r in dt.Rows)
            {
                int lv = r["Level"] == DBNull.Value ? LevelUnknown : Convert.ToInt32(r["Level"]);
                if (filter == "old90" && lv < LevelOld90) continue;
                if (filter == "old180" && lv < LevelOld180) continue;
                if (filter == "expiring" && lv < LevelExpiring) continue;
                if (filter == "pending" && Str(r["Status"]) != "รอชำระเงิน") continue;

                if (q.Length > 0)
                {
                    string phone = Str(r["Customer_MobilePhone"]);
                    string phoneDigits = new string(phone.Where(char.IsDigit).ToArray());
                    bool hit = Str(r["ID"]) == q
                        || Contains(Str(r["Name"]), q)
                        || Contains(Str(r["NickName"]), q)
                        || Contains(phone, q)
                        || (qDigits.Length >= 3 && phoneDigits.Contains(qDigits))
                        || Contains(Str(r["Remark"]), q)
                        || Contains(Str(r["LastRescheduleReason"]), q);
                    if (!hit) continue;
                }
                view.ImportRow(r);
            }
            return view;
        }

        private void BindGrid(DataTable dt)
        {
            GridView1.DataSource = dt;
            GridView1.DataBind();
        }

        private void SetSummary(int total, int shown, decimal held, int expiring, int expired, int oldest)
        {
            lblPostponeCount.Text = total.ToString("N0", EnCulture);
            lblSummaryCount.Text = lblPostponeCount.Text;
            lblTotalHeld.Text = held.ToString("N2", EnCulture);
            lblExpiringCount.Text = expiring.ToString("N0", EnCulture);
            lblExpiredCount.Text = expired.ToString("N0", EnCulture);
            lblOldestDays.Text = oldest.ToString("N0", EnCulture);
            lblShown.Text = shown == total
                ? ""
                : "แสดง " + shown.ToString("N0", EnCulture) + " จาก " + total.ToString("N0", EnCulture) + " รายการ";
            litPolicy.Text = HttpUtility.HtmlEncode(
                "อายุมัดจำของใบที่เลื่อน " + ExpireDays.ToString("N0", EnCulture) + " วัน นับจากวันที่ขอเลื่อน"
                + (WarnDays > 0 ? " (เตือนล่วงหน้า " + WarnDays.ToString("N0", EnCulture) + " วัน)" : "")
                + " — ระบบไม่ยกเลิกให้อัตโนมัติ ตั้งค่าได้ที่คีย์ Postpone_Expire_Days / Postpone_Expire_Warn_Days");
        }

        protected void GridView1_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;
            var drv = e.Row.DataItem as DataRowView;
            if (drv == null) return;
            int lv = Int(drv, "Level", LevelUnknown);
            if (lv == LevelExpired) e.Row.CssClass += " row-expired";
            else if (lv == LevelExpiring) e.Row.CssClass += " row-expiring";
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            LoadPostponedReservations();
        }

        protected void btnClearSearch_Click(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            ddlFilter.SelectedValue = "all";
            LoadPostponedReservations();
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadPostponedReservations();
        }

        /// <summary>
        /// Cancel with reason (triggered from modal via hidden button)
        /// </summary>
        protected void btnCancelWithReason_Click(object sender, EventArgs e)
        {
            int reservationId;
            if (!int.TryParse(hdnCancelReservationId.Value, out reservationId) || reservationId <= 0)
            {
                ShowMessage("error", "ไม่พบข้อมูลการจองที่จะยกเลิก");
                LoadPostponedReservations();
                return;
            }

            string reason = (hdnCancelReason.Value ?? "").Trim();
            if (reason.Length == 0) reason = "ยกเลิกจากหน้ารายการเลื่อนเข้าพัก";
            string adminName = Session["User"]?.ToString();
            string userName = Session["UserName"]?.ToString() ?? adminName ?? "User";

            try
            {
                short parsed;
                short? adminId = short.TryParse(Session["UserID"]?.ToString(), out parsed) ? (short?)parsed : null;

                // ยอดที่ถือไว้ก่อนยกเลิก — แจ้งผู้ใช้/บันทึก log (ไม่แตะบัญชี)
                decimal held = 0m;
                try
                {
                    ReservationBalance b = ReservationBalance.Load(conn, reservationId);
                    if (b != null) held = b.LedgerRows > 0 ? b.PaidLedger : b.Received;
                }
                catch { }

                bool ok = _rescheduleService.CancelPostpone(reservationId, adminId, userName, reason);
                if (!ok)
                {
                    ShowMessage("warn", "ไม่ได้ยกเลิก: การจอง #" + reservationId.ToString(EnCulture)
                        + " ไม่อยู่ในสถานะเลื่อนเข้าพักแล้ว (อาจถูกลงวันใหม่/ยกเลิกไปแล้ว) — รีเฟรชรายการ");
                    LoadPostponedReservations();
                    return;
                }

                try
                {
                    new code().Logs(conn, "PostponeList - Cancel",
                        "Reservation " + reservationId.ToString(EnCulture) + ", held " + held.ToString("N2", EnCulture)
                        + ", reason: " + reason, userName);
                }
                catch { }

                string msg = "ยกเลิกรายการเลื่อนเข้าพัก #" + reservationId.ToString(EnCulture) + " เรียบร้อยแล้ว";
                if (held > 0m)
                {
                    msg += " — มัดจำ " + held.ToString("N2", EnCulture) + " บาท ยังเป็นเงินรับล่วงหน้า (หนี้สินต่อลูกค้า) ในระบบบัญชี"
                        + " หากคืนเงินหรือริบมัดจำ ต้องทำรายการคืนเงิน/บันทึกรายได้แยกต่างหาก";
                    ShowMessage("warn", msg);
                }
                else
                {
                    ShowMessage("ok", msg);
                }
            }
            catch (Exception ex)
            {
                LogError("PostponeList - Cancel error (Reservation " + reservationId.ToString(EnCulture) + ")", ex);
                ShowMessage("error", "ยกเลิกไม่สำเร็จ: " + ex.Message);
            }

            hdnCancelReservationId.Value = "";
            hdnCancelReason.Value = "";
            LoadPostponedReservations();
        }

        /// <summary>
        /// Handle AJAX request for reschedule history (JSON ผ่าน Json.NET — เดิมต่อสตริงเอง ขึ้นบรรทัดใหม่ในเหตุผลแล้ว JSON พัง)
        /// </summary>
        private void HandleGetHistory()
        {
            Response.Clear();
            Response.ContentType = "application/json";

            string json = "[]";
            try
            {
                int reservationId;
                if (int.TryParse(Request.QueryString["rid"], out reservationId) && reservationId > 0)
                {
                    DataTable history = _rescheduleService.GetRescheduleHistory(reservationId);
                    var items = new List<Dictionary<string, string>>();
                    foreach (DataRow row in history.Rows)
                    {
                        var item = new Dictionary<string, string>();
                        item["Type"] = Str(row["RescheduleType"]);
                        item["Reason"] = Str(row["Reason"]);
                        item["Admin"] = Str(row["RescheduledBy_Name"]);
                        item["Date"] = row["RescheduledDate"] != DBNull.Value
                            ? Convert.ToDateTime(row["RescheduledDate"]).ToString("dd/MM/yyyy HH:mm", EnCulture) : "";
                        item["OldDate"] = RealDate(row["OldCheckinDate"]);
                        item["NewDate"] = RealDate(row["NewCheckinDate"]);
                        items.Add(item);
                    }
                    json = Newtonsoft.Json.JsonConvert.SerializeObject(items);
                }
            }
            catch (Exception ex)
            {
                LogError("PostponeList - GetHistory error", ex);
                json = "[]";
            }

            Response.Write(json);
            Response.End();
        }

        #region Helpers (ใช้ใน markup — encode ทุกค่าที่มาจากฐานข้อมูล)

        private void ShowMessage(string kind, string text)
        {
            string css = kind == "error" ? "msg-error" : (kind == "warn" ? "msg-warn" : "msg-ok");
            pnlMessage.Visible = true;
            pnlMessage.CssClass = "msg-banner " + css;
            litMessage.Text = HttpUtility.HtmlEncode(text);
        }

        private void LogError(string action, Exception ex)
        {
            try
            {
                string detail = ex.ToString();
                if (detail.Length > 3500) detail = detail.Substring(0, 3500);
                new code().Logs(conn, action, detail, Session?["User"]?.ToString() ?? "SYSTEM");
            }
            catch { /* log ไม่ได้ก็ไม่ให้หน้าพัง */ }
        }

        private static string Str(object v)
        {
            if (v == null || v == DBNull.Value) return "";
            return Convert.ToString(v, EnCulture) ?? "";
        }

        private static bool Contains(string hay, string needle)
        {
            return !string.IsNullOrEmpty(hay) && hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static DateTime? AsDate(object v)
        {
            if (v == null || v == DBNull.Value) return null;
            try { return Convert.ToDateTime(v); }
            catch { return null; }
        }

        private static string RealDate(object v)
        {
            DateTime? d = AsDate(v);
            return d.HasValue && d.Value.Year > 1990 ? d.Value.ToString("dd/MM/yyyy", EnCulture) : "";
        }

        private static object Val(object dataItem, string col)
        {
            var drv = dataItem as DataRowView;
            if (drv == null || drv.Row == null || !drv.Row.Table.Columns.Contains(col)) return null;
            object v = drv.Row[col];
            return v == DBNull.Value ? null : v;
        }

        private static int Int(object dataItem, string col, int def)
        {
            object v = Val(dataItem, col);
            if (v == null) return def;
            try { return Convert.ToInt32(v); }
            catch { return def; }
        }

        private static decimal Dec(object dataItem, string col)
        {
            object v = Val(dataItem, col);
            if (v == null) return 0m;
            try { return Convert.ToDecimal(v); }
            catch { return 0m; }
        }

        protected static string H(object v)
        {
            return HttpUtility.HtmlEncode(Str(v));
        }

        /// <summary>ลิงก์ "ลงจอง" — เข้าหน้าแก้ไขการจองเดิม (เลขเดิม มัดจำ/ใบเสร็จเดิมอยู่ครบ) เพื่อเลือกวันใหม่
        /// เบอร์ต้อง UrlEncode ("+852…" ไม่งั้น + กลายเป็นช่องว่าง)</summary>
        protected string EditUrl(object dataItem)
        {
            string id = Str(Val(dataItem, "ID"));
            string phone = Str(Val(dataItem, "Customer_MobilePhone"));
            return ResolveUrl("~/Reserve") + "?command=edit&id=" + HttpUtility.UrlEncode(id)
                + "&check=" + HttpUtility.UrlEncode(phone);
        }

        protected string CustomerHtml(object dataItem)
        {
            string name = Str(Val(dataItem, "Name"));
            string nick = Str(Val(dataItem, "NickName"));
            string phone = Str(Val(dataItem, "Customer_MobilePhone"));
            var sb = new StringBuilder();
            sb.Append(name.Length > 0
                ? "<b>" + HttpUtility.HtmlEncode(name) + "</b>"
                : "<span class='muted'>(ไม่พบข้อมูลลูกค้า)</span>");
            if (nick.Length > 0 && nick != name)
                sb.Append("<br/><small class='muted'>").Append(HttpUtility.HtmlEncode(nick)).Append("</small>");
            if (phone.Length > 0)
            {
                bool dialable = Take_Time_BangPhra.Services.GuestPhone.IsUsable(phone);
                sb.Append("<br/>");
                if (dialable)
                    sb.Append("<a class=\"phone-link\" href=\"tel:").Append(HttpUtility.HtmlEncode(phone)).Append("\">")
                      .Append(HttpUtility.HtmlEncode(phone)).Append("</a>");
                else
                    sb.Append("<small class='muted'>").Append(HttpUtility.HtmlEncode(phone)).Append("</small>");
            }
            if (Str(Val(dataItem, "Status")) == "รอชำระเงิน")
                sb.Append("<br/><span class='badge badge-grey'>รอชำระเงิน</span>");
            return sb.ToString();
        }

        protected string HeldHtml(object dataItem)
        {
            decimal held = Dec(dataItem, "Held");
            decimal total = Dec(dataItem, "TotalPrice");
            string note = Str(Val(dataItem, "HeldNote"));
            var sb = new StringBuilder();
            sb.Append("<b class='").Append(held > 0m ? "money-held" : "muted").Append("'>")
              .Append(held.ToString("N2", EnCulture)).Append("</b>");
            if (note.Length > 0)
                sb.Append("<br/><small class='muted'>").Append(HttpUtility.HtmlEncode(note)).Append("</small>");
            sb.Append("<br/><small class='muted'>ยอดรวม ").Append(total.ToString("N0", EnCulture)).Append("</small>");
            return sb.ToString();
        }

        protected string AgeHtml(object dataItem)
        {
            int lv = Int(dataItem, "Level", LevelUnknown);
            object ageObj = Val(dataItem, "AgeDays");
            if (ageObj == null || lv == LevelUnknown) return "<span class='muted'>-</span>";
            int age = Convert.ToInt32(ageObj);
            DateTime? start = AsDate(Val(dataItem, "StartDate"));
            string css = lv >= LevelExpiring ? "age-red" : (lv == LevelOld180 ? "age-orange" : (lv == LevelOld90 ? "age-amber" : "age-normal"));

            var sb = new StringBuilder();
            sb.Append("<span class='").Append(css).Append("'>").Append(age.ToString("N0", EnCulture)).Append(" วัน</span>");
            if (lv == LevelExpired)
                sb.Append("<br/><span class='badge badge-red'>หมดอายุ</span>");
            else if (lv == LevelExpiring)
                sb.Append("<br/><span class='badge badge-orange'>ใกล้หมดอายุ (อีก ")
                  .Append((ExpireDays - age).ToString("N0", EnCulture)).Append(" วัน)</span>");
            if (start.HasValue)
            {
                sb.Append("<br/><small class=\"muted\" title=\"")
                  .Append(HttpUtility.HtmlEncode("นับจาก" + Str(Val(dataItem, "StartSource"))))
                  .Append("\">ตั้งแต่ ").Append(start.Value.ToString("dd/MM/yyyy", EnCulture))
                  .Append("<br/>ครบ ").Append(start.Value.Date.AddDays(ExpireDays).ToString("dd/MM/yyyy", EnCulture))
                  .Append("</small>");
            }
            return sb.ToString();
        }

        protected string OrigHtml(object dataItem)
        {
            DateTime? ci = AsDate(Val(dataItem, "OrigCheckin"));
            if (!ci.HasValue) return "<span class='muted'>ไม่มีประวัติ</span>";
            DateTime? co = AsDate(Val(dataItem, "OrigCheckout"));
            object nights = Val(dataItem, "OrigStayDays");
            var sb = new StringBuilder();
            sb.Append("<b>").Append(ci.Value.ToString("dd/MM/yyyy", EnCulture)).Append("</b>");
            if (co.HasValue && co.Value.Year > 1990) sb.Append(" → ").Append(co.Value.ToString("dd/MM/yyyy", EnCulture));
            sb.Append("<br/><small class='muted'>").Append(nights == null ? "-" : HttpUtility.HtmlEncode(Str(nights))).Append(" คืน</small>");
            return sb.ToString();
        }

        protected string RequestedHtml(object dataItem)
        {
            DateTime? d = AsDate(Val(dataItem, "LastRescheduledDate")) ?? AsDate(Val(dataItem, "PostponedDate"));
            string by = Str(Val(dataItem, "LastRescheduledBy"));
            var sb = new StringBuilder();
            sb.Append(d.HasValue ? d.Value.ToString("dd/MM/yyyy HH:mm", EnCulture) : "<span class='muted'>-</span>");
            if (by.Length > 0) sb.Append("<br/><small class='muted'>โดย ").Append(HttpUtility.HtmlEncode(by)).Append("</small>");
            DateTime? created = AsDate(Val(dataItem, "Created_Date"));
            if (created.HasValue)
                sb.Append("<br/><small class='muted'>จองเมื่อ ").Append(created.Value.ToString("dd/MM/yyyy", EnCulture)).Append("</small>");
            return sb.ToString();
        }

        protected string NoteHtml(object dataItem)
        {
            string remark = Str(Val(dataItem, "Remark"));
            string reason = Str(Val(dataItem, "LastRescheduleReason"));
            var sb = new StringBuilder();
            if (reason.Length > 0)
                sb.Append("<span class='reason-text'>เหตุผล: ").Append(HttpUtility.HtmlEncode(reason)).Append("</span>");
            if (remark.Length > 0)
            {
                if (sb.Length > 0) sb.Append("<br/>");
                sb.Append("<span class='remark-text'>").Append(HttpUtility.HtmlEncode(remark)).Append("</span>");
            }
            return sb.Length > 0 ? sb.ToString() : "<span class='muted'>-</span>";
        }

        protected string RescheduleCountText(object dataItem)
        {
            // จำนวนครั้งที่ขอเลื่อน (จากประวัติ POSTPONE) — ใบในรายการนี้เลื่อนอยู่ จึงอย่างน้อย 1 ครั้ง
            // (Reservation.RescheduleCount นับการ "ลงวันใหม่" ไม่ใช่การเลื่อน — ใบที่เพิ่งเลื่อนครั้งแรกเป็น 0)
            int hist = Int(dataItem, "TotalReschedules", 0);
            return Math.Max(hist, 1).ToString("N0", EnCulture);
        }

        /// <summary>ปุ่มยกเลิก: ส่งค่าผ่าน data-* (HTML attribute encode) แทนการต่อสตริงลงใน onclick
        /// — เดิม showCancelModal(ID, 'ชื่อ') ชื่อที่มี ' หรือ &lt;script&gt; ทำให้ JS พัง/ถูกแทรกสคริปต์</summary>
        protected string CancelAttrs(object dataItem)
        {
            return "data-id=\"" + HttpUtility.HtmlEncode(Str(Val(dataItem, "ID")))
                + "\" data-name=\"" + HttpUtility.HtmlEncode(Str(Val(dataItem, "Name")))
                + "\" data-held=\"" + HttpUtility.HtmlEncode(Dec(dataItem, "Held").ToString("N2", EnCulture)) + "\"";
        }

        protected string IdText(object dataItem)
        {
            return HttpUtility.HtmlEncode(Str(Val(dataItem, "ID")));
        }

        #endregion
    }
}
