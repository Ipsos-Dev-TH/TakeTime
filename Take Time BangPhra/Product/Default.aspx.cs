using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using System.IO;
using Microsoft.Ajax.Utilities;
using Take_Time_BangPhra.Account.Report;
using System.Security.Cryptography;
using ECertificateAPI;
using System.Net.Mail;
using Microsoft.Reporting.WebForms;
using System.Web.UI.HtmlControls;
using Take_Time_BangPhra.Class;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Product
{
    public partial class Default : System.Web.UI.Page
    {
        _Default codeDefault = new _Default();
        Receipt codeReceipt = new Receipt();
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private const int PrimaryInterval = 300000; // 5 minutes = 300000 ms
        private const int SecondaryInterval = 60000; // 1 minute = 60000 ms

        // ✨ Helper Classes for refactored system
        private AddressHelper _addressHelper;
        private CustomerHelper _customerHelper;
        private DocumentHelper _documentHelper;

        // 🏨 Room Charge Feature
        private RoomChargeService _roomChargeService;
        private RoomChargeDataAccess _roomChargeDA;

        protected void Page_Load(object sender, EventArgs e)
        {
            // ✨ Initialize Helper Classes
            _addressHelper = new AddressHelper(conn);
            _customerHelper = new CustomerHelper(conn);
            _documentHelper = new DocumentHelper(conn);

            // 🏨 Initialize Room Charge services
            _roomChargeService = new RoomChargeService(conn);
            _roomChargeDA = new RoomChargeDataAccess(conn);

            try
            {
                try
                {
                    if (Session["permission"].ToString() == "True" && (Session["User"].ToString() == "Owner" || Session["User"].ToString() == "Admin"))
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

                    Page.SetFocus(TextBox1);
                    this.TextBox1.Attributes.Add("onkeypress", "button_click(this,'" + this.Button3.ClientID + "')");
                    TextBox12.Text = DateTime.Now.ToString("yyyy-MM-dd");
                    DataTable dtOrder = new DataTable();
                    try
                    {
                        dtOrder.Columns.Add("ID");
                        dtOrder.Columns.Add("Barcode");
                        dtOrder.Columns.Add("Product_Name");
                        dtOrder.Columns.Add("Amount");
                        dtOrder.Columns.Add("Sell_Price");
                        dtOrder.Columns.Add("Price_Total");
                        dtOrder.Columns.Add("Category_ID");
                        Session["dtOrder"] = dtOrder;
                    }
                    catch { }
                    string yourHTMLstring = "<script> var Material_Name = [";
                    DataTable dt = code.DatabaseQuery(conn, "SELECT Distinct(Product_Name) as Material_Name FROM [Taketime].[dbo].[Product] Where [Status] = 'True'");
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        yourHTMLstring += "\"" + dt.Rows[i][0].ToString().Replace(",", "") + "\"";
                        if (i < dt.Rows.Count - 1)
                        {
                            yourHTMLstring += ",";
                        }
                    }
                    yourHTMLstring += "];\r\nautocomplete(document.getElementById(\"MainContent_TextBox1\"), Material_Name);</script>";
                    Literal1.Text = yourHTMLstring;
                    CheckData();

                    // 🏨 Load active guests for room charge dropdown
                    LoadActiveGuests();
                }
                else
                {
                    renderProduct();
                }
            }

            catch(Exception ex)
            {
                //Response.Redirect("/Default");
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('"+ex+"');", true);
            }

        }

        /// <summary>
        /// Page_PreRender - Apply pending customer data after Panel1 is visible
        /// This fixes the issue where dropdown binding doesn't work when Panel.Visible = false
        /// </summary>
        protected void Page_PreRender(object sender, EventArgs e)
        {
            // Apply pending customer data if Panel1 is now visible
            if (Panel1.Visible && Session["PendingCustomerData"] != null)
            {
                try
                {
                    DataTable dtCustomer = (DataTable)Session["PendingCustomerData"];
                    if (dtCustomer != null && dtCustomer.Rows.Count > 0)
                    {
                        ApplyCustomerData(dtCustomer);
                        Session["PendingCustomerData"] = null; // Clear after applying
                    }
                }
                catch { }
            }
        }

        public void renderProduct()
        {
            // SECURE: Use parameterized query to prevent SQL Injection
            var parameters = new Dictionary<string, object>
            {
                { "@ProductName", TextBox1.Text ?? "" },
                { "@Barcode", TextBox1.Text ?? "" }
            };

            DataTable dtProduct = code.DatabaseQuerySafe(conn,
                "SELECT * FROM [Taketime].[dbo].[Product] " +
                "WHERE [Product_Name] = @ProductName OR Barcode = @Barcode",
                parameters);
            if (dtProduct.Rows.Count > 0)
            {
                DataTable dtOrder = (DataTable)Session["dtOrder"];
                if (dtOrder.Rows.Count == 0)
                {
                    int amount = 1;
                    double total = amount * Convert.ToDouble(dtProduct.Rows[0]["Sell_Price"].ToString());
                    dtOrder.Rows.Add(dtProduct.Rows[0]["ID"].ToString(), dtProduct.Rows[0]["Barcode"].ToString(), dtProduct.Rows[0]["Product_Name"].ToString(), amount, dtProduct.Rows[0]["Sell_Price"].ToString(), total, dtProduct.Rows[0]["Category_ID"].ToString());
                }
                else
                {
                    int rowid = 0;
                    int checkdup = 0;
                    for (int i = 0; i < dtOrder.Rows.Count; i++)
                    {
                        if (dtOrder.Rows[i]["Product_Name"].ToString() == dtProduct.Rows[0]["Product_Name"].ToString())
                        {
                            checkdup = 1;
                            rowid = i;
                        }

                    }
                    if (checkdup == 0)
                    {
                        int amount = 1;
                        double total = amount * Convert.ToDouble(dtProduct.Rows[0]["Sell_Price"].ToString());
                        dtOrder.Rows.Add(dtProduct.Rows[0]["ID"].ToString(), dtProduct.Rows[0]["Barcode"].ToString(), dtProduct.Rows[0]["Product_Name"].ToString(), amount, dtProduct.Rows[0]["Sell_Price"].ToString(), total, dtProduct.Rows[0]["Category_ID"].ToString());

                    }
                    else
                    {
                        int amount = Convert.ToInt32(dtOrder.Rows[rowid]["Amount"].ToString());
                        amount = amount + 1;
                        double total = amount * Convert.ToDouble(dtProduct.Rows[0]["Sell_Price"].ToString());
                        dtOrder.Rows[rowid]["Amount"] = amount;
                        dtOrder.Rows[rowid]["Price_Total"] = total;
                    }
                    
                }
                GridView1.DataSource = dtOrder;
                GridView1.DataBind();
                Session["dtOrder"] = dtOrder;
                TextBox1.Text = string.Empty;
                TextBox1.Focus();

                double pricetotal = 0;
                for (int i = 0; i < dtOrder.Rows.Count; i++)
                {
                    pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
                }
                TextBox2.Text = pricetotal.ToString();
            }
        }

        // ✨ MIGRATED: CheckAddressID() method has been replaced with AddressHelper.GetAddressIdString()
        // See AddressHelper class in Take_Time_BangPhra.Class namespace



        protected void CheckBox1_CheckedChanged1(object sender, EventArgs e)
        {
            
        }

        protected void CheckBox2_CheckedChanged(object sender, EventArgs e)
        {
            if(CheckBox2.Checked == true)
            {
                CheckBox1.Checked = true;
                Panel1.Visible = true;

                // 🐛 Debug start
                string debugMsg = "CheckBox2_CheckedChanged triggered\\n";

                // ✅ Auto-fill customer data from selected guest reservation
                if (ddlGuestReservation.SelectedValue != "0")
                {
                    debugMsg += $"Guest Reservation ID: {ddlGuestReservation.SelectedValue}\\n";
                    int reservationId = Convert.ToInt32(ddlGuestReservation.SelectedValue);
                    FillCustomerDataFromReservation(reservationId);

                    // 📝 Apply dropdown bindings immediately after Panel1 is visible and data is filled
                    if (Session["PendingCustomerData"] != null)
                    {
                        debugMsg += "Session[PendingCustomerData] is NOT null\\n";
                        try
                        {
                            DataTable dtCustomer = (DataTable)Session["PendingCustomerData"];
                            if (dtCustomer != null && dtCustomer.Rows.Count > 0)
                            {
                                debugMsg += $"DataTable has {dtCustomer.Rows.Count} rows\\n";
                                ApplyCustomerData(dtCustomer);
                                Session["PendingCustomerData"] = null; // Clear after applying
                            }
                            else
                            {
                                debugMsg += "DataTable is null or empty\\n";
                            }
                        }
                        catch (Exception ex)
                        {
                            debugMsg += $"Error applying data: {ex.Message}\\n";
                        }
                    }
                    else
                    {
                        debugMsg += "Session[PendingCustomerData] is NULL!\\n";
                    }
                }
                else
                {
                    debugMsg += "No guest reservation selected\\n";
                }

                // 🐛 Debug message (commented out - working correctly)
                // ClientScript.RegisterStartupScript(this.GetType(), "checkbox_debug", $"alert('{debugMsg}');", true);
            }
            else
            {
                Panel1.Visible = false;
            }
        }

        /// <summary>
        /// Fill customer data from reservation for tax invoice
        /// </summary>
        private void FillCustomerDataFromReservation(int reservationId)
        {
            try
            {
                // Get selected date from TextBox12, default to today if empty/invalid
                DateTime searchDate = DateTime.Now.Date;
                if (!string.IsNullOrEmpty(TextBox12.Text))
                {
                    DateTime.TryParse(TextBox12.Text, out searchDate);
                }

                var dt = _roomChargeDA.GetReservationById(reservationId, searchDate);

                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    string customerPhone = row["CustomerPhone"]?.ToString() ?? "";

                    if (!string.IsNullOrEmpty(customerPhone))
                    {
                        TextBox3.Text = customerPhone;
                        // SECURE: Use parameterized query to prevent SQL Injection
                        fillDataByPhone(customerPhone);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't break the flow
                code.Logs(conn, "Fill Customer Data Error", ex.Message, "SYSTEM");
            }
        }

        /// <summary>
        /// Charge mode changed - enable/disable payment method dropdown
        /// </summary>
        protected void rblChargeMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (rblChargeMode.SelectedValue == "ROOM_CHARGE")
            {
                // ชาร์จเข้าห้อง - ไม่ต้องเลือกวิธีชำระตอนนี้
                DropDownList1.Enabled = false;
                DropDownList1.SelectedIndex = 0; // Reset to default
                CheckBox1.Enabled = false;
                CheckBox1.Checked = false;
                CheckBox2.Enabled = false;
                CheckBox2.Checked = false;
                Panel1.Visible = false;
            }
            else // PAY_NOW
            {
                // ชำระทันที - ต้องเลือกวิธีชำระ
                DropDownList1.Enabled = true;
                CheckBox1.Enabled = true;
                CheckBox2.Enabled = true;
            }
        }

        protected void TextBox9_TextChanged(object sender, EventArgs e)
        {
            if(TextBox9.Text.Length == 5)
            {
                // SECURE: Use LoadAddressDropdownsByPostalCode instead of building SQL strings
                LoadAddressDropdownsByPostalCode(TextBox9.Text);
            }
        }

        /// <summary>
        /// DEPRECATED: This method assumes pre-built SQL queries are safe.
        /// Callers should use LoadAddressDropdownsByPostalCode instead.
        /// Kept for backward compatibility but queries passed here should already be parameterized.
        /// </summary>
        public void getAddress(string commp, string commd, string commsd)
        {
            string Command = Request.QueryString["Command"];
            string ID = Request.QueryString["ID"];

            // NOTE: This method receives SQL query strings as parameters.
            // The queries should be parameterized at the caller level.
            // If they contain user input concatenation, it's a security issue at the caller.
            DataTable dtProvince = code.DatabaseQuery(conn, commp);
            DataTable dtDistrict = code.DatabaseQuery(conn, commd);
            DataTable dtSubDistrict = code.DatabaseQuery(conn, commsd);
            try
            {
                if (dtProvince.Rows.Count <= 0)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ไม่พบหมายเลขไปรษณีย์ที่คุณระบุ');", true);
                }
                else
                {
                    if (Button2.Enabled == true || Command == "View" || Command == "Edit")
                    {
                        LoadAddressDropdowns(dtProvince, dtDistrict, dtSubDistrict);
                    }
                    else { }
                }
            }
            catch { }
        }

        /// <summary>
        /// ✨ REFACTORED: Load address dropdowns using AddressHelper
        /// </summary>
        private void LoadAddressDropdownsByPostalCode(string postalCode)
        {
            if (string.IsNullOrEmpty(postalCode) || postalCode.Length != 5)
                return;

            try
            {
                // ✨ Use AddressHelper to populate all dropdowns at once
                _addressHelper.PopulateAddressDropdowns(
                    postalCode,
                    DropDownList3,  // Province
                    DropDownList4,  // District
                    DropDownList5   // SubDistrict
                );
            }
            catch { }
        }

        /// <summary>
        /// Populate address dropdowns from DataTables
        /// </summary>
        private void LoadAddressDropdowns(DataTable dtProvince, DataTable dtDistrict, DataTable dtSubDistrict)
        {
            List<string> ddl = new List<string>();

            for (int i = 0; i < dtProvince.Rows.Count; i++)
            {
                ddl.Add(dtProvince.Rows[i][0].ToString());
            }
            DropDownList3.DataSource = ddl;
            DropDownList3.DataBind();

            ddl.Clear();

            for (int i = 0; i < dtDistrict.Rows.Count; i++)
            {
                ddl.Add(dtDistrict.Rows[i][0].ToString());
            }
            DropDownList4.DataSource = ddl;
            DropDownList4.DataBind();

            ddl.Clear();

            for (int i = 0; i < dtSubDistrict.Rows.Count; i++)
            {
                ddl.Add(dtSubDistrict.Rows[i][0].ToString());
            }
            DropDownList5.DataSource = ddl;
            DropDownList5.DataBind();
        }

        /// <summary>
        /// SECURE: Load address dropdowns by province/district/subdistrict using parameterized queries
        /// </summary>
        private void LoadAddressDropdownsByLocation(string province, string district, string subDistrict)
        {
            try
            {
                // Build queries based on available data (SECURE version with parameters)
                string provinceQuery = "SELECT DISTINCT [Province] FROM [Address] WHERE 1=1 ORDER BY Province ASC";
                string districtQuery = "SELECT DISTINCT [District] FROM [Address] WHERE 1=1";
                string subDistrictQuery = "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE 1=1";

                var districtParams = new Dictionary<string, object>();
                var subDistrictParams = new Dictionary<string, object>();

                // Add conditions based on available data
                if (!string.IsNullOrEmpty(province))
                {
                    districtQuery += " AND Province = @Province";
                    subDistrictQuery += " AND Province = @Province";
                    districtParams["@Province"] = province;
                    subDistrictParams["@Province"] = province;
                }

                if (!string.IsNullOrEmpty(district))
                {
                    subDistrictQuery += " AND District = @District";
                    subDistrictParams["@District"] = district;
                }

                districtQuery += " ORDER BY District ASC";
                subDistrictQuery += " ORDER BY SubDistrict ASC";

                DataTable dtProvince = code.DatabaseQuerySafe(conn, provinceQuery, null);
                DataTable dtDistrict = code.DatabaseQuerySafe(conn, districtQuery, districtParams.Count > 0 ? districtParams : null);
                DataTable dtSubDistrict = code.DatabaseQuerySafe(conn, subDistrictQuery, subDistrictParams.Count > 0 ? subDistrictParams : null);

                if (dtProvince.Rows.Count > 0 || dtDistrict.Rows.Count > 0 || dtSubDistrict.Rows.Count > 0)
                {
                    LoadAddressDropdowns(dtProvince, dtDistrict, dtSubDistrict);
                }
            }
            catch { }
        }

        protected void DropDownList1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(DropDownList1.SelectedValue == "1")
            {
                CheckBox1.Checked = true;
                CheckBox1.Enabled = false;
            }
            else
            {
                CheckBox1.Enabled = true;
                CheckBox1.Checked = false;
            }
        }

        protected void TextBox3_TextChanged(object sender, EventArgs e)
        {
            if (TextBox3.Text.Length >= 9 && TextBox6.Text.Length == 0)
            {
                // SECURE: Call fillDataByPhone instead of passing raw SQL
                fillDataByPhone(TextBox3.Text);
            }
        }

        /// <summary>
        /// SECURE: Fill customer data by mobile phone using parameterized query
        /// </summary>
        public void fillDataByPhone(string mobilePhone)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@MobilePhone", mobilePhone ?? "" }
            };

            DataTable dtCustomer = code.DatabaseQuerySafe(conn,
                "SELECT * FROM Customer WHERE MobilePhone = @MobilePhone",
                parameters);

            if (dtCustomer.Rows.Count > 0)
            {
                fillDataFromCustomerTable(dtCustomer);
            }
        }

        /// <summary>
        /// DEPRECATED: Use fillDataByPhone instead. Kept for backward compatibility.
        /// </summary>
        public void fillData(string cmd)
        {
            // WARNING: This method accepts raw SQL strings and is vulnerable to SQL Injection
            // Callers should migrate to fillDataByPhone() or similar parameterized methods
            DataTable dtCustomer = code.DatabaseQuery(conn, cmd);
            if (dtCustomer.Rows.Count > 0)
            {
                fillDataFromCustomerTable(dtCustomer);
            }
        }

        /// <summary>
        /// SECURE: Populate form fields from customer DataTable
        /// </summary>
        private void fillDataFromCustomerTable(DataTable dtCustomer)
        {
            if (dtCustomer.Rows.Count > 0)
            {
                // ✅ Fill basic textboxes (always works, even when Panel1 is hidden)
                TextBox4.Text = dtCustomer.Rows[0]["FullName"].ToString();
                TextBox6.Text = dtCustomer.Rows[0]["IDNumber"].ToString();
                TextBox7.Text = dtCustomer.Rows[0]["Address"].ToString();
                TextBox8.Text = dtCustomer.Rows[0]["Address1"].ToString();
                TextBox10.Text = dtCustomer.Rows[0]["Email"].ToString();

                // 🔍 Get Address_ID from Customer and query Address table
                string addressId = dtCustomer.Rows[0]["Address_ID"]?.ToString()?.Trim() ?? "0";
                string postalCode = "";
                string province = "";
                string district = "";
                string subDistrict = "";

                if (addressId != "0" && !string.IsNullOrEmpty(addressId))
                {
                    try
                    {
                        // SECURE: Use parameterized query for Address lookup
                        if (int.TryParse(addressId, out int addrId))
                        {
                            var addrParams = new Dictionary<string, object>
                            {
                                { "@AddressID", addrId }
                            };

                            DataTable dtAddress = code.DatabaseQuerySafe(conn,
                                "SELECT PostalCode, Province, District, SubDistrict FROM [Address] WHERE ID = @AddressID",
                                addrParams);

                            if (dtAddress.Rows.Count > 0)
                            {
                                postalCode = dtAddress.Rows[0]["PostalCode"]?.ToString()?.Trim() ?? "";
                                province = dtAddress.Rows[0]["Province"]?.ToString()?.Trim() ?? "";
                                district = dtAddress.Rows[0]["District"]?.ToString()?.Trim() ?? "";
                                subDistrict = dtAddress.Rows[0]["SubDistrict"]?.ToString()?.Trim() ?? "";
                            }
                        }
                    }
                    catch { }
                }

                TextBox9.Text = postalCode;

                // 📝 Store customer data with address info in Session for dropdown binding
                // Create a merged DataTable with both Customer and Address data
                DataTable dtMerged = dtCustomer.Copy();

                // Add address columns if they don't exist
                if (!dtMerged.Columns.Contains("PostalCode_Actual"))
                    dtMerged.Columns.Add("PostalCode_Actual", typeof(string));
                if (!dtMerged.Columns.Contains("Province_Actual"))
                    dtMerged.Columns.Add("Province_Actual", typeof(string));
                if (!dtMerged.Columns.Contains("District_Actual"))
                    dtMerged.Columns.Add("District_Actual", typeof(string));
                if (!dtMerged.Columns.Contains("SubDistrict_Actual"))
                    dtMerged.Columns.Add("SubDistrict_Actual", typeof(string));

                dtMerged.Rows[0]["PostalCode_Actual"] = postalCode;
                dtMerged.Rows[0]["Province_Actual"] = province;
                dtMerged.Rows[0]["District_Actual"] = district;
                dtMerged.Rows[0]["SubDistrict_Actual"] = subDistrict;

                Session["PendingCustomerData"] = dtMerged;

                // 🐛 Debug logging (commented out - working correctly)
                // string debugMsg = $"fillData called\\nAddress_ID: {addressId}\\nPostalCode: {postalCode}\\nProvince: {province}\\nDistrict: {district}\\nSubDistrict: {subDistrict}\\nCustomer_Type_ID: {dtCustomer.Rows[0]["Customer_Type_ID"]?.ToString() ?? "NULL"}";
                // ClientScript.RegisterStartupScript(this.GetType(), "fillData_debug", $"alert('{debugMsg}');", true);
            }
        }

        /// <summary>
        /// Apply customer data to dropdowns (Customer Type and Address)
        /// This must be called when Panel1 is visible for ViewState to work properly
        /// </summary>
        private void ApplyCustomerData(DataTable dtCustomer)
        {
            if (dtCustomer == null || dtCustomer.Rows.Count == 0)
                return;

            try
            {
                // 🐛 Debug logging
                string debugMsg = "ApplyCustomerData called\\n";

                // 🔧 Force SqlDataSource2 to bind before setting value
                // SqlDataSource binds after Page_Load, so we need to force it
                if (DropDownList2.Items.Count <= 1) // Only default item exists
                {
                    debugMsg += "Forcing SqlDataSource2 to DataBind()\\n";
                    DropDownList2.DataBind();
                    debugMsg += $"After DataBind: DropDownList2 Items Count: {DropDownList2.Items.Count}\\n";
                }

                // ✅ Customer Type dropdown
                try
                {
                    string customerTypeId = dtCustomer.Rows[0]["Customer_Type_ID"].ToString();
                    debugMsg += $"Customer Type ID: {customerTypeId}\\n";
                    debugMsg += $"DropDownList2 Items Count: {DropDownList2.Items.Count}\\n";

                    DropDownList2.ClearSelection();
                    var customerTypeItem = DropDownList2.Items.FindByValue(customerTypeId);
                    if (customerTypeItem != null)
                    {
                        customerTypeItem.Selected = true;
                        DropDownList2.SelectedIndex = DropDownList2.Items.IndexOf(customerTypeItem);
                        debugMsg += $"Customer Type selected: {customerTypeItem.Text}\\n";
                    }
                    else
                    {
                        debugMsg += "Customer Type item not found\\n";
                    }
                }
                catch (Exception ex)
                {
                    debugMsg += $"Customer Type error: {ex.Message}\\n";
                }

                // ✅ Address dropdowns
                try
                {
                    // Get address data from merged table (Address_ID query result)
                    string postalCode = dtCustomer.Rows[0]["PostalCode_Actual"]?.ToString()?.Trim() ?? "";
                    string province = dtCustomer.Rows[0]["Province_Actual"]?.ToString()?.Trim() ?? "";
                    string district = dtCustomer.Rows[0]["District_Actual"]?.ToString()?.Trim() ?? "";
                    string subDistrict = dtCustomer.Rows[0]["SubDistrict_Actual"]?.ToString()?.Trim() ?? "";

                    debugMsg += $"PostalCode: {postalCode}, Province: {province}, District: {district}, SubDistrict: {subDistrict}\\n";

                    // Step 1: Load dropdown items (prefer postal code, fallback to address values)
                    bool dropdownsPopulated = false;

                    // Try loading by postal code first (most accurate)
                    if (!string.IsNullOrEmpty(postalCode) && postalCode.Length == 5)
                    {
                        LoadAddressDropdownsByPostalCode(postalCode);
                        debugMsg += $"After LoadAddressDropdownsByPostalCode: DDL3={DropDownList3.Items.Count}, DDL4={DropDownList4.Items.Count}, DDL5={DropDownList5.Items.Count}\\n";

                        // Check if any items were added
                        if (DropDownList3.Items.Count > 0)
                        {
                            dropdownsPopulated = true;
                        }
                    }

                    // If postal code didn't work, try loading by province/district/subdistrict
                    if (!dropdownsPopulated && !string.IsNullOrEmpty(province))
                    {
                        LoadAddressDropdownsByLocation(province, district, subDistrict);
                        debugMsg += $"After LoadAddressDropdownsByLocation: DDL3={DropDownList3.Items.Count}, DDL4={DropDownList4.Items.Count}, DDL5={DropDownList5.Items.Count}\\n";
                    }

                    // Step 2: Select the correct values from the populated dropdowns
                    if (!string.IsNullOrEmpty(province))
                    {
                        try
                        {
                            DropDownList3.ClearSelection();
                            var provinceItem = DropDownList3.Items.FindByText(province);
                            if (provinceItem != null)
                            {
                                provinceItem.Selected = true;
                                DropDownList3.SelectedIndex = DropDownList3.Items.IndexOf(provinceItem);
                                debugMsg += $"Province selected: {provinceItem.Text}\\n";
                            }
                            else
                            {
                                debugMsg += $"Province '{province}' not found in dropdown\\n";
                            }
                        }
                        catch (Exception ex)
                        {
                            debugMsg += $"Province selection error: {ex.Message}\\n";
                        }
                    }

                    if (!string.IsNullOrEmpty(district))
                    {
                        try
                        {
                            DropDownList4.ClearSelection();
                            var districtItem = DropDownList4.Items.FindByText(district);
                            if (districtItem != null)
                            {
                                districtItem.Selected = true;
                                DropDownList4.SelectedIndex = DropDownList4.Items.IndexOf(districtItem);
                                debugMsg += $"District selected: {districtItem.Text}\\n";
                            }
                            else
                            {
                                debugMsg += $"District '{district}' not found in dropdown\\n";
                            }
                        }
                        catch (Exception ex)
                        {
                            debugMsg += $"District selection error: {ex.Message}\\n";
                        }
                    }

                    if (!string.IsNullOrEmpty(subDistrict))
                    {
                        try
                        {
                            DropDownList5.ClearSelection();
                            var subdistrictItem = DropDownList5.Items.FindByText(subDistrict);
                            if (subdistrictItem != null)
                            {
                                subdistrictItem.Selected = true;
                                DropDownList5.SelectedIndex = DropDownList5.Items.IndexOf(subdistrictItem);
                                debugMsg += $"SubDistrict selected: {subdistrictItem.Text}\\n";
                            }
                            else
                            {
                                debugMsg += $"SubDistrict '{subDistrict}' not found in dropdown\\n";
                            }
                        }
                        catch (Exception ex)
                        {
                            debugMsg += $"SubDistrict selection error: {ex.Message}\\n";
                        }
                    }

                    // Branch number visibility
                    if (dtCustomer.Rows[0]["Customer_Type_ID"].ToString() == "1")
                    {
                        TextBox5.Visible = true;
                        TextBox5.Text = dtCustomer.Rows[0]["Branch_Number"].ToString();
                    }
                }
                catch (Exception ex)
                {
                    debugMsg += $"Address error: {ex.Message}\\n";
                }

                // 🐛 Debug message (commented out - working correctly)
                // ClientScript.RegisterStartupScript(this.GetType(), "debug", $"alert('{debugMsg}');", true);
            }
            catch (Exception ex)
            {
                // Log errors silently instead of showing alert to users
                code.Logs(conn, "Product.ApplyCustomerData Error", ex.Message, Session["User"]?.ToString());
            }
        }
        protected void DropDownList2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(DropDownList2.SelectedValue == "1")
            {
                TextBox5.Visible = true;
            }
            else
            {
                TextBox5.Visible = false;
            }
        }

        protected void TextBox6_TextChanged(object sender, EventArgs e)
        {
            if (TextBox3.Text.Length == 0 && TextBox6.Text.Length == 13)
            {
                // SECURE: Use parameterized query to prevent SQL Injection
                var parameters = new Dictionary<string, object>
                {
                    { "@IDNumber", TextBox6.Text ?? "" }
                };
                DataTable dtCustomer = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Customer] WHERE IDNumber = @IDNumber",
                    parameters);
                if (dtCustomer.Rows.Count > 0)
                {
                    fillDataFromCustomerTable(dtCustomer);
                }
            }
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            int AccRecID = 0;
            string docNum = "0";
            double total = 0;
            DataTable dtOrder = (DataTable)Session["dtOrder"];

            // ✅ Validation: Check if cart is empty
            if (dtOrder == null || dtOrder.Rows.Count == 0)
            {
                ClientScript.RegisterStartupScript(this.GetType(), "error",
                    "alert('⚠️ กรุณาเพิ่มสินค้าลงในตะกร้าก่อนบันทึก');", true);
                return;
            }

            // ✅ Validation: Check payment method for non-room charge
            if ((ddlGuestReservation.SelectedValue == "0" || rblChargeMode.SelectedValue != "ROOM_CHARGE")
                && string.IsNullOrEmpty(DropDownList1.SelectedValue))
            {
                ClientScript.RegisterStartupScript(this.GetType(), "error",
                    "alert('⚠️ กรุณาเลือกวิธีการชำระเงิน');", true);
                return;
            }

            // 🏨 Check if Room Charge mode
            if (ddlGuestReservation.SelectedValue != "0" && rblChargeMode.SelectedValue == "ROOM_CHARGE")
            {
                // ROOM CHARGE MODE - Charge to guest room without immediate payment
                ProcessRoomCharge(dtOrder);
                return; // Exit method after processing room charge
            }

            // 🏨 Store guest info for PAY_NOW mode (will be used after receipt creation)
            bool isPayNowMode = (ddlGuestReservation.SelectedValue != "0" && rblChargeMode.SelectedValue == "PAY_NOW");
            int guestReservationId = 0;
            if (isPayNowMode)
            {
                guestReservationId = Convert.ToInt32(ddlGuestReservation.SelectedValue);
            }

            if (CheckBox1.Checked == true)
            {

                DateTime receiptDate = Convert.ToDateTime(TextBox12.Text);
                DataTable dtDetail = (DataTable)Session["dtDetail"];


                docNum = _documentHelper.CreateDocumentNumber("Account_Receipt", "REC", receiptDate);

                // Extract Year/Month for directory structure
                string Year = receiptDate.Year.ToString();
                string Month = receiptDate.Month.ToString();

                // 🎁 Apply product category discounts for loyalty members
                double originalTotal = Convert.ToDouble(TextBox2.Text);
                double totalDiscount = 0;
                string customerPhone = TextBox3.Text;

                try
                {
                    if (!string.IsNullOrEmpty(customerPhone) && CheckBox2.Checked)
                    {
                        var tierBenefitsService = new TierBenefitsService(conn);

                        // Calculate discounts for each product based on its category
                        foreach (DataRow orderRow in dtOrder.Rows)
                        {
                            long categoryId = Convert.ToInt64(orderRow["Category_ID"]);
                            decimal itemTotal = Convert.ToDecimal(orderRow["Price_Total"]);

                            var discountResult = tierBenefitsService.CalculateProductDiscount(
                                customerPhone,
                                categoryId,
                                itemTotal);

                            if (discountResult.Success && discountResult.DiscountAmount > 0)
                            {
                                totalDiscount += (double)discountResult.DiscountAmount;
                                code.Logs(conn, "Product - Category Discount Applied",
                                    $"Customer: {customerPhone}, Category: {categoryId}, " +
                                    $"Original: {itemTotal:N2}, Discount: {discountResult.DiscountAmount:N2}",
                                    Session["User"]?.ToString());
                            }
                        }

                        // Apply total discount
                        if (totalDiscount > 0)
                        {
                            originalTotal = Convert.ToDouble(TextBox2.Text);
                            total = originalTotal - totalDiscount;

                            // Show discount info to user
                            code.Logs(conn, "Product - Total Loyalty Discount",
                                $"Customer: {customerPhone}, Original: {originalTotal:N2}, " +
                                $"Discount: {totalDiscount:N2}, Final: {total:N2}",
                                Session["User"]?.ToString());
                        }
                        else
                        {
                            total = originalTotal;
                        }
                    }
                    else
                    {
                        total = originalTotal;
                    }
                }
                catch (Exception discountEx)
                {
                    // Log error but don't fail the receipt creation
                    code.Logs(conn, "Product - Category Discount Error",
                        $"Customer: {customerPhone}, Error: {discountEx.Message}",
                        Session["User"]?.ToString());
                    total = originalTotal;
                }

                // ✅ Use discounted total (already calculated above)
                int vatpercent = Convert.ToInt32(code.DatabaseQuery(conn, "SELECT [Vat_Percent] FROM [Taketime].[dbo].[Account_Vat_Type] Where ID = 1").Rows[0][0].ToString());
                double vat = Math.Round(((total * 100) / (100+vatpercent)), 2);
                double Total_Amount_Exclude_Vat = total - vat;
                string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
                try
                {
                    System.IO.Directory.CreateDirectory(path + "\\" + Year);
                    System.IO.Directory.CreateDirectory(path + "\\" + Year + "\\" + Month);
                }
                catch (Exception ex)
                {

                }
                DataTable dtbusinessinfo = code.DatabaseQuery(conn, "Select * from Business_Info left join Customer_Type on Business_Type_ID = Customer_Type.ID left join Address on Address.ID = Address_ID");


                
                DataTable dtcustomer = new DataTable();
                if (CheckBox2.Checked == true)
                {
                    // SECURE: Use parameterized queries to prevent SQL Injection
                    var custQueryParams = new Dictionary<string, object>();
                    string customerQueryInitial;

                    if (DropDownList2.SelectedValue == "1")
                    {
                        customerQueryInitial = "SELECT * FROM Customer " +
                                              "LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID " +
                                              "LEFT JOIN Address ON Address.ID = Address_ID " +
                                              "WHERE MobilePhone = @SearchValue";
                        custQueryParams["@SearchValue"] = TextBox3.Text;
                        dtcustomer = code.DatabaseQuerySafe(conn, customerQueryInitial, custQueryParams);
                    }
                    else
                    {
                        customerQueryInitial = "SELECT * FROM Customer " +
                                              "LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID " +
                                              "LEFT JOIN Address ON Address.ID = Address_ID " +
                                              "WHERE IDNumber = @SearchValue";
                        custQueryParams["@SearchValue"] = TextBox6.Text;
                        dtcustomer = code.DatabaseQuerySafe(conn, customerQueryInitial, custQueryParams);
                    }

                    // SECURE: Use UpsertCustomer to handle INSERT/UPDATE safely
                    try
                    {
                        // ✨ Use AddressHelper to get Address ID
                        int addressId = 0;
                        string addressIdStr = _addressHelper.GetAddressIdString(
                            TextBox9.Text,
                            DropDownList3.SelectedItem.Text,
                            DropDownList4.SelectedItem.Text,
                            DropDownList5.SelectedItem.Text);
                        int.TryParse(addressIdStr, out addressId);

                        int customerTypeId = 0;
                        int.TryParse(DropDownList2.SelectedValue, out customerTypeId);

                        long customerId = code.UpsertCustomer(
                            conn,
                            TextBox3.Text,                          // mobilePhone
                            "",                                     // name
                            "",                                     // nickName
                            "",                                     // comeFrom
                            "",                                     // remark
                            TextBox4.Text,                          // fullName
                            ValidationHelper.CleanText(TextBox7.Text), // address (✨ using ValidationHelper)
                            TextBox6.Text,                          // idNumber
                            TextBox10.Text,                         // email
                            customerTypeId,                         // customerTypeID
                            addressId,                              // addressID
                            TextBox8.Text,                          // address1
                            TextBox5.Text                           // branchNumber
                        );

                        // SECURE: Re-query customer data with parameterized query
                        var custParams = new Dictionary<string, object>();
                        string customerQuery = "SELECT * FROM Customer " +
                                             "LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID " +
                                             "LEFT JOIN Address ON Address.ID = Address_ID ";

                        if (DropDownList2.SelectedValue == "2")
                        {
                            customerQuery += "WHERE MobilePhone = @SearchValue";
                            custParams["@SearchValue"] = TextBox3.Text;
                        }
                        else
                        {
                            customerQuery += "WHERE IDNumber = @SearchValue";
                            custParams["@SearchValue"] = TextBox6.Text;
                        }

                        dtcustomer = code.DatabaseQuerySafe(conn, customerQuery, custParams);
                    }
                    catch { }
                }
                // SECURE: INSERT Receipt with parameterized query
                var receiptParams = new Dictionary<string, object>
                {
                    { "@ID", docNum },
                    { "@CreatedDate", Convert.ToDateTime(TextBox12.Text) },
                    { "@TotalAmount", total },
                    { "@Vat", vat },
                    { "@TotalAmountExcludeVat", Total_Amount_Exclude_Vat },
                    { "@PaidType", DropDownList1.SelectedItem.Text },
                    { "@CreatedByID", Session["UserID"].ToString() },
                    { "@Etax", CheckBox3.Checked },
                    { "@CustomerID", dtcustomer.Rows[0]["ID"].ToString() }
                };
                code.DatabaseInsertSafe(conn,
                    "INSERT INTO [dbo].[Account_Receipt] " +
                    "([ID],[Reservation_ID],[Created_Date],[Total_Amount],[Vat],[Total_Amount_Exclude_Vat],[IsDeposit],[UseDeposit],[Paid_Type],[Status],[Created_By_ID],[Etax],[Customer_ID]) " +
                    "VALUES (@ID,'0',@CreatedDate,@TotalAmount,@Vat,@TotalAmountExcludeVat,0,0,@PaidType,'Normal',@CreatedByID,@Etax,@CustomerID)",
                    receiptParams);

                // SECURE: INSERT Receipt Detail with parameterized query
                var detailParams = new Dictionary<string, object>
                {
                    { "@ReceiptID", docNum },
                    { "@ProductData", TextBox11.Text },
                    { "@Total", total }
                };
                code.DatabaseInsertSafe(conn,
                    "INSERT INTO [dbo].[Account_Receipt_Detail] " +
                    "([Number],[Receipt_ID],[ProductType_ID],[Product_ID],[Product_Data],[Product_Amount],[Product_Unit],[Price_PerPeice],[Price_Amount]) " +
                    "VALUES (1,@ReceiptID,'3','0',@ProductData,1,N'ครั้ง',@Total,@Total)",
                    detailParams);

                // 🎁 Log product category discount usage to Loyalty_Benefit_Usage
                if (totalDiscount > 0 && !string.IsNullOrEmpty(customerPhone))
                {
                    try
                    {
                        // Get receipt UID to use as reference
                        var uidParams = new Dictionary<string, object> { { "@ReceiptID", docNum } };
                        DataTable dtUid = code.DatabaseQuerySafe(conn,
                            "SELECT UID FROM [Account_Receipt] WHERE ID = @ReceiptID",
                            uidParams);

                        if (dtUid.Rows.Count > 0)
                        {
                            long receiptUid = Convert.ToInt64(dtUid.Rows[0]["UID"]);
                            short? adminId = Session["UserID"] != null ? (short?)Convert.ToInt16(Session["UserID"]) : null;

                            var tierBenefitsService = new TierBenefitsService(conn);

                            // Log discount for each product category
                            foreach (DataRow orderRow in dtOrder.Rows)
                            {
                                long categoryId = Convert.ToInt64(orderRow["Category_ID"]);
                                decimal itemTotal = Convert.ToDecimal(orderRow["Price_Total"]);

                                var applyResult = tierBenefitsService.ApplyProductDiscount(
                                    customerPhone,
                                    categoryId,
                                    itemTotal,
                                    null,  // No reservation ID for POS
                                    receiptUid,
                                    adminId);

                                if (!applyResult.Success)
                                {
                                    code.Logs(conn, "Product - Apply Category Discount Failed",
                                        $"Receipt: {docNum}, Category: {categoryId}, Message: {applyResult.Message}",
                                        Session["User"]?.ToString());
                                }
                            }
                        }
                    }
                    catch (Exception logEx)
                    {
                        // Log error but don't fail the receipt creation
                        code.Logs(conn, "Product - Log Category Discount Error",
                            $"Receipt: {docNum}, Error: {logEx.Message}",
                            Session["User"]?.ToString());
                    }
                }

                // SECURE: SELECT Receipt and Detail with parameterized query
                var selectParams = new Dictionary<string, object> { { "@DocNum", docNum } };
                DataTable dtReceipt = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Account_Receipt] " +
                    "LEFT JOIN Reservation ON Reservation.ID = Reservation_ID " +
                    "WHERE Account_Receipt.ID = @DocNum",
                    selectParams);

                DataTable dtReceiptDetail = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Account_Receipt_Detail] " +
                    "INNER JOIN Account_ProductType ON Account_ProductType.ID = ProductType_ID " +
                    "WHERE Receipt_ID = @DocNum ORDER BY Number ASC",
                    selectParams);
                string uid = dtReceipt.Rows[0]["UID"].ToString();


                //GridView1.DataSource = dt;
                //GridView1.DataBind();

                DataTable dtSignature = new DataTable();
                try
                {
                    dtSignature.Columns.Add("AuthorizeName");
                    dtSignature.Columns.Add("AuthorizeSignaturePath");
                    dtSignature.Columns.Add("CreatedName");
                    dtSignature.Columns.Add("CreatedSignaturePath");
                }
                catch { }

                string Signaturepath = System.Configuration.ConfigurationSettings.AppSettings["StaffSignatureFolderPath"].ToString();

                // SECURE: SELECT Admin with parameterized query
                DataTable dtApprover = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Admin WHERE IsCEO = 'True'",
                    null);
                string ApproverFullName = dtApprover.Rows[0]["FirstName"].ToString() + " " + dtApprover.Rows[0]["LastName"].ToString();

                var creatorParams = new Dictionary<string, object> { { "@UserID", Session["UserID"].ToString() } };
                DataTable dtCreator = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Admin WHERE ID = @UserID",
                    creatorParams);
                string CreatorFullName = dtCreator.Rows[0]["FirstName"].ToString() + " " + dtCreator.Rows[0]["LastName"].ToString();

                dtSignature.Rows.Add(ApproverFullName, "File:\\" + Signaturepath + "\\" + ApproverFullName.ToLower() + ".png", CreatorFullName, "File:\\" + Signaturepath + "\\" + CreatorFullName.ToLower() + ".png");

                DataTable dtCustomerReport = new DataTable();
                dtCustomerReport = dtcustomer.Copy();
                DataTable dtBusinessinfoReport = new DataTable();
                dtBusinessinfoReport = dtbusinessinfo.Copy();

                try
                {


                    if (CheckBox2.Checked == false)
                    {
                        dtCustomerReport.Rows[0]["FullName"] = "ประสงค์ไม่รับใบกำกับภาษี";
                        dtCustomerReport.Rows[0]["Address"] = "";
                        dtCustomerReport.Rows[0]["IDNumber"] = "";
                        dtCustomerReport.Rows[0]["MobilePhone"] = "";
                        dtCustomerReport.Rows[0]["Email"] = "";
                    }
                    else
                    {

                        
                        if (DropDownList2.SelectedValue == "1" && TextBox5.Text == "00000")
                        {
                            dtCustomerReport.Rows[0]["FullName"] = TextBox4.Text;
                        }
                        else if (DropDownList2.SelectedValue == "1" && Convert.ToInt32(TextBox5.Text) > 0)
                        {
                            dtCustomerReport.Rows[0]["FullName"] = TextBox4.Text + " สาขาที่ " + TextBox5.Text;
                        }
                        else
                        {
                            dtCustomerReport.Rows[0]["FullName"] = TextBox4.Text;
                        }
                        try
                        {

                            if (DropDownList3.SelectedValue.Contains("กรุงเทพ"))
                            {
                                dtCustomerReport.Rows[0]["Address"] = TextBox7.Text + " " + TextBox8.Text + " แขวง " + DropDownList5.SelectedValue + " เขต " + DropDownList4.SelectedValue + " " + DropDownList3.SelectedValue + " " + dtcustomer.Rows[0]["PostalCode"].ToString();
                            }
                            else
                            {
                                dtCustomerReport.Rows[0]["Address"] = TextBox7.Text + " " + TextBox8.Text + " ต." + DropDownList5.SelectedValue + " อ." + DropDownList4.SelectedValue + " จ." + DropDownList3.SelectedValue + " " + dtcustomer.Rows[0]["PostalCode"].ToString();
                            }
                        }
                        catch
                        {
                            dtCustomerReport.Rows[0]["Address"] = dtcustomer.Rows[0]["Address"].ToString();
                        }

                        dtCustomerReport.Rows[0]["IDNumber"] = TextBox6.Text;
                        dtCustomerReport.Rows[0]["MobilePhone"] = TextBox3.Text;
                        dtCustomerReport.Rows[0]["Email"] = TextBox10.Text;
                    }


                    try
                    {
                        if (dtbusinessinfo.Rows[0]["Province"].ToString().Contains("กรุงเทพ"))
                        {
                            dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString() + " " + dtbusinessinfo.Rows[0]["Address1"].ToString() + " แขวง " + dtbusinessinfo.Rows[0]["SubDistrict"].ToString() + " เขต " + dtbusinessinfo.Rows[0]["District"].ToString() + " " + dtbusinessinfo.Rows[0]["Province"].ToString() + " " + dtbusinessinfo.Rows[0]["PostalCode"].ToString();
                        }
                        else
                        {
                            dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString() + " " + dtbusinessinfo.Rows[0]["Address1"].ToString() + " ต." + dtbusinessinfo.Rows[0]["SubDistrict"].ToString() + " อ." + dtbusinessinfo.Rows[0]["District"].ToString() + " จ." + dtbusinessinfo.Rows[0]["Province"].ToString() + " " + dtbusinessinfo.Rows[0]["PostalCode"].ToString();
                        }
                    }
                    catch
                    {
                        dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString();
                    }
                }
                catch { }

                try
                {
                    Account.Report.DataSet1 dataSet1 = new Account.Report.DataSet1();
                    dataSet1.Tables.Add(dtBusinessinfoReport);
                    ReportViewer2.LocalReport.DisplayName = "Receipt";
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet1", dtBusinessinfoReport));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet2", dtCustomerReport));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet3", dtReceiptDetail));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet4", dtReceipt));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet5", dtSignature));
                    try
                    {

                        //     var deviceInfo = @"<DeviceInfo>
                        // <EmbedFonts>None</EmbedFonts>
                        //</DeviceInfo>";


                        Warning[] warnings;
                        string[] streamids;
                        string mimeType;
                        string encoding;
                        string filenameExtension;

                        byte[] bytes = ReportViewer2.LocalReport.Render(
                            "PDF", null, out mimeType, out encoding, out filenameExtension,
                            out streamids, out warnings);
                        if (File.Exists(path + "\\" + Year + "\\" + Month + "\\" + docNum +"_"+uid+".pdf"))
                        {
                            File.Delete(path + "\\" + Year + "\\" + Month + "\\" + docNum +"_"+uid+ ".pdf");
                        }
                        using (FileStream fs = new FileStream(path + "\\" + Year + "\\" + Month + "\\" + docNum +"_"+uid+ ".pdf", FileMode.Append))
                        {

                            fs.Write(bytes, 0, bytes.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                    }

                    if (CheckBox3.Checked == true)
                    {
                        try
                        {
                            string xmlFilePath = path + "\\" + Year + "\\" + Month + "\\" + docNum +"_"+uid+ ".xml";
                            string xmlString = System.IO.File.ReadAllText(ConfigurationSettings.AppSettings["BaseFolderPath"].ToString() + "\\Resources\\template.xml");
                            xmlString = xmlString.Replace("*invoice_id", docNum);
                            xmlString = xmlString.Replace("*invoice_name", "ใบเสร็จรับเงิน/ใบกำกับภาษี");
                            xmlString = xmlString.Replace("*invoice_typecode", "T03");
                            xmlString = xmlString.Replace("*invoice_issue_date", Convert.ToDateTime(dtReceipt.Rows[0]["Created_Date"].ToString()).ToString("yyyy-MM-dd") + "T00:00:00.000");
                            xmlString = xmlString.Replace("*invoice_purpose", "");
                            xmlString = xmlString.Replace("*invoice_Purpose_code", "");
                            xmlString = xmlString.Replace("*invoice_create_date", Convert.ToDateTime(dtReceipt.Rows[0]["Created_Date"].ToString()).ToString("yyyy-MM-dd") + "T00:00:00.000");
                            xmlString = xmlString.Replace("*invoice_remark", "");

                            try
                            {
                                xmlString = xmlString.Replace("*seller_type", dtbusinessinfo.Rows[0]["Customer_Code"].ToString());
                                if (dtbusinessinfo.Rows[0]["Customer_Code"].ToString() == "TXID")
                                {
                                    xmlString = xmlString.Replace("*seller_taxid", dtbusinessinfo.Rows[0]["LegalEntity_Number"].ToString() + dtbusinessinfo.Rows[0]["Branch_Number"].ToString());
                                }
                                else
                                {
                                    xmlString = xmlString.Replace("*seller_taxid", dtbusinessinfo.Rows[0]["LegalEntity_Number"].ToString());
                                }
                            }
                            catch
                            {
                                xmlString = xmlString.Replace("*seller_type", "TXID");
                                xmlString = xmlString.Replace("*seller_taxid", dtbusinessinfo.Rows[0]["LegalEntity_Number"].ToString());
                            }

                            xmlString = xmlString.Replace("*seller_name", dtbusinessinfo.Rows[0]["Company_Name"].ToString());

                            xmlString = xmlString.Replace("*seller_DefinedCITradeContact", dtbusinessinfo.Rows[0]["Email"].ToString());
                            xmlString = xmlString.Replace("*seller_PhoneNumber", dtbusinessinfo.Rows[0]["Phone_Number"].ToString());
                            xmlString = xmlString.Replace("*seller_zipcode", dtbusinessinfo.Rows[0]["PostalCode"].ToString());
                            xmlString = xmlString.Replace("*seller_address1", dtbusinessinfo.Rows[0]["Address"].ToString() + " " + dtbusinessinfo.Rows[0]["Address1"].ToString() + " " + dtbusinessinfo.Rows[0]["SubDistrict"].ToString() + " " + dtbusinessinfo.Rows[0]["District"].ToString() + " " + dtbusinessinfo.Rows[0]["Province"].ToString() + " " + dtbusinessinfo.Rows[0]["PostalCode"].ToString());
                            xmlString = xmlString.Replace("*seller_address2", "");
                            xmlString = xmlString.Replace("*seller_cityname", dtbusinessinfo.Rows[0]["Address_Code"].ToString().Substring(0, 4));
                            xmlString = xmlString.Replace("*seller_city_subdivision_name", dtbusinessinfo.Rows[0]["Address_Code"].ToString().Substring(0, 6));
                            xmlString = xmlString.Replace("*seller_country", "TH");
                            xmlString = xmlString.Replace("*sellercountry_subdivision_id", dtbusinessinfo.Rows[0]["Address_Code"].ToString().Substring(0, 2));
                            xmlString = xmlString.Replace("*seller_building_name", dtbusinessinfo.Rows[0]["Address"].ToString());
                            xmlString = xmlString.Replace("*buyer_name", dtcustomer.Rows[0]["FullName"].ToString());

                            try
                            {


                                xmlString = xmlString.Replace("*buyer_taxtype", dtcustomer.Rows[0]["Customer_Code"].ToString());


                                if (dtcustomer.Rows[0]["Customer_Code"].ToString() == "TXID")
                                {
                                    string bnumber = "00000";
                                    if (TextBox7.Text.Length == 5)
                                    {
                                        bnumber = dtcustomer.Rows[0]["Branch_Number"].ToString();
                                    }
                                    xmlString = xmlString.Replace("*buyer_taxid", dtcustomer.Rows[0]["IDNumber"].ToString() + bnumber);
                                }
                                else
                                {
                                    xmlString = xmlString.Replace("*buyer_taxtype", "NIDN");
                                    xmlString = xmlString.Replace("*buyer_taxid", dtcustomer.Rows[0]["IDNumber"].ToString());
                                }
                            }
                            catch
                            {
                                xmlString = xmlString.Replace("*buyer_taxtype", "NIDN");
                                xmlString = xmlString.Replace("*buyer_taxid", dtcustomer.Rows[0]["IDNumber"].ToString());
                            }


                            xmlString = xmlString.Replace("*buyer_DefinedCITradeContact", TextBox10.Text);
                            xmlString = xmlString.Replace("*buyer_zipcode", dtcustomer.Rows[0]["PostalCode"].ToString());
                            xmlString = xmlString.Replace("*buyer_address", dtcustomer.Rows[0]["Address"].ToString() + " " + dtcustomer.Rows[0]["Address1"].ToString() + " " + dtcustomer.Rows[0]["SubDistrict"].ToString() + " " + dtcustomer.Rows[0]["District"].ToString() + " " + dtcustomer.Rows[0]["Province"].ToString() + " " + dtcustomer.Rows[0]["PostalCode"].ToString());
                            xmlString = xmlString.Replace("*buyer_address2", "");
                            xmlString = xmlString.Replace("*buyer_cityname", dtcustomer.Rows[0]["Address_Code"].ToString().Substring(0, 4));
                            xmlString = xmlString.Replace("*buyer_city_subdivision_name", dtcustomer.Rows[0]["Address_Code"].ToString().Substring(0, 6));
                            xmlString = xmlString.Replace("*buyer_country", "TH");
                            xmlString = xmlString.Replace("*buyercountry_subdivision_id", dtcustomer.Rows[0]["Address_Code"].ToString().Substring(0, 2));
                            xmlString = xmlString.Replace("*buyer_building_name", dtcustomer.Rows[0]["Address"].ToString());
                            xmlString = xmlString.Replace("*reference", "");
                            xmlString = xmlString.Replace("*buyer_contact_person", "");
                            xmlString = xmlString.Replace("*currency", "THB");
                            xmlString = xmlString.Replace("*invoice_tax_code", "VAT");
                            xmlString = xmlString.Replace("*invoice_tax_rate", vatpercent.ToString());
                            xmlString = xmlString.Replace("*invoice_basis_amount", dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"].ToString());
                            xmlString = xmlString.Replace("*calculated_amount", dtReceipt.Rows[0]["Vat"].ToString());
                            xmlString = xmlString.Replace("*invoice_discountallowance", "");
                            xmlString = xmlString.Replace("*invoice_serviceallowance", "");
                            xmlString = xmlString.Replace("*invoice_line_total", dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"].ToString());
                            xmlString = xmlString.Replace("*tax_basis_total_amount", dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"].ToString());
                            xmlString = xmlString.Replace("*invoice_tax_total", dtReceipt.Rows[0]["Vat"].ToString());
                            xmlString = xmlString.Replace("*invoice_grand_total", dtReceipt.Rows[0]["Total_Amount"].ToString());
                            xmlString = xmlString.Replace("*item", "ค่าอาหารและเครื่องดื่ม");
                            xmlString = xmlString.Replace("*invoice_billedquantity", "1");

                            System.IO.File.WriteAllText(xmlFilePath, xmlString, System.Text.Encoding.UTF8);
                        }
                        catch { }

                        try
                        {
                            {


                                PDFA3Invoice pdf = new PDFA3Invoice();
                                string pdfFilePath = path + "\\" + Year + "\\" + Month + "\\" + docNum +"_"+uid+ ".pdf";
                                string xmlFilePath = path + "\\" + Year + "\\" + Month + "\\" + docNum +"_"+uid+ ".xml";

                                string xmlFileName = "ETDA-invoice.xml";


                                string xmlVersion = "1.0";
                                string documentID = docNum;
                                string documentOID = "";

                                string outputPath = path + "\\" + Year + "\\" + Month + "\\" + docNum +"_"+uid+ "_etax.pdf";

                                pdf.CreatePDFA3Invoice(pdfFilePath, xmlFilePath, xmlFileName, xmlVersion, documentID, documentOID, outputPath, "Tax Invoice");

                                if (CheckBox3.Checked == true)
                                {

                                    DateTime docDate = DateTime.Now;

                                    try
                                    {
                                        if (Convert.ToDateTime(TextBox12.Text) < docDate)
                                        {
                                            docDate = Convert.ToDateTime(TextBox12.Text);
                                        }
                                    }
                                    catch
                                    {

                                    }
                                    //DataTable dtReceipt = code.DatabaseQuery(conn, "SELECT  [ID] FROM [Account_Receipt] Where RESERVATION_ID = '" + Reservation_ID + "'");

                                    //string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
                                    //string pdfpath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + dtReceipt.Rows[0]["ID"].ToString() + "_etax.pdf";

                                    //string pdfFilePath = pdfpath;
                                    byte[] bytes = System.IO.File.ReadAllBytes(outputPath);
                                    Attachment[] dataall = new Attachment[1];
                                    MemoryStream pdf2 = new MemoryStream(bytes);
                                    Attachment data = new Attachment(pdf2, dtReceipt.Rows[0]["ID"].ToString() +"_"+uid+ "_etax.pdf");
                                    dataall[0] = data;

                                    string docCreateThaiDate = "";

                                    if (docDate.Day.ToString().Length > 1)
                                    {
                                        docCreateThaiDate += docDate.Day.ToString();
                                    }
                                    else
                                    {
                                        docCreateThaiDate += "0" + docDate.Day.ToString();
                                    }

                                    if (docDate.Month.ToString().Length > 1)
                                    {
                                        docCreateThaiDate += docDate.Month.ToString();
                                    }
                                    else
                                    {
                                        docCreateThaiDate += "0" + docDate.Month.ToString();
                                    }


                                    if (Convert.ToInt32(docDate.Year.ToString()) > 2500)
                                    {
                                        docCreateThaiDate += docDate.Year.ToString();
                                    }
                                    else
                                    {
                                        docCreateThaiDate += (Convert.ToInt32(docDate.Year.ToString()) + 543).ToString();
                                    }



                                    string subject = "[" + docCreateThaiDate + "][INV][" + dtReceipt.Rows[0]["ID"].ToString() + "]";
                                    string body = "เรียน ลูกค้าผู้มีอุปการะคุณ <br /><br /> หจก.แอม แฮปปี้เนส (Take Time) ได้แนบใบกำกับภาษี/ใบเสร็จรับเงินมาพร้อมกับอีเมล์ฉบับนี้ ท่านสามารถเปิดดูได้โดยคลิกไฟล์แนบ (PDF File)<br />ขอแสดงความนับถือ<br /> หจก.แอม แฮปปี้เนส (Take Time) ";

                                    NumberHelper.SendEmail(ConfigurationSettings.AppSettings["SMTP"].ToString(), Convert.ToInt32(ConfigurationSettings.AppSettings["SMTP_Port"].ToString()), Convert.ToBoolean(ConfigurationSettings.AppSettings["SMTP_EnableSsl"].ToString()), Convert.ToBoolean(ConfigurationSettings.AppSettings["SMTP_UseDefaultCredentials"].ToString()), ConfigurationSettings.AppSettings["Email_From"].ToString(), ConfigurationSettings.AppSettings["Email_Password_From"].ToString(), TextBox10.Text, ConfigurationSettings.AppSettings["Email_CC"].ToString(), subject, body, dataall);


                                }
                            }
                        }
                        catch
                        {

                        }
                        //ReportViewer2.LocalReport.Refresh();

                    }
                }

                catch { }

                // 🏨 Process IMMEDIATE charge if PAY_NOW mode
                if (isPayNowMode && guestReservationId > 0)
                {
                    try
                    {
                        ProcessImmediateCharge(dtOrder, guestReservationId, docNum);
                    }
                    catch (Exception ex)
                    {
                        code.Logs(conn, "Product.ProcessImmediateCharge Error",
                            $"Receipt: {docNum}, Reservation: {guestReservationId}, Error: {ex.Message}",
                            Session["User"]?.ToString());
                        // Don't fail receipt creation, just log the error
                    }
                }
            }

            if (GridView1.Rows.Count > 0)
            {
                // SECURE: INSERT Product_Out with parameterized query
                for(int i = 0;i<dtOrder.Rows.Count;i++)
                {
                    var productOutParams = new Dictionary<string, object>
                    {
                        { "@DateTimeOut", Convert.ToDateTime(TextBox12.Text + " " + DateTime.Now.ToString("HH:mm:ss")) },
                        { "@ProductID", dtOrder.Rows[i]["ID"].ToString() },
                        { "@Amount", dtOrder.Rows[i]["Amount"].ToString() },
                        { "@PricePerUnit", dtOrder.Rows[i]["Sell_Price"].ToString() },
                        { "@ReceiptID", docNum },
                        { "@PaidHowID", DropDownList1.SelectedValue }
                    };

                    code.DatabaseInsertSafe(conn,
                        "INSERT INTO [dbo].[Product_Out] " +
                        "([DateTime_Out],[Product_ID],[Amount],[PricePerUnit],[Account_Receipt_ID],[Account_Paid_How_ID],[Remark]) " +
                        "VALUES (@DateTimeOut,@ProductID,@Amount,@PricePerUnit,@ReceiptID,@PaidHowID,N'ขาย')",
                        productOutParams);
                }
            }
            Response.Redirect("/Product");
        }

        // ✨ MIGRATED: cleantext() method has been replaced with ValidationHelper.CleanText()
        // See ValidationHelper class in Take_Time_BangPhra.Class namespace

        protected void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "Add")
            {
                DataTable dtOrder = (DataTable)Session["dtOrder"];
                int amount = Convert.ToInt32(dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Amount"].ToString());
                amount = amount + 1;
                double total = amount * Convert.ToDouble(dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Sell_Price"].ToString());
                dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Amount"] = amount;
                dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Price_Total"] = total;
                GridView1.DataSource = dtOrder;
                GridView1.DataBind();
                Session["dtOrder"] = dtOrder;
                TextBox1.Text = string.Empty;
                double pricetotal = 0;
                for (int i = 0; i < dtOrder.Rows.Count; i++)
                {
                    pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
                }
                TextBox2.Text = pricetotal.ToString();
            }
            else if (e.CommandName == "Reduce")
            {
                DataTable dtOrder = (DataTable)Session["dtOrder"];
                int amount = Convert.ToInt32(dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Amount"].ToString());
                if (amount > 1)
                {
                    amount = amount - 1;
                    double total = amount * Convert.ToDouble(dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Sell_Price"].ToString());
                    dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Amount"] = amount;
                    dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Price_Total"] = total;
                    GridView1.DataSource = dtOrder;
                    GridView1.DataBind();
                    Session["dtOrder"] = dtOrder;
                    TextBox1.Text = string.Empty;
                    double pricetotal = 0;
                    for (int i = 0; i < dtOrder.Rows.Count; i++)
                    {
                        pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
                    }
                    TextBox2.Text = pricetotal.ToString();
                }
            }
            else if (e.CommandName == "DeleteItem")
            {
                DataTable dtOrder = (DataTable)Session["dtOrder"];
                dtOrder.Rows[Convert.ToInt32(e.CommandArgument)].Delete();
                dtOrder.AcceptChanges();
                GridView1.DataSource = dtOrder;
                GridView1.DataBind();
                Session["dtOrder"] = dtOrder;
                TextBox1.Text = string.Empty;
                double pricetotal = 0;
                for (int i = 0; i < dtOrder.Rows.Count; i++)
                {
                    pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
                }
                TextBox2.Text = pricetotal.ToString();
            }
        }
        protected void GridView1_RowEditing(object sender, GridViewEditEventArgs e)
        {
            GridView1.EditIndex = e.NewEditIndex;
            DataTable dtOrder = (DataTable)Session["dtOrder"];
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
        }

        protected void GridView1_RowCancelingEdit(object sender, GridViewCancelEditEventArgs e)
        {
            GridView1.EditIndex = -1;
            DataTable dtOrder = (DataTable)Session["dtOrder"];
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
        }

        protected void GridView1_RowUpdating(object sender, GridViewUpdateEventArgs e)
        {
            DataTable dtOrder = (DataTable)Session["dtOrder"];
            TextBox txtAmount = (TextBox)GridView1.Rows[e.RowIndex].Cells[2].Controls[0];
            TextBox txtPrice = (TextBox)GridView1.Rows[e.RowIndex].Cells[3].Controls[0];
            GridView1.EditIndex = -1;
            dtOrder.Rows[Convert.ToInt32(e.RowIndex)]["Amount"] = txtAmount.Text;
            dtOrder.Rows[Convert.ToInt32(e.RowIndex)]["Sell_Price"] = txtPrice.Text;
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
            Session["dtOrder"] = dtOrder;
            TextBox1.Text = string.Empty;
            double pricetotal = 0;
            for (int i = 0; i < dtOrder.Rows.Count; i++)
            {
                pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
            }
            TextBox2.Text = pricetotal.ToString();
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            renderProduct();
            Page.SetFocus(TextBox1);
        }

        protected void TextBox1_TextChanged(object sender, EventArgs e)
        {
            renderProduct();
        }

        private void CheckData()
        {
            DataTable dt = GetYourData();

            if (dt.Rows.Count == 0)
            {
                // No data - show message and setup 5-minute refresh
                

                // Setup refresh for primary interval (5 minutes)
                SetupRefresh(PrimaryInterval);
            }
            else
            {
                // Data exists - display it and setup 1-minute check
                

                // Setup check for secondary interval (1 minute)
                SetupRefresh(SecondaryInterval);
            }
        }

        private void SetupRefresh(int intervalSeconds)
        {
            // Convert milliseconds to seconds for meta refresh
            int intervalSec = intervalSeconds / 1000;

            // Clear any existing refresh tags
            RemoveRefreshMetaTag();

            // Add new refresh tag
            HtmlMeta meta = new HtmlMeta();
            meta.HttpEquiv = "refresh";
            meta.Content = intervalSec.ToString();
            Page.Header.Controls.Add(meta);
        }

        private void RemoveRefreshMetaTag()
        {
            // Remove any existing refresh meta tags
            foreach (Control c in Page.Header.Controls)
            {
                if (c is HtmlMeta && ((HtmlMeta)c).HttpEquiv == "refresh")
                {
                    Page.Header.Controls.Remove(c);
                    break;
                }
            }
        }

        // Replace with your actual data access method
        private DataTable GetYourData()
        {
            DataTable dt = (DataTable)Session["dtOrder"];


            return dt;
        }
        protected void TextBox12_TextChanged(object sender, EventArgs e)
        {
            if(TextBox12.Text.Length > 8)
            {
                try
                {
                   TextBox12.Text = Convert.ToDateTime(TextBox12.Text).ToString("yyyy-MM-dd");
                }
                catch { }
            }

            // 🏨 Reload active guests for the new selected date
            try
            {
                LoadActiveGuests();
            }
            catch (Exception ex)
            {
                code.Logs(conn, "Product.TextBox12_TextChanged Error",
                    $"Failed to reload active guests: {ex.Message}",
                    Session["User"]?.ToString());
            }
        }

        #region 🏨 Room Charge Feature Methods

        /// <summary>
        /// Load active guest reservations into dropdown
        /// </summary>
        private void LoadActiveGuests()
        {
            try
            {
                // Clear existing items first to prevent duplicates
                ddlGuestReservation.Items.Clear();

                // Always add default "no room charge" option first
                ddlGuestReservation.Items.Add(new ListItem("--- ไม่ชาร์จเข้าห้อง (ชำระทันที) ---", "0"));

                // Get selected date from TextBox12, default to today if empty/invalid
                DateTime searchDate = DateTime.Now.Date;
                if (!string.IsNullOrEmpty(TextBox12.Text))
                {
                    DateTime.TryParse(TextBox12.Text, out searchDate);
                }

                // Get active guests for the selected date
                var guests = _roomChargeDA.GetActiveGuestReservations(searchDate);

                if (guests.Rows.Count > 0)
                {
                    // Add all active guests
                    foreach (DataRow row in guests.Rows)
                    {
                        ddlGuestReservation.Items.Add(new ListItem(
                            row["DisplayText"].ToString(),
                            row["ReservationID"].ToString()
                        ));
                    }

                    // Show count of active guests with selected date
                    string displayDate = searchDate.ToString("dd/MM/yyyy");
                    lblActiveGuestCount.Text = $"📊 มีผู้เข้าพัก {guests.Rows.Count} รายการ ในวันที่ {displayDate}";
                }
                else
                {
                    // No active guests for selected date
                    string displayDate = searchDate.ToString("dd/MM/yyyy");
                    lblActiveGuestCount.Text = $"ℹ️ ไม่มีผู้เข้าพักในวันที่ {displayDate}";
                }
            }
            catch (Exception ex)
            {
                code.Logs(conn, "Product.LoadActiveGuests Error", ex.Message, Session["User"]?.ToString());
                lblActiveGuestCount.Text = "⚠️ ไม่สามารถโหลดรายชื่อผู้เข้าพักได้";
            }
        }

        /// <summary>
        /// Guest selection changed - show/hide charge mode
        /// </summary>
        protected void ddlGuestReservation_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ddlGuestReservation.SelectedValue != "0")
            {
                // Guest selected - show charge mode options
                trChargeMode.Visible = true;
                trGuestInfo.Visible = true;

                // Load and display guest info
                int reservationId = Convert.ToInt32(ddlGuestReservation.SelectedValue);
                LoadGuestInfo(reservationId);

                // Default to Room Charge mode
                rblChargeMode.SelectedValue = "ROOM_CHARGE";

                // ✅ Disable payment controls when room charge mode is active
                DropDownList1.Enabled = false;
                DropDownList1.SelectedIndex = 0; // Reset to default
                CheckBox1.Enabled = false;
                CheckBox1.Checked = false;
                CheckBox2.Enabled = false;
                CheckBox2.Checked = false;
                Panel1.Visible = false;
            }
            else
            {
                // No guest selected - hide charge mode and guest info
                trChargeMode.Visible = false;
                trGuestInfo.Visible = false;
                DropDownList1.Enabled = true;
                CheckBox1.Enabled = true;
                CheckBox2.Enabled = true;
                lblGuestInfo.Text = "";
            }
        }

        /// <summary>
        /// Load and display guest information
        /// </summary>
        private void LoadGuestInfo(int reservationId)
        {
            try
            {
                // Get selected date from TextBox12, default to today if empty/invalid
                DateTime searchDate = DateTime.Now.Date;
                if (!string.IsNullOrEmpty(TextBox12.Text))
                {
                    DateTime.TryParse(TextBox12.Text, out searchDate);
                }

                var dt = _roomChargeDA.GetReservationById(reservationId, searchDate);

                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    string customerName = row["CustomerName"]?.ToString() ?? "ไม่ระบุ";
                    string roomNames = row["RoomNames"]?.ToString() ?? "ไม่ระบุห้อง";
                    string checkIn = row["CheckInDate"] != DBNull.Value ? Convert.ToDateTime(row["CheckInDate"]).ToString("dd/MM/yyyy") : "-";
                    string checkOut = row["CheckOutDate"] != DBNull.Value ? Convert.ToDateTime(row["CheckOutDate"]).ToString("dd/MM/yyyy") : "-";
                    string status = row["Status"]?.ToString() ?? "-";

                    decimal totalPrice = row["TotalPrice"] != DBNull.Value ? Convert.ToDecimal(row["TotalPrice"]) : 0;
                    decimal totalPaid = row["TotalPaid"] != DBNull.Value ? Convert.ToDecimal(row["TotalPaid"]) : 0;
                    decimal remaining = row["RemainingBalance"] != DBNull.Value ? Convert.ToDecimal(row["RemainingBalance"]) : 0;
                    decimal pendingCharges = row["PendingCharges"] != DBNull.Value ? Convert.ToDecimal(row["PendingCharges"]) : 0;

                    lblGuestInfo.Text = $@"
                        <strong>👤 ชื่อ:</strong> {customerName} &nbsp;&nbsp;
                        <strong>🏠 ห้อง:</strong> {roomNames} &nbsp;&nbsp;
                        <strong>📅 เข้า:</strong> {checkIn} &nbsp;&nbsp;
                        <strong>📅 ออก:</strong> {checkOut} &nbsp;&nbsp;
                        <strong>📊 สถานะ:</strong> {status}
                        <br/>
                        <strong>💰 ยอดรวม:</strong> {totalPrice:N2} บาท &nbsp;&nbsp;
                        <strong>✅ ชำระแล้ว:</strong> {totalPaid:N2} บาท &nbsp;&nbsp;
                        <strong>⏳ ค้างชำระ:</strong> {remaining:N2} บาท &nbsp;&nbsp;
                        <strong>🛒 สินค้าค้างชำระ:</strong> {pendingCharges:N2} บาท
                    ";
                }
            }
            catch (Exception ex)
            {
                code.Logs(conn, "Product.LoadGuestInfo Error", ex.Message, Session["User"]?.ToString());
                lblGuestInfo.Text = "⚠️ ไม่สามารถโหลดข้อมูลผู้เข้าพักได้";
            }
        }

        /// <summary>
        /// Process room charge (new method)
        /// </summary>
        private void ProcessRoomCharge(DataTable dtOrder)
        {
            try
            {
                // ✅ Validation: Check if guest is selected
                if (string.IsNullOrEmpty(ddlGuestReservation.SelectedValue) || ddlGuestReservation.SelectedValue == "0")
                {
                    throw new Exception("กรุณาเลือกห้องพักที่ต้องการชาร์จ");
                }

                // ✅ Validation: Check if cart has items
                if (dtOrder == null || dtOrder.Rows.Count == 0)
                {
                    throw new Exception("ไม่มีสินค้าในตะกร้า กรุณาเพิ่มสินค้าก่อนชาร์จเข้าห้อง");
                }

                int reservationId = Convert.ToInt32(ddlGuestReservation.SelectedValue);
                int? adminId = Session["UserID"] != null ? Convert.ToInt32(Session["UserID"]) : (int?)null;
                int itemCount = dtOrder.Rows.Count;

                // Get selected date from TextBox12, default to today if empty/invalid
                DateTime searchDate = DateTime.Now.Date;
                if (!string.IsNullOrEmpty(TextBox12.Text))
                {
                    DateTime.TryParse(TextBox12.Text, out searchDate);
                }

                // ✅ Validate reservation allows charging (with date filter)
                _roomChargeService.ValidateRoomChargeAllowed(reservationId, searchDate);

                // ✅ Validate stock availability for all items
                foreach (DataRow item in dtOrder.Rows)
                {
                    int productId = Convert.ToInt32(item["ID"]);
                    decimal quantity = Convert.ToDecimal(item["Amount"]);
                    decimal currentStock = _roomChargeDA.GetProductStock(productId);

                    if (currentStock < quantity)
                    {
                        string productName = item["Product_Name"].ToString();
                        throw new Exception($"สินค้า '{productName}' มีสต๊อกไม่เพียงพอ\\n\\nสต๊อกปัจจุบัน: {currentStock}\\nต้องการ: {quantity}");
                    }
                }

                // 🏨 Charge to room
                long chargeId = _roomChargeService.ChargeToRoom(
                    reservationId,
                    dtOrder,
                    adminId,
                    $"POS Sale on {DateTime.Now:yyyy-MM-dd HH:mm}"
                );

                // ✅ Clear cart
                dtOrder.Clear();
                Session["dtOrder"] = dtOrder;
                GridView1.DataSource = dtOrder;
                GridView1.DataBind();
                TextBox2.Text = "0";

                // ✅ Reload guest info to show updated balances
                LoadGuestInfo(reservationId);

                // ✅ Success message with details
                ClientScript.RegisterStartupScript(this.GetType(), "success",
                    $"alert('✅ บันทึกรายการชาร์จเข้าห้องเรียบร้อยแล้ว\\n\\n📝 รหัสการจอง: {reservationId}\\n📦 จำนวนรายการ: {itemCount} รายการ\\n\\n💡 รายการจะรวมในบิลเช็คเอาท์');",
                    true);

                // ✅ Log success
                code.Logs(conn, "Product.ProcessRoomCharge Success",
                    $"Reservation: {reservationId}, Items: {itemCount}, ChargeID: {chargeId}",
                    Session["User"]?.ToString());
            }
            catch (Exception ex)
            {
                ClientScript.RegisterStartupScript(this.GetType(), "error",
                    $"alert('❌ เกิดข้อผิดพลาดในการชาร์จเข้าห้อง:\\n\\n{ex.Message.Replace("'", "\\'")}');",
                    true);
                code.Logs(conn, "Product.ProcessRoomCharge Error",
                    $"Error: {ex.Message}, StackTrace: {ex.StackTrace}",
                    Session["User"]?.ToString());
            }
        }

        /// <summary>
        /// Process IMMEDIATE charge when PAY_NOW mode is selected
        /// This creates charge records linked to the receipt (already paid)
        /// </summary>
        private void ProcessImmediateCharge(DataTable dtOrder, int reservationId, string receiptId)
        {
            try
            {
                if (dtOrder == null || dtOrder.Rows.Count == 0)
                {
                    return; // No items to process
                }

                int? adminId = Session["UserID"] != null ? Convert.ToInt32(Session["UserID"]) : (int?)null;

                // Create IMMEDIATE charge records for each product
                foreach (DataRow item in dtOrder.Rows)
                {
                    int productId = Convert.ToInt32(item["ID"]);
                    string productName = item["Product_Name"].ToString();
                    string barcode = item["Barcode"]?.ToString();
                    int? categoryId = item["Category_ID"] != DBNull.Value
                        ? Convert.ToInt32(item["Category_ID"])
                        : (int?)null;
                    decimal quantity = Convert.ToDecimal(item["Amount"]);
                    decimal unitPrice = Convert.ToDecimal(item["Sell_Price"]);
                    decimal total = Convert.ToDecimal(item["Price_Total"]);

                    // Create charge record with IMMEDIATE type and PAID status
                    long chargeId = _roomChargeDA.CreateRoomCharge(
                        reservationId,
                        productId,
                        productName,
                        barcode,
                        categoryId,
                        quantity,
                        unitPrice,
                        total,
                        "IMMEDIATE", // ChargeType
                        adminId,
                        $"POS PAY_NOW - Receipt: {receiptId}"
                    );

                    // Mark as paid immediately
                    _roomChargeDA.MarkChargeAsPaid(chargeId, receiptId);
                }

                // Log success
                code.Logs(conn,
                    "Product.ProcessImmediateCharge",
                    $"Created {dtOrder.Rows.Count} IMMEDIATE charges for Reservation {reservationId}, Receipt {receiptId}",
                    adminId?.ToString() ?? "SYSTEM");
            }
            catch (Exception ex)
            {
                code.Logs(conn,
                    "Product.ProcessImmediateCharge Error",
                    $"Reservation: {reservationId}, Receipt: {receiptId}, Error: {ex.Message}",
                    Session["User"]?.ToString());
                throw; // Re-throw to be caught by caller
            }
        }

        #endregion
    }
}