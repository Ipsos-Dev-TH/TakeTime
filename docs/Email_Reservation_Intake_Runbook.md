# Runbook — อีเมลจอง STAAH ไม่เข้าระบบ

คู่มือปฏิบัติการหลังเหตุการณ์ **19–21 ส.ค. 2026 (การจองไม่เข้าระบบ 2 วันเต็ม)**
ใช้ตอบคำถามเดียว: *"ตอนนี้ระบบอ่านอีเมลจองอยู่จริงไหม ถ้าไม่ เพราะอะไร"*

---

## 1. กระบวนการทำงานตั้งแต่ต้นจนจบ

```
Global.asax timer (ทุก 30 วิ)
  └─ ProcessEmailReservationIntakeIfDue()
       ├─ gate: Email_Rsv_Enabled = 1 ?
       ├─ gate: ครบรอบ Email_Rsv_PollMinutes (5 นาที) ? — ตัวจับเวลาอยู่ใน memory ต่อ process
       └─ EmailReservationService.ProcessEmails()
            ├─ ① ขอ lease  (App_Run_Lease / 'EmailReservationIntake', อายุ Email_Rsv_LeaseMinutes)
            │     ├─ ได้         → ทำต่อ
            │     ├─ มีคนถือ     → ข้ามรอบ + เขียน log + AlertIfIntakeStale()
            │     └─ ไม่มีตาราง  → ทำต่อแบบไม่ล็อก (degraded) + เขียน log
            ├─ ② วัด _stallHours จาก Email_Rsv_LastSuccess (ระบบหลับไปนานแค่ไหน)
            ├─ ③ รอบปกติ   : INBOX + ยังไม่อ่าน + ผู้ส่งมี Email_Rsv_FromContains
            │                 → HandleOne → ProcessOne → New / Modified / Cancelled
            │                 สำเร็จ → ย้ายเข้า STAAH-Processed · ล้มเหลว → STAAH-Failed
            ├─ ④ รอบลองใหม่: STAAH-Failed ที่อายุ ≤ max(Email_Rsv_RetryHours, _stallHours + 24)
            │                 สูงสุด Email_Rsv_RetryMaxPerRun ฉบับ/รอบ
            ├─ ⑤ รอบกวาดกู้: วันละครั้ง (หรือทันทีถ้า _stallHours ≥ 6)
            │                 ทุกโฟลเดอร์ ย้อนหลัง Email_Rsv_RecoverDays วัน
            │                 → ลงเฉพาะ booking ที่ "ไม่มีเลขนี้ในระบบเลย"
            └─ ⑥ บันทึก Email_Rsv_LastSuccess = เวลานี้   ← ตัวชี้วัดเดียวว่าระบบยังมีชีวิต
```

**ล็อกทุกตัวมีวันหมดอายุ** — ถ้า process ตายคางาน งานหยุดไม่เกินอายุ lease แล้วรอบถัดไปยึดคืนเอง
(เดิมใช้ `sp_getapplock` ผูกกับ SQL session ของ pooled connection → ค้างถาวรหลัง deploy)

---

## 2. เมื่อ "ไม่มีการจองเข้าระบบ" — ไล่ตามลำดับนี้

รัน **`Database/Check_Intake_Now.sql`** (อ่านอย่างเดียว) แล้วอ่านผลตามตาราง:

| ผลที่เห็น | แปลว่า | ทำอะไร |
|---|---|---|
| ข้อ 1 มี `intake skipped: ... lease` ทุก 5 นาที | มีรอบอื่นถือ lease | ดูข้อ 4 — ถ้า `Expires_At` เป็นอดีตแล้วยังค้าง แปลว่า DLL เก่า; ถ้าเป็นอนาคตคือมีรอบทำงานจริงอยู่ รอให้จบ |
| ข้อ 1 มี `intake skipped: lock held elsewhere` | **DLL เก่า** (ยังใช้ `sp_getapplock`) | recycle app pool ทันที แล้ว deploy DLL ใหม่ — ดู `Fix_Stuck_Intake_Lock.sql` |
| ข้อ 1 มี `IMAP error: ...` | ต่อกล่องเมลไม่ได้ | อ่าน error ตรง ๆ (มัก = App Password ถูกเพิกถอน) → ตั้งรหัสใหม่ในหน้า Admin |
| ข้อ 1 มี `intake lease degraded` | ยังไม่ได้รันไมเกรชัน 33 | รัน `PHASE18_Migration_33_Run_Lease.sql` (ระบบยังทำงานได้ แต่ไม่มีกันรันซ้อน) |
| ข้อ 1 **ว่างเปล่า** | timer ไม่ทำงาน / flag ปิด | เช็คข้อ 3 ว่า `Email_Rsv_Enabled = 1`; ถ้าเปิดอยู่แต่ยังเงียบ = app pool หลับ → ตั้ง Start Mode = AlwaysRunning + Idle Time-out = 0 |
| ข้อ 2 มีแถว | applock เก่ายังค้าง | recycle app pool (DLL ใหม่ไม่ใช้ล็อกนี้แล้ว จึงไม่กระทบ) |
| ข้อ 3 `Email_Rsv_LastSuccess` เก่ากว่า 1 ชม. | ระบบหยุดจริง | ไล่ตามแถวข้างบน |
| ข้อ 5 ว่าง แต่ข้อ 1 ปกติ | ระบบอ่านแล้วแต่ลงจองไม่สำเร็จ | ดู STAAH-Failed + Telegram; มัก = ไม่มี mapping ห้อง หรือห้องไม่ว่าง |

**อีเมลไม่เคยหาย** — ยังอยู่ในกล่องเมลเสมอ กู้ได้ด้วยปุ่ม **"กู้อีเมลย้อนหลัง"**

---

## 3. การตั้งค่าทั้งหมด

| คีย์ | ค่าแนะนำ | ผลถ้าตั้งผิด |
|---|---|---|
| `Email_Rsv_Enabled` | `1` | `0` = ไม่อ่านอีเมลเลย (เงียบสนิท ไม่มี error) |
| `Email_Rsv_PollMinutes` | `5` | ถี่เกินไปเปลืองโควตา IMAP |
| `Email_Rsv_RetryHours` | **`72`** | **ต่ำเกิน (เคยพบ `3`) = อีเมลที่ล้มเหลวเมื่อวานตกขบวนถาวร** — 🩺 เตือนถ้า < 24 |
| `Email_Rsv_RetryMaxPerRun` | `20` | ต่ำเกินไปกู้ backlog ช้า |
| `Email_Rsv_LeaseMinutes` | `15` | สั้นเกินไป + backlog ใหญ่ = รอบซ้อนกัน (มี heartbeat กันไว้แล้ว) |
| `Email_Rsv_StaleAlertMin` | `60` | `0` = ปิดเตือน → ระบบตายเงียบเหมือนเดิม |
| `Email_Rsv_RecoverDaily` | `1` | `0` = ไม่กวาดกู้ อีเมลที่ตัวอื่นแย่งอ่านไปจะหายถาวร |
| `Email_Rsv_RecoverDays` | `7` | ยาวเกินไปทำให้รอบกวาดช้า |
| `Email_Rsv_RecoverMax` | `100` | ต่ำเกินไปกู้ backlog ใหญ่ไม่หมดในรอบเดียว (รอบถัดไปกวาดต่อ) |
| `Email_Rsv_MoveFailed` | `1` | `0` = ไม่มี STAAH-Failed → รอบลองใหม่ไม่ทำงาน |

คีย์ที่ระบบเขียนเอง (ห้ามแก้มือ): `Email_Rsv_LastSuccess`, `Email_Rsv_LastRecover`, `Email_Rsv_LastStaleAlert`

---

## 4. กันไม่ให้เกิดซ้ำ — ที่ทำไปแล้ว

| ความเสี่ยงเดิม | สิ่งที่ป้องกันตอนนี้ |
|---|---|
| ล็อกค้างถาวรหลัง deploy | lease มีวันหมดอายุ (`App_Run_Lease`) + heartbeat ระหว่างวนอีเมล |
| ระบบตายเงียบไม่มีใครรู้ | เตือน Telegram เมื่อไม่มีรอบสำเร็จเกิน `Email_Rsv_StaleAlertMin` + 🩺 แสดงสถานะ |
| อีเมลตกหน้าต่าง retry | หน้าต่างขยายอัตโนมัติตามเวลาที่ระบบหลับ |
| มีโปรแกรมเก่าแย่งอ่านอีเมล | รอบกวาดกู้ทุกโฟลเดอร์วันละครั้ง (กู้คืนได้แม้ถูกอ่านไปแล้ว) |
| เซิร์ฟเวอร์เมลค้าง | `client.Timeout` 120 วิ/คำสั่ง + lease หมดอายุเอง |
| DB ช้าชั่วคราวตอนขอ lease | ข้ามรอบ 2 ครั้งแรก แล้วค่อยยอมทำงานแบบไม่ล็อก (ไม่ทั้งซ้ำ ไม่ทั้งค้างถาวร) |
| รอบกวาดปลุกการจองที่ยกเลิกแล้ว | โหมดกวาดใช้ dedup แบบ "มีเลขนี้ไม่ว่าสถานะใด = ข้าม" |
| รอบกวาดย้อนแก้/ยกเลิกซ้ำ | โหมดกวาดไม่แก้ไขใบเดิม และยกเลิกได้เฉพาะใบที่กู้มาในรอบเดียวกัน |

**สิ่งที่ยังต้องทำด้วยมือ:** ปิดโปรแกรม `GetReservationfromGmail` ใน Task Scheduler
วิธีเด็ดขาดคือ **เปลี่ยน Gmail App Password** แล้วใส่ค่าใหม่เฉพาะในหน้า Admin นี้
— ตัวเก่าจะล็อกอินไม่ได้อีกไม่ว่าซ่อนอยู่ที่ไหน

---

## 5. ลำดับ deploy

1. `git pull`
2. build บน Windows → deploy **`bin\*.dll` และไฟล์ `.aspx`** (ปุ่มใหม่อยู่ใน `.aspx`)
3. รันไมเกรชันตามลำดับ: **30 → 31 → 32 → 33 → 34**
4. เปิด Admin → Accounting Integration → **🩺 ตรวจสุขภาพการเชื่อมต่อ** → ต้องเห็น build `2026-08-21.x`
5. หน้าอีเมลจอง → **"กู้อีเมลย้อนหลัง"** → `7` วัน (กู้ backlog ที่ค้างอยู่)
6. รัน `Check_Intake_Now.sql` ยืนยัน `Email_Rsv_LastSuccess` ขยับ และข้อ 2 ว่าง
