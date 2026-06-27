> ✅ **สถานะ: NextAcc ทำแล้ว (commit fc6ff90) + TakeTime พร้อมแล้ว.** NextAcc เปิด
> `POST /api/companies/{id}/payroll/runs/import` (Option A) — สร้าง run สถานะ Calculated จากยอดที่ส่ง
> (Recalculate=false), idempotent ด้วย ExternalRunRef, map ด้วย EmployeeExternalId→CitizenId,
> validate `net == gross − หักฝั่งลูกจ้าง`, override SalaryExpenseAccountCode/NetPaymentAccountCode,
> แล้ว approve→pay ออก GL+ภงด.1+สปส.1-10+50ทวิ+payslip จากยอด import. **ฝั่ง TakeTime:** client
> `ImportPayrollRunAsync` + models `PayrollImportRunRequest/PayrollImportLine` + queue action
> `IMPORT_PAYROLL_RUN` (`EnqueuePayrollRunImport`/`ProcessPayrollRunImport`) อ่านยอดจาก `Payroll_Records`
> + mode ใหม่ `Nexaacc_SyncMode_Payroll=DOCUMENT_IMPORT` (เลือกในหน้า Admin). **วิธีใช้: ตั้ง payroll mode =
> DOCUMENT_IMPORT** แล้วสร้างใบสำคัญจ่ายทั้งงวด (GenerateAllVouchersForPeriod) → enqueue import อัตโนมัติ.
> หมายเหตุ map: TakeTime ไม่มีช่อง ProvidentFund/SalaryAdvance แยก, และ "หักลา (LeaveDeduction)" รวมเข้า
> `OtherDeductions` เพื่อให้ validation balance; SSO นายจ้าง = SSO ลูกจ้าง (5% เท่ากัน).

# NextAcc — Payroll "Import / External-Amounts" Endpoint (คำขอจาก TakeTime)

> ผู้ขอ: TakeTime BangPhra • ผู้รับ: ทีม Wachira-d/Accounting (NextAcc)
> เป้าหมาย: ให้ระบบภายนอก (TakeTime) ส่ง **ยอดเงินเดือนที่คำนวณเองต่อพนักงานต่องวด** เข้ามา
> แล้ว NextAcc **ออกเอกสารตามกฎหมายให้ครบ** (GL + ภ.ง.ด.1 + สปส.1-10 + 50 ทวิ + payslip)
> **โดยไม่คำนวณใหม่**

---

## 1. ปัญหา / บริบท

TakeTime คำนวณเงินเดือนเอง ค่า **ผันแปรทุกงวด** (OT, โบนัส, เบี้ยขยัน, หักลากิจ/ขาด, เงินเบิกล่วงหน้า,
ปรับพิเศษ ฯลฯ) แล้วอยากให้ NextAcc เป็นผู้ "ออกเอกสาร" ทั้งหมด

ปัจจุบัน NextAcc payroll (`PayrollController`) มีเส้นทางเดียวคือ:
`POST /runs` → `POST /runs/{id}/calculate` → `approve` → `pay`
โดย **`calculate` คำนวณ server-side จาก employee master + SSO/Tax config ของ NextAcc**
และ **ไม่มี endpoint ให้ override ยอดต่อพนักงานต่องวด** (`items` เป็น template ของรายการ ไม่ใช่ยอดต่อ run;
`runs` ไม่มี line override)

⟹ ตัวเลขที่ NextAcc ออก **ไม่ตรงกับที่ TakeTime คำนวณ** เมื่อมีรายการผันแปร — จึงต้อง fallback ไป
JOURNAL mode (โพสต์ GL เองจากตัวเลขเรา) ซึ่ง **ไม่ได้แบบ ภ.ง.ด.1/สปส/50ทวิ/payslip จาก NextAcc**

## 2. สิ่งที่ขอ

Endpoint ที่รับ **ยอดสำเร็จรูปต่อพนักงาน** แล้ว NextAcc:
1. **ไม่คำนวณใหม่** — ใช้ยอดที่ส่งมาตรง ๆ
2. โพสต์ **GL** จากยอดที่ส่งมา (Dr เงินเดือน/ปกส.นายจ้าง / Cr ปกส.ค้างจ่าย / Cr ภ.ง.ด.1 / Cr เงินสดสุทธิ)
3. สร้างเอกสารตามกฎหมายจากยอดที่ส่งมา: **ภ.ง.ด.1** (รวม WHT), **สปส.1-10** (รวม SSO นายจ้าง+ลูกจ้าง),
   **50 ทวิ** (รายพนักงาน), **payslip** (รายพนักงาน)
4. รองรับ **approve / pay / void** เหมือน run ปกติ

### ทางเลือกการออกแบบ (เสนอ A)

- **A) `POST /api/companies/{companyId}/payroll/runs/import`** — สร้าง run + lines จากยอดที่ส่งมาในคำขอเดียว
  (สถานะออกมาเป็น *Calculated* ทันที ข้าม `calculate`) แล้วค่อย `approve` → `pay` ตามเดิม ✅ แนะนำ
- B) เพิ่ม flag `POST /runs/{id}/calculate?source=external` + `PUT /runs/{id}/lines` (set ยอดราย line)
- C) `CreatePayrollRunRequest` เพิ่ม field `Lines[]` (ถ้ามี → ใช้ตามนั้น, สถานะ Calculated, ไม่ auto-recalc)

## 3. Request (ทางเลือก A)

`POST /api/companies/{companyId}/payroll/runs/import`
Headers: `X-Api-Key` (int_/acc_), `X-Acting-User` (ผู้ทำ → CreatedBy/ลายเซ็น)

```jsonc
{
  "name": "เงินเดือน มิถุนายน 2569",
  "year": 2026,
  "month": 6,
  "payDate": "2026-06-30",
  "periodStart": "2026-06-01",
  "periodEnd": "2026-06-30",
  "externalSystem": "TakeTime",
  "externalRunRef": "PAYRUN-202606",        // idempotency key ฝั่งเรา (กันสร้างซ้ำ)
  "recalculate": false,                       // false = ใช้ยอดที่ส่งมา ห้ามคำนวณใหม่
  "lines": [
    {
      "employeeExternalId": "EMP-1024",       // map กับ employee ที่ sync ไว้ (หรือ citizenId)
      "citizenId": "1234567890123",
      "employeeName": "สมชาย ใจดี",

      // ── รายได้ (ยอดจริงจาก TakeTime) ──
      "baseSalary":        20000.00,
      "overtimePay":        1500.00,
      "allowances":          800.00,
      "commission":            0.00,
      "bonus":              2000.00,
      "otherEarnings":         0.00,
      "grossIncome":       24300.00,          // = ผลรวมรายได้ทั้งหมด

      // ── รายการหัก (ยอดจริง) ──
      "socialSecurityEmployee": 750.00,       // ส่วนลูกจ้าง
      "socialSecurityEmployer": 750.00,       // ส่วนนายจ้าง (สำหรับ สปส + GL)
      "withholdingTax":         310.00,       // ภ.ง.ด.1
      "providentFundEmployee":    0.00,
      "providentFundEmployer":    0.00,
      "salaryAdvance":            0.00,        // หักเงินเบิกล่วงหน้า
      "otherDeductions":          0.00,
      "totalDeductions":       1810.00,

      "netPay":               22490.00,        // = grossIncome − (หักฝั่งลูกจ้าง)

      // ── ตัวเลือก: ผังบัญชี override (ถ้าไม่ส่ง ใช้ default ของ NextAcc) ──
      "salaryExpenseAccountCode": "52110",
      "paymentAccountCode":       "11120",     // บัญชีจ่ายเงินสุทธิ (ธนาคาร)
      "incomeTypeCode":           "01"         // ประเภทเงินได้ ภ.ง.ด.1 (40(1) เงินเดือน)
    }
    // ... พนักงานคนอื่น ...
  ]
}
```

## 4. พฤติกรรมที่ต้องการ (สำคัญสุด)

- **`recalculate: false` ⟹ ห้ามคำนวณ WHT/SSO/net ใหม่** — ใช้ค่าที่ส่งมาเป็น source of truth ทุกฟิลด์
- **Validation:** ตรวจ `grossIncome − totalDeductions(ฝั่งลูกจ้าง) == netPay` (ผิด → 422 พร้อมระบุพนักงาน);
  ตรวจ employee map เจอ (`employeeExternalId`/`citizenId`); ตรวจผังบัญชี code ที่ส่ง resolve ได้
- **GL ตอน approve/pay** (จากยอดที่ส่งมา):
  `Dr เงินเดือน(gross) + Dr ปกส.นายจ้าง / Cr ปกส.ค้างจ่าย(ลูกจ้าง+นายจ้าง) / Cr ภ.ง.ด.1 ค้างจ่าย / Cr เงินสด/ธนาคาร(net)`
- **เอกสารตามกฎหมาย (จากยอดที่ส่งมา):**
  - **ภ.ง.ด.1** — รวม `withholdingTax` ทุกพนักงาน + รายละเอียดราย line
  - **สปส.1-10** — รวม `socialSecurityEmployee + socialSecurityEmployer`
  - **50 ทวิ** — ราย `withholdingTax`/พนักงาน + `incomeTypeCode`
  - **payslip** — ราย line จาก breakdown ที่ส่งมา (ไม่คำนวณใหม่)
- **Idempotency:** `externalRunRef` ซ้ำ → คืน run เดิม (ไม่สร้าง/ไม่โพสต์ซ้ำ)
- **Void:** `POST /runs/{id}/void` กลับ GL + ยกเลิกเอกสารตามกฎหมายให้

## 5. Response (คาดหวัง)

```jsonc
{
  "success": true,
  "data": {
    "id": "<runId>",
    "payrollNumber": "PR-202606-0001",
    "status": "Approved",                  // หรือ Paid ถ้า import+approve+pay รวบ
    "totalGrossSalary": 24300.00,
    "totalWithholdingTax": 310.00,
    "totalSocialSecurityEmployee": 750.00,
    "totalSocialSecurityEmployer": 750.00,
    "totalNetPay": 22490.00,
    "employeeCount": 1,
    "journalEntryId": "<jeId>",
    "documents": { "pnd1Id": "...", "sso1_10Id": "...", "payslipIds": ["..."] }
  }
}
```

## 6. หมายเหตุ map ฝั่ง TakeTime

- ฟิลด์ตรงกับ `PayrollRunLineResponse` ของ NextAcc อยู่แล้ว (BaseSalary/OvertimePay/Allowances/
  Commission/Bonus/GrossIncome/ProvidentFundEmployee/OtherDeductions/TotalDeductions/NetPay) — ขอเพิ่ม
  ฝั่ง **request** ให้รับค่าเหล่านี้เข้าได้ (พร้อม SSO employee/employer + withholdingTax)
- ถ้ามี endpoint นี้ TakeTime จะเลิก fallback JOURNAL แล้วใช้ DOCUMENT path กับยอดของเราได้เต็มรูปแบบ
  → ออกเอกสารครบบน NextAcc ด้วยตัวเลขที่ผันแปรของเราจริง ๆ

## 7. ขั้นต่ำที่รับได้ (ถ้าทำเต็มไม่ได้)

อย่างน้อยขอ **`PUT /runs/{id}/lines`** ที่ override ยอดต่อพนักงานหลัง `calculate` (ก่อน `approve`) —
TakeTime จะ: create run → calculate → **override lines ด้วยยอดเรา** → approve → pay
แล้วเอกสาร/GL อิงจากยอดที่ override (ไม่ใช่ค่าที่ calculate)
