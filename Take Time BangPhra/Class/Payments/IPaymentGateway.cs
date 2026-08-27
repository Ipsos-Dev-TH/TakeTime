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
}
