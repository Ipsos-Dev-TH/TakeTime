// ===========================================================================
// ReviewUtil.cs — คะแนน (normalise ใน C#), แฮชกันซ้ำ, ทำความสะอาดข้อความ, HTTP ที่จำกัดโดเมน
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Services.Reviews
{
    public static class ReviewRating
    {
        /// <summary>
        /// ตรวจคะแนนดิบ + สเกล: ไม่รู้/ผิดรูป (≤0, เกินสเกล, สเกล ≤0) ⇒ false (เก็บเป็น NULL — ห้ามเป็น 0)
        /// </summary>
        public static bool IsValid(decimal? raw, decimal? scale)
        {
            return raw.HasValue && scale.HasValue && scale.Value > 0 && raw.Value > 0 && raw.Value <= scale.Value;
        }

        /// <summary>คะแนนเทียบ /10 (ปัด 2 ตำแหน่ง) หรือ null</summary>
        public static decimal? To10(decimal? raw, decimal? scale)
        {
            if (!IsValid(raw, scale)) return null;
            return Math.Round(raw.Value / scale.Value * 10m, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>คะแนนเทียบ /5 (ปัด 2 ตำแหน่ง) หรือ null</summary>
        public static decimal? To5(decimal? raw, decimal? scale)
        {
            if (!IsValid(raw, scale)) return null;
            return Math.Round(raw.Value / scale.Value * 5m, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>อ่านตัวเลขจาก object/ข้อความ (รองรับ "8,6" แบบยุโรป) — อ่านไม่ได้/≤0 คืน null</summary>
        public static decimal? Parse(object v)
        {
            if (v == null || v is DBNull) return null;
            try
            {
                if (v is decimal) return (decimal)v > 0 ? (decimal?)v : null;
                if (v is double || v is float || v is int || v is long)
                {
                    decimal d = Convert.ToDecimal(v, CultureInfo.InvariantCulture);
                    return d > 0 ? (decimal?)d : null;
                }
                string s = v.ToString().Trim();
                if (s.Length == 0) return null;
                var m = Regex.Match(s, @"\d+(?:[.,]\d+)?");
                if (!m.Success) return null;
                decimal r;
                if (decimal.TryParse(m.Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out r) && r > 0)
                    return r;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// เดาสเกลจากคะแนนเมื่อไม่ได้ระบุ: ค่า &gt; 5 ⇒ 10, ไม่งั้นใช้สเกลตั้งต้นของแพลตฟอร์ม
        /// (คะแนน 4 ของ Booking = 4/10 ตามสเกลตั้งต้น ไม่ใช่ 4/5)
        /// </summary>
        public static decimal GuessScale(decimal? raw, string platform)
        {
            if (raw.HasValue && raw.Value > 5m) return raw.Value > 10m ? 100m : 10m;
            return ReviewPlatforms.DefaultScale(platform);
        }
    }

    public static class ReviewText
    {
        private static readonly Regex Ws = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex NonWord = new Regex(@"[^\p{L}\p{N}\p{M}]+", RegexOptions.Compiled);

        /// <summary>ปรับข้อความสำหรับแฮช: ตัวเล็ก, ตัดเครื่องหมาย/ช่องว่างทั้งหมด</summary>
        public static string NormalizeForHash(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string t = s.Normalize(NormalizationForm.FormC).ToLowerInvariant();
            return NonWord.Replace(t, "");
        }

        /// <summary>
        /// Content_Hash = SHA-256(แพลตฟอร์ม | ชื่อ | วันที่ yyyy-MM-dd | ข้อความ) — ใช้กันซ้ำเมื่อไม่มีรหัสรีวิว
        /// ข้อความว่าง (อีเมลแจ้งเตือนที่มีแต่คะแนน) ⇒ ต่อท้ายด้วยคะแนนดิบ
        /// </summary>
        public static string ContentHash(string origin, string name, DateTime? date, string title, string text, decimal? ratingRaw)
        {
            string body = NormalizeForHash(text);
            if (body.Length == 0) body = NormalizeForHash(title);
            if (body.Length > 600) body = body.Substring(0, 600);
            string input = (origin ?? "").ToLowerInvariant() + "|" + NormalizeForHash(name) + "|"
                         + (date.HasValue ? date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "") + "|" + body;
            if (body.Length == 0 && ratingRaw.HasValue)
                input += "|r=" + ratingRaw.Value.ToString("0.##", CultureInfo.InvariantCulture);
            return Sha256Hex(input);
        }

        public static string Sha256Hex(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
                var sb = new StringBuilder(64);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>รหัสคงที่แทน string.GetHashCode() (ซึ่งต่างกันระหว่าง 32/64-bit และแต่ละรอบ process)</summary>
        public static string StableId(string prefix, params string[] parts)
        {
            string joined = string.Join("|", parts ?? new string[0]);
            return prefix + Sha256Hex(joined).Substring(0, 32);
        }

        /// <summary>ชื่อผู้รีวิวตามที่แสดง (PDPA: ตัดเหลือ 100 ตัวอักษร ไม่เก็บอีเมล/เบอร์)</summary>
        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string n = Ws.Replace(name, " ").Trim();
            // ถ้าเผลอเป็นอีเมล/เบอร์โทร ⇒ ปิดบัง
            if (n.Contains("@")) n = n.Substring(0, n.IndexOf('@')).Trim();
            n = Regex.Replace(n, @"\+?\d[\d\s-]{7,}\d", "***");
            if (n.Length == 0) return null;
            return n.Length > 100 ? n.Substring(0, 100) : n;
        }

        public static string Clip(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max);
        }

        /// <summary>HTML → ข้อความ (ตัด script/style/แท็ก, ถอด entity, ย่อช่องว่าง แต่คงขึ้นบรรทัด)</summary>
        public static string HtmlToText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string s = Regex.Replace(html, @"<(script|style|noscript|head)[^>]*>.*?</\1>", " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<(br|/p|/div|/tr|/li|/h\d|/td)[^>]*>", "\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<[^>]+>", " ");
            s = WebUtility.HtmlDecode(s);
            s = Regex.Replace(s, @"[ \t\f\v ]+", " ");
            s = Regex.Replace(s, @"\s*\n\s*", "\n");
            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.Trim();
        }

        /// <summary>วันที่ไม่เกินวันนี้ / ไม่ก่อนปี 2000 / พ.ศ. → ค.ศ.</summary>
        public static DateTime? SaneDate(DateTime? d)
        {
            if (!d.HasValue) return null;
            DateTime v = d.Value;
            if (v.Year > DateTime.Now.Year + 1) { try { v = v.AddYears(-543); } catch { return null; } }
            if (v.Year < 2000) return null;
            if (v > DateTime.Now.AddDays(1)) return DateTime.Now;
            return v;
        }

        public static DateTime? ParseDate(object raw)
        {
            if (raw == null || raw is DBNull) return null;
            if (raw is DateTime) return SaneDate((DateTime)raw);
            string s = raw.ToString().Trim();
            if (s.Length == 0) return null;
            string[] fmts =
            {
                "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy",
                "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-ddTHH:mm:ss.fffZ", "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm", "dd/MM/yyyy HH:mm", "MMM d, yyyy", "MMMM d, yyyy", "d MMM yyyy", "d MMMM yyyy",
                "MMM yyyy", "MMMM yyyy", "yyyy-MM"
            };
            DateTime d;
            if (DateTime.TryParseExact(s, fmts, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out d)
                || DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out d))
                return SaneDate(d);
            return null;
        }
    }

    /// <summary>HTTP ที่ใช้ในตัวดึงรีวิว — timeout ชัดเจน, TLS 1.2, อ่าน body ของ error กลับมาเพื่อ diagnose</summary>
    public static class ReviewHttp
    {
        public class Response
        {
            public int Status;
            public string Body;
            /// <summary>Location header (เมื่อ allowRedirect = false และได้ 3xx)</summary>
            public string Location;
            public bool Ok { get { return Status >= 200 && Status < 300; } }
            public bool IsRedirect { get { return Status >= 300 && Status < 400 && !string.IsNullOrEmpty(Location); } }
        }

        public static Response Send(string method, string url, string body = null, string contentType = null,
            IDictionary<string, string> headers = null, int timeoutMs = 30000, bool allowRedirect = true)
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = method;
            req.AllowAutoRedirect = allowRedirect;
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            req.UserAgent = "TakeTime-ReviewAggregator/1.0";
            req.Accept = "application/json, text/html;q=0.9, */*;q=0.8";
            if (headers != null)
                foreach (var h in headers)
                {
                    if (string.Equals(h.Key, "Referer", StringComparison.OrdinalIgnoreCase)) req.Referer = h.Value;
                    else if (string.Equals(h.Key, "User-Agent", StringComparison.OrdinalIgnoreCase)) req.UserAgent = h.Value;
                    else if (string.Equals(h.Key, "Accept", StringComparison.OrdinalIgnoreCase)) req.Accept = h.Value;
                    else req.Headers[h.Key] = h.Value;
                }
            if (body != null)
            {
                byte[] data = Encoding.UTF8.GetBytes(body);
                req.ContentType = contentType ?? "application/json";
                req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
            }
            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return new Response { Status = (int)resp.StatusCode, Location = resp.Headers["Location"], Body = rd.ReadToEnd() };
            }
            catch (WebException wex)
            {
                var r = wex.Response as HttpWebResponse;
                if (r == null) throw;
                using (r)
                using (var rd = new StreamReader(r.GetResponseStream(), Encoding.UTF8))
                    return new Response { Status = (int)r.StatusCode, Location = r.Headers["Location"], Body = rd.ReadToEnd() };
            }
        }

        /// <summary>
        /// GET URL ที่ผู้ใช้กรอก (URL_FETCH) แบบกัน SSRF: ทุก hop (รวม redirect) ต้องผ่าน allow-list โดเมน
        /// และ resolve ได้เฉพาะ IP สาธารณะ (ห้าม loopback / private / link-local / metadata) — ไม่ตาม redirect อัตโนมัติ
        /// </summary>
        public static Response GetPublic(string url, IDictionary<string, string> headers = null, int timeoutMs = 30000, int maxRedirects = 4)
        {
            string current = url;
            for (int hop = 0; ; hop++)
            {
                Uri uri;
                if (!IsAllowedPublicUrl(current) || !Uri.TryCreate(current, UriKind.Absolute, out uri) || !ResolvesToPublicAddresses(uri.DnsSafeHost))
                    throw new ReviewSourceSkippedException("URL ไม่อยู่ในรายชื่อโดเมนที่อนุญาต หรือชี้ไป IP ภายใน (กัน SSRF): " + current);
                var resp = Send("GET", current, null, null, headers, timeoutMs, false);
                if (!resp.IsRedirect) return resp;
                if (hop >= maxRedirects) throw new Exception("redirect มากเกินไป");
                Uri next;
                if (!Uri.TryCreate(uri, resp.Location, out next)) throw new Exception("redirect ไม่ถูกต้อง");
                current = next.AbsoluteUri;
            }
        }

        /// <summary>true เมื่อ host resolve ได้และทุก IP เป็นที่อยู่สาธารณะ</summary>
        public static bool ResolvesToPublicAddresses(string host)
        {
            IPAddress[] addrs;
            try { addrs = Dns.GetHostAddresses(host); }
            catch { return false; }
            if (addrs == null || addrs.Length == 0) return false;
            foreach (var a in addrs)
                if (!IsPublicAddress(a)) return false;
            return true;
        }

        private static bool IsPublicAddress(IPAddress ip)
        {
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            if (IPAddress.IsLoopback(ip)) return false;
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                byte[] b = ip.GetAddressBytes();
                if (b[0] == 0 || b[0] == 10 || b[0] == 127) return false;            // this-net / private / loopback
                if (b[0] == 169 && b[1] == 254) return false;                         // link-local (cloud metadata)
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false;            // private
                if (b[0] == 192 && b[1] == 168) return false;                         // private
                if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return false;           // CGNAT
                if (b[0] == 192 && b[1] == 0 && (b[2] == 0 || b[2] == 2)) return false;
                if (b[0] == 198 && (b[1] == 18 || b[1] == 19)) return false;
                if (b[0] >= 224) return false;                                        // multicast / reserved / broadcast
                return true;
            }
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || ip.Equals(IPAddress.IPv6Any)) return false;
                byte[] b = ip.GetAddressBytes();
                if ((b[0] & 0xFE) == 0xFC) return false;                              // unique local fc00::/7
                return true;
            }
            return false;
        }

        public static string FormEncode(IDictionary<string, string> fields)
        {
            var sb = new StringBuilder();
            foreach (var kv in fields)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(Uri.EscapeDataString(kv.Key)).Append('=').Append(Uri.EscapeDataString(kv.Value ?? ""));
            }
            return sb.ToString();
        }

        /// <summary>ตัดข้อความ error ให้สั้นพอเก็บ log (และไม่มี key หลุด)</summary>
        public static string Brief(string body, int max = 300)
        {
            if (string.IsNullOrEmpty(body)) return "";
            string b = Regex.Replace(body, @"\s+", " ");
            b = Regex.Replace(b, @"(key|token|secret)=[^&\s""]+", "$1=***", RegexOptions.IgnoreCase);
            return b.Length > max ? b.Substring(0, max) + "…" : b;
        }

        // ── SSRF allow-list สำหรับ URL ที่ผู้ใช้กรอก (URL_FETCH) ──────────────────────
        private static readonly string[] AllowedDomains =
        {
            "google.com", "agoda.com", "booking.com", "tripadvisor.com", "tripadvisor.co.th", "expedia.com",
            "expedia.co.th", "traveloka.com", "trip.com", "airbnb.com", "pantip.com", "wongnai.com", "hotels.com"
        };

        public static bool IsAllowedPublicUrl(string url)
        {
            Uri uri;
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;
            if (uri.HostNameType != UriHostNameType.Dns) return false;   // ห้าม IP ตรง ๆ
            string host = uri.Host.ToLowerInvariant();
            foreach (var d in AllowedDomains)
                if (host == d || host.EndsWith("." + d)) return true;
            return false;
        }
    }
}
