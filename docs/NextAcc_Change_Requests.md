# คำขอปรับปรุงฝั่ง NextAcc (จากทีม TakeTime)

> อ้างอิงโค้ด NextAcc: `Wachira-d/Accounting` @ `a1baa01` (2026-09-25) — ทุกเลขบรรทัดในเอกสารนี้อ่านจาก commit นี้
> ผู้เขียน: ทีม TakeTime (ฝั่ง integration) · สถานะ: รอ dev NextAcc พิจารณา
> หลัก: แต่ละข้อมี (1) ข้อเท็จจริงในโค้ดพร้อมไฟล์:บรรทัด (2) ผลกระทบฝั่งโรงแรม (3) สิ่งที่ขอ/ร่างแก้ (4) TakeTime ทำอะไรระหว่างรอ

---

## ก. ใบกำกับภาษีอย่างย่อแบบเอกสาร/A4 ไม่ควรบังคับ ภ.พ.06

> **สถานะ (ต.ค. 2026, NextAcc @ `09e9ea6`): แก้แล้วฝั่ง NextAcc** — `Helpers/AbbreviatedTaxInvoiceRule.cs:90-115` (`Judge(..., AbbreviatedInvoiceChannel channel)`)
> ตรวจ จด VAT → `Company.IsRetailApproved` (ทุกช่องทาง) → ถ้า `channel == Document` ผ่านเลยโดยไม่ต้อง ภ.พ.06; ภ.พ.06 บังคับเฉพาะสลิปเครื่องเก็บเงิน.
> เนื้อหาด้านล่างเก็บไว้เป็นประวัติ (อ้าง `a1baa01`)

### ข้อเท็จจริงในโค้ด
- ตัวตัดสินเดียว `Helpers/AbbreviatedTaxInvoiceRule.cs:64-86` (`Judge`) — จด VAT แล้ว **ต้องมีทั้ง**
  `Company.IsRetailApproved` และ `Company.PhoR06ApprovedDate` (และวันที่เอกสาร ≥ วันอนุมัติ) ที่ `:80-83`
  ไม่งั้นคืน `NoPhoR06Approval` → หัวเอกสารถูกลดเป็น "ใบเสร็จรับเงิน"
- ปิดด่านได้แค่ **ทั้งแพลตฟอร์ม** ผ่าน `SiteSettings.RequirePhoR06ForAbbreviatedTaxInvoice` (`Models/Entities/SiteSettings.cs:55-65`,
  ใช้ที่ `AbbreviatedTaxInvoiceRule.cs:75-77`) — ไม่มีค่าตั้งรายบริษัท / รายช่องทางออกเอกสาร
- ฟิลด์บริษัท `Models/Entities/Company.cs:51-61` อธิบายว่า "ต้องเป็นผู้ประกอบการค้าปลีกที่ยื่น ภ.พ.06"
- จุดเรียก (ทุกจุดส่งเกณฑ์เดียวกัน ไม่แยกว่าเป็นสลิปเครื่องเก็บเงินหรือเอกสาร A4):
  - `Services/Implementations/PdfGenerationService.cs:1546-1548` (`ResolveDocumentHeadingAsync`)
  - `Services/Implementations/PdfGenerationService.cs:1624-1626` (หัวเอกสารแบบ batch)
  - `Services/Implementations/PdfGenerationService.cs:1845-1847` (`BuildDocumentHtml`)
  - `Services/Implementations/PdfGenerationService.DocumentRenderer.cs:65-67` (PDF native)
  - `Services/Implementations/DocumentService.cs:1793-1795` (`TaxInvoiceTitleNotice` บนจอ)
  - `Helpers/PosSlipHeader.cs:60-61` (สลิป POS)

### ประเด็น
ภ.พ.06 คือ **คำขออนุมัติใช้เครื่องบันทึกการเก็บเงิน** (cash register/POS) เพื่อออกใบกำกับภาษีอย่างย่อจากเครื่อง —
เป็นเงื่อนไขของ "สลิปจากเครื่องเก็บเงิน" ไม่ใช่ของ "ใบกำกับภาษีอย่างย่อ" ทั้งหมด. ผู้ประกอบการจด VAT ที่ประกอบกิจการค้าปลีก
(ขายสินค้า/ให้บริการแก่ผู้บริโภคจำนวนมาก เช่น โรงแรม ร้านอาหาร) ออกใบกำกับภาษีอย่างย่อ §86/6 แบบเอกสาร (พิมพ์จากโปรแกรม/A4)
ได้โดยไม่ต้องมี ภ.พ.06. ผลปัจจุบัน: โรงแรมที่ไม่ได้ใช้เครื่องเก็บเงิน (จึงไม่มี ภ.พ.06) ออกได้แค่ "ใบเสร็จรับเงิน" สำหรับลูกค้า
walk-in/ไม่ประสงค์รับใบกำกับ ทั้งที่ VAT ถูกนำส่งครบ (ฝั่ง TakeTime ตั้ง `Nexaacc_Receipt_Header_Type=ABBREVIATED` แล้วก็ยังได้หัว
"ใบเสร็จรับเงิน" — ดู CLAUDE.md ข้อ 10). *ขอให้ผู้ทำบัญชี/ที่ปรึกษาภาษีของ NextAcc ยืนยันข้อกฎหมายก่อน merge.*

### สิ่งที่ขอ (ร่าง)
1. ค่าตั้ง **รายบริษัท** (ไม่ใช่ทั้งแพลตฟอร์ม): `Company.AbbreviatedInvoiceOnDocuments` (bool, default `false` = พฤติกรรมเดิม)
   — "กิจการค้าปลีก ออกใบกำกับภาษีอย่างย่อแบบเอกสาร (ไม่ใช้เครื่องบันทึกการเก็บเงิน)" + UI ในหน้าข้อมูลบริษัท + audit log
2. แยก "ช่องทางที่ออก" ในตัวตัดสิน:
   ```csharp
   public enum AbbreviatedIssueChannel { CashRegisterSlip = 1, Document = 2 }

   public static AbbreviatedInvoiceBlockReason Judge(bool isVatRegistered, bool isRetailApproved,
       DateTime? phoR06ApprovedDate, DateTime issueDateUtc, bool requirePhoR06,
       AbbreviatedIssueChannel channel = AbbreviatedIssueChannel.CashRegisterSlip,
       bool abbreviatedOnDocuments = false)
   {
       if (!isVatRegistered) return AbbreviatedInvoiceBlockReason.NotVatRegistered;          // §77/1 คงเดิม
       if (channel == AbbreviatedIssueChannel.Document && abbreviatedOnDocuments)
           return AbbreviatedInvoiceBlockReason.None;                                           // เอกสาร A4 ไม่ต้อง ภ.พ.06
       if (!requirePhoR06) return AbbreviatedInvoiceBlockReason.None;
       ... // ภ.พ.06 เดิม (:80-83) — ใช้กับสลิปเครื่องเก็บเงินเสมอ
   }
   ```
   - จุดเรียกฝั่ง PDF/เอกสาร (`PdfGenerationService.cs:1546`, `:1624`, `:1845`, `DocumentRenderer.cs:65`, `DocumentService.cs:1793`)
     ส่ง `Document` + `company.AbbreviatedInvoiceOnDocuments`
   - `PosSlipHeader.cs:60` ส่ง `CashRegisterSlip` (คงเกณฑ์ ภ.พ.06)
   - ข้อความ `Message(NoPhoR06Approval)` (`AbbreviatedTaxInvoiceRule.cs:106-109`) เพิ่มทางไปต่อ "หรือเปิดค่าตั้ง ออกอย่างย่อแบบเอกสาร"
3. Unit test: บริษัทจด VAT + ไม่มี ภ.พ.06 + เปิดค่าตั้ง → เอกสาร = อย่างย่อ, สลิป POS = ใบเสร็จรับเงิน

### TakeTime ระหว่างรอ
ไม่บังคับหัวเอง (NextAcc คำนวณตอนพิมพ์) — log `หัวเอกสาร NextAcc:` หลัง sync. ทางเลี่ยงชั่วคราว = แอดมินแพลตฟอร์มปิด
`RequirePhoR06ForAbbreviatedTaxInvoice` (กระทบทุกบริษัท) หรือกรอก ภ.พ.06 ในข้อมูลบริษัท (ถ้ามีจริง)

---

## ข. ใบรับรองแทนใบเสร็จรับเงิน (DocumentType 15) ไม่มีฟิลด์ "ผู้รับเงิน"

### ข้อเท็จจริงในโค้ด
- `Models/DTOs/Document/DocumentDtos.cs:50-56` — ฟิลด์เฉพาะใบรับรองมีแค่ `CertificateReason`, `CertifierName`, `CertifierPosition`,
  `WitnessName`, `WitnessPosition`, `PaymentDate` · ตรวจบังคับที่ `Services/Implementations/DocumentService.cs:1022-1029`
- กรอบ "ข้อมูลใบรับรอง" บน PDF พิมพ์แค่ฟิลด์ข้างบน (`PdfGenerationService.DocumentRenderer.cs:997-1032`,
  HTML `PdfGenerationService.cs:2235-2259`) — ผู้รับเงินจึงเหลือแค่ Contact ของเอกสาร

### ผลกระทบ
แบบใบรับรองแทนใบเสร็จ (บก.111) ต้องระบุ **ผู้รับเงิน (ชื่อ/ที่อยู่)** ของรายจ่ายแต่ละรายการ. โรงแรมจ่ายให้คนที่ออกใบเสร็จไม่ได้
(แท็กซี่ แผงลอย ลูกจ้างรายวัน) — สร้าง Contact ต่อคนไม่สมเหตุสมผล จึงใช้ผู้ขายกลาง ⇒ PDF ไม่มีชื่อผู้รับเงินจริงในกรอบใบรับรอง

### สิ่งที่ขอ
- เพิ่ม `PayeeName?`, `PayeeAddress?`, `PayeeTaxId?` ใน `CreateDocumentRequest` + `Document` entity + UpdateDocument
  (เฉพาะ type 15) และพิมพ์ในกรอบ "ข้อมูลใบรับรอง" (ทั้ง native และ HTML) · null = ใช้ Contact (พฤติกรรมเดิม)
- (ถ้าทำได้) รองรับหลายรายการผู้รับเงินต่อบรรทัด (`DocumentLineRequest.PayeeName?`) ตามรูปแบบ บก.111

### TakeTime ระหว่างรอ
ส่งชื่อ/ที่อยู่ผู้รับเงินจริงใน `CustomAppendix` (`DocumentDtos.cs:45`, เก็บที่ `DocumentService.cs:1153`, พิมพ์ใต้กรอบใบรับรอง
`DocumentRenderer.cs:1034-1036` / HTML `PdfGenerationService.cs:2263-2264`) + ใน `Notes` (พิมพ์เป็น "หมายเหตุ:" `DocumentRenderer.cs:1079-1088`)
— `AccountingDataMapper.MapVoucherToCertificateInLieu`

---

## ค. ภาษีขายของเอกสาร Receipt (type 3) ใน ภ.พ.30 — ตรวจแล้ว **ไม่ต้องแก้** (บันทึกไว้อ้างอิง)

- `Services/Implementations/TaxService.cs:130` `GenerateVatReport` ดึงเอกสารที่ออกแล้วไม่ Voided (`:148-153`)
- Receipt/ReceiptVoucher **standalone** (ไม่มี `RelatedDocumentId`) นับเข้าภาษีขาย tax point = วันรับเงิน (`:571-591`)
  — ใบเสร็จมัดจำ (`IsDeposit`) ก็เข้า: VAT ทันที → งวดที่รับเงิน; deferred (21913) → งวดที่ `DepositOutputVatRecognizedAt`; ถูกนำไปหักใน
  ใบปลายทาง → ข้าม (`:592-640`)
- เอกสารรับเงิน OTA ของ TakeTime (`CREATE_OTA_SALES_DOCUMENT`, type 3, ไม่มี RelatedDocumentId) จึงเข้า ภ.พ.30 ครบ — ไม่ต้องเปลี่ยนเป็น TaxInvoice
- ถ้าข้อมูลผู้ซื้อ (ผู้ติดต่อ OTA) ไม่ครบ §86/4 รายงานจะติดป้าย "[ไม่ใช่ใบกำกับเต็มรูป]" (`TaxService.cs:3126-3138`) — ข้อมูลเท่านั้น
- คำขอเล็ก (nice-to-have): ให้ `GET /document/{id}` คืนธง `IncludedInVatReport`/`VatReportPeriod` เพื่อให้ระบบต้นทางตรวจย้อนได้โดยไม่ต้องเดาจากกติกา

---

## ง. ค่าธรรมเนียมเกตเวย์/ช่องทางรับเงิน — ไม่มีเส้นบันทึกตอน payout

### ข้อเท็จจริงในโค้ด
- `FeeAmount`/`FeeAccountId` มีเฉพาะ company `CreatePaymentRequest` (`Models/DTOs/Document/DocumentDtos.cs:1407-1412`,
  ตรวจ `DocumentService.cs:11783-11784`, ใช้ `:11818`) และระบุว่า "ใช้ได้เฉพาะเอกสารฝั่งขาย Invoice/TaxInvoice/DebitNote"
- integration `InboundPaymentRequest` ไม่มีค่าธรรมเนียม (`Models/DTOs/Integration/IntegrationDtos.cs:196-203`)
- เอกสาร Receipt (type 3) / TaxInvoice+IssuedAsCashReceipt (ขายสด) ลง Dr บัญชีเงิน (`PaymentAccountId`) เต็มยอด — ไม่มีขาค่าธรรมเนียม

### ผลกระทบ
TakeTime ลงยอดรับผ่านเกตเวย์ (PaySo/Omise) เต็มจำนวนเข้า **บัญชีพักเงินเกตเวย์** (11340/กระเป๋าเงิน Digital) ตามหลัก แต่ตอนเกตเวย์โอน
payout (หักค่าธรรมเนียม + VAT ค่าธรรมเนียม) ยังต้องให้ผู้ทำบัญชีลง JE มือ: Dr ธนาคาร + Dr ค่าธรรมเนียม + Dr ภาษีซื้อ / Cr บัญชีพักเงิน

### สิ่งที่ขอ
endpoint "ย้ายเงินจากบัญชีพัก → ธนาคาร พร้อมค่าธรรมเนียม" เช่น `POST /api/companies/{cid}/banking/clearing-settlements`
`{ FromAccountId, ToBankAccountId, GrossAmount, FeeAmount, FeeVatAmount?, FeeAccountId?, SettlementDate, Reference, ExternalRef }`
(idempotent ตาม `ExternalRef`) — หรืออนุญาต `FeeAmount` บน `/api/integration/payments` สำหรับเอกสารฝั่งขาย

---

## จ. (ข้อสังเกต) เงินประกันความเสียหาย — ผังมีแล้ว ไม่ต้องแก้

ผังโรงแรม `Services/ChartOfAccountTemplates.cs:441` มี `21530 เงินประกันความเสียหาย` (และ `21620 เงินค้ำประกัน` `:130`) —
TakeTime ใช้ผ่าน mapping `SECURITY_DEPOSIT_LIABILITY` (PHASE19_24) ด้วย JE integration ธรรมดา ไม่ต้องการ endpoint ใหม่

---

> **ข้อ ฉ.–ฎ. (ต.ค. 2026)** อ้างอิงโค้ด NextAcc `Wachira-d/Accounting` @ `09e9ea6` — เลขบรรทัดในข้อเหล่านี้อ่านจาก commit นี้
> ที่มา: การตรวจเส้นทางเอกสารขาย/สต๊อก/จ่ายออกที่ไม่ใช่การขาย ของทีม TakeTime (ภาษี: ให้ผู้ทำบัญชี/ที่ปรึกษาภาษียืนยันก่อน merge)

## ฉ. (น่าจะเป็นบั๊ก) company TaxInvoice + `IssuedAsCashReceipt` ตัดสต๊อกแต่ไม่ลงต้นทุนขาย

### ข้อเท็จจริงในโค้ด
- `ApplyStockMovementsAsync` ให้ทิศ `Invoice or TaxInvoice => -1` (`Services/Implementations/DocumentService.cs:17038-17039`)
  โดยไม่แยก `IssuedAsCashReceipt` ⇒ ใบกำกับขายเงินสดที่อนุมัติแล้ว **ตัดสต๊อกออก**
- `AutoPostToJournalAsync` สาขาขายเชื่อ (ซึ่งมี COGS) ตัดใบขายสดออก: `&& !doc.IssuedAsCashReceipt` (`DocumentService.cs:17577-17581`)
  แล้วส่งใบนั้นไปสาขาใบเสร็จ (`DocumentService.cs:18393-18398`)
- สาขาใบเสร็จลง COGS (Dr 51110 / Cr 11500) เฉพาะเมื่อ `CashSaleStockRules.AppliesTo(doc)` (`DocumentService.cs:18518-18525`)
  ซึ่งรับแค่ `Receipt/ReceiptVoucher` (`Helpers/CashSaleStockRules.cs:13-19` — comment ระบุว่า "TaxInvoice+IssuedAsCashReceipt เดินสายใบกำกับอยู่แล้ว ไม่เกี่ยว"
  แต่สายใบกำกับที่มี COGS คือสาขาขายเชื่อที่ใบนี้ถูกตัดออกไปแล้ว)
- ผล: สต๊อก (StockMovement) ลด แต่ GL 11500 ไม่ลด/51110 ไม่เพิ่ม ⇒ `inventory/gl-tieout` ไม่ตรง และกำไรขั้นต้นสูงเกินจริง

### ผลกระทบฝั่งโรงแรม
TakeTime ออกใบกำกับขายสดแบบ company document ได้ (`Nexaacc_CashSale_CompanyDoc=1`) — ถ้าบรรทัดผูกสินค้า (`ProductCode`) สต๊อกฝั่ง NextAcc จะลด
โดยไม่มีต้นทุนขาย ส่วน TakeTime ส่ง COGS แยกเป็น JE (`POSINV-…`) อยู่แล้ว จึงเสี่ยงทั้ง "ขาด" (ถ้าเลิกส่ง JE) และ "ซ้ำ" (ถ้า NextAcc แก้แล้ว TakeTime ยังส่ง JE)

### สิ่งที่ขอ
- ให้สาขาใบเสร็จลง COGS สำหรับ `TaxInvoice && IssuedAsCashReceipt` ด้วย (หรือขยาย `CashSaleStockRules.AppliesTo` ให้ครอบใบขายสดที่ไม่อ้างเอกสารต้นทาง/ไม่ใช่มัดจำ)
  ให้ทิศสต๊อกกับ COGS ตอบตรงกันเสมอ + unit test (อนุมัติใบขายสดที่มีสินค้า TrackStock → มีทั้ง StockMovement OUT และ JE COGS)
- แจ้ง TakeTime เมื่อ deploy เพื่อปิดการส่ง COGS JE ซ้ำ (เราจะใส่สวิตช์ฝั่งเรา)

---

## ช. ใบลดหนี้ที่อ้าง Receipt (type 3) ไม่คืนสต๊อก

### ข้อเท็จจริงในโค้ด
- ใบเสร็จ standalone ตัดสต๊อก (−1) ตามนโยบาย `MoveStockAndCogs` (`DocumentService.cs:17073-17074`, `Helpers/CashSaleStockRules.cs:21-26`)
- แต่ CN (เหตุผล Return) ที่อ้าง `PaymentVoucher/Receipt/ReceiptVoucher` ถูกบังคับ `direction = 0` (`DocumentService.cs:17109-17116`)
  — comment ยังอธิบายว่า "OUT มีแค่ Invoice/TaxInvoice" ซึ่งไม่จริงแล้วหลังมี CashSaleStockPolicy

### ผลกระทบ
ลูกค้าคืนสินค้าที่ซื้อด้วยใบเสร็จ (ขายหน้าร้าน walk-in) → JE กลับรายได้/VAT ได้ แต่สินค้าไม่กลับเข้าสต๊อกและ COGS ไม่กลับ

### สิ่งที่ขอ
CN Return ที่อ้าง Receipt/RV standalone ซึ่ง "เคยตัดสต๊อกจริง" (มี StockMovement ของใบต้นทาง) → +1 และกลับ COGS ตามต้นทุนของ movement เดิม;
ใบเสร็จ settlement (มี `RelatedDocumentId`) / มัดจำ / นโยบาย Ignore → คง 0 เหมือนเดิม

---

## ซ. integration credit note ของการขายเงินสดทิ้งลูกหนี้ติดลบ

### ข้อเท็จจริงในโค้ด
- `InboundCreditNoteRequest` (`Models/DTOs/Integration/IntegrationDtos.cs:205-216`) มีแค่ `Reason` ข้อความ — ไม่มี `CreditNoteReason`, ไม่มีบัญชีคืนเงิน
- JE ของ CN integration: Dr รายได้ + Dr ภาษีขาย / **Cr ลูกหนี้การค้า** เสมอ (`Services/Implementations/IntegrationService.cs:1445-1492`, `:3079-3084`)
  และ integration ไม่ขยับสต๊อกเลย
- ฝั่ง company CN ถ้าใบต้นทางชำระครบจะลงแบบเงินสด (Cr เงินสด — `DocumentService.cs:17690-17699`) แต่ integration ไม่มีเส้นนี้

### ผลกระทบ
ใบขายสด (`isCashSale` — ไม่เคยเปิดลูกหนี้) ที่ออก CN ผ่าน integration ⇒ ลูกหนี้ของลูกค้า walk-in/ลูกค้ากลางติดลบ ค้างจนกว่าจะลงคืนเงินเอง

### สิ่งที่ขอ
เพิ่ม `CreditNoteReason?` (Return/Discount/…) + `RefundPaymentAccountId?` (GUID ผังบัญชีเงิน) ใน `InboundCreditNoteRequest`:
ใบต้นทางชำระครบ/ขายสด + มีบัญชีคืนเงิน → Cr บัญชีเงินนั้นแทนลูกหนี้; `Return` + บรรทัดผูกสินค้า → คืนสต๊อก/กลับ COGS เหมือน company CN · idempotent ตาม `ExternalRef`

---

## ฌ. ปรับสต๊อกไม่มีรหัสเหตุผลและไม่ลงบัญชี — ขอ endpoint ตัดจำหน่ายตามเหตุผลพร้อม GL

### ข้อเท็จจริงในโค้ด
- `StockAdjustmentRequest(ProductId, Quantity, MovementType, UnitCost?, Reference?, Notes?, WarehouseId?, LotNumber?)`
  (`Models/DTOs/Product/ProductDtos.cs:89-100`) — ไม่มีรหัสเหตุผล
- `ProductService.AdjustStockAsync` (`Services/Implementations/ProductService.cs:259-302`) และ stock-count apply (`:550-588`) ขยับจำนวนอย่างเดียว **ไม่โพสต์ JE**;
  JE มีเฉพาะ `supplies/use` สำหรับ `ProductType.Supplies` (`:1001-1123`) · ไม่มีบัญชีขาดทุนสินค้าเสียหายในผังมาตรฐาน (ใกล้สุด 57120)

### ผลกระทบ
TakeTime จึงต้องยิงสองครั้งต่อการจ่ายออกหนึ่งรายการ (qty `stock/adjust` + JE `/integration/journals`) — ไม่ atomic, รายงาน `inventory/gl-tieout` คาดว่ามี adjustment ค้าง,
และแยกไม่ได้ว่าจ่ายออกเพราะ แตก/หมดอายุ/สูญหาย/เบิกใช้/สวัสดิการ/ของแจก/เจ้าของใช้ (ต่างกันทั้งบัญชีและภาษี — ม.77/1(8), ป.79/2541)

### สิ่งที่ขอ
`POST /api/integration/stock-writeoffs` (idempotent ตาม `ExternalRef`):
`{ ExternalRef, Date, ReasonCode, Lines[{ ProductId|ProductCode, Quantity, UnitCost?, MarketPrice? }], DebitAccountCode?, OutputVat?: { Base, Rate }, EvidenceUrl?, ApprovedBy?, Notes }`
→ StockMovement OUT (ต้นทุนตาม costing ของ NextAcc) **+ JE ในธุรกรรมเดียว** Dr บัญชีตามเหตุผล / Cr 11500 (+ Cr 21911 ถ้ามี `OutputVat`) · เก็บ `ReasonCode`
บน movement ให้ค้นได้ · ตารางเหตุผล→บัญชีตั้งได้ต่อบริษัท (ค่าเริ่มต้น: เสียหาย/หมดอายุ→ขาดทุนสินค้า, เบิกใช้→ค่าใช้จ่ายดำเนินงาน, สวัสดิการ→54123, ของแจก→53120,
รับรอง→54460, เจ้าของใช้→ลูกหนี้กรรมการ/เงินถอน) · void = กลับทั้ง movement และ JE

---

## ญ. ไม่มีเอกสาร/แม่แบบ "ภาษีขายจากการนำสินค้าไปใช้" (deemed sale ม.77/1(8))

### ข้อเท็จจริงในโค้ด
- `DocumentType` (`Models/Enums/AllEnums.cs:499-520`) ไม่มีชนิดสำหรับ นำไปใช้/ของแจก/สวัสดิการ/สินค้าขาดจากรายงาน
- ทางเดียวตอนนี้คือ JE ที่มีบรรทัด Cr 21911 ซึ่ง ภ.พ.30 นับผ่าน fallback แบบถอยฐานจาก VAT ÷ อัตรา และเดาผู้เสียภาษีจากข้อความ JE
  (`Services/Implementations/TaxService.cs:1228-1334`)

### ผลกระทบ
กรณีที่ผู้ทำบัญชีเลือกเสีย VAT จากราคาตลาด (เจ้าของใช้เอง, ของรางวัล, สินค้าหาย/ถูกขโมย — ข้อหารือ กค 0706/พ./6444) ไม่มีเอกสารภายในที่มีเลขที่
และฐานภาษีจริง (ราคาตลาด) ให้ลงรายงานภาษีขาย — ฐานที่ถอยจาก VAT อาจคลาดเศษสตางค์ และไม่มีช่องอ้างเหตุผล

### สิ่งที่ขอ
เอกสาร/แม่แบบ "บันทึกภาษีขาย — การนำสินค้าไปใช้ (ม.77/1(8), ม.79/3)": ไม่มีผู้ซื้อ (หรือผู้ซื้อ = กิจการเอง), ฐาน = ราคาตลาดที่ระบุ, VAT 7%,
Dr บัญชีตามเหตุผล (ภาษีขายที่รับภาระ — รายจ่ายต้องห้าม) / Cr 21911, **นับเข้า ภ.พ.30 ด้วยฐานจริง** และ **ห้ามนำไปเป็นภาษีซื้อ** · ใช้ร่วมกับข้อ ฌ. ได้ (`OutputVat`)

---

## ฎ. ไม่มีรายงานสินค้าและวัตถุดิบ (ม.87(3))

### ข้อเท็จจริงในโค้ด
- ไม่พบตัวสร้างรายงานสินค้าและวัตถุดิบ — ใกล้สุดคือ `GET inventory/movement-summary` (`Controllers/ProductController.cs:346-354`) ที่ให้ยอดรับ/จ่าย/ปรับ และ COGS รวม
- การอ้าง ม.87(3) ที่มีคือการตรวจลำดับวันที่ในไฟล์ส่งออกภาษี (`Services/Implementations/TaxFilingExportService.cs:387-395`)

### สิ่งที่ขอ
รายงานต่อสินค้า/คลัง ตามรูปแบบประกาศอธิบดีฯ: หัวรายงาน (ชื่อผู้ประกอบการ, เลขผู้เสียภาษี, สถานประกอบการ/สาขา), ต่อรายการ: วันที่ · เลขที่เอกสาร ·
รับ · จ่าย (แยกเหตุผล — ใช้ `ReasonCode` จากข้อ ฌ.) · คงเหลือ · (ถ้ามี) มูลค่าตามวิธีต้นทุนของบริษัท · ส่งออก PDF/Excel · ปิดรายงานภายในเวลาที่กฎหมายกำหนด

### TakeTime ระหว่างรอ
มีรายงานฝั่งเราแล้ว: `Report/StockMovementReport` (ยกมา/รับ/ขาย/จ่ายออกตามเหตุผล/คงเหลือ + บัญชีคุมรายวัน, CSV/พิมพ์) — ข้อมูลจาก Product_In/Product_Out ของ TakeTime
(สต๊อกที่ปรับฝั่ง NextAcc เข้ามาเฉพาะเมื่อเปิด `Nexaacc_StockQtyPull`)

---

## ฏ. ใบลดหนี้ (company) ของใบที่ชำระครบ — เลือกบัญชีเงินที่คืนจริงไม่ได้

> อ้าง NextAcc @ `8c9cf5b` (2026-10-26)

### ข้อเท็จจริงในโค้ด
- ใบลดหนี้ฝั่งขายที่อ้างใบต้นทาง `BalanceDue <= 0.01` ลงแบบเงินสดเสมอ: `counterAccCode = "111"` (`Services/Implementations/DocumentService.cs:17899`, `:17920-17925`)
  → `FindAccountAsync("111")` คืนบัญชีโพสต์ได้ตัวแรกตาม prefix (`:15749-15783`) — ไม่อ่าน `PaymentAccountId`/`BankAccountId` ของใบลดหนี้
- `CreateDocumentRequest` มี `PaymentAccountId` (`Models/DTOs/Document/DocumentDtos.cs:50`) แต่เส้น CN ไม่ใช้

### ผลกระทบ
โรงแรมคืนเงินออกบัญชีธนาคาร/เกตเวย์ที่รับเข้ามา แต่ NextAcc ลด "เงินสด 111x" — ยอดเงินสด/ธนาคารเพี้ยนสองบัญชีจนกว่าจะลงรายการย้ายเอง

### สิ่งที่ขอ
ใบลดหนี้ cash-mode: ถ้ามี `PaymentAccountId` (หรือ `BankAccountId` ที่ผูกผัง) ให้ Cr บัญชีนั้นแทน `111` (ตรวจด้วย `DocumentMoneyChannel` เหมือนเส้นอื่น)

### TakeTime ระหว่างรอ
หลังอนุมัติใบลดหนี้ อ่าน JE ของใบ (`GET /accounting/journals?sourceDocumentId=` — `Controllers/AccountingController.cs:105-116`) หาขาเครดิตที่ NextAcc ลง
แล้วโพสต์ `/integration/journals` ref `CN-{id}-MNY` Dr บัญชีนั้น / Cr แหล่งเงินที่คืนจริง (`Class/Integration/AccountingSyncService.CreditNote.cs`) — ถ้า NextAcc แก้แล้ว JE นี้จะเป็นศูนย์เอง (บัญชีเดียวกันถูกข้าม)

---

## ฐ. ไม่มี API ยกเลิก "คืนมัดจำ" (refund-deposit)

### ข้อเท็จจริงในโค้ด
- `POST /document/{id}/refund-deposit` (`Controllers/DocumentController.cs:533-545`) สร้าง JE คืนมัดจำ (SourceDocumentId = ใบมัดจำ) + เพิ่ม `DepositRefundedAmount`
  + (VAT เข้า ภ.พ.30 แล้ว) ใบลดหนี้ Approved ที่ไม่มี JE ของตัวเอง (`Services/Implementations/DocumentService.cs:5429-5662`)
- ไม่มี endpoint กลับรายการคืนมัดจำ: void ใบลดหนี้อัตโนมัติไม่กลับ JE/ยอดคืน · กลับ JE ผ่าน `/integration/journals/reverse` ไม่ลด `DepositRefundedAmount`

### ผลกระทบ
บันทึกคืนมัดจำผิด (ผิดยอด/ผิดบัญชี/ลูกค้าไม่รับเงิน) แก้จากระบบต้นทางไม่ได้ — TakeTime จึงบล็อกการยกเลิกใบลดหนี้ประเภทคืนมัดจำที่ส่งแล้ว

### สิ่งที่ขอ
`POST /document/{depositId}/refund-deposit/{journalEntryId}/reverse` (หรือ void ใบลดหนี้คืนมัดจำแบบ cascade): กลับ JE + ลด `DepositRefundedAmount` + void ใบลดหนี้คู่กัน
ในธุรกรรมเดียว (ตรวจงวด ภ.พ.30 ที่ยื่นแล้วเหมือน void ปกติ) · รับ `Idempotency-Key`/อ้างอิงภายนอกเพื่อกันคืนซ้ำเมื่อ retry
