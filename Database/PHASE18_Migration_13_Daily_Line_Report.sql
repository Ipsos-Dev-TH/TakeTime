-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 13: Daily reservation board → LINE (พอร์ตจาก external HTMLToPicture)
-- ════════════════════════════════════════════════════════════════════════════
-- render หน้า DisplayToday เป็นรูป → push เข้า LINE รายวันอัตโนมัติ ในระบบเอง
-- (แทน console app ภายนอกที่ตั้ง scheduler แยก). token ใช้ของ LINE OA เดิม (OmniChannel_Channels)
-- หรือ override เฉพาะงานนี้ได้. idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_Enabled')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_Enabled', '0', N'ส่งรูปตารางจองรายวันเข้า LINE อัตโนมัติ (แทนโปรแกรมภายนอก)');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_Recipients')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_Recipients', '', N'ผู้รับ (LINE userId/groupId/roomId) — คั่นด้วย comma หรือขึ้นบรรทัดใหม่');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_SendTime')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_SendTime', '08:00', N'เวลาส่งรายวัน (HH:mm) — timer ส่งเมื่อถึงเวลานี้ วันละครั้ง');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_SourceUrl')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_SourceUrl', 'https://taketimebangphra.com/DailyBoard',
            N'URL ของหน้าที่จะ render เป็นรูป — แนะนำ /DailyBoard (ออกแบบมาเพื่อรูป LINE: สรุปยอด + เคยมาพักกี่ครั้ง + เข้า/ออกวันนี้ + ยอดค้าง)');

-- ระบบเดิมที่ชี้หน้า displaytoday อยู่ → ย้ายมาหน้าใหม่ที่อ่านง่ายกว่า (ผู้ใช้เปลี่ยนกลับเองได้ในหน้า Admin)
UPDATE Accounting_Integration_Config
   SET ConfigValue = REPLACE(REPLACE(ConfigValue, '/displaytoday', '/DailyBoard'), '/DisplayToday', '/DailyBoard')
 WHERE ConfigKey = 'Line_DailyReport_SourceUrl'
   AND ConfigValue LIKE '%isplay%oday%';
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_ImageWidth')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_ImageWidth', '1600', N'ความกว้างรูป (px)');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_ImageHeight')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_ImageHeight', '700', N'ความสูงรูปพื้นฐาน (px) — ถ้าเปิด AutoHeight จะขยายตามเนื้อหา');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_AutoHeight')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_AutoHeight', '1', N'ปรับความสูงรูปอัตโนมัติตามปริมาณเนื้อหา (0/1)');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_Caption')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_Caption', N'ตารางการจองวันที่ {date}', N'ข้อความประกอบ (แทนที่ {date} ด้วยวันที่ไทย) — เว้นว่าง = ส่งเฉพาะรูป');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_PublicBaseUrl')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_PublicBaseUrl', 'https://taketimebangphra.com/Images/Reservation', N'URL สาธารณะ(HTTPS)ของโฟลเดอร์รูป — LINE ต้องเข้าถึงได้');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_ImageFolder')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_ImageFolder', '~/Images/Reservation', N'โฟลเดอร์เก็บรูป (virtual path ~/... หรือ physical path) — ต้องตรงกับ PublicBaseUrl');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_TokenOverride_Encrypted')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_TokenOverride_Encrypted', '', N'LINE channel access token เฉพาะงานนี้ (encrypt) — เว้นว่าง = ใช้ token ของ LINE OA เดิม (OmniChannel)');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_LastSent')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_LastSent', '', N'วันที่ส่งล่าสุด (yyyyMMdd) — กันส่งซ้ำในวันเดียว (ระบบตั้งเอง)');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_JpegQuality')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_JpegQuality', '90', N'คุณภาพ JPEG 1-100 (สูง = คมชัด/ไฟล์ใหญ่)');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Line_DailyReport_FontScale')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Line_DailyReport_FontScale', '100',
            N'ขยายขนาดตัวอักษรในรูป (% — 100 = ตามหน้าเว็บเดิม, 150 = ใหญ่ขึ้น 1.5 เท่า) แก้ปัญหาตัวหนังสือเล็กเมื่อดูบนมือถือ');
GO

SELECT ConfigKey, ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey LIKE 'Line_DailyReport_%' ORDER BY ConfigKey;
GO
