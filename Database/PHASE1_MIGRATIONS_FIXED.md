# 🔧 Phase 1 Migrations - ลำดับที่ถูกต้อง (แก้ไขแล้ว)

## ⚠️ สำคัญ: ต้อง Apply ตามลำดับนี้!

เนื่องจาก Migration 09 ต้องการตาราง `Payment_Slips` ซึ่งถูกสร้างใน Migration 05
**จึงต้อง run Migration 05 ก่อน Migration 09**

---

## 📋 ลำดับการ Apply Migrations

### **Migration 1: Payment_Slips System (Migration 05)**
**ไฟล์:** `PHASE1_Migration_05_Payment_Slips.sql`

**ทำก่อนเสมอ!** ไฟล์นี้สร้างตาราง Payment_Slips ที่จำเป็นสำหรับ Migration 09

**วิธี Apply:**
```sql
-- 1. เปิด SSMS
-- 2. เชื่อมต่อ database Taketime
-- 3. เปิดไฟล์ PHASE1_Migration_05_Payment_Slips.sql
-- 4. กด Execute (F5)
```

**ผลลัพธ์ที่ควรเห็น:**
```
✓ Account_Receipt table exists
✓ Reservation table exists
Step 1: Creating Payment_Slips table...
✓ Payment_Slips table created successfully
...
✓ Migration completed successfully
✓ Migration recorded in Database_Migrations table
```

**สิ่งที่จะสร้าง:**
- ✅ ตาราง `Payment_Slips` (เก็บไฟล์สลิปการโอนเงิน)
- ✅ Foreign Keys ไปที่ Account_Receipt, Reservation
- ✅ Indexes สำหรับประสิทธิภาพ
- ✅ บันทึกใน Database_Migrations

---

### **Migration 2: Payment Tracking (Migration 09)**
**ไฟล์:** `PHASE1_Migration_09_Payment_Tracking.sql`

**ต้อง run หลัง Migration 05 เสร็จแล้ว!**

**วิธี Apply:**
```sql
-- 1. ตรวจสอบว่า run Migration 05 เสร็จแล้ว:
SELECT * FROM Payment_Slips;  -- ถ้า error แสดงว่ายังไม่มีตาราง

-- 2. ถ้ามีตารางแล้ว ให้เปิดไฟล์ PHASE1_Migration_09_Payment_Tracking.sql
-- 3. กด Execute (F5)
```

**ผลลัพธ์ที่ควรเห็น:**
```
✅ Payment_History table created successfully
✅ FK_Payment_History_PaymentSlip created
✅ Index IX_Payment_History_Reservation created
✅ Index IX_Payment_History_Date created
✅ View vw_ReservationPaymentSummary created
✅ Stored procedure sp_RecordPayment created
✅ Function fn_GetRemainingBalance created
Migrated XX payment records
✅ Migration 09 completed successfully!
```

**สิ่งที่จะสร้าง:**
- ✅ ตาราง `Payment_History` (ประวัติการชำระเงิน)
- ✅ View `vw_ReservationPaymentSummary` (สรุปการชำระ)
- ✅ SP `sp_RecordPayment` (บันทึกการชำระ)
- ✅ Function `fn_GetRemainingBalance` (คำนวณยอดค้าง)
- ✅ Auto-migrate ข้อมูลเดิมจาก Account_Receipt

---

### **Migration 3: Checkout Status (Migration 10)**
**ไฟล์:** `PHASE1_Migration_10_Checkout_Status.sql`

**ไม่ต้องรอ Migration อื่น สามารถ run ได้เลย**

**วิธี Apply:**
```sql
-- เปิดไฟล์ PHASE1_Migration_10_Checkout_Status.sql
-- กด Execute (F5)
```

**ผลลัพธ์ที่ควรเห็น:**
```
✅ Added CheckoutDate column
✅ Added CheckoutBy_AdminID column
✅ Added CheckoutNotes column
✅ Added FinalSettlementAmount column
✅ Checkout_History table created
✅ View vw_CheckoutSummary created
✅ Stored procedure sp_ProcessCheckout created
✅ Function fn_CanCheckout created
✅ Migration 10 completed successfully!
```

**สิ่งที่จะสร้าง:**
- ✅ เพิ่ม 4 columns ในตาราง Reservation
- ✅ ตาราง `Checkout_History` (ประวัติเช็คเอาท์)
- ✅ View `vw_CheckoutSummary` (สรุปเช็คเอาท์)
- ✅ SP `sp_ProcessCheckout` (ทำเช็คเอาท์)
- ✅ Function `fn_CanCheckout` (ตรวจสอบเช็คเอาท์ได้หรือไม่)

---

### **Migration 4: Product Images (Migration 11)**
**ไฟล์:** `PHASE1_Migration_11_Product_Images.sql`

**ไม่ต้องรอ Migration อื่น สามารถ run ได้เลย**

**วิธี Apply:**
```sql
-- เปิดไฟล์ PHASE1_Migration_11_Product_Images.sql
-- กด Execute (F5)
```

**ผลลัพธ์ที่ควรเห็น:**
```
✅ Product_Images table created
✅ Image_Upload_Log table created
✅ Index IX_Product_Images_Product created
✅ Index IX_Product_Images_Main created
✅ View vw_AccommodationWithImages created
✅ View vw_ItemsWithImages created
✅ Stored procedure sp_SetMainImage created
✅ Stored procedure sp_ReorderProductImages created
✅ Stored procedure sp_DeleteProductImage created
✅ Function fn_GetMainImageURL created
✅ Migration 11 completed successfully!
```

**สิ่งที่จะสร้าง:**
- ✅ ตาราง `Product_Images` (เก็บรูปภาพสินค้า)
- ✅ ตาราง `Image_Upload_Log` (audit trail)
- ✅ View `vw_AccommodationWithImages` (ที่พักพร้อมรูป)
- ✅ View `vw_ItemsWithImages` (อุปกรณ์พร้อมรูป)
- ✅ SP `sp_SetMainImage`, `sp_ReorderProductImages`, `sp_DeleteProductImage`
- ✅ Function `fn_GetMainImageURL`

---

## 🔄 สรุปลำดับ:

```
1️⃣ PHASE1_Migration_05_Payment_Slips.sql       (ต้องทำก่อนเสมอ!)
2️⃣ PHASE1_Migration_09_Payment_Tracking.sql    (ทำหลัง 05)
3️⃣ PHASE1_Migration_10_Checkout_Status.sql     (ทำได้เลย)
4️⃣ PHASE1_Migration_11_Product_Images.sql      (ทำได้เลย)
```

**หรือ run ทั้งหมดตามลำดับ:**
```
05 → 09 → 10 → 11
```

---

## ✅ Verification Script

หลัง apply ครบทั้ง 4 ไฟล์ ให้รันคำสั่งนี้:

```sql
USE [Taketime];
GO

PRINT '========================================';
PRINT 'Phase 1 Migrations Verification';
PRINT '========================================';
PRINT '';

-- Check Tables
PRINT 'Checking Tables:';
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Payment_Slips')
    PRINT '✅ Payment_Slips exists'
ELSE
    PRINT '❌ Payment_Slips MISSING';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Payment_History')
    PRINT '✅ Payment_History exists'
ELSE
    PRINT '❌ Payment_History MISSING';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Checkout_History')
    PRINT '✅ Checkout_History exists'
ELSE
    PRINT '❌ Checkout_History MISSING';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Product_Images')
    PRINT '✅ Product_Images exists'
ELSE
    PRINT '❌ Product_Images MISSING';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Image_Upload_Log')
    PRINT '✅ Image_Upload_Log exists'
ELSE
    PRINT '❌ Image_Upload_Log MISSING';

PRINT '';

-- Check Views
PRINT 'Checking Views:';
IF OBJECT_ID('dbo.vw_ReservationPaymentSummary', 'V') IS NOT NULL
    PRINT '✅ vw_ReservationPaymentSummary exists'
ELSE
    PRINT '❌ vw_ReservationPaymentSummary MISSING';

IF OBJECT_ID('dbo.vw_CheckoutSummary', 'V') IS NOT NULL
    PRINT '✅ vw_CheckoutSummary exists'
ELSE
    PRINT '❌ vw_CheckoutSummary MISSING';

IF OBJECT_ID('dbo.vw_AccommodationWithImages', 'V') IS NOT NULL
    PRINT '✅ vw_AccommodationWithImages exists'
ELSE
    PRINT '❌ vw_AccommodationWithImages MISSING';

IF OBJECT_ID('dbo.vw_ItemsWithImages', 'V') IS NOT NULL
    PRINT '✅ vw_ItemsWithImages exists'
ELSE
    PRINT '❌ vw_ItemsWithImages MISSING';

PRINT '';

-- Check Stored Procedures
PRINT 'Checking Stored Procedures:';
IF OBJECT_ID('dbo.sp_RecordPayment', 'P') IS NOT NULL
    PRINT '✅ sp_RecordPayment exists'
ELSE
    PRINT '❌ sp_RecordPayment MISSING';

IF OBJECT_ID('dbo.sp_ProcessCheckout', 'P') IS NOT NULL
    PRINT '✅ sp_ProcessCheckout exists'
ELSE
    PRINT '❌ sp_ProcessCheckout MISSING';

IF OBJECT_ID('dbo.sp_SetMainImage', 'P') IS NOT NULL
    PRINT '✅ sp_SetMainImage exists'
ELSE
    PRINT '❌ sp_SetMainImage MISSING';

IF OBJECT_ID('dbo.sp_ReorderProductImages', 'P') IS NOT NULL
    PRINT '✅ sp_ReorderProductImages exists'
ELSE
    PRINT '❌ sp_ReorderProductImages MISSING';

IF OBJECT_ID('dbo.sp_DeleteProductImage', 'P') IS NOT NULL
    PRINT '✅ sp_DeleteProductImage exists'
ELSE
    PRINT '❌ sp_DeleteProductImage MISSING';

PRINT '';

-- Check Functions
PRINT 'Checking Functions:';
IF OBJECT_ID('dbo.fn_GetRemainingBalance', 'FN') IS NOT NULL
    PRINT '✅ fn_GetRemainingBalance exists'
ELSE
    PRINT '❌ fn_GetRemainingBalance MISSING';

IF OBJECT_ID('dbo.fn_CanCheckout', 'FN') IS NOT NULL
    PRINT '✅ fn_CanCheckout exists'
ELSE
    PRINT '❌ fn_CanCheckout MISSING';

IF OBJECT_ID('dbo.fn_GetMainImageURL', 'FN') IS NOT NULL
    PRINT '✅ fn_GetMainImageURL exists'
ELSE
    PRINT '❌ fn_GetMainImageURL MISSING';

PRINT '';

-- Check Foreign Keys
PRINT 'Checking Foreign Keys:';
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payment_History_PaymentSlip')
    PRINT '✅ FK_Payment_History_PaymentSlip exists'
ELSE
    PRINT '⚠️ FK_Payment_History_PaymentSlip not created (Payment_Slips might not exist)';

PRINT '';

-- Show Record Counts
PRINT 'Record Counts:';
DECLARE @PaymentSlipsCount int, @PaymentHistoryCount int,
        @CheckoutHistoryCount int, @ProductImagesCount int;

SELECT @PaymentSlipsCount = COUNT(*) FROM Payment_Slips;
SELECT @PaymentHistoryCount = COUNT(*) FROM Payment_History;
SELECT @CheckoutHistoryCount = COUNT(*) FROM Checkout_History;
SELECT @ProductImagesCount = COUNT(*) FROM Product_Images;

PRINT 'Payment_Slips: ' + CAST(@PaymentSlipsCount AS nvarchar(10));
PRINT 'Payment_History: ' + CAST(@PaymentHistoryCount AS nvarchar(10));
PRINT 'Checkout_History: ' + CAST(@CheckoutHistoryCount AS nvarchar(10));
PRINT 'Product_Images: ' + CAST(@ProductImagesCount AS nvarchar(10));

PRINT '';
PRINT '========================================';
PRINT '✅ Verification Complete!';
PRINT '========================================';
GO
```

**Expected Output:**
```
✅ All tables exist (5 tables)
✅ All views exist (4 views)
✅ All stored procedures exist (5 SPs)
✅ All functions exist (3 functions)
✅ FK exists (if Payment_Slips was created first)
```

---

## 🆘 Troubleshooting

### **Error: "Invalid object name 'Payment_Slips'"**
**สาเหตุ:** ยังไม่ได้ run Migration 05

**วิธีแก้:**
```sql
-- 1. Run Migration 05 first
-- 2. Then run Migration 09 again
```

### **Error: "There is already an object named 'Payment_History'"**
**สาเหตุ:** เคย run migration นี้ไปแล้ว

**วิธีแก้:**
- ข้าม migration นี้ได้เลย
- หรือ drop table แล้ว run ใหม่:
```sql
DROP TABLE Payment_History;
-- แล้ว run migration 09 ใหม่
```

### **Error: FK constraint conflict**
**สาเหตุ:** มีข้อมูลใน PaymentSlip_ID ที่ไม่มีใน Payment_Slips

**วิธีแก้:**
```sql
-- ตรวจสอบข้อมูลที่ผิด
SELECT * FROM Payment_History
WHERE PaymentSlip_ID IS NOT NULL
AND PaymentSlip_ID NOT IN (SELECT ID FROM Payment_Slips);

-- แก้ไขโดยเซ็ตเป็น NULL
UPDATE Payment_History
SET PaymentSlip_ID = NULL
WHERE PaymentSlip_ID NOT IN (SELECT ID FROM Payment_Slips);
```

---

## 📝 หมายเหตุ

1. **Migration 05 มีระบบ migration tracking** - จะไม่ run ซ้ำถ้า apply ไปแล้ว
2. **Migration 09, 10, 11 ไม่มี migration tracking** - ระวังอย่า run ซ้ำ
3. **Backup database ก่อนเสมอ!**
4. **ควรทดสอบบน dev database ก่อนทำบน production**

---

**สร้างเมื่อ:** 2025-11-05
**แก้ไขล่าสุด:** 2025-11-05 (แก้ปัญหา Payment_Slips dependency)
