-- ============================================================================
-- PHASE19 Migration 01 — สถานที่แนะนำใกล้เคียง: พิกัด + รูป + หมุดบนแผนที่ + ขอบเขตพื้นที่
-- ============================================================================
-- ต่อยอดของเดิม ไม่สร้างใหม่ทับ: ตาราง Guest_NearbyPlaces มีอยู่แล้ว
-- (Category/Name/Description/Distance/Travel_Time/Map_Url/Phone/Icon/Sort_Order/Status)
-- แต่ยังขาดสิ่งที่ทำให้แสดงบนแผนที่ได้จริง:
--   • พิกัด (lat/lng)      — เดิมมีแค่ Map_Url ที่พิมพ์เอง วางหมุดไม่ได้
--   • รูปภาพ                — เดิมไม่มีเลย
--   • รูปแบบหมุด            — เดิม Icon เป็นอิโมจิสำหรับ list เท่านั้น
--   • ขอบเขตพื้นที่          — ไม่มี จึงวาดรูปทรงอำเภอ/โซนไม่ได้
--   • ประเภทสถานที่          — hard-code 5 ชนิดในโค้ด เพิ่มเองไม่ได้
--
-- ปลอดภัย: รันซ้ำได้ ไม่ลบข้อมูล ไม่แตะแถวเดิม (ค่าใหม่เป็น NULL ได้หมด)
-- ของเดิมยังทำงานต่อได้ทันทีแม้ยังไม่กรอกพิกัด (หน้าเว็บ fallback เป็นรายการเหมือนเดิม)
-- ============================================================================

SET NOCOUNT ON;

-- ── 0) ถ้ายังไม่มีตารางหลัก ให้สร้างตามสคีมาเดิม (หน้า Admin เคยสร้างให้ตอนรันครั้งแรก) ──
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_NearbyPlaces')
BEGIN
    CREATE TABLE Guest_NearbyPlaces (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        Category NVARCHAR(50) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        Description NVARCHAR(500),
        Distance NVARCHAR(50),
        Travel_Time NVARCHAR(50),
        Map_Url NVARCHAR(500),
        Phone NVARCHAR(50),
        Icon NVARCHAR(50),
        Sort_Order INT DEFAULT 0,
        Status NVARCHAR(10) DEFAULT 'True',
        Created_Date DATETIME DEFAULT GETDATE()
    );
    PRINT 'สร้างตาราง Guest_NearbyPlaces';
END
GO

-- ── 1) เพิ่มคอลัมน์ใหม่ให้ตารางเดิม ─────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Latitude')
BEGIN
    -- DECIMAL(9,6) = ความละเอียดราว 0.1 เมตร เพียงพอสำหรับหมุดสถานที่
    ALTER TABLE Guest_NearbyPlaces ADD Latitude DECIMAL(9,6) NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Latitude';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Longitude')
BEGIN
    ALTER TABLE Guest_NearbyPlaces ADD Longitude DECIMAL(9,6) NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Longitude';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Image_Path')
BEGIN
    ALTER TABLE Guest_NearbyPlaces ADD Image_Path NVARCHAR(500) NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Image_Path';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Marker_Color')
BEGIN
    -- สีหมุดต่อสถานที่ (ว่าง = ใช้สีของประเภท)
    ALTER TABLE Guest_NearbyPlaces ADD Marker_Color NVARCHAR(20) NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Marker_Color';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Marker_Icon')
BEGIN
    -- อิโมจิ/ตัวอักษรที่จะแสดงกลางหมุด (ว่าง = ใช้ของประเภท)
    ALTER TABLE Guest_NearbyPlaces ADD Marker_Icon NVARCHAR(50) NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Marker_Icon';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Marker_Image')
BEGIN
    -- รูปหมุดแบบกำหนดเอง (เช่น โลโก้ร้าน) — ถ้ามี จะใช้แทนหมุดอิโมจิ
    ALTER TABLE Guest_NearbyPlaces ADD Marker_Image NVARCHAR(500) NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Marker_Image';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Zone_ID')
BEGIN
    ALTER TABLE Guest_NearbyPlaces ADD Zone_ID INT NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Zone_ID';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Address')
BEGIN
    ALTER TABLE Guest_NearbyPlaces ADD Address NVARCHAR(300) NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Address';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Open_Hours')
BEGIN
    ALTER TABLE Guest_NearbyPlaces ADD Open_Hours NVARCHAR(100) NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Open_Hours';
END
GO

-- ── 2) ประเภทสถานที่ — ย้ายจาก hard-code ในโค้ดมาเป็นตารางที่แก้ได้ ─────────
-- โค้ดเดิม (Guest/NearbyPlaces.aspx.cs, Admin/ManageNearbyPlaces.aspx.cs) ผูก 5 ชนิดไว้ตายตัว
-- เพิ่มประเภทใหม่ต้องแก้โค้ด → ย้ายมาเป็นข้อมูล แล้ว seed ค่าเดิมไว้ทั้งหมด
-- ⇒ แถวเดิมที่เก็บ Category เป็น 'beach'/'restaurant'/… ยังใช้งานได้เหมือนเดิม ไม่ต้องแปลงข้อมูล
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_NearbyPlace_Category')
BEGIN
    CREATE TABLE Guest_NearbyPlace_Category (
        ID           INT IDENTITY(1,1) PRIMARY KEY,
        Code         NVARCHAR(50)  NOT NULL UNIQUE,   -- ตรงกับ Guest_NearbyPlaces.Category
        Name         NVARCHAR(100) NOT NULL,          -- ชื่อที่แสดง (ไทย)
        Icon         NVARCHAR(50)  NULL,              -- อิโมจิประจำประเภท
        Marker_Color NVARCHAR(20)  NULL,              -- สีหมุดประจำประเภท
        Sort_Order   INT           DEFAULT 0,
        Status       NVARCHAR(10)  DEFAULT 'True',
        Created_Date DATETIME      DEFAULT GETDATE()
    );
    PRINT 'สร้างตาราง Guest_NearbyPlace_Category';
END
GO

-- seed ประเภทเดิมที่ hard-code ไว้ (idempotent — เพิ่มเฉพาะที่ยังไม่มี)
INSERT INTO Guest_NearbyPlace_Category (Code, Name, Icon, Marker_Color, Sort_Order)
SELECT v.Code, v.Name, v.Icon, v.Color, v.Ord
FROM (VALUES
    ('beach',      N'ชายหาด',            N'🏖️', '#0288D1', 1),
    ('restaurant', N'ร้านอาหาร',          N'🍽️', '#E64A19', 2),
    ('cafe',       N'คาเฟ่',              N'☕',  '#6D4C41', 3),
    ('attraction', N'สถานที่ท่องเที่ยว',  N'🎯',  '#7B1FA2', 4),
    ('shopping',   N'ช้อปปิ้ง',           N'🛒',  '#00897B', 5),
    ('temple',     N'วัด / ศาสนสถาน',     N'🛕',  '#F9A825', 6),
    ('hospital',   N'โรงพยาบาล / คลินิก', N'🏥',  '#C62828', 7),
    ('transport',  N'สถานีขนส่ง / ท่าเรือ', N'🚉', '#455A64', 8)
) AS v(Code, Name, Icon, Color, Ord)
WHERE NOT EXISTS (SELECT 1 FROM Guest_NearbyPlace_Category c WHERE c.Code = v.Code);

PRINT 'seed ประเภทสถานที่: เพิ่ม ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' ประเภท';
GO

-- ประเภทที่มีข้อมูลอยู่แล้วแต่ไม่อยู่ใน seed (เคยพิมพ์เองในฐานข้อมูล) — ดึงขึ้นมาเป็นประเภทด้วย
-- ไม่งั้นสถานที่เหล่านั้นจะหายจากตัวกรองเพราะไม่มีประเภทรองรับ
INSERT INTO Guest_NearbyPlace_Category (Code, Name, Icon, Marker_Color, Sort_Order)
SELECT DISTINCT p.Category, p.Category, N'📍', '#1976D2', 99
FROM Guest_NearbyPlaces p
WHERE ISNULL(LTRIM(RTRIM(p.Category)), '') <> ''
  AND NOT EXISTS (SELECT 1 FROM Guest_NearbyPlace_Category c WHERE c.Code = p.Category);

PRINT 'กู้ประเภทที่มีอยู่ในข้อมูลแต่ยังไม่มีในตารางประเภท: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
GO

-- ── 3) โซน/พื้นที่ + ขอบเขตสำหรับวาดบนแผนที่ ────────────────────────────────
-- Boundary_GeoJson เก็บ GeoJSON ของรูปทรงขอบเขต (Polygon / MultiPolygon)
-- เช่น ขอบเขตอำเภอศรีราชา — หาได้จาก OpenStreetMap แล้ววางลงช่องในหน้า Admin
-- ว่างไว้ได้: หน้าเว็บจะ auto-fit ตามหมุดทั้งหมดแทน
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_NearbyZone')
BEGIN
    CREATE TABLE Guest_NearbyZone (
        ID               INT IDENTITY(1,1) PRIMARY KEY,
        Name             NVARCHAR(150) NOT NULL,      -- เช่น "อำเภอศรีราชา"
        Boundary_GeoJson NVARCHAR(MAX) NULL,          -- Polygon / MultiPolygon
        Center_Lat       DECIMAL(9,6)  NULL,
        Center_Lng       DECIMAL(9,6)  NULL,
        Default_Zoom     INT           DEFAULT 12,
        Fill_Color       NVARCHAR(20)  DEFAULT '#00b09b',
        Line_Color       NVARCHAR(20)  DEFAULT '#00796B',
        Is_Default       BIT           DEFAULT 0,     -- โซนที่เปิดมาเห็นก่อน
        Sort_Order       INT           DEFAULT 0,
        Status           NVARCHAR(10)  DEFAULT 'True',
        Created_Date     DATETIME      DEFAULT GETDATE()
    );
    PRINT 'สร้างตาราง Guest_NearbyZone';
END
GO

-- โซนตั้งต้น: ศรีราชา (ยังไม่ใส่ขอบเขต — ผู้ใช้วางเองจากหน้า Admin ทีหลังได้)
-- ใส่จุดศูนย์กลางไว้ก่อนเพื่อให้แผนที่เปิดมาถูกที่ตั้งแต่ครั้งแรก
IF NOT EXISTS (SELECT 1 FROM Guest_NearbyZone)
BEGIN
    INSERT INTO Guest_NearbyZone (Name, Center_Lat, Center_Lng, Default_Zoom, Is_Default, Sort_Order)
    VALUES (N'ศรีราชา / บางพระ', 13.174800, 100.930600, 12, 1, 1);
    PRINT 'สร้างโซนตั้งต้น "ศรีราชา / บางพระ"';
END
GO

-- ── 4) index ที่ใช้จริงในหน้าเว็บ ───────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NearbyPlaces_Status_Cat'
               AND object_id = OBJECT_ID('Guest_NearbyPlaces'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_NearbyPlaces_Status_Cat
        ON Guest_NearbyPlaces (Status, Category) INCLUDE (Sort_Order, Name);
    PRINT 'สร้าง IX_NearbyPlaces_Status_Cat';
END
GO

-- ── ตรวจผล ─────────────────────────────────────────────────────────────────
SELECT COUNT(*) AS สถานที่ทั้งหมด,
       SUM(CASE WHEN Latitude IS NOT NULL AND Longitude IS NOT NULL THEN 1 ELSE 0 END) AS มีพิกัดแล้ว,
       SUM(CASE WHEN ISNULL(Image_Path, '') <> '' THEN 1 ELSE 0 END) AS มีรูปแล้ว
FROM Guest_NearbyPlaces;

SELECT Code, Name, Icon, Marker_Color, Sort_Order FROM Guest_NearbyPlace_Category ORDER BY Sort_Order, Code;
SELECT ID, Name, Center_Lat, Center_Lng, Default_Zoom,
       CASE WHEN ISNULL(Boundary_GeoJson, '') = '' THEN N'ยังไม่ได้ใส่ขอบเขต (แผนที่จะ auto-fit ตามหมุด)'
            ELSE N'มีขอบเขตแล้ว' END AS ขอบเขต
FROM Guest_NearbyZone ORDER BY Sort_Order;
