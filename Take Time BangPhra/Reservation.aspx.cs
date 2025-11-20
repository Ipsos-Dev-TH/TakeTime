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
using Microsoft.Reporting.WebForms;
using ECertificateAPI;
using iTextSharp.text.pdf;
using System.Data.SqlTypes;
using System.Net;
using System.Net.Mail;
using System.Web.Services.Description;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;
using System.Threading;
using Google.Apis.Gmail.v1;
using System.Text;

namespace Take_Time_BangPhra
{
    public partial class Reservation : System.Web.UI.Page
    {
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;


        protected void Page_Load(object sender, EventArgs e)
        {
            string date = Request.QueryString["date"];
            string accom = Request.QueryString["accom"];
            Page.MaintainScrollPositionOnPostBack = true;
            if (!IsPostBack)
            {
                Response.Redirect("./Reserve?command=reserve");
                DataTable dtCustomerType = code.DatabaseQuery(conn, "Select [Customer_Type],ID From Customer_Type");


                for (int q = 0; q < dtCustomerType.Rows.Count; q++)
                {
                    DropDownList8.Items.Add(new ListItem(dtCustomerType.Rows[q][0].ToString(), dtCustomerType.Rows[q][1].ToString()));
                }

                DropDownList8.DataBind();

                DropDownList8.ClearSelection();
                DropDownList8.Items.FindByValue("2").Selected = true;
                DropDownList8.SelectedIndex = DropDownList8.Items.IndexOf(DropDownList8.Items.FindByValue("2"));


                getAddress("SELECT DISTINCT [Province] FROM [Address] order by Province ASC", "SELECT DISTINCT [District] FROM [Address] order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] order by SubDistrict ASC");


                DropDownList1.SelectedIndex = 0;

                try
                {
                    TextBox12.Text = date;
                    TextBox12_TextChanged(null, null);
                    //Convert.ToDateTime(TextBox12.Text) = DateTime.Parse(date);
                    //Calendar1.DataBind();
                    //Calendar1_SelectionChanged(null, null);
                    DataTable dtAccom = (DataTable)Session["dtAccommodation"];
                    for (int j = 0; j < dtAccom.Rows.Count; j++)
                    {
                        if (dtAccom.Rows[j]["ID"].ToString() == accom)
                        {
                            CheckBox chkAccom = GridView1.Rows[j].Cells[0].FindControl("chkSelect") as CheckBox;
                            if (chkAccom != null)
                            {
                                chkAccom.Checked = true;
                            }
                            //chkAccom.Checked = true;
                        }
                    }


                }
                catch { }
                try
                {
                    if (Session["User"].ToString() == "Owner")
                    {
                        TextBox11.Visible = true;
                    }
                }
                catch { }
                Image1.ImageUrl = "./Images/บัญชี.png";
                Image1.DataBind();

            }
            try
            {
                if (Session["permission"].ToString() == "True" )
                {
                    CheckBox4.Visible = true;
                    if (!IsPostBack)
                    {
                        DropDownList2.Enabled = true;
                        DropDownList2.Items.Insert(0, new ListItem("---โปรดเลือกวิธีการชำระ---", "0"));
                    }
                    //TextBox4.Enabled = true;
                    TextBox5.AutoPostBack = false;
                    Button1.Enabled = true;
                    Button4.Visible = true;
                }
                else
                {
                    Session["permission"] = "No";
                    GridView1.Columns[5].Visible = false;
                    GridView2.Columns[5].Visible = false;
                }
            }
            catch
            {
                Session["permission"] = "No";
                GridView1.Columns[5].Visible = false;
                GridView2.Columns[5].Visible = false;
            }

            string command = Request.QueryString["command"];
            string id = Request.QueryString["id"];
            string check = Request.QueryString["check"];

            Session["OldPrice"] = TextBox4.Text;

            if(command == "checkin")
            {
                GridView1.Enabled = false;
                GridView2.Enabled = false;
                TextBox5.Enabled = false;
                Button1.Text = "ยืนยันการเช็คอิน";
                CheckBox1.Visible = false;
                Button1.Enabled = true;
            }
            else if(command == "edit")
            {
                TextBox5.Enabled = false;
                CheckBox2.Visible = true;
                Button1.Text = "ยืนยันการแก้ไข";
                CheckBox1.Visible = false;
                Button1.Enabled = true;
                Label7.Visible = false;
            }
            else if(command == "rentmore")
            {
                TextBox5.Enabled = false;
                CheckBox2.Visible = true;
                CheckBox2.Text = "จ่ายเงินเพิ่ม";
                Button1.Text = "ยืนยันการเช่าเพิ่ม";
                CheckBox1.Visible = false;
                Button1.Enabled = true;
                DropDownList1.Enabled = false;
                Label7.Visible = false;
            }



            DataTable dtAccommodation = (DataTable)Session["dtAccommodation"];
            DataTable dtItems = (DataTable)Session["dtItems"];

            int i = 0;
            int totalPrice = 0;
            int PriceAccom = 0;
            int PriceItems = 0;
            int DepositAmount = 0;
            foreach (GridViewRow row in GridView1.Rows)
            {

                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                if (chk != null && chk.Checked)
                {
                    row.BackColor = System.Drawing.Color.Green;
                    TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);
                    txtPeopleStay.Enabled = true;
                    if (dtAccommodation.Rows[i]["LimitWithPeople"].ToString() == "True")
                    {
                        DepositAmount += 50* Convert.ToInt32(txtPeopleStay.Text);
                        if (Convert.ToInt32(DropDownList1.SelectedValue) > 1)
                        {
                            for (int k = 0; k < Convert.ToInt32(DropDownList1.SelectedValue); k++)
                            {
                                DataTable dtHolidayPrice = code.DatabaseQuery(conn, "Select * from Accommodation_HolidayPrice Where Accommodation_ID = " + dtAccommodation.Rows[i]["ID"].ToString() + " AND  DateNewPrice = '" + Convert.ToDateTime(TextBox12.Text).AddDays(k).ToString("yyyy-MM-dd") + "'");
                                DataTable dtPrice = code.DatabaseQuery(conn, "SELECT [Price] FROM [Accommodation] Where ID = " + dtAccommodation.Rows[i]["ID"].ToString());
                                if (dtHolidayPrice.Rows.Count > 0)
                                {
                                    if (Convert.ToInt32(row.Cells[4].Text) == Convert.ToInt32(dtPrice.Rows[0][0].ToString()) || Convert.ToInt32(row.Cells[4].Text) == Convert.ToInt32(dtHolidayPrice.Rows[0]["Price"].ToString()))
                                    {
                                        PriceAccom += Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(dtHolidayPrice.Rows[0]["Price"].ToString());
                                    }
                                    else
                                    {
                                        PriceAccom += Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(dtPrice.Rows[0][0].ToString());
                                    }
                                }
                                else
                                {
                                    
                                    PriceAccom += Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(dtPrice.Rows[0][0].ToString());
                                }
                            }
                        }
                        else
                        {
                            PriceAccom += Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue);
                        }

                        try
                        {
                            if (Session["permission"].ToString() == "True")
                            {

                            }
                            else
                            {
                                if (Convert.ToInt32(txtPeopleStay.Text) > Convert.ToInt32(row.Cells[3].Text))
                                {
                                    txtPeopleStay.Text = row.Cells[3].Text;
                                }
                            }
                        }
                        catch
                        {
                            if (Convert.ToInt32(txtPeopleStay.Text) > Convert.ToInt32(row.Cells[3].Text))
                            {
                                txtPeopleStay.Text = row.Cells[3].Text;
                            }
                        }
                    }
                    else
                    {
                        DepositAmount += 500;
                        if (Convert.ToInt32(DropDownList1.SelectedValue) > 1)
                        {
                            for (int k = 0; k < Convert.ToInt32(DropDownList1.SelectedValue); k++)
                            {
                                DataTable dtHolidayPrice = code.DatabaseQuery(conn, "Select * from Accommodation_HolidayPrice Where Accommodation_ID = " + dtAccommodation.Rows[i]["ID"].ToString() + " AND DateNewPrice = '" + Convert.ToDateTime(TextBox12.Text).ToString("yyyy-MM-dd") + "'");
                                DataTable dtPrice = code.DatabaseQuery(conn, "SELECT [Price] FROM [Accommodation] Where ID = " + dtAccommodation.Rows[i]["ID"].ToString());
                                if (dtHolidayPrice.Rows.Count > 0)
                                {
                                    if (Convert.ToInt32(row.Cells[4].Text) == Convert.ToInt32(dtPrice.Rows[0][0].ToString()) || Convert.ToInt32(row.Cells[4].Text) == Convert.ToInt32(dtHolidayPrice.Rows[0]["Price"].ToString()))
                                    {
                                        PriceAccom += Convert.ToInt32(dtHolidayPrice.Rows[0]["Price"].ToString());
                                    }
                                    else
                                    {
                                        PriceAccom += Convert.ToInt32(row.Cells[4].Text);
                                    }
                                }
                                else
                                {
                                    //DataTable dtPrice = code.DatabaseQuery(conn, "SELECT [Price] FROM [Accommodation] Where ID = " + dtAccommodation.Rows[i]["ID"].ToString());
                                    PriceAccom += Convert.ToInt32(row.Cells[4].Text);
                                }
                            }
                        }
                        else
                        {
                            PriceAccom += Convert.ToInt32(row.Cells[4].Text);
                        }
                        if (Convert.ToInt32(txtPeopleStay.Text) == 0)
                        {
                            txtPeopleStay.Text = dtAccommodation.Rows[i]["People"].ToString();
                        }
                        if (Convert.ToInt32(txtPeopleStay.Text) > Convert.ToInt32(row.Cells[3].Text))
                        {
                            txtPeopleStay.Text = row.Cells[3].Text;
                        }
                    }
                }
                else
                {
                    row.BackColor = System.Drawing.Color.White;
                    TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);
                    txtPeopleStay.Enabled = false;
                    txtPeopleStay.Text = "0";
                }
                i++;
            }
            Session["PriceAccom"] = PriceAccom;
            //Label2.Text = (Convert.ToInt32(PriceAccom) * 0.4).ToString();
            Label2.Text = DepositAmount.ToString();
            int y = 0;
            int totalPriceItems = 0;
            foreach (GridViewRow row in GridView2.Rows)
            {
                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                if (chk != null && chk.Checked)
                {

                    row.BackColor = System.Drawing.Color.Green;
                    TextBox txtAmount = (row.Cells[2].FindControl("txtAmount") as TextBox);
                    txtAmount.Enabled = true;
                    if (Convert.ToInt32(txtAmount.Text) == 0 && row.Cells[3].Text != "0")
                    {
                        txtAmount.Text = "1";
                    }

                    if (dtItems.Rows[y]["LimitWithAmount"].ToString() == "True")
                    {
                        PriceItems += Convert.ToInt32(txtAmount.Text) * Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue);
                        if (Convert.ToInt32(txtAmount.Text) > Convert.ToInt32(row.Cells[3].Text))
                        {
                            txtAmount.Text = row.Cells[3].Text;
                        }
                    }
                    else
                    {
                        PriceItems += Convert.ToInt32(txtAmount.Text) * Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue);

                        if (Convert.ToInt32(txtAmount.Text) == 0)
                        {
                            txtAmount.Text = dtItems.Rows[y]["Amount"].ToString();
                        }
                        if (Convert.ToInt32(txtAmount.Text) > Convert.ToInt32(row.Cells[3].Text))
                        {
                            txtAmount.Text = row.Cells[3].Text;
                        }
                    }
                    if (row.Cells[3].Text == "0" && Convert.ToInt32(txtAmount.Text) == 0)
                    {
                        chk.Checked = false;
                        row.BackColor = System.Drawing.Color.White;
                    }
                }
                else
                {
                    row.BackColor = System.Drawing.Color.White;
                    TextBox txtAmount = (row.Cells[2].FindControl("txtAmount") as TextBox);
                    txtAmount.Enabled = false;
                    txtAmount.Text = "0";
                }
                i++;
            }

            Session["PriceItems"] = PriceItems;
            totalPrice = PriceAccom + PriceItems;
            Session["totalPrice"] = totalPrice;

            TextBox4.Text = Session["totalPrice"].ToString();

            if ((command == "edit" || command == "checkin" || command == "rentmore") && Session["permission"].ToString() == "True")
            {
                if (!IsPostBack)
                {
                    

                    Button1.Enabled = true;
                    DataTable dtReservation = code.DatabaseQuery(conn, "SELECT * FROM [Reservation] Where ID = " + id + " AND Customer_MobilePhone = '" + check + "'");
                    DataTable dtAccom = code.DatabaseQuery(conn, "SELECT * FROM [Reservation] right join Reservation_Accommodation on Reservation_Accommodation.Reservation_ID = Reservation.ID Where Reservation.ID = " + id + " AND Customer_MobilePhone = '" + check + "'");
                    DataTable dtItemsold = code.DatabaseQuery(conn, "SELECT * FROM [Reservation] right join Reservation_Items on Reservation_Items.Reservation_ID = Reservation.ID Where Reservation.ID = " + id + " AND Customer_MobilePhone = '" + check + "'");

                    DataTable dtReceipt = code.DatabaseQuery(conn, "SELECT  * FROM [Account_Receipt] Where RESERVATION_ID = '" + id + "'");
                    try
                    {
                        if (dtReservation.Rows[0]["NoCreateReceipt"].ToString().ToLower() == "false")
                        {
                            CheckBox4.Checked = false;
                            CheckBox4.DataBind();
                        }
                        else
                        {
                            if (dtReceipt.Rows.Count == 0 || dtReservation.Rows[0]["NoCreateReceipt"].ToString().ToLower() == "true")
                            {
                                CheckBox4.Checked = true;
                                CheckBox4.DataBind();
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        if (dtReservation.Rows[0]["NoNameinReceipt"].ToString().ToLower() == "false")
                        {
                            CheckBox3.Checked = false;
                            CheckBox3.DataBind();
                            Panel1.Visible = true;
                        }
                        if (dtReceipt.Rows.Count == 0 || dtReservation.Rows[0]["NoCreateReceipt"].ToString().ToLower() == "true")
                        {
                            CheckBox3.Checked = true;
                            CheckBox3.DataBind();
                        }
                    }
                    catch { }

                    try
                    {

                        if (dtReceipt.Rows[0]["Etax"].ToString().ToLower() == "false")
                        {
                            CheckBox5.Checked = false;
                            CheckBox5.DataBind();
                        }
                        if (dtReceipt.Rows[0]["Etax"].ToString().ToLower() == "true")
                        {
                            Panel1.Visible = true;

                            CheckBox3.Checked = false;
                            CheckBox3.DataBind();

                            CheckBox4.Checked = false;
                            CheckBox4.DataBind();

                            CheckBox5.Checked = true;
                            CheckBox5.DataBind();


                        }
                    }
                    catch { }

                    

                    try
                    {
                        //TextBox12.Text = DateTime.Parse(dtReservation.Rows[0]["CheckinDate"].ToString()).ToString();
                        //TextBox12.DataBind();
                        //TextBox12_TextChanged(null, null);
                    }
                    catch
                    {

                    }
                    DropDownList1.SelectedIndex = DropDownList1.Items.IndexOf(DropDownList1.Items.FindByValue(dtReservation.Rows[0]["StayDays"].ToString()));
                    //Calendar1_SelectionChanged(null, null);
                    DataTable dtAccommodationlast = (DataTable)Session["dtAccommodation"];
                    foreach (GridViewRow row in GridView1.Rows)
                    {
                        CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                        TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);
                        for (int o = 0; o < dtAccom.Rows.Count; o++)
                        {
                            if (dtAccommodationlast.Rows[row.RowIndex]["ID"].ToString() == dtAccom.Rows[o]["Accommodation_ID"].ToString())
                            {
                                chk.Checked = true;
                                txtPeopleStay.Text = dtAccom.Rows[o]["Amount"].ToString();
                                row.Cells[4].Text = dtAccom.Rows[o]["Price"].ToString();
                            }
                        }
                    }
                    DataTable dtCustomer = code.DatabaseQuery(conn, "SELECT * FROM [Reservation] inner join Customer on Customer.MobilePhone = Reservation.Customer_MobilePhone left join Customer_Type on Customer_Type_ID = Customer_Type.ID left join Address on Address.ID = Address_ID left join Account_Receipt on Account_Receipt.Reservation_ID = Reservation.ID  Where Reservation.ID = " + id + " AND Customer_MobilePhone = '" + check + "'");
                    try //Address
                    {
                        TextBox16.Text = dtCustomer.Rows[0]["PostalCode"].ToString();
                        DropDownList5.ClearSelection();
                        DropDownList5.Items.FindByText(dtCustomer.Rows[0]["Province"].ToString()).Selected = true;
                        DropDownList5.SelectedIndex = DropDownList5.Items.IndexOf(DropDownList5.Items.FindByText(dtCustomer.Rows[0]["Province"].ToString()));
                        DropDownList6.ClearSelection();
                        DropDownList6.Items.FindByText(dtCustomer.Rows[0]["District"].ToString()).Selected = true;
                        DropDownList6.SelectedIndex = DropDownList6.Items.IndexOf(DropDownList6.Items.FindByText(dtCustomer.Rows[0]["District"].ToString()));
                        DropDownList7.ClearSelection();
                        DropDownList7.Items.FindByText(dtCustomer.Rows[0]["SubDistrict"].ToString()).Selected = true;
                        DropDownList7.SelectedIndex = DropDownList7.Items.IndexOf(DropDownList7.Items.FindByText(dtCustomer.Rows[0]["SubDistrict"].ToString()));

                        DropDownList8.ClearSelection();
                        DropDownList8.Items.FindByValue(dtCustomer.Rows[0]["Customer_Type_ID"].ToString()).Selected = true;
                        DropDownList8.SelectedIndex = DropDownList8.Items.IndexOf(DropDownList8.Items.FindByValue(dtCustomer.Rows[0]["Customer_Type_ID"].ToString()));

                        if(DropDownList8.Items.IndexOf(DropDownList8.Items.FindByValue(dtCustomer.Rows[0]["Customer_Type_ID"].ToString())) == 0)
                        {
                            TextBox18.Visible = true;
                            TextBox18.Text = dtCustomer.Rows[0]["Branch_Number"].ToString();
                        }

                    }
                    catch { }

                    TextBox1.Text = dtCustomer.Rows[0]["MobilePhone"].ToString();
                    TextBox2.Text = dtCustomer.Rows[0]["Name"].ToString();
                    TextBox3.Text = dtCustomer.Rows[0]["NickName"].ToString();
                    //TextBox7.Text = dtCustomer.Rows[0]["FullName"].ToString();
                    TextBox8.Text = dtCustomer.Rows[0]["Address"].ToString();
                    TextBox17.Text = dtCustomer.Rows[0]["Address1"].ToString();
                    TextBox9.Text = dtCustomer.Rows[0]["IDNumber"].ToString();
                    TextBox13.Text = dtCustomer.Rows[0]["Email"].ToString();

                    DataTable dtItemss = (DataTable)Session["dtItems"];
                    foreach (GridViewRow row in GridView2.Rows)
                    {
                        CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                        TextBox txtAmount = (row.Cells[2].FindControl("txtAmount") as TextBox);
                        for (int p = 0; p < dtItemsold.Rows.Count; p++)
                        {
                            if (dtItemss.Rows[row.RowIndex]["ID"].ToString() == dtItemsold.Rows[p]["Items_ID"].ToString())
                            {
                                chk.Checked = true;
                                txtAmount.Text = dtItemsold.Rows[p]["Amount"].ToString();
                                row.Cells[3].Text = (Convert.ToInt32(row.Cells[3].Text) + Convert.ToInt32(dtItemsold.Rows[p]["Amount"].ToString())).ToString();

                            }
                        }
                    }
                    TextBox4.Text = dtCustomer.Rows[0]["TotalPrice"].ToString();
                    TextBox5.Text = dtCustomer.Rows[0]["Deposit"].ToString();


                    Image1.ImageUrl = "./Upload/Slip/" + id + "_" + check + ".jpg";
                    Image1.DataBind();
                    TextBox6.Text = dtCustomer.Rows[0]["Remark"].ToString();

                    Label7.Visible = true;
                    Label7.Text = "ยอดเงินส่วนที่เหลือที่จะต้องชำระตอนเช็คอิน = " + (Convert.ToDecimal(TextBox4.Text) - Convert.ToDecimal(TextBox5.Text)).ToString("N2") + " บาท";

                    string paidType = dtCustomer.Rows[0]["Paid_Type"].ToString();
                    try
                    {
                        if(paidType.Length > 5)
                        {

                        }
                        else{
                            paidType = "เงินสด";
                        }
                    }
                    catch
                    {
                        paidType = "เงินสด";
                    }
                    if (command == "checkin")
                    {
                        Label7.Text += " ยอดเดิมลูกค้าชำระโดยวิธี "+ paidType;
                    }
                }
                else
                {

                }

            }

        }

        protected void Calendar1_SelectionChanged(object sender, EventArgs e)
        {
            int chkvaluecheck = 0;
            List<string> listcheck = new List<string>();
            foreach (GridViewRow gr in GridView1.Rows)
            {
                CheckBox chkC = gr.FindControl("chkSelect") as CheckBox;

                //GridViewRow Row = ((GridViewRow)chkC.Parent.Parent);

                bool chkvalue = chkC.Checked;

                if(chkvalue  == true)
                {
                    chkvaluecheck = 1;
                    listcheck.Add(gr.Cells[1].Text);
                }
                

            }

            if (TextBox12.Text.Length > 0 && chkvaluecheck == 1)
            {
                int checkdup = 0;
                DataTable dtAccom = (DataTable)Session["dtAccommodation"];
                for (int i = 0;i<Convert.ToInt32(DropDownList1.SelectedValue);i++)
                {
                    for(int j = 0;j<listcheck.Count;j++)
                    {
                        DataTable dtReserveAccomDup = code.DatabaseQuery(conn, "SELECT * FROM [Taketime].[dbo].[Reservation_Accommodation] inner join Reservation on Reservation.ID = Reservation_ID inner join Accommodation on Accommodation.ID = Accommodation_ID Where CheckinDate = '"+Convert.ToDateTime(TextBox12.Text).AddDays(i).ToString("yyyy-MM-dd")+"' AND AccomName = N'" + listcheck[j] +"'");
                        if(dtReserveAccomDup.Rows.Count > 0)
                        {
                            checkdup = 1;
                        }
                    }
                }
                if(checkdup == 1)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ห้องพักที่คุณเลือกไม่ว่างในการจองหลายวัน');", true);
                    DropDownList1.SelectedIndex = 0;
                    DropDownList1.DataBind();
                }
                Label1.Text = "Check-Out: " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy");
            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('โปรดเลือกวันที่ และ ห้องพัก ก่อนเลือกจำนวนวันเพิ่ม');", true);
                DropDownList1.SelectedIndex = 0;
                DropDownList1.DataBind();
            }
            

            
        }

        protected void DropDownList1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Calendar1_SelectionChanged(null, null);
        }

        protected void FileUpload1_DataBinding(object sender, EventArgs e)
        {
            Button1.Enabled = true;
        }

        protected void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
        {

        }

        protected void TextBox4_TextChanged(object sender, EventArgs e)
        {

        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            
            DateTime docCreatedDate = DateTime.Now;

            try
            {
                if (Convert.ToDateTime(TextBox12.Text) < docCreatedDate)
                {
                    docCreatedDate = Convert.ToDateTime(TextBox12.Text);
                }
            }
            catch
            {

            }

            try
            {
                if (TextBox11.Visible == true && TextBox11.Text.Length > 0)
                {
                    docCreatedDate = Convert.ToDateTime(TextBox11.Text);
                }
            }
            catch { }


            TextBox4.Text = Session["OldPrice"].ToString();
            string command = Request.QueryString["command"];
            string id = Request.QueryString["id"];
            string check = Request.QueryString["check"];

            int Reservation_ID = 0; ;
            DataTable dtAccommodation = (DataTable)Session["dtAccommodation"];
            DataTable dtItems = (DataTable)Session["dtItems"];
            decimal deposit = 0;
            bool IsDeposit = true;
            DataTable dtReserve = new DataTable();
            try
            {
                dtReserve.Columns.Add("Number");
                dtReserve.Columns.Add("Receipt_ID");
                dtReserve.Columns.Add("ProductType_ID");
                dtReserve.Columns.Add("Product_ID");
                dtReserve.Columns.Add("Product_Data");
                dtReserve.Columns.Add("Product_Amount");
                dtReserve.Columns.Add("Product_Unit");
                dtReserve.Columns.Add("Price_PerPeice");
                dtReserve.Columns.Add("Price_Amount");
            }
            catch { }

            try
            {
                deposit = Convert.ToDecimal(TextBox5.Text);
            }
            catch { }
            int checkgrid1 = 0;
            foreach (GridViewRow row in GridView1.Rows)
            {
                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                if (chk != null && chk.Checked)
                {
                    checkgrid1++;
                }
            }

            int checkpaymentselect = 0;
            try
            {
                if (Session["permission"].ToString() == "True" && ( command == "reserve" || command == "checkin" || (command == "edit" && CheckBox2.Checked == true) || (command == "rentmore" && CheckBox2.Checked == true) ))
                {
                    if(DropDownList2.SelectedIndex == 0)
                    {
                        checkpaymentselect = 1;
                        ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเลือกวิธีชำระเงิน');", true);
                    }
                    else
                    {
                        
                    }
                }
            }
            catch { }

            int checkcustype = 0;
            if(DropDownList8.SelectedValue == "1")
            {
                if(TextBox18.Text.Length == 5)
                {
                    checkcustype = 1;
                }
                else
                {

                }

            }
            else
            {
                checkcustype = 1;
            }

            if (TextBox1.Text.Length > 0 && checkcustype == 1)
            {
                if (checkgrid1 > 0)
                {
                    if (deposit > 0 || TextBox1.Text == "02" || CheckBox2.Checked == true)
                    {
                        if ((FileUpload1.HasFile || Image1.ImageUrl != "./Images/บัญชี.png" || TextBox1.Text == "02" || DropDownList2.SelectedItem.Text == "เงินสด" ) && checkpaymentselect == 0)
                        {
                            try
                            {
                                //if (command == "edit" && Session["permission"].ToString() == "True")
                                //{
                                //    TextBox5.Enabled = false;
                                //    code.DatabaseInsert(conn, "DELETE FROM [dbo].[Reservation_Accommodation] WHERE Reservation_ID = " + id);
                                //    code.DatabaseInsert(conn, "DELETE FROM [dbo].[Reservation_Items] WHERE Reservation_ID = " + id);


                                //    int Deposit = Convert.ToInt32(TextBox5.Text);
                                //    if (CheckBox2.Checked == true)
                                //    {
                                //        Deposit = Deposit + Convert.ToInt32(TextBox10.Text);
                                //    }
                                //    code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation] SET [Customer_MobilePhone] = '" + TextBox1.Text + "' ,[CheckinDate] = '" + Convert.ToDateTime(TextBox12.Text).ToString("yyyy-MM-dd") + "' ,[CheckoutDate] = '" + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("yyyy-MM-dd") + "' ,[StayDays] = " + DropDownList1.SelectedValue + " , [TotalPrice] = " + TextBox4.Text + " ,[Deposit] = " + Deposit + ", [Remark] = N'" + TextBox6.Text + "' WHERE ID = " + id);
                                //    code.DatabaseInsert(conn, "UPDATE [dbo].[Customer] SET [Name] = N'" + TextBox2.Text.Replace("'", "''") + "' ,[NickName] = N'" + TextBox3.Text.Replace("'", "''") + "',[FullName] = N'" + TextBox2.Text.Replace("'", "''") + "',[Address] = N'" + cleantext(TextBox8.Text) + "',[IDNumber] = N'" + TextBox9.Text.Replace("'", "''") + "' WHERE MobilePhone = '" + TextBox1.Text + "'");


                                //    Reservation_ID = Convert.ToInt32(id);
                                //    if (CheckBox2.Checked == true)
                                //    {
                                //        IsDeposit = true;
                                //        createReceipt(Reservation_ID.ToString(), Convert.ToDouble(TextBox10.Text), dtReserve, IsDeposit);
                                //    }
                                //    Response.Redirect("./Reservation_Confirmed?id=" + Reservation_ID + "&check=" + TextBox1.Text);
                                //}
                                if ((command == "edit" || command == "rentmore") && Session["permission"].ToString() == "True")
                                {
                                    DataTable dtCustomer = code.DatabaseQuery(conn, "Select * From Customer Where MobilePhone = '" + TextBox1.Text + "'");
                                    if (dtCustomer.Rows.Count == 1)
                                    {
                                        code.DatabaseInsert(conn, "UPDATE [dbo].[Customer] SET [Name] = N'" + TextBox2.Text.Replace("'", "''") + "' ,[NickName] = N'" + TextBox3.Text.Replace("'", "''") + "',[FullName] = N'" + TextBox2.Text.Replace("'", "''") + "',[Address] = N'" + cleantext(TextBox8.Text) + "',[IDNumber] = N'" + TextBox9.Text.Replace("'", "''") + "',[Email] = N'" + TextBox13.Text.Replace("'", "''") + "',[Customer_Type_ID] = " + DropDownList8.SelectedValue + ",[Address_ID] = " + CheckAddressID(TextBox16.Text, DropDownList5.SelectedItem.Text, DropDownList6.SelectedItem.Text, DropDownList7.SelectedItem.Text) + ",[Address1] = N'" + TextBox17.Text.Replace("'", "''") + "',[Branch_Number] = N'" + TextBox18.Text.Replace("'", "''") + "' WHERE MobilePhone = '" + TextBox1.Text + "'");
                                    }
                                    else
                                    {
                                        code.DatabaseInsert(conn, "INSERT INTO [dbo].[Customer]([MobilePhone],[Name],[NickName],[ComeFrom],[Remark],FullName,Address,IDNumber,Email,Customer_Type_ID,Address_ID,Address1,Branch_Number) VALUES ('" + TextBox1.Text + "',N'" + TextBox2.Text.Replace("'", "''") + "',N'" + TextBox3.Text.Replace("'", "''") + "','','',N'" + TextBox2.Text + "',N'" + cleantext(TextBox8.Text) + "',N'" + TextBox9.Text + "',N'" + TextBox13.Text + "'," + DropDownList8.SelectedValue + "," + CheckAddressID(TextBox16.Text, DropDownList5.SelectedItem.Text, DropDownList6.SelectedItem.Text, DropDownList7.SelectedItem.Text) + ",N'" + TextBox17.Text.Replace("'", "''") + "',N'" + TextBox18.Text.Replace("'", "''") + "')");
                                    }
                                    
                                    dtReserve.Clear();
                                    dtReserve.AcceptChanges();
                                    DataTable dtoldAccom = code.DatabaseQuery(conn, "SELECT * FROM [Reservation_Accommodation] inner join Reservation on Reservation.ID = Reservation_ID Where Reservation.ID = " + id);
                                    DataTable dtoldItem = code.DatabaseQuery(conn, "SELECT * FROM [Reservation_Items] inner join Reservation on Reservation.ID = Reservation_ID Where Reservation.ID = " + id);
                                    IsDeposit = false;
                                    string msg = "";
                                    int totalnew = 0;
                                    int checkoldAccomRemoved = 0;
                                    List<string> cmds = new List<string>();
                                    for (int x = 0; x < dtoldAccom.Rows.Count; x++)
                                    {
                                        foreach (GridViewRow row in GridView1.Rows)
                                        {
                                            TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);
                                            CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                            if (dtoldAccom.Rows[x]["Accommodation_ID"].ToString() == dtAccommodation.Rows[row.RowIndex]["ID"].ToString())
                                            {
                                                if (chk != null && chk.Checked)
                                                {
                                                    if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True")
                                                    {
                                                        msg += "- " + dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " " + txtPeopleStay.Text + " " + dtAccommodation.Rows[row.RowIndex]["Unit"].ToString() + "\r\n";
                                                    }
                                                    else
                                                    {
                                                        msg += "- " + dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " " + DropDownList1.SelectedValue + " " + dtAccommodation.Rows[row.RowIndex]["Unit"].ToString() + "\r\n";
                                                    }
                                                    if (command == "edit")
                                                    {
                                                        code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation_Accommodation] SET [Amount] = " + Convert.ToInt32(txtPeopleStay.Text) + " ,[Price] = " + row.Cells[4].Text + " WHERE Accommodation_ID = " + dtoldAccom.Rows[x]["Accommodation_ID"].ToString() + " AND Reservation_ID = " + id);
                                                    }
                                                    else
                                                    {
                                                        cmds.Add("UPDATE [dbo].[Reservation_Accommodation] SET [Amount] = " + Convert.ToInt32(txtPeopleStay.Text) + " ,[Price] = " + row.Cells[4].Text + " WHERE Accommodation_ID = " + dtoldAccom.Rows[x]["Accommodation_ID"].ToString() + " AND Reservation_ID = " + id);
                                                    }
                                                    if (Convert.ToInt32(txtPeopleStay.Text) >= Convert.ToInt32(dtoldAccom.Rows[x]["Amount"].ToString()))
                                                    {
                                                        int peoplemore = Convert.ToInt32(txtPeopleStay.Text) - Convert.ToInt32(dtoldAccom.Rows[x]["Amount"].ToString());
                                                        if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True" && peoplemore > 0)
                                                        {
                                                            try
                                                            {
                                                                dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), peoplemore, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * peoplemore * Convert.ToInt32(DropDownList1.SelectedValue));
                                                            }
                                                            catch 
                                                            {
                                                                dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน  เช็คเอ้าท์ ", peoplemore, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * peoplemore * Convert.ToInt32(DropDownList1.SelectedValue));
                                                            }
                                                            totalnew += Convert.ToInt32(row.Cells[4].Text) * peoplemore * Convert.ToInt32(DropDownList1.SelectedValue);
                                                        }
                                                        else
                                                        { }
                                                    }
                                                    else
                                                    {
                                                        checkoldAccomRemoved++;
                                                    }
                                                }
                                                else
                                                {
                                                    if (command == "edit")
                                                    {
                                                        code.DatabaseInsert(conn, "DELETE FROM [dbo].[Reservation_Accommodation] WHERE Accommodation_ID = " + dtoldAccom.Rows[x]["Accommodation_ID"].ToString() + " AND Reservation_ID = " + id);
                                                    }

                                                    checkoldAccomRemoved++;
                                                }
                                            }
                                            else { }
                                        }
                                    }
                                    
                                    foreach (GridViewRow row in GridView1.Rows)
                                    {
                                        TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);
                                        CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                        bool checkdup = false;
                                        for (int x = 0; x < dtoldAccom.Rows.Count; x++)
                                        {
                                            if (chk != null && chk.Checked && dtoldAccom.Rows[x]["Accommodation_ID"].ToString() == dtAccommodation.Rows[row.RowIndex]["ID"].ToString())
                                            {
                                                checkdup = true;
                                            }
                                        }
                                        if (checkdup == false && chk != null && chk.Checked)
                                        {
                                            if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True")
                                            {
                                                if (command == "edit")
                                                {
                                                    code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation_Accommodation] ([Reservation_ID],[Accommodation_ID],[Amount],[Price]) VALUES (" + id + "," + dtAccommodation.Rows[row.RowIndex]["ID"].ToString() + "," + txtPeopleStay.Text + "," + row.Cells[4].Text + ") ");
                                                }
                                                else
                                                {
                                                    cmds.Add("INSERT INTO [dbo].[Reservation_Accommodation] ([Reservation_ID],[Accommodation_ID],[Amount],[Price]) VALUES (" + id + "," + dtAccommodation.Rows[row.RowIndex]["ID"].ToString() + "," + txtPeopleStay.Text + "," + row.Cells[4].Text + ") ");
                                                }
                                                try
                                                {
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), txtPeopleStay.Text, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(DropDownList1.SelectedValue));
                                                }
                                                catch
                                                {
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน เช็คเอ้าท์ ", txtPeopleStay.Text, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(DropDownList1.SelectedValue));
                                                }
                                                totalnew += Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(DropDownList1.SelectedValue);
                                                msg += "- " + dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " "+ txtPeopleStay.Text+" "+ dtAccommodation.Rows[row.RowIndex]["Unit"].ToString()+"\r\n";
                                            }
                                            else
                                            {
                                                if (command == "edit")
                                                {
                                                    code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation_Accommodation] ([Reservation_ID],[Accommodation_ID],[Amount],[Price]) VALUES (" + id + "," + dtAccommodation.Rows[row.RowIndex]["ID"].ToString() + "," + DropDownList1.SelectedValue + "," + row.Cells[4].Text + ") ");
                                                }
                                                else
                                                {
                                                    cmds.Add("INSERT INTO [dbo].[Reservation_Accommodation] ([Reservation_ID],[Accommodation_ID],[Amount],[Price]) VALUES (" + id + "," + dtAccommodation.Rows[row.RowIndex]["ID"].ToString() + "," + DropDownList1.SelectedValue + "," + row.Cells[4].Text + ") ");
                                                }
                                                try
                                                {
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), DropDownList1.SelectedValue, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue));
                                                }
                                                catch {
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน เช็คเอ้าท์ ", DropDownList1.SelectedValue, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue));
                                                }

                                                totalnew += Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue);
                                                msg += "- " + dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " " + DropDownList1.SelectedValue + " " + dtAccommodation.Rows[row.RowIndex]["Unit"].ToString() + "\r\n";

                                            }
                                        }
                                    }
                                    msg += "\r\nรายการของเช่า\r\n";
                                    int checkoldItemRemoved = 0;
                                    for (int x = 0; x < dtoldItem.Rows.Count; x++)
                                    {
                                        foreach (GridViewRow row in GridView2.Rows)
                                        {
                                            TextBox txtAmount = (row.Cells[2].FindControl("txtAmount") as TextBox);
                                            CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                            if (dtoldItem.Rows[x]["Items_ID"].ToString() == dtItems.Rows[row.RowIndex]["ID"].ToString())
                                            {
                                                if (chk != null && chk.Checked)
                                                {
                                                    if (command == "edit")
                                                    {
                                                        code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation_Items] SET [Amount] = " + Convert.ToInt32(txtAmount.Text) + " ,[Price] = " + row.Cells[4].Text + " WHERE Items_ID = " + dtoldItem.Rows[x]["Items_ID"].ToString() + " AND Reservation_ID = " + id);
                                                        
                                                    }
                                                    else
                                                    {
                                                        cmds.Add("UPDATE [dbo].[Reservation_Items] SET [Amount] = " + Convert.ToInt32(txtAmount.Text) + " ,[Price] = " + row.Cells[4].Text + " WHERE Items_ID = " + dtoldItem.Rows[x]["Items_ID"].ToString() + " AND Reservation_ID = " + id);
                                                    }
                                                    if (Convert.ToInt32(txtAmount.Text) >= Convert.ToInt32(dtoldItem.Rows[x]["Amount"].ToString()))
                                                    {
                                                        int itemmore = Convert.ToInt32(txtAmount.Text) - Convert.ToInt32(dtoldItem.Rows[x]["Amount"].ToString());
                                                        if (itemmore > 0)
                                                        {
                                                            try
                                                            {
                                                                dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "2", dtItems.Rows[row.RowIndex]["ID"].ToString(), dtItems.Rows[row.RowIndex]["ItemName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), itemmore, dtItems.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, (Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue) * itemmore));
                                                            }
                                                            catch
                                                            {
                                                                dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "2", dtItems.Rows[row.RowIndex]["ID"].ToString(), dtItems.Rows[row.RowIndex]["ItemName"].ToString() + " เช็คอิน  เช็คเอ้าท์ ", itemmore, dtItems.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, (Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue) * itemmore));
                                                            }

                                                            totalnew += Convert.ToInt32(DropDownList1.SelectedValue) * itemmore * Convert.ToInt32(row.Cells[4].Text);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        checkoldItemRemoved++;
                                                    }
                                                    msg += "- "+dtItems.Rows[row.RowIndex]["ItemName"].ToString()+" "+ txtAmount.Text+" ชิ้น\r\n";
                                                }
                                                else
                                                {
                                                    if (command == "edit")
                                                    {
                                                        code.DatabaseInsert(conn, "DELETE FROM [dbo].[Reservation_Items] WHERE Items_ID = " + dtoldItem.Rows[x]["Items_ID"].ToString() + " AND Reservation_ID = " + id);
                                                    }
                                                    checkoldItemRemoved++;
                                                }
                                            }
                                            else
                                            {

                                            }
                                        }
                                    }

                                    foreach (GridViewRow row in GridView2.Rows)
                                    {
                                        try
                                        {
                                            TextBox txtAmount = (row.Cells[2].FindControl("txtAmount") as TextBox);
                                            CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                            bool checkdup = false;
                                            for (int x = 0; x < dtoldItem.Rows.Count; x++)
                                            {
                                                if (chk != null && chk.Checked && dtoldItem.Rows[x]["Items_ID"].ToString() == dtItems.Rows[row.RowIndex]["ID"].ToString())
                                                {
                                                    checkdup = true;
                                                }
                                            }
                                            if (chk != null && chk.Checked && checkdup == false)
                                            {
                                                if (command == "edit")
                                                {
                                                    code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation_Items] ([Reservation_ID],[Items_ID],[Amount],[Price]) VALUES (" + id + "," + dtItems.Rows[row.RowIndex]["ID"].ToString() + "," + txtAmount.Text + "," + row.Cells[4].Text + ") ");
                                                }
                                                else
                                                {
                                                    cmds.Add("INSERT INTO [dbo].[Reservation_Items] ([Reservation_ID],[Items_ID],[Amount],[Price]) VALUES (" + id + "," + dtItems.Rows[row.RowIndex]["ID"].ToString() + "," + txtAmount.Text + "," + row.Cells[4].Text + ") ");
                                                }
                                                try
                                                {
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "2", dtItems.Rows[row.RowIndex]["ID"].ToString(), dtItems.Rows[row.RowIndex]["ItemName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), txtAmount.Text, dtItems.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, (Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue) * Convert.ToInt32(txtAmount.Text)));
                                                }
                                                catch {
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "2", dtItems.Rows[row.RowIndex]["ID"].ToString(), dtItems.Rows[row.RowIndex]["ItemName"].ToString() + " เช็คอิน เช็คเอ้าท์ ", txtAmount.Text, dtItems.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, (Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue) * Convert.ToInt32(txtAmount.Text)));
                                                }
                                                msg += "- " + dtItems.Rows[row.RowIndex]["ItemName"].ToString() + " " + txtAmount.Text + " ชิ้น\r\n";
                                                totalnew += Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue) * Convert.ToInt32(txtAmount.Text);
                                            }
                                        }
                                        catch (Exception ex)
                                        { }
                                    }
                                    msg += "\r\nหมายเหตุ: " + TextBox6.Text ;
                                    code.DatabaseInsert(conn, "UPDATE [dbo].[Customer] SET [Name] = N'" + TextBox2.Text.Replace("'", "''") + "' ,[NickName] = N'" + TextBox3.Text.Replace("'", "''") + "',[FullName] = N'" + TextBox2.Text.Replace("'", "''") + "',[Address] = N'" + cleantext(TextBox8.Text) + "',[IDNumber] = N'" + TextBox9.Text.Replace("'", "''") + "',[Email] = N'" + TextBox13.Text.Replace("'", "''") + "',[Customer_Type_ID] = " + DropDownList8.SelectedValue + ",[Address_ID] = " + CheckAddressID(TextBox16.Text, DropDownList5.SelectedItem.Text, DropDownList6.SelectedItem.Text, DropDownList7.SelectedItem.Text) + ",[Address1] = N'" + TextBox17.Text.Replace("'", "''") + "' WHERE MobilePhone = '" + TextBox1.Text + "'");

                                    uploadSlip(id);
                                    if (command == "edit")
                                    {

                                        decimal Deposit = Convert.ToDecimal(TextBox5.Text);
                                        if (CheckBox2.Checked == true && TextBox1.Text != "02")
                                        {
                                            Deposit += Convert.ToDecimal(TextBox10.Text);
                                            IsDeposit = true;
                                            if (CheckBox4.Checked == false)
                                            {
                                                createReceipt(id, Convert.ToDouble(TextBox10.Text), dtReserve, IsDeposit, docCreatedDate, CheckBox5.Checked);
                                            }
                                        }
                                        else
                                        {

                                        }
                                        try
                                        {
                                            code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation] SET [Customer_MobilePhone] = '" + TextBox1.Text + "' ,[CheckinDate] = '" + Convert.ToDateTime(TextBox12.Text).ToString("yyyy-MM-dd") + "' ,[CheckoutDate] = '" + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("yyyy-MM-dd") + "' ,[StayDays] = " + DropDownList1.SelectedValue + " , [TotalPrice] = " + TextBox4.Text + " ,[Deposit] = " + Deposit + ", [Remark] = N'" + TextBox6.Text + "' WHERE ID = " + id);
                                        }
                                        catch(Exception ex)
                                        {
                                            if(TextBox12.Text == null || TextBox12.Text == "")
                                            {
                                                code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation] SET [Customer_MobilePhone] = '" + TextBox1.Text + "' ,[CheckinDate] = '1990-01-01' ,[CheckoutDate] = '1990-01-01' ,[StayDays] = " + DropDownList1.SelectedValue + " , [TotalPrice] = " + TextBox4.Text + " ,[Deposit] = " + Deposit + ", [Remark] = N'" + TextBox6.Text + "' WHERE ID = " + id);
                                            }
                                            else
                                            {
                                                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('โปรแกรมคำนวนยอดไม่ถูกต้อง กรุณาลองใหม่อีกครั้ง');", true);
                                            }
                                        }
                                        SendLineNotify("แก้ไขการจองหมายเลข: "+ id+ "\r\nหมายเลขโทรศัพท์: " + TextBox1.Text + "\r\nเช็คอินวันที่: " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + "\r\nเช็คเอ้าท์วันที่: " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("dd MMMM yyyy") + "\r\n"+msg);
                                        Response.Redirect("./Reservation_Confirmed?id=" + id + "&check=" + TextBox1.Text);
                                    }
                                    else if (command == "rentmore" && TextBox1.Text != "02")
                                    {
                                        decimal Deposit = Convert.ToDecimal(TextBox5.Text);
                                        if (checkoldAccomRemoved == 0 && checkoldItemRemoved == 0 && totalnew.ToString() == TextBox10.Text)
                                        {
                                            if (CheckBox2.Checked == true)
                                            {
                                                for (int i = 0; i < cmds.Count; i++)
                                                {
                                                    code.DatabaseInsert(conn, cmds[i]);
                                                }
                                                Deposit += Convert.ToDecimal(TextBox10.Text);
                                                IsDeposit = false;
                                                if (CheckBox4.Checked == false)
                                                {
                                                    createReceipt(id, Convert.ToDouble(TextBox10.Text), dtReserve, IsDeposit, docCreatedDate, CheckBox5.Checked);
                                                }
                                                code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation] SET [Customer_MobilePhone] = '" + TextBox1.Text + "' ,[CheckinDate] = '" + Convert.ToDateTime(TextBox12.Text).ToString("yyyy-MM-dd") + "' ,[CheckoutDate] = '" + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("yyyy-MM-dd") + "' ,[StayDays] = " + DropDownList1.SelectedValue + " , [TotalPrice] = " + TextBox4.Text + " ,[Deposit] = " + Deposit + ", [Remark] = N'" + TextBox6.Text + "' WHERE ID = " + id);
                                                Response.Redirect("./Reservation_Confirmed?id=" + id + "&check=" + TextBox1.Text);
                                            }
                                        }
                                        else
                                        {
                                            TextBox10.Text = "";
                                            ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('โปรแกรมคำนวนยอดไม่ถูกต้อง กรุณาลองใหม่อีกครั้ง');", true);

                                        }
                                    }
                                    else
                                    {
                                        
                                        Response.Redirect("./Reservation_Confirmed?id=" + id + "&check=" + TextBox1.Text);
                                    }
                                }
                                else if (command == "checkin" && Session["permission"].ToString() == "True" && TextBox1.Text != "02")
                                {
                                    IsDeposit = false;
                                    code.DatabaseInsert(conn, "UPDATE [dbo].[Customer] SET [Name] = N'" + TextBox2.Text.Replace("'", "''") + "' ,[NickName] = N'" + TextBox3.Text.Replace("'", "''") + "',[FullName] = N'" + TextBox2.Text.Replace("'", "''") + "',[Address] = N'" + cleantext(TextBox8.Text) + "',[IDNumber] = N'" + TextBox9.Text.Replace("'", "''") + "',[Email] = N'" + TextBox13.Text.Replace("'", "''") + "',[Customer_Type_ID] = " + DropDownList8.SelectedValue + ",[Address_ID] = " + CheckAddressID(TextBox16.Text, DropDownList5.SelectedItem.Text, DropDownList6.SelectedItem.Text, DropDownList7.SelectedItem.Text) + ",[Address1] = N'" + TextBox17.Text.Replace("'", "''") + "',[Branch_Number] = N'" + TextBox18.Text.Replace("'", "''") + "' WHERE MobilePhone = '" + TextBox1.Text + "'");

                                    if (Convert.ToDecimal(TextBox4.Text) == Convert.ToDecimal(TextBox5.Text))
                                    {
                                        code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation] SET [Status] = N'เช็คอินแล้ว',[Deposit] = [TotalPrice] WHERE ID = " + id);
                                    }
                                    else
                                    {
                                        TextBox5.Enabled = false;
                                        DataTable dtfindDeposit = code.DatabaseQuery(conn, "Select * From Account_Receipt Where Reservation_ID = " + id + " AND IsDeposit = 'True' AND Status = 'Normal' AND UseDeposit = 'false'");
                                        
                                        dtReserve.Clear();
                                        dtReserve.AcceptChanges();
                                        foreach (GridViewRow row in GridView1.Rows)
                                        {
                                            CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                            if (chk != null && chk.Checked)
                                            {
                                                TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);
                                                IsDeposit = false;
                                                if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True")
                                                {
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), txtPeopleStay.Text, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(txtPeopleStay.Text));
                                                }
                                                else
                                                {
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), DropDownList1.SelectedValue, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue));
                                                }
                                            }
                                        }
                                        foreach (GridViewRow row in GridView2.Rows)
                                        {
                                            CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                            TextBox txtAmount = (row.Cells[2].FindControl("txtAmount") as TextBox);
                                            if (chk != null && chk.Checked)
                                            {
                                                IsDeposit = false;
                                                dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "2", dtItems.Rows[row.RowIndex]["ID"].ToString(), dtItems.Rows[row.RowIndex]["ItemName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), txtAmount.Text, dtItems.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, (Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue) * Convert.ToInt32(txtAmount.Text)));
                                            }
                                        }
                                        decimal DepositAmount = 0;
                                        decimal totalAmount = Convert.ToDecimal(TextBox4.Text);
                                        if (dtfindDeposit.Rows.Count <= 0)
                                        {

                                            decimal Deposit = Convert.ToDecimal(TextBox5.Text);
                                            dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", "17", "ส่วนลด", "1", "ครั้ง", Deposit * -1, Deposit * -1);
                                            id = Request.QueryString["id"];
                                            if (CheckBox4.Checked == false)
                                            {
                                                createReceipt(id, totalAmount - Deposit, dtReserve, IsDeposit, docCreatedDate, CheckBox5.Checked);
                                            }
                                            code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation] SET [Status] = N'เช็คอินแล้ว',[Deposit] = [TotalPrice] WHERE ID = " + id);
                                        }
                                        else
                                        {
                                            for (int j = 0; j < dtfindDeposit.Rows.Count; j++)
                                            {
                                                DataTable dtDepositDetail = code.DatabaseQuery(conn, "Select * From Account_Receipt_Detail Where Receipt_ID = '" + dtfindDeposit.Rows[j]["ID"].ToString() + "'");
                                                for (int k = 0; k < dtDepositDetail.Rows.Count; k++)
                                                {
                                                    DepositAmount += Convert.ToDecimal(dtDepositDetail.Rows[0]["Price_Amount"].ToString());
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", dtDepositDetail.Rows[0]["ProductType_ID"].ToString(), dtDepositDetail.Rows[0]["Product_ID"].ToString(), dtDepositDetail.Rows[0]["Product_Data"].ToString(), dtDepositDetail.Rows[0]["Product_Amount"].ToString(), dtDepositDetail.Rows[0]["Product_Unit"].ToString(), Convert.ToDecimal(dtDepositDetail.Rows[0]["Price_PerPeice"].ToString()) * -1, Convert.ToDecimal(dtDepositDetail.Rows[0]["Price_Amount"].ToString()) * -1);
                                                }
                                            }


                                            if (DepositAmount == Convert.ToDecimal(TextBox5.Text))
                                            {
                                                totalAmount = totalAmount - DepositAmount;
                                                id = Request.QueryString["id"];
                                                if (CheckBox4.Checked == false)
                                                {
                                                    createReceipt(id, totalAmount, dtReserve, IsDeposit, docCreatedDate, CheckBox5.Checked);
                                                }
                                                code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation] SET [Status] = N'เช็คอินแล้ว',[Deposit] = [TotalPrice] WHERE ID = " + id);
                                            }
                                            else
                                            {
                                                decimal remain = Convert.ToDecimal(TextBox5.Text) - ( DepositAmount);
                                                dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", "17", "ส่วนลด", "1", "ครั้ง", remain * -1, remain * -1);
                                                id = Request.QueryString["id"];
                                                totalAmount = totalAmount - (DepositAmount+remain);
                                                if (DepositAmount+remain+totalAmount == Convert.ToDecimal(TextBox4.Text))
                                                {
                                                    if (CheckBox4.Checked == false)
                                                    {
                                                        createReceipt(id, totalAmount, dtReserve, IsDeposit, docCreatedDate,CheckBox5.Checked);
                                                    }
                                                    code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation] SET [Status] = N'เช็คอินแล้ว',[Deposit] = [TotalPrice] WHERE ID = " + id);
                                                }

                                                
                                                
                                            }
                                        }
                                    }
                                    
                                    Response.Redirect("/ReserveTable");
                                }
                                else if (command == "reserve")
                                {
                                    try
                                    {
                                        if (Convert.ToDateTime(TextBox12.Text) > DateTime.Parse("1999-01-01"))
                                        {
                                            if (Session["permission"].ToString() == "True")
                                            {
                                                Reservation_ID = code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation] ([Customer_MobilePhone],[CheckinDate],[CheckoutDate],[StayDays],[Status],[TotalPrice],[Deposit],[Remark],[Reserve_By],[Created_Date],NoCreateReceipt,NoNameinReceipt) VALUES ('" + TextBox1.Text + "','" + Convert.ToDateTime(TextBox12.Text).ToString("yyyy-MM-dd") + "','" + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("yyyy-MM-dd") + "'," + DropDownList1.SelectedValue + ",N'มัดจำแล้ว'," + Session["totalPrice"].ToString() + "," + TextBox5.Text + ",N'" + TextBox6.Text + "', N'" + Session["UserName"].ToString() + "','" + DateTime.Now + "','"+CheckBox4.Checked+"','"+CheckBox3.Checked+"') SELECT SCOPE_IDENTITY(); ");
                                                
                                                
                                            }
                                            else
                                            {
                                                Reservation_ID = code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation] ([Customer_MobilePhone],[CheckinDate],[CheckoutDate],[StayDays],[Status],[TotalPrice],[Deposit],[Remark],[Reserve_By],[Created_Date],NoCreateReceipt,NoNameinReceipt) VALUES ('" + TextBox1.Text + "','" + Convert.ToDateTime(TextBox12.Text).ToString("yyyy-MM-dd") + "','" + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("yyyy-MM-dd") + "'," + DropDownList1.SelectedValue + ",N'มัดจำแล้ว'," + Session["totalPrice"].ToString() + "," + TextBox5.Text + ",N'" + TextBox6.Text + "', N'User','" + DateTime.Now + "','"+CheckBox4.Checked+"','" + CheckBox3.Checked + "') SELECT SCOPE_IDENTITY(); ");
                                            }
                                        }
                                        else
                                        {
                                            if (Session["permission"].ToString() == "True")
                                            {
                                                Reservation_ID = code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation] ([Customer_MobilePhone],[CheckinDate],[CheckoutDate],[StayDays],[Status],[TotalPrice],[Deposit],[Remark],[Reserve_By],[Created_Date],NoCreateReceipt,NoNameinReceipt) VALUES ('" + TextBox1.Text + "','1990-01-01','1990-01-01'," + DropDownList1.SelectedValue + ",N'มัดจำแล้ว'," + Session["totalPrice"].ToString() + "," + TextBox5.Text + ",N'" + TextBox6.Text + "',N'" + Session["UserName"].ToString() + "','" + DateTime.Now + "','"+CheckBox4.Checked+"','" + CheckBox3.Checked + "') SELECT SCOPE_IDENTITY(); ");
                                            }
                                            else
                                            {
                                                Reservation_ID = code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation] ([Customer_MobilePhone],[CheckinDate],[CheckoutDate],[StayDays],[Status],[TotalPrice],[Deposit],[Remark],[Reserve_By],[Created_Date],NoCreateReceipt,NoNameinReceipt) VALUES ('" + TextBox1.Text + "','1990-01-01','1990-01-01'," + DropDownList1.SelectedValue + ",N'มัดจำแล้ว'," + Session["totalPrice"].ToString() + "," + TextBox5.Text + ",N'" + TextBox6.Text + "',N'User','" + DateTime.Now + "','"+CheckBox4.Checked+"','" + CheckBox3.Checked + "') SELECT SCOPE_IDENTITY(); ");
                                            }
                                        }

                                    }
                                    catch
                                    {
                                        if (Session["permission"].ToString() == "True")
                                        {
                                            Reservation_ID = code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation] ([Customer_MobilePhone],[CheckinDate],[CheckoutDate],[StayDays],[Status],[TotalPrice],[Deposit],[Remark],[Reserve_By],[Created_Date],NoCreateReceipt,NoNameinReceipt)) VALUES ('" + TextBox1.Text + "','1990-01-01','1990-01-01'," + DropDownList1.SelectedValue + ",N'มัดจำแล้ว'," + Session["totalPrice"].ToString() + "," + TextBox5.Text + ",N'" + TextBox6.Text + "',N'" + Session["UserName"].ToString() + "','" + DateTime.Now + "','"+CheckBox4.Checked+"','" + CheckBox3.Checked + "') SELECT SCOPE_IDENTITY(); ");
                                        }
                                        else
                                        {
                                            Reservation_ID = code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation] ([Customer_MobilePhone],[CheckinDate],[CheckoutDate],[StayDays],[Status],[TotalPrice],[Deposit],[Remark],[Reserve_By],[Created_Date],NoCreateReceipt,NoNameinReceipt)) VALUES ('" + TextBox1.Text + "','1990-01-01','1990-01-01'," + DropDownList1.SelectedValue + ",N'มัดจำแล้ว'," + Session["totalPrice"].ToString() + "," + TextBox5.Text + ",N'" + TextBox6.Text + "',N'User','" + DateTime.Now + "','"+CheckBox4.Checked+"','" + CheckBox3.Checked + "') SELECT SCOPE_IDENTITY(); ");
                                        }
                                    }
                                    string ID = "";
                                    try
                                    {
                                        if (Reservation_ID > 0)
                                        {
                                            ID = Reservation_ID.ToString();
                                        }
                                        else if (Convert.ToInt32(id) > 0)
                                        {
                                            ID = id.ToString();
                                        }
                                    }
                                    catch { }
                                    int i = 0;
                                    string msg = "";
                                    foreach (GridViewRow row in GridView1.Rows)
                                    {
                                        CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                        if (chk != null && chk.Checked)
                                        {
                                            TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);

                                            int Price = 0;
                                            if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True")
                                            {
                                                if (Convert.ToInt32(DropDownList1.SelectedValue) > 1)
                                                {
                                                    for (int k = 0; k < Convert.ToInt32(DropDownList1.SelectedValue); k++)
                                                    {
                                                        DataTable dtHolidayPrice = code.DatabaseQuery(conn, "Select * from Accommodation_HolidayPrice Where Accommodation_ID = " + dtAccommodation.Rows[i]["ID"].ToString() + " AND  DateNewPrice = '" + Convert.ToDateTime(TextBox12.Text).AddDays(k).ToString("yyyy-MM-dd") + "'");
                                                        DataTable dtPrice = code.DatabaseQuery(conn, "SELECT [Price] FROM [Accommodation] Where ID = " + dtAccommodation.Rows[i]["ID"].ToString());
                                                        if (dtHolidayPrice.Rows.Count > 0)
                                                        {
                                                            if(Convert.ToInt32(row.Cells[4].Text) == Convert.ToInt32(dtPrice.Rows[0][0].ToString()) || Convert.ToInt32(row.Cells[4].Text) == Convert.ToInt32(dtHolidayPrice.Rows[0]["Price"].ToString()))
                                                            {
                                                                Price += Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(dtHolidayPrice.Rows[0]["Price"].ToString());
                                                            }
                                                            else
                                                            {
                                                                Price += Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(row.Cells[4].Text);
                                                            }
                                                            
                                                        }
                                                        else
                                                        {
                                                            Price += Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(row.Cells[4].Text);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    Price =  Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(txtPeopleStay.Text);
                                                }
                                                msg += "- " + dtAccommodation.Rows[i]["AccomName"].ToString() + " " + txtPeopleStay.Text + " " + dtAccommodation.Rows[i]["Unit"].ToString() + "\r\n";


                                            }
                                            else
                                            {
                                                if (Convert.ToInt32(DropDownList1.SelectedValue) > 1)
                                                {
                                                    for (int k = 0; k < Convert.ToInt32(DropDownList1.SelectedValue); k++)
                                                    {
                                                        DataTable dtHolidayPrice = code.DatabaseQuery(conn, "Select * from Accommodation_HolidayPrice Where Accommodation_ID = " + dtAccommodation.Rows[i]["ID"].ToString() + " AND  DateNewPrice = '" + Convert.ToDateTime(TextBox12.Text).AddDays(k).ToString("yyyy-MM-dd") + "'");
                                                        DataTable dtPrice = code.DatabaseQuery(conn, "SELECT [Price] FROM [Accommodation] Where ID = " + dtAccommodation.Rows[i]["ID"].ToString()); 
                                                        if (dtHolidayPrice.Rows.Count > 0)
                                                        {
                                                            if (Convert.ToInt32(row.Cells[4].Text) == Convert.ToInt32(dtPrice.Rows[0][0].ToString()) || Convert.ToInt32(row.Cells[4].Text) == Convert.ToInt32(dtHolidayPrice.Rows[0]["Price"].ToString()))
                                                            {
                                                                Price += Convert.ToInt32(dtHolidayPrice.Rows[0]["Price"].ToString());
                                                            }
                                                            else
                                                            {
                                                                Price += Convert.ToInt32(row.Cells[4].Text);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            Price += Convert.ToInt32(row.Cells[4].Text);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    Price = Convert.ToInt32(DropDownList1.SelectedValue) * Convert.ToInt32(row.Cells[4].Text);
                                                }
                                                msg += "- " + dtAccommodation.Rows[i]["AccomName"].ToString() + " " + DropDownList1.SelectedValue + " " + dtAccommodation.Rows[i]["Unit"].ToString() + "\r\n";

                                            }
                                            if (Convert.ToDouble(TextBox5.Text) < Convert.ToDouble(TextBox4.Text))
                                            {

                                            }
                                            else if (Convert.ToDouble(TextBox5.Text) == Convert.ToDouble(TextBox4.Text))
                                            {
                                                IsDeposit = false;
                                                if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True")
                                                {
                                                    if(( Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(txtPeopleStay.Text) ) == Price)
                                                    {
                                                        if ((Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(txtPeopleStay.Text)) == Price)
                                                        {
                                                            dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[i]["ID"].ToString(), dtAccommodation.Rows[i]["AccomName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), txtPeopleStay.Text, dtAccommodation.Rows[i]["Unit"].ToString(), row.Cells[4].Text, Price);
                                                           
                                                        }
                                                        else
                                                        {
                                                            int AmounrPerUnit = (Price / Convert.ToInt32(txtPeopleStay.Text));
                                                            dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[i]["ID"].ToString(), dtAccommodation.Rows[i]["AccomName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), txtPeopleStay.Text, dtAccommodation.Rows[i]["Unit"].ToString(), AmounrPerUnit, Price);
                                                            
                                                        }
                                                    }
                                                    else
                                                    {
                                                        int AmounrPerUnit = (Price / Convert.ToInt32(txtPeopleStay.Text));
                                                        dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[i]["ID"].ToString(), dtAccommodation.Rows[i]["AccomName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), txtPeopleStay.Text, dtAccommodation.Rows[i]["Unit"].ToString(), AmounrPerUnit, Price);
                                                         }
                                                    
                                                }
                                                else
                                                { 
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[i]["ID"].ToString(), dtAccommodation.Rows[i]["AccomName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), DropDownList1.SelectedValue, dtAccommodation.Rows[i]["Unit"].ToString(), row.Cells[4].Text, Price);
                                                    }
                                            }
                                            code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation_Accommodation] ([Reservation_ID],[Accommodation_ID],[Amount],[Price]) VALUES (" + Reservation_ID + "," + dtAccommodation.Rows[i]["ID"].ToString() + "," + txtPeopleStay.Text + "," + Price + ") ");
                                        }
                                        i++;
                                    }
                                    i = 0;
                                    msg += "\r\nรายการของเช่า\r\n";
                                    foreach (GridViewRow row in GridView2.Rows)
                                    {
                                        CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                        if (chk != null && chk.Checked)
                                        {
                                            TextBox txtAmount = (row.Cells[2].FindControl("txtAmount") as TextBox);
                                            int Price = 0;
                                            if (dtItems.Rows[row.RowIndex]["LimitWithAmount"].ToString() == "True")
                                            {

                                                Price = Convert.ToInt32(DropDownList1.SelectedValue) * Convert.ToInt32(txtAmount.Text) * Convert.ToInt32(row.Cells[4].Text);
                                            }
                                            else
                                            {
                                                Price = Convert.ToInt32(DropDownList1.SelectedValue) * Convert.ToInt32(row.Cells[4].Text);
                                            }
                                            if (Convert.ToDouble(TextBox5.Text) < Convert.ToDouble(TextBox4.Text))
                                            {

                                            }
                                            else if (Convert.ToDouble(TextBox5.Text) == Convert.ToDouble(TextBox4.Text))
                                            {
                                                IsDeposit = false;
                                                dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "2", dtItems.Rows[i]["ID"].ToString(), dtItems.Rows[i]["ItemName"].ToString() + " เช็คอิน " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), txtAmount.Text, dtItems.Rows[i]["Unit"].ToString(), row.Cells[4].Text, Price);

                                            }
                                            code.DatabaseInsert(conn, "INSERT INTO [dbo].[Reservation_Items] ([Reservation_ID],[Items_ID],[Amount],[Price]) VALUES (" + Reservation_ID + "," + dtItems.Rows[i]["ID"].ToString() + "," + txtAmount.Text + "," + Price + ") ");
                                            try { msg += "- " + dtItems.Rows[i]["ItemName"].ToString() + " " + txtAmount.Text + " ชิ้น"; } catch { }
                                        }
                                        i++;
                                    }
                                    DataTable dtCustomer = code.DatabaseQuery(conn, "Select * From Customer Where MobilePhone = '" + TextBox1.Text + "'");
                                    if (dtCustomer.Rows.Count == 1)
                                    {

                                    }
                                    else
                                    {
                                        code.DatabaseInsert(conn, "INSERT INTO [dbo].[Customer]([MobilePhone],[Name],[NickName],[ComeFrom],[Remark],FullName,Address,IDNumber,Email,Customer_Type_ID,Address_ID,Address1,Branch_Number) VALUES ('" + TextBox1.Text + "',N'" + TextBox2.Text.Replace("'", "''") + "',N'" + TextBox3.Text.Replace("'", "''") + "','','',N'" + TextBox2.Text + "',N'" + cleantext(TextBox8.Text) + "',N'" + TextBox9.Text + "',N'" + TextBox13.Text + "'," + DropDownList8.SelectedValue + "," + CheckAddressID(TextBox16.Text, DropDownList5.SelectedItem.Text, DropDownList6.SelectedItem.Text, DropDownList7.SelectedItem.Text) + ",N'" + TextBox17.Text.Replace("'", "''") + "',N'" + TextBox18.Text.Replace("'", "''") + "')");
                                    }
                                    
                                    try
                                    {
                                        if (Reservation_ID > 0)
                                        {
                                            ID = Reservation_ID.ToString();
                                        }
                                        else if (Convert.ToInt32(id) > 0)
                                        {
                                            ID = id.ToString();
                                        }
                                    }
                                    catch { }

                                    try
                                    {
                                        uploadSlip(Reservation_ID.ToString());
                                    }
                                    catch { }

                                    if (TextBox1.Text != "02" && CheckBox4.Checked == false)
                                    {
                                        if (CheckBox4.Checked == false)
                                        {
                                            createReceipt(ID, Convert.ToDouble(TextBox5.Text), dtReserve, IsDeposit, docCreatedDate,CheckBox5.Checked);
                                        }
                                    }
                                    msg += "\r\nหมายเหตุ:"+ TextBox6.Text;
                                    SendLineNotify("ลูกค้าจองห้องพักใหม่หมายเลขการจอง: "+Reservation_ID+"\r\nหมายเลขโทรศัพท์: "+ TextBox1.Text + "\r\nเช็คอินวันที่: " + Convert.ToDateTime(TextBox12.Text).ToString("dd MMMM yyyy") + "\r\nเช็คเอ้าท์วันที่: " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("dd MMMM yyyy") + "\r\n"+msg);

                                    Response.Redirect("./Reservation_Confirmed?id=" + ID + "&check=" + TextBox1.Text);
                                }
                            }
                            catch
                            {
                                string ID = "";
                                try
                                {
                                    if (Reservation_ID > 0)
                                    {
                                        ID = Reservation_ID.ToString();
                                    }
                                    else if (Convert.ToInt32(id) > 0)
                                    {
                                        ID = id.ToString();
                                    }
                                }
                                catch { }
                                Response.Redirect("./Reservation_Confirmed?id=" + ID + "&check=" + TextBox1.Text);
                            }
                        }
                        else
                        {
                            ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาอัพโหลดสลิป');", true);
                        }
                    }
                    else
                    {
                        ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาระบุยอดจำนวนเงินที่โอนมามัดจำ');", true);
                    }
                }
                else
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเลือกที่พักที่ต้องการจอง');", true);
                }
            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาระบุเบอร์โทรศัพท์ หรือ เลขสาขาให้ครบ5หลัก');", true);
            }
        }

        private static UserCredential Login(string googleClientId, string googleClientSecret, string[] scopes)
        {
            ClientSecrets secrets = new ClientSecrets()
            {
                ClientId = googleClientId,
                ClientSecret = googleClientSecret
            };
            return GoogleWebAuthorizationBroker.AuthorizeAsync(secrets, scopes,user:"user",CancellationToken.None).Result;
        }

        public void SendLineNotify(string Message)
        {
            try
            {
                string lineToken = ConfigurationSettings.AppSettings["linetoken"].ToString();
                string message = Message;
                int stickerPackageID = 0;
                int stickerID = 0;
                //string pictureUrl = ConfigurationSettings.AppSettings["prefixurl"].ToString() + HttpContext.Current.Request.Url.Authority + "/" + ConfigurationSettings.AppSettings["virtualprefixpicturepath"].ToString() + "/Images/CheckIn_Display/" + ID + ".jpg";
                //string message = HttpUtility.UrlEncode(message, Encoding.UTF8);
                var request = (HttpWebRequest)WebRequest.Create(ConfigurationSettings.AppSettings["lineurl"].ToString());
                var postData = string.Format("message={0}", message.Replace("*", "x").Replace("\"", ""));

                if (stickerPackageID > 0 && stickerID > 0)
                {
                    var stickerPackageId = string.Format("stickerPackageId={0}", stickerPackageID);
                    var stickerId = string.Format("stickerId={0}", stickerID);
                    postData += "&" + stickerPackageId.ToString() + "&" + stickerId.ToString();
                }
                //if (pictureUrl != "")
                //{
                //    var imageThumbnail = string.Format("imageThumbnail={0}", pictureUrl);
                //    var imageFullsize = string.Format("imageFullsize={0}", pictureUrl);
                //    postData += "&" + imageThumbnail.ToString() + "&" + imageFullsize.ToString();
                //}
                var data = Encoding.UTF8.GetBytes(postData);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = data.Length;
                request.Headers.Add("Authorization", "Bearer " + lineToken);
                using (var stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);
                var response = (HttpWebResponse)request.GetResponse();
                var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            }
            catch { }
        }

        public void SendEmail(string SMTP,int Port,bool EnableSsl,bool UseDefaultCredentials, string from,string password, string to, string cc, string subject, string body, Attachment[] data)
        {
            
            MailMessage mail = new MailMessage(from, to);
            SmtpClient client = new SmtpClient();
            client.Host = SMTP;
            client.Port = Port;
            client.EnableSsl = EnableSsl;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = UseDefaultCredentials;
            client.Credentials = new NetworkCredential(from,password);
            try
            {
                mail.CC.Add(cc);
            }
            catch { }
            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = true;
            try
            {
                for (int i = 0; i < data.Length; i++)
                {
                    mail.Attachments.Add(data[i]);
                }

            }
            catch { }
            client.Send(mail);
        }

        public void uploadSlip(string ID)
        {
           
            try
            {
                if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox1.Text + ".jpg"))
                {
                    try
                    {
                        if (Convert.ToInt32(ID) > 0)
                        {
                            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + ID + "_" + TextBox1.Text + ".jpg"))
                            {
                                File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + ID + "_" + TextBox1.Text + ".jpg");
                            }
                            File.Move(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox1.Text + ".jpg", AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + ID + "_" + TextBox1.Text + ".jpg");
                        }
                    }
                    catch
                    {
                        File.Move(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox1.Text + ".jpg", AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + ID + "_" + TextBox1.Text + ".jpg");
                    }

                }
                else
                {
                    if (FileUpload1.HasFile)
                    {
                        if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox1.Text + ".jpg"))
                        {
                            File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox1.Text + ".jpg");
                        }
                        if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + ID + "_" + TextBox1.Text + ".jpg"))
                        {
                            File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + ID + "_" + TextBox1.Text + ".jpg");
                        }

                        string FileSaveWithPath = "";
                        string filename = ID + "_" + TextBox1.Text + ".jpg";
                        FileSaveWithPath = Server.MapPath("\\Upload\\Slip\\" + filename.Replace("/", "").Replace("\\", "").Replace("'", ""));
                        FileUpload1.SaveAs(FileSaveWithPath);
                    }
                }
            }
            catch { }
        }

        public string cleantext(string input)
        {
            string output = input.Replace(",", "").Replace("'", "").Replace("\"", "");
            return output;
        }

        public void createReceipt(string Reservation_ID, double Total_Amount,DataTable dtReserve,bool IsDeposit,DateTime docDate,bool etax)
        {
            string status = "Normal";
            if (Total_Amount > 0)
            {
                string ReceiptID = code.createDocNumber(conn, "Account_Receipt", "REC",docDate.Year.ToString(),docDate.Month.ToString(),docDate.Day.ToString());
                DataTable dtuseVat = code.DatabaseQuery(conn, "select Use_Vat from Business_Info");
                double PriceExcludeVat = Total_Amount;
                double Vat = 0;
                if (dtuseVat.Rows[0][0].ToString() == "True")
                {
                    PriceExcludeVat = (Total_Amount * 100) / 107;
                    Vat = Total_Amount - PriceExcludeVat;
                    PriceExcludeVat = TwoDecimalPoints(PriceExcludeVat);
                    Vat = TwoDecimalPoints(Vat);
                }
                else
                {

                }
                string created_By_ID = "";
                try
                {
                    created_By_ID = Session["UserID"].ToString();
                }
                catch
                {
                    created_By_ID = "0";
                }
                if (IsDeposit == true)
                {
                    code.DatabaseInsert(conn, "INSERT INTO [dbo].[Account_Receipt] (ID,[Reservation_ID],[Created_Date],[Total_Amount],[Vat],[Total_Amount_Exclude_Vat],[IsDeposit],[UseDeposit],Status,Paid_Type,Created_By_ID,Etax) VALUES ('" + ReceiptID + "','" + Reservation_ID + "','" + docDate.ToString("yyyy-MM-dd") + "'," + Total_Amount + "," + Vat + "," + PriceExcludeVat + ",'True','False','Normal',N'"+DropDownList2.SelectedItem.Text+"',N'"+created_By_ID+"','"+CheckBox5.Checked+"');");
                    code.DatabaseInsert(conn, "INSERT INTO [dbo].[Account_Receipt_Detail] ([Number],[Receipt_ID],[ProductType_ID],[Product_ID],[Product_Data],[Product_Amount],[Product_Unit],[Price_PerPeice],[Price_Amount]) Values ('1','" + ReceiptID + "',1,7,N'ค่ามัดจำที่พักของหมายเลขการจอง "+ Reservation_ID + " ["+ ReceiptID + "]','1',N'ครั้ง'," + Total_Amount + "," + Total_Amount + ")");
                }
                else
                {
                    code.DatabaseInsert(conn, "INSERT INTO [dbo].[Account_Receipt] (ID,[Reservation_ID],[Created_Date],[Total_Amount],[Vat],[Total_Amount_Exclude_Vat],[IsDeposit],[UseDeposit],Status,Paid_Type,Created_By_ID,Etax) VALUES ('" + ReceiptID + "','" + Reservation_ID + "','" + docDate.ToString("yyyy-MM-dd") + "'," + Total_Amount + "," + Vat + "," + PriceExcludeVat + ",'False','False','Normal',N'" + DropDownList2.SelectedItem.Text + "',N'" + created_By_ID + "','"+CheckBox5.Checked+"');");

                    for (int i = 0;i<dtReserve.Rows.Count;i++)
                    {
                        code.DatabaseInsert(conn, "INSERT INTO [dbo].[Account_Receipt_Detail] ([Number],[Receipt_ID],[ProductType_ID],[Product_ID],[Product_Data],[Product_Amount],[Product_Unit],[Price_PerPeice],[Price_Amount]) Values ('"+dtReserve.Rows[i]["Number"].ToString()+ "','" + ReceiptID + "'," + dtReserve.Rows[i]["ProductType_ID"].ToString() + "," + dtReserve.Rows[i]["Product_ID"].ToString() + ",N'" + dtReserve.Rows[i]["Product_Data"].ToString() + "'," + dtReserve.Rows[i]["Product_Amount"].ToString() + ",N'" + dtReserve.Rows[i]["Product_Unit"].ToString() + "'," + dtReserve.Rows[i]["Price_PerPeice"].ToString() + "," + dtReserve.Rows[i]["Price_Amount"].ToString() + ")");
                    }
                }
                if (CheckBox4.Checked == false)
                {
                    createReport(ReceiptID, status, docDate);
                }

                if (etax == true)
                {
                    DataTable dtReceipt = code.DatabaseQuery(conn, "SELECT  [ID] FROM [Account_Receipt] Where RESERVATION_ID = '" + Reservation_ID + "'");

                    string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
                    string pdfpath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + dtReceipt.Rows[0]["ID"].ToString() + "_etax.pdf";

                    string pdfFilePath = pdfpath;
                    byte[] bytes = System.IO.File.ReadAllBytes(pdfFilePath);
                    Attachment[] dataall = new Attachment[1];
                    MemoryStream pdf = new MemoryStream(bytes);
                    Attachment data = new Attachment(pdf, dtReceipt.Rows[0]["ID"].ToString() + "_etax.pdf");
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

                    SendEmail(ConfigurationSettings.AppSettings["SMTP"].ToString(), Convert.ToInt32(ConfigurationSettings.AppSettings["SMTP_Port"].ToString()), Convert.ToBoolean(ConfigurationSettings.AppSettings["SMTP_EnableSsl"].ToString()), Convert.ToBoolean(ConfigurationSettings.AppSettings["SMTP_UseDefaultCredentials"].ToString()), ConfigurationSettings.AppSettings["Email_From"].ToString(), ConfigurationSettings.AppSettings["Email_Password_From"].ToString(), TextBox13.Text, ConfigurationSettings.AppSettings["Email_CC"].ToString(), subject, body, dataall);
                }
            }

            
        }

        public void createReport(string DocNumber,string status,DateTime docDate)
        {
            string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
            try
            {
                System.IO.Directory.CreateDirectory(path+"\\"+docDate.Year.ToString());
                System.IO.Directory.CreateDirectory(path + "\\" + docDate.Year.ToString() + "\\" + DateTime.Now.Month.ToString());
            }
            catch(Exception ex)
            {

            }
            string RecNumber = DocNumber;
            DataTable dtbusinessinfo = code.DatabaseQuery(conn, "Select * from Business_Info left join Customer_Type on Business_Type_ID = Customer_Type.ID left join Address on Address.ID = Address_ID");

            DataTable dtReceiptDetail = code.DatabaseQuery(conn, "SELECT * FROM [Account_Receipt_Detail] inner join Account_ProductType on Account_ProductType.ID = ProductType_ID Where Receipt_ID = '" + RecNumber + "' order by Number ASC");
            DataTable dtReceipt = code.DatabaseQuery(conn, "SELECT * FROM [Account_Receipt] inner join Reservation on Reservation.ID = Reservation_ID Where Account_Receipt.ID = '" + RecNumber + "'");
            DataTable dtcustomer = code.DatabaseQuery(conn, "Select * from Customer left join Customer_Type on Customer_Type_ID = Customer_Type.ID left join Address on Address.ID = Address_ID Where MobilePhone = '" + dtReceipt.Rows[0]["Customer_MobilePhone"].ToString() + "'");

            DataTable dtCustomerReport = new DataTable();
            dtCustomerReport = dtcustomer.Copy();
            DataTable dtBusinessinfoReport = new DataTable();
            dtBusinessinfoReport = dtbusinessinfo.Copy();

            try
            {
                try
                {
                    if (DropDownList8.SelectedIndex == 0 && TextBox18.Text == "00000")
                    {
                        dtCustomerReport.Rows[0]["FullName"] = TextBox2.Text;
                    }
                    else if (DropDownList8.SelectedIndex == 0 && Convert.ToInt32(TextBox18.Text) > 0)
                    {
                        dtCustomerReport.Rows[0]["FullName"] = TextBox2.Text + " สาขาที่ " + TextBox18.Text;
                    }
                    else
                    {
                        dtCustomerReport.Rows[0]["FullName"] = TextBox2.Text;
                    }
                }
                catch { }
                try
                {
                    if (dtcustomer.Rows[0]["Province"].ToString().Contains("กรุงเทพ"))
                    {
                        dtCustomerReport.Rows[0]["Address"] = dtcustomer.Rows[0]["Address"].ToString() + " " + dtcustomer.Rows[0]["Address1"].ToString() + " แขวง " + dtcustomer.Rows[0]["SubDistrict"].ToString() + " เขต " + dtcustomer.Rows[0]["District"].ToString() + " " + dtcustomer.Rows[0]["Province"].ToString() + " " + dtcustomer.Rows[0]["PostalCode"].ToString();
                    }
                    else
                    {
                        dtCustomerReport.Rows[0]["Address"] = dtcustomer.Rows[0]["Address"].ToString() + " " + dtcustomer.Rows[0]["Address1"].ToString() + " ต." + dtcustomer.Rows[0]["SubDistrict"].ToString() + " อ." + dtcustomer.Rows[0]["District"].ToString() + " จ." + dtcustomer.Rows[0]["Province"].ToString() + " " + dtcustomer.Rows[0]["PostalCode"].ToString();
                    }
                }
                catch
                {
                    dtCustomerReport.Rows[0]["Address"] = dtcustomer.Rows[0]["Address"].ToString();
                }
                dtCustomerReport.Rows[0]["IDNumber"] = TextBox9.Text;
                dtCustomerReport.Rows[0]["MobilePhone"] = TextBox1.Text;
                dtCustomerReport.Rows[0]["Email"] = TextBox13.Text;
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

            if (CheckBox3.Checked == true)
            {
                dtCustomerReport.Rows[0]["FullName"] = "ประสงค์ไม่รับใบกำกับภาษี";
                dtCustomerReport.Rows[0]["Address"] = "";
                dtCustomerReport.Rows[0]["IDNumber"] = "";
            }

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

            DataTable dtCreator = new DataTable();
            string CreatorFullName = "";
            string createdsigpath = "";
            try
            {
                dtCreator = code.DatabaseQuery(conn, "Select * from Admin Where ID = " + Session["UserID"].ToString());
                CreatorFullName = dtCreator.Rows[0]["FirstName"].ToString() + " " + dtCreator.Rows[0]["LastName"].ToString();
                createdsigpath = "File:\\" + Signaturepath + "\\" + CreatorFullName.ToLower() + ".png";
            }
            catch
            {
                CreatorFullName = dtApprover.Rows[0]["FirstName"].ToString() + " " + dtApprover.Rows[0]["LastName"].ToString();
                createdsigpath = "File:\\" + Signaturepath + "\\" + ApproverFullName.ToLower() + ".png";
            }

            dtSignature.Rows.Add(ApproverFullName, "File:\\" + Signaturepath + "\\" + ApproverFullName.ToLower() + ".png", CreatorFullName,createdsigpath);

            //GridView1.DataSource = dt;
            //GridView1.DataBind();

            string pdfpath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + DocNumber + ".pdf";

            try
            {

               

                Account.Report.DataSet1 dataSet1 = new Account.Report.DataSet1();
                dataSet1.Tables.Add(dtbusinessinfo);
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



                    using (FileStream fs = new FileStream(pdfpath, FileMode.Create))
                        {
                            fs.Write(bytes, 0, bytes.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                    }

                    //ReportViewer2.LocalReport.Refresh();


                }
            
            catch { }

            if (CheckBox5.Checked == true)
            {
                try
                {
                    string xmlFilePath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + DocNumber + ".xml";
                    string xmlString = System.IO.File.ReadAllText(ConfigurationSettings.AppSettings["BaseFolderPath"].ToString() + "\\Resources\\template.xml");
                    xmlString = xmlString.Replace("*invoice_id", DocNumber);
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
                    xmlString = xmlString.Replace("*seller_address1", dtbusinessinfo.Rows[0]["Address"].ToString()+" "+ dtbusinessinfo.Rows[0]["Address1"].ToString() + " " + dtbusinessinfo.Rows[0]["SubDistrict"].ToString() + " " + dtbusinessinfo.Rows[0]["District"].ToString() + " " + dtbusinessinfo.Rows[0]["Province"].ToString() + " " + dtbusinessinfo.Rows[0]["PostalCode"].ToString());
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
                            if(dtcustomer.Rows[0]["Branch_Number"].ToString().Length == 5)
                            {
                                bnumber = dtcustomer.Rows[0]["Customer_Code"].ToString();
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


                    xmlString = xmlString.Replace("*buyer_DefinedCITradeContact", TextBox13.Text);
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
                    xmlString = xmlString.Replace("*item", "ค่าที่พักหรือค่ามัดจำที่พัก");
                    xmlString = xmlString.Replace("*invoice_billedquantity", "1");

                    System.IO.File.WriteAllText(xmlFilePath, xmlString, System.Text.Encoding.UTF8);
                }
                catch { }

                try
                {
                    {


                        PDFA3Invoice pdf = new PDFA3Invoice();
                        string pdfFilePath = path + "\\" + docDate.Year.ToString()+ "\\" + docDate.Month.ToString() + "\\" + DocNumber + ".pdf";
                        string xmlFilePath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + DocNumber + ".xml";

                        string xmlFileName = "ETDA-invoice.xml";


                        string xmlVersion = "1.0";
                        string documentID = DocNumber;
                        string documentOID = "";

                        string outputPath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + DocNumber + "_etax.pdf";

                        pdf.CreatePDFA3Invoice(pdfFilePath, xmlFilePath, xmlFileName, xmlVersion, documentID, documentOID, outputPath, "Tax Invoice");


                    }
                }
                catch
                {

                }
                //ReportViewer2.LocalReport.Refresh();

            }


        }

        public double TwoDecimalPoints(double num)
        {
            var totalCost = Convert.ToDouble(String.Format("{0:0.00}", num));
            return totalCost;
        }

        protected void TextBox5_TextChanged(object sender, EventArgs e)
        {
            
            int minDeposit = Convert.ToInt32(Label2.Text);
            try
            {
                if (Session["permission"].ToString() == "True")
                {

                }
                else
                {
                    if (Convert.ToDecimal(TextBox5.Text) < minDeposit * 0.8m)
                    {
                        TextBox5.Text = "0";
                        ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาโอนยอดมัดจำจองมากกว่ายอดมัดจำจองขั้นต่ำ');", true);
                    }
                }
            }
            catch
            {
                if (Convert.ToDecimal(TextBox5.Text) < minDeposit * 0.8m)
                {
                    TextBox5.Text = "0";
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาโอนยอดมัดจำจองมากกว่ายอดมัดจำจองขั้นต่ำ');", true);
                }
            }






        }

        protected void TextBox1_TextChanged(object sender, EventArgs e)
        {
            
            TextBox1.Text = TextBox1.Text.Replace("ชื่อเล่น", "").Replace("ชื่อ", "").Replace("คะ", "").Replace("ค่ะ", "").Replace("ค่า", "").Replace("ครับ", "").Replace("คับ", "").Replace("เบอร์", "").Replace("เบอ", "");
            string[] txt1input = TextBox1.Text.Split(' ');
            int phoneid = -1;
            string name = "";
            for (int i = 0; i < txt1input.Length; i++)
            {
                int checkint1;
                int checkint2;
                try
                {
                    if (Int32.TryParse(txt1input[i][0].ToString(), out checkint1) && Int32.TryParse(txt1input[i][txt1input.Length - 1].ToString(), out checkint2))
                    {
                        if (checkint1 >= 0 && checkint2 >= 0)
                        {
                            TextBox1.Text = txt1input[i].Replace("-", "");
                            phoneid = i;
                        }
                    }
                    else
                    {

                    }
                }
                catch { }
            }
            if (TextBox2.Text == string.Empty || TextBox2.Text == "")
            {
                for (int j = 0; j < txt1input.Length; j++)
                {
                    if (j != phoneid)
                    {
                        name += txt1input[j] + " ";
                    }
                }

                string[] names = name.Split(' ');
                name = "";
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i] == " ")
                    {

                    }
                    else
                    {
                        name += names[i] + " ";
                    }
                }
                for (int i = 0; i < names.Length; i++)
                {
                    if (name[name.Length - 1] == ' ')
                    {
                        name = name.Substring(0, name.Length - 1);
                    }
                }
                TextBox2.Text = name;
            }




            TextBox1.Text = TextBox1.Text.Replace(" ", "").Replace("-", "");
            DataTable dtCustomer = code.DatabaseQuery(conn, "SELECT * FROM [Customer] left join Customer_Type on Customer_Type_ID = Customer_Type.ID left join Address on Address.ID = Address_ID Where MobilePhone = '" + TextBox1.Text + "'");
            
            if (dtCustomer.Rows.Count >= 1)
            {
                try //Address
                {
                    TextBox16.Text = dtCustomer.Rows[0]["PostalCode"].ToString();
                    DropDownList5.ClearSelection();
                    DropDownList5.Items.FindByText(dtCustomer.Rows[0]["Province"].ToString()).Selected = true;
                    DropDownList5.SelectedIndex = DropDownList5.Items.IndexOf(DropDownList5.Items.FindByText(dtCustomer.Rows[0]["Province"].ToString()));
                    DropDownList6.ClearSelection();
                    DropDownList6.Items.FindByText(dtCustomer.Rows[0]["District"].ToString()).Selected = true;
                    DropDownList6.SelectedIndex = DropDownList6.Items.IndexOf(DropDownList6.Items.FindByText(dtCustomer.Rows[0]["District"].ToString()));
                    DropDownList7.ClearSelection();
                    DropDownList7.Items.FindByText(dtCustomer.Rows[0]["SubDistrict"].ToString()).Selected = true;
                    DropDownList7.SelectedIndex = DropDownList7.Items.IndexOf(DropDownList7.Items.FindByText(dtCustomer.Rows[0]["SubDistrict"].ToString()));

                    DropDownList8.ClearSelection();
                    DropDownList8.Items.FindByValue(dtCustomer.Rows[0]["Customer_Type_ID"].ToString()).Selected = true;
                    DropDownList8.SelectedIndex = DropDownList8.Items.IndexOf(DropDownList8.Items.FindByValue(dtCustomer.Rows[0]["Customer_Type_ID"].ToString()));

                }
                catch { }

                TextBox1.Text = dtCustomer.Rows[0]["MobilePhone"].ToString();
                TextBox2.Text = dtCustomer.Rows[0]["Name"].ToString();
                TextBox3.Text = dtCustomer.Rows[0]["NickName"].ToString();
                //TextBox7.Text = dtCustomer.Rows[0]["FullName"].ToString();
                TextBox8.Text = dtCustomer.Rows[0]["Address"].ToString();
                TextBox17.Text = dtCustomer.Rows[0]["Address1"].ToString();
                TextBox9.Text = dtCustomer.Rows[0]["IDNumber"].ToString();
                TextBox13.Text = dtCustomer.Rows[0]["Email"].ToString();

                if (Session["permission"].ToString() == "True")
                {
                    Button5.Visible = true;
                    Button5.Text = "เคยมาแล้ว "+ code.DatabaseQuery(conn, "SELECT count([Customer_MobilePhone]) as CountReserved FROM [Reservation] Where Customer_MobilePhone = '" + TextBox1.Text + "' AND Status = N'เช็คอินแล้ว'").Rows[0][0].ToString()+" ครั้ง" ;
                }
            }
            else
            {

            }

            if (TextBox1.Text.Length == 11)
            {
                TextBox1.Text = TextBox1.Text.Remove(TextBox1.Text.Length - 1);
            }

            
        }

        protected void Calendar1_DayRender(object sender, DayRenderEventArgs e)
        {
            DataTable dtReservation = code.DatabaseQuery(conn, "Select * From Reservation right join Reservation_Accommodation on Reservation.ID = Reservation_Accommodation.Reservation_ID Where '" + e.Day.Date.ToString("yyyy-MM-dd") + "' >= CheckinDate AND '" + e.Day.Date.ToString("yyyy-MM-dd") + "' < CheckoutDate");
            DataTable dtAccommodation = code.DatabaseQuery(conn, "Select * From Accommodation Where Status = 1");
            int maxAccommodation = dtAccommodation.Rows.Count;
            int totalAmount = 0;
            for (int j = 0; j < dtAccommodation.Rows.Count; j++)
            {
                for (int i = 0; i < dtReservation.Rows.Count; i++)
                {

                    if (dtReservation.Rows[i]["Accommodation_ID"].ToString() == dtAccommodation.Rows[j]["ID"].ToString() || dtAccommodation.Rows[j]["LimitWithPeople"].ToString() == "True")
                    {
                        dtAccommodation.Rows.RemoveAt(j);
                        dtAccommodation.AcceptChanges();
                        i = dtReservation.Rows.Count + 1;
                        j = -1;
                    }
                }
            }

            if (dtAccommodation.Rows.Count == 0)
            {
                //e.Cell.BackColor = System.Drawing.Color.Red;
                e.Cell.ForeColor = System.Drawing.Color.Red;
                e.Cell.Font.Bold = true;
            }
            if (dtAccommodation.Rows.Count == maxAccommodation)
            {
                //e.Cell.ForeColor = System.Drawing.Color.DarkGreen;
                //e.Cell.Font.Bold = true;
            }
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            TextBox12.Text = "";
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            if (TextBox1.Text.Length > 0)
            {
                if (FileUpload1.HasFile)
                {
                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox1.Text + ".jpg"))
                    {
                        File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + TextBox1.Text + ".jpg");
                    }

                    string FileSaveWithPath = "";
                    string filename = TextBox1.Text + ".jpg";
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

        protected void GridView2_RowEditing(object sender, GridViewEditEventArgs e)
        {
            GridView2.EditIndex = e.NewEditIndex;
            DataTable dtItems = (DataTable)Session["dtItems"];
            GridView2.DataSource = dtItems;
            GridView2.DataBind();
        }

        protected void GridView1_RowEditing(object sender, GridViewEditEventArgs e)
        {
            GridView1.EditIndex = e.NewEditIndex;
            DataTable dtAccom = (DataTable)Session["dtAccommodation"];
            GridView1.DataSource = dtAccom;
            GridView1.DataBind();
        }

        protected void GridView1_RowCancelingEdit(object sender, GridViewCancelEditEventArgs e)
        {
            GridView1.EditIndex = -1;
            DataTable dtAccom = (DataTable)Session["dtAccommodation"];
            GridView1.DataSource = dtAccom;
            GridView1.DataBind();
        }

        protected void GridView2_RowCancelingEdit(object sender, GridViewCancelEditEventArgs e)
        {
            GridView2.EditIndex = -1;
            DataTable dtItems = (DataTable)Session["dtItems"];
            GridView2.DataSource = dtItems;
            GridView2.DataBind();
        }

        protected void GridView1_RowUpdating(object sender, GridViewUpdateEventArgs e)
        {
            DataTable dtAccom = (DataTable)Session["dtAccommodation"];
            TextBox txtPrice = (TextBox)GridView1.Rows[e.RowIndex].Cells[4].Controls[0];
            GridView1.EditIndex = -1;
            dtAccom.Rows[Convert.ToInt32(e.RowIndex)]["Price"] = txtPrice.Text;
            GridView1.DataSource = dtAccom;
            GridView1.DataBind();
        }

        protected void GridView2_RowUpdating(object sender, GridViewUpdateEventArgs e)
        {
            DataTable dtItems = (DataTable)Session["dtItems"];
            TextBox txtPrice = (TextBox)GridView2.Rows[e.RowIndex].Cells[4].Controls[0];
            GridView2.EditIndex = -1;
            dtItems.Rows[Convert.ToInt32(e.RowIndex)]["Price"] = txtPrice.Text;
            GridView2.DataSource = dtItems;
            GridView2.DataBind();
        }

        protected void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (CheckBox1.Checked == true)
            {
                Button1.Enabled = true;
            }
            else
            {
                Button1.Enabled = false;
            }
        }

        protected void Button4_Click(object sender, EventArgs e)
        {

            if (GridView1.Rows.Count <= 0)
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเลือกวันที่ต้องการทำการจอง หรือวันที่เลือกไม่สามารถทำการเหมาลานได้');", true);
            }
            else
            {
                foreach (GridViewRow gr in GridView1.Rows)
                {
                    CheckBox chkC = gr.FindControl("chkSelect") as CheckBox;


                    //GridViewRow Row = ((GridViewRow)chkC.Parent.Parent);

                    chkC.Checked = true;


                }
            }

        }

        protected void CheckBox2_CheckedChanged(object sender, EventArgs e)
        {
            if(CheckBox2.Checked == true)
            {
                TextBox10.Visible = true;
            }
            else
            {
                TextBox10.Visible = false;
            }
        }

        protected void TextBox9_TextChanged(object sender, EventArgs e)
        {
            TextBox9.Text = TextBox9.Text.Replace(" ", "").Replace("-", "");
            if (TextBox9.Text.Length == 13 && TextBox8.Text.Length > 10)
            {
                CheckBox3.Checked = false;
                CheckBox3.DataBind();
            }
        }

        protected void TextBox10_TextChanged(object sender, EventArgs e)
        {
            string command = Request.QueryString["command"];
            if (command == "rentmore")
            {
                decimal total = Convert.ToDecimal(TextBox4.Text);
                decimal deposit = Convert.ToDecimal(TextBox5.Text);
                decimal paymore = Convert.ToDecimal(TextBox10.Text);
                if(total == deposit+paymore)
                { }
                else
                {
                    TextBox10.Text = "";
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('จำนวนเงินที่จ่ายเพิ่มไม่ตรงกับยอดที่ต้องจ่าย');", true);
                }
            }
        }

        protected void DropDownList2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(DropDownList2.SelectedItem.Text == "เงินสด" && CheckBox4.Checked == false)
            {
                FileUpload1.Visible = false;
            }
            else
            {
                FileUpload1.Visible = true;
            }

            if(DropDownList2.SelectedItem.Text == "เงินโอน บัญชี ธ.กสิกรไทย เลขที่ 064-1-70621-3")
            {
                CheckBox4.Checked = false;
                CheckBox4.DataBind();
            }
        }

        protected void CheckBox3_CheckedChanged(object sender, EventArgs e)
        {
            if(CheckBox3.Checked == false)
            {
                Panel1.Visible = true;
                CheckBox5.Visible = true;
            }
            else
            {
                Panel1.Visible = false;
                CheckBox5.Visible = false;
            }
        }

        protected void CheckBox4_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                string id = Request.QueryString["id"];
                string command = Request.QueryString["command"];

                DataTable dtReceipt = code.DatabaseQuery(conn, "SELECT  [ID] FROM [Account_Receipt] Where RESERVATION_ID = '" + id + "'");
                if ((command == "edit" || command == "checkin" || command == "rentmore") && dtReceipt.Rows.Count > 0)
                {
                    CheckBox4.Checked = false;
                    CheckBox4.DataBind();
                }
            }
            catch { }
            if (CheckBox4.Checked == true & CheckBox3.Checked == false)
            {
                CheckBox4.Checked = false;
                CheckBox4.DataBind();
            }
            if (DropDownList2.SelectedItem.Text == "เงินโอน บัญชี ธ.กสิกรไทย เลขที่ 064-1-70621-3")
            {
                CheckBox4.Checked = false;
                CheckBox4.DataBind();
            }
        }

        protected void Button5_Click(object sender, EventArgs e)
        {
            Response.Redirect("./CountReserved?telnum=" + TextBox1.Text);
        }

        protected void TextBox12_TextChanged(object sender, EventArgs e)
        {
            if (DateTime.Now > Convert.ToDateTime(TextBox12.Text).AddDays(1) && Session["permission"] == "No")
            {
                GridView1.Visible = false;
            }
            else
            {
                GridView1.Visible = true;
                try
                {
                    if (Session["permission"].ToString() == "True")
                    {

                    }
                    else
                    {
                        if (Convert.ToDateTime(TextBox12.Text) > DateTime.Now.AddMonths(3))
                        {
                            ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ระบบไม่อนุญาติให้จองเกิน 3 เดือน กรุณาติดต่อ Admin');", true);
                            TextBox12.Text = "";

                        }
                    }
                }
                catch
                {
                    if (Convert.ToDateTime(TextBox12.Text) > DateTime.Now.AddMonths(3))
                    {
                        ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ระบบไม่อนุญาติให้จองเกิน 3 เดือน กรุณาติดต่อ Admin');", true);
                        TextBox12.Text = "";
                    }
                }



                string command = Request.QueryString["command"];
                string id = Request.QueryString["id"];
                string check = Request.QueryString["check"];
                try
                {
                    Label1.Text = "Check-Out: " + Convert.ToDateTime(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy");
                }
                catch
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาติดต่อ Admin กรณีต้องการจองเกิน 3 เดือน');", true);
                }
                DataTable dtAccommodation = code.DatabaseQuery(conn, "Select * From Accommodation Where Status = 1 order by OrderID asc");
                for (int x = 0; x < Convert.ToInt32(DropDownList1.SelectedValue); x++)
                {
                    DataTable dtReservation = code.DatabaseQuery(conn, "Select * From Reservation right join Reservation_Accommodation on Reservation.ID = Reservation_Accommodation.Reservation_ID Where '" + Convert.ToDateTime(TextBox12.Text).AddDays((double)x).ToString("yyyy-MM-dd") + "' >= CheckinDate AND '" + Convert.ToDateTime(TextBox12.Text).AddDays((double)x).ToString("yyyy-MM-dd") + "' < CheckoutDate");

                    if (command == "edit" || command == "checkin" || command == "rentmore")
                    {
                        DataTable dtAccom = code.DatabaseQuery(conn, "SELECT * FROM [Reservation] right join Reservation_Accommodation on Reservation_Accommodation.Reservation_ID = Reservation.ID Where Reservation.ID = " + id + " AND Customer_MobilePhone = '" + check + "'");
                        for (int i = 0; i < dtReservation.Rows.Count; i++)
                        {
                            for (int j = 0; j < dtAccom.Rows.Count; j++)
                            {
                                try
                                {
                                    if (dtReservation.Rows[i]["Reservation_ID"].ToString() == dtAccom.Rows[j]["Reservation_ID"].ToString() && dtReservation.Rows[i]["Accommodation_ID"].ToString() == dtAccom.Rows[j]["Accommodation_ID"].ToString() && dtReservation.Rows[i]["Amount"].ToString() == dtAccom.Rows[j]["Amount"].ToString())
                                    {
                                        dtReservation.Rows[i].Delete();
                                    }
                                }
                                catch { }
                            }
                        }
                        dtReservation.AcceptChanges();
                    }


                    try
                    {
                        dtAccommodation.Columns.Add("StatusOnDate");
                    }
                    catch
                    {
                    }

                    List<int> rowDelete = new List<int>();
                    for (int j = 0; j < dtAccommodation.Rows.Count; j++)
                    {
                        int checkloop = 0;
                        int totalAmount = 0;
                        int ReserveAmount = 0;


                        for (int i = 0; i < dtReservation.Rows.Count; i++)
                        {
                            if (dtReservation.Rows[i]["Accommodation_ID"].ToString() == dtAccommodation.Rows[j]["ID"].ToString())
                            {
                                checkloop = 1;
                                if (dtAccommodation.Rows[j]["LimitWithPeople"].ToString() == "True" && (!DBNull.Value.Equals(dtReservation.Rows[i]["Amount"])) && dtReservation.Rows[i]["Accommodation_ID"].ToString() == dtAccommodation.Rows[j]["ID"].ToString())
                                {
                                    totalAmount += Convert.ToInt32(dtReservation.Rows[i]["Amount"].ToString());
                                }
                            }

                        }
                        if (checkloop == 1 && dtAccommodation.Rows[j]["LimitWithPeople"].ToString() == "False")
                        {
                            dtAccommodation.Rows[j]["StatusOnDate"] = "ไม่ว่าง (Not available)";
                            dtAccommodation.Rows[j].Delete();
                        }
                        else
                        {
                            dtAccommodation.Rows[j]["StatusOnDate"] = "ว่าง (Available)";

                            if ("True" == "True")
                            {
                                if (totalAmount >= Convert.ToInt32(dtAccommodation.Rows[j]["People"].ToString()))
                                {
                                    dtAccommodation.Rows[j]["StatusOnDate"] = "ไม่ว่าง (Not available)";
                                    try
                                    {
                                        dtAccommodation.Rows[j]["People"] = Convert.ToInt32(dtAccommodation.Rows[j]["People"].ToString()) - totalAmount;
                                    }
                                    catch
                                    {
                                        int checkk = 0;
                                        DataTable dtReserveAmount = code.DatabaseQuery(conn, "SELECT * FROM [Reservation_Accommodation] Where Reservation_ID = " + id);
                                        for (int l = 0; l < dtReserveAmount.Rows.Count; l++)
                                        {
                                            if (dtReserveAmount.Rows[l]["Accommodation_ID"].ToString() == dtAccommodation.Rows[j]["ID"].ToString())
                                            {
                                                dtAccommodation.Rows[j]["People"] = dtReserveAmount.Rows[l]["Amount"].ToString();
                                                checkk = 1;
                                            }
                                        }
                                        if (checkk == 0)
                                        { dtAccommodation.Rows[j].Delete(); }

                                    }


                                }
                                else
                                {
                                    dtAccommodation.Rows[j]["StatusOnDate"] = "ว่าง (Available)";
                                    dtAccommodation.Rows[j]["People"] = Convert.ToInt32(dtAccommodation.Rows[j]["People"].ToString()) - totalAmount;
                                }
                            }

                        }

                    }
                    dtAccommodation.AcceptChanges();
                }


                for (int i = 0; i < dtAccommodation.Rows.Count; i++)
                {
                    DataTable dtHolidayPrice = code.DatabaseQuery(conn, "Select * from Accommodation_HolidayPrice Where Accommodation_ID = " + dtAccommodation.Rows[i]["ID"].ToString() + " AND  DateNewPrice = '" + Convert.ToDateTime(TextBox12.Text).ToString("yyyy-MM-dd") + "'");
                    for (int j = 0; j < dtHolidayPrice.Rows.Count; j++)
                    {
                        if (dtAccommodation.Rows[i]["ID"].ToString() == dtHolidayPrice.Rows[j]["Accommodation_ID"].ToString())
                        {
                            dtAccommodation.Rows[i]["Price"] = dtHolidayPrice.Rows[j]["Price"].ToString();
                            dtAccommodation.AcceptChanges();
                        }
                    }
                }

                GridView1.DataSource = dtAccommodation;
                GridView1.DataBind();
                Session["dtAccommodation"] = dtAccommodation;

                DataTable dtItems = code.DatabaseQuery(conn, "Select * From Items Where Status = 1 order by OrderID asc");
                for (int x = 0; x < Convert.ToInt32(DropDownList1.SelectedValue); x++)
                {
                    DataTable dtReservation_Items = code.DatabaseQuery(conn, "Select * From Reservation right join Reservation_Items on Reservation.ID = Reservation_Items.Reservation_ID Where '" + Convert.ToDateTime(TextBox12.Text).AddDays((double)x).ToString("yyyy-MM-dd") + "' >= CheckinDate AND '" + Convert.ToDateTime(TextBox12.Text).AddDays((double)x).ToString("yyyy-MM-dd") + "' < CheckoutDate");

                    if (command == "edit" || command == "checkin")
                    {
                        DataTable dtItem = code.DatabaseQuery(conn, "SELECT * FROM [Reservation] right join Reservation_Items on Reservation_Items.Reservation_ID = Reservation.ID Where Reservation.ID = " + id + " AND Customer_MobilePhone = '" + check + "'");
                        for (int i = 0; i < dtReservation_Items.Rows.Count; i++)
                        {
                            for (int j = 0; j < dtItems.Rows.Count; j++)
                            {
                                try
                                {
                                    if (dtReservation_Items.Rows[i]["Reservation_ID"].ToString() == dtItems.Rows[j]["Reservation_ID"].ToString() && dtReservation_Items.Rows[i]["Accommodation_ID"].ToString() == dtItems.Rows[j]["Accommodation_ID"].ToString() && dtReservation_Items.Rows[i]["Amount"].ToString() == dtItems.Rows[j]["Amount"].ToString())
                                    {


                                    }
                                }
                                catch { }
                            }
                        }
                        dtReservation_Items.AcceptChanges();
                    }

                    try
                    {
                        dtItems.Columns.Add("StatusOnDate");
                    }
                    catch
                    {
                    }


                    for (int j = 0; j < dtItems.Rows.Count; j++)
                    {
                        int checkloop = 0;
                        int totalAmount = 0;
                        for (int i = 0; i < dtReservation_Items.Rows.Count; i++)
                        {
                            if (dtReservation_Items.Rows[i]["Items_ID"].ToString() == dtItems.Rows[j]["ID"].ToString())
                            {
                                checkloop = 1;
                                if (dtItems.Rows[j]["LimitWithAmount"].ToString() == "True" && (!DBNull.Value.Equals(dtReservation_Items.Rows[i]["Amount"])) && dtReservation_Items.Rows[i]["Items_ID"].ToString() == dtItems.Rows[j]["ID"].ToString())
                                {
                                    totalAmount += Convert.ToInt32(dtReservation_Items.Rows[i]["Amount"].ToString());
                                }
                                try
                                {
                                    if (command == "edit" || command == "checkin")
                                    {
                                        if (dtItems.Rows[j]["LimitWithAmount"].ToString() == "True" && dtReservation_Items.Rows[i]["Reservation_ID"].ToString() == id)
                                        {
                                            totalAmount -= Convert.ToInt32(dtReservation_Items.Rows[i]["Amount"].ToString());
                                        }
                                    }

                                }
                                catch
                                {

                                }
                            }

                        }
                        if (checkloop == 1 && dtItems.Rows[j]["LimitWithAmount"].ToString() == "False")
                        {
                            dtItems.Rows[j]["StatusOnDate"] = "ไม่ว่าง (Not available)";
                            dtItems.Rows[j]["Amount"] = 0;
                            //dtItems.Rows[j].Delete();
                        }
                        else
                        {
                            dtItems.Rows[j]["StatusOnDate"] = "ว่าง (Available)";
                            if (dtItems.Rows[j]["LimitWithAmount"].ToString() == "True")
                            {
                                if (totalAmount >= Convert.ToInt32(dtItems.Rows[j]["Amount"].ToString()))
                                {
                                    dtItems.Rows[j]["StatusOnDate"] = "ไม่ว่าง (Not available)";
                                    dtItems.Rows[j]["Amount"] = 0;
                                    //dtItems.Rows[j].Delete();
                                }
                                else
                                {
                                    dtItems.Rows[j]["StatusOnDate"] = "ว่าง (Available)";
                                    dtItems.Rows[j]["Amount"] = Convert.ToInt32(dtItems.Rows[j]["Amount"].ToString()) - totalAmount;
                                }
                            }
                        }

                    }
                    dtItems.AcceptChanges();
                }
                GridView2.DataSource = dtItems;
                GridView2.DataBind();
                Session["dtItems"] = dtItems;
            }
        }

        protected void CheckBox5_CheckedChanged(object sender, EventArgs e)
        {
            if(TextBox13.Text.Contains('@'))
            {

            }
            else
            {
                CheckBox5.Checked = false;
                CheckBox5.DataBind();
            }
        }

        protected void Button6_Click(object sender, EventArgs e)
        {
            TextBox16.Enabled = false;
            getAddress("SELECT DISTINCT [Province] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' order by Province ASC", "SELECT DISTINCT [District] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' order by SubDistrict ASC");

        }

        protected void Button7_Click(object sender, EventArgs e)
        {
            TextBox16.Enabled = true;
            TextBox16.Text = string.Empty;
            DropDownList5.Items.Clear();
            DropDownList6.Items.Clear();
            DropDownList7.Items.Clear();
            getAddress("SELECT DISTINCT [Province] FROM [Address] order by Province ASC", "SELECT DISTINCT [District] FROM [Address] order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] order by SubDistrict ASC");

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
            Button6_Click(null, null);
        }


        public string CheckAddressID(string ZipCode, string Province, string District, string SubDistrict)
        {
            string ID = "0";
            try
            {
                DataTable dt = code.DatabaseQuery(conn, "Select ID from Address Where PostalCode = '" + ZipCode + "' AND Province = N'" + Province + "' AND District = N'" + District + "' AND SubDistrict = N'" + SubDistrict + "'");
                ID = dt.Rows[0][0].ToString();
            }
            catch { }

            return ID;
        }



        protected void DropDownList6_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (TextBox16.Enabled == false)
            {
                getAddress("SELECT DISTINCT [Province] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' AND District = '" + DropDownList6.SelectedValue + "' order by Province ASC", "SELECT DISTINCT [District] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' AND District = '" + DropDownList6.SelectedValue + "' order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' AND District = '" + DropDownList6.SelectedValue + "' order by SubDistrict ASC");
            }
            else
            {
                getAddress("SELECT DISTINCT [Province] FROM [Address] Where District = N'" + DropDownList6.SelectedValue + "' order by Province ASC", "SELECT DISTINCT [District] FROM [Address] Where District = N'" + DropDownList6.SelectedValue + "' order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] Where District = N'" + DropDownList6.SelectedValue + "' order by SubDistrict ASC");
            }
        }

        protected void DropDownList5_SelectedIndexChanged(object sender, EventArgs e)
        {

            if (TextBox16.Enabled == false)
            {
                getAddress("SELECT DISTINCT [Province] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' AND Province = '" + DropDownList5.SelectedValue + "' order by Province ASC", "SELECT DISTINCT [District] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' AND Province = '" + DropDownList5.SelectedValue + "' order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] Where PostalCode = '" + TextBox16.Text + "' AND Province = '" + DropDownList5.SelectedValue + "' order by SubDistrict ASC");
            }
            else
            {
                getAddress("SELECT DISTINCT [Province] FROM [Address] Where Province = N'" + DropDownList5.SelectedValue + "' order by Province ASC", "SELECT DISTINCT [District] FROM [Address] Where Province = N'" + DropDownList5.SelectedValue + "' order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] Where Province = N'" + DropDownList5.SelectedValue + "' order by SubDistrict ASC");
            }
        }

        protected void DropDownList8_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(DropDownList8.SelectedValue == "1")
            {
                TextBox18.Visible = true;
            }
            else
            {
                TextBox18.Visible = false;
            }
        }
    }
}