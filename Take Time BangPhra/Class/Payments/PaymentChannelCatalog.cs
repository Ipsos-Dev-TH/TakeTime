using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace Take_Time_BangPhra.Payments
{
    /// <summary>
    /// ช่องทางชำระเงินหนึ่งช่อง — หนึ่งแถวของ Account_Paid_How (+ คอลัมน์แคตตาล็อก PHASE19_20)
    /// หรือช่องทางเสมือนของเกตเวย์ที่ยังไม่มีแถว (PaidHowId = 0)
    ///
    /// ⚠ สัญญากลางระหว่างทีม (หน้าจองลูกค้า / หน้าพนักงาน / การผูกบัญชี NextAcc) —
    ///   เพิ่มฟิลด์ได้ แต่ห้ามเปลี่ยนชื่อ/ความหมายของฟิลด์เดิม
    /// </summary>
    public sealed class PaymentChannel
    {
        public int PaidHowId;            // Account_Paid_How.ID (0 = ช่องทางเสมือนของเกตเวย์ที่ยังไม่มีแถว)
        public string Code;              // รหัสคงที่ เช่น TRANSFER_12, PAYSO_VISA, PAYSO_AMEX, PAYSO_PROMPTPAY, CASH, DIRECTOR
        public string Name;              // ชื่อที่แสดง (ไทย)
        public string Type;              // TRANSFER | QR | CARD | GATEWAY_OTHER | CASH | DIRECTOR_LOAN | OTA | OTHER
        public string Provider;          // "" | "PAYSO" | "OMISE"
        public string GatewayChannelCode;// รหัสวิธีที่ส่งเข้าเกตเวย์ (CARD / QR / INSTALLMENT — ดู PaysoGateway.SupportedMethods)
        public bool CustomerVisible;     // แสดงในหน้าจองของลูกค้าได้
        public bool StaffVisible;        // แสดงในหน้าพนักงานได้
        public string Instructions;      // ข้อความแนะนำเมื่อเลือก (ข้อความล้วน — ผู้แสดงต้อง HtmlEncode เอง)
        public string Conditions;        // เงื่อนไขรายช่องทาง (ค่าธรรมเนียม ชนิดบัตร ระยะเวลาคืนเงิน)
        public string QrImageUrl;        // TRANSFER/QR: รูปที่ให้ลูกค้าสแกน
        public bool RequiresSlip;        // โอน → ต้องแนบสลิป
        public int SortOrder;

        // ── ฟิลด์เสริม (เพิ่มจากสัญญาเดิม ไม่กระทบผู้ใช้เดิม) ──
        public string PaidHowName;       // Account_Paid_How.Paid_How — ชื่อที่ต้องใช้บันทึก Payment_History/ใบเสร็จ
        public bool Active;              // Account_Paid_How.Status = 'True'
        public bool IsGateway { get { return !string.IsNullOrEmpty(Provider); } }
        /// <summary>ช่องทางเกตเวย์: ใช้ได้จริงตอนนี้ไหม (เปิดระบบ + เป็นเจ้าที่ใช้อยู่ + พร้อม + เปิดช่องนี้) — ไม่ใช่เกตเวย์ = true</summary>
        public bool GatewayLive = true;
        /// <summary>เหตุผลที่ช่องทางเกตเวย์ยังใช้ไม่ได้ (ว่าง = ใช้ได้) — ใช้แสดงในหน้าตั้งค่า</summary>
        public string Unavailable;

        internal PaymentChannel Clone() { return (PaymentChannel)MemberwiseClone(); }
    }

    /// <summary>
    /// แคตตาล็อกช่องทางชำระเงิน — แหล่งเดียวที่ตอบว่า "หน้านี้ควรเสนอช่องทางไหน"
    ///
    /// หลักการ:
    ///   • ข้อมูลอยู่ใน Account_Paid_How (แถวเดียวกับที่ผูกบัญชี NextAcc) + คอลัมน์ PHASE19_20
    ///     ยังไม่รัน migration = เดาชนิดจากชื่อ (เงินสด/โอน/ทดรองกรรมการ/Omise/OTA) ลูกค้าเห็นแค่ "โอน"
    ///   • ลูกค้าไม่มีวันเห็น เงินสด / ทดรองกรรมการ / OTA ไม่ว่าจะติ๊กอะไรไว้
    ///   • ช่องทางเกตเวย์โผล่เฉพาะเมื่อ: ระบบชำระออนไลน์เปิด + เป็นเจ้าที่เลือกอยู่ + เกตเวย์พร้อม
    ///     + เกตเวย์เปิดวิธีนั้น (GetAvailableChannels) + วิธีนั้นอยู่ใน Payment_Methods_Enabled
    ///     + จุดรับเงินนั้นเปิด (ChannelEnabled) ⇒ Payso ยังไม่อนุมัติ = ลูกค้าไม่เห็นอะไรเกี่ยวกับเกตเวย์เลย
    ///   • cache 30 วินาที (เฉพาะแถวจากฐานข้อมูล — สถานะเกตเวย์คำนวณสดทุกครั้ง)
    ///     หน้าตั้งค่าเรียก <see cref="Invalidate"/> หลังบันทึก
    /// </summary>
    public static class PaymentChannelCatalog
    {
        public const string TypeTransfer = "TRANSFER";
        public const string TypeQr = "QR";
        public const string TypeCard = "CARD";
        public const string TypeGatewayOther = "GATEWAY_OTHER";
        public const string TypeCash = "CASH";
        public const string TypeDirectorLoan = "DIRECTOR_LOAN";
        public const string TypeOta = "OTA";
        public const string TypeOther = "OTHER";

        public static readonly string[] AllTypes =
        {
            TypeTransfer, TypeQr, TypeCard, TypeGatewayOther, TypeCash, TypeDirectorLoan, TypeOta, TypeOther
        };

        private static readonly object _lock = new object();
        private static List<PaymentChannel> _rows;
        private static DateTime _loadedAt = DateTime.MinValue;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
        private static bool _catalogColumns;

        private static string ConnStr
        {
            get { return ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString; }
        }

        /// <summary>รัน PHASE19_Migration_20 แล้วหรือยัง (มีคอลัมน์แคตตาล็อก)</summary>
        public static bool CatalogColumnsReady
        {
            get { GetRows(); return _catalogColumns; }
        }

        public static void Invalidate()
        {
            lock (_lock) { _rows = null; _loadedAt = DateTime.MinValue; }
        }

        // ── API หลัก ─────────────────────────────────────────────────────────

        /// <summary>
        /// ช่องทางสำหรับหน้าลูกค้า (หน้าจอง/หน้ายืนยัน/หน้าจ่าย) เรียงตาม Sort_Order
        /// sourceType = จุดรับเงิน เช่น "RESERVATION" (รับ "BOOKING" เป็นชื่อเล่นของ RESERVATION)
        /// ไม่คืน CASH / DIRECTOR_LOAN / OTA เด็ดขาด · ช่องทางเกตเวย์ต้องพร้อมจริงเท่านั้น
        /// </summary>
        public static IList<PaymentChannel> ForCustomer(string sourceType)
        {
            var result = new List<PaymentChannel>();
            try
            {
                string src = NormalizeSource(sourceType);
                var ctx = new GatewayContext();
                foreach (PaymentChannel raw in AllInternal(ctx))
                {
                    if (!raw.Active || !raw.CustomerVisible) continue;
                    if (IsNeverCustomer(raw.Type)) continue;
                    if (raw.IsGateway)
                    {
                        if (!raw.GatewayLive) continue;
                        if (!SafeChannelEnabled(src)) continue;
                    }
                    result.Add(raw);
                }
            }
            catch { /* แคตตาล็อกพัง = ไม่เสนออะไรเพิ่ม หน้าจอใช้ทางเดิม */ }
            return result;
        }

        /// <summary>ช่องทางสำหรับหน้าพนักงาน (รวมเงินสด/ทดรองกรรมการ/OTA ที่ติ๊กให้พนักงานเห็น)</summary>
        public static IList<PaymentChannel> ForStaff(string sourceType)
        {
            var result = new List<PaymentChannel>();
            try
            {
                string src = NormalizeSource(sourceType);
                var ctx = new GatewayContext();
                foreach (PaymentChannel raw in AllInternal(ctx))
                {
                    if (!raw.Active || !raw.StaffVisible) continue;
                    if (raw.IsGateway)
                    {
                        if (!raw.GatewayLive) continue;
                        if (!SafeChannelEnabled(src)) continue;
                    }
                    result.Add(raw);
                }
            }
            catch { }
            return result;
        }

        /// <summary>หาช่องทางด้วยรหัส (ไม่สนการมองเห็น) — ไม่พบ = null · ดู GatewayLive/Active ก่อนใช้</summary>
        public static PaymentChannel Get(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            try
            {
                foreach (PaymentChannel c in AllInternal(new GatewayContext()))
                    if (string.Equals(c.Code, code.Trim(), StringComparison.OrdinalIgnoreCase)) return c;
            }
            catch { }
            return null;
        }

        /// <summary>หาช่องทางด้วย Account_Paid_How.ID — ไม่พบ/0 = null</summary>
        public static PaymentChannel GetByPaidHowId(int id)
        {
            if (id <= 0) return null;
            try
            {
                foreach (PaymentChannel c in AllInternal(new GatewayContext()))
                    if (c.PaidHowId == id) return c;
            }
            catch { }
            return null;
        }

        /// <summary>ทุกช่องทาง (รวมที่ปิด/ซ่อน/เกตเวย์ยังไม่พร้อม) พร้อมเหตุผล — ใช้ในหน้าตั้งค่า</summary>
        public static IList<PaymentChannel> AllForAdmin()
        {
            try { return AllInternal(new GatewayContext()); }
            catch { return new List<PaymentChannel>(); }
        }

        /// <summary>
        /// นโยบายยกเลิกการจองหลัก — ครอบทุกช่องทาง (ค่าตั้ง Payment_Cancellation_Policy)
        /// ว่าง = ยังไม่ได้ตั้ง (หน้าจอใช้ข้อความนโยบายของตัวเอง)
        /// </summary>
        public static string MasterCancellationPolicy
        {
            get { try { return PaymentGatewayConfig.Get("Payment_Cancellation_Policy", "") ?? ""; } catch { return ""; } }
        }

        /// <summary>
        /// ช่องทางโอนที่ใช้แสดง "บัญชีรับเงินประกัน" — ค่าตั้ง Security_Hold_Transfer_Channel
        /// (รหัสช่องทาง) ก่อน ไม่ตั้ง = ช่องทางโอนแรกที่ลูกค้าเห็นได้ → ช่องทางโอนแรกที่เปิดอยู่
        /// </summary>
        public static PaymentChannel DepositTransferChannel()
        {
            try
            {
                string want = PaymentGatewayConfig.Get("Security_Hold_Transfer_Channel", "");
                if (!string.IsNullOrWhiteSpace(want))
                {
                    PaymentChannel c = Get(want);
                    if (c != null && c.Active) return c;
                }
                PaymentChannel firstAny = null;
                foreach (PaymentChannel c in AllInternal(new GatewayContext()))
                {
                    if (!c.Active || c.Type != TypeTransfer) continue;
                    if (c.CustomerVisible) return c;
                    if (firstAny == null) firstAny = c;
                }
                return firstAny;
            }
            catch { return null; }
        }

        /// <summary>
        /// ชื่อแหล่งเงิน (Account_Paid_How.Paid_How) ของรายการที่จ่ายผ่านเกตเวย์ — ใช้ตอนลงบันทึก
        /// ⇒ AccountingSync ค้นชื่อนี้เพื่อบังคับ Dr เข้าบัญชีที่ผูกไว้ของช่องทางนั้น
        /// ลำดับ: ช่องทางของเจ้านั้นที่ตรงวิธี+ยี่ห้อบัตร → ตรงวิธี → (Payso เท่านั้น) แถวใดก็ได้ของ Payso → null
        /// (null = ให้ผู้เรียกใช้ค่าตั้ง Payment_PaidHow_Name แบบเดิม)
        /// </summary>
        public static string ResolvePaidHowName(string provider, string method, string cardBrand)
        {
            try
            {
                string p = (provider ?? "").Trim().ToUpperInvariant();
                string m = (method ?? "").Trim().ToUpperInvariant();
                string brand = (cardBrand ?? "").Trim().ToUpperInvariant();
                if (p.Length == 0) return null;

                PaymentChannel byBrand = null, byMethod = null, anyOfProvider = null;
                foreach (PaymentChannel c in GetRows())
                {
                    if (!c.Active || c.PaidHowId <= 0 || string.IsNullOrEmpty(c.PaidHowName)) continue;
                    if (!string.Equals(c.Provider, p, StringComparison.OrdinalIgnoreCase)) continue;
                    if (anyOfProvider == null) anyOfProvider = c;
                    if (!string.Equals(c.GatewayChannelCode ?? "", m, StringComparison.OrdinalIgnoreCase)) continue;
                    if (byMethod == null) byMethod = c;
                    if (brand.Length > 0 && byBrand == null && BrandMatches(c, brand)) byBrand = c;
                }
                // Omise (ระบบเดิม) ใช้ค่าตั้ง Payment_PaidHow_Name เป็นหลัก — เลือกจากแคตตาล็อกเฉพาะแถว
                // ที่ผู้ดูแลระบุวิธีไว้ชัด (ไม่เดาแถว Omise ใด ๆ ให้ ป้องกันบัญชีเปลี่ยนเงียบ ๆ)
                if (p != PaymentGatewayConfig.ProviderPayso) anyOfProvider = null;
                PaymentChannel pick = byBrand ?? byMethod ?? anyOfProvider;
                return pick == null ? null : pick.PaidHowName;
            }
            catch { return null; }
        }

        /// <summary>ยี่ห้อบัตรที่เกตเวย์แจ้งมา ตรงกับช่องทางนี้ไหม (ดูจากรหัส/ชื่อ เช่น AMEX, VISA, MASTER, JCB)</summary>
        private static bool BrandMatches(PaymentChannel c, string brand)
        {
            string hay = ((c.Code ?? "") + " " + (c.Name ?? "")).ToUpperInvariant();
            if (brand.Contains("AMERICAN") || brand.Contains("AMEX")) return hay.Contains("AMEX") || hay.Contains("AMERICAN");
            if (brand.Contains("VISA")) return hay.Contains("VISA");
            if (brand.Contains("MASTER")) return hay.Contains("MASTER");
            if (brand.Contains("JCB")) return hay.Contains("JCB");
            if (brand.Contains("UNION")) return hay.Contains("UNION");
            return false;
        }

        // ── บันทึกจากหน้าตั้งค่า ─────────────────────────────────────────────

        /// <summary>
        /// บันทึกค่าตั้งแคตตาล็อกของแถวเดียว (ไม่แตะ Paid_How/Status/Nexaacc_* — การผูกบัญชีอยู่หน้า NextAcc)
        /// คืน null = สำเร็จ, ข้อความ = เหตุที่ไม่บันทึก
        /// </summary>
        public static string SaveRow(PaymentChannel ch)
        {
            if (ch == null || ch.PaidHowId <= 0) return "ไม่พบแถวแหล่งเงิน";
            if (!CatalogColumnsReady) return "ยังไม่ได้รัน Database/PHASE19_Migration_20_Payment_Channel_Catalog.sql";

            string code = (ch.Code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) return "รหัสช่องทางว่างไม่ได้";
            if (code.Length > 50) return "รหัสช่องทางยาวเกิน 50 ตัวอักษร";
            foreach (char x in code)
                if (!(char.IsLetterOrDigit(x) || x == '_' || x == '-'))
                    return "รหัสช่องทาง \"" + code + "\" ใช้ได้เฉพาะ A-Z 0-9 _ -";

            string type = (ch.Type ?? "").Trim().ToUpperInvariant();
            if (Array.IndexOf(AllTypes, type) < 0) return "ชนิดช่องทางไม่ถูกต้อง: " + type;

            string provider = (ch.Provider ?? "").Trim().ToUpperInvariant();
            if (provider != "" && provider != PaymentGatewayConfig.ProviderPayso && provider != PaymentGatewayConfig.ProviderOmise)
                return "ผู้ให้บริการต้องเป็น ว่าง / PAYSO / OMISE";

            // กฎตายตัว: เงินสด/ทดรองกรรมการ/OTA ไม่ให้ลูกค้าเห็นเด็ดขาด
            bool custVisible = ch.CustomerVisible && !IsNeverCustomer(type);

            using (var con = new SqlConnection(ConnStr))
            {
                con.Open();
                using (var dup = new SqlCommand(
                    "SELECT COUNT(*) FROM Account_Paid_How WHERE Channel_Code = @c AND ID <> @id", con))
                {
                    dup.Parameters.AddWithValue("@c", code);
                    dup.Parameters.AddWithValue("@id", ch.PaidHowId);
                    if (Convert.ToInt32(dup.ExecuteScalar()) > 0) return "รหัสช่องทาง \"" + code + "\" ซ้ำกับแถวอื่น";
                }

                using (var cmd = new SqlCommand(@"
                    UPDATE Account_Paid_How
                       SET Channel_Code = @code, Channel_Type = @type,
                           Gateway_Provider = @prov, Gateway_Channel_Code = @gcc,
                           Customer_Visible = @cv, Staff_Visible = @sv,
                           Instructions = @ins, Conditions = @cond, Qr_Image_Url = @qr,
                           Requires_Slip = @slip, Sort_Order = @sort
                     WHERE ID = @id", con))
                {
                    cmd.Parameters.AddWithValue("@code", code);
                    cmd.Parameters.AddWithValue("@type", type);
                    cmd.Parameters.AddWithValue("@prov", provider.Length == 0 ? (object)DBNull.Value : provider);
                    string gcc = (ch.GatewayChannelCode ?? "").Trim().ToUpperInvariant();
                    cmd.Parameters.AddWithValue("@gcc", gcc.Length == 0 ? (object)DBNull.Value : gcc);
                    cmd.Parameters.AddWithValue("@cv", custVisible);
                    cmd.Parameters.AddWithValue("@sv", ch.StaffVisible);
                    cmd.Parameters.AddWithValue("@ins", NullIfBlank(ch.Instructions));
                    cmd.Parameters.AddWithValue("@cond", NullIfBlank(ch.Conditions));
                    cmd.Parameters.AddWithValue("@qr", NullIfBlank(ch.QrImageUrl));
                    cmd.Parameters.AddWithValue("@slip", ch.RequiresSlip);
                    cmd.Parameters.AddWithValue("@sort", ch.SortOrder);
                    cmd.Parameters.AddWithValue("@id", ch.PaidHowId);
                    cmd.ExecuteNonQuery();
                }
            }
            Invalidate();
            return null;
        }

        private static object NullIfBlank(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? (object)DBNull.Value : s.Trim();
        }

        // ── กฎ ───────────────────────────────────────────────────────────────

        /// <summary>ชนิดที่ห้ามแสดงให้ลูกค้าเห็นเด็ดขาด</summary>
        public static bool IsNeverCustomer(string type)
        {
            return type == TypeCash || type == TypeDirectorLoan || type == TypeOta;
        }

        /// <summary>
        /// เดาชนิดจากชื่อแหล่งเงิน — ใช้กับแถวที่ยังไม่ได้จัดชนิด (ยังไม่รัน migration / เพิ่มแถวใหม่ภายหลัง)
        /// ต้องตรงกับกฎ backfill ใน PHASE19_Migration_20
        /// </summary>
        public static string InferType(string paidHowName, out string provider)
        {
            provider = "";
            string n = (paidHowName ?? "").Trim();
            string u = n.ToUpperInvariant();
            if (n.Contains("ทดรอง") || n.Contains("กรรมการ")) return TypeDirectorLoan;
            if (u.Contains("OMISE")) { provider = PaymentGatewayConfig.ProviderOmise; return TypeCard; }
            if (u.Contains("PAYSO")) { provider = PaymentGatewayConfig.ProviderPayso; return TypeGatewayOther; }
            if (u.Contains("AGODA") || u.Contains("BOOKING.COM") || u.Contains("EXPEDIA") || u.Contains("TRIP.COM")
                || u.Contains("TRAVELOKA") || u.Contains("AIRBNB") || u == "OTA" || u.StartsWith("OTA ")) return TypeOta;
            if (n.Contains("เงินสด") || u.Contains("CASH")) return TypeCash;
            if (n.Contains("โอน") || n.Contains("ธนาคาร") || n.Contains("พร้อมเพย์") || n.Contains("บัญชี")
                || u.Contains("BANK") || u.Contains("TRANSFER") || u.Contains("PROMPTPAY")) return TypeTransfer;
            if (n.Contains("บัตร") || u.Contains("CARD") || u.Contains("EDC")) return TypeCard;
            return TypeOther;
        }

        private static string NormalizeSource(string sourceType)
        {
            string s = (sourceType ?? "").Trim().ToUpperInvariant();
            if (s == "BOOKING") return PaymentSource.Reservation;
            return s;
        }

        private static bool SafeChannelEnabled(string src)
        {
            try { return PaymentGatewayConfig.ChannelEnabled(src); } catch { return false; }
        }

        /// <summary>สถานะเกตเวย์ ณ ตอนเรียก (คำนวณครั้งเดียวต่อการเรียก API หนึ่งครั้ง)</summary>
        private sealed class GatewayContext
        {
            public readonly bool OnlineOn;
            public readonly string Active;
            public readonly bool Ready;
            public readonly List<GatewayChannel> Channels = new List<GatewayChannel>();
            public readonly List<string> Methods = new List<string>();

            public GatewayContext()
            {
                try
                {
                    var svc = new OnlinePaymentService();
                    OnlineOn = svc.IsAvailable;
                    Active = PaymentGatewayConfig.ActiveProvider;
                    Ready = OnlineOn && PaymentGatewayConfig.IsGatewayReady;
                    if (Ready)
                    {
                        IList<GatewayChannel> gc = svc.Gateway().GetAvailableChannels();
                        if (gc != null) Channels.AddRange(gc);
                        Methods.AddRange(PaymentGatewayConfig.AvailableMethods(0m));
                    }
                }
                catch { OnlineOn = false; Ready = false; Active = PaymentGatewayConfig.ProviderPayso; }
            }

            public bool HasChannel(string code)
            {
                foreach (GatewayChannel g in Channels)
                    if (string.Equals(g.Code, code, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }

        /// <summary>คัดลอกแถว + คำนวณสถานะเกตเวย์ + เติมช่องทางเสมือนของเกตเวย์ที่ยังไม่มีแถว</summary>
        private static List<PaymentChannel> AllInternal(GatewayContext ctx)
        {
            var list = new List<PaymentChannel>();
            foreach (PaymentChannel r in GetRows())
            {
                PaymentChannel c = r.Clone();
                Evaluate(c, ctx);
                list.Add(c);
            }

            // ช่องทางที่ Payso เปิดอยู่แต่ยังไม่มีแถวใน Account_Paid_How (เช่น ยังไม่รัน migration)
            // → เสนอเป็นช่องทางเสมือน PaidHowId = 0 (ลงบันทึกด้วยชื่อแหล่งเงินของเกตเวย์แบบเดิม)
            // Omise (สำรอง) ไม่สร้างช่องทางเสมือน — ลูกค้าเห็น Omise ได้เฉพาะแถวที่ผู้ดูแลติ๊กเองเท่านั้น
            if (ctx.Ready && ctx.Active == PaymentGatewayConfig.ProviderPayso)
            {
                foreach (GatewayChannel g in ctx.Channels)
                {
                    bool covered = false;
                    foreach (PaymentChannel c in list)
                        if (c.Active && string.Equals(c.Provider, ctx.Active, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(c.GatewayChannelCode ?? "", g.Code, StringComparison.OrdinalIgnoreCase))
                        { covered = true; break; }
                    if (covered) continue;

                    var v = new PaymentChannel
                    {
                        PaidHowId = 0,
                        Code = ctx.Active + "_" + g.Code,
                        Name = "PaySo " + g.Name,
                        Type = g.Type == "QR" ? TypeQr : (g.Type == "CARD" ? TypeCard : TypeGatewayOther),
                        Provider = ctx.Active,
                        GatewayChannelCode = g.Code,
                        CustomerVisible = true,
                        StaffVisible = true,
                        Active = true,
                        SortOrder = 500,
                        PaidHowName = ""   // ยังไม่มีแถว = ยังผูกบัญชี NextAcc ไม่ได้ (ลงบันทึกใช้ Payment_PaidHow_Name)
                    };
                    Evaluate(v, ctx);
                    list.Add(v);
                }
            }

            list.Sort(delegate (PaymentChannel a, PaymentChannel b)
            {
                int x = a.SortOrder.CompareTo(b.SortOrder);
                return x != 0 ? x : a.PaidHowId.CompareTo(b.PaidHowId);
            });
            return list;
        }

        private static void Evaluate(PaymentChannel c, GatewayContext ctx)
        {
            c.GatewayLive = true;
            c.Unavailable = null;
            if (!c.IsGateway) return;

            string code = (c.GatewayChannelCode ?? "").ToUpperInvariant();
            bool payso = string.Equals(c.Provider, PaymentGatewayConfig.ProviderPayso, StringComparison.OrdinalIgnoreCase);

            if (!ctx.OnlineOn)
                c.Unavailable = payso ? "รอเปิดใช้ PaySo (ระบบรับชำระออนไลน์ยังปิดอยู่)" : "ระบบรับชำระออนไลน์ยังปิดอยู่";
            else if (!string.Equals(c.Provider, ctx.Active, StringComparison.OrdinalIgnoreCase))
                c.Unavailable = "ไม่ใช่เกตเวย์ที่เลือกใช้อยู่ (ตอนนี้ใช้ " + ctx.Active + ")";
            else if (!ctx.Ready)
                c.Unavailable = payso ? "รอเปิดใช้ PaySo (ยังไม่อนุมัติร้านค้า/ยังไม่ใส่กุญแจ)" : "เกตเวย์ Omise ยังไม่พร้อม";
            else if (code.Length == 0)
                c.Unavailable = "ยังไม่ได้ระบุรหัสวิธีที่ส่งเข้าเกตเวย์ (CARD / QR / INSTALLMENT)";
            else if (!ctx.HasChannel(code))
                c.Unavailable = payso
                    ? "PaySo ยังไม่ได้เปิดช่องทาง " + code + " (ค่าตั้ง Payso_Enabled_Channels)"
                    : "Omise ไม่ได้เปิดวิธี " + code;
            else if (!ctx.Methods.Contains(code))
                c.Unavailable = "ยังไม่ได้เปิดวิธี " + code + " ใน \"วิธีชำระที่ลูกค้าเห็น\" (Payment_Methods_Enabled)";

            c.GatewayLive = c.Unavailable == null;
        }

        // ── อ่านฐานข้อมูล (cache) ─────────────────────────────────────────────

        private static List<PaymentChannel> GetRows()
        {
            lock (_lock)
            {
                if (_rows != null && (DateTime.UtcNow - _loadedAt) < CacheTtl) return _rows;

                var list = new List<PaymentChannel>();
                bool cols = false;
                string cs = ConnStr;
                if (!string.IsNullOrEmpty(cs))
                {
                    DataTable dt = null;
                    try
                    {
                        dt = Query(cs, @"
                            SELECT ID, Paid_How, Status, Channel_Code, Channel_Type, Gateway_Provider,
                                   Gateway_Channel_Code, Customer_Visible, Staff_Visible, Instructions,
                                   Conditions, Qr_Image_Url, Requires_Slip, Sort_Order
                              FROM Account_Paid_How");
                        cols = true;
                    }
                    catch
                    {
                        // ยังไม่รัน PHASE19_20 → อ่านแบบเดิมแล้วเดาชนิดจากชื่อ
                        try { dt = Query(cs, "SELECT ID, Paid_How, Status FROM Account_Paid_How"); }
                        catch { dt = null; }
                    }

                    if (dt != null)
                        foreach (DataRow r in dt.Rows)
                            list.Add(MapRow(r, cols));
                }

                _catalogColumns = cols;
                _rows = list;
                _loadedAt = DateTime.UtcNow;
                return list;
            }
        }

        private static DataTable Query(string cs, string sql)
        {
            var dt = new DataTable();
            using (var con = new SqlConnection(cs))
            using (var da = new SqlDataAdapter(sql, con))
                da.Fill(dt);
            return dt;
        }

        private static PaymentChannel MapRow(DataRow r, bool cols)
        {
            var c = new PaymentChannel();
            c.PaidHowId = Convert.ToInt32(r["ID"]);
            c.PaidHowName = Str(r["Paid_How"]);
            c.Name = c.PaidHowName;
            string st = Str(r["Status"]);
            c.Active = st.Equals("True", StringComparison.OrdinalIgnoreCase) || st == "1";

            string inferredProvider;
            string inferred = InferType(c.PaidHowName, out inferredProvider);

            string type = cols ? Str(r["Channel_Type"]).ToUpperInvariant() : "";
            bool classified = type.Length > 0 && Array.IndexOf(AllTypes, type) >= 0;
            c.Type = classified ? type : inferred;
            c.Provider = cols && classified ? Str(r["Gateway_Provider"]).ToUpperInvariant() : inferredProvider;
            c.GatewayChannelCode = cols ? Str(r["Gateway_Channel_Code"]).ToUpperInvariant() : "";
            c.Code = cols ? Str(r["Channel_Code"]).ToUpperInvariant() : "";
            if (c.Code.Length == 0) c.Code = DefaultCode(c.Type, c.PaidHowId);

            if (cols && classified)
            {
                c.CustomerVisible = Bool(r["Customer_Visible"]);
                c.StaffVisible = Bool(r["Staff_Visible"]);
                c.Instructions = Str(r["Instructions"]);
                c.Conditions = Str(r["Conditions"]);
                c.QrImageUrl = Str(r["Qr_Image_Url"]);
                c.RequiresSlip = Bool(r["Requires_Slip"]);
                c.SortOrder = r["Sort_Order"] == DBNull.Value ? 100 : Convert.ToInt32(r["Sort_Order"]);
            }
            else
            {
                // ยังไม่ได้จัดชนิด: ปลอดภัยไว้ก่อน — ลูกค้าเห็นเฉพาะแถว "โอน" ที่ชื่อมีคำว่าโอน
                c.CustomerVisible = c.Type == TypeTransfer && c.PaidHowName.Contains("โอน");
                c.StaffVisible = c.Type != TypeCard || c.Provider.Length == 0;
                c.RequiresSlip = c.Type == TypeTransfer;
                c.SortOrder = 100;
            }

            if (IsNeverCustomer(c.Type)) c.CustomerVisible = false;

            // โอน/QR ที่ไม่ได้ใส่รูปหรือคำแนะนำเอง → ใช้ข้อมูล "สแกน QR แบบเดิม" ของหน้าตั้งค่าเกตเวย์
            if (c.Type == TypeTransfer || (c.Type == TypeQr && c.Provider.Length == 0))
            {
                if (string.IsNullOrWhiteSpace(c.QrImageUrl))
                    c.QrImageUrl = SafeGet("ManualQr_Image_Url");
                if (string.IsNullOrWhiteSpace(c.Instructions))
                {
                    string bank = SafeGet("ManualQr_Bank_Info");
                    string note = SafeGet("ManualQr_Note");
                    c.Instructions = (bank + (bank.Length > 0 && note.Length > 0 ? "\n" : "") + note).Trim();
                }
            }
            return c;
        }

        private static string DefaultCode(string type, int id)
        {
            switch (type)
            {
                case TypeDirectorLoan: return "DIRECTOR_" + id;
                case TypeOta: return "OTA_" + id;
                default: return type + "_" + id;
            }
        }

        private static string SafeGet(string key)
        {
            try { return (PaymentGatewayConfig.Get(key, "") ?? "").Trim(); } catch { return ""; }
        }

        private static string Str(object o)
        {
            return o == null || o == DBNull.Value ? "" : Convert.ToString(o).Trim();
        }

        private static bool Bool(object o)
        {
            if (o == null || o == DBNull.Value) return false;
            string s = Convert.ToString(o);
            return s == "1" || s.Equals("True", StringComparison.OrdinalIgnoreCase);
        }
    }
}
