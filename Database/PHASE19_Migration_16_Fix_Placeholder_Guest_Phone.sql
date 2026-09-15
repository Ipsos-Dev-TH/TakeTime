-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 16: แยกลูกค้า OTA ที่ถูกยุบรวมเพราะ "เบอร์โทรค่าคงที่"
-- ════════════════════════════════════════════════════════════════════════════
-- ปัญหา (พบ ก.ย. 2569):
--   STAAH/Expedia ส่งเบอร์โทรเป็นค่าคงที่ '01111111111111' มาทุกใบ แทนเบอร์ลูกค้าจริง
--   เบอร์โทรเป็น key ของตาราง Customer (Reservation.Customer_MobilePhone → Customer.MobilePhone)
--   ⇒ ใบจอง Expedia ทุกใบผูกกับลูกค้า "แถวเดียวกัน"
--   ⇒ ชื่อผู้จองที่แสดงกลายเป็นชื่อคนแรกที่เคยจองด้วยค่านี้ ตลอดไป
--      (โค้ดเดิม EnsureCustomer เจอแถวแล้ว return ทันที ไม่เคยแก้ชื่อ)
--   เคสจริง: ใบจอง 149419 ผู้เข้าพัก "Samput Ekapan" แต่หน้าเว็บขึ้น "Kanthicha Suparojwathin"
--
-- สิ่งที่สคริปต์นี้ทำ:
--   1) หาใบจองที่ Customer_MobilePhone "ไม่ใช่เบอร์ไทยจริง" (ยาวไม่ใช่ 9/10 หลัก หรือเลขซ้ำทั้งชุด)
--   2) สร้าง key ประจำใบจาก OTA_Booking_ID → 'OTA_' + เลขในวงเล็บ (หรือ 12 หลักท้าย)
--   3) สร้างแถว Customer ใหม่ด้วย "ชื่อผู้เข้าพักของใบนั้น" (OTA_Guest_Name)
--   4) ย้าย Reservation ไปผูก key ใหม่
--   ❗ ไม่ลบแถวลูกค้ารวมของเดิม และไม่แตะ Account_Receipt.Customer_ID
--      (ใบเสร็จที่ออกไปแล้ว = เอกสารบัญชี ต้องให้คนตัดสินใจเอง — สคริปต์รายงานจำนวนให้)
--
-- ปลอดภัย: รันซ้ำได้ (idempotent — ใบที่ย้ายแล้วขึ้นต้นด้วย 'OTA_' จะไม่เข้าเงื่อนไขอีก)
--          มี @DryRun ให้ดูก่อนว่าจะแก้อะไรบ้าง
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- ⚙️ ตั้ง 1 = ดูอย่างเดียวไม่แก้ / 0 = แก้จริง
DECLARE @DryRun BIT = 0;

IF COL_LENGTH('Reservation', 'OTA_Booking_ID') IS NULL
BEGIN
    PRINT N'ข้าม: ยังไม่มีคอลัมน์ Reservation.OTA_Booking_ID (รัน PHASE18_Migration_12 ก่อน)';
    RETURN;
END

-- ── 1) รวบรวมใบจองที่ต้องแก้ ────────────────────────────────────────────────
IF OBJECT_ID('tempdb..#Fix') IS NOT NULL DROP TABLE #Fix;

SELECT
    r.ID                AS ReservationID,
    r.Customer_MobilePhone AS OldPhone,
    LTRIM(RTRIM(ISNULL(r.OTA_Booking_ID, N''))) AS BookingId,
    LTRIM(RTRIM(ISNULL(r.OTA_Guest_Name, N''))) AS OtaGuestName,
    CAST(N'' AS NVARCHAR(30)) AS NewKey,
    CAST(N'' AS NVARCHAR(200)) AS NewName
INTO #Fix
FROM Reservation r
WHERE ISNULL(r.Customer_MobilePhone, N'') <> N''
  AND r.Customer_MobilePhone NOT LIKE N'OTA[_]%'          -- ย้ายไปแล้ว
  AND ISNULL(r.OTA_Booking_ID, N'') <> N''                -- ต้องมีเลขจองไว้ทำ key
  AND (
        -- ความยาวไม่ใช่เบอร์ไทย (เบอร์บ้าน 9 / มือถือ 10)
        LEN(r.Customer_MobilePhone) NOT IN (9, 10)
        -- หรือหลังหลักแรกเป็นเลขเดียวกันทั้งหมด (0111111111 / 0000000000 / 0999999999)
        OR (LEN(r.Customer_MobilePhone) > 6
            AND REPLACE(SUBSTRING(r.Customer_MobilePhone, 2, LEN(r.Customer_MobilePhone) - 1),
                        SUBSTRING(r.Customer_MobilePhone, 2, 1), N'') = N'')
      );

-- ── 2) สร้าง key ประจำใบจากเลขที่จอง ────────────────────────────────────────
-- เลขในวงเล็บก่อน: "1114600000001741 (2557213715)" → 2557213715
UPDATE #Fix
SET NewKey = N'OTA_' + SUBSTRING(BookingId,
                                 CHARINDEX(N'(', BookingId) + 1,
                                 CHARINDEX(N')', BookingId) - CHARINDEX(N'(', BookingId) - 1)
WHERE CHARINDEX(N'(', BookingId) > 0
  AND CHARINDEX(N')', BookingId) > CHARINDEX(N'(', BookingId) + 6
  AND SUBSTRING(BookingId,
                CHARINDEX(N'(', BookingId) + 1,
                CHARINDEX(N')', BookingId) - CHARINDEX(N'(', BookingId) - 1) NOT LIKE N'%[^0-9]%';

-- ไม่มีวงเล็บ → 12 หลักท้ายของท่อนแรก
UPDATE #Fix
SET NewKey = N'OTA_' + RIGHT(
        CASE WHEN CHARINDEX(N' ', BookingId) > 0
             THEN LEFT(BookingId, CHARINDEX(N' ', BookingId) - 1)
             ELSE BookingId END,
        12)
WHERE NewKey = N'';

-- ทิ้งอันที่ยังทำ key ไม่ได้ / key สั้นเกินจนเสี่ยงซ้ำ
DELETE FROM #Fix WHERE NewKey = N'' OR LEN(NewKey) < 10;

-- ── 3) ชื่อที่จะใช้: ชื่อจาก OTA ก่อน, ไม่มีก็ใช้ชื่อลูกค้าเดิม ────────────────
UPDATE f
SET NewName = CASE WHEN f.OtaGuestName <> N'' THEN f.OtaGuestName
                   ELSE ISNULL(c.Name, f.NewKey) END
FROM #Fix f
LEFT JOIN Customer c ON c.MobilePhone = f.OldPhone;

UPDATE #Fix SET NewName = NewKey WHERE ISNULL(NewName, N'') = N'';

-- ── รายงานก่อนแก้ ───────────────────────────────────────────────────────────
DECLARE @n INT = (SELECT COUNT(*) FROM #Fix);
PRINT N'ใบจองที่ใช้เบอร์ค่าคงที่/เบอร์ไม่ถูกรูปแบบ: ' + CAST(@n AS NVARCHAR(10)) + N' ใบ';

SELECT TOP 200
    ReservationID, OldPhone, BookingId, NewKey, NewName
FROM #Fix
ORDER BY ReservationID DESC;

-- ใบเสร็จที่ยังชี้ลูกค้าแถวรวม (ไม่แก้ให้ — แจ้งจำนวนไว้ตัดสินใจ)
IF OBJECT_ID('Account_Receipt') IS NOT NULL AND COL_LENGTH('Account_Receipt', 'Customer_ID') IS NOT NULL
BEGIN
    DECLARE @rc INT = (
        SELECT COUNT(*)
        FROM Account_Receipt ar
        JOIN Customer c ON c.ID = ar.Customer_ID
        WHERE c.MobilePhone IN (SELECT DISTINCT OldPhone FROM #Fix));
    IF @rc > 0
        PRINT N'⚠️ มีใบเสร็จ ' + CAST(@rc AS NVARCHAR(10))
            + N' ใบที่ยังผูกกับลูกค้าแถวรวมเดิม — สคริปต์นี้ไม่แก้ให้ (เป็นเอกสารบัญชีที่ออกไปแล้ว)';
END

IF @DryRun = 1
BEGIN
    PRINT N'DryRun = 1 → ยังไม่แก้อะไร ตั้งเป็น 0 แล้วรันใหม่เพื่อแก้จริง';
    RETURN;
END

-- ── 4) แก้จริง ──────────────────────────────────────────────────────────────
-- หนึ่ง key = หนึ่งแถวลูกค้า (กันกรณีมีใบจองมากกว่าหนึ่งใบที่ได้ key เดียวกัน)
IF OBJECT_ID('tempdb..#New') IS NOT NULL DROP TABLE #New;
SELECT NewKey,
       MAX(NewName)  AS NewName,
       MAX(OldPhone) AS OldPhone
INTO #New
FROM #Fix
GROUP BY NewKey;

BEGIN TRY
    BEGIN TRAN;

    -- 4.1 สร้างแถว Customer ของ key ใหม่ (เฉพาะที่ยังไม่มี) — คัดลอกคอลัมน์เท่าที่ตารางมีจริง
    DECLARE @cols NVARCHAR(MAX) = N'', @vals NVARCHAR(MAX) = N'';
    IF COL_LENGTH('Customer', 'FullName')  IS NOT NULL SET @cols = @cols + N', [FullName]',  @vals = @vals + N', f.NewName';
    IF COL_LENGTH('Customer', 'ComeFrom')  IS NOT NULL SET @cols = @cols + N', [ComeFrom]',  @vals = @vals + N', N''OTA''';
    IF COL_LENGTH('Customer', 'NickName')  IS NOT NULL SET @cols = @cols + N', [NickName]',  @vals = @vals + N', N''''';
    IF COL_LENGTH('Customer', 'Remark')    IS NOT NULL SET @cols = @cols + N', [Remark]',    @vals = @vals + N', N''แยกจากเบอร์ค่าคงที่ '' + f.OldPhone';
    IF COL_LENGTH('Customer', 'Address')   IS NOT NULL SET @cols = @cols + N', [Address]',   @vals = @vals + N', N''''';
    IF COL_LENGTH('Customer', 'Address1')  IS NOT NULL SET @cols = @cols + N', [Address1]',  @vals = @vals + N', N''''';
    IF COL_LENGTH('Customer', 'IDNumber')  IS NOT NULL SET @cols = @cols + N', [IDNumber]',  @vals = @vals + N', N''''';
    IF COL_LENGTH('Customer', 'Email')     IS NOT NULL SET @cols = @cols + N', [Email]',     @vals = @vals + N', N''''';
    IF COL_LENGTH('Customer', 'Branch_Number') IS NOT NULL SET @cols = @cols + N', [Branch_Number]', @vals = @vals + N', N''00000''';
    IF COL_LENGTH('Customer', 'Address_ID')    IS NOT NULL SET @cols = @cols + N', [Address_ID]',    @vals = @vals + N', 0';
    IF COL_LENGTH('Customer', 'Created_Date')  IS NOT NULL SET @cols = @cols + N', [Created_Date]',  @vals = @vals + N', GETDATE()';
    -- Customer_Type_ID: ใช้ค่าเดียวกับลูกค้าแถวรวมเดิม ถ้าไม่มีก็ค่าต่ำสุดที่มีอยู่ (สุดท้าย 1)
    IF COL_LENGTH('Customer', 'Customer_Type_ID') IS NOT NULL
        SET @cols = @cols + N', [Customer_Type_ID]',
            @vals = @vals + N', ISNULL((SELECT TOP 1 c2.Customer_Type_ID FROM Customer c2 WHERE c2.MobilePhone = f.OldPhone), '
                          + N'ISNULL((SELECT MIN(c3.Customer_Type_ID) FROM Customer c3), 1))';

    DECLARE @inserted INT = 0;
    DECLARE @sql NVARCHAR(MAX) = N'
        INSERT INTO Customer ([MobilePhone], [Name]' + @cols + N')
        SELECT f.NewKey, f.NewName' + @vals + N'
        FROM #New f
        WHERE NOT EXISTS (SELECT 1 FROM Customer c WHERE c.MobilePhone = f.NewKey);
        SET @out = @@ROWCOUNT;';
    EXEC sp_executesql @sql, N'@out INT OUTPUT', @out = @inserted OUTPUT;
    PRINT N'สร้างลูกค้าใหม่: ' + CAST(@inserted AS NVARCHAR(10)) + N' ราย';

    -- 4.2 ย้ายใบจองไปผูก key ใหม่
    UPDATE r
    SET r.Customer_MobilePhone = f.NewKey
    FROM Reservation r
    JOIN #Fix f ON f.ReservationID = r.ID;
    PRINT N'ย้ายใบจอง: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' ใบ';

    -- 4.3 ชื่อลูกค้าของ key ใหม่ต้องตรงกับชื่อผู้เข้าพักของใบนั้นเสมอ
    UPDATE c
    SET c.Name = f.NewName
    FROM Customer c
    JOIN #New f ON f.NewKey = c.MobilePhone
    WHERE ISNULL(c.Name, N'') <> f.NewName;
    PRINT N'ปรับชื่อลูกค้าให้ตรงใบจอง: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' ราย';

    COMMIT;
    PRINT N'✅ เสร็จสิ้น';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT N'❌ ล้มเหลว: ' + ERROR_MESSAGE();
    THROW;
END CATCH

-- ── 5) บันทึกค่าคงที่ที่เจอไว้ในตั้งค่า เผื่อ OTA เปลี่ยนค่าใหม่จะได้เทียบได้ ──
IF OBJECT_ID('Accounting_Integration_Config') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_DefaultPhone')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_DefaultPhone', '',
            N'เบอร์สำรองเมื่ออีเมลไม่มีเบอร์และไม่มีเลขที่จอง (ปกติเว้นว่าง — ระบบใช้ OTA_<เลขจอง> แทน)');
GO
