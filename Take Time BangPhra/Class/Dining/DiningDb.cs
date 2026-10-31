using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>
    /// ตัวช่วยฐานข้อมูลของโมดูล Dining — ใช้ <c>code.DatabaseQuerySafe / DatabaseInsertSafe</c> (โยน exception ขึ้นมา)
    /// และ SqlConnection ตรง ๆ เมื่อต้องการ SCOPE_IDENTITY / transaction. ตารางทั้งหมดถูก guard ด้วย <see cref="Ready"/>
    /// (OBJECT_ID — ยังไม่รัน PHASE19_42 = ทุก service ตอบ NOT_READY, ไม่พัง)
    /// </summary>
    internal static class DiningDb
    {
        private static readonly code _code = new code();

        public static string ConnStr
        {
            get
            {
                try { return ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString; }
                catch { return null; }
            }
        }

        // ── โครงสร้างพร้อมไหม (cache 60 วิ) ───────────────────────────────────
        private static readonly object _readyLock = new object();
        private static bool _ready;
        private static DateTime _readyAt = DateTime.MinValue;
        private static readonly Dictionary<string, Tuple<bool, DateTime>> _tableCache = new Dictionary<string, Tuple<bool, DateTime>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>ตารางหลักของ PHASE19_42 มีครบ (Dining_Zone/Table/Session/Order/Item/Payment)</summary>
        public static bool Ready(string conn)
        {
            lock (_readyLock)
            {
                if ((DateTime.UtcNow - _readyAt).TotalSeconds < 60) return _ready;
                bool ok = false;
                try
                {
                    var dt = Query(conn, @"SELECT CASE WHEN OBJECT_ID('dbo.Dining_Zone','U') IS NOT NULL AND OBJECT_ID('dbo.Dining_Table','U') IS NOT NULL
                              AND OBJECT_ID('dbo.Dining_Table_Session','U') IS NOT NULL AND OBJECT_ID('dbo.Dining_Order','U') IS NOT NULL
                              AND OBJECT_ID('dbo.Dining_Order_Item','U') IS NOT NULL AND OBJECT_ID('dbo.Dining_Payment','U') IS NOT NULL
                              AND OBJECT_ID('dbo.Dining_Menu_Item','U') IS NOT NULL THEN 1 ELSE 0 END AS R");
                    ok = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["R"]) == 1;
                }
                catch { ok = false; }
                _ready = ok;
                _readyAt = DateTime.UtcNow;
                return ok;
            }
        }

        public static void InvalidateReady()
        {
            lock (_readyLock) { _readyAt = DateTime.MinValue; _tableCache.Clear(); }
        }

        /// <summary>ตารางอื่นมีไหม (cache 5 นาที) — เช่น Product_Images, Sales_Channel_Posting</summary>
        public static bool TableExists(string conn, string table)
        {
            lock (_readyLock)
            {
                Tuple<bool, DateTime> t;
                if (_tableCache.TryGetValue(table, out t) && (DateTime.UtcNow - t.Item2).TotalMinutes < 5) return t.Item1;
            }
            bool ok = false;
            try
            {
                var dt = Query(conn, "SELECT CASE WHEN OBJECT_ID(@t,'U') IS NULL THEN 0 ELSE 1 END AS R",
                    new Dictionary<string, object> { { "@t", "dbo." + table } });
                ok = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["R"]) == 1;
            }
            catch { ok = false; }
            lock (_readyLock) { _tableCache[table] = Tuple.Create(ok, DateTime.UtcNow); }
            return ok;
        }

        public static DiningResult NotReady()
        {
            return DiningResult.Fail(DiningError.NotReady, "ระบบ QR สั่งอาหารยังไม่พร้อม (ยังไม่ได้รัน PHASE19_42)");
        }

        // ── query / exec ─────────────────────────────────────────────────────

        public static DataTable Query(string conn, string sql, Dictionary<string, object> p = null)
        {
            return _code.DatabaseQuerySafe(conn, sql, p);
        }

        public static int Exec(string conn, string sql, Dictionary<string, object> p = null)
        {
            return _code.DatabaseInsertSafe(conn, sql, p);
        }

        public static object Scalar(string conn, string sql, Dictionary<string, object> p = null)
        {
            using (var con = new SqlConnection(conn))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.CommandTimeout = 30;
                AddParams(cmd, p);
                con.Open();
                object o = cmd.ExecuteScalar();
                return o == DBNull.Value ? null : o;
            }
        }

        /// <summary>INSERT แล้วคืน SCOPE_IDENTITY (0 = ไม่ได้)</summary>
        public static long InsertIdentity(string conn, string sql, Dictionary<string, object> p = null)
        {
            object o = Scalar(conn, sql + "; SELECT CAST(SCOPE_IDENTITY() AS BIGINT)", p);
            return o == null ? 0 : Convert.ToInt64(o);
        }

        public static void AddParams(SqlCommand cmd, Dictionary<string, object> p)
        {
            if (p == null) return;
            foreach (var kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
        }

        public static object Db(object v) { return v ?? DBNull.Value; }
        public static object DbStr(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return DBNull.Value;
            s = s.Trim();
            return s.Length > max ? s.Substring(0, max) : s;
        }

        // ── log ──────────────────────────────────────────────────────────────

        public static void Log(string conn, string detail, string by = null)
        {
            try { _code.Logs(conn, "Dining", detail, by ?? StaffName() ?? "SYSTEM"); } catch { }
        }

        public static void Event(string conn, long? orderId, long? sessionId, int? tableId, string ev, string actor, string detail)
        {
            try
            {
                if (!TableExists(conn, "Dining_Event_Log")) return;
                Exec(conn, @"INSERT INTO dbo.Dining_Event_Log (Order_ID, Session_ID, Table_ID, Event, Actor, Detail)
                             VALUES (@o, @s, @t, @e, @a, @d)",
                    new Dictionary<string, object>
                    {
                        { "@o", Db(orderId) }, { "@s", Db(sessionId) }, { "@t", Db(tableId) },
                        { "@e", DbStr(ev, 30) }, { "@a", DbStr(actor ?? StaffName() ?? "GUEST", 100) }, { "@d", DbStr(detail, 500) }
                    });
            }
            catch { }
        }

        // ── ผู้ใช้/บริบท ─────────────────────────────────────────────────────

        public static string StaffName()
        {
            try { return HttpContext.Current?.Session?["username"]?.ToString(); } catch { return null; }
        }

        /// <summary>admin id ของพนักงานที่ล็อกอิน (0 = ไม่มี)</summary>
        public static int StaffId()
        {
            try
            {
                var s = HttpContext.Current?.Session;
                if (s == null) return 0;
                int id;
                if (int.TryParse(Convert.ToString(s["admin_id"]), out id) && id > 0) return id;
                if (int.TryParse(Convert.ToString(s["UserID"]), out id) && id > 0) return id;
            }
            catch { }
            return 0;
        }

        public static string ClientIp()
        {
            try
            {
                var r = HttpContext.Current?.Request;
                if (r == null) return null;
                string fwd = r.Headers["X-Forwarded-For"];
                if (!string.IsNullOrWhiteSpace(fwd)) return fwd.Split(',')[0].Trim();
                return r.UserHostAddress;
            }
            catch { return null; }
        }

        // ── converters ───────────────────────────────────────────────────────

        public static string Str(object v) { return v == null || v == DBNull.Value ? null : Convert.ToString(v); }
        public static int Int(object v) { int i; return v == null || v == DBNull.Value ? 0 : int.TryParse(Convert.ToString(v), out i) ? i : Convert.ToInt32(v); }
        public static int? NInt(object v) { return v == null || v == DBNull.Value ? (int?)null : Int(v); }
        public static long Long(object v) { return v == null || v == DBNull.Value ? 0L : Convert.ToInt64(v); }
        public static long? NLong(object v) { return v == null || v == DBNull.Value ? (long?)null : Convert.ToInt64(v); }
        public static decimal Dec(object v)
        {
            if (v == null || v == DBNull.Value) return 0m;
            decimal d;
            if (v is decimal) return (decimal)v;
            return decimal.TryParse(Convert.ToString(v), NumberStyles.Any, CultureInfo.InvariantCulture, out d) ? d : Convert.ToDecimal(v);
        }
        public static decimal? NDec(object v) { return v == null || v == DBNull.Value ? (decimal?)null : Dec(v); }
        public static bool Bool(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            if (v is bool) return (bool)v;
            string s = Convert.ToString(v);
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        public static DateTime Date(object v) { return v == null || v == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(v); }
        public static DateTime? NDate(object v) { return v == null || v == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(v); }
        public static TimeSpan? NTime(object v)
        {
            if (v == null || v == DBNull.Value) return null;
            if (v is TimeSpan) return (TimeSpan)v;
            TimeSpan t;
            return TimeSpan.TryParse(Convert.ToString(v), out t) ? t : (TimeSpan?)null;
        }

        /// <summary>"1,2,3" → ids</summary>
        public static List<long> ParseIds(string csv)
        {
            var list = new List<long>();
            if (string.IsNullOrWhiteSpace(csv)) return list;
            foreach (string s in csv.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                long v;
                if (long.TryParse(s.Trim(), out v) && v > 0 && !list.Contains(v)) list.Add(v);
            }
            return list;
        }

        public static string JoinIds(IList<long> ids)
        {
            if (ids == null || ids.Count == 0) return null;
            var parts = new List<string>();
            foreach (long id in ids) parts.Add(id.ToString(CultureInfo.InvariantCulture));
            return string.Join(",", parts.ToArray());
        }

        /// <summary>"@p0,@p1,…" สำหรับ IN (…) พร้อมเติม parameters</summary>
        public static string InList(IList<long> ids, Dictionary<string, object> p, string prefix = "@id")
        {
            var names = new List<string>();
            for (int i = 0; i < ids.Count; i++) { string n = prefix + i; names.Add(n); p[n] = ids[i]; }
            return string.Join(",", names.ToArray());
        }
    }
}
