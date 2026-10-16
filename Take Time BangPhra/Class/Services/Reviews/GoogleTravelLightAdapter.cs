// ===========================================================================
// GoogleTravelLightAdapter.cs — GOOGLE_TRAVEL_LIGHT: อ่านหน้ารีวิวโรงแรมบน Google Travel วันละครั้ง (เจ้าของขอเอง)
//
// ⚠ ทางที่ถูกข้อกำหนดและแนะนำคือ GBP_API (Google Business Profile API) — ข้อกำหนดของ Google ไม่อนุญาตการดึงหน้าเว็บอัตโนมัติ
// นโยบาย: ต่อรอบ GET เดียว (ไม่ตาม redirect, ไม่ retry, ไม่เลื่อนโหลดเพิ่ม) — ได้เฉพาะชุดแรกที่เซิร์ฟเวอร์ส่งมา
//   (ค่าเริ่มของ Google เรียง "เกี่ยวข้องที่สุด" ไม่ใช่ล่าสุด ⇒ ผลทดสอบบอกว่ากี่รายการเป็นรีวิวใหม่จริง)
//   • URL ต้องเป็น https://www.google.com/travel/… เท่านั้น — ตัดพารามิเตอร์ติดตามออก คง hl/gl (+ extraQuery ถ้าตั้ง)
//   • โดนกัน (429/403/503, /sorry/, consent, unusual traffic, captcha) หรือหน้าไม่มีกล่องรีวิว ⇒ ReviewSourceBlockedException
//     ⇒ ไม่ retry ถี่ และครบ 3 ครั้งติดปิดดึงอัตโนมัติ + แจ้งเตือนครั้งเดียว (ReviewAggregationService)
//   • รอบขั้นต่ำ 720 นาที (ค่าเริ่ม 1440), เปิดอัตโนมัติได้เฉพาะ Owner/Admin
//   • เพิ่มเฉพาะรีวิวใหม่ (postId → Content_Hash ที่ไม่รวมวันที่โดยประมาณ); คำตอบเจ้าของอัปเดตได้เมื่อมี/เปลี่ยน
// ApiConfig: sel* (selector ทุกตัว — ดู GoogleTravelSelectors), extraQuery (เช่นพารามิเตอร์เรียงล่าสุด — ยังไม่ยืนยัน, ค่าเริ่มว่าง),
//   headers ("Name: value | …"), hl (th-TH), gl (th)
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Services.Reviews
{
    public class GoogleTravelLightAdapter : IReviewSourceAdapter
    {
        public const string DefaultUrl = "https://www.google.com/travel/hotels/entity/CgsIsoLIgrCxpI6cARAB/reviews?hl=th-TH&gl=th";
        private const string BrowserUa = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
        private const int TimeoutMs = 25000;

        private readonly string _conn;
        public GoogleTravelLightAdapter(string connectionString = null) { _conn = connectionString; }

        public string Key { get { return IngestMethods.GoogleTravelLight; } }
        public bool SupportsAutomatic { get { return true; } }

        public ReviewFetchResult Fetch(ReviewSourceRow src)
        {
            return Run(src, NewDiag());
        }

        private static LightFetchDiagnostics NewDiag()
        {
            return new LightFetchDiagnostics { Method = IngestMethods.GoogleTravelLight };
        }

        /// <summary>ปุ่ม "ทดสอบดึง (ไม่บันทึก)" — ไม่บันทึก/ไม่นับล้มเหลว; นับว่ากี่รายการยังไม่มีในฐานข้อมูล</summary>
        public LightFetchDiagnostics DryRun(ReviewSourceRow src)
        {
            var diag = NewDiag();
            diag.DryRun = true;
            try
            {
                var r = Run(src, diag);
                diag.NewCount = CountNew(r.Items);
                if (diag.NewCount.HasValue) diag.Warnings.Add("รีวิวใหม่ (ยังไม่มีในระบบ) " + diag.NewCount.Value + " จาก " + r.Items.Count
                    + " — หน้าเรียง 'เกี่ยวข้องที่สุด' ไม่ใช่ล่าสุด");
            }
            catch (ReviewSourceBlockedException bx) { diag.Blocked = true; diag.Error = bx.Message; }
            catch (Exception ex) { diag.Error = ex.Message; }
            return diag;
        }

        internal ReviewFetchResult Run(ReviewSourceRow src, LightFetchDiagnostics diag)
        {
            string raw = !string.IsNullOrWhiteSpace(src.SourceUrl) ? src.SourceUrl.Trim() : src.Cfg("pageUrl");
            if (string.IsNullOrEmpty(raw)) throw new ReviewSourceSkippedException("ยังไม่ได้ตั้ง URL หน้ารีวิว Google Travel (ช่อง URL ของแหล่ง)");
            if (!IsGoogleTravelUrl(raw))
                throw new ReviewSourceSkippedException("GOOGLE_TRAVEL_LIGHT ใช้ได้เฉพาะ https://www.google.com/travel/… : " + raw);
            string url = NormalizeUrl(raw, src.Cfg("hl", "th-TH"), src.Cfg("gl", "th"), src.Cfg("extraQuery"));
            diag.EntityToken = GoogleTravelParser.EntityTokenFromUrl(url);

            var headers = new Dictionary<string, string>
            {
                { "User-Agent", BrowserUa },
                { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
                { "Accept-Language", "th-TH,th;q=0.9,en-US;q=0.6,en;q=0.5" },
                { "Cache-Control", "no-cache" }
            };
            foreach (var kv in AgodaLightAdapter.ParseHeaders(src.Cfg("headers"))) headers[kv.Key] = kv.Value;

            // GET เดียว — ไม่ตาม redirect (redirect ไป /sorry/ หรือ consent = โดนกัน)
            var info = new LightFetchDiagnostics.RequestInfo { Label = "page", Method = "GET", Url = url };
            diag.Requests.Add(info);
            var sw = Stopwatch.StartNew();
            ReviewHttp.Response resp = null;
            string netErr = null;
            try { resp = ReviewHttp.GetPublic(url, headers, TimeoutMs, 0); }
            catch (ReviewSourceSkippedException) { throw; }
            catch (Exception ex) { netErr = ReviewHttp.Brief(ex.Message, 200); }
            sw.Stop();
            info.Ms = sw.ElapsedMilliseconds;
            info.Error = netErr;
            if (netErr != null) throw new Exception("เชื่อมต่อ Google ไม่ได้: " + netErr + " · " + diag.Compact());
            info.Status = resp.Status;
            info.Bytes = resp.Body == null ? 0 : Encoding.UTF8.GetByteCount(resp.Body);

            string block = GoogleTravelParser.DetectBlock(resp.Status, resp.Body, resp.Location);
            if (block != null)
            {
                info.Block = block;
                throw new ReviewSourceBlockedException(block, "Google กันการดึงหน้า (" + block + ") — ไม่ดึงซ้ำจนถึงรอบถัดไป; ครบ "
                    + LightFetch.BlockDisableAfter + " ครั้งติดกันระบบจะปิดดึงอัตโนมัติ · " + diag.Compact());
            }
            if (resp.Status >= 300 && resp.Status < 400)
                throw new Exception("Google redirect (HTTP " + resp.Status + " → " + ReviewHttp.Brief(resp.Location, 200)
                    + ") — ตั้ง URL ให้เป็นปลายทางสุดท้าย · " + diag.Compact());
            if (!resp.Ok) throw new Exception("ดึงหน้ารีวิวไม่ได้ HTTP " + resp.Status + " · " + diag.Compact());

            string html = resp.Body ?? "";
            GoogleTravelParser.FindIds(html, diag);

            var sel = GoogleTravelSelectors.From(k => src.Cfg(k));
            string path;
            int firstBlock;
            var items = GoogleTravelParser.ParseReviews(html, url, DateTime.Now, sel, diag, out path, out firstBlock);
            diag.ParsePath = path;

            var result = new ReviewFetchResult();
            decimal? rating; int? count;
            if (GoogleTravelParser.FindOverallRating(html, firstBlock, diag, out rating, out count))
            {
                result.PlatformRating = rating;
                result.PlatformRatingScale = 5m;
                result.PlatformReviewCount = count;
            }
            else result.Warnings.Add("หาคะแนนรวมของหน้าไม่ได้อย่างมั่นใจ (ไม่ตั้งค่าเดา)");

            if (items.Count == 0)
            {
                // หน้าโหลดได้แต่ไม่มีกล่องรีวิว = Google ส่งหน้าอื่น/หน้ากัน หรือ selector เปลี่ยน — นับเป็นการโดนกัน (กันวนดึงเปล่า)
                info.Block = "no-review-container";
                throw new ReviewSourceBlockedException("no-review-container",
                    "หน้า Google Travel ไม่มีกล่องรีวิวใน HTML ที่เซิร์ฟเวอร์ส่งมา (บล็อกที่เห็น " + diag.HtmlDomBlocks
                    + ") — อาจถูกกัน หรือ selector เปลี่ยน (ตั้ง sel* ใน ApiConfig) · " + diag.Compact());
            }

            items = items.Take(Math.Max(1, Math.Min(100, src.CfgInt("maxItems", 50)))).ToList();
            diag.ReviewsParsed = items.Count;
            foreach (var it in items.Take(3))
                diag.Previews.Add(new LightFetchDiagnostics.Preview
                {
                    Name = it.ReviewerName, Origin = it.OriginPlatform, ExtId = it.ExternalReviewId == null ? null : AgodaPageParser.ClipPreview(it.ExternalReviewId, 40),
                    Date = it.ReviewDate.HasValue ? it.ReviewDate.Value.ToString("yyyy-MM-dd") + " (โดยประมาณ)" : null,
                    RelativeDate = MetaStr(it, "relativeDate"),
                    Rating = it.RatingRaw, Scale = it.RatingScale, Language = it.Language, MachineTranslated = it.MachineTranslated == true,
                    Text = AgodaPageParser.ClipPreview(it.Text, 220), TripType = it.TravellerType,
                    SubRatings = MetaPairs(it, "subRatings"), Highlights = MetaList(it, "highlights"),
                    OwnerResponse = AgodaPageParser.ClipPreview(it.OwnerResponse, 160),
                    OwnerResponseDate = MetaStr(it, "ownerResponseRelative")
                });
            int noId = items.Count(i => i.ExternalReviewId == null);
            if (noId > 0) result.Warnings.Add(noId + " รีวิวไม่มี postId (กันซ้ำด้วยแฮชข้อความแทน)");
            diag.Warnings.AddRange(result.Warnings);
            result.Items = items;
            result.Note = diag.Compact();
            return result;
        }

        /// <summary>dry-run: จำนวนรีวิวที่ยังไม่มีในฐานข้อมูล (ตาม External_Review_Id) — ไม่มี conn / ผิดพลาด = null</summary>
        private int? CountNew(List<ReviewItem> items)
        {
            if (string.IsNullOrEmpty(_conn) || items == null) return null;
            var ids = items.Where(i => i.ExternalReviewId != null).Select(i => i.ExternalReviewId).Distinct().Take(100).ToList();
            int noId = items.Count(i => i.ExternalReviewId == null);
            if (ids.Count == 0) return null;
            try
            {
                var ps = new Dictionary<string, object>();
                var names = new List<string>();
                for (int i = 0; i < ids.Count; i++) { ps["@p" + i] = ids[i]; names.Add("@p" + i); }
                var dt = new code().DatabaseQuerySafe(_conn,
                    "SELECT External_Review_Id FROM AI_Online_Reviews WHERE External_Review_Id IN (" + string.Join(",", names) + ")", ps);
                var have = new HashSet<string>(dt.Rows.Cast<DataRow>().Select(r => r[0].ToString()), StringComparer.Ordinal);
                return ids.Count(i => !have.Contains(i)) + noId;
            }
            catch { return null; }
        }

        public static bool IsGoogleTravelUrl(string url)
        {
            Uri u;
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out u)) return false;
            return ReviewHttp.IsAllowedGoogleTravelUrl(u.AbsoluteUri);
        }

        /// <summary>ตัดพารามิเตอร์ติดตาม (ved, ts, utm_*, …) — คง hl/gl (เติมค่าเริ่มถ้าไม่มี) + extraQuery</summary>
        public static string NormalizeUrl(string url, string hl, string gl, string extraQuery)
        {
            var u = new Uri(url.Trim());
            var keep = new List<string>();
            string q = u.Query.TrimStart('?');
            bool hasHl = false, hasGl = false;
            foreach (var part in q.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string k = part.Split('=')[0].ToLowerInvariant();
                if (k == "hl") { hasHl = true; keep.Add(part); }
                else if (k == "gl") { hasGl = true; keep.Add(part); }
            }
            if (!hasHl && !string.IsNullOrWhiteSpace(hl)) keep.Add("hl=" + Uri.EscapeDataString(hl.Trim()));
            if (!hasGl && !string.IsNullOrWhiteSpace(gl)) keep.Add("gl=" + Uri.EscapeDataString(gl.Trim()));
            if (!string.IsNullOrWhiteSpace(extraQuery))
                foreach (var part in extraQuery.Trim().TrimStart('?', '&').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
                    if (Regex.IsMatch(part, @"^[A-Za-z0-9_\-]{1,30}=[A-Za-z0-9_\-.%]{0,100}$")) keep.Add(part);
            return u.GetLeftPart(UriPartial.Path) + (keep.Count > 0 ? "?" + string.Join("&", keep) : "");
        }

        private static string MetaStr(ReviewItem it, string key)
        {
            object v;
            return it.Meta != null && it.Meta.TryGetValue(key, out v) && v != null ? v.ToString() : null;
        }

        private static string MetaPairs(ReviewItem it, string key)
        {
            object v;
            if (it.Meta == null || !it.Meta.TryGetValue(key, out v)) return null;
            var d = v as Dictionary<string, object>;
            return d == null ? null : string.Join(", ", d.Select(kv => kv.Key + " " + kv.Value));
        }

        private static string MetaList(ReviewItem it, string key)
        {
            object v;
            if (it.Meta == null || !it.Meta.TryGetValue(key, out v)) return null;
            var l = v as List<string>;
            return l == null ? null : string.Join(" · ", l);
        }
    }
}
