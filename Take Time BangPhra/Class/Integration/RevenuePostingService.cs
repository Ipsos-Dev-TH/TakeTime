using System;
using System.Collections.Generic;
using System.Data;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// ปิดช่องรายได้ที่ "เกิดจริงแต่ไม่เคยเข้าบัญชี" — เปิด/ปิดได้รายตัวจากหน้า Accounting Integration
    ///
    /// ตรวจพบจากการไล่โค้ดเส้นทางเงินทั้งระบบ:
    ///
    ///  1) **รูมเซอร์วิส** (`Guest_Room_Service_Orders`) — ลูกค้าสั่งอาหารผ่าน Guest Portal
    ///     • จ่ายเอง (โอน/เงินสด) → ไม่ถูกบันทึกที่ไหนเลย (หน้าเช็คเอาท์รวมเฉพาะ CHARGE_TO_ROOM)
    ///     • ทุกออเดอร์ → ไม่เคยตัดต้นทุน/สต๊อก (ไม่มี Product_Out, ไม่มี COGS)
    ///     ⟹ job นี้: จ่ายเอง = รวบเป็นใบรับเงินสด 1 ใบ/วัน/วิธีจ่าย + COGS,
    ///        ลงบิลห้อง = COGS อย่างเดียว (รายได้ไปกับใบเสร็จเช็คเอาท์แล้ว — ไม่โพสต์ซ้ำ)
    ///
    ///  2) **การจองจาก OTA** — อีเมล STAAH สร้างการจองด้วย `NoCreateReceipt=1` จึงไม่มีใบเสร็จ
    ///     ⟹ รายได้ค่าห้องจาก Agoda/Booking ไม่เคยเข้าบัญชี
    ///     ⟹ job นี้: หลังเลยวันเช็คเอาท์ โพสต์ Dr ลูกหนี้ OTA / Cr รายได้ห้อง / Cr ภาษีขาย
    ///        ต่อการจอง (ตาม docs/OTA_Settlement_Design.md เคส A) — ยอดโอนจริงจาก OTA
    ///        ไปตัดลูกหนี้ตอนปิดงวด payout
    ///
    /// ทุก job เป็น idempotent (marker ในตาราง + dedup ของ queue) และเรียกจาก background timer
    /// เดียวกับ POS rollup — no-op ทันทีถ้าปิดสวิตช์
    /// </summary>
    public class RevenuePostingService
    {
        private readonly string _conn;
        private readonly code _code = new code();
        private readonly AccountingConfig _config;
        private readonly AccountingSyncService _sync;

        public RevenuePostingService(string connectionString)
        {
            _conn = connectionString;
            _config = new AccountingConfig(connectionString);
            _sync = new AccountingSyncService(connectionString);
        }

        // ═══════════════════════════════════════════════════════════════════
        // 1) รูมเซอร์วิส
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// โพสต์รายได้/ต้นทุนรูมเซอร์วิสของ "วันที่จบแล้ว" (&lt; วันนี้) — no-op ถ้าปิดสวิตช์.
        /// เรียกจาก background timer. idempotent ด้วย `Guest_Room_Service_Orders.Acct_Post_Ref`
        /// </summary>
        public void PostRoomServiceRevenueIfDue(int maxDaysPerRun = 14)
        {
            if (!_config.IsConfigured || !_config.Enabled || !_config.IsRoomServiceRevenueEnabled) return;

            try
            {
                // กลุ่ม (วัน × วิธีจ่าย) ที่ยังไม่โพสต์ — เฉพาะออเดอร์ที่ส่งของแล้ว/ยืนยันแล้ว
                var days = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP (@cap) CAST(o.Order_Date AS DATE) AS D, o.Payment_Method AS PM
                        FROM Guest_Room_Service_Orders o
                       WHERE o.Acct_Post_Ref IS NULL
                         AND o.Order_Status NOT IN ('CANCELLED', 'PENDING')
                         AND CAST(o.Order_Date AS DATE) < CAST(GETDATE() AS DATE)
                       GROUP BY CAST(o.Order_Date AS DATE), o.Payment_Method
                       ORDER BY CAST(o.Order_Date AS DATE)",
                    new Dictionary<string, object> { { "@cap", maxDaysPerRun } });

                if (days == null || days.Rows.Count == 0) return;

                foreach (DataRow g in days.Rows)
                {
                    DateTime day = Convert.ToDateTime(g["D"]);
                    string payMethod = g["PM"]?.ToString() ?? "";
                    try { ProcessRoomServiceDay(day, payMethod); }
                    catch (Exception exDay)
                    {
                        Log($"RoomServiceRevenue: วันที่ {day:yyyy-MM-dd} ({payMethod}) ล้มเหลว: {exDay.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log("PostRoomServiceRevenueIfDue: " + ex.Message);
            }
        }

        private void ProcessRoomServiceDay(DateTime day, string payMethod)
        {
            string ds = day.ToString("yyyyMMdd");
            string methodKey = SanitizeKey(payMethod);
            string postRef = $"RSDAY-{ds}-{methodKey}";

            // ลงบิลห้อง = รายได้อยู่ในใบเสร็จเช็คเอาท์แล้ว → โพสต์เฉพาะต้นทุน กันรายได้ซ้ำ
            bool chargeToRoom = payMethod.Equals("CHARGE_TO_ROOM", StringComparison.OrdinalIgnoreCase);

            // ยอดต่อสินค้าในกลุ่ม (ราคาขาย + ต้นทุนจาก Product.Cost_Price)
            // สินค้าที่ตั้ง "ไม่รวมใบสรุปรายวัน" (Include_In_Daily_Rollup = 0) ถูกกรองออก
            // ทั้งรายได้และต้นทุน — ตั้งได้รายสินค้าที่หน้า ตั้งค่าลงบัญชีรายสินค้า
            // (LEFT JOIN: สินค้าที่หาไม่เจอ/ถูกลบ ถือว่ารวมตามเดิม)
            string flagFilter = HasRollupFlagColumn()
                ? " AND ISNULL(p.Include_In_Daily_Rollup, 1) = 1" : "";
            var prod = _code.DatabaseQuerySafe(_conn,
                @"SELECT i.Product_ID AS PID,
                         SUM(i.Quantity) AS Qty,
                         SUM(i.Subtotal) AS Gross,
                         MAX(ISNULL(p.Product_Name, i.Product_Name)) AS Name,
                         MAX(p.Cost_Price) AS Cost
                    FROM Guest_Room_Service_Orders o
                    JOIN Guest_Room_Service_Items i ON i.Order_ID = o.ID
                    LEFT JOIN Product p ON p.ID = i.Product_ID
                   WHERE o.Acct_Post_Ref IS NULL
                     AND o.Order_Status NOT IN ('CANCELLED', 'PENDING')
                     AND CAST(o.Order_Date AS DATE) = @d
                     AND o.Payment_Method = @pm" + flagFilter + @"
                   GROUP BY i.Product_ID",
                new Dictionary<string, object> { { "@d", day.Date }, { "@pm", payMethod } });

            if (prod == null || prod.Rows.Count == 0)
            {
                // ไม่มีรายการสินค้า (ออเดอร์ว่าง/ข้อมูลไม่ครบ) → mark กันวนซ้ำ
                MarkRoomServiceRows(day, payMethod, postRef);
                return;
            }

            decimal itemsTotal = 0m;
            foreach (DataRow r in prod.Rows) itemsTotal += SafeDec(r["Gross"]);

            // ค่าบริการที่เก็บจากลูกค้า (PHASE18_21) — เป็นรายได้เพิ่มจากค่าสินค้า ต้องลงบัญชีด้วย
            decimal serviceChargeTotal = SumServiceCharge(day, payMethod);
            decimal grossTotal = itemsTotal + serviceChargeTotal;

            // ── รายได้ (เฉพาะออเดอร์ที่ลูกค้าจ่ายเอง) ──
            if (!chargeToRoom && grossTotal > 0m)
            {
                // กันซ้ำข้ามรอบ: ถ้าเคยสร้างใบสรุปของกลุ่มนี้แล้ว = รอบก่อน enqueue สำเร็จแต่ crash ก่อน mark
                if (ReceiptExists(postRef))
                {
                    MarkRoomServiceRows(day, payMethod, postRef);
                    return;
                }

                string paidHowName = MapPaymentMethodToPaidHow(payMethod);
                string paidHowAccId = _sync.LookupPaidHowAccountId(paidHowName);

                _sync.EnqueueReceipt(0, postRef, grossTotal, 0, day.Date,
                    $"รูมเซอร์วิส {day:dd/MM/yyyy} ({paidHowName})",
                    isDeposit: false, paymentMethod: paidHowName,
                    revenueType: "PRODUCT_REVENUE", paymentAccountId: paidHowAccId);

                // บรรทัดที่ 1 = ค่าสินค้า, บรรทัดที่ 2 = ค่าบริการ (ถ้ามี) → ยอดรวมของบรรทัด
                // ต้องเท่ากับยอดใบเสร็จ ไม่งั้น mapper จะเตือน line sum ≠ totalAmount
                CreateSummaryReceiptRow(postRef, 0, day, itemsTotal, paidHowName,
                    $"รูมเซอร์วิสสรุปรายวัน {day:dd/MM/yyyy} ({paidHowName})", "3",
                    serviceChargeTotal, "ค่าบริการรูมเซอร์วิส");
            }

            // ── ต้นทุน/สต๊อก (ทุกออเดอร์ รวมที่ลงบิลห้อง) ──
            foreach (DataRow r in prod.Rows)
            {
                int pid; int.TryParse(r["PID"]?.ToString(), out pid);
                decimal qty = SafeDec(r["Qty"]);
                decimal cost = SafeDec(r["Cost"]);
                string name = r["Name"]?.ToString() ?? "";
                if (pid > 0 && qty > 0 && cost > 0)
                {
                    _sync.EnqueueStockOutCogs(pid, name, qty, cost, day.Date,
                        chargeToRoom ? "รูมเซอร์วิส (ลงบิลห้อง)" : "รูมเซอร์วิส (จ่ายเอง)",
                        stockRef: $"RSDAY-COGS-{ds}-{methodKey}-{pid}");
                }
            }

            MarkRoomServiceRows(day, payMethod, postRef);
            Log($"RoomServiceRevenue: {day:yyyy-MM-dd} ({payMethod}) " +
                $"{(chargeToRoom ? "COGS อย่างเดียว (รายได้อยู่ในใบเสร็จห้อง)" : $"รายได้ {grossTotal:N2} (สินค้า {itemsTotal:N2} + ค่าบริการ {serviceChargeTotal:N2}) + COGS")} " +
                $"— {prod.Rows.Count} รายการ ref={postRef}");
        }

        // ตรวจครั้งเดียวต่อ process ว่าคอลัมน์ตั้งค่ารายสินค้า (PHASE18_24) มีหรือยัง
        private static int _rollupFlagState;   // 0 = ยังไม่ตรวจ, 1 = มี, -1 = ไม่มี
        private bool HasRollupFlagColumn()
        {
            if (_rollupFlagState != 0) return _rollupFlagState == 1;
            bool exists = false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 1 FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_NAME = 'Product' AND COLUMN_NAME = 'Include_In_Daily_Rollup'", null);
                exists = dt != null && dt.Rows.Count > 0;
            }
            catch { }
            _rollupFlagState = exists ? 1 : -1;
            return exists;
        }

        /// <summary>
        /// รวมค่าบริการ (Service Charge) ของกลุ่ม (วัน × วิธีจ่าย) ที่ยังไม่โพสต์.
        /// คอลัมน์มาจาก PHASE18_21 — ฐานที่ยังไม่อัปเดตคืน 0 (พฤติกรรมเดิม)
        /// </summary>
        private decimal SumServiceCharge(DateTime day, string payMethod)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT ISNULL(SUM(o.Service_Charge), 0)
                        FROM Guest_Room_Service_Orders o
                       WHERE o.Acct_Post_Ref IS NULL
                         AND o.Order_Status NOT IN ('CANCELLED', 'PENDING')
                         AND CAST(o.Order_Date AS DATE) = @d
                         AND o.Payment_Method = @pm",
                    new Dictionary<string, object> { { "@d", day.Date }, { "@pm", payMethod } });
                return dt?.Rows.Count > 0 ? SafeDec(dt.Rows[0][0]) : 0m;
            }
            catch { return 0m; }   // ยังไม่ได้รัน PHASE18_21
        }

        private void MarkRoomServiceRows(DateTime day, string payMethod, string postRef)
        {
            _code.DatabaseInsertSafe(_conn,
                @"UPDATE Guest_Room_Service_Orders
                     SET Acct_Post_Ref = @ref
                   WHERE Acct_Post_Ref IS NULL
                     AND Order_Status NOT IN ('CANCELLED', 'PENDING')
                     AND CAST(Order_Date AS DATE) = @d
                     AND Payment_Method = @pm",
                new Dictionary<string, object>
                {
                    { "@ref", postRef }, { "@d", day.Date }, { "@pm", payMethod }
                });
        }

        /// <summary>วิธีจ่ายของรูมเซอร์วิส → ชื่อแหล่งรับเงินใน Account_Paid_How (ใช้หาบัญชี NextAcc)</summary>
        private static string MapPaymentMethodToPaidHow(string paymentMethod)
        {
            switch ((paymentMethod ?? "").ToUpperInvariant())
            {
                case "CASH": return "เงินสด";
                case "TRANSFER": return "เงินโอน";
                default: return "เงินสด";
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // 2) รายได้ห้องจาก OTA
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// โพสต์รายได้ค่าห้องของการจอง OTA ที่เลยวันเช็คเอาท์แล้วและ "ไม่มีใบเสร็จในระบบ"
        /// (Dr ลูกหนี้ OTA / Cr รายได้ห้อง / Cr ภาษีขาย) — no-op ถ้าปิดสวิตช์.
        /// idempotent ด้วย `Reservation.Ota_Revenue_Ref`
        /// </summary>
        public void PostOtaRoomRevenueIfDue(int maxPerRun = 20)
        {
            if (!_config.IsConfigured || !_config.Enabled || !_config.IsOtaRoomRevenueEnabled) return;

            // ต้องบังคับขา Dr ให้เป็น "ลูกหนี้ OTA" ให้ได้ ไม่งั้น NextAcc จะลงเป็นเงินสด (ผิด — ยังไม่ได้รับเงิน)
            string arAccountId = ResolveRealAccountId("OTA_RECEIVABLE");
            if (string.IsNullOrEmpty(arAccountId))
            {
                LogOnce("OtaRoomRevenue_NoMapping",
                    "OtaRoomRevenue: ยังไม่ได้ map บัญชี 'ลูกหนี้ OTA' (OTA_RECEIVABLE) กับผังบัญชี NextAcc " +
                    "— ข้ามการโพสต์ทั้งหมด (ตั้งค่าที่ Accounting Integration → ผังบัญชี แล้วกด 'ดึง Chart of Accounts')");
                return;
            }
            if (!_config.CanUseCompanyEndpoints)
            {
                LogOnce("OtaRoomRevenue_NoCompanyKey",
                    "OtaRoomRevenue: ต้องเปิด company endpoints (ตั้ง Company ID + ใช้คีย์ที่เข้าถึง /api/companies ได้) " +
                    "ถึงจะบังคับให้ลงบัญชีลูกหนี้ OTA ได้ — ข้ามการโพสต์ (กันลงเป็นเงินสดผิด)");
                return;
            }

            try
            {
                // การจอง OTA ที่: เลยเช็คเอาท์แล้ว, ยังไม่โพสต์, ไม่ถูกยกเลิก, มียอด, และไม่มีใบเสร็จในระบบ
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP (@cap) r.ID, r.TotalPrice, r.CheckoutDate, r.OTA_Channel,
                             r.OTA_Booking_ID, r.OTA_Guest_Name, r.Customer_MobilePhone
                        FROM Reservation r
                       WHERE r.Ota_Revenue_Ref IS NULL
                         AND r.OTA_Channel IS NOT NULL AND LTRIM(RTRIM(r.OTA_Channel)) <> ''
                         AND r.CheckoutDate < CAST(GETDATE() AS DATE)
                         AND ISNULL(r.TotalPrice, 0) > 0
                         AND r.Status NOT IN (N'ยกเลิก', N'ไม่มาเช็คอิน')
                         AND NOT EXISTS (SELECT 1 FROM Account_Receipt ar WHERE ar.Reservation_ID = r.ID)
                       ORDER BY r.CheckoutDate",
                    new Dictionary<string, object> { { "@cap", maxPerRun } });

                if (dt == null || dt.Rows.Count == 0) return;

                foreach (DataRow r in dt.Rows)
                {
                    int resId = Convert.ToInt32(r["ID"]);
                    try { ProcessOneOtaReservation(r, resId, arAccountId); }
                    catch (Exception exOne)
                    {
                        Log($"OtaRoomRevenue: การจอง #{resId} ล้มเหลว: {exOne.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // คอลัมน์ OTA_* / Ota_Revenue_Ref ยังไม่มี (ยังไม่รัน migration) → เงียบไว้ครั้งเดียว
                LogOnce("OtaRoomRevenue_Error", "PostOtaRoomRevenueIfDue: " + ex.Message);
            }
        }

        private void ProcessOneOtaReservation(DataRow r, int resId, string arAccountId)
        {
            decimal gross = SafeDec(r["TotalPrice"]);
            if (gross <= 0m) { MarkOtaPosted(resId, "SKIP-ZERO"); return; }

            DateTime docDate = r["CheckoutDate"] != DBNull.Value
                ? Convert.ToDateTime(r["CheckoutDate"]) : DateTime.Today;
            string channel = r["OTA_Channel"]?.ToString() ?? "OTA";
            string bookingId = r["OTA_Booking_ID"]?.ToString() ?? "";
            string guest = r["OTA_Guest_Name"]?.ToString();
            if (string.IsNullOrWhiteSpace(guest)) guest = $"ลูกค้า {channel}";

            string docRef = $"OTA-{resId}";

            // กันซ้ำข้ามรอบ (เคย enqueue แล้วแต่ยังไม่ได้ mark)
            if (ReceiptExists(docRef)) { MarkOtaPosted(resId, docRef); return; }

            // Dr ลูกหนี้ OTA (ไม่ใช่เงินสด — เงินยังอยู่กับ Agoda/Booking จนกว่าจะปิดงวด payout)
            _sync.EnqueueReceipt(resId, docRef, gross, 0, docDate,
                $"{guest} ({channel}{(string.IsNullOrEmpty(bookingId) ? "" : " " + bookingId)})",
                isDeposit: false, paymentMethod: channel,
                revenueType: "ROOM_REVENUE", paymentAccountId: arAccountId);

            CreateSummaryReceiptRow(docRef, resId, docDate, gross, channel,
                $"ค่าห้องพัก {channel} การจอง #{resId}" + (string.IsNullOrEmpty(bookingId) ? "" : $" ({bookingId})"), "0");

            MarkOtaPosted(resId, docRef);
            Log($"OtaRoomRevenue: การจอง #{resId} {channel} {gross:N2} → ลูกหนี้ OTA (ref={docRef})");
        }

        private void MarkOtaPosted(int reservationId, string reference)
        {
            _code.DatabaseInsertSafe(_conn,
                "UPDATE Reservation SET Ota_Revenue_Ref = @ref WHERE ID = @id AND Ota_Revenue_Ref IS NULL",
                new Dictionary<string, object> { { "@ref", reference }, { "@id", reservationId } });
        }

        // ═══════════════════════════════════════════════════════════════════
        // helpers
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// หา Nexaacc AccountId ตัวจริงของ TakeTime code — ต้องเป็น GUID จริงจากผังบัญชี NextAcc เท่านั้น
        /// (AccountingDataMapper.GetAccountId อาจคืน GUID สังเคราะห์จาก MD5 เมื่อยังไม่ sync ผังบัญชี
        ///  ซึ่งส่งไป NextAcc ไม่ได้) → ไม่พบ = คืน null ให้ผู้เรียกข้ามการโพสต์ ดีกว่าโพสต์ผิดบัญชี
        /// </summary>
        private string ResolveRealAccountId(string takeTimeCode)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 Nexaacc_AccountId, Nexaacc_AccountCode
                        FROM Accounting_Account_Mapping
                       WHERE TakeTime_Code = @c AND Is_Active = 1",
                    new Dictionary<string, object> { { "@c", takeTimeCode } });
                if (dt == null || dt.Rows.Count == 0) return null;

                if (dt.Rows[0]["Nexaacc_AccountId"] != DBNull.Value)
                {
                    string id = dt.Rows[0]["Nexaacc_AccountId"].ToString();
                    if (!string.IsNullOrWhiteSpace(id) && Guid.TryParse(id, out var g) && g != Guid.Empty)
                        return id;
                }

                // มีแต่รหัสบัญชี → หา GUID จาก cache ผังบัญชีที่ sync มาจาก NextAcc
                string code = dt.Rows[0]["Nexaacc_AccountCode"]?.ToString();
                if (string.IsNullOrWhiteSpace(code)) return null;

                var acc = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 Nexaacc_AccountId FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @code",
                    new Dictionary<string, object> { { "@code", code } });
                if (acc?.Rows.Count > 0 && acc.Rows[0][0] != DBNull.Value)
                {
                    string id2 = acc.Rows[0][0].ToString();
                    if (Guid.TryParse(id2, out var g2) && g2 != Guid.Empty) return id2;
                }
            }
            catch { }
            return null;
        }

        private bool ReceiptExists(string receiptId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ID FROM Account_Receipt WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", receiptId } });
                return dt?.Rows.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// สร้างแถว Account_Receipt สรุป + บรรทัดรายการ (durable marker ว่า enqueue ครบแล้ว
        /// และให้เอกสารมีที่อ้างอิงในระบบ) — โครงเดียวกับใบรับเงินสดสรุปรายวันของ POS rollup.
        /// ล้มเหลวไม่กระทบการ enqueue (มี log)
        /// </summary>
        /// <param name="productTypeId">
        /// ชนิดรายการของบรรทัดใบเสร็จ — ตัวกำหนดบัญชีรายได้ปลายทาง:
        /// "3" = สินค้า (ใช้กับรูมเซอร์วิส เหมือนใบรวบยอดขายหน้าร้าน),
        /// "0" = ไม่ระบุชนิด → mapper คืน ROOM_REVENUE (ใช้กับค่าห้องจาก OTA)
        /// </param>
        /// <param name="extraLineAmount">ยอดบรรทัดที่ 2 (เช่น ค่าบริการ) — 0 = ไม่มีบรรทัดนี้</param>
        /// <param name="extraLineText">คำอธิบายบรรทัดที่ 2</param>
        private void CreateSummaryReceiptRow(string receiptId, int reservationId, DateTime docDate,
            decimal total, string paidTypeName, string lineText, string productTypeId,
            decimal extraLineAmount = 0m, string extraLineText = null)
        {
            try
            {
                decimal docTotal = total + (extraLineAmount > 0m ? extraLineAmount : 0m);
                bool useVat = BusinessUsesVat();
                decimal exVat = useVat ? Math.Round(docTotal / 1.07m, 2, MidpointRounding.AwayFromZero) : docTotal;
                decimal vat = useVat ? (docTotal - exVat) : 0m;

                _code.DatabaseInsertSafe(_conn,
                    "IF NOT EXISTS (SELECT 1 FROM [dbo].[Account_Receipt] WHERE [ID] = @ID) " +
                    "INSERT INTO [dbo].[Account_Receipt] " +
                    "([ID],[Reservation_ID],[Created_Date],[Total_Amount],[Vat],[Total_Amount_Exclude_Vat]," +
                    "[IsDeposit],[UseDeposit],[Paid_Type],[Status],[Created_By_ID],[Etax],[Customer_ID]) " +
                    "VALUES (@ID,@ResID,@CreatedDate,@TotalAmount,@Vat,@ExVat,0,0,@PaidType,'Normal',0,0,0)",
                    new Dictionary<string, object>
                    {
                        { "@ID", receiptId }, { "@ResID", reservationId.ToString() },
                        { "@CreatedDate", docDate.Date }, { "@TotalAmount", docTotal },
                        { "@Vat", vat }, { "@ExVat", exVat }, { "@PaidType", paidTypeName }
                    });

                _code.DatabaseInsertSafe(_conn,
                    "IF NOT EXISTS (SELECT 1 FROM [dbo].[Account_Receipt_Detail] WHERE [Receipt_ID] = @ReceiptID) " +
                    "INSERT INTO [dbo].[Account_Receipt_Detail] " +
                    "([Number],[Receipt_ID],[ProductType_ID],[Product_ID],[Product_Data],[Product_Amount]," +
                    "[Product_Unit],[Price_PerPeice],[Price_Amount]) " +
                    "VALUES (1,@ReceiptID,@PType,'0',@Data,1,N'ครั้ง',@Total,@Total)",
                    new Dictionary<string, object>
                    {
                        { "@ReceiptID", receiptId }, { "@PType", productTypeId },
                        { "@Data", lineText }, { "@Total", total }
                    });

                // บรรทัดค่าบริการ — แยกบรรทัดเพื่อให้เห็นในเอกสารและแยกบัญชีรายได้ได้
                // (ProductType 0 → mapper คืนบัญชีรายได้ตั้งต้น; ตั้ง SERVICE_CHARGE_REVENUE
                //  ในผังบัญชีเพื่อแยกบัญชีจริงได้ภายหลัง)
                if (extraLineAmount > 0m)
                {
                    _code.DatabaseInsertSafe(_conn,
                        "IF NOT EXISTS (SELECT 1 FROM [dbo].[Account_Receipt_Detail] " +
                        "               WHERE [Receipt_ID] = @ReceiptID AND [Number] = 2) " +
                        "INSERT INTO [dbo].[Account_Receipt_Detail] " +
                        "([Number],[Receipt_ID],[ProductType_ID],[Product_ID],[Product_Data],[Product_Amount]," +
                        "[Product_Unit],[Price_PerPeice],[Price_Amount]) " +
                        "VALUES (2,@ReceiptID,@PType,'0',@Data,1,N'ครั้ง',@Total,@Total)",
                        new Dictionary<string, object>
                        {
                            { "@ReceiptID", receiptId }, { "@PType", productTypeId },
                            { "@Data", extraLineText ?? "ค่าบริการ" }, { "@Total", extraLineAmount }
                        });
                }
            }
            catch (Exception ex)
            {
                Log($"CreateSummaryReceiptRow({receiptId}) ไม่สำเร็จ (ไม่กระทบการส่งบัญชี): {ex.Message}");
            }
        }

        private bool BusinessUsesVat()
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 Use_Vat FROM Business_Info", null);
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Use_Vat"] != DBNull.Value)
                {
                    string v = dt.Rows[0]["Use_Vat"].ToString();
                    return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return false;
        }

        private static string SanitizeKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return "NA";
            var chars = new List<char>();
            foreach (char c in s) if (char.IsLetterOrDigit(c)) chars.Add(char.ToUpperInvariant(c));
            return chars.Count == 0 ? "NA" : new string(chars.ToArray());
        }

        private static decimal SafeDec(object o)
        {
            if (o == null || o == DBNull.Value) return 0m;
            decimal d;
            return decimal.TryParse(o.ToString(), out d) ? d : 0m;
        }

        private void Log(string message)
        {
            try { _code.Logs(_conn, "RevenuePosting", message, "SYSTEM"); } catch { }
        }

        // กัน log ท่วมทุกรอบ timer สำหรับเงื่อนไขที่ยังไม่ได้ตั้งค่า (เตือนซ้ำได้ทุก 6 ชม.)
        private static readonly Dictionary<string, DateTime> _lastWarn = new Dictionary<string, DateTime>();
        private void LogOnce(string key, string message)
        {
            lock (_lastWarn)
            {
                DateTime last;
                if (_lastWarn.TryGetValue(key, out last) && (DateTime.Now - last).TotalHours < 6) return;
                _lastWarn[key] = DateTime.Now;
            }
            Log(message);
        }
    }
}
