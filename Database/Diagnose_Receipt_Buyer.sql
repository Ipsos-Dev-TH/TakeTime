-- ============================================================================
-- ตรวจ "ทำไมเอกสารบน NextAcc ออกในนามผู้จอง ไม่ใช่ผู้ซื้อของใบเสร็จ"
-- ============================================================================
-- อ่านอย่างเดียว ไม่แก้ข้อมูล · รันได้เลยไม่ต้อง deploy
-- สคริปต์นี้ "จำลองตรรกะเดียวกับตอน sync จริง" ทีละขั้น แล้วบอกว่าขั้นไหนพัง
--
-- วิธีใช้: แก้ @Receipt ให้เป็นเลขใบเสร็จในระบบ (ไม่มีขีดคั่น) แล้วรันทั้งไฟล์
-- ============================================================================

DECLARE @Receipt NVARCHAR(50) = N'REC260815002';   -- <<< แก้ตรงนี้

SET NOCOUNT ON;
PRINT '===== ตรวจใบเสร็จ ' + @Receipt + ' =====';

-- ── 1) ใบเสร็จผูกผู้ซื้อไว้กับใคร ────────────────────────────────────────────
PRINT '';
PRINT '--- 1) Account_Receipt: ผู้ซื้อที่ผูกไว้กับใบนี้ ---';
SELECT
    ar.ID                                   AS ReceiptNo,
    ar.Reservation_ID,
    ar.Total_Amount, ar.Vat, ar.IsDeposit, ar.Status,
    ISNULL(CAST(ar.Customer_ID AS NVARCHAR(30)), '(NULL)') AS Customer_ID,
    ISNULL(ar.Nexaacc_Receipt_Payment_Id, '(ว่าง)')        AS Marker,
    -- ผู้ซื้อของใบ (ตัวที่ sync ควรใช้)
    ISNULL(c.FullName, ISNULL(c.Name, '(ไม่พบลูกค้า)'))    AS BuyerName,
    ISNULL(c.MobilePhone, '')                              AS BuyerPhone,
    ISNULL(NULLIF(LTRIM(RTRIM(c.IDNumber)), ''), ISNULL(c.TaxID, '')) AS BuyerTaxId,
    ISNULL(c.Address, '')                                  AS BuyerAddress,
    ISNULL(ct.Customer_Code, '(ไม่มี)')                    AS BuyerTypeCode,
    ISNULL(c.Branch_Number, '')                            AS BuyerBranch,
    -- ผู้จอง (ตัวที่ระบบตกไปใช้เมื่อหาผู้ซื้อไม่ได้)
    ISNULL(r.Customer_MobilePhone, '')                     AS GuestPhone,
    ISNULL(g.FullName, ISNULL(g.Name, ''))                 AS GuestName
FROM Account_Receipt ar
LEFT JOIN Customer      c  ON c.ID = ar.Customer_ID
LEFT JOIN Customer_Type ct ON ct.ID = c.Customer_Type_ID
LEFT JOIN Reservation   r  ON r.ID = ar.Reservation_ID
LEFT JOIN Customer      g  ON g.MobilePhone = r.Customer_MobilePhone
WHERE ar.ID = @Receipt;

-- ── 2) ผ่านเกณฑ์ §86/4 ไหม (ตัวตัดสินว่าได้ใบกำกับหรือใบเสร็จ) ───────────────
PRINT '';
PRINT '--- 2) HasFullBuyerTaxData: ต้อง TaxIdOk=1 และ AddressOk=1 ถึงจะได้ใบกำกับ ---';
SELECT
    ISNULL(NULLIF(LTRIM(RTRIM(c.IDNumber)), ''), ISNULL(c.TaxID, '')) AS TaxIdUsed,
    CASE WHEN ISNULL(NULLIF(LTRIM(RTRIM(c.IDNumber)), ''), ISNULL(c.TaxID, '')) LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
         THEN 1 ELSE 0 END AS TaxIdOk,
    -- ที่อยู่ที่ระบบประกอบส่ง (บ้านเลขที่ + หมู่ + ตำบล/อำเภอ/จังหวัด + ไปรษณีย์)
    LTRIM(RTRIM(
        ISNULL(c.Address, '') + ' ' + ISNULL(c.Address1, '') + ' ' +
        ISNULL(a.SubDistrict, '') + ' ' + ISNULL(a.District, '') + ' ' +
        ISNULL(a.Province, '') + ' ' + ISNULL(a.PostalCode, ''))) AS AddressComposed,
    CASE WHEN LTRIM(RTRIM(ISNULL(c.Address, ''))) <> '' THEN 1 ELSE 0 END AS AddressOk,
    CASE WHEN ct.Customer_Code = 'TXID' THEN N'นิติบุคคล' ELSE N'บุคคลธรรมดา' END AS TypeSentToNextAcc
FROM Account_Receipt ar
JOIN Customer c ON c.ID = ar.Customer_ID
LEFT JOIN Address a ON a.ID = c.Address_ID
LEFT JOIN Customer_Type ct ON ct.ID = c.Customer_Type_ID
WHERE ar.ID = @Receipt;

-- ── 3) contact ของผู้ซื้อ ถูก map กับ NextAcc แล้วหรือยัง ────────────────────
--     ถ้าไม่มีแถว/Nexaacc_Contact_Id ว่าง = sync ตกไปใช้ผู้จอง
PRINT '';
PRINT '--- 3) Accounting_Contact_Map (คีย์ = เบอร์โทร) ---';
SELECT m.External_Id, m.Contact_Type, m.Nexaacc_Contact_Id, m.Name, m.Tax_Id,
       m.Sync_Status, m.Sync_Error, m.Last_Synced
FROM Accounting_Contact_Map m
WHERE m.External_Id IN (
    SELECT c.MobilePhone FROM Account_Receipt ar JOIN Customer c ON c.ID = ar.Customer_ID WHERE ar.ID = @Receipt
    UNION
    SELECT r.Customer_MobilePhone FROM Account_Receipt ar JOIN Reservation r ON r.ID = ar.Reservation_ID WHERE ar.ID = @Receipt
);

-- ── 4) payload ในคิว: มี customerPhone ไหม / ยอดเท่าไหร่ / ชี้เอกสารใด ───────
PRINT '';
PRINT '--- 4) คิว CREATE_RECEIPT_DOCUMENT ของใบนี้ (ใหม่ล่าสุดก่อน) ---';
SELECT TOP 10
    q.ID, q.Status, q.Retry_Count, q.Max_Retries,
    q.Nexaacc_Response_Id, q.Nexaacc_Document_Number,
    CASE WHEN CAST(q.Payload AS NVARCHAR(MAX)) LIKE '%"customerPhone"%' THEN N'มี' ELSE N'✗ ไม่มี (คิวเก่า)' END AS HasCustomerPhone,
    CAST(q.Payload AS NVARCHAR(MAX)) AS Payload,
    LEFT(CAST(ISNULL(q.Error_Message, '') AS NVARCHAR(MAX)), 400) AS Error_Message,
    q.Created_Date, q.Processed_Date
FROM Accounting_Sync_Queue q
WHERE q.Action_Type = 'CREATE_RECEIPT_DOCUMENT'
  AND CAST(q.Payload AS NVARCHAR(MAX)) LIKE '%"receiptNumber":"' + @Receipt + '"%'
ORDER BY q.ID DESC;

-- ── 5) คิว sync ผู้ติดต่อของเบอร์ผู้ซื้อ ─────────────────────────────────────
PRINT '';
PRINT '--- 5) คิว SYNC_CUSTOMER_CONTACT ของเบอร์ผู้ซื้อ ---';
SELECT TOP 10 q.ID, q.Status, q.Retry_Count, q.Max_Retries,
       CAST(q.Payload AS NVARCHAR(MAX)) AS Payload,
       LEFT(CAST(ISNULL(q.Error_Message, '') AS NVARCHAR(MAX)), 400) AS Error_Message, q.Created_Date
FROM Accounting_Sync_Queue q
WHERE q.Action_Type = 'SYNC_CUSTOMER_CONTACT'
  AND CAST(q.Payload AS NVARCHAR(MAX)) LIKE '%' + (SELECT TOP 1 c.MobilePhone FROM Account_Receipt ar
                            JOIN Customer c ON c.ID = ar.Customer_ID WHERE ar.ID = @Receipt) + '%'
ORDER BY q.ID DESC;

-- ── 6) บรรทัดตัดสินใจจาก log (ถ้า DLL ที่รันมีบรรทัดนี้แล้ว) ─────────────────
PRINT '';
PRINT '--- 6) log การตัดสินใจของ sync (ถ้ามี) ---';
SELECT TOP 20 l.LogDateTime, LEFT(CAST(l.LogDetail AS NVARCHAR(MAX)), 700) AS LogDetail
FROM Logs l
WHERE l.LogAction = 'AccountingSync'
  AND CAST(l.LogDetail AS NVARCHAR(MAX)) LIKE '%' + @Receipt + '%'
  AND l.LogDateTime >= DATEADD(DAY, -3, GETDATE())
ORDER BY l.LogDateTime DESC;

-- ── 7) API call จริงที่ยิงไป (request/response ดิบ) ──────────────────────────
PRINT '';
PRINT '--- 7) API call ของ contact ล่าสุด — ดูว่าส่งชื่อไปไหม และ NextAcc ตอบชื่ออะไร ---';
SELECT TOP 10 g.Created_Date, g.Action, g.HTTP_Status, g.Success,
       CAST(g.Request_Payload AS NVARCHAR(MAX)) AS Request_Payload,
       CAST(g.Response_Payload AS NVARCHAR(MAX)) AS Response_Payload
FROM Accounting_Sync_Log g
WHERE g.Action LIKE '%integration/customers%' OR g.Action LIKE '%document/contacts%'
ORDER BY g.ID DESC;

-- ── 8) เอกสารทั้งหมดของการจองนี้ที่ระบบเคยสร้าง (ไล่หาใบซ้ำ) ────────────────
PRINT '';
PRINT '--- 8) เอกสาร NextAcc ทั้งหมดที่คิวเคยสร้างให้การจองนี้ (หาใบซ้ำ) ---';
SELECT q.ID, q.Action_Type, q.Status, q.Nexaacc_Document_Number, q.Nexaacc_Response_Id,
       q.Created_Date, q.Processed_Date
FROM Accounting_Sync_Queue q
WHERE q.Entity_ID = (SELECT TOP 1 Reservation_ID FROM Account_Receipt WHERE ID = @Receipt)
  AND q.Nexaacc_Document_Number IS NOT NULL
ORDER BY q.ID DESC;

-- ── 9) เลขเอกสาร NextAcc ที่ถูกใช้ซ้ำ (สำคัญ!) ───────────────────────────────
-- NextAcc จ่ายเลขเดิมให้ใบใหม่ได้หลังยกเลิกใบเก่า ⇒ "เลขเดียวกัน" อาจเป็นคนละใบ
-- ถ้าเลขหนึ่งมีหลาย GUID ให้ยึด GUID ล่าสุดเสมอ (ใบเก่าคือใบที่ถูกยกเลิกไปแล้ว)
PRINT '';
PRINT '--- 9) เลขเอกสารที่ถูกใช้ซ้ำหลาย GUID ---';
SELECT q.Nexaacc_Document_Number,
       COUNT(DISTINCT q.Nexaacc_Response_Id) AS GuidCount,
       MIN(q.Created_Date) AS FirstUsed,
       MAX(q.Created_Date) AS LastUsed
FROM Accounting_Sync_Queue q
WHERE q.Nexaacc_Document_Number IS NOT NULL
  AND q.Nexaacc_Response_Id IS NOT NULL
GROUP BY q.Nexaacc_Document_Number
HAVING COUNT(DISTINCT q.Nexaacc_Response_Id) > 1
ORDER BY LastUsed DESC;
