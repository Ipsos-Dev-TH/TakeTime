// ===========================================================================
// LightFetchDiagnostics.cs — ค่ากลาง + ผลวินิจฉัยของตัวดึงแบบ "อ่านหน้าสาธารณะวันละครั้ง"
//   (AGODA_LIGHT / GOOGLE_TRAVEL_LIGHT) — ใช้ทั้งปุ่ม "ทดสอบดึง (ไม่บันทึก)" และบรรทัดสรุปใน Review_Fetch_Log
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Take_Time_BangPhra.Services.Reviews
{
    /// <summary>กติการ่วมของวิธี light: รอบขั้นต่ำ, ปิดเองเมื่อโดนกันบอทติดกัน, กันกดถี่</summary>
    public static class LightFetch
    {
        /// <summary>รอบดึงขั้นต่ำ (นาที) — อย่างเร็ววันละ 2 ครั้ง</summary>
        public const int MinIntervalMin = 720;
        /// <summary>โดนกันบอทติดกันกี่ครั้งจึงปิดดึงอัตโนมัติ</summary>
        public const int BlockDisableAfter = 3;
        /// <summary>กด "ดึงตอนนี้" ซ้ำได้ไม่ถี่กว่า (นาที)</summary>
        public const int ManualCooldownMin = 30;
        /// <summary>คำนำหน้า log ของรอบที่หน้าโหลดได้แต่แกะรีวิวไม่ได้ — ไม่นับ/ไม่ตัดลำดับ "โดนกันติดกัน"</summary>
        public const string ParseMissPrefix = "[PARSE_MISS]";

        public static bool IsLightMethod(string method)
        {
            return string.Equals(method, IngestMethods.AgodaLight, StringComparison.OrdinalIgnoreCase)
                || string.Equals(method, IngestMethods.GoogleTravelLight, StringComparison.OrdinalIgnoreCase);
        }

        private static readonly Dictionary<string, DateTime> _gate = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _gateLock = new object();

        /// <summary>จองสิทธิ์เรียกปลายทางแบบกดเอง: ห่างกันอย่างน้อย minutes ต่อ key — false = ต้องรอ waitSec วินาที</summary>
        public static bool TryReserveManual(string key, int minutes, out int waitSec)
        {
            waitSec = 0;
            lock (_gateLock)
            {
                DateTime last;
                if (_gate.TryGetValue(key ?? "", out last))
                {
                    double left = minutes * 60 - (DateTime.Now - last).TotalSeconds;
                    if (left > 0) { waitSec = (int)Math.Ceiling(left); return false; }
                }
                _gate[key ?? ""] = DateTime.Now;
                return true;
            }
        }
    }

    /// <summary>ผลวิเคราะห์หนึ่งรอบ (ใช้ทั้งปุ่ม "ทดสอบดึง" และบรรทัดสรุปใน Review_Fetch_Log)</summary>
    public class LightFetchDiagnostics
    {
        public class RequestInfo
        {
            public string Label;      // page | api
            public string Method;
            public string Url;
            public int Status;
            public int Bytes;
            public long Ms;
            public int Hops = 1;
            public string Block;      // ตัวบ่งชี้ว่าโดนกันบอท (null = ไม่พบ)
            public string Error;      // network error
        }

        public class Preview
        {
            public string Name, Origin, ExtId, Date, Title, Text;
            public decimal? Rating, Scale;
            public string Language;
            public bool MachineTranslated;
            /// <summary>คำแปลที่ Agoda แสดง (เมื่อเก็บต้นฉบับแทน)</summary>
            public string TranslatedTitle, TranslatedText;
            /// <summary>GOOGLE_TRAVEL_LIGHT: วันที่แบบสัมพัทธ์ตามหน้า ("4 เดือนที่แล้ว"), ประเภททริป, คะแนนย่อย, คำตอบเจ้าของ</summary>
            public string RelativeDate, TripType, SubRatings, Highlights, OwnerResponse, OwnerResponseDate;
        }

        public bool DryRun;
        /// <summary>วิธีที่รัน (AGODA_LIGHT / GOOGLE_TRAVEL_LIGHT)</summary>
        public string Method = IngestMethods.AgodaLight;
        /// <summary>GOOGLE_TRAVEL_LIGHT: entity token ใน URL, Google place id (ChIJ…), Knowledge-Graph token + mid ที่ถอดได้</summary>
        public string EntityToken, PlaceId, KgToken, KgMid;
        /// <summary>จำนวนรีวิวที่มีรหัสของแพลตฟอร์ม (เช่น postId)</summary>
        public int IdsFound;
        /// <summary>dry-run: รีวิวที่แกะได้ซึ่งยังไม่มีในฐานข้อมูล (null = ไม่ได้ตรวจ)</summary>
        public int? NewCount;
        /// <summary>GOOGLE_TRAVEL_LIGHT: AF_initDataCallback ที่พบ (key + ขนาด), จำนวนลิงก์ /maps/contrib/, โทเคน postId, สตริงไทยยาว</summary>
        public string AfBlocks;
        public int AfBlockCount, ContribCount, PostIdTokens, ThaiLongStrings;
        /// <summary>หน้าโหลดได้ปกติแต่แกะรีวิวไม่ได้ (ไม่ใช่การโดนกัน — ไม่นับลำดับปิดอัตโนมัติ)</summary>
        public bool ParseMiss;
        /// <summary>dry-run: เนื้อหาดิบที่ได้ (label → body) สำหรับให้ Owner/Admin ดาวน์โหลด — ไม่ส่งไปเบราว์เซอร์ใน ToDictionary</summary>
        public List<KeyValuePair<string, string>> RawBodies = new List<KeyValuePair<string, string>>();

        /// <summary>เก็บ body ดิบ (เฉพาะ dry-run, ≤ 8 MB ต่อชิ้น)</summary>
        public void KeepRaw(string label, string body)
        {
            if (!DryRun || body == null) return;
            RawBodies.Add(new KeyValuePair<string, string>(label, body.Length > 8 * 1024 * 1024 ? body.Substring(0, 8 * 1024 * 1024) : body));
        }
        public List<RequestInfo> Requests = new List<RequestInfo>();
        public bool JsonLdFound;
        public int JsonLdBlocks;
        public decimal? Rating, RatingBest;
        public int? RatingCount;
        /// <summary>แหล่งของคะแนนรวม: json-ld | api-summary:&lt;path&gt; | page-state:&lt;key&gt; | summary-endpoint:&lt;path&gt; | none</summary>
        public string RatingSource = "none";
        /// <summary>ค่าที่พบทั้งหมด (path=value (pN)) — ใช้จูนคีย์</summary>
        public List<string> RatingCandidates = new List<string>();
        /// <summary>คีย์ชั้นบนของคำตอบ endpoint (+ คีย์ชั้นถัดไปของ object)</summary>
        public string ApiTopKeys;
        /// <summary>ชื่อฟิลด์ของ comment แรก (ไม่มีค่า ยกเว้นค่าสั้น เช่น languageId / isTranslated / translateSource)</summary>
        public string CommentFields;
        public string CommentShortValues;
        public string PropertyId;
        public string PropertyIdPattern;
        public string PropertyIdCandidates;
        public string ParsePath = "none";
        /// <summary>เส้นทาง HTML (DOM รายการรีวิวที่ render แล้ว): จำนวนบล็อก data-element-name="review-comment" ที่เจอ / ที่แกะได้</summary>
        public int HtmlDomBlocks;
        public int HtmlDomReviews;
        public int ReviewsParsed;
        public List<Preview> Previews = new List<Preview>();
        public List<string> Warnings = new List<string>();
        public List<string> UnknownProviders = new List<string>();
        public bool Blocked;
        public string Error;

        /// <summary>บรรทัดสรุปสั้น ๆ สำหรับ Review_Fetch_Log</summary>
        public string Compact()
        {
            var sb = new StringBuilder(Method ?? "LIGHT");
            if (Requests.Count > 0)
            {
                sb.Append(" req: ");
                sb.Append(string.Join("; ", Requests.Select(r =>
                    r.Label + " " + (r.Error != null ? "ERR" : r.Status.ToString(CultureInfo.InvariantCulture))
                    + " " + (r.Bytes / 1024).ToString(CultureInfo.InvariantCulture) + "KB"
                    + (r.Hops > 1 ? " hops=" + r.Hops : "")
                    + (r.Block != null ? " block=" + r.Block : ""))));
            }
            sb.Append(" · คะแนนรวม ");
            sb.Append(Rating.HasValue
                ? Rating.Value.ToString("0.##", CultureInfo.InvariantCulture) + "/" + (RatingBest ?? 10m).ToString("0", CultureInfo.InvariantCulture)
                  + (RatingCount.HasValue ? " (" + RatingCount.Value + ")" : "") + " [" + RatingSource + "]"
                : "ไม่พบ (JSON-LD " + (JsonLdFound ? "ไม่มี aggregateRating" : "ไม่พบ") + ", ผู้สมัคร " + RatingCandidates.Count + ")");
            if (Method == IngestMethods.GoogleTravelLight)
            {
                sb.Append(" · entity=").Append(EntityToken ?? "?").Append(" placeId=").Append(PlaceId ?? "?");
                if (KgMid != null) sb.Append(" kg=").Append(KgMid);
                sb.Append(" · postId ").Append(IdsFound);
                if (NewCount.HasValue) sb.Append(" · ใหม่ ").Append(NewCount.Value);
                sb.Append(" · AF ").Append(AfBlockCount).Append(" บล็อก, contrib ").Append(ContribCount)
                  .Append(", postId-token ").Append(PostIdTokens).Append(", ไทยยาว ").Append(ThaiLongStrings);
                if (ParseMiss) sb.Append(" · PARSE_MISS");
            }
            else
            {
                sb.Append(" · id=").Append(PropertyId ?? "?");
                if (PropertyIdPattern != null) sb.Append(" (").Append(PropertyIdPattern).Append(")");
            }
            sb.Append(" · html-dom ").Append(HtmlDomReviews > 0 ? "พบ " + HtmlDomReviews
                : (HtmlDomBlocks > 0 ? "บล็อก " + HtmlDomBlocks + " แต่แกะไม่ได้" : "ไม่พบ"));
            sb.Append(" · path=").Append(ParsePath).Append(" · parsed ").Append(ReviewsParsed);
            if (UnknownProviders.Count > 0) sb.Append(" · providerId ไม่รู้จัก: ").Append(string.Join(",", UnknownProviders.Take(5)));
            return sb.ToString();
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "dryRun", DryRun }, { "method", Method },
                { "entityToken", EntityToken }, { "placeId", PlaceId }, { "kgToken", KgToken }, { "kgMid", KgMid },
                { "idsFound", IdsFound }, { "newCount", NewCount },
                { "afBlocks", AfBlocks }, { "afBlockCount", AfBlockCount }, { "contribCount", ContribCount },
                { "postIdTokens", PostIdTokens }, { "thaiLongStrings", ThaiLongStrings }, { "parseMiss", ParseMiss },
                { "rawSaved", false },
                { "requests", Requests.Select(r => (object)new Dictionary<string, object>
                    {
                        { "label", r.Label }, { "method", r.Method }, { "url", r.Url }, { "status", r.Status },
                        { "bytes", r.Bytes }, { "ms", r.Ms }, { "hops", r.Hops }, { "block", r.Block }, { "error", r.Error }
                    }).ToList() },
                { "jsonLdFound", JsonLdFound }, { "jsonLdBlocks", JsonLdBlocks },
                { "rating", Rating }, { "ratingBest", RatingBest }, { "ratingCount", RatingCount },
                { "ratingSource", RatingSource }, { "ratingCandidates", RatingCandidates },
                { "apiTopKeys", ApiTopKeys }, { "commentFields", CommentFields }, { "commentShortValues", CommentShortValues },
                { "propertyId", PropertyId }, { "propertyIdPattern", PropertyIdPattern },
                { "propertyIdCandidates", PropertyIdCandidates },
                { "parsePath", ParsePath }, { "reviewsParsed", ReviewsParsed },
                { "htmlDomBlocks", HtmlDomBlocks }, { "htmlDomReviews", HtmlDomReviews },
                { "previews", Previews.Select(p => (object)new Dictionary<string, object>
                    {
                        { "name", p.Name }, { "origin", p.Origin }, { "extId", p.ExtId }, { "date", p.Date },
                        { "rating", p.Rating }, { "scale", p.Scale }, { "title", p.Title }, { "text", p.Text },
                        { "language", p.Language }, { "machineTranslated", p.MachineTranslated },
                        { "translatedTitle", p.TranslatedTitle }, { "translatedText", p.TranslatedText },
                        { "relativeDate", p.RelativeDate }, { "tripType", p.TripType }, { "subRatings", p.SubRatings },
                        { "highlights", p.Highlights }, { "ownerResponse", p.OwnerResponse }, { "ownerResponseDate", p.OwnerResponseDate }
                    }).ToList() },
                { "warnings", Warnings }, { "unknownProviders", UnknownProviders },
                { "blocked", Blocked }, { "error", Error }, { "compact", Compact() }
            };
        }
    }
}
