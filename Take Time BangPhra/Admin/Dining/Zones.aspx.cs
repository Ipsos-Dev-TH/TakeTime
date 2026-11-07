using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Dining;
using Take_Time_BangPhra.Integration;
using Take_Time_BangPhra.Payments.SlipCheck;

namespace Take_Time_BangPhra.Admin.Dining
{
    /// <summary>
    /// QR สั่งอาหาร — โซน &amp; โต๊ะ (CRUD, สร้างหลายโต๊ะ, หมุน QR, ปิดใช้) + แท็บ "ตั้งค่า &amp; เช็กลิสต์"
    /// (Feature_Dining + ค่าตั้ง Dining_* + ลิงก์ไปโมดูลที่ใช้ร่วม) — ใช้ <see cref="DiningTableService"/> / <see cref="DiningQr"/> ตาม Class/Dining/README_API.md
    /// สิทธิ์ตั้งค่า = Perm.SvcGuest (เหมือน Admin/RoomService/OrderSettings) — ไม่ guard ฟีเจอร์ เพราะต้องตั้งค่าได้ก่อนเปิดสวิตช์
    /// ทุกข้อความลง HTML ผ่าน H() (HtmlEncode) เสมอ
    /// </summary>
    public partial class ZonesPage : Page
    {
        private static readonly string Conn = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private readonly DiningTableService _tables = new DiningTableService(Conn);
        private readonly DiningMenuService _menu = new DiningMenuService(Conn);

        private const string FeatureKey = "Feature_Dining";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SvcGuest)) return;
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login", false);
                HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }
            if (!IsPostBack)
            {
                BindZoneDropdowns();
                int zone;
                if (int.TryParse(Request.QueryString["zone"], out zone) && ddlZoneFilter.Items.FindByValue(zone.ToString(CultureInfo.InvariantCulture)) != null)
                    ddlZoneFilter.SelectedValue = zone.ToString(CultureInfo.InvariantCulture);
                LoadSettings();
                if (Request.QueryString["focus"] == "settings") hfTab.Value = "settings";
            }
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            if (Response.IsRequestBeingRedirected) return;
            RenderAll();
        }

        // ── helpers ────────────────────────────────────────────────────────────

        private static string H(string s) { return HttpUtility.HtmlEncode(s ?? ""); }
        private static string Inv(int v) { return v.ToString(CultureInfo.InvariantCulture); }

        private void Msg(string cls, string text)
        {
            litMsg.Text += "<div class=\"dn-alert " + cls + "\">" + H(text) + "</div>";
        }

        private string StaffName() { return Session["username"]?.ToString() ?? "ADMIN"; }

        private short? AdminId()
        {
            short v;
            return short.TryParse(Convert.ToString(Session["admin_id"]), out v) ? v : (short?)null;
        }

        private int SelectedZoneFilter()
        {
            int z;
            return int.TryParse(ddlZoneFilter.SelectedValue, out z) ? z : 0;
        }

        private void BindZoneDropdowns()
        {
            var zones = _tables.ListZones(true);
            string keepFilter = ddlZoneFilter.SelectedValue, keepTable = ddlTableZone.SelectedValue, keepBulk = ddlBulkZone.SelectedValue;
            ddlZoneFilter.Items.Clear();
            ddlZoneFilter.Items.Add(new ListItem("ทุกโซน", "0"));
            ddlTableZone.Items.Clear();
            ddlBulkZone.Items.Clear();
            foreach (var z in zones)
            {
                string label = z.Code + " — " + z.NameTh + (z.IsActive ? "" : " (ปิด)");
                ddlZoneFilter.Items.Add(new ListItem(label, Inv(z.Id)));
                ddlTableZone.Items.Add(new ListItem(label, Inv(z.Id)));
                ddlBulkZone.Items.Add(new ListItem(label, Inv(z.Id)));
            }
            if (!string.IsNullOrEmpty(keepFilter) && ddlZoneFilter.Items.FindByValue(keepFilter) != null) ddlZoneFilter.SelectedValue = keepFilter;
            if (!string.IsNullOrEmpty(keepTable) && ddlTableZone.Items.FindByValue(keepTable) != null) ddlTableZone.SelectedValue = keepTable;
            if (!string.IsNullOrEmpty(keepBulk) && ddlBulkZone.Items.FindByValue(keepBulk) != null) ddlBulkZone.SelectedValue = keepBulk;
        }

        private void HideForms()
        {
            pnlZoneForm.Visible = false;
            pnlTableForm.Visible = false;
            pnlBulk.Visible = false;
        }

        // ── วาดทั้งหน้า ─────────────────────────────────────────────────────────

        private void RenderAll()
        {
            bool ready = _tables.SchemaReady();
            if (!ready)
                litMsg.Text = "<div class=\"dn-alert err\">ยังไม่ได้รัน <b>Database/PHASE19_Migration_42_Dining_QR_Ordering.sql</b> — ตารางโซน/โต๊ะ/เมนูยังไม่มี "
                    + "(รันแล้ว recycle App Pool แล้วกด \"ตรวจอีกครั้ง\" ในแท็บตั้งค่า)</div>" + litMsg.Text;
            RenderZones();
            RenderTables();
            RenderChecklist(ready);
            RenderLinks();
            int zf = SelectedZoneFilter();
            lnkPrintZone.HRef = ResolveUrl("~/Admin/Dining/QrSheet") + (zf > 0 ? "?zone=" + Inv(zf) : "");
        }

        private void RenderZones()
        {
            var zones = _tables.ListZones(true);
            var sb = new StringBuilder();
            if (zones.Count == 0)
            {
                litZones.Text = "<div class=\"dn-alert info\" style=\"margin:10px;\">ยังไม่มีโซน — กด \"เพิ่มโซน\" (เช่น RIVER ริมน้ำ, HALL ในร้าน)</div>";
                return;
            }
            sb.Append("<table class=\"dn-t\"><thead><tr><th>รหัส</th><th>ชื่อโซน</th><th>สถานะตอนนี้</th><th>เวลา / โหมด</th><th>วิธีชำระ</th><th>ครัว</th><th class=\"num\">โต๊ะ</th><th>จัดการ</th></tr></thead><tbody>");
            foreach (var z in zones)
            {
                string msg = null;
                bool open = z.IsActive && DiningTableService.IsZoneOpen(z, out msg);
                sb.Append("<tr").Append(z.IsActive ? ">" : " class=\"off\">");
                sb.Append("<td><b>").Append(H(z.Code)).Append("</b></td>");
                sb.Append("<td>").Append(H(z.NameTh)).Append("<div class=\"mini\">ลำดับ ").Append(Inv(z.SortOrder)).Append("</div></td>");
                sb.Append("<td>").Append(!z.IsActive ? Chip("ปิดใช้งาน", "off") : open ? Chip("เปิดรับออเดอร์", "ok") : Chip("ปิดรับ", "warn"))
                  .Append(!z.IsActive || open || string.IsNullOrEmpty(msg) ? "" : "<div class=\"mini\">" + H(msg) + "</div>").Append("</td>");
                string mode = z.ManualMode == "OPEN" ? "บังคับเปิด" : z.ManualMode == "CLOSED" ? "บังคับปิด" : "อัตโนมัติ";
                string time = z.OpenTime.HasValue && z.CloseTime.HasValue ? z.OpenTime.Value.ToString(@"hh\:mm") + "–" + z.CloseTime.Value.ToString(@"hh\:mm") : "ไม่ตั้งเวลา";
                sb.Append("<td>").Append(H(time)).Append("<div class=\"mini\">").Append(H(mode)).Append("</div></td>");
                var pay = new List<string>();
                if (z.AllowCash) pay.Add("เงินสด");
                if (z.AllowTransfer) pay.Add("โอน");
                if (z.AllowRoomCharge) pay.Add("ลงบิลห้อง");
                sb.Append("<td>").Append(H(string.Join(" · ", pay.ToArray()))).Append("</td>");
                sb.Append("<td>").Append(H(z.KitchenStation ?? "-")).Append("</td>");
                sb.Append("<td class=\"num\">").Append(Inv(z.TableCount)).Append("</td>");
                sb.Append("<td><div class=\"dn-actions\">");
                sb.Append(Btn("editZone", z.Id, "แก้ไข", "sec", null));
                sb.Append(Btn("toggleZone", z.Id, z.IsActive ? "ปิดใช้" : "เปิดใช้", z.IsActive ? "danger" : "sec",
                    z.IsActive ? "ปิดใช้งานโซน " + z.Code + "? ทุกโต๊ะในโซนจะสั่งไม่ได้" : null));
                sb.Append("<a class=\"dn-btn sm sec\" href=\"").Append(ResolveUrl("~/Admin/Dining/Zones")).Append("?zone=").Append(Inv(z.Id)).Append("#t-zones\">โต๊ะ</a>");
                sb.Append("<a class=\"dn-btn sm sec\" href=\"").Append(ResolveUrl("~/Admin/Dining/QrSheet")).Append("?zone=").Append(Inv(z.Id)).Append("\">พิมพ์ QR</a>");
                sb.Append(Btn("rotateZone", z.Id, "หมุน QR ทั้งโซน", "danger",
                    "หมุน QR ทุกโต๊ะในโซน " + z.Code + "?\n\nQR ที่พิมพ์ติดโต๊ะอยู่จะใช้ไม่ได้ทันที ต้องพิมพ์ใหม่ทั้งโซน (ลูกค้าที่นั่งสั่งอยู่ไม่กระทบ)"));
                sb.Append("</div></td></tr>");
            }
            sb.Append("</tbody></table>");
            litZones.Text = sb.ToString();
        }

        private void RenderTables()
        {
            int zf = SelectedZoneFilter();
            var tables = _tables.ListTables(zf > 0 ? (int?)zf : null, true);
            if (tables.Count == 0)
            {
                litTables.Text = "<div class=\"dn-alert info\" style=\"margin:10px;\">ยังไม่มีโต๊ะ" + (zf > 0 ? "ในโซนนี้" : "") + " — กด \"สร้างหลายโต๊ะ\" (เช่น A1–A20) หรือ \"เพิ่มโต๊ะ\"</div>";
                return;
            }
            bool engine = DiningQr.EngineReady();
            var sb = new StringBuilder();
            if (!engine)
                sb.Append("<div class=\"dn-alert warn\" style=\"margin:10px;\">สร้างรูป QR ฝั่งเซิร์ฟเวอร์ไม่ได้ (ไม่พบ zxing.dll" + (string.IsNullOrEmpty(DiningQr.LastError) ? "" : " — " + H(DiningQr.LastError)) + ") — หน้าพิมพ์จะใช้ตัวสร้างในเบราว์เซอร์แทน</div>");
            sb.Append("<table class=\"dn-t\"><thead><tr><th>QR</th><th>รหัส</th><th>โซน</th><th>ชื่อ</th><th class=\"num\">ที่นั่ง</th><th>สถานะ</th><th>ลิงก์สั่งอาหาร</th><th>จัดการ</th></tr></thead><tbody>");
            int i = 0;
            foreach (var t in tables)
            {
                i++;
                string url = DiningQr.BuildOrderUrl(t.QrToken);
                string uid = "dnUrl" + Inv(i);
                sb.Append("<tr").Append(t.IsActive ? ">" : " class=\"off\">");
                sb.Append("<td>");
                string dataUri = engine ? DiningQr.RenderSvgDataUri(url, 144) : null;
                if (dataUri != null) sb.Append("<img class=\"dn-qr-mini\" alt=\"QR ").Append(H(t.Code)).Append("\" src=\"").Append(dataUri).Append("\" />");
                else sb.Append("<span class=\"mini\">-</span>");
                sb.Append("</td>");
                sb.Append("<td><b style=\"font-size:15px;\">").Append(H(t.Code)).Append("</b><div class=\"mini\">ลำดับ ").Append(Inv(t.SortOrder)).Append("</div></td>");
                sb.Append("<td>").Append(H(t.ZoneName)).Append("<div class=\"mini\">").Append(H(t.ZoneCode)).Append("</div></td>");
                sb.Append("<td>").Append(H(t.NameTh ?? "")).Append(string.IsNullOrEmpty(t.Note) ? "" : "<div class=\"mini\">" + H(t.Note) + "</div>").Append("</td>");
                sb.Append("<td class=\"num\">").Append(Inv(t.Seats)).Append("</td>");
                sb.Append("<td>").Append(TableStatusChip(t))
                  .Append("<div class=\"mini\">QR รุ่น ").Append(Inv(t.QrVersion)).Append(" · ออก ").Append(t.QrIssuedAt == DateTime.MinValue ? "-" : t.QrIssuedAt.ToString("dd/MM/yy HH:mm", CultureInfo.InvariantCulture)).Append("</div></td>");
                sb.Append("<td><div class=\"dn-url\"><input type=\"text\" readonly id=\"").Append(uid).Append("\" value=\"").Append(H(url)).Append("\" />")
                  .Append("<button type=\"button\" class=\"dn-btn sm sec\" onclick=\"dnCopy('").Append(uid).Append("', this)\" title=\"คัดลอกลิงก์\">คัดลอก</button></div></td>");
                sb.Append("<td><div class=\"dn-actions\">");
                sb.Append(Btn("editTable", t.Id, "แก้ไข", "sec", null));
                sb.Append("<a class=\"dn-btn sm sec\" href=\"").Append(ResolveUrl("~/Admin/Dining/QrSheet")).Append("?table=").Append(Inv(t.Id)).Append("\">พิมพ์</a>");
                sb.Append(Btn("rotateTable", t.Id, "หมุน QR", "danger",
                    "หมุน QR โต๊ะ " + t.Code + "?\n\nQR ที่พิมพ์ติดโต๊ะอยู่จะใช้ไม่ได้ทันที ต้องพิมพ์ใหม่ (ลูกค้าที่นั่งสั่งอยู่ไม่กระทบ)"));
                sb.Append(Btn("toggleTable", t.Id, t.IsActive ? "ปิดใช้" : "เปิดใช้", t.IsActive ? "danger" : "sec", null));
                sb.Append(Btn("deleteTable", t.Id, "ลบ", "danger", "ลบโต๊ะ " + t.Code + "? (โต๊ะที่มีประวัติการสั่งจะถูกปิดใช้แทน)"));
                sb.Append("</div></td></tr>");
            }
            sb.Append("</tbody></table>");
            litTables.Text = sb.ToString();
        }

        private static string TableStatusChip(DiningTable t)
        {
            if (!t.IsActive) return Chip("ปิดใช้งาน", "off");
            switch (t.Status)
            {
                case DiningStatus.Table.Occupied: return Chip("มีลูกค้า", "info");
                case DiningStatus.Table.BillRequested: return Chip("ขอเช็คบิล", "warn");
                case DiningStatus.Table.NeedsStaff: return Chip("เรียกพนักงาน", "err");
                case DiningStatus.Table.Disabled: return Chip("ปิดใช้งาน", "off");
                default: return Chip("ว่าง", "ok");
            }
        }

        private static string Chip(string text, string cls)
        {
            return "<span class=\"dn-chip " + cls + "\">" + H(text) + "</span>";
        }

        /// <summary>ปุ่มเรียก dnAct — ข้อความ confirm ถูก encode ลง attribute (JS string ใน onclick)</summary>
        private static string Btn(string act, int id, string label, string cls, string confirm)
        {
            string js = "dnAct('" + act + "', " + Inv(id) + ", '" + JsStr(confirm) + "')";
            return "<button type=\"button\" class=\"dn-btn sm " + cls + "\" onclick=\"" + H(js) + "\">" + H(label) + "</button>";
        }

        private static string JsStr(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", "\\n").Replace("<", "\\x3c").Replace(">", "\\x3e");
        }

        // ── เช็กลิสต์ + ลิงก์ ───────────────────────────────────────────────────

        private void RenderChecklist(bool ready)
        {
            var items = new List<string>();
            string zonesUrl = ResolveUrl("~/Admin/Dining/Zones") + "#t-zones";

            items.Add(Ck(ready ? "ok" : "err", "รัน migration PHASE19_42 แล้ว (ตาราง Dining_*)",
                ready ? "ตารางครบ" : "รัน Database/PHASE19_Migration_42_Dining_QR_Ordering.sql แล้ว recycle App Pool", null, null));

            bool featureOn = Feature.On(DiningConfig.FeatureName);
            items.Add(Ck(featureOn ? "ok" : "warn", "เปิดสวิตช์ \"เปิดใช้ QR สั่งอาหาร\"", featureOn ? "เปิดอยู่" : "ยังปิด — ทำข้ออื่นให้ครบก่อนแล้วติ๊กในการ์ดค่าตั้งด้านล่าง", null, null));

            int zones = 0, tables = 0, menuItems = 0;
            if (ready)
            {
                zones = _tables.ListZones(false).Count;
                tables = _tables.ListTables(null, false).Count;
                foreach (var it in _menu.ListProductsForAdmin(true)) if (it.IsDining) menuItems++;
            }
            items.Add(Ck(zones > 0 ? "ok" : "warn", "มีโซนที่ใช้งานอย่างน้อย 1 โซน", zones > 0 ? zones + " โซน" : "ยังไม่มี", zonesUrl, "เพิ่มโซน"));
            items.Add(Ck(tables > 0 ? "ok" : "warn", "มีโต๊ะที่ใช้งานอย่างน้อย 1 โต๊ะ", tables > 0 ? tables + " โต๊ะ" : "ยังไม่มี — สร้างหลายโต๊ะได้ทีเดียว", zonesUrl, "สร้างโต๊ะ"));
            items.Add(Ck(menuItems > 0 ? "ok" : "warn", "มีรายการในเมนู QR อย่างน้อย 1 รายการ", menuItems > 0 ? menuItems + " รายการ" : "เลือกสินค้าจากคลังเข้าเมนู", ResolveUrl("~/Admin/Dining/Menu"), "จัดการเมนู"));

            bool slip = false;
            try { slip = SlipVerificationService.IsEnabled; } catch { }
            items.Add(Ck(slip ? "ok" : "warn", "ตรวจสลิปอัตโนมัติ (SlipOK/OCR) เปิดอยู่",
                slip ? "เปิดอยู่ — โอนแล้วยอดตรง = ชำระทันที" : "ปิดอยู่ — สลิปทุกใบจะรอพนักงานตรวจบนกระดาน", ResolveUrl("~/Admin/Settings/SlipCheckSettings"), "ตั้งค่าตรวจสลิป"));

            int receivers = 0;
            try { receivers = SlipVerificationService.LoadReceiverAccounts().Count; } catch { }
            items.Add(Ck(receivers > 0 ? "ok" : "warn", "ตั้งบัญชีรับโอน (เลขบัญชี/พร้อมเพย์) ของแหล่งเงินแล้ว",
                receivers > 0 ? receivers + " บัญชี — ลูกค้าเห็นตอนเลือกโอน + ใช้ตรวจผู้รับบนสลิป" : "ยังไม่มี — ลูกค้าจะไม่เห็นบัญชีให้โอน", ResolveUrl("~/Admin/Settings/PaymentChannels"), "ช่องทางชำระเงิน"));

            bool engine = DiningQr.EngineReady();
            items.Add(Ck(engine ? "ok" : "warn", "สร้างรูป QR ฝั่งเซิร์ฟเวอร์ได้ (zxing.dll)",
                engine ? "พร้อม" : "ไม่พบ zxing.dll — หน้าพิมพ์ใช้ตัวสร้างในเบราว์เซอร์แทน (ต้องต่ออินเทอร์เน็ตตอนพิมพ์)", null, null));

            bool policy = false;
            try { policy = SalesDocumentPolicy.Enabled(Conn); } catch { }
            items.Add(Ck(policy ? "ok" : "warn", "นโยบายเอกสารขาย (ช่องทาง DINING) เปิดใช้",
                policy ? "เปิดอยู่ — ไม่มีกฎ DINING = เงินสดสรุปรายวัน (DNDAY-…), โอนออกเอกสารรายใบ (DNP-…)" : "ปิดอยู่ — ยอดขายร้านอาหารยังไม่ลง NextAcc", ResolveUrl("~/Admin/Settings/SalesDocPolicy"), "นโยบายเอกสารขาย"));

            var sb = new StringBuilder("<ul class=\"dn-check\">");
            foreach (string s in items) sb.Append(s);
            sb.Append("</ul>");
            litChecklist.Text = sb.ToString();
        }

        private static string Ck(string cls, string title, string detail, string url, string linkText)
        {
            string ico = cls == "ok" ? "✓" : cls == "err" ? "!" : "•";
            var sb = new StringBuilder();
            sb.Append("<li class=\"").Append(cls).Append("\"><span class=\"ck\">").Append(ico).Append("</span><div><b>").Append(H(title)).Append("</b><div class=\"mini\">").Append(H(detail)).Append("</div></div>");
            if (!string.IsNullOrEmpty(url)) sb.Append("<a class=\"dn-btn sm sec go\" href=\"").Append(H(url)).Append("\">").Append(H(linkText ?? "ไปตั้งค่า")).Append("</a>");
            sb.Append("</li>");
            return sb.ToString();
        }

        private void RenderLinks()
        {
            var sb = new StringBuilder("<div class=\"dn-links\">");
            sb.Append(Link(ResolveUrl("~/Admin/Dining/Menu"), "เมนู QR", "หมวด / รายการจากคลังสินค้า / sold-out / ตัวเลือก (เผ็ด-ขนาด) / โซนที่ขาย"));
            sb.Append(Link(ResolveUrl("~/Admin/Dining/QrSheet"), "พิมพ์ QR ติดโต๊ะ", "แผ่น A4 2×4 / 3×6 หรือป้ายตั้งโต๊ะ 10×15 ซม. — QR สร้างในเครื่อง ไม่ส่งลิงก์ออกนอก"));
            sb.Append(Link(ResolveUrl("~/Admin/Dining/Tables"), "สถานะโต๊ะสด", "โต๊ะว่าง/มีลูกค้า/ขอเช็คบิล/เรียกพนักงาน + ปิดโต๊ะ (หน้าพนักงาน)"));
            sb.Append(Link(ResolveUrl("~/Admin/Dining/Orders"), "กระดานออเดอร์", "รับ/ทำ/เสิร์ฟ, รับเงินสด, ตรวจสลิปรอตรวจ (หน้าพนักงาน)"));
            sb.Append(Link(ResolveUrl("~/Admin/Settings/SalesDocPolicy"), "นโยบายเอกสารขาย — ช่องทาง DINING", "ไม่ตั้งกฎ DINING = ใช้ค่ากลาง: เงินสดสรุปรายวัน DNDAY-… / โอนออกเอกสารรายใบ DNP-…"));
            sb.Append(Link(ResolveUrl("~/Admin/Settings/SlipCheckSettings"), "ตรวจสลิป (SlipOK / OCR)", "ลูกค้าโอนแล้วระบบตรวจเอง ผ่าน = ชำระทันที; ไม่ผ่านตาม SlipVerify_Customer_Fail_Mode"));
            sb.Append(Link(ResolveUrl("~/Admin/Settings/PaymentChannels"), "ช่องทางชำระเงิน / บัญชีรับโอน", "แหล่งเงินที่ลูกค้าเห็นตอนเลือกโอน (Customer_Visible) + เลขบัญชี/พร้อมเพย์สำหรับตรวจผู้รับ"));
            sb.Append(Link(ResolveUrl("~/Admin/Notifications/Settings"), "แจ้งเตือน Telegram / LINE", "หมวด \"ร้านอาหาร (QR สั่งอาหาร)\": ออเดอร์ใหม่ · สถานะ/การชำระ · เรียกพนักงาน · เช็คบิล/ลงบิลห้อง/ปิดโต๊ะ"));
            sb.Append(Link(ResolveUrl("~/Admin/Settings/AccountingIntegration") + "?focus=mapping&key=DINING_REVENUE", "ผังบัญชี DINING_REVENUE", "ไม่ผูก = ใช้รายได้ขายสินค้า (PRODUCT_REVENUE) · รายการ Track_Stock ตัดสต๊อก+COGS ตอนจ่ายสำเร็จ"));
            sb.Append(Link(ResolveUrl("~/Admin/ProductImages"), "รูปสินค้า", "รูปในเมนู QR ใช้รูปหลักของสินค้า (Product_Images ชนิด PRODUCT)"));
            sb.Append(Link(ResolveUrl("~/Admin/Settings/PermissionGroups"), "สิทธิ์ OPS_DINING", "กลุ่มที่เห็นกระดานออเดอร์/สถานะโต๊ะ — หน้าตั้งค่านี้ใช้สิทธิ์ \"ตั้งค่าบริการในที่พัก\""));
            sb.Append("</div>");
            litLinks.Text = sb.ToString();
        }

        private static string Link(string url, string title, string desc)
        {
            return "<a class=\"dn-link\" href=\"" + H(url) + "\"><div class=\"t\">" + H(title) + "</div><div class=\"d\">" + H(desc) + "</div></a>";
        }

        // ── ค่าตั้ง ─────────────────────────────────────────────────────────────

        private void LoadSettings()
        {
            chkFeature.Checked = Feature.On(DiningConfig.FeatureName);
            txtIdle.Text = Inv(DiningConfig.IdleMinutes);
            txtMinOrder.Text = DiningConfig.MinOrder.ToString("0.##", CultureInfo.InvariantCulture);
            txtGuestPoll.Text = Inv(DiningConfig.GuestPollSec);
            txtStaffPoll.Text = Inv(DiningConfig.StaffPollSec);
            txtMaxFail.Text = Inv(DiningConfig.RoomChargeMaxFail);
        }

        protected void btnSaveSettings_Click(object sender, EventArgs e)
        {
            hfTab.Value = "settings";
            try
            {
                int idle = ParseInt(txtIdle.Text, 30, 5, 1440), gp = ParseInt(txtGuestPoll.Text, 5, 2, 60), sp = ParseInt(txtStaffPoll.Text, 3, 2, 60), mf = ParseInt(txtMaxFail.Text, 5, 1, 50);
                decimal min;
                if (!decimal.TryParse((txtMinOrder.Text ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out min) || min < 0m) min = 0m;
                short? by = AdminId();
                AppCfg.Set("Dining_Session_Idle_Minutes", Inv(idle), by);
                AppCfg.Set("Dining_Min_Order", min.ToString("0.##", CultureInfo.InvariantCulture), by);
                AppCfg.Set("Dining_Guest_Poll_Sec", Inv(gp), by);
                AppCfg.Set("Dining_Staff_Poll_Sec", Inv(sp), by);
                AppCfg.Set("Dining_Room_Charge_Max_Fail", Inv(mf), by);
                AppCfg.Set(FeatureKey, chkFeature.Checked ? "1" : "0", by);
                new code().Logs(Conn, "Dining", "ตั้งค่า QR สั่งอาหาร: feature=" + (chkFeature.Checked ? "on" : "off") + " idle=" + idle + " min=" + min + " poll=" + gp + "/" + sp + " maxFail=" + mf, StaffName());
                Msg("ok", "บันทึกค่าตั้งแล้ว" + (chkFeature.Checked ? " — เปิดใช้ QR สั่งอาหารแล้ว (เมนูพนักงานขึ้นภายใน 30 วินาที)" : " — ฟีเจอร์ปิดอยู่"));
                LoadSettings();
            }
            catch (Exception ex) { Msg("err", "บันทึกไม่สำเร็จ: " + ex.Message); }
        }

        protected void btnTestTelegram_Click(object sender, EventArgs e)
        {
            hfTab.Value = "settings";
            string r;
            try { r = global::Notify.TestChannel(global::Notify.ChannelTelegram); }
            catch (Exception ex) { r = "❌ " + ex.Message; }
            litTestResult.Text = "<span class=\"dn-chip info\" style=\"white-space:normal;\">" + H(r) + "</span>";
        }

        private static int ParseInt(string s, int def, int min, int max)
        {
            int v;
            if (!int.TryParse((s ?? "").Trim(), out v)) v = def;
            return v < min ? min : v > max ? max : v;
        }

        // ── โซน: ฟอร์ม ──────────────────────────────────────────────────────────

        private void ShowZoneForm(DiningZone z)
        {
            HideForms();
            pnlZoneForm.Visible = true;
            bool isNew = z == null;
            if (isNew) z = new DiningZone { SortOrder = _tables.ListZones(true).Count + 1 };
            litZoneFormTitle.Text = isNew ? "เพิ่มโซน" : "แก้ไขโซน " + H(z.Code);
            hfZoneId.Value = Inv(z.Id);
            txtZoneCode.Text = z.Code ?? "";
            txtZoneName.Text = z.NameTh ?? "";
            txtZoneSort.Text = Inv(z.SortOrder);
            txtZoneKitchen.Text = z.KitchenStation ?? "";
            ddlZoneMode.SelectedValue = ddlZoneMode.Items.FindByValue(z.ManualMode ?? "AUTO") != null ? z.ManualMode : "AUTO";
            txtZoneOpen.Text = z.OpenTime.HasValue ? z.OpenTime.Value.ToString(@"hh\:mm") : "";
            txtZoneClose.Text = z.CloseTime.HasValue ? z.CloseTime.Value.ToString(@"hh\:mm") : "";
            txtZoneClosedMsg.Text = z.ClosedMessage ?? "";
            chkZoneCash.Checked = z.AllowCash;
            chkZoneTransfer.Checked = z.AllowTransfer;
            chkZoneRoomCharge.Checked = z.AllowRoomCharge;
            chkZoneActive.Checked = z.IsActive;
        }

        protected void btnSaveZone_Click(object sender, EventArgs e)
        {
            try
            {
                int id; int.TryParse(hfZoneId.Value, out id);
                var z = new DiningZone
                {
                    Id = id,
                    Code = txtZoneCode.Text,
                    NameTh = (txtZoneName.Text ?? "").Trim(),
                    SortOrder = ParseInt(txtZoneSort.Text, 0, -9999, 9999),
                    KitchenStation = (txtZoneKitchen.Text ?? "").Trim(),
                    ManualMode = ddlZoneMode.SelectedValue,
                    OpenTime = ParseTime(txtZoneOpen.Text),
                    CloseTime = ParseTime(txtZoneClose.Text),
                    ClosedMessage = (txtZoneClosedMsg.Text ?? "").Trim(),
                    AllowCash = chkZoneCash.Checked,
                    AllowTransfer = chkZoneTransfer.Checked,
                    AllowRoomCharge = chkZoneRoomCharge.Checked,
                    IsActive = chkZoneActive.Checked
                };
                if ((z.OpenTime.HasValue) != (z.CloseTime.HasValue)) throw new ArgumentException("กรุณาตั้งทั้งเวลาเปิดและเวลาปิด (หรือเว้นว่างทั้งคู่ = เปิดตลอด)");
                _tables.SaveZone(z, StaffName());
                pnlZoneForm.Visible = false;
                BindZoneDropdowns();
                Msg("ok", (id > 0 ? "แก้ไขโซน " : "เพิ่มโซน ") + z.Code + " แล้ว");
            }
            catch (ArgumentException ex) { pnlZoneForm.Visible = true; Msg("err", ex.Message); }
            catch (Exception ex) { pnlZoneForm.Visible = true; Msg("err", "บันทึกไม่สำเร็จ: " + ex.Message); }
        }

        private static TimeSpan? ParseTime(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return null;
            TimeSpan t;
            if (TimeSpan.TryParseExact(s, new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" }, CultureInfo.InvariantCulture, out t)) return t;
            if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out t) && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1)) return t;
            throw new ArgumentException("รูปแบบเวลาไม่ถูกต้อง (HH:mm): " + s);
        }

        // ── โต๊ะ: ฟอร์ม ─────────────────────────────────────────────────────────

        private void ShowTableForm(DiningTable t)
        {
            HideForms();
            pnlTableForm.Visible = true;
            bool isNew = t == null;
            if (isNew)
            {
                t = new DiningTable { ZoneId = SelectedZoneFilter(), Seats = 4 };
                t.SortOrder = _tables.ListTables(t.ZoneId > 0 ? (int?)t.ZoneId : null, true).Count + 1;
            }
            litTableFormTitle.Text = isNew ? "เพิ่มโต๊ะ" : "แก้ไขโต๊ะ " + H(t.Code);
            hfTableId.Value = Inv(t.Id);
            if (t.ZoneId > 0 && ddlTableZone.Items.FindByValue(Inv(t.ZoneId)) != null) ddlTableZone.SelectedValue = Inv(t.ZoneId);
            txtTableCode.Text = t.Code ?? "";
            txtTableName.Text = t.NameTh ?? "";
            txtTableSeats.Text = Inv(t.Seats);
            txtTableSort.Text = Inv(t.SortOrder);
            txtTableNote.Text = t.Note ?? "";
            chkTableActive.Checked = t.IsActive;
        }

        protected void btnSaveTable_Click(object sender, EventArgs e)
        {
            try
            {
                int id; int.TryParse(hfTableId.Value, out id);
                int zone; int.TryParse(ddlTableZone.SelectedValue, out zone);
                var t = new DiningTable
                {
                    Id = id, ZoneId = zone,
                    Code = (txtTableCode.Text ?? "").Trim(),
                    NameTh = (txtTableName.Text ?? "").Trim(),
                    Seats = ParseInt(txtTableSeats.Text, 4, 1, 255),
                    SortOrder = ParseInt(txtTableSort.Text, 0, -9999, 9999),
                    Note = (txtTableNote.Text ?? "").Trim(),
                    IsActive = chkTableActive.Checked
                };
                _tables.SaveTable(t);
                pnlTableForm.Visible = false;
                Msg("ok", (id > 0 ? "แก้ไขโต๊ะ " : "เพิ่มโต๊ะ ") + t.Code + " แล้ว" + (id > 0 ? "" : " — ออก QR ให้แล้ว กด \"พิมพ์\" เพื่อพิมพ์ติดโต๊ะ"));
            }
            catch (ArgumentException ex) { pnlTableForm.Visible = true; Msg("err", ex.Message); }
            catch (Exception ex) { pnlTableForm.Visible = true; Msg("err", "บันทึกไม่สำเร็จ: " + ex.Message); }
        }

        protected void btnBulk_Click(object sender, EventArgs e)
        {
            try
            {
                int zone; int.TryParse(ddlBulkZone.SelectedValue, out zone);
                int from = ParseInt(txtBulkFrom.Text, 1, 0, 9999), to = ParseInt(txtBulkTo.Text, 10, 0, 9999), seats = ParseInt(txtBulkSeats.Text, 4, 1, 255);
                string prefix = (txtBulkPrefix.Text ?? "").Trim();
                int made = _tables.BulkCreateTables(zone, prefix, from, to, seats);
                pnlBulk.Visible = false;
                if (ddlZoneFilter.Items.FindByValue(Inv(zone)) != null) ddlZoneFilter.SelectedValue = Inv(zone);
                Msg(made > 0 ? "ok" : "warn", "สร้างโต๊ะ " + prefix + from + "–" + prefix + to + " = " + made + " โต๊ะ" + (made == 0 ? " (รหัสทั้งหมดมีอยู่แล้ว)" : " — ทุกโต๊ะมี QR แล้ว กด \"พิมพ์ QR\" ของโซน"));
            }
            catch (ArgumentException ex) { pnlBulk.Visible = true; Msg("err", ex.Message); }
            catch (Exception ex) { pnlBulk.Visible = true; Msg("err", "สร้างไม่สำเร็จ: " + ex.Message); }
        }

        protected void btnCancelForm_Click(object sender, EventArgs e) { HideForms(); }

        protected void ddlZoneFilter_Changed(object sender, EventArgs e) { HideForms(); }

        // ── ปุ่มในตาราง (dnAct) ─────────────────────────────────────────────────

        protected void btnAct_Click(object sender, EventArgs e)
        {
            string act = (hfAct.Value ?? "").Trim();
            int id; int.TryParse(hfArg.Value, out id);
            hfAct.Value = ""; hfArg.Value = "";
            try
            {
                switch (act)
                {
                    case "newZone": ShowZoneForm(null); break;
                    case "editZone":
                        {
                            var z = _tables.GetZone(id);
                            if (z == null) Msg("err", "ไม่พบโซน"); else ShowZoneForm(z);
                            break;
                        }
                    case "toggleZone":
                        {
                            var z = _tables.GetZone(id);
                            if (z == null) { Msg("err", "ไม่พบโซน"); break; }
                            _tables.SetZoneActive(id, !z.IsActive);
                            DiningMenuService.Invalidate();
                            BindZoneDropdowns();
                            new code().Logs(Conn, "Dining", (z.IsActive ? "ปิดใช้โซน " : "เปิดใช้โซน ") + z.Code, StaffName());
                            Msg("ok", (z.IsActive ? "ปิดใช้งานโซน " : "เปิดใช้งานโซน ") + z.Code + " แล้ว");
                            break;
                        }
                    case "rotateZone":
                        {
                            var z = _tables.GetZone(id);
                            if (z == null) { Msg("err", "ไม่พบโซน"); break; }
                            int n = _tables.RotateZoneTokens(id);
                            Msg("ok", "หมุน QR โซน " + z.Code + " แล้ว " + n + " โต๊ะ — QR เดิมใช้ไม่ได้แล้ว กด \"พิมพ์ QR\" เพื่อพิมพ์ชุดใหม่");
                            break;
                        }
                    case "newTable": ShowTableForm(null); break;
                    case "editTable":
                        {
                            var t = _tables.GetTable(id);
                            if (t == null) Msg("err", "ไม่พบโต๊ะ"); else ShowTableForm(t);
                            break;
                        }
                    case "toggleTable":
                        {
                            var t = _tables.GetTable(id);
                            if (t == null) { Msg("err", "ไม่พบโต๊ะ"); break; }
                            _tables.SetTableActive(id, !t.IsActive);
                            Msg("ok", (t.IsActive ? "ปิดใช้งานโต๊ะ " : "เปิดใช้งานโต๊ะ ") + t.Code + " แล้ว");
                            break;
                        }
                    case "rotateTable":
                        {
                            var t = _tables.GetTable(id);
                            if (t == null) { Msg("err", "ไม่พบโต๊ะ"); break; }
                            _tables.RotateTableToken(id);
                            Msg("ok", "หมุน QR โต๊ะ " + t.Code + " แล้ว (รุ่นที่ " + (t.QrVersion + 1) + ") — QR เดิมใช้ไม่ได้แล้ว กด \"พิมพ์\" เพื่อพิมพ์ใหม่");
                            break;
                        }
                    case "deleteTable":
                        {
                            var t = _tables.GetTable(id);
                            if (t == null) { Msg("err", "ไม่พบโต๊ะ"); break; }
                            var r = _tables.DeleteTable(id);
                            Msg(r.Ok ? "ok" : "warn", "โต๊ะ " + t.Code + ": " + r.Message);
                            break;
                        }
                    case "bulk":
                        HideForms();
                        pnlBulk.Visible = true;
                        if (SelectedZoneFilter() > 0 && ddlBulkZone.Items.FindByValue(ddlZoneFilter.SelectedValue) != null) ddlBulkZone.SelectedValue = ddlZoneFilter.SelectedValue;
                        break;
                    case "recheck":
                        DiningTableService.InvalidateSchemaCache();
                        AppCfg.Invalidate();
                        DiningMenuService.Invalidate();
                        BindZoneDropdowns();
                        LoadSettings();
                        hfTab.Value = "settings";
                        Msg("info", "ตรวจสอบใหม่แล้ว");
                        break;
                    default: break;
                }
            }
            catch (ArgumentException ex) { Msg("err", ex.Message); }
            catch (Exception ex) { Msg("err", "ผิดพลาด: " + ex.Message); }
        }
    }
}
