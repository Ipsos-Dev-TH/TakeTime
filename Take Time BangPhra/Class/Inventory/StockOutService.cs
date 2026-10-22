using System;
using System.Collections.Generic;
using System.Data;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Inventory
{
    /// <summary>ต้นทุน/ราคาตลาดของสินค้า ณ ตอนเบิก</summary>
    public sealed class StockOutCostInfo
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal CostPerUnit { get; set; }
        /// <summary>COST_PRICE = Product.Cost_Price · LAST_PURCHASE = ราคาซื้อล่าสุด (Product_In) · NONE = ไม่มีต้นทุน</summary>
        public string CostSource { get; set; }
        /// <summary>ราคาขาย (Product.Sell_Price — ราคารวม VAT) ใช้เป็นราคาตลาดของภาษีขายเสมือนขาย</summary>
        public decimal MarketPerUnit { get; set; }

        public string CostSourceTh =>
            CostSource == "COST_PRICE" ? "ต้นทุนสินค้า" :
            CostSource == "LAST_PURCHASE" ? "ราคาซื้อล่าสุด" : "ไม่มีต้นทุน";
    }

    /// <summary>
    /// การเบิกสินค้าออกนอกการขาย (เสียหาย/หมดอายุ/ส่งเสริมการขาย/ใช้ภายใน ฯลฯ) — PHASE19_30
    /// บันทึก Product_Out พร้อมเหตุผล → อนุมัติ (ถ้าเหตุผลกำหนด) → ลงบัญชี NextAcc เฉพาะแถว APPROVED
    /// (AccountingSyncService.EnqueueStockOutReasonJournal, ref STOCKOUT-{ID}).
    /// ยกเลิก: แถวที่ยังไม่เคยส่ง NextAcc ลบได้ / แถวที่ส่งแล้ว mark Is_Reversed + คืนสต็อก (Product_In
    /// InType='STOCKOUT_REV') + JE กลับรายการ + qty IN. หน้าเบิกไม่เขียน Remark='ขาย' (กัน POS rollup หยิบไป)
    /// </summary>
    public class StockOutService
    {
        public const string StatusPending = "PENDING";
        public const string StatusApproved = "APPROVED";
        public const string StatusRejected = "REJECTED";
        /// <summary>Product_In.InType ของแถวคืนสต็อกจากการยกเลิก/ปฏิเสธการเบิก</summary>
        public const string ReversalInType = "STOCKOUT_REV";

        private readonly string _conn;
        private readonly code _code = new code();

        public StockOutService(string conn) { _conn = conn; }

        // ── schema ────────────────────────────────────────────────────────────

        private static readonly object _schemaLock = new object();
        private static bool? _schemaReady;
        private static DateTime _schemaCheckedAt = DateTime.MinValue;

        /// <summary>รัน PHASE19_30 แล้ว (Product_Out มี Out_Reason_Code + Is_Reversed) — cache 60 วินาที</summary>
        public static bool SchemaReady(string conn)
        {
            lock (_schemaLock)
            {
                if (_schemaReady.HasValue && DateTime.Now - _schemaCheckedAt < TimeSpan.FromSeconds(60))
                    return _schemaReady.Value;
            }
            bool ok = false;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    @"SELECT CASE WHEN COL_LENGTH('dbo.Product_Out','Out_Reason_Code') IS NOT NULL
                                   AND COL_LENGTH('dbo.Product_Out','Is_Reversed') IS NOT NULL
                                   AND OBJECT_ID('dbo.Stock_Out_Reason','U') IS NOT NULL THEN 1 ELSE 0 END AS Ok", null);
                ok = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["Ok"]) == 1;
            }
            catch { ok = false; }
            lock (_schemaLock) { _schemaReady = ok; _schemaCheckedAt = DateTime.Now; }
            return ok;
        }

        // ── ต้นทุน ────────────────────────────────────────────────────────────

        /// <summary>
        /// ต้นทุนต่อหน่วย: Product.Cost_Price → ราคาซื้อล่าสุดจาก Product_In (InType PURCHASE/NULL) → 0.
        /// ไม่ใช้ราคาที่ผู้ใช้พิมพ์ (เดิมพิมพ์ราคาขายแล้วถูกใช้เป็นต้นทุน → สินค้าคงเหลือถูกเครดิตเกิน)
        /// </summary>
        public StockOutCostInfo GetCostInfo(int productId)
        {
            var info = new StockOutCostInfo { ProductId = productId, CostSource = "NONE" };
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 Product_Name, ISNULL(Cost_Price,0) AS Cost_Price, ISNULL(Sell_Price,0) AS Sell_Price FROM Product WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", productId } });
                if (dt != null && dt.Rows.Count > 0)
                {
                    info.ProductName = dt.Rows[0]["Product_Name"]?.ToString() ?? "";
                    info.MarketPerUnit = Dec(dt.Rows[0]["Sell_Price"]);
                    decimal cp = Dec(dt.Rows[0]["Cost_Price"]);
                    if (cp > 0) { info.CostPerUnit = cp; info.CostSource = "COST_PRICE"; return info; }
                }
            }
            catch { }

            // ไม่มี Cost_Price → ราคาซื้อล่าสุด (Product_In.PricePerUnit ของการรับเข้าจริง ไม่รวมแถวคืนสต็อก/ปรับปรุง)
            try
            {
                DataTable dt;
                try
                {
                    dt = _code.DatabaseQuerySafe(_conn,
                        @"SELECT TOP 1 PricePerUnit FROM Product_In
                           WHERE Product_ID = @id AND PricePerUnit > 0
                             AND (InType IS NULL OR InType = 'PURCHASE')
                           ORDER BY DateTime_In DESC, ID DESC",
                        new Dictionary<string, object> { { "@id", productId } });
                }
                catch
                {
                    dt = _code.DatabaseQuerySafe(_conn,
                        "SELECT TOP 1 PricePerUnit FROM Product_In WHERE Product_ID = @id AND PricePerUnit > 0 ORDER BY DateTime_In DESC",
                        new Dictionary<string, object> { { "@id", productId } });
                }
                if (dt != null && dt.Rows.Count > 0)
                {
                    decimal lp = Dec(dt.Rows[0]["PricePerUnit"]);
                    if (lp > 0) { info.CostPerUnit = lp; info.CostSource = "LAST_PURCHASE"; }
                }
            }
            catch { }
            return info;
        }

        // ── บันทึก ────────────────────────────────────────────────────────────

        /// <summary>
        /// บันทึกการเบิก 1 รายการ. คืน Product_Out.ID (0 = ไม่สำเร็จ, ดู message).
        /// status = PENDING (รออนุมัติ) / APPROVED (ลงบัญชีทันที)
        /// </summary>
        public long Record(int productId, decimal quantity, string reasonCode, string note, string evidencePath,
            string recordedBy, bool recorderIsOwner, DateTime when, out string status, out string message)
        {
            status = null; message = null;
            if (productId <= 0 || quantity <= 0) { message = "ข้อมูลสินค้า/จำนวนไม่ถูกต้อง"; return 0; }

            var reason = StockOutReasonCatalog.Get(_conn, reasonCode);
            if (reason == null || !reason.IsActive) { message = "กรุณาเลือกเหตุผลการเบิกที่เปิดใช้งาน"; return 0; }
            if (StockOutReasonCatalog.IsSystemReason(reason.Code)) { message = "คืนสินค้าผู้ขายต้องทำที่หน้าคืนสินค้า/ใบลดหนี้ผู้ขาย"; return 0; }
            if (reason.RequiresEvidence && string.IsNullOrWhiteSpace(evidencePath))
            {
                message = "เหตุผล \"" + reason.NameTh + "\" ต้องแนบหลักฐาน (ภาพถ่าย/บันทึกการทำลาย)";
                return 0;
            }

            var cost = GetCostInfo(productId);
            bool schema = SchemaReady(_conn);
            string outType = reason.Code == "COUNT_ADJUST" ? "ADJUSTMENT_LOSS" : "WRITEOFF";
            // Remark ห้ามเป็น 'ขาย' (POS daily rollup เลือกแถว Remark = N'ขาย')
            string remark = "เบิก:" + reason.Code;
            string reasonText = Trunc(string.IsNullOrWhiteSpace(note) ? reason.NameTh : reason.NameTh + " — " + note.Trim(), 500);

            // Owner = หุ้นส่วนผู้จัดการ/ผู้อนุมัติ → บันทึกเองถือว่าอนุมัติแล้ว
            bool needApproval = schema && reason.RequiresApproval && !recorderIsOwner;
            status = needApproval ? StatusPending : StatusApproved;

            long id;
            if (schema)
            {
                id = _code.DatabaseInsertReturnSafe(_conn,
                    @"INSERT INTO [dbo].[Product_Out]
                        ([DateTime_Out],[Product_ID],[Amount],[PricePerUnit],[Account_Receipt_ID],[Remark],[OutType],[Reason],
                         [Out_Reason_Code],[Recorded_By],[Approved_By],[Approved_At],[Approval_Status],[Evidence_Path],[Is_Reversed])
                      VALUES (@dt,@pid,@amt,@cost,'0',@remark,@outType,@reason,
                         @code,@by,@apBy,@apAt,@status,@ev,0);
                      SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new Dictionary<string, object>
                    {
                        { "@dt", when }, { "@pid", productId }, { "@amt", quantity }, { "@cost", cost.CostPerUnit },
                        { "@remark", remark }, { "@outType", outType }, { "@reason", reasonText },
                        { "@code", reason.Code }, { "@by", Trunc(recordedBy, 100) },
                        { "@apBy", needApproval ? null : (object)Trunc(recordedBy, 100) },
                        { "@apAt", needApproval ? null : (object)when },
                        { "@status", status }, { "@ev", string.IsNullOrWhiteSpace(evidencePath) ? null : Trunc(evidencePath, 400) }
                    });
            }
            else
            {
                // ยังไม่รัน PHASE19_30: บันทึกแบบเดิม (ไม่มีอนุมัติ/หลักฐานในฐานข้อมูล) แต่ลงบัญชีตามเหตุผลได้
                id = _code.DatabaseInsertReturnSafe(_conn,
                    @"INSERT INTO [dbo].[Product_Out] ([DateTime_Out],[Product_ID],[Amount],[PricePerUnit],[Account_Receipt_ID],[Remark],[OutType],[Reason])
                      VALUES (@dt,@pid,@amt,@cost,'0',@remark,@outType,@reason);
                      SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new Dictionary<string, object>
                    {
                        { "@dt", when }, { "@pid", productId }, { "@amt", quantity }, { "@cost", cost.CostPerUnit },
                        { "@remark", remark }, { "@outType", outType }, { "@reason", Trunc(reasonText, 500) }
                    });
            }
            if (id <= 0) { message = "บันทึกไม่สำเร็จ"; return 0; }

            Log($"StockOut #{id}: {reason.Code} product={productId} qty={quantity} cost={cost.CostPerUnit} ({cost.CostSource}) status={status} by={recordedBy}");

            if (status == StatusApproved)
                message = PostToNextAcc(id, reason, productId, cost.ProductName, quantity, cost.CostPerUnit, cost.MarketPerUnit, when, note);
            else
                message = "รออนุมัติ";
            return id;
        }

        // ── อนุมัติ / ปฏิเสธ ─────────────────────────────────────────────────

        /// <summary>อนุมัติแถว PENDING (ผู้บันทึกอนุมัติตัวเองไม่ได้ ยกเว้น Owner) แล้วส่งลงบัญชี</summary>
        public bool Approve(long id, string approver, bool approverIsOwner, out string message)
        {
            message = null;
            if (!SchemaReady(_conn)) { message = "ยังไม่ได้รัน PHASE19_30"; return false; }
            int n = _code.DatabaseInsertSafe(_conn,
                @"UPDATE Product_Out
                     SET Approval_Status = 'APPROVED', Approved_By = @by, Approved_At = GETDATE()
                   WHERE ID = @id AND Approval_Status = 'PENDING' AND Is_Reversed = 0
                     AND (@owner = 1 OR ISNULL(Recorded_By,'') <> @by)",
                new Dictionary<string, object> { { "@id", id }, { "@by", Trunc(approver, 100) }, { "@owner", approverIsOwner ? 1 : 0 } });
            if (n != 1)
            {
                message = "อนุมัติไม่ได้ — รายการไม่อยู่ในสถานะรออนุมัติ หรือผู้บันทึกอนุมัติรายการของตัวเอง (ต้องให้ Owner/ผู้อื่นอนุมัติ)";
                return false;
            }
            Log($"StockOut #{id}: APPROVED by {approver}");
            message = PostToNextAcc(id);
            return true;
        }

        /// <summary>ปฏิเสธแถว PENDING → คืนสต็อก (Product_In STOCKOUT_REV) ไม่ลงบัญชี</summary>
        public bool Reject(long id, string approver, string rejectNote, out string message)
        {
            message = null;
            if (!SchemaReady(_conn)) { message = "ยังไม่ได้รัน PHASE19_30"; return false; }
            var row = LoadRow(id);
            if (row == null) { message = "ไม่พบรายการ"; return false; }
            int n = _code.DatabaseInsertSafe(_conn,
                @"UPDATE Product_Out
                     SET Approval_Status = 'REJECTED', Approved_By = @by, Approved_At = GETDATE(), Is_Reversed = 1,
                         Reason = LEFT(ISNULL(Reason,'') + N' [ปฏิเสธ: ' + @note + N']', 500)
                   WHERE ID = @id AND Approval_Status = 'PENDING' AND Is_Reversed = 0",
                new Dictionary<string, object> { { "@id", id }, { "@by", Trunc(approver, 100) }, { "@note", Trunc(rejectNote ?? "", 200) } });
            if (n != 1) { message = "ปฏิเสธไม่ได้ — รายการไม่อยู่ในสถานะรออนุมัติ"; return false; }
            InsertRestockRow(row.ProductId, row.Quantity, row.CostPerUnit);
            Log($"StockOut #{id}: REJECTED by {approver} — คืนสต็อก {row.Quantity}");
            message = "ปฏิเสธแล้ว และคืนจำนวนสต็อก";
            return true;
        }

        // ── ลงบัญชี ──────────────────────────────────────────────────────────

        /// <summary>ส่ง (หรือส่งซ้ำ) แถวที่ APPROVED เข้าคิวลงบัญชี — ใช้หลังอนุมัติ และปุ่ม "ลงบัญชีอีกครั้ง"</summary>
        public string PostToNextAcc(long id)
        {
            var row = LoadRow(id);
            if (row == null) return "ไม่พบรายการ";
            if (row.IsReversed) return "รายการถูกยกเลิกแล้ว";
            if (row.ApprovalStatus.Length > 0 && row.ApprovalStatus != StatusApproved) return "รายการยังไม่อนุมัติ";
            if (row.IsPurchaseReturn) return "รายการคืนสินค้าผู้ขาย ลงบัญชีที่หน้าคืนสินค้า (PRT-…) แล้ว — ไม่ลงซ้ำ";
            var reason = StockOutReasonCatalog.Get(_conn, row.ReasonCode);
            if (reason == null) return "ไม่พบเหตุผล " + row.ReasonCode + " ในแคตตาล็อก";

            var cost = GetCostInfo(row.ProductId);
            decimal unitCost = row.CostPerUnit;
            if (unitCost <= 0 && cost.CostPerUnit > 0)
            {
                // ตอนบันทึกยังไม่มีต้นทุน แต่ตอนนี้ตั้งแล้ว → อัปเดตแถวแล้วลงบัญชีด้วยต้นทุนใหม่
                unitCost = cost.CostPerUnit;
                try
                {
                    _code.DatabaseInsertSafe(_conn, "UPDATE Product_Out SET PricePerUnit = @c WHERE ID = @id",
                        new Dictionary<string, object> { { "@c", unitCost }, { "@id", id } });
                }
                catch { }
            }
            return PostToNextAcc(id, reason, row.ProductId, cost.ProductName, row.Quantity, unitCost, cost.MarketPerUnit, row.OutDate, row.Note);
        }

        private string PostToNextAcc(long id, StockOutReason reason, int productId, string productName, decimal qty,
            decimal unitCost, decimal marketGross, DateTime when, string note)
        {
            try
            {
                var cfg = new AccountingConfig(_conn);
                if (!cfg.IsConfigured || !cfg.Enabled) return "บันทึกในระบบแล้ว (ยังไม่ได้เปิดเชื่อม NextAcc)";
                var sync = new AccountingSyncService(_conn);
                long q = sync.EnqueueStockOutReasonJournal(id, productId, productName, qty, unitCost, marketGross,
                    reason.Code, reason.NameTh, reason.GlMappingKey, reason.DeemedSaleVat, reason.VatBase, when, note);
                if (q > 0) return "ส่งเข้าคิวลงบัญชี NextAcc แล้ว" + (reason.DeemedSaleVat ? " (รวมภาษีขายเสมือนขาย ม.77/1(8))" : "");
                if (q == 0) return "ลงบัญชีไปแล้ว";
                if (q == -2) return "⚠ สินค้าไม่มีต้นทุน — ส่งเฉพาะจำนวนสต็อก ยังไม่ลงบัญชี (ตั้งต้นทุนแล้วกด \"ลงบัญชีอีกครั้ง\")";
                return "ยังไม่ได้ลงบัญชี (ตรวจการตั้งค่า NextAcc)";
            }
            catch (Exception ex)
            {
                Log($"StockOut #{id}: enqueue ล้มเหลว: {ex.Message}");
                return "⚠ ส่งลงบัญชีไม่สำเร็จ: " + ex.Message;
            }
        }

        // ── ยกเลิก / ลบ ──────────────────────────────────────────────────────

        /// <summary>
        /// ยกเลิกการเบิก: ยังไม่เคยส่ง NextAcc → ลบแถว · ส่งแล้ว → Is_Reversed=1 + คืนสต็อก + JE กลับรายการ + qty IN.
        /// รองรับแถวเบิกแบบเก่า (OutType WRITEOFF ไม่มีเหตุผล — JE ref SWO-{logId} จาก Stock_Adjustment_Log)
        /// </summary>
        public bool CancelOrDelete(long id, string user, string why, out string message)
        {
            message = null;
            var row = LoadRow(id);
            if (row == null) { message = "ไม่พบรายการ"; return false; }
            if (row.IsPurchaseReturn)
            {
                message = "รายการคืนสินค้าผู้ขาย — ยกเลิก/แก้ไขที่หน้าคืนสินค้า (ลงบัญชีไว้ที่นั่น ref PRT-…)";
                return false;
            }
            if (row.IsReversed)
            {
                message = row.ApprovalStatus == StatusRejected ? "รายการถูกปฏิเสธและคืนสต็อกแล้ว" : "รายการถูกยกเลิกไปแล้ว";
                return false;
            }

            bool schema = SchemaReady(_conn);
            string legacyJournalId = null, legacyQtySrc = null;
            bool footprint;

            if (string.IsNullOrEmpty(row.ReasonCode))
            {
                // แถวเบิกแบบเก่า: จับคู่ Stock_Adjustment_Log ที่บันทึกพร้อมกัน (สินค้า+จำนวน+เวลา ±5 นาที)
                long logId; string logStatus; string logJournal; decimal logCost;
                if (!FindLegacyWriteOffLog(row, out logId, out logStatus, out logJournal, out logCost))
                {
                    footprint = false;
                }
                else
                {
                    string swoRef = "SWO-" + logId;
                    if (string.IsNullOrEmpty(logJournal) && QueueHasUnfinished("STOCK_WRITEOFF", swoRef))
                    {
                        message = "รายการเบิกเดิมยังรอลงบัญชีในคิว (" + swoRef + ") — รอให้คิวทำงานเสร็จแล้วค่อยยกเลิก";
                        return false;
                    }
                    legacyJournalId = logJournal;
                    legacyQtySrc = swoRef;
                    if (logCost > 0) row.CostPerUnit = logCost;
                    footprint = !string.IsNullOrEmpty(logJournal) || QueueHasAny("STOCK_QTY_PUSH", "QTY-OUT-" + swoRef);
                }
            }
            else
            {
                // เคยส่งเข้าคิว NextAcc แล้ว (JE หรือจำนวนสต๊อก) = ต้องกลับรายการ ไม่ใช่ลบ
                string createRef = AccountingDataMapper.StockOutRef(id);
                footprint = !string.IsNullOrWhiteSpace(row.JournalRef)
                    || QueueHasAny("STOCK_OUT_REASON_JOURNAL", createRef)
                    || QueueHasAny("STOCK_QTY_PUSH", "QTY-OUT-" + createRef);
            }

            if (!footprint)
            {
                _code.DatabaseInsertSafe(_conn, "DELETE FROM [dbo].[Product_Out] WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                Log($"StockOut #{id}: ลบโดย {user} (ยังไม่เคยส่ง NextAcc)");
                message = "ลบรายการแล้ว (ยังไม่เคยลงบัญชี)";
                return true;
            }

            if (!schema)
            {
                message = "รายการนี้ส่งลงบัญชี NextAcc แล้ว — ต้องรัน migration PHASE19_30 ก่อนจึงจะยกเลิกแบบกลับรายการได้";
                return false;
            }

            int n = _code.DatabaseInsertSafe(_conn,
                @"UPDATE Product_Out
                     SET Is_Reversed = 1,
                         Reason = LEFT(ISNULL(Reason,'') + N' [ยกเลิกโดย ' + @by + N': ' + @why + N']', 500)
                   WHERE ID = @id AND Is_Reversed = 0",
                new Dictionary<string, object> { { "@id", id }, { "@by", Trunc(user, 60) }, { "@why", Trunc(why ?? "", 150) } });
            if (n != 1) { message = "รายการถูกยกเลิกไปแล้ว"; return false; }

            InsertRestockRow(row.ProductId, row.Quantity, row.CostPerUnit);
            string accMsg;
            try
            {
                var sync = new AccountingSyncService(_conn);
                long q = sync.EnqueueStockOutReasonReversal(id, row.ProductId, row.ProductName, row.Quantity, row.CostPerUnit,
                    DateTime.Now, why, legacyJournalId, legacyQtySrc);
                accMsg = q > 0 ? "ส่งกลับรายการบัญชี NextAcc แล้ว" : q == 0 ? "ไม่มี JE ให้กลับรายการ" : "ยังไม่ได้ส่ง NextAcc (ตรวจการตั้งค่า)";
            }
            catch (Exception ex) { accMsg = "⚠ ส่งกลับรายการไม่สำเร็จ: " + ex.Message; }

            Log($"StockOut #{id}: ยกเลิก (reverse) โดย {user}: {why} — {accMsg}");
            message = "ยกเลิกแล้ว — คืนจำนวนสต็อก และ" + accMsg;
            return true;
        }

        private bool FindLegacyWriteOffLog(StockOutRowInfo row, out long logId, out string status, out string journalId, out decimal cost)
        {
            logId = 0; status = null; journalId = null; cost = 0;
            if (row.OutType != "WRITEOFF" && row.OutType != "ADJUSTMENT_LOSS") return false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"IF OBJECT_ID('dbo.Stock_Adjustment_Log','U') IS NOT NULL
                      SELECT TOP 1 ID, Sync_Status, Nexaacc_Journal_Id, Cost_PerUnit
                        FROM Stock_Adjustment_Log
                       WHERE Adjustment_Type = 'WRITEOFF' AND Product_ID = @pid
                         AND Difference_Qty = @diff
                         AND ABS(DATEDIFF(SECOND, Created_Date, @dt)) <= 300
                       ORDER BY ABS(DATEDIFF(SECOND, Created_Date, @dt)), ID",
                    new Dictionary<string, object> { { "@pid", row.ProductId }, { "@diff", -row.Quantity }, { "@dt", row.OutDate } });
                if (dt == null || !dt.Columns.Contains("ID") || dt.Rows.Count == 0) return false;
                logId = Convert.ToInt64(dt.Rows[0]["ID"]);
                status = dt.Rows[0]["Sync_Status"]?.ToString();
                journalId = dt.Rows[0]["Nexaacc_Journal_Id"] == DBNull.Value ? null : dt.Rows[0]["Nexaacc_Journal_Id"].ToString();
                cost = Dec(dt.Rows[0]["Cost_PerUnit"]);
                return true;
            }
            catch { return false; }
        }

        private bool QueueHasAny(string action, string stockRef) => QueueCount(action, stockRef, null) > 0;
        private bool QueueHasUnfinished(string action, string stockRef) => QueueCount(action, stockRef, "('PENDING','PROCESSING','FAILED')") > 0;

        private int QueueCount(string action, string stockRef, string statusIn)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT COUNT(*) AS N FROM Accounting_Sync_Queue WHERE Entity_Type = 'STOCK' AND Action_Type = @a AND Payload LIKE @p" +
                    (statusIn == null ? "" : " AND Status IN " + statusIn),
                    new Dictionary<string, object> { { "@a", action }, { "@p", "%\"stockRef\":\"" + stockRef + "\"%" } });
                return dt != null && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["N"]) : 0;
            }
            catch { return 0; }
        }

        /// <summary>คืนจำนวนสต็อกฝั่ง TakeTime (ยอดคงเหลือ = ΣProduct_In − ΣProduct_Out) — ไม่ส่ง NextAcc เอง</summary>
        private void InsertRestockRow(int productId, decimal qty, decimal unitCost)
        {
            var ps = new Dictionary<string, object>
            {
                { "@d", DateTime.Now }, { "@pid", productId }, { "@amt", qty }, { "@cost", unitCost < 0 ? 0 : unitCost }, { "@t", ReversalInType }
            };
            try
            {
                _code.DatabaseInsertSafe(_conn,
                    "INSERT INTO [dbo].[Product_In] ([DateTime_In],[Product_ID],[Amount],[PricePerUnit],[InType]) VALUES (@d,@pid,@amt,@cost,@t)", ps);
            }
            catch
            {
                ps.Remove("@t");
                _code.DatabaseInsertSafe(_conn,
                    "INSERT INTO [dbo].[Product_In] ([DateTime_In],[Product_ID],[Amount],[PricePerUnit]) VALUES (@d,@pid,@amt,@cost)", ps);
            }
        }

        // ── อ่านข้อมูล ───────────────────────────────────────────────────────

        public sealed class StockOutRowInfo
        {
            public long Id { get; set; }
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public decimal Quantity { get; set; }
            public decimal CostPerUnit { get; set; }
            public DateTime OutDate { get; set; }
            public string OutType { get; set; }
            public string ReasonCode { get; set; }
            public string Note { get; set; }
            public string ApprovalStatus { get; set; }
            public bool IsReversed { get; set; }
            public string RecordedBy { get; set; }
            public string EvidencePath { get; set; }
            public string JournalRef { get; set; }
            public string Remark { get; set; }
            public string AccountReceiptId { get; set; }
            /// <summary>Product_Out.Purchase_Return_Ref (คอลัมน์ของหน้าคืนสินค้า — อาจยังไม่มี)</summary>
            public string PurchaseReturnRef { get; set; }
            /// <summary>แถวคืนสินค้าผู้ขาย — หน้าคืนสินค้าลงบัญชีเอง (PRT-…) ห้ามลง/กลับรายการซ้ำที่นี่</summary>
            public bool IsPurchaseReturn =>
                StockOutReasonCatalog.IsSystemReason(ReasonCode) || !string.IsNullOrWhiteSpace(PurchaseReturnRef)
                || OutType == "RETURN_TO_VENDOR";
        }

        public StockOutRowInfo LoadRow(long id)
        {
            DataTable dt = null;
            bool schema = SchemaReady(_conn);
            try
            {
                dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 po.*, p.Product_Name FROM Product_Out po LEFT JOIN Product p ON p.ID = po.Product_ID WHERE po.ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
            }
            catch { dt = null; }
            if (dt == null || dt.Rows.Count == 0) return null;
            var r = dt.Rows[0];
            Func<string, string> s = c => dt.Columns.Contains(c) && r[c] != DBNull.Value ? r[c].ToString() : "";
            var info = new StockOutRowInfo
            {
                Id = id,
                ProductId = r["Product_ID"] == DBNull.Value ? 0 : Convert.ToInt32(r["Product_ID"]),
                ProductName = s("Product_Name"),
                Quantity = Dec(r["Amount"]),
                CostPerUnit = Dec(r["PricePerUnit"]),
                OutDate = r["DateTime_Out"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(r["DateTime_Out"]),
                OutType = s("OutType").ToUpperInvariant(),
                ReasonCode = schema ? s("Out_Reason_Code") : "",
                Note = s("Reason"),
                ApprovalStatus = s("Approval_Status").ToUpperInvariant(),
                IsReversed = dt.Columns.Contains("Is_Reversed") && r["Is_Reversed"] != DBNull.Value && Convert.ToBoolean(r["Is_Reversed"]),
                RecordedBy = s("Recorded_By"),
                EvidencePath = s("Evidence_Path"),
                JournalRef = s("Nexaacc_Journal_Ref"),
                Remark = s("Remark"),
                AccountReceiptId = s("Account_Receipt_ID"),
                PurchaseReturnRef = s("Purchase_Return_Ref")
            };
            return info;
        }

        /// <summary>แถวนี้เป็นการเบิก (ไม่ใช่ขาย) — มีเหตุผล หรือ OutType WRITEOFF/ADJUSTMENT_LOSS</summary>
        public static bool IsStockOutRow(StockOutRowInfo r) =>
            r != null && (!string.IsNullOrEmpty(r.ReasonCode) || r.OutType == "WRITEOFF" || r.OutType == "ADJUSTMENT_LOSS"
                          || r.IsPurchaseReturn);

        /// <summary>รายการรออนุมัติ</summary>
        public DataTable LoadPending()
        {
            if (!SchemaReady(_conn)) return new DataTable();
            return _code.DatabaseQuerySafe(_conn,
                @"SELECT po.ID, po.DateTime_Out, po.Product_ID, p.Product_Name, po.Amount, po.PricePerUnit,
                         po.Amount * po.PricePerUnit AS Cost_Total, po.Out_Reason_Code, r.Name_TH AS Reason_Name,
                         po.Reason, po.Recorded_By, po.Evidence_Path
                    FROM Product_Out po
                    LEFT JOIN Product p ON p.ID = po.Product_ID
                    LEFT JOIN Stock_Out_Reason r ON r.Code = po.Out_Reason_Code
                   WHERE po.Out_Reason_Code IS NOT NULL AND po.Approval_Status = 'PENDING' AND po.Is_Reversed = 0
                   ORDER BY po.DateTime_Out", null);
        }

        /// <summary>ประวัติการเบิก (รวมแถวเบิกแบบเก่า) ย้อนหลัง N วัน</summary>
        public DataTable LoadHistory(int days)
        {
            var ps = new Dictionary<string, object> { { "@from", DateTime.Today.AddDays(-Math.Max(1, days)) } };
            if (SchemaReady(_conn))
            {
                return _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 300 po.ID, po.DateTime_Out, po.Product_ID, p.Product_Name, po.Amount, po.PricePerUnit,
                             po.Amount * po.PricePerUnit AS Cost_Total, po.Out_Reason_Code,
                             ISNULL(r.Name_TH, N'(รายการเดิม)') AS Reason_Name, po.Reason, po.Recorded_By, po.Approved_By,
                             ISNULL(po.Approval_Status, '') AS Approval_Status, po.Is_Reversed, po.Evidence_Path, po.Nexaacc_Journal_Ref
                        FROM Product_Out po
                        LEFT JOIN Product p ON p.ID = po.Product_ID
                        LEFT JOIN Stock_Out_Reason r ON r.Code = po.Out_Reason_Code
                       WHERE po.DateTime_Out >= @from
                         AND (po.Out_Reason_Code IS NOT NULL OR po.OutType IN ('WRITEOFF','ADJUSTMENT_LOSS'))
                       ORDER BY po.DateTime_Out DESC, po.ID DESC", ps);
            }
            return _code.DatabaseQuerySafe(_conn,
                @"SELECT TOP 300 po.ID, po.DateTime_Out, po.Product_ID, p.Product_Name, po.Amount, po.PricePerUnit,
                         po.Amount * po.PricePerUnit AS Cost_Total, CAST(NULL AS NVARCHAR(30)) AS Out_Reason_Code,
                         po.Remark AS Reason_Name, po.Reason, CAST(NULL AS NVARCHAR(100)) AS Recorded_By,
                         CAST(NULL AS NVARCHAR(100)) AS Approved_By, '' AS Approval_Status, CAST(0 AS BIT) AS Is_Reversed,
                         CAST(NULL AS NVARCHAR(400)) AS Evidence_Path, CAST(NULL AS NVARCHAR(100)) AS Nexaacc_Journal_Ref
                    FROM Product_Out po
                    LEFT JOIN Product p ON p.ID = po.Product_ID
                   WHERE po.DateTime_Out >= @from AND po.OutType IN ('WRITEOFF','ADJUSTMENT_LOSS')
                   ORDER BY po.DateTime_Out DESC, po.ID DESC", ps);
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private void Log(string msg)
        {
            try { _code.Logs(_conn, "StockOut", msg, "SYSTEM"); } catch { }
        }

        internal static decimal Dec(object o)
        {
            if (o == null || o == DBNull.Value) return 0m;
            try { return Convert.ToDecimal(o); }
            catch
            {
                decimal d;
                return decimal.TryParse(o.ToString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out d) ? d : 0m;
            }
        }

        internal static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
