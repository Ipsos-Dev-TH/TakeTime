# Email Reservation Intake — ย้าย STAAH Gmail reader เข้า TakeTime

**ต้นทาง:** `Wachira-d/GetReservationfromGmail` (console app แยก, อ่านครบทั้ง repo แล้ว)
**เป้าหมาย:** อ่านเมล + ลงจองในระบบเอง, ตั้งค่าในหน้า Admin, ข้อมูลครบสำหรับ OTA settlement
(`docs/OTA_Settlement_Design.md`)

## สิ่งที่ระบบเดิมทำ (วิเคราะห์จากโค้ดจริง)

- MailKit IMAP → Gmail → subject มี "New Reservation" / "Cancelled" / "Modified"
- Parse (HtmlAgilityPack + regex): Channel Name, Bookings Status, Booking Id#, guest/phone,
  ราย room-plan (ROOM TYPE / NO OF ROOMS / CHECK-IN/OUT / ADULTS / AMOUNT)
- 2-phase atomic save: จับคู่ห้องว่างต่อ plan (mapping Channel+RoomType→Accommodation, HOLDLOCK
  กันชนกัน, กันเลือกซ้ำใน booking เดียว) → INSERT Reservation + Reservation_Accommodation ใน transaction
- Dedup ด้วย Booking Id (duplicate → label แล้วข้าม), Modified = cancel เดิม+สร้างใหม่, Cancelled = ยกเลิก
- ย้ายเมล → label/folder STAAH-Processed / STAAH-Failed + Telegram แจ้งเตือน + validator/health-check

## ⚠ จุดที่ต้องปรับ (data ไม่เข้ากับบัญชี — ต้นเหตุบั๊กที่เจอมาแล้ว)

| ปัญหาเดิม | ผลเสีย | แก้ |
|---|---|---|
| ใช้ AMOUNT (net ที่โรงแรมจะได้ เช่น 1,461.41) เป็น TotalPrice + **Deposit** | เช็คอินตีความว่า "ลูกค้าจ่ายมัดจำแล้ว 1,461" → JE เพี้ยน (เคส 148824) และไม่มีราคาขายจริง | เก็บแยก: `OTA_Net_Amount` = AMOUNT, `OTA_Gross_Amount` = **refsell_amt** (2,186.75 = ราคาที่ลูกค้าจ่าย OTA) — TotalPrice ใช้ gross (ราคาขายจริง สำหรับรายได้/VAT) |
| ไม่ parse `refsell_amt` เลย | ไม่มีฐาน gross → settlement/VAT คำนวณไม่ได้ | เพิ่ม regex `refsell_amt\s*:\s*([\d,\.]+)` จาก Additional Information |
| ไม่เก็บ Payment Type (Channel Collect / Hotel Collect) | ไม่รู้ว่าเงินอยู่ที่ OTA หรือเก็บหน้างาน | เก็บ `OTA_Payment_Type` — Channel Collect = ลูกหนี้ OTA, Hotel Collect = เก็บเงินสดตอนเช็คอินปกติ |
| config อยู่ App.config เครื่องนอก | แก้ต้องรีโมทเข้าเครื่อง | ย้ายทุก key → `Accounting_Integration_Config` + หน้า Admin (encrypt password ด้วย code.Crypt) |

## สถาปัตยกรรมใน TakeTime

- `Class/Services/EmailReservationService.cs` — port ทั้ง flow (fetch→parse→save→label→notify)
  โครงเดิมเกือบ verbatim (พิสูจน์บน format จริงแล้ว) เปลี่ยน: App.config→DB config,
  LoggingService→code.Logs, Telegram→TelegramBot2 เดิมของระบบ, + เก็บ gross/net/paymentType
- Trigger 2 ทาง: background timer ใน `Global.asax` (ทุก `Email_Rsv_PollMinutes`, gate ด้วย flag)
  + ปุ่ม "ดึงตอนนี้" ในหน้า Admin
- Dedup ใช้ `Reservation.OTA_Booking_ID` (มีอยู่แล้ว — ChannelManagerService ใช้)
- **NuGet ใหม่** (จำเป็น, copy version จาก repo ต้นทางที่ build ผ่าน): MailKit 4.11, MimeKit 4.11,
  HtmlAgilityPack 1.12, BouncyCastle.Cryptography 2.5.1 (+ System.* runtime deps) — เพิ่มผ่าน
  VS NuGet บน Windows ปลอดภัยสุด (restore + binding redirects อัตโนมัติ)

## Config keys (PHASE18_12 — seed แล้ว, หน้า Admin แก้ได้)

`Email_Rsv_Enabled` (0/1) · `Email_Rsv_ImapServer` · `Email_Rsv_ImapPort` · `Email_Rsv_Username` ·
`Email_Rsv_Password_Encrypted` (Gmail app password, encrypt) · `Email_Rsv_PollMinutes` (default 5) ·
`Email_Rsv_ProcessedLabel` / `Email_Rsv_FailedLabel` · `Email_Rsv_MaxStayDays` / `Email_Rsv_MaxDaysFuture`
(validation เดิม) · `Email_Rsv_NotifyTelegram` (ใช้ token/chat เดิมของระบบ)

## Field mapping (email → Reservation)

| Email | คอลัมน์ | หมายเหตุ |
|---|---|---|
| Channel Name | `OTA_Channel` | ตรง `OTA_Channels.Channel_Code` → settlement รายเจ้า |
| Booking Id# "1114...(2035081438)" | `OTA_Booking_ID` | dedup + จับคู่ payout statement |
| AMOUNT (net) | `OTA_Net_Amount` 🆕 | ยอดคาดว่า OTA จะโอน (ต่อ booking) |
| refsell_amt (gross) | `OTA_Gross_Amount` 🆕 + `TotalPrice` | ราคาขายจริง → รายได้/VAT/ลูกหนี้ OTA |
| Payment Type | `OTA_Payment_Type` 🆕 | Channel Collect → เส้นลูกหนี้ OTA; Hotel Collect → เก็บหน้างาน |
| ROOM TYPE ราย plan | Reservation_Accommodation | ผ่าน mapping + availability เดิม |
| Deposit | **Channel Collect → gross** (OTA เก็บแล้ว) | สอดคล้อง fix เช็คอิน (ตัดรายการที่จ่ายแล้วออกจากใบเสร็จ) |

**ประโยชน์ทันที:** payout reconcile เทียบ `Σ OTA_Net_Amount` ของ booking ที่เลือก vs ยอดโอนจริง →
จับ booking ตกหล่นแม่นกว่า derive จาก %คอม; คอมจริง = `Σgross − โอน − advance` ตาม design settlement.

## ลำดับ implement

1. ✅ PHASE18_12: คอลัมน์ `OTA_Gross_Amount`/`OTA_Net_Amount`/`OTA_Payment_Type` + config keys
2. (ชั่วคราว — optional) external ปัจจุบันเพิ่ม 3 คอลัมน์นี้ตอน INSERT ได้เลย (แก้ 5 บรรทัด) ระหว่างรอ port
3. Port `EmailReservationService` + NuGet (ก้อนเดียว build/test บน Windows) + timer Global.asax
4. หน้า Admin: section "อ่านอีเมลจอง OTA" (settings + ทดสอบเชื่อมต่อ + ดึงตอนนี้ + log ล่าสุด)
5. ปลด external ออกหลังรันคู่ (parallel) 1-2 สัปดาห์ — dedup ด้วย OTA_Booking_ID กันสร้างซ้ำระหว่างรันคู่
