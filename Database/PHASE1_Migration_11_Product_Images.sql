-- =============================================
-- Migration 11: Product Images System
-- Purpose: Add comprehensive image management for products/accommodations
-- Author: Claude AI
-- Date: 2025-11-05
-- =============================================

USE [Taketime]
GO

PRINT '========================================';
PRINT 'Migration 11: Product Images System';
PRINT '========================================';
PRINT '';

-- =============================================
-- 1. Create Product_Images Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Product_Images')
BEGIN
    CREATE TABLE [dbo].[Product_Images] (
        [ID] bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ProductType] nvarchar(20) NOT NULL, -- ACCOMMODATION/ITEM
        [Product_ID] int NOT NULL,
        [ImageURL] nvarchar(500) NOT NULL,
        [ThumbnailURL] nvarchar(500) NULL,
        [ImageOrder] tinyint NOT NULL DEFAULT 1,
        [IsMainImage] bit NOT NULL DEFAULT 0,
        [Caption] nvarchar(200) NULL,
        [AltText] nvarchar(200) NULL, -- For SEO/accessibility
        [FileSize] int NULL, -- in bytes
        [ImageWidth] smallint NULL, -- pixels
        [ImageHeight] smallint NULL, -- pixels
        [UploadedDate] datetime NOT NULL DEFAULT GETDATE(),
        [UploadedBy_AdminID] smallint NULL,
        [Status] bit NOT NULL DEFAULT 1, -- 1=Active, 0=Inactive
        CONSTRAINT [FK_Product_Images_Admin] FOREIGN KEY ([UploadedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [CK_Product_Images_ProductType] CHECK ([ProductType] IN ('ACCOMMODATION', 'ITEM')),
        CONSTRAINT [CK_Product_Images_Order] CHECK ([ImageOrder] >= 1 AND [ImageOrder] <= 100)
    );

    PRINT '✅ Product_Images table created';
END
ELSE
    PRINT '⚠️ Product_Images table already exists';
GO

-- Create indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Product_Images_Product')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Product_Images_Product]
    ON [dbo].[Product_Images] ([ProductType], [Product_ID], [ImageOrder])
    WHERE [Status] = 1;
    PRINT '✅ Index IX_Product_Images_Product created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Product_Images_Main')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Product_Images_Main]
    ON [dbo].[Product_Images] ([ProductType], [Product_ID])
    WHERE [IsMainImage] = 1 AND [Status] = 1;
    PRINT '✅ Index IX_Product_Images_Main created';
END
GO

-- =============================================
-- 2. Create Image_Upload_Log Table (Audit Trail)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Image_Upload_Log')
BEGIN
    CREATE TABLE [dbo].[Image_Upload_Log] (
        [ID] bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ProductImage_ID] bigint NULL,
        [Action] nvarchar(20) NOT NULL, -- UPLOAD/UPDATE/DELETE
        [ProductType] nvarchar(20) NOT NULL,
        [Product_ID] int NOT NULL,
        [OriginalFileName] nvarchar(255) NULL,
        [FileSize] int NULL,
        [AdminID] smallint NULL,
        [IPAddress] nvarchar(45) NULL,
        [ActionDate] datetime NOT NULL DEFAULT GETDATE(),
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [FK_Image_Upload_Log_ProductImage] FOREIGN KEY ([ProductImage_ID])
            REFERENCES [dbo].[Product_Images]([ID]) ON DELETE CASCADE,
        CONSTRAINT [FK_Image_Upload_Log_Admin] FOREIGN KEY ([AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    PRINT '✅ Image_Upload_Log table created';
END
ELSE
    PRINT '⚠️ Image_Upload_Log table already exists';
GO

-- =============================================
-- 3. Create Views for Easy Querying
-- =============================================

-- View: Accommodation with Main Image
IF OBJECT_ID('dbo.vw_AccommodationWithImages', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_AccommodationWithImages];
GO

CREATE VIEW [dbo].[vw_AccommodationWithImages]
AS
SELECT
    a.ID AS AccommodationID,
    a.AccomName,
    a.Price,
    a.People,
    a.Status,
    pi.ID AS MainImageID,
    pi.ImageURL AS MainImageURL,
    pi.ThumbnailURL AS MainThumbnailURL,
    pi.Caption AS MainImageCaption,
    (SELECT COUNT(*) FROM Product_Images WHERE ProductType = 'ACCOMMODATION' AND Product_ID = a.ID AND Status = 1) AS TotalImages
FROM [dbo].[Accommodation] a
LEFT JOIN [dbo].[Product_Images] pi ON a.ID = pi.Product_ID
    AND pi.ProductType = 'ACCOMMODATION'
    AND pi.IsMainImage = 1
    AND pi.Status = 1
WHERE a.Status = 1;
GO

PRINT '✅ View vw_AccommodationWithImages created';
GO

-- View: Items with Main Image
IF OBJECT_ID('dbo.vw_ItemsWithImages', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_ItemsWithImages];
GO

CREATE VIEW [dbo].[vw_ItemsWithImages]
AS
SELECT
    i.ID AS ItemID,
    i.ItemName,
    i.Price,
    i.Amount AS AvailableAmount,
    i.Status,
    pi.ID AS MainImageID,
    pi.ImageURL AS MainImageURL,
    pi.ThumbnailURL AS MainThumbnailURL,
    pi.Caption AS MainImageCaption,
    (SELECT COUNT(*) FROM Product_Images WHERE ProductType = 'ITEM' AND Product_ID = i.ID AND Status = 1) AS TotalImages
FROM [dbo].[Items] i
LEFT JOIN [dbo].[Product_Images] pi ON i.ID = pi.Product_ID
    AND pi.ProductType = 'ITEM'
    AND pi.IsMainImage = 1
    AND pi.Status = 1
WHERE i.Status = 1;
GO

PRINT '✅ View vw_ItemsWithImages created';
GO

-- =============================================
-- 4. Create Stored Procedures
-- =============================================

-- Procedure: Set Main Image
IF OBJECT_ID('dbo.sp_SetMainImage', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_SetMainImage];
GO

CREATE PROCEDURE [dbo].[sp_SetMainImage]
    @ImageID bigint,
    @AdminID smallint = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ProductType nvarchar(20);
    DECLARE @ProductID int;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Get product info
        SELECT @ProductType = ProductType, @ProductID = Product_ID
        FROM [dbo].[Product_Images]
        WHERE ID = @ImageID;

        IF @ProductType IS NULL
        BEGIN
            RAISERROR('Image not found', 16, 1);
            RETURN -1;
        END

        -- Unset other main images for this product
        UPDATE [dbo].[Product_Images]
        SET IsMainImage = 0
        WHERE ProductType = @ProductType
        AND Product_ID = @ProductID
        AND ID != @ImageID;

        -- Set this image as main
        UPDATE [dbo].[Product_Images]
        SET IsMainImage = 1
        WHERE ID = @ImageID;

        -- Log action
        INSERT INTO [dbo].[Image_Upload_Log] (
            ProductImage_ID, Action, ProductType, Product_ID, AdminID, Notes
        )
        VALUES (
            @ImageID, 'UPDATE', @ProductType, @ProductID, @AdminID, 'Set as main image'
        );

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

PRINT '✅ Stored procedure sp_SetMainImage created';
GO

-- Procedure: Reorder Images
IF OBJECT_ID('dbo.sp_ReorderProductImages', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_ReorderProductImages];
GO

CREATE PROCEDURE [dbo].[sp_ReorderProductImages]
    @ImageID bigint,
    @NewOrder tinyint
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ProductType nvarchar(20);
    DECLARE @ProductID int;
    DECLARE @OldOrder tinyint;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Get current info
        SELECT @ProductType = ProductType, @ProductID = Product_ID, @OldOrder = ImageOrder
        FROM [dbo].[Product_Images]
        WHERE ID = @ImageID;

        IF @ProductType IS NULL
        BEGIN
            RAISERROR('Image not found', 16, 1);
            RETURN -1;
        END

        -- Adjust orders of affected images
        IF @NewOrder < @OldOrder
        BEGIN
            -- Moving up: shift others down
            UPDATE [dbo].[Product_Images]
            SET ImageOrder = ImageOrder + 1
            WHERE ProductType = @ProductType
            AND Product_ID = @ProductID
            AND ImageOrder >= @NewOrder
            AND ImageOrder < @OldOrder;
        END
        ELSE IF @NewOrder > @OldOrder
        BEGIN
            -- Moving down: shift others up
            UPDATE [dbo].[Product_Images]
            SET ImageOrder = ImageOrder - 1
            WHERE ProductType = @ProductType
            AND Product_ID = @ProductID
            AND ImageOrder > @OldOrder
            AND ImageOrder <= @NewOrder;
        END

        -- Set new order
        UPDATE [dbo].[Product_Images]
        SET ImageOrder = @NewOrder
        WHERE ID = @ImageID;

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

PRINT '✅ Stored procedure sp_ReorderProductImages created';
GO

-- Procedure: Delete Image (Soft Delete)
IF OBJECT_ID('dbo.sp_DeleteProductImage', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_DeleteProductImage];
GO

CREATE PROCEDURE [dbo].[sp_DeleteProductImage]
    @ImageID bigint,
    @AdminID smallint = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ProductType nvarchar(20);
    DECLARE @ProductID int;
    DECLARE @IsMainImage bit;
    DECLARE @ImageURL nvarchar(500);

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Get image info
        SELECT @ProductType = ProductType, @ProductID = Product_ID,
               @IsMainImage = IsMainImage, @ImageURL = ImageURL
        FROM [dbo].[Product_Images]
        WHERE ID = @ImageID;

        IF @ProductType IS NULL
        BEGIN
            RAISERROR('Image not found', 16, 1);
            RETURN -1;
        END

        -- Soft delete
        UPDATE [dbo].[Product_Images]
        SET Status = 0
        WHERE ID = @ImageID;

        -- If this was main image, set next image as main
        IF @IsMainImage = 1
        BEGIN
            DECLARE @NextImageID bigint;

            -- Find the next image to promote
            SELECT TOP 1 @NextImageID = ID
            FROM [dbo].[Product_Images]
            WHERE ProductType = @ProductType
            AND Product_ID = @ProductID
            AND ID != @ImageID
            AND Status = 1
            ORDER BY ImageOrder;

            -- Update it to main image
            IF @NextImageID IS NOT NULL
            BEGIN
                UPDATE [dbo].[Product_Images]
                SET IsMainImage = 1
                WHERE ID = @NextImageID;
            END
        END

        -- Log deletion
        INSERT INTO [dbo].[Image_Upload_Log] (
            ProductImage_ID, Action, ProductType, Product_ID,
            AdminID, Notes, OriginalFileName
        )
        VALUES (
            @ImageID, 'DELETE', @ProductType, @ProductID,
            @AdminID, 'Image soft deleted', @ImageURL
        );

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

PRINT '✅ Stored procedure sp_DeleteProductImage created';
GO

-- =============================================
-- 5. Create Function to Get Main Image URL
-- =============================================
IF OBJECT_ID('dbo.fn_GetMainImageURL', 'FN') IS NOT NULL
    DROP FUNCTION [dbo].[fn_GetMainImageURL];
GO

CREATE FUNCTION [dbo].[fn_GetMainImageURL]
(
    @ProductType nvarchar(20),
    @ProductID int
)
RETURNS nvarchar(500)
AS
BEGIN
    DECLARE @ImageURL nvarchar(500);

    SELECT TOP 1 @ImageURL = ImageURL
    FROM [dbo].[Product_Images]
    WHERE ProductType = @ProductType
    AND Product_ID = @ProductID
    AND IsMainImage = 1
    AND Status = 1;

    -- If no main image, get first image
    IF @ImageURL IS NULL
    BEGIN
        SELECT TOP 1 @ImageURL = ImageURL
        FROM [dbo].[Product_Images]
        WHERE ProductType = @ProductType
        AND Product_ID = @ProductID
        AND Status = 1
        ORDER BY ImageOrder;
    END

    RETURN @ImageURL;
END
GO

PRINT '✅ Function fn_GetMainImageURL created';
GO

-- =============================================
-- 6. Migrate Existing Images (Optional)
-- =============================================
PRINT '📦 Checking for existing accommodation images...';

-- This section can be customized based on current image storage
-- Example: If images are stored in ~/Images/Accommodation/{ID}/ folder
-- You can scan and populate the table

DECLARE @MigrationNote nvarchar(500) =
'Note: Manual image migration may be needed.
If you have existing images in folders, you can insert them using:

INSERT INTO Product_Images (ProductType, Product_ID, ImageURL, ImageOrder, IsMainImage)
VALUES (''ACCOMMODATION'', 1, ''~/Images/Accommodation/1/room1.jpg'', 1, 1);

Run this for each existing image.';

PRINT @MigrationNote;
GO

-- =============================================
-- 7. Grant Permissions
-- =============================================
GRANT SELECT ON [dbo].[vw_AccommodationWithImages] TO PUBLIC;
GRANT SELECT ON [dbo].[vw_ItemsWithImages] TO PUBLIC;
GRANT EXECUTE ON [dbo].[sp_SetMainImage] TO PUBLIC;
GRANT EXECUTE ON [dbo].[sp_ReorderProductImages] TO PUBLIC;
GRANT EXECUTE ON [dbo].[sp_DeleteProductImage] TO PUBLIC;

PRINT '✅ Permissions granted';
GO

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '';
PRINT '========================================';
PRINT 'Migration 11 Verification';
PRINT '========================================';

-- Check tables
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Product_Images')
    PRINT '✅ Product_Images table exists';
ELSE
    PRINT '❌ Product_Images table missing';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Image_Upload_Log')
    PRINT '✅ Image_Upload_Log table exists';
ELSE
    PRINT '❌ Image_Upload_Log table missing';

-- Check indexes
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Product_Images_Product')
    PRINT '✅ IX_Product_Images_Product exists';

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Product_Images_Main')
    PRINT '✅ IX_Product_Images_Main exists';

-- Check views
IF OBJECT_ID('dbo.vw_AccommodationWithImages', 'V') IS NOT NULL
    PRINT '✅ vw_AccommodationWithImages exists';

IF OBJECT_ID('dbo.vw_ItemsWithImages', 'V') IS NOT NULL
    PRINT '✅ vw_ItemsWithImages exists';

-- Check stored procedures
IF OBJECT_ID('dbo.sp_SetMainImage', 'P') IS NOT NULL
    PRINT '✅ sp_SetMainImage exists';

IF OBJECT_ID('dbo.sp_ReorderProductImages', 'P') IS NOT NULL
    PRINT '✅ sp_ReorderProductImages exists';

IF OBJECT_ID('dbo.sp_DeleteProductImage', 'P') IS NOT NULL
    PRINT '✅ sp_DeleteProductImage exists';

-- Check function
IF OBJECT_ID('dbo.fn_GetMainImageURL', 'FN') IS NOT NULL
    PRINT '✅ fn_GetMainImageURL exists';

PRINT '';
PRINT '========================================';
PRINT '✅ Migration 11 completed successfully!';
PRINT '========================================';
PRINT '';
PRINT '📝 Next Steps:';
PRINT '1. Review and test migrations on dev database';
PRINT '2. Create image upload UI';
PRINT '3. Migrate existing images (if any)';
PRINT '4. Update application code to use new tables';
GO
