using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using System.IO;
using System.Text;

namespace Take_Time_BangPhra.Account
{
    public partial class CheckDocument : System.Web.UI.Page
    {
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            this.MaintainScrollPositionOnPostBack = true;
            try
            {
                if (Session["permission"].ToString() == "True" && (Session["User"].ToString() == "Owner" || Session["User"].ToString() == "Admin"))
                {
                    if (!IsPostBack)
                    {
                        TextBox1.Text = DateTime.Now.ToString("yyyy-MM-dd");
                        TextBox2.Text = DateTime.Now.ToString("yyyy-MM-dd");

                        string thisyear = "";
                        string lastyear = "";

                        if (Convert.ToInt32(DateTime.Now.Year.ToString()) > 2500)
                        {
                            thisyear = (Convert.ToInt32(DateTime.Now.Year.ToString()) - 543).ToString();
                            lastyear = (Convert.ToInt32(DateTime.Now.AddYears(-1).Year.ToString()) - 543).ToString();
                        }
                        else
                        {
                            thisyear = (Convert.ToInt32(DateTime.Now.Year.ToString())).ToString();
                            lastyear = (Convert.ToInt32(DateTime.Now.AddYears(-1).Year.ToString())).ToString();
                        }

                        DropDownList4.Items.Insert(0, new ListItem(thisyear, thisyear));
                        DropDownList4.Items.Insert(1, new ListItem(lastyear, lastyear));
                        DropDownList4.DataBind();
                    }
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

        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            Label10.Text = "";
            Label11.Text = "";
            Label12.Text = "";
            Label13.Text = "";
            Label14.Text = "";
            Label15.Text = "";
            Label16.Text = "";
            Label17.Text = "";

            if (DropDownList1.SelectedValue == "P&L")
            {
                string cmd = "";
                if (DropDownList3.SelectedIndex > 0)
                {
                    cmd = "Select * From Account_Receipt Where Month(Created_Date) = " + DropDownList3.SelectedValue + " AND Year(Created_Date) = " + DropDownList4.SelectedValue + " AND Status like '" + DropDownList2.SelectedValue + "'";

                }
                else
                {
                    cmd = "Select * From Account_Receipt Where Created_Date >= '" + TextBox1.Text + "' AND Created_Date <= '" + TextBox2.Text + "' AND Status like '" + DropDownList2.SelectedValue + "'";

                }
                if (Session["User"].ToString() == "Admin")
                {
                    cmd += " AND Created_By_ID = " + Session["UserID"].ToString();
                }
                DataTable dtin = code.DatabaseQuery(conn, cmd);

                if (DropDownList3.SelectedIndex > 0)
                {
                    cmd = "Select * From Account_Payment  Where Month(Created_Date) = " + DropDownList3.SelectedValue + " AND Year(Created_Date) = " + DropDownList4.SelectedValue + " AND Status like '" + DropDownList2.SelectedValue + "'";

                }
                else
                {
                    cmd = "Select * From Account_Payment  Where Created_Date >= '" + TextBox1.Text + "' AND Created_Date <= '" + TextBox2.Text + "' AND Status like '" + DropDownList2.SelectedValue + "'";

                }
                if (Session["User"].ToString() == "Admin")
                {
                    cmd += " AND Created_By_ID = " + Session["UserID"].ToString();
                }
                DataTable dtout = code.DatabaseQuery(conn, cmd);

                double totalCash = 0;
                double total = 0;
                double totalmd = 0;
                double totalvat = 0;
                string colname = "";

                for (int i = 0; i < dtin.Rows.Count; i++)
                {
                    if (dtin.Rows[i]["Paid_Type"].ToString().Contains("สด"))
                    {
                        totalCash += Convert.ToDouble(dtin.Rows[i]["Total_Amount"].ToString());
                    }
                    if (dtin.Rows[i]["Paid_Type"].ToString().Contains("โอน"))
                    {
                        total += Convert.ToDouble(dtin.Rows[i]["Total_Amount"].ToString());
                    }
                    if (dtin.Rows[i]["Paid_Type"].ToString().Contains("กรรมการ"))
                    {
                        totalmd += Convert.ToDouble(dtin.Rows[i]["Total_Amount"].ToString());
                    }
                    totalvat += Convert.ToDouble(dtin.Rows[i]["Vat"].ToString());
                }
                Label10.Text = totalCash.ToString();
                Label11.Text = total.ToString();
                Label12.Text = totalvat.ToString();


                totalCash = 0;
                total = 0;
                double totalfrommd = 0;
                double totaltomd = 0;
                totalvat = 0;

                for (int i = 0; i < dtout.Rows.Count; i++)
                {
                    if (dtout.Rows[i]["Paid_How"].ToString().Contains("สด"))
                    {
                        totalCash += Convert.ToDouble(dtout.Rows[i]["Total_Amount"].ToString());
                    }
                    if (dtout.Rows[i]["Paid_How"].ToString().Contains("โอน"))
                    {
                        total += Convert.ToDouble(dtout.Rows[i]["Total_Amount"].ToString());
                    }
                    if (dtout.Rows[i]["Paid_How"].ToString().Contains("กรรมการ"))
                    {
                        totalfrommd += Convert.ToDouble(dtout.Rows[i]["Total_Amount"].ToString());
                    }
                    if (dtout.Rows[i]["Vendor_ID"].ToString() == "1")
                    {
                        totaltomd += Convert.ToDouble(dtout.Rows[i]["Total_Amount"].ToString());
                    }
                    totalvat += Convert.ToDouble(dtout.Rows[i]["Vat"].ToString());
                }
                Label14.Text = totalCash.ToString();
                Label15.Text = total.ToString();
                Label17.Text = totalvat.ToString();
                Label16.Text = totalfrommd.ToString();
                Label13.Text = totaltomd.ToString();

            }
            else
            {
                string cmd = "";
                string payment_cmd = "SELECT Account_Payment.ID,[Name],Created_Date,Total_Amount,Vat_Type_ID,Vat,Total_Amount_Exclude_Vat,Paid_How,Paid_Type,Account_Payment.Status,Created_By_ID FROM [Account_Payment] inner join Vendor on Vendor.ID = Vendor_ID";
                string receipt_cmd = "SELECT Account_Receipt.ID,Reservation_ID,Customer.FullName,Customer.Address,Customer.IDNumber,Customer_MobilePhone,Account_Receipt.Created_Date,Total_Amount,Vat,Total_Amount_Exclude_Vat,IsDeposit,UseDeposit,Paid_Type,Account_Receipt.[Status],Created_By_ID,Reservation.Remark,Reservation.NoNameinReceipt FROM [Account_Receipt] left join Reservation on Reservation.ID = Account_Receipt.Reservation_ID left join Customer on Customer.MobilePhone = Reservation.Customer_MobilePhone";
                string detail_payment_cmd = "SELECT * FROM [Account_Payment_Detail] inner join Account_Payment on Account_Payment.ID = Payment_ID inner join Vendor on Vendor.ID = Vendor_ID";
                string detail_receipt_cmd = "SELECT Account_Receipt.ID,Account_Receipt.Created_Date,FullName,Address,IDNumber,Product_Data,[Product_Amount],[Product_Unit],[Price_PerPeice],[Price_Amount],Paid_Type,Vat,Total_Amount FROM [Account_Receipt] inner join Account_Receipt_Detail on Account_Receipt_Detail.Receipt_ID = Account_Receipt.ID left join Reservation on Reservation.ID = Reservation_ID left join Customer on Customer.MobilePhone = Reservation.Customer_MobilePhone";

                if (DropDownList3.SelectedIndex > 0)
                {
                    if (DropDownList1.SelectedItem.Text == "ใบสำคัญจ่าย")
                    {
                        cmd = payment_cmd + " Where Month(Account_Payment.Created_Date) = " + DropDownList3.SelectedValue + " AND Year(Account_Payment.Created_Date) = " + DropDownList4.SelectedValue + " AND Account_Payment.Status like '" + DropDownList2.SelectedValue + "'";
                    }
                    else if (DropDownList1.SelectedItem.Text == "ใบเสร็จรับเงิน")
                    {
                        cmd = receipt_cmd + " Where Month(Account_Receipt.Created_Date) = " + DropDownList3.SelectedValue + " AND Year(Account_Receipt.Created_Date) = " + DropDownList4.SelectedValue + " AND Account_Receipt.Status like '" + DropDownList2.SelectedValue + "'";
                    }
                    else if (DropDownList1.SelectedItem.Text == "รายละเอียดใบสำคัญจ่าย")
                    {
                        cmd = detail_payment_cmd + " Where Month(Account_Payment.Created_Date) = " + DropDownList3.SelectedValue + " AND Year(Account_Payment.Created_Date) = " + DropDownList4.SelectedValue + " AND Account_Payment.Status like '" + DropDownList2.SelectedValue + "'";
                    }
                    else if (DropDownList1.SelectedItem.Text == "รายละเอียดใบเสร็จรับเงิน")
                    {
                        cmd = detail_receipt_cmd + " Where Month(Account_Receipt.Created_Date) = " + DropDownList3.SelectedValue + " AND Year(Account_Receipt.Created_Date) = " + DropDownList4.SelectedValue + " AND Account_Receipt.Status like '" + DropDownList2.SelectedValue + "'";
                    }
                    else { }
                }
                else
                {
                    if (DropDownList1.SelectedItem.Text == "ใบสำคัญจ่าย")
                    {
                        cmd = payment_cmd + " Where Account_Payment.Created_Date >= '" + TextBox1.Text + "' AND Account_Payment.Created_Date <= '" + TextBox2.Text + "' AND Account_Payment.Status like '" + DropDownList2.SelectedValue + "'";
                    }
                    else if (DropDownList1.SelectedItem.Text == "ใบเสร็จรับเงิน")
                    {
                        cmd = receipt_cmd + " Where Account_Receipt.Created_Date >= '" + TextBox1.Text + "' AND Account_Receipt.Created_Date <= '" + TextBox2.Text + "' AND Account_Receipt.Status like '" + DropDownList2.SelectedValue + "'";
                    }
                    else if (DropDownList1.SelectedItem.Text == "รายละเอียดใบสำคัญจ่าย")
                    {
                        cmd = detail_payment_cmd + " Where Account_Payment.Created_Date >= '" + TextBox1.Text + "' AND Account_Payment.Created_Date <= '" + TextBox2.Text + "' AND Account_Payment.Status like '" + DropDownList2.SelectedValue + "'";
                    }
                    else if (DropDownList1.SelectedItem.Text == "รายละเอียดใบเสร็จรับเงิน")
                    {
                        cmd = detail_receipt_cmd + " Where Account_Receipt.Created_Date >= '" + TextBox1.Text + "' AND Account_Receipt.Created_Date <= '" + TextBox2.Text + "' AND Account_Receipt.Status like '" + DropDownList2.SelectedValue + "'";
                    }
                    else { }


                }


                if (Session["User"].ToString() == "Admin" && (DropDownList1.SelectedIndex == 0 || DropDownList1.SelectedIndex == 2))
                {
                    cmd += " AND Vendor.Vendor_Group != N'01-พนักงานประจำ'";
                }
                cmd += " order by ID asc";
                DataTable dt = code.DatabaseQuery(conn, cmd);

                GridView1.DataSource = dt;
                GridView1.DataBind();

                Session["dtGrid"] = dt;

                double totalCash = 0;
                double total = 0;
                double totalmd = 0;
                double totalvat = 0;

                string colname = "";
                if (DropDownList1.SelectedValue == "Account_Payment" || DropDownList1.SelectedValue == "Detail_Account_Payment")
                {
                    colname = "Paid_How";
                }
                else if (DropDownList1.SelectedValue == "Account_Receipt" || DropDownList1.SelectedValue == "Detail_Account_Receipt")
                {
                    colname = "Paid_Type";
                }
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    if (dt.Rows[i][colname].ToString().Contains("สด"))
                    {
                        totalCash += Convert.ToDouble(dt.Rows[i]["Total_Amount"].ToString());
                    }
                    if (dt.Rows[i][colname].ToString().Contains("โอน"))
                    {
                        total += Convert.ToDouble(dt.Rows[i]["Total_Amount"].ToString());
                    }
                    if (dt.Rows[i][colname].ToString().Contains("กรรมการ"))
                    {
                        totalmd += Convert.ToDouble(dt.Rows[i]["Total_Amount"].ToString());
                    }
                    totalvat += Convert.ToDouble(dt.Rows[i]["Vat"].ToString());
                }
                Label10.Text = totalCash.ToString();
                Label11.Text = total.ToString();
                Label12.Text = totalvat.ToString();
                Label13.Text = totalmd.ToString();
                Label18.Text = dt.Rows.Count.ToString();
            }


        }


        protected void GridView1_RowDeleting1(object sender, GridViewDeleteEventArgs e)
        {
            if (CheckBox1.Checked == true)
            {
                string docNum = GridView1.Rows[e.RowIndex].Cells[3].Text;
                string docType = docNum.Remove(3, 9);
                string docYear = "20" + docNum.Remove(0, 3).Remove(2, 7);
                string docMonth = Convert.ToInt32(docNum.Remove(0, 5).Remove(2, 5)).ToString();
                if (docType.Length > 3)
                {
                    docType = docNum.Remove(3, 12);
                    docYear = "20" + docNum.Remove(0, 3).Remove(2, 10);
                    docMonth = Convert.ToInt32(docNum.Remove(0, 5).Remove(2, 8)).ToString();
                }
                if (docType == "REC")
                {
                    string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString() + "\\" + docYear + "\\" + docMonth;

                    // 🔧 FIX: Update Reservation.Deposit before deleting Payment_History
                    try
                    {
                        // Get payment amount and reservation ID from Payment_History
                        var paymentData = code.DatabaseQuery(conn,
                            "SELECT ph.PaymentAmount, ph.Reservation_ID " +
                            "FROM [dbo].[Payment_History] ph " +
                            "WHERE ph.Receipt_ID = '" + docNum + "'");

                        if (paymentData != null && paymentData.Rows.Count > 0)
                        {
                            foreach (DataRow row in paymentData.Rows)
                            {
                                decimal amount = row["PaymentAmount"] != DBNull.Value ? Convert.ToDecimal(row["PaymentAmount"]) : 0;
                                int reservationId = row["Reservation_ID"] != DBNull.Value ? Convert.ToInt32(row["Reservation_ID"]) : 0;

                                if (amount > 0 && reservationId > 0)
                                {
                                    // Reduce Reservation.Deposit by payment amount
                                    code.DatabaseInsert(conn,
                                        $"UPDATE [dbo].[Reservation] SET Deposit = ISNULL(Deposit, 0) - {amount} WHERE ID = {reservationId}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Continue with deletion even if update fails (data consistency issue but prevents stuck state)
                        System.Diagnostics.Debug.WriteLine($"⚠️ Error updating Reservation.Deposit: {ex.Message}");
                    }

                    // Delete Payment_History records that reference this receipt
                    code.DatabaseInsert(conn, "DELETE FROM [dbo].[Payment_History] WHERE Receipt_ID = '" + docNum + "'");

                    // Delete receipt details
                    code.DatabaseInsert(conn, "DELETE FROM [dbo].[Account_Receipt_Detail] WHERE Receipt_ID = '" + docNum + "'");

                    // Delete receipt record
                    code.DatabaseInsert(conn, "DELETE FROM [dbo].[Account_Receipt] WHERE ID = '" + docNum + "'");

                    // Delete receipt files
                    string[] dirs = Directory.GetFiles(path, docNum + "*");
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        File.Delete(dirs[i].ToString());
                    }

                }
                else if (docType == "PAY")
                {
                    string path = System.Configuration.ConfigurationSettings.AppSettings["PaymentFolderPath"].ToString() + "\\" + docYear + "\\" + docMonth;
                    code.DatabaseInsert(conn, "DELETE FROM [dbo].[Account_Payment] WHERE ID = '" + docNum + "'");
                    code.DatabaseInsert(conn, "DELETE FROM [dbo].[Account_Payment_Detail] WHERE Payment_ID = '" + docNum + "'");
                    string[] dirs = Directory.GetFiles(path, docNum + "*");
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        File.Delete(dirs[i].ToString());
                    }
                }

                Response.Redirect("/Account/CheckDocument");
            }
        }

        protected void GridView1_SelectedIndexChanging(object sender, GridViewSelectEventArgs e)
        {
            string docStatus = "";
            try
            {
                docStatus = GridView1.Rows[e.NewSelectedIndex].Cells[16].Text;
            }
            catch
            {
                try
                {
                    docStatus = GridView1.Rows[e.NewSelectedIndex].Cells[12].Text;
                }
                catch
                {

                }
            }
            string docNum = GridView1.Rows[e.NewSelectedIndex].Cells[3].Text;
            string docType = docNum.Remove(3, 9);

            string docYear = "20" + docNum.Remove(0, 3).Remove(2, 7);
            string docMonth = Convert.ToInt32(docNum.Remove(0, 5).Remove(2, 5)).ToString();


            if (docType.Length > 3)
            {
                docType = docNum.Remove(3, 12);
                docYear = "20" + docNum.Remove(0, 3).Remove(2, 10);
                docMonth = Convert.ToInt32(docNum.Remove(0, 5).Remove(2, 8)).ToString();
            }
            if (docType == "REC")
            {
                if (docStatus == "Cancel")
                {
                    string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
                    if (File.Exists(path + "\\" + docYear + "\\" + docMonth + "" + docNum + "_" + code.DatabaseQuery(conn, "SELECT [UID] FROM [Taketime].[dbo].[Account_Receipt] Where ID = '" + docNum + "'").Rows[0][0].ToString() + "_Cancel.pdf"))
                    {
                        Response.Redirect("/Documents/Receipt/" + docYear + "/" + docMonth + "/" + docNum + "_" + code.DatabaseQuery(conn, "SELECT [UID] FROM [Taketime].[dbo].[Account_Receipt] Where ID = '" + docNum + "'").Rows[0][0].ToString() + "_Cancel.pdf");
                    }
                    else
                    {
                        Response.Redirect("/Documents/Receipt/" + docYear + "/" + docMonth + "/" + docNum + "_Cancel.pdf");
                    }

                }
                else
                {
                    string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
                    if (File.Exists(path + "\\" + docYear + "\\" + docMonth + "\\" + docNum + "_" + code.DatabaseQuery(conn, "SELECT [UID] FROM [Taketime].[dbo].[Account_Receipt] Where ID = '" + docNum + "'").Rows[0][0].ToString() + ".pdf"))
                    {
                        Response.Redirect("/Documents/Receipt/" + docYear + "/" + docMonth + "/" + docNum + "_" + code.DatabaseQuery(conn, "SELECT [UID] FROM [Taketime].[dbo].[Account_Receipt] Where ID = '" + docNum + "'").Rows[0][0].ToString() + ".pdf");
                    }
                    else
                    {
                        Response.Redirect("/Documents/Receipt/" + docYear + "/" + docMonth + "/" + docNum + ".pdf");
                    }

                }


            }
            else if (docType == "PAY")
            {
                if (docStatus == "Cancel")
                {
                    string path = System.Configuration.ConfigurationSettings.AppSettings["PaymentFolderPath"].ToString();
                    if (File.Exists(path + "\\" + docYear + "\\" + docMonth + "\\" + docNum + "_" + code.DatabaseQuery(conn, "SELECT [UID] FROM [Taketime].[dbo].[Account_Payment] Where ID = '" + docNum + "'").Rows[0][0].ToString() + "_Cancel.pdf"))
                    {
                        Response.Redirect("/Documents/Payment/" + docYear + "/" + docMonth + "/" + docNum + "_" + code.DatabaseQuery(conn, "SELECT [UID] FROM [Taketime].[dbo].[Account_Payment] Where ID = '" + docNum + "'").Rows[0][0].ToString() + "_Cancel.pdf");
                    }
                    else
                    {
                        Response.Redirect("/Documents/Payment/" + docYear + "/" + docMonth + "/" + docNum + "_Cancel.pdf");
                    }

                }
                else
                {
                    string path = System.Configuration.ConfigurationSettings.AppSettings["PaymentFolderPath"].ToString();
                    if (File.Exists(path + "\\" + docYear + "\\" + docMonth + "\\" + docNum + "_" + code.DatabaseQuery(conn, "SELECT [UID] FROM [Taketime].[dbo].[Account_Payment] Where ID = '" + docNum + "'").Rows[0][0].ToString() + ".pdf"))
                    {
                        Response.Redirect("/Documents/Payment/" + docYear + "/" + docMonth + "/" + docNum + "_" + code.DatabaseQuery(conn, "SELECT [UID] FROM [Taketime].[dbo].[Account_Payment] Where ID = '" + docNum + "'").Rows[0][0].ToString() + ".pdf");
                    }
                    else
                    {
                        Response.Redirect("/Documents/Payment/" + docYear + "/" + docMonth + "/" + docNum + ".pdf");
                    }

                }


            }
        }

        protected void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            string docNum = GridView1.Rows[Convert.ToInt32(e.CommandArgument)].Cells[3].Text;
            string docType = docNum.Remove(3, 9);
            if (docType.Length > 3)
            {
                docType = docNum.Remove(3, 12);
            }

            if (e.CommandName == "edit")
            {
                if (docType == "REC")
                {
                    Response.Redirect("/Account/Receipt?command=edit&uid=" + code.DatabaseQuery(conn, "SELECT [UID] FROM [Taketime].[dbo].[Account_Receipt] Where ID = '" + docNum + "'").Rows[0][0].ToString());

                }
                else if (docType == "PAY")
                {
                    Response.Redirect("/Account/PaymentVoucher?command=edit&uid=" + code.DatabaseQuery(conn, "SELECT [UID] FROM [Taketime].[dbo].[Account_Payment] Where ID = '" + docNum + "'").Rows[0][0].ToString());
                }
            }
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            string ReportName = "Report";
            try
            {
                if (DropDownList1.SelectedValue.Length > 3)
                {
                    ReportName = DropDownList1.SelectedValue;
                }
            }
            catch
            {

            }
            DataTable dtGrid = (DataTable)Session["dtGrid"];
            Response.Clear();
            Response.Buffer = true;
            Response.AddHeader("content-disposition", "attachment;filename=" + ReportName + ".csv");
            Response.Charset = "utf-8";
            Response.ContentType = "application/text";
            StringBuilder sBuilder = new System.Text.StringBuilder();


            for (int index = 0; index < dtGrid.Columns.Count; index++)
            {
                sBuilder.Append(dtGrid.Columns[index].ColumnName + '\t');
            }
            sBuilder.Append("\r\n");
            for (int i = 0; i < dtGrid.Rows.Count; i++)
            {
                for (int k = 0; k < dtGrid.Columns.Count; k++)
                {
                    sBuilder.Append(dtGrid.Rows[i][k].ToString().Replace(",", "").Replace("&nbsp;", "").Replace("&amp;", "") + "\t");
                }
                sBuilder.Append("\r\n");
            }


            Response.ContentEncoding = Encoding.Unicode;
            Response.BinaryWrite(Encoding.Unicode.GetPreamble());
            Response.Output.Write(sBuilder.ToString());
            Response.Flush();
            Response.SuppressContent = true;
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }
    }
}