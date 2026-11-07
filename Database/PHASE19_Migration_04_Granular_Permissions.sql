-- ============================================================================
-- PHASE19 Migration 04 — แยกสิทธิ์ "ศูนย์ตั้งค่า" ออกเป็นโมดูลย่อย
-- ============================================================================
-- ปัญหาเดิม: ทุกอย่างในศูนย์ตั้งค่าอยู่ใต้ SYS_SETTINGS โมดูลเดียว
--   ⇒ อยากให้ทีมการตลาดแก้เนื้อหาเว็บ ต้องให้ SYS_SETTINGS
--   ⇒ เท่ากับให้สิทธิ์ตั้งค่าบัญชี NextAcc / ผังบัญชี / คิว sync ไปด้วย
--
-- แยกเป็น 4 โมดูลใหม่ (SYS_SETTINGS ยังอยู่ ครอบส่วนที่เหลือ):
--   WEB_CONTENT     เนื้อหาเว็บไซต์ & รูปภาพ  — หน้าแรก โปรโมชั่น สิ่งอำนวยความสะดวก
--                                              สถานที่ใกล้เคียง เบิกของใช้ ฉุกเฉิน เกี่ยวกับเรา รูปสินค้า
--   SVC_GUEST       ตั้งค่าบริการในที่พัก      — รูมเซอร์วิส (เวลา/ค่าบริการ) Guest Portal
--   SYS_CHANNEL     ช่องทางติดต่อ & AI        — Token LINE/Facebook อีเมล OTA ตั้งค่า AI
--   SYS_ACCOUNTING  ตั้งค่าบัญชี & ภาษี       — NextAcc ผังบัญชี ลงบัญชีรายสินค้า
--
-- ⚠ สำคัญ: กลุ่มที่ "มี SYS_SETTINGS อยู่แล้ว" จะได้โมดูลใหม่ทั้งหมดโดยอัตโนมัติ
--   ไม่งั้นทุกคนจะเสียสิทธิ์ที่เคยมีทันทีที่ deploy (โมดูลที่ไม่มีแถว = ไม่มีสิทธิ์)
--
-- ปลอดภัย: รันซ้ำได้ ไม่ลบสิทธิ์เดิม ไม่ลดสิทธิ์ใคร
-- ต้องรัน PHASE18_Migration_23 (ระบบกลุ่มสิทธิ์) มาก่อน
-- ============================================================================

SET NOCOUNT ON;

IF OBJECT_ID('dbo.Permission_Group_Modules', 'U') IS NULL
BEGIN
    PRINT 'ยังไม่มีตาราง Permission_Group_Modules — รัน PHASE18_Migration_23 ก่อน (ข้ามไมเกรชันนี้)';
    RETURN;
END

DECLARE @New TABLE (Code VARCHAR(40));
INSERT INTO @New (Code) VALUES
    ('WEB_CONTENT'), ('SVC_GUEST'), ('SYS_CHANNEL'), ('SYS_ACCOUNTING');

-- ── 1) กลุ่มที่มี SYS_SETTINGS อยู่แล้ว → ได้โมดูลใหม่ตามสิทธิ์เดิมเป๊ะ ─────────
-- ใช้ค่า Can_View / Can_Access ของ SYS_SETTINGS เป็นตัวตั้ง เพื่อไม่ให้ใครได้เพิ่มหรือเสีย
INSERT INTO Permission_Group_Modules (Group_ID, Module_Code, Can_View, Can_Access)
SELECT s.Group_ID, n.Code, s.Can_View, s.Can_Access
FROM Permission_Group_Modules s
CROSS JOIN @New n
WHERE s.Module_Code = 'SYS_SETTINGS'
  AND NOT EXISTS (SELECT 1 FROM Permission_Group_Modules p
                  WHERE p.Group_ID = s.Group_ID AND p.Module_Code = n.Code);

PRINT N'ให้สิทธิ์โมดูลใหม่ตามสิทธิ์ SYS_SETTINGS เดิม: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว';
GO

-- ── 2) กลุ่มมาตรฐาน "ผู้ดูแล (Admin)" — เดิมเข้าศูนย์ตั้งค่าได้ ────────────────
-- เผื่อกรณีกลุ่ม Admin ถูกตั้งไว้โดยไม่มีแถว SYS_SETTINGS (ผู้ดูแลเคยปิดไว้เอง)
-- ก็ไม่ต้องทำอะไร — ข้อ 1 ครอบคลุมแล้ว. ส่วนกลุ่ม Owner ได้ทุกอย่างจากโค้ดอยู่แล้ว
DECLARE @OwnerId INT = (SELECT TOP 1 ID FROM Permission_Groups WHERE Base_Role = N'Owner' AND Is_System = 1);

INSERT INTO Permission_Group_Modules (Group_ID, Module_Code, Can_View, Can_Access)
SELECT @OwnerId, v.Code, 1, 1
FROM (VALUES ('WEB_CONTENT'), ('SVC_GUEST'), ('SYS_CHANNEL'), ('SYS_ACCOUNTING')) AS v(Code)
WHERE @OwnerId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM Permission_Group_Modules p
                  WHERE p.Group_ID = @OwnerId AND p.Module_Code = v.Code);

PRINT N'กลุ่ม Owner ได้โมดูลใหม่: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว';
GO

-- ── ตรวจผล: แต่ละกลุ่มมีสิทธิ์อะไรบ้างในหมวดตั้งค่า ─────────────────────────
SELECT g.Group_Name AS กลุ่ม,
       MAX(CASE WHEN m.Module_Code = 'SYS_SETTINGS'   AND m.Can_Access = 1 THEN N'✓' ELSE N'' END) AS [ตั้งค่าระบบ],
       MAX(CASE WHEN m.Module_Code = 'WEB_CONTENT'    AND m.Can_Access = 1 THEN N'✓' ELSE N'' END) AS [เนื้อหาเว็บ],
       MAX(CASE WHEN m.Module_Code = 'SVC_GUEST'      AND m.Can_Access = 1 THEN N'✓' ELSE N'' END) AS [บริการในที่พัก],
       MAX(CASE WHEN m.Module_Code = 'SYS_CHANNEL'    AND m.Can_Access = 1 THEN N'✓' ELSE N'' END) AS [ช่องทาง&AI],
       MAX(CASE WHEN m.Module_Code = 'SYS_ACCOUNTING' AND m.Can_Access = 1 THEN N'✓' ELSE N'' END) AS [บัญชี&ภาษี]
FROM Permission_Groups g
LEFT JOIN Permission_Group_Modules m ON m.Group_ID = g.ID
GROUP BY g.ID, g.Group_Name
ORDER BY g.Group_Name;

PRINT '';
PRINT N'ต่อจากนี้: ศูนย์ตั้งค่า → กลุ่มสิทธิ์ผู้ใช้ → เอาเครื่องหมายออกจากโมดูลที่ไม่ต้องการให้กลุ่มนั้นเข้าถึง';
PRINT N'เช่น กลุ่ม "การตลาด" ให้เหลือแค่ เนื้อหาเว็บ → แก้สถานที่ใกล้เคียง/โปรโมชั่นได้ แต่เข้าตั้งค่าบัญชีไม่ได้';
