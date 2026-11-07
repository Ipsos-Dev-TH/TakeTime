using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Take_Time_BangPhra.Purchasing;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// ใบลดหนี้ผู้ขาย = ตัวจริง ↔ คืนสินค้า (PHASE19_39 — CLAUDE.md ข้อ 3):
    ///   • PURCHASE_RETURN_REVERSE  (PRT-…-REV) กลับ JE PRT ที่ลงไว้ระหว่างรอใบลดหนี้ เมื่อผูกใบลดหนี้ NextAcc แล้ว
    ///   • PURCHASE_RETURN_COSTDIFF (PRT-…-DIF) ปรับสินค้าคงเหลือจากราคาซื้อ (ใบลดหนี้) เป็นต้นทุนเฉลี่ย (CostService) ↔ PURCHASE_RETURNS
    ///   • VENDOR_BALANCE_JOURNAL   หักเครดิตผู้ขายตอนจ่าย (VCRED-…) / จ่ายใบเพิ่มหนี้ที่ตั้งเจ้าหนี้ (VDNPAY-…)
    ///   • เตือนรายวัน: คืนสินค้าที่วางบิลแล้วเกิน Vendor_Cn_Reminder_Days ยังไม่ได้ใบลดหนี้
    /// </summary>
    public partial class AccountingSyncService
    {
        public const string ActPurchaseReturnReverse = "PURCHASE_RETURN_REVERSE";
        public const string ActPurchaseReturnCostDiff = "PURCHASE_RETURN_COSTDIFF";
        public const string ActVendorBalanceJournal = "VENDOR_BALANCE_JOURNAL";

        /// <summary>การคืนนี้ถูกผูกใบลดหนี้แล้ว ⇒ ตัวสร้าง JE PRT ต้องข้าม (ใบลดหนี้เป็นตัวจริง; ถ้า PRT ลงไปแล้ว REV จะกลับให้)</summary>
        internal bool IsPurchaseReturnSupersededByCn(string returnRef)
        {
            try
            {
                var l = new VendorCnService(_connectionString).GetReturn(returnRef);
                return l != null && l.CnStatus == ReturnCnStatus.Linked;
            }
            catch { return false; }
        }

        // ── PRT-…-REV ──────────────────────────────────────────────────────────

        public long EnqueuePurchaseReturnReverse(string returnRef, DateTime reverseDate, string reason)
        {
            if (!_config.IsConfigured || string.IsNullOrWhiteSpace(returnRef)) return -1;
            string revRef = VendorCnPolicy.ReverseRef(returnRef);
            string st, resp;
            long existing = FindAnyStockQueueEntry(ActPurchaseReturnReverse, revRef, out st, out resp);
            if (existing > 0 && st != "CANCELLED") return existing;
            var payload = new Dictionary<string, object>
            {
                { "stockRef", revRef },
                { "returnRef", returnRef.Trim() },
                { "reverseDate", AcctDate(reverseDate, true) },
                { "reason", reason ?? "" }
            };
            return InsertQueue("STOCK", 0, ActPurchaseReturnReverse, payload);
        }

        private async Task<string> ProcessPurchaseReturnReverse(Dictionary<string, object> p)
        {
            string revRef = p["stockRef"]?.ToString();
            string returnRef = p.ContainsKey("returnRef") ? p["returnRef"]?.ToString() : (revRef ?? "").Replace("-REV", "");
            DateTime revDate = p.ContainsKey("reverseDate") ? ParseAcctDate(p["reverseDate"]?.ToString()) : DateTime.Today;
            string reason = p.ContainsKey("reason") ? p["reason"]?.ToString() : "";

            string st, resp;
            long prtId = FindAnyStockQueueEntry("PURCHASE_RETURN", returnRef, out st, out resp);
            if (prtId <= 0)
            {
                _code.Logs(_connectionString, "AccountingSync", $"PurchaseReturnReverse {revRef}: ไม่มีคิว JE PRT (ไม่เคยลงบัญชี) — ไม่ต้องกลับ", "SYSTEM");
                return "NOTHING_TO_REVERSE";
            }
            if (st == "PROCESSING")
                throw QueueErrors.Wait($"PurchaseReturnReverse {revRef}: คิว JE {returnRef} กำลังทำงาน — รอให้เสร็จแล้วกลับรายการ", prtId);
            Guid jid;
            if (st != "COMPLETED" || !Guid.TryParse(resp ?? "", out jid) || jid == Guid.Empty)
            {
                // PENDING/FAILED/AWAITING: ตัวสร้างเห็นว่าผูกใบลดหนี้แล้วจะข้ามเอง · COMPLETED แต่ไม่ใช่ GUID (SKIPPED_…) = ไม่เคยลง
                _code.Logs(_connectionString, "AccountingSync", $"PurchaseReturnReverse {revRef}: JE {returnRef} ยังไม่ลง (สถานะ {st}/{resp}) — ตัวสร้างจะข้ามเอง ไม่ต้องกลับ", "SYSTEM");
                return "NOTHING_TO_REVERSE";
            }

            string desc = $"กลับรายการคืนสินค้า {returnRef} — ใบลดหนี้ผู้ขายใน NextAcc เป็นรายการจริง" + (string.IsNullOrWhiteSpace(reason) ? "" : " (" + reason.Trim() + ")");
            try
            {
                var rev = await _apiClient.ReverseJournalWithRefAsync(jid, revRef, revDate, desc);
                if (rev?.success == true && rev.data != null && rev.data.Id != Guid.Empty)
                {
                    _code.Logs(_connectionString, "AccountingSync", $"PurchaseReturnReverse {revRef}: กลับ JE {jid} → {rev.data.Id}", "SYSTEM");
                    return rev.data.Id.ToString();
                }
                if (IsAlreadyReversedMessage(rev?.message)) return "ALREADY_REVERSED";
                throw new Exception($"PurchaseReturnReverse {revRef}: NextAcc ปฏิเสธ: {rev?.message ?? "null"}");
            }
            catch (Exception ex) when (IsAlreadyReversedMessage(ex.Message))
            {
                return "ALREADY_REVERSED";
            }
        }

        // ── PRT-…-DIF ──────────────────────────────────────────────────────────

        /// <summary>amount &gt; 0 = Dr INVENTORY / Cr PURCHASE_RETURNS · &lt; 0 = กลับทิศ (VendorCnPolicy.CostDifference)</summary>
        public long EnqueuePurchaseReturnCostDiff(string returnRef, decimal amount, DateTime entryDate, string supplierName, string cnDocNo)
        {
            if (!_config.IsConfigured || string.IsNullOrWhiteSpace(returnRef)) return -1;
            amount = VendorCnPolicy.R2(amount);
            if (Math.Abs(amount) < 0.005m) return -1;
            string difRef = VendorCnPolicy.CostDiffRef(returnRef);
            string st, resp;
            long existing = FindAnyStockQueueEntry(ActPurchaseReturnCostDiff, difRef, out st, out resp);
            if (existing > 0 && st != "CANCELLED") return existing;
            var payload = new Dictionary<string, object>
            {
                { "stockRef", difRef },
                { "returnRef", returnRef.Trim() },
                { "amount", amount },
                { "entryDate", AcctDate(entryDate, true) },
                { "supplierName", supplierName ?? "" },
                { "cnDocNo", cnDocNo ?? "" }
            };
            return InsertQueue("STOCK", 0, ActPurchaseReturnCostDiff, payload);
        }

        private async Task<string> ProcessPurchaseReturnCostDiff(Dictionary<string, object> p)
        {
            string difRef = p["stockRef"]?.ToString();
            string returnRef = p.ContainsKey("returnRef") ? p["returnRef"]?.ToString() : "";
            decimal amount = p.ContainsKey("amount") ? SafeDec(p["amount"]) : 0m;
            DateTime d = p.ContainsKey("entryDate") ? ParseAcctDate(p["entryDate"]?.ToString()) : DateTime.Today;
            string supplier = p.ContainsKey("supplierName") ? p["supplierName"]?.ToString() : "";
            string cnNo = p.ContainsKey("cnDocNo") ? p["cnDocNo"]?.ToString() : "";
            amount = VendorCnPolicy.R2(amount);
            if (Math.Abs(amount) < 0.005m) return "SKIPPED_ZERO";

            Guid inv = _mapper.GetAccountId("INVENTORY");
            Guid pr = _mapper.GetAccountId("PURCHASE_RETURNS");
            decimal a = Math.Abs(amount);
            string desc = $"ปรับต้นทุนคืนสินค้า {returnRef} (ใบลดหนี้ {cnNo}) — ราคาซื้อ ↔ ต้นทุนเฉลี่ย";
            var lines = amount > 0
                ? new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest { AccountId = inv, DebitAmount = a, CreditAmount = 0, Description = "ใบลดหนี้ตัดสินค้าคงเหลือที่ราคาซื้อ สูงกว่าต้นทุนเฉลี่ย" },
                    new JournalEntryLineRequest { AccountId = pr, DebitAmount = 0, CreditAmount = a, Description = "ผลต่างราคาคืนสินค้าสูงกว่าต้นทุนเฉลี่ย" }
                }
                : new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest { AccountId = pr, DebitAmount = a, CreditAmount = 0, Description = "ผลต่างราคาคืนสินค้าต่ำกว่าต้นทุนเฉลี่ย" },
                    new JournalEntryLineRequest { AccountId = inv, DebitAmount = 0, CreditAmount = a, Description = "ตัดสินค้าคงเหลือส่วนที่ใบลดหนี้ยังไม่ตัด (ต้นทุนเฉลี่ย)" }
                };
            var journal = new CreateJournalEntryRequest
            {
                EntryDate = d,
                JournalType = NexaaccJournalType.Purchase,
                Description = desc + (string.IsNullOrWhiteSpace(supplier) ? "" : " - " + supplier),
                Reference = difRef,
                Lines = lines
            };
            var result = await _apiClient.CreateJournalAsync(journal);
            Guid docId = RequireValidDocId(result?.data?.Id, $"PurchaseReturnCostDiff ref={difRef}");
            await SafePostJournalAsync(docId);
            return docId.ToString();
        }

        // ── VCRED-… / VDNPAY-… ─────────────────────────────────────────────────

        /// <summary>
        /// JE สองขาจากหน้าเว็บ (หักเครดิตผู้ขาย / จ่ายใบเพิ่มหนี้รอจ่าย). แต่ละขาระบุได้ 3 แบบ (ลำดับ): GUID บัญชี → mapping key → แหล่งเงิน (Paid_How).
        /// กันซ้ำด้วย jref (คิว + Reference ของ JE)
        /// </summary>
        public long EnqueueVendorBalanceJournal(string jref, decimal amount, DateTime entryDate, string description,
            string drKey, string drAccountId, string drPaidHow, string crKey, string crAccountId, string crPaidHow)
        {
            if (!_config.IsConfigured || string.IsNullOrWhiteSpace(jref)) return -1;
            amount = VendorCnPolicy.R2(amount);
            if (amount <= 0) return -1;
            long existing = FindPendingEntry("VENDOR", ActVendorBalanceJournal, "jref", jref);
            if (existing > 0) return existing;
            long completed = FindRecentCompletedEntry("VENDOR", ActVendorBalanceJournal, "jref", jref, 86400 * 365);
            if (completed > 0) return completed;
            var payload = new Dictionary<string, object>
            {
                { "jref", jref }, { "amount", amount }, { "entryDate", AcctDate(entryDate, true) }, { "description", description ?? "" },
                { "drKey", drKey ?? "" }, { "drAccountId", drAccountId ?? "" }, { "drPaidHow", drPaidHow ?? "" },
                { "crKey", crKey ?? "" }, { "crAccountId", crAccountId ?? "" }, { "crPaidHow", crPaidHow ?? "" }
            };
            return InsertQueue("VENDOR", 0, ActVendorBalanceJournal, payload);
        }

        private Guid ResolveLegAccount(Dictionary<string, object> p, string side)
        {
            string id = p.ContainsKey(side + "AccountId") ? p[side + "AccountId"]?.ToString() : null;
            Guid g;
            if (Guid.TryParse(id ?? "", out g) && g != Guid.Empty) return g;
            string key = p.ContainsKey(side + "Key") ? p[side + "Key"]?.ToString() : null;
            if (!string.IsNullOrWhiteSpace(key)) return _mapper.GetAccountId(key);
            string paidHow = p.ContainsKey(side + "PaidHow") ? p[side + "PaidHow"]?.ToString() : null;
            if (!string.IsNullOrWhiteSpace(paidHow))
            {
                string acc = LookupPaidHowAccountId(paidHow);
                if (Guid.TryParse(acc ?? "", out g) && g != Guid.Empty) return g;
                throw QueueErrors.Config("PAIDHOW_UNMAPPED", paidHow, $"แหล่งเงิน \"{paidHow}\" ยังไม่ผูกบัญชี NextAcc — ผูกที่ Account_Paid_How แล้ว Retry");
            }
            throw new ArgumentException($"VendorBalanceJournal: ไม่ได้ระบุบัญชีขา {side}");
        }

        private async Task<string> ProcessVendorBalanceJournal(Dictionary<string, object> p)
        {
            string jref = p["jref"]?.ToString();
            decimal amount = p.ContainsKey("amount") ? VendorCnPolicy.R2(SafeDec(p["amount"])) : 0m;
            if (amount <= 0) return "SKIPPED_ZERO";
            DateTime d = p.ContainsKey("entryDate") ? ParseAcctDate(p["entryDate"]?.ToString()) : DateTime.Today;
            string desc = p.ContainsKey("description") ? p["description"]?.ToString() : jref;
            Guid dr = ResolveLegAccount(p, "dr");
            Guid cr = ResolveLegAccount(p, "cr");
            var journal = new CreateJournalEntryRequest
            {
                EntryDate = d,
                JournalType = NexaaccJournalType.General,
                Description = desc,
                Reference = jref,
                Lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest { AccountId = dr, DebitAmount = amount, CreditAmount = 0, Description = desc },
                    new JournalEntryLineRequest { AccountId = cr, DebitAmount = 0, CreditAmount = amount, Description = desc }
                }
            };
            var result = await _apiClient.CreateJournalAsync(journal);
            Guid docId = RequireValidDocId(result?.data?.Id, $"VendorBalanceJournal ref={jref}");
            await SafePostJournalAsync(docId);
            return docId.ToString();
        }

        // ── เตือนรายวัน ────────────────────────────────────────────────────────

        private static DateTime _lastVendorCnReminder = DateTime.MinValue;

        /// <summary>เรียกจาก timer Global.asax — วันละครั้งหลัง 09:00: คืนสินค้าที่วางบิลแล้วเกิน Vendor_Cn_Reminder_Days ยังไม่ได้ใบลดหนี้ → Telegram</summary>
        public void RemindPendingVendorCnIfDue()
        {
            try
            {
                if (_lastVendorCnReminder.Date == DateTime.Today || DateTime.Now.Hour < 9) return;
                string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (_config.GetConfigValue("Vendor_Cn_Reminder_LastRun", "") == today) { _lastVendorCnReminder = DateTime.Today; return; }
                _lastVendorCnReminder = DateTime.Today;
                var svc = new VendorCnService(_connectionString);
                if (!svc.HasLinkTable()) return;
                try { _config.SetConfig("Vendor_Cn_Reminder_LastRun", today); } catch { }
                var due = svc.DueReminders();
                if (due.Count == 0) return;
                string msg = "🧾 คืนสินค้าให้ผู้ขายแล้ว ยังไม่ได้ใบลดหนี้ " + due.Count + " รายการ (เกิน " + svc.ReminderDays() + " วัน)\n"
                    + string.Join("\n", due.Take(15).Select(r => $"• {r.ReturnRef} · {r.SupplierName} · {r.PurchaseNet:N2} + VAT {r.VatAmount:N2} · คืน {r.CreatedAt:dd/MM}"))
                    + "\nขอใบลดหนี้จากผู้ขาย → สแกนที่หน้า OCR (ชนิดใบลดหนี้) แล้วผูกเลข PRT — ภาษีซื้อลดในเดือนที่ได้รับใบ (§82/10)";
                try { Notify.Send(Notify.Ev.AccQueueAlert, msg); } catch { }
                svc.MarkReminded(due.Select(r => r.ReturnRef));
                _code.Logs(_connectionString, "VendorCn", "เตือนคืนสินค้ารอใบลดหนี้ " + due.Count + " รายการ", "SYSTEM");
            }
            catch { }
        }
    }
}
