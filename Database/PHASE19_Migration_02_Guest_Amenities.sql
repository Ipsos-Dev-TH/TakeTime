-- ============================================================================
-- PHASE19 Migration 02 — เบิกของใช้ในห้อง (Amenities) จาก Guest Portal
-- ============================================================================
-- ผู้เข้าพักกดเบิกของใช้เพิ่ม (ผ้าเช็ดตัว/แปรงสีฟัน/น้ำดื่ม ฯลฯ) ได้เอง
-- ตั้งค่าได้ว่าอะไรฟรี อะไรคิดเงิน และฟรีกี่ชิ้นก่อนถึงจะเริ่มคิดเงิน
-- เมื่อกดเบิก → แจ้งเตือนพนักงานเหมือนออเดอร์รูมเซอร์วิส
--
-- ปลอดภัย: รันซ้ำได้ ไม่ลบข้อมูล ไม่แตะตารางอื่น
-- ============================================================================

SET NOCOUNT ON;

-- ── 1) รายการของใช้ที่เบิกได้ ────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_Amenity_Item')
BEGIN
    CREATE TABLE Guest_Amenity_Item (
        ID                  INT IDENTITY(1,1) PRIMARY KEY,
        Name                NVARCHAR(200) NOT NULL,
        Description         NVARCHAR(500) NULL,
        Category            NVARCHAR(50)  NULL,          -- ห้องน้ำ / เครื่องนอน / เครื่องดื่ม / อื่น ๆ
        Image_Path          NVARCHAR(500) NULL,
        Icon                NVARCHAR(50)  NULL,          -- อิโมจิ ใช้เมื่อไม่มีรูป

        -- ── กติกาค่าใช้จ่าย ────────────────────────────────────────────────
        -- Is_Free = 1            → ฟรีเสมอ (ไม่คิดเงิน ไม่จำกัดด้วยโควตา)
        -- Is_Free = 0 + โควตา>0  → ฟรี N ชิ้นแรกต่อการเข้าพัก เกินนั้นคิดตาม Price
        -- Is_Free = 0 + โควตา=0  → คิดเงินทุกชิ้น
        Is_Free             BIT           NOT NULL DEFAULT 1,
        Price               DECIMAL(10,2) NOT NULL DEFAULT 0,
        Free_Quota_Per_Stay INT           NOT NULL DEFAULT 0,

        Unit                NVARCHAR(30)  NULL,          -- ชิ้น / ชุด / ขวด
        Max_Per_Request     INT           NOT NULL DEFAULT 5,   -- กันกดเบิกทีละเยอะผิดปกติ
        Sort_Order          INT           NOT NULL DEFAULT 0,
        Status              NVARCHAR(10)  NOT NULL DEFAULT 'True',
        Created_Date        DATETIME      DEFAULT GETDATE()
    );
    PRINT 'สร้างตาราง Guest_Amenity_Item';
END
GO

-- ── 2) ใบเบิก ────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_Amenity_Request')
BEGIN
    CREATE TABLE Guest_Amenity_Request (
        ID                    BIGINT IDENTITY(1,1) PRIMARY KEY,
        Request_Number        NVARCHAR(30)  NOT NULL,
        Reservation_ID        BIGINT        NOT NULL,
        Customer_MobilePhone  NVARCHAR(20)  NULL,
        Accommodation_ID      SMALLINT      NULL,
        Note                  NVARCHAR(500) NULL,        -- ข้อความถึงพนักงาน
        Total_Amount          DECIMAL(10,2) NOT NULL DEFAULT 0,
        Payment_Method        NVARCHAR(30)  NOT NULL DEFAULT 'FREE',   -- FREE / CHARGE_TO_ROOM
        -- PENDING = รอพนักงานรับเรื่อง, ACCEPTED = กำลังจัดของ,
        -- DELIVERED = ส่งแล้ว, CANCELLED = ยกเลิก
        Status                NVARCHAR(20)  NOT NULL DEFAULT 'PENDING',
        Requested_Date        DATETIME      DEFAULT GETDATE(),
        Completed_Date        DATETIME      NULL,
        Staff_ID              SMALLINT      NULL
    );
    CREATE INDEX IX_AmenityRequest_Reservation ON Guest_Amenity_Request (Reservation_ID, Requested_Date);
    CREATE INDEX IX_AmenityRequest_Status ON Guest_Amenity_Request (Status, Requested_Date);
    PRINT 'สร้างตาราง Guest_Amenity_Request';
END
GO

-- ── 3) รายการในใบเบิก ────────────────────────────────────────────────────────
-- เก็บชื่อ/ราคา ณ เวลาที่เบิก — แก้ราคาหรือลบรายการทีหลังไม่กระทบใบเก่า
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_Amenity_Request_Item')
BEGIN
    CREATE TABLE Guest_Amenity_Request_Item (
        ID          BIGINT IDENTITY(1,1) PRIMARY KEY,
        Request_ID  BIGINT        NOT NULL,
        Item_ID     INT           NULL,
        Item_Name   NVARCHAR(200) NOT NULL,
        Quantity    INT           NOT NULL DEFAULT 1,
        Free_Qty    INT           NOT NULL DEFAULT 0,    -- ส่วนที่ได้ฟรีจากโควตา
        Unit_Price  DECIMAL(10,2) NOT NULL DEFAULT 0,
        Subtotal    DECIMAL(10,2) NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_AmenityRequestItem_Request ON Guest_Amenity_Request_Item (Request_ID);
    PRINT 'สร้างตาราง Guest_Amenity_Request_Item';
END
GO

-- ── 4) รายการตั้งต้น — ปรับ/ลบได้จากหน้า Admin ──────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Guest_Amenity_Item)
BEGIN
    INSERT INTO Guest_Amenity_Item
        (Name, Description, Category, Icon, Is_Free, Price, Free_Quota_Per_Stay, Unit, Max_Per_Request, Sort_Order)
    VALUES
        -- ฟรีเสมอ
        (N'ผ้าเช็ดตัวเพิ่ม',   N'ผ้าเช็ดตัวสะอาดเพิ่มเติม',              N'เครื่องนอน', N'🧻', 1, 0,   0, N'ผืน', 4, 1),
        (N'ชุดแปรงสีฟัน',      N'แปรงสีฟัน + ยาสีฟัน',                   N'ห้องน้ำ',   N'🪥', 1, 0,   0, N'ชุด', 4, 2),
        (N'สบู่ / แชมพู',       N'สบู่ก้อนหรือแชมพูซอง',                  N'ห้องน้ำ',   N'🧴', 1, 0,   0, N'ชิ้น', 4, 3),
        (N'กระดาษชำระ',        N'กระดาษชำระม้วนเพิ่ม',                    N'ห้องน้ำ',   N'🧻', 1, 0,   0, N'ม้วน', 4, 4),
        (N'ไม้แขวนเสื้อ',       N'ไม้แขวนเสื้อเพิ่มเติม',                  N'อื่น ๆ',    N'🧥', 1, 0,   0, N'อัน', 6, 5),
        -- ฟรีตามโควตา แล้วค่อยคิดเงิน
        (N'น้ำดื่ม',           N'น้ำดื่มบรรจุขวด — ฟรี 2 ขวดแรกต่อการเข้าพัก จากนั้นขวดละ 15 บาท',
                                                                          N'เครื่องดื่ม', N'💧', 0, 15,  2, N'ขวด', 6, 6),
        (N'หมอนเพิ่ม',         N'หมอนเสริม — ฟรี 1 ใบแรก จากนั้นใบละ 50 บาท',
                                                                          N'เครื่องนอน', N'🛏️', 0, 50,  1, N'ใบ', 2, 7),
        -- คิดเงินทุกชิ้น
        (N'ผ้าห่มเสริม',       N'ผ้าห่มเสริมสำหรับคืนที่อากาศเย็น',        N'เครื่องนอน', N'🧣', 0, 100, 0, N'ผืน', 2, 8),
        (N'ชุดอาบน้ำเด็ก',      N'อ่างอาบน้ำเด็กพร้อมของใช้',              N'อื่น ๆ',    N'🛁', 0, 150, 0, N'ชุด', 1, 9);

    PRINT 'seed รายการของใช้ตั้งต้น: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' รายการ';
END
GO

-- ── ตรวจผล ──────────────────────────────────────────────────────────────────
SELECT Name, Category,
       CASE WHEN Is_Free = 1 THEN N'ฟรีเสมอ'
            WHEN Free_Quota_Per_Stay > 0
                THEN N'ฟรี ' + CAST(Free_Quota_Per_Stay AS NVARCHAR(10)) + N' ' + ISNULL(Unit, N'ชิ้น')
                     + N' แรก จากนั้น ' + CAST(CAST(Price AS DECIMAL(10,0)) AS NVARCHAR(10)) + N' บาท'
            ELSE CAST(CAST(Price AS DECIMAL(10,0)) AS NVARCHAR(10)) + N' บาท/' + ISNULL(Unit, N'ชิ้น')
       END AS เงื่อนไขค่าใช้จ่าย,
       Max_Per_Request AS เบิกได้ครั้งละไม่เกิน, Status
FROM Guest_Amenity_Item
ORDER BY Sort_Order;
