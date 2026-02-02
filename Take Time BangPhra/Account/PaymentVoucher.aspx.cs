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
using iTextSharp.text.pdf.parser;
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.Account.Report
{
    public partial class PaymentVoucher : System.Web.UI.Page
    {

        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        DocumentHelper documentHelper;
        AssetService assetService;

        protected void Page_Load(object sender, EventArgs e)
        {
            this.MaintainScrollPositionOnPostBack = true;
            documentHelper = new DocumentHelper(conn);
            assetService = new AssetService();
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
                DataTable dtUpload = new DataTable();
                try
                {
                    dtUpload.Columns.Add("Name");
                }
                catch
                {

                }
                Session["dtUpload"] = dtUpload;

                DataTable dtPaidHow = code.DatabaseQuery(SqlDataSource2.ConnectionString, SqlDataSource2.SelectCommand);
                for (int i = 0; i < dtPaidHow.Rows.Count; i++)
                {
                    DropDownList2.Items.Add(new ListItem( dtPaidHow.Rows[i]["Paid_How"].ToString(), dtPaidHow.Rows[i]["ID"].ToString()));
                }
                DropDownList2.DataBind();

                DataTable dtPaidType = code.DatabaseQuery(SqlDataSource3.ConnectionString, SqlDataSource3.SelectCommand);
                for (int i = 0; i < dtPaidType.Rows.Count; i++)
                {
                    DropDownList3.Items.Add(new ListItem( dtPaidType.Rows[i]["Paid_Type"].ToString() , dtPaidType.Rows[i]["ID"].ToString()));
                }
                DropDownList3.DataBind();

                DataTable dtVatType = code.DatabaseQuery(SqlDataSource4.ConnectionString, SqlDataSource4.SelectCommand);
                for (int i = 0; i < dtVatType.Rows.Count; i++)
                {
                    DropDownList4.Items.Add(new ListItem( dtVatType.Rows[i]["Vat_Type"].ToString() , dtVatType.Rows[i]["ID"].ToString()));
                }
                DropDownList4.DataBind();

                DataTable dtVendor_Group = code.DatabaseQuery(SqlDataSource5.ConnectionString, SqlDataSource5.SelectCommand);
                for (int i = 0; i < dtVendor_Group.Rows.Count; i++)
                {
                    DropDownList5.Items.Add(new ListItem( dtVendor_Group.Rows[i]["Vendor_Group"].ToString() , dtVendor_Group.Rows[i]["Vendor_Group"].ToString()));
                }
                DropDownList5.DataBind();

                DataTable dtVendor = code.DatabaseQuery(SqlDataSource1.ConnectionString, SqlDataSource1.SelectCommand);
                for (int i = 0; i < dtVendor.Rows.Count; i++)
                {
                    DropDownList1.Items.Add(new ListItem( dtVendor.Rows[i]["Name"].ToString() , dtVendor.Rows[i]["ID"].ToString()));
                }
                DropDownList1.DataBind();

                // Load Asset Categories
                LoadAssetCategories();

                string command = Request.QueryString["command"];
                string uid = Request.QueryString["uid"];
                if (command == "edit")
                {
                    // SECURE: Get payment details with parameterized query
                    var paymentParams = new Dictionary<string, object>
                    {
                        { "@UID", uid ?? "" }
                    };
                    DataTable dtPayment = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Account_Payment WHERE UID = @UID",
                        paymentParams);
                    string id = dtPayment.Rows[0]["ID"].ToString();

                    // ✅ เก็บ id และ uid ไว้ใน ViewState เพื่อใช้ใน GetFileUrl
                    ViewState["PaymentID"] = id;
                    ViewState["PaymentUID"] = uid;

                    // SECURE: Get payment details with parameterized query
                    var detailParams = new Dictionary<string, object>
                    {
                        { "@PaymentID", id }
                    };
                    DataTable dtPaymentDetail = code.DatabaseQuerySafe(conn,
                        "SELECT Number,Detail,Amount FROM Account_Payment_Detail WHERE Payment_ID = @PaymentID",
                        detailParams);

                    // SECURE: Get vendor details with parameterized query
                    var vendorParams = new Dictionary<string, object>
                    {
                        { "@VendorID", dtPayment.Rows[0]["Vendor_ID"].ToString() }
                    };
                    DataTable dtVendorSelected = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Vendor WHERE ID = @VendorID",
                        vendorParams);

                    GridView1.DataSource = dtPaymentDetail;
                    GridView1.DataBind();

                    DateTime docdate = Convert.ToDateTime(dtPayment.Rows[0]["Created_Date"].ToString());
                    TextBox8.Text = docdate.ToString("yyyy-MM-dd");


                    TextBox3.Text = dtPayment.Rows[0]["Total_Amount_Exclude_Vat"].ToString();
                    TextBox4.Text = dtPayment.Rows[0]["Vat"].ToString();
                    TextBox6.Text = dtPayment.Rows[0]["Total_Amount"].ToString();

                    DropDownList2.SelectedIndex = DropDownList2.Items.IndexOf(DropDownList2.Items.FindByText(dtPayment.Rows[0]["Paid_How"].ToString()));
                    DropDownList2.DataBind();
                    DropDownList3.SelectedIndex = DropDownList3.Items.IndexOf(DropDownList3.Items.FindByText(dtPayment.Rows[0]["Paid_Type"].ToString()));
                    DropDownList3.DataBind();
                    DropDownList4.SelectedIndex = DropDownList4.Items.IndexOf(DropDownList4.Items.FindByValue(dtPayment.Rows[0]["Vat_Type_ID"].ToString()));
                    DropDownList4.DataBind();

                    DropDownList5.SelectedIndex = DropDownList5.Items.IndexOf(DropDownList5.Items.FindByText(dtVendorSelected.Rows[0]["Vendor_Group"].ToString()));
                    DropDownList5.DataBind();
                    DropDownList1.SelectedIndex = DropDownList1.Items.IndexOf(DropDownList1.Items.FindByValue(dtVendorSelected.Rows[0]["ID"].ToString()));
                    DropDownList1.DataBind();

                    string path = System.Configuration.ConfigurationSettings.AppSettings["PaymentFolderPath"].ToString();
                    string paymentPath = path + "\\" + docdate.Year + "\\" + docdate.Month;
                    // Fallback: check padded month directory for files created with zero-padded month
                    if (!Directory.Exists(paymentPath))
                    {
                        string altPath = path + "\\" + docdate.Year + "\\" + docdate.Month.ToString().PadLeft(2, '0');
                        if (Directory.Exists(altPath))
                            paymentPath = altPath;
                    }
                    if (Directory.Exists(paymentPath))
                    {
                        string[] dirs = Directory.GetFiles(paymentPath, id + "_" + uid + "*");
                        foreach (string file in dirs)
                        {
                            if(System.IO.Path.GetFileName(file).Split('.')[0].Replace(id, "").Replace("_" + uid, "").Length > 0)
                            {
                                dtUpload.Rows.Add(System.IO.Path.GetFileName(file).Split('.')[0].Replace(id+"_", "").Replace( uid+"_", "")+"."+ System.IO.Path.GetFileName(file).Split('.')[1]);
                            }

                        }
                    }
                    GridView2.DataSource = dtUpload;
                    GridView2.DataBind();


                    Session["dtUpload"] = dtUpload;

                    Session["dtDetail"] = dtPaymentDetail;

                    // Check if there's an asset linked to this payment voucher
                    LoadExistingAssetData(id);

                }
                DataTable dtDetail = new DataTable();
                try
                {
                    dtDetail.Columns.Add("Number");
                    dtDetail.Columns.Add("Detail");
                    dtDetail.Columns.Add("Amount");

                }
                catch
                {

                }
                if (command == "edit")
                {
                    // Set voucher number from existing ID (if not being edited)
                    if (!chkEditVoucherNo.Checked)
                    {
                        var getIdParams = new Dictionary<string, object>
                        {
                            { "@UID", Request.QueryString["uid"] ?? "" }
                        };
                        string existingId = code.DatabaseQuerySafe(conn,
                            "SELECT ID FROM [Account_Payment] WHERE UID = @UID",
                            getIdParams).Rows[0][0].ToString();
                        txtVoucherNo.Text = existingId;
                    }
                }
                else
                {
                    Session["dtDetail"] = dtDetail;
                    TextBox8.Text = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                    // Generate voucher number for new voucher
                    DateTime docDate = DateTime.Now;
                    string docNum = documentHelper.CreateDocumentNumber("Account_Payment", "PAY", docDate);
                    txtVoucherNo.Text = docNum;
                }
                try
                {
                    string yourHTMLstring = "<script> var Material_Name = [";
                    DataTable dt = code.DatabaseQuery(conn, "SELECT Distinct([Name]) as Material_Name FROM [Taketime].[dbo].[Vendor] Where [Status] = 'True'");
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        yourHTMLstring += "\"" + dt.Rows[i][0].ToString().Replace(",", "") + "\"";
                        if (i < dt.Rows.Count - 1)
                        {
                            yourHTMLstring += ",";
                        }
                    }
                    yourHTMLstring += "];\r\nautocomplete(document.getElementById(\"MainContent_TextBox9\"), Material_Name);</script>";
                    Literal1.Text = yourHTMLstring;
                }
                catch { }
                
            }

        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            Label1.Text = DropDownList4.SelectedItem.Text;
            
            if (TextBox1.Text.Length > 1 && TextBox2.Text.Length > 0)
            {
                DataTable dtDetail = (DataTable)Session["dtDetail"];
                dtDetail.Rows.Add(dtDetail.Rows.Count+1,TextBox1.Text,NumberHelper.TwoDecimalPoints(Convert.ToDouble(TextBox2.Text)));
                Session["dtDetail"] = (DataTable)dtDetail;
                GridView1.DataSource = dtDetail;
                GridView1.DataBind();
                calAmount(dtDetail, DropDownList4.SelectedValue);
                TextBox1.Text = "";
                TextBox2.Text = "";
            }
            else
            {

            }
        }

        public void calAmount(DataTable dtDetail,string vatType)
        {
            double totalAmount = 0;
            for(int i=0;i<dtDetail.Rows.Count;i++)
            {
                totalAmount += Convert.ToDouble(dtDetail.Rows[i]["Amount"].ToString());
            }

            // SECURE: Get VAT percent with parameterized query
            var vatParams = new Dictionary<string, object>
            {
                { "@VatTypeID", DropDownList4.SelectedValue }
            };
            int vatPercent = Convert.ToInt32(code.DatabaseQuerySafe(conn,
                "SELECT Vat_Percent FROM Account_Vat_Type WHERE Status = 'True' AND ID = @VatTypeID",
                vatParams).Rows[0][0].ToString());
            double vat = (totalAmount * vatPercent) / 100;
            double AmountIncludeVat = 0;
            if (vatType == "2")
            {
                AmountIncludeVat = totalAmount - vat;
            }
            else
            {
                AmountIncludeVat = totalAmount + vat;
            }
            
            TextBox3.Text = (NumberHelper.TwoDecimalPoints(totalAmount)).ToString();
            TextBox4.Text = (NumberHelper.TwoDecimalPoints(vat)).ToString();
            TextBox6.Text = (NumberHelper.TwoDecimalPoints(AmountIncludeVat)).ToString();

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
            calAmount(dtDetail,DropDownList4.SelectedValue);
        }

        protected void DropDownList4_SelectedIndexChanged(object sender, EventArgs e)
        {
            Label1.Text = DropDownList4.SelectedItem.Text;
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            string command = Request.QueryString["command"];
            string uid = Request.QueryString["uid"];
            string id = "";
            try
            {
                // SECURE: Get payment ID with parameterized query
                var getIdParams = new Dictionary<string, object>
                {
                    { "@UID", uid ?? "" }
                };
                id = code.DatabaseQuerySafe(conn,
                    "SELECT ID FROM [Account_Payment] WHERE UID = @UID",
                    getIdParams).Rows[0][0].ToString();
            }
            catch { }
            if (command == "edit")
            {
                // SECURE: Delete payment record with parameterized query
                var deletePaymentParams = new Dictionary<string, object>
                {
                    { "@UID", uid ?? "" }
                };
                code.DatabaseInsertSafe(conn,
                    "DELETE FROM [dbo].[Account_Payment] WHERE UID = @UID",
                    deletePaymentParams);

                // SECURE: Delete payment details with parameterized query
                var deleteDetailParams = new Dictionary<string, object>
                {
                    { "@PaymentID", id }
                };
                code.DatabaseInsertSafe(conn,
                    "DELETE FROM [dbo].[Account_Payment_Detail] WHERE Payment_ID = @PaymentID",
                    deleteDetailParams);
            }
            else
            {
            }

            if (TextBox6.Text.Length > 0 && DropDownList1.SelectedIndex >= 0 && DropDownList2.SelectedIndex > 0 && DropDownList3.SelectedIndex > 0 && DropDownList4.SelectedIndex > 0)
            {
                DateTime createDate = Convert.ToDateTime(TextBox8.Text);
                DateTime docDate = Convert.ToDateTime(TextBox8.Text);
                DataTable dtDetail = (DataTable)Session["dtDetail"];
                string docNum;

                // Priority 1: If user has edited the voucher number, use their value
                if (chkEditVoucherNo.Checked && !string.IsNullOrWhiteSpace(txtVoucherNo.Text))
                {
                    docNum = txtVoucherNo.Text.Trim();
                }
                // Priority 2: If edit mode and not editing number, use existing ID
                else if (command == "edit")
                {
                    docNum = id;
                }
                // Priority 3: Generate new number
                else
                {
                    docNum = documentHelper.CreateDocumentNumber("Account_Payment", "PAY", docDate);
                }

                // Extract Year/Month for directory structure
                string Year = docDate.Year.ToString();
                string Month = docDate.Month.ToString("00");

                // SECURE: Insert payment record with parameterized query
                var paymentInsertParams = new Dictionary<string, object>
                {
                    { "@ID", docNum },
                    { "@VendorID", DropDownList1.SelectedValue },
                    { "@CreatedDate", createDate.ToString("yyyy-MM-dd") },
                    { "@TotalAmount", TextBox6.Text },
                    { "@VatTypeID", DropDownList4.SelectedValue },
                    { "@Vat", TextBox4.Text },
                    { "@TotalAmountExcludeVat", TextBox3.Text },
                    { "@PaidHow", DropDownList2.SelectedItem.Text },
                    { "@PaidType", DropDownList3.SelectedItem.Text },
                    { "@CreatedByID", Session["UserID"].ToString() }
                };
                code.DatabaseInsertSafe(conn,
                    "INSERT INTO [dbo].[Account_Payment] ([ID],[Vendor_ID],[Created_Date],[Total_Amount],[Vat_Type_ID],[Vat],[Total_Amount_Exclude_Vat],[Paid_How],[Paid_Type],[Status],[Created_By_ID]) " +
                    "VALUES (@ID,@VendorID,@CreatedDate,@TotalAmount,@VatTypeID,@Vat,@TotalAmountExcludeVat,@PaidHow,@PaidType,N'Normal',@CreatedByID)",
                    paymentInsertParams);

                // SECURE: Insert payment details with parameterized queries
                for(int i = 0;i<dtDetail.Rows.Count;i++)
                {
                    var detailInsertParams = new Dictionary<string, object>
                    {
                        { "@PaymentID", docNum },
                        { "@Number", dtDetail.Rows[i][0].ToString() },
                        { "@Detail", dtDetail.Rows[i][1].ToString() },
                        { "@Amount", dtDetail.Rows[i][2].ToString() }
                    };
                    code.DatabaseInsertSafe(conn,
                        "INSERT INTO [dbo].[Account_Payment_Detail]([Payment_ID],[Number],[Detail],[Amount]) " +
                        "VALUES (@PaymentID,@Number,@Detail,@Amount)",
                        detailInsertParams);
                }
                string path = System.Configuration.ConfigurationSettings.AppSettings["PaymentFolderPath"].ToString();
                try
                {
                    System.IO.Directory.CreateDirectory(path + "\\" + Year);
                    System.IO.Directory.CreateDirectory(path + "\\" + Year + "\\" + Month);
                }
                catch (Exception ex)
                {

                }
                string PayNumber = docNum;
                DataTable dtbusinessinfo = code.DatabaseQuery(conn, "Select * from Business_Info left join Customer_Type on Business_Type_ID = Customer_Type.ID left join Address on Address.ID = Address_ID");
                DataTable dtBusinessinfoReport = new DataTable();
                dtBusinessinfoReport = dtbusinessinfo.Copy();
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

                DataTable dtVendor = code.DatabaseQuery(conn, "Select * from Vendor left join Customer_Type on Customer_Type.ID = Vendor_Type_ID left join Address on Address.ID = Address_ID Where Vendor.ID = '" + DropDownList1.SelectedValue + "'");

                DataTable dtVendorReport = new DataTable();
                dtVendorReport = dtVendor.Copy();


                try
                {
                    // Check if Address_ID exists - if not, use Address field as-is (complete address already stored)
                    if (dtVendorReport.Rows[0]["Address_ID"] == DBNull.Value || string.IsNullOrEmpty(dtVendorReport.Rows[0]["Address_ID"].ToString()))
                    {
                        // No Address_ID - use complete address from Address field without adding prefixes
                        dtVendorReport.Rows[0]["Address"] = dtVendor.Rows[0]["Address"].ToString();
                    }
                    else
                    {
                        // Has Address_ID - build address from structured fields with appropriate prefixes
                        if (dtVendorReport.Rows[0]["Province"].ToString().Contains("กรุงเทพ"))
                        {
                            dtVendorReport.Rows[0]["Address"] = dtVendorReport.Rows[0]["Address"].ToString() + " " + dtVendorReport.Rows[0]["Address1"].ToString() + " แขวง " + dtVendorReport.Rows[0]["SubDistrict"].ToString() + " เขต " + dtVendorReport.Rows[0]["District"].ToString() + " " + dtVendorReport.Rows[0]["Province"].ToString() + " " + dtVendorReport.Rows[0]["PostalCode"].ToString();
                        }
                        else
                        {
                            dtVendorReport.Rows[0]["Address"] = dtVendorReport.Rows[0]["Address"].ToString() + " " + dtVendorReport.Rows[0]["Address1"].ToString() + " ต." + dtVendorReport.Rows[0]["SubDistrict"].ToString() + " อ." + dtVendorReport.Rows[0]["District"].ToString() + " จ." + dtVendorReport.Rows[0]["Province"].ToString() + " " + dtVendorReport.Rows[0]["PostalCode"].ToString();
                        }
                    }
                }
                catch
                {
                    dtVendorReport.Rows[0]["Address"] = dtVendor.Rows[0]["Address"].ToString();
                }
                try
                {
                    if (dtVendorReport.Rows[0]["Branch_Number"].ToString().Length == 5)
                    {
                        dtVendorReport.Rows[0]["Name"] += " สาขาที่ " + dtVendorReport.Rows[0]["Branch_Number"].ToString();
                    }
                }
                catch { }

                DataTable dtPaymentDetail = code.DatabaseQuery(conn, "SELECT * FROM [Account_Payment_Detail] Where Payment_ID = '" + PayNumber + "' order by Number ASC");

                DataTable dtPayment = code.DatabaseQuery(conn, "SELECT * FROM [Account_Payment] inner join Account_Vat_Type on Account_Vat_Type.ID = Vat_Type_ID Where Account_Payment.ID = '" + PayNumber + "'");

                // For payroll payments (เงินเดือน), get employee name from Payroll_Records via VoucherNumber
                // This shows employee name instead of "เงินเดือนพนักงาน"
                try
                {
                    if (dtPayment.Rows.Count > 0 && dtPayment.Rows[0]["Paid_Type"]?.ToString() == "เงินเดือน")
                    {
                        // Get employee name from Payroll_Records where VoucherNumber = Account_Payment.ID
                        var empNameParams = new Dictionary<string, object>
                        {
                            { "@VoucherNumber", PayNumber }
                        };
                        DataTable dtEmployeeName = code.DatabaseQuerySafe(conn,
                            "SELECT EmployeeName FROM Payroll_Records WHERE VoucherNumber = @VoucherNumber",
                            empNameParams);

                        if (dtEmployeeName != null && dtEmployeeName.Rows.Count > 0 &&
                            dtEmployeeName.Rows[0]["EmployeeName"] != DBNull.Value &&
                            !string.IsNullOrEmpty(dtEmployeeName.Rows[0]["EmployeeName"].ToString()))
                        {
                            // Override vendor name with employee name for payroll vouchers
                            dtVendorReport.Rows[0]["Name"] = dtEmployeeName.Rows[0]["EmployeeName"].ToString();
                            dtVendorReport.Rows[0]["Address"] = ""; // Clear address for employee payments
                            System.Diagnostics.Debug.WriteLine($"Payroll voucher: Using employee name '{dtVendorReport.Rows[0]["Name"]}' instead of vendor");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting employee name for payroll payment: {ex.Message}");
                }
                uid = dtPayment.Rows[0]["UID"].ToString();
                //GridView1.DataSource = dt;
                //GridView1.DataBind();

                // Build signature DataTable directly like master (bypass SignatureService)
                DataTable dtSignature = new DataTable();
                dtSignature.Columns.Add("CreatedName");
                dtSignature.Columns.Add("Created");
                dtSignature.Columns.Add("ApprovedName");
                dtSignature.Columns.Add("Approved");
                dtSignature.Columns.Add("CheckedName");
                dtSignature.Columns.Add("Checked");
                dtSignature.Columns.Add("ReceivedName");
                dtSignature.Columns.Add("Received");

                string Signaturepath = System.Configuration.ConfigurationManager.AppSettings["StaffSignatureFolderPath"]?.ToString() ?? "";

                // Get creator name
                DataTable dtCreator = code.DatabaseQuery(conn, "Select * from Admin Where ID = " + Session["UserID"].ToString());
                string CreatorFullName = dtCreator.Rows.Count > 0 ?
                    dtCreator.Rows[0]["FirstName"].ToString() + " " + dtCreator.Rows[0]["LastName"].ToString() : "";

                // Get approver name (CEO)
                DataTable dtApprover = code.DatabaseQuery(conn, "Select * from Admin Where IsCEO = 'True'");
                string ApproverFullName = dtApprover.Rows.Count > 0 ?
                    dtApprover.Rows[0]["FirstName"].ToString() + " " + dtApprover.Rows[0]["LastName"].ToString() : "";

                // Get receiver name from employee by IDNumber
                string ReceivedFullName = "";
                string ReceivedPath = "";
                if (dtVendor.Rows.Count > 0 && dtVendor.Rows[0]["IDNumber"] != DBNull.Value)
                {
                    string idNumber = dtVendor.Rows[0]["IDNumber"].ToString();
                    if (!string.IsNullOrEmpty(idNumber))
                    {
                        DataTable dtEmployee = code.DatabaseQuery(conn, "Select * From Admin Where IDNumber = '" + idNumber + "'");
                        if (dtEmployee.Rows.Count > 0)
                        {
                            ReceivedFullName = dtEmployee.Rows[0]["FirstName"].ToString() + " " + dtEmployee.Rows[0]["LastName"].ToString();
                            ReceivedPath = Signaturepath + "\\" + ReceivedFullName.ToLower() + ".png";
                        }
                    }
                }

                // Build signature row exactly like master
                dtSignature.Rows.Add(
                    CreatorFullName,
                    "File:\\" + Signaturepath + "\\" + CreatorFullName.ToLower() + ".png",
                    ApproverFullName,
                    "File:\\" + Signaturepath + "\\" + ApproverFullName.ToLower() + ".png",
                    "",
                    "",
                    ReceivedFullName,
                    string.IsNullOrEmpty(ReceivedPath) ? "" : "File:\\" + ReceivedPath
                );

                DataTable dtUpload = (DataTable)Session["dtUpload"];
                try
                {
                    Account.Report.DataSet2 dataSet1 = new Account.Report.DataSet2();
                    dataSet1.Tables.Add(dtbusinessinfo);
                    ReportViewer2.LocalReport.DisplayName = "Payment Voucher";
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet1", dtBusinessinfoReport));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet2", dtVendorReport));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet3", dtPaymentDetail));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet4", dtPayment));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet5", dtSignature));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet6", dtUpload));
                    
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

                        using (FileStream fs = new FileStream(path + "\\" + Year + "\\" + Month + "\\" + docNum + "_"+uid+".pdf", FileMode.Append))
                        {
                            fs.Write(bytes, 0, bytes.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                    }

                    //ReportViewer2.LocalReport.Refresh();


                }

                catch { }
                try
                {
                    string filepath = path + "\\" + Year + "\\" + Month + "\\";

                    for (int i = 0; i < dtUpload.Rows.Count; i++)
                    {
                        File.Move(filepath + dtUpload.Rows[i][0].ToString(), filepath + docNum + "_" + uid + "_" + dtUpload.Rows[i][0].ToString());
                    }
                }
                catch { }

                // Create asset if checkbox is checked
                if (chkRecordAsset.Checked)
                {
                    decimal purchasePrice = decimal.Parse(TextBox6.Text);
                    int vendorId = Convert.ToInt32(DropDownList1.SelectedValue);
                    CreateAssetFromPaymentVoucher(docNum, purchasePrice, docDate, vendorId);
                }

                // Show success message then redirect
                ClientScript.RegisterStartupScript(this.GetType(), "success",
                    "alert('✅ บันทึกใบสำคัญจ่ายเรียบร้อยแล้ว'); window.location.href='/Account/PaymentVoucher';", true);
            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาระบุข้อมูลให้ครบถ้วน');", true);
            }
        }

        protected void Button4_Click(object sender, EventArgs e)
        {
            DateTime createDate = Convert.ToDateTime(TextBox8.Text);
            if (TextBox7.Text.Length > 0)
            {
                if (FileUpload1.HasFile)
                {
                    DataTable dtUpload = (DataTable)Session["dtUpload"];
                    string path = AppDomain.CurrentDomain.BaseDirectory + "\\Documents\\Payment";
                    try
                    {
                        System.IO.Directory.CreateDirectory(path + "\\" + createDate.Year.ToString());
                        System.IO.Directory.CreateDirectory(path + "\\" + createDate.Year.ToString() + "\\" + createDate.Month.ToString("00"));
                    }
                    catch (Exception ex)
                    {

                    }
                    path = path + "\\" + createDate.Year.ToString() + "\\" + createDate.Month.ToString("00");
                    string FileName = System.IO.Path.GetFileName(FileUpload1.PostedFile.FileName);
                    string FileExtension = FileName.Substring(FileName.LastIndexOf('.') + 1).ToLower();
                    string FileSaveWithPath = "";
                    string filename = TextBox7.Text + "."+FileExtension;
                    FileSaveWithPath = Server.MapPath("\\Documents\\Payment\\" + createDate.Year.ToString() + "\\" + createDate.Month.ToString() +"\\"+ filename);
                    FileUpload1.SaveAs(FileSaveWithPath);
                    dtUpload.Rows.Add(filename);
                    Session["dtUpload"] = dtUpload;
                    GridView2.DataSource = dtUpload;
                    GridView2.DataBind();
                    FileUpload1.Dispose();
                }
                else
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเลือกไฟล์ที่ต้องการจะอัพโหลดก่อน');", true);
                }
            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเระบุเบอร์โทรศัพท์ก่อน');", true);
            }
        }

        protected void GridView2_RowDeleted(object sender, GridViewDeletedEventArgs e)
        {
        }

        protected void GridView2_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            DateTime createDate = Convert.ToDateTime(TextBox8.Text);
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Documents\\Payment";
            path = path + "\\" + createDate.Year.ToString() + "\\" + createDate.Month.ToString();
            DataTable dtUpload = (DataTable)Session["dtUpload"];
            File.Delete(path+"\\"+dtUpload.Rows[e.RowIndex][0].ToString());
            dtUpload.Rows[e.RowIndex].Delete();
            dtUpload.AcceptChanges();
            Session["dtUpload"] = dtUpload;
            GridView2.DataSource = dtUpload;
            GridView2.DataBind();
        }

        protected void DropDownList5_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                SqlDataSource1.SelectCommand = "SELECT * FROM [Vendor] WHERE ([Status] = 'True') AND Vendor_Group like N'" + DropDownList5.SelectedValue + "' order by Vendor_Group,Name ASC";
                DropDownList1.Items.Clear();
                DropDownList1.Dispose();

                DataTable dtVendor = code.DatabaseQuery(SqlDataSource1.ConnectionString, SqlDataSource1.SelectCommand);
                for (int i = 0; i < dtVendor.Rows.Count; i++)
                {
                    DropDownList1.Items.Add(new ListItem(dtVendor.Rows[i]["Name"].ToString(), dtVendor.Rows[i]["ID"].ToString()));
                }
                DropDownList1.DataBind();
            }
            catch{ }
        }

        protected void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if(CheckBox1.Checked == true)
            {
                TextBox3.Enabled = true;
                TextBox4.Enabled = true;
                TextBox6.Enabled = true;
            }
            else
            {
                TextBox3.Enabled = false;
                TextBox4.Enabled = false;
                TextBox6.Enabled = false;
            }
        }

        protected void Button5_Click(object sender, EventArgs e)
        {
            Response.Redirect("/Admin/Vendor");
        }

        protected void TextBox9_TextChanged(object sender, EventArgs e)
        {
            try
            {
                DataTable dt = code.DatabaseQuery(conn, "SELECT * FROM [Taketime].[dbo].[Vendor] Where [Name] like N'" + TextBox9.Text + "'");
                DropDownList5.SelectedIndex = DropDownList5.Items.IndexOf(DropDownList5.Items.FindByText(dt.Rows[0]["Vendor_Group"].ToString()));
                DropDownList5_SelectedIndexChanged(null, null);
                DropDownList1.SelectedIndex = DropDownList1.Items.IndexOf(DropDownList1.Items.FindByValue(dt.Rows[0]["ID"].ToString()));
            }
            catch
            {
                TextBox9.Text = "";
            }
        }

        /// <summary>
        /// 🔗 Get file URL for viewing attachment
        /// ค้นหาไฟล์จริงในโฟลเดอร์และส่ง URL ที่ถูกต้อง
        /// </summary>
        protected string GetFileUrl(object fileName)
        {
            try
            {
                if (fileName == null || string.IsNullOrEmpty(fileName.ToString()))
                    return "#";

                DateTime createDate = Convert.ToDateTime(TextBox8.Text);
                string year = createDate.Year.ToString();
                string month = createDate.Month.ToString("00");
                string searchPattern = fileName.ToString(); // filename ที่ถูก strip แล้ว (เช่น "ใบเสร็จ.pdf")

                // Get payment folder path
                string basePath = System.Configuration.ConfigurationSettings.AppSettings["PaymentFolderPath"]?.ToString();
                if (string.IsNullOrEmpty(basePath))
                    return "#";

                string folderPath = System.IO.Path.Combine(basePath, year, month);

                // Fallback: check padded month directory for files created with zero-padded month
                if (!Directory.Exists(folderPath))
                {
                    string paddedMonth = month.PadLeft(2, '0');
                    string altFolderPath = System.IO.Path.Combine(basePath, year, paddedMonth);
                    if (Directory.Exists(altFolderPath))
                    {
                        folderPath = altFolderPath;
                        month = paddedMonth;
                    }
                }

                // ตรวจสอบว่าโฟลเดอร์มีอยู่จริง
                if (!Directory.Exists(folderPath))
                    return "#";

                // ✅ ดึง id และ uid จาก ViewState เพื่อค้นหาไฟล์ที่ถูกต้อง
                string paymentId = ViewState["PaymentID"]?.ToString() ?? "";
                string paymentUid = ViewState["PaymentUID"]?.ToString() ?? "";

                // ✅ ค้นหาไฟล์ด้วย pattern ที่เฉพาะเจาะจง: id_uid_filename
                // เพื่อป้องกันการดึงไฟล์ของ Payment อื่นที่มีชื่อไฟล์เดียวกัน
                string specificPattern = "";
                if (!string.IsNullOrEmpty(paymentId) && !string.IsNullOrEmpty(paymentUid))
                {
                    specificPattern = $"{paymentId}_{paymentUid}_*{searchPattern}";
                }
                else
                {
                    // Fallback: ถ้าไม่มี id/uid ให้ใช้ pattern เดิม (สำหรับ create mode)
                    specificPattern = "*" + searchPattern;
                }

                string[] matchingFiles = Directory.GetFiles(folderPath, specificPattern);

                if (matchingFiles.Length > 0)
                {
                    // ใช้ไฟล์แรกที่พบ
                    string actualFileName = System.IO.Path.GetFileName(matchingFiles[0]);
                    string virtualPath = $"~/Documents/Payment/{year}/{month}/{actualFileName}";

                    System.Diagnostics.Debug.WriteLine($"📂 GetFileUrl: Pattern='{specificPattern}', Found='{actualFileName}'");

                    return ResolveUrl(virtualPath);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ GetFileUrl: File not found for pattern '{specificPattern}' in {folderPath}");
                    return "#";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetFileUrl Error: {ex.Message}");
                return "#";
            }
        }

        /// <summary>
        /// 🎨 GridView2 RowDataBound event handler for custom styling
        /// </summary>
        protected void GridView2_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                // Style the delete button
                Button deleteButton = e.Row.Cells[1].Controls[0] as Button;
                if (deleteButton != null)
                {
                    deleteButton.CssClass = "btn-delete-file";
                    deleteButton.OnClientClick = "return confirm('คุณต้องการลบไฟล์นี้หรือไม่?');";
                }
            }
        }

        #region Asset Management Integration

        /// <summary>
        /// Load asset categories into dropdown
        /// </summary>
        private void LoadAssetCategories()
        {
            try
            {
                DataTable dt = assetService.GetAssetCategories();
                if (dt != null && dt.Rows.Count > 0)
                {
                    ddlAssetCategory.Items.Clear();
                    foreach (DataRow row in dt.Rows)
                    {
                        string text = row["CategoryCode"].ToString() + " - " + row["CategoryName"].ToString();
                        ddlAssetCategory.Items.Add(new System.Web.UI.WebControls.ListItem(text, row["ID"].ToString()));
                    }

                    // Set default useful life from first category
                    if (ddlAssetCategory.Items.Count > 0)
                    {
                        int defaultLife = Convert.ToInt32(dt.Rows[0]["DefaultUsefulLifeYears"]);
                        txtAssetUsefulLife.Text = defaultLife.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAssetCategories Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle checkbox change for recording asset
        /// </summary>
        protected void chkRecordAsset_CheckedChanged(object sender, EventArgs e)
        {
            pnlAssetDetails.Visible = chkRecordAsset.Checked;
        }

        /// <summary>
        /// Handle category change to update default useful life
        /// </summary>
        protected void ddlAssetCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(ddlAssetCategory.SelectedValue))
                {
                    DataTable dt = assetService.GetCategoryById(Convert.ToInt32(ddlAssetCategory.SelectedValue));
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        txtAssetUsefulLife.Text = dt.Rows[0]["DefaultUsefulLifeYears"].ToString();
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Load existing asset data if payment voucher was linked to an asset
        /// </summary>
        private void LoadExistingAssetData(string paymentVoucherId)
        {
            try
            {
                DataTable dtAsset = assetService.GetAssetsByPaymentVoucherId(paymentVoucherId);
                if (dtAsset != null && dtAsset.Rows.Count > 0)
                {
                    // Asset exists - check the checkbox and show the panel
                    chkRecordAsset.Checked = true;
                    pnlAssetDetails.Visible = true;

                    DataRow asset = dtAsset.Rows[0];

                    // Populate asset fields
                    txtAssetName.Text = asset["AssetName"]?.ToString() ?? "";
                    txtAssetBrand.Text = asset["Brand"]?.ToString() ?? "";
                    txtAssetModel.Text = asset["Model"]?.ToString() ?? "";
                    txtAssetSerial.Text = asset["SerialNumber"]?.ToString() ?? "";
                    txtAssetLocation.Text = asset["Location"]?.ToString() ?? "";

                    // Set category dropdown
                    if (asset["CategoryID"] != DBNull.Value)
                    {
                        string categoryId = asset["CategoryID"].ToString();
                        var categoryItem = ddlAssetCategory.Items.FindByValue(categoryId);
                        if (categoryItem != null)
                        {
                            ddlAssetCategory.SelectedValue = categoryId;
                        }
                    }

                    // Set useful life and residual value
                    if (asset["UsefulLifeYears"] != DBNull.Value)
                    {
                        txtAssetUsefulLife.Text = asset["UsefulLifeYears"].ToString();
                    }
                    if (asset["ResidualValue"] != DBNull.Value)
                    {
                        txtAssetResidual.Text = Convert.ToDecimal(asset["ResidualValue"]).ToString("0");
                    }

                    // Store the existing asset ID in ViewState (to prevent duplicate creation)
                    ViewState["ExistingAssetID"] = asset["ID"].ToString();

                    // Disable checkbox to prevent unchecking (asset already created)
                    chkRecordAsset.Enabled = false;
                    chkRecordAsset.ToolTip = "สินทรัพย์ถูกบันทึกแล้ว ไม่สามารถยกเลิกได้";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadExistingAssetData Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Create asset(s) from payment voucher
        /// Supports creating multiple assets if quantity > 1
        /// </summary>
        private void CreateAssetFromPaymentVoucher(string paymentVoucherId, decimal totalPurchasePrice, DateTime purchaseDate, int vendorId)
        {
            try
            {
                if (!chkRecordAsset.Checked)
                    return;

                // Check if asset already exists (from edit mode)
                if (ViewState["ExistingAssetID"] != null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Asset already exists (ID: {ViewState["ExistingAssetID"]}), skipping creation");
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtAssetName.Text))
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "assetwarning",
                        "alert('กรุณาระบุชื่อสินทรัพย์');", true);
                    return;
                }

                short userId = Convert.ToInt16(Session["UserID"]);
                int categoryId = Convert.ToInt32(ddlAssetCategory.SelectedValue);
                int usefulLife = string.IsNullOrEmpty(txtAssetUsefulLife.Text) ? 5 : Convert.ToInt32(txtAssetUsefulLife.Text);
                decimal totalResidualValue = string.IsNullOrEmpty(txtAssetResidual.Text) ? 0 : decimal.Parse(txtAssetResidual.Text);

                // Get quantity (default to 1)
                int quantity = 1;
                if (!string.IsNullOrEmpty(txtAssetQuantity.Text))
                {
                    int.TryParse(txtAssetQuantity.Text, out quantity);
                    if (quantity < 1) quantity = 1;
                }

                // Calculate price per unit
                decimal pricePerUnit = Math.Round(totalPurchasePrice / quantity, 2);
                decimal residualPerUnit = Math.Round(totalResidualValue / quantity, 2);

                string baseAssetName = txtAssetName.Text.Trim();
                int successCount = 0;

                for (int i = 1; i <= quantity; i++)
                {
                    // Add sequence number if multiple items
                    string assetName = quantity > 1
                        ? $"{baseAssetName} ({i}/{quantity})"
                        : baseAssetName;

                    // For serial number, only use for first item or leave blank for others
                    string serialNumber = (i == 1) ? txtAssetSerial.Text.Trim() : "";

                    var result = assetService.CreateAsset(
                        assetName: assetName,
                        description: null,
                        categoryId: categoryId,
                        serialNumber: serialNumber,
                        brand: txtAssetBrand.Text.Trim(),
                        model: txtAssetModel.Text.Trim(),
                        purchaseDate: purchaseDate,
                        purchasePrice: pricePerUnit,
                        vendorId: vendorId,
                        paymentVoucherId: paymentVoucherId,
                        invoiceNumber: null,
                        warrantyExpireDate: null,
                        usefulLifeYears: usefulLife,
                        residualValue: residualPerUnit,
                        location: txtAssetLocation.Text.Trim(),
                        department: null,
                        responsiblePersonId: null,
                        notes: quantity > 1
                            ? $"สร้างจากใบสำคัญจ่าย: {paymentVoucherId} (ชิ้นที่ {i}/{quantity})"
                            : $"สร้างจากใบสำคัญจ่าย: {paymentVoucherId}",
                        createdBy: userId
                    );

                    if (result.Success)
                    {
                        successCount++;
                        System.Diagnostics.Debug.WriteLine($"✅ Asset {i}/{quantity} created: {result.Message}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Asset {i}/{quantity} creation failed: {result.Message}");
                    }
                }

                if (successCount > 0 && quantity > 1)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Created {successCount}/{quantity} assets successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateAssetFromPaymentVoucher Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle Edit checkbox change for voucher number
        /// Toggle ReadOnly state of txtVoucherNo
        /// </summary>
        protected void chkEditVoucherNo_CheckedChanged(object sender, EventArgs e)
        {
            if (chkEditVoucherNo.Checked)
            {
                // Enable editing
                txtVoucherNo.ReadOnly = false;
                txtVoucherNo.BackColor = System.Drawing.Color.White;
            }
            else
            {
                // Disable editing
                txtVoucherNo.ReadOnly = true;
                txtVoucherNo.BackColor = System.Drawing.Color.LightGray;
            }
        }

        /// <summary>
        /// Handle date change - regenerate voucher number based on new date
        /// </summary>
        protected void TextBox8_TextChanged(object sender, EventArgs e)
        {
            try
            {
                string command = Request.QueryString["command"];

                // Generate new voucher number for the selected date
                DateTime newDate = Convert.ToDateTime(TextBox8.Text);
                string newDocNum = documentHelper.CreateDocumentNumber("Account_Payment", "PAY", newDate);

                txtVoucherNo.Text = newDocNum;

                // If user had Edit checkbox checked, keep it editable
                if (chkEditVoucherNo.Checked)
                {
                    txtVoucherNo.ReadOnly = false;
                    txtVoucherNo.BackColor = System.Drawing.Color.White;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TextBox8_TextChanged Error: {ex.Message}");
            }
        }

        #endregion
    }
}