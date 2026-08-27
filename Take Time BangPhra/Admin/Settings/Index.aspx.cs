using System;
using System.Collections.Generic;
using System.Text;
using System.Web.UI;

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
    /// และบอกสถานะฟีเจอร์ที่ปิดอยู่ให้เห็นด้วย
    /// </summary>
    public partial class SettingsIndex : Page
    {
        private bool IsOwner => Session["User"]?.ToString() == "Owner";

        /// <summary>รายการตั้งค่า 1 ใบ</summary>
        private class Item
        {
            public string Title, Desc, Url, Keywords;
            public bool OwnerOnly;
            /// <summary>ชื่อฟีเจอร์ใน Feature flags — ปิดอยู่จะขึ้นป้ายเตือน (null = ไม่ผูก)</summary>
            public string Feature;

            /// <summary>โมดูลสิทธิ์เฉพาะรายการ — ทับของหมวด (null = ใช้ของหมวด)
            /// ใช้เมื่อหน้าปลายทาง guard ด้วยโมดูลอื่นอยู่แล้ว จะได้ไม่โชว์การ์ดที่กดแล้วเด้งออก</summary>
            public string Module;

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
            /// <summary>โมดูลสิทธิ์ประจำหมวด — รายการในหมวดใช้ตัวนี้ (null = SYS_SETTINGS)</summary>
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
            if (!IsPostBack) Render();
        }

        private List<Group> BuildCatalog()
        {
            var groups = new List<Group>();

            // ── 1. เชื่อมต่อระบบภายนอก ────────────────────────────────────────────
            var conn = new Group("การเชื่อมต่อ & ระบบ",
                "Token / API / อีเมล / ที่เก็บไฟล์ — ค่าที่ทำให้ระบบคุยกับบริการภายนอกได้",
                "fa-plug", "#546e7a");
            conn.Items.Add(new Item("กลุ่มสิทธิ์ผู้ใช้",
                "สร้างกลุ่มสิทธิ์เอง แล้วกำหนดว่าแต่ละกลุ่มมองเห็น/เข้าใช้งานส่วนไหนได้บ้าง + ผูกพนักงานเข้ากลุ่ม",
                "~/Admin/Settings/PermissionGroups",
                "สิทธิ์ permission กลุ่ม role ผู้ใช้ พนักงาน เข้าถึง มองเห็น เมนู owner admin staff", true));
            conn.Items.Add(new Item("ตั้งค่าระบบ (Token / API / SMTP)",
                "LINE Token, Telegram, อีเมลส่งออก, API Key, path เก็บไฟล์ — แก้ได้โดยไม่ต้องแตะ Web.config",
                "~/Admin/Settings/SystemSettings",
                "token api key line telegram smtp email อีเมล รหัสผ่าน path ไฟล์ web.config ตั้งค่าระบบ", true));
            conn.Items.Add(new Item("บัญชี LINE ของฉัน / ทีม",
                "ผูกบัญชี LINE เพื่อรับแจ้งเตือนส่วนตัว, ตั้งค่า LINE Login, Callback URL, บังคับ add friend",
                "~/Admin/Settings/LineAccount",
                "line login ไลน์ ผูกบัญชี callback add friend แจ้งเตือน uid"));
            conn.Items.Add(new Item("การแจ้งเตือน",
                "เลือกว่าเหตุการณ์ไหนแจ้งใคร ทางช่องทางใด",
                "~/Admin/Notifications/Settings",
                "notification แจ้งเตือน alert เตือน line telegram"));
            conn.Items.Add(new Item("Connection Settings",
                "การเชื่อมต่อฐานข้อมูลและบริการอื่น ๆ",
                "~/Admin/Settings/ConnectionSettings",
                "connection database ฐานข้อมูล เชื่อมต่อ", true));
            groups.Add(conn);

            // ── 2. บัญชี ภาษี และการลงรายได้ ──────────────────────────────────────
            var acc = new Group("บัญชี ภาษี & การลงรายได้",
                "เชื่อม NextAcc, อ่านอีเมลจอง OTA, และเลือกว่าจะลงบันทึกรายได้ทางไหนบ้าง",
                "fa-calculator", "#00897b", Perm.SysAccounting);
            acc.Items.Add(new Item("Accounting Integration (NextAcc)",
                "เชื่อมระบบบัญชี, ผังบัญชี, โหมด sync, อ่านอีเมลจอง OTA, สวิตช์ลงบันทึกรายได้ (ขายหน้าร้าน / รูมเซอร์วิส / ค่าห้อง OTA)",
                "~/Admin/Settings/AccountingIntegration",
                "บัญชี nextacc accounting ภาษี vat ผังบัญชี sync ใบกำกับ อีเมลจอง ota รายได้ รวบยอด รูมเซอร์วิส", true));
            acc.Items.Add(new Item("ตั้งค่าลงบัญชีรายสินค้า",
                "เลือกรายสินค้า ว่าการขายจะรวมเข้า **ใบสรุปรายได้รายวัน** หรือไม่ (เช่น หมูกระทะที่ให้รายได้ไปรวมกับค่าห้อง)",
                "~/Admin/Settings/ProductAccounting",
                "สินค้า รายสินค้า ใบสรุป รายวัน รวบยอด rollup หมูกระทะ รายได้ ลงบัญชี ขายหน้าร้าน", true));
            acc.Items.Add(new Item("รับชำระเงินออนไลน์ (Payso)",
                "ให้ลูกค้าเลือกจ่ายด้วย **บัตรเครดิต/QR ตัดยอดอัตโนมัติ** หรือสแกน QR แนบสลิปแบบเดิม + ดูรายการชำระเงิน",
                "~/Admin/Settings/PaymentGateway",
                "จ่ายเงิน ชำระเงิน บัตรเครดิต payso เกตเวย์ gateway qr พร้อมเพย์ webhook รับเงิน ออนไลน์",
                false, null, Perm.SysPayment));
            acc.Items.Add(new Item("สิทธิประโยชน์ระดับสมาชิก (Tier)",
                "กำหนดส่วนลด/สิทธิพิเศษของแต่ละระดับสมาชิก",
                "~/Account/TierBenefitsManagement",
                "tier สมาชิก ระดับ ส่วนลด สิทธิประโยชน์ loyalty", false, "Loyalty"));
            groups.Add(acc);

            // ── 3. ช่องทางลูกค้า & AI ─────────────────────────────────────────────
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

            // ── 4. บริการในที่พัก ─────────────────────────────────────────────────
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

            // ── 5. ราคา & ช่องทางขาย ──────────────────────────────────────────────
            var price = new Group("ราคา & ช่องทางขาย",
                "ราคาห้องพักตามช่วงเวลา และช่องทางขายออนไลน์",
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
            groups.Add(price);

            // ── 6. เนื้อหาเว็บไซต์ ────────────────────────────────────────────────
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

            // ── 7. ข้อมูลหลัก & ขั้นสูง ───────────────────────────────────────────
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

        private void Render()
        {
            var sb = new StringBuilder();
            foreach (var g in BuildCatalog())
            {
                // ไม่มีสิทธิ์โมดูลของหมวดนี้ → ไม่ต้องแสดงทั้งหมวด
                if (!Perm.CanAccess(string.IsNullOrEmpty(g.Module) ? Perm.SysSettings : g.Module)) continue;

                var visible = new List<Item>();
                foreach (var it in g.Items)
                {
                    if (it.OwnerOnly && !IsOwner) continue;
                    // รายการที่ระบุโมดูลเอง ต้องมีสิทธิ์โมดูลนั้นด้วย
                    if (!string.IsNullOrEmpty(it.Module) && !Perm.CanAccess(it.Module)) continue;
                    visible.Add(it);
                }
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
                    sb.Append("</a>");
                }
                sb.Append("</div></div>");
            }
            litGroups.Text = sb.ToString();
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
