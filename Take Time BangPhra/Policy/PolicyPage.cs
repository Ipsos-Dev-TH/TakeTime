using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;

namespace Take_Time_BangPhra.PolicyPages
{
    /// <summary>
    /// ฐานของหน้านโยบายสาธารณะ (/Policy/Terms, /Privacy, /Refund, /Cancellation, /Pet)
    ///
    /// แต่ละหน้าเป็นไฟล์บาง ๆ ที่บอกแค่ว่าเป็นนโยบายข้อไหน (<see cref="PolicyKey"/>) — เนื้อหาทั้งหมดวาดโดย
    /// <see cref="PolicyView"/> (Policy/PolicyView.ascx) ที่ใช้ร่วมกัน · ไม่ต้องล็อกอิน · อ่านข้อความผ่าน cache ของ
    /// <see cref="BookingPolicy"/> เท่านั้น (ไม่เขียน DB) · ลิงก์เหล่านี้คือหน้า T&amp;C / Privacy / Refund ที่ payment
    /// gateway (PaySo) และ Google ขอให้เปิดสาธารณะ — ห้ามใส่ noindex
    /// canonical / og:url มาจาก Site.Master (URL ไม่มี .aspx เพราะ FriendlyUrls redirect ถาวรอยู่แล้ว)
    /// </summary>
    public abstract class PolicyPage : Page
    {
        /// <summary>คีย์นโยบายใน <see cref="BookingPolicy"/> ที่หน้านี้แสดง</summary>
        public abstract string PolicyKey { get; }

        protected override void OnLoad(EventArgs e)
        {
            Title = BookingPolicy.Title(PolicyKey) + " (" + BookingPolicy.TitleEn(PolicyKey) + ") | Take Time Nature Resort";
            base.OnLoad(e);
        }

        /// <summary>meta description + Open Graph ของหน้านี้ (แทนค่า default ของรีสอร์ทใน ContentPlaceHolder SeoHead)</summary>
        protected string SeoHeadHtml
        {
            get
            {
                string key = PolicyKey;
                string title = BookingPolicy.Title(key) + " (" + BookingPolicy.TitleEn(key) + ") — Take Time Nature Resort บางพระ";
                string desc = BookingPolicy.Title(key) + " " + BookingPolicy.TitleEn(key)
                    + " ของ Take Time Nature Resort บางพระ ศรีราชา ชลบุรี (ฉบับที่ " + BookingPolicy.VersionFor(key) + ")";
                string snippet = PlainSnippet(BookingPolicy.Get(key), 170 - desc.Length);
                if (snippet.Length > 0) desc += " — " + snippet;

                var sb = new StringBuilder();
                sb.Append("<meta name=\"description\" content=\"").Append(HttpUtility.HtmlAttributeEncode(desc)).Append("\" />\n");
                sb.Append("    <meta property=\"og:title\" content=\"").Append(HttpUtility.HtmlAttributeEncode(title)).Append("\" />\n");
                sb.Append("    <meta property=\"og:description\" content=\"").Append(HttpUtility.HtmlAttributeEncode(desc)).Append("\" />\n");
                return sb.ToString();
            }
        }

        /// <summary>ข้อความล้วนบรรทัดเดียวสำหรับ meta description (ตัด ** และช่องว่างซ้ำ) ยาวไม่เกิน max ตัวอักษร</summary>
        private static string PlainSnippet(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text) || max < 20) return "";
            string s = Regex.Replace(text.Replace("**", ""), @"\s+", " ").Trim();
            if (s.Length <= max) return s;
            int cut = max - 1;
            if (char.IsHighSurrogate(s[cut - 1])) cut--;
            return s.Substring(0, cut).TrimEnd() + "…";
        }

        // ── ใช้ร่วมกับหน้าอื่น ───────────────────────────────────────────────

        /// <summary>ผู้ดูแลที่ล็อกอิน (Owner / Admin) — เห็นป้ายเตือนข้อความตัวอย่างและลิงก์แก้ไขบนหน้านโยบาย</summary>
        public static bool IsPolicyAdmin(HttpContext ctx)
        {
            try
            {
                if (ctx == null || ctx.Session == null) return false;
                if (Convert.ToString(ctx.Session["permission"]) != "True") return false;
                string role = Convert.ToString(ctx.Session["User"]);
                return role == "Owner" || role == "Admin";
            }
            catch { return false; }
        }

        /// <summary>URL เต็ม (scheme + host) ของหน้านโยบาย — สำหรับคัดลอกไปกรอกในระบบภายนอก</summary>
        public static string AbsoluteUrl(Control ctl, string key)
        {
            string path = BookingPolicy.PublicPath(key);
            if (path == null || ctl == null) return "";
            string rel = ctl.ResolveUrl(path);
            try
            {
                HttpContext ctx = HttpContext.Current;
                if (ctx != null) return ctx.Request.Url.GetLeftPart(UriPartial.Authority) + rel;
            }
            catch { }
            return rel;
        }

        /// <summary>
        /// ตารางลิงก์หน้าสาธารณะสำหรับหน้าตั้งค่าแอดมิน: ชื่อ · URL · "ดูหน้าสาธารณะ" · "คัดลอกลิงก์"
        /// (ใช้ทั้งหน้า นโยบายการจอง และ สัตว์เลี้ยงเข้าพัก) — ข้อความทั้งหมด encode แล้ว
        /// </summary>
        public static string AdminLinksHtml(Control ctl, params string[] keys)
        {
            var sb = new StringBuilder();
            sb.Append("<style>")
              .Append(".pp-links{display:flex;flex-direction:column;gap:8px;margin-top:6px;}")
              .Append(".pp-link-row{display:flex;flex-wrap:wrap;align-items:center;gap:8px 10px;padding:9px 12px;border:1px solid #efebe9;border-radius:10px;background:#faf8f7;}")
              .Append(".pp-link-name{font-weight:600;color:#3e2723;min-width:190px;}")
              .Append(".pp-link-url{flex:1 1 240px;min-width:0;font-family:Consolas,monospace;font-size:12.5px;color:#5d4037;word-break:break-all;}")
              .Append(".pp-link-btn{display:inline-flex;align-items:center;gap:6px;padding:6px 12px;border-radius:8px;border:1.5px solid #d7ccc8;background:#fff;color:#5d4037;font-size:13px;font-weight:600;cursor:pointer;text-decoration:none;}")
              .Append(".pp-link-btn:hover{background:#efebe9;text-decoration:none;color:#3e2723;}")
              .Append(".pp-link-btn.done{background:#e8f6ee;border-color:#9fd6b5;color:#16653e;}")
              .Append("</style><div class=\"pp-links\">");
            foreach (string key in keys)
            {
                string path = BookingPolicy.PublicPath(key);
                if (path == null) continue;
                string abs = AbsoluteUrl(ctl, key);
                string attr = HttpUtility.HtmlAttributeEncode(abs);
                sb.Append("<div class=\"pp-link-row\"><span class=\"pp-link-name\">")
                  .Append(HttpUtility.HtmlEncode(BookingPolicy.Title(key)))
                  .Append("</span><code class=\"pp-link-url\">").Append(HttpUtility.HtmlEncode(abs)).Append("</code>")
                  .Append("<a class=\"pp-link-btn\" href=\"").Append(HttpUtility.HtmlAttributeEncode(ctl.ResolveUrl(path)))
                  .Append("\" target=\"_blank\" rel=\"noopener\"><i class=\"fas fa-external-link-alt\"></i> ดูหน้าสาธารณะ</a>")
                  .Append("<button type=\"button\" class=\"pp-link-btn\" data-pp-copy=\"").Append(attr)
                  .Append("\"><i class=\"far fa-copy\"></i> คัดลอกลิงก์</button></div>");
            }
            sb.Append("</div>");
            // คัดลอกลิงก์: clipboard API (ต้อง https) → สำรองด้วย textarea + execCommand → สุดท้ายให้คัดลอกเองจาก prompt
            sb.Append("<script>(function(){if(window.__ppCopyBound)return;window.__ppCopyBound=true;")
              .Append("function done(b){if(b.className.indexOf(' done')>=0)return;var t=b.innerHTML;b.className+=' done';b.innerHTML='\\u2713 คัดลอกแล้ว';")
              .Append("setTimeout(function(){b.className=b.className.replace(' done','');b.innerHTML=t;},1600);}")
              .Append("function legacy(u,b){var ta=document.createElement('textarea');ta.value=u;ta.setAttribute('readonly','');")
              .Append("ta.style.position='fixed';ta.style.left='-9999px';document.body.appendChild(ta);ta.select();var ok=false;")
              .Append("try{ok=document.execCommand('copy');}catch(e){}document.body.removeChild(ta);")
              .Append("if(ok)done(b);else window.prompt('คัดลอกลิงก์นี้',u);}")
              .Append("document.addEventListener('click',function(e){var b=e.target;while(b&&b!==document&&!(b.getAttribute&&b.getAttribute('data-pp-copy')))b=b.parentNode;")
              .Append("if(!b||b===document)return;e.preventDefault();var u=b.getAttribute('data-pp-copy');")
              .Append("if(navigator.clipboard&&window.isSecureContext){navigator.clipboard.writeText(u).then(function(){done(b);},function(){legacy(u,b);});}")
              .Append("else legacy(u,b);});})();</script>");
            return sb.ToString();
        }
    }
}
