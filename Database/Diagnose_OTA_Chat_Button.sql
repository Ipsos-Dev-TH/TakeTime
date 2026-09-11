-- ═══════════════════════════════════════════════════════════════
-- วินิจฉัย: ทำไมปุ่ม 💬 ไม่ขึ้นบนหน้า ReserveTable
-- รันทั้งชุด แล้วดูคอลัมน์ [ผล] ว่าติดขั้นไหน
-- ═══════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- ขั้น 1: มีตารางช่องทาง + เปิด EMAIL แล้วหรือยัง
IF OBJECT_ID('dbo.OmniChannel_Channels','U') IS NULL
    SELECT N'ขั้น 1' AS [ขั้น], N'❌ ไม่มีตาราง OmniChannel_Channels — รัน PHASE15_Migration_01_OmniChannel.sql' AS [ผล];
ELSE
    SELECT N'ขั้น 1' AS [ขั้น],
           CASE WHEN NOT EXISTS (SELECT 1 FROM OmniChannel_Channels WHERE ChannelCode='EMAIL')
                     THEN N'❌ ไม่มีแถว EMAIL — กดปุ่ม "สร้างช่องทางเริ่มต้น" หรือรัน migration'
                WHEN (SELECT IsEnabled FROM OmniChannel_Channels WHERE ChannelCode='EMAIL') = 0
                     THEN N'❌ ช่องทาง EMAIL ปิดอยู่ → UPDATE OmniChannel_Channels SET IsEnabled=1 WHERE ChannelCode=''EMAIL'''
                ELSE N'✅ เปิดช่องทาง EMAIL แล้ว' END AS [ผล];

-- ขั้น 2: อ่านอีเมลเข้ามาเป็นแชทได้หรือยัง
SELECT N'ขั้น 2' AS [ขั้น],
       CASE WHEN COUNT(*) = 0
            THEN N'❌ ยังไม่มีบทสนทนา EMAIL เลย — ระบบยังไม่เคยดึงอีเมลสำเร็จ (อีเมลต้อง "ยังไม่อ่าน" + โดเมนตรง fromDomains)'
            ELSE N'✅ มีบทสนทนา EMAIL ' + CAST(COUNT(*) AS nvarchar(10)) + N' รายการ' END AS [ผล]
  FROM OmniChannel_Conversations WHERE ChannelCode='EMAIL';

-- ขั้น 3: บทสนทนาผูกกับใบจองหรือยัง  ← เงื่อนไขที่ปุ่มใช้ตรง ๆ
SELECT N'ขั้น 3' AS [ขั้น],
       CASE WHEN COUNT(*) = 0
            THEN N'❌ ไม่มีบทสนทนาไหนผูกใบจอง (Contacts.Reservation_ID เป็น NULL) → ปุ่มจึงถูกซ่อน'
            ELSE N'✅ ผูกแล้ว ' + CAST(COUNT(*) AS nvarchar(10)) + N' รายการ — ปุ่มต้องขึ้นในวันที่ของใบจองนั้น' END AS [ผล]
  FROM OmniChannel_Conversations c
  JOIN OmniChannel_Contacts ct ON ct.ID = c.ContactID
 WHERE ct.Reservation_ID IS NOT NULL;

-- ขั้น 4: เจาะเคสของคุณ Apichai (เลขจอง Agoda 1986747240)
SELECT N'ขั้น 4' AS [ขั้น], ID AS [เลขที่จอง], Status AS [สถานะ],
       CheckinDate, CheckoutDate, OTA_Booking_ID, LEFT(Remark,80) AS [Remark]
  FROM Reservation
 WHERE OTA_Booking_ID LIKE '%1986747240%' OR Remark LIKE '%1986747240%';

-- ขั้น 5: รายชื่อบทสนทนา EMAIL ทั้งหมด + ผูกใบไหน
SELECT TOP 20 N'ขั้น 5' AS [ขั้น], c.ID AS [ConvID], ct.DisplayName AS [ชื่อ],
       ct.Email, ct.Reservation_ID AS [ผูกใบจอง], c.LastMessageDate
  FROM OmniChannel_Conversations c
  JOIN OmniChannel_Contacts ct ON ct.ID = c.ContactID
 WHERE c.ChannelCode = 'EMAIL'
 ORDER BY c.ID DESC;

-- ขั้น 6: log การทำงานล่าสุดของตัวอ่านอีเมลแชท
SELECT TOP 15 N'ขั้น 6' AS [ขั้น], LogDateTime, LogDetail
  FROM Logs WHERE LogAction = 'EmailChat' ORDER BY LogDateTime DESC;
