using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra
{
    public partial class SiteMaster2 : MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (HttpContext.Current.Request.Url.AbsolutePath.ToString().Contains("Default"))
            { }
            else
            {
                try
                {

                }
                catch
                {
                }
            }
        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            Session["permission"] = "False";
            Response.Redirect("/Default");
        }
    }
}