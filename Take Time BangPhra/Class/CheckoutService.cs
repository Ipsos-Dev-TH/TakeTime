using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// Checkout Service - Business logic for checkout process
    /// </summary>
    public class CheckoutService
    {
        private readonly string _connectionString;
        private readonly PaymentDataAccess _paymentDA;
        private readonly code _code;

        public CheckoutService(string connectionString)
        {
            _connectionString = connectionString;
            _paymentDA = new PaymentDataAccess(connectionString);
            _code = new code();
        }

        /// <summary>
        /// Process checkout - calls stored procedure
        /// </summary>
        public CheckoutResult ProcessCheckout(
            int reservationId,
            int adminId,
            bool roomDamage = false,
            string damageDescription = null,
            decimal damageCharge = 0,
            bool missingItems = false,
            string missingItemsDescription = null,
            decimal missingItemsCharge = 0,
            bool keyReturned = true,
            string cleaningStatus = "GOOD",
            byte? guestSatisfaction = null,
            string notes = null)
        {
            try
            {
                // Capture reservation data BEFORE sp_ProcessCheckout, because the SP
                // may zero out Reservation.Deposit during checkout processing.
                var resData = GetReservationData(reservationId);
                string customerName = resData?["CustomerName"]?.ToString() ?? "ลูกค้า";
                decimal depositAmt = resData != null ? Convert.ToDecimal(resData["Deposit"]) : 0;
                decimal totalPaid = resData != null && resData.ContainsKey("TotalPaid") ? Convert.ToDecimal(resData["TotalPaid"]) : 0;
                decimal totalPrice = resData != null ? Convert.ToDecimal(resData["TotalPrice"]) : 0;

                // Detect under-paid checkout: ลูกค้าจองห้อง 1600 จ่ายมัดจำ 500 แล้วเช็คเอาท์โดยไม่จ่ายเพิ่ม
                // → รายได้ที่ยังไม่ได้รับ = TotalPrice - TotalPaid → log warning + validation
                decimal expectedPayable = totalPrice + damageCharge + missingItemsCharge;
                if (totalPaid + 0.01m < expectedPayable)
                {
                    decimal outstanding = expectedPayable - totalPaid;
                    var validation = AccountingArithmeticValidator.ValidationResult.Fail(
                        "CHECKOUT_UNDERPAID",
                        $"ลูกค้า checkout โดยยอดชำระไม่ครบ: ราคาห้อง+ค่าเสียหาย {expectedPayable:N2} - ชำระแล้ว {totalPaid:N2} = ค้างชำระ {outstanding:N2} บาท",
                        expectedPayable, totalPaid, blocking: false);
                    AccountingArithmeticValidator.LogValidationFailure("CHECKOUT", reservationId.ToString(), validation, adminId.ToString());
                    _code.Logs(_connectionString, "Checkout",
                        $"⚠️ Reservation #{reservationId} checkout under-paid: outstanding {outstanding:N2} (expected {expectedPayable:N2}, paid {totalPaid:N2}) — admin must collect or write off", "SYSTEM");
                }

                var parameters = new Dictionary<string, object>
                {
                    { "@ReservationID", reservationId },
                    { "@AdminID", adminId },
                    { "@RoomDamage", roomDamage },
                    { "@DamageDescription", damageDescription },
                    { "@DamageCharge", damageCharge },
                    { "@MissingItems", missingItems },
                    { "@MissingItemsDescription", missingItemsDescription },
                    { "@MissingItemsCharge", missingItemsCharge },
                    { "@KeyReturned", keyReturned },
                    { "@CleaningStatus", cleaningStatus },
                    { "@GuestSatisfaction", guestSatisfaction },
                    { "@Notes", notes }
                };

                // Execute stored procedure
                var result = _code.DatabaseQuerySafe(_connectionString,
                    @"DECLARE @CheckoutID bigint, @ErrorMsg nvarchar(500);
                      EXEC sp_ProcessCheckout
                        @ReservationID, @AdminID, @RoomDamage, @DamageDescription,
                        @DamageCharge, @MissingItems, @MissingItemsDescription,
                        @MissingItemsCharge, @KeyReturned, @CleaningStatus,
                        @GuestSatisfaction, @Notes, @CheckoutID OUTPUT, @ErrorMsg OUTPUT;
                      SELECT @CheckoutID as CheckoutID, @ErrorMsg as ErrorMessage;",
                    parameters);

                if (result.Rows.Count > 0)
                {
                    var row = result.Rows[0];
                    string errorMsg = row["ErrorMessage"].ToString();

                    if (string.IsNullOrEmpty(errorMsg))
                    {
                        // ตัดมัดจำออกจากเจ้าหนี้ (ADVANCE_DEPOSIT) → รับรู้รายได้ห้องพัก (ROOM_REVENUE)
                        // ถ้ามีค่าเสียหาย/ของหาย ระบบจะแบ่ง credit ระหว่าง Room Revenue และ Other Income
                        TryEnqueueDepositClearing(reservationId, depositAmt, customerName, damageCharge + missingItemsCharge);

                        return new CheckoutResult
                        {
                            Success = true,
                            Message = "เช็คเอาท์สำเร็จ",
                            CheckoutId = Convert.ToInt64(row["CheckoutID"]),
                            CheckoutDate = DateTime.Now
                        };
                    }
                    else
                    {
                        return new CheckoutResult
                        {
                            Success = false,
                            Message = errorMsg
                        };
                    }
                }

                return new CheckoutResult
                {
                    Success = false,
                    Message = "ไม่สามารถเช็คเอาท์ได้"
                };
            }
            catch (Exception ex)
            {
                return new CheckoutResult
                {
                    Success = false,
                    Message = "เกิดข้อผิดพลาด: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Check if reservation can be checked out
        /// </summary>
        public bool CanCheckout(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            var result = _code.DatabaseQuerySafe(_connectionString,
                "SELECT dbo.fn_CanCheckout(@reservationId) as CanCheckout",
                parameters);

            if (result.Rows.Count > 0)
            {
                return Convert.ToBoolean(result.Rows[0]["CanCheckout"]);
            }

            return false;
        }

        /// <summary>
        /// Get reservation data for accounting sync.
        /// Uses multi-source fallback to ensure deposit amount is always recovered:
        /// Priority: Payment_History.TotalPaid > Reservation.Deposit > Reservation.TotalPrice
        /// </summary>
        private Dictionary<string, object> GetReservationData(int reservationId)
        {
            try
            {
                var parameters = new Dictionary<string, object> { { "@id", reservationId } };
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT r.Deposit, r.TotalPrice,
                             ISNULL(c.FullName, c.Name) AS CustomerName,
                             ISNULL(dep.DepositPaid, 0) AS DepositPaid,
                             ISNULL(allp.TotalPaid, 0) AS TotalPaid
                      FROM Reservation r
                      LEFT JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                      LEFT JOIN (
                          SELECT Reservation_ID, SUM(Total_Amount) AS DepositPaid
                          FROM Account_Receipt
                          WHERE IsDeposit = 1 AND (Status = 'Normal' OR Status IS NULL)
                          GROUP BY Reservation_ID
                      ) dep ON dep.Reservation_ID = r.ID
                      LEFT JOIN (
                          SELECT Reservation_ID, SUM(PaymentAmount) AS TotalPaid
                          FROM Payment_History
                          WHERE Status = 'COMPLETED'
                          GROUP BY Reservation_ID
                      ) allp ON allp.Reservation_ID = r.ID
                      WHERE r.ID = @id", parameters);

                if (dt?.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    decimal deposit = row["Deposit"] != DBNull.Value ? Convert.ToDecimal(row["Deposit"]) : 0m;
                    decimal depositPaid = row["DepositPaid"] != DBNull.Value ? Convert.ToDecimal(row["DepositPaid"]) : 0m;
                    decimal totalPaid = row["TotalPaid"] != DBNull.Value ? Convert.ToDecimal(row["TotalPaid"]) : 0m;
                    decimal totalPrice = row["TotalPrice"] != DBNull.Value ? Convert.ToDecimal(row["TotalPrice"]) : 0m;

                    // Truth source for "outstanding deposit liability" = Account_Receipt where IsDeposit=1.
                    // ห้ามใช้ Payment_History.TotalPaid ที่รวม final payment ด้วย (ทำให้ over-clear)
                    decimal effectiveDeposit;
                    if (depositPaid > 0)
                        effectiveDeposit = depositPaid;
                    else if (deposit > 0)
                        effectiveDeposit = deposit;
                    else
                        effectiveDeposit = 0m; // ไม่มีมัดจำในระบบ → ไม่ต้อง clear

                    if (effectiveDeposit <= 0)
                    {
                        _code.Logs(_connectionString, "Checkout",
                            $"GetReservationData: no outstanding deposit for Reservation #{reservationId} (DepositPaid={depositPaid:N2}, Deposit={deposit:N2}, TotalPaid={totalPaid:N2})", "SYSTEM");
                    }

                    return new Dictionary<string, object>
                    {
                        { "Deposit", effectiveDeposit },
                        { "TotalPrice", totalPrice },
                        { "TotalPaid", totalPaid },
                        { "CustomerName", row["CustomerName"]?.ToString() ?? "ลูกค้า" }
                    };
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Checkout",
                    $"GetReservationData failed for Reservation #{reservationId}: {ex.Message}", "SYSTEM");
            }
            return null;
        }

        /// <summary>
        /// Enqueue deposit clearing journal entry (DR ADVANCE_DEPOSIT, CR ROOM_REVENUE).
        /// ความล้มเหลวจะ log แต่ไม่ throw — เช็คเอาท์ต้องผ่านได้ถึงแม้ accounting sync มีปัญหา
        /// </summary>
        private void TryEnqueueDepositClearing(int reservationId, decimal depositAmt, string customerName, decimal damageAmt)
        {
            if (depositAmt <= 0)
            {
                _code.Logs(_connectionString, "Checkout",
                    $"TryEnqueueDepositClearing: skipped — Reservation #{reservationId} ไม่มีมัดจำ", "SYSTEM");
                return;
            }

            // ป้องกัน double-clear: ถ้าใบเสร็จได้หักมัดจำไปแล้ว (Deposit_Applied_Amount > 0) ระบบ
            // จะสร้าง adjustment journal ตอนสร้างใบเสร็จเอง — ไม่ต้องคลิยร์ซ้ำ
            decimal alreadyApplied = LookupDepositAlreadyAppliedInReceipts(reservationId);
            decimal toClear = depositAmt - alreadyApplied;
            if (toClear <= 0.01m)
            {
                _code.Logs(_connectionString, "Checkout",
                    $"TryEnqueueDepositClearing: skipped — มัดจำ {depositAmt:N2} ถูกหักในใบเสร็จไปแล้วทั้งหมด ({alreadyApplied:N2})",
                    "SYSTEM");
                return;
            }

            try
            {
                var sync = new AccountingSyncService(_connectionString);
                long queueId = sync.EnqueueDepositClearingOnCheckout(reservationId, toClear, customerName, DateTime.Now, damageAmt);
                _code.Logs(_connectionString, "Checkout",
                    $"Deposit clearing enqueued: resId={reservationId} deposit={depositAmt:N2} alreadyApplied={alreadyApplied:N2} clearing={toClear:N2} damage={damageAmt:N2} queueId={queueId}",
                    "SYSTEM");
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Checkout",
                    $"TryEnqueueDepositClearing failed for Reservation #{reservationId}: {ex.Message}", "SYSTEM");
            }
        }

        private decimal LookupDepositAlreadyAppliedInReceipts(int reservationId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ISNULL(SUM(Deposit_Applied_Amount), 0) AS Applied
                      FROM Account_Receipt
                      WHERE Reservation_ID = @id
                        AND IsDeposit = 0
                        AND (Status = 'Normal' OR Status IS NULL)",
                    new Dictionary<string, object> { { "@id", reservationId } });
                if (dt?.Rows.Count > 0)
                    return Convert.ToDecimal(dt.Rows[0]["Applied"]);
            }
            catch { }
            return 0m;
        }

        /// <summary>
        /// Get checkout details
        /// </summary>
        public DataTable GetCheckoutDetails(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM vw_CheckoutSummary WHERE ReservationID = @reservationId",
                parameters);
        }
    }

    /// <summary>
    /// Checkout operation result
    /// </summary>
    public class CheckoutResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public long CheckoutId { get; set; }
        public DateTime CheckoutDate { get; set; }
    }
}
