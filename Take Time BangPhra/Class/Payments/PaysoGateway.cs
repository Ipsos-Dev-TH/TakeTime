using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Take_Time_BangPhra.Payments
{
    /// <summary>
    /// ตัวเชื่อมเกตเวย์ Payso (https://api-docs.payso.co)
    ///
    /// ⚠ ออกแบบไว้แบบ "ปรับได้จากหน้าเว็บ" โดยตั้งใจ
    /// ─────────────────────────────────────────────────────────────────────────
    /// สัญญา API ของ Payso (เส้นทาง / ชื่อหัวข้อ auth / รูปแบบลายเซ็น / ชื่อฟิลด์ในคำขอ
    /// และคำตอบ) ถูกเก็บเป็น "ค่าตั้งค่า" ทั้งหมด ไม่ได้ hard-code ไว้ในโค้ด เพราะ
    /// ค่าเหล่านี้ต้องตรงกับเอกสารจริงเป๊ะ ๆ — เดาไม่ได้ เรื่องเงินผิดพลาดไม่ได้
    ///
    /// ⇒ เมื่อได้เอกสารจริงมาแล้ว แก้ที่ ศูนย์ตั้งค่า → "รับชำระเงินออนไลน์ (Payso)"
    ///    ได้ทันที โดยไม่ต้อง build ใหม่ ไม่ต้อง deploy ใหม่:
    ///      • Payso_Path_*            เส้นทาง endpoint
    ///      • Payso_Auth_Mode         วิธีส่งกุญแจ (Bearer / หัวข้อเฉพาะ)
    ///      • Payso_Signature_*       รูปแบบลายเซ็น
    ///      • Payso_Request_Template  รูปคำขอ (JSON) พร้อมตัวแปร {{...}}
    ///      • Payso_Response_Map      ตำแหน่งฟิลด์ในคำตอบ (รองรับหลายตัวเลือก)
    ///      • Payso_Status_*          คำที่แปลว่าจ่ายแล้ว/รอ/ไม่สำเร็จ
    ///
    /// ทุกคำขอ-คำตอบถูกเก็บดิบไว้ใน Payment_Transaction (Raw_Request/Raw_Response)
    /// และ Payment_Transaction_Event ⇒ ถ้ารูปแบบไม่ตรง จะเห็นทันทีว่าเกตเวย์ตอบอะไรมา
    /// แล้วปรับ map ให้ตรงได้เลย
    /// </summary>
    public class PaysoGateway : IPaymentGateway
    {
        public const string Provider = "PAYSO";

        static PaysoGateway()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
                    | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch { }
        }

        public string ProviderCode { get { return Provider; } }
        public string DisplayName { get { return "Payso"; } }
        public bool IsReady { get { return PaymentGatewayConfig.IsPaysoReady; } }

        // ── สร้างรายการชำระเงิน ──────────────────────────────────────────────

        public PaymentChargeResult CreateCharge(PaymentChargeRequest req)
        {
            var result = new PaymentChargeResult { TxnRef = req.TxnRef, Status = PaymentStatus.Failed };

            if (!IsReady)
            {
                result.Message = "ยังไม่ได้ตั้งค่าเกตเวย์ Payso ให้ครบ (Base URL / กุญแจ) หรือยังปิดใช้งานอยู่";
                return result;
            }

            string path = PaymentGatewayConfig.Get("Payso_Path_CreatePayment", "/api/v1/payments");
            if (string.IsNullOrWhiteSpace(path))
            {
                result.Message = "ยังไม่ได้ตั้งเส้นทาง \"สร้างรายการชำระเงิน\" ในหน้าตั้งค่า";
                return result;
            }

            string body = BuildRequestBody(req);
            result.RawRequest = body;

            string response;
            int http;
            string err = Send("POST", path, body, out response, out http);
            result.RawResponse = response;
            result.HttpStatus = http;

            if (err != null)
            {
                result.Message = err;
                return result;
            }

            JObject root = TryParse(response);
            if (root == null)
            {
                result.Message = http >= 200 && http < 300
                    ? "เกตเวย์ตอบกลับมาแต่ไม่ใช่ JSON — ตรวจเส้นทาง API ในหน้าตั้งค่า"
                    : "เกตเวย์ตอบรหัส " + http + " (ไม่ใช่ JSON)";
                return result;
            }

            result.ProviderTxnId = ReadMapped(root, "transactionId");
            result.PaymentUrl = ReadMapped(root, "paymentUrl");
            result.QrPayload = ReadMapped(root, "qrPayload");
            result.Message = ReadMapped(root, "message");

            string rawStatus = ReadMapped(root, "status");
            string status = TranslateStatus(rawStatus);

            bool httpOk = http >= 200 && http < 300;
            bool haveSomethingToShow = !string.IsNullOrEmpty(result.PaymentUrl)
                                       || !string.IsNullOrEmpty(result.QrPayload);

            if (!httpOk)
            {
                result.Status = PaymentStatus.Failed;
                result.Message = string.IsNullOrEmpty(result.Message)
                    ? "เกตเวย์ปฏิเสธคำขอ (รหัส " + http + ")"
                    : result.Message;
                return result;
            }

            if (status == PaymentStatus.Failed)
            {
                result.Status = PaymentStatus.Failed;
                if (string.IsNullOrEmpty(result.Message))
                    result.Message = "เกตเวย์แจ้งสถานะ \"" + rawStatus + "\"";
                return result;
            }

            if (!haveSomethingToShow && status != PaymentStatus.Paid)
            {
                result.Status = PaymentStatus.Failed;
                result.Message = "เกตเวย์ไม่ได้ส่งลิงก์หรือ QR สำหรับให้ลูกค้าชำระเงินกลับมา — "
                    + "ตรวจ \"ตำแหน่งฟิลด์ในคำตอบ\" (Payso_Response_Map) ให้ตรงกับเอกสารจริง";
                return result;
            }

            result.Success = true;
            result.Status = status == PaymentStatus.Paid ? PaymentStatus.Paid : PaymentStatus.Pending;
            return result;
        }

        // ── ถามสถานะ ────────────────────────────────────────────────────────

        public PaymentStatusResult QueryStatus(string providerTxnId, string txnRef)
        {
            var result = new PaymentStatusResult();
            if (!IsReady) { result.Message = "เกตเวย์ยังไม่พร้อมใช้งาน"; return result; }

            string path = PaymentGatewayConfig.Get("Payso_Path_QueryPayment", "");
            if (string.IsNullOrWhiteSpace(path))
            {
                result.Message = "ยังไม่ได้ตั้งเส้นทาง \"ตรวจสถานะรายการ\"";
                return result;
            }
            path = path.Replace("{id}", Uri.EscapeDataString(providerTxnId ?? ""))
                       .Replace("{ref}", Uri.EscapeDataString(txnRef ?? ""));

            string response; int http;
            string err = Send("GET", path, null, out response, out http);
            result.RawResponse = response;
            result.HttpStatus = http;
            if (err != null) { result.Message = err; return result; }

            JObject root = TryParse(response);
            if (root == null)
            {
                result.Message = "คำตอบไม่ใช่ JSON (รหัส " + http + ")";
                return result;
            }

            result.Success = http >= 200 && http < 300;
            result.ProviderTxnId = ReadMapped(root, "transactionId");
            result.Status = TranslateStatus(ReadMapped(root, "status"));
            result.Amount = ReadDecimal(root, "amount");
            result.Fee = ReadDecimal(root, "fee");
            result.CardBrand = ReadMapped(root, "cardBrand");
            result.CardLast4 = ReadMapped(root, "cardLast4");
            result.Message = ReadMapped(root, "message");
            return result;
        }

        // ── คืนเงิน ──────────────────────────────────────────────────────────

        public PaymentStatusResult Refund(string providerTxnId, string txnRef, decimal amount, string reason)
        {
            string path = PaymentGatewayConfig.Get("Payso_Path_Refund", "");
            if (string.IsNullOrWhiteSpace(path)) return null;   // ยังไม่เปิดใช้งานคืนเงิน

            var result = new PaymentStatusResult();
            if (!IsReady) { result.Message = "เกตเวย์ยังไม่พร้อมใช้งาน"; return result; }

            path = path.Replace("{id}", Uri.EscapeDataString(providerTxnId ?? ""))
                       .Replace("{ref}", Uri.EscapeDataString(txnRef ?? ""));

            var payload = new JObject();
            payload["amount"] = amount;
            payload["referenceNo"] = txnRef ?? "";
            payload["reason"] = reason ?? "";
            string body = payload.ToString(Formatting.None);

            string response; int http;
            string err = Send("POST", path, body, out response, out http);
            result.RawResponse = response;
            result.HttpStatus = http;
            if (err != null) { result.Message = err; return result; }

            JObject root = TryParse(response);
            result.Success = http >= 200 && http < 300;
            if (root != null)
            {
                result.Message = ReadMapped(root, "message");
                string s = TranslateStatus(ReadMapped(root, "status"));
                result.Status = result.Success ? PaymentStatus.Refunded : s;
            }
            else if (result.Success) result.Status = PaymentStatus.Refunded;
            return result;
        }

        // ── การแจ้งกลับ (webhook) ───────────────────────────────────────────

        public PaymentWebhookEvent ParseWebhook(NameValueCollection headers, string body, string remoteIp)
        {
            var ev = new PaymentWebhookEvent();

            JObject root = TryParse(body);
            if (root == null)
            {
                ev.Message = "เนื้อหาที่ส่งมาไม่ใช่ JSON";
                return ev;
            }

            ev.EventId = ReadMapped(root, "eventId");
            ev.EventType = ReadMapped(root, "eventType");
            ev.TxnRef = ReadMapped(root, "reference");
            ev.ProviderTxnId = ReadMapped(root, "transactionId");
            ev.RawStatus = ReadMapped(root, "status");
            ev.Status = TranslateStatus(ev.RawStatus);
            ev.Amount = ReadDecimal(root, "amount");
            ev.Fee = ReadDecimal(root, "fee");
            ev.CardBrand = ReadMapped(root, "cardBrand");
            ev.CardLast4 = ReadMapped(root, "cardLast4");
            ev.Message = ReadMapped(root, "message");

            ev.SignatureValid = VerifyWebhookSignature(headers, body);
            return ev;
        }

        /// <summary>
        /// ตรวจว่าข้อความนี้มาจาก Payso จริง
        /// ปิดการตรวจได้จากหน้าตั้งค่าเฉพาะตอนทดสอบ — เปิดไว้เสมอในระบบจริง
        /// </summary>
        private bool VerifyWebhookSignature(NameValueCollection headers, string body)
        {
            if (!PaymentGatewayConfig.WebhookVerify) return true;   // ผู้ดูแลเลือกปิดตรวจเอง

            string header = PaymentGatewayConfig.WebhookSignatureHeader;
            string secret = PaymentGatewayConfig.WebhookSecret;
            if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(secret)) return false;

            string got = headers == null ? null : headers[header];
            if (string.IsNullOrEmpty(got)) return false;

            // บางเจ้าส่งเป็น "sha256=<ค่า>"
            int eq = got.IndexOf('=');
            if (eq > 0 && eq < 12) got = got.Substring(eq + 1);

            string algo = PaymentGatewayConfig.Get("Payso_Signature_Algo", "HMACSHA256");
            string enc = PaymentGatewayConfig.Get("Payso_Signature_Encoding", "HEX");
            string expected = ComputeSignature(body, secret, algo, enc);
            if (string.IsNullOrEmpty(expected)) return false;

            return FixedTimeEquals(expected, got.Trim());
        }

        /// <summary>เทียบสตริงแบบไม่ให้เวลาเปรียบเทียบบอกใบ้ (กัน timing attack)</summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            byte[] x = Encoding.UTF8.GetBytes(a.ToLowerInvariant());
            byte[] y = Encoding.UTF8.GetBytes(b.ToLowerInvariant());
            int diff = x.Length ^ y.Length;
            for (int i = 0; i < x.Length && i < y.Length; i++) diff |= x[i] ^ y[i];
            return diff == 0;
        }

        // ── ทดสอบการเชื่อมต่อ ───────────────────────────────────────────────

        public string TestConnection()
        {
            var problems = new List<string>();
            if (!Feature.On("OnlinePayment")) problems.Add("สวิตช์ฟีเจอร์ \"รับชำระเงินออนไลน์\" ยังปิดอยู่");
            if (!PaymentGatewayConfig.GetBool("Payment_Enabled", false)) problems.Add("ยังไม่ได้เปิด \"เปิดรับชำระเงินออนไลน์\"");
            if (!PaymentGatewayConfig.GetBool("Payso_Enabled", false)) problems.Add("ยังไม่ได้เปิดใช้เกตเวย์ Payso");
            if (string.IsNullOrEmpty(PaymentGatewayConfig.BaseUrl)) problems.Add("ยังไม่ได้ใส่ Base URL");
            if (string.IsNullOrEmpty(PaymentGatewayConfig.MerchantId)) problems.Add("ยังไม่ได้ใส่ Merchant ID");
            if (string.IsNullOrEmpty(PaymentGatewayConfig.ApiKey) && string.IsNullOrEmpty(PaymentGatewayConfig.SecretKey))
                problems.Add("ยังไม่ได้ใส่ API Key หรือ Secret Key");

            var sb = new StringBuilder();
            sb.AppendLine("โหมด: " + (PaymentGatewayConfig.IsSandbox ? "ทดสอบ (Sandbox)" : "ใช้งานจริง (Production)"));
            sb.AppendLine("Base URL: " + (PaymentGatewayConfig.BaseUrl ?? "(ยังไม่ตั้ง)"));

            if (problems.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("ยังตั้งค่าไม่ครบ:");
                foreach (string p in problems) sb.AppendLine("  • " + p);
                return sb.ToString();
            }

            // ยิงคำขอจริงด้วยยอด 1 บาท เพื่อดูว่าเชื่อมต่อได้และคำตอบหน้าตาเป็นอย่างไร
            var req = new PaymentChargeRequest
            {
                TxnRef = "TEST-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                Method = PaymentGatewayConfig.MethodCard,
                Amount = 1m,
                Description = "ทดสอบการเชื่อมต่อจากระบบ TakeTime",
                CustomerName = "Connection Test",
                SourceType = PaymentSource.Other,
                ReturnUrl = PaymentUrls.ReturnUrl(),
                CancelUrl = PaymentUrls.CancelUrl(),
                WebhookUrl = PaymentUrls.WebhookUrl()
            };

            PaymentChargeResult r = CreateCharge(req);
            sb.AppendLine();
            sb.AppendLine("ผลการยิงคำขอทดสอบ (ยอด 1 บาท):");
            sb.AppendLine("  HTTP: " + r.HttpStatus);
            sb.AppendLine("  ผล: " + (r.Success ? "สำเร็จ" : "ไม่สำเร็จ"));
            if (!string.IsNullOrEmpty(r.Message)) sb.AppendLine("  ข้อความ: " + r.Message);
            if (!string.IsNullOrEmpty(r.ProviderTxnId)) sb.AppendLine("  รหัสรายการฝั่ง Payso: " + r.ProviderTxnId);
            if (!string.IsNullOrEmpty(r.PaymentUrl)) sb.AppendLine("  ลิงก์ชำระเงิน: " + r.PaymentUrl);
            sb.AppendLine();
            sb.AppendLine("คำขอที่ส่งไป:");
            sb.AppendLine(Truncate(r.RawRequest, 2000));
            sb.AppendLine();
            sb.AppendLine("คำตอบที่ได้ (ใช้ตัวนี้ปรับ \"ตำแหน่งฟิลด์ในคำตอบ\" ให้ตรง):");
            sb.AppendLine(Truncate(r.RawResponse, 4000));
            return sb.ToString();
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "(ว่าง)";
            return s.Length <= max ? s : s.Substring(0, max) + "… (ตัดทอน)";
        }

        // ── สร้างเนื้อคำขอจากแม่แบบ ─────────────────────────────────────────

        private string BuildRequestBody(PaymentChargeRequest req)
        {
            string tpl = PaymentGatewayConfig.Get("Payso_Request_Template", "");
            if (string.IsNullOrWhiteSpace(tpl))
            {
                // แม่แบบสำรอง เผื่อผู้ใช้ลบทิ้ง — รูปทั่วไปของเกตเวย์ไทย
                tpl = "{\"merchantId\":\"{{merchantId}}\",\"referenceNo\":\"{{ref}}\",\"amount\":{{amount}},"
                    + "\"currency\":\"{{currency}}\",\"description\":\"{{description}}\","
                    + "\"paymentMethod\":\"{{method}}\",\"returnUrl\":\"{{returnUrl}}\","
                    + "\"callbackUrl\":\"{{webhookUrl}}\"}";
            }

            decimal payable = req.Amount;
            var inv = CultureInfo.InvariantCulture;
            long satang = (long)Math.Round(payable * 100m, MidpointRounding.AwayFromZero);
            int expirySeconds = PaymentGatewayConfig.ExpiryMinutes * 60;

            var vals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            vals["merchantId"] = PaymentGatewayConfig.MerchantId;
            vals["apiKey"] = PaymentGatewayConfig.ApiKey;
            vals["ref"] = req.TxnRef ?? "";
            vals["amount"] = payable.ToString("0.00", inv);
            vals["amountSatang"] = satang.ToString(inv);
            vals["currency"] = string.IsNullOrEmpty(req.Currency) ? "THB" : req.Currency;
            vals["description"] = req.Description ?? "";
            vals["method"] = MapMethod(req.Method);
            vals["customerName"] = req.CustomerName ?? "";
            vals["customerEmail"] = req.CustomerEmail ?? "";
            vals["customerPhone"] = req.CustomerPhone ?? "";
            vals["returnUrl"] = req.ReturnUrl ?? "";
            vals["cancelUrl"] = req.CancelUrl ?? "";
            vals["webhookUrl"] = req.WebhookUrl ?? "";
            vals["expirySeconds"] = expirySeconds.ToString(inv);
            vals["expiryMinutes"] = PaymentGatewayConfig.ExpiryMinutes.ToString(inv);
            vals["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", inv);
            vals["unixTime"] = ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString(inv);

            // {{signature}} คำนวณจากค่าที่แทนแล้วทั้งหมด (ยกเว้นตัวมันเอง)
            string bodyNoSig = Substitute(tpl, vals, "");
            string sig = ComputeSignature(bodyNoSig, PaymentGatewayConfig.SecretKey,
                PaymentGatewayConfig.Get("Payso_Signature_Algo", "HMACSHA256"),
                PaymentGatewayConfig.Get("Payso_Signature_Encoding", "HEX"));

            return Substitute(tpl, vals, sig ?? "");
        }

        /// <summary>แทนตัวแปร {{...}} โดย escape ให้ปลอดภัยกับ JSON เสมอ</summary>
        private static string Substitute(string tpl, Dictionary<string, string> vals, string signature)
        {
            var sb = new StringBuilder(tpl);
            foreach (var kv in vals)
                sb.Replace("{{" + kv.Key + "}}", JsonEscape(kv.Value));
            sb.Replace("{{signature}}", JsonEscape(signature));
            return sb.ToString();
        }

        /// <summary>
        /// escape ค่าให้ใส่ในสตริง JSON ได้ (ไม่ใส่เครื่องหมายคำพูดครอบ เพราะแม่แบบใส่มาเอง)
        /// สำคัญ: กันชื่อลูกค้า/หมายเหตุที่มี " หรือขึ้นบรรทัดใหม่ ทำให้ JSON พัง
        /// </summary>
        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string quoted = JsonConvert.ToString(s);       // ได้ "....." พร้อม escape
            return quoted.Substring(1, quoted.Length - 2); // ตัดเครื่องหมายคำพูดหัวท้ายออก
        }

        private static string MapMethod(string method)
        {
            string raw = PaymentGatewayConfig.Get("Payso_Method_Map", "");
            string m = (method ?? "").ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    JObject map = JObject.Parse(raw);
                    JToken t = map[m];
                    if (t != null && t.Type != JTokenType.Null) return t.ToString();
                }
                catch { }
            }
            return m;
        }

        // ── ลายเซ็น ─────────────────────────────────────────────────────────

        private static string ComputeSignature(string payload, string secret, string algo, string encoding)
        {
            if (string.IsNullOrEmpty(algo) || algo.Equals("NONE", StringComparison.OrdinalIgnoreCase)) return null;
            if (payload == null) payload = "";

            byte[] hash;
            byte[] data = Encoding.UTF8.GetBytes(payload);
            byte[] key = Encoding.UTF8.GetBytes(secret ?? "");
            byte[] dataPlusSecret = Encoding.UTF8.GetBytes(payload + (secret ?? ""));
            string a = algo.ToUpperInvariant();

            if (a == "HMACSHA256")
            {
                using (var h = new HMACSHA256(key)) hash = h.ComputeHash(data);
            }
            else if (a == "HMACSHA512")
            {
                using (var h = new HMACSHA512(key)) hash = h.ComputeHash(data);
            }
            else if (a == "SHA256")
            {
                using (var h = SHA256.Create()) hash = h.ComputeHash(dataPlusSecret);
            }
            else if (a == "MD5")
            {
                using (var h = MD5.Create()) hash = h.ComputeHash(dataPlusSecret);
            }
            else return null;

            if (!string.IsNullOrEmpty(encoding) && encoding.Equals("BASE64", StringComparison.OrdinalIgnoreCase))
                return Convert.ToBase64String(hash);

            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        // ── HTTP ────────────────────────────────────────────────────────────

        /// <summary>ส่งคำขอ — คืน null ถ้าสำเร็จในระดับเครือข่าย (ดู http/response ต่อ), หรือข้อความผิดพลาดภาษาไทย</summary>
        private string Send(string verb, string path, string body, out string response, out int httpStatus)
        {
            response = null;
            httpStatus = 0;

            string url = PaymentGatewayConfig.BaseUrl + (path.StartsWith("/") ? path : "/" + path);

            try
            {
                var http = (HttpWebRequest)WebRequest.Create(url);
                http.Method = verb;
                http.Timeout = PaymentGatewayConfig.TimeoutSeconds * 1000;
                http.ReadWriteTimeout = PaymentGatewayConfig.TimeoutSeconds * 1000;
                http.Accept = "application/json";
                http.UserAgent = "TakeTime-BangPhra/1.0";

                ApplyAuthHeaders(http, body);

                if (!string.IsNullOrEmpty(body))
                {
                    http.ContentType = "application/json; charset=utf-8";
                    byte[] buf = Encoding.UTF8.GetBytes(body);
                    http.ContentLength = buf.Length;
                    using (Stream s = http.GetRequestStream()) s.Write(buf, 0, buf.Length);
                }

                using (var resp = (HttpWebResponse)http.GetResponse())
                {
                    httpStatus = (int)resp.StatusCode;
                    response = ReadAll(resp);
                }
                return null;
            }
            catch (WebException wex)
            {
                var resp = wex.Response as HttpWebResponse;
                if (resp != null)
                {
                    httpStatus = (int)resp.StatusCode;
                    try { response = ReadAll(resp); } catch { }
                    return null;   // เกตเวย์ตอบมาจริง (แค่เป็นรหัสผิดพลาด) → ให้ผู้เรียกอ่านเนื้อหาต่อ
                }

                if (wex.Status == WebExceptionStatus.NameResolutionFailure)
                    return "หา Base URL ไม่เจอ (ชื่อโดเมนผิด หรือเซิร์ฟเวอร์ต่อเน็ตไม่ได้): " + url;
                if (wex.Status == WebExceptionStatus.Timeout)
                    return "เกตเวย์ไม่ตอบภายใน " + PaymentGatewayConfig.TimeoutSeconds + " วินาที";
                if (wex.Status == WebExceptionStatus.TrustFailure || wex.Status == WebExceptionStatus.SecureChannelFailure)
                    return "เชื่อมต่อแบบปลอดภัยไม่ได้ (ใบรับรอง/TLS)";
                return "ติดต่อเกตเวย์ไม่ได้: " + wex.Message;
            }
            catch (Exception ex)
            {
                return "เกิดข้อผิดพลาด: " + ex.Message;
            }
        }

        private static string ReadAll(HttpWebResponse resp)
        {
            using (Stream s = resp.GetResponseStream())
            {
                if (s == null) return "";
                using (var rd = new StreamReader(s, Encoding.UTF8)) return rd.ReadToEnd();
            }
        }

        private void ApplyAuthHeaders(HttpWebRequest http, string body)
        {
            string mode = (PaymentGatewayConfig.Get("Payso_Auth_Mode", "BEARER") ?? "BEARER").ToUpperInvariant();
            string apiKey = PaymentGatewayConfig.ApiKey;

            if (!string.IsNullOrEmpty(apiKey))
            {
                if (mode == "BEARER" || mode == "BOTH")
                    http.Headers["Authorization"] = "Bearer " + apiKey;
                if (mode == "APIKEY_HEADER" || mode == "BOTH")
                {
                    string h = PaymentGatewayConfig.Get("Payso_ApiKey_Header", "X-API-Key");
                    if (!string.IsNullOrWhiteSpace(h)) http.Headers[h] = apiKey;
                }
            }

            string merchant = PaymentGatewayConfig.MerchantId;
            if (!string.IsNullOrEmpty(merchant)) http.Headers["X-Merchant-Id"] = merchant;

            // ลายเซ็นบนหัวข้อ (ถ้าเกตเวย์กำหนดให้ส่งแบบนี้)
            string sigHeader = PaymentGatewayConfig.Get("Payso_Signature_Header", "");
            if (!string.IsNullOrWhiteSpace(sigHeader))
            {
                string sig = ComputeSignature(body ?? "", PaymentGatewayConfig.SecretKey,
                    PaymentGatewayConfig.Get("Payso_Signature_Algo", "HMACSHA256"),
                    PaymentGatewayConfig.Get("Payso_Signature_Encoding", "HEX"));
                if (!string.IsNullOrEmpty(sig)) http.Headers[sigHeader] = sig;
            }

            // หัวข้อเพิ่มเติมที่ผู้ดูแลใส่เอง (บรรทัดละ ชื่อ: ค่า)
            string extra = PaymentGatewayConfig.Get("Payso_Extra_Headers", "");
            if (!string.IsNullOrWhiteSpace(extra))
            {
                foreach (string line in extra.Split('\n'))
                {
                    string l = line.Trim();
                    if (l.Length == 0) continue;
                    int i = l.IndexOf(':');
                    if (i <= 0) continue;
                    string name = l.Substring(0, i).Trim();
                    string val = l.Substring(i + 1).Trim();
                    if (name.Length == 0) continue;
                    try { http.Headers[name] = val; } catch { }
                }
            }
        }

        // ── อ่านคำตอบตาม map ────────────────────────────────────────────────

        private static JObject TryParse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                JToken t = JToken.Parse(json);
                return t as JObject;
            }
            catch { return null; }
        }

        private static Dictionary<string, string[]> _mapCache;
        private static string _mapCacheSource;
        private static readonly object _mapLock = new object();

        private static Dictionary<string, string[]> ResponseMap()
        {
            string raw = PaymentGatewayConfig.Get("Payso_Response_Map", "");
            lock (_mapLock)
            {
                if (_mapCache != null && _mapCacheSource == raw) return _mapCache;

                var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        JObject o = JObject.Parse(raw);
                        foreach (var prop in o.Properties())
                        {
                            var paths = new List<string>();
                            if (prop.Value.Type == JTokenType.Array)
                            {
                                foreach (JToken t in (JArray)prop.Value)
                                {
                                    string p = t == null ? null : t.ToString();
                                    if (!string.IsNullOrWhiteSpace(p)) paths.Add(p.Trim());
                                }
                            }
                            else if (prop.Value.Type != JTokenType.Null)
                            {
                                string p = prop.Value.ToString();
                                if (!string.IsNullOrWhiteSpace(p)) paths.Add(p.Trim());
                            }
                            if (paths.Count > 0) map[prop.Name] = paths.ToArray();
                        }
                    }
                }
                catch { /* map พัง → ตกไปใช้ชื่อฟิลด์ตรง ๆ */ }

                _mapCache = map;
                _mapCacheSource = raw;
                return map;
            }
        }

        /// <summary>อ่านค่าตามเส้นทางที่ map ไว้ — ลองทีละตัวจนกว่าจะเจอค่าที่ไม่ว่าง</summary>
        private static string ReadMapped(JObject root, string logicalName)
        {
            if (root == null) return null;

            string[] paths;
            if (!ResponseMap().TryGetValue(logicalName, out paths) || paths.Length == 0)
                paths = new[] { logicalName, "data." + logicalName };

            foreach (string p in paths)
            {
                try
                {
                    JToken t = root.SelectToken(p);
                    if (t == null || t.Type == JTokenType.Null) continue;
                    string v = t.Type == JTokenType.Object || t.Type == JTokenType.Array
                        ? t.ToString(Formatting.None) : t.ToString();
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
                catch { }
            }
            return null;
        }

        private static decimal? ReadDecimal(JObject root, string logicalName)
        {
            string s = ReadMapped(root, logicalName);
            if (string.IsNullOrWhiteSpace(s)) return null;
            decimal v;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }

        /// <summary>แปลคำสถานะของเกตเวย์เป็นสถานะภายในระบบ ตามรายการคำที่ตั้งไว้ในหน้าตั้งค่า</summary>
        public static string TranslateStatus(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim();

            if (Matches(s, PaymentGatewayConfig.Get("Payso_Status_Paid", ""))) return PaymentStatus.Paid;
            if (Matches(s, PaymentGatewayConfig.Get("Payso_Status_Failed", ""))) return PaymentStatus.Failed;
            if (Matches(s, PaymentGatewayConfig.Get("Payso_Status_Pending", ""))) return PaymentStatus.Pending;

            // ไม่รู้จัก → ถือว่ายังรออยู่ ปลอดภัยกว่าเดาว่าจ่ายแล้ว
            return PaymentStatus.Pending;
        }

        private static bool Matches(string value, string csvList)
        {
            if (string.IsNullOrWhiteSpace(csvList)) return false;
            foreach (string part in csvList.Split(','))
            {
                string p = part.Trim();
                if (p.Length == 0) continue;
                if (string.Equals(p, value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
