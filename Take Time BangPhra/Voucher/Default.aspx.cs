using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using System.IO;
using Microsoft.Reporting.WebForms;
using System.Globalization;
using iTextSharp.text.pdf.qrcode;
using iTextSharp.text.pdf;
using ECertificateAPI;
using System.Net.Mail;
using Take_Time_BangPhra.Admin;
using Take_Time_BangPhra.Account.Report;
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.Voucher
{
    public partial class Default : System.Web.UI.Page
    {
        _Default code = new _Default();
        code code2 = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        // ✨ Helper Classes for refactored system
        private AddressHelper _addressHelper;
        private CustomerHelper _customerHelper;
        private DocumentHelper _documentHelper;

        protected void Page_Load(object sender, EventArgs e)
        {
            // ✨ Initialize Helper Classes
            _addressHelper = new AddressHelper(conn);
            _customerHelper = new CustomerHelper(conn);
            _documentHelper = new DocumentHelper(conn);

            this.MaintainScrollPositionOnPostBack = true;
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

            if (!IsPostBack)
            {
                TextBox21.Text = DateTime.Now.Year.ToString()+"-12-30";

                TextBox8.Text = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                DataTable dtCustomerType = code.DatabaseQuery(conn, "Select [Customer_Type],ID From Customer_Type");
                for (int i = 0; i < dtCustomerType.Rows.Count; i++)
                {
                    DropDownList8.Items.Add(new ListItem(dtCustomerType.Rows[i][0].ToString(), dtCustomerType.Rows[i][1].ToString()));
                }
                DropDownList8.DataBind();

                DataTable dtPaidHow = code.DatabaseQuery(SqlDataSource2.ConnectionString, SqlDataSource2.SelectCommand);
                for (int i = 0; i < dtPaidHow.Rows.Count; i++)
                {
                    DropDownList2.Items.Add(new ListItem(dtPaidHow.Rows[i]["Paid_How"].ToString(), dtPaidHow.Rows[i]["ID"].ToString()));
                }
                DropDownList2.DataBind();


                DataTable dtVatType = code.DatabaseQuery(SqlDataSource4.ConnectionString, SqlDataSource4.SelectCommand);
                for (int i = 0; i < dtVatType.Rows.Count; i++)
                {
                    DropDownList4.Items.Add(new ListItem(dtVatType.Rows[i]["Vat_Type"].ToString(), dtVatType.Rows[i]["ID"].ToString()));
                }
                DropDownList4.DataBind();

                getAddress("SELECT DISTINCT [Province] FROM [Address] order by Province ASC", "SELECT DISTINCT [District] FROM [Address] order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] order by SubDistrict ASC");

                string command = Request.QueryString["command"];
                string uid = Request.QueryString["uid"];
                
                if(command == "edit")
                {

                    // SECURE: Receipt lookup with parameterized query
                    var receiptParams = new Dictionary<string, object>
                    {
                        { "@UID", uid ?? "" }
                    };

                    DataTable dtReceipt = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Account_Receipt " +
                        "LEFT JOIN Reservation ON Reservation.ID = Reservation_ID " +
                        "WHERE Account_Receipt.UID = @UID",
                        receiptParams);

                    string id = dtReceipt.Rows[0]["ID"].ToString();

                    // SECURE: Receipt detail lookup with parameterized query
                    var receiptDetailParams = new Dictionary<string, object>
                    {
                        { "@ReceiptID", id }
                    };

                    DataTable dtReceiptDetail = code.DatabaseQuerySafe(conn,
                        "SELECT Number,ProductType_ID,Product_Data,Product_Amount,Product_Unit,Price_PerPeice,Price_Amount " +
                        "FROM Account_Receipt_Detail WHERE Receipt_ID = @ReceiptID",
                        receiptDetailParams);

                    DataTable dtcustomer = new DataTable();
                    try
                    {
                        if( Convert.ToInt32(dtReceipt.Rows[0]["Customer_ID"].ToString()) > 0)
                        {
                            // SECURE: Customer lookup by ID with parameterized query
                            var customerByIDParams = new Dictionary<string, object>
                            {
                                { "@CustomerID", dtReceipt.Rows[0]["Customer_ID"].ToString() }
                            };

                            dtcustomer = code.DatabaseQuerySafe(conn,
                                "SELECT * FROM Customer " +
                                "LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID " +
                                "LEFT JOIN Address ON Address.ID = Address_ID " +
                                "WHERE Customer.ID = @CustomerID",
                                customerByIDParams);
                        }
                        else
                        {
                            // SECURE: Customer lookup by mobile with parameterized query
                            var customerByPhoneParams = new Dictionary<string, object>
                            {
                                { "@MobilePhone", dtReceipt.Rows[0]["Customer_MobilePhone"].ToString() }
                            };

                            dtcustomer = code.DatabaseQuerySafe(conn,
                                "SELECT * FROM Customer " +
                                "LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID " +
                                "LEFT JOIN Address ON Address.ID = Address_ID " +
                                "WHERE MobilePhone = @MobilePhone",
                                customerByPhoneParams);
                        }

                    }
                    catch {
                        // SECURE: Customer lookup by mobile with parameterized query (fallback)
                        var customerByPhoneFallbackParams = new Dictionary<string, object>
                        {
                            { "@MobilePhone", dtReceipt.Rows[0]["Customer_MobilePhone"].ToString() }
                        };

                        dtcustomer = code.DatabaseQuerySafe(conn,
                            "SELECT * FROM Customer " +
                            "LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID " +
                            "LEFT JOIN Address ON Address.ID = Address_ID " +
                            "WHERE MobilePhone = @MobilePhone",
                            customerByPhoneFallbackParams);
                        
                    }

                    try //Address
                    {
                        try
                        {
                            TextBox16.Text = dtcustomer.Rows[0]["PostalCode"].ToString();

                            DropDownList5.ClearSelection();
                            DropDownList5.Items.FindByText(dtcustomer.Rows[0]["Province"].ToString()).Selected = true;
                            DropDownList5.SelectedIndex = DropDownList5.Items.IndexOf(DropDownList5.Items.FindByText(dtcustomer.Rows[0]["Province"].ToString()));
                            DropDownList6.ClearSelection();
                            DropDownList6.Items.FindByText(dtcustomer.Rows[0]["District"].ToString()).Selected = true;
                            DropDownList6.SelectedIndex = DropDownList6.Items.IndexOf(DropDownList6.Items.FindByText(dtcustomer.Rows[0]["District"].ToString()));
                            DropDownList7.ClearSelection();
                            DropDownList7.Items.FindByText(dtcustomer.Rows[0]["SubDistrict"].ToString()).Selected = true;
                            DropDownList7.SelectedIndex = DropDownList7.Items.IndexOf(DropDownList7.Items.FindByText(dtcustomer.Rows[0]["SubDistrict"].ToString()));
                        }
                        catch { }
                        try
                        {
                            DropDownList8.ClearSelection();
                            DropDownList8.Items.FindByValue(dtcustomer.Rows[0]["Customer_Type_ID"].ToString()).Selected = true;
                            DropDownList8.SelectedIndex = DropDownList8.Items.IndexOf(DropDownList8.Items.FindByValue(dtcustomer.Rows[0]["Customer_Type_ID"].ToString()));
                            DropDownList8.DataBind();
                            if (DropDownList8.SelectedIndex == 0)
                            {
                                TextBox7.Visible = true;
                                TextBox7.Text = dtcustomer.Rows[0]["Branch_Number"].ToString();
                            }
                            else
                            {
                                TextBox7.Visible = false;
                            }
                        }
                        catch { DropDownList8.SelectedIndex = 1;
                            DropDownList8.DataBind();
                        }
                            DropDownList2.SelectedIndex = DropDownList2.Items.IndexOf(DropDownList2.Items.FindByText(dtReceipt.Rows[0]["Paid_Type"].ToString()));
                        DropDownList2.DataBind();
                       
                        DropDownList4.SelectedIndex = DropDownList4.Items.IndexOf(DropDownList4.Items.FindByValue("1"));
                        DropDownList4.DataBind();
                    }
                    catch { }

                    if (dtReceipt.Rows[0]["NoNameinReceipt"].ToString().ToLower() == "true")
                    {
                        CheckBox3.Checked = true;
                        CheckBox3.DataBind();
                        TextBox10.Text = "ประสงค์ไม่รับใบกำกับภาษี";
                    }

                    GridView1.DataSource = dtReceiptDetail;
                    GridView1.DataBind();

                    TextBox3.Text = dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"].ToString();
                    TextBox4.Text = dtReceipt.Rows[0]["Vat"].ToString();
                    TextBox6.Text = dtReceipt.Rows[0]["Total_Amount"].ToString();

                    try
                    {

                        if (dtReceipt.Rows[0]["Etax"].ToString().ToLower() == "false")
                        {
                            CheckBox5.Checked = false;
                            CheckBox5.DataBind();
                        }
                        if (dtReceipt.Rows[0]["Etax"].ToString().ToLower() == "true")
                        {
                            CheckBox5.Checked = true;
                            CheckBox5.DataBind();
                            TextBox7.Visible = true;
                            TextBox7.Text = dtcustomer.Rows[0]["Branch_Number"].ToString();
                            
                        }
                    }
                    catch { }

                    if (CheckBox3.Checked == true)
                    {
                        //TextBox10.Text = "ประสงค์ไม่รับใบกำกับภาษี";
                        //TextBox11.Text = "";
                        //TextBox12.Text = "";
                        // TextBox13.Text = "";

                    }
                    else
                    {
                        TextBox10.Text = dtcustomer.Rows[0]["FullName"].ToString();
                        TextBox11.Text = dtcustomer.Rows[0]["Address"].ToString();
                        TextBox12.Text = dtcustomer.Rows[0]["IDNumber"].ToString();
                        TextBox13.Text = dtcustomer.Rows[0]["MobilePhone"].ToString();
                        TextBox17.Text = dtcustomer.Rows[0]["Email"].ToString();
                        TextBox18.Text = dtcustomer.Rows[0]["Address1"].ToString();
                    }
                    

                    DropDownList2.DataBind();
                    DropDownList2.SelectedIndex = DropDownList2.Items.IndexOf(DropDownList2.Items.FindByText(dtReceipt.Rows[0]["Paid_Type"].ToString()));
                    DropDownList2.DataBind();

                    DropDownList4.SelectedIndex = 1;
                    DropDownList4.DataBind();

                    
                    Session["dtDetail"] = dtReceiptDetail;

                    Panel1.Visible = true;

                }

                DataTable dtDetail = new DataTable();
                try
                {
                    dtDetail.Columns.Add("Number");
                    dtDetail.Columns.Add("RatePlan_Group");
                    dtDetail.Columns.Add("PriceTo");

                }
                catch
                {

                }
                if (command == "edit")
                {
                    
                }
                else
                {
                    Session["dtDetail"] = dtDetail;
                }
                
                DataTable dtUpload = new DataTable();
                try
                {
                    dtUpload.Columns.Add("Name");
                }
                catch
                {

                }
                Session["dtUpload"] = dtUpload;
            }

        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            Label1.Text = DropDownList4.SelectedItem.Text;
            
            if ( TextBox2.Text.Length > 0)
            {
                DataTable dtDetail = (DataTable)Session["dtDetail"];
                if(dtDetail.Rows.Count > 0)
                {
                    // SECURE: Rate plan group lookup with parameterized query
                    var ratePlanGroupParams = new Dictionary<string, object>
                    {
                        { "@GroupID", DropDownList3.SelectedValue }
                    };

                    DataTable dtDetails = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Accommodation_RatePlan_Group WHERE GroupID = @GroupID",
                        ratePlanGroupParams);

                    bool checkdup = false;
                    for(int i = 0;i<dtDetail.Rows.Count;i++)
                    {
                        // SECURE: Rate plan group lookup by name with parameterized query
                        var ratePlanByNameParams = new Dictionary<string, object>
                        {
                            { "@GroupName", dtDetail.Rows[i]["RatePlan_Group"].ToString() }
                        };

                        DataTable dtRatePlanByName = code.DatabaseQuerySafe(conn,
                            "SELECT * FROM Accommodation_RatePlan_Group WHERE Group_Name = @GroupName",
                            ratePlanByNameParams);

                        if(dtRatePlanByName.Rows[0]["AccomGroupID"].ToString() == dtDetails.Rows[0]["AccomGroupID"].ToString())
                        {
                            
                            
                        }
                        else
                        {
                            checkdup = true;
                            ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ประเภทเรทที่พักที่คุณเลือกไม่ตรงกัน');", true);
                        }
                    }
                    if(checkdup == false)
                    {
                        dtDetail.Rows.Add(dtDetail.Rows.Count + 1, DropDownList3.SelectedItem.Text, TextBox2.Text);
                    }
                }
                else
                {
                    dtDetail.Rows.Add(dtDetail.Rows.Count + 1, DropDownList3.SelectedItem.Text, TextBox2.Text);
                }

                Session["dtDetail"] = (DataTable)dtDetail;
                GridView1.DataSource = dtDetail;
                GridView1.DataBind();
                
                TextBox2.Text = "0";
            }
            else
            {

            }
        }

        public void calAmount(DataTable dtDetail)
        {
            double totalAmount = 0;
            for(int i=0;i<dtDetail.Rows.Count;i++)
            {
                totalAmount += Convert.ToDouble(dtDetail.Rows[i]["Price_Amount"].ToString());
            }
            // SECURE: VAT percent lookup with parameterized query
            var vatParams = new Dictionary<string, object>
            {
                { "@VatTypeID", DropDownList4.SelectedValue }
            };

            int vatPercent = Convert.ToInt32(code.DatabaseQuerySafe(conn,
                "SELECT Vat_Percent FROM Account_Vat_Type WHERE Status = 'True' AND ID = @VatTypeID",
                vatParams).Rows[0][0].ToString());
            double AmountExcludeVat = (totalAmount * 100) / (100 + vatPercent);
            double vat = totalAmount - AmountExcludeVat;
            TextBox3.Text = NumberHelper.TwoDecimalPoints(AmountExcludeVat).ToString();
            TextBox4.Text = NumberHelper.TwoDecimalPoints(vat).ToString();
            TextBox6.Text = NumberHelper.TwoDecimalPoints(totalAmount).ToString();

        }

        protected void GridView1_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            DataTable dtDetail = (DataTable)Session["dtDetail"];
            dtDetail.Rows[e.RowIndex].Delete();
            dtDetail.AcceptChanges();
            for(int i = 0;i<dtDetail.Rows.Count;i++)
            {
                dtDetail.Rows[i][0] = i + 1;
            }
            Session["dtDetail"] = (DataTable)dtDetail;
            GridView1.DataSource = dtDetail;
            GridView1.DataBind();
            calAmount(dtDetail);
        }

        protected void DropDownList4_SelectedIndexChanged(object sender, EventArgs e)
        {
            Label1.Text = DropDownList4.SelectedItem.Text;
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            string command = Request.QueryString["command"];
            string id = Request.QueryString["id"];

            // ✨ Declare date variables used throughout the method
            DateTime receiptDate = Convert.ToDateTime(TextBox8.Text);
            string Year = receiptDate.Year.ToString();
            string Month = receiptDate.Month.ToString("00");
            string Day = receiptDate.Day.ToString("00");

            bool imgupload = false;

            try
            {
                if(Image1.ImageUrl.Length > 0)
                {
                    imgupload = true;
                }
            }
            catch
            {

            }
            if (TextBox6.Text.Length > 0 && DropDownList2.SelectedIndex > 0 && DropDownList4.SelectedIndex > 0 && imgupload)
            {
                // ✨ Use DocumentHelper to generate receipt number
                string docNum = _documentHelper.CreateDocumentNumber("Account_Receipt", "REC", receiptDate);

                string RecNumber = docNum;
                int reservation_id = 0;

                DataTable dtcustomer = new DataTable();

                // ✨ SECURE: Use UpsertCustomer with Helper Classes
                // Use AddressHelper to get Address ID
                string addressIdStr = _addressHelper.GetAddressIdString(
                    TextBox16.Text,
                    DropDownList5.SelectedItem.Text,
                    DropDownList6.SelectedItem.Text,
                    DropDownList7.SelectedItem.Text);
                int addressId = string.IsNullOrEmpty(addressIdStr) ? 0 : Convert.ToInt32(addressIdStr);
                int customerTypeId = string.IsNullOrEmpty(DropDownList8.SelectedValue) ? 0 : Convert.ToInt32(DropDownList8.SelectedValue);

                long customerId = code.UpsertCustomer(
                    conn,
                    TextBox13.Text,                          // mobilePhone
                    "",                                      // name
                    "",                                      // nickname
                    "",                                      // comeFrom
                    "",                                      // remark
                    TextBox10.Text,                          // fullName
                    ValidationHelper.CleanText(TextBox11.Text), // address (✨ using ValidationHelper)
                    TextBox16.Text,           // postalCode
                    TextBox17.Text,           // email
                    customerTypeId,           // customerTypeId (converted to int)
                    addressId,                // addressId (converted to int)
                    TextBox18.Text,           // address1
                    TextBox7.Text             // branchNumber
                );

                // SECURE: Customer lookup with parameterized query
                var customerParams = new Dictionary<string, object>
                {
                    { "@MobilePhone", TextBox13.Text ?? "" }
                };

                dtcustomer = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Customer " +
                    "LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID " +
                    "LEFT JOIN Address ON Address.ID = Address_ID " +
                    "WHERE MobilePhone = @MobilePhone",
                    customerParams);


                if (CheckBox4.Checked == false)
                {




                    DataTable dtReceipt = new DataTable();
                    try
                    {
                        // SECURE: Receipt lookup with parameterized query
                        var receiptParams = new Dictionary<string, object>
                        {
                            { "@ReceiptID", RecNumber }
                        };

                        dtReceipt = code.DatabaseQuerySafe(conn,
                            "SELECT * FROM [Account_Receipt] " +
                            "INNER JOIN Reservation ON Reservation.ID = Reservation_ID " +
                            "WHERE Account_Receipt.ID = @ReceiptID",
                            receiptParams);

                        if (dtReceipt.Rows.Count <= 0)
                        {
                            //reservation_id = code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation] ([Customer_MobilePhone],[CheckinDate],[CheckoutDate],[StayDays],[Status],[TotalPrice],[Deposit],[Remark],[Reserve_By],[Created_Date],NoNameinReceipt) VALUES ('" + TextBox13.Text + "','" + Convert.ToDateTime(TextBox8.Text).ToString("yyyy-MM-dd") + "','" + Convert.ToDateTime(TextBox8.Text).AddDays(Convert.ToDouble(1)).ToString("yyyy-MM-dd") + "'," + "1" + ",N'ชำระเงินแล้ว'," + TextBox6.Text + "," + TextBox6.Text + ",N'" + TextBox6.Text + "', N'" + Session["UserName"].ToString() + "','" + DateTime.Now + "','False') SELECT SCOPE_IDENTITY(); ");
                            //dtReceipt = code.DatabaseQuery(conn, "SELECT * FROM [Account_Receipt] left join Reservation on Reservation.ID = Reservation_ID Where Account_Receipt.ID = '" + RecNumber + "'");
                        }
                        else
                        {
                            reservation_id = Convert.ToInt32(dtReceipt.Rows[0]["Reservation_ID"].ToString());
                        }
                    }
                    catch
                    {
                        // SECURE: Receipt lookup fallback with parameterized query
                        var receiptFallbackParams = new Dictionary<string, object>
                        {
                            { "@ReceiptID", RecNumber }
                        };

                        dtReceipt = code.DatabaseQuerySafe(conn,
                            "SELECT * FROM [Account_Receipt] " +
                            "LEFT JOIN Reservation ON Reservation.ID = Reservation_ID " +
                            "WHERE Account_Receipt.ID = @ReceiptID",
                            receiptFallbackParams);
                    }

                    if (command == "edit")
                    {
                        // SECURE: DELETE operations with parameterized queries
                        var deleteParams = new Dictionary<string, object>
                        {
                            { "@ID", id }
                        };

                        code.DatabaseInsertSafe(conn,
                            "DELETE FROM [dbo].[Account_Receipt] WHERE ID = @ID",
                            deleteParams);

                        var deleteDetailParams = new Dictionary<string, object>
                        {
                            { "@ReceiptID", id }
                        };

                        code.DatabaseInsertSafe(conn,
                            "DELETE FROM [dbo].[Account_Receipt_Detail] WHERE Receipt_ID = @ReceiptID",
                            deleteDetailParams);
                    }
                    else { }

                    // SECURE: Receipt INSERT with parameterized query
                    var receiptInsertParams = new Dictionary<string, object>
                    {
                        { "@DocNum", docNum },
                        { "@ReservationID", reservation_id > 0 ? reservation_id.ToString() : "0" },
                        { "@CreatedDate", Convert.ToDateTime(TextBox8.Text) },
                        { "@TotalAmount", TextBox1.Text },
                        { "@Vat", TextBox4.Text },
                        { "@TotalAmountExcludeVat", TextBox3.Text },
                        { "@PaidType", DropDownList2.SelectedItem.Text },
                        { "@CreatedByID", Session["UserID"].ToString() },
                        { "@Etax", CheckBox5.Checked },
                        { "@CustomerID", dtcustomer.Rows[0]["ID"].ToString() }
                    };

                    code.DatabaseInsertSafe(conn,
                        "INSERT INTO [dbo].[Account_Receipt] " +
                        "([ID],[Reservation_ID],[Created_Date],[Total_Amount],[Vat],[Total_Amount_Exclude_Vat],[IsDeposit],[UseDeposit],[Paid_Type],[Status],[Created_By_ID],Etax,Customer_ID) " +
                        "VALUES (@DocNum,@ReservationID,@CreatedDate,@TotalAmount,@Vat,@TotalAmountExcludeVat,'False','False',@PaidType,'Normal',@CreatedByID,@Etax,@CustomerID)",
                        receiptInsertParams);

                    // SECURE: Receipt Detail INSERT with parameterized query
                    var receiptDetailInsertParams = new Dictionary<string, object>
                    {
                        { "@DocNum", docNum },
                        { "@ProductAmount", TextBox19.Text },
                        { "@PricePerPiece", TextBox6.Text },
                        { "@PriceAmount", TextBox1.Text }
                    };

                    code.DatabaseInsertSafe(conn,
                        "INSERT INTO [dbo].[Account_Receipt_Detail] " +
                        "([Number],[Receipt_ID],[ProductType_ID],[Product_ID],[Product_Data],[Product_Amount],[Product_Unit],[Price_PerPeice],[Price_Amount]) " +
                        "VALUES ('1',@DocNum,'1',0,N'Voucher ที่พัก',@ProductAmount,N'ใบ',@PricePerPiece,@PriceAmount)",
                        receiptDetailInsertParams);

                    // 🆕 Record payment to Payment_History when voucher receipt is created
                    if (reservation_id > 0)
                    {
                        try
                        {
                            string paymentType = "FULL";
                            string paymentMethod = DropDownList2.SelectedItem?.Text ?? "CASH";
                            string paymentNotes = "ขาย Voucher ที่พัก - " + docNum;
                            decimal totalAmount = Convert.ToDecimal(TextBox1.Text);

                            int? adminId = null;
                            if (Session["UserID"] != null)
                            {
                                adminId = Convert.ToInt32(Session["UserID"]);
                            }

                            // Get customer phone from reservation
                            string customerPhone = "";
                            try
                            {
                                DataTable dtPhone = code.DatabaseQuery(conn,
                                    "SELECT Customer_MobilePhone FROM Reservation WHERE ID = '" + reservation_id + "'");
                                if (dtPhone.Rows.Count > 0)
                                {
                                    customerPhone = dtPhone.Rows[0]["Customer_MobilePhone"].ToString();
                                }
                            }
                            catch { }

                            string insertPaymentQuery = @"
                                INSERT INTO [dbo].[Payment_History] (
                                    Reservation_ID,
                                    PaymentDate,
                                    PaymentAmount,
                                    PaymentType,
                                    PaymentMethod,
                                    Receipt_ID,
                                    ProcessedBy_AdminID,
                                    PaidBy_CustomerPhone,
                                    Status,
                                    Notes,
                                    CreatedDate,
                                    UpdatedDate
                                ) VALUES (
                                    @ReservationId,
                                    @PaymentDate,
                                    @PaymentAmount,
                                    @PaymentType,
                                    @PaymentMethod,
                                    @ReceiptId,
                                    @AdminId,
                                    @CustomerPhone,
                                    'COMPLETED',
                                    @Notes,
                                    GETDATE(),
                                    GETDATE()
                                )";

                            var paymentParams = new Dictionary<string, object>
                            {
                                { "@ReservationId", reservation_id },
                                { "@PaymentDate", Convert.ToDateTime(TextBox8.Text) },
                                { "@PaymentAmount", totalAmount },
                                { "@PaymentType", paymentType },
                                { "@PaymentMethod", paymentMethod },
                                { "@ReceiptId", docNum },
                                { "@AdminId", adminId ?? (object)DBNull.Value },
                                { "@CustomerPhone", customerPhone },
                                { "@Notes", paymentNotes }
                            };

                            code2.DatabaseInsertSafe(conn, insertPaymentQuery, paymentParams);
                            System.Diagnostics.Debug.WriteLine($"Created Payment_History for Voucher Receipt: {docNum}");
                        }
                        catch (Exception ex)
                        {
                            code2.Logs(conn, "Payment_History Insert Error (Voucher)",
                                ex.Message + " - " + ex.StackTrace, "SYSTEM");
                            // Don't fail receipt creation if payment history fails
                        }
                    }

                    string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
                    try
                    {
                        System.IO.Directory.CreateDirectory(path + "\\" + Year);
                        System.IO.Directory.CreateDirectory(path + "\\" + Year + "\\" + Month);
                    }
                    catch (Exception ex)
                    {

                    }

                    DataTable dtbusinessinfo = code.DatabaseQuery(conn, "Select * from Business_Info left join Customer_Type on Business_Type_ID = Customer_Type.ID left join Address on Address.ID = Address_ID");

                    // SECURE: Receipt lookup with parameterized query
                    var receiptReportParams = new Dictionary<string, object>
                    {
                        { "@RecNumber", RecNumber }
                    };

                    dtReceipt = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM [Account_Receipt] " +
                        "LEFT JOIN Reservation ON Reservation.ID = Reservation_ID " +
                        "WHERE Account_Receipt.ID = @RecNumber",
                        receiptReportParams);

                    string uid = dtReceipt.Rows[0]["UID"].ToString();

                    // SECURE: Receipt detail lookup with parameterized query
                    var receiptDetailReportParams = new Dictionary<string, object>
                    {
                        { "@RecNumber", RecNumber }
                    };

                    DataTable dtReceiptDetail = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM [Account_Receipt_Detail] " +
                        "INNER JOIN Account_ProductType ON Account_ProductType.ID = ProductType_ID " +
                        "WHERE Receipt_ID = @RecNumber " +
                        "ORDER BY Number ASC",
                        receiptDetailReportParams);



                    //GridView1.DataSource = dt;
                    //GridView1.DataBind();

                    DataTable dtSignature = new DataTable();
                    try
                    {
                        dtSignature.Columns.Add("AuthorizeName");
                        dtSignature.Columns.Add("AuthorizeSignaturePath");
                        dtSignature.Columns.Add("CreatedName");
                        dtSignature.Columns.Add("CreatedSignaturePath");
                    }
                    catch { }

                    string Signaturepath = System.Configuration.ConfigurationSettings.AppSettings["StaffSignatureFolderPath"].ToString();
                    DataTable dtApprover = code.DatabaseQuery(conn, "Select * from Admin Where IsCEO = 'True'");
                    string ApproverFullName = dtApprover.Rows[0]["FirstName"].ToString() + " " + dtApprover.Rows[0]["LastName"].ToString();

                    // SECURE: Admin/Creator lookup with parameterized query
                    var creatorParams = new Dictionary<string, object>
                    {
                        { "@UserID", Session["UserID"].ToString() }
                    };

                    DataTable dtCreator = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Admin WHERE ID = @UserID",
                        creatorParams);

                    string CreatorFullName = dtCreator.Rows[0]["FirstName"].ToString() + " " + dtCreator.Rows[0]["LastName"].ToString();

                    dtSignature.Rows.Add(ApproverFullName, "File:\\" + Signaturepath + "\\" + ApproverFullName.ToLower() + ".png", CreatorFullName, "File:\\" + Signaturepath + "\\" + CreatorFullName.ToLower() + ".png");

                    DataTable dtCustomerReport = new DataTable();
                    dtCustomerReport = dtcustomer.Copy();
                    DataTable dtBusinessinfoReport = new DataTable();
                    dtBusinessinfoReport = dtbusinessinfo.Copy();

                    try
                    {


                        if (CheckBox3.Checked == true)
                        {
                            dtCustomerReport.Rows[0]["FullName"] = "ประสงค์ไม่รับใบกำกับภาษี";
                            dtCustomerReport.Rows[0]["Address"] = "";
                            dtCustomerReport.Rows[0]["IDNumber"] = "";
                            dtCustomerReport.Rows[0]["MobilePhone"] = "";
                            dtCustomerReport.Rows[0]["Email"] = "";
                        }
                        else
                        {

                            dtcustomer.Rows[0]["PostalCode"] = TextBox16.Text;
                            if (DropDownList8.SelectedIndex == 0 && TextBox7.Text == "00000")
                            {
                                dtCustomerReport.Rows[0]["FullName"] = TextBox10.Text;
                            }
                            else if (DropDownList8.SelectedIndex == 0 && Convert.ToInt32(TextBox7.Text) > 0)
                            {
                                dtCustomerReport.Rows[0]["FullName"] = TextBox10.Text + " สาขาที่ " + TextBox7.Text;
                            }
                            else
                            {
                                dtCustomerReport.Rows[0]["FullName"] = TextBox10.Text;
                            }
                            try
                            {

                                if (DropDownList5.SelectedValue.Contains("กรุงเทพ"))
                                {
                                    dtCustomerReport.Rows[0]["Address"] = TextBox11.Text + " " + TextBox18.Text + " แขวง " + DropDownList7.SelectedValue + " เขต " + DropDownList6.SelectedValue + " " + DropDownList5.SelectedValue + " " + dtcustomer.Rows[0]["PostalCode"].ToString();
                                }
                                else
                                {
                                    dtCustomerReport.Rows[0]["Address"] = TextBox11.Text + " " + TextBox18.Text + " ต." + DropDownList7.SelectedValue + " อ." + DropDownList6.SelectedValue + " จ." + DropDownList5.SelectedValue + " " + dtcustomer.Rows[0]["PostalCode"].ToString();
                                }
                            }
                            catch
                            {
                                dtCustomerReport.Rows[0]["Address"] = dtcustomer.Rows[0]["Address"].ToString();
                            }

                            dtCustomerReport.Rows[0]["IDNumber"] = TextBox12.Text;
                            dtCustomerReport.Rows[0]["MobilePhone"] = TextBox13.Text;
                            dtCustomerReport.Rows[0]["Email"] = TextBox17.Text;
                        }


                        try
                        {
                            if (dtbusinessinfo.Rows[0]["Province"].ToString().Contains("กรุงเทพ"))
                            {
                                dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString() + " " + dtbusinessinfo.Rows[0]["Address1"].ToString() + " แขวง " + dtbusinessinfo.Rows[0]["SubDistrict"].ToString() + " เขต " + dtbusinessinfo.Rows[0]["District"].ToString() + " " + dtbusinessinfo.Rows[0]["Province"].ToString() + " " + dtbusinessinfo.Rows[0]["PostalCode"].ToString();
                            }
                            else
                            {
                                dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString() + " " + dtbusinessinfo.Rows[0]["Address1"].ToString() + " ต." + dtbusinessinfo.Rows[0]["SubDistrict"].ToString() + " อ." + dtbusinessinfo.Rows[0]["District"].ToString() + " จ." + dtbusinessinfo.Rows[0]["Province"].ToString() + " " + dtbusinessinfo.Rows[0]["PostalCode"].ToString();
                            }
                        }
                        catch
                        {
                            dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString();
                        }
                    }
                    catch { }

                    try
                    {
                        Account.Report.DataSet1 dataSet1 = new Account.Report.DataSet1();
                        dataSet1.Tables.Add(dtBusinessinfoReport);
                        ReportViewer2.LocalReport.DisplayName = "Receipt";
                        ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet1", dtBusinessinfoReport));
                        ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet2", dtCustomerReport));
                        ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet3", dtReceiptDetail));
                        ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet4", dtReceipt));
                        ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet5", dtSignature));
                        try
                        {

                            //     var deviceInfo = @"<DeviceInfo>
                            // <EmbedFonts>None</EmbedFonts>
                            //</DeviceInfo>";


                            Warning[] warnings;
                            string[] streamids;
                            string mimeType;
                            string encoding;
                            string filenameExtension;

                            byte[] bytes = ReportViewer2.LocalReport.Render(
                                "PDF", null, out mimeType, out encoding, out filenameExtension,
                                out streamids, out warnings);
                            if (File.Exists(path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + ".pdf"))
                            {
                                File.Delete(path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + ".pdf");
                            }
                            using (FileStream fs = new FileStream(path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + ".pdf", FileMode.Append))
                            {

                                fs.Write(bytes, 0, bytes.Length);
                            }
                        }
                        catch (Exception ex)
                        {
                        }

                        if (CheckBox5.Checked == true)
                        {
                            try
                            {
                                string xmlFilePath = path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + ".xml";
                                string xmlString = System.IO.File.ReadAllText(ConfigurationSettings.AppSettings["BaseFolderPath"].ToString() + "\\Resources\\template.xml");
                                xmlString = xmlString.Replace("*invoice_id", docNum);
                                xmlString = xmlString.Replace("*invoice_name", "ใบเสร็จรับเงิน/ใบกำกับภาษี");
                                xmlString = xmlString.Replace("*invoice_typecode", "T03");
                                xmlString = xmlString.Replace("*invoice_issue_date", Convert.ToDateTime(dtReceipt.Rows[0]["Created_Date"].ToString()).ToString("yyyy-MM-dd") + "T00:00:00.000");
                                xmlString = xmlString.Replace("*invoice_purpose", "");
                                xmlString = xmlString.Replace("*invoice_Purpose_code", "");
                                xmlString = xmlString.Replace("*invoice_create_date", Convert.ToDateTime(dtReceipt.Rows[0]["Created_Date"].ToString()).ToString("yyyy-MM-dd") + "T00:00:00.000");
                                xmlString = xmlString.Replace("*invoice_remark", "");

                                try
                                {
                                    xmlString = xmlString.Replace("*seller_type", dtbusinessinfo.Rows[0]["Customer_Code"].ToString());
                                    if (dtbusinessinfo.Rows[0]["Customer_Code"].ToString() == "TXID")
                                    {
                                        xmlString = xmlString.Replace("*seller_taxid", dtbusinessinfo.Rows[0]["LegalEntity_Number"].ToString() + dtbusinessinfo.Rows[0]["Branch_Number"].ToString());
                                    }
                                    else
                                    {
                                        xmlString = xmlString.Replace("*seller_taxid", dtbusinessinfo.Rows[0]["LegalEntity_Number"].ToString());
                                    }
                                }
                                catch
                                {
                                    xmlString = xmlString.Replace("*seller_type", "TXID");
                                    xmlString = xmlString.Replace("*seller_taxid", dtbusinessinfo.Rows[0]["LegalEntity_Number"].ToString());
                                }

                                xmlString = xmlString.Replace("*seller_name", dtbusinessinfo.Rows[0]["Company_Name"].ToString());

                                xmlString = xmlString.Replace("*seller_DefinedCITradeContact", dtbusinessinfo.Rows[0]["Email"].ToString());
                                xmlString = xmlString.Replace("*seller_PhoneNumber", dtbusinessinfo.Rows[0]["Phone_Number"].ToString());
                                xmlString = xmlString.Replace("*seller_zipcode", dtbusinessinfo.Rows[0]["PostalCode"].ToString());
                                xmlString = xmlString.Replace("*seller_address1", dtbusinessinfo.Rows[0]["Address"].ToString() + " " + dtbusinessinfo.Rows[0]["Address1"].ToString() + " " + dtbusinessinfo.Rows[0]["SubDistrict"].ToString() + " " + dtbusinessinfo.Rows[0]["District"].ToString() + " " + dtbusinessinfo.Rows[0]["Province"].ToString() + " " + dtbusinessinfo.Rows[0]["PostalCode"].ToString());
                                xmlString = xmlString.Replace("*seller_address2", "");
                                xmlString = xmlString.Replace("*seller_cityname", dtbusinessinfo.Rows[0]["Address_Code"].ToString().Substring(0, 4));
                                xmlString = xmlString.Replace("*seller_city_subdivision_name", dtbusinessinfo.Rows[0]["Address_Code"].ToString().Substring(0, 6));
                                xmlString = xmlString.Replace("*seller_country", "TH");
                                xmlString = xmlString.Replace("*sellercountry_subdivision_id", dtbusinessinfo.Rows[0]["Address_Code"].ToString().Substring(0, 2));
                                xmlString = xmlString.Replace("*seller_building_name", dtbusinessinfo.Rows[0]["Address"].ToString());
                                xmlString = xmlString.Replace("*buyer_name", dtcustomer.Rows[0]["FullName"].ToString());

                                try
                                {


                                    xmlString = xmlString.Replace("*buyer_taxtype", dtcustomer.Rows[0]["Customer_Code"].ToString());


                                    if (dtcustomer.Rows[0]["Customer_Code"].ToString() == "TXID")
                                    {
                                        string bnumber = "00000";
                                        if (TextBox7.Text.Length == 5)
                                        {
                                            bnumber = dtcustomer.Rows[0]["Branch_Number"].ToString();
                                        }
                                        xmlString = xmlString.Replace("*buyer_taxid", dtcustomer.Rows[0]["IDNumber"].ToString() + bnumber);
                                    }
                                    else
                                    {
                                        xmlString = xmlString.Replace("*buyer_taxtype", "NIDN");
                                        xmlString = xmlString.Replace("*buyer_taxid", dtcustomer.Rows[0]["IDNumber"].ToString());
                                    }
                                }
                                catch
                                {
                                    xmlString = xmlString.Replace("*buyer_taxtype", "NIDN");
                                    xmlString = xmlString.Replace("*buyer_taxid", dtcustomer.Rows[0]["IDNumber"].ToString());
                                }


                                xmlString = xmlString.Replace("*buyer_DefinedCITradeContact", TextBox17.Text);
                                xmlString = xmlString.Replace("*buyer_zipcode", dtcustomer.Rows[0]["PostalCode"].ToString());
                                xmlString = xmlString.Replace("*buyer_address", dtcustomer.Rows[0]["Address"].ToString() + " " + dtcustomer.Rows[0]["Address1"].ToString() + " " + dtcustomer.Rows[0]["SubDistrict"].ToString() + " " + dtcustomer.Rows[0]["District"].ToString() + " " + dtcustomer.Rows[0]["Province"].ToString() + " " + dtcustomer.Rows[0]["PostalCode"].ToString());
                                xmlString = xmlString.Replace("*buyer_address2", "");
                                xmlString = xmlString.Replace("*buyer_cityname", dtcustomer.Rows[0]["Address_Code"].ToString().Substring(0, 4));
                                xmlString = xmlString.Replace("*buyer_city_subdivision_name", dtcustomer.Rows[0]["Address_Code"].ToString().Substring(0, 6));
                                xmlString = xmlString.Replace("*buyer_country", "TH");
                                xmlString = xmlString.Replace("*buyercountry_subdivision_id", dtcustomer.Rows[0]["Address_Code"].ToString().Substring(0, 2));
                                xmlString = xmlString.Replace("*buyer_building_name", dtcustomer.Rows[0]["Address"].ToString());
                                xmlString = xmlString.Replace("*reference", "");
                                xmlString = xmlString.Replace("*buyer_contact_person", "");
                                xmlString = xmlString.Replace("*currency", "THB");
                                xmlString = xmlString.Replace("*invoice_tax_code", "VAT");
                                xmlString = xmlString.Replace("*invoice_tax_rate", "7");
                                xmlString = xmlString.Replace("*invoice_basis_amount", dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"].ToString());
                                xmlString = xmlString.Replace("*calculated_amount", dtReceipt.Rows[0]["Vat"].ToString());
                                xmlString = xmlString.Replace("*invoice_discountallowance", "");
                                xmlString = xmlString.Replace("*invoice_serviceallowance", "");
                                xmlString = xmlString.Replace("*invoice_line_total", dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"].ToString());
                                xmlString = xmlString.Replace("*tax_basis_total_amount", dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"].ToString());
                                xmlString = xmlString.Replace("*invoice_tax_total", dtReceipt.Rows[0]["Vat"].ToString());
                                xmlString = xmlString.Replace("*invoice_grand_total", dtReceipt.Rows[0]["Total_Amount"].ToString());
                                xmlString = xmlString.Replace("*item", "ค่าขายVoucherที่พัก");
                                xmlString = xmlString.Replace("*invoice_billedquantity", "1");

                                System.IO.File.WriteAllText(xmlFilePath, xmlString, System.Text.Encoding.UTF8);
                            }
                            catch { }

                            try
                            {
                                {


                                    PDFA3Invoice pdf = new PDFA3Invoice();
                                    string pdfFilePath = path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + ".pdf";
                                    string xmlFilePath = path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + ".xml";

                                    string xmlFileName = "ETDA-invoice.xml";


                                    string xmlVersion = "1.0";
                                    string documentID = docNum;
                                    string documentOID = "";

                                    string outputPath = path + "\\" + Year + "\\" + Month + "\\" + docNum + "_" + uid + "_etax.pdf";

                                    pdf.CreatePDFA3Invoice(pdfFilePath, xmlFilePath, xmlFileName, xmlVersion, documentID, documentOID, outputPath, "Tax Invoice");

                                    if (CheckBox5.Checked == true)
                                    {

                                        DateTime docDate = DateTime.Now;

                                        try
                                        {

                                        }
                                        catch
                                        {

                                        }
                                        //DataTable dtReceipt = code.DatabaseQuery(conn, "SELECT  [ID] FROM [Account_Receipt] Where RESERVATION_ID = '" + Reservation_ID + "'");

                                        //string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
                                        //string pdfpath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + dtReceipt.Rows[0]["ID"].ToString() + "_etax.pdf";

                                        //string pdfFilePath = pdfpath;
                                        byte[] bytes = System.IO.File.ReadAllBytes(outputPath);
                                        Attachment[] dataall = new Attachment[1];
                                        MemoryStream pdf2 = new MemoryStream(bytes);
                                        Attachment data = new Attachment(pdf2, dtReceipt.Rows[0]["ID"].ToString() + "_" + uid + "_etax.pdf");
                                        dataall[0] = data;

                                        string docCreateThaiDate = "";

                                        if (docDate.Day.ToString().Length > 1)
                                        {
                                            docCreateThaiDate += docDate.Day.ToString();
                                        }
                                        else
                                        {
                                            docCreateThaiDate += "0" + docDate.Day.ToString();
                                        }

                                        if (docDate.Month.ToString().Length > 1)
                                        {
                                            docCreateThaiDate += docDate.Month.ToString();
                                        }
                                        else
                                        {
                                            docCreateThaiDate += "0" + docDate.Month.ToString();
                                        }


                                        if (Convert.ToInt32(docDate.Year.ToString()) > 2500)
                                        {
                                            docCreateThaiDate += docDate.Year.ToString();
                                        }
                                        else
                                        {
                                            docCreateThaiDate += (Convert.ToInt32(docDate.Year.ToString()) + 543).ToString();
                                        }



                                        string subject = "[" + docCreateThaiDate + "][INV][" + dtReceipt.Rows[0]["ID"].ToString() + "]";
                                        string body = "เรียน ลูกค้าผู้มีอุปการะคุณ <br /><br /> หจก.แอม แฮปปี้เนส (Take Time) ได้แนบใบกำกับภาษี/ใบเสร็จรับเงินมาพร้อมกับอีเมล์ฉบับนี้ ท่านสามารถเปิดดูได้โดยคลิกไฟล์แนบ (PDF File)<br />ขอแสดงความนับถือ<br /> หจก.แอม แฮปปี้เนส (Take Time) ";

                                        NumberHelper.SendEmail(ConfigurationSettings.AppSettings["SMTP"].ToString(), Convert.ToInt32(ConfigurationSettings.AppSettings["SMTP_Port"].ToString()), Convert.ToBoolean(ConfigurationSettings.AppSettings["SMTP_EnableSsl"].ToString()), Convert.ToBoolean(ConfigurationSettings.AppSettings["SMTP_UseDefaultCredentials"].ToString()), ConfigurationSettings.AppSettings["Email_From"].ToString(), ConfigurationSettings.AppSettings["Email_Password_From"].ToString(), TextBox17.Text, ConfigurationSettings.AppSettings["Email_CC"].ToString(), subject, body, dataall);


                                    }
                                }
                            }
                            catch
                            {

                            }
                            //ReportViewer2.LocalReport.Refresh();

                        }
                    }

                    catch { }
                }

                

                string createddate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                for (int i = 0;i<Convert.ToInt32(TextBox19.Text);i++)
                {
                    // SECURE: Rate plan group lookup with parameterized query
                    var ratePlanGroupNameParams = new Dictionary<string, object>
                    {
                        { "@GroupName", GridView1.Rows[0].Cells[1].Text }
                    };

                    DataTable dtDetails = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Accommodation_RatePlan_Group WHERE Group_Name = @GroupName",
                        ratePlanGroupNameParams);

                    string vnumber = createVoucherNumber(dtDetails.Rows[0]["AccomGroupID"].ToString(), Year, Month, Day);
                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox13.Text + ".jpg"))
                    {
                        try
                        {
                            File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox13.Text + ".jpg", AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + vnumber +".jpg");
                        }
                        catch
                        {

                        }
                    }

                    // SECURE: Voucher INSERT with parameterized query
                    var voucherInsertParams = new Dictionary<string, object>
                    {
                        { "@VoucherNumber", vnumber },
                        { "@SellPrice", TextBox6.Text },
                        { "@CreatedDate", createddate },
                        { "@CustomerID", dtcustomer.Rows[0]["ID"].ToString() },
                        { "@ReceiptID", CheckBox4.Checked ? "" : RecNumber },
                        { "@Remark", TextBox20.Text ?? "" },
                        { "@ExpiredDate", Convert.ToDateTime(TextBox21.Text) }
                    };

                    code.DatabaseInsertSafe(conn,
                        "INSERT INTO [dbo].[Voucher] ([Voucher_Number],[Sell_Price],[Created_Date],[Customer_ID],Receipt_ID,Remark,Expired_Date) " +
                        "VALUES (@VoucherNumber,@SellPrice,@CreatedDate,@CustomerID,@ReceiptID,@Remark,@ExpiredDate)",
                        voucherInsertParams);

                    for (int j = 0; j < GridView1.Rows.Count; j++)
                    {
                        // SECURE: GroupID lookup with parameterized query
                        var groupIdParams = new Dictionary<string, object>
                        {
                            { "@GroupName", GridView1.Rows[j].Cells[1].Text }
                        };

                        string groupid = code.DatabaseQuerySafe(conn,
                            "SELECT [GroupID] FROM Accommodation_RatePlan_Group WHERE Group_Name = @GroupName",
                            groupIdParams).Rows[0][0].ToString();

                        // SECURE: Voucher_RatePlan_Group INSERT with parameterized query
                        var voucherRatePlanParams = new Dictionary<string, object>
                        {
                            { "@VoucherNumber", vnumber },
                            { "@GroupID", groupid },
                            { "@PriceTo", GridView1.Rows[j].Cells[2].Text }
                        };

                        code.DatabaseInsertSafe(conn,
                            "INSERT INTO [dbo].[Voucher_RatePlan_Group] ([Voucher_Number],[RatePlan_GroupID],[PriceTo]) " +
                            "VALUES (@VoucherNumber,@GroupID,@PriceTo)",
                            voucherRatePlanParams);

                    }
                }
                if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox13.Text + ".jpg"))
                {
                    File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox13.Text + ".jpg");
                }

                    createddate = createddate.Replace(" ", "@");
                Response.Redirect("/Voucher/Voucher_Confirmed?id="+createddate);
            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาระบุข้อมูลให้ครบถ้วน');", true);
            }
            
        }

        // ✨ MIGRATED: cleantext() has been replaced with ValidationHelper.CleanText()
        // See ValidationHelper class in Take_Time_BangPhra.Class namespace

        protected void Button4_Click(object sender, EventArgs e)
        {
            
          
        }

        protected void GridView2_RowDeleted(object sender, GridViewDeletedEventArgs e)
        {
        }

        protected void GridView2_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
          
        }

        public string createVoucherNumber(string AccomGroupID,string Year, string Month, string Day)
        {
            string output = "";
            Year = Year.Substring(Year.Length - 2);

            if (Convert.ToInt32(Month) < 10)
            {
                Month = "0" + Month;
            }

            if (Convert.ToInt32(Day) < 10)
            {
                Day = "0" + Day;
            }
            
            int Number = 0;
            string NumberAccom = "";
                Number = Convert.ToInt32(AccomGroupID);
                if (Number < 10)
                {
                    NumberAccom = "0" + Number.ToString();
                }
            
            
            //Random random = new Random();
            //int randomNumber = random.Next(1, 999);
            //string randomNum = "";
            //if(randomNumber < 10)
            //{
            //    randomNum = "00"+randomNumber.ToString();
            //}
            //else if (randomNumber < 100)
            //{
            //    randomNum = "0" + randomNumber.ToString();
            //}
            //else
            //{
            //    randomNum = randomNumber.ToString();
            //}
            output = "V" +NumberAccom+ Year + Month + Day+ GenerateRandomString(4);
            int a = 0;
            while(a == 0)
            {
                // SECURE: Voucher number check with parameterized query
                var voucherCheckParams = new Dictionary<string, object>
                {
                    { "@VoucherNumber", output }
                };

                DataTable dtVoucherCheck = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Voucher WHERE Voucher_Number = @VoucherNumber",
                    voucherCheckParams);

                if(dtVoucherCheck.Rows.Count > 0)
                {
                    output = "V" + NumberAccom + Year + Month + Day + GenerateRandomString(4);
                }
                else
                {
                    a = 1;
                }
            }
            return output;
        }
        private static Random random = new Random();

        public static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }


        protected void CheckBox3_CheckedChanged(object sender, EventArgs e)
        {
            if(CheckBox3.Checked == true)
            {
                CheckBox4.Checked = false;
                CheckBox4.DataBind();
               // TextBox10.Text = "ประสงค์ไม่รับใบกำกับภาษี";
               // TextBox11.Text = "";
               // TextBox12.Text = "";
               // TextBox13.Text = "";
            }
        }

       

        protected void Button4_Click1(object sender, EventArgs e)
        {
            string uid = Request.QueryString["uid"];
            string path = ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
            string Imagespath = ConfigurationSettings.AppSettings["ImagesFolderPath"].ToString();

            // SECURE: Receipt lookup with parameterized query
            var receiptByUidParams = new Dictionary<string, object>
            {
                { "@UID", uid ?? "" }
            };

            DataTable dtRec = code.DatabaseQuerySafe(conn,
                "SELECT * FROM Account_Receipt WHERE Status = 'Normal' AND UID = @UID",
                receiptByUidParams);

            string id = dtRec.Rows[0]["ID"].ToString();
            for (int i = 0; i < dtRec.Rows.Count; i++)
            {
                // SECURE: Receipt UPDATE with parameterized query
                var receiptUpdateParams = new Dictionary<string, object>
                {
                    { "@ID", id }
                };

                code.DatabaseInsertSafe(conn,
                    "UPDATE [dbo].[Account_Receipt] SET [Status] = 'Cancel' WHERE ID = @ID",
                    receiptUpdateParams);

                DateTime createdDate = Convert.ToDateTime(dtRec.Rows[i]["Created_Date"].ToString());

                string inputPdfStreampath = "";
                string outputPdfStreampath = "";
                if(File.Exists(path + "\\" + createdDate.Year.ToString() + "\\" + createdDate.Month + "\\" + dtRec.Rows[i]["ID"].ToString() + "_"+uid+".pdf"))
                {
                    inputPdfStreampath = path + "\\" + createdDate.Year.ToString() + "\\" + createdDate.Month + "\\" + dtRec.Rows[i]["ID"].ToString() + "_" + uid + ".pdf";
                }
                else
                {
                    inputPdfStreampath = path + "\\" + createdDate.Year.ToString() + "\\" + createdDate.Month + "\\" + dtRec.Rows[i]["ID"].ToString() + ".pdf";
                }

                if (File.Exists(path + "\\" + createdDate.Year.ToString() + "\\" + createdDate.Month + "\\" + dtRec.Rows[i]["ID"].ToString() +"_"+uid+ "_Cancel.pdf"))
                {
                    outputPdfStreampath = path + "\\" + createdDate.Year.ToString() + "\\" + createdDate.Month + "\\" + dtRec.Rows[i]["ID"].ToString() + "_" + uid + "_Cancel.pdf";
                }
                else
                {
                    outputPdfStreampath = path + "\\" + createdDate.Year.ToString() + "\\" + createdDate.Month + "\\" + dtRec.Rows[i]["ID"].ToString() + "_Cancel.pdf";
                }
                using (Stream inputPdfStream = new FileStream(inputPdfStreampath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (Stream inputImageStream = new FileStream(Imagespath + "\\Cancel.png", FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (Stream outputPdfStream = new FileStream(outputPdfStreampath, FileMode.Append, FileAccess.Write, FileShare.None))
                    {
                        var reader = new PdfReader(inputPdfStream);
                        var stamper = new PdfStamper(reader, outputPdfStream);
                        var pdfContentByte = stamper.GetOverContent(1);

                        iTextSharp.text.Image image = iTextSharp.text.Image.GetInstance(inputImageStream);
                        image.SetAbsolutePosition(100, 100);
                        pdfContentByte.AddImage(image);
                        stamper.Close();
                    }
                File.Delete(inputPdfStreampath);
                try
                {
                    File.Delete(inputPdfStreampath + "_etax.pdf");
                }
                catch
                {

                }

                Response.Redirect("/Account/Receipt");
            }
        }

        protected void Button5_Click(object sender, EventArgs e)
        {
            TextBox16.Enabled = false;
            getAddress("SELECT DISTINCT [Province] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' order by Province ASC", "SELECT DISTINCT [District] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' order by SubDistrict ASC");
        }

        public void getAddress(string commp, string commd, string commsd)
        {
            string Command = Request.QueryString["Command"];
            string ID = Request.QueryString["ID"];
            DataTable dtProvince = code.DatabaseQuery(conn, commp);
            DataTable dtDistrict = code.DatabaseQuery(conn, commd);
            DataTable dtSubDistrict = code.DatabaseQuery(conn, commsd);
            try
            {
                if (dtProvince.Rows.Count <= 0)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ไม่พบหมายเลขไปรษณีย์ที่คุณระบุ');", true);
                    TextBox16.Enabled = true;
                }
                else
                {
                    if (Button2.Enabled == true || Command == "View" || Command == "Edit")
                    {
                        List<string> ddl = new List<string>();

                        for (int i = 0; i < dtProvince.Rows.Count; i++)
                        {
                            ddl.Add(dtProvince.Rows[i][0].ToString());
                        }
                        DropDownList5.DataSource = ddl;
                        DropDownList5.DataBind();
                        //DropDownList5.SelectedIndex = 0;

                        ddl.Clear();

                        for (int i = 0; i < dtDistrict.Rows.Count; i++)
                        {
                            ddl.Add(dtDistrict.Rows[i][0].ToString());
                        }
                        DropDownList6.DataSource = ddl;
                        DropDownList6.DataBind();
                        //DropDownList6.SelectedIndex = 0;

                        ddl.Clear();

                        for (int i = 0; i < dtSubDistrict.Rows.Count; i++)
                        {
                            ddl.Add(dtSubDistrict.Rows[i][0].ToString());
                        }
                        DropDownList7.DataSource = ddl;
                        DropDownList7.DataBind();
                        //DropDownList7.SelectedIndex = 0;
                    }
                    else { }
                }
            }
            catch { }
        }

        protected void TextBox16_TextChanged(object sender, EventArgs e)
        {
            Button5_Click(null, null);
        }

        protected void Button6_Click(object sender, EventArgs e)
        {
            TextBox16.Enabled = true;
            TextBox16.Text = string.Empty;
            DropDownList5.Items.Clear();
            DropDownList6.Items.Clear();
            DropDownList7.Items.Clear();
        }

        // ✨ MIGRATED: CheckAddressID() has been replaced with AddressHelper.GetAddressIdString()
        // See AddressHelper class in Take_Time_BangPhra.Class namespace

        protected void DropDownList8_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (DropDownList8.SelectedValue == "1")
            {
                TextBox7.Visible = true;
            }
            else
            {
                TextBox7.Visible = false;
            }
        }

        protected void DropDownList5_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (TextBox16.Enabled == false)
            {
                getAddress("SELECT DISTINCT [Province] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' AND Province = N'" + DropDownList5.SelectedValue + "' order by Province ASC", "SELECT DISTINCT [District] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' AND Province = N'" + DropDownList5.SelectedValue + "' order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' AND Province = N'" + DropDownList5.SelectedValue + "' order by SubDistrict ASC");
            }
            else
            {
                getAddress("SELECT DISTINCT [Province] FROM [Address] Where Province = N'" + DropDownList5.SelectedValue + "' order by Province ASC", "SELECT DISTINCT [District] FROM [Address] Where Province = N'" + DropDownList5.SelectedValue + "' order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] Where Province = N'" + DropDownList5.SelectedValue + "' order by SubDistrict ASC");
            }
        }

        protected void DropDownList6_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (TextBox16.Enabled == false)
            {
                getAddress("SELECT DISTINCT [Province] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' AND District = N'" + DropDownList6.SelectedValue + "' order by Province ASC", "SELECT DISTINCT [District] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' AND District = N'" + DropDownList6.SelectedValue + "' order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' AND District = N'" + DropDownList6.SelectedValue + "' order by SubDistrict ASC");
            }
            else
            {
                getAddress("SELECT DISTINCT [Province] FROM [Address] Where District = N'" + DropDownList6.SelectedValue + "' order by Province ASC", "SELECT DISTINCT [District] FROM [Address] Where District = N'" + DropDownList6.SelectedValue + "' order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] Where District = N'" + DropDownList6.SelectedValue + "' order by SubDistrict ASC");
            }
        }

        protected void CheckBox5_CheckedChanged(object sender, EventArgs e)
        {
            if (CheckBox5.Checked == true)
            {
                CheckBox4.Checked = false;
                CheckBox4.DataBind();
               // TextBox10.Text = "ประสงค์ไม่รับใบกำกับภาษี";
               // TextBox11.Text = "";
               // TextBox12.Text = "";
               // TextBox13.Text = "";
            }
            else
            {
                
            }
        }

        protected void TextBox13_TextChanged(object sender, EventArgs e)
        {
            if (TextBox13.Text.Length > 8)
            {
               // SECURE: Customer lookup with parameterized query
               var customerLookupParams = new Dictionary<string, object>
               {
                   { "@MobilePhone", TextBox13.Text ?? "" }
               };

               DataTable dtcustomer = code.DatabaseQuerySafe(conn,
                   "SELECT * FROM [Taketime].[dbo].[Customer] WHERE MobilePhone = @MobilePhone",
                   customerLookupParams);

                if (dtcustomer.Rows.Count > 0)
                {
                    try //Address
                    {
                        TextBox10.Text = dtcustomer.Rows[0]["FullName"].ToString();
                        TextBox11.Text = dtcustomer.Rows[0]["Address"].ToString();
                        TextBox12.Text = dtcustomer.Rows[0]["IDNumber"].ToString();
                        TextBox13.Text = dtcustomer.Rows[0]["MobilePhone"].ToString();
                        TextBox17.Text = dtcustomer.Rows[0]["Email"].ToString();
                        TextBox18.Text = dtcustomer.Rows[0]["Address1"].ToString();
                        try
                        {
                            TextBox16.Text = dtcustomer.Rows[0]["PostalCode"].ToString();

                            DropDownList5.ClearSelection();
                            DropDownList5.Items.FindByText(dtcustomer.Rows[0]["Province"].ToString()).Selected = true;
                            DropDownList5.SelectedIndex = DropDownList5.Items.IndexOf(DropDownList5.Items.FindByText(dtcustomer.Rows[0]["Province"].ToString()));
                            DropDownList6.ClearSelection();
                            DropDownList6.Items.FindByText(dtcustomer.Rows[0]["District"].ToString()).Selected = true;
                            DropDownList6.SelectedIndex = DropDownList6.Items.IndexOf(DropDownList6.Items.FindByText(dtcustomer.Rows[0]["District"].ToString()));
                            DropDownList7.ClearSelection();
                            DropDownList7.Items.FindByText(dtcustomer.Rows[0]["SubDistrict"].ToString()).Selected = true;
                            DropDownList7.SelectedIndex = DropDownList7.Items.IndexOf(DropDownList7.Items.FindByText(dtcustomer.Rows[0]["SubDistrict"].ToString()));
                        }
                        catch { }

                        DropDownList8.ClearSelection();
                        DropDownList8.Items.FindByValue(dtcustomer.Rows[0]["Customer_Type_ID"].ToString()).Selected = true;
                        DropDownList8.SelectedIndex = DropDownList8.Items.IndexOf(DropDownList8.Items.FindByValue(dtcustomer.Rows[0]["Customer_Type_ID"].ToString()));
                        DropDownList8.DataBind();
                        if (DropDownList8.SelectedIndex == 0)
                        {
                            TextBox7.Visible = true;
                            TextBox7.Text = dtcustomer.Rows[0]["Branch_Number"].ToString();
                        }
                        else
                        {
                            TextBox7.Visible = false;
                        }
                        DropDownList4.SelectedIndex = DropDownList4.Items.IndexOf(DropDownList4.Items.FindByValue("1"));
                        DropDownList4.DataBind();
                    }
                    catch { }
                }
            }


        }

        protected void TextBox3_TextChanged(object sender, EventArgs e)
        {

        }

        protected void TextBox6_TextChanged(object sender, EventArgs e)
        {
            if(TextBox6.Text.Length > 0)
            {
                double totalAmount = Convert.ToDouble(TextBox6.Text) * Convert.ToInt32(TextBox19.Text);

                // SECURE: VAT percent lookup with parameterized query
                var vatLookupParams = new Dictionary<string, object>
                {
                    { "@VatTypeID", DropDownList4.SelectedValue }
                };

                int vatPercent = Convert.ToInt32(code.DatabaseQuerySafe(conn,
                    "SELECT Vat_Percent FROM Account_Vat_Type WHERE Status = 'True' AND ID = @VatTypeID",
                    vatLookupParams).Rows[0][0].ToString());

                double AmountExcludeVat = ((totalAmount * 100) / (100 + vatPercent) );
                double vat = totalAmount - AmountExcludeVat;
                TextBox3.Text = NumberHelper.TwoDecimalPoints(AmountExcludeVat).ToString();
                TextBox4.Text = NumberHelper.TwoDecimalPoints(vat).ToString();
                TextBox1.Text = NumberHelper.TwoDecimalPoints(totalAmount).ToString();
            }
        }

        protected void CheckBox4_CheckedChanged(object sender, EventArgs e)
        {
            if(CheckBox4.Checked == true)
            {
                CheckBox5.Checked = false;
                CheckBox5.DataBind();
            }
        }

        protected void TextBox19_TextChanged(object sender, EventArgs e)
        {
            TextBox6_TextChanged(null, null);
        }

        protected void Button7_Click(object sender, EventArgs e)
        {
            if (TextBox13.Text.Length > 0)
            {
                if (FileUpload1.HasFile)
                {
                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox13.Text + ".jpg"))
                    {
                        File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox13.Text + ".jpg");
                    }

                    string FileSaveWithPath = "";
                    string filename = TextBox13.Text + ".jpg";
                    FileSaveWithPath = Server.MapPath("\\Upload\\Slip\\" + filename);
                    FileUpload1.SaveAs(FileSaveWithPath);
                    
                    Image1.ImageUrl = "\\Upload\\Slip\\" + filename;
                    Image1.DataBind();
                }
                else
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเลือกไฟล์ที่ต้องการจะอัพโหลดก่อน');", true);
                }
            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเระบุเบอร์โทรศัพท์ก่อน');", true);
            }
        }

        protected void TextBox12_TextChanged(object sender, EventArgs e)
        {
            if(TextBox12.Text.Length == 13)
            {
                CheckBox3.Checked = false;
                CheckBox3.DataBind();
            }
        }
    }
}