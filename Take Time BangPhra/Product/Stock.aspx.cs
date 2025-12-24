using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using System.IO;
using Microsoft.Ajax.Utilities;
using Take_Time_BangPhra.Account.Report;
using System.Security.Cryptography;
using ECertificateAPI;
using System.Net.Mail;
using Microsoft.Reporting.WebForms;
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.Product
{
    public partial class Stock : System.Web.UI.Page
    {
        _Default codeDefault = new _Default();
        Receipt codeReceipt = new Receipt();
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        AddressHelper addressHelper;
        protected void Page_Load(object sender, EventArgs e)
        {
            addressHelper = new AddressHelper(conn);
            try
            {
                if (Session["permission"].ToString() == "True" && (Session["User"].ToString() == "Owner" || Session["User"].ToString() == "Admin"))
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

            try
            {
                DataTable dtIn = code.DatabaseQuery(conn, "SELECT Product.ID,Barcode,Category_ID,Product_Name,SUM(Amount) as Amount FROM [Taketime].[dbo].[Product] right join Product_In on Product.ID = Product_In.Product_ID group by Product.ID,Barcode,Category_ID,Product_Name order by Category_ID,Product_Name asc");
                DataTable dtOut = code.DatabaseQuery(conn, "SELECT Product.ID,Barcode,Category_ID,Product_Name,SUM(Amount) as Amount FROM [Taketime].[dbo].[Product] right join Product_Out on Product.ID = Product_Out.Product_ID group by Product.ID,Barcode,Category_ID,Product_Name order by Category_ID,Product_Name asc");
                for(int i = 0;i<dtIn.Rows.Count;i++)
                {
                    for (int j = 0; j < dtOut.Rows.Count; j++)
                    {
                        if (dtIn.Rows[i]["ID"].ToString() == dtOut.Rows[j]["ID"].ToString())
                        {
                            dtIn.Rows[i]["Amount"] = Convert.ToInt32(dtIn.Rows[i]["Amount"].ToString()) - Convert.ToInt32(dtOut.Rows[j]["Amount"].ToString());
                        }
                    }
                }
                GridView2.DataSource = dtIn;
                GridView2.DataBind();

                // Load statistics
                LoadStatistics(dtIn);

                // Load predicted low stock items (will run out in 3-4 weeks based on usage rate)
                LoadPredictedLowStock();

                if (!IsPostBack)
                {
                    DataTable dtOrder = new DataTable();
                    try
                    {
                        dtOrder.Columns.Add("ID");
                        dtOrder.Columns.Add("Barcode");
                        dtOrder.Columns.Add("Product_Name");
                        dtOrder.Columns.Add("Amount");
                        dtOrder.Columns.Add("Sell_Price");
                        dtOrder.Columns.Add("Price_Total");
                        dtOrder.Columns.Add("Category_ID");
                        dtOrder.Columns.Add("Remark");
                        Session["dtOrder"] = dtOrder;
                    }
                    catch { }
                    string yourHTMLstring = "<script> var Material_Name = [";
                    DataTable dt = code.DatabaseQuery(conn, "SELECT Distinct(Product_Name) as Material_Name FROM [Taketime].[dbo].[Product] Where [Status] = 'True'");
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        yourHTMLstring += "\"" + dt.Rows[i][0].ToString().Replace(",", "") + "\"";
                        if (i < dt.Rows.Count - 1)
                        {
                            yourHTMLstring += ",";
                        }
                    }
                    yourHTMLstring += "];\r\nautocomplete(document.getElementById(\"MainContent_TextBox1\"), Material_Name);</script>";
                    Literal1.Text = yourHTMLstring;
                }
            }
            catch (Exception ex) { ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('" + ex + "');", true); }


        }

        protected void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "Add")
            {
                DataTable dtOrder = (DataTable)Session["dtOrder"];
                int amount = Convert.ToInt32(dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Amount"].ToString());
                amount = amount + 1;
                double total = amount * Convert.ToDouble(dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Sell_Price"].ToString());
                dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Amount"] = amount;
                dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Price_Total"] = total;
                
                GridView1.DataSource = dtOrder;
                GridView1.DataBind();
                Session["dtOrder"] = dtOrder;
                TextBox1.Text = string.Empty;
                double pricetotal = 0;
                for (int i = 0; i < dtOrder.Rows.Count; i++)
                {
                    pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
                }
                
            }
            else if (e.CommandName == "Reduce")
            {
                DataTable dtOrder = (DataTable)Session["dtOrder"];
                int amount = Convert.ToInt32(dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Amount"].ToString());
                if (amount > 1)
                {
                    amount = amount - 1;
                    double total = amount * Convert.ToDouble(dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Sell_Price"].ToString());
                    dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Amount"] = amount;
                    dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Price_Total"] = total;
                    
                    GridView1.DataSource = dtOrder;
                    GridView1.DataBind();
                    Session["dtOrder"] = dtOrder;
                    TextBox1.Text = string.Empty;
                    double pricetotal = 0;
                    for (int i = 0; i < dtOrder.Rows.Count; i++)
                    {
                        pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
                    }
                    
                }
            }
            else if (e.CommandName == "DeleteItem")
            {
                DataTable dtOrder = (DataTable)Session["dtOrder"];
                dtOrder.Rows[Convert.ToInt32(e.CommandArgument)].Delete();
                dtOrder.AcceptChanges();
                GridView1.DataSource = dtOrder;
                GridView1.DataBind();
                Session["dtOrder"] = dtOrder;
                TextBox1.Text = string.Empty;
                double pricetotal = 0;
                for (int i = 0; i < dtOrder.Rows.Count; i++)
                {
                    pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
                }
                
            }
        }
        protected void GridView1_RowEditing(object sender, GridViewEditEventArgs e)
        {
            GridView1.EditIndex = e.NewEditIndex;
            DataTable dtOrder = (DataTable)Session["dtOrder"];
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
        }

        protected void GridView1_RowCancelingEdit(object sender, GridViewCancelEditEventArgs e)
        {
            GridView1.EditIndex = -1;
            DataTable dtOrder = (DataTable)Session["dtOrder"];
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
        }

        protected void GridView1_RowUpdating(object sender, GridViewUpdateEventArgs e)
        {
            DataTable dtOrder = (DataTable)Session["dtOrder"];
            TextBox txtAmount = (TextBox)GridView1.Rows[e.RowIndex].Cells[2].Controls[0];
            TextBox txtPrice = (TextBox)GridView1.Rows[e.RowIndex].Cells[3].Controls[0];
            GridView1.EditIndex = -1;
            dtOrder.Rows[Convert.ToInt32(e.RowIndex)]["Amount"] = txtAmount.Text;
            dtOrder.Rows[Convert.ToInt32(e.RowIndex)]["Sell_Price"] = txtPrice.Text;
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
            Session["dtOrder"] = dtOrder;
            TextBox1.Text = string.Empty;
            double pricetotal = 0;
            for (int i = 0; i < dtOrder.Rows.Count; i++)
            {
                pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
            }
            
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            // SECURE: Product lookup with parameterized query
            var productParams = new Dictionary<string, object>
            {
                { "@ProductName", TextBox1.Text ?? "" },
                { "@Barcode", TextBox1.Text ?? "" }
            };

            DataTable dtProduct = code.DatabaseQuerySafe(conn,
                "SELECT * FROM [Taketime].[dbo].[Product] " +
                "WHERE [Product_Name] = @ProductName OR Barcode = @Barcode",
                productParams);
            if (dtProduct.Rows.Count > 0)
            {
                DataTable dtOrder = (DataTable)Session["dtOrder"];
                if (dtOrder.Rows.Count == 0)
                {
                    int amount = Convert.ToInt32(TextBox2.Text);
                    double total = amount * Convert.ToDouble(TextBox3.Text);
                    dtOrder.Rows.Add(dtProduct.Rows[0]["ID"].ToString(), dtProduct.Rows[0]["Barcode"].ToString(), dtProduct.Rows[0]["Product_Name"].ToString(), amount, TextBox3.Text, total, dtProduct.Rows[0]["Category_ID"].ToString(),TextBox4.Text);

                }
                else
                {
                    int rowid = 0;
                    int checkdup = 0;
                    for (int i = 0; i < dtOrder.Rows.Count; i++)
                    {
                        if (dtOrder.Rows[i]["Product_Name"].ToString() == dtProduct.Rows[0]["Product_Name"].ToString())
                        {
                            checkdup = 1;
                            rowid = i;
                        }

                    }
                    if (checkdup == 0)
                    {
                        int amount = Convert.ToInt32(TextBox2.Text);
                        double total = amount * Convert.ToDouble(TextBox3.Text);
                        dtOrder.Rows.Add(dtProduct.Rows[0]["ID"].ToString(), dtProduct.Rows[0]["Barcode"].ToString(), dtProduct.Rows[0]["Product_Name"].ToString(), amount, TextBox3.Text, total, dtProduct.Rows[0]["Category_ID"].ToString(),TextBox4.Text);

                    }
                    else
                    {
                        int amount = Convert.ToInt32(dtOrder.Rows[rowid]["Amount"].ToString());
                        amount = amount + Convert.ToInt32(TextBox2.Text);
                        double total = amount * Convert.ToDouble(TextBox3.Text);
                        dtOrder.Rows[rowid]["Amount"] = amount;
                        dtOrder.Rows[rowid]["Price_Total"] = total;
                    }
                }
                GridView1.DataSource = dtOrder;
                GridView1.DataBind();
                Session["dtOrder"] = dtOrder;
                TextBox1.Text = string.Empty;
                double pricetotal = 0;
                for (int i = 0; i < dtOrder.Rows.Count; i++)
                {
                    pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
                }

            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ไม่มีรายชื่อสินค้าดังกล่าว');", true);
            }
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            DataTable dtOrder = (DataTable)Session["dtOrder"];
            if(dtOrder.Rows.Count > 0)
            {
                // SECURE: INSERT Product_Out with parameterized query
                for(int i = 0;i<dtOrder.Rows.Count;i++)
                {
                    var productOutParams = new Dictionary<string, object>
                    {
                        { "@DateTimeOut", DateTime.Now },
                        { "@ProductID", dtOrder.Rows[i]["ID"].ToString() },
                        { "@Amount", dtOrder.Rows[i]["Amount"].ToString() },
                        { "@PricePerUnit", dtOrder.Rows[i]["Sell_Price"].ToString() },
                        { "@Remark", dtOrder.Rows[i]["Remark"].ToString() }
                    };

                    code.DatabaseInsertSafe(conn,
                        "INSERT INTO [dbo].[Product_Out] ([DateTime_Out],[Product_ID],[Amount],[PricePerUnit],[Account_Receipt_ID],[Remark]) " +
                        "VALUES (@DateTimeOut,@ProductID,@Amount,@PricePerUnit,'0',@Remark)",
                        productOutParams);
                }
                Response.Redirect("/Product/Stock");
            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ไม่มีข้อมูลนำเข้า');", true);
            }

        }

        /// <summary>
        /// Clear all items from cart
        /// </summary>
        protected void btnClear_Click(object sender, EventArgs e)
        {
            DataTable dtOrder = new DataTable();
            dtOrder.Columns.Add("ID");
            dtOrder.Columns.Add("Barcode");
            dtOrder.Columns.Add("Product_Name");
            dtOrder.Columns.Add("Amount");
            dtOrder.Columns.Add("Sell_Price");
            dtOrder.Columns.Add("Price_Total");
            dtOrder.Columns.Add("Category_ID");
            dtOrder.Columns.Add("Remark");
            Session["dtOrder"] = dtOrder;
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
            TextBox1.Text = string.Empty;
            TextBox2.Text = string.Empty;
            TextBox3.Text = "0";
            TextBox4.Text = string.Empty;
        }

        /// <summary>
        /// Load stock statistics into stat cards
        /// </summary>
        private void LoadStatistics(DataTable dtStock)
        {
            try
            {
                int totalProducts = dtStock.Rows.Count;
                int normalStock = 0;
                int lowStock = 0;
                int criticalStock = 0;

                foreach (DataRow row in dtStock.Rows)
                {
                    int amount = Convert.ToInt32(row["Amount"]);
                    if (amount < 5)
                    {
                        criticalStock++;
                    }
                    else
                    {
                        normalStock++;
                    }
                }

                // Get predicted low stock count
                string query = @"
                    SELECT COUNT(*) as LowCount FROM (
                        SELECT p.ID,
                            ISNULL((SELECT SUM(Amount) FROM Product_In WHERE Product_ID = p.ID), 0) -
                            ISNULL((SELECT SUM(Amount) FROM Product_Out WHERE Product_ID = p.ID), 0) as CurrentStock,
                            ISNULL((SELECT SUM(Amount) FROM Product_Out
                                    WHERE Product_ID = p.ID
                                    AND DateTime_Out >= DATEADD(WEEK, -8, GETDATE())), 0) / 8.0 as WeeklyUsage
                        FROM Product p WHERE p.Status = 'True'
                    ) AS StockAnalysis
                    WHERE CurrentStock > 0 AND WeeklyUsage > 0 AND (CurrentStock / WeeklyUsage) <= 4";

                DataTable dtLow = code.DatabaseQuery(conn, query);
                if (dtLow != null && dtLow.Rows.Count > 0)
                {
                    lowStock = Convert.ToInt32(dtLow.Rows[0]["LowCount"]);
                }

                lblTotalProducts.Text = totalProducts.ToString();
                lblNormalStock.Text = normalStock.ToString();
                lblLowStock.Text = lowStock.ToString();
                lblCriticalStock.Text = criticalStock.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadStatistics Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Load predicted low stock items based on usage rate
        /// Formula: WeeksRemaining = CurrentStock / WeeklyUsageRate
        /// Show items where WeeksRemaining <= 4 weeks
        /// </summary>
        private void LoadPredictedLowStock()
        {
            try
            {
                // Calculate based on average weekly usage over the last 8 weeks
                // Use subquery with WHERE instead of HAVING (HAVING requires GROUP BY)
                string query = @"
                    SELECT * FROM (
                        SELECT
                            p.Product_Name,
                            -- Current stock level
                            ISNULL((SELECT SUM(Amount) FROM Product_In WHERE Product_ID = p.ID), 0) -
                            ISNULL((SELECT SUM(Amount) FROM Product_Out WHERE Product_ID = p.ID), 0) as CurrentStock,
                            -- Average weekly usage (last 8 weeks)
                            ISNULL((SELECT SUM(Amount) FROM Product_Out
                                    WHERE Product_ID = p.ID
                                    AND DateTime_Out >= DATEADD(WEEK, -8, GETDATE())), 0) / 8.0 as WeeklyUsage,
                            -- Weeks remaining
                            CASE
                                WHEN ISNULL((SELECT SUM(Amount) FROM Product_Out
                                        WHERE Product_ID = p.ID
                                        AND DateTime_Out >= DATEADD(WEEK, -8, GETDATE())), 0) / 8.0 > 0
                                THEN (ISNULL((SELECT SUM(Amount) FROM Product_In WHERE Product_ID = p.ID), 0) -
                                      ISNULL((SELECT SUM(Amount) FROM Product_Out WHERE Product_ID = p.ID), 0)) /
                                     (ISNULL((SELECT SUM(Amount) FROM Product_Out
                                            WHERE Product_ID = p.ID
                                            AND DateTime_Out >= DATEADD(WEEK, -8, GETDATE())), 0) / 8.0)
                                ELSE 999
                            END as WeeksRemaining
                        FROM Product p
                        WHERE p.Status = 'True'
                    ) AS StockAnalysis
                    WHERE CurrentStock > 0
                      AND WeeklyUsage > 0
                      AND WeeksRemaining <= 4
                    ORDER BY WeeksRemaining ASC";

                DataTable dtPredicted = code.DatabaseQuery(conn, query);

                if (dtPredicted != null && dtPredicted.Rows.Count > 0)
                {
                    gvPredictedLowStock.DataSource = dtPredicted;
                    gvPredictedLowStock.DataBind();
                    pnlPredictedLowStock.Visible = true;
                }
                else
                {
                    pnlPredictedLowStock.Visible = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadPredictedLowStock Error: {ex.Message}");
                pnlPredictedLowStock.Visible = false;
            }
        }
    }
}