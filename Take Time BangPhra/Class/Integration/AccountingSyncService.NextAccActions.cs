using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>ผลของงาน NextAcc ที่สั่งจากหน้าจอ (ข้อ 25)</summary>
    public sealed class NxActionResult
    {
        public bool Ok;
        /// <summary>QUEUED | DONE | BLOCKED | FAILED</summary>
        public string Status;
        public string Message;
        public string Path;
        public long QueueId;
        public long ActionId;
        public Guid? ResultDocId;
        public string ResultDocNo;
        public string RuleCode;

        public static NxActionResult Fail(string status, string msg) { return new NxActionResult { Ok = false, Status = status, Message = msg }; }
    }

    /// <summary>รายละเอียดเอกสารภาษีซื้อค้าง + ผู้ติดต่อ (หน้า UndueInputVat)</summary>
    public sealed class NxUndueDetail
    {
        public DocumentResponse Doc;
        public ContactResponse Contact;
        public NxUndueInputVatSummary Summary;
    }

    /// <summary>สิทธิ์งานบัญชี NextAcc ข้อ 25: Owner เสมอ · Admin ที่มีสิทธิ์ตั้งค่าบัญชี/รายงานบัญชี/ใบเสร็จ (กลุ่มเดียวกับผู้ยืนยันข้ามข้อ 21 + FIN_RECEIPT)</summary>
    public static class NextAccActionAccess
    {
        public static bool CanAct()
        {
            try
            {
                string role = HttpContext.Current?.Session?["User"]?.ToString();
                if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)) return true;
                return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                    && (Perm.CanAccess(Perm.SysAccounting) || Perm.CanAccess(Perm.FinReport) || Perm.CanAccess(Perm.FinReceipt));
            }
            catch { return false; }
        }

        /// <summary>หน้าภาษีซื้อค้าง: Owner / Admin ที่มีสิทธิ์บัญชี (SYS_ACCOUNTING หรือ FIN_REPORT)</summary>
        public static bool CanAccounting()
        {
            try
            {
                string role = HttpContext.Current?.Session?["User"]?.ToString();
                if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)) return true;
                return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                    && (Perm.CanAccess(Perm.SysAccounting) || Perm.CanAccess(Perm.FinReport));
            }
            catch { return false; }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    //  CLAUDE.md ข้อ 25 — งาน NextAcc ที่สั่งจากหน้าจอ (สัญญา/บรรทัดอ้างอิงอยู่ที่ NextAccActions.cs + AccountingApiClient)
    //   ① ISSUE_FULL_TAX_INVOICE    (คิว, Entity RECEIPT) — ลูกค้าขอใบกำกับเต็มรูปทีหลัง
    //   ② RECLASSIFY_PAYMENT_SOURCE (คิว, Entity RECEIPT) — แก้แหล่งเงินที่เลือกผิด ไม่ void
    //   ③ ภาษีซื้อค้าง 11640 — อ่าน/เติมตรง (complete-tax-invoice idempotent ฝั่ง NextAcc)
    //   ④ e-Tax ใบลดหนี้ + ส่งอีเมลเอกสาร — ตรง (generate ไม่ซ้ำ: NextAcc ปฏิเสธ "มี e-Tax แล้ว")
    //  คิว ①② ใช้ Idempotency-Key ตามข้อ 19 (บริบท {เลขใบ}-q{queueId}; เส้นทางอยู่ใน CreateRouteRegex) และถูกประมวลผลทันทีจากหน้า
    //  (ProcessNxActionNowAsync) — worker หยิบต่อได้ถ้าหน้าไม่ทัน (atomic claim เดียวกัน)
    // ══════════════════════════════════════════════════════════════════════════════════════════
    public partial class AccountingSyncService
    {
        internal const string ActIssueFullTaxInvoice = "ISSUE_FULL_TAX_INVOICE";
        internal const string ActReclassifyPaymentSource = "RECLASSIFY_PAYMENT_SOURCE";
        private const string NxActEntity = "RECEIPT";
        public const string CfgCreditNoteEtaxAuto = "CreditNote_Etax_Auto";

        private static readonly object _nxSchemaLock = new object();
        private static DateTime _nxSchemaAt = DateTime.MinValue;
        private static bool _nxActionTable, _nxCnEtaxCols;

        private void NxRefreshSchema()
        {
            lock (_nxSchemaLock)
            {
                if ((DateTime.Now - _nxSchemaAt).TotalSeconds < 60) return;
            }
            bool t = false, c = false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT CASE WHEN OBJECT_ID('dbo.Nexaacc_Doc_Action','U') IS NOT NULL THEN 1 ELSE 0 END AS T,
                             CASE WHEN COL_LENGTH('dbo.Credit_Note','Etax_Status') IS NOT NULL THEN 1 ELSE 0 END AS C", null);
                if (dt != null && dt.Rows.Count > 0)
                {
                    t = Convert.ToInt32(dt.Rows[0]["T"]) == 1;
                    c = Convert.ToInt32(dt.Rows[0]["C"]) == 1;
                }
            }
            catch { }
            lock (_nxSchemaLock) { _nxActionTable = t; _nxCnEtaxCols = c; _nxSchemaAt = DateTime.Now; }
        }

        public bool NxActionTableReady { get { NxRefreshSchema(); return _nxActionTable; } }
        public bool CreditNoteEtaxColumnsReady { get { NxRefreshSchema(); return _nxCnEtaxCols; } }

        // ── ประวัติงาน (Nexaacc_Doc_Action) ───────────────────────────────────────────────────────

        private long NxActionInsert(string action, string receiptId, int? cnId, Guid? srcDocId, string srcDocNo, string requestJson, string by)
        {
            if (!NxActionTableReady) return 0;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"INSERT INTO dbo.Nexaacc_Doc_Action (Action, Receipt_ID, Credit_Note_ID, Source_Doc_Id, Source_Doc_No, Status, Request_Json, Created_By, Created_At)
                      VALUES (@a, @r, @cn, @sd, @sn, 'QUEUED', @j, @by, GETDATE());
                      SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS NewID;",
                    new Dictionary<string, object>
                    {
                        { "@a", action }, { "@r", (object)NxCut(receiptId, 50) ?? DBNull.Value },
                        { "@cn", cnId.HasValue ? (object)cnId.Value : DBNull.Value },
                        { "@sd", srcDocId.HasValue && srcDocId.Value != Guid.Empty ? (object)srcDocId.Value : DBNull.Value },
                        { "@sn", (object)NxCut(srcDocNo, 60) ?? DBNull.Value },
                        { "@j", (object)requestJson ?? DBNull.Value }, { "@by", (object)NxCut(by, 100) ?? DBNull.Value }
                    });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value) return Convert.ToInt64(dt.Rows[0][0]);
            }
            catch (Exception ex) { _code.Logs(_connectionString, "NextAccActions", "บันทึกประวัติไม่ได้: " + ex.Message, "SYSTEM"); }
            return 0;
        }

        private void NxActionUpdate(long id, string status, string message, string path = null, Guid? resultDocId = null,
            string resultDocNo = null, long queueId = 0, Guid? srcDocId = null, string srcDocNo = null)
        {
            if (!string.IsNullOrEmpty(message)) _lastNxActionMessage = message;   // ผลให้หน้าเว็บเมื่อยังไม่มีตารางประวัติ
            if (id <= 0 || !NxActionTableReady) return;
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE dbo.Nexaacc_Doc_Action
                         SET Status = @s, Result_Message = @m, Path = COALESCE(@p, Path),
                             Result_Doc_Id = COALESCE(@rd, Result_Doc_Id), Result_Doc_No = COALESCE(@rn, Result_Doc_No),
                             Queue_Id = COALESCE(@q, Queue_Id), Source_Doc_Id = COALESCE(@sd, Source_Doc_Id),
                             Source_Doc_No = COALESCE(@sn, Source_Doc_No), Updated_At = GETDATE()
                       WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@id", id }, { "@s", status }, { "@m", (object)NxCut(message, 2000) ?? DBNull.Value },
                        { "@p", (object)NxCut(path, 30) ?? DBNull.Value },
                        { "@rd", resultDocId.HasValue && resultDocId.Value != Guid.Empty ? (object)resultDocId.Value : DBNull.Value },
                        { "@rn", (object)NxCut(resultDocNo, 60) ?? DBNull.Value },
                        { "@q", queueId > 0 ? (object)queueId : DBNull.Value },
                        { "@sd", srcDocId.HasValue && srcDocId.Value != Guid.Empty ? (object)srcDocId.Value : DBNull.Value },
                        { "@sn", (object)NxCut(srcDocNo, 60) ?? DBNull.Value }
                    });
            }
            catch { }
        }

        /// <summary>ประวัติงาน NextAcc ของใบเสร็จ (ใหม่ก่อน) — null = ไม่มีตาราง</summary>
        public DataTable NxActionHistory(string receiptId, int top = 20)
        {
            if (!NxActionTableReady || string.IsNullOrWhiteSpace(receiptId)) return null;
            try
            {
                return _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP (@n) ID, Action, Status, Path, Source_Doc_No, Result_Doc_Id, Result_Doc_No, Result_Message, Queue_Id,
                             Created_By, Created_At, Updated_At
                        FROM dbo.Nexaacc_Doc_Action WHERE Receipt_ID = @r ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@n", Math.Max(1, Math.Min(top, 100)) }, { "@r", receiptId.Trim() } });
            }
            catch { return null; }
        }

        /// <summary>เอกสารผลลัพธ์ที่บันทึกไว้ในประวัติ (ใช้เปิด PDF ใบแทน — อนุญาตเฉพาะ GUID ที่อยู่ในประวัติของใบนี้)</summary>
        public bool NxActionOwnsResultDoc(string receiptId, Guid docId)
        {
            if (!NxActionTableReady || docId == Guid.Empty) return false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 ID FROM dbo.Nexaacc_Doc_Action WHERE Receipt_ID = @r AND (Result_Doc_Id = @g OR Source_Doc_Id = @g)",
                    new Dictionary<string, object> { { "@r", (receiptId ?? "").Trim() }, { "@g", docId } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        public static string NxDocTypeLabel(int t) { return NexaaccDocTypeLabel(t); }
        public static string NxDocStatusLabel(int s) { return NexaaccDocStatusLabel(s); }
        public static bool NxEtaxReachedRd(string etaxStatus) { return IsEtaxReachedRd(etaxStatus); }

        private static string NxCut(string s, int n) { return s == null ? null : (s.Length > n ? s.Substring(0, n) : s); }

        private static string NxWho()
        {
            try { return ReceiptPreflight.CurrentUserName(); } catch { return "SYSTEM"; }
        }

        private static string NxApiMessage(Exception ex, out string ruleCode)
        {
            ruleCode = null;
            var ae = ex as AccountingApiException;
            if (ae != null)
            {
                string m = NextAccErrorText.Message(DecodeUnicodeEscapes(ae.ResponseBody ?? ""), out ruleCode);
                return "NextAcc " + ae.StatusCode + ": " + (string.IsNullOrWhiteSpace(m) ? ae.Message : m);
            }
            return ex == null ? "" : ex.Message;
        }

        /// <summary>โยนข้อจำกัดทางนโยบาย — ArgumentException เสมอ (คิวไม่ retry เอง); ชนิด POLICY เมื่อเปิดการจำแนก (ข้อ 21)</summary>
        private static ArgumentException NxPolicy(string rule, string message, string fixUrl = null)
        {
            return QueueErrors.Enabled ? (ArgumentException)new QueuePolicyException(rule, message, fixUrl) : new ArgumentException(message);
        }

        public static string NxReceiptActionUrl(string receiptNumber)
        {
            return "/Account/ReceiptNextAcc?receipt=" + Uri.EscapeDataString(receiptNumber ?? "");
        }

        // ── ตัวช่วยอ่านข้อมูลใบเสร็จ ──────────────────────────────────────────────────────────────

        /// <summary>เลขใบเสร็จจาก UID (หน้า Receipt ใช้ ?uid=)</summary>
        public string NxReceiptIdByUid(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString, "SELECT TOP 1 ID FROM Account_Receipt WHERE UID = @u",
                    new Dictionary<string, object> { { "@u", uid.Trim() } });
                return dt != null && dt.Rows.Count > 0 ? Convert.ToString(dt.Rows[0][0]) : null;
            }
            catch { return null; }
        }

        /// <summary>เอกสาร NextAcc ของใบเสร็จ: ตัวชี้บนใบ → คิว CREATE ที่สำเร็จ</summary>
        public Guid NxReceiptDocId(string receiptNumber)
        {
            Guid g = LookupLinkedReceiptDocId(receiptNumber);
            return g != Guid.Empty ? g : LookupNexaaccDocIdByReceipt(receiptNumber);
        }

        /// <summary>อ่านเอกสาร NextAcc ปัจจุบันของใบเสร็จ (null = ยังไม่มี/อ่านไม่ได้)</summary>
        public async Task<DocumentResponse> NxLoadReceiptDocAsync(string receiptNumber)
        {
            if (!_config.CanUseCompanyEndpoints) return null;
            Guid id = NxReceiptDocId(receiptNumber);
            if (id == Guid.Empty) return null;
            try { return (await _apiClient.GetDocumentAsync(id))?.data; }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "NextAccActions", $"อ่านเอกสาร {id} ของใบ {receiptNumber} ไม่ได้: {ex.Message}", "SYSTEM");
                return null;
            }
        }

        /// <summary>ผู้ซื้อของใบเสร็จตามระบบ (ตัวเดียวกับตอนออกเอกสาร — ResolveReceiptBuyer)</summary>
        public ContactInfo NxReceiptBuyer(string receiptNumber)
        {
            var f = ReceiptPreflight.LoadFacts(_connectionString, receiptNumber);
            string src;
            return ResolveReceiptBuyer(receiptNumber, f == null ? 0 : f.ReservationId, null, out src);
        }

        /// <summary>แหล่งเงิน (Account_Paid_How ที่เปิดใช้) พร้อมบัญชี NextAcc ที่ผูก</summary>
        public DataTable NxPaidHowList()
        {
            try
            {
                return _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID, Paid_How, CAST(Nexaacc_AccountId AS NVARCHAR(50)) AS AccId
                        FROM Account_Paid_How WHERE Status = 'True' ORDER BY Paid_How", null);
            }
            catch { return null; }
        }

        // ══════════════════════════════════════════════════════════════════════════════════════
        //  ① ลูกค้าขอใบกำกับภาษีเต็มรูปทีหลัง
        // ══════════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// บันทึกข้อมูลผู้ซื้อลงลูกค้าของใบ (Customer ตาม Account_Receipt.Customer_ID → ผู้ซื้อที่ resolve ได้ → เบอร์ที่กรอก) ให้ตรงกับที่จะส่ง NextAcc
        /// (ไม่งั้น sync ผู้ติดต่อรอบถัดไปจากข้อมูลเดิมจะทับชื่อกลับ) — แก้เฉพาะชื่อ/เลขภาษี/สาขา/ชนิด และที่อยู่เมื่อเปลี่ยน
        /// (ไม่ใช้ code.UpsertCustomer กับแถวเดิม เพราะทับชื่อเล่น/หมายเหตุ/อีเมลเป็นค่าว่าง). คืนข้อความสรุป
        /// </summary>
        private string NxSaveLocalBuyer(string receiptNumber, FullTaxInvoiceBuyer b)
        {
            long custId = 0;
            string phone = null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 ISNULL(R.Customer_ID, 0) AS Cid, ISNULL(C.MobilePhone, N'') AS Phone
                        FROM Account_Receipt R LEFT JOIN Customer C ON C.ID = R.Customer_ID WHERE R.ID = @r",
                    new Dictionary<string, object> { { "@r", receiptNumber } });
                if (dt != null && dt.Rows.Count > 0)
                {
                    custId = Convert.ToInt64(dt.Rows[0]["Cid"]);
                    phone = Convert.ToString(dt.Rows[0]["Phone"]);
                    if (custId > 0 && string.IsNullOrWhiteSpace(phone)) custId = 0;   // แถวหาย
                }
                if (custId <= 0)
                {
                    var cur = NxReceiptBuyer(receiptNumber);
                    string p = cur != null && !string.IsNullOrWhiteSpace(cur.Phone) ? cur.Phone.Trim() : b.Phone;
                    if (!string.IsNullOrWhiteSpace(p))
                    {
                        var c2 = _code.DatabaseQuerySafe(_connectionString, "SELECT TOP 1 ID FROM Customer WHERE MobilePhone = @p",
                            new Dictionary<string, object> { { "@p", p.Trim() } });
                        if (c2 != null && c2.Rows.Count > 0) { custId = Convert.ToInt64(c2.Rows[0][0]); phone = p.Trim(); }
                        else
                        {
                            // ลูกค้าใหม่ (ยังไม่มีแถว) — UpsertCustomer ปลอดภัยเพราะไม่มีข้อมูลเดิมให้ทับ
                            custId = _code.UpsertCustomer(_connectionString, p.Trim(), b.Name, "", "", "", b.Name, b.Address, b.TaxId, "",
                                NxCustomerTypeId(b.IsJuristic), 0, "", b.BranchCode ?? "");
                            phone = p.Trim();
                            if (custId > 0)
                            {
                                _code.DatabaseInsertSafe(_connectionString,
                                    "UPDATE Account_Receipt SET Customer_ID = @c WHERE ID = @r AND (Customer_ID IS NULL OR Customer_ID = 0)",
                                    new Dictionary<string, object> { { "@c", custId }, { "@r", receiptNumber } });
                                return "สร้างลูกค้า " + b.Name + " (" + phone + ") และผูกกับใบนี้";
                            }
                        }
                    }
                }
                if (custId <= 0)
                    return "⚠ ใบนี้ไม่มีผู้ซื้อในระบบและไม่ได้กรอกเบอร์ — บันทึกข้อมูลผู้ซื้อเฉพาะบน NextAcc";

                // ที่อยู่: เทียบกับที่อยู่เต็มที่ระบบประกอบส่ง NextAcc — ต่างกัน = เก็บที่อยู่จากฟอร์มเป็นข้อความเดียว (ล้างช่องแยก)
                string curAddr = null;
                var row = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 " + CustomerContactSelectColumns + @"
                        FROM Customer C LEFT JOIN Address A ON A.ID = C.Address_ID LEFT JOIN Customer_Type CT ON CT.ID = C.Customer_Type_ID
                       WHERE C.ID = @id", new Dictionary<string, object> { { "@id", custId } });
                if (row != null && row.Rows.Count > 0) curAddr = ComposeCustomerAddress(row.Rows[0]);
                bool addrChanged = !string.Equals(TaxPartyValidator.Clean(curAddr), TaxPartyValidator.Clean(b.Address), StringComparison.Ordinal);
                int typeId = NxCustomerTypeId(b.IsJuristic);
                var ps = new Dictionary<string, object>
                {
                    { "@id", custId }, { "@n", b.Name }, { "@t", b.TaxId }, { "@b", b.BranchCode ?? "" },
                    { "@ct", typeId > 0 ? (object)typeId : DBNull.Value }, { "@a", b.Address }, { "@ac", addrChanged ? 1 : 0 }
                };
                try
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Customer SET Name = @n, FullName = @n, IDNumber = @t, Branch_Number = @b,
                                 Customer_Type_ID = COALESCE(@ct, Customer_Type_ID),
                                 Address = CASE WHEN @ac = 1 THEN @a ELSE Address END,
                                 Address1 = CASE WHEN @ac = 1 THEN N'' ELSE Address1 END,
                                 Address_ID = CASE WHEN @ac = 1 THEN NULL ELSE Address_ID END
                           WHERE ID = @id", ps);
                }
                catch
                {
                    // Address_ID ไม่รับ NULL ในบางฐาน — ใช้ 0 (ค่าเดียวกับ UpsertCustomer เมื่อไม่มีที่อยู่แยก)
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Customer SET Name = @n, FullName = @n, IDNumber = @t, Branch_Number = @b,
                                 Customer_Type_ID = COALESCE(@ct, Customer_Type_ID),
                                 Address = CASE WHEN @ac = 1 THEN @a ELSE Address END,
                                 Address1 = CASE WHEN @ac = 1 THEN N'' ELSE Address1 END,
                                 Address_ID = CASE WHEN @ac = 1 THEN 0 ELSE Address_ID END
                           WHERE ID = @id", ps);
                }
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Account_Receipt SET Customer_ID = @c WHERE ID = @r AND (Customer_ID IS NULL OR Customer_ID = 0)",
                    new Dictionary<string, object> { { "@c", custId }, { "@r", receiptNumber } });
                return "บันทึกข้อมูลผู้ซื้อในระบบแล้ว (ลูกค้า #" + custId + (addrChanged ? ", ที่อยู่ใหม่" : "") + ")";
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "NextAccActions", $"บันทึกผู้ซื้อของใบ {receiptNumber} ในระบบไม่ได้: {ex.Message}", "SYSTEM");
                return "⚠ บันทึกข้อมูลผู้ซื้อในระบบไม่สำเร็จ (" + ex.Message + ") — ส่งเฉพาะบน NextAcc";
            }
        }

        private int NxCustomerTypeId(bool juristic)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString, "SELECT TOP 1 ID FROM Customer_Type WHERE Customer_Code = @c",
                    new Dictionary<string, object> { { "@c", juristic ? "TXID" : "NIDN" } });
                return dt != null && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// หน้า ReceiptNextAcc → เข้าคิว ISSUE_FULL_TAX_INVOICE แล้วประมวลผลทันที.
        /// ตรวจเบื้องต้นฝั่งเรา (ใบยังใช้งาน/ส่ง NextAcc แล้ว/ไม่มีใบลดหนี้) → บันทึกผู้ซื้อในระบบ → คิว (แถวที่ยังไม่ส่งของใบเดิม = ใช้แถวเดิม
        /// แทนที่ payload ⇒ บริบท Idempotency-Key เดิม) → ประมวลผล
        /// </summary>
        public async Task<NxActionResult> RequestFullTaxInvoiceAsync(string receiptNumber, FullTaxInvoiceBuyer buyer, string reason, bool allowRecreate)
        {
            string rn = (receiptNumber ?? "").Trim();
            if (rn.Length == 0 || buyer == null) return NxActionResult.Fail("BLOCKED", "ไม่ได้ระบุใบเสร็จ/ข้อมูลผู้ซื้อ");
            if (!_config.IsConfigured) return NxActionResult.Fail("BLOCKED", "ยังไม่ได้ตั้งค่าการเชื่อมต่อ NextAcc");
            if (!_config.CanUseCompanyEndpoints)
                return NxActionResult.Fail("BLOCKED", "การเชื่อมต่อนี้เรียก company endpoint ไม่ได้ (ตั้ง CompanyId + Nexaacc_Company_Endpoints=1) — ใช้ปุ่มส่งใหม่ (ยกเลิก→ออกใหม่) แทน");
            var f = ReceiptPreflight.LoadFacts(_connectionString, rn);
            if (f == null) return NxActionResult.Fail("BLOCKED", "ไม่พบใบเสร็จ " + rn);
            if (!string.IsNullOrEmpty(f.Status) && !string.Equals(f.Status, "Normal", StringComparison.OrdinalIgnoreCase))
                return NxActionResult.Fail("BLOCKED", "ใบเสร็จ " + rn + " สถานะ " + f.Status + " — ออกใบกำกับให้ใบที่ยกเลิก/คืนเงินแล้วไม่ได้");
            if (NxReceiptDocId(rn) == Guid.Empty)
                return NxActionResult.Fail("BLOCKED", "ใบ " + rn + " ยังไม่มีเอกสารบน NextAcc — ใบที่ยังไม่ส่ง: แก้ข้อมูลผู้ซื้อในหน้าใบเสร็จแล้วบันทึก ระบบจะออกเป็นใบกำกับเต็มรูปเอง");

            string by = NxWho();
            string localNote = NxSaveLocalBuyer(rn, buyer);
            var payload = new Dictionary<string, object>
            {
                { "receiptNumber", rn }, { "reservationId", f.ReservationId },
                { "buyerName", buyer.Name }, { "buyerTaxId", buyer.TaxId }, { "buyerBranch", buyer.BranchCode ?? "" },
                { "buyerAddress", buyer.Address }, { "buyerJuristic", buyer.IsJuristic }, { "buyerPhone", buyer.Phone ?? "" },
                { "reason", (reason ?? "").Trim() }, { "allowRecreate", allowRecreate }, { "requestedBy", by }
            };
            long actionId = NxActionInsert("FULL_TAX_INVOICE", rn, null, null, null, _serializer.Serialize(payload), by);
            payload["actionId"] = actionId;
            long qid = NxEnqueueOrRefresh(ActIssueFullTaxInvoice, rn, f.ReservationId, payload);
            if (qid <= 0)
            {
                NxActionUpdate(actionId, "FAILED", "เข้าคิวไม่สำเร็จ");
                return NxActionResult.Fail("FAILED", "เข้าคิวไม่สำเร็จ — ดู log AccountingSync");
            }
            NxActionUpdate(actionId, "QUEUED", localNote, queueId: qid);
            _code.Logs(_connectionString, "NextAccActions",
                $"ขอใบกำกับเต็มรูป ใบ {rn} ผู้ซื้อ {buyer.Name} ({buyer.TaxId}/{buyer.BranchCode}) โดย {by} → คิว #{qid} · {localNote}", by);
            var r = await ProcessNxActionNowAsync(qid, actionId);
            if (!string.IsNullOrEmpty(localNote) && localNote.StartsWith("⚠", StringComparison.Ordinal)) r.Message = localNote + " | " + r.Message;
            return r;
        }

        /// <summary>หน้า ReceiptNextAcc → แก้แหล่งเงิน (ตรวจก่อน → LOCAL_ONLY ทำทันที / ต้องยิง NextAcc = เข้าคิว RECLASSIFY_PAYMENT_SOURCE)</summary>
        public async Task<NxActionResult> RequestPaymentSourceReclassAsync(string receiptNumber, int newPaidHowId, string reason)
        {
            string rn = (receiptNumber ?? "").Trim();
            string reasonErr = PaymentSourceReclassPolicy.ValidateReason(reason);
            if (reasonErr != null) return NxActionResult.Fail("BLOCKED", reasonErr);
            var f = ReceiptPreflight.LoadFacts(_connectionString, rn);
            if (f == null) return NxActionResult.Fail("BLOCKED", "ไม่พบใบเสร็จ " + rn);
            if (!string.IsNullOrEmpty(f.Status) && !string.Equals(f.Status, "Normal", StringComparison.OrdinalIgnoreCase))
                return NxActionResult.Fail("BLOCKED", "ใบเสร็จสถานะ " + f.Status + " — แก้แหล่งเงินไม่ได้");

            string newName = null, newAcc = null;
            var ph = _code.DatabaseQuerySafe(_connectionString,
                "SELECT TOP 1 Paid_How, CAST(Nexaacc_AccountId AS NVARCHAR(50)) AS AccId FROM Account_Paid_How WHERE ID = @id AND Status = 'True'",
                new Dictionary<string, object> { { "@id", newPaidHowId } });
            if (ph != null && ph.Rows.Count > 0) { newName = Convert.ToString(ph.Rows[0]["Paid_How"]).Trim(); newAcc = Convert.ToString(ph.Rows[0]["AccId"]); }
            if (string.IsNullOrEmpty(newName)) return NxActionResult.Fail("BLOCKED", "ไม่พบแหล่งเงินที่เลือก (หรือถูกปิดใช้)");
            string oldName = (f.PaidType ?? "").Trim();
            if (string.Equals(oldName, newName, StringComparison.Ordinal))
                return NxActionResult.Fail("BLOCKED", "แหล่งเงินใหม่เหมือนเดิม (" + oldName + ")");

            Guid newAccId;
            Guid? newAccG = Guid.TryParse(newAcc ?? "", out newAccId) ? (Guid?)newAccId : null;
            DocumentResponse doc = await NxLoadReceiptDocAsync(rn);
            decimal depApplied = f.DepositAppliedColumn > 0.005m ? f.DepositAppliedColumn
                : -f.Lines.Where(l => l.Amount < 0m).Sum(l => l.Amount);
            var facts = new PaymentSourceReclassFacts
            {
                HasNextAccDoc = doc != null,
                DocumentType = doc == null ? 0 : doc.DocumentType,
                Status = doc == null ? 0 : doc.Status,
                CurrentPaymentAccountId = doc?.PaymentAccountId,
                CurrentBankAccountId = doc?.BankAccountId,
                NewAccountId = newAccG,
                DepositAppliedAmount = depApplied,
                EtaxReachedRd = doc != null && IsEtaxReachedRd(doc.EtaxStatus),
                HasActiveCreditNote = CreditNoteGuards.ActiveForReceipt(_connectionString, rn).Count > 0,
                ReplacedByFullTaxInvoice = doc?.ReplacedByDocumentId != null,
                IsCompanyDocMode = _config.CanUseCompanyEndpoints,
                PeriodClosedKnown = f.PeriodCacheFresh && !string.IsNullOrEmpty(f.PeriodStatus)
                    && !string.Equals(f.PeriodStatus, "Open", StringComparison.OrdinalIgnoreCase)
            };
            if (doc == null && NxReceiptDocId(rn) != Guid.Empty)
                return NxActionResult.Fail("BLOCKED", "อ่านเอกสารของใบนี้จาก NextAcc ไม่ได้ (เครือข่าย/สิทธิ์) — ลองใหม่อีกครั้ง");
            string msg;
            var verdict = PaymentSourceReclassPolicy.Check(facts, out msg);
            string by = NxWho();
            var payload = new Dictionary<string, object>
            {
                { "receiptNumber", rn }, { "reservationId", f.ReservationId }, { "newPaidHowId", newPaidHowId },
                { "newPaidHow", newName }, { "oldPaidHow", oldName }, { "newAccountId", newAcc ?? "" },
                { "reason", TaxPartyValidator.Clean(reason) }, { "requestedBy", by }
            };
            long actionId = NxActionInsert("RECLASSIFY_SOURCE", rn, null, doc?.Id, doc?.DocumentNumber, _serializer.Serialize(payload), by);
            if (verdict == PaymentSourceReclassVerdict.Blocked)
            {
                NxActionUpdate(actionId, "BLOCKED", msg);
                return new NxActionResult { Ok = false, Status = "BLOCKED", Message = msg, ActionId = actionId };
            }
            if (verdict == PaymentSourceReclassVerdict.LocalOnly)
            {
                string loc = NxApplyLocalPaidHow(rn, f.ReservationId, oldName, newName, TaxPartyValidator.Clean(reason), 0, "LOCAL_ONLY");
                NxActionUpdate(actionId, "DONE", msg + " · " + loc, "LOCAL_ONLY");
                return new NxActionResult { Ok = true, Status = "DONE", Path = "LOCAL_ONLY", Message = msg + " · " + loc, ActionId = actionId };
            }
            payload["actionId"] = actionId;
            long qid = NxEnqueueOrRefresh(ActReclassifyPaymentSource, rn, f.ReservationId, payload);
            if (qid <= 0)
            {
                NxActionUpdate(actionId, "FAILED", "เข้าคิวไม่สำเร็จ");
                return NxActionResult.Fail("FAILED", "เข้าคิวไม่สำเร็จ — ดู log AccountingSync");
            }
            NxActionUpdate(actionId, "QUEUED", null, queueId: qid);
            return await ProcessNxActionNowAsync(qid, actionId);
        }

        /// <summary>แถวคิวที่ยังไม่ส่งของใบเดียวกัน = ใช้แถวเดิม (แทนที่ payload, คืนเป็น PENDING) — บริบท Idempotency-Key "{เลขใบ}-q{id}" คงเดิม
        /// ⇒ ยิงซ้ำหลังเน็ตหลุดได้ผลเดิมจาก NextAcc ไม่สร้างใบแทนซ้ำ</summary>
        private long NxEnqueueOrRefresh(string action, string receiptNumber, int reservationId, Dictionary<string, object> payload)
        {
            string st;
            long existing = FindUnsentEntry(NxActEntity, action, "receiptNumber", receiptNumber, out st);
            if (existing > 0 && st != "PROCESSING")
            {
                try
                {
                    CaptureOperatorUser(payload);
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Accounting_Sync_Queue SET Payload = @p, Status = 'PENDING', Next_Retry_Date = NULL, Error_Message = NULL
                           WHERE ID = @id AND Status IN ('PENDING', 'FAILED')",
                        new Dictionary<string, object> { { "@p", _serializer.Serialize(payload) }, { "@id", existing } });
                    return existing;
                }
                catch { }
            }
            if (existing > 0) return existing;   // กำลังประมวลผลอยู่ — รอผลแถวนั้น
            return InsertQueue(NxActEntity, reservationId, action, payload);
        }

        /// <summary>
        /// ประมวลผลแถวคิวของงานข้อ 25 ทันที (หน้าเว็บรอผล) — atomic claim เดียวกับ worker; worker หยิบไปก่อน = รอผลสูงสุด ~25 วินาที.
        /// การจัดการผลเหมือนรอบคิวปกติแบบย่อ: นโยบาย/ข้อมูล = FAILED ไม่ retry เอง (ไม่ส่ง Telegram — ผู้ใช้อยู่หน้าจอ) · NextAcc ล่ม/เครือข่าย =
        /// คง PENDING (worker ทำต่อ) · 5xx ที่ไม่ใช่ล่มทั้งแอป = DUPLICATE ตรวจบน NextAcc ก่อน (อาจทำไปแล้ว) · ชั่วคราว (408/429/409) = worker ลองใหม่
        /// </summary>
        public async Task<NxActionResult> ProcessNxActionNowAsync(long queueId, long actionId)
        {
            _lastNxActionMessage = null;
            try { RefreshPreventionSwitch(); } catch { }
            if (!TryClaimQueueItem(queueId))
            {
                for (int i = 0; i < 12; i++)
                {
                    await Task.Delay(2000).ConfigureAwait(false);
                    var st = NxQueueRow(queueId);
                    if (st == null || st.Item1 != "PROCESSING") break;
                }
                return NxResultFromQueue(queueId, actionId);
            }
            string action = null, payload = null;
            var row = _code.DatabaseQuerySafe(_connectionString,
                "SELECT Action_Type, Payload, Retry_Count FROM Accounting_Sync_Queue WHERE ID = @id",
                new Dictionary<string, object> { { "@id", queueId } });
            int retry = 0;
            if (row != null && row.Rows.Count > 0)
            {
                action = Convert.ToString(row.Rows[0]["Action_Type"]);
                payload = Convert.ToString(row.Rows[0]["Payload"]);
                retry = row.Rows[0]["Retry_Count"] == DBNull.Value ? 0 : Convert.ToInt32(row.Rows[0]["Retry_Count"]);
            }
            _apiClient.IdempotencyContext = BuildQueueIdempotencyContext(queueId, payload);
            try
            {
                _lastDocNumber = null;
                _lastDocType = null;
                string id = await ProcessSingleItemAsync(action, payload).ConfigureAwait(false);
                UpdateQueueStatus(queueId, "COMPLETED", null, id, _lastDocNumber, _lastDocType);
            }
            catch (ServerUnavailableException ex)
            {
                UpdateQueueStatus(queueId, "PENDING", "NextAcc ไม่พร้อมใช้งาน (ไม่นับ retry): " + ex.Message, null);
                NxActionUpdate(actionId, "QUEUED", "NextAcc ไม่พร้อมใช้งาน — ระบบจะทำต่อเองเมื่อ NextAcc กลับมา");
            }
            catch (DnsResolutionException ex) { UpdateQueueStatus(queueId, "PENDING", "DNS error (not counted as retry): " + ex.Message, null); NxActionUpdate(actionId, "QUEUED", "เชื่อมต่อ NextAcc ไม่ได้ — ระบบจะทำต่อเอง"); }
            catch (AuthenticationFailedException ex) { UpdateQueueStatus(queueId, "PENDING", "Auth error (not counted as retry): " + ex.Message, null); NxActionUpdate(actionId, "QUEUED", "คีย์ NextAcc ใช้ไม่ได้ — แก้คีย์แล้วระบบจะทำต่อ"); }
            catch (ArgumentException ex)
            {
                string detail = $"Queue #{queueId} [{action}] {ex.Message}";
                UpdateQueueStatus(queueId, "FAILED", detail, null);
                IncrementRetry(queueId, _config.MaxRetries);
                PinDead(queueId);
                var iq = ex as IQueueIssue;
                if (QueueErrors.Enabled)
                    MarkQueueIssue(queueId, iq != null ? iq.IssueClass : SyncIssueClass.Data, iq != null ? iq.Rule : "VALIDATION",
                        NxReceiptActionUrl(NxPayloadReceipt(payload)));
                NxActionUpdateIfOpen(actionId, "BLOCKED", ex.Message);
                _code.Logs(_connectionString, "NextAccActions", detail, "SYSTEM");
            }
            catch (AccountingApiException ex)
            {
                string body = DecodeUnicodeEscapes(ex.ResponseBody ?? "");
                if (IsServerDownBody(body))
                {
                    UpdateQueueStatus(queueId, "PENDING", "NextAcc ไม่พร้อมใช้งาน (ไม่นับ retry) — จะลองใหม่อัตโนมัติ", null);
                    NxActionUpdate(actionId, "QUEUED", "NextAcc ไม่พร้อมใช้งาน — ระบบจะทำต่อเองเมื่อ NextAcc กลับมา");
                }
                else
                {
                    string rc;
                    string msg = NxApiMessage(ex, out rc);
                    string detail = $"Queue #{queueId} [{action}] API {ex.StatusCode}: {body}";
                    var av = ApiErrorClassifier.Classify(ex.StatusCode, body);
                    if (ex.StatusCode >= 500) av = new IssueVerdict(SyncIssueClass.Duplicate, av.Rule ?? ("HTTP_" + ex.StatusCode), "NEXTACC");
                    UpdateQueueStatus(queueId, "FAILED", detail, null);
                    if (av.Class == SyncIssueClass.Transient)
                    {
                        if (QueueErrors.Enabled) EnsureTransientBudget(queueId);
                        IncrementRetry(queueId, retry + 1);
                        NxActionUpdate(actionId, "QUEUED", msg + " — ระบบจะลองใหม่อัตโนมัติ");
                    }
                    else
                    {
                        IncrementRetry(queueId, _config.MaxRetries);
                        PinDead(queueId);
                        NxActionUpdate(actionId, ex.StatusCode >= 500 ? "FAILED" : "BLOCKED",
                            msg + (ex.StatusCode >= 500 ? " — ตรวจบน NextAcc ก่อนสั่งซ้ำ (อาจทำไปแล้ว)" : ""));
                    }
                    if (QueueErrors.Enabled) MarkQueueIssue(queueId, av.Class, rc ?? av.Rule, NxReceiptActionUrl(NxPayloadReceipt(payload)));
                    _code.Logs(_connectionString, "NextAccActions", detail, "SYSTEM");
                }
            }
            catch (Exception ex)
            {
                string detail = $"Queue #{queueId} [{action}] error: {ex.Message}";
                UpdateQueueStatus(queueId, "FAILED", detail, null);
                if (IsServerDownBody(ex.Message) || IsDnsError(ex))
                {
                    UpdateQueueStatus(queueId, "PENDING", "NextAcc ไม่พร้อมใช้งาน (ไม่นับ retry) — จะลองใหม่อัตโนมัติ", null);
                    NxActionUpdate(actionId, "QUEUED", "NextAcc ไม่พร้อมใช้งาน — ระบบจะทำต่อเอง");
                }
                else
                {
                    IncrementRetry(queueId, retry + 1);
                    NxActionUpdate(actionId, "FAILED", ex.Message + " — ระบบจะลองใหม่อัตโนมัติ (ดูที่คิว #" + queueId + ")");
                }
                _code.Logs(_connectionString, "NextAccActions", detail, "SYSTEM");
            }
            finally { _apiClient.IdempotencyContext = null; }
            return NxResultFromQueue(queueId, actionId);
        }

        private void NxActionUpdateIfOpen(long actionId, string status, string message)
        {
            if (!string.IsNullOrEmpty(message)) _lastNxActionMessage = message;
            if (actionId <= 0 || !NxActionTableReady) return;
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE dbo.Nexaacc_Doc_Action SET Status = @s, Result_Message = @m, Updated_At = GETDATE() WHERE ID = @id AND Status IN ('QUEUED')",
                    new Dictionary<string, object> { { "@s", status }, { "@m", (object)NxCut(message, 2000) ?? DBNull.Value }, { "@id", actionId } });
            }
            catch { }
        }

        private Tuple<string, string> NxQueueRow(long queueId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Status, ISNULL(Error_Message, N'') AS Err FROM Accounting_Sync_Queue WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", queueId } });
                if (dt != null && dt.Rows.Count > 0) return Tuple.Create(Convert.ToString(dt.Rows[0]["Status"]), Convert.ToString(dt.Rows[0]["Err"]));
            }
            catch { }
            return null;
        }

        private NxActionResult NxResultFromQueue(long queueId, long actionId)
        {
            var r = new NxActionResult { QueueId = queueId, ActionId = actionId };
            var q = NxQueueRow(queueId);
            if (actionId > 0 && NxActionTableReady)
            {
                try
                {
                    var dt = _code.DatabaseQuerySafe(_connectionString,
                        "SELECT TOP 1 Status, Path, Result_Message, Result_Doc_Id, Result_Doc_No FROM dbo.Nexaacc_Doc_Action WHERE ID = @id",
                        new Dictionary<string, object> { { "@id", actionId } });
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        r.Status = Convert.ToString(dt.Rows[0]["Status"]);
                        r.Path = Convert.ToString(dt.Rows[0]["Path"]);
                        r.Message = Convert.ToString(dt.Rows[0]["Result_Message"]);
                        r.ResultDocNo = Convert.ToString(dt.Rows[0]["Result_Doc_No"]);
                        if (dt.Rows[0]["Result_Doc_Id"] != DBNull.Value) r.ResultDocId = (Guid)dt.Rows[0]["Result_Doc_Id"];
                    }
                }
                catch { }
            }
            if (string.IsNullOrEmpty(r.Status) || (r.Status == "QUEUED" && q != null && q.Item1 == "COMPLETED"))
            {
                r.Status = q == null ? "FAILED" : q.Item1 == "COMPLETED" ? "DONE" : q.Item1 == "FAILED" ? "FAILED" : "QUEUED";
                if (string.IsNullOrEmpty(r.Message)) r.Message = q == null ? "ไม่พบแถวคิว #" + queueId : (q.Item2 ?? "");
            }
            if (_lastNxActionMessage != null && (string.IsNullOrEmpty(r.Message) || r.Message.StartsWith("Queue #", StringComparison.Ordinal)))
                r.Message = _lastNxActionMessage;
            r.Ok = r.Status == "DONE";
            if (r.Status == "QUEUED" && string.IsNullOrEmpty(r.Message))
                r.Message = "อยู่ในคิว #" + queueId + " — ระบบจะทำต่อเอง (ดูผลที่หน้านี้อีกครั้งภายหลัง)";
            return r;
        }

        private string _lastNxActionMessage;

        private static string NxPayloadReceipt(string payloadJson)
        {
            var m = System.Text.RegularExpressions.Regex.Match(payloadJson ?? "", "\"receiptNumber\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : "";
        }

        private static string PStr(Dictionary<string, object> p, string k)
        {
            object v;
            return p != null && p.TryGetValue(k, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : "";
        }

        private static bool PBool(Dictionary<string, object> p, string k)
        {
            string s = PStr(p, k);
            return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";
        }

        private static long PLong(Dictionary<string, object> p, string k)
        {
            long v;
            return long.TryParse(PStr(p, k), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        // ── ตัวประมวลผลคิว ① ─────────────────────────────────────────────────────────────────────────

        private async Task<string> ProcessIssueFullTaxInvoice(Dictionary<string, object> p)
        {
            try { return await ProcessIssueFullTaxInvoiceCore(p).ConfigureAwait(false); }
            catch (ArgumentException ex) { NxActionUpdateIfOpen(PLong(p, "actionId"), "BLOCKED", ex.Message); throw; }
        }

        private async Task<string> ProcessReclassifyPaymentSource(Dictionary<string, object> p)
        {
            try { return await ProcessReclassifyPaymentSourceCore(p).ConfigureAwait(false); }
            catch (ArgumentException ex) { NxActionUpdateIfOpen(PLong(p, "actionId"), "BLOCKED", ex.Message); throw; }
        }

        private async Task<string> ProcessIssueFullTaxInvoiceCore(Dictionary<string, object> p)
        {
            _lastNxActionMessage = null;
            string rn = PStr(p, "receiptNumber");
            long actionId = PLong(p, "actionId");
            bool allowRecreate = PBool(p, "allowRecreate");
            string reason = PStr(p, "reason");
            if (string.IsNullOrEmpty(rn)) throw new ArgumentException("payload ไม่มี receiptNumber");
            if (!_config.CanUseCompanyEndpoints)
                throw NxPolicy("NXA-NO-COMPANY", "เรียก company endpoint ไม่ได้ — ออกใบกำกับเต็มรูปแทนไม่ได้ (ใช้ส่งใหม่แบบยกเลิก→ออกใหม่)");

            Guid docId = NxReceiptDocId(rn);
            if (docId == Guid.Empty) throw NxPolicy("NXA-NOT-SYNCED", "ใบ " + rn + " ยังไม่มีเอกสารบน NextAcc");
            var doc = (await _apiClient.GetDocumentAsync(docId))?.data;
            if (doc == null) throw NxPolicy("NXA-DOC-GONE", "อ่านเอกสาร " + docId + " จาก NextAcc ไม่ได้");

            var buyer = new FullTaxInvoiceBuyer
            {
                Name = PStr(p, "buyerName"), TaxId = PStr(p, "buyerTaxId"), BranchCode = PStr(p, "buyerBranch"),
                Address = PStr(p, "buyerAddress"), IsJuristic = PBool(p, "buyerJuristic"), Phone = PStr(p, "buyerPhone")
            };

            Guid? draftRep = null;
            if (doc.DocumentType == FullTaxInvoicePlanner.TypeReceipt || doc.DocumentType == FullTaxInvoicePlanner.TypeReceiptVoucher)
            {
                try
                {
                    // ใบแทนค้างร่างจากรอบก่อน (RelatedDocumentId = ใบนี้ — DocumentService.cs:12921/12946; ร่าง = อนุมัติไม่ผ่าน :13156-13173)
                    var list = await _apiClient.ListDocumentsAsync(new[] { FullTaxInvoicePlanner.TypeTaxInvoice }, null, null, docId, 20);
                    var d = list?.data?.Items?.FirstOrDefault(x => x.Status == NexaaccDocumentStatus.Draft
                        && (!x.ReplacesDocumentId.HasValue || x.ReplacesDocumentId.Value == docId));
                    if (d != null) draftRep = d.Id;
                }
                catch (Exception ex) { _code.Logs(_connectionString, "NextAccActions", $"ค้นใบแทนค้างร่างของ {doc.DocumentNumber} ไม่ได้: {ex.Message}", "SYSTEM"); }
            }

            var facts = new FullTaxInvoiceFacts
            {
                DocumentType = doc.DocumentType, Status = doc.Status, VatAmount = doc.VatAmount, BuyerDeclined = doc.BuyerDeclinedTaxInvoice,
                ContactName = doc.Contact?.Name, ContactTaxId = doc.Contact?.TaxId,
                SharedWalkInContact = doc.Contact != null && ReceiptBuyerChangePlanner.LooksLikeSharedWalkIn(doc.Contact.Name),
                EtaxReachedRd = IsEtaxReachedRd(doc.EtaxStatus),
                ReplacedByDocumentId = doc.ReplacedByDocumentId, ReplacedByDocumentNumber = doc.ReplacedByDocumentNumber,
                HasActiveCreditNote = CreditNoteGuards.ActiveForReceipt(_connectionString, rn).Count > 0,
                DraftReplacementId = draftRep
            };
            var plan = FullTaxInvoicePlanner.PlanBeforeContact(facts, buyer.TaxId);
            _code.Logs(_connectionString, "NextAccActions",
                $"FullTaxInvoice: ใบ {rn} เอกสาร {doc.DocumentNumber} (type {doc.DocumentType}, status {doc.Status}, VAT {doc.VatAmount:N2}, "
                + $"declined={(doc.BuyerDeclinedTaxInvoice ? "Y" : "N")}, ผู้ติดต่อ {doc.Contact?.Name}) ⇒ {plan.Path}", "SYSTEM");

            switch (plan.Path)
            {
                case FullTaxInvoicePath.Blocked:
                    throw NxPolicy("NXA-FTI-BLOCKED", plan.Message);
                case FullTaxInvoicePath.AlreadyReplaced:
                    NxActionUpdate(actionId, "DONE", plan.Message, "ALREADY", doc.ReplacedByDocumentId, doc.ReplacedByDocumentNumber,
                        srcDocId: doc.Id, srcDocNo: doc.DocumentNumber);
                    return doc.ReplacedByDocumentId.Value.ToString();
                case FullTaxInvoicePath.NeedRecreate:
                    return NxRecreateOrBlock(rn, actionId, allowRecreate, plan.Message, doc);
                case FullTaxInvoicePath.ApproveDraftReplacement:
                {
                    var ap = await _apiClient.ApproveDocumentAsync(draftRep.Value, new ApproveDocumentRequest
                    {
                        Notes = "ออกใบกำกับภาษีเต็มรูปแทน " + doc.DocumentNumber + " (TakeTime ใบ " + rn + ")"
                    });
                    var done = ap?.data;
                    string no = done?.DocumentNumber;
                    ClearReceiptPdfCache(rn);
                    string m = "✅ อนุมัติใบกำกับภาษีเต็มรูป " + (no ?? draftRep.Value.ToString()) + " ที่ออกแทน " + doc.DocumentNumber
                        + " (ค้างร่างจากครั้งก่อน) — ใบเดิมไม่นับซ้ำในรายงานภาษีขาย";
                    NxActionUpdate(actionId, "DONE", m, "APPROVE_DRAFT", draftRep, no, srcDocId: doc.Id, srcDocNo: doc.DocumentNumber);
                    _lastDocNumber = no;
                    return draftRep.Value.ToString();
                }
            }

            // UpdateContact — ผู้ติดต่อของ "เอกสาร" (ไม่ใช่ผู้ติดต่อตามเบอร์) เพราะ NextAcc ตรวจ §86/4 จากผู้ติดต่อของใบต้นทาง
            // (EvaluateFullTaxInvoiceReplacementAsync :13088-13104 HydrateContactAsync) และเปลี่ยน ContactId ของใบที่ลงบัญชีแล้วไม่ได้
            Guid contactId = doc.Contact != null ? doc.Contact.Id : Guid.Empty;
            if (contactId == Guid.Empty) throw NxPolicy("NXA-FTI-NOCONTACT", "เอกสาร " + doc.DocumentNumber + " ไม่มีผู้ติดต่อ — ออกใบกำกับเต็มรูปแทนไม่ได้");
            string branch = string.IsNullOrWhiteSpace(buyer.BranchCode) ? null : buyer.BranchCode;
            var put = await _apiClient.UpdateContactAsync(contactId, new UpdateContactRequest
            {
                Name = buyer.Name, TaxId = buyer.TaxId, Address = buyer.Address, BranchCode = branch,
                ContactType = buyer.IsJuristic ? NexaaccContactType.JuristicPerson : NexaaccContactType.Individual
            });
            var c = put?.data;
            if (c == null)
            {
                try { c = (await _apiClient.GetContactAsync(contactId))?.data; } catch { }
            }
            if (c == null || FullTaxInvoicePlanner.Digits(c.TaxId) != buyer.TaxId || string.IsNullOrWhiteSpace(c.Address))
                throw NxPolicy("NXA-FTI-CONTACT", "ส่งข้อมูลผู้ซื้อขึ้นผู้ติดต่อ " + (doc.Contact?.Name ?? "") + " บน NextAcc แล้ว แต่อ่านกลับไม่ตรง "
                    + "(สิทธิ์แก้ผู้ติดต่อของคีย์ API?) — ไม่ออกใบแทน");

            var after = (await _apiClient.GetDocumentAsync(docId))?.data ?? doc;
            var plan2 = FullTaxInvoicePlanner.PlanAfterContact(after.DocumentType, after.CanIssueFullTaxInvoice, after.FullTaxInvoiceBlockedReason,
                after.DocumentTitle, after.TaxInvoiceTitleNotice);
            _code.Logs(_connectionString, "NextAccActions",
                $"FullTaxInvoice: ใบ {rn} หลังแก้ผู้ติดต่อ: canIssue={after.CanIssueFullTaxInvoice} title=\"{after.DocumentTitle}\" ⇒ {plan2.Path}", "SYSTEM");
            if (plan2.Path == FullTaxInvoicePath.ContactUpgrade)
            {
                ClearReceiptPdfCache(rn);
                string m = "✅ " + plan2.Message + " (เอกสาร " + after.DocumentNumber + ")";
                NxActionUpdate(actionId, "DONE", m, "CONTACT_UPGRADE", after.Id, after.DocumentNumber, srcDocId: doc.Id, srcDocNo: doc.DocumentNumber);
                _lastDocNumber = after.DocumentNumber;
                return after.Id.ToString();
            }
            if (plan2.Path == FullTaxInvoicePath.Blocked)
                return NxRecreateOrBlock(rn, actionId, allowRecreate, plan2.Message, doc);

            // IssueReplacement — สร้าง+อนุมัติใบแทนในคำสั่งเดียว (Idempotency-Key ผูกแถวคิว)
            var resp = await _apiClient.IssueFullTaxInvoiceAsync(docId, new NxIssueFullTaxInvoiceRequest
            {
                Reason = string.IsNullOrWhiteSpace(reason) ? "ลูกค้าขอใบกำกับภาษีเต็มรูป (TakeTime ใบ " + rn + ")" : reason
            });
            var rep = resp?.data;
            if (rep == null || rep.Id == Guid.Empty) throw new ResponseParseException("issue-full-tax-invoice ไม่คืนเอกสาร: " + (resp?.message ?? "-"), null, null);
            ClearReceiptPdfCache(rn);
            string ok = "✅ ออกใบกำกับภาษีเต็มรูป " + rep.DocumentNumber + " แทน " + doc.DocumentNumber
                + " (วันที่เดิม ไม่มีรายการบัญชีใหม่ — ใบเดิมไม่นับซ้ำในรายงานภาษีขาย) · ส่ง PDF ใบใหม่ให้ลูกค้าและเรียกคืนใบเดิม";
            NxActionUpdate(actionId, "DONE", ok, "REPLACEMENT", rep.Id, rep.DocumentNumber, srcDocId: doc.Id, srcDocNo: doc.DocumentNumber);
            _code.Logs(_connectionString, "NextAccActions", $"FullTaxInvoice: ใบ {rn} → {rep.DocumentNumber} ({rep.Id}) แทน {doc.DocumentNumber}", "SYSTEM");
            _lastDocNumber = rep.DocumentNumber;
            _lastDocType = "TaxInvoice";
            // ใบแทนคือใบกำกับตัวจริง ⇒ e-Tax (ถ้าเปิดออกอัตโนมัติ) ออกที่ใบนี้ — ตัวเดียวกับเส้นใบกำกับปกติ (ไม่โยน error)
            await TryAutoGenerateEtaxAsync(rep.Id, rn, (int)PLong(p, "reservationId"), rep.TotalAmount, buyer.Name);
            return rep.Id.ToString();
        }

        /// <summary>เส้น "ยกเลิก→ออกใหม่" (RepostReceiptByNumber ตัวเดียวกับ CheckDocument_New — ข้อ 22: ผู้ติดต่อกลางแต่ §86/4 ครบ = บังคับ void→สร้างใหม่)
        /// ทำเฉพาะเมื่อผู้ใช้ติ๊กยอมรับ; ไม่ติ๊ก = หยุดพร้อมบอกเหตุ</summary>
        private string NxRecreateOrBlock(string rn, long actionId, bool allowRecreate, string why, DocumentResponse doc)
        {
            if (!allowRecreate)
                throw NxPolicy("NXA-FTI-RECREATE", why + " — ถ้ายอมให้เลขเอกสาร NextAcc เปลี่ยน ติ๊ก \"ยอมให้ยกเลิกแล้วออกใหม่\" แล้วกดอีกครั้ง");
            long rc = RepostReceiptByNumber(rn);
            string m = LastRepostMessage ?? "";
            if (rc < 0) throw NxPolicy("NXA-FTI-RECREATE-FAIL", "ยกเลิก→ออกใหม่ไม่สำเร็จ: " + m);
            string done = "✅ ส่งใบใหม่ในนามผู้ซื้อแล้ว (ยกเลิกเอกสาร " + (doc?.DocumentNumber ?? "") + " แล้วออกใหม่"
                + (rc > 0 ? " — คิว #" + rc : " — แก้ในที่เดิม") + ")" + (m.Length > 0 ? " · " + m : "");
            NxActionUpdate(actionId, "DONE", done, "RECREATE", srcDocId: doc?.Id, srcDocNo: doc?.DocumentNumber);
            _lastNxActionMessage = done;
            return rc > 0 ? "RECREATE-q" + rc : "RECREATE";
        }

        // ── ตัวประมวลผลคิว ② ─────────────────────────────────────────────────────────────────────────

        private async Task<string> ProcessReclassifyPaymentSourceCore(Dictionary<string, object> p)
        {
            string rn = PStr(p, "receiptNumber");
            long actionId = PLong(p, "actionId");
            string newName = PStr(p, "newPaidHow"), oldName = PStr(p, "oldPaidHow"), reason = PStr(p, "reason");
            Guid newAcc;
            if (!Guid.TryParse(PStr(p, "newAccountId"), out newAcc) || newAcc == Guid.Empty)
                throw NxPolicy("NXA-RCL-NOACC", "แหล่งเงิน \"" + newName + "\" ยังไม่ผูกบัญชี NextAcc");
            Guid docId = NxReceiptDocId(rn);
            if (docId == Guid.Empty) throw NxPolicy("NXA-NOT-SYNCED", "ใบ " + rn + " ยังไม่มีเอกสารบน NextAcc");
            var doc = (await _apiClient.GetDocumentAsync(docId))?.data;
            if (doc == null) throw NxPolicy("NXA-DOC-GONE", "อ่านเอกสาร " + docId + " จาก NextAcc ไม่ได้");

            // งานซ้ำ (retry หลังสำเร็จแต่อ่านคำตอบไม่ได้): บัญชีบนเอกสารเป็นบัญชีใหม่แล้ว = สำเร็จ ไม่ยิงซ้ำ
            if (doc.PaymentAccountId.HasValue && doc.PaymentAccountId.Value == newAcc)
            {
                var f0 = ReceiptPreflight.LoadFacts(_connectionString, rn);
                string loc0 = NxApplyLocalPaidHow(rn, f0 == null ? 0 : f0.ReservationId, oldName, newName, reason, 0, "RECLASSIFY_SOURCE");
                NxActionUpdate(actionId, "DONE", "เอกสาร " + doc.DocumentNumber + " ใช้บัญชีของ \"" + newName + "\" อยู่แล้ว · " + loc0, "NEXTACC",
                    doc.Id, doc.DocumentNumber, srcDocId: doc.Id, srcDocNo: doc.DocumentNumber);
                return doc.Id.ToString();
            }
            var resp = await _apiClient.ReclassifyPaymentSourceAsync(docId, new NxReclassifyPaymentSourceRequest
            {
                NewPaymentAccountId = newAcc,
                Reason = (reason ?? "") + " (TakeTime ใบ " + rn + ": " + oldName + " → " + newName + ")"
            });
            var after = resp?.data;
            if (after != null && after.PaymentAccountId.HasValue && after.PaymentAccountId.Value != newAcc)
                throw NxPolicy("NXA-RCL-MISMATCH", "NextAcc ตอบสำเร็จแต่แหล่งเงินบนเอกสารไม่ใช่บัญชีที่เลือก — ตรวจบน NextAcc");
            var f = ReceiptPreflight.LoadFacts(_connectionString, rn);
            long qid = 0;
            string loc = NxApplyLocalPaidHow(rn, f == null ? 0 : f.ReservationId, oldName, newName, reason, qid, "RECLASSIFY_SOURCE");
            ClearReceiptPdfCache(rn);
            string m = "✅ เปลี่ยนแหล่งเงินของ " + doc.DocumentNumber + " จาก \"" + oldName + "\" เป็น \"" + newName
                + "\" แล้ว (NextAcc ลงรายการแก้ในงวดเดิม — เลขเอกสารเดิม) · " + loc;
            NxActionUpdate(actionId, "DONE", m, "NEXTACC", doc.Id, doc.DocumentNumber, srcDocId: doc.Id, srcDocNo: doc.DocumentNumber);
            _code.Logs(_connectionString, "NextAccActions", "ReclassifyPaymentSource: ใบ " + rn + " " + oldName + " → " + newName + " (" + doc.DocumentNumber + ")", "SYSTEM");
            _lastDocNumber = doc.DocumentNumber;
            return doc.Id.ToString();
        }

        /// <summary>ปรับแหล่งเงินฝั่งเรา: Account_Receipt.Paid_Type + Payment_History.PaymentMethod (แถวรับเงินของใบนี้ที่ใช้ชื่อเดิม) + ประวัติแก้ใบ (ข้อ 21)</summary>
        private string NxApplyLocalPaidHow(string rn, int reservationId, string oldName, string newName, string reason, long queueId, string syncAction)
        {
            try
            {
                string before = ReceiptPreflight.SnapshotJson(_connectionString, rn);
                _code.DatabaseInsertSafe(_connectionString, "UPDATE Account_Receipt SET Paid_Type = @n WHERE ID = @r",
                    new Dictionary<string, object> { { "@n", newName }, { "@r", rn } });
                int ph = 0;
                try
                {
                    var dt = _code.DatabaseQuerySafe(_connectionString,
                        @"UPDATE Payment_History SET PaymentMethod = @n
                           WHERE Receipt_ID = @r AND PaymentMethod = @o AND PaymentAmount > 0;
                          SELECT @@ROWCOUNT AS N;",
                        new Dictionary<string, object> { { "@n", newName }, { "@o", oldName ?? "" }, { "@r", rn } });
                    if (dt != null && dt.Rows.Count > 0) ph = Convert.ToInt32(dt.Rows[0]["N"]);
                }
                catch { }
                string after = ReceiptPreflight.SnapshotJson(_connectionString, rn);
                ReceiptPreflight.WriteEditLog(_connectionString, rn, null, reservationId, "EDIT",
                    "เปลี่ยนแหล่งเงิน " + oldName + " → " + newName + " · " + reason, before, after, null, syncAction, queueId);
                return "ปรับแหล่งเงินในระบบแล้ว (ใบเสร็จ" + (ph > 0 ? " + ประวัติรับเงิน " + ph + " แถว" : "") + ")";
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "NextAccActions", $"ปรับแหล่งเงินในระบบของใบ {rn} ไม่สำเร็จ: {ex.Message}", "SYSTEM");
                return "⚠ ปรับแหล่งเงินในระบบไม่สำเร็จ: " + ex.Message;
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════════
        //  ③ ภาษีซื้อยังไม่ถึงกำหนด (11640)
        // ══════════════════════════════════════════════════════════════════════════════════════

        public async Task<List<NxUndueInputVatSummary>> LoadUndueInputVatAsync()
        {
            if (!_config.CanUseCompanyEndpoints) return null;
            var r = await _apiClient.GetUndueInputVatAsync();
            return r?.data ?? new List<NxUndueInputVatSummary>();
        }

        public async Task<NxUndueDetail> LoadUndueDetailAsync(Guid docId)
        {
            var det = new NxUndueDetail();
            det.Doc = (await _apiClient.GetDocumentAsync(docId))?.data;
            if (det.Doc?.Contact != null && det.Doc.Contact.Id != Guid.Empty)
            {
                try { det.Contact = (await _apiClient.GetContactAsync(det.Doc.Contact.Id))?.data; } catch { }
            }
            return det;
        }

        /// <summary>
        /// เติมใบกำกับภาษีซื้อ: แก้ผู้ติดต่อผู้ขาย (เลขภาษี/ที่อยู่/สาขา — เฉพาะที่กรอกและต่างจากเดิม) → complete-tax-invoice (เลขที่/วันที่/สาขา)
        /// → อ่านรายการค้างใหม่ ถ้าใบหายจากรายการ = ย้าย 11640→11610 แล้ว (ReclassifyUndueInputVatAsync :15943-16040; ไม่ย้ายเมื่อพ้น §82/3 6 เดือน :15957-15963)
        /// </summary>
        public async Task<NxActionResult> CompleteSupplierTaxInvoiceAsync(Guid docId, string invoiceNo, DateTime invoiceDate, string branch,
            string taxId, string address)
        {
            string by = NxWho();
            DocumentResponse doc = null;
            try { doc = (await _apiClient.GetDocumentAsync(docId))?.data; } catch { }
            if (doc == null) return NxActionResult.Fail("BLOCKED", "อ่านเอกสาร " + docId + " จาก NextAcc ไม่ได้");
            var reqJson = _serializer.Serialize(new Dictionary<string, object>
            {
                { "invoiceNo", invoiceNo }, { "invoiceDate", invoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                { "branch", branch }, { "taxId", taxId }, { "address", address }
            });
            long actionId = NxActionInsert("COMPLETE_TAX_INV", null, null, doc.Id, doc.DocumentNumber, reqJson, by);
            try
            {
                string t = FullTaxInvoicePlanner.Digits(taxId);
                string a = TaxPartyValidator.Clean(address);
                string b = FullTaxInvoicePlanner.Digits(branch);
                var notes = new List<string>();
                if (doc.Contact != null && doc.Contact.Id != Guid.Empty)
                {
                    ContactResponse cur = null;
                    try { cur = (await _apiClient.GetContactAsync(doc.Contact.Id))?.data; } catch { }
                    var upd = new UpdateContactRequest();
                    bool any = false;
                    if (t.Length == 13 && FullTaxInvoicePlanner.Digits(cur?.TaxId) != t) { upd.TaxId = t; any = true; notes.Add("เลขผู้เสียภาษี " + t); }
                    if (a.Length > 0 && !string.Equals(TaxPartyValidator.Clean(cur?.Address), a, StringComparison.Ordinal)) { upd.Address = a; any = true; notes.Add("ที่อยู่"); }
                    if (b.Length == 5 && FullTaxInvoicePlanner.Digits(cur?.BranchCode) != b) { upd.BranchCode = b; any = true; notes.Add("สาขา " + b); }
                    if (t.Length == 13 && t[0] == '0' && (cur == null || cur.ContactType != NexaaccContactType.JuristicPerson))
                    { upd.ContactType = NexaaccContactType.JuristicPerson; any = true; }
                    if (any)
                    {
                        await _apiClient.UpdateContactAsync(doc.Contact.Id, upd);
                        _code.Logs(_connectionString, "NextAccActions",
                            $"CompleteTaxInvoice: แก้ผู้ติดต่อผู้ขาย {doc.Contact.Name} ({doc.Contact.Id}) โดย {by}: {string.Join(", ", notes)} (เดิม tax={cur?.TaxId} branch={cur?.BranchCode})", by);
                    }
                }
                var resp = await _apiClient.CompleteSupplierTaxInvoiceAsync(docId, new NxCompleteSupplierTaxInvoiceRequest
                {
                    SupplierInvoiceNumber = (invoiceNo ?? "").Trim(),
                    SupplierTaxInvoiceDate = invoiceDate.Date,
                    SupplierBranchCode = b.Length == 5 ? b : null
                });
                var still = (await _apiClient.GetUndueInputVatAsync())?.data?.FirstOrDefault(x => x.Id == docId);
                string m;
                bool moved = still == null;
                if (moved)
                    m = "✅ " + doc.DocumentNumber + ": ใบกำกับครบ §86/4 — NextAcc ย้ายภาษีซื้อ 11640 → 11610 (เคลมได้) แล้ว"
                        + (notes.Count > 0 ? " · แก้ผู้ติดต่อ: " + string.Join(", ", notes) : "");
                else
                    m = "บันทึกข้อมูลแล้ว แต่ภาษีซื้อยังพัก 11640 — ยังขาด: " + string.Join(" · ", still.MissingFields ?? new List<string>())
                        + (still.IsExpired ? " (พ้น 6 เดือน §82/3 แล้ว — เคลมไม่ได้)" : "");
                NxActionUpdate(actionId, moved ? "DONE" : "BLOCKED", m, "NEXTACC", doc.Id, doc.DocumentNumber);
                _code.Logs(_connectionString, "NextAccActions", "CompleteTaxInvoice: " + m + " (" + (resp?.message ?? "") + ")", by);
                return new NxActionResult { Ok = moved, Status = moved ? "DONE" : "BLOCKED", Message = m, ActionId = actionId, ResultDocId = doc.Id, ResultDocNo = doc.DocumentNumber };
            }
            catch (Exception ex)
            {
                string rc;
                string m = NxApiMessage(ex, out rc);
                NxActionUpdate(actionId, "FAILED", m);
                _code.Logs(_connectionString, "NextAccActions", $"CompleteTaxInvoice {doc.DocumentNumber} ล้มเหลว: {m}", by);
                return new NxActionResult { Ok = false, Status = "FAILED", Message = m, RuleCode = rc, ActionId = actionId };
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════════
        //  ④ e-Tax ใบลดหนี้ + ส่งอีเมลเอกสาร
        // ══════════════════════════════════════════════════════════════════════════════════════

        private DataRow NxCreditNoteRow(int cnId)
        {
            var dt = _code.DatabaseQuerySafe(_connectionString, "SELECT TOP 1 * FROM Credit_Note WHERE ID = @id",
                new Dictionary<string, object> { { "@id", cnId } });
            return dt != null && dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        private static string NxCol(DataRow r, string c)
        {
            return r != null && r.Table.Columns.Contains(c) && r[c] != DBNull.Value ? Convert.ToString(r[c], CultureInfo.InvariantCulture) : "";
        }

        private void NxSaveCnEtax(int cnId, EtaxInvoiceResponse e, string error)
        {
            if (!CreditNoteEtaxColumnsReady) return;
            try
            {
                if (e != null)
                    _code.DatabaseInsertSafe(_connectionString,
                        "UPDATE Credit_Note SET Etax_Id = @i, Etax_Status = @s, Etax_Ref = @r, Etax_At = GETDATE(), Etax_Error = @err WHERE ID = @id",
                        new Dictionary<string, object>
                        {
                            { "@id", cnId }, { "@i", e.Id }, { "@s", NxCut(e.Status ?? "Generated", 20) },
                            { "@r", (object)NxCut(e.EtaxRefNumber, 80) ?? DBNull.Value }, { "@err", (object)NxCut(error, 1000) ?? DBNull.Value }
                        });
                else
                    _code.DatabaseInsertSafe(_connectionString,
                        "UPDATE Credit_Note SET Etax_Status = CASE WHEN Etax_Id IS NULL THEN 'Error' ELSE Etax_Status END, Etax_Error = @err, Etax_At = GETDATE() WHERE ID = @id",
                        new Dictionary<string, object> { { "@id", cnId }, { "@err", (object)NxCut(error, 1000) ?? DBNull.Value } });
            }
            catch { }
        }

        /// <summary>ออก e-Tax ใบลดหนี้ (etax/generate) — submit = ส่งกรมสรรพากรต่อทันที (หรือเมื่อเปิด Etax_AutoSubmit)</summary>
        public async Task<NxActionResult> GenerateCreditNoteEtaxAsync(int cnId, bool submit)
        {
            var r = NxCreditNoteRow(cnId);
            if (r == null) return NxActionResult.Fail("BLOCKED", "ไม่พบใบลดหนี้ #" + cnId);
            if (!_config.CanUseCompanyEndpoints) return NxActionResult.Fail("BLOCKED", "เรียก company endpoint ไม่ได้ — ออก e-Tax ไม่ได้");
            string cnNo = NxCol(r, "CN_No");
            Guid docId;
            bool hasDoc = Guid.TryParse(NxCol(r, "Nexaacc_Doc_Id"), out docId) && docId != Guid.Empty;
            string why = CreditNoteEtaxPolicy.CanGenerate(NxCol(r, "Status"), NxCol(r, "Nexaacc_Path"), hasDoc, NxCol(r, "Etax_Status"));
            if (why != null) return NxActionResult.Fail("BLOCKED", cnNo + ": " + why);
            string by = NxWho();
            long actionId = NxActionInsert("CN_ETAX", NxCol(r, "Source_Receipt_ID"), cnId, docId, NxCol(r, "Nexaacc_Doc_No"), null, by);
            try
            {
                EtaxInvoiceResponse e = null;
                try
                {
                    e = (await _apiClient.GenerateEtaxAsync(new GenerateEtaxRequest { DocumentId = docId, AutoSign = _config.IsEtaxAutoSign }))?.data;
                }
                catch (AccountingApiException ex) when (ex.StatusCode == 400 && (DecodeUnicodeEscapes(ex.ResponseBody ?? "").IndexOf("มี e-Tax Invoice แล้ว", StringComparison.Ordinal) >= 0))
                {
                    // สร้างไปแล้ว (EtaxInvoiceService.GenerateAsync :117-118) — อ่านตัวเดิมมาแสดง
                    e = (await _apiClient.GetEtaxByDocumentAsync(docId))?.data;
                }
                if (e == null) throw new InvalidOperationException("etax/generate ไม่คืนข้อมูล");
                if (submit || _config.IsEtaxAutoSubmit) e = await NxSubmitEtaxAsync(e) ?? e;
                NxSaveCnEtax(cnId, e, null);
                string m = "✅ " + cnNo + ": e-Tax ใบลดหนี้ " + (e.EtaxRefNumber ?? "") + " — " + CreditNoteEtaxPolicy.StatusLabel(e.Status);
                NxActionUpdate(actionId, "DONE", m, "ETAX", e.Id, e.EtaxRefNumber);
                _code.Logs(_connectionString, "NextAccActions", m, by);
                return new NxActionResult { Ok = true, Status = "DONE", Message = m, ActionId = actionId };
            }
            catch (Exception ex)
            {
                string rc;
                string m = NxApiMessage(ex, out rc);
                NxSaveCnEtax(cnId, null, m);
                NxActionUpdate(actionId, "FAILED", m);
                _code.Logs(_connectionString, "NextAccActions", "e-Tax ใบลดหนี้ " + cnNo + " ล้มเหลว: " + m, by);
                return new NxActionResult { Ok = false, Status = "FAILED", Message = cnNo + ": " + m, RuleCode = rc, ActionId = actionId };
            }
        }

        /// <summary>ส่ง e-Tax ที่สร้างแล้วถึงกรมสรรพากร: Generated → sign-and-submit (EtaxController.cs:210-220) · Signed → submit (:100-106)</summary>
        private async Task<EtaxInvoiceResponse> NxSubmitEtaxAsync(EtaxInvoiceResponse e)
        {
            if (e == null || !CreditNoteEtaxPolicy.CanSubmit(e.Status)) return e;
            if (string.Equals(e.Status, "Signed", StringComparison.OrdinalIgnoreCase))
                return (await _apiClient.SubmitEtaxAsync(e.Id))?.data ?? e;
            return (await _apiClient.SignAndSubmitEtaxAsync(e.Id))?.data ?? e;
        }

        public async Task<NxActionResult> SubmitCreditNoteEtaxAsync(int cnId)
        {
            var r = NxCreditNoteRow(cnId);
            if (r == null) return NxActionResult.Fail("BLOCKED", "ไม่พบใบลดหนี้ #" + cnId);
            string cnNo = NxCol(r, "CN_No");
            Guid etaxId;
            EtaxInvoiceResponse e = null;
            try
            {
                if (Guid.TryParse(NxCol(r, "Etax_Id"), out etaxId) && etaxId != Guid.Empty) e = (await _apiClient.GetEtaxAsync(etaxId))?.data;
                Guid docId;
                if (e == null && Guid.TryParse(NxCol(r, "Nexaacc_Doc_Id"), out docId)) e = (await _apiClient.GetEtaxByDocumentAsync(docId))?.data;
                if (e == null) return NxActionResult.Fail("BLOCKED", cnNo + ": ยังไม่ได้สร้าง e-Tax");
                if (!CreditNoteEtaxPolicy.CanSubmit(e.Status))
                {
                    NxSaveCnEtax(cnId, e, null);
                    return NxActionResult.Fail("BLOCKED", cnNo + ": สถานะ e-Tax " + CreditNoteEtaxPolicy.StatusLabel(e.Status) + " — ไม่มีอะไรต้องส่ง");
                }
                e = await NxSubmitEtaxAsync(e);
                NxSaveCnEtax(cnId, e, null);
                string m = "✅ " + cnNo + ": " + CreditNoteEtaxPolicy.StatusLabel(e.Status);
                _code.Logs(_connectionString, "NextAccActions", "e-Tax ใบลดหนี้ " + m, NxWho());
                return new NxActionResult { Ok = true, Status = "DONE", Message = m };
            }
            catch (Exception ex)
            {
                string rc;
                string m = NxApiMessage(ex, out rc);
                NxSaveCnEtax(cnId, null, m);
                return new NxActionResult { Ok = false, Status = "FAILED", Message = cnNo + ": " + m, RuleCode = rc };
            }
        }

        /// <summary>อ่านสถานะ e-Tax ล่าสุดของใบลดหนี้จาก NextAcc แล้วเก็บลงแถว</summary>
        public async Task<NxActionResult> RefreshCreditNoteEtaxAsync(int cnId)
        {
            var r = NxCreditNoteRow(cnId);
            Guid docId;
            if (r == null || !Guid.TryParse(NxCol(r, "Nexaacc_Doc_Id"), out docId)) return NxActionResult.Fail("BLOCKED", "ไม่มีเอกสาร NextAcc");
            try
            {
                var e = (await _apiClient.GetEtaxByDocumentAsync(docId))?.data;
                if (e == null) return NxActionResult.Fail("BLOCKED", NxCol(r, "CN_No") + ": ยังไม่มี e-Tax บน NextAcc");
                NxSaveCnEtax(cnId, e, null);
                return new NxActionResult { Ok = true, Status = "DONE", Message = NxCol(r, "CN_No") + ": " + CreditNoteEtaxPolicy.StatusLabel(e.Status) };
            }
            catch (Exception ex)
            {
                string rc;
                return NxActionResult.Fail("FAILED", NxApiMessage(ex, out rc));
            }
        }

        /// <summary>หลังคิว CREATE_CREDIT_NOTE สำเร็จ: เปิด CreditNote_Etax_Auto=1 + ใบต้นทางส่ง e-Tax ถึงกรมสรรพากรแล้ว ⇒ ออก e-Tax ใบลดหนี้ (ห้ามทำคิวล้ม)</summary>
        private async Task TryAutoCreditNoteEtaxAsync(Dictionary<string, object> payload)
        {
            try
            {
                if (!CreditNoteEtaxPolicy.ShouldAuto(_config.GetConfigValue(CfgCreditNoteEtaxAuto, "0"), true)) return;
                int cnId;
                if (!int.TryParse(PStr(payload, "cnId"), out cnId) || cnId <= 0) return;
                var r = NxCreditNoteRow(cnId);
                if (r == null) return;
                string src = NxCol(r, "Source_Receipt_ID");
                bool srcEtax = CreditNoteGuards.EtaxReachedRd(_connectionString, src) != null;
                if (!CreditNoteEtaxPolicy.ShouldAuto("1", srcEtax)) return;
                var res = await GenerateCreditNoteEtaxAsync(cnId, false);
                _code.Logs(_connectionString, "NextAccActions", "e-Tax ใบลดหนี้อัตโนมัติ (CreditNote_Etax_Auto): " + res.Message, "SYSTEM");
            }
            catch (Exception ex) { _code.Logs(_connectionString, "NextAccActions", "e-Tax ใบลดหนี้อัตโนมัติล้มเหลว: " + ex.Message, "SYSTEM"); }
        }

        /// <summary>ส่ง PDF เอกสาร NextAcc ทางอีเมล (send-email — DocumentController.cs:104-121) · ส่งไม่สำเร็จ NextAcc ตอบ success:false + log</summary>
        public async Task<NxActionResult> SendNextAccDocumentEmailAsync(Guid docId, string to, string subject, string body, string receiptId, int? cnId)
        {
            string em = (to ?? "").Trim();
            if (em.Length < 5 || em.IndexOf('@') < 1 || em.IndexOf(' ') >= 0) return NxActionResult.Fail("BLOCKED", "อีเมลไม่ถูกต้อง");
            if (!_config.CanUseCompanyEndpoints) return NxActionResult.Fail("BLOCKED", "เรียก company endpoint ไม่ได้");
            string by = NxWho();
            long actionId = NxActionInsert("DOC_EMAIL", receiptId, cnId, docId, null, _serializer.Serialize(new Dictionary<string, object> { { "to", em } }), by);
            try
            {
                var resp = await _apiClient.SendDocumentEmailAsync(docId, new NxSendDocumentEmailRequest
                {
                    To = em, Subject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim(), Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim()
                });
                bool ok = resp != null && resp.success;
                string m = ok ? "✅ ส่งอีเมลถึง " + em + " แล้ว (จาก NextAcc)" : "ส่งอีเมลไม่สำเร็จ: " + (resp?.data?.ErrorMessage ?? resp?.message ?? "-");
                NxActionUpdate(actionId, ok ? "DONE" : "FAILED", m, "EMAIL");
                _code.Logs(_connectionString, "NextAccActions", "ส่งอีเมลเอกสาร " + docId + ": " + m, by);
                return new NxActionResult { Ok = ok, Status = ok ? "DONE" : "FAILED", Message = m, ActionId = actionId };
            }
            catch (Exception ex)
            {
                string rc;
                string m = NxApiMessage(ex, out rc);
                NxActionUpdate(actionId, "FAILED", m);
                return new NxActionResult { Ok = false, Status = "FAILED", Message = m, RuleCode = rc, ActionId = actionId };
            }
        }

        /// <summary>PDF ของเอกสาร NextAcc (ใช้กับใบแทน — ตัวดึง PDF ของใบเสร็จยืนยันตัวตนด้วย Reference = เลขใบเสร็จ ซึ่งใบแทนไม่มี
        /// (Reference = เลขเอกสารเดิม DocumentService.cs:12881) จึงแยกทาง)</summary>
        public async Task<byte[]> DownloadNextAccDocumentPdfAsync(Guid docId)
        {
            try { return await _apiClient.GenerateDocumentPdfAsync(docId); }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "NextAccActions", "PDF " + docId + ": " + ex.Message, "SYSTEM");
                return null;
            }
        }
    }
}
