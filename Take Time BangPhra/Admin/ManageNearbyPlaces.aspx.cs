using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin
{
    public partial class ManageNearbyPlaces : Page
    {
        private readonly string conn = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SysSettings)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            _code = new code();
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

        private void LoadList(string filterCategory = "")
        {
            try
            {
                string sql = "SELECT * FROM Guest_NearbyPlaces WHERE Status = 'True'";
                var parameters = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(filterCategory))
                {
                    sql += " AND Category = @Category";
                    parameters.Add("@Category", filterCategory);
                }
                sql += " ORDER BY Category, Sort_Order, Name";
                DataTable dt = _code.DatabaseQuerySafe(conn, sql, parameters.Count > 0 ? parameters : null);
                gvList.DataSource = dt;
                gvList.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        protected void btnFilter_Click(object sender, EventArgs e)
        {
            LoadList(ddlFilterCategory.SelectedValue);
        }

        protected void btnClearFilter_Click(object sender, EventArgs e)
        {
            ddlFilterCategory.SelectedIndex = 0;
            LoadList();
        }

        protected void btnNew_Click(object sender, EventArgs e)
        {
            ClearForm();
            lblFormTitle.Text = "เพิ่มสถานที่ใกล้เคียง";
            pnlList.Visible = false;
            pnlForm.Visible = true;
            hfEditId.Value = "";
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            pnlList.Visible = true;
            pnlForm.Visible = false;
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
            DataTable dt = _code.DatabaseQuerySafe(conn,
                "SELECT * FROM Guest_NearbyPlaces WHERE ID = @ID",
                new Dictionary<string, object> { { "@ID", id } });

            if (dt.Rows.Count > 0)
            {
                DataRow row = dt.Rows[0];
                hfEditId.Value = id.ToString();
                ddlCategory.SelectedValue = row["Category"].ToString();
                txtName.Text = row["Name"].ToString();
                txtDescription.Text = row["Description"].ToString();
                txtDistance.Text = row["Distance"].ToString();
                txtTravelTime.Text = row["Travel_Time"].ToString();
                txtMapUrl.Text = row["Map_Url"].ToString();
                txtPhone.Text = row["Phone"].ToString();
                txtIcon.Text = row["Icon"].ToString();
                txtSortOrder.Text = row["Sort_Order"].ToString();

                lblFormTitle.Text = "แก้ไขสถานที่ใกล้เคียง";
                pnlList.Visible = false;
                pnlForm.Visible = true;
            }
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtName.Text.Trim()))
                {
                    ShowMessage("กรุณากรอกชื่อสถานที่", false);
                    return;
                }

                var parameters = new Dictionary<string, object>
                {
                    { "@Category", ddlCategory.SelectedValue },
                    { "@Name", txtName.Text.Trim() },
                    { "@Description", txtDescription.Text.Trim() },
                    { "@Distance", txtDistance.Text.Trim() },
                    { "@Travel_Time", txtTravelTime.Text.Trim() },
                    { "@Map_Url", txtMapUrl.Text.Trim() },
                    { "@Phone", txtPhone.Text.Trim() },
                    { "@Icon", txtIcon.Text.Trim() },
                    { "@Sort_Order", string.IsNullOrEmpty(txtSortOrder.Text) ? 0 : Convert.ToInt32(txtSortOrder.Text) }
                };

                bool isUpdate = !string.IsNullOrEmpty(hfEditId.Value);
                if (isUpdate)
                {
                    parameters.Add("@ID", Convert.ToInt32(hfEditId.Value));
                    _code.DatabaseInsertSafe(conn,
                        @"UPDATE Guest_NearbyPlaces SET
                            Category = @Category, Name = @Name, Description = @Description,
                            Distance = @Distance, Travel_Time = @Travel_Time, Map_Url = @Map_Url,
                            Phone = @Phone, Icon = @Icon, Sort_Order = @Sort_Order
                          WHERE ID = @ID", parameters);
                    ShowMessage("อัพเดทข้อมูลสำเร็จ", true);
                }
                else
                {
                    _code.DatabaseInsertSafe(conn,
                        @"INSERT INTO Guest_NearbyPlaces (Category, Name, Description, Distance, Travel_Time, Map_Url, Phone, Icon, Sort_Order)
                          VALUES (@Category, @Name, @Description, @Distance, @Travel_Time, @Map_Url, @Phone, @Icon, @Sort_Order)", parameters);
                    ShowMessage("เพิ่มข้อมูลสำเร็จ", true);
                }

                pnlForm.Visible = false;
                pnlList.Visible = true;
                ClearForm();
                LoadList();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        protected void btnSeedData_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if data already exists
                DataTable dtCheck = _code.DatabaseQuerySafe(conn,
                    "SELECT COUNT(*) AS Cnt FROM Guest_NearbyPlaces WHERE Status = 'True'", null);
                if (dtCheck.Rows.Count > 0 && Convert.ToInt32(dtCheck.Rows[0]["Cnt"]) > 0)
                {
                    ShowMessage("มีข้อมูลอยู่แล้ว ไม่สามารถ Seed ซ้ำได้", false);
                    return;
                }

                string sql = @"INSERT INTO Guest_NearbyPlaces (Category, Name, Description, Distance, Travel_Time, Map_Url, Phone, Icon, Sort_Order) VALUES
                    ('beach', N'หาดบางพระ', N'ชายหาดสวยงาม น้ำทะเลใส เหมาะสำหรับเดินเล่นชมพระอาทิตย์ตก', N'500 m', N'2 นาที', N'', N'', N'🏖️', 1),
                    ('beach', N'หาดอ่างศิลา', N'หาดที่มีชื่อเสียงด้านอาหารทะเลสดๆ และบรรยากาศชิลๆ', N'5 km', N'10 นาที', N'', N'', N'🌊', 2),
                    ('restaurant', N'ร้านอาหารริมทะเล', N'อาหารทะเลสดๆ ราคาเป็นกันเอง วิวทะเลสวยมาก', N'1.2 km', N'5 นาที', N'', N'038-123456', N'🍽️', 1),
                    ('restaurant', N'ร้านก๋วยเตี๋ยวป้าแดง', N'ก๋วยเตี๋ยวเรือรสเด็ด เปิดมากว่า 20 ปี ชาวบ้านแนะนำ', N'800 m', N'3 นาที', N'', N'', N'🍜', 2),
                    ('cafe', N'Seaside Cafe', N'คาเฟ่ริมทะเล กาแฟสดคั่วเอง ขนมเค้กโฮมเมด', N'1.5 km', N'5 นาที', N'', N'', N'☕', 1),
                    ('cafe', N'บ้านสวน Coffee', N'คาเฟ่ในสวน บรรยากาศร่มรื่น เครื่องดื่มหลากหลาย', N'2 km', N'7 นาที', N'', N'', N'🌿', 2),
                    ('attraction', N'เขาสามมุข', N'จุดชมวิวที่สวยที่สุดในบางแสน มองเห็นทะเลได้กว้างไกล', N'8 km', N'15 นาที', N'', N'', N'⛰️', 1),
                    ('attraction', N'สวนสัตว์เปิดเขาเขียว', N'สวนสัตว์ขนาดใหญ่ มีสัตว์หลากหลายสายพันธุ์ เหมาะสำหรับครอบครัว', N'25 km', N'35 นาที', N'', N'038-318444', N'🦁', 2),
                    ('shopping', N'ตลาดหนองมน', N'ตลาดของฝากชื่อดัง ขนมพื้นเมือง อาหารทะเลแห้ง', N'6 km', N'12 นาที', N'', N'', N'🛒', 1),
                    ('shopping', N'Central Chonburi', N'ห้างสรรพสินค้า ช้อปปิ้ง ดูหนัง ทานอาหาร ครบครัน', N'15 km', N'25 นาที', N'', N'', N'🏬', 2)";

                _code.DatabaseInsertSafe(conn, sql, null);
                ShowMessage("เพิ่มข้อมูลตัวอย่างสำเร็จ (" + 10 + " รายการ)", true);
                LoadList();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        protected string GetCategoryText(string category)
        {
            switch (category)
            {
                case "beach": return "🏖️ ชายหาด";
                case "restaurant": return "🍽️ ร้านอาหาร";
                case "cafe": return "☕ คาเฟ่";
                case "attraction": return "🎯 สถานที่ท่องเที่ยว";
                case "shopping": return "🛒 ช้อปปิ้ง";
                default: return category;
            }
        }

        private void ClearForm()
        {
            hfEditId.Value = "";
            ddlCategory.SelectedIndex = 0;
            txtName.Text = "";
            txtDescription.Text = "";
            txtDistance.Text = "";
            txtTravelTime.Text = "";
            txtMapUrl.Text = "";
            txtPhone.Text = "";
            txtIcon.Text = "";
            txtSortOrder.Text = "0";
        }

        private void ShowMessage(string message, bool success)
        {
            lblMessage.Text = message;
            lblMessage.CssClass = success ? "alert alert-success" : "alert alert-danger";
            lblMessage.Visible = true;
        }
    }
}
