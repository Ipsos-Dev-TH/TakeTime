using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>
    /// QR ประจำโต๊ะ — ลิงก์ + รูป SVG ฝั่ง server ด้วย ZXing.Net 0.16.11 (<c>ZXing.BarcodeWriterSvg</c>, managed ล้วน ไม่พึ่ง GDI,
    /// ไม่ส่ง token ออกนอกเครื่อง — ต่างจาก RoomQRGenerator ที่เรียก api.qrserver.com)
    /// ไม่มี zxing.dll ในเครื่อง = <see cref="TryRenderSvg"/> คืน false (หน้าพิมพ์ fallback ไป qrcodejs ฝั่ง browser ได้)
    /// </summary>
    public static class DiningQr
    {
        /// <summary>ลิงก์ที่ฝังใน QR: {SiteBase}/Order/{token} (route Order/{token} → Order/Default.aspx?t= — ทีมหน้าลูกค้าเพิ่มใน RouteConfig)</summary>
        public static string BuildOrderUrl(string tableToken)
        {
            string b = DiningConfig.SiteBaseSafe();
            if (string.IsNullOrEmpty(b)) b = "";
            return b + "/Order/" + (tableToken ?? "");
        }

        /// <summary>ลิงก์หน้าสถานะของ session (ส่งทาง LINE/ปุ่ม "เปิดหน้าสถานะ")</summary>
        public static string BuildStatusUrl(string sessionToken)
        {
            string b = DiningConfig.SiteBaseSafe();
            return (b ?? "") + "/Order/Status?s=" + (sessionToken ?? "");
        }

        /// <summary>SVG ของ QR (string ทั้งก้อน เริ่มที่ &lt;svg) — null เมื่อ ZXing ใช้ไม่ได้</summary>
        public static string RenderSvg(string text, int sizePx)
        {
            string svg;
            return TryRenderSvg(text, sizePx, out svg) ? svg : null;
        }

        public static bool TryRenderSvg(string text, int sizePx, out string svg)
        {
            svg = null;
            if (string.IsNullOrEmpty(text)) return false;
            if (sizePx < 64) sizePx = 64;
            if (sizePx > 2048) sizePx = 2048;
            try
            {
                string raw = ZxingSvgBridge.Render(text, sizePx);
                if (string.IsNullOrEmpty(raw)) return false;
                int i = raw.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
                svg = i >= 0 ? raw.Substring(i) : raw;
                return true;
            }
            catch (Exception ex)
            {
                _lastError = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>data:image/svg+xml;base64,… สำหรับ &lt;img src&gt; (null เมื่อสร้างไม่ได้)</summary>
        public static string RenderSvgDataUri(string text, int sizePx)
        {
            string svg = RenderSvg(text, sizePx);
            if (svg == null) return null;
            return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        }

        private static volatile string _lastError;
        /// <summary>เหตุผลล่าสุดที่สร้าง QR ไม่ได้ (หน้าตั้งค่า) — null = ปกติ</summary>
        public static string LastError { get { return _lastError; } }

        /// <summary>ตัวสร้างพร้อมไหม (มี zxing.dll)</summary>
        public static bool EngineReady()
        {
            string s;
            return TryRenderSvg("ping", 64, out s);
        }

        /// <summary>แยกเมธอดที่แตะ ZXing (NoInlining) — DLL หายจะโยน FileNotFound ตรงนี้ ไม่ทำให้คลาสนอกโหลดไม่ได้</summary>
        private static class ZxingSvgBridge
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static string Render(string text, int size)
            {
                var writer = new ZXing.BarcodeWriterSvg
                {
                    Format = ZXing.BarcodeFormat.QR_CODE,
                    Options = new ZXing.QrCode.QrCodeEncodingOptions { Width = size, Height = size, Margin = 1 }
                };
                var img = writer.Write(text);
                return img == null ? null : img.Content;
            }
        }
    }
}
