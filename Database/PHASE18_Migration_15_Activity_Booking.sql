-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 15: ระบบกิจกรรม + จองช่วงเวลา + ชำระเงิน (ชาร์จเข้าห้อง / โอนแนบสลิป)
-- ════════════════════════════════════════════════════════════════════════════
-- ต่อยอดจาก Property_Activities (PHASE13_01) ให้รองรับ:
--   • ตั้งค่ากิจกรรมได้ละเอียด (รูปหลายรูป, เวลาเปิด-ปิด, ราคาแบบต่าง ๆ, จำนวนที่รองรับ)
--   • กิจกรรมที่ต้อง "จองเวลา" เช่น โต๊ะปิงปอง (ฟรี/คิดเป็นรายชั่วโมง) — กันจองชนกัน
--   • ชำระเงิน 2 ทาง: ชาร์จเข้าห้อง (รวมจ่ายตอนเช็คเอาท์) หรือ โอนแล้วแนบสลิป (รออนุมัติ)
--   • แสดงผลหน้าเว็บสาธารณะ + Guest Portal
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- ── 0) ถ้ายังไม่มีตารางกิจกรรม (ยังไม่ได้รัน PHASE13_01) สร้างโครงขั้นต่ำไว้ก่อน ─────────
IF OBJECT_ID('dbo.Property_Activities', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Property_Activities] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [ActivityName] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [Category] VARCHAR(20) NOT NULL DEFAULT 'ON_PROPERTY',
        [ImagePath] NVARCHAR(500) NULL,
        [Price] DECIMAL(10,2) NULL,
        [Duration] NVARCHAR(50) NULL,
        [Location] NVARCHAR(200) NULL,
        [ContactInfo] NVARCHAR(200) NULL,
        [MapUrl] NVARCHAR(500) NULL,
        [DisplayOrder] INT DEFAULT 0,
        [IsActive] BIT DEFAULT 1,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [CreatedBy_AdminID] SMALLINT NULL,
        [LastUpdated] DATETIME DEFAULT GETDATE()
    );
    PRINT 'Created Property_Activities (base)';
END
GO

-- ── 1) คอลัมน์ใหม่สำหรับ "การจอง / การตั้งค่า" ───────────────────────────────────────
-- ต้องจองเวลาไหม (เช่น โต๊ะปิงปอง = 1, สระว่ายน้ำ = 0)
IF COL_LENGTH('Property_Activities', 'IsBookable') IS NULL
    ALTER TABLE Property_Activities ADD IsBookable BIT NOT NULL DEFAULT 0;
-- รูปแบบราคา: FREE / PER_HOUR / PER_SESSION / PER_PERSON
IF COL_LENGTH('Property_Activities', 'PricingMode') IS NULL
    ALTER TABLE Property_Activities ADD PricingMode VARCHAR(20) NOT NULL DEFAULT 'FREE';
-- จำนวนที่ให้บริการพร้อมกันได้ (เช่น มีโต๊ะปิงปอง 2 ตัว = จองซ้อนเวลาได้ 2 คิว)
IF COL_LENGTH('Property_Activities', 'Capacity') IS NULL
    ALTER TABLE Property_Activities ADD Capacity INT NOT NULL DEFAULT 1;
-- เวลาเปิด-ปิดบริการ
IF COL_LENGTH('Property_Activities', 'OpenTime') IS NULL
    ALTER TABLE Property_Activities ADD OpenTime TIME NULL;
IF COL_LENGTH('Property_Activities', 'CloseTime') IS NULL
    ALTER TABLE Property_Activities ADD CloseTime TIME NULL;
-- ความยาว 1 ช่วงจอง (นาที) เช่น 60 = จองเป็นรายชั่วโมง
IF COL_LENGTH('Property_Activities', 'SlotMinutes') IS NULL
    ALTER TABLE Property_Activities ADD SlotMinutes INT NOT NULL DEFAULT 60;
-- จองต่อเนื่องได้สูงสุดกี่ช่วง / ล่วงหน้าได้กี่วัน
IF COL_LENGTH('Property_Activities', 'MaxSlotsPerBooking') IS NULL
    ALTER TABLE Property_Activities ADD MaxSlotsPerBooking INT NOT NULL DEFAULT 4;
IF COL_LENGTH('Property_Activities', 'AdvanceBookingDays') IS NULL
    ALTER TABLE Property_Activities ADD AdvanceBookingDays INT NOT NULL DEFAULT 14;
-- ต้องให้พนักงานอนุมัติก่อนไหม
IF COL_LENGTH('Property_Activities', 'RequireApproval') IS NULL
    ALTER TABLE Property_Activities ADD RequireApproval BIT NOT NULL DEFAULT 0;
-- แสดงบนเว็บสาธารณะ (หน้าแรก) / แสดงใน Guest Portal
IF COL_LENGTH('Property_Activities', 'ShowOnWebsite') IS NULL
    ALTER TABLE Property_Activities ADD ShowOnWebsite BIT NOT NULL DEFAULT 1;
IF COL_LENGTH('Property_Activities', 'ShowInPortal') IS NULL
    ALTER TABLE Property_Activities ADD ShowInPortal BIT NOT NULL DEFAULT 1;
-- ผูกกับสินค้าในสต๊อก (ถ้าอยากตัดสต๊อก/ผูกบัญชีรายได้เดิม) — ปล่อยว่างได้
IF COL_LENGTH('Property_Activities', 'Linked_Product_ID') IS NULL
    ALTER TABLE Property_Activities ADD Linked_Product_ID INT NULL;
-- ข้อมูลเสริมสำหรับแสดงผล
IF COL_LENGTH('Property_Activities', 'ShortDescription') IS NULL
    ALTER TABLE Property_Activities ADD ShortDescription NVARCHAR(300) NULL;
IF COL_LENGTH('Property_Activities', 'IconClass') IS NULL
    ALTER TABLE Property_Activities ADD IconClass NVARCHAR(60) NULL;   -- เช่น fa-table-tennis-paddle-ball
IF COL_LENGTH('Property_Activities', 'Rules') IS NULL
    ALTER TABLE Property_Activities ADD Rules NVARCHAR(MAX) NULL;      -- กติกา/ข้อควรรู้
IF COL_LENGTH('Property_Activities', 'MaxParticipants') IS NULL
    ALTER TABLE Property_Activities ADD MaxParticipants INT NULL;      -- จำนวนคนสูงสุดต่อการจอง
GO

-- ── 2) รูปภาพหลายรูปต่อกิจกรรม (แกลเลอรี) ──────────────────────────────────────────
IF OBJECT_ID('dbo.Property_Activity_Images', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Property_Activity_Images] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Activity_ID] INT NOT NULL,
        [ImagePath] NVARCHAR(500) NOT NULL,
        [Caption] NVARCHAR(200) NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        [UploadedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_ActivityImages_Activity FOREIGN KEY (Activity_ID)
            REFERENCES [dbo].[Property_Activities](ID) ON DELETE CASCADE
    );
    CREATE INDEX IX_ActivityImages_Activity ON [dbo].[Property_Activity_Images](Activity_ID, DisplayOrder);
    PRINT 'Created Property_Activity_Images';
END
GO

-- ── 3) การจองกิจกรรม ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Activity_Bookings', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Activity_Bookings] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Activity_ID] INT NOT NULL,
        [Reservation_ID] INT NULL,                    -- ผูกการเข้าพัก (ชาร์จเข้าห้องได้เมื่อมีค่านี้)
        [Customer_MobilePhone] NVARCHAR(20) NULL,
        [GuestName] NVARCHAR(200) NULL,
        [Accommodation_ID] TINYINT NULL,              -- ห้องที่พัก (ไว้แสดง/ชาร์จ)

        -- ช่วงเวลาที่จอง
        [BookingDate] DATE NOT NULL,
        [StartTime] TIME NOT NULL,
        [EndTime] TIME NOT NULL,
        [Participants] INT NOT NULL DEFAULT 1,

        -- ราคา (คำนวณตอนจองแล้วเก็บไว้ — ราคากิจกรรมเปลี่ยนภายหลังไม่กระทบใบเก่า)
        [PricingMode] VARCHAR(20) NOT NULL DEFAULT 'FREE',
        [UnitPrice] DECIMAL(10,2) NOT NULL DEFAULT 0,
        [Hours] DECIMAL(6,2) NOT NULL DEFAULT 0,
        [TotalAmount] DECIMAL(10,2) NOT NULL DEFAULT 0,

        -- สถานะ: PENDING (รออนุมัติ) / CONFIRMED / CANCELLED / COMPLETED / NO_SHOW
        [Status] NVARCHAR(20) NOT NULL DEFAULT 'CONFIRMED',
        -- การชำระ: NONE (ฟรี) / ROOM_CHARGE (ชาร์จเข้าห้อง) / TRANSFER (โอน+สลิป) / CASH (จ่ายหน้างาน)
        [PaymentMethod] VARCHAR(20) NOT NULL DEFAULT 'NONE',
        -- UNPAID / PENDING_VERIFY (แนบสลิปรออนุมัติ) / PAID / WAIVED
        [PaymentStatus] VARCHAR(20) NOT NULL DEFAULT 'UNPAID',

        -- เชื่อมกับระบบอื่น
        [Charge_ID] BIGINT NULL,                      -- แถวใน Reservation_Product_Charges (ชาร์จเข้าห้อง)
        [SlipFileURL] NVARCHAR(500) NULL,             -- สลิปโอนเงิน
        [SlipUploadedDate] DATETIME NULL,
        [VerifiedBy_AdminID] SMALLINT NULL,
        [VerifiedDate] DATETIME NULL,
        [RejectionReason] NVARCHAR(500) NULL,

        [Notes] NVARCHAR(500) NULL,
        [BookedVia] VARCHAR(20) NOT NULL DEFAULT 'PORTAL',   -- PORTAL / ADMIN / WEBSITE
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedBy_AdminID] SMALLINT NULL,
        [CancelledDate] DATETIME NULL,
        [CancelledBy_AdminID] SMALLINT NULL,
        [CancelReason] NVARCHAR(300) NULL,

        CONSTRAINT FK_ActivityBookings_Activity FOREIGN KEY (Activity_ID)
            REFERENCES [dbo].[Property_Activities](ID),
        CONSTRAINT CK_ActivityBookings_Time CHECK (EndTime > StartTime),
        CONSTRAINT CK_ActivityBookings_Status CHECK
            (Status IN ('PENDING','CONFIRMED','CANCELLED','COMPLETED','NO_SHOW')),
        CONSTRAINT CK_ActivityBookings_PayMethod CHECK
            (PaymentMethod IN ('NONE','ROOM_CHARGE','TRANSFER','CASH')),
        CONSTRAINT CK_ActivityBookings_PayStatus CHECK
            (PaymentStatus IN ('UNPAID','PENDING_VERIFY','PAID','WAIVED'))
    );
    -- ค้นหาช่วงเวลาที่ชนกัน (ใช้บ่อยสุด — ตรวจว่าง)
    CREATE INDEX IX_ActivityBookings_Slot
        ON [dbo].[Activity_Bookings](Activity_ID, BookingDate, Status) INCLUDE (StartTime, EndTime, Participants);
    CREATE INDEX IX_ActivityBookings_Reservation ON [dbo].[Activity_Bookings](Reservation_ID);
    CREATE INDEX IX_ActivityBookings_Status ON [dbo].[Activity_Bookings](Status, BookingDate);
    PRINT 'Created Activity_Bookings';
END
GO

-- ── 4) ให้ค่าใช้จ่ายในห้องรองรับ "กิจกรรม" (ไม่ใช่สินค้าในสต๊อก) ────────────────────
-- เดิม Product_ID NOT NULL (+FK ไป Product) → ค่ากิจกรรมลงไม่ได้. เปลี่ยนเป็น NULL ได้
-- (FK ยอมรับ NULL อยู่แล้ว) + เพิ่มคอลัมน์อ้างอิงการจองกิจกรรม
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.Reservation_Product_Charges')
             AND name = 'Product_ID' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[Reservation_Product_Charges] ALTER COLUMN [Product_ID] INT NULL;
    PRINT 'Reservation_Product_Charges.Product_ID -> NULLable (รองรับค่ากิจกรรม)';
END
GO

IF COL_LENGTH('Reservation_Product_Charges', 'Activity_Booking_ID') IS NULL
BEGIN
    ALTER TABLE [dbo].[Reservation_Product_Charges] ADD Activity_Booking_ID BIGINT NULL;
    PRINT 'Added Reservation_Product_Charges.Activity_Booking_ID';
END
GO

-- ── 5) ตัวอย่างกิจกรรมที่ต้องจองเวลา (เพิ่มเฉพาะถ้ายังไม่มี) ────────────────────────
IF NOT EXISTS (SELECT 1 FROM Property_Activities WHERE ActivityName = N'โต๊ะปิงปอง')
BEGIN
    INSERT INTO Property_Activities
        (ActivityName, ShortDescription, Description, Category, Price, PricingMode, IsBookable,
         Capacity, OpenTime, CloseTime, SlotMinutes, MaxSlotsPerBooking, AdvanceBookingDays,
         MaxParticipants, Duration, Location, IconClass, Rules, DisplayOrder, IsActive)
    VALUES
        (N'โต๊ะปิงปอง', N'จองเป็นรายชั่วโมง มีให้บริการ 2 โต๊ะ',
         N'โต๊ะปิงปองมาตรฐาน พร้อมไม้และลูกปิงปอง จองล่วงหน้าได้ผ่าน Guest Portal เลือกช่วงเวลาที่ต้องการ',
         'ON_PROPERTY', 100, 'PER_HOUR', 1,
         2, '08:00', '21:00', 60, 3, 14,
         4, N'รายชั่วโมง', N'บริเวณส่วนกลาง', 'fa-table-tennis-paddle-ball',
         N'• กรุณามาตรงเวลา หากสายเกิน 15 นาทีถือว่าสละสิทธิ์' + CHAR(13) + CHAR(10) +
         N'• กรุณาเก็บอุปกรณ์เข้าที่หลังใช้งาน' + CHAR(13) + CHAR(10) +
         N'• ยกเลิกฟรีก่อนเวลาจอง 1 ชั่วโมง',
         10, 1);
    PRINT 'Seeded sample bookable activity: โต๊ะปิงปอง';
END
GO

-- กิจกรรมเดิมที่มีอยู่: ตั้งค่าเริ่มต้นให้สมเหตุสมผล (ฟรี ไม่ต้องจอง)
UPDATE Property_Activities
   SET PricingMode = CASE WHEN ISNULL(Price, 0) > 0 THEN 'PER_SESSION' ELSE 'FREE' END
 WHERE PricingMode = 'FREE' AND ISNULL(Price, 0) > 0;
GO

SELECT ID, ActivityName, Category, IsBookable, PricingMode, Price, Capacity,
       OpenTime, CloseTime, SlotMinutes, ShowOnWebsite, ShowInPortal, IsActive
  FROM Property_Activities
 ORDER BY Category, DisplayOrder, ActivityName;
GO
