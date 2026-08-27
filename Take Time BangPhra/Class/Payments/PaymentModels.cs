using System;

namespace Take_Time_BangPhra.Payments
{
    /// <summary>สถานะรายการชำระเงิน — ตรงกับคอลัมน์ Payment_Transaction.Status</summary>
    public static class PaymentStatus
    {
        public const string Initiated = "INITIATED";   // สร้างรายการแล้ว ยังไม่ส่งไปเกตเวย์
        public const string Pending = "PENDING";       // ส่งแล้ว รอลูกค้าจ่าย
        public const string Paid = "PAID";
        public const string Failed = "FAILED";
        public const string Expired = "EXPIRED";
        public const string Cancelled = "CANCELLED";
        public const string Refunded = "REFUNDED";

        public static bool IsFinal(string s)
        {
            return s == Paid || s == Failed || s == Expired || s == Cancelled || s == Refunded;
        }

        public static string Thai(string s)
        {
            switch (s)
            {
                case Initiated: return "เพิ่งสร้าง";
                case Pending: return "รอชำระเงิน";
                case Paid: return "ชำระแล้ว";
                case Failed: return "ไม่สำเร็จ";
                case Expired: return "หมดอายุ";
                case Cancelled: return "ยกเลิก";
                case Refunded: return "คืนเงินแล้ว";
                default: return s ?? "";
            }
        }
    }

    /// <summary>ชนิดของรายการต้นทางที่กำลังจ่าย</summary>
    public static class PaymentSource
    {
        public const string Reservation = "RESERVATION";
        public const string Activity = "ACTIVITY";
        public const string RoomService = "ROOMSERVICE";
        public const string Amenity = "AMENITY";
        public const string Receipt = "RECEIPT";
        public const string Pos = "POS";
        public const string Damage = "DAMAGE";
        public const string Other = "OTHER";

        public static string Thai(string s)
        {
            switch (s)
            {
                case Reservation: return "การจองที่พัก";
                case Activity: return "จองกิจกรรม";
                case RoomService: return "รูมเซอร์วิส";
                case Amenity: return "เบิกของใช้";
                case Receipt: return "ใบเสร็จ";
                case Pos: return "ขายหน้าร้าน";
                case Damage: return "ค่าเสียหาย";
                default: return "อื่น ๆ";
            }
        }
    }

    /// <summary>คำขอสร้างรายการชำระเงิน</summary>
    public class PaymentChargeRequest
    {
        public string TxnRef { get; set; }            // เว้นว่าง = ให้ระบบสร้างให้
        public string Method { get; set; }            // CARD / QR / INSTALLMENT
        public string SourceType { get; set; }
        public string SourceId { get; set; }
        public decimal Amount { get; set; }           // ยอดตั้งต้น (ยังไม่รวมค่าธรรมเนียม)
        public string Currency { get; set; }
        public string Description { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerEmail { get; set; }
        public string ReturnUrl { get; set; }
        public string CancelUrl { get; set; }
        public string WebhookUrl { get; set; }
        public int? CreatedByAdminId { get; set; }

        public PaymentChargeRequest()
        {
            Currency = "THB";
            Method = PaymentGatewayConfig.MethodCard;
            SourceType = PaymentSource.Other;
        }
    }

    /// <summary>ผลจากการสร้างรายการชำระเงินที่เกตเวย์</summary>
    public class PaymentChargeResult
    {
        public bool Success { get; set; }
        public string Status { get; set; }            // PaymentStatus.*
        public string TxnRef { get; set; }
        public string ProviderTxnId { get; set; }
        public string PaymentUrl { get; set; }        // ให้ลูกค้าไปจ่ายต่อ
        public string QrPayload { get; set; }         // ถ้าเกตเวย์คืน QR มาให้
        public string Message { get; set; }
        public string RawRequest { get; set; }
        public string RawResponse { get; set; }
        public int HttpStatus { get; set; }
    }

    /// <summary>ผลการถามสถานะรายการ</summary>
    public class PaymentStatusResult
    {
        public bool Success { get; set; }
        public string Status { get; set; }
        public string ProviderTxnId { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Fee { get; set; }
        public string CardBrand { get; set; }
        public string CardLast4 { get; set; }
        public string Message { get; set; }
        public string RawResponse { get; set; }
        public int HttpStatus { get; set; }
    }

    /// <summary>เหตุการณ์ที่เกตเวย์แจ้งกลับมา (webhook)</summary>
    public class PaymentWebhookEvent
    {
        public bool SignatureValid { get; set; }
        public string EventId { get; set; }
        public string EventType { get; set; }
        public string TxnRef { get; set; }
        public string ProviderTxnId { get; set; }
        public string Status { get; set; }            // PaymentStatus.* (แปลแล้ว)
        public string RawStatus { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Fee { get; set; }
        public string CardBrand { get; set; }
        public string CardLast4 { get; set; }
        public string Message { get; set; }
    }

    /// <summary>รายการชำระเงินหนึ่งแถวใน Payment_Transaction</summary>
    public class PaymentTransaction
    {
        public int ID { get; set; }
        public string TxnRef { get; set; }
        public string Provider { get; set; }
        public string Method { get; set; }
        public string SourceType { get; set; }
        public string SourceId { get; set; }
        public decimal Amount { get; set; }
        public decimal SurchargeAmount { get; set; }
        public decimal? FeeAmount { get; set; }
        public string Currency { get; set; }
        public string Description { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerEmail { get; set; }
        public string Status { get; set; }
        public string ProviderTxnId { get; set; }
        public string CardBrand { get; set; }
        public string CardLast4 { get; set; }
        public string PaymentUrl { get; set; }
        public string QrPayload { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public string FailReason { get; set; }
        public DateTime? AppliedAt { get; set; }
        public string AppliedNote { get; set; }
        public string ReceiptId { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? CreatedBy { get; set; }

        /// <summary>ยอดที่ลูกค้าจ่ายจริง = ยอดตั้งต้น + ค่าธรรมเนียมที่บวก</summary>
        public decimal TotalPayable => Amount + SurchargeAmount;

        public bool IsExpired =>
            Status == PaymentStatus.Pending && ExpiresAt.HasValue && ExpiresAt.Value < DateTime.Now;
    }
}
