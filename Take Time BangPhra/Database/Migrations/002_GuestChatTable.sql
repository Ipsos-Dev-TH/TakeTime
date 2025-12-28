-- =============================================
-- Guest Chat Table
-- For guest messaging system
-- Created: 2025-12-28
-- =============================================

-- Create Guest_Chat table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Guest_Chat')
BEGIN
    CREATE TABLE Guest_Chat (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        Reservation_ID BIGINT NOT NULL,
        Guest_Phone NVARCHAR(50) NULL,
        Message NVARCHAR(MAX) NOT NULL,
        IsFromGuest BIT NOT NULL DEFAULT 1,
        Created_Date DATETIME NOT NULL DEFAULT GETDATE(),
        Is_Read BIT NULL DEFAULT 0,
        Read_Date DATETIME NULL,
        Guest_Read BIT NULL DEFAULT 0,
        Guest_Read_Date DATETIME NULL,
        Staff_Name NVARCHAR(100) NULL,

        -- Foreign key to Reservation
        CONSTRAINT FK_Guest_Chat_Reservation FOREIGN KEY (Reservation_ID)
            REFERENCES Reservation(ID)
    );

    -- Create indexes for performance
    CREATE NONCLUSTERED INDEX IX_Guest_Chat_Reservation_ID
        ON Guest_Chat(Reservation_ID);

    CREATE NONCLUSTERED INDEX IX_Guest_Chat_Created_Date
        ON Guest_Chat(Created_Date DESC);

    CREATE NONCLUSTERED INDEX IX_Guest_Chat_Unread
        ON Guest_Chat(IsFromGuest, Is_Read)
        WHERE IsFromGuest = 1 AND (Is_Read = 0 OR Is_Read IS NULL);

    PRINT 'Guest_Chat table created successfully.';
END
ELSE
BEGIN
    PRINT 'Guest_Chat table already exists.';

    -- Add any missing columns if table exists
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Guest_Chat') AND name = 'Staff_Name')
    BEGIN
        ALTER TABLE Guest_Chat ADD Staff_Name NVARCHAR(100) NULL;
        PRINT 'Added Staff_Name column.';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Guest_Chat') AND name = 'Guest_Read')
    BEGIN
        ALTER TABLE Guest_Chat ADD Guest_Read BIT NULL DEFAULT 0;
        PRINT 'Added Guest_Read column.';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Guest_Chat') AND name = 'Guest_Read_Date')
    BEGIN
        ALTER TABLE Guest_Chat ADD Guest_Read_Date DATETIME NULL;
        PRINT 'Added Guest_Read_Date column.';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Guest_Chat') AND name = 'Read_Date')
    BEGIN
        ALTER TABLE Guest_Chat ADD Read_Date DATETIME NULL;
        PRINT 'Added Read_Date column.';
    END
END
GO
