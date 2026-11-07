using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using iTextSharp.text.pdf.qrcode;

namespace Take_Time_BangPhra.Voucher
{
    public partial class Voucher_Confirmed : System.Web.UI.Page
    {
        code code2 = new code();
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        protected async void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SalesVoucher)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
          if (Session["permission"].ToString() == "True" && (Session["User"].ToString() == "Owner" || Session["User"].ToString() == "Admin"))
            {

            }
            else
            {
                Response.Redirect("/Default");
            }
            Page.MaintainScrollPositionOnPostBack = true;
            try
            {
                string id = Request.QueryString["id"];
                id = id.Replace("@", " ");

                // SECURE: Voucher lookup with parameterized query
                var voucherParams = new Dictionary<string, object>
                {
                    { "@CreatedDate", id ?? "" }
                };

                DataTable dtVoucher = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Taketime].[dbo].[Voucher] " +
                    "INNER JOIN Customer ON Customer.ID = Customer_ID " +
                    "WHERE Created_Date = @CreatedDate",
                    voucherParams);

                Image1.ImageUrl = "../Upload/Slip/" + dtVoucher.Rows[0]["Voucher_Number"].ToString() + ".jpg";
                Image1.DataBind();
                string vnumber = "";
                for(int i = 0;i<dtVoucher.Rows.Count;i++)
                {
                    vnumber += dtVoucher.Rows[i]["Voucher_Number"].ToString() + "<br/>"+ Environment.NewLine ;
                }
                Label1.Text = vnumber;
                Label2.Text = dtVoucher.Rows[0]["Name"].ToString();
                Label3.Text = dtVoucher.Rows[0]["NickName"].ToString();
                Label4.Text = dtVoucher.Rows[0]["MobilePhone"].ToString(); 
                Label5.Text = dtVoucher.Rows[0]["Sell_Price"].ToString();
                Label14.Text = dtVoucher.Rows[0]["Remark"].ToString();
                Label15.Text = (Convert.ToDouble(dtVoucher.Rows[0]["Sell_Price"].ToString()) * Convert.ToDouble(dtVoucher.Rows.Count)).ToString();
                Label16.Text = ( Convert.ToDouble(dtVoucher.Rows.Count)).ToString();
                Label10.Text = "ยืนยันการจองสำเร็จ (Reservation Complete)";
            }
            catch
            {
                Label10.Text = "ยืนยันการจองผิดพลาด (Reservation Error)";
            }
        }
    }
}