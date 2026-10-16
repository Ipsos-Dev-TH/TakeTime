using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web.UI;
using Take_Time_BangPhra.Payments;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// ศูนย์ตั้งค่า — หน้าเดียวที่รวมทางเข้าของ "ทุกหน้าตั้งค่า" ในระบบ จัดกลุ่มตามงานจริง
    ///
    /// ทำไมต้องมี: เดิมหน้าตั้งค่ากระจายอยู่ 15+ ที่ (Admin/Settings, Admin/Chat, Admin/RoomService,
    /// Admin/Notifications, Account/...) และปนอยู่ในเมนู mega ร่วมกับหน้างานประจำวัน — หาไม่เจอ
    /// และแยกไม่ออกว่าอันไหน "ตั้งค่า" อันไหน "ทำงาน"
    ///
    /// หน้านี้ไม่ย้าย/ไม่แก้หน้าเดิม (ไม่มีความเสี่ยง) — เป็นสารบัญที่ค้นหาได้
    /// และบอกสถานะของแต่ละเรื่องให้เห็นทันที (ป้ายสถานะบนการ์ด + เช็กลิสต์เปิดใช้งาน)
    /// สถานะทั้งหมด "อ่านอย่างเดียว" จากตัวอ่านค่าตั้งที่มีอยู่แล้ว — หน้านี้ไม่เขียนอะไรลงฐานข้อมูล
    /// </summary>
    public partial class SettingsIndex : Page
    {
        private readonly string _conn =
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        private string Role => Session["User"]?.ToString() ?? "";
        private bool IsOwner => Role == "Owner";
        private bool IsAdminRole => Role == "Owner" || Role == "Admin";

        /// <summary>คีย์ที่ PaymentChannels บันทึกไว้ตอนกดบันทึก (ใช้ในเช็กลิสต์ "ตรวจช่องทางแล้ว")</summary>
        internal const string ChannelsReviewedKey = "Admin_Channels_Reviewed_At";

        /// <summary>รายการตั้งค่า 1 ใบ</summary>
        private class Item
        {
            public string Title, Desc, Url, Keywords;
            public bool OwnerOnly;
            /// <summary>เฉพาะ Owner / Admin (หน้าปลายทางเช็คบทบาทเอง — ไม่โชว์การ์ดที่กดแล้วเด้งออก)</summary>
            public bool AdminOnly;
            /// <summary>ชื่อฟีเจอร์ใน Feature flags — ปิดอยู่จะขึ้นป้ายเตือน (null = ไม่ผูก)</summary>
            public string Feature;

            /// <summary>โมดูลสิทธิ์เฉพาะรายการ — ทับของหมวด (null = ใช้ของหมวด)
            /// ใช้เมื่อหน้าปลายทาง guard ด้วยโมดูลอื่นอยู่แล้ว จะได้ไม่โชว์การ์ดที่กดแล้วเด้งออก
            /// ใส่หลายโมดูลคั่นด้วย | = มีสิทธิ์ตัวใดตัวหนึ่งก็พอ</summary>
            public string Module;

            /// <summary>คีย์ป้ายสถานะ (คำนวณจากค่าตั้งจริง) — null = ไม่มีป้าย</summary>
            public string StatusKey;

            public Item(string title, string desc, string url, string keywords,
                bool ownerOnly = false, string feature = null, string module = null)
            {
                Title = title; Desc = desc; Url = url; Keywords = keywords;
                OwnerOnly = ownerOnly; Feature = feature; Module = module;
            }
        }

        private class Group
        {
            public string Title, Note, Icon, Color;
            /// <summary>โมดูลสิทธิ์ประจำหมวด — รายการในหมวดใช้ตัวนี้ (null = SYS_SETTINGS) · คั่นด้วย | ได้</summary>
            public string Module;
            public List<Item> Items = new List<Item>();
            public Group(string title, string note, string icon, string color, string module = null)
            { Title = title; Note = note; Icon = icon; Color = color; Module = module; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            // ศูนย์ตั้งค่ารวมหลายโมดูล — มีสิทธิ์ส่วนไหนก็เข้ามาเห็นเฉพาะส่วนนั้นได้
            // (เดิม guard ด้วย SYS_SETTINGS อย่างเดียว ⇒ คนที่มีแค่สิทธิ์เนื้อหาเว็บจะเข้าไม่ได้เลย)
            if (!Perm.CanAccess(Perm.SysSettings) && !Perm.CanAccess(Perm.WebContent)
                && !Perm.CanAccess(Perm.SvcGuest) && !Perm.CanAccess(Perm.SysChannel)
                && !Perm.CanAccess(Perm.SysAccounting) && !Perm.CanAccess(Perm.SysPayment))
            {
                Response.Redirect("~/Default", false);
                System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }
            if (!IsPostBack)
            {
                SetupState st = null;
                if (IsAdminRole)
                {
                    try { st = SetupState.Load(_conn); } catch { st = null; }
                }
                Render(st);
                RenderChecklist(st);
            }
        }

        private static bool CanAny(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return Perm.CanAccess(Perm.SysSettings);
            foreach (string m in spec.Split('|'))
            {
                string t = m.Trim();
                if (t.Length > 0 && Perm.CanAccess(t)) return true;
            }
            return false;
        }

        private List<Group> BuildCatalog()
        {
            var groups = new List<Group>();

            // ── 1. การจอง & นโยบาย ────────────────────────────────────────────────
            var booking = new Group("การจอง & นโยบาย",
                "ข้อความที่ลูกค้าต้องอ่านและติ๊กยอมรับก่อนจอง และกติกาการพาสัตว์เลี้ยงเข้าพัก",
                "fa-file-contract", "#5d4037", Perm.SysSettings + "|" + Perm.WebContent);
            booking.Items.Add(new Item("นโยบายการจอง (เงื่อนไข / ความเป็นส่วนตัว / คืนเงิน / ยกเลิก)",
                "ข้อความที่ลูกค้าต้องติ๊กยอมรับก่อนจอง และแสดงบนหน้ายืนยันการจอง · นโยบายยกเลิกแสดงคู่กับทุกช่องทางชำระเงิน",
                "~/Admin/Settings/BookingPolicies",
                "นโยบาย เงื่อนไข ข้อกำหนด terms conditions privacy ความเป็นส่วนตัว pdpa คืนเงิน refund ยกเลิก cancellation การจอง ยอมรับ")
            { AdminOnly = true, StatusKey = "policy" });
            booking.Items.Add(new Item("สัตว์เลี้ยงเข้าพัก (ห้องที่รับ / จำนวนสูงสุด / ค่าบริการ / นโยบาย)",
                "เปิด-ปิดช่อง 🐾 บนหน้าจอง, ห้องที่รับสัตว์เลี้ยง, จำนวนสูงสุดต่อห้อง, ค่าบริการต่อตัว และนโยบายสัตว์เลี้ยงที่ลูกค้าต้องยอมรับ",
                "~/Admin/Settings/PetStay",
                "สัตว์เลี้ยง หมา แมว สุนัข pet dog cat pet-friendly นโยบาย ค่าบริการ ห้องพัก จอง")
            { AdminOnly = true, StatusKey = "pet" });
            groups.Add(booking);

            // ── 2. การชำระเงิน ─────────────────────────────────────────────────────
            string payMods = Perm.SysPayment + "|" + Perm.SysSettings;
            var pay = new Group("การชำระเงิน",
                "ลูกค้าจ่ายเงินได้ทางไหนบ้าง, รับชำระออนไลน์ผ่านเกตเวย์ และเงินประกันความเสียหาย",
                "fa-credit-card", "#8d6e63", payMods);
            pay.Items.Add(new Item("ช่องทางชำระเงิน (ลูกค้า/พนักงานเห็นอะไร)",
                "เลือกว่าลูกค้าเห็นช่องทางไหน (โอน/QR, PaySo VISA · AMEX · พร้อมเพย์) พนักงานเห็นอะไร (เงินสด, ทดรองกรรมการ) ข้อความแนะนำ เงื่อนไข และนโยบายยกเลิกหลัก",
                "~/Admin/Settings/PaymentChannels",
                "ช่องทาง ชำระเงิน จ่ายเงิน โอน qr พร้อมเพย์ บัตรเครดิต visa amex payso เงินสด ทดรอง กรรมการ แหล่งเงิน นโยบาย ยกเลิก คืนเงิน")
            { StatusKey = "channels" });
            pay.Items.Add(new Item("รับชำระเงินออนไลน์ (PaySo หลัก / Omise สำรอง)",
                "เปิด/ปิดระบบรับเงินออนไลน์, กุญแจเกตเวย์, จุดที่เปิดรับ (จอง กิจกรรม POS รูมเซอร์วิส) + ดูรายการชำระเงิน/คืนเงิน",
                "~/Admin/Settings/PaymentGateway",
                "จ่ายเงิน ชำระเงิน บัตรเครดิต omise payso เกตเวย์ gateway qr พร้อมเพย์ webhook รับเงิน ออนไลน์ วงเงินประกัน กันวงเงิน hold ช่องทาง")
            { StatusKey = "gateway" });
            pay.Items.Add(new Item("เงินประกันความเสียหาย",
                "วิธีรับเงินประกัน (โอน/เงินสด/กันวงเงินบัตร), วงเงินแนะนำ, **ตั้งวงเงินแยกรายห้องพัก** และดูเงินประกันที่ยังค้าง",
                "~/Admin/Settings/SecurityDeposit",
                "เงินประกัน ค่าประกัน ความเสียหาย วงเงิน กันวงเงิน hold มัดจำ ห้องพัก รายห้อง deposit security เช็คอิน")
            { StatusKey = "deposit" });
            pay.Items.Add(new Item("ทดสอบเกตเวย์ (Sandbox)",
                "ลองจ่ายด้วย QR/บัตรทดสอบก่อนเปิดให้ลูกค้าจริง: กันวงเงิน-ตัด-คืน, คืนเงิน, ดูคำตอบดิบจากเกตเวย์",
                "~/Payment/GatewayTest",
                "sandbox ทดสอบ test เกตเวย์ omise บัตรทดสอบ 4242 กันวงเงิน คืนเงิน"));
            pay.Items.Add(new Item("จุดรับเงินออนไลน์ (หน้าร้าน)",
                "สร้าง QR/ลิงก์เก็บเงินลูกค้าตรงหน้าเคาน์เตอร์ + สร้างลิงก์วางวงเงินประกันความเสียหาย",
                "~/Payment/Charge",
                "จุดรับเงิน เก็บเงิน qr บัตร pos หน้าร้าน วงเงินประกัน มัดจำ ประกันความเสียหาย hold omise",
                false, null, Perm.FinReceipt));
            groups.Add(pay);

            // ── 3. บัญชี & ภาษี ───────────────────────────────────────────────────
            var acc = new Group("บัญชี & ภาษี (NextAcc)",
                "เชื่อมระบบบัญชี NextAcc, ผูกแหล่งเงินกับผังบัญชี, อ่านอีเมลจอง OTA และเลือกว่าจะลงบันทึกรายได้ทางไหนบ้าง",
                "fa-calculator", "#00897b", Perm.SysAccounting);
            acc.Items.Add(new Item("Accounting Integration (NextAcc)",
                "เชื่อมระบบบัญชี, ผังบัญชี, **ผูกแหล่งเงิน (กระเป๋าเงิน)**, โหมด sync, อ่านอีเมลจอง OTA, สวิตช์ลงบันทึกรายได้ (ขายหน้าร้าน / รูมเซอร์วิส / ค่าห้อง OTA)",
                "~/Admin/Settings/AccountingIntegration",
                "บัญชี nextacc accounting ภาษี vat ผังบัญชี sync ใบกำกับ อีเมลจอง ota รายได้ รวบยอด รูมเซอร์วิส แหล่งเงิน กระเป๋าเงิน wallet", true)
            { StatusKey = "nextacc" });
            acc.Items.Add(new Item("ตั้งค่าลงบัญชีรายสินค้า",
                "เลือกรายสินค้า ว่าการขายจะรวมเข้า **ใบสรุปรายได้รายวัน** หรือไม่ (เช่น หมูกระทะที่ให้รายได้ไปรวมกับค่าห้อง)",
                "~/Admin/Settings/ProductAccounting",
                "สินค้า รายสินค้า ใบสรุป รายวัน รวบยอด rollup หมูกระทะ รายได้ ลงบัญชี ขายหน้าร้าน", true));
            groups.Add(acc);

            // ── 4. ระบบ & การเชื่อมต่อ ─────────────────────────────────────────────
            var conn = new Group("ระบบ & การเชื่อมต่อ",
                "สิทธิ์ผู้ใช้ / Token / API / อีเมล / การแจ้งเตือน — ค่าที่ทำให้ระบบคุยกับบริการภายนอกได้",
                "fa-plug", "#546e7a");
            conn.Items.Add(new Item("กลุ่มสิทธิ์ผู้ใช้",
                "สร้างกลุ่มสิทธิ์เอง แล้วกำหนดว่าแต่ละกลุ่มมองเห็น/เข้าใช้งานส่วนไหนได้บ้าง + ผูกพนักงานเข้ากลุ่ม",
                "~/Admin/Settings/PermissionGroups",
                "สิทธิ์ permission กลุ่ม role ผู้ใช้ พนักงาน เข้าถึง มองเห็น เมนู owner admin staff", true));
            conn.Items.Add(new Item("ตั้งค่าระบบ (Token / API / SMTP)",
                "LINE Token, Telegram, อีเมลส่งออก, API Key, path เก็บไฟล์, สวิตช์เปิด/ปิดฟีเจอร์ — แก้ได้โดยไม่ต้องแตะ Web.config",
                "~/Admin/Settings/SystemSettings",
                "token api key line telegram smtp email อีเมล รหัสผ่าน path ไฟล์ web.config ตั้งค่าระบบ ฟีเจอร์ feature", true));
            conn.Items.Add(new Item("บัญชี LINE ของฉัน / ทีม",
                "ผูกบัญชี LINE เพื่อรับแจ้งเตือนส่วนตัว, ตั้งค่า LINE Login, Callback URL, บังคับ add friend",
                "~/Admin/Settings/LineAccount",
                "line login ไลน์ ผูกบัญชี callback add friend แจ้งเตือน uid"));
            conn.Items.Add(new Item("การแจ้งเตือน (Telegram / LINE)",
                "เลือกทีละเรื่องว่าจะให้ส่งอะไรบ้าง — การจอง, ข้อความลูกค้า, ออเดอร์, คิวบัญชี + ตั้งช่วงเวลาเงียบ และปลายทางแยกรายเรื่อง",
                "~/Admin/Notifications/Settings",
                "notification แจ้งเตือน alert เตือน line telegram ปิดแจ้งเตือน เปิดแจ้งเตือน กลุ่ม chat id ช่วงเวลาเงียบ รบกวน"));
            conn.Items.Add(new Item("Connection Settings",
                "การเชื่อมต่อฐานข้อมูลและบริการอื่น ๆ",
                "~/Admin/Settings/ConnectionSettings",
                "connection database ฐานข้อมูล เชื่อมต่อ", true));
            groups.Add(conn);

            // ── 5. ช่องทางลูกค้า & AI ─────────────────────────────────────────────
            var chan = new Group("ช่องทางติดต่อลูกค้า & AI",
                "แชททุกช่องทาง (LINE / Facebook / อีเมล OTA) และผู้ช่วย AI",
                "fa-comments", "#7b1fa2", Perm.SysChannel);
            chan.Items.Add(new Item("ตั้งค่าช่องทางแชท",
                "เปิด/ปิดช่องทาง + ใส่ Token ของ LINE, Facebook, WhatsApp, Telegram และ **อีเมลลูกค้า OTA** (Agoda/Booking)",
                "~/Admin/Chat/ChannelSettings",
                "แชท chat channel line facebook whatsapp telegram อีเมล ota agoda booking webhook", false, "Chat"));
            chan.Items.Add(new Item("AI Settings",
                "ตั้งค่าผู้ช่วย AI (โมเดล, API key, พฤติกรรมการตอบ)",
                "~/Admin/Settings/AISettings",
                "ai ปัญญาประดิษฐ์ deepseek โมเดล api", true, "AI"));
            chan.Items.Add(new Item("AI Knowledge Base",
                "คลังความรู้ที่ AI ใช้ตอบลูกค้า — ข้อมูลที่พัก กฎ ราคา คำถามพบบ่อย",
                "~/Admin/Settings/AIKnowledgeBase",
                "ai knowledge คลังความรู้ คำถาม faq ข้อมูล", true, "AI"));
            groups.Add(chan);

            // ── 6. บริการในที่พัก ─────────────────────────────────────────────────
            var svc = new Group("บริการในที่พัก",
                "สิ่งที่ลูกค้าใช้ระหว่างเข้าพัก — สั่งอาหาร กิจกรรม สิ่งอำนวยความสะดวก",
                "fa-concierge-bell", "#ef6c00", Perm.SvcGuest);
            svc.Items.Add(new Item("รูมเซอร์วิส — เวลาเปิดปิด & ค่าบริการ",
                "เปิด/ปิดรับออเดอร์, เวลาให้บริการ, และค่าบริการ (% / ต่อชิ้น / ต่อครั้ง)",
                "~/Admin/RoomService/OrderSettings",
                "รูมเซอร์วิส room service สั่งอาหาร เวลา เปิดปิด ค่าบริการ service charge เปอร์เซ็นต์", false, "RoomService"));
            svc.Items.Add(new Item("จัดการกิจกรรม",
                "เพิ่ม/แก้กิจกรรมในที่พัก ราคา รอบเวลาให้จอง และรูปภาพ",
                "~/Admin/Settings/ActivityManagement",
                "กิจกรรม activity ปิงปอง จองรอบ เวลา ราคา", false, "Activities", Perm.OpsActivity));
            svc.Items.Add(new Item("Guest Experience",
                "ภาพรวมประสบการณ์ลูกค้าและการตั้งค่า Guest Portal",
                "~/Admin/GuestExperience/Dashboard",
                "guest portal ประสบการณ์ ลูกค้า", false, "GuestPortal"));
            svc.Items.Add(new Item("QR Code ประจำห้อง",
                "สร้าง/พิมพ์ QR ให้ลูกค้าสแกนเข้า Guest Portal ของห้องนั้น",
                "~/Admin/RoomQRGenerator",
                "qr code ห้อง portal สแกน พิมพ์", false, "GuestPortal", Perm.OpsBooking));
            groups.Add(svc);

            // ── 7. ราคา สมาชิก & ช่องทางขาย ───────────────────────────────────────
            var price = new Group("ราคา สมาชิก & ช่องทางขาย",
                "ราคาห้องพักตามช่วงเวลา สิทธิประโยชน์สมาชิก และช่องทางขายออนไลน์",
                "fa-tags", "#c62828");
            price.Items.Add(new Item("ราคาวันหยุด / ช่วงพิเศษ",
                "ตั้งราคาพิเศษรายวันหรือช่วงเทศกาล",
                "~/Admin/HolidayPrice",
                "ราคา วันหยุด เทศกาล high season ปรับราคา", true));
            price.Items.Add(new Item("Dynamic Pricing",
                "ราคาอัตโนมัติตามอัตราการจอง",
                "~/Admin/Pricing/DynamicPricing",
                "ราคา dynamic อัตโนมัติ ปรับราคา", true, "DynamicPricing"));
            price.Items.Add(new Item("Channel Manager",
                "ภาพรวมช่องทาง OTA ที่เชื่อมอยู่",
                "~/Admin/ChannelManager/Dashboard",
                "channel manager ota agoda booking ช่องทาง", true, "ChannelManager"));
            price.Items.Add(new Item("สิทธิประโยชน์ระดับสมาชิก (Tier)",
                "กำหนดส่วนลด/สิทธิพิเศษของแต่ละระดับสมาชิก",
                "~/Account/TierBenefitsManagement",
                "tier สมาชิก ระดับ ส่วนลด สิทธิประโยชน์ loyalty", false, "Loyalty")
            { AdminOnly = true });
            groups.Add(price);

            // ── 8. เนื้อหาเว็บไซต์ ────────────────────────────────────────────────
            var web = new Group("เนื้อหาเว็บไซต์ & รูปภาพ",
                "สิ่งที่ลูกค้าเห็นบนหน้าเว็บสาธารณะ",
                "fa-globe", "#1565c0", Perm.WebContent);
            web.Items.Add(new Item("จัดการหน้าแรก",
                "แก้ข้อความ/รูป/แบนเนอร์บนหน้าแรกของเว็บไซต์",
                "~/Admin/Edit_Home",
                "หน้าแรก home banner แบนเนอร์ เว็บไซต์ รูป ข้อความ", true));
            web.Items.Add(new Item("โปรโมชั่น",
                "สร้าง/แก้โปรโมชั่นที่แสดงบนเว็บและ Guest Portal",
                "~/Admin/ManagePromotions",
                "โปรโมชั่น promotion ส่วนลด แคมเปญ"));
            web.Items.Add(new Item("สิ่งอำนวยความสะดวก",
                "รายการสิ่งอำนวยความสะดวกที่แสดงบนเว็บ/Portal",
                "~/Admin/ManageFacilities",
                "สิ่งอำนวยความสะดวก facilities สระ wifi"));
            web.Items.Add(new Item("สถานที่ใกล้เคียง",
                "แนะนำร้าน/สถานที่รอบที่พัก",
                "~/Admin/ManageNearbyPlaces",
                "สถานที่ ใกล้เคียง nearby แผนที่ ร้านอาหาร"));
            web.Items.Add(new Item("เบิกของใช้ในห้อง",
                "ตั้งของที่แขกเบิกได้เอง (ฟรี/คิดเงิน) + ดูคำขอที่เข้ามา",
                "~/Admin/ManageAmenities",
                "amenities ของใช้ เบิก ผ้าเช็ดตัว แปรงสีฟัน น้ำดื่ม คำขอ"));
            web.Items.Add(new Item("ข้อมูลฉุกเฉิน",
                "เบอร์โทรฉุกเฉินที่แสดงให้ลูกค้า",
                "~/Admin/ManageEmergency",
                "ฉุกเฉิน emergency เบอร์โทร โรงพยาบาล"));
            web.Items.Add(new Item("เกี่ยวกับเรา",
                "ข้อความหน้า About Us",
                "~/Admin/ManageAboutUs",
                "about เกี่ยวกับเรา แนะนำ"));
            web.Items.Add(new Item("รูปสินค้า",
                "อัปโหลด/จัดการรูปสินค้าที่ใช้ในระบบขายและ Guest Portal",
                "~/Admin/ProductImages",
                "รูป สินค้า ภาพ product image อัปโหลด"));
            groups.Add(web);

            // ── 9. ข้อมูลหลัก & ขั้นสูง ───────────────────────────────────────────
            var adv = new Group("ข้อมูลหลัก & ขั้นสูง",
                "ข้อมูลตั้งต้นของระบบ — ใช้เมื่อรู้ว่ากำลังทำอะไรอยู่",
                "fa-database", "#455a64");
            adv.Items.Add(new Item("ข้อมูลหลัก (ห้องพัก / สินค้า / ตารางระบบ)",
                "แก้ตารางข้อมูลตั้งต้นของระบบโดยตรง",
                "~/Admin/Edit_Data",
                "ข้อมูลหลัก master data ตาราง ห้องพัก แก้ไข", true));
            adv.Items.Add(new Item("ฐานข้อมูล",
                "สำรอง/ตรวจสอบฐานข้อมูล",
                "~/Admin/DatabaseManagement",
                "ฐานข้อมูล database backup สำรอง", true));
            adv.Items.Add(new Item("ผู้จำหน่าย (Vendor)",
                "ทะเบียนผู้ขาย/ซัพพลายเออร์สำหรับใบสำคัญจ่าย",
                "~/Admin/Vendor",
                "vendor ผู้ขาย ซัพพลายเออร์ เจ้าหนี้", true));
            adv.Items.Add(new Item("ตรวจสอบการแจ้งเตือน",
                "เครื่องมือทดสอบ/ไล่ดูการแจ้งเตือนของระบบ",
                "~/Admin/NotificationCheck",
                "ตรวจสอบ แจ้งเตือน ทดสอบ debug", true));
            groups.Add(adv);

            return groups;
        }

        private bool ItemVisible(Item it)
        {
            if (it.OwnerOnly && !IsOwner) return false;
            if (it.AdminOnly && !IsAdminRole) return false;
            // รายการที่ระบุโมดูลเอง ต้องมีสิทธิ์โมดูลนั้นด้วย
            if (!string.IsNullOrEmpty(it.Module) && !CanAny(it.Module)) return false;
            return true;
        }

        private void Render(SetupState st)
        {
            var sb = new StringBuilder();
            foreach (var g in BuildCatalog())
            {
                // ไม่มีสิทธิ์โมดูลของหมวดนี้ → ไม่ต้องแสดงทั้งหมวด
                if (!CanAny(g.Module)) continue;

                var visible = new List<Item>();
                foreach (var it in g.Items)
                    if (ItemVisible(it)) visible.Add(it);
                if (visible.Count == 0) continue;

                sb.Append("<div class='sh-group'>");
                sb.Append($"<h3><span class='ico' style='background:{g.Color}'><i class='fas {g.Icon}'></i></span>{Server.HtmlEncode(g.Title)}</h3>");
                sb.Append($"<p class='note'>{Server.HtmlEncode(g.Note)}</p>");
                sb.Append("<div class='sh-cards'>");

                foreach (var it in visible)
                {
                    bool featureOff = !string.IsNullOrEmpty(it.Feature) && Feature.Off(it.Feature);
                    string keys = (it.Title + " " + it.Desc + " " + it.Keywords).ToLowerInvariant();

                    sb.Append($"<a class='sh-card' href='{ResolveUrl(it.Url)}' data-k='{Server.HtmlEncode(keys)}'");
                    sb.Append($" style='border-left-color:{g.Color}'>");
                    sb.Append("<div class='t'>");
                    sb.Append(Server.HtmlEncode(it.Title));
                    if (featureOff) sb.Append("<span class='tag tag-off'>ฟีเจอร์ปิดอยู่</span>");
                    if (it.OwnerOnly) sb.Append("<span class='tag tag-owner'>Owner</span>");
                    sb.Append("</div>");
                    // Desc รองรับ **ตัวหนา** เล็กน้อยเพื่อเน้นคำสำคัญ
                    sb.Append($"<div class='d'>{Bold(Server.HtmlEncode(it.Desc))}</div>");

                    string chips = st == null ? "" : StatusChips(it.StatusKey, st);
                    if (chips.Length > 0) sb.Append("<div class='st'>").Append(chips).Append("</div>");
                    sb.Append("</a>");
                }
                sb.Append("</div></div>");
            }
            litGroups.Text = sb.ToString();
        }

        // ── ป้ายสถานะบนการ์ด ───────────────────────────────────────────────────

        private static string StatusChips(string key, SetupState st)
        {
            if (string.IsNullOrEmpty(key) || st == null) return "";
            var sb = new StringBuilder();
            try
            {
                switch (key)
                {
                    case "policy":
                        if (!st.PolicyTableReady) sb.Append(SettingsUi.Chip("ยังไม่ได้ติดตั้งตาราง (ใช้ข้อความสำรอง)", "err"));
                        else
                        {
                            sb.Append(SettingsUi.Chip("ฉบับที่ " + st.PolicyVersion, "info"));
                            if (st.PolicyPlaceholders > 0)
                                sb.Append(SettingsUi.Chip("ยังมีข้อความตัวอย่าง [แก้ไข] " + st.PolicyPlaceholders + " จุด", "warn"));
                            else sb.Append(SettingsUi.Chip("แก้ข้อความแล้ว", "ok"));
                        }
                        break;

                    case "pet":
                        if (!st.PetSchemaReady) sb.Append(SettingsUi.Chip("สัตว์เลี้ยง: ฐานข้อมูลยังไม่พร้อม", "off"));
                        else if (!st.PetEnabled) sb.Append(SettingsUi.Chip("สัตว์เลี้ยง: ปิด", "off"));
                        else
                        {
                            sb.Append(SettingsUi.Chip("สัตว์เลี้ยง: เปิด", "ok"));
                            if (st.PetRooms >= 0)
                                sb.Append(SettingsUi.Chip("รับได้ " + st.PetRooms + " ห้อง", st.PetRooms > 0 ? "info" : "err"));
                        }
                        if (st.PetPlaceholders > 0 && st.PetSchemaReady)
                            sb.Append(SettingsUi.Chip("นโยบายสัตว์เลี้ยงยังมี [แก้ไข]", st.PetEnabled ? "warn" : "off"));
                        break;

                    case "channels":
                        if (!st.CatalogReady) sb.Append(SettingsUi.Chip("ยังไม่ได้ติดตั้งแคตตาล็อก (PHASE19_20)", "err"));
                        else
                        {
                            sb.Append(SettingsUi.Chip("ลูกค้าเห็น " + st.CustomerChannels + " ช่องทาง",
                                st.CustomerChannels > 0 ? "ok" : "err"));
                            sb.Append(SettingsUi.Chip("พนักงานเห็น " + st.StaffChannels + " ช่องทาง", "info"));
                            if (!st.ChannelsReviewedAt.HasValue) sb.Append(SettingsUi.Chip("ยังไม่ได้ตรวจทาน", "warn"));
                        }
                        break;

                    case "gateway":
                        sb.Append(SettingsUi.Chip(st.GatewayText, st.GatewayCls));
                        break;

                    case "deposit":
                        if (!st.HoldEnabled) sb.Append(SettingsUi.Chip("เงินประกัน: ปิด", "off"));
                        else
                        {
                            sb.Append(SettingsUi.Chip("เงินประกัน: เปิด · " + HoldModeText(st.HoldMode), "ok"));
                            if (st.HoldMode == SecurityHoldService.ModeTransfer && !st.HoldTransferReady)
                                sb.Append(SettingsUi.Chip("ยังไม่มีบัญชีรับโอน", "warn"));
                        }
                        break;

                    case "nextacc":
                        if (!st.NextAccEnabled) sb.Append(SettingsUi.Chip("NextAcc: ยังไม่เปิดเชื่อม", "off"));
                        else
                        {
                            sb.Append(SettingsUi.Chip("NextAcc: เชื่อมอยู่", "ok"));
                            if (st.PaidHowUnmapped > 0)
                                sb.Append(SettingsUi.Chip("แหล่งเงินยังไม่ผูก " + st.PaidHowUnmapped, "warn"));
                        }
                        break;
                }
            }
            catch { }
            return sb.ToString();
        }

        private static string HoldModeText(string mode)
        {
            if (mode == SecurityHoldService.ModeCardHold) return "กันวงเงินบัตร";
            if (mode == SecurityHoldService.ModeCash) return "เงินสด";
            return "รับโอน";
        }

        // ── เช็กลิสต์เปิดใช้งาน ────────────────────────────────────────────────

        private sealed class CheckItem
        {
            public string State;   // ok | warn | err | off | info
            public string Title;
            public string DetailHtml;
            public string Url;
            public string LinkText;
            /// <summary>นับรวมในคะแนน "พร้อมแล้ว x / y" (ข้อที่ไม่บังคับ/ข้อมูลประกอบไม่นับ)</summary>
            public bool Counted = true;
        }

        private void RenderChecklist(SetupState st)
        {
            if (st == null) { litChecklist.Text = ""; return; }

            var items = new List<CheckItem>();
            try { items.Add(CheckMigrations(st)); } catch { }
            try { items.Add(CheckPolicies(st)); } catch { }
            try { items.Add(CheckChannels(st)); } catch { }
            try { items.Add(CheckWallets(st)); } catch { }
            try { items.Add(CheckGateway(st)); } catch { }
            try { items.Add(CheckPets(st)); } catch { }
            try { items.Add(CheckDeposit(st)); } catch { }
            items.RemoveAll(x => x == null);
            if (items.Count == 0) { litChecklist.Text = ""; return; }

            int total = 0, done = 0, problems = 0;
            foreach (CheckItem c in items)
            {
                if (!c.Counted) continue;
                total++;
                if (c.State == "ok") done++;
                else if (c.State == "err" || c.State == "warn") problems++;
            }
            int pct = total == 0 ? 100 : (int)Math.Round(done * 100.0 / total);

            var sb = new StringBuilder();
            sb.Append("<details class='sh-check'").Append(problems > 0 ? " open" : "").Append(">");
            sb.Append("<summary><span class='sh-check-title'><i class='fas fa-list-check'></i> เช็กลิสต์เปิดใช้งาน</span>")
              .Append("<span class='sh-check-score ").Append(problems > 0 ? "todo" : "done").Append("'>")
              .Append(problems > 0
                    ? "พร้อมแล้ว " + done + " / " + total + " ข้อ — เหลือ " + problems + " เรื่องที่ควรแก้"
                    : "✓ พร้อมครบ " + done + " / " + total + " ข้อ")
              .Append("</span><span class='sh-check-bar'><span style='width:").Append(pct).Append("%'></span></span></summary>");
            sb.Append("<p class='sh-check-note'>ไล่ทำจากบนลงล่าง — กดปุ่มทางขวาเพื่อไปแก้ที่หน้านั้นได้เลย ")
              .Append("(คำนวณจากค่าที่บันทึกไว้จริง ณ ตอนเปิดหน้านี้)</p><ul class='sh-check-list'>");

            foreach (CheckItem c in items)
            {
                sb.Append("<li class='ck-").Append(c.State).Append("'><span class='ck-ico'>").Append(StateIcon(c.State))
                  .Append("</span><div class='ck-body'><b>").Append(Server.HtmlEncode(c.Title)).Append("</b>")
                  .Append(c.Counted ? "" : " <span class='as-chip off'>ไม่บังคับ</span>")
                  .Append("<div class='ck-detail'>").Append(c.DetailHtml ?? "").Append("</div></div>");
                if (!string.IsNullOrEmpty(c.Url))
                    sb.Append("<a class='ck-go' href='").Append(Server.HtmlEncode(ResolveUrl(c.Url))).Append("'>")
                      .Append(Server.HtmlEncode(c.LinkText ?? "ไปแก้")).Append(" →</a>");
                sb.Append("</li>");
            }
            sb.Append("</ul></details>");
            litChecklist.Text = sb.ToString();
        }

        private static string StateIcon(string state)
        {
            switch (state)
            {
                case "ok": return "✓";
                case "err": return "✕";
                case "warn": return "!";
                case "info": return "i";
                default: return "–";
            }
        }

        private string Enc(string s) { return Server.HtmlEncode(s ?? ""); }

        private CheckItem CheckMigrations(SetupState st)
        {
            var c = new CheckItem { Title = "ฐานข้อมูลอัปเดตครบ (PHASE19 ไมเกรชัน 16–23)" };
            if (st.Migrations == null)
            {
                c.State = "warn";
                c.DetailHtml = "ตรวจโครงสร้างฐานข้อมูลไม่สำเร็จ — ลองโหลดหน้านี้ใหม่";
                return c;
            }
            var missing = new List<string>();
            foreach (KeyValuePair<int, bool> kv in st.Migrations)
                if (!kv.Value) missing.Add(MigrationFile(kv.Key));

            if (missing.Count == 0)
            {
                c.State = "ok";
                c.DetailHtml = "ติดตั้งครบทั้ง 8 ไฟล์ (เบอร์โทร OTA, โหมดเก็บเงิน OTA, หัวเอกสาร NextAcc, ใบรับรองแทนใบเสร็จ, "
                             + "ช่องทางชำระเงิน, กระเป๋าเงิน NextAcc, นโยบายการจอง, สัตว์เลี้ยง)";
                return c;
            }
            c.State = "err";
            var sb = new StringBuilder("ยังไม่ได้รัน " + missing.Count + " ไฟล์ — ให้ผู้ดูแลฐานข้อมูลรันใน SQL Server ตามลำดับ "
                + "(ทุกไฟล์รันซ้ำได้ ไม่ลบข้อมูล):<ul class='ck-sub'>");
            foreach (string f in missing) sb.Append("<li><code>").Append(Enc(f)).Append("</code></li>");
            sb.Append("</ul>");
            c.DetailHtml = sb.ToString();
            if (IsOwner) { c.Url = "~/Admin/DatabaseManagement"; c.LinkText = "หน้าฐานข้อมูล"; }
            return c;
        }

        private static string MigrationFile(int n)
        {
            switch (n)
            {
                case 16: return "Database/PHASE19_Migration_16_Fix_Placeholder_Guest_Phone.sql — แยกลูกค้า OTA ที่เบอร์โทรซ้ำ";
                case 17: return "Database/PHASE19_Migration_17_OTA_Collect_Mode.sql — ใครเก็บเงินค่าห้อง OTA";
                case 18: return "Database/PHASE19_Migration_18_Receipt_Header_Type.sql — หัวกระดาษเอกสารขาย NextAcc";
                case 19: return "Database/PHASE19_Migration_19_Certificate_In_Lieu.sql — ใบรับรองแทนใบเสร็จ";
                case 20: return "Database/PHASE19_Migration_20_Payment_Channel_Catalog.sql — ช่องทางชำระเงิน";
                case 21: return "Database/PHASE19_Migration_21_NextAcc_Wallet_Mapping.sql — กระเป๋าเงิน NextAcc";
                case 22: return "Database/PHASE19_Migration_22_Booking_Policies.sql — นโยบายการจอง";
                case 23: return "Database/PHASE19_Migration_23_Pet_Stay.sql — สัตว์เลี้ยงเข้าพัก";
                default: return "PHASE19_Migration_" + n;
            }
        }

        private CheckItem CheckPolicies(SetupState st)
        {
            var c = new CheckItem
            {
                Title = "นโยบายการจองแก้เป็นข้อความจริงแล้ว",
                Url = "~/Admin/Settings/BookingPolicies",
                LinkText = "แก้นโยบาย"
            };
            if (!st.PolicyTableReady)
            {
                c.State = "err";
                c.DetailHtml = "ยังไม่มีตารางนโยบาย — หน้าจองใช้ข้อความสำรองแบบสั้นอยู่ (รัน PHASE19_Migration_22 ก่อน)";
            }
            else if (st.PolicyPlaceholders > 0)
            {
                c.State = "warn";
                c.DetailHtml = "ยังมีข้อความตัวอย่าง/ช่อง <b>[แก้ไข: …]</b> ค้างอยู่ <b>" + st.PolicyPlaceholders
                             + "</b> จุด — ลูกค้าจะเห็นข้อความนี้ตามจริง";
            }
            else
            {
                c.State = "ok";
                c.DetailHtml = "ฉบับที่ " + st.PolicyVersion
                             + (st.PolicyUpdatedAt.HasValue ? " · แก้ไขล่าสุด " + st.PolicyUpdatedAt.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "");
            }
            return c;
        }

        private CheckItem CheckChannels(SetupState st)
        {
            var c = new CheckItem
            {
                Title = "ตรวจช่องทางชำระเงินที่ลูกค้า/พนักงานเห็นแล้ว",
                Url = CanAny(Perm.SysPayment + "|" + Perm.SysSettings) ? "~/Admin/Settings/PaymentChannels" : null,
                LinkText = "ตรวจช่องทาง"
            };
            if (!st.CatalogReady)
            {
                c.State = "err";
                c.DetailHtml = "ยังไม่ได้ติดตั้งแคตตาล็อกช่องทาง (PHASE19_Migration_20) — ตอนนี้ระบบเดาจากชื่อแหล่งเงิน";
            }
            else if (st.CustomerChannels <= 0)
            {
                c.State = "err";
                c.DetailHtml = "ลูกค้า<b>ไม่เห็นช่องทางชำระเงินเลย</b> — ติ๊ก \"ลูกค้าเห็น\" ให้ช่องทางโอนอย่างน้อยหนึ่งช่อง";
            }
            else if (!st.ChannelsReviewedAt.HasValue)
            {
                c.State = "warn";
                c.DetailHtml = "ลูกค้าเห็น " + st.CustomerChannels + " ช่องทาง · พนักงานเห็น " + st.StaffChannels
                             + " ช่องทาง — เปิดหน้าช่องทาง ตรวจข้อความแนะนำ/เลขบัญชีให้ถูก แล้วกด <b>บันทึก</b> หนึ่งครั้งเพื่อยืนยัน";
            }
            else
            {
                c.State = "ok";
                c.DetailHtml = "ลูกค้าเห็น " + st.CustomerChannels + " ช่องทาง · พนักงานเห็น " + st.StaffChannels
                             + " ช่องทาง · ตรวจล่าสุด " + st.ChannelsReviewedAt.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
            }
            return c;
        }

        private CheckItem CheckWallets(SetupState st)
        {
            var c = new CheckItem
            {
                Title = "แหล่งเงินผูกกับบัญชี NextAcc ครบ",
                Url = Perm.CanAccess(Perm.SysAccounting) ? "~/Admin/Settings/AccountingIntegration" : null,
                LinkText = "ผูกแหล่งเงิน"
            };
            if (!st.NextAccEnabled)
            {
                c.State = "off";
                c.Counted = false;
                c.DetailHtml = "ยังไม่ได้เปิดเชื่อม NextAcc — ข้ามข้อนี้ได้ (เปิดเมื่อพร้อมส่งข้อมูลเข้าระบบบัญชี)";
                return c;
            }
            if (st.PaidHowActive < 0)
            {
                c.State = "warn";
                c.DetailHtml = "อ่านรายการแหล่งเงินไม่สำเร็จ";
                return c;
            }
            if (st.PaidHowUnmapped > 0)
            {
                c.State = "warn";
                var sb = new StringBuilder("ยังไม่ผูก <b>" + st.PaidHowUnmapped + "</b> จาก " + st.PaidHowActive
                    + " แหล่งเงินที่เปิดใช้ — รับ/จ่ายผ่านแหล่งนี้ NextAcc จะเลือกบัญชีเอง (อาจลงผิดบัญชีธนาคาร): ");
                for (int i = 0; i < st.UnmappedNames.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(Enc(st.UnmappedNames[i]));
                }
                if (st.PaidHowUnmapped > st.UnmappedNames.Count) sb.Append(" …");
                c.DetailHtml = sb.ToString();
                return c;
            }
            c.State = "ok";
            c.DetailHtml = "ผูกครบทั้ง " + st.PaidHowActive + " แหล่งเงินที่เปิดใช้";
            return c;
        }

        private CheckItem CheckGateway(SetupState st)
        {
            var c = new CheckItem
            {
                Title = "รับชำระเงินออนไลน์ (PaySo)",
                Url = CanAny(Perm.SysPayment + "|" + Perm.SysSettings) ? "~/Admin/Settings/PaymentGateway" : null,
                LinkText = "ตั้งค่าเกตเวย์",
                Counted = false
            };
            c.State = st.GatewayCls == "ok" ? "ok" : (st.GatewayCls == "warn" ? "warn" : "info");
            c.DetailHtml = Enc(st.GatewayDetail);
            return c;
        }

        private CheckItem CheckPets(SetupState st)
        {
            var c = new CheckItem
            {
                Title = "สัตว์เลี้ยงเข้าพัก — ตั้งห้องที่รับและนโยบายแล้ว",
                Url = "~/Admin/Settings/PetStay",
                LinkText = "ตั้งค่าสัตว์เลี้ยง"
            };
            if (!st.PetSchemaReady || !st.PetEnabled)
            {
                c.State = "off";
                c.Counted = false;
                c.DetailHtml = !st.PetSchemaReady
                    ? "ฐานข้อมูลยังไม่พร้อม (PHASE19_Migration_23) — หน้าจองไม่แสดงส่วนสัตว์เลี้ยง"
                    : "ปิดอยู่ — หน้าจองไม่แสดงส่วนสัตว์เลี้ยง (เปิดเมื่อพร้อมรับ)";
                return c;
            }
            if (st.PetRooms == 0)
            {
                c.State = "err";
                c.DetailHtml = "เปิดฟีเจอร์แล้ว แต่<b>ยังไม่มีห้องที่รับสัตว์เลี้ยง</b> — ลูกค้าจะเห็นแต่ \"ห้องนี้ไม่รับสัตว์เลี้ยง\"";
                return c;
            }
            if (st.PetPlaceholders > 0)
            {
                c.State = "warn";
                c.DetailHtml = "นโยบายสัตว์เลี้ยงยังมีข้อความตัวอย่าง/ช่อง <b>[แก้ไข: …]</b> " + st.PetPlaceholders + " จุด";
                return c;
            }
            c.State = "ok";
            c.DetailHtml = "เปิดอยู่ · รับได้ " + (st.PetRooms < 0 ? "?" : st.PetRooms.ToString(CultureInfo.InvariantCulture))
                         + " ห้อง · คิดค่าบริการ " + Enc(PetStay.UnitLabel(st.PetUnit));
            return c;
        }

        private CheckItem CheckDeposit(SetupState st)
        {
            var c = new CheckItem
            {
                Title = "เงินประกันความเสียหาย",
                Url = CanAny(Perm.SysPayment + "|" + Perm.SysSettings) ? "~/Admin/Settings/SecurityDeposit" : null,
                LinkText = "ตั้งค่าเงินประกัน"
            };
            if (!st.HoldEnabled)
            {
                c.State = "off";
                c.Counted = false;
                c.DetailHtml = "ปิดอยู่ — ไม่เก็บเงินประกันในระบบ";
                return c;
            }
            if (st.HoldMode == SecurityHoldService.ModeTransfer && !st.HoldTransferReady)
            {
                c.State = "warn";
                c.DetailHtml = "เปิดใช้แบบรับโอนแล้ว แต่ยังไม่มีบัญชีรับโอน — ตั้งช่องทางโอนที่หน้า ช่องทางชำระเงิน";
                return c;
            }
            c.State = "ok";
            c.DetailHtml = "เปิดอยู่ · " + Enc(HoldModeText(st.HoldMode));
            return c;
        }

        // ── อ่านสถานะทั้งหมดครั้งเดียว (อ่านอย่างเดียว — ทุกส่วนพังแยกกันได้) ───────────

        private sealed class SetupState
        {
            public SortedDictionary<int, bool> Migrations;

            public bool PolicyTableReady;
            public int PolicyVersion = 1;
            public DateTime? PolicyUpdatedAt;
            public int PolicyPlaceholders;
            public int PetPlaceholders;

            public bool PetSchemaReady;
            public bool PetEnabled;
            public int PetRooms = -1;
            public string PetUnit = PetStay.UnitNight;

            public bool CatalogReady;
            public int CustomerChannels;
            public int StaffChannels;
            public DateTime? ChannelsReviewedAt;

            public string GatewayText = "รับชำระออนไลน์: ปิด";
            public string GatewayCls = "off";
            public string GatewayDetail = "";

            public bool HoldEnabled;
            public string HoldMode = SecurityHoldService.ModeTransfer;
            public bool HoldTransferReady;

            public bool NextAccEnabled;
            public int PaidHowActive = -1;
            public int PaidHowUnmapped = -1;
            public List<string> UnmappedNames = new List<string>();

            public static SetupState Load(string conn)
            {
                var s = new SetupState();
                var db = new code();

                // ไมเกรชัน PHASE19 16–23 — ตรวจจากตาราง/คอลัมน์/ค่าตั้งที่แต่ละไฟล์สร้าง
                try
                {
                    DataTable dt = db.DatabaseQuerySafe(conn, @"
                        DECLARE @m16 INT = 0, @m18 INT = 0;
                        IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
                        BEGIN
                            IF EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_DefaultPhone') SET @m16 = 1;
                            IF EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Receipt_Header_Type') SET @m18 = 1;
                        END
                        SELECT @m16 AS M16,
                               CASE WHEN COL_LENGTH('dbo.Reservation', 'OTA_Collect_Mode') IS NULL THEN 0 ELSE 1 END AS M17,
                               @m18 AS M18,
                               CASE WHEN COL_LENGTH('dbo.Account_Payment', 'Is_Certificate_In_Lieu') IS NULL THEN 0 ELSE 1 END AS M19,
                               CASE WHEN COL_LENGTH('dbo.Account_Paid_How', 'Channel_Code') IS NULL THEN 0 ELSE 1 END AS M20,
                               CASE WHEN COL_LENGTH('dbo.Account_Paid_How', 'Nexaacc_BankAccountId') IS NULL
                                      OR OBJECT_ID('dbo.Accounting_Nexaacc_BankAccounts', 'U') IS NULL THEN 0 ELSE 1 END AS M21,
                               CASE WHEN OBJECT_ID('dbo.Booking_Policy_Config', 'U') IS NULL
                                      OR COL_LENGTH('dbo.Reservation', 'Policy_Accepted_Version') IS NULL THEN 0 ELSE 1 END AS M22,
                               CASE WHEN COL_LENGTH('dbo.Accommodation', 'Pet_Allowed') IS NULL
                                      OR COL_LENGTH('dbo.Reservation', 'Pet_Policy_Version') IS NULL THEN 0 ELSE 1 END AS M23", null);
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        var m = new SortedDictionary<int, bool>();
                        for (int n = 16; n <= 23; n++)
                        {
                            object o = dt.Rows[0]["M" + n];
                            m[n] = o != null && o != DBNull.Value && Convert.ToInt32(o) == 1;
                        }
                        s.Migrations = m;
                    }
                }
                catch { s.Migrations = null; }

                // นโยบาย
                try
                {
                    // มีแถวในตารางนโยบาย = ติดตั้งแล้ว (เกณฑ์เดียวกับหน้านโยบายการจอง)
                    s.PolicyUpdatedAt = BookingPolicy.UpdatedAt;
                    s.PolicyTableReady = s.PolicyUpdatedAt.HasValue;
                    s.PolicyVersion = BookingPolicy.Version;
                    int ph = 0;
                    foreach (string k in BookingPolicy.TextKeys) ph += SettingsUi.CountPlaceholders(BookingPolicy.Get(k));
                    s.PolicyPlaceholders = ph;
                    s.PetPlaceholders = SettingsUi.CountPlaceholders(BookingPolicy.Get(BookingPolicy.KeyPet));
                }
                catch { }

                // สัตว์เลี้ยง
                try
                {
                    s.PetSchemaReady = PetStay.SchemaReady;
                    s.PetEnabled = PetStay.Enabled;
                    s.PetUnit = PetStay.FeeUnit;
                    if (s.PetSchemaReady)
                    {
                        DataTable pr = db.DatabaseQuerySafe(conn,
                            "SELECT COUNT(*) AS N FROM Accommodation WHERE Status = 1 AND Pet_Allowed = 1", null);
                        if (pr != null && pr.Rows.Count > 0) s.PetRooms = Convert.ToInt32(pr.Rows[0]["N"]);
                    }
                }
                catch { s.PetRooms = -1; }

                // ช่องทางชำระเงิน
                try
                {
                    s.CatalogReady = PaymentChannelCatalog.CatalogColumnsReady;
                    s.CustomerChannels = PaymentChannelCatalog.ForCustomer(PaymentSource.Reservation).Count;
                    s.StaffChannels = PaymentChannelCatalog.ForStaff(PaymentSource.Reservation).Count;
                    DateTime reviewed;
                    string rv = BookingPolicy.GetSetting(ChannelsReviewedKey);
                    if (!string.IsNullOrEmpty(rv)
                        && DateTime.TryParseExact(rv, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                                                  DateTimeStyles.None, out reviewed))
                        s.ChannelsReviewedAt = reviewed;
                }
                catch { }

                // เกตเวย์
                try { LoadGateway(s); } catch { }

                // เงินประกัน
                try
                {
                    s.HoldEnabled = PaymentGatewayConfig.GetBool("Payment_SecurityHold_Enabled", false);
                    s.HoldMode = SecurityHoldService.Mode;
                    s.HoldTransferReady = SecurityHoldService.TransferChannel() != null;
                }
                catch { }

                // NextAcc + แหล่งเงินที่ยังไม่ผูกบัญชี
                try
                {
                    s.NextAccEnabled = new Take_Time_BangPhra.Integration.AccountingConfig(conn).Enabled;
                    if (s.NextAccEnabled)
                    {
                        DataTable ph = db.DatabaseQuerySafe(conn,
                            "SELECT Paid_How, CASE WHEN Nexaacc_AccountId IS NULL THEN 0 ELSE 1 END AS Mapped "
                            + "FROM Account_Paid_How WHERE Status = 'True' ORDER BY Paid_How", null);
                        if (ph != null)
                        {
                            s.PaidHowActive = ph.Rows.Count;
                            s.PaidHowUnmapped = 0;
                            foreach (DataRow r in ph.Rows)
                            {
                                if (Convert.ToInt32(r["Mapped"]) == 1) continue;
                                s.PaidHowUnmapped++;
                                if (s.UnmappedNames.Count < 6) s.UnmappedNames.Add(Convert.ToString(r["Paid_How"]));
                            }
                        }
                    }
                }
                catch { s.PaidHowActive = -1; }

                return s;
            }

            private static void LoadGateway(SetupState s)
            {
                bool feature = Feature.On("OnlinePayment");
                bool enabled = PaymentGatewayConfig.IsEnabled;
                bool payso = PaymentGatewayConfig.ActiveProvider == PaymentGatewayConfig.ProviderPayso;
                bool ready = PaymentGatewayConfig.IsGatewayReady;

                if (!feature || !enabled)
                {
                    s.GatewayText = payso ? "PaySo: ยังไม่เปิด" : "รับชำระออนไลน์: ปิด";
                    s.GatewayCls = "off";
                    s.GatewayDetail = "ยังไม่เปิดรับชำระออนไลน์ — ลูกค้าเห็นเฉพาะการโอน/แนบสลิป ซึ่งใช้งานได้ตามปกติ "
                                    + (feature ? "(เปิดได้ที่หน้าตั้งค่าเกตเวย์ ข้อ ๑)" : "(สวิตช์ฟีเจอร์ \"รับชำระเงินออนไลน์\" ปิดอยู่ที่หน้าตั้งค่าระบบ)");
                    return;
                }
                if (ready)
                {
                    bool sandbox = payso && PaymentGatewayConfig.IsSandbox;
                    s.GatewayText = (payso ? "PaySo" : "Omise (สำรอง)") + (sandbox ? ": พร้อม (โหมดทดสอบ)" : ": พร้อมใช้");
                    s.GatewayCls = sandbox ? "warn" : "ok";
                    s.GatewayDetail = sandbox
                        ? "เชื่อมต่อแล้วแต่ยังเป็นโหมดทดสอบ (Sandbox) — ยังไม่ตัดเงินจริง เปลี่ยนเป็นโหมดใช้งานจริงเมื่อทดสอบผ่าน"
                        : "ลูกค้าเห็นช่องทางเกตเวย์ได้ตามที่ติ๊กไว้ในหน้าช่องทางชำระเงิน";
                    return;
                }
                s.GatewayText = payso ? "PaySo: รอเปิดใช้" : "Omise: ตั้งค่าไม่ครบ";
                s.GatewayCls = "warn";
                s.GatewayDetail = payso
                    ? "เปิดระบบแล้วแต่ PaySo ยังไม่พร้อม (รออนุมัติร้านค้า / ยังไม่ใส่ Base URL + กุญแจ) — ระหว่างนี้ลูกค้าเห็นเฉพาะการโอน"
                    : "เลือก Omise ไว้แต่ยังไม่ได้เปิด/ใส่ Secret Key";
            }
        }

        /// <summary>แปลง **ข้อความ** เป็นตัวหนา (หลัง HtmlEncode แล้ว จึงปลอดภัย)</summary>
        private static string Bold(string encoded)
        {
            if (string.IsNullOrEmpty(encoded) || encoded.IndexOf("**", StringComparison.Ordinal) < 0)
                return encoded;
            var parts = encoded.Split(new[] { "**" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
                sb.Append(i % 2 == 1 ? "<b>" + parts[i] + "</b>" : parts[i]);
            return sb.ToString();
        }
    }
}
