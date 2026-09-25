-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 17: คำขอผูกบัญชี LINE (เลือกชื่อตัวเอง → ผู้ดูแลอนุมัติ)
-- ════════════════════════════════════════════════════════════════════════════
-- ให้พนักงานที่ล็อกอิน LINE แล้ว "เลือกชื่อตัวเอง" เพื่อผูกบัญชีได้ทันทีโดยไม่ต้อง
-- เข้าระบบด้วยรหัสผ่านก่อน — แต่ต้องผ่านการยืนยันอย่างใดอย่างหนึ่ง:
--   (1) ยืนยันด้วยรหัสผ่านของตัวเอง → ผูกทันที
--   (2) ส่งคำขอให้ผู้ดูแล (Owner) กดอนุมัติ → เหมาะกับคนที่จำรหัสไม่ได้
-- ⚠️ ห้ามให้เลือกชื่อแล้วผูกทันทีโดยไม่ยืนยัน เพราะใครก็ตามที่มี LINE จะสวมสิทธิ์
--    บัญชี Owner ได้ (ยึดระบบทั้งหมด)
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

IF OBJECT_ID('dbo.Admin_Line_Link_Requests', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Admin_Line_Link_Requests] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL,              -- บัญชีที่ผู้ขอเลือกว่าเป็นตัวเอง
        [Line_UserId] NVARCHAR(64) NOT NULL,
        [Line_DisplayName] NVARCHAR(200) NULL,
        [Line_PictureUrl] NVARCHAR(500) NULL,
        [Status] VARCHAR(20) NOT NULL DEFAULT 'PENDING',   -- PENDING / APPROVED / REJECTED
        [RequestedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [DecidedBy_AdminID] SMALLINT NULL,
        [DecidedDate] DATETIME NULL,
        [RejectReason] NVARCHAR(300) NULL,
        CONSTRAINT FK_LineLinkReq_Admin FOREIGN KEY (Admin_ID) REFERENCES [dbo].[Admin](ID),
        CONSTRAINT CK_LineLinkReq_Status CHECK (Status IN ('PENDING','APPROVED','REJECTED'))
    );
    CREATE INDEX IX_LineLinkReq_Status ON [dbo].[Admin_Line_Link_Requests](Status, RequestedDate DESC);
    CREATE INDEX IX_LineLinkReq_Line ON [dbo].[Admin_Line_Link_Requests](Line_UserId);
    PRINT 'Created Admin_Line_Link_Requests';
END
GO

SELECT TOP 20 * FROM [dbo].[Admin_Line_Link_Requests] ORDER BY ID DESC;
GO
