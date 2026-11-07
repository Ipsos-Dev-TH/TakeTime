using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// ใบลดหนี้ / คืนเงินลูกค้า → NextAcc (PHASE19_33) — ข้อมูลอยู่ที่ Credit_Note (CreditNoteService)
    ///
    /// คิว (Entity_Type = CREDIT_NOTE, payload {cnId, cnNo}):
    ///   CREATE_CREDIT_NOTE  ใบต้นทางเป็นมัดจำ → refund-deposit + realize-deposit(ForfeitAs) · ใบกำกับ → ใบลดหนี้ + JE ขาเงินคืน
    ///   RECORD_CN_REFUND    คืนเงินภายหลังของใบที่ส่งไปแล้ว (JE Dr ลูกหนี้ / Cr แหล่งเงิน)
    ///   VOID_CREDIT_NOTE    ยกเลิกใบลดหนี้ (company void + กลับ JE ขาเงิน)
    ///
    /// สัญญา NextAcc (Wachira-d/Accounting HEAD 8c9cf5b):
    ///   • ใบลดหนี้ company: POST /document DocumentType=6 + RelatedDocumentId + CreditNoteReason (DocumentDtos.cs:23,77)
    ///     → approve (DocumentController.cs:660) → JE: Dr รายได้ต่อบรรทัด + Dr 21911 / Cr **111x เงินสด** เมื่อใบต้นทางชำระครบ
    ///     (BalanceDue ≤ 0.01) ไม่งั้น Cr ลูกหนี้ของใบต้นทาง (DocumentService.cs:17899-17925) — เลือกบัญชีเงินไม่ได้
    ///     ⇒ TakeTime โพสต์ JE "CN-{id}-MNY" ย้ายขาเงินจากบัญชีที่ NextAcc ลง (อ่านจาก GET /accounting/journals?sourceDocumentId=,
    ///       AccountingController.cs:105-116) ไปแหล่งเงินที่เลือกคืนจริง
    ///   • คืนมัดจำ: POST /document/{id}/refund-deposit {Amount, RefundDate, Reason, MoneyAccountId} (DocumentController.cs:533-545)
    ///     → JE Dr หนี้สินมัดจำ (+Dr 21913/21911) / Cr MoneyAccountId + ใบลดหนี้อัตโนมัติเมื่อ VAT มัดจำเข้า ภ.พ.30 แล้ว
    ///     (DocumentService.cs:5429-5662)
    ///   • ยึดมัดจำ: POST /document/{id}/realize-deposit {Amount(ฐาน), ForfeitAs} (DocumentController.cs:590-602) — NextAcc ตัดสิน VAT
    ///     ของยอดที่ยึดเอง (ออกใบกำกับของยอดที่ยึดเมื่อมัดจำยังไม่เคยเสีย VAT — DocumentService.cs:4866-4950)
    ///   • ไม่มี company endpoint (int_ + Nexaacc_Company_Endpoints=0): /integration/credit-notes (IntegrationService.cs:1402-1500,
    ///     JE Cr ลูกหนี้ :3040-3086) + JE Dr ROOM_AR / Cr แหล่งเงิน · มัดจำ → JE แบบเดิม (MapRefundToJournal/MapForfeitDepositToJournal)
    /// </summary>
    public partial class AccountingSyncService
    {
        private const string CnEntity = "CREDIT_NOTE";

        // ── Enqueue ─────────────────────────────────────────────────────────────

        public long EnqueueCreditNote(int cnId, string cnNo)
        {
            if (!_config.IsConfigured || string.IsNullOrWhiteSpace(cnNo)) return -1;
            string st;
            long existing = FindUnsentEntry(CnEntity, "CREATE_CREDIT_NOTE", "cnNo", cnNo, out st);
            if (existing > 0) return existing;
            var payload = new Dictionary<string, object> { { "cnId", cnId }, { "cnNo", cnNo } };
            return InsertQueue(CnEntity, cnId, "CREATE_CREDIT_NOTE", payload);
        }

        public long EnqueueCreditNoteRefund(int cnId, string cnNo)
        {
            if (!_config.IsConfigured || string.IsNullOrWhiteSpace(cnNo)) return -1;
            string st;
            long existing = FindUnsentEntry(CnEntity, "RECORD_CN_REFUND", "cnNo", cnNo, out st);
            if (existing > 0) return existing;
            var payload = new Dictionary<string, object> { { "cnId", cnId }, { "cnNo", cnNo } };
            return InsertQueue(CnEntity, cnId, "RECORD_CN_REFUND", payload);
        }

        public long EnqueueVoidCreditNote(int cnId, string cnNo, string reason)
        {
            if (!_config.IsConfigured || string.IsNullOrWhiteSpace(cnNo)) return -1;
            string st;
            long existing = FindUnsentEntry(CnEntity, "VOID_CREDIT_NOTE", "cnNo", cnNo, out st);
            if (existing > 0) return existing;
            var payload = new Dictionary<string, object> { { "cnId", cnId }, { "cnNo", cnNo }, { "reason", reason ?? "" } };
            return InsertQueue(CnEntity, cnId, "VOID_CREDIT_NOTE", payload);
        }

        /// <summary>ถอนคิวสร้างใบลดหนี้ที่ยังไม่ได้ส่ง (PENDING/FAILED → CANCELLED) · false = กำลังประมวลผลอยู่</summary>
        public bool CancelPendingCreditNoteQueue(string cnNo)
        {
            if (string.IsNullOrWhiteSpace(cnNo)) return true;
            try
            {
                var p = new Dictionary<string, object> { { "@p", "%\"cnNo\":\"" + cnNo.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "\"%" } };
                // ถอนก่อน แล้วตรวจว่ามีตัวที่ถูกหยิบไปประมวลผลแล้วหรือไม่ — เดิมตรวจก่อนถอน: คิวหยิบไปแทรกระหว่างสองคำสั่งได้
                // (ถอนไม่โดน แต่ฝั่งเรายกเลิกใบไปแล้วขณะที่ NextAcc กำลังสร้างเอกสาร)
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Accounting_Sync_Queue SET Status = 'CANCELLED', Error_Message = N'ยกเลิก: ใบลดหนี้ถูกยกเลิกก่อนส่ง NextAcc'
                       WHERE Entity_Type = 'CREDIT_NOTE' AND Action_Type = 'CREATE_CREDIT_NOTE' AND Status IN ('PENDING', 'FAILED') AND Payload LIKE @p", p);
                var busy = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 ID FROM Accounting_Sync_Queue
                       WHERE Entity_Type = 'CREDIT_NOTE' AND Action_Type = 'CREATE_CREDIT_NOTE' AND Status = 'PROCESSING' AND Payload LIKE @p", p);
                return busy == null || busy.Rows.Count == 0;
            }
            catch { return false; }
        }

        // ── ตัวช่วย ─────────────────────────────────────────────────────────────

        private string CnCompanyPath => "/api/companies/" + _config.CompanyId;

        private void CnSet(int id, string setSql, Dictionary<string, object> ps = null)
        {
            if (ps == null) ps = new Dictionary<string, object>();
            ps["@id"] = id;
            _code.DatabaseInsertSafe(_connectionString, "UPDATE Credit_Note SET " + setSql + " WHERE ID = @id", ps);
        }

        private void CnMarker(int id, string marker)
        {
            CnSet(id, "Nexaacc_Marker = @m", new Dictionary<string, object> { { "@m", (object)marker ?? DBNull.Value } });
        }

        /// <summary>marker เป็นชุดเฟสคั่นด้วย '|' — token ที่ลงท้าย ':' = ขึ้นต้นด้วย (REF:{id}) · ไม่งั้นต้องตรงทั้งเฟส
        /// (กัน "FFT" ไปตรงกับ "FFTPRE:" ที่จดไว้ก่อนยิง แล้วข้ามการยึดมัดจำที่ยังไม่ได้ส่ง)</summary>
        private static bool CnMarkerHas(string marker, string token)
        {
            if (string.IsNullOrEmpty(marker) || string.IsNullOrEmpty(token)) return false;
            foreach (string part in marker.Split('|'))
            {
                if (token.EndsWith(":", StringComparison.Ordinal) ? part.StartsWith(token, StringComparison.Ordinal) : part == token)
                    return true;
            }
            return false;
        }

        /// <summary>เอกสาร NextAcc ของใบเสร็จต้นทาง: Account_Receipt.Nexaacc_Doc_Id (PHASE18_32) → คิว CREATE ที่สำเร็จ →
        /// marker DOC:/APR: (เฉพาะสองเฟสนี้ — ค่าสุดท้ายของเส้น int_ เป็น payment id ไม่ใช่เอกสาร)</summary>
        private Guid ResolveCnSourceDocId(string receiptId)
        {
            if (string.IsNullOrWhiteSpace(receiptId)) return Guid.Empty;
            receiptId = receiptId.Trim();
            if (IsOtaSalesDocRef(receiptId)) return LookupOtaSalesDocId(receiptId);
            try
            {
                var link = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Nexaacc_Doc_Id FROM Account_Receipt WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", receiptId } });
                Guid g;
                if (link != null && link.Rows.Count > 0 && link.Rows[0][0] != DBNull.Value
                    && Guid.TryParse(link.Rows[0][0].ToString(), out g) && g != Guid.Empty)
                    return g;
            }
            catch { /* ยังไม่รัน PHASE18_32 */ }
            Guid q = LookupNexaaccDocIdByReceipt(receiptId);
            if (q != Guid.Empty) return q;
            string raw = (LookupReceiptPaymentMarkerRaw(receiptId) ?? "").Trim();
            if (raw.StartsWith("DOC:", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("APR:", StringComparison.OrdinalIgnoreCase))
            {
                Guid m;
                if (Guid.TryParse(raw.Substring(4).Trim(), out m)) return m;
            }
            return Guid.Empty;
        }

        /// <summary>ใบเสร็จต้นทางยังค้างคิวสร้างเอกสาร (PENDING/PROCESSING/FAILED) — ต้องรอให้ส่งก่อน ไม่ใช่ตกไปเส้นสำรอง</summary>
        private bool SourceReceiptStillQueued(string receiptId)
        {
            string st;
            return FindUnsentEntry("RECEIPT", "CREATE_RECEIPT_DOCUMENT", "receiptNumber", (receiptId ?? "").Trim(), out st) > 0;
        }

        private async Task<NxDocumentDetail> GetNxDocumentAsync(Guid docId)
        {
            var r = await _apiClient.GetAsync<ApiResponse<NxDocumentDetail>>(CnCompanyPath + "/document/" + docId);
            if (r?.data == null) throw new Exception("อ่านเอกสาร NextAcc " + docId + " ไม่ได้");
            return r.data;
        }

        /// <summary>บัญชี (GUID) ของแหล่งเงินที่จ่ายคืน — Account_Paid_How.Nexaacc_AccountId · เกตเวย์ไม่ระบุ = แหล่งเงินของใบต้นทาง</summary>
        private string CnRefundAccountGuid(CreditNoteService.Record cn)
        {
            if (cn.RefundPaidHowId.HasValue)
            {
                try
                {
                    var dt = _code.DatabaseQuerySafe(_connectionString,
                        "SELECT TOP 1 Nexaacc_AccountId FROM Account_Paid_How WHERE ID = @id",
                        new Dictionary<string, object> { { "@id", cn.RefundPaidHowId.Value } });
                    if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                    {
                        string s = dt.Rows[0][0].ToString().Trim();
                        if (s.Length > 0) return s;
                    }
                }
                catch { }
            }
            if (cn.RefundMethod == CreditNoteService.MGateway)
            {
                var hdr = LookupReceiptHeaderInfo(cn.SourceReceiptId);
                if (hdr.HasValue && !string.IsNullOrEmpty(hdr.Value.paymentAccountId)) return hdr.Value.paymentAccountId;
            }
            return null;
        }

        private string CnRefundAccountCode(CreditNoteService.Record cn)
        {
            string g = CnRefundAccountGuid(cn);
            Guid id;
            if (!string.IsNullOrEmpty(g) && Guid.TryParse(g, out id) && id != Guid.Empty)
            {
                string code = _mapper.GetAccountCodeFromId(id);
                if (!string.IsNullOrEmpty(code)) return code;
            }
            if (cn.RefundMethod == CreditNoteService.MCash)
            {
                string c = CnSafeCode("CASH");
                if (!string.IsNullOrEmpty(c)) return c;
            }
            throw new ArgumentException("ใบลดหนี้ " + cn.CnNo + ": แหล่งเงินที่จ่ายคืน" +
                (string.IsNullOrEmpty(cn.RefundPaidHowName) ? "" : " (" + cn.RefundPaidHowName + ")") +
                " ยังไม่ได้ผูกบัญชี NextAcc — ผูกที่ Admin → Accounting Integration → แหล่งเงิน แล้วกด Retry");
        }

        private string CnSafeCode(string mappingKey)
        {
            try { return _mapper.GetAccountCode(mappingKey); } catch { return null; }
        }

        /// <summary>JE ของเอกสาร (Posted, ยังไม่ถูกกลับ) — AccountingController.cs:105-116 รองรับ sourceDocumentId (มีบรรทัด + รหัสบัญชี)</summary>
        private async Task<JournalEntryResponse> FindDocumentJournalAsync(Guid docId)
        {
            var r = await _apiClient.GetAsync<ApiResponse<PagedResponse<JournalEntryResponse>>>(
                CnCompanyPath + "/accounting/journals?page=1&pageSize=20&sourceDocumentId=" + docId);
            var items = r?.data?.Items ?? new List<JournalEntryResponse>();
            return items
                .Where(j => !IsVoidedStatus(j.Status) && j.OriginalEntryId == null
                         && (j.ReversedByEntryId == null || j.ReversedByEntryId == Guid.Empty)
                         && j.Lines != null && j.Lines.Count > 0)
                .OrderByDescending(j => j.CreatedAt)
                .FirstOrDefault();
        }

        private async Task<Guid> PostCnJournalAsync(string externalRef, DateTime date, string description,
            List<Tuple<string, string, decimal, string>> legs)
        {
            // legs: (Dr code, Cr code, amount, description) — ข้ามขาที่เดบิต=เครดิตบัญชีเดียวกัน/ยอด 0
            var lines = new List<IntegrationJournalLineRequest>();
            foreach (var l in legs)
            {
                decimal amt = Math.Round(l.Item3, 2, MidpointRounding.AwayFromZero);
                if (amt <= 0.004m || string.IsNullOrEmpty(l.Item1) || string.IsNullOrEmpty(l.Item2)) continue;
                if (string.Equals(l.Item1, l.Item2, StringComparison.OrdinalIgnoreCase)) continue;
                lines.Add(new IntegrationJournalLineRequest { AccountCode = l.Item1, DebitAmount = amt, CreditAmount = 0, Description = l.Item4 });
                lines.Add(new IntegrationJournalLineRequest { AccountCode = l.Item2, DebitAmount = 0, CreditAmount = amt, Description = l.Item4 });
            }
            if (lines.Count == 0) return Guid.Empty;
            var res = await _apiClient.CreateIntegrationJournalAsync(new CreateIntegrationJournalRequest
            {
                ExternalId = externalRef,
                ExternalRef = externalRef,          // idempotent by ExternalRef (IntegrationService.cs:4490-4508)
                EntryDate = date,
                JournalType = "general",
                Description = description,
                Lines = lines,
                AutoBalanceVat = false
            });
            return RequireValidDocId(res?.data?.Id, "CreditNote JE " + externalRef);
        }

        private static string CnDecode(Exception ex)
        {
            var api = ex as AccountingApiException;
            string body = api != null ? DecodeUnicodeEscapes(api.ResponseBody ?? "") : "";
            string msg = (ex.Message ?? "") + (body.Length > 0 ? " — " + body : "");
            return msg.Length > 1000 ? msg.Substring(0, 1000) : msg;
        }

        // ── CREATE_CREDIT_NOTE ──────────────────────────────────────────────────

        private async Task<string> ProcessCreditNote(Dictionary<string, object> p)
        {
            int cnId = Convert.ToInt32(p["cnId"]);
            var svc = new CreditNoteService(_connectionString);
            var cn = svc.Load(cnId);
            if (cn == null) throw new ArgumentException("ไม่พบใบลดหนี้ #" + cnId + " (Credit_Note)");
            if (cn.Status == CreditNoteService.StVoided) return "SKIPPED_VOIDED";
            if (cn.Status == CreditNoteService.StDraft) throw new ArgumentException("ใบลดหนี้ " + cn.CnNo + " ยังไม่อนุมัติ");
            if (cn.Status == CreditNoteService.StSynced && cn.NexaaccMarker == "DONE") return cn.NexaaccDocId.HasValue ? cn.NexaaccDocId.Value.ToString() : "ALREADY_SYNCED";
            if (_config.IsReceiptLocal)
            {
                CnSet(cnId, "Nexaacc_Path = 'LOCAL'");
                return "SKIPPED_LOCAL_MODE";
            }
            try
            {
                // "จะคืนภายหลัง" ตอนเริ่มส่ง — ถ้าระหว่างส่งมีคน "บันทึกคืนเงิน" (สถานะยัง APPROVED → RecordRefund ไม่เข้าคิว RFD
                // เพราะคิดว่าจะลงพร้อมใบ) ขาเงินคืนจะหายไป ⇒ ตรวจซ้ำหลังส่งแล้วเข้าคิว RECORD_CN_REFUND เอง (JE CN-{id}-RFD idempotent)
                bool refundWasPending = cn.RefundStatus == CreditNoteService.RsPending;
                string result = cn.SourceDocType == CreditNoteService.SrcDeposit
                    ? await ProcessCnDepositAsync(cn)
                    : await ProcessCnInvoiceAsync(cn);
                CnSet(cnId, "Status = 'SYNCED', Sync_Error = NULL, Nexaacc_Marker = 'DONE'");
                _code.Logs(_connectionString, "AccountingSync", $"CreditNote {cn.CnNo}: ส่ง NextAcc สำเร็จ ({result})", "SYSTEM");
                if (refundWasPending)
                {
                    try
                    {
                        var after = svc.Load(cnId);
                        if (after != null && after.RefundStatus == CreditNoteService.RsPaid && after.RefundAmount > 0.004m)
                        {
                            long q = EnqueueCreditNoteRefund(cnId, cn.CnNo);
                            _code.Logs(_connectionString, "AccountingSync",
                                $"CreditNote {cn.CnNo}: บันทึกคืนเงินระหว่างส่ง → เข้าคิว RECORD_CN_REFUND #{q}", "SYSTEM");
                        }
                    }
                    catch { }
                }
                return result;
            }
            catch (Exception ex)
            {
                try { CnSet(cnId, "Sync_Error = @e", new Dictionary<string, object> { { "@e", CnDecode(ex) } }); } catch { }
                throw;
            }
        }

        /// <summary>ใบต้นทางเป็นใบกำกับ/ใบเสร็จ (ไม่ใช่มัดจำ) → ใบลดหนี้ company + JE ขาเงินคืน</summary>
        private async Task<string> ProcessCnInvoiceAsync(CreditNoteService.Record cn)
        {
            Guid srcDocId = ResolveCnSourceDocId(cn.SourceReceiptId);
            // ใบต้นทางยังค้างคิว → รอ (ทั้งสองเส้น) ไม่งั้นเส้น integration ออกใบลดหนี้ที่ไม่ผูกเอกสารเดิม (OriginalInvoiceRef หาไม่เจอ)
            if (srcDocId == Guid.Empty && SourceReceiptStillQueued(cn.SourceReceiptId))
                throw QueueErrors.Wait("ใบต้นทาง " + cn.SourceReceiptId + " ยังรอส่งเข้า NextAcc (คิว CREATE_RECEIPT_DOCUMENT) — ระบบจะลองใหม่อัตโนมัติ");
            if (!_config.CanUseCompanyEndpoints) return await ProcessCnIntegrationAsync(cn, srcDocId);
            if (srcDocId == Guid.Empty)
            {
                // ใบต้นทางไม่เคยเป็นเอกสารบน NextAcc (ยุค JOURNAL/ข้อมูลเก่า) → ใบลดหนี้เส้น integration (อ้างเลขใบเดิมเป็นข้อความ)
                _code.Logs(_connectionString, "AccountingSync",
                    "CreditNote " + cn.CnNo + ": ไม่พบเอกสาร NextAcc ของ " + cn.SourceReceiptId + " → ใช้ /integration/credit-notes (Cr ลูกหนี้) + JE ขาเงิน", "SYSTEM");
                return await ProcessCnIntegrationAsync(cn, Guid.Empty);
            }

            bool refundPaid = cn.RefundAmount > 0.004m && cn.RefundStatus == CreditNoteService.RsPaid;
            string marker = cn.NexaaccMarker ?? "";
            Guid docId = cn.NexaaccDocId ?? Guid.Empty;
            bool approved = marker.StartsWith("APR:", StringComparison.Ordinal) || marker == "MNY";
            NxDocumentDetail src = await GetNxDocumentAsync(srcDocId);

            if (docId == Guid.Empty || !marker.StartsWith("DOC:", StringComparison.Ordinal) && !approved)
            {
                if (src.Status == NexaaccDocumentStatus.Voided)
                    throw new ArgumentException("เอกสารต้นทาง " + src.DocumentNumber + " ถูกยกเลิกบน NextAcc แล้ว — ออกใบลดหนี้อ้างไม่ได้ (§86/9-10)");
                if (src.Contact == null || src.Contact.Id == Guid.Empty)
                    throw new Exception("เอกสารต้นทาง " + src.DocumentNumber + " ไม่มีผู้ติดต่อ — ใบลดหนี้ต้องออกให้คู่ค้าเดียวกับใบเดิม");
                if (src.BalanceDue <= 0.01m && !refundPaid && cn.RefundStatus != CreditNoteService.RsPending)
                    throw new ArgumentException("เอกสารต้นทาง " + src.DocumentNumber + " ชำระครบแล้ว — ใบลดหนี้ต้องคืนเงิน (เลือกแหล่งเงินคืน) หรือตั้ง \"จะคืนภายหลัง\"");

                var req = BuildCnDocumentRequest(cn, src, srcDocId);
                var created = await _apiClient.PostAsync<CreditNoteDocumentRequest, ApiResponse<DocumentResponse>>(CnCompanyPath + "/document", req);
                docId = RequireValidDocId(created?.data?.Id, "CreateCreditNote " + cn.CnNo);
                CnSet(cn.Id, "Nexaacc_Doc_Id = @g, Nexaacc_Doc_No = @n, Nexaacc_Path = 'COMPANY_CN', Nexaacc_Marker = @m",
                    new Dictionary<string, object> { { "@g", docId }, { "@n", (object)created?.data?.DocumentNumber ?? DBNull.Value }, { "@m", "DOC:" + docId } });
                marker = "DOC:" + docId;
            }

            if (!approved)
            {
                string num = null;
                try
                {
                    var apr = await _apiClient.ApproveDocumentAsync(docId, new ApproveDocumentRequest { AcknowledgeWarnings = true });
                    num = apr?.data?.DocumentNumber;
                    int st = apr?.data?.Status ?? -1;
                    if (st == NexaaccDocumentStatus.Draft || st == NexaaccDocumentStatus.WaitingApproval || st == NexaaccDocumentStatus.Rejected)
                        throw new Exception("อนุมัติใบลดหนี้ไม่สำเร็จ — สถานะ " + st + " (" + cn.CnNo + ")");
                }
                catch (AccountingApiException ex) when (IsAlreadyApprovedOrPosted(ex) || IsApproveStateConflict(ex))
                {
                    if (!await IsDocumentPostedAsync(docId))
                        throw new Exception("อนุมัติใบลดหนี้ไม่ได้และตรวจแล้วยังไม่ถูกโพสต์ " + cn.CnNo + " — " + DecodeUnicodeEscapes(ex.ResponseBody ?? ""));
                }
                if (string.IsNullOrEmpty(num) || num.StartsWith("DRAFT", StringComparison.OrdinalIgnoreCase))
                {
                    try { num = (await GetNxDocumentAsync(docId)).DocumentNumber; } catch { }
                }
                CnSet(cn.Id, "Nexaacc_Marker = @m, Nexaacc_Doc_No = ISNULL(@n, Nexaacc_Doc_No)",
                    new Dictionary<string, object> { { "@m", "APR:" + docId }, { "@n", (object)num ?? DBNull.Value } });
                marker = "APR:" + docId;
            }

            if (marker != "MNY")
            {
                // ขาเงิน: NextAcc ลง Cr 111x (ใบต้นทางชำระครบ) หรือ Cr ลูกหนี้ — ย้ายไปแหล่งเงินที่คืนจริง
                var je = await FindDocumentJournalAsync(docId);
                if (je == null)
                    throw QueueErrors.Wait("ยังไม่พบรายการบัญชี (JE) ของใบลดหนี้ " + cn.CnNo + " บน NextAcc — จะลองใหม่", null, 2);
                var counter = je.Lines.Where(l => l.CreditAmount > 0).OrderByDescending(l => l.CreditAmount).FirstOrDefault();
                if (counter == null || string.IsNullOrEmpty(counter.AccountCode))
                    throw new Exception("อ่านขาเครดิตของ JE ใบลดหนี้ " + cn.CnNo + " ไม่ได้");
                string counterCode = counter.AccountCode.Trim();
                decimal cnTotal = counter.CreditAmount;
                bool counterIsAr = counterCode.StartsWith("113", StringComparison.Ordinal);
                string arCode = CnSafeCode("ROOM_AR");
                var legs = new List<Tuple<string, string, decimal, string>>();
                string desc = "คืนเงินตามใบลดหนี้ " + cn.CnNo + " อ้าง " + cn.SourceReceiptId;
                if (refundPaid)
                {
                    string refundCode = CnRefundAccountCode(cn);
                    decimal refund = Math.Min(cn.RefundAmount, cnTotal);
                    legs.Add(Tuple.Create(counterCode, refundCode, refund, desc));
                    if (!counterIsAr && cnTotal - refund > 0.004m)
                        legs.Add(Tuple.Create(counterCode, arCode, cnTotal - refund, "ยอดลดหนี้ที่ยังไม่คืน (เครดิตลูกค้า) " + cn.CnNo));
                }
                else if (!counterIsAr)
                {
                    if (string.IsNullOrEmpty(arCode))
                        throw new ArgumentException("ไม่พบ mapping ROOM_AR — ตั้งค่าบัญชีลูกหนี้ก่อน (ใบลดหนี้ที่ยังไม่คืนเงินต้องพักเป็นเครดิตลูกค้า)");
                    legs.Add(Tuple.Create(counterCode, arCode, cnTotal, "ลดหนี้รอคืนเงินลูกค้า " + cn.CnNo));
                }
                Guid jid = await PostCnJournalAsync("CN-" + cn.Id + "-MNY", cn.CnDate, desc, legs);
                CnSet(cn.Id, "Nexaacc_Counter_Code = @c, Nexaacc_Refund_Id = @j, Nexaacc_Marker = 'MNY'",
                    new Dictionary<string, object> { { "@c", counterCode }, { "@j", jid == Guid.Empty ? (object)DBNull.Value : jid.ToString() } });
            }
            return docId.ToString();
        }

        private CreditNoteDocumentRequest BuildCnDocumentRequest(CreditNoteService.Record cn, NxDocumentDetail src, Guid srcDocId)
        {
            // บัญชีรายได้ต่อบรรทัด: บรรทัดลำดับเดียวกันของเอกสาร NextAcc ต้นทาง (ใบต้นทางสร้างจากบรรทัดบวกของใบเสร็จตามลำดับเดียวกัน —
            // LookupReceiptLinesEx) → Account_Code ที่คำนวณตอนสร้าง → บรรทัดแรกของต้นทาง
            var srcLines = (src.Lines ?? new List<NxDocumentLine>()).OrderBy(l => l.LineOrder).ToList();
            var ttLines = LookupReceiptLines(cn.SourceReceiptId, cn.ReservationId, cn.AmountGross, null) ?? new List<AccountingDataMapper.ReceiptLineSpec>();
            var srcNos = new List<int>();
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT Number, Price_Amount, Product_Amount, Price_PerPeice FROM Account_Receipt_Detail
                       WHERE Receipt_ID = @r ORDER BY Number",
                    new Dictionary<string, object> { { "@r", cn.SourceReceiptId } });
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                    {
                        decimal q = r["Product_Amount"] != DBNull.Value ? Convert.ToDecimal(r["Product_Amount"]) : 1m;
                        decimal u = r["Price_PerPeice"] != DBNull.Value ? Convert.ToDecimal(r["Price_PerPeice"]) : 0m;
                        decimal a = r["Price_Amount"] != DBNull.Value ? Convert.ToDecimal(r["Price_Amount"]) : q * u;
                        if (a <= 0) continue;
                        int no;
                        srcNos.Add(int.TryParse(Convert.ToString(r["Number"]), out no) ? no : srcNos.Count + 1);
                    }
            }
            catch { }
            bool aligned = srcLines.Count > 0 && srcLines.Count == srcNos.Count && ttLines.Count == srcNos.Count;
            NxDocumentLine firstRev = srcLines.FirstOrDefault(l => l.AccountId.HasValue || !string.IsNullOrEmpty(l.AccountCode));
            decimal defaultRate = src.VatAmount > 0 ? 7m : 0m;

            var lines = new List<DocumentLineRequest>();
            foreach (var l in cn.Lines.OrderBy(x => x.LineNo))
            {
                if (l.Amount <= 0) continue;
                NxDocumentLine match = null;
                if (aligned && l.SourceLineNo.HasValue)
                {
                    int idx = srcNos.IndexOf(l.SourceLineNo.Value);
                    if (idx >= 0 && idx < srcLines.Count) match = srcLines[idx];
                }
                Guid? accId = null;
                string accCode = null;
                if (match != null && (match.AccountId.HasValue || !string.IsNullOrEmpty(match.AccountCode)))
                { accId = match.AccountId; accCode = match.AccountCode; }
                else if (!string.IsNullOrWhiteSpace(l.AccountCode)) accCode = l.AccountCode.Trim();
                else if (firstRev != null) { accId = firstRev.AccountId; accCode = firstRev.AccountCode; }
                decimal rate = match != null ? match.VatRate : (cn.VatAmount > 0 ? 7m : defaultRate);
                if (rate < 0) rate = 0;   // ยกเว้น (−1) → ใบลดหนี้ไม่มี VAT

                decimal qty = l.Qty > 0 ? l.Qty : 1m;
                decimal unit = Math.Round(l.Amount / qty, 2, MidpointRounding.AwayFromZero);
                string descr = string.IsNullOrWhiteSpace(l.Description) ? "ลดหนี้ตาม " + cn.SourceReceiptId : l.Description.Trim();
                if (unit * qty != l.Amount) { descr += " (" + qty.ToString("0.##") + " " + (l.Unit ?? "") + ")"; qty = 1m; unit = l.Amount; }
                // ไม่ส่ง ProductCode — สต๊อก/ต้นทุนรับคืน TakeTime ลงเองผ่าน STOCK_OUT_COGS_REVERSE (NextAcc ไม่ restock บรรทัดที่ไม่มี ProductCode
                // — DocumentService.cs:17401-17409) กันกลับต้นทุนซ้ำ
                lines.Add(new DocumentLineRequest
                {
                    Description = descr.Length > 500 ? descr.Substring(0, 500) : descr,
                    Quantity = qty,
                    Unit = string.IsNullOrWhiteSpace(l.Unit) ? null : l.Unit,
                    UnitPrice = unit,
                    DiscountPercent = 0,
                    VatRate = rate,
                    WithholdingTaxRate = 0,
                    AccountId = accId,
                    AccountCode = accId.HasValue ? null : accCode
                });
            }
            if (lines.Count == 0) throw new ArgumentException("ใบลดหนี้ " + cn.CnNo + " ไม่มีรายการยอด > 0");

            DateTime date = cn.CnDate.Date < src.DocumentDate.Date ? src.DocumentDate.Date : cn.CnDate.Date;   // §86/10 ไม่ก่อนใบเดิม
            string note = "ใบลดหนี้ " + cn.CnNo + " อ้าง " + src.DocumentNumber + " (" + cn.SourceReceiptId + ") — เหตุผล: "
                          + CreditNoteService.ReasonLabel(cn.ReasonCode) + (string.IsNullOrWhiteSpace(cn.ReasonText) ? "" : " · " + cn.ReasonText);
            return new CreditNoteDocumentRequest
            {
                DocumentType = 6,
                DocumentDate = date,
                DueDate = date,
                ContactId = src.Contact.Id,
                Reference = cn.CnNo,
                Notes = note.Length > 1000 ? note.Substring(0, 1000) : note,
                Lines = lines,
                PricesIncludeVat = true,
                RelatedDocumentId = srcDocId,
                CreditNoteReason = CreditNoteService.NextAccReason(cn.ReasonCode),
                BookingNumber = cn.ReservationId > 0 ? "RES-" + cn.ReservationId : null
            };
        }

        /// <summary>ไม่มี company endpoint: /integration/credit-notes (Cr ลูกหนี้) + JE Dr ROOM_AR / Cr แหล่งเงินคืน</summary>
        private async Task<string> ProcessCnIntegrationAsync(CreditNoteService.Record cn, Guid srcDocId)
        {
            string marker = cn.NexaaccMarker ?? "";
            Guid docId = cn.NexaaccDocId ?? Guid.Empty;
            if (docId == Guid.Empty || !(marker.StartsWith("APR:", StringComparison.Ordinal) || marker == "MNY"))
            {
                bool hasVat = cn.VatAmount > 0;
                var lines = new List<IntegrationLineRequest>();
                foreach (var l in cn.Lines.OrderBy(x => x.LineNo))
                {
                    if (l.Amount <= 0) continue;
                    decimal qty = l.Qty > 0 ? l.Qty : 1m;
                    // /integration/credit-notes คิดราคาแบบไม่รวม VAT (IntegrationService.ProcessCreditNoteAsync → BuildDocumentLinesAsync
                    // includeVat=false) — ยอดของเรารวม VAT ⇒ ถอด VAT ก่อนส่ง (เหมือน MapReceiptVoidToCreditNote) ไม่งั้น VAT ถูกบวกซ้ำ 7%
                    decimal net = hasVat ? Math.Round(l.Amount * 100m / 107m, 2, MidpointRounding.AwayFromZero) : l.Amount;
                    decimal unitNet = Math.Round(net / qty, 2, MidpointRounding.AwayFromZero);
                    if (unitNet * qty != net) { qty = 1m; unitNet = net; }
                    lines.Add(new IntegrationLineRequest
                    {
                        ItemName = string.IsNullOrWhiteSpace(l.Description) ? "ลดหนี้" : l.Description,
                        Description = l.Description,
                        Quantity = qty,
                        UnitPrice = unitNet,
                        Unit = l.Unit,
                        AccountCode = string.IsNullOrWhiteSpace(l.AccountCode) ? null : l.AccountCode,
                        VatRate = hasVat ? 7 : 0
                    });
                }
                var res = await _apiClient.CreateIntegrationCreditNoteAsync(new InboundCreditNoteRequest
                {
                    ExternalId = cn.CnNo,
                    ExternalRef = cn.CnNo,                     // idempotent (IntegrationService.cs:1409-1440)
                    OriginalInvoiceRef = cn.SourceReceiptId,
                    OriginalDocumentId = srcDocId == Guid.Empty ? (Guid?)null : srcDocId,
                    CustomerName = string.IsNullOrWhiteSpace(cn.CustomerName) ? "ลูกค้าทั่วไป" : cn.CustomerName,
                    DocumentDate = cn.CnDate,
                    Reason = CreditNoteService.ReasonLabel(cn.ReasonCode) + (string.IsNullOrWhiteSpace(cn.ReasonText) ? "" : " · " + cn.ReasonText),
                    Lines = lines,
                    Notes = "TakeTime " + cn.CnNo,
                    BookingNumber = cn.ReservationId > 0 ? "RES-" + cn.ReservationId : null
                });
                docId = RequireValidDocId(res?.data?.Id, "IntegrationCreditNote " + cn.CnNo);
                CnSet(cn.Id, "Nexaacc_Doc_Id = @g, Nexaacc_Doc_No = @n, Nexaacc_Path = 'INTEGRATION_CN', Nexaacc_Marker = @m",
                    new Dictionary<string, object> { { "@g", docId }, { "@n", (object)res?.data?.DocumentNumber ?? DBNull.Value }, { "@m", "APR:" + docId } });
                marker = "APR:" + docId;
            }
            if (marker != "MNY")
            {
                Guid jid = Guid.Empty;
                if (cn.RefundAmount > 0.004m && cn.RefundStatus == CreditNoteService.RsPaid)
                {
                    string ar = CnSafeCode("ROOM_AR");
                    if (string.IsNullOrEmpty(ar)) throw new ArgumentException("ไม่พบ mapping ROOM_AR สำหรับขาคืนเงินของใบลดหนี้");
                    jid = await PostCnJournalAsync("CN-" + cn.Id + "-MNY", cn.CnDate, "คืนเงินตามใบลดหนี้ " + cn.CnNo,
                        new List<Tuple<string, string, decimal, string>> { Tuple.Create(ar, CnRefundAccountCode(cn), cn.RefundAmount, "คืนเงินตามใบลดหนี้ " + cn.CnNo) });
                }
                CnSet(cn.Id, "Nexaacc_Counter_Code = @c, Nexaacc_Refund_Id = @j, Nexaacc_Marker = 'MNY'",
                    new Dictionary<string, object> { { "@c", (object)CnSafeCode("ROOM_AR") ?? DBNull.Value }, { "@j", jid == Guid.Empty ? (object)DBNull.Value : jid.ToString() } });
            }
            return docId.ToString();
        }

        /// <summary>ใบต้นทางเป็นมัดจำ → refund-deposit (คืน) + realize-deposit ForfeitAs (ยึด) · ไม่มีเอกสารมัดจำบน NextAcc → JE แบบเดิม</summary>
        private async Task<string> ProcessCnDepositAsync(CreditNoteService.Record cn)
        {
            Guid depId = ResolveCnSourceDocId(cn.SourceReceiptId);
            string marker = cn.NexaaccMarker ?? "";
            NxDocumentDetail dep = null;
            if (_config.CanUseCompanyEndpoints && depId != Guid.Empty)
            {
                dep = await GetNxDocumentAsync(depId);
                if (!dep.IsDeposit) dep = null;   // ใบเก่าที่ส่งเป็นใบกำกับ/JE → เส้น JE
            }
            if (dep == null && depId == Guid.Empty && !CnMarkerHas(marker, "JRN") && SourceReceiptStillQueued(cn.SourceReceiptId))
                throw QueueErrors.Wait("ใบมัดจำ " + cn.SourceReceiptId + " ยังรอส่งเข้า NextAcc (คิว CREATE_RECEIPT_DOCUMENT) — ระบบจะลองใหม่อัตโนมัติ");

            if (dep != null)
            {
                // 1) คืนเงิน — กันคืนซ้ำเมื่อ retry: จด DepositRefundedAmount ก่อนยิง (REFPRE) แล้วเทียบหลัง
                if (cn.RefundAmount > 0.004m && !CnMarkerHas(marker, "REF:"))
                {
                    bool already = false;
                    if (marker.StartsWith("REFPRE:", StringComparison.Ordinal))
                    {
                        decimal before;
                        if (decimal.TryParse(marker.Substring(7).Split('|')[0], System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out before)
                            && dep.DepositRefundedAmount >= before + cn.RefundAmount - 0.01m)
                            already = true;
                    }
                    if (!already)
                    {
                        CnMarker(cn.Id, "REFPRE:" + dep.DepositRefundedAmount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        Guid money;
                        string g = CnRefundAccountGuid(cn);
                        await _apiClient.PostAsync<NxRefundDepositRequest, ApiResponse<DocumentResponse>>(
                            CnCompanyPath + "/document/" + depId + "/refund-deposit",
                            new NxRefundDepositRequest
                            {
                                Amount = cn.RefundAmount,
                                RefundDate = cn.CnDate,
                                Reason = "ใบลดหนี้ " + cn.CnNo + " — " + CreditNoteService.ReasonLabel(cn.ReasonCode),
                                MoneyAccountId = !string.IsNullOrEmpty(g) && Guid.TryParse(g, out money) && money != Guid.Empty ? (Guid?)money : null
                            });
                    }
                    marker = "REF:" + depId;
                    CnSet(cn.Id, "Nexaacc_Marker = @m, Nexaacc_Path = 'DEPOSIT_REFUND', Nexaacc_Refund_Id = @r",
                        new Dictionary<string, object> { { "@m", marker }, { "@r", depId.ToString() } });
                    await LinkAutoCreditNoteAsync(cn, depId, dep.DocumentNumber);
                    dep = await GetNxDocumentAsync(depId);   // ยอดล่าสุดก่อนยึด
                }

                // 2) ส่วนที่ยึด (ค่าธรรมเนียมยกเลิก/ค่าเสียหาย) — NextAcc ตัดสิน VAT เอง
                if (cn.RetainedAmount > 0.004m && !CnMarkerHas(marker, "FFT"))
                {
                    decimal ratio = dep.TotalAmount > 0 ? dep.SubTotal / dep.TotalAmount : 1m;
                    decimal baseAmt = Math.Round(cn.RetainedAmount * ratio, 2, MidpointRounding.AwayFromZero);
                    bool already = false;
                    int pre = marker.IndexOf("|FFTPRE:", StringComparison.Ordinal);
                    if (pre >= 0)
                    {
                        decimal before;
                        if (decimal.TryParse(marker.Substring(pre + 8), System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out before)
                            && dep.DepositRealizedAmount >= before + baseAmt - 0.01m)
                            already = true;
                    }
                    else
                    {
                        marker = marker + "|FFTPRE:" + dep.DepositRealizedAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        CnMarker(cn.Id, marker);
                    }
                    if (!already && baseAmt > 0)
                        await _apiClient.PostAsync<NxRealizeDepositRequest, ApiResponse<DocumentResponse>>(
                            CnCompanyPath + "/document/" + depId + "/realize-deposit",
                            new NxRealizeDepositRequest
                            {
                                Amount = baseAmt,
                                RealizeDate = cn.CnDate,
                                ForfeitAs = cn.RetainedAs == CreditNoteService.RetCompensation ? 2 : 1
                            });
                    int cut = marker.IndexOf("|FFTPRE:", StringComparison.Ordinal);
                    marker = (cut >= 0 ? marker.Substring(0, cut) : marker) + "|FFT";
                    CnSet(cn.Id, "Nexaacc_Marker = @m, Nexaacc_Path = 'DEPOSIT_REFUND'", new Dictionary<string, object> { { "@m", marker } });
                }
                return depId.ToString();
            }

            // เส้น JE (ไม่มี company endpoint / มัดจำเก่าไม่ใช่เอกสารมัดจำ) — โหมด VAT มัดจำตามค่าตั้งปัจจุบัน
            CnMarker(cn.Id, "JRN");
            bool hasVat = LookupBusinessHasVat();
            Guid lastJ = Guid.Empty;
            if (cn.RefundAmount > 0.004m)
            {
                var j = _mapper.MapRefundToJournal(cn.ReservationId, cn.RefundAmount, cn.RefundPaidHowName ?? "CASH", cn.CnDate,
                    cn.CustomerName ?? "", hasVat, _config.IsDepositVatAtReceipt, _config.IsDepositOutputVatDeferred, CnRefundAccountGuid(cn));
                j.Reference = "CN-" + cn.Id + "-REF";
                j.Description = "คืนเงินมัดจำตามใบลดหนี้ " + cn.CnNo + " (" + cn.SourceReceiptId + ")";
                var r = await _apiClient.CreateJournalAsync(j);
                lastJ = RequireValidDocId(r?.data?.Id, "CreditNote deposit refund JE " + cn.CnNo);
            }
            if (cn.RetainedAmount > 0.004m)
            {
                var j2 = _mapper.MapForfeitDepositToJournal(cn.ReservationId, cn.RetainedAmount, cn.CustomerName ?? "", cn.CnDate,
                    "ยึดเป็นค่าธรรมเนียมยกเลิก ตามใบลดหนี้ " + cn.CnNo, hasVat, _config.IsDepositVatAtReceipt, _config.IsDepositOutputVatDeferred);
                j2.Reference = "CN-" + cn.Id + "-FFT";
                var r2 = await _apiClient.CreateJournalAsync(j2);
                lastJ = RequireValidDocId(r2?.data?.Id, "CreditNote deposit forfeit JE " + cn.CnNo);
            }
            CnSet(cn.Id, "Nexaacc_Path = 'JOURNAL', Nexaacc_Refund_Id = @j, Nexaacc_Doc_No = ISNULL(Nexaacc_Doc_No, @n)",
                new Dictionary<string, object> { { "@j", lastJ == Guid.Empty ? (object)DBNull.Value : lastJ.ToString() }, { "@n", "JE CN-" + cn.Id } });
            return lastJ == Guid.Empty ? "NO_AMOUNT" : lastJ.ToString();
        }

        /// <summary>หลัง refund-deposit: NextAcc สร้างใบลดหนี้ให้เอง (เฉพาะ VAT มัดจำเข้า ภ.พ.30 แล้ว) — ผูกเลขเข้าใบของเรา
        /// (ลูกของใบมัดจำผ่าน ConvertedToDocuments, DocumentDtos.cs:731-736)</summary>
        private async Task LinkAutoCreditNoteAsync(CreditNoteService.Record cn, Guid depId, string depNo)
        {
            try
            {
                var after = await GetNxDocumentAsync(depId);
                var taken = new HashSet<Guid>();
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Nexaacc_Doc_Id FROM Credit_Note WHERE Nexaacc_Doc_Id IS NOT NULL AND ID <> @id",
                    new Dictionary<string, object> { { "@id", cn.Id } });
                if (dt != null)
                    foreach (DataRow r in dt.Rows) { Guid g; if (Guid.TryParse(Convert.ToString(r[0]), out g)) taken.Add(g); }
                var auto = (after.ConvertedToDocuments ?? new List<NxDocumentBrief>())
                    .Where(d => d.DocumentType == 6 && d.Status != NexaaccDocumentStatus.Voided && !taken.Contains(d.Id))
                    .OrderByDescending(d => d.DocumentDate).ThenByDescending(d => d.DocumentNumber)
                    .FirstOrDefault();
                if (auto != null)
                    CnSet(cn.Id, "Nexaacc_Doc_Id = @g, Nexaacc_Doc_No = @n",
                        new Dictionary<string, object> { { "@g", auto.Id }, { "@n", auto.DocumentNumber } });
                else
                    CnSet(cn.Id, "Nexaacc_Doc_No = @n", new Dictionary<string, object> { { "@n", "คืนมัดจำ " + (depNo ?? "") } });
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync", "CreditNote " + cn.CnNo + ": หาเลขใบลดหนี้อัตโนมัติไม่ได้ " + ex.Message, "SYSTEM");
            }
        }

        // ── RECORD_CN_REFUND ────────────────────────────────────────────────────

        private async Task<string> ProcessCreditNoteRefund(Dictionary<string, object> p)
        {
            int cnId = Convert.ToInt32(p["cnId"]);
            var cn = new CreditNoteService(_connectionString).Load(cnId);
            if (cn == null) throw new ArgumentException("ไม่พบใบลดหนี้ #" + cnId);
            if (cn.Status == CreditNoteService.StVoided) return "SKIPPED_VOIDED";
            if (cn.Status != CreditNoteService.StSynced)
                throw QueueErrors.Wait("ใบลดหนี้ " + cn.CnNo + " ยังไม่ส่ง NextAcc — การคืนเงินจะลงพร้อมใบเมื่อส่ง");
            if (cn.RefundStatus != CreditNoteService.RsPaid || cn.RefundAmount <= 0.004m) return "SKIPPED_NO_REFUND";
            // ใบที่ส่งแบบ "จะคืนภายหลัง": ขาเงินถูกพักไว้ที่ลูกหนี้ (ROOM_AR หรือบัญชีลูกหนี้ของใบต้นทาง) → ตัดออกไปแหล่งเงินที่คืนจริง
            string from = cn.NexaaccPath == CreditNoteService.PathCompanyCn
                          && !string.IsNullOrEmpty(cn.NexaaccCounterCode) && cn.NexaaccCounterCode.StartsWith("113", StringComparison.Ordinal)
                ? cn.NexaaccCounterCode : CnSafeCode("ROOM_AR");
            if (string.IsNullOrEmpty(from)) throw new ArgumentException("ไม่พบบัญชีลูกหนี้ (ROOM_AR) สำหรับตัดยอดรอคืน");
            Guid jid = await PostCnJournalAsync("CN-" + cn.Id + "-RFD", DateTime.Today, "คืนเงินลูกค้าตามใบลดหนี้ " + cn.CnNo,
                new List<Tuple<string, string, decimal, string>> { Tuple.Create(from, CnRefundAccountCode(cn), cn.RefundAmount, "คืนเงินตามใบลดหนี้ " + cn.CnNo) });
            CnSet(cn.Id, "Nexaacc_Refund_Id = @j", new Dictionary<string, object> { { "@j", jid.ToString() } });
            return jid.ToString();
        }

        // ── VOID_CREDIT_NOTE ────────────────────────────────────────────────────

        private async Task<string> ProcessVoidCreditNote(Dictionary<string, object> p)
        {
            int cnId = Convert.ToInt32(p["cnId"]);
            string reason = p.ContainsKey("reason") ? Convert.ToString(p["reason"]) : null;
            var svc = new CreditNoteService(_connectionString);
            var cn = svc.Load(cnId);
            if (cn == null) throw new ArgumentException("ไม่พบใบลดหนี้ #" + cnId);
            if (cn.Status == CreditNoteService.StVoided) return "ALREADY_VOIDED";
            if (cn.NexaaccPath == CreditNoteService.PathDepositRefund
                || (cn.SourceDocType == CreditNoteService.SrcDeposit
                    && (cn.NexaaccPath == CreditNoteService.PathJournal || !string.IsNullOrEmpty(cn.NexaaccMarker))))
                throw new ArgumentException("ใบลดหนี้ " + cn.CnNo + " เป็นการคืนมัดจำบน NextAcc — ยกเลิกผ่าน API ไม่ได้ (ดู docs/NextAcc_Change_Requests.md)");

            if (cn.NexaaccDocId.HasValue)
            {
                Guid docId = cn.NexaaccDocId.Value;
                if (cn.NexaaccPath == CreditNoteService.PathIntegrationCn)
                {
                    await _apiClient.VoidDocumentViaIntegrationAsync(new InboundVoidDocumentRequest
                    {
                        ExternalId = cn.CnNo, ExternalRef = cn.CnNo, DocumentId = docId,
                        Reason = string.IsNullOrWhiteSpace(reason) ? "ยกเลิกใบลดหนี้ " + cn.CnNo : reason
                    });
                }
                else
                {
                    try { await _apiClient.PostActionAsync(CnCompanyPath + "/document/" + docId + "/void"); }
                    catch (AccountingApiException ex) when (IsAlreadyApprovedOrPosted(ex) && DecodeUnicodeEscapes(ex.ResponseBody ?? "").IndexOf("ยกเลิก", StringComparison.Ordinal) >= 0) { }
                    var d = await GetNxDocumentAsync(docId);
                    if (d.Status != NexaaccDocumentStatus.Voided)
                        throw new Exception("ยกเลิกใบลดหนี้ " + cn.CnNo + " บน NextAcc ยังไม่สำเร็จ (สถานะ " + d.Status + ") — อาจอยู่ในงวดที่ยื่น ภ.พ.30 แล้ว");
                }
            }
            await TryReverseJournalByReferenceAsync("CN-" + cn.Id + "-MNY", "กลับรายการขาเงินคืน — ยกเลิกใบลดหนี้ " + cn.CnNo);
            await TryReverseJournalByReferenceAsync("CN-" + cn.Id + "-RFD", "กลับรายการคืนเงิน — ยกเลิกใบลดหนี้ " + cn.CnNo);
            svc.MarkVoided(cnId);
            _code.Logs(_connectionString, "AccountingSync", "CreditNote " + cn.CnNo + ": ยกเลิกบน NextAcc แล้ว", "SYSTEM");
            return cn.NexaaccDocId.HasValue ? cn.NexaaccDocId.Value.ToString() : "VOIDED_LOCAL";
        }

        /// <summary>PDF ใบลดหนี้จาก NextAcc (company endpoint) — null = ยังไม่มีเอกสาร/ดึงไม่ได้</summary>
        public async Task<byte[]> DownloadCreditNotePdfAsync(Guid docId)
        {
            try { return await _apiClient.GenerateDocumentPdfAsync(docId); }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync", "CreditNote PDF " + docId + ": " + ex.Message, "SYSTEM");
                return null;
            }
        }
    }
}
