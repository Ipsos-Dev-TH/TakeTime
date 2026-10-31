# slip-ocr-bench — วัดความแม่นของ "อ่านยอดจากรูปสลิป" (OCR ในเครื่อง)

ตัวอ่านยอดระดับ 2 ของระบบตรวจสลิป (CLAUDE.md ข้อ 18) ใช้เมื่อโควต้า SlipOK หมด
`reader.py` คือต้นฉบับของอัลกอริทึม และ `Take Time BangPhra/Class/Payments/SlipCheck/LocalSlipAmountReader.cs`
พอร์ตมาแบบบรรทัดต่อบรรทัด ถ้าแก้ไฟล์หนึ่งต้องแก้อีกไฟล์ด้วย จากนั้นรัน benchmark ซ้ำทั้ง 2 ตัว

ใน repo **ไม่มีรูปสลิป** มีเฉพาะโค้ด: ตัวสร้างสลิปจำลอง (`slipgen.py`), ตัวรัน (`bench.py`) และตัวสรุปผล (`score.py`)

## ติดตั้ง

```
pip install Pillow numpy
# Tesseract 5 + ภาษาไทย  (Windows: UB Mannheim installer → เลือก Thai · Linux: apt install tesseract-ocr tesseract-ocr-tha)
```
ถ้า `tesseract` ไม่อยู่ใน PATH ให้ตั้ง env `TESS=C:\Program Files\Tesseract-OCR\tesseract.exe`

## วัดกับสลิปจริงของโรงแรม (แนะนำ)

1. คัดลอกโฟลเดอร์สลิปจากเซิร์ฟเวอร์ (เช่น `Upload/Slip/`) มาไว้ในเครื่องที่ใช้วัด (เป็นข้อมูลส่วนบุคคล — ห้าม commit)
2. ส่งออกยอดที่ถูกต้องเป็น CSV ยอดที่ SlipOK ยืนยันกับธนาคารแล้วเป็นคำตอบที่ดีที่สุด:
   ```sql
   SELECT SlipFileURL AS [file], Slip_Amount AS amount, ISNULL(Slip_Sending_Bank, '-') AS bank
   FROM dbo.Payment_Slips
   WHERE Verify_Source = 'SLIPOK' AND Slip_Amount IS NOT NULL
   -- ถ้าอยากได้มากขึ้น: สลิปที่พนักงานอนุมัติแล้ว (ยอดที่ลง)
   -- SELECT SlipFileURL, Claimed_Amount, '-' FROM dbo.Payment_Slips WHERE VerificationStatus = 'APPROVED' AND Claimed_Amount > 0
   ```
   บันทึกเป็น `truth.csv` โดยแถวแรกเป็นหัวคอลัมน์ `file,amount,bank` ส่วนคอลัมน์ `file` จะใส่เป็น path หรือชื่อไฟล์ก็ได้ (ระบบจับคู่ด้วยชื่อไฟล์)
3. รัน
   ```
   python bench.py --images D:\slips --truth truth.csv --out results.csv --par 4
   ```
4. เปิดดูทีละใบ (ข้อความแต่ละรอบ ผู้สมัคร โหวต และเหตุผลที่ตัดสิน): `python reader.py D:\slips\x.jpg [ยอดที่ลง]`
   ในเว็บใช้หน้าแอดมิน **ตั้งค่าตรวจสลิป → ทดสอบด้วยสลิป** ซึ่งรันตัว C# จริง และแสดงข้อความแต่ละรอบ ROI กับภาพที่ส่งเข้ารอบ 2

## ความหมายของตัวเลข

| คอลัมน์ | ความหมาย |
|---|---|
| exact | คืนยอดถูกต้อง (กรณีไม่มียอดที่ลง) |
| **FALSE** | คืน**ยอดผิด** เป็นกรณีที่แย่ที่สุด เพราะจะหยุดพนักงานผิด ๆ หรือล็อกยอดผิด ต้องใกล้ 0 |
| unread | อ่านไม่ได้ ระบบจะ bypass และติดธงให้ตรวจภายหลัง (ปลอดภัย) |
| >=85 / F@85 | สัดส่วนที่ความมั่นใจถึงเกณฑ์กรอกให้และล็อกช่องยอด (`SlipVerify_Lock_Min_Confidence`) / จำนวนที่ผิดในกลุ่มนี้ **ต้องเป็น 0** |
| E:verif / E:fStop | พนักงานลงยอดถูก: ผ่าน / ถูกหยุดผิด ๆ |
| W:detect / W:fVerif | พนักงานลงยอดผิด (ครึ่งหนึ่ง หรือ +100): ระบบจับได้ / ปล่อยผ่านผิด ๆ |
| ms | เวลาต่อสลิป (`--par` > 1 ทำให้ช้ากว่าการใช้งานจริงที่อ่านทีละใบ) |

## ชุดสลิปจำลอง

```
python slipgen.py --out corpus --seed 7 --aug 4 --real path\to\real.jpg=1000=kbank ...
python slipgen.py --out holdout --seed 99 --hard --aug 4 --amount-offset 7
python slipgen.py --out eights --seed 31 --hard --aug 3 --amounts 8500,850,80,6500,8888,68,800,86.5,8000,61.25
python bench.py --images corpus --truth corpus/truth.csv
```
ตัวสร้างทำ "โครงสร้าง" ของสลิป 9 แบบ (SCB, K PLUS, Krungthai NEXT, Bangkok Bank, Krungsri, ttb, MyMo, PromptPay, TrueMoney)
มีทั้งแบบสว่าง แบบมืด และภาษาอังกฤษ แต่ไม่ได้ลอกภาพจริงของธนาคาร แล้วเพิ่มความเสียหายของภาพแบบต่าง ๆ: ขนาดจอ, JPEG คุณภาพต่ำ,
บีบอัดซ้ำแบบ LINE, เบลอ, เอียง ±3°, ย่อครึ่ง, ถ่ายจากจอ (perspective/แสง/สี/noise), ตัดขอบ, ฝังในหน้าเว็บ และ `--hard`
ที่ซ้อนหลายอย่างรวมกับภาพย่อขนาด `--real` ใส่สลิปจริงพร้อม augmentation เหมือนกัน
`--amounts` ใช้ทดสอบยอดที่ขึ้นต้นด้วย 8/6 (เสี่ยงสับสนกับ ฿)
ฟอนต์: วางไฟล์ `<family>-<weight>.ttf` (sarabun, kanit, prompt, noto-sans-thai, ibm-plex-sans-thai; weight 400/500/700) ไว้ใน `fonts/`
หรือ `--fonts DIR` (ทั้งหมดเป็น OFL เช่นจาก npm `@fontsource/<family>` โดยรวม subset latin กับ thai ด้วย fontTools)
ถ้าไม่มีจะใช้ฟอนต์ไทยของระบบแทน (Garuda / Leelawadee UI / Tahoma)

## ผลล่าสุด (ต.ค. 2026, Tesseract 5.3.4 tessdata ปกติ, เครื่อง 4 core, `--par 4`, OMP_THREAD_LIMIT=1)

| ชุด | n | ตัวเก่า exact / FALSE / unread | ใหม่ Python | ใหม่ C# (mono) | ใหม่ F@85 |
|---|---|---|---|---|---|
| corpus (จำลอง 200 + สลิปจริง 6 × aug 7 = 42) | 242 | 90.5 / 5.4 / 4.1 % | 98.3 / 0 / 1.7 % | 98.3 / 0 / 1.7 % | 0 |
| holdout (seed อื่น + `--hard`) | 200 | 90.0 / 3.0 / 7.0 % | 99.0 / 0 / 1.0 % | 99.5 / 0 / 0.5 % | 0 |
| eights (ยอดขึ้นต้น 8/6) | 160 | 85.0 / 6.2 / 8.8 % | 96.2 / 0 / 3.8 % | 93.8 / 0 / 6.2 % | 0 |

ตัวเก่าคืนยอดผิดที่ความมั่นใจ 90 ไป 12 + 4 + 8 ใบ ส่วนใหญ่เป็น "฿15,000.00" ที่อ่านเป็น 815,000.00 หรือ 5,000.00
สลิปจริง 6 ใบ (ไม่มี aug): ตัวเก่าอ่านถูก 5 ใบ (ใบที่มีแบนเนอร์ "คงเหลือ 132,754.47" อ่านไม่ได้) ส่วนตัวใหม่ถูกทั้ง 6 ใบ โดย 5 ใบมีความมั่นใจ 92
เวลา (อ่านทีละใบ, C# บน mono): เฉลี่ย 2.6 วินาที p95 3.5 วินาที บน Windows GDI+ จะเร็วกว่านี้ ส่วนเวลาส่วนใหญ่อยู่ที่ tesseract 2–3 ครั้ง
