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
    ///   · อื่น ๆ (HOTEL / NONE): คงเหลือ = ค่าห้อง + ค่าใช้จ่ายในห้อง − ยอดที่รับแล้ว
    ///   · เศษต่างไม่เกิน Balance_Rounding_Tolerance (ค่าเริ่มต้น 1 บาท) ถือว่าครบ
    ///   · จ่ายเกิน → Credit (Due = 0)
    /// </summary>
    public sealed class ReservationBalance
    {
        public const string ModeChannel = "CHANNEL";
        public const string ModeHotel = "HOTEL";
        public const string ModeNone = "NONE";

        public int ReservationId;
        public string CollectMode = ModeNone;   // CHANNEL | HOTEL | NONE
        public decimal RoomTotal;        // Reservation.TotalPrice
        public decimal Charges;          // SUM(Reservation_Product_Charges.TotalAmount) Status <> 'CANCELLED'
        public decimal PendingCharges;   // same, Status = 'PENDING'
        public decimal Total;            // RoomTotal + Charges
        public decimal PaidLedger;       // SUM(Payment_History.PaymentAmount) Status='COMPLETED' (refund rows are negative)
        public int LedgerRows;           // COUNT of those rows
        public decimal Deposit;          // Reservation.Deposit
        public decimal OtaCovered;       // CHANNEL: RoomTotal; else 0
        public decimal Due;              // what the front desk still has to collect (>= 0)
        public decimal Credit;           // overpaid amount (>= 0)
        public decimal Received;         // for display = Total - Due + Credit

        public bool IsChannelCollect { get { return CollectMode == ModeChannel; } }

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
            decimal pendingCharges, decimal paidLedger, int ledgerRows, decimal deposit)
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

            decimal paid = ledgerRows > 0 ? paidLedger : deposit;

            decimal due;
            if (b.CollectMode == ModeChannel)
            {
                // OTA เก็บค่าห้องไปแล้ว — หน้างานเก็บเฉพาะค่าใช้จ่ายในห้องที่ยังค้าง
                b.OtaCovered = roomTotal;
                due = pendingCharges;
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
                b.Credit = -due;
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

            DataTable dt;
            try
            {
                dt = db.DatabaseQuerySafe(conn, BuildSql(where, withOta, withCharges, withPayments), p);
            }
            catch
            {
                // ส่วนเสริม (คอลัมน์/ตารางจาก migration รุ่นหลัง) ทำให้ query พัง → ลองแบบพื้นฐานอีกครั้ง
                // ถ้าไม่มีส่วนเสริมอยู่แล้ว แปลว่าเงื่อนไขหลักพังจริง → โยนต่อให้ผู้เรียกจัดการ
                if (!withOta && !withCharges && !withPayments) throw;
                InvalidateSchema();
                dt = db.DatabaseQuerySafe(conn, BuildSql(where, false, false, false), p);
            }

            if (dt == null) return result;
            bool hasOtaCol = dt.Columns.Contains("OTA_Payment_Type");

            foreach (DataRow row in dt.Rows)
            {
                if (row["ID"] == DBNull.Value) continue;
                int id = Convert.ToInt32(row["ID"]);
                string remark = row["Remark"] == DBNull.Value ? "" : Convert.ToString(row["Remark"]);
                string otaPay = hasOtaCol && row["OTA_Payment_Type"] != DBNull.Value
                    ? Convert.ToString(row["OTA_Payment_Type"]) : "";

                var b = Compute(id,
                    DetectCollectMode(remark, otaPay),
                    Dec(row["TotalPrice"]),
                    Dec(row["Charges"]),
                    Dec(row["PendingCharges"]),
                    Dec(row["PaidLedger"]),
                    row["LedgerRows"] == DBNull.Value ? 0 : Convert.ToInt32(row["LedgerRows"]),
                    Dec(row["Deposit"]));

                // ใบจองที่ยกเลิกแล้วไม่มียอดค้างเก็บจากลูกค้า และ "รับแล้ว" = เงินที่รับจริง
                // (ไม่ใช่ Total − 0 = ราคาเต็ม) — ไม่งั้นหน้ารายการจองรวมยอดค้างของใบที่ยกเลิกไปด้วย
                string status = dt.Columns.Contains("Status") && row["Status"] != DBNull.Value
                    ? Convert.ToString(row["Status"]) : "";
                if (status.StartsWith("ยกเลิก", StringComparison.Ordinal))
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

        // ── ภายใน ─────────────────────────────────────────────────────────────

        private static string BuildSql(string where, bool withOta, bool withCharges, bool withPayments)
        {
            string chargeCols = withCharges
                ? "ISNULL(rbc.Charges, 0) AS Charges, ISNULL(rbc.PendingCharges, 0) AS PendingCharges"
                : "CAST(0 AS decimal(18,2)) AS Charges, CAST(0 AS decimal(18,2)) AS PendingCharges";
            string payCols = withPayments
                ? "ISNULL(rbp.PaidLedger, 0) AS PaidLedger, ISNULL(rbp.LedgerRows, 0) AS LedgerRows"
                : "CAST(0 AS decimal(18,2)) AS PaidLedger, 0 AS LedgerRows";

            string sql =
                "SELECT r.ID, ISNULL(r.TotalPrice, 0) AS TotalPrice, ISNULL(r.Deposit, 0) AS Deposit, r.Remark, r.Status" +
                (withOta ? ", r.OTA_Payment_Type" : "") +
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

        private static string NormalizeMode(string mode)
        {
            string m = (mode ?? "").Trim().ToUpperInvariant();
            if (m == ModeChannel) return ModeChannel;
            if (m == ModeHotel) return ModeHotel;
            return ModeNone;
        }

        // ตรวจโครงสร้างฐานข้อมูลครั้งเดียวแล้ว cache — ถ้าพบครบถือถาวร, ถ้าขาดบางอย่างตรวจใหม่ทุก 5 นาที
        // (เผื่อเพิ่งรัน migration ระหว่างที่แอปยังทำงานอยู่)
        private sealed class SchemaInfo
        {
            public bool HasOtaPaymentType, HasProductCharges, HasPaymentHistory;
            public DateTime CheckedAt;
            public bool Complete { get { return HasOtaPaymentType && HasProductCharges && HasPaymentHistory; } }
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
                             OBJECT_ID('Reservation_Product_Charges') AS Rpc,
                             OBJECT_ID('Payment_History') AS Ph");
                if (dt != null && dt.Rows.Count > 0)
                {
                    s.HasOtaPaymentType = dt.Rows[0]["OtaPay"] != DBNull.Value;
                    s.HasProductCharges = dt.Rows[0]["Rpc"] != DBNull.Value;
                    s.HasPaymentHistory = dt.Rows[0]["Ph"] != DBNull.Value;
                }
            }
            catch
            {
                // ตรวจไม่ได้ → สมมติว่ามีครบ (โครงสร้างปกติของระบบ) แล้วให้ fallback ใน LoadMany จัดการ
                s.HasOtaPaymentType = true;
                s.HasProductCharges = true;
                s.HasPaymentHistory = true;
                s.CheckedAt = DateTime.MinValue;   // อย่าถือเป็นผลถาวร
                return s;
            }

            lock (_schemaLock) { _schema = s; }
            return s;
        }
    }
}
