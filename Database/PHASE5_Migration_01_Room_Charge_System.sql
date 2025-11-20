-- =============================================
-- Migration: PHASE5_01 - Room Charge System
-- Purpose: Integrate POS with Reservation System for room charges
-- Author: Claude AI
-- Date: 2025-11-06
-- =============================================

USE [Taketime]
GO

PRINT '========================================';
PRINT 'PHASE 5 Migration 01: Room Charge System';
PRINT '========================================';
PRINT '';

-- =============================================
-- 1. Create Reservation_Product_Charges Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reservation_Product_Charges')
BEGIN
    PRINT '📦 Creating Reservation_Product_Charges table...';

    CREATE TABLE [dbo].[Reservation_Product_Charges] (
        [ID] bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,

        -- Core References
        [Reservation_ID] int NOT NULL,
        [Product_ID] int NOT NULL,

        -- Product Info (snapshot at time of charge)
        [Product_Name] nvarchar(255) NOT NULL,
        [Product_Barcode] nvarchar(50) NULL,
        [Category_ID] int NULL,

        -- Quantity & Pricing
        [Quantity] decimal(10,2) NOT NULL DEFAULT 1,
        [UnitPrice] decimal(10,2) NOT NULL,
        [TotalAmount] decimal(10,2) NOT NULL,

        -- Status & Tracking
        [Status] nvarchar(20) NOT NULL DEFAULT 'PENDING',
            -- PENDING: Charged to room, not paid
            -- PAID: Payment collected
            -- CANCELLED: Deleted/refunded, stock returned

        [ChargeType] nvarchar(20) NOT NULL DEFAULT 'ROOM_CHARGE',
            -- ROOM_CHARGE: Deferred payment
            -- IMMEDIATE: Paid immediately at POS
            -- PRE_BOOKING: Pre-booked with reservation

        [IsPaid] bit NOT NULL DEFAULT 0,

        -- Payment Tracking
        [Receipt_ID] nvarchar(50) NULL, -- Link to Account_Receipt when paid
        [PaymentDate] datetime NULL,

        -- Stock Management
        [StockDeducted] bit NOT NULL DEFAULT 1,
        [StockReturned] bit NOT NULL DEFAULT 0,
        [OriginalStock] int NULL, -- For audit trail

        -- Audit Fields
        [ChargedDate] datetime NOT NULL DEFAULT GETDATE(),
        [ChargedBy_AdminID] smallint NULL,
        [CancelledDate] datetime NULL,
        [CancelledBy_AdminID] smallint NULL,
        [CancelReason] nvarchar(500) NULL,
        [Notes] nvarchar(500) NULL,

        -- Foreign Keys
        CONSTRAINT [FK_ReservationProductCharges_Reservation]
            FOREIGN KEY ([Reservation_ID]) REFERENCES [dbo].[Reservation]([ID]),

        CONSTRAINT [FK_ReservationProductCharges_Product]
            FOREIGN KEY ([Product_ID]) REFERENCES [dbo].[Product]([ID]),

        CONSTRAINT [FK_ReservationProductCharges_Admin_Charged]
            FOREIGN KEY ([ChargedBy_AdminID]) REFERENCES [dbo].[Admin]([ID]),

        CONSTRAINT [FK_ReservationProductCharges_Admin_Cancelled]
            FOREIGN KEY ([CancelledBy_AdminID]) REFERENCES [dbo].[Admin]([ID]),

        -- Constraints
        CONSTRAINT [CK_ReservationProductCharges_Status]
            CHECK ([Status] IN ('PENDING', 'PAID', 'CANCELLED')),

        CONSTRAINT [CK_ReservationProductCharges_ChargeType]
            CHECK ([ChargeType] IN ('ROOM_CHARGE', 'IMMEDIATE', 'PRE_BOOKING')),

        CONSTRAINT [CK_ReservationProductCharges_Quantity]
            CHECK ([Quantity] > 0),

        CONSTRAINT [CK_ReservationProductCharges_Amount]
            CHECK ([TotalAmount] >= 0)
    );

    PRINT '✅ Reservation_Product_Charges table created';
END
ELSE
    PRINT '⚠️ Reservation_Product_Charges table already exists';
GO

-- =============================================
-- 2. Create Indexes
-- =============================================
PRINT '📊 Creating indexes...';

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ReservationProductCharges_Reservation')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ReservationProductCharges_Reservation]
    ON [dbo].[Reservation_Product_Charges] ([Reservation_ID], [Status])
    INCLUDE ([TotalAmount]);
    PRINT '✅ Index IX_ReservationProductCharges_Reservation created';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ReservationProductCharges_Product')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ReservationProductCharges_Product]
    ON [dbo].[Reservation_Product_Charges] ([Product_ID], [Status]);
    PRINT '✅ Index IX_ReservationProductCharges_Product created';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ReservationProductCharges_Receipt')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ReservationProductCharges_Receipt]
    ON [dbo].[Reservation_Product_Charges] ([Receipt_ID])
    WHERE [Receipt_ID] IS NOT NULL;
    PRINT '✅ Index IX_ReservationProductCharges_Receipt created';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ReservationProductCharges_ChargedDate')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ReservationProductCharges_ChargedDate]
    ON [dbo].[Reservation_Product_Charges] ([ChargedDate] DESC);
    PRINT '✅ Index IX_ReservationProductCharges_ChargedDate created';
END
GO

-- =============================================
-- 3. Modify Product Table - Add CanPreBook Column
-- =============================================
PRINT '🔧 Checking Product table modifications...';

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('Product')
    AND name = 'CanPreBook'
)
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD [CanPreBook] bit NOT NULL DEFAULT 0;

    PRINT '✅ Added CanPreBook column to Product table';

    -- Create index for pre-bookable products
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Product_PreBook')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Product_PreBook]
        ON [dbo].[Product] ([CanPreBook])
        WHERE [Status] = 'True' AND [CanPreBook] = 1;
        PRINT '✅ Index IX_Product_PreBook created';
    END
END
ELSE
    PRINT '⚠️ CanPreBook column already exists in Product table';
GO

-- =============================================
-- 4. Modify Account_Receipt_Detail Table
-- =============================================
PRINT '🔧 Checking Account_Receipt_Detail modifications...';

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('Account_Receipt_Detail')
    AND name = 'ReservationProductCharge_ID'
)
BEGIN
    ALTER TABLE [dbo].[Account_Receipt_Detail]
    ADD [ReservationProductCharge_ID] bigint NULL;

    PRINT '✅ Added ReservationProductCharge_ID column to Account_Receipt_Detail';

    -- Add foreign key constraint
    ALTER TABLE [dbo].[Account_Receipt_Detail]
    ADD CONSTRAINT [FK_ReceiptDetail_ReservationProductCharge]
        FOREIGN KEY ([ReservationProductCharge_ID])
        REFERENCES [dbo].[Reservation_Product_Charges]([ID]);

    PRINT '✅ Added foreign key FK_ReceiptDetail_ReservationProductCharge';
END
ELSE
    PRINT '⚠️ ReservationProductCharge_ID column already exists';
GO

-- =============================================
-- 5. Create Views
-- =============================================
PRINT '👁️ Creating views...';

-- View: Active Guest Reservations (for POS dropdown)
IF OBJECT_ID('dbo.vw_ActiveGuestReservations', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_ActiveGuestReservations];
GO

CREATE VIEW [dbo].[vw_ActiveGuestReservations]
AS
SELECT
    r.ID AS ReservationID,
    r.CheckInDate,
    r.CheckOutDate,
    r.Name AS GuestName,
    r.MobilePhone,
    r.TotalPrice,
    r.Deposit,
    r.Status,
    (SELECT ISNULL(SUM(PaymentAmount), 0)
     FROM Payment_History
     WHERE Reservation_ID = r.ID AND Status = 'COMPLETED') AS TotalPaid,
    (r.TotalPrice - ISNULL((SELECT SUM(PaymentAmount)
                            FROM Payment_History
                            WHERE Reservation_ID = r.ID AND Status = 'COMPLETED'), 0)) AS RemainingBalance,
    (SELECT ISNULL(SUM(TotalAmount), 0)
     FROM Reservation_Product_Charges
     WHERE Reservation_ID = r.ID AND Status = 'PENDING') AS PendingCharges,
    -- Display text for dropdown: "Room 101 - John Doe (0812345678)"
    CONCAT(
        CASE
            WHEN r.Accom_ID IS NOT NULL THEN
                CONCAT('Room ', (SELECT AccomName FROM Accommodation WHERE ID = r.Accom_ID), ' - ')
            ELSE ''
        END,
        r.Name,
        ' (',
        r.MobilePhone,
        ')'
    ) AS DisplayText
FROM [dbo].[Reservation] r
WHERE r.Status IN (N'เข้าพักแล้ว', N'จองเรียบร้อย')
  AND r.CheckInDate <= GETDATE()
  AND (r.CheckOutDate >= GETDATE() OR r.CheckOutDate IS NULL);
GO

PRINT '✅ View vw_ActiveGuestReservations created';
GO

-- View: Reservation Product Charges Summary
IF OBJECT_ID('dbo.vw_ReservationProductCharges', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_ReservationProductCharges];
GO

CREATE VIEW [dbo].[vw_ReservationProductCharges]
AS
SELECT
    rpc.ID,
    rpc.Reservation_ID,
    rpc.Product_ID,
    rpc.Product_Name,
    rpc.Product_Barcode,
    rpc.Quantity,
    rpc.UnitPrice,
    rpc.TotalAmount,
    rpc.Status,
    rpc.ChargeType,
    rpc.IsPaid,
    rpc.ChargedDate,
    rpc.Receipt_ID,
    rpc.PaymentDate,
    rpc.StockDeducted,
    rpc.StockReturned,
    rpc.Notes,
    rpc.CancelReason,
    p.Category_Name,
    a.Username AS ChargedBy,
    ca.Username AS CancelledBy,
    r.Name AS GuestName,
    r.MobilePhone,
    r.CheckInDate,
    r.CheckOutDate
FROM [dbo].[Reservation_Product_Charges] rpc
INNER JOIN [dbo].[Reservation] r ON rpc.Reservation_ID = r.ID
LEFT JOIN [dbo].[Product] p ON rpc.Product_ID = p.ID
LEFT JOIN [dbo].[Product_Category] pc ON rpc.Category_ID = pc.ID
LEFT JOIN [dbo].[Admin] a ON rpc.ChargedBy_AdminID = a.ID
LEFT JOIN [dbo].[Admin] ca ON rpc.CancelledBy_AdminID = ca.ID;
GO

PRINT '✅ View vw_ReservationProductCharges created';
GO

-- View: Reservation Total Charges Summary
IF OBJECT_ID('dbo.vw_ReservationTotalCharges', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_ReservationTotalCharges];
GO

CREATE VIEW [dbo].[vw_ReservationTotalCharges]
AS
SELECT
    r.ID AS ReservationID,
    r.Name AS GuestName,
    r.MobilePhone,
    r.CheckInDate,
    r.CheckOutDate,
    r.TotalPrice AS AccommodationTotal,
    ISNULL(SUM(CASE WHEN rpc.Status = 'PENDING' THEN rpc.TotalAmount ELSE 0 END), 0) AS PendingProductCharges,
    ISNULL(SUM(CASE WHEN rpc.Status = 'PAID' THEN rpc.TotalAmount ELSE 0 END), 0) AS PaidProductCharges,
    ISNULL(SUM(CASE WHEN rpc.Status = 'CANCELLED' THEN rpc.TotalAmount ELSE 0 END), 0) AS CancelledProductCharges,
    (r.TotalPrice + ISNULL(SUM(CASE WHEN rpc.Status IN ('PENDING', 'PAID') THEN rpc.TotalAmount ELSE 0 END), 0)) AS GrandTotal
FROM [dbo].[Reservation] r
LEFT JOIN [dbo].[Reservation_Product_Charges] rpc ON r.ID = rpc.Reservation_ID
GROUP BY r.ID, r.Name, r.MobilePhone, r.CheckInDate, r.CheckOutDate, r.TotalPrice;
GO

PRINT '✅ View vw_ReservationTotalCharges created';
GO

-- =============================================
-- 6. Create Stored Procedures
-- =============================================
PRINT '⚙️ Creating stored procedures...';

-- Procedure: Get Active Guests for POS
IF OBJECT_ID('dbo.sp_GetActiveGuestsForPOS', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_GetActiveGuestsForPOS];
GO

CREATE PROCEDURE [dbo].[sp_GetActiveGuestsForPOS]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ReservationID,
        GuestName,
        MobilePhone,
        CheckInDate,
        CheckOutDate,
        TotalPrice,
        TotalPaid,
        RemainingBalance,
        PendingCharges,
        DisplayText
    FROM [dbo].[vw_ActiveGuestReservations]
    ORDER BY CheckInDate DESC, GuestName;
END
GO

PRINT '✅ Stored procedure sp_GetActiveGuestsForPOS created';
GO

-- Procedure: Charge Products to Room
IF OBJECT_ID('dbo.sp_ChargeProductsToRoom', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_ChargeProductsToRoom];
GO

CREATE PROCEDURE [dbo].[sp_ChargeProductsToRoom]
    @ReservationID int,
    @ProductID int,
    @ProductName nvarchar(255),
    @ProductBarcode nvarchar(50) = NULL,
    @CategoryID int = NULL,
    @Quantity decimal(10,2),
    @UnitPrice decimal(10,2),
    @TotalAmount decimal(10,2),
    @ChargeType nvarchar(20) = 'ROOM_CHARGE',
    @AdminID smallint = NULL,
    @Notes nvarchar(500) = NULL,
    @NewChargeID bigint OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Get current stock
        DECLARE @CurrentStock decimal(10,2);
        SELECT @CurrentStock = Amount FROM Product WHERE ID = @ProductID;

        IF @CurrentStock IS NULL
        BEGIN
            RAISERROR('Product not found', 16, 1);
            RETURN -1;
        END

        IF @CurrentStock < @Quantity
        BEGIN
            RAISERROR('Insufficient stock', 16, 1);
            RETURN -2;
        END

        -- Deduct stock
        UPDATE Product
        SET Amount = Amount - @Quantity
        WHERE ID = @ProductID;

        -- Create charge record
        INSERT INTO Reservation_Product_Charges (
            Reservation_ID, Product_ID, Product_Name, Product_Barcode,
            Category_ID, Quantity, UnitPrice, TotalAmount, ChargeType,
            ChargedBy_AdminID, Notes, Status, IsPaid, StockDeducted,
            OriginalStock
        )
        VALUES (
            @ReservationID, @ProductID, @ProductName, @ProductBarcode,
            @CategoryID, @Quantity, @UnitPrice, @TotalAmount, @ChargeType,
            @AdminID, @Notes, 'PENDING', 0, 1,
            @CurrentStock
        );

        SET @NewChargeID = SCOPE_IDENTITY();

        -- Update reservation total (only for room charges, not immediate payment)
        IF @ChargeType = 'ROOM_CHARGE'
        BEGIN
            UPDATE Reservation
            SET TotalPrice = TotalPrice + @TotalAmount
            WHERE ID = @ReservationID;
        END

        COMMIT TRANSACTION;
        RETURN 0;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
        RETURN -99;
    END CATCH
END
GO

PRINT '✅ Stored procedure sp_ChargeProductsToRoom created';
GO

-- Procedure: Cancel Room Charge
IF OBJECT_ID('dbo.sp_CancelRoomCharge', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_CancelRoomCharge];
GO

CREATE PROCEDURE [dbo].[sp_CancelRoomCharge]
    @ChargeID bigint,
    @AdminID smallint = NULL,
    @CancelReason nvarchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ReservationID int;
    DECLARE @ProductID int;
    DECLARE @Quantity decimal(10,2);
    DECLARE @TotalAmount decimal(10,2);
    DECLARE @Status nvarchar(20);
    DECLARE @ChargeType nvarchar(20);

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Get charge details
        SELECT
            @ReservationID = Reservation_ID,
            @ProductID = Product_ID,
            @Quantity = Quantity,
            @TotalAmount = TotalAmount,
            @Status = Status,
            @ChargeType = ChargeType
        FROM Reservation_Product_Charges
        WHERE ID = @ChargeID;

        IF @ReservationID IS NULL
        BEGIN
            RAISERROR('Charge not found', 16, 1);
            RETURN -1;
        END

        IF @Status != 'PENDING'
        BEGIN
            RAISERROR('Can only cancel PENDING charges', 16, 1);
            RETURN -2;
        END

        -- Return stock
        UPDATE Product
        SET Amount = Amount + @Quantity
        WHERE ID = @ProductID;

        -- Mark as cancelled
        UPDATE Reservation_Product_Charges
        SET Status = 'CANCELLED',
            StockReturned = 1,
            CancelledDate = GETDATE(),
            CancelledBy_AdminID = @AdminID,
            CancelReason = @CancelReason
        WHERE ID = @ChargeID;

        -- Update reservation total (only for room charges)
        IF @ChargeType = 'ROOM_CHARGE'
        BEGIN
            UPDATE Reservation
            SET TotalPrice = TotalPrice - @TotalAmount
            WHERE ID = @ReservationID;
        END

        COMMIT TRANSACTION;
        RETURN 0;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
        RETURN -99;
    END CATCH
END
GO

PRINT '✅ Stored procedure sp_CancelRoomCharge created';
GO

-- Procedure: Mark Charges as Paid
IF OBJECT_ID('dbo.sp_MarkChargesAsPaid', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_MarkChargesAsPaid];
GO

CREATE PROCEDURE [dbo].[sp_MarkChargesAsPaid]
    @ReservationID int,
    @ReceiptID nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        UPDATE Reservation_Product_Charges
        SET Status = 'PAID',
            IsPaid = 1,
            Receipt_ID = @ReceiptID,
            PaymentDate = GETDATE()
        WHERE Reservation_ID = @ReservationID
        AND Status = 'PENDING';

        RETURN @@ROWCOUNT;
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
        RETURN -99;
    END CATCH
END
GO

PRINT '✅ Stored procedure sp_MarkChargesAsPaid created';
GO

-- =============================================
-- 7. Create Functions
-- =============================================
PRINT '📐 Creating functions...';

-- Function: Get Total Pending Charges
IF OBJECT_ID('dbo.fn_GetTotalPendingCharges', 'FN') IS NOT NULL
    DROP FUNCTION [dbo].[fn_GetTotalPendingCharges];
GO

CREATE FUNCTION [dbo].[fn_GetTotalPendingCharges]
(
    @ReservationID int
)
RETURNS decimal(10,2)
AS
BEGIN
    DECLARE @Total decimal(10,2);

    SELECT @Total = ISNULL(SUM(TotalAmount), 0)
    FROM Reservation_Product_Charges
    WHERE Reservation_ID = @ReservationID
    AND Status = 'PENDING';

    RETURN ISNULL(@Total, 0);
END
GO

PRINT '✅ Function fn_GetTotalPendingCharges created';
GO

-- Function: Check if Reservation has Pending Charges
IF OBJECT_ID('dbo.fn_HasPendingCharges', 'FN') IS NOT NULL
    DROP FUNCTION [dbo].[fn_HasPendingCharges];
GO

CREATE FUNCTION [dbo].[fn_HasPendingCharges]
(
    @ReservationID int
)
RETURNS bit
AS
BEGIN
    DECLARE @HasPending bit = 0;

    IF EXISTS (
        SELECT 1 FROM Reservation_Product_Charges
        WHERE Reservation_ID = @ReservationID
        AND Status = 'PENDING'
    )
        SET @HasPending = 1;

    RETURN @HasPending;
END
GO

PRINT '✅ Function fn_HasPendingCharges created';
GO

-- =============================================
-- 8. Grant Permissions
-- =============================================
PRINT '🔐 Granting permissions...';

GRANT SELECT ON [dbo].[vw_ActiveGuestReservations] TO PUBLIC;
GRANT SELECT ON [dbo].[vw_ReservationProductCharges] TO PUBLIC;
GRANT SELECT ON [dbo].[vw_ReservationTotalCharges] TO PUBLIC;
GRANT EXECUTE ON [dbo].[sp_GetActiveGuestsForPOS] TO PUBLIC;
GRANT EXECUTE ON [dbo].[sp_ChargeProductsToRoom] TO PUBLIC;
GRANT EXECUTE ON [dbo].[sp_CancelRoomCharge] TO PUBLIC;
GRANT EXECUTE ON [dbo].[sp_MarkChargesAsPaid] TO PUBLIC;

PRINT '✅ Permissions granted';
GO

-- =============================================
-- 9. Sample Data (Optional - for testing)
-- =============================================
PRINT '';
PRINT '🧪 Sample data setup (optional)...';

-- Mark some products as pre-bookable
-- Example: BBQ packages, breakfast sets, etc.
/*
UPDATE Product
SET CanPreBook = 1
WHERE Product_Name IN (
    N'ชุดบาร์บีคิว',
    N'เซ็ตอาหารเช้า',
    N'ชุดชาบู'
);

PRINT '✅ Marked sample products as pre-bookable';
*/

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '';
PRINT '========================================';
PRINT 'Migration Verification';
PRINT '========================================';

-- Check tables
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Reservation_Product_Charges')
    PRINT '✅ Reservation_Product_Charges table exists';
ELSE
    PRINT '❌ Reservation_Product_Charges table missing';

-- Check columns
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Product') AND name = 'CanPreBook')
    PRINT '✅ Product.CanPreBook column exists';
ELSE
    PRINT '❌ Product.CanPreBook column missing';

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Account_Receipt_Detail') AND name = 'ReservationProductCharge_ID')
    PRINT '✅ Account_Receipt_Detail.ReservationProductCharge_ID column exists';
ELSE
    PRINT '❌ Account_Receipt_Detail.ReservationProductCharge_ID column missing';

-- Check indexes
DECLARE @IndexCount int;
SELECT @IndexCount = COUNT(*)
FROM sys.indexes
WHERE name LIKE 'IX_ReservationProductCharges_%';
PRINT '✅ Created ' + CAST(@IndexCount AS nvarchar) + ' indexes for Reservation_Product_Charges';

-- Check views
IF OBJECT_ID('dbo.vw_ActiveGuestReservations', 'V') IS NOT NULL
    PRINT '✅ vw_ActiveGuestReservations exists';
ELSE
    PRINT '❌ vw_ActiveGuestReservations missing';

IF OBJECT_ID('dbo.vw_ReservationProductCharges', 'V') IS NOT NULL
    PRINT '✅ vw_ReservationProductCharges exists';
ELSE
    PRINT '❌ vw_ReservationProductCharges missing';

IF OBJECT_ID('dbo.vw_ReservationTotalCharges', 'V') IS NOT NULL
    PRINT '✅ vw_ReservationTotalCharges exists';
ELSE
    PRINT '❌ vw_ReservationTotalCharges missing';

-- Check stored procedures
IF OBJECT_ID('dbo.sp_GetActiveGuestsForPOS', 'P') IS NOT NULL
    PRINT '✅ sp_GetActiveGuestsForPOS exists';

IF OBJECT_ID('dbo.sp_ChargeProductsToRoom', 'P') IS NOT NULL
    PRINT '✅ sp_ChargeProductsToRoom exists';

IF OBJECT_ID('dbo.sp_CancelRoomCharge', 'P') IS NOT NULL
    PRINT '✅ sp_CancelRoomCharge exists';

IF OBJECT_ID('dbo.sp_MarkChargesAsPaid', 'P') IS NOT NULL
    PRINT '✅ sp_MarkChargesAsPaid exists';

-- Check functions
IF OBJECT_ID('dbo.fn_GetTotalPendingCharges', 'FN') IS NOT NULL
    PRINT '✅ fn_GetTotalPendingCharges exists';

IF OBJECT_ID('dbo.fn_HasPendingCharges', 'FN') IS NOT NULL
    PRINT '✅ fn_HasPendingCharges exists';

-- Summary
PRINT '';
PRINT '========================================';
PRINT '✅ PHASE 5 Migration 01 Completed!';
PRINT '========================================';
PRINT '';
PRINT '📝 Next Steps:';
PRINT '1. Review migration results above';
PRINT '2. Test views and stored procedures';
PRINT '3. Implement RoomChargeService.cs and RoomChargeDataAccess.cs';
PRINT '4. Modify Product/Default.aspx to add room charge UI';
PRINT '5. Modify Reserve.aspx to show product charges';
PRINT '6. Test end-to-end workflows';
PRINT '';
PRINT '📚 Documentation:';
PRINT 'See /Docs/ROOM_CHARGE_SYSTEM_DESIGN.md for full implementation guide';
GO
