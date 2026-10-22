using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;

namespace Take_Time_BangPhra.TaxReports
{
    /// <summary>
    /// ของใช้ร่วมของรายงานภาษี/กระทบยอด (Report/StockMovementReport, DailySalesTaxSummary, NextAccReconciliation)
    ///
    /// หลัก: อ่านอย่างเดียว · ทุกตาราง/คอลัมน์ที่มาจาก migration ใหม่ (PHASE19_29–31: Sales_Doc_Policy,
    /// Stock_Out_Reason, Product_Out.Out_Reason_Code ฯลฯ) ตรวจก่อนใช้เสมอ — ยังไม่รัน migration รายงานก็ทำงานได้
    /// (แค่ไม่มีคอลัมน์ส่วนนั้น). ไม่ใช้ Newtonsoft (อ่าน payload คิวด้วย JavaScriptSerializer).
    /// </summary>
    internal static class TaxReportKit
    {
        public static string Conn =>
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString;

        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ── สิทธิ์ ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Owner เสมอ · Admin ที่มีสิทธิ์รายงานบัญชี/ตั้งค่าบัญชี/รายงานผู้บริหาร · ผู้ใช้ที่อยู่ในกลุ่มสิทธิ์ซึ่งให้
        /// FIN_REPORT หรือ SYS_ACCOUNTING (เช่น กลุ่ม "ผู้ทำบัญชี"). Staff ที่ไม่ได้จัดกลุ่มเข้าไม่ได้ —
        /// สิทธิ์เริ่มต้นตาม Role ของ Staff มี FIN_REPORT ติดมาจากระบบเดิม จึงต้องกันด้วย Role อีกชั้น
        /// </summary>
        public static bool EnsureAccess(Page page)
        {
            var s = page.Session;
            if (s == null || s["permission"]?.ToString() != "True")
            {
                page.Response.Redirect("~/Admin/Login", false);
                HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return false;
            }
            string role = s["User"]?.ToString() ?? "";
            bool ok;
            if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)) ok = true;
            else if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                ok = Perm.CanAccess(Perm.FinReport) || Perm.CanAccess(Perm.SysAccounting) || Perm.CanAccess(Perm.MgtReport);
            else
                ok = !string.IsNullOrEmpty(Perm.CurrentGroupName())
                     && (Perm.CanAccess(Perm.FinReport) || Perm.CanAccess(Perm.SysAccounting));
            if (!ok)
            {
                page.Response.Redirect("~/ReserveTable?denied=" + Perm.FinReport, false);
                HttpContext.Current?.ApplicationInstance?.CompleteRequest();
            }
            return ok;
        }

        // ── ฐานข้อมูล ────────────────────────────────────────────────────────────

        public static DataTable Query(string conn, string sql, Dictionary<string, object> ps = null, int timeoutSec = 60)
        {
            var dt = new DataTable();
            using (var con = new SqlConnection(conn))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.CommandTimeout = timeoutSec;
                if (ps != null)
                    foreach (var kv in ps) cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
                using (var da = new SqlDataAdapter(cmd)) da.Fill(dt);
            }
            return dt;
        }

        private static readonly object _schemaLock = new object();
        private static readonly Dictionary<string, bool> _schema = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _schemaAt = DateTime.MinValue;

        private static bool SchemaProbe(string key, string conn, string sql, Dictionary<string, object> ps)
        {
            lock (_schemaLock)
            {
                if ((DateTime.UtcNow - _schemaAt).TotalSeconds > 120) { _schema.Clear(); _schemaAt = DateTime.UtcNow; }
                bool v;
                if (_schema.TryGetValue(key, out v)) return v;
            }
            bool r;
            try { r = Query(conn, sql, ps, 15).Rows.Count > 0; }
            catch { r = false; }
            lock (_schemaLock) _schema[key] = r;
            return r;
        }

        public static bool TableExists(string conn, string table)
        {
            return SchemaProbe("T:" + table, conn, "SELECT 1 WHERE OBJECT_ID(@t, 'U') IS NOT NULL",
                new Dictionary<string, object> { { "@t", "dbo." + table } });
        }

        public static bool ColumnExists(string conn, string table, string column)
        {
            return SchemaProbe("C:" + table + "." + column, conn, "SELECT 1 WHERE COL_LENGTH(@t, @c) IS NOT NULL",
                new Dictionary<string, object> { { "@t", "dbo." + table }, { "@c", column } });
        }

        // ── แปลงค่า / จัดรูปแบบ ─────────────────────────────────────────────────

        public static decimal Dec(object o)
        {
            if (o == null || o == DBNull.Value) return 0m;
            if (o is decimal) return (decimal)o;
            try { return Convert.ToDecimal(o, Inv); }
            catch
            {
                decimal d;
                return decimal.TryParse(o.ToString(), NumberStyles.Any, Inv, out d) ? d : 0m;
            }
        }

        public static string Str(object o) => o == null || o == DBNull.Value ? "" : o.ToString();

        public static string H(object o) => HttpUtility.HtmlEncode(Str(o));

        public static string Money(decimal d) => d.ToString("#,##0.00", Inv);

        public static string Qty(decimal d) => d.ToString("#,##0.##", Inv);

        public static string D(DateTime d) => d.ToString("dd/MM/yyyy", Inv);

        public static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", Inv);

        public static DateTime NormalizeEra(DateTime d)
        {
            if (d.Year > 2400) return d.AddYears(-543);
            if (d.Year < 1900) return d.AddYears(543);
            return d;
        }

        /// <summary>อ่านวันที่จากช่อง input type=date (yyyy-MM-dd) — ไม่ขึ้นกับ culture ของเซิร์ฟเวอร์</summary>
        public static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            DateTime d;
            if (DateTime.TryParseExact(s.Trim(), new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy" }, Inv, DateTimeStyles.None, out d))
                return NormalizeEra(d.Date);
            return null;
        }

        public static DateTime? ParsePayloadDate(object o)
        {
            string s = Str(o).Trim();
            if (s.Length == 0) return null;
            DateTime d;
            if (DateTime.TryParse(s, Inv, DateTimeStyles.None, out d)) return NormalizeEra(d).Date;
            return null;
        }

        // ── CSV ─────────────────────────────────────────────────────────────────

        public static void SendCsv(HttpResponse resp, string fileName, IList<string[]> rows)
        {
            var sb = new StringBuilder();
            foreach (var r in rows)
            {
                for (int i = 0; i < r.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    string v = r[i] ?? "";
                    if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) v = "\"" + v.Replace("\"", "\"\"") + "\"";
                    sb.Append(v);
                }
                sb.Append("\r\n");
            }
            resp.Clear();
            resp.ContentType = "text/csv";
            resp.ContentEncoding = Encoding.UTF8;
            resp.AddHeader("Content-Disposition", "attachment; filename=" + fileName);
            resp.BinaryWrite(Encoding.UTF8.GetPreamble());
            resp.Write(sb.ToString());
            resp.Flush();
            resp.SuppressContent = true;   // กันหน้า HTML ที่ render ต่อท้ายไฟล์ CSV
            HttpContext.Current?.ApplicationInstance?.CompleteRequest();
        }

        // ── VAT ─────────────────────────────────────────────────────────────────

        public static bool BusinessUsesVat(string conn)
        {
            try
            {
                var dt = Query(conn, "SELECT TOP 1 Use_Vat FROM Business_Info", null, 15);
                if (dt.Rows.Count > 0)
                {
                    string v = Str(dt.Rows[0][0]);
                    return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return false;
        }

        /// <summary>ชื่อผู้ประกอบการ + เลขผู้เสียภาษี + ที่อยู่ (หัวรายงานตาม ม.87) — คอลัมน์ไม่มี = ว่าง</summary>
        public static void BusinessHeader(string conn, out string name, out string taxId, out string address)
        {
            name = taxId = address = "";
            try
            {
                var dt = Query(conn, "SELECT TOP 1 * FROM Business_Info", null, 15);
                if (dt.Rows.Count == 0) return;
                var r = dt.Rows[0];
                if (dt.Columns.Contains("Company_Name")) name = Str(r["Company_Name"]).Trim();
                if (dt.Columns.Contains("LegalEntity_Number")) taxId = Str(r["LegalEntity_Number"]).Trim();
                if (dt.Columns.Contains("Address")) address = Str(r["Address"]).Trim();
            }
            catch { }
        }

        /// <summary>ถอด VAT 7% จากยอดรวม VAT (ปัดแบบเดียวกับ POS rollup)</summary>
        public static decimal VatFromGross(decimal gross, bool useVat)
        {
            if (!useVat || gross == 0m) return 0m;
            return gross - Math.Round(gross / 1.07m, 2, MidpointRounding.AwayFromZero);
        }

        // ── แหล่งเงิน (Account_Paid_How) ────────────────────────────────────────

        public sealed class PaidHow
        {
            public string Id, Name, ChannelType, DocModeOverride;
        }

        public sealed class PaidHowBook
        {
            public readonly Dictionary<string, PaidHow> ById = new Dictionary<string, PaidHow>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, PaidHow> ByName = new Dictionary<string, PaidHow>(StringComparer.OrdinalIgnoreCase);

            public PaidHow Get(string id)
            {
                PaidHow p;
                if (!string.IsNullOrEmpty(id) && ById.TryGetValue(id.Trim(), out p)) return p;
                return new PaidHow { Id = id ?? "", Name = string.IsNullOrEmpty(id) ? "(ไม่ระบุแหล่งเงิน)" : "แหล่งเงิน #" + id, ChannelType = "OTHER" };
            }

            public PaidHow GetByName(string name)
            {
                string n = (name ?? "").Trim();
                PaidHow p;
                if (n.Length > 0 && ByName.TryGetValue(n, out p)) return p;
                string prov;
                return new PaidHow { Id = "", Name = n.Length == 0 ? "(ไม่ระบุแหล่งเงิน)" : n,
                    ChannelType = Take_Time_BangPhra.Payments.PaymentChannelCatalog.InferType(n, out prov) };
            }
        }

        public static PaidHowBook LoadPaidHow(string conn)
        {
            var book = new PaidHowBook();
            try
            {
                bool hasType = ColumnExists(conn, "Account_Paid_How", "Channel_Type");
                bool hasMode = ColumnExists(conn, "Account_Paid_How", "Sales_Doc_Mode");
                var dt = Query(conn,
                    "SELECT ID, Paid_How" + (hasType ? ", Channel_Type" : "") + (hasMode ? ", Sales_Doc_Mode" : "") +
                    " FROM Account_Paid_How", null, 20);
                foreach (DataRow r in dt.Rows)
                {
                    string name = Str(r["Paid_How"]).Trim();
                    string type = hasType ? Str(r["Channel_Type"]).Trim().ToUpperInvariant() : "";
                    if (type.Length == 0)
                    {
                        string prov;
                        type = Take_Time_BangPhra.Payments.PaymentChannelCatalog.InferType(name, out prov);
                    }
                    var p = new PaidHow
                    {
                        Id = Str(r["ID"]).Trim(), Name = name, ChannelType = type,
                        DocModeOverride = hasMode ? Str(r["Sales_Doc_Mode"]).Trim().ToUpperInvariant() : ""
                    };
                    book.ById[p.Id] = p;
                    if (name.Length > 0 && !book.ByName.ContainsKey(name)) book.ByName[name] = p;
                }
            }
            catch { }
            return book;
        }

        public static string ChannelTypeLabel(string t)
        {
            switch ((t ?? "").ToUpperInvariant())
            {
                case "CASH": return "เงินสด";
                case "TRANSFER": return "โอน/พร้อมเพย์";
                case "QR": return "QR";
                case "CARD": return "บัตร";
                case "GATEWAY_OTHER": return "เกตเวย์ออนไลน์";
                case "DIRECTOR_LOAN": return "ทดรองกรรมการ";
                case "OTA": return "OTA";
                default: return "อื่น ๆ";
            }
        }

        public const string ChPos = "POS", ChRoomService = "ROOM_SERVICE", ChReservation = "RESERVATION",
            ChOta = "OTA", ChOther = "OTHER";

        public static string SalesChannelLabel(string ch)
        {
            switch (ch)
            {
                case ChPos: return "ขายหน้าร้าน (POS)";
                case ChRoomService: return "รูมเซอร์วิส";
                case ChReservation: return "ห้องพัก/การจอง";
                case ChOta: return "OTA (Channel Collect)";
                // ช่องทางของสมุด Sales_Channel_Posting (PHASE19_32)
                case "ACTIVITY": return "กิจกรรม";
                case "AMENITY": return "ของใช้/อาเมนิตี้";
                case "FRONTDESK_GATEWAY": return "เงินเกตเวย์หน้าร้าน (ไม่มีบันทึกขาย)";
                case "DAMAGE": return "ค่าเสียหาย";
                default: return "อื่น ๆ (สมาชิก/บัตรกำนัล ฯลฯ)";
            }
        }

        // ── นโยบายเอกสารขาย (Sales_Doc_Policy — PHASE19_29, อ่านแบบ guarded) ─────

        public sealed class PolicyBook
        {
            public bool TableReady, Enabled;
            private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public void Add(string ch, string type, string mode) { _map[(ch ?? "*") + "|" + (type ?? "*")] = mode; }

            /// <summary>ลำดับเดียวกับสัญญา SalesDocumentPolicy.Resolve: override แหล่งเงิน → (ช่องทาง,ชนิด) →
            /// (ช่องทาง,*) → (*,ชนิด) → (*,*) → IMMEDIATE · ปิดนโยบาย/ยังไม่รัน migration = "" (พฤติกรรมเดิม)</summary>
            public string Resolve(string channel, PaidHow ph)
            {
                if (!TableReady || !Enabled) return "";
                if (ph != null && !string.IsNullOrEmpty(ph.DocModeOverride)) return ph.DocModeOverride;
                string t = ph?.ChannelType ?? "OTHER";
                string m;
                if (_map.TryGetValue(channel + "|" + t, out m)) return m;
                if (_map.TryGetValue(channel + "|*", out m)) return m;
                if (_map.TryGetValue("*|" + t, out m)) return m;
                if (_map.TryGetValue("*|*", out m)) return m;
                return "IMMEDIATE";
            }
        }

        public static PolicyBook LoadPolicy(string conn)
        {
            var pb = new PolicyBook();
            try
            {
                pb.TableReady = TableExists(conn, "Sales_Doc_Policy");
                if (!pb.TableReady) return pb;
                var cfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                pb.Enabled = cfg.GetConfigValue("Sales_Doc_Policy_Enabled", "0") == "1";
                var dt = Query(conn, "SELECT Sales_Channel, Channel_Type, Doc_Mode FROM Sales_Doc_Policy WHERE ISNULL(Is_Enabled,1) = 1", null, 15);
                foreach (DataRow r in dt.Rows)
                    pb.Add(Str(r[0]).Trim().ToUpperInvariant(), Str(r[1]).Trim().ToUpperInvariant(), Str(r[2]).Trim().ToUpperInvariant());
            }
            catch { }
            return pb;
        }

        public static string ModeLabel(string mode)
        {
            switch ((mode ?? "").ToUpperInvariant())
            {
                case "IMMEDIATE": return "ออกทันทีรายใบ";
                case "DAILY_ROLLUP": return "รวบรายวัน";
                case "OFF": return "ไม่ส่ง NextAcc";
                default: return "ตามระบบเดิม";
            }
        }

        // ── คิว NextAcc (Accounting_Sync_Queue) ──────────────────────────────────

        public sealed class QueueItem
        {
            public long Id;
            public string Entity, Action, Status, Error, DocNumber, Key;
            public int Retry;
            public DateTime Created;
            public DateTime? Processed, DocDate;
            public Dictionary<string, object> P = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            public decimal Num(string k) { object o; return P.TryGetValue(k, out o) ? Dec(o) : 0m; }
            public string Text(string k) { object o; return P.TryGetValue(k, out o) ? Str(o) : ""; }
            /// <summary>วันที่ของเอกสาร (จาก payload) — ไม่มีให้ใช้วันที่เข้าคิว</summary>
            public DateTime Day => (DocDate ?? Created).Date;
        }

        public static readonly string[] ReceiptCreateActions = { "CREATE_RECEIPT_DOCUMENT", "CREATE_OTA_SALES_DOCUMENT" };

        private static readonly string[] DateKeys =
            { "receiptDate", "outDate", "reverseDate", "writeOffDate", "adjustDate", "receiveDate", "documentDate", "entryDate", "date" };

        public static bool QueueReady(string conn) => TableExists(conn, "Accounting_Sync_Queue");

        /// <summary>
        /// โหลดรายการคิวตามชนิด (actions = null → ทุกชนิด) ที่เข้าคิวตั้งแต่ createdFrom (ไม่จำกัดปลาย —
        /// เอกสารรวบรายวัน/แก้ไขย้อนหลังเข้าคิวหลังวันเอกสารได้). actionPrefixes = ชนิดที่ขึ้นต้นด้วย (เช่น STOCK_)
        /// </summary>
        public static List<QueueItem> LoadQueue(string conn, IList<string> actions, IList<string> actionPrefixes, DateTime createdFrom)
        {
            var list = new List<QueueItem>();
            if (!QueueReady(conn)) return list;
            bool hasDocNo = ColumnExists(conn, "Accounting_Sync_Queue", "Nexaacc_Document_Number");
            var ps = new Dictionary<string, object> { { "@cf", createdFrom } };
            var where = new List<string>();
            if (actions != null)
                for (int i = 0; i < actions.Count; i++) { where.Add("Action_Type = @a" + i); ps["@a" + i] = actions[i]; }
            if (actionPrefixes != null)
                for (int i = 0; i < actionPrefixes.Count; i++) { where.Add("Action_Type LIKE @p" + i); ps["@p" + i] = actionPrefixes[i] + "%"; }
            string sql = "SELECT ID, Entity_Type, Action_Type, Status, Payload, ISNULL(Retry_Count,0) AS Retry_Count, Error_Message, " +
                         "Created_Date, Processed_Date" + (hasDocNo ? ", Nexaacc_Document_Number" : "") +
                         " FROM Accounting_Sync_Queue WITH (NOLOCK) WHERE Created_Date >= @cf" +
                         (where.Count > 0 ? " AND (" + string.Join(" OR ", where) + ")" : "") +
                         " ORDER BY ID";
            var dt = Query(conn, sql, ps, 90);
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            foreach (DataRow r in dt.Rows)
            {
                var q = new QueueItem
                {
                    Id = Convert.ToInt64(r["ID"]),
                    Entity = Str(r["Entity_Type"]),
                    Action = Str(r["Action_Type"]),
                    Status = Str(r["Status"]).ToUpperInvariant(),
                    Retry = Convert.ToInt32(r["Retry_Count"]),
                    Error = Str(r["Error_Message"]),
                    Created = r["Created_Date"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["Created_Date"]),
                    Processed = r["Processed_Date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["Processed_Date"]),
                    DocNumber = hasDocNo ? Str(r["Nexaacc_Document_Number"]) : ""
                };
                try
                {
                    var obj = js.DeserializeObject(Str(r["Payload"])) as Dictionary<string, object>;
                    if (obj != null) foreach (var kv in obj) q.P[kv.Key] = kv.Value;
                }
                catch { }
                q.Key = q.Text("receiptNumber");
                if (q.Key.Length == 0) q.Key = q.Text("stockRef");
                foreach (string k in DateKeys)
                {
                    object o;
                    if (q.P.TryGetValue(k, out o)) { q.DocDate = ParsePayloadDate(o); if (q.DocDate != null) break; }
                }
                list.Add(q);
            }
            return list;
        }

        /// <summary>สถานะล่าสุดของเอกสารรับเงินหนึ่งเลข (ใบเสร็จ/ใบสรุปรายวัน/OTADOC) บนคิว</summary>
        public sealed class ReceiptState
        {
            public QueueItem Create;     // CREATE ล่าสุด
            public QueueItem VoidOk;     // VOID ที่สำเร็จล่าสุด
            public bool Voided => Create != null && VoidOk != null && VoidOk.Id > Create.Id;
            public string Status => Create == null ? "NONE" : (Voided ? "VOIDED" : Create.Status);
        }

        public static Dictionary<string, ReceiptState> IndexReceipts(IEnumerable<QueueItem> items)
        {
            var map = new Dictionary<string, ReceiptState>(StringComparer.OrdinalIgnoreCase);
            foreach (var q in items)
            {
                if (string.IsNullOrEmpty(q.Key)) continue;
                bool isCreate = Array.IndexOf(ReceiptCreateActions, q.Action) >= 0;
                bool isVoid = q.Action == "VOID_RECEIPT" && q.Status == "COMPLETED";
                if (!isCreate && !isVoid) continue;
                ReceiptState st;
                if (!map.TryGetValue(q.Key, out st)) { st = new ReceiptState(); map[q.Key] = st; }
                if (isCreate && (st.Create == null || q.Id > st.Create.Id)) st.Create = q;
                if (isVoid && (st.VoidOk == null || q.Id > st.VoidOk.Id)) st.VoidOk = q;
            }
            return map;
        }

        public static string StatusLabel(string status)
        {
            switch ((status ?? "").ToUpperInvariant())
            {
                case "COMPLETED": return "ส่งแล้ว";
                case "PENDING": return "รอส่ง";
                case "PROCESSING": return "กำลังส่ง";
                case "FAILED": return "ล้มเหลว";
                case "VOIDED": return "ยกเลิกแล้ว";
                case "NONE": return "ไม่มีในคิว";
                default: return status.StartsWith("SKIPPED", StringComparison.OrdinalIgnoreCase) ? "ข้าม" : status;
            }
        }

        public static string StatusCss(string status)
        {
            switch ((status ?? "").ToUpperInvariant())
            {
                case "COMPLETED": return "ok";
                case "PENDING":
                case "PROCESSING": return "warn";
                case "FAILED":
                case "NONE": return "err";
                default: return "off";
            }
        }

        public static string Chip(string text, string css) =>
            "<span class='tr-chip tr-" + css + "'>" + HttpUtility.HtmlEncode(text) + "</span>";

        public const string QueuePageUrl = "~/Admin/Settings/AccountingIntegration#queueStats";

        public static bool IntegrationOn(string conn, out Take_Time_BangPhra.Integration.AccountingConfig cfg)
        {
            cfg = null;
            try
            {
                cfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                return cfg.IsConfigured && cfg.Enabled;
            }
            catch { return false; }
        }

        // ── ยอดขายรายวัน × ช่องทาง × แหล่งเงิน (ใช้ร่วม DailySalesTaxSummary + NextAccReconciliation) ──

        public const string FNoDoc = "NO_DOC", FFailed = "FAILED", FPending = "PENDING", FLate = "LATE",
            FExcluded = "EXCLUDED", FUnverified = "UNVERIFIED", FVoided = "VOIDED", FToday = "TODAY",
            FPolicy = "POLICY", FDeposit = "DEPOSIT", FOrderPending = "ORDER_PENDING", FLegacy = "LEGACY",
            FInfoOnly = "INFO_ONLY",
            // FPolicyOff: นโยบายเอกสารขาย = ไม่ส่งบัญชี (OFF-POLICY / RSOFF / สมุดช่องทาง Status OFF) — ตั้งใจไม่ส่ง ไม่ใช่ตกหล่น
            FPolicyOff = "POLICY_OFF";

        public static bool IsProblemFlag(string f) =>
            f == FNoDoc || f == FFailed || f == FPending || f == FLate || f == FUnverified || f == FPolicy || f == FOrderPending;

        public sealed class DocRef
        {
            public string Id, Status, DocNo;
            public bool Rollup;
            public long QueueId;
        }

        public sealed class SaleGroup
        {
            public DateTime Day;
            public string Channel, ChannelType, PaidHowName, PolicyMode = "";
            public int Count;
            public decimal Gross, Vat;
            /// <summary>ส่วนของ Gross/Vat ที่นโยบายตั้ง "ไม่ส่งบัญชี" — กระทบยอดกับ NextAcc ไม่นับส่วนนี้ (ตั้งใจไม่ส่ง)</summary>
            public decimal PolicyOffGross, PolicyOffVat;
            /// <summary>true = แสดงเพื่อทราบเท่านั้น ไม่นับเป็นยอดขาย (ชาร์จเข้าห้อง / สลิปรอตรวจ)</summary>
            public bool InfoOnly;
            public string Note = "";
            public readonly List<DocRef> Docs = new List<DocRef>();
            public readonly HashSet<string> Flags = new HashSet<string>();
            public readonly List<string> ProblemIds = new List<string>();
            public bool HasProblem { get { foreach (var f in Flags) if (IsProblemFlag(f)) return true; return false; } }
        }

        public sealed class SalesResult
        {
            public readonly List<SaleGroup> Groups = new List<SaleGroup>();
            public Dictionary<string, ReceiptState> Receipts = new Dictionary<string, ReceiptState>(StringComparer.OrdinalIgnoreCase);
            public List<QueueItem> Queue = new List<QueueItem>();
            public bool UseVat, IntegrationOn, QueueReady, PosRollupOn, RoomServiceOn, RoomServiceTable;
            public PolicyBook Policy;
            public readonly List<string> Notes = new List<string>();
        }

        private static readonly Regex RollupIdRx = new Regex(@"^([A-Z]+)DAY-(\d{8})-", RegexOptions.Compiled);
        /// <summary>รูปย่อเมื่อเลขเต็มยาวเกิน 20 ตัว (ChannelPosting.NextFreeRollupRef): {P}DAY{yyMMdd}-{key}[-S{n}]</summary>
        private static readonly Regex RollupCompactIdRx = new Regex(@"^[A-Z]+DAY\d{6}-", RegexOptions.Compiled);

        /// <summary>
        /// เลขเอกสารสรุปรายวัน (ไม่ใช่ยอดขายรายใบ): {P}DAY-{yyyyMMdd}-… / {P}DAY{yyMMdd}-… (RSDAY/ACTDAY/…) และ
        /// ใบสรุปขายหน้าร้านทุกรูป รวมใบเพิ่มเติมแบบย่อ PS{yyMMdd}-{แหล่งเงิน}[-S{n}] (AccountingSyncService.IsPosRollupRef)
        /// — ไม่รู้จักรูปย่อ = ใบสรุปถูกนับเป็นยอดขายซ้ำกับแถวขาย
        /// </summary>
        public static bool IsRollupId(string id) =>
            !string.IsNullOrEmpty(id) && (RollupIdRx.IsMatch(id) || RollupCompactIdRx.IsMatch(id)
                                          || Take_Time_BangPhra.Integration.AccountingSyncService.IsPosRollupRef(id));

        private static string SanitizeKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return "NA";
            var sb = new StringBuilder();
            foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            return sb.Length == 0 ? "NA" : sb.ToString();
        }

        /// <summary>
        /// รวบรวมการขายช่วง [from, toInclusive] จาก: ใบเสร็จ (Account_Receipt ที่ไม่ใช่ใบสรุปรายวัน),
        /// แถวขาย POS ที่ไม่ออกใบ (Product_Out), รูมเซอร์วิส, สลิปลูกค้ารอตรวจ — แล้วผูกเอกสาร NextAcc จากคิว
        /// </summary>
        public static SalesResult CollectSales(string conn, DateTime from, DateTime toInclusive)
        {
            var res = new SalesResult();
            DateTime toEx = toInclusive.Date.AddDays(1);
            DateTime today = DateTime.Today;
            res.UseVat = BusinessUsesVat(conn);
            Take_Time_BangPhra.Integration.AccountingConfig cfg;
            res.IntegrationOn = IntegrationOn(conn, out cfg);
            try { res.PosRollupOn = cfg != null && cfg.IsPosDailyRollupEnabled; } catch { }
            try { res.RoomServiceOn = cfg != null && cfg.IsRoomServiceRevenueEnabled; } catch { }
            res.QueueReady = QueueReady(conn);
            res.Policy = LoadPolicy(conn);
            var book = LoadPaidHow(conn);

            if (res.QueueReady)
            {
                var acts = new List<string>(ReceiptCreateActions) { "VOID_RECEIPT" };
                res.Queue = LoadQueue(conn, acts, null, from.AddDays(-3));
                res.Receipts = IndexReceipts(res.Queue);
            }

            var groups = new Dictionary<string, SaleGroup>();
            Func<DateTime, string, PaidHow, SaleGroup> G = (day, ch, ph) =>
            {
                string k = Iso(day) + "|" + ch + "|" + ph.Name;
                SaleGroup g;
                if (!groups.TryGetValue(k, out g))
                {
                    g = new SaleGroup { Day = day.Date, Channel = ch, ChannelType = ph.ChannelType, PaidHowName = ph.Name,
                                        PolicyMode = res.Policy.Resolve(ch, ph) };
                    groups[k] = g;
                }
                return g;
            };

            // ── A. ใบเสร็จ ───────────────────────────────────────────────────────
            var rollupTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var posReceiptIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var dtp = Query(conn,
                    @"SELECT DISTINCT Account_Receipt_ID FROM Product_Out
                       WHERE Remark = N'ขาย' AND Account_Receipt_ID IS NOT NULL AND Account_Receipt_ID NOT IN ('0','')
                         AND DateTime_Out >= @f AND DateTime_Out < @t",
                    new Dictionary<string, object> { { "@f", from.AddDays(-1) }, { "@t", toEx.AddDays(1) } });
                foreach (DataRow r in dtp.Rows) posReceiptIds.Add(Str(r[0]).Trim());
            }
            catch (Exception ex) { res.Notes.Add("อ่านแถวขาย POS ไม่ได้: " + ex.Message); }

            // ใบรายการเดียวที่ช่องทางอื่นนับเองแล้ว — ข้ามในส่วนใบเสร็จ กันนับสองรอบ:
            //   RS-{orderId} (รูมเซอร์วิสรายออเดอร์) นับในส่วน C จากออเดอร์ · ใบรายการเดียวของสมุดช่องทาง
            //   (Sales_Channel_Posting.Post_Ref เช่น ACT-/AMN-/FDG-/DMG-) นับในส่วน E ตามวันขาย
            bool rsReady = TableExists(conn, "Guest_Room_Service_Orders") && ColumnExists(conn, "Guest_Room_Service_Orders", "Acct_Post_Ref");
            bool ledgerReady = TableExists(conn, "Sales_Channel_Posting");
            try
            {
                var dtr = Query(conn,
                    @"SELECT ID, Reservation_ID, Created_Date, Total_Amount, Vat, Paid_Type, IsDeposit, " +
                    (rsReady ? "CASE WHEN EXISTS (SELECT 1 FROM Guest_Room_Service_Orders so WHERE so.Acct_Post_Ref = Account_Receipt.ID) THEN 1 ELSE 0 END"
                             : "0") + " AS InRs, " +
                    (ledgerReady ? "CASE WHEN EXISTS (SELECT 1 FROM Sales_Channel_Posting sp WHERE sp.Post_Ref = Account_Receipt.ID) THEN 1 ELSE 0 END"
                                 : "0") + @" AS InLedger
                        FROM Account_Receipt
                       WHERE Created_Date >= @f AND Created_Date < @t
                         AND ISNULL(Status, '') NOT IN (N'Cancel', N'Void', N'ยกเลิก')",
                    new Dictionary<string, object> { { "@f", from }, { "@t", toEx } });
                foreach (DataRow r in dtr.Rows)
                {
                    string id = Str(r["ID"]).Trim();
                    decimal total = Dec(r["Total_Amount"]);
                    if (IsRollupId(id)) { rollupTotals[id] = total; continue; }   // ใบสรุป = เอกสารครอบ ไม่ใช่ยอดขายซ้ำ
                    if (Str(r["InRs"]) == "1" || Str(r["InLedger"]) == "1") continue;   // นับในส่วน C / E แล้ว
                    DateTime day = NormalizeEra(Convert.ToDateTime(r["Created_Date"])).Date;
                    string resId = Str(r["Reservation_ID"]).Trim();
                    string ch = id.StartsWith("OTA", StringComparison.OrdinalIgnoreCase) ? ChOta
                              : posReceiptIds.Contains(id) ? ChPos
                              : (resId.Length > 0 && resId != "0") ? ChReservation : ChOther;
                    var ph = book.GetByName(Str(r["Paid_Type"]));
                    if (ch == ChOta) ph = new PaidHow { Name = "OTA (ช่องทางเก็บเงิน)", ChannelType = "OTA" };
                    var g = G(day, ch, ph);
                    g.Count++;
                    g.Gross += total;
                    g.Vat += Dec(r["Vat"]);
                    string dep = Str(r["IsDeposit"]);
                    if (dep == "1" || dep.Equals("true", StringComparison.OrdinalIgnoreCase)) g.Flags.Add(FDeposit);
                    AttachReceiptDoc(res, g, id, false);
                }
            }
            catch (Exception ex) { res.Notes.Add("อ่านใบเสร็จไม่ได้: " + ex.Message); }

            // ใบสรุปรายวันที่ลงวันที่นอกช่วง (แต่แถวขายอยู่ในช่วง) — โหลดเพิ่มตามต้องการ
            Func<string, decimal?> RollupTotal = rid =>
            {
                decimal v;
                if (rollupTotals.TryGetValue(rid, out v)) return v;
                try
                {
                    var d = Query(conn, "SELECT Total_Amount FROM Account_Receipt WHERE ID = @id", new Dictionary<string, object> { { "@id", rid } }, 15);
                    if (d.Rows.Count > 0) { v = Dec(d.Rows[0][0]); rollupTotals[rid] = v; return v; }
                }
                catch { }
                return null;
            };

            // ── B. ขาย POS ที่ไม่ออกใบ (Product_Out) ────────────────────────────────
            try
            {
                bool hasRef = ColumnExists(conn, "Product_Out", "Pos_Rollup_Ref");
                var dto = Query(conn,
                    @"SELECT CAST(o.DateTime_Out AS DATE) AS D, ISNULL(CAST(o.Account_Paid_How_ID AS NVARCHAR(50)), '') AS PH, " +
                    (hasRef ? "o.Pos_Rollup_Ref" : "CAST(NULL AS NVARCHAR(100))") + @" AS REF,
                             COUNT(DISTINCT o.DateTime_Out) AS Bills, SUM(o.Amount * o.PricePerUnit) AS Gross
                        FROM Product_Out o
                       WHERE o.Remark = N'ขาย'
                         AND (o.Account_Receipt_ID IS NULL OR o.Account_Receipt_ID IN ('0',''))
                         AND o.DateTime_Out >= @f AND o.DateTime_Out < @t
                       GROUP BY CAST(o.DateTime_Out AS DATE), ISNULL(CAST(o.Account_Paid_How_ID AS NVARCHAR(50)), ''), " +
                    (hasRef ? "o.Pos_Rollup_Ref" : "CAST(NULL AS NVARCHAR(100))"),
                    new Dictionary<string, object> { { "@f", from }, { "@t", toEx } });
                var rowsByRef = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                var groupByRef = new Dictionary<string, SaleGroup>(StringComparer.OrdinalIgnoreCase);
                foreach (DataRow r in dto.Rows)
                {
                    string rf = Str(r["REF"]).Trim();
                    // แถวขายที่ถูกยกเลิกจริง (คืนสต๊อก+กลับต้นทุนแล้ว) — ไม่ใช่ยอดขาย
                    if (rf.StartsWith(Take_Time_BangPhra.Integration.AccountingSyncService.PosVoidRevPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;
                    DateTime day = NormalizeEra(Convert.ToDateTime(r["D"])).Date;
                    string phId = Str(r["PH"]).Trim();
                    var ph = book.Get(phId);
                    var g = G(day, ChPos, ph);
                    decimal gross = Dec(r["Gross"]);
                    decimal vat = VatFromGross(gross, res.UseVat);
                    g.Count += Convert.ToInt32(r["Bills"]);
                    g.Gross += gross;
                    g.Vat += vat;
                    // เลขใบแรกตามที่ระบบรวบจริง (POSDAY-… หรือ PS… เมื่อ ID แหล่งเงินยาว)
                    string expectedRef = Take_Time_BangPhra.Integration.AccountingSyncService.PosRollupBaseRef(day, phId);
                    if (rf.Length == 0)
                    {
                        if (day >= today) g.Flags.Add(FToday);
                        else
                        {
                            g.Flags.Add(FNoDoc);
                            if (expectedRef != null && RollupTotal(expectedRef) != null) { g.Flags.Add(FLate); g.ProblemIds.Add(expectedRef + " (มาช้า)"); }
                        }
                    }
                    else if (rf == Take_Time_BangPhra.Integration.AccountingSyncService.PosOffPolicyRef)
                    {
                        // นโยบาย = ไม่ส่งบัญชี → ตั้งใจไม่ส่ง ไม่ใช่ "ไม่มีเอกสาร"
                        g.Flags.Add(FPolicyOff);
                        g.PolicyOffGross += gross; g.PolicyOffVat += vat;
                    }
                    else if (rf.StartsWith("PENDING-", StringComparison.OrdinalIgnoreCase))
                        g.Flags.Add(FPending);                     // กำลังรวบเข้าใบสรุป (claim ค้าง/รอบนี้)
                    else if (rf.StartsWith(Take_Time_BangPhra.Integration.AccountingSyncService.PosRerollPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        g.Flags.Add(FPending);                     // ใบสรุปถูกยกเลิก รอ void บน NextAcc แล้วรวบใหม่
                        g.ProblemIds.Add(rf.Substring(Take_Time_BangPhra.Integration.AccountingSyncService.PosRerollPrefix.Length) + " (ยกเลิกแล้ว รอรวบใหม่)");
                    }
                    else if (rf.StartsWith("EXCLUDED-", StringComparison.OrdinalIgnoreCase)) g.Flags.Add(FExcluded);
                    else if (rf == "LEGACY" || rf == "NEXTACC") g.Flags.Add(FLegacy);
                    else
                    {
                        AttachReceiptDoc(res, g, rf, true);
                        decimal acc;
                        rowsByRef.TryGetValue(rf, out acc);
                        rowsByRef[rf] = acc + gross;
                        groupByRef[rf] = g;
                    }
                }
                foreach (var kv in rowsByRef)
                {
                    decimal? rt = RollupTotal(kv.Key);
                    if (rt == null || kv.Value > rt.Value + 0.01m)
                    {
                        var g = groupByRef[kv.Key];
                        g.Flags.Add(FLate);
                        g.ProblemIds.Add(kv.Key + (rt == null ? " (ไม่พบใบสรุป)" : " (แถวขาย " + Money(kv.Value) + " > ใบสรุป " + Money(rt.Value) + ")"));
                    }
                }
            }
            catch (Exception ex) { res.Notes.Add("อ่านแถวขาย POS ไม่ได้: " + ex.Message); }

            // ── C. รูมเซอร์วิส ─────────────────────────────────────────────────────
            res.RoomServiceTable = TableExists(conn, "Guest_Room_Service_Orders");
            if (res.RoomServiceTable)
            {
                try
                {
                    bool hasRef = ColumnExists(conn, "Guest_Room_Service_Orders", "Acct_Post_Ref");
                    bool hasPs = ColumnExists(conn, "Guest_Room_Service_Orders", "Payment_Status");
                    string refX = hasRef ? "o.Acct_Post_Ref" : "CAST(NULL AS NVARCHAR(60))";
                    string psX = hasPs ? "ISNULL(o.Payment_Status, '')" : "CAST('' AS NVARCHAR(20))";
                    var dts = Query(conn,
                        "SELECT CAST(o.Order_Date AS DATE) AS D, ISNULL(o.Payment_Method, '') AS PM, " + refX + " AS REF, " +
                        psX + " AS PS, ISNULL(o.Order_Status, '') AS OS, COUNT(*) AS N, SUM(o.Total_Amount) AS G " +
                        "FROM Guest_Room_Service_Orders o WHERE o.Order_Date >= @f AND o.Order_Date < @t " +
                        "AND ISNULL(o.Order_Status, '') <> 'CANCELLED' " +
                        "GROUP BY CAST(o.Order_Date AS DATE), ISNULL(o.Payment_Method, ''), " + refX + ", " + psX + ", ISNULL(o.Order_Status, '')",
                        new Dictionary<string, object> { { "@f", from }, { "@t", toEx } });
                    foreach (DataRow r in dts.Rows)
                    {
                        DateTime day = NormalizeEra(Convert.ToDateTime(r["D"])).Date;
                        string pm = Str(r["PM"]).Trim().ToUpperInvariant();
                        string os = Str(r["OS"]).Trim().ToUpperInvariant();
                        string ps = Str(r["PS"]).Trim().ToUpperInvariant();
                        var ph = RoomServicePaidHow(pm);
                        var g = G(day, ChRoomService, ph);
                        decimal gross = Dec(r["G"]);
                        g.Count += Convert.ToInt32(r["N"]);
                        g.Gross += gross;
                        g.Vat += VatFromGross(gross, res.UseVat);
                        if (pm == "CHARGE_TO_ROOM")
                        {
                            g.InfoOnly = true;
                            g.Note = "ลงบิลห้อง — รายได้อยู่ในใบเสร็จเช็คเอาท์ (ไม่นับซ้ำ)";
                            continue;
                        }
                        if (os == "PENDING") { g.Flags.Add(FOrderPending); continue; }
                        if (pm == "TRANSFER" && ps != "PAID" && ps != "CHARGED") g.Flags.Add(FUnverified);
                        string rf = Str(r["REF"]).Trim();
                        if (rf.Length == 0)
                        {
                            if (day >= today) g.Flags.Add(FToday);
                            else
                            {
                                g.Flags.Add(FNoDoc);
                                string expectedRef = "RSDAY-" + day.ToString("yyyyMMdd", Inv) + "-" + SanitizeKey(pm);
                                if (RollupTotal(expectedRef) != null) { g.Flags.Add(FLate); g.ProblemIds.Add(expectedRef + " (มาช้า)"); }
                            }
                        }
                        else if (rf.StartsWith("EXCLUDED", StringComparison.OrdinalIgnoreCase) || rf == "LEGACY") g.Flags.Add(FLegacy);
                        else if (rf.StartsWith("~", StringComparison.Ordinal)) g.Flags.Add(FPending);   // จองเลขแล้ว กำลังออกเอกสาร
                        else if (rf.StartsWith("RSOFF", StringComparison.Ordinal))
                        {
                            // นโยบาย = ไม่ออกเอกสาร (ต้นทุน/สต๊อกยังลง) → ตั้งใจไม่ส่ง
                            g.Flags.Add(FPolicyOff);
                            g.PolicyOffGross += gross; g.PolicyOffVat += VatFromGross(gross, res.UseVat);
                        }
                        else if (rf.StartsWith("RSCTR", StringComparison.Ordinal))
                        {
                            // ลงบิลห้อง (Payment_Status CHARGED) — รายได้อยู่ในใบเสร็จเช็คเอาท์: ย้ายไปแถวแสดงเพื่อทราบ ไม่นับซ้ำ
                            g.Count -= Convert.ToInt32(r["N"]); g.Gross -= gross; g.Vat -= VatFromGross(gross, res.UseVat);
                            var gc = G(day, ChRoomService, RoomServicePaidHow("CHARGE_TO_ROOM"));
                            gc.Count += Convert.ToInt32(r["N"]); gc.Gross += gross; gc.Vat += VatFromGross(gross, res.UseVat);
                            gc.InfoOnly = true;
                            gc.Note = "ลงบิลห้อง — รายได้อยู่ในใบเสร็จเช็คเอาท์ (ไม่นับซ้ำ)";
                        }
                        // RS-{orderId} = ใบรายออเดอร์ (ส่วน A ข้ามใบนี้แล้ว) · RSDAY… = ใบสรุปรายวัน
                        else AttachReceiptDoc(res, g, rf, !rf.StartsWith("RS-", StringComparison.Ordinal));
                    }
                }
                catch (Exception ex) { res.Notes.Add("อ่านรูมเซอร์วิสไม่ได้: " + ex.Message); }
            }

            // ── E. สมุดการขายรายช่องทาง (Sales_Channel_Posting — PHASE19_32: กิจกรรม/ของใช้/เกตเวย์หน้าร้าน/ค่าเสียหาย) ──
            //    1 แถว = 1 การขายที่รับเงินแล้ว นับตามวันขาย. ใบรายการเดียว (Post_Ref ไม่ใช่ใบสรุป) ส่วน A ข้ามไว้แล้ว;
            //    ใบสรุป {P}DAY… ส่วน A ไม่นับ (เป็นเอกสารครอบ) ⇒ ยอดของช่องทางเหล่านี้มาจากสมุดนี้ที่เดียว.
            //    MATCHED (เงินเกตเวย์จับคู่การขาย POS แล้ว) = นับใน POS แล้ว → ข้าม · NOPOLICY = นโยบายยังไม่เปิด (ไม่ส่ง) → แสดงเพื่อทราบ
            if (ledgerReady)
            {
                try
                {
                    var dtl = Query(conn,
                        @"SELECT Sale_Date AS D, Sales_Channel AS CH, ISNULL(Paid_How_ID, 0) AS PH, ISNULL(Paid_How_Name, '') AS PN,
                                 ISNULL([Status], '') AS ST, ISNULL(Doc_Mode, '') AS DM, ISNULL(Post_Ref, '') AS REF,
                                 COUNT(*) AS N, SUM(Amount) AS G
                            FROM Sales_Channel_Posting
                           WHERE Sale_Date >= @f AND Sale_Date < @t AND ISNULL([Status], '') <> 'MATCHED'
                           GROUP BY Sale_Date, Sales_Channel, ISNULL(Paid_How_ID, 0), ISNULL(Paid_How_Name, ''),
                                    ISNULL([Status], ''), ISNULL(Doc_Mode, ''), ISNULL(Post_Ref, '')",
                        new Dictionary<string, object> { { "@f", from.Date }, { "@t", toEx } });
                    foreach (DataRow r in dtl.Rows)
                    {
                        DateTime day = NormalizeEra(Convert.ToDateTime(r["D"])).Date;
                        string ch = Str(r["CH"]).Trim().ToUpperInvariant();
                        if (ch.Length == 0) ch = ChOther;
                        string st = Str(r["ST"]).Trim().ToUpperInvariant();
                        string rf = Str(r["REF"]).Trim();
                        string phId = Str(r["PH"]).Trim();
                        var ph = phId != "0" ? book.Get(phId) : book.GetByName(Str(r["PN"]));
                        decimal gross = Dec(r["G"]);
                        decimal vat = VatFromGross(gross, res.UseVat);
                        int n = Convert.ToInt32(r["N"]);

                        if (st == "NOPOLICY")
                        {
                            var gi = G(day, ch, new PaidHow { Name = ph.Name + " (นโยบายยังไม่เปิด)", ChannelType = ph.ChannelType });
                            gi.InfoOnly = true;
                            gi.Count += n; gi.Gross += gross; gi.Vat += vat;
                            gi.Note = "บันทึกในสมุดช่องทาง แต่นโยบายเอกสารขายยังไม่เปิด — ไม่ส่งบัญชี (พฤติกรรมเดิม)";
                            continue;
                        }

                        var g = G(day, ch, ph);
                        g.Count += n; g.Gross += gross; g.Vat += vat;
                        if (st == "OFF")
                        {
                            g.Flags.Add(FPolicyOff);
                            g.PolicyOffGross += gross; g.PolicyOffVat += vat;
                        }
                        else if (st == "POSTED" && rf.Length > 0) AttachReceiptDoc(res, g, rf, IsRollupId(rf));
                        else if (st == "PENDING" && Str(r["DM"]).Equals("DAILY_ROLLUP", StringComparison.OrdinalIgnoreCase) && day >= today)
                            g.Flags.Add(FToday);
                        else g.Flags.Add(FPending);            // PENDING / POSTING — รอส่ง
                    }
                }
                catch (Exception ex) { res.Notes.Add("อ่านสมุดการขายรายช่องทางไม่ได้: " + ex.Message); }
            }

            // ── D. สลิปที่ลูกค้าแนบเองรอตรวจ (ยังไม่ใช่รายได้) ─────────────────────────
            if (TableExists(conn, "Payment_Slips") && ColumnExists(conn, "Payment_Slips", "VerificationStatus"))
            {
                try
                {
                    bool hasClaim = ColumnExists(conn, "Payment_Slips", "Claimed_Amount");
                    var dtl = Query(conn,
                        "SELECT CAST(UploadedDate AS DATE) AS D, COUNT(*) AS N, " +
                        (hasClaim ? "SUM(ISNULL(Claimed_Amount, 0))" : "CAST(0 AS DECIMAL(18,2))") + " AS A " +
                        "FROM Payment_Slips WHERE VerificationStatus = 'PENDING' AND ISNULL(IsActive, 1) = 1 " +
                        "AND UploadedDate >= @f AND UploadedDate < @t GROUP BY CAST(UploadedDate AS DATE)",
                        new Dictionary<string, object> { { "@f", from }, { "@t", toEx } });
                    foreach (DataRow r in dtl.Rows)
                    {
                        DateTime day = NormalizeEra(Convert.ToDateTime(r["D"])).Date;
                        var g = G(day, ChReservation, new PaidHow { Name = "สลิปลูกค้ารอตรวจ", ChannelType = "TRANSFER" });
                        g.InfoOnly = true;
                        g.Count += Convert.ToInt32(r["N"]);
                        g.Gross += Dec(r["A"]);
                        g.Note = "ยังไม่ลงรับเงิน — อนุมัติที่หน้า ตรวจสลิปโอน";
                        g.Flags.Add(FUnverified);
                    }
                }
                catch { }
            }

            // ── นโยบาย: ตั้ง "ออกทันที" แต่ถูกรวบเข้าใบสรุปรายวัน (เช่น ขายโอนไปอยู่ใน POSDAY) ─────
            //    ไม่ตีธงกลับด้าน (ตั้งรวบแต่ออกรายใบ) — ลูกค้าขอใบกำกับเต็มรูปรายใบได้เสมอ
            foreach (var g in groups.Values)
            {
                if (g.InfoOnly || g.PolicyMode != "IMMEDIATE") continue;
                foreach (var d in g.Docs) if (d.Rollup) { g.Flags.Add(FPolicy); break; }
            }

            res.Groups.AddRange(groups.Values);
            res.Groups.Sort((a, b) =>
            {
                int c = a.Day.CompareTo(b.Day);
                if (c != 0) return c;
                c = string.CompareOrdinal(a.Channel, b.Channel);
                return c != 0 ? c : string.Compare(a.PaidHowName, b.PaidHowName, StringComparison.CurrentCulture);
            });
            return res;
        }

        private static PaidHow RoomServicePaidHow(string pm)
        {
            switch (pm)
            {
                case "CASH": return new PaidHow { Name = "รูมเซอร์วิส: เงินสด", ChannelType = "CASH" };
                case "TRANSFER": return new PaidHow { Name = "รูมเซอร์วิส: เงินโอน", ChannelType = "TRANSFER" };
                case "ONLINE": return new PaidHow { Name = "รูมเซอร์วิส: ออนไลน์", ChannelType = "GATEWAY_OTHER" };
                case "CHARGE_TO_ROOM": return new PaidHow { Name = "รูมเซอร์วิส: ลงบิลห้อง", ChannelType = "OTHER" };
                default: return new PaidHow { Name = "รูมเซอร์วิส: " + (pm.Length == 0 ? "ไม่ระบุ" : pm), ChannelType = "OTHER" };
            }
        }

        private static void AttachReceiptDoc(SalesResult res, SaleGroup g, string id, bool rollup)
        {
            foreach (var d in g.Docs) if (string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)) return;
            ReceiptState st;
            res.Receipts.TryGetValue(id, out st);
            string status = st == null ? "NONE" : st.Status;
            g.Docs.Add(new DocRef
            {
                Id = id, Rollup = rollup, Status = status,
                DocNo = st?.Create?.DocNumber ?? "", QueueId = st?.Create?.Id ?? 0
            });
            if (!res.IntegrationOn && !res.QueueReady) return;   // ไม่ได้เชื่อม NextAcc — ไม่ตีธง
            switch (status)
            {
                case "NONE": g.Flags.Add(FNoDoc); g.ProblemIds.Add(id); break;
                case "FAILED": g.Flags.Add(FFailed); g.ProblemIds.Add(id); break;
                case "PENDING":
                case "PROCESSING": g.Flags.Add(FPending); break;
                case "VOIDED": g.Flags.Add(FVoided); g.ProblemIds.Add(id + " (ยกเลิกแล้ว)"); break;
            }
        }

        /// <summary>แถบสถานะการตั้งค่าที่กระทบความครบของเอกสาร (เชื่อม NextAcc / รวบ POS / รูมเซอร์วิส / นโยบาย)</summary>
        public static string ConfigStatusHtml(SalesResult res)
        {
            var sb = new StringBuilder("<div class='tr-card tr-noprint' style='padding:10px 14px;font-size:13px'>");
            sb.Append("<b>สถานะการตั้งค่า:</b> ");
            sb.Append(res.IntegrationOn ? Chip("เชื่อม NextAcc: เปิด", "ok") : Chip("เชื่อม NextAcc: ปิด/ยังไม่ตั้งค่า", "err"));
            sb.Append(res.QueueReady ? "" : Chip("ไม่มีตารางคิว", "err"));
            sb.Append(res.PosRollupOn ? Chip("รวบ POS รายวัน: เปิด", "ok") : Chip("รวบ POS รายวัน: ปิด (ขายไม่ออกใบไม่ถึง NextAcc)", "warn"));
            if (res.RoomServiceTable)
                sb.Append(res.RoomServiceOn ? Chip("รายได้รูมเซอร์วิส: เปิด", "ok") : Chip("รายได้รูมเซอร์วิส: ปิด", "warn"));
            if (res.Policy != null)
            {
                if (!res.Policy.TableReady) sb.Append(Chip("นโยบายเอกสารขาย: ยังไม่ติดตั้ง (PHASE19_29)", "off"));
                else sb.Append(res.Policy.Enabled ? Chip("นโยบายเอกสารขาย: เปิด", "ok") : Chip("นโยบายเอกสารขาย: ปิด (ใช้พฤติกรรมเดิม)", "off"));
            }
            sb.Append(Chip(res.UseVat ? "จด VAT (ถอด 7%)" : "ไม่จด VAT", "info"));
            sb.Append("</div>");
            return sb.ToString();
        }

        public static string FlagChip(string f)
        {
            switch (f)
            {
                case FNoDoc: return Chip("ไม่มีเอกสาร NextAcc", "err");
                case FFailed: return Chip("คิวล้มเหลว", "err");
                case FPending: return Chip("รอส่ง", "warn");
                case FLate: return Chip("รายการมาช้า/ตกหล่น", "err");
                case FExcluded: return Chip("ตั้งค่าไม่รวมใบสรุป", "off");
                case FUnverified: return Chip("โอนยังไม่ตรวจ", "warn");
                case FVoided: return Chip("ยกเลิกบน NextAcc แล้ว", "off");
                case FToday: return Chip("รอรวบหลังจบวัน", "info");
                case FPolicy: return Chip("ไม่ตรงนโยบายเอกสาร", "warn");
                case FDeposit: return Chip("มีมัดจำ", "info");
                case FOrderPending: return Chip("ออเดอร์ยังไม่ยืนยัน", "warn");
                case FLegacy: return Chip("ข้อมูลเก่า/นำเข้า (ไม่มีเอกสาร)", "off");
                case FPolicyOff: return Chip("ไม่ส่งตามนโยบาย", "off");
                default: return Chip(f, "off");
            }
        }

        public static string FlagText(string f)
        {
            switch (f)
            {
                case FNoDoc: return "ไม่มีเอกสาร NextAcc";
                case FFailed: return "คิวล้มเหลว";
                case FPending: return "รอส่ง";
                case FLate: return "รายการมาช้า/ตกหล่น";
                case FExcluded: return "ตั้งค่าไม่รวมใบสรุป";
                case FUnverified: return "โอนยังไม่ตรวจ";
                case FVoided: return "ยกเลิกบน NextAcc แล้ว";
                case FToday: return "รอรวบหลังจบวัน";
                case FPolicy: return "ไม่ตรงนโยบายเอกสาร";
                case FDeposit: return "มีมัดจำ";
                case FOrderPending: return "ออเดอร์ยังไม่ยืนยัน";
                case FLegacy: return "ข้อมูลเก่า/นำเข้า";
                case FPolicyOff: return "ไม่ส่งตามนโยบาย";
                default: return f;
            }
        }

        /// <summary>CSS ร่วมของรายงานทั้งสามหน้า (ฝังในหน้า — ไม่ต้องเพิ่มไฟล์ static)</summary>
        public const string Css = @"
.tr-wrap{max-width:1280px;margin:0 auto;padding:18px 12px 60px}
.tr-head{background:linear-gradient(135deg,#37474f,#546e7a);color:#fff;border-radius:14px;padding:20px 24px;margin-bottom:16px}
.tr-head h2{margin:0 0 6px;font-weight:700;font-size:1.45em}.tr-head p{margin:0;opacity:.93;font-size:13.5px}
.tr-note{background:#fff8e1;border-left:4px solid #f9a825;padding:11px 14px;border-radius:8px;font-size:13px;color:#5d4037;margin-bottom:14px;line-height:1.6}
.tr-infobox{background:#e3f2fd;border-left:4px solid #1e88e5;padding:10px 14px;border-radius:8px;font-size:13px;color:#0d47a1;margin-bottom:14px;line-height:1.6}
.tr-card{background:#fff;border-radius:11px;box-shadow:0 2px 10px rgba(0,0,0,.08);padding:16px 18px;margin-bottom:16px}
.tr-card h3{margin:0 0 10px;font-size:1.1em;font-weight:700;color:#263238}
.tr-tools{display:flex;gap:10px;flex-wrap:wrap;align-items:flex-end}
.tr-tools label{display:block;font-size:12px;color:#607d8b;margin-bottom:3px}
.tr-tools input[type=date],.tr-tools input[type=text],.tr-tools select{padding:7px 10px;border:1.5px solid #dbe2e7;border-radius:8px;min-width:150px}
.tr-scroll{overflow-x:auto;border:1px solid #eef1f3;border-radius:8px}
table.tr-t{width:100%;border-collapse:collapse;font-size:12.8px}
table.tr-t th{background:#f5f7f9;text-align:left;padding:7px 8px;font-weight:650;color:#37474f;border-bottom:2px solid #e3e9ed;white-space:nowrap}
table.tr-t td{padding:6px 8px;border-bottom:1px solid #f0f3f5;vertical-align:top}
table.tr-t td.n,table.tr-t th.n{text-align:right;white-space:nowrap}
table.tr-t tr.sub td{background:#f7f9fa;font-weight:650}
table.tr-t tr.tot td{background:#eceff1;font-weight:700;border-top:2px solid #cfd8dc}
table.tr-t tr.info td{color:#78909c;font-style:italic}
table.tr-t tr.bad td{background:#fff5f5}
.tr-diff{color:#c62828;font-weight:700}
.tr-chip{display:inline-block;font-size:11px;padding:1px 7px;border-radius:10px;margin:1px 3px 1px 0;white-space:nowrap}
.tr-ok{background:#e8f5e9;color:#1e7e42}.tr-warn{background:#fff3e0;color:#e65100}.tr-err{background:#ffebee;color:#c62828}
.tr-off{background:#eceff1;color:#607d8b}.tr-info{background:#e3f2fd;color:#1565c0}
.tr-kpis{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:14px}
.tr-kpi{flex:1 1 160px;background:#fff;border-radius:10px;box-shadow:0 1px 6px rgba(0,0,0,.07);padding:12px 14px}
.tr-kpi .v{font-size:1.35em;font-weight:700;color:#263238}.tr-kpi .l{font-size:12px;color:#78909c}
.tr-sub{font-size:11.5px;color:#90a4ae}
@media print{.navbar,.tr-noprint,footer,.mega-menu{display:none!important}.tr-wrap{max-width:none;padding:0}
.tr-card{box-shadow:none;border:1px solid #ccc;page-break-inside:auto}.tr-head{background:none;color:#000;border:1px solid #999}
table.tr-t{font-size:10.5px}body{background:#fff!important}}
@media (max-width:640px){.tr-wrap{padding:12px 8px 40px}.tr-tools input,.tr-tools select{min-width:120px}}
";
    }
}
