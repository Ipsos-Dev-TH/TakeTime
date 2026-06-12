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
            // Admin/Owner only — page renders receipt data (customer PII + financials)
            try
            {
                if (Session["permission"]?.ToString() != "True" ||
                    (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
                {
                    Response.Redirect("~/Admin/Login", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
            }
            catch
            {
                Response.Redirect("~/Admin/Login", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            Panel6.Visible = true;

            // ต้องระบุเลขใบเสร็จผ่าน ?id= เท่านั้น — ไม่มีค่า default
            string RecNumber = Request.QueryString["id"];
            if (string.IsNullOrEmpty(RecNumber)) return;

            try
            {
                var recDetailParams = new Dictionary<string, object> { { "@ReceiptID", RecNumber } };
                DataTable dtReceiptDetail = code.DatabaseQuerySafe(conn, "SELECT * FROM [Account_Receipt_Detail] inner join Account_ProductType on Account_ProductType.ID = ProductType_ID Where Receipt_ID = @ReceiptID order by Number ASC", recDetailParams);
                var recParams = new Dictionary<string, object> { { "@ReceiptID", RecNumber } };
                DataTable dtReceipt = code.DatabaseQuerySafe(conn, "SELECT * FROM [Account_Receipt] inner join Reservation on Reservation.ID = Reservation_ID Where Account_Receipt.ID = @ReceiptID", recParams);
                if (dtReceipt == null || dtReceipt.Rows.Count == 0) return;

                DataTable dtbusinessinfo = code.DatabaseQuery(conn, "Select * from Business_Info");
                var custParams = new Dictionary<string, object> { { "@MobilePhone", dtReceipt.Rows[0]["Customer_MobilePhone"].ToString() } };
                DataTable dtcustomer = code.DatabaseQuerySafe(conn, "Select * from Customer Where MobilePhone = @MobilePhone", custParams);

                if (!IsPostBack)
                {
                    var Parameterx = new ReportParameter("status", "Cancel");
                    ReportViewer2.LocalReport.SetParameters(new ReportParameter[] { Parameterx });
                    ReportViewer2.LocalReport.DisplayName = "Receipt";
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet1", dtbusinessinfo));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet2", dtcustomer));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet3", dtReceiptDetail));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet4", dtReceipt));
                    ReportViewer2.LocalReport.Refresh();
                }
            }
            catch (Exception ex)
            {
                try { code.Logs(conn, "ReportPage", "Report load failed: " + ex.Message, "SYSTEM"); } catch { }
            }
        }
    }
        
}