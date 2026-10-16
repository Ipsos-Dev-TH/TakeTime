using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// หน้ากิจกรรมสาธารณะ (เมนู "กิจกรรม" บนหน้าแรก) — แสดงกิจกรรมที่ตั้งค่าให้ ShowOnWebsite
    /// ข้อมูลชุดเดียวกับที่ผู้เข้าพักเห็นใน Guest Portal (จัดการจากหน้า Admin ที่เดียว)
    /// </summary>
    public partial class ActivitiesPublic : Page
    {
        private readonly string _conn =
            System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        /// <summary>ข้อมูลแผนที่ (หมุด + ขอบเขตโซน) ที่หน้า .aspx เอาไปวาดด้วย Leaflet</summary>
        protected string MapJson = "{\"places\":[],\"categories\":[]}";

        /// <summary>มีหมุดให้วาดจริงไหม — ไม่มีก็ไม่ต้องโหลดแผนที่เปล่า</summary>
        protected bool HasMap;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Activities", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            if (!IsPostBack) RenderActivities();
        }

        private void RenderActivities()
        {
            var sb = new StringBuilder();
            try
            {
                var svc = new ActivityService(_conn);
                DataTable dt = svc.GetVisibleActivities("WEBSITE");

                if (dt == null || dt.Rows.Count == 0)
                {
                    // ไม่มีกิจกรรม ไม่ได้แปลว่าไม่มีสถานที่แนะนำ — แสดงส่วนสถานที่ต่อไป
                    var only = new StringBuilder();
                    RenderNearbySection(only);
                    litActivities.Text = only.Length > 0
                        ? only.ToString()
                        : "<div class='act-empty'><i class='fas fa-calendar-xmark'></i>" +
                          "<h4>ยังไม่มีข้อมูลกิจกรรม</h4><p>กรุณาติดต่อเจ้าหน้าที่เพื่อสอบถามรายละเอียด</p></div>";
                    return;
                }

                RenderSection(sb, dt, "ON_PROPERTY", "กิจกรรมในที่พัก", "fa-tree");
                RenderSection(sb, dt, "OFF_PROPERTY", "กิจกรรมนอกที่พัก", "fa-mountain-sun");
                RenderNearbySection(sb);
                litActivities.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Activities page error: " + ex.Message);
                litActivities.Text =
                    "<div class='act-empty'><i class='fas fa-triangle-exclamation'></i>" +
                    "<h4>ไม่สามารถโหลดข้อมูลกิจกรรมได้</h4><p>กรุณาลองใหม่อีกครั้ง</p></div>";
            }
        }

        private void RenderSection(StringBuilder sb, DataTable dt, string category, string title, string icon)
        {
            var rows = dt.Select($"Category = '{category}'");
            if (rows.Length == 0) return;

            sb.Append("<div class='act-section' style='margin-bottom:38px;'>");
            sb.Append($"<h2 style='color:#2e5d3a;font-weight:700;margin-bottom:20px;font-size:1.5em;'>" +
                      $"<i class='fas {icon}'></i> {Server.HtmlEncode(title)} " +
                      $"<small style='color:#90a096;font-weight:400;font-size:.62em;'>({rows.Length} รายการ)</small></h2>");
            sb.Append("<div class='act-grid'>");

            foreach (DataRow r in rows)
            {
                string name = Val(r, "ActivityName");
                string shortDesc = Val(r, "ShortDescription");
                string desc = string.IsNullOrWhiteSpace(shortDesc) ? Val(r, "Description") : shortDesc;
                if (desc.Length > 165) desc = desc.Substring(0, 165) + "...";
                string img = Val(r, "ImagePath");
                string iconClass = Val(r, "IconClass");
                if (string.IsNullOrWhiteSpace(iconClass)) iconClass = "fa-star";
                string duration = Val(r, "Duration");
                string location = Val(r, "Location");
                bool bookable = ToBool(r, "IsBookable");

                decimal price = r["Price"] != DBNull.Value ? Convert.ToDecimal(r["Price"]) : 0m;
                string pricingMode = Val(r, "PricingMode");
                bool isFree = price <= 0 || pricingMode == "FREE";

                string priceText = isFree
                    ? "<i class='fas fa-gift'></i> ไม่มีค่าใช้จ่าย"
                    : $"฿{price:N0}{PriceSuffix(pricingMode)}";

                sb.Append($"<div class='act-card' data-cat='{category}'>");

                // รูป / ไอคอน
                if (!string.IsNullOrWhiteSpace(img))
                    sb.Append($"<div class='act-thumb' style=\"background-image:url('{Server.HtmlEncode(img)}')\">");
                else
                    sb.Append($"<div class='act-thumb'><i class='fas {Server.HtmlEncode(iconClass)}'></i>");

                sb.Append("<div class='act-badges'>");
                sb.Append(isFree
                    ? "<span class='act-badge badge-free'>ฟรี</span>"
                    : "<span class='act-badge badge-paid'>มีค่าบริการ</span>");
                if (bookable)
                    sb.Append("<span class='act-badge badge-book'><i class='fas fa-clock'></i> จองเวลา</span>");
                sb.Append("</div></div>");

                // เนื้อหา
                sb.Append("<div class='act-body'>");
                sb.Append($"<h3>{Server.HtmlEncode(name)}</h3>");
                sb.Append($"<div class='act-desc'>{Server.HtmlEncode(desc)}</div>");

                if (!string.IsNullOrWhiteSpace(duration) || !string.IsNullOrWhiteSpace(location))
                {
                    sb.Append("<div class='act-meta'>");
                    if (!string.IsNullOrWhiteSpace(duration))
                        sb.Append($"<span><i class='fas fa-clock'></i>{Server.HtmlEncode(duration)}</span>");
                    if (!string.IsNullOrWhiteSpace(location))
                        sb.Append($"<span><i class='fas fa-location-dot'></i>{Server.HtmlEncode(location)}</span>");
                    sb.Append("</div>");
                }

                sb.Append($"<div class='act-price{(isFree ? " free" : "")}'>{priceText}</div>");
                sb.Append("</div></div>");
            }

            sb.Append("</div></div>");
        }

        /// <summary>
        /// ส่วน "สถานที่แนะนำใกล้เคียง" — แผนที่ขอบเขตพื้นที่ + หมุด + การ์ดพร้อมปุ่มนำทาง
        /// ใช้ข้อมูลชุดเดียวกับ Guest Portal (จัดการที่ Admin → จัดการสถานที่ใกล้เคียง ที่เดียว)
        /// การ์ดใช้คลาส act-card + data-cat='NEARBY' เพื่อให้ตัวกรองแท็บเดิมทำงานต่อได้ทันที
        /// </summary>
        private void RenderNearbySection(StringBuilder sb)
        {
            try
            {
                var svc = new NearbyPlaceService(_conn);
                DataTable places = svc.GetPlaces();
                if (places == null || places.Rows.Count == 0) return;

                MapJson = svc.BuildMapJson();
                HasMap = MapJson.IndexOf("\"lat\"", StringComparison.Ordinal) >= 0;

                sb.Append("<div class='act-section' data-nearby='1' style='margin-bottom:38px;'>");
                sb.Append("<h2 style='color:#2e5d3a;font-weight:700;margin-bottom:20px;font-size:1.5em;'>"
                        + "<i class='fas fa-map-location-dot'></i> สถานที่แนะนำใกล้เคียง "
                        + "<small style='color:#90a096;font-weight:400;font-size:.62em;'>("
                        + places.Rows.Count + " แห่ง)</small></h2>");

                if (HasMap)
                {
                    sb.Append("<div class='nb-map-wrap' data-mapsection='1'>");
                    sb.Append("<div id='publicNearbyMap'></div>");
                    sb.Append("</div>");
                    sb.Append("<p class='nb-map-hint' data-mapsection='1'>"
                            + "<i class='fas fa-hand-pointer'></i> แตะที่หมุดเพื่อดูรายละเอียดและกดนำทาง</p>");
                }

                // จัดกลุ่มตามประเภท — หัวข้อกลุ่มเฉพาะที่มีของจริง
                var cats = svc.GetCategories();
                var seen = new List<string>();
                foreach (DataRow c in cats.Rows)
                {
                    string code = Val(c, "Code");
                    var rows = places.Select("Category = '" + code.Replace("'", "''") + "'");
                    if (rows.Length == 0) continue;
                    seen.Add(code);
                    RenderNearbyGroup(sb, Val(c, "Icon"), Val(c, "Name"), rows);
                }
                // ประเภทที่ไม่มีในตารางประเภท (ข้อมูลเก่า) — รวมไว้ท้ายสุด ไม่ให้ตกหล่น
                var leftovers = new List<DataRow>();
                foreach (DataRow r in places.Rows)
                    if (!seen.Contains(Val(r, "Category"))) leftovers.Add(r);
                if (leftovers.Count > 0)
                    RenderNearbyGroup(sb, "📍", "อื่น ๆ", leftovers.ToArray());

                sb.Append("</div>");
            }
            catch (Exception ex)
            {
                // ส่วนสถานที่ล้มไม่ควรทำให้หน้ากิจกรรมทั้งหน้าพัง (เช่น ยังไม่ได้รันไมเกรชัน)
                System.Diagnostics.Trace.TraceError("Nearby section error: " + ex.Message);
            }
        }

        /// <summary>การ์ดสถานที่หนึ่งกลุ่มประเภท พร้อมหัวข้อกลุ่ม</summary>
        private void RenderNearbyGroup(StringBuilder sb, string groupIcon, string title, DataRow[] rows)
        {
            sb.Append("<div class='nb-group'>");
            sb.Append("<h3 class='nb-group-title'><span>" + Server.HtmlEncode(string.IsNullOrWhiteSpace(groupIcon) ? "📍" : groupIcon)
                    + "</span> " + Server.HtmlEncode(title)
                    + " <small>(" + rows.Length + ")</small></h3>");
            sb.Append("<div class='act-grid'>");
            {
                foreach (DataRow r in rows)
                {
                    string name = Val(r, "Name");
                    string desc = Val(r, "Description");
                    if (desc.Length > 165) desc = desc.Substring(0, 165) + "...";
                    string img = Val(r, "Image_Path");
                    string icon = Val(r, "Icon");
                    if (string.IsNullOrWhiteSpace(icon)) icon = Val(r, "CategoryIcon");
                    string catName = Val(r, "CategoryName");
                    string dist = Val(r, "Distance");
                    string time = Val(r, "Travel_Time");
                    string hours = Val(r, "Open_Hours");
                    string phone = Val(r, "Phone");
                    string nav = NavLink(r);

                    sb.Append("<div class='act-card' data-cat='NEARBY'>");

                    if (!string.IsNullOrWhiteSpace(img))
                        sb.Append("<div class='act-thumb' style=\"background-image:url('"
                                + Server.HtmlEncode(img) + "')\">");
                    else
                        sb.Append("<div class='act-thumb'><span style='font-size:3em;'>"
                                + Server.HtmlEncode(string.IsNullOrWhiteSpace(icon) ? "📍" : icon) + "</span>");

                    string badge = Val(r, "Badge_Text");
                    string badgeColor = Val(r, "Badge_Color");
                    if (string.IsNullOrWhiteSpace(badgeColor)) badgeColor = "#e67e22";
                    bool featured = ToBool(r, "Is_Featured");

                    sb.Append("<div class='act-badges'>");
                    if (!string.IsNullOrWhiteSpace(badge))
                        sb.Append("<span class='act-badge' style='background:" + Server.HtmlEncode(badgeColor) + "'>"
                                + Server.HtmlEncode(badge) + "</span>");
                    if (featured)
                        sb.Append("<span class='act-badge badge-featured'>⭐ แนะนำ</span>");
                    if (string.IsNullOrWhiteSpace(badge) && !featured && !string.IsNullOrWhiteSpace(catName))
                        sb.Append("<span class='act-badge badge-nearby'>" + Server.HtmlEncode(catName) + "</span>");
                    sb.Append("</div>");
                    sb.Append("</div>");

                    sb.Append("<div class='act-body'>");
                    sb.Append("<h3>" + Server.HtmlEncode(name) + "</h3>");
                    // ข้อความโปรโมท "ที่นี่ดียังไง" — เด่นกว่าคำอธิบายทั่วไป
                    string highlight = Val(r, "Highlight");
                    if (!string.IsNullOrWhiteSpace(highlight))
                        sb.Append("<div class='nb-highlight'>💡 " + Server.HtmlEncode(highlight) + "</div>");
                    if (!string.IsNullOrWhiteSpace(desc))
                        sb.Append("<div class='act-desc'>" + Server.HtmlEncode(desc) + "</div>");

                    if (!string.IsNullOrWhiteSpace(dist) || !string.IsNullOrWhiteSpace(time)
                        || !string.IsNullOrWhiteSpace(hours) || !string.IsNullOrWhiteSpace(Val(r, "Price_Range")))
                    {
                        sb.Append("<div class='act-meta'>");
                        if (!string.IsNullOrWhiteSpace(dist))
                            sb.Append("<span><i class='fas fa-location-dot'></i>" + Server.HtmlEncode(dist) + "</span>");
                        if (!string.IsNullOrWhiteSpace(time))
                            sb.Append("<span><i class='fas fa-clock'></i>" + Server.HtmlEncode(time) + "</span>");
                        if (!string.IsNullOrWhiteSpace(hours))
                            sb.Append("<span><i class='fas fa-door-open'></i>" + Server.HtmlEncode(hours) + "</span>");
                        string priceRange = Val(r, "Price_Range");
                        if (!string.IsNullOrWhiteSpace(priceRange))
                            sb.Append("<span><i class='fas fa-coins'></i>" + Server.HtmlEncode(priceRange) + "</span>");
                        sb.Append("</div>");
                    }

                    sb.Append("<div class='nb-actions'>");
                    if (!string.IsNullOrWhiteSpace(nav))
                        sb.Append("<a class='nb-btn nb-btn-nav' target='_blank' rel='noopener' href='"
                                + Server.HtmlEncode(nav) + "'><i class='fas fa-diamond-turn-right'></i> นำทาง</a>");
                    if (!string.IsNullOrWhiteSpace(phone))
                        sb.Append("<a class='nb-btn nb-btn-call' href='tel:" + Server.HtmlEncode(phone)
                                + "'><i class='fas fa-phone'></i> โทร</a>");
                    sb.Append("</div>");

                    sb.Append("</div></div>");
                }
            }
            sb.Append("</div></div>");
        }

        /// <summary>ลิงก์นำทาง — ใช้ลิงก์ที่ผู้ดูแลใส่เองก่อน ไม่มีก็สร้างจากพิกัด</summary>
        private static string NavLink(DataRow r)
        {
            string url = Val(r, "Map_Url");
            if (!string.IsNullOrWhiteSpace(url)) return url;
            double la, ln;
            if (!double.TryParse(Val(r, "Latitude"), out la)) return "";
            if (!double.TryParse(Val(r, "Longitude"), out ln)) return "";
            if (la == 0 && ln == 0) return "";
            return NearbyPlaceService.BuildNavUrl(la, ln, "", null);
        }

        private static string PriceSuffix(string mode)
        {
            switch (mode)
            {
                case "PER_HOUR": return " / ชั่วโมง";
                case "PER_PERSON": return " / คน";
                default: return "";
            }
        }

        private static string Val(DataRow r, string col)
        {
            return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? r[col].ToString() : "";
        }

        private static bool ToBool(DataRow r, string col)
        {
            if (!r.Table.Columns.Contains(col) || r[col] == DBNull.Value) return false;
            if (r[col] is bool b) return b;
            string s = r[col].ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
