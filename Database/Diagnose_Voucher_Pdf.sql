-- ============================================================================
-- ทำไมกดดู PDF ใบสำคัญจ่ายแล้วได้ "ตัวร่าง" ทั้งที่ NextAcc อนุมัติแล้ว
-- ============================================================================
-- อ่านอย่างเดียว รันได้เลย · ตอบด้วย "ข้อมูลจริงที่ NextAcc ตอบกลับมา" ไม่ใช่การเดา
--
-- ระบบบันทึก request + response ของทุก API call ไว้ใน Accounting_Sync_Log อยู่แล้ว
-- จึงย้อนดูได้ตรง ๆ ว่า "GUID ที่เราส่งไปขอ PDF นั้น NextAcc บอกว่าเป็นเอกสารเลขอะไร"
--
-- เคสที่กำลังไล่: เอกสาร PV-20260807-0003 / อ้างอิง 057-ESI26067356
--   ไฟล์ที่เสิร์ฟ = /Documents/Payment/NextAcc/057-ESI26067356_56c16cb0/...
--   ⇒ GUID ที่ใช้ขึ้นต้นด้วย 56c16cb0
-- ============================================================================

DECLARE @Guid8   NVARCHAR(20) = N'56c16cb0';          -- <<< 8 ตัวแรกของ GUID (จาก URL ไฟล์)
DECLARE @DocNo   NVARCHAR(50) = N'PV-20260807-0003';  -- <<< เลขที่เอกสารที่ควรจะเป็น
DECLARE @Ref     NVARCHAR(50) = N'057-ESI26067356';   -- <<< เลขอ้างอิง

SET NOCOUNT ON;

-- ── 1) เราขอ PDF จาก GUID ไหนบ้าง (ล่าสุดก่อน) ──────────────────────────────
PRINT '--- 1) การเรียก generate-pdf ล่าสุด ---';
SELECT TOP 10 g.Created_Date, g.Action, g.HTTP_Status, g.Success,
       CAST(g.Request_Payload AS NVARCHAR(MAX)) AS Request_ที่ส่งไป
FROM Accounting_Sync_Log g
WHERE g.Action LIKE '%generate-pdf%'
ORDER BY g.ID DESC;
GO

-- ── 2) ⭐ คำตอบอยู่ตรงนี้: NextAcc บอกว่า GUID นี้คือเอกสารเลขอะไร ────────────
--     ระบบยิง GET .../document/{guid} หลังดึง PDF ทุกครั้ง (ตั้งแต่ build 26/08)
--     Response_Payload จะมี "documentNumber" และ "status" ของจริง
PRINT '';
PRINT '--- 2) NextAcc ตอบว่า GUID นี้เป็นเอกสารอะไร ---';
DECLARE @G NVARCHAR(20) = (SELECT TOP 1 N'56c16cb0');
SELECT TOP 10 g.Created_Date, g.Action, g.HTTP_Status,
       CAST(g.Response_Payload AS NVARCHAR(MAX)) AS Response_จาก_NextAcc
FROM Accounting_Sync_Log g
WHERE g.Action LIKE '%/document/%'
  AND CAST(g.Action AS NVARCHAR(MAX)) LIKE '%' + @Guid8 + '%'
ORDER BY g.ID DESC;
GO

-- ── 3) log ที่ระบบสรุปให้ (ตั้งแต่ build ใหม่) ──────────────────────────────
PRINT '';
PRINT '--- 3) บรรทัดสรุปของระบบ ---';
SELECT TOP 20 l.LogDateTime, LEFT(CAST(l.LogDetail AS NVARCHAR(MAX)), 500) AS LogDetail
FROM Logs l
WHERE l.LogAction = 'AccountingSync'
  AND (CAST(l.LogDetail AS NVARCHAR(MAX)) LIKE '%DownloadNextAccDocumentById%'
       OR CAST(l.LogDetail AS NVARCHAR(MAX)) LIKE '%DownloadVoucherDocument%')
  AND l.LogDateTime >= DATEADD(DAY, -3, GETDATE())
ORDER BY l.LogDateTime DESC;
GO

-- ── 4) รายการเอกสารทั้งหมดที่ NextAcc เคยส่งมาให้ ที่มีเลขนี้/อ้างอิงนี้ ─────
--     ถ้าเห็น "สองใบ" (ใบร่าง + ใบอนุมัติ) แสดงว่าเป็นเคสผูกผิดใบ
PRINT '';
PRINT '--- 4) เอกสารที่มีเลข/อ้างอิงนี้ ในคำตอบที่ NextAcc เคยส่งมา ---';
SELECT TOP 10 g.Created_Date, g.Action,
       LEFT(CAST(g.Response_Payload AS NVARCHAR(MAX)), 3000) AS Response
FROM Accounting_Sync_Log g
WHERE CAST(g.Response_Payload AS NVARCHAR(MAX)) LIKE '%' + @Ref + '%'
   OR CAST(g.Response_Payload AS NVARCHAR(MAX)) LIKE '%' + @DocNo + '%'
ORDER BY g.ID DESC;
GO

-- ============================================================================
-- วิธีอ่านผล — ดูที่ข้อ 2 เป็นหลัก
--
--   documentNumber = "PV-20260807-0003"  →  GUID ถูกแล้ว แต่ PDF ที่ NextAcc
--        เรนเดอร์ออกมาเขียน DRAFT-  ⇒ **บั๊กฝั่ง NextAcc** (metadata กับ PDF ไม่ตรงกัน)
--        แก้ฝั่งเราไม่ได้ ต้องแจ้งทีม NextAcc — ส่ง GUID + ผลข้อ 2 ให้เขาดู
--
--   documentNumber = "DRAFT-75d1235a"    →  รายการนี้ผูกกับ **ใบร่างที่ค้าง**
--        ใบที่อนุมัติเป็นคนละ GUID ⇒ ลบใบร่างที่ค้างบน NextAcc แล้วให้ระบบดึงรายการใหม่
--
--   ไม่มีแถวเลย                          →  build ใหม่ยังไม่ได้ deploy หรือยังไม่ได้กดดึงล่าสุด
--        หลัง deploy ให้กด "🔄 ดึงล่าสุด" หนึ่งครั้งแล้วรันสคริปต์นี้ใหม่
-- ============================================================================
