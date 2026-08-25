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
                rptPlaces.DataSource = DtPlaces;
                rptPlaces.DataBind();
                pnlNoData.Visible = false;
            }
            else
            {
                pnlNoData.Visible = true;
            }
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
