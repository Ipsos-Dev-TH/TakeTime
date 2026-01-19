using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using System.IO;
using Microsoft.Reporting.WebForms;
using System.Globalization;
using iTextSharp.text.pdf.qrcode;
using iTextSharp.text.pdf;
using ECertificateAPI;
using System.Net.Mail;
using Take_Time_BangPhra.Admin;
using iTextSharp.text.pdf.parser;
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.Account.Report
{
    public partial class Receipt : System.Web.UI.Page
    {
        _Default code = new _Default();
        code code2 = new code();
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

            this.MaintainScrollPositionOnPostBack = true;
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
                DataTable dtCustomerType = code.DatabaseQuery(conn, "Select [Customer_Type],ID From Customer_Type");
                
                
                for (int i = 0; i < dtCustomerType.Rows.Count; i++)
                {
                    DropDownList8.Items.Add(new ListItem(dtCustomerType.Rows[i][0].ToString(), dtCustomerType.Rows[i][1].ToString()));
                }
                
                DropDownList8.DataBind();

                DataTable dtPaidHow = code.DatabaseQuery(SqlDataSource2.ConnectionString, SqlDataSource2.SelectCommand);
                for (int i = 0; i < dtPaidHow.Rows.Count; i++)
                {
                    DropDownList2.Items.Add(new ListItem(dtPaidHow.Rows[i]["Paid_How"].ToString(), dtPaidHow.Rows[i]["ID"].ToString()));
                }
                DropDownList2.DataBind();


                DataTable dtVatType = code.DatabaseQuery(SqlDataSource4.ConnectionString, SqlDataSource4.SelectCommand);
                for (int i = 0; i < dtVatType.Rows.Count; i++)
                {
                    DropDownList4.Items.Add(new ListItem(dtVatType.Rows[i]["Vat_Type"].ToString(), dtVatType.Rows[i]["ID"].ToString()));
                }
                DropDownList4.DataBind();

                getAddress("SELECT DISTINCT [Province] FROM [Address] order by Province ASC", "SELECT DISTINCT [District] FROM [Address] order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] order by SubDistrict ASC");

                string command = Request.QueryString["command"];
                string uid = Request.QueryString["uid"];

                // ✅ เก็บ command และ uid ใน ViewState เพื่อใช้ใน postback ต่อๆ ไป
                if (!string.IsNullOrEmpty(command))
                {
                    ViewState["EditCommand"] = command;
                }
                if (!string.IsNullOrEmpty(uid))
                {
                    ViewState["EditUID"] = uid;
                }

                if(command == "edit")
                {
                    // SECURE: Get receipt with parameterized query
                    var receiptParams = new Dictionary<string, object>
                    {
                        { "@UID", uid ?? "" }
                    };
                    DataTable dtReceipt = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Account_Receipt LEFT JOIN Reservation ON Reservation.ID = Reservation_ID WHERE Account_Receipt.UID = @UID",
                        receiptParams);
                    string id = dtReceipt.Rows[0]["ID"].ToString();

                    // SECURE: Get receipt details with parameterized query
                    var detailParams = new Dictionary<string, object>
                    {
                        { "@ReceiptID", id }
                    };
                    DataTable dtReceiptDetail = code.DatabaseQuerySafe(conn,
                        "SELECT Number,ProductType_ID,Product_Data,Product_Amount,Product_Unit,Price_PerPeice,Price_Amount FROM Account_Receipt_Detail WHERE Receipt_ID = @ReceiptID",
                        detailParams);
                    DataTable dtcustomer = new DataTable();
                    try
                    {
                        if( Convert.ToInt32(dtReceipt.Rows[0]["Customer_ID"].ToString()) > 0)
                        {
                            // SECURE: Get customer by ID with parameterized query
                            var customerIdParams = new Dictionary<string, object>
                            {
                                { "@CustomerID", dtReceipt.Rows[0]["Customer_ID"].ToString() }
                            };
                            dtcustomer = code.DatabaseQuerySafe(conn, @"SELECT
                                Customer.ID, Customer.MobilePhone, Customer.Name, Customer.NickName, Customer.ComeFrom,
                                Customer.Remark, Customer.Status, Customer.FullName, Customer.Address, Customer.Address1,
                                Customer.Address_ID, Customer.IDNumber, Customer.Email, Customer.Customer_Type_ID,
                                Customer.Branch_Number, Customer.TaxID, Customer.LastUpdated, Customer.LastUpdatedBy_ID,
                                Customer.CreatedDate, Customer.CreatedBy_ID, Customer.IsActive,
                                Customer_Type.Customer_Type, Customer_Type.Customer_Code,
                                Address.Province, Address.District, Address.SubDistrict, Address.PostalCode, Address.Address_Code
                                FROM Customer
                                LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID
                                LEFT JOIN Address ON Address.ID = Customer.Address_ID
                                WHERE Customer.ID = @CustomerID",
                                customerIdParams);
                        }
                        else
                        {
                            // SECURE: Get customer by mobile with parameterized query
                            var customerMobileParams = new Dictionary<string, object>
                            {
                                { "@MobilePhone", dtReceipt.Rows[0]["Customer_MobilePhone"].ToString() }
                            };
                            dtcustomer = code.DatabaseQuerySafe(conn, @"SELECT
                                Customer.ID, Customer.MobilePhone, Customer.Name, Customer.NickName, Customer.ComeFrom,
                                Customer.Remark, Customer.Status, Customer.FullName, Customer.Address, Customer.Address1,
                                Customer.Address_ID, Customer.IDNumber, Customer.Email, Customer.Customer_Type_ID,
                                Customer.Branch_Number, Customer.TaxID, Customer.LastUpdated, Customer.LastUpdatedBy_ID,
                                Customer.CreatedDate, Customer.CreatedBy_ID, Customer.IsActive,
                                Customer_Type.Customer_Type, Customer_Type.Customer_Code,
                                Address.Province, Address.District, Address.SubDistrict, Address.PostalCode, Address.Address_Code
                                FROM Customer
                                LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID
                                LEFT JOIN Address ON Address.ID = Customer.Address_ID
                                WHERE MobilePhone = @MobilePhone",
                                customerMobileParams);
                        }

                    }
                    catch {
                        // SECURE: Customer lookup fallback with parameterized query
                        var customerMobileFallbackParams = new Dictionary<string, object>
                        {
                            { "@MobilePhone", dtReceipt.Rows[0]["Customer_MobilePhone"].ToString() }
                        };
                        dtcustomer = code.DatabaseQuerySafe(conn, @"SELECT
                            Customer.ID, Customer.MobilePhone, Customer.Name, Customer.NickName, Customer.ComeFrom,
                            Customer.Remark, Customer.Status, Customer.FullName, Customer.Address, Customer.Address1,
                            Customer.Address_ID, Customer.IDNumber, Customer.Email, Customer.Customer_Type_ID,
                            Customer.Branch_Number, Customer.TaxID, Customer.LastUpdated, Customer.LastUpdatedBy_ID,
                            Customer.CreatedDate, Customer.CreatedBy_ID, Customer.IsActive,
                            Customer_Type.Customer_Type, Customer_Type.Customer_Code,
                            Address.Province, Address.District, Address.SubDistrict, Address.PostalCode, Address.Address_Code
                            FROM Customer
                            LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID
                            LEFT JOIN Address ON Address.ID = Customer.Address_ID
                            WHERE MobilePhone = @MobilePhone",
                            customerMobileFallbackParams);

                    }

                    // 🔧 FIX: Check if customer data exists before loading
                    bool hasCustomerData = dtcustomer != null && dtcustomer.Rows.Count > 0;

                    try //Address
                    {
                        if (hasCustomerData)
                        {
                            try
                            {
                                TextBox16.Text = dtcustomer.Rows[0]["PostalCode"]?.ToString() ?? "";

                                if (!string.IsNullOrEmpty(dtcustomer.Rows[0]["Province"]?.ToString()))
                                {
                                    DropDownList5.ClearSelection();
                                    var provinceItem = DropDownList5.Items.FindByText(dtcustomer.Rows[0]["Province"].ToString());
                                    if (provinceItem != null)
                                    {
                                        provinceItem.Selected = true;
                                        DropDownList5.SelectedIndex = DropDownList5.Items.IndexOf(provinceItem);
                                    }
                                }
                                if (!string.IsNullOrEmpty(dtcustomer.Rows[0]["District"]?.ToString()))
                                {
                                    DropDownList6.ClearSelection();
                                    var districtItem = DropDownList6.Items.FindByText(dtcustomer.Rows[0]["District"].ToString());
                                    if (districtItem != null)
                                    {
                                        districtItem.Selected = true;
                                        DropDownList6.SelectedIndex = DropDownList6.Items.IndexOf(districtItem);
                                    }
                                }
                                if (!string.IsNullOrEmpty(dtcustomer.Rows[0]["SubDistrict"]?.ToString()))
                                {
                                    DropDownList7.ClearSelection();
                                    var subDistrictItem = DropDownList7.Items.FindByText(dtcustomer.Rows[0]["SubDistrict"].ToString());
                                    if (subDistrictItem != null)
                                    {
                                        subDistrictItem.Selected = true;
                                        DropDownList7.SelectedIndex = DropDownList7.Items.IndexOf(subDistrictItem);
                                    }
                                }
                            }
                            catch { }

                            string customerTypeId = dtcustomer.Rows[0]["Customer_Type_ID"]?.ToString() ?? "2";
                            DropDownList8.ClearSelection();
                            var typeItem = DropDownList8.Items.FindByValue(customerTypeId);
                            if (typeItem != null)
                            {
                                typeItem.Selected = true;
                                DropDownList8.SelectedIndex = DropDownList8.Items.IndexOf(typeItem);
                            }
                            DropDownList8.DataBind();
                            if (DropDownList8.SelectedIndex == 0)
                            {
                                TextBox7.Visible = true;
                                TextBox7.Text = dtcustomer.Rows[0]["Branch_Number"]?.ToString() ?? "";
                            }
                            else
                            {
                                TextBox7.Visible = false;
                            }
                        }
                        else
                        {
                            // 🔧 No customer found - set defaults
                            DropDownList8.ClearSelection();
                            var defaultTypeItem = DropDownList8.Items.FindByValue("2"); // บุคคลธรรมดา
                            if (defaultTypeItem != null)
                            {
                                defaultTypeItem.Selected = true;
                                DropDownList8.SelectedIndex = DropDownList8.Items.IndexOf(defaultTypeItem);
                            }
                            DropDownList8.DataBind();
                            TextBox7.Visible = false;
                        }

                        DropDownList2.SelectedIndex = DropDownList2.Items.IndexOf(DropDownList2.Items.FindByText(dtReceipt.Rows[0]["Paid_Type"].ToString()));
                        DropDownList2.DataBind();

                        DropDownList4.SelectedIndex = DropDownList4.Items.IndexOf(DropDownList4.Items.FindByValue("1"));
                        DropDownList4.DataBind();
                    }
                    catch { }

                    if (dtReceipt.Rows[0]["NoNameinReceipt"].ToString().ToLower() == "true")
                    {
                        CheckBox3.Checked = true;
                        CheckBox3.DataBind();
                        TextBox10.Text = "ประสงค์ไม่รับใบกำกับภาษี";
                    }

                    GridView1.DataSource = dtReceiptDetail;
                    GridView1.DataBind();

                    // 🔍 Debug Page_Load
                    System.Diagnostics.Debug.WriteLine($"=== [Receipt Edit - Page_Load] ===");
                    System.Diagnostics.Debug.WriteLine($"ID from DB: {id}");
                    System.Diagnostics.Debug.WriteLine($"IsPostBack: {IsPostBack}");
                    System.Diagnostics.Debug.WriteLine($"CheckBox2.Checked: {CheckBox2.Checked}");
                    System.Diagnostics.Debug.WriteLine($"TextBox5.Text (before): '{TextBox5.Text}'");

                    // ⚠️ ไม่ set TextBox5.Text = id ถ้า CheckBox2 ถูก check แล้ว (user กำลังแก้ไขเลขที่)
                    // เพราะจะทำให้ค่าที่ user กรอกหายไปเมื่อ postback
                    if (!CheckBox2.Checked)
                    {
                        TextBox5.Text = id;
                        System.Diagnostics.Debug.WriteLine($"✏️ Set TextBox5.Text = {id}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ Keep TextBox5.Text = '{TextBox5.Text}' (CheckBox2 is checked)");
                    }
                    System.Diagnostics.Debug.WriteLine($"TextBox5.Text (after): '{TextBox5.Text}'");
                    System.Diagnostics.Debug.WriteLine($"=============================");

                    TextBox8.Text = Convert.ToDateTime(dtReceipt.Rows[0]["Created_Date"].ToString()).ToString("yyyy-MM-dd") ;
                    TextBox9.Text = dtReceipt.Rows[0]["Reservation_ID"].ToString();

                    TextBox3.Text = dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"].ToString();
                    TextBox4.Text = dtReceipt.Rows[0]["Vat"].ToString();
                    TextBox6.Text = dtReceipt.Rows[0]["Total_Amount"].ToString();

                    try
                    {

                        if (dtReceipt.Rows[0]["Etax"].ToString().ToLower() == "false")
                        {
                            CheckBox5.Checked = false;
                            CheckBox5.DataBind();
                        }
                        if (dtReceipt.Rows[0]["Etax"].ToString().ToLower() == "true")
                        {
                            CheckBox5.Checked = true;
                            CheckBox5.DataBind();
                            TextBox7.Visible = true;
                            // 🔧 FIX: Check if customer data exists before accessing Branch_Number
                            if (hasCustomerData)
                            {
                                TextBox7.Text = dtcustomer.Rows[0]["Branch_Number"]?.ToString() ?? "00000";
                            }
                            else
                            {
                                TextBox7.Text = "00000";
                            }
                        }
                    }
                    catch { }

                    if (CheckBox3.Checked == true)
                    {
                        //TextBox10.Text = "ประสงค์ไม่รับใบกำกับภาษี";
                        //TextBox11.Text = "";
                        //TextBox12.Text = "";
                        // TextBox13.Text = "";

                    }
                    else
                    {
                        // 🔧 FIX: Check if customer data exists before loading
                        if (hasCustomerData)
                        {
                            TextBox10.Text = dtcustomer.Rows[0]["FullName"]?.ToString() ?? "";
                            TextBox11.Text = dtcustomer.Rows[0]["Address"]?.ToString() ?? "";
                            TextBox12.Text = dtcustomer.Rows[0]["IDNumber"]?.ToString() ?? "";
                            TextBox13.Text = dtcustomer.Rows[0]["MobilePhone"]?.ToString() ?? "";
                            TextBox17.Text = dtcustomer.Rows[0]["Email"]?.ToString() ?? "";
                            TextBox18.Text = dtcustomer.Rows[0]["Address1"]?.ToString() ?? "";
                        }
                        else
                        {
                            // No customer found - use phone from reservation as default
                            TextBox13.Text = dtReceipt.Rows[0]["Customer_MobilePhone"]?.ToString() ?? "";
                        }
                    }
                    

                    DropDownList2.DataBind();
                    DropDownList2.SelectedIndex = DropDownList2.Items.IndexOf(DropDownList2.Items.FindByText(dtReceipt.Rows[0]["Paid_Type"].ToString()));
                    DropDownList2.DataBind();

                    DropDownList4.SelectedIndex = 1;
                    DropDownList4.DataBind();

                    if (dtReceipt.Rows[0]["IsDeposit"].ToString().ToLower() == "true")
                    {
                        CheckBox1.Checked = true;
                    }
                    Session["dtDetail"] = dtReceiptDetail;

                    Panel1.Visible = true;
                    string Year = Convert.ToDateTime(TextBox8.Text).Year.ToString();
                    string Month = Convert.ToDateTime(TextBox8.Text).Month.ToString();
                    string Day = Convert.ToDateTime(TextBox8.Text).Day.ToString();
                    string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
                    if (File.Exists(path + "\\" + Year + "\\" + Month + "\\" + dtReceipt.Rows[0]["ID"].ToString() + "_" + uid + ".pdf"))
                    {
                        myFrame.Attributes["src"] = "/Documents/Receipt/" + Year + "/" + Month + "/" + dtReceipt.Rows[0]["ID"].ToString() + "_" + uid+ ".pdf";
                    }
                    else
                    {
                        myFrame.Attributes["src"] = "/Documents/Receipt/" + Year + "/" + Month + "/" + dtReceipt.Rows[0]["ID"].ToString() + ".pdf";
                    }
                        

                }

                DataTable dtDetail = new DataTable();
                try
                {
                    dtDetail.Columns.Add("Number");
                    dtDetail.Columns.Add("ProductType_ID");
                    dtDetail.Columns.Add("Product_Data");
                    dtDetail.Columns.Add("Product_Amount");
                    dtDetail.Columns.Add("Product_Unit");
                    dtDetail.Columns.Add("Price_PerPeice");
                    dtDetail.Columns.Add("Price_Amount");

                }
                catch
                {

                }
                if (command == "edit")
                {
                    
                }
                else
                {
                    Session["dtDetail"] = dtDetail;
                    TextBox8.Text = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                
                DataTable dtUpload = new DataTable();
                try
                {
                    dtUpload.Columns.Add("Name");
                }
                catch
                {

                }
                Session["dtUpload"] = dtUpload;
            }

        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            Label1.Text = DropDownList4.SelectedItem.Text;
            
            if (TextBox1.Text.Length > 1 && TextBox2.Text.Length > 0)
            {
                DataTable dtDetail = (DataTable)Session["dtDetail"];
                dtDetail.Rows.Add(dtDetail.Rows.Count+1,DropDownList3.SelectedValue,TextBox1.Text, TextBox14.Text, TextBox15.Text, NumberHelper.TwoDecimalPoints(Convert.ToDouble(TextBox2.Text)),NumberHelper.TwoDecimalPoints(Convert.ToDouble(TextBox14.Text) *Convert.ToDouble(TextBox2.Text)));
                Session["dtDetail"] = (DataTable)dtDetail;
                GridView1.DataSource = dtDetail;
                GridView1.DataBind();
                calAmount(dtDetail);
                TextBox1.Text = "";
                TextBox2.Text = "";
            }
            else
            {

            }
        }

        public void calAmount(DataTable dtDetail)
        {
            double totalAmount = 0;
            for(int i=0;i<dtDetail.Rows.Count;i++)
            {
                totalAmount += Convert.ToDouble(dtDetail.Rows[i]["Price_Amount"].ToString());
            }

            // SECURE: Get VAT percent with parameterized query
            var vatParams = new Dictionary<string, object>
            {
                { "@VatTypeID", DropDownList4.SelectedValue }
            };
            int vatPercent = Convert.ToInt32(code.DatabaseQuerySafe(conn,
                "SELECT Vat_Percent FROM Account_Vat_Type WHERE Status = 'True' AND ID = @VatTypeID",
                vatParams).Rows[0][0].ToString());
            double AmountExcludeVat = (totalAmount * 100) / (100 + vatPercent);
            double vat = totalAmount - AmountExcludeVat;
            TextBox3.Text = NumberHelper.TwoDecimalPoints(AmountExcludeVat).ToString();
            TextBox4.Text = NumberHelper.TwoDecimalPoints(vat).ToString();
            TextBox6.Text = NumberHelper.TwoDecimalPoints(totalAmount).ToString();

        }

        protected void GridView1_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            DataTable dtDetail = (DataTable)Session["dtDetail"];
            dtDetail.Rows[e.RowIndex].Delete();
            dtDetail.AcceptChanges();
            for(int i = 0;i<dtDetail.Rows.Count;i++)
            {
                dtDetail.Rows[i][0] = i + 1;
            }
            Session["dtDetail"] = (DataTable)dtDetail;
            GridView1.DataSource = dtDetail;
            GridView1.DataBind();
            calAmount(dtDetail);
        }

        protected void DropDownList4_SelectedIndexChanged(object sender, EventArgs e)
        {
            Label1.Text = DropDownList4.SelectedItem.Text;
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            if (TextBox6.Text.Length > 0 && DropDownList2.SelectedIndex > 0 && DropDownList4.SelectedIndex > 0)
            {
                // ✅ อ่าน command และ uid จาก QueryString หรือ ViewState (สำหรับ postback)
                string command = Request.QueryString["command"];
                string uid = Request.QueryString["uid"];

                // ถ้า QueryString เป็น null (postback) → ใช้ ViewState
                if (string.IsNullOrEmpty(command) && ViewState["EditCommand"] != null)
                {
                    command = ViewState["EditCommand"].ToString();
                }
                if (string.IsNullOrEmpty(uid) && ViewState["EditUID"] != null)
                {
                    uid = ViewState["EditUID"].ToString();
                }

                string id = "";
                DataTable dtReceipt = new DataTable();
                DateTime receiptDate = Convert.ToDateTime(TextBox8.Text);

                // Extract Year/Month for directory structure
                string Year = receiptDate.Year.ToString();
                string Month = receiptDate.Month.ToString();

                DataTable dtDetail = (DataTable)Session["dtDetail"];
                string docNum = "";

                // 🔍 Debug: ตรวจสอบค่า command ก่อนทำอะไร
                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"==================================================");
                System.Diagnostics.Debug.WriteLine($"=== [Button3_Click START] ===");
                System.Diagnostics.Debug.WriteLine($"QueryString['command'] = '{Request.QueryString["command"]}'");
                System.Diagnostics.Debug.WriteLine($"ViewState['EditCommand'] = '{ViewState["EditCommand"]}'");
                System.Diagnostics.Debug.WriteLine($"→ Final command = '{command}'");
                System.Diagnostics.Debug.WriteLine($"QueryString['uid'] = '{Request.QueryString["uid"]}'");
                System.Diagnostics.Debug.WriteLine($"ViewState['EditUID'] = '{ViewState["EditUID"]}'");
                System.Diagnostics.Debug.WriteLine($"→ Final uid = '{uid}'");
                System.Diagnostics.Debug.WriteLine($"==================================================");
                System.Diagnostics.Debug.WriteLine($"");

                // ✅ ถ้าเป็น edit mode → ดึงข้อมูลเดิม
                if (command == "edit")
                {
                    // SECURE: Get receipt with parameterized query
                    var receiptUidEditParams = new Dictionary<string, object>
                    {
                        { "@UID", uid ?? "" }
                    };
                    dtReceipt = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Account_Receipt LEFT JOIN Reservation ON Reservation.ID = Reservation_ID WHERE Account_Receipt.UID = @UID",
                        receiptUidEditParams);
                    id = dtReceipt.Rows[0]["ID"].ToString();
                    System.Diagnostics.Debug.WriteLine($"[Edit Mode] Original ID from DB: {id}");
                }

                // 🔍 Debug: ดูค่าทั้งหมดก่อนตัดสินใจ
                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"=== [Receipt Number Decision] ===");
                System.Diagnostics.Debug.WriteLine($"Mode: {(command == "edit" ? "EDIT" : "CREATE")}");
                System.Diagnostics.Debug.WriteLine($"CheckBox2.Checked: {CheckBox2.Checked}");
                System.Diagnostics.Debug.WriteLine($"TextBox5.Text: '{TextBox5.Text}'");
                System.Diagnostics.Debug.WriteLine($"TextBox5.ReadOnly: {TextBox5.ReadOnly}");

                // ✅ Priority 1: ถ้า CheckBox2 checked และมีเลขกรอก → ใช้เลขที่กรอก (ทั้ง CREATE และ EDIT mode)
                if (CheckBox2.Checked == true && !string.IsNullOrWhiteSpace(TextBox5.Text))
                {
                    docNum = TextBox5.Text.Trim();
                    System.Diagnostics.Debug.WriteLine($"✅ DECISION: Using CUSTOM receipt number from TextBox5: {docNum}");
                }
                // ✅ Priority 2: ถ้าเป็น EDIT mode และไม่ได้กรอกเลข → ใช้เลขเดิม
                else if (command == "edit")
                {
                    docNum = id;
                    System.Diagnostics.Debug.WriteLine($"✅ DECISION: Using ORIGINAL receipt ID (edit mode): {docNum}");
                }
                // ✅ Priority 3: ถ้าเป็น CREATE mode และไม่ได้กรอกเลข → สร้างเลขใหม่
                else
                {
                    docNum = _documentHelper.CreateDocumentNumber("Account_Receipt", "REC", receiptDate);
                    System.Diagnostics.Debug.WriteLine($"✅ DECISION: Generated NEW receipt number (create mode): {docNum}");
                }
                System.Diagnostics.Debug.WriteLine($"====================================");
                System.Diagnostics.Debug.WriteLine($"");

                // 🔍 Final decision
                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"=== FINAL DECISION ===");
                System.Diagnostics.Debug.WriteLine($"📋 docNum that will be used: '{docNum}'");
                System.Diagnostics.Debug.WriteLine($"======================");
                System.Diagnostics.Debug.WriteLine($"");

                // ✅ Validate new receipt number (if editing and number changed)
                if (command == "edit")
                {
                    // id already contains originalID from line 433

                    // ถ้าเลขที่เปลี่ยน → ต้อง check ว่าเลขใหม่มีอยู่แล้วหรือไม่
                    if (docNum != id)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Duplicate Check] Receipt number changed from '{id}' to '{docNum}'");

                        // SECURE: Check duplicate with parameterized query
                        var checkDuplicateParams = new Dictionary<string, object>
                        {
                            { "@DocNum", docNum }
                        };
                        DataTable dtCheckDuplicate = code.DatabaseQuerySafe(conn,
                            "SELECT ID FROM Account_Receipt WHERE ID = @DocNum",
                            checkDuplicateParams);

                        if (dtCheckDuplicate.Rows.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ [Duplicate Check] Receipt number '{docNum}' already exists!");

                            ClientScript.RegisterStartupScript(this.GetType(), "duplicateReceipt",
                                "alert('❌ ไม่สามารถใช้เลขที่ " + docNum + " ได้\\n\\nเพราะมีอยู่ในระบบแล้ว\\nกรุณาใช้เลขที่อื่น');", true);
                            return;
                        }

                        System.Diagnostics.Debug.WriteLine($"✅ [Duplicate Check] Receipt number '{docNum}' is available");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Duplicate Check] Receipt number unchanged: '{docNum}'");
                    }
                }

                string RecNumber = docNum;
                int reservation_id = 0;

                // ✅ Validate Customer_Type_ID with fallback to default
                int customerTypeId = 1; // Default: บุคคลธรรมดา
                if (!string.IsNullOrEmpty(DropDownList8.SelectedValue))
                {
                    if (!int.TryParse(DropDownList8.SelectedValue, out customerTypeId))
                    {
                        customerTypeId = 1; // Fallback
                    }
                }

                // ✅ Validate Address_ID with fallback to 0
                int addressId = 0;
                try
                {
                    string addressIdString = _addressHelper.GetAddressIdString(
                        TextBox16.Text,
                        DropDownList5.SelectedItem?.Text ?? "",
                        DropDownList6.SelectedItem?.Text ?? "",
                        DropDownList7.SelectedItem?.Text ?? ""
                    );

                    if (!string.IsNullOrEmpty(addressIdString))
                    {
                        if (!int.TryParse(addressIdString, out addressId))
                        {
                            addressId = 0; // Fallback
                        }
                    }
                }
                catch
                {
                    addressId = 0; // Fallback on error
                }

                // Upsert customer data (insert or update) - ensures no duplicates and always latest data
                // ALWAYS matches by MobilePhone - ensures only 1 record per phone number
                // If customer type changes from Individual to Corporate (or vice versa), it updates the existing record
                long customerId = code.UpsertCustomer(
                    conn,
                    TextBox13.Text,  // MobilePhone
                    TextBox10.Text,  // Name
                    "",  // NickName
                    "",  // ComeFrom
                    "",  // Remark
                    TextBox10.Text,  // FullName
                    ValidationHelper.CleanText(TextBox11.Text),  // Address
                    TextBox12.Text,  // IDNumber
                    TextBox17.Text,  // Email
                    customerTypeId,  // Customer_Type_ID (validated)
                    addressId,  // Address_ID (validated)
                    TextBox18.Text,  // Address1
                    TextBox7.Text  // Branch_Number
                );

                // SECURE: Query customer data after upsert with parameterized query
                var customerIdQueryParams = new Dictionary<string, object>
                {
                    { "@CustomerID", customerId }
                };
                DataTable dtcustomer = code.DatabaseQuerySafe(conn, @"SELECT
                    Customer.ID, Customer.MobilePhone, Customer.Name, Customer.NickName, Customer.ComeFrom,
                    Customer.Remark, Customer.Status, Customer.FullName, Customer.Address, Customer.Address1,
                    Customer.Address_ID, Customer.IDNumber, Customer.Email, Customer.Customer_Type_ID,
                    Customer.Branch_Number, Customer.TaxID, Customer.LastUpdated, Customer.LastUpdatedBy_ID,
                    Customer.CreatedDate, Customer.CreatedBy_ID, Customer.IsActive,
                    Customer_Type.Customer_Type, Customer_Type.Customer_Code,
                    Address.Province, Address.District, Address.SubDistrict, Address.PostalCode, Address.Address_Code
                    FROM Customer
                    LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID
                    LEFT JOIN Address ON Address.ID = Customer.Address_ID
                    WHERE Customer.ID = @CustomerID",
                    customerIdQueryParams);

                // ⚠️ Validate that customer exists before proceeding
                if (dtcustomer.Rows.Count == 0)
                {
                    Response.Write("<script>alert('เกิดข้อผิดพลาด: ไม่พบข้อมูลลูกค้า (Customer ID: " + customerId + ")'); window.history.back();</script>");
                    return;
                }


                try
                {
                    // ✅ ถ้า edit mode → query ด้วย UID (เพราะ ID อาจจะเปลี่ยน, แต่ UID ไม่เปลี่ยน)
                    if (command == "edit")
                    {
                        // SECURE: Get receipt by UID with parameterized query
                        // ✅ Use LEFT JOIN because POS receipts may not have Reservation_ID
                        var receiptUidParams = new Dictionary<string, object>
                        {
                            { "@UID", uid ?? "" }
                        };
                        dtReceipt = code.DatabaseQuerySafe(conn,
                            "SELECT * FROM [Account_Receipt] LEFT JOIN Reservation ON Reservation.ID = Reservation_ID WHERE Account_Receipt.UID = @UID",
                            receiptUidParams);
                        System.Diagnostics.Debug.WriteLine($"[Edit Mode] Query Receipt by UID: {uid}");
                    }
                    else
                    {
                        // SECURE: Get receipt by ID with parameterized query
                        // ✅ Use LEFT JOIN because POS receipts may not have Reservation_ID
                        var receiptIdParams = new Dictionary<string, object>
                        {
                            { "@ID", RecNumber }
                        };
                        dtReceipt = code.DatabaseQuerySafe(conn,
                            "SELECT * FROM [Account_Receipt] LEFT JOIN Reservation ON Reservation.ID = Reservation_ID WHERE Account_Receipt.ID = @ID",
                            receiptIdParams);
                        System.Diagnostics.Debug.WriteLine($"[Create Mode] Query Receipt by ID: {RecNumber}");
                    }

                    if (dtReceipt.Rows.Count <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Receipt not found - this is normal for CREATE mode");
                        //reservation_id = code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation] ([Customer_MobilePhone],[CheckinDate],[CheckoutDate],[StayDays],[Status],[TotalPrice],[Deposit],[Remark],[Reserve_By],[Created_Date],NoNameinReceipt) VALUES ('" + TextBox13.Text + "','" + Convert.ToDateTime(TextBox8.Text).ToString("yyyy-MM-dd") + "','" + Convert.ToDateTime(TextBox8.Text).AddDays(Convert.ToDouble(1)).ToString("yyyy-MM-dd") + "'," + "1" + ",N'ชำระเงินแล้ว'," + TextBox6.Text + "," + TextBox6.Text + ",N'" + TextBox6.Text + "', N'" + Session["UserName"].ToString() + "','" + DateTime.Now + "','False') SELECT SCOPE_IDENTITY(); ");
                        //dtReceipt = code.DatabaseQuery(conn, "SELECT * FROM [Account_Receipt] left join Reservation on Reservation.ID = Reservation_ID Where Account_Receipt.ID = '" + RecNumber + "'");
                    }
                    else
                    {
                        // ✅ Handle NULL Reservation_ID (POS receipts without reservation)
                        var resIdValue = dtReceipt.Rows[0]["Reservation_ID"];
                        if (resIdValue != null && resIdValue != DBNull.Value && !string.IsNullOrEmpty(resIdValue.ToString()))
                        {
                            reservation_id = Convert.ToInt32(resIdValue);
                            System.Diagnostics.Debug.WriteLine($"✅ Found Receipt - Reservation_ID: {reservation_id}");
                        }
                        else
                        {
                            reservation_id = 0;
                            System.Diagnostics.Debug.WriteLine($"✅ Found Receipt (POS) - No Reservation_ID");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error querying Receipt: {ex.Message}");

                    // Fallback: try with ID
                    if (command == "edit")
                    {
                        // SECURE: Fallback get receipt by UID with parameterized query
                        var fallbackUidParams = new Dictionary<string, object>
                        {
                            { "@UID", uid ?? "" }
                        };
                        dtReceipt = code.DatabaseQuerySafe(conn,
                            "SELECT * FROM [Account_Receipt] LEFT JOIN Reservation ON Reservation.ID = Reservation_ID WHERE Account_Receipt.UID = @UID",
                            fallbackUidParams);
                    }
                    else
                    {
                        // SECURE: Fallback get receipt by ID with parameterized query
                        var fallbackIdParams = new Dictionary<string, object>
                        {
                            { "@ID", RecNumber }
                        };
                        dtReceipt = code.DatabaseQuerySafe(conn,
                            "SELECT * FROM [Account_Receipt] LEFT JOIN Reservation ON Reservation.ID = Reservation_ID WHERE Account_Receipt.ID = @ID",
                            fallbackIdParams);
                    }
                }

                if (command == "edit")
                {
                    // ✅ Validate that receipt exists before proceeding with edit
                    if (dtReceipt == null || dtReceipt.Rows.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ [Edit Mode Error] Receipt not found for UID: {uid}");
                        ClientScript.RegisterStartupScript(this.GetType(), "receiptNotFound",
                            "alert('❌ ไม่พบใบเสร็จที่ต้องการแก้ไข\\n\\nกรุณาลองใหม่อีกครั้ง');", true);
                        return;
                    }

                    // ✅ แทนที่จะ DELETE + INSERT → ใช้ UPDATE ID แทน (เพื่อไม่ให้เจอ FK constraint error)
                    string originalUID = dtReceipt.Rows[0]["UID"].ToString();
                    string originalID = id;  // เลขที่ใบเสร็จเดิม
                    string newID = docNum;   // เลขที่ใบเสร็จใหม่

                    System.Diagnostics.Debug.WriteLine($"");
                    System.Diagnostics.Debug.WriteLine($"=== [Receipt Edit Mode] ===");
                    System.Diagnostics.Debug.WriteLine($"Original ID: {originalID}");
                    System.Diagnostics.Debug.WriteLine($"New ID: {newID}");
                    System.Diagnostics.Debug.WriteLine($"UID: {originalUID}");

                    // ถ้าเลขที่เปลี่ยน → ใช้ INSERT + UPDATE FK + DELETE แทนการ UPDATE PK
                    // เพราะ SQL Server ไม่ยอมให้ UPDATE PK ถ้ายังมี FK อ้างอิงอยู่
                    if (originalID != newID)
                    {
                        System.Diagnostics.Debug.WriteLine($"Receipt ID changed - Using INSERT + UPDATE FK + DELETE pattern...");

                        try
                        {
                            // 🆕 STEP 1: INSERT Account_Receipt ใหม่ด้วย newID และข้อมูลที่ถูก update
                            // ใช้ค่า NoNameinReceipt จาก record เดิม (ถ้ามี column นี้)
                            string noNameInReceipt = "False";
                            try
                            {
                                if (dtReceipt.Columns.Contains("NoNameinReceipt"))
                                {
                                    noNameInReceipt = dtReceipt.Rows[0]["NoNameinReceipt"].ToString();
                                }
                            }
                            catch { }

                            // SECURE: INSERT with parameterized query
                            var insertReceiptParams = new Dictionary<string, object>
                            {
                                { "@NewID", newID },
                                { "@ReservationID", reservation_id > 0 ? reservation_id.ToString() : TextBox9.Text },
                                { "@CreatedDate", Convert.ToDateTime(TextBox8.Text).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                                { "@TotalAmount", TextBox6.Text },
                                { "@Vat", TextBox4.Text },
                                { "@TotalAmountExcludeVat", TextBox3.Text },
                                { "@IsDeposit", CheckBox1.Checked },
                                { "@PaidType", DropDownList2.SelectedItem.Text },
                                { "@CreatedByID", Session["UserID"].ToString() },
                                { "@Etax", CheckBox5.Checked },
                                { "@CustomerID", customerId },
                                { "@UID", originalUID }
                            };
                            code.DatabaseInsertSafe(conn,
                                "INSERT INTO [dbo].[Account_Receipt] " +
                                "([ID],[Reservation_ID],[Created_Date],[Total_Amount],[Vat],[Total_Amount_Exclude_Vat]," +
                                "[IsDeposit],[UseDeposit],[Paid_Type],[Status],[Created_By_ID],[Etax],[Customer_ID],[UID]) " +
                                "VALUES (@NewID,@ReservationID,@CreatedDate,@TotalAmount,@Vat,@TotalAmountExcludeVat,@IsDeposit,'False',@PaidType,'Normal',@CreatedByID,@Etax,@CustomerID,@UID)",
                                insertReceiptParams);
                            System.Diagnostics.Debug.WriteLine($"✅ Step 1: Inserted new Account_Receipt with ID: {newID} (with updated data)");

                            // SECURE: STEP 2: UPDATE Payment_Slips with parameterized query
                            var updatePaymentSlipsParams = new Dictionary<string, object>
                            {
                                { "@NewID", newID },
                                { "@OriginalID", originalID }
                            };
                            code.DatabaseInsertSafe(conn,
                                "UPDATE [dbo].[Payment_Slips] SET Account_Receipt_ID = @NewID WHERE Account_Receipt_ID = @OriginalID",
                                updatePaymentSlipsParams);
                            System.Diagnostics.Debug.WriteLine($"✅ Step 2: Updated Payment_Slips FK: {originalID} → {newID}");

                            // SECURE: STEP 3: UPDATE Payment_History with parameterized query
                            var updatePaymentHistoryParams = new Dictionary<string, object>
                            {
                                { "@NewID", newID },
                                { "@OriginalID", originalID }
                            };
                            code.DatabaseInsertSafe(conn,
                                "UPDATE [dbo].[Payment_History] SET Receipt_ID = @NewID WHERE Receipt_ID = @OriginalID",
                                updatePaymentHistoryParams);
                            System.Diagnostics.Debug.WriteLine($"✅ Step 3: Updated Payment_History FK: {originalID} → {newID}");

                            // SECURE: STEP 4: UPDATE Account_Receipt_Detail with parameterized query
                            var updateReceiptDetailParams = new Dictionary<string, object>
                            {
                                { "@NewID", newID },
                                { "@OriginalID", originalID }
                            };
                            code.DatabaseInsertSafe(conn,
                                "UPDATE [dbo].[Account_Receipt_Detail] SET Receipt_ID = @NewID WHERE Receipt_ID = @OriginalID",
                                updateReceiptDetailParams);
                            System.Diagnostics.Debug.WriteLine($"✅ Step 4: Updated Account_Receipt_Detail FK: {originalID} → {newID}");

                            // SECURE: STEP 5: UPDATE Product_Out with parameterized query
                            var updateProductOutParams = new Dictionary<string, object>
                            {
                                { "@NewID", newID },
                                { "@OriginalID", originalID }
                            };
                            code.DatabaseInsertSafe(conn,
                                "UPDATE [dbo].[Product_Out] SET Account_Receipt_ID = @NewID WHERE Account_Receipt_ID = @OriginalID",
                                updateProductOutParams);
                            System.Diagnostics.Debug.WriteLine($"✅ Step 5: Updated Product_Out FK: {originalID} → {newID}");

                            // SECURE: STEP 6: DELETE old Account_Receipt with parameterized query
                            var deleteOldReceiptParams = new Dictionary<string, object>
                            {
                                { "@OriginalID", originalID },
                                { "@OriginalUID", originalUID }
                            };
                            code.DatabaseInsertSafe(conn,
                                "DELETE FROM [dbo].[Account_Receipt] WHERE ID = @OriginalID AND UID = @OriginalUID",
                                deleteOldReceiptParams);
                            System.Diagnostics.Debug.WriteLine($"✅ Step 6: Deleted old Account_Receipt with ID: {originalID}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ Error updating Receipt ID: {ex.Message}");
                            ClientScript.RegisterStartupScript(this.GetType(), "updateError",
                                "alert('❌ เกิดข้อผิดพลาดในการเปลี่ยนเลขที่ใบเสร็จ\\n\\n" + ex.Message.Replace("'", "\\'") + "');", true);
                            return;
                        }
                    }
                    else
                    {
                        // ถ้าเลขที่ไม่เปลี่ยน → UPDATE ข้อมูลอื่นๆ
                        System.Diagnostics.Debug.WriteLine($"Receipt ID unchanged - Updating data only");

                        try
                        {
                            // SECURE: UPDATE with parameterized query
                            var updateReceiptDataParams = new Dictionary<string, object>
                            {
                                { "@ReservationID", reservation_id > 0 ? reservation_id.ToString() : TextBox9.Text },
                                { "@CreatedDate", Convert.ToDateTime(TextBox8.Text).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                                { "@TotalAmount", TextBox6.Text },
                                { "@Vat", TextBox4.Text },
                                { "@TotalAmountExcludeVat", TextBox3.Text },
                                { "@IsDeposit", CheckBox1.Checked },
                                { "@PaidType", DropDownList2.SelectedItem.Text },
                                { "@Etax", CheckBox5.Checked },
                                { "@CustomerID", customerId },
                                { "@OriginalUID", originalUID }
                            };
                            code.DatabaseInsertSafe(conn,
                                "UPDATE [dbo].[Account_Receipt] SET " +
                                "Reservation_ID = @ReservationID, " +
                                "Created_Date = @CreatedDate, " +
                                "Total_Amount = @TotalAmount, " +
                                "Vat = @Vat, " +
                                "Total_Amount_Exclude_Vat = @TotalAmountExcludeVat, " +
                                "IsDeposit = @IsDeposit, " +
                                "Paid_Type = @PaidType, " +
                                "Etax = @Etax, " +
                                "Customer_ID = @CustomerID " +
                                "WHERE UID = @OriginalUID",
                                updateReceiptDataParams);
                            System.Diagnostics.Debug.WriteLine($"✅ Updated Account_Receipt data");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ Error updating Account_Receipt data: {ex.Message}");
                        }
                    }

                    // ✅ DELETE และ Re-INSERT Account_Receipt_Detail (เพราะอาจมีการเปลี่ยน items)
                    // SECURE: Delete with parameterized query
                    var deleteDetailParams = new Dictionary<string, object>
                    {
                        { "@ReceiptID", docNum }
                    };
                    code.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Account_Receipt_Detail] WHERE Receipt_ID = @ReceiptID",
                        deleteDetailParams);
                    System.Diagnostics.Debug.WriteLine($"✅ Deleted old Account_Receipt_Detail (Receipt_ID={docNum}) for re-insert");

                    // Store UID for re-use
                    Session["EditReceiptUID"] = originalUID;
                }
                else { }

                // Get UID: use original UID for edit, or create new one
                string receiptUID;
                if (command == "edit" && Session["EditReceiptUID"] != null)
                {
                    receiptUID = Session["EditReceiptUID"].ToString();
                }
                else if (!string.IsNullOrEmpty(uid))
                {
                    receiptUID = uid;
                }
                else
                {
                    receiptUID = Guid.NewGuid().ToString();
                }

                // ✅ INSERT Account_Receipt (เฉพาะ CREATE mode, ถ้า EDIT mode → ใช้ UPDATE ด้านบนแทน)
                if (command != "edit")
                {
                    System.Diagnostics.Debug.WriteLine($"[Receipt CREATE] Inserting Account_Receipt with ID={docNum}, UID={receiptUID}");

                    // SECURE: INSERT Account_Receipt with parameterized query
                    var receiptInsertParams = new Dictionary<string, object>
                    {
                        { "@ID", docNum },
                        { "@ReservationID", reservation_id > 0 ? reservation_id.ToString() : TextBox9.Text },
                        { "@CreatedDate", Convert.ToDateTime(TextBox8.Text).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                        { "@TotalAmount", TextBox6.Text },
                        { "@Vat", TextBox4.Text },
                        { "@TotalAmountExcludeVat", TextBox3.Text },
                        { "@IsDeposit", CheckBox1.Checked },
                        { "@PaidType", DropDownList2.SelectedItem.Text },
                        { "@CreatedByID", Session["UserID"].ToString() },
                        { "@Etax", CheckBox5.Checked },
                        { "@CustomerID", customerId },
                        { "@UID", receiptUID }
                    };
                    code.DatabaseInsertSafe(conn,
                        "INSERT INTO [dbo].[Account_Receipt] ([ID],[Reservation_ID],[Created_Date],[Total_Amount],[Vat],[Total_Amount_Exclude_Vat],[IsDeposit],[UseDeposit],[Paid_Type],[Status],[Created_By_ID],Etax,Customer_ID,UID) " +
                        "VALUES (@ID,@ReservationID,@CreatedDate,@TotalAmount,@Vat,@TotalAmountExcludeVat,@IsDeposit,'False',@PaidType,'Normal',@CreatedByID,@Etax,@CustomerID,@UID)",
                        receiptInsertParams);
                    System.Diagnostics.Debug.WriteLine($"[Receipt CREATE] Account_Receipt inserted successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Receipt EDIT] Skip INSERT Account_Receipt (already updated above)");
                }

                // ✅ INSERT Account_Receipt_Detail (ทั้ง CREATE และ EDIT mode - เพราะ DELETE ไปแล้วด้านบน)
                System.Diagnostics.Debug.WriteLine($"[Receipt] Inserting {dtDetail.Rows.Count} detail rows...");
                for (int i = 0; i < dtDetail.Rows.Count; i++)
                {
                    // SECURE: INSERT Receipt Detail with parameterized query
                    var detailInsertParams = new Dictionary<string, object>
                    {
                        { "@Number", dtDetail.Rows[i]["Number"].ToString() },
                        { "@ReceiptID", docNum },
                        { "@ProductTypeID", dtDetail.Rows[i]["ProductType_ID"].ToString() },
                        { "@ProductData", dtDetail.Rows[i]["Product_Data"].ToString() },
                        { "@ProductAmount", dtDetail.Rows[i]["Product_Amount"].ToString() },
                        { "@ProductUnit", dtDetail.Rows[i]["Product_Unit"].ToString() },
                        { "@PricePerPeice", dtDetail.Rows[i]["Price_PerPeice"].ToString() },
                        { "@PriceAmount", dtDetail.Rows[i]["Price_Amount"].ToString() }
                    };
                    code.DatabaseInsertSafe(conn,
                        "INSERT INTO [dbo].[Account_Receipt_Detail] ([Number],[Receipt_ID],[ProductType_ID],[Product_ID],[Product_Data],[Product_Amount],[Product_Unit],[Price_PerPeice],[Price_Amount]) " +
                        "VALUES (@Number,@ReceiptID,@ProductTypeID,0,@ProductData,@ProductAmount,@ProductUnit,@PricePerPeice,@PriceAmount)",
                        detailInsertParams);
                }
                System.Diagnostics.Debug.WriteLine($"[Receipt] Inserted {dtDetail.Rows.Count} detail rows successfully");

                // 🆕 Record/Update payment to Payment_History
                string actualReservationId = reservation_id > 0 ? reservation_id.ToString() : TextBox9.Text;
                if (!string.IsNullOrEmpty(actualReservationId) && actualReservationId != "0")
                {
                    try
                    {
                        string paymentType = CheckBox1.Checked ? "DEPOSIT" : "FULL";
                        string paymentMethod = DropDownList2.SelectedItem?.Text ?? "CASH";
                        string paymentNotes = "ออกใบกำกับภาษีแยก - " + docNum;
                        decimal totalAmount = Convert.ToDecimal(TextBox6.Text);

                        int? adminId = null;
                        if (Session["UserID"] != null)
                        {
                            adminId = Convert.ToInt32(Session["UserID"]);
                        }

                        // SECURE: Get customer phone from reservation with parameterized query
                        string customerPhone = "";
                        try
                        {
                            var phoneParams = new Dictionary<string, object>
                            {
                                { "@ReservationID", actualReservationId }
                            };
                            DataTable dtPhone = code.DatabaseQuerySafe(conn,
                                "SELECT Customer_MobilePhone FROM Reservation WHERE ID = @ReservationID",
                                phoneParams);
                            if (dtPhone.Rows.Count > 0)
                            {
                                customerPhone = dtPhone.Rows[0]["Customer_MobilePhone"].ToString();
                            }
                        }
                        catch { }

                        if (command == "edit")
                        {
                            // ✅ EDIT mode: UPDATE Payment_History (Receipt_ID ถูก UPDATE ไปแล้วด้านบน)
                            string updatePaymentQuery = @"
                                UPDATE [dbo].[Payment_History] SET
                                    Reservation_ID = @ReservationId,
                                    PaymentDate = @PaymentDate,
                                    PaymentAmount = @PaymentAmount,
                                    PaymentType = @PaymentType,
                                    PaymentMethod = @PaymentMethod,
                                    ProcessedBy_AdminID = @AdminId,
                                    PaidBy_CustomerPhone = @CustomerPhone,
                                    Notes = @Notes,
                                    UpdatedDate = GETDATE()
                                WHERE Receipt_ID = @ReceiptId";

                            var updateParams = new Dictionary<string, object>
                            {
                                { "@ReservationId", actualReservationId },
                                { "@PaymentDate", Convert.ToDateTime(TextBox8.Text) },
                                { "@PaymentAmount", totalAmount },
                                { "@PaymentType", paymentType },
                                { "@PaymentMethod", paymentMethod },
                                { "@ReceiptId", docNum },
                                { "@AdminId", adminId ?? (object)DBNull.Value },
                                { "@CustomerPhone", customerPhone },
                                { "@Notes", paymentNotes }
                            };

                            code2.DatabaseInsertSafe(conn, updatePaymentQuery, updateParams);
                            System.Diagnostics.Debug.WriteLine($"✅ Updated Payment_History for Receipt: {docNum}");
                        }
                        else
                        {
                            // ✅ CREATE mode: INSERT Payment_History
                            string insertPaymentQuery = @"
                                INSERT INTO [dbo].[Payment_History] (
                                    Reservation_ID,
                                    PaymentDate,
                                    PaymentAmount,
                                    PaymentType,
                                    PaymentMethod,
                                    Receipt_ID,
                                    ProcessedBy_AdminID,
                                    PaidBy_CustomerPhone,
                                    Status,
                                    Notes,
                                    CreatedDate,
                                    UpdatedDate
                                ) VALUES (
                                    @ReservationId,
                                    @PaymentDate,
                                    @PaymentAmount,
                                    @PaymentType,
                                    @PaymentMethod,
                                    @ReceiptId,
                                    @AdminId,
                                    @CustomerPhone,
                                    'COMPLETED',
                                    @Notes,
                                    GETDATE(),
                                    GETDATE()
                                )";

                            var insertParams = new Dictionary<string, object>
                            {
                                { "@ReservationId", actualReservationId },
                                { "@PaymentDate", Convert.ToDateTime(TextBox8.Text) },
                                { "@PaymentAmount", totalAmount },
                                { "@PaymentType", paymentType },
                                { "@PaymentMethod", paymentMethod },
                                { "@ReceiptId", docNum },
                                { "@AdminId", adminId ?? (object)DBNull.Value },
                                { "@CustomerPhone", customerPhone },
                                { "@Notes", paymentNotes }
                            };

                            code2.DatabaseInsertSafe(conn, insertPaymentQuery, insertParams);
                            System.Diagnostics.Debug.WriteLine($"✅ Inserted Payment_History for Receipt: {docNum}");
                        }
                    }
                    catch (Exception ex)
                    {
                        code2.Logs(conn, "Payment_History Upsert Error (Account/Receipt)",
                            ex.Message + " - " + ex.StackTrace, "SYSTEM");
                        System.Diagnostics.Debug.WriteLine($"❌ Error upserting Payment_History: {ex.Message}");
                        // Don't fail receipt creation if payment history fails
                    }
                }

                string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
                try
                {
                    System.IO.Directory.CreateDirectory(path + "\\" + Year);
                    System.IO.Directory.CreateDirectory(path + "\\" + Year + "\\" + Month);
                }
                catch (Exception ex)
                {

                }

                

                DataTable dtbusinessinfo = code.DatabaseQuery(conn, @"SELECT
                    Business_Info.ID AS BusinessInfo_ID, Business_Info.Business_Type_ID, Business_Info.Company_Name,
                    Business_Info.Address, Business_Info.Address_ID, Business_Info.Email, Business_Info.LegalEntity_Number,
                    Business_Info.Branch_Number, Business_Info.Phone_Number, Business_Info.Use_Vat,
                    Business_Info.Status, Business_Info.Address1,
                    Customer_Type.Customer_Type, Customer_Type.Customer_Code,
                    Address.Province, Address.District, Address.SubDistrict, Address.PostalCode, Address.Address_Code
                    FROM Business_Info
                    LEFT JOIN Customer_Type ON Business_Type_ID = Customer_Type.ID
                    LEFT JOIN Address ON Address.ID = Business_Info.Address_ID");
                

                // ✅ Query ด้วย UID แทน ID เพราะ UID ไม่เปลี่ยนแปลง (แม้จะแก้ไขเลขที่ใบเสร็จ)
                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"=== [Query Receipt for PDF] ===");
                System.Diagnostics.Debug.WriteLine($"receiptUID: {receiptUID}");
                System.Diagnostics.Debug.WriteLine($"docNum: {docNum}");

                // SECURE: Get receipt by UID with parameterized query
                var finalReceiptParams = new Dictionary<string, object>
                {
                    { "@UID", receiptUID }
                };
                dtReceipt = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Account_Receipt] LEFT JOIN Reservation ON Reservation.ID = Reservation_ID WHERE Account_Receipt.UID = @UID",
                    finalReceiptParams);

                // ✅ Validate query result
                if (dtReceipt == null || dtReceipt.Rows.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ ERROR: Receipt not found for UID: {receiptUID}");
                    ClientScript.RegisterStartupScript(this.GetType(), "receiptQueryError",
                        "alert('❌ เกิดข้อผิดพลาด: ไม่พบข้อมูลใบเสร็จหลังบันทึก\\n\\nกรุณาติดต่อผู้ดูแลระบบ');", true);
                    return;
                }

                // ✅ Ensure uid variable matches receiptUID (used for PDF filename later)
                uid = receiptUID;

                // ✅ ใช้ docNum (เลขที่ที่ต้องการแสดงใน PDF) แทน actualReceiptID
                // เพราะ docNum คือเลขที่ที่เรา INSERT Receipt_Detail ไว้
                string actualReceiptID = dtReceipt.Rows[0]["ID"].ToString();
                System.Diagnostics.Debug.WriteLine($"actualReceiptID from DB: {actualReceiptID}");

                // ✅ ตรวจสอบว่า actualReceiptID ตรงกับ docNum หรือไม่
                if (actualReceiptID != docNum)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ WARNING: Mismatch! actualReceiptID ({actualReceiptID}) != docNum ({docNum})");
                    System.Diagnostics.Debug.WriteLine($"⚠️ Using docNum for Receipt_Detail query to match INSERT");
                }

                // ✅ ใช้ docNum แทน actualReceiptID เพื่อให้ตรงกับ Receipt_Detail ที่เรา INSERT
                // SECURE: Get receipt details with parameterized query
                var receiptDetailParams = new Dictionary<string, object>
                {
                    { "@ReceiptID", docNum }
                };
                DataTable dtReceiptDetail = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Account_Receipt_Detail] ard LEFT JOIN Account_ProductType apt ON apt.ID = ard.ProductType_ID WHERE ard.Receipt_ID = @ReceiptID ORDER BY ard.Number ASC",
                    receiptDetailParams);

                System.Diagnostics.Debug.WriteLine($"dtReceiptDetail.Rows.Count: {dtReceiptDetail.Rows.Count}");
                System.Diagnostics.Debug.WriteLine($"================================");


               
                //GridView1.DataSource = dt;
                //GridView1.DataBind();
               
                // Use SignatureService for centralized signature management
                SignatureService signatureService = new SignatureService();
                short creatorAdminId = 0;
                short.TryParse(Session["UserID"]?.ToString() ?? "0", out creatorAdminId);

                DataTable dtSignature = signatureService.GetSignatureDataWithCEO(creatorAdminId);

                DataTable dtCustomerReport = new DataTable();
                dtCustomerReport = dtcustomer.Copy();
                DataTable dtBusinessinfoReport = new DataTable();
                dtBusinessinfoReport = dtbusinessinfo.Copy();

                try
                {
                    

                    if (CheckBox3.Checked == true)
                    {
                        dtCustomerReport.Rows[0]["FullName"] = "ประสงค์ไม่รับใบกำกับภาษี";
                        dtCustomerReport.Rows[0]["Address"] = "";
                        dtCustomerReport.Rows[0]["IDNumber"] = "";
                        dtCustomerReport.Rows[0]["MobilePhone"] = "";
                        dtCustomerReport.Rows[0]["Email"] = "";
                    }
                    else
                    {

                        dtcustomer.Rows[0]["PostalCode"] = TextBox16.Text;
                        if (DropDownList8.SelectedIndex == 0 && TextBox7.Text == "00000")
                        {
                            dtCustomerReport.Rows[0]["FullName"] = TextBox10.Text;
                        }
                        else if(DropDownList8.SelectedIndex == 0 && Convert.ToInt32(TextBox7.Text) > 0)
                        {
                            dtCustomerReport.Rows[0]["FullName"] = TextBox10.Text + " สาขาที่ "+TextBox7.Text ;
                        }
                        else
                        {
                            dtCustomerReport.Rows[0]["FullName"] = TextBox10.Text;
                        }
                        try
                        {
                            // Check if Address_ID exists - if not, use Address field as-is (complete address already stored)
                            if (dtcustomer.Rows[0]["Address_ID"] == DBNull.Value || string.IsNullOrEmpty(dtcustomer.Rows[0]["Address_ID"].ToString()))
                            {
                                // No Address_ID - use complete address from Address field without adding prefixes
                                dtCustomerReport.Rows[0]["Address"] = dtcustomer.Rows[0]["Address"].ToString();
                            }
                            else
                            {
                                // Has Address_ID - build address from form inputs with appropriate prefixes
                                if (DropDownList5.SelectedValue.Contains("กรุงเทพ"))
                                {
                                    dtCustomerReport.Rows[0]["Address"] = TextBox11.Text + " " + TextBox18.Text + " แขวง " + DropDownList7.SelectedValue + " เขต " + DropDownList6.SelectedValue + " " + DropDownList5.SelectedValue + " " + dtcustomer.Rows[0]["PostalCode"].ToString();
                                }
                                else
                                {
                                    dtCustomerReport.Rows[0]["Address"] = TextBox11.Text + " " + TextBox18.Text + " ต." + DropDownList7.SelectedValue + " อ." + DropDownList6.SelectedValue + " จ." + DropDownList5.SelectedValue + " " + dtcustomer.Rows[0]["PostalCode"].ToString();
                                }
                            }
                        }
                        catch
                        {
                            dtCustomerReport.Rows[0]["Address"] = dtcustomer.Rows[0]["Address"].ToString();
                        }

                        dtCustomerReport.Rows[0]["IDNumber"] = TextBox12.Text;
                        dtCustomerReport.Rows[0]["MobilePhone"] = TextBox13.Text;
                        dtCustomerReport.Rows[0]["Email"] = TextBox17.Text;
                    }


                    try
                    {
                        // Check if Address_ID exists - if not, use Address field as-is (complete address already stored)
                        if (dtbusinessinfo.Rows[0]["Address_ID"] == DBNull.Value || string.IsNullOrEmpty(dtbusinessinfo.Rows[0]["Address_ID"].ToString()))
                        {
                            // No Address_ID - use complete address from Address field without adding prefixes
                            dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString();
                        }
                        else
                        {
                            // Has Address_ID - build address from structured fields with appropriate prefixes
                            if (dtbusinessinfo.Rows[0]["Province"].ToString().Contains("กรุงเทพ"))
                            {
                                dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString() + " " + dtbusinessinfo.Rows[0]["Address1"].ToString() + " แขวง " + dtbusinessinfo.Rows[0]["SubDistrict"].ToString() + " เขต " + dtbusinessinfo.Rows[0]["District"].ToString() + " " + dtbusinessinfo.Rows[0]["Province"].ToString() + " " + dtbusinessinfo.Rows[0]["PostalCode"].ToString();
                            }
                            else
                            {
                                dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString() + " " + dtbusinessinfo.Rows[0]["Address1"].ToString() + " ต." + dtbusinessinfo.Rows[0]["SubDistrict"].ToString() + " อ." + dtbusinessinfo.Rows[0]["District"].ToString() + " จ." + dtbusinessinfo.Rows[0]["Province"].ToString() + " " + dtbusinessinfo.Rows[0]["PostalCode"].ToString();
                            }
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

                        // ✅ PDF filename uses docNum (ใช้เลขที่ที่กรอกถ้า CheckBox2 checked)
                        string pdfFileName = docNum + "_" + uid + ".pdf";
                        System.Diagnostics.Debug.WriteLine($"[Receipt] Creating PDF: {pdfFileName}");

                        if (File.Exists(path + "\\" + Year + "\\" + Month + "\\" + docNum + "_"+uid+".pdf"))
                        {
                            File.Delete(path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + ".pdf");
                        }
                        if (File.Exists(path + "\\" + Year + "\\" + Month + "\\" + docNum +  ".pdf"))
                        {
                            File.Delete(path + "\\" + Year + "\\" + Month + "\\" + docNum + ".pdf");
                        }
                        using (FileStream fs = new FileStream(path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + ".pdf", FileMode.Append))
                        { 
                            fs.Write(bytes, 0, bytes.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                    }

                    if (CheckBox5.Checked == true)
                    {
                        try
                        {
                            string xmlFilePath = path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + ".xml";
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


                            xmlString = xmlString.Replace("*buyer_DefinedCITradeContact", TextBox17.Text);
                            xmlString = xmlString.Replace("*buyer_zipcode", dtcustomer.Rows[0]["PostalCode"].ToString());
                            xmlString = xmlString.Replace("*buyer_address", dtcustomer.Rows[0]["Address"].ToString() +" "+ dtcustomer.Rows[0]["Address1"].ToString()+ " "+ dtcustomer.Rows[0]["SubDistrict"].ToString() + " " + dtcustomer.Rows[0]["District"].ToString()+ " " + dtcustomer.Rows[0]["Province"].ToString()+ " " + dtcustomer.Rows[0]["PostalCode"].ToString());
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
                            xmlString = xmlString.Replace("*invoice_tax_rate", "7");
                            xmlString = xmlString.Replace("*invoice_basis_amount", dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"].ToString());
                            xmlString = xmlString.Replace("*calculated_amount", dtReceipt.Rows[0]["Vat"].ToString());
                            xmlString = xmlString.Replace("*invoice_discountallowance", "");
                            xmlString = xmlString.Replace("*invoice_serviceallowance", "");
                            xmlString = xmlString.Replace("*invoice_line_total", dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"].ToString());
                            xmlString = xmlString.Replace("*tax_basis_total_amount", dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"].ToString());
                            xmlString = xmlString.Replace("*invoice_tax_total", dtReceipt.Rows[0]["Vat"].ToString());
                            xmlString = xmlString.Replace("*invoice_grand_total", dtReceipt.Rows[0]["Total_Amount"].ToString());
                            xmlString = xmlString.Replace("*item", "ค่าที่พักหรือค่ามัดจำที่พัก");
                            xmlString = xmlString.Replace("*invoice_billedquantity", "1");

                            System.IO.File.WriteAllText(xmlFilePath, xmlString, System.Text.Encoding.UTF8);
                        }
                        catch { }

                        try
                        {
                            {


                                PDFA3Invoice pdf = new PDFA3Invoice();
                                string pdfFilePath = path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + ".pdf";
                                string xmlFilePath = path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + ".xml";

                                string xmlFileName = "ETDA-invoice.xml";


                                string xmlVersion = "1.0";
                                string documentID = docNum;
                                string documentOID = "";

                                string outputPath = path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + "_etax.pdf";

                                pdf.CreatePDFA3Invoice(pdfFilePath, xmlFilePath, xmlFileName, xmlVersion, documentID, documentOID, outputPath, "Tax Invoice");

                                if (CheckBox5.Checked == true)
                                {

                                    DateTime docDate = DateTime.Now;

                                    try
                                    {
                                        if (Convert.ToDateTime(TextBox8.Text) < docDate)
                                        {
                                            docDate = Convert.ToDateTime(TextBox8.Text);
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
                                    Attachment data = new Attachment(pdf2, dtReceipt.Rows[0]["ID"].ToString() + "_" + uid + "_etax.pdf");
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

                                    NumberHelper.SendEmail(ConfigurationSettings.AppSettings["SMTP"].ToString(), Convert.ToInt32(ConfigurationSettings.AppSettings["SMTP_Port"].ToString()), Convert.ToBoolean(ConfigurationSettings.AppSettings["SMTP_EnableSsl"].ToString()), Convert.ToBoolean(ConfigurationSettings.AppSettings["SMTP_UseDefaultCredentials"].ToString()), ConfigurationSettings.AppSettings["Email_From"].ToString(), ConfigurationSettings.AppSettings["Email_Password_From"].ToString(), TextBox17.Text, ConfigurationSettings.AppSettings["Email_CC"].ToString(), subject, body, dataall);


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


                // Show success message then redirect
                ClientScript.RegisterStartupScript(this.GetType(), "success",
                    "alert('✅ บันทึกใบเสร็จรับเงินเรียบร้อยแล้ว'); window.location.href='/Account/Receipt';", true);
            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาระบุข้อมูลให้ครบถ้วน');", true);
            }

        }

        // ✨ MIGRATED: cleantext() has been replaced with ValidationHelper.CleanText()
        // See ValidationHelper class in Take_Time_BangPhra.Class namespace

        protected void Button4_Click(object sender, EventArgs e)
        {
            
          
        }

        protected void GridView2_RowDeleted(object sender, GridViewDeletedEventArgs e)
        {
        }

        protected void GridView2_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
          
        }

        protected void TextBox9_TextChanged(object sender, EventArgs e)
        {
            if(TextBox9.Text.Length > 0)
            {
                // SECURE: Get customer from reservation with parameterized query
                var reservationParams = new Dictionary<string, object>
                {
                    { "@ReservationID", TextBox9.Text }
                };
                DataTable dtCustomer = code.DatabaseQuerySafe(conn, @"SELECT
                    Customer.ID, Customer.MobilePhone, Customer.Name, Customer.NickName, Customer.ComeFrom,
                    Customer.Remark, Customer.Status, Customer.FullName, Customer.Address, Customer.Address1,
                    Customer.Address_ID, Customer.IDNumber, Customer.Email, Customer.Customer_Type_ID,
                    Customer.Branch_Number, Customer.TaxID,
                    Customer_Type.Customer_Type, Customer_Type.Customer_Code,
                    Address.Province, Address.District, Address.SubDistrict, Address.PostalCode, Address.Address_Code
                    FROM [Reservation]
                    INNER JOIN Customer ON Customer.MobilePhone = Reservation.Customer_MobilePhone
                    LEFT JOIN Customer_Type ON Customer.Customer_Type_ID = Customer_Type.ID
                    LEFT JOIN Address ON Address.ID = Customer.Address_ID
                    WHERE Reservation.ID = @ReservationID",
                    reservationParams);

                if(dtCustomer.Rows.Count > 0)
                {
                    // ✅ Populate customer info
                    TextBox10.Text = dtCustomer.Rows[0]["FullName"].ToString();
                    TextBox11.Text = dtCustomer.Rows[0]["Address"].ToString();
                    TextBox12.Text = dtCustomer.Rows[0]["IDNumber"].ToString();
                    TextBox13.Text = dtCustomer.Rows[0]["MobilePhone"].ToString();
                    TextBox17.Text = dtCustomer.Rows[0]["Email"].ToString();
                    TextBox18.Text = dtCustomer.Rows[0]["Address1"].ToString();

                    // ✅ Populate address dropdowns and postal code
                    try
                    {
                        if (!string.IsNullOrEmpty(dtCustomer.Rows[0]["PostalCode"].ToString()))
                        {
                            TextBox16.Text = dtCustomer.Rows[0]["PostalCode"].ToString();

                            // Populate dropdowns
                            DropDownList5.ClearSelection();
                            if (DropDownList5.Items.FindByText(dtCustomer.Rows[0]["Province"].ToString()) != null)
                            {
                                DropDownList5.Items.FindByText(dtCustomer.Rows[0]["Province"].ToString()).Selected = true;
                            }

                            DropDownList6.ClearSelection();
                            if (DropDownList6.Items.FindByText(dtCustomer.Rows[0]["District"].ToString()) != null)
                            {
                                DropDownList6.Items.FindByText(dtCustomer.Rows[0]["District"].ToString()).Selected = true;
                            }

                            DropDownList7.ClearSelection();
                            if (DropDownList7.Items.FindByText(dtCustomer.Rows[0]["SubDistrict"].ToString()) != null)
                            {
                                DropDownList7.Items.FindByText(dtCustomer.Rows[0]["SubDistrict"].ToString()).Selected = true;
                            }
                        }
                    }
                    catch { }

                    // ✅ Populate customer type dropdown
                    try
                    {
                        if (!string.IsNullOrEmpty(dtCustomer.Rows[0]["Customer_Type_ID"].ToString()))
                        {
                            DropDownList8.ClearSelection();
                            if (DropDownList8.Items.FindByValue(dtCustomer.Rows[0]["Customer_Type_ID"].ToString()) != null)
                            {
                                DropDownList8.Items.FindByValue(dtCustomer.Rows[0]["Customer_Type_ID"].ToString()).Selected = true;
                            }
                        }
                    }
                    catch { }
                }
            }

        }

        protected void CheckBox2_CheckedChanged(object sender, EventArgs e)
        {
            if(CheckBox2.Checked == true)
            {
                // ✅ ใช้ ReadOnly แทน Enabled เพราะ ReadOnly TextBox ยังส่งค่ากลับมาใน postback
                TextBox5.ReadOnly = false;
                TextBox5.BackColor = System.Drawing.Color.White;
                System.Diagnostics.Debug.WriteLine($"[CheckBox2_CheckedChanged] Set TextBox5.ReadOnly=false (editable)");
            }
            else
            {
                TextBox5.ReadOnly = true;
                TextBox5.BackColor = System.Drawing.Color.LightGray;
                System.Diagnostics.Debug.WriteLine($"[CheckBox2_CheckedChanged] Set TextBox5.ReadOnly=true (readonly)");
            }
        }

        /// <summary>
        /// 🔧 FIX: เมื่อเปลี่ยนวันที่ใบกำกับภาษี ให้รันเลขใบกำกับใหม่จากวันนั้น
        /// เพื่อให้เลขใบกำกับภาษีเรียงต่อจากใบกำกับภาษีในวันที่เลือก
        /// </summary>
        protected void TextBox8_TextChanged(object sender, EventArgs e)
        {
            try
            {
                // ดึง command จาก ViewState
                string command = ViewState["EditCommand"]?.ToString() ?? "";

                // ถ้าไม่ใช่ mode edit หรือ CheckBox2 ไม่ได้ check → ไม่รันเลขใหม่
                // เพราะถ้า user ไม่ได้ติ๊ก edit แสดงว่าไม่ต้องการเปลี่ยนเลข
                if (command != "edit" && !CheckBox2.Checked)
                {
                    // สร้างเลขใหม่สำหรับ create mode
                    DateTime newDate = Convert.ToDateTime(TextBox8.Text);
                    string newDocNum = _documentHelper.CreateDocumentNumber("Account_Receipt", "REC", newDate);
                    TextBox5.Text = newDocNum;
                    System.Diagnostics.Debug.WriteLine($"[TextBox8_TextChanged] CREATE mode - Generated new doc number: {newDocNum}");
                    return;
                }

                // ถ้าเป็น edit mode และ CheckBox2 checked (user ต้องการแก้เลข)
                // หรือถ้า user ติ๊ก CheckBox2 → รันเลขใบกำกับใหม่จากวันที่เลือก
                if (CheckBox2.Checked)
                {
                    DateTime newDate = Convert.ToDateTime(TextBox8.Text);
                    string newDocNum = _documentHelper.CreateDocumentNumber("Account_Receipt", "REC", newDate);

                    TextBox5.Text = newDocNum;
                    TextBox5.ReadOnly = false;
                    TextBox5.BackColor = System.Drawing.Color.White;

                    System.Diagnostics.Debug.WriteLine($"[TextBox8_TextChanged] Generated new doc number for date {newDate:yyyy-MM-dd}: {newDocNum}");

                    // แจ้งเตือน user ว่าเลขใบกำกับภาษีถูกเปลี่ยน
                    ClientScript.RegisterStartupScript(this.GetType(), "dateChanged",
                        $"alert('🔄 เปลี่ยนวันที่เป็น {newDate:dd/MM/yyyy}\\n\\nเลขใบกำกับภาษีใหม่: {newDocNum}\\n\\nคุณสามารถแก้ไขเลขที่ได้ตามต้องการ');", true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TextBox8_TextChanged] Error: {ex.Message}");
            }
        }

        protected void CheckBox3_CheckedChanged(object sender, EventArgs e)
        {
            if(CheckBox3.Checked == true)
            {
                
               // TextBox10.Text = "ประสงค์ไม่รับใบกำกับภาษี";
               // TextBox11.Text = "";
               // TextBox12.Text = "";
               // TextBox13.Text = "";
            }
        }

        protected void CheckBox4_CheckedChanged(object sender, EventArgs e)
        {
            if(CheckBox4.Checked == true)
            {
                Button4.Enabled = true;
            }
            else
            {
                Button4.Enabled = false;
            }
        }

        protected void Button4_Click1(object sender, EventArgs e)
        {
            string uid = Request.QueryString["uid"];
            string path = ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
            string Imagespath = ConfigurationSettings.AppSettings["ImagesFolderPath"].ToString();

            // SECURE: Use parameterized query
            var receiptParams = new Dictionary<string, object>
            {
                { "@UID", uid }
            };
            DataTable dtRec = code.DatabaseQuerySafe(conn,
                "SELECT * FROM Account_Receipt WHERE Status = 'Normal' AND UID = @UID",
                receiptParams);

            string id = dtRec.Rows[0]["ID"].ToString();
            for (int i = 0; i < dtRec.Rows.Count; i++)
            {
                // Delete related records before cancelling (to maintain data integrity)
                string docNum = dtRec.Rows[i]["ID"].ToString();

                // Delete Payment_History that references Payment_Slips (SECURE: FK: PaymentSlip_ID)
                var deleteParams1 = new Dictionary<string, object> { { "@DocNum", docNum } };
                code.DatabaseInsertSafe(conn,
                    "DELETE FROM [dbo].[Payment_History] " +
                    "WHERE PaymentSlip_ID IN (SELECT ID FROM [dbo].[Payment_Slips] WHERE Account_Receipt_ID = @DocNum)",
                    deleteParams1);

                // Delete Payment_History by Receipt_ID (SECURE: for records not linked via PaymentSlip_ID)
                var deleteParams2 = new Dictionary<string, object> { { "@DocNum", docNum } };
                code.DatabaseInsertSafe(conn,
                    "DELETE FROM [dbo].[Payment_History] WHERE Receipt_ID = @DocNum",
                    deleteParams2);

                // Delete Payment_Slips (SECURE: FK to Account_Receipt)
                var deleteParams3 = new Dictionary<string, object> { { "@DocNum", docNum } };
                code.DatabaseInsertSafe(conn,
                    "DELETE FROM [dbo].[Payment_Slips] WHERE Account_Receipt_ID = @DocNum",
                    deleteParams3);

                // Update status to Cancel (SECURE)
                var updateParams = new Dictionary<string, object> { { "@ID", id } };
                code.DatabaseInsertSafe(conn,
                    "UPDATE [dbo].[Account_Receipt] SET [Status] = 'Cancel' WHERE ID = @ID",
                    updateParams);

                DateTime createdDate = Convert.ToDateTime(dtRec.Rows[i]["Created_Date"].ToString());

                string inputPdfStreampath = "";
                string outputPdfStreampath = "";
                if(File.Exists(path + "\\" + createdDate.Year.ToString() + "\\" + createdDate.Month + "\\" + dtRec.Rows[i]["ID"].ToString() + "_"+uid+".pdf"))
                {
                    inputPdfStreampath = path + "\\" + createdDate.Year.ToString() + "\\" + createdDate.Month + "\\" + dtRec.Rows[i]["ID"].ToString() + "_" + uid + ".pdf";
                }
                else
                {
                    inputPdfStreampath = path + "\\" + createdDate.Year.ToString() + "\\" + createdDate.Month + "\\" + dtRec.Rows[i]["ID"].ToString() + ".pdf";
                }

                if (File.Exists(path + "\\" + createdDate.Year.ToString() + "\\" + createdDate.Month + "\\" + dtRec.Rows[i]["ID"].ToString() +"_"+uid+ "_Cancel.pdf"))
                {
                    outputPdfStreampath = path + "\\" + createdDate.Year.ToString() + "\\" + createdDate.Month + "\\" + dtRec.Rows[i]["ID"].ToString() + "_" + uid + "_Cancel.pdf";
                }
                else
                {
                    outputPdfStreampath = path + "\\" + createdDate.Year.ToString() + "\\" + createdDate.Month + "\\" + dtRec.Rows[i]["ID"].ToString() + "_Cancel.pdf";
                }
                using (Stream inputPdfStream = new FileStream(inputPdfStreampath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (Stream inputImageStream = new FileStream(Imagespath + "\\Cancel.png", FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (Stream outputPdfStream = new FileStream(outputPdfStreampath, FileMode.Append, FileAccess.Write, FileShare.None))
                    {
                        var reader = new PdfReader(inputPdfStream);
                        var stamper = new PdfStamper(reader, outputPdfStream);
                        var pdfContentByte = stamper.GetOverContent(1);

                        iTextSharp.text.Image image = iTextSharp.text.Image.GetInstance(inputImageStream);
                        image.SetAbsolutePosition(100, 100);
                        pdfContentByte.AddImage(image);
                        stamper.Close();
                    }
                File.Delete(inputPdfStreampath);
                try
                {
                    File.Delete(inputPdfStreampath + "_etax.pdf");
                }
                catch
                {

                }

                // Show success message then redirect
                ClientScript.RegisterStartupScript(this.GetType(), "success",
                    "alert('✅ เพิ่มลายเซ็นอิเล็กทรอนิกส์เรียบร้อยแล้ว'); window.location.href='/Account/Receipt';", true);
            }
        }

        protected void Button5_Click(object sender, EventArgs e)
        {
            TextBox16.Enabled = false;

            // SECURE: Get address with parameterized query
            var addressParams = new Dictionary<string, object>
            {
                { "@PostalCode", TextBox16.Text ?? "" }
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

        // ✨ Helper method to populate address dropdowns (LEGACY - kept for backward compatibility)
        public void getAddress(string commp, string commd, string commsd)
        {
            string Command = Request.QueryString["Command"];
            string ID = Request.QueryString["ID"];
            DataTable dtProvince = code.DatabaseQuery(conn, commp);
            DataTable dtDistrict = code.DatabaseQuery(conn, commd);
            DataTable dtSubDistrict = code.DatabaseQuery(conn, commsd);
            populateAddressDropdowns(dtProvince, dtDistrict, dtSubDistrict);
        }

        // ✨ SECURE: Helper method to populate address dropdowns
        private void populateAddressDropdowns(DataTable dtProvince, DataTable dtDistrict, DataTable dtSubDistrict)
        {
            string Command = Request.QueryString["Command"];
            try
            {
                if (dtProvince.Rows.Count <= 0)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ไม่พบหมายเลขไปรษณีย์ที่คุณระบุ');", true);
                    TextBox16.Enabled = true;
                }
                else
                {
                    if (Button2.Enabled == true || Command == "View" || Command == "Edit")
                    {
                        List<string> ddl = new List<string>();

                        for (int i = 0; i < dtProvince.Rows.Count; i++)
                        {
                            ddl.Add(dtProvince.Rows[i][0].ToString());
                        }
                        DropDownList5.DataSource = ddl;
                        DropDownList5.DataBind();

                        ddl.Clear();

                        for (int i = 0; i < dtDistrict.Rows.Count; i++)
                        {
                            ddl.Add(dtDistrict.Rows[i][0].ToString());
                        }
                        DropDownList6.DataSource = ddl;
                        DropDownList6.DataBind();

                        ddl.Clear();

                        for (int i = 0; i < dtSubDistrict.Rows.Count; i++)
                        {
                            ddl.Add(dtSubDistrict.Rows[i][0].ToString());
                        }
                        DropDownList7.DataSource = ddl;
                        DropDownList7.DataBind();
                    }
                }
            }
            catch { }
        }

        protected void TextBox16_TextChanged(object sender, EventArgs e)
        {
            Button5_Click(null, null);
        }

        protected void Button6_Click(object sender, EventArgs e)
        {
            TextBox16.Enabled = true;
            TextBox16.Text = string.Empty;
            DropDownList5.Items.Clear();
            DropDownList6.Items.Clear();
            DropDownList7.Items.Clear();
        }

        // ✨ MIGRATED: CheckAddressID() has been replaced with AddressHelper.GetAddressIdString()
        // See AddressHelper class in Take_Time_BangPhra.Class namespace

        protected void DropDownList8_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (DropDownList8.SelectedValue == "1")
            {
                TextBox7.Visible = true;
            }
            else
            {
                TextBox7.Visible = false;
            }
        }

        protected void DropDownList5_SelectedIndexChanged(object sender, EventArgs e)
        {
            DataTable dtProvince, dtDistrict, dtSubDistrict;

            if (TextBox16.Enabled == false)
            {
                // SECURE: Filter by PostalCode and Province with parameterized query
                var addressParams = new Dictionary<string, object>
                {
                    { "@PostalCode", TextBox16.Text ?? "" },
                    { "@Province", DropDownList5.SelectedValue ?? "" }
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
                    { "@Province", DropDownList5.SelectedValue ?? "" }
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

        protected void DropDownList6_SelectedIndexChanged(object sender, EventArgs e)
        {
            DataTable dtProvince, dtDistrict, dtSubDistrict;

            if (TextBox16.Enabled == false)
            {
                // SECURE: Filter by PostalCode and District with parameterized query
                var addressParams = new Dictionary<string, object>
                {
                    { "@PostalCode", TextBox16.Text ?? "" },
                    { "@District", DropDownList6.SelectedValue ?? "" }
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
                    { "@District", DropDownList6.SelectedValue ?? "" }
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

        protected void CheckBox5_CheckedChanged(object sender, EventArgs e)
        {
            if (CheckBox5.Checked == true)
            {
               if(TextBox17.Text.Length <= 0)
                {
                    CheckBox5.Checked = false;
                }
               // TextBox10.Text = "ประสงค์ไม่รับใบกำกับภาษี";
               // TextBox11.Text = "";
               // TextBox12.Text = "";
               // TextBox13.Text = "";
            }
            else
            {

            }
        }

        /// <summary>
        /// Handle checkbox change for editing amount fields (TextBox3, TextBox4, TextBox6)
        /// </summary>
        protected void ChkEditAmount_CheckedChanged(object sender, EventArgs e)
        {
            if (ChkEditAmount.Checked)
            {
                // Enable editing for amount fields
                TextBox3.Enabled = true;
                TextBox4.Enabled = true;
                TextBox6.Enabled = true;
            }
            else
            {
                // Disable editing for amount fields
                TextBox3.Enabled = false;
                TextBox4.Enabled = false;
                TextBox6.Enabled = false;
            }
        }
    }
}