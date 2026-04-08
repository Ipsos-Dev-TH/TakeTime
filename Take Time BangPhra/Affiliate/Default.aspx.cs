using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using System.Resources;
using System.Runtime.Remoting.Lifetime;

namespace Take_Time_BangPhra.Affiliate
{
    public partial class Default : System.Web.UI.Page
    {
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        protected void Page_Load(object sender, EventArgs e)
        {
            string LoginID = "";
            try
            {
                LoginID = Session["AffiliateID"].ToString();
                var loginParams = new Dictionary<string, object> { { "@IDNumber", LoginID } };
                DataTable dt = code.DatabaseQuerySafe(conn, "SELECT * FROM [Taketime].[dbo].[Affiliate_Member] inner join vendor on Affiliate_Member.ID_Number = Vendor.IDNumber Where ID_Number = @IDNumber", loginParams);
                TextBox1.Text = dt.Rows[0]["Name"].ToString();
                TextBox2.Text = dt.Rows[0]["Bank_Code"].ToString();
                TextBox3.Text = dt.Rows[0]["Bank_Number"].ToString();
                TextBox4.Text = dt.Rows[0]["Coupon_Code"].ToString();
                TextBox5.Text = HttpContext.Current.Request.Url.Host+"/Reserve.aspx?couponcode="+dt.Rows[0]["Coupon_Code"].ToString();
                var sumParams = new Dictionary<string, object> { { "@CouponCode", LoginID }, { "@Status", "TRANSFERED" } };
                DataTable dtSum = code.DatabaseQuerySafe(conn, "SELECT SUM(Commission) as Commission FROM [Taketime].[dbo].[Affiliate_Reservation] Where Affiliate_Member_Coupon_Code = @CouponCode AND Status = @Status", sumParams);
                TextBox6.Text = dtSum.Rows[0][0].ToString();
            }
            catch
            {
                Response.Redirect("../Affiliate/Login");
            }
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            code.Logs(conn, "Affiliate-View",DropDownList1.SelectedItem.Text, TextBox1.Text);

            string couponCode = TextBox4.Text;
            string baseQuery = "SELECT Affiliate_Reservation.ID,StayDate,AccomName,PriceAfterDiscount as PricePerDay,Commission " +
                "FROM [Taketime].[dbo].[Affiliate_Reservation] " +
                "inner join Accommodation on Accommodation.ID = Accommodation_ID " +
                "inner join Affiliate_Discount_RatePlan on Affiliate_Discount_RatePlan.ID = Affiliate_Discount_RatePlan_ID " +
                "inner join Reservation on Reservation.ID = Reservation_ID ";

            string query = "";
            var queryParams = new Dictionary<string, object> { { "@CouponCode", couponCode } };

            GridView1.Columns[0].Visible = false;
            GridView1.Columns[1].Visible = false;
            GridView1.Columns[2].Visible = false;

            if (DropDownList1.SelectedValue == "1")
            {
                query = baseQuery + "Where Reservation.Status = N'มัดจำแล้ว' AND Affiliate_Reservation.Status = 'NEW' AND Affiliate_Member_Coupon_Code = @CouponCode order by StayDate desc";
            }
            else if (DropDownList1.SelectedValue == "2")
            {
                query = baseQuery + "Where Reservation.Status = N'เช็คอินแล้ว' AND Affiliate_Reservation.Status = 'NEW' AND Affiliate_Member_Coupon_Code = @CouponCode order by StayDate desc";
            }
            else if (DropDownList1.SelectedValue == "3")
            {
                query = baseQuery + "Where Reservation.Status = N'เช็คอินแล้ว' AND Affiliate_Reservation.Status = 'TRANSFERED' AND Affiliate_Member_Coupon_Code = @CouponCode order by StayDate desc";
            }
            else if (DropDownList1.SelectedValue == "4")
            {
                query = baseQuery + "Where (Reservation.Status = N'ยกเลิกคืนเงิน' OR Reservation.Status = N'ยกเลิกไม่คืนเงิน') AND Affiliate_Reservation.Status = 'NEW' AND Affiliate_Member_Coupon_Code = @CouponCode order by StayDate desc";
            }
            else if (DropDownList1.SelectedValue == "5")
            {
                query = "SELECT distinct(Account_Payment_ID),Created_Date as TransferedDate,Total_Amount,Vat,Total_Amount_Exclude_Vat " +
                    "FROM [Taketime].[dbo].[Affiliate_Reservation_Payment] " +
                    "inner join Account_Payment on Account_Payment.ID = Account_Payment_ID " +
                    "inner join Affiliate_Reservation on Affiliate_Reservation.ID = Affiliate_Reservation_ID " +
                    "Where Affiliate_Member_Coupon_Code = @CouponCode AND Affiliate_Reservation.Status = 'TRANSFERED'";
                GridView1.Columns[0].Visible = true;
                GridView1.Columns[1].Visible = true;
                GridView1.Columns[2].Visible = true;
            }

            // Store selected filter index instead of raw SQL in session
            Session["AffiliateFilterIndex"] = DropDownList1.SelectedValue;
            Session["AffiliateCouponCode"] = couponCode;

            try
            {
                if (!string.IsNullOrEmpty(query))
                {
                    DataTable dt = code.DatabaseQuerySafe(conn, query, queryParams);
                    GridView1.DataSource = dt;
                    GridView1.DataBind();
                }
            }
            catch { }
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            // Re-execute query using stored filter parameters instead of raw SQL from session
            string couponCode = Session["AffiliateCouponCode"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(couponCode)) return;

            var queryParams = new Dictionary<string, object> { { "@CouponCode", couponCode } };
            string query = "SELECT Affiliate_Reservation.ID,StayDate,AccomName,PriceAfterDiscount as PricePerDay,Commission,StayDays,IncentivePercent " +
                "FROM [Taketime].[dbo].[Affiliate_Reservation] " +
                "inner join Accommodation on Accommodation.ID = Accommodation_ID " +
                "inner join Affiliate_Discount_RatePlan on Affiliate_Discount_RatePlan.ID = Affiliate_Discount_RatePlan_ID " +
                "inner join Reservation on Reservation.ID = Reservation_ID " +
                "Where Affiliate_Member_Coupon_Code = @CouponCode order by StayDate desc";

            DataTable dt = code.DatabaseQuerySafe(conn, query, queryParams);
            dt.Columns.Add("Incentive");
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                double stay = Convert.ToDouble(dt.Rows[i]["StayDays"].ToString());
                double incentivepercent = Convert.ToDouble(dt.Rows[i]["IncentivePercent"].ToString());
                double price = Convert.ToDouble(dt.Rows[i]["PricePerDay"].ToString());
                dt.Rows[i]["Incentive"] = ((price * stay) * incentivepercent) / 100;
            }
            GridView1.DataSource = dt;
            GridView1.DataBind();
        }

        protected void Button4_Click(object sender, EventArgs e)
        {
            Session["AffiliateFilterIndex"] = "";
            Session["AffiliateCouponCode"] = "";
            GridView1.DataSource = null;
            GridView1.DataBind();
        }

        protected void GridView1_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            // Re-execute the current filter query for paging
            Button2_Click(sender, e);
            GridView1.PageIndex = e.NewPageIndex;
            GridView1.DataBind();
        }

        protected void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if(e.CommandName == "REC")
            {
                Response.Redirect("/Account/Report/" + GridView1.Rows[Convert.ToInt32(e.CommandArgument)].Cells[3].Text+".pdf");
            }
            else if (e.CommandName == "Slip")
            {
                Response.Redirect("/Account/Report/" + GridView1.Rows[Convert.ToInt32(e.CommandArgument)].Cells[3].Text+"_Slip.pdf");
            }
            else if (e.CommandName == "TAX")
            {
                Response.Redirect("/Account/Report/" + GridView1.Rows[Convert.ToInt32(e.CommandArgument)].Cells[3].Text+"_TAX.pdf");
            }

        }
    }
}
