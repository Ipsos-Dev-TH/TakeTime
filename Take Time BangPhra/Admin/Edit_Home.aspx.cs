using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin
{
    public partial class Edit_Home : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Owner only — เมนูเข้าหน้าแก้ไขข้อมูลดิบ (Admin/Business_Info/Customer)
            try
            {
                if (Session["permission"]?.ToString() != "True" || Session["User"]?.ToString() != "Owner")
                {
                    Response.Redirect("~/Admin/Login", false);
                    Context.ApplicationInstance.CompleteRequest();
                }
            }
            catch
            {
                Response.Redirect("~/Admin/Login", false);
                Context.ApplicationInstance.CompleteRequest();
            }
        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Accommodation");
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Accommodation_HolidayPrice");
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Customer");
        }

        protected void Button4_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Items");
        }

        protected void Button5_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Admin");
        }

        protected void Button6_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Business_Info");
        }

        protected void Button7_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Account_Paid_How");
        }

        protected void Button8_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Account_Paid_Type");
        }

        protected void Button9_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Account_Vat_Type");
        }

        protected void Button10_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Vendor");
        }

        protected void Button11_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Accommodation_RatePlan");
        }

        protected void Button12_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Accommodation_Holiday");
        }

        protected void Button13_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Affiliate_Discount");
        }

        protected void Button14_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Affiliate_Discount_RatePlan");
        }

        protected void Button15_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Product");
        }

        protected void Button16_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Affiliate_Member");
        }

        protected void Button17_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Affiliate_Reservation");
        }

        protected void Button18_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Accommodation_DayType");
            
        }

        protected void Button19_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Voucher");
        }

        protected void Button20_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=Voucher_RatePlan_Group");
        }

        protected void Button21_Click(object sender, EventArgs e)
        {
            Response.Redirect("./Edit_Data.aspx?data=MapDataWithSTAAH"); 
        }
    }
}