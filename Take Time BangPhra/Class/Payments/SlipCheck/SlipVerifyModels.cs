using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;

namespace Take_Time_BangPhra.Payments.SlipCheck
{
    /// <summary>
    /// ผลการตรวจสลิป (เก็บใน Payment_Slips.Verify_Status — PHASE19_34)
    /// VERIFIED = อ่านยอดได้และตรงกับยอดที่ลง (หรือไม่มียอดให้เทียบ) · MISMATCH = ยอดไม่ตรง ·
    /// DUPLICATE = รหัสอ้างอิงนี้เคยใช้แล้ว · WRONG_RECEIVER = โอนเข้าบัญชีที่ไม่ใช่ของร้าน ·
    /// UNVERIFIED_BYPASS = ตรวจไม่ได้ (ไม่มีคีย์/เน็ต/อ่านไม่ออก) → ปล่อยผ่านแต่ติดธงให้ตรวจภายหลัง ·
    /// PENDING_BANK = ธนาคารยังไม่ยืนยันรายการ (โอนเพิ่งเสร็จ) → ปล่อยผ่านแบบเดียวกับ bypass ·
    /// ALLOCATED = ใช้ยอดคงเหลือของสลิปที่แบ่งไว้กับการจองอื่น (แถวรองของสลิปเดิม)
    /// </summary>
    public static class SlipVerifyStatus
    {
        public const string Verified = "VERIFIED";
        public const string Mismatch = "MISMATCH";
        public const string Duplicate = "DUPLICATE";
        public const string WrongReceiver = "WRONG_RECEIVER";
        public const string UnverifiedBypass = "UNVERIFIED_BYPASS";
        public const string PendingBank = "PENDING_BANK";
        public const string Allocated = "ALLOCATED";

        /// <summary>สถานะที่ต้องหยุดก่อนออกเอกสาร (ยกเว้น override/แบ่งสลิป)</summary>
        public static bool IsBlocking(string s)
        {
            return s == Mismatch || s == Duplicate || s == WrongReceiver;
        }

        public static string Thai(string s)
        {
            switch (s)
            {
                case Verified: return "ตรวจแล้ว ยอดตรง";
                case Mismatch: return "ยอดไม่ตรงกับสลิป";
                case Duplicate: return "สลิปซ้ำ (เคยใช้แล้ว)";
                case WrongReceiver: return "โอนเข้าบัญชีอื่น";
                case UnverifiedBypass: return "ไม่ได้ตรวจ/ตรวจไม่ได้";
                case PendingBank: return "ธนาคารยังไม่ยืนยัน";
                case Allocated: return "ใช้ยอดคงเหลือของสลิปที่แบ่งไว้";
                default: return string.IsNullOrEmpty(s) ? "-" : s;
            }
        }
    }

    /// <summary>แหล่งที่มาของยอดเงิน</summary>
    public static class SlipVerifySource
    {
        public const string SlipOk = "SLIPOK";       // ธนาคารยืนยันผ่าน SlipOK (เชื่อถือได้)
        public const string LocalOcr = "LOCAL_OCR";  // อ่านตัวเลขจากรูปเอง (Tesseract) — ไม่ใช่ข้อมูลธนาคาร
        public const string QrOnly = "QR_ONLY";      // อ่านได้แค่ QR (รหัสอ้างอิง) ไม่มียอด
        public const string None = "NONE";

        public static string Thai(string s)
        {
            switch (s)
            {
                case SlipOk: return "ธนาคารยืนยัน (SlipOK)";
                case LocalOcr: return "อ่านตัวเลขจากรูป (OCR ในเครื่อง)";
                case QrOnly: return "อ่านได้แค่ QR";
                default: return "ไม่ได้ตรวจ";
            }
        }
    }

    /// <summary>ข้อมูลใน mini-QR ของสลิป (BOT Verify Pay Slip — ไม่มียอดเงิน)</summary>
    public class SlipQrInfo
    {
        public string RawPayload;
        public bool IsSlip;               // QR ตรวจสลิปของธนาคาร (API id 000001)
        public bool IsTrueMoney;          // รูปแบบ TrueMoney Wallet
        public bool IsPaymentQr;          // QR รับเงิน PromptPay (000201…) — ไม่ใช่สลิป
        public bool CrcValid;
        public string ApiId;
        public string SendingBank;        // รหัสธนาคาร 3 หลัก (เช่น 014)
        public string TransRef;
        public string CountryCode;
    }

    /// <summary>คำตอบจากผู้ให้บริการตรวจสลิป (SlipOK) แปลงเป็นรูปกลาง</summary>
    public class SlipVendorResponse
    {
        public const string KindOk = "OK";
        public const string KindDuplicate = "DUPLICATE";
        public const string KindAmountMismatch = "AMOUNT_MISMATCH";
        public const string KindReceiverMismatch = "RECEIVER_MISMATCH";
        public const string KindPendingBank = "PENDING_BANK";
        public const string KindNotFound = "NOT_FOUND";
        public const string KindNoQr = "NO_QR";
        public const string KindQuota = "QUOTA";
        public const string KindConfig = "CONFIG";
        public const string KindError = "ERROR";

        public string Kind = KindError;
        public string Code;
        public string Message;
        public bool HasData;              // มีข้อมูลรายการจากธนาคาร (สำเร็จ หรือ 1012/1013/1014 ที่แนบ data)
        public bool Charged;              // นับโควต้า
        public decimal? Amount;
        public DateTime? TransDate;
        public string TransRef;
        public string SendingBank;
        public string ReceivingBank;
        public string SenderName;
        public string SenderAcctMasked;
        public string ReceiverName;
        public string ReceiverAcctMasked;
        public string ReceiverProxyMasked;
        /// <summary>ชนิด proxy ของผู้รับ (BILLERID / MSISDN / NATID / EWALLETID …) — จ่ายผ่าน QR ร้านค้า (Biller) = BILLERID</summary>
        public string ReceiverProxyType;
        /// <summary>รหัสอ้างอิงของรายการจ่ายบิล/QR ร้านค้า (SlipOK API Guide v1.13 หน้า 8 — เทียบ ref1 แทนเลขบัญชี)</summary>
        public string Ref1;
        public string Ref2;
        public string Ref3;
        /// <summary>รหัสร้านค้าผู้รับ (เช่น KB000001813229 ของ K PLUS SHOP)</summary>
        public string ToMerchantId;
        public long ElapsedMs;
    }

    /// <summary>
    /// ข้อเท็จจริงของรูปสลิปหนึ่งรูป (cache ตาม hash ของไฟล์ — กดตรวจซ้ำ/บันทึกซ้ำไม่เสียโควต้า SlipOK อีก)
    /// ไม่ขึ้นกับยอดที่ลง — การตัดสินยอดตรง/ไม่ตรงอยู่ที่ SlipVerificationService.Evaluate
    /// </summary>
    public class SlipInspection
    {
        public string FileHash;
        public SlipQrInfo Qr;
        public SlipVendorResponse Vendor;     // null = ไม่ได้เรียก SlipOK (ปิด/ไม่มีคีย์/โควต้าหมด)
        public string VendorSkipReason;
        public string OcrText;                // ข้อความจาก OCR ในเครื่อง (null = ไม่ได้อ่าน)
        public string OcrError;
        public bool OcrAttempted;
        public DateTime InspectedAt = DateTime.Now;
        public List<string> Notes = new List<string>();
    }

    /// <summary>ตัวตรวจสลิปภายนอก — สลับผู้ให้บริการได้ (SlipOK ตอนนี้)</summary>
    public interface ISlipVerifier
    {
        string Name { get; }
        bool IsConfigured { get; }
        /// <summary>ส่ง qrPayload ถ้ามี (ส่งข้อมูลน้อยกว่า — PDPA) ไม่งั้นส่งรูป</summary>
        SlipVendorResponse Verify(byte[] image, string fileName, string qrPayload);
    }

    /// <summary>บริบทของการตรวจ (ใช้กันสลิปซ้ำ/บันทึก log)</summary>
    public class SlipVerifyContext
    {
        public string SourceType = "RESERVATION";   // RESERVATION | ACTIVITY | ROOM_SERVICE | TEST
        public long? SourceId;
        public long? ExcludeSlipId;                  // ตรวจซ้ำแถวเดิม — ไม่นับตัวเองเป็นสลิปซ้ำ
        public bool CustomerSelfService;
        public bool ForceRefresh;                    // ข้าม cache (ปุ่ม "ตรวจใหม่")
        public string User;
    }

    public class SlipVerifyResult
    {
        public string Status = SlipVerifyStatus.UnverifiedBypass;
        public string Source = SlipVerifySource.None;
        public decimal? Amount;
        public decimal? ExpectedAmount;
        public decimal? Difference;
        public string TransRef;
        public string SendingBank;
        public DateTime? TransDate;
        public string SenderName;
        public string ReceiverName;
        public string ReceiverAcctMasked;
        public int? MatchedPaidHowId;
        public string Message;
        public string QrPayload;
        public decimal? OcrAmount;
        public int OcrConfidence;
        public string OcrRaw;
        public string FileHash;
        public long? DuplicateOfSlipId;
        /// <summary>ใช้ยอดคงเหลือของสลิปที่แบ่งไว้ (สลิปเดิม = DuplicateOfSlipId)</summary>
        public bool IsAllocationContinuation;
        public decimal? AllocationRemaining;
        public bool CanOverride;
        public bool CanAllocate;
        public List<string> Notes = new List<string>();
        /// <summary>ยอดล็อกตามสลิป (PHASE19_36) — null = ปิดฟีเจอร์/ไม่ได้คำนวณ</summary>
        public SlipAmountLock AmountLock;
        /// <summary>ยอดที่ผู้ใช้กรอก (เมื่อถูกแทนด้วยยอดจากสลิป)</summary>
        public decimal? TypedAmount;
        /// <summary>ชนิดคำตอบของ SlipOK (SlipVendorResponse.Kind*) — null = ไม่ได้เรียก</summary>
        public string VendorKind;
        /// <summary>รูปเป็น QR รับเงิน (PromptPay) ไม่ใช่สลิปโอน</summary>
        public bool NotASlip;

        public bool IsBlocking => SlipVerifyStatus.IsBlocking(Status) && !IsAllocationContinuation;
        public bool AmountLocked => AmountLock != null && AmountLock.Locked;
        public bool BankConfirmed => Source == SlipVerifySource.SlipOk;

        public static string BankName(string code)
        {
            switch ((code ?? "").Trim())
            {
                case "002": return "กรุงเทพ";
                case "004": return "กสิกรไทย";
                case "006": return "กรุงไทย";
                case "011": return "ทีทีบี";
                case "014": return "ไทยพาณิชย์";
                case "022": return "ซีไอเอ็มบี";
                case "024": return "ยูโอบี";
                case "025": return "กรุงศรี";
                case "030": return "ออมสิน";
                case "033": return "ธอส.";
                case "034": return "ธ.ก.ส.";
                case "065": return "ธนชาต";
                case "066": return "อิสลาม";
                case "067": return "ทิสโก้";
                case "069": return "เกียรตินาคินภัทร";
                case "070": return "ไอซีบีซี";
                case "071": return "ไทยเครดิต";
                case "073": return "แลนด์ แอนด์ เฮ้าส์";
                default: return string.IsNullOrEmpty(code) ? "" : code;
            }
        }

        /// <summary>ข้อความสั้นบรรทัดเดียว (log/Telegram/alert)</summary>
        public string ShortText()
        {
            var sb = new StringBuilder();
            sb.Append(SlipVerifyStatus.Thai(Status));
            if (Amount.HasValue) sb.Append(" · สลิป ").Append(Amount.Value.ToString("N2"));
            if (ExpectedAmount.HasValue) sb.Append(" / ลง ").Append(ExpectedAmount.Value.ToString("N2"));
            if (Difference.HasValue && Difference.Value != 0m) sb.Append(" / ต่าง ").Append(Difference.Value.ToString("+#,##0.00;-#,##0.00"));
            sb.Append(" · ").Append(SlipVerifySource.Thai(Source));
            if (!string.IsNullOrEmpty(TransRef)) sb.Append(" · อ้างอิง ").Append(TransRef);
            return sb.ToString();
        }

        /// <summary>การ์ดผลตรวจ (HTML encode ทุกค่าแล้ว)</summary>
        public string ToHtml()
        {
            string color = Status == SlipVerifyStatus.Verified || Status == SlipVerifyStatus.Allocated || IsAllocationContinuation ? "#2e7d32"
                : IsBlocking ? "#c62828" : "#ef6c00";
            string bg = Status == SlipVerifyStatus.Verified || IsAllocationContinuation ? "#e8f5e9" : IsBlocking ? "#ffebee" : "#fff3e0";
            var sb = new StringBuilder();
            sb.Append("<div class='slipchk' style='border:1px solid ").Append(color).Append(";background:").Append(bg)
              .Append(";border-radius:8px;padding:10px 12px;margin:8px 0;font-size:14px;line-height:1.7;max-width:560px;'>");
            sb.Append("<b style='color:").Append(color).Append("'>🔎 ผลตรวจสลิป: ")
              .Append(E(IsAllocationContinuation ? SlipVerifyStatus.Thai(SlipVerifyStatus.Allocated) : SlipVerifyStatus.Thai(Status))).Append("</b>");
            sb.Append(" <span style='color:#666;font-size:12px'>(").Append(E(SlipVerifySource.Thai(Source))).Append(")</span><br/>");
            if (Amount.HasValue)
                sb.Append("ยอดในสลิป: <b>").Append(Amount.Value.ToString("N2")).Append("</b> บาท");
            else
                sb.Append("ยอดในสลิป: <i>อ่านไม่ได้</i>");
            if (ExpectedAmount.HasValue)
                sb.Append(" · ยอดที่ลง: <b>").Append(ExpectedAmount.Value.ToString("N2")).Append("</b>");
            if (Difference.HasValue && Difference.Value != 0m)
                sb.Append(" · ต่าง <b>").Append(Difference.Value.ToString("+#,##0.00;-#,##0.00")).Append("</b>");
            sb.Append("<br/>");
            if (!string.IsNullOrEmpty(SendingBank)) sb.Append("ธนาคารผู้โอน: ").Append(E(BankName(SendingBank))).Append(" · ");
            if (TransDate.HasValue) sb.Append("วันเวลาโอน: ").Append(E(TransDate.Value.ToString("dd/MM/yyyy HH:mm", new CultureInfo("th-TH")))).Append(" · ");
            if (!string.IsNullOrEmpty(TransRef)) sb.Append("อ้างอิง: ").Append(E(TransRef));
            sb.Append("<br/>");
            if (!string.IsNullOrEmpty(SenderName)) sb.Append("ผู้โอน: ").Append(E(SenderName)).Append(" · ");
            if (!string.IsNullOrEmpty(ReceiverName) || !string.IsNullOrEmpty(ReceiverAcctMasked))
                sb.Append("ผู้รับ: ").Append(E(ReceiverName)).Append(' ').Append(E(ReceiverAcctMasked));
            if (AllocationRemaining.HasValue)
                sb.Append("<br/>ยอดคงเหลือของสลิปที่ยังไม่ได้ใช้: <b>").Append(AllocationRemaining.Value.ToString("N2")).Append("</b>");
            if (!string.IsNullOrEmpty(Message))
                sb.Append("<div style='margin-top:4px;color:").Append(color).Append("'>").Append(E(Message)).Append("</div>");
            if (AmountLock != null && AmountLock.Active)
                sb.Append(AmountLock.ToHtml());
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string E(string s) => HttpUtility.HtmlEncode(s ?? "");
    }

    /// <summary>ผลการตัดสินที่หน้า (ไป/หยุด/พักรอพนักงาน)</summary>
    public class SlipGateDecision
    {
        public bool Proceed = true;
        public bool HoldForStaff;          // ช่องทางลูกค้าทำเอง: ไม่ออกเอกสาร เก็บสลิปรอพนักงานตรวจ
        public string BlockMessage;
        public decimal? UseAmount;         // ใช้ยอดธนาคารแทนยอดที่ลง (ต่างไม่เกิน tolerance)
        public bool OverrideUsed;
        public string OverrideReason;
        public bool AllocationUsed;
    }

    /// <summary>
    /// ยอดล็อกตามสลิป (CLAUDE.md ข้อ 18 "ยอดล็อกตามสลิป" — PHASE19_36): อ่านยอดได้น่าเชื่อถือ = ใช้ยอดจากสลิปเท่านั้น ห้ามแก้มือ ·
    /// อ่านไม่ได้/ความมั่นใจต่ำ = แก้ยอดเองได้ (มี HintAmount เป็นค่าที่ระบบเดา — ไม่ใส่ให้อัตโนมัติ)
    /// ตัดสินที่ SlipVerificationService.GetAmountLock ที่เดียว
    /// </summary>
    public class SlipAmountLock
    {
        public bool Active;                // เปิดตรวจสลิป + มี PHASE19_34 (ไม่งั้นทุกหน้าแบบเดิม)
        public bool Locked;
        public decimal? Amount;            // ยอดที่ล็อก (Locked เท่านั้น)
        public string Source = SlipVerifySource.None;
        public int Confidence;             // 100 = SlipOK (ธนาคาร) · LOCAL_OCR = คะแนนผู้อ่าน
        public string Reason;              // ทำไมล็อก/ไม่ล็อก (ภาษาไทย)
        public decimal? HintAmount;        // ไม่ล็อก: ยอดที่ระบบเดา (แสดงเป็นคำใบ้)
        public int HintConfidence;

        public static SlipAmountLock Inactive()
        {
            return new SlipAmountLock { Active = false, Reason = "ไม่ได้เปิดล็อกยอดตามสลิป" };
        }

        public string BadgeText()
        {
            if (!Locked) return null;
            return Source == SlipVerifySource.SlipOk
                ? "ยอดจากสลิป ✓ (SlipOK ธนาคารยืนยัน)"
                : "ยอดจากสลิป (อ่านอัตโนมัติ " + Confidence + "%)";
        }

        /// <summary>ข้อความใต้ช่องยอด: ล็อก = ป้ายยอดจากสลิป · ไม่ล็อก = ให้กรอกยอดตามสลิป (+ คำใบ้)</summary>
        public string NoticeText()
        {
            if (Locked) return BadgeText() + " — " + Amount.GetValueOrDefault().ToString("N2") + " บาท (แก้ไม่ได้)";
            string s = HintConfidence > 0
                ? "อ่านยอดจากสลิปไม่ได้/ความมั่นใจต่ำ (" + HintConfidence + "%) — กรอกยอดตามสลิป"
                : "อ่านยอดจากสลิปไม่ได้ — กรอกยอดตามสลิป";
            if (HintAmount.HasValue)
                s += " (ระบบอ่านได้ประมาณ " + HintAmount.Value.ToString("N2") + " บาท — ตรวจให้ตรงสลิปก่อนบันทึก)";
            return s;
        }

        public string ToHtml()
        {
            if (!Active) return "";
            string color = Locked ? "#1b5e20" : "#e65100";
            string bg = Locked ? "#e8f5e9" : "#fff8e1";
            return "<div class='slip-lock-badge' style='margin-top:6px;padding:6px 10px;border-radius:6px;font-weight:600;color:" + color
                + ";background:" + bg + ";border:1px dashed " + color + ";'>" + (Locked ? "🔒 " : "✏️ ")
                + HttpUtility.HtmlEncode(NoticeText()) + "</div>";
        }
    }

    /// <summary>
    /// ผลตรวจสลิปของช่องทางลูกค้าทำเอง (จองห้อง / หน้าชำระเงิน / จองกิจกรรม / รูมเซอร์วิส) ตามโหมด SlipVerify_Customer_Fail_Mode
    /// </summary>
    public class CustomerSlipOutcome
    {
        public const string ModeContactAdmin = "CONTACT_ADMIN";
        public const string ModeAllowManual = "ALLOW_MANUAL";

        public bool Accept = true;          // รับสลิปนี้ได้ (ไปต่อ — อาจพักรอพนักงานตรวจ)
        public bool Blocked;                // ไม่รับ (สลิปซ้ำ หรือ ตรวจไม่ผ่าน + โหมดติดต่อเจ้าหน้าที่)
        public bool Failed;                 // ตรวจไม่ผ่าน (อ่านไม่ได้/ยอดไม่ตรง/ไม่ใช่สลิป/ไม่พบรายการ/ผู้รับผิด/ตรวจไม่ได้)
        public bool ManualAmount;           // ลูกค้ากรอกยอดเอง (ALLOW_MANUAL + ไม่ล็อก) → ติดธง "ลูกค้ากรอกยอดเอง"
        public string Mode = ModeContactAdmin;
        public string Reason;               // เหตุผลภาษาคน
        public string Message;              // ข้อความเต็มที่แสดงลูกค้า (ไม่มี HTML)
        public decimal? Amount;             // ยอดที่ใช้ (ล็อก = ยอดสลิป)
        public SlipVerifyResult Result;
        public SlipAmountLock Lock;
    }
}
