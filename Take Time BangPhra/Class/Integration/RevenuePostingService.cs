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
    ///        ไปตัดลูกหนี้ตอนปิดงวด payout. เฉพาะ Channel Collect, ยอดตามอีเมลจอง (NET/GROSS),
    ///        ข้ามการจองที่ยกเลิก/ลบ/เลื่อนวัน และที่ค่าห้องออกใบเสร็จไปแล้ว
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
        /// โพสต์รายได้/ต้นทุนรูมเซอร์วิส — no-op ถ้าปิดสวิตช์ (Nexaacc_RoomServiceRevenue). เรียกจาก background timer.
        ///
        /// กติกา (ต.ค. 2026 — แก้รายได้หาย/รายได้เกินจากการตรวจ audit A ข้อ 6):
        ///  • นับเฉพาะออเดอร์ที่ "รับเงินแล้วจริง" (Payment_Status = PAID — โอนที่ตรวจสลิปแล้ว/เงินสดที่พนักงานกด
        ///    "ยืนยันรับเงินแล้ว"/จ่ายออนไลน์ผ่านเกตเวย์). สลิปที่ยังไม่ตรวจ/เงินสดที่ยังไม่เก็บ = ยังไม่ลง
        ///  • ลงบิลห้อง (CHARGE_TO_ROOM/CHARGED) = รายได้ไปกับใบเสร็จเช็คเอาท์ → ที่นี่ลงเฉพาะต้นทุน+สต๊อก
        ///  • นโยบายเอกสารขายเปิด (SalesDocumentPolicy) → แยกตามแหล่งเงินของออเดอร์:
        ///      IMMEDIATE = ใบรายออเดอร์ RS-{orderId} ทันทีที่รับเงิน · DAILY_ROLLUP = RSDAY-{yyyyMMdd}-{paidHowId}
        ///      หลังจบวัน · OFF = ไม่ออกเอกสาร (ต้นทุน/สต๊อกยังลง)
        ///    นโยบายปิด = แบบเดิม: รวบรายวันตามวิธีจ่าย (RSDAY-{yyyyMMdd}-{CASH|TRF|ONL})
        ///  • ออเดอร์ที่ยืนยันช้า (วันนั้นออกใบสรุปไปแล้ว) → ใบเสริม -S{n} — เดิมถูก mark ทิ้งเงียบ ๆ (รายได้หาย)
        ///  • ค่าบริการ (Service Charge) แยกบรรทัด → mapping SERVICE_CHARGE_REVENUE (ไม่ผูก = รายได้ขายสินค้าเหมือนเดิม)
        ///    และอยู่ในฐาน VAT เดียวกับค่าสินค้า
        ///  • ต้นทุน: ที่นี่เป็นแหล่งความจริงเดียว — ตัดสต๊อกในระบบ (Product_Out, Remark 'รูมเซอร์วิส') คู่กับ COGS ที่ส่ง
        ///    NextAcc ในรอบเดียวกัน (เดิมส่ง COGS แต่ไม่ตัดสต๊อกในระบบ → สต๊อกสองฝั่งไม่ตรง)
        /// idempotent: จองออเดอร์ด้วย Acct_Post_Ref = "~{ref}" ก่อน → ทำเอกสาร/ต้นทุน → ปิดเป็น "{ref}"
        /// (process ตายกลางทาง = รอบถัดไปทำต่อจาก "~" เอง; ใบ IF NOT EXISTS + dedup คิว)
        /// </summary>
        public void PostRoomServiceRevenueIfDue(int maxDaysPerRun = 14)
        {
            if (!_config.IsConfigured || !_config.Enabled || !_config.IsRoomServiceRevenueEnabled) return;

            try
            {
                // 0) งานที่จองไว้แล้วแต่ยังไม่จบ (process ตายกลางทาง)
                var stale = _code.DatabaseQuerySafe(_conn,
                    @"SELECT DISTINCT TOP 50 Acct_Post_Ref FROM Guest_Room_Service_Orders
                       WHERE Acct_Post_Ref LIKE '~%'", null);
                if (stale != null)
                    foreach (DataRow s in stale.Rows)
                    {
                        string claim = Convert.ToString(s[0]);
                        try { FinishRoomServiceBatch(claim.Substring(1)); }
                        catch (Exception exS) { Log($"RoomServiceRevenue: ทำต่อ {claim} ล้มเหลว: {exS.Message}"); }
                    }

                bool policy = ChannelPosting.PolicyEnabled(_conn);
                bool hasPaidDate = ChannelPosting.HasColumn(_conn, "Guest_Room_Service_Orders", "Paid_Date");
                string settleExpr = hasPaidDate ? "COALESCE(o.Paid_Date, o.Order_Date)" : "o.Order_Date";

                var orders = _code.DatabaseQuerySafe(_conn,
                    $@"SELECT TOP 500 o.ID, o.Order_Date, {settleExpr} AS Settle_Date,
                              ISNULL(o.Payment_Method, '') AS PM, ISNULL(o.Payment_Status, '') AS PS
                         FROM Guest_Room_Service_Orders o
                        WHERE o.Acct_Post_Ref IS NULL
                          AND o.Order_Status NOT IN ('CANCELLED', 'PENDING')
                          AND (o.Payment_Status IN ('PAID', 'CHARGED') OR o.Payment_Method = 'CHARGE_TO_ROOM')
                        ORDER BY {settleExpr}, o.ID", null);
                if (orders == null || orders.Rows.Count == 0) return;

                DateTime today = DateTime.Today;
                var batches = new Dictionary<string, RsBatch>();
                var order = new List<string>();
                foreach (DataRow r in orders.Rows)
                {
                    long id = Convert.ToInt64(r["ID"]);
                    string pm = Convert.ToString(r["PM"]).Trim().ToUpperInvariant();
                    string ps = Convert.ToString(r["PS"]).Trim().ToUpperInvariant();
                    DateTime orderDay = Convert.ToDateTime(r["Order_Date"]).Date;
                    DateTime settleDay = Convert.ToDateTime(r["Settle_Date"]).Date;
                    bool chargeToRoom = ps != "PAID" && (pm == "CHARGE_TO_ROOM" || ps == "CHARGED");

                    string key; string stem; DateTime day; string groupKey;
                    if (chargeToRoom)
                    {
                        if (orderDay >= today) continue;               // ต้นทุนลงบิลห้อง รวบหลังจบวันเหมือนเดิม
                        key = "C|" + orderDay.ToString("yyyyMMdd"); stem = "RSCTR"; day = orderDay; groupKey = "0";
                    }
                    else
                    {
                        int paidHowId; string paidHowName;
                        ResolveRoomServicePaidHow(id, pm, out paidHowId, out paidHowName);
                        string mode = ChannelSalesPostingService.ModeRollup, reason;
                        if (policy)
                            ChannelPosting.ResolveMode(_conn, SalesDocumentPolicy.ChRoomService, paidHowId, out mode, out reason);
                        groupKey = policy && paidHowId > 0 ? paidHowId.ToString() : LegacyMethodKey(pm);

                        if (mode == ChannelSalesPostingService.ModeImmediate)
                        {
                            key = "I|" + id; stem = null; day = settleDay;
                        }
                        else if (mode == ChannelSalesPostingService.ModeOff)
                        {
                            if (settleDay >= today) continue;
                            key = "O|" + settleDay.ToString("yyyyMMdd") + "|" + groupKey; stem = "RSOFF"; day = settleDay;
                        }
                        else
                        {
                            if (settleDay >= today) continue;            // ใบสรุปรายวัน = หลังจบวันเท่านั้น
                            key = "R|" + settleDay.ToString("yyyyMMdd") + "|" + groupKey; stem = "RSDAY"; day = settleDay;
                        }
                    }

                    RsBatch b;
                    if (!batches.TryGetValue(key, out b))
                    {
                        if (batches.Count >= Math.Max(1, maxDaysPerRun) * 4) continue;   // กันรอบเดียวทำงานหนักเกิน
                        b = new RsBatch { Stem = stem, Day = day, GroupKey = groupKey, OrderId = stem == null ? id : 0 };
                        batches[key] = b;
                        order.Add(key);
                    }
                    b.Ids.Add(id);
                }

                foreach (string k in order)
                {
                    RsBatch b = batches[k];
                    try
                    {
                        string refId = b.Stem == null
                            ? "RS-" + b.OrderId
                            : ChannelPosting.NextFreeRollupRef(_conn, b.Stem, b.Day, b.GroupKey, RoomServiceRefUsed);
                        if (string.IsNullOrEmpty(refId) || (b.Stem == null && RoomServiceRefUsed(refId)))
                        {
                            Log($"RoomServiceRevenue: หาเลขเอกสารว่างไม่ได้ ({k}) — ข้าม");
                            continue;
                        }
                        if (ClaimRoomServiceOrders(b.Ids, refId) <= 0) continue;
                        FinishRoomServiceBatch(refId);
                    }
                    catch (Exception exB)
                    {
                        Log($"RoomServiceRevenue: กลุ่ม {k} ล้มเหลว: {exB.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log("PostRoomServiceRevenueIfDue: " + ex.Message);
            }
        }

        private sealed class RsBatch
        {
            public string Stem;          // RSDAY / RSOFF / RSCTR — null = IMMEDIATE (RS-{orderId})
            public DateTime Day;
            public string GroupKey;
            public long OrderId;
            public readonly List<long> Ids = new List<long>();
        }

        /// <summary>เลขนี้ถูกใช้กับออเดอร์ไปแล้วหรือยัง (ทั้งที่จบแล้วและที่จองค้าง "~")</summary>
        private bool RoomServiceRefUsed(string refId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 1 FROM Guest_Room_Service_Orders WHERE Acct_Post_Ref IN (@r, @c)",
                    new Dictionary<string, object> { { "@r", refId }, { "@c", "~" + refId } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>จองออเดอร์ (เฉพาะที่ยังว่าง) ด้วย "~{ref}" — คืนจำนวนที่จองได้</summary>
        private int ClaimRoomServiceOrders(List<long> ids, string refId)
        {
            if (ids == null || ids.Count == 0) return 0;
            int total = 0;
            for (int start = 0; start < ids.Count; start += 200)
            {
                var p = new Dictionary<string, object> { { "@c", "~" + refId } };
                var names = new List<string>();
                for (int i = start; i < ids.Count && i < start + 200; i++)
                {
                    string n = "@i" + (i - start);
                    names.Add(n);
                    p[n] = ids[i];
                }
                total += _code.DatabaseInsertSafe(_conn,
                    "UPDATE Guest_Room_Service_Orders SET Acct_Post_Ref = @c WHERE Acct_Post_Ref IS NULL AND ID IN (" +
                    string.Join(",", names) + ")", p);
            }
            return total;
        }

        /// <summary>
        /// ออกเอกสาร + ต้นทุนของออเดอร์ที่จองด้วย "~{refId}" แล้วปิดเป็น "{refId}" — ชนิดดูจากคำนำหน้า:
        /// RS- = รายออเดอร์ · RSDAY/RSDAY(ย่อ) = สรุปรายวัน · RSOFF = นโยบายไม่ออกเอกสาร · RSCTR = ลงบิลห้อง (ต้นทุนอย่างเดียว)
        /// เรียกซ้ำได้ (ใบ IF NOT EXISTS, คิว dedup ตามเลขใบ/stockRef, Product_Out IF NOT EXISTS)
        /// </summary>
        private void FinishRoomServiceBatch(string refId)
        {
            string claim = "~" + refId;
            bool revenueDoc = refId.StartsWith("RS-", StringComparison.Ordinal) || refId.StartsWith("RSDAY", StringComparison.Ordinal);
            bool chargeToRoom = refId.StartsWith("RSCTR", StringComparison.Ordinal);
            bool hasPaidDate = ChannelPosting.HasColumn(_conn, "Guest_Room_Service_Orders", "Paid_Date");
            bool hasSvc = ChannelPosting.HasColumn(_conn, "Guest_Room_Service_Orders", "Service_Charge");
            bool hasFlag = HasRollupFlagColumn();
            string settleExpr = hasPaidDate ? "COALESCE(o.Paid_Date, o.Order_Date)" : "o.Order_Date";

            // ออเดอร์ในกลุ่ม + ส่วนแบ่งค่าบริการ (ค่าบริการของออเดอร์ตามสัดส่วนสินค้าที่ "รวมในใบสรุป")
            var heads = _code.DatabaseQuerySafe(_conn,
                $@"SELECT o.ID, o.Order_Number, {settleExpr} AS Settle_Date, ISNULL(o.Payment_Method, '') AS PM,
                          o.Customer_MobilePhone AS Phone,
                          {(hasSvc ? "ISNULL(o.Service_Charge, 0)" : "CAST(0 AS DECIMAL(18,2))")} AS SC,
                          ISNULL((SELECT SUM(i.Subtotal) FROM Guest_Room_Service_Items i WHERE i.Order_ID = o.ID), 0) AS ItemsAll,
                          ISNULL((SELECT SUM(i.Subtotal) FROM Guest_Room_Service_Items i
                                   LEFT JOIN Product p ON p.ID = i.Product_ID
                                  WHERE i.Order_ID = o.ID{(hasFlag ? " AND ISNULL(p.Include_In_Daily_Rollup, 1) = 1" : "")}), 0) AS ItemsIncl
                     FROM Guest_Room_Service_Orders o
                    WHERE o.Acct_Post_Ref = @c
                    ORDER BY o.ID",
                new Dictionary<string, object> { { "@c", claim } });
            if (heads == null || heads.Rows.Count == 0) return;

            DataRow first = heads.Rows[0];
            DateTime docDate = Convert.ToDateTime(first["Settle_Date"]).Date;
            decimal scTotal = 0m;
            foreach (DataRow h in heads.Rows)
            {
                decimal sc = SafeDec(h["SC"]), all = SafeDec(h["ItemsAll"]), incl = SafeDec(h["ItemsIncl"]);
                if (sc <= 0m) continue;
                scTotal += all > 0m ? Math.Round(sc * incl / all, 2, MidpointRounding.AwayFromZero) : sc;
            }

            // ยอดต่อสินค้า (ราคาขาย + ต้นทุนจาก Product.Cost_Price + สวิตช์รวมใบสรุปรายสินค้า)
            var prod = _code.DatabaseQuerySafe(_conn,
                $@"SELECT i.Product_ID AS PID,
                          SUM(i.Quantity) AS Qty,
                          SUM(i.Subtotal) AS Gross,
                          MAX(ISNULL(p.Product_Name, i.Product_Name)) AS Name,
                          MAX(p.Cost_Price) AS Cost,
                          MAX({(hasFlag ? "CASE WHEN ISNULL(p.Include_In_Daily_Rollup, 1) = 1 THEN 1 ELSE 0 END" : "1")}) AS Incl
                     FROM Guest_Room_Service_Orders o
                     JOIN Guest_Room_Service_Items i ON i.Order_ID = o.ID
                     LEFT JOIN Product p ON p.ID = i.Product_ID
                    WHERE o.Acct_Post_Ref = @c
                    GROUP BY i.Product_ID",
                new Dictionary<string, object> { { "@c", claim } });

            decimal itemsIncluded = 0m;
            if (prod != null)
                foreach (DataRow r in prod.Rows)
                    if (Convert.ToInt32(r["Incl"]) == 1) itemsIncluded += SafeDec(r["Gross"]);

            // ── รายได้ ──
            decimal grossTotal = itemsIncluded + scTotal;
            if (revenueDoc && grossTotal > 0m)
            {
                bool single = refId.StartsWith("RS-", StringComparison.Ordinal);
                string pm = Convert.ToString(first["PM"]).Trim().ToUpperInvariant();
                int paidHowId; string paidHowName;
                ResolveRoomServicePaidHow(Convert.ToInt64(first["ID"]), pm, out paidHowId, out paidHowName);

                string scKey = scTotal > 0m ? ChannelPosting.ResolveRevenueKey(_conn, ChannelPosting.RevenueChain("SERVICE_CHARGE", null)) : null;
                string itemsType = ChannelPosting.ProductTypeIdForRevenueKey(_conn, "PRODUCT_REVENUE") ?? "3";
                string headText = single
                    ? $"รูมเซอร์วิส ออเดอร์ {first["Order_Number"]} ({paidHowName})"
                    : $"รูมเซอร์วิสสรุปรายวัน {docDate:dd/MM/yyyy} ({paidHowName})" + (refId.Contains("-S") ? " — ใบเสริม (ยืนยันรับเงินช้า)" : "");

                var lines = new List<ChannelReceiptLine>();
                if (itemsIncluded > 0m)
                    lines.Add(new ChannelReceiptLine { Text = headText, Amount = itemsIncluded, ProductTypeId = itemsType });
                if (scTotal > 0m)
                    lines.Add(new ChannelReceiptLine
                    {
                        Text = "ค่าบริการรูมเซอร์วิส (Service Charge)",
                        Amount = scTotal,
                        // ไม่ระบุชนิด → ใช้ revenueType ของใบ = SERVICE_CHARGE_REVENUE (ถ้าผูกบัญชีแล้ว)
                        ProductTypeId = scKey == "PRODUCT_REVENUE" ? itemsType : null
                    });

                ChannelPosting.WriteSummaryReceipt(_conn, refId, docDate, paidHowName, lines);

                string accId = ChannelPosting.PaidHowAccountId(_conn, paidHowId) ?? _sync.LookupPaidHowAccountId(paidHowName);
                long q = _sync.EnqueueReceipt(0, refId, grossTotal, 0, docDate, headText,
                    isDeposit: false, paymentMethod: paidHowName,
                    revenueType: scKey ?? "PRODUCT_REVENUE", paymentAccountId: accId,
                    customerPhone: single ? Convert.ToString(first["Phone"]) : null);
                if (q <= 0)
                {
                    // คงสถานะจอง "~" ไว้ — รอบถัดไปทำต่อ (ไม่ทิ้งรายได้)
                    LogOnce("rs-enq-" + refId, $"RoomServiceRevenue: {refId} เข้าคิวเอกสารไม่สำเร็จ ({q}) — จะลองใหม่รอบถัดไป");
                    return;
                }
            }

            // ── ต้นทุน + สต๊อกในระบบ (ทุกออเดอร์ รวมที่ลงบิลห้อง/นโยบายไม่ออกเอกสาร) ──
            string why = chargeToRoom ? "รูมเซอร์วิส (ลงบิลห้อง)" : "รูมเซอร์วิส (จ่ายเอง)";
            DateTime outDate = docDate;
            if (prod != null)
                foreach (DataRow r in prod.Rows)
                {
                    int pid; int.TryParse(Convert.ToString(r["PID"]), out pid);
                    decimal qty = SafeDec(r["Qty"]);
                    decimal cost = SafeDec(r["Cost"]);
                    decimal gross = SafeDec(r["Gross"]);
                    string name = Convert.ToString(r["Name"]);
                    if (pid <= 0 || qty <= 0) continue;

                    WriteRoomServiceStockOut(refId, pid, qty, qty > 0 ? Math.Round(gross / qty, 2) : 0m, outDate);
                    if (cost > 0 && Convert.ToInt32(r["Incl"]) == 1)
                        _sync.EnqueueStockOutCogs(pid, name, qty, cost, outDate, why, stockRef: $"RSCOGS-{refId}-{pid}");
                }

            _code.DatabaseInsertSafe(_conn,
                "UPDATE Guest_Room_Service_Orders SET Acct_Post_Ref = @r WHERE Acct_Post_Ref = @c",
                new Dictionary<string, object> { { "@r", refId }, { "@c", claim } });

            Log($"RoomServiceRevenue: {refId} — {heads.Rows.Count} ออเดอร์ " +
                (revenueDoc ? $"รายได้ {grossTotal:N2} (สินค้า {itemsIncluded:N2} + ค่าบริการ {scTotal:N2}) + " : chargeToRoom ? "ลงบิลห้อง: " : "นโยบายไม่ออกเอกสาร: ") +
                $"ต้นทุน/สต๊อก {prod?.Rows.Count ?? 0} สินค้า");
        }

        /// <summary>
        /// ตัดสต๊อกในระบบ (Product_Out) ของรูมเซอร์วิส — Remark 'รูมเซอร์วิส' + Account_Receipt_ID = เลขเอกสาร
        /// (ไม่เข้าเงื่อนไขรวบยอดขาย POS: ต้อง Remark 'ขาย' + Account_Receipt_ID '0') · IF NOT EXISTS กันซ้ำ
        /// </summary>
        private void WriteRoomServiceStockOut(string refId, int productId, decimal qty, decimal pricePerUnit, DateTime outDate)
        {
            try
            {
                _code.DatabaseInsertSafe(_conn,
                    @"IF NOT EXISTS (SELECT 1 FROM Product_Out WHERE Account_Receipt_ID = @r AND Product_ID = @pid AND Remark = N'รูมเซอร์วิส')
                      INSERT INTO Product_Out (DateTime_Out, Product_ID, Amount, PricePerUnit, Account_Receipt_ID, Remark)
                      VALUES (@dt, @pid, @qty, @price, @r, N'รูมเซอร์วิส')",
                    new Dictionary<string, object>
                    {
                        { "@r", refId }, { "@pid", productId }, { "@qty", qty }, { "@price", pricePerUnit },
                        { "@dt", outDate.Date.AddHours(12) }
                    });
            }
            catch (Exception ex)
            {
                Log($"RoomServiceRevenue: ตัดสต๊อกในระบบ {refId} สินค้า #{productId} ไม่สำเร็จ: {ex.Message}");
            }
        }

        /// <summary>คีย์วิธีจ่ายแบบเดิม (สั้นพอให้ RSDAY-{yyyyMMdd}-{key}-S{n} ยาวไม่เกินคอลัมน์เลขใบ)</summary>
        private static string LegacyMethodKey(string pm)
        {
            switch ((pm ?? "").ToUpperInvariant())
            {
                case "CASH": return "CASH";
                case "TRANSFER": return "TRF";
                case "ONLINE": return "ONL";
                default: return SanitizeKey(pm).Length > 4 ? SanitizeKey(pm).Substring(0, 4) : SanitizeKey(pm);
            }
        }

        /// <summary>
        /// แหล่งเงินของออเดอร์: ONLINE = แหล่งเงินของเกตเวย์ที่ตัดเงินจริง (Payment_Transaction ของออเดอร์ →
        /// PaymentChannelCatalog.ResolveRecordingPaidHowName เดียวกับค่าห้อง) · อื่น ๆ = ชื่อเดิม (เงินสด/เงินโอน)
        /// แล้วหา Account_Paid_How.ID (ไม่พบตามชื่อ → แถวแรกตามชนิดช่องทาง CASH/TRANSFER)
        /// </summary>
        private void ResolveRoomServicePaidHow(long orderId, string pm, out int paidHowId, out string paidHowName)
        {
            paidHowName = null;
            string up = (pm ?? "").ToUpperInvariant();
            if (up == "ONLINE")
            {
                try
                {
                    var t = _code.DatabaseQuerySafe(_conn,
                        @"SELECT TOP 1 Provider, Method, Card_Brand FROM Payment_Transaction
                           WHERE Source_Type = 'ROOMSERVICE' AND Source_ID = @id AND [Status] = 'PAID' ORDER BY ID DESC",
                        new Dictionary<string, object> { { "@id", orderId.ToString() } });
                    if (t != null && t.Rows.Count > 0)
                        paidHowName = Take_Time_BangPhra.Payments.PaymentChannelCatalog.ResolveRecordingPaidHowName(_conn,
                            Convert.ToString(t.Rows[0]["Provider"]), Convert.ToString(t.Rows[0]["Method"]),
                            t.Rows[0]["Card_Brand"] == DBNull.Value ? null : Convert.ToString(t.Rows[0]["Card_Brand"]));
                }
                catch { }
            }
            if (string.IsNullOrWhiteSpace(paidHowName)) paidHowName = MapPaymentMethodToPaidHow(pm);
            paidHowId = ChannelPosting.FindPaidHowId(_conn, paidHowName);
            if (paidHowId <= 0 && (up == "CASH" || up == "TRANSFER"))
            {
                string n;
                int id = ChannelPosting.PaidHowByType(_conn, up, paidHowName, out n);
                if (id > 0) { paidHowId = id; paidHowName = n; }
            }
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

        // ═══════════════════════════════════════════════════════════════════
        // 1b) รายได้ช่องทางอื่นนอก POS (กิจกรรม / เบิกของใช้ / เกตเวย์หน้าร้าน / ค่าเสียหาย)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// ส่งรายการค้างของสมุด Sales_Channel_Posting (IMMEDIATE ที่ยังไม่เข้าคิว, ใบสรุปรายวัน, ตาข่ายเงินเกตเวย์หน้าร้าน)
        /// — no-op ถ้าไม่มีตาราง/นโยบายเอกสารขายยังไม่เปิด. ดู ChannelSalesPostingService
        /// </summary>
        public void PostChannelSalesIfDue()
        {
            new ChannelSalesPostingService(_conn).PostDueIfAny();
        }

        /// <summary>วิธีจ่ายของรูมเซอร์วิส → ชื่อแหล่งรับเงินใน Account_Paid_How (ใช้หาบัญชี NextAcc)</summary>
        private string MapPaymentMethodToPaidHow(string paymentMethod)
        {
            switch ((paymentMethod ?? "").ToUpperInvariant())
            {
                case "CASH": return "เงินสด";
                case "TRANSFER": return "เงินโอน";
                // จ่ายออนไลน์ผ่านเกตเวย์ → แหล่งเงินของเกตเวย์ (ผูกบัญชีพักเงินใน NextAcc)
                // ต้องตรงกับชื่อแถวใน Account_Paid_How เป๊ะ ๆ — ตั้งทับได้ที่ Payment_PaidHow_Name
                // หรือรายผู้ให้บริการที่ Accounting Integration (Nexaacc_Gateway_PaidHow_{PROVIDER}, PHASE19_21) —
                // ออเดอร์ไม่ได้เก็บว่าจ่ายผ่านเจ้าไหน → ใช้เกตเวย์ที่เปิดใช้อยู่ (ใบสรุปรายวันรวบตามวัน)
                case "ONLINE":
                {
                    string fallback = Take_Time_BangPhra.Payments.PaymentGatewayConfig
                        .Get("Payment_PaidHow_Name", "Omise (จ่ายออนไลน์)");
                    string provider = null;
                    try { provider = Take_Time_BangPhra.Payments.PaymentGatewayConfig.ActiveProvider; } catch { }
                    return AccountingSyncService.ResolveGatewayPaidHowName(_conn, provider, null, fallback);
                }
                default: return "เงินสด";
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // 2) รายได้ห้องจาก OTA
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// โพสต์รายได้ค่าห้องของการจอง OTA แบบ **Channel Collect** ที่เลยวันเช็คเอาท์แล้วและค่าห้อง
        /// "ยังไม่ถูกรับรู้ด้วยใบเสร็จที่ยังใช้งาน" (Dr ลูกหนี้ OTA / Cr รายได้ห้อง / Cr ภาษีขาย) — no-op ถ้าปิดสวิตช์
        /// (Nexaacc_OtaRoomRevenue, ค่าเริ่มต้นปิด).
        /// ยอด = ยอดตามอีเมลจอง (ฐาน NET/GROSS ตาม Nexaacc_OtaRevenue_Basis) — ไม่ใช้ TotalPrice.
        /// idempotent ด้วย `Reservation.Ota_Revenue_Ref`
        /// </summary>
        public void PostOtaRoomRevenueIfDue(int maxPerRun = 20)
        {
            // โหมดเอกสาร OTA (Nexaacc_OtaDocument_Mode = RECEIPT_DOC) เปิดตัวเดียวก็ทำงาน และ "ชนะ" โหมดเดิม:
            // การจองหนึ่งรายการถูกโพสต์ทางเดียวเสมอ (marker เดียวกัน Reservation.Ota_Revenue_Ref)
            bool docMode = _config.IsOtaDocumentMode;
            if (!_config.IsConfigured || !_config.Enabled || (!_config.IsOtaRoomRevenueEnabled && !docMode)) return;

            // ต้องบังคับขา Dr ให้เป็น "ลูกหนี้ OTA" ให้ได้ ไม่งั้น NextAcc จะลงเป็นเงินสด (ผิด — ยังไม่ได้รับเงิน)
            // โหมดเอกสาร: บัญชีรายช่องทาง (Accounting_Ota_Channel_Map) มาก่อน — OTA_RECEIVABLE เป็นแค่ค่าสำรอง
            string arAccountId = ResolveRealAccountId("OTA_RECEIVABLE");
            if (string.IsNullOrEmpty(arAccountId) && !docMode)
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

            // ฐานรายได้ OTA — "นโยบายบัญชี" ให้ผู้ทำบัญชีเลือก (Accounting_Integration_Config.Nexaacc_OtaRevenue_Basis):
            //   NET   (ค่าเริ่มต้น) = agency model — โรงแรมรับรู้รายได้เท่ายอดสุทธิที่ OTA จะโอนให้ (หลังหักคอมมิชชั่น)
            //                        ใช้ ReservationBalance.OtaAmount (ยอดตามอีเมลจอง อิง OTA_Net_Amount)
            //   GROSS              = merchant model — รับรู้รายได้เต็มราคาขายที่ลูกค้าจ่าย OTA (OTA_Gross_Amount)
            //                        คอมมิชชั่นไปลงค่าใช้จ่ายตอนปิดงวด payout
            // ไม่มียอดตามฐานที่เลือก → ข้าม + log (ไม่ถอยไปใช้ TotalPrice ที่พนักงานแก้ได้)
            bool grossBasis = string.Equals(
                (ReadIntegrationConfig("Nexaacc_OtaRevenue_Basis", "NET") ?? "NET").Trim(),
                "GROSS", StringComparison.OrdinalIgnoreCase);

            try
            {
                // การจอง OTA ที่: เลยเช็คเอาท์แล้ว, ยังไม่โพสต์ (Ota_Revenue_Ref IS NULL — รวม LEGACY / RECLASS-… ที่ตั้งไว้แล้ว),
                // ไม่ถูกยกเลิก/ลบ/เลื่อนวัน/ไม่มาเช็คอิน, และ "ค่าห้องยังไม่ถูกรับรู้ด้วยใบเสร็จ" —
                //   ใบเสร็จที่ถือว่ารับรู้ค่าห้องแล้ว = ใบที่ยังใช้งาน (Status Normal/NULL), ไม่ใช่ใบมัดจำ, และมีบรรทัด
                //   ค่าห้อง (ProductType_ID = 1) ยอดบวกที่ไม่ใช่บรรทัด "ส่วนลด…"
                //   (ใบเสร็จเฉพาะค่าใช้จ่ายเสริม / ใบที่ยกเลิกแล้ว ไม่บล็อกรายได้ค่าห้องอีกต่อไป)
                // หมายเหตุ: whereSql นี้เขียนในโค้ด (ไม่มีค่าจากผู้ใช้) — ใช้ร่วมกับ ReservationBalance.LoadMany (alias r)
                const string candidateWhere = @"r.Ota_Revenue_Ref IS NULL
                         AND r.OTA_Channel IS NOT NULL AND LTRIM(RTRIM(r.OTA_Channel)) <> ''
                         AND r.CheckoutDate < CAST(GETDATE() AS DATE)
                         AND ISNULL(r.TotalPrice, 0) > 0
                         AND r.Status NOT LIKE N'ยกเลิก%'
                         AND r.Status NOT LIKE N'ลบ%'
                         AND r.Status <> N'ไม่มาเช็คอิน'";

                // ค่าห้องที่ใบเสร็จรับรู้ไปแล้ว "สุทธิ" = Σ บรรทัด ProductType 1 รวมบรรทัด "ส่วนลด" (ยอดติดลบ, ProductType 1)
                //   ใบเช็คอิน Channel Collect ที่เพิ่มคืนหลังจอง: ค่าห้อง 2,156 + ส่วนลด (OTA จ่ายแล้ว) −1,619.01 = 537
                //   = รับรู้เฉพาะส่วนที่ลูกค้าจ่ายหน้างาน → ส่วน OTA ยังต้องโพสต์ (เดิม "มีบรรทัดค่าห้องบวก" = ข้ามตลอดไป)
                //   ใบเสร็จเงินสดปลอมรุ่นเก่า (ค่าห้องเต็ม ไม่มีส่วนลด) → สุทธิ ≈ ราคาห้อง → ข้าม (รับรู้ไปแล้ว)
                const string receiptRoomNetSql = @"(SELECT ISNULL(SUM(TRY_CONVERT(DECIMAL(18, 2), ard.Price_Amount)), 0)
                                 FROM Account_Receipt ar
                                 JOIN Account_Receipt_Detail ard ON ard.Receipt_ID = ar.ID
                                WHERE TRY_CONVERT(INT, ar.Reservation_ID) = r.ID
                                  AND (ar.Status = 'Normal' OR ar.Status IS NULL)
                                  AND ISNULL(ar.IsDeposit, 0) = 0
                                  AND TRY_CONVERT(INT, ard.ProductType_ID) = 1)";

                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT r.ID, r.CheckoutDate, r.OTA_Channel, r.OTA_Booking_ID, r.OTA_Guest_Name,
                             r.OTA_Net_Amount, r.OTA_Gross_Amount,
                             " + receiptRoomNetSql + @" AS ReceiptRoomNet
                        FROM Reservation r
                       WHERE " + candidateWhere + @"
                       ORDER BY r.CheckoutDate, r.ID",
                    null);

                if (dt == null || dt.Rows.Count == 0) return;

                // ใครเก็บเงินค่าห้อง + ยอดตามอีเมลจอง — สูตรเดียวกับทั้งระบบ (ReservationBalance)
                Dictionary<int, Take_Time_BangPhra.ReservationBalance> balances =
                    Take_Time_BangPhra.ReservationBalance.LoadMany(_conn, candidateWhere, null);

                int posted = 0;
                foreach (DataRow r in dt.Rows)
                {
                    if (posted >= maxPerRun) break;
                    int resId = Convert.ToInt32(r["ID"]);
                    try
                    {
                        decimal amount;
                        if (!TryResolveOtaRevenueAmount(r, resId, balances, grossBasis, out amount)) continue;

                        // ใบเสร็จรับรู้ส่วนของ OTA ไปแล้วหรือยัง: สุทธิค่าห้องในใบเสร็จ > (ราคาห้อง − ยอด OTA) = รวมส่วน OTA แล้ว
                        Take_Time_BangPhra.ReservationBalance bRcpt;
                        decimal roomTotal = balances.TryGetValue(resId, out bRcpt) && bRcpt != null ? bRcpt.RoomTotal : 0m;
                        decimal receiptRoomNet = SafeDec(r["ReceiptRoomNet"]);
                        decimal otaPortionLimit = Math.Max(0m, roomTotal - amount) + Take_Time_BangPhra.ReservationBalance.RoundingTolerance;
                        if (receiptRoomNet > otaPortionLimit)
                        {
                            LogOnce("OtaRoomRevenue_Receipted_" + resId,
                                $"OtaRoomRevenue: การจอง #{resId} ใบเสร็จรับรู้ค่าห้องสุทธิ {receiptRoomNet:N2} แล้ว " +
                                $"(ราคาห้อง {roomTotal:N2}, ยอด OTA {amount:N2}) — ข้าม กันรายได้ซ้ำ");
                            continue;
                        }
                        if (docMode)
                        {
                            if (ProcessOneOtaDocument(r, resId, amount, grossBasis ? "GROSS" : "NET", arAccountId))
                                posted++;
                        }
                        else
                        {
                            ProcessOneOtaReservation(r, resId, amount, grossBasis ? "GROSS" : "NET", arAccountId);
                            posted++;
                        }
                    }
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

        /// <summary>
        /// ตัดสินว่าการจองนี้โพสต์ได้ไหม และยอดเท่าไร — โพสต์เฉพาะ Channel Collect (OTA เก็บเงินแทนโรงแรม)
        /// Hotel Collect / ไม่ทราบ (NONE/UNKNOWN) → ข้าม: เงินค่าห้องเก็บที่หน้าเคาน์เตอร์ ต้องมาทางใบเสร็จ
        /// ไม่ใช่ลูกหนี้ OTA (ไม่งั้นเกิดลูกหนี้ OTA ที่ไม่มีวันได้รับโอน)
        /// ไม่ mark Ota_Revenue_Ref ตอนข้าม — ถ้าแก้ข้อมูลการจองภายหลัง (เช่น ระบุโหมดเก็บเงิน) รอบถัดไปจะโพสต์ได้เอง
        /// </summary>
        private bool TryResolveOtaRevenueAmount(DataRow r, int resId,
            Dictionary<int, Take_Time_BangPhra.ReservationBalance> balances, bool grossBasis, out decimal amount)
        {
            amount = 0m;

            Take_Time_BangPhra.ReservationBalance b;
            if (balances == null || !balances.TryGetValue(resId, out b) || b == null)
            {
                LogOnce("OtaRoomRevenue_NoBal_" + resId,
                    $"OtaRoomRevenue: การจอง #{resId} โหลดยอด (ReservationBalance) ไม่ได้ — ข้าม");
                return false;
            }

            if (!b.IsChannelCollect)
            {
                LogOnce("OtaRoomRevenue_NotChannel_" + resId,
                    $"OtaRoomRevenue: การจอง #{resId} โหมดเก็บเงิน = {b.CollectMode} (ไม่ใช่ Channel Collect) " +
                    "— ข้าม ไม่ตั้งลูกหนี้ OTA (ค่าห้องต้องรับรู้ผ่านใบเสร็จหน้าเคาน์เตอร์)");
                return false;
            }

            decimal net = SafeDec(r["OTA_Net_Amount"]);
            decimal gross = SafeDec(r["OTA_Gross_Amount"]);

            if (grossBasis)
            {
                if (gross <= 0m)
                {
                    LogOnce("OtaRoomRevenue_NoGross_" + resId,
                        $"OtaRoomRevenue: การจอง #{resId} ฐานรายได้ = GROSS แต่ไม่มี OTA_Gross_Amount จากอีเมลจอง " +
                        "— ข้าม (ไม่ใช้ TotalPrice แทน)");
                    return false;
                }
                amount = gross;
                return true;
            }

            // NET: ReservationBalance.Compute ถอยไปใช้ Deposit/TotalPrice เมื่อไม่มียอดจากอีเมล (เพื่อแสดงยอดค้างหน้าเคาน์เตอร์)
            // ⟹ ใช้ b.OtaAmount ได้เฉพาะเมื่ออีเมลจองมียอดจริง (OTA_Net_Amount / OTA_Gross_Amount > 0)
            bool hasEmailAmount = net > 0m || gross > 0m;
            if (!hasEmailAmount || b.OtaAmount < 0m)
            {
                LogOnce("OtaRoomRevenue_NoOtaAmt_" + resId,
                    $"OtaRoomRevenue: การจอง #{resId} ไม่มียอดตามอีเมลจอง (OTA_Net_Amount/OTA_Gross_Amount) " +
                    "— ข้าม (ไม่ใช้ TotalPrice แทน)");
                return false;
            }
            if (b.OtaAmount == 0m)
            {
                LogOnce("OtaRoomRevenue_ZeroOtaAmt_" + resId,
                    $"OtaRoomRevenue: การจอง #{resId} ยอดตามอีเมลจอง = 0 — ข้าม");
                return false;
            }
            if (net > 0m && b.OtaAmount != net)
            {
                // เช่น Email_Rsv_TotalSource = REFSELL → OtaAmount อิงยอด gross — แจ้งให้ผู้ทำบัญชีทราบ
                Log($"OtaRoomRevenue: การจอง #{resId} ฐาน NET แต่ OtaAmount {b.OtaAmount:N2} ≠ OTA_Net_Amount {net:N2} " +
                    "(ตรวจค่า Email_Rsv_TotalSource) — โพสต์ตาม OtaAmount");
            }
            amount = b.OtaAmount;
            return true;
        }

        private void ProcessOneOtaReservation(DataRow r, int resId, decimal amount, string basisLabel, string arAccountId)
        {
            if (amount <= 0m) return;   // ผู้เรียกกรองแล้ว — กันพลาดเท่านั้น (ไม่ mark เพื่อให้แก้ข้อมูลแล้วโพสต์ได้)

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
            _sync.EnqueueReceipt(resId, docRef, amount, 0, docDate,
                $"{guest} ({channel}{(string.IsNullOrEmpty(bookingId) ? "" : " " + bookingId)})",
                isDeposit: false, paymentMethod: channel,
                revenueType: "ROOM_REVENUE", paymentAccountId: arAccountId);

            CreateSummaryReceiptRow(docRef, resId, docDate, amount, channel,
                $"ค่าห้องพัก {channel} การจอง #{resId}" + (string.IsNullOrEmpty(bookingId) ? "" : $" ({bookingId})"), "0");

            MarkOtaPosted(resId, docRef);
            Log($"OtaRoomRevenue: การจอง #{resId} {channel} {amount:N2} (ฐาน {basisLabel}) → ลูกหนี้ OTA (ref={docRef})");
        }

        private void MarkOtaPosted(int reservationId, string reference)
        {
            _code.DatabaseInsertSafe(_conn,
                "UPDATE Reservation SET Ota_Revenue_Ref = @ref WHERE ID = @id AND Ota_Revenue_Ref IS NULL",
                new Dictionary<string, object> { { "@ref", reference }, { "@id", reservationId } });
        }

        /// <summary>
        /// โหมดเอกสาร OTA (RECEIPT_DOC): สร้าง "ใบเสร็จรับเงิน" ใน NextAcc ต่อการจอง Channel Collect —
        /// ผู้ซื้อ = ผู้ติดต่อของ OTA, แหล่งเงิน = บัญชีพักเงิน/ลูกหนี้ของ OTA รายช่องทาง (ประมวลผลในคิว
        /// CREATE_OTA_SALES_DOCUMENT → AccountingSyncService.ProcessOtaSalesDocument).
        ///
        /// กันซ้ำกับโหมดเดิม (JE/ใบเสร็จผ่าน OTA_RECEIVABLE) และ reclass เงินสดปลอม: "จอง" marker
        /// Reservation.Ota_Revenue_Ref = 'OTADOC-{id}' แบบ atomic (UPDATE … WHERE IS NULL + @@ROWCOUNT)
        /// **ก่อน** สร้างอะไรทั้งนั้น — ใครจองได้ก่อนคนนั้นโพสต์ อีกทางจะไม่เห็นการจองนี้ใน candidate อีก.
        /// ล้มก่อน enqueue สำเร็จ → คืน marker + ลบแถวสรุป เพื่อให้รอบหน้าลองใหม่ได้.
        /// คืน true = เข้าคิวแล้ว
        /// </summary>
        private bool ProcessOneOtaDocument(DataRow r, int resId, decimal amount, string basisLabel, string fallbackAccountId)
        {
            if (amount <= 0m) return false;

            DateTime docDate = r["CheckoutDate"] != DBNull.Value
                ? Convert.ToDateTime(r["CheckoutDate"]) : DateTime.Today;
            string channel = r["OTA_Channel"]?.ToString() ?? "OTA";
            string bookingId = r["OTA_Booking_ID"]?.ToString() ?? "";
            string guest = r["OTA_Guest_Name"]?.ToString();
            if (string.IsNullOrWhiteSpace(guest)) guest = $"ลูกค้า {channel}";

            var map = _sync.LookupOtaChannelMapping(channel);
            string key = AccountingSyncService.OtaChannelKey(channel);
            if (map != null && !map.IsActive)
            {
                LogOnce("OtaDoc_Disabled_" + key,
                    $"OtaDocument: ช่องทาง {channel} ({key}) ถูกปิดใน 'OTA → บัญชี/ผู้ซื้อ' — ข้าม (ไม่โพสต์อัตโนมัติ)");
                return false;
            }
            string accountId = map != null && !string.IsNullOrEmpty(map.AccountId) ? map.AccountId : fallbackAccountId;
            if (string.IsNullOrEmpty(accountId))
            {
                LogOnce("OtaDoc_NoAccount_" + key,
                    $"OtaDocument: ช่องทาง {channel} ({key}) ยังไม่ได้ผูกบัญชีพักเงิน/ลูกหนี้ OTA และไม่ได้ map OTA_RECEIVABLE " +
                    "— ข้าม (ตั้งที่ Accounting Integration → 'OTA → บัญชี/ผู้ซื้อ')");
                return false;
            }

            string docRef = $"OTADOC-{resId}";

            // 1) จอง marker แบบ atomic — ได้ 0 แถว = ถูกโพสต์/จองไปแล้วโดยทางอื่น
            if (!TryClaimOtaRevenueRef(resId, docRef)) return false;

            try
            {
                // 2) แถวสรุปในระบบ (เก็บ marker DOC:/APR:/{docId} ของ NextAcc กันสร้างซ้ำตอน retry)
                string paidType = map != null && !string.IsNullOrEmpty(map.PaidHowName) ? map.PaidHowName : channel;
                CreateSummaryReceiptRow(docRef, resId, docDate, amount, paidType,
                    $"ค่าห้องพัก {channel} (OTA เก็บเงินแทน) การจอง #{resId}" + (string.IsNullOrEmpty(bookingId) ? "" : $" ({bookingId})"), "0");
                if (!ReceiptExists(docRef))
                    throw new Exception("สร้างแถว Account_Receipt ไม่สำเร็จ");

                // 3) เข้าคิว
                long qid = _sync.EnqueueOtaSalesDocument(resId, docRef, amount, docDate.Date, channel, bookingId, guest,
                    basisLabel, fallbackAccountId);
                if (qid <= 0) throw new Exception("เข้าคิวไม่สำเร็จ (qid=" + qid + ")");

                Log($"OtaDocument: การจอง #{resId} {channel} {amount:N2} (ฐาน {basisLabel}) → คิว #{qid} " +
                    $"ใบเสร็จรับเงิน ref={docRef} แหล่งเงิน={(map != null && !string.IsNullOrEmpty(map.AccountId) ? map.PaidHowName + " " + map.AccountCode : "OTA_RECEIVABLE")}");
                return true;
            }
            catch (Exception ex)
            {
                ReleaseOtaRevenueRef(resId, docRef);
                Log($"OtaDocument: การจอง #{resId} ล้มเหลว → คืนสถานะให้ลองรอบหน้า: {ex.Message}");
                return false;
            }
        }

        private bool TryClaimOtaRevenueRef(int reservationId, string reference)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"UPDATE Reservation SET Ota_Revenue_Ref = @ref WHERE ID = @id AND Ota_Revenue_Ref IS NULL;
                      SELECT @@ROWCOUNT AS N;",
                    new Dictionary<string, object> { { "@ref", reference }, { "@id", reservationId } });
                return dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["N"]) > 0;
            }
            catch { return false; }
        }

        /// <summary>คืน marker + ลบแถวสรุปที่ยังไม่เคยขึ้น NextAcc (ใช้ตอน enqueue ล้ม)</summary>
        private void ReleaseOtaRevenueRef(int reservationId, string reference)
        {
            try
            {
                _code.DatabaseInsertSafe(_conn,
                    @"DELETE FROM Account_Receipt_Detail
                       WHERE Receipt_ID = @ref
                         AND EXISTS (SELECT 1 FROM Account_Receipt WHERE ID = @ref AND Nexaacc_Receipt_Payment_Id IS NULL);
                      DELETE FROM Account_Receipt WHERE ID = @ref AND Nexaacc_Receipt_Payment_Id IS NULL;
                      UPDATE Reservation SET Ota_Revenue_Ref = NULL WHERE ID = @id AND Ota_Revenue_Ref = @ref;",
                    new Dictionary<string, object> { { "@ref", reference }, { "@id", reservationId } });
            }
            catch (Exception ex)
            {
                Log($"OtaDocument: คืนสถานะการจอง #{reservationId} ({reference}) ไม่สำเร็จ: {ex.Message}");
            }
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

        /// <summary>
        /// อ่านค่าจาก Accounting_Integration_Config (ตารางเดียวกับที่ AccountingConfig อ่าน) — ไม่มี/ว่าง/อ่านไม่ได้ = ค่าเริ่มต้น
        /// </summary>
        private string ReadIntegrationConfig(string key, string defaultValue)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = @k AND ConfigValue IS NOT NULL",
                    new Dictionary<string, object> { { "@k", key } });
                if (dt?.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                {
                    string v = dt.Rows[0][0].ToString();
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
            }
            catch { }
            return defaultValue;
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
