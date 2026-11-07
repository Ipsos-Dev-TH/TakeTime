// ===========================================================================
// AgodaPageParser.cs — ตัวแกะหน้าที่พัก Agoda แบบ "อ่านเฉพาะที่หน้าแสดง" (AGODA_LIGHT) — pure ไม่มี HTTP/DB
//
// แยกจาก AgodaLightAdapter เพื่อทดสอบแบบ offline ได้ด้วยตัวอย่าง HTML/JSON สังเคราะห์
//   • DetectBlock        : หน้า bot wall / captcha / Access Denied / body ว่าง
//   • ParseJsonLdRating  : schema.org JSON-LD → aggregateRating (ratingValue, bestRating ค่าเริ่ม 10, reviewCount)
//   • FindPropertyId     : รหัสที่พักจาก HTML — นับทุกรูปแบบแล้วเลือกตัวที่พบบ่อยสุด
//                          (หน้า Agoda มี "ที่พักใกล้เคียง" ที่มี hotelId ของคนอื่นปนอยู่)
//   • ParseReviewDom     : DOM รายการรีวิวที่ render แล้ว (data-element-name="review-comment" / data-review-id /
//                          data-info-type / data-testid) — ตัวอย่างจริงจากเบราว์เซอร์ของเจ้าของ (ต.ค. 2026); ลองก่อนทางอื่นเสมอ
//   • ExtractReviews     : หา object ที่หน้าตาเป็นรีวิว (มีคะแนน/ชื่อผู้รีวิว + ข้อความ + คีย์เฉพาะรีวิว)
//                          จาก JSON ใน <script> / ld+json / JSON.parse("...") / สแกนรอบคีย์ยึด (anchor) แบบทนรูปแบบ
//   • ExtractReviewsFromJson : ใช้กับคำตอบของ endpoint รีวิว (รูปแบบไม่แน่นอน — เดินทั้งต้นไม้ JSON)
//
// สมมติฐานรูปแบบข้อมูล (ยังไม่ได้ยืนยันกับหน้า Agoda จริง — sandbox เข้า agoda.com ไม่ได้):
//   รีวิว Agoda แบบที่เคยเห็นในหน้า/API สาธารณะ: { hotelReviewId, providerId, rating (0–10), reviewTitle,
//   reviewComments, reviewPositives, reviewNegatives, reviewDate (ISO) / formattedReviewDate (ข้อความตาม locale),
//   reviewerInfo { displayMemberName, reviewGroupName, roomTypeName }, reviewProviderText / providerName }
//   — ตัวแกะไม่ผูกกับโครงสร้างชั้นนอก (comments / commentList / reviews ...) และรองรับ schema.org Review ใน JSON-LD
// PDPA: เก็บเฉพาะชื่อที่แสดง (ไม่เก็บประเทศ/รูป/ลิงก์โปรไฟล์)
// ===========================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Services.Reviews
{
    public static class AgodaPageParser
    {
        /// <summary>ค่าเริ่ม: providerId 332 = Agoda (ยังไม่ยืนยัน — แก้ได้ที่ ApiConfig providerMap เช่น "332:Agoda,3038:Booking.com")</summary>
        public const string DefaultProviderMap = "332:Agoda";
        private const int MaxSpan = 4 * 1024 * 1024;
        private const int MaxAnchorTries = 40;

        private static JavaScriptSerializer Json()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
        }

        // ── 1) ตรวจโดนกันบอท ──────────────────────────────────────────────────

        private static readonly string[] StrongMarkers =
        {
            "px-captcha", "_pxCaptcha", "captcha-delivery.com", "geo.captcha-delivery", "errors.edgesuite.net",
            "Pardon Our Interruption", "Incapsula incident", "Attention Required! | Cloudflare", "cf-chl-",
            "/cdn-cgi/challenge-platform", "Please verify you are a human", "Access to this page has been denied"
        };

        private static readonly string[] WeakMarkers = { "captcha", "Access Denied", "akamai", "Request blocked", "bot detection" };

        /// <summary>คืนชื่อตัวบ่งชี้ (เช่น "HTTP 403", "px-captcha", "empty-body") หรือ null ถ้าไม่ใช่หน้ากันบอท</summary>
        /// <param name="isApi">คำตอบของ endpoint JSON — JSON สั้น ๆ ไม่ใช่ "body ว่าง" และตัวบ่งชี้อ่อนนับเฉพาะเมื่อได้ HTML กลับมา</param>
        public static string DetectBlock(int status, string body, bool isApi = false)
        {
            if (status == 403 || status == 429 || status == 503) return "HTTP " + status;
            string b = body ?? "";
            if (isApi)
            {
                if (b.Trim().Length == 0) return status >= 200 && status < 300 ? "empty-body" : null;
                foreach (var m in StrongMarkers)
                    if (b.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0) return m;
                if (!b.TrimStart().StartsWith("<")) return null;
                foreach (var m in WeakMarkers)
                    if (b.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0) return m;
                return null;
            }
            if (b.Trim().Length < 200) return "empty-body";
            foreach (var m in StrongMarkers)
                if (b.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0) return m;
            // ตัวบ่งชี้อ่อน (คำว่า captcha / akamai อาจอยู่ในหน้าปกติขนาดใหญ่ เช่น CDN/reCAPTCHA ของฟอร์มล็อกอิน)
            // ⇒ นับเฉพาะหน้าเล็ก หรือหน้าที่ไม่มีร่องรอยของหน้าที่พักเลย
            bool looksLikePropertyPage = b.IndexOf("application/ld+json", StringComparison.OrdinalIgnoreCase) >= 0
                                      || b.IndexOf("hotelId", StringComparison.OrdinalIgnoreCase) >= 0
                                      || b.IndexOf("propertyId", StringComparison.OrdinalIgnoreCase) >= 0;
            if (b.Length < 60000 || !looksLikePropertyPage)
                foreach (var m in WeakMarkers)
                    if (b.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0) return m;
            return null;
        }

        // ── 2) JSON-LD aggregateRating ────────────────────────────────────────

        private static readonly Regex ScriptRx = new Regex(@"<script\b([^>]*)>(.*?)</script\s*>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static List<string> JsonLdBlocks(string html)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(html)) return list;
            foreach (Match m in ScriptRx.Matches(html))
            {
                string attrs = m.Groups[1].Value;
                if (Regex.IsMatch(attrs, @"type\s*=\s*[""']?application/ld\+json", RegexOptions.IgnoreCase))
                    list.Add(m.Groups[2].Value.Trim());
            }
            return list;
        }

        /// <summary>อ่าน aggregateRating จาก JSON-LD — bestRating ไม่ระบุ = 10 (สเกล Agoda)</summary>
        public static bool ParseJsonLdRating(string html, LightFetchDiagnostics diag, out decimal? rating, out decimal? best, out int? count,
            decimal defaultBest = 10m)
        {
            rating = null; best = null; count = null;
            var blocks = JsonLdBlocks(html);
            if (diag != null) { diag.JsonLdBlocks = blocks.Count; diag.JsonLdFound = blocks.Count > 0; }
            foreach (var raw in blocks)
            {
                object root = TryDeserialize(raw);
                if (root == null) root = TryDeserialize(WebUtility.HtmlDecode(raw));
                if (root == null) continue;
                var agg = FindAggregate(root, 0);
                if (agg == null) continue;
                decimal? rv = ReviewRating.Parse(Get(agg, "ratingValue"));
                decimal? bv = ReviewRating.Parse(Get(agg, "bestRating")) ?? defaultBest;
                if (!ReviewRating.IsValid(rv, bv)) continue;
                rating = rv; best = bv;
                decimal? c = ReviewRating.Parse(Get(agg, "reviewCount")) ?? ReviewRating.Parse(Get(agg, "ratingCount"));
                if (c.HasValue && c.Value < int.MaxValue) count = (int)c.Value;
                if (diag != null) { diag.Rating = rating; diag.RatingBest = best; diag.RatingCount = count; diag.RatingSource = "json-ld"; }
                return true;
            }
            return false;
        }

        private static Dictionary<string, object> FindAggregate(object node, int depth)
        {
            if (node == null || depth > 40) return null;
            var d = node as Dictionary<string, object>;
            if (d != null)
            {
                object a;
                if (d.TryGetValue("aggregateRating", out a) && a is Dictionary<string, object>) return (Dictionary<string, object>)a;
                if (string.Equals(Str(Get(d, "@type")), "AggregateRating", StringComparison.OrdinalIgnoreCase)) return d;
                foreach (var v in d.Values)
                {
                    var r = FindAggregate(v, depth + 1);
                    if (r != null) return r;
                }
                return null;
            }
            var arr = AsList(node);
            if (arr != null)
                foreach (var v in arr)
                {
                    var r = FindAggregate(v, depth + 1);
                    if (r != null) return r;
                }
            return null;
        }

        // ── 3) รหัสที่พัก ─────────────────────────────────────────────────────

        private static readonly KeyValuePair<string, Regex>[] IdPatterns =
        {
            // ลำดับ = ความน่าเชื่อถือเมื่อจำนวนครั้งเท่ากัน
            new KeyValuePair<string, Regex>("data-hotel-id", new Regex(@"data-hotel-?id\s*=\s*[""']?(\d{3,10})\b", RegexOptions.IgnoreCase)),
            new KeyValuePair<string, Regex>("data-property-id", new Regex(@"data-property-?id\s*=\s*[""']?(\d{3,10})\b", RegexOptions.IgnoreCase)),
            new KeyValuePair<string, Regex>("canonical/og-url", new Regex(@"<(?:link|meta)[^>]+(?:rel\s*=\s*[""']canonical[""']|property\s*=\s*[""']og:url[""'])[^>]*?(?:hotel_?id|hid|propertyId)=(\d{3,10})", RegexOptions.IgnoreCase)),
            new KeyValuePair<string, Regex>("hotelId-json", new Regex(@"[""']hotelId[""']\s*:\s*[""']?(\d{3,10})\b")),
            new KeyValuePair<string, Regex>("propertyId-json", new Regex(@"[""']propertyId[""']\s*:\s*[""']?(\d{3,10})\b")),
            new KeyValuePair<string, Regex>("masterHotelId-json", new Regex(@"[""'](?:masterHotelId|selectedHotelId|hotel_id|property_id)[""']\s*:\s*[""']?(\d{3,10})\b", RegexOptions.IgnoreCase)),
            new KeyValuePair<string, Regex>("hotelId-js", new Regex(@"\b(?:hotelId|propertyId)\s*[:=]\s*[""']?(\d{3,10})\b")),
            new KeyValuePair<string, Regex>("hotel_id-query", new Regex(@"[?&](?:amp;)?(?:hotel_?id|propertyId|hid)=(\d{3,10})\b", RegexOptions.IgnoreCase)),
            new KeyValuePair<string, Regex>("escaped-json", new Regex(@"\\[""'](?:hotelId|propertyId)\\[""']\s*:\s*\\?[""']?(\d{3,10})\b"))
        };

        /// <summary>หา id ที่พัก: นับทุก pattern แล้วเลือก id ที่พบบ่อยสุด (เสมอกัน = pattern ที่น่าเชื่อถือกว่า)</summary>
        public static string FindPropertyId(string html, out string pattern, out string candidates)
        {
            pattern = null; candidates = null;
            if (string.IsNullOrEmpty(html)) return null;
            var count = new Dictionary<string, int>();
            var firstPattern = new Dictionary<string, int>();
            var patternName = new Dictionary<string, string>();
            for (int p = 0; p < IdPatterns.Length; p++)
            {
                foreach (Match m in IdPatterns[p].Value.Matches(html))
                {
                    string id = m.Groups[1].Value.TrimStart('0');
                    if (id.Length < 3) continue;
                    int c;
                    count[id] = count.TryGetValue(id, out c) ? c + 1 : 1;
                    if (!firstPattern.ContainsKey(id)) { firstPattern[id] = p; patternName[id] = IdPatterns[p].Key; }
                }
            }
            if (count.Count == 0) return null;
            var ordered = count.OrderByDescending(kv => kv.Value).ThenBy(kv => firstPattern[kv.Key]).ToList();
            string best = ordered[0].Key;
            pattern = patternName[best] + " ×" + ordered[0].Value;
            if (ordered.Count > 1)
                candidates = string.Join(", ", ordered.Skip(1).Take(4).Select(kv => kv.Key + " ×" + kv.Value));
            return best;
        }

        // ── 4) รีวิวที่ฝังมากับหน้า ──────────────────────────────────────────

        private static readonly string[] Anchors =
        {
            "\"hotelReviewId\"", "\"reviewComments\"", "\"displayMemberName\"", "\"reviewPositives\"", "\"reviewTitle\"",
            "\"reviewText\"", "\"reviewBody\"", "\"reviewId\""
        };

        /// <summary>
        /// หารีวิวที่อยู่ใน HTML แล้ว (server-rendered state / ld+json) — คืนรายการพร้อมเส้นทางที่ใช้ได้ (path)
        /// ลำดับ: script JSON (application/json, __NEXT_DATA__, window.X = {...}, JSON.parse("...")) → JSON-LD Review → anchor scan
        /// </summary>
        public static List<ReviewItem> ExtractReviews(string html, string pageUrl, decimal defaultScale, string providerMap,
            LightFetchDiagnostics diag, out string path)
        {
            path = "none";
            var found = new List<Dictionary<string, object>>();
            if (string.IsNullOrEmpty(html)) return new List<ReviewItem>();

            // 4-0) DOM รายการรีวิวที่ render แล้ว (Review-comments / data-review-id) — ลองก่อนเสมอ
            int blocks;
            var dom = ParseReviewDom(html, out blocks);
            if (diag != null) { diag.HtmlDomBlocks = blocks; diag.HtmlDomReviews = dom.Count; }
            if (dom.Count > 0)
            {
                path = "html-dom";
                return MapAll(dom, pageUrl, defaultScale, providerMap, diag);
            }

            // 4a) JSON ใน <script> (ไม่รวม ld+json — แยกไว้ทีหลังเพื่อรายงานเส้นทางให้ชัด)
            foreach (Match m in ScriptRx.Matches(html))
            {
                string attrs = m.Groups[1].Value, body = m.Groups[2].Value;
                if (body.Length < 30 || !HasAnchor(body)) continue;
                if (Regex.IsMatch(attrs, @"application/ld\+json", RegexOptions.IgnoreCase)) continue;
                if (Regex.IsMatch(attrs, @"type\s*=\s*[""']?application/(?:json|.*\+json)", RegexOptions.IgnoreCase))
                {
                    var root = TryDeserialize(body.Trim()) ?? TryDeserialize(WebUtility.HtmlDecode(body.Trim()));
                    if (root != null) CollectReviews(root, found, 0);
                }
                if (found.Count == 0) ScanAssignments(body, found);
                if (found.Count == 0) ScanJsonParseLiterals(body, found);
                if (found.Count > 0) { path = "embedded:script-json"; break; }
            }

            // 4b) schema.org Review ใน JSON-LD
            if (found.Count == 0)
            {
                foreach (var raw in JsonLdBlocks(html))
                {
                    var root = TryDeserialize(raw) ?? TryDeserialize(WebUtility.HtmlDecode(raw));
                    if (root != null) CollectReviews(root, found, 0);
                }
                if (found.Count > 0) path = "jsonld-review";
            }

            // 4c) สแกนรอบคีย์ยึดทั้งหน้า (เช่น JSON ใน data-attribute / สคริปต์ที่ตัว regex ไม่ครอบ)
            if (found.Count == 0 && HasAnchor(html))
            {
                AnchorScan(html, found);
                if (found.Count > 0) path = "embedded:anchor-scan";
            }
            // 4d) JSON ที่ถูก escape อยู่ในสตริง JS (\"hotelReviewId\":...)
            if (found.Count == 0 && (html.IndexOf("\\\"hotelReviewId\\\"", StringComparison.Ordinal) >= 0
                                  || html.IndexOf("\\\"reviewComments\\\"", StringComparison.Ordinal) >= 0))
            {
                string un = html.Replace("\\\\", "\u0001").Replace("\\\"", "\"").Replace("\\/", "/").Replace("\u0001", "\\");
                AnchorScan(un, found);
                if (found.Count > 0) path = "embedded:escaped-json";
            }

            return MapAll(found, pageUrl, defaultScale, providerMap, diag);
        }

        /// <summary>แกะคำตอบของ endpoint รีวิว (JSON) — เดินทั้งต้นไม้หา array ของ object ที่หน้าตาเป็นรีวิว</summary>
        public static List<ReviewItem> ExtractReviewsFromJson(string json, string pageUrl, decimal defaultScale, string providerMap,
            LightFetchDiagnostics diag)
        {
            object root;
            return ExtractReviewsFromJson(json, pageUrl, defaultScale, providerMap, diag, out root);
        }

        /// <param name="root">ต้นไม้ JSON ที่แปลงแล้ว (null = แปลงไม่ได้) — ใช้หาคะแนนรวมต่อ</param>
        public static List<ReviewItem> ExtractReviewsFromJson(string json, string pageUrl, decimal defaultScale, string providerMap,
            LightFetchDiagnostics diag, out object root)
        {
            root = null;
            var found = new List<Dictionary<string, object>>();
            if (string.IsNullOrWhiteSpace(json)) return new List<ReviewItem>();
            string t = json.Trim();
            if (t.StartsWith(")]}'", StringComparison.Ordinal)) t = t.Substring(t.IndexOf('\n') + 1);   // XSSI prefix
            root = TryDeserialize(t);
            if (root != null) CollectReviews(root, found, 0);
            else AnchorScan(t, found);   // JSON ไม่สมบูรณ์ — ลองสแกนรอบคีย์ยึด
            if (diag != null)
            {
                if (root != null) diag.ApiTopKeys = DescribeKeys(root);
                if (found.Count > 0) DescribeComment(found[0], diag);
            }
            return MapAll(found, pageUrl, defaultScale, providerMap, diag);
        }

        // ── schema dump (ชื่อฟิลด์ ไม่มีค่า) — ให้เห็นโครงสร้างจริงของ Agoda เพื่อจูนตัวแกะ ──

        public static string DescribeKeys(object root)
        {
            var d = root as Dictionary<string, object>;
            if (d == null)
            {
                var l = AsList(root);
                return l == null ? "(scalar)" : "[array " + l.Count + "]" + (l.Count > 0 && l[0] is Dictionary<string, object> ? " of {" + string.Join(",", ((Dictionary<string, object>)l[0]).Keys.Take(40)) + "}" : "");
            }
            var parts = new List<string>();
            foreach (var kv in d.Take(40))
            {
                var cd = kv.Value as Dictionary<string, object>;
                var cl = AsList(kv.Value);
                if (cd != null) parts.Add(kv.Key + "{" + string.Join(",", cd.Keys.Take(25)) + (cd.Count > 25 ? ",…" : "") + "}");
                else if (cl != null) parts.Add(kv.Key + "[" + cl.Count + "]");
                else parts.Add(kv.Key);
            }
            return string.Join(", ", parts);
        }

        private static readonly Regex ShortValueKey = new Regex(@"lang|translat|original.*(id|code)|isTranslated|providerId|provider$|source$|target$", RegexOptions.IgnoreCase);

        public static void DescribeComment(Dictionary<string, object> c, LightFetchDiagnostics diag)
        {
            var names = new List<string>();
            var vals = new List<string>();
            foreach (var kv in c)
            {
                var nd = kv.Value as Dictionary<string, object>;
                if (nd != null) { names.Add(kv.Key + "{" + string.Join(",", nd.Keys.Take(20)) + "}"); continue; }
                var nl = AsList(kv.Value);
                names.Add(nl != null ? kv.Key + "[" + nl.Count + "]" : kv.Key);
                if (nl == null && kv.Value != null && ShortValueKey.IsMatch(kv.Key))
                {
                    string v = Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
                    if (v != null && v.Length <= 20) vals.Add(kv.Key + "=" + v);
                }
                // บอกว่ามีค่าหรือว่าง สำหรับฟิลด์ต้นฉบับ (ไม่โชว์ข้อความ)
                if (kv.Key.IndexOf("original", StringComparison.OrdinalIgnoreCase) >= 0 && kv.Value is string)
                    vals.Add(kv.Key + (string.IsNullOrWhiteSpace((string)kv.Value) ? "=(ว่าง)" : "=(มีข้อความ)"));
            }
            diag.CommentFields = string.Join(", ", names);
            diag.CommentShortValues = string.Join(", ", vals);
        }

        // ── คะแนนรวมสำรอง (เมื่อ JSON-LD ไม่มี aggregateRating) ─────────────────
        //   ไม่เฉลี่ยจากรีวิวรายตัวเด็ดขาด — อ่านเฉพาะฟิลด์สรุปใน object ที่ "ไม่ใช่" รีวิว
        //   ตั้งค่าเฉพาะเมื่อมั่นใจ: ผู้สมัครอันดับสูงสุด priority ≥ 2 และไม่มีค่าอื่นที่ priority เท่ากัน หรือทุกค่าตรงกัน

        public class SummaryCandidate
        {
            public string Path;
            public decimal Value;
            public int Priority;
            public bool IsCount;
            public string Parent;
            public override string ToString()
            {
                return Path + "=" + Value.ToString("0.##", CultureInfo.InvariantCulture) + " (p" + Priority + ")";
            }
        }

        private static readonly HashSet<string> ScoreKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "score", "reviewScore", "overallScore", "averageScore", "cumulativeScore", "combinedScore", "overallRating",
            "averageRating", "ratingValue", "hotelScore", "propertyScore", "hotelReviewScore", "totalScore"
        };
        private static readonly HashSet<string> CountKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "reviewCount", "totalReviews", "countOfReviews", "numberOfReviews", "reviewsCount", "totalReviewCount",
            "reviewCounts", "totalCount", "count", "ratingCount"
        };

        private static int PathPriority(string path, bool allGuests)
        {
            string p = path.ToLowerInvariant();
            int pri = 1;
            if (p.Contains("combined")) pri = Math.Max(pri, 5);
            if (p.Contains("cumulative")) pri = Math.Max(pri, 4);
            if (allGuests || p.Contains("allguest")) pri = Math.Max(pri, 4);
            if (p.Contains("overall") || p.Contains("summary") || p.Contains("aggregate")) pri = Math.Max(pri, 3);
            if (p.Contains("hotel") || p.Contains("property")) pri = Math.Max(pri, 2);
            if (!allGuests && (p.Contains("similar") || p.Contains("nearby") || p.Contains("recommend"))) pri = 0;
            return pri;
        }

        private static bool IsAllGuests(Dictionary<string, object> d)
        {
            foreach (var k in new[] { "name", "title", "label", "id", "demographicName", "groupName" })
            {
                string v = Str(Get(d, k));
                if (v == null) continue;
                string l = v.ToLowerInvariant().Replace(" ", "");
                if (l == "allguests" || l == "allguest" || l == "all" || l.Contains("ผู้เข้าพักทั้งหมด") || l == "ทั้งหมด") return true;
            }
            return false;
        }

        /// <summary>เดินต้นไม้ JSON เก็บผู้สมัครคะแนน/จำนวนรีวิวจาก object ที่ไม่ใช่รีวิว</summary>
        public static void CollectSummary(object node, string path, int depth, List<SummaryCandidate> list, bool allGuests)
        {
            if (node == null || depth > 25 || list.Count > 200) return;
            var d = node as Dictionary<string, object>;
            if (d != null)
            {
                if (IsReviewLike(d)) return;
                bool ag = allGuests || IsAllGuests(d);
                foreach (var kv in d)
                {
                    string p = path.Length == 0 ? kv.Key : path + "." + kv.Key;
                    if (kv.Value is Dictionary<string, object> || (AsList(kv.Value) != null))
                    {
                        CollectSummary(kv.Value, p, depth + 1, list, ag);
                        continue;
                    }
                    if (kv.Value == null || kv.Value is bool) continue;
                    bool isScore = ScoreKeys.Contains(kv.Key)
                                || (kv.Key.Equals("rating", StringComparison.OrdinalIgnoreCase) && PathPriority(p, ag) >= 3);
                    bool isCount = CountKeys.Contains(kv.Key);
                    if (!isScore && !isCount) continue;
                    decimal? v = ReviewRating.Parse(kv.Value);
                    if (!v.HasValue) continue;
                    if (isScore && v.Value > 0 && v.Value <= 10)
                        list.Add(new SummaryCandidate { Path = p, Value = v.Value, Priority = PathPriority(p, ag), Parent = path });
                    else if (isCount && v.Value >= 1 && v.Value == Math.Floor(v.Value) && v.Value < 10000000)
                    {
                        int pri = PathPriority(p, ag);
                        // count/totalCount ลอย ๆ อาจเป็นจำนวนรีวิวในหน้าผลลัพธ์ — ลดน้ำหนัก
                        if ((kv.Key.Equals("count", StringComparison.OrdinalIgnoreCase) || kv.Key.Equals("totalCount", StringComparison.OrdinalIgnoreCase)) && pri < 3) pri = Math.Min(pri, 1);
                        list.Add(new SummaryCandidate { Path = p, Value = v.Value, Priority = pri, Parent = path, IsCount = true });
                    }
                }
                return;
            }
            var arr = AsList(node);
            if (arr == null) return;
            int reviewish = 0;
            for (int i = 0; i < arr.Count && i < 5; i++) if (arr[i] is Dictionary<string, object> && IsReviewLike((Dictionary<string, object>)arr[i])) reviewish++;
            if (reviewish > 0) return;   // รายการรีวิว — ข้ามทั้งก้อน
            for (int i = 0; i < arr.Count && i < 50; i++) CollectSummary(arr[i], path + "[" + i + "]", depth + 1, list, allGuests);
        }

        /// <summary>เลือกคะแนน/จำนวนที่มั่นใจจากผู้สมัคร — คืน false ถ้าไม่มั่นใจ (ไม่ตั้งค่า)</summary>
        public static bool PickSummary(List<SummaryCandidate> list, out decimal? rating, out int? count, out string source)
        {
            rating = null; count = null; source = null;
            var scores = list.Where(c => !c.IsCount && c.Priority > 0).OrderByDescending(c => c.Priority).ToList();
            if (scores.Count == 0) return false;
            var best = scores[0];
            bool unanimous = scores.Select(c => c.Value).Distinct().Count() == 1;
            bool clearTop = best.Priority >= 2 && !scores.Any(c => c.Priority == best.Priority && c.Value != best.Value);
            if (!(unanimous || clearTop)) return false;
            rating = best.Value;
            source = best.Path;
            var counts = list.Where(c => c.IsCount && c.Priority > 0).ToList();
            // จำนวนรีวิว: อยู่ object เดียวกัน/object แม่ของคะแนนก่อน ไม่งั้นต้องมั่นใจแบบเดียวกัน
            var sib = counts.Where(c => c.Parent == best.Parent || (c.Parent.Length > 0 && best.Path.StartsWith(c.Parent + ".", StringComparison.Ordinal)))
                            .OrderByDescending(c => c.Parent.Length).ThenByDescending(c => c.Priority).FirstOrDefault();
            if (sib != null) count = (int)sib.Value;
            else if (counts.Count > 0)
            {
                var top = counts.OrderByDescending(c => c.Priority).First();
                bool cu = counts.Select(c => c.Value).Distinct().Count() == 1;
                bool ct = top.Priority >= 2 && !counts.Any(c => c.Priority == top.Priority && c.Value != top.Value);
                if (cu || ct) count = (int)top.Value;
            }
            return true;
        }

        private static readonly Regex HtmlScoreRx = new Regex(
            @"[""'](reviewScore|overallScore|cumulativeScore|combinedScore|averageScore|hotelReviewScore|score)[""']\s*:\s*[""']?(\d{1,2}(?:\.\d{1,2})?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HtmlCountRx = new Regex(
            @"[""'](reviewCount|totalReviews|countOfReviews|numberOfReviews|reviewsCount|totalReviewCount)[""']\s*:\s*[""']?(\d{1,7})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// หา คะแนน/จำนวนรีวิว ในสคริปต์/state ของหน้า: นับเฉพาะค่าที่ใกล้รหัสที่พักของเรา (ตัดของ "ที่พักใกล้เคียง")
        /// "score" ทั่วไปนับเฉพาะเมื่อบริบทมีคำว่า review/combined/cumulative
        /// </summary>
        public static List<SummaryCandidate> CollectSummaryFromHtml(string html, string propertyId)
        {
            var list = new List<SummaryCandidate>();
            if (string.IsNullOrEmpty(html)) return list;
            Action<Regex, bool> scan = (rx, isCount) =>
            {
                foreach (Match m in rx.Matches(html))
                {
                    if (list.Count > 200) break;
                    string key = m.Groups[1].Value;
                    decimal v;
                    if (!decimal.TryParse(m.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out v)) continue;
                    if (isCount ? v < 1 : (v <= 0 || v > 10)) continue;
                    int from = Math.Max(0, m.Index - 600);
                    string ctx = html.Substring(from, m.Index - from).ToLowerInvariant();
                    if (!isCount && key.Equals("score", StringComparison.OrdinalIgnoreCase)
                        && !(ctx.Contains("review") || ctx.Contains("combined") || ctx.Contains("cumulative"))) continue;
                    int pri = 1;
                    if (!key.Equals("score", StringComparison.OrdinalIgnoreCase)) pri++;
                    if (ctx.Contains("combined") || ctx.Contains("cumulative") || ctx.Contains("overall")) pri += 1;
                    if (!string.IsNullOrEmpty(propertyId))
                    {
                        int to = Math.Min(html.Length, m.Index + 300);
                        if (html.IndexOf(propertyId, from, to - from, StringComparison.Ordinal) >= 0) pri += 2;
                    }
                    if (ctx.Contains("similar") || ctx.Contains("nearby") || ctx.Contains("recommend")) pri = 0;
                    list.Add(new SummaryCandidate { Path = "page:" + key + "@" + m.Index, Value = v, Priority = pri, IsCount = isCount, Parent = "page@" + (m.Index / 2000) });
                }
            };
            scan(HtmlScoreRx, false);
            scan(HtmlCountRx, true);
            return list;
        }

        private static bool HasAnchor(string s)
        {
            foreach (var a in Anchors) if (s.IndexOf(a, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>window.__STATE__ = {...}; / "props": {...} — ลองทุก { หรือ [ หลัง = ที่ช่วงครอบคีย์ยึด</summary>
        private static void ScanAssignments(string body, List<Dictionary<string, object>> found)
        {
            int tries = 0;
            foreach (Match m in Regex.Matches(body, @"=\s*([\{\[])"))
            {
                if (++tries > 30) break;
                int start = m.Groups[1].Index;
                int end = MatchBalanced(body, start);
                if (end < 0) continue;
                string span = body.Substring(start, end - start + 1);
                if (!HasAnchor(span)) continue;
                var root = TryDeserialize(span);
                if (root != null) CollectReviews(root, found, 0);
                if (found.Count > 0) return;
            }
            // ทั้ง body เป็น JSON (เช่น <script id="__NEXT_DATA__"> ที่ไม่มี type)
            string t = body.Trim();
            if (t.StartsWith("{") || t.StartsWith("["))
            {
                var root = TryDeserialize(t);
                if (root != null) CollectReviews(root, found, 0);
            }
        }

        private static void ScanJsonParseLiterals(string body, List<Dictionary<string, object>> found)
        {
            foreach (Match m in Regex.Matches(body, @"JSON\.parse\(\s*([""'])"))
            {
                int qpos = m.Groups[1].Index;
                string lit = ReadJsString(body, qpos);
                if (lit == null || !HasAnchor(lit)) continue;
                var root = TryDeserialize(lit);
                if (root != null) CollectReviews(root, found, 0);
                if (found.Count > 0) return;
            }
        }

        /// <summary>
        /// สแกนรอบคีย์ยึด: จากแต่ละตำแหน่งคีย์ยึด ถอยหลังหา '{' ทีละตัว แล้วตัดช่วงที่สมดุลไปข้างหน้า —
        /// ช่วงแรกที่ครอบคีย์ยึด + แปลง JSON ได้ + เป็นรีวิว ⇒ เก็บ (ข้ามคีย์ยึดที่อยู่ในช่วงที่เก็บแล้ว)
        /// </summary>
        private static void AnchorScan(string s, List<Dictionary<string, object>> found)
        {
            var covered = new List<KeyValuePair<int, int>>();
            var positions = new List<int>();
            foreach (var a in Anchors)
            {
                int idx = 0;
                while ((idx = s.IndexOf(a, idx, StringComparison.Ordinal)) >= 0 && positions.Count < 2000) { positions.Add(idx); idx += a.Length; }
            }
            positions.Sort();
            foreach (int pos in positions)
            {
                if (covered.Any(c => pos >= c.Key && pos <= c.Value)) continue;
                int tries = 0;
                int back = pos;
                while (tries < MaxAnchorTries && back > 0 && pos - back < 200000)
                {
                    back = s.LastIndexOf('{', back - 1);
                    if (back < 0) break;
                    tries++;
                    int end = MatchBalanced(s, back);
                    if (end < pos) continue;   // ไม่ครอบคีย์ยึด
                    var root = TryDeserialize(s.Substring(back, end - back + 1));
                    if (root == null) continue;
                    int before = found.Count;
                    var d = root as Dictionary<string, object>;
                    if (d != null && IsReviewLike(d)) found.Add(d);
                    if (found.Count > before) { covered.Add(new KeyValuePair<int, int>(back, end)); break; }
                }
            }
        }

        /// <summary>หา index ของวงเล็บปิดที่คู่กับ s[start] ('{' หรือ '[') โดยข้ามสตริง JSON — ไม่พบคืน -1</summary>
        public static int MatchBalanced(string s, int start)
        {
            if (start < 0 || start >= s.Length || (s[start] != '{' && s[start] != '[')) return -1;
            int depth = 0;
            bool inStr = false;
            int limit = Math.Min(s.Length, start + MaxSpan);
            for (int i = start; i < limit; i++)
            {
                char c = s[i];
                if (inStr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; continue; }
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']')
                {
                    depth--;
                    if (depth == 0) return i;
                    if (depth < 0) return -1;
                }
                else if (c == '<' && i + 8 < limit && string.CompareOrdinal(s, i, "</script", 0, 8) == 0) return -1;
            }
            return -1;
        }

        /// <summary>อ่าน string literal ของ JS ที่ s[q] เป็นเครื่องหมายคำพูด → ข้อความที่ถอด escape แล้ว</summary>
        public static string ReadJsString(string s, int q)
        {
            if (q < 0 || q >= s.Length) return null;
            char quote = s[q];
            var sb = new StringBuilder();
            for (int i = q + 1; i < s.Length && sb.Length < MaxSpan; i++)
            {
                char c = s[i];
                if (c == quote) return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (++i >= s.Length) break;
                char e = s[i];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        int code;
                        if (i + 4 < s.Length && int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                        { sb.Append((char)code); i += 4; }
                        else sb.Append('u');
                        break;
                    case 'x':
                        int hx;
                        if (i + 2 < s.Length && int.TryParse(s.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hx))
                        { sb.Append((char)hx); i += 2; }
                        else sb.Append('x');
                        break;
                    default: sb.Append(e); break;   // \" \' \\ \/
                }
            }
            return null;
        }

        private static object TryDeserialize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            string t = s.Trim();
            if (t.EndsWith(";")) t = t.Substring(0, t.Length - 1);
            if (!(t.StartsWith("{") || t.StartsWith("["))) return null;
            try { return Json().DeserializeObject(t); }
            catch { return null; }
        }

        // ── 4-0) DOM รายการรีวิว (หน้าที่ render แล้ว) ──────────────────────────
        //   <ol class="Review-comments" data-provider-id="332">
        //     <div data-element-name="review-comment" data-review-id="…" data-review-comment-type="comment" class="Review-comment">
        //       Review-comment-left: <p><span>10.0</span><span>ยอดเยี่ยม</span></p>
        //         data-info-type = reviewer-name (<strong>ชื่อ</strong><span> จาก</span><span>ประเทศ</span>) / group-name / room-type / stay-detail
        //       <h4 data-testid="review-title">“…”</h4> <p data-testid="review-comment" class="Review-comment-bodyText">…</p>
        //       <div class="Review-statusBar"><span>รีวิวเมื่อ 12 ตุลาคม 2026</span>
        //   ยึดเฉพาะ data-* และคลาส Review-* (คลาสแฮช เช่น sc-aXZVg / a07fe-* เปลี่ยนได้ทุก deploy) — แบ่งบล็อกด้วย regex
        //   ได้ dictionary รูปเดียวกับรีวิว JSON แล้วส่งต่อ MapReview (map ผู้ให้บริการ/คะแนน/วันที่ชุดเดียวกัน)

        private static readonly Regex DomStartRx = new Regex(
            @"<(?:div|li|article|section)\b(?=[^>]*\bdata-element-name\s*=\s*[""']review-comment[""'])[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ClassStartRx = new Regex(
            @"<(?:div|li|article)\b(?=[^>]*\bclass\s*=\s*[""'](?:[^""']*\s)?Review-comment(?:\s[^""']*)?[""'])(?=[^>]*\bdata-review-id\s*=)[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>แกะ DOM รายการรีวิว — blocks = จำนวนบล็อกรีวิวที่เห็น (ใช้รายงานว่าหน้ามี DOM รีวิวไหม)</summary>
        public static List<Dictionary<string, object>> ParseReviewDom(string html, out int blocks)
        {
            var list = new List<Dictionary<string, object>>();
            blocks = 0;
            if (string.IsNullOrEmpty(html) || html.IndexOf("review-comment", StringComparison.OrdinalIgnoreCase) < 0
                && html.IndexOf("Review-comment", StringComparison.Ordinal) < 0) return list;
            var starts = DomStartRx.Matches(html).Cast<Match>().ToList();
            if (starts.Count == 0) starts = ClassStartRx.Matches(html).Cast<Match>().ToList();
            blocks = starts.Count;
            for (int i = 0; i < starts.Count && list.Count < 100; i++)
            {
                var m = starts[i];
                int end = i + 1 < starts.Count ? starts[i + 1].Index : Math.Min(html.Length, m.Index + 80000);
                if (i + 1 >= starts.Count)
                {
                    int close = html.IndexOf("</ol>", m.Index, StringComparison.OrdinalIgnoreCase);
                    if (close > m.Index && close < end) end = close;
                }
                string tag = m.Value;
                string seg = html.Substring(m.Index, Math.Max(0, end - m.Index));
                string type = Attr(tag, "data-review-comment-type");
                if (type != null && !type.Equals("comment", StringComparison.OrdinalIgnoreCase)
                                 && !type.Equals("review", StringComparison.OrdinalIgnoreCase)) continue;
                var d = DomToReview(html, m.Index, tag, seg);
                if (d != null) list.Add(d);
            }
            return list;
        }

        private static Dictionary<string, object> DomToReview(string html, int pos, string tag, string seg)
        {
            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string id = Attr(tag, "data-review-id");
            if (id != null && Regex.IsMatch(id, @"^\d{1,20}$")) d["hotelReviewId"] = id;

            // ผู้ให้บริการ: data-provider-id ในบล็อก (ปุ่ม helpful) ก่อน ไม่งั้นของ <ol class="Review-comments"> ที่อยู่ก่อนหน้า
            var pm = Regex.Match(seg, @"data-provider-id\s*=\s*[""']?(\d{1,8})");
            string pid = pm.Success ? pm.Groups[1].Value : null;
            if (pid == null)
            {
                int lookFrom = Math.Max(0, pos - 200000);
                var before = Regex.Matches(html.Substring(lookFrom, pos - lookFrom), @"data-provider-id\s*=\s*[""']?(\d{1,8})");
                if (before.Count > 0) pid = before[before.Count - 1].Groups[1].Value;
            }
            if (pid != null) d["providerId"] = pid;

            // คะแนน: <span>10.0</span> แรกในฝั่งซ้าย (ก่อน Review-comment-right)
            int right = seg.IndexOf("Review-comment-right", StringComparison.Ordinal);
            string left = right > 0 ? seg.Substring(0, right) : seg;
            var rm = Regex.Match(left, @"<span\b[^>]*>\s*(\d{1,2}(?:[.,]\d{1,2})?)\s*</span>");
            if (rm.Success) d["rating"] = rm.Groups[1].Value.Replace(',', '.');

            var info = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string reviewer = InfoBlock(seg, "reviewer-name");
            if (reviewer != null)
            {
                var sm = Regex.Match(reviewer, @"<strong\b[^>]*>(.*?)</strong>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                string name = sm.Success ? StripTags(sm.Groups[1].Value) : Regex.Replace(StripTags(reviewer), @"\s+จาก\s+.*$", "");
                if (!string.IsNullOrWhiteSpace(name)) info["displayMemberName"] = name.Trim();
            }
            string group = StripTags(InfoBlock(seg, "group-name"));
            if (!string.IsNullOrWhiteSpace(group)) info["reviewGroupName"] = group.Trim();
            string room = StripTags(InfoBlock(seg, "room-type"));
            if (!string.IsNullOrWhiteSpace(room)) info["roomTypeName"] = room.Trim();
            if (info.Count > 0) d["reviewerInfo"] = info;
            string stay = StripTags(InfoBlock(seg, "stay-detail"));
            if (!string.IsNullOrWhiteSpace(stay)) d["checkInDateMonthAndYear"] = stay.Trim();

            var tm = Regex.Match(seg, @"<(h\d|div|span|p)\b[^>]*data-testid\s*=\s*[""']review-title[""'][^>]*>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (tm.Success)
            {
                string title = StripTags(tm.Groups[2].Value).Trim().Trim('“', '”', '"', '„', '«', '»', ' ', '\u00A0').Trim();
                if (title.Length > 0) d["reviewTitle"] = title;
            }

            var bm = Regex.Match(seg, @"<p\b(?=[^>]*(?:data-testid\s*=\s*[""']review-comment[""']|Review-comment-bodyText))[^>]*>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (bm.Success)
            {
                string body = StripTags(bm.Groups[1].Value, true).Trim();
                if (body.Length > 0) d["reviewComments"] = body;
            }

            // วันที่รีวิว: ข้อความใน Review-statusBar (รีวิวเมื่อ …) — สำรอง: ข้อความ "รีวิวเมื่อ" ที่ใดก็ได้ในบล็อก / aria-label
            int sb = seg.IndexOf("Review-statusBar", StringComparison.Ordinal);
            string dateText = null;
            if (sb >= 0)
            {
                var dm = Regex.Match(seg.Substring(sb), @"<span\b[^>]*>([^<]{4,80})</span>", RegexOptions.IgnoreCase);
                if (dm.Success) dateText = dm.Groups[1].Value;
            }
            if (dateText == null)
            {
                var dm = Regex.Match(seg, @"(?:รีวิวเมื่อ|Reviewed)\s*([^<""]{4,40})");
                if (dm.Success) dateText = dm.Groups[0].Value;
            }
            if (dateText != null) d["reviewDate"] = WebUtility.HtmlDecode(dateText).Trim();

            // คำตอบจากที่พัก (best-effort — ยังไม่เห็นตัวอย่างจริง)
            var resp = Regex.Match(seg, @"(?:data-review-comment-type\s*=\s*[""']response[""']|class\s*=\s*[""'][^""']*Review-response[^""']*[""']|ตอบกลับจากที่พัก|Response from (?:the )?property)",
                RegexOptions.IgnoreCase);
            if (resp.Success)
            {
                string after = seg.Substring(resp.Index, Math.Min(6000, seg.Length - resp.Index));
                var rp = Regex.Match(after, @"<p\b(?![^>]*data-testid\s*=\s*[""']review-comment[""'])[^>]*>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (rp.Success)
                {
                    string r = StripTags(rp.Groups[1].Value, true).Trim();
                    if (r.Length > 0 && !string.Equals(r, Str(Get(d, "reviewComments")), StringComparison.Ordinal)) d["responseText"] = r;
                }
            }

            bool content = d.ContainsKey("reviewComments") || d.ContainsKey("reviewTitle");
            return content ? d : null;
        }

        private static string Attr(string tag, string name)
        {
            var m = Regex.Match(tag, @"\b" + Regex.Escape(name) + @"\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            return m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
        }

        /// <summary>เนื้อในของ element ที่มี data-info-type="…" (ถึง &lt;/div&gt; แรก — บล็อกนี้มีแต่ i/strong/span ข้างใน)</summary>
        private static string InfoBlock(string seg, string infoType)
        {
            var m = Regex.Match(seg, @"<(div|span|li|p)\b[^>]*data-info-type\s*=\s*[""']" + Regex.Escape(infoType) + @"[""'][^>]*>(.*?)</\1>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[2].Value : null;
        }

        /// <summary>ตัดแท็ก (คง entity ไว้ให้ MapReview ถอดครั้งเดียว) — keepNewlines: &lt;br&gt; → \n และไม่ยุบบรรทัด</summary>
        private static string StripTags(string s, bool keepNewlines = false)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = Regex.Replace(s, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"<[^>]+>", keepNewlines ? "" : " ");
            if (keepNewlines)
            {
                t = t.Replace("\r\n", "\n");
                t = Regex.Replace(t, @"[ \t]+\n", "\n");
                return t;
            }
            return Regex.Replace(WebUtility.HtmlDecode(t), @"\s+", " ").Trim();
        }

        // ── 5) เดินต้นไม้ JSON หา object ที่เป็นรีวิว ─────────────────────────

        private static readonly string[] SpecificKeys =
            { "hotelReviewId", "reviewId", "reviewText", "reviewBody", "reviewTitle", "reviewPositives", "reviewNegatives", "reviewerInfo" };

        public static void CollectReviews(object node, List<Dictionary<string, object>> found, int depth)
        {
            if (node == null || depth > 60 || found.Count > 500) return;
            var d = node as Dictionary<string, object>;
            if (d != null)
            {
                if (IsReviewLike(d)) { found.Add(d); return; }
                foreach (var v in d.Values) CollectReviews(v, found, depth + 1);
                return;
            }
            var arr = AsList(node);
            if (arr != null) foreach (var v in arr) CollectReviews(v, found, depth + 1);
        }

        public static bool IsReviewLike(Dictionary<string, object> d)
        {
            if (d == null) return false;
            bool specific = string.Equals(Str(Get(d, "@type")), "Review", StringComparison.OrdinalIgnoreCase)
                         || Get(d, "reviewComments") is string;
            if (!specific)
                foreach (var k in SpecificKeys) if (d.ContainsKey(k)) { specific = true; break; }
            if (!specific) return false;
            bool content = !string.IsNullOrWhiteSpace(TextOf(d)) || !string.IsNullOrWhiteSpace(TitleOf(d))
                        || !string.IsNullOrWhiteSpace(Str(Get(d, "reviewPositives"))) || !string.IsNullOrWhiteSpace(Str(Get(d, "reviewNegatives")));
            if (!content) return false;
            return RatingOf(d).HasValue || !string.IsNullOrWhiteSpace(NameOf(d));
        }

        // ── 6) map เป็น ReviewItem ────────────────────────────────────────────

        private static List<ReviewItem> MapAll(List<Dictionary<string, object>> found, string pageUrl, decimal defaultScale,
            string providerMap, LightFetchDiagnostics diag)
        {
            var map = ParseProviderMap(providerMap);
            var items = new List<ReviewItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in found)
            {
                var it = MapReview(d, pageUrl, defaultScale, map, diag);
                if (it == null) continue;
                string key = it.ExternalReviewId ?? ReviewText.ContentHash(it.OriginPlatform, it.ReviewerName, it.ReviewDate, it.Title, it.Text, it.RatingRaw);
                if (!seen.Add(key)) continue;
                items.Add(it);
                if (items.Count >= 100) break;   // หน้าแรกเท่านั้น — กันข้อมูลประหลาด
            }
            return items;
        }

        public static Dictionary<string, string> ParseProviderMap(string s)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in (string.IsNullOrWhiteSpace(s) ? DefaultProviderMap : s).Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int c = part.IndexOf(':');
                if (c <= 0) continue;
                string k = part.Substring(0, c).Trim(), v = part.Substring(c + 1).Trim();
                if (k.Length > 0 && v.Length > 0) map[k] = v;
            }
            return map;
        }

        public static ReviewItem MapReview(Dictionary<string, object> d, string pageUrl, decimal defaultScale,
            Dictionary<string, string> providerMap, LightFetchDiagnostics diag)
        {
            // ข้อความที่แสดง (อาจเป็นคำแปลของ Agoda) vs ต้นฉบับ (originalComment / originalTitle / original*Positives …)
            string shownText = TextOf(d), shownTitle = TitleOf(d);
            string shownPos = Str(Get(d, "reviewPositives")), shownNeg = Str(Get(d, "reviewNegatives"));
            string oText = OriginalOf(d, "comment", "text", "body", "review", "content");
            string oTitle = OriginalOf(d, "title", "headline");
            string oPos = OriginalOf(d, "positive");
            string oNeg = OriginalOf(d, "negative");
            bool useOText = !string.IsNullOrWhiteSpace(oText) && !SameText(oText, shownText);
            bool useOTitle = !string.IsNullOrWhiteSpace(oTitle) && !SameText(oTitle, shownTitle);

            string srcLang = LangOf(d, "translateSource", "originalLanguage", "originalLanguageCode", "sourceLanguage",
                                       "originalLanguageId", "reviewLanguage", "languageCode", "language", "languageId");
            string tgtLang = LangOf(d, "translateTarget", "targetLanguage", "translatedLanguage", "translateTargetLanguage");
            bool? flag = BoolOf(d, "isTranslated", "translated", "isMachineTranslated", "isAutoTranslated", "machineTranslated");

            string text = useOText ? oText : shownText;
            string title = useOTitle ? oTitle : shownTitle;
            string pos = !string.IsNullOrWhiteSpace(oPos) ? oPos : (useOText ? null : shownPos);   // ไม่ปนภาษา
            string neg = !string.IsNullOrWhiteSpace(oNeg) ? oNeg : (useOText ? null : shownNeg);
            string body = JoinBody(text, pos, neg);
            if (body.Length == 0 && string.IsNullOrWhiteSpace(title)) return null;

            // แปลโดย Agoda? — มีต้นฉบับแยก = ใช้ต้นฉบับ (ไม่ใช่คำแปล); ไม่มีต้นฉบับ: ดูธง/ภาษาต้นทาง≠ปลายทาง + ตรวจอักษรจริง
            bool machine = false;
            string lang;
            string detected = DetectLang(body.Length > 0 ? body : title);
            if (useOText || useOTitle)
                lang = srcLang ?? detected;
            else
            {
                if (flag == true) machine = true;
                else if (srcLang != null && tgtLang != null && srcLang != tgtLang)
                    machine = detected != null ? detected == tgtLang && detected != srcLang : flag != false;
                lang = machine ? (tgtLang ?? detected) : (srcLang ?? detected);
                if (!machine && detected != null && srcLang != null && detected != srcLang && (detected == "th" || srcLang == "th"))
                    lang = detected;   // ภาษาที่ประกาศไม่ตรงกับอักษรจริง (ไทย/ไม่ไทย) — เชื่ออักษร
            }

            decimal? scale;
            decimal? rating = RatingOf(d, out scale);
            if (!scale.HasValue) scale = defaultScale > 0 ? defaultScale : 10m;

            string id = Str(Get(d, "hotelReviewId")) ?? Str(Get(d, "reviewId"));
            if (string.IsNullOrWhiteSpace(id) || id == "0") id = null;

            string origin = OriginOf(d, providerMap, diag);
            string shownBody = JoinBody(shownText, shownPos, shownNeg);

            var item = new ReviewItem
            {
                ExternalReviewId = id == null ? null : "agoda:" + id.Trim(),
                OriginPlatform = origin,
                ReviewerName = ReviewText.CleanName(NameOf(d)),
                RatingRaw = rating,
                RatingScale = rating.HasValue ? scale : null,
                Title = string.IsNullOrWhiteSpace(title) ? null : WebUtility.HtmlDecode(title.Trim()),
                Text = body.Length == 0 ? null : WebUtility.HtmlDecode(body),
                Language = lang,
                IsOriginalText = !machine,
                MachineTranslated = machine,
                TranslatedText = useOText && shownBody.Length > 0 ? WebUtility.HtmlDecode(shownBody) : null,
                TranslatedTitle = useOTitle && !string.IsNullOrWhiteSpace(shownTitle) ? WebUtility.HtmlDecode(shownTitle.Trim()) : null,
                ReviewDate = DateOf(d),
                ReviewUrl = pageUrl,
                RoomType = Str(Get(Info(d), "roomTypeName")) ?? Str(Get(d, "roomTypeName")),
                TravellerType = Str(Get(Info(d), "reviewGroupName")) ?? Str(Get(d, "reviewGroupName")) ?? Str(Get(d, "travellerType")),
                StayDate = ParseFlexibleDate(Get(d, "checkInDateMonthAndYear")),
                OwnerResponse = Str(Get(d, "responseText")) ?? Str(Get(d, "hotelResponse")),
                InsertOnly = true
            };
            return item;
        }

        private static string JoinBody(string text, string pos, string neg)
        {
            var sb = new StringBuilder(text ?? "");
            if (!string.IsNullOrWhiteSpace(pos)) sb.Append(sb.Length > 0 ? "\n" : "").Append("ข้อดี: ").Append(pos.Trim());
            if (!string.IsNullOrWhiteSpace(neg)) sb.Append(sb.Length > 0 ? "\n" : "").Append("ข้อเสีย: ").Append(neg.Trim());
            return sb.ToString().Trim();
        }

        private static bool SameText(string a, string b)
        {
            return string.Equals(ReviewText.NormalizeForHash(a), ReviewText.NormalizeForHash(b), StringComparison.Ordinal);
        }

        /// <summary>ค่าข้อความของคีย์ต้นฉบับ: ชื่อคีย์ขึ้นต้น/มีคำว่า original และมีคำใดคำหนึ่งใน parts (เช่น originalComment, reviewOriginalTitle)</summary>
        private static string OriginalOf(Dictionary<string, object> d, params string[] parts)
        {
            foreach (var kv in d)
            {
                string k = kv.Key.ToLowerInvariant();
                if (!k.Contains("original")) continue;
                if (k.Contains("language") || k.Contains("lang")) continue;
                bool hit = false;
                foreach (var p in parts) if (k.Contains(p)) { hit = true; break; }
                if (!hit) continue;
                // originalComment ไม่ควรจับ "originalTitle" ผ่านคำว่า review — ตัดคีย์ที่เป็นของส่วนอื่นออก
                if (!parts.Contains("title") && k.Contains("title")) continue;
                if (!parts.Contains("positive") && k.Contains("positive")) continue;
                if (!parts.Contains("negative") && k.Contains("negative")) continue;
                string s = kv.Value as string;
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            return null;
        }

        private static readonly Dictionary<string, string> LangIdMap = new Dictionary<string, string>
        {
            // ⚠ ยังไม่ยืนยันกับ Agoda — ใช้เฉพาะเมื่อคำตอบให้ languageId เป็นตัวเลข
            { "1", "en" }, { "22", "th" }
        };

        private static string LangOf(Dictionary<string, object> d, params string[] keys)
        {
            foreach (var k in keys)
            {
                object v = Get(d, k);
                string s = Str(v);
                if (string.IsNullOrEmpty(s) || s.Length > 20) continue;
                string n = NormLang(s);
                if (n != null) return n;
            }
            return null;
        }

        public static string NormLang(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().ToLowerInvariant();
            string m;
            if (Regex.IsMatch(s, @"^\d+$")) return LangIdMap.TryGetValue(s, out m) ? m : null;
            if (s == "thai") return "th";
            if (s == "english") return "en";
            var mm = Regex.Match(s, @"^([a-z]{2,3})(?:[-_][a-z0-9]{2,4})?$");
            return mm.Success ? mm.Groups[1].Value : null;
        }

        private static bool? BoolOf(Dictionary<string, object> d, params string[] keys)
        {
            foreach (var k in keys)
            {
                object v = Get(d, k);
                if (v is bool) return (bool)v;
                string s = Str(v);
                if (s == null) continue;
                if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return null;
        }

        /// <summary>เดาภาษาจากอักษร (ไทย/เกาหลี/ญี่ปุ่น/จีน/ละติน→en) — ไม่แน่ใจคืน null</summary>
        public static string DetectLang(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            int thai = 0, latin = 0, hangul = 0, kana = 0, han = 0;
            foreach (char c in s)
            {
                if (c >= '฀' && c <= '๿') thai++;
                else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) latin++;
                else if (c >= '가' && c <= '힯') hangul++;
                else if (c >= '぀' && c <= 'ヿ') kana++;
                else if (c >= '一' && c <= '鿿') han++;
            }
            int total = thai + latin + hangul + kana + han;
            if (total < 3) return null;
            if (thai * 3 >= total) return "th";
            if (hangul * 3 >= total) return "ko";
            if (kana > 0 && (kana + han) * 3 >= total) return "ja";
            if (han * 3 >= total) return "zh";
            if (latin * 10 >= total * 9) return "en";   // ละตินล้วน — ส่วนใหญ่เป็นอังกฤษ (ภาษายุโรปอื่นอาจผิด)
            return null;
        }

        private static Dictionary<string, object> Info(Dictionary<string, object> d)
        {
            return (Get(d, "reviewerInfo") as Dictionary<string, object>) ?? (Get(d, "reviewer") as Dictionary<string, object>);
        }

        private static string NameOf(Dictionary<string, object> d)
        {
            var info = Info(d);
            string n = Str(Get(info, "displayMemberName")) ?? Str(Get(info, "displayName")) ?? Str(Get(info, "name"));
            if (n != null) return n;
            n = Str(Get(d, "displayMemberName")) ?? Str(Get(d, "reviewerName")) ?? Str(Get(d, "memberName"));
            if (n != null) return n;
            object a = Get(d, "author");
            var ad = a as Dictionary<string, object>;
            if (ad != null) return Str(Get(ad, "name"));
            return a as string;
        }

        private static string TextOf(Dictionary<string, object> d)
        {
            foreach (var k in new[] { "reviewComments", "reviewText", "reviewBody", "comment", "comments", "text", "body", "content" })
            {
                string s = Get(d, k) as string;
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            return null;
        }

        private static string TitleOf(Dictionary<string, object> d)
        {
            foreach (var k in new[] { "reviewTitle", "title", "headline", "name" })
            {
                // "name" ของ schema.org Review = หัวข้อ (ใช้เฉพาะเมื่อเป็น @type Review)
                if (k == "name" && !string.Equals(Str(Get(d, "@type")), "Review", StringComparison.OrdinalIgnoreCase)) continue;
                string s = Get(d, k) as string;
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            return null;
        }

        private static decimal? RatingOf(Dictionary<string, object> d)
        {
            decimal? scale;
            return RatingOf(d, out scale);
        }

        private static decimal? RatingOf(Dictionary<string, object> d, out decimal? scale)
        {
            scale = null;
            foreach (var k in new[] { "rating", "reviewRating", "overallRating", "score", "reviewScore", "ratingValue", "formattedRating" })
            {
                object v = Get(d, k);
                if (v == null) continue;
                var vd = v as Dictionary<string, object>;
                if (vd != null)
                {
                    decimal? r = ReviewRating.Parse(Get(vd, "ratingValue")) ?? ReviewRating.Parse(Get(vd, "value"));
                    if (r.HasValue) { scale = ReviewRating.Parse(Get(vd, "bestRating")); return r; }
                    continue;
                }
                if (v is bool) continue;
                decimal? x = ReviewRating.Parse(v);
                if (x.HasValue) return x;
            }
            return null;
        }

        private static string OriginOf(Dictionary<string, object> d, Dictionary<string, string> providerMap, LightFetchDiagnostics diag)
        {
            string name = Str(Get(d, "providerName")) ?? Str(Get(d, "reviewProviderText")) ?? Str(Get(d, "provider") as string)
                       ?? Str(Get(d, "source") as string);
            if (!string.IsNullOrWhiteSpace(name)) return ReviewPlatforms.NormalizeOrigin(name);
            string pid = Str(Get(d, "providerId"));
            if (string.IsNullOrWhiteSpace(pid)) return "Agoda";
            string mapped;
            if (providerMap != null && providerMap.TryGetValue(pid.Trim(), out mapped)) return ReviewPlatforms.NormalizeOrigin(mapped);
            if (diag != null && !diag.UnknownProviders.Contains(pid)) diag.UnknownProviders.Add(pid);
            return "Agoda";
        }

        private static DateTime? DateOf(Dictionary<string, object> d)
        {
            foreach (var k in new[] { "reviewDate", "datePublished", "date", "reviewedDate", "formattedReviewDate", "reviewDateText" })
            {
                var r = ParseFlexibleDate(Get(d, k));
                if (r.HasValue) return r;
            }
            return null;
        }

        // ── วันที่หลายรูปแบบ (ISO, /Date(ms)/, ไทย, อังกฤษ) ─────────────────────

        private static readonly Dictionary<string, int> ThaiMonths = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "มกราคม", 1 }, { "ม.ค.", 1 }, { "กุมภาพันธ์", 2 }, { "ก.พ.", 2 }, { "มีนาคม", 3 }, { "มี.ค.", 3 },
            { "เมษายน", 4 }, { "เม.ย.", 4 }, { "พฤษภาคม", 5 }, { "พ.ค.", 5 }, { "มิถุนายน", 6 }, { "มิ.ย.", 6 },
            { "กรกฎาคม", 7 }, { "ก.ค.", 7 }, { "สิงหาคม", 8 }, { "ส.ค.", 8 }, { "กันยายน", 9 }, { "ก.ย.", 9 },
            { "ตุลาคม", 10 }, { "ต.ค.", 10 }, { "พฤศจิกายน", 11 }, { "พ.ย.", 11 }, { "ธันวาคม", 12 }, { "ธ.ค.", 12 }
        };

        public static DateTime? ParseFlexibleDate(object raw)
        {
            if (raw == null) return null;
            if (raw is DateTime) return ReviewText.SaneDate(((DateTime)raw).ToLocalTime());
            string s = raw.ToString().Trim();
            if (s.Length == 0 || s.Length > 80) return null;

            var ms = Regex.Match(s, @"^/?Date\((-?\d+)(?:[+-]\d{4})?\)/?$");
            if (ms.Success)
            {
                long v;
                if (long.TryParse(ms.Groups[1].Value, out v))
                    return ReviewText.SaneDate(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(v).ToLocalTime());
            }
            // ไทย: "18 ตุลาคม 2568", "รีวิวเมื่อ 18 ต.ค. 2568", "ต.ค. 2568" (เดือน-ปี)
            // หาชื่อเดือนในข้อความ (ชื่อเต็มก่อนตัวย่อ) — รองรับคำนำหน้าติดกัน เช่น "เมื่อเดือนตุลาคม 2026"; ปี ≥ 2400 = พ.ศ.
            if (Regex.IsMatch(s, @"[ก-๏]"))
                foreach (var kv in ThaiMonths.OrderByDescending(k => k.Key.Length))
                {
                    if (s.IndexOf(kv.Key, StringComparison.Ordinal) < 0) continue;
                    var th = Regex.Match(s, @"(?:(\d{1,2})\s*)?" + Regex.Escape(kv.Key) + @"\s*(\d{4})");
                    if (!th.Success) continue;
                    int mon = kv.Value;
                    int y = int.Parse(th.Groups[2].Value, CultureInfo.InvariantCulture);
                    if (y > 2400) y -= 543;
                    int day = th.Groups[1].Success ? int.Parse(th.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
                    try { return ReviewText.SaneDate(new DateTime(y, mon, Math.Max(1, Math.Min(day, DateTime.DaysInMonth(y, mon))))); }
                    catch { return null; }
                }
            var direct = ReviewText.ParseDate(s);
            if (direct.HasValue) return direct;
            // อังกฤษที่มีคำนำหน้า: "Reviewed October 25, 2023"
            var en = Regex.Match(s, @"([A-Za-z]{3,9}\.? \d{1,2},? \d{4}|\d{1,2} [A-Za-z]{3,9} \d{4}|[A-Za-z]{3,9} \d{4})");
            if (en.Success) return ReviewText.ParseDate(en.Value.Replace(".", "").Replace(" ,", ","));
            return null;
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static object Get(Dictionary<string, object> d, string k)
        {
            if (d == null) return null;
            object v;
            if (d.TryGetValue(k, out v)) return v;
            foreach (var kv in d) if (string.Equals(kv.Key, k, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }

        private static string Str(object v)
        {
            if (v == null) return null;
            if (v is Dictionary<string, object> || (v is IEnumerable && !(v is string))) return null;
            string s = Convert.ToString(v, CultureInfo.InvariantCulture);
            if (s == null) return null;
            s = s.Trim();
            return s.Length == 0 || s == "null" ? null : s;
        }

        private static IList AsList(object node)
        {
            if (node is string) return null;
            return node as IList;
        }

        /// <summary>ตัดข้อความสำหรับ preview (ไม่ escape — หน้าเว็บ escape เอง)</summary>
        public static string ClipPreview(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
