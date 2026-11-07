using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using Take_Time_BangPhra.Dining;
using Take_Time_BangPhra.Payments.SlipCheck;

namespace Take_Time_BangPhra.Admin.Dining
{
    /// <summary>
    /// แผ่นพิมพ์ QR ติดโต๊ะ — ?zone= | ?table= | (ว่าง = ทุกโต๊ะที่ใช้งาน) · ?layout=grid8 (A4 2×4) | grid18 (A4 3×6) | tent (ป้ายตั้งโต๊ะ 10×15 ซม. 2 ใบ/แผ่น) · ?contact=0 ซ่อนบรรทัดติดต่อ
    /// QR = SVG ฝั่งเซิร์ฟเวอร์ (<see cref="DiningQr.TryRenderSvg"/>); ไม่มี zxing.dll → fallback qrcodejs ฝั่งเบราว์เซอร์ (เหมือน Admin/RoomService/OrderManagement.aspx:779)
    /// โลโก้ = ~/Images/Logo.png (ไฟล์ที่ Site.Master ใช้) · ชื่อร้าน = Business_Info.Company_Name · ติดต่อ = SlipVerificationService.CustomerContactText (โทร + LINE OA)
    /// สิทธิ์ Perm.SvcGuest — ไม่ guard ฟีเจอร์ (พิมพ์ได้ก่อนเปิดสวิตช์)
    /// </summary>
    public partial class QrSheetPage : Page
    {
        private static readonly string Conn = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private readonly DiningTableService _tables = new DiningTableService(Conn);

        private sealed class Layout
        {
            public string Key, Label; public int PerSheet;
            public Layout(string key, string label, int perSheet) { Key = key; Label = label; PerSheet = perSheet; }
        }

        private static readonly Layout[] Layouts =
        {
            new Layout("grid8", "A4 2×4 (8 ใบ)", 8),
            new Layout("grid18", "A4 3×6 (18 ใบ)", 18),
            new Layout("tent", "ป้ายตั้งโต๊ะ 10×15 ซม. (2 ใบ/แผ่น)", 2)
        };

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SvcGuest)) return;
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login", false);
                HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }
            if (!IsPostBack) Render();
        }

        private static string H(string s) { return HttpUtility.HtmlEncode(s ?? ""); }
        private static string Inv(int v) { return v.ToString(CultureInfo.InvariantCulture); }

        private void Render()
        {
            int zoneId, tableId;
            int.TryParse(Request.QueryString["zone"], out zoneId);
            int.TryParse(Request.QueryString["table"], out tableId);
            string layoutKey = (Request.QueryString["layout"] ?? "grid8").Trim().ToLowerInvariant();
            Layout layout = Layouts[0];
            foreach (var l in Layouts) if (l.Key == layoutKey) layout = l;
            bool contact = Request.QueryString["contact"] != "0";

            if (!_tables.SchemaReady())
            {
                litMsg.Text = "<div class=\"dn-alert err\">ยังไม่ได้รัน migration PHASE19_42 — ไม่มีตารางโต๊ะ</div>";
                return;
            }

            // โต๊ะที่จะพิมพ์
            var tables = new List<DiningTable>();
            string scopeLabel;
            if (tableId > 0)
            {
                var t = _tables.GetTable(tableId);
                if (t != null) tables.Add(t);
                scopeLabel = t == null ? "ไม่พบโต๊ะ" : "โต๊ะ " + t.Code + " (" + t.ZoneName + ")";
            }
            else
            {
                tables = _tables.ListTables(zoneId > 0 ? (int?)zoneId : null, false);
                var z = zoneId > 0 ? _tables.GetZone(zoneId) : null;
                scopeLabel = z == null ? "ทุกโซน" : "โซน " + z.Code + " — " + z.NameTh;
            }

            RenderTools(zoneId, tableId, layout, contact, scopeLabel, tables.Count);
            if (tables.Count == 0)
            {
                litMsg.Text = "<div class=\"dn-alert warn\">ไม่มีโต๊ะที่ใช้งานในขอบเขตนี้ — สร้างโต๊ะที่หน้า <a href=\"" + ResolveUrl("~/Admin/Dining/Zones") + "\">โซน &amp; โต๊ะ</a> ก่อน</div>";
                return;
            }

            bool engine = DiningQr.EngineReady();
            string bizName = LoadCompanyName();
            string contactText = "";
            if (contact) { try { contactText = SlipVerificationService.CustomerContactText() ?? ""; } catch { contactText = ""; } }
            string logo = ResolveUrl("~/Images/Logo.png");
            string host = HostOnly(DiningQr.BuildOrderUrl(""));

            var sb = new StringBuilder();
            int i = 0;
            foreach (var t in tables)
            {
                if (i % layout.PerSheet == 0) sb.Append(i == 0 ? "" : "</div>").Append("<div class=\"qs-sheet ").Append(layout.Key).Append("\">");
                i++;
                string url = DiningQr.BuildOrderUrl(t.QrToken);
                sb.Append("<div class=\"qs-card\">");
                sb.Append("<img class=\"qs-logo\" src=\"").Append(H(logo)).Append("\" alt=\"\" onerror=\"this.style.display='none'\" />");
                if (!string.IsNullOrEmpty(bizName)) sb.Append("<div class=\"qs-biz\">").Append(H(bizName)).Append("</div>");
                sb.Append("<div class=\"qs-title\">สแกนสั่งอาหาร<small>Scan to order</small></div>");
                sb.Append("<div class=\"qs-qr\">");
                string svg;
                if (engine && DiningQr.TryRenderSvg(url, 512, out svg)) sb.Append(svg);
                else sb.Append("<div class=\"qr-js\" data-url=\"").Append(H(url)).Append("\"><span class=\"qs-nojs\">กำลังสร้าง QR…</span></div>");
                sb.Append("</div>");
                sb.Append("<div class=\"qs-table\">").Append(H(t.Code)).Append("</div>");
                sb.Append("<div class=\"qs-zone\">").Append(H(t.ZoneName)).Append(string.IsNullOrEmpty(t.NameTh) || t.NameTh == t.Code ? "" : " · " + H(t.NameTh)).Append("</div>");
                sb.Append("<div class=\"qs-note\">เปิดกล้องมือถือส่องที่ QR แล้วแตะลิงก์ — สั่งได้หลายรอบ กด \"เช็คบิล\" เมื่อพร้อมชำระ · Point your camera at the QR, order as many rounds as you like, tap \"Bill\" when done</div>");
                if (!string.IsNullOrEmpty(host)) sb.Append("<div class=\"qs-url\">").Append(H(host)).Append("/Order/… · โต๊ะ ").Append(H(t.Code)).Append("</div>");
                if (!string.IsNullOrEmpty(contactText)) sb.Append("<div class=\"qs-contact\">").Append(H(contactText)).Append("</div>");
                sb.Append("</div>");
            }
            if (i > 0) sb.Append("</div>");
            litSheets.Text = sb.ToString();

            if (!engine)
            {
                litMsg.Text = "<div class=\"dn-alert warn\">สร้าง QR ฝั่งเซิร์ฟเวอร์ไม่ได้ (ไม่พบ zxing.dll" + (string.IsNullOrEmpty(DiningQr.LastError) ? "" : " — " + H(DiningQr.LastError))
                    + ") — ใช้ตัวสร้างในเบราว์เซอร์แทน (ต้องต่ออินเทอร์เน็ต) รอให้ QR ขึ้นครบก่อนกดพิมพ์</div>";
                litFallback.Text = "<script src=\"https://cdn.jsdelivr.net/npm/qrcodejs@1.0.0/qrcode.min.js\"></script>"
                    + "<script>(function(){var els=document.querySelectorAll('.qr-js[data-url]');for(var i=0;i<els.length;i++){var el=els[i];el.innerHTML='';"
                    + "try{new QRCode(el,{text:el.getAttribute('data-url'),width:512,height:512,correctLevel:QRCode.CorrectLevel.M});}catch(e){el.innerHTML='<span class=\"qs-nojs\">สร้าง QR ไม่ได้</span>';}}})();</script>";
            }
        }

        private void RenderTools(int zoneId, int tableId, Layout layout, bool contact, string scopeLabel, int count)
        {
            string baseUrl = ResolveUrl("~/Admin/Dining/QrSheet");
            var sb = new StringBuilder();
            sb.Append("<div class=\"grp\"><span>ขอบเขต:</span><b>").Append(H(scopeLabel)).Append("</b> <span>(").Append(Inv(count)).Append(" โต๊ะ)</span>");
            if (tableId == 0)
            {
                sb.Append(" <a class=\"opt").Append(zoneId == 0 ? " on" : "").Append("\" href=\"").Append(H(Url(baseUrl, 0, 0, layout.Key, contact))).Append("\">ทุกโซน</a>");
                foreach (var z in _tables.ListZones(false))
                    sb.Append("<a class=\"opt").Append(z.Id == zoneId ? " on" : "").Append("\" href=\"").Append(H(Url(baseUrl, z.Id, 0, layout.Key, contact))).Append("\">").Append(H(z.Code)).Append("</a>");
            }
            else sb.Append(" <a class=\"opt\" href=\"").Append(H(Url(baseUrl, 0, 0, layout.Key, contact))).Append("\">พิมพ์ทุกโต๊ะ</a>");
            sb.Append("</div>");

            sb.Append("<div class=\"grp\"><span>แบบ:</span>");
            foreach (var l in Layouts)
                sb.Append("<a class=\"opt").Append(l.Key == layout.Key ? " on" : "").Append("\" href=\"").Append(H(Url(baseUrl, zoneId, tableId, l.Key, contact))).Append("\">").Append(H(l.Label)).Append("</a>");
            sb.Append("</div>");

            sb.Append("<div class=\"grp\"><span>ติดต่อร้าน:</span>");
            sb.Append("<a class=\"opt").Append(contact ? " on" : "").Append("\" href=\"").Append(H(Url(baseUrl, zoneId, tableId, layout.Key, true))).Append("\">แสดง</a>");
            sb.Append("<a class=\"opt").Append(!contact ? " on" : "").Append("\" href=\"").Append(H(Url(baseUrl, zoneId, tableId, layout.Key, false))).Append("\">ซ่อน</a>");
            sb.Append("</div>");
            litTools.Text = sb.ToString();
        }

        private static string Url(string baseUrl, int zoneId, int tableId, string layout, bool contact)
        {
            var q = new List<string>();
            if (tableId > 0) q.Add("table=" + Inv(tableId));
            else if (zoneId > 0) q.Add("zone=" + Inv(zoneId));
            q.Add("layout=" + layout);
            if (!contact) q.Add("contact=0");
            return baseUrl + "?" + string.Join("&", q.ToArray());
        }

        private static string HostOnly(string url)
        {
            try
            {
                Uri u;
                if (Uri.TryCreate(url, UriKind.Absolute, out u)) return u.Host;
            }
            catch { }
            return "";
        }

        private static string LoadCompanyName()
        {
            try
            {
                using (var con = new SqlConnection(Conn))
                using (var cmd = new SqlCommand("SELECT TOP 1 * FROM Business_Info ORDER BY ID", con))
                using (var da = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    da.Fill(dt);
                    if (dt.Rows.Count == 0) return "";
                    foreach (string col in new[] { "Company_Name", "Business_Name", "Name" })
                    {
                        if (!dt.Columns.Contains(col)) continue;
                        object v = dt.Rows[0][col];
                        if (v != null && v != DBNull.Value && v.ToString().Trim().Length > 0) return v.ToString().Trim();
                    }
                }
            }
            catch { }
            return "";
        }
    }
}
