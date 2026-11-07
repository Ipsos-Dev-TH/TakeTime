# ส่งรูปตารางจองรายวันเข้า LINE — ขั้นตอนติดตั้ง (ทำบน Windows)

พอร์ตจาก external console app (HTMLToPicture) เข้าระบบ: render หน้า DisplayToday เป็นรูป → push เข้า LINE
อัตโนมัติทุกวัน. Code + timer + หน้า Admin เสร็จแล้ว เหลือ **เพิ่ม NuGet ตัว render + build + migration**.

## 1) เพิ่ม NuGet (Package Manager Console — Default project = `Take Time BangPhra`)

```powershell
Install-Package HtmlRenderer.WinForms -Version 1.5.0.6
```

- ดึง `HtmlRenderer.Core 1.5.0.6` มาด้วย (namespace `TheArtOfDev.HtmlRenderer.WinForms`) — ตัวเดียวกับที่โปรแกรมภายนอกใช้.
- `System.Drawing` เป็น framework assembly (อ้างอยู่แล้วใน .csproj) ไม่ต้องเพิ่ม.
- **ไม่ใช้** `Line.Messaging` NuGet — เรียก LINE REST API ตรง (`api.line.me/v2/bot/message/push`) เหมือนโค้ด OmniChannel เดิม.

## 2) รัน migration

```
Database/PHASE18_Migration_13_Daily_Line_Report.sql
```

seed config keys `Line_DailyReport_*` (idempotent).

## 3) ตั้งค่า IIS ให้ render รูปได้ (สำคัญ)

GDI+ / HtmlRenderer ต้องการสิทธิ์เดสก์ท็อป + ฟอนต์:

1. **App Pool → Advanced Settings → Load User Profile = True** (ไม่งั้น GDI+ อาจได้รูปเปล่า/ฟอนต์เพี้ยน).
2. ติดตั้ง **ฟอนต์ไทย** บนเครื่อง server (เช่น Tahoma/Sarabun — ให้ตรงกับที่หน้า DisplayToday ใช้).
3. โฟลเดอร์รูป (`~/Images/Reservation`) ต้องให้ App Pool identity เขียนได้ + เข้าถึงได้ผ่าน HTTPS สาธารณะ
   (LINE โหลดรูปจาก URL — ต้องเป็น HTTPS ที่เปิดสาธารณะ).

## 4) ตั้งค่าในหน้า Admin

Admin → Accounting Integration → การ์ด **"ส่งรูปตารางจองรายวันเข้า LINE"**:

1. **ผู้รับ**: ใส่ LINE `userId`/`groupId`/`roomId` (คั่นด้วย comma หรือขึ้นบรรทัดใหม่ ได้หลายราย).
   - groupId: เชิญ LINE OA เข้ากลุ่ม → ดู `source.groupId` จาก webhook (หรือใช้ค่าเดิมจากโปรแกรมภายนอก).
2. **token**: ปล่อยว่าง = ใช้ token ของ LINE OA เดิม (ตั้งไว้แล้วใน OmniChannel/Web.config); หรือใส่เฉพาะงานนี้.
3. ตั้ง **เวลาส่ง** (เช่น 08:00), ข้อความประกอบ (`{date}` = วันที่ไทย), ขนาดรูป, URL หน้า source.
4. กด **"พรีวิวรูป"** → ระบบ render + โชว์รูปในหน้า (ตรวจว่าฟอนต์/เลย์เอาต์ถูกก่อนเปิดส่งจริง).
5. กด **"ทดสอบข้อความ"** → ส่ง text ทดสอบถึงผู้รับ (ตรวจ token/recipient).
6. กด **"ส่งตอนนี้"** → render + push รูปจริงทันที.
7. ตั้ง **สถานะ = เปิด** → **บันทึก**. หลังจากนี้ timer จะส่งเองทุกวันเมื่อถึงเวลา (วันละครั้ง กันส่งซ้ำด้วย marker `Line_DailyReport_LastSent`).

ปุ่ม **"ดู logs"** = ไล่ประวัติการส่ง (LogAction='DailyLineReport').

## หมายเหตุ / จุดที่ทำได้ดีกว่าโปรแกรมเดิม

- **ความสูงรูปวัดจากเนื้อหาจริง** (`HtmlRender.Measure`) — ไม่ตัดตกและไม่เหลือขอบขาว (เดิมเดาจากความยาว HTML เป็นช่วง ๆ).
- คุณภาพ JPEG ปรับได้ + พื้นหลังขาว + anti-alias.
- cache-bust URL (`?t=HHmmss`) ให้ LINE โหลดรูปใหม่ทุกครั้ง (เดิม URL เดิมทั้งวัน LINE อาจ cache รูปเก่า).
- ผู้รับหลายคน/หลายกลุ่มได้ในที่เดียว.

## ⚠️ ความปลอดภัย — token ที่หลุด

LINE channel access token + userId ที่ส่งมาใน chat **ถือว่าหลุดแล้ว** — ควร **rotate token ใหม่**
ที่ LINE Developers Console แล้วอัปเดตใน OmniChannel/Web.config. ระบบนี้เก็บ token override แบบ
**เข้ารหัส** ใน DB (ตั้งผ่านหน้า Admin) ไม่เก็บใน source. อนึ่ง **LINE Notify** (`notify-api.line.me`)
ปิดบริการแล้ว — ระบบใหม่ใช้ Messaging API ล้วน.
