using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
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

        /// <summary>เกตเวย์ที่ใช้อยู่ — ตอนนี้มี Payso เจ้าเดียว แต่เพิ่มได้โดยไม่แตะหน้าจอ</summary>
        public IPaymentGateway Gateway(string provider = null)
        {
            // เผื่ออนาคตมีหลายเจ้า: เลือกตาม provider ที่ส่งมา
            return new PaysoGateway();
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
            var allowed = PaymentGatewayConfig.AvailableMethods(req.Amount);
            if (!allowed.Contains(method))
            {
                fail.Message = "วิธีชำระเงินนี้ใช้ไม่ได้กับยอด " + req.Amount.ToString("N2") + " บาท";
                return fail;
            }
            req.Method = method;

            // กันเก็บเงินซ้ำ: ถ้ารายการต้นทางนี้จ่ายสำเร็จไปแล้ว ไม่สร้างใหม่
            var paid = _store.GetPaidForSource(req.SourceType, req.SourceId);
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
                    case PaymentSource.Activity:
                        note = ApplyToActivityBooking(txn);
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

            string methodText = PaymentGatewayConfig.MethodName(txn.Method);
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
                txn.CustomerPhone,
                notes);

            if (r == null) return "เรียกระบบรับชำระเงินเดิมไม่สำเร็จ";
            if (!r.Success) throw new Exception(r.Message ?? "บันทึกการชำระเงินไม่สำเร็จ");

            receiptId = r.ReceiptId == null ? null : r.ReceiptId.ToString();
            return "บันทึกเข้าการจอง #" + reservationId + " แล้ว"
                 + (string.IsNullOrEmpty(receiptId) ? "" : " (ใบเสร็จ " + receiptId + ")");
        }

        /// <summary>กิจกรรม — ใช้เมธอดเดิมของ ActivityService</summary>
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
