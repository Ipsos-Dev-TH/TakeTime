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
                Dictionary<string, object> parameters = new Dictionary<string, object>();

                if (DropDownList3.SelectedIndex > 0)
                {
                    cmd = "Select * From Account_Receipt Where Month(Created_Date) = @Month AND Year(Created_Date) = @Year AND Status like @Status";
                    parameters["@Month"] = DropDownList3.SelectedValue;
                    parameters["@Year"] = DropDownList4.SelectedValue;
                    parameters["@Status"] = DropDownList2.SelectedValue;
                }
                else
                {
                    cmd = "Select * From Account_Receipt Where Created_Date >= @StartDate AND Created_Date <= @EndDate AND Status like @Status";
                    parameters["@StartDate"] = TextBox1.Text;
                    parameters["@EndDate"] = TextBox2.Text;
                    parameters["@Status"] = DropDownList2.SelectedValue;
                }
                if (Session["User"].ToString() == "Admin")
                {
                    cmd += " AND Created_By_ID = @CreatedByID";
                    parameters["@CreatedByID"] = Session["UserID"].ToString();
                }
                DataTable dtin = code.DatabaseQuerySafe(conn, cmd, parameters);

                cmd = "";
                parameters = new Dictionary<string, object>();

                if (DropDownList3.SelectedIndex > 0)
                {
                    cmd = "Select * From Account_Payment  Where Month(Created_Date) = @Month AND Year(Created_Date) = @Year AND Status like @Status";
                    parameters["@Month"] = DropDownList3.SelectedValue;
                    parameters["@Year"] = DropDownList4.SelectedValue;
                    parameters["@Status"] = DropDownList2.SelectedValue;
                }
                else
                {
                    cmd = "Select * From Account_Payment  Where Created_Date >= @StartDate AND Created_Date <= @EndDate AND Status like @Status";
                    parameters["@StartDate"] = TextBox1.Text;
                    parameters["@EndDate"] = TextBox2.Text;
                    parameters["@Status"] = DropDownList2.SelectedValue;
                }
                if (Session["User"].ToString() == "Admin")
                {
                    cmd += " AND Created_By_ID = @CreatedByID";
                    parameters["@CreatedByID"] = Session["UserID"].ToString();
                }
                DataTable dtout = code.DatabaseQuerySafe(conn, cmd, parameters);

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
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                string payment_cmd = "SELECT Account_Payment.ID,[Name],Created_Date,Total_Amount,Vat_Type_ID,Vat,Total_Amount_Exclude_Vat,Paid_How,Paid_Type,Account_Payment.Status,Created_By_ID FROM [Account_Payment] inner join Vendor on Vendor.ID = Vendor_ID";
                string receipt_cmd = "SELECT Account_Receipt.ID,Reservation_ID,Customer.FullName,Customer.Address,Customer.IDNumber,Customer_MobilePhone,Account_Receipt.Created_Date,Total_Amount,Vat,Total_Amount_Exclude_Vat,IsDeposit,UseDeposit,Paid_Type,Account_Receipt.[Status],Created_By_ID,Reservation.Remark,Reservation.NoNameinReceipt FROM [Account_Receipt] left join Reservation on Reservation.ID = Account_Receipt.Reservation_ID left join Customer on Customer.MobilePhone = Reservation.Customer_MobilePhone";
                string detail_payment_cmd = "SELECT * FROM [Account_Payment_Detail] inner join Account_Payment on Account_Payment.ID = Payment_ID inner join Vendor on Vendor.ID = Vendor_ID";
                string detail_receipt_cmd = "SELECT Account_Receipt.ID,Account_Receipt.Created_Date,FullName,Address,IDNumber,Product_Data,[Product_Amount],[Product_Unit],[Price_PerPeice],[Price_Amount],Paid_Type,Vat,Total_Amount FROM [Account_Receipt] inner join Account_Receipt_Detail on Account_Receipt_Detail.Receipt_ID = Account_Receipt.ID left join Reservation on Reservation.ID = Reservation_ID left join Customer on Customer.MobilePhone = Reservation.Customer_MobilePhone";

                if (DropDownList3.SelectedIndex > 0)
                {
                    parameters["@Month"] = DropDownList3.SelectedValue;
                    parameters["@Year"] = DropDownList4.SelectedValue;
                    parameters["@Status"] = DropDownList2.SelectedValue;

                    if (DropDownList1.SelectedItem.Text == "ใบสำคัญจ่าย")
                    {
                        cmd = payment_cmd + " Where Month(Account_Payment.Created_Date) = @Month AND Year(Account_Payment.Created_Date) = @Year AND Account_Payment.Status like @Status";
                    }
                    else if (DropDownList1.SelectedItem.Text == "ใบเสร็จรับเงิน")
                    {
                        cmd = receipt_cmd + " Where Month(Account_Receipt.Created_Date) = @Month AND Year(Account_Receipt.Created_Date) = @Year AND Account_Receipt.Status like @Status";
                    }
                    else if (DropDownList1.SelectedItem.Text == "รายละเอียดใบสำคัญจ่าย")
                    {
                        cmd = detail_payment_cmd + " Where Month(Account_Payment.Created_Date) = @Month AND Year(Account_Payment.Created_Date) = @Year AND Account_Payment.Status like @Status";
                    }
                    else if (DropDownList1.SelectedItem.Text == "รายละเอียดใบเสร็จรับเงิน")
                    {
                        cmd = detail_receipt_cmd + " Where Month(Account_Receipt.Created_Date) = @Month AND Year(Account_Receipt.Created_Date) = @Year AND Account_Receipt.Status like @Status";
                    }
                    else { }
                }
                else
                {
                    parameters["@StartDate"] = TextBox1.Text;
                    parameters["@EndDate"] = TextBox2.Text;
                    parameters["@Status"] = DropDownList2.SelectedValue;

                    if (DropDownList1.SelectedItem.Text == "ใบสำคัญจ่าย")
                    {
                        cmd = payment_cmd + " Where Account_Payment.Created_Date >= @StartDate AND Account_Payment.Created_Date <= @EndDate AND Account_Payment.Status like @Status";
                    }
                    else if (DropDownList1.SelectedItem.Text == "ใบเสร็จรับเงิน")
                    {
                        cmd = receipt_cmd + " Where Account_Receipt.Created_Date >= @StartDate AND Account_Receipt.Created_Date <= @EndDate AND Account_Receipt.Status like @Status";
                    }
                    else if (DropDownList1.SelectedItem.Text == "รายละเอียดใบสำคัญจ่าย")
                    {
                        cmd = detail_payment_cmd + " Where Account_Payment.Created_Date >= @StartDate AND Account_Payment.Created_Date <= @EndDate AND Account_Payment.Status like @Status";
                    }
                    else if (DropDownList1.SelectedItem.Text == "รายละเอียดใบเสร็จรับเงิน")
                    {
                        cmd = detail_receipt_cmd + " Where Account_Receipt.Created_Date >= @StartDate AND Account_Receipt.Created_Date <= @EndDate AND Account_Receipt.Status like @Status";
                    }
                    else { }


                }


                if (Session["User"].ToString() == "Admin" && (DropDownList1.SelectedIndex == 0 || DropDownList1.SelectedIndex == 2))
                {
                    cmd += " AND Vendor.Vendor_Group != N'01-พนักงานประจำ'";
                }
                cmd += " order by ID asc";
                DataTable dt = code.DatabaseQuerySafe(conn, cmd, parameters);

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
                // Fallback: check padded month directory for files created with zero-padded month
                {
                    string ck = (docType == "REC") ? "ReceiptFolderPath" : "PaymentFolderPath";
                    string bp = System.Configuration.ConfigurationSettings.AppSettings[ck]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(bp) && !Directory.Exists(bp + "\\" + docYear + "\\" + docMonth))
                    {
                        string pm = docMonth.PadLeft(2, '0');
                        if (pm != docMonth && Directory.Exists(bp + "\\" + docYear + "\\" + pm))
                            docMonth = pm;
                    }
                }
                if (docType == "REC")
                {
                    string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString() + "\\" + docYear + "\\" + docMonth;

                    // 🔧 FIX: Update Reservation.Deposit before deleting Payment_History
                    try
                    {
                        // Get payment amount and reservation ID from Payment_History
                        var paymentHistoryParams = new Dictionary<string, object>
                        {
                            { "@ReceiptID", docNum }
                        };
                        var paymentData = code.DatabaseQuerySafe(conn,
                            "SELECT ph.PaymentAmount, ph.Reservation_ID " +
                            "FROM [dbo].[Payment_History] ph " +
                            "WHERE ph.Receipt_ID = @ReceiptID",
                            paymentHistoryParams);

                        if (paymentData != null && paymentData.Rows.Count > 0)
                        {
                            foreach (DataRow row in paymentData.Rows)
                            {
                                decimal amount = row["PaymentAmount"] != DBNull.Value ? Convert.ToDecimal(row["PaymentAmount"]) : 0;
                                int reservationId = row["Reservation_ID"] != DBNull.Value ? Convert.ToInt32(row["Reservation_ID"]) : 0;

                                if (amount > 0 && reservationId > 0)
                                {
                                    // Reduce Reservation.Deposit by payment amount
                                    var updateReservationParams = new Dictionary<string, object>
                                    {
                                        { "@Amount", amount },
                                        { "@ReservationID", reservationId }
                                    };
                                    code.DatabaseInsertSafe(conn,
                                        "UPDATE [dbo].[Reservation] SET Deposit = ISNULL(Deposit, 0) - @Amount WHERE ID = @ReservationID",
                                        updateReservationParams);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Continue with deletion even if update fails (data consistency issue but prevents stuck state)
                        System.Diagnostics.Debug.WriteLine($"⚠️ Error updating Reservation.Deposit: {ex.Message}");
                    }

                    // SECURE: Delete Payment_History records that reference this receipt
                    var deletePaymentHistoryParams = new Dictionary<string, object>
                    {
                        { "@ReceiptID", docNum }
                    };
                    code.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Payment_History] WHERE Receipt_ID = @ReceiptID",
                        deletePaymentHistoryParams);

                    // SECURE: Delete receipt details
                    var deleteReceiptDetailParams = new Dictionary<string, object>
                    {
                        { "@ReceiptID", docNum }
                    };
                    code.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Account_Receipt_Detail] WHERE Receipt_ID = @ReceiptID",
                        deleteReceiptDetailParams);

                    // SECURE: Delete receipt record
                    var deleteReceiptParams = new Dictionary<string, object>
                    {
                        { "@ID", docNum }
                    };
                    code.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Account_Receipt] WHERE ID = @ID",
                        deleteReceiptParams);

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

                    // SECURE: Delete payment record
                    var deletePaymentParams = new Dictionary<string, object>
                    {
                        { "@ID", docNum }
                    };
                    code.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Account_Payment] WHERE ID = @ID",
                        deletePaymentParams);

                    // SECURE: Delete payment details
                    var deletePaymentDetailParams = new Dictionary<string, object>
                    {
                        { "@PaymentID", docNum }
                    };
                    code.DatabaseInsertSafe(conn,
                        "DELETE FROM [dbo].[Account_Payment_Detail] WHERE Payment_ID = @PaymentID",
                        deletePaymentDetailParams);
                    string[] dirs = Directory.GetFiles(path, docNum + "*");
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        File.Delete(dirs[i].ToString());
                    }
                }

                // Show success message then redirect
                ClientScript.RegisterStartupScript(this.GetType(), "success",
                    "alert('✅ ลบเอกสารเรียบร้อยแล้ว'); window.location.href='/Account/CheckDocument';", true);
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
            // Fallback: check padded month directory for files created with zero-padded month
            {
                string ck = (docType == "REC") ? "ReceiptFolderPath" : "PaymentFolderPath";
                string bp = System.Configuration.ConfigurationSettings.AppSettings[ck]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(bp) && !Directory.Exists(bp + "\\" + docYear + "\\" + docMonth))
                {
                    string pm = docMonth.PadLeft(2, '0');
                    if (pm != docMonth && Directory.Exists(bp + "\\" + docYear + "\\" + pm))
                        docMonth = pm;
                }
            }
            if (docType == "REC")
            {
                // SECURE: Get receipt UID with parameterized query
                var receiptUidParams = new Dictionary<string, object>
                {
                    { "@ID", docNum }
                };
                DataTable uidResult = code.DatabaseQuerySafe(conn,
                    "SELECT [UID] FROM [Taketime].[dbo].[Account_Receipt] WHERE ID = @ID",
                    receiptUidParams);
                string receiptUid = uidResult.Rows.Count > 0 ? uidResult.Rows[0][0].ToString() : "";

                if (docStatus == "Cancel")
                {
                    string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
                    if (!string.IsNullOrEmpty(receiptUid) && File.Exists(path + "\\" + docYear + "\\" + docMonth + "" + docNum + "_" + receiptUid + "_Cancel.pdf"))
                    {
                        Response.Redirect("/Documents/Receipt/" + docYear + "/" + docMonth + "/" + docNum + "_" + receiptUid + "_Cancel.pdf");
                    }
                    else
                    {
                        Response.Redirect("/Documents/Receipt/" + docYear + "/" + docMonth + "/" + docNum + "_Cancel.pdf");
                    }

                }
                else
                {
                    string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
                    if (!string.IsNullOrEmpty(receiptUid) && File.Exists(path + "\\" + docYear + "\\" + docMonth + "\\" + docNum + "_" + receiptUid + ".pdf"))
                    {
                        Response.Redirect("/Documents/Receipt/" + docYear + "/" + docMonth + "/" + docNum + "_" + receiptUid + ".pdf");
                    }
                    else
                    {
                        Response.Redirect("/Documents/Receipt/" + docYear + "/" + docMonth + "/" + docNum + ".pdf");
                    }

                }


            }
            else if (docType == "PAY")
            {
                // SECURE: Get payment UID with parameterized query
                var paymentUidParams = new Dictionary<string, object>
                {
                    { "@ID", docNum }
                };
                DataTable uidResult = code.DatabaseQuerySafe(conn,
                    "SELECT [UID] FROM [Taketime].[dbo].[Account_Payment] WHERE ID = @ID",
                    paymentUidParams);
                string paymentUid = uidResult.Rows.Count > 0 ? uidResult.Rows[0][0].ToString() : "";

                if (docStatus == "Cancel")
                {
                    string path = System.Configuration.ConfigurationSettings.AppSettings["PaymentFolderPath"].ToString();
                    if (!string.IsNullOrEmpty(paymentUid) && File.Exists(path + "\\" + docYear + "\\" + docMonth + "\\" + docNum + "_" + paymentUid + "_Cancel.pdf"))
                    {
                        Response.Redirect("/Documents/Payment/" + docYear + "/" + docMonth + "/" + docNum + "_" + paymentUid + "_Cancel.pdf");
                    }
                    else
                    {
                        Response.Redirect("/Documents/Payment/" + docYear + "/" + docMonth + "/" + docNum + "_Cancel.pdf");
                    }

                }
                else
                {
                    string path = System.Configuration.ConfigurationSettings.AppSettings["PaymentFolderPath"].ToString();
                    if (!string.IsNullOrEmpty(paymentUid) && File.Exists(path + "\\" + docYear + "\\" + docMonth + "\\" + docNum + "_" + paymentUid + ".pdf"))
                    {
                        Response.Redirect("/Documents/Payment/" + docYear + "/" + docMonth + "/" + docNum + "_" + paymentUid + ".pdf");
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
                    // SECURE: Get receipt UID with parameterized query
                    var receiptUidParams = new Dictionary<string, object>
                    {
                        { "@ID", docNum }
                    };
                    string uid = code.DatabaseQuerySafe(conn,
                        "SELECT [UID] FROM [Taketime].[dbo].[Account_Receipt] WHERE ID = @ID",
                        receiptUidParams).Rows[0][0].ToString();
                    Response.Redirect("/Account/Receipt?command=edit&uid=" + uid);
                }
                else if (docType == "PAY")
                {
                    // SECURE: Get payment UID with parameterized query
                    var paymentUidParams = new Dictionary<string, object>
                    {
                        { "@ID", docNum }
                    };
                    string uid = code.DatabaseQuerySafe(conn,
                        "SELECT [UID] FROM [Taketime].[dbo].[Account_Payment] WHERE ID = @ID",
                        paymentUidParams).Rows[0][0].ToString();
                    Response.Redirect("/Account/PaymentVoucher?command=edit&uid=" + uid);
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