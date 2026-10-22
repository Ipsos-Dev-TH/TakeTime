-- ============================================================================
-- PHASE19 Migration 10 — ยอดที่ลงระบบจากอีเมล OTA ใช้ AMOUNT ไม่ใช่ refsell_amt
-- ============================================================================
-- ปัญหา: อีเมล STAAH ให้ตัวเลขมา 2 ตัว
--     refsell_amt = ยอดที่ "ลูกค้าจ่าย OTA" (รวมค่าคอม/ส่วนที่ OTA บวกเอง)
--     AMOUNT      = เรตต่อคืนต่อห้อง = "เงินที่รีสอร์ทได้รับจริง"
--   ระบบเดิมเอา refsell_amt ไปลงเป็น TotalPrice และ Deposit
--   ⇒ รายได้และยอดมัดจำสูงเกินจริงทุกใบ
--   เคสจริง: Booking 1114600000001705 — refsell 1,325 แต่ AMOUNT จริง 886 (ต่าง 440)
--
-- แก้แล้วในโค้ด: ยอดที่ลงระบบ = ผลรวม AMOUNT รายห้อง × จำนวนห้อง × จำนวนคืน
--   refsell_amt ยังถูกเก็บไว้ที่คอลัมน์ OTA_Gross_Amount สำหรับกระทบยอดกับ OTA
--
-- ไฟล์นี้เพิ่ม "สวิตช์" ให้สลับกลับได้ถ้าผู้ทำบัญชีต้องการ + สคริปต์ตรวจใบเก่า
-- ปลอดภัย: รันซ้ำได้ · ไม่แก้ข้อมูลการจองใด ๆ (ส่วนแก้ย้อนหลังเป็นแบบรันเองเท่านั้น)
-- ============================================================================

SET NOCOUNT ON;

IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NULL
BEGIN
    PRINT N'ยังไม่มีตาราง Accounting_Integration_Config — ข้ามไมเกรชันนี้';
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_TotalSource')
BEGIN
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue)
    VALUES ('Email_Rsv_TotalSource', 'AMOUNT');
    PRINT N'เพิ่มค่า Email_Rsv_TotalSource = AMOUNT (ยอดที่รีสอร์ทได้จริง)';
END
ELSE PRINT N'มีค่า Email_Rsv_TotalSource อยู่แล้ว — ไม่แตะค่าที่ตั้งไว้';
GO

-- ── ตรวจใบเก่าที่ลงด้วย refsell (ยอดสูงเกินจริง) ────────────────────────────
-- อ่านอย่างเดียว ไม่แก้อะไร — ใช้ตัดสินใจว่าจะแก้ย้อนหลังใบไหนบ้าง
IF COL_LENGTH('dbo.Reservation', 'OTA_Gross_Amount') IS NOT NULL
   AND COL_LENGTH('dbo.Reservation', 'OTA_Net_Amount') IS NOT NULL
BEGIN
    PRINT '';
    PRINT N'--- ใบจอง OTA ที่ TotalPrice ยังเป็นยอด refsell (ต่างจาก AMOUNT) ---';

    SELECT TOP 200
           r.ID              AS [เลขที่จอง],
           r.OTA_Booking_ID  AS [Booking ID],
           r.CheckinDate     AS [เช็คอิน],
           r.TotalPrice      AS [ยอดที่ลงไว้],
           r.OTA_Net_Amount  AS [ยอดที่ควรเป็น_AMOUNT],
           r.OTA_Gross_Amount AS [ลูกค้าจ่าย_OTA],
           r.TotalPrice - r.OTA_Net_Amount AS [ส่วนต่าง],
           r.Deposit         AS [มัดจำที่ลงไว้],
           r.Status          AS [สถานะ]
      FROM dbo.Reservation r
     WHERE r.OTA_Net_Amount IS NOT NULL
       AND r.OTA_Net_Amount > 0
       AND ABS(ISNULL(r.TotalPrice, 0) - r.OTA_Net_Amount) > 1
       AND r.CheckinDate >= DATEADD(MONTH, -6, GETDATE())
     ORDER BY r.ID DESC;

    PRINT '';
    PRINT N'⚠ วิธีแก้ย้อนหลัง — อ่านให้ครบก่อนทำ:';
    PRINT N'   1. ใบที่ยัง "ไม่เช็คอิน / ไม่มีใบเสร็จ" แก้ได้ปลอดภัยที่สุด';
    PRINT N'   2. ใบที่ออกใบเสร็จ/ส่ง NextAcc ไปแล้ว ห้ามแก้ตรงในฐาน —';
    PRINT N'      ต้องแก้เอกสารฝั่ง NextAcc ให้สอดคล้องด้วย (ปรึกษาผู้ทำบัญชีก่อน)';
    PRINT N'   3. คำสั่งด้านล่างถูกคอมเมนต์ไว้ตั้งใจ — เอาคอมเมนต์ออกเองเมื่อตัดสินใจแล้ว';
    PRINT '';
    PRINT N'   -- UPDATE r SET r.TotalPrice = r.OTA_Net_Amount,';
    PRINT N'   --        r.Deposit = CASE WHEN r.Deposit > 0 THEN r.OTA_Net_Amount ELSE 0 END';
    PRINT N'   --   FROM dbo.Reservation r';
    PRINT N'   --  WHERE r.ID IN (/* ใส่เลขที่จองที่ตรวจแล้วว่าแก้ได้ */);';
END
ELSE PRINT N'ตารางนี้ยังไม่มีคอลัมน์ OTA_Net_Amount — ข้ามส่วนตรวจใบเก่า';

PRINT '';
PRINT N'ใบจองใหม่ตั้งแต่ deploy จะลงยอด AMOUNT ให้เองอัตโนมัติ';
PRINT N'สลับกลับไปใช้ refsell: ตั้ง Email_Rsv_TotalSource = REFSELL';
