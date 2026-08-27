-- ============================================================================
-- PHASE19 Migration 08 — ขยายความยาวคอลัมน์ Reservation.Remark
-- ============================================================================
-- อาการ: ลงจองจากอีเมล OTA ไม่สำเร็จ
--        "String or binary data would be truncated. The statement has been terminated."
--
-- สาเหตุ: หมายเหตุการจองถูกทำให้ละเอียดขึ้น (แผนราคา / อาหารเช้า / จำนวนผู้เข้าพัก /
--        คำขอพิเศษจาก OTA) แล้วยาวเกินความกว้างของคอลัมน์ Remark ที่มีอยู่
--
-- ⚠ ฝั่งโค้ดแก้ให้ปลอดภัยแล้ว: อ่านความกว้างจริงของคอลัมน์แล้วตัดให้พอดีเสมอ
--   (บรรทัด "Booking ID:" และบันทึกของเจ้าหน้าที่ถูกกันที่ไว้ก่อน — ไม่มีทางหาย)
--   ⇒ ไม่รันไฟล์นี้ก็ลงจองได้ปกติ เพียงแต่บางบรรทัดจะถูกตัดออกเมื่อที่ไม่พอ
--
-- ไฟล์นี้ทำให้ "ข้อมูลครบไม่ต้องตัด" — ขยายเป็น NVARCHAR(1000)
--
-- ปลอดภัย: การขยาย NVARCHAR ให้กว้างขึ้นเป็นการแก้ metadata อย่างเดียว
--          ไม่เขียนข้อมูลใหม่ ไม่ทำข้อมูลเดิมหาย และรันซ้ำได้
--          สคริปต์จะ "ไม่แตะ" ถ้าคอลัมน์ถูกใช้เป็นคีย์ของ index (กันเกินขนาดคีย์ 900 ไบต์)
-- ============================================================================

SET NOCOUNT ON;

DECLARE @type   NVARCHAR(50);
DECLARE @len    INT;
DECLARE @idxCnt INT = 0;

SELECT @type = DATA_TYPE, @len = CHARACTER_MAXIMUM_LENGTH
  FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_NAME = 'Reservation' AND COLUMN_NAME = 'Remark';

IF @type IS NULL
BEGIN
    PRINT N'ไม่พบคอลัมน์ Reservation.Remark — ข้ามไมเกรชันนี้';
    RETURN;
END

PRINT N'ตอนนี้ Reservation.Remark เป็น ' + @type
      + CASE WHEN @len IS NULL THEN N''
             WHEN @len < 0 THEN N'(max)'
             ELSE N'(' + CAST(@len AS NVARCHAR(10)) + N')' END;

IF @type IN ('ntext', 'text')
BEGIN
    PRINT N'เป็นชนิดข้อความยาวอยู่แล้ว — ไม่ต้องขยาย';
    RETURN;
END

IF @type NOT IN ('nvarchar', 'varchar', 'nchar', 'char')
BEGIN
    PRINT N'ชนิดข้อมูลไม่ใช่ข้อความ — ไม่แตะ';
    RETURN;
END

IF @len < 0 OR @len >= 1000
BEGIN
    PRINT N'กว้างพอแล้ว — ไม่ต้องขยาย';
    RETURN;
END

-- คอลัมน์ถูกใช้เป็นคีย์ของ index หรือเปล่า (ถ้าใช่ ขยายแล้วอาจเกินขีดจำกัดคีย์)
SELECT @idxCnt = COUNT(*)
  FROM sys.index_columns ic
  JOIN sys.columns c  ON c.object_id = ic.object_id AND c.column_id = ic.column_id
 WHERE ic.object_id = OBJECT_ID('dbo.Reservation')
   AND c.name = 'Remark'
   AND ic.is_included_column = 0;

IF @idxCnt > 0
BEGIN
    PRINT N'⚠ คอลัมน์นี้เป็นคีย์ของ index อยู่ ' + CAST(@idxCnt AS NVARCHAR(10))
          + N' รายการ — ไม่ขยายให้อัตโนมัติ (ระบบจะตัดข้อความให้พอดีเองอยู่แล้ว)';
    RETURN;
END

ALTER TABLE dbo.Reservation ALTER COLUMN [Remark] NVARCHAR(1000) NULL;
PRINT N'ขยาย Reservation.Remark เป็น NVARCHAR(1000) เรียบร้อย';
GO

-- ── ตรวจผล ─────────────────────────────────────────────────────────────────
SELECT DATA_TYPE AS [ชนิด],
       CASE WHEN CHARACTER_MAXIMUM_LENGTH < 0 THEN N'ไม่จำกัด'
            ELSE CAST(CHARACTER_MAXIMUM_LENGTH AS NVARCHAR(10)) END AS [ความยาวสูงสุด]
  FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_NAME = 'Reservation' AND COLUMN_NAME = 'Remark';

PRINT '';
PRINT N'หมายเหตุ: ต้องรีสตาร์ท App Pool หรือรอสักครู่ ระบบจึงอ่านความกว้างใหม่ (จำค่าไว้ตอนเริ่มทำงาน)';
