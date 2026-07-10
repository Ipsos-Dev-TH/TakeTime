# คำขอ/ประเด็นค้าง ฝั่ง NextAcc — Deposit & Checkout Document

> **สถานะ (ก.ค. 2026): ✅ ปิดครบทุก request — NextAcc ยืนยันเสร็จทั้งหมด.** เหลือแค่ฝั่ง TakeTime
> rebuild (Windows) → deploy → ทดสอบ end-to-end + เปิด drives mode ด้วย config/checkbox.
> บริบท: hotel booking → รับมัดจำ (Receipt/ใบเสร็จมัดจำ) → เช็คเอาท์ (ใบกำกับภาษี/ใบเสร็จรับเงิน หักมัดจำ).
> เอกสารนี้เก็บไว้เป็น audit trail ของสัญญา/การตัดสินใจ (ไม่ใช่ backlog ที่ค้างแล้ว).

TakeTime ส่งบน checkout Receipt (DocumentType=3) แล้ว: `depositAppliedAmount`, `depositAppliedRef`
(เลขเอกสาร NextAcc ของใบมัดจำ), `depositAppliedDrivesJournal` (config `Nexaacc_Deposit_Drives_Journal`).

---

## ✅ เสร็จแล้ว (NextAcc — ยืนยันครบ)
- **drives รับ journal ref (JV-INT):** cb55e3b — depositAppliedRef เป็น JournalEntry.EntryNumber ได้
  → กลับ deferred (217xx/21913) จากบรรทัด Cr ของ journal ใน JE ใบเดียว (self-contained). TakeTime
  รองรับแล้ว (config `Nexaacc_Drives_Journal_Ref`, commit 4c34f5c/72b37b1).
- **confirmation A** (แยกฐาน/VAT ตามใบมัดจำจริง): ✅ cb55e3b กลับจากบรรทัด Cr จริง.
- **double-reverse guard:** ✅ NextAcc มี `DepositAppliedToDocumentId`.
- **#1 Un-realize on void:** ✅ เสร็จ (NextAcc ยืนยัน) — void เช็คเอาท์ → clear `DepositAppliedToDocumentId`
  ทั้ง Document ref และ journal ref (JV-INT) โดยไม่ void ใบมัดจำ → CREATE#2 re-realize ได้ (ดู §1).
- **#2 PDF page-break:** ✅ เสร็จ — ส่วน "การบันทึกบัญชี" ขึ้นหน้าใหม่เสมอ (ดู §2).
- **#2b bookingNumber บน CN/DN:** ✅ เสร็จ (NextAcc commit 40ed760) — `InboundCreditNoteRequest` +
  `InboundDebitNoteRequest` รับ JSON key `bookingNumber` (string) → `Document.BookingNumber` เหมือน invoice.
  TakeTime ส่งครบแล้ว (commit c255a03). ครบทั้ง 3 endpoint: invoice / credit-note / debit-note (ดู §2b).
- **#3 แยกเลขมัดจำ:** คงเลข series เดิม (REC ร่วม, gap-free §86/4) — ตัดสินใจไม่แยก (ดู §3).
- **confirmation A + B (display-only missing-ref ไม่ 404):** ✅ ยืนยันแล้ว.
- **un-realize ครอบ DELETE + self-heal stale mark (84cdd29):** ✅ ปิด gap สุดท้ายของ #1 — เดิม un-realize
  ทำเฉพาะตอน **void** แต่เอกสารเช็คเอาท์ที่ถูก **ลบ (hard delete/purge)** ไม่ปลดมาร์ค
  `DepositAppliedToDocumentId` → มัดจำค้างสถานะ "ถูกหักกับเอกสารอื่นแล้ว" กับผีเอกสาร → เช็คเอาท์ใหม่
  โดน 400 "ถูกนำไปหักกับเอกสารอื่นแล้ว (กัน reverse ซ้ำ)" (เคส 148968 ที่ลบ+สร้างซ้ำ ~10 รอบ).
  NextAcc แก้ทั้ง 2 ทาง: **(1) self-heal** — guard เช็คว่าเอกสารที่อ้างยังมีชีวิตก่อน throw; ถูกลบ/void →
  มาร์คโมฆะ → อนุญาตหักซ้ำ (ครอบ delete/purge/void ทุกเส้นทาง) **(2) proactive** — purge un-mark JV
  ที่เอกสารหักไว้ กันสะสม stale. net-balance guard ยังคุมยอด (ปลดแค่ธง ไม่หักเกิน).
  TakeTime ฝั่งเรา: error hint (891fce0) คงไว้เป็น diagnostic; ไม่ต้อง manual clear อีก — Retry แล้ว
  self-heal ปลดเอง → drives ผ่าน single-JE.
- **drives-resolve net-balance + closure (c74eaed → f13f4af):** ✅ NextAcc เลิกพึ่ง `ReversedByEntryId`
  → คำนวณ **net GL จริง = Σ(Cr−Dr) บนบัญชีมัดจำของทั้ง reverse-family** (transitive closure ตาม
  `OriginalEntryId` ทุกชั้น, นับเฉพาะ Posted + ไม่ถูกลบ). รองรับ un-reverse ทุกวิธี (reversal-of-reversal /
  void reversal / delete reversal) → net กลับ live → drives หักได้.
  **กลไก TakeTime (ยืนยัน — อยู่ในเคสที่ครอบคลุมครบ):** auto-recover un-reverse ผ่าน `ReverseJournalAsync`
  → `POST /api/integration/journals/reverse` ตั้ง `OriginalJournalEntryId` = ตัว reversal → NextAcc post
  **reversal-of-reversal JE จริง ที่ link กลับ family** (`OriginalEntryId` ชี้ตัว reversal) → closure เห็น →
  net live. **ไม่เข้า edge** (เราไม่ได้ post JV ใหม่แบบไม่ link / ไม่ได้ลบ-void ฝั่งเดียว) → ไม่ต้องใช้ fallback
  bookingNumber/reference. (feature `Nexaacc_Auto_Recover_Deposit`, commit b92b232.)

**เหลือฝั่ง TakeTime:** pull build ล่าสุด → rebuild (Windows) → deploy → ทดสอบ end-to-end
(deposit → checkout → void + CN/DN ผูก booking) → เปิด drives mode (ดู "ลำดับเปิด drives mode" ท้ายไฟล์).

---

## 1. ✅ [เสร็จ] Un-realize ใบมัดจำ ตอน void เอกสารเช็คเอาท์ (ทั้ง Document REC- และ journal JV-INT)

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

## 2. ✅ [เสร็จ] Page-break ก่อนส่วน "การบันทึกบัญชี" บน PDF เอกสาร

**บริบท:** PDF เอกสาร (Receipt/TaxInvoice) มีส่วน "การบันทึกบัญชี" (JE Dr/Cr) ต่อท้าย.

**ปัญหา:** ส่วน JE ไหลต่อจากเนื้อหาลูกค้า → บรรทัด Dr/Cr **ถูกตัดคนละหน้า** (เช่น Dr อยู่หน้า 1,
Cr อยู่หน้า 2) อ่านยาก.

**ขอ:** ใส่ **page-break ก่อนส่วน "การบันทึกบัญชี"** ให้ขึ้นหน้าใหม่เสมอ
→ หน้า 1 = เอกสารลูกค้า (สะอาด) / หน้าถัดไป = การบันทึกบัญชี (ครบในหน้าเดียว ไม่ตัด).

---

## 2b. ✅ [เสร็จ] `bookingNumber` บน Credit Note / Debit Note DTO

**บริบท:** NextAcc เพิ่ม `bookingNumber` (เลขการจอง `RES-{id}`) ให้ **integration invoice** ก่อน
(commit 8ed90ba) แล้ว **ขยายไป CN/DN ด้วย (commit 40ed760):** `InboundCreditNoteRequest` +
`InboundDebitNoteRequest` รับ JSON key `bookingNumber` (string) → wire เข้า `Document.BookingNumber`
เหมือน invoice ทุกประการ.

**TakeTime (commit c255a03):** ส่ง `bookingNumber = RES-{id}` บน `/api/integration/credit-notes`
และ `/debit-notes` ครบทุก mapper ที่อิงการจอง — `MapRefundToCreditNote` (คืนเงิน) /
`MapReceiptVoidToCreditNote` (ยกเลิกใบเสร็จ) / `MapDamageChargeToDebitNote` (ค่าเสียหาย).
(`MapVoucherVoidToDebitNote` อิงใบสำคัญจ่าย ไม่ใช่การจอง → ไม่ส่ง.)

**ผล:** ครบทั้ง 3 endpoint — invoice / credit-note / debit-note — group ทุกเอกสารของการจองด้วยคีย์
เดียว (`bookingNumber`) filter/ค้นตาม booking ได้. (ก่อนหน้านี้ CN/DN ก็โยงได้ผ่าน `externalRef`
`CN-RES-{id}`/`DN-DMG-{id}` + `originalInvoiceRef` — bookingNumber ทำให้เป็นฟิลด์ dedicated เหมือน invoice.)

---

## 4. ✅ [เสร็จ] ช่อง "ผู้รับเงิน/ผู้จัดทำ" บนเอกสาร company `/document` = คนทำจริง (ไม่ใช่ NextAcc user)

> **NextAcc เสร็จแล้ว (commit e6908cb):** company `/document` (CreateDocumentRequest) รับ
> `preparerName` / `preparerSignatureBase64` (JSON camelCase, string) + PDF ให้ preparer ที่ส่งมา
> **priority เหนือ CreatedBy** สำหรับช่อง "ผู้รับเงิน" (slot 0) ทั้งชื่อ+ลายเซ็น; "ผู้มีอำนาจลงนาม"
> (slot 1 = กรรมการ) คงเดิม → ผู้รับเงิน = ชวนพิศ, ผู้มีอำนาจลงนาม = วชิร. ไม่ส่ง field = ignore เงียบ.
>
> **TakeTime (commit 1c86a93):** ส่ง preparer บน create ทุกเอกสาร company รับ (`ApplyReceiptPreparer`).
>
> **UpdateDocumentRequest (PUT) — ✅ NextAcc เสร็จ (commit a30525e) + TakeTime wire แล้ว:** เอกสารที่
> NextAcc สร้างจาก **OCR** (`Voucher/OcrUpload`) เราไม่คุมตอน create → ยัดผู้จัดทำผ่าน PUT
> (`ApplyCurrentUserPreparer` จาก Session["UserID"] → Admin ชื่อ+ลายเซ็น) เพราะ `X-Acting-User` ช่วยเฉพาะ
> เมื่อ staff เป็น NextAcc user. PUT รับ `preparerName`/`preparerSignatureBase64` + priority เดียวกับ create
> (slot 0 ชนะ CreatedBy; null=ไม่แตะ, ""=ล้าง, ค่า=ตั้ง).
>
> **edge case (NextAcc flag):** ถ้า OCR สร้างเป็น **Approved** (ไม่ใช่ Draft) PUT จะติด "แก้ได้เฉพาะ Draft".
> **flow เราไม่เข้าเคสนี้:** `CreateDocumentFromOcrAsync` สร้าง **Draft เสมอ** → PUT ตอน Draft → approve
> ทีหลัง. ทางเดียวที่ resume แล้วเจอ Approved = approve สำเร็จแต่ response หลุด → รอบนั้น preparer ถูกตั้ง
> ใน PUT ตอน Draft ไปแล้ว (ไม่เสียข้อมูล). NextAcc เตรียม carve-out (preparer แก้ได้ทุกสถานะ เพราะ display-only)
> ไว้เป็น safety-net — ไม่ต้องเปิดสำหรับ flow ปัจจุบัน.

<details><summary>รายละเอียดเดิม (audit trail)</summary>

**อาการ:** ใบเสร็จ/ใบกำกับที่ออกจากเช็คเอาท์ (company Receipt DocumentType=3) ช่อง **"ผู้รับเงิน"**
บน PDF ขึ้นเป็น **NextAcc user เจ้าของ API key (เจ้าของ/กรรมการ เช่น "วชิร ดิลกสัมพันธ์")** ไม่ใช่พนักงาน
ที่สร้างใบจริงในระบบ TakeTime (เช่น "ชวนพิศ …").

**เหตุ (verified):** `CreateDocumentRequest` (company `/document`) **ไม่มีฟิลด์ preparer/ผู้รับเงิน** →
NextAcc `PdfGenerationService` เลย fallback ไปใช้ลายเซ็น `CreatedBy` user (= เจ้าของ API key). แม้จะมี
`Document.PreparerSignatureBase64` ฝั่ง NextAcc ก็ priority **ต่ำกว่า** `CreatedBy.SignatureImageBase64`
(ใช้ก็ต่อเมื่อ CreatedBy user ไม่มีลายเซ็น) → เจ้าของมีลายเซ็น เลยชนะเสมอ.

**TakeTime ทำแล้ว (forward-compatible):** เพิ่ม `PreparerName` + `PreparerSignatureBase64` ใน
`CreateDocumentRequest` (JSON `preparerName` / `preparerSignatureBase64`) + wire จากพนักงานที่สร้างใบ
(`Account_Receipt.Created_By_ID → Admin` ชื่อ + ลายเซ็น) เข้าทุกเอกสาร company (ใบเสร็จมัดจำ/เช็คเอาท์/
ใบกำกับ) ผ่าน `ApplyReceiptPreparer`. NextAcc record ignore ได้ถ้ายังไม่รองรับ.

**ขอ NextAcc (2 อย่าง):**
1. รับฟิลด์ `preparerName` + `preparerSignatureBase64` บน `CreateDocumentRequest` (company `/document`).
2. **ให้ priority เอกสารที่ส่ง preparer มา "เหนือ" `CreatedBy` user** สำหรับช่อง "ผู้รับเงิน/ผู้จัดทำ"
   บน Receipt/TaxInvoice — คือถ้า request มี `preparerName`/`preparerSignatureBase64` → ใช้ตัวนี้ก่อน
   (ไม่ fallback ไป CreatedBy user แม้เจ้าของมีลายเซ็น). ช่อง "ผู้มีอำนาจลงนาม" คงเป็นเจ้าของ/กรรมการเหมือนเดิม.

**ผลที่ต้องการ:** ผู้รับเงิน = ชวนพิศ (คนทำจริง) / ผู้มีอำนาจลงนาม = วชิร (กรรมการ). ตรงกับใบสำคัญจ่าย
(integration PV) ที่ส่ง `PreparerName`/`PreparerSignatureBase64` ได้อยู่แล้ว — ขอให้ company document
รองรับแบบเดียวกัน.

</details>

---

## 5. [ใหม่ — BUG ต้องแก้] drives เคส document-deposit + deferred VAT: JE ขาดขา Dr 21913

**เคสจริง (REC-20260707-0002, booking 149025):** ใบมัดจำเป็น **เอกสาร REC-20260707-0001** (เคส b)
book แบบ defer VAT (`DepositOutputVatDeferred=true` → Cr 21510 1,448.60 + **Cr 21913 101.40**).
เช็คเอาท์ drives โพสต์ JE:
```
Dr เงินฝาก 2,850.00 + Dr 21510 1,448.60 / Cr 21911 186.45 + Cr 41110 4,112.15   (รวม 4,298.60)
```
**ผิด:** สมมติว่า VAT มัดจำเข้า 21911 ไปแล้วตอนรับมัดจำ (พฤติกรรม no-defer) ทั้งที่ใบมัดจำ defer ไว้ 21913.
**ผล:** 21913 ค้าง Cr 101.40 ถาวร (VAT มัดจำไม่เข้า ภ.พ.30) + 21911 ขาด 101.40 + JE รวม 4,298.60 ≠ เอกสาร 4,400.

**JE ที่ถูกต้อง (defer mode):**
```
Dr เงินฝาก 2,850.00 + Dr 21510 1,448.60 + Dr 21913 101.40
/ Cr 21911 287.85 (เต็ม) + Cr 41110 4,112.15                                    (รวม 4,400 = เอกสาร)
```

**ขอแก้:** drives ต้องอ่าน "ขา VAT จริงของใบมัดจำ" (ตาม confirmation A ที่ยืนยันไว้) — ครอบ **เคส b
(document REC-)** ด้วย ไม่ใช่เฉพาะ JV-INT: ใบมัดจำ Cr 21913 → JE เช็คเอาท์ต้องมี Dr 21913 + Cr 21911 เต็ม;
ใบมัดจำ Cr 21911 (no-defer) → JE แบบปัจจุบันถูกแล้ว.

**TakeTime ระหว่างรอ:** verify เพิ่ม invariant ขา VAT (Σ21911 คู่มัดจำ+เช็คเอาท์ = VAT เอกสาร, Σ21913 = 0)
→ จับได้แล้ว + auto-fix (`FixStuckDeferredVatAsync` — JV Dr 21913/Cr 21911 ยอดวัดจริง, idempotent
`{receipt}-DEPVATFIX`, gate `Nexaacc_Auto_Reconcile_Deposit`). เมื่อ NextAcc แก้ต้นเหตุแล้ว auto-fix
จะไม่มีอะไรให้ทำเอง (stuck=0 → ข้าม).

**หลักการร่วม 2 ฝั่ง (สัญญา — "อ่านตามที่ลงจริง อย่า force โหมด"):** ทุกจุดที่ต้องหักมัดจำ/กลับมัดจำ —
เมื่อ **เจอ** เอกสารใบมัดจำ/JE → **อ่านขาที่ลงจริง** (มัดจำล้วน gross / แยก net+21913 defer / แยก net+21911
immediate) แล้วใช้ค่าตามนั้น; เมื่อ **หาไม่เจอจริง ๆ** เท่านั้น → หักแบบ gross ไม่มีขา VAT + ให้ verify
ตรวจ/ปรับต่อ. TakeTime บังคับใช้ครบแล้ว:
- JV หักมัดจำ (SettleReceiptDoc step 3): `GetDepositMirrorLegsAsync` อ่านขา Cr จริงจาก JE ใบมัดจำ →
  `MapDepositAdjustmentFromActualLegs` mirror ทุกขา (ไม่ใช้ config) — fallback gross เมื่อหาไม่เจอ.
- Void: `TryReverseJournalByReferenceAsync` กลับ JV -DEPADJ **ตัวจริง** account-for-account
  (undo ตรงตามที่โพสต์ ไม่สร้าง counter จาก config) — fallback counter เดิมเมื่อ reverse ไม่ได้.
- TryReverseDepositJournals (กลับใบมัดจำ) ใช้ ReverseJournalAsync บนตัวจริงอยู่แล้ว.
เหตุ: config สลับได้กลางทาง (เคส 148968: adjustment คนละโหมดซ้อน → 21510 = −967.29 = 500 gross +
467.29 net). **ขอ NextAcc ใช้หลักเดียวกันกับ drives ทุกเคส (ข้อ 5 ด้านบน).**

---

## 3. ✅ [ปิด — คงเดิม] เลข running-number ชุดแยกสำหรับ "ใบกำกับภาษี/ใบเสร็จรับเงิน"

> **ตัดสินใจ:** NextAcc คงเลข series เดิม (REC ร่วมกับใบเสร็จมัดจำ, gap-free §86/4) — ไม่แยกชุด.
> ตามกฎหมายใช้ชุด REC ได้ ไม่ผิด. รายละเอียดเดิมด้านล่างเก็บไว้เป็น audit trail.


**บริบท:** เช็คเอาท์ลูกค้า walk-in ใช้ **DocumentType=Receipt(3)** (จำเป็น — TaxInvoice(4) ติด §86/4
เพราะ walk-in ไม่มีเลขภาษี+ที่อยู่). Receipt(3)+VAT render เป็น "ใบกำกับภาษี/ใบเสร็จรับเงิน" แต่ได้
**เลขชุด REC** ปนกับใบเสร็จมัดจำ.

**ขอ (ถ้าทำได้):** running-number **ชุดแยก** สำหรับเอกสาร "ใบกำกับภาษี/ใบเสร็จรับเงิน" (แทนที่จะปน
ชุด REC เดียวกับใบเสร็จมัดจำ) — เพื่อ audit trail ภาษีขาย.

**หมายเหตุ:** ตามกฎหมายใช้ชุด REC ได้ ไม่ผิด (ความเป็นใบกำกับมาจากเนื้อหา ไม่ใช่ prefix) — ข้อนี้เป็น
preference ไม่ใช่ blocker. **ห้ามแก้เป็น DocumentType=TaxInvoice(4)** เพราะจะทำให้ walk-in ถูก §86/4
ปฏิเสธ + เสียคุณสมบัติ "จ่ายจบในใบ + ลายเซ็นผู้จัดทำ".

---

## ยืนยันเพิ่มเติม (confirmation) — ✅ ยืนยันครบ

- **A.** `depositAppliedDrivesJournal` แยกฐาน (217xx net) vs VAT (21913) **ตามที่ใบมัดจำ book จริง**
  (แต่ละใบอาจ defer/immediate ต่างกัน) — ✅ resolve จาก `depositAppliedRef` แล้วกลับตามบัญชี/ยอดจริง.
- **B.** โหมด display-only (`depositAppliedDrivesJournal=false`) — ถ้า `depositAppliedRef` ชี้เอกสารที่
  หาไม่เจอ **ไม่ 404** (แค่แสดง text). ✅ ยืนยัน. (ฝั่ง TakeTime กันไว้แล้ว: ส่ง ref เฉพาะเมื่อใบมัดจำ
  sync + book แล้ว.)

---

## สถานะฝั่ง TakeTime — ✅ พร้อม (เหลือ rebuild + deploy + test)

| ฟีเจอร์ | สถานะ | Gate |
|---|---|---|
| ส่ง `depositAppliedAmount`/`Ref` (display หักมัดจำ/สุทธิ) | ✅ | ส่งเมื่อใบมัดจำ resolve เลข NextAcc |
| `depositAppliedDrivesJournal` (JE self-contained) | ✅ | config `Nexaacc_Deposit_Drives_Journal` (default off) + checkbox Admin |
| Auto-backfill ใบมัดจำ legacy ก่อนเช็คเอาท์ | ✅ | อัตโนมัติ (self-heal) |
| Void guard (ไม่ double-reverse JV) | ✅ | เช็ค JV จริงด้วย reference |
| Deposit-consumed marker (กันเรียกใช้มัดจำซ้ำ) | ✅ | migration PHASE18_05 (auto no-op ก่อน migrate) |
| `bookingNumber` บน invoice + CN + DN | ✅ | อัตโนมัติ (RES-{id} ทุกเอกสารที่อิงการจอง) |

**ลำดับ go-live:**
1. Pull build ล่าสุด (NextAcc: `claude/fix-errors-638kW`) → **rebuild บน Windows** (WebForms build บน Linux ไม่ได้).
2. Deploy build ล่าสุด (ต้องมีโค้ด drives + safety-net) **ก่อน** รัน migration เปิด drives.
3. รัน migration ค้าง (idempotent):
   - **PHASE18_05** (deposit-consumed marker กันใช้มัดจำซ้ำ)
   - **PHASE18_06** (เปิด drives mode: `Nexaacc_Deposit_Drives_Journal=1` + `Nexaacc_Drives_Journal_Ref=1`)
4. ทดสอบ end-to-end: deposit → checkout (JE เดียว Dr เงินฝาก = รับจริง ไม่ใช่เต็มยอด) → void → CREATE#2
   (re-realize) + CN/DN ผูก booking.

**หมายเหตุ drives mode:** ตั้งเป็น "เปิด" ผ่าน PHASE18_06 แล้ว (NextAcc ทำ #1 un-realize on void + journal-ref
ครบ → ปลอดภัย GL บาลานซ์). เดิม display-only (flags=0) การหักมัดจำแยก 2 JE → เงินฝากสุทธิ 950 ถูก แต่
"ตัว JE ของเอกสารโชว์เต็มยอด 1,450" (การกลับมัดจำอยู่คนละใบ) → drives รวมเป็น JE เดียว Dr เงินฝาก 950 ตรงตัว.
ถอยกลับได้ทุกเมื่อ (ตั้ง flags='0' — GL ยังถูกผ่าน JV แยก). safety-net auto-fallback ถ้า NextAcc endpoint พลาด.
