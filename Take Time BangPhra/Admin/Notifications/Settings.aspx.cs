using System;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.Notifications
{
    public partial class Settings : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }
        }
    }
}
