// ===========================================================================
// AgodaLightAdapter.cs — AGODA_LIGHT: อ่านหน้าที่พัก Agoda "เท่าที่หน้าแสดง" วันละครั้ง (ไม่ crawl)
//
// นโยบายเจ้าของ: ไม่ไล่หน้า/ไม่ไล่ลิงก์ — ต่อรอบใช้ HTTP ไม่เกิน 2 คำขอ (รวม redirect):
//   1) GET หน้าที่พัก (Source_Url) ผ่าน ReviewHttp (กัน SSRF + allow-list; บังคับโฮสต์ agoda.com)
//      → JSON-LD aggregateRating (คะแนนรวม/จำนวนรีวิว) + รหัสที่พัก + รีวิวที่ฝังมากับ HTML (ถ้ามี)
//   2) ถ้าหน้าไม่มีรีวิวฝังมา และหา property id ได้: เรียก endpoint รีวิวที่หน้าเว็บใช้เอง "หน้าแรก ล่าสุดก่อน" หนึ่งครั้ง
//      ⚠ ค่าเริ่มของ endpoint/method/body ด้านล่าง "ยังไม่ได้ยืนยัน" (sandbox เข้า agoda.com ไม่ได้) —
//        ตรวจด้วยปุ่ม "ทดสอบดึง (ไม่บันทึก)" บนเซิร์ฟเวอร์จริง แล้วแก้ใน ApiConfig: apiUrl / apiMethod / apiBody / apiHeaders
// ความปลอดภัย:
//   • รอบดึงขั้นต่ำ 720 นาที (ค่าเริ่ม 1440) — บังคับใน ReviewSourceConfig.FromRow + หน้าแอดมิน
//   • โดนกันบอท (403/429/503, captcha, Access Denied, body ว่าง) ⇒ ReviewSourceBlockedException:
//     ไม่ retry ถี่ (เลื่อนรอบเต็ม) และครบ 3 ครั้งติดกัน ReviewAggregationService ปิด Is_Enabled + แจ้งเตือนครั้งเดียว
//   • ไม่อิง Review_UrlFetch_Allowed — opt-in ด้วย Is_Enabled ของแหล่ง ซึ่งเปิดได้เฉพาะ Owner/Admin
//   • เพิ่มเฉพาะรีวิวที่ยังไม่มี (ReviewItem.InsertOnly) — ไม่เขียนข้อความเดิมทับ
// ApiConfig (ไม่บังคับ): propertyId, apiEnabled (true), apiUrl, apiMethod (POST|GET), apiBody (ใช้ {hotelId}),
//   apiHeaders ("Name: value | Name2: value"), forwardCookies (true), providerMap ("332:Agoda,…"), maxItems (20), ratingScale (10),
//   languageId (22 = ไทย — ยังไม่ยืนยัน; "off" = ไม่ใส่ใน body), locale (th-th), sendLocaleHeaders (false → ag-language-locale/ag-language-id),
//   summaryUrl / summaryMethod (GET) / summaryBody — endpoint สรุปคะแนน (ปิดเป็นค่าเริ่ม; ตั้งแล้วงบคำขอ +1 = 3 และเรียกเฉพาะเมื่อหาคะแนนรวมไม่ได้)
// placeholder ใน apiUrl/apiBody/summaryUrl/summaryBody: {hotelId} {languageId} {locale}
// ข้อความรีวิว: เก็บ "ต้นฉบับ" (originalComment/originalTitle …) แทนคำแปลของ Agoda เมื่อมี, Language จากภาษาต้นฉบับ
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Services.Reviews
{
    public class AgodaLightAdapter : IReviewSourceAdapter
    {
        /// <summary>รอบดึงขั้นต่ำของวิธีนี้ (นาที)</summary>
        public const int MinIntervalMin = 720;
        /// <summary>คำขอ HTTP สูงสุดต่อรอบ (รวม redirect)</summary>
        public const int MaxRequestsPerRun = 2;
        /// <summary>โดนกันบอทติดกันกี่ครั้งจึงปิดดึงอัตโนมัติ</summary>
        public const int BlockDisableAfter = 3;

        // ⚠ UNVERIFIED DEFAULTS — ยืนยันบนเซิร์ฟเวอร์จริงด้วยปุ่มทดสอบ (ดู DevTools → Network ของหน้าที่พักว่าหน้าเรียกอะไร)
        public const string DefaultApiUrl = "https://www.agoda.com/api/cronos/property/review/ReviewComments";
        public const string DefaultApiMethod = "POST";
        public const string DefaultApiBody =
            "{\"hotelId\":{hotelId},\"providerId\":332,\"demographicId\":0,\"languageId\":{languageId},\"page\":1,\"pageSize\":20,\"sorting\":1," +
            "\"providerIds\":[332],\"isReviewPage\":false,\"isCrawlablePage\":true,\"filters\":{\"language\":[],\"room\":[]}}";

        private const string BrowserUa = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
        private const string AcceptLanguage = "th-TH,th;q=0.9,en-US;q=0.6,en;q=0.5";
        /// <summary>⚠ ยังไม่ยืนยัน: languageId ไทยของ Agoda (ตั้งได้ที่ ApiConfig languageId)</summary>
        public const string DefaultLanguageId = "22";
        public const string DefaultLocale = "th-th";
        private const int TimeoutMs = 25000;

        public string Key { get { return IngestMethods.AgodaLight; } }

        /// <summary>opt-in ด้วย Is_Enabled ของแหล่งเท่านั้น (ไม่ผูกกับ Review_UrlFetch_Allowed)</summary>
        public bool SupportsAutomatic { get { return true; } }

        public ReviewFetchResult Fetch(ReviewSourceRow src)
        {
            return Run(src, new AgodaLightDiagnostics());
        }

        /// <summary>ปุ่ม "ทดสอบดึง (ไม่บันทึก)" — รันเหมือนจริงแต่ไม่บันทึก/ไม่นับล้มเหลว/ไม่แจ้งเตือน</summary>
        public AgodaLightDiagnostics DryRun(ReviewSourceRow src)
        {
            var diag = new AgodaLightDiagnostics { DryRun = true };
            try { Run(src, diag); }
            catch (ReviewSourceBlockedException bx) { diag.Blocked = true; diag.Error = bx.Message; }
            catch (Exception ex) { diag.Error = ex.Message; }
            return diag;
        }

        // ── กันกดถี่ (ปุ่มทดสอบ/ดึงตอนนี้) — ในหน่วยความจำต่อ process ──────────────
        private static readonly Dictionary<string, DateTime> _manualGate = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _gateLock = new object();

        /// <summary>จองสิทธิ์เรียก Agoda แบบกดเอง: ห่างกันอย่างน้อย minutes ต่อ key — false = ต้องรอ waitSec วินาที</summary>
        public static bool TryReserveManual(string key, int minutes, out int waitSec)
        {
            waitSec = 0;
            lock (_gateLock)
            {
                DateTime last;
                if (_manualGate.TryGetValue(key ?? "", out last))
                {
                    double left = minutes * 60 - (DateTime.Now - last).TotalSeconds;
                    if (left > 0) { waitSec = (int)Math.Ceiling(left); return false; }
                }
                _manualGate[key ?? ""] = DateTime.Now;
                return true;
            }
        }

        // ── งานหลัก ───────────────────────────────────────────────────────────

        internal ReviewFetchResult Run(ReviewSourceRow src, AgodaLightDiagnostics diag)
        {
            string pageUrl = !string.IsNullOrWhiteSpace(src.SourceUrl) ? src.SourceUrl.Trim() : src.Cfg("pageUrl");
            if (string.IsNullOrEmpty(pageUrl))
                throw new ReviewSourceSkippedException("ยังไม่ได้ตั้ง URL หน้าที่พัก Agoda (ช่อง URL ของแหล่ง)");
            if (!IsAgodaUrl(pageUrl))
                throw new ReviewSourceSkippedException("AGODA_LIGHT ใช้ได้เฉพาะ URL บน agoda.com: " + pageUrl);

            decimal scale = ReviewRating.Parse(src.Cfg("ratingScale")) ?? 10m;
            string providerMap = src.Cfg("providerMap", AgodaPageParser.DefaultProviderMap);
            int maxItems = Math.Max(1, Math.Min(50, src.CfgInt("maxItems", 20)));
            bool summaryConfigured = !string.IsNullOrWhiteSpace(src.Cfg("summaryUrl"));
            int budget = MaxRequestsPerRun + (summaryConfigured ? 1 : 0);   // endpoint สรุปคะแนน = opt-in ชัดเจน (+1)
            string locale = src.Cfg("locale", DefaultLocale);
            string acceptLanguage = AcceptLanguageFor(locale);
            var result = new ReviewFetchResult();

            // 1) หน้าที่พัก
            var pageHeaders = new Dictionary<string, string>
            {
                { "User-Agent", BrowserUa },
                { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
                { "Accept-Language", acceptLanguage },
                { "Cache-Control", "no-cache" }
            };
            var page = Call(diag, "page", "GET", pageUrl, null, pageHeaders, budget - 1);
            budget -= page.Requests;
            if (page.Error != null)
                throw new Exception("เชื่อมต่อ Agoda ไม่ได้: " + page.Error + " · " + diag.Compact());
            string block = AgodaPageParser.DetectBlock(page.Status, page.Body);
            if (block != null)
            {
                diag.Requests.Last().Block = block;
                throw new ReviewSourceBlockedException(block,
                    "Agoda กันบอทที่หน้าที่พัก (" + block + ") — ไม่ดึงซ้ำจนถึงรอบถัดไป; ครบ " + BlockDisableAfter
                    + " ครั้งติดกันระบบจะปิดดึงอัตโนมัติ · " + diag.Compact());
            }
            if (page.Status >= 300 && page.Status < 400)
                throw new Exception("หน้าที่พัก redirect เกินงบคำขอ (HTTP " + page.Status + " → " + ReviewHttp.Brief(page.Location, 200)
                    + ") — ตั้ง URL ให้เป็นปลายทางสุดท้าย · " + diag.Compact());
            if (page.Status < 200 || page.Status >= 300)
                throw new Exception("ดึงหน้าที่พักไม่ได้ HTTP " + page.Status + " · " + diag.Compact());

            string html = page.Body ?? "";

            // 1a) คะแนนรวม
            decimal? rating, best;
            int? count;
            if (AgodaPageParser.ParseJsonLdRating(html, diag, out rating, out best, out count))
            {
                result.PlatformRating = rating;
                result.PlatformRatingScale = best;
                result.PlatformReviewCount = count;
            }
            else result.Warnings.Add(diag.JsonLdFound ? "JSON-LD ไม่มี aggregateRating" : "ไม่พบ JSON-LD ในหน้า");

            // 1b) รหัสที่พัก (ตั้งเองใน ApiConfig ได้ เมื่อหาเองผิด/ไม่เจอ)
            string pattern, candidates;
            string foundId = AgodaPageParser.FindPropertyId(html, out pattern, out candidates);
            string cfgId = src.Cfg("propertyId");
            if (!string.IsNullOrEmpty(cfgId) && Regex.IsMatch(cfgId, @"^\d{3,12}$"))
            {
                diag.PropertyId = cfgId;
                diag.PropertyIdPattern = "ApiConfig.propertyId" + (foundId != null && foundId != cfgId ? " (หน้าเว็บพบ " + foundId + ")" : "");
            }
            else
            {
                diag.PropertyId = foundId;
                diag.PropertyIdPattern = pattern;
            }
            diag.PropertyIdCandidates = candidates;

            // 1c) รีวิวที่ฝังมากับหน้า
            string path;
            var items = AgodaPageParser.ExtractReviews(html, pageUrl, scale, providerMap, diag, out path);
            diag.ParsePath = path;

            // 2) endpoint รีวิว (หน้าแรก ล่าสุดก่อน) — เฉพาะเมื่อหน้าไม่มีรีวิวฝังมา
            object apiRoot = null;
            int reviewBudget = budget - (summaryConfigured ? 1 : 0);
            if (items.Count == 0)
            {
                if (!src.CfgBool("apiEnabled", true))
                    result.Warnings.Add("หน้าไม่มีรีวิวฝังมา และปิด endpoint ไว้ (apiEnabled = false)");
                else if (diag.PropertyId == null)
                    result.Warnings.Add("หน้าไม่มีรีวิวฝังมา และหารหัสที่พักไม่เจอ — ตั้ง propertyId ใน ApiConfig");
                else if (reviewBudget <= 0)
                    result.Warnings.Add("ใช้คำขอครบ " + MaxRequestsPerRun + " ครั้งแล้ว (หน้าที่พัก redirect) — ไม่เรียก endpoint รีวิว");
                else
                {
                    items = CallReviewApi(src, diag, result, pageUrl, page, scale, providerMap, acceptLanguage, out apiRoot);
                    budget--;
                }
            }

            // 3) คะแนนรวมสำรอง เมื่อ JSON-LD ไม่มี: (ก) ฟิลด์สรุปในคำตอบ endpoint → (ข) state ในหน้า → (ค) endpoint สรุป (ถ้าตั้ง)
            if (!result.PlatformRating.HasValue)
                ResolveSummaryRating(src, diag, result, html, apiRoot, pageUrl, page, acceptLanguage, budget);

            // ล่าสุดก่อน + หน้าแรกเท่านั้น
            items = items.OrderByDescending(i => i.ReviewDate ?? DateTime.MinValue).Take(maxItems).ToList();
            diag.ReviewsParsed = items.Count;
            foreach (var it in items.Take(3))
                diag.Previews.Add(new AgodaLightDiagnostics.Preview
                {
                    Name = it.ReviewerName, Origin = it.OriginPlatform, ExtId = it.ExternalReviewId,
                    Date = it.ReviewDate.HasValue ? it.ReviewDate.Value.ToString("yyyy-MM-dd") : null,
                    Rating = it.RatingRaw, Scale = it.RatingScale,
                    Title = AgodaPageParser.ClipPreview(it.Title, 120), Text = AgodaPageParser.ClipPreview(it.Text, 220),
                    Language = it.Language, MachineTranslated = it.MachineTranslated == true,
                    TranslatedTitle = AgodaPageParser.ClipPreview(it.TranslatedTitle, 120),
                    TranslatedText = AgodaPageParser.ClipPreview(it.TranslatedText, 220)
                });
            int mt = items.Count(i => i.MachineTranslated == true);
            if (mt > 0)
                result.Warnings.Add(mt + " รีวิวได้มาเฉพาะคำแปลของ Agoda (ไม่มีต้นฉบับในคำตอบ) — เก็บพร้อมธงแปลเครื่อง; ลองตั้ง languageId/locale/sendLocaleHeaders");
            if (items.Count == 0) result.Warnings.Add("ไม่พบรีวิวในหน้า/endpoint (ได้เฉพาะคะแนนรวม)");
            if (diag.UnknownProviders.Count > 0)
                result.Warnings.Add("providerId ไม่รู้จัก " + string.Join(",", diag.UnknownProviders.Take(5)) + " (ถือเป็น Agoda — ตั้ง providerMap)");

            diag.Warnings.AddRange(result.Warnings);
            result.Items = items;
            result.Note = diag.Compact();
            return result;
        }

        private List<ReviewItem> CallReviewApi(ReviewSourceRow src, AgodaLightDiagnostics diag, ReviewFetchResult result,
            string pageUrl, CallResult page, decimal scale, string providerMap, string acceptLanguage, out object apiRoot)
        {
            apiRoot = null;
            string hotelId = diag.PropertyId;
            string apiUrl = Fill(src, src.Cfg("apiUrl", DefaultApiUrl) ?? "", hotelId, true);
            string method = (src.Cfg("apiMethod", DefaultApiMethod) ?? "POST").Trim().ToUpperInvariant();
            if (method != "GET" && method != "POST") method = "POST";
            if (!IsAgodaUrl(apiUrl))
            {
                result.Warnings.Add("apiUrl ต้องอยู่บน agoda.com — ไม่เรียก endpoint");
                return new List<ReviewItem>();
            }

            var headers = new Dictionary<string, string>
            {
                { "User-Agent", BrowserUa },
                { "Accept", "application/json, text/plain, */*" },
                { "Accept-Language", acceptLanguage },
                { "Referer", pageUrl },
                { "Origin", "https://www.agoda.com" }
            };
            if (src.CfgBool("forwardCookies", true))
            {
                string cookie = CookieHeader(page.SetCookies);
                if (!string.IsNullOrEmpty(cookie)) headers["Cookie"] = cookie;
            }
            AddLocaleHeaders(src, headers);
            foreach (var kv in ParseHeaders(src.Cfg("apiHeaders"))) headers[kv.Key] = kv.Value;

            string body = null;
            if (method == "POST")
                body = Fill(src, src.Cfg("apiBody", DefaultApiBody) ?? DefaultApiBody, hotelId, false);

            var resp = Call(diag, "api", method, apiUrl, body, headers, 0);
            var info = diag.Requests.Last();
            if (resp.Error != null)
            {
                result.Warnings.Add("เรียก endpoint รีวิวไม่สำเร็จ: " + resp.Error);
                return new List<ReviewItem>();
            }
            string block = AgodaPageParser.DetectBlock(resp.Status, resp.Body, true);
            if (block != null)
            {
                info.Block = block;
                throw new ReviewSourceBlockedException(block,
                    "Agoda กันบอทที่ endpoint รีวิว (" + block + ") — ไม่ดึงซ้ำจนถึงรอบถัดไป; ครบ " + BlockDisableAfter
                    + " ครั้งติดกันระบบจะปิดดึงอัตโนมัติ (ถ้าเป็นเพราะค่า endpoint ไม่ตรง ให้ตั้ง apiEnabled = false) · " + diag.Compact());
            }
            if (resp.Status < 200 || resp.Status >= 300)
            {
                result.Warnings.Add("endpoint รีวิวตอบ HTTP " + resp.Status + " — ค่าเริ่มของ endpoint ยังไม่ยืนยัน ตรวจ apiUrl/apiMethod/apiBody ด้วยปุ่มทดสอบ");
                diag.ParsePath = "endpoint:http-" + resp.Status;
                return new List<ReviewItem>();
            }
            string b = (resp.Body ?? "").TrimStart();
            if (b.StartsWith("<"))
            {
                result.Warnings.Add("endpoint รีวิวตอบเป็น HTML ไม่ใช่ JSON — ตรวจ apiUrl/apiMethod");
                diag.ParsePath = "endpoint:html";
                return new List<ReviewItem>();
            }
            var items = AgodaPageParser.ExtractReviewsFromJson(resp.Body, pageUrl, scale, providerMap, diag, out apiRoot);
            diag.ParsePath = items.Count > 0 ? "endpoint" : "endpoint:no-reviews";
            return items;
        }

        // ── คะแนนรวมสำรอง ─────────────────────────────────────────────────────

        private void ResolveSummaryRating(ReviewSourceRow src, AgodaLightDiagnostics diag, ReviewFetchResult result, string html,
            object apiRoot, string pageUrl, CallResult page, string acceptLanguage, int budget)
        {
            decimal? r; int? c; string path;
            // (ก) คำตอบ endpoint รีวิว — เฉพาะ object ที่ไม่ใช่รีวิว (ไม่เฉลี่ยรีวิวรายตัว)
            if (apiRoot != null)
            {
                var cands = new List<AgodaPageParser.SummaryCandidate>();
                AgodaPageParser.CollectSummary(apiRoot, "", 0, cands, false);
                diag.RatingCandidates.AddRange(cands.Take(30).Select(x => "api:" + x));
                if (AgodaPageParser.PickSummary(cands, out r, out c, out path)) { SetRating(diag, result, r, c, "api-summary:" + path); return; }
            }
            // (ข) state/สคริปต์ในหน้า — นับเฉพาะค่าใกล้รหัสที่พักของเรา (priority ≥ 3)
            var hc = AgodaPageParser.CollectSummaryFromHtml(html, diag.PropertyId);
            diag.RatingCandidates.AddRange(hc.OrderByDescending(x => x.Priority).Take(20).Select(x => x.ToString()));
            if (AgodaPageParser.PickSummary(hc.Where(x => x.Priority >= 3).ToList(), out r, out c, out path))
            { SetRating(diag, result, r, c, "page-state:" + path); return; }

            // (ค) endpoint สรุปคะแนน (ปิดเป็นค่าเริ่ม)
            string sumUrlT = src.Cfg("summaryUrl");
            if (string.IsNullOrWhiteSpace(sumUrlT)) { result.Warnings.Add("หาคะแนนรวมไม่ได้อย่างมั่นใจ — ดูค่าที่พบในผลทดสอบ (ไม่ตั้งค่าเดา)"); return; }
            if (budget <= 0 || diag.PropertyId == null) { result.Warnings.Add("ไม่เรียก endpoint สรุปคะแนน (งบคำขอหมด / ไม่มีรหัสที่พัก)"); return; }
            string url = Fill(src, sumUrlT, diag.PropertyId, true);
            if (!IsAgodaUrl(url)) { result.Warnings.Add("summaryUrl ต้องอยู่บน agoda.com"); return; }
            string method = (src.Cfg("summaryMethod", "GET") ?? "GET").Trim().ToUpperInvariant() == "POST" ? "POST" : "GET";
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", BrowserUa }, { "Accept", "application/json, text/plain, */*" },
                { "Accept-Language", acceptLanguage }, { "Referer", pageUrl }, { "Origin", "https://www.agoda.com" }
            };
            if (src.CfgBool("forwardCookies", true))
            {
                string cookie = CookieHeader(page.SetCookies);
                if (!string.IsNullOrEmpty(cookie)) headers["Cookie"] = cookie;
            }
            AddLocaleHeaders(src, headers);
            foreach (var kv in ParseHeaders(src.Cfg("apiHeaders"))) headers[kv.Key] = kv.Value;
            string body = method == "POST" ? Fill(src, src.Cfg("summaryBody", "{\"hotelId\":{hotelId}}"), diag.PropertyId, false) : null;
            var resp = Call(diag, "summary", method, url, body, headers, 0);
            string block = resp.Error == null ? AgodaPageParser.DetectBlock(resp.Status, resp.Body, true) : null;
            if (block != null) diag.Requests.Last().Block = block;
            // endpoint เสริม: ล้มเหลว/โดนกัน = คำเตือนเท่านั้น (ไม่นับลำดับปิดอัตโนมัติ — อาจเป็นค่าที่ตั้งผิด)
            if (resp.Error != null || block != null || resp.Status < 200 || resp.Status >= 300)
            {
                result.Warnings.Add("endpoint สรุปคะแนนใช้ไม่ได้: " + (resp.Error ?? block ?? ("HTTP " + resp.Status)));
                return;
            }
            object root;
            AgodaPageParser.ExtractReviewsFromJson(resp.Body, pageUrl, 10m, null, null, out root);
            if (root == null) { result.Warnings.Add("endpoint สรุปคะแนนไม่ได้ตอบ JSON"); return; }
            var sc = new List<AgodaPageParser.SummaryCandidate>();
            AgodaPageParser.CollectSummary(root, "", 0, sc, false);
            diag.RatingCandidates.AddRange(sc.Take(30).Select(x => "summary:" + x));
            if (AgodaPageParser.PickSummary(sc, out r, out c, out path)) SetRating(diag, result, r, c, "summary-endpoint:" + path);
            else result.Warnings.Add("endpoint สรุปคะแนน: ไม่พบค่าที่มั่นใจ — ดูรายการค่าที่พบ");
        }

        private static void SetRating(AgodaLightDiagnostics diag, ReviewFetchResult result, decimal? r, int? c, string source)
        {
            result.PlatformRating = r;
            result.PlatformRatingScale = 10m;
            result.PlatformReviewCount = c;
            diag.Rating = r; diag.RatingBest = 10m; diag.RatingCount = c; diag.RatingSource = source;
            result.Warnings.RemoveAll(w => w.StartsWith("JSON-LD ไม่มี aggregateRating", StringComparison.Ordinal)
                                         || w.StartsWith("ไม่พบ JSON-LD", StringComparison.Ordinal));
        }

        // ── ภาษา/locale ───────────────────────────────────────────────────────

        /// <summary>แทน {hotelId} {languageId} {locale}; languageId = "off" ⇒ ตัดฟิลด์ "languageId":{languageId} ออกจาก body</summary>
        internal static string Fill(ReviewSourceRow src, string template, string hotelId, bool isUrl)
        {
            string t = template ?? "";
            string langId = src.Cfg("languageId", DefaultLanguageId);
            string locale = src.Cfg("locale", DefaultLocale);
            bool off = string.Equals(langId, "off", StringComparison.OrdinalIgnoreCase) || !Regex.IsMatch(langId ?? "", @"^\d{1,4}$");
            if (off)
            {
                t = Regex.Replace(t, @"""languageId""\s*:\s*\{languageId\}\s*,?", "");
                langId = "";
            }
            if (!Regex.IsMatch(locale ?? "", @"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,4})?$")) locale = DefaultLocale;
            Func<string, string> enc = v => isUrl ? Uri.EscapeDataString(v ?? "") : (v ?? "");
            return t.Replace("{hotelId}", enc(hotelId)).Replace("{languageId}", enc(langId)).Replace("{locale}", enc(locale));
        }

        private static void AddLocaleHeaders(ReviewSourceRow src, Dictionary<string, string> headers)
        {
            if (!src.CfgBool("sendLocaleHeaders", false)) return;
            string locale = src.Cfg("locale", DefaultLocale);
            string langId = src.Cfg("languageId", DefaultLanguageId);
            if (Regex.IsMatch(locale ?? "", @"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,4})?$")) headers["ag-language-locale"] = locale.ToLowerInvariant();
            if (Regex.IsMatch(langId ?? "", @"^\d{1,4}$")) headers["ag-language-id"] = langId;
        }

        /// <summary>"th-th" → "th-TH,th;q=0.9,en-US;q=0.6,en;q=0.5"</summary>
        public static string AcceptLanguageFor(string locale)
        {
            var m = Regex.Match(locale ?? "", @"^([A-Za-z]{2,3})(?:-([A-Za-z0-9]{2,4}))?$");
            if (!m.Success) return AcceptLanguage;
            string lang = m.Groups[1].Value.ToLowerInvariant();
            string full = m.Groups[2].Success ? lang + "-" + m.Groups[2].Value.ToUpperInvariant() : lang;
            return lang == "en" ? full + ",en;q=0.9" : full + "," + lang + ";q=0.9,en-US;q=0.6,en;q=0.5";
        }

        // ── HTTP + บันทึกผลต่อคำขอ ────────────────────────────────────────────

        private class CallResult
        {
            public int Status;
            public string Body;
            public string Location;
            public string[] SetCookies;
            public int Requests = 1;
            public string Error;
        }

        private static CallResult Call(AgodaLightDiagnostics diag, string label, string method, string url, string body,
            Dictionary<string, string> headers, int maxRedirects)
        {
            var info = new AgodaLightDiagnostics.RequestInfo { Label = label, Method = method, Url = StripQuery(url) };
            diag.Requests.Add(info);
            var sw = Stopwatch.StartNew();
            var cr = new CallResult();
            try
            {
                ReviewHttp.Response r = method == "GET"
                    ? ReviewHttp.GetPublic(url, headers, TimeoutMs, Math.Max(0, maxRedirects))
                    : ReviewHttp.SendPublic(method, url, body, "application/json", headers, TimeoutMs);
                cr.Status = r.Status; cr.Body = r.Body; cr.Location = r.Location; cr.SetCookies = r.SetCookies; cr.Requests = Math.Max(1, r.Requests);
            }
            catch (ReviewSourceSkippedException) { throw; }   // URL ผิดนโยบาย SSRF — ไม่ใช่ความล้มเหลวของ Agoda
            catch (Exception ex) { cr.Error = ReviewHttp.Brief(ex.Message, 200); }
            sw.Stop();
            info.Status = cr.Status;
            info.Bytes = cr.Body == null ? 0 : Encoding.UTF8.GetByteCount(cr.Body);
            info.Ms = sw.ElapsedMilliseconds;
            info.Hops = cr.Requests;
            info.Error = cr.Error;
            return cr;
        }

        public static bool IsAgodaUrl(string url)
        {
            Uri u;
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out u)) return false;
            if (u.Scheme != Uri.UriSchemeHttps) return false;
            string h = u.Host.ToLowerInvariant();
            return (h == "agoda.com" || h.EndsWith(".agoda.com")) && ReviewHttp.IsAllowedPublicUrl(url.Trim());
        }

        private static string StripQuery(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            int q = url.IndexOf('?');
            return q < 0 ? url : url.Substring(0, q) + "?…";
        }

        private static readonly Regex CookieToken = new Regex(@"^[A-Za-z0-9!#$%&'*+.^_`|~-]{1,100}=[^;,\s]{0,1000}$", RegexOptions.Compiled);

        /// <summary>Set-Cookie ของหน้าที่พัก → Cookie header (name=value เท่านั้น, สูงสุด 30 ตัว / 6000 ตัวอักษร)</summary>
        public static string CookieHeader(string[] setCookies)
        {
            if (setCookies == null || setCookies.Length == 0) return null;
            var parts = new List<string>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            int len = 0;
            foreach (var sc in setCookies)
            {
                if (string.IsNullOrEmpty(sc)) continue;
                string first = sc.Split(';')[0].Trim();
                if (!CookieToken.IsMatch(first)) continue;
                string name = first.Substring(0, first.IndexOf('='));
                if (!names.Add(name)) continue;
                if (parts.Count >= 30 || len + first.Length > 6000) break;
                parts.Add(first);
                len += first.Length + 2;
            }
            return parts.Count == 0 ? null : string.Join("; ", parts);
        }

        private static readonly HashSet<string> ForbiddenHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Host", "Content-Length", "Connection", "Transfer-Encoding", "Expect", "Proxy-Connection", "Content-Type", "Date", "Range", "If-Modified-Since" };

        /// <summary>apiHeaders: "Name: value | Name2: value" (หรือขึ้นบรรทัดใหม่)</summary>
        public static List<KeyValuePair<string, string>> ParseHeaders(string s)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(s)) return list;
            foreach (var part in s.Split(new[] { '|', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int c = part.IndexOf(':');
                if (c <= 0) continue;
                string k = part.Substring(0, c).Trim(), v = part.Substring(c + 1).Trim();
                if (!Regex.IsMatch(k, @"^[A-Za-z0-9-]{1,60}$") || ForbiddenHeaders.Contains(k)) continue;
                if (v.IndexOfAny(new[] { '\r', '\n' }) >= 0 || v.Length > 2000) continue;
                list.Add(new KeyValuePair<string, string>(k, v));
                if (list.Count >= 10) break;
            }
            return list;
        }
    }
}
