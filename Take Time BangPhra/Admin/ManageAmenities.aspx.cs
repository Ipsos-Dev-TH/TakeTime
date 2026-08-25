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
    /// จัดการ "เบิกของใช้ในห้อง" — รายการของใช้ + คำขอที่เข้ามา
    ///
    /// ตั้งค่าต่อรายการได้ว่า ฟรีเสมอ / ฟรีกี่ชิ้นแรกแล้วค่อยคิดเงิน / คิดเงินทุกชิ้น
    /// คำขอที่เข้ามาจะไล่สถานะ รอรับเรื่อง → กำลังจัดของ → ส่งแล้ว (หรือยกเลิก)
    /// </summary>
    public partial class ManageAmenities : Page
    {
        private readonly string conn =
            System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private AmenityService _svc;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SysSettings)) return;
            _svc = new AmenityService(conn);
            try
            {
                if (Session["permission"] == null || Session["permission"].ToString() != "True")
                {
                    Response.Redirect("/Default");
                    return;
                }
                if (!IsPostBack)
                {
                    if (!_svc.IsReady)
                        ShowMessage("ยังไม่ได้รันไมเกรชัน PHASE19_02 — รัน "
                                  + "Database/PHASE19_Migration_02_Guest_Amenities.sql ก่อนใช้งานหน้านี้", false);
                    LoadItems();
                    LoadRequests();
                }
            }
            catch
            {
                Response.Redirect("/Default");
            }
        }

        // ── แท็บ ──────────────────────────────────────────────────────────────────

        protected void btnTabItems_Click(object sender, EventArgs e) { ShowPanel("items"); LoadItems(); }
        protected void btnTabRequests_Click(object sender, EventArgs e) { ShowPanel("requests"); LoadRequests(); }

        private void ShowPanel(string which)
        {
            pnlItems.Visible = which == "items";
            pnlForm.Visible = which == "form";
            pnlRequests.Visible = which == "requests";
        }

        // ── รายการของใช้ ─────────────────────────────────────────────────────────

        private void LoadItems()
        {
            gvItems.DataSource = _svc.GetItems(false);
            gvItems.DataBind();
        }

        /// <summary>ข้อความสรุปเงื่อนไขค่าใช้จ่าย — ใช้ในกริดให้เห็นทั้งหมดในคอลัมน์เดียว</summary>
        protected string ChargeText(object isFree, object price, object quota, object unit)
        {
            bool free = ToBool(isFree);
            decimal p = price == null || price == DBNull.Value ? 0m : Convert.ToDecimal(price);
            int q = quota == null || quota == DBNull.Value ? 0 : Convert.ToInt32(quota);
            string u = unit == null || unit == DBNull.Value ? "ชิ้น" : unit.ToString();
            if (free) return "ฟรีเสมอ";
            if (q > 0) return "ฟรี " + q + " " + u + " แรก · เกินนั้น " + p.ToString("N0") + " บาท";
            return p.ToString("N0") + " บาท/" + u;
        }

        protected void btnNewItem_Click(object sender, EventArgs e)
        {
            ClearItemForm();
            lblFormTitle.Text = "เพิ่มรายการของใช้";
            ShowPanel("form");
        }

        protected void btnCancelItem_Click(object sender, EventArgs e) { ShowPanel("items"); LoadItems(); }

        protected void gvItems_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            int id = ParseInt(e.CommandArgument == null ? "" : e.CommandArgument.ToString(), 0);
            if (e.CommandName == "EditItem")
            {
                DataRow r = _svc.GetItem(id);
                if (r == null) { ShowMessage("ไม่พบรายการนี้", false); ShowPanel("items"); LoadItems(); return; }

                hfItemId.Value = id.ToString();
                txtName.Text = Val(r, "Name");
                txtDescription.Text = Val(r, "Description");
                txtCategory.Text = Val(r, "Category");
                txtIcon.Text = Val(r, "Icon");
                txtUnit.Text = Val(r, "Unit");
                txtPrice.Text = Val(r, "Price");
                txtQuota.Text = Val(r, "Free_Quota_Per_Stay");
                txtMaxPer.Text = Val(r, "Max_Per_Request");
                txtSortOrder.Text = Val(r, "Sort_Order");
                chkFree.Checked = ToBool(r["Is_Free"]);
                chkActive.Checked = Val(r, "Status") != "False";

                string img = Val(r, "Image_Path");
                pnlCurrentImage.Visible = img.Length > 0;
                if (img.Length > 0) imgCurrent.ImageUrl = img;
                chkRemoveImage.Checked = false;

                lblFormTitle.Text = "แก้ไขรายการของใช้";
                ShowPanel("form");
                return;
            }
            if (e.CommandName == "DeleteItem")
            {
                ShowMessage(_svc.DeleteItem(id) ? "ปิดใช้งานรายการแล้ว (ใบเบิกเก่ายังเก็บชื่อ/ราคาเดิมไว้)"
                                                : "ทำรายการไม่สำเร็จ", true);
            }
            ShowPanel("items");
            LoadItems();
        }

        protected void btnSaveItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    ShowMessage("กรุณากรอกชื่อรายการ", false);
                    ShowPanel("form");
                    return;
                }

                var input = new AmenityItemInput
                {
                    Id = ParseInt(hfItemId.Value, 0),
                    Name = txtName.Text.Trim(),
                    Description = txtDescription.Text.Trim(),
                    Category = txtCategory.Text.Trim(),
                    Icon = txtIcon.Text.Trim(),
                    Unit = txtUnit.Text.Trim(),
                    IsFree = chkFree.Checked,
                    Price = ParseDec(txtPrice.Text),
                    FreeQuotaPerStay = ParseInt(txtQuota.Text, 0),
                    MaxPerRequest = ParseInt(txtMaxPer.Text, 5),
                    SortOrder = ParseInt(txtSortOrder.Text, 0),
                    Active = chkActive.Checked
                };

                DataRow old = input.Id > 0 ? _svc.GetItem(input.Id) : null;
                if (fuImage.HasFile) input.ImagePath = SaveImage(fuImage);
                else if (chkRemoveImage.Checked) input.ImagePath = "";
                else input.ImagePath = Val(old, "Image_Path");

                _svc.SaveItem(input);

                // เตือนเมื่อตั้งค่าขัดกันเอง — จะได้ไม่งงว่าทำไมเก็บเงินไม่ได้
                if (!input.IsFree && input.Price <= 0)
                    ShowMessage("บันทึกแล้ว — แต่ตั้งเป็น \"คิดเงิน\" โดยราคาเป็น 0 "
                              + "ผลคือได้ฟรีอยู่ดี ถ้าตั้งใจให้ฟรีควรติ๊ก \"ฟรีเสมอ\"", true);
                else
                    ShowMessage("บันทึกสำเร็จ", true);

                ClearItemForm();
                ShowPanel("items");
                LoadItems();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
                ShowPanel("form");
            }
        }

        private string SaveImage(FileUpload fu)
        {
            string ext = Path.GetExtension(fu.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (Array.IndexOf(allowed, ext) < 0)
                throw new Exception("รองรับเฉพาะไฟล์รูปภาพ (jpg, png, gif, webp)");
            if (fu.PostedFile.ContentLength > 8 * 1024 * 1024)
                throw new Exception("ไฟล์ใหญ่เกิน 8 MB");

            string folder = Server.MapPath("~/Images/Amenities");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            string fileName = "am_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_"
                            + Guid.NewGuid().ToString("N").Substring(0, 6) + ext;
            fu.SaveAs(Path.Combine(folder, fileName));
            return "/Images/Amenities/" + fileName;
        }

        // ── คำขอที่เข้ามา ────────────────────────────────────────────────────────

        private void LoadRequests()
        {
            string filter = ddlStatus.SelectedValue;
            gvRequests.DataSource = _svc.GetStaffRequests(string.IsNullOrEmpty(filter) ? null : filter);
            gvRequests.DataBind();
        }

        protected void btnFilterRequests_Click(object sender, EventArgs e) { ShowPanel("requests"); LoadRequests(); }

        protected void gvRequests_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            long id;
            if (!long.TryParse(e.CommandArgument == null ? "" : e.CommandArgument.ToString(), out id))
            {
                ShowPanel("requests"); LoadRequests(); return;
            }

            short? staff = null;
            if (Session["AdminID"] != null)
            {
                short s;
                if (short.TryParse(Session["AdminID"].ToString(), out s)) staff = s;
            }

            switch (e.CommandName)
            {
                case "Accept": _svc.UpdateStatus(id, "ACCEPTED", staff); ShowMessage("รับเรื่องแล้ว", true); break;
                case "Deliver": _svc.UpdateStatus(id, "DELIVERED", staff); ShowMessage("บันทึกว่าส่งของแล้ว", true); break;
                case "Cancel": _svc.UpdateStatus(id, "CANCELLED", staff); ShowMessage("ยกเลิกคำขอแล้ว", true); break;
            }
            ShowPanel("requests");
            LoadRequests();
        }

        protected string StatusText(object status) { return AmenityService.StatusText(status == null ? "" : status.ToString()); }

        // ── helper ────────────────────────────────────────────────────────────────

        private void ClearItemForm()
        {
            hfItemId.Value = "";
            txtName.Text = ""; txtDescription.Text = ""; txtCategory.Text = "";
            txtIcon.Text = ""; txtUnit.Text = "ชิ้น";
            txtPrice.Text = "0"; txtQuota.Text = "0"; txtMaxPer.Text = "5"; txtSortOrder.Text = "0";
            chkFree.Checked = true; chkActive.Checked = true;
            pnlCurrentImage.Visible = false; chkRemoveImage.Checked = false;
        }

        private static string Val(DataRow r, string col)
        {
            if (r == null || !r.Table.Columns.Contains(col) || r[col] == DBNull.Value) return "";
            return r[col].ToString();
        }

        private static bool ToBool(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            if (v is bool b) return b;
            string s = v.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseInt(string s, int def)
        {
            int v;
            return int.TryParse((s ?? "").Trim(), out v) ? v : def;
        }

        private static decimal ParseDec(string s)
        {
            decimal v;
            return decimal.TryParse((s ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : 0m;
        }

        private void ShowMessage(string message, bool success)
        {
            lblMessage.Text = message;
            lblMessage.CssClass = success ? "alert alert-success" : "alert alert-danger";
            lblMessage.Visible = true;
        }
    }
}
