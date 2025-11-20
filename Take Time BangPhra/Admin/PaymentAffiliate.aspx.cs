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

        protected void Page_Load(object sender, EventArgs e)
        {
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
                if (command == "edit")
                {
                    DataTable dtPayment = code.DatabaseQuery(conn, "Select * from Account_Payment Where ID = '" + id + "'");
                    DataTable dtPaymentDetail = code.DatabaseQuery(conn, "Select Number,Detail,Amount from Account_Payment_Detail Where Payment_ID = '" + id + "'");

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
                code.DatabaseInsert(conn, "DELETE FROM [dbo].[Account_Payment] WHERE ID = '" + id + "'");
                code.DatabaseInsert(conn, "DELETE FROM [dbo].[Account_Payment_Detail] WHERE Payment_ID = '" + id + "'");
            }
            else
            {
            }

            if (TextBox6.Text.Length > 0)
            {
                DateTime createDate = Convert.ToDateTime(TextBox8.Text);
                string Year = Convert.ToDateTime(TextBox8.Text).Year.ToString();
                string Month = Convert.ToDateTime(TextBox8.Text).Month.ToString();
                string Day = Convert.ToDateTime(TextBox8.Text).Day.ToString();
                DataTable dtDetail = (DataTable)Session["dtDetail"];
                string docNum = code.createDocNumber(conn, "Account_Payment", "PAY",Year,Month,Day);
                if (command == "edit")
                {
                    docNum = id;
                }
                int accountPaymentID = code.DatabaseInsert(conn, "INSERT INTO [dbo].[Account_Payment] ([ID],[Vendor_ID],[Created_Date],[Total_Amount],[Vat_Type_ID],[Vat],[Total_Amount_Exclude_Vat],[Paid_How],[Paid_Type],[Status],[Created_By_ID]) VALUES ('"+docNum+"',N'"+ DropDownList5.SelectedValue+ "','"+createDate.ToString("yyyy-MM-dd")+"','"+TextBox6.Text+"','4','"+TextBox4.Text+"','"+TextBox3.Text+"',N'"+DropDownList2.SelectedItem.Text+"','2',N'Normal','"+Session["UserID"].ToString()+ "'); Select scope_identity();");
                for(int i = 0;i<dtDetail.Rows.Count;i++)
                {

                    code.DatabaseInsert(conn, "INSERT INTO [dbo].[Affiliate_Reservation_Payment] ([Affiliate_Reservation_ID],[Account_Payment_ID]) VALUES ('"+accountPaymentID+"','"+ dtDetail.Rows[i]["AffResID"].ToString() + "')");
                    code.DatabaseInsert(conn, "INSERT INTO [dbo].[Account_Payment_Detail]([Payment_ID],[Number],[Detail],[Amount]) VALUES ('" + docNum + "','"+dtDetail.Rows[i][0].ToString()+ "',N'" + dtDetail.Rows[i][1].ToString() + "','" + dtDetail.Rows[i][2].ToString() + "')");
                    code.DatabaseInsert(conn, "UPDATE [dbo].[Affiliate_Reservation] SET [Status] = 'TRANSFERED' WHERE ID = '" + dtDetail.Rows[i]["AffResID"].ToString() + "'");
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
                DataTable dtVendor = code.DatabaseQuery(conn, "Select * from Vendor left join Customer_Type on Customer_Type.ID = Vendor_Type_ID left join Address on Address.ID = Address_ID Where Vendor.ID = '" + DropDownList5.SelectedValue + "'");

                DataTable dtVendorReport = new DataTable();
                dtVendorReport = dtVendor.Copy();
                try
                {
                    if (dtVendorReport.Rows[0]["Province"].ToString().Contains("กรุงเทพ"))
                    {
                        dtVendorReport.Rows[0]["Address"] = dtVendorReport.Rows[0]["Address"].ToString() + " " + dtVendorReport.Rows[0]["Address1"].ToString() + " แขวง " + dtVendorReport.Rows[0]["SubDistrict"].ToString() + " เขต " + dtVendorReport.Rows[0]["District"].ToString() + " " + dtVendorReport.Rows[0]["Province"].ToString() + " " + dtVendorReport.Rows[0]["PostalCode"].ToString();
                    }
                    else
                    {
                        dtVendorReport.Rows[0]["Address"] = dtVendorReport.Rows[0]["Address"].ToString() + " " + dtVendorReport.Rows[0]["Address1"].ToString() + " ต." + dtVendorReport.Rows[0]["SubDistrict"].ToString() + " อ." + dtVendorReport.Rows[0]["District"].ToString() + " จ." + dtVendorReport.Rows[0]["Province"].ToString() + " " + dtVendorReport.Rows[0]["PostalCode"].ToString();
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
                //GridView1.DataSource = dt;
                //GridView1.DataBind();
                DataTable dtSignature = new DataTable();
                try
                {
                    dtSignature.Columns.Add("CreatedName");
                    dtSignature.Columns.Add("Created");
                    dtSignature.Columns.Add("ApprovedName");
                    dtSignature.Columns.Add("Approved");
                    dtSignature.Columns.Add("CheckedName");
                    dtSignature.Columns.Add("Checked");
                    dtSignature.Columns.Add("ReceivedName");
                    dtSignature.Columns.Add("Received");
                }
                catch
                {

                }
                string Signaturepath = System.Configuration.ConfigurationSettings.AppSettings["StaffSignatureFolderPath"].ToString();
                DataTable dtCreator = code.DatabaseQuery(conn, "Select * from Admin Where ID = "+ Session["UserID"].ToString());
                string CreatorFullName = dtCreator.Rows[0]["FirstName"].ToString() + " " + dtCreator.Rows[0]["LastName"].ToString();

                DataTable dtApprover = code.DatabaseQuery(conn, "Select * from Admin Where IsCEO = 'True'");
                string ApproverFullName = dtApprover.Rows[0]["FirstName"].ToString() + " " + dtApprover.Rows[0]["LastName"].ToString();


                DataTable dtEmployee = code.DatabaseQuery(conn, "Select * From Admin Where IDNumber = '"+ dtVendor.Rows[0]["IDNumber"].ToString() + "'");
                string ReceivedFullName = "";
                string ReceivedPath = "";
                if(dtEmployee.Rows.Count > 0)
                {
                    ReceivedFullName = dtEmployee.Rows[0]["FirstName"].ToString() + " " + dtEmployee.Rows[0]["LastName"].ToString();
                    ReceivedPath = Signaturepath + "\\" + ReceivedFullName.ToLower() + ".png";
                }
                dtSignature.Rows.Add(CreatorFullName,"File:\\"+Signaturepath+"\\"+CreatorFullName.ToLower()+".png",ApproverFullName,"File:\\"+Signaturepath+"\\"+ApproverFullName.ToLower()+".png","","",ReceivedFullName, "File:\\"+ReceivedPath);

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
                Response.Redirect("/Account/PaymentVoucher");
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
                DataTable dt = code.DatabaseQuery(conn, "SELECT Affiliate_Reservation.ID,StayDate,AccomName,PriceAfterDiscount as PricePerDay,Commission FROM [Taketime].[dbo].[Affiliate_Reservation] inner join Accommodation on Accommodation.ID = Accommodation_ID inner join Affiliate_Discount_RatePlan on Affiliate_Discount_RatePlan.ID = Affiliate_Discount_RatePlan_ID inner join Reservation on Reservation.ID = Reservation_ID Where Reservation.Status = N'เช็คอินแล้ว' AND Affiliate_Reservation.Status = 'NEW' AND Affiliate_Member_Coupon_Code = '" + DropDownList5.SelectedItem.Text + "' order by StayDate desc");
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