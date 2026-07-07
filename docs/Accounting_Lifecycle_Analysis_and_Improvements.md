# วิเคราะห์ Lifecycle บัญชี (จอง→เข้าพัก→เช็คเอาท์→sync) + แผนปรับปรุงกระบวนการ

> สรุปจากการไล่โค้ดทั้งฝั่งต้นทาง (Reserve.aspx/ChannelManager) และฝั่ง sync (AccountingSyncService).
> เป้าหมาย: ยิงข้อมูลสร้างเอกสาร NextAcc ได้ถูกต้อง ไม่ค้าง/ผิดกลางกระบวนการ.

## 1. Lifecycle ปัจจุบัน (ต้นทาง → sync)

| ขั้น | ต้นทางสร้างอะไร | ปัญหา/ความกำกวม |
|---|---|---|
| **จอง** | `Reservation` (Deposit, TotalPrice, Status) — **ไม่มี** Account_Receipt | OTA: `ChannelManager` ตั้ง `Deposit=TotalPrice` + `OTA_Channel` แต่**ไม่สร้าง** Payment/Receipt |
| **รับมัดจำ** | ถ้าออกใบ → `Account_Receipt IsDeposit=1` (line บวก "ค่ามัดจำ"). ถ้าติ๊ก "ไม่ออกใบ" (CheckBox4) → **แค่ Payment_History** (ไม่มี IsDeposit=1) | มัดจำที่ไม่ออกใบ = **ไม่มี IsDeposit=1** → sync แยกไม่ออกจาก OTA |
| **เช็คอิน** | ใบเช็คอิน: line บวก (ห้องเต็ม) + line **ติดลบ "ส่วนลด" (Product_ID=17)** ชดเชยมัดจำ → `Total_Amount = สุทธิ` | negative line ใช้**ร่วมกัน**ทั้ง มัดจำ/OTA/ส่วนลด — **แยกไม่ได้ที่ตัว line** |
| **เช็คเอาท์** | `Checkout.aspx` **ไม่สร้าง receipt** (บล็อกจนจ่ายครบ). ใบจริงสร้างจาก Reserve.aspx edit → `IsDeposit=0` + line บวก + negative line. `EnqueueReceipt(depositApplied ไม่ส่ง=0)` | sync ต้อง**เดา** depositApplied จาก negative line เอง |
| **sync** | อ่าน negative line → depositApplied → gross-up + กลับมัดจำ | เดาผิด = 404/ค้าง/GL เพี้ยน |

## 2. รากปัญหาเชิงระบบ (สาเหตุร่วมของทุกบั๊ก)

**ต้นทางไม่ "ติดป้ายความหมาย" ให้ negative line** — มัดจำ / OTA-prepaid / ส่วนลด ออกมาเป็น
`Product_ID=17 "ส่วนลด" Price_Amount ติดลบ` เหมือนกันหมด → **sync ต้องเดาเจตนา**จากข้อมูลอ้อม
(มี/ไม่มี `IsDeposit=1`). การเดานี้พังในหลายเคส:

| เคส | ต้นทาง | sync เดา (เดิม) | ถูกไหม |
|---|---|---|---|
| มัดจำออกใบ | IsDeposit=1 มี | มัดจำจริง → กลับ | ✅ |
| **OTA-prepaid** | negative line, ไม่มี IsDeposit=1, มี OTA_Channel | เดาเป็น OTA → book net | ✅ (หลังแก้) |
| **มัดจำไม่ออกใบ** (CheckBox4) | Payment_History, ไม่มี IsDeposit=1 | เดาเป็น OTA → book net | ❌ มัดจำจริงแต่ไม่กลับ |
| **ส่วนลดจริง** | negative "ส่วนลด" | ถ้ามี IsDeposit=1 → เดาเป็นมัดจำ | ❌ ส่วนลดถูกกลับผิด |

## 3. บั๊กที่เจอจริง session นี้ (+ สถานะแก้)

| # | อาการ | สาเหตุ | แก้แล้ว |
|---|---|---|---|
| 1 | เช็คเอาท์ walk-in ได้ int_ TIV (VAT-on-top, ไม่มีลายเซ็น, REC ref) | ตกด่าน §86/4 → int_ fallback | ✅ `b6d8eb6` company Receipt(3) |
| 2 | JE ไม่ self-contained (Dr มัดจำ อยู่ JV แยก) | field display-only | ✅ `9758325` drives-journal (รอ NextAcc) |
| 3 | OTA 480 → depositApplied 18,416 → ค้าง/404 | negative line OTA เดาเป็นมัดจำ | ✅ `c31f7e6` OTA gate + `298df2f` OTA_Channel |
| 4 | 404 "ไม่พบเอกสาร" ค้าง | marker เฟสกลางชี้ doc ที่หาย | ✅ `3ee60df` marker recovery |
| 5 | มัดจำ legacy ไม่บน NextAcc | ไม่มีเอกสารให้ sync | ✅ `05ad775` never-stuck 3 เคส |

## 4. แผนปรับปรุงกระบวนการ (เรียงตามผลกระทบ)

### ★ ระดับต้นทาง (แก้รากจริง — ต้องแตะ Reserve.aspx/ChannelManager, ทดสอบ Windows)

**P1. ติดป้ายความหมายให้ negative line** — ใช้ `Product_ID`/`ProductType_ID` หรือคอลัมน์ใหม่แยกชนิด:
- มัดจำ → ชนิด "หักมัดจำ"
- OTA-prepaid → ชนิด "ชำระผ่าน OTA" (+ carry OTA_Channel)
- ส่วนลด → "ส่วนลด" (ของจริงเท่านั้น)

→ sync อ่านชนิดตรง ๆ ไม่ต้องเดา → หมดปัญหา #3/ส่วนลด/มัดจำไม่ออกใบ ในคราวเดียว

**P2. มัดจำต้องมี IsDeposit=1 เสมอ** — แม้ติ๊ก "ไม่ออกใบ" ก็สร้าง Account_Receipt IsDeposit=1
(ไม่พิมพ์ก็ได้) → มัดจำทุกก้อนมี "หนี้สินตั้งต้น" ให้กลับ → หมดเคส "มัดจำไม่ออกใบ = เดาเป็น OTA"

**P3. OTA revenue รับรู้แยก** — ปัจจุบันค่าห้อง OTA (เช่น 18,416) **ไม่เข้า NextAcc** เลย
(เข้าเฉพาะส่วนเพิ่มที่รับหน้าโรงแรม). ถ้าต้องการรายได้ครบ ต้องมี flow กระทบยอด OTA
(ตอน Agoda โอนเงินสุทธิ: Dr เงินสด + Dr ค่าคอมฯ / Cr รายได้ห้อง)

### ● ระดับ sync (ทำได้เลย ไม่แตะต้นทาง)

**S1.** ✅ อ่าน `OTA_Channel` จำแนก OTA vs มัดจำ-ไม่ออกใบ/ส่วนลด (`298df2f`) — flag รีวิว
**S2.** ✅ OTA gate (ไม่มี IsDeposit=1 → book net) (`c31f7e6`)
**S3.** ✅ marker recovery เฟสกลาง (`3ee60df`) + never-stuck 3 เคส (`05ad775`)
**S4.** (แนะนำต่อ) รวมกลไกมัดจำให้เหลือทางเดียว — ตอนนี้มี 2 ทางซ้อน:
  (a) หักในใบ (negative line → depositApplied → กลับในเอกสาร)
  (b) `CLEAR_DEPOSIT_AT_CHECKOUT` แยก (Dr 21510/Cr รายได้)
  มี anti-double-clear (recompute) แต่เปราะ — ควรเลือกทางเดียวชัด ๆ

### ○ ระดับ NextAcc (ส่งคำขอแล้ว — `docs/NextAcc_Pending_Requests_*`)
un-realize ตอน void, page-break JE, running-number, drives-journal deploy

## 5. ลำดับแนะนำ

1. **ตอนนี้**: rebuild+deploy `298df2f` → Retry #1023 (จะได้ 480), เคสค้างอื่นหาย
2. **สั้น**: ไล่ log `⚠ ยอดหักไม่มีใบมัดจำ + ไม่ใช่ OTA` หาเคส "มัดจำไม่ออกใบ/ส่วนลด" ที่ต้องรีวิว
3. **กลาง**: ทำ P1 (ติดป้าย negative line) + P2 (มัดจำมี IsDeposit=1 เสมอ) — แก้รากถาวร
4. **ยาว**: P3 (OTA revenue) + S4 (รวมกลไกมัดจำ) + NextAcc requests
