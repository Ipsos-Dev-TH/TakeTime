using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Take_Time_BangPhra.Payments.SlipCheck
{
    /// <summary>
    /// อ่าน mini-QR ของสลิปโอนเงินในเครื่อง (ฟรี ไม่ใช้เน็ต) — ได้ธนาคารผู้โอน + รหัสอ้างอิง (transRef) ไว้กันสลิปซ้ำ
    /// และส่ง payload ให้ SlipOK แทนรูป (ส่งข้อมูลส่วนบุคคลน้อยกว่า)
    ///
    /// รูปแบบ payload (EMV TLV: tag 2 หลัก + ความยาว 2 หลัก + ค่า) ตามเอกสาร SCB "Extracting data from mini QR"
    /// (https://developer.scb/assets/documents/documentation/qr-payment/extracting-data-from-mini-qr.pdf
    ///  ซึ่งทำตามภาคผนวก Thai QR Payment ของ ธปท.) ยืนยันกับ github.com/nuttakulsv/promptpay-slip-parser:
    ///   00 LL [ 00 06 000001 (API id Verify Pay Slip) · 01 03 รหัสธนาคารผู้โอน · 02 NN transRef ]
    ///   51 02 TH · 91 04 CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF) คำนวณถึงและรวม "9104"
    /// ตัวอย่างจริง (สลิป SCB การจอง #149286): 0046000600000101030140225202608192jhSpp0xi8fQ6T5y95102TH91042D47
    ///   → ธนาคาร 014, transRef 202608192jhSpp0xi8fQ6T5y9, CRC 2D47 (คำนวณตรง)
    /// QR ไม่มียอดเงิน/วันที่/ชื่อ — CRC ถูกแปลว่ารูปแบบถูก ไม่ได้แปลว่าเงินเข้าจริง
    ///
    /// ตัวถอดรหัสรูป = ZXing.Net (NuGet ZXing.Net 0.16.x, lib\net47\zxing.dll — managed ล้วน รันบน IIS ได้)
    /// เรียกผ่าน <see cref="ZxingBridge"/> ที่แยกเมธอด (NoInlining) — ไม่มี zxing.dll ในเครื่อง = คืน null ไม่ทำหน้าพัง
    /// </summary>
    public static class SlipQrDecoder
    {
        private static volatile string _engineError;

        /// <summary>สถานะตัวอ่าน QR (หน้าตั้งค่า)</summary>
        public static string EngineStatus(out bool ready)
        {
            try
            {
                string v = ZxingBridge.Version();
                ready = true;
                return "ZXing.Net " + v;
            }
            catch (Exception ex)
            {
                ready = false;
                return "ไม่พบ zxing.dll (ติดตั้ง NuGet ZXing.Net) — " + ex.GetType().Name;
            }
        }

        /// <summary>หา QR ในรูปแล้วแปลความ — คืน null ถ้าไม่พบ/อ่านไม่ได้ (ไม่ throw)</summary>
        public static SlipQrInfo TryDecode(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length < 100) return null;
            string text = null;
            try
            {
                using (var ms = new MemoryStream(imageBytes))
                using (var img = Image.FromStream(ms, false, false))
                using (var bmp = new Bitmap(img))
                {
                    text = DecodeAnyScale(bmp);
                }
            }
            catch (Exception ex)
            {
                // ไม่ใช่รูป (เช่น PDF) / zxing.dll ไม่มี / System.Drawing ล้ม → ไม่มี QR
                _engineError = ex.GetType().Name + ": " + ex.Message;
                return null;
            }
            return string.IsNullOrEmpty(text) ? null : Parse(text);
        }

        public static string LastEngineError => _engineError;

        private static string DecodeAnyScale(Bitmap bmp)
        {
            // 1) ตามจริง 2) ขยาย 2 เท่า (QR สลิปเล็ก) 3) ครึ่งล่าง (QR มักอยู่ล่าง) ขยาย 2 เท่า 4) ล่างขวา
            string t = ZxingBridge.Decode(bmp);
            if (!string.IsNullOrEmpty(t)) return t;
            if (bmp.Width < 1600)
            {
                using (var big = Scale(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height), 2.0))
                {
                    t = ZxingBridge.Decode(big);
                    if (!string.IsNullOrEmpty(t)) return t;
                }
            }
            var lower = new Rectangle(0, bmp.Height / 2, bmp.Width, bmp.Height - bmp.Height / 2);
            using (var crop = Scale(bmp, lower, bmp.Width < 1600 ? 2.0 : 1.0))
            {
                t = ZxingBridge.Decode(crop);
                if (!string.IsNullOrEmpty(t)) return t;
            }
            var lowerRight = new Rectangle(bmp.Width / 2, bmp.Height / 2, bmp.Width - bmp.Width / 2, bmp.Height - bmp.Height / 2);
            using (var crop = Scale(bmp, lowerRight, bmp.Width < 1600 ? 2.5 : 1.5))
            {
                t = ZxingBridge.Decode(crop);
            }
            return t;
        }

        private static Bitmap Scale(Bitmap src, Rectangle area, double factor)
        {
            int w = Math.Max(1, (int)(area.Width * factor));
            int h = Math.Max(1, (int)(area.Height * factor));
            var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;   // ขอบโมดูล QR คม
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, new Rectangle(0, 0, w, h), area, GraphicsUnit.Pixel);
            }
            return dst;
        }

        /// <summary>แปลง payload → ข้อมูลสลิป (ทดสอบได้โดยไม่ต้องมีรูป)</summary>
        public static SlipQrInfo Parse(string payload)
        {
            var info = new SlipQrInfo { RawPayload = (payload ?? "").Trim() };
            string p = info.RawPayload;
            if (p.Length < 8) return info;

            // QR รับเงิน PromptPay (Payload Format Indicator 000201) — ลูกค้าแนบรูป QR ของร้านมาแทนสลิป
            if (p.StartsWith("000201", StringComparison.Ordinal))
            {
                info.IsPaymentQr = true;
                return info;
            }

            var top = ParseTlv(p);
            if (top == null || !top.ContainsKey("00")) return info;
            info.CrcValid = CheckCrc(p);
            if (top.ContainsKey("51")) info.CountryCode = top["51"];

            var sub = ParseTlv(top["00"]);
            if (sub == null) return info;
            string s00 = sub.ContainsKey("00") ? sub["00"] : null;
            info.ApiId = s00;
            if (s00 == "000001")
            {
                info.IsSlip = true;
                info.SendingBank = sub.ContainsKey("01") ? sub["01"] : null;
                info.TransRef = sub.ContainsKey("02") ? sub["02"] : null;
            }
            else if (s00 == "01" && sub.ContainsKey("03"))
            {
                // TrueMoney: 00=01, 01=01, 02=ชนิดรายการ, 03=เลขรายการ, 04=DDMMYYYY
                info.IsSlip = true;
                info.IsTrueMoney = true;
                info.SendingBank = "TMN";
                info.TransRef = sub["03"];
            }
            if (!string.IsNullOrEmpty(info.TransRef) && info.TransRef.Length > 40)
                info.TransRef = info.TransRef.Substring(0, 40);
            return info;
        }

        private static Dictionary<string, string> ParseTlv(string s)
        {
            var map = new Dictionary<string, string>();
            int i = 0;
            while (i + 4 <= s.Length)
            {
                string tag = s.Substring(i, 2);
                int len;
                if (!int.TryParse(s.Substring(i + 2, 2), out len) || len < 0) return map.Count > 0 ? map : null;
                if (i + 4 + len > s.Length)
                {
                    // แอปบางธนาคารตัดเลข 0 นำหน้า CRC (ค่าสั้นกว่าความยาวที่ประกาศ) — รับส่วนที่เหลือไว้
                    map[tag] = s.Substring(i + 4);
                    break;
                }
                map[tag] = s.Substring(i + 4, len);
                i += 4 + len;
            }
            return map;
        }

        /// <summary>CRC-16/CCITT-FALSE ของทุกอย่างก่อนค่า CRC (รวม "9104") เทียบกับค่าในแท็ก 91 (เติม 0 ด้านหน้าให้ครบ 4)</summary>
        public static bool CheckCrc(string payload)
        {
            int idx = payload.LastIndexOf("9104", StringComparison.Ordinal);
            if (idx < 0) return false;
            string body = payload.Substring(0, idx + 4);
            string given = payload.Substring(idx + 4).Trim().PadLeft(4, '0').ToUpperInvariant();
            return Crc16(body).ToString("X4") == given;
        }

        public static int Crc16(string s)
        {
            int crc = 0xFFFF;
            foreach (byte b in Encoding.ASCII.GetBytes(s))
            {
                crc ^= b << 8;
                for (int k = 0; k < 8; k++)
                    crc = (crc & 0x8000) != 0 ? ((crc << 1) ^ 0x1021) & 0xFFFF : (crc << 1) & 0xFFFF;
            }
            return crc;
        }
    }

    /// <summary>
    /// จุดเดียวที่อ้างชนิดของ ZXing.Net — ทุกเมธอด NoInlining เพื่อให้ JIT โหลด zxing.dll เฉพาะตอนเรียกจริง
    /// (ไม่มีไฟล์ = FileNotFoundException ที่ผู้เรียกจับได้ ไม่ใช่ทั้งคลาสผู้เรียกพัง)
    /// ใช้ core API (RGBLuminanceSource/HybridBinarizer/QRCodeReader) ที่คงที่ทุกรุ่น 0.16.x
    /// </summary>
    internal static class ZxingBridge
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Version()
        {
            return typeof(ZXing.QrCode.QRCodeReader).Assembly.GetName().Version.ToString();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Decode(Bitmap bmp)
        {
            if (bmp == null) return null;
            byte[] raw;
            int w = bmp.Width, h = bmp.Height;
            using (var b32 = bmp.PixelFormat == PixelFormat.Format32bppArgb ? null : new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                Bitmap src = bmp;
                if (b32 != null)
                {
                    using (var g = Graphics.FromImage(b32)) { g.Clear(Color.White); g.DrawImage(bmp, 0, 0, w, h); }
                    src = b32;
                }
                var data = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    raw = new byte[Math.Abs(data.Stride) * h];
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                }
                finally { src.UnlockBits(data); }
            }

            var hints = new Dictionary<ZXing.DecodeHintType, object>
            {
                { ZXing.DecodeHintType.TRY_HARDER, true },
                { ZXing.DecodeHintType.POSSIBLE_FORMATS, new List<ZXing.BarcodeFormat> { ZXing.BarcodeFormat.QR_CODE } }
            };
            ZXing.LuminanceSource lum = new ZXing.RGBLuminanceSource(raw, w, h, ZXing.RGBLuminanceSource.BitmapFormat.BGRA32);
            var reader = new ZXing.QrCode.QRCodeReader();
            for (int pass = 0; pass < 4; pass++)
            {
                ZXing.LuminanceSource src = pass >= 2 ? lum.invert() : lum;
                ZXing.Binarizer bin = pass % 2 == 0
                    ? (ZXing.Binarizer)new ZXing.Common.HybridBinarizer(src)
                    : new ZXing.Common.GlobalHistogramBinarizer(src);
                try
                {
                    var res = reader.decode(new ZXing.BinaryBitmap(bin), hints);
                    if (res != null && !string.IsNullOrEmpty(res.Text)) return res.Text;
                }
                catch (Exception) { /* รุ่นเก่าบางรุ่น throw แทนคืน null */ }
                reader.reset();
            }
            return null;
        }
    }
}
