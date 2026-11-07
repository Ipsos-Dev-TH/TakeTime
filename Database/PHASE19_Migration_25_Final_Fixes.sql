-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 25: Final fixes (เงินประกันหักค่าเสียหาย + สลิปลูกค้ารอตรวจ)
-- ════════════════════════════════════════════════════════════════════════════
-- 1) แหล่งเงิน "หักจากเงินประกัน (Security deposit)" (Account_Paid_How, Channel_Code = SECURITY_DEPOSIT_OFFSET)
--    ใช้กับเงินประกันความเสียหายแบบโอน (Security_Hold_Mode = TRANSFER) + เปิด Nexaacc_SecurityDeposit_Journal (PHASE19_24)
--    เดิม: หักค่าเสียหาย → JE SECDEP-{id}-DMG  Dr หนี้สินเงินประกัน / Cr ธนาคาร  ทันที แล้วพึ่งพนักงานออกใบเสร็จค่าเสียหาย
--          ด้วยแหล่งเงิน = บัญชีรับโอน (Dr ธนาคาร) มาหักล้าง → ลืมออกใบ/เลือกเงินสด = ธนาคารขาด + ไม่มีรายได้
--    ใหม่: ไม่มี JE DMG — หนี้สินเงินประกันส่วนที่หักถูกล้างด้วย "ใบเสร็จค่าเสียหาย" เอง โดยเลือกแหล่งเงินแถวนี้
--          ผู้ดูแลผูกบัญชี NextAcc ของแถวนี้ = บัญชีเดียวกับ SECURITY_DEPOSIT_LIABILITY (ผังโรงแรม NextAcc 21530)
--          ⇒ ใบเสร็จลง Dr เงินประกันรอคืน / Cr รายได้ค่าเสียหาย + ภาษีขาย (ไม่มีเงินเข้าธนาคารซ้ำ)
--    Channel_Type OTHER, Customer_Visible = 0 (ลูกค้าไม่เห็น), Staff_Visible = 1 (พนักงานเลือกในหน้าออกใบเสร็จได้)
--
-- 2) Payment_Slips.Claimed_Amount — ยอดที่ลูกค้าแจ้งตอนแนบสลิปเองในหน้า /Payment/Pay (สแกน QR แนบสลิป)
--    สลิปลูกค้า = "รอตรวจ": ยังไม่ลง Payment_History/ใบเสร็จ/บัญชี และไม่ยืนยันการจอง จนกว่าเจ้าหน้าที่อนุมัติที่
--    Account/SlipVerification (PaymentService.ApproveReservationSlip ลงรับเงินด้วยยอดนี้)
--    ยังไม่รันไฟล์นี้ = หน้า Pay ลงรับเงินทันทีแบบเดิม แต่ไม่เลื่อนสถานะใบจองจากสลิปที่ยังไม่ตรวจ
--
-- idempotent — รันซ้ำได้ (COL_LENGTH / NOT EXISTS guard), ไม่แตะค่าที่ผู้ดูแลแก้แล้ว, ไม่ลบข้อมูล
-- ต้องรัน PHASE19_20 (คอลัมน์แคตตาล็อก Account_Paid_How) + PHASE19_24 มาก่อนเพื่อให้ได้ครบ (ไม่มี = ข้ามส่วนนั้น)
-- หลังรัน: recycle App Pool (หรือรอ 5 นาทีให้ cache ตรวจคอลัมน์หมดอายุ)
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
GO

-- 1a) แถวแหล่งเงิน "หักจากเงินประกัน"
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NULL
BEGIN
    PRINT N'⚠ ไม่มีตาราง Account_Paid_How — ข้ามการเพิ่มแหล่งเงินหักจากเงินประกัน';
END
ELSE
BEGIN
    DECLARE @nm NVARCHAR(200) = N'หักจากเงินประกัน (Security deposit)';
    DECLARE @exists BIT = 0;

    IF EXISTS (SELECT 1 FROM dbo.Account_Paid_How WHERE Paid_How = @nm) SET @exists = 1;
    IF @exists = 0 AND COL_LENGTH('dbo.Account_Paid_How', 'Channel_Code') IS NOT NULL
    BEGIN
        DECLARE @n INT = 0;
        EXEC sp_executesql N'SELECT @c = COUNT(*) FROM dbo.Account_Paid_How WHERE Channel_Code = N''SECURITY_DEPOSIT_OFFSET''',
            N'@c INT OUTPUT', @c = @n OUTPUT;
        IF @n > 0 SET @exists = 1;
    END

    IF @exists = 0
    BEGIN
        INSERT INTO dbo.Account_Paid_How (Paid_How, Status) VALUES (@nm, 'True');
        PRINT N'เพิ่มแหล่งเงิน "' + @nm + N'" (ผูกบัญชี NextAcc = บัญชีเดียวกับ SECURITY_DEPOSIT_LIABILITY ที่หน้า Accounting Integration)';
    END
    ELSE
        PRINT N'มีแหล่งเงินหักจากเงินประกันอยู่แล้ว — ไม่เพิ่มซ้ำ';
END
GO

-- 1b) ตั้งค่าแคตตาล็อกของแถวนี้ (เฉพาะคอลัมน์ที่มี + เฉพาะเมื่อยังไม่จัดชนิด — ไม่ทับค่าที่ผู้ดูแลแก้แล้ว)
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Account_Paid_How', 'Channel_Code') IS NOT NULL
   AND COL_LENGTH('dbo.Account_Paid_How', 'Channel_Type') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'
        UPDATE dbo.Account_Paid_How
           SET Channel_Code = N''SECURITY_DEPOSIT_OFFSET'', Channel_Type = N''OTHER''
         WHERE Paid_How = N''หักจากเงินประกัน (Security deposit)''
           AND Channel_Type IS NULL
           AND NOT EXISTS (SELECT 1 FROM dbo.Account_Paid_How x
                            WHERE x.Channel_Code = N''SECURITY_DEPOSIT_OFFSET'' AND x.Paid_How <> N''หักจากเงินประกัน (Security deposit)'');';
    EXEC sp_executesql @sql;
    PRINT N'ตั้งแคตตาล็อกแถวหักจากเงินประกัน: Channel_Code = SECURITY_DEPOSIT_OFFSET, Channel_Type = OTHER (เฉพาะแถวที่ยังไม่จัดชนิด)';

    IF COL_LENGTH('dbo.Account_Paid_How', 'Customer_Visible') IS NOT NULL
        EXEC sp_executesql N'UPDATE dbo.Account_Paid_How SET Customer_Visible = 0
                              WHERE Channel_Code = N''SECURITY_DEPOSIT_OFFSET'' AND Customer_Visible <> 0;';
    IF COL_LENGTH('dbo.Account_Paid_How', 'Staff_Visible') IS NOT NULL
        EXEC sp_executesql N'UPDATE dbo.Account_Paid_How SET Staff_Visible = 1
                              WHERE Channel_Code = N''SECURITY_DEPOSIT_OFFSET'' AND Staff_Visible IS NULL;';
    IF COL_LENGTH('dbo.Account_Paid_How', 'Requires_Slip') IS NOT NULL
        EXEC sp_executesql N'UPDATE dbo.Account_Paid_How SET Requires_Slip = 0
                              WHERE Channel_Code = N''SECURITY_DEPOSIT_OFFSET'' AND Requires_Slip <> 0;';
    IF COL_LENGTH('dbo.Account_Paid_How', 'Instructions') IS NOT NULL
        EXEC sp_executesql N'UPDATE dbo.Account_Paid_How
                                SET Instructions = N''ใช้ออก "ใบเสร็จค่าเสียหาย" ที่หักจากเงินประกันความเสียหายแบบโอนเท่านั้น — ล้างหนี้สินเงินประกันรอคืน (ไม่ใช่เงินเข้าใหม่) · ห้ามใช้กับการรับเงินอื่น''
                              WHERE Channel_Code = N''SECURITY_DEPOSIT_OFFSET'' AND Instructions IS NULL;';
    IF COL_LENGTH('dbo.Account_Paid_How', 'Sort_Order') IS NOT NULL
        EXEC sp_executesql N'UPDATE dbo.Account_Paid_How SET Sort_Order = 900
                              WHERE Channel_Code = N''SECURITY_DEPOSIT_OFFSET'' AND Sort_Order = 100;';
END
ELSE
    PRINT N'⚠ ยังไม่มีคอลัมน์แคตตาล็อก (รัน PHASE19_20) — แถวหักจากเงินประกันถูกค้นด้วยชื่อแทน และลูกค้าอาจเห็นแถวนี้จนกว่าจะรัน PHASE19_20 แล้วรันไฟล์นี้ซ้ำ';
GO

-- 2) Payment_Slips.Claimed_Amount
IF OBJECT_ID('dbo.Payment_Slips', 'U') IS NULL
    PRINT N'⚠ ไม่มีตาราง Payment_Slips — ข้าม Claimed_Amount';
ELSE IF COL_LENGTH('dbo.Payment_Slips', 'Claimed_Amount') IS NULL
BEGIN
    ALTER TABLE dbo.Payment_Slips ADD Claimed_Amount DECIMAL(18, 2) NULL;
    PRINT N'เพิ่ม Payment_Slips.Claimed_Amount (สลิปที่ลูกค้าแนบเอง = รอเจ้าหน้าที่ตรวจก่อนลงรับเงิน)';
END
ELSE
    PRINT N'มี Payment_Slips.Claimed_Amount อยู่แล้ว — ไม่แตะ';
GO

PRINT '';
PRINT N'หลังรัน:';
PRINT N'  1) Admin → Accounting Integration → วิธีจ่ายเงิน → บัญชี NextAcc: ผูก "หักจากเงินประกัน (Security deposit)"';
PRINT N'     กับบัญชีเดียวกับ SECURITY_DEPOSIT_LIABILITY (เช่น 21530) แล้วกด 🩺 ตรวจสุขภาพการเชื่อมต่อ';
PRINT N'  2) หักค่าเสียหายจากเงินประกันโอน (หน้าเช็คอาท์) → ออกใบเสร็จค่าเสียหายด้วยแหล่งเงินนี้ (ระบบบอกชื่อแหล่งเงินในข้อความ)';
PRINT N'  3) สลิปที่ลูกค้าแนบเองในหน้าชำระเงิน → ตรวจ/อนุมัติที่ บัญชี → ตรวจสอบสลิป (อนุมัติ = ลงรับเงิน + ใบเสร็จ + ยืนยันการจอง)';
GO
