# คำขอปรับปรุงฝั่ง NextAcc (จากทีม TakeTime)

> อ้างอิงโค้ด NextAcc: `Wachira-d/Accounting` @ `a1baa01` (2026-09-25) — ทุกเลขบรรทัดในเอกสารนี้อ่านจาก commit นี้
> ผู้เขียน: ทีม TakeTime (ฝั่ง integration) · สถานะ: รอ dev NextAcc พิจารณา
> หลัก: แต่ละข้อมี (1) ข้อเท็จจริงในโค้ดพร้อมไฟล์:บรรทัด (2) ผลกระทบฝั่งโรงแรม (3) สิ่งที่ขอ/ร่างแก้ (4) TakeTime ทำอะไรระหว่างรอ

---

## ก. ใบกำกับภาษีอย่างย่อแบบเอกสาร/A4 ไม่ควรบังคับ ภ.พ.06

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
