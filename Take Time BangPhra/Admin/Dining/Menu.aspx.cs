using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Dining;

namespace Take_Time_BangPhra.Admin.Dining
{
    /// <summary>
    /// QR สั่งอาหาร — จัดการเมนู: หมวด / รายการ (เลือกจาก Product, ชื่อแสดง, sold-out, โซน, Track_Stock, ตัวเลือก) / กลุ่มตัวเลือก + ตัวเลือก
    /// ใช้ <see cref="DiningMenuService"/> ตาม Class/Dining/README_API.md (service ล้าง cache ให้เองหลังบันทึก)
    /// สิทธิ์ Perm.SvcGuest — ไม่ guard ฟีเจอร์ (ตั้งเมนูได้ก่อนเปิดสวิตช์) · ทุกข้อความลง HTML ผ่าน H()
    /// </summary>
    public partial class MenuPage : Page
    {
        private static readonly string Conn = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private readonly DiningMenuService _menu = new DiningMenuService(Conn);
        private readonly DiningTableService _tables = new DiningTableService(Conn);

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
                BindLists();
                string show = Request.QueryString["show"];
                if (!string.IsNullOrEmpty(show) && ddlShow.Items.FindByValue(show.ToUpperInvariant()) != null) ddlShow.SelectedValue = show.ToUpperInvariant();
                int pid;
                if (int.TryParse(Request.QueryString["pid"], out pid) && pid > 0) ShowItemForm(pid);
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
        private static string Money(decimal v) { return v.ToString("N2", CultureInfo.InvariantCulture); }
        private void Msg(string cls, string text) { litMsg.Text += "<div class=\"dn-alert " + cls + "\">" + H(text) + "</div>"; }
        private string StaffName() { return Session["username"]?.ToString() ?? "ADMIN"; }

        private static string Chip(string text, string cls) { return "<span class=\"dn-chip " + cls + "\">" + H(text) + "</span>"; }

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

        private static int ParseInt(string s, int def, int min, int max)
        {
            int v;
            if (!int.TryParse((s ?? "").Trim(), out v)) v = def;
            return v < min ? min : v > max ? max : v;
        }

        private string Img(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            if (url.StartsWith("~")) return ResolveUrl(url);
            return url;
        }

        private void HideForms()
        {
            pnlItem.Visible = false; pnlCat.Visible = false; pnlGroup.Visible = false; pnlOpt.Visible = false;
        }

        /// <summary>หมวด/โซน/กลุ่ม ลง dropdown + checkbox list (คงค่าที่เลือก)</summary>
        private void BindLists()
        {
            string keepCat = ddlItemCat.SelectedValue, keepFilter = ddlCatFilter.SelectedValue;
            var cats = _menu.ListCategories(true);
            ddlItemCat.Items.Clear();
            ddlItemCat.Items.Add(new ListItem("— ไม่มีหมวด (อื่น ๆ) —", "0"));
            ddlCatFilter.Items.Clear();
            ddlCatFilter.Items.Add(new ListItem("ทุกหมวด", ""));
            foreach (var c in cats)
            {
                string label = c.NameTh + (c.IsActive ? "" : " (ปิด)");
                ddlItemCat.Items.Add(new ListItem(label, Inv(c.Id)));
                ddlCatFilter.Items.Add(new ListItem(label, Inv(c.Id)));
            }
            ddlCatFilter.Items.Add(new ListItem("ไม่มีหมวด", "0"));
            if (!string.IsNullOrEmpty(keepCat) && ddlItemCat.Items.FindByValue(keepCat) != null) ddlItemCat.SelectedValue = keepCat;
            if (!string.IsNullOrEmpty(keepFilter) && ddlCatFilter.Items.FindByValue(keepFilter) != null) ddlCatFilter.SelectedValue = keepFilter;

            var zonesSel = SelectedValues(cblItemZones);
            cblItemZones.Items.Clear();
            foreach (var z in _tables.ListZones(true))
                cblItemZones.Items.Add(new ListItem(z.Code + " — " + z.NameTh + (z.IsActive ? "" : " (ปิด)"), z.Code) { Selected = zonesSel.Contains(z.Code) });

            var groupsSel = SelectedValues(cblItemGroups);
            cblItemGroups.Items.Clear();
            foreach (var g in _menu.ListOptionGroups(true))
                cblItemGroups.Items.Add(new ListItem(g.NameTh + " (" + g.Options.Count + " ตัวเลือก" + (g.IsRequired ? ", บังคับ" : "") + (g.IsActive ? "" : ", ปิด") + ")", Inv(g.Id)) { Selected = groupsSel.Contains(Inv(g.Id)) });
        }

        private static HashSet<string> SelectedValues(CheckBoxList cbl)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ListItem li in cbl.Items) if (li.Selected) set.Add(li.Value);
            return set;
        }

        // ── วาดทั้งหน้า ─────────────────────────────────────────────────────────

        private void RenderAll()
        {
            if (!_tables.SchemaReady())
            {
                litMsg.Text = "<div class=\"dn-alert err\">ยังไม่ได้รัน <b>Database/PHASE19_Migration_42_Dining_QR_Ordering.sql</b> — ตารางเมนูยังไม่มี</div>" + litMsg.Text;
                litItems.Text = ""; litCats.Text = ""; litGroups.Text = "";
                return;
            }
            RenderItems();
            RenderCats();
            RenderGroups();
        }

        private void RenderItems()
        {
            string q = (txtSearch.Text ?? "").Trim();
            string show = ddlShow.SelectedValue;
            string catF = ddlCatFilter.SelectedValue;
            var all = _menu.ListProductsForAdmin(show == "IN" || show == "SOLD");
            DateTime now = DateTime.Now;
            var rows = new List<DiningMenuItem>();
            int inMenu = 0;
            foreach (var it in all)
            {
                if (it.IsDining) inMenu++;
                if (show == "IN" && !it.IsDining) continue;
                if (show == "OUT" && it.IsDining) continue;
                if (show == "SOLD" && !(it.IsDining && it.SoldOutNow(now))) continue;
                if (catF == "0" && it.CategoryId.HasValue && it.CategoryId.Value > 0) continue;
                if (catF.Length > 0 && catF != "0" && Inv(it.CategoryId ?? 0) != catF) continue;
                if (q.Length > 0)
                {
                    bool hit = (it.Name ?? "").IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0
                        || (it.ProductName ?? "").IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0
                        || (it.NameEn ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                        || (it.Barcode ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!hit) continue;
                }
                rows.Add(it);
            }
            litItemCount.Text = "<span class=\"dn-chip info\">ในเมนู " + Inv(inMenu) + " · แสดง " + Inv(rows.Count) + "</span>";
            if (rows.Count == 0)
            {
                litItems.Text = "<div class=\"dn-alert info\" style=\"margin:10px;\">ไม่มีรายการ" + (show == "IN" ? " — เลือก \"สินค้าทั้งหมดในคลัง\" แล้วกด \"เพิ่มเข้าเมนู\"" : "") + "</div>";
                return;
            }
            var sb = new StringBuilder("<table class=\"dn-t\"><thead><tr><th>รูป</th><th>สินค้า</th><th class=\"num\">ราคา</th><th>หมวด</th><th>โซน</th><th>สถานะ</th><th>ตัวเลือก</th><th>จัดการ</th></tr></thead><tbody>");
            foreach (var it in rows)
            {
                bool sold = it.SoldOutNow(now);
                sb.Append("<tr").Append(it.IsDining && it.ProductActive ? ">" : " class=\"off\">");
                string img = Img(it.ThumbUrl ?? it.ImageUrl);
                sb.Append("<td>").Append(img.Length > 0 ? "<img class=\"dn-thumb\" src=\"" + H(img) + "\" alt=\"\" />" : "<span class=\"dn-thumb\"></span>").Append("</td>");
                sb.Append("<td><b>").Append(H(it.Name)).Append("</b>");
                if (it.Name != it.ProductName) sb.Append("<div class=\"mini\">สินค้า: ").Append(H(it.ProductName)).Append("</div>");
                if (!string.IsNullOrEmpty(it.NameEn)) sb.Append("<div class=\"mini\">").Append(H(it.NameEn)).Append("</div>");
                if (!string.IsNullOrEmpty(it.Barcode)) sb.Append("<div class=\"mini\">").Append(H(it.Barcode)).Append("</div>");
                sb.Append("</td>");
                sb.Append("<td class=\"num\">").Append(Money(it.Price)).Append("</td>");
                sb.Append("<td>").Append(it.IsDining ? H(string.IsNullOrEmpty(it.CategoryName) ? "อื่น ๆ" : it.CategoryName) : "<span class=\"mini\">-</span>").Append("</td>");
                sb.Append("<td>").Append(it.IsDining ? H(string.IsNullOrWhiteSpace(it.AvailableZones) ? "ทุกโซน" : it.AvailableZones) : "<span class=\"mini\">-</span>").Append("</td>");
                sb.Append("<td>");
                if (!it.ProductActive) sb.Append(Chip("สินค้าปิดใช้", "off")).Append(' ');
                if (!it.IsDining) sb.Append(Chip("ไม่อยู่ในเมนู", "off"));
                else
                {
                    sb.Append(sold ? Chip(it.SoldOutUntil.HasValue ? "หมดถึง " + it.SoldOutUntil.Value.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture) : "หมด", "err") : Chip("ขายอยู่", "ok"));
                    if (it.IsRecommended) sb.Append(' ').Append(Chip("แนะนำ", "warn"));
                    if (it.TrackStock) sb.Append(' ').Append(Chip("สต๊อก " + (it.StockQty.HasValue ? it.StockQty.Value.ToString("0.##", CultureInfo.InvariantCulture) : "?"), it.StockQty.HasValue && it.StockQty.Value <= 0m ? "err" : "info"));
                    if (it.PrepMinutes.HasValue) sb.Append("<div class=\"mini\">").Append(Inv(it.PrepMinutes.Value)).Append(" นาที</div>");
                    if (!string.IsNullOrEmpty(it.AllergenTags)) sb.Append("<div class=\"mini\">").Append(H(it.AllergenTags)).Append("</div>");
                }
                sb.Append("</td>");
                sb.Append("<td>");
                if (it.OptionGroups.Count == 0) sb.Append("<span class=\"mini\">-</span>");
                else
                {
                    var names = new List<string>();
                    foreach (var g in it.OptionGroups) names.Add(g.NameTh);
                    sb.Append(H(string.Join(", ", names.ToArray())));
                }
                sb.Append("</td>");
                sb.Append("<td><div class=\"dn-actions\">");
                if (!it.IsDining) sb.Append(Btn("addItem", it.ProductId, "เพิ่มเข้าเมนู", "", null));
                else
                {
                    sb.Append(Btn("editItem", it.ProductId, "แก้ไข", "sec", null));
                    if (sold) sb.Append(Btn("available", it.ProductId, "มีแล้ว", "", null));
                    else
                    {
                        sb.Append(Btn("soldToday", it.ProductId, "หมดวันนี้", "danger", null));
                        sb.Append(Btn("soldOut", it.ProductId, "หมด", "danger", null));
                    }
                    sb.Append(Btn("removeItem", it.ProductId, "เอาออก", "danger", "เอา \"" + it.Name + "\" ออกจากเมนู QR? (สินค้าในคลังไม่หาย)"));
                }
                sb.Append("</div></td></tr>");
            }
            sb.Append("</tbody></table>");
            litItems.Text = sb.ToString();
        }

        private void RenderCats()
        {
            var cats = _menu.ListCategories(true);
            if (cats.Count == 0) { litCats.Text = "<div class=\"dn-alert info\" style=\"margin:10px;\">ยังไม่มีหมวด — กด \"เพิ่มหมวด\" (เช่น อาหารจานเดียว / กับข้าว / เครื่องดื่ม / ของหวาน)</div>"; return; }
            var counts = new Dictionary<int, int>();
            foreach (var it in _menu.ListProductsForAdmin(true))
            {
                if (!it.IsDining) continue;
                int cid = it.CategoryId ?? 0; int n; counts.TryGetValue(cid, out n); counts[cid] = n + 1;
            }
            var sb = new StringBuilder("<table class=\"dn-t\" style=\"min-width:560px;\"><thead><tr><th>ไอคอน</th><th>ชื่อหมวด</th><th>English</th><th class=\"num\">ลำดับ</th><th class=\"num\">รายการ</th><th>สถานะ</th><th>จัดการ</th></tr></thead><tbody>");
            foreach (var c in cats)
            {
                int n; counts.TryGetValue(c.Id, out n);
                sb.Append("<tr").Append(c.IsActive ? ">" : " class=\"off\">");
                sb.Append("<td>").Append(IconHtml(c.Icon)).Append("</td>");
                sb.Append("<td><b>").Append(H(c.NameTh)).Append("</b></td><td>").Append(H(c.NameEn ?? "")).Append("</td>");
                sb.Append("<td class=\"num\">").Append(Inv(c.SortOrder)).Append("</td><td class=\"num\">").Append(Inv(n)).Append("</td>");
                sb.Append("<td>").Append(c.IsActive ? Chip("ใช้งาน", "ok") : Chip("ปิด", "off")).Append("</td>");
                sb.Append("<td><div class=\"dn-actions\">").Append(Btn("editCat", c.Id, "แก้ไข", "sec", null))
                  .Append(Btn("deleteCat", c.Id, "ลบ", "danger", "ลบหมวด \"" + c.NameTh + "\"? รายการ " + n + " รายการในหมวดจะกลายเป็น \"อื่น ๆ\"")).Append("</div></td></tr>");
            }
            int none; counts.TryGetValue(0, out none);
            if (none > 0) sb.Append("<tr><td></td><td><i>อื่น ๆ (ไม่มีหมวด)</i></td><td></td><td></td><td class=\"num\">").Append(Inv(none)).Append("</td><td></td><td></td></tr>");
            sb.Append("</tbody></table>");
            litCats.Text = sb.ToString();
        }

        private static string IconHtml(string icon)
        {
            icon = (icon ?? "").Trim();
            if (icon.Length == 0) return "<span class=\"mini\">-</span>";
            if (icon.StartsWith("fa-", StringComparison.OrdinalIgnoreCase))
            {
                foreach (char ch in icon) if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == ' ')) return H(icon);
                return "<i class=\"fas " + H(icon) + "\"></i>";
            }
            return "<span style=\"font-size:18px;\">" + H(icon) + "</span>";
        }

        private void RenderGroups()
        {
            var groups = _menu.ListOptionGroups(true);
            if (groups.Count == 0) { litGroups.Text = "<div class=\"dn-alert info\">ยังไม่มีกลุ่มตัวเลือก — กด \"เพิ่มกลุ่ม\" (เช่น ระดับความเผ็ด: ไม่เผ็ด/กลาง/มาก)</div>"; return; }
            var sb = new StringBuilder();
            foreach (var g in groups)
            {
                sb.Append("<div class=\"mn-group\"><h4>").Append(H(g.NameTh)).Append(' ')
                  .Append(g.IsActive ? "" : Chip("ปิด", "off"))
                  .Append(Chip((g.IsRequired ? "บังคับ · " : "ไม่บังคับ · ") + "เลือก " + Inv(g.MinSelect) + "–" + (g.MaxSelect > 0 ? Inv(g.MaxSelect) : "ไม่จำกัด"), "info"))
                  .Append("<span class=\"mini\">ลำดับ ").Append(Inv(g.SortOrder)).Append("</span>");
                sb.Append("<span class=\"dn-right\">").Append(Btn("newOpt", g.Id, "+ ตัวเลือก", "", null)).Append(Btn("editGroup", g.Id, "แก้ไขกลุ่ม", "sec", null))
                  .Append(Btn("deleteGroup", g.Id, "ลบกลุ่ม", "danger", "ลบกลุ่ม \"" + g.NameTh + "\" พร้อมตัวเลือก " + g.Options.Count + " รายการ และการผูกกับเมนูทั้งหมด?")).Append("</span></h4>");
                if (g.Options.Count == 0) sb.Append("<div class=\"mini\">ยังไม่มีตัวเลือก</div>");
                foreach (var o in g.Options)
                {
                    sb.Append("<div class=\"mn-opt").Append(o.IsActive ? "" : " off").Append("\"><span>").Append(H(o.NameTh)).Append("</span>")
                      .Append("<span class=\"pr\">").Append(o.PriceDelta == 0m ? "" : (o.PriceDelta > 0m ? "+" : "") + Money(o.PriceDelta)).Append("</span>")
                      .Append(o.IsActive ? "" : Chip("ปิด", "off"))
                      .Append("<span class=\"dn-actions\">").Append(Btn("editOpt", o.Id, "แก้ไข", "sec", null)).Append(Btn("deleteOpt", o.Id, "ลบ", "danger", "ลบตัวเลือก \"" + o.NameTh + "\"?")).Append("</span></div>");
                }
                sb.Append("</div>");
            }
            litGroups.Text = sb.ToString();
        }

        // ── รายการ ───────────────────────────────────────────────────────────────

        private DiningMenuItem FindProduct(int pid)
        {
            foreach (var it in _menu.ListProductsForAdmin(false)) if (it.ProductId == pid) return it;
            return null;
        }

        private void ShowItemForm(int pid)
        {
            var it = FindProduct(pid);
            if (it == null) { Msg("err", "ไม่พบสินค้า #" + pid); return; }
            HideForms();
            pnlItem.Visible = true;
            hfTab.Value = "items";
            litItemTitle.Text = H(it.ProductName) + " <span class=\"mini\">฿" + Money(it.Price) + " · #" + Inv(it.ProductId) + "</span>";
            hfItemPid.Value = Inv(it.ProductId);
            bool inTable = false;
            foreach (var m in _menu.ListProductsForAdmin(true)) if (m.ProductId == pid) { inTable = true; break; }
            chkItemDining.Checked = inTable ? it.IsDining : true;   // ยังไม่เคยอยู่ในเมนู = ติ๊กให้
            string cat = Inv(it.CategoryId ?? 0);
            ddlItemCat.SelectedValue = ddlItemCat.Items.FindByValue(cat) != null ? cat : "0";
            txtItemSort.Text = Inv(it.SortOrder);
            txtItemName.Text = it.Name == it.ProductName ? "" : (it.Name ?? "");
            txtItemNameEn.Text = it.NameEn ?? "";
            txtItemDesc.Text = it.Description ?? "";
            txtItemPrep.Text = it.PrepMinutes.HasValue ? Inv(it.PrepMinutes.Value) : "";
            txtItemAllergen.Text = it.AllergenTags ?? "";
            chkItemSoldOut.Checked = it.IsSoldOut;
            txtItemSoldOutUntil.Text = it.SoldOutUntil.HasValue ? it.SoldOutUntil.Value.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture) : "";
            chkItemTrack.Checked = it.TrackStock;
            chkItemRecommended.Checked = it.IsRecommended;
            var zones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string z in (it.AvailableZones ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)) zones.Add(z.Trim());
            foreach (ListItem li in cblItemZones.Items) li.Selected = zones.Contains(li.Value);
            var gids = new HashSet<string>();
            foreach (var g in it.OptionGroups) gids.Add(Inv(g.Id));
            foreach (ListItem li in cblItemGroups.Items) li.Selected = gids.Contains(li.Value);
        }

        protected void btnSaveItem_Click(object sender, EventArgs e)
        {
            hfTab.Value = "items";
            try
            {
                int pid; int.TryParse(hfItemPid.Value, out pid);
                var it = FindProduct(pid);
                if (it == null) throw new ArgumentException("ไม่พบสินค้า");
                int cat; int.TryParse(ddlItemCat.SelectedValue, out cat);
                var zones = new List<string>();
                foreach (ListItem li in cblItemZones.Items) if (li.Selected) zones.Add(li.Value);
                var groups = new List<int>();
                foreach (ListItem li in cblItemGroups.Items) { int g; if (li.Selected && int.TryParse(li.Value, out g)) groups.Add(g); }
                DateTime? until = null;
                string u = (txtItemSoldOutUntil.Text ?? "").Trim();
                if (u.Length > 0)
                {
                    DateTime d;
                    if (DateTime.TryParseExact(u, new[] { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d) || DateTime.TryParse(u, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) until = d;
                    else throw new ArgumentException("รูปแบบ \"หมดถึงเวลา\" ไม่ถูกต้อง");
                }
                string name = (txtItemName.Text ?? "").Trim();
                var save = new DiningMenuItem
                {
                    ProductId = pid, ProductName = it.ProductName,
                    Name = name.Length == 0 ? it.ProductName : name,
                    NameEn = (txtItemNameEn.Text ?? "").Trim(),
                    Description = (txtItemDesc.Text ?? "").Trim(),
                    IsDining = chkItemDining.Checked,
                    CategoryId = cat > 0 ? (int?)cat : null,
                    SortOrder = ParseInt(txtItemSort.Text, 0, -9999, 9999),
                    PrepMinutes = string.IsNullOrWhiteSpace(txtItemPrep.Text) ? (int?)null : ParseInt(txtItemPrep.Text, 0, 0, 255),
                    AllergenTags = (txtItemAllergen.Text ?? "").Trim(),
                    IsSoldOut = chkItemSoldOut.Checked,
                    SoldOutUntil = chkItemSoldOut.Checked ? until : null,
                    TrackStock = chkItemTrack.Checked,
                    IsRecommended = chkItemRecommended.Checked,
                    AvailableZones = zones.Count == 0 ? null : string.Join(",", zones.ToArray())
                };
                _menu.SaveMenuItem(save, StaffName());
                _menu.SetItemOptionGroups(pid, groups);
                pnlItem.Visible = false;
                Msg("ok", "บันทึก \"" + save.Name + "\" แล้ว" + (save.IsDining ? "" : " (ซ่อนจากเมนู)"));
            }
            catch (ArgumentException ex) { pnlItem.Visible = true; Msg("err", ex.Message); }
            catch (Exception ex) { pnlItem.Visible = true; Msg("err", "บันทึกไม่สำเร็จ: " + ex.Message); }
        }

        protected void btnFilter_Click(object sender, EventArgs e) { hfTab.Value = "items"; HideForms(); }

        // ── หมวด ─────────────────────────────────────────────────────────────────

        private void ShowCatForm(DiningMenuCategory c)
        {
            HideForms();
            pnlCat.Visible = true;
            hfTab.Value = "cats";
            bool isNew = c == null;
            if (isNew) c = new DiningMenuCategory { SortOrder = _menu.ListCategories(true).Count + 1 };
            litCatTitle.Text = isNew ? "เพิ่มหมวด" : "แก้ไขหมวด " + H(c.NameTh);
            hfCatId.Value = Inv(c.Id);
            txtCatName.Text = c.NameTh ?? "";
            txtCatNameEn.Text = c.NameEn ?? "";
            txtCatIcon.Text = c.Icon ?? "";
            txtCatSort.Text = Inv(c.SortOrder);
            chkCatActive.Checked = c.IsActive;
        }

        protected void btnSaveCat_Click(object sender, EventArgs e)
        {
            hfTab.Value = "cats";
            try
            {
                int id; int.TryParse(hfCatId.Value, out id);
                var c = new DiningMenuCategory
                {
                    Id = id, NameTh = (txtCatName.Text ?? "").Trim(), NameEn = (txtCatNameEn.Text ?? "").Trim(), Icon = (txtCatIcon.Text ?? "").Trim(),
                    SortOrder = ParseInt(txtCatSort.Text, 0, -9999, 9999), IsActive = chkCatActive.Checked
                };
                _menu.SaveCategory(c);
                pnlCat.Visible = false;
                BindLists();
                Msg("ok", (id > 0 ? "แก้ไขหมวด " : "เพิ่มหมวด ") + c.NameTh + " แล้ว");
            }
            catch (ArgumentException ex) { pnlCat.Visible = true; Msg("err", ex.Message); }
            catch (Exception ex) { pnlCat.Visible = true; Msg("err", "บันทึกไม่สำเร็จ: " + ex.Message); }
        }

        // ── กลุ่มตัวเลือก / ตัวเลือก ─────────────────────────────────────────────

        private DiningOptionGroup FindGroup(int id)
        {
            foreach (var g in _menu.ListOptionGroups(true)) if (g.Id == id) return g;
            return null;
        }

        private DiningOption FindOption(int id, out DiningOptionGroup group)
        {
            group = null;
            foreach (var g in _menu.ListOptionGroups(true))
                foreach (var o in g.Options)
                    if (o.Id == id) { group = g; return o; }
            return null;
        }

        private void ShowGroupForm(DiningOptionGroup g)
        {
            HideForms();
            pnlGroup.Visible = true;
            hfTab.Value = "opts";
            bool isNew = g == null;
            if (isNew) g = new DiningOptionGroup { SortOrder = _menu.ListOptionGroups(true).Count + 1 };
            litGroupTitle.Text = isNew ? "เพิ่มกลุ่มตัวเลือก" : "แก้ไขกลุ่ม " + H(g.NameTh);
            hfGroupId.Value = Inv(g.Id);
            txtGroupName.Text = g.NameTh ?? "";
            txtGroupMin.Text = Inv(g.MinSelect);
            txtGroupMax.Text = Inv(g.MaxSelect);
            txtGroupSort.Text = Inv(g.SortOrder);
            chkGroupReq.Checked = g.IsRequired;
            chkGroupActive.Checked = g.IsActive;
        }

        protected void btnSaveGroup_Click(object sender, EventArgs e)
        {
            hfTab.Value = "opts";
            try
            {
                int id; int.TryParse(hfGroupId.Value, out id);
                var g = new DiningOptionGroup
                {
                    Id = id, NameTh = (txtGroupName.Text ?? "").Trim(), MinSelect = ParseInt(txtGroupMin.Text, 0, 0, 99), MaxSelect = ParseInt(txtGroupMax.Text, 1, 0, 99),
                    SortOrder = ParseInt(txtGroupSort.Text, 0, -9999, 9999), IsRequired = chkGroupReq.Checked, IsActive = chkGroupActive.Checked
                };
                if (g.IsRequired && g.MinSelect < 1) g.MinSelect = 1;
                _menu.SaveOptionGroup(g);
                pnlGroup.Visible = false;
                BindLists();
                Msg("ok", (id > 0 ? "แก้ไขกลุ่ม " : "เพิ่มกลุ่ม ") + g.NameTh + " แล้ว" + (id > 0 ? "" : " — กด \"+ ตัวเลือก\" เพื่อเพิ่มตัวเลือกในกลุ่ม"));
            }
            catch (ArgumentException ex) { pnlGroup.Visible = true; Msg("err", ex.Message); }
            catch (Exception ex) { pnlGroup.Visible = true; Msg("err", "บันทึกไม่สำเร็จ: " + ex.Message); }
        }

        private void ShowOptForm(DiningOptionGroup g, DiningOption o)
        {
            HideForms();
            pnlOpt.Visible = true;
            hfTab.Value = "opts";
            bool isNew = o == null;
            if (isNew) o = new DiningOption { GroupId = g.Id, SortOrder = g.Options.Count + 1 };
            litOptTitle.Text = (isNew ? "เพิ่มตัวเลือกในกลุ่ม " : "แก้ไขตัวเลือกในกลุ่ม ") + H(g.NameTh);
            hfOptId.Value = Inv(o.Id);
            hfOptGroup.Value = Inv(g.Id);
            txtOptName.Text = o.NameTh ?? "";
            txtOptDelta.Text = o.PriceDelta.ToString("0.##", CultureInfo.InvariantCulture);
            txtOptSort.Text = Inv(o.SortOrder);
            chkOptActive.Checked = o.IsActive;
        }

        protected void btnSaveOpt_Click(object sender, EventArgs e)
        {
            hfTab.Value = "opts";
            try
            {
                int id, gid; int.TryParse(hfOptId.Value, out id); int.TryParse(hfOptGroup.Value, out gid);
                decimal delta;
                if (!decimal.TryParse((txtOptDelta.Text ?? "0").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out delta)) throw new ArgumentException("ราคาเพิ่มไม่ถูกต้อง");
                var o = new DiningOption { Id = id, GroupId = gid, NameTh = (txtOptName.Text ?? "").Trim(), PriceDelta = delta, SortOrder = ParseInt(txtOptSort.Text, 0, -9999, 9999), IsActive = chkOptActive.Checked };
                _menu.SaveOption(o);
                pnlOpt.Visible = false;
                BindLists();
                Msg("ok", (id > 0 ? "แก้ไขตัวเลือก " : "เพิ่มตัวเลือก ") + o.NameTh + " แล้ว");
            }
            catch (ArgumentException ex) { pnlOpt.Visible = true; Msg("err", ex.Message); }
            catch (Exception ex) { pnlOpt.Visible = true; Msg("err", "บันทึกไม่สำเร็จ: " + ex.Message); }
        }

        protected void btnCancelForm_Click(object sender, EventArgs e) { HideForms(); }

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
                    case "addItem":
                        {
                            var it = FindProduct(id);
                            if (it == null) { Msg("err", "ไม่พบสินค้า"); break; }
                            int sort = 0;
                            foreach (var x in _menu.ListProductsForAdmin(true)) if (x.SortOrder > sort) sort = x.SortOrder;
                            _menu.SaveMenuItem(new DiningMenuItem { ProductId = id, ProductName = it.ProductName, Name = it.ProductName, IsDining = true, SortOrder = sort + 1 }, StaffName());
                            Msg("ok", "เพิ่ม \"" + it.ProductName + "\" เข้าเมนูแล้ว — กด \"แก้ไข\" เพื่อตั้งหมวด/ตัวเลือก");
                            ShowItemForm(id);
                            break;
                        }
                    case "editItem": ShowItemForm(id); break;
                    case "soldToday":
                        _menu.SetSoldOut(id, true, DateTime.Today.AddDays(1));
                        Msg("ok", "ตั้ง \"หมดวันนี้\" แล้ว — กลับมาขายอัตโนมัติ " + DateTime.Today.AddDays(1).ToString("dd/MM 00:00", CultureInfo.InvariantCulture));
                        hfTab.Value = "items";
                        break;
                    case "soldOut":
                        _menu.SetSoldOut(id, true, null);
                        Msg("ok", "ตั้งเป็น \"หมด\" แล้ว (จนกว่าจะกด \"มีแล้ว\")");
                        hfTab.Value = "items";
                        break;
                    case "available":
                        _menu.SetSoldOut(id, false, null);
                        Msg("ok", "กลับมาขายแล้ว");
                        hfTab.Value = "items";
                        break;
                    case "removeItem":
                        {
                            var it = FindProduct(id);
                            _menu.RemoveMenuItem(id);
                            Msg("ok", "เอา \"" + (it == null ? "#" + id : it.Name) + "\" ออกจากเมนูแล้ว");
                            hfTab.Value = "items";
                            break;
                        }
                    case "newCat": ShowCatForm(null); break;
                    case "editCat":
                        {
                            DiningMenuCategory found = null;
                            foreach (var c in _menu.ListCategories(true)) if (c.Id == id) found = c;
                            if (found == null) Msg("err", "ไม่พบหมวด"); else ShowCatForm(found);
                            break;
                        }
                    case "deleteCat":
                        _menu.DeleteCategory(id);
                        BindLists();
                        hfTab.Value = "cats";
                        Msg("ok", "ลบหมวดแล้ว — รายการในหมวดย้ายไป \"อื่น ๆ\"");
                        break;
                    case "newGroup": ShowGroupForm(null); break;
                    case "editGroup":
                        {
                            var g = FindGroup(id);
                            if (g == null) Msg("err", "ไม่พบกลุ่ม"); else ShowGroupForm(g);
                            break;
                        }
                    case "deleteGroup":
                        _menu.DeleteOptionGroup(id);
                        BindLists();
                        hfTab.Value = "opts";
                        Msg("ok", "ลบกลุ่มตัวเลือกแล้ว");
                        break;
                    case "newOpt":
                        {
                            var g = FindGroup(id);
                            if (g == null) Msg("err", "ไม่พบกลุ่ม"); else ShowOptForm(g, null);
                            break;
                        }
                    case "editOpt":
                        {
                            DiningOptionGroup g;
                            var o = FindOption(id, out g);
                            if (o == null) Msg("err", "ไม่พบตัวเลือก"); else ShowOptForm(g, o);
                            break;
                        }
                    case "deleteOpt":
                        _menu.DeleteOption(id);
                        BindLists();
                        hfTab.Value = "opts";
                        Msg("ok", "ลบตัวเลือกแล้ว");
                        break;
                    default: break;
                }
            }
            catch (ArgumentException ex) { Msg("err", ex.Message); }
            catch (Exception ex) { Msg("err", "ผิดพลาด: " + ex.Message); }
        }
    }
}
