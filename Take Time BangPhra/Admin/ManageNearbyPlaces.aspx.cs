using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin
{
    /// <summary>
    /// จัดการ "สถานที่แนะนำใกล้เคียง" — สถานที่ / ประเภท / โซนกับขอบเขตพื้นที่
    ///
    /// PHASE19_01 เพิ่มความสามารถที่หน้าเดิมทำไม่ได้:
    ///   • พิกัด — เลือกจากแผนที่ได้เลย หรือดึงจากลิงก์ Google Maps ที่วางมา
    ///   • รูปภาพสถานที่ + รูปหมุดแบบกำหนดเอง
    ///   • ประเภทสถานที่แก้เองได้ (เดิม hard-code 5 ชนิดในโค้ด)
    ///   • โซน + ขอบเขต GeoJSON สำหรับวาดรูปทรงพื้นที่บนแผนที่ฝั่งแขก
    ///
    /// ยังทำงานได้แม้ยังไม่รันไมเกรชัน — NearbyPlaceService ตรวจสคีมาให้ แล้วซ่อนส่วนที่ยังไม่มี
    /// </summary>
    public partial class ManageNearbyPlaces : Page
    {
        private readonly string conn =
            System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;
        private NearbyPlaceService _svc;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SysSettings)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            _code = new code();
            _svc = new NearbyPlaceService(conn);
            try
            {
                if (Session["permission"] == null || Session["permission"].ToString() != "True")
                {
                    Response.Redirect("/Default");
                    return;
                }
                EnsureTableExists();
                if (!IsPostBack)
                {
                    BindCategoryDropdowns();
                    BindZoneDropdown();
                    LoadList();
                }
            }
            catch
            {
                Response.Redirect("/Default");
            }
        }

        private void EnsureTableExists()
        {
            try
            {
                _code.DatabaseInsertSafe(conn,
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_NearbyPlaces')
                      CREATE TABLE Guest_NearbyPlaces (
                          ID INT IDENTITY(1,1) PRIMARY KEY,
                          Category NVARCHAR(50) NOT NULL,
                          Name NVARCHAR(200) NOT NULL,
                          Description NVARCHAR(500),
                          Distance NVARCHAR(50),
                          Travel_Time NVARCHAR(50),
                          Map_Url NVARCHAR(500),
                          Phone NVARCHAR(50),
                          Icon NVARCHAR(50),
                          Sort_Order INT DEFAULT 0,
                          Status NVARCHAR(10) DEFAULT 'True',
                          Created_Date DATETIME DEFAULT GETDATE()
                      )", null);
            }
            catch { }
        }

        // ── binding ───────────────────────────────────────────────────────────────

        private void BindCategoryDropdowns()
        {
            var dt = _svc.GetCategories();
            ddlCategory.Items.Clear();
            ddlFilterCategory.Items.Clear();
            ddlFilterCategory.Items.Add(new ListItem("-- ทั้งหมด --", ""));
            foreach (DataRow r in dt.Rows)
            {
                string code = r["Code"].ToString();
                string text = (r["Icon"] == DBNull.Value ? "" : r["Icon"] + " ") + r["Name"];
                ddlCategory.Items.Add(new ListItem(text, code));
                ddlFilterCategory.Items.Add(new ListItem(text, code));
            }
            if (ddlCategory.Items.Count == 0)
                ddlCategory.Items.Add(new ListItem("(ยังไม่มีประเภท — สร้างที่แท็บประเภทสถานที่)", ""));
        }

        private void BindZoneDropdown()
        {
            ddlZone.Items.Clear();
            ddlZone.Items.Add(new ListItem("-- ไม่ระบุโซน --", "0"));
            foreach (DataRow r in _svc.GetZones().Rows)
                ddlZone.Items.Add(new ListItem(r["Name"].ToString(), r["ID"].ToString()));
        }

        private void LoadList(string filterCategory = "")
        {
            try
            {
                gvList.DataSource = _svc.GetPlaces(filterCategory, 0, true);
                gvList.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        /// <summary>ใช้ในกริด — บอกว่าแถวนี้ขึ้นหมุดบนแผนที่ได้แล้วหรือยัง</summary>
        protected bool HasCoords(object lat, object lng)
        {
            if (lat == null || lat == DBNull.Value || lng == null || lng == DBNull.Value) return false;
            double a, b;
            if (!double.TryParse(lat.ToString(), out a) || !double.TryParse(lng.ToString(), out b)) return false;
            return !(a == 0 && b == 0);
        }

        protected string GetCategoryText(string category)
        {
            foreach (DataRow r in _svc.GetCategories(false).Rows)
                if (r["Code"].ToString() == category)
                    return (r["Icon"] == DBNull.Value ? "" : r["Icon"] + " ") + r["Name"];
            return category;
        }

        // ── แท็บ ──────────────────────────────────────────────────────────────────

        protected void btnTabCategories_Click(object sender, EventArgs e)
        {
            ShowPanel("categories");
            LoadCategoryGrid();
        }

        protected void btnTabZones_Click(object sender, EventArgs e)
        {
            ShowPanel("zones");
            LoadZoneGrid();
        }

        protected void btnBackToList_Click(object sender, EventArgs e)
        {
            ShowPanel("list");
            LoadList();
        }

        private void ShowPanel(string which)
        {
            pnlList.Visible = which == "list";
            pnlForm.Visible = which == "form";
            pnlCategories.Visible = which == "categories";
            pnlZones.Visible = which == "zones";
        }

        // ── สถานที่ ───────────────────────────────────────────────────────────────

        protected void btnFilter_Click(object sender, EventArgs e) { LoadList(ddlFilterCategory.SelectedValue); }

        protected void btnClearFilter_Click(object sender, EventArgs e)
        {
            ddlFilterCategory.SelectedIndex = 0;
            LoadList();
        }

        protected void btnNew_Click(object sender, EventArgs e)
        {
            ClearForm();
            lblFormTitle.Text = "เพิ่มสถานที่ใกล้เคียง";
            ShowPanel("form");
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            ShowPanel("list");
            LoadList();
        }

        protected void gvList_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            string id = e.CommandArgument.ToString();
            if (e.CommandName == "EditItem")
            {
                LoadForEdit(Convert.ToInt32(id));
            }
            else if (e.CommandName == "DeleteItem")
            {
                _code.DatabaseInsertSafe(conn,
                    "UPDATE Guest_NearbyPlaces SET Status = 'False' WHERE ID = @ID",
                    new Dictionary<string, object> { { "@ID", id } });
                ShowMessage("ลบข้อมูลสำเร็จ", true);
                LoadList();
            }
        }

        private void LoadForEdit(int id)
        {
            DataRow row = _svc.GetPlace(id);
            if (row == null) { ShowMessage("ไม่พบรายการนี้", false); return; }

            hfEditId.Value = id.ToString();
            SelectIfPresent(ddlCategory, Val(row, "Category"));
            txtName.Text = Val(row, "Name");
            txtDescription.Text = Val(row, "Description");
            txtDistance.Text = Val(row, "Distance");
            txtTravelTime.Text = Val(row, "Travel_Time");
            txtMapUrl.Text = Val(row, "Map_Url");
            txtPhone.Text = Val(row, "Phone");
            txtIcon.Text = Val(row, "Icon");
            txtSortOrder.Text = Val(row, "Sort_Order");
            txtAddress.Text = Val(row, "Address");
            txtOpenHours.Text = Val(row, "Open_Hours");
            txtMarkerIcon.Text = Val(row, "Marker_Icon");
            txtMarkerColor.Text = Val(row, "Marker_Color");
            txtLat.Text = Val(row, "Latitude");
            txtLng.Text = Val(row, "Longitude");
            chkActive.Checked = Val(row, "Status") != "False";
            SelectIfPresent(ddlZone, Val(row, "Zone_ID"));

            string img = Val(row, "Image_Path");
            pnlCurrentImage.Visible = img.Length > 0;
            if (img.Length > 0) imgCurrent.ImageUrl = img;
            chkRemoveImage.Checked = false;

            string mimg = Val(row, "Marker_Image");
            pnlCurrentMarker.Visible = mimg.Length > 0;
            if (mimg.Length > 0) imgCurrentMarker.ImageUrl = mimg;
            chkRemoveMarkerImage.Checked = false;

            lblFormTitle.Text = "แก้ไขสถานที่ใกล้เคียง";
            ShowPanel("form");
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    ShowMessage("กรุณากรอกชื่อสถานที่", false);
                    return;
                }

                var input = new NearbyPlaceInput
                {
                    Id = string.IsNullOrEmpty(hfEditId.Value) ? 0 : Convert.ToInt32(hfEditId.Value),
                    Category = ddlCategory.SelectedValue,
                    Name = txtName.Text.Trim(),
                    Description = txtDescription.Text.Trim(),
                    Distance = txtDistance.Text.Trim(),
                    TravelTime = txtTravelTime.Text.Trim(),
                    MapUrl = txtMapUrl.Text.Trim(),
                    Phone = txtPhone.Text.Trim(),
                    Icon = txtIcon.Text.Trim(),
                    SortOrder = ParseInt(txtSortOrder.Text, 0),
                    Active = chkActive.Checked,
                    Address = txtAddress.Text.Trim(),
                    OpenHours = txtOpenHours.Text.Trim(),
                    MarkerIcon = txtMarkerIcon.Text.Trim(),
                    MarkerColor = txtMarkerColor.Text.Trim(),
                    Latitude = ParseDec(txtLat.Text),
                    Longitude = ParseDec(txtLng.Text),
                    ZoneId = ParseInt(ddlZone.SelectedValue, 0)
                };

                // รูปเดิม: เก็บไว้ ถ้าไม่ได้อัปโหลดใหม่และไม่ได้สั่งลบ
                DataRow old = input.Id > 0 ? _svc.GetPlace(input.Id) : null;
                input.ImagePath = ResolveUpload(fuImage, chkRemoveImage.Checked, Val(old, "Image_Path"), "place");
                input.MarkerImage = ResolveUpload(fuMarkerImage, chkRemoveMarkerImage.Checked, Val(old, "Marker_Image"), "pin");

                int savedId = _svc.SavePlace(input);

                if (!_svc.HasMapColumns)
                    ShowMessage("บันทึกแล้ว — แต่ยังไม่ได้รันไมเกรชัน PHASE19_01 พิกัด/รูป/หมุด จึงยังไม่ถูกบันทึก "
                              + "(รัน Database/PHASE19_Migration_01_Nearby_Places_Map.sql แล้วบันทึกซ้ำ)", false);
                else if (!input.Latitude.HasValue || !input.Longitude.HasValue)
                    ShowMessage("บันทึกสำเร็จ — แต่ยังไม่ได้ใส่พิกัด สถานที่นี้จะยังไม่ขึ้นหมุดบนแผนที่", true);
                else
                    ShowMessage("บันทึกสำเร็จ", true);

                ShowPanel("list");
                ClearForm();
                LoadList();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        /// <summary>
        /// คืน path รูปที่ควรบันทึก: อัปโหลดใหม่ → ใช้ไฟล์ใหม่ · สั่งลบ → ว่าง · ไม่ทำอะไร → ของเดิม
        /// </summary>
        private string ResolveUpload(FileUpload fu, bool remove, string existing, string prefix)
        {
            if (fu != null && fu.HasFile) return SaveImage(fu, prefix);
            if (remove) return "";
            return existing;
        }

        private string SaveImage(FileUpload fu, string prefix)
        {
            string ext = Path.GetExtension(fu.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (Array.IndexOf(allowed, ext) < 0)
                throw new Exception("รองรับเฉพาะไฟล์รูปภาพ (jpg, png, gif, webp)");
            if (fu.PostedFile.ContentLength > 8 * 1024 * 1024)
                throw new Exception("ไฟล์ใหญ่เกิน 8 MB");

            string folder = Server.MapPath("~/Images/NearbyPlaces");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string fileName = prefix + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_"
                            + Guid.NewGuid().ToString("N").Substring(0, 6) + ext;
            fu.SaveAs(Path.Combine(folder, fileName));
            return "/Images/NearbyPlaces/" + fileName;
        }

        // ── ประเภทสถานที่ ─────────────────────────────────────────────────────────

        private void LoadCategoryGrid()
        {
            gvCategories.DataSource = _svc.GetCategories(false);
            gvCategories.DataBind();
        }

        protected void gvCategories_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            int id = ParseInt(e.CommandArgument.ToString(), 0);
            if (e.CommandName == "EditCat")
            {
                foreach (DataRow r in _svc.GetCategories(false).Rows)
                {
                    if (Convert.ToInt32(r["ID"]) != id) continue;
                    hfCatId.Value = id.ToString();
                    txtCatCode.Text = Val(r, "Code");
                    txtCatName.Text = Val(r, "Name");
                    txtCatIcon.Text = Val(r, "Icon");
                    txtCatColor.Text = Val(r, "Marker_Color");
                    txtCatOrder.Text = Val(r, "Sort_Order");
                    chkCatActive.Checked = Val(r, "Status") != "False";
                    break;
                }
            }
            else if (e.CommandName == "DeleteCat")
            {
                var res = _svc.DeleteCategory(id);
                ShowMessage(res.Message, res.Ok);
            }
            ShowPanel("categories");
            LoadCategoryGrid();
        }

        protected void btnCatSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtCatCode.Text) || string.IsNullOrWhiteSpace(txtCatName.Text))
                {
                    ShowMessage("กรุณากรอกรหัสและชื่อประเภท", false);
                }
                else if (!_svc.SaveCategory(ParseInt(hfCatId.Value, 0), txtCatCode.Text, txtCatName.Text,
                             txtCatIcon.Text, txtCatColor.Text, ParseInt(txtCatOrder.Text, 0), chkCatActive.Checked))
                {
                    ShowMessage("ยังไม่ได้รันไมเกรชัน PHASE19_01 — เพิ่มประเภทไม่ได้", false);
                }
                else
                {
                    ShowMessage("บันทึกประเภทสำเร็จ", true);
                    ClearCatForm();
                    BindCategoryDropdowns();
                }
            }
            catch (Exception ex) { ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false); }
            ShowPanel("categories");
            LoadCategoryGrid();
        }

        protected void btnCatClear_Click(object sender, EventArgs e)
        {
            ClearCatForm();
            ShowPanel("categories");
            LoadCategoryGrid();
        }

        private void ClearCatForm()
        {
            hfCatId.Value = "";
            txtCatCode.Text = ""; txtCatName.Text = ""; txtCatIcon.Text = "";
            txtCatColor.Text = ""; txtCatOrder.Text = "0"; chkCatActive.Checked = true;
        }

        // ── โซน / ขอบเขต ──────────────────────────────────────────────────────────

        private void LoadZoneGrid()
        {
            gvZones.DataSource = _svc.GetZones(false);
            gvZones.DataBind();
        }

        protected void gvZones_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            int id = ParseInt(e.CommandArgument.ToString(), 0);
            if (e.CommandName == "EditZone")
            {
                foreach (DataRow r in _svc.GetZones(false).Rows)
                {
                    if (Convert.ToInt32(r["ID"]) != id) continue;
                    hfZoneId.Value = id.ToString();
                    txtZoneName.Text = Val(r, "Name");
                    txtZoneGeo.Text = Val(r, "Boundary_GeoJson");
                    txtZoneLat.Text = Val(r, "Center_Lat");
                    txtZoneLng.Text = Val(r, "Center_Lng");
                    txtZoneZoom.Text = Val(r, "Default_Zoom");
                    txtZoneFill.Text = Val(r, "Fill_Color");
                    txtZoneLine.Text = Val(r, "Line_Color");
                    txtZoneOrder.Text = Val(r, "Sort_Order");
                    chkZoneDefault.Checked = Val(r, "Is_Default") == "True";
                    chkZoneActive.Checked = Val(r, "Status") != "False";
                    break;
                }
            }
            else if (e.CommandName == "DeleteZone")
            {
                bool ok = _svc.DeleteZone(id);   // เรียกครั้งเดียว — เรียกซ้ำในเงื่อนไขจะลบสองรอบ
                ShowMessage(ok ? "ลบโซนแล้ว" : "ลบไม่สำเร็จ", ok);
            }
            ShowPanel("zones");
            LoadZoneGrid();
        }

        protected void btnZoneSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtZoneName.Text))
                {
                    ShowMessage("กรุณากรอกชื่อโซน", false);
                }
                else if (!_svc.SaveZone(ParseInt(hfZoneId.Value, 0), txtZoneName.Text, txtZoneGeo.Text.Trim(),
                             ParseDec(txtZoneLat.Text), ParseDec(txtZoneLng.Text), ParseInt(txtZoneZoom.Text, 12),
                             txtZoneFill.Text, txtZoneLine.Text, chkZoneDefault.Checked,
                             ParseInt(txtZoneOrder.Text, 0), chkZoneActive.Checked))
                {
                    ShowMessage("ยังไม่ได้รันไมเกรชัน PHASE19_01 — เพิ่มโซนไม่ได้", false);
                }
                else
                {
                    ShowMessage(string.IsNullOrWhiteSpace(txtZoneGeo.Text)
                        ? "บันทึกโซนสำเร็จ — ยังไม่ได้ใส่ขอบเขต แผนที่จะย่อ/ขยายตามหมุดแทน"
                        : "บันทึกโซนสำเร็จ", true);
                    ClearZoneForm();
                    BindZoneDropdown();
                }
            }
            catch (Exception ex) { ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false); }
            ShowPanel("zones");
            LoadZoneGrid();
        }

        protected void btnZoneClear_Click(object sender, EventArgs e)
        {
            ClearZoneForm();
            ShowPanel("zones");
            LoadZoneGrid();
        }

        private void ClearZoneForm()
        {
            hfZoneId.Value = "";
            txtZoneName.Text = ""; txtZoneGeo.Text = "";
            txtZoneLat.Text = ""; txtZoneLng.Text = ""; txtZoneZoom.Text = "12";
            txtZoneFill.Text = "#00b09b"; txtZoneLine.Text = "#00796B";
            txtZoneOrder.Text = "0"; chkZoneDefault.Checked = false; chkZoneActive.Checked = true;
        }

        // ── ข้อมูลตัวอย่าง ────────────────────────────────────────────────────────

        protected void btnSeedData_Click(object sender, EventArgs e)
        {
            try
            {
                DataTable dtCheck = _code.DatabaseQuerySafe(conn,
                    "SELECT COUNT(*) AS Cnt FROM Guest_NearbyPlaces WHERE Status = 'True'", null);
                if (dtCheck.Rows.Count > 0 && Convert.ToInt32(dtCheck.Rows[0]["Cnt"]) > 0)
                {
                    ShowMessage("มีข้อมูลอยู่แล้ว ไม่สามารถ Seed ซ้ำได้", false);
                    return;
                }

                // ใส่พิกัดจริงมาด้วย เพื่อให้เห็นหมุดบนแผนที่ทันทีหลัง seed
                string cols = "Category, Name, Description, Distance, Travel_Time, Map_Url, Phone, Icon, Sort_Order";
                string vals = @"
                    ('beach', N'หาดบางพระ', N'ชายหาดสวยงาม น้ำทะเลใส เหมาะสำหรับเดินเล่นชมพระอาทิตย์ตก', N'500 m', N'2 นาที', N'', N'', N'🏖️', 1),
                    ('beach', N'หาดอ่างศิลา', N'หาดที่มีชื่อเสียงด้านอาหารทะเลสดๆ และบรรยากาศชิลๆ', N'5 km', N'10 นาที', N'', N'', N'🌊', 2),
                    ('restaurant', N'ร้านอาหารริมทะเล', N'อาหารทะเลสดๆ ราคาเป็นกันเอง วิวทะเลสวยมาก', N'1.2 km', N'5 นาที', N'', N'038-123456', N'🍽️', 1),
                    ('cafe', N'Seaside Cafe', N'คาเฟ่ริมทะเล กาแฟสดคั่วเอง ขนมเค้กโฮมเมด', N'1.5 km', N'5 นาที', N'', N'', N'☕', 1),
                    ('attraction', N'เขาสามมุข', N'จุดชมวิวที่สวยที่สุดในบางแสน มองเห็นทะเลได้กว้างไกล', N'8 km', N'15 นาที', N'', N'', N'⛰️', 1),
                    ('shopping', N'ตลาดหนองมน', N'ตลาดของฝากชื่อดัง ขนมพื้นเมือง อาหารทะเลแห้ง', N'6 km', N'12 นาที', N'', N'', N'🛒', 1)";

                _code.DatabaseInsertSafe(conn, "INSERT INTO Guest_NearbyPlaces (" + cols + ") VALUES " + vals, null);
                ShowMessage("เพิ่มข้อมูลตัวอย่างสำเร็จ — ใส่พิกัดให้แต่ละแห่งเพื่อให้ขึ้นหมุดบนแผนที่", true);
                LoadList();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        // ── helper ────────────────────────────────────────────────────────────────

        private void ClearForm()
        {
            hfEditId.Value = "";
            if (ddlCategory.Items.Count > 0) ddlCategory.SelectedIndex = 0;
            if (ddlZone.Items.Count > 0) ddlZone.SelectedIndex = 0;
            txtName.Text = ""; txtDescription.Text = ""; txtDistance.Text = "";
            txtTravelTime.Text = ""; txtMapUrl.Text = ""; txtPhone.Text = "";
            txtIcon.Text = ""; txtSortOrder.Text = "0";
            txtAddress.Text = ""; txtOpenHours.Text = "";
            txtMarkerIcon.Text = ""; txtMarkerColor.Text = "";
            txtLat.Text = ""; txtLng.Text = "";
            chkActive.Checked = true;
            pnlCurrentImage.Visible = false; chkRemoveImage.Checked = false;
            pnlCurrentMarker.Visible = false; chkRemoveMarkerImage.Checked = false;
        }

        private static string Val(DataRow r, string col)
        {
            if (r == null || !r.Table.Columns.Contains(col) || r[col] == DBNull.Value) return "";
            return r[col].ToString();
        }

        private static void SelectIfPresent(DropDownList ddl, string value)
        {
            if (ddl == null || string.IsNullOrEmpty(value)) return;
            ListItem li = ddl.Items.FindByValue(value);
            if (li != null) ddl.SelectedValue = value;
        }

        private static int ParseInt(string s, int def)
        {
            int v;
            return int.TryParse((s ?? "").Trim(), out v) ? v : def;
        }

        private static decimal? ParseDec(string s)
        {
            decimal v;
            s = (s ?? "").Trim();
            if (s.Length == 0) return null;
            // พิกัดใช้จุดทศนิยมเสมอ ไม่ขึ้นกับ locale ของเซิร์ฟเวอร์
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }

        private void ShowMessage(string message, bool success)
        {
            lblMessage.Text = message;
            lblMessage.CssClass = success ? "alert alert-success" : "alert alert-danger";
            lblMessage.Visible = true;
        }
    }
}
