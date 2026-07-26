using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using TheArtOfDev.HtmlRenderer.WinForms;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// สร้างรูปตารางจองรายวัน (render หน้า DisplayToday) แล้ว push เข้า LINE — พอร์ตจาก
    /// external console app (HTMLToPicture) เข้าระบบ: ตั้งเวลาส่งอัตโนมัติ, ตั้งค่าในหน้า Admin,
    /// ใช้ token ของ LINE OA เดิม (OmniChannel) หรือ override, วัดความสูงรูปจากเนื้อหาจริง (ไม่ตัด/ไม่เหลือขอบ).
    /// </summary>
    public class DailyReportLineService
    {
        private readonly string _conn;
        private readonly code _code = new code();

        private readonly bool _enabled;
        private readonly string _recipientsRaw, _sendTime, _sourceUrl, _caption, _publicBaseUrl, _imageFolder, _tokenOverrideEnc;
        private readonly int _imageWidth, _imageHeight, _jpegQuality;
        private readonly bool _autoHeight;

        public DailyReportLineService(string connectionString)
        {
            _conn = connectionString;
            _enabled = Cfg("Line_DailyReport_Enabled", "0") == "1";
            _recipientsRaw = Cfg("Line_DailyReport_Recipients", "");
            _sendTime = Cfg("Line_DailyReport_SendTime", "08:00");
            _sourceUrl = Cfg("Line_DailyReport_SourceUrl", "https://taketimebangphra.com/displaytoday");
            _caption = Cfg("Line_DailyReport_Caption", "ตารางการจองวันที่ {date}");
            _publicBaseUrl = Cfg("Line_DailyReport_PublicBaseUrl", "https://taketimebangphra.com/Images/Reservation").TrimEnd('/');
            _imageFolder = Cfg("Line_DailyReport_ImageFolder", "~/Images/Reservation");
            _tokenOverrideEnc = Cfg("Line_DailyReport_TokenOverride_Encrypted", "");
            _imageWidth = ParseInt(Cfg("Line_DailyReport_ImageWidth", "1600"), 1600);
            _imageHeight = ParseInt(Cfg("Line_DailyReport_ImageHeight", "700"), 700);
            _autoHeight = Cfg("Line_DailyReport_AutoHeight", "1") == "1";
            _jpegQuality = Math.Max(1, Math.Min(100, ParseInt(Cfg("Line_DailyReport_JpegQuality", "90"), 90)));
        }

        // ── public API ───────────────────────────────────────────────────────────
        public class SendResult
        {
            public bool Success;
            public string ImageUrl, ImagePath, Error;
            public int RecipientsOk, RecipientsFail;
            public List<string> Messages = new List<string>();
            public override string ToString() =>
                Error != null ? "ผิดพลาด: " + Error
                : $"สร้างรูปสำเร็จ → ส่ง {RecipientsOk} สำเร็จ, {RecipientsFail} ล้มเหลว";
        }

        public static bool IsEnabled(string conn)
        {
            try
            {
                var c = new code();
                var dt = c.DatabaseQuerySafe(conn,
                    "SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_Enabled'", null);
                return dt?.Rows.Count > 0 && dt.Rows[0][0]?.ToString() == "1";
            }
            catch { return false; }
        }

        /// <summary>ถึงเวลาส่งอัตโนมัติแล้วหรือยัง (เปิด + เลยเวลาที่ตั้ง + ยังไม่ส่งวันนี้).</summary>
        public bool IsDueNow()
        {
            if (!_enabled) return false;
            if (Cfg("Line_DailyReport_LastSent", "") == DateTime.Now.ToString("yyyyMMdd")) return false;
            var t = ParseTime(_sendTime);
            return DateTime.Now.TimeOfDay >= t;
        }

        /// <summary>สร้างรูป + push เข้า LINE. ถ้า markSent=true จะบันทึกว่าส่งวันนี้แล้ว (กันส่งซ้ำ).</summary>
        public SendResult SendNow(bool markSent = true)
        {
            var res = new SendResult();
            try
            {
                string path, url;
                if (!GenerateImage(out path, out url, out string genErr))
                {
                    res.Error = genErr ?? "สร้างรูปไม่สำเร็จ";
                    return res;
                }
                res.ImagePath = path; res.ImageUrl = url;

                string token = GetLineToken();
                if (string.IsNullOrEmpty(token)) { res.Error = "ไม่พบ LINE channel access token (ตั้งใน LINE OA เดิม หรือ override)"; return res; }

                var recipients = ParseRecipients(_recipientsRaw);
                if (recipients.Count == 0) { res.Error = "ยังไม่ได้ตั้งผู้รับ (LINE userId/groupId)"; return res; }

                string caption = string.IsNullOrWhiteSpace(_caption) ? null
                    : _caption.Replace("{date}", ThaiDate(DateTime.Now));

                var messages = new List<object>();
                if (caption != null) messages.Add(new { type = "text", text = caption });
                messages.Add(new { type = "image", originalContentUrl = url, previewImageUrl = url });

                foreach (var to in recipients)
                {
                    try
                    {
                        PushMessages(token, to, messages);
                        res.RecipientsOk++;
                        res.Messages.Add($"ส่งถึง {Mask(to)} สำเร็จ");
                    }
                    catch (Exception ex)
                    {
                        res.RecipientsFail++;
                        res.Messages.Add($"ส่งถึง {Mask(to)} ล้มเหลว: {ExtractLineError(ex)}");
                    }
                }

                res.Success = res.RecipientsOk > 0;
                if (markSent && res.Success)
                    _code.DatabaseInsertSafe(_conn,
                        "UPDATE Accounting_Integration_Config SET ConfigValue = @v WHERE ConfigKey = 'Line_DailyReport_LastSent'",
                        new Dictionary<string, object> { { "@v", DateTime.Now.ToString("yyyyMMdd") } });
                _code.Logs(_conn, "DailyLineReport", res.ToString() + " | " + url, "SYSTEM");
                return res;
            }
            catch (Exception ex)
            {
                res.Error = ex.Message;
                _code.Logs(_conn, "DailyLineReport", "SendNow error: " + ex.Message, "SYSTEM");
                return res;
            }
        }

        /// <summary>สร้างรูปอย่างเดียว (สำหรับปุ่มพรีวิว) — คืน public URL.</summary>
        public (bool, string, string) GeneratePreview()
        {
            if (GenerateImage(out string path, out string url, out string err))
                return (true, url, path);
            return (false, err ?? "สร้างรูปไม่สำเร็จ", null);
        }

        /// <summary>ส่งข้อความทดสอบ (text) ถึงผู้รับทั้งหมด — ตรวจ token/recipient โดยไม่ render รูป.</summary>
        public (bool, string) SendTestText()
        {
            string token = GetLineToken();
            if (string.IsNullOrEmpty(token)) return (false, "ไม่พบ LINE channel access token");
            var recipients = ParseRecipients(_recipientsRaw);
            if (recipients.Count == 0) return (false, "ยังไม่ได้ตั้งผู้รับ");
            int ok = 0; var errs = new List<string>();
            foreach (var to in recipients)
            {
                try { PushMessages(token, to, new List<object> { new { type = "text", text = "🔔 ทดสอบส่งรายงานจองรายวัน (TakeTime)" } }); ok++; }
                catch (Exception ex) { errs.Add(Mask(to) + ": " + ExtractLineError(ex)); }
            }
            return ok > 0
                ? (true, $"ส่งทดสอบสำเร็จ {ok}/{recipients.Count}" + (errs.Count > 0 ? " (ล้มเหลว: " + string.Join("; ", errs) + ")" : ""))
                : (false, "ส่งไม่สำเร็จ: " + string.Join("; ", errs));
        }

        // ── image generation ───────────────────────────────────────────────────
        private bool GenerateImage(out string physicalPath, out string publicUrl, out string error)
        {
            physicalPath = null; publicUrl = null; error = null;
            try
            {
                string html = DownloadHtml(_sourceUrl);
                if (string.IsNullOrWhiteSpace(html)) { error = "โหลดหน้า source ไม่ได้ (" + _sourceUrl + ")"; return false; }

                int width = _imageWidth > 0 ? _imageWidth : 1600;
                int height = _imageHeight > 0 ? _imageHeight : 700;

                // วัดความสูงจากเนื้อหาจริง (ไม่ตัด/ไม่เหลือขอบ) — ดีกว่าการเดาจากความยาว HTML แบบเดิม
                if (_autoHeight)
                {
                    try
                    {
                        using (var measureBmp = new Bitmap(1, 1))
                        using (var mg = Graphics.FromImage(measureBmp))
                        {
                            SizeF sz = HtmlRender.Measure(mg, html, width);
                            int measured = (int)Math.Ceiling(sz.Height) + 40; // padding ล่าง
                            if (measured > height) height = measured;
                        }
                    }
                    catch { /* วัดไม่ได้ → ใช้ height พื้นฐาน */ }
                }
                if (height > 8000) height = 8000; // กัน runaway

                using (var bmp = new Bitmap(width, height))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.White);
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        HtmlRender.Render(g, html, new PointF(0, 0), new SizeF(width, height));
                    }

                    string folder = ResolveFolder(_imageFolder);
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    string fileName = DateTime.Now.ToString("yyyyMMdd") + ".jpg";
                    physicalPath = Path.Combine(folder, fileName);
                    SaveJpeg(bmp, physicalPath, _jpegQuality);
                    // cache-bust ให้ LINE โหลดรูปใหม่ (LINE cache ตาม URL)
                    publicUrl = _publicBaseUrl + "/" + fileName + "?t=" + DateTime.Now.ToString("HHmmss");
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "render รูปล้มเหลว: " + ex.Message + (ex.InnerException != null ? " / " + ex.InnerException.Message : "");
                _code.Logs(_conn, "DailyLineReport", error, "SYSTEM");
                return false;
            }
        }

        private static void SaveJpeg(Bitmap bmp, string path, int quality)
        {
            var enc = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using (var ep = new EncoderParameters(1))
            {
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
                if (enc != null) bmp.Save(path, enc, ep);
                else bmp.Save(path, ImageFormat.Jpeg);
            }
        }

        private string ResolveFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) folder = "~/Images/Reservation";
            if (folder.StartsWith("~") || folder.StartsWith("/"))
            {
                // virtual path → physical (ใช้ HostingEnvironment ทำงานได้แม้ไม่มี HttpContext เช่นใน timer)
                string mapped = System.Web.Hosting.HostingEnvironment.MapPath(folder.StartsWith("~") ? folder : "~" + folder);
                if (!string.IsNullOrEmpty(mapped)) return mapped;
            }
            return folder; // ถือเป็น physical path
        }

        private string DownloadHtml(string url)
        {
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                var data = client.DownloadData(url);
                return Encoding.UTF8.GetString(data);
            }
        }

        // ── LINE push ────────────────────────────────────────────────────────────
        private void PushMessages(string token, string to, List<object> messages)
        {
            var body = new JavaScriptSerializer().Serialize(new { to = to, messages = messages });
            var request = (HttpWebRequest)WebRequest.Create("https://api.line.me/v2/bot/message/push");
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = 20000;
            request.Headers.Add("Authorization", "Bearer " + token);
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            request.ContentLength = bytes.Length;
            using (var s = request.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
            using (var resp = (HttpWebResponse)request.GetResponse())
            using (var rd = new StreamReader(resp.GetResponseStream()))
                rd.ReadToEnd();
        }

        private string GetLineToken()
        {
            // 1) override เฉพาะงานนี้
            string ov = _code.Derypt(_tokenOverrideEnc ?? "");
            if (!string.IsNullOrWhiteSpace(ov)) return ov;
            // 2) LINE OA เดิม (OmniChannel_Channels: Config JSON.channelAccessToken)
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT Config FROM OmniChannel_Channels WHERE ChannelCode = 'LINE'", null);
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Config"] != DBNull.Value)
                {
                    string json = dt.Rows[0]["Config"].ToString();
                    if (!string.IsNullOrEmpty(json))
                    {
                        var cfg = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                        if (cfg != null && cfg.ContainsKey("channelAccessToken"))
                        {
                            string t = cfg["channelAccessToken"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(t)) return t;
                        }
                    }
                }
            }
            catch { }
            // 3) AppSettings เดิม
            string appTok = System.Configuration.ConfigurationManager.AppSettings["linechannelaccesstokentaketime"];
            return appTok ?? "";
        }

        // ── helpers ────────────────────────────────────────────────────────────
        private string Cfg(string key, string def)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = @k",
                    new Dictionary<string, object> { { "@k", key } });
                if (dt?.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                {
                    string v = dt.Rows[0][0].ToString();
                    return string.IsNullOrEmpty(v) ? def : v;
                }
            }
            catch { }
            return def;
        }

        private static List<string> ParseRecipients(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            return raw.Split(new[] { ',', ';', '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList();
        }

        private static int ParseInt(string s, int def) => int.TryParse(s, out var v) ? v : def;

        private static TimeSpan ParseTime(string hhmm)
        {
            if (TimeSpan.TryParseExact(hhmm ?? "", @"hh\:mm", CultureInfo.InvariantCulture, out var t)) return t;
            if (TimeSpan.TryParse(hhmm ?? "", out var t2)) return t2;
            return new TimeSpan(8, 0, 0);
        }

        private static string ThaiDate(DateTime d)
        {
            try { return d.ToString("dd MMMM yyyy", new CultureInfo("th-TH")); }
            catch { return d.ToString("dd/MM/yyyy"); }
        }

        private static string Mask(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length <= 8) return id;
            return id.Substring(0, 5) + "…" + id.Substring(id.Length - 3);
        }

        private static string ExtractLineError(Exception ex)
        {
            if (ex is WebException wex && wex.Response is HttpWebResponse resp)
            {
                try
                {
                    using (var rd = new StreamReader(resp.GetResponseStream()))
                        return ((int)resp.StatusCode) + " " + rd.ReadToEnd();
                }
                catch { }
            }
            return ex.Message;
        }
    }
}
