# นโยบายการตรวจสอบยอดเงินใน PMS

## 📋 สรุป

เอกสารนี้อธิบายนโยบายการตรวจสอบยอดเงินในระบบ PMS (Property Management System) ของ Take Time BangPhra โดยแบ่งตามแต่ละโหมดการทำงาน

---

## 🎯 นโยบายตามโหมดการทำงาน

### 1. 📝 **Reserve Mode (การจองใหม่)**
**ไฟล์:** `Reserve.aspx.cs` → Button1_Click (command = "reserve")

**นโยบาย:**
- ✅ อนุญาตให้จองด้วยมัดจำขั้นต่ำ (Minimum Deposit)
- ✅ ยอดมัดจำต้อง ≥ 80% ของยอดมัดจำขั้นต่ำ
- ✅ สร้างใบกำกับภาษีมัดจำ (IsDeposit = true)

**โค้ดที่เกี่ยวข้อง:**
```csharp
// Reserve.aspx.cs บรรทัด 3730
if (Convert.ToInt32(TextBox5.Text) < minDeposit * 0.8)
{
    TextBox5.Text = "0";
    ClientScript.RegisterStartupScript(this.GetType(), "myalert",
        "alert('กรุณาโอนยอดมัดจำจองมากกว่ายอดมัดจำจองขั้นต่ำ');", true);
    return;
}
```

---

### 2. ✏️ **Edit Mode (แก้ไขการจอง)**
**ไฟล์:** `Reserve.aspx.cs` → Button1_Click (command = "edit")

#### 2.1 กรณีไม่มีการมัดจำเพิ่ม
**นโยบาย:**
- ✅ อนุญาตให้แก้ไขข้อมูลการจองได้เสมอ
- ❌ ไม่สร้างใบกำกับภาษีใหม่ (ถ้าไม่ tick CheckBox2)

#### 2.2 กรณีมีการมัดจำเพิ่ม (CheckBox2.Checked = true)
**นโยบาย:**
- ✅ **อนุญาตให้มัดจำเพิ่มได้โดยไม่ต้องเช็คว่ายอดรวมเท่ากับยอด Total หรือไม่**
- ✅ สร้างใบกำกับภาษีมัดจำเพิ่มทันที (IsDeposit = true)
- ✅ บันทึก Payment_History และ Payment_Slips
- ✅ อัพโหลดสลิปการโอนเงิน

**โค้ดที่เกี่ยวข้อง:**
```csharp
// Reserve.aspx.cs บรรทัด 1416-1433
if (command == "edit")
{
    int Deposit = Convert.ToInt32(TextBox5.Text);
    if (CheckBox2.Checked == true && TextBox1.Text != "02")
    {
        Deposit += Convert.ToInt32(TextBox10.Text);
        IsDeposit = true;  // 🔑 สำคัญ: ตั้งเป็น true สำหรับมัดจำเพิ่ม

        if (CheckBox4.Checked == false)
        {
            // สร้างใบกำกับภาษีมัดจำเพิ่ม (ไม่เช็คยอดรวม)
            AddProductChargesToReceipt(Convert.ToInt32(id), dtReserve);
            string receiptId = createReceipt(id, Convert.ToDouble(TextBox10.Text),
                                            dtReserve, IsDeposit, docCreatedDate,
                                            CheckBox5.Checked);
            uploadSlip(id, receiptId);
        }
    }
}
```

**เหตุผล:**
- มัดจำเพิ่มเป็นเพียงการชำระเงินส่วนเพิ่มเติม ไม่จำเป็นต้องเท่ากับยอดรวม
- ลูกค้าอาจจ่ายมัดจำเพิ่มทีละน้อย จนกว่าจะครบยอดรวม
- ระบบจะตรวจสอบความครบถ้วนของยอดเงินใน CheckIn/CheckOut เท่านั้น

---

### 3. 🏨 **CheckIn Mode (เช็คอิน)**
**ไฟล์:** `Reserve.aspx.cs` → Button1_Click (command = "checkin")

**นโยบาย:**
- 🔒 **ยอดชำระต้องเท่ากับยอดคงเหลือ (Remaining Amount) เท่านั้น**
- 🔒 ไม่อนุญาตให้แก้ไขยอดชำระ (TextBox10 ถูกล็อค)
- ✅ สร้างใบกำกับภาษีเต็มจำนวน (IsDeposit = false)
- ✅ อัพเดทสถานะเป็น "เช็คอินแล้ว"

**โค้ดที่เกี่ยวข้อง:**
```csharp
// Reserve.aspx.cs บรรทัด 1884-1890
if (paymentAmount != remainingAmount && remainingAmount > 0)
{
    ClientScript.RegisterStartupScript(this.GetType(), "myalert",
        "alert('ยอดชำระต้องเท่ากับยอดคงเหลือ " +
        remainingAmount.ToString("N0") + " บาทเท่านั้น\\nไม่สามารถแก้ไขยอดได้');",
        true);
    return;  // 🚫 หยุดการทำงานถ้ายอดไม่ตรง
}
```

**ตัวอย่าง:**
```
Total Price:     10,000 บาท
Deposit Paid:     3,000 บาท
Remaining:        7,000 บาท

✅ ต้องจ่าย:      7,000 บาทเท่านั้น
❌ ไม่อนุญาต:    5,000 บาท (น้อยกว่า)
❌ ไม่อนุญาต:    8,000 บาท (มากกว่า)
```

---

### 4. 🚪 **CheckOut Mode (เช็คเอาท์)**
**ไฟล์:** `Checkout.aspx.cs` → LoadReservationData()

**นโยบาย:**
- 🔒 **ยอดชำระต้องครบ 100% ก่อนเช็คเอาท์**
- 🔒 Remaining Balance ต้องเป็น 0 หรือน้อยกว่า 0
- ❌ ไม่อนุญาตให้เช็คเอาท์ถ้ายังมียอดคงเหลือ
- ✅ อัพเดทสถานะเป็น "เช็คเอ้าท์แล้ว"

**โค้ดที่เกี่ยวข้อง:**
```csharp
// Checkout.aspx.cs บรรทัด 123-144
// ✅ STRICT VALIDATION: Must pay FULL amount before checkout
if (remainingBalance <= 0)
{
    lblPaymentStatus.Text = "✅ ชำระครบแล้ว";
    btnCheckout.Enabled = true;
}
else
{
    // 🔒 STRICT: ไม่อนุญาตให้เช็คเอาท์ถ้ายอดไม่ครบ 100%
    ShowWarning($"⚠️ ไม่สามารถเช็คเอาท์ได้<br/>" +
               $"กรุณาชำระเงินให้ครบ 100% ก่อนเช็คเอาท์<br/>" +
               $"<strong>ยอดคงเหลือ: {remainingBalance:N2} บาท</strong>");
    btnCheckout.Enabled = false;  // 🚫 ปิดปุ่มเช็คเอาท์
}
```

**เหตุผล:**
- ตามมาตรฐาน PMS ที่ดี ต้องชำระเงินครบก่อนเช็คเอาท์
- ป้องกันปัญหาการค้างชำระ
- อำนวยความสะดวกในการตรวจสอบบัญชี

---

### 5. 🛒 **RentMore Mode (จองของเช่าเพิ่ม)**
**ไฟล์:** `Reserve.aspx.cs` → Button1_Click (command = "rentmore")

**นโยบาย:**
- ✅ อนุญาตให้จองของเช่าเพิ่มได้
- ✅ สร้างใบกำกับภาษีสำหรับของเช่าเพิ่ม (IsDeposit = false)
- ✅ ยอดชำระต้องตรงกับยอดของเช่าเพิ่มเท่านั้น

**โค้ดที่เกี่ยวข้อง:**
```csharp
// Reserve.aspx.cs บรรทัด 1768-1790
if (command == "rentmore" && TextBox1.Text != "02")
{
    if (checkoldAccomRemoved == 0 && checkoldItemRemoved == 0 &&
        totalnew.ToString() == TextBox10.Text)  // 🔑 ยอดต้องตรงกับของเช่าเพิ่ม
    {
        if (CheckBox2.Checked == true)
        {
            IsDeposit = false;  // ไม่ใช่มัดจำ แต่เป็นค่าของเช่า
            string receiptId = createReceipt(id, Convert.ToDouble(TextBox10.Text),
                                            dtReserve, IsDeposit, docCreatedDate,
                                            CheckBox5.Checked);
            uploadSlip(id, receiptId);
        }
    }
}
```

---

## 🔐 การตรวจสอบยอดเงินใน createReceipt()

**ไฟล์:** `Reserve.aspx.cs` → createReceipt()

### กรณี IsDeposit = true (ใบเสร็จมัดจำ)
```csharp
// บรรทัด 2966-2988
if (IsDeposit == true)
{
    // ✅ ไม่มีการตรวจสอบยอดรวม
    // บันทึกเป็นรายการเดียว: "ค่ามัดจำที่พัก..."
    code.DatabaseInsert(conn,
        "INSERT INTO [dbo].[Account_Receipt_Detail] " +
        "Values ('1','" + ReceiptID + "',1,7," +
        "N'ค่ามัดจำที่พักของหมายเลขการจอง " + Reservation_ID + " [" + ReceiptID + "]'," +
        "'1',N'ครั้ง'," + Total_Amount + "," + Total_Amount + ")");
}
```

### กรณี IsDeposit = false (ใบเสร็จเต็มจำนวน)
```csharp
// บรรทัด 2960-3037
if (!IsDeposit)
{
    // ✅ มีการตรวจสอบยอดรวม
    AdjustReserveDataToMatch(dtReserve, Total_Amount, Reservation_ID);
}

// บันทึกรายละเอียดทุกรายการ
for (int i = 0; i < dtReserve.Rows.Count; i++)
{
    // คำนวณและบันทึก
    double calculatedAmount = TwoDecimalPoints(pricePerPiece * productAmount);
    receiptTotal += calculatedAmount;
}

// ตรวจสอบความถูกต้อง (tolerance 0.01 บาท)
if (Math.Abs(receiptTotal - Total_Amount) > 0.01)
{
    code2.Logs(conn, "Receipt Total Mismatch",
        $"Receipt {ReceiptID}: Expected {Total_Amount}, Calculated {receiptTotal}",
        "SYSTEM");
}
```

---

## 📊 สรุปเปรียบเทียบ

| โหมด | การตรวจสอบยอด | IsDeposit | บังคับใบเสร็จ |
|------|--------------|-----------|---------------|
| **Reserve** | ≥ 80% ของมัดจำขั้นต่ำ | ✅ true | ✅ |
| **Edit (มัดจำเพิ่ม)** | ❌ ไม่เช็ค | ✅ true | ✅ |
| **Edit (ไม่มัดจำเพิ่ม)** | ❌ ไม่เช็ค | - | ❌ |
| **CheckIn** | 🔒 = ยอดคงเหลือ | ❌ false | ✅ |
| **CheckOut** | 🔒 = 100% (0 คงเหลือ) | - | ✅ |
| **RentMore** | = ยอดของเช่าเพิ่ม | ❌ false | ✅ |

---

## 🎯 ประโยชน์ของนโยบายนี้

### ✅ ข้อดี

1. **ยืดหยุ่นในการมัดจำ**: อนุญาตให้ลูกค้าจ่ายมัดจำทีละน้อยระหว่างการจอง
2. **เข้มงวดในการเช็คอิน/เช็คเอาท์**: ป้องกันปัญหาค้างชำระ
3. **ตรวจสอบได้ง่าย**: มี Payment_History บันทึกทุกการชำระเงิน
4. **ตามมาตรฐาน PMS**: สอดคล้องกับ Best Practice ของ Property Management System

### 📈 ผลลัพธ์

- ✅ ลดข้อผิดพลาดในการบันทึกยอดเงิน
- ✅ เพิ่มความถูกต้องของใบกำกับภาษี
- ✅ อำนวยความสะดวกในการตรวจสอบบัญชี
- ✅ เพิ่มความโปร่งใสในการจัดการการเงิน

---

## 📝 การอัพเดท

**วันที่:** 2025-01-07
**เวอร์ชัน:** 1.0
**ผู้อัพเดท:** Claude AI Assistant
**Commit:** Incremental System Development

---

## 🔗 ไฟล์ที่เกี่ยวข้อง

1. **Reserve.aspx.cs** - การจอง, แก้ไข, เช็คอิน, จองเพิ่ม
2. **Checkout.aspx.cs** - การเช็คเอาท์
3. **Payment_History** - บันทึกประวัติการชำระเงิน
4. **Account_Receipt** - ใบกำกับภาษี/ใบเสร็จ
5. **Payment_Slips** - สลิปการโอนเงิน

---

**หมายเหตุ:** นโยบายนี้อาจมีการปรับปรุงเพิ่มเติมในอนาคตตามความต้องการของธุรกิจ
