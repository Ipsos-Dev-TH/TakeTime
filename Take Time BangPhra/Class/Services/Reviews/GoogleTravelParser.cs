// ===========================================================================
// GoogleTravelParser.cs — ตัวแกะหน้ารีวิวโรงแรมบน Google Travel (GOOGLE_TRAVEL_LIGHT) — pure ไม่มี HTTP/DB
//
// หน้า: https://www.google.com/travel/hotels/entity/{token}/reviews?hl=th-TH&gl=th
// คลาสของ Google ถูก obfuscate และเปลี่ยนได้ทุก deploy ⇒ ทุก selector ตั้งได้ใน ApiConfig (ค่าเริ่ม = DOM จริงจากเบราว์เซอร์
// ของเจ้าของ ต.ค. 2026) และมีทางสำรองแบบโครงสร้าง (ลิงก์ /maps/contrib/ + "x/5" + ลิงก์รายงาน postId) เมื่อ selector ไม่เจอ
//   บล็อกรีวิว        div.Svr5cf
//   ชื่อผู้รีวิว      a.DHIhE  (ชื่อที่แสดงเท่านั้น — ไม่เก็บ contributor id / รูป: PDPA)
//   วันที่ + ที่มา    span.iUtr1 "4 เดือนที่แล้ว ใน Google" → วันโดยประมาณ (ApproxDate) + Origin_Platform
//   คะแนน             div.GDWaad "5/5" (สเกลจากตัวหาร)
//   ประเภททริป        div.ThUm5b span
//   ข้อความ           div[jsname=NwoMSd] .K7oBsc span (ฉบับขยาย) → div[jsname=kmPxT] .K7oBsc span → .K7oBsc span
//   คะแนนย่อย         div.dA5Vzb (span.uTU5Ac = หัวข้อ)   ไฮไลต์ span.iTppcd + span ถัดไป   หัวข้อย่อย div.NkS78 (span.Iw4tIc)
//   คำตอบเจ้าของ      div.lU7Ape → span.a0th8b (วันที่สัมพัทธ์), p.JOzlvd
//   รหัสรีวิว         postId ในลิงก์ "รายงานรีวิว" (local/content/rap/report?postId=…) — คงที่ต่อรีวิว
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Services.Reviews
{
    /// <summary>selector ที่ใช้ (ค่าเริ่ม = DOM ต.ค. 2026) — แทนได้ด้วย ApiConfig คีย์ sel*</summary>
    public class GoogleTravelSelectors
    {
        public string Block = "div.Svr5cf";
        public string Name = "a.DHIhE";
        public string Date = "span.iUtr1";
        public string Rating = "div.GDWaad";
        public string Trip = "div.ThUm5b span";
        public string TextExpanded = "div[jsname=NwoMSd] .K7oBsc span";
        public string TextCollapsed = "div[jsname=kmPxT] .K7oBsc span";
        public string Text = ".K7oBsc span";
        public string More = "span[role=button]";
        public string SubRating = "div.dA5Vzb";
        public string SubLabel = "span.uTU5Ac";
        public string HighlightLabel = "span.iTppcd";
        public string Topic = "div.NkS78";
        public string TopicLabel = "span.Iw4tIc";
        public string Response = "div.lU7Ape";
        public string ResponseDate = "span.a0th8b";
        public string ResponseText = "p.JOzlvd";

        public static readonly string[] Keys =
        {
            "selBlock", "selName", "selDate", "selRating", "selTrip", "selTextExpanded", "selTextCollapsed", "selText", "selMore",
            "selSubRating", "selSubLabel", "selHighlightLabel", "selTopic", "selTopicLabel", "selResponse", "selResponseDate", "selResponseText"
        };

        /// <summary>อ่านทับจาก ApiConfig (ค่าว่าง = ค่าเริ่ม)</summary>
        public static GoogleTravelSelectors From(Func<string, string> cfg)
        {
            var s = new GoogleTravelSelectors();
            if (cfg == null) return s;
            Func<string, string, string> g = (k, d) => { string v = cfg(k); return string.IsNullOrWhiteSpace(v) ? d : v.Trim(); };
            s.Block = g("selBlock", s.Block); s.Name = g("selName", s.Name); s.Date = g("selDate", s.Date);
            s.Rating = g("selRating", s.Rating); s.Trip = g("selTrip", s.Trip); s.TextExpanded = g("selTextExpanded", s.TextExpanded);
            s.TextCollapsed = g("selTextCollapsed", s.TextCollapsed); s.Text = g("selText", s.Text); s.More = g("selMore", s.More);
            s.SubRating = g("selSubRating", s.SubRating); s.SubLabel = g("selSubLabel", s.SubLabel);
            s.HighlightLabel = g("selHighlightLabel", s.HighlightLabel); s.Topic = g("selTopic", s.Topic);
            s.TopicLabel = g("selTopicLabel", s.TopicLabel); s.Response = g("selResponse", s.Response);
            s.ResponseDate = g("selResponseDate", s.ResponseDate); s.ResponseText = g("selResponseText", s.ResponseText);
            return s;
        }
    }

    public static class GoogleTravelParser
    {
        // ── 1) กันบอท ─────────────────────────────────────────────────────────

        /// <summary>คืนตัวบ่งชี้การโดนกัน (HTTP 429/403/503, /sorry/, consent, unusual traffic, captcha, body ว่าง) หรือ null</summary>
        public static string DetectBlock(int status, string body, string location)
        {
            if (status == 403 || status == 429 || status == 503) return "HTTP " + status;
            string loc = location ?? "";
            if (loc.IndexOf("/sorry/", StringComparison.OrdinalIgnoreCase) >= 0) return "redirect /sorry/";
            if (loc.IndexOf("consent.google.", StringComparison.OrdinalIgnoreCase) >= 0) return "redirect consent.google.com";
            string b = body ?? "";
            if (status >= 200 && status < 300 && b.Trim().Length < 200) return "empty-body";
            if (b.IndexOf("unusual traffic", StringComparison.OrdinalIgnoreCase) >= 0
                || b.IndexOf("การเข้าชมที่ผิดปกติ", StringComparison.Ordinal) >= 0) return "unusual traffic";
            if (b.IndexOf("/sorry/index", StringComparison.OrdinalIgnoreCase) >= 0) return "/sorry/";
            bool looksLikeReviews = b.IndexOf("/maps/contrib/", StringComparison.Ordinal) >= 0 || b.IndexOf("rap/report", StringComparison.Ordinal) >= 0;
            if (!looksLikeReviews)
            {
                if (b.IndexOf("consent.google.com", StringComparison.OrdinalIgnoreCase) >= 0) return "consent page";
                if (b.IndexOf("g-recaptcha", StringComparison.OrdinalIgnoreCase) >= 0
                    || b.IndexOf("captcha", StringComparison.OrdinalIgnoreCase) >= 0 && b.Length < 60000) return "captcha";
            }
            return null;
        }

        // ── 2) รหัสต่าง ๆ ของหน้า (diagnostics) ───────────────────────────────

        public static string EntityTokenFromUrl(string url)
        {
            var m = Regex.Match(url ?? "", @"/entity/([A-Za-z0-9_\-]{6,200})");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>Google place id (ChIJ…) + Knowledge-Graph token (base64url ที่ถอดแล้วมี /g/… หรือ /m/…)</summary>
        public static void FindIds(string html, LightFetchDiagnostics diag)
        {
            if (string.IsNullOrEmpty(html) || diag == null) return;
            string h = html.Length > 3000000 ? html.Substring(0, 3000000) : html;
            string dec = WebUtility.HtmlDecode(h);
            var pm = Regex.Match(dec, @"\b(ChIJ[A-Za-z0-9_\-]{15,80})");
            if (pm.Success) diag.PlaceId = pm.Groups[1].Value;
            foreach (Match m in Regex.Matches(dec, @"""(Ch[A-Za-z0-9_\-]{16,120})"""))
            {
                string tok = m.Groups[1].Value;
                if (tok.StartsWith("ChIJ", StringComparison.Ordinal)) continue;
                string mid = DecodeKgMid(tok);
                if (mid == null) continue;
                diag.KgToken = tok; diag.KgMid = mid;
                break;
            }
        }

        public static string DecodeKgMid(string token)
        {
            try
            {
                string b = token.Replace('-', '+').Replace('_', '/');
                while (b.Length % 4 != 0) b += "=";
                string s = Encoding.ASCII.GetString(Convert.FromBase64String(b));
                var m = Regex.Match(s, @"/[gm]/[0-9a-z_]{4,20}");
                return m.Success ? m.Value : null;
            }
            catch { return null; }
        }

        // ── 3) คะแนนรวมของหน้า ────────────────────────────────────────────────

        private static readonly Regex HeaderRatingRx = new Regex(
            @"(?<r>[1-5][.,]\d)(?:\s*(?:/\s*5|\(?\s*(?:เต็ม|จาก|out of)\s*5\s*\)?))?[^\d]{0,80}?(?<c>\d{1,3}(?:,\d{3})+|\d{1,6})\s*(?:รีวิว|reviews?|ความคิดเห็น)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex CountOnlyRx = new Regex(
            @"(?:รีวิว\s*(?<c>\d{1,3}(?:,\d{3})+|\d{1,6})\s*รายการ|(?<c>\d{1,3}(?:,\d{3})+|\d{1,6})\s*(?:รีวิว|reviews))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// คะแนนรวม /5 + จำนวนรีวิว: JSON-LD aggregateRating ก่อน → ข้อความหัวหน้า (ก่อนรีวิวแรก) ที่มี "4.8 … 123 รีวิว" ติดกัน
        /// ตั้งค่าเฉพาะเมื่อมั่นใจ — ค่าอื่นที่เจอใส่ diag.RatingCandidates
        /// </summary>
        public static bool FindOverallRating(string html, int firstBlockIndex, LightFetchDiagnostics diag, out decimal? rating, out int? count)
        {
            rating = null; count = null;
            decimal? best;
            if (AgodaPageParser.ParseJsonLdRating(html, diag, out rating, out best, out count, 5m))
            {
                if (best.HasValue && best.Value != 5m && rating.HasValue) rating = Math.Round(rating.Value / best.Value * 5m, 2);
                if (diag != null) { diag.Rating = rating; diag.RatingBest = 5m; diag.RatingSource = "json-ld"; }
                return true;
            }
            if (string.IsNullOrEmpty(html)) return false;
            string head = firstBlockIndex > 0 ? html.Substring(0, firstBlockIndex) : html;
            if (head.Length > 1500000) head = head.Substring(head.Length - 1500000);
            string text = ReviewText.HtmlToText(head);
            var m = HeaderRatingRx.Match(text);
            foreach (Match x in HeaderRatingRx.Matches(text).Cast<Match>().Take(5))
                if (diag != null) diag.RatingCandidates.Add("header:" + x.Groups["r"].Value + " / " + x.Groups["c"].Value + " รีวิว");
            foreach (Match x in CountOnlyRx.Matches(text).Cast<Match>().Take(5))
                if (diag != null) diag.RatingCandidates.Add("count:" + x.Groups["c"].Value);
            if (!m.Success) return false;
            decimal r;
            int c;
            if (!decimal.TryParse(m.Groups["r"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out r)) return false;
            if (!int.TryParse(m.Groups["c"].Value.Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out c) || c < 1) return false;
            rating = r; count = c;
            if (diag != null) { diag.Rating = r; diag.RatingBest = 5m; diag.RatingCount = c; diag.RatingSource = "header-text"; }
            return true;
        }

        // ── 4) รีวิว ──────────────────────────────────────────────────────────

        /// <summary>แกะรีวิวทั้งหมดในหน้า — path = "dom-selectors" | "dom-structural" | "none"</summary>
        public static List<ReviewItem> ParseReviews(string html, string pageUrl, DateTime now, GoogleTravelSelectors sel,
            LightFetchDiagnostics diag, out string path, out int firstBlockIndex)
        {
            path = "none";
            firstBlockIndex = -1;
            var items = new List<ReviewItem>();
            if (string.IsNullOrEmpty(html)) return items;
            sel = sel ?? new GoogleTravelSelectors();

            var whole = new HtmlLite.El { Start = 0, InnerStart = 0, InnerEnd = html.Length, End = html.Length, Tag = "" };
            var blocks = HtmlLite.SelectAll(html, whole, sel.Block);
            // บล็อกรีวิวไม่ซ้อนกันจริง — ตัดปลายแต่ละบล็อกไม่ให้เกินจุดเริ่มบล็อกถัดไป
            // (กัน HTML ที่ปิด tag ไม่ครบ ทำให้บล็อกหนึ่ง "กลืน" บล็อกถัดไป)
            for (int i = 0; i + 1 < blocks.Count; i++)
            {
                int next = blocks[i + 1].Start;
                if (blocks[i].End > next) { blocks[i].End = next; blocks[i].InnerEnd = Math.Min(blocks[i].InnerEnd, next); }
            }
            if (diag != null) diag.HtmlDomBlocks = blocks.Count;
            if (blocks.Count > 0)
            {
                firstBlockIndex = blocks[0].Start;
                foreach (var b in blocks)
                {
                    var it = ParseBlock(html, b, pageUrl, now, sel, diag);
                    if (it != null) items.Add(it);
                    if (items.Count >= 100) break;
                }
                if (items.Count > 0) path = "dom-selectors";
            }
            if (items.Count == 0)
            {
                var segs = StructuralBlocks(html);
                if (segs.Count > 0)
                {
                    if (firstBlockIndex < 0) firstBlockIndex = segs[0].Start;
                    foreach (var b in segs)
                    {
                        var it = ParseBlock(html, b, pageUrl, now, sel, diag);
                        if (it != null) items.Add(it);
                        if (items.Count >= 100) break;
                    }
                    if (items.Count > 0) path = "dom-structural";
                }
            }
            if (diag != null) diag.HtmlDomReviews = items.Count;

            // ข้อมูลที่ฝังใน <script> (AF_initDataCallback) — เมื่อ DOM รีวิวไม่ได้ render มากับ HTML
            var af = GoogleEmbeddedData.ExtractBlocks(html);
            GoogleEmbeddedData.RawStats(html, af, diag);
            if (items.Count == 0 && af.Count > 0)
            {
                items = GoogleEmbeddedData.ExtractReviews(af, pageUrl, now, diag);
                if (items.Count > 0) path = "af-initdata";
            }
            if (diag != null) diag.IdsFound = items.Count(i => i.ExternalReviewId != null);
            return items;
        }

        /// <summary>ทางสำรอง: แบ่งช่วงด้วยลิงก์ชื่อผู้รีวิว (/maps/contrib/{id} ที่มีข้อความ) — ลิงก์รูปกับชื่อของคนเดียวกันรวมเป็นช่วงเดียว</summary>
        private static List<HtmlLite.El> StructuralBlocks(string html)
        {
            var list = new List<HtmlLite.El>();
            var rx = new Regex(@"<a\b[^>]*href\s*=\s*[""'][^""']*/maps/contrib/(\d+)[^""']*[""'][^>]*>([^<]{1,200})</a>", RegexOptions.IgnoreCase);
            var ms = rx.Matches(html).Cast<Match>().Where(m => m.Groups[2].Value.Trim().Length > 0).ToList();
            for (int i = 0; i < ms.Count; i++)
            {
                int start = ms[i].Index;
                // ย้อนไปรวมลิงก์รูปของคนเดียวกันที่อยู่ก่อนหน้าไม่ไกล
                int avatar = html.LastIndexOf("/maps/contrib/" + ms[i].Groups[1].Value, start, Math.Min(start, 1500), StringComparison.Ordinal);
                if (avatar >= 0) { int a = html.LastIndexOf("<a", avatar, StringComparison.OrdinalIgnoreCase); if (a >= 0 && start - a < 1500) start = a; }
                int end = i + 1 < ms.Count ? ms[i + 1].Index : Math.Min(html.Length, ms[i].Index + 60000);
                if (i + 1 < ms.Count)
                {
                    int nextAvatar = html.LastIndexOf("/maps/contrib/" + ms[i + 1].Groups[1].Value, end, Math.Min(end - ms[i].Index, 1500), StringComparison.Ordinal);
                    if (nextAvatar > ms[i].Index) { int a = html.LastIndexOf("<a", nextAvatar, StringComparison.OrdinalIgnoreCase); if (a > ms[i].Index) end = a; }
                }
                list.Add(new HtmlLite.El { Start = start, InnerStart = start, InnerEnd = end, End = end, Tag = "" });
            }
            return list;
        }

        private static readonly Regex RatingRx = new Regex(@"(\d{1,2}(?:[.,]\d{1,2})?)\s*/\s*(\d{1,3})", RegexOptions.Compiled);

        private static ReviewItem ParseBlock(string html, HtmlLite.El b, string pageUrl, DateTime now, GoogleTravelSelectors sel, LightFetchDiagnostics diag)
        {
            // ส่วนคำตอบเจ้าของ/รูป ไม่ใช่เนื้อรีวิว — ตัดออกจากการหาข้อความสำรอง
            var resp = HtmlLite.SelectFirst(html, b, sel.Response);

            string name = HtmlLite.Text(html, HtmlLite.SelectFirst(html, b, sel.Name), null);
            if (string.IsNullOrWhiteSpace(name))
            {
                var nm = Regex.Match(Slice(html, b), @"<a\b[^>]*/maps/contrib/[^>]*>([^<]{1,200})</a>", RegexOptions.IgnoreCase);
                if (nm.Success) name = WebUtility.HtmlDecode(nm.Groups[1].Value).Trim();
            }

            string dateLine = HtmlLite.Text(html, HtmlLite.SelectFirst(html, b, sel.Date), null);
            if (string.IsNullOrWhiteSpace(dateLine))
            {
                var dm = RelativeRx.Match(HtmlLite.Text(html, b, null) ?? "");
                if (dm.Success) dateLine = dm.Value;
            }
            string relative = dateLine, origin = "Google";
            if (!string.IsNullOrEmpty(dateLine))
            {
                var om = Regex.Match(dateLine, @"^(.*?)\s+(?:ใน|on)\s+(.+)$");
                if (om.Success) { relative = om.Groups[1].Value.Trim(); origin = ReviewPlatforms.NormalizeOrigin(om.Groups[2].Value.Trim()) ?? "Google"; }
            }
            DateTime? approx = ParseRelativeDate(relative, now);

            decimal? raw = null, scale = null;
            string rt = HtmlLite.Text(html, HtmlLite.SelectFirst(html, b, sel.Rating), null);
            var rm = RatingRx.Match(rt ?? "");
            if (!rm.Success)
            {
                string bodyNoResp = resp == null ? Slice(html, b) : html.Substring(b.Start, resp.Start - b.Start);
                rm = Regex.Match(bodyNoResp, @">\s*(\d(?:[.,]\d)?)\s*/\s*(5)\s*<");
            }
            if (rm.Success)
            {
                raw = ReviewRating.Parse(rm.Groups[1].Value);
                scale = ReviewRating.Parse(rm.Groups[2].Value);
                if (!ReviewRating.IsValid(raw, scale)) { raw = null; scale = null; }
            }

            string trip = HtmlLite.Text(html, HtmlLite.SelectFirst(html, b, sel.Trip), sel.More);

            string text = null;
            foreach (var chain in new[] { sel.TextExpanded, sel.TextCollapsed, sel.Text })
            {
                var el = HtmlLite.SelectFirst(html, b, chain);
                if (el == null || (resp != null && el.Start >= resp.Start && el.Start < resp.End)) continue;
                text = CleanReviewText(HtmlLite.Text(html, el, sel.More, true));
                if (!string.IsNullOrWhiteSpace(text)) break;
            }

            // คะแนนย่อย / ไฮไลต์ / หัวข้อย่อย
            var subs = new Dictionary<string, object>();
            foreach (var sr in HtmlLite.SelectAll(html, b, sel.SubRating))
            {
                string label = HtmlLite.Text(html, HtmlLite.SelectFirst(html, sr, sel.SubLabel), null);
                string all = HtmlLite.Text(html, sr, null) ?? "";
                if (string.IsNullOrWhiteSpace(label)) continue;
                string rest = all.Replace(label, "").Trim();
                decimal? v = ReviewRating.Parse(rest);
                if (v.HasValue && !subs.ContainsKey(label)) subs[label] = v.Value;
            }
            var highlights = new List<string>();
            var hl = HtmlLite.SelectFirst(html, b, sel.HighlightLabel);
            if (hl != null)
            {
                var after = new HtmlLite.El { Start = hl.End, InnerStart = hl.End, InnerEnd = b.InnerEnd, End = b.End, Tag = "" };
                var sib = HtmlLite.SelectFirst(html, after, "span");
                string ht = HtmlLite.Text(html, sib, sel.More);
                if (!string.IsNullOrWhiteSpace(ht))
                    highlights.AddRange(ht.TrimEnd('…', '.', ' ').Split(new[] { '·', '•' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim()).Where(x => x.Length > 0 && x.Length <= 60).Take(15));
            }
            var topics = new Dictionary<string, object>();
            foreach (var tp in HtmlLite.SelectAll(html, b, sel.Topic))
            {
                foreach (var lab in HtmlLite.SelectAll(html, tp, sel.TopicLabel))
                {
                    string label = HtmlLite.Text(html, lab, null);
                    var after = new HtmlLite.El { Start = lab.End, InnerStart = lab.End, InnerEnd = tp.InnerEnd, End = tp.End, Tag = "" };
                    string note = HtmlLite.Text(html, HtmlLite.SelectFirst(html, after, "span"), sel.More, true);
                    if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(note) && !topics.ContainsKey(label))
                        topics[label] = ReviewText.Clip(note, 1000);
                }
            }

            // คำตอบของเจ้าของ
            string respText = null, respRel = null;
            DateTime? respDate = null;
            if (resp != null)
            {
                respText = HtmlLite.Text(html, HtmlLite.SelectFirst(html, resp, sel.ResponseText), sel.More, true);
                respRel = HtmlLite.Text(html, HtmlLite.SelectFirst(html, resp, sel.ResponseDate), null);
                respDate = ParseRelativeDate(respRel, now);
            }
            else
            {
                string seg = Slice(html, b);
                int k = IndexOfAny(seg, "คำตอบจากเจ้าของ", "Response from the owner");
                if (k >= 0)
                {
                    var pm = Regex.Match(seg.Substring(k), @"<p\b[^>]*>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (pm.Success) respText = CleanReviewText(HtmlLite.StripToText(pm.Groups[1].Value, true));
                    var rd = RelativeRx.Match(HtmlLite.StripToText(seg.Substring(k, Math.Min(400, seg.Length - k)), false));
                    if (rd.Success) { respRel = rd.Value; respDate = ParseRelativeDate(respRel, now); }
                }
            }

            // ข้อความสำรอง: span ที่ยาวที่สุดนอกส่วนคำตอบเจ้าของ
            if (string.IsNullOrWhiteSpace(text))
            {
                string seg = resp == null ? Slice(html, b) : html.Substring(b.Start, resp.Start - b.Start);
                string bestSpan = null;
                foreach (Match sm in Regex.Matches(seg, @"<span\b[^>]*>([^<]{20,})", RegexOptions.IgnoreCase))
                {
                    string t = WebUtility.HtmlDecode(sm.Groups[1].Value).Trim();
                    if (t == name || t == dateLine) continue;
                    if (bestSpan == null || t.Length > bestSpan.Length) bestSpan = t;
                }
                text = CleanReviewText(bestSpan);
            }

            var pid = Regex.Match(WebUtility.HtmlDecode(Slice(html, b)), @"rap/report\?(?:[^""'\s>]*&)?postId=([A-Za-z0-9_\-%=]{8,300})");
            string postId = pid.Success ? Uri.UnescapeDataString(pid.Groups[1].Value) : null;

            if (string.IsNullOrWhiteSpace(text) && !raw.HasValue) return null;   // ไม่ใช่รีวิว

            string blockText = HtmlLite.Text(html, b, null) ?? "";
            bool machine = blockText.IndexOf("แปลโดย Google", StringComparison.Ordinal) >= 0
                        || blockText.IndexOf("Translated by Google", StringComparison.OrdinalIgnoreCase) >= 0;

            var meta = new Dictionary<string, object> { { "approxDate", approx.HasValue }, { "relativeDate", relative } };
            if (subs.Count > 0) meta["subRatings"] = subs;
            if (highlights.Count > 0) meta["highlights"] = highlights;
            if (topics.Count > 0) meta["topics"] = topics;
            if (!string.IsNullOrEmpty(respRel)) meta["ownerResponseRelative"] = respRel;
            if (machine) meta["machineTranslated"] = true;

            return new ReviewItem
            {
                ExternalReviewId = postId == null ? null : "gpost:" + postId,
                OriginPlatform = origin,
                ReviewerName = ReviewText.CleanName(name),
                RatingRaw = raw,
                RatingScale = raw.HasValue ? scale : null,
                Text = string.IsNullOrWhiteSpace(text) ? null : text,
                ReviewDate = approx,
                ApproxDate = true,
                Language = AgodaPageParser.DetectLang(text),
                MachineTranslated = machine,
                IsOriginalText = !machine,
                TravellerType = string.IsNullOrWhiteSpace(trip) ? null : trip.Trim(),
                OwnerResponse = string.IsNullOrWhiteSpace(respText) ? null : respText,
                OwnerResponseDate = respDate,
                AllowOwnerResponseUpdate = true,
                ReviewUrl = pageUrl,
                InsertOnly = true,
                Meta = meta
            };
        }

        /// <summary>ตัด "&nbsp;…" ท้ายข้อความย่อ / ปุ่ม "อ่านเพิ่มเติม" ที่หลุดมา</summary>
        public static string CleanReviewText(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            string t = s.Replace(' ', ' ');
            t = Regex.Replace(t, @"\s*…?\s*(อ่านเพิ่มเติม|Read more|More)\s*$", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"[ \t]*…\s*$", "");
            t = Regex.Replace(t, @"[ \t]+\n", "\n").Trim();
            return t.Length == 0 ? null : t;
        }

        // ── วันที่สัมพัทธ์ ────────────────────────────────────────────────────

        private static readonly Regex RelativeRx = new Regex(
            @"(?:(\d+|หนึ่ง|a|an|one)\s*)?(วินาที|นาที|ชั่วโมง|วัน|สัปดาห์|อาทิตย์|เดือน|ปี|seconds?|minutes?|hours?|days?|weeks?|months?|years?)\s*(?:ที่แล้ว|ago)|เมื่อวาน(?:นี้)?|yesterday",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>"4 เดือนที่แล้ว" / "a month ago" / "เมื่อวาน" → วันโดยประมาณ (เทียบ now) หรือ null</summary>
        public static DateTime? ParseRelativeDate(string s, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var m = RelativeRx.Match(s);
            if (!m.Success) return null;
            if (m.Value.StartsWith("เมื่อวาน", StringComparison.Ordinal) || m.Value.Equals("yesterday", StringComparison.OrdinalIgnoreCase))
                return now.Date.AddDays(-1);
            int n = 1;
            string num = m.Groups[1].Value;
            if (num.Length > 0 && char.IsDigit(num[0])) int.TryParse(num, out n);
            if (n < 0 || n > 100) return null;
            string u = m.Groups[2].Value.ToLowerInvariant();
            if (u.StartsWith("วินาที") || u.StartsWith("นาที") || u.StartsWith("ชั่วโมง") || u.StartsWith("second") || u.StartsWith("minute") || u.StartsWith("hour")) return now.Date;
            if (u == "วัน" || u.StartsWith("day")) return now.Date.AddDays(-n);
            if (u == "สัปดาห์" || u == "อาทิตย์" || u.StartsWith("week")) return now.Date.AddDays(-7 * n);
            if (u == "เดือน" || u.StartsWith("month")) return now.Date.AddMonths(-n);
            if (u == "ปี" || u.StartsWith("year")) return now.Date.AddYears(-n);
            return null;
        }

        private static string Slice(string html, HtmlLite.El e) { return html.Substring(e.Start, Math.Max(0, e.End - e.Start)); }

        private static int IndexOfAny(string s, params string[] keys)
        {
            foreach (var k in keys) { int i = s.IndexOf(k, StringComparison.OrdinalIgnoreCase); if (i >= 0) return i; }
            return -1;
        }
    }

    /// <summary>
    /// ตัวเลือก element แบบเบา (ไม่พึ่งไลบรารี): simple selector = tag / .class / [attr=value] รวมกันได้ (div.a.b[jsname=x]),
    /// ต่อกันด้วยช่องว่าง = ลูกหลาน, คั่น "," = ทางเลือก — หา tag ปิดด้วยการนับ tag ชื่อเดียวกันที่ซ้อน
    /// </summary>
    public static class HtmlLite
    {
        public class El
        {
            public int Start, InnerStart, InnerEnd, End;
            public string Tag;
        }

        private class Simple
        {
            public string Tag;
            public List<string> Classes = new List<string>();
            public string Attr, Value;
        }

        private static readonly Regex StartTagRx = new Regex(@"<([a-zA-Z][a-zA-Z0-9\-]*)\b([^>]*)>", RegexOptions.Compiled);
        private static readonly HashSet<string> Void = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "img", "br", "hr", "input", "meta", "link", "source", "wbr", "area", "base", "col", "embed", "param", "track" };

        private static Simple Parse(string s)
        {
            var m = Regex.Match(s.Trim(), @"^([a-zA-Z][a-zA-Z0-9\-]*)?((?:\.[A-Za-z0-9_\-]+)*)(?:\[([A-Za-z0-9_\-]+)(?:\s*=\s*[""']?([^\]""']*)[""']?)?\])?$");
            if (!m.Success) return null;
            var r = new Simple { Tag = m.Groups[1].Success && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : null };
            foreach (var c in m.Groups[2].Value.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)) r.Classes.Add(c);
            if (m.Groups[3].Success && m.Groups[3].Value.Length > 0) { r.Attr = m.Groups[3].Value; r.Value = m.Groups[4].Success ? m.Groups[4].Value : null; }
            return r;
        }

        private static string AttrVal(string attrs, string name)
        {
            var m = Regex.Match(attrs, @"(?:^|\s)" + Regex.Escape(name) + @"\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase);
            if (!m.Success) return Regex.IsMatch(attrs, @"(?:^|\s)" + Regex.Escape(name) + @"(?:\s|$)", RegexOptions.IgnoreCase) ? "" : null;
            return m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
        }

        private static bool Matches(Simple s, string tag, string attrs)
        {
            if (s.Tag != null && !s.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase)) return false;
            if (s.Classes.Count > 0)
            {
                string cls = AttrVal(attrs, "class");
                if (cls == null) return false;
                var tokens = cls.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var c in s.Classes) if (!tokens.Contains(c)) return false;
            }
            if (s.Attr != null)
            {
                string v = AttrVal(attrs, s.Attr);
                if (v == null) return false;
                if (s.Value != null && v != s.Value) return false;
            }
            return true;
        }

        /// <summary>ตำแหน่งเริ่มของ tag ปิดที่คู่กัน (นับ tag ชื่อเดียวกันที่ซ้อน) — ไม่พบคืน limit</summary>
        private static int CloseOf(string html, string tag, int from, int limit)
        {
            var rx = new Regex(@"<(/?)" + Regex.Escape(tag) + @"\b[^>]*?(/?)>", RegexOptions.IgnoreCase);
            int depth = 1;
            var m = rx.Match(html, from);
            while (m.Success && m.Index < limit)
            {
                if (m.Groups[1].Value == "/") { depth--; if (depth == 0) return m.Index; }
                else if (m.Groups[2].Value != "/") depth++;
                m = m.NextMatch();
            }
            return limit;
        }

        private static IEnumerable<El> FindAll(string html, El scope, Simple s)
        {
            var m = StartTagRx.Match(html, scope.InnerStart);
            while (m.Success && m.Index < scope.InnerEnd)
            {
                string tag = m.Groups[1].Value;
                if (Matches(s, tag, m.Groups[2].Value))
                {
                    int innerStart = m.Index + m.Length;
                    bool selfClosing = Void.Contains(tag) || m.Value.EndsWith("/>");
                    int close = selfClosing ? innerStart : CloseOf(html, tag, innerStart, scope.InnerEnd);
                    int end = selfClosing ? innerStart : Math.Min(scope.InnerEnd, html.IndexOf('>', close) < 0 ? close : html.IndexOf('>', close) + 1);
                    yield return new El { Start = m.Index, InnerStart = innerStart, InnerEnd = close, End = Math.Max(end, close), Tag = tag };
                }
                m = m.NextMatch();
            }
        }

        private static List<List<Simple>> ParseChains(string selector)
        {
            var chains = new List<List<Simple>>();
            if (string.IsNullOrWhiteSpace(selector)) return chains;
            foreach (var alt in selector.Split(','))
            {
                var parts = alt.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(Parse).ToList();
                if (parts.Count > 0 && parts.All(p => p != null)) chains.Add(parts);
            }
            return chains;
        }

        public static El SelectFirst(string html, El scope, string selector)
        {
            if (scope == null || string.IsNullOrEmpty(html)) return null;
            foreach (var chain in ParseChains(selector))
            {
                var r = First(html, scope, chain, 0);
                if (r != null) return r;
            }
            return null;
        }

        private static El First(string html, El scope, List<Simple> chain, int i)
        {
            int tries = 0;
            foreach (var e in FindAll(html, scope, chain[i]))
            {
                if (i == chain.Count - 1) return e;
                var r = First(html, e, chain, i + 1);
                if (r != null) return r;
                if (++tries > 200) break;
            }
            return null;
        }

        public static List<El> SelectAll(string html, El scope, string selector)
        {
            var list = new List<El>();
            if (scope == null || string.IsNullOrEmpty(html)) return list;
            foreach (var chain in ParseChains(selector))
            {
                Collect(html, scope, chain, 0, list);
                if (list.Count > 0) break;
            }
            return list.GroupBy(e => e.Start).Select(g => g.First()).OrderBy(e => e.Start).ToList();
        }

        private static void Collect(string html, El scope, List<Simple> chain, int i, List<El> list)
        {
            foreach (var e in FindAll(html, scope, chain[i]))
            {
                if (list.Count > 500) return;
                if (i == chain.Count - 1) list.Add(e);
                else Collect(html, e, chain, i + 1, list);
            }
        }

        /// <summary>ข้อความของ element: ตัด element ที่ตรง removeSelector (ปุ่ม "อ่านเพิ่มเติม"), &lt;br&gt; → \n, ถอด entity</summary>
        public static string Text(string html, El e, string removeSelector, bool keepNewlines = false)
        {
            if (e == null) return null;
            string inner = html.Substring(e.InnerStart, Math.Max(0, e.InnerEnd - e.InnerStart));
            if (!string.IsNullOrEmpty(removeSelector))
            {
                var whole = new El { Start = 0, InnerStart = 0, InnerEnd = inner.Length, End = inner.Length, Tag = "" };
                var rm = SelectAll(inner, whole, removeSelector);
                for (int k = rm.Count - 1; k >= 0; k--)
                {
                    var r = rm[k];
                    if (rm.Any(o => o != r && o.Start < r.Start && o.End >= r.End)) continue;
                    inner = inner.Substring(0, r.Start) + inner.Substring(Math.Min(inner.Length, r.End));
                }
            }
            string t = StripToText(inner, keepNewlines);
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }

        public static string StripToText(string s, bool keepNewlines)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = Regex.Replace(s, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"<[^>]+>", keepNewlines ? "" : " ");
            t = WebUtility.HtmlDecode(t).Replace(' ', ' ');
            if (keepNewlines)
            {
                t = t.Replace("\r\n", "\n");
                t = Regex.Replace(t, @"[ \t]+\n", "\n");
                t = Regex.Replace(t, @"[ \t]{2,}", " ");
                return t.Trim();
            }
            return Regex.Replace(t, @"\s+", " ").Trim();
        }
    }
}
