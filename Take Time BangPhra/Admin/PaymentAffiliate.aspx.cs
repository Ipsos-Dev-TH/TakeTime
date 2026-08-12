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
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.Admin
{
    public partial class PaymentAffiliate : System.Web.UI.Page
    {
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        DocumentHelper documentHelper;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Affiliate", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            documentHelper = new DocumentHelper(conn);
            try
            {
                if (Session["permission"].ToString() == "True" && (Session["User"].ToString() == "Owner"))
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
                string command = Request.QueryString["command"];
                string id = Request.QueryString["id"];
                string affiliateCode = Request.QueryString["affiliate"];

                if (command == "edit")
                {
                    // SECURE: Payment lookup with parameterized query
                    var paymentParams = new Dictionary<string, object>
                    {
                        { "@ID", id ?? "" }
                    };

                    DataTable dtPayment = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Account_Payment WHERE ID = @ID",
                        paymentParams);

                    // SECURE: Payment detail lookup with parameterized query
                    var paymentDetailParams = new Dictionary<string, object>
                    {
                        { "@PaymentID", id ?? "" }
                    };

                    DataTable dtPaymentDetail;
                    try
                    {
                        dtPaymentDetail = code.DatabaseQuerySafe(conn,
                            "SELECT Number, Detail, Amount, ISNULL(Paid_Type_ID, 0) AS PaidTypeId, ISNULL(Paid_Type_Name, N'') AS PaidTypeName, ISNULL(CAST(Nexaacc_AccountId AS NVARCHAR(50)), N'') AS NexaaccAccountId FROM Account_Payment_Detail WHERE Payment_ID = @PaymentID",
                            paymentDetailParams);
                    }
                    catch
                    {
                        dtPaymentDetail = code.DatabaseQuerySafe(conn,
                            "SELECT Number, Detail, Amount FROM Account_Payment_Detail WHERE Payment_ID = @PaymentID",
                            paymentDetailParams);
                    }

                    TextBox8.Text = Convert.ToDateTime(dtPayment.Rows[0]["Created_Date"].ToString()).ToString("yyyy-MM-dd");
                    TextBox3.Text = dtPayment.Rows[0]["Total_Amount_Exclude_Vat"].ToString();
                    TextBox4.Text = dtPayment.Rows[0]["Vat"].ToString();
                    TextBox6.Text = dtPayment.Rows[0]["Total_Amount"].ToString();




                    Session["dtDetail"] = dtPaymentDetail;

                }
                DataTable dtDetail = new DataTable();
                try
                {
                    dtDetail.Columns.Add("Number");
                    dtDetail.Columns.Add("Detail");
                    dtDetail.Columns.Add("Amount");
                    dtDetail.Columns.Add("AffResID");
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

                // Pre-select affiliate if passed from AffiliateManagement
                if (!string.IsNullOrEmpty(affiliateCode))
                {
                    // Find and select the affiliate in dropdown
                    foreach (ListItem item in DropDownList5.Items)
                    {
                        if (item.Text == affiliateCode)
                        {
                            item.Selected = true;
                            // Trigger the selection to load pending commissions
                            DropDownList5_SelectedIndexChanged(null, null);
                            break;
                        }
                    }
                }
            }

            DataTable dt = (DataTable)Session["dtDetail"];
            dt.Rows.Clear();
            dt.Clear();
            dt.AcceptChanges();
            double incentiveTotal = 0;
            double vat = 0;
            double incentiveTotalExcludeVat = 0;
            int i = 1;
            foreach (GridViewRow row in GridView3.Rows)
            {
                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                if (chk != null && chk.Checked)
                {
                    foreach (GridViewRow row2 in GridView3.Rows)
                    {
                        if (row.Cells[1].Text == row2.Cells[1].Text)
                        {
                            CheckBox chk2 = (row2.Cells[0].FindControl("chkSelect") as CheckBox);
                            chk2.Checked = true;
                            chk2.DataBind();
                        }
                    }
                }
            }


            foreach (GridViewRow row in GridView3.Rows)
            {

                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                if (chk != null && chk.Checked)
                {
                    incentiveTotal += NumberHelper.TwoDecimalPoints(Convert.ToDouble(row.Cells[5].Text));
                    dt.Rows.Add(i.ToString(), "ค่าแนะนำ ID:" + row.Cells[1].Text + " เข้าพัก:" + row.Cells[2].Text +" ห้องพัก:" + row.Cells[3].Text + " ราคาต่อคืน:" + row.Cells[4].Text, NumberHelper.TwoDecimalPoints(Convert.ToDouble(row.Cells[5].Text)), row.Cells[1].Text);
                    i++;
                }
                else
                {

                }
            }

            vat = NumberHelper.TwoDecimalPoints((incentiveTotal * 0) / 100);
            incentiveTotalExcludeVat = NumberHelper.TwoDecimalPoints(incentiveTotal - vat);

            TextBox3.Text = incentiveTotalExcludeVat.ToString();
            TextBox4.Text = vat.ToString();
            TextBox6.Text = incentiveTotal.ToString();
            Session["dtDetail"] = dt;
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            string command = Request.QueryString["command"];
            string id = Request.QueryString["id"];
            if (command == "edit")
            {
                // Void เอกสารเก่าในระบบบัญชี
                try
                {
                    var sync = new Integration.AccountingSyncService(conn);
                    sync.EnqueueVoidPaymentVoucher(id);
                }
                catch (Exception accEx)
                {
                    new code().Logs(conn, "Accounting Sync", $"Void voucher error (PaymentAffiliate): id={id} {accEx.Message}", "SYSTEM");
                }

                // SECURE: DELETE operations with parameterized queries
                var deletePaymentParams = new Dictionary<string, object>
                {
                    { "@ID", id }
                };

                code.DatabaseInsertSafe(conn,
                    "DELETE FROM [dbo].[Account_Payment] WHERE ID = @ID",
                    deletePaymentParams);

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

            if (TextBox6.Text.Length > 0)
            {
                DateTime createDate = Convert.ToDateTime(TextBox8.Text);
                DateTime docDate = Convert.ToDateTime(TextBox8.Text);
                DataTable dtDetail = (DataTable)Session["dtDetail"];
                string docNum = documentHelper.CreateDocumentNumber("Account_Payment", "PAY", docDate);

                // Extract Year/Month for directory structure
                string Year = docDate.Year.ToString();
                string Month = docDate.Month.ToString("00");

                if (command == "edit")
                {
                    docNum = id;
                }

                // SECURE: Account_Payment INSERT with parameterized query
                var paymentInsertParams = new Dictionary<string, object>
                {
                    { "@DocNum", docNum },
                    { "@VendorID", DropDownList5.SelectedValue },
                    { "@CreatedDate", createDate },
                    { "@TotalAmount", TextBox6.Text },
                    { "@Vat", TextBox4.Text },
                    { "@TotalAmountExcludeVat", TextBox3.Text },
                    { "@PaidHow", DropDownList2.SelectedItem.Text },
                    { "@CreatedByID", Session["UserID"].ToString() }
                };

                int accountPaymentID = code.DatabaseInsertSafe(conn,
                    "INSERT INTO [dbo].[Account_Payment] " +
                    "([ID],[Vendor_ID],[Created_Date],[Total_Amount],[Vat_Type_ID],[Vat],[Total_Amount_Exclude_Vat],[Paid_How],[Paid_Type],[Status],[Created_By_ID]) " +
                    "VALUES (@DocNum,@VendorID,@CreatedDate,@TotalAmount,'4',@Vat,@TotalAmountExcludeVat,@PaidHow,'2',N'Normal',@CreatedByID); " +
                    "SELECT SCOPE_IDENTITY();",
                    paymentInsertParams);

                for(int i = 0;i<dtDetail.Rows.Count;i++)
                {
                    // SECURE: Affiliate_Reservation_Payment INSERT with parameterized query
                    var affResPaymentParams = new Dictionary<string, object>
                    {
                        { "@AccountPaymentID", accountPaymentID },
                        { "@AffResID", dtDetail.Rows[i]["AffResID"].ToString() }
                    };

                    code.DatabaseInsertSafe(conn,
                        "INSERT INTO [dbo].[Affiliate_Reservation_Payment] ([Affiliate_Reservation_ID],[Account_Payment_ID]) " +
                        "VALUES (@AccountPaymentID,@AffResID)",
                        affResPaymentParams);

                    // SECURE: Account_Payment_Detail INSERT with parameterized query (including per-line category)
                    try
                    {
                        string linePaidTypeId = dtDetail.Columns.Contains("PaidTypeId") ? dtDetail.Rows[i]["PaidTypeId"]?.ToString() : "";
                        string linePaidTypeName = dtDetail.Columns.Contains("PaidTypeName") ? dtDetail.Rows[i]["PaidTypeName"]?.ToString() : "";
                        string lineNexaaccId = dtDetail.Columns.Contains("NexaaccAccountId") ? dtDetail.Rows[i]["NexaaccAccountId"]?.ToString() : "";

                        var paymentDetailParams = new Dictionary<string, object>
                        {
                            { "@DocNum", docNum },
                            { "@Number", dtDetail.Rows[i]["Number"].ToString() },
                            { "@Detail", dtDetail.Rows[i]["Detail"].ToString() },
                            { "@Amount", dtDetail.Rows[i]["Amount"].ToString() },
                            { "@PaidTypeId", string.IsNullOrEmpty(linePaidTypeId) || linePaidTypeId == "0" ? (object)DBNull.Value : Convert.ToInt32(linePaidTypeId) },
                            { "@PaidTypeName", string.IsNullOrEmpty(linePaidTypeName) ? (object)DBNull.Value : linePaidTypeName },
                            { "@NexaaccAccountId", string.IsNullOrEmpty(lineNexaaccId) ? (object)DBNull.Value : lineNexaaccId }
                        };
                        code.DatabaseInsertSafe(conn,
                            "INSERT INTO [dbo].[Account_Payment_Detail]([Payment_ID],[Number],[Detail],[Amount],[Paid_Type_ID],[Paid_Type_Name],[Nexaacc_AccountId]) " +
                            "VALUES (@DocNum,@Number,@Detail,@Amount,@PaidTypeId,@PaidTypeName,@NexaaccAccountId)",
                            paymentDetailParams);
                    }
                    catch
                    {
                        var fallbackParams = new Dictionary<string, object>
                        {
                            { "@DocNum", docNum },
                            { "@Number", dtDetail.Rows[i]["Number"].ToString() },
                            { "@Detail", dtDetail.Rows[i]["Detail"].ToString() },
                            { "@Amount", dtDetail.Rows[i]["Amount"].ToString() }
                        };
                        code.DatabaseInsertSafe(conn,
                            "INSERT INTO [dbo].[Account_Payment_Detail]([Payment_ID],[Number],[Detail],[Amount]) " +
                            "VALUES (@DocNum,@Number,@Detail,@Amount)",
                            fallbackParams);
                    }

                    // SECURE: Affiliate_Reservation UPDATE with parameterized query
                    var affResUpdateParams = new Dictionary<string, object>
                    {
                        { "@AffResID", dtDetail.Rows[i]["AffResID"].ToString() }
                    };

                    code.DatabaseInsertSafe(conn,
                        "UPDATE [dbo].[Affiliate_Reservation] SET [Status] = 'TRANSFERED' WHERE ID = @AffResID",
                        affResUpdateParams);
                }
                // Auto-sync voucher to accounting
                try
                {
                    var acctConfig = new Integration.AccountingConfig(conn);
                    if (acctConfig.IsConfigured && acctConfig.Enabled)
                    {
                        decimal voucherAmount = Convert.ToDecimal(TextBox6.Text);
                        string payMethod = DropDownList2.SelectedItem?.Text ?? "CASH";
                        string vendorName = DropDownList5.SelectedItem?.Text ?? "";
                        string desc = dtDetail.Rows.Count > 0 ? dtDetail.Rows[0][1]?.ToString() ?? "" : "";
                        DateTime voucherDate = Convert.ToDateTime(TextBox8.Text);

                        if (acctConfig.IsDocumentMode || (!string.IsNullOrEmpty(docNum) && docNum != "0"))
                        {
                            var sync = new Integration.AccountingSyncService(conn);
                            string affPayAccId = sync.LookupPaidHowAccountId(payMethod);
                            sync.EnqueuePaymentVoucher(0, "AFFILIATE", voucherAmount, payMethod,
                                voucherDate, desc, vendorName, documentNumber: docNum,
                                paymentAccountId: affPayAccId);
                        }
                    }
                }
                catch (Exception accEx)
                {
                    new code().Logs(conn, "Accounting Sync", $"Voucher auto-sync error (PaymentAffiliate): {accEx.Message}", "SYSTEM");
                }

                string path = AppCfg.Get("PaymentFolderPath").ToString();
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

                // SECURE: Vendor lookup with parameterized query
                var vendorParams = new Dictionary<string, object>
                {
                    { "@VendorID", DropDownList5.SelectedValue }
                };

                DataTable dtVendor = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Vendor " +
                    "LEFT JOIN Customer_Type ON Customer_Type.ID = Vendor_Type_ID " +
                    "LEFT JOIN Address ON Address.ID = Address_ID " +
                    "WHERE Vendor.ID = @VendorID",
                    vendorParams);

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

                // SECURE: Payment detail lookup with parameterized query
                var paymentDetailReportParams = new Dictionary<string, object>
                {
                    { "@PayNumber", PayNumber }
                };

                DataTable dtPaymentDetail = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Account_Payment_Detail] WHERE Payment_ID = @PayNumber ORDER BY Number ASC",
                    paymentDetailReportParams);

                // SECURE: Payment with VAT type lookup with parameterized query
                var paymentReportParams = new Dictionary<string, object>
                {
                    { "@PayNumber", PayNumber }
                };

                DataTable dtPayment = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Account_Payment] " +
                    "INNER JOIN Account_Vat_Type ON Account_Vat_Type.ID = Vat_Type_ID " +
                    "WHERE Account_Payment.ID = @PayNumber",
                    paymentReportParams);

                //GridView1.DataSource = dt;
                //GridView1.DataBind();

                // Use SignatureService for centralized signature management
                SignatureService signatureService = new SignatureService();
                short creatorAdminId = 0;
                short.TryParse(Session["UserID"]?.ToString() ?? "0", out creatorAdminId);
                string receiverIdNumber = dtVendor.Rows.Count > 0 ? dtVendor.Rows[0]["IDNumber"]?.ToString() ?? "" : "";

                DataTable dtSignature = signatureService.GetPaymentVoucherSignatureData(creatorAdminId, receiverIdNumber);

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

                        using (FileStream fs = new FileStream(path + "\\" + Year + "\\" + Month + "\\" + docNum + ".pdf", FileMode.Append))
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
                        File.Move(filepath + dtUpload.Rows[i][0].ToString(), filepath + docNum + "_" + dtUpload.Rows[i][0].ToString());
                    }
                }
                catch { }
                // Show success message then redirect
                ClientScript.RegisterStartupScript(this.GetType(), "success",
                    "alert('✅ บันทึกใบสำคัญจ่าย Affiliate เรียบร้อยแล้ว'); window.location.href='/Account/PaymentVoucher';", true);
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
                        System.IO.Directory.CreateDirectory(path + "\\" + createDate.Year.ToString() + "\\" + createDate.Month.ToString());
                    }
                    catch (Exception ex)
                    {

                    }
                    path = path + "\\" + createDate.Year.ToString() + "\\" + createDate.Month.ToString();
                    string FileName = Path.GetFileName(FileUpload1.PostedFile.FileName);
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
                // SECURE: Affiliate reservation lookup with parameterized query
                var affResParams = new Dictionary<string, object>
                {
                    { "@CouponCode", DropDownList5.SelectedItem.Text }
                };

                DataTable dt = code.DatabaseQuerySafe(conn,
                    "SELECT Affiliate_Reservation.ID,StayDate,AccomName,PriceAfterDiscount AS PricePerDay,Commission " +
                    "FROM [Taketime].[dbo].[Affiliate_Reservation] " +
                    "INNER JOIN Accommodation ON Accommodation.ID = Accommodation_ID " +
                    "INNER JOIN Affiliate_Discount_RatePlan ON Affiliate_Discount_RatePlan.ID = Affiliate_Discount_RatePlan_ID " +
                    "INNER JOIN Reservation ON Reservation.ID = Reservation_ID " +
                    "WHERE Reservation.Status = N'เช็คอินแล้ว' " +
                    "AND Affiliate_Reservation.Status = 'NEW' " +
                    "AND Affiliate_Member_Coupon_Code = @CouponCode " +
                    "ORDER BY StayDate DESC",
                    affResParams);

                GridView3.DataSource = dt;
                GridView3.DataBind();
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

        protected void Button5_Click1(object sender, EventArgs e)
        {
            DropDownList5_SelectedIndexChanged(null, null);
        }
    }
}