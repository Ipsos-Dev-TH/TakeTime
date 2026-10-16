// ===========================================================================
// ReviewModels.cs — โมเดลกลางของระบบรวมรีวิวหลายแพลตฟอร์ม (PHASE19_26)
//
//   IReviewSourceAdapter  : ตัวดึงรีวิวหนึ่งวิธี (GBP_API / PLACES_API / TRIPADVISOR_API / EMAIL / URL_FETCH / AGODA_LIGHT)
//   ReviewSourceRow       : แถวหนึ่งของ AI_Review_Sources (+ ApiConfig ที่ถอดรหัสแล้ว — ห้ามส่งไปเบราว์เซอร์)
//   ReviewItem            : รีวิวหนึ่งรายการในรูปกลาง ก่อนบันทึกด้วย ReviewStore.Upsert
//   ReviewFetchResult     : ผลการดึงหนึ่งรอบ + คะแนนรวมที่แพลตฟอร์มประกาศ (ถ้ามี)
// ===========================================================================

using System;
using System.Collections.Generic;

namespace Take_Time_BangPhra.Services.Reviews
{
    /// <summary>วิธีนำเข้ารีวิว — ค่าที่เก็บใน Ingest_Method</summary>
    public static class IngestMethods
    {
        public const string GbpApi = "GBP_API";
        public const string PlacesApi = "PLACES_API";
        public const string TripAdvisorApi = "TRIPADVISOR_API";
        public const string Email = "EMAIL";
        public const string Csv = "CSV";
        public const string Manual = "MANUAL";
        public const string UrlFetch = "URL_FETCH";
        /// <summary>อ่านหน้าที่พัก Agoda วันละครั้ง (JSON-LD + รีวิวที่หน้าแสดง + endpoint รีวิวหน้าแรก) — ไม่ crawl</summary>
        public const string AgodaLight = "AGODA_LIGHT";
        /// <summary>อ่านหน้ารีวิวโรงแรมบน Google Travel วันละครั้ง (1 คำขอ, ไม่เลื่อนโหลดเพิ่ม) — ทางที่ถูกข้อกำหนดคือ GBP_API</summary>
        public const string GoogleTravelLight = "GOOGLE_TRAVEL_LIGHT";
        public const string AiSearch = "AI_SEARCH";
        public const string Legacy = "LEGACY";

        public static readonly string[] All =
            { GbpApi, PlacesApi, TripAdvisorApi, Email, Csv, Manual, UrlFetch, AgodaLight, GoogleTravelLight, AiSearch };

        /// <summary>วิธีที่ตัวตั้งเวลาเรียกเองได้ (ต้องตรงกับ AdapterFor และ SQL ใน RunDueSources)</summary>
        public static readonly string[] Automatic = { GbpApi, PlacesApi, TripAdvisorApi, Email, UrlFetch, AgodaLight, GoogleTravelLight };

        public static bool IsValid(string m)
        {
            if (string.IsNullOrEmpty(m)) return false;
            foreach (var x in All) if (string.Equals(x, m, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    /// <summary>ตัวดึงรีวิวหนึ่งวิธี — ต้องไม่บันทึก DB เอง (ReviewAggregationService เป็นคน upsert)</summary>
    public interface IReviewSourceAdapter
    {
        /// <summary>รหัสวิธี = ค่าใน Ingest_Method (เช่น GBP_API)</summary>
        string Key { get; }

        /// <summary>true = ให้ตัวตั้งเวลาเรียกเองได้ (false = ต้องกดเองเท่านั้น)</summary>
        bool SupportsAutomatic { get; }

        /// <summary>ดึงรีวิวของแหล่งนี้ — โยน exception ได้เมื่อดึงไม่สำเร็จ (นับเป็นล้มเหลว)</summary>
        ReviewFetchResult Fetch(ReviewSourceRow source);
    }

    /// <summary>แหล่งยังตั้งค่าไม่ครบ / ปิดไว้ตามนโยบาย — บันทึกเป็น SKIPPED ไม่นับเป็นความล้มเหลว</summary>
    public class ReviewSourceSkippedException : Exception
    {
        public ReviewSourceSkippedException(string message) : base(message) { }
    }

    /// <summary>
    /// ปลายทางกันบอท (HTTP 403/429/503, captcha, Access Denied, body ว่าง) — นับเป็น FAILED แบบพิเศษ:
    /// ไม่ retry ถี่ (เลื่อนรอบเต็ม) และครบ N ครั้งติดกันปิดดึงอัตโนมัติของแหล่งนั้น + แจ้งเตือนครั้งเดียว
    /// </summary>
    public class ReviewSourceBlockedException : Exception
    {
        public string Marker { get; private set; }
        public ReviewSourceBlockedException(string marker, string message) : base(message) { Marker = marker; }
    }

    public class ReviewSourceRow
    {
        public int Id;
        public string SourceCode;
        public string SourceName;
        public string IngestMethod;
        public int FetchIntervalMin = 1440;
        public bool IsAutoEnabled;          // Is_Enabled (ดึงอัตโนมัติ)
        public bool IsLegacyEnabled;        // IsEnabled เดิม (หน้า AIReviewDashboard)
        public string SourceUrl;
        public DateTime? NextFetchAt;
        public DateTime? LastSuccessAt;
        public DateTime? LastFetchDate;
        public string LastError;
        public int ConsecutiveFailures;
        public decimal? PlatformRating;
        public decimal? PlatformRatingScale;
        public int? PlatformReviewCount;
        public int TotalReviews;

        /// <summary>ApiConfig ที่ถอดรหัสความลับแล้ว — ใช้ฝั่ง server เท่านั้น</summary>
        public Dictionary<string, object> Config = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>ชื่อแพลตฟอร์มต้นทางที่เก็บใน Origin_Platform (เช่น Booking.com)</summary>
        public string OriginPlatform => ReviewPlatforms.OriginFor(SourceCode, SourceName);

        public string Cfg(string key, string fallback = null)
        {
            object v;
            if (Config != null && Config.TryGetValue(key, out v) && v != null)
            {
                string s = v.ToString().Trim();
                if (s.Length > 0) return s;
            }
            return fallback;
        }

        public int CfgInt(string key, int fallback)
        {
            int v;
            return int.TryParse(Cfg(key), out v) ? v : fallback;
        }

        public bool CfgBool(string key, bool fallback)
        {
            string s = Cfg(key);
            if (string.IsNullOrEmpty(s)) return fallback;
            bool b;
            if (bool.TryParse(s, out b)) return b;
            return s == "1" || s.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class ReviewItem
    {
        /// <summary>รหัสรีวิวของแพลตฟอร์ม (หรือรหัสสังเคราะห์ เช่น email:&lt;msgid&gt; สำหรับแถวชั่วคราว)</summary>
        public string ExternalReviewId;
        /// <summary>แพลตฟอร์มที่เขียนรีวิวจริง — ว่าง = ใช้ของแหล่ง</summary>
        public string OriginPlatform;
        /// <summary>ชื่อที่แสดงบนแพลตฟอร์มเท่านั้น (PDPA: ไม่เก็บรูป/โปรไฟล์/อีเมล)</summary>
        public string ReviewerName;
        public decimal? RatingRaw;
        public decimal? RatingScale;
        public string Title;
        public string Text;
        public DateTime? ReviewDate;
        public string Language;
        public string ReviewUrl;
        public string OwnerResponse;
        public DateTime? OwnerResponseDate;
        public DateTime? StayDate;
        public string RoomType;
        public string TravellerType;
        public bool IsProvisional;
        /// <summary>
        /// true = เพิ่มเฉพาะรีวิวที่ยังไม่มี — แถวเดิมที่ไม่ใช่ชั่วคราวจะไม่ถูกแก้ (ไม่เขียนข้อความทับ/ไม่ส่ง AI วิเคราะห์ซ้ำ)
        /// แถวชั่วคราวจากอีเมลยังถูกเลื่อนขั้นตามปกติ
        /// </summary>
        public bool InsertOnly;
        /// <summary>
        /// true = ข้อความเป็นภาษาต้นฉบับ (ไม่ใช่คำแปลของแพลตฟอร์ม) — ใช้กับกฎ "แทนคำแปลที่เก็บไว้ด้วยต้นฉบับครั้งเดียว"
        /// แม้แถวเดิมจะเป็น InsertOnly (เมื่อภาษาเปลี่ยน เช่น en → th ด้วย External_Review_Id เดิม)
        /// </summary>
        public bool IsOriginalText;
        /// <summary>true = แพลตฟอร์มให้มาแต่คำแปลเครื่อง (เก็บใน Is_Machine_Translated ถ้ามีคอลัมน์ — ไม่เขียนลงข้อความ)</summary>
        public bool? MachineTranslated;
        /// <summary>คำแปลที่แพลตฟอร์มแสดง (เฉพาะ diagnostics — ไม่บันทึก)</summary>
        public string TranslatedText;
        public string TranslatedTitle;
        /// <summary>
        /// true = ReviewDate เป็นวันโดยประมาณ (จาก "4 เดือนที่แล้ว") — ไม่ใช้วันที่ในแฮชกันซ้ำ (ไม่งั้นแฮชเลื่อนทุกวัน)
        /// และวันที่ถูกตั้งตอนเพิ่มครั้งแรกเท่านั้น
        /// </summary>
        public bool ApproxDate;
        /// <summary>true = อนุญาตอัปเดตคำตอบของเจ้าของบนแพลตฟอร์ม แม้แถวเป็น InsertOnly (เมื่อมี/เปลี่ยน)</summary>
        public bool AllowOwnerResponseUpdate;
        /// <summary>ข้อมูลเสริมจากแพลตฟอร์ม (คะแนนย่อย/ไฮไลต์/หัวข้อ/วันที่โดยประมาณ) → Platform_Meta (JSON) ถ้ามีคอลัมน์</summary>
        public Dictionary<string, object> Meta;
    }

    public class ReviewFetchResult
    {
        public List<ReviewItem> Items = new List<ReviewItem>();
        public decimal? PlatformRating;
        public decimal? PlatformRatingScale;
        public int? PlatformReviewCount;
        public List<string> Warnings = new List<string>();
        /// <summary>ข้อความสรุปเพิ่มเติมจากตัวดึง (เช่น จำนวนหน้า)</summary>
        public string Note;
        /// <summary>token AI ที่ใช้ในรอบนี้ (URL_FETCH)</summary>
        public int TokensUsed;
    }

    /// <summary>ผลบันทึกรีวิวหนึ่งรายการ</summary>
    public enum UpsertOutcome { Inserted, Updated, Unchanged, Skipped }

    public class UpsertResult
    {
        public UpsertOutcome Outcome;
        public long ReviewId;
        public decimal? Rating5;
    }

    /// <summary>ผลการดึงหนึ่งแหล่ง (หลังบันทึก)</summary>
    public class SourceRunResult
    {
        public string SourceCode;
        public string Status;      // OK | PARTIAL | FAILED | SKIPPED
        public int Fetched, Inserted, Updated, Unchanged;
        public string Message;
        public List<long> InsertedIds = new List<long>();

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "source", SourceCode }, { "status", Status },
                { "success", Status == "OK" || Status == "PARTIAL" },
                { "fetched", Fetched }, { "inserted", Inserted }, { "updated", Updated },
                { "newCount", Inserted }, { "skipCount", Unchanged },
                { "message", Message }
            };
        }
    }

    /// <summary>ชื่อแพลตฟอร์ม/สเกลคะแนนตั้งต้นต่อรหัสแหล่ง</summary>
    public static class ReviewPlatforms
    {
        private static readonly Dictionary<string, string> Origins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "GOOGLE", "Google" }, { "FACEBOOK", "Facebook" }, { "AGODA", "Agoda" },
            { "BOOKING", "Booking.com" }, { "TRIPADVISOR", "Tripadvisor" }, { "EXPEDIA", "Expedia" },
            { "TRAVELOKA", "Traveloka" }, { "TRIPCOM", "Trip.com" }, { "AIRBNB", "Airbnb" },
            { "PANTIP", "Pantip" }, { "WONGNAI", "Wongnai" }, { "TIKTOK", "TikTok" },
            { "LEMON8", "Lemon8" }, { "TWITTER", "X (Twitter)" }, { "INSTAGRAM", "Instagram" },
            { "YOUTUBE", "YouTube" }, { "INTERNAL", "INTERNAL" }
        };

        public static string OriginFor(string sourceCode, string sourceName = null)
        {
            string o;
            if (!string.IsNullOrEmpty(sourceCode) && Origins.TryGetValue(sourceCode, out o)) return o;
            if (!string.IsNullOrWhiteSpace(sourceName)) return Truncate(sourceName.Trim(), 50);
            return Truncate(sourceCode ?? "UNKNOWN", 50);
        }

        /// <summary>แปลงชื่อแพลตฟอร์มที่เขียนมา (เช่น "booking", "Booking.com") เป็นชื่อมาตรฐาน — ไม่รู้จักคืนค่าเดิม</summary>
        public static string NormalizeOrigin(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim().ToLowerInvariant();
            if (s.Contains("booking")) return "Booking.com";
            if (s.Contains("agoda")) return "Agoda";
            if (s.Contains("expedia") || s.Contains("hotels.com")) return "Expedia";
            if (s.Contains("tripadvisor")) return "Tripadvisor";
            if (s.Contains("trip.com") || s == "tripcom" || s.Contains("ctrip")) return "Trip.com";
            if (s.Contains("traveloka")) return "Traveloka";
            if (s.Contains("google")) return "Google";
            if (s.Contains("airbnb")) return "Airbnb";
            if (s.Contains("facebook")) return "Facebook";
            if (s.Contains("wongnai")) return "Wongnai";
            string o;
            if (Origins.TryGetValue(raw.Trim(), out o)) return o;
            return Truncate(raw.Trim(), 50);
        }

        /// <summary>สเกลคะแนนตั้งต้นของแพลตฟอร์ม (Booking/Agoda/Expedia/Traveloka = 10, อื่น ๆ = 5)</summary>
        public static decimal DefaultScale(string sourceCodeOrOrigin)
        {
            string s = (sourceCodeOrOrigin ?? "").ToLowerInvariant();
            if (s.Contains("booking") || s.Contains("agoda") || s.Contains("expedia") || s.Contains("traveloka"))
                return 10m;
            return 5m;
        }

        internal static string Truncate(string s, int max)
        {
            if (s == null) return null;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
