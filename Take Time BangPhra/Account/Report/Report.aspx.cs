using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using System.IO;
using System.Data.SqlClient;
using System.Drawing;
using System.Data.OleDb;
using System.Text;
using System.IO;
using System.Globalization;
using Microsoft.Reporting.WebForms;

namespace Take_Time_BangPhra.Account.Report
{
    public partial class Report : System.Web.UI.Page
    {

        code code = new code();
        string Email = "";
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        //int cutoffday = Convert.ToInt32(ConfigurationManager.AppSettings["PaymentCutOffDay"].ToString());
        protected void Page_Load(object sender, EventArgs e)
        {
            Panel6.Visible = true;
            string RecNumber = "REC221227004";
            DataTable dtbusinessinfo = code.DatabaseQuery(conn, "Select * from Business_Info");
            
            DataTable dtReceiptDetail = code.DatabaseQuery(conn, "SELECT * FROM [Account_Receipt_Detail] inner join Account_ProductType on Account_ProductType.ID = ProductType_ID Where Receipt_ID = '"+RecNumber+ "' order by Number ASC");
            DataTable dtReceipt = code.DatabaseQuery(conn, "SELECT * FROM [Account_Receipt] inner join Reservation on Reservation.ID = Reservation_ID Where Account_Receipt.ID = '" + RecNumber+"'");
            DataTable dtcustomer = code.DatabaseQuery(conn, "Select * from Customer Where MobilePhone = '"+dtReceipt.Rows[0]["Customer_MobilePhone"].ToString()+"'");
            //GridView1.DataSource = dt;
            //GridView1.DataBind();
            try
            {
                if (!IsPostBack)
                {
                    var Parameterx = new ReportParameter("status", "Cancel");
                    ReportViewer2.LocalReport.SetParameters(new ReportParameter[] { Parameterx });
                    DataSet1 dataSet1 = new DataSet1();
                    dataSet1.Tables.Add(dtbusinessinfo);
                    ReportViewer2.LocalReport.DisplayName = "Receipt";
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet1", dtbusinessinfo));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet2", dtcustomer));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet3", dtReceiptDetail));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet4", dtReceipt));

                    try
                    {

                        var deviceInfo = @"<DeviceInfo>
                    <EmbedFonts>None</EmbedFonts>
                   </DeviceInfo>";


                        Warning[] warnings;
                        string[] streamids;
                        string mimeType;
                        string encoding;
                        string filenameExtension;

                        byte[] bytes = ReportViewer2.LocalReport.Render(
                            "PDF", deviceInfo, out mimeType, out encoding, out filenameExtension,
                            out streamids, out warnings);

                        using (FileStream fs = new FileStream("D:\\output.pdf", FileMode.Create))
                        {
                            fs.Write(bytes, 0, bytes.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                    }

                        //ReportViewer2.LocalReport.Refresh();

                    
                }
            }
            catch { }
        }
    }
        
}