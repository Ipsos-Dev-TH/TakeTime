# ตั้งค่า IIS / Windows Server สำหรับฟีเจอร์ล่าสุด

ครอบคลุม: **อ่านอีเมลจอง OTA (STAAH)**, **ส่งรูปตารางจองรายวันเข้า LINE**, **งานเบื้องหลัง
(sync บัญชี NextAcc / POS rollup / stock sync)**, และ **ปุ่มดึงใบเสร็จกลับจาก NextAcc**

> ทำตามลำดับ 0 → 6. ข้อที่ทำเครื่องหมาย ⚠️ = ไม่ทำแล้วฟีเจอร์ไม่ทำงาน (เงียบ ๆ ไม่มี error)

---

## 0) ก่อนอื่น — NuGet + build (บนเครื่อง dev/Windows)

Package Manager Console (Default project = `Take Time BangPhra`):

```powershell
Install-Package MimeKit -Version 2.15.0
Install-Package MailKit -Version 2.15.0
Install-Package HtmlAgilityPack -Version 1.11.72
Install-Package HtmlRenderer.WinForms -Version 1.5.0.6
```

⚠️ **ห้ามใช้ MailKit/MimeKit 4.x** — จะชน BouncyCastle 1.8.9 ที่ใช้เซ็น e-Tax (CS0433)
รายละเอียด: `docs/Email_Reservation_Intake_Setup.md`

รัน migration: `PHASE18_12` (email intake), `PHASE18_13` (LINE report), `PHASE18_14` (barcode guard)

---

## 1) Application Pool — งานเบื้องหลังต้องมีชีวิตอยู่ ⚠️

งานทั้งหมด (อ่านอีเมลตามรอบ, ส่ง LINE ตามเวลา, sync บัญชี) ทำงานผ่าน **timer ใน
`Application_Start`** → ถ้า app pool หลับ/รีไซเคิล timer จะตายจนกว่าจะมีคนเปิดเว็บ

**IIS Manager → Application Pools → [pool ของเว็บ] → Advanced Settings:**

| Setting | ค่าที่ต้องตั้ง | เหตุผล |
|---|---|---|
| **Load User Profile** | **True** ⚠️ | GDI+ / HtmlRenderer ต้องใช้ (ไม่ตั้ง = รูป LINE ออกมาเปล่า/ฟอนต์เพี้ยน) |
| **Idle Time-out (minutes)** | **0** ⚠️ | default 20 นาที = ไม่มีคนเข้าเว็บ 20 นาที pool หลับ → timer หยุด |
| **Regular Time Interval (minutes)** | **0** | ปิด recycle ทุก 29 ชม. (ถ้าต้องการ recycle ให้ตั้ง Specific Times ช่วงดึกแทน) |
| **Start Mode** | **AlwaysRunning** ⚠️ | หลัง reboot/deploy ให้ pool เริ่มเองโดยไม่ต้องรอคนเปิดเว็บ |
| **.NET CLR Version** | v4.0 | |
| **Managed Pipeline Mode** | Integrated | |

**IIS Manager → Sites → [เว็บ] → Advanced Settings → Preload Enabled = True** ⚠️

> ต้องติดตั้ง Windows feature **"Application Initialization"** ก่อน (Server Manager → Add Roles
> and Features → Web Server → Application Development → Application Initialization)
> ถ้าไม่ตั้ง 2 ข้อนี้: หลังรีสตาร์ทเซิร์ฟเวอร์ ถ้าไม่มีใครเปิดเว็บ **รายงาน LINE 08:00 จะไม่ถูกส่ง**

---

## 2) ฟอนต์ + สิทธิ์โฟลเดอร์ (สำหรับรูปส่ง LINE) ⚠️

1. **ติดตั้งฟอนต์ไทยบนเซิร์ฟเวอร์** — ให้ตรงกับที่หน้า DisplayToday ใช้ (เช่น Sarabun, Tahoma)
   ถ้าไม่มี ตัวอักษรไทยจะกลายเป็นกล่องสี่เหลี่ยม/หายไป
2. **สิทธิ์เขียนโฟลเดอร์รูป**: `<ที่ตั้งเว็บ>\Images\Reservation`
   - คลิกขวา → Properties → Security → เพิ่ม `IIS AppPool\<ชื่อ AppPool>` → **Modify**
   - (ถ้า app pool ใช้ identity อื่น เช่น domain account ให้ตั้งสิทธิ์ให้ account นั้น)
3. **โฟลเดอร์เอกสารเดิม** (ใบเสร็จ/ใบสำคัญจ่าย/e-Tax) ต้องเขียนได้เหมือนเดิม

---

## 3) รูปต้องเปิดสาธารณะผ่าน HTTPS ⚠️ (LINE เป็นคนมาโหลดรูปเอง)

LINE ไม่รับไฟล์แนบ — เราส่งแค่ URL แล้ว **เซิร์ฟเวอร์ของ LINE มาดึงรูปเอง** ดังนั้น:

- `https://taketimebangphra.com/Images/Reservation/20260804.jpg` ต้องเปิดได้จากภายนอก
  **โดยไม่ต้องล็อกอิน** (ลองเปิดในโหมดไม่ระบุตัวตน/มือถือที่ไม่ได้ล็อกอิน)
- ใบรับรอง SSL ต้องถูกต้อง ไม่หมดอายุ (LINE ปฏิเสธ cert ที่ไม่ผ่าน)
- ห้ามมี IP restriction / Windows Auth / Basic Auth ครอบโฟลเดอร์นี้
- ค่าในหน้า Admin ต้องตรงกัน:
  - **URL สาธารณะของโฟลเดอร์รูป** = `https://taketimebangphra.com/Images/Reservation`
  - **โฟลเดอร์เก็บรูป** = `~/Images/Reservation` (แนะนำ — ระบบแปลงเป็น path จริงให้เอง)

---

## 4) เน็ตขาออก (firewall / proxy)

| ปลายทาง | พอร์ต | ใช้ทำอะไร |
|---|---|---|
| `imap.gmail.com` | **993 (TCP, SSL)** | อ่านอีเมลจอง STAAH |
| `api.line.me` | 443 | ส่งรูป/ข้อความเข้า LINE |
| เซิร์ฟเวอร์ NextAcc | 443 | sync บัญชี, ดึง/กู้เอกสาร |
| `taketimebangphra.com` (ตัวเอง) | 443 | ระบบดึงหน้า DisplayToday มา render เป็นรูป ⚠️ |

⚠️ ข้อสุดท้ายสำคัญ: เซิร์ฟเวอร์ต้องเรียก **URL สาธารณะของตัวเอง** ได้ (loopback)
ถ้าองค์กรบล็อก hairpin NAT ให้แก้โดยตั้ง **URL หน้าที่จะ render** เป็น
`http://localhost/displaytoday` แทนในหน้า Admin

TLS 1.2 บังคับในโค้ดแล้ว (`Global.asax`) — ไม่ต้องตั้งเพิ่ม

---

## 5) Gmail (สำหรับอ่านอีเมลจอง)

1. เปิด IMAP: Gmail → Settings → Forwarding and POP/IMAP → **Enable IMAP**
2. เปิด 2-Step Verification แล้วสร้าง **App Password** (16 ตัว) — **ห้ามใช้รหัสผ่านปกติ**
3. กรอกในหน้า Admin → กด **"ทดสอบการเชื่อมต่อ"** ต้องขึ้น "เชื่อมต่อสำเร็จ"

---

## 6) ตรวจ Web.config ก่อน deploy ⚠️

`Web.config` มี AppSettings ที่เป็น **path ของเครื่อง dev** — ต้องแก้ให้ตรงเครื่องจริง:

```xml
<add key="ImagesFolderPath" value="C:\Users\Wachira.Diloksumpan\source\repos\..." />
```
→ เปลี่ยนเป็น path จริงบนเซิร์ฟเวอร์ เช่น `D:\Web Sites\wwwroot\Take Time\Images`
(ตรวจ `ReceiptFolderPath` / `PaymentFolderPath` / `DocumentFolderPath` ด้วย)

---

## ตรวจว่าใช้ได้จริง (ไล่ตามนี้หลัง deploy)

1. Admin → Accounting Integration → การ์ด **"อ่านอีเมลจอง OTA"** → **ทดสอบการเชื่อมต่อ** → เขียว
2. การ์ดเดียวกัน → **ดึงตอนนี้** → ดูสรุป → **ดู logs ล่าสุด**
3. การ์ด **"ส่งรูปตารางจองรายวันเข้า LINE"** → **พรีวิวรูป** → ต้องเห็นรูปตารางจอง
   **ฟอนต์ไทยอ่านออก ไม่ตกขอบ** (ถ้าเป็นกล่องสี่เหลี่ยม = ยังไม่ได้ลงฟอนต์ ข้อ 2)
4. → **ทดสอบข้อความ** (เช็ค token/ผู้รับ) → **ส่งตอนนี้** (เช็ค URL สาธารณะ ข้อ 3)
5. เปิดใช้งาน (สถานะ = เปิด) แล้ว **บันทึก** ทั้ง 2 การ์ด
6. ทิ้งไว้ข้ามคืน → เช้าวันรุ่งขึ้นดูว่ารูปเข้า LINE ตามเวลาที่ตั้ง (ถ้าไม่เข้า = ข้อ 1 App Pool)

## อาการ ↔ สาเหตุที่พบบ่อย

| อาการ | สาเหตุ |
|---|---|
| รูป LINE ออกมาเป็นหน้าขาว/ฟอนต์เป็นกล่อง | Load User Profile = False หรือไม่ได้ลงฟอนต์ไทย |
| กด "ส่งตอนนี้" สำเร็จ แต่ LINE ไม่ขึ้นรูป | URL รูปไม่เปิดสาธารณะ / cert ไม่ผ่าน (ข้อ 3) |
| กลางวันทำงาน แต่กลางคืน/เช้าไม่ส่ง | Idle Time-out ≠ 0 หรือ recycle (ข้อ 1) |
| หลังรีบูตเซิร์ฟเวอร์แล้วเงียบไปเลย | ไม่ได้ตั้ง AlwaysRunning + Preload (ข้อ 1) |
| อีเมลจองไม่ถูกดึง แต่ปุ่ม "ดึงตอนนี้" ใช้ได้ | timer ไม่ทำงาน → ข้อ 1 |
| สร้างรูปไม่สำเร็จ "โหลดหน้า source ไม่ได้" | เซิร์ฟเวอร์เรียก URL ตัวเองไม่ได้ (ข้อ 4) |
