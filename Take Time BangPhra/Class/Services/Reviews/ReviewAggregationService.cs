// ===========================================================================
// ReviewAggregationService.cs — ตัวตั้งเวลา/ตัวประสานการดึงรีวิวทุกแหล่ง
//
// RunIfDue(conn) ถูกเรียกจาก timer กลางใน Global.asax (เหมือน *IfDue ตัวอื่น) — ไม่ throw, ไม่บล็อก tick:
//   • สวิตช์ใหญ่ Review_Aggregation_Enabled (System_Config) + รายแหล่ง Is_Enabled
//   • กันถี่ในหน่วยความจำ (ทุก 5 นาที) + งานรันใน background task ทีละรอบ (Interlocked)
//   • แหล่งที่ถึงรอบ (Next_Fetch_At ≤ ตอนนี้) — lease ต่อแหล่งใน DB (UPDATE…WHERE Lease_Until หมดอายุ + @@ROWCOUNT)
//     กันสอง worker/สอง app pool ดึงแหล่งเดียวกันพร้อมกัน
//   • ล้มเหลว → exponential backoff (15 นาที × 2^(n-1)) + แจ้งเตือน REVIEW_FETCH_FAILING เมื่อครบ N ครั้งติด
//   • โดนกันบอท (ReviewSourceBlockedException — AGODA_LIGHT / GOOGLE_TRAVEL_LIGHT) → เลื่อนรอบเต็ม (ไม่ retry ถี่),
//     log ขึ้นต้น [BLOCKED], ครบ LightFetch.BlockDisableAfter ครั้งติด ⇒ ปิด Is_Enabled + แจ้งเตือน REVIEW_FETCH_FAILING ครั้งเดียว
//   • รีวิวใหม่คะแนนต่ำ (Rating_5 ≤ Review_Negative_Threshold) → แจ้งเตือน REVIEW_NEGATIVE
//   • หลังดึง: ให้ AI วิเคราะห์รีวิวใหม่ทีละชุดภายใต้งบ token ต่อวัน (Review_AI_AutoAnalyze)
// RunSourceNow(...) = ปุ่ม "ดึงตอนนี้" (ไม่สนรอบ/สวิตช์ใหญ่ แต่ยังใช้ lease)
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Take_Time_BangPhra.Services.Reviews
{
    public class ReviewAggregationService
    {
        private readonly string _conn;
        public ReviewAggregationService(string connectionString) { _conn = connectionString; }

        private const int LeaseMinutes = 20;
        private const int MaxSourcesPerRun = 5;
        private const int ManualAgodaCooldownMin = LightFetch.ManualCooldownMin;
        private static readonly TimeSpan RunBudget = TimeSpan.FromMinutes(8);

        private static int _running;                       // 0/1 — กันรันซ้อนใน process เดียว
        private static DateTime _lastRun = DateTime.MinValue;
        private static DateTime _lastPurge = DateTime.MinValue;
        private static readonly object _gate = new object();

        /// <summary>เปิดดึงรีวิวอัตโนมัติไหม (ใช้ใน Global.asax เพื่อเปิด timer ด้วย)</summary>
        public static bool IsEnabled(string conn)
        {
            try { return AppCfg.GetBool("Review_Aggregation_Enabled", false); }
            catch { return false; }
        }

        /// <summary>ตัวดึงตามวิธี — null = วิธีนี้ไม่มีการดึงอัตโนมัติ (CSV / MANUAL / AI_SEARCH)</summary>
        public IReviewSourceAdapter AdapterFor(string method)
        {
            switch ((method ?? "").ToUpperInvariant())
            {
                case IngestMethods.GbpApi: return new GoogleBusinessProfileAdapter();
                case IngestMethods.PlacesApi: return new PlacesApiAdapter();
                case IngestMethods.TripAdvisorApi: return new TripAdvisorContentApiAdapter();
                case IngestMethods.Email: return new EmailReviewAdapter(_conn);
                case IngestMethods.UrlFetch: return new UrlFetchAdapter(_conn);
                case IngestMethods.AgodaLight: return new AgodaLightAdapter();
                case IngestMethods.GoogleTravelLight: return new GoogleTravelLightAdapter(_conn);
                default: return null;
            }
        }

        // ── ตัวตั้งเวลา ───────────────────────────────────────────────────────

        public static void RunIfDue(string conn)
        {
            try
            {
                if (string.IsNullOrEmpty(conn) || !IsEnabled(conn)) return;
                lock (_gate)
                {
                    if ((DateTime.Now - _lastRun).TotalMinutes < 5) return;
                    _lastRun = DateTime.Now;
                }
                if (!ReviewSourceConfig.SchemaReady(conn)) return;
                if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;

                // ไม่บล็อก timer กลาง (คิวบัญชี/อีเมลจอง) — งาน IMAP/HTTP อาจนานหลายนาที
                Task.Run(() =>
                {
                    try { new ReviewAggregationService(conn).RunDueSources(); }
                    catch (Exception ex) { System.Diagnostics.Trace.TraceError("ReviewAggregation: " + ex.Message); }
                    finally { Interlocked.Exchange(ref _running, 0); }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("ReviewAggregation RunIfDue: " + ex.Message);
            }
        }

        internal void RunDueSources()
        {
            var started = DateTime.Now;
            var store = new ReviewStore(_conn);

            var dt = new code().DatabaseQuerySafe(_conn,
                @"SELECT TOP (@n) SourceCode FROM AI_Review_Sources
                   WHERE Is_Enabled = 1 AND SourceCode <> 'INTERNAL'
                     AND Ingest_Method IN ('GBP_API','PLACES_API','TRIPADVISOR_API','EMAIL','URL_FETCH','AGODA_LIGHT','GOOGLE_TRAVEL_LIGHT')
                     AND (Next_Fetch_At IS NULL OR Next_Fetch_At <= GETDATE())
                     AND (Lease_Until IS NULL OR Lease_Until < GETDATE())
                   ORDER BY ISNULL(Next_Fetch_At, '2000-01-01')",
                new Dictionary<string, object> { { "@n", MaxSourcesPerRun } });

            foreach (DataRow r in dt.Rows)
            {
                if (DateTime.Now - started > RunBudget) break;
                var src = ReviewSourceConfig.Load(_conn, r["SourceCode"].ToString());
                if (src == null) continue;
                var adapter = AdapterFor(src.IngestMethod);
                if (adapter == null) continue;
                if (!adapter.SupportsAutomatic)
                {
                    // เช่น URL_FETCH ขณะ Review_UrlFetch_Allowed = 0 — เลื่อนรอบ ไม่นับล้มเหลว
                    store.MarkSkipped(src, "วิธี " + src.IngestMethod + " ไม่อนุญาตให้ดึงอัตโนมัติ (Review_UrlFetch_Allowed = 0)");
                    store.FinishLog(0, src.Id, new SourceRunResult { SourceCode = src.SourceCode, Status = "SKIPPED",
                        Message = "ดึงอัตโนมัติถูกปิดสำหรับวิธี " + src.IngestMethod });
                    continue;
                }
                RunSource(src, adapter, "auto");
            }

            // งานรอง: แฮชแถวเก่า, AI วิเคราะห์, ล้าง log
            try { store.BackfillHashes(300); } catch { }
            try
            {
                if (AppCfg.GetBool("Review_AI_AutoAnalyze", true))
                {
                    string note;
                    new ReviewAiAnalyzer(_conn).RunBatch(40, 8, out note);
                }
            }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("ReviewAI batch: " + ex.Message); }
            if ((DateTime.Now - _lastPurge).TotalHours >= 24) { _lastPurge = DateTime.Now; store.PurgeOldLogs(); }
        }

        /// <summary>ปุ่ม "ดึงตอนนี้" — ใช้ lease เดียวกับตัวตั้งเวลา (กดซ้ำระหว่างดึงอยู่ = แจ้งว่ากำลังดึง)</summary>
        public SourceRunResult RunSourceNow(string sourceCode, string overrideMethod = null)
        {
            var res = new SourceRunResult { SourceCode = sourceCode };
            if (!ReviewSourceConfig.SchemaReady(_conn))
            {
                res.Status = "SKIPPED";
                res.Message = "ยังไม่ได้รัน migration PHASE19_Migration_26_Review_Aggregation.sql";
                return res;
            }
            var src = ReviewSourceConfig.Load(_conn, sourceCode);
            if (src == null) { res.Status = "FAILED"; res.Message = "ไม่พบแหล่ง " + sourceCode; return res; }
            string method = string.IsNullOrEmpty(overrideMethod) ? src.IngestMethod : overrideMethod;
            var adapter = AdapterFor(method);
            if (adapter == null)
            {
                res.Status = "SKIPPED";
                res.Message = "วิธี " + (method ?? "(ไม่ได้ตั้ง)") + " ไม่มีการดึงอัตโนมัติ — ใช้นำเข้า CSV / ป้อนเอง";
                return res;
            }
            // วิธี light (Agoda / Google Travel): กดดึงเองได้ไม่ถี่กว่า 30 นาที (กันกดซ้ำจนโดนกันบอท) — ไม่นับเป็นล้มเหลว
            if (LightFetch.IsLightMethod(adapter.Key) && src.LastFetchDate.HasValue
                && (DateTime.Now - src.LastFetchDate.Value).TotalMinutes < ManualAgodaCooldownMin)
            {
                res.Status = "SKIPPED";
                res.Message = adapter.Key + " เพิ่งดึงไปเมื่อ " + src.LastFetchDate.Value.ToString("HH:mm")
                    + " — กดดึงเองได้อีกครั้งหลัง " + src.LastFetchDate.Value.AddMinutes(ManualAgodaCooldownMin).ToString("HH:mm")
                    + " (ใช้ปุ่ม 'ทดสอบดึง' เพื่อดูผลโดยไม่บันทึก)";
                return res;
            }
            var r = RunSource(src, adapter, "manual");
            // วิเคราะห์ AI ทันทีเฉพาะรีวิวใหม่ของรอบนี้ (ภายใต้งบเดียวกัน)
            try
            {
                if (r.Inserted > 0 && AppCfg.GetBool("Review_AI_AutoAnalyze", true))
                {
                    string note;
                    new ReviewAiAnalyzer(_conn).RunBatch(Math.Min(16, r.Inserted), 8, out note);
                }
            }
            catch { }
            return r;
        }

        private SourceRunResult RunSource(ReviewSourceRow src, IReviewSourceAdapter adapter, string trigger)
        {
            var store = new ReviewStore(_conn);
            var res = new SourceRunResult { SourceCode = src.SourceCode };
            string owner = Environment.MachineName + "/" + System.Diagnostics.Process.GetCurrentProcess().Id + "/" + trigger;

            if (!store.TryLease(src.Id, LeaseMinutes, owner))
            {
                res.Status = "SKIPPED";
                res.Message = "แหล่งนี้กำลังถูกดึงอยู่ (มี lease) — ลองใหม่ภายหลัง";
                return res;
            }

            long logId = store.StartLog(src.Id);
            try
            {
                ReviewFetchResult fr = adapter.Fetch(src);
                res.Fetched = fr.Items.Count;
                int itemErrors = 0;
                string firstErr = null;
                foreach (var it in fr.Items)
                {
                    try
                    {
                        var u = store.Upsert(src, it, adapter.Key);
                        switch (u.Outcome)
                        {
                            case UpsertOutcome.Inserted:
                                res.Inserted++;
                                res.InsertedIds.Add(u.ReviewId);
                                break;
                            case UpsertOutcome.Updated: res.Updated++; break;
                            default: res.Unchanged++; break;
                        }
                    }
                    catch (Exception ix)
                    {
                        itemErrors++;
                        if (firstErr == null) firstErr = ix.Message;
                    }
                }

                var warns = new List<string>(fr.Warnings);
                if (itemErrors > 0) warns.Add("บันทึกไม่สำเร็จ " + itemErrors + " รายการ: " + firstErr);
                res.Status = warns.Count > 0 ? "PARTIAL" : "OK";
                res.Message = string.Format("ดึง {0} รายการ: ใหม่ {1}, อัปเดต {2}, ซ้ำ {3}", res.Fetched, res.Inserted, res.Updated, res.Unchanged)
                    + (string.IsNullOrEmpty(fr.Note) ? "" : " · " + fr.Note.Trim())
                    + (warns.Count > 0 ? " · ⚠ " + string.Join("; ", warns) : "");
                store.MarkSuccess(src, fr, owner, warns.Count > 0 ? string.Join("; ", warns) : null);
                store.FinishLog(logId, src.Id, res);
                AlertNegative(src, res.InsertedIds);
            }
            catch (ReviewSourceSkippedException sx)
            {
                res.Status = "SKIPPED";
                res.Message = sx.Message;
                store.MarkSkipped(src, sx.Message);
                store.FinishLog(logId, src.Id, res);
            }
            catch (ReviewSourceBlockedException bx)
            {
                // ปลายทางกันบอท: ไม่ retry ถี่ — เลื่อนเต็มรอบ (≥ 720 นาที) และนับลำดับติดกันจาก Review_Fetch_Log
                res.Status = "FAILED";
                res.Message = ReviewHttp.Brief(ReviewStore.BlockedPrefix + " " + bx.Message, 900);
                try { store.MarkFailure(src, res.Message, Math.Max(src.FetchIntervalMin, LightFetch.MinIntervalMin)); } catch { }
                store.FinishLog(logId, src.Id, res);
                HandleBlocked(src, store, bx);
            }
            catch (Exception ex)
            {
                res.Status = "FAILED";
                res.Message = ReviewHttp.Brief(ex.Message, 900);
                int failures = 0;
                // วิธี light: ล้มเหลวแบบอื่นก็ไม่ retry ถี่ (รอบถัดไปไม่เร็วกว่า 720 นาที)
                int minDelay = LightFetch.IsLightMethod(adapter.Key) ? LightFetch.MinIntervalMin : 0;
                try { failures = store.MarkFailure(src, res.Message, minDelay); } catch { }
                store.FinishLog(logId, src.Id, res);
                AlertFailing(src, failures, res.Message);
            }
            return res;
        }

        // ── แจ้งเตือน ─────────────────────────────────────────────────────────

        /// <summary>โดนกันบอทครบ N ครั้งติด ⇒ ปิดดึงอัตโนมัติของแหล่ง + แจ้งเตือนครั้งเดียว (เฉพาะตอนที่เพิ่งปิดจริง)</summary>
        private void HandleBlocked(ReviewSourceRow src, ReviewStore store, ReviewSourceBlockedException bx)
        {
            try
            {
                int n = store.CountConsecutiveBlocked(src.Id, LightFetch.BlockDisableAfter);
                if (n < LightFetch.BlockDisableAfter) return;
                string reason = "ปิดดึงอัตโนมัติแล้ว: " + src.SourceCode + " โดนกันบอท " + n + " ครั้งติดกัน (ล่าสุด " + bx.Marker
                    + ") — ตรวจด้วยปุ่ม 'ทดสอบดึง' แล้วเปิดใหม่เอง (Owner/Admin)";
                if (!store.DisableAuto(src.Id, reason)) return;   // ปิดอยู่แล้ว — ไม่แจ้งซ้ำ
                Notify.Send(Notify.Ev.ReviewFetchFailing,
                    "⛔ <b>ปิดดึงรีวิวอัตโนมัติ: " + E(src.SourceName ?? src.SourceCode) + "</b> (" + E(src.IngestMethod) + ")\n"
                    + "ปลายทางกันบอท " + n + " ครั้งติดกัน — ล่าสุด: <code>" + E(bx.Marker) + "</code>\n"
                    + "ระบบจะไม่ดึงอีกจนกว่า Owner/Admin เปิดใหม่\n"
                    + "<i>ตรวจที่ ลูกค้า &amp; การตลาด → แหล่งรีวิว → ทดสอบดึง (ไม่บันทึก)</i>");
            }
            catch { }
        }

        private void AlertFailing(ReviewSourceRow src, int failures, string error)
        {
            try
            {
                int n = Math.Max(1, AppCfg.GetInt("Review_FetchFail_Alert_After", 3));
                if (failures != n && !(failures > n && failures % (n * 4) == 0)) return;   // ครบ N แล้วเตือนซ้ำห่าง ๆ
                Notify.Send(Notify.Ev.ReviewFetchFailing,
                    "⚠️ <b>ดึงรีวิวไม่สำเร็จติดกัน " + failures + " ครั้ง</b>\n"
                    + "แหล่ง: <b>" + E(src.SourceName ?? src.SourceCode) + "</b> (" + E(src.IngestMethod) + ")\n"
                    + "สาเหตุล่าสุด: <code>" + E(ReviewText.Clip(error, 300)) + "</code>\n"
                    + "<i>ตรวจที่ ลูกค้า &amp; การตลาด → แหล่งรีวิว</i>");
            }
            catch { }
        }

        private void AlertNegative(ReviewSourceRow src, List<long> insertedIds)
        {
            if (insertedIds == null || insertedIds.Count == 0) return;
            try
            {
                decimal threshold = AppCfg.GetInt("Review_Negative_Threshold", 2);
                int maxAge = Math.Max(1, AppCfg.GetInt("Review_Alert_MaxAgeDays", 14));
                var ids = string.Join(",", insertedIds.Take(500).Select(i => i.ToString()));
                // ids เป็นตัวเลขที่ได้จาก SCOPE_IDENTITY เท่านั้น (ไม่ใช่ input ผู้ใช้)
                var dt = new code().DatabaseQuerySafe(_conn,
                    "SELECT TOP 20 ID, Origin_Platform, ReviewerName, Rating_Raw, Rating_Scale, Rating_5, ReviewTitle, ReviewText, Review_Url, ReviewDate " +
                    "FROM AI_Online_Reviews WHERE ID IN (" + ids + ") AND Is_Excluded = 0 AND Rating_5 IS NOT NULL AND Rating_5 <= @t " +
                    "AND ReviewDate >= DATEADD(DAY, -@age, GETDATE()) ORDER BY Rating_5, ID",
                    new Dictionary<string, object> { { "@t", threshold }, { "@age", maxAge } });
                if (dt.Rows.Count == 0) return;

                var sb = new StringBuilder();
                sb.Append("🔴 <b>รีวิวใหม่คะแนนต่ำ ").Append(dt.Rows.Count).Append(" รายการ</b> (").Append(E(src.SourceName ?? src.SourceCode)).Append(")\n");
                int shown = 0;
                foreach (DataRow r in dt.Rows)
                {
                    if (shown >= 5) { sb.Append("… และอีก ").Append(dt.Rows.Count - shown).Append(" รายการ\n"); break; }
                    shown++;
                    string raw = r["Rating_Raw"] == DBNull.Value ? "?" : Convert.ToDecimal(r["Rating_Raw"]).ToString("0.#");
                    string scale = r["Rating_Scale"] == DBNull.Value ? "?" : Convert.ToDecimal(r["Rating_Scale"]).ToString("0");
                    string body = r["ReviewText"] == DBNull.Value ? (r["ReviewTitle"] == DBNull.Value ? "" : r["ReviewTitle"].ToString()) : r["ReviewText"].ToString();
                    sb.Append("\n⭐ <b>").Append(raw).Append("/").Append(scale).Append("</b> · ")
                      .Append(E(r["Origin_Platform"] == DBNull.Value ? "" : r["Origin_Platform"].ToString()))
                      .Append(r["ReviewerName"] == DBNull.Value ? "" : " · " + E(r["ReviewerName"].ToString())).Append('\n');
                    if (body.Length > 0) sb.Append("<i>").Append(E(ReviewText.Clip(body, 250))).Append("</i>\n");
                    if (r["Review_Url"] != DBNull.Value) sb.Append(E(r["Review_Url"].ToString())).Append('\n');
                }
                sb.Append("\nตอบกลับเร็วช่วยคะแนนได้มาก — ดูร่างคำตอบ AI ที่หน้า วิเคราะห์รีวิว");
                Notify.Send(Notify.Ev.ReviewNegative, sb.ToString());
            }
            catch { }
        }

        private static string E(string s) { return WebUtility.HtmlEncode(s ?? ""); }
    }
}
