using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.Admin
{
    public partial class Vendor : System.Web.UI.Page
    {
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        // ✨ Helper Classes for refactored system
        private AddressHelper _addressHelper;
        private CustomerHelper _customerHelper;
        private DocumentHelper _documentHelper;

        protected void Page_Load(object sender, EventArgs e)
        {
            // ✨ Initialize Helper Classes
            _addressHelper = new AddressHelper(conn);
            _customerHelper = new CustomerHelper(conn);
            _documentHelper = new DocumentHelper(conn);

            try
            {
                if (Session["permission"].ToString() == "True")
                {

                    if(!IsPostBack)
                    {
                        DataTable dtCustomerType = code.DatabaseQuery(conn, "Select [Customer_Type],ID From Customer_Type");
                        for (int i = 0; i < dtCustomerType.Rows.Count; i++)
                        {
                            DropDownList1.Items.Add(new ListItem(dtCustomerType.Rows[i][0].ToString(), dtCustomerType.Rows[i][1].ToString()));
                        }
                        DropDownList1.DataBind();

                        DataTable dtVendorCategory = code.DatabaseQuery(conn, "Select DISTINCT [Vendor_Group] From Vendor");
                        for (int i = 0; i < dtVendorCategory.Rows.Count; i++)
                        {
                            DropDownList5.Items.Add(new ListItem(dtVendorCategory.Rows[i][0].ToString(), dtVendorCategory.Rows[i][0].ToString()));
                        }
                        DropDownList5.DataBind();

                        getAddress("SELECT DISTINCT [Province] FROM [Address] order by Province ASC", "SELECT DISTINCT [District] FROM [Address] order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] order by SubDistrict ASC");

                    }
                }
                else
                {
                    Response.Redirect("/Default");
                }
            }
            catch { Response.Redirect("/Default");  }
        }

        protected void TextBox1_TextChanged(object sender, EventArgs e)
        {
            // ✅ รองรับการค้นหาด้วย Tax ID (13 หลัก)
            if(TextBox1.Text.Length == 13)
            {
                // SECURE: Vendor lookup with parameterized query
                var vendorParams = new Dictionary<string, object>
                {
                    { "@IDNumber", TextBox1.Text ?? "" }
                };

                DataTable dt = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Vendor " +
                    "LEFT JOIN Customer_Type ON Customer_Type.ID = Vendor_Type_ID " +
                    "LEFT JOIN Address ON Address.ID = Address_ID " +
                    "WHERE IDNumber = @IDNumber",
                    vendorParams);

                if(dt.Rows.Count > 0)
                {
                    TextBox2.Text = dt.Rows[0]["Name"].ToString();
                    TextBox3.Text = dt.Rows[0]["Branch_Number"].ToString();
                    TextBox7.Text = dt.Rows[0]["Phone_Number"].ToString();
                    TextBox4.Text = dt.Rows[0]["Address"].ToString();
                    TextBox5.Text = dt.Rows[0]["Address1"].ToString();

                    try //Address
                    {
                        TextBox6.Text = dt.Rows[0]["PostalCode"].ToString();
                        DropDownList2.ClearSelection();
                        DropDownList2.Items.FindByText(dt.Rows[0]["Province"].ToString()).Selected = true;
                        DropDownList2.SelectedIndex = DropDownList2.Items.IndexOf(DropDownList2.Items.FindByText(dt.Rows[0]["Province"].ToString()));
                        DropDownList3.ClearSelection();
                        DropDownList3.Items.FindByText(dt.Rows[0]["District"].ToString()).Selected = true;
                        DropDownList3.SelectedIndex = DropDownList3.Items.IndexOf(DropDownList3.Items.FindByText(dt.Rows[0]["District"].ToString()));
                        DropDownList4.ClearSelection();
                        DropDownList4.Items.FindByText(dt.Rows[0]["SubDistrict"].ToString()).Selected = true;
                        DropDownList4.SelectedIndex = DropDownList4.Items.IndexOf(DropDownList4.Items.FindByText(dt.Rows[0]["SubDistrict"].ToString()));

                        DropDownList1.ClearSelection();
                        DropDownList1.Items.FindByValue(dt.Rows[0]["Customer_Type_ID"].ToString()).Selected = true;
                        DropDownList1.SelectedIndex = DropDownList1.Items.IndexOf(DropDownList1.Items.FindByValue(dt.Rows[0]["Customer_Type_ID"].ToString()));

                    }
                    catch { }

                }
            }
        }

        // ✨ MIGRATED: CheckAddressID() has been replaced with AddressHelper.GetAddressIdString()
        // See AddressHelper class in Take_Time_BangPhra.Class namespace

        public void getAddress(string commp, string commd, string commsd)
        {
            string Command = Request.QueryString["Command"];
            string ID = Request.QueryString["ID"];
            DataTable dtProvince = code.DatabaseQuery(conn, commp);
            DataTable dtDistrict = code.DatabaseQuery(conn, commd);
            DataTable dtSubDistrict = code.DatabaseQuery(conn, commsd);
            try
            {
                if (dtProvince.Rows.Count <= 0)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ไม่พบหมายเลขไปรษณีย์ที่คุณระบุ');", true);
                    TextBox6.Enabled = true;
                }
                else
                {

                        List<string> ddl = new List<string>();

                        for (int i = 0; i < dtProvince.Rows.Count; i++)
                        {
                            ddl.Add(dtProvince.Rows[i][0].ToString());
                        }
                        DropDownList2.DataSource = ddl;
                        DropDownList2.DataBind();
                        //DropDownList5.SelectedIndex = 0;

                        ddl.Clear();

                        for (int i = 0; i < dtDistrict.Rows.Count; i++)
                        {
                            ddl.Add(dtDistrict.Rows[i][0].ToString());
                        }
                        DropDownList3.DataSource = ddl;
                        DropDownList3.DataBind();
                        //DropDownList6.SelectedIndex = 0;

                        ddl.Clear();

                        for (int i = 0; i < dtSubDistrict.Rows.Count; i++)
                        {
                            ddl.Add(dtSubDistrict.Rows[i][0].ToString());
                        }
                        DropDownList4.DataSource = ddl;
                        DropDownList4.DataBind();
                        //DropDownList7.SelectedIndex = 0;
                }
            }
            catch { }
        }

        protected void Button6_Click(object sender, EventArgs e)
        {
            TextBox6.Enabled = true;
            TextBox6.Text = string.Empty;
            DropDownList2.Items.Clear();
            DropDownList3.Items.Clear();
            DropDownList4.Items.Clear();
        }

        protected void Button5_Click(object sender, EventArgs e)
        {
            TextBox6.Enabled = false;

            // SECURE: Get address with parameterized query
            var addressParams = new Dictionary<string, object>
            {
                { "@PostalCode", TextBox6.Text ?? "" }
            };

            DataTable dtProvince = code.DatabaseQuerySafe(conn,
                "SELECT DISTINCT [Province] FROM [Address] WHERE PostalCode = @PostalCode ORDER BY Province ASC",
                addressParams);
            DataTable dtDistrict = code.DatabaseQuerySafe(conn,
                "SELECT DISTINCT [District] FROM [Address] WHERE PostalCode = @PostalCode ORDER BY District ASC",
                addressParams);
            DataTable dtSubDistrict = code.DatabaseQuerySafe(conn,
                "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE PostalCode = @PostalCode ORDER BY SubDistrict ASC",
                addressParams);

            populateAddressDropdowns(dtProvince, dtDistrict, dtSubDistrict);
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            // ✅ Validation: ต้องมีชื่อ
            if (string.IsNullOrEmpty(TextBox2.Text.Trim()))
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('⚠️ กรุณากรอกชื่อผู้เสียภาษี / ชื่อบริษัท');", true);
                return;
            }

            // ✅ Validation: ต้องมีเลขสาขา
            if (string.IsNullOrEmpty(TextBox3.Text.Trim()))
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('⚠️ กรุณากรอกเลขสาขา (00000 = สำนักงานใหญ่)');", true);
                return;
            }

            // ✅ Validation: ถ้ามี Tax ID ต้องยาว 13 หลัก
            string taxId = TextBox1.Text.Trim();
            if (!string.IsNullOrEmpty(taxId) && taxId.Length != 13)
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('⚠️ เลขผู้เสียภาษีต้องมี 13 หลัก (หรือเว้นว่างไว้ถ้าไม่มี)');", true);
                return;
            }

            try
            {
                string vendorName = TextBox2.Text.Trim();
                string branchNumber = TextBox3.Text.Trim();
                string addressId = _addressHelper.GetAddressIdString(TextBox6.Text, DropDownList2.SelectedValue, DropDownList3.SelectedValue, DropDownList4.SelectedValue);

                // SECURE: Check duplicate by Name + Branch_Number with parameterized query
                var checkNameParams = new Dictionary<string, object>
                {
                    { "@VendorName", vendorName },
                    { "@BranchNumber", branchNumber }
                };
                DataTable dtCheckName = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Vendor WHERE Name = @VendorName AND Branch_Number = @BranchNumber",
                    checkNameParams);
                bool isDuplicateName = dtCheckName.Rows.Count > 0;

                // SECURE: Check duplicate by IDNumber + Branch_Number with parameterized query (if Tax ID exists)
                bool isDuplicateTaxId = false;
                if (!string.IsNullOrEmpty(taxId))
                {
                    var checkTaxParams = new Dictionary<string, object>
                    {
                        { "@IDNumber", taxId },
                        { "@BranchNumber", branchNumber }
                    };
                    DataTable dtCheckTax = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Vendor WHERE IDNumber = @IDNumber AND Branch_Number = @BranchNumber",
                        checkTaxParams);
                    isDuplicateTaxId = dtCheckTax.Rows.Count > 0;

                    // ⚠️ Check conflict: มี Tax ID ซ้ำแต่ชื่อไม่ตรงกัน
                    if (isDuplicateTaxId && !isDuplicateName)
                    {
                        string existingName = dtCheckTax.Rows[0]["Name"].ToString();
                        ClientScript.RegisterStartupScript(this.GetType(), "conflict",
                            $"alert('⚠️ เลขผู้เสียภาษี {taxId} สาขา {branchNumber} มีอยู่แล้วในชื่อ \"{existingName}\"\\n\\nไม่สามารถใช้เลขผู้เสียภาษีซ้ำกับชื่อต่างกันได้');", true);
                        return;
                    }
                }

                if (isDuplicateName || isDuplicateTaxId)
                {
                    // SECURE: UPDATE existing vendor with parameterized query
                    var updateVendorParams = new Dictionary<string, object>
                    {
                        { "@IDNumber", string.IsNullOrEmpty(taxId) ? (object)DBNull.Value : taxId },
                        { "@VendorTypeID", DropDownList1.SelectedValue },
                        { "@Address", TextBox4.Text.Trim() },
                        { "@Address1", TextBox5.Text.Trim() },
                        { "@AddressID", addressId },
                        { "@PhoneNumber", string.IsNullOrEmpty(TextBox7.Text.Trim()) ? (object)DBNull.Value : TextBox7.Text.Trim() },
                        { "@VendorGroup", DropDownList5.SelectedItem.Text },
                        { "@VendorName", vendorName },
                        { "@BranchNumber", branchNumber }
                    };

                    code.DatabaseInsertSafe(conn,
                        @"UPDATE [dbo].[Vendor] SET
                            [IDNumber] = @IDNumber,
                            [Vendor_Type_ID] = @VendorTypeID,
                            [Address] = @Address,
                            [Address1] = @Address1,
                            [Address_ID] = @AddressID,
                            [Phone_Number] = @PhoneNumber,
                            [Vendor_Group] = @VendorGroup
                        WHERE Name = @VendorName AND Branch_Number = @BranchNumber",
                        updateVendorParams);

                    ClientScript.RegisterStartupScript(this.GetType(), "success", "alert('✅ อัพเดทข้อมูล Vendor สำเร็จ');", true);
                }
                else
                {
                    // SECURE: INSERT new vendor with parameterized query
                    var insertVendorParams = new Dictionary<string, object>
                    {
                        { "@IDNumber", string.IsNullOrEmpty(taxId) ? (object)DBNull.Value : taxId },
                        { "@VendorTypeID", DropDownList1.SelectedValue },
                        { "@VendorName", vendorName },
                        { "@BranchNumber", branchNumber },
                        { "@PhoneNumber", string.IsNullOrEmpty(TextBox7.Text.Trim()) ? (object)DBNull.Value : TextBox7.Text.Trim() },
                        { "@Address", TextBox4.Text.Trim() },
                        { "@Address1", TextBox5.Text.Trim() },
                        { "@AddressID", addressId },
                        { "@VendorGroup", DropDownList5.SelectedItem.Text }
                    };

                    code.DatabaseInsertSafe(conn,
                        @"INSERT INTO [dbo].[Vendor]
                            (IDNumber, Vendor_Type_ID, Name, Branch_Number, Phone_Number, Address, Address1, Address_ID, Vendor_Group)
                        VALUES (@IDNumber, @VendorTypeID, @VendorName, @BranchNumber, @PhoneNumber, @Address, @Address1, @AddressID, @VendorGroup)",
                        insertVendorParams);

                    ClientScript.RegisterStartupScript(this.GetType(), "success", "alert('✅ บันทึกข้อมูล Vendor สำเร็จ');", true);
                }

                // Clear form after save
                Response.Redirect("/Admin/Vendor");
            }
            catch (Exception ex)
            {
                ClientScript.RegisterStartupScript(this.GetType(), "error",
                    $"alert('❌ เกิดข้อผิดพลาด: {ex.Message}');", true);
            }
        }

        protected void TextBox3_TextChanged(object sender, EventArgs e)
        {
            if (TextBox3.Text.Length == 5)
            {
                
            }
            else { ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('เลขสาขาไม่ครบ 5 หลัก');", true); }
        }

        protected void TextBox7_TextChanged(object sender, EventArgs e)
        {
            TextBox7.Text = TextBox7.Text.Replace("-","").Replace(" ", "");
        }

        protected void DropDownList2_SelectedIndexChanged(object sender, EventArgs e)
        {
            DataTable dtProvince, dtDistrict, dtSubDistrict;

            if (TextBox6.Enabled == false)
            {
                // SECURE: Filter by PostalCode and Province with parameterized query
                var addressParams = new Dictionary<string, object>
                {
                    { "@PostalCode", TextBox6.Text ?? "" },
                    { "@Province", DropDownList2.SelectedValue ?? "" }
                };

                dtProvince = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [Province] FROM [Address] WHERE PostalCode = @PostalCode AND Province = @Province ORDER BY Province ASC",
                    addressParams);
                dtDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [District] FROM [Address] WHERE PostalCode = @PostalCode AND Province = @Province ORDER BY District ASC",
                    addressParams);
                dtSubDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE PostalCode = @PostalCode AND Province = @Province ORDER BY SubDistrict ASC",
                    addressParams);
            }
            else
            {
                // SECURE: Filter by Province only with parameterized query
                var addressParams = new Dictionary<string, object>
                {
                    { "@Province", DropDownList2.SelectedValue ?? "" }
                };

                dtProvince = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [Province] FROM [Address] WHERE Province = @Province ORDER BY Province ASC",
                    addressParams);
                dtDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [District] FROM [Address] WHERE Province = @Province ORDER BY District ASC",
                    addressParams);
                dtSubDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE Province = @Province ORDER BY SubDistrict ASC",
                    addressParams);
            }

            populateAddressDropdowns(dtProvince, dtDistrict, dtSubDistrict);
        }

        protected void DropDownList3_SelectedIndexChanged(object sender, EventArgs e)
        {
            DataTable dtProvince, dtDistrict, dtSubDistrict;

            if (TextBox6.Enabled == false)
            {
                // SECURE: Filter by PostalCode and District with parameterized query
                var addressParams = new Dictionary<string, object>
                {
                    { "@PostalCode", TextBox6.Text ?? "" },
                    { "@District", DropDownList3.SelectedValue ?? "" }
                };

                dtProvince = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [Province] FROM [Address] WHERE PostalCode = @PostalCode AND District = @District ORDER BY Province ASC",
                    addressParams);
                dtDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [District] FROM [Address] WHERE PostalCode = @PostalCode AND District = @District ORDER BY District ASC",
                    addressParams);
                dtSubDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE PostalCode = @PostalCode AND District = @District ORDER BY SubDistrict ASC",
                    addressParams);
            }
            else
            {
                // SECURE: Filter by District only with parameterized query
                var addressParams = new Dictionary<string, object>
                {
                    { "@District", DropDownList3.SelectedValue ?? "" }
                };

                dtProvince = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [Province] FROM [Address] WHERE District = @District ORDER BY Province ASC",
                    addressParams);
                dtDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [District] FROM [Address] WHERE District = @District ORDER BY District ASC",
                    addressParams);
                dtSubDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE District = @District ORDER BY SubDistrict ASC",
                    addressParams);
            }

            populateAddressDropdowns(dtProvince, dtDistrict, dtSubDistrict);
        }

        // ✨ SECURE: Helper method to populate address dropdowns
        private void populateAddressDropdowns(DataTable dtProvince, DataTable dtDistrict, DataTable dtSubDistrict)
        {
            try
            {
                if (dtProvince.Rows.Count <= 0)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ไม่พบหมายเลขไปรษณีย์ที่คุณระบุ');", true);
                    TextBox6.Enabled = true;
                }
                else
                {
                    List<string> ddl = new List<string>();

                    for (int i = 0; i < dtProvince.Rows.Count; i++)
                    {
                        ddl.Add(dtProvince.Rows[i][0].ToString());
                    }
                    DropDownList2.DataSource = ddl;
                    DropDownList2.DataBind();

                    ddl.Clear();

                    for (int i = 0; i < dtDistrict.Rows.Count; i++)
                    {
                        ddl.Add(dtDistrict.Rows[i][0].ToString());
                    }
                    DropDownList3.DataSource = ddl;
                    DropDownList3.DataBind();

                    ddl.Clear();

                    for (int i = 0; i < dtSubDistrict.Rows.Count; i++)
                    {
                        ddl.Add(dtSubDistrict.Rows[i][0].ToString());
                    }
                    DropDownList4.DataSource = ddl;
                    DropDownList4.DataBind();
                }
            }
            catch { }
        }
    }
}