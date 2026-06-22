# CLAUDE.md — TakeTime BangPhra

Hotel/restaurant management system (ASP.NET **WebForms**, .NET Framework 4.7.2, C#).
Main project: `Take Time BangPhra/`. Database scripts: `Database/` (phased migrations).

> Build/test only on Windows + IIS (WebForms cannot build on Linux containers).
> The `code` helper class lives in namespace `Take_Time_BangPhra` (Code.cs). Files
> without a namespace block must use `Take_Time_BangPhra.code` or add the using.

## NextAcc accounting integration

NextAcc = "Nexaacc" accounting backend. **Source of truth repo:** `Wachira-d/Accounting`
(clone to read the real API — it is the authoritative contract, not guesswork).

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

`AccountingConfig.IsIntegrationKey => ApiKey.StartsWith("int_")`. **An `int_` key cannot
call `/api/companies/*`.** Features below that need company endpoints REQUIRE an `acc_` key.
Chart-of-Accounts sync ("ดึง Chart of Accounts") also needs `acc_`.

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
1. **Director-advance credit account (เจ้าหนี้กรรมการ):** ✅ DONE. `AutoRecordPaymentForVoucher`
   now routes to the company `document/payments` endpoint (`CreatePaymentAsync`,
   `OverridePaymentAccountId` + `PayerSignature*`) whenever an override/signature is needed AND an
   `acc_` key is configured; falls back to `/api/integration/payments` (which ignores both) with a
   log line when an `int_` key blocks it. Verified vs Wachira-d/Accounting: `InboundPaymentRequest`
   has neither field; `CreatePaymentRequest` (DocumentController) has both.
2. **Deposit deferred VAT (มัดจำ → 21913 ภาษีขายรอรับรู้) + realize at check-in:** ✅ DONE (opt-in,
   journal-based — no native deposit-doc/ContactId dependency). Config `Deposit_Defer_Output_Vat`:
   when on (RECEIPT mode), deposit receipt CR `OUTPUT_VAT_DEFERRED` (21913) instead of OUTPUT_VAT,
   and checkout reclassifies Dr 21913 / Cr 21911 so VAT only hits ภ.พ.30 at revenue recognition.
   Default off = unchanged; unmapped 21913 → falls back to OUTPUT_VAT. Migration PHASE17_05 seeds
   the config + `OUTPUT_VAT_DEFERRED` mapping; admin toggle on AccountingIntegration page.
3. **OCR-first ใบสำคัญจ่าย flow:** ✅ DONE (page `Voucher/OcrUpload.aspx`, acc_ key required).
   upload→`ocr/upload`(autoCreate=false)→poll `OcrResultResponse`→prefill review (shows OCR
   `SuggestedAccounts` + quality/role/WHT)→user confirm→`ocr/{id}/create-document?targetType=…`
   →`approve` (retries with AcknowledgeWarnings=true on 422). NextAcc auto-creates the Vendor from
   OCR DBD/tax-id. Client: `UploadOcrAsync` / `GetOcrResultAsync` / `CreateDocumentFromOcrAsync` /
   `ApproveDocumentAsync(id, ApproveDocumentRequest)`. (Future: line-level AI suggest endpoints +
   editable line grid; current page submits the OCR-derived doc as-is.)
4. CheckPayment_New: now sorts by DisplayDoc (done). Homepage Google reviews: validates status (done).

## Git / workflow
Feature branch: `claude/vibrant-davinci-nzwlgq` (based on default branch
`claude/restructure-system-architecture-szeM0`). Remote: `Ipsos-Dev-TH/TakeTime`
(NOT `Wachira-d/TakeTime` — that is the dev's fork). PR #1 open against the default branch.
