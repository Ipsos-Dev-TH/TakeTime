-- ════════════════════════════════════════════════════════════════════════════
-- PHASE19_Migration_44 — Mapping ห้อง OTA: ช่องติ๊กอาหารเช้า + จัดห้องตาม mapping แบบเข้ม (พ.ย. 2026)
--
-- เคส: Agoda "Nordic Tent - RNB - Nordic Tent - Double - Room no breakfast" ได้ "ห้องกระจก3(ชั้นล่าง)"
--   • ตาราง MapDataWithSTAAH เก็บแค่เลข Accommodation_ID — แก้ผ่านหน้าจัดการฐานข้อมูลแล้วมองไม่เห็นว่าเลขไหนคือห้องอะไร
--   • ตัวอ่านอีเมลจับคู่แบบหลวม (ชื่อสั้น / 15 ตัวแรก / ข้ามช่องทาง) ⇒ "เชื่อชื่อห้องจาก OTA"
--   • อาหารเช้ารู้จากข้อความในคอลัมน์ Remark เท่านั้น ("Agoda ไม่รวมอาหารเช้า")
-- แก้: คอลัมน์ Breakfast (INCLUDED / EXCLUDED / NULL = ไม่ระบุ) + ค่า Email_Rsv_MapStrict = 1
--      + หน้าใหม่ Admin/Settings/OtaRoomMapping (แสดงชื่อห้อง/ประเภท, ติ๊กห้อง, ติ๊กอาหารเช้า, ชื่อจาก OTA ที่ยังไม่ map)
-- idempotent — รันซ้ำได้. ไม่แก้ห้องที่ผูกไว้ (Accommodation_ID) ให้เอง — ดูรายงานท้ายไฟล์แล้วแก้ที่หน้าใหม่
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.MapDataWithSTAAH', 'U') IS NULL
BEGIN
    PRINT 'ไม่มีตาราง MapDataWithSTAAH — ข้าม';
END
ELSE IF COL_LENGTH('dbo.MapDataWithSTAAH', 'Breakfast') IS NULL
BEGIN
    ALTER TABLE dbo.MapDataWithSTAAH ADD Breakfast NVARCHAR(10) NULL;
    PRINT 'เพิ่มคอลัมน์ MapDataWithSTAAH.Breakfast';
END
GO

IF OBJECT_ID('dbo.MapDataWithSTAAH', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.MapDataWithSTAAH', 'Breakfast') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_MapDataWithSTAAH_Breakfast')
    EXEC('ALTER TABLE dbo.MapDataWithSTAAH ADD CONSTRAINT CK_MapDataWithSTAAH_Breakfast
          CHECK (Breakfast IS NULL OR Breakfast IN (N''INCLUDED'', N''EXCLUDED''))');
GO

-- ── ย้ายข้อมูลเดิม: อ่านจากคอลัมน์หมายเหตุที่เจ้าหน้าที่พิมพ์ไว้ (ไม่อ่านชื่อแผนราคาจาก OTA) ─────────
-- คำปฏิเสธก่อนเสมอ ("ไม่รวมอาหารเช้า" มีคำว่า "รวมอาหารเช้า" อยู่ข้างใน) — กติกาเดียวกับ OtaRoomMap.BreakfastFromText
IF OBJECT_ID('dbo.MapDataWithSTAAH', 'U') IS NOT NULL AND COL_LENGTH('dbo.MapDataWithSTAAH', 'Breakfast') IS NOT NULL
BEGIN
    DECLARE @noteCol SYSNAME = (SELECT TOP 1 c.name FROM sys.columns c
                                 WHERE c.object_id = OBJECT_ID('dbo.MapDataWithSTAAH')
                                   AND c.name IN (N'Remark', N'Remarks', N'Note', N'Notes', N'Comment', N'Description', N'Detail', N'หมายเหตุ')
                                 ORDER BY CASE c.name WHEN N'Remark' THEN 0 WHEN N'Remarks' THEN 1 ELSE 2 END);
    IF @noteCol IS NULL
        PRINT 'ไม่มีคอลัมน์หมายเหตุ — ไม่มีข้อมูลอาหารเช้าเดิมให้ย้าย (ติ๊กเองที่หน้า Mapping ห้อง OTA)';
    ELSE
    BEGIN
        DECLARE @n NVARCHAR(300) = N'LOWER(CONVERT(NVARCHAR(400), ' + QUOTENAME(@noteCol) + N'))';
        DECLARE @sql NVARCHAR(MAX) = N'
            UPDATE dbo.MapDataWithSTAAH SET Breakfast = N''EXCLUDED''
             WHERE Breakfast IS NULL AND (
                   ' + @n + N' LIKE N''%ไม่รวมอาหารเช้า%'' OR ' + @n + N' LIKE N''%ไม่มีอาหารเช้า%''
                OR ' + @n + N' LIKE N''%ไม่รวมอาหาร%''    OR ' + @n + N' LIKE N''%งดอาหารเช้า%''
                OR ' + @n + N' LIKE N''%no breakfast%''   OR ' + @n + N' LIKE N''%without breakfast%''
                OR ' + @n + N' LIKE N''%breakfast not included%'' OR ' + @n + N' LIKE N''%breakfast excluded%''
                OR ' + @n + N' LIKE N''%room only%''      OR ' + @n + N' LIKE N''%roomonly%'');
            PRINT ''ไม่รวมอาหารเช้า (จากหมายเหตุ): '' + CONVERT(VARCHAR(10), @@ROWCOUNT) + '' แถว'';
            UPDATE dbo.MapDataWithSTAAH SET Breakfast = N''INCLUDED''
             WHERE Breakfast IS NULL AND (
                   ' + @n + N' LIKE N''%รวมอาหารเช้า%''   OR ' + @n + N' LIKE N''%มีอาหารเช้า%''
                OR ' + @n + N' LIKE N''%พร้อมอาหารเช้า%'' OR ' + @n + N' LIKE N''%with breakfast%''
                OR ' + @n + N' LIKE N''%breakfast included%'' OR ' + @n + N' LIKE N''%including breakfast%''
                OR ' + @n + N' LIKE N''%bed and breakfast%'');
            PRINT ''รวมอาหารเช้า (จากหมายเหตุ): '' + CONVERT(VARCHAR(10), @@ROWCOUNT) + '' แถว'';';
        EXEC sp_executesql @sql;
    END
END
GO

-- ── ค่าตั้ง: จัดห้องตาม mapping แบบเข้ม (ช่องทาง + ชื่อห้องเต็มตรงกัน) ─────────────────────
IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_MapStrict')
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Email_Rsv_MapStrict', '1');
GO

-- ── รายงาน: mapping ทั้งหมดพร้อม "ชื่อห้อง/ประเภท" (เดิมเห็นแค่เลข) ───────────────────────────
IF OBJECT_ID('dbo.MapDataWithSTAAH', 'U') IS NOT NULL AND OBJECT_ID('dbo.Accommodation', 'U') IS NOT NULL
BEGIN
    PRINT '════ mapping ห้อง OTA (ช่องทาง | ชื่อห้องจาก OTA | อาหารเช้า | ห้องที่ผูก) ════';
    DECLARE @typeExpr NVARCHAR(200) =
        CASE WHEN COL_LENGTH('dbo.Accommodation', 'Accommodation_Type') IS NOT NULL
             THEN N'ISNULL(CONVERT(NVARCHAR(100), a.Accommodation_Type), N''?'')' ELSE N'N''?''' END;
    DECLARE @rep NVARCHAR(MAX) = N'
        SELECT ISNULL(m.Agency, N'''') AS Agency, ISNULL(m.ROOM_TYPE, N'''') AS RoomType,
               ISNULL(m.Breakfast, N''-'') AS Breakfast,
               COUNT(DISTINCT ISNULL(' + @typeExpr + N', N''?'')) AS TypeCount,
               STUFF((SELECT N'', '' + CONVERT(NVARCHAR(10), m2.Accommodation_ID) + N'' '' + ISNULL(a2.AccomName, N''(ไม่มีห้องนี้)'')
                             + N'' ['' + ' + REPLACE(@typeExpr, N'a.', N'a2.') + N' + N'']''
                        FROM dbo.MapDataWithSTAAH m2 LEFT JOIN dbo.Accommodation a2 ON a2.ID = m2.Accommodation_ID
                       WHERE ISNULL(m2.Agency, N'''') = ISNULL(m.Agency, N'''') AND ISNULL(m2.ROOM_TYPE, N'''') = ISNULL(m.ROOM_TYPE, N'''')
                       ORDER BY m2.Accommodation_ID FOR XML PATH(''''), TYPE).value(''.'', ''NVARCHAR(MAX)''), 1, 2, N'''') AS Rooms
          FROM dbo.MapDataWithSTAAH m LEFT JOIN dbo.Accommodation a ON a.ID = m.Accommodation_ID
         GROUP BY ISNULL(m.Agency, N''''), ISNULL(m.ROOM_TYPE, N''''), ISNULL(m.Breakfast, N''-'')
         ORDER BY 1, 2;';
    EXEC sp_executesql @rep;
    PRINT '>> TypeCount > 1 = ห้องที่ผูกปนหลายประเภท (เช่น Nordic Tent + ห้องกระจก) — ตรวจที่ ศูนย์ตั้งค่า → Mapping ห้อง OTA';
    PRINT '>> Breakfast = ''-'' = ยังไม่ระบุอาหารเช้า — ติ๊กที่หน้าเดียวกัน';
END
GO

PRINT 'PHASE19_Migration_44 completed';
GO
