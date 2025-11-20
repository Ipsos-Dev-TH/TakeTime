using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;


namespace Take_Time_BangPhra
{
    public partial class PostponeList : System.Web.UI.Page
    {
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        protected void Page_Load(object sender, EventArgs e)
        {
            Page.MaintainScrollPositionOnPostBack = true;
            try
            {
                if (Session["permission"].ToString() == "True")
                {

                    if (!IsPostBack)
                    {

                    }
                }
                else
                {
                    Response.Redirect("./Default");
                }
            }
            catch
            {
                Response.Redirect("./Default");
            }

            try
            {
                if (Session["permission"].ToString() == "True")
                {
                    DataTable dt = code.DatabaseQuery(conn, "SELECT * FROM [Taketime].[dbo].[Reservation] inner join Customer on Customer.MobilePhone = Reservation.Customer_MobilePhone Where (CheckinDate = '1990-01-01' OR CheckinDate = '0001-01-01') AND Reservation.Status = N'มัดจำแล้ว' order by Reservation.ID desc");
                    Session["dtShow"] = dt;
                    GridView1.DataSource = dt;
                    GridView1.DataBind();
                }
                else
                {
                    Response.Redirect("./Default");
                }
            }
            catch
            {
                Response.Redirect("./Default");
            }


        }

        protected void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "Edit")
            {
                DataTable dtShow = (DataTable)Session["dtShow"];
                string id = Request.QueryString["id"];
                string check = Request.QueryString["check"];
                Response.Redirect("./Reserve?command=edit&id=" + dtShow.Rows[Convert.ToInt32(e.CommandArgument)]["ID"].ToString() + "&check=" + dtShow.Rows[Convert.ToInt32(e.CommandArgument)]["Customer_MobilePhone"]);
            }
            if(e.CommandName == "Delete")
            {
                DataTable dtShow = (DataTable)Session["dtShow"];
                code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation] SET [Status] = N'ลบจากการเลื่อนวันเข้าพัก' WHERE ID = "+ dtShow.Rows[Convert.ToInt32(e.CommandArgument.ToString())]["ID"].ToString());
                Response.Redirect("/PostponeList");
            }
        }

        protected void DeleteButton_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            string id = btn.CommandArgument;

                DataTable dtShow = (DataTable)Session["dtShow"];
                code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation] SET [Status] = N'ลบจากการเลื่อนวันเข้าพัก' WHERE ID = " + id);
                Response.Redirect("/PostponeList");
            
        }

    }
}