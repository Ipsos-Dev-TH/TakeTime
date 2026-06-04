-- ================================================================
-- PHASE 14: AI Integration - Knowledge Base & Learning System
-- Migration 02: Knowledge base, learned patterns, feature settings
-- ================================================================

-- 1. AI Knowledge Base (uploaded training data & Q&A pairs)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Knowledge_Base')
BEGIN
    CREATE TABLE AI_Knowledge_Base (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        Category NVARCHAR(50) NOT NULL DEFAULT 'GENERAL',
        Question NVARCHAR(MAX) NOT NULL,
        Answer NVARCHAR(MAX) NOT NULL,
        Keywords NVARCHAR(500),
        Language NVARCHAR(10) DEFAULT 'TH',
        Source NVARCHAR(50) DEFAULT 'MANUAL',
        IsActive BIT DEFAULT 1,
        MatchCount INT DEFAULT 0,
        Created_Date DATETIME DEFAULT GETDATE(),
        Updated_Date DATETIME DEFAULT GETDATE(),
        INDEX IX_AI_KB_Category (Category),
        INDEX IX_AI_KB_Active (IsActive)
    );

    INSERT INTO AI_Knowledge_Base (Category, Question, Answer, Keywords, Source) VALUES
    (N'CHECK_IN',   N'เช็คอินกี่โมง', N'เวลาเช็คอิน 14:00 น. ค่ะ หากต้องการ Early check-in สามารถแจ้งล่วงหน้าได้นะคะ (ขึ้นอยู่กับห้องว่าง)', N'เช็คอิน,check in,check-in,กี่โมง,เวลา', 'SEED'),
    (N'CHECK_OUT',  N'เช็คเอาท์กี่โมง', N'เวลาเช็คเอาท์ 12:00 น. ค่ะ หากต้องการ Late check-out กรุณาแจ้งล่วงหน้าอย่างน้อย 1 วัน (อาจมีค่าใช้จ่ายเพิ่มเติม)', N'เช็คเอาท์,checkout,check out,กี่โมง', 'SEED'),
    (N'WIFI',       N'รหัส WiFi คืออะไร', N'WiFi ชื่อ "TakeTime-Guest" รหัส TakeTime2024 ค่ะ ใช้ได้ทั่วทั้งที่พักเลยนะคะ', N'wifi,ไวไฟ,password,รหัส,อินเทอร์เน็ต,internet', 'SEED'),
    (N'LOCATION',   N'ที่พักอยู่ที่ไหน', N'TakeTime BangPhra ตั้งอยู่ที่ตำบลบางพระ อำเภอศรีราชา จังหวัดชลบุรี ริมทะเลบางพระ ห่างจากกรุงเทพฯ ประมาณ 1.5 ชั่วโมง', N'ที่ไหน,ตั้งอยู่,location,ที่อยู่,แผนที่,map,ทาง', 'SEED'),
    (N'PARKING',    N'มีที่จอดรถไหม', N'มีที่จอดรถฟรีสำหรับแขกทุกท่านค่ะ จอดได้บริเวณหน้าที่พักเลยนะคะ', N'จอดรถ,parking,ที่จอด,รถ', 'SEED'),
    (N'PET',        N'นำสัตว์เลี้ยงไปได้ไหม', N'ขออภัยค่ะ ทางที่พักไม่อนุญาตให้นำสัตว์เลี้ยงเข้าพักนะคะ เพื่อความสะดวกของแขกท่านอื่นค่ะ', N'สัตว์เลี้ยง,pet,หมา,แมว,dog,cat,สุนัข', 'SEED'),
    (N'BREAKFAST',  N'มีอาหารเช้าไหม', N'มีบริการอาหารเช้าค่ะ เสิร์ฟตั้งแต่ 07:00-10:00 น. ที่ห้องอาหารชั้น 1 สามารถสอบถามเมนูเพิ่มเติมได้ที่ Front Desk นะคะ', N'อาหารเช้า,breakfast,เช้า,กิน,ข้าว', 'SEED'),
    (N'CANCEL',     N'ยกเลิกการจองทำยังไง', N'สำหรับการยกเลิกการจอง กรุณาแจ้งล่วงหน้าอย่างน้อย 3 วันก่อนเช็คอิน เพื่อรับเงินคืนเต็มจำนวนค่ะ หากยกเลิกน้อยกว่า 3 วัน อาจมีค่าธรรมเนียมนะคะ', N'ยกเลิก,cancel,เปลี่ยน,คืนเงิน,refund', 'SEED'),
    (N'BOOKING',    N'จะจองห้องพักทำยังไง', N'สามารถจองห้องพักได้หลายช่องทางค่ะ:\n1. จองผ่านเว็บไซต์ของเราโดยตรง\n2. โทรจอง\n3. จองผ่าน LINE Official\n4. จองผ่าน Agoda, Booking.com\nแจ้งวันเช็คอิน-เช็คเอาท์ และจำนวนผู้เข้าพักมาได้เลยค่ะ', N'จอง,booking,book,reserve,สำรอง,ห้องพัก,ว่าง', 'SEED'),
    (N'PRICE',      N'ราคาห้องพักเท่าไหร่', N'ราคาห้องพักขึ้นอยู่กับประเภทห้องและวันที่เข้าพักค่ะ กรุณาแจ้งวันที่ต้องการเข้าพัก จะเช็คราคาให้นะคะ', N'ราคา,price,เท่าไหร่,ค่าห้อง,cost,rate', 'SEED');

    PRINT 'Created table: AI_Knowledge_Base with seed data';
END
GO

-- 2. AI Learned Patterns (learns from staff corrections & successful responses)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Learned_Patterns')
BEGIN
    CREATE TABLE AI_Learned_Patterns (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        InputPattern NVARCHAR(MAX) NOT NULL,
        InputKeywords NVARCHAR(500),
        Response NVARCHAR(MAX) NOT NULL,
        Category NVARCHAR(50),
        Confidence FLOAT DEFAULT 0.5,
        SuccessCount INT DEFAULT 0,
        FailCount INT DEFAULT 0,
        Source NVARCHAR(30) DEFAULT 'LEARNED',
        LearnedFrom NVARCHAR(100),
        IsActive BIT DEFAULT 1,
        Created_Date DATETIME DEFAULT GETDATE(),
        Updated_Date DATETIME DEFAULT GETDATE(),
        INDEX IX_AI_LP_Active (IsActive),
        INDEX IX_AI_LP_Confidence (Confidence DESC)
    );

    PRINT 'Created table: AI_Learned_Patterns';
END
GO

-- 3. AI Feature Settings (granular per-feature per-channel toggles)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Feature_Settings')
BEGIN
    CREATE TABLE AI_Feature_Settings (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        FeatureCode NVARCHAR(50) NOT NULL,
        FeatureName NVARCHAR(100) NOT NULL,
        Description NVARCHAR(300),
        IsEnabled BIT DEFAULT 0,
        ChannelScope NVARCHAR(200) DEFAULT 'ALL',
        Config NVARCHAR(MAX),
        Updated_Date DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_AI_Feature UNIQUE (FeatureCode)
    );

    INSERT INTO AI_Feature_Settings (FeatureCode, FeatureName, Description, IsEnabled, ChannelScope) VALUES
    ('AUTO_REPLY',          N'AI ตอบอัตโนมัติ',              N'AI ตอบกลับข้อความลูกค้าอัตโนมัติในทุกช่องทาง',          0, 'ALL'),
    ('AUTO_REPLY_KB_ONLY',  N'ตอบจาก Knowledge Base เท่านั้น', N'ตอบเฉพาะเมื่อมีคำตอบใน Knowledge Base (ไม่เรียก API)',  0, 'ALL'),
    ('BOOKING_LOOKUP',      N'ค้นหาข้อมูลการจอง',             N'AI สามารถค้นหาข้อมูลการจอง ห้องว่าง ราคาให้ลูกค้า',    0, 'ALL'),
    ('BOOKING_CREATE',      N'สร้างการจองอัตโนมัติ',           N'AI สามารถสร้างการจองเบื้องต้นให้ลูกค้า (ต้องพนักงานยืนยัน)', 0, 'ALL'),
    ('LEARN_FROM_STAFF',    N'เรียนรู้จากพนักงาน',            N'บันทึกรูปแบบการตอบของพนักงานเพื่อเรียนรู้',            1, 'ALL'),
    ('SUGGEST_REPLY',       N'แนะนำคำตอบ',                   N'แนะนำข้อความตอบกลับให้พนักงาน',                       0, 'ALL'),
    ('SENTIMENT_ANALYSIS',  N'วิเคราะห์ความรู้สึก',           N'วิเคราะห์อารมณ์ลูกค้า (บวก/ลบ/กลาง) และแจ้งเตือน',   0, 'ALL'),
    ('AUTO_TAG',            N'ติดแท็กอัตโนมัติ',             N'ติดหมวดหมู่สนทนาอัตโนมัติ',                           0, 'ALL'),
    ('GUEST_CHAT_AI',       N'AI แชทผู้ช่วยแขก',             N'เปิด AI Assistant ในหน้าแชทของแขก',                   0, 'ALL');

    PRINT 'Created table: AI_Feature_Settings';
END
GO

-- 4. AI Booking Actions (log AI-initiated booking lookups and creations)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Booking_Actions')
BEGIN
    CREATE TABLE AI_Booking_Actions (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        ConversationID BIGINT,
        ActionType NVARCHAR(30) NOT NULL,
        RequestData NVARCHAR(MAX),
        ResponseData NVARCHAR(MAX),
        ReservationID BIGINT NULL,
        Status NVARCHAR(20) DEFAULT 'PENDING',
        StaffConfirmedBy NVARCHAR(100),
        Created_Date DATETIME DEFAULT GETDATE(),
        INDEX IX_AI_BA_Conv (ConversationID),
        INDEX IX_AI_BA_Status (Status)
    );

    PRINT 'Created table: AI_Booking_Actions';
END
GO

-- 5. Add AI auto-reply columns to OmniChannel tables
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OmniChannel_Messages')
BEGIN
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'OmniChannel_Messages' AND COLUMN_NAME = 'IsAIGenerated')
    BEGIN
        ALTER TABLE OmniChannel_Messages ADD IsAIGenerated BIT DEFAULT 0;
        ALTER TABLE OmniChannel_Messages ADD AIConfidence FLOAT NULL;
        ALTER TABLE OmniChannel_Messages ADD AISource NVARCHAR(30) NULL;
        PRINT 'Added AI columns to OmniChannel_Messages';
    END
END
GO

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OmniChannel_Conversations')
BEGIN
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'OmniChannel_Conversations' AND COLUMN_NAME = 'AIEnabled')
    BEGIN
        ALTER TABLE OmniChannel_Conversations ADD AIEnabled BIT DEFAULT 1;
        ALTER TABLE OmniChannel_Conversations ADD Sentiment NVARCHAR(20) NULL;
        PRINT 'Added AI columns to OmniChannel_Conversations';
    END
END
GO
