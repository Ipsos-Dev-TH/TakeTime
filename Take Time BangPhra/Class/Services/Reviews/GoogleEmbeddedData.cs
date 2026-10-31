// ===========================================================================
// GoogleEmbeddedData.cs — แกะรีวิวจากข้อมูลที่ Google ฝังใน <script> (AF_initDataCallback({key:'ds:N', data:[…]}))
//
// ใช้เมื่อหน้า Google Travel ไม่ได้ render DOM รีวิวมากับ HTML ฝั่งเซิร์ฟเวอร์ (ผลจริงบนเซิร์ฟเวอร์ ต.ค. 2026)
// โครงสร้าง array ของ Google ไม่มีชื่อฟิลด์และเปลี่ยนได้ ⇒ heuristic:
//   • node (array) ที่ทั้งต้นไม้ย่อยมี "ผู้รีวิว" อยู่คนเดียว (URL /maps/contrib/<digits> หรือรหัส 21 หลัก)
//     + คะแนน 1–5 ("x/5" หรือจำนวนเต็ม) + ข้อความ ≥ 15 ตัวอักษร ⇒ หนึ่งรีวิว (เลือก node นอกสุดที่ยังมีผู้รีวิวคนเดียว)
//   • postId (base64 ขึ้นต้น Ci9DQUlRQUNv / ChZDSUhN / ChdDSUhN), วันที่สัมพัทธ์ หรือ timestamp (วินาที/มิลลิ/ไมโคร),
//     คำตอบเจ้าของ (ถัดจาก "คำตอบจากเจ้าของ" / "Response from the owner"), ประเภททริป (มี ❘)
//   • ข้อความไทย + ไม่ไทยคู่กัน ⇒ ถือไทยเป็นต้นฉบับ อีกตัวเป็นคำแปล (ไม่บันทึก)
// คะแนนจากจำนวนเต็มเป็นการเดา (Platform_Meta.ratingGuess = true) — ตรวจกับ preview ในปุ่มทดสอบ
// ===========================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Services.Reviews
{
    public static class GoogleEmbeddedData
    {
        public class Block
        {
            public string Key;
            public int Chars;
            public object Data;   // null = แปลงไม่ได้
        }

        private static readonly Regex KeyRx = new Regex(@"key\s*:\s*['""]([^'""]{1,40})['""]", RegexOptions.Compiled);
        private static readonly Regex DataRx = new Regex(@"\bdata\s*:\s*(?:function\s*\(\s*\)\s*\{\s*return\s*)?\[", RegexOptions.Compiled);
        private static readonly Regex PostIdRx = new Regex(@"^(?:Ci9DQUlRQUNv|ChZDSUhN|ChdDSUhN)[A-Za-z0-9_\-]{8,}={0,2}$", RegexOptions.Compiled);
        private static readonly Regex ContribRx = new Regex(@"/maps/contrib/(\d{6,30})", RegexOptions.Compiled);
        private static readonly Regex ContribIdRx = new Regex(@"^\d{21}$", RegexOptions.Compiled);
        private static readonly Regex TokenLikeRx = new Regex(@"^[A-Za-z0-9_\-+/=]{15,}$", RegexOptions.Compiled);

        /// <summary>หา AF_initDataCallback ทุกตัว → key + ขนาด + data ที่แปลงแล้ว (ตัวที่แปลงไม่ได้ Data = null)</summary>
        public static List<Block> ExtractBlocks(string html, int maxBlocks = 80)
        {
            var list = new List<Block>();
            if (string.IsNullOrEmpty(html)) return list;
            int idx = 0;
            while (list.Count < maxBlocks && (idx = html.IndexOf("AF_initDataCallback(", idx, StringComparison.Ordinal)) >= 0)
            {
                int callStart = idx;
                idx += 20;
                int window = Math.Min(html.Length - idx, 400);
                string head = html.Substring(idx, window);
                var km = KeyRx.Match(head);
                var dm = DataRx.Match(html, idx, Math.Min(html.Length - idx, 4000));
                if (!dm.Success) continue;
                int arrStart = dm.Index + dm.Length - 1;
                int arrEnd = AgodaPageParser.MatchBalanced(html, arrStart);
                var b = new Block { Key = km.Success && km.Index < dm.Index - idx ? km.Groups[1].Value : "?" };
                if (arrEnd < 0) { b.Chars = 0; list.Add(b); continue; }
                string json = html.Substring(arrStart, arrEnd - arrStart + 1);
                b.Chars = json.Length;
                b.Data = TryParse(json);
                list.Add(b);
                idx = arrEnd;
            }
            return list;
        }

        /// <summary>แปลง JS array literal ของ Google: \xNN → \u00NN, ช่องว่างใน array ([,1] / ,, / ,]) → null</summary>
        public static object TryParse(string js)
        {
            if (string.IsNullOrEmpty(js)) return null;
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 2000 };
            try { return ser.DeserializeObject(js); } catch { }
            try { return ser.DeserializeObject(FixJs(js)); } catch { return null; }
        }

        public static string FixJs(string s)
        {
            var sb = new StringBuilder(s.Length + 64);
            bool inStr = false;
            char quote = '"';
            char prevSig = '\0';   // อักขระสำคัญล่าสุดนอกสตริง
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr)
                {
                    if (c == '\\' && i + 1 < s.Length)
                    {
                        char n = s[i + 1];
                        if (n == 'x' && i + 3 < s.Length) { sb.Append("\\u00").Append(s, i + 2, 2); i += 3; continue; }
                        if (n == '\'' ) { sb.Append('\''); i++; continue; }
                        sb.Append(c).Append(n); i++; continue;
                    }
                    if (c == quote) { inStr = false; sb.Append('"'); prevSig = '"'; continue; }
                    if (c == '"' && quote == '\'') { sb.Append("\\\""); continue; }
                    sb.Append(c);
                    continue;
                }
                if (c == '"' || c == '\'') { inStr = true; quote = c; sb.Append('"'); continue; }
                if (char.IsWhiteSpace(c)) { sb.Append(c); continue; }
                if ((c == ',' || c == ']') && (prevSig == '[' || prevSig == ',')) sb.Append("null");
                sb.Append(c);
                prevSig = c;
            }
            return sb.ToString();
        }

        // ── สถิติต่อ node (คำนวณครั้งเดียว — อ้างอิงตาม reference) ──────────────────

        private class Stats { public int Contrib; public int Size; }

        private sealed class RefEq : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object o) { return RuntimeHelpers.GetHashCode(o); }
        }

        private static Stats Count(object node, Dictionary<object, Stats> memo, int depth)
        {
            var s = new Stats();
            if (node == null || depth > 400) return s;
            string str = node as string;
            if (str != null) { if (IsContrib(str)) s.Contrib = 1; s.Size = 1; return s; }
            var list = AsList(node);
            if (list == null)
            {
                var d = node as IDictionary<string, object>;
                if (d == null) { s.Size = 1; return s; }
                list = d.Values.ToList();
            }
            Stats cached;
            if (memo.TryGetValue(node, out cached)) return cached;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ch in list)
            {
                var cs = Count(ch, memo, depth + 1);
                s.Size += cs.Size;
                s.Contrib += cs.Contrib;
            }
            // contributor id เดียวกันที่ซ้ำ (URL + รหัส) นับเป็นคนเดียว
            if (s.Contrib > 1)
            {
                CollectContribIds(node, seen, 0);
                if (seen.Count == 1) s.Contrib = 1;
            }
            memo[node] = s;
            return s;
        }

        private static void CollectContribIds(object node, HashSet<string> ids, int depth)
        {
            if (node == null || depth > 60 || ids.Count > 3) return;
            string str = node as string;
            if (str != null)
            {
                var m = ContribRx.Match(str);
                if (m.Success) ids.Add(m.Groups[1].Value);
                else if (ContribIdRx.IsMatch(str)) ids.Add(str);
                return;
            }
            var list = AsList(node);
            if (list != null) foreach (var ch in list) CollectContribIds(ch, ids, depth + 1);
        }

        private static bool IsContrib(string s)
        {
            return s.IndexOf("/maps/contrib/", StringComparison.Ordinal) >= 0 || ContribIdRx.IsMatch(s);
        }

        private static IList AsList(object o)
        {
            if (o is string) return null;
            return o as IList;
        }

        // ── หาและแปลงรีวิว ─────────────────────────────────────────────────────

        public static List<ReviewItem> ExtractReviews(List<Block> blocks, string pageUrl, DateTime now, LightFetchDiagnostics diag)
        {
            var items = new List<ReviewItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in blocks)
            {
                if (b.Data == null) continue;
                var memo = new Dictionary<object, Stats>(new RefEq());
                var found = new List<IList>();
                Find(b.Data, memo, found, 0);
                foreach (var node in found)
                {
                    var it = ToReview(node, pageUrl, now);
                    if (it == null) continue;
                    string key = it.ExternalReviewId ?? ((it.ReviewerName ?? "") + "|" + ReviewText.NormalizeForHash(it.Text));
                    if (!seen.Add(key)) continue;
                    items.Add(it);
                    if (items.Count >= 100) return items;
                }
            }
            return items;
        }

        /// <summary>เดินหา node นอกสุดที่มีผู้รีวิวคนเดียว + ข้อความ + คะแนน</summary>
        private static void Find(object node, Dictionary<object, Stats> memo, List<IList> found, int depth)
        {
            if (depth > 300 || found.Count > 300) return;
            var list = AsList(node);
            if (list == null)
            {
                var d = node as IDictionary<string, object>;
                if (d != null) foreach (var v in d.Values) Find(v, memo, found, depth + 1);
                return;
            }
            var st = Count(list, memo, 0);
            if (st.Contrib == 0) return;
            if (st.Contrib == 1 && st.Size <= 4000)
            {
                var f = Features(list);
                if (f.Texts.Count > 0 && (f.RatingSlash.HasValue || f.Ints.Count > 0)) { found.Add(list); return; }
                // มีผู้รีวิวแต่ไม่ครบ — อาจเป็นแค่ข้อมูลผู้ใช้; ไม่ต้องลงลึกต่อ (ลูกก็มีผู้รีวิวคนเดียวเหมือนกันแต่ข้อมูลน้อยกว่า)
                return;
            }
            foreach (var ch in list) Find(ch, memo, found, depth + 1);
        }

        private class Feat
        {
            public string Name, ContribUrl, PostId, Relative, TripType, Origin;
            public decimal? RatingSlash, RatingSlashScale;
            public List<KeyValuePair<int, long>> Ints = new List<KeyValuePair<int, long>>();   // (depth, value) 1..5
            public List<long> Timestamps = new List<long>();
            public List<KeyValuePair<int, string>> Texts = new List<KeyValuePair<int, string>>(); // (order, text)
            public string ResponseText, ResponseRelative;
            public bool Translated;
            public int Order;
        }

        private static readonly string[] OriginNames = { "Tripadvisor", "Booking.com", "Trip.com", "Agoda", "Expedia", "Hotels.com", "Priceline", "Traveloka", "Google" };

        private static Feat Features(IList node)
        {
            var f = new Feat();
            Walk(node, f, 0, null, false);
            return f;
        }

        private static void Walk(object node, Feat f, int depth, IList parent, bool afterResponseLabel)
        {
            if (node == null || depth > 12) return;
            f.Order++;
            string s = node as string;
            if (s != null)
            {
                string t = s.Trim();
                if (t.Length == 0) return;
                if (IsContrib(t))
                {
                    if (f.ContribUrl == null) f.ContribUrl = t;
                    if (f.Name == null && parent != null)
                        foreach (var sib in parent)
                        {
                            string ss = sib as string;
                            if (ss != null && IsName(ss)) { f.Name = ss.Trim(); break; }
                        }
                    return;
                }
                if (PostIdRx.IsMatch(t)) { if (f.PostId == null) f.PostId = t; return; }
                if (t.Equals("คำตอบจากเจ้าของ", StringComparison.Ordinal) || t.StartsWith("Response from the owner", StringComparison.OrdinalIgnoreCase)) return;
                var sm = Regex.Match(t, @"^(\d(?:[.,]\d)?)\s*/\s*(5|10)$");
                if (sm.Success) { if (!f.RatingSlash.HasValue) { f.RatingSlash = ReviewRating.Parse(sm.Groups[1].Value); f.RatingSlashScale = ReviewRating.Parse(sm.Groups[2].Value); } return; }
                if (t.Length <= 40 && GoogleTravelParser.ParseRelativeDate(t, DateTime.Now).HasValue && !t.Contains("\n"))
                {
                    if (afterResponseLabel) { if (f.ResponseRelative == null) f.ResponseRelative = t; }
                    else if (f.Relative == null) f.Relative = t;
                    var om = Regex.Match(t, @"\s(?:ใน|on)\s+(.+)$");
                    if (om.Success && f.Origin == null) f.Origin = om.Groups[1].Value.Trim();
                    return;
                }
                if (t.IndexOf('❘') >= 0 && t.Length <= 80) { if (f.TripType == null) f.TripType = t; return; }
                if (t.IndexOf("แปลโดย Google", StringComparison.Ordinal) >= 0 || t.IndexOf("Translated by Google", StringComparison.OrdinalIgnoreCase) >= 0) { f.Translated = true; return; }
                foreach (var on in OriginNames) if (t.Equals(on, StringComparison.OrdinalIgnoreCase)) { if (f.Origin == null || f.Origin == "Google") f.Origin = on; return; }
                if (t.Length >= 15 && !t.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !TokenLikeRx.IsMatch(t) && t.IndexOf(' ') >= 0 || (t.Length >= 15 && ContainsThai(t) && !t.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
                {
                    if (afterResponseLabel) { if (f.ResponseText == null) f.ResponseText = t; }
                    else f.Texts.Add(new KeyValuePair<int, string>(f.Order, t));
                }
                return;
            }
            if (node is bool) return;
            if (node is int || node is long || node is decimal || node is double)
            {
                decimal v;
                try { v = Convert.ToDecimal(node, CultureInfo.InvariantCulture); } catch { return; }
                if (v == Math.Floor(v))
                {
                    long l = (long)v;
                    if (l >= 1 && l <= 5) f.Ints.Add(new KeyValuePair<int, long>(depth, l));
                    else if (ToDate(l).HasValue) f.Timestamps.Add(l);
                }
                return;
            }
            var list = AsList(node);
            if (list == null) return;
            bool resp = afterResponseLabel;
            foreach (var ch in list)
            {
                string cs = ch as string;
                if (cs != null && (cs.Trim().Equals("คำตอบจากเจ้าของ", StringComparison.Ordinal) || cs.Trim().StartsWith("Response from the owner", StringComparison.OrdinalIgnoreCase)))
                    resp = true;
                Walk(ch, f, depth + 1, list, resp);
            }
        }

        private static bool IsName(string s)
        {
            string t = s.Trim();
            if (t.Length == 0 || t.Length > 100) return false;
            if (t.StartsWith("http", StringComparison.OrdinalIgnoreCase) || t.Contains("/")) return false;
            if (Regex.IsMatch(t, @"^\d+$") || TokenLikeRx.IsMatch(t) && t.IndexOf(' ') < 0 && t.Length > 30) return false;
            return true;
        }

        private static bool ContainsThai(string s)
        {
            foreach (char c in s) if (c >= '฀' && c <= '๿') return true;
            return false;
        }

        /// <summary>timestamp วินาที / มิลลิวินาที / ไมโครวินาที ระหว่างปี 2005 ถึงพรุ่งนี้</summary>
        public static DateTime? ToDate(long v)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime? d = null;
            try
            {
                if (v >= 1100000000L && v <= 4102444800L) d = epoch.AddSeconds(v);
                else if (v >= 1100000000000L && v <= 4102444800000L) d = epoch.AddMilliseconds(v);
                else if (v >= 1100000000000000L && v <= 4102444800000000L) d = epoch.AddMilliseconds(v / 1000);
            }
            catch { return null; }
            if (!d.HasValue) return null;
            var local = d.Value.ToLocalTime();
            if (local.Year < 2005 || local > DateTime.Now.AddDays(1)) return null;
            return local;
        }

        private static ReviewItem ToReview(IList node, string pageUrl, DateTime now)
        {
            var f = Features(node);
            if (f.Texts.Count == 0) return null;

            decimal? raw = f.RatingSlash, scale = f.RatingSlashScale;
            bool guess = false;
            if (!raw.HasValue && f.Ints.Count > 0)
            {
                // ตื้นที่สุดก่อน (คะแนนมักเป็นฟิลด์ระดับบนของ tuple รีวิว)
                var pick = f.Ints.OrderBy(x => x.Key).First();
                raw = pick.Value; scale = 5m; guess = true;
            }
            if (!raw.HasValue) return null;

            // ข้อความหลัก: ไทยยาวสุด (ต้นฉบับ) ถ้ามี ไม่งั้นยาวสุด; อีกภาษาที่ยาวใกล้กัน = คำแปล
            var texts = f.Texts.Select(x => x.Value).Where(t => t != f.Name && t != f.TripType).Distinct().ToList();
            if (texts.Count == 0) return null;
            var thai = texts.Where(t => ContainsThai(t) && ThaiShare(t) >= 0.3).OrderByDescending(t => t.Length).FirstOrDefault();
            string main = thai ?? texts.OrderByDescending(t => t.Length).First();
            string translated = null;
            if (thai != null)
                translated = texts.Where(t => t != thai && !ContainsThai(t)).OrderByDescending(t => t.Length).FirstOrDefault();

            DateTime? date = null;
            bool approx = false;
            if (f.Timestamps.Count > 0) date = ToDate(f.Timestamps.Min());
            if (!date.HasValue && f.Relative != null) { date = GoogleTravelParser.ParseRelativeDate(f.Relative, now); approx = true; }

            string rel = f.Relative;
            if (rel != null) { var om = Regex.Match(rel, @"^(.*?)\s+(?:ใน|on)\s+.+$"); if (om.Success) rel = om.Groups[1].Value.Trim(); }

            var meta = new Dictionary<string, object> { { "source", "af-initdata" }, { "approxDate", approx } };
            if (rel != null) meta["relativeDate"] = rel;
            if (guess) meta["ratingGuess"] = true;
            if (f.ResponseRelative != null) meta["ownerResponseRelative"] = f.ResponseRelative;
            if (translated != null) meta["hasTranslation"] = true;
            bool machine = f.Translated && thai == null;
            if (machine) meta["machineTranslated"] = true;

            string text = GoogleTravelParser.CleanReviewText(main);
            return new ReviewItem
            {
                ExternalReviewId = f.PostId == null ? null : "gpost:" + f.PostId,
                OriginPlatform = string.IsNullOrEmpty(f.Origin) ? "Google" : (ReviewPlatforms.NormalizeOrigin(f.Origin) ?? "Google"),
                ReviewerName = ReviewText.CleanName(f.Name),
                RatingRaw = ReviewRating.IsValid(raw, scale) ? raw : null,
                RatingScale = ReviewRating.IsValid(raw, scale) ? scale : null,
                Text = text,
                TranslatedText = translated,
                ReviewDate = date,
                ApproxDate = approx || !date.HasValue,
                Language = AgodaPageParser.DetectLang(text),
                MachineTranslated = machine,
                IsOriginalText = !machine,
                TravellerType = f.TripType,
                OwnerResponse = f.ResponseText,
                OwnerResponseDate = f.ResponseRelative == null ? null : GoogleTravelParser.ParseRelativeDate(f.ResponseRelative, now),
                AllowOwnerResponseUpdate = true,
                ReviewUrl = pageUrl,
                InsertOnly = true,
                Meta = meta
            };
        }

        private static double ThaiShare(string s)
        {
            int thai = 0, letters = 0;
            foreach (char c in s)
            {
                if (c >= '฀' && c <= '๿') { thai++; letters++; }
                else if (char.IsLetter(c)) letters++;
            }
            return letters == 0 ? 0 : (double)thai / letters;
        }

        // ── สถิติดิบของหน้า (diagnostics) ─────────────────────────────────────

        public static void RawStats(string html, List<Block> blocks, LightFetchDiagnostics diag)
        {
            if (diag == null) return;
            diag.AfBlocks = string.Join(", ", blocks.Take(40).Select(b => b.Key + " " + (b.Chars / 1024) + "KB" + (b.Data == null ? " (แปลงไม่ได้)" : "")));
            diag.AfBlockCount = blocks.Count;
            if (string.IsNullOrEmpty(html)) return;
            diag.ContribCount = Regex.Matches(html, @"/maps/contrib/\d").Count;
            diag.PostIdTokens = Regex.Matches(html, @"(?:Ci9DQUlRQUNv|ChZDSUhN|ChdDSUhN)[A-Za-z0-9_\-]{8,}").Count;
            diag.ThaiLongStrings = Regex.Matches(html, @"""[^""<>]{0,20}[฀-๿][^""<>]{30,}""").Count;
        }
    }
}
