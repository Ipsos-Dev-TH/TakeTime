# Payment Slip OCR Verification System

## Overview

ระบบตรวจสอบสลิปการชำระเงินอัตโนมัติด้วย OCR (Optical Character Recognition) สำหรับโครงการ Take Time BangPhra Resort

**วัตถุประสงค์:**
- ตรวจสอบยอดเงินในสลิปโอนอัตโนมัติ
- ลดข้อผิดพลาดจากการกรอกยอดเงินผิด
- เพิ่มความรวดเร็วในการตรวจสอบสลิป
- สร้างระบบเตือนให้ Admin ตรวจสอบเพิ่มเติมกรณีที่ OCR อ่านไม่ได้

---

## Features

### 1. Automatic OCR Processing
- ✅ อ่านยอดเงินจากสลิปโอนอัตโนมัติเมื่ออัพโหลด
- ✅ รองรับสลิปจากธนาคารไทยทุกธนาคาร (แอพ/ตู้/สาขา)
- ✅ รองรับภาษาไทย + อังกฤษ
- ✅ คำนวณความมั่นใจ (Confidence) 0-100%

### 2. Intelligent Verification
- ✅ เปรียบเทียบยอดที่อ่านได้กับยอดที่ลูกค้ากรอก
- ✅ แจ้งเตือนถ้ายอดไม่ตรง แต่ยังให้ทำรายการต่อได้
- ✅ Auto-flag สลิปที่ต้องตรวจสอบด้วยตนเอง (Confidence < 70%)

### 3. Admin Verification Page
- ✅ หน้าจอรวมสำหรับตรวจสอบสลิปทั้งหมด
- ✅ Filter ตามสถานะ OCR / สถานะการตรวจสอบ / วันที่
- ✅ แสดงสถิติแบบ Real-time
- ✅ อนุมัติ/ปฏิเสธสลิปได้ทันที

### 4. Database Tracking
- ✅ บันทึกผล OCR ทุกครั้ง (Amount, Confidence, Raw Text)
- ✅ Audit trail สำหรับการอนุมัติ/ปฏิเสธ
- ✅ Log errors สำหรับ debugging

---

## Installation & Setup

### 1. Database Migration

รันไฟล์ SQL migration:
```sql
USE Taketime
GO

-- รัน migration file
:r Database/PHASE2_Migration_03_OCR_Slip_Verification.sql
```

คำสั่งนี้จะเพิ่ม columns ต่อไปนี้ใน `Payment_Slips` table:
- `OCR_Amount` - ยอดเงินที่ OCR อ่านได้
- `OCR_Status` - สถานะ OCR (PENDING/SUCCESS/FAILED/MANUAL_REVIEW)
- `OCR_Confidence` - ความมั่นใจ 0-100%
- `OCR_RawText` - ข้อความที่อ่านได้ทั้งหมด
- `OCR_ProcessedDate` - วันเวลาที่ประมวลผล OCR
- `OCR_ErrorMessage` - ข้อความ error (ถ้ามี)

### 2. Install NuGet Packages

ใน Visual Studio:
```bash
Install-Package Tesseract -Version 5.2.0
```

หรือใช้ NuGet Package Manager:
1. Tools → NuGet Package Manager → Manage NuGet Packages for Solution
2. ค้นหา "Tesseract"
3. เลือก version 5.2.0
4. กด Install

### 3. Download Tesseract Language Data

ดาวน์โหลด trained data สำหรับภาษาไทยและอังกฤษ:

1. ไปที่: https://github.com/tesseract-ocr/tessdata
2. ดาวน์โหลดไฟล์:
   - `tha.traineddata` (ภาษาไทย)
   - `eng.traineddata` (ภาษาอังกฤษ)
3. สร้างโฟลเดอร์ `tessdata` ใน project root
4. วางไฟล์ `.traineddata` ลงในโฟลเดอร์ `tessdata`

โครงสร้างโฟลเดอร์:
```
Take Time BangPhra/
├── tessdata/
│   ├── tha.traineddata
│   └── eng.traineddata
├── Take Time BangPhra/
│   ├── Services/
│   │   └── SlipOCRService.cs
│   ├── Account/
│   │   ├── SlipVerification.aspx
│   │   └── SlipVerification.aspx.cs
│   └── Web.config
└── Database/
    └── PHASE2_Migration_03_OCR_Slip_Verification.sql
```

### 4. Update Web.config

Web.config มี settings ดังนี้:
```xml
<appSettings>
  <!-- OCR Settings -->
  <add key="TesseractDataPath" value="~/tessdata" />
  <add key="OCR_Enabled" value="true" />
  <add key="OCR_MinConfidenceThreshold" value="70" />
</appSettings>
```

**Configuration Options:**
- `TesseractDataPath`: path ไปยังโฟลเดอร์ tessdata
- `OCR_Enabled`: เปิด/ปิด OCR (true/false)
- `OCR_MinConfidenceThreshold`: ค่า confidence ต่ำสุดที่ยอมรับ (0-100)

### 5. Build & Run

1. **Clean Solution**: Build → Clean Solution
2. **Rebuild**: Build → Rebuild Solution
3. **Run Application**: F5 หรือ Ctrl+F5

---

## Usage

### สำหรับลูกค้า (Customer)

1. **อัพโหลดสลิป**:
   - ไปที่หน้า Reserve (จองห้องพัก)
   - กรอกข้อมูลการจอง
   - อัพโหลดรูปสลิปโอนเงิน
   - ระบบจะ process OCR อัตโนมัติ

2. **การแจ้งเตือน**:
   - ถ้า OCR สำเร็จ: ไม่มี message พิเศษ (ทำรายการต่อได้ปกติ)
   - ถ้า OCR อ่านไม่ได้: แสดงข้อความ "ระบบไม่สามารถอ่านยอดเงินจากสลิปได้ กรุณารอ Admin ตรวจสอบ"
   - **สำคัญ**: ลูกค้ายังสามารถทำรายการต่อได้ทันที ไม่ต้องรอการตรวจสอบ

### สำหรับ Admin

#### 1. เข้าหน้า Slip Verification

URL: `/Account/SlipVerification.aspx`

หรือเพิ่ม menu link ใน Site.Master:
```html
<li><a href="/Account/SlipVerification.aspx">ตรวจสอบสลิป</a></li>
```

#### 2. ดู Summary

หน้าแรกจะแสดงสถิติ:
- **รอตรวจสอบ**: จำนวนสลิปที่ยังไม่ได้ตรวจสอบ
- **OCR สำเร็จ**: จำนวนสลิปที่ OCR อ่านได้สำเร็จ
- **OCR ล้มเหลว**: จำนวนสลิปที่ OCR อ่านไม่ได้
- **ต้องตรวจสอบด้วยตนเอง**: จำนวนสลิปที่ Confidence < 70%

#### 3. Filter & Search

Filter ตามเงื่อนไข:
- **สถานะ OCR**: PENDING / SUCCESS / FAILED / MANUAL_REVIEW
- **สถานะการตรวจสอบ**: PENDING / APPROVED / REJECTED
- **ช่วงวันที่**: เลือกวันเริ่มต้น - สิ้นสุด

#### 4. ตรวจสอบสลิป

สำหรับแต่ละสลิป Admin จะเห็น:
- **รูปสลิป**: คลิกที่รูปเพื่อดูแบบเต็มจอ
- **ยอดที่ลูกค้าระบุ**: จำนวนเงินที่ลูกค้ากรอก
- **OCR อ่านได้**: จำนวนเงินที่ OCR ตรวจพบ
- **Confidence Bar**: แถบแสดงความมั่นใจ (เขียว = สูง, เหลือง = ปานกลาง, แดง = ต่ำ)
- **สถานะ OCR**: badge สี
- **Action Buttons**: ✓ อนุมัติ / ✗ ปฏิเสธ

#### 5. อนุมัติ/ปฏิเสธ

**อนุมัติ**:
1. คลิกปุ่ม "✓ อนุมัติ"
2. Confirm
3. ระบบจะบันทึก Admin ID และเวลาที่อนุมัติ

**ปฏิเสธ**:
1. คลิกปุ่ม "✗ ปฏิเสธ"
2. ระบุเหตุผล (ในเวอร์ชันปัจจุบันใช้เหตุผลเริ่มต้น)
3. Confirm
4. ระบบจะบันทึกเหตุผลและแจ้งลูกค้า

---

## OCR Status Reference

| Status | Thai | Description | Action Required |
|--------|------|-------------|-----------------|
| **PENDING** | รอดำเนินการ | OCR ยังไม่ได้ process | รอ background job |
| **SUCCESS** | สำเร็จ | อ่านยอดได้สำเร็จ (Confidence ≥ 70%) | อาจไม่ต้องตรวจสอบ |
| **FAILED** | ล้มเหลว | อ่านไม่ได้เลย | **ต้องตรวจสอบ** |
| **MANUAL_REVIEW** | ต้องตรวจสอบ | อ่านได้แต่ Confidence < 70% | **ต้องตรวจสอบ** |
| **SKIPPED** | ข้าม | สลิปเก่าก่อนมี OCR | ไม่ต้องดำเนินการ |

---

## Verification Status Reference

| Status | Thai | Description |
|--------|------|-------------|
| **PENDING** | รอตรวจสอบ | ยังไม่ได้ตรวจสอบ |
| **APPROVED** | อนุมัติแล้ว | Admin ตรวจสอบและอนุมัติแล้ว |
| **REJECTED** | ปฏิเสธแล้ว | Admin ปฏิเสธ (สลิปปลอม/ยอดไม่ถูกต้อง) |

---

## Technical Details

### OCR Processing Flow

```
1. Customer uploads slip → Save to /Documents/PaymentSlips/
                           ↓
2. Insert record to Payment_Slips (OCR_Status = PENDING)
                           ↓
3. Call SlipOCRService.ProcessSlip()
   - Preprocess image (grayscale, contrast, thresholding)
   - Run Tesseract OCR (Thai + English)
   - Extract text
                           ↓
4. Extract amount using regex patterns
   - Pattern 1: "จำนวนเงิน 1,234.56 บาท" (Priority 90)
   - Pattern 2: "฿1,234.56" or "THB 1,234.56" (Priority 85)
   - Pattern 3: "1,234.56 บาท" (Priority 85)
   - Pattern 4: Standalone "1,234.56" (Priority 80)
   - Pattern 5: "1234.56" (Priority 70)
                           ↓
5. Calculate confidence based on:
   - Pattern priority (70-90)
   - Multiple matches (+10)
                           ↓
6. Determine OCR_Status:
   - Confidence ≥ 70% → SUCCESS
   - Amount found but Confidence < 70% → MANUAL_REVIEW
   - No amount found → MANUAL_REVIEW
   - Exception occurred → FAILED
                           ↓
7. Save OCR result to database
   - Update Payment_Slips.OCR_Amount
   - Update Payment_Slips.OCR_Confidence
   - Update Payment_Slips.OCR_Status
   - Update Payment_Slips.OCR_RawText
   - Update Payment_Slips.OCR_ProcessedDate
```

### Database Schema

**Payment_Slips Table (New Columns)**:
```sql
OCR_Amount DECIMAL(18,2) NULL              -- ยอดเงินที่ OCR อ่านได้
OCR_Status NVARCHAR(20) DEFAULT 'PENDING'  -- PENDING/SUCCESS/FAILED/MANUAL_REVIEW/SKIPPED
OCR_Confidence DECIMAL(5,2) NULL           -- 0-100%
OCR_RawText NVARCHAR(MAX) NULL             -- ข้อความที่อ่านได้ทั้งหมด
OCR_ProcessedDate DATETIME NULL            -- วันเวลาที่ process
OCR_ErrorMessage NVARCHAR(1000) NULL       -- ข้อความ error
```

**Indexes**:
- `IX_Payment_Slips_OCR_Pending` - Query สลิปที่รอ process
- `IX_Payment_Slips_Manual_Review` - Query สลิปที่ต้องตรวจสอบ

### SlipOCRService.cs Methods

| Method | Description |
|--------|-------------|
| `ProcessSlip(string imagePath)` | Process slip และ return OCRResult |
| `PerformOCR(string imagePath)` | เรียก Tesseract engine |
| `PreprocessImage(string imagePath)` | ปรับแต่งรูปให้อ่านง่าย (grayscale, contrast) |
| `ExtractAmountFromText(string text)` | ดึงยอดเงินจากข้อความด้วย regex |
| `SaveOCRResult(long slipId, OCRResult result)` | บันทึกผล OCR ลง database |
| `VerifyAmount(decimal ocr, decimal declared)` | เปรียบเทียบยอดเงิน |
| `GetPendingOCRSlips(int limit)` | ดึงสลิปที่รอ process (สำหรับ batch job) |

---

## Troubleshooting

### Problem: OCR ไม่ทำงาน

**Symptoms**: OCR_Status ค้างที่ PENDING ตลอด

**Solutions**:
1. ตรวจสอบว่า Tesseract package ติดตั้งแล้ว:
   ```
   Tools → NuGet Package Manager → Manage NuGet Packages
   ค้นหา "Tesseract" ต้องมี version 5.2.0
   ```

2. ตรวจสอบว่ามีไฟล์ tessdata:
   ```
   Take Time BangPhra/tessdata/tha.traineddata
   Take Time BangPhra/tessdata/eng.traineddata
   ```

3. ตรวจสอบ Web.config:
   ```xml
   <add key="TesseractDataPath" value="~/tessdata" />
   <add key="OCR_Enabled" value="true" />
   ```

4. ดู error log ใน database:
   ```sql
   SELECT TOP 10 * FROM Log
   WHERE LogTitle LIKE '%OCR%'
   ORDER BY Created_Date DESC
   ```

### Problem: OCR อ่านยอดไม่ถูกต้อง

**Symptoms**: OCR_Amount ต่างจาก actual amount มาก

**Solutions**:
1. **ตรวจสอบคุณภาพรูป**:
   - รูปคมชัดหรือไม่?
   - มีแสงสะท้อนหรือไม่?
   - ขนาดรูปเหมาะสมหรือไม่? (แนะนำ > 800px width)

2. **ตรวจสอบ OCR_RawText**:
   ```sql
   SELECT OCR_RawText, OCR_Amount, OCR_Confidence
   FROM Payment_Slips
   WHERE ID = <slip_id>
   ```
   - ดูว่า Tesseract อ่านข้อความได้ถูกต้องหรือไม่
   - ถ้าอ่านข้อความได้แต่ extract amount ผิด → ปรับ regex pattern

3. **ปรับ Confidence Threshold**:
   ```xml
   <add key="OCR_MinConfidenceThreshold" value="60" />
   ```
   ลดจาก 70 → 60 เพื่อให้ผ่านง่ายขึ้น

4. **เพิ่ม Regex Pattern**:
   แก้ไข `SlipOCRService.cs` method `ExtractAmountFromText()`:
   ```csharp
   var patterns = new List<(Regex Pattern, int Priority)>
   {
       // เพิ่ม pattern ใหม่ที่นี่
       (new Regex(@"your-pattern-here", RegexOptions.IgnoreCase), 95),
       ...
   };
   ```

### Problem: หน้า SlipVerification.aspx ไม่เปิด

**Symptoms**: Error 404 หรือ 500

**Solutions**:
1. ตรวจสอบว่าไฟล์อยู่ที่ถูกต้อง:
   ```
   Take Time BangPhra/Account/SlipVerification.aspx
   Take Time BangPhra/Account/SlipVerification.aspx.cs
   ```

2. Rebuild solution:
   ```
   Build → Clean Solution
   Build → Rebuild Solution
   ```

3. ตรวจสอบ authentication:
   ```csharp
   if (Session["Name"] == null)
   {
       Response.Redirect("~/Login.aspx");
   }
   ```

### Problem: Designer file ไม่ sync

**Symptoms**: "The name 'gvSlips' does not exist in the current context"

**Solutions**:
1. **Clean & Rebuild**:
   ```
   Build → Clean Solution
   Build → Rebuild Solution
   ```

2. **Delete bin/obj**:
   - ปิด Visual Studio
   - ลบโฟลเดอร์ `bin` และ `obj` ใน project
   - เปิด Visual Studio ใหม่
   - Rebuild

3. **Force regenerate designer**:
   - เปิด SlipVerification.aspx
   - แก้อักษรใดก็ได้ แล้ว Save
   - Ctrl+Shift+B (Build)

---

## Performance Optimization

### 1. Batch Processing (Future Enhancement)

สำหรับ slips จำนวนมาก สามารถสร้าง background job:

```csharp
// Scheduled task (ทุก 5 นาที)
var ocrService = new SlipOCRService(tessDataPath, connectionString);
var pendingSlips = ocrService.GetPendingOCRSlips(limit: 10);

foreach (DataRow slip in pendingSlips.Rows)
{
    long slipId = Convert.ToInt64(slip["ID"]);
    string slipPath = slip["SlipFileURL"].ToString();

    var result = ocrService.ProcessSlip(Server.MapPath(slipPath));
    ocrService.SaveOCRResult(slipId, result);
}
```

### 2. Image Optimization

ปรับขนาดรูปก่อน OCR:
```csharp
// ใน PreprocessImage method
if (original.Width > 1920)
{
    int newWidth = 1920;
    int newHeight = (int)(original.Height * (1920.0 / original.Width));
    processed = new Bitmap(original, newWidth, newHeight);
}
```

### 3. Caching

Cache Tesseract engine instance:
```csharp
private static TesseractEngine _cachedEngine;

private string PerformOCR(string imageFilePath)
{
    if (_cachedEngine == null)
    {
        _cachedEngine = new TesseractEngine(_tessDataPath, "tha+eng", EngineMode.Default);
    }
    // Use _cachedEngine...
}
```

---

## Future Enhancements

### Phase 3 Roadmap

1. **QR Code Verification** ✨
   - สแกน QR code จากสลิป PromptPay
   - ตรวจสอบความถูกต้องจาก Bank API

2. **Cloud OCR Integration** ✨
   - Google Cloud Vision API (แม่นยำกว่า 95%)
   - Fallback: ถ้า Tesseract ล้มเหลว → ใช้ Cloud API

3. **Machine Learning** 🤖
   - Train custom model จากสลิปจริงของรีสอร์ท
   - ปรับปรุง accuracy เมื่อมีข้อมูลเพิ่มขึ้น

4. **Notification System** 📧
   - แจ้งเตือน Admin ผ่าน LINE/Email เมื่อมีสลิปต้องตรวจสอบ
   - แจ้งลูกค้าเมื่อสลิปถูกอนุมัติ/ปฏิเสธ

5. **Mobile App Support** 📱
   - อัพโหลดสลิปผ่าน Mobile App
   - รับ notification แบบ real-time

6. **Analytics Dashboard** 📊
   - สถิติ OCR accuracy rate
   - Average processing time
   - Top rejection reasons

---

## Support & Contact

หากมีปัญหาหรือข้อสงสัย:

1. **Check Logs**:
   ```sql
   SELECT * FROM Log
   WHERE LogTitle LIKE '%OCR%'
   ORDER BY Created_Date DESC
   ```

2. **Check Database**:
   ```sql
   SELECT TOP 10
       ID, OCR_Status, OCR_Confidence,
       OCR_ErrorMessage, VerificationStatus
   FROM Payment_Slips
   ORDER BY UploadedDate DESC
   ```

3. **Contact Developer**:
   - GitHub Issues: [Link]
   - Email: [Support Email]

---

## License

Copyright © 2025 Take Time BangPhra Resort
All rights reserved.

---

**Last Updated**: 2025-11-05
**Version**: 1.0.0
**Author**: Claude (AI Assistant)
