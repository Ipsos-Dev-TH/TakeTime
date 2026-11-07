// ===========================================================================
// PlacesApiAdapter.cs — Google Places API (legacy Place Details) — ย้ายมาจาก AIReviewAnalysisService.FetchGoogleReviews
//
// ⚠ ข้อจำกัด: ได้รีวิวสูงสุด 5 รายการต่อครั้ง (ย้อนประวัติไม่ได้) และข้อกำหนด Google Maps Platform 3.2.3
//   ห้ามเก็บเนื้อหารีวิวถาวร — ตั้ง ratingOnly = true เพื่อเก็บเฉพาะคะแนนรวม/จำนวนรีวิว
//   ทางที่ถูกต้องสำหรับรีวิวทั้งหมด = GoogleBusinessProfileAdapter (GBP_API)
// config: placeId, apiKey (ความลับ) — ไม่ตั้ง = ใช้ GooglePlacesApiKey / GooglePlaceId ของระบบ
// ===========================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Services.Reviews
{
    public class PlacesApiAdapter : IReviewSourceAdapter
    {
        public string Key { get { return IngestMethods.PlacesApi; } }
        public bool SupportsAutomatic { get { return true; } }

        /// <summary>คีย์ Places ที่ใช้ได้: ของแหล่ง GOOGLE ก่อน แล้วค่อยค่าระบบ (ใช้ร่วมกับหน้าแรกได้)</summary>
        public static string ResolveApiKey(ReviewSourceRow src)
        {
            string k = src != null ? src.Cfg("apiKey") : null;
            if (string.IsNullOrEmpty(k)) k = AppCfg.Get("GooglePlacesApiKey");
            return string.IsNullOrWhiteSpace(k) ? null : k.Trim();
        }

        public static string ResolvePlaceId(ReviewSourceRow src)
        {
            string p = src != null ? src.Cfg("placeId") : null;
            if (string.IsNullOrEmpty(p)) p = AppCfg.Get("GooglePlaceId");
            if (string.IsNullOrEmpty(p)) p = ConfigurationManager.AppSettings["GooglePlaceId"];
            return string.IsNullOrWhiteSpace(p) ? null : p.Trim();
        }

        public ReviewFetchResult Fetch(ReviewSourceRow src)
        {
            string apiKey = ResolveApiKey(src);
            string placeId = ResolvePlaceId(src);
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(placeId))
                throw new ReviewSourceSkippedException("ยังไม่ได้ตั้ง placeId / apiKey ของ Google Places");

            bool ratingOnly = src.CfgBool("ratingOnly", false);
            var result = new ReviewFetchResult { PlatformRatingScale = 5m };
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var seen = new HashSet<string>();

            // th ก่อน แล้ว en (รีวิวชุดเดียวกัน ต่างภาษาแปล — ได้รายการเพิ่มไม่มาก แต่ได้ข้อความต้นฉบับบางส่วน)
            string[] langs = ratingOnly ? new[] { "th" } : new[] { "th", "en" };
            foreach (string lang in langs)
            {
                string url = "https://maps.googleapis.com/maps/api/place/details/json"
                    + "?place_id=" + Uri.EscapeDataString(placeId)
                    + "&fields=" + (ratingOnly ? "rating,user_ratings_total,url" : "rating,user_ratings_total,url,reviews")
                    + "&reviews_sort=newest&reviews_no_translations=true&language=" + lang
                    + "&key=" + Uri.EscapeDataString(apiKey);

                var resp = ReviewHttp.Send("GET", url);
                if (!resp.Ok) throw new Exception("Google Places HTTP " + resp.Status + ": " + ReviewHttp.Brief(resp.Body));
                var root = ser.Deserialize<Dictionary<string, object>>(resp.Body);
                string status = root != null && root.ContainsKey("status") ? Convert.ToString(root["status"]) : "";
                if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    string em = root != null && root.ContainsKey("error_message") ? Convert.ToString(root["error_message"]) : "";
                    throw new Exception("Google Places status=" + status + (em.Length > 0 ? " — " + em : ""));
                }
                var place = root["result"] as Dictionary<string, object>;
                if (place == null) continue;

                if (place.ContainsKey("rating")) result.PlatformRating = ReviewRating.Parse(place["rating"]);
                if (place.ContainsKey("user_ratings_total"))
                {
                    try { result.PlatformReviewCount = Convert.ToInt32(place["user_ratings_total"]); } catch { }
                }
                string listingUrl = place.ContainsKey("url") ? Convert.ToString(place["url"]) : null;

                if (ratingOnly) { result.Note = "เก็บเฉพาะคะแนนรวม (ratingOnly)"; break; }

                var reviews = place.ContainsKey("reviews") ? place["reviews"] as ArrayList : null;
                if (reviews == null) continue;
                foreach (var o in reviews)
                {
                    var r = o as Dictionary<string, object>;
                    if (r == null) continue;
                    string author = r.ContainsKey("author_name") ? Convert.ToString(r["author_name"]) : null;
                    string authorUrl = r.ContainsKey("author_url") ? Convert.ToString(r["author_url"]) : null;
                    long epoch = 0;
                    try { if (r.ContainsKey("time")) epoch = Convert.ToInt64(r["time"]); } catch { }
                    // รหัสคงที่: SHA-256(ลิงก์ผู้รีวิวหรือชื่อ | เวลา) — ไม่เก็บลิงก์โปรไฟล์เอง (PDPA)
                    string extId = ReviewText.StableId("pl:", authorUrl ?? author ?? "", epoch.ToString());
                    if (!seen.Add(extId)) continue;

                    result.Items.Add(new ReviewItem
                    {
                        ExternalReviewId = extId,
                        ReviewerName = author,
                        RatingRaw = r.ContainsKey("rating") ? ReviewRating.Parse(r["rating"]) : null,
                        RatingScale = 5m,
                        Text = r.ContainsKey("text") ? Convert.ToString(r["text"]) : null,
                        ReviewDate = epoch > 0
                            ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(epoch).ToLocalTime()
                            : (DateTime?)null,
                        Language = r.ContainsKey("language") ? Convert.ToString(r["language"]) : null,
                        ReviewUrl = listingUrl
                    });
                }
            }
            result.Note = (result.Note ?? "") + " (Places API ให้สูงสุด 5 รีวิวต่อครั้ง)";
            return result;
        }
    }
}
