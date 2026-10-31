using System;
using System.Security.Cryptography;
using System.Text;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>
    /// token และตัวช่วยความปลอดภัยของ QR สั่งอาหาร — <b>บริสุทธิ์</b> (ทดสอบด้วย mono ได้)
    ///   • QR_Token ของโต๊ะ / Session_Token ของแขก = 32 ไบต์จาก CSPRNG → base64url 43 ตัว (ไม่มี = + / =)
    ///   • เทียบ token แบบ constant-time (กัน timing attack)
    ///   • normalize เบอร์โทร (ตัวเลขล้วน, +66/66 → 0) สำหรับยืนยันลงบิลห้อง
    /// </summary>
    public static class DiningTokens
    {
        public const int TokenBytes = 32;
        public const int TokenLength = 43;   // base64url ของ 32 ไบต์ ไม่มี padding

        /// <summary>token ใหม่ 43 ตัว [A-Za-z0-9_-]</summary>
        public static string NewToken()
        {
            var buf = new byte[TokenBytes];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(buf);
            return Base64Url(buf);
        }

        public static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>รูปแบบ token ถูกต้อง (ความยาว 43 + อักขระ base64url) — ใช้กรองก่อนค้น DB</summary>
        public static bool IsValidToken(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length != TokenLength) return false;
            foreach (char c in token)
            {
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>เทียบสองสตริงโดยใช้เวลาคงที่ (ไม่ขึ้นกับตำแหน่งที่ต่างกัน)</summary>
        public static bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            byte[] x = Encoding.UTF8.GetBytes(a), y = Encoding.UTF8.GetBytes(b);
            int diff = x.Length ^ y.Length;
            int n = Math.Min(x.Length, y.Length);
            for (int i = 0; i < n; i++) diff |= x[i] ^ y[i];
            return diff == 0;
        }

        /// <summary>
        /// เบอร์โทรเป็นตัวเลขล้วน: ตัดทุกอย่างที่ไม่ใช่ตัวเลข, "+66812345678"/"66812345678" → "0812345678",
        /// "0066…" → "0…". ว่าง/ไม่มีตัวเลข = ""
        /// </summary>
        public static string NormalizePhone(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (c >= '0' && c <= '9') sb.Append(c);
                else if (c >= '๐' && c <= '๙') sb.Append((char)('0' + (c - '๐')));   // เลขไทย
            }
            string d = sb.ToString();
            if (d.StartsWith("0066") && d.Length >= 12) d = "0" + d.Substring(4);
            else if (d.StartsWith("66") && d.Length >= 11) d = "0" + d.Substring(2);
            return d;
        }

        /// <summary>เบอร์ตรงกัน (หลัง normalize; ต้องมีอย่างน้อย 9 หลักทั้งคู่)</summary>
        public static bool PhoneMatches(string typed, string stored)
        {
            string a = NormalizePhone(typed), b = NormalizePhone(stored);
            if (a.Length < 9 || b.Length < 9) return false;
            return ConstantTimeEquals(a, b);
        }

        /// <summary>"08x-xxx-1234" → "08*****234" สำหรับ log</summary>
        public static string MaskPhone(string raw)
        {
            string d = NormalizePhone(raw);
            if (d.Length <= 4) return new string('*', d.Length);
            int keepHead = Math.Min(2, d.Length - 4);
            return d.Substring(0, keepHead) + new string('*', d.Length - keepHead - 3) + d.Substring(d.Length - 3);
        }

        /// <summary>SHA-256 hex (ตัวพิมพ์เล็ก) — ใช้ทำ Device_Hash จาก user-agent + ip หรือคีย์กันซ้ำ</summary>
        public static string Sha256Hex(string s)
        {
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(s ?? ""));
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Device_Hash (64 ตัว) จาก user-agent + ip — ไม่เก็บข้อมูลดิบ</summary>
        public static string DeviceHash(string userAgent, string ip)
        {
            return Sha256Hex((userAgent ?? "") + "|" + (ip ?? ""));
        }

        /// <summary>คีย์กันสั่งซ้ำที่ client ส่งมา — ยอมรับ 8-64 ตัว [A-Za-z0-9_-]; อื่น ๆ = null (ไม่ใช้)</summary>
        public static string SanitizeIdempotencyKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            key = key.Trim();
            if (key.Length < 8 || key.Length > 64) return null;
            foreach (char c in key)
            {
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok) return null;
            }
            return key;
        }
    }
}
