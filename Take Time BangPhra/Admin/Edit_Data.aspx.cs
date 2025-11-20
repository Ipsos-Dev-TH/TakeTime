using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;

namespace Take_Time_BangPhra.Admin
{
    public partial class Edit_Data : System.Web.UI.Page
    {
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        static DataTable dt = new DataTable();
        static DataTable dtshow = new DataTable();
        static string data = string.Empty;
        static string supplier_type = string.Empty;
        static string supplierid = string.Empty;
        static string Email = "";
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                if (Session["permission"].ToString() == "True" && Session["User"].ToString() == "Owner")
                {


                }
                else
                {
                    Response.Redirect("/Default");
                }


                data = Request.QueryString["data"];
                supplier_type = Request.QueryString["supplier_type"];
                supplierid = Request.QueryString["supplierid"];

                if (!IsPostBack)
                {

                    if (data == "Supplier_Contact" && supplierid.Length > 0)
                    {

                        DataTable dtt = code.DatabaseQuery(conn, "Select [Name] From Supplier Where [Status] = 'True' AND  ID = " + supplierid);
                        dt = code.DatabaseQuery(conn, "Select * From Supplier_Contact Where [Status] = 'True' AND  SupplierID = " + supplierid);
                        readData();
                        string name = dtt.Rows[0][0].ToString();
                        Label1.Text = "Editing Data of " + data + " that Supplier is " + name;
                    }
                    else if (data == "Supplier" && supplier_type.Length > 0)
                    {

                        DataTable dtt = code.DatabaseQuery(conn, "Select [Type] From Supplier_Type Where [Status] = 'True' AND ID = " + supplier_type);
                        dt = code.DatabaseQuery(conn, "Select * From Supplier Where [Status] = 'True' AND  TypeID = " + supplier_type);
                        readData();
                        string name = dtt.Rows[0][0].ToString();
                        Label1.Text = "Editing Data of " + data + " that Type is " + name;
                    }
                    else if (data == "Accommodation_RatePlan")
                    {
                        dt = code.DatabaseQuery(conn, "select * from Accommodation where [Status] = 'True'");
                        GridView2.Visible = true;
                        GridView2.DataSource = dt;
                        GridView2.DataBind();
                        dt = code.DatabaseQuery(conn, "select * from " + data + " where [Status] = 'True'");
                        readData();
                        Label1.Text = "Editing Data of " + data;
                    }
                    else if (1 == 1)
                    {
                        GridView2.Visible = false;
                        dt = code.DatabaseQuery(conn, "select * from " + data + " where [Status] = 'True'");
                        readData();
                        Label1.Text = "Editing Data of " + data;
                    }
                    
                    else
                    {
                        Response.Redirect("/Default");
                    }
                }
                else { }
            }
            catch
            {
                Response.Redirect("/Default");
            }
        }

        public void readData()
        {
            if (data == "Supplier_Contact" && supplierid.Length > 0)
            {

                DataTable dtt = code.DatabaseQuery(conn, "Select [Name] From Supplier Where [Status] = 'True' AND  ID = " + supplierid);
                dt = code.DatabaseQuery(conn, "Select * From Supplier_Contact Where [Status] = 'True' AND  SupplierID = " + supplierid);
                string name = dtt.Rows[0][0].ToString();
                Label1.Text = "Editing Data of " + data + " that Supplier is " + name;
            }
            else if (data == "Supplier" && supplier_type.Length > 0)
            {

                DataTable dtt = code.DatabaseQuery(conn, "Select [Type] From Supplier_Type Where [Status] = 'True' AND  ID = " + supplier_type);
                dt = code.DatabaseQuery(conn, "Select * From Supplier Where [Status] = 'True' AND  TypeID = " + supplier_type);
                string name = dtt.Rows[0][0].ToString();
                Label1.Text = "Editing Data of " + data + " that Type is " + name;
            }
            else if (1 == 1)
            {
                dt = code.DatabaseQuery(conn, "select * from " + data + " where [Status] = 'True'");
                Label1.Text = "Editing Data of " + data;
            }
            else
            {
                Response.Redirect("/Default");
            }

            dtshow = new DataTable();
            dtshow.Merge(dt);
            //for (int i = 0; i < dtshow.Columns.Count; i++)
            //{
            //    if (dtshow.Columns[i].ColumnName.ToLower() == "id")
            //    {
            //        dtshow.Columns.Remove(dtshow.Columns[i].ColumnName);
            //        i--;
            //    }
            //    else if (dtshow.Columns[i].ColumnName.ToLower() == "supplierid")
            //    {
            //        dtshow.Columns.Remove(dtshow.Columns[i].ColumnName);
            //        i--;
            //    }
            //    else if (dtshow.Columns[i].ColumnName.ToLower() == "typeid")
            //    {
            //        dtshow.Columns.Remove(dtshow.Columns[i].ColumnName);
            //        i--;
            //    }
            //    else if (dtshow.Columns[i].ColumnName.ToLower() == "status")
            //    {
            //        dtshow.Columns.Remove(dtshow.Columns[i].ColumnName);
            //        i--;
            //    }
            //}

            GridView1.DataSource = dtshow;
            GridView1.DataBind();
        }

        protected void GridView1_RowEditing(object sender, GridViewEditEventArgs e)
        {
            GridView1.EditIndex = e.NewEditIndex;
            GridView1.DataSource = dtshow;
            GridView1.DataBind();
        }

        protected void GridView1_RowCancelingEdit(object sender, GridViewCancelEditEventArgs e)
        {
            e.Cancel = true;
            GridView1.EditIndex = -1;
            GridView1.DataSource = dtshow;
            GridView1.DataBind();
        }

        protected void GridView1_RowUpdating(object sender, GridViewUpdateEventArgs e)
        {
            GridViewRow row = GridView1.Rows[e.RowIndex];
            string cmd = "UPDATE " + data + " SET ";

            for (int i = 0; i < dtshow.Columns.Count; i++)
            {
                if(dtshow.Columns[i].ColumnName == "ID")
                {

                }
                else
                {
                    try
                    {
                        cmd += dtshow.Columns[i].ColumnName + " = N'" + ((TextBox)(row.Cells[2 + i].Controls[0])).Text + "'";
                    }
                    catch
                    {
                        cmd += dtshow.Columns[i].ColumnName + " = N'" + ((CheckBox)(row.Cells[2 + i].Controls[0])).Checked + "'";
                    }

                    if (i != dtshow.Columns.Count - 1)
                    {
                        cmd += ", ";
                    }
                }
                    
            }
            cmd += " WHERE ID = " + Convert.ToInt64(dt.Rows[e.RowIndex]["ID"].ToString());

            try
            {
                code.DatabaseInsert(conn, cmd);


            }
            catch (Exception ex)
            {
                throw ex;
            }
            GridView1.EditIndex = -1;
            readData();
        }

        protected void GridView1_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            string logde = "";
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                logde += dt.Rows[e.RowIndex][i].ToString() + ";";
            }
            code.DatabaseInsert(conn, "UPDATE " + data + " SET [Status] = 'False' WHERE ID = " + Convert.ToInt64(dt.Rows[e.RowIndex]["ID"].ToString()));

            readData();
        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            DataTable dtupdate = dt;
            try
            {
                dtupdate.Columns.Remove("ID");
                dtupdate.Columns.Remove("Status");
            }
            catch { }
            string cmd = "INSERT INTO  " + data + " (";
            for (int i = 0; i < dtupdate.Columns.Count; i++)
            {
                cmd += "[" + dtupdate.Columns[i].ColumnName + "]";
                if (i != dtupdate.Columns.Count - 1)
                {
                    cmd += ", ";
                }
            }
            cmd += ") VALUES (";
            for (int i = 0; i < dtupdate.Columns.Count; i++)
            {
                if (dtupdate.Columns[i].ColumnName == "TypeID")
                {
                    cmd += supplier_type;
                }
                else if (dtupdate.Columns[i].ColumnName == "SupplierID")
                {
                    cmd += supplierid;
                }
                else
                {
                    cmd += "''";
                }
                if (i != dtupdate.Columns.Count - 1)
                {
                    cmd += ", ";
                }
            }
            cmd += ")";

            try
            {
                code.DatabaseInsert(conn, cmd);


            }
            catch (Exception ex)
            {
                throw ex;
            }
            readData();
        }
    }
}