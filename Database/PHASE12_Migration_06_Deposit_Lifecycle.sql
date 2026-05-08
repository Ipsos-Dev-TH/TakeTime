-- =============================================
-- PHASE 12 Migration 06: Deposit Lifecycle Accounting
-- รองรับการตัดมัดจำ (เจ้าหนี้ ADVANCE_DEPOSIT) ออกตอน checkout / cancel / forfeit
--   1. Account mappings ใหม่: DAMAGE_INCOME, FORFEIT_INCOME, OTHER_INCOME
--   2. View vw_Reservation_Deposit_Status — ติดตามสถานะมัดจำต่อการจอง
-- =============================================

-- ──────────────────────────────────────────────
-- 1. Seed account mappings ใหม่
-- เพื่อรองรับกรณีหักค่าเสียหาย / ริบเงินมัดจำ / รายได้อื่น ๆ
-- หากไม่มีอยู่ในระบบ NextAcc ให้ผูกกับ Other Income (เช่น 41200)
-- ──────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'DAMAGE_INCOME')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        (N'DAMAGE_INCOME', N'รายได้ค่าเสียหาย/หักจากมัดจำ', '41210', 'REVENUE', 1);
END

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'FORFEIT_INCOME')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        (N'FORFEIT_INCOME', N'รายได้จากการริบมัดจำ', '41220', 'REVENUE', 1);
END

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'OTHER_INCOME')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        (N'OTHER_INCOME', N'รายได้อื่น ๆ', '41200', 'REVENUE', 1);
END
GO

-- ──────────────────────────────────────────────
-- 2. View vw_Reservation_Deposit_Status
-- รวมข้อมูลมัดจำ + การตัดเจ้าหนี้ต่อการจอง:
--   - DepositPaid       — มัดจำที่ลูกค้าจ่ายไปทั้งหมด
--   - DepositCleared    — มัดจำที่ตัดออกแล้ว (จาก checkout/refund/forfeit)
--   - DepositOutstanding — เจ้าหนี้คงเหลือ (Liability balance)
--   - Status            — OPEN / CLEARED / PARTIAL / OVER_CLEARED
-- ตอบโจทย์: เห็นได้ชัดเจนว่าเจ้าหนี้มัดจำของแต่ละการจองสถานะเป็นยังไง
-- ──────────────────────────────────────────────

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Reservation_Deposit_Status')
    DROP VIEW vw_Reservation_Deposit_Status;
GO

CREATE VIEW vw_Reservation_Deposit_Status AS
SELECT
    R.ID AS Reservation_ID,
    R.Status AS Reservation_Status,
    R.CheckinDate,
    R.CheckoutDate,
    R.Customer_MobilePhone,
    ISNULL(C.FullName, C.Name) AS Customer_Name,
    C.Email AS Customer_Email,
    -- มัดจำที่ลูกค้าจ่าย (จาก Account_Receipt)
    ISNULL(D.DepositPaid, 0) AS DepositPaid,
    -- มัดจำที่ตัดเจ้าหนี้ออกแล้ว (จาก Sync Queue ที่ COMPLETED)
    ISNULL(L.DepositCleared, 0) AS DepositCleared,
    -- คงเหลือในเจ้าหนี้
    (ISNULL(D.DepositPaid, 0) - ISNULL(L.DepositCleared, 0)) AS DepositOutstanding,
    -- สถานะ
    CASE
        WHEN ISNULL(D.DepositPaid, 0) = 0 THEN 'NO_DEPOSIT'
        WHEN ISNULL(L.DepositCleared, 0) = 0 THEN 'OPEN'
        WHEN ABS(ISNULL(D.DepositPaid, 0) - ISNULL(L.DepositCleared, 0)) < 0.01 THEN 'CLEARED'
        WHEN ISNULL(L.DepositCleared, 0) > ISNULL(D.DepositPaid, 0) THEN 'OVER_CLEARED'
        ELSE 'PARTIAL'
    END AS Deposit_Status,
    -- รายละเอียดเพิ่มเติม
    L.LastClearDate,
    L.LastClearAction,
    L.LastClearJournalId
FROM Reservation R
LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
OUTER APPLY (
    SELECT SUM(Total_Amount) AS DepositPaid
    FROM Account_Receipt
    WHERE Reservation_ID = R.ID
      AND IsDeposit = 1
      AND (Status = 'Normal' OR Status IS NULL OR Status = '1' OR Status = 'COMPLETED')
) D
OUTER APPLY (
    SELECT
        SUM(TRY_CAST(JSON_VALUE(Q.Payload, '$.depositAmount') AS DECIMAL(18,2)))
            + SUM(TRY_CAST(JSON_VALUE(Q.Payload, '$.refundAmount') AS DECIMAL(18,2)))
            + SUM(TRY_CAST(JSON_VALUE(Q.Payload, '$.forfeitAmount') AS DECIMAL(18,2)))
            AS DepositCleared,
        MAX(Q.Processed_Date) AS LastClearDate,
        (SELECT TOP 1 Action_Type FROM Accounting_Sync_Queue
            WHERE Entity_Type = 'RESERVATION' AND Entity_ID = R.ID
              AND Action_Type IN ('CLEAR_DEPOSIT_AT_CHECKOUT','REFUND_DEPOSIT','FORFEIT_DEPOSIT')
              AND Status = 'COMPLETED'
            ORDER BY Processed_Date DESC) AS LastClearAction,
        (SELECT TOP 1 Nexaacc_Response_Id FROM Accounting_Sync_Queue
            WHERE Entity_Type = 'RESERVATION' AND Entity_ID = R.ID
              AND Action_Type IN ('CLEAR_DEPOSIT_AT_CHECKOUT','REFUND_DEPOSIT','FORFEIT_DEPOSIT')
              AND Status = 'COMPLETED'
            ORDER BY Processed_Date DESC) AS LastClearJournalId
    FROM Accounting_Sync_Queue Q
    WHERE Q.Entity_Type = 'RESERVATION'
      AND Q.Entity_ID = R.ID
      AND Q.Action_Type IN ('CLEAR_DEPOSIT_AT_CHECKOUT','REFUND_DEPOSIT','FORFEIT_DEPOSIT')
      AND Q.Status = 'COMPLETED'
) L;
GO

PRINT 'Created view: vw_Reservation_Deposit_Status';
GO
