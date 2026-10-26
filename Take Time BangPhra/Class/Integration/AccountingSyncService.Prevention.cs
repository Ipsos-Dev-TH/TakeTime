using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Take_Time_BangPhra.Integration
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //  กันคิว NextAcc ค้าง/ล้ม (PHASE19_37 — CLAUDE.md ข้อ 21)
    //    • ชนิดปัญหารายแถว (Error_Class / Error_Rule / Fix_Url) + สถานะรอ (Wait_Since / Depends_On_Queue_Id)
    //    • รอแทนล้ม: ไม่นับ retry, ตื่นเมื่อรายการที่รอสำเร็จ, หมดเวลา Nexaacc_Wait_Max_Hours (48)
    //    • ปัญหาข้อมูล/ตั้งค่า/นโยบาย ไม่เผา retry 5 รอบ — ตั้งค่า กลับเข้าคิวเองเมื่อบันทึก mapping/แหล่งเงิน
    //    • ตรวจใบก่อนเข้าคิว/ก่อนส่ง (ReceiptPreflight) + สร้าง payload ใหม่จาก DB ตอนหยิบแถว (Retry ไม่ส่ง snapshot เก่า)
    //    • ตรวจรายคืนแบบไม่เรียก API (ยอด/มัดจำ/ใบไม่เข้าคิว/ใบค้างก่อนยื่น ภ.พ.30) + Telegram รายชนิด + ตัวชี้วัดในหน้าตรวจสุขภาพ
    //  ยังไม่รัน migration 37 หรือ Nexaacc_Preflight_Mode=OFF → ทุกเมธอด no-op/พฤติกรรมเดิม
    // ══════════════════════════════════════════════════════════════════════════════════════════
    public partial class AccountingSyncService
    {
        private static readonly CultureInfo InvP = CultureInfo.InvariantCulture;

        /// <summary>โหมดตัวตรวจที่มีผลจริง (ไม่มีคอลัมน์ PHASE19_37 = OFF)</summary>
        internal PreflightMode PreventionMode
        {
            get
            {
                try
                {
                    return ReceiptPreflight.QueueSchemaReady(_connectionString)
                        ? ReceiptPreflight.ParseMode(_config.GetConfigValue("Nexaacc_Preflight_Mode", "OFF"))
                        : PreflightMode.Off;
                }
                catch { return PreflightMode.Off; }
            }
        }

        /// <summary>ตรึงแถวเป็น "ไม่ retry อัตโนมัติ" ตาม Max_Retries ของแถวเอง (แถวที่เคยถูกขยายโควตาชั่วคราวเป็น 10 ก็ต้องตาย)</summary>
        private void PinDead(long queueId)
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Accounting_Sync_Queue SET Retry_Count = Max_Retries WHERE ID = @id AND Retry_Count < Max_Retries",
                    new Dictionary<string, object> { { "@id", queueId } });
            }
            catch { }
        }

        /// <summary>ตั้งสวิตช์ QueueErrors.Enabled ทุกรอบคิว (worker) — ตัวช่วยโยน exception จะคืนชนิดใหม่เมื่อเปิด</summary>
        internal void RefreshPreventionSwitch()
        {
            QueueErrors.Enabled = PreventionMode != PreflightMode.Off;
        }

        private int ConfigInt(string key, int def, int min, int max)
        {
            int v;
            return int.TryParse(_config.GetConfigValue(key, def.ToString(InvP)), out v) && v >= min && v <= max ? v : def;
        }

        // ── เขียน/ล้างชนิดปัญหาบนแถวคิว ─────────────────────────────────────────────
        private void MarkQueueIssue(long queueId, SyncIssueClass cls, string rule, string fixUrl, long? dependsOn = null, bool waiting = false)
        {
            if (!ReceiptPreflight.QueueSchemaReady(_connectionString)) return;
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Accounting_Sync_Queue
                      SET Error_Class = @c, Error_Rule = @r, Fix_Url = @u, Depends_On_Queue_Id = @d,
                          Wait_Since = CASE WHEN @w = 1 THEN CASE WHEN Error_Class = 'WAIT' AND Wait_Since IS NOT NULL THEN Wait_Since ELSE GETDATE() END
                                            ELSE NULL END
                      WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@id", queueId }, { "@c", QueueErrors.Code(cls) },
                        { "@r", string.IsNullOrEmpty(rule) ? (object)DBNull.Value : (rule.Length > 40 ? rule.Substring(0, 40) : rule) },
                        { "@u", string.IsNullOrEmpty(fixUrl) ? (object)DBNull.Value : (fixUrl.Length > 400 ? fixUrl.Substring(0, 400) : fixUrl) },
                        { "@d", dependsOn.HasValue && dependsOn.Value > 0 ? (object)dependsOn.Value : DBNull.Value },
                        { "@w", waiting ? 1 : 0 }
                    });
            }
            catch (Exception ex) { LogThrottled("mark-issue", "MarkQueueIssue: " + ex.Message); }
        }

        /// <summary>แก้ใบ/กด Retry → ข้อมูลใหม่ เริ่มนับเวลารอใหม่ (คงชนิดปัญหาเดิมไว้เป็นประวัติ/ตัวชี้วัด)</summary>
        private void ResetQueueWaitClock(long queueId)
        {
            if (!ReceiptPreflight.QueueSchemaReady(_connectionString)) return;
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Accounting_Sync_Queue SET Wait_Since = NULL, Depends_On_Queue_Id = NULL WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", queueId } });
            }
            catch { }
        }

        private string ReceiptNumberOfPayload(string payload)
        {
            var m = System.Text.RegularExpressions.Regex.Match(payload ?? "", "\"receiptNumber\"\\s*:\\s*\"([^\"]{1,60})\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>ปัญหาข้อมูล/ตั้งค่า/นโยบาย (IQueueIssue) → FAILED ไม่ retry อัตโนมัติ + ชนิด/กฎ/ลิงก์แก้</summary>
        private void HandleQueueIssue(long queueId, string actionType, string payload, IQueueIssue iq, string message)
        {
            string label = QueueErrors.BadgeTh(iq.IssueClass);
            string detail = $"Queue #{queueId} [{actionType}] {label}{(string.IsNullOrEmpty(iq.Rule) ? "" : " (" + iq.Rule + ")")}: {message}";
            if (iq.IssueClass == SyncIssueClass.Config)
                detail += "\n↻ แก้ mapping/แหล่งเงินแล้วกดบันทึก — รายการนี้จะกลับเข้าคิวเอง";
            else if (iq.IssueClass == SyncIssueClass.Data)
                detail += "\n↻ แก้ใบแล้วกดบันทึก — ระบบตรวจใหม่และเข้าคิวเอง (ไม่ต้องกด Retry)";
            UpdateQueueStatus(queueId, "FAILED", detail, null);
            IncrementRetry(queueId, _config.MaxRetries);
            PinDead(queueId);
            string rn = ReceiptNumberOfPayload(payload);
            string url = iq.FixUrl ?? ReceiptPreflight.BuildFixUrl(_connectionString, iq.FixKind, rn,
                (iq as QueueConfigException)?.MappingKey);
            MarkQueueIssue(queueId, iq.IssueClass, iq.Rule, url);
            _code.Logs(_connectionString, "AccountingSync", detail, "SYSTEM");
            NotifyImmediateIssue(queueId, iq.IssueClass, iq.Rule, rn, message);
        }

        /// <summary>รอรายการอื่น → PENDING ไม่นับ retry, เช็คใหม่ตามกำหนด · ตัวที่รอล้มถาวร/รอเกินกำหนด → FAILED พร้อมบอกเหตุ</summary>
        private void HandleQueueWait(long queueId, string actionType, QueueWaitException w)
        {
            int maxHours = ConfigInt("Nexaacc_Wait_Max_Hours", 48, 1, 24 * 30);
            DateTime? since = null;
            string depState = null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT q.Error_Class, q.Wait_Since,
                             d.ID AS DepId, d.Status AS DepStatus, d.Retry_Count AS DepRetry, d.Max_Retries AS DepMax,
                             d.Error_Class AS DepClass, d.Error_Rule AS DepRule
                      FROM Accounting_Sync_Queue q
                      LEFT JOIN Accounting_Sync_Queue d ON d.ID = @dep
                      WHERE q.ID = @id",
                    new Dictionary<string, object> { { "@id", queueId }, { "@dep", w.DependsOnQueueId ?? 0L } });
                if (dt != null && dt.Rows.Count > 0)
                {
                    var r = dt.Rows[0];
                    if (r["Wait_Since"] != DBNull.Value && string.Equals(r["Error_Class"]?.ToString(), "WAIT", StringComparison.Ordinal))
                        since = Convert.ToDateTime(r["Wait_Since"]);
                    if (r["DepId"] != DBNull.Value && r["DepStatus"]?.ToString() == "FAILED"
                        && Convert.ToInt32(r["DepRetry"]) >= Convert.ToInt32(r["DepMax"]))
                    {
                        string dc = r["DepClass"] == DBNull.Value ? "" : QueueErrors.BadgeTh(QueueErrors.Parse(r["DepClass"].ToString()) ?? SyncIssueClass.Unknown);
                        depState = $"รายการที่รอ #{r["DepId"]} ล้มถาวร{(dc.Length > 0 ? " (" + dc + (r["DepRule"] == DBNull.Value ? "" : " " + r["DepRule"]) + ")" : "")} — แก้ที่รายการนั้นก่อน";
                    }
                }
            }
            catch { }

            if (depState != null || (since.HasValue && (DateTime.Now - since.Value).TotalHours > maxHours))
            {
                string why = depState ?? $"รอเกิน {maxHours} ชม. (ตั้งแต่ {since.Value:dd/MM HH:mm})";
                string detail = $"Queue #{queueId} [{actionType}] รอไม่สำเร็จ: {why} · {w.WaitReason}";
                UpdateQueueStatus(queueId, "FAILED", detail, null);
                IncrementRetry(queueId, _config.MaxRetries);
                PinDead(queueId);
                MarkQueueIssue(queueId, SyncIssueClass.Wait, depState != null ? "WAIT_DEP_FAILED" : "WAIT_TIMEOUT", null, w.DependsOnQueueId, true);
                _code.Logs(_connectionString, "AccountingSync", detail, "SYSTEM");
                return;
            }

            UpdateQueueStatus(queueId, "PENDING",
                $"⏳ รอ (ไม่นับ retry): {w.WaitReason}" + (w.DependsOnQueueId.HasValue ? $" — จะลองทันทีเมื่อรายการ #{w.DependsOnQueueId} สำเร็จ" : ""), null);
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Accounting_Sync_Queue SET Next_Retry_Date = DATEADD(MINUTE, @m, GETDATE()) WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", queueId }, { "@m", w.RecheckMinutes } });
            }
            catch { }
            MarkQueueIssue(queueId, SyncIssueClass.Wait, "WAIT", null, w.DependsOnQueueId, true);
            LogThrottled("wait-" + queueId, $"Queue #{queueId} [{actionType}] รอ: {w.WaitReason}", 60);
        }

        /// <summary>รายการสำเร็จ → ปลุกแถวที่รอมันอยู่ (Depends_On) หรือแถวรอของการจองเดียวกัน ให้หยิบในรอบถัดไปทันที</summary>
        private void WakeWaitersOf(long completedQueueId)
        {
            if (!QueueErrors.Enabled || !ReceiptPreflight.QueueSchemaReady(_connectionString)) return;
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"DECLARE @eid INT = (SELECT TOP 1 Entity_ID FROM Accounting_Sync_Queue
                                           WHERE ID = @id AND Entity_Type IN ('RECEIPT','RESERVATION') AND Entity_ID > 0);
                      UPDATE Accounting_Sync_Queue SET Next_Retry_Date = NULL
                      WHERE Status = 'PENDING' AND Error_Class = 'WAIT' AND ID <> @id
                        AND (Depends_On_Queue_Id = @id
                             OR (@eid IS NOT NULL AND Entity_Type IN ('RECEIPT','RESERVATION') AND Entity_ID = @eid))",
                    new Dictionary<string, object> { { "@id", completedQueueId } });
            }
            catch { }
        }

        /// <summary>หลังแอดมินแก้ mapping/แหล่งเงิน/Sync บัญชี: แถวที่พักเพราะ "ตั้งค่า" กลับเข้าคิว (คืนจำนวน)</summary>
        public int RequeueConfigHeld()
        {
            if (!ReceiptPreflight.QueueSchemaReady(_connectionString)) return 0;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"UPDATE Accounting_Sync_Queue
                      SET Status = 'PENDING', Retry_Count = 0, Next_Retry_Date = NULL,
                          Error_Message = N'↻ เข้าคิวใหม่อัตโนมัติหลังแก้การตั้งค่า | ' + CAST(ISNULL(Error_Message, N'') AS NVARCHAR(MAX))
                      WHERE Status = 'FAILED' AND Error_Class = 'CONFIG';
                      SELECT @@ROWCOUNT AS N;", null);
                int n = dt != null && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["N"]) : 0;
                if (n > 0)
                    _code.Logs(_connectionString, "AccountingSync", $"RequeueConfigHeld: คืน {n} รายการที่พักเพราะการตั้งค่า เข้าคิวใหม่", "SYSTEM");
                return n;
            }
            catch { return 0; }
        }

        /// <summary>exception ทั่วไปที่ "ลองใหม่ได้" (เครือข่าย/หมดเวลา) — ขยายโควตา retry (Nexaacc_Transient_MaxRetries, default 10)</summary>
        private void EnsureTransientBudget(long queueId)
        {
            int t = ConfigInt("Nexaacc_Transient_MaxRetries", 10, 1, 50);
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Accounting_Sync_Queue SET Max_Retries = @t WHERE ID = @id AND Max_Retries < @t",
                    new Dictionary<string, object> { { "@id", queueId }, { "@t", t } });
            }
            catch { }
        }

        /// <summary>ปักธง "ห้ามสร้าง payload ใหม่" (NextAcc อาจสร้างเอกสารไปแล้ว — คีย์กันซ้ำต้องคงเดิม)</summary>
        private void PinPayloadNoRebuild(long queueId, string payload)
        {
            try
            {
                var p = _serializer.Deserialize<Dictionary<string, object>>(payload ?? "{}");
                if (p == null || p.ContainsKey("noRebuild")) return;
                p["noRebuild"] = true;
                _code.DatabaseInsertSafe(_connectionString, "UPDATE Accounting_Sync_Queue SET Payload = @p WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", queueId }, { "@p", _serializer.Serialize(p) } });
            }
            catch { }
        }

        // ── ตรวจใบก่อนเข้าคิว / ก่อนส่ง ────────────────────────────────────────────
        /// <summary>หลัง EnqueueReceipt ได้แถว: ตรวจใบ → ปัญหาที่ต้องพัก = FAILED (ไม่เผา retry) + ชนิด/กฎ/ลิงก์ · ปัญหาเตือน = เขียนข้อความไว้</summary>
        private void ApplyEnqueuePreflight(long queueId, string receiptNumber)
        {
            if (queueId <= 0 || string.IsNullOrEmpty(receiptNumber)) return;
            var mode = PreventionMode;
            if (mode == PreflightMode.Off) return;
            try
            {
                var pr = ReceiptPreflight.Validate(_connectionString, receiptNumber, PreflightContext.Enqueue);
                var hold = pr.QueueHoldIssue(mode);
                if (hold != null)
                {
                    string msg = "⛔ ตรวจก่อนส่งไม่ผ่าน — พักไว้ (ไม่ยิง NextAcc):\n" + pr.ToText();
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Accounting_Sync_Queue SET Status = 'FAILED', Retry_Count = Max_Retries, Error_Message = @m
                          WHERE ID = @id AND Status IN ('PENDING','FAILED')",
                        new Dictionary<string, object> { { "@id", queueId }, { "@m", msg } });
                    MarkQueueIssue(queueId, hold.Class, hold.Rule,
                        hold.FixUrl ?? ReceiptPreflight.BuildFixUrl(_connectionString, hold.FixKind, receiptNumber));
                    _code.Logs(_connectionString, "AccountingSync", $"EnqueueReceipt #{queueId} {receiptNumber}: {msg}", "SYSTEM");
                    NotifyImmediateIssue(queueId, hold.Class, hold.Rule, receiptNumber, pr.ToText(3));
                }
                else if (pr.Issues.Any(i => i.Severity >= PreflightSeverity.Warn))
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        "UPDATE Accounting_Sync_Queue SET Error_Message = @m WHERE ID = @id AND Status = 'PENDING'",
                        new Dictionary<string, object> { { "@id", queueId }, { "@m", "ℹ ตรวจก่อนส่ง (เตือน — ยังส่งตามปกติ):\n" + pr.ToText() } });
                }
            }
            catch (Exception ex) { LogThrottled("enq-preflight", "ApplyEnqueuePreflight: " + ex.Message); }
        }

        /// <summary>ด่านแรกของ ProcessReceiptDocument — กฎชุดเดียวกับหน้าเว็บ/ตอนเข้าคิว (โยนชนิดปัญหาให้ worker จัดการ)</summary>
        private void PreflightReceiptInWorker(string receiptNumber)
        {
            if (!QueueErrors.Enabled || string.IsNullOrEmpty(receiptNumber)) return;
            var mode = PreventionMode;
            PreflightResult pr;
            try { pr = ReceiptPreflight.Validate(_connectionString, receiptNumber, PreflightContext.Worker); }
            catch (Exception ex) { LogThrottled("wk-preflight", "PreflightReceiptInWorker: " + ex.Message); return; }
            var hold = pr.QueueHoldIssue(mode);
            if (hold == null)
            {
                if (pr.Issues.Any(i => i.Severity >= PreflightSeverity.Warn))
                    _code.Logs(_connectionString, "AccountingSync", $"ProcessReceiptDocument: receipt={receiptNumber} ตรวจก่อนส่ง (เตือน):\n{pr.ToText()}", "SYSTEM");
                return;
            }
            string msg = "ตรวจก่อนส่งไม่ผ่าน:\n" + pr.ToText();
            string url = hold.FixUrl ?? ReceiptPreflight.BuildFixUrl(_connectionString, hold.FixKind, receiptNumber);
            if (hold.Class == SyncIssueClass.Policy) throw new QueuePolicyException(hold.Rule, msg, url);
            if (hold.Class == SyncIssueClass.Config) throw new QueueConfigException(hold.Rule, null, msg, url);
            throw new QueueDataException(hold.Rule, msg, hold.FixKind, url);
        }

        // ── สร้าง payload ใหม่จาก DB ตอนหยิบแถว (Retry ไม่ส่ง snapshot เก่า) ──────────────
        /// <summary>
        /// คืน payload ที่ใช้ยิงจริง. gone=true = ใบถูกลบ/ยกเลิกและยังไม่เคยมีเอกสาร NextAcc → ผู้เรียกปิดแถวเป็น SKIPPED_RECEIPT_GONE.
        /// สร้างใหม่: ยอด/VAT/วันที่/ชนิดมัดจำ/แหล่งเงิน/เบอร์ผู้ซื้อ/ยอดหักมัดจำ (ชื่อลูกค้าคงตามผู้เรียก). ไม่สร้างใหม่: ปิดด้วย Nexaacc_Payload_Rebuild=0, แถวที่ปักธง noRebuild (อาจสร้างไปแล้ว), payload ขาย walk-in/ใบสรุป
        /// </summary>
        private string RebuildReceiptPayloadAtClaim(long queueId, string payload, out bool gone)
        {
            gone = false;
            if (!QueueErrors.Enabled || _config.GetConfigValue("Nexaacc_Payload_Rebuild", "1") == "0") return payload;
            Dictionary<string, object> old;
            try { old = _serializer.Deserialize<Dictionary<string, object>>(payload ?? "{}"); }
            catch { return payload; }
            if (old == null || old.ContainsKey("noRebuild") || IsWalkInSalesDocPayload(old)) return payload;
            string rn = old.ContainsKey("receiptNumber") ? old["receiptNumber"]?.ToString() : null;
            if (string.IsNullOrWhiteSpace(rn)) return payload;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 R.*, C.MobilePhone AS Buyer_Phone
                      FROM Account_Receipt R LEFT JOIN Customer C ON C.ID = R.Customer_ID WHERE R.ID = @id",
                    new Dictionary<string, object> { { "@id", rn } });
                bool missing = dt == null || dt.Rows.Count == 0;
                string st = missing ? "" : (dt.Rows[0]["Status"]?.ToString() ?? "");
                // ใบถูกยกเลิก/ลบก่อนส่ง และยังไม่เคยมีเอกสาร → ไม่มีอะไรให้สร้าง (ไม่พบแถวเลย = นับเฉพาะเลขใบเสร็จมาตรฐาน REC…
                // — อ้างอิงแบบอื่นอาจไม่ใช่ Account_Receipt จึงยิงตาม payload เดิม)
                bool cancelled = st == "Cancel" || st == "Delete" || st == "ยกเลิก";
                bool missingStd = missing && System.Text.RegularExpressions.Regex.IsMatch(rn, "^REC[0-9]{6,}$");
                if (cancelled || missingStd)
                {
                    if (!ReceiptHasLiveNextAccDoc(rn)) gone = true;
                    return payload;
                }
                if (missing) return payload;
                var r = dt.Rows[0];
                var np = new Dictionary<string, object>(old);
                np["totalAmount"] = ReceiptPreflight.Dec(r["Total_Amount"]);
                np["vatAmount"] = ReceiptPreflight.Dec(r["Vat"]);
                if (r["Created_Date"] is DateTime) np["receiptDate"] = AcctDate((DateTime)r["Created_Date"]);
                np["isDeposit"] = ReceiptPreflight.Bool(r["IsDeposit"]);
                int rid = ReceiptPreflight.Int(r["Reservation_ID"]);
                if (rid > 0) np["reservationId"] = rid;
                string paid = r["Paid_Type"]?.ToString();
                if (!string.IsNullOrWhiteSpace(paid))
                {
                    np["paymentMethod"] = paid;
                    string acc = LookupPaidHowAccountId(paid);
                    if (!string.IsNullOrEmpty(acc)) np["paymentAccountId"] = acc;
                }
                string bp = r.Table.Columns.Contains("Buyer_Phone") ? (r["Buyer_Phone"]?.ToString() ?? "").Trim() : "";
                if (bp.Length > 0) np["customerPhone"] = bp;
                if (r.Table.Columns.Contains("Deposit_Applied_Amount"))
                {
                    decimal da = ReceiptPreflight.Dec(r["Deposit_Applied_Amount"]);
                    if (da > 0.005m) np["depositApplied"] = da;
                }

                var diffs = new List<string>();
                foreach (var k in np.Keys)
                {
                    string a = old.ContainsKey(k) && old[k] != null ? Convert.ToString(old[k], InvP) : "";
                    string b = np[k] == null ? "" : Convert.ToString(np[k], InvP);
                    decimal da2, db2;
                    bool same = decimal.TryParse(a, NumberStyles.Any, InvP, out da2) && decimal.TryParse(b, NumberStyles.Any, InvP, out db2)
                        ? da2 == db2 : string.Equals(a.Trim(), b.Trim(), StringComparison.Ordinal);
                    if (!same) diffs.Add($"{k} {a}→{b}");
                }
                if (diffs.Count == 0) return payload;
                string json = _serializer.Serialize(np);
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Accounting_Sync_Queue SET Payload = @p, Payload_Rebuilt_At = GETDATE() WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", queueId }, { "@p", json } });
                _code.Logs(_connectionString, "AccountingSync",
                    $"Queue #{queueId} receipt={rn}: payload rebuilt จาก DB ปัจจุบัน — {string.Join(", ", diffs)}", "SYSTEM");
                return json;
            }
            catch (Exception ex)
            {
                LogThrottled("rebuild", "RebuildReceiptPayloadAtClaim: " + ex.Message);
                return payload;
            }
        }

        /// <summary>แถว CREATE ใหม่ของใบเดียวกันถูกใส่ → แถวเก่าที่ยังไม่ส่ง (PENDING/FAILED/AWAITING_SLIP) = SUPERSEDED</summary>
        private void SupersedeOlderReceiptCreates(long newQueueId, string receiptNumber)
        {
            if (!QueueErrors.Enabled || newQueueId <= 0 || string.IsNullOrEmpty(receiptNumber)) return;
            try
            {
                string esc = receiptNumber.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
                int n = _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Accounting_Sync_Queue
                      SET Status = 'SUPERSEDED', Error_Message = N'ถูกแทนด้วยรายการใหม่ #' + CAST(@nid AS NVARCHAR(20))
                      WHERE Action_Type = 'CREATE_RECEIPT_DOCUMENT' AND ID < @nid
                        AND Status IN ('PENDING','FAILED','AWAITING_SLIP') AND Payload LIKE @p",
                    new Dictionary<string, object> { { "@nid", newQueueId }, { "@p", "%\"receiptNumber\":\"" + esc + "\"%" } });
                if (n > 0)
                    _code.Logs(_connectionString, "AccountingSync", $"EnqueueReceipt {receiptNumber}: แถวเก่าที่ยังไม่ส่ง {n} แถว → SUPERSEDED (แทนด้วย #{newQueueId})", "SYSTEM");
            }
            catch { }
        }

        /// <summary>ใบนี้มีเอกสารบน NextAcc ที่ยังใช้งาน: Account_Receipt.Nexaacc_Doc_Id หรือ CREATE สำเร็จ (ไม่ใช่ SKIPPED_*) ที่ไม่มี VOID สำเร็จตามหลัง</summary>
        private bool ReceiptHasLiveNextAccDoc(string receiptNumber)
        {
            if (string.IsNullOrWhiteSpace(receiptNumber)) return false;
            try
            {
                var h = _code.DatabaseQuerySafe(_connectionString, "SELECT TOP 1 * FROM Account_Receipt WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", receiptNumber.Trim() } });
                if (h != null && h.Rows.Count > 0 && ReceiptPreflight.Str(ReceiptPreflight.Col(h.Rows[0], "Nexaacc_Doc_Id")).Length > 0) return true;
                string esc = receiptNumber.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
                var q = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 c.ID FROM Accounting_Sync_Queue c
                      WHERE c.Action_Type = 'CREATE_RECEIPT_DOCUMENT' AND c.Status = 'COMPLETED'
                        AND ISNULL(c.Nexaacc_Response_Id, '') <> '' AND c.Nexaacc_Response_Id NOT LIKE 'SKIPPED%' AND c.Payload LIKE @p
                        AND NOT EXISTS (SELECT 1 FROM Accounting_Sync_Queue v WHERE v.Action_Type = 'VOID_RECEIPT'
                                        AND v.Status = 'COMPLETED' AND v.ID > c.ID AND v.Payload LIKE @p)",
                    new Dictionary<string, object> { { "@p", "%\"receiptNumber\":\"" + esc + "\"%" } });
                return q != null && q.Rows.Count > 0;
            }
            catch { return true; }   // อ่านไม่ได้ = ถือว่ามี (ปลอดภัยกว่า: ใช้เส้นเดิม/ไม่ข้าม)
        }

        /// <summary>แถว CREATE ของใบมัดจำ (การจองนี้) ที่ยังไม่ส่ง — ใช้เป็น Depends_On ของแถวที่รอมัดจำ</summary>
        private long? FindUnsentDepositCreate(int reservationId)
        {
            if (reservationId <= 0) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 ID FROM Accounting_Sync_Queue
                      WHERE Action_Type = 'CREATE_RECEIPT_DOCUMENT' AND Entity_Type = 'RECEIPT' AND Entity_ID = @r
                        AND Status IN ('PENDING','PROCESSING','FAILED','AWAITING_SLIP') AND Payload LIKE N'%""isDeposit"":true%'
                      ORDER BY ID",
                    new Dictionary<string, object> { { "@r", reservationId } });
                if (dt != null && dt.Rows.Count > 0) return Convert.ToInt64(dt.Rows[0][0]);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// จุดเดียวหลัง "แก้ใบเสร็จ" (หน้า Account/Receipt): ยังไม่มีเอกสาร NextAcc → ไม่ต้อง VOID (เดิมได้ VOID SKIPPED_NO_DOCUMENT ทุกครั้ง)
        /// แค่รีเฟรชแถว CREATE ที่ค้าง/เข้าคิวใหม่ + เปลี่ยนเลขใบ = แถวเลขเดิมที่ยังไม่ส่งถูกแทน · มีเอกสารแล้ว = เส้นเดิม (VOID → CREATE เลขเดิม)
        /// คืน REFRESH | VOID_RECREATE | LEGACY
        /// </summary>
        public string OnReceiptEdited(string receiptNumber, string originalNumber, Func<long> enqueueCreate, out long createQueueId)
        {
            createQueueId = -1;
            string orig = string.IsNullOrWhiteSpace(originalNumber) ? receiptNumber : originalNumber.Trim();
            bool docExists = ReceiptHasLiveNextAccDoc(orig);
            RefreshPreventionSwitch();
            if (!QueueErrors.Enabled || docExists)
            {
                EnqueueVoidReceipt(orig);
                // ยอดหักมัดจำ = บรรทัดหักมัดจำที่เหลือ — ต้องหลัง VOID (VOID จับยอดที่ลงไว้จริง) (#149366)
                RealignDepositAppliedToLines(receiptNumber);
                createQueueId = enqueueCreate != null ? enqueueCreate() : -1;
                return docExists ? "VOID_RECREATE" : "LEGACY";
            }
            if (!string.Equals(orig, receiptNumber, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string esc = orig.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Accounting_Sync_Queue SET Status = 'SUPERSEDED',
                                 Error_Message = N'ใบถูกเปลี่ยนเลขเป็น ' + @nn + N' ก่อนส่ง NextAcc'
                          WHERE Action_Type = 'CREATE_RECEIPT_DOCUMENT' AND Status IN ('PENDING','FAILED','AWAITING_SLIP') AND Payload LIKE @p",
                        new Dictionary<string, object> { { "@nn", receiptNumber }, { "@p", "%\"receiptNumber\":\"" + esc + "\"%" } });
                }
                catch { }
            }
            RealignDepositAppliedToLines(receiptNumber);
            createQueueId = enqueueCreate != null ? enqueueCreate() : -1;
            return "REFRESH";
        }

        // ── Telegram รายชนิด ─────────────────────────────────────────────────────
        private static readonly object _immLock = new object();
        private static readonly HashSet<long> _immSent = new HashSet<long>();
        private static DateTime _immLast = DateTime.MinValue;

        /// <summary>แจ้งทันที (ไม่รอรอบ 6 ชม.) เมื่อเงิน/ภาษีเสี่ยง: นโยบาย (ต้องใบลดหนี้) และข้อมูลผิดของใบเสร็จ — กันซ้ำรายแถว, อย่างมาก 1 ข้อความ/10 นาที</summary>
        private void NotifyImmediateIssue(long queueId, SyncIssueClass cls, string rule, string receiptNumber, string message)
        {
            try
            {
                if (!QueueErrors.Enabled || !_config.QueueAlertEnabled) return;
                if (cls != SyncIssueClass.Policy && !(cls == SyncIssueClass.Data && !string.IsNullOrEmpty(receiptNumber))) return;
                lock (_immLock)
                {
                    if (_immSent.Contains(queueId) || (DateTime.Now - _immLast).TotalMinutes < 10) return;
                    if (_immSent.Count > 5000) _immSent.Clear();
                    _immSent.Add(queueId);
                    _immLast = DateTime.Now;
                }
                string icon = cls == SyncIssueClass.Policy ? "🟥" : "🟧";
                string m = message ?? "";
                if (m.Length > 600) m = m.Substring(0, 600) + "…";
                var sb = new StringBuilder();
                sb.AppendLine($"<b>{icon} คิวบัญชี: {QueueErrors.BadgeTh(cls)}</b>");
                sb.AppendLine($"คิว #{queueId}{(string.IsNullOrEmpty(receiptNumber) ? "" : " · ใบ " + System.Net.WebUtility.HtmlEncode(receiptNumber))}{(string.IsNullOrEmpty(rule) ? "" : " · " + System.Net.WebUtility.HtmlEncode(rule))}");
                sb.AppendLine(System.Net.WebUtility.HtmlEncode(m));
                sb.Append("<i>แก้ที่: Admin → ตั้งค่า → NextAcc → คิว (ปุ่ม \"แก้ที่นี่\")</i>");
                string text = sb.ToString();
                // อาจถูกเรียกจากคำขอหน้าเว็บ (EnqueueReceipt) — อย่าให้การส่ง Telegram หน่วงการบันทึก
                Task.Run(() => { try { Notify.Send(Notify.Ev.AccQueueAlert, text); } catch { } });
            }
            catch { }
        }

        /// <summary>บรรทัดสรุปรายชนิดสำหรับข้อความเตือนรอบ 6 ชม. (null = ไม่มี/ยังไม่รัน migration)</summary>
        private string QueueClassDigest()
        {
            if (!ReceiptPreflight.QueueSchemaReady(_connectionString)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT Error_Class, COUNT(*) AS N, MIN(ID) AS FirstId,
                             MIN(CASE WHEN Error_Rule IS NULL THEN NULL ELSE Error_Rule END) AS AnyRule
                      FROM Accounting_Sync_Queue
                      WHERE Error_Class IS NOT NULL
                        AND ((Status = 'FAILED' AND Retry_Count >= Max_Retries) OR (Status = 'PENDING' AND Error_Class = 'WAIT'))
                      GROUP BY Error_Class", null);
                if (dt == null || dt.Rows.Count == 0) return null;
                var parts = new List<string>();
                var icons = new Dictionary<string, string>
                { { "DATA", "🟧" }, { "CONFIG", "🟪" }, { "WAIT", "🟦" }, { "POLICY", "🟥" }, { "DUPLICATE", "⬛" }, { "TRANSIENT", "⬜" }, { "UNKNOWN", "❔" } };
                foreach (DataRow r in dt.Rows)
                {
                    string c = r["Error_Class"]?.ToString() ?? "UNKNOWN";
                    string ic;
                    icons.TryGetValue(c, out ic);
                    parts.Add($"{ic} {QueueErrors.BadgeTh(QueueErrors.Parse(c) ?? SyncIssueClass.Unknown)} {r["N"]}"
                              + (r["AnyRule"] == DBNull.Value ? "" : $" (เช่น #{r["FirstId"]} {r["AnyRule"]})"));
                }
                return string.Join(" · ", parts);
            }
            catch { return null; }
        }

        // ── ตรวจซ้ำรายแถว (ปุ่มหน้าคิว) ──────────────────────────────────────────────
        /// <summary>ตรวจใบของแถวคิวใหม่ (ไม่ยิง NextAcc): ผ่าน → คืนเข้าคิว · ไม่ผ่าน → บอกเหตุ</summary>
        public (bool requeued, string message) RecheckQueueItem(long queueId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Action_Type, Status, Payload FROM Accounting_Sync_Queue WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", queueId } });
                if (dt == null || dt.Rows.Count == 0) return (false, "ไม่พบรายการคิว #" + queueId);
                string action = dt.Rows[0]["Action_Type"]?.ToString();
                string st = dt.Rows[0]["Status"]?.ToString();
                string rn = ReceiptNumberOfPayload(dt.Rows[0]["Payload"]?.ToString());
                if (st == "PROCESSING" || st == "COMPLETED" || st == "SUPERSEDED" || st == "CANCELLED")
                    return (false, $"รายการอยู่ในสถานะ {st} — ไม่ต้องตรวจซ้ำ");
                if (action != "CREATE_RECEIPT_DOCUMENT" || string.IsNullOrEmpty(rn))
                {
                    bool ok = RetryItem(queueId);
                    return (ok, ok ? "รายการนี้ไม่มีตัวตรวจล่วงหน้า — คืนเข้าคิวแล้ว" : "คืนเข้าคิวไม่ได้");
                }
                var mode = PreventionMode == PreflightMode.Off ? PreflightMode.Block : PreventionMode;
                var pr = ReceiptPreflight.Validate(_connectionString, rn, PreflightContext.Worker);
                var hold = pr.QueueHoldIssue(mode);
                if (hold != null)
                    return (false, "ยังไม่ผ่าน — ยังไม่ส่ง:\n" + pr.ToText());
                bool reset = RetryItem(queueId);
                return (reset, (reset ? "✅ ตรวจผ่าน — คืนเข้าคิวแล้ว (ส่งในรอบถัดไป)" : "ตรวจผ่าน แต่คืนเข้าคิวไม่ได้")
                               + (pr.HasAny ? "\n" + pr.ToText() : ""));
            }
            catch (Exception ex) { return (false, "ตรวจไม่สำเร็จ: " + ex.Message); }
        }

        /// <summary>สแกนใบเสร็จย้อนหลังด้วยกฎ (ไม่ยิง API) — ใช้ปรับเกณฑ์ก่อนเปลี่ยน WARN → BLOCK</summary>
        public (int scanned, Dictionary<string, int> byRule, List<string> samples) PreflightScan(int days, int max = 500)
        {
            var byRule = new Dictionary<string, int>(StringComparer.Ordinal);
            var samples = new List<string>();
            int n = 0;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP (@max) ID FROM Account_Receipt
                      WHERE Created_Date >= DATEADD(DAY, -@d, CAST(GETDATE() AS DATE)) AND (Status = 'Normal' OR Status IS NULL)
                      ORDER BY Created_Date DESC",
                    new Dictionary<string, object> { { "@max", max }, { "@d", days } });
                if (dt == null) return (0, byRule, samples);
                foreach (DataRow r in dt.Rows)
                {
                    string rn = r[0]?.ToString();
                    n++;
                    var pr = ReceiptPreflight.Validate(_connectionString, rn, PreflightContext.Scan);
                    foreach (var i in pr.Issues.Where(x => x.Severity >= PreflightSeverity.Warn))
                    {
                        string key = i.Rule + (i.Severity == PreflightSeverity.Block ? " ⛔" : " ⚠");
                        int c; byRule.TryGetValue(key, out c); byRule[key] = c + 1;
                        if (samples.Count < 30) samples.Add(rn + " " + i);
                    }
                }
            }
            catch (Exception ex) { samples.Add("สแกนไม่สำเร็จ: " + ex.Message); }
            return (n, byRule, samples);
        }

        // ── cache งวดบัญชี/เดือนภาษีจาก NextAcc (R12) ─────────────────────────────────
        private static DateTime _periodRefreshAt = DateTime.MinValue;

        /// <summary>รายชั่วโมง (จากรอบคิว): GET fiscal-periods (AccountingController.cs:335) + tax?taxType=VAT (TaxController.cs:53)
        /// → Accounting_Nexaacc_Period_Status. ยื่นแล้ว = Status ≠ Draft หรือ filingPeriodLocked (เกณฑ์เดียวกับ IntegrationService.cs:3500-3503)</summary>
        private async Task RefreshPeriodStatusIfDueAsync()
        {
            if (!QueueErrors.Enabled || !_config.CanUseCompanyEndpoints || !ReceiptPreflight.PeriodCacheReady(_connectionString)) return;
            if ((DateTime.Now - _periodRefreshAt).TotalMinutes < 60) return;
            _periodRefreshAt = DateTime.Now;
            try
            {
                string cp = $"/api/companies/{_config.CompanyId}";
                var months = new Dictionary<string, Tuple<int, int, string, bool?>>();
                string fp = await _apiClient.GetRawAsync(cp + "/accounting/fiscal-periods").ConfigureAwait(false);
                foreach (var it in DataArray(fp))
                {
                    int y = ReceiptPreflight.Int(Get(it, "year")), m = ReceiptPreflight.Int(Get(it, "month"));
                    if (y < 2000 || m < 1 || m > 12) continue;
                    months[y + "-" + m] = Tuple.Create(y, m, ReceiptPreflight.Str(Get(it, "status")), (bool?)null);
                }
                int thisYear = DateTime.Today.Year;
                foreach (int yy in new[] { thisYear - 1, thisYear })
                {
                    string tx = await _apiClient.GetRawAsync(cp + "/tax?taxType=VAT&year=" + yy).ConfigureAwait(false);
                    foreach (var it in DataArray(tx))
                    {
                        int y = ReceiptPreflight.Int(Get(it, "year")), m = ReceiptPreflight.Int(Get(it, "month"));
                        if (y < 2000 || m < 1 || m > 12) continue;
                        string st = ReceiptPreflight.Str(Get(it, "status"));
                        bool filed = (st.Length > 0 && !st.Equals("Draft", StringComparison.OrdinalIgnoreCase) && st != "0")
                                     || ReceiptPreflight.Bool(Get(it, "filingPeriodLocked"));
                        Tuple<int, int, string, bool?> cur;
                        string key = y + "-" + m;
                        months.TryGetValue(key, out cur);
                        bool prev = cur != null && cur.Item4 == true;
                        months[key] = Tuple.Create(y, m, cur?.Item3, (bool?)(prev || filed));
                    }
                }
                foreach (var t in months.Values)
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"IF EXISTS (SELECT 1 FROM Accounting_Nexaacc_Period_Status WHERE [Year] = @y AND [Month] = @m)
                              UPDATE Accounting_Nexaacc_Period_Status
                                 SET Period_Status = COALESCE(@s, Period_Status), Vat_Filed = ISNULL(@f, 0), Refreshed_At = GETDATE()
                               WHERE [Year] = @y AND [Month] = @m
                          ELSE
                              INSERT INTO Accounting_Nexaacc_Period_Status ([Year], [Month], Period_Status, Vat_Filed, Refreshed_At)
                              VALUES (@y, @m, @s, ISNULL(@f, 0), GETDATE())",
                        new Dictionary<string, object>
                        {
                            { "@y", t.Item1 }, { "@m", t.Item2 },
                            { "@s", string.IsNullOrEmpty(t.Item3) ? (object)DBNull.Value : t.Item3 },
                            { "@f", t.Item4.HasValue ? (object)t.Item4.Value : DBNull.Value }
                        });
                }
            }
            catch (Exception ex)
            {
                LogThrottled("period-cache", "RefreshPeriodStatus: อ่านงวด/เดือนภาษีจาก NextAcc ไม่ได้ (" + ex.Message + ") — R12 ใช้เกณฑ์วันกำหนดยื่นแทน", 360);
            }
        }

        private List<Dictionary<string, object>> DataArray(string json)
        {
            var list = new List<Dictionary<string, object>>();
            if (string.IsNullOrWhiteSpace(json)) return list;
            try
            {
                object root = _serializer.DeserializeObject(json);
                object data = root;
                var rd = root as Dictionary<string, object>;
                if (rd != null) data = Get(rd, "data");
                var arr = data as object[];
                if (arr == null)
                {
                    var dd = data as Dictionary<string, object>;
                    if (dd != null) arr = Get(dd, "items") as object[];
                }
                if (arr != null)
                    foreach (var o in arr) { var d = o as Dictionary<string, object>; if (d != null) list.Add(d); }
            }
            catch { }
            return list;
        }

        private static object Get(Dictionary<string, object> d, string key)
        {
            if (d == null) return null;
            foreach (var kv in d) if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }

        // ── ตรวจรายคืนแบบไม่เรียก API ─────────────────────────────────────────────
        private static DateTime _lastIntegrityRun = DateTime.MinValue;
        private static volatile bool _integrityRunning;

        /// <summary>เรียกจาก timer Global.asax ทุก 30 วิ — วันละครั้งหลัง Nexaacc_Integrity_Hour (default 2) · โหมด OFF/ไม่มีตาราง = no-op</summary>
        public void RunLocalIntegrityScanIfDue()
        {
            try
            {
                if (_integrityRunning || _lastIntegrityRun.Date == DateTime.Today) return;
                if (PreventionMode == PreflightMode.Off) return;
                if (DateTime.Now.Hour < ConfigInt("Nexaacc_Integrity_Hour", 2, 0, 23)) return;
                string last = _config.GetConfigValue("Nexaacc_Integrity_LastRun", "");
                if (last == DateTime.Today.ToString("yyyy-MM-dd", InvP)) { _lastIntegrityRun = DateTime.Today; return; }
                _integrityRunning = true;
                _lastIntegrityRun = DateTime.Today;
                try { _config.SetConfig("Nexaacc_Integrity_LastRun", DateTime.Today.ToString("yyyy-MM-dd", InvP)); } catch { }
                string conn = _connectionString;
                Task.Run(() =>
                {
                    try { new AccountingSyncService(conn).RunLocalIntegrityScan(); }
                    catch (Exception ex) { new code().Logs(conn, "AccountingSync", "Integrity scan ล้มเหลว: " + ex.Message, "SYSTEM"); }
                    finally { _integrityRunning = false; }
                });
            }
            catch { _integrityRunning = false; }
        }

        /// <summary>
        /// ตรวจรายคืน (ไม่ยิง NextAcc) → Accounting_Reconcile_Finding คีย์ขึ้นต้น LOC: (ปิดเองเมื่อการตรวจครั้งถัดไปไม่พบแล้ว):
        /// PREFLIGHT_BLOCK · NOT_ENQUEUED · ZERO_TOTAL · DEPOSIT_LEDGER · BALANCE_VS_RECEIPTS · QUEUE_STUCK · PRE_FILING_GAP
        /// + ตรวจแถวที่พักเพราะ "ข้อมูล" ซ้ำ — ผ่านแล้ว (แก้ผ่านเส้นที่ไม่เข้าคิว) คืนเข้าคิว
        /// </summary>
        public string RunLocalIntegrityScan()
        {
            int days = ConfigInt("Nexaacc_Integrity_Days", 14, 1, 120);
            int max = ConfigInt("Nexaacc_Integrity_Max", 400, 10, 5000);
            int filingDay = ConfigInt("Nexaacc_Vat_Filing_Day", 23, 1, 28);
            DateTime scanStart = DateTime.Now;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            bool tableOk = true;
            Action<int, string, string, string, decimal?, string> save = (resId, key, type, receipt, amount, detail) =>
            {
                int c; counts.TryGetValue(type, out c); counts[type] = c + 1;
                if (!tableOk) return;
                try
                {
                    string d = detail ?? "";
                    if (d.Length > 1000) d = d.Substring(0, 1000);
                    string k = "LOC:" + key;
                    if (k.Length > 200) k = k.Substring(0, 200);
                    _code.DatabaseInsertSafe(_connectionString,
                        @"IF EXISTS (SELECT 1 FROM Accounting_Reconcile_Finding WHERE Reservation_ID = @r AND Finding_Key = @k AND Resolved_At IS NULL)
                              UPDATE Accounting_Reconcile_Finding SET Last_Seen = GETDATE(), Finding_Type = @t, Receipt_Number = @rn, Amount = @a, Detail = @d
                               WHERE Reservation_ID = @r AND Finding_Key = @k AND Resolved_At IS NULL
                          ELSE
                              INSERT INTO Accounting_Reconcile_Finding (Reservation_ID, Finding_Key, Finding_Type, Receipt_Number, Amount, Detail, First_Seen, Last_Seen)
                              VALUES (@r, @k, @t, @rn, @a, @d, GETDATE(), GETDATE())",
                        new Dictionary<string, object>
                        {
                            { "@r", resId }, { "@k", k }, { "@t", type }, { "@rn", (object)receipt ?? DBNull.Value },
                            { "@a", amount.HasValue ? (object)amount.Value : DBNull.Value }, { "@d", d }
                        });
                }
                catch { tableOk = false; }
            };
            bool docCol = false;
            try
            {
                var cc = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT CASE WHEN COL_LENGTH('dbo.Account_Receipt','Nexaacc_Doc_Id') IS NOT NULL THEN 1 ELSE 0 END", null);
                docCol = cc != null && cc.Rows.Count > 0 && Convert.ToInt32(cc.Rows[0][0]) == 1;
            }
            catch { }
            bool syncOn = _config.IsConfigured && _config.Enabled && !_config.IsReceiptLocal;
            var resIds = new HashSet<int>();

            // 1) ใบเสร็จช่วงนี้ที่ไม่ผ่านกฎ (รวมที่ส่งไปแล้ว = เอกสาร NextAcc อาจลงผิด)
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP (@max) ID, ISNULL(Reservation_ID, 0) AS R, ISNULL(Total_Amount, 0) AS T, IsDeposit FROM Account_Receipt
                      WHERE Created_Date >= DATEADD(DAY, -@d, CAST(GETDATE() AS DATE)) AND (Status = 'Normal' OR Status IS NULL)
                      ORDER BY Created_Date DESC",
                    new Dictionary<string, object> { { "@max", max }, { "@d", days } });
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                    {
                        string rn = r["ID"]?.ToString();
                        int rid = ReceiptPreflight.Int(r["R"]);
                        if (rid > 0) resIds.Add(rid);
                        var pr = ReceiptPreflight.Validate(_connectionString, rn, PreflightContext.Scan);
                        var blocks = pr.Issues.Where(i => i.Severity == PreflightSeverity.Block && i.Class == SyncIssueClass.Data).ToList();
                        if (blocks.Count > 0)
                            save(rid, "PF:" + rn, "PREFLIGHT_BLOCK", rn, ReceiptPreflight.Dec(r["T"]), string.Join("\n", blocks.Take(4).Select(b => b.ToString())));
                    }
            }
            catch (Exception ex) { LogThrottled("integ-1", "Integrity scan (preflight): " + ex.Message); }

            // 2) ใบที่ไม่เคยเข้าคิวเลย / ใบยอด ≤ 0 ที่การจองมีมัดจำแต่ไม่มีรายการตัดมัดจำ
            if (syncOn)
            {
                try
                {
                    var dt = _code.DatabaseQuerySafe(_connectionString,
                        @"SELECT TOP (@max) R.ID, ISNULL(R.Reservation_ID, 0) AS RID, ISNULL(R.Total_Amount, 0) AS T FROM Account_Receipt R
                          WHERE R.Created_Date >= DATEADD(DAY, -@d, CAST(GETDATE() AS DATE)) AND (R.Status = 'Normal' OR R.Status IS NULL)
                            AND ISNULL(R.Total_Amount, 0) > 0" + (docCol ? " AND R.Nexaacc_Doc_Id IS NULL" : "") + @"
                            AND R.Created_Date < DATEADD(HOUR, -2, GETDATE())
                            AND NOT EXISTS (SELECT 1 FROM Accounting_Sync_Queue q
                                            WHERE q.Action_Type IN ('CREATE_RECEIPT_DOCUMENT','CREATE_OTA_SALES_DOCUMENT')
                                              AND q.Payload LIKE N'%""receiptNumber"":""' + R.ID + N'""%')
                          ORDER BY R.Created_Date DESC",
                        new Dictionary<string, object> { { "@max", max }, { "@d", days } });
                    if (dt != null)
                        foreach (DataRow r in dt.Rows)
                            save(ReceiptPreflight.Int(r["RID"]), "NQ:" + r["ID"], "NOT_ENQUEUED", r["ID"]?.ToString(), ReceiptPreflight.Dec(r["T"]),
                                "ใบเสร็จไม่เคยเข้าคิวส่ง NextAcc (บันทึกตอนปิดการเชื่อม/ยอดผิด?) — เปิดหน้าเอกสารแล้วกด Sync ใบนี้");
                    var z = _code.DatabaseQuerySafe(_connectionString,
                        @"SELECT TOP (@max) R.ID, ISNULL(R.Reservation_ID, 0) AS RID FROM Account_Receipt R
                          WHERE R.Created_Date >= DATEADD(DAY, -@d, CAST(GETDATE() AS DATE)) AND (R.Status = 'Normal' OR R.Status IS NULL)
                            AND ISNULL(R.Total_Amount, 0) <= 0 AND ISNULL(R.IsDeposit, 0) = 0 AND ISNULL(R.Reservation_ID, 0) > 0
                            AND EXISTS (SELECT 1 FROM Account_Receipt D WHERE D.Reservation_ID = R.Reservation_ID AND D.IsDeposit = 1
                                        AND (D.Status = 'Normal' OR D.Status IS NULL))
                            AND NOT EXISTS (SELECT 1 FROM Accounting_Sync_Queue q WHERE q.Action_Type = 'CLEAR_DEPOSIT_AT_CHECKOUT'
                                            AND q.Entity_ID = R.Reservation_ID AND q.Status <> 'CANCELLED')",
                        new Dictionary<string, object> { { "@max", max }, { "@d", days } });
                    if (z != null)
                        foreach (DataRow r in z.Rows)
                            save(ReceiptPreflight.Int(r["RID"]), "ZT:" + r["ID"], "ZERO_TOTAL", r["ID"]?.ToString(), 0m,
                                "ใบยอด ≤ 0 ไม่ถูกส่งบัญชี และไม่มีรายการตัดมัดจำตอนเช็คเอาท์ — มัดจำอาจค้างเป็นหนี้สิน (ตรวจที่ปุ่ม NextAcc ของการจอง)");
                }
                catch (Exception ex) { LogThrottled("integ-2", "Integrity scan (not enqueued): " + ex.Message); }
            }

            // 3) มัดจำ (Reservation.Deposit) vs ใบมัดจำ · 4) เช็คเอาท์แล้วยอดค้าง/จ่ายเกิน
            foreach (int rid in resIds.Take(max))
            {
                try
                {
                    var bal = ReservationBalance.Load(_connectionString, rid);
                    if (bal == null) continue;
                    var dd = _code.DatabaseQuerySafe(_connectionString,
                        @"SELECT ISNULL(SUM(CASE WHEN IsDeposit = 1 THEN Total_Amount ELSE 0 END), 0) AS Dep,
                                 (SELECT TOP 1 Status FROM Reservation WHERE ID = @r) AS St
                          FROM Account_Receipt WHERE Reservation_ID = @r AND (Status = 'Normal' OR Status IS NULL)",
                        new Dictionary<string, object> { { "@r", rid } });
                    if (dd == null || dd.Rows.Count == 0) continue;
                    decimal depRcpt = ReceiptPreflight.Dec(dd.Rows[0]["Dep"]);
                    string st = dd.Rows[0]["St"]?.ToString() ?? "";
                    bool checkedOut = st == "เสร็จสิ้น" || st.StartsWith("เช็คเอาท์", StringComparison.Ordinal) || st.StartsWith("เช็คเอ้าท์", StringComparison.Ordinal);
                    if (bal.CollectMode != "CHANNEL" && depRcpt > 0.005m && Math.Abs(bal.Deposit - depRcpt) > 1m)
                        save(rid, "DL:" + rid, "DEPOSIT_LEDGER", null, bal.Deposit - depRcpt,
                            $"มัดจำในการจอง {bal.Deposit:N2} ≠ ใบมัดจำ {depRcpt:N2} (ต่าง {bal.Deposit - depRcpt:N2}) · รับเงินตามสมุด {bal.PaidLedger:N2} — "
                            + "ตอนเช็คอินระบบจะเติมบรรทัดปรับยอดส่วนต่าง ตรวจว่าส่วนต่างรับเงินด้วยใบไหน");
                    decimal tol = Math.Max(ReservationBalance.RoundingTolerance, 1m);
                    if (checkedOut && (bal.Due > tol || bal.Credit > tol))
                        save(rid, "BAL:" + rid, "BALANCE_VS_RECEIPTS", null, bal.Due > tol ? bal.Due : -bal.Credit,
                            bal.Due > tol ? $"เช็คเอาท์แล้วแต่ยังมียอดค้าง {bal.Due:N2} (ยอดรวม {bal.Total:N2} รับ {bal.Received:N2})"
                                          : $"เช็คเอาท์แล้วรับเกิน {bal.Credit:N2} — ออกใบลดหนี้/คืนเงินส่วนเกิน (Account/OverpayRefund)");
                }
                catch { }
            }

            // 5) คิวค้าง
            if (ReceiptPreflight.QueueSchemaReady(_connectionString))
            {
                try
                {
                    var q = _code.DatabaseQuerySafe(_connectionString,
                        @"SELECT TOP 300 ID, Action_Type, Status, Error_Class, Error_Rule, Entity_Type, Entity_ID FROM Accounting_Sync_Queue
                          WHERE (Status = 'PENDING' AND Error_Class = 'WAIT' AND Wait_Since < DATEADD(HOUR, -24, GETDATE()))
                             OR (Status = 'FAILED' AND Retry_Count >= Max_Retries AND Error_Class IN ('DATA','CONFIG','POLICY','WAIT')
                                 AND Created_Date < DATEADD(HOUR, -24, GETDATE()))
                             OR (Status = 'AWAITING_SLIP' AND Created_Date < DATEADD(HOUR, -48, GETDATE()))
                          ORDER BY ID", null);
                    if (q != null)
                        foreach (DataRow r in q.Rows)
                        {
                            string et = r["Entity_Type"]?.ToString();
                            int rid = (et == "RECEIPT" || et == "RESERVATION") ? ReceiptPreflight.Int(r["Entity_ID"]) : 0;
                            string cls = r["Error_Class"]?.ToString() ?? r["Status"]?.ToString();
                            save(rid, "Q:" + r["ID"], "QUEUE_STUCK", null, null,
                                $"คิว #{r["ID"]} {r["Action_Type"]} ค้าง ({r["Status"]}/{cls}{(r["Error_Rule"] == DBNull.Value ? "" : " " + r["Error_Rule"])}) เกินกำหนด");
                        }
                }
                catch { }
            }

            // 6) ใบเดือนก่อนที่ยังไม่ขึ้น NextAcc — ต้องเสร็จก่อนยื่น ภ.พ.30 (วันที่ Nexaacc_Vat_Filing_Day)
            int gap = 0;
            if (syncOn)
            {
                try
                {
                    var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    var g = _code.DatabaseQuerySafe(_connectionString,
                        @"SELECT TOP (@max) R.ID, ISNULL(R.Reservation_ID, 0) AS RID, ISNULL(R.Total_Amount, 0) AS T FROM Account_Receipt R
                          WHERE R.Created_Date >= @from AND R.Created_Date < @to AND (R.Status = 'Normal' OR R.Status IS NULL)
                            AND ISNULL(R.Total_Amount, 0) > 0" + (docCol ? " AND R.Nexaacc_Doc_Id IS NULL" : "") + @"
                            AND NOT EXISTS (SELECT 1 FROM Accounting_Sync_Queue q
                                            WHERE q.Action_Type IN ('CREATE_RECEIPT_DOCUMENT','CREATE_OTA_SALES_DOCUMENT') AND q.Status = 'COMPLETED'
                                              AND q.Payload LIKE N'%""receiptNumber"":""' + R.ID + N'""%')",
                        new Dictionary<string, object> { { "@max", max }, { "@from", first.AddMonths(-1) }, { "@to", first } });
                    if (g != null)
                        foreach (DataRow r in g.Rows)
                        {
                            gap++;
                            save(ReceiptPreflight.Int(r["RID"]), "FG:" + r["ID"], "PRE_FILING_GAP", r["ID"]?.ToString(), ReceiptPreflight.Dec(r["T"]),
                                $"ใบเดือน {first.AddMonths(-1):MM/yyyy} ยังไม่ขึ้น NextAcc — ต้องแก้ก่อนยื่น ภ.พ.30 วันที่ {filingDay}");
                        }
                    int daysLeft = (first.AddDays(filingDay - 1) - DateTime.Today).Days;
                    if (gap > 0 && daysLeft >= 0 && daysLeft <= 5 && _config.QueueAlertEnabled)
                        Notify.Send(Notify.Ev.AccQueueAlert,
                            $"<b>🧾 ใกล้วันยื่น ภ.พ.30 (วันที่ {filingDay}) — อีก {daysLeft} วัน</b>\nใบเสร็จเดือน {first.AddMonths(-1):MM/yyyy} ยังไม่ขึ้น NextAcc {gap} ใบ\n"
                            + "<i>ดู: Admin → ตั้งค่า → NextAcc → คิว / ตรวจสุขภาพ</i>");
                }
                catch (Exception ex) { LogThrottled("integ-6", "Integrity scan (filing gap): " + ex.Message); }
            }

            // ปิดผลตรวจ LOC ที่ไม่พบในรอบนี้
            if (tableOk)
                try
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Accounting_Reconcile_Finding SET Resolved_At = GETDATE(), Resolved_Note = N'ไม่พบในการตรวจรายคืนล่าสุด'
                          WHERE Resolved_At IS NULL AND Finding_Key LIKE N'LOC:%' AND Last_Seen < @s",
                        new Dictionary<string, object> { { "@s", scanStart } });
                }
                catch { }

            // 7) แถวที่พักเพราะข้อมูล — ข้อมูลอาจถูกแก้ผ่านเส้นที่ไม่เข้าคิว → ตรวจซ้ำ ผ่านแล้วคืนเข้าคิว
            int requeued = 0;
            if (ReceiptPreflight.QueueSchemaReady(_connectionString))
            {
                try
                {
                    var dq = _code.DatabaseQuerySafe(_connectionString,
                        @"SELECT TOP 200 ID FROM Accounting_Sync_Queue
                          WHERE Status = 'FAILED' AND Error_Class = 'DATA' AND Action_Type = 'CREATE_RECEIPT_DOCUMENT'", null);
                    if (dq != null)
                        foreach (DataRow r in dq.Rows)
                        {
                            var res = RecheckQueueItem(Convert.ToInt64(r[0]));
                            if (res.requeued) requeued++;
                        }
                }
                catch { }
            }

            string summary = "Integrity scan (ย้อน " + days + " วัน): " +
                (counts.Count == 0 ? "ไม่พบปัญหา" : string.Join(", ", counts.Select(kv => kv.Key + " " + kv.Value))) +
                (requeued > 0 ? $" · คืนเข้าคิว (ข้อมูลแก้แล้ว) {requeued}" : "") + (tableOk ? "" : " · ⚠ เก็บผลไม่ได้ (ยังไม่รัน PHASE19_35?)");
            _code.Logs(_connectionString, "AccountingSync", summary, "SYSTEM");
            return summary;
        }

        // ── ตรวจสุขภาพ: ตัวชี้วัด 14 วัน + ผลตรวจรายคืน ────────────────────────────────
        public static void AppendQueuePreventionHealth(string conn, Action<string, string, string> add)
        {
            try
            {
                var mode = ReceiptPreflight.CurrentMode(conn);
                if (!ReceiptPreflight.QueueSchemaReady(conn))
                {
                    add("info", "ตัวตรวจก่อนส่ง/ชนิดปัญหาคิว: ยังไม่ได้รัน PHASE19_37",
                        "รัน Database/PHASE19_Migration_37_Queue_Prevention.sql แล้ว recycle app pool — ระบบจะเริ่มในโหมด WARN (เตือน ไม่บล็อก)");
                    return;
                }
                var c = new code();
                var o = c.DatabaseQuerySafe(conn,
                    @"SELECT Error_Class, COUNT(*) AS N FROM Accounting_Sync_Queue
                      WHERE Error_Class IS NOT NULL
                        AND ((Status = 'FAILED' AND Retry_Count >= Max_Retries) OR (Status = 'PENDING' AND Error_Class = 'WAIT'))
                      GROUP BY Error_Class", null);
                var open = new List<string>();
                int dataN = 0, configN = 0, policyN = 0;
                if (o != null)
                    foreach (DataRow r in o.Rows)
                    {
                        string cls = r["Error_Class"]?.ToString();
                        int n = Convert.ToInt32(r["N"]);
                        if (cls == "DATA") dataN = n; else if (cls == "CONFIG") configN = n; else if (cls == "POLICY") policyN = n;
                        open.Add(QueueErrors.BadgeTh(QueueErrors.Parse(cls) ?? SyncIssueClass.Unknown) + " " + n);
                    }
                var m = c.DatabaseQuerySafe(conn,
                    @"SELECT COUNT(*) AS Total,
                             SUM(CASE WHEN Retry_Count = 0 AND Error_Class IS NULL THEN 1 ELSE 0 END) AS FirstPass,
                             SUM(CASE WHEN Error_Class IN ('WAIT','TRANSIENT') THEN 1 ELSE 0 END) AS AutoHealed,
                             SUM(CASE WHEN Error_Class IN ('DATA','CONFIG','POLICY','DUPLICATE','UNKNOWN') THEN 1 ELSE 0 END) AS ManualFixed
                      FROM Accounting_Sync_Queue
                      WHERE Action_Type = 'CREATE_RECEIPT_DOCUMENT' AND Status = 'COMPLETED'
                        AND Created_Date >= DATEADD(DAY, -14, GETDATE())", null);
                string kpi = "";
                if (m != null && m.Rows.Count > 0 && m.Rows[0]["Total"] != DBNull.Value && Convert.ToInt32(m.Rows[0]["Total"]) > 0)
                {
                    int t = Convert.ToInt32(m.Rows[0]["Total"]);
                    int fp = m.Rows[0]["FirstPass"] == DBNull.Value ? 0 : Convert.ToInt32(m.Rows[0]["FirstPass"]);
                    int ah = m.Rows[0]["AutoHealed"] == DBNull.Value ? 0 : Convert.ToInt32(m.Rows[0]["AutoHealed"]);
                    int mf = m.Rows[0]["ManualFixed"] == DBNull.Value ? 0 : Convert.ToInt32(m.Rows[0]["ManualFixed"]);
                    kpi = $"ใบเสร็จส่งสำเร็จ 14 วัน: {t} ใบ · ผ่านรอบแรก {fp * 100.0 / t:0.#}% (เป้า ≥ 95%) · หายเอง (รอ/ลองใหม่) {ah} · ต้องมีคนแก้ {mf}";
                }
                if (ReceiptPreflight.EditLogReady(conn))
                {
                    var e = c.DatabaseQuerySafe(conn,
                        @"SELECT SUM(CASE WHEN Action = 'BLOCKED' THEN 1 ELSE 0 END) AS B,
                                 SUM(CASE WHEN Action = 'OVERRIDE' THEN 1 ELSE 0 END) AS O,
                                 SUM(CASE WHEN Action = 'EDIT' THEN 1 ELSE 0 END) AS E
                          FROM Account_Receipt_Edit_Log WHERE Edited_At >= DATEADD(DAY, -14, GETDATE())", null);
                    if (e != null && e.Rows.Count > 0)
                        kpi += (kpi.Length > 0 ? "\n" : "") + $"หน้าเว็บ 14 วัน: บล็อกก่อนบันทึก {ReceiptPreflight.Int(e.Rows[0]["B"])} · ยืนยันข้าม {ReceiptPreflight.Int(e.Rows[0]["O"])} · แก้ใบ (มีประวัติ) {ReceiptPreflight.Int(e.Rows[0]["E"])}";
                }
                string modeTh = mode == PreflightMode.Block ? "BLOCK (บล็อกปัญหาข้อมูลที่หน้าเว็บ)" : mode == PreflightMode.Warn ? "WARN (เตือน ไม่บล็อก)" : "OFF (ปิด)";
                add("info", "11) ตัวตรวจก่อนส่ง: โหมด " + modeTh,
                    (open.Count > 0 ? "ค้างที่ต้องดู: " + string.Join(" · ", open) + "\n" : "ไม่มีรายการค้างที่ต้องมีคนแก้\n") + kpi);
                if (dataN > 0) add("warn", $"คิวพักเพราะข้อมูลใบเสร็จผิด: {dataN} รายการ", "กรองชนิด \"แก้ข้อมูล\" ในตารางคิว → ปุ่ม \"แก้ที่นี่\" เปิดหน้าแก้ใบ — บันทึกแล้วเข้าคิวเอง");
                if (configN > 0) add("warn", $"คิวพักเพราะการตั้งค่า: {configN} รายการ", "แก้ mapping/แหล่งเงินตามข้อความในคิว — กดบันทึกแล้วรายการกลับเข้าคิวเอง");
                if (policyN > 0) add("error", $"คิวติดนโยบายบัญชี/ภาษี (ต้องใบลดหนี้/เปิดงวด): {policyN} รายการ", "ปุ่ม \"แก้ที่นี่\" ในคิวพาไปหน้าออกใบลดหนี้");
                try
                {
                    var lf = c.DatabaseQuerySafe(conn,
                        @"IF OBJECT_ID(N'dbo.Accounting_Reconcile_Finding', N'U') IS NOT NULL
                          SELECT Finding_Type, COUNT(*) AS N FROM dbo.Accounting_Reconcile_Finding
                          WHERE Resolved_At IS NULL AND Finding_Key LIKE N'LOC:%' GROUP BY Finding_Type", null);
                    if (lf != null && lf.Rows.Count > 0)
                    {
                        var parts = lf.Rows.Cast<DataRow>().Select(r => r["Finding_Type"] + " " + r["N"]).ToList();
                        add("warn", "ผลตรวจรายคืน (ไม่เรียก API) ที่ยังเปิด", string.Join(" · ", parts)
                            + "\nPREFLIGHT_BLOCK=ใบผิดกฎ · NOT_ENQUEUED=ใบไม่เคยเข้าคิว · DEPOSIT_LEDGER=มัดจำการจอง≠ใบมัดจำ · BALANCE_VS_RECEIPTS=เช็คเอาท์แล้วยอดไม่ปิด · "
                            + "QUEUE_STUCK=คิวค้าง · PRE_FILING_GAP=ใบเดือนก่อนยังไม่ขึ้น NextAcc (ก่อนยื่น ภ.พ.30)");
                    }
                }
                catch { }
            }
            catch (Exception ex) { add("warn", "ตรวจตัวชี้วัดคิวไม่สำเร็จ", ex.Message); }
        }
    }
}
