# CLAUDE.md — TakeTime BangPhra

Hotel/restaurant management system (ASP.NET **WebForms**, .NET Framework 4.7.2, C#).
Main project: `Take Time BangPhra/`. Database scripts: `Database/` (phased migrations).

> Build/test only on Windows + IIS (WebForms cannot build on Linux containers).
> The `code` helper class lives in namespace `Take_Time_BangPhra` (Code.cs). Files
> without a namespace block must use `Take_Time_BangPhra.code` or add the using.

## NextAcc accounting integration

NextAcc = "Nexaacc" accounting backend. **Source of truth repo:** `Wachira-d/Accounting`
→ **https://github.com/Wachira-d/Accounting** (ดู code/สัญญา API ล่าสุดของ NextAcc ที่นี่เสมอ —
authoritative contract, ห้ามเดา. clone หรือเปิด GitHub อ่านจริงก่อน map endpoint ใด ๆ).

TakeTime side lives in `Take Time BangPhra/Class/Integration/`:
`AccountingConfig`, `AccountingApiClient`, `AccountingDataMapper`, `AccountingSyncService`,
`AccountingModels`. Admin UI: `Admin/Settings/AccountingIntegration.aspx`.
Config in DB table `Accounting_Integration_Config` + `Accounting_Account_Mapping`
(+ `Account_Paid_How` / `Account_Paid_Type` for แหล่งเงิน / หมวดค่าใช้จ่าย).

### TWO API surfaces / TWO keys — this is the crux

| Surface | Auth | Routes | Use |
|---------|------|--------|-----|
| **Integration** | `X-Integration-Key` / `Authorization: Bearer` with **`int_`** key | `/api/integration/*` | one-shot create (invoice, expense, payment-voucher, payment, journal, customer). Background sync uses this. |
| **Company** | **`acc_`** API key (acts as the company Bearer) | `/api/companies/{companyId}/*` | OCR, AI suggestions, document approve, deposit deferred-VAT docs, payment with account override + payer signature, chart of accounts |

`AccountingConfig.IsIntegrationKey => ApiKey.StartsWith("int_")`. **Key-type reality (verified
in NextAcc `ApiKeyMiddleware`):** the `X-Api-Key` header (used for all `/api/companies/*` calls)
looks up the `ApiKey` table (`acc_`) first, then **falls back to the `ExternalIntegration` table
(`int_`)** — so **an `int_` key authenticates company endpoints too** (full access within its
company). Conversely `/api/integration/*` is authed by `X-Integration-Key` → `ExternalIntegration`
only, so **an `acc_` key CANNOT do the core sync**. ⟹ **Configure the system with an `int_` key**
(it covers both surfaces); `acc_` breaks `/api/integration/*` (TestConnection 401). The client picks
the header by PATH, not key type. Gate company-endpoint features on
`AccountingConfig.CanUseCompanyEndpoints` (= `CompanyId` set + `Nexaacc_Company_Endpoints` flag,
default `1`), **NOT** `!IsIntegrationKey`. Set the flag to `0` only if a deployment's NextAcc is too
old to have the `X-Api-Key` int_ fallback (then everything routes via `/api/integration/*`).
**Dual-key (recommended):** the admin page accepts TWO keys — `Nexaacc_ApiKey_Encrypted` (the `int_`
Integration Key, required, sent as `X-Integration-Key` on `/api/integration/*`) and the optional
`Nexaacc_CompanyApiKey_Encrypted` (an `acc_` API Key, sent as `X-Api-Key` on `/api/companies/*`).
`AccountingConfig.CompanyApiKey` returns the dedicated `acc_` key, or falls back to `ApiKey` (int_)
when unset. Setting both makes each surface auth with its native key type (no reliance on the
`X-Api-Key` int_ fallback); a single `int_` key still works via the fallback. The client uses
`_config.ApiKey` for `X-Integration-Key` headers and `_config.CompanyApiKey` for `X-Api-Key` headers.

### Verified API contracts (from Wachira-d/Accounting @ HEAD, June 2026)

DTOs are C# **records** (positional). Response wrapper: `{ Success, Data, Message }`.

#### Integration (int_) — IntegrationController.cs / IntegrationDtos.cs
- `POST /api/integration/payment-vouchers` — `InboundPaymentVoucherRequest`
  (ExternalId, ExternalRef, Supplier{ExternalId,Name,TaxId}, DocumentDate, PaymentDate?,
  Lines, VatRate?, Notes, IncludeVat=true, Attachments?, **PreparerName?, PreparerSignatureBase64?**).
  One-shot: Dr expense + Dr InputVAT / **Cr Cash** (+ Cr WHT 2191x). Auto-issues WHT cert.
  **NO credit-account override** — always CR cash. Preparer signature → "ผู้จัดทำ" slot.
- `POST /api/integration/payments` — `InboundPaymentRequest`
  (ExternalId, ExternalRef, InvoiceExternalRef, DocumentId?, Customer{ExternalId,Name},
  PaymentDate, Amount, PaymentMethod (string: Cash/BankTransfer/CreditCard/PromptPay/Cheque/EWallet),
  BankAccountName, ReferenceNo, SlipUrl, Notes).
  **NO `OverridePaymentAccountId`, NO payer signature.** (⚠️ earlier TakeTime commit added
  these to `CreateIntegrationPaymentRequest` — NextAcc IGNORES them on this endpoint.)
- `POST /api/integration/expenses` — `InboundExpenseRequest` (Draft; +PreparerName/Signature).
- `POST /api/integration/invoices` (+ `/multipart`) — deposits: **deposit flags NOT in this DTO.**
- `POST /api/integration/journals`, `/credit-notes`, `/debit-notes`, `/batch` (≤500/type),
  `/documents/void` (by ExternalRef/DocumentId, cascades JE + payment reversal).
- Customers: `/api/integration/customers` — `InboundCustomerRequest` has Address + structured
  fields + ContactType ("Individual"/"JuristicPerson"/"GovernmentAgency").

#### Company (acc_) — DocumentController.cs / OcrController.cs / AiSuggestionController.cs
- `POST /api/companies/{cid}/documents/payments` — `CreatePaymentRequest`:
  DocumentId, PaymentDate, Amount, PaymentMethod (enum int: Cash=1, BankTransfer=2, CreditCard=3,
  Cheque=4, PromptPay=5, DirectDebit=6, EWallet=7, Other=99), Reference, BankAccount,
  **OverrideBankAccountId?, OverridePaymentAccountId?** (e.g. เจ้าหนี้กรรมการ),
  WithholdingTaxAmount?, ProjectId?, Allocations?, **PayerSignatureBase64?, PayerSignatureName?**.
  → renders "ผู้จ่ายเงิน" (slot 0). Response: HasPayerSignature, PayerSignatureName.
- `POST /api/companies/{cid}/documents` — `CreateDocumentRequest` for **DEPOSIT**:
  IsDeposit=true, DepositDeferredAccountCode (default 21712 รายได้ยังไม่รับรู้),
  **DepositOutputVatDeferred** (true → Cr **21913 ภาษีขายรอเรียกเก็บ/รอรับรู้**, not into ภ.พ.30
  until realized; false → Cr 21911 ภาษีขาย immediately). Carry reservation ref in Reference.
- `POST /api/companies/{cid}/documents/{id}/realize-deposit` — `RealizeDepositRequest`
  (Amount, RealizeDate?, RevenueAccountCode?, FinalInvoiceId?): at check-in, Dr 217xx→Cr 41xxx
  and (if deferred VAT) Dr 21913→Cr 21911. Query: `GET .../documents/deposits?status=outstanding|realized`.
- `POST /api/companies/{cid}/documents/{id}/approve` — `ApproveDocumentRequest`
  (Notes?, AcknowledgeWarnings=false). 422 = soft warnings + AiHints (resend with Acknowledge=true).
  Auto-posts GL. Does NOT auto-pay.
- **OCR**: `POST /api/companies/{cid}/ocr/upload` (multipart `file`; query `preferredEngine`,
  `autoCreate`; accepts int_ OR acc_). Returns `OcrResultResponse` (extractedVendorName/TaxId/
  DocumentNumber/Date/SubTotal/VatAmount/TotalAmount, buyerName/TaxId, extractedItems[] with
  suggestedAccountCode, matchedContactId, suggestedAccounts{debit/credit/vat}, hasWht/whtRate,
  ourRole Buyer/Seller, targetDocumentType, dbdInfo{canonicalName,address,juristicType}, quality).
  `autoCreate=false` (web default) = OCR only, file sits in OCR inbox, no doc created.
- `POST /api/companies/{cid}/ocr/{scanId}/create-document?targetType=PaymentVoucher|Expense|...`
  → creates **Draft** doc (auto-creates Contact by TaxId then Name). Then call approve.
- **AI suggest** (all `POST /api/companies/{cid}/ai/...`, return {value, confidence, reasoning, feedbackId}):
  `payment-voucher/suggest-account`, `vat/infer-type` (7/0/Exempt), `wht/infer-category` (3/53/…),
  `payment-voucher/suggest-type` (Cash/Credit), `payment-terms/suggest`, `contact/fuzzy-match`.

#### VAT claim rules (TaxInvoiceCompletenessChecker.cs)
Input VAT claimable (`IsVatClaimable=true`, line-level) requires a **full tax invoice §86/4**:
supplier Name + TaxId(13, mod-11) + Address + BranchCode + SupplierInvoiceNumber +
SupplierTaxInvoiceDate, and `HasTaxInvoiceReference=true`. Incomplete → posted to **11640
ภาษีซื้อยังไม่ถึงกำหนด** (suspense), `InputVatPostedAsUndue=true`; reclassified to **11610**
when completed. Non-claimable (§82/5: professional fee / personal car / entertainment) → VAT
bundled into expense. **A plain "ใบเสร็จรับเงิน" (no buyer details) is NOT claimable** — this is
why uploading a receipt and claiming VAT is wrong; need a proper tax invoice via OCR→§86/4 fields.

#### Official resync contract (docs/NextAcc_Integration_Resync_Contract.md — ก.ค. 2026)
`POST /api/integration/invoices|expenses` + **`resyncUpdate:true`** + `externalRef` เดิม →
NextAcc แก้เอกสารเดิมเอง: งวดเปิด+JE เดียว = **in-place (เลข JE คงเดิม)** / งวดปิด-หลาย JE =
**reversal+post ใหม่** — **เลขเอกสารคงเดิมเสมอ ไม่มี void**. ตรวจผลจาก `message` prefix:
`"Resync updated (in-place)"` / `"(reversal)"` / `"Already synced"` (รุ่นเก่า → fallback void→create)
/ `success:false` = ติด guard (ชำระแล้ว/มี CN-DN/เดือนภาษียื่น ภ.พ.30 แล้ว — แสดงข้อความให้ผู้ใช้).
ไม่รองรับ: credit-notes/debit-notes/payments. TakeTime: `RepostReceiptWithCurrentLogic` ใช้เส้นนี้
ก่อนเมื่อเอกสารเดิมเป็น INVOICE (`BuildCorrectedReceiptInvoice` + `ResyncUpdate=true`), เอกสาร
RECEIPT/JOURNAL ใช้ JE in-place (SearchJournals→UpdateJournalEntry) → fallback void→recreate.
`LastRepostMessage` เก็บผลให้ UI. Return: 0 = แก้แบบไม่ void, >0 = fallback queue id, -1 = guard/fail.

#### JE mutability (AccountingService.UpdateJournalEntryAsync — updated ก.ค. 2026)
`PUT /api/companies/{cid}/accounting/journals/{entryId}` (`UpdateJournalEntryRequest`:
EntryDate?/Description?/Reference?/Lines? — Lines != null = แทนที่ทั้งชุด) นโยบาย
**operator-override: แก้ได้ทุกสถานะรวม Posted** ตราบเท่าที่งวดบัญชี (FiscalPeriod) ยัง **Open**
(งวด Closed → 400). **นโยบายล่าสุด (ก.ค. 2026 ตามที่ dev NextAcc ยืนยัน): อนุญาตแก้ JE โดยไม่ต้อง
void แล้ว — รวมถึง JE จากเอกสาร**; โค้ดเวอร์ชันก่อนหน้า (commit 755feb4d ที่ search index เห็น)
ยังบล็อก `IsAutoGenerated` อยู่ → **TakeTime ออกแบบรองรับทั้งสองรุ่น**: ปุ่ม Retry บนคิว COMPLETED
CREATE_RECEIPT_DOCUMENT = `RepostReceiptWithCurrentLogic` **ลองแก้ JE in-place ก่อนเสมอ**
(SearchJournalsAsync หา JE จาก Reference=เลขใบเสร็จ → `UpdateJournalEntryAsync` แทนที่ Lines ด้วย
ชุดจาก `BuildCorrectedReceiptJournal` — mapper เดียวกับ sync ปกติ) → ถ้า NextAcc ปฏิเสธ
(รุ่นเก่า/งวดปิด/ไม่พบ JE) จึง fallback void→สร้างใหม่เลขเดิม. คิว COMPLETED ประเภทอื่นถูกบล็อก
Retry กัน double-post. Return: 0 = แก้ in-place, >0 = queue id ของ CREATE ใหม่ (fallback).

#### Signatures (PdfGenerationService.cs)
"ผู้จัดทำ" slot priority: Payment.PayerSignatureBase64 → Document.PreparerSignatureBase64
(used only if CreatedBy user has no SignatureImageBase64) → CreatedBy.SignatureImageBase64.
"ผู้จ่ายเงิน" slot (PV): Payment.PayerSignatureBase64 → CreatedBy.SignatureImageBase64.
Accepts data-URI or bare base64; cap 512KB. **If signature missing on a synced doc:** our
`LoadSignatureDataUri` returned null (signature file missing on TakeTime server) → check
`AccountingSync` log line `ApplyPreparerSignature: ... signature=แนบแล้ว/ไม่พบไฟล์ลายเซ็น`.

### WHT payable accounts (TakeTime mapping)
`WHT_PAYABLE`=ภ.ง.ด.3 (21916, บุคคล), `WHT_PAYABLE_PND53`=ภ.ง.ด.53 (21917, นิติบุคคล),
`WHT_PAYABLE_PND1`=ภ.ง.ด.1 (เงินเดือน). Juristic detection: Thai tax id 13 digits **starting
with `0`** (helper `AccountingDataMapper.IsJuristicPerson`). In DOCUMENT mode NextAcc auto-splits
21916/21917 by supplier TaxId/ContactType, so send correct ContactType.

### Known gaps / TODO (NextAcc-dependent)
0. **จ่ายจริง = ใบสำคัญจ่าย (ไม่ใช่ค่าใช้จ่าย), DOCUMENT mode + acc_:** ✅ DONE. NextAcc แยกบทบาท
   (DocumentService.cs): **Expense (type 9)** = ตั้งหนี้ (Cr เจ้าหนี้, เงินยังไม่ออก, บังคับ
   PaymentType=Credit) / **PaymentVoucher (type 13)** = จ่ายจริง (Cr เงินสด/ธนาคาร, เงินออก,
   standalone→PaymentType=Cash). เดิม `ProcessVoucherJournal` ใช้ one-shot `/integration/payment-vouchers`
   เฉพาะกรณีจ่ายเงินสด/ธนาคาร (เพราะ endpoint นั้น **บังคับ Cr เงินสด override บัญชีไม่ได้**) → กรณีจ่ายแบบ
   ไม่ใช่เงินสด (เจ้าหนี้กรรมการ) ตกไปสร้าง **Expense** ผิดบทบาท. **Fix:** เมื่อ `CanUseCompanyEndpoints`
   + มี supplier contact + `!isCredit` + `!salary` + **`!autoRecordPayment` (จ่ายไม่ใช่เงินสด/ธนาคาร เช่น
   เจ้าหนี้กรรมการ ต้อง override บัญชีเครดิต)** → สร้าง **PaymentVoucher ผ่าน company `/document`**
   (`MapVoucherToDocument` type 13, `PaymentAccountId`=แหล่งเงิน → Cr บัญชีนั้นตรง ๆ) → `SettleVoucherDocAsync`
   (create+approve, idempotent marker `Account_Payment.Nexaacc_Voucher_Doc_Marker` DOC:→APR:→{id}/VOIDED,
   migration PHASE17_07) → `TryAutoGenerateWhtCertAsync`. **เคสเงินสด/ธนาคาร (`autoRecordPayment`) ใช้
   integration one-shot PV** — เพราะ company `/document` (CreateDocumentRequest) **ไม่มีฟิลด์ลายเซ็นผู้จัดทำ**
   ส่วน integration `InboundPaymentVoucherRequest` มี `PreparerSignatureBase64` → ส่งลายเซ็นได้ (NextAcc
   รองรับลายเซ็นบน integration PV + company payment เท่านั้น ไม่รองรับบน company document). non-cash company PV
   → ผู้จัดทำ fallback ลายเซ็น NextAcc user. **เครดิต→ยังเป็น Expense** (ถูกต้อง — NextAcc ห้าม PV เครดิตลอย ๆ); เงินเดือน
   ยังใช้ expense+Payroll; ไม่มี contact / `int_` → fallback one-shot PV / expense เดิม. Edit = void→สร้างใหม่
   (row reinsert → marker null). **VAT ผสม (มีของไม่เสียภาษีปน):** TakeTime ส่งยอด VAT จริง (`vatAmount`,
   TextBox4) มาด้วย; `MapVoucherToExpense` ตรวจถ้า `vatAmount` ≠ 7% ของ net (ผสม) จะ **แตกเป็น 2 บรรทัด**
   ส่วนมีภาษี (`vatAmount/0.07`@7%) + ส่วนไม่มีภาษี (ที่เหลือ@0%) `IncludeVat=false` → NextAcc คิด VAT ตรง
   (เดิมส่ง VatRate=7 ทั้งใบ NextAcc คิด 7% เต็ม → ยอดเกิน เกิด "ค้างชำระ"). per-line VAT honored บน
   company `/document`. **ข้อจำกัด:** company `/document` ไม่มีช่อง preparer signature (ผู้จัดทำใช้ลายเซ็น
   CreatedBy user แทน); ไม่แนบไฟล์; VAT-mixed split รวมหมวดเป็นบัญชี line แรก. ต้อง build+test บน Windows.
1. **Director-advance credit account (เจ้าหนี้กรรมการ):** ✅ DONE. `AutoRecordPaymentForVoucher`
   now routes to the company `document/payments` endpoint (`CreatePaymentAsync`,
   `OverridePaymentAccountId` + `PayerSignature*`) whenever an override/signature is needed AND an
   `acc_` key is configured; falls back to `/api/integration/payments` (which ignores both) with a
   log line when an `int_` key blocks it. Verified vs Wachira-d/Accounting: `InboundPaymentRequest`
   has neither field; `CreatePaymentRequest` (DocumentController) has both.
2. **Deposit VAT — 3 โหมด (นโยบายบัญชี ผู้ทำบัญชีเลือก; ทุกโหมด GL สมดุลถูกต้อง):**
   verified vs NextAcc `CmsBookingService`: PrePayment (มัดจำ) → **Receipt doc + IsDeposit=true →
   Cr 217xx (ขายรอรับรู้)**; tax point §78/1 รับชำระแล้ว = เกิดทันที (`DepositOutputVatDeferred=false`
   default ฝั่ง NextAcc) → ภาษีขายเข้า ภ.พ.30 เดือนรับเงิน. Guaranteed → TaxInvoice. ⇒ มัดจำเป็น
   **"ใบเสร็จรับเงิน/ใบรับเงินมัดจำ (AdvanceReceipt)" ไม่ใช่ TaxInvoice** ตามดีไซน์ NextAcc — โค้ดเราตรง.
   Config 2 ตัว (`Deposit_Vat_Recognition` = CHECKOUT|RECEIPT, `Deposit_Defer_Output_Vat` = 0|1):
   - **CHECKOUT (default TakeTime):** มัดจำ Cr 21712 เต็มก้อน ไม่แยก VAT; ใบเสร็จมัดจำ **ไม่โชว์ VAT**;
     VAT รับรู้ตอนเช็คเอาท์ (ใบกำกับเต็มยอด ใบเดียว). สะอาดสุดฝั่งลูกค้า (ใบกำกับใบเดียว ไม่ซ้อน).
   - **RECEIPT + defer (Deposit_Defer_Output_Vat=1):** มัดจำ Cr **21913** (พัก); เช็คเอาท์ใบกำกับเต็มยอด
     + โอน Dr 21913/Cr 21911. VAT ไม่เข้า ภ.พ.30 จนเช็คเอาท์. Migration PHASE17_05 + admin toggle.
   - **RECEIPT + no-defer (เคร่ง §78/1):** มัดจำ Cr **21911 ทันที** → เข้า ภ.พ.30 เดือนรับมัดจำ.
     ใบเสร็จมัดจำ = เอกสารภาษี **ควรโชว์ VAT**. เช็คเอาท์ปัจจุบันออกใบกำกับเต็มยอด + VAT correction
     (Dr 21911/Cr 21712) → **GL ถูก** แต่ลูกค้าได้เอกสารภาษี VAT ซ้อน 2 ใบ; ให้ customer-correct
     เต็มที่ต้องออกใบกำกับ **เฉพาะยอดคงเหลือ** ตอนเช็คเอาท์ (ยังไม่ทำ — ต้องตัดสินใจ+test Windows).
   **แนะนำ hotel:** CHECKOUT/defer (ใบกำกับใบเดียวเต็มยอด ตอนเช็คเอาท์ — ตรงเวลา ภ.พ.30 กับวันที่ใบกำกับ,
   ไม่ซ้อน). §78/1 เคร่งใช้เมื่อผู้ทำบัญชียืนยันว่ามัดจำ = ค่าบริการล่วงหน้า (ไม่ใช่มัดจำประกัน).
   มัดจำ = ใบเสร็จรับเงิน ไม่ออก e-Tax ทุกโหมด; ลูกค้าไม่มีเลขภาษี → walk-in contact ปกติ (มัดจำไม่ติด §86/4).
3. **OCR-first ใบสำคัญจ่าย flow:** ✅ DONE (page `Voucher/OcrUpload.aspx`; gated on
   `CanUseCompanyEndpoints`, not key prefix). upload→`ocr/upload`(autoCreate=false)→poll
   `OcrResultResponse`→prefill review (shows OCR `SuggestedAccounts` + quality/role/WHT)→user
   confirm + **เลือกแหล่งจ่ายเงิน** (dropdown จาก `Account_Paid_How`)→`ocr/{id}/create-document?targetType=…`
   (Draft + auto-create Vendor by tax-id)→**PUT `/document/{id}` (`UpdateDocumentAsync`) บังคับ
   `PaymentAccountId`=แหล่งเงิน + วันที่/เลขที่เอกสาร + rebuild line เดียวจากยอดที่ผู้ใช้แก้ (เฉพาะกรณีไม่มี WHT
   — มี WHT คง line จาก OCR)**→`approve` (retries with AcknowledgeWarnings=true on 422). Update แก้
   ได้เฉพาะ Draft; null field = คงเดิม, `Lines!=null` = แทนที่. Client: `UploadOcrAsync` /
   `GetOcrResultAsync` / `CreateDocumentFromOcrAsync` / `UpdateDocumentAsync` /
   `ApproveDocumentAsync(id, ApproveDocumentRequest)`. (Future: line-level AI suggest + multi-line edit grid.)
   **ไฟล์แนบ OCR:** NextAcc create-from-OCR เก็บไฟล์เป็น **linked scan** (`EntityType="OcrScan"`, ชื่อ
   `ocr_...`) — คนละชนิดกับ Document attachment ที่ `GET /attachments/Document/{id}` อ่าน → คอลัมน์ไฟล์แนบ
   หน้า CheckPayment ขึ้น "-". **TakeTime fix:** หลัง create+approve ใน `OcrUpload.btnCreate_Click`
   อัปโหลดไฟล์ที่สแกน (เก็บไว้ใน `Session["OcrScanFile_<scanId>"]` ตอน scan) เป็น Document attachment
   ผ่าน `UploadAttachmentAsync("Document", docId, file)` **เฉพาะเมื่อ `GetAttachmentsAsync("Document",id)`
   ว่าง** (กันซ้ำ). **NextAcc-side fix (มิ.ย. 2026):** `GET /integration/documents` เพิ่ม `Attachments[]`
   (Id/FileName/ContentType/FileSize/DownloadUrl/CreatedAt) + on-read repair ที่ relink `OcrScan`→`Document`
   ทั้ง `/integration/documents` และ `/attachments?entityType=Document` → เอกสารเก่า auto-repair เมื่อ partner
   ดึงครั้งแรก. TakeTime อ่าน `OutboundDocumentResponse.Attachments` (fast-path ใน `FetchAndCacheNextAccAttachmentsAsync`
   โหลดผ่าน `DownloadUrl` กัน N+1) ถ้าว่าง/null → fallback `GetAttachmentsAsync` เดิม (มี repair). เมื่อ NextAcc
   deploy แล้ว guard ในหน้า OCR จะ skip การ upload ซ้ำเอง (GetAttachments trigger repair → เจอไฟล์).
3b. **หน้า PaymentVoucher (manual) parity กับ OCR:** ✅ DONE (page-side only, ไม่แตะ mapper/sync).
   เพิ่ม 3 dropdown: (a) `ddlPaidHowNexaacc` แหล่งจ่ายเงินดึงจากผังบัญชีจริง NextAcc (11x/21x, value=
   `Nexaacc_AccountId`) → save บังคับ `paymentAccountId` ตัวนี้ก่อน mapping (`LookupPaidHowAccountId`)
   แก้ปัญหา กสิกร→กรุงไทย; (b) `ddlLineChargeNexaacc` ผังค่าใช้จ่ายจาก NextAcc (5x/12x, value=
   `Account_Code`) ต่อรายการ — เก็บ code+GUID+ชื่อลง line (`AccountCode` column) ตอน Button2 เพิ่มรายการ
   → expenseLines ใช้ code นี้ก่อน mapping; (c) `ddlVatClaim` เคลม/ไม่เคลม — **ไม่เคลม = bundle VAT
   เข้ายอด line (กระจายตามสัดส่วน, ปัดเศษไปบรรทัดสุดท้าย) แล้วส่ง `hasInputVat=false`/`vatAmount=0`** →
   ได้ §82/5 ทุก endpoint (integration `IntegrationLineRequest` ไม่มี `IsVatClaimable` ต่างจาก company
   `DocumentLineRequest`). ทั้งหมด fallback ไป mapping เดิมถ้าไม่เลือก/ยังไม่ Sync ผังบัญชี. โหลด dropdown
   จาก `Accounting_Nexaacc_Accounts` ใน Page_Load !IsPostBack (ViewState คงค่า). Save สำเร็จ → server-side
   `Response.Redirect(?saved=1)` + alert ใน Page_Load (แทน ClientScript alert+window.location ที่เงียบถ้า JS error).
4. CheckPayment_New: now sorts by DisplayDoc (done). Homepage Google reviews: validates status (done).
5. **รับ-side AR closure (DOCUMENT mode):** ✅ DONE. NextAcc `/integration/invoices` posts
   Dr ลูกหนี้การค้า / Cr รายได้ / Cr ภาษีขาย and does **NOT** auto-record a payment (PaymentMethod
   is ignored; `BalanceDue=Total`). Previously every cash receipt left AR open + cash unbooked.
   `ProcessReceiptDocument` now calls `SettleReceiptInNextAcc` after the invoice: posts the
   deposit-applied adjustment (now **Cr ROOM_AR**, not Cr Cash) then records the real cash via
   `/integration/payments` (Dr Cash / Cr AR "ตัดลูกหนี้"), amount = total − depositApplied.
   **แหล่งเงิน (Account_Paid_How.Nexaacc_AccountId) is now FORCED** onto NextAcc: when a mapped
   account GUID exists AND an `acc_` key is configured, the cash leg routes to the company
   `document/payments` endpoint with `OverridePaymentAccountId` (verified: NextAcc
   `CreatePaymentJournalAsync` resolves the cash-side GL from `OverridePaymentAccountId` first →
   Dr that exact account). `int_` key can't force it → integration endpoint, NextAcc picks by
   PaymentMethod, logged. (Mirrors the จ่าย side `AutoRecordPaymentForVoucher`.)
   Idempotent via `Account_Receipt.Nexaacc_Receipt_Payment_Id` (migration PHASE17_06) — payment
   endpoint isn't deduped, so a two-phase marker (`ADJ:{jid}` → paymentId/`NOCASH`/`VOIDED`) guards
   queue retries. Void: primary `/documents/void` cascades the payment reversal; the credit-note
   fallback calls `VoidPaymentAsync`; `MapDepositAppliedAdjustmentReverse` now reverses Dr AR / Cr
   ADVANCE_DEPOSIT(+VAT). The จ่าย side already settled AP correctly (PV one-shot / expense+payment).
6c. **ลูกค้าไม่ประสงค์รับใบกำกับภาษี (NextAcc ก.ค. 2026):** `POST /integration/invoices` รองรับ
   `buyerDeclinedTaxInvoice:true` (หรือเว้น customerName/TaxId/ExternalId ว่างทั้ง 3) → ผูก contact
   กลาง "ลูกค้าเงินสด (ไม่ประสงค์รับใบกำกับภาษี)" (`Contact.IsWalkInCustomer` — ยกเว้น gate §86/4),
   VAT เข้า ภ.พ.30 ครบ. ขอใบเต็มรูปทีหลัง = void+ส่งใหม่ หรือ resyncUpdate (ยังไม่ชำระ/ยังไม่ยื่น).
   TakeTime: `HasFullBuyerTaxData` (เลขภาษี 13 หลัก + ที่อยู่) เป็นตัวเลือกเส้นทาง —
   ครบ → company TaxInvoice doc ใบเต็มรูป (forceRefresh contact ก่อนเสมอ) / ไม่ครบ → int_ invoice
   + `MarkBuyerDeclinedTaxInvoice` (ทั้ง payment, deposit-int_, repost). Error §86/4 ในคิวถูก decode
   \uXXXX เป็นไทย + แนบวิธีแก้ (`BuildApiErrorHint`).

6b. **ชนิดเอกสารฝั่งรับ (ก.ค. 2026):** ✅ มัดจำ → **Receipt doc (type 3)** ใบเสร็จรับเงิน เงินสดจบในใบ
   (ไม่ออก e-Tax — ใบกำกับออกตอนเช็คเอาท์เต็มยอด ใช้คู่ Deposit_Defer_Output_Vat); รับชำระ/เช็คเอาท์/
   ส่วนต่าง-อัพเกรด → **TaxInvoice doc (type 4)** ใบกำกับภาษี (Dr AR / Cr รายได้ราย line / Cr VAT —
   NextAcc นับ TaxInvoice เป็น AR ตาม BalanceDue) ผ่าน `EnsureRevenueDocCreatedApprovedAsync`
   (marker DOC:→APR:) แล้วปิดลูกหนี้ด้วย `SettleReceiptInNextAcc` (ADJ:→paymentId/NOCASH — parser
   มอง DOC:/APR: เป็นยังไม่เริ่ม settle) → e-Tax TAX_INVOICE ใช้ได้จริง. ปุ่ม "ดู PDF" หน้า
   CheckDocument_New เปิด PDF จาก NextAcc ก่อน (`DownloadReceiptPdfFromNextAccAsync` smart-cache ใต้
   `ReceiptFolderPath\NextAcc\` + `IsReceiptPdfCacheStale`) fallback local. ใบเก่าที่ออกผิดประเภท
   (Receipt) → กด Retry ในคิว = void→สร้างใหม่เป็น TaxInvoice อัตโนมัติ.

6. **รับ-side full accounting correctness (DOCUMENT mode, acc_ key):** ✅ DONE. Root cause: NextAcc's
   integration invoice JE credits a **single** `AccountType=Revenue` account for the whole SubTotal and
   ignores per-line `AccountId`, so (a) multi-line revenue was flattened and (b) deposits via `/invoices`
   were recognised as revenue immediately, not as เงินรับล่วงหน้า (liability). **Fix:** when an `acc_`
   key is configured, `ProcessReceiptDocument` now creates a **`Receipt` document (DocumentType=3) via the
   company `/document` endpoint** (`MapReceiptToDocument` → `CreateDocumentAsync` → `ApproveDocumentAsync`)
   instead of an integration invoice. NextAcc `AutoPostToJournalAsync` Receipt branch (verified) posts in
   ONE doc: **Dr Cash/Bank (`PaymentAccountId` = แหล่งเงิน) / Cr revenue PER LINE (`docLine.AccountId`) /
   Cr Output VAT** — no AR opened, no separate payment. Deposits set `IsDeposit=true` +
   `DepositDeferredAccountCode = GetAccountCode("ADVANCE_DEPOSIT")` (so checkout clearing's
   `Dr ADVANCE_DEPOSIT` nets exactly) + `DepositOutputVatDeferred` → Cr 21712 liability + Cr 21913
   deferred VAT. `PricesIncludeVat=true` (our amounts are gross). In-receipt deposit deduction →
   `MapDepositAppliedReceiptAdjustment` (Dr ADVANCE_DEPOSIT net + Dr 21913/21911 VAT / Cr Cash) reduces
   the doc's full Dr-cash to the actual cash received. Idempotent via `Nexaacc_Receipt_Payment_Id`
   3-phase marker (`DOC:{id}` → `APR:{id}` → `{id}`/`VOIDED`); company create-document isn't deduped.
   Void: company `/document/{id}/void` (cascades JE) + `MapDepositAppliedReceiptAdjustmentReverse`
   (Dr Cash / Cr ADVANCE_DEPOSIT(+VAT)). **Edit = void→recreate เลขเดิม**: void เก่า (doc id จาก queue
   history) → CREATE ใหม่; marker `"VOIDED"` ถูก **reset เป็น null** ทั้งใน `SettleReceiptDocAsync` และ
   `SettleReceiptInNextAcc` เพื่อไม่ให้บล็อกการสร้างใหม่ (delete ปกติไม่ enqueue CREATE จึงไม่สร้างซ้ำ).
   GL verified balanced across RECEIPT / CHECKOUT / deferred-VAT
   timing. **`int_` key keeps the integration-invoice + `SettleReceiptInNextAcc` fallback (item 5)** —
   correct GL totals but single revenue account + deposit-as-revenue caveat remains for `int_`.
   Needs Windows build + live-NextAcc testing (cannot build/test on Linux).
7. **Payroll (เงินเดือน) — TakeTime calc + NextAcc GL, variable amounts:** TWO modes via
   `Nexaacc_SyncMode_Payroll`. **JOURNAL_ONLY (now the DEFAULT — no longer inherits DOCUMENT):**
   TakeTime computes (gross/OT/bonus/deductions/SSO/WHT — fully variable per period) →
   `EnqueuePayrollJournal` per employee → `MapPayrollToJournal` posts the COMPLETE balanced GL to
   NextAcc with our exact numbers (Dr SALARY_EXPENSE + Dr SSF_EMPLOYER_EXPENSE / Cr SSF_PAYABLE
   (emp+er) / Cr WHT_PAYABLE_PND1 (ภ.ง.ด.1) / Cr cash net). TakeTime generates the payslip/voucher
   PDF locally. **DOCUMENT mode:** NextAcc's NATIVE payroll run (`ProcessPayrollRunSync`:
   sync employees→create run→**calculate (server-side)**→approve→pay) auto-issues payslip + ภ.ง.ด.1
   + สปส.1-10 + 50ทวิ + GL — but NextAcc RECALCULATES from employee master + its SSO/tax config and
   has **no per-employee per-run amount override** (verified: PayrollController has create/calculate/
   approve/pay/void only; `items` are templates), so it CANNOT honor TakeTime's variable per-period
   amounts. ⟹ for variable payroll use JOURNAL_ONLY (default); use DOCUMENT only for fixed salary
   when you want NextAcc's native statutory forms and accept NextAcc's calculation. Statutory forms
   (ภ.ง.ด.1/สปส) for the JOURNAL path must be filed from TakeTime data (NextAcc can't auto-gen them
   without the recalculating native run).
   **DOCUMENT_IMPORT mode (✅ NEW — variable amounts + NextAcc statutory forms):** NextAcc shipped
   `POST /api/companies/{id}/payroll/runs/import` (Option A, commit fc6ff90) — creates a run as
   **Calculated from the amounts we send (`Recalculate=false`)**, idempotent by `ExternalRunRef`,
   maps by `EmployeeExternalId`→`CitizenId`, validates `net == gross − หักฝั่งลูกจ้าง` (SSO emp + WHT +
   PVD emp + advance + other; **excludes employer SSO/PVD**, else GL won't balance at pay → 422),
   then approve→pay issues GL + ภ.ง.ด.1 + สปส.1-10 + 50ทวิ + payslip **from our numbers**. TakeTime side:
   `ImportPayrollRunAsync` + `PayrollImportRunRequest`/`PayrollImportLine`; queue action `IMPORT_PAYROLL_RUN`
   (`EnqueuePayrollRunImport`/`ProcessPayrollRunImport`) reads per-employee amounts from `Payroll_Records`,
   syncs employees, imports→approve→pay (status-gated, idempotent). `GenerateAllVouchersForPeriod` routes to
   import when `IsPayrollImportMode`; per-employee `EnqueuePayrollJournal` is skipped in DOCUMENT and
   DOCUMENT_IMPORT (only JOURNAL_ONLY posts per-employee). **Map caveats:** no PVD/SalaryAdvance fields in
   `Payroll_Records` (→0); `LeaveDeduction` folded into `OtherDeductions` so validation balances; employer
   SSO = employee SSO (5%/5%); `IncomeTypeCode="01"`; account-code overrides left null (NextAcc defaults).
   **Use:** set `Nexaacc_SyncMode_Payroll=DOCUMENT_IMPORT` in Admin → Accounting Integration. Needs Windows build.
8. **POS daily roll-up — ขายหน้าร้านไม่ออกใบกำกับ → ใบรับเงินสดสรุปรายวัน:** ✅ DONE (opt-in,
   `Nexaacc_PosDailyRollup`, default off). การขายใน `Product/Default.aspx` ที่ **ไม่ติ๊ก "ออกใบกำกับภาษีในระบบ"
   (CheckBox1)** เดิมเขียนแค่ `Product_Out` (Remark='ขาย', Account_Receipt_ID='0') ไม่ sync เลย → รายได้ไม่เข้า NextAcc.
   **Fix (ไม่แตะ flow ขาย):** `RollupPosDailySalesIfDue` (เรียกจาก background timer ใน `Global.asax` หลัง
   `ProcessQueueAsync` — auto ไม่ต้องกด) รวบแถว Product_Out ที่ยังไม่รวบของ **วันที่จบแล้ว (< วันนี้)** group ตาม
   วัน × แหล่งรับเงิน (`Account_Paid_How_ID`) → สร้าง `Account_Receipt` สรุป (ID=`POSDAY-{yyyyMMdd}-{paidHowId}`) +
   `EnqueueReceipt` (Dr เงินสด/Cr รายได้สินค้า/Cr ภาษีขาย — VAT คำนวณจาก `Business_Info.Use_Vat`, จด VAT=ถอด 7%) +
   `EnqueueStockOutCogs` ต่อสินค้า (Dr COGS/Cr Inventory, ต้นทุน `Product.Cost_Price`) → mark `Product_Out.Pos_Rollup_Ref`.
   **ออกใบกำกับในระบบ (CheckBox1 ติ๊ก) ยิงต่อใบเหมือนเดิม** (Product_Out มี Account_Receipt_ID จริง → ถูก exclude).
   Idempotent: marker + queue dedup (receiptNumber/stockRef). Migration PHASE18_01 เพิ่ม `Pos_Rollup_Ref`
   (+ backfill 'LEGACY' กันรวบย้อนหลังทั้งประวัติ) + seed flag. **ข้อจำกัด:** ตัด GL/COGS (journal-driven เหมือน
   STOCK_IN เดิม) แต่ **ไม่ขยับ qty StockMovement ฝั่ง NextAcc** (DocumentLineRequest ไม่มี product code) — qty
   sync ต้องรอ inventory spec (`docs/NextAcc_Inventory_Sync_Spec.md`). Needs Windows build + live test.
9. **Stock qty 2-way sync (TakeTime ↔ NextAcc):** ✅ DONE (opt-in, PHASE18_02). NextAcc มีระบบ inventory
   จริง (StockMovement + costing) — verified `POST {company}/product/stock/adjust` (qty-only, **ไม่โพสต์ GL**)
   + `GET {company}/product/{id}/stock/movements`. **ขาออก** (`Nexaacc_StockQtySync`): ทุก `EnqueueStock*`
   เพิ่ม `STOCK_QTY_PUSH` → `ProcessStockQtyPush` (resolve `Accounting_Product_Map` TT→Nexaacc GUID, ไม่ map→
   EnqueueProductSync+retry) → `AdjustStockAsync`; ไม่เบิล GL (journal เดิมคุมมูลค่า). **ขากลับ**
   (`Nexaacc_StockQtyPull`): `PullNextAccStockMovementsIfDue` (timer Global.asax, round-robin
   `Stock_Last_Pulled` cap 25) → movement ที่ไม่อยู่ใน `Nexaacc_Stock_Movement_Seen` (= ปรับฝั่ง NextAcc เอง)
   → ลง `Product_In`/`Product_Out` (Remark='NEXTACC_SYNC'). echo-safe: push ทุกตัวบันทึก movement id ใน Seen
   + inbound insert ตรงไม่ trigger ขาออก. Migration PHASE18_02 (Seen table + Stock_Last_Pulled + 2 flags) +
   admin toggles. ข้อจำกัด: pull เป็น per-product polling (รอ NextAcc เพิ่ม global `?since=` cursor),
   inbound Product_Out omit Account_Paid_How_ID. Needs Windows build + live test.

## Git / workflow
Feature branch: `claude/vibrant-davinci-nzwlgq` (based on default branch
`claude/restructure-system-architecture-szeM0`). Remote: `Ipsos-Dev-TH/TakeTime`
(NOT `Wachira-d/TakeTime` — that is the dev's fork). PR #1 open against the default branch.
