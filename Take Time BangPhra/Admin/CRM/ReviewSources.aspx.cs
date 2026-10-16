// ===========================================================================
// ReviewSources.aspx.cs — ตั้งค่าแหล่งรีวิว: วิธีดึง/รอบดึง/เปิดดึงอัตโนมัติ, ความลับ (เขียนได้อย่างเดียว),
// ปุ่ม "ดึงตอนนี้", "ทดสอบดึง (ไม่บันทึก)" ของ AGODA_LIGHT, log การดึง, นำเข้า CSV, ป้อนรีวิวเอง, ค่าตั้งกลางของระบบรีวิว
//
// สิทธิ์: Perm.CrmReview + Feature "Reviews" — แก้ความลับ/ค่าตั้งกลางได้เฉพาะ Owner/Admin
// API: POST ?action=… (JSON, ต้องมี header X-CSRF-Token) / POST multipart ?action=importCsv (field csrf)
//      GET ?action=csvTemplate = ดาวน์โหลดแม่แบบ CSV
//      GET ?action=downloadRaw&code=…&t=<csrf> = เนื้อหาดิบของ "ทดสอบดึง" ล่าสุด (Owner/Admin, เก็บใน App_Data 24 ชม.)
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra.Services.Reviews;

namespace Take_Time_BangPhra.Admin.CRM
{
    public partial class ReviewSources : Page
    {
        private string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";
        private const string CsrfKey = "ReviewSources_Csrf";
        private const int MaxCsvBytes = 5 * 1024 * 1024;

        /// <summary>โทเคนกัน CSRF ของ session นี้ (ฝังในหน้าให้ JS ส่งกลับใน header)</summary>
        protected string CsrfToken
        {
            get
            {
                string t = Session[CsrfKey] as string;
                if (string.IsNullOrEmpty(t))
                {
                    t = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                    Session[CsrfKey] = t;
                }
                return t;
            }
        }

        /// <summary>Owner/Admin — แก้ความลับและค่าตั้งกลางได้</summary>
        protected bool IsSecretAdmin
        {
            get
            {
                string u = Session["User"] != null ? Session["User"].ToString() : "";
                return u == "Owner" || u == "Admin";
            }
        }

        private static readonly string[] SettingKeys =
        {
            "Review_Aggregation_Enabled", "Review_AI_AutoAnalyze", "Review_AI_Daily_Token_Budget",
            "Review_Negative_Threshold", "Review_Alert_MaxAgeDays", "Review_FetchFail_Alert_After", "Review_UrlFetch_Allowed"
        };

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            ViewStateUserKey = Session.SessionID;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["permission"] == null || Session["permission"].ToString() != "True")
            {
                Response.Redirect("~/Admin/Login", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            if (!Perm.Guard(this, Perm.CrmReview)) return;
            if (!Feature.Guard(this, "Reviews", "~/Default")) return;

            string action = Request.QueryString["action"] ?? "";
            if (Request.HttpMethod == "GET" && action == "csvTemplate") { SendTemplate(); return; }
            if (Request.HttpMethod == "GET" && action == "downloadRaw") { SendRaw(Request.QueryString["code"], Request.QueryString["t"]); return; }
            if (Request.HttpMethod != "POST" || action.Length == 0) return;

            Server.ScriptTimeout = 300;   // ดึงอีเมล/API อาจใช้เวลานาน
            Dictionary<string, object> result;
            try
            {
                if (action == "importCsv")
                {
                    if (!CheckCsrf(Request.Form["csrf"])) { WriteJson(Fail("CSRF token ไม่ถูกต้อง — โหลดหน้าใหม่")); return; }
                    result = HandleImportCsv();
                }
                else
                {
                    if (!CheckCsrf(Request.Headers["X-CSRF-Token"])) { WriteJson(Fail("CSRF token ไม่ถูกต้อง — โหลดหน้าใหม่")); return; }
                    string body;
                    using (var rd = new StreamReader(Request.InputStream, Encoding.UTF8)) body = rd.ReadToEnd();
                    var ser = new JavaScriptSerializer { MaxJsonLength = 10 * 1024 * 1024 };
                    var data = string.IsNullOrWhiteSpace(body) ? new Dictionary<string, object>() : ser.Deserialize<Dictionary<string, object>>(body);
                    switch (action)
                    {
                        case "getSources": result = HandleGetSources(); break;
                        case "saveSource": result = HandleSaveSource(data); break;
                        case "fetchNow": result = HandleFetchNow(data); break;
                        case "testAgoda": result = HandleTestAgoda(data); break;
                        case "getLog": result = HandleGetLog(data); break;
                        case "addManual": result = HandleAddManual(data); break;
                        case "saveSettings": result = HandleSaveSettings(data); break;
                        case "analyzeNow": result = HandleAnalyzeNow(); break;
                        default: result = Fail("ไม่รู้จักคำสั่ง: " + action); break;
                    }
                }
            }
            catch (Exception ex)
            {
                result = Fail("เกิดข้อผิดพลาด: " + ex.Message);
            }
            WriteJson(result);
        }

        private bool CheckCsrf(string token)
        {
            string expected = Session[CsrfKey] as string;
            return !string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(token)
                && string.Equals(expected, token, StringComparison.Ordinal);
        }

        // ── actions ───────────────────────────────────────────────────────────

        private Dictionary<string, object> HandleGetSources()
        {
            bool ready = ReviewSourceConfig.SchemaReady(ConnStr);
            var list = new List<Dictionary<string, object>>();
            var dt = new code().DatabaseQuerySafe(ConnStr, "SELECT * FROM AI_Review_Sources ORDER BY SourceCode", null);
            foreach (DataRow r in dt.Rows)
            {
                var s = ReviewSourceConfig.FromRow(r);
                if (s.SourceCode == "INTERNAL") continue;
                string apiJson = r.Table.Columns.Contains("ApiConfig") && r["ApiConfig"] != DBNull.Value ? r["ApiConfig"].ToString() : null;
                list.Add(new Dictionary<string, object>
                {
                    { "code", s.SourceCode }, { "name", s.SourceName },
                    { "icon", Col(r, "IconClass") }, { "color", Col(r, "BrandColor") },
                    { "origin", s.OriginPlatform },
                    { "method", s.IngestMethod ?? "" }, { "intervalMin", s.FetchIntervalMin },
                    { "autoEnabled", s.IsAutoEnabled }, { "legacyEnabled", s.IsLegacyEnabled },
                    { "sourceUrl", s.SourceUrl ?? "" },
                    { "nextFetchAt", Fmt(s.NextFetchAt) }, { "lastSuccessAt", Fmt(s.LastSuccessAt) },
                    { "lastFetchAt", Fmt(s.LastFetchDate) },
                    { "lastError", s.LastError ?? "" }, { "failures", s.ConsecutiveFailures },
                    { "platformRating", s.PlatformRating }, { "platformScale", s.PlatformRatingScale },
                    { "platformCount", s.PlatformReviewCount }, { "totalReviews", s.TotalReviews },
                    { "leaseUntil", Fmt(DateCol(r, "Lease_Until")) },
                    // ความลับถูกแทนด้วย ••••xxxx — ค่าจริงไม่ออกจาก server
                    { "config", ReviewSourceConfig.MaskedDictionary(apiJson) }
                });
            }

            var settings = new Dictionary<string, object>();
            foreach (var k in SettingKeys) settings[k] = AppCfg.Get(k, "");

            return new Dictionary<string, object>
            {
                { "success", true }, { "schemaReady", ready }, { "isSecretAdmin", IsSecretAdmin },
                { "sources", list }, { "settings", settings },
                { "methods", IngestMethods.All },
                { "aiUsedToday", ready ? ReviewAiBudget.UsedToday(ConnStr) : 0 },
                { "emailDefaults", EmailReviewAdapter.Defaults.ToDictionary(kv => kv.Key, kv => (object)kv.Value) }
            };
        }

        private Dictionary<string, object> HandleSaveSource(Dictionary<string, object> d)
        {
            if (!ReviewSourceConfig.SchemaReady(ConnStr)) return Fail("ยังไม่ได้รัน migration PHASE19_Migration_26_Review_Aggregation.sql");
            string code = Str(d, "code");
            var src = ReviewSourceConfig.Load(ConnStr, code);
            if (src == null) return Fail("ไม่พบแหล่ง " + code);

            string method = (Str(d, "method") ?? "").Trim().ToUpperInvariant();
            if (method.Length > 0 && !IngestMethods.IsValid(method)) return Fail("วิธีดึงไม่ถูกต้อง: " + method);
            int interval;
            if (!int.TryParse(Str(d, "intervalMin"), out interval)) interval = src.FetchIntervalMin;
            if (interval < 15) interval = 15;
            if (interval > 10080) interval = 10080;
            bool isAgodaLight = method == IngestMethods.AgodaLight;
            bool isGoogleLight = method == IngestMethods.GoogleTravelLight;
            bool isLight = LightFetch.IsLightMethod(method);
            if (isLight && interval < LightFetch.MinIntervalMin) interval = LightFetch.MinIntervalMin;
            bool auto = Bool(d, "autoEnabled");
            string url = (Str(d, "sourceUrl") ?? "").Trim();
            if (url.Length > 1000) return Fail("URL ยาวเกิน 1000 ตัวอักษร");
            if (url.Length > 0 && !Uri.IsWellFormedUriString(url, UriKind.Absolute)) return Fail("URL ไม่ถูกต้อง");
            if (isAgodaLight && url.Length > 0 && !AgodaLightAdapter.IsAgodaUrl(url))
                return Fail("AGODA_LIGHT ต้องใช้ URL หน้าที่พักบน https://www.agoda.com/…");
            if (isGoogleLight && url.Length > 0 && !GoogleTravelLightAdapter.IsGoogleTravelUrl(url))
                return Fail("GOOGLE_TRAVEL_LIGHT ต้องใช้ URL หน้ารีวิวบน https://www.google.com/travel/…");
            // วิธี light เป็น opt-in ของเจ้าของ: เปิดดึงอัตโนมัติ (ใหม่ หรือเพิ่งเปลี่ยนมาเป็นวิธีนี้) ได้เฉพาะ Owner/Admin
            if (isLight && auto && !IsSecretAdmin
                && (!src.IsAutoEnabled || !string.Equals(src.IngestMethod, method, StringComparison.OrdinalIgnoreCase)))
                return Fail("เปิดดึงอัตโนมัติวิธี " + method + " ได้เฉพาะ Owner หรือ Admin");

            // config: คีย์ที่ไม่ใช่ความลับแก้ได้ทุกคนที่มีสิทธิ์หน้านี้ / ความลับเฉพาะ Owner/Admin (เว้นว่าง = คงเดิม)
            var incoming = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var cfg = d.ContainsKey("config") ? d["config"] as Dictionary<string, object> : null;
            if (cfg != null)
            {
                if (cfg.Count > 40) return Fail("ค่าตั้งมากเกินไป");
                foreach (var kv in cfg)
                {
                    string k = (kv.Key ?? "").Trim();
                    if (k.Length == 0 || k.Length > 60) continue;
                    string v = kv.Value == null ? "" : kv.Value.ToString();
                    if (v.Length > 4000) return Fail("ค่า " + k + " ยาวเกินไป");
                    incoming[k] = v;
                }
            }
            if (!IsSecretAdmin && incoming.Keys.Any(k => ReviewSourceConfig.IsSecretKey(k)
                    && !string.IsNullOrEmpty(Convert.ToString(incoming[k])) && !ReviewSourceConfig.IsMasked(Convert.ToString(incoming[k]))))
                return Fail("แก้ API key / token ได้เฉพาะ Owner หรือ Admin");

            var dtOld = new code().DatabaseQuerySafe(ConnStr, "SELECT ApiConfig FROM AI_Review_Sources WHERE ID = @id",
                new Dictionary<string, object> { { "@id", src.Id } });
            string oldJson = dtOld.Rows.Count > 0 && dtOld.Rows[0][0] != DBNull.Value ? dtOld.Rows[0][0].ToString() : null;
            string newJson = cfg != null ? ReviewSourceConfig.MergeForSave(oldJson, incoming, IsSecretAdmin) : oldJson;

            new code().DatabaseInsertSafe(ConnStr, @"
UPDATE AI_Review_Sources SET
    Ingest_Method = @m, Fetch_Interval_Min = @i, Is_Enabled = @a, Source_Url = @u, ApiConfig = @c,
    -- เปิดดึงอัตโนมัติใหม่ / เปลี่ยนวิธี ⇒ ให้รอบถัดไปเริ่มเร็ว และล้างตัวนับล้มเหลว
    Next_Fetch_At = CASE WHEN @a = 1 AND (Is_Enabled = 0 OR ISNULL(Ingest_Method,'') <> ISNULL(@m,'')) THEN GETDATE() ELSE Next_Fetch_At END,
    Consecutive_Failures = CASE WHEN ISNULL(Ingest_Method,'') <> ISNULL(@m,'') THEN 0 ELSE Consecutive_Failures END,
    Updated_Date = GETDATE()
WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@m", method.Length == 0 ? null : method }, { "@i", interval }, { "@a", auto },
                    { "@u", url.Length == 0 ? null : url }, { "@c", newJson }, { "@id", src.Id }
                });
            new code().Logs(ConnStr, "ReviewSources", "แก้ไขแหล่งรีวิว " + src.SourceCode + " method=" + method + " auto=" + auto
                + " interval=" + interval, Session["UserName"] != null ? Session["UserName"].ToString() : "ADMIN");

            string warn = null;
            if (auto && method == IngestMethods.UrlFetch && !AppCfg.GetBool("Review_UrlFetch_Allowed", false))
                warn = "เปิดดึงอัตโนมัติแล้ว แต่วิธี URL_FETCH จะไม่ทำงานจนกว่า Owner เปิด 'อนุญาตดึงจากหน้าเว็บ OTA' (ขัดข้อกำหนดแพลตฟอร์ม)";
            if (isLight && auto)
                warn = (warn == null ? "" : warn + " · ") + method + " อ่านหน้าสาธารณะวันละ ≤ 1–2 ครั้ง (รอบขั้นต่ำ " + LightFetch.MinIntervalMin
                    + " นาที) — แพลตฟอร์มอาจถือว่าขัดข้อกำหนด; โดนกันบอท " + LightFetch.BlockDisableAfter + " ครั้งติดระบบปิดให้เอง"
                    + (isGoogleLight ? " · ทางที่แนะนำสำหรับ Google คือ GBP_API" : "");
            if (auto && !ReviewAggregationService.IsEnabled(ConnStr))
                warn = (warn == null ? "" : warn + " · ") + "สวิตช์ใหญ่ 'เปิดดึงรีวิวอัตโนมัติ' ยังปิดอยู่";
            return new Dictionary<string, object> { { "success", true }, { "message", "บันทึกแล้ว" }, { "warning", warn } };
        }

        private Dictionary<string, object> HandleFetchNow(Dictionary<string, object> d)
        {
            string code = Str(d, "code");
            if (string.IsNullOrEmpty(code)) return Fail("ไม่ระบุแหล่ง");
            var r = new ReviewAggregationService(ConnStr).RunSourceNow(code);
            var outD = r.ToDictionary();
            return outD;
        }

        /// <summary>
        /// ปุ่ม "ทดสอบดึง (ไม่บันทึก)" — รัน AgodaLightAdapter / GoogleTravelLightAdapter แบบ dry-run กับค่าที่บันทึกไว้ของแหล่ง
        /// ไม่เขียนรีวิว/สถานะแหล่ง/Review_Fetch_Log; กดได้ทุก 3 นาทีต่อแหล่ง (ยังเป็นคำขอจริงไป Agoda)
        /// </summary>
        private Dictionary<string, object> HandleTestAgoda(Dictionary<string, object> d)
        {
            string code = Str(d, "code");
            if (string.IsNullOrEmpty(code)) return Fail("ไม่ระบุแหล่ง");
            var src = ReviewSourceConfig.Load(ConnStr, code);
            if (src == null) return Fail("ไม่พบแหล่ง " + code);
            if (!LightFetch.IsLightMethod(src.IngestMethod))
                return Fail("ทดสอบได้เฉพาะแหล่งที่บันทึกวิธีเป็น AGODA_LIGHT หรือ GOOGLE_TRAVEL_LIGHT แล้ว");
            int wait;
            if (!LightFetch.TryReserveManual("test:" + src.SourceCode, 3, out wait))
                return Fail("เพิ่งทดสอบไป — รออีก " + wait + " วินาที (กันยิงปลายทางถี่)");
            var diag = string.Equals(src.IngestMethod, IngestMethods.GoogleTravelLight, StringComparison.OrdinalIgnoreCase)
                ? new GoogleTravelLightAdapter(ConnStr).DryRun(src)
                : new AgodaLightAdapter().DryRun(src);
            new code().Logs(ConnStr, "ReviewSources", "ทดสอบดึง " + src.IngestMethod + " " + src.SourceCode + ": " + diag.Compact()
                + (diag.Error != null ? " · " + ReviewHttp.Brief(diag.Error, 300) : ""),
                Session["UserName"] != null ? Session["UserName"].ToString() : "ADMIN");
            var dd = diag.ToDictionary();
            dd["rawSaved"] = SaveRaw(src.SourceCode, diag);
            dd["rawCanDownload"] = IsSecretAdmin;
            var outD = new Dictionary<string, object> { { "success", true }, { "diag", dd } };
            return outD;
        }

        // ── เนื้อหาดิบของการทดสอบล่าสุด (gzip ใน App_Data — IIS ไม่เสิร์ฟ App_Data ตรง; ลบเองหลัง 24 ชม.) ──

        private const int RawKeepHours = 24;

        private string RawDir { get { return Server.MapPath("~/App_Data/ReviewDebug"); } }

        private static string SafeCode(string code)
        {
            return Regex.Replace(code ?? "", @"[^A-Za-z0-9_]", "");
        }

        private void PurgeRaw()
        {
            try
            {
                if (!Directory.Exists(RawDir)) return;
                foreach (var f in Directory.GetFiles(RawDir, "*.txt.gz"))
                    if ((DateTime.Now - File.GetLastWriteTime(f)).TotalHours > RawKeepHours) File.Delete(f);
            }
            catch { }
        }

        private bool SaveRaw(string sourceCode, LightFetchDiagnostics diag)
        {
            try
            {
                PurgeRaw();
                if (diag == null || diag.RawBodies.Count == 0) return false;
                string code = SafeCode(sourceCode);
                if (code.Length == 0) return false;
                Directory.CreateDirectory(RawDir);
                string path = Path.Combine(RawDir, code + ".txt.gz");
                using (var fs = File.Create(path))
                using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
                using (var w = new StreamWriter(gz, new UTF8Encoding(false)))
                {
                    w.WriteLine("# " + diag.Method + " dry-run " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " source=" + code);
                    w.WriteLine("# " + diag.Compact());
                    foreach (var kv in diag.RawBodies)
                    {
                        w.WriteLine();
                        w.WriteLine("===== " + kv.Key + " (" + kv.Value.Length + " chars) =====");
                        w.WriteLine(kv.Value);
                    }
                }
                return true;
            }
            catch { return false; }
        }

        private void SendRaw(string sourceCode, string token)
        {
            if (!IsSecretAdmin || !CheckCsrf(token))
            {
                Response.StatusCode = 403;
                Response.Write("forbidden");
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            PurgeRaw();
            string code = SafeCode(sourceCode);
            string path = code.Length == 0 ? null : Path.Combine(RawDir, code + ".txt.gz");
            if (path == null || !File.Exists(path))
            {
                Response.StatusCode = 404;
                Response.Write("ไม่พบไฟล์ (หมดอายุ 24 ชม. หรือยังไม่ได้กดทดสอบดึง)");
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            new code().Logs(ConnStr, "ReviewSources", "ดาวน์โหลดเนื้อหาดิบทดสอบดึง " + code,
                Session["UserName"] != null ? Session["UserName"].ToString() : "ADMIN");
            Response.Clear();
            Response.ContentType = "application/gzip";
            Response.AddHeader("Content-Disposition", "attachment; filename=review_raw_" + code + "_"
                + File.GetLastWriteTime(path).ToString("yyyyMMdd_HHmm") + ".txt.gz");
            Response.AddHeader("X-Content-Type-Options", "nosniff");
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.TransmitFile(path);
            Response.Flush();
            Response.SuppressContent = true;
            Context.ApplicationInstance.CompleteRequest();
        }

        private Dictionary<string, object> HandleGetLog(Dictionary<string, object> d)
        {
            if (!ReviewSourceConfig.SchemaReady(ConnStr)) return new Dictionary<string, object> { { "success", true }, { "rows", new object[0] } };
            int? sid = null;
            string code = Str(d, "code");
            if (!string.IsNullOrEmpty(code))
            {
                var s = ReviewSourceConfig.Load(ConnStr, code);
                if (s != null) sid = s.Id;
            }
            int limit;
            if (!int.TryParse(Str(d, "limit"), out limit)) limit = 50;
            var dt = new ReviewStore(ConnStr).GetLog(sid, limit);
            var rows = new List<Dictionary<string, object>>();
            foreach (DataRow r in dt.Rows)
                rows.Add(new Dictionary<string, object>
                {
                    { "code", Col(r, "SourceCode") }, { "name", Col(r, "SourceName") },
                    { "startedAt", Fmt(DateCol(r, "Started_At")) }, { "finishedAt", Fmt(DateCol(r, "Finished_At")) },
                    { "status", Col(r, "Status") }, { "fetched", IntCol(r, "Fetched") },
                    { "inserted", IntCol(r, "Inserted") }, { "updated", IntCol(r, "Updated") },
                    { "message", Col(r, "Message") }
                });
            return new Dictionary<string, object> { { "success", true }, { "rows", rows } };
        }

        private Dictionary<string, object> HandleAddManual(Dictionary<string, object> d)
        {
            if (!ReviewSourceConfig.SchemaReady(ConnStr)) return Fail("ยังไม่ได้รัน migration PHASE19_26");
            var item = new ReviewItem
            {
                ExternalReviewId = Str(d, "externalId"),
                OriginPlatform = Str(d, "origin"),
                ReviewerName = Str(d, "reviewerName"),
                RatingRaw = ReviewRating.Parse(Str(d, "rating")),
                RatingScale = ReviewRating.Parse(Str(d, "scale")),
                Title = Str(d, "title"),
                Text = Str(d, "text"),
                ReviewDate = ReviewText.ParseDate(Str(d, "reviewDate")) ?? DateTime.Now,
                OwnerResponse = Str(d, "ownerResponse"),
                ReviewUrl = Str(d, "url"),
                StayDate = ReviewText.ParseDate(Str(d, "stayDate")),
                RoomType = Str(d, "roomType"),
                TravellerType = Str(d, "travellerType"),
                Language = Str(d, "language")
            };
            if (!string.IsNullOrEmpty(item.ReviewUrl) && !Uri.IsWellFormedUriString(item.ReviewUrl, UriKind.Absolute))
                return Fail("ลิงก์รีวิวไม่ถูกต้อง");
            try
            {
                var u = new ReviewImportService(ConnStr).AddManual(Str(d, "code"), item);
                string msg = u.Outcome == UpsertOutcome.Inserted ? "เพิ่มรีวิวแล้ว"
                    : u.Outcome == UpsertOutcome.Updated ? "มีรีวิวนี้อยู่แล้ว — อัปเดตข้อมูลที่ขาดให้"
                    : "มีรีวิวนี้อยู่แล้ว (ไม่เพิ่มซ้ำ)";
                return new Dictionary<string, object> { { "success", true }, { "message", msg }, { "id", u.ReviewId } };
            }
            catch (ArgumentException ax) { return Fail(ax.Message); }
        }

        private Dictionary<string, object> HandleImportCsv()
        {
            if (!ReviewSourceConfig.SchemaReady(ConnStr)) return Fail("ยังไม่ได้รัน migration PHASE19_26");
            if (Request.Files.Count == 0 || Request.Files[0] == null || Request.Files[0].ContentLength == 0)
                return Fail("ยังไม่ได้เลือกไฟล์");
            var f = Request.Files[0];
            if (f.ContentLength > MaxCsvBytes) return Fail("ไฟล์ใหญ่เกิน 5 MB");
            string ext = Path.GetExtension(f.FileName ?? "").ToLowerInvariant();
            if (ext != ".csv" && ext != ".txt" && ext != ".tsv")
                return Fail("รองรับเฉพาะไฟล์ .csv / .tsv (ถ้าได้ .xlsx จาก extranet ให้ Save As → CSV UTF-8 ก่อน)");
            string code = Request.Form["code"];
            string profile = Request.Form["profile"];
            decimal? scale = ReviewRating.Parse(Request.Form["scale"]);
            try
            {
                var res = new ReviewImportService(ConnStr).ImportCsv(code, profile, f.InputStream, scale);
                new code().Logs(ConnStr, "ReviewSources", "นำเข้า CSV " + code + " (" + profile + "): " + res.Message,
                    Session["UserName"] != null ? Session["UserName"].ToString() : "ADMIN");
                // วิเคราะห์ AI รีวิวใหม่ทันที (ภายใต้งบ) — ไม่สำเร็จก็ไม่เป็นไร ตัวตั้งเวลาจะทำต่อ
                string note = null;
                try
                {
                    if (res.Inserted > 0 && AppCfg.GetBool("Review_AI_AutoAnalyze", true))
                        new ReviewAiAnalyzer(ConnStr).RunBatch(Math.Min(24, res.Inserted), 8, out note);
                }
                catch { }
                return new Dictionary<string, object>
                {
                    { "success", true }, { "message", res.Message }, { "inserted", res.Inserted },
                    { "updated", res.Updated }, { "unchanged", res.Unchanged }, { "skipped", res.Skipped },
                    { "errors", res.Errors }, { "aiNote", note }
                };
            }
            catch (ArgumentException ax) { return Fail(ax.Message); }
        }

        private Dictionary<string, object> HandleSaveSettings(Dictionary<string, object> d)
        {
            if (!IsSecretAdmin) return Fail("แก้ค่าตั้งกลางได้เฉพาะ Owner หรือ Admin");
            short? adminId = null;
            short aid;
            if (Session["UserID"] != null && short.TryParse(Session["UserID"].ToString(), out aid)) adminId = aid;
            foreach (var k in SettingKeys)
            {
                if (!d.ContainsKey(k) || d[k] == null) continue;
                string v = d[k].ToString().Trim();
                if (k.EndsWith("_Enabled") || k.EndsWith("_Allowed") || k == "Review_AI_AutoAnalyze")
                    v = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)) ? "1" : "0";
                else
                {
                    int n;
                    if (!int.TryParse(v, out n) || n < 0) return Fail("ค่า " + k + " ต้องเป็นตัวเลข ≥ 0");
                    v = n.ToString();
                }
                AppCfg.Set(k, v, adminId);
            }
            new code().Logs(ConnStr, "ReviewSources", "แก้ค่าตั้งระบบรีวิว", Session["UserName"] != null ? Session["UserName"].ToString() : "ADMIN");
            string note = ReviewAggregationService.IsEnabled(ConnStr)
                ? "ถ้าเพิ่งเปิดสวิตช์ใหญ่ครั้งแรกและระบบยังไม่มีงานเบื้องหลังอื่นเปิดอยู่ ต้อง recycle App Pool หนึ่งครั้งให้ตัวตั้งเวลาเริ่ม"
                : null;
            return new Dictionary<string, object> { { "success", true }, { "message", "บันทึกค่าตั้งแล้ว" }, { "warning", note } };
        }

        private Dictionary<string, object> HandleAnalyzeNow()
        {
            if (!ReviewSourceConfig.SchemaReady(ConnStr)) return Fail("ยังไม่ได้รัน migration PHASE19_26");
            string note;
            int n = new ReviewAiAnalyzer(ConnStr).RunBatch(24, 8, out note);
            return new Dictionary<string, object>
            {
                { "success", true },
                { "message", "AI วิเคราะห์แล้ว " + n + " รีวิว" + (string.IsNullOrEmpty(note) ? "" : " (" + note + ")") }
            };
        }

        private void SendTemplate()
        {
            var sb = new StringBuilder();
            sb.AppendLine(ReviewImportService.GenericTemplateHeader);
            sb.AppendLine("Booking.com,,2026-09-30,Somchai K.,8.5,10,สะอาดมาก,\"ห้องสะอาด พนักงานใจดี\",วิวสวย,ที่จอดรถน้อย,ขอบคุณค่ะ,2026-10-01,2026-09-27,Deluxe Sea View,Couple,th,");
            sb.AppendLine("Google,,2026-10-02,Anna,5,5,,Great stay!,,,,,,,,en,");
            Response.Clear();
            Response.ContentType = "text/csv";
            Response.ContentEncoding = Encoding.UTF8;
            Response.AddHeader("Content-Disposition", "attachment; filename=review_import_template.csv");
            Response.BinaryWrite(Encoding.UTF8.GetPreamble());
            Response.Write(sb.ToString());
            Response.Flush();
            Response.SuppressContent = true;
            Context.ApplicationInstance.CompleteRequest();
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private void WriteJson(object data)
        {
            Response.Clear();
            Response.ContentType = "application/json";
            Response.Write(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(data));
            Response.Flush();
            Response.SuppressContent = true;
            Context.ApplicationInstance.CompleteRequest();
        }

        private static Dictionary<string, object> Fail(string msg)
        {
            return new Dictionary<string, object> { { "success", false }, { "message", msg } };
        }

        private static string Str(Dictionary<string, object> d, string k)
        {
            object v;
            if (d == null || !d.TryGetValue(k, out v) || v == null) return null;
            string s = v.ToString().Trim();
            return s.Length == 0 ? null : s;
        }

        private static bool Bool(Dictionary<string, object> d, string k)
        {
            string s = Str(d, k);
            return s != null && (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase));
        }

        private static string Col(DataRow r, string c)
        {
            return r.Table.Columns.Contains(c) && r[c] != DBNull.Value ? r[c].ToString() : null;
        }

        private static int? IntCol(DataRow r, string c)
        {
            if (!r.Table.Columns.Contains(c) || r[c] == DBNull.Value) return null;
            return Convert.ToInt32(r[c]);
        }

        private static DateTime? DateCol(DataRow r, string c)
        {
            if (!r.Table.Columns.Contains(c) || r[c] == DBNull.Value) return null;
            return Convert.ToDateTime(r[c]);
        }

        private static string Fmt(DateTime? d)
        {
            return d.HasValue ? d.Value.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) : "";
        }
    }
}
