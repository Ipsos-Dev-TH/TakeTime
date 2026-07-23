-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 12: Email reservation intake (STAAH) — OTA gross/net + config
-- ════════════════════════════════════════════════════════════════════════════
-- Design: docs/Email_Reservation_Intake_Design.md
-- เก็บ "ราคาขายจริงที่ลูกค้าจ่าย OTA (gross/refsell_amt)" แยกจาก "ยอดที่ OTA จะโอน (net)"
-- → ฐานรายได้/VAT/ลูกหนี้ OTA ถูก (settlement) + เช็คอินไม่ตีความ net เป็นมัดจำผิด (เคส 148824)
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- 1) คอลัมน์ Reservation (OTA_Channel / OTA_Booking_ID มีอยู่แล้วจาก Channel Manager — กันไว้ด้วย IF)
IF COL_LENGTH('Reservation', 'OTA_Channel') IS NULL
    ALTER TABLE Reservation ADD OTA_Channel NVARCHAR(50) NULL;
IF COL_LENGTH('Reservation', 'OTA_Booking_ID') IS NULL
    ALTER TABLE Reservation ADD OTA_Booking_ID NVARCHAR(100) NULL;
IF COL_LENGTH('Reservation', 'OTA_Gross_Amount') IS NULL
    ALTER TABLE Reservation ADD OTA_Gross_Amount DECIMAL(18,2) NULL;   -- refsell_amt: ราคาที่ลูกค้าจ่าย OTA
IF COL_LENGTH('Reservation', 'OTA_Net_Amount') IS NULL
    ALTER TABLE Reservation ADD OTA_Net_Amount DECIMAL(18,2) NULL;     -- AMOUNT: ยอดคาดว่า OTA โอน (หลังหักคอม)
IF COL_LENGTH('Reservation', 'OTA_Payment_Type') IS NULL
    ALTER TABLE Reservation ADD OTA_Payment_Type NVARCHAR(30) NULL;    -- ChannelCollect / HotelCollect
GO

-- 2) Config keys ของ email intake (หน้า Admin แก้ได้; password encrypt ด้วย code.Crypt)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_Enabled')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_Enabled', '0', N'อ่านอีเมลจอง STAAH ในระบบเอง (แทน external GetReservationfromGmail) — เปิดหลัง deploy service + ตั้งค่า IMAP ครบ');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_ImapServer')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_ImapServer', 'imap.gmail.com', N'IMAP server สำหรับอ่านอีเมลจอง');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_ImapPort')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_ImapPort', '993', N'IMAP port (SSL)');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_Username')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_Username', '', N'Gmail address ที่รับอีเมล STAAH');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_Password_Encrypted')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_Password_Encrypted', '', N'Gmail app password (เข้ารหัสด้วย code.Crypt — ตั้งผ่านหน้า Admin เท่านั้น)');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_PollMinutes')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_PollMinutes', '5', N'ความถี่ดึงอีเมล (นาที)');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_ProcessedLabel')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_ProcessedLabel', 'STAAH-Processed', N'label/folder อีเมลที่ประมวลผลแล้ว');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_FailedLabel')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_FailedLabel', 'STAAH-Failed', N'label/folder อีเมลที่ประมวลผลไม่สำเร็จ');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_MaxStayDays')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_MaxStayDays', '30', N'validation: จำนวนคืนสูงสุดที่ยอมรับ');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_MaxDaysFuture')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_MaxDaysFuture', '365', N'validation: จองล่วงหน้าสูงสุด (วัน)');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_NotifyTelegram')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_NotifyTelegram', '1', N'แจ้ง Telegram เมื่อลงจอง/ล้มเหลว (ใช้ token เดิมของระบบ)');
GO

SELECT ConfigKey, ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey LIKE 'Email_Rsv_%' ORDER BY ConfigKey;
GO
