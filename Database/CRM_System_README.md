# 🎯 CRM System for Take Time Nature Resort

## 📋 Overview

ระบบ CRM (Customer Relationship Management) แบบครบวงจรสำหรับโรงแรม Take Time Nature Resort
พัฒนาเมื่อ: 2025-01-24

## ✨ Features Implemented

### 1. **Guest Preferences & Notes** 👤
- บันทึกความชอบของแขก (ห้องโปรด, หมอน, เตียง, อาหาร, กิจกรรม)
- จัดการ allergies และข้อจำกัดด้านอาหาร
- บันทึกข้อมูลสำคัญเกี่ยวกับแขก (VIP, คำร้องเรียน, คำชม, พฤติกรรม)
- ระดับความสำคัญ (Low, Medium, High, Critical)

**Tables:**
- `Guest_Preferences` - ความชอบ
- `Guest_Notes` - บันทึกเกี่ยวกับแขก

### 2. **Loyalty Program** 🏆
- ระบบคะแนนสะสม (Points earning & redemption)
- 5 ระดับสมาชิก: Member, Silver, Gold, Platinum, VIP
- Points multiplier ตามระดับ (1.0x - 3.0x)
- ส่วนลดอัตโนมัติตามระดับ (0% - 20%)
- ของรางวัลที่แลกได้ (Rewards catalog)
- วันหมดอายุของคะแนน (12 เดือน)

**Tables:**
- `Loyalty_Tiers` - ระดับสมาชิก
- `Customer_Loyalty` - บัญชีคะแนนของลูกค้า
- `Loyalty_Transactions` - ประวัติการได้-ใช้คะแนน
- `Loyalty_Rewards` - ของรางวัล

**Service Class:**
- `LoyaltyService.cs` - จัดการทุกอย่างเกี่ยวกับ Loyalty Program

### 3. **Guest Reviews & Ratings** ⭐
- รีวิวแบบละเอียด (6 หมวด: Overall, Cleanliness, Service, Facilities, Location, Value)
- ระบบ approval workflow (Pending → Approved/Rejected)
- การตอบกลับรีวิวจากฝั่ง management
- Featured reviews
- Analytics และ statistics

**Tables:**
- `Guest_Reviews` - รีวิวจากแขก

**Service Class:**
- `ReviewService.cs` - จัดการรีวิวทั้งหมด

### 4. **Communication History** 📧
- บันทึกการติดต่อทุกช่องทาง (Email, SMS, LINE, Phone, WhatsApp)
- แยก Inbound/Outbound
- เชื่อมโยงกับ Reservation
- Status tracking (Sent, Delivered, Read, Failed)

**Tables:**
- `Communication_Log` - ประวัติการติดต่อ

### 5. **Guest Segmentation** 🎯
- กลุ่มลูกค้าแบบอัตโนมัติและกำหนดเอง
- 6 กลุ่มเริ่มต้น: VIP, Repeat, New, Inactive, Families, Couples

**Tables:**
- `Guest_Segments` - กลุ่มลูกค้า
- `Customer_Segment_Assignments` - การจัดกลุ่มลูกค้า

### 6. **Birthday & Special Occasions** 🎂
- จัดเก็บวันเกิด, วันครบรอบ, วันแต่งงาน
- ระบบแจ้งเตือนอัตโนมัติ
- กำหนดได้ว่าจะแจ้งล่วงหน้ากี่วัน

**Tables:**
- `Customer_Special_Dates` - วันสำคัญ

## 📊 Views (Analytics)

สร้าง Views สำหรับ reporting และ analytics:

| View Name | Description |
|-----------|-------------|
| `vw_Guest_Profile_360` | ข้อมูลลูกค้าครบวงจร (การจอง, การเงิน, รีวิว, คะแนน) |
| `vw_Customer_Lifetime_Value` | CLV และ churn risk analysis |
| `vw_Guest_Preferences_Summary` | สรุปความชอบของแขกแต่ละคน |
| `vw_Loyalty_Program_Performance` | ประสิทธิภาพโปรแกรมสะสมคะแนน |
| `vw_Review_Analytics` | สถิติรีวิวทั้งหมด |
| `vw_Upcoming_Special_Occasions` | วันเกิด/วันครบรอบที่จะถึง |

## 🔧 Stored Procedures

| Procedure Name | Purpose |
|----------------|---------|
| `sp_UpsertGuestPreference` | เพิ่ม/แก้ไขความชอบของแขก |
| `sp_EarnLoyaltyPoints` | เพิ่มคะแนนสะสม |
| `sp_RedeemLoyaltyPoints` | แลกคะแนน |
| `sp_UpdateLoyaltyTier` | อัพเดตระดับสมาชิก |
| `sp_SubmitGuestReview` | ส่งรีวิว |
| `sp_LogCommunication` | บันทึกการติดต่อ |

## 🚀 Installation

### Step 1: Run Database Scripts
```sql
-- Run in order:
1. Database/07_CRM_System_Schema.sql
2. Database/08_CRM_Views_And_Procedures.sql
```

### Step 2: Service Classes (Already Created)
Service classes are located in:
- `/Class/Services/LoyaltyService.cs`
- `/Class/Services/ReviewService.cs`

## 📖 Usage Examples

### Loyalty Program

```csharp
// Initialize service
var loyaltyService = new LoyaltyService(connectionString);

// Earn points from reservation
loyaltyService.EarnPointsFromReservation(
    customerPhone: "0812345678",
    reservationId: 12345,
    totalAmount: 5000, // ฿5,000 = 50 points
    adminId: 1
);

// Redeem points for reward
loyaltyService.RedeemPoints(
    customerPhone: "0812345678",
    points: 500,
    rewardId: 1,
    description: "แลกส่วนลด 100 บาท",
    adminId: 1
);

// Get customer loyalty info
var loyaltyInfo = loyaltyService.GetLoyaltyInfo("0812345678");
Console.WriteLine($"Tier: {loyaltyInfo.TierName}");
Console.WriteLine($"Points: {loyaltyInfo.AvailablePoints}");
```

### Reviews

```csharp
// Initialize service
var reviewService = new ReviewService(connectionString);

// Submit review
var review = new ReviewSubmission
{
    ReservationId = 12345,
    CustomerPhone = "0812345678",
    OverallRating = 5,
    CleanlinessRating = 5,
    ServiceRating = 5,
    ReviewText = "ที่พักสวยมาก บริการดีเยี่ยม!",
    WouldRecommend = true,
    TravelType = "COUPLE"
};

var result = reviewService.SubmitReview(review);

// Get analytics
var analytics = reviewService.GetAnalytics();
Console.WriteLine($"Average Rating: {analytics.AvgOverallRating:F2}");
Console.WriteLine($"Total Reviews: {analytics.TotalReviews}");
```

## 🎨 UI Pages (To Be Created)

ต่อไปต้องสร้างหน้า UI:

### Priority 1 (Essential):
1. **Guest Profile 360°** (`/Admin/CRM/GuestProfile.aspx`)
   - แสดงข้อมูลลูกค้าทั้งหมด
   - ประวัติการจอง
   - คะแนนสะสม
   - รีวิว
   - ความชอบ

2. **Loyalty Dashboard** (`/Admin/CRM/LoyaltyDashboard.aspx`)
   - สถิติโปรแกรม
   - รายการสมาชิกแต่ละระดับ
   - Rewards management

3. **Review Management** (`/Admin/CRM/ReviewManagement.aspx`)
   - รีวิวที่รอ approval
   - ตอบกลับรีวิว
   - สถิติรีวิว

### Priority 2 (Nice to Have):
4. **Guest Segmentation** (`/Admin/CRM/Segmentation.aspx`)
   - จัดกลุ่มลูกค้า
   - สร้าง campaign ตามกลุ่ม

5. **Birthday Reminders** (`/Admin/CRM/Birthdays.aspx`)
   - วันเกิดที่จะถึง
   - ส่งโปรโมชั่น

## 📈 Automation Opportunities

Features ที่ควรทำให้อัตโนมัติ:

1. **Auto-award points on checkout** ✅
   - เมื่อเช็คเอาท์ให้คะแนนอัตโนมัติ

2. **Auto-tier upgrade** ✅
   - เมื่อคะแนนถึง threshold

3. **Birthday email automation**
   - ส่งอีเมลวันเกิดพร้อมโปรโมชั่น

4. **Review request email**
   - ส่งขอรีวิว 1 วันหลังเช็คเอาท์

5. **Win-back campaign**
   - ส่งโปรโมชั่นลูกค้าที่ไม่มานาน

## 🔗 Integration Points

ระบบ CRM เชื่อมโยงกับ:

- ✅ **Customer** table (existing)
- ✅ **Reservation** system
- ✅ **Payment_History** system
- ✅ **Account_Receipt** system
- ⏳ **Email Service** (for automation)
- ⏳ **LINE Messaging** (for notifications)

## 💡 Benefits

### For Business:
- 📈 เพิ่ม repeat customers
- 💰 เพิ่มรายได้จาก loyalty program
- ⭐ ปรับปรุงคุณภาพบริการจากรีวิว
- 🎯 Marketing ที่แม่นยำจาก segmentation

### For Guests:
- 🎁 รับของรางวัลและส่วนลด
- ⚡ บริการที่ตรงใจ (จำความชอบ)
- 🎂 ข้อเสนอพิเศษวันเกิด
- ✨ ประสบการณ์ที่ personalized

## 📝 Next Steps

1. ✅ Run database migration scripts
2. ⏳ Create Guest Profile 360° page
3. ⏳ Create Loyalty Dashboard
4. ⏳ Create Review Management page
5. ⏳ Integrate auto-point earning on checkout
6. ⏳ Set up email automation

## 🐛 Known Limitations

- Birthday automation ยังไม่มี scheduler (ต้องใช้ SQL Agent หรือ Windows Task Scheduler)
- Email service ยังไม่ได้เชื่อมโยง (ใช้ EmailService.cs ที่มีอยู่แล้ว)
- UI pages ยังไม่ได้สร้าง (เหลือแค่ backend)

## 📞 Support

สำหรับคำถามหรือปัญหา:
- ดูเอกสาร: `/Database/CRM_System_README.md` (ไฟล์นี้)
- ตรวจสอบ SQL scripts: `/Database/07_*.sql`, `/Database/08_*.sql`
- ตรวจสอบ Services: `/Class/Services/LoyaltyService.cs`, `/Class/Services/ReviewService.cs`

---

**Version:** 1.0
**Created:** 2025-01-24
**Author:** Claude Code
**Status:** Phase 1 Complete (Backend) ✅
