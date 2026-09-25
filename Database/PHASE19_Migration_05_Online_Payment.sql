-- ============================================================================
-- PHASE19 Migration 05 — รับชำระเงินออนไลน์ (QR เดิม + บัตรเครดิตผ่าน Payso)
-- ============================================================================
-- เป้าหมาย: ให้ลูกค้าเลือกได้ว่าจะ "สแกน QR โอนแล้วแนบสลิป" แบบเดิม
--           หรือ "จ่ายด้วยบัตรเครดิต/ออนไลน์" ผ่านเกตเวย์ (Payso)
--
-- ⚠ ปลอดภัยกับระบบเดิม 100%:
--   • ฟีเจอร์ปิดเป็นค่าเริ่มต้น (Feature_OnlinePayment = 0)
--   • ปิดอยู่ = ไม่มีอะไรเปลี่ยน — ตัวเลือกจ่ายออนไลน์ไม่โผล่, หน้าเดิมทำงานเหมือนเดิมทุกอย่าง
--   • ไม่แก้/ไม่ลบตารางเดิมแม้แต่คอลัมน์เดียว — เพิ่มตารางใหม่ล้วน
--   • รันซ้ำได้ (idempotent)
--
-- ต้องรัน PHASE18_Migration_23 (ระบบกลุ่มสิทธิ์) มาก่อน ถ้าต้องการสิทธิ์แยกโมดูล
-- ============================================================================

SET NOCOUNT ON;
GO

-- ── 1) ตารางตั้งค่าเกตเวย์ ──────────────────────────────────────────────────
-- แยกจาก System_Config เพราะมีคีย์เยอะและเป็นเรื่องการเงินล้วน อยากให้สิทธิ์แยกได้
IF OBJECT_ID('dbo.Payment_Gateway_Config', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payment_Gateway_Config (
        Config_Key      NVARCHAR(80)   NOT NULL PRIMARY KEY,
        Config_Value    NVARCHAR(MAX)  NULL,
        Is_Secret       BIT            NOT NULL DEFAULT 0,
        Category        NVARCHAR(60)   NULL,
        Display_Name    NVARCHAR(200)  NULL,
        Description     NVARCHAR(1000) NULL,
        Input_Type      NVARCHAR(20)   NULL,      -- text / password / bool / number / select / textarea
        Options         NVARCHAR(500)  NULL,      -- ตัวเลือกของ select คั่นด้วย ,
        Display_Order   INT            NOT NULL DEFAULT 100,
        Modified_Date   DATETIME       NULL,
        Modified_By     INT            NULL
    );
    PRINT N'สร้างตาราง Payment_Gateway_Config';
END
ELSE PRINT N'มี Payment_Gateway_Config อยู่แล้ว — ข้าม';
GO

-- ── 2) รายการชำระเงิน (state machine) ───────────────────────────────────────
IF OBJECT_ID('dbo.Payment_Transaction', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payment_Transaction (
        ID                INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Txn_Ref           NVARCHAR(50)   NOT NULL,   -- อ้างอิงฝั่งเรา (ส่งให้เกตเวย์) = กันจ่ายซ้ำ
        Provider          NVARCHAR(30)   NOT NULL,   -- PAYSO / MANUAL_QR
        Method            NVARCHAR(30)   NOT NULL,   -- CARD / QR / INSTALLMENT / MANUAL_QR
        Source_Type       NVARCHAR(30)   NOT NULL,   -- RESERVATION / ACTIVITY / ROOMSERVICE / AMENITY / RECEIPT / OTHER
        Source_ID         NVARCHAR(50)   NULL,
        Amount            DECIMAL(18,2)  NOT NULL,
        Surcharge_Amount  DECIMAL(18,2)  NOT NULL DEFAULT 0,   -- ค่าธรรมเนียมที่บวกให้ลูกค้า (ถ้าตั้งไว้)
        Fee_Amount        DECIMAL(18,2)  NULL,                 -- ค่าธรรมเนียมที่เกตเวย์หัก (ถ้ารู้)
        Currency          NVARCHAR(3)    NOT NULL DEFAULT 'THB',
        [Description]     NVARCHAR(255)  NULL,
        Customer_Name     NVARCHAR(200)  NULL,
        Customer_Phone    NVARCHAR(50)   NULL,
        Customer_Email    NVARCHAR(200)  NULL,
        -- INITIATED = สร้างแล้วยังไม่ส่ง / PENDING = รอลูกค้าจ่าย / PAID / FAILED / EXPIRED / CANCELLED / REFUNDED
        [Status]          NVARCHAR(20)   NOT NULL DEFAULT 'INITIATED',
        Provider_Txn_ID   NVARCHAR(100)  NULL,
        Provider_Ref      NVARCHAR(100)  NULL,
        Card_Brand        NVARCHAR(30)   NULL,
        Card_Last4        NVARCHAR(8)    NULL,
        Payment_Url       NVARCHAR(1000) NULL,
        Qr_Payload        NVARCHAR(MAX)  NULL,
        Expires_At        DATETIME       NULL,
        Paid_At           DATETIME       NULL,
        Fail_Reason       NVARCHAR(500)  NULL,
        Raw_Request       NVARCHAR(MAX)  NULL,
        Raw_Response      NVARCHAR(MAX)  NULL,
        -- ผลลัพธ์ปลายทางหลังจ่ายสำเร็จ (กันทำซ้ำ)
        Applied_At        DATETIME       NULL,      -- บันทึกเข้าระบบเดิมแล้วเมื่อไหร่
        Applied_Note      NVARCHAR(500)  NULL,
        Receipt_ID        NVARCHAR(50)   NULL,
        Created_Date      DATETIME       NOT NULL DEFAULT GETDATE(),
        Updated_Date      DATETIME       NULL,
        Created_By        INT            NULL
    );
    CREATE UNIQUE INDEX UX_Payment_Transaction_Ref ON dbo.Payment_Transaction (Txn_Ref);
    CREATE INDEX IX_Payment_Transaction_Source  ON dbo.Payment_Transaction (Source_Type, Source_ID);
    CREATE INDEX IX_Payment_Transaction_Status  ON dbo.Payment_Transaction ([Status], Created_Date);
    CREATE INDEX IX_Payment_Transaction_PTxn    ON dbo.Payment_Transaction (Provider, Provider_Txn_ID);
    PRINT N'สร้างตาราง Payment_Transaction';
END
ELSE PRINT N'มี Payment_Transaction อยู่แล้ว — ข้าม';
GO

-- ── 3) เหตุการณ์จากเกตเวย์ (webhook/callback) — เก็บดิบไว้ตรวจย้อนหลังเสมอ ──
IF OBJECT_ID('dbo.Payment_Transaction_Event', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payment_Transaction_Event (
        ID              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Txn_ID          INT            NULL,
        Txn_Ref         NVARCHAR(50)   NULL,
        Provider        NVARCHAR(30)   NOT NULL,
        Event_ID        NVARCHAR(120)  NULL,   -- id ของเหตุการณ์ฝั่งเกตเวย์ (ถ้ามี) = กันประมวลผลซ้ำ
        Event_Type      NVARCHAR(60)   NULL,
        Signature_Valid BIT            NULL,
        Remote_IP       NVARCHAR(60)   NULL,
        Raw_Headers     NVARCHAR(MAX)  NULL,
        Raw_Body        NVARCHAR(MAX)  NULL,
        Handled         BIT            NOT NULL DEFAULT 0,
        Handle_Note     NVARCHAR(500)  NULL,
        Created_Date    DATETIME       NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_Payment_Event_Txn ON dbo.Payment_Transaction_Event (Txn_Ref, Created_Date);
    -- กันประมวลผล event เดิมซ้ำ (เกตเวย์ส่งซ้ำเป็นเรื่องปกติ)
    CREATE UNIQUE INDEX UX_Payment_Event_Dedup ON dbo.Payment_Transaction_Event (Provider, Event_ID)
        WHERE Event_ID IS NOT NULL;
    PRINT N'สร้างตาราง Payment_Transaction_Event';
END
ELSE PRINT N'มี Payment_Transaction_Event อยู่แล้ว — ข้าม';
GO

-- ── 4) ค่าตั้งต้น ───────────────────────────────────────────────────────────
-- ⚠ ค่าเส้นทาง/รูปแบบข้อมูลของ Payso ด้านล่าง เป็น "ค่าตั้งต้นที่แก้ได้จากหน้าเว็บ"
--    ต้องเทียบกับเอกสารจริง https://api-docs.payso.co แล้วแก้ให้ตรงก่อนใช้งานจริง
--    ระบบออกแบบให้แก้ได้ทั้งหมดโดยไม่ต้อง build ใหม่ (เส้นทาง / หัวข้อ auth / รูปแบบ body / ชื่อฟิลด์)
DECLARE @cfg TABLE (
    K NVARCHAR(80), V NVARCHAR(MAX), S BIT, Cat NVARCHAR(60),
    DN NVARCHAR(200), D NVARCHAR(1000), IT NVARCHAR(20), Opt NVARCHAR(500), Ord INT);

INSERT INTO @cfg (K, V, S, Cat, DN, D, IT, Opt, Ord) VALUES
-- ── ทั่วไป ──
('Payment_Enabled',            '0', 0, N'ทั่วไป', N'เปิดรับชำระเงินออนไลน์',
 N'ปิดอยู่ = ระบบทำงานเหมือนเดิมทุกอย่าง ตัวเลือกจ่ายออนไลน์จะไม่แสดงให้ลูกค้าเห็น', 'bool', NULL, 10),
('Payment_Methods_Enabled',    'MANUAL_QR', 0, N'ทั่วไป', N'วิธีชำระที่เปิดให้ลูกค้าเลือก',
 N'คั่นด้วยจุลภาค — MANUAL_QR (สแกน QR แล้วแนบสลิป แบบเดิม), CARD (บัตรเครดิตผ่านเกตเวย์), QR (QR พร้อมเพย์ที่เกตเวย์สร้างให้ ตัดยอดอัตโนมัติ), INSTALLMENT (ผ่อนชำระ)', 'text', NULL, 20),
('Payment_Default_Method',     'MANUAL_QR', 0, N'ทั่วไป', N'วิธีที่เลือกไว้ให้ตั้งแต่แรก',
 N'ต้องเป็นหนึ่งในวิธีที่เปิดไว้ด้านบน', 'text', NULL, 30),
('Payment_Min_Amount',         '20', 0, N'ทั่วไป', N'ยอดขั้นต่ำที่จ่ายออนไลน์ได้ (บาท)',
 N'ต่ำกว่านี้จะเหลือเฉพาะวิธีเดิม', 'number', NULL, 40),
('Payment_Max_Amount',         '0', 0, N'ทั่วไป', N'ยอดสูงสุดที่จ่ายออนไลน์ได้ (บาท)',
 N'0 = ไม่จำกัด', 'number', NULL, 50),
('Payment_Expiry_Minutes',     '30', 0, N'ทั่วไป', N'อายุลิงก์/QR ชำระเงิน (นาที)',
 N'เลยเวลาแล้วรายการจะกลายเป็น "หมดอายุ" และต้องสร้างใหม่', 'number', NULL, 60),
('Payment_Card_Surcharge_Pct', '0', 0, N'ทั่วไป', N'ค่าธรรมเนียมบัตรที่บวกให้ลูกค้า (%)',
 N'0 = ไม่บวก (ร้านรับภาระเอง). ใส่ 3 = บวก 3% จากยอด เฉพาะเมื่อจ่ายด้วยบัตร', 'number', NULL, 70),
('Payment_Auto_Apply',         '1', 0, N'ทั่วไป', N'บันทึกเข้าระบบอัตโนมัติเมื่อจ่ายสำเร็จ',
 N'เปิด = พอเกตเวย์แจ้งว่าจ่ายแล้ว ระบบจะอัปเดตสถานะรายการต้นทาง (การจอง/กิจกรรม) ให้เอง. ปิด = รอพนักงานกดยืนยันเอง', 'bool', NULL, 80),
('Payment_Notify_Staff',       '1', 0, N'ทั่วไป', N'แจ้งเตือนพนักงานเมื่อมีการจ่ายเงินเข้ามา',
 N'ใช้ช่องทางแจ้งเตือนเดิมของระบบ', 'bool', NULL, 90),
('Payment_Site_BaseUrl',       '', 0, N'ทั่วไป', N'โดเมนของเว็บ (สำหรับสร้างลิงก์ให้เกตเวย์)',
 N'เช่น https://taketimebangphra.com — ปล่อยว่าง = ใช้โดเมนที่ผู้ใช้เปิดอยู่. ต้องใส่เมื่อเว็บอยู่หลัง proxy/CDN ไม่งั้น Webhook URL ที่ส่งให้เกตเวย์จะผิด', 'text', NULL, 95),

-- ── QR แบบเดิม (โอนแล้วแนบสลิป) ──
('ManualQr_Image_Url',         '', 0, N'สแกน QR แบบเดิม', N'รูป QR พร้อมเพย์/บัญชีร้าน',
 N'ที่อยู่รูปที่จะแสดงให้ลูกค้าสแกน เช่น /Images/promptpay.png (ปล่อยว่าง = ไม่แสดงรูป แสดงแต่ข้อมูลบัญชี)', 'text', NULL, 110),
('ManualQr_Bank_Info',         '', 0, N'สแกน QR แบบเดิม', N'ข้อมูลบัญชีที่แสดงใต้ QR',
 N'ชื่อบัญชี / เลขบัญชี / ธนาคาร — ขึ้นบรรทัดใหม่ได้', 'textarea', NULL, 120),
('ManualQr_Note',              N'โอนแล้วกรุณาแนบสลิปเพื่อยืนยันการชำระเงิน', 0, N'สแกน QR แบบเดิม',
 N'ข้อความแนะนำใต้ QR', N'', 'textarea', NULL, 130),
('ManualQr_Require_Slip',      '1', 0, N'สแกน QR แบบเดิม', N'บังคับแนบสลิป',
 N'ปิด = ลูกค้ากดยืนยันได้โดยยังไม่แนบสลิป (พนักงานตรวจภายหลัง)', 'bool', NULL, 140),

-- ── Payso: การเชื่อมต่อ ──
('Payso_Enabled',              '0', 0, N'Payso — การเชื่อมต่อ', N'เปิดใช้เกตเวย์ Payso',
 N'ต้องเปิดทั้งข้อนี้และ "เปิดรับชำระเงินออนไลน์" จึงจะใช้บัตรเครดิตได้', 'bool', NULL, 200),
('Payso_Mode',                 'SANDBOX', 0, N'Payso — การเชื่อมต่อ', N'โหมด',
 N'SANDBOX = ทดสอบ (ไม่ตัดเงินจริง) / PRODUCTION = ใช้งานจริง', 'select', 'SANDBOX,PRODUCTION', 210),
('Payso_BaseUrl_Sandbox',      'https://sandbox-api.payso.co', 0, N'Payso — การเชื่อมต่อ', N'Base URL (ทดสอบ)',
 N'⚠ ตรวจกับเอกสารจริงของ Payso ก่อนใช้', 'text', NULL, 220),
('Payso_BaseUrl_Production',   'https://api.payso.co', 0, N'Payso — การเชื่อมต่อ', N'Base URL (ใช้งานจริง)',
 N'⚠ ตรวจกับเอกสารจริงของ Payso ก่อนใช้', 'text', NULL, 230),
('Payso_MerchantId',           '', 0, N'Payso — การเชื่อมต่อ', N'Merchant ID',
 N'รหัสร้านค้าที่ Payso ออกให้', 'text', NULL, 240),
('Payso_ApiKey',               '', 1, N'Payso — การเชื่อมต่อ', N'API Key',
 N'เก็บแบบเข้ารหัสในฐานข้อมูล', 'password', NULL, 250),
('Payso_SecretKey',            '', 1, N'Payso — การเชื่อมต่อ', N'Secret Key',
 N'ใช้เซ็นลายเซ็นคำขอ (ถ้าเกตเวย์กำหนด) — เก็บแบบเข้ารหัส', 'password', NULL, 260),
('Payso_Timeout_Seconds',      '30', 0, N'Payso — การเชื่อมต่อ', N'หมดเวลารอ (วินาที)',
 N'', 'number', NULL, 270),

-- ── Payso: รูปแบบการยืนยันตัวตน (ปรับได้ ไม่ต้อง build ใหม่) ──
('Payso_Auth_Mode',            'BEARER', 0, N'Payso — รูปแบบคำขอ', N'วิธีส่งกุญแจ',
 N'BEARER = Authorization: Bearer <API Key> / APIKEY_HEADER = ส่งเป็นหัวข้อชื่อที่กำหนดด้านล่าง / BOTH = ส่งทั้งสองแบบ / NONE = ไม่ส่ง (ใช้ลายเซ็นอย่างเดียว)',
 'select', 'BEARER,APIKEY_HEADER,BOTH,NONE', 300),
('Payso_ApiKey_Header',        'X-API-Key', 0, N'Payso — รูปแบบคำขอ', N'ชื่อหัวข้อสำหรับ API Key',
 N'ใช้เมื่อเลือก APIKEY_HEADER หรือ BOTH', 'text', NULL, 310),
('Payso_Extra_Headers',        '', 0, N'Payso — รูปแบบคำขอ', N'หัวข้อเพิ่มเติม',
 N'บรรทัดละคู่ รูปแบบ ชื่อ: ค่า', 'textarea', NULL, 320),
('Payso_Signature_Header',     '', 0, N'Payso — รูปแบบคำขอ', N'ชื่อหัวข้อลายเซ็น',
 N'ปล่อยว่าง = ไม่ส่งลายเซ็น เช่น X-Signature', 'text', NULL, 330),
('Payso_Signature_Algo',       'HMACSHA256', 0, N'Payso — รูปแบบคำขอ', N'อัลกอริทึมลายเซ็น',
 N'คำนวณจากเนื้อคำขอ (body) ด้วย Secret Key', 'select', 'HMACSHA256,HMACSHA512,SHA256,MD5,NONE', 340),
('Payso_Signature_Encoding',   'HEX', 0, N'Payso — รูปแบบคำขอ', N'รูปแบบผลลัพธ์ลายเซ็น',
 N'HEX (ตัวอักษร a-f0-9) หรือ BASE64', 'select', 'HEX,BASE64', 350),

-- ── Payso: เส้นทาง API ──
('Payso_Path_CreatePayment',   '/api/v1/payments', 0, N'Payso — เส้นทาง API', N'สร้างรายการชำระเงิน',
 N'⚠ ต้องตรงกับเอกสารจริง — ต่อท้าย Base URL', 'text', NULL, 400),
('Payso_Path_QueryPayment',    '/api/v1/payments/{id}', 0, N'Payso — เส้นทาง API', N'ตรวจสถานะรายการ',
 N'{id} = รหัสรายการฝั่ง Payso, {ref} = เลขอ้างอิงฝั่งเรา', 'text', NULL, 410),
('Payso_Path_Refund',          '/api/v1/payments/{id}/refund', 0, N'Payso — เส้นทาง API', N'คืนเงิน',
 N'ปล่อยว่าง = ปิดปุ่มคืนเงิน', 'text', NULL, 420),

-- ── Payso: รูปแบบเนื้อคำขอ (แม่แบบ JSON — แก้ให้ตรงเอกสารได้ทันที) ──
('Payso_Request_Template', N'{
  "merchantId": "{{merchantId}}",
  "referenceNo": "{{ref}}",
  "amount": {{amount}},
  "currency": "{{currency}}",
  "description": "{{description}}",
  "paymentMethod": "{{method}}",
  "customerName": "{{customerName}}",
  "customerEmail": "{{customerEmail}}",
  "customerPhone": "{{customerPhone}}",
  "returnUrl": "{{returnUrl}}",
  "cancelUrl": "{{cancelUrl}}",
  "callbackUrl": "{{webhookUrl}}",
  "expiresIn": {{expirySeconds}}
}', 0, N'Payso — รูปแบบคำขอ', N'แม่แบบเนื้อคำขอ (JSON)',
 N'ตัวแปรที่ใช้ได้: {{merchantId}} {{ref}} {{amount}} {{amountSatang}} {{currency}} {{description}} {{method}} {{customerName}} {{customerEmail}} {{customerPhone}} {{returnUrl}} {{cancelUrl}} {{webhookUrl}} {{expirySeconds}} {{timestamp}} {{signature}} — แก้ให้ตรงกับเอกสาร Payso ได้เลย ไม่ต้อง build ใหม่',
 'textarea', NULL, 360),
('Payso_Method_Map', N'{"CARD":"credit_card","QR":"promptpay","INSTALLMENT":"installment"}', 0,
 N'Payso — รูปแบบคำขอ', N'แปลงชื่อวิธีชำระเป็นของ Payso',
 N'ซ้ายคือชื่อในระบบเรา ขวาคือค่าที่ Payso ต้องการ (ใช้แทน {{method}})', 'textarea', NULL, 370),

-- ── Payso: อ่านผลลัพธ์ (map ชื่อฟิลด์ในคำตอบ) ──
('Payso_Response_Map', N'{
  "transactionId": ["data.transactionId", "data.id", "transactionId", "id", "paymentId"],
  "paymentUrl":    ["data.paymentUrl", "data.redirectUrl", "data.webPaymentUrl", "paymentUrl", "redirectUrl", "url"],
  "qrPayload":     ["data.qrRawData", "data.qrCode", "qrRawData", "qrCode"],
  "status":        ["data.status", "status", "paymentStatus"],
  "reference":     ["data.referenceNo", "referenceNo", "reference"],
  "message":       ["message", "error", "errorMessage", "data.message"],
  "amount":        ["data.amount", "amount"],
  "fee":           ["data.fee", "fee", "data.feeAmount"],
  "cardBrand":     ["data.cardBrand", "cardBrand", "data.card.brand"],
  "cardLast4":     ["data.cardLast4", "cardLast4", "data.card.last4"],
  "eventId":       ["eventId", "id", "data.eventId"],
  "eventType":     ["eventType", "type", "event"]
}', 0, N'Payso — อ่านคำตอบ', N'ตำแหน่งฟิลด์ในคำตอบ',
 N'ระบบจะลองอ่านตามลำดับที่ระบุ เจอตัวแรกที่มีค่าก็ใช้ตัวนั้น — รองรับเส้นทางซ้อน เช่น data.transactionId',
 'textarea', NULL, 500),
('Payso_Status_Paid',    'success,succeeded,paid,completed,complete,captured,settled,approved', 0,
 N'Payso — อ่านคำตอบ', N'ค่าสถานะที่แปลว่า "จ่ายแล้ว"', N'คั่นด้วยจุลภาค ไม่สนตัวพิมพ์', 'text', NULL, 510),
('Payso_Status_Pending', 'pending,processing,waiting,created,initiated,authorized', 0,
 N'Payso — อ่านคำตอบ', N'ค่าสถานะที่แปลว่า "รอชำระ"', N'', 'text', NULL, 520),
('Payso_Status_Failed',  'failed,fail,error,rejected,declined,void,voided,expired,cancelled,canceled', 0,
 N'Payso — อ่านคำตอบ', N'ค่าสถานะที่แปลว่า "ไม่สำเร็จ"', N'', 'text', NULL, 530),

-- ── Payso: การแจ้งกลับ (webhook) ──
('Payso_Webhook_Verify',   '1', 0, N'Payso — การแจ้งกลับ', N'ตรวจลายเซ็นการแจ้งกลับ',
 N'⚠ ปิดเฉพาะตอนทดสอบเท่านั้น — ปิดไว้ = ใครก็ยิงมาบอกว่า "จ่ายแล้ว" ได้', 'bool', NULL, 600),
('Payso_Webhook_Secret',   '', 1, N'Payso — การแจ้งกลับ', N'กุญแจตรวจลายเซ็นการแจ้งกลับ',
 N'ปล่อยว่าง = ใช้ Secret Key ตัวเดียวกับคำขอ', 'password', NULL, 610),
('Payso_Webhook_Sig_Header', 'X-Signature', 0, N'Payso — การแจ้งกลับ', N'ชื่อหัวข้อลายเซ็นที่ Payso ส่งมา',
 N'', 'text', NULL, 620),
('Payso_Webhook_Ip_Allow', '', 0, N'Payso — การแจ้งกลับ', N'อนุญาตเฉพาะ IP เหล่านี้',
 N'คั่นด้วยจุลภาค ปล่อยว่าง = ไม่จำกัด (ยังตรวจลายเซ็นอยู่)', 'text', NULL, 630),
('Payso_Poll_Enabled',     '1', 0, N'Payso — การแจ้งกลับ', N'ตรวจสถานะซ้ำเองด้วย',
 N'เผื่อการแจ้งกลับหาย — ระบบจะถามสถานะรายการที่ยังค้างเป็นระยะ', 'bool', NULL, 640),
('Payso_Poll_Minutes',     '3', 0, N'Payso — การแจ้งกลับ', N'ถามสถานะทุกกี่นาที',
 N'', 'number', NULL, 650);

INSERT INTO dbo.Payment_Gateway_Config
    (Config_Key, Config_Value, Is_Secret, Category, Display_Name, [Description], Input_Type, Options, Display_Order)
SELECT c.K, c.V, c.S, c.Cat, c.DN, c.D, c.IT, c.Opt, c.Ord
FROM @cfg c
WHERE NOT EXISTS (SELECT 1 FROM dbo.Payment_Gateway_Config p WHERE p.Config_Key = c.K);

PRINT N'เพิ่มค่าตั้งต้นใหม่: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' คีย์';

-- อัปเดตคำอธิบาย/หมวด/ลำดับ ของคีย์ที่มีอยู่แล้ว (ไม่แตะค่าที่ผู้ใช้ตั้งไว้)
UPDATE p
   SET p.Category = c.Cat, p.Display_Name = c.DN, p.[Description] = c.D,
       p.Input_Type = c.IT, p.Options = c.Opt, p.Display_Order = c.Ord, p.Is_Secret = c.S
FROM dbo.Payment_Gateway_Config p
JOIN @cfg c ON c.K = p.Config_Key;
GO

-- ── 5) สวิตช์ฟีเจอร์ (ปิดไว้ก่อน) ───────────────────────────────────────────
IF OBJECT_ID('dbo.System_Config', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.System_Config WHERE ConfigKey = 'Feature_OnlinePayment')
    BEGIN
        INSERT INTO dbo.System_Config (ConfigKey, ConfigValue, Category, DisplayName, [Description], IsSecret, InputType, DisplayOrder, ModifiedDate)
        VALUES ('Feature_OnlinePayment', '0', N'ฟีเจอร์', N'รับชำระเงินออนไลน์',
                N'เปิดให้ลูกค้าเลือกจ่ายด้วยบัตรเครดิต/QR ตัดยอดอัตโนมัติ ผ่านเกตเวย์ — ปิดอยู่ระบบทำงานเหมือนเดิมทุกอย่าง',
                0, 'bool', 500, GETDATE());
        PRINT N'เพิ่มสวิตช์ Feature_OnlinePayment (ปิดไว้)';
    END
    ELSE PRINT N'มีสวิตช์ Feature_OnlinePayment อยู่แล้ว — ไม่แตะค่าเดิม';
END
GO

-- ── 6) โมดูลสิทธิ์ SYS_PAYMENT ──────────────────────────────────────────────
-- ให้กลุ่มที่มีสิทธิ์ "ตั้งค่าบัญชี & ภาษี" อยู่แล้ว ได้สิทธิ์นี้ตามเดิม จะได้ไม่มีใครเสียสิทธิ์
IF OBJECT_ID('dbo.Permission_Group_Modules', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.Permission_Group_Modules (Group_ID, Module_Code, Can_View, Can_Access)
    SELECT s.Group_ID, 'SYS_PAYMENT', s.Can_View, s.Can_Access
    FROM dbo.Permission_Group_Modules s
    WHERE s.Module_Code = 'SYS_ACCOUNTING'
      AND NOT EXISTS (SELECT 1 FROM dbo.Permission_Group_Modules p
                      WHERE p.Group_ID = s.Group_ID AND p.Module_Code = 'SYS_PAYMENT');
    PRINT N'ให้สิทธิ์ SYS_PAYMENT ตาม SYS_ACCOUNTING: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว';

    DECLARE @OwnerId INT = (SELECT TOP 1 ID FROM dbo.Permission_Groups WHERE Base_Role = N'Owner' AND Is_System = 1);
    IF @OwnerId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Permission_Group_Modules
                                            WHERE Group_ID = @OwnerId AND Module_Code = 'SYS_PAYMENT')
        INSERT INTO dbo.Permission_Group_Modules (Group_ID, Module_Code, Can_View, Can_Access)
        VALUES (@OwnerId, 'SYS_PAYMENT', 1, 1);
END
GO

-- ── ตรวจผล ─────────────────────────────────────────────────────────────────
SELECT Category AS [หมวด], COUNT(*) AS [จำนวนค่าตั้ง]
FROM dbo.Payment_Gateway_Config GROUP BY Category ORDER BY MIN(Display_Order);

SELECT N'สถานะฟีเจอร์' AS [รายการ],
       ISNULL((SELECT ConfigValue FROM dbo.System_Config WHERE ConfigKey='Feature_OnlinePayment'), '0') AS [ค่า],
       N'0 = ปิด (ระบบทำงานเหมือนเดิมทุกอย่าง)' AS [หมายเหตุ];

PRINT '';
PRINT N'ขั้นต่อไป: ศูนย์ตั้งค่า → "รับชำระเงินออนไลน์ (Payso)" → ใส่ค่าจากเอกสาร Payso → กด "ทดสอบการเชื่อมต่อ"';
PRINT N'ตราบใดที่ยังไม่เปิดสวิตช์ ระบบเดิมทำงานเหมือนเดิมทุกประการ';
