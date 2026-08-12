# Feature Flags — สวิตช์เปิด/ปิดฟีเจอร์รายโมดูล

ตั้งค่าที่ **Admin → Settings → ตั้งค่าระบบ (System Settings) → หมวด "ฟีเจอร์ (เปิด/ปิดรายโมดูล)"**
(Owner เท่านั้น) — มีผลภายใน ~30 วินาที ไม่ต้องรีสตาร์ท

## ปิดแล้วเกิดอะไร

1. **เมนูซ่อน** — ทั้งเมนูหน้าเว็บสาธารณะและเมนูผู้ดูแลระบบ (Site.Master)
2. **เข้าหน้าโมดูลตรง ๆ ไม่ได้** — ทุกหน้าของโมดูลมี `Feature.Guard` เด้งกลับหน้าหลัก
   (หน้า Guest Portal เด้งกลับ Dashboard ของ Portal)
3. **การ์ด/ปุ่มใน Guest Portal Dashboard ซ่อน** (quick action, การ์ดบริการ, bottom nav)
4. **ข้อมูลไม่ถูกลบ** — ตารางข้อมูลเดิมอยู่ครบ เปิดกลับมาใช้ต่อได้ทันที

## รายการฟีเจอร์และค่าเริ่มต้น

| Key | โมดูล | ค่าเริ่มต้น | เหตุผล |
|---|---|---|---|
| `Feature_Housekeeping` | แม่บ้าน/สถานะทำความสะอาด | **ปิด** | ยังไม่ใช้งานจริง (ยังไม่ผูกกับเช็คอิน/เอาท์) |
| `Feature_Maintenance` | งานซ่อมบำรุง | **ปิด** | ยังไม่ใช้งานจริง |
| `Feature_DynamicPricing` | ราคาไดนามิก | **ปิด** | ยังไม่ต่อกับราคาจองจริง |
| `Feature_RoomService` | รูมเซอร์วิส | เปิด | |
| `Feature_Activities` | กิจกรรมในที่พัก | เปิด | |
| `Feature_Chat` | แชทลูกค้า (Omni-Channel + อีเมล OTA) | เปิด | ปิดแล้วกระดิ่งแจ้งเตือนแชทก็หาย |
| `Feature_Loyalty` | สะสมแต้ม/สมาชิก/Tier | เปิด | |
| `Feature_Reviews` | รีวิวลูกค้า | เปิด | |
| `Feature_Affiliate` | ระบบตัวแทน | เปิด | |
| `Feature_AI` | ผู้ช่วย AI | เปิด | |
| `Feature_ChannelManager` | Channel Manager Dashboard | เปิด | |
| `Feature_WebAnalytics` | สถิติเว็บ | เปิด | |
| `Feature_Assets` | ทะเบียนทรัพย์สิน | เปิด | |
| `Feature_HR` | HR (พนักงาน/ลา/เงินเดือน/OT) | เปิด | |
| `Feature_GuestPortal` | Guest Portal | เปิด | |

ค่าใน DB ว่าง (— ใช้ค่าเดิม —) = ใช้ค่าเริ่มต้นตามตาราง / เลือก true/false = บังคับตามนั้น

## สำหรับนักพัฒนา

- `Feature.On("ชื่อ")` / `Feature.Off("ชื่อ")` — อ่านสวิตช์ (คีย์จริงคือ `Feature_ชื่อ` ใน System_Config ผ่าน AppCfg cache 30 วิ)
- `if (!Feature.Guard(this, "ชื่อ", "~/ปลายทาง")) return;` — บรรทัดแรกใน Page_Load ของหน้าโมดูล
- โมดูลใหม่: เพิ่ม default ใน `Feature.cs` (DefaultOff ถ้าอยากปิดเริ่มต้น) + seed row ใน migration + Guard ทุกหน้า + PlaceHolder ในเมนู
- Migration: `Database/PHASE18_Migration_19_Feature_Flags.sql` (ต้องรัน PHASE18_18 ก่อน)

## หมายเหตุ

- แชทอีเมล OTA มีสวิตช์ของตัวเองอีกชั้น (channel EMAIL ใน Admin → Chat → ตั้งค่าช่องทาง) —
  `Feature_Chat` ปิด = ปิดหน้าแชททั้งหมด แต่ตัวดึงอีเมลเบื้องหลังคุมด้วยสวิตช์ channel EMAIL
- ระบบบัญชี NextAcc / อ่านอีเมลจอง / รายงาน LINE รายวัน มีสวิตช์เฉพาะของตัวเองอยู่แล้ว
  (Accounting Integration) จึงไม่อยู่ในหมวดนี้
