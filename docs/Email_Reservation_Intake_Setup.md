# Email Reservation Intake — ขั้นตอนติดตั้ง (ทำบน Windows)

Code + migration + Global.asax timer + หน้า Admin เสร็จแล้ว. เหลือ **เพิ่ม NuGet 3 ตัว** (ต้องทำบน
Windows ด้วย Visual Studio — WebForms build บน Linux ไม่ได้) แล้ว build + รัน migration.

## 1) เพิ่ม NuGet (Package Manager Console — Tools → NuGet → Package Manager Console)

> ⚠️ **สำคัญ — เรื่อง BouncyCastle:** โปรเจกต์นี้อ้าง `BouncyCastle 1.8.9` อยู่แล้ว (ใช้เซ็น e-Tax
> ใน `PDFA3U/PDFA3Invoice.cs`). MailKit เวอร์ชันใหม่ (3.x/4.x) ผูกกับ `BouncyCastle.Cryptography 2.x`
> ซึ่งเป็น **assembly คนละตัวแต่ namespace `Org.BouncyCastle` ทับกัน** → เกิด CS0433 (ambiguous) ที่
> PDFA3Invoice.cs. ให้ใช้ **MailKit/MimeKit 2.15.0** ซึ่งผูกกับ `Portable.BouncyCastle` (assembly
> `BouncyCastle.Crypto` ชื่อเดียวกับของเดิม) → NuGet จะอัปเกรด 1.8.9 → 1.9.0 แบบ in-place
> (API เข้ากันได้ ไม่กระทบการเซ็น e-Tax). **อย่าเลือก MailKit 4.x.**

รันทีละบรรทัด (ตั้ง Default project = `Take Time BangPhra`):

```powershell
Install-Package MimeKit -Version 2.15.0
Install-Package MailKit -Version 2.15.0
Install-Package HtmlAgilityPack -Version 1.11.72
```

- `MimeKit 2.15.0` จะดึง `Portable.BouncyCastle 1.9.0` + `System.*` (มีอยู่แล้วในโปรเจกต์:
  System.Buffers / System.Memory / System.Threading.Tasks.Extensions / System.Runtime.CompilerServices.Unsafe)
  — ถ้ามัน prompt อัปเกรด BouncyCastle เป็น 1.9.0 ให้ **ตอบ Yes**.
- PM Console จะแก้ทั้ง `packages.config` และ `<Reference>` ใน .csproj + binding redirects ให้อัตโนมัติ
  (จึงไม่ commit การแก้สองไฟล์นี้มาให้ — ให้ VS จัดการ HintPath ให้ถูกกับเครื่อง).

**หลังติดตั้ง**: เปิด `PDFA3U/PDFA3Invoice.cs` แล้ว build — ต้องผ่าน. รันสร้าง e-Tax หนึ่งใบเพื่อยืนยัน
ลายเซ็นยังทำงาน (BouncyCastle 1.8.9 → 1.9.0 เป็น minor bump เข้ากันได้ แต่ verify ไว้ให้ชัวร์).

## 2) รัน migration

```
Database/PHASE18_Migration_12_Email_Reservation_Intake.sql
```

เพิ่มคอลัมน์ `OTA_Gross_Amount` / `OTA_Net_Amount` / `OTA_Payment_Type` บน `Reservation` (idempotent)
+ seed config keys `Email_Rsv_*`. (คอลัมน์ `OTA_Channel`/`OTA_Booking_ID`/`OTA_Guest_Name` มีอยู่แล้ว
จาก Channel Manager.)

## 3) ตั้งค่าในหน้า Admin

Admin → Accounting Integration → การ์ด **"อ่านอีเมลจอง OTA (STAAH)"**:

1. เปิด IMAP ในบัญชี Gmail (Settings → Forwarding and POP/IMAP → Enable IMAP).
2. สร้าง **App Password** (Google Account → Security → 2-Step Verification → App passwords) — ไม่ใช่รหัสผ่านปกติ.
3. กรอก Gmail address + App Password + IMAP server (`imap.gmail.com`) + port (`993`).
4. กด **"ทดสอบการเชื่อมต่อ"** → ต้องขึ้น "เชื่อมต่อสำเร็จ".
5. เลือก **หลังลงจอง**: `ลงจองเฉย ๆ` (default — เอกสารออกตอนเช็คอิน/เช็คเอาท์) หรือ `ยิงสร้างเอกสารทันที`
   (ต้องเปิด `Nexaacc_Ota_Settlement` + map บัญชี OTA ก่อน — ดู `docs/OTA_Settlement_Design.md`).
6. ตั้ง **สถานะ = เปิด** → **บันทึกการตั้งค่า**.
7. กด **"ดึงตอนนี้"** เพื่อทดสอบดึงรอบแรก (ดูสรุป: ดึงกี่ฉบับ / สร้าง / ซ้ำ / ล้มเหลว).

หลังจากนี้ background timer จะดึงเองทุก N นาที (`ดึงทุก (นาที)`, default 5) — ใช้ timer เดียวกับ accounting sync.

## 4) รันคู่กับโปรแกรมเดิม (parallel) 1–2 สัปดาห์

dedup ใช้ Booking ID (ทั้ง `OTA_Booking_ID` และ `Remark`) → รันคู่ระบบภายนอกเดิมได้ ไม่สร้างจองซ้ำ.
เมื่อมั่นใจแล้วค่อยปิดโปรแกรมภายนอก.

## หมายเหตุการทำงาน

- **gross vs net:** เก็บ `OTA_Gross_Amount` (refsell_amt = ราคาที่ลูกค้าจ่าย OTA) เป็น `TotalPrice`/`Deposit`
  (ฐานรายได้/VAT ถูก) และ `OTA_Net_Amount` (AMOUNT = ยอด OTA จะโอน) ไว้เทียบ payout — แก้บั๊กเดิมที่เอา net
  มาเป็นมัดจำ (เคส 148824).
- **Channel Collect** → Deposit = gross (OTA เก็บเงินแล้ว); **Hotel Collect** → Deposit = 0 (เก็บหน้างาน).
- **Modified email** = ยกเลิกการจองเดิม (เว้นที่ถูกยกเลิกไปแล้ว) + สร้างใหม่; **Cancelled** = ตั้ง Status ยกเลิก + ลบห้อง.
- ทุกอย่าง gate ด้วย `Email_Rsv_Enabled` — ปิดอยู่ = timer ข้าม, ไม่มีผลอะไร.
