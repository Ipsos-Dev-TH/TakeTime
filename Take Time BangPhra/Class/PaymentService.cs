using System;
using System.Collections.Generic;
using System.Data;
using System.Web;
using System.IO;
using System.Configuration;
using Take_Time_BangPhra.Services;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// Payment Service - Business logic for payment processing
    /// </summary>
    public class PaymentService
    {
        private readonly string _connectionString;
        private readonly PaymentDataAccess _paymentDA;
        private readonly ReservationDataAccess _reservationDA;
        private readonly code _code;
        public PaymentService(string connectionString)
        {
            _connectionString = connectionString;
            _paymentDA = new PaymentDataAccess(connectionString);
            _reservationDA = new ReservationDataAccess(connectionString);
            _code = new code();
        }

        #region Payment Processing

        /// <summary>
        /// Process additional payment for a reservation
        /// </summary>
        public PaymentResult ProcessAdditionalPayment(
            int reservationId,
            decimal amount,
            string paymentMethod,
            HttpPostedFile slipFile = null,
            int? adminId = null,
            string customerPhone = null,
            string notes = null)
        {
            try
            {
                // 1. Validate reservation
                var reservation = _reservationDA.GetReservationByIdAndPhone(reservationId, customerPhone ?? "");
                if (reservation == null || reservation.Rows.Count == 0)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "ไม่พบการจองนี้"
                    };
                }

                // 2. Calculate remaining balance
                decimal remaining = _paymentDA.GetRemainingBalance(reservationId);
                if (amount > remaining + 0.01m) // Allow small rounding
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = $"จำนวนเงินเกินยอดค้างชำระ (ค้าง: {remaining:N2} บาท)"
                    };
                }

                if (amount <= 0)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "จำนวนเงินต้องมากกว่า 0"
                    };
                }

                // 3. Upload slip (if provided)
                long? slipId = null;
                if (slipFile != null && slipFile.ContentLength > 0)
                {
                    slipId = UploadPaymentSlip(reservationId, slipFile, customerPhone, adminId);
                }

                // 4. Create receipt
                string receiptId = CreatePaymentReceipt(
                    reservationId,
                    amount,
                    paymentMethod,
                    isDeposit: false
                );

                // 5. Record payment
                long paymentId = _paymentDA.RecordPaymentDirect(
                    reservationId,
                    amount,
                    "ADDITIONAL", // Payment type
                    paymentMethod,
                    receiptId,
                    slipId,
                    customerPhone,
                    adminId,
                    notes
                );

                // 6. Update reservation deposit amount
                decimal newDepositAmount = _paymentDA.GetTotalPaidAmount(reservationId);
                UpdateReservationDeposit(reservationId, newDepositAmount);

                // 7. Send email confirmation
                try
                {
                    SendPaymentConfirmationEmail(reservationId, receiptId, amount);
                }
                catch (Exception emailEx)
                {
                    // Log but don't fail the payment
                    _code.Logs(_connectionString, "Email Error", emailEx.Message, "SYSTEM");
                }

                // 8. Auto-sync receipt to accounting
                try
                {
                    var config = new AccountingConfig(_connectionString);
                    if (config.IsConfigured && config.Enabled)
                    {
                        string custName = GetCustomerName(reservationId);
                        var sync = new AccountingSyncService(_connectionString);
                        string psPayAccId = sync.LookupPaidHowAccountId(paymentMethod);
                        decimal payVat = ComputeVatFromGross(amount);
                        if (config.IsDocumentMode)
                        {
                            sync.EnqueueReceipt(reservationId, receiptId, amount, payVat, DateTime.Now, custName,
                                isDeposit: false, paymentMethod: paymentMethod,
                                revenueType: "ROOM_REVENUE", paymentAccountId: psPayAccId);
                        }
                        else if (!string.IsNullOrEmpty(receiptId) && receiptId != "0")
                        {
                            sync.EnqueueReceipt(reservationId, receiptId, amount, payVat, DateTime.Now, custName,
                                isDeposit: false, paymentMethod: paymentMethod,
                                revenueType: "ROOM_REVENUE", paymentAccountId: psPayAccId);
                        }
                    }
                }
                catch (Exception accEx)
                {
                    _code.Logs(_connectionString, "Accounting Sync", "Receipt auto-sync error: " + accEx.Message, "SYSTEM");
                }

                return new PaymentResult
                {
                    Success = true,
                    Message = "ชำระเงินสำเร็จ",
                    PaymentId = paymentId,
                    ReceiptId = receiptId,
                    RemainingBalance = remaining - amount
                };
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Payment Error", ex.Message + " - " + ex.StackTrace, "SYSTEM");
                return new PaymentResult
                {
                    Success = false,
                    Message = "เกิดข้อผิดพลาด: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Process initial deposit payment
        /// </summary>
        public PaymentResult ProcessDepositPayment(
            int reservationId,
            decimal depositAmount,
            string paymentMethod,
            HttpPostedFile slipFile,
            string customerPhone,
            int? adminId = null)
        {
            try
            {
                // 1. Validate
                if (depositAmount <= 0)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "จำนวนเงินมัดจำต้องมากกว่า 0"
                    };
                }

                // 2. Upload slip (REQUIRED for deposit)
                if (slipFile == null || slipFile.ContentLength == 0)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "กรุณาแนบสลิปการชำระเงิน"
                    };
                }

                long slipId = UploadPaymentSlip(reservationId, slipFile, customerPhone, adminId);

                // 3. Determine if this is truly a deposit or a full payment
                bool isDeposit = true;
                try
                {
                    var dtCheck = _code.DatabaseQuerySafe(_connectionString,
                        @"SELECT ISNULL(R.TotalPrice, 0) AS TotalPrice,
                                 ISNULL((SELECT SUM(Total_Amount) FROM Account_Receipt
                                         WHERE Reservation_ID = @id AND IsDeposit = 1
                                           AND (Status = 'Normal' OR Status IS NULL)), 0) AS PriorDeposits
                          FROM Reservation R WHERE R.ID = @id",
                        new Dictionary<string, object> { { "@id", reservationId } });
                    if (dtCheck?.Rows.Count > 0)
                    {
                        decimal totalPrice = Convert.ToDecimal(dtCheck.Rows[0]["TotalPrice"]);
                        decimal priorDeposits = Convert.ToDecimal(dtCheck.Rows[0]["PriorDeposits"]);
                        if (totalPrice > 0 && (priorDeposits + depositAmount) >= totalPrice)
                            isDeposit = false;
                    }
                }
                catch { }

                // 4. Create receipt
                string receiptId = CreatePaymentReceipt(
                    reservationId,
                    depositAmount,
                    paymentMethod,
                    isDeposit: isDeposit
                );

                // 5. Record payment
                string paymentType = isDeposit ? "DEPOSIT" : "FULL";
                long paymentId = _paymentDA.RecordPaymentDirect(
                    reservationId,
                    depositAmount,
                    paymentType,
                    paymentMethod,
                    receiptId,
                    slipId,
                    customerPhone,
                    adminId,
                    isDeposit ? "มัดจำการจอง" : "ชำระเต็มจำนวน"
                );

                // 6. Update reservation
                UpdateReservationDeposit(reservationId, depositAmount);
                UpdateReservationStatus(reservationId, isDeposit ? "มัดจำแล้ว" : "ชำระแล้ว");

                // 6. Send confirmation
                try
                {
                    SendPaymentConfirmationEmail(reservationId, receiptId, depositAmount);
                }
                catch { }

                // 7. Auto-sync receipt to accounting
                try
                {
                    var config = new AccountingConfig(_connectionString);
                    if (config.IsConfigured && config.Enabled)
                    {
                        string custName = GetCustomerName(reservationId);
                        var sync = new AccountingSyncService(_connectionString);
                        string depPayAccId = sync.LookupPaidHowAccountId(paymentMethod);
                        sync.EnqueueReceipt(reservationId, receiptId, depositAmount, ComputeVatFromGross(depositAmount), DateTime.Now, custName,
                            isDeposit: isDeposit, paymentMethod: paymentMethod, paymentAccountId: depPayAccId);
                    }
                }
                catch (Exception accEx)
                {
                    _code.Logs(_connectionString, "Accounting Sync", "Receipt auto-sync error: " + accEx.Message, "SYSTEM");
                }

                return new PaymentResult
                {
                    Success = true,
                    Message = isDeposit ? "ชำระเงินมัดจำสำเร็จ" : "ชำระเงินเต็มจำนวนสำเร็จ",
                    PaymentId = paymentId,
                    ReceiptId = receiptId,
                    RemainingBalance = _paymentDA.GetRemainingBalance(reservationId)
                };
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Deposit Payment Error", ex.Message, "SYSTEM");
                return new PaymentResult
                {
                    Success = false,
                    Message = "เกิดข้อผิดพลาด: " + ex.Message
                };
            }
        }

        /// <summary>
        /// รับชำระของการจองจากหน้า Payment/Pay และเกตเวย์ออนไลน์ (OnlinePaymentService.ApplyToReservation)
        ///
        /// ต่างจาก <see cref="ProcessAdditionalPayment"/> ตรงที่ใช้ "ยอดกลาง" <see cref="ReservationBalance"/>
        /// (ค่าห้อง + ค่าใช้จ่ายในห้อง เช่น ค่าบริการสัตว์เลี้ยง PRE_BOOKING) แทนยอดค่าห้องล้วน:
        ///   • ยอดเกินยอดค้างกลาง → ไม่รับ (เกตเวย์: เงินค้างไว้ให้พนักงานตรวจ/คืน ตามเดิม)
        ///   • ครบยอดค้าง (เผื่อเศษ Balance_Rounding_Tolerance) = ชำระเต็ม → ปิดค่าใช้จ่ายในห้องที่ค้างทั้งหมด
        ///   • ไม่ครบ → เงินตัดค่าห้องก่อน ส่วนที่เกินค่าห้องค่อยปิดค่าใช้จ่ายในห้อง "เก่าสุดก่อน" เฉพาะแถวที่ยอดพอ
        ///     (มัดจำไม่ทำให้ค่าสัตว์เลี้ยงกลายเป็นชำระแล้ว — เก็บตอนเช็คอินเหมือนใบมัดจำของหน้าจอง)
        ///   • ไม่ครบ + ยังไม่เช็คอิน = ใบรับมัดจำ (IsDeposit) บรรทัดเดียวแบบ CreateDepositReceipt — กฎเดียวกับหน้าแก้ไขการจอง
        ///     (Reserve.aspx.cs: IsDeposit = Deposit &lt; totalPrice) และ CLAUDE.md 6b (รับก่อนเข้าพักไม่ครบ = มัดจำ → Cr เงินรับล่วงหน้า).
        ///     ค่าใช้จ่ายในห้องที่ยอดครอบคลุมยังถูกปิด (Receipt_ID = ใบมัดจำ) เหมือน Reserve (MarkChargesPaidUpTo หลังออกใบมัดจำ)
        ///     ⇒ รายได้ค่าใช้จ่ายรับรู้ตอนเช็คเอาท์ผ่านการล้างมัดจำ ไม่รับรู้ก่อนเข้าพัก
        ///   • อื่น ๆ (ครบยอด หรือเช็คอินแล้ว) = ใบเสร็จมีบรรทัดค่าห้อง + บรรทัดค่าใช้จ่ายที่ปิด (แบบ AddProductChargesToReceipt) ยอดรวม = ยอดรับ
        ///     รายได้แยกตามบรรทัด: บรรทัดมี ProductType_ID (1 ห้อง / 3 ค่าใช้จ่ายในห้อง) → mapping รายได้ของประเภทนั้น
        ///     (LookupReceiptLinesEx ใช้ revenueType เป็น override เฉพาะบรรทัดที่ไม่มี ProductType_ID/กรณีไม่มีบรรทัด)
        /// การปิดค่าใช้จ่ายเขียนแบบ WHERE Status = 'PENDING' ต่อแถว ⇒ เรียกซ้ำไม่ปิดซ้ำ
        /// (กันซ้ำระดับรายการเกตเวย์อยู่ที่ Payment_Transaction.Applied_At อยู่แล้ว)
        /// อ่านยอดกลางไม่ได้ → ถอยไปใช้ ProcessAdditionalPayment แบบเดิม
        /// </summary>
        public PaymentResult ProcessReservationPayment(
            int reservationId,
            decimal amount,
            string paymentMethod,
            HttpPostedFile slipFile = null,
            int? adminId = null,
            string customerPhone = null,
            string notes = null,
            long? existingSlipId = null)
        {
            try
            {
                var reservation = _reservationDA.GetReservationByIdAndPhone(reservationId, customerPhone ?? "");
                if (reservation == null || reservation.Rows.Count == 0)
                    return new PaymentResult { Success = false, Message = "ไม่พบการจองนี้" };
                if (amount <= 0)
                    return new PaymentResult { Success = false, Message = "จำนวนเงินต้องมากกว่า 0" };

                string status = reservation.Columns.Contains("Status") && reservation.Rows[0]["Status"] != DBNull.Value
                    ? Convert.ToString(reservation.Rows[0]["Status"]).Trim() : "";
                if (status.StartsWith("ยกเลิก", StringComparison.Ordinal) || status.StartsWith("ลบ", StringComparison.Ordinal)
                    || status == "Cancel" || status == "CANCELLED")
                {
                    // ไม่ลงรับเงินเข้าใบจองที่ยกเลิกแล้ว (เช่น ระบบยกเลิกใบที่ค้างชำระระหว่างลูกค้ากำลังจ่าย)
                    // เงินเกตเวย์จะค้างเป็น "จ่ายแล้วแต่ยังไม่บันทึก" ให้เจ้าหน้าที่ตรวจ/คืนเงิน
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "การจองนี้ถูกยกเลิกแล้ว (" + status + ") — ต้องตรวจสอบ/คืนเงินลูกค้า"
                    };
                }

                ReservationBalance bal = null;
                try { bal = ReservationBalance.Load(_connectionString, reservationId); } catch { bal = null; }
                if (bal == null)
                    return ProcessAdditionalPayment(reservationId, amount, paymentMethod, slipFile, adminId, customerPhone, notes);

                decimal tol = Math.Max(0.01m, ReservationBalance.RoundingTolerance);
                decimal due = bal.Due;
                if (amount > due + tol)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = $"จำนวนเงินเกินยอดค้างชำระ (ค้าง: {due:N2} บาท)"
                    };
                }

                // ── ค่าใช้จ่ายในห้องที่ยอดนี้ปิดได้ (เก่าสุดก่อน) ──
                DataTable pending = LoadPendingChargesOldestFirst(reservationId);
                decimal pendingTotal = 0m;
                foreach (DataRow pr in pending.Rows) pendingTotal += ToDec(pr["TotalAmount"]);

                bool coversAll = amount >= due - tol;
                decimal nonChargeDue = due - pendingTotal;
                if (nonChargeDue < 0m) nonChargeDue = 0m;
                decimal budget = amount - nonChargeDue;

                var covered = new List<DataRow>();
                decimal coveredTotal = 0m;
                foreach (DataRow pr in pending.Rows)
                {
                    decimal a = ToDec(pr["TotalAmount"]);
                    if (coversAll)
                    {
                        covered.Add(pr);
                        coveredTotal += a;
                        continue;
                    }
                    if (a <= budget + 0.01m)
                    {
                        covered.Add(pr);
                        coveredTotal += a;
                        budget -= a;
                    }
                    else break;   // ต้องปิดตามลำดับ — แถวเก่ายังไม่พอ ห้ามข้ามไปปิดแถวใหม่
                }

                bool arrived = status == "เช็คอินแล้ว" || status == "เสร็จสิ้น"
                               || status.StartsWith("เช็คเอาท์", StringComparison.Ordinal)
                               || status.StartsWith("เช็คเอ้าท์", StringComparison.Ordinal);
                // รับก่อนเข้าพักแต่ไม่ครบยอด = มัดจำเสมอ (เดิมมีเงื่อนไข covered.Count == 0 → ยอดที่เผอิญปิดค่าบริการได้
                // กลายเป็นใบเสร็จรายได้ก่อนเข้าพัก ไม่ตรงกับหน้าแก้ไขการจองที่ใช้ Deposit < totalPrice)
                bool isDeposit = !coversAll && !arrived;

                // ── สลิป / ใบเสร็จ ──
                long? slipId = existingSlipId;   // สลิปที่ลูกค้าแนบไว้แล้วและพนักงานเพิ่งอนุมัติ (ApproveReservationSlip)
                if (!slipId.HasValue && slipFile != null && slipFile.ContentLength > 0)
                    slipId = UploadPaymentSlip(reservationId, slipFile, customerPhone, adminId);

                string receiptId = GenerateReceiptId();
                decimal vat = ComputeVatFromGross(amount);
                _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO Account_Receipt (
                        ID, UID, Reservation_ID, Created_Date, Total_Amount, Vat,
                        Total_Amount_Exclude_Vat, IsDeposit, UseDeposit, Paid_Type,
                        Status, Etax, Created_By_ID
                      )
                      VALUES (
                        @receiptId, @uid, @reservationId, @createdDate, @totalAmount, @vat,
                        @totalExcludeVat, @isDeposit, @useDeposit, @paidType,
                        'Normal', @etax, @createdBy
                      )",
                    new Dictionary<string, object>
                    {
                        { "@receiptId", receiptId },
                        { "@uid", Guid.NewGuid().ToString("N") },
                        { "@reservationId", reservationId },
                        { "@createdDate", DateTime.Now },
                        { "@totalAmount", amount },
                        { "@vat", vat },
                        { "@totalExcludeVat", amount - vat },
                        { "@isDeposit", isDeposit },
                        { "@useDeposit", false },
                        { "@etax", false },
                        { "@paidType", paymentMethod },
                        { "@createdBy", adminId.HasValue ? (object)adminId.Value.ToString() : DBNull.Value }
                    });

                // บรรทัดใบเสร็จเป็นส่วนเสริม — ล้มแล้วไม่ทิ้งการรับเงินกลางทาง (หัวใบเสร็จออกไปแล้ว ถ้าล้มทั้งก้อน
                // เกตเวย์จะลองใหม่แล้วได้ใบเสร็จซ้ำ) ⇒ log ไว้ให้ตรวจ
                try
                {
                    InsertReservationPaymentLines(receiptId, reservationId, amount, isDeposit, covered, coveredTotal);
                }
                catch (Exception lineEx)
                {
                    _code.Logs(_connectionString, "Payment Receipt Lines",
                        $"ใบเสร็จ {receiptId} การจอง {reservationId}: บันทึกบรรทัดไม่สำเร็จ — {lineEx.Message}", "SYSTEM");
                }

                // ── Payment_History ──
                decimal remainingAfter = due - amount;
                if (remainingAfter < 0m) remainingAfter = 0m;
                string paymentType = isDeposit ? "DEPOSIT" : (coversAll ? "FULL" : "ADDITIONAL");
                long paymentId = _code.DatabaseInsertReturnSafe(_connectionString,
                    @"INSERT INTO Payment_History (
                        Reservation_ID, PaymentDate, PaymentAmount, PaymentType, PaymentMethod,
                        Receipt_ID, PaymentSlip_ID, RemainingBalance, PaidBy_CustomerPhone,
                        ProcessedBy_AdminID, Notes, Status
                      )
                      VALUES (
                        @reservationId, GETDATE(), @amount, @paymentType, @paymentMethod,
                        @receiptId, @paymentSlipId, @remaining, @paidBy,
                        @processedBy, @notes, 'COMPLETED'
                      );
                      SELECT SCOPE_IDENTITY();",
                    new Dictionary<string, object>
                    {
                        { "@reservationId", reservationId },
                        { "@amount", amount },
                        { "@paymentType", paymentType },
                        { "@paymentMethod", paymentMethod },
                        { "@receiptId", receiptId },
                        { "@paymentSlipId", slipId },
                        { "@remaining", remainingAfter },
                        { "@paidBy", customerPhone },
                        { "@processedBy", adminId },
                        { "@notes", notes }
                    });

                // ── ปิดค่าใช้จ่ายในห้องที่เงินก้อนนี้ครอบคลุม (ทีละแถว เฉพาะที่ยัง PENDING) ──
                int closed = 0;
                foreach (DataRow pr in covered)
                {
                    try
                    {
                        closed += _code.DatabaseInsertSafe(_connectionString,
                            @"UPDATE Reservation_Product_Charges
                                 SET Status = 'PAID', IsPaid = 1, Receipt_ID = @receiptId, PaymentDate = GETDATE()
                               WHERE ID = @id AND Status = 'PENDING'",
                            new Dictionary<string, object>
                            {
                                { "@receiptId", receiptId },
                                { "@id", Convert.ToInt64(pr["ID"]) }
                            });
                    }
                    catch (Exception chEx)
                    {
                        _code.Logs(_connectionString, "Payment Charges",
                            $"ปิดค่าใช้จ่าย #{pr["ID"]} ของการจอง {reservationId} ไม่สำเร็จ: {chEx.Message}", "SYSTEM");
                    }
                }

                UpdateReservationDeposit(reservationId, _paymentDA.GetTotalPaidAmount(reservationId));

                try { SendPaymentConfirmationEmail(reservationId, receiptId, amount); }
                catch (Exception emailEx) { _code.Logs(_connectionString, "Email Error", emailEx.Message, "SYSTEM"); }

                try
                {
                    var config = new AccountingConfig(_connectionString);
                    if (config.IsConfigured && config.Enabled)
                    {
                        var sync = new AccountingSyncService(_connectionString);
                        // revenueType = fallback เท่านั้น: บรรทัดค่าใช้จ่ายในห้อง (ProductType_ID 3 เช่น ค่าสัตว์เลี้ยง)
                        // ได้บัญชีรายได้ตามประเภทของบรรทัดเอง ไม่ถูกตีเป็น ROOM_REVENUE (ดู LookupReceiptLinesEx)
                        sync.EnqueueReceipt(reservationId, receiptId, amount, vat, DateTime.Now, GetCustomerName(reservationId),
                            isDeposit: isDeposit, paymentMethod: paymentMethod,
                            revenueType: "ROOM_REVENUE", paymentAccountId: sync.LookupPaidHowAccountId(paymentMethod));
                    }
                }
                catch (Exception accEx)
                {
                    _code.Logs(_connectionString, "Accounting Sync", "Receipt auto-sync error: " + accEx.Message, "SYSTEM");
                }

                _code.Logs(_connectionString, "Payment",
                    $"การจอง {reservationId}: รับ {amount:N2} ({paymentType}) ใบเสร็จ {receiptId} · ยอดค้างก่อนรับ {due:N2}"
                    + (covered.Count > 0 ? $" · ปิดค่าใช้จ่ายในห้อง {closed}/{covered.Count} แถว ({coveredTotal:N2})" : ""),
                    adminId.HasValue ? adminId.Value.ToString() : "SYSTEM");

                return new PaymentResult
                {
                    Success = true,
                    Message = isDeposit ? "ชำระเงินมัดจำสำเร็จ" : (coversAll ? "ชำระเงินครบแล้ว" : "ชำระเงินสำเร็จ"),
                    PaymentId = paymentId,
                    ReceiptId = receiptId,
                    RemainingBalance = remainingAfter
                };
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Payment Error", ex.Message + " - " + ex.StackTrace, "SYSTEM");
                return new PaymentResult { Success = false, Message = "เกิดข้อผิดพลาด: " + ex.Message };
            }
        }

        // ── สลิปที่ลูกค้าแนบเอง → รอพนักงานตรวจก่อนลงรับเงิน ────────────────────────
        //  เดิมหน้า /Payment/Pay (สแกน QR แนบสลิป) เรียก ProcessReservationPayment ทันที = Payment_History COMPLETED
        //  + ใบเสร็จ + ส่งบัญชี + เลื่อนใบจองเป็น "มัดจำแล้ว" ทั้งที่ยังไม่มีใครดูสลิป (สลิปปลอม/ยอดไม่ตรง = ยืนยันห้องฟรี)
        //  ตอนนี้: เก็บสลิป (Payment_Slips VerificationStatus = PENDING) + ยอดที่แจ้ง (Claimed_Amount, PHASE19_25) เท่านั้น
        //  ใบจองคง "รอชำระเงิน" (ตัวกวาดยกเลิกอัตโนมัติข้ามใบที่มีสลิปรอตรวจ — BookingPayment.CancelStaleUnpaidIfDue)
        //  พนักงานอนุมัติที่ Account/SlipVerification → ApproveReservationSlip = ลงรับเงินจริงด้วยสลิปเดิม + PromoteIfPending

        private static bool? _hasSlipClaim;
        private static DateTime _slipClaimCheckedAt = DateTime.MinValue;

        /// <summary>มีคอลัมน์ Payment_Slips.Claimed_Amount (PHASE19_25) — cache; ไม่มีตรวจใหม่ทุก 5 นาที</summary>
        public bool HasSlipClaimColumn()
        {
            if (_hasSlipClaim == true) return true;
            if (_hasSlipClaim == false && DateTime.Now - _slipClaimCheckedAt < TimeSpan.FromMinutes(5)) return false;
            bool has = false;
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT COL_LENGTH('Payment_Slips', 'Claimed_Amount') AS C", null);
                has = dt != null && dt.Rows.Count > 0 && dt.Rows[0]["C"] != DBNull.Value;
            }
            catch { has = false; }
            _hasSlipClaim = has;
            _slipClaimCheckedAt = DateTime.Now;
            return has;
        }

        /// <summary>
        /// ลูกค้าแนบสลิปโอนค่าจองเอง — เก็บเป็น "รอตรวจ" ไม่สร้าง Payment_History/ใบเสร็จ/บัญชี และไม่เลื่อนสถานะใบจอง
        /// คืน null = ยังไม่รัน PHASE19_25 (ไม่มี Claimed_Amount) → ผู้เรียกตัดสินใจเอง (ห้าม PromoteIfPending)
        /// </summary>
        public PaymentResult SubmitReservationSlipForVerification(int reservationId, decimal amount,
            HttpPostedFile slipFile, string customerPhone, string notes)
        {
            if (!HasSlipClaimColumn()) return null;
            try
            {
                var reservation = _reservationDA.GetReservationByIdAndPhone(reservationId, customerPhone ?? "");
                if (reservation == null || reservation.Rows.Count == 0)
                    return new PaymentResult { Success = false, Message = "ไม่พบการจองนี้" };
                if (amount <= 0)
                    return new PaymentResult { Success = false, Message = "จำนวนเงินต้องมากกว่า 0" };
                if (slipFile == null || slipFile.ContentLength <= 0)
                    return new PaymentResult { Success = false, Message = "กรุณาแนบสลิปการโอนเงิน" };

                string status = reservation.Columns.Contains("Status") && reservation.Rows[0]["Status"] != DBNull.Value
                    ? Convert.ToString(reservation.Rows[0]["Status"]).Trim() : "";
                if (status.StartsWith("ยกเลิก", StringComparison.Ordinal) || status.StartsWith("ลบ", StringComparison.Ordinal)
                    || status == "Cancel" || status == "CANCELLED")
                    return new PaymentResult { Success = false, Message = "การจองนี้ถูกยกเลิกแล้ว กรุณาติดต่อเจ้าหน้าที่" };

                ReservationBalance bal = null;
                try { bal = ReservationBalance.Load(_connectionString, reservationId); } catch { bal = null; }
                if (bal != null && amount > bal.Due + Math.Max(0.01m, ReservationBalance.RoundingTolerance))
                    return new PaymentResult { Success = false, Message = $"จำนวนเงินเกินยอดค้างชำระ (ค้าง: {bal.Due:N2} บาท)" };

                long slipId = UploadPaymentSlip(reservationId, slipFile, customerPhone, null);
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Payment_Slips SET Claimed_Amount = @amt, Notes = @notes WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@id", slipId }, { "@amt", amount },
                        { "@notes", string.IsNullOrEmpty(notes) ? (object)DBNull.Value : (notes.Length > 1000 ? notes.Substring(0, 1000) : notes) }
                    });

                _code.Logs(_connectionString, "Payment Slip",
                    $"การจอง {reservationId}: ลูกค้าแนบสลิป #{slipId} ยอด {amount:N2} — รอเจ้าหน้าที่ตรวจ (ยังไม่ลงรับเงิน/ใบเสร็จ/บัญชี)", "SYSTEM");
                try
                {
                    Notify.Send(Notify.Ev.PaymentOnline,
                        "🧾 <b>ลูกค้าแนบสลิปโอน รอตรวจ</b> " + amount.ToString("N2") + " บาท\nการจอง #" + reservationId
                        + "\nตรวจ/อนุมัติที่หน้า ตรวจสอบสลิป — อนุมัติแล้วระบบจึงลงรับเงิน ออกใบเสร็จ และยืนยันการจอง");
                }
                catch { }

                return new PaymentResult
                {
                    Success = true,
                    Message = "ได้รับสลิปแล้ว รอเจ้าหน้าที่ตรวจสอบ",
                    PaymentId = 0,
                    ReceiptId = null,
                    RemainingBalance = bal != null ? bal.Due : 0m
                };
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Payment Slip Error", $"การจอง {reservationId}: {ex.Message}", "SYSTEM");
                return new PaymentResult { Success = false, Message = "บันทึกสลิปไม่สำเร็จ: " + ex.Message };
            }
        }

        /// <summary>
        /// พนักงานอนุมัติสลิปที่ลูกค้าแนบ (SubmitReservationSlipForVerification) → ลงรับเงินจริงด้วยสลิปเดิม
        /// (Payment_History / ใบเสร็จ / บัญชี ผ่าน ProcessReservationPayment) แล้วเลื่อนใบจอง "รอชำระเงิน" → "มัดจำแล้ว"
        /// ชิงสถานะ PENDING → APPROVED แบบ atomic ก่อน (กันกดซ้ำ/สองคนกดพร้อมกันลงเงินซ้ำ) — ลงเงินไม่ผ่านคืนเป็น PENDING
        /// คืน null = ไม่ใช่สลิปแบบรอลงเงิน (ไม่มี Claimed_Amount / ลงเงินไปแล้ว / ยังไม่รัน migration) → ผู้เรียกอนุมัติแบบเดิม
        /// </summary>
        public PaymentResult ApproveReservationSlip(long slipId, int? adminId)
        {
            if (!HasSlipClaimColumn()) return null;
            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ps.Reservation_ID, ps.Claimed_Amount, ps.VerificationStatus, CAST(ps.Notes AS NVARCHAR(1000)) AS Notes,
                         r.Customer_MobilePhone,
                         CASE WHEN EXISTS (SELECT 1 FROM Payment_History ph WHERE ph.PaymentSlip_ID = ps.ID) THEN 1 ELSE 0 END AS Linked
                    FROM Payment_Slips ps
                    LEFT JOIN Reservation r ON r.ID = ps.Reservation_ID
                   WHERE ps.ID = @id",
                new Dictionary<string, object> { { "@id", slipId } });
            if (dt == null || dt.Rows.Count == 0) return null;
            DataRow s0 = dt.Rows[0];
            decimal claimed = ToDec(s0["Claimed_Amount"]);
            if (claimed <= 0m || Convert.ToInt32(s0["Linked"]) > 0 || s0["Reservation_ID"] == DBNull.Value) return null;
            if (!string.Equals(Convert.ToString(s0["VerificationStatus"]), "PENDING", StringComparison.OrdinalIgnoreCase))
                return new PaymentResult { Success = false, Message = "สลิปนี้ถูกตรวจไปแล้ว (" + Convert.ToString(s0["VerificationStatus"]) + ")" };

            int reservationId = Convert.ToInt32(s0["Reservation_ID"]);
            int claimedN = _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Payment_Slips
                     SET VerificationStatus = 'APPROVED', IsVerified = 1, VerifiedBy_ID = @by, VerifiedDate = GETDATE()
                   WHERE ID = @id AND VerificationStatus = 'PENDING'",
                new Dictionary<string, object> { { "@id", slipId }, { "@by", adminId.HasValue ? (object)adminId.Value : DBNull.Value } });
            if (claimedN <= 0)
                return new PaymentResult { Success = false, Message = "สลิปนี้กำลังถูกตรวจ/ถูกตรวจไปแล้ว — รีเฟรชหน้า" };

            PaymentResult pr;
            try
            {
                string notes = s0["Notes"] == DBNull.Value ? "" : Convert.ToString(s0["Notes"]);
                pr = ProcessReservationPayment(reservationId, claimed, "โอนเงิน", null, adminId,
                    s0["Customer_MobilePhone"] == DBNull.Value ? "" : Convert.ToString(s0["Customer_MobilePhone"]),
                    (notes.Length > 0 ? notes + " · " : "") + "อนุมัติสลิป #" + slipId, slipId);
            }
            catch (Exception ex)
            {
                pr = new PaymentResult { Success = false, Message = ex.Message };
            }

            if (pr == null || !pr.Success)
            {
                // ลงเงินไม่ผ่าน (เช่น ยอดเกินยอดค้างแล้ว / ใบจองถูกยกเลิก) → คืนสถานะให้ตรวจ/แก้แล้วอนุมัติใหม่ได้
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Payment_Slips SET VerificationStatus = 'PENDING', IsVerified = 0, VerifiedBy_ID = NULL, VerifiedDate = NULL
                       WHERE ID = @id AND VerificationStatus = 'APPROVED'
                         AND NOT EXISTS (SELECT 1 FROM Payment_History ph WHERE ph.PaymentSlip_ID = @id)",
                    new Dictionary<string, object> { { "@id", slipId } });
                return new PaymentResult
                {
                    Success = false,
                    Message = "อนุมัติไม่สำเร็จ — ลงรับเงินไม่ได้: " + (pr == null ? "-" : pr.Message)
                };
            }

            Take_Time_BangPhra.Payments.BookingPayment.PromoteIfPending(_connectionString, reservationId);
            _code.Logs(_connectionString, "Payment Slip",
                $"อนุมัติสลิป #{slipId} การจอง {reservationId}: ลงรับเงิน {claimed:N2} ใบเสร็จ {pr.ReceiptId}",
                adminId.HasValue ? adminId.Value.ToString() : "SYSTEM");
            return pr;
        }

        /// <summary>ค่าใช้จ่ายในห้องที่ยัง PENDING เรียงเก่าสุดก่อน (ตารางไม่มี = ว่าง)</summary>
        private DataTable LoadPendingChargesOldestFirst(int reservationId)
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID, Product_ID, Product_Name, Quantity, UnitPrice, TotalAmount, Notes
                        FROM Reservation_Product_Charges
                       WHERE Reservation_ID = @id AND Status = 'PENDING'
                       ORDER BY ChargedDate ASC, ID ASC",
                    new Dictionary<string, object> { { "@id", reservationId } });
                if (dt != null) return dt;
            }
            catch { }
            return new DataTable();
        }

        /// <summary>
        /// บรรทัดของใบเสร็จรับชำระการจอง — มัดจำ: บรรทัดเดียวแบบ ReceiptService.CreateDepositReceipt;
        /// อื่น ๆ: บรรทัดค่าห้อง (ยอด − ค่าใช้จ่ายที่ปิด) + บรรทัดค่าใช้จ่ายแบบ Reserve.AddProductChargesToReceipt
        /// ยอดบรรทัดรวม = ยอดรับเสมอ (ค่าห้องติดลบไม่ได้ → รวมเป็นบรรทัดเดียว)
        /// </summary>
        private void InsertReservationPaymentLines(string receiptId, int reservationId, decimal amount,
            bool isDeposit, List<DataRow> covered, decimal coveredTotal)
        {
            const string sql = @"INSERT INTO Account_Receipt_Detail
                  (Number, Receipt_ID, ProductType_ID, Product_ID, Product_Data, Product_Amount, Product_Unit,
                   Price_PerPeice, Price_Amount)
                  VALUES (@number, @receiptId, @type, @productId, @data, @qty, @unit, @per, @amt)";

            if (isDeposit)
            {
                _code.DatabaseInsertSafe(_connectionString, sql, new Dictionary<string, object>
                {
                    { "@number", 1 }, { "@receiptId", receiptId }, { "@type", 1 }, { "@productId", 7 },
                    { "@data", "ค่ามัดจำที่พักของหมายเลขการจอง " + reservationId + " [" + receiptId + "]" },
                    { "@qty", 1 }, { "@unit", "ครั้ง" }, { "@per", amount }, { "@amt", amount }
                });
                return;
            }

            decimal roomPortion = amount - coveredTotal;
            bool collapse = roomPortion < -0.005m;   // ยอดเคยรับไว้เกินค่าห้อง — แยกบรรทัดแล้วยอดไม่ลงตัว

            int accomId = 0;
            string roomText = "ค่าที่พัก การจอง #" + reservationId;
            try
            {
                DataTable rd = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT r.CheckinDate, r.CheckoutDate,
                             (SELECT TOP 1 ra.Accommodation_ID FROM Reservation_Accommodation ra
                               WHERE ra.Reservation_ID = r.ID ORDER BY ra.Accommodation_ID) AS AccomId,
                             (SELECT TOP 1 a.AccomName FROM Reservation_Accommodation ra
                                JOIN Accommodation a ON a.ID = ra.Accommodation_ID
                               WHERE ra.Reservation_ID = r.ID ORDER BY ra.Accommodation_ID) AS AccomName
                        FROM Reservation r WHERE r.ID = @id",
                    new Dictionary<string, object> { { "@id", reservationId } });
                if (rd != null && rd.Rows.Count > 0)
                {
                    DataRow r0 = rd.Rows[0];
                    if (r0["AccomId"] != DBNull.Value) accomId = Convert.ToInt32(r0["AccomId"]);
                    var th = new System.Globalization.CultureInfo("th-TH");
                    if (r0["AccomName"] != DBNull.Value) roomText += " " + Convert.ToString(r0["AccomName"]);
                    if (r0["CheckinDate"] != DBNull.Value && r0["CheckoutDate"] != DBNull.Value)
                        roomText += " เช็คอิน " + Convert.ToDateTime(r0["CheckinDate"]).ToString("dd MMMM yyyy", th)
                                  + " เช็คเอ้าท์ " + Convert.ToDateTime(r0["CheckoutDate"]).ToString("dd MMMM yyyy", th);
                }
            }
            catch { /* ข้อความสำรองพอ */ }

            int number = 1;
            if (collapse || roomPortion > 0.005m)
            {
                decimal lineAmt = collapse ? amount : roomPortion;
                string text = collapse ? roomText + " และค่าบริการในห้อง (ส่วนที่ค้างชำระ)" : roomText;
                _code.DatabaseInsertSafe(_connectionString, sql, new Dictionary<string, object>
                {
                    { "@number", number++ }, { "@receiptId", receiptId }, { "@type", 1 }, { "@productId", accomId },
                    { "@data", text }, { "@qty", 1 }, { "@unit", "ครั้ง" }, { "@per", lineAmt }, { "@amt", lineAmt }
                });
            }
            if (collapse) return;

            foreach (DataRow c in covered)
            {
                string notes = c.Table.Columns.Contains("Notes") && c["Notes"] != DBNull.Value ? Convert.ToString(c["Notes"]) : "";
                string unit = "ชิ้น";
                if (notes.StartsWith(PetStay.ChargeNotePrefix, StringComparison.Ordinal))
                    unit = notes.EndsWith(PetStay.UnitStay, StringComparison.Ordinal) ? "ตัว" : "ตัว/คืน";
                _code.DatabaseInsertSafe(_connectionString, sql, new Dictionary<string, object>
                {
                    { "@number", number++ }, { "@receiptId", receiptId }, { "@type", 3 },
                    { "@productId", c["Product_ID"] == DBNull.Value ? 0 : Convert.ToInt32(c["Product_ID"]) },
                    { "@data", Convert.ToString(c["Product_Name"]) },
                    { "@qty", c["Quantity"] == DBNull.Value ? 1m : Convert.ToDecimal(c["Quantity"]) },
                    { "@unit", unit },
                    { "@per", c["UnitPrice"] == DBNull.Value ? ToDec(c["TotalAmount"]) : Convert.ToDecimal(c["UnitPrice"]) },
                    { "@amt", ToDec(c["TotalAmount"]) }
                });
            }
        }

        private static decimal ToDec(object o)
        {
            if (o == null || o == DBNull.Value) return 0m;
            try { return Convert.ToDecimal(o); } catch { return 0m; }
        }

        #endregion

        #region Payment Slip Management

        /// <summary>
        /// Upload payment slip and return slip ID
        /// </summary>
        private long UploadPaymentSlip(
            int reservationId,
            HttpPostedFile slipFile,
            string customerPhone,
            int? adminId)
        {
            // 1. Validate file
            if (slipFile == null || slipFile.ContentLength == 0)
            {
                throw new Exception("ไฟล์ไม่ถูกต้อง");
            }

            string fileExtension = Path.GetExtension(slipFile.FileName).ToLower();
            if (fileExtension != ".jpg" && fileExtension != ".jpeg" && fileExtension != ".png" && fileExtension != ".pdf")
            {
                throw new Exception("รองรับเฉพาะไฟล์ JPG, PNG, หรือ PDF เท่านั้น");
            }

            if (slipFile.ContentLength > 5 * 1024 * 1024) // 5MB limit
            {
                throw new Exception("ไฟล์ใหญ่เกินไป (สูงสุด 5MB)");
            }

            // 2. Generate unique filename
            string uniqueFileName = $"Slip_{reservationId}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}{fileExtension}";

            // 3. Create directory if not exists
            string uploadPath = HttpContext.Current.Server.MapPath("~/Documents/PaymentSlips/");
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            // 4. Save file
            string fullPath = Path.Combine(uploadPath, uniqueFileName);
            slipFile.SaveAs(fullPath);

            // 5. Insert to database
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@slipFileURL", "~/Documents/PaymentSlips/" + uniqueFileName },
                { "@fileName", slipFile.FileName },
                { "@fileType", slipFile.ContentType },
                { "@fileSize", slipFile.ContentLength },
                { "@uploadedByCustomer", customerPhone },
                { "@uploadedByAdmin", adminId },
                { "@verificationStatus", "PENDING" }
            };

            long slipId = _code.DatabaseInsertReturnSafe(_connectionString,
                @"INSERT INTO Payment_Slips (
                    Reservation_ID, SlipFileURL, FileName, FileType, FileSize,
                    UploadedDate, UploadedBy_CustomerPhone, UploadedBy_ID,
                    VerificationStatus, IsVerified
                  )
                  VALUES (
                    @reservationId, @slipFileURL, @fileName, @fileType, @fileSize,
                    GETDATE(), @uploadedByCustomer, @uploadedByAdmin,
                    @verificationStatus, 0
                  );
                  SELECT SCOPE_IDENTITY();",
                parameters);

            // 6. Process OCR asynchronously (best effort - don't fail if OCR fails)
            try
            {
                ProcessSlipOCR(slipId, fullPath);
            }
            catch (Exception ocrEx)
            {
                // Log OCR error but don't fail the upload
                _code.Logs(_connectionString, "OCR Processing Error",
                    $"SlipID: {slipId}, Error: {ocrEx.Message}", "SYSTEM");
            }

            return slipId;
        }

        /// <summary>
        /// Process OCR for uploaded slip
        /// </summary>
        private void ProcessSlipOCR(long slipId, string imageFilePath)
        {
            try
            {
                // Get tesseract data path from web.config
                string tessDataPath = ConfigurationManager.AppSettings["TesseractDataPath"] ??
                    HttpContext.Current.Server.MapPath("~/tessdata");

                var ocrService = new Take_Time_BangPhra.Services.SlipOCRService(tessDataPath, _connectionString);
                var ocrResult = ocrService.ProcessSlip(imageFilePath);

                // Save OCR result to database
                ocrService.SaveOCRResult(slipId, ocrResult);

                // Log result for monitoring
                string logMessage = ocrResult.Success
                    ? $"OCR Success - Amount: {ocrResult.Amount:N2}, Confidence: {ocrResult.Confidence:N2}%"
                    : $"OCR Failed - {ocrResult.ErrorMessage}";

                _code.Logs(_connectionString, "OCR Processing",
                    $"SlipID: {slipId}, {logMessage}", "SYSTEM");
            }
            catch (Exception ex)
            {
                // Update slip with error status
                var errorParams = new Dictionary<string, object>
                {
                    { "@slipId", slipId },
                    { "@errorMessage", ex.Message },
                    { "@status", "FAILED" }
                };

                // Note: Payment_Slips table does not have OCR columns
                // Just log the error, don't update non-existent columns
                _code.Logs(_connectionString, "OCR Processing Failed",
                    $"SlipID: {slipId}, Error: {ex.Message}", "SYSTEM");

                throw;
            }
        }

        /// <summary>
        /// Verify payment slip
        /// </summary>
        public bool VerifyPaymentSlip(long slipId, int adminId, bool approved, string rejectionReason = null)
        {
            try
            {
                string status = approved ? "APPROVED" : "REJECTED";
                _paymentDA.UpdateSlipVerification(slipId, status, adminId, rejectionReason);

                // Get reservation ID from slip
                var parameters = new Dictionary<string, object>
                {
                    { "@slipId", slipId }
                };

                var slipData = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Reservation_ID FROM Payment_Slips WHERE ID = @slipId",
                    parameters);

                if (slipData.Rows.Count > 0)
                {
                    int reservationId = Convert.ToInt32(slipData.Rows[0]["Reservation_ID"]);

                    // If rejected, send notification to customer
                    if (!approved && !string.IsNullOrEmpty(rejectionReason))
                    {
                        SendPaymentRejectionEmail(reservationId, rejectionReason);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "VerifyPaymentSlip Error", ex.Message, "SYSTEM");
                return false;
            }
        }

        #endregion

        #region Receipt Management

        /// <summary>
        /// Create payment receipt
        /// </summary>
        private string CreatePaymentReceipt(
            int reservationId,
            decimal amount,
            string paymentMethod,
            bool isDeposit)
        {
            // Generate receipt ID
            string receiptId = GenerateReceiptId();

            // Get reservation data
            var reservationParams = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            var reservation = _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM Reservation WHERE ID = @reservationId",
                reservationParams);

            if (reservation.Rows.Count == 0)
            {
                throw new Exception("ไม่พบข้อมูลการจอง");
            }

            // Insert receipt — แยก VAT ตาม Business_Info.Use_Vat (ราคาเป็น gross รวม VAT)
            // เดิม hardcode Vat=0 → กิจการจด VAT รายงานภาษีขายขาดทุกการจ่ายผ่านช่องทางนี้
            decimal receiptVat = ComputeVatFromGross(amount);
            var parameters = new Dictionary<string, object>
            {
                { "@receiptId", receiptId },
                { "@reservationId", reservationId },
                { "@createdDate", DateTime.Now },
                { "@totalAmount", amount },
                { "@vat", receiptVat },
                { "@totalExcludeVat", amount - receiptVat },
                { "@isDeposit", isDeposit },
                { "@useDeposit", false },
                { "@paidType", paymentMethod },
                { "@status", "Normal" },
                { "@etax", false }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO Account_Receipt (
                    ID, Reservation_ID, Created_Date, Total_Amount, Vat,
                    Total_Amount_Exclude_Vat, IsDeposit, UseDeposit, Paid_Type,
                    Status, Etax
                  )
                  VALUES (
                    @receiptId, @reservationId, @createdDate, @totalAmount, @vat,
                    @totalExcludeVat, @isDeposit, @useDeposit, @paidType,
                    @status, @etax
                  )",
                parameters);

            return receiptId;
        }

        /// <summary>
        /// ถอด VAT 7% จากยอด gross เมื่อกิจการจด VAT (Business_Info.Use_Vat) — ไม่จด → 0.
        /// สูตรเดียวกับ ReceiptService/POS rollup: net = round(gross*100/107), vat = gross − net
        /// </summary>
        private decimal ComputeVatFromGross(decimal grossAmount)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Use_Vat FROM Business_Info", null);
                bool useVat = dt != null && dt.Rows.Count > 0
                    && dt.Rows[0]["Use_Vat"] != DBNull.Value
                    && Convert.ToBoolean(dt.Rows[0]["Use_Vat"]);
                if (!useVat || grossAmount <= 0) return 0m;
                decimal net = Math.Round(grossAmount * 100m / 107m, 2, MidpointRounding.AwayFromZero);
                return grossAmount - net;
            }
            catch { return 0m; }
        }

        /// <summary>
        /// Generate unique receipt ID (format: RECYYMMDDXXX)
        /// </summary>
        private string GenerateReceiptId()
        {
            string datePrefix = "REC" + DateTime.Now.ToString("yyMMdd");

            var parameters = new Dictionary<string, object>
            {
                { "@datePrefix", datePrefix + "%" }
            };

            var result = _code.DatabaseQuerySafe(_connectionString,
                "SELECT MAX(ID) as LastID FROM Account_Receipt WHERE ID LIKE @datePrefix",
                parameters);

            int sequenceNumber = 1;
            if (result.Rows.Count > 0 && result.Rows[0]["LastID"] != DBNull.Value)
            {
                string lastId = result.Rows[0]["LastID"].ToString();
                if (lastId.Length >= 12)
                {
                    string lastSeq = lastId.Substring(9);
                    if (int.TryParse(lastSeq, out int lastNum))
                    {
                        sequenceNumber = lastNum + 1;
                    }
                }
            }

            return datePrefix + sequenceNumber.ToString("D3");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get customer name for a reservation
        /// </summary>
        private string GetCustomerName(int reservationId)
        {
            try
            {
                var parameters = new Dictionary<string, object> { { "@reservationId", reservationId } };
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ISNULL(c.Customer_Name, c.NickName) AS Name
                      FROM Reservation r
                      INNER JOIN Customer c ON r.Customer_MobilePhone = c.Customer_MobilePhone
                      WHERE r.ID = @reservationId", parameters);
                return dt?.Rows.Count > 0 ? dt.Rows[0]["Name"]?.ToString() ?? "ลูกค้า" : "ลูกค้า";
            }
            catch { return "ลูกค้า"; }
        }

        /// <summary>
        /// Update reservation deposit amount
        /// </summary>
        private void UpdateReservationDeposit(int reservationId, decimal depositAmount)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@deposit", depositAmount }
            };

            // ⚠ ใบ OTA Channel Collect: Deposit = ยอดที่ OTA เก็บแทนโรงแรม (ตั้งตอนรับอีเมลจอง)
            //   เดิมเขียนทับด้วยยอดรวม Payment_History ⇒ ลูกค้าจ่ายของเสริม 450 ผ่านลิงก์ → Deposit ร่วงจาก
            //   3,719 เหลือ 450 → หน้ารายละเอียดโชว์ "ยอดเงินรับมา 450" ค่าห้องกลายเป็นค้างทั้งที่ OTA เก็บแล้ว
            //   → ห้ามลดต่ำกว่ายอดเดิมสำหรับใบที่ระบุว่า Channel Collect
            // วิธีเก็บเงิน: คอลัมน์ OTA_Collect_Mode (PHASE19 migration 17) ก่อน — กันเฉพาะ CHANNEL ที่ไม่ได้มาจากการเดา
            //   (HOTEL / UNKNOWN / CHANNEL+GUESS = ยังไม่มีใครยืนยันว่า OTA เก็บ → Deposit = เงินที่รับจริง ไม่กัน
            //    แม้หมายเหตุจะเขียน Channel)
            //   ยังไม่มีค่า (NULL) → ถอยไปดูหมายเหตุแบบ "Hotel มาก่อน" (มีทั้งสองคำ = Hotel = ไม่กัน)
            //   ยังไม่ได้รัน migration (คอลัมน์ไม่มี → query พัง) → ใช้เงื่อนไขหมายเหตุอย่างเดียว
            const string remarkChannel =
                "(ISNULL(CAST(Remark AS NVARCHAR(MAX)), N'') NOT LIKE N'%(Hotel Collect)%' " +
                " AND ISNULL(CAST(Remark AS NVARCHAR(MAX)), N'') LIKE N'%(Channel Collect)%')";
            if (!_collectModeColumnMissing)
            {
                try
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Reservation SET Deposit = CASE
                                WHEN ((ISNULL(OTA_Collect_Mode, N'') = N'CHANNEL'
                                       AND ISNULL(OTA_Collect_Source, N'') <> N'GUESS')
                                      OR (OTA_Collect_Mode IS NULL AND " + remarkChannel + @"))
                                     AND ISNULL(Deposit, 0) > @deposit
                                THEN Deposit ELSE @deposit END
                          WHERE ID = @reservationId",
                        parameters);
                    return;
                }
                catch (Exception ex) when ((ex.Message ?? "").IndexOf("OTA_Collect_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // "Invalid column name 'OTA_Collect_Mode'/'OTA_Collect_Source'" — ยังไม่ได้รัน migration 17
                    _collectModeColumnMissing = true;   // ถาวรจน recycle แอป (หลังรัน migration 17 ให้ recycle)
                }
            }

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Reservation SET Deposit = CASE
                        WHEN " + remarkChannel + @" AND ISNULL(Deposit, 0) > @deposit
                        THEN Deposit ELSE @deposit END
                  WHERE ID = @reservationId",
                parameters);
        }

        /// <summary>ยังไม่ได้รัน PHASE19 migration 17 (ไม่มีคอลัมน์ Reservation.OTA_Collect_Mode) — ตั้งครั้งแรกที่ query พัง</summary>
        private static volatile bool _collectModeColumnMissing;

        /// <summary>
        /// Update reservation status
        /// </summary>
        private void UpdateReservationStatus(int reservationId, string status)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@status", status }
            };

            _code.DatabaseInsertSafe(_connectionString,
                "UPDATE Reservation SET Status = @status WHERE ID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Send payment confirmation email
        /// </summary>
        private void SendPaymentConfirmationEmail(int reservationId, string receiptId, decimal amount)
        {
            try
            {
                // Get customer email from reservation
                var parameters = new Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };

                var customerData = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT c.Email, c.Customer_Name, r.CheckinDate, r.CheckoutDate, r.TotalPrice
                      FROM Reservation r
                      INNER JOIN Customer c ON r.Customer_MobilePhone = c.Customer_MobilePhone
                      WHERE r.ID = @reservationId",
                    parameters);

                if (customerData.Rows.Count == 0 || string.IsNullOrEmpty(customerData.Rows[0]["Email"]?.ToString()))
                {
                    _code.Logs(_connectionString, "Email Skipped", $"No email for reservation {reservationId}", "SYSTEM");
                    return;
                }

                string customerEmail = customerData.Rows[0]["Email"].ToString();
                string customerName = customerData.Rows[0]["Customer_Name"]?.ToString() ?? "ลูกค้า";
                DateTime checkInDate = Convert.ToDateTime(customerData.Rows[0]["CheckinDate"]);
                DateTime checkOutDate = Convert.ToDateTime(customerData.Rows[0]["CheckoutDate"]);
                decimal totalPrice = Convert.ToDecimal(customerData.Rows[0]["TotalPrice"]);
                decimal remaining = totalPrice - amount;

                // Build email content
                string subject = $"[Take Time] ยืนยันการชำระเงิน - ใบเสร็จ #{receiptId}";
                string body = $@"
                    <html>
                    <head>
                        <style>
                            body {{ font-family: 'Sarabun', Arial, sans-serif; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background: #2c3e50; color: white; padding: 20px; text-align: center; }}
                            .content {{ padding: 20px; background: #f9f9f9; }}
                            .amount {{ font-size: 24px; color: #27ae60; font-weight: bold; }}
                            .footer {{ padding: 20px; text-align: center; color: #666; font-size: 12px; }}
                            table {{ width: 100%; border-collapse: collapse; }}
                            td {{ padding: 10px; border-bottom: 1px solid #ddd; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Take Time Bang Phra</h1>
                                <p>ยืนยันการชำระเงิน</p>
                            </div>
                            <div class='content'>
                                <p>เรียน คุณ{customerName},</p>
                                <p>ขอบคุณสำหรับการชำระเงิน ระบบได้บันทึกการชำระเงินของท่านเรียบร้อยแล้ว</p>

                                <table>
                                    <tr><td><strong>เลขที่ใบเสร็จ:</strong></td><td>{receiptId}</td></tr>
                                    <tr><td><strong>เลขที่การจอง:</strong></td><td>{reservationId}</td></tr>
                                    <tr><td><strong>วันเช็คอิน:</strong></td><td>{checkInDate:dd/MM/yyyy}</td></tr>
                                    <tr><td><strong>วันเช็คเอาท์:</strong></td><td>{checkOutDate:dd/MM/yyyy}</td></tr>
                                    <tr><td><strong>จำนวนเงินที่ชำระ:</strong></td><td class='amount'>฿{amount:N2}</td></tr>
                                    <tr><td><strong>ยอดคงเหลือ:</strong></td><td>฿{remaining:N2}</td></tr>
                                </table>

                                <p style='margin-top: 20px;'>หากท่านมีข้อสงสัย กรุณาติดต่อเราได้ทุกช่องทาง</p>
                            </div>
                            <div class='footer'>
                                <p>Take Time Bang Phra</p>
                                <p>โทร: 038-XXX-XXX | อีเมล: taketime.bangphra@gmail.com</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                // Send email using EmailService
                var emailService = new Take_Time_BangPhra.Services.EmailService();
                emailService.SendEmail(customerEmail, subject, body);

                _code.Logs(_connectionString, "Email Sent", $"Payment confirmation sent to {customerEmail} for receipt {receiptId}", "SYSTEM");
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Email Error", $"Failed to send payment confirmation: {ex.Message}", "SYSTEM");
                throw;
            }
        }

        /// <summary>
        /// Send payment rejection notification email
        /// </summary>
        private void SendPaymentRejectionEmail(int reservationId, string rejectionReason)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };

                var customerData = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT c.Email, c.Customer_Name, r.TotalPrice, r.Deposit
                      FROM Reservation r
                      INNER JOIN Customer c ON r.Customer_MobilePhone = c.Customer_MobilePhone
                      WHERE r.ID = @reservationId",
                    parameters);

                if (customerData.Rows.Count == 0 || string.IsNullOrEmpty(customerData.Rows[0]["Email"]?.ToString()))
                {
                    return;
                }

                string customerEmail = customerData.Rows[0]["Email"].ToString();
                string customerName = customerData.Rows[0]["Customer_Name"]?.ToString() ?? "ลูกค้า";

                string subject = $"[Take Time] แจ้งเตือน - สลิปการชำระเงินไม่ผ่านการตรวจสอบ (การจอง #{reservationId})";
                string body = $@"
                    <html>
                    <head>
                        <style>
                            body {{ font-family: 'Sarabun', Arial, sans-serif; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background: #e74c3c; color: white; padding: 20px; text-align: center; }}
                            .content {{ padding: 20px; background: #f9f9f9; }}
                            .reason {{ background: #fff3cd; padding: 15px; border-left: 4px solid #ffc107; margin: 15px 0; }}
                            .footer {{ padding: 20px; text-align: center; color: #666; font-size: 12px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Take Time Bang Phra</h1>
                                <p>แจ้งเตือนการชำระเงิน</p>
                            </div>
                            <div class='content'>
                                <p>เรียน คุณ{customerName},</p>
                                <p>สลิปการชำระเงินของท่านสำหรับการจอง #{reservationId} ไม่ผ่านการตรวจสอบ</p>

                                <div class='reason'>
                                    <strong>เหตุผล:</strong> {rejectionReason}
                                </div>

                                <p>กรุณาอัปโหลดสลิปใหม่หรือติดต่อเจ้าหน้าที่เพื่อดำเนินการต่อ</p>
                                <p>หากท่านมีข้อสงสัย กรุณาติดต่อเราได้ทุกช่องทาง</p>
                            </div>
                            <div class='footer'>
                                <p>Take Time Bang Phra</p>
                                <p>โทร: 038-XXX-XXX | อีเมล: taketime.bangphra@gmail.com</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                var emailService = new Take_Time_BangPhra.Services.EmailService();
                emailService.SendEmail(customerEmail, subject, body);

                _code.Logs(_connectionString, "Email Sent", $"Payment rejection notification sent to {customerEmail}", "SYSTEM");
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Email Error", $"Failed to send rejection notification: {ex.Message}", "SYSTEM");
            }
        }

        #endregion

        #region Public Query Methods

        /// <summary>
        /// Get payment history for display
        /// </summary>
        public DataTable GetPaymentHistory(int reservationId)
        {
            return _paymentDA.GetPaymentHistory(reservationId);
        }

        /// <summary>
        /// Get payment summary
        /// </summary>
        public PaymentSummary GetPaymentSummary(int reservationId)
        {
            var summary = _paymentDA.GetPaymentSummary(reservationId);
            if (summary.Rows.Count > 0)
            {
                var row = summary.Rows[0];
                return new PaymentSummary
                {
                    ReservationId = reservationId,
                    TotalPrice = Convert.ToDecimal(row["TotalPrice"]),
                    TotalPaid = Convert.ToDecimal(row["TotalPaid"]),
                    DepositPaid = Convert.ToDecimal(row["DepositPaid"]),
                    AdditionalPaid = Convert.ToDecimal(row["AdditionalPaid"]),
                    RemainingBalance = Convert.ToDecimal(row["RemainingBalance"]),
                    PaymentStatus = row["PaymentStatus"].ToString()
                };
            }

            return null;
        }

        /// <summary>
        /// Check if can make additional payment
        /// </summary>
        public bool CanMakeAdditionalPayment(int reservationId)
        {
            return _paymentDA.GetRemainingBalance(reservationId) > 0.01m;
        }

        #endregion
    }

    #region Result Classes

    /// <summary>
    /// Payment operation result
    /// </summary>
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public long PaymentId { get; set; }
        public string ReceiptId { get; set; }
        public decimal RemainingBalance { get; set; }
    }

    /// <summary>
    /// Payment summary data
    /// </summary>
    public class PaymentSummary
    {
        public int ReservationId { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal DepositPaid { get; set; }
        public decimal AdditionalPaid { get; set; }
        public decimal RemainingBalance { get; set; }
        public string PaymentStatus { get; set; } // PAID/PARTIAL/UNPAID
    }

    #endregion
}
