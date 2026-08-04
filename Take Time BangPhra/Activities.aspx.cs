using System;
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

        protected void Page_Load(object sender, EventArgs e)
        {
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
                    litActivities.Text =
                        "<div class='act-empty'><i class='fas fa-calendar-xmark'></i>" +
                        "<h4>ยังไม่มีข้อมูลกิจกรรม</h4><p>กรุณาติดต่อเจ้าหน้าที่เพื่อสอบถามรายละเอียด</p></div>";
                    return;
                }

                RenderSection(sb, dt, "ON_PROPERTY", "กิจกรรมในที่พัก", "fa-tree");
                RenderSection(sb, dt, "OFF_PROPERTY", "สถานที่ท่องเที่ยวใกล้เคียง", "fa-map-location-dot");
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
