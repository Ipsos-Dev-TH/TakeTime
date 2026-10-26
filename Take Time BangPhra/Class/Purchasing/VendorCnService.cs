using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Purchasing
{
    /// <summary>หนึ่งการคืนสินค้า (Purchase_Return_Cn_Link)</summary>
    public sealed class ReturnCnLink
    {
        public string ReturnRef;
        public string Batch;
        public string SupplierName;
        public string SupplierTaxId;
        public DateTime? ReturnDate;
        public decimal PurchaseNet;
        public decimal CarryingCost;
        public decimal VatAmount;
        public string CounterKey;
        public string PaidHow;
        public string PaidHowAccountId;
        public string CnStatus;
        public string PrtJournal;
        public Guid? CnScanId;
        public Guid? CnDocId;
        public string CnDocNo;
        public string CnSupplierDocNo;
        public decimal? CnInventoryNet;
        public decimal? DifAmount;
        public string RevRef;
        public string DifRef;
        public DateTime CreatedAt;
        public DateTime? LastRemindedAt;
        public bool IsRefund => CounterKey == "CASH";
    }

    /// <summary>ใบลดหนี้ผู้ขายที่บันทึกจากหน้า OCR แล้ว แต่ยังไม่ผูกการคืนสินค้า (ตัวเลือก "ผูก" ในหน้าคืนสินค้า)</summary>
    public sealed class UnlinkedVendorCn
    {
        public Guid ScanId;
        public Guid? DocId;
        public string DocNo;
        public string SupplierName;
        public string SupplierTaxId;
        public string SupplierDocNo;
        public decimal Total;
        public DateTime? DocDate;
    }

    /// <summary>ยอดกับผู้ขายหนึ่งราย (Vendor_Credit_Ledger)</summary>
    public sealed class VendorBalanceRow
    {
        public int Id { get; set; }
        public string EntryType { get; set; }
        public string VendorName { get; set; }
        public string VendorTaxId { get; set; }
        public int? VendorId { get; set; }
        public decimal Amount { get; set; }
        public decimal Remaining { get; set; }
        public string AccountCode { get; set; }
        public string SourceRef { get; set; }
        public string SourceDocNo { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// ข้อมูลใบลดหนี้ผู้ขาย ↔ คืนสินค้า ↔ ยอดกับผู้ขาย (PHASE19_39 — guarded: ไม่มีตาราง = คืนค่าว่าง/false ⇒ พฤติกรรมเดิม).
    /// กติกา (pure) อยู่ที่ <see cref="VendorCnPolicy"/>.
    /// </summary>
    public sealed class VendorCnService
    {
        public const string EtCnCredit = "CN_CREDIT", EtCnApplied = "CN_APPLIED", EtDnPayable = "DN_PAYABLE", EtDnPaid = "DN_PAID";
        public const string MapVendorCredit = "VENDOR_CREDIT_RECEIVABLE";

        private readonly string _conn;
        private readonly code _code = new code();

        public VendorCnService(string conn) { _conn = conn; }

        // ── schema guard (cache 5 นาที) ──
        private static readonly object _lock = new object();
        private static Dictionary<string, bool> _tbl = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _tblAt = DateTime.MinValue;

        private bool HasTable(string name)
        {
            lock (_lock)
            {
                if (DateTime.Now - _tblAt > TimeSpan.FromMinutes(5)) { _tbl.Clear(); _tblAt = DateTime.Now; }
                bool v;
                if (_tbl.TryGetValue(name, out v)) return v;
            }
            bool ok = false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT CASE WHEN OBJECT_ID(@t, 'U') IS NULL THEN 0 ELSE 1 END",
                    new Dictionary<string, object> { { "@t", "dbo." + name } });
                ok = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 1;
            }
            catch { ok = false; }
            lock (_lock) { _tbl[name] = ok; }
            return ok;
        }

        public bool HasLinkTable() => HasTable("Purchase_Return_Cn_Link");
        public bool HasLedgerTable() => HasTable("Vendor_Credit_Ledger");

        /// <summary>นโยบาย "ใบลดหนี้ผู้ขายเป็นตัวจริง" เปิดอยู่ (มีตาราง + Vendor_Cn_Primary ≠ 0)</summary>
        public bool CnPrimary()
        {
            if (!HasLinkTable()) return false;
            try { return new Integration.AccountingConfig(_conn).GetConfigValue("Vendor_Cn_Primary", "1") != "0"; }
            catch { return true; }
        }

        public static string Digits(string s) => Regex.Replace(s ?? "", @"\D", "");

        private static string Cut(string s, int n) => string.IsNullOrEmpty(s) ? s : (s.Length > n ? s.Substring(0, n) : s);

        // ──────────────────────────────────────────────────────────────
        // การคืนสินค้า
        // ──────────────────────────────────────────────────────────────

        /// <summary>บันทึกการคืนหนึ่งครั้ง (เรียกจากหน้าคืนสินค้าหลังตัดสต๊อก) — มีอยู่แล้ว = ไม่ทับ</summary>
        public bool RecordReturn(ReturnCnLink r, string user)
        {
            if (r == null || string.IsNullOrWhiteSpace(r.ReturnRef) || !HasLinkTable()) return false;
            try
            {
                _code.DatabaseInsertSafe(_conn,
                    @"IF NOT EXISTS (SELECT 1 FROM Purchase_Return_Cn_Link WHERE Return_Ref = @r)
                      INSERT INTO Purchase_Return_Cn_Link (Return_Ref, Receipt_Batch, Supplier_Name, Supplier_TaxId, Return_Date, Purchase_Net,
                             Carrying_Cost, Vat_Amount, Counter_Key, Paid_How, Paid_How_Account_Id, Cn_Status, Prt_Journal, Created_By, Created_At)
                      VALUES (@r, @b, @sn, @tx, @d, @net, @carry, @vat, @ck, @ph, @pha, @st, @pj, @u, GETDATE())",
                    new Dictionary<string, object>
                    {
                        { "@r", r.ReturnRef.Trim() }, { "@b", Cut(r.Batch, 50) }, { "@sn", Cut(r.SupplierName, 300) },
                        { "@tx", Cut(Digits(r.SupplierTaxId), 20) }, { "@d", (object)r.ReturnDate ?? DBNull.Value },
                        { "@net", VendorCnPolicy.R2(r.PurchaseNet) }, { "@carry", VendorCnPolicy.R2(r.CarryingCost) }, { "@vat", VendorCnPolicy.R2(r.VatAmount) },
                        { "@ck", Cut(r.CounterKey, 30) }, { "@ph", Cut(r.PaidHow, 100) }, { "@pha", Cut(r.PaidHowAccountId, 50) },
                        { "@st", r.CnStatus ?? ReturnCnStatus.Pending }, { "@pj", r.PrtJournal ?? ReturnPrtJournal.Full }, { "@u", Cut(user, 100) }
                    });
                return true;
            }
            catch (Exception ex)
            {
                try { _code.Logs(_conn, "VendorCn", "RecordReturn " + r.ReturnRef + " failed: " + ex.Message, user ?? "SYSTEM"); } catch { }
                return false;
            }
        }

        private const string LinkCols = @"Return_Ref, Receipt_Batch, Supplier_Name, Supplier_TaxId, Return_Date, Purchase_Net, Carrying_Cost, Vat_Amount,
                Counter_Key, Paid_How, Paid_How_Account_Id, Cn_Status, Prt_Journal, Cn_Scan_Id, Cn_Doc_Id, Cn_Doc_No, Cn_Supplier_Doc_No,
                Cn_Inventory_Net, Dif_Amount, Rev_Ref, Dif_Ref, Created_At, Last_Reminded_At";

        private static ReturnCnLink ToLink(DataRow r)
        {
            Func<string, string> s = c => r[c] == DBNull.Value ? null : r[c].ToString();
            Func<string, decimal> d = c => r[c] == DBNull.Value ? 0m : Convert.ToDecimal(r[c]);
            return new ReturnCnLink
            {
                ReturnRef = s("Return_Ref"), Batch = s("Receipt_Batch"), SupplierName = s("Supplier_Name"), SupplierTaxId = s("Supplier_TaxId"),
                ReturnDate = r["Return_Date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["Return_Date"]),
                PurchaseNet = d("Purchase_Net"), CarryingCost = d("Carrying_Cost"), VatAmount = d("Vat_Amount"),
                CounterKey = s("Counter_Key"), PaidHow = s("Paid_How"), PaidHowAccountId = s("Paid_How_Account_Id"),
                CnStatus = s("Cn_Status"), PrtJournal = s("Prt_Journal"),
                CnScanId = r["Cn_Scan_Id"] == DBNull.Value ? (Guid?)null : (Guid)r["Cn_Scan_Id"],
                CnDocId = r["Cn_Doc_Id"] == DBNull.Value ? (Guid?)null : (Guid)r["Cn_Doc_Id"],
                CnDocNo = s("Cn_Doc_No"), CnSupplierDocNo = s("Cn_Supplier_Doc_No"),
                CnInventoryNet = r["Cn_Inventory_Net"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["Cn_Inventory_Net"]),
                DifAmount = r["Dif_Amount"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["Dif_Amount"]),
                RevRef = s("Rev_Ref"), DifRef = s("Dif_Ref"),
                CreatedAt = r["Created_At"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(r["Created_At"]),
                LastRemindedAt = r["Last_Reminded_At"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["Last_Reminded_At"])
            };
        }

        public ReturnCnLink GetReturn(string returnRef)
        {
            if (string.IsNullOrWhiteSpace(returnRef) || !HasLinkTable()) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 " + LinkCols + " FROM Purchase_Return_Cn_Link WHERE Return_Ref = @r",
                    new Dictionary<string, object> { { "@r", returnRef.Trim() } });
                return dt != null && dt.Rows.Count > 0 ? ToLink(dt.Rows[0]) : null;
            }
            catch { return null; }
        }

        /// <summary>การคืนที่ผูกกับสแกนใบลดหนี้นี้ (ทำต่อหลังล้มกลางทาง / แสดงในหน้าเสร็จ)</summary>
        public ReturnCnLink GetByScan(Guid scanId)
        {
            if (!HasLinkTable()) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 " + LinkCols + " FROM Purchase_Return_Cn_Link WHERE Cn_Scan_Id = @s",
                    new Dictionary<string, object> { { "@s", scanId } });
                return dt != null && dt.Rows.Count > 0 ? ToLink(dt.Rows[0]) : null;
            }
            catch { return null; }
        }

        /// <summary>การคืนที่รอใบลดหนี้ (ล่าสุดก่อน) — ตัวเลือกในหน้า OCR</summary>
        public List<ReturnCnLink> ListPending(int top = 50)
        {
            var list = new List<ReturnCnLink>();
            if (!HasLinkTable()) return list;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP (@n) " + LinkCols + " FROM Purchase_Return_Cn_Link WHERE Cn_Status = N'PENDING' ORDER BY Created_At DESC",
                    new Dictionary<string, object> { { "@n", top } });
                if (dt != null) foreach (DataRow r in dt.Rows) list.Add(ToLink(r));
            }
            catch { }
            return list;
        }

        /// <summary>สถานะใบลดหนี้ของการคืนหลายรายการ (ตารางล่าสุดในหน้าคืนสินค้า)</summary>
        public Dictionary<string, ReturnCnLink> LoadMany(IEnumerable<string> refs)
        {
            var map = new Dictionary<string, ReturnCnLink>(StringComparer.OrdinalIgnoreCase);
            var list = (refs ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(200).ToList();
            if (list.Count == 0 || !HasLinkTable()) return map;
            try
            {
                var prm = new Dictionary<string, object>();
                var names = new List<string>();
                for (int i = 0; i < list.Count; i++) { names.Add("@r" + i); prm["@r" + i] = list[i]; }
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT " + LinkCols + " FROM Purchase_Return_Cn_Link WHERE Return_Ref IN (" + string.Join(",", names) + ")", prm);
                if (dt != null) foreach (DataRow r in dt.Rows) { var l = ToLink(r); map[l.ReturnRef] = l; }
            }
            catch { }
            return map;
        }

        /// <summary>
        /// ผูกการคืน ↔ ใบลดหนี้ (atomic: เฉพาะแถวที่ยัง PENDING หรือผูกกับสแกนเดียวกันอยู่แล้ว = ทำต่อ). คืน false = ถูกผูกกับใบอื่นไปแล้ว
        /// </summary>
        public bool MarkLinked(string returnRef, Guid? scanId, Guid? docId, string docNo, string supplierDocNo, decimal cnInventoryNet, decimal cnTotal,
            string revRef, string difRef, decimal difAmount, string prtJournal, string user)
        {
            if (string.IsNullOrWhiteSpace(returnRef) || !HasLinkTable()) return false;
            try
            {
                int n = _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Purchase_Return_Cn_Link
                         SET Cn_Status = N'LINKED', Cn_Scan_Id = @s, Cn_Doc_Id = @d, Cn_Doc_No = @no, Cn_Supplier_Doc_No = @sdn,
                             Cn_Inventory_Net = @inv, Cn_Total = @tot, Rev_Ref = @rev, Dif_Ref = @dif, Dif_Amount = @difA,
                             Prt_Journal = ISNULL(@pj, Prt_Journal), Linked_By = @u, Linked_At = ISNULL(Linked_At, GETDATE())
                       WHERE Return_Ref = @r
                         AND (Cn_Status = N'PENDING' OR (Cn_Status = N'NOT_REQUIRED' AND Prt_Journal = N'PENDING') OR (Cn_Status = N'LINKED' AND ((@s IS NOT NULL AND Cn_Scan_Id = @s) OR (@d IS NOT NULL AND Cn_Doc_Id = @d))))",
                    new Dictionary<string, object>
                    {
                        { "@r", returnRef.Trim() }, { "@s", (object)scanId ?? DBNull.Value }, { "@d", (object)docId ?? DBNull.Value },
                        { "@no", (object)Cut(docNo, 60) ?? DBNull.Value }, { "@sdn", (object)Cut(supplierDocNo, 100) ?? DBNull.Value },
                        { "@inv", VendorCnPolicy.R2(cnInventoryNet) }, { "@tot", VendorCnPolicy.R2(cnTotal) },
                        { "@rev", (object)Cut(revRef, 60) ?? DBNull.Value }, { "@dif", (object)Cut(difRef, 60) ?? DBNull.Value },
                        { "@difA", VendorCnPolicy.R2(difAmount) }, { "@pj", (object)prtJournal ?? DBNull.Value }, { "@u", Cut(user, 100) }
                    });
                return n > 0;
            }
            catch (Exception ex)
            {
                try { _code.Logs(_conn, "VendorCn", "MarkLinked " + returnRef + " failed: " + ex.Message, user ?? "SYSTEM"); } catch { }
                return false;
            }
        }

        /// <summary>
        /// ใบลดหนี้ผู้ขายที่ส่งจากหน้า OCR สำเร็จ (DONE) และยังไม่ผูกการคืนใด — สำหรับ "คืนสินค้าทีหลังใบลดหนี้" (CN-first).
        /// supplierTaxId ว่าง = ทุกราย (ผู้ขายตรงกันก่อน)
        /// </summary>
        public List<UnlinkedVendorCn> ListUnlinkedCns(string supplierTaxId, string supplierName, int top = 40)
        {
            var list = new List<UnlinkedVendorCn>();
            if (!HasLinkTable() || !HasTable("Ocr_Document_Log")) return list;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP (@n) l.Scan_Id, l.Nexaacc_Doc_Id, l.Nexaacc_Doc_No, l.Supplier_Name, l.Supplier_TaxId, l.Supplier_Doc_No,
                             ISNULL(l.Total_Amount, 0) AS Total_Amount, l.Doc_Date
                        FROM Ocr_Document_Log l
                       WHERE l.Doc_Kind = N'VCN' AND l.Status = N'DONE' AND l.Nexaacc_Doc_Id IS NOT NULL
                         AND NOT EXISTS (SELECT 1 FROM Purchase_Return_Cn_Link k WHERE k.Cn_Scan_Id = l.Scan_Id OR k.Cn_Doc_Id = l.Nexaacc_Doc_Id)
                       ORDER BY CASE WHEN @t <> N'' AND l.Supplier_TaxId = @t THEN 0 ELSE 1 END, ISNULL(l.Updated_At, l.Created_At) DESC",
                    new Dictionary<string, object> { { "@n", top }, { "@t", Digits(supplierTaxId) } });
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        list.Add(new UnlinkedVendorCn
                        {
                            ScanId = (Guid)r["Scan_Id"],
                            DocId = r["Nexaacc_Doc_Id"] == DBNull.Value ? (Guid?)null : (Guid)r["Nexaacc_Doc_Id"],
                            DocNo = r["Nexaacc_Doc_No"]?.ToString(), SupplierName = r["Supplier_Name"]?.ToString(),
                            SupplierTaxId = r["Supplier_TaxId"]?.ToString(), SupplierDocNo = r["Supplier_Doc_No"]?.ToString(),
                            Total = Convert.ToDecimal(r["Total_Amount"]),
                            DocDate = r["Doc_Date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["Doc_Date"])
                        });
            }
            catch { }
            return list;
        }

        /// <summary>อ่านค่าที่ยืนยันของใบลดหนี้จาก Ocr_Document_Log (Review_Json) — ใช้หายอดบรรทัดที่ลงบัญชีสินค้า</summary>
        public Integration.OcrReview.OcrReviewState LoadCnState(Guid scanId, out Guid? docId, out string docNo)
        {
            docId = null; docNo = null;
            if (!HasTable("Ocr_Document_Log")) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 Review_Json, Nexaacc_Doc_Id, Nexaacc_Doc_No, Status, Doc_Kind FROM Ocr_Document_Log WHERE Scan_Id = @s",
                    new Dictionary<string, object> { { "@s", scanId } });
                if (dt == null || dt.Rows.Count == 0) return null;
                var r = dt.Rows[0];
                if (!string.Equals(r["Doc_Kind"]?.ToString(), "VCN", StringComparison.Ordinal)) return null;
                if (r["Nexaacc_Doc_Id"] != DBNull.Value) docId = (Guid)r["Nexaacc_Doc_Id"];
                docNo = r["Nexaacc_Doc_No"] == DBNull.Value ? null : r["Nexaacc_Doc_No"].ToString();
                return Integration.OcrReview.OcrReviewService.ParseState(r["Review_Json"] == DBNull.Value ? null : r["Review_Json"].ToString());
            }
            catch { return null; }
        }

        /// <summary>ยอดก่อน VAT ของบรรทัดที่ลงบัญชีสินค้าคงเหลือ (รหัสบัญชีของ mapping INVENTORY)</summary>
        public static decimal InventoryNet(Integration.OcrReview.OcrReviewState s, string inventoryCode)
        {
            if (s?.Lines == null || string.IsNullOrWhiteSpace(inventoryCode)) return 0m;
            string inv = inventoryCode.Trim();
            return VendorCnPolicy.R2(s.Lines.Where(l => string.Equals((l.AccountCode ?? "").Trim(), inv, StringComparison.OrdinalIgnoreCase))
                .Sum(l => Integration.OcrReview.OcrReviewMath.CalcLine(l).Net));
        }

        /// <summary>รหัสบัญชีของ mapping key ("" = ไม่ผูก)</summary>
        public string MappedCode(string key)
        {
            var t = PurchaseService.LookupMappedAccount(_conn, key);
            return t.Item2 ?? "";
        }

        // ──────────────────────────────────────────────────────────────
        // ยอดกับผู้ขาย (Vendor_Credit_Ledger)
        // ──────────────────────────────────────────────────────────────

        /// <summary>เพิ่มรายการ (กันซ้ำด้วย UNIQUE(Entry_Type, Source_Ref) — มีแล้ว = ไม่เพิ่ม). คืน ID หรือ -1</summary>
        public int AddLedger(string entryType, int? vendorId, string taxId, string vendorName, Guid? contactId, decimal amount,
            string accountCode, string sourceRef, string sourceDocNo, int? parentId, string appliedVoucherId, string journalRef, string note, string user)
        {
            if (!HasLedgerTable() || string.IsNullOrWhiteSpace(sourceRef) || amount == 0m) return -1;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"IF NOT EXISTS (SELECT 1 FROM Vendor_Credit_Ledger WHERE Entry_Type = @t AND Source_Ref = @src)
                      BEGIN
                        INSERT INTO Vendor_Credit_Ledger (Entry_Type, Vendor_Id, Vendor_TaxId, Vendor_Name, Nexaacc_Contact_Id, Amount, Account_Code,
                               Source_Ref, Source_Doc_No, Parent_Id, Applied_Voucher_Id, Journal_Ref, Note, Created_By, Created_At)
                        VALUES (@t, @vid, @tx, @vn, @cid, @a, @acc, @src, @sdn, @pid, @av, @jr, @note, @u, GETDATE());
                        SELECT CAST(SCOPE_IDENTITY() AS INT);
                      END
                      ELSE SELECT TOP 1 ID FROM Vendor_Credit_Ledger WHERE Entry_Type = @t AND Source_Ref = @src",
                    new Dictionary<string, object>
                    {
                        { "@t", entryType }, { "@vid", vendorId.HasValue && vendorId.Value > 0 ? (object)vendorId.Value : DBNull.Value },
                        { "@tx", (object)Cut(Digits(taxId), 20) ?? DBNull.Value }, { "@vn", (object)Cut(vendorName, 300) ?? DBNull.Value },
                        { "@cid", (object)contactId ?? DBNull.Value }, { "@a", VendorCnPolicy.R2(amount) },
                        { "@acc", (object)Cut(accountCode, 20) ?? DBNull.Value }, { "@src", Cut(sourceRef.Trim(), 100) },
                        { "@sdn", (object)Cut(sourceDocNo, 100) ?? DBNull.Value }, { "@pid", (object)parentId ?? DBNull.Value },
                        { "@av", (object)Cut(appliedVoucherId, 50) ?? DBNull.Value }, { "@jr", (object)Cut(journalRef, 60) ?? DBNull.Value },
                        { "@note", (object)Cut(note, 500) ?? DBNull.Value }, { "@u", Cut(user, 100) }
                    });
                return dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value ? Convert.ToInt32(dt.Rows[0][0]) : -1;
            }
            catch (Exception ex)
            {
                try { _code.Logs(_conn, "VendorCn", "AddLedger " + entryType + " " + sourceRef + " failed: " + ex.Message, user ?? "SYSTEM"); } catch { }
                return -1;
            }
        }

        /// <summary>เงื่อนไขจับคู่ผู้ขาย: เลขผู้เสียภาษี 13 หลักตรง หรือ Vendor.ID ตรง หรือ (ไม่มีเลขทั้งคู่) ชื่อตรง</summary>
        private static string VendorWhere(Dictionary<string, object> prm, int? vendorId, string taxId, string name)
        {
            string tx = Digits(taxId);
            prm["@vtx"] = tx;
            prm["@vid"] = vendorId.HasValue && vendorId.Value > 0 ? vendorId.Value : -1;
            prm["@vn"] = (name ?? "").Trim();
            return "((@vtx <> N'' AND LEN(@vtx) = 13 AND Vendor_TaxId = @vtx) OR (@vid > 0 AND Vendor_Id = @vid)"
                 + " OR (@vn <> N'' AND ISNULL(Vendor_TaxId, N'') = N'' AND Vendor_Name = @vn))";
        }

        /// <summary>เครดิตคงเหลือของผู้ขาย (Σ CN_CREDIT + Σ CN_APPLIED)</summary>
        public decimal AvailableCredit(int? vendorId, string taxId, string name)
        {
            if (!HasLedgerTable()) return 0m;
            try
            {
                var prm = new Dictionary<string, object>();
                string w = VendorWhere(prm, vendorId, taxId, name);
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT ISNULL(SUM(Amount), 0) FROM Vendor_Credit_Ledger WHERE Entry_Type IN (N'CN_CREDIT', N'CN_APPLIED') AND " + w, prm);
                return dt != null && dt.Rows.Count > 0 ? Math.Max(0m, VendorCnPolicy.R2(Convert.ToDecimal(dt.Rows[0][0]))) : 0m;
            }
            catch { return 0m; }
        }

        /// <summary>บัญชีที่เครดิตของผู้ขายรายนี้นั่งอยู่ (แถว CN_CREDIT ล่าสุด) — ใช้ตอนหัก (ต้องกลับบัญชีเดียวกับที่ลงไว้)</summary>
        public string CreditAccountCode(int? vendorId, string taxId, string name)
        {
            if (!HasLedgerTable()) return null;
            try
            {
                var prm = new Dictionary<string, object>();
                string w = VendorWhere(prm, vendorId, taxId, name);
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 Account_Code FROM Vendor_Credit_Ledger WHERE Entry_Type = N'CN_CREDIT' AND Account_Code IS NOT NULL AND " + w + " ORDER BY ID DESC", prm);
                return dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value ? dt.Rows[0][0].ToString() : null;
            }
            catch { return null; }
        }

        /// <summary>ใบเพิ่มหนี้ที่ตั้งเจ้าหนี้รอจ่าย (ยังมียอดคงเหลือ)</summary>
        public List<VendorBalanceRow> ListOpenDnPayables(int top = 100)
        {
            var list = new List<VendorBalanceRow>();
            if (!HasLedgerTable()) return list;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP (@n) p.ID, p.Entry_Type, p.Vendor_Name, p.Vendor_TaxId, p.Vendor_Id, p.Amount, p.Account_Code, p.Source_Ref, p.Source_Doc_No, p.Created_At,
                             p.Amount + ISNULL((SELECT SUM(x.Amount) FROM Vendor_Credit_Ledger x WHERE x.Entry_Type = N'DN_PAID' AND x.Parent_Id = p.ID), 0) AS Remaining
                        FROM Vendor_Credit_Ledger p
                       WHERE p.Entry_Type = N'DN_PAYABLE'
                         AND p.Amount + ISNULL((SELECT SUM(x.Amount) FROM Vendor_Credit_Ledger x WHERE x.Entry_Type = N'DN_PAID' AND x.Parent_Id = p.ID), 0) > 0.005
                       ORDER BY p.Created_At",
                    new Dictionary<string, object> { { "@n", top } });
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        list.Add(new VendorBalanceRow
                        {
                            Id = Convert.ToInt32(r["ID"]), EntryType = r["Entry_Type"]?.ToString(), VendorName = r["Vendor_Name"]?.ToString(),
                            VendorTaxId = r["Vendor_TaxId"]?.ToString(), VendorId = r["Vendor_Id"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["Vendor_Id"]),
                            Amount = Convert.ToDecimal(r["Amount"]), Remaining = Convert.ToDecimal(r["Remaining"]),
                            AccountCode = r["Account_Code"]?.ToString(), SourceRef = r["Source_Ref"]?.ToString(), SourceDocNo = r["Source_Doc_No"]?.ToString(),
                            CreatedAt = Convert.ToDateTime(r["Created_At"])
                        });
            }
            catch { }
            return list;
        }

        public VendorBalanceRow GetDnPayable(int id) => ListOpenDnPayables(500).FirstOrDefault(x => x.Id == id);

        /// <summary>สรุปเครดิตคงเหลือรายผู้ขาย (ตรวจสุขภาพ/หน้าจ่าย)</summary>
        public List<VendorBalanceRow> ListVendorCredits(int top = 50)
        {
            var list = new List<VendorBalanceRow>();
            if (!HasLedgerTable()) return list;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP (@n) MAX(Vendor_Name) AS Vendor_Name, ISNULL(Vendor_TaxId, N'') AS Vendor_TaxId, SUM(Amount) AS Remaining
                        FROM Vendor_Credit_Ledger WHERE Entry_Type IN (N'CN_CREDIT', N'CN_APPLIED')
                       GROUP BY ISNULL(Vendor_TaxId, N''), CASE WHEN ISNULL(Vendor_TaxId, N'') = N'' THEN Vendor_Name ELSE N'' END
                      HAVING SUM(Amount) > 0.005 ORDER BY SUM(Amount) DESC",
                    new Dictionary<string, object> { { "@n", top } });
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        list.Add(new VendorBalanceRow { VendorName = r["Vendor_Name"]?.ToString(), VendorTaxId = r["Vendor_TaxId"]?.ToString(), Remaining = Convert.ToDecimal(r["Remaining"]) });
            }
            catch { }
            return list;
        }

        // ──────────────────────────────────────────────────────────────
        // เตือน + ตรวจสุขภาพ
        // ──────────────────────────────────────────────────────────────

        public int ReminderDays()
        {
            try
            {
                int d;
                return int.TryParse(new Integration.AccountingConfig(_conn).GetConfigValue("Vendor_Cn_Reminder_Days", "7"), out d) && d >= 1 && d <= 120 ? d : 7;
            }
            catch { return 7; }
        }

        /// <summary>การคืนที่รอใบลดหนี้เกินกำหนด และถึงรอบเตือน</summary>
        public List<ReturnCnLink> DueReminders()
        {
            int days = ReminderDays();
            DateTime now = DateTime.Now;
            return ListPending(200).Where(r => VendorCnPolicy.ReminderDue(r.CreatedAt, r.LastRemindedAt, now, days)).ToList();
        }

        /// <summary>ปิดรายการรอใบลดหนี้ (ผู้ขายไม่ออก) — JE PRT ที่ลงไว้ (ไม่กลับ VAT) เป็นรายการจริง; ภาษีซื้อคงเดิมเพราะไม่มีเอกสารลด</summary>
        public bool CloseWithoutCn(string returnRef, string user)
        {
            if (string.IsNullOrWhiteSpace(returnRef) || !HasLinkTable()) return false;
            try
            {
                int n = _code.DatabaseInsertSafe(_conn,
                    "UPDATE Purchase_Return_Cn_Link SET Cn_Status = N'NOT_REQUIRED', Linked_By = @u, Linked_At = GETDATE() WHERE Return_Ref = @r AND Cn_Status = N'PENDING'",
                    new Dictionary<string, object> { { "@r", returnRef.Trim() }, { "@u", Cut(user, 100) } });
                if (n > 0) _code.Logs(_conn, "VendorCn", "ปิดรายการรอใบลดหนี้ " + returnRef + " (ผู้ขายไม่ออกใบลดหนี้ — ภาษีซื้อคงเดิม)", user ?? "SYSTEM");
                return n > 0;
            }
            catch { return false; }
        }

        public void MarkReminded(IEnumerable<string> refs)
        {
            if (!HasLinkTable()) return;
            foreach (var r in refs ?? Enumerable.Empty<string>())
            {
                try
                {
                    _code.DatabaseInsertSafe(_conn, "UPDATE Purchase_Return_Cn_Link SET Last_Reminded_At = GETDATE() WHERE Return_Ref = @r",
                        new Dictionary<string, object> { { "@r", r } });
                }
                catch { }
            }
        }

        /// <summary>ข้อ 12 ของตรวจสุขภาพ (หน้า Accounting Integration)</summary>
        public static void AppendHealthIssues(string conn, bool haveCoa, Action<string, string, string> add)
        {
            try
            {
                var svc = new VendorCnService(conn);
                if (!svc.HasLinkTable() || !svc.HasLedgerTable())
                {
                    add("info", "ใบลดหนี้ผู้ขาย ↔ คืนสินค้า: ยังไม่รัน migration PHASE19_39",
                        "รัน Database/PHASE19_Migration_39_Vendor_CN_Links.sql เพื่อให้ใบลดหนี้ใน NextAcc เป็นรายการจริงของการคืนสินค้า (กลับ PRT อัตโนมัติ) "
                        + "และหักเครดิตผู้ขายตอนจ่ายใบซื้อเชื่อได้ — ยังไม่รัน = ใบลดหนี้ของการคืนเก็บเป็นหลักฐาน (เดิม)");
                    return;
                }
                string vcr = svc.MappedCode(MapVendorCredit);
                if (string.IsNullOrWhiteSpace(vcr))
                    add("warn", "ยังไม่ผูกบัญชี VENDOR_CREDIT_RECEIVABLE (ลูกหนี้ผู้ขาย/เครดิตจากผู้ขาย)",
                        "ใบลดหนี้ผู้ขายที่ \"เก็บเป็นเครดิตกับผู้ขาย\" จะลงเจ้าหนี้การค้าเป็นยอดเดบิตแทน (GL ถูก แต่ปนกับยอดค้างจ่าย) "
                        + "— สร้างบัญชีสินทรัพย์หมุนเวียน เช่น 11xxx \"ลูกหนี้ผู้ขาย/เครดิตจากผู้ขาย\" ใน NextAcc แล้วผูก mapping ด้านล่างของหน้านี้");
                else if (haveCoa)
                {
                    var dt = new code().DatabaseQuerySafe(conn, "SELECT TOP 1 1 FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @c",
                        new Dictionary<string, object> { { "@c", vcr } });
                    if (dt == null || dt.Rows.Count == 0)
                        add("error", "บัญชี VENDOR_CREDIT_RECEIVABLE (" + vcr + ") ไม่มีในผังบัญชี NextAcc", "สร้างบัญชีใน NextAcc หรือแก้ mapping แล้วกด Sync บัญชี");
                }
                string inv = svc.MappedCode("INVENTORY");
                if (string.IsNullOrWhiteSpace(inv))
                    add("warn", "ยังไม่ผูกบัญชี INVENTORY", "ใบลดหนี้ของการคืนสินค้าต้องลงบรรทัดที่บัญชีสินค้าคงเหลือ — ผูก mapping INVENTORY ก่อนผูกใบลดหนี้กับการคืน");

                int days = svc.ReminderDays();
                var overdue = svc.ListPending(200).Where(r => (DateTime.Now - r.CreatedAt).TotalDays >= days).ToList();
                if (overdue.Count > 0)
                    add("warn", $"คืนสินค้าแล้วยังไม่ได้ใบลดหนี้เกิน {days} วัน ({overdue.Count} รายการ)",
                        string.Join("\n", overdue.Take(10).Select(r => $"{r.ReturnRef} · {r.SupplierName} · {r.PurchaseNet:N2} (+VAT {r.VatAmount:N2}) · คืนเมื่อ {r.CreatedAt:dd/MM/yyyy}"))
                        + "\n\nขอใบลดหนี้จากผู้ขาย แล้วสแกนที่หน้า \"สแกนเอกสาร OCR\" เลือกชนิดใบลดหนี้ + ผูกเลข PRT — ภาษีซื้อจะลดในเดือนที่ได้รับใบ (§82/10) "
                        + "· ถ้าผู้ขายไม่ออกใบลดหนี้จริง: หน้า \"คืนสินค้าให้ผู้ขาย\" → \"ปิดรายการรอใบลดหนี้\" (ภาษีซื้อคงเดิม ไม่มีเอกสารลด)");
                var credits = svc.ListVendorCredits(10);
                if (credits.Count > 0)
                    add("info", $"เครดิตกับผู้ขายคงเหลือ {credits.Sum(c => c.Remaining):N2} บาท ({credits.Count} ราย)",
                        string.Join("\n", credits.Select(c => $"{c.VendorName} {(string.IsNullOrEmpty(c.VendorTaxId) ? "" : "(" + c.VendorTaxId + ")")} · {c.Remaining:N2}"))
                        + "\n\nหักได้ตอนบันทึกจ่ายใบซื้อเชื่อ (หน้า ชำระใบซื้อเชื่อ)");
                var dn = svc.ListOpenDnPayables(20);
                if (dn.Count > 0)
                    add("info", $"ใบเพิ่มหนี้ผู้ขายที่ตั้งเจ้าหนี้รอจ่าย {dn.Sum(x => x.Remaining):N2} บาท ({dn.Count} ใบ)",
                        string.Join("\n", dn.Take(10).Select(x => $"{x.SourceDocNo} · {x.VendorName} · {x.Remaining:N2}")) + "\n\nจ่ายได้ที่หน้า ชำระใบซื้อเชื่อ (ส่วนใบเพิ่มหนี้รอจ่าย)");
            }
            catch { }
        }
    }
}
