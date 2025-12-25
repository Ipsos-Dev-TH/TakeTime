using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Class;
using System.Configuration;

namespace Take_Time_BangPhra.Admin.Assets
{
    public partial class AssetManagement : Page
    {
        private AssetService assetService;
        private _Default code;
        private string conn;

        protected void Page_Load(object sender, EventArgs e)
        {
            assetService = new AssetService();
            code = new _Default();
            conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

            // Check permission - Owner only
            if (!CheckPermission())
            {
                Response.Redirect("/Default");
                return;
            }

            if (!IsPostBack)
            {
                LoadCategories();
                LoadVendors();
                LoadAssets();
                LoadSummary();
            }
        }

        private bool CheckPermission()
        {
            try
            {
                return Session["permission"]?.ToString() == "True" &&
                       (Session["User"]?.ToString() == "Owner" || Session["User"]?.ToString() == "Admin");
            }
            catch
            {
                return false;
            }
        }

        #region Data Loading

        private void LoadCategories()
        {
            try
            {
                DataTable dt = assetService.GetAssetCategories();
                if (dt != null && dt.Rows.Count > 0)
                {
                    // For filter dropdown
                    ddlFilterCategory.Items.Clear();
                    ddlFilterCategory.Items.Add(new ListItem("-- ทั้งหมด --", ""));
                    foreach (DataRow row in dt.Rows)
                    {
                        ddlFilterCategory.Items.Add(new ListItem(
                            row["CategoryName"].ToString(),
                            row["ID"].ToString()));
                    }

                    // For form dropdown
                    ddlCategory.Items.Clear();
                    foreach (DataRow row in dt.Rows)
                    {
                        string text = row["CategoryCode"].ToString() + " - " + row["CategoryName"].ToString();
                        ddlCategory.Items.Add(new ListItem(text, row["ID"].ToString()));
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดหมวดหมู่: " + ex.Message, "error");
            }
        }

        private void LoadVendors()
        {
            try
            {
                DataTable dt = code.DatabaseQuery(conn, "SELECT ID, Name FROM Vendor WHERE Status = 'True' ORDER BY Name");
                if (dt != null && dt.Rows.Count > 0)
                {
                    ddlVendor.Items.Clear();
                    ddlVendor.Items.Add(new ListItem("-- ไม่ระบุ --", ""));
                    foreach (DataRow row in dt.Rows)
                    {
                        ddlVendor.Items.Add(new ListItem(row["Name"].ToString(), row["ID"].ToString()));
                    }
                }
            }
            catch { }
        }

        private void LoadAssets()
        {
            try
            {
                string status = ddlFilterStatus.SelectedValue;
                int? categoryId = string.IsNullOrEmpty(ddlFilterCategory.SelectedValue)
                    ? (int?)null
                    : Convert.ToInt32(ddlFilterCategory.SelectedValue);

                DataTable dt = assetService.GetAllAssets(status, categoryId);

                // Filter by search text if provided
                if (!string.IsNullOrEmpty(txtSearch.Text))
                {
                    string search = txtSearch.Text.ToLower();
                    DataView dv = dt.DefaultView;
                    dv.RowFilter = $"AssetCode LIKE '%{search}%' OR AssetName LIKE '%{search}%' OR SerialNumber LIKE '%{search}%'";
                    dt = dv.ToTable();
                }

                gvAssets.DataSource = dt;
                gvAssets.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, "error");
            }
        }

        private void LoadSummary()
        {
            try
            {
                DataTable dt = assetService.GetAssetSummaryByCategory();
                if (dt != null)
                {
                    int totalCount = 0;
                    int activeCount = 0;
                    decimal totalPurchase = 0;
                    decimal totalDepreciation = 0;
                    decimal totalBookValue = 0;

                    foreach (DataRow row in dt.Rows)
                    {
                        totalCount += Convert.ToInt32(row["AssetCount"]);
                        activeCount += Convert.ToInt32(row["ActiveCount"]);
                        totalPurchase += row["TotalPurchasePrice"] != DBNull.Value ? Convert.ToDecimal(row["TotalPurchasePrice"]) : 0;
                        totalDepreciation += row["TotalDepreciation"] != DBNull.Value ? Convert.ToDecimal(row["TotalDepreciation"]) : 0;
                        totalBookValue += row["TotalBookValue"] != DBNull.Value ? Convert.ToDecimal(row["TotalBookValue"]) : 0;
                    }

                    lblTotalAssets.Text = totalCount.ToString("N0");
                    lblActiveAssets.Text = activeCount.ToString("N0");
                    lblTotalPurchase.Text = totalPurchase.ToString("N0");
                    lblTotalDepreciation.Text = totalDepreciation.ToString("N0");
                    lblTotalBookValue.Text = totalBookValue.ToString("N0");
                }
            }
            catch { }
        }

        #endregion

        #region Event Handlers

        protected void btnAddNew_Click(object sender, EventArgs e)
        {
            ClearForm();
            // Show modal via script
            ScriptManager.RegisterStartupScript(this, GetType(), "showModal",
                "document.getElementById('assetModal').style.display = 'block'; document.body.style.overflow = 'hidden';", true);
        }

        protected void ddlFilterCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadAssets();
        }

        protected void ddlFilterStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadAssets();
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            LoadAssets();
        }

        protected void btnSaveAsset_Click(object sender, EventArgs e)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(txtAssetName.Text))
                {
                    ShowMessage("กรุณาระบุชื่อสินทรัพย์", "error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtPurchaseDate.Text))
                {
                    ShowMessage("กรุณาระบุวันที่ซื้อ", "error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtPurchasePrice.Text))
                {
                    ShowMessage("กรุณาระบุราคาซื้อ", "error");
                    return;
                }

                int assetId = Convert.ToInt32(hdnAssetId.Value);
                short userId = Convert.ToInt16(Session["UserID"]);

                if (assetId == 0)
                {
                    // Create new asset
                    var result = assetService.CreateAsset(
                        assetName: txtAssetName.Text.Trim(),
                        description: txtDescription.Text.Trim(),
                        categoryId: Convert.ToInt32(ddlCategory.SelectedValue),
                        serialNumber: txtSerialNumber.Text.Trim(),
                        brand: txtBrand.Text.Trim(),
                        model: txtModel.Text.Trim(),
                        purchaseDate: DateTime.Parse(txtPurchaseDate.Text),
                        purchasePrice: decimal.Parse(txtPurchasePrice.Text),
                        vendorId: string.IsNullOrEmpty(ddlVendor.SelectedValue) ? (int?)null : Convert.ToInt32(ddlVendor.SelectedValue),
                        paymentVoucherId: null,
                        invoiceNumber: txtInvoiceNumber.Text.Trim(),
                        warrantyExpireDate: string.IsNullOrEmpty(txtWarrantyExpire.Text) ? (DateTime?)null : DateTime.Parse(txtWarrantyExpire.Text),
                        usefulLifeYears: Convert.ToInt32(txtUsefulLife.Text),
                        residualValue: decimal.Parse(txtResidualValue.Text ?? "0"),
                        location: txtLocation.Text.Trim(),
                        department: txtDepartment.Text.Trim(),
                        responsiblePersonId: null,
                        notes: null,
                        createdBy: userId
                    );

                    if (result.Success)
                    {
                        ShowMessage($"บันทึกสินทรัพย์สำเร็จ รหัส: {result.Message}", "success");
                        ClearForm();
                        LoadAssets();
                        LoadSummary();
                    }
                    else
                    {
                        ShowMessage(result.Message, "error");
                    }
                }
                else
                {
                    // Update existing asset
                    var result = assetService.UpdateAsset(
                        assetId: assetId,
                        assetName: txtAssetName.Text.Trim(),
                        description: txtDescription.Text.Trim(),
                        serialNumber: txtSerialNumber.Text.Trim(),
                        brand: txtBrand.Text.Trim(),
                        model: txtModel.Text.Trim(),
                        warrantyExpireDate: string.IsNullOrEmpty(txtWarrantyExpire.Text) ? (DateTime?)null : DateTime.Parse(txtWarrantyExpire.Text),
                        location: txtLocation.Text.Trim(),
                        department: txtDepartment.Text.Trim(),
                        responsiblePersonId: null,
                        notes: txtDescription.Text.Trim(),
                        modifiedBy: userId
                    );

                    if (result.Success)
                    {
                        ShowMessage("อัพเดทข้อมูลสำเร็จ", "success");
                        ClearForm();
                        LoadAssets();
                        LoadSummary();
                    }
                    else
                    {
                        ShowMessage(result.Message, "error");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void btnCalcDepreciation_Click(object sender, EventArgs e)
        {
            try
            {
                int year = DateTime.Now.Year;
                int month = DateTime.Now.Month;

                var result = assetService.CalculateMonthlyDepreciation(year, month);

                if (result.Success)
                {
                    ShowMessage(result.Message, "success");
                    LoadAssets();
                    LoadSummary();
                }
                else
                {
                    ShowMessage(result.Message, "error");
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void gvAssets_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            int assetId = Convert.ToInt32(e.CommandArgument);

            if (e.CommandName == "ViewAsset" || e.CommandName == "EditAsset")
            {
                LoadAssetForEdit(assetId);

                // Show modal via script
                ScriptManager.RegisterStartupScript(this, GetType(), "showModal",
                    "document.getElementById('assetModal').style.display = 'block';", true);
            }
            else if (e.CommandName == "DisposeAsset")
            {
                LoadAssetForDispose(assetId);

                // Show dispose modal
                ScriptManager.RegisterStartupScript(this, GetType(), "showDisposeModal",
                    "document.getElementById('disposeModal').style.display = 'block';", true);
            }
            else if (e.CommandName == "ViewHistory")
            {
                LoadDepreciationHistory(assetId);

                // Show history modal
                ScriptManager.RegisterStartupScript(this, GetType(), "showHistoryModal",
                    "document.getElementById('historyModal').style.display = 'block';", true);
            }
        }

        private void LoadAssetForDispose(int assetId)
        {
            DataTable dt = assetService.GetAssetFullDetails(assetId);
            if (dt != null && dt.Rows.Count > 0)
            {
                DataRow row = dt.Rows[0];
                hdnDisposeAssetId.Value = assetId.ToString();
                lblDisposeAssetName.Text = $"{row["AssetCode"]} - {row["AssetName"]}";
                lblDisposeBookValue.Text = Convert.ToDecimal(row["BookValue"]).ToString("N2");
                txtDisposeDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
                ddlDisposeType.SelectedIndex = 0;
                txtDisposePrice.Text = "";
                txtDisposeReason.Text = "";
            }
        }

        private void LoadDepreciationHistory(int assetId)
        {
            DataTable dtAsset = assetService.GetAssetFullDetails(assetId);
            if (dtAsset != null && dtAsset.Rows.Count > 0)
            {
                DataRow row = dtAsset.Rows[0];
                lblHistoryAssetName.Text = $"{row["AssetCode"]} - {row["AssetName"]}";
                lblHistoryMonthlyDep.Text = Convert.ToDecimal(row["MonthlyDepreciation"]).ToString("N2");
            }

            DataTable dtHistory = assetService.GetDepreciationHistory(assetId);
            gvDepreciationHistory.DataSource = dtHistory;
            gvDepreciationHistory.DataBind();
        }

        protected void btnConfirmDispose_Click(object sender, EventArgs e)
        {
            try
            {
                int assetId = Convert.ToInt32(hdnDisposeAssetId.Value);

                if (string.IsNullOrEmpty(ddlDisposeType.SelectedValue))
                {
                    ShowMessage("กรุณาเลือกประเภทการจำหน่าย", "error");
                    return;
                }

                if (string.IsNullOrEmpty(txtDisposeDate.Text))
                {
                    ShowMessage("กรุณาระบุวันที่จำหน่าย", "error");
                    return;
                }

                if (string.IsNullOrEmpty(txtDisposeReason.Text))
                {
                    ShowMessage("กรุณาระบุเหตุผล/หมายเหตุ", "error");
                    return;
                }

                decimal? disposePrice = null;
                if (!string.IsNullOrEmpty(txtDisposePrice.Text))
                {
                    disposePrice = decimal.Parse(txtDisposePrice.Text);
                }

                int userId = Convert.ToInt32(Session["UserID"]);

                var result = assetService.DisposeAsset(
                    assetId: assetId,
                    status: ddlDisposeType.SelectedValue,
                    disposalDate: DateTime.Parse(txtDisposeDate.Text),
                    disposalPrice: disposePrice,
                    reason: txtDisposeReason.Text.Trim(),
                    disposedBy: userId
                );

                if (result.Success)
                {
                    ShowMessage(result.Message, "success");
                    LoadAssets();
                    LoadSummary();
                }
                else
                {
                    ShowMessage(result.Message, "error");
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        private void LoadAssetForEdit(int assetId)
        {
            // Use GetAssetFullDetails to get all columns including WarrantyExpireDate
            DataTable dt = assetService.GetAssetFullDetails(assetId);
            if (dt != null && dt.Rows.Count > 0)
            {
                DataRow row = dt.Rows[0];

                hdnAssetId.Value = assetId.ToString();
                lblModalTitle.Text = "แก้ไขสินทรัพย์: " + row["AssetCode"].ToString();

                txtAssetName.Text = row["AssetName"].ToString();
                txtDescription.Text = row["Description"]?.ToString() ?? "";
                txtSerialNumber.Text = row["SerialNumber"]?.ToString() ?? "";
                txtBrand.Text = row["Brand"]?.ToString() ?? "";
                txtModel.Text = row["Model"]?.ToString() ?? "";
                txtLocation.Text = row["Location"]?.ToString() ?? "";
                txtDepartment.Text = row["Department"]?.ToString() ?? "";

                if (row["PurchaseDate"] != DBNull.Value)
                    txtPurchaseDate.Text = Convert.ToDateTime(row["PurchaseDate"]).ToString("yyyy-MM-dd");

                if (row["PurchasePrice"] != DBNull.Value)
                    txtPurchasePrice.Text = Convert.ToDecimal(row["PurchasePrice"]).ToString("0.00");

                if (row["UsefulLifeYears"] != DBNull.Value)
                    txtUsefulLife.Text = row["UsefulLifeYears"].ToString();

                if (row["ResidualValue"] != DBNull.Value)
                    txtResidualValue.Text = Convert.ToDecimal(row["ResidualValue"]).ToString("0.00");

                // Safely check for WarrantyExpireDate column
                if (dt.Columns.Contains("WarrantyExpireDate") && row["WarrantyExpireDate"] != DBNull.Value)
                    txtWarrantyExpire.Text = Convert.ToDateTime(row["WarrantyExpireDate"]).ToString("yyyy-MM-dd");

                // Set category dropdown
                if (row["CategoryID"] != DBNull.Value)
                {
                    string categoryId = row["CategoryID"].ToString();
                    var item = ddlCategory.Items.FindByValue(categoryId);
                    if (item != null)
                        ddlCategory.SelectedValue = categoryId;
                }

                // Set vendor dropdown
                if (row["VendorID"] != DBNull.Value)
                {
                    string vendorId = row["VendorID"].ToString();
                    var item = ddlVendor.Items.FindByValue(vendorId);
                    if (item != null)
                        ddlVendor.SelectedValue = vendorId;
                }

                // Category and Vendor are disabled for editing
                ddlCategory.Enabled = false;
                ddlVendor.Enabled = false;
                txtPurchaseDate.Enabled = false;
                txtPurchasePrice.Enabled = false;
                txtUsefulLife.Enabled = false;
                txtResidualValue.Enabled = false;
            }
        }

        #endregion

        #region Helper Methods

        protected string GetStatusBadge(string status)
        {
            switch (status)
            {
                case "ACTIVE":
                    return "<span class='badge badge-active'>ใช้งานอยู่</span>";
                case "DISPOSED":
                    return "<span class='badge badge-disposed'>จำหน่ายแล้ว</span>";
                case "SOLD":
                    return "<span class='badge badge-sold'>ขายแล้ว</span>";
                case "WRITTEN_OFF":
                    return "<span class='badge badge-disposed'>ตัดจำหน่าย</span>";
                default:
                    return $"<span class='badge'>{status}</span>";
            }
        }

        private void ShowMessage(string message, string type)
        {
            pnlMessage.Visible = true;
            lblMessage.Text = message;
            pnlMessage.CssClass = type == "success" ? "alert alert-success" : "alert alert-error";
        }

        private void ClearForm()
        {
            hdnAssetId.Value = "0";
            lblModalTitle.Text = "เพิ่มสินทรัพย์ใหม่";
            txtAssetName.Text = "";
            txtDescription.Text = "";
            txtSerialNumber.Text = "";
            txtBrand.Text = "";
            txtModel.Text = "";
            txtPurchaseDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtPurchasePrice.Text = "";
            txtUsefulLife.Text = "5";
            txtResidualValue.Text = "0";
            txtWarrantyExpire.Text = "";
            txtInvoiceNumber.Text = "";
            txtLocation.Text = "";
            txtDepartment.Text = "";
            ddlVendor.SelectedIndex = 0;
            ddlCategory.SelectedIndex = 0;

            // Re-enable fields
            ddlCategory.Enabled = true;
            ddlVendor.Enabled = true;
            txtPurchaseDate.Enabled = true;
            txtPurchasePrice.Enabled = true;
            txtUsefulLife.Enabled = true;
            txtResidualValue.Enabled = true;
        }

        /// <summary>
        /// Export assets to Excel for accounting office and revenue department
        /// ส่งออกข้อมูลสินทรัพย์สำหรับสำนักงานบัญชีและสรรพากร
        /// </summary>
        protected void btnExportExcel_Click(object sender, EventArgs e)
        {
            try
            {
                // Get filter values
                string status = ddlFilterStatus.SelectedValue;
                int? categoryId = string.IsNullOrEmpty(ddlFilterCategory.SelectedValue)
                    ? (int?)null
                    : Convert.ToInt32(ddlFilterCategory.SelectedValue);

                // Get data for export
                DataTable dt = assetService.GetAssetsForExport(DateTime.Now.Year, status, categoryId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    ShowMessage("ไม่พบข้อมูลสำหรับส่งออก", "error");
                    return;
                }

                // Create Excel file
                string fileName = $"ทะเบียนสินทรัพย์_{DateTime.Now:yyyyMMdd_HHmmss}.xls";

                Response.Clear();
                Response.Buffer = true;
                Response.AddHeader("content-disposition", $"attachment;filename={fileName}");
                Response.Charset = "";
                Response.ContentType = "application/vnd.ms-excel";
                Response.ContentEncoding = System.Text.Encoding.UTF8;

                // Write BOM for UTF-8
                Response.BinaryWrite(new byte[] { 0xEF, 0xBB, 0xBF });

                using (System.IO.StringWriter sw = new System.IO.StringWriter())
                {
                    using (System.Web.UI.HtmlTextWriter hw = new System.Web.UI.HtmlTextWriter(sw))
                    {
                        // Create a form to export
                        System.Web.UI.WebControls.GridView gv = new System.Web.UI.WebControls.GridView();
                        gv.DataSource = dt;
                        gv.DataBind();

                        // Style the grid for Excel
                        gv.HeaderStyle.BackColor = System.Drawing.Color.FromArgb(102, 126, 234);
                        gv.HeaderStyle.ForeColor = System.Drawing.Color.White;
                        gv.HeaderStyle.Font.Bold = true;
                        gv.RowStyle.BackColor = System.Drawing.Color.White;
                        gv.AlternatingRowStyle.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);

                        // Write title
                        hw.Write("<h2 style='font-family: TH Sarabun New, Arial; text-align: center;'>ทะเบียนสินทรัพย์ถาวร</h2>");
                        hw.Write($"<p style='font-family: TH Sarabun New, Arial; text-align: center;'>วันที่ส่งออก: {DateTime.Now:dd/MM/yyyy HH:mm}</p>");
                        hw.Write("<br/>");

                        gv.RenderControl(hw);

                        Response.Output.Write(sw.ToString());
                        Response.Flush();
                        Response.End();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการส่งออก: " + ex.Message, "error");
            }
        }

        public override void VerifyRenderingInServerForm(System.Web.UI.Control control)
        {
            // Required for GridView export
        }

        #endregion
    }
}
