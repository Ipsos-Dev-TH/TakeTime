-- ============================================================
-- PHASE13 Migration 03: Redemption QR Tokens
-- ============================================================
-- Supports staff-driven reward redemption via QR scan:
--   1) Customer taps "use points" -> a short-lived single-use
--      token is created and shown as a QR code.
--   2) Staff selects a reward and scans the customer's QR.
--      The token resolves to the customer; staff confirms and
--      the reward is redeemed (token marked used).
-- ============================================================

USE TakeTime;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Loyalty_Redemption_Tokens]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Loyalty_Redemption_Tokens] (
        [Token] VARCHAR(40) NOT NULL PRIMARY KEY,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [ExpiryDate] DATETIME NOT NULL,
        [Used] BIT DEFAULT 0,
        [UsedDate] DATETIME NULL,
        [UsedBy_AdminID] SMALLINT NULL,

        CONSTRAINT [FK_RedemptionTokens_Customer] FOREIGN KEY ([Customer_MobilePhone])
            REFERENCES [dbo].[Customer]([MobilePhone]) ON UPDATE CASCADE,
        CONSTRAINT [FK_RedemptionTokens_Admin] FOREIGN KEY ([UsedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_RedemptionTokens_Customer] ON [dbo].[Loyalty_Redemption_Tokens]([Customer_MobilePhone]);
    CREATE INDEX [IDX_RedemptionTokens_Expiry] ON [dbo].[Loyalty_Redemption_Tokens]([ExpiryDate]);

    PRINT 'Created Loyalty_Redemption_Tokens table';
END
GO
