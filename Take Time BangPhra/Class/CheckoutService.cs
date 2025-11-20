using System;
using System.Collections.Generic;
using System.Data;

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
