using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;

namespace Take_Time_BangPhra.Admin
{
    public partial class HolidayPrice : System.Web.UI.Page
    {
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.MgtReport)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            Page.MaintainScrollPositionOnPostBack = true;
            try
            {
                if (Session["permission"].ToString() == "True")
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
                DataTable dtAccom = code.DatabaseQuery(conn, "SELECT * FROM [Taketime].[dbo].[Accommodation] Where Status = 1 order by OrderID asc");
                GridView1.DataSource = dtAccom;
                GridView1.DataBind();

                // SECURE: Holiday price lookup with parameterized query
                var holidayPriceParams = new Dictionary<string, object>
                {
                    { "@Today", DateTime.Today }
                };

                DataTable dt = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Taketime].[dbo].[Accommodation_HolidayPrice] " +
                    "WHERE DateNewPrice >= @Today " +
                    "ORDER BY DateNewPrice ASC",
                    holidayPriceParams);

                GridView2.DataSource = dt;
                GridView2.DataBind();

                try
                {
                    DataTable dtSelectedDate = new DataTable();
                    dtSelectedDate.Columns.Add("Date");
                    Session["dtSelectedDate"] = dtSelectedDate;
                }
                catch
                {

                }
            }


        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            if (Panel1.Visible == false)
            {
                Panel1.Visible = true;
            }
            else
            {
                Panel1.Visible = false;
            }
        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            DataTable dtSelectedDate = (DataTable)Session["dtSelectedDate"];
            int check = 0;
            foreach (GridViewRow row in GridView1.Rows)
            {
                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                if (chk != null && chk.Checked)
                {
                    check++;
                }
            }

            if (Calendar1.SelectedDate > DateTime.Parse("1990-03-03") && check > 0)
            {
                foreach (GridViewRow row in GridView1.Rows)
                {
                    CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                    TextBox TxtPrice = (row.Cells[3].FindControl("TxtPrice") as TextBox);
                    TextBox TxtRemark = (row.Cells[4].FindControl("TxtRemark") as TextBox);
                    if (chk != null && chk.Checked && Convert.ToInt32(TxtPrice.Text) > 0)
                    {
                        for (int i = 0; i < dtSelectedDate.Rows.Count; i++)
                        {
                            // SECURE: Holiday price INSERT with parameterized query
                            var insertParams = new Dictionary<string, object>
                            {
                                { "@AccommodationID", row.Cells[1].Text },
                                { "@DateNewPrice", DateTime.Parse(dtSelectedDate.Rows[i][0].ToString()) },
                                { "@Price", TxtPrice.Text },
                                { "@Remark", TxtRemark.Text ?? "" }
                            };

                            code.DatabaseInsertSafe(conn,
                                "INSERT INTO [dbo].[Accommodation_HolidayPrice] ([Accommodation_ID],[DateNewPrice],[Price],[Remark]) " +
                                "VALUES (@AccommodationID,@DateNewPrice,@Price,@Remark)",
                                insertParams);
                        }
                    }
                }
                // Show success message then redirect
                ClientScript.RegisterStartupScript(this.GetType(), "success",
                    "alert('✅ บันทึกราคาวันหยุดเรียบร้อยแล้ว'); window.location.href='./HolidayPrice';", true);
            }
        }

        protected void GridView2_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {

            GridViewRow row = (GridViewRow)GridView2.Rows[e.RowIndex];

            // SECURE: Holiday price DELETE with parameterized query
            var deleteParams = new Dictionary<string, object>
            {
                { "@AccommodationID", row.Cells[0].Text },
                { "@DateNewPrice", row.Cells[1].Text },
                { "@Price", row.Cells[2].Text }
            };

            code.DatabaseInsertSafe(conn,
                "DELETE FROM [dbo].[Accommodation_HolidayPrice] " +
                "WHERE Accommodation_ID = @AccommodationID " +
                "AND DateNewPrice = @DateNewPrice " +
                "AND Price = @Price",
                deleteParams);

            DataTable dt = code.DatabaseQuery(conn, "SELECT * FROM [Taketime].[dbo].[Accommodation_HolidayPrice]");
            GridView2.DataSource = dt;
            GridView2.DataBind();
        }

        protected void Calendar1_SelectionChanged(object sender, EventArgs e)
        {
            DataTable dtSelectedDate = (DataTable)Session["dtSelectedDate"];
            if (dtSelectedDate.Rows.Count > 0)
            {
                int check = 0;
                int row = -1;
                for (int i = 0; i < dtSelectedDate.Rows.Count; i++)
                {
                    if (Calendar1.SelectedDate.ToString("yyyy-MM-dd") == DateTime.Parse(dtSelectedDate.Rows[i][0].ToString()).ToString("yyyy-MM-dd"))
                    {
                        check = 1;
                        row = i;

                    }
                    else
                    {

                    }

                }
                if (check == 0)
                {
                    dtSelectedDate.Rows.Add(Calendar1.SelectedDate.ToString("yyyy-MM-dd"));
                }
                else
                {
                    try
                    {
                        DataRow dr = dtSelectedDate.Rows[row];
                        dr.Delete();
                        dtSelectedDate.AcceptChanges();
                    }
                    catch { }
                }
            }
            else
            {
                dtSelectedDate.Rows.Add(Calendar1.SelectedDate.ToString("yyyy-MM-dd"));
            }
            Session["dtSelectedDate"] = dtSelectedDate;
        }

        protected void Calendar1_DayRender(object sender, DayRenderEventArgs e)
        {
            DataTable dtSelectedDate = (DataTable)Session["dtSelectedDate"];

            for (int i = 0; i < dtSelectedDate.Rows.Count; i++)
            {
                if (e.Day.Date.ToString("yyyy-MM-dd") == DateTime.Parse(dtSelectedDate.Rows[i][0].ToString()).ToString("yyyy-MM-dd"))
                {
                    //e.Cell.BackColor = System.Drawing.Color.Red;
                    e.Cell.BackColor = System.Drawing.Color.Gray;
                    e.Cell.Font.Bold = true;
                }

            }
        }
    }
}