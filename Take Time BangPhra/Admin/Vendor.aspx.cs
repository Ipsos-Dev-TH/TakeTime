using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.Admin
{
    public partial class Vendor : System.Web.UI.Page
    {
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private AddressHelper _addressHelper;

        protected void Page_Load(object sender, EventArgs e)
        {
            _addressHelper = new AddressHelper(conn);

            try
            {
                if (Session["permission"]?.ToString() != "True")
                {
                    Response.Redirect("/Default");
                    return;
                }

                if (!IsPostBack)
                {
                    LoadVendorTypes();
                    LoadVendorGroups();
                    LoadSearchGroups();
                    LoadAddress();
                    LoadVendorList();
                }
            }
            catch
            {
                Response.Redirect("/Default");
            }
        }

        #region Load Data

        private void LoadVendorTypes()
        {
            try
            {
                DataTable dtTypes = code.DatabaseQuery(conn, "SELECT [Customer_Type], ID FROM Customer_Type ORDER BY [Customer_Type]");
                ddlVendorType.Items.Clear();
                ddlVendorType.Items.Add(new ListItem("-- เลือกประเภท --", ""));
                foreach (DataRow row in dtTypes.Rows)
                {
                    ddlVendorType.Items.Add(new ListItem(row["Customer_Type"].ToString(), row["ID"].ToString()));
                }
                if (ddlVendorType.Items.Count > 0)
                    ddlVendorType.SelectedIndex = 0;
            }
            catch { }
        }

        private void LoadVendorGroups()
        {
            try
            {
                DataTable dtGroups = code.DatabaseQuery(conn, "SELECT DISTINCT [Vendor_Group] FROM Vendor WHERE [Vendor_Group] IS NOT NULL ORDER BY [Vendor_Group]");
                ddlVendorGroup.Items.Clear();
                ddlVendorGroup.Items.Add(new ListItem("-- เลือกกลุ่ม --", ""));
                foreach (DataRow row in dtGroups.Rows)
                {
                    string group = row["Vendor_Group"].ToString();
                    if (!string.IsNullOrEmpty(group))
                    {
                        ddlVendorGroup.Items.Add(new ListItem(group, group));
                    }
                }
            }
            catch { }
        }

        private void LoadSearchGroups()
        {
            try
            {
                DataTable dtGroups = code.DatabaseQuery(conn, "SELECT DISTINCT [Vendor_Group] FROM Vendor WHERE [Vendor_Group] IS NOT NULL ORDER BY [Vendor_Group]");
                ddlSearchGroup.Items.Clear();
                ddlSearchGroup.Items.Add(new ListItem("ทุกประเภท", ""));
                foreach (DataRow row in dtGroups.Rows)
                {
                    string group = row["Vendor_Group"].ToString();
                    if (!string.IsNullOrEmpty(group))
                    {
                        ddlSearchGroup.Items.Add(new ListItem(group, group));
                    }
                }
            }
            catch { }
        }

        private void LoadAddress()
        {
            try
            {
                DataTable dtProvince = code.DatabaseQuery(conn, "SELECT DISTINCT [Province] FROM [Address] ORDER BY Province ASC");
                ddlProvince.Items.Clear();
                ddlProvince.Items.Add(new ListItem("-- เลือกจังหวัด --", ""));
                foreach (DataRow row in dtProvince.Rows)
                {
                    ddlProvince.Items.Add(new ListItem(row["Province"].ToString(), row["Province"].ToString()));
                }
            }
            catch { }
        }

        private void LoadVendorList(string searchTax = "", string searchName = "", string searchGroup = "")
        {
            try
            {
                string sql = @"
                    SELECT V.*, CT.Customer_Type
                    FROM Vendor V
                    LEFT JOIN Customer_Type CT ON CT.ID = V.Vendor_Type_ID
                    WHERE V.Status = 'True'";

                var parameters = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(searchTax))
                {
                    sql += " AND V.IDNumber = @SearchTax";
                    parameters.Add("@SearchTax", searchTax);
                }

                if (!string.IsNullOrEmpty(searchName))
                {
                    sql += " AND V.Name LIKE @SearchName";
                    parameters.Add("@SearchName", "%" + searchName + "%");
                }

                if (!string.IsNullOrEmpty(searchGroup))
                {
                    sql += " AND V.Vendor_Group = @SearchGroup";
                    parameters.Add("@SearchGroup", searchGroup);
                }

                sql += " ORDER BY V.Name";

                DataTable dt = code.DatabaseQuerySafe(conn, sql, parameters);

                // Add Province column to DataTable
                if (!dt.Columns.Contains("Province"))
                {
                    dt.Columns.Add("Province", typeof(string));
                }

                // Join with Address table to get province
                foreach (DataRow row in dt.Rows)
                {
                    if (row["Address_ID"] != DBNull.Value && !string.IsNullOrEmpty(row["Address_ID"].ToString()))
                    {
                        var addressParams = new Dictionary<string, object>
                        {
                            { "@AddressID", row["Address_ID"] }
                        };
                        DataTable dtAddress = code.DatabaseQuerySafe(conn,
                            "SELECT Province FROM Address WHERE ID = @AddressID", addressParams);

                        if (dtAddress.Rows.Count > 0)
                        {
                            row["Province"] = dtAddress.Rows[0]["Province"];
                        }
                    }
                }

                gvVendors.DataSource = dt;
                gvVendors.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดรายการ Vendor: " + ex.Message, "error");
            }
        }

        #endregion

        #region Search Events

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            LoadVendorList(txtSearchTax.Text.Trim(), txtSearchName.Text.Trim(), ddlSearchGroup.SelectedValue);
        }

        protected void btnClearSearch_Click(object sender, EventArgs e)
        {
            txtSearchTax.Text = "";
            txtSearchName.Text = "";
            ddlSearchGroup.SelectedIndex = 0;
            LoadVendorList();
        }

        protected void txtSearchTax_TextChanged(object sender, EventArgs e)
        {
            if (txtSearchTax.Text.Length == 13)
            {
                LoadVendorList(txtSearchTax.Text.Trim(), "", "");
            }
        }

        #endregion

        #region Form Events

        protected void btnNewVendor_Click(object sender, EventArgs e)
        {
            ClearForm();
            lblFormTitle.Text = "เพิ่ม Vendor ใหม่";
            pnlVendorForm.Visible = true;
            pnlVendorList.Visible = false;
            hfVendorID.Value = "";
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            pnlVendorForm.Visible = false;
            pnlVendorList.Visible = true;
            ClearForm();
        }

        protected void txtTaxID_TextChanged(object sender, EventArgs e)
        {
            if (txtTaxID.Text.Trim().Length == 13)
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@IDNumber", txtTaxID.Text.Trim() }
                };

                DataTable dt = code.DatabaseQuerySafe(conn,
                    @"SELECT V.*, A.Province, A.District, A.SubDistrict, A.PostalCode
                      FROM Vendor V
                      LEFT JOIN Address A ON A.ID = V.Address_ID
                      WHERE V.IDNumber = @IDNumber AND V.Status = 'True'",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    LoadVendorToForm(dt.Rows[0]);
                }
            }
        }

        #endregion

        #region GridView Events

        protected void gvVendors_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            string vendorId = e.CommandArgument.ToString();

            if (e.CommandName == "EditVendor")
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@VendorID", vendorId }
                };

                DataTable dt = code.DatabaseQuerySafe(conn,
                    @"SELECT V.*, A.Province, A.District, A.SubDistrict, A.PostalCode
                      FROM Vendor V
                      LEFT JOIN Address A ON A.ID = V.Address_ID
                      WHERE V.ID = @VendorID",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    LoadVendorToForm(dt.Rows[0]);
                    lblFormTitle.Text = "แก้ไขข้อมูล Vendor";
                    hfVendorID.Value = vendorId;
                    pnlVendorForm.Visible = true;
                    pnlVendorList.Visible = false;
                }
            }
            else if (e.CommandName == "DeleteVendor")
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@VendorID", vendorId }
                };

                code.DatabaseInsertSafe(conn,
                    "UPDATE Vendor SET Status = 'False' WHERE ID = @VendorID",
                    parameters);

                ShowMessage("ลบข้อมูล Vendor สำเร็จ", "success");
                LoadVendorList();
            }
        }

        #endregion

        #region Address Events

        protected void btnSearchPostal_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtPostalCode.Text) || txtPostalCode.Text.Length != 5)
            {
                ShowMessage("กรุณากรอกรหัสไปรษณีย์ 5 หลัก", "error");
                return;
            }

            var parameters = new Dictionary<string, object>
            {
                { "@PostalCode", txtPostalCode.Text }
            };

            DataTable dtProvince = code.DatabaseQuerySafe(conn,
                "SELECT DISTINCT [Province] FROM [Address] WHERE PostalCode = @PostalCode ORDER BY Province ASC",
                parameters);
            DataTable dtDistrict = code.DatabaseQuerySafe(conn,
                "SELECT DISTINCT [District] FROM [Address] WHERE PostalCode = @PostalCode ORDER BY District ASC",
                parameters);
            DataTable dtSubDistrict = code.DatabaseQuerySafe(conn,
                "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE PostalCode = @PostalCode ORDER BY SubDistrict ASC",
                parameters);

            if (dtProvince.Rows.Count == 0)
            {
                ShowMessage("ไม่พบรหัสไปรษณีย์นี้ในระบบ", "error");
                return;
            }

            PopulateAddressDropdowns(dtProvince, dtDistrict, dtSubDistrict);
        }

        protected void btnClearPostal_Click(object sender, EventArgs e)
        {
            txtPostalCode.Text = "";
            LoadAddress();
            ddlDistrict.Items.Clear();
            ddlDistrict.Items.Add(new ListItem("-- เลือกอำเภอ/เขต --", ""));
            ddlSubDistrict.Items.Clear();
            ddlSubDistrict.Items.Add(new ListItem("-- เลือกตำบล/แขวง --", ""));
        }

        protected void ddlProvince_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ddlProvince.SelectedValue))
                return;

            var parameters = new Dictionary<string, object>
            {
                { "@Province", ddlProvince.SelectedValue }
            };

            if (!string.IsNullOrEmpty(txtPostalCode.Text))
                parameters.Add("@PostalCode", txtPostalCode.Text);

            string sql = !string.IsNullOrEmpty(txtPostalCode.Text)
                ? "SELECT DISTINCT [District] FROM [Address] WHERE Province = @Province AND PostalCode = @PostalCode ORDER BY District ASC"
                : "SELECT DISTINCT [District] FROM [Address] WHERE Province = @Province ORDER BY District ASC";

            DataTable dtDistrict = code.DatabaseQuerySafe(conn, sql, parameters);

            ddlDistrict.Items.Clear();
            ddlDistrict.Items.Add(new ListItem("-- เลือกอำเภอ/เขต --", ""));
            foreach (DataRow row in dtDistrict.Rows)
            {
                ddlDistrict.Items.Add(new ListItem(row["District"].ToString(), row["District"].ToString()));
            }

            ddlSubDistrict.Items.Clear();
            ddlSubDistrict.Items.Add(new ListItem("-- เลือกตำบล/แขวง --", ""));
        }

        protected void ddlDistrict_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ddlDistrict.SelectedValue))
                return;

            var parameters = new Dictionary<string, object>
            {
                { "@Province", ddlProvince.SelectedValue },
                { "@District", ddlDistrict.SelectedValue }
            };

            if (!string.IsNullOrEmpty(txtPostalCode.Text))
                parameters.Add("@PostalCode", txtPostalCode.Text);

            string sql = !string.IsNullOrEmpty(txtPostalCode.Text)
                ? "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE Province = @Province AND District = @District AND PostalCode = @PostalCode ORDER BY SubDistrict ASC"
                : "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE Province = @Province AND District = @District ORDER BY SubDistrict ASC";

            DataTable dtSubDistrict = code.DatabaseQuerySafe(conn, sql, parameters);

            ddlSubDistrict.Items.Clear();
            ddlSubDistrict.Items.Add(new ListItem("-- เลือกตำบล/แขวง --", ""));
            foreach (DataRow row in dtSubDistrict.Rows)
            {
                ddlSubDistrict.Items.Add(new ListItem(row["SubDistrict"].ToString(), row["SubDistrict"].ToString()));
            }
        }

        #endregion

        #region Save/Update

        protected void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                // Validation
                if (string.IsNullOrEmpty(txtVendorName.Text.Trim()))
                {
                    ShowMessage("กรุณากรอกชื่อ Vendor", "error");
                    return;
                }

                if (string.IsNullOrEmpty(ddlVendorType.SelectedValue))
                {
                    ShowMessage("กรุณาเลือกประเภท Vendor", "error");
                    return;
                }

                // 🔧 FIX: Branch number validation based on vendor type
                // - นิติบุคคล (Corporate, Type ID = 1): ต้องกรอกเลขสาขา 5 หลัก
                // - บุคคลธรรมดา (Individual, Type ID = 2): ไม่จำเป็นต้องกรอก ใช้ค่าเริ่มต้น "00000"
                string vendorTypeId = ddlVendorType.SelectedValue;
                bool isCorporate = vendorTypeId == "1"; // 1 = นิติบุคคล

                string branchNumber = txtBranchNumber.Text.Trim();
                if (isCorporate)
                {
                    // นิติบุคคลต้องกรอกเลขสาขา 5 หลัก
                    if (string.IsNullOrEmpty(branchNumber) || branchNumber.Length != 5)
                    {
                        ShowMessage("กรุณากรอกเลขสาขา 5 หลัก (สำหรับนิติบุคคล)", "error");
                        return;
                    }
                }
                else
                {
                    // บุคคลธรรมดา: ใช้ค่าเริ่มต้น "00000" ถ้าไม่ได้กรอก
                    if (string.IsNullOrEmpty(branchNumber))
                    {
                        branchNumber = "00000";
                    }
                }

                string taxId = txtTaxID.Text.Trim();
                if (!string.IsNullOrEmpty(taxId) && taxId.Length != 13)
                {
                    ShowMessage("เลขผู้เสียภาษีต้องมี 13 หลัก", "error");
                    return;
                }

                // Get Address ID
                string addressId = _addressHelper.GetAddressIdString(
                    txtPostalCode.Text.Trim(),
                    ddlProvince.SelectedValue,
                    ddlDistrict.SelectedValue,
                    ddlSubDistrict.SelectedValue);

                string vendorId = hfVendorID.Value;
                bool isUpdate = !string.IsNullOrEmpty(vendorId);

                string email = txtEmail.Text.Trim();
                string contactPerson = txtContactPerson.Text.Trim();
                string remark = txtRemark.Text.Trim();

                var parameters = new Dictionary<string, object>
                {
                    { "@IDNumber", string.IsNullOrEmpty(taxId) ? (object)DBNull.Value : taxId },
                    { "@VendorTypeID", vendorTypeId },
                    { "@Name", txtVendorName.Text.Trim() },
                    { "@BranchNumber", branchNumber },
                    { "@PhoneNumber", string.IsNullOrEmpty(txtPhone.Text.Trim()) ? (object)DBNull.Value : txtPhone.Text.Trim() },
                    { "@Address", txtAddress.Text.Trim() },
                    { "@Address1", txtAddress1.Text.Trim() },
                    { "@AddressID", string.IsNullOrEmpty(addressId) ? (object)DBNull.Value : addressId },
                    { "@VendorGroup", string.IsNullOrEmpty(ddlVendorGroup.SelectedValue) ? (object)DBNull.Value : ddlVendorGroup.SelectedValue },
                    { "@Email", string.IsNullOrEmpty(email) ? (object)DBNull.Value : email },
                    { "@ContactPerson", string.IsNullOrEmpty(contactPerson) ? (object)DBNull.Value : contactPerson },
                    { "@Remark", string.IsNullOrEmpty(remark) ? (object)DBNull.Value : remark }
                };

                if (isUpdate)
                {
                    parameters.Add("@VendorID", vendorId);
                    code.DatabaseInsertSafe(conn,
                        @"UPDATE [dbo].[Vendor] SET
                            [IDNumber] = @IDNumber,
                            [Vendor_Type_ID] = @VendorTypeID,
                            [Name] = @Name,
                            [Branch_Number] = @BranchNumber,
                            [Phone_Number] = @PhoneNumber,
                            [Address] = @Address,
                            [Address1] = @Address1,
                            [Address_ID] = @AddressID,
                            [Vendor_Group] = @VendorGroup,
                            [Email] = @Email,
                            [Contact_Person] = @ContactPerson,
                            [Remark] = @Remark
                          WHERE ID = @VendorID",
                        parameters);

                    ShowMessage("อัพเดทข้อมูล Vendor สำเร็จ", "success");
                }
                else
                {
                    parameters.Add("@Status", "True");
                    code.DatabaseInsertSafe(conn,
                        @"INSERT INTO [dbo].[Vendor]
                            (IDNumber, Vendor_Type_ID, Name, Branch_Number, Phone_Number,
                             Address, Address1, Address_ID, Vendor_Group, Status,
                             Email, Contact_Person, Remark)
                          VALUES (@IDNumber, @VendorTypeID, @Name, @BranchNumber, @PhoneNumber,
                                  @Address, @Address1, @AddressID, @VendorGroup, @Status,
                                  @Email, @ContactPerson, @Remark)",
                        parameters);

                    ShowMessage("บันทึกข้อมูล Vendor สำเร็จ", "success");
                }

                pnlVendorForm.Visible = false;
                pnlVendorList.Visible = true;
                ClearForm();
                LoadVendorList();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        #endregion

        #region Helper Methods

        private void ClearForm()
        {
            txtTaxID.Text = "";
            txtVendorName.Text = "";
            txtBranchNumber.Text = "00000";
            txtPhone.Text = "";
            txtEmail.Text = "";
            txtContactPerson.Text = "";
            txtAddress.Text = "";
            txtAddress1.Text = "";
            txtPostalCode.Text = "";
            txtRemark.Text = "";

            if (ddlVendorType.Items.Count > 0)
                ddlVendorType.SelectedIndex = 0;
            if (ddlVendorGroup.Items.Count > 0)
                ddlVendorGroup.SelectedIndex = 0;

            LoadAddress();
            ddlDistrict.Items.Clear();
            ddlDistrict.Items.Add(new ListItem("-- เลือกอำเภอ/เขต --", ""));
            ddlSubDistrict.Items.Clear();
            ddlSubDistrict.Items.Add(new ListItem("-- เลือกตำบล/แขวง --", ""));

            hfVendorID.Value = "";
        }

        private void LoadVendorToForm(DataRow row)
        {
            txtTaxID.Text = row["IDNumber"] != DBNull.Value ? row["IDNumber"].ToString() : "";
            txtVendorName.Text = row["Name"].ToString();
            txtBranchNumber.Text = row["Branch_Number"].ToString();
            txtPhone.Text = row["Phone_Number"] != DBNull.Value ? row["Phone_Number"].ToString() : "";
            txtAddress.Text = row["Address"] != DBNull.Value ? row["Address"].ToString() : "";
            txtAddress1.Text = row["Address1"] != DBNull.Value ? row["Address1"].ToString() : "";

            if (row.Table.Columns.Contains("Email"))
                txtEmail.Text = row["Email"] != DBNull.Value ? row["Email"].ToString() : "";
            if (row.Table.Columns.Contains("Contact_Person"))
                txtContactPerson.Text = row["Contact_Person"] != DBNull.Value ? row["Contact_Person"].ToString() : "";
            if (row.Table.Columns.Contains("Remark"))
                txtRemark.Text = row["Remark"] != DBNull.Value ? row["Remark"].ToString() : "";

            if (ddlVendorType.Items.FindByValue(row["Vendor_Type_ID"].ToString()) != null)
            {
                ddlVendorType.SelectedValue = row["Vendor_Type_ID"].ToString();
            }

            if (row["Vendor_Group"] != DBNull.Value && !string.IsNullOrEmpty(row["Vendor_Group"].ToString()))
            {
                if (ddlVendorGroup.Items.FindByValue(row["Vendor_Group"].ToString()) == null)
                {
                    ddlVendorGroup.Items.Add(new ListItem(row["Vendor_Group"].ToString(), row["Vendor_Group"].ToString()));
                }
                ddlVendorGroup.SelectedValue = row["Vendor_Group"].ToString();
            }

            // Load Address
            if (row.Table.Columns.Contains("Province") && row["Province"] != DBNull.Value)
            {
                txtPostalCode.Text = row["PostalCode"] != DBNull.Value ? row["PostalCode"].ToString() : "";

                if (ddlProvince.Items.FindByValue(row["Province"].ToString()) != null)
                {
                    ddlProvince.SelectedValue = row["Province"].ToString();
                    ddlProvince_SelectedIndexChanged(null, null);

                    if (row["District"] != DBNull.Value && ddlDistrict.Items.FindByValue(row["District"].ToString()) != null)
                    {
                        ddlDistrict.SelectedValue = row["District"].ToString();
                        ddlDistrict_SelectedIndexChanged(null, null);

                        if (row["SubDistrict"] != DBNull.Value && ddlSubDistrict.Items.FindByValue(row["SubDistrict"].ToString()) != null)
                        {
                            ddlSubDistrict.SelectedValue = row["SubDistrict"].ToString();
                        }
                    }
                }
            }
        }

        private void PopulateAddressDropdowns(DataTable dtProvince, DataTable dtDistrict, DataTable dtSubDistrict)
        {
            ddlProvince.Items.Clear();
            ddlProvince.Items.Add(new ListItem("-- เลือกจังหวัด --", ""));
            foreach (DataRow row in dtProvince.Rows)
            {
                ddlProvince.Items.Add(new ListItem(row["Province"].ToString(), row["Province"].ToString()));
            }

            ddlDistrict.Items.Clear();
            ddlDistrict.Items.Add(new ListItem("-- เลือกอำเภอ/เขต --", ""));
            foreach (DataRow row in dtDistrict.Rows)
            {
                ddlDistrict.Items.Add(new ListItem(row["District"].ToString(), row["District"].ToString()));
            }

            ddlSubDistrict.Items.Clear();
            ddlSubDistrict.Items.Add(new ListItem("-- เลือกตำบล/แขวง --", ""));
            foreach (DataRow row in dtSubDistrict.Rows)
            {
                ddlSubDistrict.Items.Add(new ListItem(row["SubDistrict"].ToString(), row["SubDistrict"].ToString()));
            }

            if (dtProvince.Rows.Count > 0)
                ddlProvince.SelectedIndex = 1;
            if (dtDistrict.Rows.Count > 0)
                ddlDistrict.SelectedIndex = 1;
            if (dtSubDistrict.Rows.Count > 0)
                ddlSubDistrict.SelectedIndex = 1;
        }

        protected string GetShortAddress(object address, object province)
        {
            string addr = address != DBNull.Value ? address.ToString() : "";
            string prov = province != DBNull.Value ? province.ToString() : "";

            if (string.IsNullOrEmpty(addr) && string.IsNullOrEmpty(prov))
                return "-";

            if (!string.IsNullOrEmpty(addr) && addr.Length > 50)
                addr = addr.Substring(0, 47) + "...";

            return !string.IsNullOrEmpty(prov) ? $"{addr} ({prov})" : addr;
        }

        private void ShowMessage(string message, string type)
        {
            string script = $"alert('{message.Replace("'", "\\'")}');";
            ClientScript.RegisterStartupScript(this.GetType(), "alert", script, true);
        }

        #endregion
    }
}
