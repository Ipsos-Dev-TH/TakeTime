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
   - **หน้าตรวจทานก่อนส่ง + ใบลดหนี้ผู้ขาย (ต.ค. 2026, PHASE19_38):** สแกนเสร็จ → `OcrUpload.aspx?scan={id}` (ภาพซ้าย/ฟอร์มขวา,
     มือถือเรียงบน-ล่าง) แก้ได้ทุกช่อง: ชนิด PV/EXP/**VCN ใบลดหนี้**/**VDN ใบเพิ่มหนี้**, ผู้ขาย (เลข 13 หลัก mod-11, สาขา 5 หลัก, ที่อยู่),
     เลขที่/วันที่/วันที่ใบกำกับ, **บรรทัดหลายรายการ** (บัญชี NextAcc, VAT 7/0/ยกเว้น(-1), เคลม/ไม่เคลม, VAT ฿ override, WHT%) +
     **ตารางเทียบยอดกับกระดาษ** — ส่งไม่ได้จนก่อน VAT/VAT/รวม ตรง ±0.01 (`Class/Integration/OcrReview/OcrReviewMath.cs` ตัดสินฝั่ง
     server; JS ในหน้าเป็นสำเนาแสดงผลสด). `OcrReviewService.SubmitAsync`: PV/EXP/ใบเดิมนอกระบบ = `ocr/{id}/correct`(best-effort) →
     `create-document?allowDuplicate` → PUT ทั้งชุด (Lines+VatAmountOverride) · VCN/VDN อ้างเอกสาร NextAcc (PI/EXP/PV) = `POST /document`
     ตรง (RelatedDocumentId ตั้งได้แค่ตอน create; ContactId = ของใบต้นทาง; `CnDnPurchaseSideOverride=true`; เพดาน §86/10 =
     ยอดใบ − ΣCN และ ≤ BalanceDue เมื่อค้าง — DocumentService.cs:11919-11951) → `link-document` → เติมที่อยู่/สาขา contact ที่ว่าง →
     approve (**422 แสดงคำเตือนให้ผู้ใช้กด "ยืนยันแม้มีคำเตือน" เอง**) → แนบไฟล์ → 50 ทวิ (PV) → CN/DN ใบต้นทางจ่ายครบ: NextAcc ลง
     เงินสด 111 เสมอ (:17899-17924) ⇒ ผู้ใช้เลือกบัญชีรับคืน/เก็บเป็นยอดกับผู้ขาย → JE ย้ายขา `OCRCN|OCRDN-{scan8}-MNY`.
     กันซ้ำ: เลขผู้ขาย+เลขที่ใน `Ocr_Document_Log` + `SupplierInvoiceNumber` ของ contact เดียวกันบน NextAcc. Draft/สถานะ
     (DRAFT→CREATED→UPDATED→APPROVED→DONE, FAILED, EVIDENCE) + OCR raw vs ค่าที่ยืนยัน (Corrections_Json) ใน `Ocr_Document_Log`
     (ไม่มีตาราง = Session); `IdempotencyContext=OCR-{scanId}` ⇒ กดส่งซ้ำ = ทำต่อจากขั้นที่ค้าง. ใบลดหนี้ของการคืน `PRT-` = ดูข้อย่อยถัดไป
     (ปิดนโยบาย/ยังไม่รัน PHASE19_39 = เก็บเป็นหลักฐาน EVIDENCE แบบเดิม). PDF: `?pdf={docId}`
     (เฉพาะเอกสารจากหน้านี้). ไม่ทำ: ใบรับรองแทนใบเสร็จจาก OCR (ใช้ PaymentVoucher ข้อ 11), ปรับสต๊อกจาก CN.
   - **ใบลดหนี้ผู้ขาย ↔ คืนสินค้า / เครดิตผู้ขาย (ต.ค. 2026, PHASE19_39 — คำตัดสินทีม 4 ข้อ; ไม่มีตาราง/`Vendor_Cn_Primary=0` = เดิม):**
     (1) **ใบลดหนี้ใน NextAcc = รายการบัญชี/ภาษีตัวจริง** เพราะ ภ.พ.30 อ่าน "เอกสาร" ใบลดหนี้ ไม่อ่าน JE (TaxService.cs:675-690) และ §82/10 ลด
     ภาษีซื้อเดือนที่ได้ใบ. หน้า `Product/PurchaseReturn` (วางบิลแล้ว = คู่บัญชีเจ้าหนี้/เงินคืน) เลือก: **รอใบลดหนี้** (default — JE PRT ไม่กลับ VAT;
     คืนเงินสด = VAT ที่ได้คืนพักเจ้าหนี้ `vatHoldKey`) · **ผูกใบที่บันทึกแล้ว** (CN-first: ไม่มี JE PRT, ตัดจำนวน+DIF) · **ผู้ขายไม่ออก** (JE เดิมครบ).
     ยังไม่วางบิล (GRNI) = ไม่ต้องรอใบ. OCR ใบลดหนี้เลือก "การคืนสินค้า" (รายการรอ) → หลังอนุมัติ `MarkLinked` (ตัวสร้าง PRT เห็นแล้ว `SKIPPED_CN_LINKED`)
     + คิว `PURCHASE_RETURN_REVERSE` (`PRT-…-REV` — กลับ JE ที่ลงแล้ว; PROCESSING = รอ, ยังไม่ลง = ไม่มีอะไรกลับ) + `PURCHASE_RETURN_COSTDIFF`
     (`PRT-…-DIF` = Σบรรทัดใบลดหนี้ที่ลงบัญชี INVENTORY − ต้นทุนเฉลี่ยที่ตัด ↔ PURCHASE_RETURNS — CostService คงค่าเฉลี่ย; บรรทัดลงบัญชีสินค้าชนะ
     DocumentService.cs:17383). CN ปรับราคา (ไม่ผูกคืน) → แนะนำบัญชีส่งคืน/ส่วนลด (ค่าเฉลี่ยไม่เปลี่ยน). คืนบางส่วน = 1 PRT ↔ 1 CN. qty push ข้อ 9 ไม่เปลี่ยน.
     เตือน: Telegram วันละครั้ง (`RemindPendingVendorCnIfDue`, `Vendor_Cn_Reminder_Days` 7) + ตรวจสุขภาพข้อ 12 + ปุ่ม "ปิดรายการรอใบลดหนี้".
     ตาราง `Purchase_Return_Cn_Link` (สถานะ PENDING/LINKED/NOT_REQUIRED, Prt_Journal FULL/PENDING/NONE). การคืนก่อน PHASE19_39 = หลักฐาน.
     (2) **ใบเพิ่มหนี้ของใบที่จ่ายครบ:** NextAcc ลง Cr 111 (DocumentService.cs:17899 `isCashSettlement`, :17922) ⇒ default **ตั้งเจ้าหนี้รอจ่าย**
     (JE `OCRDN-…-MNY` Dr 111/Cr เจ้าหนี้ + `Vendor_Credit_Ledger` DN_PAYABLE → หน้า `Account/CreditPurchasePayment` ปุ่ม "จ่าย" = `VDNPAY-…` Dr เจ้าหนี้/Cr แหล่งเงิน)
     หรือ "จ่ายทันทีจากบัญชี…". ขาเงินจริง = `VendorCnPolicy.EffectiveSettlement` (ใบต้นทางค้าง/นอกระบบ = ไม่มีขาเงิน; ไม่ลงเงินสดที่ไม่มีจริง).
     (3) **เก็บเป็นเครดิตกับผู้ขาย (CN ใบต้นทางจ่ายครบ, default):** mapping ใหม่ `VENDOR_CREDIT_RECEIVABLE` (ยังไม่ผูก = เจ้าหนี้การค้ายอดเดบิต + ตรวจสุขภาพเตือน)
     + ledger CN_CREDIT. หักตอนจ่ายใบซื้อเชื่อ: รับชำระเอกสารเต็ม (เงินจริง+เครดิต) + `VCRED-{doc}` Dr แหล่งเงิน/Cr บัญชีที่เครดิตนั่ง (ledger CN_APPLIED).
     ผู้ขายคืนเงิน = BANK (การคืนแบบคืนเงินตั้ง default บัญชีเดียวกับ PRT). (4) **เติมที่อยู่/สาขา/เลขผู้เสียภาษีของผู้ติดต่อเฉพาะช่องว่าง** (ไม่ทับ) — สวิตช์
     `Ocr_Fill_Missing_Contact` (1), log audit ค่าเดิม→ใหม่, กล่องสำเร็จแสดง "เติมข้อมูลผู้ขายใน NextAcc: …". โค้ด `Class/Purchasing/VendorCnPolicy.cs` (pure, mono test),
     `VendorCnService.cs`, `Class/Integration/AccountingSyncService.VendorCn.cs`. ข้อจำกัด: CN-first ช่วงก่อนบันทึกคืน GL สต๊อกลดก่อนจำนวน; ledger จับคู่ผู้ขายด้วยเลขภาษี/Vendor.ID/ชื่อ.
   - **ราคารวม VAT / เหตุผล / ใบเดิม / ขาเงิน (ต.ค. 2026 — เคส Makro CN 005911030177; ไม่มี migration):** (1) `OcrReviewState.PricesIncludeVat` + ตรวจอัตโนมัติ
     `OcrReviewMath.DetectPricesIncludeVat` (คิดสองแบบเทียบยอดกระดาษ; VAT 0/ยกเว้นรายบรรทัดได้) — รวม VAT ⇒ VAT ต่ออัตรา = Σยอด − round(Σยอด×100/107)
     (ปัดทั้งใบ: 3,447 → 3,221.50/225.50) ส่ง `PricesIncludeVat=true` + `VatAmountOverride` ทุกบรรทัด (NextAcc ฐาน = ยอด − VAT, ไม่กระทบเศษซ้ำ
     DocumentService.cs:653-659, :812-814; ไม่ส่ง override NextAcc ได้ 225.51). `CalcLines` กระทบเศษต่ออัตราแบบ NextAcc (:802-901); JS `OcrMath` = สำเนา (parity test).
     ปุ่มแก้ด่วน `SuggestFixes/ApplyFix` (SWITCH_BASIS/FIT_VAT/FILL_MISSING/ADD_GAP) + "แก้อย่างไร" ใต้แต่ละข้อ; ข้อผิดพลาดอยู่บนตารางยอด, แถบล่างลิงก์ไปข้อที่ต้องแก้ + เว้นที่ (มือถือ).
     (2) เหตุผล `OcrNoteReasons` (RETURN/DEFECT→Return1, SHORT ส่งไม่ครบ/PRICE_ERR/OTHER→Adjustment3, DISCOUNT→2; DN 4 แบบ) — NextAcc พิมพ์ได้แค่ข้อความ enum
     (PdfGenerationService.cs:3198-3205) ⇒ ข้อความเต็ม + อ้างใบเดิม + มูลค่าเดิม/ถูกต้อง/ผลต่าง ไปที่ Notes; ผูก PRT- ได้เฉพาะเหตุผลที่มีของส่งคืน; SHORT = ไม่แตะสต๊อก
     (ถ้ารับเข้า Product/In เต็มจำนวนแล้ว แก้จำนวนรับเข้า; บรรทัดใช้บัญชีใบเดิม). (3) §86/10 จาก `RawTextContent` (`OcrCnPaperReader`): เลข/วันที่ใบกำกับเดิม, มูลค่า 3 ช่อง,
     เหตุผล, วิธีชำระ; ใบเดิมนอกระบบต้องมีเลขใบเดิม (= `Reference` ที่ NextAcc พิมพ์ :1907-1916; ผูกใบต้นทางแล้ว NextAcc พิมพ์เอง :1864-1901). (4) ใบต้นทาง:
     ค้นด้วยเลขใบกำกับเดิม (search ครอบ Reference :2427-2430 + SupplierInvoiceNumber ของผู้ติดต่อ) ตรงใบเดียว = เลือกให้ตอนเปิดหน้า; เลือกแล้ว `LoadSourceDetailAsync`
     ดึงบรรทัด+บัญชี (`AssignAccountsFromSource`: รหัสสินค้า → รายละเอียด → ราคา → บัญชีหลัก; ไม่ทับบรรทัดที่ผู้ใช้แก้) + บัญชีที่ใบเดิมจ่าย (PV `PaymentAccountId` /
     ขาเครดิตเงินของ JE `sourceDocumentId` :20046 ที่อยู่ใน Account_Paid_How) ⇒ `DefaultSettlement`: ค้างจ่าย = ลดเจ้าหนี้ · จ่ายแล้ว = รับคืนเข้าบัญชีเดิม (BANK →
     JE `OCRCN-…-MNY`) · ไม่รู้บัญชี = เครดิตผู้ขาย; dropdown เดียว (แหล่งเงิน / เครดิตผู้ขาย) ผู้ใช้เปลี่ยนได้ (`SettlementUserSet`).
   - **รวม/ไม่รวม VAT อัตโนมัติ + ขาเงินตรงบัญชี (ต.ค. 2026 — CN-20261021-0001 เลือก 21230 แต่ NextAcc ลง Dr 11111):** (1) `OcrReviewMath.AutoPriceBasis`
     (JS `OcrMath.autoBasis`, parity) เรียกทุก render + ก่อนส่ง: `PriceBasisSource != USER` ⇒ ตาม `DetectPricesIncludeVat` (สลับ + ล้าง override + FIT_VAT) — เดิมตรวจครั้งเดียว
     ตอนสแกนแล้วแค่ขึ้นคำใบ้ · OCR ติดธงรวม VAT บางบรรทัด = ลองราคาตามกระดาษทั้งใบก่อน (ตัดสินได้ = ไม่ถอด 7%). (2) NextAcc HEAD (V-CN-03, `DocumentService.cs:18225-18229`)
     ลงขาเงิน CN/DN ด้วย `UseMoneyAccount()` = `PaymentAccountId` ของเอกสาร (ผังที่ลงรายการได้ทุกชนิด — `DocumentMoneyChannel.Decide`); CN = ขาเงินเมื่อใบต้นทาง BalanceDue≈0,
     DN = เฉพาะ `PaymentType=Cash` (`AdjustmentNoteAccount.CounterLegIsMoney:197-200`) ⇒ `BuildUpdate` ตั้ง `PaymentAccountId`=`RefundAccountId` เมื่อ Settlement=BANK (+ DN `PaymentType=1`)
     ส่งทั้ง create ตรงและ PUT ร่าง · `TryReclassNoteMoneyLegAsync` เห็นบรรทัดบัญชีปลายทางใน JE แล้ว = ไม่โพสต์ MNY (+ ledger ผู้ขายเมื่อ VENDOR) · NextAcc รุ่นเก่ายังลง 111 ⇒ MNY ย้ายเหมือนเดิม.
     ใบที่ลงไปแล้ว (Dr 11111): ดู JE `OCRCN-{scan8}-MNY` ใน NextAcc — ไม่มี = ลง JE เอง Dr 21230 / Cr 11111.
   - **ใบลด/เพิ่มหนี้ผู้ขายในหน้าตรวจสอบใบสำคัญจ่าย + ทิ้งงาน (ต.ค. 2026):** `DownloadVoucherDocumentsForRangeAsync` ดึงชนิด CreditNote/DebitNote เพิ่ม
     เฉพาะเมื่อมี id ใน `Ocr_Document_Log` (VCN/VDN ที่สร้างเอกสารแล้ว — `LoadVendorNoteDocIds`; รายการ NextAcc ไม่บอกฝั่งซื้อ/ขาย ⇒ ใบลดหนี้ลูกค้าไม่ปน) →
     `NextAccCachedDocument.IsVendorNote`, ป้าย "ใบลดหนี้ (ผู้ขาย)"/"ใบเพิ่มหนี้ (ผู้ขาย)", ใบลดหนี้ยอด/VAT ติดลบ (ผลรวมหน้า = สุทธิ), ไม่ใช้จับคู่ Reference
     (`CheckPayment_New.MergeNextAccIntoGrid`). ใบลด/เพิ่มหนี้ที่สร้างบน NextAcc เองไม่ขึ้น. **ทิ้งงาน:** `OcrReviewService.DiscardAsync` — ไม่มีเอกสาร = DISCARDED ·
     ร่างบน NextAcc (status 0) = `DeleteDraftDocumentAsync` (`DELETE document/{id}` DocumentController.cs:1018-1029, เฉพาะ Draft) · 404/ยกเลิกแล้ว = ทิ้งได้ ·
     รออนุมัติ/ตีกลับ/อนุมัติ/APPROVED-DONE = ไม่ทิ้ง (บอกให้ยกเลิกใน NextAcc). ปุ่ม "🗑 ทิ้ง" ในรายการงานที่ยังไม่ส่ง (`btnDiscardRow` + confirm) และปุ่มเดิมในหน้าตรวจทาน
     (แสดงจนกว่าจะอนุมัติ).
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
   - **ออกแบบใหม่ (ต.ค. 2026, PHASE19_40):** 6 การ์ด ผู้รับเงิน→รายการ→การชำระ→ภาษี→เอกสารแนบ→สรุป (mobile-first, `Content/pv-voucher.css`
     + `Scripts/pv-voucher.js`, แถบบันทึกติดล่าง, ข้อความในหน้าแทน alert ทั้งหมด). ตัวเลือกเหลือ 2: **"จ่ายจาก"** `ddlPayFrom`
     (`PH:{Paid_How.ID}` แสดงบัญชี NextAcc ที่ผูก / `NA:{guid}` บัญชี 11x/21x ที่ไม่มีแหล่งเงินผูก → ถาม "บันทึกในระบบเป็น" DropDownList2) และ
     **"หมวดค่าใช้จ่าย"** `ddlLineAccount` (`PT:{Paid_Type.ID}` / `NA:{code}` ที่ไม่มีหมวดผูก) — `ApplyPayFromToLegacy`/`ApplyLineAccountToLegacy`
     ตั้งค่า control เดิม (ซ่อน) ก่อนโค้ดเดิมทำงาน; หมวดหลักของใบ (DropDownList3) เลือกให้จากรายการแรก (`DeriveHeaderPaidTypeId`).
     payload คิวย้ายไป `Class/Integration/PaymentVoucherForm.BuildVoucherPayload` (+`BuildLineRow`) — mono test เทียบโค้ดเดิม verbatim 10 เคส×2 culture
     ผลเหมือนกันทุกตัว. ตรวจช่องบังคับ/ยอดทั้งหมด **ก่อน** void+ลบใบเดิมในโหมดแก้ไข (เดิมตรวจหลังลบ = ใบหาย). **ใบซ้ำ:** ช่อง "เลขที่ใบกำกับ/ใบเสร็จ
     ของผู้ขาย" (`Account_Payment.Supplier_Invoice_No[_Norm]`, ไม่ส่งเข้าคิว) เทียบ Account_Payment + `Ocr_Document_Log` + NextAcc
     `document/duplicate-check` (DocumentController.cs:907, เฉพาะ strong) → เตือนพร้อมลิงก์ กดบันทึกซ้ำ = ยืนยัน (จ่ายงวด). **AI** (`API/PvAssist.ashx`
     → `PaymentVoucherAssist`, timeout 4–7 วิ, nonce ใน session, ไม่มี company endpoint/`Nexaacc_Pv_Ai_Assist=0`/NextAcc ล่ม = ไม่แสดง): ชิป
     `ai/payment-voucher/suggest-account` (AiSuggestionController.cs:204), `vat/infer-type` (:501), `wht/infer-category` (:447 → อัตราตาม
     ThaiWhtRateTable), `payment-voucher/suggest-type` (:123, ต้องมี contact ใน Accounting_Contact_Map), `contact/fuzzy-match` (:938) พร้อม %
     + เหตุผล กด "ใช้" เอง → `ai-feedback/record` (AiFeedbackController.cs:46, Source=Explicit; ✕/เปลี่ยนเอง = rejected + ค่าที่เลือก).
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

10. **หัวกระดาษเอกสารขาย (`Nexaacc_Receipt_Header_Type` AUTO|ABBREVIATED, PHASE19_18):** NextAcc คำนวณหัวตอนพิมพ์
   (`PdfGenerationService.ComputeDocumentTitle`) — "ใบเสร็จรับเงิน/ใบกำกับภาษีอย่างย่อ" เฉพาะเมื่อผ่าน `AbbreviatedTaxInvoiceRule`
   ไม่งั้น TaxInvoice ของผู้ซื้อ declined/ไม่ครบ = "ใบเสร็จรับเงิน". **NextAcc HEAD `09e9ea6`:** `Judge(..., channel)`
   (`Helpers/AbbreviatedTaxInvoiceRule.cs:90-115`) = จด VAT → `Company.IsRetailApproved` (ทุกช่องทาง) → **เอกสาร/A4 (`channel=Document`)
   ไม่ต้อง ภ.พ.06**; ภ.พ.06 (`PhoR06ApprovedDate`) บังคับ **เฉพาะสลิป POS/เครื่องเก็บเงิน** (สวิตช์แพลตฟอร์ม
   `RequirePhoR06ForAbbreviatedTaxInvoice` คุมแค่ด่าน ภ.พ.06). ⇒ หัว "อย่างย่อ" บนเอกสาร TakeTime ต้องการแค่ จด VAT + ติ๊ก
   `IsRetailApproved` ในข้อมูลบริษัท NextAcc. TakeTime บังคับหัวไม่ได้ — หลัง sync log `หัวเอกสาร NextAcc:` (อ่าน documentTitle กลับ).
   `docs/NextAcc_Change_Requests.md` ข้อ ก. (แก้แล้วฝั่ง NextAcc) + ข้อ ข. ผู้รับเงินใบรับรอง, ข้อ ง. ค่าธรรมเนียมเกตเวย์ตอน payout,
   ข้อ ฉ.–ฎ. (COGS ใบขายสด, CN อ้าง Receipt, CN integration, ตัดจำหน่ายมีเหตุผล+GL, deemed sale, รายงาน ม.87(3))
11. **ใบรับรองแทนใบเสร็จรับเงิน (type 15, PHASE19_19):** หน้า `Account/PaymentVoucher.aspx` ติ๊ก "ผู้รับเงินออกใบเสร็จไม่ได้"
   → payload `CREATE_VOUCHER_JOURNAL` มี certificateInLieu → `ProcessCertificateInLieu` (company `/document` type 15 +
   approve: Dr ค่าใช้จ่าย / Cr แหล่งเงิน (+Cr WHT), Paid ทันที). **ห้ามใช้** `/integration/certificates-in-lieu`
   (ตั้งเจ้าหนี้ 21220 ค้าง). ข้อมูลเก็บใน `Account_Payment.Is_Certificate_In_Lieu`/`Cil_*`; `EnqueuePaymentVoucher`
   อ่านจาก DB เองเมื่อผู้เรียกไม่ส่ง (กันปุ่มซิงค์ใหม่ส่งเป็น PV ซ้ำ).
   **ผู้รับเงินจริง:** NextAcc type 15 ไม่มีฟิลด์ผู้รับเงิน (`DocumentDtos.cs:50-56` มีแค่ Certificate*/Certifier*/Witness*/PaymentDate)
   → `MapVoucherToCertificateInLieu` ใส่ชื่อ/ที่อยู่ใน **`CustomAppendix`** (`DocumentDtos.cs:45`, พิมพ์ใต้กรอบใบรับรอง
   `PdfGenerationService.DocumentRenderer.cs:1034-1036`) + Notes. ขอฟิลด์จริงแล้ว: `docs/NextAcc_Change_Requests.md` ข้อ ข.
12. **เอกสารรับเงิน OTA (OTADOC-{resId}) — void/resync/reclass (ต.ค. 2026):** เอกสารสร้างโดยคิว `CREATE_OTA_SALES_DOCUMENT`
   (Entity RESERVATION) ⇒ lookup แบบใบเสร็จปกติหาไม่เจอ. `LookupOtaSalesDocId` (คิว CREATE_OTA ที่ COMPLETED ล่าสุด ไม่มี VOID
   สำเร็จตามหลัง → marker → `Nexaacc_Doc_Id`). `EnqueueVoidReceipt("OTADOC-…")` → `EnqueueVoidOtaSalesDocument` (VOID_RECEIPT +
   `otaDoc=true`, ปฏิเสธถ้าคิวสร้างยังค้าง) → `ProcessVoidOtaSalesDocument`: company `POST /document/{id}/void`
   (NextAcc `DocumentController.cs:803-815`; ยื่นภาษีแล้ว = ปฏิเสธ `DocumentService.cs:7805-7811`) แล้ว **GET ยืนยัน Voided** ก่อน
   ปลด marker: `allowRepost=true` → ลบแถวสรุป + `Ota_Revenue_Ref=NULL` (job สร้างใหม่; `EnqueueOtaSalesDocument` ข้าม dedup เมื่อมี
   VOID สำเร็จหลัง CREATE เดิม) / `false` → `Ota_Revenue_Ref='VOIDED-OTADOC-{id}'` + แถว Status='Cancel'. ปุ่มแอดมิน:
   Accounting Integration → "ยกเลิกเอกสาร OTA" (`action=voidOtaDoc`). Resync/Repost/ResyncInternal **ข้าม OTADOC** (เดิมเข้าคิวสร้าง
   ใบเสร็จใหม่ = รายได้ซ้ำ). **OTA_CASH_RECLASS** ปฏิเสธเมื่อ `Ota_Revenue_Ref` ขึ้นต้น `OTADOC-`/`OTA-` (`IsOtaRevenuePostedRef`) —
   ทั้งหน้า PaymentHistory (hint + skip), `EnqueueOtaCashReclass` (คืน -2) และ processor (ArgumentException ก่อนโพสต์).
   **VAT ใบ Receipt type 3:** ตรวจแล้วเข้า ภ.พ.30 (standalone receipt `TaxService.cs:571-591`, มัดจำ `:592-640`) ⇒ RECEIPT_DOC
   ไม่ต้องเปลี่ยนเป็น TaxInvoice.
13. **แหล่งเงินรายช่องทาง (หน้า Accounting Integration):** "ที่ใช้จริง" = `ResolveEffectiveGatewayPaidHow` ซึ่ง **เรียก**
   `PaymentChannelCatalog.ResolveRecordingPaidHowName` (ตัวเดียวกับ `OnlinePaymentService.ApplyToReservation` / `Payment/Charge`:
   แคตตาล็อก → `Nexaacc_Gateway_PaidHow_{P}[_{M}]` → `Payment_PaidHow_Name`; แถวแคตตาล็อกยังไม่ผูกบัญชีแต่ตั้งรายเกตเวย์ → รายเกตเวย์)
   แล้วเติมแค่ป้าย source (CATALOG/GATEWAY_OVERRIDE/GATEWAY_CONFIG/DEFAULT) — แก้กฎที่ catalog ที่เดียว. ตารางรายช่องทาง + health check.
14. **เงินประกันความเสียหายแบบโอน → บัญชี (opt-in `Nexaacc_SecurityDeposit_Journal`, PHASE19_24, default 0):** hook ใน
   `SecurityHoldService` (TRANSFER เท่านั้น) → คิว `SECURITY_DEPOSIT_JOURNAL`: รับโอน Dr ธนาคาร (Paid_How ของช่องทางรับโอน) /
   Cr `SECURITY_DEPOSIT_LIABILITY` (ผังโรงแรม NextAcc 21530) · โอนคืน กลับขา. ref `SECDEP-{holdId}-IN/OUT` (กันซ้ำทั้งคิว+JE);
   OUT ต้องมี IN (รับก่อนเปิดสวิตช์ = ข้าม) และใช้บัญชีธนาคารของ IN.
   **หักค่าเสียหาย (ต.ค. 2026 — PHASE19_25): ไม่มี JE `DMG` แล้ว** (เดิม Dr หนี้สิน / Cr ธนาคาร ทันที แล้วพึ่งใบเสร็จค่าเสียหายที่เลือก
   บัญชีรับโอน → ลืมออกใบ/เลือกเงินสด = ธนาคารขาด + ไม่มีรายได้). หนี้สินส่วนที่หักล้างด้วย **ใบเสร็จค่าเสียหายเอง** ที่เลือกแหล่งเงิน
   **"หักจากเงินประกัน (Security deposit)"** (`Account_Paid_How`, `Channel_Code=SECURITY_DEPOSIT_OFFSET`, OTHER, ลูกค้าไม่เห็น) —
   **แอดมินต้องผูกบัญชี NextAcc ของแถวนี้ = บัญชีเดียวกับ `SECURITY_DEPOSIT_LIABILITY`** ⇒ Dr หนี้สิน / Cr รายได้ค่าเสียหาย + VAT.
   ไม่มี flow สร้างใบเสร็จค่าเสียหายในโค้ด (พนักงานออกเอง) จึง default แหล่งเงินให้อัตโนมัติไม่ได้ → `CaptureDamage` บอกชื่อแหล่งเงิน
   ที่ต้องใช้ในข้อความผลลัพธ์/บันทึก txn/Telegram + log เตือน (`DamageReceiptPaidHowText`: มี IN แล้ว = แถวหักจากเงินประกัน,
   ไม่มี IN = บัญชีรับโอนแบบเดิม) + health check ข้อ 4c ตรวจว่าแถวมีและผูกบัญชีเดียวกับหนี้สิน. `EnqueueSecurityDepositJournal("DMG")`
   ถูกปฏิเสธ; คิว DMG เก่าที่ค้างยังประมวลผลได้ (คู่กับใบเสร็จแบบเดิม).
15. **มัดจำ vs รายได้ (`PaymentService.ProcessReservationPayment`, ต.ค. 2026):** รับก่อนเข้าพักแต่ไม่ครบยอด = **มัดจำเสมอ**
   (`isDeposit = !coversAll && !arrived` — ตรงกับหน้าแก้ไขการจอง `IsDeposit = Deposit < totalPrice` และข้อ 6b); ค่าใช้จ่ายในห้องที่ยอด
   ครอบคลุมยังถูกปิด (Receipt_ID = ใบมัดจำ) เหมือน Reserve `MarkChargesPaidUpTo`. รายได้ต่อบรรทัดมาจาก ProductType_ID ของบรรทัด
   (ค่าสัตว์เลี้ยง type 3 ไม่ถูกตีเป็น ROOM_REVENUE — `revenueType` เป็น fallback เฉพาะบรรทัดไม่มี type).
16. **สลิปที่ลูกค้าแนบเอง (`/Payment/Pay` สแกน QR):** ไม่ลงรับเงินทันทีแล้ว — `SubmitReservationSlipForVerification` เก็บ
   `Payment_Slips` (PENDING) + `Claimed_Amount` (PHASE19_25) ไม่สร้าง Payment_History/ใบเสร็จ/บัญชี และไม่ `PromoteIfPending`; ตัวกวาด
   `CancelStaleUnpaidIfDue` ข้ามใบที่มีสลิปรอตรวจ. เจ้าหน้าที่อนุมัติที่ `Account/SlipVerification` → `ApproveReservationSlip` (ชิงสถานะ
   PENDING→APPROVED แบบ atomic → `ProcessReservationPayment(existingSlipId)` → `PromoteIfPending`; ลงเงินไม่ผ่าน = คืน PENDING).
   ยังไม่รัน migration = ลงรับเงินแบบเดิมแต่ไม่ยืนยันการจองจากสลิปลูกค้า. พนักงานใช้หน้า Pay = เหมือนเดิม.
17. **ใบลดหนี้ + คืนเงินลูกค้า (ต.ค. 2026, PHASE19_33, opt-in `CreditNote_Enabled`):** `Credit_Note`/`Credit_Note_Line` (เลข `CN{yyMM}-####`,
   1 แถว = 1 ใบต้นทาง Account_Receipt) · `Class/Accounting/CreditNoteService.cs` (สร้าง/อนุมัติ/ยกเลิก/คืนภายหลัง/ยกเลิกการจอง) ·
   `Class/Integration/AccountingSyncService.CreditNote.cs` (คิว `CREATE_CREDIT_NOTE`/`RECORD_CN_REFUND`/`VOID_CREDIT_NOTE`, Entity `CREDIT_NOTE`) ·
   หน้า `Account/CreditNotes.aspx` + ลิงก์ "คืนเงินแบบละเอียด" ใน modal ยกเลิกคืนเงิน (ReserveTable → `?cancelRes=`). NextAcc (HEAD `8c9cf5b`):
   - **ใบต้นทาง = มัดจำ** → `POST /document/{id}/refund-deposit` {Amount gross, MoneyAccountId=แหล่งเงินคืน} (DocumentController.cs:533-545;
     JE Dr หนี้สินมัดจำ (+Dr 21913/21911) / Cr บัญชีที่เลือก + ใบลดหนี้อัตโนมัติเฉพาะ VAT มัดจำเข้า ภ.พ.30 แล้ว DocumentService.cs:5429-5662;
     เลขใบผูกจาก `ConvertedToDocuments`) · ส่วนที่ยึด → `POST /document/{id}/realize-deposit` {Amount ฐาน, ForfeitAs PriceOrFee=1|Compensation=2}
     (DocumentController.cs:590-602 — มัดจำไม่เคยเสีย VAT ⇒ NextAcc ออกใบกำกับของยอดที่ยึดเอง DocumentService.cs:4866-4950). กันซ้ำด้วย marker
     `REFPRE:{คืนแล้วก่อนยิง}`→`REF:{id}`→`|FFTPRE:`→`|FFT`. ยกเลิกหลังส่ง = บล็อก (ไม่มี API — Change Requests ข้อ ฐ.).
   - **ใบต้นทาง = ใบกำกับ/ใบเสร็จ** → company `POST /document` DocumentType=6 + `RelatedDocumentId` + `CreditNoteReason` (Return1/Discount2/Adjustment3,
     DocumentDtos.cs:23,77) บัญชีรายได้ต่อบรรทัดจากบรรทัดของเอกสารต้นทาง · ไม่ส่ง ProductCode (สต๊อก/COGS รับคืน TakeTime ลงเอง
     `Product_In` InType `CN_RESTOCK` + `EnqueueStockOutCogsReversal`) → approve → NextAcc ลง **Cr 111x** เมื่อใบเดิมชำระครบ / Cr ลูกหนี้
     (DocumentService.cs:17899-17925 — เลือกบัญชีไม่ได้, Change Requests ข้อ ฏ.) ⇒ JE `CN-{id}-MNY` Dr บัญชีนั้น (อ่านจาก
     `GET /accounting/journals?sourceDocumentId=`) / Cr แหล่งเงินคืน (ยังไม่คืน = Cr ROOM_AR เครดิตลูกค้า → คืนภายหลัง `CN-{id}-RFD`). marker `DOC:`→`APR:`→`MNY`→`DONE`.
     ไม่พบเอกสารต้นทาง/ไม่มี company endpoint → `/integration/credit-notes` (Cr ลูกหนี้) + JE Dr ROOM_AR / Cr แหล่งเงิน.
   - **ยกเลิกการจองแบบละเอียด** (`CancelReservation`): แบ่งยอดคืน มัดจำคงเหลือก่อน (ส่วนไม่คืน = ยึด) แล้วใบกำกับ (ส่วนไม่คืน = รายได้คงเดิม ไม่ออกใบลดหนี้ส่วนที่ยึด)
     → ตั้งสถานะ/ล้างห้อง/ชาร์จค้างเหมือนปุ่มเดิม แต่ **ไม่** ยิง `EnqueueVoidReceipt`/`EnqueueDepositRefund`/`EnqueueDepositForfeit` (กันซ้ำ); ใบมัดจำ → `Refunded`/`Forfeit`.
   - อนุมัติ (`CreditNote_Require_Approval`=1: Owner / Admin+SYS_ACCOUNTING): คืนผ่านเกตเวย์ `OnlinePaymentService.RefundTransaction` (ล้ม = กลับเป็นร่าง) →
     คืนสต๊อก → `Payment_History` REFUND ติดลบ (การจองยกเลิกแล้ว = REFUNDED ไม่นับยอด) → เข้าคิว. รายงาน: `TaxReportKit.LoadCreditNotes` = ยอดขาย/VAT ติดลบ
     ช่องทาง `CREDIT_NOTE` (DailySalesTaxSummary) + หักฝั่ง NextAcc ใน NextAccReconciliation. ยังไม่ทำ: e-Tax ของใบลดหนี้, Idempotency-Key header.
   - **ยกเลิก/คืนเงินการจอง (เชื่อมครบ):** เปิดระบบ = ปุ่มเดิมใน ReserveTable ไม่ทำบัญชีเอง — "ไม่คืนเงิน" → `CancelReservation(ยอด 0)` (ยึดผ่าน
     realize-deposit) · "คืนเงิน" → หน้า `?cancelRes=` เท่านั้น · ปุ่มเดิม (ปิดระบบ) ตรวจสถานะซ้ำ + ชิงสถานะ atomic + ปฏิเสธเมื่อมีใบลดหนี้.
     `CheckCancellable`: เข้าพักแล้ว (`IsArrivedStatus` = เช็คอินแล้ว/เสร็จสิ้น/เช็คเอ้าท์) = ใบลดหนี้ manual · ยกเลิกแล้ว = โหมด**ปิดเงินค้าง**
     (เลื่อนวันแล้วยกเลิก หรือเคยมีชุด `CXL-{id}-` และไม่มีคิว REFUND/FORFEIT_DEPOSIT เดิม — PostponeList ส่งต่อมาหน้านี้) · สลิปลูกค้ารอตรวจ = บล็อก.
     ลำดับ: ตรวจทั้งหมด → สร้างร่างทั้งชุด (หัว+บรรทัด+เพดานต่อใบต้นทางใน transaction เดียว) → การจองใน transaction + `WHERE Status=เดิม` →
     ชาร์จค้าง/ลิงก์ชำระค้าง (CANCELLED)/แจ้งเงินประกัน → อนุมัติ (เกตเวย์หลังสุด, `Refund_Ref` = `GWPRE:{ยอดคืนสะสมก่อนสั่ง}|` กันคืนซ้ำ).
     ร่าง = ใบมัดจำยังปกติ; Refunded/Forfeit เมื่ออนุมัติครบ (เฉพาะชุดยกเลิก — คืนมัดจำส่วนเกินตอนเช็คเอาท์ไม่แตะสถานะ); ยกเลิกร่าง → Deposit
     บวกคืน + ใบมัดจำกลับปกติ → ปิดเงินค้างใหม่ได้. ยอดคืนเริ่มต้น = ใบของ folio (มัดจำ/มี Payment_History/บรรทัดค่าห้อง) — ใบอื่นติ๊กรวมเอง ·
     ตัดใบ OTA_RECLASS/`RECLASS-` (OTA คืนลูกค้าเอง). มัดจำโหมด CHECKOUT/RECEIPT+defer: VAT ใบ = 0 พิมพ์ "ใบคืนเงินมัดจำ". `ReservationBalance`:
     ยอดรวม − ใบลดหนี้ที่อนุมัติ (ไม่ใช่มัดจำ) และ `PaidHeld` (Deposit + แถวคืน เมื่อไม่มีแถวรับเงิน) ⇒ คืนเงินไม่ทำให้ยอดค้างกลับขึ้น.
   - **การเชื่อมส่วนอื่น** (`Class/Accounting/CreditNoteGuards.cs` — อ่าน `Credit_Note` ตรง, guarded ไม่มีตาราง = เดิม): ใบเสร็จที่มีใบลดหนี้อ้าง
     (หรือ e-Tax Submitted/Accepted เมื่อเปิดระบบ — NextAcc `DocumentVoidPreconditions.cs:21-29,91-94`) **ยกเลิก/ลบ/แก้ไข/repost/reset ไม่ได้**
     (Receipt/CheckDocument(_New)/Voucher/ReceiptService + `EnqueueVoidReceipt`/`RepostReceiptWithCurrentLogic`/`ResetReservationAccounting` +
     admin resync/คืน-ริบมัดจำ) → ชี้ไป `CreditNotes?new=1&src=`. **เช็คเอาท์จ่ายเกิน**: `CheckoutService` กันส่วนเกิน (Credit − รูมเซอร์วิสเข้าห้อง − ค่าเสียหาย)
     ไว้ในเงินรับล่วงหน้า (ไม่ตัดเป็นรายได้ — เฉพาะเมื่อเปิดระบบ) → หน้า `Account/OverpayRefund?res=` ออกใบลดหนี้มัดจำ (`Create(fromCancellation:true)`
     = เส้น refund-deposit, ไม่ยกเลิกการจอง). คืนเงินเกตเวย์จากหน้า Admin/แดชบอร์ด (webhook Omise `refund.*`/`dispute.*`, Payso `Payso_Status_Refunded`
     + ฟิลด์ `refundedAmount`) = บันทึก `Refunded_Amount` + Telegram ไม่ลงบัญชีเอง (ไม่เขียน Payment_History — ใบลดหนี้ "คืนภายหลัง → บันทึกการคืนเงิน"
     ด้วยแหล่งเงินเกตเวย์เขียนให้). ตรวจสุขภาพข้อ 9 + Telegram poll (Global.asax, 15 นาที) ร่างรออนุมัติ/คืนเกตเวย์ไม่มีใบลดหนี้. สวิตช์ 2 ตัวใน
     Accounting Integration. ProfitLoss หักใบลดหนี้ + บวกส่วนที่ยึด; PaymentHistory การ์ด/ตัวกรอง REFUND. PHASE19_33 เปลี่ยนชื่อ `Credit_Note` schema เก่าเป็น
     `Credit_Note_Legacy_yyyyMMdd`. โค้ดใบลดหนี้เก่าใน `Class/AccountingService.cs` ลบแล้ว.
18. **ตรวจสลิปก่อนออกเอกสาร (ต.ค. 2026, PHASE19_34, opt-in `SlipVerify_Enabled` ใน System_Config):** นโยบายเจ้าของ = SlipOK (ฟรี 100/เดือน) →
   หมด/ใช้ไม่ได้ = อ่านตัวเลขยอดโอนจากรูปเอง → มีปัญหา = **ข้ามการตรวจ** (UNVERIFIED_BYPASS ใช้ยอดที่ลง ติดธงในแถวสลิป). โค้ด `Class/Payments/SlipCheck/`:
   `SlipVerificationService` (Inspect=ข้อเท็จจริงของรูป cache ตาม SHA-256 60 นาที → Evaluate=เทียบยอด/ซ้ำ/ผู้รับ → Decide=ไป/หยุด/พักรอพนักงาน → SaveResult),
   `SlipOkVerifier` (`POST https://api.slipok.com/api/line/apikey/{branch}` header `x-authorization`, multipart `data`=QR payload (หรือ `files`), `log=true`,
   ไม่ส่ง `amount`; รหัส 1003/1004/1015 โควต้า/แพ็กเกจ → ธง `Slip_Verify_Usage.Exhausted`, 1012/1013/1014 แนบ data, 1010 = รอ `data.delay` นาที, 1007+"หมดอายุ/ไม่มีรายการ" = NotFound;
   นับโควต้าเองรายเดือน; ปุ่มเช็คโควต้า `GET …/quota` → `ParseQuota` (quota+specialQuota = 0 → ธงหมด; overQuota>0 เตือน) — ยืนยันกับ **SlipOK API Guide v1.13 (28 ก.พ. 2026)**),
   - **ต.ค. 2026 — "ทดสอบสลิปแล้ว SlipOK ไม่ถูกเรียก/โควต้าไม่ลด":** ต้นเหตุ cache `Inspect` 60 นาทีจำผล "ยังไม่ได้ตั้งค่า SlipOK" ของรูปที่ทดสอบก่อนบันทึกคีย์ + ปุ่มทดสอบไม่ force.
     แก้: cache key ผูก `SlipVendorGate.ConfigFingerprint` (Branch/คีย์/โควต้า) + รายการที่ข้าม SlipOK ถือว่าเก่าเมื่อตอนนี้เรียกได้ (`IsStaleSkip`) · เหตุผลข้ามระบุที่มา (`SkipReason`: ไม่ตั้งค่า/ธง
     Exhausted/ตัวนับในระบบ/โควต้า 0/ไม่ใช่สลิป) · เส้น `data` ส่ง JSON `{data,log}` (`BuildJsonBody`) เฉพาะ QR ที่ CRC ถูก (`UsePayload`) ไม่งั้น multipart `files` (`BuildMultipartBody`);
     data ตอบไม่มี QR/ไม่พบรายการ → ส่งรูปซ้ำ 1 ครั้ง (`ShouldRetryWithFile`); `Expect: 100-continue` ปิด; `SlipVendorResponse.HttpStatus/BodyType/Attempts/RawError` ·
     หน้าตั้งค่า: ปุ่มตรวจ = force เสมอ + แสดง เรียก/ไม่เรียก-เพราะอะไร, HTTP/code, ส่งแบบ data|files, ตัวนับก่อน→หลัง, ธง; ปุ่ม "ทดสอบ SlipOK อย่างเดียว (บังคับเรียก)" =
     `Inspect(bytes,name,force:true,ignoreLocalQuota:true,skipLocalOcr:true)` (ไม่เก็บ cache เมื่อไม่ได้ยอด).
   `SlipQrDecoder` (ZXing.Net 0.16.11 `lib\net47\zxing.dll` + TLV mini-QR ธปท.: ธนาคาร+transRef+CRC16 — ไม่มี DLL = คืน null),
   `LocalSlipAmountReader` (**tesseract.exe ผ่าน Process** ไม่ใช่ P/Invoke — native crash ไม่ล้ม w3wp, timeout kill ได้). ผลอ่านจาก OCR ไม่ใช่ข้อมูลธนาคาร.
   **OCR 2 รอบ + โหวต** (พอร์ต 1:1 จาก `tools/slip-ocr-bench/reader.py` — แก้คู่กัน): EXIF → ตัดการ์ดสลิป (รูปถ่ายจอ: ก้อน min(R,G,B) สว่าง) → เทา/กลับสีโหมดมืด/กว้าง 1500/ยืด
   → รอบ 1 `tha+eng --psm 6` TSV หาป้าย จำนวนเงิน/จำนวน/ยอดชำระ/Amount + ตำแหน่ง (ขวา/ใต้ป้าย; ไม่เจอป้าย = รอบ 1b psm 11) → รอบ 2 ROI ตัวเลข (ลบคำตัวอักษร,
   ลบ ฿ ด้วยรูปทรง) ต่อภาพเดียว เทา+Otsu `eng` whitelist ตัวเลข → โหวต. ความมั่นใจ **92** = รอบ 1 + รอบ 2 ≥ 2 แบบตรงกันในบริบทป้าย ไม่มีคู่แข่ง (เกณฑ์ล็อก 85) ·
   80/72/62 · ขัดกัน/฿ กำกวม (8xxx vs ฿xxx) = ไม่คืนค่า (`SlipVerify_Ocr_Min_Confidence` 60). ผลเก็บใน `SlipInspection.OcrResult` → `ExtractAmount(…, result)`;
   ข้อความอย่างเดียว (cache เก่า) ≤ 70. ค่าตั้ง `SlipVerify_Ocr_Timeout_Ms` (15000/รอบ), `SlipVerify_Ocr_Budget_Ms` (20000 รวม). หน้า SlipCheckSettings "ทดสอบด้วยสลิป"
   แสดงข้อความแต่ละรอบ/ผู้สมัคร/ROI/โหวต + ภาพ ROI. **Benchmark** (สลิปจำลอง 9 ธนาคาร + สลิปจริง 6 ใบ × aug, 602 รูป): exact 89→97.5% · **ยอดผิด 4.8→0%**
   (ตัวเก่าผิดที่ conf 90 ถึง 24 ใบ ส่วนใหญ่ ฿→8) · conf ≥85 ผิด 0 · ~2.6 วินาที/ใบ. วัดกับสลิปจริงของโรงแรม: `tools/slip-ocr-bench/README.md` (ยอด SlipOK เป็นเฉลย).
   **กฎ:** VERIFIED ต่าง ≤ `SlipVerify_Amount_Tolerance` (1.00) → ผ่าน + ใช้ยอดธนาคาร (เฉพาะ SlipOK) · MISMATCH → หยุด "สลิป X / ลง Y / ต่าง Z" (แก้ยอดตามสลิป /
   ติ๊กแบ่งสลิปหลายการจอง → `Payment_Slip_Allocation` / Owner-Admin override + เหตุผล) · DUPLICATE (unique `UX_Payment_Slips_TransRef` บน ธนาคาร+transRef ยกเว้น
   REJECTED/ALLOCATED; 1012 ของ SlipOK ไม่นับ — ฐานข้อมูลเราเป็นตัวตัดสิน) → หยุด ห้าม override (ยกเว้นใช้ยอดคงเหลือของสลิปที่แบ่งไว้ → แถวรอง ALLOCATED) ·
   WRONG_RECEIVER (เทียบเลขปิดบังกับ `Account_Paid_How.Bank_Code/Bank_Account_No/PromptPay_Id` หรือ NextAcc `Account_Number`; ยังไม่ตั้งบัญชี = ข้าม) → หยุด, override ได้ ·
   ช่องทางลูกค้าทำเอง: ออกเอกสารอัตโนมัติเฉพาะ SlipOK ยืนยัน+ยอดตรง ไม่งั้นพักรอพนักงาน (หน้าจองลูกค้า → `Reservation` PendingStatus + Deposit 0 +
   `SubmitReservationSlipFileForVerification`; ต้องมี PHASE19_25). **จุดตรวจ:** Reserve (`RunSlipGate` ก่อนบันทึกใด ๆ ใน Button1 + `PreviewSlipCheck` หลังปุ่มอัปโหลด,
   ไฟล์ชั่วคราว `{เบอร์}_{hash session+การจอง}.jpg`, `ApplySlipVerifyToRow` หลัง uploadSlip) · `PaymentService.GateUploadedSlip` (ProcessReservationPayment/
   ProcessAdditionalPayment ก่อนสร้างสลิป/ใบเสร็จ) · `ApproveReservationSlip(…, overrideReason, amountOverride)` ลงยอดธนาคารแทน Claimed_Amount · หน้า
   `Account/SlipVerification` (คอลัมน์ผลตรวจ, ตรวจใหม่, อนุมัติตามยอดสลิป, ช่องเหตุผล override) · `ActivityService.ReviewBooking` + รูมเซอร์วิส `btnMarkPaid`
   (ไม่มีช่องเหตุผล → Owner/Admin กดซ้ำภายใน 10 นาที; กันซ้ำผ่าน `Slip_Verify_Log.Consumed`) · `MakePayment` เพิ่ม `Perm.Guard(FinReceipt)`. **Backstop:** `EnqueueReceipt` →
   `HoldQueueIfSlipBlocked` (คิวเป็น `AWAITING_SLIP` — worker อ่านแค่ PENDING/FAILED) ปล่อยเมื่อ override/อนุมัติ (`ReleaseQueueForReceipt`: ตัวล่าสุด PENDING
   ตัวเก่า SUPERSEDED). Telegram เมื่อซ้ำ/ผู้รับผิด/override (`Notify.Ev.PaymentOnline`). PDPA: เก็บเลขบัญชีที่ปิดบังเท่านั้น ส่ง QR payload แทนรูปเมื่ออ่าน QR ได้; ไม่ log คีย์.
   ตั้งค่า: `Admin/Settings/SlipCheckSettings` (สถานะ/โควต้าเดือนนี้/บัญชีรับเงิน/ทดสอบด้วยสลิป). **Deploy:** NuGet restore ZXing.Net 0.16.11; ติดตั้ง Tesseract 5
   (UB Mannheim, เลือกภาษา Thai) ที่ `C:\Program Files\Tesseract-OCR` หรือวาง `tesseract.exe`+DLL ที่ `~/App_Data/tesseract` และ `tha/eng.traineddata`
   (tessdata_fast) ที่ `~/App_Data/tessdata`; App Pool identity ต้อง execute ได้ + เขียน `%TEMP%`; VC++ 2015-2022 x64 (ตัวติดตั้งใส่ให้). ไม่มีอะไรเลย = ข้ามการตรวจ.
   - **ยอดล็อกตามสลิป (PHASE19_36):** `SlipVerificationService.GetAmountLock` (ที่เดียว) = SlipOK มีข้อมูลธนาคาร (ไม่นับ 1012) หรือ OCR ในเครื่อง (อ่านแบบไม่อิงยอดที่กรอก)
     ≥ `SlipVerify_Lock_Min_Confidence` (85) → ยอดที่บันทึก = ยอดสลิปเสมอ: UI ใส่ให้+readonly (`SlipAmountLockUi`, `Scripts/slip-amount-lock.js`; Reserve หลังปุ่มอัปโหลด,
     MakePayment AJAX `X-Slip-Preview`) และเซิร์ฟเวอร์บังคับซ้ำ (`VerifyWithLock` ใน Reserve `RunSlipGate`/`GateUploadedSlip`, `Decide` ตั้ง UseAmount=ยอดสลิป +
     ปิด override ยอดไม่ตรง, `ApproveReservationSlip` ไม่สน amountOverride, `SubmitReservationSlip*` Claimed=ยอดสลิป, กิจกรรม/รูมเซอร์วิส `VerifyFixedTotal`: เกิน=ผ่าน ขาด=ชำระไม่ครบ).
     โอนเกินยอดค้าง = บันทึกเต็มยอด (ก่อนเข้าพัก = ใบมัดจำ; หลังเข้าพัก = ปฏิเสธ ให้ไปเช็คเอาท์+ใบลดหนี้). แบ่งสลิป: ติ๊กแบ่ง = กรอกส่วนของการจอง < ยอดสลิป,
     การจองถัดไป ≤ ยอดคงเหลือ. ไม่ล็อก = พนักงานกรอกเองได้เสมอ (คำใบ้ HintAmount). **ลูกค้าทำเอง** (`EvaluateCustomerSlip`, `SlipVerify_Customer_Fail_Mode`):
     ตรวจไม่ผ่าน (อ่านไม่ได้/ไม่มั่นใจ/ยอดไม่ตรง/ไม่ใช่สลิป/NO_QR/NOT_FOUND/ผู้รับผิด/ขัดข้อง) → `CONTACT_ADMIN` ไม่รับ (จองห้อง = ไม่บันทึกการจอง, Pay/กิจกรรม/รูมเซอร์วิส =
     ไม่สร้างรายการ; เก็บไฟล์ + log + Telegram กันถี่) · `ALLOW_MANUAL` รับเป็นรอพนักงานตรวจ (`Customer_Manual_Amount`); สลิปซ้ำ = ไม่รับเสมอ. ผู้รับ QR ร้านค้า:
     `SlipVerify_Receiver_Biller_Ids`/`_Merchant_Ids` เทียบ proxy BILLERID/ref1-3/toMerchantId (`MatchesBillerOrMerchant`).
   - **หน้าจอง: อัปโหลดสลิปก่อนเห็นช่องยอด:** `Reserve.aspx.SlipFirst.cs` — เมื่อ `AmountLockActive` + ช่องทาง `IsTransferPaidHow` (กฎเดียวกับ
     `RunSlipGate`; ไม่ใช่เงินสด/เกตเวย์/PayNow/`02`) `#rvStepPay` ได้ `data-sf` = pick|wait|locked|edit|stop (`SlipFirstState`: ผลล่าสุดจาก
     `ShowSlipCheck` → `SetSlipFirstResult` ใน ViewState `rvSfState` + ไฟล์ชั่วคราวยังอยู่) → ขั้นย่อย 1-4 + การ์ด "โอนเงินตามยอดนี้" (มัดจำขั้นต่ำ/
     เต็มจำนวน หรือยอดค้าง + บัญชีจาก `LoadReceiverAccounts` + ปุ่มคัดลอก; พนักงานเปิด QR ได้) → `Scripts/slip-first.js` + `Content/slip-first.css`
     ห่อ FileUpload1 เป็นกล่องแตะ/ถ่ายรูป/ลากวาง แล้วกด Button3 ให้เอง ("กำลังตรวจสลิป…") → ช่องยอด (TextBox5 จอง / TextBox10 แก้ไข-เช็คอิน) ย้ายมา
     `#rvSfRevealSlot`: ล็อก / กรอกเอง (คำใบ้ HintAmount) / หยุด (ซ้ำ, ลูกค้า CONTACT_ADMIN + ปุ่มโทร/LINE `SlipAmountLockUi.ContactButtonsHtml`)
     + ผลก่อนยืนยัน (`SlipFirst.outcome`: รับมัดจำ/คงเหลือ · ชำระครบ · รับเกิน · ต่ำกว่ามัดจำขั้นต่ำ — ลูกค้าบล็อก, พนักงานเตือน). กดยืนยันโดยยังไม่อ่าน
     (ไม่มี JS) → `SlipFirstPreSubmit` อ่านก่อน (ล็อก = ไปต่อ, กรอกเอง/หยุด = หยุดให้ดู); เปลี่ยนวิธีชำระ = อ่านซ้ำจาก cache / ไม่ใช่โอนแล้วปลดล็อก.
     ด่านเซิร์ฟเวอร์ไม่เปลี่ยน. `MakePayment` ลำดับ วิธี→สลิป→ยอด (AJAX preview ส่ง event `sliplock:start|result|error`, ลิงก์ "ไม่มีสลิป? กรอกยอดเอง")
     · `Pay.aspx` กล่องแนบสลิปแบบเดียวกัน. ไม่มี data-sf / สคริปต์โหลดไม่ได้ = หน้าตาเดิม. ต้องทดสอบบน Windows.
   - **ตรวจสลิป async + ย่อรูป + เติมยอด + ไม่มีสลิป (ต.ค. 2026 — เจ้าของให้ตัดสินเอง):**
     ① `SlipFirst.prepare` (slip-first.js) ย่อก่อนส่งทุกหน้า (Reserve/MakePayment/Pay): ด้านยาว 1600 px JPEG 0.85 smoothing high, หมุนตาม EXIF,
     ≤600 KB/PDF/ไม่เล็กลง = ไฟล์เดิม (วัดสลิปจริง 6 ใบใน Chromium: QR 6/6 ยอดเท่าเดิม 6/6; 1024/0.75 เริ่มทำ QR/ยอดหาย). Reserve ส่งไป
     `API/SlipCheck.ashx` (IRequiresSessionState; nonce ต่อหน้าใน Session ผูก id/command/พนักงาน-ลูกค้า — `SlipUploadStore.IssueContext`,
     header `X-Slip-Check` + Origin/Referer โฮสต์เดียวกัน, 20 ครั้ง/session + 60/IP ต่อ 10 นาที, ≤8 MB + magic bytes, Web.config
     `maxRequestLength` 10 MB เฉพาะ path นี้) → เขียนไฟล์ชั่วคราวชื่อเดิม (`SlipUploadStore.TempFilename` = สูตร SlipTempFilename) →
     VerifyWithLock + `SlipFirstVerdict.Build` (แยกจาก ShowSlipCheck — ตัวเดียวกันทั้ง postback/async) → JSON; QR หายหรืออ่านยอดไม่ได้โดยไม่มีผล
     SlipOK = ส่งรูปเดิมซ้ำ 1 ครั้ง; API ใช้ไม่ได้ = กด Button3 เดิมด้วยรูปที่ย่อ. postback ถัดไป/Button1 `SfAdoptAsyncSlip` อ่านซ้ำจากไฟล์ (cache) ·
     RunSlipGate ตรวจจากไฟล์เสมอ (ไม่เชื่อค่าจากหน้า). ② พนักงาน: เลือกวิธีชำระล่าสุดของผู้ใช้ไว้ให้ (cookie `ttRvPH_{hash}` เก็บแค่ ID, เฉพาะเปิดยอดล็อก)
     + สถานะ pick มีปุ่มลัด ≤6 วิธี (ล่าสุดก่อน); **ไม่เพิ่มตัวเลือก "ยังไม่รับเงิน"** (เส้นบันทึกผูกวิธีชำระ→ใบเสร็จ ต้องมีสถานะไม่ออกใบเสร็จแยก — เลื่อนไว้).
     ③ ลูกค้าสลิปต่ำกว่ามัดจำขั้นต่ำ (80%): การ์ด "ยอดรวมจากสลิป X / ขั้นต่ำ Y / ขาดอีก Z" + คัดลอกยอดที่ขาด + "แนบสลิปเพิ่ม" (สูงสุด 3 ใบ,
     ไฟล์ `_t{n}`, ลบได้) + ปุ่มติดต่อ. `SlipUploadStore.EvaluateCustomerSet`: ทุกใบต้องล็อกยอด, ซ้ำ DB/ซ้ำในชุด (hash หรือธนาคาร+transRef) = ใช้ไม่ได้,
     CONTACT_ADMIN = ใช้ไม่ได้ / ALLOW_MANUAL = รอพนักงาน. บันทึก (`RunSlipGateTopUp`): ยอด = ผลรวมจากไฟล์; ธนาคารยืนยันครบ = **ใบเสร็จมัดจำใบเดียว**
     ยอดรวม + Payment_Slips ต่อใบผูก Account_Receipt_ID เดียว (`SfSaveTopUpRows` — NextAcc แนบสลิปครบ, คิว/บัญชีไม่เปลี่ยน); มีใบรอตรวจ = การจองรอตรวจ
     + สลิปรอตรวจแยกใบ (Claimed ต่อใบ, อนุมัติทีละใบตามกลไกเดิม). ใบเดียว: ยอดล็อกต่ำกว่าเกณฑ์ = หยุดหลังล็อก (เดิมด่านขั้นต่ำเชื่อยอดที่หน้าส่ง).
     ④ MakePayment ช่องทางโอนโดยไม่มีสลิป: เฉพาะ `CanRecordWithoutSlip` (Owner / Admin ที่มี FIN_RECEIPT|SYS_ACCOUNTING|FIN_REPORT — คนอื่นไม่เห็นลิงก์
     `pnlNoSlip` และเซิร์ฟเวอร์ปฏิเสธ) + เหตุผล ≥10 ตัว → หมายเหตุ `[ไม่มีสลิป — รอตรวจ]` + `RecordNoSlipPayment` (Logs, Slip_Verify_Log NO_SLIP,
     Telegram 1 ครั้ง/ผู้ใช้/10 นาที). ปิดยอดล็อก = เดิมทุกอย่าง. ต้องทดสอบบน Windows.
19. **กันเอกสารซ้ำ + ตัวตรวจเทียบ (ต.ค. 2026, PHASE19_35 — เคส #149286: REC-…0001/0002 + TIV-…0002/0003 ซ้อน ไม่มีใบถูก void):**
   - **แนบไฟล์:** `LookupReceiptAttachmentFiles` = PDF ของใบเอง (`{เลขใบ}[_uid].pdf` โฟลเดอร์ปี/เดือนของ `Created_Date` ±1) + สลิป
     `Payment_Slips.Account_Receipt_ID = ใบนี้` (สลิประดับการจองเฉพาะเมื่อการจองมีใบเดียว) — ใช้ทั้ง sync และ "📎 ไฟล์" ในหน้าต่างการจอง.
   - **ไม่สร้างซ้ำ:** `LookupKnownDocIdsForReceipt` (Nexaacc_Doc_Id + คิว CREATE **COMPLETED+SUPERSEDED**) · `EnsureRevenueDocCreatedApprovedAsync`
     marker เลย APR = หาเอกสารเดิม (ไม่เจอเลย = `ArgumentException` ให้คนตรวจ, เจอแต่ถูก void/404 ทั้งหมด = สร้างใหม่) · ก่อนสร้างทุกครั้ง
     (`GuardNoActivePriorDocAsync`, ทั้ง TaxInvoice และ Receipt/มัดจำ) เอกสารเดิมยังใช้งาน = draft ใช้ต่อ / โพสต์แล้ว = หยุด · ผูก `Nexaacc_Doc_Id` ทันทีหลังสร้าง.
   - **Retry/Sync หน้าเอกสาร** (CheckDocument_New/CheckDocument) ใบที่มี CREATE COMPLETED → `ResyncSingleReceipt` (ไม่ PrepareResync+Enqueue) ·
     `PrepareResync` โหมด DOCUMENT enqueue VOID ใบเสร็จด้วย (สมมติฐาน ReplaceExistingForSource ใช้ไม่ได้กับ company /document) · `enqueueVoid:false` เมื่อรู้ว่าเอกสารหายแล้ว.
   - **Repost fallback:** ใส่แค่ VOID (`recreateFromQueueId`) → `ProcessVoidReceipt` **GET ยืนยัน Voided/404** (`RequireDocumentVoidedAsync`; ไม่กลืน
     Posted/Draft/409 อีก — `IsVoidBlockedByRule`) แล้ว `AfterVerifiedReceiptVoid` จึง supersede+reset marker+CREATE ใหม่ (idempotent, ผลสั้น ≤100 ตัว).
   - **ล้างทั้งหมด:** void ทุกใบจาก `GET document/by-booking/RES-{id}` (DocumentController.cs:515-521, non-void) ∪ id ที่รู้ · ยืนยันรายใบ ·
     reset marker เฉพาะใบเสร็จที่ไม่เหลือเอกสารใช้งาน · ข้าม OTA/e-Tax ถึง RD · รายงานรายเอกสาร.
   - **Idempotency-Key** (NextAcc `IdempotencyMiddleware.cs:47-145`: 8-128 ตัว, แยก caller+method+path, replay 2xx 24 ชม., 409+Retry-After ระหว่างทำ):
     ทุก POST สร้าง (`AccountingApiClient.CreateRouteRegex`: /document, /document/payments, refund/realize/apply-deposit, convert, /integration/* creates
     รวม multipart, ocr create-document, payroll runs/import, stock adjust) คีย์ `TT-{เลขใบ}-q{queueId}[-g{#VOIDสำเร็จ}][-x{docหาย}]-{route}-{sha256(body)}`
     (ตั้งต่อแถวคิวใน ProcessQueueCoreAsync — คิวใบลดหนี้ได้ด้วยอัตโนมัติ; นอกคิว = คีย์สุ่มต่อการเรียก) · 409 in-flight = รอแล้วยิงคีย์เดิม ·
     `Nexaacc_Idempotency_Keys=0` → ไม่ส่งหัว **และไม่ยิงซ้ำเอง** ตอน timeout/เน็ตหลุด/502/504.
   - **ตัวตรวจเทียบ** (`AccountingSyncService.Reconcile.cs`): NextAcc (by-booking ∪ ช่วงวันที่ Reference RES-{id} ∪ GET ตัวชี้) vs Account_Receipt ทุกสถานะ +
     คิว CREATE/VOID ทุกสถานะ → MATCHED / ORPHAN_SUPERSEDED / ORPHAN_DELETED_LOCAL / ORPHAN_OTHER_BOOKING? / UNKNOWN_NEXTACC_ONLY · ธงใบ DUPLICATE /
     VOID_STUCK / AMOUNT_MISMATCH / MISSING_ON_NEXTACC · การจอง DEPOSIT_MISMATCH. แสดงในหน้าต่าง NextAcc ของการจอง (ReserveTable) ส่วน
     "เอกสารบน NextAcc ที่ไม่ผูกใบเสร็จ" + ปุ่ม `voidNaDoc` → `EnqueueVoidOrphanNextAccDoc` (ห้ามตัวชี้ใบที่ใช้งาน/เอกสารการจองอื่น/e-Tax ถึง RD → ใบลดหนี้).
     เก็บใน `Accounting_Reconcile_Finding` · รายคืน `ReconcileRecentReservationsIfDue` (Global.asax; `Nexaacc_Reconcile_Nightly/Days/Max/Hour`) ·
     ตรวจสุขภาพข้อ 10 · Telegram ผ่าน `NotifyQueueHealthIfNeeded` (พบใหม่) · log อายุ 180 วัน + ไม่ลบแถวที่ผลตรวจที่ยังเปิดอ้างถึง.
   - ข้อจำกัด: int_ void ไม่ GET-verify (ไม่มี company endpoint) · JV `…-CSDEPADJ-{doc8}` ของเอกสารกำพร้าไม่ถูกกลับอัตโนมัติ · ต้อง build+test บน Windows.
20. **ยอดหักมัดจำค้างหลังแก้ใบ (ต.ค. 2026 — เคส #149366 / REC261024001 "หักมัดจำ 4,700 > มัดจำจริง 1,500" แก้ใบแล้ว Retry ยังล้ม):**
   ยอดหักใน `ProcessReceiptDocument` = payload `depositApplied` → (0) `Account_Receipt.Deposit_Applied_Amount` → ถ้ามีบรรทัดติดลบ
   `ReconcileDepositApplied` ใช้บรรทัด (+persist เมื่อยังไม่โพสต์). ใบที่ **ไม่มีบรรทัดติดลบแล้ว** เดิมคืนค่าที่เก็บไว้ (รุ่นก่อนแก้) และ
   `Receipt.aspx` ไม่แตะคอลัมน์นี้ ⇒ ค้างถาวร. แก้: `DiscardStaleDepositApplied` (ProcessReceiptDocument + BuildCorrectedReceiptInvoice/Journal)
   — ยอดหัก > มูลค่าบรรทัดบวกของใบ และไม่มีบรรทัดติดลบ = ค่าค้าง → 0 (+ล้างคอลัมน์/`ClearDepositConsumed` เมื่อ GL ยังไม่โพสต์) ·
   `RealignDepositAppliedToLines` เรียกจาก `Receipt.aspx` **หลัง** `EnqueueVoidReceipt` (VOID จับ depositAppliedPosted ก่อน) ตั้งคอลัมน์ = ผลรวม
   บรรทัดติดลบที่เหลือ · `DepositExceedsPaidMessage` บอกที่มา (บรรทัด/คอลัมน์/payload) + วิธีแก้ตามที่มา · ดู PDF: `ViewReceiptDoc.ashx` ต่อ
   `?v={mtime}` กัน browser cache ไฟล์รุ่นก่อนแก้ + Receipt.aspx log ตอน render PDF ล้ม (เดิมกลืนเงียบ). Retry หน้าเอกสาร = enqueue ใหม่จาก DB
   (มี CREATE COMPLETED → `ResyncSingleReceipt` ตามข้อ 19); แก้ใบ = `TryRefreshQueuePayload` แทน payload แถว FAILED เดิม.
21. **กันคิวค้าง/ล้ม (ต.ค. 2026, PHASE19_37, `Nexaacc_Preflight_Mode` OFF|WARN|BLOCK — seed WARN; ไม่มีคอลัมน์/OFF = เดิมทุกอย่าง):**
   - **ชนิดปัญหารายแถว** (`Class/Integration/QueueErrors.cs`): `Error_Class` DATA/CONFIG/WAIT/TRANSIENT/POLICY/DUPLICATE/UNKNOWN + `Error_Rule`/`Fix_Url`/
     `Wait_Since`/`Depends_On_Queue_Id`. `QueueData/Config/PolicyException : ArgumentException` (catch เดิม = ตายทันที), `QueueWaitException` (จับก่อน
     `catch (Exception)`) → PENDING ไม่นับ retry, `Next_Retry_Date`+N, ตื่นเมื่อแถวที่รอ COMPLETED (`WakeWaitersOf` ใน `UpdateQueueStatus`), เกิน
     `Nexaacc_Wait_Max_Hours` (48)/ตัวที่รอตายถาวร → FAILED. ตัวช่วย `QueueErrors.Data/Config/Wait()` คืน `Exception` ธรรมดาเมื่อปิด. แปลงจุดโยน: หักมัดจำเกิน
     (`DepositExceedsPaidMessage`) = DATA R04 · ใบมัดจำยังไม่ขึ้น/รอ sync (2 เส้นเช็คเอาท์, SettleReceipt, DepositClearing) = WAIT ผูกแถวมัดจำ · ผู้ซื้อผิดชื่อ = DATA ·
     mapping ว่าง (`AccountingDataMapper.GetAccountId/GetAccountCode`) = CONFIG · รอใบต้นทาง/JE ใบลดหนี้, stock reverse/qty, OTA reclass, เงินประกัน OUT = WAIT.
     API error: `ApiErrorClassifier` ใช้ `data.ruleCode` ก่อน (NextAcc `ExceptionMiddleware.cs:191-198`; RD-ETAX-REACHED-VOID `DocumentService.cs:9570`,
     RD-50TWI-FILED `:9549`, PP36-CHANGE-AFTER-REMIT `Pp36Lifecycle.cs:54`, SETTLEMENT-ARTIFACT-VOID `:9544`, WHT-INCOME-TYPE `:475`) แล้วข้อความ (86/4,
     ไม่พบผังบัญชี, งวดปิด `JournalEntryBuilder.cs:140`, ยื่น ภ.พ.30 `IntegrationService.cs:3505`, มัดจำถูกตัดแล้ว `DocumentService.cs:6084`). 408/429/409-idempotency
     = TRANSIENT (`Nexaacc_Transient_MaxRetries` 10, backoff ถึง 6 ชม.); 5xx ที่ไม่ใช่ล่มทั้งแอป = DUPLICATE (อาจสร้างแล้ว). CONFIG กลับเข้าคิวเอง
     (`RequeueConfigHeld`) หลัง updateMapping/PaidHow/PaidType/syncAccounts/syncWallets/gateway/OTA map และตรวจสุขภาพที่ไม่มี error.
   - **ตัวตรวจใบเสร็จ** (`ReceiptPreflight.cs` บริสุทธิ์ + `.Data.cs`): กฎ R01 บรรทัด=หัว · R02 VAT (หน้าเว็บเท่านั้น) · R03 ยอด≤0 (EnqueueReceipt log ชัด) · R04 หักมัดจำ ≤
     ใบมัดจำที่รับจริง (#149366 4,700 vs 1,500 = Block/HardFail) · R05 "ส่วนลด" ไม่ระบุชนิด · R06 บรรทัดมัดจำคัดลอกซ้ำ · R07 ใบมัดจำ · R08 §86/4 เมื่อขอ e-Tax ·
     R09 แหล่งเงิน/R10 mapping (CONFIG ไม่บล็อกหน้าร้าน) · R11/R20 แก้ใบที่มีเอกสาร/ใบลดหนี้/เปลี่ยนเลข · R12 งวดปิด/ยื่น ภ.พ.30 (cache `Accounting_Nexaacc_Period_Status`
     จาก `fiscal-periods` + `tax?taxType=VAT` รายชั่วโมง; ไม่มี cache = เตือนตาม `Nexaacc_Vat_Filing_Day` 23 = e-Filing ตามที่เจ้าของเลือก) · R16 ทศนิยม · R17 ผู้ซื้อ. ใช้ที่: `EnqueueReceipt`
     (`ApplyEnqueuePreflight` — พัก = FAILED+Retry_Count=Max), `ProcessReceiptDocument` ด่านแรก, หน้า `Account/Receipt` (ก่อนเขียน DB), เช็คอิน Reserve
     (`CheckinReceiptPreflightOk` ก่อน `createReceipt` 3 จุด), สแกนรายคืน. WARN: หน้าเตือน/บันทึกได้ คิวพักเฉพาะ HardFail · BLOCK: หน้าหยุดปัญหาข้อมูล — Owner หรือ Admin
     ที่มี SYS_ACCOUNTING/FIN_REPORT ยืนยันข้ามด้วยเหตุผล ≥10 ตัว (Receipt: ช่อง `txtEditReason`; เช็คอิน: prompt → `pfOverrideReason`) บันทึก OVERRIDE/BLOCKED.
     นโยบายข้ามไม่ได้.
   - **Line_Kind** (`Account_Receipt_Detail`, DEPOSIT_DEDUCTION|DISCOUNT|ADJUSTMENT; NULL = กฎเดิม): `ReceiptLineClassifier` ตัวเดียวกับ `LookupReceiptLinesEx`
     (DISCOUNT/ADJUSTMENT กระจายลดรายได้ตามสัดส่วน ไม่นับหักมัดจำ) + `RealignDepositAppliedToLines`. เช็คอิน: บรรทัดคัดลอกใบมัดจำ = DEPOSIT_DEDUCTION (แก้บั๊ก
     `Rows[0]` คัดลอกบรรทัดแรกซ้ำ), "ส่วนลด" ส่วนต่างมัดจำ = ADJUSTMENT เฉพาะเมื่อรับด้วยใบอื่นครบ/การจอง OTA ไม่งั้นว่าง (ให้ R04/R05 ฟ้องคน). หน้าใบเสร็จคงชนิดข้ามการแก้ +
     dropdown `ddlLineKind`.
   - **Retry จาก DB**: หยิบแถว CREATE → `RebuildReceiptPayloadAtClaim` (ยอด/VAT/วันที่/มัดจำ/แหล่งเงิน/เบอร์ผู้ซื้อ/ยอดหัก; ข้าม `noRebuild` ที่ปักเมื่ออ่านคำตอบไม่ได้,
     walk-in, `Nexaacc_Payload_Rebuild=0`); ใบยกเลิก/REC… ไม่พบ + ไม่มีเอกสาร → COMPLETED `SKIPPED_RECEIPT_GONE`. แถว CREATE ใหม่ → แถวเก่าที่ยังไม่ส่ง SUPERSEDED;
     `FindUnsentEntry` รวม AWAITING_SLIP (รีเฟรชคงสถานะพัก). แก้ใบหน้า Receipt → `OnReceiptEdited`: ไม่มีเอกสาร NextAcc = รีเฟรชคิว (ไม่ใส่ VOID เปล่า, เปลี่ยนเลข = แถวเลขเดิม
     SUPERSEDED) · มีเอกสาร = VOID→CREATE เดิม · ยอดหักมัดจำ realign หลัง VOID เสมอ. การแก้ที่มีนัยทางบัญชี (หักมัดจำ/ยอด>1฿/ย้ายเดือน/แหล่งเงิน/เลขใบ/ผู้ซื้อหลังมีเอกสาร)
     ต้องมีเหตุผล → `Account_Receipt_Edit_Log` (ก่อน/หลัง JSON, ผลตรวจ, ทางที่ส่ง).
   - **หน้าคิว**: ป้ายชนิด + กรองชนิด (`cls=`) + การ์ด AWAITING_SLIP, ปุ่ม "🛠 แก้ที่นี่" (`Fix_Url`: แก้ใบ / `?focus=mapping&key=` / `?focus=paidHow` / ใบลดหนี้), DATA = "ตรวจซ้ำ"
     (`recheckItem` คืนเข้าคิวเมื่อผ่าน), CONFIG/POLICY ซ่อน Retry, WAIT = "ตรวจตอนนี้"; แถบโหมดตัวตรวจ (`setPreflightMode`, Owner/Admin บัญชี) + `preflightScan` + `integrityScan`.
   - **รายคืน** (`RunLocalIntegrityScanIfDue` Global.asax, `Nexaacc_Integrity_Hour/Days/Max`, ไม่เรียก API) → `Accounting_Reconcile_Finding` คีย์ `LOC:` (ตัวตรวจเทียบข้อ 19
     ไม่ปิดคีย์นี้): PREFLIGHT_BLOCK · NOT_ENQUEUED · ZERO_TOTAL (ไม่มี CLEAR_DEPOSIT_AT_CHECKOUT) · DEPOSIT_LEDGER (Reservation.Deposit ≠ ใบมัดจำ) · BALANCE_VS_RECEIPTS ·
     QUEUE_STUCK · PRE_FILING_GAP (ใบเดือนก่อนยังไม่ขึ้น; Telegram เมื่อเหลือ ≤5 วันถึงวันยื่น) + DATA ที่ผ่านแล้วคืนเข้าคิว. Telegram รอบ 6 ชม. แยกชนิด + แจ้งทันทีเมื่อ POLICY/
     DATA ของใบเสร็จ (กันซ้ำรายแถว ≤1 ข้อความ/10 นาที). ตรวจสุขภาพข้อ 11: โหมด, ค้างรายชนิด, ผ่านรอบแรก 14 วัน (เป้า ≥95%), หายเอง, ต้องมีคนแก้, บล็อก/ยืนยันข้าม.
   - ยังไม่ทำ (P2): ป้ายชนิดใน CheckDocument_New/ReserveTable, ตัวตรวจใบสำคัญจ่าย/ใบลดหนี้/เงินเดือน, ตารางตัวชี้วัดรายวัน, resync in-place ตอนแก้ใบที่มีเอกสาร,
     NextAcc `/document/validate`. ต้อง build+test บน Windows.
22. **"ดึงล่าสุด" ไม่ทับไฟล์ + แก้ชื่อผู้ซื้อแล้วส่งใหม่ชื่อไม่เปลี่ยน (ต.ค. 2026):** `Class/Integration/NextAccDocRefresh.cs` (pure, mono test) +
   `AccountingSyncService.BuyerSync.cs`.
   - **PDF:** ต้นเหตุ — `DownloadReceiptPdfFromNextAccAsync` หา GUID จากคิว COMPLETED อย่างเดียว (หลัง void→สร้างใหม่/supersede หาไม่เจอ) แล้ว
     **เสิร์ฟไฟล์ cache เก่าแม้ forceRefresh** ⇒ caller เห็น Found ไม่ลอง by-GUID (ลบไฟล์เองจึงหาย) · `ClearReceiptPdfCache` ล้างจาก Web.config แต่ตัวดึงใช้
     `AppCfg` (DB ก่อน) · ความสดดูแค่คิวฝั่งเรา (resync in-place/แก้ผู้ติดต่อ ไม่มีคิวใหม่) · redirect URL เดิม (browser cache). แก้: GUID จาก
     `Account_Receipt.Nexaacc_Doc_Id` ก่อน · forceRefresh ไม่เสิร์ฟ cache · `.docstate` = `เลข|สถานะ|fingerprint` (เลข/สถานะ/ยอด/VAT/วันที่/ผู้ติดต่อ/
     e-Tax/หักมัดจำ จาก GET document ที่ตรวจตัวตนอยู่แล้ว — NextAcc ไม่มี UpdatedAt) ไม่ตรง = ดึงใหม่ · เขียน atomic (tmp → ตรวจ `%PDF-` → Replace; ไฟล์ล็อก =
     `{stem}_v{ticks}{suffix}.pdf`; ล้ม = คงไฟล์เดิม + แจ้ง) ทั้งเส้นใบเสร็จและ by-GUID (by-GUID + force ล้ม = Found=false ไม่เสิร์ฟของเก่า) ·
     redirect `?v={mtime}-{size}` (CheckDocument_New/CheckPayment_New) · Web.config `Documents/Receipt`, `Documents/Payment/NextAcc` = DisableCache ·
     ViewReceiptDoc no-store.
   - **ผู้ซื้อ:** PDF NextAcc พิมพ์ชื่อจาก**ผู้ติดต่อของเอกสาร** (PdfGenerationService.cs:2148, ไม่มี snapshot); resyncUpdate ไม่แตะ ContactId/ธง declined
     (IntegrationService.cs:3311-3342); เอกสารลงบัญชีแล้วแก้ ContactId ไม่ได้ (DocumentService.cs:2611-2621); int_ invoice จับผู้ติดต่อด้วยเลขภาษี/ชื่อ ไม่ใช่
     ExternalId (IntegrationService.cs:1801-1826). เดิม repost INVOICE refresh แค่ "ผู้จอง" (ไม่ใช่ `Account_Receipt.Customer_ID`) และข้ามใบมัดจำ, RECEIPT ไป
     void→ใหม่ (เลขเปลี่ยน) ด้วยผู้ติดต่อ cache (มัดจำไม่ push ชื่อ). ตอนนี้ `RepostReceiptWithCurrentLogic` (ทุกปุ่มส่งใหม่/Resync/Retry) เรียก
     `PlanBuyerChangeForRepostAsync` ก่อน: GET เอกสาร → push ผู้ซื้อของใบ (`ResolveReceiptBuyer`; ไม่มีเบอร์ = `RES-{id}`) ขึ้นผู้ติดต่อของตัวเอง →
     `ReceiptBuyerChangePlanner.Decide`: ผู้ติดต่อเดิม = **แก้ชื่อผู้ติดต่อ** (PUT `document/contacts/{id}` DocumentController.cs:1486) ยืนยันด้วย GET
     ("✅ อัปเดตชื่อผู้ซื้อใน NextAcc แล้ว") — ยอด/วันที่/วิธีรับเงินเท่าเดิม = คืน 0 เลขเดิม ไม่ resync/void (`Nexaacc_Repost_NameOnly_Shortcut`=0 ปิด) ·
     คนละแถวเลขภาษีเดียวกัน = แก้ชื่อแถวที่เอกสารใช้ · คนละคน/ผู้ติดต่อกลางแต่ตอนนี้ §86/4 ครบ = **บังคับ void→สร้างใหม่** (ข้าม resyncUpdate/JE in-place) ·
     e-Tax ถึง RD = หยุด แนะใบลดหนี้ · ผู้ติดต่อกลาง + ผู้ซื้อยังไม่ครบ = อธิบาย (ชื่อไม่ขึ้นถูกต้อง) **ไม่แก้ชื่อผู้ติดต่อกลางเด็ดขาด**. ข้อความต่อหน้า
     `LastRepostMessage`. `ApplyContactToInvoice`/`BuildCorrectedReceiptInvoice` ใช้ชื่อผู้ติดต่อปัจจุบันแทนชื่อใน payload. ปิดทั้งหมด: `Nexaacc_Repost_Buyer_Sync=0`.
     ข้อจำกัด: แก้ชื่อผู้ติดต่อ = ทุกใบของผู้ติดต่อนั้นพิมพ์ชื่อใหม่ (รวมใบที่ส่ง e-Tax แล้ว — XML เดิมไม่เปลี่ยน); ต้อง company endpoint; ต้อง build+test บน Windows.
23. **UX/ความปลอดภัยหน้าจอ NextAcc — roadmap P0-1…P0-10 (ต.ค. 2026, ไม่มี migration):** ตัวกลาง `Class/Integration/NextAccUi.cs` (pure, mono test)
   = คำไทยของสถานะ/งาน/ชนิดรายการ/ชนิดปัญหา, ป้ายแถวคิวในหน้าเอกสาร (`DocQueueBadge` — encode ข้อความเสมอ), กติกาลองใหม่ทั้งหมด,
   ลิงก์แก้ของตรวจสุขภาพ, ข้อความเงินเดือน, ลิงก์/สรุป Telegram. **ห้ามเขียนรหัสภายใน/ข้อความจาก NextAcc ลง HTML ตรง** — ฝั่ง C# ผ่าน `NextAccUi.H`,
   ฝั่ง JS หน้าตั้งค่าผ่าน `escapeHtml` (คิว, ตรวจสุขภาพ, ทุก `data.message`/`err.message`, ตารางมัดจำ/ที่มาเอกสาร/ผังบัญชี).
   - **ลองใหม่ที่ล้มเหลวทั้งหมด** (`retryAllPreview` → confirm นับตามชนิด → `retryAllFailed`): รีเซ็ตเฉพาะ FAILED หมดรอบที่ชนิด TRANSIENT/UNKNOWN/WAIT
     (รอเกินกำหนด)/ยังไม่จำแนก — **ข้าม DUPLICATE/POLICY/CONFIG/DATA** (`NextAccUi.RetryAllBlockedClasses`) แล้วรายงานข้างปุ่มว่าข้ามอะไรเพราะอะไร + log.
     ลองใหม่/ยิงใหม่รายแถวของ DUPLICATE = Owner + เหตุผล ≥10 ตัว (`GuardDuplicateRetry`, log AccountingSync); ปุ่มในคิวเป็น "ลองใหม่ (เจ้าของ)".
   - คิว: ป้าย/หัวตารางไทย (เซิร์ฟเวอร์ส่ง `statusLabel/actionLabel/entityLabel`, รหัสจริงใน tooltip) + ค้นหา `?q=` (เลขใบเสร็จ/เลขจอง `reservationId`/
     เลขเอกสาร NextAcc/เลขคิว — พารามิเตอร์ทั้งหมด, escape LIKE) + เปิดจากลิงก์ `?cls=`/`?status=`/`?q=`/`?focus=`. ตรวจสุขภาพทุกข้อมี `fixUrl`
     (`NextAccUi.HealthFixUrl`: `?focus=api|sync|mapping&key=|paidHow|gateway|ota|email|queue` เลื่อน+โหลดตารางให้ / `/Account/CreditNotes` ฯลฯ) และรหัส
     migration ย้ายไป "รายละเอียดสำหรับผู้ดูแล" (`HideInternalCodes`).
   - Telegram: ลิงก์เต็มไปคิวที่กรองตามชนิด (`PaymentUrls.SiteBase()` = `Payment_Site_BaseUrl` → `SiteBaseUrl`) + สรุปวันละครั้ง "ปกติดี"
     (`Nexaacc_Queue_Daily_Summary` 1, `_Hour` 9, จำวันใน `_Last`).
   - CheckDocument_New/CheckPayment_New: ป้ายตาม `Error_Class` (ซ่อน "ส่งใหม่" ของ CONFIG/POLICY/DUPLICATE, ลิงก์ "แก้ที่นี่"/"ดูในคิว"), คำไทย AWAITING_SLIP/
     SUPERSEDED, แถบเตือนเมื่อดึง NextAcc ไม่ทัน (20/18 วิ). ตารางรายได้ CheckDocument_New: คอลัมน์ **"อื่น ๆ"** + แจกแจงรายแหล่งเงิน
     (`Class/Reports/RevenueBySource.cs` — ทุกวิธีชำระลงกล่องเดียว: คำค้นเดิม → `Account_Paid_How.Channel_Type` CASH/DIRECTOR_LOAN → อื่น ๆ) ⇒ ผลรวมคอลัมน์ =
     ยอดรวม (ตรวจใน `ValidateTotal`); CSV เพิ่มคอลัมน์ + quote ค่า.
   - เครื่องมือ "Stock Adjustment & Write-off" ในหน้าตั้งค่าปลดระวาง (ซ่อน + server ปฏิเสธ `stockAdjustment` → ใช้ Product/Stock); `Account/CheckDocument.aspx`
     redirect → `CheckDocument_New` (คงพารามิเตอร์, endResponse). เมนูการเงินเพิ่ม CreditPurchasePayment + PurchaseReturn พร้อมตัวเลขงานค้าง (แคช 5 นาที).
     เงินเดือน: ข้อความไทยไม่มีรหัสโหมด + ลิงก์คิว; JOURNAL_ONLY เตือนตลอดว่า ภ.ง.ด.1/สปส.1-10/50 ทวิ ต้องยื่นจากระบบนี้. คำ: "ยกเลิก (void)" = เอกสาร NextAcc,
     "ลบ" = ใบในระบบ. ไม่ทำ: P0-11 (OCR — ทีมอื่น). ต้อง build+test บน Windows.
24. **ป้าย "สถานะ NextAcc" แบบเดียวทุกหน้า + ศูนย์บัญชี NextAcc (ต.ค. 2026 — roadmap P1-1/P1-2, ไม่มี migration):**
   - **ป้าย:** `Class/Integration/NextAccUi.Badge.cs` (pure, mono test) `ResolveBadge` → สถานะ ส่งแล้ว (เลขเอกสาร) / รอส่ง / กำลังส่ง /
     รอเอกสารอื่น (WAIT, AWAITING_SLIP) / ต้องแก้ข้อมูล (DATA) / ต้องตั้งค่า (CONFIG) / ต้องใบลดหนี้ (POLICY) / อาจซ้ำ (DUPLICATE) /
     ส่งไม่สำเร็จ / ยกเลิกแล้ว / ยังไม่ส่ง / ไม่ส่งบัญชี — ลำดับ: ยกเลิก → ไม่ส่งบัญชี → แถวคิวล่าสุด → เอกสารที่รู้ (marker) → ยังไม่ส่ง;
     `ShowRetry` กติกาเดิม (ซ่อน CONFIG/POLICY/DUPLICATE). `BuildBadgeLinks` = เปิดใน NextAcc (`AccountingConfig.WebBaseUrl` =
     `Nexaacc_Web_BaseUrl` ถ้าตั้ง ไม่งั้น `Nexaacc_BaseUrl` เดิม; ไม่มีบริษัท/GUID/http(s) = ไม่แสดง) · PDF (ตัวดูเดิม) · แก้ที่นี่ (`Fix_Url`,
     `?focus=` → หน้าตั้งค่า; CONFIG/POLICY มีค่าเริ่มต้น) · ดูในคิว (`?q=`). `RenderBadge` encode ทุกอย่าง + ตรวจ URL; `BadgeToDict` → JSON ให้
     `Scripts/nextacc-ui.js` (`NaBadge.render`, ตรวจซ้ำ) · CSS `Content/nextacc-ui.css` (`.na-st--{code}`) โหลดใน Site.Master.
     ข้อมูล: `NextAccStatusData` — `LatestByPayloadKey`/`LatestByEntityId` (1 query ต่อชุด ≤80 คีย์, คอลัมน์ guarded), `ReceiptBadges`/`CreditNoteBadges`.
   - **ใช้ที่:** CheckDocument_New / CheckPayment_New (แทนป้ายข้อ 23 — `BuildQueueBadge`/`BuildNexaaccLink` ของ CheckDocument ลบแล้ว),
     ReserveTable หน้าต่าง NextAcc (`WithNaBadges` — สถานะ GET จริงเป็นตัวตัดสินเอกสาร + ลิงก์คิวของการจอง), CreditNotes (คอลัมน์ NextAcc),
     OverpayRefund (การ์ดสถานะใบเสร็จ/ใบลดหนี้ของการจอง), Product/In (รวมต่อการรับของ — STOCK_IN `PIN-{สินค้า}-{เวลา}` ±1 วิ), Product/PurchaseReturn
     (PRT/-REV/-DIF + ใบลดหนี้ผู้ขายที่ผูก), Product/Stock (STKOUT/STOCKOUT + `Nexaacc_Journal_Ref`), PayrollManagement (IMPORT/SYNC_PAYROLL_RUN ของงวด).
   - **ศูนย์บัญชี** `Account/NextAccHub.aspx` (`NextAccHubData` อ่าน DB อย่างเดียว; สิทธิ์ = `ReceiptPreflight.CanOverrideCurrentUser`): การเชื่อมต่อ
     (ส่งสำเร็จล่าสุด, พักเพราะล่ม, ชนิดคีย์ภาษาไทย, ผังบัญชี, โหมดตัวตรวจ) · คิวตามชนิด → ลิงก์คิวกรอง · งานที่ต้องมีคนทำ (FAILED DATA/CONFIG/POLICY/DUPLICATE
     top 15) · ก่อนยื่น ภ.พ.30 (`VatFilingCountdown` + `Nexaacc_Vat_Filing_Day`, PRE_FILING_GAP, คิวเดือนภาษีค้าง) · ตรวจเทียบ (ข้อ 19 + LOC:, `FindingTypeTh`) ·
     งานข้ามโมดูล (สลิปรอตรวจ/ธง, ใบลดหนี้รออนุมัติ/ส่งไม่สำเร็จ/รอคืน, รอใบลดหนี้ผู้ขาย, OCR ค้าง) · ปุ่มลัดเรียก `AccountingIntegration?action=processQueue|
     healthCheck|preflightScan|integrityScan` เดิม (ต้อง SYS_ACCOUNTING; ไม่มีตรรกะซ้ำ) · รีเฟรชเอง 60 วิ (localStorage). เมนูการเงินบนสุด + ตัวเลขงานค้าง
     (แคช 5 นาที), การ์ดหน้า Settings, ลิงก์ในสรุป Telegram รายวัน. ต้อง build+test บน Windows.
25. **ใช้ความสามารถ NextAcc ที่ยังไม่เคยเรียก (ต.ค. 2026, PHASE19_41 — เลข 40 ทีมอื่นจองแล้ว; ไม่มีตาราง = ทำงานได้แต่ไม่มีประวัติ):**
   ตรรกะบริสุทธิ์ `Class/Integration/NextAccActions.cs` (mono test) + บริการ `AccountingSyncService.NextAccActions.cs` + หน้า `Account/ReceiptNextAcc.aspx`
   (`?receipt=`/`?uid=` — ลิงก์จาก `Receipt.aspx` โหมดแก้ไข) · `Account/UndueInputVat.aspx` (เมนูการเงิน) · บล็อกแยกใน `Account/CreditNotes.aspx` (`CreditNotes.Etax.cs`).
   ประวัติ `Nexaacc_Doc_Action` (ใคร/ขออะไร/ผล/เอกสารใหม่). สิทธิ์: Owner / Admin ที่มี SYS_ACCOUNTING|FIN_REPORT|FIN_RECEIPT (หน้า 11640: SYS_ACCOUNTING|FIN_REPORT).
   - **① ใบกำกับเต็มรูปทีหลัง** — คิว `ISSUE_FULL_TAX_INVOICE` (Entity RECEIPT, ประมวลผลทันทีจากหน้า `ProcessNxActionNowAsync`; แถวที่ยังไม่ส่งของใบเดิม = ใช้แถวเดิม ⇒
     Idempotency-Key เดิม; เส้นทางอยู่ใน `CreateRouteRegex`). NextAcc `POST document/{id}/issue-full-tax-invoice` {Reason} (DocumentController.cs:1138-1157, สิทธิ์ระดับ Approve)
     → `IssueFullTaxInvoiceForReceiptAsync` (DocumentService.cs:13109-13176): ต้นทางต้องเป็น Receipt/ReceiptVoucher (:13116-13120) · `FullTaxInvoiceReplacement.Check`
     (Helpers/FullTaxInvoiceReplacement.cs:56-90 — จด VAT/อนุมัติแล้ว/มี VAT/ยังไม่เคยออก/ผู้ซื้อของ "ผู้ติดต่อใบต้นทาง" ครบ §86/4) · ใบแทน TaxInvoice วันที่เดิม
     ไม่มี JE (:7258-7273) RelatedDocumentId=ต้นทาง (:12921) Reference=เลขเอกสารเดิม (:12881) · อนุมัติไม่ผ่าน (งวดปิด) = ใบแทนค้างร่าง RD-86/4-REPLACE-APPROVE-FAILED
     (:13156-13173). "เต็มรูปอยู่แล้ว" = TaxInvoice หรือผู้ติดต่อครบ+ไม่ declined+ไม่ walk-in (:13095-13097); ContactId ของใบลงบัญชีแก้ไม่ได้ (:2658-2662).
     ลำดับ (`FullTaxInvoicePlanner`): ตรวจฟอร์ม (ชื่อ+ที่อยู่ §86/4(3), เลข 13 หลัก mod-11 ตาม TaxInvoiceCompletenessChecker.cs:157, สาขา 5 หลัก — นิติว่าง=00000 :97-120)
     → บันทึกผู้ซื้อในระบบ (UPDATE Customer เฉพาะชื่อ/เลข/สาขา/ชนิด/ที่อยู่ — ไม่ใช้ UpsertCustomer กับแถวเดิมเพราะทับชื่อเล่น/อีเมล) → GET เอกสาร: ยกเลิก/ร่าง/มีใบลดหนี้/
     e-Tax ถึง RD/ไม่มี VAT = หยุด · เคยออกแล้ว = ALREADY · ใบแทนค้างร่าง = อนุมัติใบนั้น (ไม่ออกซ้ำ) · ผู้ติดต่อกลาง/เลขภาษีคนละราย/TaxInvoice ที่ declined =
     `NeedRecreate` (ทำเมื่อติ๊กยอมรับ → `RepostReceiptByNumber` ยกเลิก→ออกใหม่ เลขเปลี่ยน) · ไม่งั้น PUT ผู้ติดต่อ "ของเอกสาร" (DocumentController.cs:1486) ยืนยันด้วย GET →
     อ่านค่าที่ NextAcc คำนวณ (`CanIssueFullTaxInvoice`/`FullTaxInvoiceBlockedReason` DocumentService.cs:2075-2083, `DocumentTitle`): ออกได้ → issue (ใบแทน + e-Tax อัตโนมัติถ้าเปิด) ·
     หัวเป็นใบกำกับแล้ว = `ContactUpgrade` (เลขเดิม) · อื่น ๆ = หยุดพร้อมเหตุผลของ NextAcc. ทุกทางล้าง PDF cache (ข้อ 22). PDF ใบแทนเปิดจากหน้านี้ (`?pdf=` เฉพาะ GUID ของใบ/
     ในประวัติ) เพราะตัวดึง PDF ใบเสร็จยืนยันตัวตนด้วย Reference = เลขใบเสร็จ. **ผลข้างเคียงที่กันไว้:** `CreditNoteGuards.VoidBlockReason` บล็อกยกเลิก/แก้/ส่งซ้ำใบที่ออกใบแทนแล้ว
     · ตัวตรวจเทียบ (ข้อ 19) นับใบแทน (`ReplacesDocumentId`) = MATCHED ไม่ใช่ DUPLICATE/กำพร้า และ `voidNaDoc` ปฏิเสธใบแทน. ข้อจำกัด: แก้ผู้ติดต่อ = ทุกใบของผู้ติดต่อนั้น
     พิมพ์ข้อมูลใหม่ (เหมือนข้อ 22); ใบลดหนี้ของใบที่ออกใบแทนแล้วยังอ้างใบเดิม (ทีมใบลดหนี้ควรเปลี่ยนเป็นอ้างใบแทน); int_ = ใช้ไม่ได้ (ไม่มี company endpoint).
   - **② แก้แหล่งเงินที่เลือกผิด** — คิว `RECLASSIFY_PAYMENT_SOURCE` → `POST document/{id}/reclassify-payment-source` {NewPaymentAccountId=`Account_Paid_How.Nexaacc_AccountId`,
     Reason} (DocumentController.cs:739-757, สิทธิ์ Approve) → `ReclassifyPaymentSourceAsync` (DocumentService.cs:9067-9240): ต้องมีแหล่งเงินบนหัวเอกสาร (:9082-9085) · อนุมัติแล้ว ·
     งวด Open (:9095-9100) · ไม่มีเอกสารปลายทาง (:9102-9107) · **ไม่มีรายการชำระแยก (:9111-9116)** · ไม่อยู่ในรายงานภาษีที่ยื่น (:9118-9124) · ไม่ส่ง e-Tax (:9126-9129) ·
     JE แก้ (เงินเข้า: Dr ใหม่/Cr เก่า) วันที่เอกสารเดิม (:9207-9226). ⇒ ใช้ได้กับใบรับมัดจำ/ใบเสร็จ (Receipt type 3 มี PaymentAccountId) — **ใบกำกับภาษี + รับชำระแยก (ข้อ 6b)
     ใช้ไม่ได้** (NextAcc ไม่มี API เปลี่ยนแหล่งเงินของ payment) → ข้อความไทยให้ใช้การแก้ใบเสร็จตามปกติ. `PaymentSourceReclassPolicy` กันก่อนยิง: ใบหักมัดจำในใบ (JE ปรับปรุงของเรา
     Cr บัญชีเดิม — ย้ายแค่ขาเอกสารทำบัญชีเดิมติดลบ) · งวดปิดจาก cache ข้อ 21 · e-Tax/ใบลดหนี้/ใบแทน · บัญชีเดียวกัน/ยังไม่ส่ง/ถูก void = แก้เฉพาะในระบบ. สำเร็จ → `Account_Receipt.Paid_Type`
     + `Payment_History.PaymentMethod` (แถวรับเงินของใบที่ใช้ชื่อเดิม) + `Account_Receipt_Edit_Log` (ก่อน/หลัง). retry หลังสำเร็จ = เห็นบัญชีใหม่บนเอกสารแล้วไม่ยิงซ้ำ.
     ใบสำคัญจ่าย: ไม่ทำ UI (หน้า PaymentVoucher ทีมอื่น) — API client พร้อม (`ReclassifyPaymentSourceAsync`) ใช้กับ PV company (type 13) ได้.
   - **③ ภาษีซื้อยังไม่ถึงกำหนด 11640** — `GET document/undue-input-vat` (DocumentController.cs:450-455 → DocumentService.cs:6343-6390; ไม่มีพารามิเตอร์ กรองเดือน/เลยกำหนดฝั่งเรา) +
     ฟอร์ม → PUT ผู้ติดต่อผู้ขาย (เฉพาะช่องที่เปลี่ยน; เลขขึ้นต้น 0 = นิติบุคคล) → `POST document/{id}/complete-tax-invoice` {SupplierInvoiceNumber, SupplierTaxInvoiceDate,
     SupplierBranchCode} (:398-412, สิทธิ์ Document.Purchase.Create; DocumentDtos.cs:395-410) → NextAcc ย้าย 11640→11610 เมื่อครบ (`ReclassifyUndueInputVatAsync` :15943; ครบ =
     ชื่อ/เลข mod-11/ที่อยู่/สาขา/เลขที่/วันที่ใบกำกับ TaxInvoiceCompletenessChecker.cs:34-62; พ้น §82/3 6 เดือนไม่ย้าย :15957-15963) → อ่านรายการใหม่: หาย = สำเร็จ, ยังอยู่ = แสดงช่องที่ขาด.
     ใบ ภ.พ.36 (IsForeignService) แสดงอย่างเดียว (เคลมผ่านหน้านำส่งภาษี).
   - **④ e-Tax ใบลดหนี้** — `POST etax/generate` {DocumentId} (EtaxController.cs:61-68, สิทธิ์ EtaxIssue; CN → TypeCode 81 Helpers/EtaxDocumentTypeMap.cs:25-35; CN ต้องมี
     RelatedDocumentId และต้นทางฝั่งขาย, ไม่ซ้ำ — EtaxInvoiceService.GenerateAsync) → ส่ง: `etax/{id}/submit` (:100-106, ต้อง Signed) หรือ `sign-and-submit` (:210-220).
     เฉพาะใบลดหนี้ SYNCED ที่มีเอกสาร NextAcc (COMPANY_CN / ใบลดหนี้อัตโนมัติของ DEPOSIT_REFUND / INTEGRATION_CN; JOURNAL/LOCAL ไม่มีใบ). `CreditNote_Etax_Auto` (seed 0):
     1 = หลังคิว CREATE_CREDIT_NOTE สำเร็จ ถ้าใบต้นทาง e-Tax ถึงกรมสรรพากร ออกให้เอง (ไม่โยน). สถานะเก็บ `Credit_Note.Etax_*`. **ส่งอีเมลเอกสาร** (ใบลดหนี้/ใบเสร็จ/ใบแทน):
     `POST document/{id}/send-email` {To,…,AttachPdf} (DocumentController.cs:104-121, สิทธิ์ Document.Create; ล้ม = 200 success:false). (หมายเหตุ: `SendEtaxByEmailRequest` เดิม
     ส่ง `recipientEmail` แต่ NextAcc รับ `To` — EmailDtos.cs:16 — **แก้แล้ว**: `SendEtaxByEmailRequest` ส่ง To/IncludePdf/IncludeXml/IncludeRdTimestamp (ชื่อเดิมเป็น alias ไม่ serialize).
   - ไม่ทำ: `reissue-settlement-paid` (เฉพาะใบที่รอบโอน settlement ลงบัญชีแล้ว), webhooks, รายงาน NextAcc, AI แนะนำบัญชี (ดู audit §5). ไม่มี JS ใหม่. ต้อง build+test บน Windows.
26. **หน้าตั้งค่า NextAcc แบ่งแท็บ + วิซาร์ดตั้งค่า 6 ขั้น (ต.ค. 2026, ไม่มี migration):** `Admin/Settings/AccountingIntegration.aspx` = 7 แท็บฝั่งเบราว์เซอร์
   (การเชื่อมต่อ · ผังบัญชี & แหล่งเงิน · โหมดการส่ง & นโยบาย · คิว & ตรวจสุขภาพ · เครื่องมือ (+ **โซนอันตราย** พับไว้: ลบใบที่หาย/เก็บกวาด/กลับมัดจำค้าง/รีเซ็ตการจอง/
   มัดจำด้วยมือ/void OTADOC — ทุกปุ่ม confirm) · อีเมลจอง & LINE · บันทึก (`action=recentSyncLog` อ่าน Logs 1-168 ชม., ไม่ใช่ Owner ตัดบรรทัดเงินเดือน)).
   **id คอนโทรล/ชื่อฟังก์ชัน/action เดิมคงทั้งหมด** (ฟังก์ชันได้พารามิเตอร์เสริมท้ายเท่านั้น เช่น `saveSyncSettings(resultId, opts)`, `postAction(..., {noReload, done})`,
   `updateMapping(id, idPrefix, resultId, after)`); ทุกปุ่มบันทึกในแท็บนโยบาย = `saveSyncSettings` ตัวเดิม (ส่งค่าทุกข้อพร้อมกัน — ความหมายค่าไม่เปลี่ยน).
   ลิงก์: `#t-<แท็บ>` (จำแท็บข้ามรีโหลด) · `?focus=api|sync|mapping&key=|paidHow|gateway|ota|paidType|email|health|queue` → แท็บของส่วนนั้น (`ACC_FOCUS_TAB` + `accShowFor` ใน
   `applyFocus`) · `?cls=/?status=/?q=` → แท็บคิว · `#queueStats` (id ส่วน) → แท็บที่มีส่วนนั้น · `?wizard=1`/`#wizard` เปิดวิซาร์ด. เพิ่มส่วน/การ์ดใหม่ = ใส่ใน `<section id="tab-…">`
   ที่ถูก (+ เพิ่ม focus ใน `ACC_FOCUS_TAB` ถ้ามีลิงก์). ป้าย "ค่าแนะนำ" = `<span class="rec-badge" data-rec-for="cfgX" data-rec="ค่า" data-rec-label="ไทย">` (เขียว/ส้มอัตโนมัติ;
   `neutral` = นโยบายกิจการ) — ค่าแนะนำตรงกับ `ApplyRecommendedPreset`. **วิซาร์ด** (modal, Owner/Admin บัญชี = `ReceiptPreflight.CanOverrideCurrentUser`): 1 คีย์ (int_ = "คีย์เชื่อมระบบ",
   acc_ = "คีย์บริษัท (ไม่บังคับ)", เตือนใส่ผิดช่อง) + saveApi/testApi · 2 syncWallets/syncAccounts · 3 แหล่งเงิน (`wzSuggestPaidHow`: เลข 4 ตัวท้าย/ชื่อธนาคาร/เงินสด 11111/เกตเวย์ 11340/
   OTA/เงินประกัน 21530/กรรมการ — เลือกให้แต่ไม่บันทึกจนกด) · 4 บัญชีสำคัญ (`WZ_KEYS`: รหัสผังมาตรฐาน → คำในชื่อ; เรียง "จำเป็น/ใช้กับฟีเจอร์ที่เปิด" ก่อน; แถวที่ไม่มีใน
   `Accounting_Account_Mapping` = ข้าม) · 5 นโยบาย (VAT มัดจำ 3 โหมดพร้อมตัวอย่าง JE มัดจำ 1,000 + เช็คเอาท์ 3,000, ใบลดหนี้, ตัวตรวจก่อนส่ง `setPreflightMode`, ลิงก์ SlipCheckSettings)
   · 6 healthCheck + สรุป. จำขั้นใน `Accounting_Integration_Config.Nexaacc_Setup_Wizard_Step` (1-6|DONE, `action=saveWizardStep`) → แถบ "ทำต่อ". มือถือ: ไม่มี scroll แนวนอนทั้งหน้า
   (ตารางเลื่อนในการ์ด). ทดสอบ: static render + Playwright (แท็บ/ลิงก์/วิซาร์ด/escape/390px). ต้อง build+test บน Windows.

## QR สั่งอาหาร/เครื่องดื่ม แยกโต๊ะ/โซน (Dining — ต.ค. 2026, PHASE19_42)
โมดูลแยกจากรูมเซอร์วิส/POS ทั้งหมด (ไม่แตะ flow เดิม) ใช้ซ้ำ Product/Product_Images, ตรวจสลิป (ข้อ 18), แคตตาล็อกแหล่งเงิน, สมุดขายรายช่องทาง (PHASE19_32),
Notify/NotificationService/กล่องแชทรวม. **สัญญา API ของชั้น Foundation = `Take Time BangPhra/Class/Dining/README_API.md` (อ่านก่อนแก้ทุกครั้ง).**
ไม่มีตาราง (ยังไม่รัน migration) = service อ่านคืนว่าง/เขียนคืน `NOT_READY`, ฟีเจอร์ปิด, หน้าตั้งค่าขึ้นเตือน. **C# 7.3 เท่านั้น.**
- **คำตัดสินเจ้าของ (29 ต.ค. 2026):** ราคาเมนูรวม VAT, **ไม่มี service charge/ทิป** · เงินสด = พนักงานเก็บที่โต๊ะแล้วกด "รับเงินสด" บนกระดาน · โอน = ระบบตรวจสลิปเอง
  (SlipOK/OCR ผ่าน+ยอดตรง = PAID ทันที; ไม่ผ่านตาม `SlipVerify_Customer_Fail_Mode`; ซ้ำ = ไม่รับเสมอ) · สั่งได้หลายรอบต่อโต๊ะ (session) + ปุ่ม "เช็คบิล" ·
  ลงบิลห้องพักลูกค้าทำเอง (เลือกเลขห้องที่เช็คอินอยู่ + กรอกเบอร์ที่จอง; ผิดครบ `Dining_Room_Charge_Max_Fail` = ล็อก 15 นาที + Telegram).
- **ข้อมูล (migration `Database/PHASE19_Migration_42_Dining_QR_Ordering.sql`, idempotent):** `Dining_Zone` (Code, เวลาเปิด-ปิด, Manual_Mode AUTO|OPEN|CLOSED, Allow_Cash/Transfer/Room_Charge,
  Kitchen_Station) → `Dining_Table` (QR_Token 32 ไบต์ CSPRNG base64url หมุนได้ → QR_Version; Status FREE|OCCUPIED|BILL_REQUESTED|NEEDS_STAFF|DISABLED) → `Dining_Table_Session`
  (1 รอบลูกค้า, Session_Token = cookie `dnsess` คนละตัวกับ QR_Token; OPEN|BILL_REQUESTED|CLOSED) → `Dining_Order` (Order_No `DN{yyMMdd}-####`, Round_No; NEW→ACCEPTED→PREPARING→SERVED/CANCELLED;
  Payment_Status UNPAID|PAID|CHARGED|REFUNDED) + `Dining_Order_Item` (snapshot) · เมนู `Dining_Menu_Category/Item (Product_ID PK, Is_Dining, Display_Name, Sold_Out_Until, Available_Zones CSV,
  Track_Stock)/Option_Group/Option/Menu_Item_Option` · `Dining_Payment` (1 แถวต่อการจ่ายของ "บิล" session: Method CASH|TRANSFER|ROOM_CHARGE, Status CONFIRMED|PENDING_SLIP|REJECTED|VOID,
  Covers_Order_Ids, Acct_Post_Ref `DNP-{id}`/`RC:{ref}`) · `Dining_Room_Charge_Attempt` · `Dining_Event_Log`. Seeds: mapping `DINING_REVENUE` (ไม่ผูก → PRODUCT_REVENUE), Perm `OPS_DINING`,
  Notification_Rules 4 เหตุการณ์ (Telegram เปิด), OmniChannel `DINING`, System_Config `Feature_Dining=0` + `Dining_Session_Idle_Minutes` (30) / `Dining_Min_Order` (0) / `Dining_Guest_Poll_Sec` (5) /
  `Dining_Staff_Poll_Sec` (3) / `Dining_Room_Charge_Max_Fail` (5). **ไม่ seed Sales_Doc_Policy แถว DINING** → ค่ากลาง (*,CASH)=DAILY_ROLLUP ใบสรุป `DNDAY-{yyyyMMdd}-{paidHowId}`, (*,*)=IMMEDIATE `DNP-{id}`.
- **โค้ด `Class/Dining/*` (namespace `Take_Time_BangPhra.Dining`):** pure = `DiningModels` (POCO + `DiningStatus.*`/`DiningError`), `DiningPricing` (server คิดราคาเสมอ), `DiningTokens`, `DiningNotifyText`
  (mono test `tools/dining-tests/run.sh` 136 กรณี) · `DiningConfig` · `DiningDb` (internal, guard OBJECT_ID) · `DiningTableService` (โซน/โต๊ะ CRUD, `BulkCreateTables`, `RotateTableToken/RotateZoneTokens`,
  `ResolveTable`, session, `RequestBill`, `CloseSession`, `AutoCloseIdleSessions`, `GetBoard`, `SchemaReady`) · `DiningMenuService` (cache 60 วิ, admin CRUD ล้าง cache เอง) · `DiningOrderService`
  (Quote/PlaceOrder idempotent/UpdateStatus/CallStaff) · `DiningPaymentService` (RecordCash/SubmitTransferSlip/Approve-RejectPendingSlip/ChargeToRoom) · `DiningPostingService.PostIfDue` (timer) ·
  `DiningNotifier` (Telegram+ในระบบ+แชทรวม — service ยิงเอง หน้าไม่ต้องเรียก) · `DiningQr` (`BuildOrderUrl` = `{SiteBase}/Order/{token}`, `RenderSvg` ZXing.BarcodeWriterSvg ฝั่ง server ไม่ส่ง token ออกนอก;
  ไม่มี zxing.dll → หน้าพิมพ์ fallback qrcodejs CDN).
- **หน้า:** ลูกค้า (ทีม A) `Order/Default.aspx` (เมนู+ตะกร้า) / `Order/Status.aspx` (สถานะ/เช็คบิล/จ่าย) / `API/DiningOrder.ashx` (`X-Dining: 1` + same-origin + rate limit) / `Content/dining.css`
  / `Scripts/dining.js` / route `Order/{token}` · พนักงาน (ทีม B) `Admin/Dining/Orders.aspx` (กระดาน+เสียง+poll, `?o=`) / `Admin/Dining/Tables.aspx` (สถานะโต๊ะสด, ปิดโต๊ะ) / `Admin/NotificationCheck`
  `action=dining` / badge ใน `Site.Master` / timer `Global.asax` (`PostIfDue` + `AutoCloseIdleSessions`) — สิทธิ์ `Perm.OpsDining` + `Feature.Guard("Dining")` ·
  **แอดมิน (ทีม C):** `Admin/Dining/Zones.aspx` (โซน/โต๊ะ CRUD, สร้างหลายโต๊ะ A1–A20, หมุน QR รายโต๊ะ/ทั้งโซน confirm, ปิดใช้, ลิงก์+คัดลอก+QR ย่อต่อโต๊ะ; แท็บ "ตั้งค่า & เช็กลิสต์" `?focus=settings`:
  migration/ฟีเจอร์/โซน/โต๊ะ/เมนู/ตรวจสลิป/บัญชีรับโอน/zxing/นโยบายเอกสารขาย + สวิตช์ `Feature_Dining` + ค่า `Dining_*` + ทดสอบ Telegram + การ์ดลิงก์ SalesDocPolicy/SlipCheckSettings/
  PaymentChannels/Notifications/AccountingIntegration?focus=mapping&key=DINING_REVENUE) · `Admin/Dining/Menu.aspx` (แท็บ รายการ/หมวด/กลุ่มตัวเลือก: เลือก Product เข้าเมนู, ชื่อแสดง TH/EN,
  sold-out + "หมดวันนี้"/"หมดถึงเวลา", เตรียมกี่นาที, แพ้อาหาร, โซนที่ขาย, Track_Stock, แนะนำ, ผูกกลุ่มตัวเลือก; `?pid=` `?show=`; รูป = ลิงก์ `Admin/ProductImages`) · `Admin/Dining/QrSheet.aspx`
  (`?zone=|?table=`, `?layout=grid8` A4 2×4 | `grid18` 3×6 | `tent` ป้ายตั้งโต๊ะ 10×15 ซม.; การ์ด = โลโก้ `~/Images/Logo.png` + `Business_Info.Company_Name` + "สแกนสั่งอาหาร / Scan to order" + SVG QR +
  รหัสโต๊ะ + โซน + คำแนะนำไทย/EN + ติดต่อ `SlipVerificationService.CustomerContactText`; `@media print` ซ่อนทุกอย่างนอก `#qsPrintRoot`) · การ์ดในศูนย์ตั้งค่า (หมวด "บริการในที่พัก") ·
  `Content/dining-admin.css`. หน้าตั้งค่าใช้สิทธิ์ `Perm.SvcGuest` และ **ไม่ guard ฟีเจอร์** (ต้องตั้งค่าได้ก่อนเปิด). ปุ่มในตาราง = `dnAct(act,id,confirm)` → hidden field + ปุ่มซ่อน `btnAct` (ไม่มี AJAX);
  encode ทุกอย่างผ่าน `H()`, confirm ผ่าน `JsStr`.
- **แจ้งเตือน:** `Notify.Ev.OrderDining` (ออเดอร์ใหม่, HIGH) / `OrderDiningStatus` (รับ-ทำ-เสิร์ฟ-ยกเลิก, สลิป, รับเงินสด) / `DiningCallStaff` (urgent) / `DiningBill` (เช็คบิล/ลงบิลห้อง/ปิดโต๊ะ) +
  `NotifyAllStaff(…,"DINING_ORDER")` + แชทรวมช่องทาง `DINING` 1 conversation ต่อโต๊ะ (`table_{id}`, ปิดโต๊ะ = RESOLVED). ลิงก์ในข้อความ → `Admin/Dining/Orders?o={orderId}`.
- **บัญชี:** `Dining_Payment` CASH/TRANSFER CONFIRMED → `ChannelSalesPostingService.Record(Channel="DINING", SourceKey="DNP-{id}")` ตามนโยบายเอกสารขาย; รายการ `Track_Stock=1` → `Product_Out`
  (Remark `ร้านอาหาร`) + `EnqueueStockOutCogs("DNCOGS-{orderId}-{pid}")`; ลงบิลห้อง = `RoomChargeService.ChargeToRoom` ไปกับใบเช็คเอาท์ (ไม่โพสต์ซ้ำ). ยกเลิกหลังลงบัญชี = ใบลดหนี้ (ข้อ 17).
  รายงานภาษีเห็นช่องทาง `DINING` อัตโนมัติ (`TaxReportKit.ChDining`).
- **ข้อจำกัด/ยังไม่ทำ:** ต้อง build+test บน Windows (Linux = compile-check + mono test + Playwright mock) · ไม่มีจ่ายผ่านเกตเวย์ (`PaymentSource.Dining` จองไว้) · ไม่มีแยกบิล/ย้ายโต๊ะ ·
  ไม่มี kitchen ticket/ใบเสร็จอย่างย่อให้ลูกค้า (ม.86/6 — ดู `Sales_Doc_Local_Slip_Required`) · ตอบแชทกลับถึงมือถือลูกค้า/รายงานยอดขายร้านอาหาร · ตรวจสลิปต้องมี PHASE19_34/36 + `SlipVerify_Enabled`
  ไม่งั้นสลิปทุกใบรอพนักงาน · หน้าตั้งค่าใช้ postback ล้วน (ไม่มี JS framework) — ตารางยาวเลื่อนในการ์ด.

## Queue resilience (ส.ค. 2026 — หลังเคส NextAcc ล่มทั้งแอป)

NextAcc เคย start ไม่ขึ้น (DI circular dependency ใน `Program.cs`) → ทุก endpoint ตอบ HTTP 500
เป็น **หน้า HTML developer-exception page**. สิ่งที่พังตามมาฝั่ง TakeTime และวิธีที่แก้ไว้แล้ว:

| อาการ | สาเหตุจริง | สิ่งที่ทำ |
|-------|-----------|----------|
| ทุกรายการในคิวตกเป็น FAILED ถาวร | 5xx ถูกโยนเป็น `Exception` ธรรมดา คิวจึงนับเป็น "ข้อมูลผิด" แล้วเผา Retry_Count 5/5 | `ServerUnavailableException` + `AccountingApiClient.LooksLikeServerDown` → คิวคง **PENDING ไม่นับ retry** แล้วหยุดรอบ (เหมือน DNS/Auth) |
| retry storm | 5 attempts × ทุกรายการ ทั้งที่ปลายทางล่ม | fail-fast บน 5xx+HTML + **server-down cooldown** (`Nexaacc_ServerDown_Cooldown_Min`, default 5 นาที) กันไว้ที่ `IsApiReachable` — call สำเร็จหนึ่งครั้ง `ClearServerDown()` เอง; ปุ่ม Test/Retry/Process now ล้าง cooldown ให้ก่อนเสมอ |
| เสี่ยงเอกสารซ้ำใน NextAcc | (ก) orphan cleanup วัดจาก `Created_Date` ไม่ใช่เวลาที่เริ่มยิง → ดึงรายการที่กำลังยิงอยู่กลับเป็น PENDING (ข) `_isSyncing` เป็น in-process + `task.Wait(2 min)` ทิ้ง task แล้วปลดล็อก (ค) ไม่มี guard ข้าม process | คอลัมน์ **`Processing_Started`** (PHASE18_31) + `Nexaacc_Stuck_Processing_Min` (15) / **atomic claim** `TryClaimQueueItem` (UPDATE…WHERE Status IN (PENDING,FAILED) + `@@ROWCOUNT`) / **`sp_getapplock`** ครอบทั้งรอบ (`ProcessQueueAsync` → `ProcessQueueCoreAsync`) / Global.asax ไม่ปลด `_isSyncing` เมื่องานยังวิ่ง + watchdog 30 นาที |
| ปุ่ม Log ในหน้าคิวค้าง | `LogDetail LIKE '%…%'` ไม่จำกัดช่วงเวลา = scan ทั้งตาราง Logs | จำกัดช่วงเวลาตามอายุคิว + `CommandTimeout` 20 วิ + AbortController 40 วิ + index (PHASE18_30) |
| DB โต/ช้า | `Accounting_Sync_Log` เก็บ req+res ทุก call ไม่มีวันลบ; Logs โตไม่จำกัด | `PurgeOldSyncLogsIfDue` (วันละครั้ง, `Nexaacc_SyncLog_Retention_Days` 90) + `LogThrottled` (log ซ้ำ 1 ครั้ง/30 นาที) |
| ไม่มีใครรู้ว่าคิวพัง | ต้องเปิดหน้าคิวดูเอง | `NotifyQueueHealthIfNeeded` → Telegram (`Nexaacc_Queue_Alert`, ทุก `Nexaacc_Queue_Alert_Hours` 6 ชม.) |
| รู้ว่า mapping ผิดตอน sync ไม่ผ่านแล้ว (`ไม่พบผังบัญชี: 21240`) | ไม่มี pre-flight | ปุ่ม **🩺 ตรวจสุขภาพการเชื่อมต่อ** (`action=healthCheck`) เทียบ `Accounting_Account_Mapping` กับ `Accounting_Nexaacc_Accounts` + แหล่งเงินที่ยังไม่ผูก + คิวตายค้าง + ชนิดคีย์ |

`BuildApiErrorHint` เพิ่ม hint: `ไม่พบผังบัญชี: <code>` (สร้างบัญชี/แก้ mapping/ปิด GR/IR) และ NextAcc-ล่ม
Migrations: **PHASE18_30** (index Logs) และ **PHASE18_31** (`Processing_Started` + config + คืนคิวที่ล้มเพราะ NextAcc ล่ม)

## OTA collect mode + ReservationBalance (ก.ย. 2026)
`Class/ReservationBalance.cs` = กฎกลางยอดค้าง/รับแล้ว (บอร์ด, รายการจอง, เช็คอิน, เช็คเอาท์, Telegram). โหมดเก็บเงินอ่านจาก
คอลัมน์ `Reservation.OTA_Collect_Mode` (CHANNEL|HOTEL|UNKNOWN) + `OTA_Collect_Source` (EMAIL|GUESS|STAFF|BACKFILL)
(migration **PHASE19_17**, DryRun=1 default, ต้อง recycle app หลังรัน) ก่อน fallback หมายเหตุ. CHANNEL+GUESS = UNKNOWN
(intake เขียน Deposit=0; ไม่นับ Deposit) — UNKNOWN ที่ไม่ได้มาจากการเดา นับ Deposit เป็นมัดจำจริง. เปลี่ยนโหมดผ่าน
`SetCollectMode` เท่านั้น (log `Reservation_Collect_Mode_Log`, ล็อกเมื่อ `Ota_Revenue_Ref` โพสต์แล้ว/ใบเสร็จ sync แล้ว).
เช็คอิน: Due=0 → ข้ามรับเงิน (ไม่มีแถวเงินสดปลอม), UNKNOWN → บังคับเลือก (CHANNEL = Owner/Admin). ⇒ รายได้ค่าห้อง
Channel Collect ต้องมาจาก `RevenuePostingService.PostOtaRoomRevenueIfDue` (`Nexaacc_OtaRoomRevenue`, ฐาน
`Nexaacc_OtaRevenue_Basis` NET|GROSS) — ต้องเปิดพร้อม deploy หลังผู้ทำบัญชีตรวจ dry-run. **ยอดที่ OTA ครอบคลุม (CHANNEL):**
`OtaCoverageAmount` = `OTA_Gross_Amount` เมื่อ |TotalPrice − Gross| ≤ จำนวนคืน (ราคาห้องตั้งจาก gross — ใบเก่า/REFSELL) ไม่งั้น
`OtaAmount` (net-first, คงความหมายเดิมเพราะ RevenuePostingService เทียบกับ net) ⇒ ค่าคอมฯ OTA ไม่กลายเป็นยอดค้างตอนเช็คเอาท์.
สถานะที่ถือห้องใน `EmailReservationService.FreeStatusFilter` / `Default.aspx` ใช้ `RescheduleService.SqlHoldsRoom` แล้ว. แถวเงินสดปลอมเก่า: หน้า
`Payment/PaymentHistory.aspx` แท็บ "⚠ เงินสดของใบ OTA" → Status `OTA_RECLASS` / คิว `OTA_CASH_RECLASS`
(JE Dr OTA_RECEIVABLE / Cr เงินสด, ref `OTA-RECLASS-{receiptId}`, ตั้ง `Ota_Revenue_Ref=RECLASS-…`).

## Sales & stock document policy (ต.ค. 2026)
ทุกตาราง/คอลัมน์ใหม่ guarded (OBJECT_ID/COL_LENGTH) — ยังไม่รัน migration = พฤติกรรมเดิม.

**นโยบายเอกสารขาย (PHASE19_29, `Class/Integration/SalesDocumentPolicy.cs`):** ตาราง `Sales_Doc_Policy(Sales_Channel, Channel_Type, Doc_Mode,
Is_Enabled, …)` UNIQUE(ช่องทาง,ชนิด). ช่องทาง POS|ROOM_SERVICE|ACTIVITY|AMENITY|FRONTDESK_GATEWAY|DAMAGE|RESERVATION|`*` · ชนิดเงิน
(`Account_Paid_How.Channel_Type`) CASH|TRANSFER|QR|CARD|GATEWAY_OTHER|DIRECTOR_LOAN|OTA|OTHER|`*` · โหมด IMMEDIATE|DAILY_ROLLUP|OFF.
Seed: (`*`,CASH)=DAILY_ROLLUP, (`*`,`*`)=IMMEDIATE, (RESERVATION,`*`)=IMMEDIATE. ลำดับ `Resolve(conn, channel, paidHowId)`:
`Account_Paid_How.Sales_Doc_Mode` (override รายแหล่งเงิน) → (ช่องทาง,ชนิด) → (ช่องทาง,`*`) → (`*`,ชนิด) → (`*`,`*`) → IMMEDIATE.
Config: `Sales_Doc_Policy_Enabled` (ไม่มี = '0' เดิม), `Sales_Doc_Local_Slip_Required` (รวบรายวัน ⇒ POS ต้องพิมพ์สลิปอย่างย่อรายใบ),
`Sales_Doc_Send_Local_Invoice_Email` ('0' = ไม่ส่ง PDF "[INV]" ของ TakeTime เมื่อเอกสาร NextAcc เป็นตัวจริง).
เลขใบสรุป: `POSDAY-{yyyyMMdd}-{paidHowId}[-S{n}]`, `RSDAY-…`, ทั่วไป `{CHANNEL}DAY-…` — `-S{n}` = ใบเสริมของแถวมาช้า (แทน mark-and-skip เดิมที่ทำรายได้หาย).
แอดมิน: `Admin/Settings/SalesDocPolicy.aspx`.

**เหตุผลจ่ายออกสต๊อก (PHASE19_30, `Class/Inventory/StockOutReasonCatalog.cs`):** `Stock_Out_Reason(Code, Name_TH, Gl_Mapping_Key, Deemed_Sale_Vat,
Vat_Base MARKET|COST, Requires_Approval, Requires_Evidence, Is_Active, Sort_Order, Help_TH)` + `Product_Out` เพิ่ม `Out_Reason_Code, Recorded_By,
Approved_By/At, Approval_Status PENDING|APPROVED|REJECTED, Evidence_Path, Nexaacc_Journal_Ref, Is_Reversed`. แยก แตก/หมดอายุ/สูญหาย/
เบิกใช้ (แม่บ้าน/ครัว)/อาหารพนักงาน/เจ้าของใช้/ของแจก/รับรอง/คืนผู้ขาย ไปบัญชีของตัวเอง (เดิมทุกอย่าง Dr 51200 "เสียหาย"). แอดมิน `Admin/Settings/StockOutReasons.aspx`.

**ซื้อ/รับของ (PHASE19_31):** `Product_In` เพิ่ม `Supplier_Name/TaxId/Doc_No/Doc_Date, Vat_Amount, Payment_Status UNPAID|PAID|CREDIT,
Linked_Voucher_Id, Receipt_Batch`; `Account_Payment.Linked_Goods_Receipt` (= Receipt_Batch) ⇒ ใบสำคัญจ่ายของที่รับเข้าแล้วลง **Dr GRNI 21240**
ไม่ใช่ค่าใช้จ่าย (กันต้นทุนซ้ำ Product/In + PV). ซื้อเชื่อ `Account/CreditPurchasePayment.aspx`; ต้นทุนถัวเฉลี่ย `Class/Inventory/CostService.cs`.

**รายงาน (อ่านอย่างเดียว, `Class/Reports/TaxReportKit.cs`, สิทธิ์ Owner / Admin+FIN_REPORT|SYS_ACCOUNTING / กลุ่มที่ให้ FIN_REPORT|SYS_ACCOUNTING):**
`Report/DailySalesTaxSummary` (วัน×ช่องทาง×แหล่งเงิน: จำนวน/ยอด/VAT/เอกสาร NextAcc รายใบหรือใบสรุป + ธง ไม่มีเอกสาร/มาช้า/โอนยังไม่ตรวจ/ไม่ตรงนโยบาย
— ใช้เป็นบันทึกสรุปยอดขายเงินสดรายวันที่รองรับใบสรุป), `Report/StockMovementReport` (ม.87(3): ยกมา/รับ/ขาย/จ่ายออกตามเหตุผล/คงเหลือ ราคาทุน
+ บัญชีคุมรายวัน), `Report/NextAccReconciliation` (รายได้/VAT/COGS/ตัดจำหน่าย TakeTime vs คิวที่ส่งสำเร็จ + คิวล้ม/ค้าง). เมนู การเงิน & บัญชี + การ์ดศูนย์ตั้งค่า.

**ข้อกฎหมาย (สรุปจากงานค้นคว้า ไม่ใช่ความเห็นที่ลงนาม — ให้ผู้ทำบัญชียืนยัน):**
- จด VAT ต้องออกใบกำกับทุกครั้งที่ความรับผิดเกิด (ม.86) ไม่ว่าเงินสด/โอน · โรงแรม/ร้านอาหาร = ค้าปลีก ⇒ ออก **อย่างย่อ** ได้ (ม.86/6); ผู้ซื้อจด VAT ขอ ⇒ เต็มรูป (ม.86/4).
  ข้อยกเว้น ≤1,000 บาทไม่ต้องออก ใช้เฉพาะรายย่อย (ประกาศ ฉ.154) — รีสอร์ตนี้น่าจะไม่เข้า.
- **ใบสรุปรายวัน (POSDAY ฯลฯ) = เอกสารประกอบการลงบัญชีเท่านั้น ไม่ใช่ใบกำกับของลูกค้า** — ทุกการขายเงินสดยังต้องมีใบอย่างย่อรายใบที่หน้าร้าน
  (เครื่องที่ได้ ภ.พ.06 / e-Tax ABB / ใบเขียนมือเลขเรียง) แล้วลงรายงานภาษีขายเป็นยอดรวมต่อวันอ้างเลขแรก–สุดท้ายได้. ไม่มีใบให้ลูกค้าเลย = เสี่ยง ม.86
  (เบี้ยปรับ ม.89). รวมหลายลูกค้าเป็นใบกำกับใบเดียวไม่ได้ (ป.86/2542 ข้อ 1 ใช้กับผู้ซื้อรายเดียว — เช่น folio ห้องพัก).
- ภ.พ.06 = อนุมัติ **เครื่องบันทึกการเก็บเงิน**; ใบอย่างย่อพิมพ์จากซอฟต์แวร์หลังบ้านเป็นจุดเทา (ถามสรรพากรชลบุรี).
- บริการ (ห้อง/อาหารเสิร์ฟ/กิจกรรม) tax point = รับชำระ (ม.78/1(1)) ⇒ มัดจำค่าห้อง VAT เกิดทันที (โหมด RECEIPT+no-defer ตรงสุด; defer เสี่ยง ม.89/1);
  ขายสินค้า = ส่งมอบ (ม.78). Service charge อยู่ในฐาน VAT; ทิปผ่านบิล = เจ้าหนี้พนักงาน.
- แตก/หมดอายุ **ทำลายมีหลักฐาน** (ป.79/2541 แก้ ป.84/2542: รายงาน ผู้อนุมัติ พยาน ภาพ) ไม่ใช่การขาย ไม่ต้องกลับภาษีซื้อ; **สูญหาย/ถูกขโมย/ขาดจากการนับ
  = สินค้าขาดจากรายงาน ⇒ ถือเป็นขาย ม.77/1(8)(จ) เสีย VAT ราคาตลาด** (กค 0706/พ./6444). เจ้าของใช้เอง = ขาย (ม.77/1(8)(ง)) + รายจ่ายต้องห้าม;
  อาหารพนักงานตามระเบียบ/ของแถมพร้อมการขาย (ระบุในใบกำกับ)/ตัวอย่าง-ของแจก = ไม่รวมฐาน (ประกาศ ฉ.40); ใช้สต๊อกในการให้บริการห้องพักเอง = ไม่ใช่การขาย.
- ภาษีซื้อเคลมได้เฉพาะใบกำกับเต็มรูป ม.86/4 (อย่างย่อ/ใบเสร็จธรรมดา = ต้องห้าม ม.82/5(3)); ผู้ขายออกใบเสร็จไม่ได้ ⇒ ใบรับรองแทนใบเสร็จ (type 15).
- รายงานสินค้าและวัตถุดิบ ม.87(3) ต้องจัดทำสำหรับสินค้าที่มีไว้ขาย — NextAcc ไม่มีตัวสร้าง (คำขอ ข้อ ฎ.) ⇒ ใช้ `Report/StockMovementReport`.

## Booking policies & confirmation page (ต.ค. 2026)
นโยบายการจอง = `Class/BookingPolicy.cs` (ตาราง `Booking_Policy_Config`, cache 60 วิ, ไม่มีตาราง = ข้อความสำรอง; `ToHtml` encode เสมอ) — แก้ที่
`Admin/Settings/BookingPolicies.aspx` · หน้าสาธารณะ `Policy/*.aspx` (`PolicyPage.cs` + `PolicyView.ascx`) · หน้าจอง `Reserve.aspx`
`SetupPolicySection` แสดงนโยบายการยกเลิก + modal 4 ข้อ + ติ๊กยอมรับ (`RecordAcceptance` → `Reservation.Policy_Accepted_*`).
**หน้ายืนยันการจอง `Reservation_Confirmed.aspx` (`?id=&check=เบอร์`):** ลำดับ สถานะ/ขั้นตอนถัดไป → รายละเอียด (ผู้จอง/วันเข้าพัก/ห้อง/
ของเช่า/สัตว์เลี้ยง/หมายเหตุ) → ชำระเงิน + สลิป/เอกสาร → **นโยบายการยกเลิกการจอง** (`pnlCancelPolicy`, ข้อความเดียวกับหน้าจอง ผ่าน
`Class/PolicySummary.cs` pure: บรรทัด = ข้อ, ตัดเครื่องหมายนำหน้า, 4 ข้อแรกแสดงเสมอ ที่เหลือ `<details>` "อ่านเพิ่มเติม", เน้นตัวเลข วัน/%/บาท,
ว่าง = ซ่อน; ลิงก์ /Policy/Cancellation + /Policy/Refund) → นโยบายอื่นพับ (`pnlPolicies`, ไม่ติดรูป) → เช็คอิน/ติดต่อ → ปุ่ม (นอกกรอบจับภาพ, มือถือ
sticky ล่าง). CSS `Content/reservation-confirmed.css` (mobile-first; ≥1024 สองคอลัมน์ การจองซ้าย / ชำระเงิน+นโยบายขวา; print). **รูปยืนยัน** =
html2canvas (CDN) จับ `#rcCapture` ฝั่งเบราว์เซอร์ (ไม่มี System.Drawing): ใส่ `body.rc-capturing` (ซ่อน `.rc-no-capture`, ปลด max-height
ของกล่องเลื่อน) + เปิด details ของนโยบายให้ติดรูปครบ แล้วคืนสถานะ; ชื่อไฟล์ `การยืนยันการจอง_{id}.png`. ปุ่ม "เพิ่มลงปฏิทิน" = .ics ฝั่งเบราว์เซอร์
(`lnkCalendar` data-* วันที่ InvariantCulture, DTEND exclusive = เช็คเอาท์+1). อีเมล/LINE/Telegram ยืนยันการจองยังไม่มีนโยบาย (ทำแยก).
ทดสอบ: mono `PolicySummary` + Playwright mock (ไม่มี Windows). ต้อง build+test บน Windows.

## Git / workflow
Feature branch: `claude/vibrant-davinci-nzwlgq` (based on default branch
`claude/restructure-system-architecture-szeM0`). Remote: `Ipsos-Dev-TH/TakeTime`
(NOT `Wachira-d/TakeTime` — that is the dev's fork). PR #1 open against the default branch.
