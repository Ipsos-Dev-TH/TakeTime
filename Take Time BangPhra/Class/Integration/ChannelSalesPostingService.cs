using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>หนึ่งการขายที่ "รับเงินแล้วจริง" จากช่องทางนอก POS (ดู ChannelSalesPostingService)</summary>
    public sealed class ChannelSale
    {
        /// <summary>SalesDocumentPolicy.Ch* (ACTIVITY / AMENITY / FRONTDESK_GATEWAY / DAMAGE)</summary>
        public string Channel;
        /// <summary>คีย์กันซ้ำ (UNIQUE) — ACT-{bookingId} / AMN-{requestId} / FDG-{txnId} / DMG-{checkoutId}</summary>
        public string SourceKey;
        public int ReservationId;
        /// <summary>วันรับเงิน (จุดเกิดภาษี §78/1) — ใช้เป็นวันที่เอกสาร</summary>
        public DateTime SaleDate = DateTime.Now;
        public int PaidHowId;
        public string PaidHowName;
        /// <summary>ยอดรวม VAT (gross) ที่รับจริง</summary>
        public decimal Amount;
        /// <summary>mapping รายได้ที่อยากได้ — ว่าง = ตามช่องทาง; ไม่ผูกบัญชีจริงจะถอยตาม chain เอง</summary>
        public string RevenueKey;
        public string Description;
        public string CustomerName;
        public string CustomerPhone;
        public string CreatedBy;
    }

    /// <summary>บรรทัดของใบสรุป/ใบรายการ (ProductTypeId null = ใช้ revenueType ของใบ)</summary>
    public sealed class ChannelReceiptLine
    {
        public string Text;
        public decimal Amount;
        public string ProductTypeId;
        public decimal Quantity = 1m;
        public string Unit = "ครั้ง";
    }

    /// <summary>
    /// รายได้ช่องทางนอก POS → NextAcc "ครั้งเดียวต่อการขาย" ตามนโยบายเอกสารขาย (SalesDocumentPolicy):
    ///   • ACTIVITY          กิจกรรมที่จ่ายเอง (โอน/เงินสด/ออนไลน์) — เดิมไม่เคยเข้าบัญชีเลย
    ///   • AMENITY           เบิกของใช้ที่จ่ายแยกออนไลน์ (ไม่ได้รวมบิลห้อง)
    ///   • FRONTDESK_GATEWAY เงินเกตเวย์จากหน้า Payment/Charge ที่ไม่มีการบันทึกขาย POS คู่กัน (ตาข่ายกันลืม)
    ///   • DAMAGE            ค่าเสียหายตอนเช็คเอาท์ส่วนที่มัดจำ/เงินประกันไม่ครอบคลุม
    /// ทุกการขายลงสมุด Sales_Channel_Posting (UNIQUE Source_Key ⇒ กันซ้ำข้ามหน้าจอ/การกดซ้ำ/เกตเวย์ส่งซ้ำ)
    /// แล้ว route ด้วย SalesDocumentPolicy.Resolve(conn, channel, paidHowId):
    ///   IMMEDIATE    → ใบรายการเดียวทันที (Account_Receipt {Source_Key} + EnqueueReceipt)
    ///   DAILY_ROLLUP → ใบสรุปหลังจบวัน {P}DAY-{yyyyMMdd}-{paidHowId}[-S{n}] (S = ใบเสริมของรายการที่มาช้า)
    ///   OFF          → บันทึกไว้ในสมุด ไม่ออกเอกสาร
    /// นโยบายยังไม่เปิด (Sales_Doc_Policy_Enabled ≠ 1) = พฤติกรรมเดิม: ไม่ส่ง (บันทึกสถานะ NOPOLICY ไว้ตรวจย้อน)
    /// ไม่มีตาราง (ยังไม่รัน PHASE19_32) = no-op ทั้งหมด
    /// </summary>
    public class ChannelSalesPostingService
    {
        public const string StPending = "PENDING";
        public const string StPosting = "POSTING";
        public const string StPosted = "POSTED";
        public const string StOff = "OFF";
        public const string StMatched = "MATCHED";
        public const string StNoPolicy = "NOPOLICY";

        public const string ModeImmediate = "IMMEDIATE";
        public const string ModeRollup = "DAILY_ROLLUP";
        public const string ModeOff = "OFF";

        private readonly string _conn;
        private readonly code _code = new code();

        public ChannelSalesPostingService(string connectionString)
        {
            _conn = connectionString;
        }

        // ═══════════════════════════════════════════════════════════════════
        // บันทึกการขาย (เรียกจากจุดที่ยืนยันรับเงิน)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// บันทึกการขายที่รับเงินแล้ว 1 รายการ — idempotent ด้วย Source_Key. IMMEDIATE ส่งเข้าคิวเอกสารทันที.
        /// ไม่ throw (คืนข้อความสถานะ, null = ไม่มีตาราง/ไม่ได้ทำอะไร) — การรับเงินต้องไม่พังเพราะบัญชี
        /// </summary>
        public string Record(ChannelSale s)
        {
            try
            {
                if (s == null || string.IsNullOrWhiteSpace(s.SourceKey) || s.Amount <= 0m) return null;
                if (!ChannelPosting.TableReady(_conn)) return null;

                string channel = string.IsNullOrWhiteSpace(s.Channel) ? "OTHER" : s.Channel.Trim().ToUpperInvariant();
                if (s.PaidHowId <= 0 && !string.IsNullOrWhiteSpace(s.PaidHowName))
                    s.PaidHowId = ChannelPosting.FindPaidHowId(_conn, s.PaidHowName);
                if (string.IsNullOrWhiteSpace(s.PaidHowName) && s.PaidHowId > 0)
                    s.PaidHowName = ChannelPosting.FindPaidHowName(_conn, s.PaidHowId);

                string status, mode = null, reason = null;
                if (!ChannelPosting.PolicyEnabled(_conn))
                {
                    status = StNoPolicy;
                    reason = "นโยบายเอกสารขายยังไม่เปิด (พฤติกรรมเดิม — ไม่ส่ง)";
                }
                else
                {
                    ChannelPosting.ResolveMode(_conn, channel, s.PaidHowId, out mode, out reason);
                    status = mode == ModeOff ? StOff : StPending;
                }

                string revKey = string.IsNullOrWhiteSpace(s.RevenueKey) ? ChannelPosting.DefaultRevenueKey(channel) : s.RevenueKey.Trim();
                int n = InsertLedgerRow(channel, s, revKey, mode, status, null, reason);
                if (n <= 0) return "บันทึกการขายนี้ไว้แล้ว (" + s.SourceKey + ")";

                Log($"Record {s.SourceKey} {channel} {s.Amount:N2} paidHow={s.PaidHowName}#{s.PaidHowId} → {status}/{mode ?? "-"} ({reason})");

                if (status == StPending && mode == ModeImmediate)
                {
                    long id = LedgerIdOf(s.SourceKey);
                    if (id > 0)
                    {
                        string posted = PostImmediate(id);
                        return posted != null ? "ส่งเอกสารรายได้ " + posted + " เข้าคิวบัญชีแล้ว" : "บันทึกการขายแล้ว (รอส่งเข้าบัญชีรอบถัดไป)";
                    }
                }
                if (status == StPending) return "บันทึกการขายแล้ว — รวมในใบสรุปรายวัน";
                if (status == StOff) return "บันทึกการขายแล้ว (นโยบาย: ไม่ออกเอกสาร)";
                return "บันทึกการขายแล้ว";
            }
            catch (Exception ex)
            {
                Log("Record " + (s == null ? "?" : s.SourceKey) + " ล้มเหลว: " + ex.Message);
                return null;
            }
        }

        /// <summary>มีการขายของ Source_Key นี้ในสมุดแล้วหรือยัง (ไม่มีตาราง = false)</summary>
        public bool IsRecorded(string sourceKey)
        {
            return LedgerIdOf(sourceKey) > 0;
        }

        private int InsertLedgerRow(string channel, ChannelSale s, string revKey, string mode, string status,
            string matchedKey, string note)
        {
            try
            {
                return _code.DatabaseInsertSafe(_conn, @"
                    IF NOT EXISTS (SELECT 1 FROM dbo.Sales_Channel_Posting WHERE Source_Key = @k)
                    INSERT INTO dbo.Sales_Channel_Posting
                        (Sales_Channel, Source_Key, Reservation_ID, Sale_Date, Paid_How_ID, Paid_How_Name, Amount,
                         Revenue_Key, [Description], Customer_Phone, Doc_Mode, [Status], Matched_Key, Note, Created_By, Created_At)
                    VALUES (@ch, @k, @res, @d, @ph, @phn, @amt, @rk, @desc, @phone, @mode, @st, @mk, @note, @by, GETDATE())",
                    new Dictionary<string, object>
                    {
                        { "@ch", channel }, { "@k", ChannelPosting.Trunc(s.SourceKey, 80) },
                        { "@res", s.ReservationId > 0 ? (object)s.ReservationId : DBNull.Value },
                        { "@d", s.SaleDate.Date },
                        { "@ph", s.PaidHowId > 0 ? (object)s.PaidHowId : DBNull.Value },
                        { "@phn", (object)ChannelPosting.Trunc(s.PaidHowName, 150) ?? DBNull.Value },
                        { "@amt", Math.Round(s.Amount, 2, MidpointRounding.AwayFromZero) },
                        { "@rk", ChannelPosting.Trunc(revKey, 60) },
                        { "@desc", (object)ChannelPosting.Trunc(s.Description, 300) ?? DBNull.Value },
                        { "@phone", (object)ChannelPosting.Trunc(s.CustomerPhone, 30) ?? DBNull.Value },
                        { "@mode", (object)mode ?? DBNull.Value },
                        { "@st", status },
                        { "@mk", (object)ChannelPosting.Trunc(matchedKey, 100) ?? DBNull.Value },
                        { "@note", (object)ChannelPosting.Trunc(note, 500) ?? DBNull.Value },
                        { "@by", (object)ChannelPosting.Trunc(s.CreatedBy, 100) ?? DBNull.Value }
                    });
            }
            catch (SqlException sx) when (sx.Number == 2627 || sx.Number == 2601)
            {
                return 0;   // ชนกันพร้อมกัน — อีกฝั่งบันทึกไปแล้ว
            }
        }

        private long LedgerIdOf(string sourceKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceKey) || !ChannelPosting.TableReady(_conn)) return 0;
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ID FROM dbo.Sales_Channel_Posting WHERE Source_Key = @k",
                    new Dictionary<string, object> { { "@k", sourceKey } });
                return dt != null && dt.Rows.Count > 0 ? Convert.ToInt64(dt.Rows[0][0]) : 0;
            }
            catch { return 0; }
        }

        // ═══════════════════════════════════════════════════════════════════
        // งานเบื้องหลัง (Global.asax timer)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// ส่งรายการค้าง: IMMEDIATE ที่ส่งไม่สำเร็จ, ใบสรุป DAILY_ROLLUP ของวันที่จบแล้ว, ตาข่ายเงินเกตเวย์หน้าร้าน.
        /// no-op ถ้าไม่มีตาราง/ยังไม่ตั้งค่าบัญชี/นโยบายยังไม่เปิด
        /// </summary>
        public void PostDueIfAny(int max = 50)
        {
            try
            {
                if (!ChannelPosting.TableReady(_conn)) return;
                var cfg = new AccountingConfig(_conn);
                if (!cfg.IsConfigured || !cfg.Enabled) return;
                if (!ChannelPosting.PolicyEnabled(_conn)) return;

                RecoverStale();

                // 1) IMMEDIATE ที่ยังค้าง (เข้าคิวไม่สำเร็จตอนบันทึก)
                var imm = _code.DatabaseQuerySafe(_conn, @"
                    SELECT TOP (@n) ID FROM dbo.Sales_Channel_Posting
                     WHERE [Status] = 'PENDING' AND Doc_Mode = 'IMMEDIATE'
                     ORDER BY ID",
                    new Dictionary<string, object> { { "@n", max } });
                if (imm != null)
                    foreach (DataRow r in imm.Rows) PostImmediate(Convert.ToInt64(r["ID"]));

                // 2) ใบสรุปรายวัน (วันที่จบแล้ว)
                PostRollups(14);

                // 3) เงินเกตเวย์หน้าร้านที่ไม่มีการขาย POS คู่กัน
                try { ScanFrontdeskGateway(max); }
                catch (Exception fx) { Log("ScanFrontdeskGateway: " + fx.Message); }
            }
            catch (Exception ex)
            {
                Log("PostDueIfAny: " + ex.Message);
            }
        }

        /// <summary>POSTING ค้างเกิน 15 นาที (process ตายกลางทาง) → ทำต่อแบบ idempotent</summary>
        private void RecoverStale()
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, @"
                    SELECT DISTINCT Doc_Mode, Post_Ref, CASE WHEN Doc_Mode = 'IMMEDIATE' THEN ID ELSE 0 END AS ImmId
                      FROM dbo.Sales_Channel_Posting
                     WHERE [Status] = 'POSTING' AND (Claimed_At IS NULL OR Claimed_At < DATEADD(MINUTE, -15, GETDATE()))", null);
                if (dt == null) return;
                foreach (DataRow r in dt.Rows)
                {
                    string mode = Convert.ToString(r["Doc_Mode"]);
                    string pref = r["Post_Ref"] == DBNull.Value ? null : Convert.ToString(r["Post_Ref"]);
                    if (mode == ModeRollup && !string.IsNullOrEmpty(pref))
                    {
                        FinishRollup(pref);
                    }
                    else
                    {
                        long id = Convert.ToInt64(r["ImmId"]);
                        if (id > 0)
                        {
                            _code.DatabaseInsertSafe(_conn,
                                "UPDATE dbo.Sales_Channel_Posting SET [Status] = 'PENDING' WHERE ID = @id AND [Status] = 'POSTING'",
                                new Dictionary<string, object> { { "@id", id } });
                            PostImmediate(id);
                        }
                    }
                }
            }
            catch (Exception ex) { Log("RecoverStale: " + ex.Message); }
        }

        /// <summary>ส่งรายการ IMMEDIATE หนึ่งแถว — คืนเลขเอกสาร (null = ยังไม่สำเร็จ, คงสถานะให้ลองใหม่)</summary>
        private string PostImmediate(long id)
        {
            int claimed = _code.DatabaseInsertSafe(_conn, @"
                UPDATE dbo.Sales_Channel_Posting SET [Status] = 'POSTING', Claimed_At = GETDATE()
                 WHERE ID = @id AND [Status] = 'PENDING'",
                new Dictionary<string, object> { { "@id", id } });
            if (claimed <= 0) return null;

            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT * FROM dbo.Sales_Channel_Posting WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                if (dt == null || dt.Rows.Count == 0) return null;
                DataRow r = dt.Rows[0];

                var cfg = new AccountingConfig(_conn);
                if (!cfg.IsConfigured || !cfg.Enabled)
                {
                    Release(id, "ยังไม่ได้ตั้งค่า/ปิดการเชื่อมต่อบัญชี — รอรอบถัดไป");
                    return null;
                }

                string channel = Convert.ToString(r["Sales_Channel"]);
                string key = Convert.ToString(r["Source_Key"]);
                string refId = key.Length <= ChannelPosting.MaxReceiptIdLength
                    ? key
                    : ChannelPosting.PrefixOf(channel) + "-" + id.ToString(CultureInfo.InvariantCulture);
                DateTime saleDate = Convert.ToDateTime(r["Sale_Date"]);
                decimal amount = ChannelPosting.Dec(r["Amount"]);
                int paidHowId = r["Paid_How_ID"] == DBNull.Value ? 0 : Convert.ToInt32(r["Paid_How_ID"]);
                string paidHowName = r["Paid_How_Name"] == DBNull.Value ? ChannelPosting.FindPaidHowName(_conn, paidHowId) : Convert.ToString(r["Paid_How_Name"]);
                string desc = r["Description"] == DBNull.Value ? (ChannelPosting.ChannelThai(channel) + " " + key) : Convert.ToString(r["Description"]);
                string phone = r["Customer_Phone"] == DBNull.Value ? null : Convert.ToString(r["Customer_Phone"]);
                string revKey = ChannelPosting.ResolveRevenueKey(_conn, ChannelPosting.RevenueChain(channel, Convert.ToString(r["Revenue_Key"])));

                ChannelPosting.WriteSummaryReceipt(_conn, refId, saleDate, paidHowName,
                    new List<ChannelReceiptLine> { new ChannelReceiptLine { Text = desc, Amount = amount } });

                var sync = new AccountingSyncService(_conn);
                string accId = ChannelPosting.PaidHowAccountId(_conn, paidHowId) ?? sync.LookupPaidHowAccountId(paidHowName);
                long q = sync.EnqueueReceipt(0, refId, amount, 0, saleDate, desc,
                    isDeposit: false, paymentMethod: paidHowName, revenueType: revKey, paymentAccountId: accId,
                    customerPhone: phone);
                if (q <= 0)
                {
                    Release(id, "เข้าคิวเอกสารไม่สำเร็จ (EnqueueReceipt=" + q + ") — ลองใหม่รอบถัดไป");
                    return null;
                }

                _code.DatabaseInsertSafe(_conn, @"
                    UPDATE dbo.Sales_Channel_Posting
                       SET [Status] = 'POSTED', Post_Ref = @ref, Posted_At = GETDATE(), Revenue_Key = @rk
                     WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id }, { "@ref", refId }, { "@rk", revKey } });
                Log($"IMMEDIATE {key} → เอกสาร {refId} ({amount:N2}, {paidHowName}, {revKey}) คิว #{q}");
                return refId;
            }
            catch (Exception ex)
            {
                Release(id, "ส่งไม่สำเร็จ: " + ex.Message);
                Log("PostImmediate #" + id + ": " + ex.Message);
                return null;
            }
        }

        private void Release(long id, string note)
        {
            try
            {
                _code.DatabaseInsertSafe(_conn, @"
                    UPDATE dbo.Sales_Channel_Posting SET [Status] = 'PENDING', Note = @n
                     WHERE ID = @id AND [Status] = 'POSTING'",
                    new Dictionary<string, object> { { "@id", id }, { "@n", ChannelPosting.Trunc(note, 500) } });
            }
            catch { }
        }

        /// <summary>
        /// ใบสรุปรายวัน: กลุ่ม (ช่องทาง × วัน × แหล่งเงิน) ที่ยัง PENDING ของวันที่จบแล้ว.
        /// เลขใบ = {P}DAY-{yyyyMMdd}-{paidHowId}; ถ้าเลขนั้นใช้ไปแล้ว (รายการยืนยันช้า) → -S1, -S2 … (ไม่ทิ้งเงียบ)
        /// </summary>
        private void PostRollups(int maxGroups)
        {
            var groups = _code.DatabaseQuerySafe(_conn, @"
                SELECT TOP (@n) Sales_Channel, Sale_Date, ISNULL(Paid_How_ID, 0) AS PH
                  FROM dbo.Sales_Channel_Posting
                 WHERE [Status] = 'PENDING' AND Doc_Mode = 'DAILY_ROLLUP'
                   AND Sale_Date < CAST(GETDATE() AS DATE)
                 GROUP BY Sales_Channel, Sale_Date, ISNULL(Paid_How_ID, 0)
                 ORDER BY Sale_Date",
                new Dictionary<string, object> { { "@n", maxGroups } });
            if (groups == null) return;

            foreach (DataRow g in groups.Rows)
            {
                string channel = Convert.ToString(g["Sales_Channel"]);
                DateTime day = Convert.ToDateTime(g["Sale_Date"]);
                int ph = Convert.ToInt32(g["PH"]);
                try
                {
                    string prefix = ChannelPosting.PrefixOf(channel);
                    string refId = ChannelPosting.NextFreeRollupRef(_conn, prefix + "DAY", day, ph.ToString(CultureInfo.InvariantCulture),
                        LedgerRefUsed);
                    if (refId == null) { Log($"Rollup {channel} {day:yyyy-MM-dd} #{ph}: หาเลขเอกสารว่างไม่ได้"); continue; }

                    int claimed = _code.DatabaseInsertSafe(_conn, @"
                        UPDATE dbo.Sales_Channel_Posting
                           SET [Status] = 'POSTING', Post_Ref = @ref, Claimed_At = GETDATE()
                         WHERE [Status] = 'PENDING' AND Doc_Mode = 'DAILY_ROLLUP'
                           AND Sales_Channel = @ch AND Sale_Date = @d AND ISNULL(Paid_How_ID, 0) = @ph",
                        new Dictionary<string, object> { { "@ref", refId }, { "@ch", channel }, { "@d", day.Date }, { "@ph", ph } });
                    if (claimed <= 0) continue;
                    FinishRollup(refId);
                }
                catch (Exception ex)
                {
                    Log($"Rollup {channel} {day:yyyy-MM-dd} #{ph} ล้มเหลว: {ex.Message}");
                }
            }
        }

        private bool LedgerRefUsed(string refId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 1 FROM dbo.Sales_Channel_Posting WHERE Post_Ref = @r",
                    new Dictionary<string, object> { { "@r", refId } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>ออกใบสรุปจากแถวที่จองเลข refId ไว้ (POSTING) — idempotent (ใบ IF NOT EXISTS + dedup คิว)</summary>
        private void FinishRollup(string refId)
        {
            var rows = _code.DatabaseQuerySafe(_conn, @"
                SELECT Sales_Channel, Sale_Date, ISNULL(Paid_How_ID, 0) AS PH, Paid_How_Name, Revenue_Key, Amount
                  FROM dbo.Sales_Channel_Posting
                 WHERE Post_Ref = @r AND [Status] = 'POSTING'",
                new Dictionary<string, object> { { "@r", refId } });
            if (rows == null || rows.Rows.Count == 0) return;

            var cfg = new AccountingConfig(_conn);
            if (!cfg.IsConfigured || !cfg.Enabled) return;   // คง POSTING ไว้ — RecoverStale ทำต่อเมื่อเปิด

            string channel = Convert.ToString(rows.Rows[0]["Sales_Channel"]);
            DateTime day = Convert.ToDateTime(rows.Rows[0]["Sale_Date"]);
            int ph = Convert.ToInt32(rows.Rows[0]["PH"]);
            string paidHowName = rows.Rows[0]["Paid_How_Name"] == DBNull.Value
                ? ChannelPosting.FindPaidHowName(_conn, ph) : Convert.ToString(rows.Rows[0]["Paid_How_Name"]);
            if (string.IsNullOrWhiteSpace(paidHowName)) paidHowName = "เงินสด";

            // ยอดต่อ mapping รายได้ (ปกติช่องทางละ 1 บัญชี) — บัญชีแรก = revenueType ของใบ (บรรทัด ProductType ว่าง)
            var byKey = new Dictionary<string, decimal>();
            var order = new List<string>();
            decimal total = 0m;
            foreach (DataRow r in rows.Rows)
            {
                string k = ChannelPosting.ResolveRevenueKey(_conn, ChannelPosting.RevenueChain(channel, Convert.ToString(r["Revenue_Key"])));
                decimal a = ChannelPosting.Dec(r["Amount"]);
                if (!byKey.ContainsKey(k)) { byKey[k] = 0m; order.Add(k); }
                byKey[k] += a;
                total += a;
            }
            if (total <= 0m)
            {
                MarkPosted(refId, "ยอดรวม 0 — ไม่ออกเอกสาร");
                return;
            }

            string baseText = ChannelPosting.ChannelThai(channel) + "สรุปรายวัน " + day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                            + " (" + paidHowName + ")" + (refId.Contains("-S") ? " — ใบเสริม" : "");
            var lines = new List<ChannelReceiptLine>();
            for (int i = 0; i < order.Count; i++)
            {
                string k = order[i];
                lines.Add(new ChannelReceiptLine
                {
                    Text = baseText + (order.Count > 1 ? " [" + k + "]" : ""),
                    Amount = byKey[k],
                    ProductTypeId = i == 0 ? null : ChannelPosting.ProductTypeIdForRevenueKey(_conn, k)
                });
            }

            ChannelPosting.WriteSummaryReceipt(_conn, refId, day, paidHowName, lines);

            var sync = new AccountingSyncService(_conn);
            string accId = ChannelPosting.PaidHowAccountId(_conn, ph) ?? sync.LookupPaidHowAccountId(paidHowName);
            long q = sync.EnqueueReceipt(0, refId, total, 0, day.Date, baseText,
                isDeposit: false, paymentMethod: paidHowName, revenueType: order[0], paymentAccountId: accId);
            if (q <= 0)
            {
                Log($"Rollup {refId}: เข้าคิวไม่สำเร็จ ({q}) — คง POSTING ไว้ให้ RecoverStale ทำต่อ");
                return;
            }
            MarkPosted(refId, null);
            Log($"Rollup {refId}: {rows.Rows.Count} รายการ {total:N2} ({paidHowName}) คิว #{q}");
        }

        private void MarkPosted(string refId, string note)
        {
            _code.DatabaseInsertSafe(_conn, @"
                UPDATE dbo.Sales_Channel_Posting
                   SET [Status] = 'POSTED', Posted_At = GETDATE(), Note = COALESCE(@n, Note)
                 WHERE Post_Ref = @r AND [Status] = 'POSTING'",
                new Dictionary<string, object> { { "@r", refId }, { "@n", (object)note ?? DBNull.Value } });
        }

        // ═══════════════════════════════════════════════════════════════════
        // ตาข่ายเงินเกตเวย์หน้าร้าน (Payment/Charge.aspx, Source_Type = POS)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// หน้า Payment/Charge ตั้งใจ "เก็บเงินก่อน แล้วพนักงานบันทึกขาย POS" — ลืมบันทึกขาย = เงินเข้าแต่ไม่มีรายได้.
        /// หลังจบวัน + ระยะผ่อนผัน (Nexaacc_FrontdeskGateway_GraceHours, 24 ชม.) เทียบต่อ (วัน × แหล่งเงินเกตเวย์):
        ///   ขาด = ยอดเกตเวย์ − ยอดขาย POS ที่บันทึกด้วยแหล่งเงินนั้น − ยอดที่โพสต์จากตาข่ายนี้ไปแล้ว
        /// ไล่รายการเกตเวย์ตามเวลา: ขาด &gt; 0 → บันทึกเป็นการขาย FRONTDESK_GATEWAY (FDG-{txnId}) เท่าที่ขาด,
        /// ไม่ขาด → MATCHED (ถือว่าพนักงานบันทึกขายแล้ว — ไม่ออกซ้ำ). กันซ้ำด้วย Source_Key = FDG-{txnId}.
        /// เฉพาะรายการที่จ่ายตั้งแต่ Nexaacc_ChannelSales_Since (กันโพสต์ย้อนหลังทั้งประวัติ)
        /// </summary>
        private void ScanFrontdeskGateway(int max)
        {
            if (!ChannelPosting.ReadBool(_conn, "Nexaacc_FrontdeskGateway_AutoRevenue", true)) return;
            DateTime since;
            string sinceText = ChannelPosting.ReadConfig(_conn, "Nexaacc_ChannelSales_Since", null);
            if (string.IsNullOrWhiteSpace(sinceText)
                || !DateTime.TryParse(sinceText, CultureInfo.InvariantCulture, DateTimeStyles.None, out since)) return;
            int grace = ChannelPosting.ReadInt(_conn, "Nexaacc_FrontdeskGateway_GraceHours", 24);
            if (grace < 0) grace = 0;

            var days = _code.DatabaseQuerySafe(_conn, @"
                SELECT DISTINCT TOP (@n) CAST(t.Paid_At AS DATE) AS D
                  FROM Payment_Transaction t
                 WHERE t.Source_Type = 'POS' AND t.[Status] = 'PAID' AND t.Paid_At IS NOT NULL
                   AND t.Paid_At >= @since
                   AND t.Paid_At < DATEADD(HOUR, -@grace, GETDATE())
                   AND CAST(t.Paid_At AS DATE) < CAST(GETDATE() AS DATE)
                   AND NOT EXISTS (SELECT 1 FROM dbo.Sales_Channel_Posting s
                                    WHERE s.Source_Key = 'FDG-' + CAST(t.ID AS NVARCHAR(20)))
                 ORDER BY CAST(t.Paid_At AS DATE)",
                new Dictionary<string, object> { { "@n", 7 }, { "@since", since.Date }, { "@grace", grace } });
            if (days == null || days.Rows.Count == 0) return;

            int done = 0;
            foreach (DataRow d in days.Rows)
            {
                if (done >= max) break;
                done += ScanFrontdeskGatewayDay(Convert.ToDateTime(d["D"]), grace, max - done);
            }
        }

        private int ScanFrontdeskGatewayDay(DateTime day, int grace, int max)
        {
            DataTable txns;
            try
            {
                txns = _code.DatabaseQuerySafe(_conn, @"
                    SELECT t.ID, t.Txn_Ref, t.Provider, t.Method, t.Card_Brand, t.Amount, ISNULL(t.Refunded_Amount, 0) AS Refunded,
                           t.Paid_At, t.[Description], t.Customer_Phone, t.Customer_Name
                      FROM Payment_Transaction t
                     WHERE t.Source_Type = 'POS' AND t.[Status] = 'PAID' AND CAST(t.Paid_At AS DATE) = @d
                     ORDER BY t.Paid_At, t.ID",
                    new Dictionary<string, object> { { "@d", day.Date } });
            }
            catch
            {
                // ยังไม่มีคอลัมน์ Refunded_Amount
                txns = _code.DatabaseQuerySafe(_conn, @"
                    SELECT t.ID, t.Txn_Ref, t.Provider, t.Method, t.Card_Brand, t.Amount, CAST(0 AS DECIMAL(18,2)) AS Refunded,
                           t.Paid_At, t.[Description], t.Customer_Phone, t.Customer_Name
                      FROM Payment_Transaction t
                     WHERE t.Source_Type = 'POS' AND t.[Status] = 'PAID' AND CAST(t.Paid_At AS DATE) = @d
                     ORDER BY t.Paid_At, t.ID",
                    new Dictionary<string, object> { { "@d", day.Date } });
            }
            if (txns == null || txns.Rows.Count == 0) return 0;

            // จัดกลุ่มตามแหล่งเงินที่ใช้บันทึก (ชื่อเดียวกับที่หน้า Charge บอกพนักงานให้เลือกตอนบันทึกขาย)
            var groups = new Dictionary<string, List<DataRow>>();
            var groupId = new Dictionary<string, int>();
            foreach (DataRow t in txns.Rows)
            {
                string name = null;
                try
                {
                    name = Take_Time_BangPhra.Payments.PaymentChannelCatalog.ResolveRecordingPaidHowName(_conn,
                        Convert.ToString(t["Provider"]), Convert.ToString(t["Method"]),
                        t["Card_Brand"] == DBNull.Value ? null : Convert.ToString(t["Card_Brand"]));
                }
                catch { }
                if (string.IsNullOrWhiteSpace(name))
                {
                    try { name = Take_Time_BangPhra.Payments.PaymentChannelCatalog.LegacyPaidHowName; } catch { }
                }
                if (string.IsNullOrWhiteSpace(name)) name = "-";
                if (!groups.ContainsKey(name))
                {
                    groups[name] = new List<DataRow>();
                    groupId[name] = ChannelPosting.FindPaidHowId(_conn, name);
                }
                groups[name].Add(t);
            }

            int processed = 0;
            foreach (var kv in groups)
            {
                string paidHowName = kv.Key;
                int ph = groupId[paidHowName];
                decimal gatewayTotal = 0m;
                foreach (DataRow t in kv.Value) gatewayTotal += NetTxn(t);

                decimal posTotal = PosSalesTotal(day, paidHowName, ph);
                decimal fdgPosted = FrontdeskPostedTotal(day, ph, paidHowName);
                decimal shortfall = gatewayTotal - posTotal - fdgPosted;

                foreach (DataRow t in kv.Value)
                {
                    if (processed >= max) return processed;
                    long txnId = Convert.ToInt64(t["ID"]);
                    string key = "FDG-" + txnId.ToString(CultureInfo.InvariantCulture);
                    if (LedgerIdOf(key) > 0) continue;

                    decimal amt = NetTxn(t);
                    decimal post = Math.Min(amt, Math.Max(0m, shortfall));
                    DateTime paidAt = t["Paid_At"] == DBNull.Value ? day : Convert.ToDateTime(t["Paid_At"]);
                    string txnRef = Convert.ToString(t["Txn_Ref"]);
                    string desc = "รับเงินหน้าร้านผ่านเกตเวย์ " + txnRef
                                + (t["Description"] == DBNull.Value ? "" : " — " + Convert.ToString(t["Description"]));

                    if (post > 0.01m)
                    {
                        shortfall -= post;
                        Record(new ChannelSale
                        {
                            Channel = SalesDocumentPolicy.ChFrontdeskGateway,
                            SourceKey = key,
                            SaleDate = paidAt,
                            PaidHowId = ph,
                            PaidHowName = paidHowName,
                            Amount = post,
                            Description = desc + (post < amt - 0.01m ? " (เฉพาะส่วนที่ไม่มีการบันทึกขาย " + post.ToString("N2") + ")" : ""),
                            CustomerPhone = t["Customer_Phone"] == DBNull.Value ? null : Convert.ToString(t["Customer_Phone"]),
                            CustomerName = t["Customer_Name"] == DBNull.Value ? null : Convert.ToString(t["Customer_Name"]),
                            CreatedBy = "SYSTEM"
                        });
                    }
                    else
                    {
                        // ยอดขาย POS ของวัน/แหล่งเงินนี้ครอบคลุมแล้ว → จับคู่ (ไม่ออกเอกสารซ้ำ)
                        InsertLedgerRow(SalesDocumentPolicy.ChFrontdeskGateway, new ChannelSale
                        {
                            SourceKey = key, SaleDate = paidAt, PaidHowId = ph, PaidHowName = paidHowName,
                            Amount = amt, Description = desc, CreatedBy = "SYSTEM"
                        }, ChannelPosting.DefaultRevenueKey(SalesDocumentPolicy.ChFrontdeskGateway), null, StMatched,
                            "AGG:" + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ":" + ph.ToString(CultureInfo.InvariantCulture),
                            "ยอดขาย POS วันเดียวกัน/แหล่งเงินเดียวกันครอบคลุมแล้ว (POS " + posTotal.ToString("N2")
                            + " / เกตเวย์ " + gatewayTotal.ToString("N2") + ")");
                    }
                    processed++;
                }
            }
            return processed;
        }

        private static decimal NetTxn(DataRow t)
        {
            decimal a = ChannelPosting.Dec(t["Amount"]) - ChannelPosting.Dec(t["Refunded"]);
            return a < 0m ? 0m : a;
        }

        /// <summary>
        /// ยอดขาย POS ของวัน+แหล่งเงิน: ใบเสร็จรายใบ (ออกใบกำกับในระบบ — Reservation_ID 0, Paid_Type = ชื่อแหล่งเงิน)
        /// + Product_Out ที่ขายแบบไม่ออกใบ (Remark ขาย, Account_Receipt_ID 0/ว่าง, Account_Paid_How_ID = แหล่งเงิน)
        /// ไม่นับเอกสารที่ระบบนี้สร้างเอง (FDG-/…DAY-)
        /// </summary>
        private decimal PosSalesTotal(DateTime day, string paidHowName, int paidHowId)
        {
            decimal total = 0m;
            try
            {
                var a = _code.DatabaseQuerySafe(_conn, @"
                    SELECT ISNULL(SUM(Total_Amount), 0) FROM Account_Receipt
                     WHERE CAST(Created_Date AS DATE) = @d AND Paid_Type = @pt
                       AND ISNULL(CAST(Reservation_ID AS NVARCHAR(20)), '0') IN ('0', '')
                       AND (Status = 'Normal' OR Status IS NULL)
                       AND ID NOT LIKE 'FDG%' AND ID NOT LIKE 'ACT%' AND ID NOT LIKE 'AMN%' AND ID NOT LIKE 'DMG%'
                       AND ID NOT LIKE 'RS%' AND ID NOT LIKE 'OTA%' AND ID NOT LIKE 'POSD%' AND ID NOT LIKE 'CHS%'",
                    new Dictionary<string, object> { { "@d", day.Date }, { "@pt", paidHowName } });
                if (a != null && a.Rows.Count > 0) total += ChannelPosting.Dec(a.Rows[0][0]);
            }
            catch (Exception ex) { Log("PosSalesTotal(receipt): " + ex.Message); }

            if (paidHowId > 0)
            {
                try
                {
                    var b = _code.DatabaseQuerySafe(_conn, @"
                        SELECT ISNULL(SUM(CAST(Amount AS DECIMAL(18,4)) * CAST(PricePerUnit AS DECIMAL(18,4))), 0)
                          FROM Product_Out
                         WHERE Remark = N'ขาย'
                           AND (Account_Receipt_ID = '0' OR Account_Receipt_ID IS NULL)
                           AND CAST(Account_Paid_How_ID AS NVARCHAR(20)) = @ph
                           AND CAST(DateTime_Out AS DATE) = @d",
                        new Dictionary<string, object> { { "@d", day.Date }, { "@ph", paidHowId.ToString(CultureInfo.InvariantCulture) } });
                    if (b != null && b.Rows.Count > 0) total += ChannelPosting.Dec(b.Rows[0][0]);
                }
                catch (Exception ex) { Log("PosSalesTotal(product_out): " + ex.Message); }
            }
            return total;
        }

        private decimal FrontdeskPostedTotal(DateTime day, int paidHowId, string paidHowName)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, @"
                    SELECT ISNULL(SUM(Amount), 0) FROM dbo.Sales_Channel_Posting
                     WHERE Sales_Channel = 'FRONTDESK_GATEWAY' AND Sale_Date = @d
                       AND [Status] NOT IN ('MATCHED')
                       AND (ISNULL(Paid_How_ID, 0) = @ph OR (@ph = 0 AND Paid_How_Name = @pn))",
                    new Dictionary<string, object> { { "@d", day.Date }, { "@ph", paidHowId }, { "@pn", paidHowName ?? "" } });
                return dt != null && dt.Rows.Count > 0 ? ChannelPosting.Dec(dt.Rows[0][0]) : 0m;
            }
            catch { return 0m; }
        }

        // ═══════════════════════════════════════════════════════════════════
        // ค่าใช้จ่ายในห้อง (folio) ที่ลูกค้าจ่ายแยกออนไลน์
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// ค่าใช้จ่ายในห้อง (Reservation_Product_Charges) ที่ลูกค้าจ่ายแยกผ่านเกตเวย์ก่อนออกบิลห้อง:
        /// PENDING → ปิดเป็น CANCELLED พร้อมหมายเหตุ "ชำระแยก" (ยอดค้างของการจองไม่นับซ้ำ, ใบเสร็จเช็คเอาท์ไม่ดึงไปเก็บซ้ำ)
        /// แล้วรายได้ไปทางสมุดช่องทาง (Record) ครั้งเดียว.
        /// คืน "PENDING" (ปิดได้), "PAID" (ถูกเก็บในบิลห้องแล้ว — ต้องคืนเงิน), "NONE" (ไม่พบ/ยกเลิกไปแล้ว)
        /// </summary>
        public string SettleFolioChargeSeparately(long chargeId, string note)
        {
            if (chargeId <= 0) return "NONE";
            try
            {
                int n = _code.DatabaseInsertSafe(_conn, @"
                    UPDATE Reservation_Product_Charges
                       SET Status = 'CANCELLED', CancelledDate = GETDATE(),
                           CancelReason = LEFT(@note, 500)
                     WHERE ID = @id AND Status = 'PENDING' AND ISNULL(IsPaid, 0) = 0",
                    new Dictionary<string, object> { { "@id", chargeId }, { "@note", note ?? "ชำระแยกออนไลน์" } });
                if (n > 0) return "PENDING";
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 Status, ISNULL(IsPaid, 0) AS IsPaid FROM Reservation_Product_Charges WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", chargeId } });
                if (dt == null || dt.Rows.Count == 0) return "NONE";
                string st = Convert.ToString(dt.Rows[0]["Status"]);
                bool paid = Convert.ToBoolean(dt.Rows[0]["IsPaid"]);
                return (paid || st == "PAID") ? "PAID" : "NONE";
            }
            catch (Exception ex)
            {
                Log("SettleFolioChargeSeparately #" + chargeId + ": " + ex.Message);
                return "NONE";
            }
        }

        private void Log(string message)
        {
            try { _code.Logs(_conn, "ChannelSales", message, "SYSTEM"); } catch { }
        }
    }

    /// <summary>ตัวช่วยร่วมของการลงรายได้ช่องทาง (ใช้ทั้ง ChannelSalesPostingService และใบสรุปรูมเซอร์วิส)</summary>
    public static class ChannelPosting
    {
        /// <summary>Account_Receipt.ID / Account_Receipt_Detail.Receipt_ID กว้าง 20 ตัว (PHASE9_Migration_02)</summary>
        public const int MaxReceiptIdLength = 20;

        private static readonly code _code = new code();

        // ── นโยบาย (W1: SalesDocumentPolicy) — ห่อกันพังทุกจุด ───────────────────

        public static bool PolicyEnabled(string conn)
        {
            try { return SalesDocumentPolicy.Enabled(conn); }
            catch { return false; }
        }

        /// <summary>โหมดเอกสารของ (ช่องทาง × แหล่งเงิน) — อ่านไม่ได้/ไม่รู้จัก = IMMEDIATE</summary>
        public static void ResolveMode(string conn, string channel, int paidHowId, out string mode, out string reason)
        {
            mode = ChannelSalesPostingService.ModeImmediate;
            reason = null;
            try
            {
                SalesDocRoute route = SalesDocumentPolicy.Resolve(conn, channel, paidHowId);
                if (route != null)
                {
                    reason = route.Reason;
                    if (route.IsOff) mode = ChannelSalesPostingService.ModeOff;
                    else if (route.IsRollup) mode = ChannelSalesPostingService.ModeRollup;
                    else mode = ChannelSalesPostingService.ModeImmediate;
                }
            }
            catch (Exception ex) { reason = "อ่านนโยบายไม่ได้ (" + ex.Message + ") → IMMEDIATE"; }
        }

        // ── โครงสร้างฐานข้อมูล ─────────────────────────────────────────────────

        private static bool? _tableReady;
        private static DateTime _tableCheckedAt = DateTime.MinValue;

        /// <summary>มีตาราง Sales_Channel_Posting (PHASE19_32) — ไม่มีตรวจใหม่ทุก 5 นาที</summary>
        public static bool TableReady(string conn)
        {
            if (_tableReady == true) return true;
            if (_tableReady == false && (DateTime.Now - _tableCheckedAt).TotalMinutes < 5) return false;
            bool ok = false;
            try
            {
                var dt = _code.DatabaseQuerySafe(conn, "SELECT OBJECT_ID('dbo.Sales_Channel_Posting', 'U')", null);
                ok = dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value;
            }
            catch { }
            _tableReady = ok;
            _tableCheckedAt = DateTime.Now;
            return ok;
        }

        private static readonly Dictionary<string, bool> _colCache = new Dictionary<string, bool>();
        private static readonly Dictionary<string, DateTime> _colCheckedAt = new Dictionary<string, DateTime>();

        /// <summary>มีคอลัมน์นี้หรือยัง (มี = cache ถาวร, ไม่มี = ตรวจใหม่ทุก 5 นาที)</summary>
        public static bool HasColumn(string conn, string table, string column)
        {
            string k = table + "." + column;
            lock (_colCache)
            {
                bool v; DateTime at;
                if (_colCache.TryGetValue(k, out v) && (v || (_colCheckedAt.TryGetValue(k, out at) && (DateTime.Now - at).TotalMinutes < 5)))
                    return v;
            }
            bool ok = false;
            try
            {
                var dt = _code.DatabaseQuerySafe(conn, "SELECT COL_LENGTH(@t, @c)",
                    new Dictionary<string, object> { { "@t", "dbo." + table }, { "@c", column } });
                ok = dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value;
            }
            catch { }
            lock (_colCache) { _colCache[k] = ok; _colCheckedAt[k] = DateTime.Now; }
            return ok;
        }

        // ── ค่าตั้ง ────────────────────────────────────────────────────────────

        public static string ReadConfig(string conn, string key, string def)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(conn,
                    "SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = @k AND ConfigValue IS NOT NULL",
                    new Dictionary<string, object> { { "@k", key } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                {
                    string v = Convert.ToString(dt.Rows[0][0]);
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
            }
            catch { }
            return def;
        }

        public static bool ReadBool(string conn, string key, bool def)
        {
            string v = ReadConfig(conn, key, null);
            if (v == null) return def;
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static int ReadInt(string conn, string key, int def)
        {
            int i;
            return int.TryParse(ReadConfig(conn, key, null), out i) ? i : def;
        }

        // ── ช่องทาง ────────────────────────────────────────────────────────────

        /// <summary>คำนำหน้าเลขเอกสาร (สั้นพอให้ {P}DAY-{yyyyMMdd}-{id}-S{n} ไม่เกิน 20 ตัว)</summary>
        public static string PrefixOf(string channel)
        {
            switch ((channel ?? "").ToUpperInvariant())
            {
                case "ROOM_SERVICE": return "RS";
                case "DINING": return "DN";
                case "ACTIVITY": return "ACT";
                case "AMENITY": return "AMN";
                case "FRONTDESK_GATEWAY": return "FDG";
                case "DAMAGE": return "DMG";
                default: return "CHS";
            }
        }

        public static string ChannelThai(string channel)
        {
            switch ((channel ?? "").ToUpperInvariant())
            {
                case "ROOM_SERVICE": return "รูมเซอร์วิส";
                case "DINING": return "ร้านอาหาร (QR สั่งอาหาร)";
                case "ACTIVITY": return "ค่ากิจกรรม";
                case "AMENITY": return "ของใช้ในห้องพัก";
                case "FRONTDESK_GATEWAY": return "รับเงินหน้าร้าน (เกตเวย์)";
                case "DAMAGE": return "ค่าเสียหาย/ของหาย";
                default: return "รายได้อื่น";
            }
        }

        public static string DefaultRevenueKey(string channel)
        {
            switch ((channel ?? "").ToUpperInvariant())
            {
                case "ACTIVITY": return "ACTIVITY_REVENUE";
                case "AMENITY": return "AMENITY_REVENUE";
                case "FRONTDESK_GATEWAY": return "PRODUCT_REVENUE";
                case "DAMAGE": return "DAMAGE_INCOME";
                case "ROOM_SERVICE": return "PRODUCT_REVENUE";
                case "DINING": return "DINING_REVENUE";
                default: return "OTHER_INCOME";
            }
        }

        /// <summary>ลำดับ mapping ที่ลองใช้ (ตัวแรกที่ผูกบัญชี NextAcc จริงชนะ)</summary>
        public static string[] RevenueChain(string channel, string preferred)
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(preferred)) list.Add(preferred.Trim());
            switch ((channel ?? "").ToUpperInvariant())
            {
                case "ACTIVITY": list.AddRange(new[] { "ACTIVITY_REVENUE", "SERVICE_REVENUE", "OTHER_INCOME" }); break;
                case "AMENITY": list.AddRange(new[] { "AMENITY_REVENUE", "PRODUCT_REVENUE" }); break;
                case "FRONTDESK_GATEWAY": list.AddRange(new[] { "PRODUCT_REVENUE", "OTHER_INCOME" }); break;
                case "DAMAGE": list.AddRange(new[] { "DAMAGE_INCOME", "OTHER_INCOME" }); break;
                case "SERVICE_CHARGE": list.AddRange(new[] { "SERVICE_CHARGE_REVENUE", "PRODUCT_REVENUE" }); break;
                case "DINING": list.AddRange(new[] { "DINING_REVENUE", "PRODUCT_REVENUE" }); break;
                default: list.Add("OTHER_INCOME"); break;
            }
            list.Add("ROOM_REVENUE");
            var uniq = new List<string>();
            foreach (string k in list) if (!uniq.Contains(k)) uniq.Add(k);
            return uniq.ToArray();
        }

        /// <summary>
        /// mapping ตัวแรกใน chain ที่ผูกบัญชี NextAcc จริง (GUID หรือรหัสที่มีในผังบัญชีที่ sync มา),
        /// รอบสอง = ตัวแรกที่มีรหัสบัญชี, ไม่มีเลย = ตัวแรก (คิวจะแจ้ง "ไม่พบผังบัญชี" ให้ผู้ดูแลผูก)
        /// </summary>
        public static string ResolveRevenueKey(string conn, string[] chain)
        {
            if (chain == null || chain.Length == 0) return "ROOM_REVENUE";
            string withCode = null;
            foreach (string k in chain)
            {
                try
                {
                    var dt = _code.DatabaseQuerySafe(conn, @"
                        SELECT TOP 1 m.Nexaacc_AccountId, m.Nexaacc_AccountCode,
                               (SELECT TOP 1 a.Nexaacc_AccountId FROM Accounting_Nexaacc_Accounts a
                                 WHERE a.Account_Code = m.Nexaacc_AccountCode) AS CachedId
                          FROM Accounting_Account_Mapping m
                         WHERE m.TakeTime_Code = @c AND ISNULL(m.Is_Active, 1) = 1",
                        new Dictionary<string, object> { { "@c", k } });
                    if (dt == null || dt.Rows.Count == 0) continue;
                    Guid g;
                    string id = dt.Rows[0]["Nexaacc_AccountId"] == DBNull.Value ? null : Convert.ToString(dt.Rows[0]["Nexaacc_AccountId"]);
                    string cached = dt.Rows[0]["CachedId"] == DBNull.Value ? null : Convert.ToString(dt.Rows[0]["CachedId"]);
                    if ((Guid.TryParse(id, out g) && g != Guid.Empty) || (Guid.TryParse(cached, out g) && g != Guid.Empty))
                        return k;
                    string codeV = dt.Rows[0]["Nexaacc_AccountCode"] == DBNull.Value ? null : Convert.ToString(dt.Rows[0]["Nexaacc_AccountCode"]);
                    if (withCode == null && !string.IsNullOrWhiteSpace(codeV)) withCode = k;
                }
                catch { }
            }
            return withCode ?? chain[0];
        }

        /// <summary>ชนิดรายการ (Account_ProductType.ID) ที่ตั้ง Revenue_Mapping_Code = key — ไม่มี = null</summary>
        public static string ProductTypeIdForRevenueKey(string conn, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(conn,
                    "SELECT TOP 1 ID FROM Account_ProductType WHERE Revenue_Mapping_Code = @k ORDER BY ID",
                    new Dictionary<string, object> { { "@k", key } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                    return Convert.ToString(dt.Rows[0][0]);
            }
            catch { }
            return null;
        }

        // ── แหล่งเงิน (Account_Paid_How) ──────────────────────────────────────

        public static int FindPaidHowId(string conn, string paidHowName)
        {
            if (string.IsNullOrWhiteSpace(paidHowName)) return 0;
            try
            {
                var dt = _code.DatabaseQuerySafe(conn,
                    "SELECT TOP 1 ID FROM Account_Paid_How WHERE Paid_How = @n ORDER BY CASE WHEN Status = 'True' THEN 0 ELSE 1 END, ID",
                    new Dictionary<string, object> { { "@n", paidHowName.Trim() } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                    return Convert.ToInt32(dt.Rows[0][0]);
            }
            catch { }
            return 0;
        }

        public static string FindPaidHowName(string conn, int paidHowId)
        {
            if (paidHowId <= 0) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(conn, "SELECT TOP 1 Paid_How FROM Account_Paid_How WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", paidHowId } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                    return Convert.ToString(dt.Rows[0][0]);
            }
            catch { }
            return null;
        }

        /// <summary>GUID บัญชี NextAcc ของแหล่งเงินตาม ID — ไม่ผูก/อ่านไม่ได้ = null</summary>
        public static string PaidHowAccountId(string conn, int paidHowId)
        {
            if (paidHowId <= 0) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(conn, "SELECT TOP 1 Nexaacc_AccountId FROM Account_Paid_How WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", paidHowId } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                {
                    string v = Convert.ToString(dt.Rows[0][0]);
                    Guid g;
                    if (Guid.TryParse(v, out g) && g != Guid.Empty) return v;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// แหล่งเงินตามชนิดช่องทาง (Channel_Type = CASH / TRANSFER …, PHASE19_20) — ไม่มีคอลัมน์/ไม่พบ = หาตามชื่อสำรอง
        /// </summary>
        public static int PaidHowByType(string conn, string channelType, string fallbackName, out string name)
        {
            name = null;
            try
            {
                if (HasColumn(conn, "Account_Paid_How", "Channel_Type"))
                {
                    var dt = _code.DatabaseQuerySafe(conn,
                        "SELECT TOP 1 ID, Paid_How FROM Account_Paid_How WHERE Status = 'True' AND Channel_Type = @t ORDER BY ID",
                        new Dictionary<string, object> { { "@t", channelType } });
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        name = Convert.ToString(dt.Rows[0]["Paid_How"]);
                        return Convert.ToInt32(dt.Rows[0]["ID"]);
                    }
                }
            }
            catch { }
            int id = FindPaidHowId(conn, fallbackName);
            name = id > 0 ? FindPaidHowName(conn, id) : fallbackName;
            return id;
        }

        // ── เอกสาร (Account_Receipt) ───────────────────────────────────────────

        public static bool ReceiptExists(string conn, string receiptId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(conn, "SELECT TOP 1 1 FROM Account_Receipt WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", receiptId } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// เลขใบสรุปที่ยังว่าง: {stem}-{yyyyMMdd}-{key} → -S1 … -S99 (เกิน 20 ตัว → รูปย่อ {stem}{yyMMdd}-{key}[-S{n}]).
        /// ว่าง = ไม่มี Account_Receipt และ extraUsed(ref) = false
        /// </summary>
        public static string NextFreeRollupRef(string conn, string stem, DateTime day, string key, Func<string, bool> extraUsed)
        {
            string full = stem + "-" + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + key;
            string compact = stem + day.ToString("yyMMdd", CultureInfo.InvariantCulture) + "-" + key;
            for (int n = 0; n < 100; n++)
            {
                string sfx = n == 0 ? "" : "-S" + n.ToString(CultureInfo.InvariantCulture);
                string cand = full + sfx;
                if (cand.Length > MaxReceiptIdLength) cand = compact + sfx;
                if (cand.Length > MaxReceiptIdLength) return null;
                if (ReceiptExists(conn, cand)) continue;
                if (extraUsed != null && extraUsed(cand)) continue;
                return cand;
            }
            return null;
        }

        private static bool BusinessUsesVat(string conn)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(conn, "SELECT TOP 1 Use_Vat FROM Business_Info", null);
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0]["Use_Vat"] != DBNull.Value)
                {
                    string v = Convert.ToString(dt.Rows[0]["Use_Vat"]);
                    return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// สร้างแถว Account_Receipt + บรรทัด (IF NOT EXISTS — เรียกซ้ำได้) โครงเดียวกับใบสรุปรายวันของ POS/รูมเซอร์วิส.
        /// VAT คิดจากยอดรวมทุกบรรทัด (รวมค่าบริการ — ฐานภาษีรวม service charge). บรรทัด ProductTypeId ว่าง =
        /// ProductType_ID NULL ⇒ ตัวประมวลผลใช้ revenueType ของใบ (LookupReceiptLinesEx) — ฐานที่บังคับ NOT NULL
        /// จะถอยไปชนิดของ PRODUCT_REVENUE/“3”
        /// </summary>
        public static void WriteSummaryReceipt(string conn, string receiptId, DateTime docDate, string paidTypeName,
            List<ChannelReceiptLine> lines)
        {
            if (string.IsNullOrEmpty(receiptId) || lines == null || lines.Count == 0) return;
            decimal docTotal = 0m;
            foreach (var l in lines) docTotal += l.Amount;
            bool useVat = BusinessUsesVat(conn);
            decimal exVat = useVat ? Math.Round(docTotal / 1.07m, 2, MidpointRounding.AwayFromZero) : docTotal;
            decimal vat = useVat ? (docTotal - exVat) : 0m;

            _code.DatabaseInsertSafe(conn,
                "IF NOT EXISTS (SELECT 1 FROM [dbo].[Account_Receipt] WHERE [ID] = @ID) " +
                "INSERT INTO [dbo].[Account_Receipt] " +
                "([ID],[Reservation_ID],[Created_Date],[Total_Amount],[Vat],[Total_Amount_Exclude_Vat]," +
                "[IsDeposit],[UseDeposit],[Paid_Type],[Status],[Created_By_ID],[Etax],[Customer_ID]) " +
                "VALUES (@ID,'0',@CreatedDate,@TotalAmount,@Vat,@ExVat,0,0,@PaidType,'Normal',0,0,0)",
                new Dictionary<string, object>
                {
                    { "@ID", receiptId }, { "@CreatedDate", docDate.Date }, { "@TotalAmount", docTotal },
                    { "@Vat", vat }, { "@ExVat", exVat }, { "@PaidType", paidTypeName ?? "เงินสด" }
                });

            string fallbackType = null;
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (l.Amount == 0m) continue;
                decimal qty = l.Quantity > 0m ? l.Quantity : 1m;
                var p = new Dictionary<string, object>
                {
                    { "@N", i + 1 }, { "@R", receiptId },
                    { "@T", string.IsNullOrEmpty(l.ProductTypeId) ? (object)DBNull.Value : l.ProductTypeId },
                    { "@D", Trunc(l.Text, 400) ?? "-" }, { "@Q", qty }, { "@U", l.Unit ?? "ครั้ง" },
                    { "@P", Math.Round(l.Amount / qty, 2, MidpointRounding.AwayFromZero) }, { "@A", l.Amount }
                };
                const string sql =
                    "IF NOT EXISTS (SELECT 1 FROM [dbo].[Account_Receipt_Detail] WHERE [Receipt_ID] = @R AND [Number] = @N) " +
                    "INSERT INTO [dbo].[Account_Receipt_Detail] " +
                    "([Number],[Receipt_ID],[ProductType_ID],[Product_ID],[Product_Data],[Product_Amount]," +
                    "[Product_Unit],[Price_PerPeice],[Price_Amount]) " +
                    "VALUES (@N,@R,@T,'0',@D,@Q,@U,@P,@A)";
                try
                {
                    _code.DatabaseInsertSafe(conn, sql, p);
                }
                catch (SqlException) when (string.IsNullOrEmpty(l.ProductTypeId))
                {
                    // ฐานที่ ProductType_ID ห้าม NULL → ใช้ชนิดของรายได้ขายสินค้า (หรือ 3 = สินค้า แบบเดิม)
                    if (fallbackType == null) fallbackType = ProductTypeIdForRevenueKey(conn, "PRODUCT_REVENUE") ?? "3";
                    p["@T"] = fallbackType;
                    _code.DatabaseInsertSafe(conn, sql, p);
                }
            }
        }

        // ── misc ──────────────────────────────────────────────────────────────

        public static decimal Dec(object o)
        {
            if (o == null || o == DBNull.Value) return 0m;
            try { return Convert.ToDecimal(o, CultureInfo.InvariantCulture); }
            catch
            {
                decimal d;
                return decimal.TryParse(Convert.ToString(o), out d) ? d : 0m;
            }
        }

        public static string Trunc(string s, int max)
        {
            if (s == null) return null;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
