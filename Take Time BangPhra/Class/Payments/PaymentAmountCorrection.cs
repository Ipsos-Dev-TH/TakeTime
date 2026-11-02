using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using Take_Time_BangPhra.Integration;
using Take_Time_BangPhra.Payments.SlipCheck;

namespace Take_Time_BangPhra.Payments
{
    /// <summary>รายการรับเงินหนึ่งใบของการจอง พร้อมยอดบนสลิปที่ระบบอ่านได้ (ถ้ามี)</summary>
    public sealed class PaymentAmountRow
    {
        public string ReceiptId;
        public int ReservationId;
        public decimal Recorded;            // Payment_History.PaymentAmount (= Account_Receipt.Total_Amount)
        public string PaymentType;          // DEPOSIT / FULL / ...
        public string PaymentMethod;
        public DateTime? PaymentDate;
        public bool IsDeposit;
        public string ReceiptStatus;
        public long SlipId;                 // 0 = ไม่มีสลิป
        public string SlipUrl;
        /// <summary>ยอดบนสลิปที่เชื่อได้ (SlipOK = ธนาคาร · OCR ≥ เกณฑ์ล็อก) — null = ยังไม่รู้</summary>
        public decimal? SlipAmount;
        public string SlipSource;           // SLIPOK / LOCAL_OCR
        public bool Overridden;             // พนักงานยืนยันข้ามแล้ว (Override_At)
        public bool Mismatch => SlipAmount.HasValue && !Overridden && Math.Abs(SlipAmount.Value - Recorded) > 1.00m;
    }

    public sealed class PaymentAmountFixResult
    {
        public bool Success;
        public string Message;
        public decimal OldAmount, NewAmount;
        public string SyncMessage;
    }

    /// <summary>
    /// แก้ "ยอดที่บันทึก" ของการรับเงินที่ผิดจากสลิปจริง (เคสลูกค้ากรอกมัดจำ 3,200 แต่โอน 2,000 ก่อนมีระบบล็อกยอดตามสลิป) —
    /// ที่เดียวที่แก้ครบทุกจุด: Account_Receipt (ยอด/VAT ตามสัดส่วน) + บรรทัดใบ + Payment_History + Reservation.Deposit (ส่วนต่าง)
    /// + Account_Receipt_Edit_Log (เหตุผลบังคับ) + ส่ง NextAcc ด้วยเส้นเดิมของการแก้ใบ (<see cref="AccountingSyncService.ResyncSingleReceipt"/>:
    /// in-place ก่อน · ไม่ได้ = void→สร้างใหม่ · ยังไม่เคยส่ง = เข้าคิวตามปกติ).
    /// <para>หน้าแก้ไขการจองปิดช่องยอด (TextBox5.Enabled=false) และหน้าใบเสร็จแก้ยอดได้แต่ไม่ปรับ Reservation.Deposit ⇒ เดิมแอดมินแก้ให้ถูกไม่ได้เลย.</para>
    /// <para>ป้องกัน/ตรวจจับ: <see cref="ListForReservation"/> เทียบยอดที่บันทึกกับยอดบนสลิป (Payment_Slips.Slip_Amount จาก SlipOK, หรือ OCR_Amount
    /// ที่ความมั่นใจ ≥ เกณฑ์ล็อก) → เตือนบนหน้าแก้ไข/เช็คอิน · <see cref="ScanMismatches"/> ให้ตัวตรวจรายคืน (SLIP_AMOUNT_MISMATCH) · สลิปเก่าที่ยังไม่เคยตรวจ
    /// → <see cref="ReadSlipAmount"/> อ่านจากไฟล์ที่เก็บไว้.</para>
    /// </summary>
    public static class PaymentAmountCorrection
    {
        public const decimal Tolerance = 1.00m;

        /// <summary>ยอดที่ถือว่าตรง (±1 บาท — เกณฑ์เดียวกับ SlipVerify_Amount_Tolerance เริ่มต้น)</summary>
        public static bool Differs(decimal a, decimal b) => Math.Abs(a - b) > Tolerance;

        // ──────────────────────────────────────────────────────────────
        // อ่าน
        // ──────────────────────────────────────────────────────────────

        /// <summary>รายการรับเงิน (มีใบเสร็จ) ของการจอง + ยอดสลิปที่เชื่อได้ — ไม่มีคอลัมน์ตรวจสลิป (ยังไม่รัน PHASE19_34) = ไม่มียอดสลิป</summary>
        public static List<PaymentAmountRow> ListForReservation(string conn, int reservationId)
        {
            var list = new List<PaymentAmountRow>();
            if (reservationId <= 0) return list;
            bool slipCols = SlipVerificationService.SchemaReady;
            string slipSel = slipCols
                ? "ps.Slip_Amount, ps.OCR_Amount, ps.OCR_Confidence, ps.Amount_Locked, ps.Lock_Confidence, ps.Verify_Source, ps.Override_At"
                : "CAST(NULL AS DECIMAL(18,2)) AS Slip_Amount, CAST(NULL AS DECIMAL(18,2)) AS OCR_Amount, CAST(NULL AS INT) AS OCR_Confidence, CAST(NULL AS BIT) AS Amount_Locked, CAST(NULL AS INT) AS Lock_Confidence, CAST(NULL AS NVARCHAR(20)) AS Verify_Source, CAST(NULL AS DATETIME) AS Override_At";
            var dt = new code().DatabaseQuerySafe(conn, @"
                SELECT ph.Receipt_ID, ph.PaymentAmount, ph.PaymentType, ph.PaymentMethod, ph.PaymentDate,
                       ISNULL(ar.IsDeposit, 0) AS IsDeposit, ISNULL(ar.Status, 'Normal') AS RStatus,
                       ps.ID AS SlipId, ps.SlipFileURL, " + slipSel + @"
                FROM Payment_History ph
                INNER JOIN Account_Receipt ar ON ar.ID = ph.Receipt_ID
                OUTER APPLY (SELECT TOP 1 * FROM Payment_Slips s
                             WHERE (s.ID = ph.PaymentSlip_ID OR (ph.PaymentSlip_ID IS NULL AND s.Account_Receipt_ID = ph.Receipt_ID))
                               AND ISNULL(s.IsActive, 1) = 1
                             ORDER BY CASE WHEN s.ID = ph.PaymentSlip_ID THEN 0 ELSE 1 END, s.ID DESC) ps
                WHERE ph.Reservation_ID = @r AND ph.Status = 'COMPLETED' AND ph.Receipt_ID IS NOT NULL AND ph.PaymentAmount > 0
                ORDER BY ph.PaymentDate, ph.ID",
                new Dictionary<string, object> { { "@r", reservationId } });
            if (dt == null) return list;
            int minConf = SlipVerificationService.LockMinConfidence;
            foreach (DataRow r in dt.Rows)
            {
                var row = new PaymentAmountRow
                {
                    ReceiptId = Convert.ToString(r["Receipt_ID"]),
                    ReservationId = reservationId,
                    Recorded = Dec(r["PaymentAmount"]),
                    PaymentType = Convert.ToString(r["PaymentType"]),
                    PaymentMethod = Convert.ToString(r["PaymentMethod"]),
                    PaymentDate = r["PaymentDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["PaymentDate"]),
                    IsDeposit = r["IsDeposit"] != DBNull.Value && Convert.ToBoolean(r["IsDeposit"]),
                    ReceiptStatus = Convert.ToString(r["RStatus"]),
                    SlipId = r["SlipId"] == DBNull.Value ? 0 : Convert.ToInt64(r["SlipId"]),
                    SlipUrl = r["SlipFileURL"] == DBNull.Value ? null : Convert.ToString(r["SlipFileURL"]),
                    Overridden = r["Override_At"] != DBNull.Value
                };
                // ยอดสลิปที่เชื่อได้: ธนาคาร (SlipOK) ก่อน · OCR เฉพาะเมื่อ ≥ เกณฑ์ล็อก (ผลอ่านไม่ใช่ข้อมูลธนาคาร)
                if (r["Slip_Amount"] != DBNull.Value && Dec(r["Slip_Amount"]) > 0m)
                {
                    row.SlipAmount = Dec(r["Slip_Amount"]); row.SlipSource = SlipVerifySource.SlipOk;
                }
                else if (r["OCR_Amount"] != DBNull.Value && Dec(r["OCR_Amount"]) > 0m
                         && r["OCR_Confidence"] != DBNull.Value && Convert.ToInt32(r["OCR_Confidence"]) >= minConf)
                {
                    row.SlipAmount = Dec(r["OCR_Amount"]); row.SlipSource = SlipVerifySource.LocalOcr;
                }
                list.Add(row);
            }
            return list;
        }

        /// <summary>
        /// อ่านยอดจากไฟล์สลิปที่เก็บไว้ (สลิปก่อนมีระบบตรวจ) ด้วยตัวอ่านเดียวกับหน้าจอง (<see cref="SlipVerificationService.InspectAmountLockFile"/>:
        /// SlipOK → OCR) แล้วบันทึกลง Payment_Slips (Slip_Amount เมื่อธนาคารยืนยัน / OCR_Amount+OCR_Confidence) — ล็อกไม่ได้ = คืน null + เหตุผล
        /// </summary>
        public static decimal? ReadSlipAmount(string conn, long slipId, out string note)
        {
            note = "";
            if (slipId <= 0) { note = "ไม่มีสลิปผูกกับรายการนี้"; return null; }
            string url = null;
            var dt = new code().DatabaseQuerySafe(conn, "SELECT SlipFileURL FROM Payment_Slips WHERE ID = @id", new Dictionary<string, object> { { "@id", slipId } });
            if (dt != null && dt.Rows.Count > 0) url = Convert.ToString(dt.Rows[0]["SlipFileURL"]);
            if (string.IsNullOrWhiteSpace(url)) { note = "ไม่พบไฟล์สลิป"; return null; }
            if (!SlipVerificationService.AmountLockActive) { note = "ระบบตรวจสลิปยังไม่เปิด (SlipVerify_Enabled / PHASE19_34)"; return null; }
            SlipAmountLock lk;
            try
            {
                // ลำดับเดียวกับหน้าจอง: SlipOK (ธนาคาร) ก่อน → ไม่ได้ข้อมูล/โควต้าหมด/ไม่ตั้งค่า → OCR ในเครื่อง (SlipVerificationService.Inspect)
                string path = SlipVerificationService.MapSlipPath(url);
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) { note = "ไม่พบไฟล์สลิปบนเซิร์ฟเวอร์ (" + url + ")"; return null; }
                var insp = SlipVerificationService.Inspect(System.IO.File.ReadAllBytes(path), System.IO.Path.GetFileName(path), false);
                // สลิปเก่าที่เคยส่ง SlipOK แล้ว → 1012 "เคยตรวจแล้ว" แต่ยังคืนข้อมูลธนาคารมา — สำหรับ "อ่านยอด" ถือเป็นยอดธนาคารได้
                // (ตัวล็อกยอดหน้าจองไม่ล็อกเคสนี้เพราะใช้ตรวจสลิปซ้ำ — คนละวัตถุประสงค์)
                var v = insp.Vendor;
                if (v != null && v.HasData && v.Amount.HasValue && v.Amount.Value > 0m)
                    lk = new SlipAmountLock { Active = true, Locked = true, Amount = v.Amount, Source = SlipVerifySource.SlipOk, Confidence = 100,
                        Reason = v.Kind == SlipVendorResponse.KindDuplicate ? "ธนาคารยืนยัน (SlipOK เคยตรวจสลิปนี้แล้ว)" : "ธนาคารยืนยันยอดผ่าน SlipOK" };
                else
                    lk = SlipVerificationService.GetAmountLock(insp);
                if (!string.IsNullOrEmpty(insp.VendorSkipReason)) Log(conn, "ReadSlipAmount slip #" + slipId + " ข้าม SlipOK: " + insp.VendorSkipReason);
            }
            catch (Exception ex) { note = "อ่านสลิปไม่ได้: " + ex.Message; return null; }
            if (!lk.Locked || !lk.Amount.HasValue)
            {
                note = "อ่านยอดจากสลิปไม่มั่นใจพอ" + (lk.HintAmount.HasValue ? " (ค่าที่เดา " + lk.HintAmount.Value.ToString("N2") + ")" : "")
                     + (string.IsNullOrEmpty(lk.Reason) ? "" : " — " + lk.Reason);
                return null;
            }
            // บันทึกผลไว้ที่สลิป (คอลัมน์ PHASE19_34/36) ให้หน้าอื่น/ตัวตรวจรายคืนเห็นด้วย
            try
            {
                if (SlipVerificationService.SchemaReady)
                {
                    bool bank = lk.Source == SlipVerifySource.SlipOk;
                    new code().DatabaseInsertSafe(conn, bank
                        ? "UPDATE Payment_Slips SET Slip_Amount = @a, Verify_Source = @s, Verify_At = ISNULL(Verify_At, GETDATE()) WHERE ID = @id AND Slip_Amount IS NULL"
                        : "UPDATE Payment_Slips SET OCR_Amount = @a, OCR_Confidence = @c, Verify_Source = ISNULL(Verify_Source, @s) WHERE ID = @id AND (OCR_Amount IS NULL OR ISNULL(OCR_Confidence,0) < @c)",
                        new Dictionary<string, object> { { "@a", lk.Amount.Value }, { "@c", lk.Confidence }, { "@s", lk.Source ?? "" }, { "@id", slipId } });
                }
            }
            catch (Exception ex) { Log(conn, "ReadSlipAmount save slip #" + slipId + ": " + ex.Message); }
            note = (lk.Source == SlipVerifySource.SlipOk ? lk.Reason : "OCR ในเครื่อง ความมั่นใจ " + lk.Confidence + "% (SlipOK ไม่ได้ข้อมูล)") + " · ยอด " + lk.Amount.Value.ToString("N2");
            return lk.Amount;
        }

        // ──────────────────────────────────────────────────────────────
        // แก้ยอด
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// แก้ยอดรับเงินของใบเสร็จ <paramref name="receiptId"/> เป็น <paramref name="newAmount"/> (เหตุผล ≥ 10 ตัว). ใช้กับใบที่มีบรรทัดบวกบรรทัดเดียว
        /// ไม่มีบรรทัดติดลบ (ใบมัดจำ/รับชำระทั่วไป) — ใบหลายบรรทัดให้แก้ที่หน้าใบเสร็จ. ปฏิเสธเมื่อใบถูกยกเลิก/มีใบลดหนี้/e-Tax ถึงกรมสรรพากร/ออกใบแทนแล้ว
    /// (<see cref="CreditNoteGuards.VoidBlockReason"/>).
        /// </summary>
        public static PaymentAmountFixResult Correct(string conn, string receiptId, decimal newAmount, string reason, int adminId)
        {
            var res = new PaymentAmountFixResult { NewAmount = newAmount };
            receiptId = (receiptId ?? "").Trim();
            reason = (reason ?? "").Trim();
            if (receiptId.Length == 0) return Fail(res, "ไม่ระบุเลขที่ใบเสร็จ");
            if (newAmount <= 0m) return Fail(res, "ยอดใหม่ต้องมากกว่า 0");
            if (reason.Length < 10) return Fail(res, "กรุณาระบุเหตุผลอย่างน้อย 10 ตัวอักษร (เก็บในประวัติการแก้ใบ)");

            var c = new code();
            var dt = c.DatabaseQuerySafe(conn, @"
                SELECT ar.ID, ar.UID, ar.Reservation_ID, ar.Total_Amount, ISNULL(ar.Vat,0) AS Vat, ISNULL(ar.Total_Amount_Exclude_Vat, ar.Total_Amount) AS Ex,
                       ISNULL(ar.IsDeposit,0) AS IsDeposit, ISNULL(ar.Status,'Normal') AS Status,
                       (SELECT COUNT(*) FROM Account_Receipt_Detail d WHERE d.Receipt_ID = ar.ID AND ISNULL(d.Price_Amount,0) > 0) AS PosLines,
                       (SELECT COUNT(*) FROM Account_Receipt_Detail d WHERE d.Receipt_ID = ar.ID AND ISNULL(d.Price_Amount,0) < 0) AS NegLines,
                       (SELECT COUNT(*) FROM Payment_History ph WHERE ph.Receipt_ID = ar.ID AND ph.Status = 'COMPLETED') AS Ledger
                FROM Account_Receipt ar WHERE ar.ID = @id",
                new Dictionary<string, object> { { "@id", receiptId } });
            if (dt == null || dt.Rows.Count == 0) return Fail(res, "ไม่พบใบเสร็จ " + receiptId);
            var r = dt.Rows[0];
            string status = Convert.ToString(r["Status"]);
            if (!string.Equals(status, "Normal", StringComparison.OrdinalIgnoreCase)) return Fail(res, "ใบ " + receiptId + " สถานะ " + status + " — แก้ยอดไม่ได้");
            decimal old = Dec(r["Total_Amount"]), oldVat = Dec(r["Vat"]);
            int resId = r["Reservation_ID"] == DBNull.Value ? 0 : Convert.ToInt32(r["Reservation_ID"]);
            res.OldAmount = old;
            if (!Differs(old, newAmount) && old == newAmount) return Fail(res, "ยอดใหม่เท่ายอดเดิม (" + old.ToString("N2") + ") ไม่มีอะไรต้องแก้");
            if (Convert.ToInt32(r["PosLines"]) != 1 || Convert.ToInt32(r["NegLines"]) != 0)
                return Fail(res, "ใบ " + receiptId + " มีหลายบรรทัด/มีบรรทัดหักมัดจำ — แก้ที่หน้าใบเสร็จ (Account/Receipt?command=edit&id=" + receiptId + ") แทน");
            string block = CreditNoteGuards.VoidBlockReason(conn, receiptId);
            if (!string.IsNullOrEmpty(block)) return Fail(res, block);

            // VAT ตามสัดส่วนเดิม (ใบมัดจำโหมด CHECKOUT = 0) · ยอดก่อน VAT = ยอด − VAT
            decimal newVat = oldVat > 0m && old > 0m ? Math.Round(newAmount * oldVat / old, 2, MidpointRounding.AwayFromZero) : 0m;
            decimal newEx = newAmount - newVat;
            decimal delta = newAmount - old;
            bool hasLedger = Convert.ToInt32(r["Ledger"]) > 0;

            string before = Json(receiptId, old, oldVat, Dec(r["Ex"]));
            string after = Json(receiptId, newAmount, newVat, newEx);

            using (var cn = new SqlConnection(conn))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        Exec(cn, tx, "UPDATE Account_Receipt SET Total_Amount = @t, Vat = @v, Total_Amount_Exclude_Vat = @e WHERE ID = @id",
                            new Dictionary<string, object> { { "@t", newAmount }, { "@v", newVat }, { "@e", newEx }, { "@id", receiptId } });
                        // บรรทัดเดียว: ยอดบรรทัด = ยอดใหม่ · ราคา/หน่วย = ยอด ÷ จำนวน (จำนวน 0/ว่าง = 1)
                        Exec(cn, tx, @"UPDATE Account_Receipt_Detail
                                       SET Price_Amount = @t,
                                           Price_PerPeice = CASE WHEN ISNULL(Product_Amount,0) > 0 THEN ROUND(@t / Product_Amount, 2) ELSE @t END
                                       WHERE Receipt_ID = @id AND ISNULL(Price_Amount,0) > 0",
                            new Dictionary<string, object> { { "@t", newAmount }, { "@id", receiptId } });
                        Exec(cn, tx, @"UPDATE Payment_History
                                       SET PaymentAmount = @t, UpdatedDate = GETDATE(),
                                           Notes = LEFT(ISNULL(Notes,'') + CASE WHEN ISNULL(Notes,'') = '' THEN '' ELSE ' | ' END
                                                   + N'[แก้ยอด ' + CONVERT(NVARCHAR(10), @old) + N'→' + CONVERT(NVARCHAR(10), @t) + N': ' + @why + N']', 500)
                                       WHERE Receipt_ID = @id AND Status = 'COMPLETED'",
                            new Dictionary<string, object> { { "@t", newAmount }, { "@old", old }, { "@why", reason }, { "@id", receiptId } });
                        // Reservation.Deposit = ยอดรับสะสม → ปรับส่วนต่าง (ไม่ต่ำกว่า 0) — เฉพาะใบที่อยู่ในสมุดรับเงิน
                        if (hasLedger && resId > 0)
                            Exec(cn, tx, @"UPDATE Reservation SET Deposit = CASE WHEN ISNULL(Deposit,0) + @d < 0 THEN 0 ELSE ISNULL(Deposit,0) + @d END WHERE ID = @r",
                                new Dictionary<string, object> { { "@d", delta }, { "@r", resId } });
                        // สลิปที่ผูก: ยอดที่ลูกค้าแจ้ง = ยอดใหม่ (กันคิวตรวจสลิปฟ้องซ้ำ)
                        if (SlipVerificationService.SchemaReady)
                            Exec(cn, tx, "UPDATE Payment_Slips SET Claimed_Amount = @t WHERE Account_Receipt_ID = @id",
                                new Dictionary<string, object> { { "@t", newAmount }, { "@id", receiptId } });
                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(); } catch { }
                        Log(conn, "Correct " + receiptId + " failed: " + ex.Message);
                        return Fail(res, "บันทึกไม่สำเร็จ: " + ex.Message);
                    }
                }
            }

            // NextAcc: เส้นเดียวกับแก้ใบ (in-place → void→สร้างใหม่ → ยังไม่เคยส่ง = เข้าคิว)
            string syncAction = "NO_SYNC"; long qid = 0;
            try
            {
                var sync = new AccountingSyncService(conn);
                var rs = sync.ResyncSingleReceipt(receiptId);
                res.SyncMessage = rs.message;
                syncAction = rs.kind; qid = rs.result > 0 ? rs.result : 0;
            }
            catch (Exception ex) { res.SyncMessage = "ส่ง NextAcc ไม่สำเร็จ: " + ex.Message; }

            try
            {
                ReceiptPreflight.WriteEditLog(conn, receiptId, null, resId, "AMOUNT_FIX", reason, before, after, null, syncAction, qid);
            }
            catch (Exception ex) { Log(conn, "WriteEditLog " + receiptId + ": " + ex.Message); }
            Log(conn, $"แก้ยอดใบ {receiptId} การจอง {resId}: {old:N2} → {newAmount:N2} (VAT {oldVat:N2} → {newVat:N2}) เหตุผล: {reason} · NextAcc: {res.SyncMessage}");

            res.Success = true;
            res.Message = $"แก้ยอดใบ {receiptId} จาก {old:N2} เป็น {newAmount:N2} แล้ว"
                + (hasLedger && resId > 0 ? $" · มัดจำ/ยอดรับของการจองปรับ {(delta >= 0 ? "+" : "")}{delta:N2}" : "")
                + " · PDF ใบเสร็จในระบบยังเป็นยอดเดิม — ดู PDF จาก NextAcc หรือเปิดหน้าใบเสร็จแล้วบันทึกใหม่เพื่อพิมพ์";
            return res;
        }

        /// <summary>
        /// หน้าใบเสร็จแก้ยอดใบที่อยู่ในสมุดรับเงิน → ปรับ Reservation.Deposit ตามส่วนต่าง (เดิมหน้าใบเสร็จอัปเดต Payment_History แต่ไม่แตะ Deposit ⇒ ยอดค้างบนบอร์ด/เช็คอินผิด)
        /// </summary>
        public static void ApplyDepositDelta(string conn, int reservationId, string receiptId, decimal oldTotal, decimal newTotal)
        {
            if (reservationId <= 0 || Math.Abs(newTotal - oldTotal) < 0.005m) return;
            try
            {
                var c = new code();
                var dt = c.DatabaseQuerySafe(conn, "SELECT COUNT(*) AS N FROM Payment_History WHERE Receipt_ID = @id AND Status = 'COMPLETED'",
                    new Dictionary<string, object> { { "@id", receiptId } });
                if (dt == null || dt.Rows.Count == 0 || Convert.ToInt32(dt.Rows[0]["N"]) == 0) return;
                c.DatabaseInsertSafe(conn, "UPDATE Reservation SET Deposit = CASE WHEN ISNULL(Deposit,0) + @d < 0 THEN 0 ELSE ISNULL(Deposit,0) + @d END WHERE ID = @r",
                    new Dictionary<string, object> { { "@d", newTotal - oldTotal }, { "@r", reservationId } });
                Log(conn, $"ApplyDepositDelta การจอง {reservationId} ใบ {receiptId}: {oldTotal:N2} → {newTotal:N2}");
            }
            catch (Exception ex) { Log(conn, "ApplyDepositDelta " + receiptId + ": " + ex.Message); }
        }

        // ──────────────────────────────────────────────────────────────
        // ตรวจจับ (ตัวตรวจรายคืน)
        // ──────────────────────────────────────────────────────────────

        public sealed class Mismatch
        {
            public int ReservationId; public string ReceiptId; public long SlipId; public decimal Recorded, SlipAmount; public string Source;
        }

        /// <summary>รายการรับเงิน <paramref name="days"/> วันล่าสุดที่ยอดที่บันทึก ≠ ยอดบนสลิปที่เชื่อได้ และยังไม่มีการยืนยันข้าม — ไม่มีคอลัมน์ตรวจสลิป = ว่าง</summary>
        public static List<Mismatch> ScanMismatches(string conn, int days, int max)
        {
            var list = new List<Mismatch>();
            if (!SlipVerificationService.SchemaReady) return list;
            int minConf = SlipVerificationService.LockMinConfidence;
            var dt = new code().DatabaseQuerySafe(conn, @"
                SELECT TOP (@max) ph.Reservation_ID, ph.Receipt_ID, ps.ID AS SlipId, ph.PaymentAmount,
                       CASE WHEN ISNULL(ps.Slip_Amount,0) > 0 THEN ps.Slip_Amount ELSE ps.OCR_Amount END AS SlipAmt,
                       CASE WHEN ISNULL(ps.Slip_Amount,0) > 0 THEN 'SLIPOK' ELSE 'LOCAL_OCR' END AS Src
                FROM Payment_History ph
                INNER JOIN Account_Receipt ar ON ar.ID = ph.Receipt_ID AND ISNULL(ar.Status,'Normal') = 'Normal'
                INNER JOIN Payment_Slips ps ON (ps.ID = ph.PaymentSlip_ID OR (ph.PaymentSlip_ID IS NULL AND ps.Account_Receipt_ID = ph.Receipt_ID))
                WHERE ph.Status = 'COMPLETED' AND ph.PaymentAmount > 0 AND ph.PaymentDate >= DATEADD(DAY, -@days, GETDATE())
                  AND ps.Override_At IS NULL AND ISNULL(ps.IsActive, 1) = 1
                  AND ((ISNULL(ps.Slip_Amount,0) > 0 AND ABS(ps.Slip_Amount - ph.PaymentAmount) > @tol)
                    OR (ISNULL(ps.Slip_Amount,0) = 0 AND ISNULL(ps.OCR_Amount,0) > 0 AND ISNULL(ps.OCR_Confidence,0) >= @conf
                        AND ABS(ps.OCR_Amount - ph.PaymentAmount) > @tol))
                ORDER BY ph.PaymentDate DESC",
                new Dictionary<string, object> { { "@max", max }, { "@days", days }, { "@tol", Tolerance }, { "@conf", minConf } });
            if (dt == null) return list;
            foreach (DataRow r in dt.Rows)
                list.Add(new Mismatch
                {
                    ReservationId = r["Reservation_ID"] == DBNull.Value ? 0 : Convert.ToInt32(r["Reservation_ID"]),
                    ReceiptId = Convert.ToString(r["Receipt_ID"]), SlipId = Convert.ToInt64(r["SlipId"]),
                    Recorded = Dec(r["PaymentAmount"]), SlipAmount = Dec(r["SlipAmt"]), Source = Convert.ToString(r["Src"])
                });
            return list;
        }

        /// <summary>ข้อความเตือนภาษาไทยของแถวที่ยอดไม่ตรงสลิป (encode แล้ว)</summary>
        public static string MismatchHtml(PaymentAmountRow row)
        {
            if (row == null || !row.Mismatch) return "";
            string src = row.SlipSource == SlipVerifySource.SlipOk ? "ธนาคารยืนยัน" : "อ่านจากรูป (OCR)";
            return "⚠ ใบ " + H(row.ReceiptId) + ": บันทึก <b>" + row.Recorded.ToString("N2") + "</b> แต่สลิป <b>" + row.SlipAmount.Value.ToString("N2")
                 + "</b> (" + src + ") ต่าง " + (row.Recorded - row.SlipAmount.Value).ToString("N2");
        }

        // ──────────────────────────────────────────────────────────────

        private static PaymentAmountFixResult Fail(PaymentAmountFixResult r, string m) { r.Success = false; r.Message = m; return r; }
        private static decimal Dec(object v) { if (v == null || v == DBNull.Value) return 0m; try { return Convert.ToDecimal(v); } catch { return 0m; } }
        private static string H(string s) => System.Web.HttpUtility.HtmlEncode(s ?? "");
        private static string Json(string id, decimal t, decimal v, decimal e) =>
            "{\"receipt\":\"" + id.Replace("\"", "") + "\",\"total\":" + t.ToString("0.00", CultureInfo.InvariantCulture)
            + ",\"vat\":" + v.ToString("0.00", CultureInfo.InvariantCulture) + ",\"exVat\":" + e.ToString("0.00", CultureInfo.InvariantCulture) + "}";
        private static void Exec(SqlConnection cn, SqlTransaction tx, string sql, Dictionary<string, object> p)
        {
            using (var cmd = new SqlCommand(sql, cn, tx))
            {
                foreach (var kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }
        private static void Log(string conn, string msg)
        {
            try { new code().Logs(conn, "PaymentAmountFix", msg, System.Web.HttpContext.Current?.Session?["User"]?.ToString() ?? "System"); } catch { }
        }
    }
}
