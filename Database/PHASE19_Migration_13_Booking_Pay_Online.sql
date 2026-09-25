-- ============================================================================
-- PHASE19 Migration 13 — ลูกค้าจองเองแล้วจ่ายด้วยบัตร/QR ทันที
--
-- เดิมหน้าจองบังคับโอนเงิน + แนบสลิปเสมอ ใบจองสร้างด้วยสถานะ "มัดจำแล้ว" ตายตัว
-- ไม่มีสถานะ "ยังไม่จ่าย" ⇒ จ่ายออนไลน์ไม่ได้ เพราะตัวรับเงินต้องมีใบจองอยู่ก่อน
-- แต่ใบจองสร้างไม่ได้ถ้ายังไม่จ่าย (ไก่กับไข่)
--
-- ไฟล์นี้เพิ่ม "ค่าตั้ง" อย่างเดียว ไม่แตะ schema และไม่แตะข้อมูลเดิม
-- สถานะใหม่ 'รอชำระเงิน' เก็บในคอลัมน์ Reservation.Status เดิม (NVARCHAR อยู่แล้ว)
--
-- ⚠ ปิดสวิตช์ไว้เป็นค่าเริ่มต้น — ยังไม่เปิด = หน้าจองทำงานเหมือนเดิมทุกประการ
-- รันซ้ำได้
-- ============================================================================

SET NOCOUNT ON;

IF OBJECT_ID('dbo.Payment_Gateway_Config', 'U') IS NULL
BEGIN
    PRINT N'⚠ ยังไม่มีตาราง Payment_Gateway_Config — รัน PHASE19_Migration_05 ก่อน';
    RETURN;
END
GO

DECLARE @cfg TABLE (K NVARCHAR(80), V NVARCHAR(MAX), S BIT, Cat NVARCHAR(60),
                    DN NVARCHAR(200), D NVARCHAR(1000), IT NVARCHAR(20), Opt NVARCHAR(500), Ord INT);

INSERT INTO @cfg (K, V, S, Cat, DN, D, IT, Opt, Ord) VALUES
('Payment_Booking_PayOnline', '0', 0, N'ทั่วไป',
 N'ให้ลูกค้าจองแล้วจ่ายด้วยบัตร/QR ทันที',
 N'เปิดแล้วหน้าจองจะมีตัวเลือก "จ่ายทันที" ไม่ต้องโอนและแนบสลิป — ใบจองถูกบันทึกเป็น '
 + N'"รอชำระเงิน" กันห้องไว้ก่อน จ่ายสำเร็จจึงเลื่อนเป็น "มัดจำแล้ว" อัตโนมัติ '
 + N'(ต้องเปิดเกตเวย์และวิธีจ่ายด้วยบัตร/QR ไว้ด้วย)',
 'bool', NULL, 70),
('Payment_Booking_Hold_Minutes', '60', 0, N'ทั่วไป',
 N'กันห้องให้กี่นาทีระหว่างรอลูกค้าชำระ',
 N'เลยเวลานี้แล้วใบจองที่ยังไม่มีเงินเข้าจะถูกยกเลิกอัตโนมัติ ห้องกลับมาว่างให้คนอื่นจอง '
 + N'(ระบบตรวจทุก 5 นาที · ยกเลิกเฉพาะใบที่ไม่มีเงินเข้าจริงสักบาท)',
 'number', NULL, 71);

INSERT INTO dbo.Payment_Gateway_Config
    (Config_Key, Config_Value, Is_Secret, Category, Display_Name, [Description], Input_Type, Options, Display_Order)
SELECT c.K, c.V, c.S, c.Cat, c.DN, c.D, c.IT, c.Opt, c.Ord
FROM @cfg c
WHERE NOT EXISTS (SELECT 1 FROM dbo.Payment_Gateway_Config p WHERE p.Config_Key = c.K);

PRINT N'เพิ่มค่าตั้งใหม่: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' คีย์';

-- อัปเดตคำอธิบายของคีย์ที่มีอยู่แล้ว (ไม่แตะค่าที่ผู้ใช้ตั้งไว้)
UPDATE p
   SET p.Category = c.Cat, p.Display_Name = c.DN, p.[Description] = c.D,
       p.Input_Type = c.IT, p.Options = c.Opt, p.Display_Order = c.Ord
FROM dbo.Payment_Gateway_Config p
JOIN @cfg c ON c.K = p.Config_Key;
GO

-- ── ดัชนีช่วยตัวกวาดใบจองที่ไม่ชำระ (ค้นด้วย Status + Created_Date) ─────────
IF OBJECT_ID('dbo.Reservation', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_Reservation_Status_Created'
                     AND object_id = OBJECT_ID('dbo.Reservation'))
BEGIN
    CREATE INDEX IX_Reservation_Status_Created
        ON dbo.Reservation ([Status], Created_Date) INCLUDE (Deposit);
    PRINT N'สร้างดัชนี IX_Reservation_Status_Created';
END
ELSE PRINT N'มีดัชนี IX_Reservation_Status_Created อยู่แล้ว หรือไม่มีตาราง Reservation — ข้าม';
GO

SELECT Config_Key AS [คีย์], Config_Value AS [ค่า]
  FROM dbo.Payment_Gateway_Config
 WHERE Config_Key IN ('Payment_Booking_PayOnline','Payment_Booking_Hold_Minutes',
                      'Payment_Enabled','Payment_Methods_Enabled')
 ORDER BY Config_Key;

PRINT '';
PRINT N'--- ใบจองที่ค้างสถานะ "รอชำระเงิน" ตอนนี้ ---';
SELECT COUNT(*) AS [จำนวน] FROM dbo.Reservation WHERE [Status] = N'รอชำระเงิน';

PRINT '';
PRINT N'ลำดับเปิดใช้: เปิด Payment_Enabled + ใส่กุญแจเกตเวย์ + เปิดวิธี CARD/QR';
PRINT N'              → แล้วค่อยเปิด Payment_Booking_PayOnline';
PRINT N'ยังไม่เปิด = หน้าจองทำงานเหมือนเดิมทุกประการ (โอน + แนบสลิป)';
GO
