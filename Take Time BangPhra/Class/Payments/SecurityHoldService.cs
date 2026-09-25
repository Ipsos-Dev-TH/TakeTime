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
    ///
    /// โหมด (ค่าตั้ง Security_Hold_Mode):
    ///   • TRANSFER (ค่าเริ่มต้น) — PaySo (เกตเวย์หลัก) กันวงเงินบัตรไม่ได้ ⇒ รับเงินประกันโดยลูกค้า
    ///     โอนเข้าบัญชีโรงแรม บันทึกเป็นรายการนอกเกตเวย์ (Provider = TRANSFER) เช็คเอาท์ค่อยบันทึก
    ///     "โอนคืน" หรือ "หักค่าเสียหาย" พร้อมผู้ทำ/เวลา/เลขอ้างอิง — ไม่มีการเรียกเกตเวย์ใด ๆ
    ///   • CARD_HOLD — กันวงเงินบนบัตร (ต้องใช้เกตเวย์ที่ SupportsPreAuth = Omise)
    ///   • CASH — รับเงินสดเป็นหลัก
    ///   ทุกโหมด: เงินประกันไม่ใช่รายได้ ไม่ลง Account_Receipt/Payment_History ⇒ ไม่ส่ง NextAcc
    ///   (เงินที่ "หักค่าเสียหาย" เท่านั้นที่ออกใบเสร็จค่าเสียหายตามปกติ)
    /// </summary>
    public class SecurityHoldService
    {
        public const string ModeTransfer = "TRANSFER";
        public const string ModeCardHold = "CARD_HOLD";
        public const string ModeCash = "CASH";

        public const string ProviderCash = "CASH";
        public const string ProviderTransfer = "TRANSFER";

        /// <summary>โหมดรับเงินประกัน — ไม่ได้ตั้ง/ค่าแปลก = TRANSFER</summary>
        public static string Mode
        {
            get
            {
                string m = "";
                try { m = (PaymentGatewayConfig.Get("Security_Hold_Mode", ModeTransfer) ?? "").Trim().ToUpperInvariant(); }
                catch { }
                return m == ModeCardHold || m == ModeCash ? m : ModeTransfer;
            }
        }

        /// <summary>รายการเงินประกันนี้อยู่นอกเกตเวย์ไหม (เงินสด/โอน — ไม่มีอะไรต้องยิงไปเกตเวย์)</summary>
        public static bool IsOffGateway(string provider)
        {
            return string.Equals(provider, ProviderCash, StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, ProviderTransfer, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>บัญชีโรงแรมที่ให้ลูกค้าโอนเงินประกัน (จากแคตตาล็อกช่องทาง) — null = ยังไม่มีช่องทางโอน</summary>
        public static PaymentChannel TransferChannel()
        {
            try { return PaymentChannelCatalog.DepositTransferChannel(); } catch { return null; }
        }

        /// <summary>
        /// กล่องข้อมูลบัญชีรับโอนเงินประกัน (ชื่อ/ข้อความแนะนำ/QR) สำหรับหน้าพนักงานโชว์ให้ลูกค้า
        /// — encode ครบทุกค่า ใส่ Literal ได้ตรง ๆ
        /// </summary>
        public static string TransferInfoHtml()
        {
            PaymentChannel ch = TransferChannel();
            if (ch == null)
                return "<div style=\"color:#8D6E00;font-size:0.9em;\">⚠ ยังไม่มีช่องทางโอนในระบบ — "
                     + "ตั้งที่ ศูนย์ตั้งค่า → ช่องทางชำระเงิน (บันทึกรับโอนได้ตามปกติ)</div>";

            var sb = new System.Text.StringBuilder();
            sb.Append("<div style=\"display:flex;gap:14px;flex-wrap:wrap;align-items:flex-start;\">");
            string qr = (ch.QrImageUrl ?? "").Trim();
            if (qr.Length > 0)
            {
                if (qr.StartsWith("~/"))
                {
                    try { qr = System.Web.VirtualPathUtility.ToAbsolute(qr); } catch { }
                }
                sb.Append("<img src=\"").Append(System.Web.HttpUtility.HtmlAttributeEncode(qr))
                  .Append("\" alt=\"QR\" style=\"max-width:150px;border-radius:8px;background:#fff;\" />");
            }
            sb.Append("<div style=\"flex:1;min-width:200px;font-size:0.92em;line-height:1.6;\">")
              .Append("<b>โอนเงินประกันเข้า: ").Append(System.Web.HttpUtility.HtmlEncode(ch.Name ?? "")).Append("</b>");
            if (!string.IsNullOrWhiteSpace(ch.Instructions))
                sb.Append("<div>").Append(System.Web.HttpUtility.HtmlEncode(ch.Instructions.Trim())
                    .Replace("\r\n", "<br/>").Replace("\n", "<br/>")).Append("</div>");
            sb.Append("<div style=\"color:#7CB342;\">เงินประกันไม่ใช่ค่าห้อง — ไม่ออกใบเสร็จ เช็คเอาท์โอนคืน/หักค่าเสียหาย</div>");
            sb.Append("</div></div>");
            return sb.ToString();
        }

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
                    // โหมดโอน/เงินสด (ค่าเริ่มต้น) ไม่เสนอลิงก์กันวงเงินบัตรเด็ดขาด
                    if (Mode != ModeCardHold) return false;
                    if (!PaymentGatewayConfig.IsEnabled) return false;
                    IPaymentGateway gw = new OnlinePaymentService(_conn).Gateway();
                    return gw != null && gw.SupportsPreAuth && gw.IsReady && gw is IDepositGateway;
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
            if (!IsCardHoldAvailable)
            {
                error = Mode != ModeCardHold
                    ? "ที่พักรับเงินประกันโดยการโอน (โหมด " + Mode + ") — ไม่ใช้ลิงก์กันวงเงินบัตร"
                    : "เกตเวย์ที่ใช้อยู่กันวงเงินบัตรไม่ได้ — รับเป็นเงินโอน/เงินสดแทนได้";
                return null;
            }
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
            string holdRef = CreateOffGatewayHold(ProviderCash, reservationId, amount, null, null, adminId, out error);
            if (holdRef == null) return null;

            Notify.Send(Notify.Ev.PaymentHold,
                "🛡 <b>รับเงินประกันเป็นเงินสด</b> " + amount.ToString("N2") + " บาท\n"
                + "การจอง #" + reservationId + " · " + holdRef
                + "\nเช็คเอาท์: คืนเงินสดก้อนนี้ หรือหักค่าเสียหายแล้วคืนส่วนที่เหลือ");
            return holdRef;
        }

        /// <summary>
        /// รับเงินประกันโดย "โอนเข้าบัญชีโรงแรม" — บันทึกนอกเกตเวย์ทันที (HELD, ไม่มีวันหมดอายุ)
        /// transferRef = เลขอ้างอิง/เวลาโอน/ธนาคารต้นทางของลูกค้า · note = หมายเหตุ (เช่น ชื่อบัญชีผู้โอน)
        /// ไม่ลงใบเสร็จ/Payment_History/NextAcc — เงินประกันเป็นเงินที่ต้องคืน ไม่ใช่รายได้
        /// </summary>
        public string CreateTransferHold(int reservationId, decimal amount, string transferRef, string note,
            int? adminId, out string error)
        {
            PaymentChannel ch = TransferChannel();
            string channelText = ch == null ? null
                : ch.Code + " · " + (string.IsNullOrEmpty(ch.PaidHowName) ? ch.Name : ch.PaidHowName);
            string fullNote = string.IsNullOrWhiteSpace(note) ? "" : note.Trim();
            if (channelText != null)
                fullNote = fullNote + (fullNote.Length > 0 ? " | " : "") + "เข้าบัญชี: " + channelText;

            string holdRef = CreateOffGatewayHold(ProviderTransfer, reservationId, amount,
                transferRef, fullNote, adminId, out error);
            if (holdRef == null) return null;

            Notify.Send(Notify.Ev.PaymentHold,
                "🛡 <b>รับเงินประกันโดยการโอน</b> " + amount.ToString("N2") + " บาท\n"
                + "การจอง #" + reservationId + " · " + holdRef
                + (string.IsNullOrWhiteSpace(transferRef)
                    ? "\n⚠ ยังไม่ระบุเลขอ้างอิงการโอน"
                    : "\nอ้างอิง: " + Notify.E(transferRef.Trim()))
                + "\nเช็คเอาท์: บันทึกโอนคืน หรือหักค่าเสียหายแล้วโอนคืนส่วนที่เหลือ (นอกเกตเวย์)");
            return holdRef;
        }

        /// <summary>บันทึกเงินประกันนอกเกตเวย์ (เงินสด/โอน) — สถานะ HELD ทันที</summary>
        private string CreateOffGatewayHold(string provider, int reservationId, decimal amount,
            string transferRef, string transferNote, int? adminId, out string error)
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

            bool isTransfer = provider == ProviderTransfer;
            string tRef = Trunc(transferRef == null ? null : transferRef.Trim(), 100);
            string tNote = Trunc(transferNote == null ? null : transferNote.Trim(), 400);

            if (isTransfer && HasTransferColumns())
            {
                _code.DatabaseInsertSafe(_conn, @"
                    INSERT INTO Payment_Security_Holds
                        (Hold_Ref, Reservation_ID, Provider, Amount, [Status], Held_At, Created_Date, Created_By,
                         Transfer_Ref, Transfer_Note)
                    VALUES (@r, @res, @prov, @amt, @st, GETDATE(), GETDATE(), @by, @tref, @tnote)",
                    new Dictionary<string, object>
                    {
                        { "@r", holdRef }, { "@res", reservationId }, { "@prov", provider },
                        { "@amt", amount }, { "@st", HoldStatus.Held },
                        { "@by", (object)adminId ?? DBNull.Value },
                        { "@tref", (object)tRef ?? DBNull.Value },
                        { "@tnote", (object)tNote ?? DBNull.Value }
                    });
            }
            else
            {
                // ยังไม่รัน PHASE19_20 (ไม่มีคอลัมน์โอน) → เก็บอ้างอิงไว้ใน Raw_Response ให้ตรวจย้อนได้
                string raw = isTransfer ? "TRANSFER ref=" + (tRef ?? "-") + "; note=" + (tNote ?? "-") : null;
                _code.DatabaseInsertSafe(_conn, @"
                    INSERT INTO Payment_Security_Holds
                        (Hold_Ref, Reservation_ID, Provider, Amount, [Status], Held_At, Created_Date, Created_By, Raw_Response)
                    VALUES (@r, @res, @prov, @amt, @st, GETDATE(), GETDATE(), @by, @raw)",
                    new Dictionary<string, object>
                    {
                        { "@r", holdRef }, { "@res", reservationId }, { "@prov", provider },
                        { "@amt", amount }, { "@st", HoldStatus.Held },
                        { "@by", (object)adminId ?? DBNull.Value },
                        { "@raw", (object)raw ?? DBNull.Value }
                    });
            }
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

            // โหมดโอน/เงินสด หรือเกตเวย์หลัก (PaySo) กันวงเงินไม่ได้ → ลิงก์เก่าใช้ไม่ได้แล้ว
            if (!IsCardHoldAvailable)
                return "ที่พักรับเงินประกันโดยการโอนเข้าบัญชี (ไม่ใช้การกันวงเงินบัตรแล้ว) — กรุณาติดต่อเจ้าหน้าที่";

            IDepositGateway gw = Gateway();
            if (gw == null) return "เกตเวย์ที่ใช้อยู่ไม่รองรับการกันวงเงิน";

            // ⚠ กันการกันวงเงิน "ซ้ำสองใบ" บนบัตรลูกค้า
            // ถ้ารายการนี้เคยยิงไปแล้วและมี charge id ค้างอยู่ ให้ถามเกตเวย์ก่อนว่าตัวจริง
            // สถานะอะไร — เคยมีเคสที่ฝั่งเกตเวย์ authorize สำเร็จแต่ฝั่งเราบันทึกพลาด
            // ยิงใหม่ตรง ๆ = ลูกค้าโดนกันวงเงินสองก้อน
            if (!string.IsNullOrEmpty(hold.ProviderChargeId))
            {
                HoldResult prev = gw.GetHold(hold.ProviderChargeId);
                if (prev != null && prev.Status == HoldStatus.Held)
                {
                    SaveGatewayResult(hold.ID, prev);
                    ok = true;
                    return "รายการนี้กันวงเงินไว้เรียบร้อยแล้ว (ตรวจสอบกับผู้ให้บริการแล้ว) — ไม่มีการตัดเงิน";
                }
                if (prev != null && (prev.Status == HoldStatus.Captured || prev.Status == HoldStatus.Released))
                {
                    SaveGatewayResult(hold.ID, prev);
                    return "รายการนี้ดำเนินการไปแล้ว (" + HoldStatus.Thai(prev.Status) + ")";
                }
            }

            HoldResult r = gw.CreateHold(cardToken, hold.Amount, holdRef,
                "วงเงินประกันความเสียหาย การจอง #" + hold.ReservationId);

            // ⚠ บัตรไม่ผ่าน ≠ รายการนี้จบแล้ว — เดิมบันทึกเป็น FAILED ทันที ลิงก์ที่ส่งให้
            // ลูกค้าจึงใช้ไม่ได้อีกเลย ทั้งที่ควรลองบัตรใบอื่นบนลิงก์เดิมได้
            // ⇒ คงสถานะ PENDING_CARD ไว้ เก็บเหตุผลไว้แสดงแทน
            if (r.Status == HoldStatus.Failed)
            {
                SaveFailedAttempt(hold.ID, r);
                return "กันวงเงินไม่สำเร็จ: " + (r.Message ?? "ไม่ทราบสาเหตุ")
                     + " — ลองใหม่ด้วยบัตรใบอื่นได้ทันที";
            }

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
            return CaptureDamage(holdId, amount, reason, adminId, null, null);
        }

        /// <summary>
        /// หักค่าเสียหาย — refundRef/refundNote ใช้กับเงินประกันโอน: บันทึกการโอนคืนส่วนที่เหลือ
        /// (เลขอ้างอิง/บัญชีที่โอนคืน) · เงินสด/โอน ไม่มีการเรียกเกตเวย์ใด ๆ
        /// </summary>
        public string CaptureDamage(long holdId, decimal amount, string reason, int? adminId,
            string refundRef, string refundNote)
        {
            var hold = GetById(holdId);
            if (hold == null) return "ไม่พบรายการวงเงินประกัน";
            if (hold.Status != HoldStatus.Held) return "สถานะปัจจุบัน: " + HoldStatus.Thai(hold.Status) + " — ตัดเงินไม่ได้";
            if (amount <= 0) return "จำนวนเงินต้องมากกว่า 0";
            if (amount > hold.Amount) return "ตัดได้ไม่เกินยอดที่กันไว้ " + hold.Amount.ToString("N2") + " บาท";

            // จองสิทธิ์กันกดซ้ำ — เปลี่ยนสถานะก่อนยิง ใครมาทีหลังเจอสถานะไม่ใช่ HELD
            if (!TryTransition(hold.ID, HoldStatus.Held, "CAPTURING"))
                return "รายการนี้กำลังถูกดำเนินการอยู่ กรุณารอสักครู่แล้วรีเฟรช";

            bool isTransfer = string.Equals(hold.Provider, ProviderTransfer, StringComparison.OrdinalIgnoreCase);
            bool isCash = isTransfer || string.Equals(hold.Provider, ProviderCash, StringComparison.OrdinalIgnoreCase);
            HoldResult r;
            if (isCash)
            {
                // เงินสด/เงินโอนอยู่กับเราแล้ว — ไม่มีอะไรต้องยิง แค่บันทึกการตัดสินใจ
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
                       Captured_At = GETDATE(), Captured_By = @by, Raw_Response = COALESCE(@raw, Raw_Response),
                       Updated_Date = GETDATE()
                 WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@id", hold.ID }, { "@st", HoldStatus.Captured }, { "@amt", amount },
                    { "@rs", (object)Trunc(reason, 400) ?? DBNull.Value },
                    { "@by", (object)adminId ?? DBNull.Value },
                    { "@raw", (object)r.RawResponse ?? DBNull.Value }
                });

            decimal remainder = hold.Amount - amount;
            if (isTransfer) SaveRefundRecord(hold.ID, remainder, refundRef, refundNote);

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
                    isTransfer
                        ? "หักจากเงินประกันเงินโอน " + hold.HoldRef + " — ออกใบเสร็จค่าเสียหาย (แหล่งเงิน: " + TransferPaidHowText() + ")"
                        : isCash
                        ? "หักจากเงินประกันเงินสด " + hold.HoldRef + " — ออกใบเสร็จค่าเสียหาย (แหล่งเงิน: เงินสด)"
                        : "ตัดจากวงเงินประกัน " + hold.HoldRef + " — ออกใบเสร็จค่าเสียหายโดยเลือกแหล่งเงินของเกตเวย์",
                    null);
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "SecurityHold", "บันทึก txn หลัง capture ไม่สำเร็จ: " + ex.Message, "System");
            }

            Notify.Send(Notify.Ev.PaymentHold,
                "💥 <b>หักค่าเสียหายจากเงินประกัน" + (isTransfer ? " (เงินโอน)" : isCash ? " (เงินสด)" : "") + "</b> "
                + amount.ToString("N2") + " / " + hold.Amount.ToString("N2") + " บาท\nการจอง #" + hold.ReservationId
                + (string.IsNullOrEmpty(reason) ? "" : "\n📝 " + Notify.E(reason))
                + (isTransfer
                    ? (remainder > 0
                        ? "\n🏦 ต้องโอนคืนลูกค้า " + remainder.ToString("N2") + " บาท"
                          + (string.IsNullOrWhiteSpace(refundRef)
                              ? " (ยังไม่ระบุเลขอ้างอิงการโอนคืน)"
                              : " · อ้างอิง " + Notify.E(refundRef.Trim()))
                        : "")
                    : isCash
                    ? (remainder > 0 ? "\n💵 ต้องคืนเงินสดลูกค้า " + remainder.ToString("N2") + " บาท" : "")
                    : "\nส่วนที่เหลือคืนวงเงินให้ลูกค้าอัตโนมัติ")
                + " · อย่าลืมออกใบเสร็จค่าเสียหาย");

            if (isTransfer)
                return "หักค่าเสียหาย " + amount.ToString("N2") + " บาทแล้ว"
                     + (remainder > 0 ? " — โอนคืนลูกค้า " + remainder.ToString("N2") + " บาท (นอกเกตเวย์)" : "")
                     + " และออกใบเสร็จค่าเสียหาย (แหล่งเงิน: " + TransferPaidHowText() + ")";

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
            return Release(holdId, adminId, null, null);
        }

        /// <summary>
        /// คืนเงินประกันทั้งหมด — เงินโอน: บันทึกการ "โอนคืน" พร้อมเลขอ้างอิง/หมายเหตุ (ไม่มีการเรียกเกตเวย์)
        /// </summary>
        public string Release(long holdId, int? adminId, string refundRef, string refundNote)
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

            bool isTransfer = string.Equals(hold.Provider, ProviderTransfer, StringComparison.OrdinalIgnoreCase);
            bool isCash = isTransfer || string.Equals(hold.Provider, ProviderCash, StringComparison.OrdinalIgnoreCase);
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
                       Raw_Response = COALESCE(@raw, Raw_Response), Updated_Date = GETDATE()
                 WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@id", hold.ID }, { "@st", HoldStatus.Released },
                    { "@by", (object)adminId ?? DBNull.Value },
                    { "@raw", (object)r.RawResponse ?? DBNull.Value }
                });

            if (isTransfer)
            {
                SaveRefundRecord(hold.ID, hold.Amount, refundRef, refundNote);
                Notify.Send(Notify.Ev.PaymentHold,
                    "✅ <b>บันทึกโอนคืนเงินประกันแล้ว</b> " + hold.Amount.ToString("N2") + " บาท\n"
                    + "การจอง #" + hold.ReservationId
                    + (string.IsNullOrWhiteSpace(refundRef)
                        ? " · ⚠ ยังไม่ระบุเลขอ้างอิงการโอนคืน"
                        : " · อ้างอิง " + Notify.E(refundRef.Trim())));
                return "บันทึกคืนเงินประกันแล้ว — โอนคืนลูกค้า " + hold.Amount.ToString("N2") + " บาท (นอกเกตเวย์)"
                     + (string.IsNullOrWhiteSpace(refundRef) ? " · ยังไม่ได้ระบุเลขอ้างอิงการโอนคืน" : "");
            }

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
                     WHERE [Status] = @held AND Expires_At IS NOT NULL AND Expires_At < GETDATE()
                       AND Provider NOT IN ('CASH','TRANSFER')",
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
                       AND Expires_At < DATEADD(HOUR, @h, GETDATE())
                       AND Provider NOT IN ('CASH','TRANSFER')",
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

                // 3) เงินประกันนอกเกตเวย์ (โอน/เงินสด) — ไม่มีวันหมดอายุ ไม่ต้องยิงเกตเวย์
                //    แค่เตือน + ลง log ครั้งเดียว เมื่อเช็คเอาท์เลยมาแล้ว N วันแต่ยังไม่บันทึกคืน/หัก (กันลืมคืนลูกค้า)
                int overdueDays = Math.Max(1, PaymentGatewayConfig.GetInt("Security_Hold_Overdue_Days", 3));
                var overdue = _code.DatabaseQuerySafe(_conn, @"
                    SELECT h.ID, h.Hold_Ref, h.Reservation_ID, h.Amount, h.Provider, r.CheckoutDate
                      FROM Payment_Security_Holds h
                      JOIN Reservation r ON r.ID = h.Reservation_ID
                     WHERE h.[Status] = @held AND h.Expiry_Warned = 0
                       AND h.Provider IN ('CASH','TRANSFER')
                       AND r.CheckoutDate IS NOT NULL
                       AND r.CheckoutDate < DATEADD(DAY, -@d, GETDATE())",
                    new Dictionary<string, object> { { "@held", HoldStatus.Held }, { "@d", overdueDays } });

                if (overdue != null)
                    foreach (DataRow r in overdue.Rows)
                    {
                        _code.DatabaseInsertSafe(_conn,
                            "UPDATE Payment_Security_Holds SET Expiry_Warned = 1 WHERE ID = @id",
                            new Dictionary<string, object> { { "@id", r["ID"] } });
                        bool tr = string.Equals(Convert.ToString(r["Provider"]), ProviderTransfer,
                            StringComparison.OrdinalIgnoreCase);
                        string line = "เงินประกัน" + (tr ? "โอน" : "เงินสด") + " " + Convert.ToString(r["Hold_Ref"])
                            + " การจอง #" + Convert.ToString(r["Reservation_ID"])
                            + " ยอด " + Convert.ToDecimal(r["Amount"]).ToString("N2")
                            + " บาท — เช็คเอาท์ " + Convert.ToDateTime(r["CheckoutDate"]).ToString("dd/MM/yyyy")
                            + " แล้วแต่ยังไม่บันทึกคืน/หักค่าเสียหาย";
                        _code.Logs(_conn, "SecurityHold", "ค้างคืน: " + line, "System");
                        Notify.Send(Notify.Ev.PaymentHold,
                            "⏰ <b>เงินประกันยังไม่ได้คืน</b>\n" + Notify.E(line)
                            + "\n→ บันทึก " + (tr ? "\"โอนคืน\"" : "\"คืนเงินสด\"")
                            + " หรือ \"หักค่าเสียหาย\" ที่หน้าเช็คเอาท์");
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

        // ── เงินประกันโอน: คอลัมน์ + บันทึกโอนคืน ─────────────────────────────

        private static bool _hasTransferCols;
        private static DateTime _transferColsCheckedAt = DateTime.MinValue;

        /// <summary>มีคอลัมน์ของเงินประกันโอน (PHASE19_Migration_20) แล้วหรือยัง — ยังไม่มีก็ทำงานได้ (เก็บใน Raw_Response)</summary>
        public bool HasTransferColumns()
        {
            if (_hasTransferCols) return true;
            if ((DateTime.UtcNow - _transferColsCheckedAt).TotalMinutes < 5) return false;
            _transferColsCheckedAt = DateTime.UtcNow;
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_conn, @"
                    SELECT COUNT(*) AS N FROM sys.columns
                     WHERE object_id = OBJECT_ID('dbo.Payment_Security_Holds')
                       AND name IN ('Transfer_Ref','Transfer_Note','Refund_Amount','Refund_Ref','Refund_Note','Refund_Method')", null);
                _hasTransferCols = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["N"]) >= 6;
            }
            catch { _hasTransferCols = false; }
            return _hasTransferCols;
        }

        /// <summary>บันทึกการโอนคืนเงินประกัน (ใคร/เมื่อไหร่ อยู่ใน Released_By/Captured_By + วันที่ของแถวนั้น)</summary>
        private void SaveRefundRecord(long holdId, decimal refundAmount, string refundRef, string refundNote)
        {
            try
            {
                string rRef = Trunc(refundRef == null ? null : refundRef.Trim(), 100);
                string rNote = Trunc(refundNote == null ? null : refundNote.Trim(), 400);
                decimal amt = refundAmount < 0 ? 0m : refundAmount;
                if (HasTransferColumns())
                    _code.DatabaseInsertSafe(_conn, @"
                        UPDATE Payment_Security_Holds
                           SET Refund_Method = 'TRANSFER', Refund_Amount = @amt,
                               Refund_Ref = @ref, Refund_Note = @note, Updated_Date = GETDATE()
                         WHERE ID = @id",
                        new Dictionary<string, object>
                        {
                            { "@id", holdId }, { "@amt", amt },
                            { "@ref", (object)rRef ?? DBNull.Value }, { "@note", (object)rNote ?? DBNull.Value }
                        });
                else
                    _code.DatabaseInsertSafe(_conn, @"
                        UPDATE Payment_Security_Holds
                           SET Raw_Response = ISNULL(Raw_Response,'') + @txt, Updated_Date = GETDATE()
                         WHERE ID = @id",
                        new Dictionary<string, object>
                        {
                            { "@id", holdId },
                            { "@txt", " | REFUND TRANSFER amt=" + amt.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                                      + " ref=" + (rRef ?? "-") + " note=" + (rNote ?? "-") }
                        });
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "SecurityHold", "บันทึกโอนคืนไม่สำเร็จ (hold " + holdId + "): " + ex.Message, "System");
            }
        }

        /// <summary>ชื่อแหล่งเงินของบัญชีรับโอน (ใช้บอกพนักงานตอนออกใบเสร็จค่าเสียหาย)</summary>
        private static string TransferPaidHowText()
        {
            PaymentChannel ch = TransferChannel();
            if (ch == null) return "บัญชีโอนของที่พัก";
            return string.IsNullOrEmpty(ch.PaidHowName) ? ch.Name : ch.PaidHowName;
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

        /// <summary>
        /// มีคอลัมน์ Fail_Reason/Fail_Count แล้วหรือยัง (PHASE19_Migration_12)
        /// ยังไม่รันไฟล์ก็ทำงานได้ แค่ไม่เก็บเหตุผล — ไม่ทำให้การกันวงเงินพัง
        /// </summary>
        private static bool? _hasFailCols;
        private bool HasFailColumns()
        {
            if (_hasFailCols.HasValue) return _hasFailCols.Value;
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_conn, @"
                    SELECT COUNT(*) AS N FROM sys.columns
                     WHERE object_id = OBJECT_ID('dbo.Payment_Security_Holds')
                       AND name IN ('Fail_Reason','Fail_Count')", null);
                _hasFailCols = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["N"]) >= 2;
            }
            catch { _hasFailCols = false; }
            return _hasFailCols.Value;
        }

        /// <summary>
        /// บัตรไม่ผ่าน — คงสถานะเดิมไว้ให้ลองใหม่ได้ เก็บเหตุผลและจำนวนครั้งไว้ให้คนดูออก
        /// </summary>
        private void SaveFailedAttempt(long id, HoldResult r)
        {
            string reason = Trunc(r.Message ?? "ไม่ทราบสาเหตุ", 400);
            try
            {
                if (HasFailColumns())
                    _code.DatabaseInsertSafe(_conn, @"
                        UPDATE Payment_Security_Holds
                           SET Fail_Reason = @why, Fail_Count = ISNULL(Fail_Count,0) + 1,
                               Raw_Response = @raw, Updated_Date = GETDATE()
                         WHERE ID = @id",
                        new Dictionary<string, object>
                        {
                            { "@id", id }, { "@why", (object)reason ?? DBNull.Value },
                            { "@raw", (object)r.RawResponse ?? DBNull.Value }
                        });
                else
                    _code.DatabaseInsertSafe(_conn, @"
                        UPDATE Payment_Security_Holds
                           SET Raw_Response = @raw, Updated_Date = GETDATE()
                         WHERE ID = @id",
                        new Dictionary<string, object>
                        {
                            { "@id", id }, { "@raw", (object)r.RawResponse ?? DBNull.Value }
                        });
            }
            catch { }
        }

        /// <summary>
        /// ถามเกตเวย์ว่ารายการนี้สถานะอะไรกันแน่ แล้วปรับฐานข้อมูลให้ตรง
        ///
        /// ใช้กับรายการที่ "มี charge id แล้วแต่สถานะฝั่งเราดูไม่เข้าท่า" — เช่นเคสที่
        /// Omise authorize สำเร็จแต่เราบันทึกเป็น FAILED (บั๊กเก่าของ ToHoldResult)
        /// เงินถูกกันบนบัตรลูกค้าอยู่จริง ระบบต้องรู้ให้ตรงกัน ไม่งั้นวงเงินลอยค้าง
        ///
        /// คืนสถานะจริงที่ได้มา (null = ถามไม่ได้/ไม่มี charge id)
        /// </summary>
        public string SyncFromGateway(string holdRef)
        {
            var hold = GetByRef(holdRef);
            if (hold == null || string.IsNullOrEmpty(hold.ProviderChargeId)) return null;

            try
            {
                IDepositGateway gw = Gateway();
                if (gw == null) return null;

                HoldResult r = gw.GetHold(hold.ProviderChargeId);
                if (r == null || !r.Success || string.IsNullOrEmpty(r.Status)) return null;
                if (r.Status == hold.Status) return r.Status;    // ตรงกันอยู่แล้ว ไม่ต้องแตะ

                SaveGatewayResult(hold.ID, r);
                return r.Status;
            }
            catch { return null; }
        }

        /// <summary>เหตุผลครั้งล่าสุดที่บัตรไม่ผ่าน — null ถ้าไม่มี/ยังไม่ได้รันไฟล์ migration</summary>
        public string LastFailReason(string holdRef)
        {
            if (!HasFailColumns() || string.IsNullOrEmpty(holdRef)) return null;
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT Fail_Reason FROM Payment_Security_Holds WHERE Hold_Ref = @r",
                    new Dictionary<string, object> { { "@r", holdRef } });
                if (dt == null || dt.Rows.Count == 0) return null;
                string s = Convert.ToString(dt.Rows[0]["Fail_Reason"]);
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            catch { return null; }
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
            // เงินประกันโอน (PHASE19_20 — ยังไม่รัน = null)
            public string TransferRef, TransferNote, RefundRef, RefundNote;
            public decimal? RefundAmount;
            public bool IsTransfer { get { return string.Equals(Provider, ProviderTransfer, StringComparison.OrdinalIgnoreCase); } }
            public bool IsCash { get { return string.Equals(Provider, ProviderCash, StringComparison.OrdinalIgnoreCase); } }
        }

        private static string OptStr(DataRow r, string col)
        {
            return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToString(r[col]) : null;
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
            h.TransferRef = OptStr(r, "Transfer_Ref");
            h.TransferNote = OptStr(r, "Transfer_Note");
            h.RefundRef = OptStr(r, "Refund_Ref");
            h.RefundNote = OptStr(r, "Refund_Note");
            h.RefundAmount = r.Table.Columns.Contains("Refund_Amount") && r["Refund_Amount"] != DBNull.Value
                ? Convert.ToDecimal(r["Refund_Amount"]) : (decimal?)null;
            return h;
        }
    }
}
