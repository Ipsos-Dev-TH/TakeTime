-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 32: รายได้ช่องทางอื่นนอก POS → NextAcc ครั้งเดียวต่อการขาย
-- ════════════════════════════════════════════════════════════════════════════
-- ใช้คู่กับนโยบายเอกสารขาย (PHASE19_29 Sales_Doc_Policy — SalesDocumentPolicy.Resolve)
--
-- 1) Sales_Channel_Posting — สมุดการขายรายช่องทาง (1 แถว = 1 การขายที่รับเงินแล้ว, UNIQUE Source_Key)
--      ACTIVITY          ACT-{bookingId}     กิจกรรมที่จ่ายเอง (โอน/เงินสด/ออนไลน์) — ROOM_CHARGE ไปกับบิลห้อง
--      AMENITY           AMN-{requestId}     เบิกของใช้ที่จ่ายแยกออนไลน์ (ไม่ได้รวมบิลห้อง)
--      FRONTDESK_GATEWAY FDG-{txnId}         เงินเกตเวย์จากหน้า Payment/Charge ที่ไม่มีการบันทึกขาย POS คู่กัน
--      DAMAGE            DMG-{checkoutId}    ค่าเสียหาย/ของหายตอนเช็คเอาท์ส่วนที่มัดจำ/เงินประกันไม่ครอบคลุม
--    Status: PENDING (รอส่ง) / POSTING (กำลังส่ง) / POSTED (เข้าคิวเอกสารแล้ว — Post_Ref = เลขเอกสาร)
--            OFF (นโยบายปิดเอกสาร) / MATCHED (เงินเกตเวย์จับคู่กับการขาย POS แล้ว — ไม่ออกซ้ำ)
--            NOPOLICY (นโยบายยังไม่เปิด — บันทึกไว้ตรวจย้อน ไม่ส่ง)
--    Doc_Mode IMMEDIATE = ส่งทันที (Post_Ref = {prefix}-{id}) / DAILY_ROLLUP = รวบรายวันหลังจบวัน
--    ({prefix}DAY-{yyyyMMdd}-{paidHowId}[-S{n}] — S = ใบเสริมของรายการที่ยืนยันช้า ไม่ทิ้งเงียบ)
--
-- 2) Guest_Room_Service_Orders.Paid_Date — เวลาที่ยืนยันรับเงิน (ปุ่ม "ยืนยันรับเงินแล้ว" / เกตเวย์)
--    ใบสรุปรูมเซอร์วิสจัดกลุ่มตามวันรับเงิน (จุดเกิดภาษี §78/1) — ว่าง = ใช้วันสั่ง (พฤติกรรมเดิม)
--
-- 3) mapping รายได้ใหม่ (ยังไม่ผูกรหัส — ไม่ผูก = ระบบถอยไปบัญชีเดิมเอง):
--      ACTIVITY_REVENUE → (SERVICE_REVENUE → OTHER_INCOME)   AMENITY_REVENUE → (PRODUCT_REVENUE)
--      SERVICE_CHARGE_REVENUE (มีจาก PHASE18_21 — seed ซ้ำแบบมีเงื่อนไข) → (PRODUCT_REVENUE)
--
-- 4) ค่าตั้ง: Nexaacc_ChannelSales_Since (วันเริ่มสแกนเงินเกตเวย์หน้าร้าน — กันโพสต์ย้อนหลังทั้งประวัติ),
--    Nexaacc_FrontdeskGateway_AutoRevenue (1), Nexaacc_FrontdeskGateway_GraceHours (24)
--
-- idempotent — รันซ้ำได้, ไม่ลบข้อมูล, ไม่ทับค่าที่ผู้ดูแลแก้แล้ว. ยังไม่รัน = โค้ดทำงานแบบเดิม
-- (ไม่มีตาราง → ไม่บันทึกช่องทางใหม่; รูมเซอร์วิสใช้วันสั่งแทนวันรับเงิน)
-- หลังรัน: recycle App Pool (หรือรอ 5 นาทีให้ cache ตรวจโครงสร้างหมดอายุ)
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
GO

-- 1) สมุดการขายรายช่องทาง
IF OBJECT_ID('dbo.Sales_Channel_Posting', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sales_Channel_Posting (
        ID              BIGINT IDENTITY(1,1) PRIMARY KEY,
        Sales_Channel   NVARCHAR(30)   NOT NULL,
        Source_Key      NVARCHAR(80)   NOT NULL,
        Reservation_ID  INT            NULL,
        Sale_Date       DATE           NOT NULL,
        Paid_How_ID     INT            NULL,
        Paid_How_Name   NVARCHAR(150)  NULL,
        Amount          DECIMAL(18,2)  NOT NULL,
        Revenue_Key     NVARCHAR(60)   NOT NULL,
        [Description]   NVARCHAR(300)  NULL,
        Customer_Phone  NVARCHAR(30)   NULL,
        Doc_Mode        NVARCHAR(20)   NULL,
        [Status]        NVARCHAR(20)   NOT NULL DEFAULT 'PENDING',
        Post_Ref        NVARCHAR(60)   NULL,
        Matched_Key     NVARCHAR(100)  NULL,
        Note            NVARCHAR(500)  NULL,
        Created_By      NVARCHAR(100)  NULL,
        Created_At      DATETIME       NOT NULL DEFAULT GETDATE(),
        Claimed_At      DATETIME       NULL,
        Posted_At       DATETIME       NULL,
        CONSTRAINT UQ_Sales_Channel_Posting_Source UNIQUE (Source_Key)
    );
    CREATE INDEX IX_Sales_Channel_Posting_Status ON dbo.Sales_Channel_Posting ([Status], Doc_Mode, Sale_Date);
    CREATE INDEX IX_Sales_Channel_Posting_Matched ON dbo.Sales_Channel_Posting (Matched_Key);
    PRINT N'สร้างตาราง Sales_Channel_Posting';
END
ELSE
    PRINT N'มีตาราง Sales_Channel_Posting อยู่แล้ว — ไม่แตะ';
GO

-- 2) วันที่รับเงินของออเดอร์รูมเซอร์วิส
IF OBJECT_ID('dbo.Guest_Room_Service_Orders', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Guest_Room_Service_Orders', 'Paid_Date') IS NULL
BEGIN
    ALTER TABLE dbo.Guest_Room_Service_Orders ADD Paid_Date DATETIME NULL;
    PRINT N'เพิ่ม Guest_Room_Service_Orders.Paid_Date';
END
ELSE
    PRINT N'Guest_Room_Service_Orders.Paid_Date มีอยู่แล้ว/ไม่มีตาราง — ข้าม';
GO

-- 3) mapping รายได้ (ยังไม่ผูกรหัสบัญชี — ระบบถอยไปบัญชีเดิมจนกว่าผู้ดูแลผูก)
IF OBJECT_ID('dbo.Accounting_Account_Mapping', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = 'ACTIVITY_REVENUE')
    BEGIN
        INSERT INTO dbo.Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
        VALUES ('ACTIVITY_REVENUE', N'รายได้ค่ากิจกรรม (ไม่ผูก = รายได้ค่าบริการอื่น/รายได้อื่น)', '', 'REVENUE', 1);
        PRINT N'เพิ่ม mapping ACTIVITY_REVENUE';
    END
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = 'AMENITY_REVENUE')
    BEGIN
        INSERT INTO dbo.Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
        VALUES ('AMENITY_REVENUE', N'รายได้ของใช้ในห้องพัก (เบิกของใช้ — ไม่ผูก = รายได้ขายสินค้า)', '', 'REVENUE', 1);
        PRINT N'เพิ่ม mapping AMENITY_REVENUE';
    END
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = 'SERVICE_CHARGE_REVENUE')
    BEGIN
        INSERT INTO dbo.Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
        VALUES ('SERVICE_CHARGE_REVENUE', N'รายได้ค่าบริการ (Service Charge)', '', 'REVENUE', 1);
        PRINT N'เพิ่ม mapping SERVICE_CHARGE_REVENUE';
    END
END
ELSE
    PRINT N'ไม่มีตาราง Accounting_Account_Mapping — ข้ามการเพิ่ม mapping';
GO

-- 4) ค่าตั้ง
IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_ChannelSales_Since')
    BEGIN
        INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue)
        VALUES ('Nexaacc_ChannelSales_Since', CONVERT(NVARCHAR(10), GETDATE(), 23));
        PRINT N'เพิ่มค่า Nexaacc_ChannelSales_Since = วันนี้ (เงินเกตเวย์หน้าร้านก่อนวันนี้ไม่ถูกโพสต์ย้อนหลัง)';
    END
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_FrontdeskGateway_AutoRevenue')
    BEGIN
        INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue)
        VALUES ('Nexaacc_FrontdeskGateway_AutoRevenue', '1');
        PRINT N'เพิ่มค่า Nexaacc_FrontdeskGateway_AutoRevenue = 1';
    END
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_FrontdeskGateway_GraceHours')
    BEGIN
        INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue)
        VALUES ('Nexaacc_FrontdeskGateway_GraceHours', '24');
        PRINT N'เพิ่มค่า Nexaacc_FrontdeskGateway_GraceHours = 24';
    END
END
ELSE
    PRINT N'ไม่มีตาราง Accounting_Integration_Config — ข้ามการเพิ่มค่าตั้ง';
GO

PRINT N'PHASE19_32 เสร็จ — ผูก mapping ACTIVITY_REVENUE / AMENITY_REVENUE / SERVICE_CHARGE_REVENUE ที่หน้า Accounting Integration (ไม่ผูกก็ใช้บัญชีเดิม)';
GO
