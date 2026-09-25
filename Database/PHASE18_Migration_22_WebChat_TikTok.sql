-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 22: แชทหน้าเว็บ (Web Chat) + ช่องทาง TikTok
-- ════════════════════════════════════════════════════════════════════════════
-- 1) เพิ่มช่องทาง WEBCHAT (แชทลอยหน้าเว็บสาธารณะ) และ TIKTOK ใน OmniChannel
-- 2) เพิ่มคอลัมน์ Slip_Path ให้ AI_Booking_Actions — เก็บสลิปที่ลูกค้าแนบตอนจองผ่านแชท
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- ── ช่องทางใหม่ ───────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.OmniChannel_Channels', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM OmniChannel_Channels WHERE ChannelCode = 'WEBCHAT')
        INSERT INTO OmniChannel_Channels (ChannelCode, ChannelName, ChannelType, IconClass, BrandColor, IsEnabled)
        VALUES ('WEBCHAT', N'แชทหน้าเว็บ', 'WEB', 'fas fa-comment-dots', '#5D4037', 1);

    IF NOT EXISTS (SELECT 1 FROM OmniChannel_Channels WHERE ChannelCode = 'TIKTOK')
        INSERT INTO OmniChannel_Channels (ChannelCode, ChannelName, ChannelType, IconClass, BrandColor, IsEnabled)
        VALUES ('TIKTOK', N'TikTok', 'SOCIAL', 'fab fa-tiktok', '#000000', 0);

    PRINT 'Seeded WEBCHAT + TIKTOK channels';
END
GO

-- ── สลิปการจองผ่านแชท ─────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.AI_Booking_Actions', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.AI_Booking_Actions') AND name = 'Slip_Path')
BEGIN
    ALTER TABLE dbo.AI_Booking_Actions ADD Slip_Path NVARCHAR(500) NULL;
    PRINT 'Added AI_Booking_Actions.Slip_Path';
END
GO

PRINT 'PHASE18_22 completed';
GO
