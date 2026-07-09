# คำขอ/ประเด็นค้าง ฝั่ง NextAcc — Deposit & Checkout Document

> สรุปรวมสำหรับทีม NextAcc (Wachira-d/Accounting). TakeTime ฝั่งเราแก้/รองรับครบแล้ว
> (config-gated, default off) — เหลือ 3 ข้อที่ต้องทำ/ยืนยันฝั่ง NextAcc ก่อนเปิดใช้เต็ม.
> บริบท: hotel booking → รับมัดจำ (Receipt/ใบเสร็จมัดจำ) → เช็คเอาท์ (ใบกำกับภาษี/ใบเสร็จรับเงิน หักมัดจำ).

TakeTime ส่งบน checkout Receipt (DocumentType=3) แล้ว: `depositAppliedAmount`, `depositAppliedRef`
(เลขเอกสาร NextAcc ของใบมัดจำ), `depositAppliedDrivesJournal` (config `Nexaacc_Deposit_Drives_Journal`).

---

## ✅ เสร็จแล้ว (NextAcc)
- **drives รับ journal ref (JV-INT):** cb55e3b — depositAppliedRef เป็น JournalEntry.EntryNumber ได้
  → กลับ deferred (217xx/21913) จากบรรทัด Cr ของ journal ใน JE ใบเดียว (self-contained). TakeTime
  รองรับแล้ว (config `Nexaacc_Drives_Journal_Ref`, commit 4c34f5c/72b37b1).
- **confirmation A** (แยกฐาน/VAT ตามใบมัดจำจริง): ✅ cb55e3b กลับจากบรรทัด Cr จริง.
- **double-reverse guard:** ✅ NextAcc มี `DepositAppliedToDocumentId`.

---

## 1. [สำคัญสุด — ยังต้องยืนยัน] Un-realize ใบมัดจำ ตอน void เอกสารเช็คเอาท์ (ทั้ง Document REC- และ journal JV-INT)

**บริบท:** drives-journal (REC- หรือ JV-INT) → post เช็คเอาท์ ลง JE self-contained + **mark ใบมัดจำ/JV
"realized/applied"** (`DepositAppliedToDocumentId`) กันรับรู้ซ้ำ.

**สิ่งที่ต้องยืนยัน/ทำ:** เมื่อ **void เอกสารเช็คเอาท์** ที่ใช้ drives-journal → NextAcc ต้อง
1. reverse JE (คืน 217xx/21913) — ปกติ void cascade ทำอยู่แล้ว
2. **UN-mark "realized" / clear `DepositAppliedToDocumentId`** ของใบมัดจำ **หรือ JV-INT** ที่อ้างถึง
   โดย **ไม่ต้อง void ใบมัดจำ/JV เอง**

**ทำไมสำคัญ:** TakeTime resync/edit = **void→สร้างใหม่เฉพาะใบเช็คเอาท์** (ไม่แตะใบมัดจำ/JV — เป็น
ธุรกรรมรับเงินจริงที่จบแล้ว). สถานะ realized จึงต้องคุมด้วย lifecycle ของ "ใบเช็คเอาท์":
```
CREATE#1 เช็คเอาท์ → กลับมัดจำ/JV + mark realized (DepositAppliedToDocumentId)
VOID#1   เช็คเอาท์ → reverse JE + UN-mark realized / clear DepositAppliedToDocumentId  ← ข้อนี้
CREATE#2 เช็คเอาท์ (อ้างมัดจำ/JV เดิม) → re-realize ได้
```
ถ้า void ไม่ un-mark → CREATE#2 เจอ "realized/applied แล้ว" → guard บล็อก → ข้ามการกลับ 217xx/21913
→ ใบใหม่ JE ขาด Dr มัดจำ = GL ไม่บาลานซ์. **ต้องรองรับทั้ง Document ref และ journal ref (JV-INT)**.

---

## 2. Page-break ก่อนส่วน "การบันทึกบัญชี" บน PDF เอกสาร

**บริบท:** PDF เอกสาร (Receipt/TaxInvoice) มีส่วน "การบันทึกบัญชี" (JE Dr/Cr) ต่อท้าย.

**ปัญหา:** ส่วน JE ไหลต่อจากเนื้อหาลูกค้า → บรรทัด Dr/Cr **ถูกตัดคนละหน้า** (เช่น Dr อยู่หน้า 1,
Cr อยู่หน้า 2) อ่านยาก.

**ขอ:** ใส่ **page-break ก่อนส่วน "การบันทึกบัญชี"** ให้ขึ้นหน้าใหม่เสมอ
→ หน้า 1 = เอกสารลูกค้า (สะอาด) / หน้าถัดไป = การบันทึกบัญชี (ครบในหน้าเดียว ไม่ตัด).

---

## 2b. [ทางเลือก] `bookingNumber` บน Credit Note / Debit Note DTO

**บริบท:** NextAcc เพิ่ม `bookingNumber` (เลขการจอง `RES-{id}`) ให้ **integration invoice** แล้ว
(commit 8ed90ba) — TakeTime ส่งครบทุก invoice/document ที่อิงการจอง. ตอนนี้ TakeTime **ส่ง
`bookingNumber` มาบน `/api/integration/credit-notes` และ `/debit-notes` ด้วย** (mapper คืนเงิน/
ยกเลิกใบเสร็จ/ค่าเสียหาย: `MapRefundToCreditNote` / `MapReceiptVoidToCreditNote` /
`MapDamageChargeToDebitNote`).

**ขอ (ถ้าทำได้):** เพิ่มฟิลด์ `bookingNumber` (string, camelCase) ใน `InboundCreditNoteRequest` /
`InboundDebitNoteRequest` DTO ฝั่ง NextAcc (mirror invoice 8ed90ba) → เพื่อ **group ทุกเอกสารของ
การจองด้วยคีย์เดียว** (ใบกำกับ + CN + DN) filter/ค้นตาม booking ได้เหมือน invoice.

**หมายเหตุ:** ไม่ใช่ blocker — ถ้า DTO ยังไม่มีฟิลด์นี้ NextAcc จะ **ignore** เฉย (record ไม่ error).
CN/DN โยงการจองได้อยู่แล้วผ่าน `externalRef` (`CN-RES-{id}` / `DN-DMG-{id}`) + `originalInvoiceRef`.

---

## 3. [ทางเลือก] เลข running-number ชุดแยกสำหรับ "ใบกำกับภาษี/ใบเสร็จรับเงิน"

**บริบท:** เช็คเอาท์ลูกค้า walk-in ใช้ **DocumentType=Receipt(3)** (จำเป็น — TaxInvoice(4) ติด §86/4
เพราะ walk-in ไม่มีเลขภาษี+ที่อยู่). Receipt(3)+VAT render เป็น "ใบกำกับภาษี/ใบเสร็จรับเงิน" แต่ได้
**เลขชุด REC** ปนกับใบเสร็จมัดจำ.

**ขอ (ถ้าทำได้):** running-number **ชุดแยก** สำหรับเอกสาร "ใบกำกับภาษี/ใบเสร็จรับเงิน" (แทนที่จะปน
ชุด REC เดียวกับใบเสร็จมัดจำ) — เพื่อ audit trail ภาษีขาย.

**หมายเหตุ:** ตามกฎหมายใช้ชุด REC ได้ ไม่ผิด (ความเป็นใบกำกับมาจากเนื้อหา ไม่ใช่ prefix) — ข้อนี้เป็น
preference ไม่ใช่ blocker. **ห้ามแก้เป็น DocumentType=TaxInvoice(4)** เพราะจะทำให้ walk-in ถูก §86/4
ปฏิเสธ + เสียคุณสมบัติ "จ่ายจบในใบ + ลายเซ็นผู้จัดทำ".

---

## ยืนยันเพิ่มเติม (confirmation)

- **A.** `depositAppliedDrivesJournal` แยกฐาน (217xx net) vs VAT (21913) **ตามที่ใบมัดจำ book จริง**
  (แต่ละใบอาจ defer/immediate ต่างกัน) — ยืนยันว่า resolve จาก `depositAppliedRef` แล้วกลับตามบัญชี/
  ยอดจริงของใบมัดจำนั้น ✔?
- **B.** โหมด display-only (`depositAppliedDrivesJournal=false`) — ถ้า `depositAppliedRef` ชี้เอกสารที่
  หาไม่เจอ ต้อง **ไม่ 404** (แค่แสดง text). *(ฝั่ง TakeTime กันไว้แล้ว: ส่ง ref เฉพาะเมื่อใบมัดจำ sync +
  book แล้ว — แต่ขอ NextAcc ยืนยัน behavior นี้กันเคสตกหล่น).*

---

## สถานะฝั่ง TakeTime (ทำเสร็จแล้ว รอ NextAcc)

| ฟีเจอร์ | สถานะ | Gate |
|---|---|---|
| ส่ง `depositAppliedAmount`/`Ref` (display หักมัดจำ/สุทธิ) | ✅ | ส่งเมื่อใบมัดจำ resolve เลข NextAcc |
| `depositAppliedDrivesJournal` (JE self-contained) | ✅ | config `Nexaacc_Deposit_Drives_Journal` (default off) + checkbox Admin |
| Auto-backfill ใบมัดจำ legacy ก่อนเช็คเอาท์ | ✅ | อัตโนมัติ (self-heal) |
| Void guard (ไม่ double-reverse JV) | ✅ | เช็ค JV จริงด้วย reference |

**ลำดับเปิด drives mode:** NextAcc deploy (รองรับ + ข้อ 1 un-realize) → TakeTime rebuild → ติ๊ก
checkbox / ตั้ง `Nexaacc_Deposit_Drives_Journal=1`. เปิดก่อน NextAcc พร้อม = GL ไม่บาลานซ์.
