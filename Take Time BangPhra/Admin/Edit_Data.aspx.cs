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
                        // SECURE: Supplier lookup with parameterized query
                        var supplierParams = new Dictionary<string, object>
                        {
                            { "@SupplierID", supplierid }
                        };

                        DataTable dtt = code.DatabaseQuerySafe(conn,
                            "SELECT [Name] FROM Supplier WHERE [Status] = 'True' AND ID = @SupplierID",
                            supplierParams);

                        // SECURE: Supplier contact lookup with parameterized query
                        var contactParams = new Dictionary<string, object>
                        {
                            { "@SupplierID", supplierid }
                        };

                        dt = code.DatabaseQuerySafe(conn,
                            "SELECT * FROM Supplier_Contact WHERE [Status] = 'True' AND SupplierID = @SupplierID",
                            contactParams);

                        readData();
                        string name = dtt.Rows[0][0].ToString();
                        Label1.Text = "Editing Data of " + data + " that Supplier is " + name;
                    }
                    else if (data == "Supplier" && supplier_type.Length > 0)
                    {
                        // SECURE: Supplier type lookup with parameterized query
                        var typeParams = new Dictionary<string, object>
                        {
                            { "@TypeID", supplier_type }
                        };

                        DataTable dtt = code.DatabaseQuerySafe(conn,
                            "SELECT [Type] FROM Supplier_Type WHERE [Status] = 'True' AND ID = @TypeID",
                            typeParams);

                        // SECURE: Supplier by type lookup with parameterized query
                        var supplierByTypeParams = new Dictionary<string, object>
                        {
                            { "@TypeID", supplier_type }
                        };

                        dt = code.DatabaseQuerySafe(conn,
                            "SELECT * FROM Supplier WHERE [Status] = 'True' AND TypeID = @TypeID",
                            supplierByTypeParams);

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

                        // SECURE: Whitelist validation for table name (cannot parameterize table names)
                        string[] allowedTables = { "Accommodation", "Accommodation_HolidayPrice", "Customer", "Items", "Admin",
                            "Business_Info", "Account_Paid_How", "Account_Paid_Type", "Account_Vat_Type", "Vendor",
                            "Accommodation_DayType", "Accommodation_RatePlan", "Accommodation_Holiday",
                            "Affiliate_Member", "Affiliate_Reservation", "Affiliate_Discount", "Affiliate_Discount_RatePlan",
                            "Product", "Voucher", "Voucher_RatePlan_Group", "MapDataWithSTAAH",
                            "Supplier", "Supplier_Type", "Supplier_Contact", "Accommodation_Type", "Account_ProductType" };
                        if (!allowedTables.Contains(data))
                        {
                            Response.Redirect("/Default");
                            return;
                        }

                        dt = code.DatabaseQuery(conn, "SELECT * FROM [" + data + "] WHERE [Status] = 'True'");
                        readData();
                        Label1.Text = "Editing Data of " + data;
                    }
                    else if (1 == 1)
                    {
                        GridView2.Visible = false;

                        // SECURE: Whitelist validation for table name (cannot parameterize table names)
                        string[] allowedTables = { "Accommodation", "Accommodation_HolidayPrice", "Customer", "Items", "Admin",
                            "Business_Info", "Account_Paid_How", "Account_Paid_Type", "Account_Vat_Type", "Vendor",
                            "Accommodation_DayType", "Accommodation_RatePlan", "Accommodation_Holiday",
                            "Affiliate_Member", "Affiliate_Reservation", "Affiliate_Discount", "Affiliate_Discount_RatePlan",
                            "Product", "Voucher", "Voucher_RatePlan_Group", "MapDataWithSTAAH",
                            "Supplier", "Supplier_Type", "Supplier_Contact", "Accommodation_Type", "Account_ProductType" };
                        if (!allowedTables.Contains(data))
                        {
                            Response.Redirect("/Default");
                            return;
                        }

                        dt = code.DatabaseQuery(conn, "SELECT * FROM [" + data + "] WHERE [Status] = 'True'");
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
                // SECURE: Supplier lookup with parameterized query
                var supplierParams = new Dictionary<string, object>
                {
                    { "@SupplierID", supplierid }
                };
                DataTable dtt = code.DatabaseQuerySafe(conn,
                    "SELECT [Name] FROM Supplier WHERE [Status] = 'True' AND ID = @SupplierID",
                    supplierParams);

                // SECURE: Supplier contact lookup with parameterized query
                var contactParams = new Dictionary<string, object>
                {
                    { "@SupplierID", supplierid }
                };
                dt = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Supplier_Contact WHERE [Status] = 'True' AND SupplierID = @SupplierID",
                    contactParams);

                string name = dtt.Rows[0][0].ToString();
                Label1.Text = "Editing Data of " + data + " that Supplier is " + name;
            }
            else if (data == "Supplier" && supplier_type.Length > 0)
            {
                // SECURE: Supplier type lookup with parameterized query
                var typeParams = new Dictionary<string, object>
                {
                    { "@TypeID", supplier_type }
                };
                DataTable dtt = code.DatabaseQuerySafe(conn,
                    "SELECT [Type] FROM Supplier_Type WHERE [Status] = 'True' AND ID = @TypeID",
                    typeParams);

                // SECURE: Supplier by type lookup with parameterized query
                var supplierByTypeParams = new Dictionary<string, object>
                {
                    { "@TypeID", supplier_type }
                };
                dt = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Supplier WHERE [Status] = 'True' AND TypeID = @TypeID",
                    supplierByTypeParams);

                string name = dtt.Rows[0][0].ToString();
                Label1.Text = "Editing Data of " + data + " that Type is " + name;
            }
            else if (1 == 1)
            {
                // SECURE: Whitelist validation for table name (cannot parameterize table names)
                string[] allowedTables = { "Accommodation", "Accommodation_HolidayPrice", "Customer", "Items", "Admin",
                    "Business_Info", "Account_Paid_How", "Account_Paid_Type", "Account_Vat_Type", "Vendor",
                    "Accommodation_DayType", "Accommodation_RatePlan", "Accommodation_Holiday",
                    "Affiliate_Member", "Affiliate_Reservation", "Affiliate_Discount", "Affiliate_Discount_RatePlan",
                    "Product", "Voucher", "Voucher_RatePlan_Group", "MapDataWithSTAAH",
                    "Supplier", "Supplier_Type", "Supplier_Contact", "Accommodation_Type", "Account_ProductType" };
                if (!allowedTables.Contains(data))
                {
                    Response.Redirect("/Default");
                    return;
                }

                dt = code.DatabaseQuery(conn, "SELECT * FROM [" + data + "] WHERE [Status] = 'True'");
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
            // SECURE: Whitelist validation for table name
            string[] allowedTables = { "Accommodation", "Accommodation_HolidayPrice", "Customer", "Items", "Admin",
                "Business_Info", "Account_Paid_How", "Account_Paid_Type", "Account_Vat_Type", "Vendor",
                "Accommodation_DayType", "Accommodation_RatePlan", "Accommodation_Holiday",
                "Affiliate_Member", "Affiliate_Reservation", "Affiliate_Discount", "Affiliate_Discount_RatePlan",
                "Product", "Voucher", "Voucher_RatePlan_Group", "MapDataWithSTAAH",
                "Supplier", "Supplier_Type", "Supplier_Contact", "Accommodation_Type", "Account_ProductType" };
            if (!allowedTables.Contains(data))
            {
                Response.Redirect("/Default");
                return;
            }

            GridViewRow row = GridView1.Rows[e.RowIndex];
            string cmd = "UPDATE [" + data + "] SET ";
            var updateParams = new Dictionary<string, object>();
            var setClauses = new List<string>();

            for (int i = 0; i < dtshow.Columns.Count; i++)
            {
                if(dtshow.Columns[i].ColumnName == "ID")
                {
                    // Skip ID column
                }
                else
                {
                    string paramName = "@Col" + i;
                    string value;

                    try
                    {
                        value = ((TextBox)(row.Cells[2 + i].Controls[0])).Text;
                    }
                    catch
                    {
                        value = ((CheckBox)(row.Cells[2 + i].Controls[0])).Checked.ToString();
                    }

                    setClauses.Add("[" + dtshow.Columns[i].ColumnName + "] = " + paramName);
                    updateParams.Add(paramName, value);
                }
            }

            cmd += string.Join(", ", setClauses);
            cmd += " WHERE ID = @ID";
            updateParams.Add("@ID", Convert.ToInt64(dt.Rows[e.RowIndex]["ID"].ToString()));

            try
            {
                code.DatabaseInsertSafe(conn, cmd, updateParams);
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
            // SECURE: Whitelist validation for table name
            string[] allowedTables = { "Accommodation", "Accommodation_HolidayPrice", "Customer", "Items", "Admin",
                "Business_Info", "Account_Paid_How", "Account_Paid_Type", "Account_Vat_Type", "Vendor",
                "Accommodation_DayType", "Accommodation_RatePlan", "Accommodation_Holiday",
                "Affiliate_Member", "Affiliate_Reservation", "Affiliate_Discount", "Affiliate_Discount_RatePlan",
                "Product", "Voucher", "Voucher_RatePlan_Group", "MapDataWithSTAAH",
                "Supplier", "Supplier_Type", "Supplier_Contact", "Accommodation_Type", "Account_ProductType" };
            if (!allowedTables.Contains(data))
            {
                Response.Redirect("/Default");
                return;
            }

            string logde = "";
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                logde += dt.Rows[e.RowIndex][i].ToString() + ";";
            }

            // SECURE: Parameterized query for soft delete
            var deleteParams = new Dictionary<string, object>
            {
                { "@ID", Convert.ToInt64(dt.Rows[e.RowIndex]["ID"].ToString()) }
            };
            code.DatabaseInsertSafe(conn,
                "UPDATE [" + data + "] SET [Status] = 'False' WHERE ID = @ID",
                deleteParams);

            readData();
        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            // SECURE: Whitelist validation for table name
            string[] allowedTables = { "Accommodation", "Accommodation_HolidayPrice", "Customer", "Items", "Admin",
                "Business_Info", "Account_Paid_How", "Account_Paid_Type", "Account_Vat_Type", "Vendor",
                "Accommodation_DayType", "Accommodation_RatePlan", "Accommodation_Holiday",
                "Affiliate_Member", "Affiliate_Reservation", "Affiliate_Discount", "Affiliate_Discount_RatePlan",
                "Product", "Voucher", "Voucher_RatePlan_Group", "MapDataWithSTAAH",
                "Supplier", "Supplier_Type", "Supplier_Contact", "Accommodation_Type", "Account_ProductType" };
            if (!allowedTables.Contains(data))
            {
                Response.Redirect("/Default");
                return;
            }

            DataTable dtupdate = dt;
            try
            {
                dtupdate.Columns.Remove("ID");
                dtupdate.Columns.Remove("Status");
            }
            catch { }

            string cmd = "INSERT INTO [" + data + "] (";
            var insertParams = new Dictionary<string, object>();
            var columnNames = new List<string>();
            var valueParams = new List<string>();

            for (int i = 0; i < dtupdate.Columns.Count; i++)
            {
                columnNames.Add("[" + dtupdate.Columns[i].ColumnName + "]");
                string paramName = "@Col" + i;

                if (dtupdate.Columns[i].ColumnName == "TypeID")
                {
                    insertParams.Add(paramName, supplier_type);
                }
                else if (dtupdate.Columns[i].ColumnName == "SupplierID")
                {
                    insertParams.Add(paramName, supplierid);
                }
                else
                {
                    insertParams.Add(paramName, "");
                }

                valueParams.Add(paramName);
            }

            cmd += string.Join(", ", columnNames);
            cmd += ") VALUES (";
            cmd += string.Join(", ", valueParams);
            cmd += ")";

            try
            {
                code.DatabaseInsertSafe(conn, cmd, insertParams);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            readData();
        }
    }
}