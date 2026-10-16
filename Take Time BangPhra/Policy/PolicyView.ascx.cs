using System;
using System.Text;
using System.Web;
using System.Web.UI;
using Take_Time_BangPhra.Admin.Settings;

namespace Take_Time_BangPhra.PolicyPages
{
    /// <summary>
    /// เนื้อหาหน้านโยบายสาธารณะ (ใช้ร่วมทุกหน้าใน /Policy) — หัวเรื่องไทย + อังกฤษ, ฉบับที่/วันที่ปรับปรุง,
    /// เมนูไปนโยบายอื่น, ปุ่มพิมพ์, ข้อความนโยบาย (ผ่าน <see cref="BookingPolicy.ToHtml"/> ที่ encode ก่อนเสมอ)
    /// และช่องทางติดต่อ · ผู้ดูแล (Owner/Admin) เห็นป้ายเตือนเมื่อยังมีช่อง "[แก้ไข: …]" ค้าง
    /// </summary>
    public partial class PolicyView : UserControl
    {
        /// <summary>คีย์นโยบาย — เว้นว่างได้ถ้าอยู่บนหน้าที่สืบจาก <see cref="PolicyPage"/></summary>
        public string PolicyKey { get; set; }

        protected string Key { get; private set; }
        protected string TitleTh { get; private set; }
        protected string TitleEnglish { get; private set; }
        protected string MetaLine { get; private set; }
        protected string BodyHtml { get; private set; }
        protected string NavHtml { get; private set; }
        protected string AdminHtml { get; private set; }
        protected string NoticeHtml { get; private set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            Key = ResolveKey();
            TitleTh = BookingPolicy.Title(Key);
            TitleEnglish = BookingPolicy.TitleEn(Key);

            DateTime? upd = BookingPolicy.UpdatedAtFor(Key);
            MetaLine = "ฉบับที่ " + BookingPolicy.VersionFor(Key)
                + (upd.HasValue ? " · ปรับปรุงล่าสุด " + upd.Value.ToString("dd/MM/yyyy") : "");

            string text = BookingPolicy.Get(Key);
            bool admin = PolicyPage.IsPolicyAdmin(HttpContext.Current);
            string body = BookingPolicy.ToHtml(text);
            BodyHtml = admin ? SettingsUi.HighlightPlaceholders(body) : body;

            NavHtml = BuildNav();
            AdminHtml = admin ? BuildAdminBar(text) : "";

            NoticeHtml = "";
            if (Key == BookingPolicy.KeyPet && !PetStay.Enabled)
            {
                NoticeHtml = "<div class=\"pp-notice\" role=\"status\"><b>🐾 ขณะนี้ที่พักยังไม่เปิดรับสัตว์เลี้ยงเข้าพัก</b>"
                    + "<span lang=\"en\"> (Pets are currently not accepted)</span><br/>"
                    + "นโยบายด้านล่างจะใช้เมื่อที่พักเปิดรับสัตว์เลี้ยง — หากต้องการสอบถามเพิ่มเติม กรุณาติดต่อที่พัก</div>";
            }
        }

        private string ResolveKey()
        {
            string k = PolicyKey;
            if (string.IsNullOrEmpty(k))
            {
                var host = Page as PolicyPage;
                if (host != null) k = host.PolicyKey;
            }
            foreach (string known in BookingPolicy.PublicKeys)
                if (string.Equals(known, k, StringComparison.OrdinalIgnoreCase)) return known;
            return BookingPolicy.KeyTerms;
        }

        private static string Icon(string key)
        {
            switch (key)
            {
                case BookingPolicy.KeyTerms: return "📜";
                case BookingPolicy.KeyPrivacy: return "🔒";
                case BookingPolicy.KeyRefund: return "💸";
                case BookingPolicy.KeyCancellation: return "📅";
                case BookingPolicy.KeyPet: return "🐾";
                default: return "📄";
            }
        }

        private string BuildNav()
        {
            var sb = new StringBuilder();
            foreach (string k in BookingPolicy.PublicKeys)
            {
                bool on = k == Key;
                sb.Append("<a href=\"").Append(HttpUtility.HtmlAttributeEncode(ResolveUrl(BookingPolicy.PublicPath(k)))).Append("\"")
                  .Append(on ? " class=\"on\" aria-current=\"page\"" : "").Append(">")
                  .Append(Icon(k)).Append(" ").Append(HttpUtility.HtmlEncode(BookingPolicy.Title(k)))
                  .Append("</a>");
            }
            return sb.ToString();
        }

        private string BuildAdminBar(string text)
        {
            string editUrl = Key == BookingPolicy.KeyPet
                ? ResolveUrl("~/Admin/Settings/PetStay")
                : ResolveUrl("~/Admin/Settings/BookingPolicies") + "#bp-" + BookingPolicy.Slug(Key);
            int ph = SettingsUi.CountPlaceholders(text);
            var sb = new StringBuilder();
            sb.Append("<div class=\"pp-admin").Append(ph > 0 ? " warn" : "").Append("\" role=\"note\">");
            if (ph > 0)
                sb.Append("⚠ <b>เห็นเฉพาะผู้ดูแล:</b> นโยบายนี้ยังมีข้อความตัวอย่าง/ช่อง <b>[แก้ไข: …]</b> ค้างอยู่ <b>")
                  .Append(ph).Append("</b> จุด (ไฮไลต์สีเหลือง) — ลูกค้าและ payment gateway เห็นข้อความนี้ตามจริง ");
            else
                sb.Append("<b>เห็นเฉพาะผู้ดูแล:</b> หน้านี้เปิดสาธารณะ ใช้ลิงก์นี้กรอกในระบบชำระเงิน/Google ได้ ");
            sb.Append("<a href=\"").Append(HttpUtility.HtmlAttributeEncode(editUrl)).Append("\">แก้ไขนโยบายนี้ →</a></div>");
            return sb.ToString();
        }
    }
}
