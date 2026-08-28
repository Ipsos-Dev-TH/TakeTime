using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace Take_Time_BangPhra.Payments
{
    /// <summary>
    /// วงเงินประกันความเสียหาย (security hold) บนบัตรลูกค้า — แทนวิธีเดิมที่ให้โอนเข้า
    /// บัญชีส่วนตัวแล้วคืนเป็นเงินสดที่เบิกมารอ
    ///
    /// หลักการ:
    ///   • "กันวงเงิน" ไม่ใช่เงินเข้า — ห้ามแตะ Reservation.Deposit / Payment_History /
    ///     Account_Receipt เด็ดขาด (คอลัมน์เหล่านั้นคือเงินค่าห้องจริง แตะแล้วบัญชีเพี้ยนทั้งระบบ)
    ///   • เงินเกิดขึ้นครั้งเดียวคือตอน "ตัดค่าเสียหาย" — บันทึกเป็น Payment_Transaction
    ///     แล้วให้พนักงานออกใบเสร็จค่าเสียหายตามปกติ เลือกแหล่งเงิน "Omise" ที่ผูกกับ
    ///     บัญชีพักเงินใน NextAcc ⇒ เดินบัญชีผ่านเส้นทางใบเสร็จเดิมที่ผ่านการใช้งานจริงแล้ว
    ///   • Omise กันวงเงินได้เฉพาะบัตร และ **หมดอายุเอง 7 วัน** — เกินนั้นวงเงินคืนลูกค้า
    ///     อัตโนมัติ ระบบจะเตือนก่อนหมดอายุและปิดสถานะให้เอง
    ///   • ฟีเจอร์ปิดอยู่ = คลาสนี้ไม่ถูกเรียกจากหน้าไหนเลย ระบบเดิมทำงานเหมือนเดิม
    /// </summary>
    public class SecurityHoldService
    {
        private readonly string _conn;
        private readonly code _code = new code();

        public SecurityHoldService()
            : this(ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString) { }

        public SecurityHoldService(string connectionString) { _conn = connectionString; }

        /// <summary>
        /// ระบบเงินประกันเปิดใช้ไหม — ครอบทั้งเงินสดและบัตร
        /// (เงินสดไม่ต้องพึ่งเกตเวย์ ขอแค่ตารางพร้อม + สวิตช์เปิด)
        /// </summary>
        public bool IsAvailable
        {
            get
            {
                try
                {
                    if (!PaymentGatewayConfig.GetBool("Payment_SecurityHold_Enabled", false)) return false;
                    return TableReady();
                }
                catch { return false; }
            }
        }

        /// <summary>กันวงเงินบนบัตรได้ไหม (ต้องมีเกตเวย์ที่รองรับ + ระบบชำระเงินเปิด)</summary>
        public bool IsCardHoldAvailable
        {
            get
            {
                try
                {
                    if (!IsAvailable) return false;
                    if (!PaymentGatewayConfig.IsEnabled) return false;
                    return new OnlinePaymentService(_conn).Gateway() is IDepositGateway;
                }
                catch { return false; }
            }
        }

        /// <summary>
        /// วงเงินประกันแนะนำของการจองนี้ — สูงสุดของค่าที่ตั้งรายห้อง (Accommodation.
        /// Security_Deposit_Amount) ของห้องในใบจอง; ไม่ได้ตั้งเลยใช้ค่ากลาง
        /// </summary>
        public decimal SuggestedAmount(int reservationId)
        {
            decimal fallback = PaymentGatewayConfig.GetDecimal("Payment_SecurityHold_Default", 1000m);
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, @"
                    SELECT MAX(a.Security_Deposit_Amount) AS Amt
                      FROM Reservation_Accommodation ra
                      JOIN Accommodation a ON a.ID = ra.Accommodation_ID
                     WHERE ra.Reservation_ID = @r AND a.Security_Deposit_Amount IS NOT NULL",
                    new Dictionary<string, object> { { "@r", reservationId } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0]["Amt"] != DBNull.Value)
                {
                    decimal v = Convert.ToDecimal(dt.Rows[0]["Amt"]);
                    if (v > 0) return v;
                }
            }
            catch { }
            return fallback;
        }

        public bool TableReady()
        {
            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT CASE WHEN OBJECT_ID('dbo.Payment_Security_Holds','U') IS NULL THEN 0 ELSE 1 END", con))
                        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                }
            }
            catch { return false; }
        }

        private IDepositGateway Gateway()
        {
            return new OnlinePaymentService(_conn).Gateway() as IDepositGateway;
        }

        // ── ขั้น 1: สร้างคำขอ + ลิงก์ให้ลูกค้ากรอกบัตรเอง ────────────────────

        /// <summary>
        /// สร้างรายการรอกันวงเงิน แล้วคืนลิงก์หน้า Payment/Card (โหมด HOLD) ให้ส่ง/สแกน
        /// ลูกค้ากรอกบัตรเอง — ข้อมูลบัตรไปที่ Omise ตรง ไม่ผ่านเราเลย
        /// ถ้ามีรายการเปิดค้างของการจองเดิมอยู่แล้ว ใช้ลิงก์เดิม (กันกดสร้างซ้ำ)
        /// </summary>
        public string CreateHoldRequest(int reservationId, decimal amount, int? adminId, out string error)
        {
            error = null;
            if (!IsAvailable) { error = "ระบบวงเงินประกันยังไม่เปิดใช้งาน"; return null; }
            if (!IsCardHoldAvailable) { error = "เกตเวย์ยังไม่พร้อมกันวงเงินบัตร — รับเป็นเงินสดแทนได้"; return null; }
            if (amount <= 0) { error = "จำนวนเงินต้องมากกว่า 0"; return null; }

            var open = GetOpenHold(reservationId);
            if (open != null)
            {
                if (open.Status == HoldStatus.Held)
                { error = "การจองนี้มีวงเงินประกันกันอยู่แล้ว (" + open.HoldRef + ")"; return null; }
                return CardUrl(open.HoldRef);   // PENDING_CARD เดิม — ใช้ลิงก์เดิมซ้ำ
            }

            string holdRef = "HOLD-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-"
                           + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();

            _code.DatabaseInsertSafe(_conn, @"
                INSERT INTO Payment_Security_Holds
                    (Hold_Ref, Reservation_ID, Provider, Amount, [Status], Created_Date, Created_By)
                VALUES (@r, @res, @prov, @amt, @st, GETDATE(), @by)",
                new Dictionary<string, object>
                {
                    { "@r", holdRef },
                    { "@res", reservationId },
                    { "@prov", PaymentGatewayConfig.ActiveProvider },
                    { "@amt", amount },
                    { "@st", HoldStatus.PendingCard },
                    { "@by", (object)adminId ?? DBNull.Value }
                });

            return CardUrl(holdRef);
        }

        /// <summary>
        /// รับเงินประกันเป็น "เงินสด" — บันทึกเข้าระบบทันที (สถานะ HELD, ไม่มีวันหมดอายุ)
        /// เดิมเงินก้อนนี้อยู่นอกระบบทั้งขาเข้า-ขาออก ตอนนี้มีร่องรอยครบ:
        /// รับเมื่อไหร่ เท่าไหร่ คืน/หักเมื่อไหร่ โดยใคร
        /// </summary>
        public string CreateCashHold(int reservationId, decimal amount, int? adminId, out string error)
        {
            error = null;
            if (!IsAvailable) { error = "ระบบวงเงินประกันยังไม่เปิดใช้งาน"; return null; }
            if (amount <= 0) { error = "จำนวนเงินต้องมากกว่า 0"; return null; }

            var open = GetOpenHold(reservationId);
            if (open != null)
            {
                error = "การจองนี้มีเงินประกันเปิดอยู่แล้ว (" + open.HoldRef + " · "
                      + HoldStatus.Thai(open.Status) + ")";
                return null;
            }

            string holdRef = "HOLD-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-"
                           + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();

            _code.DatabaseInsertSafe(_conn, @"
                INSERT INTO Payment_Security_Holds
                    (Hold_Ref, Reservation_ID, Provider, Amount, [Status], Held_At, Created_Date, Created_By)
                VALUES (@r, @res, 'CASH', @amt, @st, GETDATE(), GETDATE(), @by)",
                new Dictionary<string, object>
                {
                    { "@r", holdRef },
                    { "@res", reservationId },
                    { "@amt", amount },
                    { "@st", HoldStatus.Held },
                    { "@by", (object)adminId ?? DBNull.Value }
                });

            Notify.Send(Notify.Ev.PaymentHold,
                "🛡 <b>รับเงินประกันเป็นเงินสด</b> " + amount.ToString("N2") + " บาท\n"
                + "การจอง #" + reservationId + " · " + holdRef
                + "\nเช็คเอาท์: คืนเงินสดก้อนนี้ หรือหักค่าเสียหายแล้วคืนส่วนที่เหลือ");
            return holdRef;
        }

        public static string CardUrl(string holdRef)
        {
            return PaymentUrls.SiteBase() + "/Payment/Card?mode=HOLD&hold="
                 + Uri.EscapeDataString(holdRef ?? "");
        }

        // ── ขั้น 2: หน้า Card เรียกเมื่อได้ token บัตร ───────────────────────

        /// <summary>กันวงเงินจริงด้วย token ที่ลูกค้าเพิ่งกรอก — คืนข้อความผลลัพธ์ไทย</summary>
        public string PlaceHold(string holdRef, string cardToken, out bool ok, out string authorizeUri)
        {
            ok = false; authorizeUri = null;
            var hold = GetByRef(holdRef);
            if (hold == null) return "ไม่พบรายการวงเงินประกันนี้";
            if (hold.Status == HoldStatus.Held) { ok = true; return "กันวงเงินไว้เรียบร้อยแล้ว"; }
            if (hold.Status != HoldStatus.PendingCard) return "รายการนี้ปิดไปแล้ว (" + HoldStatus.Thai(hold.Status) + ")";

            IDepositGateway gw = Gateway();
            if (gw == null) return "เกตเวย์ที่ใช้อยู่ไม่รองรับการกันวงเงิน";

            HoldResult r = gw.CreateHold(cardToken, hold.Amount, holdRef,
                "วงเงินประกันความเสียหาย การจอง #" + hold.ReservationId);

            SaveGatewayResult(hold.ID, r);

            if (r.Status == HoldStatus.Held)
            {
                ok = true;
                Notify.Send(Notify.Ev.PaymentHold,
                    "🛡 <b>กันวงเงินประกันสำเร็จ</b> " + hold.Amount.ToString("N2") + " บาท\n"
                    + "การจอง #" + hold.ReservationId
                    + (string.IsNullOrEmpty(r.CardLast4) ? "" : " · บัตร ****" + r.CardLast4)
                    + (r.ExpiresAt.HasValue ? "\n⏳ วงเงินหมดอายุ " + r.ExpiresAt.Value.ToString("dd/MM/yyyy HH:mm") : ""));
                return "กันวงเงิน " + hold.Amount.ToString("N2") + " บาท เรียบร้อย (ยังไม่มีการตัดเงิน)";
            }
            if (r.Status == HoldStatus.PendingCard && !string.IsNullOrEmpty(r.Message)
                && r.Message.StartsWith("http"))
            {
                authorizeUri = r.Message;   // 3-D Secure
                ok = true;
                return "กรุณายืนยันตัวตนกับธนาคารต่อ";
            }
            return "กันวงเงินไม่สำเร็จ: " + (r.Message ?? "ไม่ทราบสาเหตุ");
        }

        // ── ขั้น 3: เช็คเอาท์ — ตัดค่าเสียหาย หรือคืนวงเงิน ──────────────────

        /// <summary>
        /// ตัดค่าเสียหายจากวงเงิน (amount &lt; ยอดกัน = ส่วนที่เหลือคืนลูกค้าอัตโนมัติ)
        /// เงินที่ตัดถูกบันทึกเป็น Payment_Transaction (source DAMAGE) —
        /// จากนั้นพนักงานออกใบเสร็จค่าเสียหายเลือกแหล่งเงิน "Omise" ตามปกติ
        /// </summary>
        public string CaptureDamage(long holdId, decimal amount, string reason, int? adminId)
        {
            var hold = GetById(holdId);
            if (hold == null) return "ไม่พบรายการวงเงินประกัน";
            if (hold.Status != HoldStatus.Held) return "สถานะปัจจุบัน: " + HoldStatus.Thai(hold.Status) + " — ตัดเงินไม่ได้";
            if (amount <= 0) return "จำนวนเงินต้องมากกว่า 0";
            if (amount > hold.Amount) return "ตัดได้ไม่เกินยอดที่กันไว้ " + hold.Amount.ToString("N2") + " บาท";

            // จองสิทธิ์กันกดซ้ำ — เปลี่ยนสถานะก่อนยิง ใครมาทีหลังเจอสถานะไม่ใช่ HELD
            if (!TryTransition(hold.ID, HoldStatus.Held, "CAPTURING"))
                return "รายการนี้กำลังถูกดำเนินการอยู่ กรุณารอสักครู่แล้วรีเฟรช";

            bool isCash = string.Equals(hold.Provider, "CASH", StringComparison.OrdinalIgnoreCase);
            HoldResult r;
            if (isCash)
            {
                // เงินสดอยู่ในมือเราแล้ว — ไม่มีอะไรต้องยิง แค่บันทึกการตัดสินใจ
                r = new HoldResult { Success = true, Status = HoldStatus.Captured };
            }
            else
            {
                IDepositGateway gw = Gateway();
                r = gw == null
                    ? new HoldResult { Success = false, Message = "เกตเวย์ไม่รองรับ" }
                    : gw.CaptureHold(hold.ProviderChargeId, amount);
            }

            if (!r.Success)
            {
                TryTransition(hold.ID, "CAPTURING", HoldStatus.Held);   // คืนสถานะให้ลองใหม่ได้
                return "ตัดเงินไม่สำเร็จ: " + (r.Message ?? "-");
            }

            _code.DatabaseInsertSafe(_conn, @"
                UPDATE Payment_Security_Holds
                   SET [Status] = @st, Captured_Amount = @amt, Capture_Reason = @rs,
                       Captured_At = GETDATE(), Captured_By = @by, Raw_Response = @raw,
                       Updated_Date = GETDATE()
                 WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@id", hold.ID }, { "@st", HoldStatus.Captured }, { "@amt", amount },
                    { "@rs", (object)Trunc(reason, 400) ?? DBNull.Value },
                    { "@by", (object)adminId ?? DBNull.Value },
                    { "@raw", (object)r.RawResponse ?? DBNull.Value }
                });

            // เงินเข้าจริงแล้ว → ลงสมุดรายการชำระเงินกลาง (ตรวจย้อน/ออกใบเสร็จต่อได้)
            try
            {
                var store = new PaymentTransactionStore(_conn);
                var req = new PaymentChargeRequest
                {
                    TxnRef = hold.HoldRef + "-CAP",
                    Method = isCash ? PaymentGatewayConfig.MethodManualQr : PaymentGatewayConfig.MethodCard,
                    SourceType = "DAMAGE",
                    SourceId = hold.ReservationId.ToString(),
                    Amount = amount,
                    Description = "ค่าเสียหายจากวงเงินประกัน การจอง #" + hold.ReservationId
                                + (string.IsNullOrEmpty(reason) ? "" : " — " + reason),
                    CreatedByAdminId = adminId
                };
                var txn = store.Create(req, hold.Provider, 0m);
                store.MarkPaid(txn.ID, hold.ProviderChargeId, null, hold.CardBrand, hold.CardLast4);
                store.SetApplied(txn.ID,
                    isCash
                        ? "หักจากเงินประกันเงินสด " + hold.HoldRef + " — ออกใบเสร็จค่าเสียหาย (แหล่งเงิน: เงินสด)"
                        : "ตัดจากวงเงินประกัน " + hold.HoldRef + " — ออกใบเสร็จค่าเสียหายโดยเลือกแหล่งเงินของเกตเวย์",
                    null);
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "SecurityHold", "บันทึก txn หลัง capture ไม่สำเร็จ: " + ex.Message, "System");
            }

            decimal remainder = hold.Amount - amount;
            Notify.Send(Notify.Ev.PaymentHold,
                "💥 <b>หักค่าเสียหายจากเงินประกัน" + (isCash ? " (เงินสด)" : "") + "</b> "
                + amount.ToString("N2") + " / " + hold.Amount.ToString("N2") + " บาท\nการจอง #" + hold.ReservationId
                + (string.IsNullOrEmpty(reason) ? "" : "\n📝 " + Notify.E(reason))
                + (isCash
                    ? (remainder > 0 ? "\n💵 ต้องคืนเงินสดลูกค้า " + remainder.ToString("N2") + " บาท" : "")
                    : "\nส่วนที่เหลือคืนวงเงินให้ลูกค้าอัตโนมัติ")
                + " · อย่าลืมออกใบเสร็จค่าเสียหาย");

            if (isCash)
                return "หักค่าเสียหาย " + amount.ToString("N2") + " บาทแล้ว"
                     + (remainder > 0 ? " — คืนเงินสดลูกค้า " + remainder.ToString("N2") + " บาท" : "")
                     + " และออกใบเสร็จค่าเสียหาย (แหล่งเงิน: เงินสด)";

            return "ตัดค่าเสียหาย " + amount.ToString("N2") + " บาทแล้ว"
                 + (remainder > 0
                    ? " ส่วนที่เหลือ " + remainder.ToString("N2") + " บาท คืนวงเงินอัตโนมัติ"
                    : "")
                 + " — ไปออกใบเสร็จค่าเสียหาย (แหล่งเงิน: เกตเวย์) ให้เรียบร้อย";
        }

        /// <summary>คืนวงเงินทั้งหมด (ไม่พบความเสียหาย)</summary>
        public string Release(long holdId, int? adminId)
        {
            var hold = GetById(holdId);
            if (hold == null) return "ไม่พบรายการวงเงินประกัน";

            if (hold.Status == HoldStatus.PendingCard)
            {
                // ยังไม่ได้กันจริง — ปิดรายการเฉย ๆ
                TryTransition(hold.ID, HoldStatus.PendingCard, HoldStatus.Released);
                return "ยกเลิกคำขอวงเงินประกันแล้ว (ลูกค้ายังไม่ได้กรอกบัตร)";
            }
            if (hold.Status != HoldStatus.Held)
                return "สถานะปัจจุบัน: " + HoldStatus.Thai(hold.Status);

            if (!TryTransition(hold.ID, HoldStatus.Held, "RELEASING"))
                return "รายการนี้กำลังถูกดำเนินการอยู่";

            bool isCash = string.Equals(hold.Provider, "CASH", StringComparison.OrdinalIgnoreCase);
            HoldResult r;
            if (isCash)
            {
                r = new HoldResult { Success = true, Status = HoldStatus.Released };
            }
            else
            {
                IDepositGateway gw = Gateway();
                r = gw == null
                    ? new HoldResult { Success = false, Message = "เกตเวย์ไม่รองรับ" }
                    : gw.ReleaseHold(hold.ProviderChargeId);
            }

            if (!r.Success)
            {
                TryTransition(hold.ID, "RELEASING", HoldStatus.Held);
                return "คืนวงเงินไม่สำเร็จ: " + (r.Message ?? "-");
            }

            _code.DatabaseInsertSafe(_conn, @"
                UPDATE Payment_Security_Holds
                   SET [Status] = @st, Released_At = GETDATE(), Released_By = @by,
                       Raw_Response = @raw, Updated_Date = GETDATE()
                 WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@id", hold.ID }, { "@st", HoldStatus.Released },
                    { "@by", (object)adminId ?? DBNull.Value },
                    { "@raw", (object)r.RawResponse ?? DBNull.Value }
                });

            Notify.Send(Notify.Ev.PaymentHold,
                (isCash ? "✅ <b>คืนเงินประกันเงินสดแล้ว</b> " : "✅ <b>คืนวงเงินประกันแล้ว</b> ")
                + hold.Amount.ToString("N2") + " บาท\n"
                + "การจอง #" + hold.ReservationId
                + (isCash ? " · คืนเงินสดให้ลูกค้าที่เคาน์เตอร์" : " · ไม่มีการตัดเงินใด ๆ"));
            return isCash
                ? "บันทึกคืนเงินประกันแล้ว — คืนเงินสด " + hold.Amount.ToString("N2") + " บาท ให้ลูกค้า"
                : "คืนวงเงิน " + hold.Amount.ToString("N2") + " บาท เรียบร้อย — เงินไม่เคยออกจากบัตรลูกค้า";
        }

        // ── งานเบื้องหลัง: เตือนก่อนหมดอายุ + ปิดรายการที่หมดอายุ ─────────────

        private static DateTime _lastSweep = DateTime.MinValue;
        private static readonly object _sweepLock = new object();

        /// <summary>
        /// เรียกจาก timer เดิมใน Global.asax — เงียบสนิทถ้าฟีเจอร์ปิด
        /// ⚠ วงเงิน Omise อยู่ได้ 7 วัน: เตือนล่วงหน้า (ค่าเริ่มต้น 1 วัน) ให้ตัดสินใจ
        /// ตัด/คืน/ขอกันใหม่ ก่อนที่วงเงินจะหลุดมือไปเอง
        /// </summary>
        public void SweepIfDue()
        {
            if (!TableReady()) return;
            lock (_sweepLock)
            {
                if ((DateTime.Now - _lastSweep).TotalMinutes < 30) return;
                _lastSweep = DateTime.Now;
            }

            try
            {
                // 1) ปิดรายการที่เลยวันหมดอายุ (Omise คืนวงเงินให้ลูกค้าไปแล้ว)
                var expired = _code.DatabaseQuerySafe(_conn, @"
                    SELECT ID, Reservation_ID, Amount FROM Payment_Security_Holds
                     WHERE [Status] = @held AND Expires_At IS NOT NULL AND Expires_At < GETDATE()",
                    new Dictionary<string, object> { { "@held", HoldStatus.Held } });

                if (expired != null)
                    foreach (DataRow r in expired.Rows)
                    {
                        int resId = Convert.ToInt32(r["Reservation_ID"]);
                        decimal amt = Convert.ToDecimal(r["Amount"]);
                        _code.DatabaseInsertSafe(_conn,
                            "UPDATE Payment_Security_Holds SET [Status]=@st, Updated_Date=GETDATE() WHERE ID=@id AND [Status]=@held",
                            new Dictionary<string, object>
                            { { "@st", HoldStatus.Expired }, { "@id", r["ID"] }, { "@held", HoldStatus.Held } });

                        // ตามที่ตกลง: เกิน 7 วัน → สร้างลิงก์ใหม่ให้เอง (เฉพาะการจองที่ยังพักอยู่)
                        // ลิงก์ใหม่แนบไปกับแจ้งเตือนเลย พนักงานแค่ส่งต่อให้ลูกค้า
                        string newLink = null;
                        if (IsCardHoldAvailable && ReservationStillActive(resId))
                        {
                            string linkErr;
                            newLink = CreateHoldRequest(resId, amt, null, out linkErr);
                        }

                        Notify.Send(Notify.Ev.PaymentHold,
                            "⌛ <b>วงเงินประกันหมดอายุแล้ว</b> " + amt.ToString("N2")
                            + " บาท\nการจอง #" + resId
                            + "\nวงเงินคืนลูกค้าอัตโนมัติโดยเกตเวย์"
                            + (newLink != null
                                ? "\n🔗 <b>ลิงก์กันวงเงินรอบใหม่ (ส่งให้ลูกค้าได้เลย):</b>\n" + newLink
                                : "\nการจองปิดแล้วหรือเกตเวย์ไม่พร้อม — ไม่สร้างลิงก์ใหม่"));
                    }

                // 2) เตือนก่อนหมดอายุ (ครั้งเดียวต่อรายการ)
                int warnHours = Math.Max(1, PaymentGatewayConfig.GetInt("Payment_SecurityHold_WarnHours", 24));
                var warn = _code.DatabaseQuerySafe(_conn, @"
                    SELECT ID, Reservation_ID, Amount, Expires_At FROM Payment_Security_Holds
                     WHERE [Status] = @held AND Expiry_Warned = 0
                       AND Expires_At IS NOT NULL
                       AND Expires_At < DATEADD(HOUR, @h, GETDATE())",
                    new Dictionary<string, object> { { "@held", HoldStatus.Held }, { "@h", warnHours } });

                if (warn != null)
                    foreach (DataRow r in warn.Rows)
                    {
                        _code.DatabaseInsertSafe(_conn,
                            "UPDATE Payment_Security_Holds SET Expiry_Warned = 1 WHERE ID = @id",
                            new Dictionary<string, object> { { "@id", r["ID"] } });
                        Notify.Send(Notify.Ev.PaymentHold,
                            "⏰ <b>วงเงินประกันใกล้หมดอายุ</b> " + Convert.ToDecimal(r["Amount"]).ToString("N2")
                            + " บาท\nการจอง #" + r["Reservation_ID"]
                            + "\nหมดอายุ " + Convert.ToDateTime(r["Expires_At"]).ToString("dd/MM/yyyy HH:mm")
                            + " — ตัดค่าเสียหายหรือคืนวงเงินก่อนถึงเวลานั้น");
                    }
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "SecurityHold", "sweep ล้มเหลว: " + ex.Message, "System");
            }
        }

        /// <summary>การจองยังไม่จบ (ยังไม่เช็คเอาท์/ยกเลิก) — ค่อยต่ออายุวงเงินให้</summary>
        private bool ReservationStillActive(int reservationId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 Status, CheckoutDate FROM Reservation WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", reservationId } });
                if (dt == null || dt.Rows.Count == 0) return false;
                string st = Convert.ToString(dt.Rows[0]["Status"]) ?? "";
                if (st.Contains("ยกเลิก") || st.Contains("เสร็จสิ้น")) return false;
                if (dt.Rows[0]["CheckoutDate"] != DBNull.Value
                    && Convert.ToDateTime(dt.Rows[0]["CheckoutDate"]).Date < DateTime.Today) return false;
                return true;
            }
            catch { return false; }
        }

        // ── อ่านข้อมูล ──────────────────────────────────────────────────────

        public HoldRow GetOpenHold(int reservationId)
        {
            var dt = _code.DatabaseQuerySafe(_conn, @"
                SELECT TOP 1 * FROM Payment_Security_Holds
                 WHERE Reservation_ID = @r AND [Status] IN (@p, @h)
                 ORDER BY ID DESC",
                new Dictionary<string, object>
                { { "@r", reservationId }, { "@p", HoldStatus.PendingCard }, { "@h", HoldStatus.Held } });
            return dt != null && dt.Rows.Count > 0 ? Map(dt.Rows[0]) : null;
        }

        public HoldRow GetByRef(string holdRef)
        {
            var dt = _code.DatabaseQuerySafe(_conn,
                "SELECT TOP 1 * FROM Payment_Security_Holds WHERE Hold_Ref = @r",
                new Dictionary<string, object> { { "@r", holdRef ?? "" } });
            return dt != null && dt.Rows.Count > 0 ? Map(dt.Rows[0]) : null;
        }

        public HoldRow GetById(long id)
        {
            var dt = _code.DatabaseQuerySafe(_conn,
                "SELECT TOP 1 * FROM Payment_Security_Holds WHERE ID = @id",
                new Dictionary<string, object> { { "@id", id } });
            return dt != null && dt.Rows.Count > 0 ? Map(dt.Rows[0]) : null;
        }

        public DataTable Recent(int top)
        {
            return _code.DatabaseQuerySafe(_conn, @"
                SELECT TOP (" + Math.Max(1, top) + @") ID, Hold_Ref, Reservation_ID, Provider, Amount,
                       Captured_Amount, [Status], Card_Last4, Expires_At, Capture_Reason, Created_Date
                  FROM Payment_Security_Holds ORDER BY ID DESC", null);
        }

        // ── ภายใน ───────────────────────────────────────────────────────────

        /// <summary>เปลี่ยนสถานะแบบ atomic — คืน true เฉพาะผู้ที่ชิงเปลี่ยนได้จริง (กันกดซ้ำ)</summary>
        private bool TryTransition(long id, string from, string to)
        {
            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE Payment_Security_Holds SET [Status] = @to, Updated_Date = GETDATE()
                         WHERE ID = @id AND [Status] = @from", con))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@from", from);
                        cmd.Parameters.AddWithValue("@to", to);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch { return false; }
        }

        private void SaveGatewayResult(long id, HoldResult r)
        {
            _code.DatabaseInsertSafe(_conn, @"
                UPDATE Payment_Security_Holds
                   SET [Status] = @st, Provider_Charge_ID = COALESCE(NULLIF(@cid,''), Provider_Charge_ID),
                       Card_Brand = COALESCE(NULLIF(@brand,''), Card_Brand),
                       Card_Last4 = COALESCE(NULLIF(@last4,''), Card_Last4),
                       Held_At = CASE WHEN @st = @heldSt THEN GETDATE() ELSE Held_At END,
                       Expires_At = COALESCE(@exp, Expires_At),
                       Raw_Response = @raw, Updated_Date = GETDATE()
                 WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@id", id },
                    { "@st", r.Status ?? HoldStatus.Failed },
                    { "@heldSt", HoldStatus.Held },
                    { "@cid", r.ProviderChargeId ?? "" },
                    { "@brand", r.CardBrand ?? "" },
                    { "@last4", r.CardLast4 ?? "" },
                    { "@exp", (object)r.ExpiresAt ?? DBNull.Value },
                    { "@raw", (object)r.RawResponse ?? DBNull.Value }
                });
        }

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return s.Length <= max ? s : s.Substring(0, max);
        }

        public class HoldRow
        {
            public long ID;
            public string HoldRef, Provider, ProviderChargeId, Status, CardBrand, CardLast4, CaptureReason;
            public int ReservationId;
            public decimal Amount, CapturedAmount;
            public DateTime? HeldAt, ExpiresAt;
        }

        private static HoldRow Map(DataRow r)
        {
            var h = new HoldRow();
            h.ID = Convert.ToInt64(r["ID"]);
            h.HoldRef = Convert.ToString(r["Hold_Ref"]);
            h.ReservationId = Convert.ToInt32(r["Reservation_ID"]);
            h.Provider = Convert.ToString(r["Provider"]);
            h.ProviderChargeId = r["Provider_Charge_ID"] == DBNull.Value ? null : Convert.ToString(r["Provider_Charge_ID"]);
            h.Status = Convert.ToString(r["Status"]);
            h.Amount = Convert.ToDecimal(r["Amount"]);
            h.CapturedAmount = r["Captured_Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Captured_Amount"]);
            h.CardBrand = r["Card_Brand"] == DBNull.Value ? null : Convert.ToString(r["Card_Brand"]);
            h.CardLast4 = r["Card_Last4"] == DBNull.Value ? null : Convert.ToString(r["Card_Last4"]);
            h.CaptureReason = r.Table.Columns.Contains("Capture_Reason") && r["Capture_Reason"] != DBNull.Value
                ? Convert.ToString(r["Capture_Reason"]) : null;
            h.HeldAt = r["Held_At"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["Held_At"]);
            h.ExpiresAt = r["Expires_At"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["Expires_At"]);
            return h;
        }
    }
}
