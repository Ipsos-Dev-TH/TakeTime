using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.UI;

namespace Take_Time_BangPhra.Integration
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //  ข้อมูลของป้าย "สถานะ NextAcc" (CLAUDE.md ข้อ 24) — อ่านอย่างเดียว, query เดียวต่อชุดรายการ (ไม่ N+1)
    //
    //  LatestByPayloadKey / LatestByEntityId — แถวคิวล่าสุดต่อรายการ (Status + Error_Class + Fix_Url + เลขเอกสาร)
    //  Render(...)       — ตัดสิน + ลิงก์ + HTML ในคำสั่งเดียว (ใช้ ResolveUrl ของหน้า)
    //  ReceiptBadges / CreditNoteBadges — ชุดสำเร็จรูปของใบเสร็จ / ใบลดหนี้ (หลายหน้าใช้ร่วม)
    //  ไม่มีตาราง/คอลัมน์ (ยังไม่รัน migration) = ป้ายแบบไม่มีชนิดปัญหา ไม่โยน
    // ══════════════════════════════════════════════════════════════════════════════════════════
    public static class NextAccStatusData
    {
        private const int ChunkSize = 80;

        // ── schema (แคช 10 นาที) ─────────────────────────────────────────────────────

        private static readonly object _colLock = new object();
        private static readonly Dictionary<string, bool> _cols = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _colsAt = DateTime.MinValue;

        /// <summary>มีคอลัมน์/ตารางไหม (col = null → ตาราง) — DB พัง = false</summary>
        public static bool HasColumn(string conn, string table, string col)
        {
            string key = table + "." + (col ?? "*");
            lock (_colLock)
            {
                if ((DateTime.Now - _colsAt).TotalMinutes > 10) { _cols.Clear(); _colsAt = DateTime.Now; }
                bool v;
                if (_cols.TryGetValue(key, out v)) return v;
            }
            bool ok = false;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    col == null ? "SELECT CASE WHEN OBJECT_ID(@t, 'U') IS NULL THEN 0 ELSE 1 END"
                                : "SELECT CASE WHEN COL_LENGTH(@t, @c) IS NULL THEN 0 ELSE 1 END",
                    col == null ? new Dictionary<string, object> { { "@t", "dbo." + table } }
                                : new Dictionary<string, object> { { "@t", "dbo." + table }, { "@c", col } });
                ok = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 1;
            }
            catch { ok = false; }
            lock (_colLock) { _cols[key] = ok; }
            return ok;
        }

        private static string QueueSelectCols(string conn)
        {
            string s = "ID, Status, Action_Type, Entity_ID, Payload, LEFT(ISNULL(Error_Message, N''), 600) AS Error_Message, Nexaacc_Response_Id, Created_Date";
            s += HasColumn(conn, "Accounting_Sync_Queue", "Nexaacc_Document_Number")
                ? ", Nexaacc_Document_Number" : ", CAST(NULL AS NVARCHAR(60)) AS Nexaacc_Document_Number";
            s += HasColumn(conn, "Accounting_Sync_Queue", "Nexaacc_Document_Type")
                ? ", Nexaacc_Document_Type" : ", CAST(NULL AS NVARCHAR(30)) AS Nexaacc_Document_Type";
            s += ReceiptPreflight.QueueSchemaReady(conn)
                ? ", Error_Class, Fix_Url" : ", CAST(NULL AS NVARCHAR(20)) AS Error_Class, CAST(NULL AS NVARCHAR(400)) AS Fix_Url";
            return s;
        }

        /// <summary>DataRow ของคิว → NaQueueRow (คอลัมน์ที่ไม่มี = null)</summary>
        public static NextAccUi.NaQueueRow FromDataRow(DataRow r)
        {
            if (r == null) return null;
            Func<string, string> s = c => r.Table.Columns.Contains(c) && r[c] != DBNull.Value ? Convert.ToString(r[c], CultureInfo.InvariantCulture) : null;
            long id; long.TryParse(s("ID") ?? "0", out id);
            DateTime? created = null;
            if (r.Table.Columns.Contains("Created_Date") && r["Created_Date"] != DBNull.Value) created = Convert.ToDateTime(r["Created_Date"]);
            return new NextAccUi.NaQueueRow
            {
                Id = id, Status = s("Status"), ActionType = s("Action_Type"), ErrorClass = s("Error_Class"),
                ErrorMessage = s("Error_Message"), FixUrl = s("Fix_Url"), ResponseId = s("Nexaacc_Response_Id"),
                DocNumber = s("Nexaacc_Document_Number"), DocType = s("Nexaacc_Document_Type"), Created = created
            };
        }

        private static string EscLike(string s)
        {
            return (s ?? "").Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }

        /// <summary>แถวคิวล่าสุดต่อค่า payload key (เช่น receiptNumber / stockRef / refKey) — ID มากสุดชนะ.
        /// entityType = null ไม่กรองชนิดรายการ. query ละ ≤ 80 คีย์ (ปกติหน้าละ 1 query)</summary>
        public static Dictionary<string, NextAccUi.NaQueueRow> LatestByPayloadKey(string conn, string entityType, string[] actionTypes,
                                                                                 string jsonKey, IEnumerable<string> keys)
        {
            var result = new Dictionary<string, NextAccUi.NaQueueRow>(StringComparer.OrdinalIgnoreCase);
            var list = (keys ?? Enumerable.Empty<string>()).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim())
                       .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (list.Count == 0 || actionTypes == null || actionTypes.Length == 0 || string.IsNullOrEmpty(jsonKey)) return result;
            var wanted = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
            string cols = QueueSelectCols(conn);
            for (int off = 0; off < list.Count; off += ChunkSize)
            {
                var chunk = list.Skip(off).Take(ChunkSize).ToList();
                var ps = new Dictionary<string, object>();
                var acts = new List<string>();
                for (int a = 0; a < actionTypes.Length; a++) { acts.Add("@a" + a); ps["@a" + a] = actionTypes[a]; }
                var likes = new List<string>();
                for (int k = 0; k < chunk.Count; k++)
                {
                    likes.Add("Payload LIKE @p" + k);
                    ps["@p" + k] = "%\"" + jsonKey + "\":\"" + EscLike(chunk[k]) + "\"%";
                }
                string where = "Action_Type IN (" + string.Join(",", acts) + ") AND (" + string.Join(" OR ", likes) + ")";
                if (!string.IsNullOrEmpty(entityType)) { where = "Entity_Type = @e AND " + where; ps["@e"] = entityType; }
                DataTable dt = null;
                try { dt = new code().DatabaseQuerySafe(conn, "SELECT " + cols + " FROM Accounting_Sync_Queue WHERE " + where + " ORDER BY ID DESC", ps); }
                catch { dt = null; }
                if (dt == null) continue;
                foreach (DataRow r in dt.Rows)
                {
                    string v = NextAccUi.PayloadValue(r["Payload"] as string, jsonKey);
                    if (string.IsNullOrEmpty(v) || !wanted.Contains(v) || result.ContainsKey(v)) continue;
                    result[v] = FromDataRow(r);
                }
            }
            return result;
        }

        /// <summary>แถวคิวล่าสุดต่อ Entity_ID (เช่น ใบลดหนี้ / งวดเงินเดือน)</summary>
        public static Dictionary<long, NextAccUi.NaQueueRow> LatestByEntityId(string conn, string entityType, string[] actionTypes, IEnumerable<long> ids)
        {
            var result = new Dictionary<long, NextAccUi.NaQueueRow>();
            var list = (ids ?? Enumerable.Empty<long>()).Where(x => x > 0).Distinct().ToList();
            if (list.Count == 0 || actionTypes == null || actionTypes.Length == 0) return result;
            string cols = QueueSelectCols(conn);
            for (int off = 0; off < list.Count; off += 500)
            {
                var chunk = list.Skip(off).Take(500).ToList();
                var ps = new Dictionary<string, object> { { "@e", entityType } };
                var acts = new List<string>();
                for (int a = 0; a < actionTypes.Length; a++) { acts.Add("@a" + a); ps["@a" + a] = actionTypes[a]; }
                // id เป็นตัวเลขจาก long — ต่อ string ได้ปลอดภัย (ไม่มีอักขระอื่น)
                string idList = string.Join(",", chunk.Select(x => x.ToString(CultureInfo.InvariantCulture)));
                DataTable dt = null;
                try
                {
                    dt = new code().DatabaseQuerySafe(conn,
                        "SELECT " + cols + " FROM Accounting_Sync_Queue WHERE Entity_Type = @e AND Action_Type IN (" + string.Join(",", acts)
                        + ") AND Entity_ID IN (" + idList + ") ORDER BY ID DESC", ps);
                }
                catch { dt = null; }
                if (dt == null) continue;
                foreach (DataRow r in dt.Rows)
                {
                    long id = Convert.ToInt64(r["Entity_ID"]);
                    if (!result.ContainsKey(id)) result[id] = FromDataRow(r);
                }
            }
            return result;
        }

        // ── ที่อยู่เว็บ NextAcc + สถานะล่ม (ต่อคำขอ) ───────────────────────────────────

        public sealed class LinkContext
        {
            public string WebBase;
            public Guid CompanyId;
            public bool ServerDown;
            public DateTime? ServerDownUntil;
        }

        public static LinkContext GetLinkContext(string conn)
        {
            const string key = "__NaLinkCtx";
            var ctx = HttpContext.Current;
            if (ctx != null && ctx.Items[key] is LinkContext) return (LinkContext)ctx.Items[key];
            var lc = new LinkContext();
            try
            {
                var cfg = new AccountingConfig(conn);
                if (cfg.IsConfigured)
                {
                    lc.WebBase = cfg.WebBaseUrl;
                    lc.CompanyId = cfg.CompanyId;
                }
            }
            catch { }
            try
            {
                DateTime until; string err;
                if (AccountingApiClient.IsServerDown(out until, out err)) { lc.ServerDown = true; lc.ServerDownUntil = until; }
            }
            catch { }
            if (ctx != null) ctx.Items[key] = lc;
            return lc;
        }

        /// <summary>ตัดสิน + ลิงก์ + HTML. queueQuery = คำค้นหน้าคิว (เลขใบ) · pdfUrl = path ตัวดู PDF ที่มีอยู่ (null = ไม่มี)</summary>
        public static string Render(Control ctl, string conn, NextAccUi.NaBadgeInput input, string queueQuery, string pdfUrl,
                                    out NextAccUi.NaBadge badge, bool compact = false)
        {
            var lc = GetLinkContext(conn);
            input = input ?? new NextAccUi.NaBadgeInput();
            input.ServerDown = lc.ServerDown;
            input.ServerDownUntil = lc.ServerDownUntil;
            badge = NextAccUi.ResolveBadge(input);
            Func<string, string> res = p => ctl != null ? ctl.ResolveUrl("~" + p) : p;
            var links = NextAccUi.BuildBadgeLinks(badge, input, lc.WebBase, lc.CompanyId, queueQuery, pdfUrl, res);
            return NextAccUi.RenderBadge(badge, links, compact);
        }

        public static string Render(Control ctl, string conn, NextAccUi.NaBadgeInput input, string queueQuery, string pdfUrl, bool compact = false)
        {
            NextAccUi.NaBadge b;
            return Render(ctl, conn, input, queueQuery, pdfUrl, out b, compact);
        }

        /// <summary>เหมือน Render แต่คืนแบบโครงสร้างสำหรับ JSON (หน้าที่ render ด้วย JS)</summary>
        public static Dictionary<string, object> Describe(Control ctl, string conn, NextAccUi.NaBadgeInput input, string queueQuery, string pdfUrl)
        {
            var lc = GetLinkContext(conn);
            input = input ?? new NextAccUi.NaBadgeInput();
            input.ServerDown = lc.ServerDown;
            input.ServerDownUntil = lc.ServerDownUntil;
            var badge = NextAccUi.ResolveBadge(input);
            Func<string, string> res = p => ctl != null ? ctl.ResolveUrl("~" + p) : p;
            return NextAccUi.BadgeToDict(badge, NextAccUi.BuildBadgeLinks(badge, input, lc.WebBase, lc.CompanyId, queueQuery, pdfUrl, res));
        }

        // ── ชุดสำเร็จรูป: ใบเสร็จ / ใบกำกับ ─────────────────────────────────────────────

        public static readonly string[] ReceiptCreateActions = { "CREATE_RECEIPT_DOCUMENT" };

        /// <summary>ข้อมูลป้ายของใบเสร็จหลายใบ — 2 query (คิว CREATE_RECEIPT_DOCUMENT + Account_Receipt.Nexaacc_Doc_Id/สถานะ)</summary>
        public static Dictionary<string, NextAccUi.NaBadgeInput> ReceiptBadgeInputs(string conn, IEnumerable<string> receiptIds)
        {
            var map = new Dictionary<string, NextAccUi.NaBadgeInput>(StringComparer.OrdinalIgnoreCase);
            var ids = (receiptIds ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
                      .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (ids.Count == 0) return map;
            var q = LatestByPayloadKey(conn, "RECEIPT", ReceiptCreateActions, "receiptNumber", ids);
            var local = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            try
            {
                bool docCol = HasColumn(conn, "Account_Receipt", "Nexaacc_Doc_Id");
                bool numCol = HasColumn(conn, "Account_Receipt", "Nexaacc_Doc_Number");
                for (int off = 0; off < ids.Count; off += 500)
                {
                    var chunk = ids.Skip(off).Take(500).ToList();
                    var ps = new Dictionary<string, object>();
                    var names = new List<string>();
                    for (int k = 0; k < chunk.Count; k++) { names.Add("@r" + k); ps["@r" + k] = chunk[k]; }
                    var dt = new code().DatabaseQuerySafe(conn,
                        "SELECT ID, Status" + (docCol ? ", CAST(Nexaacc_Doc_Id AS NVARCHAR(50)) AS DocId" : ", CAST(NULL AS NVARCHAR(50)) AS DocId")
                        + (numCol ? ", Nexaacc_Doc_Number AS DocNo" : ", CAST(NULL AS NVARCHAR(60)) AS DocNo")
                        + " FROM Account_Receipt WHERE ID IN (" + string.Join(",", names) + ")", ps);
                    if (dt != null) foreach (DataRow r in dt.Rows) local[Convert.ToString(r["ID"])] = r;
                }
            }
            catch { }
            foreach (var id in ids)
            {
                DataRow lr;
                local.TryGetValue(id, out lr);
                NextAccUi.NaQueueRow qr;
                q.TryGetValue(id, out qr);
                string st = lr != null ? Convert.ToString(lr["Status"]) : "";
                map[id] = new NextAccUi.NaBadgeInput
                {
                    Queue = qr,
                    DocId = lr != null && lr["DocId"] != DBNull.Value ? Convert.ToString(lr["DocId"]) : null,
                    DocNumber = lr != null && lr["DocNo"] != DBNull.Value ? Convert.ToString(lr["DocNo"]) : null,
                    Voided = string.Equals(st, "Cancel", StringComparison.OrdinalIgnoreCase)
                };
            }
            return map;
        }

        /// <summary>ตัวดู PDF ของใบเสร็จ (NextAcc smart cache → ไฟล์ local)</summary>
        public static string ReceiptPdfPath(string receiptId)
        {
            return "/API/ViewReceiptDoc.ashx?doc=" + Uri.EscapeDataString(receiptId ?? "");
        }

        /// <summary>ป้ายของใบเสร็จหลายใบ (HTML). คืน เลขใบ → HTML</summary>
        public static Dictionary<string, string> ReceiptBadges(Control ctl, string conn, IEnumerable<string> receiptIds, bool compact = false)
        {
            var html = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in ReceiptBadgeInputs(conn, receiptIds))
                html[kv.Key] = Render(ctl, conn, kv.Value, kv.Key, ReceiptPdfPath(kv.Key), compact);
            return html;
        }

        // ── ชุดสำเร็จรูป: ใบลดหนี้ (Credit_Note) ──────────────────────────────────────────

        public static readonly string[] CreditNoteActions = { "CREATE_CREDIT_NOTE", "RECORD_CN_REFUND" };

        /// <summary>ข้อมูลใบลดหนี้ที่ป้ายต้องใช้ (จากแถว Credit_Note)</summary>
        public sealed class CnRow
        {
            public long Id;
            public string CnNo, Status, DocId, DocNo, Path, SyncError;
        }

        /// <summary>ป้ายของใบลดหนี้หลายใบ — 1 query คิว. ร่าง = ยังไม่ส่ง (รออนุมัติ) · ยกเลิก = ยกเลิกแล้ว · SYNCED/มีเอกสาร = ส่งแล้ว</summary>
        public static Dictionary<long, string> CreditNoteBadges(Control ctl, string conn, IList<CnRow> rows, bool compact = false)
        {
            var html = new Dictionary<long, string>();
            if (rows == null || rows.Count == 0) return html;
            var q = LatestByEntityId(conn, "CREDIT_NOTE", CreditNoteActions, rows.Select(r => r.Id));
            foreach (var r in rows)
            {
                NextAccUi.NaQueueRow qr;
                q.TryGetValue(r.Id, out qr);
                string st = (r.Status ?? "").Trim().ToUpperInvariant();
                bool journal = string.Equals(r.Path, "JOURNAL", StringComparison.OrdinalIgnoreCase);
                var input = new NextAccUi.NaBadgeInput
                {
                    Queue = qr,
                    DocId = r.DocId,
                    DocNumber = r.DocNo,
                    DocType = journal ? "JOURNAL" : null,
                    Voided = st == "VOIDED",
                    NotApplicable = st == "DRAFT",
                    NotApplicableLabel = "รออนุมัติ — ยังไม่ส่ง",
                    NotApplicableReason = "ใบลดหนี้ร่าง — ส่งเข้า NextAcc เมื่ออนุมัติ"
                };
                // ส่งแล้วแต่คิวล่าสุดเป็น "บันทึกคืนเงิน" ที่ล้ม → ยังแสดงปัญหาของคิวนั้น (มีลิงก์เปิดเอกสารเดิม)
                if (qr == null && !string.IsNullOrWhiteSpace(r.SyncError) && st != "VOIDED" && st != "DRAFT" && string.IsNullOrWhiteSpace(r.DocId))
                    input.Queue = new NextAccUi.NaQueueRow { Status = "FAILED", ErrorMessage = r.SyncError };
                string pdf = !string.IsNullOrWhiteSpace(r.DocId) && !journal ? "/Account/CreditNotes?pdf=" + r.Id.ToString(CultureInfo.InvariantCulture) : null;
                html[r.Id] = Render(ctl, conn, input, r.CnNo, pdf, compact);
            }
            return html;
        }
    }
}
