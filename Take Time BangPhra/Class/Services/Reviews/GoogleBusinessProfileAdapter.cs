// ===========================================================================
// GoogleBusinessProfileAdapter.cs — Google Business Profile API (v4) accounts.locations.reviews.list
//
// ทางเดียวที่ได้รีวิว Google "ทั้งหมด" + คำตอบของโรงแรม อย่างถูกข้อกำหนด (ต้องขอสิทธิ์ GBP API ก่อน ~14 วัน)
// config: accountId, locationId, clientId, clientSecret (ความลับ), refreshToken (ความลับ)
//         fullResyncDays (7) — รอบเต็มเพื่อจับการแก้ไข/คำตอบใหม่, maxPages (20)
// OAuth: refresh-token flow → access token (cache ในหน่วยความจำจนใกล้หมดอายุ)
//        consent screen ต้องเป็น "In production" ไม่งั้น refresh token หมดอายุใน 7 วัน
// ===========================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Services.Reviews
{
    public class GoogleBusinessProfileAdapter : IReviewSourceAdapter
    {
        public string Key { get { return IngestMethods.GbpApi; } }
        public bool SupportsAutomatic { get { return true; } }

        private class CachedToken { public string Token; public DateTime ExpiresAt; }
        private static readonly Dictionary<string, CachedToken> _tokens = new Dictionary<string, CachedToken>();
        private static readonly object _tokLock = new object();

        public ReviewFetchResult Fetch(ReviewSourceRow src)
        {
            string accountId = NormalizeId(src.Cfg("accountId"), "accounts/");
            string locationId = NormalizeId(src.Cfg("locationId"), "locations/");
            string clientId = src.Cfg("clientId");
            string clientSecret = src.Cfg("clientSecret");
            string refreshToken = src.Cfg("refreshToken");
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(clientId)
                || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(refreshToken))
                throw new ReviewSourceSkippedException("ยังตั้งค่า GBP ไม่ครบ (accountId, locationId, clientId, clientSecret, refreshToken)");

            string token = GetAccessToken(clientId, clientSecret, refreshToken);
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var result = new ReviewFetchResult { PlatformRatingScale = 5m };

            int fullDays = src.CfgInt("fullResyncDays", 7);
            int maxPages = Math.Max(1, Math.Min(100, src.CfgInt("maxPages", 20)));
            // incremental: เรียงตาม updateTime ใหม่สุดก่อน หยุดเมื่อเจอที่เก่ากว่ารอบสำเร็จล่าสุด − 1 วัน
            DateTime? stopBefore = null;
            if (src.LastSuccessAt.HasValue && src.LastFetchDate.HasValue
                && (DateTime.Now - src.LastSuccessAt.Value).TotalDays < fullDays
                && !src.CfgBool("forceFull", false))
                stopBefore = src.LastSuccessAt.Value.AddDays(-1);

            // full resync ตามรอบ: ถ้าครบ fullResyncDays นับจากรอบเต็มล่าสุด (เก็บใน config ไม่ได้ → ใช้วันในสัปดาห์แบบง่าย)
            if (stopBefore.HasValue && fullDays > 0 && DateTime.Now.DayOfYear % fullDays == 0 && DateTime.Now.Hour < 6)
                stopBefore = null;

            string pageToken = null;
            int pages = 0;
            bool stop = false;
            do
            {
                pages++;
                string url = "https://mybusiness.googleapis.com/v4/" + accountId + "/" + locationId
                    + "/reviews?pageSize=50&orderBy=" + Uri.EscapeDataString("updateTime desc")
                    + (pageToken != null ? "&pageToken=" + Uri.EscapeDataString(pageToken) : "");
                var resp = ReviewHttp.Send("GET", url, null, null,
                    new Dictionary<string, string> { { "Authorization", "Bearer " + token } });
                if (resp.Status == 401)
                {
                    InvalidateToken(clientId, refreshToken);
                    throw new Exception("GBP 401 (token ใช้ไม่ได้ — ตรวจ refreshToken / consent screen ต้องเป็น In production)");
                }
                if (resp.Status == 403 || resp.Status == 429)
                    throw new Exception("GBP HTTP " + resp.Status + " — ยังไม่ได้รับอนุมัติ GBP API หรือโควตาเป็น 0: " + ReviewHttp.Brief(resp.Body));
                if (!resp.Ok) throw new Exception("GBP HTTP " + resp.Status + ": " + ReviewHttp.Brief(resp.Body));

                var root = ser.Deserialize<Dictionary<string, object>>(resp.Body) ?? new Dictionary<string, object>();
                if (root.ContainsKey("averageRating")) result.PlatformRating = ReviewRating.Parse(root["averageRating"]);
                if (root.ContainsKey("totalReviewCount"))
                {
                    try { result.PlatformReviewCount = Convert.ToInt32(root["totalReviewCount"]); } catch { }
                }

                var reviews = root.ContainsKey("reviews") ? root["reviews"] as ArrayList : null;
                if (reviews != null)
                    foreach (var o in reviews)
                    {
                        var r = o as Dictionary<string, object>;
                        if (r == null) continue;
                        DateTime? updated = ParseTime(Get(r, "updateTime"));
                        if (stopBefore.HasValue && updated.HasValue && updated.Value < stopBefore.Value) { stop = true; break; }
                        result.Items.Add(Map(r));
                    }

                pageToken = root.ContainsKey("nextPageToken") ? Convert.ToString(root["nextPageToken"]) : null;
                if (string.IsNullOrEmpty(pageToken)) pageToken = null;
            } while (pageToken != null && !stop && pages < maxPages);

            result.Note = "GBP: " + pages + " หน้า" + (stopBefore.HasValue ? " (เฉพาะที่อัปเดตหลังรอบก่อน)" : " (รอบเต็ม)");
            if (pageToken != null && !stop) result.Warnings.Add("หยุดที่ " + maxPages + " หน้า (maxPages) — ยังมีรีวิวเก่าเหลือ");
            return result;
        }

        private static ReviewItem Map(Dictionary<string, object> r)
        {
            string reviewer = null;
            var rv = r.ContainsKey("reviewer") ? r["reviewer"] as Dictionary<string, object> : null;
            if (rv != null)
            {
                bool anon = rv.ContainsKey("isAnonymous") && Convert.ToString(rv["isAnonymous"]).Equals("true", StringComparison.OrdinalIgnoreCase);
                reviewer = anon ? null : Get(rv, "displayName");
            }
            string reply = null;
            DateTime? replyDate = null;
            var rr = r.ContainsKey("reviewReply") ? r["reviewReply"] as Dictionary<string, object> : null;
            if (rr != null) { reply = Get(rr, "comment"); replyDate = ParseTime(Get(rr, "updateTime")); }

            return new ReviewItem
            {
                ExternalReviewId = Get(r, "reviewId"),
                ReviewerName = reviewer,
                RatingRaw = StarToNumber(Get(r, "starRating")),
                RatingScale = 5m,
                Text = Get(r, "comment"),
                ReviewDate = ParseTime(Get(r, "createTime")),
                OwnerResponse = reply,
                OwnerResponseDate = replyDate
            };
        }

        private static decimal? StarToNumber(string star)
        {
            switch ((star ?? "").ToUpperInvariant())
            {
                case "ONE": return 1m;
                case "TWO": return 2m;
                case "THREE": return 3m;
                case "FOUR": return 4m;
                case "FIVE": return 5m;
                default: return null;   // STAR_RATING_UNSPECIFIED = ไม่รู้
            }
        }

        private static string Get(Dictionary<string, object> d, string k)
        {
            object v;
            return d != null && d.TryGetValue(k, out v) && v != null ? v.ToString() : null;
        }

        private static DateTime? ParseTime(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            DateTime d;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out d))
                return DateTime.SpecifyKind(d, DateTimeKind.Utc).ToLocalTime();
            return null;
        }

        private static string NormalizeId(string v, string prefix)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            v = v.Trim().Trim('/');
            // รับได้ทั้ง "123", "accounts/123" และ "accounts/123/locations/456" (ตัดเอาส่วนที่ต้องการ)
            int i = v.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (i >= 0)
            {
                string rest = v.Substring(i + prefix.Length);
                int slash = rest.IndexOf('/');
                if (slash >= 0) rest = rest.Substring(0, slash);
                return prefix + rest;
            }
            return prefix + v;
        }

        // ── OAuth refresh-token → access token ───────────────────────────────

        private static string CacheKey(string clientId, string refreshToken)
        {
            return ReviewText.Sha256Hex(clientId + "|" + refreshToken);
        }

        private static void InvalidateToken(string clientId, string refreshToken)
        {
            lock (_tokLock) { _tokens.Remove(CacheKey(clientId, refreshToken)); }
        }

        public static string GetAccessToken(string clientId, string clientSecret, string refreshToken)
        {
            string key = CacheKey(clientId, refreshToken);
            lock (_tokLock)
            {
                CachedToken c;
                if (_tokens.TryGetValue(key, out c) && c.ExpiresAt > DateTime.UtcNow.AddMinutes(2)) return c.Token;
            }
            var body = ReviewHttp.FormEncode(new Dictionary<string, string>
            {
                { "client_id", clientId }, { "client_secret", clientSecret },
                { "refresh_token", refreshToken }, { "grant_type", "refresh_token" }
            });
            var resp = ReviewHttp.Send("POST", "https://oauth2.googleapis.com/token", body, "application/x-www-form-urlencoded");
            var ser = new JavaScriptSerializer();
            Dictionary<string, object> root = null;
            try { root = ser.Deserialize<Dictionary<string, object>>(resp.Body); } catch { }
            if (!resp.Ok || root == null || !root.ContainsKey("access_token"))
            {
                string err = root != null && root.ContainsKey("error") ? Convert.ToString(root["error"]) : ("HTTP " + resp.Status);
                if (err == "invalid_grant")
                    throw new Exception("refreshToken ใช้ไม่ได้แล้ว (invalid_grant) — ให้เจ้าของกด Connect Google ใหม่ / ตั้ง consent screen เป็น In production");
                throw new Exception("ขอ access token ไม่สำเร็จ: " + err);
            }
            int expires = 3600;
            try { if (root.ContainsKey("expires_in")) expires = Convert.ToInt32(root["expires_in"]); } catch { }
            string tok = Convert.ToString(root["access_token"]);
            lock (_tokLock) { _tokens[key] = new CachedToken { Token = tok, ExpiresAt = DateTime.UtcNow.AddSeconds(expires) }; }
            return tok;
        }
    }
}
