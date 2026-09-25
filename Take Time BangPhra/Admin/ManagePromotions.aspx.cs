using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin
{
    public partial class ManagePromotions : System.Web.UI.Page
    {
        private readonly string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private PromotionService _service;

        private static readonly string[] AllowedExt = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const int MaxBytes = 10 * 1024 * 1024; // 10MB

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

                _service = new PromotionService(conn);

                if (!IsPostBack)
                {
                    _service.EnsureTableExists();
                    LoadList();
                }
            }
            catch
            {
                Response.Redirect("/Default");
            }
        }

        private void LoadList()
        {
            gv.DataSource = _service.GetAll();
            gv.DataBind();
        }

        // ── ใช้ใน markup แสดงช่วงเวลา ──
        public string FormatRange(object start, object end)
        {
            string s = start == null || start == DBNull.Value ? "—" : Convert.ToDateTime(start).ToString("dd/MM/yyyy");
            string e = end == null || end == DBNull.Value ? "—" : Convert.ToDateTime(end).ToString("dd/MM/yyyy");
            return Server.HtmlEncode($"{s} ถึง {e}");
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                string title = txtTitle.Text.Trim();
                if (string.IsNullOrEmpty(title))
                {
                    ShowMsg("กรุณากรอกชื่อโปรโมชั่น", false);
                    return;
                }

                int id = int.TryParse(hfEditId.Value, out var pid) ? pid : 0;

                // อัปโหลดรูป (ถ้ามี) — imageUrl == null แปลว่าไม่เปลี่ยนรูปเดิม (ตอน update)
                string imageUrl = null;
                if (fuImage.HasFile)
                {
                    imageUrl = SaveImage();
                    if (imageUrl == null) return; // SaveImage แสดง error แล้ว
                }

                string description = txtDescription.Text.Trim();
                string linkUrl = txtLinkUrl.Text.Trim();
                bool showHome = chkShowHome.Checked;
                bool active = chkActive.Checked;
                int sortOrder = int.TryParse(txtSortOrder.Text.Trim(), out var so) ? so : 0;
                DateTime? startDate = ParseDate(txtStartDate.Text);
                DateTime? endDate = ParseDate(txtEndDate.Text);

                if (startDate.HasValue && endDate.HasValue && startDate > endDate)
                {
                    ShowMsg("วันที่เริ่มต้องไม่เกินวันที่สิ้นสุด", false);
                    return;
                }

                if (id > 0)
                {
                    _service.Update(id, title, description, imageUrl, linkUrl, showHome, startDate, endDate, sortOrder, active);
                    ShowMsg("แก้ไขโปรโมชั่นเรียบร้อย", true);
                }
                else
                {
                    // เพิ่มใหม่: ถ้าไม่อัปรูปให้เก็บค่าว่าง
                    _service.Insert(title, description, imageUrl ?? "", linkUrl, showHome, startDate, endDate, sortOrder, active);
                    ShowMsg("เพิ่มโปรโมชั่นเรียบร้อย", true);
                }

                ResetForm();
                LoadList();
            }
            catch (Exception ex)
            {
                ShowMsg("บันทึกไม่สำเร็จ: " + ex.Message, false);
            }
        }

        private string SaveImage()
        {
            string ext = Path.GetExtension(fuImage.FileName ?? "").ToLowerInvariant();
            if (Array.IndexOf(AllowedExt, ext) < 0)
            {
                ShowMsg("รองรับเฉพาะไฟล์รูป JPG, PNG, GIF, WEBP", false);
                return null;
            }
            if (fuImage.PostedFile.ContentLength > MaxBytes)
            {
                ShowMsg("ไฟล์ใหญ่เกิน 10MB", false);
                return null;
            }

            string dir = Server.MapPath("~/Images/Promotions/");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string fileName = "promo_" + Guid.NewGuid().ToString("N") + ext;
            fuImage.SaveAs(Path.Combine(dir, fileName));
            return "/Images/Promotions/" + fileName;
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            ResetForm();
        }

        protected void gv_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out int id) || id <= 0) return;

            try
            {
                switch (e.CommandName)
                {
                    case "EditPromo":
                        LoadForEdit(id);
                        break;
                    case "TogglePromo":
                        _service.ToggleStatus(id);
                        LoadList();
                        ShowMsg("เปลี่ยนสถานะเรียบร้อย", true);
                        break;
                    case "DeletePromo":
                        _service.Delete(id);
                        ResetForm();
                        LoadList();
                        ShowMsg("ลบโปรโมชั่นเรียบร้อย", true);
                        break;
                }
            }
            catch (Exception ex)
            {
                ShowMsg("ดำเนินการไม่สำเร็จ: " + ex.Message, false);
            }
        }

        private void LoadForEdit(int id)
        {
            var row = _service.GetById(id);
            if (row == null) { ShowMsg("ไม่พบโปรโมชั่น", false); return; }

            hfEditId.Value = id.ToString();
            litFormTitle.Text = "แก้ไขโปรโมชั่น #" + id;
            txtTitle.Text = row["Title"]?.ToString();
            txtDescription.Text = row["Description"] == DBNull.Value ? "" : row["Description"].ToString();
            txtLinkUrl.Text = row["Link_Url"] == DBNull.Value ? "" : row["Link_Url"].ToString();
            txtSortOrder.Text = row["Sort_Order"]?.ToString();
            chkShowHome.Checked = row["Show_On_Homepage"] != DBNull.Value && Convert.ToBoolean(row["Show_On_Homepage"]);
            chkActive.Checked = (row["Status"]?.ToString() ?? "True") == "True";
            txtStartDate.Text = row["Start_Date"] == DBNull.Value ? "" : Convert.ToDateTime(row["Start_Date"]).ToString("yyyy-MM-dd");
            txtEndDate.Text = row["End_Date"] == DBNull.Value ? "" : Convert.ToDateTime(row["End_Date"]).ToString("yyyy-MM-dd");

            string img = row["Image_Url"] == DBNull.Value ? "" : row["Image_Url"].ToString();
            if (!string.IsNullOrEmpty(img))
            {
                imgCurrent.ImageUrl = img;
                imgCurrent.Visible = true;
            }
            else
            {
                imgCurrent.Visible = false;
            }
        }

        private void ResetForm()
        {
            hfEditId.Value = "0";
            litFormTitle.Text = "เพิ่มโปรโมชั่นใหม่";
            txtTitle.Text = "";
            txtDescription.Text = "";
            txtLinkUrl.Text = "";
            txtSortOrder.Text = "0";
            txtStartDate.Text = "";
            txtEndDate.Text = "";
            chkShowHome.Checked = true;
            chkActive.Checked = true;
            imgCurrent.Visible = false;
        }

        private static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return DateTime.TryParse(s, out var d) ? d : (DateTime?)null;
        }

        private void ShowMsg(string text, bool success)
        {
            string color = success ? "#155724" : "#721c24";
            string bg = success ? "#d4edda" : "#f8d7da";
            lblMsg.Visible = true;
            lblMsg.Text = $"<div style='padding:10px 14px;border-radius:6px;margin-bottom:14px;background:{bg};color:{color};font-weight:600;'>"
                + (success ? "✔ " : "⚠ ") + Server.HtmlEncode(text) + "</div>";
        }
    }
}
