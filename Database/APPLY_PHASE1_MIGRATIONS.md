# 🚀 คำแนะนำการ Apply Migrations - PHASE 1

## 📦 ภาพรวม

PHASE 1 มี migrations ทั้งหมด **3 ไฟล์**:
1. `09_Add_Payment_Tracking_Enhancement.sql` - ระบบติดตามการชำระเงิน
2. `10_Add_Checkout_Status_Enhancement.sql` - ระบบเช็คเอาท์
3. `11_Add_Product_Images_System.sql` - ระบบจัดการรูปภาพ

---

## ⚠️ ข้อควรระวังก่อน Apply

### ✅ Checklist ก่อน Apply

- [ ] **Backup database ก่อน!** (สำคัญมาก!)
- [ ] ทดสอบบน **Development database** ก่อนเสมอ
- [ ] ตรวจสอบ database connection string
- [ ] ตรวจสอบ permissions (ต้องเป็น db_owner หรือ sysadmin)
- [ ] แจ้งทีมก่อน apply บน Production

### 🔍 ตรวจสอบก่อน Apply

```sql
-- ตรวจสอบว่าตารางเหล่านี้ยังไม่มี
SELECT * FROM sys.tables WHERE name IN ('Payment_History', 'Checkout_History', 'Product_Images')

-- ถ้าผลลัพธ์ว่างเปล่า = ยังไม่มี = ปลอดภัย
-- ถ้ามีแถวออกมา = มีแล้ว = ต้องตรวจสอบก่อน apply
```

---

## 🎯 วิธีที่ 1: Apply ทีละไฟล์ (แนะนำ)

### ใช้ SQL Server Management Studio (SSMS)

#### Step 1: เปิด Migration 09
1. เปิด **SQL Server Management Studio**
2. Connect to server
3. คลิก **File** → **Open** → **File...**
4. เลือก `Database/09_Add_Payment_Tracking_Enhancement.sql`
5. **อ่านทั้งไฟล์** เพื่อเข้าใจว่าจะทำอะไร
6. เลือก database `Taketime` จาก dropdown
7. คลิก **Execute** (F5) หรือกด ▶️
8. ตรวจสอบ **Messages** tab ว่ามี ✅ ทุกบรรทัดหรือไม่

**ผลลัพธ์ที่ต้องเห็น:**
```
✅ Payment_History table created successfully
✅ Index IX_Payment_History_Reservation created
✅ View vw_ReservationPaymentSummary created
✅ Stored procedure sp_RecordPayment created
✅ Function fn_GetRemainingBalance created
✅ Migrated X payment records
✅ Permissions granted
========================================
✅ Migration 09 completed successfully!
========================================
```

#### Step 2: เปิด Migration 10
1. เปิดไฟล์ `Database/10_Add_Checkout_Status_Enhancement.sql`
2. เลือก database `Taketime`
3. Execute (F5)
4. ตรวจสอบผลลัพธ์

**ผลลัพธ์ที่ต้องเห็น:**
```
✅ CheckoutDate column exists
✅ Checkout_History table exists
✅ View vw_CheckoutSummary exists
✅ Stored procedure sp_ProcessCheckout exists
========================================
✅ Migration 10 completed successfully!
========================================
```

#### Step 3: เปิด Migration 11
1. เปิดไฟล์ `Database/11_Add_Product_Images_System.sql`
2. เลือก database `Taketime`
3. Execute (F5)
4. ตรวจสอบผลลัพธ์

**ผลลัพธ์ที่ต้องเห็น:**
```
✅ Product_Images table exists
✅ Image_Upload_Log table exists
✅ View vw_AccommodationWithImages exists
✅ View vw_ItemsWithImages exists
✅ Stored procedure sp_SetMainImage exists
========================================
✅ Migration 11 completed successfully!
========================================
```

---

## 🚀 วิธีที่ 2: Apply ทั้งหมดพร้อมกัน (ขั้นสูง)

ใช้ script `APPLY_ALL_PHASE1_MIGRATIONS.sql` (ดูด้านล่าง)

### ใช้ SSMS
1. เปิดไฟล์ `APPLY_ALL_PHASE1_MIGRATIONS.sql`
2. เลือก database `Taketime`
3. **อ่านทั้งไฟล์ให้ละเอียด**
4. Execute (F5)

### ใช้ Command Line (sqlcmd)
```bash
sqlcmd -S localhost -d Taketime -i "APPLY_ALL_PHASE1_MIGRATIONS.sql" -o "migration_output.txt"
```

---

## 🔍 การตรวจสอบหลัง Apply

### ตรวจสอบตาราง
```sql
-- ตรวจสอบว่าตารางสร้างสำเร็จ
SELECT
    name AS TableName,
    create_date AS CreatedDate
FROM sys.tables
WHERE name IN ('Payment_History', 'Checkout_History', 'Product_Images', 'Image_Upload_Log')
ORDER BY create_date DESC

-- ควรได้ 4 แถว
```

### ตรวจสอบ Views
```sql
-- ตรวจสอบ views
SELECT name FROM sys.views
WHERE name IN (
    'vw_ReservationPaymentSummary',
    'vw_CheckoutSummary',
    'vw_AccommodationWithImages',
    'vw_ItemsWithImages'
)

-- ควรได้ 4 แถว
```

### ตรวจสอบ Stored Procedures
```sql
-- ตรวจสอบ stored procedures
SELECT name FROM sys.procedures
WHERE name LIKE 'sp_%'
AND name IN (
    'sp_RecordPayment',
    'sp_ProcessCheckout',
    'sp_SetMainImage',
    'sp_ReorderProductImages',
    'sp_DeleteProductImage'
)

-- ควรได้ 5 แถว
```

### ตรวจสอบ Functions
```sql
-- ตรวจสอบ functions
SELECT name FROM sys.objects
WHERE type IN ('FN', 'IF', 'TF')
AND name IN (
    'fn_GetRemainingBalance',
    'fn_CanCheckout',
    'fn_GetMainImageURL'
)

-- ควรได้ 3 แถว
```

### ตรวจสอบข้อมูล Migration (Payment_History)
```sql
-- ตรวจสอบว่ามีข้อมูล migrate มาจาก Account_Receipt หรือไม่
SELECT
    COUNT(*) as TotalPayments,
    SUM(CASE WHEN PaymentType = 'DEPOSIT' THEN 1 ELSE 0 END) as DepositCount,
    SUM(CASE WHEN PaymentType = 'FINAL' THEN 1 ELSE 0 END) as FinalCount
FROM Payment_History

-- ถ้ามีข้อมูลเดิมใน Account_Receipt จะ migrate มาแสดงที่นี่
```

---

## 🐛 Troubleshooting

### ปัญหา: "Object already exists"
**สาเหตุ:** Migration เคย run ไปแล้ว

**แก้ไข:**
```sql
-- ตรวจสอบว่ามีตารางอยู่แล้วหรือไม่
SELECT * FROM sys.tables WHERE name = 'Payment_History'

-- ถ้ามี ให้ skip migration นั้นไป หรือลบออกก่อน (ระวัง!)
-- DROP TABLE Payment_History -- ⚠️ อันตราย! จะลบข้อมูลด้วย
```

### ปัญหา: "Permission denied"
**สาเหตุ:** User ไม่มีสิทธิ์สร้าง objects

**แก้ไข:**
```sql
-- ต้อง login ด้วย account ที่มีสิทธิ์ db_owner
-- หรือขอ sysadmin ทำให้

-- ตรวจสอบ permissions
SELECT
    USER_NAME() as CurrentUser,
    IS_MEMBER('db_owner') as IsDbOwner,
    IS_SRVROLEMEMBER('sysadmin') as IsSysAdmin
```

### ปัญหา: "Foreign key constraint error"
**สาเหตุ:** ตารางที่ reference ถึงยังไม่มี

**แก้ไข:**
- ตรวจสอบว่ามีตาราง `Reservation`, `Admin`, `Account_Receipt` อยู่หรือไม่
- ถ้าไม่มี ต้อง apply migrations เก่าก่อน

### ปัญหา: Migration 09 ไม่ migrate ข้อมูล
**สาเหตุ:** ตาราง `Payment_History` มีข้อมูลอยู่แล้ว

**ตรวจสอบ:**
```sql
SELECT COUNT(*) FROM Payment_History
-- ถ้า > 0 แสดงว่ามีข้อมูลแล้ว จะไม่ migrate ซ้ำ
```

**แก้ไข:** (ถ้าต้องการ migrate ใหม่)
```sql
-- 1. Backup ข้อมูลเดิม
SELECT * INTO Payment_History_Backup FROM Payment_History

-- 2. ลบข้อมูล
TRUNCATE TABLE Payment_History

-- 3. Re-run section 5 ของ Migration 09
-- (copy เฉพาะส่วน "Migrate Existing Data")
```

---

## 📊 การ Verify ทั้งหมด

หลัง apply ทั้ง 3 migrations ให้ run script นี้:

```sql
PRINT '========================================'
PRINT 'PHASE 1 Migrations Verification'
PRINT '========================================'

-- Count tables
DECLARE @TableCount int
SELECT @TableCount = COUNT(*)
FROM sys.tables
WHERE name IN ('Payment_History', 'Checkout_History', 'Product_Images', 'Image_Upload_Log')
PRINT 'Tables created: ' + CAST(@TableCount AS nvarchar(10)) + ' / 4'

-- Count views
DECLARE @ViewCount int
SELECT @ViewCount = COUNT(*)
FROM sys.views
WHERE name IN ('vw_ReservationPaymentSummary', 'vw_CheckoutSummary',
               'vw_AccommodationWithImages', 'vw_ItemsWithImages')
PRINT 'Views created: ' + CAST(@ViewCount AS nvarchar(10)) + ' / 4'

-- Count stored procedures
DECLARE @ProcCount int
SELECT @ProcCount = COUNT(*)
FROM sys.procedures
WHERE name IN ('sp_RecordPayment', 'sp_ProcessCheckout',
               'sp_SetMainImage', 'sp_ReorderProductImages', 'sp_DeleteProductImage')
PRINT 'Stored procedures created: ' + CAST(@ProcCount AS nvarchar(10)) + ' / 5'

-- Count functions
DECLARE @FuncCount int
SELECT @FuncCount = COUNT(*)
FROM sys.objects
WHERE type IN ('FN', 'IF', 'TF')
AND name IN ('fn_GetRemainingBalance', 'fn_CanCheckout', 'fn_GetMainImageURL')
PRINT 'Functions created: ' + CAST(@FuncCount AS nvarchar(10)) + ' / 3'

PRINT ''
IF @TableCount = 4 AND @ViewCount = 4 AND @ProcCount = 5 AND @FuncCount = 3
    PRINT '✅ ALL MIGRATIONS APPLIED SUCCESSFULLY!'
ELSE
    PRINT '⚠️ SOME MIGRATIONS INCOMPLETE - PLEASE REVIEW'

PRINT '========================================'
```

**ผลลัพธ์ที่คาดหวัง:**
```
========================================
PHASE 1 Migrations Verification
========================================
Tables created: 4 / 4
Views created: 4 / 4
Stored procedures created: 5 / 5
Functions created: 3 / 3

✅ ALL MIGRATIONS APPLIED SUCCESSFULLY!
========================================
```

---

## 🔄 การ Rollback (ถ้าจำเป็น)

⚠️ **คำเตือน:** Rollback จะลบตารางและข้อมูลทั้งหมด!

### Rollback Migration 11 (Product Images)
```sql
-- ลบ stored procedures
DROP PROCEDURE IF EXISTS sp_DeleteProductImage
DROP PROCEDURE IF EXISTS sp_ReorderProductImages
DROP PROCEDURE IF EXISTS sp_SetMainImage

-- ลบ function
DROP FUNCTION IF EXISTS fn_GetMainImageURL

-- ลบ views
DROP VIEW IF EXISTS vw_ItemsWithImages
DROP VIEW IF EXISTS vw_AccommodationWithImages

-- ลบ tables
DROP TABLE IF EXISTS Image_Upload_Log
DROP TABLE IF EXISTS Product_Images
```

### Rollback Migration 10 (Checkout)
```sql
-- ลบ function
DROP FUNCTION IF EXISTS fn_CanCheckout

-- ลบ stored procedure
DROP PROCEDURE IF EXISTS sp_ProcessCheckout

-- ลบ view
DROP VIEW IF EXISTS vw_CheckoutSummary

-- ลบ table
DROP TABLE IF EXISTS Checkout_History

-- ลบ columns จาก Reservation
ALTER TABLE Reservation DROP CONSTRAINT IF EXISTS FK_Reservation_CheckoutAdmin
ALTER TABLE Reservation DROP COLUMN IF EXISTS CheckoutDate
ALTER TABLE Reservation DROP COLUMN IF EXISTS CheckoutBy_AdminID
ALTER TABLE Reservation DROP COLUMN IF EXISTS CheckoutNotes
ALTER TABLE Reservation DROP COLUMN IF EXISTS FinalSettlementAmount
```

### Rollback Migration 09 (Payment History)
```sql
-- ลบ function
DROP FUNCTION IF EXISTS fn_GetRemainingBalance

-- ลบ stored procedure
DROP PROCEDURE IF EXISTS sp_RecordPayment

-- ลบ view
DROP VIEW IF EXISTS vw_ReservationPaymentSummary

-- ลบ table
DROP TABLE IF EXISTS Payment_History
```

---

## 📞 ติดต่อ Support

ถ้ามีปัญหาในการ apply migrations:

1. **ตรวจสอบ error message ใน Messages tab**
2. **Screenshot error message**
3. **บันทึก output จาก verification script**
4. **ติดต่อทีมพัฒนา**

---

## 📚 เอกสารเพิ่มเติม

- [Migration 09 Details](./09_Add_Payment_Tracking_Enhancement.sql)
- [Migration 10 Details](./10_Add_Checkout_Status_Enhancement.sql)
- [Migration 11 Details](./11_Add_Product_Images_System.sql)
- [PHASE 1 Summary](../PHASE1_SUMMARY.md)

---

**เตรียมไว้แล้วครับ! Happy Migrating! 🚀**
