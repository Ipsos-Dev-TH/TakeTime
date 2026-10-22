using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Purchasing
{
    /// <summary>บรรทัดสินค้าของการรับของหนึ่ง batch (รวมแถว Product_In ของสินค้าเดียวกัน)</summary>
    public sealed class ReceiptLine
    {
        public int ProductId;
        public string ProductName;
        public decimal Qty;
        public decimal UnitCost;        // ราคาต่อหน่วยสุทธิ (ไม่รวม VAT) ที่บันทึกตอนรับ
        public decimal ReturnedQty;     // คืนผู้ขายไปแล้ว (Product_Out RETURN_TO_VENDOR ของ batch นี้)
        public decimal Net => Math.Round(Qty * UnitCost, 2, MidpointRounding.AwayFromZero);
        public decimal ReturnableQty => Math.Max(0m, Qty - ReturnedQty);
    }

    /// <summary>หนึ่งการรับของ (Receipt_Batch) — ข้อมูลเอกสารซื้อ + บรรทัดสินค้า</summary>
    public sealed class ReceiptBatchInfo
    {
        public string Batch;
        public DateTime ReceivedAt;
        public string SupplierName;
        public string SupplierTaxId;
        public string SupplierDocNo;
        public DateTime? SupplierDocDate;
        public decimal VatAmount;
        public string PaymentStatus;     // UNPAID | PAID | CREDIT | null (legacy)
        public string LinkedVoucherId;
        public List<ReceiptLine> Lines = new List<ReceiptLine>();
        public decimal NetTotal => Lines.Sum(l => l.Net);
        public bool IsLinked => !string.IsNullOrEmpty(LinkedVoucherId);
        /// <summary>ภาษีซื้อเคลมได้ (โดยประมาณ): มี VAT + ผู้ขายมีเลขผู้เสียภาษี 13 หลัก (ใบกำกับเต็มรูป §86/4 ต้องมีเลขผู้ขาย)</summary>
        public bool VatLikelyClaimable => VatAmount > 0 && PurchaseService.IsThaiTaxId(SupplierTaxId);
    }

    /// <summary>ผลคำนวณ JE คืนสินค้าให้ผู้ขาย (pure — ใช้ทั้งหน้าเว็บและตัวจำลองทดสอบ)</summary>
    public sealed class PurchaseReturnPlan
    {
        public decimal PurchaseNet;       // มูลค่าคืนตามราคาซื้อ (ผู้ขายลดหนี้/คืนเงินส่วนสุทธิ)
        public decimal CarryingCost;      // มูลค่าที่ตัดสินค้าคงเหลือ ณ ค่าเฉลี่ย
        public decimal VatReversal;       // กลับภาษีซื้อ (§82/10 ผู้ซื้อลดภาษีซื้อเดือนที่ได้รับใบลดหนี้)
        public string CounterKey;         // GRNI | ACCOUNTS_PAYABLE | CASH (แหล่งเงิน) | NONE (ไม่มี JE)
        public decimal CounterAmount => PurchaseNet + VatReversal;
        public decimal Difference => PurchaseNet - CarryingCost;   // >0 Cr PURCHASE_RETURNS, <0 Dr PURCHASE_RETURNS
        public bool PostsJournal => CounterKey != "NONE" && (CounterAmount > 0 || CarryingCost > 0);
        public string Explanation;
    }

    /// <summary>
    /// ฝั่งซื้อ (W4): รับของ (Product/In) ↔ ใบสำคัญจ่ายที่ผูก (Account/PaymentVoucher, Dr GRNI แทนค่าใช้จ่าย) ↔
    /// ใบซื้อเชื่อ (Account/CreditPurchasePayment) ↔ คืนสินค้าให้ผู้ขาย (Product/PurchaseReturn).
    /// ทุกคอลัมน์ใหม่ (PHASE19_31) ตรวจก่อนใช้ — ยังไม่รัน migration = ทุกเมธอดคืนค่าว่าง/false (พฤติกรรมเดิม).
    /// </summary>
    public static class PurchaseService
    {
        public const string StatusUnpaid = "UNPAID", StatusPaid = "PAID", StatusCredit = "CREDIT";
        public const string QtySourceTakeTime = "TAKETIME", QtySourceNextAccOcr = "NEXTACC_OCR";
        /// <summary>คำนำหน้าชื่อหมวดของบรรทัดใบสำคัญจ่ายที่ล้างรับของ — ใช้ระบุบรรทัดตอนบันทึก/แก้ไข</summary>
        public const string GrniLinePrefix = "🔄 ล้างรับของ";

        // ── schema guard (cache 5 นาที) ──
        private static readonly object _lock = new object();
        private static Dictionary<string, bool> _colCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _colCacheAt = DateTime.MinValue;

        public static bool HasColumn(string conn, string table, string column)
        {
            string key = table + "." + column;
            lock (_lock)
            {
                if (DateTime.Now - _colCacheAt > TimeSpan.FromMinutes(5)) { _colCache.Clear(); _colCacheAt = DateTime.Now; }
                bool v;
                if (_colCache.TryGetValue(key, out v)) return v;
            }
            bool exists = false;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT CASE WHEN COL_LENGTH(@t, @c) IS NULL THEN 0 ELSE 1 END",
                    new Dictionary<string, object> { { "@t", "dbo." + table }, { "@c", column } });
                exists = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 1;
            }
            catch { exists = false; }
            lock (_lock) { _colCache[key] = exists; }
            return exists;
        }

        public static void InvalidateSchemaCache() { lock (_lock) { _colCache.Clear(); } }

        /// <summary>Product_In มีคอลัมน์ PHASE19_31 ครบ</summary>
        public static bool HasPurchaseColumns(string conn) =>
            HasColumn(conn, "Product_In", "Receipt_Batch") && HasColumn(conn, "Product_In", "Payment_Status");
        public static bool HasVoucherLink(string conn) => HasColumn(conn, "Account_Payment", "Linked_Goods_Receipt");
        public static bool HasCreditPaidColumns(string conn) => HasColumn(conn, "Account_Payment", "Credit_Paid_Date");
        public static bool HasReturnColumns(string conn) =>
            HasColumn(conn, "Product_Out", "Purchase_Return_Ref") && HasColumn(conn, "Product_Out", "Return_Receipt_Batch");
        public static bool HasMovementLink(string conn) => HasColumn(conn, "Product_In", "Nexaacc_Movement_Id");

        // ── ids ──
        private static readonly Random _rnd = new Random();
        private static string Rand3() { lock (_rnd) { return _rnd.Next(100, 999).ToString(); } }
        public static string NewReceiptBatchId(DateTime at) => $"GR-{at:yyyyMMdd-HHmmss}-{Rand3()}";
        public static string NewReturnRef(DateTime at) => $"PRT-{at:yyyyMMdd-HHmmss}-{Rand3()}";

        public static bool IsThaiTaxId(string taxId)
        {
            string d = Regex.Replace(taxId ?? "", @"\D", "");
            return d.Length == 13;
        }

        // ── config ──
        /// <summary>แหล่งจริงของจำนวนสินค้าที่ซื้อเข้า (Nexaacc_Purchase_Qty_Source): TAKETIME (default) | NEXTACC_OCR</summary>
        public static string QtySource(Integration.AccountingConfig cfg)
        {
            string v = (cfg?.GetConfigValue("Nexaacc_Purchase_Qty_Source", QtySourceTakeTime) ?? QtySourceTakeTime).Trim().ToUpperInvariant();
            return v == QtySourceNextAccOcr ? QtySourceNextAccOcr : QtySourceTakeTime;
        }

        /// <summary>
        /// บัญชีที่บรรทัด "ชำระค่าสินค้าที่รับเข้าแล้ว" ต้องเดบิต (TakeTime mapping key) — ให้หักล้างสิ่งที่ Product/In โพสต์ไว้พอดี:
        ///   • Nexaacc_StockIn_SkipJournal=1 / voucher LOCAL → รับของไม่โพสต์ GL ⇒ ใบกำกับต้อง Dr <b>INVENTORY</b> (perpetual)
        ///   • GR/IR เปิด (default)                         → รับของ Cr GRNI ⇒ Dr <b>GRNI</b> (ล้างพักรับของ)
        ///   • GR/IR ปิด                                     → รับของ Cr เจ้าหนี้ (ราคาสุทธิ) ⇒ Dr <b>ACCOUNTS_PAYABLE</b>
        /// (ภาษีซื้อยังลงที่ใบสำคัญจ่ายตามปกติ — รับของไม่เคยลง VAT)
        /// </summary>
        public static string ClearingAccountKey(Integration.AccountingConfig cfg)
        {
            if (cfg == null) return "GRNI";
            if (cfg.IsStockInSkipJournal || cfg.IsVoucherLocal) return "INVENTORY";
            return cfg.IsStockInUseGRNI ? "GRNI" : "ACCOUNTS_PAYABLE";
        }

        public static string ClearingAccountLabel(string key)
        {
            switch (key)
            {
                case "INVENTORY": return "สินค้าคงเหลือ (ใบกำกับลงสต๊อก — รับของไม่โพสต์ JE)";
                case "ACCOUNTS_PAYABLE": return "เจ้าหนี้การค้า (รับของตั้งเจ้าหนี้แล้ว)";
                default: return "GRNI พักรับของ";
            }
        }

        /// <summary>(GUID, code) ของ mapping key จาก Accounting_Account_Mapping — ไม่มี = (null, null)</summary>
        public static Tuple<string, string> LookupMappedAccount(string conn, string takeTimeCode)
        {
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    @"SELECT TOP 1 CAST(Nexaacc_AccountId AS NVARCHAR(50)) AS Id, Nexaacc_AccountCode AS Code
                      FROM Accounting_Account_Mapping WHERE TakeTime_Code = @k AND Is_Active = 1",
                    new Dictionary<string, object> { { "@k", takeTimeCode } });
                if (dt != null && dt.Rows.Count > 0)
                {
                    string id = dt.Rows[0]["Id"] == DBNull.Value ? null : dt.Rows[0]["Id"].ToString();
                    string c = dt.Rows[0]["Code"] == DBNull.Value ? null : dt.Rows[0]["Code"].ToString();
                    if (!string.IsNullOrEmpty(c) && string.IsNullOrEmpty(id))
                    {
                        // ยังไม่ได้ดึงผังบัญชี — หา GUID จาก cache ผังบัญชี NextAcc
                        var dt2 = new code().DatabaseQuerySafe(conn,
                            "SELECT TOP 1 CAST(Nexaacc_AccountId AS NVARCHAR(50)) FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @c",
                            new Dictionary<string, object> { { "@c", c } });
                        if (dt2 != null && dt2.Rows.Count > 0 && dt2.Rows[0][0] != DBNull.Value) id = dt2.Rows[0][0].ToString();
                    }
                    return Tuple.Create(id, c);
                }
            }
            catch { }
            return Tuple.Create<string, string>(null, null);
        }

        // ── อ่าน batch ──

        /// <summary>batch ที่ยังไม่มีใบสำคัญจ่าย "ที่ยังใช้งานอยู่" ผูก (ใบที่ถูกยกเลิก/ลบไป = ผูกใหม่ได้)</summary>
        private const string OpenLinkSql =
            "(pi.Linked_Voucher_Id IS NULL OR NOT EXISTS (SELECT 1 FROM Account_Payment apx WHERE apx.ID = pi.Linked_Voucher_Id AND ISNULL(apx.Status, N'Normal') <> N'Cancel'))";

        /// <summary>
        /// รายการรับของ (กลุ่มตาม Receipt_Batch). openForPayment=true → เฉพาะ batch ที่ยังไม่ผูกใบสำคัญจ่าย
        /// (ไม่รวม PAID ที่ไม่ได้ผูก — ถือว่าจ่ายนอกระบบไม่ได้; ยังโชว์เพื่อให้ผูกย้อนหลังได้).
        /// </summary>
        public static DataTable ListBatches(string conn, bool openForPayment, int top = 200)
        {
            if (!HasPurchaseColumns(conn)) return null;
            try
            {
                string where = "pi.Receipt_Batch IS NOT NULL" + (openForPayment ? " AND " + OpenLinkSql : "");
                return new code().DatabaseQuerySafe(conn,
                    $@"SELECT TOP (@n) pi.Receipt_Batch AS Batch,
                              MIN(pi.DateTime_In) AS Received_At,
                              MAX(pi.Supplier_Name) AS Supplier_Name,
                              MAX(pi.Supplier_TaxId) AS Supplier_TaxId,
                              MAX(pi.Supplier_Doc_No) AS Supplier_Doc_No,
                              MAX(pi.Supplier_Doc_Date) AS Supplier_Doc_Date,
                              CAST(SUM(CAST(pi.Amount AS DECIMAL(18,4)) * CAST(pi.PricePerUnit AS DECIMAL(18,4))) AS DECIMAL(18,2)) AS Net_Total,
                              MAX(ISNULL(pi.Vat_Amount, 0)) AS Vat_Amount,
                              MAX(pi.Payment_Status) AS Payment_Status,
                              MAX(pi.Linked_Voucher_Id) AS Linked_Voucher_Id,
                              COUNT(*) AS Item_Count
                       FROM Product_In pi
                       WHERE {where}
                       GROUP BY pi.Receipt_Batch
                       ORDER BY MIN(pi.DateTime_In) DESC",
                    new Dictionary<string, object> { { "@n", top } });
            }
            catch { return null; }
        }

        public static ReceiptBatchInfo GetBatch(string conn, string batch)
        {
            if (string.IsNullOrEmpty(batch) || !HasPurchaseColumns(conn)) return null;
            try
            {
                var c = new code();
                var dt = c.DatabaseQuerySafe(conn,
                    @"SELECT pi.Product_ID, MAX(p.Product_Name) AS Product_Name,
                             SUM(CAST(pi.Amount AS DECIMAL(18,4))) AS Qty,
                             CASE WHEN SUM(CAST(pi.Amount AS DECIMAL(18,4))) = 0 THEN 0
                                  ELSE SUM(CAST(pi.Amount AS DECIMAL(18,4)) * CAST(pi.PricePerUnit AS DECIMAL(18,4))) / SUM(CAST(pi.Amount AS DECIMAL(18,4))) END AS Unit_Cost,
                             MIN(pi.DateTime_In) AS Received_At,
                             MAX(pi.Supplier_Name) AS Supplier_Name, MAX(pi.Supplier_TaxId) AS Supplier_TaxId,
                             MAX(pi.Supplier_Doc_No) AS Supplier_Doc_No, MAX(pi.Supplier_Doc_Date) AS Supplier_Doc_Date,
                             MAX(ISNULL(pi.Vat_Amount, 0)) AS Vat_Amount, MAX(pi.Payment_Status) AS Payment_Status,
                             MAX(pi.Linked_Voucher_Id) AS Linked_Voucher_Id
                      FROM Product_In pi LEFT JOIN Product p ON p.ID = pi.Product_ID
                      WHERE pi.Receipt_Batch = @b
                      GROUP BY pi.Product_ID",
                    new Dictionary<string, object> { { "@b", batch } });
                if (dt == null || dt.Rows.Count == 0) return null;

                var info = new ReceiptBatchInfo { Batch = batch };
                var r0 = dt.Rows[0];
                info.ReceivedAt = r0["Received_At"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(r0["Received_At"]);
                info.SupplierName = r0["Supplier_Name"]?.ToString() ?? "";
                info.SupplierTaxId = r0["Supplier_TaxId"]?.ToString() ?? "";
                info.SupplierDocNo = r0["Supplier_Doc_No"]?.ToString() ?? "";
                info.SupplierDocDate = r0["Supplier_Doc_Date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r0["Supplier_Doc_Date"]);
                info.VatAmount = r0["Vat_Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(r0["Vat_Amount"]);
                info.PaymentStatus = r0["Payment_Status"] == DBNull.Value ? null : r0["Payment_Status"].ToString();
                info.LinkedVoucherId = r0["Linked_Voucher_Id"] == DBNull.Value ? null : r0["Linked_Voucher_Id"].ToString();
                if (info.LinkedVoucherId != null)
                {
                    // ใบสำคัญจ่ายที่ผูกถูกยกเลิก/ลบไปแล้ว = ถือว่ายังไม่ผูก
                    var dtV = c.DatabaseQuerySafe(conn,
                        "SELECT TOP 1 1 FROM Account_Payment WHERE ID = @v AND ISNULL(Status, N'Normal') <> N'Cancel'",
                        new Dictionary<string, object> { { "@v", info.LinkedVoucherId } });
                    if (dtV == null || dtV.Rows.Count == 0) info.LinkedVoucherId = null;
                }

                var returned = ReturnedQtyByProduct(conn, batch);
                foreach (DataRow r in dt.Rows)
                {
                    int pid = Convert.ToInt32(r["Product_ID"]);
                    decimal rq;
                    returned.TryGetValue(pid, out rq);
                    info.Lines.Add(new ReceiptLine
                    {
                        ProductId = pid,
                        ProductName = r["Product_Name"]?.ToString() ?? ("#" + pid),
                        Qty = Convert.ToDecimal(r["Qty"]),
                        UnitCost = Math.Round(Convert.ToDecimal(r["Unit_Cost"]), 4, MidpointRounding.AwayFromZero),
                        ReturnedQty = rq
                    });
                }
                return info;
            }
            catch { return null; }
        }

        public static Dictionary<int, decimal> ReturnedQtyByProduct(string conn, string batch)
        {
            var res = new Dictionary<int, decimal>();
            if (!HasReturnColumns(conn)) return res;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    @"SELECT Product_ID, SUM(CAST(Amount AS DECIMAL(18,4))) AS Qty FROM Product_Out
                      WHERE Return_Receipt_Batch = @b GROUP BY Product_ID",
                    new Dictionary<string, object> { { "@b", batch } });
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        res[Convert.ToInt32(r["Product_ID"])] = Convert.ToDecimal(r["Qty"]);
            }
            catch { }
            return res;
        }

        // ── ผูก/ปลดใบสำคัญจ่าย ──

        /// <summary>ผูก batch กับใบสำคัญจ่าย: Product_In.Linked_Voucher_Id + Payment_Status, Account_Payment.Linked_Goods_Receipt (batch แรก)</summary>
        public static void LinkBatchesToVoucher(string conn, IList<string> batches, string voucherId, bool isCredit)
        {
            if (batches == null || batches.Count == 0 || string.IsNullOrEmpty(voucherId)) return;
            var c = new code();
            if (HasPurchaseColumns(conn))
            {
                foreach (var b in batches.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                {
                    try
                    {
                        c.DatabaseInsertSafe(conn,
                            "UPDATE Product_In SET Linked_Voucher_Id = @v, Payment_Status = @s WHERE Receipt_Batch = @b",
                            new Dictionary<string, object> { { "@v", voucherId }, { "@s", isCredit ? StatusCredit : StatusPaid }, { "@b", b.Trim() } });
                    }
                    catch { }
                }
            }
            if (HasVoucherLink(conn))
            {
                try
                {
                    string first = batches.First(x => !string.IsNullOrWhiteSpace(x)).Trim();
                    if (first.Length > 50) first = first.Substring(0, 50);
                    c.DatabaseInsertSafe(conn,
                        "UPDATE Account_Payment SET Linked_Goods_Receipt = @b WHERE ID = @v",
                        new Dictionary<string, object> { { "@b", first }, { "@v", voucherId } });
                }
                catch { }
            }
        }

        /// <summary>ปลดการผูก (ก่อนบันทึกใบที่แก้ไข/ลบ) — batch กลับเป็น UNPAID ให้ผูกใหม่ได้</summary>
        public static void UnlinkVoucher(string conn, string voucherId)
        {
            if (string.IsNullOrEmpty(voucherId) || !HasPurchaseColumns(conn)) return;
            try
            {
                new code().DatabaseInsertSafe(conn,
                    "UPDATE Product_In SET Linked_Voucher_Id = NULL, Payment_Status = N'UNPAID' WHERE Linked_Voucher_Id = @v",
                    new Dictionary<string, object> { { "@v", voucherId } });
            }
            catch { }
        }

        /// <summary>batch ทั้งหมดที่ผูกกับใบสำคัญจ่ายนี้</summary>
        public static List<string> BatchesOfVoucher(string conn, string voucherId)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(voucherId) || !HasPurchaseColumns(conn)) return list;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT DISTINCT Receipt_Batch FROM Product_In WHERE Linked_Voucher_Id = @v AND Receipt_Batch IS NOT NULL",
                    new Dictionary<string, object> { { "@v", voucherId } });
                if (dt != null) foreach (DataRow r in dt.Rows) list.Add(r[0].ToString());
            }
            catch { }
            return list;
        }

        /// <summary>
        /// ใบสำคัญจ่ายที่ "ดูเหมือนค่าสินค้าที่รับเข้าสต๊อกแล้ว" แต่ไม่ได้ผูก: batch ที่ยังไม่ผูก ของผู้ขายเดียวกัน
        /// (เลขผู้เสียภาษีตรง หรือชื่อคล้าย) หรือรายละเอียดบรรทัดมีชื่อสินค้าใน batch. คืนรายการ batch (ล่าสุดก่อน, ≤ 5).
        /// </summary>
        public static List<string> FindUnlinkedMatches(string conn, string vendorName, string vendorTaxId, IEnumerable<string> lineTexts)
        {
            var res = new List<string>();
            if (!HasPurchaseColumns(conn)) return res;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    @"SELECT TOP 200 pi.Receipt_Batch, MAX(pi.Supplier_Name) AS Supplier_Name, MAX(pi.Supplier_TaxId) AS Supplier_TaxId,
                             MIN(pi.DateTime_In) AS Received_At,
                             STUFF((SELECT DISTINCT N'|' + ISNULL(p2.Product_Name, N'') FROM Product_In x
                                    LEFT JOIN Product p2 ON p2.ID = x.Product_ID
                                    WHERE x.Receipt_Batch = pi.Receipt_Batch FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, N'') AS Products
                      FROM Product_In pi
                      WHERE pi.Receipt_Batch IS NOT NULL AND " + OpenLinkSql + @"
                        AND pi.DateTime_In >= DATEADD(DAY, -120, GETDATE())
                      GROUP BY pi.Receipt_Batch
                      ORDER BY MIN(pi.DateTime_In) DESC", null);
                if (dt == null) return res;
                string vTax = Regex.Replace(vendorTaxId ?? "", @"\D", "");
                string vName = Normalize(vendorName);
                var texts = (lineTexts ?? Enumerable.Empty<string>()).Select(Normalize).Where(t => t.Length > 0).ToList();
                foreach (DataRow r in dt.Rows)
                {
                    string sTax = Regex.Replace(r["Supplier_TaxId"]?.ToString() ?? "", @"\D", "");
                    string sName = Normalize(r["Supplier_Name"]?.ToString());
                    bool supplierMatch = (vTax.Length == 13 && vTax == sTax)
                        || (vName.Length >= 3 && sName.Length >= 3 && (vName.Contains(sName) || sName.Contains(vName)));
                    bool productMatch = false;
                    foreach (var pn in (r["Products"]?.ToString() ?? "").Split('|'))
                    {
                        string n = Normalize(pn);
                        if (n.Length >= 3 && texts.Any(t => t.Contains(n))) { productMatch = true; break; }
                    }
                    if (supplierMatch || productMatch) res.Add(r["Receipt_Batch"].ToString());
                    if (res.Count >= 5) break;
                }
            }
            catch { }
            return res;
        }

        private static string Normalize(string s) =>
            Regex.Replace((s ?? "").ToLowerInvariant(), @"\bco\b|\bltd\b|[\s\.\,\(\)\-]|บริษัท|จำกัด|หจก|ห้างหุ้นส่วน|มหาชน", "");

        // ── คืนสินค้าให้ผู้ขาย ──

        /// <summary>
        /// คำนวณ JE คืนสินค้า (pure). settlement: OFFSET (หักเจ้าหนี้/รอใบลดหนี้) | REFUND (ผู้ขายคืนเงิน).
        ///   Cr สินค้าคงเหลือ = carrying (ค่าเฉลี่ย); Dr คู่บัญชี = ราคาซื้อสุทธิ (+ VAT ที่กลับ);
        ///   Cr ภาษีซื้อ = vatReversal (เฉพาะเมื่อเคยเคลมแล้ว = ผูกใบสำคัญจ่าย/ใบกำกับแล้ว หรือคืนเงินรวม VAT);
        ///   ผลต่าง (ราคาซื้อ − ค่าเฉลี่ย) → PURCHASE_RETURNS.
        /// คู่บัญชี: REFUND → แหล่งเงิน (CASH); OFFSET + ผูกใบแล้ว → เจ้าหนี้ (ACCOUNTS_PAYABLE);
        ///           OFFSET + ยังไม่ผูก → บัญชีที่รับของเครดิตไว้ (GRNI / ACCOUNTS_PAYABLE) และไม่กลับ VAT (ยังไม่เคยเคลม);
        ///           รับของไม่โพสต์ JE (SkipJournal/LOCAL) + ยังไม่ผูก → ไม่มี JE (ตัดจำนวนอย่างเดียว).
        /// </summary>
        public static PurchaseReturnPlan PlanReturn(string clearingKey, bool batchLinked, string settlement,
            decimal purchaseNet, decimal carryingCost, decimal batchNet, decimal batchVat, bool reverseVat)
        {
            var plan = new PurchaseReturnPlan
            {
                PurchaseNet = Math.Round(purchaseNet, 2, MidpointRounding.AwayFromZero),
                CarryingCost = Math.Round(carryingCost, 2, MidpointRounding.AwayFromZero)
            };
            bool refund = string.Equals(settlement, "REFUND", StringComparison.OrdinalIgnoreCase);
            decimal vat = 0m;
            if (reverseVat && batchVat > 0 && batchNet > 0)
                vat = Math.Round(batchVat * plan.PurchaseNet / batchNet, 2, MidpointRounding.AwayFromZero);

            if (refund)
            {
                plan.CounterKey = "CASH";
                plan.VatReversal = vat;
                plan.Explanation = "ผู้ขายคืนเงิน: Dr แหล่งเงิน / Cr สินค้าคงเหลือ (ค่าเฉลี่ย)" + (vat > 0 ? " / Cr ภาษีซื้อ" : "");
            }
            else if (batchLinked)
            {
                plan.CounterKey = "ACCOUNTS_PAYABLE";
                plan.VatReversal = vat;
                plan.Explanation = "หักเจ้าหนี้ (ใบกำกับ/ใบสำคัญจ่ายบันทึกแล้ว): Dr เจ้าหนี้ / Cr สินค้าคงเหลือ" + (vat > 0 ? " / Cr ภาษีซื้อ (§82/10)" : "");
            }
            else if (clearingKey == "INVENTORY")
            {
                plan.CounterKey = "NONE";
                plan.VatReversal = 0m;
                plan.Explanation = "รับของไม่ได้โพสต์ JE (invoice-only) และยังไม่มีใบกำกับ → ตัดจำนวนอย่างเดียว ไม่มี JE";
            }
            else
            {
                plan.CounterKey = clearingKey == "ACCOUNTS_PAYABLE" ? "ACCOUNTS_PAYABLE" : "GRNI";
                plan.VatReversal = 0m;   // รับของไม่เคยลงภาษีซื้อ → ไม่มีอะไรให้กลับ
                plan.Explanation = (plan.CounterKey == "GRNI"
                    ? "ยังไม่วางบิล: Dr GRNI (ลดยอดพักรับของ) / Cr สินค้าคงเหลือ"
                    : "ยังไม่วางบิล: Dr เจ้าหนี้ / Cr สินค้าคงเหลือ") + " — ไม่กลับภาษีซื้อ (ยังไม่เคยเคลม)";
            }
            return plan;
        }
    }
}
