-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 42: QR สั่งอาหาร/เครื่องดื่ม แยกโต๊ะ/โซน (Dining QR Ordering)
-- ════════════════════════════════════════════════════════════════════════════
-- โมดูล Dining แยกจากรูมเซอร์วิส/POS ทั้งหมด (ไม่แตะ flow เดิม) — ใช้ซ้ำ Product/Product_Images (เมนู),
-- ตรวจสลิป (PHASE19_34/36), แคตตาล็อกแหล่งเงิน (Account_Paid_How), สมุดขายรายช่องทาง (PHASE19_32),
-- Notify / NotificationService / กล่องแชทรวม (OmniChannel).
--
-- 1) Dining_Zone              โซน (ริมน้ำ/ในร้าน/สระ) — เวลาเปิด-ปิด, บังคับเปิด/ปิด, วิธีจ่ายที่อนุญาต, สถานีครัว
-- 2) Dining_Table             โต๊ะในโซน — QR_Token (32 ไบต์ CSPRNG base64url, หมุนได้ → QR_Version), สถานะ, session ปัจจุบัน
-- 3) Dining_Table_Session     "โต๊ะที่กำลังนั่ง" 1 แถว = 1 รอบลูกค้า (Session_Token = cookie ของแขก, คนละตัวกับ QR_Token)
--                             สั่งได้หลายรอบ (หลาย Dining_Order) จนกด "เช็คบิล" → BILL_REQUESTED → ปิดโต๊ะ → CLOSED
-- 4) Dining_Order             ออเดอร์ 1 รอบ (Order_No DN{yyMMdd}-#### รายวัน, Round_No ของ session)
--                             Order_Status NEW|ACCEPTED|PREPARING|SERVED|CANCELLED · Payment_Status UNPAID|PAID|CHARGED|REFUNDED
--                             (รอพนักงานตรวจสลิป = ยัง UNPAID แต่มี Pending_Payment_ID)
--                             ราคารวม VAT แล้ว ไม่มี service charge/ทิป (คำตัดสินเจ้าของ 29 ต.ค. 2026) — Vat_Amount = ถอด 7% เมื่อ Use_Vat
-- 5) Dining_Order_Item        snapshot รายการ (ชื่อ/ราคา/ตัวเลือก/หมายเหตุ ณ เวลาสั่ง) + Track_Stock (ตัดสต๊อกตอนจ่ายสำเร็จ)
-- 6) Dining_Menu_Category / Dining_Menu_Item / Dining_Option_Group / Dining_Option / Dining_Menu_Item_Option
--                             เมนู QR = Product ที่ติ๊ก Is_Dining + หมวด/ชื่อแสดง/sold-out/โซนที่ขาย/ตัวเลือก (ราคาเพิ่ม)
-- 7) Dining_Payment           การชำระ 1 แถวต่อการจ่าย (ต่อ "บิล" ของ session ไม่ใช่ต่อออเดอร์)
--                             Method CASH|TRANSFER|ROOM_CHARGE · Status CONFIRMED|PENDING_SLIP|REJECTED|VOID
--                             Covers_Order_Ids = ออเดอร์ที่ปิดด้วยการจ่ายนี้ (CSV) · Acct_Post_Ref = DNP-{id} (สมุดขาย) / RC:{ref} (ลงบิลห้อง)
-- 8) Dining_Room_Charge_Attempt  ความพยายามยืนยันเบอร์โทรตอนลงบิลห้อง (ผิดครบ Dining_Room_Charge_Max_Fail ต่อ session = ล็อก 15 นาที)
-- 9) Dining_Event_Log         เหตุการณ์ (NEW_ORDER|STATUS|PAID|CALL_STAFF|BILL_REQUEST|QR_ROTATED|SESSION_OPEN|SESSION_CLOSE|…)
--
-- Seeds: Accounting_Account_Mapping DINING_REVENUE (ไม่ผูก → PRODUCT_REVENUE) · Perm OPS_DINING (ทุกกลุ่มมาตรฐาน)
--        · Notification_Rules ORDER_DINING / ORDER_DINING_STATUS / DINING_CALL_STAFF / DINING_BILL (Telegram เปิด)
--        · OmniChannel_Channels DINING (INTERNAL) · System_Config Feature_Dining=0 + ค่าตั้ง Dining_*
--        · Sales_Doc_Policy: **ไม่ seed แถว DINING** — ตกไปใช้ค่ากลาง (*,CASH)=DAILY_ROLLUP / (*,*)=IMMEDIATE ตาม PHASE19_29
--          (ผู้ทำบัญชีเพิ่มกฎเฉพาะช่องทางได้ที่ Admin/Settings/SalesDocPolicy)
--
-- idempotent — รันซ้ำได้ ไม่ลบข้อมูล ไม่ทับค่าที่ผู้ดูแลแก้แล้ว. ยังไม่รัน = โค้ด Dining ตรวจ OBJECT_ID แล้วทำงานแบบปิดฟีเจอร์
-- หลังรัน: recycle App Pool (cache โครงสร้าง 60 วินาที) แล้วเปิด Feature_Dining ที่ศูนย์ตั้งค่า
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── 1) โซน ──────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Dining_Zone', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Zone (
        ID                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dining_Zone PRIMARY KEY,
        Code              NVARCHAR(20)   NOT NULL,
        Name_TH           NVARCHAR(100)  NOT NULL,
        Sort_Order        INT            NOT NULL CONSTRAINT DF_Dining_Zone_Sort DEFAULT (0),
        Is_Active         BIT            NOT NULL CONSTRAINT DF_Dining_Zone_Active DEFAULT (1),
        Open_Time         TIME           NULL,
        Close_Time        TIME           NULL,
        Manual_Mode       NVARCHAR(10)   NOT NULL CONSTRAINT DF_Dining_Zone_Mode DEFAULT ('AUTO'),   -- AUTO | OPEN | CLOSED
        Closed_Message    NVARCHAR(300)  NULL,
        Allow_Cash        BIT            NOT NULL CONSTRAINT DF_Dining_Zone_Cash DEFAULT (1),
        Allow_Transfer    BIT            NOT NULL CONSTRAINT DF_Dining_Zone_Transfer DEFAULT (1),
        Allow_Room_Charge BIT            NOT NULL CONSTRAINT DF_Dining_Zone_RoomCharge DEFAULT (0),
        Kitchen_Station   NVARCHAR(50)   NULL,
        Created_At        DATETIME       NOT NULL CONSTRAINT DF_Dining_Zone_Created DEFAULT (GETDATE()),
        Updated_At        DATETIME       NULL,
        Updated_By        NVARCHAR(100)  NULL,
        CONSTRAINT UQ_Dining_Zone_Code UNIQUE (Code)
    );
    PRINT N'สร้างตาราง Dining_Zone';
END
ELSE PRINT N'มีตาราง Dining_Zone อยู่แล้ว — ไม่แตะ';
GO

-- ── 2) โต๊ะ ─────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Dining_Table', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Table (
        ID                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dining_Table PRIMARY KEY,
        Zone_ID            INT            NOT NULL CONSTRAINT FK_Dining_Table_Zone REFERENCES dbo.Dining_Zone (ID),
        Code               NVARCHAR(20)   NOT NULL,
        Name_TH            NVARCHAR(100)  NULL,
        Seats              TINYINT        NOT NULL CONSTRAINT DF_Dining_Table_Seats DEFAULT (4),
        Sort_Order         INT            NOT NULL CONSTRAINT DF_Dining_Table_Sort DEFAULT (0),
        QR_Token           NVARCHAR(64)   NOT NULL,
        QR_Issued_At       DATETIME       NOT NULL CONSTRAINT DF_Dining_Table_QrAt DEFAULT (GETDATE()),
        QR_Version         INT            NOT NULL CONSTRAINT DF_Dining_Table_QrVer DEFAULT (1),
        Status             NVARCHAR(20)   NOT NULL CONSTRAINT DF_Dining_Table_Status DEFAULT ('FREE'),  -- FREE|OCCUPIED|BILL_REQUESTED|NEEDS_STAFF|DISABLED
        Current_Session_ID BIGINT         NULL,
        Is_Active          BIT            NOT NULL CONSTRAINT DF_Dining_Table_Active DEFAULT (1),
        Note               NVARCHAR(300)  NULL,
        Created_At         DATETIME       NOT NULL CONSTRAINT DF_Dining_Table_Created DEFAULT (GETDATE()),
        Updated_At         DATETIME       NULL,
        CONSTRAINT UQ_Dining_Table_Token UNIQUE (QR_Token),
        CONSTRAINT UQ_Dining_Table_ZoneCode UNIQUE (Zone_ID, Code)
    );
    CREATE INDEX IX_Dining_Table_Zone ON dbo.Dining_Table (Zone_ID, Sort_Order);
    PRINT N'สร้างตาราง Dining_Table';
END
ELSE PRINT N'มีตาราง Dining_Table อยู่แล้ว — ไม่แตะ';
GO

-- ── 3) session ต่อโต๊ะ ───────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Dining_Table_Session', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Table_Session (
        ID                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dining_Table_Session PRIMARY KEY,
        Table_ID          INT            NOT NULL CONSTRAINT FK_Dining_Session_Table REFERENCES dbo.Dining_Table (ID),
        Session_Token     NVARCHAR(64)   NOT NULL,
        Opened_At         DATETIME       NOT NULL CONSTRAINT DF_Dining_Session_Opened DEFAULT (GETDATE()),
        Closed_At         DATETIME       NULL,
        Closed_By         INT            NULL,
        Close_Reason      NVARCHAR(300)  NULL,
        Guest_Name        NVARCHAR(100)  NULL,
        Guest_Phone       NVARCHAR(30)   NULL,
        Reservation_ID    INT            NULL,
        Pax               TINYINT        NULL,
        Status            NVARCHAR(20)   NOT NULL CONSTRAINT DF_Dining_Session_Status DEFAULT ('OPEN'),  -- OPEN|BILL_REQUESTED|CLOSED
        Device_Hash       NVARCHAR(64)   NULL,
        Client_Ip         NVARCHAR(45)   NULL,
        Last_Seen_At      DATETIME       NOT NULL CONSTRAINT DF_Dining_Session_Seen DEFAULT (GETDATE()),
        Bill_Requested_At DATETIME       NULL,
        Last_Call_At      DATETIME       NULL,
        Room_Charge_Locked_Until DATETIME NULL,
        CONSTRAINT UQ_Dining_Session_Token UNIQUE (Session_Token)
    );
    CREATE INDEX IX_Dining_Session_Table_Status ON dbo.Dining_Table_Session (Table_ID, Status);
    CREATE INDEX IX_Dining_Session_Status_Seen ON dbo.Dining_Table_Session (Status, Last_Seen_At);
    PRINT N'สร้างตาราง Dining_Table_Session';
END
ELSE PRINT N'มีตาราง Dining_Table_Session อยู่แล้ว — ไม่แตะ';
GO

-- ── 4) ออเดอร์ ───────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Dining_Order', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Order (
        ID                 BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dining_Order PRIMARY KEY,
        Order_No           NVARCHAR(20)   NOT NULL,
        Session_ID         BIGINT         NOT NULL CONSTRAINT FK_Dining_Order_Session REFERENCES dbo.Dining_Table_Session (ID),
        Table_ID           INT            NOT NULL,
        Zone_ID            INT            NOT NULL,
        Round_No           INT            NOT NULL CONSTRAINT DF_Dining_Order_Round DEFAULT (1),
        Order_Status       NVARCHAR(20)   NOT NULL CONSTRAINT DF_Dining_Order_Status DEFAULT ('NEW'),   -- NEW|ACCEPTED|PREPARING|SERVED|CANCELLED
        Subtotal           DECIMAL(10,2)  NOT NULL CONSTRAINT DF_Dining_Order_Sub DEFAULT (0),
        Discount           DECIMAL(10,2)  NOT NULL CONSTRAINT DF_Dining_Order_Disc DEFAULT (0),
        Total_Amount       DECIMAL(10,2)  NOT NULL CONSTRAINT DF_Dining_Order_Total DEFAULT (0),
        Vat_Amount         DECIMAL(10,2)  NOT NULL CONSTRAINT DF_Dining_Order_Vat DEFAULT (0),
        Payment_Status     NVARCHAR(20)   NOT NULL CONSTRAINT DF_Dining_Order_Pay DEFAULT ('UNPAID'),  -- UNPAID|PAID|CHARGED|REFUNDED
        Payment_ID         BIGINT         NULL,
        Pending_Payment_ID BIGINT         NULL,
        Guest_Note         NVARCHAR(500)  NULL,
        Guest_Name         NVARCHAR(100)  NULL,
        Guest_Phone        NVARCHAR(30)   NULL,
        Cancel_Reason      NVARCHAR(300)  NULL,
        Accepted_By        INT            NULL,
        Accepted_At        DATETIME       NULL,
        Served_By          INT            NULL,
        Served_At          DATETIME       NULL,
        Cancelled_By       INT            NULL,
        Cancelled_At       DATETIME       NULL,
        Source_Channel     NVARCHAR(10)   NOT NULL CONSTRAINT DF_Dining_Order_Src DEFAULT ('QR'),      -- QR | STAFF
        Client_Ip          NVARCHAR(45)   NULL,
        Idempotency_Key    NVARCHAR(64)   NULL,
        Stock_Posted       BIT            NOT NULL CONSTRAINT DF_Dining_Order_Stock DEFAULT (0),
        Created_At         DATETIME       NOT NULL CONSTRAINT DF_Dining_Order_Created DEFAULT (GETDATE()),
        Updated_At         DATETIME       NULL,
        CONSTRAINT UQ_Dining_Order_No UNIQUE (Order_No)
    );
    CREATE UNIQUE INDEX UX_Dining_Order_Idem ON dbo.Dining_Order (Idempotency_Key) WHERE Idempotency_Key IS NOT NULL;
    CREATE INDEX IX_Dining_Order_Session ON dbo.Dining_Order (Session_ID, Order_Status);
    CREATE INDEX IX_Dining_Order_Status_Created ON dbo.Dining_Order (Order_Status, Created_At DESC);
    CREATE INDEX IX_Dining_Order_Pay ON dbo.Dining_Order (Payment_Status, Session_ID);
    CREATE INDEX IX_Dining_Order_Created ON dbo.Dining_Order (Created_At DESC);
    PRINT N'สร้างตาราง Dining_Order';
END
ELSE PRINT N'มีตาราง Dining_Order อยู่แล้ว — ไม่แตะ';
GO

-- ── 5) รายการในออเดอร์ ───────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Dining_Order_Item', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Order_Item (
        ID             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dining_Order_Item PRIMARY KEY,
        Order_ID       BIGINT         NOT NULL CONSTRAINT FK_Dining_Item_Order REFERENCES dbo.Dining_Order (ID) ON DELETE CASCADE,
        Product_ID     INT            NOT NULL,
        Product_Name   NVARCHAR(200)  NOT NULL,
        Qty            DECIMAL(9,2)   NOT NULL,
        Unit_Price     DECIMAL(10,2)  NOT NULL,
        Options_Json   NVARCHAR(MAX)  NULL,
        Options_Price  DECIMAL(10,2)  NOT NULL CONSTRAINT DF_Dining_Item_OptPrice DEFAULT (0),
        Line_Total     DECIMAL(10,2)  NOT NULL,
        Item_Note      NVARCHAR(300)  NULL,
        Track_Stock    BIT            NOT NULL CONSTRAINT DF_Dining_Item_Track DEFAULT (0),
        Kitchen_Status NVARCHAR(20)   NOT NULL CONSTRAINT DF_Dining_Item_KStatus DEFAULT ('QUEUED'),  -- QUEUED|COOKING|READY|SERVED|VOID
        Station        NVARCHAR(50)   NULL
    );
    CREATE INDEX IX_Dining_Item_Order ON dbo.Dining_Order_Item (Order_ID);
    CREATE INDEX IX_Dining_Item_Product ON dbo.Dining_Order_Item (Product_ID);
    PRINT N'สร้างตาราง Dining_Order_Item';
END
ELSE PRINT N'มีตาราง Dining_Order_Item อยู่แล้ว — ไม่แตะ';
GO

-- ── 6) เมนู ─────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Dining_Menu_Category', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Menu_Category (
        ID         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dining_Menu_Category PRIMARY KEY,
        Name_TH    NVARCHAR(100) NOT NULL,
        Name_EN    NVARCHAR(100) NULL,
        Icon       NVARCHAR(50)  NULL,
        Sort_Order INT           NOT NULL CONSTRAINT DF_Dining_Cat_Sort DEFAULT (0),
        Is_Active  BIT           NOT NULL CONSTRAINT DF_Dining_Cat_Active DEFAULT (1)
    );
    PRINT N'สร้างตาราง Dining_Menu_Category';
END
ELSE PRINT N'มีตาราง Dining_Menu_Category อยู่แล้ว — ไม่แตะ';
GO

IF OBJECT_ID(N'dbo.Dining_Menu_Item', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Menu_Item (
        Product_ID       INT            NOT NULL CONSTRAINT PK_Dining_Menu_Item PRIMARY KEY,
        Is_Dining        BIT            NOT NULL CONSTRAINT DF_Dining_Menu_IsDining DEFAULT (1),
        Category_ID      INT            NULL,
        Display_Name_TH  NVARCHAR(200)  NULL,
        Display_Name_EN  NVARCHAR(200)  NULL,
        Description_TH   NVARCHAR(500)  NULL,
        Is_Sold_Out      BIT            NOT NULL CONSTRAINT DF_Dining_Menu_SoldOut DEFAULT (0),
        Sold_Out_Until   DATETIME       NULL,
        Prep_Minutes     TINYINT        NULL,
        Allergen_Tags    NVARCHAR(200)  NULL,
        Sort_Order       INT            NOT NULL CONSTRAINT DF_Dining_Menu_Sort DEFAULT (0),
        Available_Zones  NVARCHAR(200)  NULL,     -- CSV รหัสโซน; NULL/ว่าง = ทุกโซน
        Track_Stock      BIT            NOT NULL CONSTRAINT DF_Dining_Menu_Track DEFAULT (0),
        Is_Recommended   BIT            NOT NULL CONSTRAINT DF_Dining_Menu_Rec DEFAULT (0),
        Updated_At       DATETIME       NULL,
        Updated_By       NVARCHAR(100)  NULL
    );
    CREATE INDEX IX_Dining_Menu_Cat ON dbo.Dining_Menu_Item (Category_ID, Sort_Order);
    PRINT N'สร้างตาราง Dining_Menu_Item';
END
ELSE PRINT N'มีตาราง Dining_Menu_Item อยู่แล้ว — ไม่แตะ';
GO

IF OBJECT_ID(N'dbo.Dining_Option_Group', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Option_Group (
        ID          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dining_Option_Group PRIMARY KEY,
        Name_TH     NVARCHAR(100) NOT NULL,
        Min_Select  INT           NOT NULL CONSTRAINT DF_Dining_OG_Min DEFAULT (0),
        Max_Select  INT           NOT NULL CONSTRAINT DF_Dining_OG_Max DEFAULT (1),
        Is_Required BIT           NOT NULL CONSTRAINT DF_Dining_OG_Req DEFAULT (0),
        Sort_Order  INT           NOT NULL CONSTRAINT DF_Dining_OG_Sort DEFAULT (0),
        Is_Active   BIT           NOT NULL CONSTRAINT DF_Dining_OG_Active DEFAULT (1)
    );
    PRINT N'สร้างตาราง Dining_Option_Group';
END
ELSE PRINT N'มีตาราง Dining_Option_Group อยู่แล้ว — ไม่แตะ';
GO

IF OBJECT_ID(N'dbo.Dining_Option', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Option (
        ID          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dining_Option PRIMARY KEY,
        Group_ID    INT           NOT NULL CONSTRAINT FK_Dining_Option_Group REFERENCES dbo.Dining_Option_Group (ID) ON DELETE CASCADE,
        Name_TH     NVARCHAR(100) NOT NULL,
        Price_Delta DECIMAL(10,2) NOT NULL CONSTRAINT DF_Dining_Opt_Delta DEFAULT (0),
        Sort_Order  INT           NOT NULL CONSTRAINT DF_Dining_Opt_Sort DEFAULT (0),
        Is_Active   BIT           NOT NULL CONSTRAINT DF_Dining_Opt_Active DEFAULT (1)
    );
    CREATE INDEX IX_Dining_Option_Group ON dbo.Dining_Option (Group_ID, Sort_Order);
    PRINT N'สร้างตาราง Dining_Option';
END
ELSE PRINT N'มีตาราง Dining_Option อยู่แล้ว — ไม่แตะ';
GO

IF OBJECT_ID(N'dbo.Dining_Menu_Item_Option', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Menu_Item_Option (
        Product_ID INT NOT NULL,
        Group_ID   INT NOT NULL CONSTRAINT FK_Dining_MIO_Group REFERENCES dbo.Dining_Option_Group (ID) ON DELETE CASCADE,
        CONSTRAINT PK_Dining_Menu_Item_Option PRIMARY KEY (Product_ID, Group_ID)
    );
    PRINT N'สร้างตาราง Dining_Menu_Item_Option';
END
ELSE PRINT N'มีตาราง Dining_Menu_Item_Option อยู่แล้ว — ไม่แตะ';
GO

-- ── 7) การชำระ ───────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Dining_Payment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Payment (
        ID                 BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dining_Payment PRIMARY KEY,
        Session_ID         BIGINT         NOT NULL CONSTRAINT FK_Dining_Payment_Session REFERENCES dbo.Dining_Table_Session (ID),
        Method             NVARCHAR(20)   NOT NULL,   -- CASH | TRANSFER | ROOM_CHARGE
        Status             NVARCHAR(20)   NOT NULL CONSTRAINT DF_Dining_Payment_Status DEFAULT ('CONFIRMED'),  -- CONFIRMED|PENDING_SLIP|REJECTED|VOID
        Amount             DECIMAL(10,2)  NOT NULL,
        Paid_How_ID        INT            NULL,
        Paid_How_Name      NVARCHAR(150)  NULL,
        Paid_At            DATETIME       NULL,
        Paid_By            INT            NULL,
        Slip_Path          NVARCHAR(500)  NULL,
        Slip_Verify_Status NVARCHAR(30)   NULL,       -- VERIFIED|MISMATCH|DUPLICATE|UNVERIFIED_BYPASS|CONTACT_ADMIN|MANUAL|WRONG_RECEIVER
        Slip_Bank_Amount   DECIMAL(10,2)  NULL,
        Slip_Trans_Ref     NVARCHAR(60)   NULL,
        Slip_Note          NVARCHAR(500)  NULL,
        Room_Charge_Ref    NVARCHAR(60)   NULL,
        Reservation_ID     INT            NULL,
        Covers_Order_Ids   NVARCHAR(400)  NULL,
        Acct_Post_Ref      NVARCHAR(60)   NULL,
        Idempotency_Key    NVARCHAR(64)   NULL,
        Created_At         DATETIME       NOT NULL CONSTRAINT DF_Dining_Payment_Created DEFAULT (GETDATE()),
        Updated_At         DATETIME       NULL
    );
    CREATE UNIQUE INDEX UX_Dining_Payment_Idem ON dbo.Dining_Payment (Idempotency_Key) WHERE Idempotency_Key IS NOT NULL;
    CREATE INDEX IX_Dining_Payment_Session ON dbo.Dining_Payment (Session_ID, Status);
    CREATE INDEX IX_Dining_Payment_Post ON dbo.Dining_Payment (Acct_Post_Ref, Status);
    PRINT N'สร้างตาราง Dining_Payment';
END
ELSE PRINT N'มีตาราง Dining_Payment อยู่แล้ว — ไม่แตะ';
GO

-- ── 8) ความพยายามลงบิลห้อง (ยืนยันเบอร์โทร) ───────────────────────────────────
IF OBJECT_ID(N'dbo.Dining_Room_Charge_Attempt', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Room_Charge_Attempt (
        ID             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dining_RC_Attempt PRIMARY KEY,
        Session_ID     BIGINT        NOT NULL,
        Reservation_ID INT           NULL,
        Ok             BIT           NOT NULL,
        Phone_Masked   NVARCHAR(20)  NULL,
        Client_Ip      NVARCHAR(45)  NULL,
        Attempted_At   DATETIME      NOT NULL CONSTRAINT DF_Dining_RC_At DEFAULT (GETDATE())
    );
    CREATE INDEX IX_Dining_RC_Session ON dbo.Dining_Room_Charge_Attempt (Session_ID, Attempted_At DESC);
    PRINT N'สร้างตาราง Dining_Room_Charge_Attempt';
END
ELSE PRINT N'มีตาราง Dining_Room_Charge_Attempt อยู่แล้ว — ไม่แตะ';
GO

-- ── 9) บันทึกเหตุการณ์ ───────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Dining_Event_Log', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dining_Event_Log (
        ID         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Dining_Event_Log PRIMARY KEY,
        Order_ID   BIGINT        NULL,
        Session_ID BIGINT        NULL,
        Table_ID   INT           NULL,
        Event      NVARCHAR(30)  NOT NULL,
        Actor      NVARCHAR(100) NULL,
        Detail     NVARCHAR(500) NULL,
        Created_At DATETIME      NOT NULL CONSTRAINT DF_Dining_Event_Created DEFAULT (GETDATE())
    );
    CREATE INDEX IX_Dining_Event_Session ON dbo.Dining_Event_Log (Session_ID, Created_At DESC);
    CREATE INDEX IX_Dining_Event_Created ON dbo.Dining_Event_Log (Created_At DESC);
    PRINT N'สร้างตาราง Dining_Event_Log';
END
ELSE PRINT N'มีตาราง Dining_Event_Log อยู่แล้ว — ไม่แตะ';
GO

-- ── 10) mapping รายได้ (ไม่ผูกรหัส = ถอยไป PRODUCT_REVENUE ตาม RevenueChain) ─────
IF OBJECT_ID(N'dbo.Accounting_Account_Mapping', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = 'DINING_REVENUE')
    BEGIN
        INSERT INTO dbo.Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
        VALUES ('DINING_REVENUE', N'รายได้ร้านอาหาร (QR สั่งอาหาร — ไม่ผูก = รายได้ขายสินค้า)', '', 'REVENUE', 1);
        PRINT N'เพิ่ม mapping DINING_REVENUE';
    END
END
ELSE PRINT N'ไม่มีตาราง Accounting_Account_Mapping — ข้ามการเพิ่ม mapping';
GO

-- ── 11) สิทธิ์โมดูล OPS_DINING (ทุกกลุ่มมาตรฐานเปิด เหมือน OPS_ROOMSERVICE) ────
IF OBJECT_ID(N'dbo.Permission_Groups', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Permission_Group_Modules', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.Permission_Group_Modules (Group_ID, Module_Code, Can_View, Can_Access)
    SELECT g.ID, 'OPS_DINING', 1, 1
      FROM dbo.Permission_Groups g
     WHERE g.Is_System = 1
       AND NOT EXISTS (SELECT 1 FROM dbo.Permission_Group_Modules p WHERE p.Group_ID = g.ID AND p.Module_Code = 'OPS_DINING');
    PRINT N'สิทธิ์ OPS_DINING: เพิ่มให้กลุ่มมาตรฐาน ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' กลุ่ม';
END
ELSE PRINT N'ไม่มีตารางกลุ่มสิทธิ์ (PHASE18_23) — ใช้สิทธิ์ตาม Role เดิม';
GO

-- ── 12) กฎแจ้งเตือน (Telegram เปิดเป็นค่าเริ่มต้น — เจ้าของต้องการทุกเหตุการณ์) ─────
IF OBJECT_ID(N'dbo.Notification_Rules', N'U') IS NOT NULL
BEGIN
    DECLARE @seed TABLE (Code NVARCHAR(60), TG BIT, LN BIT);
    INSERT INTO @seed (Code, TG, LN) VALUES
        ('ORDER_DINING',        1, 0),   -- ออเดอร์ใหม่จากโต๊ะ
        ('ORDER_DINING_STATUS', 1, 0),   -- รับ/กำลังทำ/เสิร์ฟ/ยกเลิก/รับเงิน/สลิป
        ('DINING_CALL_STAFF',   1, 0),   -- ลูกค้าเรียกพนักงาน
        ('DINING_BILL',         1, 0);   -- เช็คบิล / ลงบิลห้อง / ปิดโต๊ะ

    INSERT INTO dbo.Notification_Rules (Event_Code, Channel, Enabled, Modified_Date)
    SELECT s.Code, 'TELEGRAM', s.TG, GETDATE() FROM @seed s
     WHERE NOT EXISTS (SELECT 1 FROM dbo.Notification_Rules r WHERE r.Event_Code = s.Code AND r.Channel = 'TELEGRAM');
    PRINT N'เพิ่มกฎ Telegram: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' เหตุการณ์';

    INSERT INTO dbo.Notification_Rules (Event_Code, Channel, Enabled, Modified_Date)
    SELECT s.Code, 'LINE', s.LN, GETDATE() FROM @seed s
     WHERE NOT EXISTS (SELECT 1 FROM dbo.Notification_Rules r WHERE r.Event_Code = s.Code AND r.Channel = 'LINE');
    PRINT N'เพิ่มกฎ LINE: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' เหตุการณ์';
END
ELSE PRINT N'ไม่มีตาราง Notification_Rules (PHASE19_07) — Notify ใช้ค่าตั้งต้น (เปิด Telegram)';
GO

-- ── 13) ช่องทางแชทรวม DINING (1 conversation ต่อโต๊ะ: platformUserId = table_{id}) ──
IF OBJECT_ID(N'dbo.OmniChannel_Channels', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.OmniChannel_Channels WHERE ChannelCode = 'DINING')
    BEGIN
        INSERT INTO dbo.OmniChannel_Channels (ChannelCode, ChannelName, ChannelType, IconClass, BrandColor, IsEnabled)
        VALUES ('DINING', N'QR สั่งอาหาร (โต๊ะ)', 'INTERNAL', 'fas fa-utensils', '#e65100', 1);
        PRINT N'เพิ่มช่องทางแชท DINING';
    END
END
ELSE PRINT N'ไม่มีตาราง OmniChannel_Channels (PHASE15) — ข้ามการเพิ่มช่องทางแชท';
GO

-- ── 14) ค่าตั้ง (System_Config — ไม่ทับค่าที่มีอยู่) ──────────────────────────
IF OBJECT_ID(N'dbo.System_Config', N'U') IS NOT NULL
BEGIN
    DECLARE @cfg TABLE (K NVARCHAR(100), V NVARCHAR(200), C NVARCHAR(50), N NVARCHAR(200), D NVARCHAR(500), S BIT, T NVARCHAR(20), O INT);
    INSERT INTO @cfg (K, V, C, N, D, S, T, O) VALUES
      (N'Feature_Dining',              N'0',  N'FEATURE',      N'🍽️ QR สั่งอาหาร (โต๊ะ/โซน)',
       N'ค่าเริ่มต้น: ปิด — เปิดหลังตั้งโซน/โต๊ะ/เมนูที่ Admin/Dining แล้ว', 0, N'bool', 160),
      (N'Dining_Session_Idle_Minutes', N'30', N'QR สั่งอาหาร', N'ปิดโต๊ะอัตโนมัติเมื่อไม่มีกิจกรรม (นาที)',
       N'เมื่อทุกออเดอร์จ่ายครบ/ยกเลิกหมด และลูกค้าไม่มีกิจกรรมนานเท่านี้ → ปิด session (ลิงก์มือถือเดิมใช้ต่อไม่ได้)', 0, N'number', 10),
      (N'Dining_Min_Order',            N'0',  N'QR สั่งอาหาร', N'ยอดสั่งขั้นต่ำต่อรอบ (บาท)', N'0 = ไม่มีขั้นต่ำ', 0, N'number', 20),
      (N'Dining_Guest_Poll_Sec',       N'5',  N'QR สั่งอาหาร', N'หน้าลูกค้าตรวจสถานะทุก (วินาที)', N'หน้าสถานะออเดอร์ของลูกค้า', 0, N'number', 30),
      (N'Dining_Staff_Poll_Sec',       N'3',  N'QR สั่งอาหาร', N'กระดานพนักงานตรวจออเดอร์ใหม่ทุก (วินาที)', N'เสียง + ป้ายออเดอร์ใหม่', 0, N'number', 40),
      (N'Dining_Room_Charge_Max_Fail', N'5',  N'QR สั่งอาหาร', N'ยืนยันเบอร์โทรลงบิลห้องผิดได้กี่ครั้ง', N'ครบแล้วล็อก 15 นาทีต่อโต๊ะ + แจ้ง Telegram', 0, N'number', 50);

    INSERT INTO dbo.System_Config (ConfigKey, ConfigValue, Category, DisplayName, [Description], IsSecret, InputType, DisplayOrder, ModifiedDate)
    SELECT c.K, c.V, c.C, c.N, c.D, c.S, c.T, CASE WHEN c.C = N'FEATURE' THEN c.O ELSE 950 + c.O END, GETDATE()
      FROM @cfg c
     WHERE NOT EXISTS (SELECT 1 FROM dbo.System_Config x WHERE x.ConfigKey = c.K);
    PRINT N'ค่าตั้ง QR สั่งอาหาร: เพิ่ม ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' ค่า (ค่าเดิมไม่แตะ)';
END
ELSE PRINT N'⚠ ไม่มี System_Config (PHASE18_18) — ตั้งค่าผ่าน Web.config appSettings ได้ (Feature_Dining ปิดเป็นค่าเริ่มต้นอยู่แล้ว)';
GO

-- ── 15) นโยบายเอกสารขาย: ไม่ seed แถว DINING โดยตั้งใจ ──────────────────────
-- ช่องทาง DINING ตกไปใช้กฎกลางของ PHASE19_29: (*,CASH)=DAILY_ROLLUP (ใบสรุป DNDAY-{yyyyMMdd}-{paidHowId})
-- และ (*,*)=IMMEDIATE (โอน/อื่น ๆ ออกเอกสารรายใบ DNP-{paymentId}). ผู้ทำบัญชีเพิ่มกฎ (DINING, ชนิด) เองได้
-- ที่ Admin/Settings/SalesDocPolicy เมื่อต้องการให้ร้านอาหารต่างจากค่ากลาง.

PRINT N'PHASE19_42 เสร็จ — ตั้งโซน/โต๊ะ/เมนูที่ Admin/Dining แล้วเปิด Feature_Dining · ผูก mapping DINING_REVENUE ที่หน้า Accounting Integration (ไม่ผูกก็ใช้ PRODUCT_REVENUE)';
GO
