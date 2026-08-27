using System.Collections.Specialized;

namespace Take_Time_BangPhra.Payments
{
    /// <summary>
    /// สัญญาของ "เกตเวย์รับชำระเงิน" — แยกออกมาเพื่อให้เปลี่ยน/เพิ่มผู้ให้บริการได้
    /// โดยไม่ต้องแตะหน้าจอหรือ flow การจองเลย
    ///
    /// รายละเอียด HTTP ของแต่ละเจ้าถูกกักไว้ในคลาสที่ implement ตัวนี้เท่านั้น
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>รหัสผู้ให้บริการ เช่น PAYSO</summary>
        string ProviderCode { get; }

        /// <summary>ชื่อที่แสดงให้ผู้ใช้เห็น</summary>
        string DisplayName { get; }

        /// <summary>ตั้งค่าครบและเปิดใช้งานอยู่</summary>
        bool IsReady { get; }

        /// <summary>สร้างรายการชำระเงินที่ฝั่งเกตเวย์ → ได้ลิงก์/QR ให้ลูกค้าจ่าย</summary>
        PaymentChargeResult CreateCharge(PaymentChargeRequest request);

        /// <summary>ถามสถานะรายการจากเกตเวย์ (ใช้เมื่อ webhook หาย หรือกดตรวจสอบเอง)</summary>
        PaymentStatusResult QueryStatus(string providerTxnId, string txnRef);

        /// <summary>ขอคืนเงิน — คืน null ถ้าเกตเวย์นี้ยังไม่ได้ตั้งค่าเส้นทางคืนเงินไว้</summary>
        PaymentStatusResult Refund(string providerTxnId, string txnRef, decimal amount, string reason);

        /// <summary>แปลง + ตรวจลายเซ็นข้อความที่เกตเวย์แจ้งกลับมา</summary>
        PaymentWebhookEvent ParseWebhook(NameValueCollection headers, string body, string remoteIp);

        /// <summary>ทดสอบการเชื่อมต่อจากหน้าตั้งค่า — คืนข้อความสรุปให้ผู้ใช้อ่าน</summary>
        string TestConnection();
    }

    /// <summary>
    /// ความสามารถ "กันวงเงิน" (authorization hold) — ใช้กับเงินประกันความเสียหาย
    ///
    /// แยกจาก IPaymentGateway เพราะไม่ใช่ทุกเจ้าทำได้ (Omise ทำได้เฉพาะบัตร,
    /// PromptPay กันวงเงินไม่ได้, Payso ยังไม่ทราบ) — โค้ดฝั่งหน้าจอตรวจด้วย
    /// <c>gateway is IDepositGateway</c> ก่อนเสนอทางเลือกนี้เสมอ
    /// </summary>
    public interface IDepositGateway
    {
        /// <summary>
        /// กันวงเงินบนบัตร (ยังไม่ตัดเงิน) — ต้องมี token บัตรจากฝั่งเบราว์เซอร์แล้ว
        /// ⚠ วงเงินที่กันไว้หมดอายุเองตามนโยบายเกตเวย์ (Omise = 7 วัน)
        /// </summary>
        HoldResult CreateHold(string cardToken, decimal amount, string reference, string description);

        /// <summary>ตัดเงินจากวงเงินที่กันไว้ — amount น้อยกว่ายอดกันได้ (ส่วนที่เหลือคืนอัตโนมัติ)</summary>
        HoldResult CaptureHold(string providerChargeId, decimal amount);

        /// <summary>คืนวงเงินทั้งหมด (ไม่ตัดอะไรเลย)</summary>
        HoldResult ReleaseHold(string providerChargeId);

        /// <summary>อ่านสถานะล่าสุดของรายการกันวงเงินจากเกตเวย์</summary>
        HoldResult GetHold(string providerChargeId);
    }

    /// <summary>ผลของปฏิบัติการกันวงเงิน — สถานะใช้ค่าจาก <see cref="HoldStatus"/></summary>
    public class HoldResult
    {
        public bool Success;
        public string Status;
        public string ProviderChargeId;
        public decimal Amount;
        public decimal CapturedAmount;
        public string CardBrand, CardLast4;
        public System.DateTime? ExpiresAt;
        public string Message;
        public string RawResponse;
    }

    /// <summary>สถานะของวงเงินประกัน — ตรงกับคอลัมน์ Payment_Security_Holds.Status</summary>
    public static class HoldStatus
    {
        public const string PendingCard = "PENDING_CARD"; // ส่งลิงก์ให้ลูกค้าแล้ว รอกรอกบัตร
        public const string Held = "HELD";                // กันวงเงินสำเร็จ ยังไม่ตัด
        public const string Captured = "CAPTURED";        // ตัดค่าเสียหายแล้ว (เต็มหรือบางส่วน — ส่วนเหลือคืนแล้ว)
        public const string Released = "RELEASED";        // คืนวงเงินทั้งหมดแล้ว
        public const string Expired = "EXPIRED";          // หมดอายุเอง (7 วัน) — วงเงินคืนลูกค้าโดยเกตเวย์
        public const string Failed = "FAILED";

        public static bool IsOpen(string s) { return s == PendingCard || s == Held; }

        public static string Thai(string s)
        {
            switch (s)
            {
                case PendingCard: return "รอลูกค้ากรอกบัตร";
                case Held: return "กันวงเงินอยู่";
                case Captured: return "ตัดค่าเสียหายแล้ว";
                case Released: return "คืนวงเงินแล้ว";
                case Expired: return "หมดอายุ (วงเงินคืนอัตโนมัติ)";
                case Failed: return "ไม่สำเร็จ";
                default: return s ?? "";
            }
        }
    }
}
