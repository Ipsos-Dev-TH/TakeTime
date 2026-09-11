using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Text;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Payments
{
    /// <summary>
    /// สมองของระบบรับชำระเงินออนไลน์ — หน้าจอทุกหน้าคุยกับคลาสนี้เท่านั้น
    /// ไม่ต้องรู้ว่าเบื้องหลังเป็นเกตเวย์เจ้าไหน
    ///
    /// หลักการที่ยึด (เรื่องเงิน ผิดไม่ได้):
    ///   1. ฟีเจอร์ปิด = ทุกเมธอดคืนค่าที่แปลว่า "ใช้ทางเดิม" ⇒ ระบบเดิมไม่เปลี่ยนพฤติกรรม
    ///   2. เปลี่ยนเป็น "จ่ายแล้ว" ได้ครั้งเดียว (UPDATE … WHERE Status &lt;&gt; 'PAID')
    ///   3. ลงบันทึกเข้าระบบเดิมได้ครั้งเดียว (จองสิทธิ์ด้วย Applied_At)
    ///   4. ไม่รู้จักสถานะที่เกตเวย์ตอบมา = ถือว่ายังไม่จ่าย (ไม่เดาว่าจ่ายแล้ว)
    ///   5. เก็บคำขอ-คำตอบดิบทุกครั้ง ตรวจย้อนหลังได้เสมอ
    /// </summary>
    public class OnlinePaymentService
    {
        private readonly string _conn;
        private readonly PaymentTransactionStore _store;
        private readonly code _code = new code();

        public OnlinePaymentService()
            : this(ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString) { }

        public OnlinePaymentService(string connectionString)
        {
            _conn = connectionString;
            _store = new PaymentTransactionStore(connectionString);
        }

        public PaymentTransactionStore Store { get { return _store; } }

        /// <summary>
        /// เกตเวย์ที่ใช้อยู่ — สลับได้จากหน้าตั้งค่า (Payment_Provider = OMISE/PAYSO)
        /// ไม่ต้อง build ใหม่ และหน้าจอทุกหน้าไม่ต้องรู้ว่าข้างหลังเป็นเจ้าไหน
        /// ส่ง provider มาเมื่อรายการเก่าถูกสร้างด้วยเจ้าอื่น (เช่นถามสถานะย้อนหลังหลังสลับเจ้า)
        /// </summary>
        public IPaymentGateway Gateway(string provider = null)
        {
            string p = string.IsNullOrEmpty(provider) ? PaymentGatewayConfig.ActiveProvider
                                                      : provider.Trim().ToUpperInvariant();
            if (p == PaymentGatewayConfig.ProviderPayso) return new PaysoGateway();
            return new OmiseGateway();
        }

        /// <summary>
        /// ระบบพร้อมให้ลูกค้าเลือกจ่ายออนไลน์ไหม
        /// ปิดอยู่/ยังไม่ได้รัน migration = false ⇒ หน้าจอต้องแสดงทางเดิมเท่านั้น
        /// </summary>
        public bool IsAvailable
        {
            get
            {
                try { return PaymentGatewayConfig.IsEnabled && _store.TablesReady(); }
                catch { return false; }
            }
        }

        /// <summary>วิธีชำระที่แสดงให้ลูกค้าเลือกได้จริงกับยอดนี้</summary>
        public List<string> AvailableMethods(decimal amount)
        {
            if (!IsAvailable) return new List<string>();
            return PaymentGatewayConfig.AvailableMethods(amount);
        }

        /// <summary>
        /// เหมือนข้างบนแต่เคารพสวิตช์รายช่องทางด้วย — ช่องทางไหนปิดไว้
        /// (จอง/กิจกรรม/POS/รูมเซอร์วิส/...) ช่องนั้นได้รายการว่าง = หน้าจอไม่เสนอเลย
        /// </summary>
        public List<string> AvailableMethods(decimal amount, string sourceType)
        {
            if (!PaymentGatewayConfig.ChannelEnabled(sourceType)) return new List<string>();
            return AvailableMethods(amount);
        }

        // ── เริ่มการชำระเงิน ──────────────────────────────────────────────────

        /// <summary>
        /// สร้างรายการชำระเงิน แล้วส่งไปเกตเวย์ (ถ้าเป็นวิธีที่ต้องใช้เกตเวย์)
        /// MANUAL_QR จะไม่ยิงออกไปไหน — แค่บันทึกรายการไว้รอพนักงานตรวจสลิป (พฤติกรรมเดิม)
        /// </summary>
        public PaymentChargeResult Start(PaymentChargeRequest req)
        {
            var fail = new PaymentChargeResult { Status = PaymentStatus.Failed, TxnRef = req.TxnRef };

            if (!IsAvailable)
            {
                fail.Message = "ระบบรับชำระเงินออนไลน์ยังไม่เปิดใช้งาน";
                return fail;
            }
            if (req.Amount <= 0)
            {
                fail.Message = "ยอดชำระต้องมากกว่า 0";
                return fail;
            }

            string method = (req.Method ?? "").ToUpperInvariant();

            // หน้าทดสอบ sandbox ต้องลองวิธีที่ "ยังไม่เปิดให้ลูกค้า" ได้ — นั่นคือเหตุผลที่มีหน้านั้น
            // (ยังบังคับว่าเกตเวย์ต้องพร้อมจริงเสมอ ไม่ข้ามให้)
            if (req.IsTest)
            {
                if (method != PaymentGatewayConfig.MethodManualQr && !PaymentGatewayConfig.IsGatewayReady)
                {
                    fail.Message = PaymentGatewayConfig.DescribeMethodUnavailable(method, req.Amount)
                                   ?? "เกตเวย์ยังไม่พร้อมใช้งาน";
                    return fail;
                }
            }
            else
            {
                string why = PaymentGatewayConfig.DescribeMethodUnavailable(method, req.Amount);
                if (why != null) { fail.Message = why; return fail; }
            }
            req.Method = method;

            // ช่องทางถูกปิดไว้จากหน้าตั้งค่า = ไม่รับ ไม่ว่าลิงก์จะมาจากไหน
            if (!PaymentGatewayConfig.ChannelEnabled(req.SourceType))
            {
                fail.Message = "ช่องทางนี้ยังไม่เปิดรับชำระเงินออนไลน์";
                return fail;
            }

            // กันเก็บเงินซ้ำ: ถ้ารายการต้นทางนี้จ่ายสำเร็จไปแล้ว ไม่สร้างใหม่
            // ⚠ ยกเว้น "การจอง" — จ่ายได้หลายงวด (มัดจำก่อน แล้วจ่ายส่วนที่เหลือทีหลัง)
            //   ความถูกต้องของยอดคุมโดยหน้า Pay ที่คำนวณยอดค้างจากฐานข้อมูลทุกครั้งอยู่แล้ว
            var paid = req.SourceType == PaymentSource.Reservation
                ? null : _store.GetPaidForSource(req.SourceType, req.SourceId);
            if (paid != null)
            {
                fail.Message = "รายการนี้ชำระเงินเรียบร้อยแล้ว (อ้างอิง " + paid.TxnRef + ")";
                fail.TxnRef = paid.TxnRef;
                fail.Status = PaymentStatus.Paid;
                return fail;
            }

            // ใช้ลิงก์เดิมซ้ำถ้ายังไม่หมดอายุ — ลูกค้ากดย้อนกลับแล้วกดใหม่จะไม่ได้ 2 รายการ
            var open = _store.GetOpenForSource(req.SourceType, req.SourceId, method);
            if (open != null && !open.IsExpired
                && (!string.IsNullOrEmpty(open.PaymentUrl) || method == PaymentGatewayConfig.MethodManualQr))
            {
                return new PaymentChargeResult
                {
                    Success = true,
                    Status = PaymentStatus.Pending,
                    TxnRef = open.TxnRef,
                    ProviderTxnId = open.ProviderTxnId,
                    PaymentUrl = open.PaymentUrl,
                    QrPayload = open.QrPayload,
                    Message = "ใช้รายการชำระเงินเดิมที่ยังไม่หมดอายุ"
                };
            }

            decimal surcharge = PaymentGatewayConfig.SurchargeFor(method, req.Amount);

            // ── วิธีเดิม: สแกน QR ของร้าน แล้วแนบสลิป ──
            if (method == PaymentGatewayConfig.MethodManualQr)
            {
                var manualTxn = _store.Create(req, "MANUAL_QR", 0m);
                _store.SaveChargeResult(manualTxn.ID, new PaymentChargeResult
                {
                    Status = PaymentStatus.Pending,
                    Message = "รอลูกค้าโอนและแนบสลิป"
                });
                return new PaymentChargeResult
                {
                    Success = true,
                    Status = PaymentStatus.Pending,
                    TxnRef = manualTxn.TxnRef,
                    Message = "รอการโอนและแนบสลิป"
                };
            }

            // ── วิธีผ่านเกตเวย์ ──
            IPaymentGateway gw = Gateway();
            if (!gw.IsReady)
            {
                fail.Message = "เกตเวย์รับชำระเงินยังตั้งค่าไม่ครบ";
                return fail;
            }

            var txn = _store.Create(req, gw.ProviderCode, surcharge);

            // ยอดที่ส่งไปเกตเวย์คือยอดที่ลูกค้าจ่ายจริง (รวมค่าธรรมเนียมที่บวก ถ้ามี)
            var gwReq = new PaymentChargeRequest
            {
                TxnRef = txn.TxnRef,
                Method = method,
                SourceType = req.SourceType,
                SourceId = req.SourceId,
                Amount = txn.TotalPayable,
                Currency = txn.Currency,
                Description = req.Description,
                CustomerName = req.CustomerName,
                CustomerPhone = req.CustomerPhone,
                CustomerEmail = req.CustomerEmail,
                ReturnUrl = PaymentUrls.ReturnUrl(txn.TxnRef),
                CancelUrl = PaymentUrls.CancelUrl(txn.TxnRef),
                WebhookUrl = PaymentUrls.WebhookUrl()
            };

            PaymentChargeResult r = gw.CreateCharge(gwReq);
            r.TxnRef = txn.TxnRef;
            _store.SaveChargeResult(txn.ID, r);

            Log("Start " + txn.TxnRef + " วิธี=" + method + " ยอด=" + txn.TotalPayable.ToString("N2")
                + " ผล=" + (r.Success ? "สำเร็จ" : "ไม่สำเร็จ") + " " + (r.Message ?? ""));

            // จ่ายจบทันทีตั้งแต่ตอนสร้าง (บางเกตเวย์ทำได้) → ลงบันทึกเลย
            if (r.Success && r.Status == PaymentStatus.Paid)
            {
                if (_store.MarkPaid(txn.ID, r.ProviderTxnId, null, null, null))
                    ApplyPaid(_store.GetById(txn.ID));
            }

            return r;
        }

        // ── การแจ้งกลับจากเกตเวย์ ────────────────────────────────────────────

        public class WebhookOutcome
        {
            public int HttpStatus = 200;
            public string Message = "OK";
            public bool Accepted;
        }

        /// <summary>
        /// จัดการข้อความที่เกตเวย์แจ้งกลับมา — ต้องทนต่อการส่งซ้ำและของปลอม
        /// ตอบ 200 เสมอเมื่อ "รับไว้แล้ว" เพื่อไม่ให้เกตเวย์ยิงซ้ำไม่หยุด
        /// </summary>
        public WebhookOutcome HandleWebhook(NameValueCollection headers, string body, string remoteIp)
        {
            var outcome = new WebhookOutcome();

            if (!IsAvailable)
            {
                outcome.HttpStatus = 503;
                outcome.Message = "payment module disabled";
                return outcome;
            }

            // จำกัด IP (ถ้าตั้งไว้)
            string allow = PaymentGatewayConfig.WebhookIpAllow;
            if (!string.IsNullOrWhiteSpace(allow) && !IpAllowed(allow, remoteIp))
            {
                Log("Webhook ปฏิเสธ: IP " + remoteIp + " ไม่อยู่ในรายการที่อนุญาต");
                outcome.HttpStatus = 403;
                outcome.Message = "ip not allowed";
                return outcome;
            }

            IPaymentGateway gw = Gateway();
            PaymentWebhookEvent ev = gw.ParseWebhook(headers, body, remoteIp);

            PaymentTransaction txn = _store.GetByRef(ev.TxnRef)
                                  ?? _store.GetByProviderTxnId(gw.ProviderCode, ev.ProviderTxnId);

            int eventId;
            bool isNew = _store.LogEvent(ev, gw.ProviderCode, txn == null ? (int?)null : txn.ID,
                DumpHeaders(headers), body, remoteIp, out eventId);

            if (!isNew)
            {
                outcome.Accepted = true;
                outcome.Message = "duplicate event ignored";
                return outcome;   // เคยรับไปแล้ว — ตอบ 200 เฉย ๆ
            }

            if (!ev.SignatureValid)
            {
                Log("Webhook ปฏิเสธ: ลายเซ็นไม่ถูกต้อง (ref=" + (ev.TxnRef ?? "-") + ", ip=" + remoteIp + ")");
                _store.MarkEventHandled(eventId, "ลายเซ็นไม่ถูกต้อง — ไม่ดำเนินการ");
                outcome.HttpStatus = 401;
                outcome.Message = "invalid signature";
                return outcome;
            }

            if (txn == null)
            {
                _store.MarkEventHandled(eventId, "ไม่พบรายการที่ตรงกับเลขอ้างอิงนี้");
                outcome.Accepted = true;
                outcome.Message = "unknown reference";
                return outcome;   // ไม่ใช่ของเรา แต่รับไว้แล้ว ไม่ต้องให้ยิงซ้ำ
            }

            string note = ApplyEventToTransaction(txn, ev);
            _store.MarkEventHandled(eventId, note);
            outcome.Accepted = true;
            outcome.Message = "ok";
            return outcome;
        }

        /// <summary>เปลี่ยนสถานะรายการตามเหตุการณ์ แล้วลงบันทึกปลายทางถ้าจ่ายสำเร็จ</summary>
        private string ApplyEventToTransaction(PaymentTransaction txn, PaymentWebhookEvent ev)
        {
            if (ev.Status == PaymentStatus.Paid)
            {
                // ตรวจยอด: เกตเวย์ต้องบอกยอดที่ตรงกับที่เราขอ ไม่งั้นไม่รับ
                if (ev.Amount.HasValue && Math.Abs(ev.Amount.Value - txn.TotalPayable) > 0.05m
                    && Math.Abs(ev.Amount.Value - txn.TotalPayable * 100m) > 0.05m)   // เผื่อเกตเวย์คืนหน่วยสตางค์
                {
                    Log("Webhook " + txn.TxnRef + ": ยอดไม่ตรง (เกตเวย์=" + ev.Amount.Value.ToString("N2")
                        + " ระบบ=" + txn.TotalPayable.ToString("N2") + ") — ไม่บันทึกว่าจ่ายแล้ว");
                    return "ยอดเงินไม่ตรงกับรายการ — ต้องตรวจสอบด้วยตนเอง";
                }

                bool changed = _store.MarkPaid(txn.ID, ev.ProviderTxnId, ev.Fee, ev.CardBrand, ev.CardLast4);
                if (!changed) return "รายการนี้บันทึกว่าจ่ายแล้วก่อนหน้านี้";

                PaymentTransaction fresh = _store.GetById(txn.ID);
                string applyNote = ApplyPaid(fresh);
                NotifyPaid(fresh);
                return "บันทึกจ่ายแล้ว · " + applyNote;
            }

            if (ev.Status == PaymentStatus.Failed)
            {
                _store.MarkTerminal(txn.ID, PaymentStatus.Failed,
                    ev.Message ?? ("เกตเวย์แจ้งสถานะ " + (ev.RawStatus ?? "-")));
                return "บันทึกว่าไม่สำเร็จ";
            }

            return "สถานะยังไม่เปลี่ยน (" + (ev.RawStatus ?? "-") + ")";
        }

        // ── ตามสถานะเอง (เผื่อ webhook หาย) ─────────────────────────────────

        /// <summary>ถามสถานะรายการเดียวจากเกตเวย์แล้วอัปเดตให้ตรง</summary>
        /// <summary>
        /// ตัดเงินด้วย token บัตรจากหน้า Payment/Card (Omise.js สร้างให้ฝั่งเบราว์เซอร์ —
        /// ข้อมูลบัตรจริงไม่เคยผ่านเซิร์ฟเวอร์เรา)
        /// คืนข้อความผลลัพธ์ไทย; ถ้า authorizeUri ไม่ว่าง = ต้องพาลูกค้าไปยืนยัน 3-D Secure ต่อ
        /// </summary>
        public string ProcessCardToken(string txnRef, string cardToken, out bool ok, out string authorizeUri)
        {
            ok = false; authorizeUri = null;

            PaymentTransaction txn = _store.GetByRef(txnRef);
            if (txn == null) return "ไม่พบรายการชำระเงินนี้";
            if (txn.Status == PaymentStatus.Paid) { ok = true; return "รายการนี้ชำระเงินแล้ว"; }
            if (PaymentStatus.IsFinal(txn.Status)) return "รายการนี้ปิดไปแล้ว (" + PaymentStatus.Thai(txn.Status) + ")";
            if (txn.IsExpired) return "รายการหมดอายุแล้ว กรุณาเริ่มใหม่";

            var omise = Gateway(txn.Provider) as OmiseGateway;
            if (omise == null) return "เกตเวย์ที่ใช้อยู่ไม่รองรับการกรอกบัตรบนหน้านี้";

            PaymentChargeResult r = omise.ChargeWithToken(cardToken, txn.TotalPayable, txn.TxnRef,
                txn.Description, holdOnly: false,
                returnUri: PaymentUrls.ReturnUrl(txn.TxnRef));

            if (!r.Success)
            {
                // บัตรไม่ผ่าน = ลองใบอื่นบนลิงก์เดิมได้ ไม่ปิดรายการทิ้ง
                _store.SaveFailedAttempt(txn.ID, r);
                return "ชำระเงินไม่สำเร็จ: " + (r.Message ?? "-") + " — ลองใหม่ด้วยบัตรใบอื่นได้ทันที";
            }

            _store.SaveChargeResult(txn.ID, r);

            if (r.Status == PaymentStatus.Paid)
            {
                if (_store.MarkPaid(txn.ID, r.ProviderTxnId, null, null, null))
                {
                    PaymentTransaction fresh = _store.GetById(txn.ID);
                    ApplyPaid(fresh);
                    NotifyPaid(fresh);
                }
                ok = true;
                return "ชำระเงินเรียบร้อยแล้ว";
            }

            // 3-D Secure: ธนาคารขอยืนยันตัวตนก่อน — สถานะจริงจะตามมาทาง webhook/การถามซ้ำ
            if (!string.IsNullOrEmpty(r.PaymentUrl))
            {
                ok = true;
                authorizeUri = r.PaymentUrl;
                return "กรุณายืนยันตัวตนกับธนาคาร";
            }

            return "อยู่ระหว่างดำเนินการ — ระบบจะตรวจสถานะให้อัตโนมัติ";
        }

        /// <summary>
        /// คืนเงินรายการที่จ่ายผ่านเกตเวย์ (เต็มหรือบางส่วน)
        ///
        /// เงื่อนไขการใช้ (วิเคราะห์ไว้ใน docs/Online_Payment_Omise.md):
        ///   1. ลูกค้าจ่ายซ้ำ/ซ้อน (รายการ PAID ที่ยังไม่ถูกบันทึกเข้าใบจอง)
        ///   2. ยกเลิกการจองแบบคืนเงิน ที่รับเงินผ่านเกตเวย์
        ///   3. ยกเลิกออเดอร์/กิจกรรมหลังจ่ายแล้ว
        ///   4. เก็บผิดยอดที่จุดรับเงินหน้าร้าน (คืนบางส่วน)
        ///
        /// ⚠ การคืนเงิน "ไม่" ย้อนบันทึกฝั่ง TakeTime/NextAcc ให้ — ใบเสร็จ/Payment_History
        ///   ที่ออกไปแล้วต้องให้พนักงานยกเลิก/ปรับตามขั้นตอนเดิม (ระบบเตือนในข้อความผลลัพธ์)
        /// กันคืนเกินด้วยยอดคืนสะสม (Refunded_Amount) — Omise เองรับคืนได้ไม่เกิน 365 วัน
        /// </summary>
        public string RefundTransaction(long txnId, decimal amount, string reason, int? adminId)
        {
            PaymentTransaction txn = _store.GetById((int)txnId);
            if (txn == null) return "ไม่พบรายการ";
            if (txn.Provider == "MANUAL_QR")
                return "รายการโอน/แนบสลิปไม่ได้ผ่านเกตเวย์ — คืนเงินด้วยการโอนกลับตามปกติ";
            if (txn.Status != PaymentStatus.Paid && txn.Status != PaymentStatus.Refunded)
                return "คืนได้เฉพาะรายการที่จ่ายสำเร็จแล้ว (สถานะปัจจุบัน: " + PaymentStatus.Thai(txn.Status) + ")";
            if (amount <= 0) return "ยอดคืนต้องมากกว่า 0";

            decimal already = _store.GetRefundedAmount(txn.ID);
            decimal refundable = txn.TotalPayable - already;
            if (amount > refundable + 0.005m)
                return "คืนได้อีกไม่เกิน " + refundable.ToString("N2") + " บาท (คืนไปแล้ว "
                     + already.ToString("N2") + " จากยอด " + txn.TotalPayable.ToString("N2") + ")";

            IPaymentGateway gw = Gateway(txn.Provider);
            PaymentStatusResult r = gw.Refund(txn.ProviderTxnId, txn.TxnRef, amount, reason);
            if (r == null) return "เกตเวย์นี้ยังไม่ได้ตั้งค่าเส้นทางคืนเงิน";
            if (!r.Success) return "คืนเงินไม่สำเร็จ: " + (r.Message ?? "-");

            bool fullyRefunded = already + amount >= txn.TotalPayable - 0.005m;
            _store.RecordRefund(txn.ID, amount, fullyRefunded, reason, adminId);

            Log("Refund " + txn.TxnRef + " ยอด " + amount.ToString("N2")
                + (fullyRefunded ? " (คืนครบ)" : " (บางส่วน)") + " เหตุผล: " + (reason ?? "-"));
            Notify.Send(Notify.Ev.PaymentOnline,
                "↩️ <b>คืนเงินลูกค้า</b> " + amount.ToString("N2") + " บาท"
                + (fullyRefunded ? " (ครบทั้งยอด)" : " (บางส่วน)") + "\n"
                + Notify.E(PaymentSource.Thai(txn.SourceType) + " " + (txn.SourceId ?? ""))
                + " · อ้างอิง " + Notify.E(txn.TxnRef)
                + (string.IsNullOrEmpty(reason) ? "" : "\n📝 " + Notify.E(reason)));

            string warn = txn.AppliedAt.HasValue
                ? " ⚠ รายการนี้ถูกบันทึกเข้าระบบไปแล้ว — อย่าลืมยกเลิก/ปรับใบเสร็จหรือยอดการจองให้ตรงด้วย"
                : "";
            return "คืนเงิน " + amount.ToString("N2") + " บาท สำเร็จ"
                 + (fullyRefunded ? " (คืนครบทั้งยอด)" : " (คืนสะสม " + (already + amount).ToString("N2") + ")")
                 + warn;
        }

        public string RefreshStatus(PaymentTransaction txn)
        {
            if (txn == null) return "ไม่พบรายการ";
            if (txn.Status == PaymentStatus.Paid) return "ชำระแล้ว";
            if (txn.Provider == "MANUAL_QR") return "รอตรวจสลิป (ไม่ต้องถามเกตเวย์)";

            IPaymentGateway gw = Gateway(txn.Provider);
            if (!gw.IsReady) return "เกตเวย์ยังไม่พร้อม";

            PaymentStatusResult r = gw.QueryStatus(txn.ProviderTxnId, txn.TxnRef);
            if (!r.Success && string.IsNullOrEmpty(r.Status))
                return "ถามสถานะไม่สำเร็จ: " + (r.Message ?? "-");

            if (r.Status == PaymentStatus.Paid)
            {
                if (_store.MarkPaid(txn.ID, r.ProviderTxnId, r.Fee, r.CardBrand, r.CardLast4))
                {
                    PaymentTransaction fresh = _store.GetById(txn.ID);
                    string note = ApplyPaid(fresh);
                    NotifyPaid(fresh);
                    return "ชำระแล้ว · " + note;
                }
                return "ชำระแล้ว";
            }

            if (r.Status == PaymentStatus.Failed)
            {
                _store.MarkTerminal(txn.ID, PaymentStatus.Failed, r.Message);
                return "ไม่สำเร็จ: " + (r.Message ?? "-");
            }

            return "ยังรอชำระเงิน";
        }

        private static DateTime _lastPoll = DateTime.MinValue;
        private static readonly object _pollLock = new object();

        /// <summary>
        /// งานเบื้องหลัง — ปิดรายการหมดอายุ + ตามสถานะรายการที่ค้าง
        /// เรียกจากตัวจับเวลาเดิมใน Global.asax ได้เลย (เงียบสนิทถ้าฟีเจอร์ปิด)
        /// </summary>
        public void PollPendingIfDue()
        {
            if (!IsAvailable) return;
            if (!PaymentGatewayConfig.PollEnabled) return;

            lock (_pollLock)
            {
                if ((DateTime.Now - _lastPoll).TotalMinutes < PaymentGatewayConfig.PollMinutes) return;
                _lastPoll = DateTime.Now;
            }

            // วงเงินประกัน: ปิดรายการหมดอายุ + เตือนก่อนหมด (เงียบถ้าฟีเจอร์ปิด)
            try { new SecurityHoldService(_conn).SweepIfDue(); }
            catch (Exception hex) { Log("sweep วงเงินประกันล้มเหลว: " + hex.Message); }

            // ใบจองที่ลูกค้ากดจองแล้วไม่จ่ายสักที → ยกเลิกคืนห้อง (เงียบถ้าสวิตช์ปิด)
            try { BookingPayment.CancelStaleUnpaidIfDue(_conn); }
            catch (Exception bex) { Log("กวาดใบจองที่ไม่ชำระล้มเหลว: " + bex.Message); }

            try
            {
                int expired = _store.ExpireStale();
                if (expired > 0) Log("ปิดรายการที่หมดอายุ " + expired + " รายการ");

                foreach (PaymentTransaction t in _store.GetPendingForPoll(20))
                {
                    try { RefreshStatus(t); }
                    catch (Exception ex) { Log("ตามสถานะ " + t.TxnRef + " ล้มเหลว: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                Log("งานตามสถานะล้มเหลว: " + ex.Message);
            }
        }

        // ── ลงบันทึกเข้าระบบเดิมเมื่อจ่ายสำเร็จ ─────────────────────────────

        /// <summary>
        /// พาเงินที่รับมาเข้าสู่ระบบเดิม (Payment_History / Activity_Bookings ฯลฯ)
        /// ทำได้ครั้งเดียวต่อรายการ — ถ้าพลาดจะคืนสิทธิ์ให้รอบถัดไปลองใหม่
        /// </summary>
        public string ApplyPaid(PaymentTransaction txn)
        {
            if (txn == null) return "ไม่พบรายการ";
            if (txn.Status != PaymentStatus.Paid) return "ยังไม่ได้ชำระ";
            if (!PaymentGatewayConfig.AutoApply) return "ปิดการบันทึกอัตโนมัติไว้ — รอพนักงานยืนยัน";

            if (!_store.TryClaimApply(txn.ID))
                return "บันทึกเข้าระบบไปแล้ว";

            try
            {
                string note;
                string receiptId = null;

                switch ((txn.SourceType ?? "").ToUpperInvariant())
                {
                    case PaymentSource.Reservation:
                        note = ApplyToReservation(txn, out receiptId);
                        break;
                    case PaymentSource.RoomService:
                        note = ApplyToRoomServiceOrder(txn);
                        break;
                    case PaymentSource.Activity:
                        note = ApplyToActivityBooking(txn);
                        break;
                    case PaymentSource.Amenity:
                        note = ApplyToAmenityRequest(txn);
                        break;
                    default:
                        note = "รับเงินแล้ว — ต้นทางชนิด " + PaymentSource.Thai(txn.SourceType)
                             + " ยังไม่มีการลงบันทึกอัตโนมัติ";
                        break;
                }

                _store.SetApplied(txn.ID, note, receiptId);
                Log("ApplyPaid " + txn.TxnRef + ": " + note);
                return note;
            }
            catch (Exception ex)
            {
                _store.ReleaseApply(txn.ID, "ลงบันทึกไม่สำเร็จ: " + ex.Message);
                Log("ApplyPaid " + txn.TxnRef + " ล้มเหลว: " + ex);
                return "ลงบันทึกไม่สำเร็จ: " + ex.Message + " (ระบบจะลองใหม่อัตโนมัติ)";
            }
        }

        /// <summary>
        /// ค่าที่พัก — ใช้เส้นทางเดิมทั้งหมด (PaymentService.ProcessAdditionalPayment)
        /// จึงได้ Payment_History / ใบเสร็จ / การส่งเข้าระบบบัญชี เหมือนที่พนักงานคีย์มือเป๊ะ ๆ
        /// </summary>
        private string ApplyToReservation(PaymentTransaction txn, out string receiptId)
        {
            receiptId = null;
            int reservationId;
            if (!int.TryParse(txn.SourceId, out reservationId))
                return "รหัสการจองไม่ถูกต้อง (" + (txn.SourceId ?? "-") + ")";

            // ⚠ ProcessAdditionalPayment ค้นใบจองด้วย ID + เบอร์โทรแบบตรงตัวเป๊ะ ๆ
            // ถ้าเบอร์ในรายการจ่ายว่างหรือคนละรูปแบบ (รายการที่พนักงานสร้างจากหน้าจุดรับเงิน
            // ไม่มีเบอร์เลย) จะหาไม่เจอ → โยน exception → ปล่อยสิทธิ์ให้ลองใหม่วนไปเรื่อย ๆ
            // ⇒ เงินถูกตัดที่เกตเวย์แล้วแต่ไม่เคยเข้า Payment_History
            // ใช้เบอร์ที่อยู่บนใบจองจริงเสมอ (ตัวรายการจ่ายเชื่อถือไม่ได้)
            string phone = txn.CustomerPhone;
            try
            {
                DataTable rdt = new code().DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 Customer_MobilePhone FROM Reservation WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", reservationId } });
                if (rdt == null || rdt.Rows.Count == 0)
                    return "ไม่พบการจอง #" + reservationId;
                phone = Convert.ToString(rdt.Rows[0]["Customer_MobilePhone"]);
            }
            catch { /* อ่านไม่ได้ก็ใช้เบอร์เดิมของรายการจ่ายไปก่อน */ }

            // ⚠ ต้องเป็นชื่อที่ตรงกับแถวใน Account_Paid_How เป๊ะ ๆ — AccountingSync ค้นด้วย
            // ข้อความนี้ (LookupPaidHowAccountId) เพื่อบังคับ Dr เข้าบัญชีพักเงินเกตเวย์ใน NextAcc
            // ถ้าส่งชื่อสวย ๆ ("บัตรเครดิต / เดบิต") จะหาไม่เจอ แล้วบัญชีจะเดาเป็นเงินสด
            string methodText = PaymentGatewayConfig.Get("Payment_PaidHow_Name", "Omise (จ่ายออนไลน์)");
            string notes = "ชำระออนไลน์ผ่าน " + (txn.Provider ?? "-")
                         + " · อ้างอิง " + txn.TxnRef
                         + (string.IsNullOrEmpty(txn.ProviderTxnId) ? "" : " · เลขที่เกตเวย์ " + txn.ProviderTxnId)
                         + (string.IsNullOrEmpty(txn.CardLast4) ? "" : " · บัตร ****" + txn.CardLast4);

            var svc = new PaymentService(_conn);
            PaymentResult r = svc.ProcessAdditionalPayment(
                reservationId,
                txn.Amount,                       // ลงบัญชีเฉพาะยอดค่าห้องจริง ไม่รวมค่าธรรมเนียมบัตร
                methodText,
                null,                             // ไม่มีสลิป — เกตเวย์ยืนยันให้แล้ว
                txn.CreatedBy,
                phone,
                notes);

            if (r == null) return "เรียกระบบรับชำระเงินเดิมไม่สำเร็จ";
            if (!r.Success) throw new Exception(r.Message ?? "บันทึกการชำระเงินไม่สำเร็จ");

            // ใบจองที่ลูกค้าจองเองแล้วรอจ่าย → ได้เงินแล้วเลื่อนเป็น "มัดจำแล้ว"
            // (ไม่งั้นตัวกวาดจะยกเลิกใบที่จ่ายเงินมาแล้วทิ้ง)
            bool promoted = BookingPayment.PromoteIfPending(_conn, reservationId);

            receiptId = r.ReceiptId == null ? null : r.ReceiptId.ToString();
            return "บันทึกเข้าการจอง #" + reservationId + " แล้ว"
                 + (promoted ? " · ยืนยันการจองเรียบร้อย (รอชำระเงิน → มัดจำแล้ว)" : "")
                 + (string.IsNullOrEmpty(receiptId) ? "" : " (ใบเสร็จ " + receiptId + ")");
        }

        /// <summary>กิจกรรม — ใช้เมธอดเดิมของ ActivityService</summary>
        /// <summary>
        /// รูมเซอร์วิส — บันทึกว่าออเดอร์จ่ายออนไลน์แล้ว
        /// ⚠ ห้ามทับออเดอร์ที่ CHARGED (คิดเข้าห้องไปแล้ว) ไม่งั้นรายได้ซ้ำตอนเช็คเอาท์
        /// ใบสรุปรายได้รายวัน (RevenuePostingService) จะจัดกลุ่มวิธี ONLINE เข้าแหล่งเงินเกตเวย์เอง
        /// </summary>
        /// <summary>
        /// ใบเบิกของใช้ — บันทึกว่าจ่ายออนไลน์แล้ว
        /// ⚠ ห้ามทับใบที่ CHARGE_TO_ROOM (คิดเข้าห้องไปแล้ว) ไม่งั้นเก็บซ้ำตอนเช็คเอาท์
        /// </summary>
        private string ApplyToAmenityRequest(PaymentTransaction txn)
        {
            long reqId;
            if (!long.TryParse(txn.SourceId, out reqId))
                return "รหัสใบเบิกไม่ถูกต้อง (" + (txn.SourceId ?? "-") + ")";

            using (var con = new System.Data.SqlClient.SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                    UPDATE Guest_Amenity_Request
                       SET Payment_Method = 'PAID'
                     WHERE ID = @id AND ISNULL(Payment_Method,'') NOT IN ('PAID','CHARGE_TO_ROOM')", con))
                {
                    cmd.Parameters.AddWithValue("@id", reqId);
                    if (cmd.ExecuteNonQuery() > 0)
                        return "บันทึกว่าใบเบิกของใช้ #" + reqId + " ชำระเงินแล้ว";
                }
                using (var chk = new System.Data.SqlClient.SqlCommand(
                    "SELECT ISNULL(Payment_Method,'') FROM Guest_Amenity_Request WHERE ID = @id", con))
                {
                    chk.Parameters.AddWithValue("@id", reqId);
                    string pm = Convert.ToString(chk.ExecuteScalar());
                    if (pm == "PAID") return "ใบเบิก #" + reqId + " ถูกบันทึกว่าจ่ายแล้วก่อนหน้านี้";
                    if (pm == "CHARGE_TO_ROOM")
                        throw new Exception("ใบเบิก #" + reqId + " ถูกคิดเข้าห้องพักไปแล้ว — "
                            + "รับเงินซ้ำไม่ได้ ต้องคืนเงินลูกค้าหรือปรับบิลห้องก่อน");
                    return "ไม่พบใบเบิก #" + reqId;
                }
            }
        }

        private string ApplyToRoomServiceOrder(PaymentTransaction txn)
        {
            long orderId;
            if (!long.TryParse(txn.SourceId, out orderId))
                return "รหัสออเดอร์รูมเซอร์วิสไม่ถูกต้อง (" + (txn.SourceId ?? "-") + ")";

            using (var con = new System.Data.SqlClient.SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                    UPDATE Guest_Room_Service_Orders
                       SET Payment_Status = 'PAID', Payment_Method = 'ONLINE'
                     WHERE ID = @id AND ISNULL(Payment_Status,'') NOT IN ('PAID','CHARGED')", con))
                {
                    cmd.Parameters.AddWithValue("@id", orderId);
                    int n = cmd.ExecuteNonQuery();
                    if (n > 0) return "บันทึกว่าออเดอร์รูมเซอร์วิส #" + orderId + " ชำระเงินแล้ว";
                }
                using (var chk = new System.Data.SqlClient.SqlCommand(
                    "SELECT ISNULL(Payment_Status,'') FROM Guest_Room_Service_Orders WHERE ID = @id", con))
                {
                    chk.Parameters.AddWithValue("@id", orderId);
                    string st = Convert.ToString(chk.ExecuteScalar());
                    if (st == "PAID") return "ออเดอร์ #" + orderId + " ถูกบันทึกว่าจ่ายแล้วก่อนหน้านี้";
                    if (st == "CHARGED")
                        throw new Exception("ออเดอร์ #" + orderId + " ถูกคิดเข้าห้องพักไปแล้ว — "
                            + "รับเงินซ้ำไม่ได้ ต้องคืนเงินลูกค้าหรือปรับบิลห้องก่อน");
                    return "ไม่พบออเดอร์ #" + orderId;
                }
            }
        }

        private string ApplyToActivityBooking(PaymentTransaction txn)
        {
            long bookingId;
            if (!long.TryParse(txn.SourceId, out bookingId))
                return "รหัสการจองกิจกรรมไม่ถูกต้อง (" + (txn.SourceId ?? "-") + ")";

            var svc = new ActivityService(_conn);
            svc.MarkPaid(bookingId, null);
            return "บันทึกว่าจองกิจกรรม #" + bookingId + " ชำระเงินแล้ว";
        }

        // ── แจ้งเตือน ────────────────────────────────────────────────────────

        private void NotifyPaid(PaymentTransaction txn)
        {
            if (txn == null || !PaymentGatewayConfig.NotifyStaff) return;
            try
            {
                var notify = new NotificationService(_conn);
                notify.NotifyAllStaff(
                    "รับชำระเงินออนไลน์ " + txn.TotalPayable.ToString("N2") + " บาท",
                    PaymentSource.Thai(txn.SourceType) + " " + (txn.SourceId ?? "")
                    + " · " + PaymentGatewayConfig.MethodName(txn.Method)
                    + " · อ้างอิง " + txn.TxnRef
                    + (string.IsNullOrEmpty(txn.CustomerName) ? "" : " · " + txn.CustomerName),
                    "PAYMENT", "NORMAL");
            }
            catch (Exception ex) { Log("แจ้งเตือนในระบบไม่สำเร็จ: " + ex.Message); }

            // ช่องทางภายนอก (Telegram/LINE) — ปิดไว้เป็นค่าเริ่มต้น เปิดได้ที่หน้าการแจ้งเตือน
            try
            {
                global::Notify.Send(global::Notify.Ev.PaymentOnline,
                    "💳 <b>รับชำระเงินออนไลน์</b> " + txn.TotalPayable.ToString("N2") + " บาท\n"
                    + global::Notify.E(PaymentSource.Thai(txn.SourceType) + " " + (txn.SourceId ?? ""))
                    + "\n" + global::Notify.E(PaymentGatewayConfig.MethodName(txn.Method))
                    + "\nอ้างอิง " + global::Notify.E(txn.TxnRef)
                    + (string.IsNullOrEmpty(txn.CustomerName) ? "" : "\n👤 " + global::Notify.E(txn.CustomerName)));
            }
            catch { }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static bool IpAllowed(string csv, string ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;
            foreach (string part in csv.Split(','))
            {
                string p = part.Trim();
                if (p.Length == 0) continue;
                if (string.Equals(p, ip, StringComparison.OrdinalIgnoreCase)) return true;
                // รองรับ prefix แบบง่าย เช่น 203.0.113.
                if (p.EndsWith(".") && ip.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string DumpHeaders(NameValueCollection headers)
        {
            if (headers == null) return null;
            var sb = new StringBuilder();
            foreach (string k in headers.AllKeys)
            {
                if (k == null) continue;
                // ไม่เก็บค่าที่เป็นความลับลงฐานข้อมูล
                if (k.IndexOf("authorization", StringComparison.OrdinalIgnoreCase) >= 0
                    || k.IndexOf("cookie", StringComparison.OrdinalIgnoreCase) >= 0)
                { sb.AppendLine(k + ": (ซ่อน)"); continue; }
                sb.AppendLine(k + ": " + headers[k]);
            }
            return sb.ToString();
        }

        private void Log(string detail)
        {
            try { _code.Logs(_conn, "OnlinePayment", detail, "System"); }
            catch { }
        }
    }
}
