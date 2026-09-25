using System;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    /// <summary>
    /// สถานที่แนะนำใกล้เคียง (ฝั่งแขก) — แผนที่ตามขอบเขตพื้นที่ + หมุดของแต่ละสถานที่ + รายการด้านล่าง
    ///
    /// เดิมหน้านี้ฝัง iframe Google Maps ที่ hard-code พิกัดไว้ในหน้า จึงเป็นแค่ "ภาพแผนที่"
    /// ไม่มีหมุด ไม่รู้จักสถานที่ในฐานข้อมูล และแก้พื้นที่ไม่ได้เลยถ้าย้ายสาขา
    /// ตอนนี้วาดด้วย Leaflet จากข้อมูลจริง: ขอบเขตโซน (GeoJSON) + หมุดตามพิกัดที่บันทึกไว้
    /// ปุ่มนำทางใช้ลิงก์ Google Maps แบบ api=1 (ไม่ต้องใช้ API key)
    /// </summary>
    public partial class NearbyPlaces : Page
    {
        private readonly string _connectionString =
            System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private NearbyPlaceService _svc;
        private code _code;

        protected DataTable DtPlaces;
        protected DataTable DtCategories;

        /// <summary>JSON สำหรับวาดแผนที่ (หมุด + ขอบเขต + ประเภท) — ฝังลงหน้าเว็บตรง ๆ</summary>
        protected string MapJson = "{\"places\":[],\"categories\":[]}";

        /// <summary>มีสถานที่ที่ใส่พิกัดแล้วอย่างน้อยหนึ่งแห่งไหม — ไม่มีก็ไม่ต้องโชว์แผนที่เปล่า</summary>
        protected bool HasMapPoints;

        protected void Page_Load(object sender, EventArgs e)
        {
            _guestPortalService = new GuestPortalService(_connectionString);
            _svc = new NearbyPlaceService(_connectionString);
            _code = new code();

            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
            }

            if (!IsPostBack)
            {
                LoadPlaces();
            }
        }

        private void LoadPlaces()
        {
            try { DtCategories = _svc.GetCategories(); }
            catch { DtCategories = new DataTable(); }

            try { DtPlaces = _svc.GetPlaces(); }
            catch { DtPlaces = new DataTable(); }

            try
            {
                MapJson = _svc.BuildMapJson();
                // นับหมุดที่วาดได้จริง (มีพิกัด) — ต่างจากจำนวนสถานที่ทั้งหมด
                HasMapPoints = MapJson.IndexOf("\"lat\"", StringComparison.Ordinal) >= 0;
            }
            catch
            {
                MapJson = "{\"places\":[],\"categories\":[]}";
                HasMapPoints = false;
            }

            if (DtPlaces != null && DtPlaces.Rows.Count > 0)
            {
                rptGroups.DataSource = CategoriesInUse();
                rptGroups.DataBind();
                pnlNoData.Visible = false;
            }
            else
            {
                pnlNoData.Visible = true;
            }
        }

        /// <summary>ประเภทที่ "มีสถานที่จริง" เท่านั้น — ไม่โชว์หัวข้อกลุ่มว่าง ๆ</summary>
        protected DataTable CategoriesInUse()
        {
            var result = new DataTable();
            result.Columns.Add("Code", typeof(string));
            result.Columns.Add("Name", typeof(string));
            result.Columns.Add("Icon", typeof(string));
            result.Columns.Add("Count", typeof(int));
            if (DtCategories == null || DtPlaces == null) return result;

            foreach (DataRow c in DtCategories.Rows)
            {
                string code = c["Code"] == DBNull.Value ? "" : c["Code"].ToString();
                int n = PlacesIn(code).Length;
                if (n == 0) continue;
                var r = result.NewRow();
                r["Code"] = code;
                r["Name"] = c["Name"] == DBNull.Value ? code : c["Name"].ToString();
                r["Icon"] = c["Icon"] == DBNull.Value ? "📍" : c["Icon"].ToString();
                r["Count"] = n;
                result.Rows.Add(r);
            }
            return result;
        }

        /// <summary>สถานที่ในประเภทนั้น — ใช้เป็น DataSource ของ Repeater ชั้นใน</summary>
        protected DataRow[] PlacesIn(object categoryCode)
        {
            if (DtPlaces == null) return new DataRow[0];
            string code = categoryCode == null ? "" : categoryCode.ToString();
            return DtPlaces.Select("Category = '" + code.Replace("'", "''") + "'");
        }

        // ── helper สำหรับ markup ──────────────────────────────────────────────────

        /// <summary>รูปของสถานที่ — ไม่มีรูปให้คืนค่าว่าง (การ์ดจะแสดงไอคอนแทน)</summary>
        protected string PlaceImage(object imagePath)
        {
            string p = imagePath == null || imagePath == DBNull.Value ? "" : imagePath.ToString().Trim();
            return p;
        }

        /// <summary>ลิงก์นำทาง: ใช้ลิงก์ที่ผู้ดูแลใส่เองก่อน ไม่มีก็สร้างจากพิกัด</summary>
        protected string NavUrl(object mapUrl, object lat, object lng)
        {
            string existing = mapUrl == null || mapUrl == DBNull.Value ? "" : mapUrl.ToString().Trim();
            if (existing.Length > 0) return existing;
            double la, ln;
            if (lat == null || lat == DBNull.Value || lng == null || lng == DBNull.Value) return "";
            if (!double.TryParse(lat.ToString(), out la) || !double.TryParse(lng.ToString(), out ln)) return "";
            if (la == 0 && ln == 0) return "";
            return NearbyPlaceService.BuildNavUrl(la, ln, "", null);
        }

        protected string Esc(object v)
        {
            return v == null || v == DBNull.Value ? "" : Server.HtmlEncode(v.ToString());
        }

        protected bool IsFeatured(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            if (v is bool b) return b;
            string s = v.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>ป้ายมุมรูป — ป้ายที่ตั้งเอง และป้าย "แนะนำ" เมื่อปักหมุดไว้</summary>
        protected string BadgeHtml(object text, object color, object featured)
        {
            string html = "";
            string t = Esc(text);
            if (t.Length > 0)
            {
                string c = color == null || color == DBNull.Value ? "" : color.ToString().Trim();
                if (c.Length == 0) c = "#e67e22";
                html += "<span class='place-badge' style='background:" + Server.HtmlEncode(c) + "'>" + t + "</span>";
            }
            if (IsFeatured(featured))
                html += "<span class='place-badge feat'>⭐ แนะนำ</span>";
            return html;
        }

        /// <summary>ข้อความโปรโมท "ที่นี่ดียังไง" — ไม่ได้ใส่ก็ไม่ต้องเว้นที่ว่างไว้</summary>
        protected string HighlightHtml(object highlight)
        {
            string h = Esc(highlight);
            return h.Length == 0 ? "" : "<div class='place-highlight'>💡 " + h + "</div>";
        }

        /// <summary>บรรทัดข้อมูลย่อย — แสดงเฉพาะช่องที่กรอกไว้ ไม่โชว์ไอคอนลอย ๆ</summary>
        protected string MetaHtml(object distance, object travelTime, object hours, object priceRange)
        {
            var parts = new System.Collections.Generic.List<string>();
            string d = Esc(distance), t = Esc(travelTime), o = Esc(hours), pr = Esc(priceRange);
            if (d.Length > 0) parts.Add("<span class='distance'><i class='fas fa-map-marker-alt'></i> " + d + "</span>");
            if (t.Length > 0) parts.Add("<span class='travel-time'><i class='fas fa-clock'></i> " + t + "</span>");
            if (o.Length > 0) parts.Add("<span><i class='fas fa-door-open'></i> " + o + "</span>");
            if (pr.Length > 0) parts.Add("<span><i class='fas fa-coins'></i> " + pr + "</span>");
            return string.Join("", parts);
        }

        private bool ValidateGuestSession()
        {
            string sessionToken = Request.Cookies["GuestSession"]?.Value ?? Session["GuestSessionToken"]?.ToString();
            if (string.IsNullOrEmpty(sessionToken)) return false;
            try
            {
                DataTable dtSession = _guestPortalService.ValidateGuestSession(sessionToken);
                return dtSession != null && dtSession.Rows.Count > 0;
            }
            catch { return false; }
        }
    }
}
