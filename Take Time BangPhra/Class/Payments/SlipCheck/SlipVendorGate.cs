using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Take_Time_BangPhra.Payments.SlipCheck
{
    /// <summary>
    /// กติกา "จะเรียก SlipOK ไหม / ใช้ cache เดิมได้ไหม / ส่งอะไรไป" — ตรรกะบริสุทธิ์ (ไม่แตะ DB/HTTP, ทดสอบด้วย mono ได้)
    /// ที่มา (เคสเจ้าของ ต.ค. 2026): ทดสอบสลิปก่อนบันทึกคีย์ → cache 60 นาทีจำว่า "ยังไม่ได้ตั้งค่า SlipOK" → บันทึกคีย์แล้วทดสอบรูปเดิม
    /// ก็ยังไม่เรียก SlipOK (โควต้าไม่ลด, ไม่มียอดจากธนาคาร). แก้: cache key ผูก fingerprint ของค่าตั้ง + รายการที่ "ข้าม" ถือว่าเก่าเมื่อตอนนี้เรียกได้แล้ว
    /// </summary>
    public static class SlipVendorGate
    {
        public const string SkipNotConfigured = "ยังไม่ได้ตั้งค่า SlipOK (Branch ID / API key ว่าง)";
        public const string SkipNotASlip = "ไม่ใช่สลิป (รูปเป็น QR รับเงิน)";

        /// <summary>
        /// เหตุผลที่ข้าม SlipOK (null = เรียกได้) — ข้อความระบุที่มาชัด (ธงจาก SlipOK / ตัวนับในระบบ / ค่าตั้งโควต้า 0) ให้แก้ถูกจุด
        /// </summary>
        public static string SkipReason(bool configured, bool exhaustedFlag, int used, int quota, bool isPaymentQr, string yearMonth)
        {
            if (!configured) return SkipNotConfigured;
            if (exhaustedFlag)
                return "SlipOK แจ้งโควต้า/แพ็กเกจหมดแล้วเดือน " + (yearMonth ?? "") + " (ธง Exhausted ใน Slip_Verify_Usage — ต่อแพ็กเกจแล้วกด \"ล้างธงโควต้าหมด\")";
            if (quota <= 0) return "ตั้งค่า SlipOK_Monthly_Quota = 0 (ปิดใช้ SlipOK — ใส่โควต้าต่อเดือน เช่น 100)";
            if (used >= quota)
                return "ตัวนับในระบบเดือน " + (yearMonth ?? "") + " ใช้ครบ " + used.ToString(CultureInfo.InvariantCulture) + "/"
                    + quota.ToString(CultureInfo.InvariantCulture) + " (SlipOK_Monthly_Quota — ถ้า SlipOK ยังมีโควต้าให้เพิ่มค่านี้)";
            if (isPaymentQr) return SkipNotASlip;
            return null;
        }

        /// <summary>ส่ง QR payload (field data) แทนรูปเฉพาะเมื่ออ่านได้ครบและ CRC ถูก — ไม่งั้นส่งรูปให้ SlipOK อ่าน QR เอง</summary>
        public static bool UsePayload(SlipQrInfo qr)
        {
            return qr != null && qr.IsSlip && !qr.IsTrueMoney && qr.CrcValid && !string.IsNullOrEmpty(qr.RawPayload);
        }

        /// <summary>ส่ง data แล้ว SlipOK บอกไม่มี QR / ไม่พบรายการ → ลองส่งรูปอีกครั้ง (error ไม่เสียโควต้า — API Guide หน้า 6)</summary>
        public static bool ShouldRetryWithFile(string kind)
        {
            return kind == SlipVendorResponse.KindNoQr || kind == SlipVendorResponse.KindNotFound;
        }

        /// <summary>ลายนิ้วมือค่าตั้ง (ไม่เปิดเผยคีย์) — เปลี่ยน Branch/คีย์/โควต้า = cache เดิมใช้ไม่ได้</summary>
        public static string ConfigFingerprint(string branchId, string apiKey, int quota)
        {
            string s = (branchId ?? "").Trim() + "\n" + (apiKey ?? "").Trim() + "\n" + quota.ToString(CultureInfo.InvariantCulture);
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                return BitConverter.ToString(h, 0, 4).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string CacheKey(string fileHash, string configFingerprint)
        {
            return "slipinsp:" + (configFingerprint ?? "-") + ":" + fileHash;
        }

        /// <summary>
        /// รายการ cache ที่ "ข้าม SlipOK" (Vendor == null) แต่ตอนนี้ไม่มีเหตุให้ข้ามแล้ว (ล้างธง/เดือนใหม่/เพิ่มโควต้า) = เก่า ต้องตรวจใหม่
        /// </summary>
        public static bool IsStaleSkip(SlipInspection cached, string currentSkipReason)
        {
            return cached != null && cached.Vendor == null && !string.IsNullOrEmpty(cached.VendorSkipReason) && currentSkipReason == null;
        }
    }
}
