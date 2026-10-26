using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Payments.SlipCheck
{
    /// <summary>
    /// SlipOK (ระดับที่ 1 — ธนาคารยืนยันยอด/วันเวลา/ผู้รับ) — แพ็กเกจฟรี 100 สลิป/เดือน (นับเองต่อเดือนใน Slip_Verify_Usage)
    ///
    /// สัญญา API (slipok.com/api-documentation/check-slip — โดเมนถูกบล็อกจาก sandbox จึงยืนยันจากโค้ดสาธารณะที่เรียก API จริง):
    ///   POST https://api.slipok.com/api/line/apikey/{branchId}
    ///   header  x-authorization: {API key}
    ///   body    multipart/form-data: data = ข้อความจาก QR | files = รูปสลิป | url = ลิงก์รูป (อย่างใดอย่างหนึ่ง),
    ///           amount (ไม่บังคับ — เราไม่ส่ง เทียบยอดเองด้วย tolerance), log = true (เก็บประวัติ/ตรวจซ้ำฝั่ง SlipOK)
    ///   อ้างอิง: github.com/PrakritManStudio/slipok-sdk README (baseUrl https://api.slipok.com/api/line/apikey, ฟิลด์ data/url/files/
    ///            amount/log, ตารางรหัส 1000–1014); github.com/jakapobrs-oss/concert-antibot lib/slipok.ts (header x-authorization,
    ///            multipart files|data|url, shape คำตอบ); github.com/timelessnesses/accountery-cron src/slipOK.ts (ชนิด receiver.proxy/account)
    ///   สำเร็จ  { success:true, data:{ success, message, transRef, sendingBank, receivingBank, transDate "yyyyMMdd", transTime "HH:mm:ss",
    ///            transTimestamp, sender{displayName,name,proxy{type,value},account{type,value}}, receiver{…}, amount, … } }
    ///   ไม่ผ่าน { success:false, code, message, data? } — 1012/1013/1014 แนบ data ของสลิปมาด้วย (concert-antibot)
    ///   รหัส (slipok-sdk README / donate-project lib/services/slipok.ts / Restaurant-Management-Back slipok.service.ts):
    ///     1000 ไม่มี data/files/url · 1001 ไม่พบสาขา (branch id ผิด) · 1002 Authorization header ผิด · 1003 แพ็กเกจหมดอายุ ·
    ///     1004 ใช้เกินโควต้า · 1005 ไม่ใช่ไฟล์รูป · 1006 รูปไม่ถูกต้อง · 1007 ไม่พบ QR · 1008 QR ไม่ใช่สลิป ·
    ///     1009 ระบบธนาคารขัดข้องชั่วคราว (ไม่เสียโควต้า) · 1010 ธนาคารต้องรอสักครู่หลังโอน · 1011 QR หมดอายุ/ไม่มีรายการ ·
    ///     1012 สลิปซ้ำ (เคยส่งเมื่อ {timestamp}) · 1013 ยอดไม่ตรงกับ amount ที่ส่ง · 1014 บัญชีผู้รับไม่ตรงบัญชีหลักของร้าน
    /// ข้อมูลผู้รับ/ผู้โอนจาก SlipOK ถูกปิดบังเลขบัญชีอยู่แล้ว (xxx-x-x1234-x) — เราเก็บเฉพาะค่าที่ปิดบัง (PDPA)
    /// ไม่ log คีย์ API
    /// </summary>
    public class SlipOkVerifier : ISlipVerifier
    {
        public const string ProviderName = "SLIPOK";
        private const string BaseUrl = "https://api.slipok.com/api/line/apikey/";

        private readonly string _branchId;
        private readonly string _apiKey;
        private readonly int _timeoutMs;

        public SlipOkVerifier(string branchId, string apiKey, int timeoutSec)
        {
            _branchId = (branchId ?? "").Trim();
            _apiKey = (apiKey ?? "").Trim();
            _timeoutMs = Math.Max(3, Math.Min(10, timeoutSec <= 0 ? 10 : timeoutSec)) * 1000;
        }

        public string Name => ProviderName;
        public bool IsConfigured => _branchId.Length > 0 && _apiKey.Length > 0;

        public SlipVendorResponse Verify(byte[] image, string fileName, string qrPayload)
        {
            var sw = Stopwatch.StartNew();
            var r = new SlipVendorResponse();
            if (!IsConfigured)
            {
                r.Kind = SlipVendorResponse.KindConfig;
                r.Message = "ยังไม่ได้ตั้ง SlipOK Branch ID / API key";
                return r;
            }
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                string boundary = "----tt" + Guid.NewGuid().ToString("N");
                byte[] body;
                using (var ms = new MemoryStream())
                {
                    if (!string.IsNullOrEmpty(qrPayload))
                        WriteField(ms, boundary, "data", qrPayload);
                    else
                        WriteFile(ms, boundary, "files", SafeName(fileName), image);
                    WriteField(ms, boundary, "log", "true");
                    WriteAscii(ms, "--" + boundary + "--\r\n");
                    body = ms.ToArray();
                }

                var req = (HttpWebRequest)WebRequest.Create(BaseUrl + Uri.EscapeDataString(_branchId));
                req.Method = "POST";
                req.Timeout = _timeoutMs;
                req.ReadWriteTimeout = _timeoutMs;
                req.ContentType = "multipart/form-data; boundary=" + boundary;
                req.Accept = "application/json";
                req.Headers["x-authorization"] = _apiKey;
                req.ContentLength = body.Length;
                using (var rs = req.GetRequestStream()) rs.Write(body, 0, body.Length);

                string json;
                try
                {
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        json = sr.ReadToEnd();
                }
                catch (WebException wex) when (wex.Response != null)
                {
                    // 4xx ของ SlipOK มี JSON {success:false, code, message, data?}
                    using (var sr = new StreamReader(wex.Response.GetResponseStream(), Encoding.UTF8))
                        json = sr.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(json) || json.TrimStart()[0] != '{')
                    {
                        r.Kind = SlipVendorResponse.KindError;
                        r.Message = "SlipOK HTTP " + (int)((HttpWebResponse)wex.Response).StatusCode;
                        return r;
                    }
                }
                Parse(json, r);
            }
            catch (WebException wex)
            {
                r.Kind = SlipVendorResponse.KindError;
                r.Message = wex.Status == WebExceptionStatus.Timeout ? "SlipOK ไม่ตอบภายในเวลา" : "เชื่อมต่อ SlipOK ไม่ได้: " + wex.Status;
            }
            catch (Exception ex)
            {
                r.Kind = SlipVendorResponse.KindError;
                r.Message = "SlipOK ผิดพลาด: " + ex.GetType().Name;
            }
            finally
            {
                r.ElapsedMs = sw.ElapsedMilliseconds;
            }
            return r;
        }

        /// <summary>แปลง JSON คำตอบ → รูปกลาง (public เพื่อทดสอบ/หน้าตั้งค่า)</summary>
        public static void Parse(string json, SlipVendorResponse r)
        {
            // JavaScriptSerializer (System.Web.Extensions) — ไม่พึ่งรุ่นของ Newtonsoft
            var o = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.DeserializeObject(json) as Dictionary<string, object>;
            if (o == null) { r.Kind = SlipVendorResponse.KindError; r.Message = "SlipOK ตอบรูปแบบไม่รู้จัก"; return; }
            object sv;
            bool success = o.TryGetValue("success", out sv) && sv is bool && (bool)sv;
            r.Code = Str(o, "code");
            r.Message = Str(o, "message");
            var data = Dict(o, "data");
            if (data != null) FillData(data, r);

            if (success && r.HasData && string.IsNullOrEmpty(r.Code))
            {
                r.Kind = SlipVendorResponse.KindOk;
                r.Charged = true;
                return;
            }
            switch (r.Code)
            {
                case "1012": r.Kind = SlipVendorResponse.KindDuplicate; break;           // ไม่เสียโควต้า (ส่งซ้ำ)
                case "1013": r.Kind = SlipVendorResponse.KindAmountMismatch; r.Charged = r.HasData; break;
                case "1014": r.Kind = SlipVendorResponse.KindReceiverMismatch; r.Charged = r.HasData; break;
                case "1003":
                case "1004": r.Kind = SlipVendorResponse.KindQuota; break;
                case "1001":
                case "1002": r.Kind = SlipVendorResponse.KindConfig; break;
                case "1009":
                case "1010": r.Kind = SlipVendorResponse.KindPendingBank; break;
                case "1011": r.Kind = SlipVendorResponse.KindNotFound; break;
                case "1000":
                case "1005":
                case "1006":
                case "1007":
                case "1008": r.Kind = SlipVendorResponse.KindNoQr; break;
                default:
                    r.Kind = success && r.HasData ? SlipVendorResponse.KindOk : SlipVendorResponse.KindError;
                    r.Charged = r.Kind == SlipVendorResponse.KindOk;
                    break;
            }
        }

        private static void FillData(Dictionary<string, object> d, SlipVendorResponse r)
        {
            decimal amt;
            string a = Str(d, "amount");
            if (!string.IsNullOrEmpty(a) && decimal.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out amt) && amt > 0m)
                r.Amount = Math.Round(amt, 2);
            r.TransRef = Str(d, "transRef");
            r.SendingBank = Str(d, "sendingBank");
            r.ReceivingBank = Str(d, "receivingBank");

            string date = Str(d, "transDate");
            string time = Str(d, "transTime");
            DateTime dt;
            if (!string.IsNullOrEmpty(date) && DateTime.TryParseExact(date + " " + (string.IsNullOrEmpty(time) ? "00:00:00" : time),
                    new[] { "yyyyMMdd HH:mm:ss", "yyyyMMdd HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                r.TransDate = dt;
            else
            {
                string ts = Str(d, "transTimestamp");
                if (!string.IsNullOrEmpty(ts) && DateTime.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dt))
                    r.TransDate = dt.ToLocalTime();
            }

            var s = Dict(d, "sender");
            if (s != null)
            {
                r.SenderName = PartyName(s);
                r.SenderAcctMasked = Acct(s, "account");
            }
            var rc = Dict(d, "receiver");
            if (rc != null)
            {
                r.ReceiverName = PartyName(rc);
                r.ReceiverAcctMasked = Acct(rc, "account");
                r.ReceiverProxyMasked = Acct(rc, "proxy");
            }
            r.HasData = r.Amount.HasValue || !string.IsNullOrEmpty(r.TransRef);
        }

        private static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null || v is Dictionary<string, object> || v is object[]) return null;
            if (v is decimal) return ((decimal)v).ToString(CultureInfo.InvariantCulture);
            if (v is double) return ((double)v).ToString("R", CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, object> Dict(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
        }

        private static string PartyName(Dictionary<string, object> p)
        {
            string n = Str(p, "displayName");
            if (string.IsNullOrWhiteSpace(n)) n = Str(p, "name");
            return string.IsNullOrWhiteSpace(n) ? null : n.Trim();
        }

        private static string Acct(Dictionary<string, object> p, string key)
        {
            var o = Dict(p, key);
            if (o == null) return null;
            string v = Str(o, "value") ?? Str(o, "account");
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        /// <summary>เช็คโควต้าคงเหลือ (GET …/{branchId}/quota — ตาม research §1 / ใช้แสดงในหน้าตั้งค่าเท่านั้น)</summary>
        public string CheckQuotaRaw()
        {
            if (!IsConfigured) return null;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(BaseUrl + Uri.EscapeDataString(_branchId) + "/quota");
                req.Method = "GET";
                req.Timeout = _timeoutMs;
                req.Headers["x-authorization"] = _apiKey;
                try
                {
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        return sr.ReadToEnd();
                }
                catch (WebException wex) when (wex.Response != null)
                {
                    using (var sr = new StreamReader(wex.Response.GetResponseStream(), Encoding.UTF8))
                        return sr.ReadToEnd();
                }
            }
            catch (Exception ex) { return "ERROR: " + ex.GetType().Name; }
        }

        private static string SafeName(string n)
        {
            n = Path.GetFileName(n ?? "");
            if (string.IsNullOrEmpty(n)) n = "slip.jpg";
            var sb = new StringBuilder();
            foreach (char c in n) sb.Append(char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }

        private static void WriteField(Stream s, string boundary, string name, string value)
        {
            WriteAscii(s, "--" + boundary + "\r\nContent-Disposition: form-data; name=\"" + name + "\"\r\n\r\n");
            byte[] v = Encoding.UTF8.GetBytes(value ?? "");
            s.Write(v, 0, v.Length);
            WriteAscii(s, "\r\n");
        }

        private static void WriteFile(Stream s, string boundary, string name, string fileName, byte[] data)
        {
            string ext = (Path.GetExtension(fileName) ?? "").ToLowerInvariant();
            string ct = ext == ".png" ? "image/png" : ext == ".webp" ? "image/webp" : "image/jpeg";
            WriteAscii(s, "--" + boundary + "\r\nContent-Disposition: form-data; name=\"" + name + "\"; filename=\"" + fileName
                + "\"\r\nContent-Type: " + ct + "\r\n\r\n");
            if (data != null) s.Write(data, 0, data.Length);
            WriteAscii(s, "\r\n");
        }

        private static void WriteAscii(Stream s, string t)
        {
            byte[] b = Encoding.ASCII.GetBytes(t);
            s.Write(b, 0, b.Length);
        }
    }
}
