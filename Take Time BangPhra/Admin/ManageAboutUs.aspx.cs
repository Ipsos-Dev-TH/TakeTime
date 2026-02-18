using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;

namespace Take_Time_BangPhra.Admin
{
    public partial class ManageAboutUs : System.Web.UI.Page
    {
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
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
                    LoadSectionTypeFilter();
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
            string sql = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Guest_AboutUs_Sections' AND xtype='U')
                CREATE TABLE Guest_AboutUs_Sections (
                    ID INT IDENTITY(1,1) PRIMARY KEY,
                    Section_Type NVARCHAR(50),
                    Title NVARCHAR(200),
                    Content NVARCHAR(MAX),
                    Sub_Content NVARCHAR(MAX),
                    Icon NVARCHAR(50),
                    Image_Url NVARCHAR(500),
                    Year_Text NVARCHAR(50),
                    Sort_Order INT DEFAULT 0,
                    Status NVARCHAR(10) DEFAULT 'True',
                    Created_Date DATETIME DEFAULT GETDATE()
                )";
            code.DatabaseInsertSafe(conn, sql, new Dictionary<string, object>());
        }

        #endregion

        #region Load Data

        private void LoadSectionTypeFilter()
        {
            ddlFilterSectionType.Items.Clear();
            ddlFilterSectionType.Items.Add(new ListItem("-- ทั้งหมด --", ""));
            ddlFilterSectionType.Items.Add(new ListItem("\U0001F3E0 Hero", "hero"));
            ddlFilterSectionType.Items.Add(new ListItem("\U0001F4D6 เรื่องราว", "story"));
            ddlFilterSectionType.Items.Add(new ListItem("\U0001F48E คุณค่า", "values"));
            ddlFilterSectionType.Items.Add(new ListItem("\U0001F4C5 ไทม์ไลน์", "timeline"));
            ddlFilterSectionType.Items.Add(new ListItem("\U0001F4A1 แนวคิด", "concept"));
            ddlFilterSectionType.Items.Add(new ListItem("\U0001F465 ทีมงาน", "team"));
            ddlFilterSectionType.Items.Add(new ListItem("\U0001F4AC คำพูด", "quote"));
        }

        private void LoadList(string filterType = "")
        {
            string sql = "SELECT * FROM Guest_AboutUs_Sections WHERE Status = 'True'";
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(filterType))
            {
                sql += " AND Section_Type = @SectionType";
                parameters.Add("@SectionType", filterType);
            }

            sql += " ORDER BY Sort_Order ASC, ID ASC";

            DataTable dt = code.DatabaseQuerySafe(conn, sql, parameters);
            gvSections.DataSource = dt;
            gvSections.DataBind();
        }

        protected void ddlFilterSectionType_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadList(ddlFilterSectionType.SelectedValue);
        }

        #endregion

        #region Form Events

        protected void btnAdd_Click(object sender, EventArgs e)
        {
            ClearForm();
            lblFormTitle.Text = "เพิ่มข้อมูลใหม่";
            hfEditID.Value = "";
            pnlList.Visible = false;
            pnlForm.Visible = true;
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
                if (string.IsNullOrEmpty(txtTitle.Text.Trim()))
                {
                    lblMessage.Text = "กรุณากรอกหัวข้อ";
                    lblMessage.ForeColor = System.Drawing.Color.Red;
                    return;
                }

                string editId = hfEditID.Value;
                bool isUpdate = !string.IsNullOrEmpty(editId);

                int sortOrder = 0;
                int.TryParse(txtSortOrder.Text.Trim(), out sortOrder);

                var parameters = new Dictionary<string, object>
                {
                    { "@Section_Type", ddlSectionType.SelectedValue },
                    { "@Title", txtTitle.Text.Trim() },
                    { "@Content", txtContent.Text.Trim() },
                    { "@Sub_Content", string.IsNullOrEmpty(txtSubContent.Text.Trim()) ? (object)DBNull.Value : txtSubContent.Text.Trim() },
                    { "@Icon", string.IsNullOrEmpty(txtIcon.Text.Trim()) ? (object)DBNull.Value : txtIcon.Text.Trim() },
                    { "@Image_Url", string.IsNullOrEmpty(txtImageUrl.Text.Trim()) ? (object)DBNull.Value : txtImageUrl.Text.Trim() },
                    { "@Year_Text", string.IsNullOrEmpty(txtYearText.Text.Trim()) ? (object)DBNull.Value : txtYearText.Text.Trim() },
                    { "@Sort_Order", sortOrder }
                };

                if (isUpdate)
                {
                    parameters.Add("@ID", editId);
                    code.DatabaseInsertSafe(conn,
                        @"UPDATE Guest_AboutUs_Sections SET
                            Section_Type = @Section_Type,
                            Title = @Title,
                            Content = @Content,
                            Sub_Content = @Sub_Content,
                            Icon = @Icon,
                            Image_Url = @Image_Url,
                            Year_Text = @Year_Text,
                            Sort_Order = @Sort_Order
                          WHERE ID = @ID",
                        parameters);

                    lblMessage.Text = "อัพเดทข้อมูลสำเร็จ";
                }
                else
                {
                    parameters.Add("@Status", "True");
                    code.DatabaseInsertSafe(conn,
                        @"INSERT INTO Guest_AboutUs_Sections
                            (Section_Type, Title, Content, Sub_Content, Icon, Image_Url, Year_Text, Sort_Order, Status)
                          VALUES
                            (@Section_Type, @Title, @Content, @Sub_Content, @Icon, @Image_Url, @Year_Text, @Sort_Order, @Status)",
                        parameters);

                    lblMessage.Text = "บันทึกข้อมูลสำเร็จ";
                }

                lblMessage.ForeColor = System.Drawing.Color.Green;
                pnlForm.Visible = false;
                pnlList.Visible = true;
                ClearForm();
                LoadList(ddlFilterSectionType.SelectedValue);
            }
            catch (Exception ex)
            {
                lblMessage.Text = "เกิดข้อผิดพลาด: " + ex.Message;
                lblMessage.ForeColor = System.Drawing.Color.Red;
            }
        }

        #endregion

        #region GridView Events

        protected void gvSections_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            string id = e.CommandArgument.ToString();

            if (e.CommandName == "EditSection")
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ID", id }
                };

                DataTable dt = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Guest_AboutUs_Sections WHERE ID = @ID",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    hfEditID.Value = id;
                    ddlSectionType.SelectedValue = row["Section_Type"].ToString();
                    txtTitle.Text = row["Title"].ToString();
                    txtContent.Text = row["Content"] != DBNull.Value ? row["Content"].ToString() : "";
                    txtSubContent.Text = row["Sub_Content"] != DBNull.Value ? row["Sub_Content"].ToString() : "";
                    txtIcon.Text = row["Icon"] != DBNull.Value ? row["Icon"].ToString() : "";
                    txtImageUrl.Text = row["Image_Url"] != DBNull.Value ? row["Image_Url"].ToString() : "";
                    txtYearText.Text = row["Year_Text"] != DBNull.Value ? row["Year_Text"].ToString() : "";
                    txtSortOrder.Text = row["Sort_Order"].ToString();

                    lblFormTitle.Text = "แก้ไขข้อมูล";
                    pnlList.Visible = false;
                    pnlForm.Visible = true;
                }
            }
            else if (e.CommandName == "DeleteSection")
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ID", id }
                };

                code.DatabaseInsertSafe(conn,
                    "UPDATE Guest_AboutUs_Sections SET Status = 'False' WHERE ID = @ID",
                    parameters);

                lblMessage.Text = "ลบข้อมูลสำเร็จ";
                lblMessage.ForeColor = System.Drawing.Color.Green;
                LoadList(ddlFilterSectionType.SelectedValue);
            }
        }

        #endregion

        #region Seed Data

        protected void btnSeedData_Click(object sender, EventArgs e)
        {
            try
            {
                DataTable dtCheck = code.DatabaseQuerySafe(conn,
                    "SELECT COUNT(*) AS cnt FROM Guest_AboutUs_Sections WHERE Status = 'True'",
                    new Dictionary<string, object>());

                if (dtCheck.Rows.Count > 0 && Convert.ToInt32(dtCheck.Rows[0]["cnt"]) > 0)
                {
                    lblMessage.Text = "มีข้อมูลอยู่แล้ว ไม่สามารถ Seed ซ้ำได้";
                    lblMessage.ForeColor = System.Drawing.Color.Red;
                    return;
                }

                SeedDefaultData();
                LoadList();
                lblMessage.Text = "Seed ข้อมูลตัวอย่างสำเร็จ";
                lblMessage.ForeColor = System.Drawing.Color.Green;
            }
            catch (Exception ex)
            {
                lblMessage.Text = "เกิดข้อผิดพลาด: " + ex.Message;
                lblMessage.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void SeedDefaultData()
        {
            // Hero
            InsertSection("hero", "Take Time Nature Resort",
                "พักผ่อนอย่างมีสไตล์ ใกล้ชิดธรรมชาติ", "", "", "", "", 1);

            // Story
            InsertSection("story", "เรื่องราวของเรา",
                "Take Time Nature Resort ก่อตั้งขึ้นด้วยความฝันที่จะสร้างพื้นที่พักผ่อนที่ผสมผสานความสะดวกสบายสมัยใหม่เข้ากับความงามของธรรมชาติ รีสอร์ทของเราตั้งอยู่ในทำเลที่เงียบสงบ ล้อมรอบด้วยต้นไม้และธรรมชาติ เหมาะสำหรับผู้ที่ต้องการหลีกหนีจากความวุ่นวายของชีวิตประจำวัน",
                "", "", "", "", 2);

            // Values (4 items)
            InsertSection("values", "ธรรมชาติ",
                "เราเชื่อในพลังของธรรมชาติที่ช่วยเยียวยาและฟื้นฟูจิตใจ", "", "\U0001F33F", "", "", 3);
            InsertSection("values", "ความสะดวกสบาย",
                "สิ่งอำนวยความสะดวกครบครันเพื่อการพักผ่อนที่สมบูรณ์แบบ", "", "\U0001F6CB", "", "", 4);
            InsertSection("values", "บริการใส่ใจ",
                "ทีมงานพร้อมดูแลทุกรายละเอียดด้วยความใส่ใจ", "", "\U0001F49D", "", "", 5);
            InsertSection("values", "ยั่งยืน",
                "มุ่งมั่นดำเนินธุรกิจอย่างรับผิดชอบต่อสิ่งแวดล้อม", "", "\U0001F30D", "", "", 6);

            // Timeline (5 items)
            InsertSection("timeline", "จุดเริ่มต้นความฝัน",
                "เริ่มต้นโครงการ Take Time Nature Resort บนพื้นที่ธรรมชาติอันสวยงาม", "", "", "", "2019", 7);
            InsertSection("timeline", "เปิดให้บริการ",
                "เปิดให้บริการอย่างเป็นทางการ ต้อนรับแขกกลุ่มแรก", "", "", "", "2020", 8);
            InsertSection("timeline", "ขยายบริการ",
                "เพิ่มห้องพักและสิ่งอำนวยความสะดวกใหม่", "", "", "", "2021", 9);
            InsertSection("timeline", "รางวัลแห่งความสำเร็จ",
                "ได้รับการยอมรับจากนักท่องเที่ยวและสื่อต่างๆ", "", "", "", "2023", 10);
            InsertSection("timeline", "ก้าวสู่อนาคต",
                "พัฒนาระบบและบริการใหม่เพื่อประสบการณ์ที่ดียิ่งขึ้น", "", "", "", "2024", 11);

            // Concept (4 items)
            InsertSection("concept", "ธรรมชาติบำบัด",
                "ใช้ธรรมชาติเป็นส่วนหนึ่งของการพักผ่อนและเยียวยา", "", "\U0001F331", "", "", 12);
            InsertSection("concept", "ดีไซน์ร่วมสมัย",
                "ออกแบบที่ผสมผสานความทันสมัยกับกลิ่นอายธรรมชาติ", "", "\U0001F3A8", "", "", 13);
            InsertSection("concept", "อาหารท้องถิ่น",
                "คัดสรรวัตถุดิบท้องถิ่นเพื่อมอบรสชาติแท้จริง", "", "\U0001F37D", "", "", 14);
            InsertSection("concept", "กิจกรรมหลากหลาย",
                "กิจกรรมที่หลากหลายสำหรับทุกเพศทุกวัย", "", "\U0001F3AF", "", "", 15);

            // Team
            InsertSection("team", "ทีมงานของเรา",
                "ทีมงานของเราประกอบด้วยผู้เชี่ยวชาญด้านการบริการที่มีประสบการณ์ พร้อมดูแลและให้บริการแขกทุกท่านด้วยความเอาใจใส่และอบอุ่น",
                "", "", "", "", 16);

            // Quote
            InsertSection("quote", "ทุกช่วงเวลาที่นี่ คือการพักผ่อนที่แท้จริง",
                "- Take Time Nature Resort", "", "", "", "", 17);
        }

        private void InsertSection(string sectionType, string title, string content,
            string subContent, string icon, string imageUrl, string yearText, int sortOrder)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@Section_Type", sectionType },
                { "@Title", title },
                { "@Content", content },
                { "@Sub_Content", string.IsNullOrEmpty(subContent) ? (object)DBNull.Value : subContent },
                { "@Icon", string.IsNullOrEmpty(icon) ? (object)DBNull.Value : icon },
                { "@Image_Url", string.IsNullOrEmpty(imageUrl) ? (object)DBNull.Value : imageUrl },
                { "@Year_Text", string.IsNullOrEmpty(yearText) ? (object)DBNull.Value : yearText },
                { "@Sort_Order", sortOrder },
                { "@Status", "True" }
            };

            code.DatabaseInsertSafe(conn,
                @"INSERT INTO Guest_AboutUs_Sections
                    (Section_Type, Title, Content, Sub_Content, Icon, Image_Url, Year_Text, Sort_Order, Status)
                  VALUES
                    (@Section_Type, @Title, @Content, @Sub_Content, @Icon, @Image_Url, @Year_Text, @Sort_Order, @Status)",
                parameters);
        }

        #endregion

        #region Helper Methods

        private void ClearForm()
        {
            hfEditID.Value = "";
            if (ddlSectionType.Items.Count > 0)
                ddlSectionType.SelectedIndex = 0;
            txtTitle.Text = "";
            txtContent.Text = "";
            txtSubContent.Text = "";
            txtIcon.Text = "";
            txtImageUrl.Text = "";
            txtYearText.Text = "";
            txtSortOrder.Text = "0";
        }

        protected string GetSectionTypeText(object sectionType)
        {
            if (sectionType == null || sectionType == DBNull.Value)
                return "";

            switch (sectionType.ToString())
            {
                case "hero": return "\U0001F3E0 Hero";
                case "story": return "\U0001F4D6 เรื่องราว";
                case "values": return "\U0001F48E คุณค่า";
                case "timeline": return "\U0001F4C5 ไทม์ไลน์";
                case "concept": return "\U0001F4A1 แนวคิด";
                case "team": return "\U0001F465 ทีมงาน";
                case "quote": return "\U0001F4AC คำพูด";
                default: return sectionType.ToString();
            }
        }

        protected string TruncateText(object text, int maxLength)
        {
            if (text == null || text == DBNull.Value)
                return "";

            string str = text.ToString();
            if (str.Length <= maxLength)
                return str;

            return str.Substring(0, maxLength) + "...";
        }

        #endregion
    }
}
