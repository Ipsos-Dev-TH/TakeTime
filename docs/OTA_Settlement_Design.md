# OTA Settlement Design — ลูกหนี้ OTA / payout / AGP Advance / ภ.พ.36 (ครบทุกเคส)

**สถานะ:** ออกแบบเสร็จ (ก.ค. 2026) — foundation (migration PHASE18_11) ลงแล้ว, UI + processor รอเคาะ
**หลักการ:** agency model — รายได้ห้อง = ราคาขายเต็มที่ลูกค้าจ่าย OTA (gross), ค่าคอม OTA = ค่าใช้จ่ายแยก
(Agoda/Booking.com เป็นบริษัทต่างประเทศ → ค่าคอมเข้า §83/6 → ภ.พ.36)

## ทำไมต้องเลิก "ลง payout ก้อนเดียว" (วิธีปัจจุบัน)

1. รายได้ = net หลังหักคอม → รายได้ต่ำกว่าจริง + ค่าคอมไม่เคยปรากฏ
2. VAT ขายฐาน = gross ที่ลูกค้าจ่าย ไม่ใช่ net → นำส่ง ภ.พ.30 ขาดทุกเดือน (เบี้ยปรับย้อนหลัง)
3. ค่าคอม OTA ต่างประเทศไม่เคยยื่น ภ.พ.36 (§83/6 — self-assess 7% แล้วเคลมคืนได้ ไม่ยื่น = ความผิด)
4. ไม่มีลูกหนี้ OTA → ไม่รู้ค้างเท่าไหร่ ตรวจ/ทวงไม่ได้

## บัญชี (mapping ใหม่ — PHASE18_11)

| TakeTime_Code | บัญชี | ใช้ |
|---|---|---|
| `OTA_RECEIVABLE` | ลูกหนี้ OTA (11xxx) | Dr ตอนเช็คเอาท์ / Cr ตอน payout |
| `OTA_COMMISSION` | ค่าคอมมิชชั่น OTA (5xxxx) | Dr ตอน payout (derive) |
| `OTA_ADVANCE` | เงินรับล่วงหน้าจาก OTA — AGP (2xxxx หนี้สิน) | Cr ตอนรับ advance / Dr ตอนถูกหัก |

**แยกลูกหนี้รายเจ้า (คำถาม 4):** ไม่แตกบัญชี GL รายเจ้า (ผังบวม) — ใช้ **contact ราย OTA บน NextAcc**
(invoice AR ผูก contact "Agoda" / "Booking.com") → aging รายเจ้าดูบน NextAcc ได้ + ตาราง
`OTA_Channels` ฝั่งเรา (มีอยู่แล้ว — ใช้เป็นทะเบียนเจ้า) เพิ่มคอลัมน์การเงิน (commission%, foreign flag,
contact id) แทนการสร้างตารางใหม่. หน้าจัดการ = ต่อยอดหน้า Channel Manager เดิม + หน้า Payout ใหม่.

## GL ทุกเคส

```
A. เช็คเอาท์ booking OTA (ทุกเจ้า — รวมเจ้าที่ไม่แจกแจง):
   Dr ลูกหนี้ OTA (gross = ราคาขายในระบบ) / Cr รายได้ห้อง + Cr ภาษีขาย
   → ราย booking, ref RES-{id}, ผูก contact เจ้านั้น. ใบเสร็จของเช่า/จ่ายหน้างานแยกใบ (แก้แล้ว b6328db)

B. Payout ปกติ:  Σgross(booking ที่เลือก) = โอนจริง + ค่าคอม
   Dr เงินฝาก (โอนจริง) + Dr ค่าคอม OTA (derive) / Cr ลูกหนี้ OTA (Σgross)

C. AGP Advance รับเงินก้อน:
   Dr เงินฝาก / Cr เงินรับล่วงหน้า OTA (หนี้สิน — ไม่ใช่รายได้)

D. Payout ที่ถูกหัก advance:  Σgross = โอนจริง + advanceหักงวดนี้ + ค่าคอม
   Dr เงินฝาก + Dr เงินรับล่วงหน้า OTA (ส่วนหัก) + Dr ค่าคอม / Cr ลูกหนี้ OTA

E. เจ้าไม่แจกแจงราคาขาย/คอม (คำถาม 2): ไม่ต้องการ statement รายละเอียด —
   ผู้ใช้เลือก booking ที่อยู่ในงวด → gross มาจาก "ราคาขายในระบบเรา" (TotalPrice ตอนจอง)
   → ค่าคอม = Σgross − โอน − advanceหัก (derive เสมอ). หน้าจอโชว์ %คอม เทียบ default
   ของเจ้า (เช่น Agoda ~17%) → เบี่ยงเกิน threshold = เตือนว่าเลือก booking ขาด/เกิน
F. ยกเลิก/refund หลังลง AR: credit-note ลด AR (เส้น CN + bookingNumber มีแล้ว)
G. เศษปรับปรุงเล็ก: รวมเข้า ค่าคอม OTA (แสดงแยกบรรทัดใน JE description)
```

**ค่าคอม → ภ.พ.36:** ตอนปิด payout สร้าง expense doc บน NextAcc (ค่าคอม OTA) พร้อม
`IsForeignService=true` เมื่อ `OTA_Channels.Is_Foreign=1` → เข้า ภ.พ.36 + ภ.ง.ด.54 อัตโนมัติ
(NextAcc `ExportPp36Async` รองรับแล้ว — verified). เจ้าไทยตั้ง flag = 0.

## ตาราง (PHASE18_11)

- `OTA_Channels` + คอลัมน์: `Default_Commission_Pct DECIMAL(5,2)`, `Is_Foreign BIT` (default 1),
  `Nexaacc_Contact_Id UNIQUEIDENTIFIER`, `Advance_Balance DECIMAL(18,2)` (ยอด AGP คงเหลือ, denormalized)
- `OTA_Payout` (งวดโอน): ID, Channel_Code, Payout_Date, Amount_Received, Advance_Deducted,
  Commission_Derived, Gross_Total, Status (DRAFT/POSTED/VOIDED), Nexaacc_Journal_Id, Nexaacc_Expense_Id, Notes
- `OTA_Payout_Item`: Payout_ID, Reservation_ID, Gross_Amount — booking ที่รวมในงวด
  (unique Reservation_ID กันปิดซ้ำ 2 งวด)
- `OTA_Advance`: ID, Channel_Code, Received_Date, Amount, Nexaacc_Journal_Id, Notes — ก้อน AGP ที่รับ

## Flow ระบบ

1. **เช็คเอาท์ booking ที่ `Reservation.OTA_Channel` มีค่า** → enqueue `OTA_AR_INVOICE`
   (Dr AR-OTA / Cr revenue+VAT, ราย booking, contact = เจ้านั้น) — ของเช่า/จ่ายหน้างานออกใบเสร็จแยกตามเดิม
2. **หน้า "รับเงินล่วงหน้า OTA"** — บันทึกก้อน AGP → JE เคส C + บวก Advance_Balance
3. **หน้า "ปิดงวด Payout OTA"** — เลือกเจ้า → กรอกยอดโอน + advance ที่ถูกหัก (default 0) →
   ติ๊กเลือก booking ค้าง (แสดง AR ค้างรายเจ้า, ค้นตามช่วงวันที่/เลข OTA_Booking_ID) →
   ระบบคำนวณ gross/คอม/% → ยืนยัน → JE เคส B/D + expense ค่าคอม (§83/6) + mark items
4. **ยอดค้าง**: ลูกหนี้รายเจ้า = Σ OTA_AR ที่ยังไม่อยู่ใน payout POSTED — โชว์บนหน้า payout + NextAcc aging

## คำถาม 5 — STAAH email → external ลงจอง

**คำแนะนำ: คงระบบ external ระยะสั้น + แก้เล็กน้อยให้ส่งข้อมูลครบ** (ไม่ block settlement):
settlement ต้องการจาก booking แค่ 3 field: `OTA_Channel`, ราคาขาย gross (TotalPrice), `OTA_Booking_ID`
(ใช้จับคู่กับ statement) — `ChannelManagerService` มี field เหล่านี้อยู่แล้ว ถ้า external เขียนครบก็จบ.
**ระยะยาว** ค่อยย้าย email-parser เข้า TakeTime (อ่าน IMAP เอง — คุม format/retry/log ได้, ตัด dependency)
เป็นงานแยกอิสระ ไม่กระทบดีไซน์นี้เพราะ settlement อิงแค่ข้อมูลบน Reservation.

## ลำดับ implement (หลังเคาะกับผู้ทำบัญชี)

1. ✅ PHASE18_11 migration (ตาราง + mapping + flag `Nexaacc_Ota_Settlement` default off)
2. Enqueue/Processor `OTA_AR_INVOICE` (เช็คเอาท์ → AR ราย booking) — gate ด้วย flag
3. หน้า Payout + Advance (Admin) + JE posting + expense §83/6
4. รายงานค้าง/kreconcile + เตือน %คอมเบี่ยง
5. (แยก) ย้าย STAAH parser เข้าระบบ

**การเปิดใช้:** เปิด flag → booking OTA ที่เช็คเอาท์หลังจากนั้นเข้าระบบใหม่; ของเก่าไม่ย้อน (ค่อย
บันทึก payout ย้อนเฉพาะที่ต้องการ). ระหว่าง flag ปิด ทุกอย่างเหมือนเดิม.
