# TakeTime x Nexaacc Accounting Integration Plan

## Overview

แผนพัฒนาระบบเชื่อมต่อ TakeTime (Hotel Management System) เข้ากับ Nexaacc Accounting System
เพื่อยิงข้อมูลการจอง ใบสำคัญจ่าย การจัดการสินค้า และธุรกรรมบัญชีทั้งหมดอัตโนมัติผ่าน REST API

**Source System:** TakeTime (ASP.NET Web Forms, .NET Framework, SQL Server)
**Target System:** Nexaacc Accounting (ASP.NET Core 8.0, SQL Server, JWT Auth, REST API)
**Repository:** https://github.com/Wachira-d/Accounting

---

## 1. System Architecture

```
┌─────────────────────────────────────────────────────┐
│                    TakeTime System                    │
│                                                       │
│  ┌──────────┐  ┌──────────┐  ┌───────────┐          │
│  │ Booking   │  │ Payment  │  │ Product   │          │
│  │ Service   │  │ Service  │  │ Service   │          │
│  └─────┬─────┘  └─────┬────┘  └─────┬─────┘          │
│        │              │              │                │
│        ▼              ▼              ▼                │
│  ┌────────────────────────────────────────────┐      │
│  │         AccountingSyncService               │      │
│  │  (Event Listener + Queue + Retry Logic)     │      │
│  └──────────────────┬─────────────────────────┘      │
│                     │                                 │
│  ┌──────────────────▼─────────────────────────┐      │
│  │         AccountingDataMapper                │      │
│  │  (TakeTime Models → Nexaacc DTOs)           │      │
│  └──────────────────┬─────────────────────────┘      │
│                     │                                 │
│  ┌──────────────────▼─────────────────────────┐      │
│  │         AccountingApiClient                 │      │
│  │  (HTTP Client + Auth + Error Handling)       │      │
│  └──────────────────┬─────────────────────────┘      │
│                     │                                 │
└─────────────────────┼───────────────────────────────┘
                      │ HTTPS (REST API)
                      ▼
┌─────────────────────────────────────────────────────┐
│              Nexaacc Accounting System                │
│                                                       │
│  POST /api/auth/login                          → JWT Token    │
│  POST /api/companies/{id}/accounting/journals  → Journal Entry│
│  POST /api/companies/{id}/accounting/accounts  → Chart of Acc │
│  POST /api/companies/{id}/document             → Invoice/Rcpt │
│  POST /api/companies/{id}/document/payments    → Payment      │
│  POST /api/companies/{id}/document/contacts    → Contact      │
│  POST /api/companies/{id}/product              → Product Sync │
│  POST /api/companies/{id}/product/stock/adjust → Stock Adjust │
│                                                       │
└─────────────────────────────────────────────────────┘
```

---

## 2. Integration Points (จุดเชื่อมต่อ)

### 2.1 การจอง (Reservation) → บันทึกบัญชีรายรับ

| Event ใน TakeTime | Action ใน Nexaacc | Journal Type | บัญชี Debit | บัญชี Credit |
|---|---|---|---|---|
| สร้างการจอง + รับมัดจำ | Create Journal (CashReceipts) | CashReceipts | 1110 เงินสด/ธนาคาร | 2130 เงินรับล่วงหน้า |
| ชำระเงินเต็มจำนวน | Create Journal (CashReceipts) | CashReceipts | 1110 เงินสด/ธนาคาร | 4100 รายได้ค่าห้อง |
| Checkout สมบูรณ์ | Create Journal (Sales) + Recognize Revenue | Sales | 2130 เงินรับล่วงหน้า | 4100 รายได้ค่าห้อง |
| ยกเลิกการจอง + คืนเงิน | Create Journal (CashPayments) | CashPayments | 2130 เงินรับล่วงหน้า | 1110 เงินสด/ธนาคาร |

**Trigger Points:**
- `Reserve.aspx.cs` → หลังบันทึกการจองสำเร็จ
- `MakePayment.aspx.cs` → หลังบันทึกการชำระเงินสำเร็จ
- `Checkout.aspx.cs` → หลัง checkout สำเร็จ
- `ReservationService.cs` → หลังยกเลิกการจอง

### 2.2 ใบสำคัญจ่าย (Payment Voucher) → บันทึกบัญชีรายจ่าย

| Event ใน TakeTime | Action ใน Nexaacc | Journal Type | บัญชี Debit | บัญชี Credit |
|---|---|---|---|---|
| สร้างใบสำคัญจ่าย | Create Journal (CashPayments) | CashPayments | 5xxx ค่าใช้จ่ายตามประเภท | 1110 เงินสด/ธนาคาร |
| อนุมัติใบสำคัญจ่าย | Post Journal Entry | - | (Post draft → Posted) | - |
| ยกเลิกใบสำคัญจ่าย | Void Journal Entry | - | (Void posted entry) | - |

**Trigger Points:**
- `Voucher/Default.aspx.cs` → หลังสร้าง voucher
- `Account/PaymentVoucher.aspx.cs` → หลังอนุมัติ/ยกเลิก

### 2.3 การจัดการสินค้า (Product/Stock) → Sync สินค้าและบันทึกบัญชี

| Event ใน TakeTime | Action ใน Nexaacc | Journal Type | บัญชี Debit | บัญชี Credit |
|---|---|---|---|---|
| เพิ่มสินค้าใหม่ | Create Product | - | - | - |
| รับสินค้าเข้า (Stock In) | Stock Adjust + Journal (Purchase) | Purchase | 1150 สินค้าคงเหลือ | 2110 เจ้าหนี้การค้า |
| ขายสินค้า/Charge to Room | Journal (Sales) | Sales | 1120 ลูกหนี้การค้า | 4200 รายได้ขายสินค้า |
| ขายสินค้า (COGS) | Journal (General) | General | 5100 ต้นทุนสินค้าขาย | 1150 สินค้าคงเหลือ |

**Trigger Points:**
- `Product/Default.aspx.cs` → เพิ่ม/แก้ไขสินค้า
- `Product/In.aspx.cs` → รับสินค้าเข้าสต็อก
- `RoomChargeService.cs` → charge สินค้าเข้าห้อง

### 2.4 ใบเสร็จรับเงิน (Receipt) → เอกสารบัญชี

| Event ใน TakeTime | Action ใน Nexaacc | Document Type |
|---|---|---|
| ออกใบเสร็จ | Create Document (Receipt) | Receipt |
| ออกใบกำกับภาษี | Create Document (TaxInvoice) | TaxInvoice |
| ออกใบลดหนี้ | Create Document (CreditNote) | CreditNote |
| ยกเลิกใบเสร็จ | Void Document | - |

**Trigger Points:**
- `ReceiptService.cs` → หลังสร้างใบเสร็จ
- `AccountingService.cs` → หลังสร้าง Credit Note
- `TaxInvoiceAPI.ashx.cs` → หลังสร้างใบกำกับภาษี

### 2.5 ลูกค้า (Contact) → Sync ข้อมูลลูกค้า

| Event ใน TakeTime | Action ใน Nexaacc |
|---|---|
| ลูกค้าใหม่จากการจอง | Create Contact (isCustomer=true) |
| อัพเดตข้อมูลลูกค้า | Update Contact |

### 2.6 เงินเดือน (Payroll) → บันทึกบัญชี

| Event ใน TakeTime | Action ใน Nexaacc | Journal Type | บัญชี Debit | บัญชี Credit |
|---|---|---|---|---|
| จ่ายเงินเดือน | Create Journal (CashPayments) | CashPayments | 5200 เงินเดือนและค่าแรง | 1110 เงินสด/ธนาคาร |

---

## 3. Chart of Accounts Mapping (ผังบัญชีที่เกี่ยวข้อง)

```
หมวด 1 - สินทรัพย์ (Assets)
├── 1110  เงินสด (Cash)
├── 1111  เงินฝากธนาคาร - KBANK
├── 1112  เงินฝากธนาคาร - KTB
├── 1120  ลูกหนี้การค้า (Accounts Receivable)
├── 1121  ลูกหนี้ค่าห้องพัก (Room AR)
├── 1150  สินค้าคงเหลือ (Inventory)
└── 1160  ภาษีซื้อ (Input VAT)

หมวด 2 - หนี้สิน (Liabilities)
├── 2110  เจ้าหนี้การค้า (Accounts Payable)
├── 2130  เงินรับล่วงหน้า - มัดจำ (Advance Deposits)
├── 2140  ภาษีขาย (Output VAT)
└── 2150  ภาษีหัก ณ ที่จ่าย (Withholding Tax Payable)

หมวด 4 - รายได้ (Revenue)
├── 4100  รายได้ค่าห้องพัก (Room Revenue)
├── 4200  รายได้ขายสินค้า (Product Sales Revenue)
├── 4210  รายได้ค่าอาหารและเครื่องดื่ม (F&B Revenue)
├── 4300  รายได้ค่าบริการอื่น (Other Service Revenue)
└── 4900  รายได้อื่น (Other Income)

หมวด 5 - ค่าใช้จ่าย (Expenses)
├── 5100  ต้นทุนสินค้าขาย (COGS)
├── 5200  เงินเดือนและค่าแรง (Salaries & Wages)
├── 5300  ค่าสาธารณูปโภค (Utilities)
├── 5400  ค่าซ่อมแซมบำรุงรักษา (Maintenance)
├── 5500  ค่าวัสดุสิ้นเปลือง (Supplies)
├── 5600  ค่าคอมมิชชั่น OTA (OTA Commission)
└── 5900  ค่าใช้จ่ายอื่น (Other Expenses)
```

---

## 4. Payment Method → Bank Account Mapping

| TakeTime Payment Method | Nexaacc Account Code | Account Name |
|---|---|---|
| CASH | 1110 | เงินสด |
| KBANK | 1111 | เงินฝากธนาคาร - กสิกรไทย |
| KTB | 1112 | เงินฝากธนาคาร - กรุงไทย |
| PROMPTPAY | 1111 | เงินฝากธนาคาร - กสิกรไทย (default) |
| CARD | 1113 | เงินฝากธนาคาร - บัตรเครดิต |
| DIRECTOR | 1114 | เงินทดรองจ่าย - กรรมการ |
| OTHER | 1110 | เงินสด (default) |

---

## 5. Technical Implementation

### 5.1 New Files to Create

```
Take Time BangPhra/
├── Class/
│   ├── Integration/
│   │   ├── AccountingApiClient.cs        ← HTTP client wrapper
│   │   ├── AccountingDataMapper.cs        ← Data transformation
│   │   ├── AccountingSyncService.cs       ← Orchestrator + queue
│   │   ├── AccountingModels.cs            ← DTOs for Nexaacc API
│   │   └── AccountingConfig.cs            ← Configuration model
│   └── (existing files modified)
│
Database/
├── PHASE9_Migration_01_Accounting_Integration.sql  ← Config + queue tables
```

### 5.2 Component Details

#### AccountingApiClient.cs
- JWT authentication (login + token refresh)
- Token caching with auto-refresh before expiry
- HTTP methods: GET, POST, PUT with typed responses
- Retry logic with exponential backoff (3 retries)
- Error logging to System_Logs table
- Timeout: 30 seconds per request

#### AccountingDataMapper.cs
- `MapReservationToJournal()` → CreateJournalEntryRequest
- `MapPaymentToJournal()` → CreateJournalEntryRequest
- `MapVoucherToJournal()` → CreateJournalEntryRequest
- `MapProductToProduct()` → CreateProductRequest
- `MapReceiptToDocument()` → CreateDocumentRequest
- `MapCustomerToContact()` → CreateContactRequest
- `MapRoomChargeToJournal()` → CreateJournalEntryRequest
- `MapCheckoutToJournal()` → CreateJournalEntryRequest

#### AccountingSyncService.cs
- Queue-based processing (database queue table)
- Async fire-and-forget from trigger points
- Retry failed items (max 5 retries, exponential backoff)
- Idempotency check (prevent duplicate entries)
- Batch processing for bulk operations
- Status tracking per sync item

### 5.3 Database Schema (New Tables)

```sql
-- Configuration
CREATE TABLE Accounting_Integration_Config (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    ConfigKey NVARCHAR(100) NOT NULL UNIQUE,
    ConfigValue NVARCHAR(500) NOT NULL,
    Description NVARCHAR(255),
    Updated_Date DATETIME DEFAULT GETDATE()
);

-- Account Mapping
CREATE TABLE Accounting_Account_Mapping (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    TakeTime_Code NVARCHAR(50) NOT NULL,
    TakeTime_Description NVARCHAR(255),
    Nexaacc_AccountCode NVARCHAR(20) NOT NULL,
    Nexaacc_AccountId UNIQUEIDENTIFIER,
    Mapping_Type NVARCHAR(50) NOT NULL, -- PAYMENT_METHOD, REVENUE, EXPENSE, ASSET
    Is_Active BIT DEFAULT 1
);

-- Sync Queue
CREATE TABLE Accounting_Sync_Queue (
    ID BIGINT IDENTITY(1,1) PRIMARY KEY,
    Entity_Type NVARCHAR(50) NOT NULL,      -- RESERVATION, PAYMENT, VOUCHER, PRODUCT, RECEIPT, etc.
    Entity_ID INT NOT NULL,
    Action_Type NVARCHAR(50) NOT NULL,       -- CREATE_JOURNAL, CREATE_DOCUMENT, SYNC_PRODUCT, etc.
    Payload NVARCHAR(MAX),                   -- JSON payload
    Status NVARCHAR(20) DEFAULT 'PENDING',   -- PENDING, PROCESSING, COMPLETED, FAILED, SKIPPED
    Retry_Count INT DEFAULT 0,
    Max_Retries INT DEFAULT 5,
    Error_Message NVARCHAR(MAX),
    Nexaacc_Response_Id NVARCHAR(100),       -- ID returned from Nexaacc
    Created_Date DATETIME DEFAULT GETDATE(),
    Processed_Date DATETIME,
    Next_Retry_Date DATETIME
);

-- Sync Log
CREATE TABLE Accounting_Sync_Log (
    ID BIGINT IDENTITY(1,1) PRIMARY KEY,
    Queue_ID BIGINT REFERENCES Accounting_Sync_Queue(ID),
    Action NVARCHAR(100),
    Request_Payload NVARCHAR(MAX),
    Response_Payload NVARCHAR(MAX),
    HTTP_Status INT,
    Success BIT,
    Duration_Ms INT,
    Created_Date DATETIME DEFAULT GETDATE()
);
```

---

## 6. API Authentication Flow

```
1. TakeTime boots → Load config from Accounting_Integration_Config
2. First API call → POST /api/auth/login { email, password }
3. Receive JWT token (60 min expiry) + refresh token (7 days)
4. Cache token in memory
5. Before each call → Check token expiry
6. If expired → POST /api/auth/refresh { refreshToken }
7. If refresh fails → Re-login
```

---

## 7. Sync Flow (Per Transaction)

```
1. TakeTime event fires (e.g., payment recorded)
2. → AccountingSyncService.EnqueueSync(entityType, entityId, action)
3. → INSERT into Accounting_Sync_Queue (status=PENDING)
4. → Background processor picks up PENDING items
5. → AccountingDataMapper transforms data
6. → AccountingApiClient sends to Nexaacc API
7. → On success: status=COMPLETED, store Nexaacc response ID
8. → On failure: status=FAILED, increment retry, set next_retry_date
9. → Log everything to Accounting_Sync_Log
```

---

## 8. Error Handling Strategy

| Error Type | Action | Retry |
|---|---|---|
| Network timeout | Retry with backoff | Yes (5x) |
| 401 Unauthorized | Refresh token, retry | Yes (1x) |
| 400 Bad Request | Log error, mark FAILED | No |
| 404 Not Found | Log error, mark FAILED | No |
| 409 Conflict | Check idempotency, skip if exists | No |
| 500 Server Error | Retry with backoff | Yes (5x) |
| Validation Error | Log details, mark FAILED for review | No |

---

## 9. Development Phases

### Phase 1: Foundation (สัปดาห์ 1-2)
- [x] วิเคราะห์ระบบ TakeTime และ Nexaacc
- [ ] สร้าง AccountingApiClient.cs (HTTP client + auth)
- [ ] สร้าง AccountingModels.cs (DTOs)
- [ ] สร้าง AccountingConfig.cs
- [ ] สร้าง database migration (config + queue tables)
- [ ] ทดสอบ authentication flow

### Phase 2: Data Mapping (สัปดาห์ 3-4)
- [ ] สร้าง AccountingDataMapper.cs
- [ ] Map Chart of Accounts (seed ผังบัญชี)
- [ ] Map Payment Methods → Bank Accounts
- [ ] Map Reservation → Journal Entry
- [ ] Map Payment → Journal Entry
- [ ] Unit tests สำหรับ mapping

### Phase 3: Core Integration (สัปดาห์ 5-6)
- [ ] สร้าง AccountingSyncService.cs (queue + processor)
- [ ] เชื่อมต่อ trigger point ใน Reserve.aspx.cs
- [ ] เชื่อมต่อ trigger point ใน MakePayment.aspx.cs
- [ ] เชื่อมต่อ trigger point ใน Checkout.aspx.cs
- [ ] ทดสอบ end-to-end flow: จอง → ชำระ → checkout

### Phase 4: Voucher & Document (สัปดาห์ 7-8)
- [ ] เชื่อมต่อ Payment Voucher → Journal Entry
- [ ] เชื่อมต่อ Receipt → Document
- [ ] เชื่อมต่อ Credit Note → Document
- [ ] เชื่อมต่อ Tax Invoice → Document
- [ ] ทดสอบ document flow

### Phase 5: Product & Stock (สัปดาห์ 9-10)
- [ ] Sync Product catalog → Nexaacc Products
- [ ] Stock In → Journal Entry (Purchase)
- [ ] Room Charge → Journal Entry (Sales + COGS)
- [ ] ทดสอบ inventory flow

### Phase 6: Advanced (สัปดาห์ 11-12)
- [ ] Contact/Customer sync
- [ ] Payroll → Journal Entry
- [ ] OTA Commission → Journal Entry
- [ ] Admin dashboard สำหรับ monitor sync status
- [ ] Reconciliation tools
- [ ] Production deployment

---

## 10. Configuration Requirements

```
-- ใส่ค่าเหล่านี้ใน Accounting_Integration_Config
Nexaacc_BaseUrl          = https://accounting.example.com
Nexaacc_Email            = integration@taketime.com
Nexaacc_Password         = (encrypted)
Nexaacc_CompanyId        = (GUID from Nexaacc)
Nexaacc_Enabled          = true
Nexaacc_SyncInterval_Sec = 30
Nexaacc_MaxRetries       = 5
Nexaacc_TimeoutSec       = 30
```

---

## 11. Monitoring & Admin

### Sync Dashboard Queries
```sql
-- สรุปสถานะ sync วันนี้
SELECT Status, COUNT(*) as Count, Entity_Type
FROM Accounting_Sync_Queue
WHERE CAST(Created_Date AS DATE) = CAST(GETDATE() AS DATE)
GROUP BY Status, Entity_Type;

-- รายการที่ fail ต้องตรวจสอบ
SELECT * FROM Accounting_Sync_Queue
WHERE Status = 'FAILED' AND Retry_Count >= Max_Retries
ORDER BY Created_Date DESC;

-- Performance log
SELECT AVG(Duration_Ms), MAX(Duration_Ms), COUNT(*)
FROM Accounting_Sync_Log
WHERE Created_Date >= DATEADD(HOUR, -1, GETDATE());
```

---

## 12. Security Considerations

1. **Credentials:** เก็บ password แบบเข้ารหัสใน DB (ใช้ Code.Crypt() ที่มีอยู่)
2. **Transport:** HTTPS only (TLS 1.2+)
3. **Token:** เก็บ JWT token ใน memory เท่านั้น ไม่เก็บลง DB
4. **Audit:** Log ทุก API call ใน Accounting_Sync_Log
5. **Access:** จำกัดสิทธิ์ admin เท่านั้นที่เข้าถึง config
6. **Rate Limiting:** Nexaacc มี limit 600 req/min → ควบคุมจาก queue processor
