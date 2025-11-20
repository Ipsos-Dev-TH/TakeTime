using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin
{
    public partial class CustomerManagement : System.Web.UI.Page
    {
        private string connString;
        private SqlConnection conn;
        private CustomerService customerService;
        private code codeHelper;

        protected void Page_Load(object sender, EventArgs e)
        {
            // Initialize connection
            connString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
            conn = new SqlConnection(connString);
            customerService = new CustomerService(conn);
            codeHelper = new code();

            if (!IsPostBack)
            {
                CheckAdminLogin();
                LoadCustomerTypes();
                LoadStatistics();
                LoadCustomers();
            }
        }

        #region Authentication

        private void CheckAdminLogin()
        {
            if (Session["Admin_ID"] == null)
            {
                Response.Redirect("~/Admin/Login.aspx");
            }
        }

        private short? GetAdminID()
        {
            if (Session["Admin_ID"] != null)
            {
                return Convert.ToInt16(Session["Admin_ID"]);
            }
            return null;
        }

        #endregion

        #region Load Data

        private void LoadCustomerTypes()
        {
            try
            {
                DataTable dt = customerService.GetCustomerTypes();

                ddlCustomerType.Items.Clear();
                ddlCustomerType.Items.Add(new ListItem("-- เลือกประเภทลูกค้า --", ""));

                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        ddlCustomerType.Items.Add(new ListItem(
                            row["CustomerTypeName"].ToString(),
                            row["ID"].ToString()
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("ไม่สามารถโหลดประเภทลูกค้าได้: " + ex.Message);
            }
        }

        private void LoadStatistics()
        {
            try
            {
                // Total customers
                string queryTotal = "SELECT COUNT(*) FROM Customer WHERE Status = 1";
                DataTable dtTotal = codeHelper.DatabaseQuery(connString, queryTotal);
                if (dtTotal.Rows.Count > 0)
                {
                    lblTotalCustomers.Text = dtTotal.Rows[0][0].ToString();
                }

                // Active customers
                string queryActive = "SELECT COUNT(*) FROM Customer WHERE Status = 1 AND IsActive = 1";
                DataTable dtActive = codeHelper.DatabaseQuery(connString, queryActive);
                if (dtActive.Rows.Count > 0)
                {
                    lblActiveCustomers.Text = dtActive.Rows[0][0].ToString();
                }

                // New customers this month
                string queryNew = @"
                    SELECT COUNT(*)
                    FROM Customer
                    WHERE Status = 1
                      AND CreatedDate >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)";
                DataTable dtNew = codeHelper.DatabaseQuery(connString, queryNew);
                if (dtNew.Rows.Count > 0)
                {
                    lblNewCustomersThisMonth.Text = dtNew.Rows[0][0].ToString();
                }
            }
            catch (Exception ex)
            {
                ShowError("ไม่สามารถโหลดสถิติได้: " + ex.Message);
            }
        }

        private void LoadCustomers(string searchTerm = null)
        {
            try
            {
                DataTable dt = customerService.SearchCustomers(searchTerm, activeOnly: false, maxResults: 1000);

                if (dt != null && dt.Rows.Count > 0)
                {
                    gvCustomers.DataSource = dt;
                    gvCustomers.DataBind();
                }
                else
                {
                    gvCustomers.DataSource = null;
                    gvCustomers.DataBind();
                }
            }
            catch (Exception ex)
            {
                ShowError("ไม่สามารถโหลดข้อมูลลูกค้าได้: " + ex.Message);
            }
        }

        #endregion

        #region Search

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            string searchTerm = txtSearch.Text.Trim();
            LoadCustomers(searchTerm);
        }

        protected void btnClearSearch_Click(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            LoadCustomers();
        }

        #endregion

        #region Add/Edit Customer

        protected void btnShowAddForm_Click(object sender, EventArgs e)
        {
            ShowAddForm();
        }

        protected void btnSaveCustomer_Click(object sender, EventArgs e)
        {
            if (Page.IsValid)
            {
                try
                {
                    bool isEditMode = hfEditMode.Value == "true";
                    string originalPhone = hfOriginalPhone.Value;

                    // Validate phone number
                    if (!CustomerService.IsValidPhoneNumber(txtMobilePhone.Text.Trim()))
                    {
                        ShowError("รูปแบบเบอร์โทรศัพท์ไม่ถูกต้อง");
                        return;
                    }

                    // Validate email if provided
                    if (!string.IsNullOrWhiteSpace(txtEmail.Text))
                    {
                        if (!CustomerService.IsValidEmail(txtEmail.Text.Trim()))
                        {
                            ShowError("รูปแบบอีเมลไม่ถูกต้อง");
                            return;
                        }
                    }

                    // Validate Tax ID if provided
                    if (!string.IsNullOrWhiteSpace(txtTaxID.Text))
                    {
                        if (!CustomerService.IsValidTaxID(txtTaxID.Text.Trim()))
                        {
                            ShowError("รูปแบบเลขประจำตัวผู้เสียภาษีไม่ถูกต้อง (ต้องเป็นตัวเลข 13 หลัก)");
                            return;
                        }
                    }

                    // Prepare customer data
                    CustomerData customer = new CustomerData
                    {
                        MobilePhone = txtMobilePhone.Text.Trim(),
                        Name = txtName.Text.Trim(),
                        Email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim(),
                        TaxID = string.IsNullOrWhiteSpace(txtTaxID.Text) ? null : txtTaxID.Text.Trim(),
                        Address = string.IsNullOrWhiteSpace(txtAddress.Text) ? null : txtAddress.Text.Trim(),
                        District = string.IsNullOrWhiteSpace(txtDistrict.Text) ? null : txtDistrict.Text.Trim(),
                        Subdistrict = string.IsNullOrWhiteSpace(txtSubdistrict.Text) ? null : txtSubdistrict.Text.Trim(),
                        Province = string.IsNullOrWhiteSpace(txtProvince.Text) ? null : txtProvince.Text.Trim(),
                        Postcode = string.IsNullOrWhiteSpace(txtPostcode.Text) ? null : txtPostcode.Text.Trim(),
                        CustomerTypeID = string.IsNullOrEmpty(ddlCustomerType.SelectedValue) ? (byte?)null : Convert.ToByte(ddlCustomerType.SelectedValue)
                    };

                    // Save customer using CustomerService
                    short? adminId = GetAdminID();
                    CustomerUpsertResult result = customerService.UpsertCustomer(
                        customer,
                        adminId,
                        "CustomerManagement.aspx"
                    );

                    if (result.Success)
                    {
                        ShowSuccess(result.Message);
                        ClearForm();
                        pnlCustomerForm.Visible = false;
                        LoadCustomers();
                        LoadStatistics();
                    }
                    else
                    {
                        ShowError(result.Message);
                    }
                }
                catch (Exception ex)
                {
                    ShowError("เกิดข้อผิดพลาด: " + ex.Message);
                }
            }
        }

        protected void btnCancelForm_Click(object sender, EventArgs e)
        {
            ClearForm();
            pnlCustomerForm.Visible = false;
        }

        private void ShowAddForm()
        {
            ClearForm();
            lblFormTitle.Text = "เพิ่มลูกค้าใหม่";
            hfEditMode.Value = "false";
            hfOriginalPhone.Value = "";
            pnlCustomerForm.Visible = true;
            txtMobilePhone.Enabled = true;

            // Scroll to form
            ScriptManager.RegisterStartupScript(this, GetType(), "ScrollToForm",
                "window.scrollTo(0, document.getElementById('" + pnlCustomerForm.ClientID + "').offsetTop);", true);
        }

        private void ShowEditForm(string mobilePhone)
        {
            try
            {
                CustomerData customer = customerService.GetCustomer(mobilePhone);

                if (customer != null)
                {
                    lblFormTitle.Text = "แก้ไขข้อมูลลูกค้า";
                    hfEditMode.Value = "true";
                    hfOriginalPhone.Value = mobilePhone;

                    txtMobilePhone.Text = customer.MobilePhone;
                    txtName.Text = customer.Name;
                    txtEmail.Text = customer.Email ?? "";
                    txtTaxID.Text = customer.TaxID ?? "";
                    txtAddress.Text = customer.Address ?? "";
                    txtDistrict.Text = customer.District ?? "";
                    txtSubdistrict.Text = customer.Subdistrict ?? "";
                    txtProvince.Text = customer.Province ?? "";
                    txtPostcode.Text = customer.Postcode ?? "";

                    if (customer.CustomerTypeID.HasValue)
                    {
                        ddlCustomerType.SelectedValue = customer.CustomerTypeID.Value.ToString();
                    }
                    else
                    {
                        ddlCustomerType.SelectedIndex = 0;
                    }

                    pnlCustomerForm.Visible = true;
                    txtMobilePhone.Enabled = false; // Don't allow changing phone number

                    // Scroll to form
                    ScriptManager.RegisterStartupScript(this, GetType(), "ScrollToForm",
                        "window.scrollTo(0, document.getElementById('" + pnlCustomerForm.ClientID + "').offsetTop);", true);
                }
                else
                {
                    ShowError("ไม่พบข้อมูลลูกค้า");
                }
            }
            catch (Exception ex)
            {
                ShowError("ไม่สามารถโหลดข้อมูลลูกค้าได้: " + ex.Message);
            }
        }

        private void ClearForm()
        {
            txtMobilePhone.Text = "";
            txtName.Text = "";
            txtEmail.Text = "";
            txtTaxID.Text = "";
            txtAddress.Text = "";
            txtDistrict.Text = "";
            txtSubdistrict.Text = "";
            txtProvince.Text = "";
            txtPostcode.Text = "";
            ddlCustomerType.SelectedIndex = 0;
            hfEditMode.Value = "false";
            hfOriginalPhone.Value = "";
        }

        #endregion

        #region GridView Events

        protected void gvCustomers_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvCustomers.PageIndex = e.NewPageIndex;
            string searchTerm = string.IsNullOrEmpty(txtSearch.Text) ? null : txtSearch.Text.Trim();
            LoadCustomers(searchTerm);
        }

        protected void gvCustomers_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            string mobilePhone = e.CommandArgument.ToString();

            if (e.CommandName == "EditCustomer")
            {
                ShowEditForm(mobilePhone);
            }
            else if (e.CommandName == "DeleteCustomer")
            {
                DeleteCustomer(mobilePhone);
            }
            else if (e.CommandName == "ViewHistory")
            {
                ShowHistory(mobilePhone);
            }
        }

        #endregion

        #region Delete Customer

        private void DeleteCustomer(string mobilePhone)
        {
            try
            {
                short? adminId = GetAdminID();
                bool success = customerService.DeleteCustomer(
                    mobilePhone,
                    adminId,
                    "ลบโดยผู้ดูแลระบบผ่านหน้าจัดการลูกค้า"
                );

                if (success)
                {
                    ShowSuccess("ลบข้อมูลลูกค้าสำเร็จ");
                    LoadCustomers();
                    LoadStatistics();
                }
                else
                {
                    ShowError("ไม่สามารถลบข้อมูลลูกค้าได้");
                }
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาด: " + ex.Message);
            }
        }

        #endregion

        #region View History

        private void ShowHistory(string mobilePhone)
        {
            try
            {
                DataTable dt = customerService.GetCustomerAuditHistory(mobilePhone, maxResults: 50);

                if (dt != null && dt.Rows.Count > 0)
                {
                    System.Text.StringBuilder html = new System.Text.StringBuilder();
                    html.Append("<div style='max-height: 500px; overflow-y: auto;'>");

                    foreach (DataRow row in dt.Rows)
                    {
                        html.Append("<div class='history-item'>");
                        html.Append("<div class='date'>");
                        html.Append(Convert.ToDateTime(row["ChangeDate"]).ToString("dd/MM/yyyy HH:mm:ss"));
                        html.Append("</div>");

                        html.Append("<div class='action'>");
                        string action = row["Action"].ToString();
                        switch (action)
                        {
                            case "INSERT":
                                html.Append("✅ เพิ่มข้อมูลลูกค้าใหม่");
                                break;
                            case "UPDATE":
                                html.Append("✏️ แก้ไขข้อมูล");
                                break;
                            case "DELETE":
                                html.Append("🗑️ ลบข้อมูล");
                                break;
                            case "MERGE":
                                html.Append("🔄 ผสานข้อมูล");
                                break;
                            default:
                                html.Append(action);
                                break;
                        }
                        html.Append("</div>");

                        html.Append("<div class='details'>");

                        if (row["FieldChanged"] != DBNull.Value)
                        {
                            string field = row["FieldChanged"].ToString();
                            html.Append("<strong>ฟิลด์:</strong> " + field + "<br/>");
                        }

                        if (row["OldValue"] != DBNull.Value && !string.IsNullOrEmpty(row["OldValue"].ToString()))
                        {
                            html.Append("<strong>ค่าเดิม:</strong> " + Server.HtmlEncode(row["OldValue"].ToString()) + "<br/>");
                        }

                        if (row["NewValue"] != DBNull.Value && !string.IsNullOrEmpty(row["NewValue"].ToString()))
                        {
                            html.Append("<strong>ค่าใหม่:</strong> " + Server.HtmlEncode(row["NewValue"].ToString()) + "<br/>");
                        }

                        if (row["ChangedByName"] != DBNull.Value && !string.IsNullOrEmpty(row["ChangedByName"].ToString()))
                        {
                            html.Append("<strong>ผู้แก้ไข:</strong> " + Server.HtmlEncode(row["ChangedByName"].ToString()) + "<br/>");
                        }

                        if (row["ChangedBy_Source"] != DBNull.Value && !string.IsNullOrEmpty(row["ChangedBy_Source"].ToString()))
                        {
                            html.Append("<strong>หน้าที่แก้ไข:</strong> " + Server.HtmlEncode(row["ChangedBy_Source"].ToString()) + "<br/>");
                        }

                        if (row["IPAddress"] != DBNull.Value && !string.IsNullOrEmpty(row["IPAddress"].ToString()))
                        {
                            html.Append("<strong>IP Address:</strong> " + Server.HtmlEncode(row["IPAddress"].ToString()) + "<br/>");
                        }

                        if (row["Notes"] != DBNull.Value && !string.IsNullOrEmpty(row["Notes"].ToString()))
                        {
                            html.Append("<strong>หมายเหตุ:</strong> " + Server.HtmlEncode(row["Notes"].ToString()));
                        }

                        html.Append("</div>");
                        html.Append("</div>");
                    }

                    html.Append("</div>");

                    lblHistoryContent.Text = html.ToString();

                    // Show modal via JavaScript
                    ScriptManager.RegisterStartupScript(this, GetType(), "ShowHistory",
                        "showHistoryModal();", true);
                }
                else
                {
                    lblHistoryContent.Text = "<p style='text-align: center; color: #666; padding: 20px;'>ไม่พบประวัติการแก้ไขข้อมูล</p>";
                    ScriptManager.RegisterStartupScript(this, GetType(), "ShowHistory",
                        "showHistoryModal();", true);
                }
            }
            catch (Exception ex)
            {
                ShowError("ไม่สามารถโหลดประวัติการแก้ไขได้: " + ex.Message);
            }
        }

        #endregion

        #region Alert Messages

        private void ShowSuccess(string message)
        {
            lblSuccess.Text = message;
            pnlSuccess.Visible = true;
            pnlError.Visible = false;
        }

        private void ShowError(string message)
        {
            lblError.Text = message;
            pnlError.Visible = true;
            pnlSuccess.Visible = false;
        }

        #endregion
    }
}
