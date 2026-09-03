using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// สถานที่แนะนำใกล้เคียง — ข้อมูลสำหรับทั้งหน้าแขก (แผนที่ + รายการ) และหน้าจัดการ
    ///
    /// ต่อยอดจากตาราง Guest_NearbyPlaces เดิม (เคยเก็บแค่ชื่อ/คำอธิบาย/ระยะทาง/ลิงก์แผนที่)
    /// PHASE19_01 เพิ่ม พิกัด/รูป/รูปแบบหมุด/โซน และย้าย "ประเภทสถานที่" จาก hard-code
    /// ในโค้ดมาเป็นตาราง Guest_NearbyPlace_Category
    ///
    /// **ทนต่อฐานข้อมูลที่ยังไม่ได้รันไมเกรชัน**: ทุก query ตรวจคอลัมน์/ตารางก่อนใช้
    /// ถ้ายังไม่มีจะถอยไปใช้สคีมาเดิม → หน้าเว็บยังแสดงรายการได้ ไม่ขาวทั้งหน้า
    /// (ตอน deploy จริงมักอัป DLL ก่อนรันไมเกรชัน — ช่วงคาบเกี่ยวนั้นต้องไม่พัง)
    /// </summary>
    public class NearbyPlaceService
    {
        private readonly string _conn;
        private readonly code _code = new code();

        public NearbyPlaceService(string connectionString)
        {
            _conn = connectionString;
        }

        // ── ตรวจสคีมา (cache ต่ออายุ process — ไมเกรชันรันครั้งเดียว) ────────────
        private static bool? _hasMapColumns;
        private static bool? _hasPromoColumns;
        private static bool? _hasCategoryTable;
        private static bool? _hasZoneTable;

        /// <summary>รันไมเกรชัน PHASE19_01 แล้วหรือยัง (มีคอลัมน์พิกัด)</summary>
        public bool HasMapColumns
        {
            get
            {
                if (_hasMapColumns.HasValue) return _hasMapColumns.Value;
                _hasMapColumns = ColumnExists("Guest_NearbyPlaces", "Latitude");
                return _hasMapColumns.Value;
            }
        }

        /// <summary>รันไมเกรชัน PHASE19_03 แล้วหรือยัง (ข้อความโปรโมท/ป้าย/ปักหมุด)</summary>
        public bool HasPromoColumns
        {
            get
            {
                if (_hasPromoColumns.HasValue) return _hasPromoColumns.Value;
                _hasPromoColumns = ColumnExists("Guest_NearbyPlaces", "Highlight");
                return _hasPromoColumns.Value;
            }
        }

        public bool HasCategoryTable
        {
            get
            {
                if (_hasCategoryTable.HasValue) return _hasCategoryTable.Value;
                _hasCategoryTable = TableExists("Guest_NearbyPlace_Category");
                return _hasCategoryTable.Value;
            }
        }

        public bool HasZoneTable
        {
            get
            {
                if (_hasZoneTable.HasValue) return _hasZoneTable.Value;
                _hasZoneTable = TableExists("Guest_NearbyZone");
                return _hasZoneTable.Value;
            }
        }

        /// <summary>ล้าง cache สคีมา — เรียกหลังรันไมเกรชันโดยไม่ต้อง restart เว็บ</summary>
        public static void ResetSchemaCache()
        {
            _hasMapColumns = null;
            _hasPromoColumns = null;
            _hasCategoryTable = null;
            _hasZoneTable = null;
        }

        private bool TableExists(string table)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @t",
                    new Dictionary<string, object> { { "@t", table } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        private bool ColumnExists(string table, string column)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t AND COLUMN_NAME = @c",
                    new Dictionary<string, object> { { "@t", table }, { "@c", column } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        // ── ประเภทสถานที่ ────────────────────────────────────────────────────────

        /// <summary>ประเภททั้งหมด (เปิดใช้งาน). ถ้ายังไม่มีตาราง → คืนชุดเดิมที่เคย hard-code ไว้</summary>
        public DataTable GetCategories(bool activeOnly = true)
        {
            if (HasCategoryTable)
            {
                try
                {
                    string where = activeOnly ? "WHERE Status = 'True'" : "";
                    return _code.DatabaseQuerySafe(_conn,
                        $@"SELECT ID, Code, Name, Icon, Marker_Color, Sort_Order, Status
                           FROM Guest_NearbyPlace_Category {where}
                           ORDER BY Sort_Order, Name", null);
                }
                catch { }
            }
            return LegacyCategories();
        }

        /// <summary>ชุดประเภทเดิมที่เคยฝังในโค้ด — ใช้เมื่อยังไม่ได้รันไมเกรชัน</summary>
        private static DataTable LegacyCategories()
        {
            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(int));
            dt.Columns.Add("Code", typeof(string));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Icon", typeof(string));
            dt.Columns.Add("Marker_Color", typeof(string));
            dt.Columns.Add("Sort_Order", typeof(int));
            dt.Columns.Add("Status", typeof(string));
            AddLegacy(dt, 1, "beach", "ชายหาด", "🏖️", "#0288D1", 1);
            AddLegacy(dt, 2, "restaurant", "ร้านอาหาร", "🍽️", "#E64A19", 2);
            AddLegacy(dt, 3, "cafe", "คาเฟ่", "☕", "#6D4C41", 3);
            AddLegacy(dt, 4, "attraction", "สถานที่ท่องเที่ยว", "🎯", "#7B1FA2", 4);
            AddLegacy(dt, 5, "shopping", "ช้อปปิ้ง", "🛒", "#00897B", 5);
            return dt;
        }

        private static void AddLegacy(DataTable dt, int id, string code, string name, string icon, string color, int ord)
        {
            var r = dt.NewRow();
            r["ID"] = id; r["Code"] = code; r["Name"] = name;
            r["Icon"] = icon; r["Marker_Color"] = color; r["Sort_Order"] = ord; r["Status"] = "True";
            dt.Rows.Add(r);
        }

        public bool SaveCategory(int id, string code, string name, string icon, string color, int sortOrder, bool active)
        {
            if (!HasCategoryTable) return false;
            var p = new Dictionary<string, object>
            {
                { "@code", (code ?? "").Trim() },
                { "@name", (name ?? "").Trim() },
                { "@icon", icon ?? "" },
                { "@color", color ?? "" },
                { "@ord", sortOrder },
                { "@st", active ? "True" : "False" }
            };
            if (id > 0)
            {
                p.Add("@id", id);
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Guest_NearbyPlace_Category
                      SET Code = @code, Name = @name, Icon = @icon,
                          Marker_Color = @color, Sort_Order = @ord, Status = @st
                      WHERE ID = @id", p);
            }
            else
            {
                _code.DatabaseInsertSafe(_conn,
                    @"INSERT INTO Guest_NearbyPlace_Category (Code, Name, Icon, Marker_Color, Sort_Order, Status)
                      VALUES (@code, @name, @icon, @color, @ord, @st)", p);
            }
            return true;
        }

        /// <summary>ลบประเภท — กันลบทิ้งทั้งที่ยังมีสถานที่ผูกอยู่ (สถานที่จะหายจากตัวกรอง)</summary>
        public (bool Ok, string Message) DeleteCategory(int id)
        {
            if (!HasCategoryTable) return (false, "ยังไม่ได้รันไมเกรชัน PHASE19_01");
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT COUNT(*) AS N FROM Guest_NearbyPlaces p
                      JOIN Guest_NearbyPlace_Category c ON c.Code = p.Category
                      WHERE c.ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                int used = dt?.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["N"]) : 0;
                if (used > 0)
                    return (false, $"ลบไม่ได้ — ยังมีสถานที่ {used} แห่งใช้ประเภทนี้อยู่ "
                                 + "(ย้ายสถานที่ไปประเภทอื่นก่อน หรือปิดใช้งานประเภทนี้แทน)");
                _code.DatabaseInsertSafe(_conn, "DELETE FROM Guest_NearbyPlace_Category WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                return (true, "ลบประเภทแล้ว");
            }
            catch (Exception ex) { return (false, "ลบไม่สำเร็จ: " + ex.Message); }
        }

        // ── โซน / ขอบเขตพื้นที่ ──────────────────────────────────────────────────

        public DataTable GetZones(bool activeOnly = true)
        {
            if (!HasZoneTable) return new DataTable();
            try
            {
                string where = activeOnly ? "WHERE Status = 'True'" : "";
                return _code.DatabaseQuerySafe(_conn,
                    $@"SELECT ID, Name, Boundary_GeoJson, Center_Lat, Center_Lng, Default_Zoom,
                              Fill_Color, Line_Color, Is_Default, Sort_Order, Status
                       FROM Guest_NearbyZone {where}
                       ORDER BY Is_Default DESC, Sort_Order, Name", null);
            }
            catch { return new DataTable(); }
        }

        /// <summary>โซนที่จะเปิดให้เห็นก่อน (Is_Default ก่อน แล้วค่อยตัวแรกสุด)</summary>
        public DataRow GetDefaultZone()
        {
            var dt = GetZones();
            return dt != null && dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public bool SaveZone(int id, string name, string geoJson, decimal? lat, decimal? lng,
                             int zoom, string fill, string line, bool isDefault, int sortOrder, bool active)
        {
            if (!HasZoneTable) return false;
            var p = new Dictionary<string, object>
            {
                { "@name", (name ?? "").Trim() },
                { "@geo", (object)geoJson ?? DBNull.Value },
                { "@lat", lat.HasValue ? (object)lat.Value : DBNull.Value },
                { "@lng", lng.HasValue ? (object)lng.Value : DBNull.Value },
                { "@zoom", zoom <= 0 ? 12 : zoom },
                { "@fill", string.IsNullOrWhiteSpace(fill) ? "#00b09b" : fill },
                { "@line", string.IsNullOrWhiteSpace(line) ? "#00796B" : line },
                { "@def", isDefault ? 1 : 0 },
                { "@ord", sortOrder },
                { "@st", active ? "True" : "False" }
            };
            if (id > 0)
            {
                p.Add("@id", id);
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Guest_NearbyZone
                      SET Name = @name, Boundary_GeoJson = @geo, Center_Lat = @lat, Center_Lng = @lng,
                          Default_Zoom = @zoom, Fill_Color = @fill, Line_Color = @line,
                          Is_Default = @def, Sort_Order = @ord, Status = @st
                      WHERE ID = @id", p);
            }
            else
            {
                _code.DatabaseInsertSafe(_conn,
                    @"INSERT INTO Guest_NearbyZone (Name, Boundary_GeoJson, Center_Lat, Center_Lng,
                                                    Default_Zoom, Fill_Color, Line_Color, Is_Default, Sort_Order, Status)
                      VALUES (@name, @geo, @lat, @lng, @zoom, @fill, @line, @def, @ord, @st)", p);
            }
            // มีโซน default ได้ตัวเดียว
            if (isDefault)
            {
                try
                {
                    _code.DatabaseInsertSafe(_conn,
                        id > 0 ? "UPDATE Guest_NearbyZone SET Is_Default = 0 WHERE ID <> @id"
                               : "UPDATE Guest_NearbyZone SET Is_Default = 0 WHERE ID <> (SELECT MAX(ID) FROM Guest_NearbyZone)",
                        id > 0 ? new Dictionary<string, object> { { "@id", id } } : null);
                }
                catch { }
            }
            return true;
        }

        public bool DeleteZone(int id)
        {
            if (!HasZoneTable) return false;
            try
            {
                // ปลดสถานที่ออกจากโซนก่อน ไม่ให้ชี้ไปโซนที่ไม่มีแล้ว
                if (HasMapColumns)
                    _code.DatabaseInsertSafe(_conn,
                        "UPDATE Guest_NearbyPlaces SET Zone_ID = NULL WHERE Zone_ID = @id",
                        new Dictionary<string, object> { { "@id", id } });
                _code.DatabaseInsertSafe(_conn, "DELETE FROM Guest_NearbyZone WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                return true;
            }
            catch { return false; }
        }

        // ── สถานที่ ──────────────────────────────────────────────────────────────

        /// <summary>
        /// รายการสถานที่พร้อมข้อมูลประเภท. คืนคอลัมน์ครบเสมอไม่ว่าฐานข้อมูลจะรันไมเกรชันหรือยัง
        /// (ยังไม่รัน → คอลัมน์ใหม่เป็น NULL) เพื่อให้หน้าเว็บเขียนโค้ดชุดเดียว
        /// </summary>
        public DataTable GetPlaces(string categoryCode = null, int zoneId = 0, bool activeOnly = true)
        {
            var p = new Dictionary<string, object>();
            var sb = new StringBuilder();
            sb.Append("SELECT p.ID, p.Category, p.Name, p.Description, p.Distance, p.Travel_Time, ");
            sb.Append("p.Map_Url, p.Phone, p.Icon, p.Sort_Order, p.Status, ");
            if (HasMapColumns)
                sb.Append(@"p.Latitude, p.Longitude, p.Image_Path, p.Marker_Color, p.Marker_Icon,
                            p.Marker_Image, p.Zone_ID, p.Address, p.Open_Hours, ");
            else
                sb.Append(@"CAST(NULL AS DECIMAL(9,6)) AS Latitude, CAST(NULL AS DECIMAL(9,6)) AS Longitude,
                            CAST(NULL AS NVARCHAR(500)) AS Image_Path, CAST(NULL AS NVARCHAR(20)) AS Marker_Color,
                            CAST(NULL AS NVARCHAR(50)) AS Marker_Icon, CAST(NULL AS NVARCHAR(500)) AS Marker_Image,
                            CAST(NULL AS INT) AS Zone_ID, CAST(NULL AS NVARCHAR(300)) AS Address,
                            CAST(NULL AS NVARCHAR(100)) AS Open_Hours, ");

            if (HasPromoColumns)
                sb.Append("p.Highlight, p.Badge_Text, p.Badge_Color, p.Is_Featured, p.Price_Range, ");
            else
                sb.Append(@"CAST(NULL AS NVARCHAR(300)) AS Highlight, CAST(NULL AS NVARCHAR(50)) AS Badge_Text,
                            CAST(NULL AS NVARCHAR(20)) AS Badge_Color, CAST(0 AS BIT) AS Is_Featured,
                            CAST(NULL AS NVARCHAR(10)) AS Price_Range, ");

            if (HasCategoryTable)
                sb.Append(@"ISNULL(c.Name, p.Category) AS CategoryName,
                            ISNULL(c.Icon, N'📍') AS CategoryIcon,
                            ISNULL(c.Marker_Color, '#1976D2') AS CategoryColor
                        FROM Guest_NearbyPlaces p
                        LEFT JOIN Guest_NearbyPlace_Category c ON c.Code = p.Category ");
            else
                sb.Append(@"p.Category AS CategoryName, N'📍' AS CategoryIcon, '#1976D2' AS CategoryColor
                        FROM Guest_NearbyPlaces p ");

            sb.Append("WHERE 1 = 1 ");
            if (activeOnly) sb.Append("AND p.Status = 'True' ");
            if (!string.IsNullOrWhiteSpace(categoryCode))
            {
                sb.Append("AND p.Category = @cat ");
                p.Add("@cat", categoryCode);
            }
            if (zoneId > 0 && HasMapColumns)
            {
                sb.Append("AND p.Zone_ID = @zone ");
                p.Add("@zone", zoneId);
            }
            // ปักหมุดแนะนำขึ้นก่อนเสมอ แล้วค่อยเรียงตามลำดับประเภทและลำดับที่ตั้งไว้
            sb.Append(HasPromoColumns
                ? (HasCategoryTable
                    ? "ORDER BY p.Is_Featured DESC, ISNULL(c.Sort_Order, 99), p.Sort_Order, p.Name"
                    : "ORDER BY p.Is_Featured DESC, p.Sort_Order, p.Name")
                : (HasCategoryTable
                    ? "ORDER BY ISNULL(c.Sort_Order, 99), p.Sort_Order, p.Name"
                    : "ORDER BY p.Sort_Order, p.Name"));

            try { return _code.DatabaseQuerySafe(_conn, sb.ToString(), p.Count > 0 ? p : null); }
            catch { return new DataTable(); }
        }

        public DataRow GetPlace(int id)
        {
            try
            {
                var dt = GetPlaces(null, 0, false);
                foreach (DataRow r in dt.Rows)
                    if (Convert.ToInt32(r["ID"]) == id) return r;
            }
            catch { }
            return null;
        }

        /// <summary>บันทึกสถานที่ (id = 0 → เพิ่มใหม่). คืน ID ที่บันทึก</summary>
        public int SavePlace(NearbyPlaceInput input)
        {
            if (input == null) return 0;

            var p = new Dictionary<string, object>
            {
                { "@cat", (input.Category ?? "").Trim() },
                { "@name", (input.Name ?? "").Trim() },
                { "@desc", (object)input.Description ?? DBNull.Value },
                { "@dist", (object)input.Distance ?? DBNull.Value },
                { "@time", (object)input.TravelTime ?? DBNull.Value },
                { "@map", (object)input.MapUrl ?? DBNull.Value },
                { "@phone", (object)input.Phone ?? DBNull.Value },
                { "@icon", (object)input.Icon ?? DBNull.Value },
                { "@ord", input.SortOrder },
                { "@st", input.Active ? "True" : "False" }
            };

            string setMap = "", colMap = "", valMap = "";
            if (HasMapColumns)
            {
                p.Add("@lat", input.Latitude.HasValue ? (object)input.Latitude.Value : DBNull.Value);
                p.Add("@lng", input.Longitude.HasValue ? (object)input.Longitude.Value : DBNull.Value);
                p.Add("@img", (object)input.ImagePath ?? DBNull.Value);
                p.Add("@mcolor", (object)input.MarkerColor ?? DBNull.Value);
                p.Add("@micon", (object)input.MarkerIcon ?? DBNull.Value);
                p.Add("@mimg", (object)input.MarkerImage ?? DBNull.Value);
                p.Add("@zone", input.ZoneId > 0 ? (object)input.ZoneId : DBNull.Value);
                p.Add("@addr", (object)input.Address ?? DBNull.Value);
                p.Add("@open", (object)input.OpenHours ?? DBNull.Value);
                setMap = @", Latitude = @lat, Longitude = @lng, Image_Path = @img,
                            Marker_Color = @mcolor, Marker_Icon = @micon, Marker_Image = @mimg,
                            Zone_ID = @zone, Address = @addr, Open_Hours = @open";
                colMap = ", Latitude, Longitude, Image_Path, Marker_Color, Marker_Icon, Marker_Image, Zone_ID, Address, Open_Hours";
                valMap = ", @lat, @lng, @img, @mcolor, @micon, @mimg, @zone, @addr, @open";
            }

            if (HasPromoColumns)
            {
                p.Add("@hl", (object)input.Highlight ?? DBNull.Value);
                p.Add("@btext", (object)input.BadgeText ?? DBNull.Value);
                p.Add("@bcolor", (object)input.BadgeColor ?? DBNull.Value);
                p.Add("@feat", input.IsFeatured ? 1 : 0);
                p.Add("@prange", (object)input.PriceRange ?? DBNull.Value);
                setMap += @", Highlight = @hl, Badge_Text = @btext, Badge_Color = @bcolor,
                             Is_Featured = @feat, Price_Range = @prange";
                colMap += ", Highlight, Badge_Text, Badge_Color, Is_Featured, Price_Range";
                valMap += ", @hl, @btext, @bcolor, @feat, @prange";
            }

            if (input.Id > 0)
            {
                p.Add("@id", input.Id);
                _code.DatabaseInsertSafe(_conn,
                    $@"UPDATE Guest_NearbyPlaces
                       SET Category = @cat, Name = @name, Description = @desc, Distance = @dist,
                           Travel_Time = @time, Map_Url = @map, Phone = @phone, Icon = @icon,
                           Sort_Order = @ord, Status = @st{setMap}
                       WHERE ID = @id", p);
                return input.Id;
            }

            _code.DatabaseInsertSafe(_conn,
                $@"INSERT INTO Guest_NearbyPlaces
                   (Category, Name, Description, Distance, Travel_Time, Map_Url, Phone, Icon, Sort_Order, Status{colMap})
                   VALUES (@cat, @name, @desc, @dist, @time, @map, @phone, @icon, @ord, @st{valMap})", p);

            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ID FROM Guest_NearbyPlaces ORDER BY ID DESC", null);
                return dt?.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["ID"]) : 0;
            }
            catch { return 0; }
        }

        public bool DeletePlace(int id)
        {
            try
            {
                _code.DatabaseInsertSafe(_conn, "DELETE FROM Guest_NearbyPlaces WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                return true;
            }
            catch { return false; }
        }

        // ── ข้อมูลสำหรับวาดแผนที่ ────────────────────────────────────────────────

        /// <summary>
        /// สร้าง JSON ก้อนเดียวให้หน้าเว็บเอาไปวาดแผนที่ — หมุด + ขอบเขต + ค่าเริ่มต้นของมุมมอง
        /// รวมลิงก์นำทาง Google Maps ให้เสร็จจากฝั่งเซิร์ฟเวอร์ (ไม่ต้องใช้ API key)
        /// </summary>
        public string BuildMapJson(int zoneId = 0, string categoryCode = null)
        {
            var places = new List<Dictionary<string, object>>();
            var dt = GetPlaces(categoryCode, zoneId);

            foreach (DataRow r in dt.Rows)
            {
                double lat, lng;
                if (!TryGetDouble(r, "Latitude", out lat) || !TryGetDouble(r, "Longitude", out lng))
                    continue;                       // ยังไม่ได้ใส่พิกัด → ไม่ขึ้นหมุด (ยังอยู่ในรายการด้านล่าง)
                if (lat == 0 && lng == 0) continue;

                string color = Str(r, "Marker_Color");
                if (string.IsNullOrWhiteSpace(color)) color = Str(r, "CategoryColor");
                if (string.IsNullOrWhiteSpace(color)) color = "#1976D2";

                string icon = Str(r, "Marker_Icon");
                if (string.IsNullOrWhiteSpace(icon)) icon = Str(r, "Icon");
                if (string.IsNullOrWhiteSpace(icon)) icon = Str(r, "CategoryIcon");
                if (string.IsNullOrWhiteSpace(icon)) icon = "📍";

                places.Add(new Dictionary<string, object>
                {
                    { "id", Convert.ToInt32(r["ID"]) },
                    { "name", Str(r, "Name") },
                    { "desc", Str(r, "Description") },
                    { "cat", Str(r, "Category") },
                    { "catName", Str(r, "CategoryName") },
                    { "lat", lat },
                    { "lng", lng },
                    { "img", Str(r, "Image_Path") },
                    { "markerImg", Str(r, "Marker_Image") },
                    { "icon", icon },
                    { "color", color },
                    { "addr", Str(r, "Address") },
                    { "phone", Str(r, "Phone") },
                    { "hours", Str(r, "Open_Hours") },
                    { "dist", Str(r, "Distance") },
                    { "time", Str(r, "Travel_Time") },
                    { "highlight", Str(r, "Highlight") },
                    { "badge", Str(r, "Badge_Text") },
                    { "badgeColor", string.IsNullOrWhiteSpace(Str(r, "Badge_Color")) ? "#e67e22" : Str(r, "Badge_Color") },
                    { "priceRange", Str(r, "Price_Range") },
                    { "nav", BuildNavUrl(lat, lng, Str(r, "Name"), Str(r, "Map_Url")) }
                });
            }

            var map = new Dictionary<string, object> { { "places", places } };

            DataRow zone = null;
            if (zoneId > 0)
            {
                var zones = GetZones();
                foreach (DataRow z in zones.Rows)
                    if (Convert.ToInt32(z["ID"]) == zoneId) { zone = z; break; }
            }
            if (zone == null) zone = GetDefaultZone();

            if (zone != null)
            {
                double zlat, zlng;
                map["zone"] = new Dictionary<string, object>
                {
                    { "id", Convert.ToInt32(zone["ID"]) },
                    { "name", Str(zone, "Name") },
                    { "geojson", Str(zone, "Boundary_GeoJson") },
                    { "lat", TryGetDouble(zone, "Center_Lat", out zlat) ? zlat : 13.1748 },
                    { "lng", TryGetDouble(zone, "Center_Lng", out zlng) ? zlng : 100.9306 },
                    { "zoom", zone.Table.Columns.Contains("Default_Zoom") && zone["Default_Zoom"] != DBNull.Value
                              ? Convert.ToInt32(zone["Default_Zoom"]) : 12 },
                    { "fill", string.IsNullOrWhiteSpace(Str(zone, "Fill_Color")) ? "#00b09b" : Str(zone, "Fill_Color") },
                    { "line", string.IsNullOrWhiteSpace(Str(zone, "Line_Color")) ? "#00796B" : Str(zone, "Line_Color") }
                };
            }

            var cats = new List<Dictionary<string, object>>();
            foreach (DataRow c in GetCategories().Rows)
                cats.Add(new Dictionary<string, object>
                {
                    { "code", Str(c, "Code") },
                    { "name", Str(c, "Name") },
                    { "icon", Str(c, "Icon") },
                    { "color", Str(c, "Marker_Color") }
                });
            map["categories"] = cats;

            try { return new JavaScriptSerializer().Serialize(map); }
            catch { return "{\"places\":[],\"categories\":[]}"; }
        }

        /// <summary>
        /// ลิงก์นำทาง — ใช้พิกัดเป็นหลักเพราะแม่นกว่าชื่อ และไม่ต้องใช้ API key
        /// ถ้าผู้ใช้ใส่ลิงก์แผนที่เองไว้ (ของเดิม) ให้ลิงก์นั้นชนะ เพราะอาจเป็นลิงก์ร้านที่ถูกต้องกว่า
        /// </summary>
        public static string BuildNavUrl(double lat, double lng, string name, string existingUrl)
        {
            if (!string.IsNullOrWhiteSpace(existingUrl)) return existingUrl.Trim();
            // api=1 เป็นรูปแบบทางการของ Google Maps — เปิดได้ทั้งเว็บและเด้งเข้าแอปบนมือถือ
            // ส่งพิกัดตรง ๆ (ไม่ escape ลูกน้ำ) เพราะแม่นกว่าค้นด้วยชื่อ และไม่ต้องใช้ API key
            return "https://www.google.com/maps/dir/?api=1&destination="
                   + lat.ToString("0.######", CultureInfo.InvariantCulture) + ","
                   + lng.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string Str(DataRow r, string col)
        {
            if (r == null || !r.Table.Columns.Contains(col) || r[col] == DBNull.Value) return "";
            return r[col].ToString();
        }

        private static bool TryGetDouble(DataRow r, string col, out double v)
        {
            v = 0;
            if (r == null || !r.Table.Columns.Contains(col) || r[col] == DBNull.Value) return false;
            try { v = Convert.ToDouble(r[col], CultureInfo.InvariantCulture); return true; }
            catch { return false; }
        }
    }

    /// <summary>ข้อมูลนำเข้าสำหรับบันทึกสถานที่ — แยกเป็นคลาสเพื่อไม่ต้องส่งพารามิเตอร์ยาวเหยียด</summary>
    public class NearbyPlaceInput
    {
        public int Id;
        public string Category, Name, Description, Distance, TravelTime, MapUrl, Phone, Icon;
        public int SortOrder;
        public bool Active = true;

        // ฟิลด์ที่เพิ่มใน PHASE19_01
        public decimal? Latitude, Longitude;
        public string ImagePath, MarkerColor, MarkerIcon, MarkerImage, Address, OpenHours;
        public int ZoneId;

        // ฟิลด์ที่เพิ่มใน PHASE19_03
        public string Highlight, BadgeText, BadgeColor, PriceRange;
        public bool IsFeatured;
    }
}
