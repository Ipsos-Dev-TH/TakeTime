using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Take_Time_BangPhra.Payments
{
    /// <summary>
    /// ตัวเชื่อมเกตเวย์ Omise (Opn Payments — docs.omise.co)
    ///
    /// สัญญา API ที่ใช้ (ยืนยันจากเอกสารสาธารณะของ Omise):
    ///   • Auth: HTTP Basic — username = Secret Key (skey_...), password ว่าง
    ///   • จำนวนเงินเป็น "สตางค์" เสมอ (12300 = ฿123.00)
    ///   • POST /charges                    — สร้างรายการ (source[type]=promptpay ฝั่งเซิร์ฟเวอร์ได้,
    ///                                        card=tokn_... จาก Omise.js, capture=false = กันวงเงิน)
    ///   • POST /charges/{id}/capture       — ตัดเงินจากวงเงินที่กัน (capture_amount = ตัดบางส่วน
    ///                                        ส่วนที่เหลือคืนอัตโนมัติ)
    ///   • POST /charges/{id}/reverse       — คืนวงเงินที่กันไว้ทั้งหมด
    ///   • POST /charges/{id}/refunds       — คืนเงินรายการที่ตัดแล้ว (amount สตางค์)
    ///   • GET  /charges/{id}               — อ่านสถานะ
    ///   • วงเงินที่กันไว้ (authorized) หมดอายุเอง 7 วัน
    ///
    /// ⚠ ความปลอดภัยของ webhook: เนื้อหา webhook ของ Omise ถือเป็น "คำใบ้" เท่านั้น
    ///   แนวทางที่ Omise แนะนำคือดึงข้อมูล charge กลับจาก API ด้วย Secret Key ก่อนเชื่อทุกครั้ง
    ///   — ParseWebhook ที่นี่จึงยิง GET /charges/{id} เสมอ แล้วรายงานสถานะจากคำตอบ API
    ///   ไม่ใช่จากเนื้อ webhook ⇒ ของปลอมยิงมาก็ไม่มีผล (SignatureValid = ผลการยืนยันกับ API)
    ///
    /// ⚠ ข้อมูลบัตรไม่ผ่านเซิร์ฟเวอร์เราเด็ดขาด — หน้า Payment/Card ใช้ Omise.js ส่งเข้า vault
    ///   ของ Omise ด้วย Public Key แล้วส่งมาแค่ token (tokn_...)
    ///
    /// โหมดทดสอบ/จริงดูจาก prefix ของกุญแจเอง (skey_test_ / pkey_test_) ไม่ต้องตั้งแยก
    /// </summary>
    public class OmiseGateway : IPaymentGateway, IDepositGateway
    {
        public const string Provider = "OMISE";
        private const string ApiBase = "https://api.omise.co";

        static OmiseGateway()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
                    | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch { }
        }

        public string ProviderCode { get { return Provider; } }
        public string DisplayName { get { return "Omise"; } }

        public static string SecretKey { get { return PaymentGatewayConfig.Get("Omise_SecretKey", ""); } }
        public static string PublicKey { get { return PaymentGatewayConfig.Get("Omise_PublicKey", ""); } }
        public static bool IsTestKey
        {
            get
            {
                string k = SecretKey ?? "";
                return k.Length == 0 || k.IndexOf("_test_", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public bool IsReady
        {
            get
            {
                if (!PaymentGatewayConfig.IsEnabled) return false;
                if (!PaymentGatewayConfig.GetBool("Omise_Enabled", false)) return false;
                return !string.IsNullOrEmpty(SecretKey);
            }
        }

        // ── สร้างรายการชำระเงิน ──────────────────────────────────────────────

        public PaymentChargeResult CreateCharge(PaymentChargeRequest req)
        {
            var result = new PaymentChargeResult { TxnRef = req.TxnRef, Status = PaymentStatus.Failed };

            if (!IsReady)
            {
                result.Message = "ยังไม่ได้ตั้งค่า Omise (Secret Key) หรือยังปิดใช้งานอยู่";
                return result;
            }

            string method = (req.Method ?? "").ToUpperInvariant();

            // ── บัตรเครดิต: ต้องให้ลูกค้ากรอกบัตรผ่าน Omise.js ก่อน → พาไปหน้าบัตรของเรา
            //    (การตัดเงินจริงเกิดใน ChargeWithToken หลังได้ token) ──
            if (method == PaymentGatewayConfig.MethodCard || method == PaymentGatewayConfig.MethodInstallment)
            {
                if (string.IsNullOrEmpty(PublicKey))
                {
                    result.Message = "ยังไม่ได้ตั้ง Omise Public Key (ใช้กรอกบัตรฝั่งลูกค้า)";
                    return result;
                }
                result.Success = true;
                result.Status = PaymentStatus.Pending;
                result.PaymentUrl = PaymentUrls.SiteBase() + "/Payment/Card?ref="
                                  + Uri.EscapeDataString(req.TxnRef ?? "");
                result.Message = "รอลูกค้ากรอกข้อมูลบัตร";
                return result;
            }

            // ── QR พร้อมเพย์ (ตัดยอดอัตโนมัติ): สร้าง charge พร้อม source ฝั่งเซิร์ฟเวอร์ ──
            var form = new Dictionary<string, string>
            {
                { "amount", Satang(req.Amount).ToString(CultureInfo.InvariantCulture) },
                { "currency", "thb" },
                { "source[type]", "promptpay" },
                { "description", Trunc(req.Description, 240) },
                { "metadata[txn_ref]", req.TxnRef ?? "" },
                { "metadata[source_type]", req.SourceType ?? "" }
            };

            string response; int http;
            string err = Send("POST", "/charges", form, out response, out http);
            result.RawRequest = FormDump(form);
            result.RawResponse = response;
            result.HttpStatus = http;
            if (err != null) { result.Message = err; return result; }

            JObject c = TryParse(response);
            if (c == null) { result.Message = "คำตอบจาก Omise ไม่ใช่ JSON (HTTP " + http + ")"; return result; }

            if (IsErrorObject(c, result)) return result;

            result.ProviderTxnId = S(c, "id");
            string qr = S(c, "source.scannable_code.image.download_uri");
            if (string.IsNullOrEmpty(qr)) qr = S(c, "source.scannable_code.image.uri");
            result.QrPayload = qr;

            string status = MapChargeStatus(c);
            if (status == PaymentStatus.Failed)
            {
                result.Status = PaymentStatus.Failed;
                result.Message = FailureText(c);
                return result;
            }

            result.Success = true;
            result.Status = status == PaymentStatus.Paid ? PaymentStatus.Paid : PaymentStatus.Pending;
            return result;
        }

        /// <summary>
        /// ตัดเงินด้วย token บัตรจาก Omise.js (เรียกจากหน้า Payment/Card)
        /// holdOnly = true → กันวงเงินไว้เฉย ๆ (capture=false) สำหรับเงินประกัน
        /// ถ้าบัตรต้องผ่าน 3-D Secure จะได้ authorize_uri กลับมาให้พาลูกค้าไปยืนยัน
        /// </summary>
        public PaymentChargeResult ChargeWithToken(string cardToken, decimal amount, string reference,
            string description, bool holdOnly, string returnUri)
        {
            var result = new PaymentChargeResult { TxnRef = reference, Status = PaymentStatus.Failed };
            if (!IsReady) { result.Message = "Omise ยังไม่พร้อมใช้งาน"; return result; }
            if (string.IsNullOrEmpty(cardToken) || !cardToken.StartsWith("tokn_"))
            {
                result.Message = "token บัตรไม่ถูกต้อง";
                return result;
            }

            var form = new Dictionary<string, string>
            {
                { "amount", Satang(amount).ToString(CultureInfo.InvariantCulture) },
                { "currency", "thb" },
                { "card", cardToken },
                { "capture", holdOnly ? "false" : "true" },
                { "description", Trunc(description, 240) },
                { "metadata[txn_ref]", reference ?? "" }
            };
            if (!string.IsNullOrEmpty(returnUri)) form["return_uri"] = returnUri;

            string response; int http;
            string err = Send("POST", "/charges", form, out response, out http);
            result.RawRequest = FormDump(form);
            result.RawResponse = response;
            result.HttpStatus = http;
            if (err != null) { result.Message = err; return result; }

            JObject c = TryParse(response);
            if (c == null) { result.Message = "คำตอบจาก Omise ไม่ใช่ JSON"; return result; }
            if (IsErrorObject(c, result)) return result;

            result.ProviderTxnId = S(c, "id");

            // บัตร 3DS: status=pending + authorize_uri → ลูกค้าต้องไปยืนยันกับธนาคารก่อน
            string authorizeUri = S(c, "authorize_uri");
            if (!string.IsNullOrEmpty(authorizeUri) && B(c, "authorized") != true && B(c, "paid") != true)
            {
                result.Success = true;
                result.Status = PaymentStatus.Pending;
                result.PaymentUrl = authorizeUri;
                result.Message = "รอยืนยันตัวตนกับธนาคาร (3-D Secure)";
                return result;
            }

            string status = MapChargeStatus(c);
            if (holdOnly)
            {
                // สำหรับ hold: authorized=true คือสำเร็จ (เงินยังไม่ออกจากบัตร)
                if (B(c, "authorized") == true && B(c, "paid") != true)
                {
                    result.Success = true;
                    result.Status = PaymentStatus.Pending;   // ฝั่ง hold ใช้สถานะของตัวเองใน SecurityHoldService
                    return result;
                }
            }
            if (status == PaymentStatus.Failed)
            {
                result.Message = FailureText(c);
                return result;
            }

            result.Success = true;
            result.Status = status;
            return result;
        }

        // ── ถามสถานะ ────────────────────────────────────────────────────────

        public PaymentStatusResult QueryStatus(string providerTxnId, string txnRef)
        {
            var result = new PaymentStatusResult();
            if (!IsReady) { result.Message = "Omise ยังไม่พร้อมใช้งาน"; return result; }
            if (string.IsNullOrEmpty(providerTxnId)) { result.Message = "ไม่มีรหัสรายการฝั่ง Omise"; return result; }

            string response; int http;
            string err = Send("GET", "/charges/" + Uri.EscapeDataString(providerTxnId), null, out response, out http);
            result.RawResponse = response;
            result.HttpStatus = http;
            if (err != null) { result.Message = err; return result; }

            JObject c = TryParse(response);
            if (c == null) { result.Message = "คำตอบไม่ใช่ JSON (HTTP " + http + ")"; return result; }

            var errProbe = new PaymentChargeResult();
            if (IsErrorObject(c, errProbe)) { result.Message = errProbe.Message; return result; }

            result.Success = true;
            result.ProviderTxnId = S(c, "id");
            result.Status = MapChargeStatus(c);
            result.Amount = Baht(L(c, "amount"));
            result.Fee = FeeBaht(c);
            result.CardBrand = S(c, "card.brand");
            result.CardLast4 = S(c, "card.last_digits");
            if (result.Status == PaymentStatus.Failed) result.Message = FailureText(c);
            return result;
        }

        // ── คืนเงิน (รายการที่ตัดแล้ว) ───────────────────────────────────────

        public PaymentStatusResult Refund(string providerTxnId, string txnRef, decimal amount, string reason)
        {
            var result = new PaymentStatusResult();
            if (!IsReady) { result.Message = "Omise ยังไม่พร้อมใช้งาน"; return result; }

            var form = new Dictionary<string, string>
            {
                { "amount", Satang(amount).ToString(CultureInfo.InvariantCulture) },
                { "metadata[reason]", Trunc(reason, 200) }
            };

            string response; int http;
            string err = Send("POST", "/charges/" + Uri.EscapeDataString(providerTxnId ?? "") + "/refunds",
                form, out response, out http);
            result.RawResponse = response;
            result.HttpStatus = http;
            if (err != null) { result.Message = err; return result; }

            JObject o = TryParse(response);
            var errProbe = new PaymentChargeResult();
            if (o != null && IsErrorObject(o, errProbe)) { result.Message = errProbe.Message; return result; }

            result.Success = http >= 200 && http < 300;
            if (result.Success) result.Status = PaymentStatus.Refunded;
            return result;
        }

        // ── กันวงเงิน (IDepositGateway) ─────────────────────────────────────

        public HoldResult CreateHold(string cardToken, decimal amount, string reference, string description)
        {
            PaymentChargeResult r = ChargeWithToken(cardToken, amount, reference, description,
                holdOnly: true, returnUri: PaymentUrls.SiteBase() + "/Payment/PayResult?ref="
                    + Uri.EscapeDataString(reference ?? ""));
            var h = ToHoldResult(TryParse(r.RawResponse));
            h.RawResponse = r.RawResponse;
            if (!r.Success)
            {
                h.Success = false;
                h.Status = HoldStatus.Failed;
                h.Message = r.Message;
            }
            else if (!string.IsNullOrEmpty(r.PaymentUrl) && r.PaymentUrl.Contains("omise"))
            {
                // 3DS — ยังไม่จบ ให้ผู้เรียกพาลูกค้าไป authorize_uri แล้วรอ webhook/ตรวจซ้ำ
                h.Success = true;
                h.Status = HoldStatus.PendingCard;
                h.Message = r.PaymentUrl;   // authorize_uri
            }
            return h;
        }

        public HoldResult CaptureHold(string providerChargeId, decimal amount)
        {
            var form = new Dictionary<string, string>();
            // capture_amount = ตัดบางส่วน (ส่วนที่เหลือ Omise คืนวงเงินให้เอง)
            if (amount > 0) form["capture_amount"] = Satang(amount).ToString(CultureInfo.InvariantCulture);

            string response; int http;
            string err = Send("POST", "/charges/" + Uri.EscapeDataString(providerChargeId ?? "") + "/capture",
                form.Count > 0 ? form : null, out response, out http);

            if (err != null) return new HoldResult { Success = false, Status = HoldStatus.Failed, Message = err };

            JObject c = TryParse(response);
            var probe = new PaymentChargeResult();
            if (c == null || IsErrorObject(c, probe))
                return new HoldResult { Success = false, Status = HoldStatus.Failed,
                                        Message = probe.Message ?? ("HTTP " + http), RawResponse = response };

            var h = ToHoldResult(c);
            h.RawResponse = response;
            h.Success = B(c, "paid") == true;
            h.Status = h.Success ? HoldStatus.Captured : HoldStatus.Failed;
            if (!h.Success) h.Message = FailureText(c);
            return h;
        }

        public HoldResult ReleaseHold(string providerChargeId)
        {
            string response; int http;
            string err = Send("POST", "/charges/" + Uri.EscapeDataString(providerChargeId ?? "") + "/reverse",
                null, out response, out http);
            if (err != null) return new HoldResult { Success = false, Status = HoldStatus.Failed, Message = err };

            JObject c = TryParse(response);
            var probe = new PaymentChargeResult();
            if (c == null || IsErrorObject(c, probe))
                return new HoldResult { Success = false, Status = HoldStatus.Failed,
                                        Message = probe.Message ?? ("HTTP " + http), RawResponse = response };

            var h = ToHoldResult(c);
            h.RawResponse = response;
            bool reversed = B(c, "reversed") == true || S(c, "status") == "reversed";
            h.Success = reversed;
            h.Status = reversed ? HoldStatus.Released : HoldStatus.Failed;
            return h;
        }

        public HoldResult GetHold(string providerChargeId)
        {
            string response; int http;
            string err = Send("GET", "/charges/" + Uri.EscapeDataString(providerChargeId ?? ""),
                null, out response, out http);
            if (err != null) return new HoldResult { Success = false, Status = HoldStatus.Failed, Message = err };

            JObject c = TryParse(response);
            var probe = new PaymentChargeResult();
            if (c == null || IsErrorObject(c, probe))
                return new HoldResult { Success = false, Status = HoldStatus.Failed,
                                        Message = probe.Message ?? ("HTTP " + http), RawResponse = response };

            // สถานะแปลใน ToHoldResult ที่เดียว — เดิมเขียนซ้ำที่นี่ด้วย พอที่หนึ่งลืมอัปเดต
            // ก็เพี้ยนคนละทางกัน (ต้นเหตุของเคส "authorize สำเร็จแต่ระบบว่าไม่สำเร็จ")
            var h = ToHoldResult(c);
            h.RawResponse = response;
            h.Success = true;   // = ถามสถานะได้สำเร็จ (ไม่ได้แปลว่าวงเงินยังอยู่)
            return h;
        }

        /// <summary>
        /// แปลง charge ของ Omise เป็นผลการกันวงเงิน — <b>รวมสถานะด้วย</b>
        ///
        /// ⚠ เดิมเมธอดนี้เติมแค่ id/ยอด/บัตร/วันหมดอายุ ไม่เคยตั้ง Status เลย ⇒ CreateHold
        /// คืน Status = null ทั้งที่ Omise ตอบ authorized:true (กันวงเงินสำเร็จจริง) แล้ว
        /// SaveGatewayResult แปลง null เป็น FAILED ⇒ เงินถูกกันบนบัตรลูกค้าจริง
        /// แต่ระบบบันทึกว่า "ไม่สำเร็จ" — วงเงินลอยค้างโดยไม่มีใครรู้
        ///
        /// สถานะของ charge ที่กันวงเงิน (capture=false) คือ status:"pending" + authorized:true
        /// ⇒ ต้องดู authorized ไม่ใช่ status เพียงอย่างเดียว
        /// </summary>
        private static HoldResult ToHoldResult(JObject c)
        {
            var h = new HoldResult();
            if (c == null) return h;

            string st = (S(c, "status") ?? "").ToLowerInvariant();
            bool paid = B(c, "paid") == true;
            bool reversed = B(c, "reversed") == true || st == "reversed";
            bool authorized = B(c, "authorized") == true;

            if (st == "failed") h.Status = HoldStatus.Failed;
            else if (reversed) h.Status = HoldStatus.Released;
            else if (st == "expired") h.Status = HoldStatus.Expired;
            else if (paid) h.Status = HoldStatus.Captured;       // ตัดเงินแล้ว
            else if (authorized) h.Status = HoldStatus.Held;     // กันวงเงินอยู่ เงินยังไม่ออก
            else h.Status = HoldStatus.PendingCard;              // ยังไม่ผ่าน authorize (เช่น รอ 3DS)

            h.Success = h.Status != HoldStatus.Failed;
            if (h.Status == HoldStatus.Failed) h.Message = FailureText(c);

            h.ProviderChargeId = S(c, "id");
            h.Amount = Baht(L(c, "amount"));
            long cap = L(c, "captured_amount");
            h.CapturedAmount = cap > 0 ? Baht(cap) : (B(c, "paid") == true ? h.Amount : 0m);
            h.CardBrand = S(c, "card.brand");
            h.CardLast4 = S(c, "card.last_digits");
            DateTime exp;
            if (DateTime.TryParse(S(c, "expires_at"), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out exp))
                h.ExpiresAt = exp.ToLocalTime();
            return h;
        }

        // ── webhook ─────────────────────────────────────────────────────────

        /// <summary>
        /// รับ event จาก Omise — ไม่เชื่อเนื้อ webhook เลย: อ่านแค่ id ของ charge
        /// แล้วดึงตัวจริงจาก API ด้วย Secret Key มาตอบ (แนวทางที่ Omise แนะนำ)
        /// ⇒ SignatureValid = "ยืนยันกับ API แล้วว่ารายการนี้มีจริงและสถานะตรง"
        /// </summary>
        public PaymentWebhookEvent ParseWebhook(NameValueCollection headers, string body, string remoteIp)
        {
            var ev = new PaymentWebhookEvent();

            JObject root = TryParse(body);
            if (root == null) { ev.Message = "เนื้อหาไม่ใช่ JSON"; return ev; }

            ev.EventId = S(root, "id");                        // evnt_...
            ev.EventType = S(root, "key");                     // charge.complete / charge.create ...
            string chargeId = S(root, "data.id");
            if (string.IsNullOrEmpty(chargeId)) chargeId = S(root, "id");

            if (string.IsNullOrEmpty(chargeId) || !chargeId.StartsWith("chrg_"))
            {
                ev.Message = "ไม่พบรหัส charge ในเหตุการณ์";
                return ev;
            }

            // ดึงตัวจริงจาก API — ถ้าดึงไม่ได้ = ยืนยันไม่ได้ = ไม่เชื่อ
            string response; int http;
            string err = Send("GET", "/charges/" + Uri.EscapeDataString(chargeId), null, out response, out http);
            JObject c = err == null ? TryParse(response) : null;
            var probe = new PaymentChargeResult();
            if (c == null || IsErrorObject(c, probe))
            {
                ev.SignatureValid = false;
                ev.Message = "ยืนยันรายการกับ Omise ไม่สำเร็จ: " + (err ?? probe.Message ?? ("HTTP " + http));
                return ev;
            }

            ev.SignatureValid = true;
            ev.ProviderTxnId = S(c, "id");
            ev.TxnRef = S(c, "metadata.txn_ref");
            ev.RawStatus = S(c, "status") + "/authorized=" + B(c, "authorized") + "/paid=" + B(c, "paid");
            ev.Status = MapChargeStatus(c);
            ev.Amount = Baht(L(c, "amount"));
            ev.Fee = FeeBaht(c);
            ev.CardBrand = S(c, "card.brand");
            ev.CardLast4 = S(c, "card.last_digits");
            if (ev.Status == PaymentStatus.Failed) ev.Message = FailureText(c);
            return ev;
        }

        // ── ทดสอบการเชื่อมต่อ ───────────────────────────────────────────────

        public string TestConnection()
        {
            var sb = new StringBuilder();
            sb.AppendLine("เกตเวย์: Omise");
            sb.AppendLine("โหมด: " + (IsTestKey ? "ทดสอบ (คีย์ test)" : "ใช้งานจริง (คีย์ live)"));

            if (string.IsNullOrEmpty(SecretKey)) { sb.AppendLine("❌ ยังไม่ได้ใส่ Secret Key"); return sb.ToString(); }
            if (string.IsNullOrEmpty(PublicKey))
                sb.AppendLine("⚠ ยังไม่ได้ใส่ Public Key — QR ใช้ได้ แต่บัตร/กันวงเงินจะใช้ไม่ได้");

            string response; int http;
            string err = Send("GET", "/account", null, out response, out http);
            if (err != null) { sb.AppendLine("❌ ต่อ Omise ไม่ได้: " + err); return sb.ToString(); }

            JObject a = TryParse(response);
            var probe = new PaymentChargeResult();
            if (a == null || IsErrorObject(a, probe))
            {
                sb.AppendLine("❌ Omise ปฏิเสธ (HTTP " + http + "): " + (probe.Message ?? Trunc(response, 300)));
                return sb.ToString();
            }

            sb.AppendLine("✅ เชื่อมต่อสำเร็จ — บัญชี: " + S(a, "email")
                + (S(a, "team") != null ? " / " + S(a, "team") : ""));
            sb.AppendLine();
            sb.AppendLine("Webhook ที่ต้องตั้งใน Omise Dashboard → Webhooks:");
            sb.AppendLine("  " + PaymentUrls.WebhookUrl());
            return sb.ToString();
        }

        // ── แปลงสถานะ ───────────────────────────────────────────────────────

        /// <summary>
        /// สถานะ Omise → สถานะภายใน — เข้มงวดฝั่ง "จ่ายแล้ว": ต้อง paid=true เท่านั้น
        /// (authorized เฉย ๆ = แค่กันวงเงิน เงินยังไม่เข้า)
        /// </summary>
        private static string MapChargeStatus(JObject c)
        {
            string s = (S(c, "status") ?? "").ToLowerInvariant();
            bool paid = B(c, "paid") == true;

            if (paid && s == "successful") return PaymentStatus.Paid;
            if (s == "failed") return PaymentStatus.Failed;
            if (s == "expired") return PaymentStatus.Expired;
            if (s == "reversed") return PaymentStatus.Cancelled;
            return PaymentStatus.Pending;   // pending / authorized-not-captured / ไม่รู้จัก = ยังไม่จ่าย
        }

        private static string FailureText(JObject c)
        {
            string code = S(c, "failure_code"), msg = S(c, "failure_message");
            if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(msg)) return "รายการไม่สำเร็จ";
            return (msg ?? "") + (string.IsNullOrEmpty(code) ? "" : " (" + code + ")");
        }

        private static bool IsErrorObject(JObject o, PaymentChargeResult fillMessage)
        {
            if (o == null) return false;
            if (S(o, "object") != "error") return false;
            fillMessage.Message = "Omise: " + (S(o, "message") ?? "error")
                + (S(o, "code") != null ? " (" + S(o, "code") + ")" : "");
            return true;
        }

        // ── HTTP (form-encoded ตามเอกสาร Omise) ─────────────────────────────

        private static string Send(string verb, string path, Dictionary<string, string> form,
            out string response, out int httpStatus)
        {
            response = null; httpStatus = 0;
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(ApiBase + path);
                req.Method = verb;
                req.Timeout = 30000;
                req.ReadWriteTimeout = 30000;
                req.Accept = "application/json";
                req.UserAgent = "TakeTime-BangPhra/1.0";
                req.Headers["Authorization"] = "Basic " + Convert.ToBase64String(
                    Encoding.ASCII.GetBytes((SecretKey ?? "") + ":"));

                if (form != null)
                {
                    var sb = new StringBuilder();
                    foreach (var kv in form)
                    {
                        if (kv.Value == null) continue;
                        if (sb.Length > 0) sb.Append('&');
                        sb.Append(Uri.EscapeDataString(kv.Key)).Append('=')
                          .Append(Uri.EscapeDataString(kv.Value));
                    }
                    byte[] body = Encoding.UTF8.GetBytes(sb.ToString());
                    req.ContentType = "application/x-www-form-urlencoded";
                    req.ContentLength = body.Length;
                    using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);
                }
                else if (verb != "GET")
                {
                    req.ContentLength = 0;
                }

                using (var resp = (HttpWebResponse)req.GetResponse())
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
                    return null;   // Omise ตอบ error object มา — ให้ผู้เรียกอ่านต่อ
                }
                if (wex.Status == WebExceptionStatus.NameResolutionFailure)
                    return "ต่อ api.omise.co ไม่ได้ (DNS/เครือข่าย)";
                if (wex.Status == WebExceptionStatus.Timeout)
                    return "Omise ไม่ตอบภายใน 30 วินาที";
                return "ติดต่อ Omise ไม่ได้: " + wex.Message;
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

        // ── helpers ─────────────────────────────────────────────────────────

        /// <summary>บาท → สตางค์ (จำนวนเต็ม) — ปัดครึ่งขึ้นแบบการเงิน</summary>
        public static long Satang(decimal baht)
        {
            return (long)Math.Round(baht * 100m, MidpointRounding.AwayFromZero);
        }

        public static decimal Baht(long satang) { return satang / 100m; }

        /// <summary>ค่าธรรมเนียมรวม VAT ของค่าธรรมเนียม เป็นบาท (fee + fee_vat)</summary>
        private static decimal? FeeBaht(JObject c)
        {
            long fee = L(c, "fee"), vat = L(c, "fee_vat");
            if (fee <= 0 && vat <= 0) return null;
            return Baht(fee + vat);
        }

        private static JObject TryParse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JToken.Parse(json) as JObject; } catch { return null; }
        }

        private static string S(JObject o, string path)
        {
            try
            {
                JToken t = o == null ? null : o.SelectToken(path);
                if (t == null || t.Type == JTokenType.Null) return null;
                string v = t.ToString();
                return string.IsNullOrWhiteSpace(v) ? null : v;
            }
            catch { return null; }
        }

        private static long L(JObject o, string path)
        {
            long v;
            return long.TryParse(S(o, path), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        private static bool? B(JObject o, string path)
        {
            string s = S(o, path);
            if (s == null) return null;
            bool v;
            return bool.TryParse(s, out v) ? v : (bool?)null;
        }

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return s.Length <= max ? s : s.Substring(0, max);
        }

        /// <summary>สำเนาคำขอไว้ตรวจย้อนหลัง — ไม่มีข้อมูลบัตรอยู่แล้ว (มีแต่ token ใช้ครั้งเดียว)</summary>
        private static string FormDump(Dictionary<string, string> form)
        {
            var sb = new StringBuilder();
            foreach (var kv in form) sb.AppendLine(kv.Key + "=" + kv.Value);
            return sb.ToString();
        }
    }
}
