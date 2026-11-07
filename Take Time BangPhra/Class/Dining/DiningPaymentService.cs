using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.Hosting;
using Take_Time_BangPhra.Integration;
using Take_Time_BangPhra.Payments.SlipCheck;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>
    /// การชำระของ "บิล" ต่อ session (ไม่ใช่ต่อออเดอร์) — 1 แถว Dining_Payment ต่อการจ่าย, ปิดออเดอร์ที่ครอบเป็น PAID/CHARGED
    ///   • เงินสด: พนักงานเดินเก็บแล้วกด "รับเงินสดแล้ว" (RecordCash) — ยอดต้องเท่ากับยอดค้างของชุดออเดอร์ (ไม่รับจ่ายบางส่วน)
    ///   • โอน: ลูกค้าแนบสลิปเอง → SlipVerificationService.EvaluateCustomerSlip (fixedTotal) → ผ่าน = PAID ทันที ·
    ///     ไม่ผ่าน = ตาม SlipVerify_Customer_Fail_Mode (CONTACT_ADMIN ไม่รับ / ALLOW_MANUAL รับเป็น PENDING_SLIP รอพนักงาน) · สลิปซ้ำ = ไม่รับเสมอ ·
    ///     ปิดตรวจสลิป (ไม่มี PHASE19_34) = รับเป็น PENDING_SLIP (พนักงานตรวจเอง) — ไม่ PAID อัตโนมัติ
    ///   • ลงบิลห้อง: ลูกค้าเลือกห้อง (เฉพาะเลขห้อง) + กรอกเบอร์ที่ใช้จอง → RoomChargeService/RoomChargeDataAccess → ออเดอร์ CHARGED
    ///     ผิดครบ Dining_Room_Charge_Max_Fail ต่อ session = ล็อก 15 นาที + Telegram
    /// บัญชี: CASH/TRANSFER ที่ CONFIRMED ถูก DiningPostingService เก็บไป ChannelSalesPostingService (SourceKey DNP-{id}); ROOM_CHARGE ไปกับใบเช็คเอาท์
    /// </summary>
    public class DiningPaymentService
    {
        private readonly string _conn;
        private readonly DiningTableService _tables;
        private readonly DiningOrderService _orders;

        public DiningPaymentService() : this(DiningDb.ConnStr) { }
        public DiningPaymentService(string connectionString)
        {
            _conn = connectionString;
            _tables = new DiningTableService(connectionString);
            _orders = new DiningOrderService(connectionString);
        }

        // ═════════════════════════════════════════════════════════════════════
        // ยอดค้าง / อ่านการชำระ
        // ═════════════════════════════════════════════════════════════════════

        public OutstandingInfo Outstanding(long sessionId) { return LoadOutstanding(_conn, sessionId); }

        /// <summary>ยอดค้างของ session: ออเดอร์ UNPAID ที่ไม่ยกเลิก — แยกส่วนที่มีสลิปรอตรวจ (จ่ายซ้ำไม่ได้จนพนักงานตัดสิน)</summary>
        internal static OutstandingInfo LoadOutstanding(string conn, long sessionId)
        {
            var o = new OutstandingInfo();
            if (!DiningDb.Ready(conn)) return o;
            var dt = DiningDb.Query(conn, @"SELECT ID, Total_Amount, Pending_Payment_ID FROM dbo.Dining_Order
                                            WHERE Session_ID = @s AND Order_Status <> 'CANCELLED' AND Payment_Status = 'UNPAID' ORDER BY ID",
                new Dictionary<string, object> { { "@s", sessionId } });
            foreach (DataRow r in dt.Rows)
            {
                long id = DiningDb.Long(r["ID"]); decimal amt = DiningDb.Dec(r["Total_Amount"]);
                o.Amount += amt; o.OrderIds.Add(id);
                if (r["Pending_Payment_ID"] != DBNull.Value) { o.PendingSlipAmount += amt; o.PendingSlipOrderIds.Add(id); }
                else { o.PayableAmount += amt; o.PayableOrderIds.Add(id); }
            }
            return o;
        }

        public List<DiningPayment> ListPayments(long sessionId) { return LoadPayments(_conn, sessionId); }

        internal static List<DiningPayment> LoadPayments(string conn, long sessionId)
        {
            var list = new List<DiningPayment>();
            if (!DiningDb.Ready(conn)) return list;
            var dt = DiningDb.Query(conn, "SELECT * FROM dbo.Dining_Payment WHERE Session_ID = @s ORDER BY ID", new Dictionary<string, object> { { "@s", sessionId } });
            foreach (DataRow r in dt.Rows) list.Add(ReadPayment(r));
            return list;
        }

        public DiningPayment GetPayment(long paymentId) { return LoadPayment(_conn, paymentId); }

        internal static DiningPayment LoadPayment(string conn, long id)
        {
            if (!DiningDb.Ready(conn)) return null;
            var dt = DiningDb.Query(conn, "SELECT * FROM dbo.Dining_Payment WHERE ID = @id", new Dictionary<string, object> { { "@id", id } });
            return dt.Rows.Count == 0 ? null : ReadPayment(dt.Rows[0]);
        }

        /// <summary>สลิปที่รอพนักงานตรวจ (PENDING_SLIP) ทั้งหมด พร้อม session</summary>
        public List<DiningPayment> ListPendingSlips()
        {
            var list = new List<DiningPayment>();
            if (!DiningDb.Ready(_conn)) return list;
            var dt = DiningDb.Query(_conn, "SELECT TOP 200 * FROM dbo.Dining_Payment WHERE Status = 'PENDING_SLIP' ORDER BY ID DESC");
            foreach (DataRow r in dt.Rows) list.Add(ReadPayment(r));
            return list;
        }

        // ═════════════════════════════════════════════════════════════════════
        // เงินสด (พนักงาน)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// บันทึกรับเงินสดที่โต๊ะ — orderIds null/ว่าง = ทุกออเดอร์ที่จ่ายได้ของ session; paidHowId 0 = แหล่งเงินชนิด CASH แถวแรก
        /// ยอด = ผลรวมของชุดออเดอร์ (ไม่รับจ่ายบางส่วน)
        /// </summary>
        public PaymentResult RecordCash(long sessionId, IList<long> orderIds, int paidHowId, int staffId)
        {
            var r = new PaymentResult();
            if (!DiningDb.Ready(_conn)) { Fail(r, DiningError.NotReady, DiningDb.NotReady().Message); return r; }
            var s = _tables.GetSessionById(sessionId);
            if (s == null) { Fail(r, DiningError.NotFound, "ไม่พบ session ของโต๊ะ"); return r; }
            List<long> cover; decimal amount; string err;
            if (!ResolveCover(sessionId, orderIds, out cover, out amount, out err)) { Fail(r, DiningError.NothingToPay, err); return r; }

            string phName;
            int ph = ResolvePaidHow(paidHowId, "CASH", out phName);
            var pay = new DiningPayment
            {
                SessionId = sessionId, Method = DiningStatus.Method.Cash, Status = DiningStatus.Payment.Confirmed, Amount = amount,
                PaidHowId = ph > 0 ? ph : (int?)null, PaidHowName = phName, PaidAt = DateTime.Now, PaidBy = staffId > 0 ? staffId : (int?)null, CoversOrderIds = cover
            };
            if (!InsertPaymentAndSettle(pay, DiningStatus.Pay.Paid, out err)) { Fail(r, DiningError.Duplicate, err); return r; }
            r.Ok = true; r.Payment = pay; r.CoveredOrderIds = cover;
            r.Message = "รับเงินสด " + amount.ToString("N2", CultureInfo.InvariantCulture) + " บาท แล้ว";
            string staffName = DiningDb.StaffName();
            DiningDb.Event(_conn, null, sessionId, s.TableId, DiningStatus.Event.Paid, staffName, "เงินสด " + amount.ToString("N2", CultureInfo.InvariantCulture) + " → " + phName + " (" + cover.Count + " ออเดอร์)");
            DiningDb.Log(_conn, "รับเงินสด " + s.Label + " " + amount.ToString("N2", CultureInfo.InvariantCulture) + " บาท → " + phName);
            try { new DiningNotifier(_conn).CashReceived(pay, s, OrderNos(cover), staffName); } catch { }
            return r;
        }

        // ═════════════════════════════════════════════════════════════════════
        // โอน + สลิป (ลูกค้า)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>แบบรับ HttpPostedFile (จาก .ashx/FileUpload) — บันทึกผ่าน UploadHelper.Save แล้วอ่านไฟล์กลับมาตรวจ</summary>
        public SlipOutcome SubmitTransferSlip(string sessionToken, HttpPostedFile file, IList<long> coversOrderIds, int selectedPaidHowId, string clientIp)
        {
            var r = new SlipOutcome();
            if (file == null || file.ContentLength <= 0) { Fail(r, DiningError.Validation, "กรุณาแนบรูปสลิป"); return r; }
            byte[] bytes;
            try
            {
                using (var ms = new MemoryStream())
                {
                    file.InputStream.Position = 0;
                    file.InputStream.CopyTo(ms);
                    bytes = ms.ToArray();
                }
            }
            catch (Exception ex) { Fail(r, DiningError.Validation, "อ่านไฟล์ไม่ได้: " + ex.Message); return r; }
            return SubmitTransferSlip(sessionToken, bytes, file.FileName, coversOrderIds, selectedPaidHowId, clientIp);
        }

        /// <summary>
        /// ลูกค้าแนบสลิปโอน — ยอด = ผลรวมของชุดออเดอร์ (coversOrderIds null/ว่าง = ทุกออเดอร์ที่จ่ายได้ของ session)
        /// ผ่าน → Payment CONFIRMED + ออเดอร์ PAID (แหล่งเงิน = บัญชีผู้รับที่สลิปชี้ > ช่องทางที่เลือก > TRANSFER แถวแรก)
        /// ไม่ผ่าน → CONTACT_ADMIN = Rejected (Message + ContactText) / ALLOW_MANUAL = Pending (ออเดอร์ติด Pending_Payment_ID) · สลิปซ้ำ = Rejected เสมอ
        /// </summary>
        public SlipOutcome SubmitTransferSlip(string sessionToken, byte[] bytes, string fileName, IList<long> coversOrderIds, int selectedPaidHowId, string clientIp)
        {
            var r = new SlipOutcome();
            try
            {
                if (!DiningDb.Ready(_conn)) { Fail(r, DiningError.NotReady, DiningDb.NotReady().Message); return r; }
                var s = _tables.GetSession(sessionToken);
                if (s == null) { Fail(r, DiningError.SessionClosed, "โต๊ะนี้ปิดแล้ว กรุณาสแกน QR ที่โต๊ะใหม่"); return r; }
                if (!s.AllowTransfer) { Fail(r, DiningError.Forbidden, "โซนนี้ไม่รับชำระด้วยการโอน กรุณาชำระเงินสดกับพนักงาน"); return r; }
                if (bytes == null || bytes.Length == 0) { Fail(r, DiningError.Validation, "กรุณาแนบรูปสลิป"); return r; }
                List<long> cover; decimal amount; string err;
                if (!ResolveCover(s.Id, coversOrderIds, out cover, out amount, out err)) { Fail(r, DiningError.NothingToPay, err); return r; }

                string webPath;
                if (!SaveSlipBytes(bytes, fileName, out webPath, out err)) { Fail(r, DiningError.Validation, err); return r; }
                r.SlipPath = webPath;

                var ctx = new SlipVerifyContext { SourceType = DiningConfig.SlipSourceType, SourceId = s.Id, CustomerSelfService = true, User = "GUEST" };
                CustomerSlipOutcome oc = null;
                try { oc = SlipVerificationService.EvaluateCustomerSlip(bytes, fileName, amount, true, ctx, webPath); }
                catch (Exception ex) { DiningDb.Log(_conn, "ตรวจสลิปไม่ได้ (รับไว้รอพนักงาน): " + ex.Message, "SYSTEM"); }

                SlipVerifyResult vr = oc != null ? oc.Result : null;
                r.SlipAmount = vr != null ? vr.Amount : null;
                bool checkerRan = oc != null && vr != null;   // ปิดตรวจสลิป/ไม่มี PHASE19_34 → Result null → รอพนักงาน

                if (oc != null && oc.Blocked)
                {
                    r.Rejected = true; r.Ok = false; r.Reason = oc.Reason;
                    r.DuplicateSlip = vr != null && vr.Status == SlipVerifyStatus.Duplicate;
                    r.ErrorCode = r.DuplicateSlip ? DiningError.SlipDuplicate : DiningError.SlipRejected;
                    r.Message = oc.Message;
                    r.ContactText = SafeContact();
                    DiningDb.Event(_conn, null, s.Id, s.TableId, DiningStatus.Event.SlipRejected, "GUEST", (r.DuplicateSlip ? "สลิปซ้ำ" : "ตรวจไม่ผ่าน") + ": " + oc.Reason + " · " + webPath);
                    try { new DiningNotifier(_conn).SlipRejected(s, amount, oc.Reason); } catch { }
                    return r;
                }

                bool verified = checkerRan && oc.Accept && !oc.Failed;
                string phName;
                int ph = verified && vr.MatchedPaidHowId.HasValue && vr.MatchedPaidHowId.Value > 0 ? vr.MatchedPaidHowId.Value : selectedPaidHowId;
                ph = ResolvePaidHow(ph, "TRANSFER", out phName);

                var pay = new DiningPayment
                {
                    SessionId = s.Id, Method = DiningStatus.Method.Transfer, Amount = amount, PaidHowId = ph > 0 ? ph : (int?)null, PaidHowName = phName,
                    SlipPath = webPath, CoversOrderIds = cover,
                    SlipVerifyStatus = vr != null ? vr.Status : (checkerRan ? null : "MANUAL"),
                    SlipBankAmount = vr != null ? vr.Amount : null, SlipTransRef = vr != null ? Cut(vr.TransRef, 60) : null,
                    SlipNote = oc != null && oc.Failed ? Cut(oc.Reason, 500) : (checkerRan ? null : "ไม่ได้ตรวจอัตโนมัติ (ปิดตรวจสลิป) — รอพนักงาน")
                };

                if (verified)
                {
                    pay.Status = DiningStatus.Payment.Confirmed; pay.PaidAt = DateTime.Now;
                    if (!InsertPaymentAndSettle(pay, DiningStatus.Pay.Paid, out err)) { Fail(r, DiningError.Duplicate, err); return r; }
                    try { SlipVerificationService.TryLog(ctx, vr, true); } catch { }
                    r.Ok = true; r.Verified = true; r.Payment = pay; r.CoveredOrderIds = cover;
                    r.Message = "ตรวจสลิปผ่านแล้ว — ชำระ " + amount.ToString("N2", CultureInfo.InvariantCulture) + " บาท เรียบร้อย ขอบคุณค่ะ";
                    DiningDb.Event(_conn, null, s.Id, s.TableId, DiningStatus.Event.Paid, "GUEST", "โอน " + amount.ToString("N2", CultureInfo.InvariantCulture) + " ตรวจผ่าน → " + phName + " · " + webPath);
                    DiningDb.Log(_conn, "รับโอน " + s.Label + " " + amount.ToString("N2", CultureInfo.InvariantCulture) + " บาท ตรวจสลิปผ่าน → " + phName, "GUEST");
                    try { new DiningNotifier(_conn).SlipVerified(pay, s, OrderNos(cover)); } catch { }
                    return r;
                }

                // รอพนักงานตรวจ
                pay.Status = DiningStatus.Payment.PendingSlip;
                if (!InsertPaymentAndSettle(pay, null, out err)) { Fail(r, DiningError.Duplicate, err); return r; }
                if (vr != null) { try { SlipVerificationService.TryLog(ctx, vr, false); } catch { } }
                r.Ok = true; r.Pending = true; r.Payment = pay; r.CoveredOrderIds = cover; r.Reason = oc != null ? oc.Reason : null;
                r.Message = oc != null && !string.IsNullOrEmpty(oc.Message) ? oc.Message : "รับสลิปแล้ว — พนักงานจะตรวจสอบและยืนยันให้";
                DiningDb.Event(_conn, null, s.Id, s.TableId, DiningStatus.Event.SlipPending, "GUEST", "สลิปรอตรวจ " + amount.ToString("N2", CultureInfo.InvariantCulture) + (oc != null ? ": " + oc.Reason : "") + " · " + webPath);
                try { new DiningNotifier(_conn).SlipPending(pay, s, OrderNos(cover), r.Reason); } catch { }
                return r;
            }
            catch (Exception ex)
            {
                DiningDb.Log(_conn, "SubmitTransferSlip ล้มเหลว: " + ex.Message, "SYSTEM");
                Fail(r, DiningError.Internal, "บันทึกสลิปไม่สำเร็จ กรุณาลองใหม่หรือเรียกพนักงาน");
                return r;
            }
        }

        /// <summary>
        /// พนักงานยืนยันสลิปที่รอตรวจ — ตรวจซ้ำด้วย VerifyFileFixedTotal (กฎเดียวกับ OrderManagement.CheckOrderSlip):
        /// ไม่ผ่าน (ยอดไม่ตรง/ผู้รับผิด) = Owner/Admin ยืนยันได้ด้วย overrideReason (≥5 ตัว) หรือกดซ้ำภายใน 10 นาที (ConsumeSecondClick) · สลิปซ้ำ = หยุดเสมอ
        /// </summary>
        public PaymentResult ApprovePendingSlip(long paymentId, int staffId, string overrideReason)
        {
            var r = new PaymentResult();
            if (!DiningDb.Ready(_conn)) { Fail(r, DiningError.NotReady, DiningDb.NotReady().Message); return r; }
            var pay = LoadPayment(_conn, paymentId);
            if (pay == null) { Fail(r, DiningError.NotFound, "ไม่พบรายการสลิป"); return r; }
            if (pay.Status != DiningStatus.Payment.PendingSlip) { Fail(r, DiningError.Validation, "รายการนี้ไม่ได้รอตรวจ (" + pay.Status + ")"); return r; }
            var s = _tables.GetSessionById(pay.SessionId);
            string staffName = DiningDb.StaffName();

            try
            {
                if (SlipVerificationService.IsEnabled && !string.IsNullOrEmpty(pay.SlipPath))
                {
                    var ctx = new SlipVerifyContext { SourceType = DiningConfig.SlipSourceType, SourceId = pay.SessionId, User = SlipVerificationService.CurrentUserName() };
                    var vr = SlipVerificationService.VerifyFileFixedTotal(pay.SlipPath, pay.Amount > 0m ? (decimal?)pay.Amount : null, ctx);
                    if (vr.IsBlocking)
                    {
                        bool canOv = SlipVerificationService.CanOverrideCurrentUser();
                        bool confirmed = vr.CanOverride && canOv
                            && ((!string.IsNullOrWhiteSpace(overrideReason) && overrideReason.Trim().Length >= 5) || SlipVerificationService.ConsumeSecondClick("DN-" + paymentId));
                        if (!confirmed)
                        {
                            SlipVerificationService.NotifyIfSerious(vr, null, null, null, ctx.User);
                            Fail(r, vr.Status == SlipVerifyStatus.Duplicate ? DiningError.SlipDuplicate : DiningError.AmountMismatch,
                                "ตรวจสลิปไม่ผ่าน: " + vr.Message + (vr.CanOverride && canOv ? "\nตรวจกับบัญชีธนาคารแล้วระบุเหตุผล หรือกด \"ยืนยันรับเงิน\" อีกครั้งภายใน 10 นาทีเพื่อยืนยัน" : ""));
                            return r;
                        }
                        SlipVerificationService.NotifyIfSerious(vr, null, null, "ยืนยันซ้ำที่กระดาน QR สั่งอาหาร (การชำระ #" + paymentId + ")" + (string.IsNullOrWhiteSpace(overrideReason) ? "" : " เหตุผล: " + overrideReason.Trim()), ctx.User);
                    }
                    SlipVerificationService.TryLog(ctx, vr, true);
                    if (vr.MatchedPaidHowId.HasValue && vr.MatchedPaidHowId.Value > 0 && (!pay.PaidHowId.HasValue || pay.PaidHowId.Value != vr.MatchedPaidHowId.Value))
                    {
                        string n; pay.PaidHowId = ResolvePaidHow(vr.MatchedPaidHowId.Value, "TRANSFER", out n); pay.PaidHowName = n;
                    }
                    pay.SlipVerifyStatus = vr.Status; pay.SlipBankAmount = vr.Amount; pay.SlipTransRef = Cut(vr.TransRef, 60);
                }
            }
            catch (Exception ex) { DiningDb.Log(_conn, "ตรวจสลิป #" + paymentId + " ไม่ได้ (ข้าม): " + ex.Message, "SYSTEM"); }

            string err;
            if (!ConfirmPendingPayment(pay, staffId, overrideReason, out err)) { Fail(r, DiningError.Validation, err); return r; }
            r.Ok = true; r.Payment = pay; r.CoveredOrderIds = pay.CoversOrderIds;
            r.Message = "ยืนยันรับเงินโอน " + pay.Amount.ToString("N2", CultureInfo.InvariantCulture) + " บาท แล้ว";
            DiningDb.Event(_conn, null, pay.SessionId, s != null ? s.TableId : (int?)null, DiningStatus.Event.Paid, staffName, "ยืนยันสลิป #" + paymentId + " " + pay.Amount.ToString("N2", CultureInfo.InvariantCulture) + (string.IsNullOrWhiteSpace(overrideReason) ? "" : " override: " + overrideReason));
            DiningDb.Log(_conn, "ยืนยันสลิป #" + paymentId + " " + (s != null ? s.Label : "") + " " + pay.Amount.ToString("N2", CultureInfo.InvariantCulture) + " บาท");
            try { new DiningNotifier(_conn).SlipVerified(pay, s, OrderNos(pay.CoversOrderIds)); } catch { }
            return r;
        }

        /// <summary>พนักงานปฏิเสธสลิปที่รอตรวจ — ออเดอร์กลับเป็นจ่ายได้ (ลูกค้าส่งสลิปใหม่/จ่ายเงินสด)</summary>
        public DiningResult RejectPendingSlip(long paymentId, int staffId, string reason)
        {
            if (!DiningDb.Ready(_conn)) return DiningDb.NotReady();
            var pay = LoadPayment(_conn, paymentId);
            if (pay == null) return DiningResult.Fail(DiningError.NotFound, "ไม่พบรายการสลิป");
            if (pay.Status != DiningStatus.Payment.PendingSlip) return DiningResult.Fail(DiningError.Validation, "รายการนี้ไม่ได้รอตรวจ");
            int n = DiningDb.Exec(_conn, "UPDATE dbo.Dining_Payment SET Status='REJECTED', Slip_Note=@n, Paid_By=@by, Updated_At=GETDATE() WHERE ID=@id AND Status='PENDING_SLIP'",
                new Dictionary<string, object> { { "@id", paymentId }, { "@n", DiningDb.DbStr("ปฏิเสธ: " + (reason ?? ""), 500) }, { "@by", staffId > 0 ? (object)staffId : DBNull.Value } });
            if (n <= 0) return DiningResult.Fail(DiningError.Validation, "สถานะเปลี่ยนไปแล้ว");
            DiningDb.Exec(_conn, "UPDATE dbo.Dining_Order SET Pending_Payment_ID = NULL, Updated_At=GETDATE() WHERE Pending_Payment_ID = @id", new Dictionary<string, object> { { "@id", paymentId } });
            var s = _tables.GetSessionById(pay.SessionId);
            DiningDb.Event(_conn, null, pay.SessionId, s != null ? s.TableId : (int?)null, DiningStatus.Event.SlipRejected, DiningDb.StaffName(), "ปฏิเสธสลิป #" + paymentId + ": " + reason);
            DiningDb.Log(_conn, "ปฏิเสธสลิป #" + paymentId + " " + (s != null ? s.Label : "") + ": " + reason);
            try { new DiningNotifier(_conn).SlipRejected(s, pay.Amount, "พนักงานปฏิเสธ: " + (reason ?? "")); } catch { }
            return DiningResult.Success("ปฏิเสธสลิปแล้ว — ออเดอร์กลับเป็นค้างชำระ");
        }

        // ═════════════════════════════════════════════════════════════════════
        // ลงบิลห้องพัก (ลูกค้า)
        // ═════════════════════════════════════════════════════════════════════

        private const string CandidateSql = @"
            SELECT R.ID, dbo.fn_GetReservationRoomNames(R.ID) AS Rooms
              FROM Reservation R
             WHERE {HOLDS}
               AND CAST(GETDATE() AS DATE) >= CAST(R.CheckinDate AS DATE)
               AND CAST(GETDATE() AS DATE) <= CAST(R.CheckoutDate AS DATE)
               AND (R.Status LIKE N'เช็คอิน%' OR CAST(R.CheckinDate AS DATE) >= DATEADD(DAY, -1, CAST(GETDATE() AS DATE)))";

        /// <summary>
        /// การจองที่ลงบิลห้องได้: ถือห้องอยู่วันนี้ (RescheduleService.SqlHoldsRoom) และ (เช็คอินแล้ว หรือ วันเข้าพักคือวันนี้/เมื่อวาน) —
        /// คืนเฉพาะ ReservationId + ป้ายห้อง (ไม่มีชื่อ/เบอร์) เรียงตามห้อง
        /// </summary>
        public List<RoomChargeCandidate> ListRoomChargeCandidates()
        {
            var list = new List<RoomChargeCandidate>();
            try
            {
                var dt = DiningDb.Query(_conn, CandidateSql.Replace("{HOLDS}", RescheduleService.SqlHoldsRoom("R")) + " ORDER BY Rooms, R.ID");
                foreach (DataRow r in dt.Rows)
                {
                    int id = DiningDb.Int(r["ID"]);
                    string rooms = (DiningDb.Str(r["Rooms"]) ?? "").Trim();
                    list.Add(new RoomChargeCandidate { ReservationId = id, RoomLabel = rooms.Length > 0 ? rooms : "การจอง #" + id });
                }
            }
            catch (Exception ex) { DiningDb.Log(_conn, "ListRoomChargeCandidates ล้มเหลว: " + ex.Message, "SYSTEM"); }
            return list;
        }

        /// <summary>
        /// ลงบิลห้องทั้งยอดค้างของ session: ลูกค้าเลือกห้อง + กรอกเบอร์ที่ใช้จอง (เทียบ Reservation.Customer_MobilePhone / Customer.MobilePhone แบบ normalize)
        /// ผิด → นับต่อ session; ครบ Dining_Room_Charge_Max_Fail = ล็อก 15 นาที + Telegram · ผ่าน → RoomChargeService (รายการตัดสต๊อก) /
        /// RoomChargeDataAccess.CreateRoomCharge (อาหารปรุง ไม่ตัดสต๊อก) → Payment ROOM_CHARGE + ออเดอร์ CHARGED + Room_Charge_Ref
        /// </summary>
        public RoomChargeResult ChargeToRoom(string sessionToken, int reservationId, string phoneTyped, string clientIp)
        {
            var r = new RoomChargeResult();
            try
            {
                if (!DiningDb.Ready(_conn)) { Fail(r, DiningError.NotReady, DiningDb.NotReady().Message); return r; }
                var s = _tables.GetSession(sessionToken);
                if (s == null) { Fail(r, DiningError.SessionClosed, "โต๊ะนี้ปิดแล้ว กรุณาสแกน QR ที่โต๊ะใหม่"); return r; }
                if (!s.AllowRoomCharge) { Fail(r, DiningError.Forbidden, "โซนนี้ไม่เปิดให้ลงบิลห้องพัก"); return r; }
                if (s.RoomChargeLockedUntil.HasValue && s.RoomChargeLockedUntil.Value > DateTime.Now)
                {
                    r.LockedUntil = s.RoomChargeLockedUntil;
                    Fail(r, DiningError.Locked, "การลงบิลห้องของโต๊ะนี้ถูกล็อกชั่วคราว (ยืนยันเบอร์ผิดหลายครั้ง) — กรุณาติดต่อพนักงาน หรือลองใหม่หลัง " + s.RoomChargeLockedUntil.Value.ToString("HH:mm", CultureInfo.InvariantCulture));
                    return r;
                }
                if (reservationId <= 0) { Fail(r, DiningError.Validation, "กรุณาเลือกห้องพัก"); return r; }
                string typed = DiningTokens.NormalizePhone(phoneTyped);
                if (typed.Length < 9) { Fail(r, DiningError.Validation, "กรุณากรอกเบอร์โทรที่ใช้จองให้ครบ"); return r; }

                // ห้องต้องอยู่ในรายการที่ลงบิลได้
                var cand = DiningDb.Query(_conn, CandidateSql.Replace("{HOLDS}", RescheduleService.SqlHoldsRoom("R")) + " AND R.ID = @rid", new Dictionary<string, object> { { "@rid", reservationId } });
                if (cand.Rows.Count == 0) { Fail(r, DiningError.NotFound, "ห้องนี้ไม่อยู่ในรายการที่ลงบิลได้ (ยังไม่เช็คอิน/เช็คเอาท์แล้ว) กรุณาติดต่อพนักงาน"); return r; }
                string roomLabel = (DiningDb.Str(cand.Rows[0]["Rooms"]) ?? "").Trim();
                if (roomLabel.Length == 0) roomLabel = "การจอง #" + reservationId;
                r.RoomLabel = roomLabel;

                // เบอร์โทร
                var ph = DiningDb.Query(_conn, @"SELECT R.Customer_MobilePhone AS P1, C.MobilePhone AS P2 FROM Reservation R LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone WHERE R.ID = @rid",
                    new Dictionary<string, object> { { "@rid", reservationId } });
                bool match = false;
                if (ph.Rows.Count > 0)
                    match = DiningTokens.PhoneMatches(typed, DiningDb.Str(ph.Rows[0]["P1"])) || DiningTokens.PhoneMatches(typed, DiningDb.Str(ph.Rows[0]["P2"]));
                if (!match)
                {
                    RecordAttempt(s.Id, reservationId, false, typed, clientIp);
                    int fails = CountRecentFailures(s.Id);
                    int max = DiningConfig.RoomChargeMaxFail;
                    if (fails >= max)
                    {
                        DateTime until = DateTime.Now.AddMinutes(DiningConfig.LockoutMinutes);
                        DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table_Session SET Room_Charge_Locked_Until=@u WHERE ID=@id", new Dictionary<string, object> { { "@id", s.Id }, { "@u", until } });
                        DiningDb.Event(_conn, null, s.Id, s.TableId, DiningStatus.Event.RoomChargeFail, "GUEST", "ยืนยันเบอร์ผิดครบ " + fails + " ครั้ง → ล็อกถึง " + until.ToString("HH:mm", CultureInfo.InvariantCulture) + " (ห้อง " + roomLabel + ")");
                        DiningDb.Log(_conn, "ล็อกลงบิลห้อง " + s.Label + " (ผิด " + fails + " ครั้ง, ห้อง " + roomLabel + ", เบอร์ " + DiningTokens.MaskPhone(typed) + ")", "GUEST");
                        try { new DiningNotifier(_conn).RoomChargeLocked(s, fails, until); } catch { }
                        r.LockedUntil = until; r.AttemptsLeft = 0;
                        Fail(r, DiningError.Locked, "ยืนยันเบอร์โทรผิดครบ " + max + " ครั้ง — ล็อกการลงบิลห้อง " + DiningConfig.LockoutMinutes + " นาที กรุณาติดต่อพนักงาน");
                        return r;
                    }
                    r.AttemptsLeft = max - fails;
                    DiningDb.Event(_conn, null, s.Id, s.TableId, DiningStatus.Event.RoomChargeFail, "GUEST", "เบอร์ไม่ตรง ห้อง " + roomLabel + " (" + DiningTokens.MaskPhone(typed) + ") เหลือ " + r.AttemptsLeft);
                    Fail(r, DiningError.PhoneMismatch, "เบอร์โทรไม่ตรงกับการจองของห้องนี้ (ลองได้อีก " + r.AttemptsLeft + " ครั้ง)");
                    return r;
                }

                List<long> cover; decimal amount; string err;
                if (!ResolveCover(s.Id, null, out cover, out amount, out err)) { RecordAttempt(s.Id, reservationId, true, typed, clientIp); Fail(r, DiningError.NothingToPay, err); return r; }

                try { new RoomChargeService(_conn).ValidateRoomChargeAllowed(reservationId); }
                catch (Exception ex) { Fail(r, DiningError.Forbidden, ex.Message); return r; }

                // จอง: payment + ออเดอร์ CHARGED ก่อน (กันกดซ้ำ) → ลงรายการเข้าห้อง → ล้มเหลว = ถอยกลับ
                string refNote = "QR สั่งอาหาร " + s.Label + " session #" + s.Id;
                var pay = new DiningPayment
                {
                    SessionId = s.Id, Method = DiningStatus.Method.RoomCharge, Status = DiningStatus.Payment.Confirmed, Amount = amount, PaidAt = DateTime.Now,
                    ReservationId = reservationId, CoversOrderIds = cover, RoomChargeRef = "RC-" + reservationId + "-S" + s.Id, PaidHowName = "ลงบิลห้องพัก"
                };
                if (!InsertPaymentAndSettle(pay, DiningStatus.Pay.Charged, out err)) { Fail(r, DiningError.Duplicate, err); return r; }

                long lastChargeId = 0;
                try
                {
                    lastChargeId = PostChargesToFolio(reservationId, cover, refNote + " (" + pay.RoomChargeRef + ")");
                }
                catch (Exception ex)
                {
                    RevertPayment(pay, cover, "ลงบิลห้องไม่สำเร็จ: " + ex.Message);
                    DiningDb.Log(_conn, "ลงบิลห้อง " + s.Label + " → การจอง #" + reservationId + " ล้มเหลว: " + ex.Message, "GUEST");
                    Fail(r, DiningError.Internal, "ลงบิลห้องไม่สำเร็จ: " + ex.Message + " — กรุณาติดต่อพนักงาน");
                    return r;
                }
                if (lastChargeId > 0)
                {
                    pay.RoomChargeRef = "RPC:" + lastChargeId;
                    DiningDb.Exec(_conn, "UPDATE dbo.Dining_Payment SET Room_Charge_Ref=@r, Updated_At=GETDATE() WHERE ID=@id", new Dictionary<string, object> { { "@id", pay.Id }, { "@r", pay.RoomChargeRef } });
                }
                // สต๊อกถูกตัดโดย RoomChargeService แล้ว — กัน DiningPostingService ตัดซ้ำ
                var pp = new Dictionary<string, object>();
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Order SET Stock_Posted = 1 WHERE ID IN (" + DiningDb.InList(cover, pp) + ")", pp);
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table_Session SET Reservation_ID=@rid, Last_Seen_At=GETDATE() WHERE ID=@id", new Dictionary<string, object> { { "@id", s.Id }, { "@rid", reservationId } });
                RecordAttempt(s.Id, reservationId, true, typed, clientIp);

                r.Ok = true; r.Payment = pay; r.CoveredOrderIds = cover;
                r.Message = "ลงบิลห้อง " + roomLabel + " " + amount.ToString("N2", CultureInfo.InvariantCulture) + " บาท แล้ว — ยอดจะรวมในใบเสร็จตอนเช็คเอาท์";
                DiningDb.Event(_conn, null, s.Id, s.TableId, DiningStatus.Event.RoomCharge, "GUEST", amount.ToString("N2", CultureInfo.InvariantCulture) + " → ห้อง " + roomLabel + " (การจอง #" + reservationId + ") " + pay.RoomChargeRef);
                DiningDb.Log(_conn, "ลงบิลห้อง " + s.Label + " " + amount.ToString("N2", CultureInfo.InvariantCulture) + " → ห้อง " + roomLabel + " (#" + reservationId + ")", "GUEST");
                try { new DiningNotifier(_conn).RoomCharged(pay, s, roomLabel, OrderNos(cover)); } catch { }
                return r;
            }
            catch (Exception ex)
            {
                DiningDb.Log(_conn, "ChargeToRoom ล้มเหลว: " + ex.Message, "SYSTEM");
                Fail(r, DiningError.Internal, "ลงบิลห้องไม่สำเร็จ กรุณาติดต่อพนักงาน");
                return r;
            }
        }

        /// <summary>ลงรายการเข้าห้อง: Track_Stock → RoomChargeService.ChargeToRoom (ตัดสต๊อก+COGS) · อื่น ๆ → CreateRoomCharge ตรง (ไม่ตัดสต๊อก)</summary>
        private long PostChargesToFolio(int reservationId, List<long> orderIds, string notes)
        {
            var orders = new List<DiningOrder>();
            foreach (long id in orderIds) { var o = _orders.GetOrder(id); if (o != null) orders.Add(o); }
            var pinfo = LoadProductInfo(orders);
            var stockCart = NewCartTable();
            var da = new RoomChargeDataAccess(_conn);
            int staff = DiningDb.StaffId();
            long last = 0;
            foreach (var o in orders)
                foreach (var it in o.Items)
                {
                    if (it.KitchenStatus == DiningStatus.Kitchen.Void || it.Qty <= 0m) continue;
                    decimal unit = DiningPricing.R2(it.UnitPrice + it.OptionsPrice);
                    string name = it.ProductName + (string.IsNullOrEmpty(it.OptionsText) ? "" : " (" + it.OptionsText + ")");
                    if (name.Length > 200) name = name.Substring(0, 200);
                    Tuple<string, int?> pi; pinfo.TryGetValue(it.ProductId, out pi);
                    if (it.TrackStock)
                    {
                        stockCart.Rows.Add(it.ProductId, pi != null ? pi.Item1 : null, name, it.Qty, unit, it.LineTotal, pi != null && pi.Item2.HasValue ? (object)pi.Item2.Value : DBNull.Value);
                    }
                    else
                    {
                        last = da.CreateRoomCharge(reservationId, it.ProductId, name, pi != null ? pi.Item1 : null, pi != null ? pi.Item2 : null,
                            it.Qty, unit, it.LineTotal, "ROOM_CHARGE", staff > 0 ? staff : (int?)null, notes + " " + o.OrderNo);
                    }
                }
            if (stockCart.Rows.Count > 0)
            {
                long id = new RoomChargeService(_conn).ChargeToRoom(reservationId, stockCart, staff > 0 ? staff : (int?)null, notes);
                if (id > 0) last = id;
            }
            return last;
        }

        private static DataTable NewCartTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(int)); dt.Columns.Add("Barcode", typeof(string)); dt.Columns.Add("Product_Name", typeof(string));
            dt.Columns.Add("Amount", typeof(decimal)); dt.Columns.Add("Sell_Price", typeof(decimal)); dt.Columns.Add("Price_Total", typeof(decimal));
            dt.Columns.Add("Category_ID", typeof(object));
            return dt;
        }

        private Dictionary<int, Tuple<string, int?>> LoadProductInfo(List<DiningOrder> orders)
        {
            var map = new Dictionary<int, Tuple<string, int?>>();
            var ids = new List<long>();
            foreach (var o in orders) foreach (var it in o.Items) if (!ids.Contains(it.ProductId)) ids.Add(it.ProductId);
            if (ids.Count == 0) return map;
            try
            {
                var p = new Dictionary<string, object>();
                var dt = DiningDb.Query(_conn, "SELECT ID, Barcode, Category_ID FROM Product WHERE ID IN (" + DiningDb.InList(ids, p) + ")", p);
                foreach (DataRow r in dt.Rows) map[DiningDb.Int(r["ID"])] = Tuple.Create(DiningDb.Str(r["Barcode"]), DiningDb.NInt(r["Category_ID"]));
            }
            catch { }
            return map;
        }

        private void RecordAttempt(long sessionId, int reservationId, bool ok, string phone, string ip)
        {
            try
            {
                if (!DiningDb.TableExists(_conn, "Dining_Room_Charge_Attempt")) return;
                DiningDb.Exec(_conn, "INSERT INTO dbo.Dining_Room_Charge_Attempt (Session_ID, Reservation_ID, Ok, Phone_Masked, Client_Ip) VALUES (@s, @r, @ok, @p, @ip)",
                    new Dictionary<string, object> { { "@s", sessionId }, { "@r", reservationId }, { "@ok", ok }, { "@p", DiningDb.DbStr(DiningTokens.MaskPhone(phone), 20) }, { "@ip", DiningDb.DbStr(ip, 45) } });
            }
            catch { }
        }

        private int CountRecentFailures(long sessionId)
        {
            try
            {
                if (!DiningDb.TableExists(_conn, "Dining_Room_Charge_Attempt")) return 0;
                object o = DiningDb.Scalar(_conn, @"SELECT COUNT(*) FROM dbo.Dining_Room_Charge_Attempt
                    WHERE Session_ID = @s AND Ok = 0 AND Attempted_At > DATEADD(MINUTE, -@m, GETDATE())
                      AND Attempted_At > ISNULL((SELECT MAX(Attempted_At) FROM dbo.Dining_Room_Charge_Attempt WHERE Session_ID = @s AND Ok = 1), '19000101')",
                    new Dictionary<string, object> { { "@s", sessionId }, { "@m", DiningConfig.LockoutMinutes * 4 } });
                return DiningDb.Int(o);
            }
            catch { return 0; }
        }

        // ═════════════════════════════════════════════════════════════════════
        // แกนกลาง: จอง/ปิดออเดอร์ + payment
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>ชุดออเดอร์ที่จะปิด: ระบุ = ต้องทุกตัวเป็นของ session และจ่ายได้ · ไม่ระบุ = ทุกตัวที่จ่ายได้</summary>
        private bool ResolveCover(long sessionId, IList<long> orderIds, out List<long> cover, out decimal amount, out string error)
        {
            cover = new List<long>(); amount = 0m; error = null;
            var outs = LoadOutstanding(_conn, sessionId);
            if (orderIds == null || orderIds.Count == 0)
            {
                cover.AddRange(outs.PayableOrderIds);
                amount = outs.PayableAmount;
            }
            else
            {
                var want = new List<long>();
                foreach (long id in orderIds) if (id > 0 && !want.Contains(id)) want.Add(id);
                foreach (long id in want)
                {
                    if (outs.PendingSlipOrderIds.Contains(id)) { error = "ออเดอร์ #" + id + " มีสลิปรอพนักงานตรวจอยู่ — รอผลก่อน"; return false; }
                    if (!outs.PayableOrderIds.Contains(id)) { error = "ออเดอร์ #" + id + " ไม่ได้ค้างชำระ (จ่ายแล้ว/ยกเลิก/ไม่ใช่ของโต๊ะนี้)"; return false; }
                    cover.Add(id);
                }
                var p = new Dictionary<string, object>();
                object sum = DiningDb.Scalar(_conn, "SELECT SUM(Total_Amount) FROM dbo.Dining_Order WHERE ID IN (" + DiningDb.InList(cover, p) + ")", p);
                amount = DiningDb.Dec(sum);
            }
            if (cover.Count == 0 || amount <= 0m)
            {
                error = outs.PendingSlipAmount > 0m ? "มีสลิปรอพนักงานตรวจอยู่ — ไม่มียอดที่จ่ายเพิ่มได้ตอนนี้" : "ไม่มียอดค้างชำระ";
                return false;
            }
            amount = DiningPricing.R2(amount);
            return true;
        }

        /// <summary>
        /// INSERT Dining_Payment + ปิดออเดอร์ใน transaction เดียว — settleStatus = PAID|CHARGED (null = แค่ติด Pending_Payment_ID)
        /// ออเดอร์ทุกตัวต้องยัง UNPAID และไม่มี Pending_Payment_ID (ไม่ครบ = rollback, คืน false — มีคนจ่ายซ้อน)
        /// </summary>
        private bool InsertPaymentAndSettle(DiningPayment pay, string settleStatus, out string error)
        {
            error = null;
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new SqlCommand(@"
                            INSERT INTO dbo.Dining_Payment (Session_ID, Method, Status, Amount, Paid_How_ID, Paid_How_Name, Paid_At, Paid_By, Slip_Path, Slip_Verify_Status,
                                Slip_Bank_Amount, Slip_Trans_Ref, Slip_Note, Room_Charge_Ref, Reservation_ID, Covers_Order_Ids, Idempotency_Key, Created_At)
                            VALUES (@s, @m, @st, @a, @ph, @phn, @at, @by, @sp, @svs, @sba, @str, @sn, @rcr, @rid, @cov, @idem, GETDATE());
                            SELECT CAST(SCOPE_IDENTITY() AS BIGINT)", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@s", pay.SessionId);
                            cmd.Parameters.AddWithValue("@m", pay.Method);
                            cmd.Parameters.AddWithValue("@st", pay.Status);
                            cmd.Parameters.AddWithValue("@a", pay.Amount);
                            cmd.Parameters.AddWithValue("@ph", DiningDb.Db(pay.PaidHowId));
                            cmd.Parameters.AddWithValue("@phn", DiningDb.DbStr(pay.PaidHowName, 150));
                            cmd.Parameters.AddWithValue("@at", DiningDb.Db(pay.PaidAt));
                            cmd.Parameters.AddWithValue("@by", DiningDb.Db(pay.PaidBy));
                            cmd.Parameters.AddWithValue("@sp", DiningDb.DbStr(pay.SlipPath, 500));
                            cmd.Parameters.AddWithValue("@svs", DiningDb.DbStr(pay.SlipVerifyStatus, 30));
                            cmd.Parameters.AddWithValue("@sba", DiningDb.Db(pay.SlipBankAmount));
                            cmd.Parameters.AddWithValue("@str", DiningDb.DbStr(pay.SlipTransRef, 60));
                            cmd.Parameters.AddWithValue("@sn", DiningDb.DbStr(pay.SlipNote, 500));
                            cmd.Parameters.AddWithValue("@rcr", DiningDb.DbStr(pay.RoomChargeRef, 60));
                            cmd.Parameters.AddWithValue("@rid", DiningDb.Db(pay.ReservationId));
                            cmd.Parameters.AddWithValue("@cov", DiningDb.DbStr(DiningDb.JoinIds(pay.CoversOrderIds), 400));
                            cmd.Parameters.AddWithValue("@idem", DiningDb.Db(pay.IdempotencyKey));
                            pay.Id = Convert.ToInt64(cmd.ExecuteScalar());
                        }
                        string sql = settleStatus != null
                            ? "UPDATE dbo.Dining_Order SET Payment_Status=@ps, Payment_ID=@p, Pending_Payment_ID=NULL, Updated_At=GETDATE()"
                            : "UPDATE dbo.Dining_Order SET Pending_Payment_ID=@p, Updated_At=GETDATE()";
                        sql += " WHERE Session_ID=@s AND Order_Status <> 'CANCELLED' AND Payment_Status='UNPAID' AND Pending_Payment_ID IS NULL AND ID IN (";
                        var names = new List<string>();
                        using (var cmd = new SqlCommand("", con, tx))
                        {
                            for (int i = 0; i < pay.CoversOrderIds.Count; i++) { names.Add("@o" + i); cmd.Parameters.AddWithValue("@o" + i, pay.CoversOrderIds[i]); }
                            cmd.CommandText = sql + string.Join(",", names.ToArray()) + ")";
                            cmd.Parameters.AddWithValue("@p", pay.Id);
                            cmd.Parameters.AddWithValue("@s", pay.SessionId);
                            if (settleStatus != null) cmd.Parameters.AddWithValue("@ps", settleStatus);
                            int n = cmd.ExecuteNonQuery();
                            if (n != pay.CoversOrderIds.Count) { tx.Rollback(); error = "มีการชำระซ้อนกัน (ออเดอร์บางรายการถูกปิดไปแล้ว) กรุณารีเฟรชแล้วลองใหม่"; return false; }
                        }
                        tx.Commit();
                        pay.CreatedAt = DateTime.Now;
                        return true;
                    }
                    catch { try { tx.Rollback(); } catch { } throw; }
                }
            }
        }

        private bool ConfirmPendingPayment(DiningPayment pay, int staffId, string note, out string error)
        {
            error = null;
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        int n;
                        using (var cmd = new SqlCommand(@"UPDATE dbo.Dining_Payment SET Status='CONFIRMED', Paid_At=GETDATE(), Paid_By=@by, Paid_How_ID=@ph, Paid_How_Name=@phn,
                                Slip_Verify_Status=@svs, Slip_Bank_Amount=@sba, Slip_Trans_Ref=@str, Slip_Note=CASE WHEN @n IS NULL THEN Slip_Note ELSE LEFT(ISNULL(Slip_Note,'') + ' | ' + @n, 500) END, Updated_At=GETDATE()
                                WHERE ID=@id AND Status='PENDING_SLIP'", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@id", pay.Id);
                            cmd.Parameters.AddWithValue("@by", staffId > 0 ? (object)staffId : DBNull.Value);
                            cmd.Parameters.AddWithValue("@ph", DiningDb.Db(pay.PaidHowId));
                            cmd.Parameters.AddWithValue("@phn", DiningDb.DbStr(pay.PaidHowName, 150));
                            cmd.Parameters.AddWithValue("@svs", DiningDb.DbStr(pay.SlipVerifyStatus, 30));
                            cmd.Parameters.AddWithValue("@sba", DiningDb.Db(pay.SlipBankAmount));
                            cmd.Parameters.AddWithValue("@str", DiningDb.DbStr(pay.SlipTransRef, 60));
                            cmd.Parameters.AddWithValue("@n", DiningDb.DbStr(string.IsNullOrWhiteSpace(note) ? null : "ยืนยันโดยพนักงาน: " + note.Trim(), 200));
                            n = cmd.ExecuteNonQuery();
                        }
                        if (n <= 0) { tx.Rollback(); error = "รายการนี้ถูกตัดสินไปแล้ว"; return false; }
                        using (var cmd = new SqlCommand(@"UPDATE dbo.Dining_Order SET Payment_Status='PAID', Payment_ID=@p, Pending_Payment_ID=NULL, Updated_At=GETDATE()
                                WHERE Pending_Payment_ID=@p AND Payment_Status='UNPAID' AND Order_Status <> 'CANCELLED'", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@p", pay.Id);
                            cmd.ExecuteNonQuery();
                        }
                        // ออเดอร์ที่ถูกยกเลิกระหว่างรอตรวจ → ตัดออกจากชุดและลดยอด
                        using (var cmd = new SqlCommand("SELECT ID, Total_Amount FROM dbo.Dining_Order WHERE Payment_ID=@p", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@p", pay.Id);
                            var ids = new List<long>(); decimal sum = 0m;
                            using (var rd = cmd.ExecuteReader()) while (rd.Read()) { ids.Add(Convert.ToInt64(rd[0])); sum += Convert.ToDecimal(rd[1]); }
                            if (ids.Count == 0) { tx.Rollback(); error = "ไม่มีออเดอร์ค้างชำระที่ผูกกับสลิปนี้แล้ว (ถูกยกเลิก/จ่ายทางอื่น)"; return false; }
                            if (ids.Count != pay.CoversOrderIds.Count)
                            {
                                using (var up = new SqlCommand("UPDATE dbo.Dining_Payment SET Covers_Order_Ids=@c, Amount=@a, Slip_Note=LEFT(ISNULL(Slip_Note,'') + ' | ยอดปรับตามออเดอร์ที่เหลือ', 500) WHERE ID=@id", con, tx))
                                {
                                    up.Parameters.AddWithValue("@id", pay.Id); up.Parameters.AddWithValue("@c", DiningDb.JoinIds(ids)); up.Parameters.AddWithValue("@a", DiningPricing.R2(sum));
                                    up.ExecuteNonQuery();
                                }
                                pay.CoversOrderIds = ids; pay.Amount = DiningPricing.R2(sum);
                            }
                        }
                        tx.Commit();
                        pay.Status = DiningStatus.Payment.Confirmed; pay.PaidAt = DateTime.Now; pay.PaidBy = staffId > 0 ? staffId : (int?)null;
                        return true;
                    }
                    catch { try { tx.Rollback(); } catch { } throw; }
                }
            }
        }

        private void RevertPayment(DiningPayment pay, List<long> cover, string note)
        {
            try
            {
                var p = new Dictionary<string, object> { { "@p", pay.Id } };
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Order SET Payment_Status='UNPAID', Payment_ID=NULL, Updated_At=GETDATE() WHERE Payment_ID=@p AND Payment_Status IN ('PAID','CHARGED')", p);
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Payment SET Status='VOID', Slip_Note=@n, Updated_At=GETDATE() WHERE ID=@p",
                    new Dictionary<string, object> { { "@p", pay.Id }, { "@n", DiningDb.DbStr(note, 500) } });
                pay.Status = DiningStatus.Payment.Void;
            }
            catch (Exception ex) { DiningDb.Log(_conn, "RevertPayment #" + pay.Id + " ล้มเหลว: " + ex.Message, "SYSTEM"); }
        }

        /// <summary>ยกเลิกออเดอร์ที่ชำระแล้ว (ก่อนลงบัญชี): ตัดออกจากชุดของ payment และลดยอด — ยอด 0 = VOID</summary>
        internal static void DetachOrderFromPayment(string conn, DiningPayment pay, DiningOrder order, string note)
        {
            var ids = new List<long>(pay.CoversOrderIds);
            ids.Remove(order.Id);
            decimal amount = DiningPricing.R2(pay.Amount - order.TotalAmount);
            if (amount < 0m) amount = 0m;
            DiningDb.Exec(conn, @"UPDATE dbo.Dining_Payment SET Covers_Order_Ids=@c, Amount=@a, Status=CASE WHEN @a <= 0 THEN 'VOID' ELSE Status END,
                                  Slip_Note=LEFT(ISNULL(Slip_Note,'') + ' | ' + @n, 500), Updated_At=GETDATE() WHERE ID=@id AND Acct_Post_Ref IS NULL",
                new Dictionary<string, object> { { "@id", pay.Id }, { "@c", DiningDb.Db(DiningDb.JoinIds(ids)) }, { "@a", amount }, { "@n", DiningDb.DbStr(note, 300) } });
            pay.CoversOrderIds = ids; pay.Amount = amount;
            if (amount <= 0m) pay.Status = DiningStatus.Payment.Void;
        }

        // ═════════════════════════════════════════════════════════════════════
        // helpers
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>แหล่งเงิน: id ที่ให้ (ต้องใช้งาน) ไม่งั้นแถวแรกตามชนิด (ChannelPosting.PaidHowByType) — ไม่พบ = 0 + ชื่อชนิด</summary>
        private int ResolvePaidHow(int paidHowId, string channelType, out string name)
        {
            name = null;
            if (paidHowId > 0)
            {
                try
                {
                    var dt = DiningDb.Query(_conn, "SELECT Paid_How FROM Account_Paid_How WHERE ID = @id AND ISNULL(CAST(Status AS NVARCHAR(10)),'True') IN ('True','1')",
                        new Dictionary<string, object> { { "@id", paidHowId } });
                    if (dt.Rows.Count > 0) { name = DiningDb.Str(dt.Rows[0]["Paid_How"]); return paidHowId; }
                }
                catch { }
            }
            try
            {
                string n;
                int id = ChannelPosting.PaidHowByType(_conn, channelType, channelType == "CASH" ? "เงินสด" : "เงินโอน", out n);
                if (id > 0) { name = n; return id; }
            }
            catch { }
            name = channelType == "CASH" ? "เงินสด" : "เงินโอน";
            return 0;
        }

        private List<string> OrderNos(IList<long> ids)
        {
            var list = new List<string>();
            try
            {
                if (ids == null || ids.Count == 0) return list;
                var p = new Dictionary<string, object>();
                var dt = DiningDb.Query(_conn, "SELECT Order_No FROM dbo.Dining_Order WHERE ID IN (" + DiningDb.InList(ids, p) + ") ORDER BY ID", p);
                foreach (DataRow r in dt.Rows) list.Add(DiningDb.Str(r["Order_No"]));
            }
            catch { }
            return list;
        }

        /// <summary>บันทึกไฟล์สลิปจาก bytes ด้วยกติกาเดียวกับ UploadHelper.Save (ชื่อไฟล์สร้างเอง, whitelist นามสกุล, ≤ 8 MB, ตรวจ magic bytes)</summary>
        internal static bool SaveSlipBytes(byte[] bytes, string fileName, out string webPath, out string error)
        {
            webPath = null; error = null;
            if (bytes == null || bytes.Length == 0) { error = "ไม่พบไฟล์ที่แนบมา"; return false; }
            if (bytes.Length > 8 * 1024 * 1024) { error = "ไฟล์ใหญ่เกิน 8 MB"; return false; }
            string ext = (Path.GetExtension(fileName ?? "") ?? "").ToLowerInvariant();
            string sniffed = SniffExt(bytes);
            if (sniffed == null) { error = "ไฟล์ไม่ใช่รูปภาพ/PDF"; return false; }
            if (string.IsNullOrEmpty(ext) || Array.IndexOf(UploadHelper.ImageDoc, ext) < 0) ext = sniffed;
            try
            {
                string abs = HostingEnvironment.MapPath(DiningConfig.SlipFolder);
                if (string.IsNullOrEmpty(abs)) { error = "หาโฟลเดอร์เก็บสลิปไม่พบ"; return false; }
                Directory.CreateDirectory(abs);
                string name = DiningConfig.SlipPrefix + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N") + ext;
                File.WriteAllBytes(Path.Combine(abs, name), bytes);
                webPath = DiningConfig.SlipFolder.TrimStart('~').TrimEnd('/') + "/" + name;
                return true;
            }
            catch (Exception ex) { error = "บันทึกไฟล์ไม่สำเร็จ: " + ex.Message; return false; }
        }

        private static string SniffExt(byte[] b)
        {
            if (b.Length < 12) return null;
            if (b[0] == 0xFF && b[1] == 0xD8) return ".jpg";
            if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
            if (b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return ".gif";
            if (b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return ".webp";
            if (b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46) return ".pdf";
            return null;
        }

        private static string SafeContact()
        {
            try { return SlipVerificationService.CustomerContactText(); } catch { return ""; }
        }

        private static string Cut(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static void Fail(DiningResult r, string code, string message)
        {
            r.Ok = false; r.ErrorCode = code; r.Message = message;
        }

        internal static DiningPayment ReadPayment(DataRow r)
        {
            return new DiningPayment
            {
                Id = DiningDb.Long(r["ID"]), SessionId = DiningDb.Long(r["Session_ID"]), Method = DiningDb.Str(r["Method"]), Status = DiningDb.Str(r["Status"]),
                Amount = DiningDb.Dec(r["Amount"]), PaidHowId = DiningDb.NInt(r["Paid_How_ID"]), PaidHowName = DiningDb.Str(r["Paid_How_Name"]),
                PaidAt = DiningDb.NDate(r["Paid_At"]), PaidBy = DiningDb.NInt(r["Paid_By"]), SlipPath = DiningDb.Str(r["Slip_Path"]), SlipVerifyStatus = DiningDb.Str(r["Slip_Verify_Status"]),
                SlipBankAmount = DiningDb.NDec(r["Slip_Bank_Amount"]), SlipTransRef = DiningDb.Str(r["Slip_Trans_Ref"]), SlipNote = DiningDb.Str(r["Slip_Note"]),
                RoomChargeRef = DiningDb.Str(r["Room_Charge_Ref"]), ReservationId = DiningDb.NInt(r["Reservation_ID"]), CoversOrderIds = DiningDb.ParseIds(DiningDb.Str(r["Covers_Order_Ids"])),
                AcctPostRef = DiningDb.Str(r["Acct_Post_Ref"]), IdempotencyKey = DiningDb.Str(r["Idempotency_Key"]), CreatedAt = DiningDb.Date(r["Created_At"]), UpdatedAt = DiningDb.NDate(r["Updated_At"])
            };
        }
    }
}
