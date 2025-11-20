# แนวทางการปรับปรุงระบบบัญชี Phase 4

## สรุปความต้องการ

### 1. ปรับปรุง CheckDocument_New
- ใช้ Account_Receipt_Detail สำหรับดูรายละเอียดสินค้า/บริการในแต่ละใบเสร็จ
- แยก Category รายได้ให้ชัดเจน:
  - **หมวด 1**: จองพัก (เข้าพักในช่วง) - **ไม่รวมมัดจำ**
  - **หมวด 2**: มัดจำที่จ่ายในช่วง แต่เข้าพักนอกช่วง
  - **หมวด 3**: ขายของที่ไม่อ้างอิงการจอง
  - **หมวด 4**: รายได้อื่นๆ
- เพิ่มส่วนสรุปค่าใช้จ่าย (Payment Voucher)
- คำนวณกำไรสุทธิ (รายได้ - ค่าใช้จ่าย)

### 2. แก้ไขหน้า Reserve - การเช็คอิน
- เมื่อกดเช็คอินโดยไม่เลือก checkbox ชำระเงิน:
  - แสดง Alert ที่ชัดเจนว่า "ยังไม่ได้เช็คอินจริง"
  - Redirect ไป ReserveTable
  - **สถานะการจองไม่เปลี่ยน**

---

## สิ่งที่ทำเสร็จแล้ว

### ✅ 1. Database Views และ Functions (PHASE4_Migration_01)

#### Views ที่สร้างแล้ว:

**vw_Revenue_Detail_By_Items**
- แสดงรายละเอียดใบเสร็จทุกรายการพร้อม items
- รวมข้อมูลการจอง check-in date
- แยกประเภทรายได้

```sql
SELECT * FROM vw_Revenue_Detail_By_Items
WHERE Created_Date >= '2025-11-01' AND Created_Date < '2025-12-01';
```

**vw_Deposit_Receipts**
- ติดตามใบเสร็จมัดจำทั้งหมด
- แสดง check-in date vs receipt date
- ใช้สำหรับจัด Category 2

```sql
SELECT * FROM vw_Deposit_Receipts
WHERE Receipt_Date >= '2025-11-01' AND Receipt_Date <= '2025-11-30'
  AND (Checkin_Date < '2025-11-01' OR Checkin_Date > '2025-11-30');
```

**vw_Expense_Summary**
- สรุปค่าใช้จ่ายจาก Payment Vouchers
- แสดงรายละเอียดแต่ละรายการ
- จัดกลุ่มตาม vendor

```sql
SELECT * FROM vw_Expense_Summary
WHERE Payment_Date >= '2025-11-01' AND Payment_Date <= '2025-11-30';
```

**vw_Revenue_By_Category**
- จัดหมวดรายได้อัตโนมัติ
- CAT1_CHECKIN, CAT2_DEPOSIT, CAT3_PRODUCT, CAT4_OTHER

```sql
SELECT Category, SUM(Total_Amount) AS Total
FROM vw_Revenue_By_Category
WHERE Receipt_Date >= '2025-11-01' AND Receipt_Date <= '2025-11-30'
GROUP BY Category;
```

**vw_Profit_Loss_Summary**
- สรุปกำไร/ขาดทุนรายวัน
- รายได้ - ค่าใช้จ่าย = กำไรสุทธิ

```sql
SELECT
    Business_Date,
    Revenue,
    Expense,
    Net_Profit,
    CASE
        WHEN Net_Profit > 0 THEN 'กำไร'
        WHEN Net_Profit < 0 THEN 'ขาดทุน'
        ELSE 'พอดี'
    END AS Status
FROM vw_Profit_Loss_Summary
WHERE Business_Date >= '2025-11-01' AND Business_Date <= '2025-11-30'
ORDER BY Business_Date;
```

#### Functions และ Stored Procedures:

**fn_Categorize_Receipt**
- Function สำหรับจัดหมวดใบเสร็จ
- ใช้สำหรับ validation

```sql
SELECT dbo.fn_Categorize_Receipt('REC2511000123', '2025-11-01', '2025-11-30');
-- Returns: 1, 2, 3, or 4
```

**sp_Get_Revenue_Report**
- Stored procedure สำหรับดึงรายงานรายได้
- แยกตาม category ทั้ง 4 หมวด

```sql
EXEC sp_Get_Revenue_Report
    @StartDate = '2025-11-01',
    @EndDate = '2025-11-30';
```

---

## การติดตั้ง

### ขั้นตอนที่ 1: รัน Migration Script

```sql
USE [Taketime]
GO

-- รัน Phase 4 Migration
:r /path/to/Database/PHASE4_Migration_01_Enhanced_Revenue_Views.sql
GO

-- ตรวจสอบ
SELECT name, type_desc FROM sys.objects
WHERE name LIKE 'vw_%' OR name LIKE 'sp_Get_%' OR name LIKE 'fn_Categorize_%'
ORDER BY name;
```

### ขั้นตอนที่ 2: ทดสอบ Views

```sql
-- ทดสอบแต่ละ view
SELECT TOP 10 * FROM vw_Revenue_Detail_By_Items;
SELECT TOP 10 * FROM vw_Deposit_Receipts;
SELECT TOP 10 * FROM vw_Expense_Summary;
SELECT TOP 10 * FROM vw_Revenue_By_Category;
SELECT TOP 10 * FROM vw_Profit_Loss_Summary;

-- ทดสอบ stored procedure
EXEC sp_Get_Revenue_Report
    @StartDate = DATEADD(MONTH, -1, GETDATE()),
    @EndDate = GETDATE();
```

---

## การอัพเดท CheckDocument_New.aspx.cs

### ปัญหาปัจจุบัน:

1. **Category 1** รวมทั้งมัดจำและค่าที่พักเต็มจำนวน ทำให้นับซ้ำ
2. ไม่มีส่วนแสดงค่าใช้จ่าย (Payment Vouchers)
3. ไม่มีการคำนวณกำไรสุทธิ
4. ไม่แสดงรายละเอียด items ในใบเสร็จ

### แนวทางแก้ไข:

#### 1. แก้ไข GetCategory1Revenue()

**เดิม:**
```csharp
private DataTable GetCategory1Revenue(DateTime startDate, DateTime endDate, string status)
{
    // ดึงทุกใบเสร็จที่ check-in ในช่วง
    string query = @"
        SELECT DISTINCT ph.ID as PaymentHistoryID, ph.PaymentMethod, ph.PaymentAmount, ar.ID as ReceiptID
        FROM Payment_History ph
        INNER JOIN Reservation r ON ph.Reservation_ID = r.ID
        INNER JOIN Account_Receipt ar ON ph.Receipt_ID = ar.ID
        WHERE r.CheckinDate >= @StartDate AND r.CheckinDate <= @EndDate
          AND ar.Status LIKE @Status
          AND ph.Status = 'COMPLETED'
          AND ph.Receipt_ID IS NOT NULL
          AND ar.Reservation_ID > 0";
    // ...
}
```

**ใหม่ (แก้ไข):**
```csharp
private DataTable GetCategory1Revenue(DateTime startDate, DateTime endDate, string status)
{
    // ดึงเฉพาะใบเสร็จที่ check-in ในช่วง แต่ **ไม่รวมมัดจำ**
    string query = @"
        SELECT DISTINCT ph.ID as PaymentHistoryID, ph.PaymentMethod, ph.PaymentAmount, ar.ID as ReceiptID
        FROM Payment_History ph
        INNER JOIN Reservation r ON ph.Reservation_ID = r.ID
        INNER JOIN Account_Receipt ar ON ph.Receipt_ID = ar.ID
        WHERE r.CheckinDate >= @StartDate AND r.CheckinDate <= @EndDate
          AND ar.Status LIKE @Status
          AND ar.IsDeposit = 0  -- ⭐ เพิ่ม: ไม่รวมมัดจำ
          AND ph.Status = 'COMPLETED'
          AND ph.Receipt_ID IS NOT NULL
          AND ar.Reservation_ID > 0";

    var parameters = new Dictionary<string, object>
    {
        { "@StartDate", startDate },
        { "@EndDate", endDate },
        { "@Status", status }
    };

    return codeInstance.DatabaseQuerySafe(conn, query, parameters);
}
```

#### 2. แก้ไข GetCategory2Revenue()

**ใหม่ (แก้ไข):**
```csharp
private DataTable GetCategory2Revenue(DateTime startDate, DateTime endDate, string status)
{
    // ดึงเฉพาะใบเสร็จ **มัดจำ** ที่สร้างในช่วง แต่ check-in นอกช่วง
    string query = @"
        SELECT DISTINCT ph.ID as PaymentHistoryID, ph.PaymentMethod, ph.PaymentAmount, ar.ID as ReceiptID
        FROM Payment_History ph
        INNER JOIN Reservation r ON ph.Reservation_ID = r.ID
        INNER JOIN Account_Receipt ar ON ph.Receipt_ID = ar.ID
        WHERE ar.Created_Date >= @StartDate AND ar.Created_Date < DATEADD(DAY, 1, @EndDate)
          AND (r.CheckinDate < @StartDate OR r.CheckinDate > @EndDate OR r.CheckinDate IS NULL)
          AND ar.Status LIKE @Status
          AND ar.IsDeposit = 1  -- ⭐ เพิ่ม: เฉพาะมัดจำ
          AND ph.Status = 'COMPLETED'
          AND ph.Receipt_ID IS NOT NULL
          AND ar.Reservation_ID > 0";

    var parameters = new Dictionary<string, object>
    {
        { "@StartDate", startDate },
        { "@EndDate", endDate },
        { "@Status", status }
    };

    return codeInstance.DatabaseQuerySafe(conn, query, parameters);
}
```

#### 3. เพิ่ม Method สำหรับค่าใช้จ่าย

```csharp
private DataTable GetExpenseSummary(DateTime startDate, DateTime endDate, string status)
{
    string query = @"
        SELECT
            ap.ID,
            ap.Created_Date,
            ap.Total_Amount,
            ap.Vat,
            ap.Paid_How,
            v.Name AS Vendor_Name,
            v.Vendor_Group
        FROM Account_Payment ap
        LEFT JOIN Vendor v ON ap.Vendor_ID = v.ID
        WHERE ap.Created_Date >= @StartDate
          AND ap.Created_Date < DATEADD(DAY, 1, @EndDate)
          AND ap.Status = @Status
        ORDER BY ap.Created_Date";

    var parameters = new Dictionary<string, object>
    {
        { "@StartDate", startDate },
        { "@EndDate", endDate },
        { "@Status", status }
    };

    return codeInstance.DatabaseQuerySafe(conn, query, parameters);
}

private decimal GetExpenseByPaymentMethod(DataTable dt, int paymentMethodID)
{
    string paymentMethodName = GetPaymentMethodNameByLegacyId(paymentMethodID);
    if (string.IsNullOrEmpty(paymentMethodName))
    {
        return 0;
    }

    decimal total = 0;
    foreach (DataRow row in dt.Rows)
    {
        string paidHow = row["Paid_How"]?.ToString() ?? "";
        if (paidHow.Contains(paymentMethodName))
        {
            decimal amount = row["Total_Amount"] != DBNull.Value ?
                Convert.ToDecimal(row["Total_Amount"]) : 0;
            total += amount;
        }
    }
    return total;
}
```

#### 4. อัพเดท CalculateRevenue() เพื่อเพิ่มค่าใช้จ่าย

```csharp
private void CalculateRevenue(DateTime startDate, DateTime endDate)
{
    string status = "Normal";

    // ... existing revenue calculation code ...

    // ⭐ เพิ่ม: คำนวณค่าใช้จ่าย
    var expenseData = GetExpenseSummary(startDate, endDate, status);
    decimal expCash = GetExpenseByPaymentMethod(expenseData, 2);
    decimal expKBANK = GetExpenseByPaymentMethod(expenseData, 1);
    decimal expKTB = GetExpenseByPaymentMethod(expenseData, 4);
    decimal expDirector = GetExpenseByPaymentMethod(expenseData, 3);

    // แสดงค่าใช้จ่าย
    lblExpCash.Text = expCash.ToString("N2");
    lblExpKBANK.Text = expKBANK.ToString("N2");
    lblExpKTB.Text = expKTB.ToString("N2");
    lblExpDirector.Text = expDirector.ToString("N2");
    lblExpTotal.Text = (expCash + expKBANK + expKTB + expDirector).ToString("N2");

    // ⭐ คำนวณกำไรสุทธิ
    decimal totalRevenue = totalCash + totalKBANK + totalKTB + totalDirector;
    decimal totalExpense = expCash + expKBANK + expKTB + expDirector;
    decimal netProfit = totalRevenue - totalExpense;

    lblNetProfit.Text = netProfit.ToString("N2");
    lblNetProfit.ForeColor = netProfit >= 0 ? System.Drawing.Color.Green : System.Drawing.Color.Red;

    // Log ผลลัพธ์
    loggingService.LogAccountingOperation(
        "RevenueAndExpenseCalculation",
        $"Revenue: {totalRevenue:N2}, Expense: {totalExpense:N2}, Net Profit: {netProfit:N2}",
        true,
        GetCurrentUserId());
}
```

---

## การอัพเดท CheckDocument_New.aspx (UI)

### เพิ่ม Section สำหรับค่าใช้จ่าย

```asp
<!-- ส่วนรายได้ (เดิม) -->
<div class="revenue-section">
    <h3>📈 สรุปรายได้</h3>
    <!-- ... existing revenue table ... -->
</div>

<!-- ⭐ ส่วนค่าใช้จ่าย (ใหม่) -->
<div class="expense-section">
    <h3>📉 สรุปค่าใช้จ่าย</h3>
    <table class="summary-table">
        <thead>
            <tr>
                <th>รายการ</th>
                <th>เงินสด</th>
                <th>โอนกสิกร</th>
                <th>โอนกรุงไทย</th>
                <th>เงินกรรมการ</th>
                <th>รวม</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td>ค่าใช้จ่ายทั้งหมด</td>
                <td><asp:Label ID="lblExpCash" runat="server" Text="0.00"></asp:Label></td>
                <td><asp:Label ID="lblExpKBANK" runat="server" Text="0.00"></asp:Label></td>
                <td><asp:Label ID="lblExpKTB" runat="server" Text="0.00"></asp:Label></td>
                <td><asp:Label ID="lblExpDirector" runat="server" Text="0.00"></asp:Label></td>
                <td><strong><asp:Label ID="lblExpTotal" runat="server" Text="0.00"></asp:Label></strong></td>
            </tr>
        </tbody>
    </table>

    <!-- GridView สำหรับรายละเอียดค่าใช้จ่าย -->
    <asp:GridView ID="gvExpenses" runat="server" AutoGenerateColumns="False" CssClass="detail-grid">
        <Columns>
            <asp:BoundField DataField="ID" HeaderText="เลขที่" />
            <asp:BoundField DataField="Created_Date" HeaderText="วันที่" DataFormatString="{0:dd/MM/yyyy}" />
            <asp:BoundField DataField="Vendor_Name" HeaderText="ผู้ขาย" />
            <asp:BoundField DataField="Vendor_Group" HeaderText="ประเภท" />
            <asp:BoundField DataField="Total_Amount" HeaderText="ยอดรวม" DataFormatString="{0:N2}" />
            <asp:BoundField DataField="Vat" HeaderText="VAT" DataFormatString="{0:N2}" />
            <asp:BoundField DataField="Paid_How" HeaderText="วิธีชำระ" />
        </Columns>
    </asp:GridView>
</div>

<!-- ⭐ ส่วนกำไร/ขาดทุน (ใหม่) -->
<div class="profit-section">
    <h3>💰 สรุปกำไรสุทธิ</h3>
    <table class="profit-table">
        <tr>
            <td>รายได้รวม:</td>
            <td><asp:Label ID="lblGrandTotal" runat="server" Text="0.00"></asp:Label> บาท</td>
        </tr>
        <tr>
            <td>ค่าใช้จ่ายรวม:</td>
            <td><asp:Label ID="lblExpTotal2" runat="server" Text="0.00"></asp:Label> บาท</td>
        </tr>
        <tr class="net-profit-row">
            <td><strong>กำไรสุทธิ:</strong></td>
            <td><strong><asp:Label ID="lblNetProfit" runat="server" Text="0.00"></asp:Label> บาท</strong></td>
        </tr>
    </table>
</div>
```

### CSS สำหรับ UI ใหม่

```css
.expense-section {
    background-color: #fff3e0;
    padding: 20px;
    border-radius: 8px;
    margin: 20px 0;
}

.profit-section {
    background-color: #e8f5e9;
    padding: 20px;
    border-radius: 8px;
    margin: 20px 0;
    border: 2px solid #4caf50;
}

.net-profit-row {
    font-size: 1.2em;
    background-color: #c8e6c9;
}

.detail-grid {
    width: 100%;
    margin-top: 15px;
}

.detail-grid th {
    background-color: #ff9800;
    color: white;
    padding: 10px;
}

.detail-grid td {
    padding: 8px;
    border-bottom: 1px solid #ddd;
}
```

---

## การแก้ไข Reserve.aspx.cs - Check-in Validation

### ปัญหาปัจจุบัน:

บรรทัด 1757-1764 มีการจัดการอยู่แล้ว แต่ **ยังเช็คอินจริง**:

```csharp
else
{
    // ถ้าไม่ check checkbox
    ClientScript.RegisterStartupScript(this.GetType(), "myalert",
        "alert('กรุณาเลือก \"ชำระเงิน\" และกรอกยอดเงิน');", true);
}
Response.Redirect("/ReserveTable", false);  // ⚠️ Redirect แต่อาจเช็คอินแล้ว
```

### แนวทางแก้ไข:

**ปรับโค้ดที่บรรทัด 1621-1764:**

```csharp
else if (command == "checkin" && Session["permission"].ToString() == "True" && TextBox1.Text != "02")
{
    var reservationDA = new ReservationDataAccess(conn);
    IsDeposit = false;

    // Upsert customer data
    code.UpsertCustomer(
        conn,
        TextBox1.Text,
        TextBox2.Text,
        TextBox3.Text,
        cleantext(TextBox8.Text),
        TextBox9.Text,
        "1"
    );

    // ⭐ เช็คว่า CheckBox2 ถูก check หรือไม่
    if (!CheckBox2.Checked || string.IsNullOrEmpty(TextBox10.Text))
    {
        // ❌ ไม่ได้ tick checkbox หรือไม่ได้กรอกยอดเงิน
        // ไม่ทำการเช็คอิน เพียงแค่แจ้งเตือน

        // เพิ่มข้อความเตือนที่ชัดเจน
        string alertMessage = @"
            ⚠️ ยังไม่ได้ทำการเช็คอิน!

            กรุณาติ๊กเลือก 'ชำระเงิน' และกรอกยอดเงินที่รับ
            จึงจะสามารถเช็คอินได้

            สถานะการจองยังไม่เปลี่ยนแปลง";

        ClientScript.RegisterStartupScript(
            this.GetType(),
            "checkinWarning",
            $"alert('{alertMessage.Replace("\n", "\\n").Replace("'", "\\'")}');",
            true);

        // Log การพยายามเช็คอินโดยไม่ชำระเงิน
        try
        {
            var loggingService = new LoggingService(conn);
            loggingService.LogAccountingOperation(
                "CheckInAttemptWithoutPayment",
                $"User attempted to check-in Reservation ID: {id} without payment checkbox",
                false,
                GetCurrentUserId(),
                Convert.ToInt64(id));
        }
        catch { }

        // Redirect กลับไป ReserveTable โดยไม่เปลี่ยนสถานะ
        Response.Redirect("/ReserveTable", false);
        HttpContext.Current.ApplicationInstance.CompleteRequest();
        return; // ⭐ สำคัญ: ออกจาก method ทันที
    }

    // ✅ ถ้า CheckBox2 ถูก check แล้ว ทำการเช็คอินตามปกติ
    if (CheckBox2.Checked && !string.IsNullOrEmpty(TextBox10.Text))
    {
        // ... existing check-in logic ...
        int paymentAmount = Convert.ToInt32(TextBox10.Text);
        // ... rest of check-in process ...

        reservationDA.CheckInReservation(Convert.ToInt32(id));

        // Log successful check-in
        try
        {
            var loggingService = new LoggingService(conn);
            loggingService.LogAccountingOperation(
                "CheckInCompleted",
                $"Successfully checked in Reservation ID: {id}, Payment: {paymentAmount}",
                true,
                GetCurrentUserId(),
                Convert.ToInt64(id));
        }
        catch { }

        Response.Redirect("/ReserveTable", false);
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }
}
```

### เพิ่ม JavaScript Confirmation (Optional)

เพิ่ม confirmation dialog ก่อนเช็คอิน:

```javascript
<asp:Button ID="btnCheckIn" runat="server" Text="เช็คอิน"
    OnClientClick="return confirmCheckIn();"
    OnClick="Button1_Click" />

<script type="text/javascript">
function confirmCheckIn() {
    var checkboxPayment = document.getElementById('<%= CheckBox2.ClientID %>');
    var amountTextbox = document.getElementById('<%= TextBox10.ClientID %>');

    if (!checkboxPayment.checked || amountTextbox.value == '') {
        alert('⚠️ คำเตือน!\n\nท่านยังไม่ได้เลือก "ชำระเงิน" หรือกรอกยอดเงิน\n\n' +
              'การกดเช็คอินโดยไม่ชำระเงิน จะไม่ทำการเช็คอินจริง\n' +
              'และสถานะการจองจะไม่เปลี่ยนแปลง\n\n' +
              'กรุณาติ๊กเลือก "ชำระเงิน" และกรอกยอดเงิน\nหากต้องการเช็คอิน');
        return false; // ยกเลิกการ submit
    }

    return confirm('ยืนยันการเช็คอิน?\n\nยอดชำระ: ' + amountTextbox.value + ' บาท');
}
</script>
```

---

## การทดสอบ

### 1. ทดสอบ Revenue Categories

```sql
-- ทดสอบ Category 1 (Check-in in range, no deposits)
SELECT ar.ID, ar.IsDeposit, r.CheckinDate, ar.Total_Amount
FROM Account_Receipt ar
INNER JOIN Reservation r ON ar.Reservation_ID = r.ID
WHERE r.CheckinDate >= '2025-11-01' AND r.CheckinDate <= '2025-11-30'
  AND ar.Status = 'Normal'
  AND ar.IsDeposit = 0
ORDER BY ar.Created_Date;

-- ทดสอบ Category 2 (Deposits paid in range, check-in outside)
SELECT ar.ID, ar.IsDeposit, ar.Created_Date, r.CheckinDate, ar.Total_Amount
FROM Account_Receipt ar
INNER JOIN Reservation r ON ar.Reservation_ID = r.ID
WHERE ar.Created_Date >= '2025-11-01' AND ar.Created_Date <= '2025-11-30'
  AND ar.Status = 'Normal'
  AND ar.IsDeposit = 1
  AND (r.CheckinDate < '2025-11-01' OR r.CheckinDate > '2025-11-30' OR r.CheckinDate IS NULL)
ORDER BY ar.Created_Date;
```

### 2. ทดสอบ Check-in Validation

1. เปิดหน้า Reserve.aspx?command=checkin&id=XXX
2. **ไม่ tick** CheckBox "ชำระเงิน"
3. กดปุ่ม "เช็คอิน"
4. **คาดหวัง:**
   - แสดง Alert "⚠️ ยังไม่ได้ทำการเช็คอิน!"
   - Redirect ไป ReserveTable
   - สถานะการจองยังคงเป็น "มัดจำแล้ว" (ไม่เปลี่ยนเป็น "เช็คอินแล้ว")
   - มี log บันทึกใน System_Logs

5. **Tick** CheckBox "ชำระเงิน" และกรอกยอดเงิน
6. กดปุ่ม "เช็คอิน" อีกครั้ง
7. **คาดหวัง:**
   - เช็คอินสำเร็จ
   - สถานะเปลี่ยนเป็น "เช็คอินแล้ว"
   - สร้างใบเสร็จ
   - มี log บันทึกใน System_Logs

### 3. ทดสอบ Expense Section

```sql
-- สร้าง Payment Voucher ทดสอบ
INSERT INTO Account_Payment (ID, Created_Date, Vendor_ID, Total_Amount, Vat, Paid_How, Status)
VALUES ('PAY2511000001', '2025-11-15', 1, 5000, 350, 'เงินโอน บัญชี ธ.กสิกรไทย', 'Normal');

-- เรียก CheckDocument_New และตรวจสอบว่า
-- 1. มีส่วนแสดงค่าใช้จ่าย
-- 2. ยอดถูกต้อง
-- 3. กำไรสุทธิคำนวณถูกต้อง (รายได้ - ค่าใช้จ่าย)
```

---

## สรุป Checklist

### Database
- [ ] รัน PHASE4_Migration_01_Enhanced_Revenue_Views.sql
- [ ] ทดสอบ Views ทั้งหมด
- [ ] ทดสอบ sp_Get_Revenue_Report

### CheckDocument_New.aspx.cs
- [ ] แก้ไข GetCategory1Revenue() - เพิ่ม IsDeposit = 0
- [ ] แก้ไข GetCategory2Revenue() - เพิ่ม IsDeposit = 1
- [ ] เพิ่ม GetExpenseSummary() method
- [ ] เพิ่ม GetExpenseByPaymentMethod() method
- [ ] อัพเดท CalculateRevenue() - เพิ่มการคำนวณค่าใช้จ่าย
- [ ] เพิ่ม LoadExpenseDetails() method
- [ ] อัพเดท logging

### CheckDocument_New.aspx (UI)
- [ ] เพิ่ม expense section
- [ ] เพิ่ม GridView สำหรับรายละเอียดค่าใช้จ่าย
- [ ] เพิ่ม profit/loss summary section
- [ ] เพิ่ม CSS สำหรับ styling

### Reserve.aspx.cs
- [ ] แก้ไข check-in validation logic (บรรทัด 1757-1764)
- [ ] เพิ่ม return statement เมื่อไม่ได้ tick checkbox
- [ ] ปรับปรุง alert message ให้ชัดเจน
- [ ] เพิ่ม logging สำหรับ check-in attempts

### Reserve.aspx (Optional)
- [ ] เพิ่ม JavaScript confirmation dialog
- [ ] ปรับปรุง UI สำหรับ check-in section

### Testing
- [ ] ทดสอบ revenue categories ทั้ง 4 หมวด
- [ ] ทดสอบ expense calculation
- [ ] ทดสอบ net profit calculation
- [ ] ทดสอบ check-in validation (ไม่ tick checkbox)
- [ ] ทดสอบ check-in success (tick checkbox)
- [ ] ตรวจสอบ logs ใน System_Logs

---

## ไฟล์ที่ต้องแก้ไข

1. **Database/PHASE4_Migration_01_Enhanced_Revenue_Views.sql** ✅ (สร้างแล้ว)
2. **Take Time BangPhra/Account/CheckDocument_New.aspx.cs** (ต้องแก้ไข)
3. **Take Time BangPhra/Account/CheckDocument_New.aspx** (ต้องแก้ไข)
4. **Take Time BangPhra/Reserve.aspx.cs** (ต้องแก้ไข)
5. **Take Time BangPhra/Reserve.aspx** (Optional)

---

## การ Commit

เมื่อทำเสร็จแล้ว commit ด้วยข้อความ:

```
✨ Phase 4: Enhanced accounting system with expense tracking

## Changes

### Database
- Create views for detailed revenue tracking by items
- Add expense summary views from Payment Vouchers
- Add profit/loss calculation views
- Create stored procedures for comprehensive reports

### CheckDocument_New
- Fix Category 1: Exclude deposits to prevent double counting
- Fix Category 2: Only include deposits paid in range but check-in outside
- Add expense section with Payment Voucher summary
- Calculate net profit (revenue - expenses)
- Improve logging for all operations

### Reserve Check-in
- Fix validation when check-in without payment checkbox
- Prevent status change if payment not confirmed
- Add clear alert messages for user guidance
- Add logging for check-in attempts

### Documentation
- Add PHASE4_IMPLEMENTATION_GUIDE.md with complete instructions
- Update accounting system documentation
```

---

## ติดต่อ/สอบถาม

หากมีปัญหาหรือข้อสงสัย:
1. ตรวจสอบ System_Logs
2. ทดสอบ Views ใน database
3. ตรวจสอบ validation logic

---

**หมายเหตุ:** เอกสารนี้เป็น guide สำหรับการพัฒนา ควรทดสอบทุก feature ในสภาพแวดล้อม development ก่อนนำขึ้น production
