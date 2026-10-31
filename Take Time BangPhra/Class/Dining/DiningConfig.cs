using System;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>
    /// ค่าตั้งของ QR สั่งอาหาร — อ่านจาก System_Config/AppCfg (seed ใน PHASE19_42; ไม่มีค่า = ค่าเริ่มต้นด้านล่าง)
    /// เปิด/ปิดฟีเจอร์ = Feature_Dining (ปิดเป็นค่าเริ่มต้นจนตั้งโซน/โต๊ะ/เมนูเสร็จ) — หน้าทุกหน้าเรียก
    /// <c>if (!Feature.Guard(this, DiningConfig.FeatureName)) return;</c> และพนักงาน <c>Perm.Guard(this, Perm.OpsDining)</c>
    /// </summary>
    public static class DiningConfig
    {
        /// <summary>ชื่อฟีเจอร์ใน Feature (System_Config key = Feature_Dining)</summary>
        public const string FeatureName = "Dining";

        /// <summary>ช่องทางขายในนโยบายเอกสารขาย/สมุดขาย (SalesDocumentPolicy.ChDining)</summary>
        public const string SalesChannel = "DINING";
        /// <summary>ช่องทางแชทรวม (OmniChannel_Channels.ChannelCode)</summary>
        public const string OmniChannel = "DINING";
        /// <summary>SourceType ของการตรวจสลิป (SlipVerifyContext.SourceType)</summary>
        public const string SlipSourceType = "DINING";
        /// <summary>โฟลเดอร์เก็บสลิป + คำนำหน้าชื่อไฟล์ (UploadHelper.Save)</summary>
        public const string SlipFolder = "~/Uploads/PaymentSlips";
        public const string SlipPrefix = "DN";
        /// <summary>Remark ของ Product_Out ที่ตัดสต๊อกจากร้านอาหาร (คนละค่ากับ 'ขาย' ของ POS และ 'รูมเซอร์วิส')</summary>
        public const string StockOutRemark = "ร้านอาหาร";

        public const int LockoutMinutes = 15;
        public const int CallStaffCooldownSec = 60;
        public const int MaxLines = DiningPricing.MaxLines;
        public const decimal MaxQtyPerLine = DiningPricing.MaxQtyPerLine;

        public static bool Enabled { get { return Feature.On(FeatureName); } }

        /// <summary>ปิดโต๊ะอัตโนมัติเมื่อจ่ายครบ + ไม่มีกิจกรรม (นาที) — Dining_Session_Idle_Minutes (30)</summary>
        public static int IdleMinutes { get { return Clamp(AppCfg.GetInt("Dining_Session_Idle_Minutes", 30), 5, 24 * 60); } }

        /// <summary>ยอดสั่งขั้นต่ำต่อรอบ — Dining_Min_Order (0)</summary>
        public static decimal MinOrder
        {
            get
            {
                decimal v;
                return decimal.TryParse(AppCfg.Get("Dining_Min_Order", "0"), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0m ? v : 0m;
            }
        }

        /// <summary>หน้าลูกค้า poll สถานะทุก n วินาที — Dining_Guest_Poll_Sec (5)</summary>
        public static int GuestPollSec { get { return Clamp(AppCfg.GetInt("Dining_Guest_Poll_Sec", 5), 2, 60); } }

        /// <summary>กระดานพนักงาน poll ทุก n วินาที — Dining_Staff_Poll_Sec (3)</summary>
        public static int StaffPollSec { get { return Clamp(AppCfg.GetInt("Dining_Staff_Poll_Sec", 3), 2, 60); } }

        /// <summary>ยืนยันเบอร์โทรลงบิลห้องผิดได้กี่ครั้งต่อ session ก่อนล็อก 15 นาที — Dining_Room_Charge_Max_Fail (5)</summary>
        public static int RoomChargeMaxFail { get { return Clamp(AppCfg.GetInt("Dining_Room_Charge_Max_Fail", 5), 1, 50); } }

        /// <summary>ลิงก์หน้ากระดานพนักงาน (ใส่ในแจ้งเตือน) — PaymentUrls.SiteBase() + /Admin/Dining/Orders?o={id}</summary>
        public static string OrdersPageUrl(long orderId)
        {
            string b = SiteBaseSafe();
            return string.IsNullOrEmpty(b) ? null : b + "/Admin/Dining/Orders?o=" + orderId;
        }

        /// <summary>ลิงก์หน้ากระดาน (ไม่ระบุออเดอร์)</summary>
        public static string BoardUrl()
        {
            string b = SiteBaseSafe();
            return string.IsNullOrEmpty(b) ? null : b + "/Admin/Dining/Orders";
        }

        internal static string SiteBaseSafe()
        {
            try { return Take_Time_BangPhra.Payments.PaymentUrls.SiteBase(); } catch { return null; }
        }

        private static int Clamp(int v, int min, int max) { return v < min ? min : v > max ? max : v; }
    }
}
