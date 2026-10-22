using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin
{
    public partial class ManageFacilities : Page
    {
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.WebContent)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            try
            {
                if (Session["permission"] == null || Session["permission"].ToString() != "True")
                {
                    Response.Redirect("/Default");
                    return;
                }

                if (!IsPostBack)
                {
                    EnsureTableExists();
                    LoadList();
                }
            }
            catch
            {
                Response.Redirect("/Default");
            }
        }

        #region Table Setup

        private void EnsureTableExists()
        {
            try
            {
                string sql = @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_Facilities')
                    BEGIN
                        CREATE TABLE [dbo].[Guest_Facilities] (
                            [ID] INT IDENTITY(1,1) PRIMARY KEY,
                            [Category] NVARCHAR(50) NOT NULL,
                            [Name] NVARCHAR(200) NOT NULL,
                            [Description] NVARCHAR(500) NULL,
                            [Icon] NVARCHAR(50) NULL,
                            [Css_Class] NVARCHAR(50) NULL,
                            [Hours] NVARCHAR(100) NULL,
                            [Extra_Info] NVARCHAR(200) NULL,
                            [Sort_Order] INT DEFAULT 0,
                            [Status] NVARCHAR(10) DEFAULT 'True',
                            [Created_Date] DATETIME DEFAULT GETDATE()
                        )
                    END";

                code.DatabaseInsertSafe(conn, sql, new Dictionary<string, object>());
            }
            catch { }
        }

        #endregion

        #region Load Data

        private void LoadList(string categoryFilter = "")
        {
            try
            {
                string sql = "SELECT * FROM Guest_Facilities WHERE Status = 'True'";
                var parameters = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(categoryFilter))
                {
                    sql += " AND Category = @Category";
                    parameters.Add("@Category", categoryFilter);
                }

                sql += " ORDER BY Category, Sort_Order, Name";

                DataTable dt = code.DatabaseQuerySafe(conn, sql, parameters);
                gvFacilities.DataSource = dt;
                gvFacilities.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, "error");
            }
        }

        #endregion

        #region Filter

        protected void ddlFilterCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadList(ddlFilterCategory.SelectedValue);
        }

        protected void btnClearFilter_Click(object sender, EventArgs e)
        {
            ddlFilterCategory.SelectedIndex = 0;
            LoadList();
        }

        #endregion

        #region Form Events

        protected void btnAdd_Click(object sender, EventArgs e)
        {
            ClearForm();
            lblFormTitle.Text = "เพิ่มรายการใหม่";
            pnlForm.Visible = true;
            pnlList.Visible = false;
            hfEditID.Value = "";
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            pnlForm.Visible = false;
            pnlList.Visible = true;
            ClearForm();
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtName.Text.Trim()))
                {
                    ShowMessage("กรุณากรอกชื่อ", "error");
                    return;
                }

                if (string.IsNullOrEmpty(ddlCategory.SelectedValue))
                {
                    ShowMessage("กรุณาเลือกหมวดหมู่", "error");
                    return;
                }

                string editId = hfEditID.Value;
                bool isUpdate = !string.IsNullOrEmpty(editId);

                int sortOrder = 0;
                int.TryParse(txtSortOrder.Text.Trim(), out sortOrder);

                var parameters = new Dictionary<string, object>
                {
                    { "@Category", ddlCategory.SelectedValue },
                    { "@Name", txtName.Text.Trim() },
                    { "@Description", string.IsNullOrEmpty(txtDescription.Text.Trim()) ? (object)DBNull.Value : txtDescription.Text.Trim() },
                    { "@Icon", string.IsNullOrEmpty(txtIcon.Text.Trim()) ? (object)DBNull.Value : txtIcon.Text.Trim() },
                    { "@Css_Class", string.IsNullOrEmpty(txtCssClass.Text.Trim()) ? (object)DBNull.Value : txtCssClass.Text.Trim() },
                    { "@Hours", string.IsNullOrEmpty(txtHours.Text.Trim()) ? (object)DBNull.Value : txtHours.Text.Trim() },
                    { "@Extra_Info", string.IsNullOrEmpty(txtExtraInfo.Text.Trim()) ? (object)DBNull.Value : txtExtraInfo.Text.Trim() },
                    { "@Sort_Order", sortOrder }
                };

                if (isUpdate)
                {
                    parameters.Add("@ID", editId);
                    code.DatabaseInsertSafe(conn,
                        @"UPDATE [dbo].[Guest_Facilities] SET
                            [Category] = @Category,
                            [Name] = @Name,
                            [Description] = @Description,
                            [Icon] = @Icon,
                            [Css_Class] = @Css_Class,
                            [Hours] = @Hours,
                            [Extra_Info] = @Extra_Info,
                            [Sort_Order] = @Sort_Order
                          WHERE ID = @ID",
                        parameters);

                    ShowMessage("อัพเดทข้อมูลสำเร็จ", "success");
                }
                else
                {
                    parameters.Add("@Status", "True");
                    code.DatabaseInsertSafe(conn,
                        @"INSERT INTO [dbo].[Guest_Facilities]
                            (Category, Name, [Description], Icon, Css_Class, Hours, Extra_Info, Sort_Order, Status)
                          VALUES (@Category, @Name, @Description, @Icon, @Css_Class, @Hours, @Extra_Info, @Sort_Order, @Status)",
                        parameters);

                    ShowMessage("บันทึกข้อมูลสำเร็จ", "success");
                }

                pnlForm.Visible = false;
                pnlList.Visible = true;
                ClearForm();
                LoadList(ddlFilterCategory.SelectedValue);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        #endregion

        #region GridView Events

        protected void gvFacilities_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            string id = e.CommandArgument.ToString();

            if (e.CommandName == "EditItem")
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ID", id }
                };

                DataTable dt = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Guest_Facilities WHERE ID = @ID",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    LoadToForm(row);
                    lblFormTitle.Text = "แก้ไขรายการ";
                    hfEditID.Value = id;
                    pnlForm.Visible = true;
                    pnlList.Visible = false;
                }
            }
            else if (e.CommandName == "DeleteItem")
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ID", id }
                };

                code.DatabaseInsertSafe(conn,
                    "UPDATE Guest_Facilities SET Status = 'False' WHERE ID = @ID",
                    parameters);

                ShowMessage("ลบข้อมูลสำเร็จ", "success");
                LoadList(ddlFilterCategory.SelectedValue);
            }
        }

        #endregion

        #region Seed Data

        protected void btnSeed_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if data already exists
                DataTable dtCheck = code.DatabaseQuerySafe(conn,
                    "SELECT COUNT(*) AS Cnt FROM Guest_Facilities WHERE Status = 'True'",
                    new Dictionary<string, object>());

                if (dtCheck.Rows.Count > 0 && Convert.ToInt32(dtCheck.Rows[0]["Cnt"]) > 0)
                {
                    ShowMessage("มีข้อมูลอยู่แล้ว ไม่สามารถ Seed ซ้ำได้ กรุณาลบข้อมูลเดิมก่อน", "error");
                    return;
                }

                SeedDefaultData();
                ShowMessage("Seed ข้อมูลตัวอย่างสำเร็จ!", "success");
                LoadList();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการ Seed ข้อมูล: " + ex.Message, "error");
            }
        }

        private void SeedDefaultData()
        {
            // ===== FACILITY items (12) =====
            SeedItem("facility", "Swimming Pool", "สระว่ายน้ำกลางแจ้ง ขนาดมาตรฐาน พร้อมเก้าอี้อาบแดด", "fa-swimming-pool", "pool", "06:00 - 20:00", "25m x 12m", 1);
            SeedItem("facility", "Restaurant & Bar", "ห้องอาหารและบาร์ อาหารไทย-นานาชาติ", "fa-utensils", "restaurant", "07:00 - 22:00", "80 seats", 2);
            SeedItem("facility", "Spa & Massage", "สปาและนวดแผนไทย ผ่อนคลายครบวงจร", "fa-spa", "spa", "10:00 - 21:00", null, 3);
            SeedItem("facility", "Fitness Center", "ห้องออกกำลังกาย อุปกรณ์ครบครัน", "fa-dumbbell", "gym", "06:00 - 22:00", null, 4);
            SeedItem("facility", "Private Beach Access", "ทางลงหาดส่วนตัว หาดทรายสะอาด", "fa-umbrella-beach", "beach", "06:00 - 19:00", null, 5);
            SeedItem("facility", "BBQ Area", "พื้นที่บาร์บีคิว พร้อมอุปกรณ์ครบชุด", "fa-fire", "bbq", "16:00 - 22:00", "Max 20 persons", 6);
            SeedItem("facility", "Karaoke Room", "ห้องคาราโอเกะ ระบบเสียงคุณภาพสูง", "fa-microphone", "karaoke", "14:00 - 23:00", "10-15 persons", 7);
            SeedItem("facility", "Meeting Room", "ห้องประชุม พร้อมอุปกรณ์นำเสนอ โปรเจคเตอร์", "fa-chalkboard-teacher", "meeting", "08:00 - 20:00", "30 persons", 8);
            SeedItem("facility", "Garden & Playground", "สวนและสนามเด็กเล่น พื้นที่สีเขียวร่มรื่น", "fa-tree", "garden", "24hr", null, 9);
            SeedItem("facility", "Free Parking", "ที่จอดรถฟรี พร้อมระบบรักษาความปลอดภัย", "fa-car", "parking", "24hr", "CCTV 24hr", 10);
            SeedItem("facility", "High-Speed WiFi", "อินเทอร์เน็ตความเร็วสูง ครอบคลุมทั่วรีสอร์ท", "fa-wifi", "wifi", "24hr", "100 Mbps", 11);
            SeedItem("facility", "Laundry Service", "บริการซักรีด รับ-ส่งถึงห้อง", "fa-tshirt", "laundry", "08:00 - 18:00", "Ext. 103", 12);

            // ===== AMENITY items (12) =====
            SeedItem("amenity", "Air Conditioning", "เครื่องปรับอากาศ ปรับอุณหภูมิได้", "fa-snowflake", null, null, null, 1);
            SeedItem("amenity", "Smart TV with Netflix", "สมาร์ททีวี พร้อม Netflix และช่องดาวเทียม", "fa-tv", null, null, null, 2);
            SeedItem("amenity", "Mini Bar / Refrigerator", "มินิบาร์และตู้เย็น พร้อมเครื่องดื่มต้อนรับ", "fa-box", null, null, null, 3);
            SeedItem("amenity", "Coffee & Tea Maker", "ชุดชงกาแฟและชา ฟรีทุกวัน", "fa-coffee", null, null, null, 4);
            SeedItem("amenity", "Rain Shower", "ฝักบัว Rain Shower น้ำแรงสะอาด", "fa-shower", null, null, null, 5);
            SeedItem("amenity", "Premium Toiletries", "ชุดอุปกรณ์อาบน้ำระดับพรีเมียม", "fa-pump-soap", null, null, null, 6);
            SeedItem("amenity", "Hair Dryer", "ไดร์เป่าผม พร้อมใช้งาน", "fa-wind", null, null, null, 7);
            SeedItem("amenity", "In-Room Safe", "ตู้เซฟนิรภัยในห้อง", "fa-lock", null, null, null, 8);
            SeedItem("amenity", "Direct Dial Phone", "โทรศัพท์สายตรง ติดต่อแผนกต้อนรับ", "fa-phone", null, null, null, 9);
            SeedItem("amenity", "Premium Bedding", "เครื่องนอนระดับพรีเมียม ผ้าปูที่นอนเกรด A", "fa-bed", null, null, null, 10);
            SeedItem("amenity", "Private Balcony", "ระเบียงส่วนตัว วิวทะเลหรือสวน", "fa-door-open", null, null, null, 11);
            SeedItem("amenity", "Free WiFi", "WiFi ฟรีในห้องพัก ความเร็วสูง", "fa-wifi", null, null, null, 12);

            // ===== POLICY items (6) =====
            SeedItem("policy", "Check-in / Check-out", "เช็คอิน: 14:00 น. / เช็คเอาท์: 12:00 น. Early check-in และ Late check-out สามารถสอบถามได้ที่แผนกต้อนรับ", "fa-clock", null, null, null, 1);
            SeedItem("policy", "No Smoking", "ห้ามสูบบุหรี่ในห้องพักและพื้นที่ปิด มีพื้นที่สูบบุหรี่จัดไว้ให้โดยเฉพาะ ฝ่าฝืนปรับ 2,000 บาท", "fa-smoking-ban", null, null, null, 2);
            SeedItem("policy", "Pet Policy", "อนุญาตให้นำสัตว์เลี้ยงขนาดเล็กเข้าพักได้ (ไม่เกิน 5 กก.) มีค่าธรรมเนียมเพิ่มเติม 500 บาท/คืน", "fa-paw", null, null, null, 3);
            SeedItem("policy", "Quiet Hours", "เวลาเงียบ 22:00 - 07:00 น. กรุณางดส่งเสียงดังรบกวนผู้เข้าพักท่านอื่น", "fa-volume-down", null, null, null, 4);
            SeedItem("policy", "Pool Rules", "เด็กอายุต่ำกว่า 12 ปี ต้องมีผู้ปกครองดูแล ห้ามนำอาหารและเครื่องดื่มแก้วลงสระ ห้ามวิ่งรอบสระ", "fa-swimmer", null, null, null, 5);
            SeedItem("policy", "Visitors", "ผู้มาเยี่ยมต้องลงทะเบียนที่แผนกต้อนรับ อนุญาตเข้าพื้นที่ส่วนกลางได้ถึง 22:00 น. ไม่อนุญาตให้ค้างคืนโดยไม่ลงทะเบียน", "fa-user-friends", null, null, null, 6);
        }

        private void SeedItem(string category, string name, string description, string icon, string cssClass, string hours, string extraInfo, int sortOrder)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@Category", category },
                { "@Name", name },
                { "@Description", (object)description ?? DBNull.Value },
                { "@Icon", (object)icon ?? DBNull.Value },
                { "@Css_Class", (object)cssClass ?? DBNull.Value },
                { "@Hours", (object)hours ?? DBNull.Value },
                { "@Extra_Info", (object)extraInfo ?? DBNull.Value },
                { "@Sort_Order", sortOrder },
                { "@Status", "True" }
            };

            code.DatabaseInsertSafe(conn,
                @"INSERT INTO [dbo].[Guest_Facilities]
                    (Category, Name, [Description], Icon, Css_Class, Hours, Extra_Info, Sort_Order, Status)
                  VALUES (@Category, @Name, @Description, @Icon, @Css_Class, @Hours, @Extra_Info, @Sort_Order, @Status)",
                parameters);
        }

        #endregion

        #region Helper Methods

        private void LoadToForm(DataRow row)
        {
            if (ddlCategory.Items.FindByValue(row["Category"].ToString()) != null)
                ddlCategory.SelectedValue = row["Category"].ToString();

            txtName.Text = row["Name"].ToString();
            txtDescription.Text = row["Description"] != DBNull.Value ? row["Description"].ToString() : "";
            txtIcon.Text = row["Icon"] != DBNull.Value ? row["Icon"].ToString() : "";
            txtCssClass.Text = row["Css_Class"] != DBNull.Value ? row["Css_Class"].ToString() : "";
            txtHours.Text = row["Hours"] != DBNull.Value ? row["Hours"].ToString() : "";
            txtExtraInfo.Text = row["Extra_Info"] != DBNull.Value ? row["Extra_Info"].ToString() : "";
            txtSortOrder.Text = row["Sort_Order"] != DBNull.Value ? row["Sort_Order"].ToString() : "0";
        }

        private void ClearForm()
        {
            ddlCategory.SelectedIndex = 0;
            txtName.Text = "";
            txtDescription.Text = "";
            txtIcon.Text = "";
            txtCssClass.Text = "";
            txtHours.Text = "";
            txtExtraInfo.Text = "";
            txtSortOrder.Text = "0";
            hfEditID.Value = "";
        }

        protected string GetCategoryText(object category)
        {
            if (category == null || category == DBNull.Value)
                return "";

            switch (category.ToString())
            {
                case "facility": return "\U0001F3E2 สิ่งอำนวยความสะดวก";
                case "amenity": return "\U0001F6CF สิ่งอำนวยความสะดวกในห้อง";
                case "policy": return "\U0001F4CB กฎระเบียบ";
                default: return category.ToString();
            }
        }

        protected string GetCategoryBadgeClass(object category)
        {
            if (category == null || category == DBNull.Value)
                return "badge-default";

            switch (category.ToString())
            {
                case "facility": return "badge-facility";
                case "amenity": return "badge-amenity";
                case "policy": return "badge-policy";
                default: return "badge-default";
            }
        }

        private void ShowMessage(string message, string type)
        {
            lblMessage.Text = message;
            lblMessage.CssClass = type == "success" ? "alert alert-success" : "alert alert-danger";
            lblMessage.Visible = true;
        }

        #endregion
    }
}
