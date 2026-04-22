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
using Take_Time_BangPhra.Integration;

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

                // Add sync status column for receipt/payment views (not detail views)
                if (dt != null && (DropDownList1.SelectedValue == "Account_Payment" || DropDownList1.SelectedValue == "Account_Receipt"))
                {
                    AddSyncStatusColumn(dt, DropDownList1.SelectedValue);
                }

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
                    string bp = System.Configuration.ConfigurationManager.AppSettings[ck]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(bp) && !Directory.Exists(bp + "\\" + docYear + "\\" + docMonth))
                    {
                        string pm = docMonth.PadLeft(2, '0');
                        if (pm != docMonth && Directory.Exists(bp + "\\" + docYear + "\\" + pm))
                            docMonth = pm;
                    }
                }
                if (docType == "REC")
                {
                    string path = System.Configuration.ConfigurationManager.AppSettings["ReceiptFolderPath"].ToString() + "\\" + docYear + "\\" + docMonth;

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

                    try
                    {
                        var sync = new AccountingSyncService(conn);
                        sync.EnqueueVoidReceipt(docNum);
                    }
                    catch (Exception accEx)
                    {
                        new code().Logs(conn, "Accounting Sync", $"Void receipt error (CheckDocument): docNum={docNum} {accEx.Message}", "SYSTEM");
                    }
                }
                else if (docType == "PAY")
                {
                    string path = System.Configuration.ConfigurationManager.AppSettings["PaymentFolderPath"].ToString() + "\\" + docYear + "\\" + docMonth;

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

                    try
                    {
                        var sync = new AccountingSyncService(conn);
                        sync.EnqueueVoidPaymentVoucher(docNum);
                    }
                    catch (Exception accEx)
                    {
                        new code().Logs(conn, "Accounting Sync", $"Void voucher error (CheckDocument): docNum={docNum} {accEx.Message}", "SYSTEM");
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
                string bp = System.Configuration.ConfigurationManager.AppSettings[ck]?.ToString() ?? "";
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
                    string path = System.Configuration.ConfigurationManager.AppSettings["ReceiptFolderPath"].ToString();
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
                    string path = System.Configuration.ConfigurationManager.AppSettings["ReceiptFolderPath"].ToString();
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
                    string path = System.Configuration.ConfigurationManager.AppSettings["PaymentFolderPath"].ToString();
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
                    string path = System.Configuration.ConfigurationManager.AppSettings["PaymentFolderPath"].ToString();
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
            if (e.CommandName == "sync")
            {
                string docId = e.CommandArgument.ToString();
                HandleSyncDocument(docId);
                return;
            }

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

        // ──────────────────────────────────────────────
        // Accounting Sync
        // ──────────────────────────────────────────────

        private Dictionary<string, string[]> _syncCache;

        private void AddSyncStatusColumn(DataTable dt, string docType)
        {
            dt.Columns.Add("Sync_บัญชี", typeof(string));

            try
            {
                string entityType = docType == "Account_Payment" ? "VOUCHER" : "RECEIPT";
                string actionType = docType == "Account_Payment" ? "CREATE_VOUCHER_JOURNAL" : "CREATE_RECEIPT_DOCUMENT";

                var queueDt = code.DatabaseQuerySafe(conn,
                    @"SELECT ID, Status, Payload FROM Accounting_Sync_Queue
                      WHERE Entity_Type = @et AND Action_Type = @at ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@et", entityType }, { "@at", actionType } });

                _syncCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                if (queueDt != null)
                {
                    foreach (DataRow qr in queueDt.Rows)
                    {
                        string payload = qr["Payload"]?.ToString() ?? "";
                        string key = docType == "Account_Payment"
                            ? ExtractJsonValue(payload, "documentNumber")
                            : ExtractJsonValue(payload, "receiptNumber");
                        if (!string.IsNullOrEmpty(key) && !_syncCache.ContainsKey(key))
                            _syncCache[key] = new[] { qr["ID"].ToString(), qr["Status"].ToString() };
                    }
                }

                foreach (DataRow row in dt.Rows)
                {
                    string docId = row["ID"]?.ToString() ?? "";
                    string status = "";
                    try { status = row["Status"]?.ToString() ?? ""; } catch { }

                    if (status == "Cancel")
                    {
                        row["Sync_บัญชี"] = "-";
                    }
                    else if (_syncCache.ContainsKey(docId))
                    {
                        var info = _syncCache[docId];
                        row["Sync_บัญชี"] = info[1] == "COMPLETED" ? "Synced #" + info[0]
                            : info[1] == "FAILED" ? "Failed #" + info[0]
                            : "รอ #" + info[0];
                    }
                    else
                    {
                        row["Sync_บัญชี"] = "ยังไม่ sync";
                    }
                }
            }
            catch { }
        }

        private string ExtractJsonValue(string json, string key)
        {
            string search = "\"" + key + "\":\"";
            int idx = json.IndexOf(search);
            if (idx < 0) return null;
            idx += search.Length;
            int end = json.IndexOf("\"", idx);
            return end > idx ? json.Substring(idx, end - idx) : null;
        }

        protected void GridView1_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;

            // Find the Sync_บัญชี column index
            int syncColIdx = -1;
            for (int i = 0; i < GridView1.Columns.Count + GridView1.HeaderRow.Cells.Count; i++)
            {
                try
                {
                    if (GridView1.HeaderRow.Cells[i].Text == "Sync_บัญชี")
                    {
                        syncColIdx = i;
                        break;
                    }
                }
                catch { break; }
            }
            if (syncColIdx < 0) return;

            string cellText = e.Row.Cells[syncColIdx].Text;
            string docId = e.Row.Cells[3].Text;

            if (cellText.StartsWith("Synced"))
            {
                e.Row.Cells[syncColIdx].Text = $"<span class='sync-badge completed'>{cellText}</span>";
            }
            else if (cellText.StartsWith("Failed"))
            {
                e.Row.Cells[syncColIdx].Text = $"<span class='sync-badge failed'>{cellText}</span> " +
                    $"<input type='button' class='btn-sync-sm' value='🔄' onclick=\"{Page.ClientScript.GetPostBackEventReference(GridView1, "sync$" + docId)}\" />";
            }
            else if (cellText.StartsWith("รอ"))
            {
                e.Row.Cells[syncColIdx].Text = $"<span class='sync-badge pending'>{cellText}</span>";
            }
            else if (cellText == "ยังไม่ sync")
            {
                e.Row.Cells[syncColIdx].Text = $"<span class='sync-badge none'>{cellText}</span> " +
                    $"<input type='button' class='btn-sync-sm' value='📤 Sync' onclick=\"if(confirm('ยืนยันส่งเข้าระบบบัญชี?')){{{Page.ClientScript.GetPostBackEventReference(GridView1, "sync$" + docId)}}}\" />";
            }
        }

        private void HandleSyncDocument(string docId)
        {
            try
            {
                string docType = docId.Length >= 3 ? docId.Substring(0, 3) : "";
                var sync = new AccountingSyncService(conn);
                long queueId = -1;

                if (docType == "PAY")
                {
                    var docParams = new Dictionary<string, object> { { "@ID", docId } };
                    var dt = code.DatabaseQuerySafe(conn,
                        @"SELECT ap.Total_Amount, ap.Vat, ap.Paid_How, ap.Paid_Type, ap.Created_Date,
                                 ISNULL(v.Name, '-') AS Vendor_Name
                          FROM Account_Payment ap LEFT JOIN Vendor v ON ap.Vendor_ID = v.ID
                          WHERE ap.ID = @ID", docParams);

                    if (dt?.Rows.Count > 0)
                    {
                        var r = dt.Rows[0];
                        string desc = "";
                        var detDt = code.DatabaseQuerySafe(conn,
                            "SELECT TOP 1 Detail FROM Account_Payment_Detail WHERE Payment_ID = @ID", docParams);
                        if (detDt?.Rows.Count > 0) desc = detDt.Rows[0]["Detail"]?.ToString() ?? "";

                        queueId = sync.EnqueuePaymentVoucher(0,
                            r["Paid_Type"]?.ToString() ?? "OTHER",
                            Convert.ToDecimal(r["Total_Amount"]),
                            r["Paid_How"]?.ToString() ?? "CASH",
                            Convert.ToDateTime(r["Created_Date"]),
                            desc,
                            r["Vendor_Name"]?.ToString() ?? "",
                            hasInputVat: Convert.ToDecimal(r["Vat"]) > 0,
                            documentNumber: docId);
                    }
                }
                else if (docType == "REC")
                {
                    var docParams = new Dictionary<string, object> { { "@ID", docId } };
                    var dt = code.DatabaseQuerySafe(conn,
                        @"SELECT ar.Total_Amount, ar.Vat, ar.Created_Date, ar.Reservation_ID,
                                 ar.IsDeposit, ar.Paid_Type,
                                 ISNULL(c.FullName, '-') AS CustomerName
                          FROM Account_Receipt ar
                          LEFT JOIN Reservation res ON res.ID = ar.Reservation_ID
                          LEFT JOIN Customer c ON c.MobilePhone = res.Customer_MobilePhone
                          WHERE ar.ID = @ID", docParams);

                    if (dt?.Rows.Count > 0)
                    {
                        var r = dt.Rows[0];
                        int resId = r["Reservation_ID"] != DBNull.Value ? Convert.ToInt32(r["Reservation_ID"]) : 0;
                        bool isDeposit = r["IsDeposit"] != DBNull.Value && Convert.ToBoolean(r["IsDeposit"]);
                        string paidType = r["Paid_Type"]?.ToString() ?? "CASH";
                        queueId = sync.EnqueueReceipt(resId, docId,
                            Convert.ToDecimal(r["Total_Amount"]),
                            Convert.ToDecimal(r["Vat"]),
                            Convert.ToDateTime(r["Created_Date"]),
                            r["CustomerName"]?.ToString() ?? "",
                            isDeposit: isDeposit, paymentMethod: paidType);
                    }
                }

                if (queueId > 0)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "syncOk",
                        $"alert('ส่งเข้าคิว sync สำเร็จ (Queue #{queueId})\\nดูสถานะได้ที่หน้า Accounting Integration');", true);
                }
                else
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "syncFail",
                        "alert('ไม่สามารถสร้าง sync job ได้ — ตรวจสอบการตั้งค่า Accounting Integration');", true);
                }
            }
            catch (Exception ex)
            {
                ClientScript.RegisterStartupScript(this.GetType(), "syncErr",
                    $"alert('Sync error: {ex.Message.Replace("'", "\\'")}');", true);
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