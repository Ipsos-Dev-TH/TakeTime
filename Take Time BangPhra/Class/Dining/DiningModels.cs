using System;
using System.Collections.Generic;

namespace Take_Time_BangPhra.Dining
{
    // ════════════════════════════════════════════════════════════════════════
    // QR สั่งอาหาร (Dining) — โมเดลข้อมูล + ค่าคงที่สถานะ
    // ไฟล์นี้ "บริสุทธิ์" (ไม่อ้าง System.Web / DB) เพื่อให้ mono test คอมไพล์ร่วมกับ DiningPricing/DiningTokens ได้
    // ตรงกับตาราง PHASE19_42 (Database/PHASE19_Migration_42_Dining_QR_Ordering.sql)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>ค่าคงที่สถานะทุกชนิด (string ตรงกับค่าในตาราง)</summary>
    public static class DiningStatus
    {
        /// <summary>Dining_Table.Status</summary>
        public static class Table
        {
            public const string Free = "FREE";
            public const string Occupied = "OCCUPIED";
            public const string BillRequested = "BILL_REQUESTED";
            public const string NeedsStaff = "NEEDS_STAFF";
            public const string Disabled = "DISABLED";
        }

        /// <summary>Dining_Table_Session.Status</summary>
        public static class Session
        {
            public const string Open = "OPEN";
            public const string BillRequested = "BILL_REQUESTED";
            public const string Closed = "CLOSED";
        }

        /// <summary>Dining_Order.Order_Status</summary>
        public static class Order
        {
            public const string New = "NEW";
            public const string Accepted = "ACCEPTED";
            public const string Preparing = "PREPARING";
            public const string Served = "SERVED";
            public const string Cancelled = "CANCELLED";

            public static readonly string[] All = { New, Accepted, Preparing, Served, Cancelled };

            /// <summary>ลำดับปกติ NEW→ACCEPTED→PREPARING→SERVED; ยกเลิกได้จากทุกสถานะที่ยังไม่เสิร์ฟ</summary>
            public static bool CanTransition(string from, string to)
            {
                from = (from ?? "").ToUpperInvariant(); to = (to ?? "").ToUpperInvariant();
                if (from == to) return false;
                if (to == Cancelled) return from == New || from == Accepted || from == Preparing;
                if (from == New) return to == Accepted || to == Preparing || to == Served;
                if (from == Accepted) return to == Preparing || to == Served;
                if (from == Preparing) return to == Served;
                return false;
            }

            public static string Thai(string s)
            {
                switch ((s ?? "").ToUpperInvariant())
                {
                    case New: return "ออเดอร์ใหม่";
                    case Accepted: return "รับออเดอร์แล้ว";
                    case Preparing: return "กำลังทำ";
                    case Served: return "เสิร์ฟแล้ว";
                    case Cancelled: return "ยกเลิก";
                    default: return s ?? "";
                }
            }
        }

        /// <summary>Dining_Order.Payment_Status</summary>
        public static class Pay
        {
            public const string Unpaid = "UNPAID";
            public const string Paid = "PAID";
            public const string Charged = "CHARGED";
            public const string Refunded = "REFUNDED";

            public static string Thai(string s)
            {
                switch ((s ?? "").ToUpperInvariant())
                {
                    case Unpaid: return "ยังไม่ชำระ";
                    case Paid: return "ชำระแล้ว";
                    case Charged: return "ลงบิลห้องพัก";
                    case Refunded: return "คืนเงินแล้ว";
                    default: return s ?? "";
                }
            }
        }

        /// <summary>Dining_Payment.Method</summary>
        public static class Method
        {
            public const string Cash = "CASH";
            public const string Transfer = "TRANSFER";
            public const string RoomCharge = "ROOM_CHARGE";

            public static string Thai(string s)
            {
                switch ((s ?? "").ToUpperInvariant())
                {
                    case Cash: return "เงินสด";
                    case Transfer: return "โอนเงิน";
                    case RoomCharge: return "ลงบิลห้องพัก";
                    default: return s ?? "";
                }
            }
        }

        /// <summary>Dining_Payment.Status</summary>
        public static class Payment
        {
            public const string Confirmed = "CONFIRMED";
            public const string PendingSlip = "PENDING_SLIP";
            public const string Rejected = "REJECTED";
            public const string Void = "VOID";
        }

        /// <summary>Dining_Order_Item.Kitchen_Status</summary>
        public static class Kitchen
        {
            public const string Queued = "QUEUED";
            public const string Cooking = "COOKING";
            public const string Ready = "READY";
            public const string Served = "SERVED";
            public const string Void = "VOID";
        }

        /// <summary>Dining_Zone.Manual_Mode</summary>
        public static class ZoneMode
        {
            public const string Auto = "AUTO";
            public const string Open = "OPEN";
            public const string Closed = "CLOSED";
        }

        /// <summary>Dining_Order.Source_Channel</summary>
        public static class Source
        {
            public const string Qr = "QR";
            public const string Staff = "STAFF";
        }

        /// <summary>Dining_Event_Log.Event</summary>
        public static class Event
        {
            public const string SessionOpen = "SESSION_OPEN";
            public const string SessionClose = "SESSION_CLOSE";
            public const string NewOrder = "NEW_ORDER";
            public const string Status = "STATUS";
            public const string Paid = "PAID";
            public const string SlipPending = "SLIP_PENDING";
            public const string SlipRejected = "SLIP_REJECTED";
            public const string RoomCharge = "ROOM_CHARGE";
            public const string RoomChargeFail = "ROOM_CHARGE_FAIL";
            public const string CallStaff = "CALL_STAFF";
            public const string BillRequest = "BILL_REQUEST";
            public const string QrRotated = "QR_ROTATED";
            public const string AutoClose = "AUTO_CLOSE";
        }
    }

    /// <summary>รหัสผิดพลาดที่หน้าใช้ตัดสินการแสดงผล (ข้อความไทยอยู่ใน Message)</summary>
    public static class DiningError
    {
        public const string FeatureOff = "FEATURE_OFF";
        public const string NotReady = "NOT_READY";              // ยังไม่รัน PHASE19_42
        public const string BadToken = "BAD_TOKEN";
        public const string TableInactive = "TABLE_INACTIVE";
        public const string ZoneClosed = "ZONE_CLOSED";
        public const string SessionClosed = "SESSION_CLOSED";
        public const string Validation = "VALIDATION";
        public const string MinOrder = "MIN_ORDER";
        public const string SoldOut = "SOLD_OUT";
        public const string OutOfStock = "OUT_OF_STOCK";
        public const string NotFound = "NOT_FOUND";
        public const string Transition = "TRANSITION";
        public const string Forbidden = "FORBIDDEN";
        public const string AccountingPosted = "ACCOUNTING_POSTED";
        public const string Outstanding = "OUTSTANDING";
        public const string AmountMismatch = "AMOUNT_MISMATCH";
        public const string SlipRejected = "SLIP_REJECTED";
        public const string SlipDuplicate = "SLIP_DUPLICATE";
        public const string RateLimited = "RATE_LIMITED";
        public const string Locked = "LOCKED";
        public const string PhoneMismatch = "PHONE_MISMATCH";
        public const string NothingToPay = "NOTHING_TO_PAY";
        public const string PendingSlip = "PENDING_SLIP";
        public const string Duplicate = "DUPLICATE";
        public const string Internal = "INTERNAL";
    }

    // ── ผลลัพธ์ ───────────────────────────────────────────────────────────────

    /// <summary>ผลลัพธ์พื้นฐาน: Ok + ข้อความไทย (แสดงได้เลย — ไม่มี HTML) + รหัส</summary>
    public class DiningResult
    {
        public bool Ok;
        public string Message;
        public string ErrorCode;

        public static DiningResult Success(string message = null)
        {
            return new DiningResult { Ok = true, Message = message };
        }

        public static DiningResult Fail(string code, string message)
        {
            return new DiningResult { Ok = false, ErrorCode = code, Message = message };
        }
    }

    public sealed class TableResolveResult : DiningResult
    {
        public DiningTable Table;
        public DiningZone Zone;
        /// <summary>โซนเปิดรับออเดอร์อยู่ (false = Message บอกเหตุผล/เวลาทำการ)</summary>
        public bool ZoneOpen;
    }

    public sealed class SessionOpenResult : DiningResult
    {
        public DiningTableSession Session;
        /// <summary>token สำหรับ cookie ของแขก</summary>
        public string SessionToken;
        /// <summary>true = ต่อ session เดิมของโต๊ะ (มีคนที่โต๊ะสั่งอยู่แล้ว)</summary>
        public bool Resumed;
    }

    public sealed class PlaceOrderResult : DiningResult
    {
        public DiningOrder Order;
        public DiningQuote Quote;
        /// <summary>true = คีย์กันซ้ำตรงกับออเดอร์เดิม (คืนออเดอร์เดิม ไม่สร้างใหม่)</summary>
        public bool Duplicate;
    }

    public sealed class PaymentResult : DiningResult
    {
        public DiningPayment Payment;
        public List<long> CoveredOrderIds = new List<long>();
    }

    /// <summary>ผลการส่งสลิปของลูกค้า</summary>
    public sealed class SlipOutcome : DiningResult
    {
        public DiningPayment Payment;
        /// <summary>ธนาคาร/OCR ยืนยัน + ยอดตรง → ออเดอร์ PAID แล้ว</summary>
        public bool Verified;
        /// <summary>รับไว้รอพนักงานตรวจ (SlipVerify_Customer_Fail_Mode = ALLOW_MANUAL)</summary>
        public bool Pending;
        /// <summary>ไม่รับ (สลิปซ้ำ / ตรวจไม่ผ่าน + CONTACT_ADMIN / ยอดไม่ตรง)</summary>
        public bool Rejected;
        public bool DuplicateSlip;
        /// <summary>เหตุผลสั้น (ไทย)</summary>
        public string Reason;
        /// <summary>ข้อความติดต่อร้าน (โทร/LINE) สำหรับแสดงเมื่อถูกปฏิเสธ — ว่างได้</summary>
        public string ContactText;
        public string SlipPath;
        public decimal? SlipAmount;
        public List<long> CoveredOrderIds = new List<long>();
    }

    public sealed class RoomChargeResult : DiningResult
    {
        public DiningPayment Payment;
        /// <summary>ล็อกจนถึงเวลา (ยืนยันเบอร์ผิดครบจำนวน)</summary>
        public DateTime? LockedUntil;
        /// <summary>เหลือลองได้อีกกี่ครั้งก่อนล็อก (เมื่อเบอร์ไม่ตรง)</summary>
        public int AttemptsLeft;
        public string RoomLabel;
        public List<long> CoveredOrderIds = new List<long>();
    }

    /// <summary>การจองที่ลงบิลห้องได้ — ส่งให้ลูกค้าเฉพาะเลขที่การจอง + ป้ายห้อง (ไม่มีชื่อ/เบอร์)</summary>
    public sealed class RoomChargeCandidate
    {
        public int ReservationId;
        public string RoomLabel;
    }

    // ── โซน / โต๊ะ / session ──────────────────────────────────────────────────

    public class DiningZone
    {
        public int Id;
        public string Code;
        public string NameTh;
        public int SortOrder;
        public bool IsActive = true;
        public TimeSpan? OpenTime;
        public TimeSpan? CloseTime;
        /// <summary>AUTO | OPEN | CLOSED</summary>
        public string ManualMode = DiningStatus.ZoneMode.Auto;
        public string ClosedMessage;
        public bool AllowCash = true;
        public bool AllowTransfer = true;
        public bool AllowRoomCharge;
        public string KitchenStation;
        public DateTime? UpdatedAt;
        public string UpdatedBy;
        /// <summary>จำนวนโต๊ะที่ใช้งาน (เติมเมื่ออ่านรายการ)</summary>
        public int TableCount;
    }

    public class DiningTable
    {
        public int Id;
        public int ZoneId;
        public string ZoneCode;
        public string ZoneName;
        public string Code;
        public string NameTh;
        public int Seats = 4;
        public int SortOrder;
        public string QrToken;
        public DateTime QrIssuedAt;
        public int QrVersion = 1;
        public string Status = DiningStatus.Table.Free;
        public long? CurrentSessionId;
        public bool IsActive = true;
        public string Note;
        public string DisplayName
        {
            get { return string.IsNullOrEmpty(NameTh) ? Code : NameTh; }
        }
    }

    /// <summary>แถวในกระดานสถานะโต๊ะ (ต่อโซน)</summary>
    public sealed class TableBoardRow
    {
        public DiningTable Table;
        public long? SessionId;
        public string SessionStatus;
        public DateTime? SessionOpenedAt;
        public DateTime? LastSeenAt;
        public bool BillRequested;
        public int OpenOrders;        // ยังไม่เสิร์ฟ/ยังไม่ยกเลิก
        public int TotalOrders;
        public decimal Outstanding;   // ยอดค้างชำระ
        public decimal PendingSlipAmount;
        public DateTime? LatestOrderAt;
        /// <summary>อายุออเดอร์ล่าสุด (นาที) — null = ไม่มี</summary>
        public double? LatestOrderAgeMinutes;
    }

    public class DiningTableSession
    {
        public long Id;
        public int TableId;
        public string TableCode;
        public string TableName;
        public int ZoneId;
        public string ZoneCode;
        public string ZoneName;
        public string SessionToken;
        public DateTime OpenedAt;
        public DateTime? ClosedAt;
        public int? ClosedBy;
        public string CloseReason;
        public string GuestName;
        public string GuestPhone;
        public int? ReservationId;
        public int? Pax;
        public string Status = DiningStatus.Session.Open;
        public string DeviceHash;
        public DateTime LastSeenAt;
        public DateTime? BillRequestedAt;
        public DateTime? LastCallAt;
        public DateTime? RoomChargeLockedUntil;
        // วิธีจ่ายที่โซนอนุญาต (เติมตอนอ่าน)
        public bool AllowCash = true;
        public bool AllowTransfer = true;
        public bool AllowRoomCharge;
        public string KitchenStation;

        public bool IsOpen
        {
            get { return Status == DiningStatus.Session.Open || Status == DiningStatus.Session.BillRequested; }
        }

        /// <summary>"โต๊ะ A3 (โซนริมน้ำ)"</summary>
        public string Label
        {
            get
            {
                string t = string.IsNullOrEmpty(TableName) ? TableCode : TableName;
                return "โต๊ะ " + t + (string.IsNullOrEmpty(ZoneName) ? "" : " (" + ZoneName + ")");
            }
        }
    }

    // ── ออเดอร์ ───────────────────────────────────────────────────────────────

    public class DiningOrder
    {
        public long Id;
        public string OrderNo;
        public long SessionId;
        public int TableId;
        public int ZoneId;
        public string TableCode;
        public string TableName;
        public string ZoneName;
        public int RoundNo = 1;
        public string OrderStatus = DiningStatus.Order.New;
        public decimal Subtotal;
        public decimal Discount;
        public decimal TotalAmount;
        public decimal VatAmount;
        public string PaymentStatus = DiningStatus.Pay.Unpaid;
        public long? PaymentId;
        public long? PendingPaymentId;
        public string GuestNote;
        public string GuestName;
        public string GuestPhone;
        public string CancelReason;
        public int? AcceptedBy;
        public DateTime? AcceptedAt;
        public int? ServedBy;
        public DateTime? ServedAt;
        public int? CancelledBy;
        public DateTime? CancelledAt;
        public string SourceChannel = DiningStatus.Source.Qr;
        public string ClientIp;
        public string IdempotencyKey;
        public bool StockPosted;
        public DateTime CreatedAt;
        public DateTime? UpdatedAt;
        public List<DiningOrderItem> Items = new List<DiningOrderItem>();

        public bool IsCancelled { get { return OrderStatus == DiningStatus.Order.Cancelled; } }
        public bool IsSettled { get { return PaymentStatus == DiningStatus.Pay.Paid || PaymentStatus == DiningStatus.Pay.Charged; } }
        /// <summary>ค้างชำระและยังไม่ถูกยกเลิก</summary>
        public bool IsOutstanding { get { return !IsCancelled && PaymentStatus == DiningStatus.Pay.Unpaid; } }
        /// <summary>ค้างชำระและไม่มีสลิปรอตรวจค้างอยู่ (จ่ายใหม่ได้)</summary>
        public bool IsPayable { get { return IsOutstanding && !PendingPaymentId.HasValue; } }
    }

    public class DiningOrderItem
    {
        public long Id;
        public long OrderId;
        public int ProductId;
        public string ProductName;
        public decimal Qty;
        public decimal UnitPrice;
        public string OptionsJson;
        public List<DiningSelectedOption> Options = new List<DiningSelectedOption>();
        public decimal OptionsPrice;
        public decimal LineTotal;
        public string ItemNote;
        public bool TrackStock;
        public string KitchenStatus = DiningStatus.Kitchen.Queued;
        public string Station;

        /// <summary>"เผ็ดมาก, ไม่ใส่ผัก" — สำหรับแสดงผล</summary>
        public string OptionsText
        {
            get
            {
                if (Options == null || Options.Count == 0) return "";
                var parts = new List<string>();
                foreach (var o in Options) parts.Add(o.Name + (o.PriceDelta != 0m ? " (+" + o.PriceDelta.ToString("0.##") + ")" : ""));
                return string.Join(", ", parts.ToArray());
            }
        }
    }

    /// <summary>ตัวเลือกที่เลือกแล้ว (snapshot ใน Options_Json)</summary>
    public class DiningSelectedOption
    {
        public int GroupId;
        public string GroupName;
        public int OptionId;
        public string Name;
        public decimal PriceDelta;
    }

    // ── การชำระ ───────────────────────────────────────────────────────────────

    public class DiningPayment
    {
        public long Id;
        public long SessionId;
        public string Method;
        public string Status = DiningStatus.Payment.Confirmed;
        public decimal Amount;
        public int? PaidHowId;
        public string PaidHowName;
        public DateTime? PaidAt;
        public int? PaidBy;
        public string SlipPath;
        public string SlipVerifyStatus;
        public decimal? SlipBankAmount;
        public string SlipTransRef;
        public string SlipNote;
        public string RoomChargeRef;
        public int? ReservationId;
        public List<long> CoversOrderIds = new List<long>();
        public string AcctPostRef;
        public string IdempotencyKey;
        public DateTime CreatedAt;
        public DateTime? UpdatedAt;

        public bool IsPending { get { return Status == DiningStatus.Payment.PendingSlip; } }
    }

    /// <summary>ยอดค้างของ session</summary>
    public sealed class OutstandingInfo
    {
        /// <summary>ยอดค้างทั้งหมด (ออเดอร์ UNPAID ที่ไม่ถูกยกเลิก รวมที่มีสลิปรอตรวจ)</summary>
        public decimal Amount;
        /// <summary>ยอดที่มีสลิปรอพนักงานตรวจ (รวมอยู่ใน Amount)</summary>
        public decimal PendingSlipAmount;
        /// <summary>ยอดที่จ่ายใหม่ได้ตอนนี้ (= Amount − PendingSlipAmount)</summary>
        public decimal PayableAmount;
        public List<long> OrderIds = new List<long>();
        public List<long> PayableOrderIds = new List<long>();
        public List<long> PendingSlipOrderIds = new List<long>();
    }

    /// <summary>สรุปทั้ง session สำหรับหน้าสถานะของลูกค้า</summary>
    public sealed class SessionSummary : DiningResult
    {
        public DiningTableSession Session;
        public List<DiningOrder> Orders = new List<DiningOrder>();
        public List<DiningPayment> Payments = new List<DiningPayment>();
        public OutstandingInfo Outstanding = new OutstandingInfo();
        public decimal TotalOrdered;   // ยอดรวมทุกออเดอร์ที่ไม่ยกเลิก
        public decimal TotalPaid;      // PAID + CHARGED
        public bool BillRequested;
        public bool ZoneOpen;
        public string ZoneClosedMessage;
        /// <summary>tick ล่าสุดของการเปลี่ยนแปลง (ใช้ทำ ETag/poll)</summary>
        public long VersionTicks;
    }

    // ── เมนู ──────────────────────────────────────────────────────────────────

    public class DiningMenuCategory
    {
        public int Id;
        public string NameTh;
        public string NameEn;
        public string Icon;
        public int SortOrder;
        public bool IsActive = true;
        /// <summary>จำนวนรายการที่แสดง (เติมตอนอ่านเมนู)</summary>
        public int ItemCount;
    }

    public class DiningMenuItem
    {
        public int ProductId;
        /// <summary>ชื่อสินค้าใน Product (ต้นฉบับ)</summary>
        public string ProductName;
        /// <summary>ชื่อที่แสดง (Display_Name_TH หรือ ProductName)</summary>
        public string Name;
        public string NameEn;
        public string Description;
        /// <summary>ราคาขาย (รวม VAT แล้ว) จาก Product.Sell_Price</summary>
        public decimal Price;
        public decimal CostPrice;
        public string Barcode;
        public int? CategoryId;
        public string CategoryName;
        public string ImageUrl;
        public string ThumbUrl;
        public bool IsDining = true;
        public bool IsSoldOut;
        public DateTime? SoldOutUntil;
        public int? PrepMinutes;
        public string AllergenTags;
        public int SortOrder;
        /// <summary>CSV รหัสโซน — ว่าง = ทุกโซน</summary>
        public string AvailableZones;
        public bool TrackStock;
        /// <summary>สต๊อกคงเหลือ (ΣProduct_In − ΣProduct_Out) — มีค่าเฉพาะ TrackStock</summary>
        public decimal? StockQty;
        public bool IsRecommended;
        /// <summary>Product.Status = ใช้งาน</summary>
        public bool ProductActive = true;
        public List<DiningOptionGroup> OptionGroups = new List<DiningOptionGroup>();

        /// <summary>sold-out ตอนนี้ (ติ๊ก หรือยังไม่ถึง Sold_Out_Until)</summary>
        public bool SoldOutNow(DateTime now)
        {
            if (!IsSoldOut) return false;
            if (SoldOutUntil.HasValue && SoldOutUntil.Value <= now) return false;
            return true;
        }

        /// <summary>ขายในโซนนี้ไหม (AvailableZones ว่าง = ทุกโซน)</summary>
        public bool InZone(string zoneCode)
        {
            if (string.IsNullOrWhiteSpace(AvailableZones)) return true;
            if (string.IsNullOrWhiteSpace(zoneCode)) return true;
            foreach (string z in AvailableZones.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (string.Equals(z.Trim(), zoneCode.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>สั่งได้ตอนนี้ (ใช้งาน + ไม่ sold-out + อยู่ในโซน + ถ้าตัดสต๊อกต้องมีของ)</summary>
        public bool Orderable(string zoneCode, DateTime now)
        {
            if (!IsDining || !ProductActive) return false;
            if (SoldOutNow(now)) return false;
            if (!InZone(zoneCode)) return false;
            if (TrackStock && StockQty.HasValue && StockQty.Value <= 0m) return false;
            return true;
        }
    }

    public class DiningOptionGroup
    {
        public int Id;
        public string NameTh;
        public int MinSelect;
        public int MaxSelect = 1;
        public bool IsRequired;
        public int SortOrder;
        public bool IsActive = true;
        public List<DiningOption> Options = new List<DiningOption>();
    }

    public class DiningOption
    {
        public int Id;
        public int GroupId;
        public string NameTh;
        public decimal PriceDelta;
        public int SortOrder;
        public bool IsActive = true;
    }

    /// <summary>เมนูของโซน (หมวด + รายการ) — สิ่งที่หน้าแขกแสดง</summary>
    public sealed class DiningMenu
    {
        public string ZoneCode;
        public List<DiningMenuCategory> Categories = new List<DiningMenuCategory>();
        public List<DiningMenuItem> Items = new List<DiningMenuItem>();
        public DateTime LoadedAt;
    }

    // ── ตะกร้า / ใบเสนอราคา ────────────────────────────────────────────────────

    /// <summary>บรรทัดตะกร้าจาก client — ราคาไม่รับจาก client, server คิดเองเสมอ</summary>
    public class DiningCartLine
    {
        public int ProductId;
        public decimal Qty;
        public int[] OptionIds;
        public string Note;
    }

    public class DiningQuoteLine
    {
        public int ProductId;
        public string Name;
        public decimal Qty;
        public decimal UnitPrice;
        public decimal OptionsPrice;
        public List<DiningSelectedOption> Options = new List<DiningSelectedOption>();
        public decimal LineTotal;
        public string Note;
        public bool TrackStock;
        public string Station;
    }

    public class DiningQuote
    {
        public List<DiningQuoteLine> Lines = new List<DiningQuoteLine>();
        public decimal Subtotal;
        public decimal Discount;
        public decimal Total;
        /// <summary>ราคารวม VAT แล้ว (เสมอ — คำตัดสินเจ้าของ)</summary>
        public bool VatIncluded = true;
        /// <summary>VAT ที่ถอดจากยอดรวม (0 เมื่อกิจการไม่จด VAT)</summary>
        public decimal VatAmount;
        public decimal NetAmount;
        public List<string> Errors = new List<string>();
        public bool Ok { get { return Errors.Count == 0; } }
        public string ErrorCode;
        public int ItemCount;
    }
}
