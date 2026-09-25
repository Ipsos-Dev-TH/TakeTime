using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// ยอดเงินของการจอง "สูตรเดียวทั้งระบบ" — ยอดรวม / รับแล้ว / คงเหลือ
    ///
    /// เดิมแต่ละหน้าคิดเองคนละแบบ: ตารางรายวัน (และรูปรายงาน LINE) นับเฉพาะ Payment_History,
    /// หน้ารายละเอียดใช้ Reservation.Deposit ⇒ การจอง OTA แบบ Channel Collect (OTA เก็บเงินไปแล้ว,
    /// ระบบรับอีเมลตั้ง Deposit = TotalPrice และไม่เขียน Payment_History) ถูกแสดงเป็น "ยังไม่จ่าย"
    /// เต็มยอดบนตารางรายวันจนกว่าจะเช็คอิน และยอดค้างชำระรวมบนหัวตารางสูงเกินจริง
    ///
    /// กติกา (Compute):
    ///   · ยอดที่รับแล้ว = SUM(Payment_History COMPLETED) ถ้ามีแถว, ไม่มีแถว → ใช้ Reservation.Deposit
    ///   · CHANNEL: ค่าห้องถือว่า OTA จ่ายแล้วเสมอ → หน้างานเก็บแค่ค่าใช้จ่ายในห้องที่ยัง PENDING
    ///   · UNKNOWN (ใบ OTA ที่ "ไม่รู้ว่าใครเก็บ" — ระบบรับอีเมลเดาให้ หรือไม่มีข้อมูล): ยังไม่ถือว่า OTA จ่าย
    ///     คงเหลือ = ค่าห้อง + ค่าใช้จ่ายในห้อง − ยอด Payment_History (ไม่นับ Deposit — ใบที่เดาเคยตั้ง Deposit = ยอดเต็ม)
    ///     ⇒ หน้างานเห็นยอดค้างจนกว่าเจ้าหน้าที่จะยืนยันโหมด (SetCollectMode) — ทิศที่ปลอดภัย (ไม่ปล่อยให้ไม่มีใครเก็บ)
    ///   · อื่น ๆ (HOTEL / NONE): คงเหลือ = ค่าห้อง + ค่าใช้จ่ายในห้อง − ยอดที่รับแล้ว
    ///   · เศษต่างไม่เกิน Balance_Rounding_Tolerance (ค่าเริ่มต้น 1 บาท) ถือว่าครบ
    ///   · จ่ายเกิน → Credit (Due = 0) — ยกเว้น CHANNEL / UNKNOWN ไม่รายงานจ่ายเกิน
    ///
    /// โหมดอ่านจากคอลัมน์ Reservation.OTA_Collect_Mode / OTA_Collect_Source (PHASE19 migration 17) ก่อน
    /// ไม่มีคอลัมน์/ค่ายังว่าง → ถอยไปดูหมายเหตุ + OTA_Payment_Type แบบเดิม (CollectSource = "REMARK")
    /// CHANNEL ที่มาจากการเดา (source GUESS) ถือเป็น UNKNOWN
    /// </summary>
    public sealed class ReservationBalance
    {
        public const string ModeChannel = "CHANNEL";
        public const string ModeHotel = "HOTEL";
        public const string ModeNone = "NONE";
        public const string ModeUnknown = "UNKNOWN";

        // ค่าของ OTA_Collect_Source
        private const string SourceEmail = "EMAIL";
        private const string SourceGuess = "GUESS";
        private const string SourceStaff = "STAFF";
        private const string SourceBackfill = "BACKFILL";
        private const string SourceRemark = "REMARK";

        public int ReservationId;
        public string CollectMode = ModeNone;   // CHANNEL | HOTEL | UNKNOWN | NONE
        public string CollectSource = "";          // EMAIL|GUESS|STAFF|BACKFILL|REMARK|""
        public bool IsOta;                         // OTA booking (OTA_Channel/OTA_Booking_ID non-empty or Remark collect marker)
        public bool OtaAmountEstimated;            // OtaAmount came from fallback (no OTA_Net/Gross)
        public decimal RoomTotal;        // Reservation.TotalPrice
        public decimal Charges;          // SUM(Reservation_Product_Charges.TotalAmount) Status <> 'CANCELLED'
        public decimal PendingCharges;   // same, Status = 'PENDING'
        public decimal Total;            // RoomTotal + Charges
        public decimal PaidLedger;       // SUM(Payment_History.PaymentAmount) Status='COMPLETED' (refund rows are negative)
        public int LedgerRows;           // COUNT of those rows
        public decimal Deposit;          // Reservation.Deposit
        public decimal OtaCovered;       // CHANNEL: ยอดที่ OTA เก็บแทนโรงแรม (ไม่เกินยอดรวม); else 0
        /// <summary>
        /// ยอดที่ OTA เก็บจากลูกค้าไปแล้ว ตาม "อีเมลจอง" (OTA_Net_Amount / OTA_Gross_Amount ตาม Email_Rsv_TotalSource)
        /// ไม่ใช่ Reservation.Deposit — Deposit ถูกเขียนทับได้หลายทาง (หน้าแก้ไขการจองตั้งเป็นยอด Payment_History
        /// = 0, เช็คอินตั้งเป็น TotalPrice) ถ้าใช้ Deposit คืนที่เพิ่มหลังแก้ไขจะถูกนับว่า OTA จ่ายแล้ว (เก็บเงินไม่ครบ)
        /// −1 = ไม่มีข้อมูล (ใบเก่าก่อนมีคอลัมน์ OTA_*)
        /// </summary>
        public decimal OtaAmount = -1m;
        public decimal Due;              // what the front desk still has to collect (>= 0)
        public decimal Credit;           // overpaid amount (>= 0)
        public decimal Received;         // for display = Total - Due + Credit

        public bool IsChannelCollect { get { return CollectMode == ModeChannel; } }
        public bool IsCollectUnknown { get { return CollectMode == ModeUnknown; } }

        /// <summary>เศษต่างที่ยอมให้ถือว่า "ครบแล้ว" (System_Config / Web.config: Balance_Rounding_Tolerance, ค่าเริ่มต้น 1.00)</summary>
        public static decimal RoundingTolerance
        {
            get
            {
                try
                {
                    string s = AppCfg.Get("Balance_Rounding_Tolerance", "1.00");
                    decimal v;
                    if (!string.IsNullOrWhiteSpace(s)
                        && decimal.TryParse(s.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out v))
                        return v < 0m ? 0m : v;
                }
                catch { /* อ่านค่าไม่ได้ → ใช้ค่าเริ่มต้น */ }
                return 1.00m;
            }
        }

        /// <summary>คำนวณยอด — ไม่แตะฐานข้อมูล (ยกเว้นอ่านค่า tolerance ผ่าน AppCfg ที่ cache ไว้)</summary>
        public static ReservationBalance Compute(int reservationId, string collectMode, decimal roomTotal, decimal charges,
            decimal pendingCharges, decimal paidLedger, int ledgerRows, decimal deposit, decimal otaAmount = -1m)
        {
            var b = new ReservationBalance();
            b.ReservationId = reservationId;
            b.CollectMode = NormalizeMode(collectMode);
            b.RoomTotal = roomTotal;
            b.Charges = charges;
            b.PendingCharges = pendingCharges;
            b.PaidLedger = paidLedger;
            b.LedgerRows = ledgerRows;
            b.Deposit = deposit;
            b.Total = roomTotal + charges;
            // ค่าเริ่มต้นสำหรับผู้เรียก Compute ตรง ๆ — LoadMany ตั้งค่าจริงทับให้อีกที
            b.IsOta = b.CollectMode != ModeNone;

            decimal paid = ledgerRows > 0 ? paidLedger : deposit;

            decimal due;
            bool channel = b.CollectMode == ModeChannel;
            bool unknown = b.CollectMode == ModeUnknown;
            if (unknown)
            {
                // ไม่รู้ว่าใครเก็บ → ยังไม่ถือว่า OTA จ่ายค่าห้อง และ "ไม่นับ Deposit"
                // (ระบบรับอีเมลรุ่นก่อนตั้ง Deposit = ยอดเต็มให้ใบที่เดาว่า Channel — ไม่ใช่เงินที่รับจริง)
                // ยอด OTA เก็บไว้แสดงเท่านั้น (ถ้าเจ้าหน้าที่ยืนยันว่า Channel ยอดนี้จะถูกนับว่า OTA จ่าย)
                b.OtaAmountEstimated = otaAmount < 0m;
                b.OtaAmount = otaAmount >= 0m ? otaAmount : roomTotal;
                b.OtaCovered = 0m;
                decimal paidByGuest = ledgerRows > 0 ? paidLedger : 0m;
                due = roomTotal + charges - paidByGuest;
            }
            else if (channel)
            {
                // OTA เก็บค่าห้อง "ตามที่จองไว้" ไปแล้ว — ลูกค้ายังต้องจ่ายหน้างาน:
                //   ส่วนที่ราคาห้องเกินยอด OTA (เพิ่มคืน/อัปเกรดหลังจอง) + ค่าใช้จ่ายในห้อง − ที่ลูกค้าจ่ายโรงแรมเองแล้ว
                //   ⚠ ยอด OTA มาจากอีเมลจอง (OtaAmount) ไม่ใช่ Deposit — ดูคำอธิบายที่ฟิลด์ OtaAmount
                //   ใบเก่าที่ไม่มีข้อมูลนี้ → ถอยไปใช้ Deposit (ตั้งตอนรับอีเมล) แล้วค่อยราคาห้อง
                decimal ota = otaAmount >= 0m ? otaAmount : (deposit > 0m ? deposit : roomTotal);
                b.OtaAmount = ota;
                b.OtaAmountEstimated = otaAmount < 0m;
                b.OtaCovered = Math.Min(ota, b.Total);
                decimal paidByGuest = ledgerRows > 0 ? paidLedger : 0m;   // Deposit ของใบ OTA = เงิน OTA ไม่ใช่ลูกค้าจ่าย
                due = roomTotal + charges - ota - paidByGuest;
                // ค่าชาร์จที่ยัง PENDING = ยังไม่ได้จ่ายแน่นอน — ต้องไม่ถูกกลบโดยแถว "เงินสดปลอม" ที่หน้าเช็คอินรุ่นเก่า
                // บังคับลงเต็มยอดค่าห้อง (Payment_History ของใบ OTA ที่เช็คอินก่อนแก้)
                if (due < pendingCharges) due = pendingCharges;
            }
            else
            {
                b.OtaCovered = 0m;
                due = roomTotal + charges - paid;
            }

            due = Math.Round(due, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(due) <= RoundingTolerance) due = 0m;

            if (due < 0m)
            {
                // ใบ OTA ไม่รายงาน "จ่ายเกิน" — ยอดติดลบมาจากแถวเงินสดปลอมของหน้าเช็คอินรุ่นเก่า ไม่ใช่เงินที่ต้องคืนลูกค้า
                // UNKNOWN เหมือนกัน — ยังไม่รู้ว่าใครถือเงิน จึงไม่รายงานว่าต้องคืนลูกค้า
                b.Credit = (channel || unknown) ? 0m : -due;
                b.Due = 0m;
            }
            else
            {
                b.Credit = 0m;
                b.Due = due;
            }

            b.Received = b.Total - b.Due + b.Credit;
            return b;
        }

        /// <summary>
        /// ใครเก็บเงินค่าห้อง — ดูจากหมายเหตุที่ระบบรับอีเมลเขียนไว้ก่อน ("(Hotel Collect)" / "(Channel Collect)")
        /// แล้วค่อยดู OTA_Payment_Type ด้วยตัวจำแนกเดียวกับตอนรับอีเมล; ไม่รู้ = NONE
        /// </summary>
        public static string DetectCollectMode(string remark, string otaPaymentType)
        {
            string rm = remark ?? "";
            // ตรวจ Hotel ก่อน — ถ้ามีทั้งสองคำ ให้ถือว่าต้องเก็บเงินเอง (ปลอดภัยกว่า: ไม่ปล่อยให้เงินหาย)
            if (rm.IndexOf("(Hotel Collect)", StringComparison.OrdinalIgnoreCase) >= 0) return ModeHotel;
            if (rm.IndexOf("(Channel Collect)", StringComparison.OrdinalIgnoreCase) >= 0) return ModeChannel;

            if (!string.IsNullOrWhiteSpace(otaPaymentType))
            {
                string c = Take_Time_BangPhra.Services.EmailReservationService.ClassifyCollect(otaPaymentType);
                if (c == Take_Time_BangPhra.Services.EmailReservationService.CollectChannel) return ModeChannel;
                if (c == Take_Time_BangPhra.Services.EmailReservationService.CollectHotel) return ModeHotel;
            }
            return ModeNone;
        }

        /// <summary>
        /// โหลดยอดของหลายการจองใน query เดียว — whereSql เป็นเงื่อนไขที่เชื่อถือได้ (เขียนในโค้ด ไม่ใช่ข้อความผู้ใช้)
        /// อ้างอิงตาราง Reservation ด้วย alias <c>r</c> เช่น "@d >= r.CheckinDate AND @d &lt; r.CheckoutDate"
        /// ค่าจากผู้ใช้ต้องส่งผ่าน parameters เท่านั้น
        /// </summary>
        public static Dictionary<int, ReservationBalance> LoadMany(string conn, string whereSql, Dictionary<string, object> parameters)
        {
            var result = new Dictionary<int, ReservationBalance>();
            string where = string.IsNullOrWhiteSpace(whereSql) ? "1=1" : whereSql;
            var p = parameters ?? new Dictionary<string, object>();
            var db = new code();

            SchemaInfo schema = GetSchema(conn, db);
            bool withOta = schema.HasOtaPaymentType;
            bool withCharges = schema.HasProductCharges;
            bool withPayments = schema.HasPaymentHistory;
            bool withCh = schema.HasOtaChannel;
            bool withBk = schema.HasOtaBookingId;
            bool withCollect = schema.HasCollectMode;

            DataTable dt;
            try
            {
                dt = db.DatabaseQuerySafe(conn,
                    BuildSql(where, withOta, withCharges, withPayments, withCh, withBk, withCollect), p);
            }
            catch
            {
                // ส่วนเสริม (คอลัมน์/ตารางจาก migration รุ่นหลัง) ทำให้ query พัง → ลองแบบพื้นฐานอีกครั้ง
                // ถ้าไม่มีส่วนเสริมอยู่แล้ว แปลว่าเงื่อนไขหลักพังจริง → โยนต่อให้ผู้เรียกจัดการ
                if (!withOta && !withCharges && !withPayments && !withCh && !withBk && !withCollect) throw;
                InvalidateSchema();
                dt = db.DatabaseQuerySafe(conn, BuildSql(where, false, false, false, false, false, false), p);
            }

            if (dt == null) return result;
            bool hasOtaCol = dt.Columns.Contains("OTA_Payment_Type");
            bool hasOtaAmt = dt.Columns.Contains("OtaAmount");
            bool hasOtaCh = dt.Columns.Contains("OTA_Channel");
            bool hasOtaBk = dt.Columns.Contains("OTA_Booking_ID");
            bool hasCMode = dt.Columns.Contains("OTA_Collect_Mode");
            bool hasCSrc = dt.Columns.Contains("OTA_Collect_Source");

            foreach (DataRow row in dt.Rows)
            {
                if (row["ID"] == DBNull.Value) continue;
                int id = Convert.ToInt32(row["ID"]);
                string remark = row["Remark"] == DBNull.Value ? "" : Convert.ToString(row["Remark"]);
                string otaPay = hasOtaCol && row["OTA_Payment_Type"] != DBNull.Value
                    ? Convert.ToString(row["OTA_Payment_Type"]) : "";
                string otaCh = hasOtaCh ? Str(row["OTA_Channel"]) : "";
                string otaBk = hasOtaBk ? Str(row["OTA_Booking_ID"]) : "";
                string storedMode = hasCMode ? Str(row["OTA_Collect_Mode"]) : "";
                string storedSrc = hasCSrc ? Str(row["OTA_Collect_Source"]) : "";

                bool isOta = otaCh.Trim().Length > 0 || otaBk.Trim().Length > 0 || HasRemarkCollectMarker(remark);
                string mode, source;
                ResolveMode(storedMode, storedSrc, remark, otaPay, isOta, out mode, out source);

                var b = Compute(id,
                    mode,
                    Dec(row["TotalPrice"]),
                    Dec(row["Charges"]),
                    Dec(row["PendingCharges"]),
                    Dec(row["PaidLedger"]),
                    row["LedgerRows"] == DBNull.Value ? 0 : Convert.ToInt32(row["LedgerRows"]),
                    Dec(row["Deposit"]),
                    hasOtaAmt && row["OtaAmount"] != DBNull.Value ? Dec(row["OtaAmount"]) : -1m);
                b.CollectSource = source;
                b.IsOta = isOta;

                // ใบจองที่ยกเลิกแล้วไม่มียอดค้างเก็บจากลูกค้า และ "รับแล้ว" = เงินที่รับจริง
                // (ไม่ใช่ Total − 0 = ราคาเต็ม) — ไม่งั้นหน้ารายการจองรวมยอดค้างของใบที่ยกเลิกไปด้วย
                string status = dt.Columns.Contains("Status") && row["Status"] != DBNull.Value
                    ? Convert.ToString(row["Status"]) : "";
                // "ลบจากการเลื่อนวันเข้าพัก" = ใบที่ถูกย้ายไปใบใหม่ตอนเลื่อนวัน — ไม่มียอดค้างเช่นกัน
                if (status.StartsWith("ยกเลิก", StringComparison.Ordinal)
                    || status.StartsWith("ลบ", StringComparison.Ordinal))
                {
                    decimal paidActual = b.LedgerRows > 0 ? b.PaidLedger : b.Deposit;
                    b.Due = 0m;
                    b.Credit = 0m;
                    b.Received = paidActual;
                }
                result[id] = b;
            }
            return result;
        }

        /// <summary>ยอดของการจองเดียว — ไม่พบคืน null</summary>
        public static ReservationBalance Load(string conn, int reservationId)
        {
            var map = LoadMany(conn, "r.ID = @rbId", new Dictionary<string, object> { { "@rbId", reservationId } });
            ReservationBalance b;
            return map.TryGetValue(reservationId, out b) ? b : null;
        }

        // ── เปลี่ยนวิธีเก็บเงิน (เจ้าหน้าที่ยืนยัน) ─────────────────────────────────

        /// <summary>ผลของ <see cref="SetCollectMode"/> — OldMode/NewMode เป็นโหมด "ที่ใช้จริง" (CHANNEL ที่เดา = UNKNOWN, ว่าง = "")</summary>
        public sealed class CollectModeChangeResult
        {
            public bool Ok;
            public string Message;
            public string OldMode;
            public string NewMode;
        }

        /// <summary>
        /// ตั้งวิธีเก็บเงินค่าห้องของการจอง (CHANNEL | HOTEL) ลง Reservation.OTA_Collect_Mode / OTA_Collect_Source
        /// พร้อมบันทึกประวัติลง Reservation_Collect_Mode_Log
        ///
        /// ล็อก (ไม่ยอมเปลี่ยน) เมื่อเงินค่าห้องถูกลงบัญชีไปแล้ว:
        ///   · Reservation.Ota_Revenue_Ref มีค่า (ยกเว้น 'LEGACY' / 'SKIP…' ที่ไม่ได้โพสต์จริง) = โพสต์รายได้ OTA แล้ว
        ///   · มี Account_Receipt (Normal, ไม่ใช่มัดจำ) ที่ Nexaacc_Receipt_Payment_Id ไม่ว่าง = ใบเสร็จ sync แล้ว
        /// เปลี่ยนเป็น HOTEL บนใบ OTA ที่ Deposit = ยอด OTA และไม่มี Payment_History → ตั้ง Deposit = 0 ใน UPDATE เดียวกัน
        /// (Deposit นั้นระบบรับอีเมลตั้งให้ ไม่ใช่เงินที่โรงแรมรับจริง)
        /// การตรวจสิทธิ์ทิศทาง (ใครเปลี่ยนจากอะไรเป็นอะไรได้) เป็นหน้าที่ของผู้เรียก
        /// </summary>
        public static CollectModeChangeResult SetCollectMode(string conn, int reservationId, string newMode, string source, string changedBy, string reason)
        {
            var res = new CollectModeChangeResult { Ok = false, Message = "", OldMode = "", NewMode = "" };

            string mode = (newMode ?? "").Trim().ToUpperInvariant();
            if (mode != ModeChannel && mode != ModeHotel)
            {
                res.Message = "โหมดการเก็บเงินไม่ถูกต้อง (ต้องเป็น CHANNEL หรือ HOTEL)";
                return res;
            }
            res.NewMode = mode;

            string src = (source ?? "").Trim().ToUpperInvariant();
            if (src != SourceEmail && src != SourceGuess && src != SourceStaff && src != SourceBackfill) src = SourceStaff;

            string by = string.IsNullOrWhiteSpace(changedBy) ? "SYSTEM" : changedBy.Trim();
            if (by.Length > 100) by = by.Substring(0, 100);
            string why = (reason ?? "").Trim();

            var db = new code();
            try
            {
                // ── โครงสร้างฐานข้อมูล (ไม่ cache — เรียกไม่บ่อย และต้องรู้ทันทีหลังรัน migration)
                DataTable sc = db.DatabaseQuerySafe(conn,
                    @"SELECT COL_LENGTH('Reservation', 'OTA_Collect_Mode') AS CMode,
                             COL_LENGTH('Reservation', 'OTA_Collect_Source') AS CSrc,
                             OBJECT_ID('Reservation_Collect_Mode_Log') AS LogT,
                             COL_LENGTH('Reservation', 'Ota_Revenue_Ref') AS RevRef,
                             COL_LENGTH('Reservation', 'OTA_Channel') AS OtaCh,
                             COL_LENGTH('Reservation', 'OTA_Booking_ID') AS OtaBk,
                             COL_LENGTH('Reservation', 'OTA_Net_Amount') AS OtaNet,
                             COL_LENGTH('Reservation', 'OTA_Gross_Amount') AS OtaGross,
                             COL_LENGTH('Account_Receipt', 'Nexaacc_Receipt_Payment_Id') AS RcptMark,
                             COL_LENGTH('Account_Receipt', 'IsDeposit') AS RcptDep,
                             OBJECT_ID('Payment_History') AS Ph");
                if (sc == null || sc.Rows.Count == 0)
                {
                    res.Message = "ตรวจโครงสร้างฐานข้อมูลไม่ได้";
                    return res;
                }
                DataRow s0 = sc.Rows[0];
                if (s0["CMode"] == DBNull.Value || s0["CSrc"] == DBNull.Value || s0["LogT"] == DBNull.Value)
                {
                    res.Message = "ยังไม่ได้รัน migration 17 (PHASE19_Migration_17_OTA_Collect_Mode.sql) — บันทึกวิธีเก็บเงินไม่ได้";
                    return res;
                }
                bool hasRevRef = s0["RevRef"] != DBNull.Value;
                bool hasCh = s0["OtaCh"] != DBNull.Value;
                bool hasBk = s0["OtaBk"] != DBNull.Value;
                bool hasOtaAmt = s0["OtaNet"] != DBNull.Value && s0["OtaGross"] != DBNull.Value;
                bool hasRcptMark = s0["RcptMark"] != DBNull.Value;
                bool hasRcptDep = s0["RcptDep"] != DBNull.Value;
                bool hasPh = s0["Ph"] != DBNull.Value;

                // เงื่อนไข "ใบเสร็จที่ sync เข้าบัญชีแล้ว" — ใช้ทั้งตอนอ่านและใน WHERE ของ UPDATE (กันแข่งกัน)
                // {0} = นิพจน์ ID ของการจองฝั่งนอก
                string syncedReceiptCond = hasRcptMark
                    ? "SELECT 1 FROM Account_Receipt ar WHERE ar.Reservation_ID = {0}" +
                      " AND ISNULL(ar.Status, N'Normal') = N'Normal'" +
                      (hasRcptDep ? " AND ISNULL(ar.IsDeposit, 0) = 0" : "") +
                      " AND ar.Nexaacc_Receipt_Payment_Id IS NOT NULL"
                    : null;

                // ── อ่านสถานะปัจจุบัน
                string readSql =
                    @"SELECT r.ID, r.OTA_Collect_Mode, r.OTA_Collect_Source,
                             ISNULL(r.Deposit, 0) AS Deposit, ISNULL(r.TotalPrice, 0) AS TotalPrice,
                             CAST(r.Remark AS NVARCHAR(MAX)) AS Remark" +
                    (hasCh ? ", r.OTA_Channel" : "") +
                    (hasBk ? ", r.OTA_Booking_ID" : "") +
                    (hasOtaAmt ? ", " + OtaAmountSql + " AS OtaAmount" : "") +
                    (hasRevRef ? ", r.Ota_Revenue_Ref" : "") +
                    (hasPh ? @", (SELECT COUNT(*) FROM Payment_History ph
                                   WHERE ph.Reservation_ID = r.ID AND ph.Status = 'COMPLETED') AS LedgerRows" : "") +
                    (syncedReceiptCond != null
                        ? ", CASE WHEN EXISTS (" + string.Format(syncedReceiptCond, "r.ID") + ") THEN 1 ELSE 0 END AS SyncedReceipt"
                        : "") + @"
                      FROM Reservation r
                     WHERE r.ID = @id";
                DataTable dt = db.DatabaseQuerySafe(conn, readSql,
                    new Dictionary<string, object> { { "@id", reservationId } });
                if (dt == null || dt.Rows.Count == 0)
                {
                    res.Message = "ไม่พบการจอง #" + reservationId;
                    return res;
                }
                DataRow r = dt.Rows[0];

                string oldModeStored = Str(r["OTA_Collect_Mode"]).Trim();
                string oldSrcStored = Str(r["OTA_Collect_Source"]).Trim();
                string oldModeRaw = oldModeStored.ToUpperInvariant();
                string oldSrcRaw = oldSrcStored.ToUpperInvariant();
                res.OldMode = (oldModeRaw == ModeChannel && oldSrcRaw == SourceGuess) ? ModeUnknown : oldModeRaw;

                if (oldModeRaw == mode && oldSrcRaw == src)
                {
                    res.Ok = true;
                    res.Message = "ไม่มีการเปลี่ยนแปลง (วิธีเก็บเงินเป็นค่านี้อยู่แล้ว)";
                    return res;
                }

                // ── ล็อก: เงินค่าห้องลงบัญชีไปแล้ว
                string revRef = hasRevRef ? Str(r["Ota_Revenue_Ref"]).Trim() : "";
                if (revRef.Length > 0
                    && !revRef.Equals("LEGACY", StringComparison.OrdinalIgnoreCase)
                    && !revRef.StartsWith("SKIP", StringComparison.OrdinalIgnoreCase))
                {
                    res.Message = "เปลี่ยนวิธีเก็บเงินไม่ได้: รายได้ค่าห้อง OTA ของการจองนี้ลงบัญชีไปแล้ว (อ้างอิง " + revRef +
                                  ") — กรุณาแจ้งฝ่ายบัญชีให้ตรวจสอบ/กลับรายการก่อน";
                    return res;
                }
                if (syncedReceiptCond != null && r["SyncedReceipt"] != DBNull.Value && Convert.ToInt32(r["SyncedReceipt"]) > 0)
                {
                    res.Message = "เปลี่ยนวิธีเก็บเงินไม่ได้: การจองนี้มีใบเสร็จที่ส่งเข้าระบบบัญชี (NextAcc) แล้ว " +
                                  "— กรุณาแจ้งฝ่ายบัญชีให้ตรวจสอบก่อน";
                    return res;
                }

                // ── HOTEL บนใบ OTA: Deposit ที่ระบบรับอีเมลตั้งให้ (= ยอด OTA) ไม่ใช่เงินที่รับจริง → ล้างเป็น 0
                decimal deposit = Dec(r["Deposit"]);
                decimal totalPrice = Dec(r["TotalPrice"]);
                string remark = Str(r["Remark"]);
                bool isOta = (hasCh && Str(r["OTA_Channel"]).Trim().Length > 0)
                             || (hasBk && Str(r["OTA_Booking_ID"]).Trim().Length > 0)
                             || HasRemarkCollectMarker(remark);
                int ledgerRows = hasPh && r["LedgerRows"] != DBNull.Value ? Convert.ToInt32(r["LedgerRows"]) : 0;
                decimal otaAmt = hasOtaAmt && r["OtaAmount"] != DBNull.Value ? Dec(r["OtaAmount"]) : -1m;
                bool depositIsOtaMoney = deposit > 0m
                    && ((otaAmt >= 0m && Math.Abs(deposit - otaAmt) <= 0.01m) || Math.Abs(deposit - totalPrice) <= 0.01m);
                bool zeroDeposit = mode == ModeHotel && isOta && ledgerRows == 0 && depositIsOtaMoney;

                string logReason = why;
                if (zeroDeposit)
                    logReason = (logReason.Length > 0 ? logReason + " | " : "") +
                                "Deposit " + deposit.ToString("N2", CultureInfo.InvariantCulture) +
                                " → 0 (เป็นยอด OTA ที่ระบบรับอีเมลตั้งให้ ไม่ใช่เงินที่รับจริง)";
                if (logReason.Length > 500) logReason = logReason.Substring(0, 500);

                // ── UPDATE + log ใน transaction เดียว; WHERE ตรวจซ้ำว่าไม่มีใครเปลี่ยน/ล็อกระหว่างนี้
                string sql =
                    @"SET NOCOUNT ON;
                      SET XACT_ABORT ON;
                      DECLARE @n INT;
                      BEGIN TRAN;
                      UPDATE Reservation
                         SET OTA_Collect_Mode = @newMode, OTA_Collect_Source = @newSrc" +
                    (zeroDeposit ? ", Deposit = 0" : "") + @"
                       WHERE ID = @id
                         AND ISNULL(OTA_Collect_Mode, N'') = @oldMode
                         AND ISNULL(OTA_Collect_Source, N'') = @oldSrc" +
                    (zeroDeposit ? " AND ABS(ISNULL(Deposit, 0) - @oldDep) <= 0.01" : "") +
                    (hasRevRef
                        ? " AND (Ota_Revenue_Ref IS NULL OR Ota_Revenue_Ref = N'LEGACY' OR Ota_Revenue_Ref LIKE N'SKIP%')"
                        : "") +
                    (syncedReceiptCond != null
                        ? " AND NOT EXISTS (" + string.Format(syncedReceiptCond, "Reservation.ID") + ")"
                        : "") + @";
                      SET @n = @@ROWCOUNT;
                      IF @n > 0
                          INSERT INTO Reservation_Collect_Mode_Log
                              (Reservation_ID, Old_Mode, New_Mode, Old_Source, New_Source, Reason, Changed_By, Changed_Date)
                          VALUES (@id, NULLIF(@oldMode, N''), @newMode, NULLIF(@oldSrc, N''), @newSrc, @reason, @by, GETDATE());
                      COMMIT;
                      SELECT @n AS N;";

                var p = new Dictionary<string, object>
                {
                    { "@id", reservationId },
                    { "@newMode", mode },
                    { "@newSrc", src },
                    { "@oldMode", oldModeStored },
                    { "@oldSrc", oldSrcStored },
                    { "@oldDep", deposit },
                    { "@reason", logReason },
                    { "@by", by }
                };
                DataTable up = db.DatabaseQuerySafe(conn, sql, p);
                int n = up != null && up.Rows.Count > 0 && up.Rows[0][0] != DBNull.Value ? Convert.ToInt32(up.Rows[0][0]) : 0;
                if (n <= 0)
                {
                    res.Message = "บันทึกไม่สำเร็จ: ข้อมูลการจองเปลี่ยนระหว่างบันทึก หรือถูกล็อกโดยฝ่ายบัญชี — โหลดหน้าใหม่แล้วลองอีกครั้ง";
                    return res;
                }

                try
                {
                    db.Logs(conn, "CollectMode",
                        "การจอง #" + reservationId + " วิธีเก็บเงิน " +
                        (oldModeRaw.Length > 0 ? oldModeRaw + "/" + oldSrcRaw : "(ว่าง)") + " → " + mode + "/" + src +
                        (logReason.Length > 0 ? " — " + logReason : ""), by);
                }
                catch { /* log ไม่ได้ไม่ใช่เหตุให้ล้ม */ }

                res.Ok = true;
                res.Message = mode == ModeHotel
                    ? "บันทึกแล้ว: โรงแรมเก็บเงินเอง (Hotel Collect)" + (zeroDeposit ? " — ล้างยอดมัดจำที่ระบบตั้งให้เป็น 0" : "")
                    : "บันทึกแล้ว: OTA เก็บเงินแล้ว (Channel Collect)";
                return res;
            }
            catch (Exception ex)
            {
                res.Ok = false;
                res.Message = "บันทึกวิธีเก็บเงินไม่สำเร็จ: " + ex.Message;
                return res;
            }
        }

        // ── ภายใน ─────────────────────────────────────────────────────────────

        /// <summary>ยอดที่ OTA เก็บตามอีเมลจอง (alias r) — ต้องมีคอลัมน์ OTA_Gross_Amount / OTA_Net_Amount</summary>
        private const string OtaAmountSql =
            @"CASE WHEN ISNULL((SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config
                                WHERE ConfigKey = 'Email_Rsv_TotalSource'), 'AMOUNT') = 'REFSELL'
                        AND ISNULL(r.OTA_Gross_Amount, 0) > 0 THEN r.OTA_Gross_Amount
                   WHEN ISNULL(r.OTA_Net_Amount, 0) > 0 THEN r.OTA_Net_Amount
                   WHEN ISNULL(r.OTA_Gross_Amount, 0) > 0 THEN r.OTA_Gross_Amount
                   ELSE NULL END";

        /// <summary>
        /// ตัดสินโหมดที่ใช้คำนวณ: คอลัมน์ OTA_Collect_Mode ก่อน (CHANNEL ที่เดา = UNKNOWN)
        /// ไม่มีค่า → หมายเหตุ/OTA_Payment_Type แบบเดิม; ใบ OTA ที่ตัดสินไม่ได้ = UNKNOWN; ไม่ใช่ OTA = NONE
        /// </summary>
        private static void ResolveMode(string storedMode, string storedSource, string remark, string otaPay, bool isOta,
            out string mode, out string source)
        {
            string sm = (storedMode ?? "").Trim().ToUpperInvariant();
            string ss = (storedSource ?? "").Trim().ToUpperInvariant();
            if (sm == ModeChannel || sm == ModeHotel || sm == ModeUnknown)
            {
                source = ss;
                mode = (sm == ModeChannel && ss == SourceGuess) ? ModeUnknown : sm;
                return;
            }

            // ยังไม่มีค่าในคอลัมน์ (ก่อนรัน migration 17 / ใบที่ยังไม่ backfill)
            string detected = DetectCollectMode(remark, otaPay);
            // "[ระบบเดาให้]" ต่อท้ายบรรทัดการชำระในหมายเหตุ = ระบบรับอีเมลเดาเอง (ตรงกับ backfill ของ migration 17)
            bool guessedRemark = HasRemarkCollectMarker(remark)
                                 && (remark ?? "").IndexOf("ระบบเดาให้", StringComparison.Ordinal) >= 0;
            if (detected == ModeChannel && guessedRemark)
            {
                mode = ModeUnknown;
                source = SourceGuess;
                return;
            }
            if (detected != ModeNone)
            {
                mode = detected;
                source = guessedRemark ? SourceGuess : SourceRemark;
                return;
            }
            if (isOta)
            {
                mode = ModeUnknown;
                source = SourceRemark;
                return;
            }
            mode = ModeNone;
            source = "";
        }

        private static bool HasRemarkCollectMarker(string remark)
        {
            string rm = remark ?? "";
            return rm.IndexOf("(Hotel Collect)", StringComparison.OrdinalIgnoreCase) >= 0
                || rm.IndexOf("(Channel Collect)", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildSql(string where, bool withOta, bool withCharges, bool withPayments,
            bool withCh, bool withBk, bool withCollect)
        {
            string chargeCols = withCharges
                ? "ISNULL(rbc.Charges, 0) AS Charges, ISNULL(rbc.PendingCharges, 0) AS PendingCharges"
                : "CAST(0 AS decimal(18,2)) AS Charges, CAST(0 AS decimal(18,2)) AS PendingCharges";
            string payCols = withPayments
                ? "ISNULL(rbp.PaidLedger, 0) AS PaidLedger, ISNULL(rbp.LedgerRows, 0) AS LedgerRows"
                : "CAST(0 AS decimal(18,2)) AS PaidLedger, 0 AS LedgerRows";

            string sql =
                "SELECT r.ID, ISNULL(r.TotalPrice, 0) AS TotalPrice, ISNULL(r.Deposit, 0) AS Deposit, r.Remark, r.Status" +
                (withOta ? ", r.OTA_Payment_Type, " + OtaAmountSql + " AS OtaAmount" : "") +
                (withCh ? ", r.OTA_Channel" : "") +
                (withBk ? ", r.OTA_Booking_ID" : "") +
                (withCollect ? ", r.OTA_Collect_Mode, r.OTA_Collect_Source" : "") +
                ", " + chargeCols + ", " + payCols + @"
                  FROM Reservation r";

            if (withCharges)
                sql += @"
                  OUTER APPLY (SELECT SUM(CASE WHEN rbx.Status <> 'CANCELLED' THEN rbx.TotalAmount ELSE 0 END) AS Charges,
                                      SUM(CASE WHEN rbx.Status = 'PENDING' THEN rbx.TotalAmount ELSE 0 END) AS PendingCharges
                                 FROM Reservation_Product_Charges rbx
                                WHERE rbx.Reservation_ID = r.ID) rbc";

            if (withPayments)
                sql += @"
                  OUTER APPLY (SELECT SUM(rby.PaymentAmount) AS PaidLedger, COUNT(*) AS LedgerRows
                                 FROM Payment_History rby
                                WHERE rby.Reservation_ID = r.ID AND rby.Status = 'COMPLETED') rbp";

            sql += @"
                 WHERE " + where;
            return sql;
        }

        private static decimal Dec(object v)
        {
            if (v == null || v == DBNull.Value) return 0m;
            try { return Convert.ToDecimal(v); }
            catch { return 0m; }
        }

        private static string Str(object v)
        {
            if (v == null || v == DBNull.Value) return "";
            return Convert.ToString(v) ?? "";
        }

        private static string NormalizeMode(string mode)
        {
            string m = (mode ?? "").Trim().ToUpperInvariant();
            if (m == ModeChannel) return ModeChannel;
            if (m == ModeHotel) return ModeHotel;
            if (m == ModeUnknown) return ModeUnknown;
            return ModeNone;
        }

        // ตรวจโครงสร้างฐานข้อมูลครั้งเดียวแล้ว cache — ถ้าพบครบถือถาวร, ถ้าขาดบางอย่างตรวจใหม่ทุก 5 นาที
        // (เผื่อเพิ่งรัน migration ระหว่างที่แอปยังทำงานอยู่)
        private sealed class SchemaInfo
        {
            public bool HasOtaPaymentType, HasProductCharges, HasPaymentHistory;
            public bool HasOtaChannel, HasOtaBookingId, HasCollectMode;
            public DateTime CheckedAt;
            public bool Complete
            {
                get
                {
                    return HasOtaPaymentType && HasProductCharges && HasPaymentHistory
                        && HasOtaChannel && HasOtaBookingId && HasCollectMode;
                }
            }
        }

        private static readonly object _schemaLock = new object();
        private static SchemaInfo _schema;

        private static void InvalidateSchema()
        {
            lock (_schemaLock) { _schema = null; }
        }

        private static SchemaInfo GetSchema(string conn, code db)
        {
            lock (_schemaLock)
            {
                if (_schema != null && (_schema.Complete || DateTime.Now - _schema.CheckedAt < TimeSpan.FromMinutes(5)))
                    return _schema;
            }

            var s = new SchemaInfo { CheckedAt = DateTime.Now };
            try
            {
                DataTable dt = db.DatabaseQuerySafe(conn,
                    @"SELECT COL_LENGTH('Reservation', 'OTA_Payment_Type') AS OtaPay,
                             COL_LENGTH('Reservation', 'OTA_Channel') AS OtaCh,
                             COL_LENGTH('Reservation', 'OTA_Booking_ID') AS OtaBk,
                             COL_LENGTH('Reservation', 'OTA_Collect_Mode') AS CMode,
                             COL_LENGTH('Reservation', 'OTA_Collect_Source') AS CSrc,
                             OBJECT_ID('Reservation_Product_Charges') AS Rpc,
                             OBJECT_ID('Payment_History') AS Ph");
                if (dt != null && dt.Rows.Count > 0)
                {
                    s.HasOtaPaymentType = dt.Rows[0]["OtaPay"] != DBNull.Value;
                    s.HasOtaChannel = dt.Rows[0]["OtaCh"] != DBNull.Value;
                    s.HasOtaBookingId = dt.Rows[0]["OtaBk"] != DBNull.Value;
                    s.HasCollectMode = dt.Rows[0]["CMode"] != DBNull.Value && dt.Rows[0]["CSrc"] != DBNull.Value;
                    s.HasProductCharges = dt.Rows[0]["Rpc"] != DBNull.Value;
                    s.HasPaymentHistory = dt.Rows[0]["Ph"] != DBNull.Value;
                }
            }
            catch
            {
                // ตรวจไม่ได้ → สมมติว่ามีส่วนเดิมครบ (โครงสร้างปกติของระบบ) แล้วให้ fallback ใน LoadMany จัดการ
                // คอลัมน์ของ migration 17 (OTA_Collect_*) ไม่สมมติ — ถ้ายังไม่มี query จะพังทั้งชุดโดยไม่จำเป็น
                s.HasOtaPaymentType = true;
                s.HasProductCharges = true;
                s.HasPaymentHistory = true;
                s.HasOtaChannel = true;
                s.HasOtaBookingId = true;
                s.HasCollectMode = false;
                s.CheckedAt = DateTime.MinValue;   // อย่าถือเป็นผลถาวร
                return s;
            }

            lock (_schemaLock) { _schema = s; }
            return s;
        }
    }
}
