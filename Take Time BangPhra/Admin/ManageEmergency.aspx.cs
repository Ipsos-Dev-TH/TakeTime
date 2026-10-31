using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin
{
    public partial class ManageEmergency : System.Web.UI.Page
    {
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.WebContent)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            Page.MaintainScrollPositionOnPostBack = true;
            try
            {
                if (Session["permission"].ToString() == "True")
                {
                }
                else
                {
                    Response.Redirect("/Default");
                }
            }
            catch
            {
                Response.Redirect("/Default");
            }

            if (!IsPostBack)
            {
                EnsureTableExists();
                LoadList();
            }
        }

        #region Table Setup

        private void EnsureTableExists()
        {
            try
            {
                string sql = @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Guest_Emergency_Contacts' AND xtype='U')
                    CREATE TABLE [dbo].[Guest_Emergency_Contacts] (
                        [ID] INT IDENTITY(1,1) PRIMARY KEY,
                        [Category] NVARCHAR(50) NOT NULL,
                        [Name] NVARCHAR(200) NOT NULL,
                        [Phone] NVARCHAR(50) NULL,
                        [Description] NVARCHAR(500) NULL,
                        [Icon] NVARCHAR(50) NULL,
                        [Is_Quick_Dial] BIT DEFAULT 0,
                        [Sort_Order] INT DEFAULT 0,
                        [Status] NVARCHAR(10) DEFAULT 'True',
                        [Created_Date] DATETIME DEFAULT GETDATE()
                    )";

                code.DatabaseInsertSafe(conn, sql, new Dictionary<string, object>());
            }
            catch
            {
                // Table may already exist
            }
        }

        #endregion

        #region Load List

        private void LoadList(string categoryFilter = "")
        {
            try
            {
                string sql = "SELECT * FROM [dbo].[Guest_Emergency_Contacts] WHERE [Status] = 'True'";
                var parameters = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(categoryFilter))
                {
                    sql += " AND [Category] = @Category";
                    parameters.Add("@Category", categoryFilter);
                }

                sql += " ORDER BY [Category], [Sort_Order], [Name]";

                DataTable dt = code.DatabaseQuerySafe(conn, sql, parameters);
                gvList.DataSource = dt;
                gvList.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, false);
            }
        }

        protected void ddlFilterCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadList(ddlFilterCategory.SelectedValue);
        }

        #endregion

        #region CRUD Operations

        protected void btnAdd_Click(object sender, EventArgs e)
        {
            ClearForm();
            hdnEditId.Value = "";
            pnlList.Visible = false;
            pnlForm.Visible = true;
        }

        protected void gvList_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "EditItem")
            {
                string id = e.CommandArgument.ToString();
                LoadEditForm(id);
            }
            else if (e.CommandName == "DeleteItem")
            {
                SoftDelete(e.CommandArgument.ToString());
            }
        }

        private void LoadEditForm(string id)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ID", Convert.ToInt32(id) }
                };

                DataTable dt = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM [dbo].[Guest_Emergency_Contacts] WHERE [ID] = @ID",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    hdnEditId.Value = row["ID"].ToString();
                    ddlCategory.SelectedValue = row["Category"].ToString();
                    txtName.Text = row["Name"].ToString();
                    txtPhone.Text = row["Phone"].ToString();
                    txtDescription.Text = row["Description"].ToString();
                    txtIcon.Text = row["Icon"].ToString();
                    chkQuickDial.Checked = row["Is_Quick_Dial"] != DBNull.Value && Convert.ToBoolean(row["Is_Quick_Dial"]);
                    txtSortOrder.Text = row["Sort_Order"].ToString();

                    pnlList.Visible = false;
                    pnlForm.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, false);
            }
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    ShowMessage("กรุณากรอกชื่อ", false);
                    return;
                }

                int sortOrder = 0;
                int.TryParse(txtSortOrder.Text, out sortOrder);

                if (string.IsNullOrEmpty(hdnEditId.Value))
                {
                    // INSERT
                    var parameters = new Dictionary<string, object>
                    {
                        { "@Category", ddlCategory.SelectedValue },
                        { "@Name", txtName.Text.Trim() },
                        { "@Phone", txtPhone.Text.Trim() },
                        { "@Description", txtDescription.Text.Trim() },
                        { "@Icon", txtIcon.Text.Trim() },
                        { "@IsQuickDial", chkQuickDial.Checked ? 1 : 0 },
                        { "@SortOrder", sortOrder }
                    };

                    code.DatabaseInsertSafe(conn,
                        @"INSERT INTO [dbo].[Guest_Emergency_Contacts]
                            ([Category], [Name], [Phone], [Description], [Icon], [Is_Quick_Dial], [Sort_Order])
                          VALUES
                            (@Category, @Name, @Phone, @Description, @Icon, @IsQuickDial, @SortOrder)",
                        parameters);

                    ShowMessage("เพิ่มข้อมูลสำเร็จ", true);
                }
                else
                {
                    // UPDATE
                    var parameters = new Dictionary<string, object>
                    {
                        { "@ID", Convert.ToInt32(hdnEditId.Value) },
                        { "@Category", ddlCategory.SelectedValue },
                        { "@Name", txtName.Text.Trim() },
                        { "@Phone", txtPhone.Text.Trim() },
                        { "@Description", txtDescription.Text.Trim() },
                        { "@Icon", txtIcon.Text.Trim() },
                        { "@IsQuickDial", chkQuickDial.Checked ? 1 : 0 },
                        { "@SortOrder", sortOrder }
                    };

                    code.DatabaseInsertSafe(conn,
                        @"UPDATE [dbo].[Guest_Emergency_Contacts]
                          SET [Category] = @Category,
                              [Name] = @Name,
                              [Phone] = @Phone,
                              [Description] = @Description,
                              [Icon] = @Icon,
                              [Is_Quick_Dial] = @IsQuickDial,
                              [Sort_Order] = @SortOrder
                          WHERE [ID] = @ID",
                        parameters);

                    ShowMessage("แก้ไขข้อมูลสำเร็จ", true);
                }

                ClearForm();
                pnlForm.Visible = false;
                pnlList.Visible = true;
                LoadList(ddlFilterCategory.SelectedValue);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการบันทึก: " + ex.Message, false);
            }
        }

        private void SoftDelete(string id)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ID", Convert.ToInt32(id) }
                };

                code.DatabaseInsertSafe(conn,
                    "UPDATE [dbo].[Guest_Emergency_Contacts] SET [Status] = 'False' WHERE [ID] = @ID",
                    parameters);

                ShowMessage("ลบข้อมูลสำเร็จ", true);
                LoadList(ddlFilterCategory.SelectedValue);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการลบ: " + ex.Message, false);
            }
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            ClearForm();
            pnlForm.Visible = false;
            pnlList.Visible = true;
        }

        #endregion

        #region Seed Default Data

        protected void btnSeed_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if data already exists
                DataTable existing = code.DatabaseQuerySafe(conn,
                    "SELECT COUNT(*) AS Cnt FROM [dbo].[Guest_Emergency_Contacts] WHERE [Status] = 'True'",
                    new Dictionary<string, object>());

                if (existing.Rows.Count > 0 && Convert.ToInt32(existing.Rows[0]["Cnt"]) > 0)
                {
                    ShowMessage("มีข้อมูลอยู่แล้ว (" + existing.Rows[0]["Cnt"].ToString() + " รายการ) หากต้องการเพิ่มใหม่ กรุณาลบข้อมูลเดิมก่อน", false);
                    return;
                }

                SeedDefaultData();
                ShowMessage("เพิ่มข้อมูลตัวอย่างสำเร็จ", true);
                LoadList(ddlFilterCategory.SelectedValue);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการเพิ่มข้อมูลตัวอย่าง: " + ex.Message, false);
            }
        }

        private void SeedDefaultData()
        {
            // Quick Dial
            InsertSeedRow("quickdial", "แผนกต้อนรับ", "1000", "ติดต่อแผนกต้อนรับ 24 ชม.", "fa-concierge-bell", true, 1);
            InsertSeedRow("quickdial", "รปภ.", "1001", "แจ้งเหตุด้านความปลอดภัย", "fa-shield-alt", true, 2);
            InsertSeedRow("quickdial", "แม่บ้าน", "1002", "ขอผ้าเช็ดตัว/อุปกรณ์เพิ่ม", "fa-broom", true, 3);
            InsertSeedRow("quickdial", "ฉุกเฉินโรงแรม", "1003", "แจ้งเหตุฉุกเฉินภายในโรงแรม", "fa-exclamation-triangle", true, 4);
            InsertSeedRow("quickdial", "เรียกรถ", "1004", "เรียกรถรับ-ส่งภายในรีสอร์ท", "fa-car", true, 5);

            // Hospital
            InsertSeedRow("hospital", "โรงพยาบาลชลบุรี", "038-931-200", "โรงพยาบาลรัฐบาล ห่าง ~25 กม.", "fa-hospital", false, 1);
            InsertSeedRow("hospital", "โรงพยาบาลบางพระ", "038-341-234", "โรงพยาบาลใกล้ที่สุด", "fa-hospital", false, 2);
            InsertSeedRow("hospital", "โรงพยาบาลสมิติเวช ศรีราชา", "038-320-300", "โรงพยาบาลเอกชน ศรีราชา", "fa-hospital-alt", false, 3);

            // Police
            InsertSeedRow("police", "สถานีตำรวจภูธรบางพระ", "038-341-110", "สถานีตำรวจท้องที่", "fa-shield-alt", false, 1);
            InsertSeedRow("police", "ศูนย์แจ้งเหตุ", "191", "แจ้งเหตุด่วนตำรวจ", "fa-phone-alt", false, 2);
            InsertSeedRow("police", "หน่วยกู้ภัย", "1669", "หน่วยกู้ชีพ/กู้ภัย", "fa-ambulance", false, 3);

            // Hotel
            InsertSeedRow("hotel", "Room Service", "1005", "สั่งอาหาร/เครื่องดื่มเข้าห้องพัก", "fa-utensils", false, 1);
            InsertSeedRow("hotel", "Concierge", "1006", "จองทัวร์/ร้านอาหาร/กิจกรรม", "fa-info-circle", false, 2);
            InsertSeedRow("hotel", "สปา/นวด", "1007", "จองบริการสปาและนวดแผนไทย", "fa-spa", false, 3);

            // Transport
            InsertSeedRow("transport", "เรียกรถรับส่ง", "063-416-1496", "รถรับส่งสนามบิน/ในเมือง", "fa-shuttle-van", false, 1);
            InsertSeedRow("transport", "Grab/Bolt", "เปิดแอป", "เรียกผ่านแอปพลิเคชัน", "fa-mobile-alt", false, 2);
            InsertSeedRow("transport", "เช่ามอเตอร์ไซค์", "081-xxx-xxxx", "บริการเช่ามอเตอร์ไซค์รายวัน", "fa-motorcycle", false, 3);
        }

        private void InsertSeedRow(string category, string name, string phone, string description, string icon, bool isQuickDial, int sortOrder)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@Category", category },
                { "@Name", name },
                { "@Phone", phone },
                { "@Description", description },
                { "@Icon", icon },
                { "@IsQuickDial", isQuickDial ? 1 : 0 },
                { "@SortOrder", sortOrder }
            };

            code.DatabaseInsertSafe(conn,
                @"INSERT INTO [dbo].[Guest_Emergency_Contacts]
                    ([Category], [Name], [Phone], [Description], [Icon], [Is_Quick_Dial], [Sort_Order])
                  VALUES
                    (@Category, @Name, @Phone, @Description, @Icon, @IsQuickDial, @SortOrder)",
                parameters);
        }

        #endregion

        #region Helpers

        public string GetCategoryText(object categoryObj)
        {
            string category = categoryObj != null ? categoryObj.ToString() : "";
            switch (category)
            {
                case "quickdial": return "\U0001F4DE โทรด่วน";
                case "hospital": return "\U0001F3E5 โรงพยาบาล";
                case "police": return "\U0001F694 ตำรวจ/ฉุกเฉิน";
                case "hotel": return "\U0001F3E8 บริการโรงแรม";
                case "transport": return "\U0001F697 การเดินทาง";
                default: return category;
            }
        }

        private void ClearForm()
        {
            hdnEditId.Value = "";
            ddlCategory.SelectedIndex = 0;
            txtName.Text = "";
            txtPhone.Text = "";
            txtDescription.Text = "";
            txtIcon.Text = "";
            chkQuickDial.Checked = false;
            txtSortOrder.Text = "0";
        }

        private void ShowMessage(string message, bool isSuccess)
        {
            lblMessage.Visible = true;
            lblMessage.Text = message;
            lblMessage.CssClass = isSuccess ? "alert alert-success" : "alert alert-danger";
        }

        #endregion
    }
}
