-- =============================================
-- Customer Analytics Views
-- สร้างวันที่: 2025-11-10
-- จุดประสงค์: Views สำหรับรายงาน Customer Analytics
-- =============================================

-- =============================================
-- View 1: vw_CustomerSegmentation
-- แสดงข้อมูลลูกค้าทั้งหมดพร้อมการแบ่งประเภท
-- =============================================
IF OBJECT_ID('dbo.vw_CustomerSegmentation', 'V') IS NOT NULL
    DROP VIEW dbo.vw_CustomerSegmentation;
GO

CREATE VIEW dbo.vw_CustomerSegmentation
AS
SELECT
    c.MobilePhone,
    ISNULL(c.FullName, c.Name) as CustomerName,
    c.Email,
    c.Address,
    COUNT(r.ID) as TotalBookings,
    ISNULL(SUM(r.TotalPrice), 0) as TotalSpent,
    CASE
        WHEN COUNT(r.ID) = 0 THEN 0
        ELSE ISNULL(AVG(r.TotalPrice), 0)
    END as AvgSpendingPerBooking,
    MIN(r.Created_Date) as FirstVisit,
    MAX(r.Created_Date) as LastVisit,
    DATEDIFF(DAY, MAX(r.Created_Date), GETDATE()) as DaysSinceLastVisit,
    CASE
        WHEN COUNT(r.ID) = 0 THEN 'PROSPECT'
        WHEN COUNT(r.ID) = 1 THEN 'NEW'
        WHEN COUNT(r.ID) BETWEEN 2 AND 5 THEN 'RETURNING'
        ELSE 'VIP'
    END as CustomerType,
    CASE
        WHEN COUNT(r.ID) = 0 THEN 0
        WHEN COUNT(r.ID) = 1 THEN 1
        WHEN COUNT(r.ID) BETWEEN 2 AND 5 THEN 2
        ELSE 3
    END as CustomerTier
FROM
    Customer c
    LEFT JOIN Reservation r ON c.MobilePhone = r.Customer_MobilePhone
        AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
GROUP BY
    c.MobilePhone,
    c.FullName,
    c.Name,
    c.Email,
    c.Address;
GO

-- =============================================
-- View 2: vw_RepeatCustomers
-- แสดงเฉพาะลูกค้าที่กลับมาพักซ้ำ (มากกว่า 1 ครั้ง)
-- =============================================
IF OBJECT_ID('dbo.vw_RepeatCustomers', 'V') IS NOT NULL
    DROP VIEW dbo.vw_RepeatCustomers;
GO

CREATE VIEW dbo.vw_RepeatCustomers
AS
SELECT
    c.MobilePhone,
    ISNULL(c.FullName, c.Name) as CustomerName,
    c.Email,
    COUNT(r.ID) as TotalBookings,
    SUM(r.TotalPrice) as TotalSpent,
    AVG(r.TotalPrice) as AvgSpending,
    MIN(r.Created_Date) as FirstVisit,
    MAX(r.Created_Date) as LastVisit,
    DATEDIFF(DAY, MAX(r.Created_Date), GETDATE()) as DaysSinceLastVisit,
    CASE
        WHEN COUNT(r.ID) BETWEEN 2 AND 5 THEN 'RETURNING'
        ELSE 'VIP'
    END as CustomerType,
    -- หาที่พักที่ลูกค้าชอบมากที่สุด
    (
        SELECT TOP 1 a.AccomName
        FROM Reservation_Accommodation ra
        INNER JOIN Accommodation a ON ra.Accommodation_ID = a.ID
        WHERE ra.Reservation_ID IN (
            SELECT ID
            FROM Reservation
            WHERE Customer_MobilePhone = c.MobilePhone
            AND Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
        )
        GROUP BY a.AccomName
        ORDER BY COUNT(*) DESC
    ) as PreferredAccommodation,
    -- นับจำนวนครั้งของที่พักที่ชอบ
    (
        SELECT TOP 1 COUNT(*)
        FROM Reservation_Accommodation ra
        INNER JOIN Accommodation a ON ra.Accommodation_ID = a.ID
        WHERE ra.Reservation_ID IN (
            SELECT ID
            FROM Reservation
            WHERE Customer_MobilePhone = c.MobilePhone
            AND Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
        )
        GROUP BY a.AccomName
        ORDER BY COUNT(*) DESC
    ) as PreferredAccomBookings
FROM
    Customer c
    INNER JOIN Reservation r ON c.MobilePhone = r.Customer_MobilePhone
WHERE
    r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
GROUP BY
    c.MobilePhone,
    c.FullName,
    c.Name,
    c.Email
HAVING
    COUNT(r.ID) > 1  -- เฉพาะลูกค้าที่มาซ้ำ
GO

-- =============================================
-- View 3: vw_RepeatCustomersByYear
-- แสดงลูกค้าที่กลับมาพักซ้ำในแต่ละปี
-- =============================================
IF OBJECT_ID('dbo.vw_RepeatCustomersByYear', 'V') IS NOT NULL
    DROP VIEW dbo.vw_RepeatCustomersByYear;
GO

CREATE VIEW dbo.vw_RepeatCustomersByYear
AS
SELECT
    YEAR(r.Created_Date) as Year,
    c.MobilePhone,
    ISNULL(c.FullName, c.Name) as CustomerName,
    COUNT(r.ID) as TotalBookings,
    SUM(r.TotalPrice) as TotalSpent,
    AVG(r.TotalPrice) as AvgSpending,
    MIN(r.Created_Date) as FirstVisit,
    MAX(r.Created_Date) as LastVisit,
    CASE
        WHEN COUNT(r.ID) BETWEEN 2 AND 5 THEN 'RETURNING'
        ELSE 'VIP'
    END as CustomerType,
    (
        SELECT TOP 1 a.AccomName
        FROM Reservation_Accommodation ra
        INNER JOIN Accommodation a ON ra.Accommodation_ID = a.ID
        INNER JOIN Reservation res ON ra.Reservation_ID = res.ID
        WHERE res.Customer_MobilePhone = c.MobilePhone
            AND YEAR(res.Created_Date) = YEAR(r.Created_Date)
            AND res.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
        GROUP BY a.AccomName
        ORDER BY COUNT(*) DESC
    ) as PreferredAccommodation
FROM
    Customer c
    INNER JOIN Reservation r ON c.MobilePhone = r.Customer_MobilePhone
WHERE
    r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
GROUP BY
    YEAR(r.Created_Date),
    c.MobilePhone,
    c.FullName,
    c.Name
HAVING
    COUNT(r.ID) > 1  -- เฉพาะลูกค้าที่มาซ้ำในปีนั้น
GO

-- =============================================
-- View 4: vw_CustomerLifetimeValue
-- แสดง Customer Lifetime Value (CLV) และข้อมูลเชิงลึก
-- =============================================
IF OBJECT_ID('dbo.vw_CustomerLifetimeValue', 'V') IS NOT NULL
    DROP VIEW dbo.vw_CustomerLifetimeValue;
GO

CREATE VIEW dbo.vw_CustomerLifetimeValue
AS
SELECT
    c.MobilePhone,
    ISNULL(c.FullName, c.Name) as CustomerName,
    c.Email,

    -- Booking Metrics
    COUNT(r.ID) as TotalBookings,
    SUM(DATEDIFF(DAY, r.CheckInDate, r.CheckOutDate)) as TotalNights,

    -- Financial Metrics
    SUM(r.TotalPrice) as LifetimeValue,
    AVG(r.TotalPrice) as AvgBookingValue,
    MAX(r.TotalPrice) as MaxBookingValue,
    MIN(r.TotalPrice) as MinBookingValue,

    -- Time Metrics
    MIN(r.Created_Date) as FirstBookingDate,
    MAX(r.Created_Date) as LastBookingDate,
    DATEDIFF(MONTH, MIN(r.Created_Date), MAX(r.Created_Date)) as CustomerLifetimeMonths,
    DATEDIFF(DAY, MAX(r.Created_Date), GETDATE()) as DaysSinceLastBooking,

    -- Frequency Metrics
    CASE
        WHEN COUNT(r.ID) > 1 AND DATEDIFF(MONTH, MIN(r.Created_Date), MAX(r.Created_Date)) > 0
        THEN CAST(COUNT(r.ID) AS FLOAT) / DATEDIFF(MONTH, MIN(r.Created_Date), MAX(r.Created_Date))
        ELSE 0
    END as BookingFrequency,  -- จำนวนการจองต่อเดือน

    -- Customer Status
    CASE
        WHEN DATEDIFF(DAY, MAX(r.Created_Date), GETDATE()) > 365 THEN 'INACTIVE'
        WHEN DATEDIFF(DAY, MAX(r.Created_Date), GETDATE()) > 180 THEN 'AT_RISK'
        ELSE 'ACTIVE'
    END as CustomerStatus,

    -- Customer Tier
    CASE
        WHEN COUNT(r.ID) >= 10 OR SUM(r.TotalPrice) >= 100000 THEN 'PLATINUM'
        WHEN COUNT(r.ID) >= 5 OR SUM(r.TotalPrice) >= 50000 THEN 'GOLD'
        WHEN COUNT(r.ID) >= 2 OR SUM(r.TotalPrice) >= 20000 THEN 'SILVER'
        ELSE 'BRONZE'
    END as CustomerTier

FROM
    Customer c
    INNER JOIN Reservation r ON c.MobilePhone = r.Customer_MobilePhone
WHERE
    r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
GROUP BY
    c.MobilePhone,
    c.FullName,
    c.Name,
    c.Email;
GO

-- =============================================
-- View 5: vw_MonthlyCustomerStats
-- สถิติลูกค้ารายเดือน
-- =============================================
IF OBJECT_ID('dbo.vw_MonthlyCustomerStats', 'V') IS NOT NULL
    DROP VIEW dbo.vw_MonthlyCustomerStats;
GO

CREATE VIEW dbo.vw_MonthlyCustomerStats
AS
SELECT
    YEAR(r.Created_Date) as Year,
    MONTH(r.Created_Date) as Month,

    -- Customer Counts
    COUNT(DISTINCT r.Customer_MobilePhone) as TotalCustomers,
    COUNT(DISTINCT CASE WHEN FirstBooking.IsFirst = 1 THEN r.Customer_MobilePhone END) as NewCustomers,
    COUNT(DISTINCT CASE WHEN FirstBooking.IsFirst = 0 THEN r.Customer_MobilePhone END) as ReturningCustomers,

    -- Booking Counts
    COUNT(r.ID) as TotalBookings,

    -- Revenue
    SUM(r.TotalPrice) as TotalRevenue,
    AVG(r.TotalPrice) as AvgBookingValue,

    -- Metrics
    CAST(COUNT(DISTINCT CASE WHEN FirstBooking.IsFirst = 0 THEN r.Customer_MobilePhone END) AS FLOAT)
        / NULLIF(COUNT(DISTINCT r.Customer_MobilePhone), 0) * 100 as RepeatCustomerRate

FROM
    Reservation r
    CROSS APPLY (
        SELECT CASE
            WHEN r.Created_Date = (
                SELECT MIN(Created_Date)
                FROM Reservation
                WHERE Customer_MobilePhone = r.Customer_MobilePhone
                AND Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
            ) THEN 1
            ELSE 0
        END as IsFirst
    ) FirstBooking
WHERE
    r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
GROUP BY
    YEAR(r.Created_Date),
    MONTH(r.Created_Date);
GO

-- =============================================
-- สร้าง Index สำหรับเพิ่มประสิทธิภาพ
-- =============================================

-- Index สำหรับ Customer table
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Customer_MobilePhone_Name')
    CREATE NONCLUSTERED INDEX IX_Customer_MobilePhone_Name
    ON Customer(MobilePhone)
    INCLUDE (FullName, Name, Email);
GO

-- Index สำหรับ Reservation table
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Reservation_Customer_Date_Status')
    CREATE NONCLUSTERED INDEX IX_Reservation_Customer_Date_Status
    ON Reservation(Customer_MobilePhone, Created_Date, Status)
    INCLUDE (TotalPrice, CheckInDate, CheckOutDate);
GO

-- Index สำหรับ Reservation_Accommodation
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ReservationAccommodation_ReservationID')
    CREATE NONCLUSTERED INDEX IX_ReservationAccommodation_ReservationID
    ON Reservation_Accommodation(Reservation_ID, Accommodation_ID);
GO

PRINT '✅ Customer Analytics Views สร้างสำเร็จ!'
PRINT ''
PRINT 'Views ที่สร้าง:'
PRINT '1. vw_CustomerSegmentation - แบ่งประเภทลูกค้า (NEW, RETURNING, VIP)'
PRINT '2. vw_RepeatCustomers - ลูกค้าที่กลับมาพักซ้ำทั้งหมด'
PRINT '3. vw_RepeatCustomersByYear - ลูกค้าที่กลับมาพักซ้ำแยกตามปี'
PRINT '4. vw_CustomerLifetimeValue - CLV และข้อมูลเชิงลึก'
PRINT '5. vw_MonthlyCustomerStats - สถิติลูกค้ารายเดือน'
PRINT ''
PRINT 'Indexes ที่สร้าง:'
PRINT '- IX_Customer_MobilePhone_Name'
PRINT '- IX_Reservation_Customer_Date_Status'
PRINT '- IX_ReservationAccommodation_ReservationID'
GO
