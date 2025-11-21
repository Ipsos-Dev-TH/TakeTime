# TakeTime BangPhra - Helper Classes Architecture

## 📚 ภาพรวม (Overview)

โฟลเดอร์ `Class/` เป็น **Service & Helper Layer** ที่รวบรวม business logic และฟังก์ชันที่ใช้ร่วมกันทั้งระบบ เพื่อลด code duplication และเพิ่มความปลอดภัย

## 🆕 Helper Classes ใหม่

### 1. ✅ ValidationHelper
**วัตถุประสงค์**: จัดการ validation และ sanitization ของข้อมูล input

**แทนที่ฟังก์ชัน**:
- `cleantext()` จาก Product/Default.aspx.cs, Voucher/Default.aspx.cs

**วิธีใช้**:
```csharp
using Take_Time_BangPhra.Class;

// ทำความสะอาดข้อความ (ลบ , ' ")
string cleaned = ValidationHelper.CleanText(userInput);

// Validate email
bool isValid = ValidationHelper.IsValidEmail("user@example.com");

// Validate เบอร์มือถือไทย
bool isValidPhone = ValidationHelper.IsValidThaiMobilePhone("0812345678");

// Validate เลขบัตรประชาชน (13 หลัก + checksum)
bool isValidId = ValidationHelper.IsValidThaiIdNumber("1234567890123");

// Normalize เบอร์โทร (เอา - space ออก)
string normalized = ValidationHelper.NormalizePhoneNumber("081-234-5678");
// Returns: "0812345678"
```

**Methods สำคัญ**:
- `CleanText(string)` - ลบ comma, quote
- `SanitizeForHtml(string)` - ป้องกัน XSS
- `IsValidEmail(string)` - Validate email
- `IsValidThaiMobilePhone(string)` - Validate เบอร์มือถือ 08x, 09x, 06x
- `IsValidThaiIdNumber(string)` - Validate เลขบัตรประชาชน 13 หลัก
- `IsValidThaiPostalCode(string)` - Validate รหัสไปรษณีย์ 5 หลัก
- `NormalizePhoneNumber(string)` - ทำให้เบอร์โทรเป็น format มาตรฐาน
- `IsPositiveNumber(string)` - เช็คว่าเป็นตัวเลขบวก
- `StripHtmlTags(string)` - ลบ HTML tags
- `TruncateText(string, int)` - ตัดข้อความ

---

### 2. ✅ DocumentHelper
**วัตถุประสงค์**: สร้างเลขที่เอกสารแบบ sequential (REC2501150001, PAY2501150001)

**แทนที่ฟังก์ชัน**:
- `createDocNumber()` จาก _Default.aspx.cs, _Default2.aspx.cs
- `GenerateDocumentNumber()` จาก DatabaseHelper.cs

**วิธีใช้**:
```csharp
using Take_Time_BangPhra.Class;

var docHelper = new DocumentHelper(conn);

// สร้างเลขที่ใบเสร็จ (REC + วันที่ + running number)
string receiptNo = docHelper.CreateReceiptNumber();
// Example: REC2501150001

// สร้างเลขที่ใบสำคัญจ่าย
string paymentNo = docHelper.CreatePaymentNumber();
// Example: PAY2501150001

// สร้างเลขที่การจอง
string reservationNo = docHelper.CreateReservationNumber();
// Example: RSV2501150001

// สร้างเลขเอกสารแบบกำหนด table และ วันที่
string customDoc = docHelper.CreateDocumentNumber(
    "Account_Receipt",
    "REC",
    new DateTime(2025, 1, 15)
);
// Returns: REC2501150001

// เช็คว่าเลขเอกสารมีอยู่แล้วหรือไม่
bool exists = docHelper.DocumentNumberExists("Account_Receipt", "REC2501150001");

// Parse เลขเอกสาร
var parts = docHelper.ParseDocumentNumber("REC2501150001");
// Returns: { DocType: "REC", Year: "25", Month: "01", Day: "15", Sequence: "001" }

// Get วันที่จากเลขเอกสาร
DateTime? date = docHelper.GetDocumentDate("REC2501150001");
// Returns: 2025-01-15
```

**Security**:
- ✅ ใช้ **parameterized queries** (ป้องกัน SQL Injection)
- ✅ ใช้ **table whitelist** (ป้องกัน table name injection)

---

### 3. ✅ AddressHelper
**วัตถุประสงค์**: จัดการข้อมูลที่อยู่ไทย (จังหวัด อำเภอ ตำบล รหัสไปรษณีย์)

**แทนที่ฟังก์ชัน**:
- `CheckAddressID()` จาก Product, Voucher, Reserve
- `getAddress()` จาก Product, Voucher, Receipt
- `LoadAddressDropdownsByPostalCode()` จาก Product

**วิธีใช้**:
```csharp
using Take_Time_BangPhra.Class;

var addressHelper = new AddressHelper(conn);

// หา Address.ID จากรหัสไปรษณีย์ + จังหวัด + อำเภอ + ตำบล
int? addressId = addressHelper.GetAddressId(
    "10110",
    "กรุงเทพมหานคร",
    "บางรัก",
    "สีลม"
);

// ดึงรายการจังหวัดจากรหัสไปรษณีย์
List<string> provinces = addressHelper.GetProvincesByPostalCode("10110");

// ดึงรายการอำเภอจากรหัสไปรษณีย์ + จังหวัด
List<string> districts = addressHelper.GetDistrictsByPostalCodeAndProvince(
    "10110",
    "กรุงเทพมหานคร"
);

// ดึงข้อมูลที่อยู่ทั้งหมดจากรหัสไปรษณีย์
var addressData = addressHelper.GetAddressHierarchyByPostalCode("10110");
// Returns: {
//   "Provinces": [...],
//   "Districts": [...],
//   "SubDistricts": [...]
// }

// Populate dropdowns อัตโนมัติ
addressHelper.PopulateAddressDropdowns(
    "10110",
    DropDownListProvince,
    DropDownListDistrict,
    DropDownListSubDistrict
);

// ตรวจสอบรหัสไปรษณีย์
bool exists = addressHelper.PostalCodeExists("10110");

// Format ที่อยู่แบบไทย
string formatted = addressHelper.FormatAddress(
    "123/45",
    "สีลม",
    "บางรัก",
    "กรุงเทพมหานคร",
    "10110"
);
// Returns: "123/45 ต.สีลม อ.บางรัก จ.กรุงเทพมหานคร 10110"

// Insert หรือ Update address (UPSERT)
int addrId = addressHelper.UpsertAddress(
    "10110",
    "กรุงเทพมหานคร",
    "บางรัก",
    "สีลม"
);
```

**Security**:
- ✅ ใช้ **parameterized queries** ทุก method

---

### 4. ✅ CustomerHelper
**วัตถุประสงค์**: จัดการข้อมูลลูกค้า (CRUD operations)

**แทนที่ฟังก์ชัน**:
- `fillData()` จาก Product, Voucher (DEPRECATED - SQL injection risk)
- `fillDataByPhone()` จาก Product (secure version)
- `fillDataFromCustomerTable()` จาก Product

**วิธีใช้**:
```csharp
using Take_Time_BangPhra.Class;

var customerHelper = new CustomerHelper(conn);

// ดึงข้อมูลลูกค้าจากเบอร์โทร
DataTable dtCustomer = customerHelper.GetCustomerByPhone("0812345678");

// ดึงข้อมูลลูกค้าจากเลขบัตรประชาชน
DataTable dtCustomer2 = customerHelper.GetCustomerByIdNumber("1234567890123");

// ดึงข้อมูลลูกค้าจาก ID
DataTable dtCustomer3 = customerHelper.GetCustomerById(123);

// ค้นหาลูกค้า (ชื่อ, เบอร์โทร)
DataTable dtSearch = customerHelper.SearchCustomers("สมชาย");

// สร้าง/แก้ไขลูกค้า (UPSERT - ป้องกัน duplicate)
var customerData = new CustomerData
{
    MobilePhone = "0812345678",
    Name = "สมชาย",
    NickName = "ชาย",
    FullName = "นายสมชาย ใจดี",
    Email = "somchai@example.com",
    IdNumber = "1234567890123",
    CustomerTypeId = 1,  // 1 = Individual, 2 = Corporate
    AddressId = 456,
    Address = "123 ถนนสุขุมวิท",
    Address1 = "อาคาร ABC ชั้น 5",
    BranchNumber = "00000",
    ComeFrom = "Facebook",
    Remark = "VIP Customer"
};

// Validate ข้อมูลก่อน save
List<string> errors = customerHelper.ValidateCustomerData(customerData);
if (errors.Count == 0)
{
    long customerId = customerHelper.UpsertCustomer(customerData);
    // Returns: Customer ID (existing or newly created)
}
else
{
    // Show validation errors
    foreach (string error in errors)
    {
        Response.Write(error);
    }
}

// เช็คว่าลูกค้ามีอยู่หรือไม่
bool exists = customerHelper.CustomerExistsByPhone("0812345678");

// ดึงประวัติการจองของลูกค้า
DataTable dtReservations = customerHelper.GetCustomerReservations(customerId, limit: 10);

// ดึงประวัติการชำระเงิน
DataTable dtPayments = customerHelper.GetCustomerPayments(customerId, limit: 10);

// ดึงสถิติลูกค้า
var stats = customerHelper.GetCustomerStatistics(customerId);
int totalReservations = (int)stats["TotalReservations"];
decimal totalSpend = (decimal)stats["TotalSpend"];
DateTime? lastVisit = stats["LastVisit"] as DateTime?;

// Populate dropdown ประเภทลูกค้า
customerHelper.PopulateCustomerTypeDropdown(DropDownListCustomerType);

// อัปเดตสถานะลูกค้า (1 = active, 0 = inactive)
customerHelper.UpdateCustomerStatus(customerId, 1);
```

**CustomerData Class**:
```csharp
var data = new CustomerData
{
    MobilePhone = "0812345678",  // ✅ Required
    Name = "สมชาย",
    NickName = "ชาย",
    FullName = "นายสมชาย ใจดี",
    Email = "somchai@example.com",  // ✅ Validated
    IdNumber = "1234567890123",     // ✅ Validated (13 digits + checksum)
    CustomerTypeId = 1,              // ✅ Required (1=Individual, 2=Corporate)
    AddressId = 456,
    Address = "123 ถนนสุขุมวิท",
    Address1 = "อาคาร ABC ชั้น 5",
    BranchNumber = "00000",          // สำหรับนิติบุคคล
    ComeFrom = "Facebook",           // จากช่องทางไหน
    Remark = "VIP Customer"
};

// สร้างจาก DataRow
CustomerData fromDb = CustomerData.FromDataRow(dtCustomer.Rows[0]);
```

**Validation**:
- ✅ เบอร์โทร: 10 หลัก, เริ่มต้น 06/08/09
- ✅ Email: RFC 5322 format
- ✅ เลขบัตรประชาชน: 13 หลัก + checksum validation

---

## 🔄 Migration Guide

### ❌ วิธีเก่า (Deprecated)
```csharp
// ❌ SQL Injection Risk - DON'T USE
_Default code = new _Default();
DataTable dt = code.DatabaseQuery(conn,
    "SELECT * FROM Customer WHERE Phone = '" + phone + "'");

// ❌ ฟังก์ชันกระจัดกระจาย - ไม่มีการ reuse
public string cleantext(string input)
{
    return input.Replace(",", "").Replace("'", "").Replace("\"", "");
}

// ❌ ฟังก์ชันซ้ำซ้อน - มีหลาย class
public string CheckAddressID(string zip, string province, ...)
{
    // Copy-paste code ใน Product, Voucher, Reserve
}
```

### ✅ วิธีใหม่ (Recommended)
```csharp
using Take_Time_BangPhra.Class;

// ✅ Secure & Centralized
code code = new code();  // ใช้ code class (ไม่ใช่ _Default)
var customerHelper = new CustomerHelper(conn);
var addressHelper = new AddressHelper(conn);
var docHelper = new DocumentHelper(conn);

// ✅ ใช้ parameterized queries
var parameters = new Dictionary<string, object> { { "@Phone", phone } };
DataTable dt = code.DatabaseQuerySafe(conn,
    "SELECT * FROM Customer WHERE Phone = @Phone",
    parameters);

// ✅ ใช้ helper classes (reusable, tested, secure)
string cleaned = ValidationHelper.CleanText(userInput);
int? addressId = addressHelper.GetAddressId(zip, province, district, subdistrict);
string docNo = docHelper.CreateReceiptNumber();
```

---

## 📖 ตัวอย่างการใช้งานจริง

### ตัวอย่างที่ 1: สร้างใบเสร็จ (Receipt Creation)
```csharp
using Take_Time_BangPhra.Class;

protected void btnCreateReceipt_Click(object sender, EventArgs e)
{
    var customerHelper = new CustomerHelper(conn);
    var docHelper = new DocumentHelper(conn);
    var addressHelper = new AddressHelper(conn);

    // 1. Validate & Clean Input
    string phone = ValidationHelper.NormalizePhoneNumber(TextBoxPhone.Text);
    if (!ValidationHelper.IsValidThaiMobilePhone(phone))
    {
        ShowError("เบอร์โทรศัพท์ไม่ถูกต้อง");
        return;
    }

    // 2. Get or Create Customer
    var customerData = new CustomerData
    {
        MobilePhone = phone,
        Name = ValidationHelper.CleanText(TextBoxName.Text),
        Email = TextBoxEmail.Text,
        CustomerTypeId = int.Parse(DropDownListCustomerType.SelectedValue),
        AddressId = addressHelper.GetAddressId(
            TextBoxPostalCode.Text,
            DropDownListProvince.SelectedItem.Text,
            DropDownListDistrict.SelectedItem.Text,
            DropDownListSubDistrict.SelectedItem.Text
        ) ?? 0
    };

    // Validate
    var errors = customerHelper.ValidateCustomerData(customerData);
    if (errors.Count > 0)
    {
        ShowError(string.Join("<br>", errors));
        return;
    }

    // UPSERT customer
    long customerId = customerHelper.UpsertCustomer(customerData);

    // 3. Generate Receipt Number
    string receiptNo = docHelper.CreateReceiptNumber();

    // 4. Insert Receipt
    var receiptParams = new Dictionary<string, object>
    {
        { "@ReceiptNo", receiptNo },
        { "@CustomerId", customerId },
        { "@Date", DateTime.Now },
        { "@TotalAmount", decimal.Parse(TextBoxAmount.Text) }
    };

    code code = new code();
    code.DatabaseInsertSafe(conn,
        "INSERT INTO Account_Receipt (ID, Customer_ID, Date, TotalAmount) " +
        "VALUES (@ReceiptNo, @CustomerId, @Date, @TotalAmount)",
        receiptParams);

    ShowSuccess($"สร้างใบเสร็จเลขที่ {receiptNo} เรียบร้อย");
}
```

### ตัวอย่างที่ 2: Auto-fill Customer Form จากเบอร์โทร
```csharp
using Take_Time_BangPhra.Class;

protected void TextBoxPhone_TextChanged(object sender, EventArgs e)
{
    var customerHelper = new CustomerHelper(conn);
    var addressHelper = new AddressHelper(conn);

    // Normalize phone number
    string phone = ValidationHelper.NormalizePhoneNumber(TextBoxPhone.Text);

    // Validate
    if (!ValidationHelper.IsValidThaiMobilePhone(phone))
    {
        ClearCustomerForm();
        return;
    }

    // Get customer data
    DataTable dtCustomer = customerHelper.GetCustomerByPhone(phone);

    if (dtCustomer.Rows.Count > 0)
    {
        DataRow row = dtCustomer.Rows[0];

        // Auto-fill form
        TextBoxName.Text = row["Name"].ToString();
        TextBoxNickName.Text = row["NickName"].ToString();
        TextBoxFullName.Text = row["FullName"].ToString();
        TextBoxEmail.Text = row["Email"].ToString();
        TextBoxIdNumber.Text = row["IDNumber"].ToString();

        // Select customer type
        DropDownListCustomerType.SelectedValue = row["Customer_Type_ID"].ToString();

        // Load address dropdowns
        if (!row.IsNull("PostalCode"))
        {
            string postalCode = row["PostalCode"].ToString();
            TextBoxPostalCode.Text = postalCode;

            addressHelper.PopulateAddressDropdowns(
                postalCode,
                DropDownListProvince,
                DropDownListDistrict,
                DropDownListSubDistrict
            );

            // Select values
            DropDownListProvince.SelectedValue = row["Province"].ToString();
            DropDownListDistrict.SelectedValue = row["District"].ToString();
            DropDownListSubDistrict.SelectedValue = row["SubDistrict"].ToString();
        }

        // Show customer stats
        var stats = customerHelper.GetCustomerStatistics(Convert.ToInt64(row["ID"]));
        LabelTotalReservations.Text = stats["TotalReservations"].ToString();
        LabelTotalSpend.Text = string.Format("{0:N2}", stats["TotalSpend"]);

        if (stats["LastVisit"] != null)
        {
            LabelLastVisit.Text = ((DateTime)stats["LastVisit"]).ToString("dd/MM/yyyy");
        }
    }
    else
    {
        // New customer - clear form
        ClearCustomerForm();
        LabelStatus.Text = "ลูกค้าใหม่";
    }
}
```

### ตัวอย่างที่ 3: Postal Code Autocomplete
```csharp
using Take_Time_BangPhra.Class;

protected void TextBoxPostalCode_TextChanged(object sender, EventArgs e)
{
    var addressHelper = new AddressHelper(conn);

    string postalCode = TextBoxPostalCode.Text;

    // Validate postal code format
    if (!ValidationHelper.IsValidThaiPostalCode(postalCode))
    {
        LabelPostalCodeError.Text = "รหัสไปรษณีย์ไม่ถูกต้อง (ต้องเป็น 5 หลัก)";
        return;
    }

    // Check if postal code exists
    if (!addressHelper.PostalCodeExists(postalCode))
    {
        LabelPostalCodeError.Text = "ไม่พบรหัสไปรษณีย์นี้ในระบบ";
        return;
    }

    // Auto-populate address dropdowns
    addressHelper.PopulateAddressDropdowns(
        postalCode,
        DropDownListProvince,
        DropDownListDistrict,
        DropDownListSubDistrict
    );

    LabelPostalCodeError.Text = "";
}
```

---

## 🏗️ โครงสร้างไฟล์ใหม่

```
Take Time BangPhra/
├── Code.cs                          ← PRIMARY utility class (database, logs, email)
│
├── Class/                           ← Service & Helper Layer
│   ├── ValidationHelper.cs          ← ✅ NEW: Input validation & sanitization
│   ├── DocumentHelper.cs            ← ✅ NEW: Document number generation
│   ├── AddressHelper.cs             ← ✅ NEW: Thai address management
│   ├── CustomerHelper.cs            ← ✅ NEW: Customer operations
│   ├── README.md                    ← ✅ This documentation
│   │
│   ├── DatabaseHelper.cs            ← Database abstraction (wrapper for Code.cs)
│   ├── ReservationService.cs        ← Reservation business logic
│   ├── PaymentService.cs            ← Payment processing
│   ├── CheckoutService.cs           ← Checkout operations
│   ├── AccountingService.cs         ← Accounting operations
│   ├── ProductService.cs            ← Product/inventory
│   ├── EmailService.cs              ← Email sending
│   └── ... (other services)
│
├── Default.aspx.cs                  ← Homepage (NO utility methods)
├── Reserve.aspx.cs                  ← Uses helpers + services
├── Reservation.aspx.cs              ← Uses helpers + services
│
├── Account/
│   └── Receipt.aspx.cs              ← Uses helpers + services
│
├── Product/
│   └── Default.aspx.cs              ← Uses helpers + services
│
└── ... (other modules)
```

---

## ✅ Best Practices

### 1. ใช้ `code` class แทน `_Default`
```csharp
// ✅ ถูกต้อง
code code = new code();
DataTable dt = code.DatabaseQuerySafe(conn, query, parameters);

// ❌ เลิกใช้
_Default code = new _Default();
```

### 2. ใช้ Helper Classes สำหรับฟังก์ชันที่ใช้บ่อย
```csharp
// ✅ ถูกต้อง - Reusable, tested, secure
var customerHelper = new CustomerHelper(conn);
DataTable dt = customerHelper.GetCustomerByPhone(phone);

// ❌ เลิกใช้ - Copy-paste code
public void FillData(string phone)
{
    // Duplicate code ในหลายไฟล์
}
```

### 3. Validate Input ก่อนเสมอ
```csharp
// ✅ ถูกต้อง
string phone = ValidationHelper.NormalizePhoneNumber(input);
if (!ValidationHelper.IsValidThaiMobilePhone(phone))
{
    ShowError("เบอร์โทรไม่ถูกต้อง");
    return;
}

// ❌ เลิกใช้ - ไม่ validate
string phone = TextBox1.Text;  // อาจเป็น "081-234-5678" หรือ "+6681234567"
```

### 4. ใช้ Parameterized Queries เสมอ
```csharp
// ✅ ถูกต้อง - ป้องกัน SQL Injection
var parameters = new Dictionary<string, object> { { "@Phone", phone } };
DataTable dt = code.DatabaseQuerySafe(conn,
    "SELECT * FROM Customer WHERE Phone = @Phone",
    parameters);

// ❌ เลิกใช้ - SQL Injection Risk
string sql = "SELECT * FROM Customer WHERE Phone = '" + phone + "'";
DataTable dt = code.DatabaseQuery(conn, sql);
```

### 5. ใช้ Helper Method สำหรับ Common Operations
```csharp
// ✅ ถูกต้อง
var docHelper = new DocumentHelper(conn);
string receiptNo = docHelper.CreateReceiptNumber();

// ❌ เลิกใช้ - Copy-paste code ในทุกไฟล์
string year = DateTime.Now.Year.ToString().Substring(2, 2);
string month = DateTime.Now.Month.ToString("00");
// ... (20+ lines of duplicate code)
```

---

## 🔄 การ Migrate ไฟล์เก่า

### ขั้นตอน:

1. **เพิ่ม using statement**
   ```csharp
   using Take_Time_BangPhra.Class;
   ```

2. **แทนที่ `_Default` ด้วย `code`**
   ```csharp
   // เก่า
   _Default code = new _Default();

   // ใหม่
   code code = new code();
   ```

3. **แทนที่ฟังก์ชัน local ด้วย Helper classes**
   ```csharp
   // เก่า
   private string cleantext(string input) { ... }

   // ใหม่
   // ลบฟังก์ชัน cleantext ออก, ใช้ ValidationHelper แทน
   string cleaned = ValidationHelper.CleanText(input);
   ```

4. **แทนที่ unsafe queries ด้วย safe queries**
   ```csharp
   // เก่า
   string sql = "SELECT * FROM Customer WHERE Phone = '" + phone + "'";
   DataTable dt = code.DatabaseQuery(conn, sql);

   // ใหม่
   var parameters = new Dictionary<string, object> { { "@Phone", phone } };
   DataTable dt = code.DatabaseQuerySafe(conn,
       "SELECT * FROM Customer WHERE Phone = @Phone",
       parameters);
   ```

5. **Test ทุกฟังก์ชัน**
   - ทดสอบ search, insert, update, delete
   - ทดสอบ validation
   - ทดสอบ edge cases

---

## 📊 ผลลัพธ์ที่คาดหวัง

### Before Migration:
- ❌ Database methods ซ้ำซ้อนใน 4 classes
- ❌ Helper methods กระจายใน 10+ ไฟล์
- ❌ SQL Injection ใน 25+ ไฟล์
- ❌ ไม่มี input validation
- ❌ Code duplication สูง

### After Migration:
- ✅ Database methods อยู่ใน 1 class (Code.cs)
- ✅ Helper methods รวมศูนย์ (4 classes)
- ✅ SQL Injection = 0
- ✅ Input validation ครบ
- ✅ Code reuse สูง

---

## 📞 ติดต่อ & สนับสนุน

หากมีคำถามหรือพบปัญหา:
1. อ่าน documentation นี้ให้ละเอียด
2. ดูตัวอย่างการใช้งาน (Examples)
3. ตรวจสอบ validation rules
4. ทดสอบก่อนนำไปใช้ production

**Happy Coding! 🚀**
