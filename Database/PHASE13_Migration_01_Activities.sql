-- ============================================================
-- PHASE13 Migration 01: Activities System
-- On-property and off-property activities for guest portal
-- ============================================================

-- Activities table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Property_Activities]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Property_Activities] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [ActivityName] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(MAX),
        [Category] VARCHAR(20) NOT NULL, -- ON_PROPERTY, OFF_PROPERTY
        [ImagePath] NVARCHAR(500),
        [Price] DECIMAL(10,2),
        [Duration] NVARCHAR(50),
        [Location] NVARCHAR(200),
        [ContactInfo] NVARCHAR(200),
        [MapUrl] NVARCHAR(500),
        [DisplayOrder] INT DEFAULT 0,
        [IsActive] BIT DEFAULT 1,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [CreatedBy_AdminID] SMALLINT,
        [LastUpdated] DATETIME DEFAULT GETDATE(),

        CONSTRAINT [FK_PropertyActivities_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_PropertyActivities_Category] ON [dbo].[Property_Activities]([Category]);
    CREATE INDEX [IDX_PropertyActivities_Active] ON [dbo].[Property_Activities]([IsActive]);

    -- Sample on-property activities
    INSERT INTO [dbo].[Property_Activities] ([ActivityName], [Description], [Category], [Price], [Duration], [Location], [DisplayOrder]) VALUES
    (N'สระว่ายน้ำ', N'สระว่ายน้ำกลางแจ้งขนาดใหญ่ เปิดให้บริการ 07:00 - 20:00 น. พร้อมเก้าอี้นอนอาบแดดและผ้าเช็ดตัว', 'ON_PROPERTY', 0, N'ตลอดวัน', N'บริเวณส่วนกลาง', 1),
    (N'ห้องออกกำลังกาย', N'ห้อง Fitness Center พร้อมอุปกรณ์ออกกำลังกายครบครัน เปิดให้บริการ 06:00 - 22:00 น.', 'ON_PROPERTY', 0, N'ตลอดวัน', N'อาคาร B ชั้น 1', 2),
    (N'สปาและนวดแผนไทย', N'บริการนวดแผนไทย นวดน้ำมัน สปาผิวกาย โดยช่างนวดมืออาชีพ', 'ON_PROPERTY', 500, N'1-2 ชั่วโมง', N'Spa Center', 3),
    (N'จักรยานให้ยืม', N'บริการจักรยานฟรีสำหรับผู้เข้าพัก ปั่นชมวิวรอบๆ ที่พัก', 'ON_PROPERTY', 0, N'ไม่จำกัด', N'ล็อบบี้', 4),
    (N'บาร์บีคิว', N'บริการเตาบาร์บีคิวส่วนตัว พร้อมอุปกรณ์ (กรุณาจองล่วงหน้า)', 'ON_PROPERTY', 300, N'ตอนเย็น', N'สวนหลัง', 5);

    -- Sample off-property activities
    INSERT INTO [dbo].[Property_Activities] ([ActivityName], [Description], [Category], [Price], [Duration], [Location], [DisplayOrder]) VALUES
    (N'ล่องเรือเกาะสีชัง', N'ทัวร์เรือเที่ยวเกาะสีชัง ชมวิวทะเล แวะว่ายน้ำ ดำน้ำดูปะการัง พร้อมอาหารกลางวัน', 'OFF_PROPERTY', 1500, N'ทั้งวัน (08:00-17:00)', N'ท่าเรือศรีราชา', 1),
    (N'ตลาดอาหารทะเลอ่างศิลา', N'ตลาดอาหารทะเลสดๆ ราคาย่อมเยา ห่างจากที่พักเพียง 10 นาที', 'OFF_PROPERTY', NULL, N'2-3 ชั่วโมง', N'อ่างศิลา (10 กม.)', 2),
    (N'สวนสัตว์เปิดเขาเขียว', N'สวนสัตว์เปิดขนาดใหญ่ พบสัตว์หลากหลายชนิดในธรรมชาติ เหมาะสำหรับครอบครัว', 'OFF_PROPERTY', 200, N'ครึ่งวัน - ทั้งวัน', N'เขาเขียว (25 กม.)', 3),
    (N'วัดเขาบางทราย', N'วัดเก่าแก่บนยอดเขา ชมวิวพาโนรามา 360 องศา บรรยากาศสงบร่มรื่น', 'OFF_PROPERTY', 0, N'1-2 ชั่วโมง', N'บางทราย (8 กม.)', 4),
    (N'เที่ยวพัทยา', N'เมืองท่องเที่ยวชื่อดัง ช้อปปิ้ง Walking Street ชมโชว์ ห่างจากที่พัก 30 นาที', 'OFF_PROPERTY', NULL, N'ครึ่งวัน - ทั้งวัน', N'พัทยา (30 กม.)', 5),
    (N'ดำน้ำตื้นเกาะล้าน', N'ทริปดำน้ำตื้นชมปะการัง หาดทรายขาว น้ำทะเลใส พร้อมอุปกรณ์ดำน้ำ', 'OFF_PROPERTY', 1200, N'ทั้งวัน', N'ท่าเรือพัทยา (35 กม.)', 6);

    PRINT 'Created Property_Activities table with sample data';
END
GO
