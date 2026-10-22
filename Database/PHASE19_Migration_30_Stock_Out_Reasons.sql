-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 30: เหตุผลการเบิกสินค้า (Stock-out reasons) + การอนุมัติ/หลักฐาน/กลับรายการ
-- ════════════════════════════════════════════════════════════════════════════
-- ใช้กับหน้า Product/Stock.aspx (บันทึกเบิกสินค้า / ใช้ภายใน) และ Admin/Settings/StockOutReasons.aspx
--
-- 1) ตาราง Stock_Out_Reason (แคตตาล็อกเหตุผล) + seed 15 เหตุผล
--    แต่ละเหตุผลกำหนด: บัญชีค่าใช้จ่าย (Gl_Mapping_Key → Accounting_Account_Mapping),
--    ต้องคิดภาษีขายเสมือนขายหรือไม่ (Deemed_Sale_Vat — ม.77/1(8) ฐานราคาตลาด ม.79/3),
--    ฐานภาษี (MARKET = ราคาขาย Product.Sell_Price ถอด VAT / COST = ต้นทุน),
--    ต้องอนุมัติ (หุ้นส่วนผู้จัดการ/Owner) และต้องแนบหลักฐาน (ภาพถ่าย/บันทึกการทำลาย)
--    ⚠ ค่าเริ่มต้นทุกแถวมาจากงานค้นคว้า (audit C) — "ให้ผู้ทำบัญชียืนยัน" ก่อนเปิดใช้จริง
--
-- 2) Product_Out เพิ่มคอลัมน์: Out_Reason_Code, Recorded_By, Approved_By, Approved_At,
--    Approval_Status (PENDING|APPROVED|REJECTED), Evidence_Path, Nexaacc_Journal_Ref, Is_Reversed
--    แถวเดิมทั้งหมด: Out_Reason_Code = NULL (= แถวเก่า/ขาย), Is_Reversed = 0
--
-- 3) กันแถวเบิกเก่าที่พิมพ์หมายเหตุว่า 'ขาย' (หน้า Stock เดิมเขียน Remark = หมายเหตุ) ถูก POS daily rollup
--    หยิบไปเป็นยอดขาย: mark Pos_Rollup_Ref = 'EXCLUDED-WRITEOFF' (rollup เลือกเฉพาะ Pos_Rollup_Ref IS NULL)
--    หน้า Stock ใหม่ไม่เขียน Remark = 'ขาย' อีกแล้ว (Remark = 'เบิก:<ชื่อเหตุผล>')
--
-- 4) mapping บัญชีใหม่ (Accounting_Account_Mapping) — เพิ่มเฉพาะที่ยังไม่มี ไม่แตะค่าที่ตั้งไว้แล้ว
--    รหัสแนะนำอ้างผังมาตรฐาน NextAcc (Wachira-d/Accounting Services/ChartOfAccountTemplates.cs):
--      INVENTORY_LOSS      ← คัดลอกจาก STOCK_LOSS_EXPENSE ถ้ามี ไม่งั้น 57120 ขาดทุนจากการด้อยค่า (:323)
--      PROMOTION_EXPENSE   53120 ค่าโฆษณาและส่งเสริมการขาย (:230)
--      STAFF_WELFARE       54123 สวัสดิการพนักงาน (:248)
--      ENTERTAINMENT       54460 ค่ารับรอง / เลี้ยงรับรอง (:266)
--      DONATION            54940 เงินบริจาค/การกุศล (:292)
--      OWNER_DRAWINGS      (ว่าง — ผูกเอง) ผังห้างหุ้นส่วน 31030 ถอนใช้ส่วนตัว – หุ้นส่วนผู้จัดการ (:387) หรือ 11330 ลูกหนี้กรรมการ (:36)
--      OPERATING_SUPPLIES  (ว่าง — ผูกเอง) ผังโรงแรม 51520 ต้นทุน Amenities/ของใช้ในห้อง (:482) หรือ 52120 ค่าวัสดุสิ้นเปลือง (:223)
--      COGS_ADJUST         ← คัดลอกจาก INVENTORY_VARIANCE ถ้ามี ไม่งั้น 51210
--      OUTPUT_VAT_DEEMED_EXPENSE (ว่าง = ใช้บัญชีค่าใช้จ่ายของเหตุผลนั้น) — ภาษีขายที่กิจการรับภาระเอง
--        ⚠ ห้ามผูกกับบัญชีที่ชื่อมีคำว่า "ภาษีขาย"/"ภาษีซื้อ" หรือรหัส 2191x/116x: NextAcc ภ.พ.30 อ่านบรรทัด JE
--          ตามชื่อ/รหัส (TaxService.cs:1277-1293) → ขาเดบิตจะหักล้างภาษีขายจนไม่เข้า ภ.พ.30 (โค้ดกันไว้แล้ว)
--    mapping ที่ยังว่าง → โค้ดลงบัญชี STOCK_LOSS_EXPENSE แทน (พฤติกรรมเดิม) และเขียน log เตือน
--
-- การลงบัญชี (เฉพาะแถว APPROVED, ref STOCKOUT-{Product_Out.ID}, /api/integration/journals idempotent):
--   Dr <บัญชีของเหตุผล> / Cr INVENTORY  (ต้นทุน)
--   ถ้า Deemed_Sale_Vat=1 และกิจการจด VAT: Dr <บัญชีของเหตุผล หรือ OUTPUT_VAT_DEEMED_EXPENSE> / Cr OUTPUT_VAT (21911)
--     = 7% ของฐาน (ราคาตลาด) — NextAcc นับเข้า ภ.พ.30 ผ่าน JE fallback (TaxService.cs:1229-1334, ฐาน = VAT ÷ 7%)
--   กลับรายการ: /api/integration/journals/reverse (ref STOCKOUT-REV-{ID}) — ต้นฉบับถูกตัดออกจาก ภ.พ.30 (TaxService.cs:1245-1246)
--
-- idempotent — รันซ้ำได้, ไม่ลบข้อมูล, ไม่แตะค่าที่ผู้ดูแลแก้ไว้แล้ว
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- 1) ตารางแคตตาล็อก
IF OBJECT_ID('dbo.Stock_Out_Reason', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Stock_Out_Reason (
        Code              NVARCHAR(30)  NOT NULL CONSTRAINT PK_Stock_Out_Reason PRIMARY KEY,
        Name_TH           NVARCHAR(100) NOT NULL,
        Gl_Mapping_Key    NVARCHAR(60)  NULL,
        Deemed_Sale_Vat   BIT           NOT NULL CONSTRAINT DF_SOR_Deemed DEFAULT 0,
        Vat_Base          NVARCHAR(10)  NOT NULL CONSTRAINT DF_SOR_VatBase DEFAULT 'MARKET',   -- MARKET | COST
        Requires_Approval BIT           NOT NULL CONSTRAINT DF_SOR_Approval DEFAULT 0,
        Requires_Evidence BIT           NOT NULL CONSTRAINT DF_SOR_Evidence DEFAULT 0,
        Is_Active         BIT           NOT NULL CONSTRAINT DF_SOR_Active DEFAULT 1,
        Sort_Order        INT           NOT NULL CONSTRAINT DF_SOR_Sort DEFAULT 100,
        Help_TH           NVARCHAR(500) NULL
    );
    PRINT N'สร้างตาราง Stock_Out_Reason';
END
ELSE
    PRINT N'มีตาราง Stock_Out_Reason อยู่แล้ว';
GO

-- 1b) seed (เพิ่มเฉพาะรหัสที่ยังไม่มี — ไม่ทับค่าที่แก้ไว้)
DECLARE @seed TABLE (Code NVARCHAR(30), Name_TH NVARCHAR(100), Gl NVARCHAR(60), Deemed BIT, VatBase NVARCHAR(10),
                     Appr BIT, Evid BIT, Sort INT, Help NVARCHAR(500));
INSERT INTO @seed VALUES
 (N'DAMAGE', N'ของแตก/เสียหาย', N'INVENTORY_LOSS', 0, N'COST', 1, 1, 10,
  N'ไม่ถือเป็นการขายเมื่อมีหลักฐานความเสียหาย (ม.77/1(8)) — ไม่มีภาษีขาย, ภาษีซื้อเดิมไม่ต้องคืน. ตัดเป็นรายจ่ายได้ในรอบที่เสียหายจริง: ทำรายงานสินค้าเสียหาย (รายการ จำนวน ต้นทุน สาเหตุ ผู้อนุมัติ) + ภาพถ่าย ตามแนวคำสั่ง ป.79/2541 (แก้โดย ป.84/2542). ซากที่ขายได้ต้องออกใบขายตามปกติ. ให้ผู้ทำบัญชียืนยัน'),
 (N'EXPIRED', N'หมดอายุ/เน่าเสีย', N'INVENTORY_LOSS', 0, N'COST', 1, 1, 20,
  N'ไม่มีภาษีขายเมื่อทำลายจริงและมีหลักฐาน. ทำลายต่อหน้าพยาน มีภาพถ่าย/บันทึกการทำลาย ตามแนว ป.79/2541. อาหารสด/แช่แข็งที่เก็บรักษาไม่ได้โดยสภาพ ทำลายได้ทันทีแต่ต้องมีหลักฐาน (ความมั่นใจกลาง). ค่าเผื่อสินค้าเสื่อมที่ยังไม่ทำลายหักไม่ได้ (ม.65 ตรี(1)). ให้ผู้ทำบัญชียืนยัน'),
 (N'DESTROYED', N'ทำลายตามขั้นตอน (ป.79/2541)', N'INVENTORY_LOSS', 0, N'COST', 1, 1, 30,
  N'คำสั่ง ป.79/2541 (แก้โดย ป.84/2542): ทำลายโดยมีพยาน + ผู้สอบบัญชีสังเกตการณ์/รับรอง, ถ่ายภาพ/วิดีโอ, บันทึกการทำลายให้ผู้ร่วมลงนาม. ข้อ 3.2 เดิมให้แจ้งสรรพากรพื้นที่ล่วงหน้า 30 วัน (ฉบับแก้ไขอาจให้ทางเลือกทำลายต่อหน้าผู้สอบบัญชี — ตรวจทาน). พิมพ์ "รายงานสินค้าเสียหาย/ทำลาย" แนบ. ไม่มีภาษีขาย. ให้ผู้ทำบัญชียืนยัน'),
 (N'LOST_THEFT', N'สูญหาย/ขาดจากการนับ', N'INVENTORY_LOSS', 1, N'MARKET', 1, 1, 40,
  N'สินค้าขาดจากรายงานสินค้าและวัตถุดิบ/ถูกโจรกรรม ถือเป็นการขาย ม.77/1(8)(จ) (ข้อหารือ กค 0706/พ./6444) — ภาษีขาย 7% จากราคาตลาดวันที่ตรวจพบ (ม.79/3) เข้า ภ.พ.30 โดยไม่ต้องออกใบกำกับ. แนบบันทึกแจ้งความถ้าถูกขโมย; หักเงินชดเชยประกัน. ภาษีขายที่รับภาระเองหักรายจ่ายไม่ได้ ม.65 ตรี(6 ทวิ) [ตรวจทาน]. ให้ผู้ทำบัญชียืนยัน'),
 (N'PROMO_SAMPLE', N'ตัวอย่างสินค้า/ของแจกส่งเสริมการขาย', N'PROMOTION_EXPENSE', 0, N'MARKET', 0, 0, 50,
  N'ประกาศอธิบดีฯ VAT ฉบับที่ 40 ข้อ 2(7): สินค้าตัวอย่าง/ของแจกเพื่อส่งเสริมการขาย ไม่รวมเป็นฐานภาษี. เก็บเอกสารกิจกรรม รายชื่อ/จำนวนผู้รับ (ควรพิมพ์ "ตัวอย่าง ห้ามจำหน่าย"). CIT: แจกทั่วไปไม่เจาะจงผู้รับ = ค่าส่งเสริมการขาย หักได้เต็ม. ให้ผู้ทำบัญชียืนยัน'),
 (N'GIFT_WITH_PURCHASE', N'ของแถมพร้อมการขาย (ระบุในใบกำกับ)', N'COGS', 0, N'MARKET', 0, 0, 60,
  N'ฉบับที่ 40 ข้อ 2(1): ของแถมพร้อมการขายในคราวเดียวกัน ไม่เสียภาษีเพิ่ม — ต้องระบุรายการของแถม (ราคา 0) ในใบกำกับภาษีของการขายนั้น (ป.86/2542 ข้อ 13). ถ้าทำได้ ให้ใส่เป็นบรรทัดราคา 0 ในบิลที่หน้าขายสินค้าแทนการเบิกที่นี่. ต้นทุนรวมในต้นทุนขาย. ให้ผู้ทำบัญชียืนยัน'),
 (N'GIVEAWAY', N'แจกฟรี/ของรางวัลชิงโชค', N'PROMOTION_EXPENSE', 1, N'MARKET', 1, 0, 70,
  N'ของแจก/ของรางวัลที่ไม่ผูกกับการขาย ถือเป็นการขาย ม.77/1(8)(ง) ภาษีขายจากราคาตลาด (ม.79/3). ผู้ได้รางวัลเป็นบุคคลธรรมดา: หัก ณ ที่จ่าย 5% (ม.40(8)) ทำแยก; การจับรางวัลต้องขออนุญาตตาม พ.ร.บ.การพนัน. ให้ผู้ทำบัญชียืนยัน'),
 (N'BUSINESS_GIFT', N'ของขวัญ/ของชำร่วยตามประเพณี', N'ENTERTAINMENT', 0, N'MARKET', 1, 0, 80,
  N'ฉบับที่ 40 ข้อ 2(6): ของขวัญ/ของชำร่วยตามประเพณีทางธุรกิจ ราคาไม่เกินสมควร ไม่รวมเป็นฐานภาษี. CIT: ให้บุคคลเจาะจง = ค่ารับรอง ม.65 ตรี(4) + กฎกระทรวง 143 (วงเงิน 0.3% ของรายได้/ทุน ไม่เกิน 10 ล้าน, ของขวัญไม่เกิน 2,000 บาท/คน/ครั้ง — ตรวจทาน). ราคาเกินสมควร → ใช้ "แจกฟรี". ให้ผู้ทำบัญชียืนยัน'),
 (N'STAFF_MEAL', N'อาหารพนักงานระหว่างงาน', N'STAFF_WELFARE', 0, N'MARKET', 0, 0, 90,
  N'ฉบับที่ 40 ข้อ 2(8): อาหาร/เครื่องดื่มที่นายจ้างจัดให้ลูกจ้างระหว่างเวลาทำงานตามระเบียบสวัสดิการ ไม่รวมเป็นฐานภาษี. ต้องมีระเบียบสวัสดิการ + ใบเบิกวัตถุดิบ. CIT: สวัสดิการทั่วไป หักได้ ไม่ถือเป็นเงินได้ลูกจ้าง. ให้ผู้ทำบัญชียืนยัน'),
 (N'STAFF_WELFARE', N'สวัสดิการพนักงานอื่น', N'STAFF_WELFARE', 1, N'MARKET', 1, 0, 100,
  N'สวัสดิการที่ไม่อยู่ในฉบับที่ 40 (เช่น ของขวัญปีใหม่จากสต็อก) โดยหลักถือเป็นการขาย ม.77/1(8)(ง) ฐานราคาตลาด และอาจเป็นเงินได้ลูกจ้าง ม.40(1). ถ้าเป็นสวัสดิการตามระเบียบที่เข้าฉบับที่ 40 ข้อ 2(8) ผู้ทำบัญชีปิดภาษีขายของเหตุผลนี้ได้. ให้ผู้ทำบัญชียืนยัน'),
 (N'OWNER_USE', N'หุ้นส่วน/เจ้าของนำไปใช้ส่วนตัว', N'OWNER_DRAWINGS', 1, N'MARKET', 1, 0, 110,
  N'ถือเป็นการขาย ม.77/1(8)(ง) ภาษีขาย 7% จากราคาตลาด (ม.79/3) — ความมั่นใจสูง. CIT: รายจ่ายส่วนตัว หักไม่ได้ ม.65 ตรี(3) → ลงบัญชีถอนใช้ส่วนตัว/บัญชีเดินสะพัดหุ้นส่วน (ไม่ใช่ค่าใช้จ่าย) — ผูก OWNER_DRAWINGS ก่อนใช้. ให้ผู้ทำบัญชียืนยัน'),
 (N'COMPLIMENTARY_GUEST', N'ให้ลูกค้าฟรี/ชดเชย', N'ENTERTAINMENT', 1, N'MARKET', 1, 0, 120,
  N'ความมั่นใจต่ำ-กลาง (ควรขอข้อหารือ): ให้แขก/เอเจนต์ฟรีโดยไม่มีค่าตอบแทน แนวระมัดระวังถือเป็นการขาย ม.77/1(8)(ง) ฐานราคาตลาด. ของแถมผูกกับการขายในบิลเดียวกัน → "ของแถมพร้อมการขาย"; ชดเชยด้วยการลดราคา = ส่วนลด/ใบลดหนี้. FAM trip/สื่อ = ค่าส่งเสริมการขาย, ให้บุคคลเจาะจง = ค่ารับรอง. ให้ผู้ทำบัญชียืนยัน'),
 (N'INTERNAL_OPERATIONS', N'ใช้ในการดำเนินงาน (เช่น ของใช้ในห้องพัก)', N'OPERATING_SUPPLIES', 0, N'COST', 0, 0, 130,
  N'ใช้ในการให้บริการของตนเองโดยตรง (เช่น น้ำดื่ม/amenity ที่รวมในค่าห้อง) ไม่ถือเป็นการขาย — ข้อยกเว้นใน ม.77/1(8)(ง); มูลค่าอยู่ในค่าห้องที่เสีย VAT แล้ว (ความมั่นใจสูง). ใช้ใบเบิกภายใน. CIT: ต้นทุนบริการ หักได้. ผูก OPERATING_SUPPLIES ก่อนใช้'),
 (N'DONATION', N'บริจาค', N'DONATION', 1, N'MARKET', 1, 1, 140,
  N'บริจาคสินค้า = นำสินค้าไปใช้ ถือเป็นการขาย ม.77/1(8)(ง) ฐานราคาตลาด (เว้นแต่มีข้อยกเว้นเฉพาะ — ตรวจทาน). CIT: เงินบริจาคสาธารณกุศลหักได้ไม่เกิน 2% ของกำไรสุทธิ ม.65 ตรี(3) (ตรวจทาน). เก็บหนังสืออนุโมทนา/ใบรับบริจาคเป็นหลักฐาน. ให้ผู้ทำบัญชียืนยัน'),
 (N'COUNT_ADJUST', N'ปรับยอดจากการตรวจนับ (อธิบายสาเหตุได้)', N'COGS_ADJUST', 0, N'COST', 1, 0, 150,
  N'ใช้เมื่อส่วนต่างอธิบายได้ (บันทึกผิด/หน่วยนับผิด) ไม่ใช่สินค้าหาย. สินค้าขาดโดยไม่ทราบสาเหตุให้ใช้ "สูญหาย/ขาดจากการนับ" เพราะสินค้าขาดจากรายงาน = ถือเป็นการขาย ม.77/1(8)(จ). ลงส่วนต่างสินค้าที่ต้นทุน. ให้ผู้ทำบัญชียืนยัน'),
 (N'RETURN_TO_VENDOR', N'คืนสินค้าผู้ขาย', N'PURCHASE_RETURNS', 0, N'COST', 0, 0, 900,
  N'สร้างอัตโนมัติจากหน้าคืนสินค้า/ใบลดหนี้ผู้ขาย (ลงบัญชีที่หน้านั้น ref PRT-…) — ไม่แสดงในหน้าเบิกสินค้า และหน้าเบิกไม่ลงบัญชีซ้ำ. ภาษีซื้อลดตามใบลดหนี้ของผู้ขาย (ม.82/10, ม.86/10).');

INSERT INTO dbo.Stock_Out_Reason (Code, Name_TH, Gl_Mapping_Key, Deemed_Sale_Vat, Vat_Base, Requires_Approval, Requires_Evidence, Is_Active, Sort_Order, Help_TH)
SELECT s.Code, s.Name_TH, s.Gl, s.Deemed, s.VatBase, s.Appr, s.Evid, 1, s.Sort, s.Help
FROM @seed s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Stock_Out_Reason r WHERE r.Code = s.Code);
PRINT N'seed Stock_Out_Reason: เพิ่ม ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' รายการ';
GO

-- 2) Product_Out คอลัมน์ใหม่
IF OBJECT_ID('dbo.Product_Out', 'U') IS NULL
BEGIN
    PRINT N'ไม่มีตาราง Product_Out — ข้าม';
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.Product_Out', 'Out_Reason_Code') IS NULL
        ALTER TABLE dbo.Product_Out ADD Out_Reason_Code NVARCHAR(30) NULL;
    IF COL_LENGTH('dbo.Product_Out', 'Recorded_By') IS NULL
        ALTER TABLE dbo.Product_Out ADD Recorded_By NVARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Product_Out', 'Approved_By') IS NULL
        ALTER TABLE dbo.Product_Out ADD Approved_By NVARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Product_Out', 'Approved_At') IS NULL
        ALTER TABLE dbo.Product_Out ADD Approved_At DATETIME NULL;
    IF COL_LENGTH('dbo.Product_Out', 'Approval_Status') IS NULL
        ALTER TABLE dbo.Product_Out ADD Approval_Status NVARCHAR(20) NULL;   -- PENDING | APPROVED | REJECTED
    IF COL_LENGTH('dbo.Product_Out', 'Evidence_Path') IS NULL
        ALTER TABLE dbo.Product_Out ADD Evidence_Path NVARCHAR(400) NULL;
    IF COL_LENGTH('dbo.Product_Out', 'Nexaacc_Journal_Ref') IS NULL
        ALTER TABLE dbo.Product_Out ADD Nexaacc_Journal_Ref NVARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Product_Out', 'Is_Reversed') IS NULL
        ALTER TABLE dbo.Product_Out ADD Is_Reversed BIT NOT NULL CONSTRAINT DF_Product_Out_Is_Reversed DEFAULT 0;
    PRINT N'Product_Out: เพิ่มคอลัมน์เหตุผล/อนุมัติ/หลักฐาน/กลับรายการ (ถ้ายังไม่มี)';
END
GO

IF OBJECT_ID('dbo.Product_Out', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Product_Out', 'Out_Reason_Code') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_Out_Reason_Status' AND object_id = OBJECT_ID('dbo.Product_Out'))
BEGIN
    EXEC sp_executesql N'
        CREATE NONCLUSTERED INDEX IX_Product_Out_Reason_Status
            ON dbo.Product_Out (Out_Reason_Code, Approval_Status, DateTime_Out)
            WHERE Out_Reason_Code IS NOT NULL;';
    PRINT N'สร้าง index IX_Product_Out_Reason_Status';
END
GO

-- 3) แถวเบิกเก่าที่ Remark = 'ขาย' (หน้า Stock เดิมเขียนหมายเหตุลง Remark) → กัน POS rollup หยิบไปเป็นยอดขาย
IF OBJECT_ID('dbo.Product_Out', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Product_Out', 'Pos_Rollup_Ref') IS NOT NULL
   AND COL_LENGTH('dbo.Product_Out', 'OutType') IS NOT NULL
BEGIN
    -- dynamic SQL: คอลัมน์อาจยังไม่มีตอน compile batch (SQL Server ไม่ defer การตรวจชื่อคอลัมน์)
    DECLARE @n INT;
    EXEC sp_executesql N'
        UPDATE dbo.Product_Out
           SET Pos_Rollup_Ref = N''EXCLUDED-WRITEOFF''
         WHERE Remark = N''ขาย''
           AND OutType IN (''WRITEOFF'', ''ADJUSTMENT_LOSS'')
           AND Pos_Rollup_Ref IS NULL;
        SET @cnt = @@ROWCOUNT;', N'@cnt INT OUTPUT', @cnt = @n OUTPUT;
    PRINT N'กันแถวเบิก (WRITEOFF) ที่หมายเหตุว่า ''ขาย'' ออกจาก POS rollup: ' + CAST(ISNULL(@n, 0) AS NVARCHAR(10)) + N' แถว';
END
ELSE
    PRINT N'ไม่มีคอลัมน์ Pos_Rollup_Ref/OutType (ยังไม่รัน PHASE18_01/PHASE12_09) — ข้ามขั้นกัน rollup';
GO

-- 4) mapping บัญชี (เพิ่มเฉพาะที่ยังไม่มี)
IF OBJECT_ID('dbo.Accounting_Account_Mapping', 'U') IS NULL
BEGIN
    PRINT N'ไม่มีตาราง Accounting_Account_Mapping — ข้ามการเพิ่ม mapping';
END
ELSE
BEGIN
    -- INVENTORY_LOSS: คัดลอกบัญชีจาก STOCK_LOSS_EXPENSE (ผูกไว้แล้วจะได้บัญชีเดิม) ไม่งั้น 57120
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = 'INVENTORY_LOSS')
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = 'STOCK_LOSS_EXPENSE')
            INSERT INTO dbo.Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Nexaacc_AccountId, Mapping_Type, Is_Active)
            SELECT TOP 1 'INVENTORY_LOSS', N'ขาดทุนสินค้าเสียหาย/หมดอายุ/สูญหาย (เหตุผลการเบิก — แนะนำ 57120 หรือสร้างบัญชีขาดทุนสินค้า)',
                   Nexaacc_AccountCode, Nexaacc_AccountId, 'EXPENSE', 1
              FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = 'STOCK_LOSS_EXPENSE';
        ELSE
            INSERT INTO dbo.Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
            VALUES ('INVENTORY_LOSS', N'ขาดทุนสินค้าเสียหาย/หมดอายุ/สูญหาย (NextAcc 57120 ขาดทุนจากการด้อยค่า หรือสร้างบัญชีขาดทุนสินค้า)', '57120', 'EXPENSE', 1);
        PRINT N'เพิ่ม mapping INVENTORY_LOSS';
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = 'COGS_ADJUST')
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = 'INVENTORY_VARIANCE')
            INSERT INTO dbo.Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Nexaacc_AccountId, Mapping_Type, Is_Active)
            SELECT TOP 1 'COGS_ADJUST', N'ปรับปรุงต้นทุน/ส่วนต่างจากการตรวจนับ (เหตุผลการเบิก COUNT_ADJUST)',
                   Nexaacc_AccountCode, Nexaacc_AccountId, 'EXPENSE', 1
              FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = 'INVENTORY_VARIANCE';
        ELSE
            INSERT INTO dbo.Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
            VALUES ('COGS_ADJUST', N'ปรับปรุงต้นทุน/ส่วนต่างจากการตรวจนับ (เหตุผลการเบิก COUNT_ADJUST)', '51210', 'EXPENSE', 1);
        PRINT N'เพิ่ม mapping COGS_ADJUST';
    END

    DECLARE @m TABLE (Code NVARCHAR(60), Descr NVARCHAR(200), AccCode NVARCHAR(20), MType NVARCHAR(50));
    INSERT INTO @m VALUES
     ('PROMOTION_EXPENSE', N'ค่าโฆษณาและส่งเสริมการขาย (ของแจก/ตัวอย่าง — NextAcc 53120)', '53120', 'EXPENSE'),
     ('STAFF_WELFARE',     N'สวัสดิการพนักงาน (อาหารพนักงาน/สวัสดิการจากสต็อก — NextAcc 54123)', '54123', 'EXPENSE'),
     ('ENTERTAINMENT',     N'ค่ารับรอง (ของขวัญ/ให้ลูกค้าฟรี — NextAcc 54460, ภาษีซื้อต้องห้าม ม.82/5(6))', '54460', 'EXPENSE'),
     ('DONATION',          N'เงินบริจาค/การกุศล (บริจาคสินค้า — NextAcc 54940)', '54940', 'EXPENSE'),
     ('OWNER_DRAWINGS',    N'ถอนใช้ส่วนตัว/บัญชีเดินสะพัดหุ้นส่วน (ผูกเอง: ผังห้างหุ้นส่วน 31030 หรือ 11330 ลูกหนี้กรรมการ)', '', 'EQUITY'),
     ('OPERATING_SUPPLIES', N'ของใช้ในการดำเนินงาน (ผูกเอง: ผังโรงแรม 51520 ต้นทุน Amenities หรือ 52120 วัสดุสิ้นเปลือง)', '', 'EXPENSE'),
     ('OUTPUT_VAT_DEEMED_EXPENSE', N'ภาษีขายที่กิจการรับภาระเอง (ไม่บังคับ — ว่าง = ใช้บัญชีของเหตุผล; ห้ามผูกบัญชีชื่อ ภาษีขาย/ภาษีซื้อ)', '', 'EXPENSE');

    INSERT INTO dbo.Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    SELECT m.Code, m.Descr, m.AccCode, m.MType, 1
      FROM @m m
     WHERE NOT EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping a WHERE a.TakeTime_Code = m.Code);
    PRINT N'เพิ่ม mapping บัญชีเหตุผลการเบิก: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' รายการ';
END
GO

PRINT '';
PRINT N'ขั้นตอนหลังรัน:';
PRINT N'  1) Admin → Accounting Integration → ดึง Chart of Accounts แล้วตรวจ/ผูก mapping ใหม่ (โดยเฉพาะ OWNER_DRAWINGS, OPERATING_SUPPLIES)';
PRINT N'  2) Admin → ตั้งค่า → เหตุผลการเบิกสินค้า (/Admin/Settings/StockOutReasons) — ให้ผู้ทำบัญชียืนยันค่าเริ่มต้นทุกเหตุผล';
PRINT N'  3) recycle app pool (แคตตาล็อกเหตุผล cache 5 นาที)';
GO
