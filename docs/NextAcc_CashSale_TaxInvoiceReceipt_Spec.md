# NextAcc Spec: ใบกำกับภาษี/ใบเสร็จรับเงิน ใบเดียว (cash-sale) — เลิกออกใบเสร็จรับชำระแยก

**สถานะ:** รอ NextAcc implement (ตัดสินใจแล้วฝั่ง TakeTime: ต้องการเอกสารเดียว)
**Repo NextAcc:** Wachira-d/Accounting

## ปัญหา (จากงานจริง — การจอง #148936, TIV-20260718-0002)

เช็คเอาท์ B2B (ลูกค้ามีเลขภาษี) 1 ครั้ง → ได้ **3 เอกสาร**:
| เอกสาร | ชนิด | ยอด |
|---|---|---|
| TIV-20260718-0002 | ใบกำกับภาษี (TaxInvoice type 4) | 9,900 |
| REC-20260718-0010 | ใบเสร็จรับเงิน (รับชำระ-ตัดมัดจำ) | 3,500 |
| REC-20260718-0011 | ใบเสร็จรับเงิน (รับชำระ-เงินโอน) | 6,400 |

GL **ถูกต้อง** (JE เดียว `RV-202607-0148`: Dr เงินฝาก 6,400 + Dr มัดจำ 21510 3,500 / Cr รายได้ 9,252.34 / Cr VAT 647.66) — แต่**เอกสารเยอะเกิน**: การขายเงินสดควรออก **ใบเดียว** = "ใบกำกับภาษี/ใบเสร็จรับเงิน" ไม่ใช่ใบกำกับ + ใบเสร็จรับชำระอีก 2 ใบ (เปลืองเลขเอกสาร, ลูกค้าสับสน, ยื่นบัญชีรก).

## ต้นเหตุ (ฝั่ง TakeTime — โครงปัจจุบัน)

`ProcessReceiptDocument` แยก 2 เส้นตามข้อมูลภาษีผู้ซื้อ:

- **walk-in / B2C (ไม่มีเลขภาษี §86/4):** `MapReceiptToDocument(... documentType=Receipt(3))` →
  **cash-sale ใบเดียวจบ** (Dr เงินสด/แหล่งเงิน / Cr รายได้ / Cr VAT, ไม่เปิดลูกหนี้, ไม่มี payment แยก).
  ✅ นี่คือรูปแบบที่ต้องการ — **แต่ Receipt(3) ออก e-Tax TAX_INVOICE ไม่ได้**
- **B2B (มีเลขภาษีครบ):** `MapReceiptToDocument(... documentType=TaxInvoice(4))` = **เปิดลูกหนี้ (AR)**
  (`PaymentDate=null`, `PaymentAccountId=null`) → แล้วปิดด้วย `SettleReceiptInNextAcc` ที่บันทึก
  **payment แยก** (ตัดมัดจำ 1 + รับเงินสด 1) → NextAcc สร้างเป็น ReceiptVoucher = REC-xxxx ต่อ payment.
  เหตุที่ต้องใช้ TaxInvoice(4): **เพื่อออก e-Tax TAX_INVOICE ได้** (Receipt(3) ออกไม่ได้).

⟹ ปมอยู่ที่: **"เอกสารเดียว cash-sale" กับ "ออก e-Tax ได้" ตอนนี้ได้อย่างใดอย่างหนึ่ง** ไม่ได้ทั้งคู่.

## สิ่งที่ขอให้ NextAcc ทำ (เลือกทางใดทางหนึ่ง — แนะนำ A)

### ทางเลือก A (แนะนำ — เปลี่ยนน้อย ใช้ของที่มี): Receipt(3) ออก e-Tax ได้เมื่อผู้ซื้อมีข้อมูล §86/4

ทำให้ **Receipt document (type 3)** ที่ผู้ซื้อมีเลขภาษี 13 หลัก + ที่อยู่ครบ (§86/4):
1. **ออก e-Tax `TAX_INVOICE` ได้** (ปัจจุบัน Receipt→e-Tax ไม่ผ่าน) — เหมือน TaxInvoice(4)
2. **หัวเอกสาร render เป็น "ใบกำกับภาษี/ใบเสร็จรับเงิน"** (มีเลขภาษี = เอกสารภาษีเต็มรูป + จ่ายจบในใบ = ใบเสร็จ)
   ถ้าไม่มีเลขภาษี คงเป็น "ใบเสร็จรับเงิน" ตามเดิม
3. GL คงเดิม (Dr เงินสด/แหล่งเงิน `PaymentAccountId` / Cr รายได้ราย line / Cr VAT 21911); มัดจำผ่าน
   `IsDeposit`/`DepositAppliedAmount`/`DepositAppliedRef`/`DepositAppliedDrivesJournal` ที่มีอยู่แล้ว

**ผลลัพธ์:** TakeTime แค่ route เส้น B2B ไปใช้ Receipt(3) (เส้น walk-in ที่มีอยู่แล้ว) → **ใบเดียวจบ ไม่มี REC แยก + e-Tax ออกได้** ไม่ต้องมี AR/settle/payment แยกเลย.

### ทางเลือก B: TaxInvoice(4) รองรับ cash-sale (payment ฝังในใบ)

ทำให้ **TaxInvoice(4)** รับ `PaymentDate` + `PaymentAccountId` (+ deposit fields) แบบ cash-sale:
- โพสต์ JE ในใบเดียว (Dr เงินสด/แหล่งเงิน + Dr มัดจำ 21510 / Cr รายได้ / Cr VAT) **ไม่เปิดลูกหนี้ ไม่ต้องมี payment แยก**
- หัว render "ใบกำกับภาษี/ใบเสร็จรับเงิน", ออก e-Tax TAX_INVOICE ได้
- ต้องมี flag ชัดเจน (เช่น `IsCashSale: true`) กัน NextAcc รุ่นเดิมตีความ payment ในใบ **ซ้อน**กับ settle เดิม

## เงื่อนไขที่ต้องคงไว้ (ทั้ง A และ B)

- **แหล่งเงิน (bank/cash) ตามที่ผู้ใช้เลือก** — `PaymentAccountId` (ChartOfAccount GUID) → Dr บัญชีนั้นตรง ๆ
- **หักมัดจำในใบ** — Dr 21510 (เงินรับล่วงหน้า) ตามยอดมัดจำ + กลับ VAT มัดจำ (21913/21911) ถ้า defer;
  net GL ต้องเท่าเดิม (มัดจำ + เงินรับจริง = ยอดใบ)
- **ลายเซ็นผู้รับเงิน/ผู้จัดทำ** = ผู้สร้างใบในระบบ (`PreparerName`/`PreparerSignatureBase64` ที่ส่งอยู่แล้ว)
- **e-Tax XML** ต้องออก TAX_INVOICE ได้จริง (ใบเดียวนี้คือใบกำกับ)
- **resyncUpdate / void→recreate** ยังทำงาน (แก้ไข/ยกเลิกได้)

## จุดแก้ฝั่ง TakeTime (พร้อม implement เมื่อ NextAcc ยืนยัน contract)

ไฟล์ `Class/Integration/AccountingSyncService.cs` → `ProcessReceiptDocument`, สาขา B2B
(ปัจจุบันบรรทัด ~4272 `documentType: NexaaccDocumentType.TaxInvoice` + `SettleReceiptInNextAcc`):

- **ทางเลือก A:** เปลี่ยน `documentType` เป็น `Receipt(3)` + ใช้เส้น `SettleReceiptDocAsync` (cash-sale
  ใบเดียว แบบเดียวกับ walk-in) แทน `EnsureRevenueDocCreatedApprovedAsync + SettleReceiptInNextAcc`
  → **ตัด payment แยกทิ้งทั้งหมด** สำหรับ B2B. gate ด้วย config ใหม่ `Nexaacc_TaxReceipt_SingleDoc`
  (default off จนกว่า NextAcc รุ่นรองรับจะ deploy).
- **ทางเลือก B:** คง TaxInvoice(4) แต่ mapper set `PaymentDate`/`PaymentAccountId` + `IsCashSale=true`
  แล้ว**ข้าม** `SettleReceiptInNextAcc`.

Config flag ใหม่ (migration) — default off เพื่อ backward-compatible; เปิดเมื่อ NextAcc รุ่นรองรับ deploy.
ทุก guard รับเงินซ้อน (BalanceDue cap, PAID_EXTERNAL) ที่เพิ่งเพิ่มยังทำงานกับเส้นเดิม (int_/ยังไม่เปิด flag).

## สรุปที่ผู้ใช้ TakeTime ต้องรู้

- GL ปัจจุบัน**ถูกต้อง ไม่ซ้อน** — ใบเสร็จแยกเป็นแค่บันทึกการรับชำระ (JE "ก่อนอนุมัติ" ไม่โพสต์)
- การออกใบเดียว "ใบกำกับภาษี/ใบเสร็จรับเงิน" **ต้องรอ NextAcc** ทำ A หรือ B ก่อน แล้ว TakeTime เปิด flag
- ระหว่างรอ: เอกสารยังใช้ได้ถูกต้องตามกฎหมาย (ใบกำกับ TIV = ใบภาษีตัวจริง; REC = ใบเสร็จรับชำระ)

---

## ⚠ ส่วนขยายที่ต้องทำเพิ่ม (Option B ยังไม่ครบ): หักมัดจำบน cash-sale invoice

**อาการหลัง deploy Option B รอบแรก:** เช็คเอาท์ B2B ที่**มีหักมัดจำ** (โรงแรมเก็บมัดจำแทบทุกใบ)
**ยังไม่รวมใบ** — เพราะ TakeTime gate ไว้ (`depositApplied <= 0`) เนื่องจาก integration invoice +
`isCashSale` **ไม่มี field หักมัดจำ**. ถ้าส่งเคสมัดจำโดยไม่มี field → NextAcc Dr เงินสดเต็มยอด
ไม่ Dr 21510 → มัดจำไม่ถูกล้าง + เงินสดเกิน = **GL พัง** ⟹ ต้องกันไว้.

⟹ **ผลคือ Option B ช่วยได้เฉพาะเคสจ่ายเต็มไม่มีมัดจำ (ส่วนน้อย)** — ต้องรองรับหักมัดจำถึงจะใช้จริงได้.

### สิ่งที่ขอ NextAcc เพิ่ม: `/integration/invoices` + `isCashSale` รับ field หักมัดจำ

เพิ่ม field (ชื่อเทียบ `CreateDocumentRequest` company ที่มีอยู่แล้ว):
- **`depositAppliedAmount`** (decimal) — ยอดมัดจำที่หัก (รวม VAT)
- **`depositAppliedRef`** (string) — เลขใบมัดจำ (เช่น REC260718003) เพื่อกลับ 217xx/21913 ของใบนั้น
- (ทางเลือก) **`depositOutputVatDeferred`** — ถ้ามัดจำพัก VAT ที่ 21913

**พฤติกรรมที่ต้องการ** (เหมือน company Receipt(3) ที่ทำได้แล้ว แต่บน cash-sale TaxInvoice + e-Tax):
```
Dr แหล่งเงิน (PaymentAccountId)      = ยอดรับจริง = Total − depositApplied   (เช่น 6,400)
Dr เงินมัดจำรับล่วงหน้า 21510          = depositApplied                       (เช่น 3,500)
[+ ถ้า defer: Dr 21913 / Cr 21911 ส่วน VAT มัดจำ]
   Cr รายได้ราย line
   Cr ภาษีขาย 21911
```
= ใบเดียว (ใบกำกับภาษี/ใบเสร็จรับเงิน) หักมัดจำในใบ + e-Tax TAX_INVOICE + GL สมดุล
(อ้างอิง: company Receipt(3) ทำ pattern นี้ได้แล้วผ่าน DepositAppliedAmount/Ref — ขอ port มา cash-sale invoice)

### จุดแก้ TakeTime (พร้อมทันทีที่ NextAcc ยืนยัน)

- `CreateIntegrationInvoiceRequest`: เพิ่ม `DepositAppliedAmount`/`DepositAppliedRef`/`DepositOutputVatDeferred`
- `MapReceiptToCashSaleTaxInvoice`: รับ depositApplied + depositRef → set field
- `ProcessReceiptDocument`: **เอา gate `depositApplied <= 0.005m` ออก** (รับเคสมัดจำ) + resolve `depositAppliedRef`
  จากใบมัดจำ (เหมือน SettleReceiptInNextAcc / MapReceiptToDocument deposit path ที่มีอยู่)
- `BuildCorrectedReceiptInvoice`: ส่ง deposit field บนเส้น resync ด้วย

**จนกว่าจะเพิ่ม:** เคสมัดจำใช้เส้นเดิม (TIV + settle payments แยก) — GL ถูก แต่ยังหลายใบ.
