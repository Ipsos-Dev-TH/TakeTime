// ===========================================================================
// ReviewSourceConfig.cs — อ่าน/เขียน AI_Review_Sources + ApiConfig (JSON)
//
// ความลับ (apiKey / clientSecret / refreshToken / pageAccessToken ...) อยู่ใน ApiConfig เดิม แต่:
//   • บันทึกแบบเข้ารหัส DPAPI (prefix "dpapi:") — ค่าเก่าที่เป็นข้อความเปล่ายังอ่านได้
//   • ไม่ส่งไปเบราว์เซอร์: MaskedJson() แทนค่าด้วย ••••xxxx
//   • บันทึกจากหน้าเว็บ: ช่องว่าง/ค่าที่ถูก mask = คงค่าเดิม, "__CLEAR__" = ลบ
// ===========================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Services.Reviews
{
    public static class ReviewSourceConfig
    {
        public const string MaskPrefix = "••••";
        public const string ClearToken = "__CLEAR__";
        private const string DpapiPrefix = "dpapi:";
        private const string B64Prefix = "b64:";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TakeTime.ReviewSources.v1");

        private static JavaScriptSerializer Json()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        }

        /// <summary>คีย์ที่ถือเป็นความลับ (ดูจากชื่อ)</summary>
        public static bool IsSecretKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            string k = key.ToLowerInvariant();
            return k == "apikey" || k == "key" || k.Contains("secret") || k.Contains("token") || k.Contains("password");
        }

        // ── อ่าน ──────────────────────────────────────────────────────────────

        /// <summary>แปลง ApiConfig เป็น dictionary พร้อมถอดรหัสความลับ (ใช้ฝั่ง server เท่านั้น)</summary>
        public static Dictionary<string, object> Parse(string json)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return result;
            Dictionary<string, object> raw;
            try { raw = Json().Deserialize<Dictionary<string, object>>(json); }
            catch { return result; }
            if (raw == null) return result;
            foreach (var kv in raw)
            {
                object v = kv.Value;
                if (v is string && IsSecretKey(kv.Key)) v = Unprotect((string)v);
                result[kv.Key] = v;
            }
            return result;
        }

        /// <summary>ApiConfig สำหรับส่งไปเบราว์เซอร์ — ความลับถูกแทนด้วย ••••xxxx (หรือ "" ถ้าไม่ได้ตั้ง)</summary>
        public static string MaskedJson(string json)
        {
            var d = Parse(json);
            var outD = new Dictionary<string, object>();
            foreach (var kv in d)
                outD[kv.Key] = IsSecretKey(kv.Key) ? (object)Mask(kv.Value as string) : kv.Value;
            return Json().Serialize(outD);
        }

        public static Dictionary<string, object> MaskedDictionary(string json)
        {
            var d = Parse(json);
            var outD = new Dictionary<string, object>();
            foreach (var kv in d)
                outD[kv.Key] = IsSecretKey(kv.Key) ? (object)Mask(kv.Value as string) : kv.Value;
            return outD;
        }

        public static string Mask(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            if (plain.Length <= 8) return MaskPrefix;
            return MaskPrefix + plain.Substring(plain.Length - 4);
        }

        public static bool IsMasked(string v)
        {
            return !string.IsNullOrEmpty(v) && v.StartsWith(MaskPrefix, StringComparison.Ordinal);
        }

        // ── เขียน ─────────────────────────────────────────────────────────────

        /// <summary>
        /// รวมค่าที่ส่งมาจากหน้าเว็บกับค่าเดิม แล้วคืน JSON สำหรับบันทึก (ความลับถูกเข้ารหัส)
        /// </summary>
        /// <param name="allowSecretChange">false = ผู้ใช้ไม่ใช่ Owner/Admin → ความลับคงค่าเดิมเสมอ</param>
        public static string MergeForSave(string oldJson, IDictionary<string, object> incoming, bool allowSecretChange)
        {
            var old = Parse(oldJson);
            var merged = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            // คีย์ที่ไม่ใช่ความลับ: ใช้ชุดที่ส่งมาเป็นหลัก (ลบคีย์ที่ไม่ได้ส่งมาได้) / ความลับ: คงไว้เสมอถ้าไม่ได้ส่งค่าใหม่
            foreach (var kv in old)
                if (IsSecretKey(kv.Key)) merged[kv.Key] = kv.Value;

            if (incoming != null)
                foreach (var kv in incoming)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    if (IsSecretKey(kv.Key))
                    {
                        if (!allowSecretChange) continue;
                        string v = kv.Value == null ? "" : kv.Value.ToString().Trim();
                        if (v == ClearToken) { merged.Remove(kv.Key); continue; }
                        if (v.Length == 0 || IsMasked(v)) continue;   // คงค่าเดิม
                        merged[kv.Key] = v;
                    }
                    else merged[kv.Key] = kv.Value;
                }
            else
                foreach (var kv in old)
                    if (!IsSecretKey(kv.Key)) merged[kv.Key] = kv.Value;

            var stored = new Dictionary<string, object>();
            foreach (var kv in merged)
                stored[kv.Key] = (IsSecretKey(kv.Key) && kv.Value is string) ? Protect((string)kv.Value) : kv.Value;
            return Json().Serialize(stored);
        }

        /// <summary>รวมแบบรับ JSON ทั้งก้อน (หน้า AIReviewDashboard เดิมส่ง ApiConfig เป็นข้อความ)</summary>
        public static string MergeJsonForSave(string oldJson, string newJson, bool allowSecretChange)
        {
            if (string.IsNullOrWhiteSpace(newJson)) return oldJson;
            Dictionary<string, object> incoming;
            try { incoming = Json().Deserialize<Dictionary<string, object>>(newJson); }
            catch { throw new ArgumentException("ApiConfig ต้องเป็น JSON object"); }
            return MergeForSave(oldJson, incoming, allowSecretChange);
        }

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return plain;
            if (plain.StartsWith(DpapiPrefix) || plain.StartsWith(B64Prefix)) return plain;
            try
            {
                byte[] p = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.LocalMachine);
                return DpapiPrefix + Convert.ToBase64String(p);
            }
            catch
            {
                // DPAPI ใช้ไม่ได้ (ไม่ใช่ Windows) — อย่างน้อยไม่เก็บข้อความเปล่า
                return B64Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));
            }
        }

        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return stored;
            try
            {
                if (stored.StartsWith(DpapiPrefix))
                {
                    byte[] p = ProtectedData.Unprotect(Convert.FromBase64String(stored.Substring(DpapiPrefix.Length)),
                        Entropy, DataProtectionScope.LocalMachine);
                    return Encoding.UTF8.GetString(p);
                }
                if (stored.StartsWith(B64Prefix))
                    return Encoding.UTF8.GetString(Convert.FromBase64String(stored.Substring(B64Prefix.Length)));
            }
            catch { return ""; }
            return stored;   // ค่าเก่า: ข้อความเปล่า
        }

        // ── แถวแหล่งรีวิว ─────────────────────────────────────────────────────

        public static List<ReviewSourceRow> LoadAll(string conn)
        {
            var list = new List<ReviewSourceRow>();
            var dt = new code().DatabaseQuerySafe(conn, "SELECT * FROM AI_Review_Sources ORDER BY SourceCode", null);
            foreach (DataRow r in dt.Rows) list.Add(FromRow(r));
            return list;
        }

        public static ReviewSourceRow Load(string conn, string sourceCode)
        {
            var dt = new code().DatabaseQuerySafe(conn, "SELECT * FROM AI_Review_Sources WHERE SourceCode = @c",
                new Dictionary<string, object> { { "@c", sourceCode ?? "" } });
            return dt.Rows.Count == 0 ? null : FromRow(dt.Rows[0]);
        }

        public static ReviewSourceRow FromRow(DataRow r)
        {
            var s = new ReviewSourceRow
            {
                Id = Convert.ToInt32(r["ID"]),
                SourceCode = Str(r, "SourceCode"),
                SourceName = Str(r, "SourceName"),
                IngestMethod = Str(r, "Ingest_Method"),
                FetchIntervalMin = Int(r, "Fetch_Interval_Min") ?? 1440,
                IsAutoEnabled = Bool(r, "Is_Enabled"),
                IsLegacyEnabled = Bool(r, "IsEnabled"),
                SourceUrl = Str(r, "Source_Url"),
                NextFetchAt = Date(r, "Next_Fetch_At"),
                LastSuccessAt = Date(r, "Last_Success_At"),
                LastFetchDate = Date(r, "LastFetchDate"),
                LastError = Str(r, "Last_Error"),
                ConsecutiveFailures = Int(r, "Consecutive_Failures") ?? 0,
                PlatformRating = Dec(r, "Platform_Rating"),
                PlatformRatingScale = Dec(r, "Platform_Rating_Scale"),
                PlatformReviewCount = Int(r, "Platform_Review_Count"),
                TotalReviews = Int(r, "TotalReviews") ?? 0,
                Config = Parse(Str(r, "ApiConfig"))
            };
            if (s.FetchIntervalMin < 15) s.FetchIntervalMin = 15;
            // วิธี light (AGODA_LIGHT / GOOGLE_TRAVEL_LIGHT): อย่างเร็ววันละ 2 ครั้ง ไม่ว่าจะตั้งค่าไว้เท่าไร (บังคับซ้ำที่หน้าแอดมิน)
            if (LightFetch.IsLightMethod(s.IngestMethod) && s.FetchIntervalMin < LightFetch.MinIntervalMin)
                s.FetchIntervalMin = LightFetch.MinIntervalMin;
            return s;
        }

        private static string Str(DataRow r, string c)
        {
            return r.Table.Columns.Contains(c) && r[c] != DBNull.Value ? r[c].ToString() : null;
        }
        private static int? Int(DataRow r, string c)
        {
            if (!r.Table.Columns.Contains(c) || r[c] == DBNull.Value) return null;
            try { return Convert.ToInt32(r[c]); } catch { return null; }
        }
        private static decimal? Dec(DataRow r, string c)
        {
            if (!r.Table.Columns.Contains(c) || r[c] == DBNull.Value) return null;
            try { return Convert.ToDecimal(r[c]); } catch { return null; }
        }
        private static bool Bool(DataRow r, string c)
        {
            if (!r.Table.Columns.Contains(c) || r[c] == DBNull.Value) return false;
            try { return Convert.ToBoolean(r[c]); } catch { return false; }
        }
        private static DateTime? Date(DataRow r, string c)
        {
            if (!r.Table.Columns.Contains(c) || r[c] == DBNull.Value) return null;
            try { return Convert.ToDateTime(r[c]); } catch { return null; }
        }

        // ── ตรวจว่ารัน migration PHASE19_26 แล้วหรือยัง (cache 5 นาที) ──────────────
        private static bool? _schemaReady;
        private static DateTime _schemaCheckedAt = DateTime.MinValue;
        private static readonly object _schemaLock = new object();

        public static bool SchemaReady(string conn)
        {
            lock (_schemaLock)
            {
                if (_schemaReady.HasValue && (DateTime.Now - _schemaCheckedAt).TotalMinutes < 5)
                    return _schemaReady.Value;
            }
            bool ok = false;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    @"SELECT CASE WHEN COL_LENGTH('dbo.AI_Online_Reviews','Is_Excluded') IS NOT NULL
                                   AND COL_LENGTH('dbo.AI_Review_Sources','Lease_Until') IS NOT NULL
                                   AND OBJECT_ID('dbo.Review_Fetch_Log','U') IS NOT NULL THEN 1 ELSE 0 END AS Ok", null);
                ok = dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 1;
            }
            catch { ok = false; }
            lock (_schemaLock) { _schemaReady = ok; _schemaCheckedAt = DateTime.Now; }
            return ok;
        }

        public static void InvalidateSchemaCache()
        {
            lock (_schemaLock) { _schemaReady = null; }
        }
    }
}
