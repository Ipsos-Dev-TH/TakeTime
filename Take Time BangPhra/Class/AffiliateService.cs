using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

/// <summary>
/// Affiliate Service - Business logic for Affiliate Management
/// Handles affiliate members, commissions, reservations, and payments
/// </summary>
public class AffiliateService
{
    private readonly string connectionString;

    public AffiliateService()
    {
        connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
    }

    #region Dashboard Statistics

    /// <summary>
    /// Get affiliate dashboard statistics
    /// </summary>
    public DataTable GetDashboardStats()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        (SELECT COUNT(*) FROM Affiliate_Member WHERE Status = 1) AS TotalAffiliates,
                        (SELECT COUNT(*) FROM Affiliate_Reservation WHERE Status = 'NEW') AS PendingCommissions,
                        (SELECT ISNULL(SUM(Commission), 0) FROM Affiliate_Reservation WHERE Status = 'NEW') AS PendingAmount,
                        (SELECT COUNT(*) FROM Affiliate_Reservation WHERE Status = 'TRANSFERED') AS PaidCommissions,
                        (SELECT ISNULL(SUM(Commission), 0) FROM Affiliate_Reservation WHERE Status = 'TRANSFERED') AS TotalPaidAmount,
                        (SELECT COUNT(*) FROM Affiliate_Reservation) AS TotalReservations,
                        (SELECT ISNULL(SUM(Commission), 0) FROM Affiliate_Reservation) AS TotalCommission";

                conn.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    #endregion

    #region Affiliate Members

    /// <summary>
    /// Get all affiliate members with optional search
    /// </summary>
    public DataTable GetAffiliateMembers(string search = "")
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        AM.ID,
                        AM.ID_Number AS IDNumber,
                        AM.FirstName,
                        AM.LastName,
                        AM.Coupon_Code AS CouponCode,
                        AM.Register_Date AS RegisterDate,
                        AM.Bank_Code AS BankCode,
                        AM.Bank_Number AS BankNumber,
                        AM.Status,
                        V.Name AS VendorName,
                        V.Phone_Number AS PhoneNumber,
                        (SELECT COUNT(*) FROM Affiliate_Reservation WHERE Affiliate_Member_Coupon_Code = AM.Coupon_Code) AS TotalReservations,
                        (SELECT ISNULL(SUM(Commission), 0) FROM Affiliate_Reservation WHERE Affiliate_Member_Coupon_Code = AM.Coupon_Code AND Status = 'TRANSFERED') AS TotalEarned,
                        (SELECT ISNULL(SUM(Commission), 0) FROM Affiliate_Reservation WHERE Affiliate_Member_Coupon_Code = AM.Coupon_Code AND Status = 'NEW') AS PendingEarnings
                    FROM Affiliate_Member AM
                    LEFT JOIN Vendor V ON AM.Vendor_ID = V.ID
                    WHERE (@Search = '' OR AM.Coupon_Code LIKE '%' + @Search + '%'
                           OR AM.FirstName LIKE '%' + @Search + '%'
                           OR AM.LastName LIKE '%' + @Search + '%'
                           OR V.Name LIKE '%' + @Search + '%')
                    ORDER BY AM.Register_Date DESC";

                cmd.Parameters.AddWithValue("@Search", search ?? "");

                conn.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get affiliate member by ID
    /// </summary>
    public DataTable GetAffiliateMember(int affiliateId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        AM.*,
                        V.Name AS VendorName,
                        V.Phone_Number AS PhoneNumber,
                        V.Address AS VendorAddress
                    FROM Affiliate_Member AM
                    LEFT JOIN Vendor V ON AM.Vendor_ID = V.ID
                    WHERE AM.ID = @AffiliateID";

                cmd.Parameters.AddWithValue("@AffiliateID", affiliateId);

                conn.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get affiliate members for dropdown
    /// </summary>
    public DataTable GetAffiliateMembersForDropdown()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        AM.ID,
                        AM.Coupon_Code AS CouponCode,
                        V.Name AS VendorName,
                        AM.FirstName + ' ' + AM.LastName AS FullName
                    FROM Affiliate_Member AM
                    LEFT JOIN Vendor V ON AM.Vendor_ID = V.ID
                    WHERE AM.Status = 1
                    ORDER BY V.Name, AM.Coupon_Code";

                conn.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Toggle affiliate member status (active/inactive)
    /// </summary>
    public bool ToggleAffiliateStatus(int affiliateId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    UPDATE Affiliate_Member
                    SET Status = CASE WHEN Status = 1 THEN 0 ELSE 1 END
                    WHERE ID = @AffiliateID";

                cmd.Parameters.AddWithValue("@AffiliateID", affiliateId);

                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    #endregion

    #region Commissions & Reservations

    /// <summary>
    /// Get pending commissions (reservations checked-in but not yet paid)
    /// </summary>
    public DataTable GetPendingCommissions(string affiliateCouponCode = "")
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        AR.ID,
                        AR.Affiliate_Member_Coupon_Code AS CouponCode,
                        AR.StayDate,
                        AR.PriceAfterDiscount AS Price,
                        AR.Commission,
                        AR.Status,
                        A.AccomName AS RoomName,
                        R.ID AS ReservationID,
                        R.Status AS ReservationStatus,
                        R.Customer_Name AS CustomerName,
                        V.Name AS AffiliateName
                    FROM Affiliate_Reservation AR
                    INNER JOIN Accommodation A ON A.ID = AR.Accommodation_ID
                    INNER JOIN Reservation R ON R.ID = AR.Reservation_ID
                    INNER JOIN Affiliate_Member AM ON AM.Coupon_Code = AR.Affiliate_Member_Coupon_Code
                    LEFT JOIN Vendor V ON V.ID = AM.Vendor_ID
                    WHERE AR.Status = 'NEW'
                    AND R.Status = N'เช็คอินแล้ว'
                    AND (@CouponCode = '' OR AR.Affiliate_Member_Coupon_Code = @CouponCode)
                    ORDER BY AR.StayDate DESC";

                cmd.Parameters.AddWithValue("@CouponCode", affiliateCouponCode ?? "");

                conn.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get all affiliate reservations with filters
    /// </summary>
    public DataTable GetAffiliateReservations(string couponCode = "", string status = "", int year = 0, int month = 0)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        AR.ID,
                        AR.Affiliate_Member_Coupon_Code AS CouponCode,
                        AR.StayDate,
                        AR.PriceAfterDiscount AS Price,
                        AR.Commission,
                        AR.Status,
                        A.AccomName AS RoomName,
                        R.ID AS ReservationID,
                        R.Status AS ReservationStatus,
                        R.Customer_Name AS CustomerName,
                        V.Name AS AffiliateName
                    FROM Affiliate_Reservation AR
                    INNER JOIN Accommodation A ON A.ID = AR.Accommodation_ID
                    INNER JOIN Reservation R ON R.ID = AR.Reservation_ID
                    INNER JOIN Affiliate_Member AM ON AM.Coupon_Code = AR.Affiliate_Member_Coupon_Code
                    LEFT JOIN Vendor V ON V.ID = AM.Vendor_ID
                    WHERE (@CouponCode = '' OR AR.Affiliate_Member_Coupon_Code = @CouponCode)
                    AND (@Status = '' OR AR.Status = @Status)
                    AND (@Year = 0 OR YEAR(AR.StayDate) = @Year)
                    AND (@Month = 0 OR MONTH(AR.StayDate) = @Month)
                    ORDER BY AR.StayDate DESC";

                cmd.Parameters.AddWithValue("@CouponCode", couponCode ?? "");
                cmd.Parameters.AddWithValue("@Status", status ?? "");
                cmd.Parameters.AddWithValue("@Year", year);
                cmd.Parameters.AddWithValue("@Month", month);

                conn.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get commission summary by affiliate
    /// </summary>
    public DataTable GetCommissionSummaryByAffiliate()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        AM.Coupon_Code AS CouponCode,
                        V.Name AS AffiliateName,
                        COUNT(CASE WHEN AR.Status = 'NEW' THEN 1 END) AS PendingCount,
                        ISNULL(SUM(CASE WHEN AR.Status = 'NEW' THEN AR.Commission END), 0) AS PendingAmount,
                        COUNT(CASE WHEN AR.Status = 'TRANSFERED' THEN 1 END) AS PaidCount,
                        ISNULL(SUM(CASE WHEN AR.Status = 'TRANSFERED' THEN AR.Commission END), 0) AS PaidAmount,
                        COUNT(*) AS TotalCount,
                        ISNULL(SUM(AR.Commission), 0) AS TotalAmount
                    FROM Affiliate_Member AM
                    LEFT JOIN Vendor V ON V.ID = AM.Vendor_ID
                    LEFT JOIN Affiliate_Reservation AR ON AR.Affiliate_Member_Coupon_Code = AM.Coupon_Code
                    WHERE AM.Status = 1
                    GROUP BY AM.Coupon_Code, V.Name
                    ORDER BY PendingAmount DESC";

                conn.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    #endregion

    #region Payment History

    /// <summary>
    /// Get affiliate payment history
    /// </summary>
    public DataTable GetPaymentHistory(string couponCode = "", int year = 0)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT DISTINCT
                        AP.ID AS PaymentID,
                        AP.Created_Date AS PaymentDate,
                        AP.Total_Amount AS TotalAmount,
                        AP.Vat AS VatAmount,
                        AP.Total_Amount_Exclude_Vat AS AmountExcludeVat,
                        AP.Paid_How AS PaymentMethod,
                        V.Name AS AffiliateName,
                        AM.Coupon_Code AS CouponCode,
                        (SELECT COUNT(*) FROM Affiliate_Reservation_Payment ARP2
                         INNER JOIN Affiliate_Reservation AR2 ON AR2.ID = ARP2.Affiliate_Reservation_ID
                         WHERE ARP2.Account_Payment_ID = AP.ID) AS ReservationCount
                    FROM Account_Payment AP
                    INNER JOIN Affiliate_Reservation_Payment ARP ON ARP.Account_Payment_ID = AP.ID
                    INNER JOIN Affiliate_Reservation AR ON AR.ID = ARP.Affiliate_Reservation_ID
                    INNER JOIN Affiliate_Member AM ON AM.Coupon_Code = AR.Affiliate_Member_Coupon_Code
                    INNER JOIN Vendor V ON V.ID = AM.Vendor_ID
                    WHERE AP.Paid_Type = '2'
                    AND (@CouponCode = '' OR AM.Coupon_Code = @CouponCode)
                    AND (@Year = 0 OR YEAR(AP.Created_Date) = @Year)
                    ORDER BY AP.Created_Date DESC";

                cmd.Parameters.AddWithValue("@CouponCode", couponCode ?? "");
                cmd.Parameters.AddWithValue("@Year", year);

                conn.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get years with payment data
    /// </summary>
    public DataTable GetPaymentYears()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT DISTINCT YEAR(Created_Date) AS PaymentYear
                    FROM Account_Payment
                    WHERE Paid_Type = '2'
                    ORDER BY PaymentYear DESC";

                conn.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    #endregion

    #region Rate Plans

    /// <summary>
    /// Get affiliate discount rate plans
    /// </summary>
    public DataTable GetAffiliateRatePlans()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ARP.ID,
                        ARP.Affiliate_Discount_ID AS DiscountID,
                        AD.Discount_Name AS DiscountName,
                        AD.Discount_Percent AS DiscountPercent,
                        ARP.Commission,
                        ARP.Status
                    FROM Affiliate_Discount_RatePlan ARP
                    INNER JOIN Affiliate_Discount AD ON AD.ID = ARP.Affiliate_Discount_ID
                    ORDER BY AD.Discount_Name";

                conn.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    #endregion
}
