# 🤔 Migration 05: Which File Should I Use?

You have **4 versions** of Migration 05. This guide helps you choose the right one.

---

## 📁 Available Files:

| File | Purpose | Use When | Status |
|------|---------|----------|--------|
| **PHASE1_Migration_05b_Payment_Slips_SIMPLE.sql** | ✅ Create Payment_Slips table only | You just want Payment_Slips table | ✅ **RECOMMENDED** |
| **PHASE1_Migration_05a_Fix_Account_Receipt.sql** | Add columns to Account_Receipt | You have Account_Receipt and want to add columns | ✅ Optional |
| **PHASE1_Migration_05_Payment_Slips.sql** | Full migration with all features | Advanced users only | ⚠️ Complex |
| **PHASE1_Migration_05b_Payment_Slips_NO_UPDATE.sql** | Old version | Don't use (has GOTO bugs) | ❌ Deprecated |

---

## 🎯 Quick Decision Tree:

### **Question 1: Do you just want to create Payment_Slips table?**

**YES** → Use **`PHASE1_Migration_05b_Payment_Slips_SIMPLE.sql`**
- ✅ Simple and safe
- ✅ Creates Payment_Slips table
- ✅ Creates FK to Reservation
- ✅ Creates indexes
- ✅ No complex dependencies
- ✅ Won't touch Account_Receipt

**Example:**
```sql
-- Just run this file:
-- PHASE1_Migration_05b_Payment_Slips_SIMPLE.sql
```

---

### **Question 2: Do you also need Account_Receipt columns?**

**YES** → Run **both** files in order:

**Step 1:** `PHASE1_Migration_05a_Fix_Account_Receipt.sql`
```sql
-- Adds these columns to Account_Receipt:
-- - HasPaymentSlip (bit)
-- - PaymentSlipRequired (bit)
```

**Step 2:** `PHASE1_Migration_05b_Payment_Slips_SIMPLE.sql`
```sql
-- Creates Payment_Slips table
```

---

## ✅ Recommended Approach (Easiest):

```
1️⃣ PHASE1_Migration_05b_Payment_Slips_SIMPLE.sql
```

That's it! This single file creates everything you need.

---

## 📋 What Each File Creates:

### **PHASE1_Migration_05b_Payment_Slips_SIMPLE.sql** (Recommended)

Creates:
- ✅ `Payment_Slips` table (17 columns)
- ✅ FK to `Reservation` (required)
- ✅ FK to `Account_Receipt` (if table exists)
- ✅ FK to `Admin` (if table exists)
- ✅ 2-3 indexes for performance
- ✅ Primary key constraint

Does NOT touch:
- ❌ Account_Receipt table
- ❌ Any existing data

---

### **PHASE1_Migration_05a_Fix_Account_Receipt.sql** (Optional)

Adds to `Account_Receipt`:
- ✅ `HasPaymentSlip` column (bit)
- ✅ `PaymentSlipRequired` column (bit)

Use this if:
- You need to track which receipts have slips
- You need to mark which payments require slips
- Your application code uses these columns

---

### **PHASE1_Migration_05_Payment_Slips.sql** (Advanced)

Full migration including:
- ✅ Everything in 05b
- ✅ Account_Receipt modifications
- ✅ Stored procedures
- ✅ Views
- ✅ Data migration
- ✅ Migration tracking

Use this if:
- You're confident with SQL
- You want all features
- You're okay with complexity

⚠️ **Warning:** This file is complex and may fail if your database structure differs.

---

## 🔄 Migration Order:

If using the **simple approach**:
```
1. PHASE1_Migration_05b_Payment_Slips_SIMPLE.sql
2. PHASE1_Migration_09_Payment_Tracking.sql
3. PHASE1_Migration_10_Checkout_Status.sql
4. PHASE1_Migration_11_Product_Images.sql
```

If using the **full approach**:
```
1. PHASE1_Migration_05a_Fix_Account_Receipt.sql (optional)
2. PHASE1_Migration_05b_Payment_Slips_SIMPLE.sql
3. PHASE1_Migration_09_Payment_Tracking.sql
4. PHASE1_Migration_10_Checkout_Status.sql
5. PHASE1_Migration_11_Product_Images.sql
```

---

## ✅ Verification:

After running Migration 05b (Simple), check:

```sql
-- Check table exists
SELECT * FROM sys.tables WHERE name = 'Payment_Slips';
-- Should return 1 row

-- Check columns
SELECT name FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.Payment_Slips')
ORDER BY column_id;
-- Should return 17 columns

-- Check FK exists
SELECT name FROM sys.foreign_keys
WHERE parent_object_id = OBJECT_ID('dbo.Payment_Slips');
-- Should return 1-4 rows (depending on which tables exist)

-- Check indexes
SELECT name FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.Payment_Slips')
AND name LIKE 'IX_%';
-- Should return 2-3 rows
```

---

## 🆘 Troubleshooting:

### **Error: "Cannot find the object dbo.Payment_Slips"**

**Cause:** Table creation failed (previous step in script)

**Solution:**
1. Check if Reservation table exists:
   ```sql
   SELECT * FROM sys.tables WHERE name = 'Reservation';
   ```
2. If Reservation doesn't exist, you need to create it first
3. Re-run the migration

---

### **Error: "GOTO statement references label that has not been declared"**

**Cause:** You're using the old `05b_NO_UPDATE` file

**Solution:** Use `05b_SIMPLE` instead:
```sql
-- Use this file:
PHASE1_Migration_05b_Payment_Slips_SIMPLE.sql

-- NOT this file:
PHASE1_Migration_05b_Payment_Slips_NO_UPDATE.sql
```

---

### **Error: "Invalid column name PaymentSlipRequired"**

**Cause:** You're using the original `05` file and Account_Receipt structure is different

**Solution:** Use the simple version instead:
```sql
PHASE1_Migration_05b_Payment_Slips_SIMPLE.sql
```

---

## 💡 Summary:

**For 99% of users:**
```
Use: PHASE1_Migration_05b_Payment_Slips_SIMPLE.sql
```

**If you need Account_Receipt columns:**
```
1. PHASE1_Migration_05a_Fix_Account_Receipt.sql
2. PHASE1_Migration_05b_Payment_Slips_SIMPLE.sql
```

---

**Created:** 2025-11-05
**Last Updated:** 2025-11-05
**Version:** 1.0
