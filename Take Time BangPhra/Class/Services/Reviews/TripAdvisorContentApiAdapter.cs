// ===========================================================================
// TripAdvisorContentApiAdapter.cs — Tripadvisor Content API: location/{id}/reviews (5 รีวิวล่าสุด) + details
//
// config: locationId, apiKey (ความลับ), language (en), referer (ถ้าคีย์ถูกจำกัดโดเมน)
//         ratingOnly (false) — ข้อกำหนด Content API เน้น "แสดงผล"; ถ้ายังไม่ได้ตรวจเงื่อนไขการเก็บ ให้ตั้ง true
// ===========================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Services.Reviews
{
    public class TripAdvisorContentApiAdapter : IReviewSourceAdapter
    {
        public string Key { get { return IngestMethods.TripAdvisorApi; } }
        public bool SupportsAutomatic { get { return true; } }

        private const string Base = "https://api.content.tripadvisor.com/api/v1/location/";

        public ReviewFetchResult Fetch(ReviewSourceRow src)
        {
            string locationId = src.Cfg("locationId");
            string apiKey = src.Cfg("apiKey");
            if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(apiKey))
                throw new ReviewSourceSkippedException("ยังไม่ได้ตั้ง locationId / apiKey ของ Tripadvisor Content API");
            string lang = src.Cfg("language", "en");
            var headers = new Dictionary<string, string> { { "Accept", "application/json" } };
            string referer = src.Cfg("referer");
            if (!string.IsNullOrEmpty(referer)) headers["Referer"] = referer;

            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var result = new ReviewFetchResult { PlatformRatingScale = 5m };
            string loc = Uri.EscapeDataString(locationId.Trim());
            string key = Uri.EscapeDataString(apiKey);

            // คะแนนรวม + จำนวนรีวิว
            var det = ReviewHttp.Send("GET", Base + loc + "/details?language=" + Uri.EscapeDataString(lang) + "&key=" + key, null, null, headers);
            if (det.Status == 401 || det.Status == 403)
                throw new Exception("Tripadvisor HTTP " + det.Status + " — ตรวจ apiKey / โดเมน-IP ที่อนุญาต / งบรายวัน: " + ReviewHttp.Brief(det.Body));
            if (det.Ok)
            {
                var d = ser.Deserialize<Dictionary<string, object>>(det.Body);
                if (d != null)
                {
                    if (d.ContainsKey("rating")) result.PlatformRating = ReviewRating.Parse(d["rating"]);
                    if (d.ContainsKey("num_reviews"))
                    {
                        int n;
                        if (int.TryParse(Convert.ToString(d["num_reviews"]), out n)) result.PlatformReviewCount = n;
                    }
                }
            }
            else result.Warnings.Add("details HTTP " + det.Status);

            if (src.CfgBool("ratingOnly", false)) { result.Note = "เก็บเฉพาะคะแนนรวม (ratingOnly)"; return result; }

            var resp = ReviewHttp.Send("GET", Base + loc + "/reviews?language=" + Uri.EscapeDataString(lang) + "&limit=5&key=" + key, null, null, headers);
            if (!resp.Ok) throw new Exception("Tripadvisor reviews HTTP " + resp.Status + ": " + ReviewHttp.Brief(resp.Body));
            var root = ser.Deserialize<Dictionary<string, object>>(resp.Body);
            var data = root != null && root.ContainsKey("data") ? root["data"] as ArrayList : null;
            if (data != null)
                foreach (var o in data)
                {
                    var r = o as Dictionary<string, object>;
                    if (r == null) continue;
                    string user = null;
                    var u = r.ContainsKey("user") ? r["user"] as Dictionary<string, object> : null;
                    if (u != null) user = G(u, "username");
                    string ownerResp = null;
                    DateTime? ownerDate = null;
                    var or = r.ContainsKey("owner_response") ? r["owner_response"] as Dictionary<string, object> : null;
                    if (or != null) { ownerResp = G(or, "text"); ownerDate = ReviewText.ParseDate(G(or, "published_date")); }

                    result.Items.Add(new ReviewItem
                    {
                        ExternalReviewId = G(r, "id"),
                        ReviewerName = user,
                        RatingRaw = r.ContainsKey("rating") ? ReviewRating.Parse(r["rating"]) : null,
                        RatingScale = 5m,
                        Title = G(r, "title"),
                        Text = G(r, "text"),
                        ReviewDate = ReviewText.ParseDate(G(r, "published_date")),
                        Language = G(r, "lang"),
                        ReviewUrl = G(r, "url"),
                        StayDate = ReviewText.ParseDate(G(r, "travel_date")),
                        TravellerType = G(r, "trip_type"),
                        OwnerResponse = ownerResp,
                        OwnerResponseDate = ownerDate
                    });
                }
            result.Note = "Tripadvisor: 5 รีวิวล่าสุดต่อครั้ง";
            return result;
        }

        private static string G(Dictionary<string, object> d, string k)
        {
            object v;
            return d != null && d.TryGetValue(k, out v) && v != null ? v.ToString() : null;
        }
    }
}
