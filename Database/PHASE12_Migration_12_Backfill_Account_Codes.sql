-- ============================================================================
-- PHASE 12 Migration 12: Backfill missing Nexaacc_AccountCode in mapping table
-- ============================================================================
-- ปัญหา: Sync API error
--   "ConvertJournalToIntegration: ไม่พบ Nexaacc_AccountCode สำหรับ AccountId ..."
--
-- สาเหตุ: บางแถวใน Accounting_Account_Mapping มี Nexaacc_AccountId ตั้งไว้
--         แต่ Nexaacc_AccountCode ว่าง/NULL → reverse lookup (Id→Code) ล้มเหลว
--         ตอนแปลง journal เป็น /api/integration/journals payload
--
-- แก้ไข: เติม Nexaacc_AccountCode จาก Accounting_Nexaacc_Accounts
--        (chart-of-accounts cache ที่ sync จาก NextAcc โดยตรง — มี Id↔Code ครบ)
--
-- หมายเหตุ: ต้องเคยกด "ดึง Chart of Accounts" ในหน้า Accounting Integration
--           อย่างน้อย 1 ครั้ง เพื่อให้ Accounting_Nexaacc_Accounts มีข้อมูล
-- ============================================================================

USE [Taketime]
GO

PRINT '============================================================================'
PRINT 'PHASE 12 Migration 12: Backfill missing Nexaacc_AccountCode'
PRINT '============================================================================'
GO

IF OBJECT_ID(N'[dbo].[Accounting_Account_Mapping]', N'U') IS NULL
   OR OBJECT_ID(N'[dbo].[Accounting_Nexaacc_Accounts]', N'U') IS NULL
BEGIN
    PRINT '! ตารางที่จำเป็นไม่ครบ (Accounting_Account_Mapping / Accounting_Nexaacc_Accounts) — ข้าม';
END
ELSE
BEGIN
    -- แสดงแถวที่มีปัญหา ก่อนแก้
    DECLARE @before INT = (
        SELECT COUNT(*)
        FROM Accounting_Account_Mapping
        WHERE Nexaacc_AccountId IS NOT NULL
          AND (Nexaacc_AccountCode IS NULL OR LTRIM(RTRIM(Nexaacc_AccountCode)) = '')
    );
    PRINT 'พบแถวที่มี AccountId แต่ขาด AccountCode: ' + CAST(@before AS VARCHAR(10)) + ' แถว';

    -- Backfill: เติม code จาก chart-of-accounts cache โดย join ด้วย AccountId
    UPDATE M
    SET M.Nexaacc_AccountCode = A.Account_Code
    FROM Accounting_Account_Mapping M
    INNER JOIN Accounting_Nexaacc_Accounts A ON A.Nexaacc_AccountId = M.Nexaacc_AccountId
    WHERE M.Nexaacc_AccountId IS NOT NULL
      AND (M.Nexaacc_AccountCode IS NULL OR LTRIM(RTRIM(M.Nexaacc_AccountCode)) = '')
      AND A.Account_Code IS NOT NULL
      AND LTRIM(RTRIM(A.Account_Code)) <> '';

    DECLARE @fixed INT = @@ROWCOUNT;
    PRINT '+ เติม Nexaacc_AccountCode สำเร็จ: ' + CAST(@fixed AS VARCHAR(10)) + ' แถว';

    -- แถวที่ยังเหลือ (AccountId ไม่อยู่ใน chart cache เลย) — ต้อง re-sync chart
    DECLARE @remaining INT = (
        SELECT COUNT(*)
        FROM Accounting_Account_Mapping
        WHERE Nexaacc_AccountId IS NOT NULL
          AND (Nexaacc_AccountCode IS NULL OR LTRIM(RTRIM(Nexaacc_AccountCode)) = '')
    );
    IF @remaining > 0
        PRINT '⚠ ยังเหลือ ' + CAST(@remaining AS VARCHAR(10)) + ' แถวที่ AccountId ไม่อยู่ใน chart cache — '
            + 'กรุณากด "ดึง Chart of Accounts" ในหน้า Accounting Integration อีกครั้ง';
    ELSE
        PRINT '= ไม่มีแถวที่มีปัญหาเหลือแล้ว';
END
GO

PRINT '============================================================================'
PRINT 'PHASE 12 Migration 12: COMPLETED'
PRINT '============================================================================'
GO
