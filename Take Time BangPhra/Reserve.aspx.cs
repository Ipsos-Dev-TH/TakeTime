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
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading.Tasks;
using Line.Messaging;
using Line.Messaging.Webhooks;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Net.Http;
using Take_Time_BangPhra.Account.Report;
using Take_Time_BangPhra.Class;
using Google.Apis.Gmail.v1.Data;

namespace Take_Time_BangPhra
{
    public partial class Reserve : System.Web.UI.Page
    {
        code code2 = new code();
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        // ✨ Helper Classes for refactored system
        private AddressHelper _addressHelper;
        private CustomerHelper _customerHelper;
        private DocumentHelper _documentHelper;

        // 🏨 Room Charge Feature
        private RoomChargeService _roomChargeService;
        private RoomChargeDataAccess _roomChargeDA;

        // 💰 Payment Feature
        private PaymentDataAccess _paymentDA;

        protected void Page_Load(object sender, EventArgs e)
        {
            // ✨ Initialize Helper Classes
            _addressHelper = new AddressHelper(conn);
            _customerHelper = new CustomerHelper(conn);
            _documentHelper = new DocumentHelper(conn);

            // 🏨 Initialize Room Charge Services
            _roomChargeService = new RoomChargeService(conn);
            _roomChargeDA = new RoomChargeDataAccess(conn);

            // 💰 Initialize Payment Services
            _paymentDA = new PaymentDataAccess(conn);

            this.MaintainScrollPositionOnPostBack = true;
            string date = Request.QueryString["date"];
            string accom = Request.QueryString["accom"];
            string couponcode = Request.QueryString["couponcode"];
            Page.MaintainScrollPositionOnPostBack = true;
            
            if (!IsPostBack)
            {
                DataTable dtPaidHow = code.DatabaseQuery(SqlDataSource1.ConnectionString, SqlDataSource1.SelectCommand);
                for (int p = 0; p < dtPaidHow.Rows.Count; p++)
                {
                    DropDownList2.Items.Add(new ListItem(dtPaidHow.Rows[p]["Paid_How"].ToString(), dtPaidHow.Rows[p]["ID"].ToString()));
                }
                DropDownList2.DataBind();

                Session["UseCoupon"] = "false";
                try
                {
                    if (couponcode.Length > 0)
                    {
                        TextBox19.Text = couponcode;
                        Button8_Click(null, null);
                    }
                }
                catch { }
                DataTable dtCustomerType = code.DatabaseQuery(conn, "Select [Customer_Type],ID From Customer_Type");


                for (int q = 0; q < dtCustomerType.Rows.Count; q++)
                {
                    DropDownList8.Items.Add(new ListItem(dtCustomerType.Rows[q][0].ToString(), dtCustomerType.Rows[q][1].ToString()));
                }

                DropDownList8.DataBind();

                DropDownList8.ClearSelection();
                DropDownList8.Items.FindByValue("2").Selected = true;
                DropDownList8.SelectedIndex = DropDownList8.Items.IndexOf(DropDownList8.Items.FindByValue("2"));

                // ✅ FIXED: No filters - get all addresses
                getAddress();

                DropDownList1.SelectedIndex = 0;

                try
                {
                    TextBox12.Text = date;
                    TextBox12_TextChanged(null, null);
                    //code2.ParseDate(TextBox12.Text) = DateTime.Parse(date);
                    //Calendar1.DataBind();
                    //Calendar1_SelectionChanged(null, null);
                    DataTable dtAccom = (DataTable)Session["dtAccommodation"];

                    // ✅ Safety check: Only process if dtAccom is not null
                    if (dtAccom != null && dtAccom.Rows.Count > 0)
                    {
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
                }
                catch { }
                try
                {
                    if (Session["User"].ToString() == "Owner")
                    {
                        TextBox11.Visible = true;
                    }
                    else
                    {
                        TextBox11.Visible = false;
                    }
                    if(Session["User"].ToString() == "Owner" || Session["User"].ToString() == "Admin")
                    {
                        CheckBox6.Visible = true; 
                    }
                }
                catch { }
                Image1.ImageUrl = "./Images/บัญชี.png";
                Image1.DataBind();
            }

            try
            {
                if (Session["permission"].ToString() == "True" && CheckBox6.Checked == true)
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
                    GridView1.Columns[5].Visible = true;
                    GridView2.Columns[5].Visible = true;
                }
                else if (Session["permission"].ToString() == "True" && CheckBox6.Checked == false)
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
                    GridView1.Columns[5].Visible = false;
                    GridView2.Columns[5].Visible = false;
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
            try
            {
                if (check[0] == ' ')
                {
                    check = "+" + check.Replace(" ", "");
                }
            }
            catch { }
            Session["OldPrice"] = TextBox4.Text;

            // 🏨 Load product charges for modes that support it
            // Must be called on EVERY postback to prevent Event Validation errors with dynamic button visibility
            bool shouldLoadProductCharges = false;

            if(command == "checkin")
            {
                GridView1.Enabled = false;
                GridView2.Enabled = false;
                TextBox5.Enabled = false;
                CheckBox2.Visible = true;
                CheckBox2.Text = "ชำระเงิน";
                Button1.Text = "ยืนยันการเช็คอิน";
                CheckBox1.Visible = false;
                Button1.Enabled = true;

                // 🆕 Show payment history
                LoadPaymentHistory();

                shouldLoadProductCharges = true;
            }
            else if(command == "edit")
            {
                TextBox5.Enabled = false;
                CheckBox2.Visible = true;
                Button1.Text = "ยืนยันการแก้ไข";
                CheckBox1.Visible = false;
                Button1.Enabled = true;
                Label7.Visible = false;

                // 🆕 Show payment history
                LoadPaymentHistory();

                shouldLoadProductCharges = true;
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

                // 🆕 Show payment history
                LoadPaymentHistory();

                shouldLoadProductCharges = true;
            }
            else if(command == "reserve")
            {
                // Reserve mode: Don't show payment history (new reservation)
                divPaymentHistory.Visible = false;
            }

            // 🔧 IMPORTANT: Rebind product charges on page load, BUT NOT during postback from delete button
            // Check if this is a postback from GridView command (delete button click)
            // ASP.NET Button doesn't use __EVENTTARGET, so check Request.Form keys instead
            bool isGridViewCommand = false;
            if (IsPostBack && Request.Form != null)
            {
                foreach (string key in Request.Form.AllKeys)
                {
                    if (key != null && key.Contains("gvProductCharges") && key.Contains("btnDeleteCharge"))
                    {
                        isGridViewCommand = true;
                        break;
                    }
                }
            }

            if (shouldLoadProductCharges && !string.IsNullOrEmpty(Request.QueryString["id"]) && !isGridViewCommand)
            {
                LoadProductCharges();
            }

            DataTable dtAccommodation = (DataTable)Session["dtAccommodation"];
            DataTable dtItems = (DataTable)Session["dtItems"];

            // ✅ Safety check: If Session is null, try to reload from GridView or DB
            if (dtAccommodation == null && GridView1.DataSource != null && GridView1.DataSource is DataTable)
            {
                dtAccommodation = (DataTable)GridView1.DataSource;
                Session["dtAccommodation"] = dtAccommodation;
            }

            if (dtItems == null && GridView2.DataSource != null && GridView2.DataSource is DataTable)
            {
                dtItems = (DataTable)GridView2.DataSource;
                Session["dtItems"] = dtItems;
            }

            int i = 0;
            double totalPrice = 0;
            double PriceAccom = 0;
            int PriceItems = 0;
            int DepositAmount = 0;

            // 🔧 FIX: Call Calendar1_SelectionChanged ONCE before looping, not for each checked row
            // This prevents the GridView from being rebound multiple times, which resets price calculations
            // 🔧 FIX: Don't call Calendar1_SelectionChanged if PostBack is from txtPeopleStay change
            // because it will rebind GridView and reset the txtPeopleStay value that user just changed
            bool isPostBackFromPeopleStayChange = false;
            if (IsPostBack && Request.Form["__EVENTTARGET"] != null)
            {
                string eventTarget = Request.Form["__EVENTTARGET"];
                if (eventTarget.Contains("txtPeopleStay"))
                {
                    isPostBackFromPeopleStayChange = true;
                }
            }

            bool hasCheckedAccommodation = false;
            foreach (GridViewRow row in GridView1.Rows)
            {
                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                if (chk != null && chk.Checked)
                {
                    hasCheckedAccommodation = true;
                    break;
                }
            }

            // Only call Calendar1_SelectionChanged if NOT from txtPeopleStay change
            if (hasCheckedAccommodation && !isPostBackFromPeopleStayChange)
            {
                Calendar1_SelectionChanged(null, null);
                // Reload dtAccommodation after Calendar1_SelectionChanged updates it
                dtAccommodation = (DataTable)Session["dtAccommodation"];
                dtItems = (DataTable)Session["dtItems"];

                // ✅ Safety check after reload
                if (dtAccommodation == null && GridView1.DataSource != null && GridView1.DataSource is DataTable)
                {
                    dtAccommodation = (DataTable)GridView1.DataSource;
                    Session["dtAccommodation"] = dtAccommodation;
                }
            }

            // Reset loop counter
            i = 0;

            foreach (GridViewRow row in GridView1.Rows)
            {

                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                if (chk != null && chk.Checked)
                {
                    row.BackColor = System.Drawing.ColorTranslator.FromHtml("#8D9F7F");
                    TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);
                    txtPeopleStay.Enabled = true;

                    // ✅ Safety check: Ensure dtAccommodation is not null and index is valid
                    if (dtAccommodation != null && i < dtAccommodation.Rows.Count &&
                        dtAccommodation.Rows[i]["LimitWithPeople"].ToString() == "True")
                    {
                        // 🔧 FIX: Set default guest count to 1 if 0 (when checkbox first checked)
                        // Don't use max occupancy as default - let user choose the actual number
                        if (Convert.ToInt32(txtPeopleStay.Text) == 0)
                        {
                            txtPeopleStay.Text = "1";
                        }

                        // 🔧 Validate guest count doesn't exceed max occupancy
                        try
                        {
                            if (Session["permission"].ToString() != "True")
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

                        DepositAmount += 50* Convert.ToInt32(txtPeopleStay.Text);

                        // 🔧 Calculate price for LimitWithPeople (price per person per night)
                        // Formula: basePrice × numberOfGuests × numberOfNights
                        // Example: 100฿/person × 5 people × 2 nights = 1,000฿

                        int numberOfGuests = Convert.ToInt32(txtPeopleStay.Text);
                        int numberOfNights = Convert.ToInt32(DropDownList1.SelectedValue);

                        // ✅ FIX: On PostBack, use price from GridView (preserves manual edits for LimitWithPeople)
                        // Only query DB price on initial load or when CheckBox6 unchecked AND not PostBack
                        bool useGridViewPrice = IsPostBack || CheckBox6.Checked == true;
                        int basePricePerPerson = 0;

                        if (useGridViewPrice)
                        {
                            // ✅ Use current price from GridView (preserves manual edits)
                            if (int.TryParse(row.Cells[4].Text, out basePricePerPerson))
                            {
                                // basePricePerPerson ได้รับค่าจาก GridView แล้ว
                            }
                            else if (dtAccommodation != null && i < dtAccommodation.Rows.Count)
                            {
                                // Fallback to query DB if invalid
                                basePricePerPerson = Convert.ToInt32(AccomPrice(
                                    dtAccommodation.Rows[i]["ID"].ToString(),
                                    code2.ParseDate(TextBox12.Text).Value));
                            }
                        }
                        else if (dtAccommodation != null && i < dtAccommodation.Rows.Count)
                        {
                            // Get base price per person per night from DB
                            basePricePerPerson = Convert.ToInt32(AccomPrice(
                                dtAccommodation.Rows[i]["ID"].ToString(),
                                code2.ParseDate(TextBox12.Text).Value));

                            // Display base price per person in GridView
                            GridView1.Rows[i].Cells[4].Text = basePricePerPerson.ToString("0");
                        }

                        // Calculate total: price × people × nights
                        int totalPriceAllNights = basePricePerPerson * numberOfGuests * numberOfNights;
                        PriceAccom += totalPriceAllNights;

                        // 🔧 IMPORTANT: Display base price per person in GridView (NOT total price)
                        // This will be saved to database and used for recalculation later
                        // Database will store: Amount=5 (people), Price=100 (per person per night)
                        // When querying back: 100 × 5 × 2 = 1,000฿ ✅
                        if (!useGridViewPrice)
                        {
                            GridView1.Rows[i].Cells[4].Text = basePricePerPerson.ToString("0");
                        }
                    }
                    else
                    {
                        // ✅ Safety check before accessing dtAccommodation
                        if (dtAccommodation != null && i < dtAccommodation.Rows.Count)
                        {
                            if (dtAccommodation.Rows[i]["ID"].ToString() == "21" || dtAccommodation.Rows[i]["ID"].ToString() == "22" || dtAccommodation.Rows[i]["ID"].ToString() == "23" || dtAccommodation.Rows[i]["ID"].ToString() == "18")
                            {
                                DepositAmount += 1000;
                            }
                            else
                            {
                                DepositAmount += 500;
                            }
                        }
                            
                        string ReserveDate = "";
                        try
                        {
                            ReserveDate = TextBox12.Text;
                        }
                        catch { }
                        if ((ReserveDate == null || ReserveDate == "") && command == "edit")
                        {
                            // 🔒 SECURE: Using parameterized query
                            var parameters = new Dictionary<string, object>
                            {
                                { "@id", id },
                                { "@phone", check }
                            };
                            DataTable dtReservation = code2.DatabaseQuerySafe(conn,
                                "SELECT * FROM [Reservation] WHERE ID = @id AND Customer_MobilePhone = @phone",
                                parameters);

                            if (dtReservation.Rows.Count > 0)
                            {
                                ReserveDate = dtReservation.Rows[0]["CheckinDate"].ToString();
                            }
                        }
                        // 🔧 FIX: On PostBack, use price from GridView (may be edited by admin)
                        // Only query DB price on initial load or when CheckBox6 unchecked AND not PostBack
                        bool useGridViewPrice = IsPostBack || CheckBox6.Checked == true;

                        if (useGridViewPrice)
                        {
                            // ✅ Use current price from GridView (preserves manual edits)
                            double gridPrice = 0;
                            if (double.TryParse(row.Cells[4].Text, out gridPrice))
                            {
                                PriceAccom += gridPrice * Convert.ToInt32(DropDownList1.SelectedValue);
                            }
                            else
                            {
                                // Fallback to 0 if invalid
                                PriceAccom += 0;
                            }
                        }
                        else if (Convert.ToInt32(DropDownList1.SelectedValue) > 1 && CheckBox6.Checked == false)
                        {
                            // Initial load: Multi-day reservation, query DB for holiday prices
                            // ✅ Safety check
                            if (dtAccommodation != null && i < dtAccommodation.Rows.Count)
                            {
                                double PriceThisAccom = 0;
                                for (int k = 0; k < Convert.ToInt32(DropDownList1.SelectedValue); k++)
                                {
                                    PriceThisAccom += Convert.ToInt32(AccomPrice(dtAccommodation.Rows[i]["ID"].ToString(), code2.ParseDate(ReserveDate).Value.AddDays(k)));
                                }
                                PriceAccom += Convert.ToInt32(PriceThisAccom);
                                GridView1.Rows[i].Cells[4].Text = (PriceThisAccom / Convert.ToInt32(DropDownList1.SelectedValue)).ToString();
                            }
                        }
                        else if(CheckBox6.Checked == false)
                        {
                            // Initial load: Single-day reservation, query DB for price
                            // ✅ Safety check
                            if (dtAccommodation != null && i < dtAccommodation.Rows.Count)
                            {
                                double PriceThisAccom = Convert.ToInt32(AccomPrice(dtAccommodation.Rows[i]["ID"].ToString(), code2.ParseDate(ReserveDate).Value));
                                PriceAccom += PriceThisAccom;
                                GridView1.Rows[i].Cells[4].Text = PriceThisAccom.ToString();
                            }
                        }
                        else
                        {
                            // Permission mode: Use GridView price
                            PriceAccom += Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue);
                        }
                        if (Convert.ToInt32(txtPeopleStay.Text) == 0 && dtAccommodation != null && i < dtAccommodation.Rows.Count)
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

                    row.BackColor = System.Drawing.ColorTranslator.FromHtml("#8D9F7F");
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

            // 🏨 เพิ่มสินค้าชาร์จเข้าห้อง (Product Charges - ทั้งหมด ไม่ใช่เฉพาะที่ค้างชำระ)
            double ProductCharges = 0;
            try
            {
                if ((command == "edit" || command == "checkin" || command == "rentmore") && !string.IsNullOrEmpty(id))
                {
                    int reservationId = Convert.ToInt32(id);

                    // 🔧 FIX: Query directly from database (same as ReserveTable and Reservation_Confirmed)
                    // This ensures we always get the latest data without caching issues
                    var productChargesParams = new Dictionary<string, object>
                    {
                        { "@reservationId", reservationId }
                    };
                    DataTable dtProductCharges = code2.DatabaseQuerySafe(conn,
                        @"SELECT ISNULL(SUM(TotalAmount), 0) as TotalCharges
                          FROM Reservation_Product_Charges
                          WHERE Reservation_ID = @reservationId
                          AND Status <> 'CANCELLED'",
                        productChargesParams);

                    if (dtProductCharges.Rows.Count > 0 && dtProductCharges.Rows[0]["TotalCharges"] != DBNull.Value)
                    {
                        ProductCharges = Convert.ToDouble(dtProductCharges.Rows[0]["TotalCharges"]);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error for debugging
                code2.Logs(conn, "Reserve - Get Product Charges Error",
                    $"Reservation ID: {id}, Error: {ex.Message}",
                    Session["User"]?.ToString());
                ProductCharges = 0;
            }

            totalPrice = PriceAccom + PriceItems + ProductCharges;
            Session["totalPrice"] = totalPrice;
            Session["ProductCharges"] = ProductCharges;

            // ⚠️ DON'T set TextBox4.Text here - it will be set later in edit/checkin/rentmore section
            // TextBox4.Text = Session["totalPrice"].ToString();

            if ((command == "edit" || command == "checkin" || command == "rentmore") && Session["permission"].ToString() == "True")
            {
                // 🔒 SECURE: Load data every time (even on PostBack) to ensure accuracy
                Button1.Enabled = true;
                var reservationDA = new ReservationDataAccess(conn);

                DataTable dtReservation = reservationDA.GetReservationByIdAndPhone(Convert.ToInt32(id), check);
                DataTable dtCustomer = reservationDA.GetReservationWithCustomerDetails(Convert.ToInt32(id), check);

                // 💰 Load payment amounts (needed for PostBack validation)
                // ✅ FIX: Reservation.TotalPrice now contains ONLY base price (accommodations + items)
                // ProductCharges are stored separately in Reservation_Product_Charges table
                // Grand total = Reservation.TotalPrice + ProductCharges (sum from separate table)
                decimal dbTotalPrice = Convert.ToDecimal(dtCustomer.Rows[0]["TotalPrice"]);
                decimal calculatedTotalPrice = Convert.ToDecimal(Session["totalPrice"]);

                decimal finalTotalPrice;
                if (!IsPostBack)
                {
                    // 🔧 Initial load: Use totalPrice from Session (already includes ProductCharges)
                    // Session["totalPrice"] is calculated in Page_Load from PriceAccom + PriceItems + ProductCharges
                    // This is correct because ProductCharges are NOT included in dbTotalPrice anymore
                    finalTotalPrice = calculatedTotalPrice;

                    Session["PriceModified"] = "false";
                }
                else
                {
                    // 🔧 PostBack: Use calculatedTotalPrice from GridView
                    // Because user may have modified prices/rooms
                    finalTotalPrice = calculatedTotalPrice;

                    // Mark as modified if price differs from DB base total
                    decimal calculatedBasePrice = calculatedTotalPrice - Convert.ToDecimal(Session["ProductCharges"] ?? "0");
                    if (calculatedBasePrice != dbTotalPrice)
                    {
                        Session["PriceModified"] = "true";
                    }
                }

                // Set TextBox and Session
                TextBox4.Text = finalTotalPrice.ToString();
                Session["OldPrice"] = finalTotalPrice.ToString();

                // 💰 Get actual total paid amount from Payment_History instead of Deposit column
                decimal totalPaid = 0;
                try
                {
                    totalPaid = _paymentDA.GetTotalPaidAmount(Convert.ToInt32(id));
                }
                catch
                {
                    // Fallback to Deposit if Payment_History not available
                    totalPaid = Convert.ToDecimal(dtCustomer.Rows[0]["Deposit"] ?? "0");
                }
                TextBox5.Text = totalPaid.ToString();

                // Update remaining balance label
                Label7.Visible = true;
                decimal remainingAmount = Convert.ToDecimal(TextBox4.Text) - Convert.ToDecimal(TextBox5.Text);
                Label7.Text = "ยอดเงินส่วนที่เหลือที่จะต้องชำระตอนเช็คอิน = " + remainingAmount.ToString("N2") + " บาท";

                if (command == "checkin")
                {
                    string paidType = dtCustomer.Rows[0]["Paid_Type"]?.ToString() ?? "เงินสด";
                    if (string.IsNullOrWhiteSpace(paidType) || paidType.Length <= 5) paidType = "เงินสด";
                    Label7.Text += " ยอดเดิมลูกค้าชำระโดยวิธี " + paidType;
                }

                if (!IsPostBack)
                {
                    // Load data only on first load (not on PostBack)
                    DataTable dtAccom = reservationDA.GetReservationWithAccommodations(Convert.ToInt32(id), check);
                    DataTable dtItemsold = reservationDA.GetReservationWithItems(Convert.ToInt32(id), check);
                    DataTable dtReceipt = reservationDA.GetReceiptsByReservation(Convert.ToInt32(id));
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

                            try
                            {
                                DropDownList2.SelectedIndex = DropDownList2.Items.IndexOf(DropDownList2.Items.FindByText(dtReceipt.Rows[0]["Paid_Type"].ToString()));
                                DropDownList2.DataBind();
                            }
                            catch { }
                        }
                        else
                        {
                            CheckBox3.Checked = true;
                            CheckBox3.DataBind();
                            Panel1.Visible = false;

                            try
                            {
                                DropDownList2.SelectedIndex = DropDownList2.Items.IndexOf(DropDownList2.Items.FindByText(dtReceipt.Rows[0]["Paid_Type"].ToString()));
                                DropDownList2.DataBind();
                            }
                            catch { }

                        }
                        try
                        {
                            if (dtReceipt.Rows.Count == 0 || dtReservation.Rows[0]["NoCreateReceipt"].ToString().ToLower() == "true")
                            {
                                CheckBox4.Checked = true;
                                CheckBox4.DataBind();

                                DropDownList2.SelectedIndex = DropDownList2.Items.IndexOf(DropDownList2.Items.FindByText("เงินสด"));
                                DropDownList2.DataBind();
                            }
                            else
                            {
                                CheckBox4.Checked = false;
                                CheckBox4.DataBind();
                            }
                        }
                        catch { }
                    
                    
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

                    // Load customer details for form fields (address, email, etc.)
                    try //Address
                    {
                        try
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
                        }
                        catch { }
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

                    // Load slip image
                    // 🔧 FIX: For CheckIn/Edit/RentMore modes - don't show image (shown in GridView instead)
                    try
                    {
                        // Use command variable from line 192 (already declared)
                        if (command == "checkin" || command == "edit" || command == "rentmore")
                        {
                            // Hide image - payment history GridView shows slips instead
                            Image1.Visible = false;
                        }
                        else
                        {
                            // For other modes (reserve, etc.) - show image as before
                            string slipQuery = @"
                                SELECT TOP 1 SlipFileURL
                                FROM Payment_Slips
                                WHERE Reservation_ID = @ReservationId
                                AND IsActive = 1
                                ORDER BY UploadedDate DESC";

                            var slipParams = new Dictionary<string, object>
                            {
                                { "@ReservationId", Convert.ToInt32(id) }
                            };

                            DataTable dtSlip = code2.DatabaseQuerySafe(conn, slipQuery, slipParams);
                            if (dtSlip.Rows.Count > 0)
                            {
                                // Use slip URL from database (new format)
                                Image1.ImageUrl = "./" + dtSlip.Rows[0]["SlipFileURL"].ToString();
                                Image1.Visible = true;
                            }
                            else
                            {
                                // Fall back to old pattern for legacy slips
                                string legacySlipPath = "./Upload/Slip/" + id + "_" + check + ".jpg";
                                if (File.Exists(Server.MapPath(legacySlipPath)))
                                {
                                    Image1.ImageUrl = legacySlipPath;
                                    Image1.Visible = true;
                                }
                                else
                                {
                                    // No slip found - show placeholder
                                    Image1.ImageUrl = "./Images/บัญชี.png";
                                    Image1.Visible = true;
                                }
                            }
                            Image1.DataBind();
                        }
                    }
                    catch
                    {
                        // Error loading slip - show placeholder
                        Image1.ImageUrl = "./Images/บัญชี.png";
                        Image1.Visible = true;
                        Image1.DataBind();
                    }
                    TextBox6.Text = dtCustomer.Rows[0]["Remark"].ToString();
                }
                // Note: TextBox4, TextBox5, Label7 are now loaded outside if (!IsPostBack) above

            }
            else
            {
                // 🔧 For reserve mode (new reservation), use calculated totalPrice
                TextBox4.Text = Session["totalPrice"]?.ToString() ?? "0";
            }

        }

        protected void Calendar1_SelectionChanged(object sender, EventArgs e)
        {
            bool usevoucher = false;
            try
            {

                Convert.ToBoolean(Session["UseVoucher"].ToString());
            }
            catch
            {

            }
            try
            {
                if (usevoucher == true && DropDownList1.SelectedIndex > 0)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('Voucher สามารถใช้ได้1ห้อง ต่อ 1 วันเท่านั้น');", true);
                    DropDownList1.SelectedIndex = 0;
                    DropDownList1.DataBind();
                }
            }
            catch { }
            int chkvaluecheck = 0;
            List<string> listcheck = new List<string>();
            int countcheck = 0;
            foreach (GridViewRow gr in GridView1.Rows)
            {
                CheckBox chkC = gr.FindControl("chkSelect") as CheckBox;

                //GridViewRow Row = ((GridViewRow)chkC.Parent.Parent);

                bool chkvalue = chkC.Checked;

                if(chkvalue  == true)
                {
                    countcheck++;
                    chkvaluecheck = 1;
                    listcheck.Add(gr.Cells[1].Text);
                    if (usevoucher == true)
                    {
                        if (countcheck >= 2)
                        {
                            chkC.Checked = false;
                            ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('Voucher สามารถใช้ได้1ห้อง ต่อ 1 วันเท่านั้น');", true);
                        }
                    }
                }
            }

            if (TextBox12.Text.Length > 0 && chkvaluecheck == 1)
            {
                string id = Request.QueryString["id"];
                try { id = Convert.ToInt32(id).ToString(); }
                catch { id = "0"; }
                int checkdup = 0;
                DataTable dtAccom = (DataTable)Session["dtAccommodation"];

                // ✅ FIX: Reload dtAccommodation if Session is null (can happen after postback/timeout)
                if (dtAccom == null)
                {
                    // Try to get from GridView1.DataSource first
                    if (GridView1.DataSource != null && GridView1.DataSource is DataTable)
                    {
                        dtAccom = (DataTable)GridView1.DataSource;
                    }
                    else
                    {
                        // Fallback: Query from database
                        dtAccom = code.DatabaseQuery(conn, "SELECT * FROM Accommodation WHERE Status = 1 ORDER BY OrderID ASC");
                    }

                    // Restore to Session
                    if (dtAccom != null)
                    {
                        Session["dtAccommodation"] = dtAccom;
                    }
                }

                // ✅ Safety check: If still null, show error and exit
                if (dtAccom == null || dtAccom.Rows.Count == 0)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert",
                        "alert('ไม่พบข้อมูลห้องพัก กรุณาโหลดหน้าใหม่อีกครั้ง');", true);
                    return;
                }

                // ✅ Use AccommodationAvailabilityService for centralized logic
                var availabilityService = new AccommodationAvailabilityService(conn);

                for (int i = 0;i<Convert.ToInt32(DropDownList1.SelectedValue);i++)
                {
                    for(int j = 0;j<listcheck.Count;j++)
                    {
                        // Find accommodation info
                        string accomName = listcheck[j];
                        int accommodationId = 0;
                        int requestedPeople = 1;

                        // Get accommodation ID and requested people count
                        foreach (GridViewRow row in GridView1.Rows)
                        {
                            if (row.Cells[1].Text == accomName)
                            {
                                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                if (chk != null && chk.Checked)
                                {
                                    // ✅ Safety check before accessing dtAccom
                                    if (row.RowIndex < dtAccom.Rows.Count)
                                    {
                                        accommodationId = Convert.ToInt32(dtAccom.Rows[row.RowIndex]["ID"]);
                                    }
                                    else
                                    {
                                        // Fallback: Search by AccomName
                                        foreach (DataRow dr in dtAccom.Rows)
                                        {
                                            if (dr["AccomName"].ToString() == accomName)
                                            {
                                                accommodationId = Convert.ToInt32(dr["ID"]);
                                                break;
                                            }
                                        }
                                    }

                                    TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);
                                    if (txtPeopleStay != null && !string.IsNullOrEmpty(txtPeopleStay.Text))
                                    {
                                        int.TryParse(txtPeopleStay.Text, out requestedPeople);
                                    }
                                }
                                break;
                            }
                        }

                        if (accommodationId > 0)
                        {
                            // ✅ Check availability for this date
                            DateTime checkinDate = code2.ParseDate(TextBox12.Text).Value.AddDays(i);
                            DateTime checkoutDate = checkinDate.AddDays(1);

                            var availabilityResult = availabilityService.CheckAvailability(
                                accommodationId,
                                accomName,
                                checkinDate,
                                checkoutDate,
                                requestedPeople,
                                Convert.ToInt32(id)
                            );

                            if (!availabilityResult.IsAvailable)
                            {
                                checkdup = 1;
                                break; // Exit inner loop
                            }
                        }
                    }

                    if (checkdup == 1)
                    {
                        break; // Exit outer loop
                    }
                }
                if(checkdup == 1)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ห้องพักที่คุณเลือกไม่ว่างในการจองหลายวัน');", true);
                    DropDownList1.SelectedIndex = 0;
                    DropDownList1.DataBind();
                }
                Label1.Text = "Check-Out: " + code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy");
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

        protected async void Button1_Click(object sender, EventArgs e)
        {
            // ✅ Removed Session["Submit"] check - was preventing re-submission after errors
            // Double-submit prevention should be handled by client-side button disable
            // or by checking if operation already completed (check Status in DB)

            // Tax invoice date should be payment received date (when button is pressed)
            // NOT the check-in date
            DateTime docCreatedDate = DateTime.Now;

                // REMOVED: Code that overwrote docCreatedDate with check-in date
                // Tax invoices must always use payment date (DateTime.Now), not check-in date
                // Old logic incorrectly used check-in date from TextBox12 or TextBox11

                /*
                try
                {
                    if (code2.ParseDate(TextBox12.Text) < docCreatedDate)
                    {
                        docCreatedDate = code2.ParseDate(TextBox12.Text).Value;
                    }
                }
                catch
                {

                }

                try
                {
                    if (TextBox11.Visible == true && TextBox11.Text.Length > 0)
                    {
                        docCreatedDate = code2.ParseDate(TextBox11.Text).Value;
                    }
                }
                catch { }
                */


                TextBox4.Text = Session["OldPrice"].ToString();
                string command = Request.QueryString["command"];
                string id = Request.QueryString["id"];
                string check = Request.QueryString["check"];
                try
                {
                    if (check[0] == ' ')
                    {
                        check = "+" + check.Replace(" ", "");
                    }
                }
                catch { }
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
                    if (Session["permission"].ToString() == "True" && (command == "reserve" || command == "checkin" || (command == "edit" && CheckBox2.Checked == true) || (command == "rentmore" && CheckBox2.Checked == true)))
                    {
                        if (DropDownList2.SelectedIndex == 0)
                        {
                            checkpaymentselect = 1;
                            ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('⚠️ กรุณาเลือกวิธีชำระเงิน (ธนาคาร/เงินสด)\\nเพื่อบันทึกการชำระเงิน');", true);
                            code2.Logs(conn, "Reserve Validation Error",
                                $"Command: {command}, Reservation ID: {id}, Error: Payment method not selected",
                                Session["User"]?.ToString());
                        }
                        else
                        {

                        }
                    }
                }
                catch { }

                int checkcustype = 0;
                if (DropDownList8.SelectedValue == "1")
                {
                    if (TextBox18.Text.Length == 5)
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

                // 🔧 FIX: Allow edit mode with or without additional deposit
                bool isEditMode = (command == "edit");
                bool isAdditionalDepositOnly = (command == "edit" && CheckBox2.Checked && checkgrid1 == 0);
                bool hasRoomSelection = checkgrid1 > 0;
                bool isEditWithoutPayment = (command == "edit" && !CheckBox2.Checked);

                if (TextBox1.Text.Length > 0 && checkcustype == 1)
                {
                    // ✅ Allow: 1) Room changes, OR 2) Additional deposit only, OR 3) Edit without payment
                    if (hasRoomSelection || isAdditionalDepositOnly || isEditWithoutPayment)
                    {
                        if (deposit >= 0 || TextBox1.Text == "02" || CheckBox2.Checked == true || isEditWithoutPayment)
                        {
                            // 🔧 Check slip only if additional deposit is checked
                            bool needSlipValidation = (command == "edit" && CheckBox2.Checked) ||
                                                     (command == "reserve") ||
                                                     (command == "checkin" && CheckBox2.Checked) ||
                                                     (command == "rentmore" && CheckBox2.Checked);

                            bool hasValidPaymentProof = FileUpload1.HasFile ||
                                                       Image1.ImageUrl != "./Images/บัญชี.png" ||
                                                       TextBox1.Text == "02" ||
                                                       DropDownList2.SelectedItem.Text == "เงินสด";

                            // ✅ Skip slip validation for edit without additional deposit
                            if ((!needSlipValidation || hasValidPaymentProof) && checkpaymentselect == 0)
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
                                    //    code.DatabaseInsert(conn, "UPDATE [dbo].[Reservation] SET [Customer_MobilePhone] = '" + TextBox1.Text + "' ,[CheckinDate] = '" + code2.ParseDate(TextBox12.Text).ToString("yyyy-MM-dd") + "' ,[CheckoutDate] = '" + code2.ParseDate(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("yyyy-MM-dd") + "' ,[StayDays] = " + DropDownList1.SelectedValue + " , [TotalPrice] = " + TextBox4.Text + " ,[Deposit] = " + Deposit + ", [Remark] = N'" + TextBox6.Text + "' WHERE ID = " + id);
                                    //    code.DatabaseInsert(conn, "UPDATE [dbo].[Customer] SET [Name] = N'" + TextBox2.Text.Replace("'", "''") + "' ,[NickName] = N'" + TextBox3.Text.Replace("'", "''") + "',[FullName] = N'" + TextBox2.Text.Replace("'", "''") + "',[Address] = N'" + ValidationHelper.CleanText(TextBox8.Text) + "',[IDNumber] = N'" + TextBox9.Text.Replace("'", "''") + "' WHERE MobilePhone = '" + TextBox1.Text + "'");


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
                                        checkCreateCustomer();

                                        dtReserve.Clear();
                                        dtReserve.AcceptChanges();

                                        // 🔒 SECURE: Using ReservationDataAccess
                                        var reservationDA = new ReservationDataAccess(conn);
                                        DataTable dtoldAccom = reservationDA.GetOldAccommodations(Convert.ToInt32(id));
                                        DataTable dtoldItem = reservationDA.GetOldItems(Convert.ToInt32(id));
                                        IsDeposit = false;
                                        string msg = "";
                                        int totalnew = 0;
                                        int checkoldAccomRemoved = 0;
                                        List<string> cmds = new List<string>();

                                        // 🔒 RACE CONDITION PREVENTION: Check NEW rooms availability before editing reservation
                                        DateTime? editCheckinDate = code2.ParseDate(TextBox12.Text);
                                        if (editCheckinDate.HasValue && editCheckinDate > DateTime.Parse("1999-01-01"))
                                        {
                                            DateTime editCheckoutDate = editCheckinDate.Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue));

                                            // Check each selected room to see if it's NEW (not in old accommodations)
                                            foreach (GridViewRow row in GridView1.Rows)
                                            {
                                                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                                if (chk != null && chk.Checked)
                                                {
                                                    int accommodationId = Convert.ToInt32(dtAccommodation.Rows[row.RowIndex]["ID"]);
                                                    string accomName = dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString();

                                                    // Check if this is a NEW room (not already in this reservation)
                                                    bool isExistingRoom = false;
                                                    for (int m = 0; m < dtoldAccom.Rows.Count; m++)
                                                    {
                                                        if (dtoldAccom.Rows[m]["Accommodation_ID"].ToString() == accommodationId.ToString())
                                                        {
                                                            isExistingRoom = true;
                                                            break;
                                                        }
                                                    }

                                                    // Only check availability for NEW rooms
                                                    if (!isExistingRoom)
                                                    {
                                                        // ✅ Get requested people count
                                                        int requestedPeople = 1; // Default
                                                        TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);
                                                        if (txtPeopleStay != null && !string.IsNullOrEmpty(txtPeopleStay.Text))
                                                        {
                                                            int.TryParse(txtPeopleStay.Text, out requestedPeople);
                                                        }

                                                        // ✅ Use AccommodationAvailabilityService
                                                        var availabilityService = new AccommodationAvailabilityService(conn);
                                                        var availabilityResult = availabilityService.CheckAvailability(
                                                            accommodationId,
                                                            accomName,
                                                            editCheckinDate.Value,
                                                            editCheckoutDate,
                                                            requestedPeople,
                                                            Convert.ToInt32(id) // Exclude current reservation
                                                        );

                                                        if (!availabilityResult.IsAvailable)
                                                        {
                                                            // Room is not available!
                                                            ClientScript.RegisterStartupScript(this.GetType(), "editRoomConflict",
                                                                $"alert('{availabilityResult.ErrorMessage.Replace("'", "\\'")}');", true);

                                                            code2.Logs(conn, "Edit Reservation Conflict - Race Condition Prevented",
                                                                $"Reservation ID: {id}, Room: {accomName} (ID: {accommodationId}), " +
                                                                $"Requested: {editCheckinDate.Value:yyyy-MM-dd} to {editCheckoutDate:yyyy-MM-dd}, " +
                                                                $"People: {requestedPeople}, Details: {availabilityResult.ConflictDetails ?? "Room not available"}",
                                                                Session["User"]?.ToString() ?? "User");

                                                            return; // Stop edit process
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        // ✅ All NEW rooms are available - proceed with edit
                                        // ✅ FIX: Delete old accommodations that are unchecked
                                        for (int x = 0; x < dtoldAccom.Rows.Count; x++)
                                        {
                                            bool stillChecked = false;
                                            string oldAccomId = dtoldAccom.Rows[x]["Accommodation_ID"].ToString();

                                            // Check if this old room is still checked
                                            foreach (GridViewRow row in GridView1.Rows)
                                            {
                                                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                                if (chk != null && chk.Checked)
                                                {
                                                    if (dtAccommodation.Rows[row.RowIndex]["ID"].ToString() == oldAccomId)
                                                    {
                                                        stillChecked = true;
                                                        break;
                                                    }
                                                }
                                            }

                                            // If old room is no longer checked, DELETE it
                                            if (!stillChecked)
                                            {
                                                if (command == "edit")
                                                {
                                                    reservationDA.DeleteReservationAccommodation(
                                                        Convert.ToInt32(id),
                                                        Convert.ToInt32(oldAccomId)
                                                    );
                                                    msg += $"🗑️ ยกเลิกห้อง: {dtoldAccom.Rows[x]["AccomName"]}\r\n";
                                                    checkoldAccomRemoved++;
                                                }
                                            }
                                        }

                                        for (int x = 0; x < dtoldAccom.Rows.Count; x++)
                                        {
                                            foreach (GridViewRow row in GridView1.Rows)
                                            {
                                                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                                if (chk != null && chk.Checked)
                                                {
                                                    TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);

                                                    // ตรวจสอบว่าเป็นห้องเก่าหรือจองเพิ่ม
                                                    bool isNewRoom = true;
                                                    for (int m = 0; m < dtoldAccom.Rows.Count; m++)
                                                    {
                                                        if (dtoldAccom.Rows[m]["Accommodation_ID"].ToString() == dtAccommodation.Rows[row.RowIndex]["ID"].ToString())
                                                        {
                                                            isNewRoom = false;

                                                            // ถ้าเป็นห้องเก่า ให้เอาแค่ส่วนที่เพิ่มมา
                                                            int oldAmount = Convert.ToInt32(dtoldAccom.Rows[m]["Amount"].ToString());
                                                            int newAmount = Convert.ToInt32(txtPeopleStay.Text);

                                                            if (newAmount > oldAmount)
                                                            {
                                                                int amountDiff = newAmount - oldAmount;
                                                                double pricePerUnit = Convert.ToDouble(row.Cells[4].Text);

                                                                if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True")
                                                                {
                                                                    // คิดตามคน
                                                                    double totalDiff = TwoDecimalPoints(pricePerUnit * amountDiff * Convert.ToInt32(DropDownList1.SelectedValue));
                                                                    dtReserve.Rows.Add(
                                                                        dtReserve.Rows.Count + 1, "", "1",
                                                                        dtAccommodation.Rows[row.RowIndex]["ID"].ToString(),
                                                                        dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เพิ่มจาก " + oldAmount + " เป็น " + newAmount + " คน",
                                                                        amountDiff * Convert.ToInt32(DropDownList1.SelectedValue),
                                                                        "คน-คืน",
                                                                        pricePerUnit,
                                                                        totalDiff
                                                                    );
                                                                    totalnew += (int)totalDiff;
                                                                }
                                                            }
                                                            break;
                                                        }
                                                    }

                                                    // ถ้าเป็นห้องใหม่ ให้เพิ่มเต็มจำนวน
                                                    if (isNewRoom)
                                                    {
                                                        double pricePerUnit = Convert.ToDouble(row.Cells[4].Text);

                                                        if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True")
                                                        {
                                                            double total = TwoDecimalPoints(pricePerUnit * Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(DropDownList1.SelectedValue));
                                                            dtReserve.Rows.Add(
                                                                dtReserve.Rows.Count + 1, "", "1",
                                                                dtAccommodation.Rows[row.RowIndex]["ID"].ToString(),
                                                                dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " [จองเพิ่ม]",
                                                                Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(DropDownList1.SelectedValue),
                                                                "คน-คืน",
                                                                pricePerUnit,
                                                                total
                                                            );
                                                            totalnew += (int)total;
                                                        }
                                                        else
                                                        {
                                                            double total = TwoDecimalPoints(pricePerUnit * Convert.ToInt32(DropDownList1.SelectedValue));
                                                            dtReserve.Rows.Add(
                                                                dtReserve.Rows.Count + 1, "", "1",
                                                                dtAccommodation.Rows[row.RowIndex]["ID"].ToString(),
                                                                dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " [จองเพิ่ม]",
                                                                Convert.ToInt32(DropDownList1.SelectedValue),
                                                                "คืน",
                                                                pricePerUnit,
                                                                total
                                                            );
                                                            totalnew += (int)total;
                                                        }
                                                    }
                                                }
                                            }
                                                
                                        }

                                        foreach (GridViewRow row in GridView1.Rows)
                                        {
                                            TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);
                                            CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                            bool checkdup = false;
                                            int oldAccomIndex = -1;
                                            for (int x = 0; x < dtoldAccom.Rows.Count; x++)
                                            {
                                                if (chk != null && chk.Checked && dtoldAccom.Rows[x]["Accommodation_ID"].ToString() == dtAccommodation.Rows[row.RowIndex]["ID"].ToString())
                                                {
                                                    checkdup = true;
                                                    oldAccomIndex = x;
                                                }
                                            }

                                            // 🔧 FIX: UPDATE existing room's price and amount
                                            if (checkdup == true && chk != null && chk.Checked && command == "edit")
                                            {
                                                // This is an existing room - UPDATE its price and amount
                                                int newAmount;
                                                if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True")
                                                {
                                                    // Room charged by people count
                                                    newAmount = Convert.ToInt32(txtPeopleStay.Text);
                                                }
                                                else
                                                {
                                                    // Room charged by nights
                                                    newAmount = Convert.ToInt32(DropDownList1.SelectedValue);
                                                }

                                                decimal newPrice = Convert.ToDecimal(row.Cells[4].Text);

                                                // UPDATE Reservation_Accommodation with new price and amount
                                                reservationDA.UpdateReservationAccommodation(
                                                    Convert.ToInt32(id),
                                                    Convert.ToInt32(dtAccommodation.Rows[row.RowIndex]["ID"]),
                                                    newAmount,
                                                    newPrice
                                                );
                                            }
                                            else if (checkdup == false && chk != null && chk.Checked)
                                            {
                                                int checkusecoupon = checkAccomUseCoupon(dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), code2.ParseDate(TextBox12.Text).Value);
                                                if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True")
                                                {
                                                    if (command == "edit")
                                                    {
                                                        // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                        reservationDA.InsertReservationAccommodation(
                                                            Convert.ToInt32(id),
                                                            Convert.ToInt32(dtAccommodation.Rows[row.RowIndex]["ID"]),
                                                            Convert.ToInt32(txtPeopleStay.Text),
                                                            Convert.ToDecimal(row.Cells[4].Text),
                                                            checkusecoupon.ToString()
                                                        );
                                                    }
                                                    else
                                                    {
                                                        // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                        reservationDA.InsertReservationAccommodation(
                                                            Convert.ToInt32(id),
                                                            Convert.ToInt32(dtAccommodation.Rows[row.RowIndex]["ID"]),
                                                            Convert.ToInt32(txtPeopleStay.Text),
                                                            Convert.ToDecimal(row.Cells[4].Text),
                                                            checkusecoupon.ToString()
                                                        );
                                                    }
                                                    try
                                                    {
                                                        double pricePerPiece = Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(txtPeopleStay.Text);
                                                        double totalAmount = TwoDecimalPoints(pricePerPiece * Convert.ToInt32(DropDownList1.SelectedValue));
                                                        dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(),
                                                            dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน " + code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy") + " " + txtPeopleStay.Text + " คน",
                                                            Convert.ToInt32(DropDownList1.SelectedValue), "คืน",
                                                            pricePerPiece,
                                                            totalAmount);
                                                    }
                                                    catch
                                                    {
                                                        dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน เช็คเอ้าท์ ", txtPeopleStay.Text, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(DropDownList1.SelectedValue));
                                                    }
                                                    totalnew += Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(txtPeopleStay.Text) * Convert.ToInt32(DropDownList1.SelectedValue);
                                                    msg += "- " + dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " " + txtPeopleStay.Text + " " + dtAccommodation.Rows[row.RowIndex]["Unit"].ToString() + " [จองเพิ่ม]\r\n";
                                                }
                                                else
                                                {
                                                    if (command == "edit")
                                                    {
                                                        // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                        reservationDA.InsertReservationAccommodation(
                                                            Convert.ToInt32(id),
                                                            Convert.ToInt32(dtAccommodation.Rows[row.RowIndex]["ID"]),
                                                            Convert.ToInt32(DropDownList1.SelectedValue),
                                                            Convert.ToDecimal(row.Cells[4].Text),
                                                            checkusecoupon.ToString()
                                                        );
                                                    }
                                                    else
                                                    {
                                                        // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                        reservationDA.InsertReservationAccommodation(
                                                            Convert.ToInt32(id),
                                                            Convert.ToInt32(dtAccommodation.Rows[row.RowIndex]["ID"]),
                                                            Convert.ToInt32(DropDownList1.SelectedValue),
                                                            Convert.ToDecimal(row.Cells[4].Text),
                                                            checkusecoupon.ToString()
                                                        );
                                                    }
                                                    try
                                                    {
                                                        dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน " + code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), DropDownList1.SelectedValue, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue));
                                                    }
                                                    catch {
                                                        dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน เช็คเอ้าท์ ", DropDownList1.SelectedValue, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue));
                                                    }

                                                    totalnew += Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue);
                                                    msg += "- " + dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " " + DropDownList1.SelectedValue + " " + dtAccommodation.Rows[row.RowIndex]["Unit"].ToString() + "[จองเพิ่ม]\r\n";

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
                                                            // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                            reservationDA.UpdateReservationItem(
                                                                Convert.ToInt32(id),
                                                                Convert.ToInt32(dtoldItem.Rows[x]["Items_ID"]),
                                                                Convert.ToInt32(txtAmount.Text),
                                                                Convert.ToDecimal(row.Cells[4].Text)
                                                            );
                                                        }
                                                        else
                                                        {
                                                            // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                            reservationDA.UpdateReservationItem(
                                                                Convert.ToInt32(id),
                                                                Convert.ToInt32(dtoldItem.Rows[x]["Items_ID"]),
                                                                Convert.ToInt32(txtAmount.Text),
                                                                Convert.ToDecimal(row.Cells[4].Text)
                                                            );
                                                        }
                                                        if (Convert.ToInt32(txtAmount.Text) >= Convert.ToInt32(dtoldItem.Rows[x]["Amount"].ToString()))
                                                        {
                                                            int itemmore = Convert.ToInt32(txtAmount.Text) - Convert.ToInt32(dtoldItem.Rows[x]["Amount"].ToString());
                                                            if (itemmore > 0)
                                                            {
                                                                try
                                                                {
                                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "2", dtItems.Rows[row.RowIndex]["ID"].ToString(), dtItems.Rows[row.RowIndex]["ItemName"].ToString() + " เช็คอิน " + code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), itemmore, dtItems.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, (Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue) * itemmore));
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
                                                        msg += "- " + dtItems.Rows[row.RowIndex]["ItemName"].ToString() + " " + txtAmount.Text + " ชิ้น\r\n";
                                                    }
                                                    else
                                                    {
                                                        if (command == "edit")
                                                        {
                                                            // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                            reservationDA.DeleteReservationItem(
                                                                Convert.ToInt32(id),
                                                                Convert.ToInt32(dtoldItem.Rows[x]["Items_ID"])
                                                            );
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
                                                        // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                        reservationDA.InsertReservationItem(
                                                            Convert.ToInt32(id),
                                                            Convert.ToInt32(dtItems.Rows[row.RowIndex]["ID"]),
                                                            Convert.ToInt32(txtAmount.Text),
                                                            Convert.ToDecimal(row.Cells[4].Text)
                                                        );
                                                    }
                                                    else
                                                    {
                                                        // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                        reservationDA.InsertReservationItem(
                                                            Convert.ToInt32(id),
                                                            Convert.ToInt32(dtItems.Rows[row.RowIndex]["ID"]),
                                                            Convert.ToInt32(txtAmount.Text),
                                                            Convert.ToDecimal(row.Cells[4].Text)
                                                        );
                                                    }
                                                    try
                                                    {
                                                        double pricePerPiece = Convert.ToInt32(row.Cells[4].Text);
                                                        double totalAmount = TwoDecimalPoints(pricePerPiece * Convert.ToInt32(DropDownList1.SelectedValue) * Convert.ToInt32(txtAmount.Text));
                                                        dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "2", dtItems.Rows[row.RowIndex]["ID"].ToString(),
                                                            dtItems.Rows[row.RowIndex]["ItemName"].ToString() + " เช็คอิน " + code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"),
                                                            txtAmount.Text, dtItems.Rows[row.RowIndex]["Unit"].ToString(),
                                                            pricePerPiece,
                                                            totalAmount);
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
                                        msg += "\r\nหมายเหตุ: " + TextBox6.Text;
                                        // Upsert customer data (insert or update) - ensures no duplicates and always latest data
                                        code.UpsertCustomer(
                                            conn,
                                            TextBox1.Text,  // MobilePhone
                                            TextBox2.Text,  // Name
                                            TextBox3.Text,  // NickName
                                            "",  // ComeFrom
                                            "",  // Remark
                                            TextBox2.Text,  // FullName
                                            ValidationHelper.CleanText(TextBox8.Text),  // Address
                                            TextBox9.Text,  // IDNumber
                                            TextBox13.Text,  // Email
                                            Convert.ToInt32(DropDownList8.SelectedValue),  // Customer_Type_ID
                                            Convert.ToInt32(_addressHelper.GetAddressIdString(TextBox16.Text, DropDownList5.SelectedItem.Text, DropDownList6.SelectedItem.Text, DropDownList7.SelectedItem.Text)),  // Address_ID
                                            TextBox17.Text,  // Address1
                                            TextBox18.Text  // Branch_Number
                                        );

                                        // ⚠️ uploadSlip moved AFTER createReceipt to ensure Session["PaymentHistoryId"] exists
                                        // uploadSlip(id);  // MOVED DOWN

                                        if (command == "edit")
                                        {
                                            // 📝 Log edit mode entry
                                            code2.Logs(conn, "Reserve Edit Mode",
                                                $"Reservation ID: {id}, CheckBox2: {CheckBox2.Checked}, TextBox10: {TextBox10.Text}, FileUpload: {FileUpload1.HasFile}",
                                                Session["User"]?.ToString());

                                            decimal Deposit = Convert.ToDecimal(TextBox5.Text);
                                            if (CheckBox2.Checked == true && TextBox1.Text != "02")
                                            {
                                                decimal additionalDeposit = 0;
                                                try
                                                {
                                                    additionalDeposit = Convert.ToDecimal(TextBox10.Text);
                                                }
                                                catch
                                                {
                                                    ClientScript.RegisterStartupScript(this.GetType(), "myalert",
                                                        "alert('⚠️ กรุณากรอกยอดมัดจำเพิ่มให้ถูกต้อง');", true);
                                                    code2.Logs(conn, "Reserve Edit Error",
                                                        $"Reservation ID: {id}, Error: Invalid additional deposit amount: {TextBox10.Text}",
                                                        Session["User"]?.ToString());
                                                    return;
                                                }

                                                if (additionalDeposit <= 0)
                                                {
                                                    ClientScript.RegisterStartupScript(this.GetType(), "myalert",
                                                        "alert('⚠️ ยอดมัดจำเพิ่มต้องมากกว่า 0 บาท');", true);
                                                    return;
                                                }

                                                Deposit += additionalDeposit;

                                                // ✅ Check if payment is FULL or DEPOSIT
                                                decimal totalPrice = Convert.ToDecimal(TextBox4.Text);
                                                IsDeposit = Deposit < totalPrice;  // true = มัดจำ, false = ยอดเต็ม

                                                // 📝 Log before receipt creation
                                                code2.Logs(conn, "Reserve Edit - Creating Additional Deposit Receipt",
                                                    $"Reservation ID: {id}, Amount: {additionalDeposit}, Total: {totalPrice}, NewDeposit: {Deposit}, IsDeposit: {IsDeposit}, CheckBox4: {CheckBox4.Checked}",
                                                    Session["User"]?.ToString());

                                                if (CheckBox4.Checked == false)
                                                {
                                                    // 🏨 Add product charges to receipt
                                                    AddProductChargesToReceipt(Convert.ToInt32(id), dtReserve);

                                                    string receiptId = createReceipt(id, Convert.ToDouble(additionalDeposit), dtReserve, IsDeposit, docCreatedDate, CheckBox5.Checked);

                                                    // 📝 Log after receipt creation
                                                    code2.Logs(conn, "Reserve Edit - Receipt Created",
                                                        $"Reservation ID: {id}, Receipt ID: {receiptId}",
                                                        Session["User"]?.ToString());

                                                    // ✅ Upload slip AFTER createReceipt with Receipt_ID
                                                    uploadSlip(id, receiptId);
                                                }
                                                else
                                                {
                                                    // 🏨 ไม่สร้างใบเสร็จ แต่รับเงินแล้ว → mark charges as PAID
                                                    try
                                                    {
                                                        MarkProductChargesAsPaid(Convert.ToInt32(id), "MANUAL_PAYMENT");
                                                        code2.Logs(conn, "Reserve Edit - Manual Payment",
                                                            $"Marked charges as PAID without receipt for Reservation {id}",
                                                            Session["User"]?.ToString());

                                                        // ✅ Create Payment_History record (without Receipt_ID)
                                                        try
                                                        {
                                                            decimal depositAmount = Convert.ToDecimal(additionalDeposit);
                                                            string paymentMethod = DropDownList2.SelectedItem?.Text ?? "TRANSFER";
                                                            int? adminId = Session["UserID"] != null ? (int?)Convert.ToInt32(Session["UserID"]) : null;

                                                            // Determine payment type (use totalPrice from outer scope)
                                                            string paymentType = depositAmount >= totalPrice ? "FULL" : "DEPOSIT";

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
                                                                ) OUTPUT INSERTED.ID VALUES (
                                                                    @ReservationId,
                                                                    GETDATE(),
                                                                    @PaymentAmount,
                                                                    @PaymentType,
                                                                    @PaymentMethod,
                                                                    NULL,
                                                                    @AdminId,
                                                                    @CustomerPhone,
                                                                    'COMPLETED',
                                                                    N'ชำระเงินเพิ่มเติม (ไม่ออกใบเสร็จ)',
                                                                    GETDATE(),
                                                                    GETDATE()
                                                                )";

                                                            var paymentParams = new Dictionary<string, object>
                                                            {
                                                                { "@ReservationId", id },
                                                                { "@PaymentAmount", depositAmount },
                                                                { "@PaymentType", paymentType },
                                                                { "@PaymentMethod", paymentMethod },
                                                                { "@AdminId", adminId ?? (object)DBNull.Value },
                                                                { "@CustomerPhone", TextBox1.Text }
                                                            };

                                                            DataTable dtPaymentId = code2.DatabaseQuerySafe(conn, insertPaymentQuery, paymentParams);
                                                            if (dtPaymentId.Rows.Count > 0)
                                                            {
                                                                long paymentHistoryId = Convert.ToInt64(dtPaymentId.Rows[0][0]);
                                                                Session["PaymentHistoryId"] = paymentHistoryId;
                                                                System.Diagnostics.Debug.WriteLine($"✅ Created Payment_History ID: {paymentHistoryId} (no receipt)");
                                                            }
                                                        }
                                                        catch (Exception exPayment)
                                                        {
                                                            code2.Logs(conn, "Reserve Edit - Payment_History Insert Error",
                                                                $"Reservation {id}: {exPayment.Message}", "SYSTEM");
                                                        }

                                                        // ✅ Upload slip without receipt (NULL Receipt_ID)
                                                        uploadSlip(id, null);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        code2.Logs(conn, "Reserve Edit - Manual Payment Error",
                                                            $"Reservation {id}, Error: {ex.Message}",
                                                            Session["User"]?.ToString());
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                // ✅ No payment checkbox - but might have slip from earlier
                                                // Upload slip anyway if file exists
                                                if (FileUpload1.HasFile)
                                                {
                                                    uploadSlip(id, null);
                                                }
                                            }

                                            // 💰 Calculate NEW total price from ALL selected rooms and items
                                            decimal calculatedTotalPrice = 0;

                                            try
                                            {
                                                // Calculate from ALL selected accommodations (not just new ones)
                                                foreach (GridViewRow row in GridView1.Rows)
                                                {
                                                    CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                                    if (chk != null && chk.Checked)
                                                    {
                                                        TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);
                                                        decimal pricePerUnit = Convert.ToDecimal(row.Cells[4].Text);
                                                        int stayDays = Convert.ToInt32(DropDownList1.SelectedValue);

                                                        if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True")
                                                        {
                                                            // คิดตามคน
                                                            int peopleCount = Convert.ToInt32(txtPeopleStay.Text);
                                                            calculatedTotalPrice += pricePerUnit * peopleCount * stayDays;
                                                        }
                                                        else
                                                        {
                                                            // คิดตามห้อง/คืน
                                                            calculatedTotalPrice += pricePerUnit * stayDays;
                                                        }
                                                    }
                                                }

                                                // Calculate from ALL selected items
                                                foreach (GridViewRow row in GridView2.Rows)
                                                {
                                                    CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                                    if (chk != null && chk.Checked)
                                                    {
                                                        TextBox txtAmount = (row.Cells[2].FindControl("txtAmount") as TextBox);
                                                        decimal pricePerUnit = Convert.ToDecimal(row.Cells[4].Text);
                                                        int itemAmount = Convert.ToInt32(txtAmount.Text);
                                                        int stayDays = Convert.ToInt32(DropDownList1.SelectedValue);

                                                        calculatedTotalPrice += pricePerUnit * itemAmount * stayDays;
                                                    }
                                                }

                                                // 📝 Log calculated price
                                                code2.Logs(conn, "Reserve Edit - Price Calculation",
                                                    $"Reservation ID: {id}, Old Price: {TextBox4.Text}, " +
                                                    $"New Calculated Price: {calculatedTotalPrice}",
                                                    Session["User"]?.ToString());
                                            }
                                            catch (Exception ex)
                                            {
                                                code2.Logs(conn, "Reserve Edit - Price Calculation Error",
                                                    $"Reservation ID: {id}, Error: {ex.Message}, Using TextBox4: {TextBox4.Text}",
                                                    Session["User"]?.ToString());
                                                // Fallback to TextBox4 if calculation fails
                                                calculatedTotalPrice = Convert.ToDecimal(TextBox4.Text);
                                            }

                                            try
                                            {
                                                // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                DateTime? checkinDate = code2.ParseDate(TextBox12.Text);
                                                if (checkinDate.HasValue)
                                                {
                                                    reservationDA.UpdateReservation(
                                                        Convert.ToInt32(id),
                                                        TextBox1.Text,
                                                        checkinDate.Value,
                                                        checkinDate.Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue)),
                                                        Convert.ToInt32(DropDownList1.SelectedValue),
                                                        calculatedTotalPrice,  // ✅ Use calculated price
                                                        Deposit,
                                                        TextBox6.Text
                                                    );
                                                }
                                                else
                                                {
                                                    throw new Exception("Invalid check-in date");
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                if (TextBox12.Text == null || TextBox12.Text == "")
                                                {
                                                    // ✅ FIXED: Use parameterized query even in fallback case
                                                    reservationDA.UpdateReservation(
                                                        Convert.ToInt32(id),
                                                        TextBox1.Text,
                                                        DateTime.Parse("1990-01-01"),
                                                        DateTime.Parse("1990-01-01"),
                                                        Convert.ToInt32(DropDownList1.SelectedValue),
                                                        calculatedTotalPrice,  // ✅ Use calculated price
                                                        Deposit,
                                                        TextBox6.Text
                                                    );
                                                }
                                                else
                                                {
                                                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('โปรแกรมคำนวนยอดไม่ถูกต้อง กรุณาลองใหม่อีกครั้ง');", true);
                                                }
                                            }
                                            try
                                            {
                                                //code2.SendLineMessageAPI("Ccf82e94eb4f39cc97eaecdeae2edfd16", "แก้ไขการจองหมายเลข: " + id + "\r\nหมายเลขโทรศัพท์: " + TextBox1.Text + "\r\nเช็คอินวันที่: " + code2.ParseDate(TextBox12.Text).ToString("dd MMMM yyyy") + "\r\nเช็คเอ้าท์วันที่: " + code2.ParseDate(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("dd MMMM yyyy") + "\r\n" + msg, "", "") ;
                                                //Thread.Sleep(1000);
                                                //SendLineNotify("แก้ไขการจองหมายเลข: "+ id+ "\r\nหมายเลขโทรศัพท์: " + TextBox1.Text + "\r\nเช็คอินวันที่: " + code2.ParseDate(TextBox12.Text).ToString("dd MMMM yyyy") + "\r\nเช็คเอ้าท์วันที่: " + code2.ParseDate(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("dd MMMM yyyy") + "\r\n"+msg);
                                                ////                                        using (var client = new HttpClient())
                                                ////                                        {
                                                ////                                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ConfigurationManager.AppSettings["linechannelaccesstokentaketime"]);
                                                ////                                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                                                ////                                            var jsonPayload = new
                                                ////                                            {
                                                ////                                                to = ConfigurationManager.AppSettings["lineuserid"],
                                                ////                                                messages = new[]
                                                ////                                                {
                                                ////    new { type = "text", text = "แก้ไขการจองหมายเลข: " + id + "\r\nหมายเลขโทรศัพท์: " + TextBox1.Text + "\r\nเช็คอินวันที่: " + code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy") + "\r\nเช็คเอ้าท์วันที่: " + code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("dd MMMM yyyy") + "\r\n" + msg }
                                                ////}
                                                ////                                            };

                                                ////                                            var json = JsonConvert.SerializeObject(jsonPayload);
                                                ////                                            var content = new StringContent(json, Encoding.UTF8, "application/json");
                                                ////                                            if(msg.Contains("[จองเพิ่ม]"))
                                                ////                                                    {
                                                ////                                                ////var response = await client.PostAsync("https://api.line.me/v2/bot/message/push", content);
                                                ////                                                Console.WriteLine(await response.Content.ReadAsStringAsync());
                                                ////                                            }

                                                ////                                        }
                                                ///                                     bool hasRoomChanges = false;
                                                // ในส่วนของ command == "edit" ให้แทนที่ส่วนตรวจสอบการเปลี่ยนแปลงด้วย:

                                                bool hasAnyChanges = false;
                                                StringBuilder changeDetails = new StringBuilder();

                                                // 1. ตรวจสอบการเปลี่ยนแปลงข้อมูลลูกค้า
                                                // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                DataTable dtOldCustomer = reservationDA.GetCustomerByPhone(TextBox1.Text);
                                                if (dtOldCustomer.Rows.Count > 0)
                                                {
                                                    if (dtOldCustomer.Rows[0]["Name"].ToString() != TextBox2.Text)
                                                    {
                                                        hasAnyChanges = true;
                                                        changeDetails.AppendLine($"👤 เปลี่ยนชื่อ: {dtOldCustomer.Rows[0]["Name"]} → {TextBox2.Text}");
                                                    }

                                                    if (dtOldCustomer.Rows[0]["NickName"].ToString() != TextBox3.Text)
                                                    {
                                                        hasAnyChanges = true;
                                                        changeDetails.AppendLine($"🎭 เปลี่ยนชื่อเล่น: {dtOldCustomer.Rows[0]["NickName"]} → {TextBox3.Text}");
                                                    }
                                                }

                                                // 2. ตรวจสอบการเปลี่ยนแปลงวันที่
                                                // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                DataTable dtOldReservation = reservationDA.GetReservationByIdAndPhone(Convert.ToInt32(id), TextBox1.Text);
                                                if (dtOldReservation.Rows.Count > 0)
                                                {
                                                    DateTime oldCheckin = Convert.ToDateTime(dtOldReservation.Rows[0]["CheckinDate"]);
                                                    DateTime oldCheckout = Convert.ToDateTime(dtOldReservation.Rows[0]["CheckoutDate"]);
                                                    int oldStayDays = Convert.ToInt32(dtOldReservation.Rows[0]["StayDays"]);

                                                    DateTime newCheckin = code2.ParseDate(TextBox12.Text).Value;
                                                    DateTime newCheckout = newCheckin.AddDays(Convert.ToDouble(DropDownList1.SelectedValue));
                                                    int newStayDays = Convert.ToInt32(DropDownList1.SelectedValue);

                                                    if (oldCheckin != newCheckin)
                                                    {
                                                        hasAnyChanges = true;
                                                        changeDetails.AppendLine($"📅 เปลี่ยนเช็คอิน: {oldCheckin.ToString("dd MMM yyyy")} → {newCheckin.ToString("dd MMM yyyy")}");
                                                    }

                                                    if (oldCheckout != newCheckout)
                                                    {
                                                        hasAnyChanges = true;
                                                        changeDetails.AppendLine($"📅 เปลี่ยนเช็คเอ้าท์: {oldCheckout.ToString("dd MMM yyyy")} → {newCheckout.ToString("dd MMM yyyy")}");
                                                    }

                                                    if (oldStayDays != newStayDays)
                                                    {
                                                        hasAnyChanges = true;
                                                        changeDetails.AppendLine($"🕐 เปลี่ยนจำนวนคืน: {oldStayDays} → {newStayDays} คืน");
                                                    }

                                                    // ตรวจสอบการเปลี่ยนแปลงราคา
                                                    decimal oldPrice = Convert.ToDecimal(dtOldReservation.Rows[0]["TotalPrice"]);
                                                    decimal newPrice = Convert.ToDecimal(TextBox4.Text);
                                                    if (oldPrice != newPrice)
                                                    {
                                                        hasAnyChanges = true;
                                                        changeDetails.AppendLine($"💰 เปลี่ยนราคา: {oldPrice:N0} → {newPrice:N0} บาท");
                                                    }
                                                }

                                                // 3. ตรวจสอบการเปลี่ยนแปลงห้องพัก
                                                List<string> oldRoomList = new List<string>();
                                                List<string> newRoomList = new List<string>();

                                                // รายการห้องเดิม
                                                foreach (DataRow row in dtoldAccom.Rows)
                                                {
                                                    string roomInfo = $"{row["AccomName"]} ({row["Amount"]} คน)";
                                                    oldRoomList.Add(roomInfo);
                                                }

                                                // รายการห้องใหม่
                                                foreach (GridViewRow row in GridView1.Rows)
                                                {
                                                    CheckBox chk = (row.Cells[0].FindControl("chkSelect")) as CheckBox;
                                                    if (chk != null && chk.Checked)
                                                    {
                                                        TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay")) as TextBox;
                                                        string roomInfo = $"{row.Cells[1].Text} ({txtPeopleStay.Text} คน)";
                                                        newRoomList.Add(roomInfo);
                                                    }
                                                }

                                                // เปรียบเทียบรายการห้อง
                                                bool roomsIdentical = oldRoomList.Count == newRoomList.Count;
                                                if (roomsIdentical)
                                                {
                                                    for (int i = 0; i < oldRoomList.Count; i++)
                                                    {
                                                        if (oldRoomList[i] != newRoomList[i])
                                                        {
                                                            roomsIdentical = false;
                                                            break;
                                                        }
                                                    }
                                                }

                                                // ถ้าห้องไม่เหมือนเดิมเป๊ะๆ
                                                if (!roomsIdentical)
                                                {
                                                    hasAnyChanges = true;
                                                    changeDetails.AppendLine("🏨 การเปลี่ยนแปลงห้องพัก:");

                                                    // หาห้องที่เพิ่ม
                                                    var addedRooms = newRoomList.Except(oldRoomList).ToList();
                                                    foreach (var room in addedRooms)
                                                    {
                                                        changeDetails.AppendLine($"   ➕ {room}");
                                                    }

                                                    // หาห้องที่ลบ
                                                    var removedRooms = oldRoomList.Except(newRoomList).ToList();
                                                    foreach (var room in removedRooms)
                                                    {
                                                        changeDetails.AppendLine($"   ➖ {room}");
                                                    }

                                                    // หาห้องที่แก้ไข
                                                    var commonRooms = newRoomList.Intersect(oldRoomList).ToList();
                                                    foreach (var room in commonRooms)
                                                    {
                                                        // แสดงห้องที่ยังคงอยู่แต่อาจมีการเปลี่ยนแปลงอื่น
                                                        changeDetails.AppendLine($"   ✅ {room}");
                                                    }
                                                }

                                                // 4. ตรวจสอบการเปลี่ยนแปลงของเช่า
                                                List<string> oldItemList = new List<string>();
                                                List<string> newItemList = new List<string>();

                                                // รายการของเดิม
                                                foreach (DataRow row in dtoldItem.Rows)
                                                {
                                                    string itemInfo = $"{row["ItemName"]} ({row["Amount"]} ชิ้น)";
                                                    oldItemList.Add(itemInfo);
                                                }

                                                // รายการของใหม่
                                                foreach (GridViewRow row in GridView2.Rows)
                                                {
                                                    CheckBox chk = (row.Cells[0].FindControl("chkSelect")) as CheckBox;
                                                    if (chk != null && chk.Checked)
                                                    {
                                                        TextBox txtAmount = (row.Cells[2].FindControl("txtAmount")) as TextBox;
                                                        string itemInfo = $"{row.Cells[1].Text} ({txtAmount.Text} ชิ้น)";
                                                        newItemList.Add(itemInfo);
                                                    }
                                                }

                                                // เปรียบเทียบรายการของเช่า
                                                bool itemsIdentical = oldItemList.Count == newItemList.Count;
                                                if (itemsIdentical)
                                                {
                                                    for (int i = 0; i < oldItemList.Count; i++)
                                                    {
                                                        if (oldItemList[i] != newItemList[i])
                                                        {
                                                            itemsIdentical = false;
                                                            break;
                                                        }
                                                    }
                                                }

                                                if (!itemsIdentical)
                                                {
                                                    hasAnyChanges = true;
                                                    changeDetails.AppendLine("🛍️ การเปลี่ยนแปลงของเช่า:");

                                                    var addedItems = newItemList.Except(oldItemList).ToList();
                                                    foreach (var item in addedItems)
                                                    {
                                                        changeDetails.AppendLine($"   ➕ {item}");
                                                    }

                                                    var removedItems = oldItemList.Except(newItemList).ToList();
                                                    foreach (var item in removedItems)
                                                    {
                                                        changeDetails.AppendLine($"   ➖ {item}");
                                                    }
                                                }

                                                // 5. ตรวจสอบการเปลี่ยนแปลงหมายเหตุ
                                                string oldRemark = dtOldReservation.Rows[0]["Remark"].ToString();
                                                if (oldRemark != TextBox6.Text)
                                                {
                                                    hasAnyChanges = true;
                                                    changeDetails.AppendLine($"💬 เปลี่ยนหมายเหตุ: {oldRemark} → {TextBox6.Text}");
                                                }

                                                // ส่งข้อความถ้ามีการเปลี่ยนแปลงใดๆ
                                                if (hasAnyChanges)
                                                {
                                                    string message = $@"✏️ ═══ แก้ไขการจอง ═══

📋 หมายเลขการจอง: {id}
👤 ชื่อผู้จอง: {TextBox2.Text}
📞 เบอร์โทร: {TextBox1.Text}

📅 วันที่เข้าพัก: {code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("th-TH"))}
📅 วันที่ออก: {code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("th-TH"))}
🌙 จำนวนคืน: {DropDownList1.SelectedValue} คืน

💰 ยอดรวมทั้งหมด: {Convert.ToDecimal(TextBox4.Text):N2} บาท

📝 รายละเอียดการเปลี่ยนแปลง:
{changeDetails}

👨‍💼 แก้ไขโดย: {Session["UserName"]?.ToString() ?? "System"}
━━━━━━━━━━━━━━━━━";

                                                    var bot = new TelegramBot2(ConfigurationSettings.AppSettings["TelegramTokenTakeTime"].ToString());
                                                    await bot.SendMessageAsync("-4969611371", message);
                                                }
                                            }
                                            catch { }

                                            // ✅ Edit mode always redirects to ReserveTable
                                            // 📝 Log edit completion
                                            code2.Logs(conn, "Reserve Edit - Completed",
                                                $"Reservation ID: {id}, HasAdditionalDeposit: {CheckBox2.Checked}, " +
                                                $"TotalPrice: {TextBox4.Text}, Deposit: {Deposit}",
                                                Session["User"]?.ToString());

                                            // 📊 All edit operations redirect to ReserveTable
                                            Response.Redirect("/ReserveTable", false);
                                            HttpContext.Current.ApplicationInstance.CompleteRequest();



                                        }
                                        else if (command == "rentmore" && TextBox1.Text != "02")
                                        {
                                            decimal Deposit = Convert.ToDecimal(TextBox5.Text);

                                            // ✅ Process payment if checkbox is checked AND validation passes
                                            if (CheckBox2.Checked == true)
                                            {
                                                // Validate calculation before processing payment
                                                if (checkoldAccomRemoved == 0 && checkoldItemRemoved == 0 && totalnew.ToString() == TextBox10.Text)
                                                {
                                                    for (int i = 0; i < cmds.Count; i++)
                                                    {
                                                        code.DatabaseInsert(conn, cmds[i]);
                                                    }
                                                    Deposit += Convert.ToDecimal(TextBox10.Text);

                                                    // ✅ Check if payment is FULL or DEPOSIT (same logic as EDIT mode)
                                                    decimal totalPrice = Convert.ToDecimal(TextBox4.Text);
                                                    IsDeposit = Deposit < totalPrice;  // true = มัดจำ, false = ยอดเต็ม

                                                    if (CheckBox4.Checked == false)
                                                    {
                                                        // 🏨 Add product charges to receipt
                                                        AddProductChargesToReceipt(Convert.ToInt32(id), dtReserve);

                                                        string receiptId = createReceipt(id, Convert.ToDouble(TextBox10.Text), dtReserve, IsDeposit, docCreatedDate, CheckBox5.Checked);

                                                        // ✅ Upload slip AFTER createReceipt with Receipt_ID
                                                        uploadSlip(id, receiptId);
                                                    }
                                                    else
                                                    {
                                                        // 🏨 ไม่สร้างใบเสร็จ แต่รับเงินแล้ว → mark charges as PAID
                                                        try
                                                        {
                                                            MarkProductChargesAsPaid(Convert.ToInt32(id), "MANUAL_PAYMENT");
                                                            code2.Logs(conn, "Reserve RentMore - Manual Payment",
                                                                $"Marked charges as PAID without receipt for Reservation {id}",
                                                                Session["User"]?.ToString());

                                                            // ✅ Create Payment_History record (without Receipt_ID)
                                                            try
                                                            {
                                                                decimal depositAmount = Convert.ToDecimal(TextBox10.Text);
                                                                string paymentMethod = DropDownList2.SelectedItem?.Text ?? "TRANSFER";
                                                                int? adminId = Session["UserID"] != null ? (int?)Convert.ToInt32(Session["UserID"]) : null;

                                                                // Determine payment type (use totalPrice from outer scope)
                                                                string paymentType = depositAmount >= totalPrice ? "FULL" : "DEPOSIT";

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
                                                                    ) OUTPUT INSERTED.ID VALUES (
                                                                        @ReservationId,
                                                                        GETDATE(),
                                                                        @PaymentAmount,
                                                                        @PaymentType,
                                                                        @PaymentMethod,
                                                                        NULL,
                                                                        @AdminId,
                                                                        @CustomerPhone,
                                                                        'COMPLETED',
                                                                        N'เช่าเพิ่ม (ไม่ออกใบเสร็จ)',
                                                                        GETDATE(),
                                                                        GETDATE()
                                                                    )";

                                                                var paymentParams = new Dictionary<string, object>
                                                                {
                                                                    { "@ReservationId", id },
                                                                    { "@PaymentAmount", depositAmount },
                                                                    { "@PaymentType", paymentType },
                                                                    { "@PaymentMethod", paymentMethod },
                                                                    { "@AdminId", adminId ?? (object)DBNull.Value },
                                                                    { "@CustomerPhone", TextBox1.Text }
                                                                };

                                                                DataTable dtPaymentId = code2.DatabaseQuerySafe(conn, insertPaymentQuery, paymentParams);
                                                                if (dtPaymentId.Rows.Count > 0)
                                                                {
                                                                    long paymentHistoryId = Convert.ToInt64(dtPaymentId.Rows[0][0]);
                                                                    Session["PaymentHistoryId"] = paymentHistoryId;
                                                                    System.Diagnostics.Debug.WriteLine($"✅ Created Payment_History ID: {paymentHistoryId} (rentmore, no receipt)");
                                                                }
                                                            }
                                                            catch (Exception exPayment)
                                                            {
                                                                code2.Logs(conn, "Reserve RentMore - Payment_History Insert Error",
                                                                    $"Reservation {id}: {exPayment.Message}", "SYSTEM");
                                                            }

                                                            // ✅ Upload slip without receipt (NULL Receipt_ID)
                                                            uploadSlip(id, null);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            code2.Logs(conn, "Reserve RentMore - Manual Payment Error",
                                                                $"Reservation {id}, Error: {ex.Message}",
                                                                Session["User"]?.ToString());
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    // ❌ Validation failed - show error but still update reservation below
                                                    TextBox10.Text = "";
                                                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('โปรแกรมคำนวนยอดไม่ถูกต้อง กรุณาลองใหม่อีกครั้ง');", true);
                                                }
                                            }

                                            // ✅ FIXED: ALWAYS update reservation (same as EDIT mode) - moved outside payment conditional
                                            try
                                            {
                                                reservationDA.UpdateReservation(
                                                    Convert.ToInt32(id),
                                                    TextBox1.Text,
                                                    code2.ParseDate(TextBox12.Text).Value,
                                                    code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue)),
                                                    Convert.ToInt32(DropDownList1.SelectedValue),
                                                    Convert.ToDecimal(TextBox4.Text),
                                                    Deposit,
                                                    TextBox6.Text
                                                );

                                                // 🛒 จองของเช่าเพิ่มสำเร็จ → ไปหน้า ReserveTable
                                                Response.Redirect("/ReserveTable", false);
                                                HttpContext.Current.ApplicationInstance.CompleteRequest();
                                            }
                                            catch (Exception ex)
                                            {
                                                code2.Logs(conn, "Reserve RentMore - UpdateReservation Error",
                                                    $"Reservation {id}, Error: {ex.Message}",
                                                    Session["User"]?.ToString());
                                                ClientScript.RegisterStartupScript(this.GetType(), "myalert",
                                                    "alert('เกิดข้อผิดพลาดในการอัพเดทข้อมูล กรุณาลองใหม่อีกครั้ง');", true);
                                            }
                                        }
                                        else
                                        {

                                            Response.Redirect("./Reservation_Confirmed?id=" + id + "&check=" + TextBox1.Text);
                                        }
                                    }
                                    else if (command == "checkin" && Session["permission"].ToString() == "True" && TextBox1.Text != "02")
                                    {
                                        // 🔒 SECURE: Initialize ReservationDataAccess for checkin mode
                                        var reservationDA = new ReservationDataAccess(conn);

                                        IsDeposit = false;
                                        // Upsert customer data (insert or update) - ensures no duplicates and always latest data
                                        code.UpsertCustomer(
                                            conn,
                                            TextBox1.Text,  // MobilePhone
                                            TextBox2.Text,  // Name
                                            TextBox3.Text,  // NickName
                                            "",  // ComeFrom
                                            "",  // Remark
                                            TextBox2.Text,  // FullName
                                            ValidationHelper.CleanText(TextBox8.Text),  // Address
                                            TextBox9.Text,  // IDNumber
                                            TextBox13.Text,  // Email
                                            Convert.ToInt32(DropDownList8.SelectedValue),  // Customer_Type_ID
                                            Convert.ToInt32(_addressHelper.GetAddressIdString(TextBox16.Text, DropDownList5.SelectedItem.Text, DropDownList6.SelectedItem.Text, DropDownList7.SelectedItem.Text)),  // Address_ID
                                            TextBox17.Text,  // Address1
                                            TextBox18.Text  // Branch_Number
                                        );

                                        // 🆕 Check if payment checkbox is checked
                                        if (CheckBox2.Checked && !string.IsNullOrEmpty(TextBox10.Text))
                                        {
                                            // CheckIn mode: Must pay exact remaining amount (locked)
                                            decimal paymentAmount = Convert.ToDecimal(TextBox10.Text);
                                            decimal Deposit = Convert.ToDecimal(TextBox5.Text);
                                            decimal totalAmount = Convert.ToDecimal(TextBox4.Text);
                                            decimal remainingAmount = totalAmount - Deposit;

                                            // 🔒 Validate: payment must equal remaining amount (prevent manual editing)
                                            if (paymentAmount != remainingAmount && remainingAmount > 0)
                                            {
                                                ClientScript.RegisterStartupScript(this.GetType(), "myalert",
                                                    "alert('ยอดชำระต้องเท่ากับยอดคงเหลือ " + remainingAmount.ToString("N0") + " บาทเท่านั้น\\nไม่สามารถแก้ไขยอดได้');", true);
                                                return;
                                            }

                                            if (Convert.ToDecimal(TextBox4.Text) == Convert.ToDecimal(TextBox5.Text))
                                            {
                                                // Already paid in full - just check in
                                                reservationDA.CheckInReservation(Convert.ToInt32(id));
                                            }
                                            else
                                            {
                                            TextBox5.Enabled = false;
                                            // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                            DataTable dtfindDeposit = reservationDA.GetDepositReceipts(Convert.ToInt32(id));

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
                                                        dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน " + code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), txtPeopleStay.Text, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(txtPeopleStay.Text));
                                                    }
                                                    else
                                                    {
                                                        dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", dtAccommodation.Rows[row.RowIndex]["ID"].ToString(), dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() + " เช็คอิน " + code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), DropDownList1.SelectedValue, dtAccommodation.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue));
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
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "2", dtItems.Rows[row.RowIndex]["ID"].ToString(), dtItems.Rows[row.RowIndex]["ItemName"].ToString() + " เช็คอิน " + code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"), txtAmount.Text, dtItems.Rows[row.RowIndex]["Unit"].ToString(), row.Cells[4].Text, (Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue) * Convert.ToInt32(txtAmount.Text)));
                                                }
                                            }
                                            decimal DepositAmount = 0;
                                            if (dtfindDeposit.Rows.Count <= 0)
                                            {
                                                // 🆕 Use manual payment amount from TextBox10 (like rentmore)
                                                // Deposit already declared above at line 1636
                                                dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", "17", "ส่วนลด", "1", "ครั้ง", Deposit * -1, Deposit * -1);
                                                id = Request.QueryString["id"];
                                                if (CheckBox4.Checked == false)
                                                {
                                                    // 🏨 Add product charges to receipt
                                                    AddProductChargesToReceipt(Convert.ToInt32(id), dtReserve);

                                                    string receiptId = createReceipt(id, Convert.ToDouble(paymentAmount), dtReserve, IsDeposit, docCreatedDate, CheckBox5.Checked);

                                                    // ✅ Upload slip AFTER createReceipt with Receipt_ID
                                                    uploadSlip(id, receiptId);
                                                }
                                                else
                                                {
                                                    // 🏨 ไม่สร้างใบเสร็จ แต่รับเงินแล้ว → mark charges as PAID
                                                    try
                                                    {
                                                        MarkProductChargesAsPaid(Convert.ToInt32(id), "MANUAL_PAYMENT");
                                                        code2.Logs(conn, "Reserve CheckIn - Manual Payment (No Deposit)",
                                                            $"Marked charges as PAID without receipt for Reservation {id}",
                                                            Session["User"]?.ToString());

                                                        // ✅ Create Payment_History record (without Receipt_ID)
                                                        try
                                                        {
                                                            decimal depositAmount = Convert.ToDecimal(paymentAmount);
                                                            string paymentMethod = DropDownList2.SelectedItem?.Text ?? "TRANSFER";
                                                            int? adminId = Session["UserID"] != null ? (int?)Convert.ToInt32(Session["UserID"]) : null;

                                                            // Determine payment type
                                                            decimal totalPrice = Convert.ToDecimal(Session["totalPrice"]?.ToString() ?? TextBox4.Text ?? "0");
                                                            string paymentType = depositAmount >= totalPrice ? "FULL" : "DEPOSIT";

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
                                                                ) OUTPUT INSERTED.ID VALUES (
                                                                    @ReservationId,
                                                                    GETDATE(),
                                                                    @PaymentAmount,
                                                                    @PaymentType,
                                                                    @PaymentMethod,
                                                                    NULL,
                                                                    @AdminId,
                                                                    @CustomerPhone,
                                                                    'COMPLETED',
                                                                    N'เช็คอิน (ไม่ออกใบเสร็จ)',
                                                                    GETDATE(),
                                                                    GETDATE()
                                                                )";

                                                            var paymentParams = new Dictionary<string, object>
                                                            {
                                                                { "@ReservationId", id },
                                                                { "@PaymentAmount", depositAmount },
                                                                { "@PaymentType", paymentType },
                                                                { "@PaymentMethod", paymentMethod },
                                                                { "@AdminId", adminId ?? (object)DBNull.Value },
                                                                { "@CustomerPhone", TextBox1.Text }
                                                            };

                                                            DataTable dtPaymentId = code2.DatabaseQuerySafe(conn, insertPaymentQuery, paymentParams);
                                                            if (dtPaymentId.Rows.Count > 0)
                                                            {
                                                                long paymentHistoryId = Convert.ToInt64(dtPaymentId.Rows[0][0]);
                                                                Session["PaymentHistoryId"] = paymentHistoryId;
                                                                System.Diagnostics.Debug.WriteLine($"✅ Created Payment_History ID: {paymentHistoryId} (checkin, no receipt, no deposit)");
                                                            }
                                                        }
                                                        catch (Exception exPayment)
                                                        {
                                                            code2.Logs(conn, "Reserve CheckIn - Payment_History Insert Error",
                                                                $"Reservation {id}: {exPayment.Message}", "SYSTEM");
                                                        }

                                                        // ✅ Upload slip without receipt (NULL Receipt_ID)
                                                        uploadSlip(id, null);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        code2.Logs(conn, "Reserve CheckIn - Manual Payment Error",
                                                            $"Reservation {id}, Error: {ex.Message}",
                                                            Session["User"]?.ToString());
                                                    }
                                                }
                                                // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                reservationDA.CheckInReservation(Convert.ToInt32(id));
                                            }
                                            else
                                            {
                                                for (int j = 0; j < dtfindDeposit.Rows.Count; j++)
                                                {
                                                    // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                    DataTable dtDepositDetail = reservationDA.GetReceiptDetails(dtfindDeposit.Rows[j]["ID"].ToString());
                                                    for (int k = 0; k < dtDepositDetail.Rows.Count; k++)
                                                    {
                                                        DepositAmount += Convert.ToDecimal(dtDepositDetail.Rows[0]["Price_Amount"].ToString());
                                                        dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", dtDepositDetail.Rows[0]["ProductType_ID"].ToString(), dtDepositDetail.Rows[0]["Product_ID"].ToString(), dtDepositDetail.Rows[0]["Product_Data"].ToString(), dtDepositDetail.Rows[0]["Product_Amount"].ToString(), dtDepositDetail.Rows[0]["Product_Unit"].ToString(), Convert.ToDecimal(dtDepositDetail.Rows[0]["Price_PerPeice"].ToString()) * -1, Convert.ToDecimal(dtDepositDetail.Rows[0]["Price_Amount"].ToString()) * -1);
                                                    }
                                                }


                                                if (DepositAmount == Convert.ToDecimal(TextBox5.Text))
                                                {
                                                    // 🆕 Use manual payment amount from TextBox10
                                                    id = Request.QueryString["id"];
                                                    if (CheckBox4.Checked == false)
                                                    {
                                                        // 🏨 Add product charges to receipt
                                                        AddProductChargesToReceipt(Convert.ToInt32(id), dtReserve);

                                                        string receiptId = createReceipt(id, Convert.ToDouble(paymentAmount), dtReserve, IsDeposit, docCreatedDate, CheckBox5.Checked);

                                                        // ✅ Upload slip AFTER createReceipt with Receipt_ID
                                                        uploadSlip(id, receiptId);
                                                    }
                                                    else
                                                    {
                                                        // 🏨 ไม่สร้างใบเสร็จ แต่รับเงินแล้ว → mark charges as PAID
                                                        try
                                                        {
                                                            MarkProductChargesAsPaid(Convert.ToInt32(id), "MANUAL_PAYMENT");
                                                            code2.Logs(conn, "Reserve CheckIn - Manual Payment (Exact Deposit)",
                                                                $"Marked charges as PAID without receipt for Reservation {id}",
                                                                Session["User"]?.ToString());

                                                            // ✅ Create Payment_History record (without Receipt_ID)
                                                            try
                                                            {
                                                                decimal depositAmount = Convert.ToDecimal(paymentAmount);
                                                                string paymentMethod = DropDownList2.SelectedItem?.Text ?? "TRANSFER";
                                                                int? adminId = Session["UserID"] != null ? (int?)Convert.ToInt32(Session["UserID"]) : null;

                                                                // Determine payment type
                                                                decimal totalPrice = Convert.ToDecimal(Session["totalPrice"]?.ToString() ?? TextBox4.Text ?? "0");
                                                                string paymentType = depositAmount >= totalPrice ? "FULL" : "DEPOSIT";

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
                                                                    ) OUTPUT INSERTED.ID VALUES (
                                                                        @ReservationId,
                                                                        GETDATE(),
                                                                        @PaymentAmount,
                                                                        @PaymentType,
                                                                        @PaymentMethod,
                                                                        NULL,
                                                                        @AdminId,
                                                                        @CustomerPhone,
                                                                        'COMPLETED',
                                                                        N'เช็คอิน (ไม่ออกใบเสร็จ)',
                                                                        GETDATE(),
                                                                        GETDATE()
                                                                    )";

                                                                var paymentParams = new Dictionary<string, object>
                                                                {
                                                                    { "@ReservationId", id },
                                                                    { "@PaymentAmount", depositAmount },
                                                                    { "@PaymentType", paymentType },
                                                                    { "@PaymentMethod", paymentMethod },
                                                                    { "@AdminId", adminId ?? (object)DBNull.Value },
                                                                    { "@CustomerPhone", TextBox1.Text }
                                                                };

                                                                DataTable dtPaymentId = code2.DatabaseQuerySafe(conn, insertPaymentQuery, paymentParams);
                                                                if (dtPaymentId.Rows.Count > 0)
                                                                {
                                                                    long paymentHistoryId = Convert.ToInt64(dtPaymentId.Rows[0][0]);
                                                                    Session["PaymentHistoryId"] = paymentHistoryId;
                                                                    System.Diagnostics.Debug.WriteLine($"✅ Created Payment_History ID: {paymentHistoryId} (checkin, no receipt, exact deposit)");
                                                                }
                                                            }
                                                            catch (Exception exPayment)
                                                            {
                                                                code2.Logs(conn, "Reserve CheckIn - Payment_History Insert Error",
                                                                    $"Reservation {id}: {exPayment.Message}", "SYSTEM");
                                                            }

                                                            // ✅ Upload slip without receipt (NULL Receipt_ID)
                                                            uploadSlip(id, null);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            code2.Logs(conn, "Reserve CheckIn - Manual Payment Error",
                                                                $"Reservation {id}, Error: {ex.Message}",
                                                                Session["User"]?.ToString());
                                                        }
                                                    }
                                                    // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                    reservationDA.CheckInReservation(Convert.ToInt32(id));
                                                }
                                                else
                                                {
                                                    // 🆕 Use manual payment amount from TextBox10
                                                    decimal remain = Convert.ToDecimal(TextBox5.Text) - (DepositAmount);
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "1", "17", "ส่วนลด", "1", "ครั้ง", remain * -1, remain * -1);
                                                    id = Request.QueryString["id"];
                                                    if (CheckBox4.Checked == false)
                                                    {
                                                        // 🏨 Add product charges to receipt
                                                        AddProductChargesToReceipt(Convert.ToInt32(id), dtReserve);

                                                        string receiptId = createReceipt(id, Convert.ToDouble(paymentAmount), dtReserve, IsDeposit, docCreatedDate, CheckBox5.Checked);

                                                        // ✅ Upload slip AFTER createReceipt with Receipt_ID
                                                        uploadSlip(id, receiptId);
                                                    }
                                                    else
                                                    {
                                                        // 🏨 ไม่สร้างใบเสร็จ แต่รับเงินแล้ว → mark charges as PAID
                                                        try
                                                        {
                                                            MarkProductChargesAsPaid(Convert.ToInt32(id), "MANUAL_PAYMENT");

                                                            // ✅ Create Payment_History record (without Receipt_ID)
                                                            try
                                                            {
                                                                decimal depositAmount = Convert.ToDecimal(paymentAmount);
                                                                string paymentMethod = DropDownList2.SelectedItem?.Text ?? "TRANSFER";
                                                                int? adminId = Session["UserID"] != null ? (int?)Convert.ToInt32(Session["UserID"]) : null;
                                                                decimal totalPrice = Convert.ToDecimal(Session["totalPrice"]?.ToString() ?? TextBox4.Text ?? "0");
                                                                string paymentType = depositAmount >= totalPrice ? "FULL" : "DEPOSIT";

                                                                string insertPaymentQuery = @"
                                                                    INSERT INTO [dbo].[Payment_History] (
                                                                        Reservation_ID, PaymentDate, PaymentAmount, PaymentType,
                                                                        PaymentMethod, Receipt_ID, ProcessedBy_AdminID,
                                                                        PaidBy_CustomerPhone, Status, Notes, CreatedDate, UpdatedDate
                                                                    ) OUTPUT INSERTED.ID VALUES (
                                                                        @ReservationId, GETDATE(), @PaymentAmount, @PaymentType,
                                                                        @PaymentMethod, NULL, @AdminId, @CustomerPhone,
                                                                        'COMPLETED', N'เช็คอินพร้อมส่วนลด (ไม่ออกใบเสร็จ)', GETDATE(), GETDATE()
                                                                    )";

                                                                var paymentParams = new Dictionary<string, object>
                                                                {
                                                                    { "@ReservationId", id },
                                                                    { "@PaymentAmount", depositAmount },
                                                                    { "@PaymentType", paymentType },
                                                                    { "@PaymentMethod", paymentMethod },
                                                                    { "@AdminId", adminId ?? (object)DBNull.Value },
                                                                    { "@CustomerPhone", TextBox1.Text }
                                                                };

                                                                DataTable dtPaymentId = code2.DatabaseQuerySafe(conn, insertPaymentQuery, paymentParams);
                                                                if (dtPaymentId.Rows.Count > 0)
                                                                {
                                                                    long paymentHistoryId = Convert.ToInt64(dtPaymentId.Rows[0][0]);
                                                                    Session["PaymentHistoryId"] = paymentHistoryId;
                                                                }
                                                            }
                                                            catch (Exception exPayment)
                                                            {
                                                                code2.Logs(conn, "Reserve CheckIn (Discount) - Payment_History Insert Error",
                                                                    $"Reservation {id}, Error: {exPayment.Message}",
                                                                    Session["User"]?.ToString());
                                                            }

                                                            code2.Logs(conn, "Reserve CheckIn - Manual Payment (With Discount)",
                                                                $"Marked charges as PAID without receipt for Reservation {id}",
                                                                Session["User"]?.ToString());

                                                            // ✅ Upload slip without receipt (NULL Receipt_ID)
                                                            uploadSlip(id, null);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            code2.Logs(conn, "Reserve CheckIn - Manual Payment Error",
                                                                $"Reservation {id}, Error: {ex.Message}",
                                                                Session["User"]?.ToString());
                                                        }
                                                    }
                                                    // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                    reservationDA.CheckInReservation(Convert.ToInt32(id));
                                                }
                                            }
                                            }
                                        }
                                        else
                                        {
                                            // ❌ ไม่ได้ tick checkbox หรือไม่ได้กรอกยอดเงิน - ไม่ทำการเช็คอิน
                                            string alertMessage = "⚠️ ยังไม่ได้ทำการเช็คอิน!\\n\\n" +
                                                                "กรุณาติ๊กเลือก \\'ชำระเงิน\\' และกรอกยอดเงินที่รับ\\n" +
                                                                "จึงจะสามารถเช็คอินได้\\n\\n" +
                                                                "สถานะการจองยังไม่เปลี่ยนแปลง";

                                            ClientScript.RegisterStartupScript(this.GetType(), "checkinWarning",
                                                $"alert('{alertMessage}');", true);

                                            // Log การพยายามเช็คอินโดยไม่ชำระเงิน
                                            try
                                            {
                                                var loggingService = new LoggingService(conn);
                                                loggingService.LogAccountingOperation(
                                                    "CheckInAttemptWithoutPayment",
                                                    $"User attempted to check-in Reservation ID: {id} without payment checkbox. " +
                                                    $"User: {Session["UserName"]?.ToString() ?? "Unknown"}",
                                                    false,
                                                    Session["UserID"] != null ? (int?)Convert.ToInt32(Session["UserID"]) : null,
                                                    Convert.ToInt64(id));
                                            }
                                            catch { }
                                        }

                                        // ✅ Reload page to show uploaded slip image (don't redirect to ReserveTable yet)
                                        // This allows user to see the uploaded slip before completing checkin
                                        if (FileUpload1.HasFile && CheckBox2.Checked)
                                        {
                                            // Reload the same checkin page to show the slip
                                            Response.Redirect($"./Reserve?command=checkin&id={id}&check={TextBox1.Text}", false);
                                            HttpContext.Current.ApplicationInstance.CompleteRequest();
                                        }
                                        else
                                        {
                                            Response.Redirect("/ReserveTable",false);
                                            HttpContext.Current.ApplicationInstance.CompleteRequest();
                                        }
                                    }
                                    else if (command == "reserve")
                                    {
                                        // 🔒 SECURE: Initialize ReservationDataAccess for reserve mode
                                        var reservationDA = new ReservationDataAccess(conn);

                                        try
                                        {
                                            // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                            DateTime? checkinDate = code2.ParseDate(TextBox12.Text);
                                            DateTime now = DateTime.Now;
                                            string reserveBy = Session["permission"].ToString() == "True" ?
                                                Session["UserName"]?.ToString() ?? "User" : "User";

                                            // 🔒 RACE CONDITION PREVENTION: Check room availability before creating reservation
                                            if (checkinDate.HasValue && checkinDate > DateTime.Parse("1999-01-01"))
                                            {
                                                DateTime checkoutDate = checkinDate.Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue));
                                                var availabilityService = new AccommodationAvailabilityService(conn);

                                                // Check each selected room for availability
                                                foreach (GridViewRow row in GridView1.Rows)
                                                {
                                                    CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                                    if (chk != null && chk.Checked)
                                                    {
                                                        int accommodationId = Convert.ToInt32(dtAccommodation.Rows[row.RowIndex]["ID"]);
                                                        string accomName = dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString();

                                                        // ✅ Get requested people count
                                                        int requestedPeople = 1; // Default
                                                        TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);
                                                        if (txtPeopleStay != null && !string.IsNullOrEmpty(txtPeopleStay.Text))
                                                        {
                                                            int.TryParse(txtPeopleStay.Text, out requestedPeople);
                                                        }

                                                        // ✅ Check availability (supports both regular and LimitWithPeople rooms)
                                                        var availabilityResult = availabilityService.CheckAvailability(
                                                            accommodationId,
                                                            accomName,
                                                            checkinDate.Value,
                                                            checkoutDate,
                                                            requestedPeople,
                                                            0 // excludeReservationId = 0 for new reservations
                                                        );

                                                        if (!availabilityResult.IsAvailable)
                                                        {
                                                            // Room is not available!
                                                            ClientScript.RegisterStartupScript(this.GetType(), "roomConflict",
                                                                $"alert('{availabilityResult.ErrorMessage.Replace("'", "\\'")}');", true);

                                                            code2.Logs(conn, "Reservation Conflict - Race Condition Prevented",
                                                                $"Room: {accomName} (ID: {accommodationId}), " +
                                                                $"Requested: {checkinDate.Value:yyyy-MM-dd} to {checkoutDate:yyyy-MM-dd}, " +
                                                                $"People: {requestedPeople}, " +
                                                                $"IsLimitWithPeople: {availabilityResult.IsLimitWithPeople}, " +
                                                                $"Details: {availabilityResult.ConflictDetails ?? "Not available"}",
                                                                Session["User"]?.ToString() ?? "User");

                                                            return; // Stop reservation process
                                                        }
                                                    }
                                                }
                                            }

                                            // ✅ All rooms are available - proceed with reservation
                                            if (checkinDate.HasValue && checkinDate > DateTime.Parse("1999-01-01"))
                                            {
                                                Reservation_ID = reservationDA.InsertNewReservation(
                                                    TextBox1.Text,
                                                    checkinDate.Value,
                                                    checkinDate.Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue)),
                                                    Convert.ToInt32(DropDownList1.SelectedValue),
                                                    "มัดจำแล้ว",
                                                    Convert.ToDecimal(Session["totalPrice"]?.ToString() ?? "0"),
                                                    Convert.ToDecimal(TextBox5.Text ?? "0"),
                                                    TextBox6.Text,
                                                    reserveBy,
                                                    now,
                                                    CheckBox4.Checked,
                                                    CheckBox3.Checked
                                                );
                                            }
                                            else
                                            {
                                                Reservation_ID = reservationDA.InsertNewReservation(
                                                    TextBox1.Text,
                                                    DateTime.Parse("1990-01-01"),
                                                    DateTime.Parse("1990-01-01"),
                                                    Convert.ToInt32(DropDownList1.SelectedValue),
                                                    "มัดจำแล้ว",
                                                    Convert.ToDecimal(Session["totalPrice"]?.ToString() ?? "0"),
                                                    Convert.ToDecimal(TextBox5.Text ?? "0"),
                                                    TextBox6.Text,
                                                    reserveBy,
                                                    now,
                                                    CheckBox4.Checked,
                                                    CheckBox3.Checked
                                                );
                                            }

                                            System.Diagnostics.Debug.WriteLine("Returned Reservation ID: " + Reservation_ID);

                                            if (Reservation_ID <= 0)
                                            {
                                                throw new Exception("Failed to create reservation - returned ID is 0");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            code2.Logs(conn, "Reservation Creation Error", ex.Message + " - " + ex.StackTrace, "SYSTEM");
                                            ClientScript.RegisterStartupScript(this.GetType(), "myalert", $"alert('เกิดข้อผิดพลาดในการสร้างการจอง: {ex.Message}');", true);
                                            return;
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

                                        // 🆕 Record payment to Payment_History if deposit paid
                                        // Only record here if NOT creating receipt (CheckBox4.Checked == true)
                                        // If creating receipt, it will be recorded in createReceipt() with Receipt_ID
                                        if (Reservation_ID > 0 && Convert.ToDecimal(TextBox5.Text ?? "0") > 0 && CheckBox4.Checked)
                                        {
                                            try
                                            {
                                                decimal depositAmount = Convert.ToDecimal(TextBox5.Text ?? "0");
                                                string paymentMethod = DropDownList2.SelectedItem?.Text ?? "CASH";
                                                int? adminId = Session["UserID"] != null ? (int?)Convert.ToInt32(Session["UserID"]) : null;

                                                // Determine payment type
                                                decimal totalPrice = Convert.ToDecimal(Session["totalPrice"]?.ToString() ?? "0");
                                                string paymentType = depositAmount >= totalPrice ? "FULL" : "DEPOSIT";

                                                // Insert Payment_History record (without Receipt_ID, since no receipt created)
                                                string insertPaymentQuery = @"
                                                    INSERT INTO [dbo].[Payment_History] (
                                                        Reservation_ID,
                                                        PaymentDate,
                                                        PaymentAmount,
                                                        PaymentType,
                                                        PaymentMethod,
                                                        ProcessedBy_AdminID,
                                                        PaidBy_CustomerPhone,
                                                        Status,
                                                        Notes,
                                                        CreatedDate,
                                                        UpdatedDate
                                                    ) OUTPUT INSERTED.ID VALUES (
                                                        @ReservationId,
                                                        GETDATE(),
                                                        @PaymentAmount,
                                                        @PaymentType,
                                                        @PaymentMethod,
                                                        @AdminId,
                                                        @CustomerPhone,
                                                        'COMPLETED',
                                                        N'มัดจำเมื่อจอง',
                                                        GETDATE(),
                                                        GETDATE()
                                                    )";

                                                var paymentParams = new Dictionary<string, object>
                                                {
                                                    { "@ReservationId", Reservation_ID },
                                                    { "@PaymentAmount", depositAmount },
                                                    { "@PaymentType", paymentType },
                                                    { "@PaymentMethod", paymentMethod },
                                                    { "@AdminId", adminId ?? (object)DBNull.Value },
                                                    { "@CustomerPhone", TextBox1.Text }
                                                };

                                                DataTable dtPaymentId = code2.DatabaseQuerySafe(conn, insertPaymentQuery, paymentParams);
                                                if (dtPaymentId.Rows.Count > 0)
                                                {
                                                    long paymentHistoryId = Convert.ToInt64(dtPaymentId.Rows[0][0]);
                                                    Session["PaymentHistoryId"] = paymentHistoryId;
                                                    System.Diagnostics.Debug.WriteLine($"Created Payment_History ID: {paymentHistoryId}");
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                code2.Logs(conn, "Payment_History Insert Error", ex.Message + " - " + ex.StackTrace, "SYSTEM");
                                                // Don't fail the whole reservation if payment history fails
                                            }
                                        }

                                        int i = 0;
                                        string msg = "";
                                        int PriceAccom = 0;

                                        foreach (GridViewRow row in GridView1.Rows)
                                        {
                                            CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                                            if (chk != null && chk.Checked)
                                            {
                                                TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);

                                                // ใช้ราคาจาก GridView โดยตรง (ราคาที่ admin แก้ไขแล้ว)
                                                double pricePerNight = Convert.ToDouble(row.Cells[4].Text);
                                                int nights = Convert.ToInt32(DropDownList1.SelectedValue);

                                                if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True")
                                                {
                                                    // ห้องคิดตามจำนวนคน
                                                    int people = Convert.ToInt32(txtPeopleStay.Text);
                                                    double pricePerPiece = pricePerNight * people; // ราคาต่อคืน (รวมทุกคน)
                                                    double totalAmount = TwoDecimalPoints(pricePerPiece * nights); // ราคารวมทุกคืน

                                                    PriceAccom += (int)totalAmount;

                                                    if (Convert.ToDouble(TextBox5.Text) == Convert.ToDouble(TextBox4.Text))
                                                    {
                                                        IsDeposit = false;
                                                        dtReserve.Rows.Add(
                                                            dtReserve.Rows.Count + 1,
                                                            "",
                                                            "1",
                                                            dtAccommodation.Rows[row.RowIndex]["ID"].ToString(),
                                                            dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() +
                                                                " เช็คอิน " + code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy") +
                                                                " เช็คเอ้าท์ " + code2.ParseDate(TextBox12.Text).Value.AddDays(nights).ToString("dd MMMM yyyy") +
                                                                " " + people + " คน",
                                                            nights,
                                                            "คืน",
                                                            pricePerPiece,  // ราคาต่อคืน (รวมทุกคน)
                                                            totalAmount     // ราคารวมทั้งหมด
                                                        );
                                                    }

                                                    msg += "- " + dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() +
                                                           " " + people + " คน " + nights + " คืน\r\n";
                                                }
                                                else
                                                {
                                                    // ห้องไม่คิดตามจำนวนคน
                                                    double totalAmount = TwoDecimalPoints(pricePerNight * nights);

                                                    PriceAccom += (int)totalAmount;

                                                    if (Convert.ToDouble(TextBox5.Text) == Convert.ToDouble(TextBox4.Text))
                                                    {
                                                        IsDeposit = false;
                                                        dtReserve.Rows.Add(
                                                            dtReserve.Rows.Count + 1,
                                                            "",
                                                            "1",
                                                            dtAccommodation.Rows[row.RowIndex]["ID"].ToString(),
                                                            dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() +
                                                                " เช็คอิน " + code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy") +
                                                                " เช็คเอ้าท์ " + code2.ParseDate(TextBox12.Text).Value.AddDays(nights).ToString("dd MMMM yyyy"),
                                                            nights,
                                                            "คืน",
                                                            pricePerNight,  // ราคาต่อคืน
                                                            totalAmount     // ราคารวมทั้งหมด
                                                        );
                                                    }

                                                    msg += "- " + dtAccommodation.Rows[row.RowIndex]["AccomName"].ToString() +
                                                           " " + nights + " คืน\r\n";
                                                }

                                                // บันทึกราคาลงฐานข้อมูล (ใช้ราคาที่แก้ไขแล้ว)
                                                // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                int checkusecoupon = checkAccomUseCoupon(dtAccommodation.Rows[row.RowIndex]["ID"].ToString(),
                                                                                          code2.ParseDate(TextBox12.Text).Value);
                                                reservationDA.InsertReservationAccommodation(
                                                    Reservation_ID,
                                                    Convert.ToInt32(dtAccommodation.Rows[row.RowIndex]["ID"]),
                                                    Convert.ToInt32(txtPeopleStay.Text),
                                                    Convert.ToDecimal(pricePerNight),
                                                    checkusecoupon.ToString()
                                                );
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
                                                    double pricePerPiece = Convert.ToDouble(row.Cells[4].Text);
                                                    double totalAmount = TwoDecimalPoints(pricePerPiece * Convert.ToInt32(DropDownList1.SelectedValue) * Convert.ToInt32(txtAmount.Text));
                                                    dtReserve.Rows.Add(dtReserve.Rows.Count + 1, "", "2", dtItems.Rows[i]["ID"].ToString(),
                                                        dtItems.Rows[i]["ItemName"].ToString() + " เช็คอิน " + code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy") + " เช็คเอ้าท์ " + code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy"),
                                                        txtAmount.Text, dtItems.Rows[i]["Unit"].ToString(),
                                                        pricePerPiece,
                                                        totalAmount);

                                                }
                                                // ✅ FIXED: Use parameterized query to prevent SQL Injection
                                                reservationDA.InsertReservationItem(
                                                    Reservation_ID,
                                                    Convert.ToInt32(dtItems.Rows[i]["ID"]),
                                                    Convert.ToInt32(txtAmount.Text),
                                                    Convert.ToDecimal(Price)
                                                );
                                                try { msg += "- " + dtItems.Rows[i]["ItemName"].ToString() + " " + txtAmount.Text + " ชิ้น"; } catch { }
                                            }
                                            i++;
                                        }
                                        checkCreateCustomer();
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

                                        if (TextBox1.Text != "02" && CheckBox4.Checked == false)
                                        {
                                            if (CheckBox4.Checked == false)
                                            {
                                                string receiptId = createReceipt(ID, Convert.ToDouble(TextBox5.Text), dtReserve, IsDeposit, docCreatedDate, CheckBox5.Checked);

                                                // ✅ Upload slip AFTER createReceipt with Receipt_ID
                                                try
                                                {
                                                    uploadSlip(ID, receiptId);
                                                }
                                                catch (Exception ex)
                                                {
                                                    code2.Logs(conn, "Reserve Mode - Upload Slip Error",
                                                        $"Reservation: {ID}, Receipt: {receiptId}, Error: {ex.Message}",
                                                        Session["User"]?.ToString());
                                                }
                                            }
                                        }
                                        else if (TextBox1.Text != "02" && CheckBox4.Checked == true)
                                        {
                                            // Manual payment - no receipt but slip uploaded
                                            try
                                            {
                                                uploadSlip(ID, null);
                                            }
                                            catch (Exception ex)
                                            {
                                                code2.Logs(conn, "Reserve Mode - Manual Payment Upload Slip Error",
                                                    $"Reservation: {ID}, Error: {ex.Message}",
                                                    Session["User"]?.ToString());
                                            }
                                        }
                                        msg += "\r\nหมายเหตุ:" + TextBox6.Text;

                                        try
                                        {
                                            //code2.SendLineMessageAPI("Ccf82e94eb4f39cc97eaecdeae2edfd16", "ลูกค้าจองห้องพักใหม่หมายเลขการจอง: " + Reservation_ID + "\r\nหมายเลขโทรศัพท์: " + TextBox1.Text + "\r\nเช็คอินวันที่: " + code2.ParseDate(TextBox12.Text).ToString("dd MMMM yyyy") + "\r\nเช็คเอ้าท์วันที่: " + code2.ParseDate(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("dd MMMM yyyy") + "\r\n" + msg, "", "");
                                            //Thread.Sleep(1000);

                                            //                                    using (var client = new HttpClient())
                                            //                                    {
                                            //                                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ConfigurationManager.AppSettings["linechannelaccesstokentaketime"]);
                                            //                                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                                            //                                        var jsonPayload = new
                                            //                                        {
                                            //                                            to = ConfigurationManager.AppSettings["lineuserid"],
                                            //                                            messages = new[]
                                            //                                            {
                                            //    new { type = "text", text = "ลูกค้าจองห้องพักใหม่หมายเลขการจอง: " + Reservation_ID + "\r\nหมายเลขโทรศัพท์: " + TextBox1.Text + "\r\nเช็คอินวันที่: " + code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy") + "\r\nเช็คเอ้าท์วันที่: " + code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("dd MMMM yyyy") + "\r\n" + msg }
                                            //}
                                            //                                        };

                                            //                                        var json = JsonConvert.SerializeObject(jsonPayload);
                                            //                                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                                            //                                        var response = await client.PostAsync("https://api.line.me/v2/bot/message/push", content);
                                            //                                        Console.WriteLine(await response.Content.ReadAsStringAsync());
                                            //                                    }
                                            //                                    Response.Redirect("./Reservation_Confirmed?id=" + ID + "&check=" + TextBox1.Text+"&sendline=ok", false);
                                            //                                    HttpContext.Current.ApplicationInstance.CompleteRequest();
                                            // สำหรับการจองใหม่ (command == "reserve") ให้แทนที่ส่วนส่งข้อความด้วย:
                                            string message = $@"🎉 ═══ การจองใหม่ ═══

📋 หมายเลขการจอง: {Reservation_ID}
👤 ชื่อผู้จอง: {TextBox2.Text}
📞 เบอร์โทร: {TextBox1.Text}

📅 วันที่เข้าพัก: {code2.ParseDate(TextBox12.Text).Value.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("th-TH"))}
📅 วันที่ออก: {code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("th-TH"))}
🌙 จำนวนคืน: {DropDownList1.SelectedValue} คืน

🏨 รายการห้องพัก:
{msg}

💰 ยอดรวมทั้งหมด: {Convert.ToDecimal(TextBox4.Text):N2} บาท
💵 มัดจำ: {Convert.ToDecimal(TextBox5.Text):N2} บาท
💳 ยอดคงเหลือ: {(Convert.ToDecimal(TextBox4.Text) - Convert.ToDecimal(TextBox5.Text)):N2} บาท

{(!string.IsNullOrWhiteSpace(TextBox6.Text) ? $"💬 หมายเหตุ: {TextBox6.Text}\n" : "")}👨‍💼 ลงจองโดย: {Session["UserName"]?.ToString() ?? "System"}
━━━━━━━━━━━━━━━━━";

                                            var bot = new TelegramBot2(ConfigurationSettings.AppSettings["TelegramTokenTakeTime"].ToString());
                                            await bot.SendMessageAsync("-4969611371", message);

                                            // ✅ Reload page to show uploaded slip image (don't redirect to Confirmed yet)
                                            // This allows user to see the uploaded slip before confirming
                                            if (FileUpload1.HasFile)
                                            {
                                                // Reload the same reserve page to show the slip
                                                Response.Redirect($"./Reserve?command=reserve&date={TextBox12.Text}", false);
                                                HttpContext.Current.ApplicationInstance.CompleteRequest();
                                            }
                                            else
                                            {
                                                Response.Redirect("./Reservation_Confirmed?id=" + ID + "&check=" + TextBox1.Text + "&sendline=ok", false);
                                                HttpContext.Current.ApplicationInstance.CompleteRequest();
                                            }
                                        }
                                        catch(Exception ex) {
                                            code2.Logs(conn, "Check Telegram", "Error"+ex, "SYSTEM");

                                            // ✅ Reload page to show uploaded slip image even on Telegram error
                                            if (FileUpload1.HasFile)
                                            {
                                                Response.Redirect($"./Reserve?command=reserve&date={TextBox12.Text}", false);
                                                HttpContext.Current.ApplicationInstance.CompleteRequest();
                                            }
                                            else
                                            {
                                                Response.Redirect("./Reservation_Confirmed?id=" + ID + "&check=" + TextBox1.Text, false);
                                                HttpContext.Current.ApplicationInstance.CompleteRequest();
                                            }
                                        }
                                        //SendLineNotify("ลูกค้าจองห้องพักใหม่หมายเลขการจอง: "+Reservation_ID+"\r\nหมายเลขโทรศัพท์: "+ TextBox1.Text + "\r\nเช็คอินวันที่: " + code2.ParseDate(TextBox12.Text).ToString("dd MMMM yyyy") + "\r\nเช็คเอ้าท์วันที่: " + code2.ParseDate(TextBox12.Text).AddDays(Convert.ToDouble(DropDownList1.SelectedValue)).ToString("dd MMMM yyyy") + "\r\n"+msg);


                                        
                                    }
                                }
                                catch(Exception ex)
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



        public void CouponRecord(string couponcode, string RevID,string AccomID,DateTime reserveday,int priceafterdiscount)
        {
            string coupontype = "";
            try
            {
                coupontype = Session["UseCoupon"].ToString();
            }
            catch
            {

            }
            bool usevoucher = false;
            try
            {
                usevoucher = Convert.ToBoolean(Session["UseVoucher"].ToString());
            }
            catch { }

            if(coupontype == "Affiliate")
            {
                int affiRatePlanID = checkAccomUseCoupon(AccomID, reserveday);
                if (affiRatePlanID > 0)
                {
                    // SECURE: Affiliate member lookup with parameterized query
                    var couponParams = new Dictionary<string, object>
                    {
                        { "@CouponCode", couponcode ?? "" }
                    };

                    DataTable dt = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM [Taketime].[dbo].[Affiliate_Member] " +
                        "INNER JOIN Affiliate_Discount ON Affiliate_Discount.ID = Affiliate_Discount_ID " +
                        "WHERE Coupon_Code = @CouponCode",
                        couponParams);

                    if (dt.Rows.Count > 0)
                    {
                        double commission = 0;
                        double incentivepercent = Convert.ToDouble(dt.Rows[0]["IncentivePercent"].ToString());
                        commission = TwoDecimalPoints((priceafterdiscount * incentivepercent) / 100);

                        // SECURE: INSERT Affiliate_Reservation with parameterized query
                        var insertParams = new Dictionary<string, object>
                        {
                            { "@CouponCode", TextBox19.Text ?? "" },
                            { "@ReservationID", RevID },
                            { "@AccommodationID", AccomID },
                            { "@AffiRatePlanID", affiRatePlanID },
                            { "@PriceAfterDiscount", priceafterdiscount },
                            { "@Commission", commission },
                            { "@StayDate", reserveday }
                        };

                        code.DatabaseInsertSafe(conn,
                            "INSERT INTO [dbo].[Affiliate_Reservation] " +
                            "([Affiliate_Member_Coupon_Code],[Reservation_ID],[Accommodation_ID],[Affiliate_Discount_RatePlan_ID],[PriceAfterDiscount],[Commission],StayDate,[Status]) " +
                            "VALUES (@CouponCode,@ReservationID,@AccommodationID,@AffiRatePlanID,@PriceAfterDiscount,@Commission,@StayDate,'NEW')",
                            insertParams);
                    }
                }
            }
            else if(coupontype == "Voucher" && usevoucher == true)
            {
                string useVoucherAccomID = "";
                try
                {
                    useVoucherAccomID = Session["UseVoucherAccomID"].ToString();
                }
                catch { }
                string[] AccomIDs = useVoucherAccomID.Split(';');
                for(int i = 0;i<AccomIDs.Length;i++)
                {
                    if (AccomIDs[i] == AccomID)
                    {
                        // SECURE: UPDATE Voucher with parameterized query
                        var voucherParams = new Dictionary<string, object>
                        {
                            { "@UsedDate", DateTime.Now },
                            { "@ReservationID", RevID },
                            { "@VoucherNumber", couponcode ?? "" }
                        };

                        code.DatabaseInsertSafe(conn,
                            "UPDATE [dbo].[Voucher] " +
                            "SET [Used_Status] = 'True',[Used_Date] = @UsedDate,[Reservation_ID] = @ReservationID " +
                            "WHERE [Voucher_Number] = @VoucherNumber",
                            voucherParams);
                    }
                }
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

        //public void SendLineNotify(string Message)
        //{
        //    try
        //    {
        //        string lineToken = ConfigurationSettings.AppSettings["linetoken"].ToString();
        //        string message = Message;
        //        int stickerPackageID = 0;
        //        int stickerID = 0;
        //        //string pictureUrl = ConfigurationSettings.AppSettings["prefixurl"].ToString() + HttpContext.Current.Request.Url.Authority + "/" + ConfigurationSettings.AppSettings["virtualprefixpicturepath"].ToString() + "/Images/CheckIn_Display/" + ID + ".jpg";
        //        //string message = HttpUtility.UrlEncode(message, Encoding.UTF8);
        //        var request = (HttpWebRequest)WebRequest.Create(ConfigurationSettings.AppSettings["lineurl"].ToString());
        //        var postData = string.Format("message={0}", message.Replace("*", "x").Replace("\"", ""));

        //        if (stickerPackageID > 0 && stickerID > 0)
        //        {
        //            var stickerPackageId = string.Format("stickerPackageId={0}", stickerPackageID);
        //            var stickerId = string.Format("stickerId={0}", stickerID);
        //            postData += "&" + stickerPackageId.ToString() + "&" + stickerId.ToString();
        //        }
        //        //if (pictureUrl != "")
        //        //{
        //        //    var imageThumbnail = string.Format("imageThumbnail={0}", pictureUrl);
        //        //    var imageFullsize = string.Format("imageFullsize={0}", pictureUrl);
        //        //    postData += "&" + imageThumbnail.ToString() + "&" + imageFullsize.ToString();
        //        //}
        //        var data = Encoding.UTF8.GetBytes(postData);
        //        request.Method = "POST";
        //        request.ContentType = "application/x-www-form-urlencoded";
        //        request.ContentLength = data.Length;
        //        request.Headers.Add("Authorization", "Bearer " + lineToken);
        //        using (var stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);
        //        var response = (HttpWebResponse)request.GetResponse();
        //        var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
        //    }
        //    catch { }
        //}

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

        public void uploadSlip(string reservationID, string receiptID)
        {
            try
            {
                // 🔧 FIX: Always create Payment_History record if uploading slip
                long? paymentHistoryId = null;

                // Check if PaymentHistoryId already exists in session
                if (Session["PaymentHistoryId"] != null)
                {
                    paymentHistoryId = Convert.ToInt64(Session["PaymentHistoryId"]);
                }
                else if (FileUpload1.HasFile && Convert.ToInt32(reservationID) > 0)
                {
                    // No existing PaymentHistoryId → Create new Payment_History record
                    try
                    {
                        decimal depositAmount = CheckBox2.Checked ? Convert.ToDecimal(TextBox10.Text ?? "0") : 0;
                        string paymentMethod = DropDownList2.SelectedItem?.Text ?? "TRANSFER";
                        int? adminId = Session["UserID"] != null ? (int?)Convert.ToInt32(Session["UserID"]) : null;

                        // Determine payment type
                        decimal totalPrice = Convert.ToDecimal(Session["totalPrice"]?.ToString() ?? TextBox4.Text ?? "0");
                        string paymentType = depositAmount >= totalPrice ? "FULL" : "DEPOSIT";

                        // Insert Payment_History record
                        string insertPaymentQuery = @"
                            INSERT INTO [dbo].[Payment_History] (
                                Reservation_ID,
                                PaymentDate,
                                PaymentAmount,
                                PaymentType,
                                PaymentMethod,
                                Account_Receipt_ID,
                                ProcessedBy_AdminID,
                                PaidBy_CustomerPhone,
                                Status,
                                Notes,
                                CreatedDate,
                                UpdatedDate
                            ) OUTPUT INSERTED.ID VALUES (
                                @ReservationId,
                                GETDATE(),
                                @PaymentAmount,
                                @PaymentType,
                                @PaymentMethod,
                                @ReceiptId,
                                @AdminId,
                                @CustomerPhone,
                                'COMPLETED',
                                N'อัพโหลดสลิปการโอนเงิน',
                                GETDATE(),
                                GETDATE()
                            )";

                        var paymentParams = new Dictionary<string, object>
                        {
                            { "@ReservationId", reservationID },
                            { "@PaymentAmount", depositAmount },
                            { "@PaymentType", paymentType },
                            { "@PaymentMethod", paymentMethod },
                            { "@ReceiptId", string.IsNullOrEmpty(receiptID) ? (object)DBNull.Value : receiptID },
                            { "@AdminId", adminId ?? (object)DBNull.Value },
                            { "@CustomerPhone", TextBox1.Text }
                        };

                        DataTable dtPaymentId = code2.DatabaseQuerySafe(conn, insertPaymentQuery, paymentParams);
                        if (dtPaymentId.Rows.Count > 0)
                        {
                            paymentHistoryId = Convert.ToInt64(dtPaymentId.Rows[0][0]);
                            Session["PaymentHistoryId"] = paymentHistoryId;
                            System.Diagnostics.Debug.WriteLine($"✅ Created Payment_History ID: {paymentHistoryId} for slip upload");
                        }
                    }
                    catch (Exception ex)
                    {
                        code2.Logs(conn, "uploadSlip - Payment_History Insert Error",
                            $"Reservation {reservationID}: {ex.Message}", "SYSTEM");
                        // Continue with upload even if Payment_History fails (use timestamp as fallback)
                    }
                }

                // Generate unique filename pattern: {ReservationID}_{Phone}_{PaymentHistoryId}.jpg
                string uniqueSuffix = paymentHistoryId.HasValue ? $"_{paymentHistoryId.Value}" : $"_{DateTime.Now:yyyyMMddHHmmss}";
                string tempFilename = TextBox1.Text + ".jpg";
                string finalFilename = reservationID + "_" + TextBox1.Text + uniqueSuffix + ".jpg";

                if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + tempFilename))
                {
                    try
                    {
                        if (Convert.ToInt32(reservationID) > 0)
                        {
                            string sourcePath = AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + tempFilename;
                            string destPath = AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + finalFilename;

                            // ✅ Delete destination file if exists (to prevent IOException)
                            if (File.Exists(destPath))
                            {
                                try
                                {
                                    File.Delete(destPath);
                                }
                                catch (Exception deleteEx)
                                {
                                    code2.Logs(conn, "uploadSlip - Delete Destination Warning",
                                        $"Reservation {reservationID}: Could not delete existing file: {deleteEx.Message}",
                                        Session["User"]?.ToString() ?? "SYSTEM");
                                }
                            }

                            // Move temp file to final unique filename
                            File.Move(sourcePath, destPath);

                            // ✅ Show uploaded image preview
                            Image1.ImageUrl = "./Upload/Slip/" + finalFilename;
                            Image1.Visible = true;
                            Image1.DataBind();
                        }
                    }
                    catch (Exception moveEx)
                    {
                        code2.Logs(conn, "uploadSlip - File Move Error",
                            $"Reservation {reservationID}: {moveEx.Message}",
                            Session["User"]?.ToString() ?? "SYSTEM");

                        // If move fails, try to at least show the temp file
                        try
                        {
                            Image1.ImageUrl = "./Upload/Slip/" + tempFilename;
                            Image1.Visible = true;
                            Image1.DataBind();
                        }
                        catch { }
                    }

                }
                else
                {
                    if (FileUpload1.HasFile)
                    {
                        try
                        {
                            // 🔒 Validate file extension
                            string fileExtension = Path.GetExtension(FileUpload1.FileName).ToLower();
                            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };

                            if (!allowedExtensions.Contains(fileExtension))
                            {
                                code2.Logs(conn, "uploadSlip - Invalid File Type",
                                    $"Reservation {reservationID}: Attempted to upload {fileExtension}",
                                    Session["User"]?.ToString() ?? "SYSTEM");
                                throw new Exception($"ไม่รองรับไฟล์ประเภท {fileExtension}\nกรุณาอัพโหลด JPG, PNG, GIF หรือ PDF เท่านั้น");
                            }

                            // 🔒 Validate file size (max 10MB)
                            int maxFileSize = 10 * 1024 * 1024; // 10MB
                            if (FileUpload1.PostedFile.ContentLength > maxFileSize)
                            {
                                code2.Logs(conn, "uploadSlip - File Too Large",
                                    $"Reservation {reservationID}: File size {FileUpload1.PostedFile.ContentLength / 1024 / 1024}MB",
                                    Session["User"]?.ToString() ?? "SYSTEM");
                                throw new Exception($"ไฟล์ใหญ่เกินไป ({FileUpload1.PostedFile.ContentLength / 1024 / 1024}MB)\nขนาดสูงสุดที่รองรับคือ 10MB");
                            }

                            // ✅ Ensure Upload/Slip directory exists
                            string uploadDir = Server.MapPath("\\Upload\\Slip");
                            if (!Directory.Exists(uploadDir))
                            {
                                Directory.CreateDirectory(uploadDir);
                            }

                            // Clean up temp file if exists
                            string tempPath = AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + tempFilename;
                            try
                            {
                                if (File.Exists(tempPath))
                                {
                                    File.Delete(tempPath);
                                }
                            }
                            catch (Exception deleteEx)
                            {
                                code2.Logs(conn, "uploadSlip - Delete Temp File Warning",
                                    $"Reservation {reservationID}: Could not delete temp file: {deleteEx.Message}",
                                    Session["User"]?.ToString() ?? "SYSTEM");
                                // Continue even if delete fails
                            }

                            // Save with unique filename
                            string FileSaveWithPath = Server.MapPath("\\Upload\\Slip\\" + finalFilename.Replace("/", "").Replace("\\", "").Replace("'", ""));
                            FileUpload1.SaveAs(FileSaveWithPath);

                            // ✅ Show uploaded image preview
                            Image1.ImageUrl = "./Upload/Slip/" + finalFilename;
                            Image1.Visible = true;
                            Image1.DataBind();
                        }
                        catch (Exception saveEx)
                        {
                            code2.Logs(conn, "uploadSlip - SaveAs Error",
                                $"Reservation {reservationID}: {saveEx.Message}",
                                Session["User"]?.ToString() ?? "SYSTEM");

                            // Show error to user but don't crash
                            System.Diagnostics.Debug.WriteLine($"❌ Upload error: {saveEx.Message}");
                            // Continue processing - slip can be uploaded later
                        }
                    }
                }

                // 🆕 Record Payment_Slip with Receipt_ID
                if (Convert.ToInt32(reservationID) > 0)
                {
                    string slipPath = AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + finalFilename;
                    if (File.Exists(slipPath))
                    {
                        try
                        {
                            string slipFileURL = "Upload/Slip/" + finalFilename;
                            string fileName = finalFilename;
                            FileInfo fileInfo = new FileInfo(slipPath);
                            long fileSize = fileInfo.Length;
                            int? adminId = Session["UserID"] != null ? (int?)Convert.ToInt32(Session["UserID"]) : null;

                            // ✅ Use Receipt_ID from createReceipt() or NULL if manual payment
                            object accountReceiptId = string.IsNullOrEmpty(receiptID) ? (object)DBNull.Value : receiptID;

                            // Insert Payment_Slip record
                            string insertSlipQuery = @"
                                INSERT INTO [dbo].[Payment_Slips] (
                                    Account_Receipt_ID,
                                    Reservation_ID,
                                    SlipFileURL,
                                    FileName,
                                    FileType,
                                    FileSize,
                                    UploadedBy_ID,
                                    UploadedBy_CustomerPhone,
                                    UploadedDate,
                                    VerificationStatus,
                                    IsVerified,
                                    Notes,
                                    IsActive,
                                    Status
                                ) OUTPUT INSERTED.ID VALUES (
                                    @AccountReceiptId,
                                    @ReservationId,
                                    @SlipFileURL,
                                    @FileName,
                                    'image/jpeg',
                                    @FileSize,
                                    @AdminId,
                                    @CustomerPhone,
                                    GETDATE(),
                                    'PENDING',
                                    0,
                                    N'อัพโหลดพร้อมบันทึก',
                                    1,
                                    1
                                )";

                            var slipParams = new Dictionary<string, object>
                            {
                                { "@AccountReceiptId", accountReceiptId },  // ✅ Use Receipt_ID or NULL
                                { "@ReservationId", Convert.ToInt32(reservationID) },
                                { "@SlipFileURL", slipFileURL },
                                { "@FileName", fileName },
                                { "@FileSize", (int)fileSize },
                                { "@AdminId", adminId ?? (object)DBNull.Value },
                                { "@CustomerPhone", TextBox1.Text }
                            };

                            DataTable dtSlipId = code2.DatabaseQuerySafe(conn, insertSlipQuery, slipParams);
                            if (dtSlipId.Rows.Count > 0)
                            {
                                long paymentSlipId = Convert.ToInt64(dtSlipId.Rows[0][0]);

                                // Update Payment_History with PaymentSlip_ID
                                string updatePaymentQuery = @"
                                    UPDATE [dbo].[Payment_History]
                                    SET PaymentSlip_ID = @PaymentSlipId
                                    WHERE ID = @PaymentHistoryId";

                                var updateParams = new Dictionary<string, object>
                                {
                                    { "@PaymentSlipId", paymentSlipId },
                                    { "@PaymentHistoryId", paymentHistoryId }
                                };

                                code2.DatabaseInsertSafe(conn, updatePaymentQuery, updateParams);
                                System.Diagnostics.Debug.WriteLine($"Created Payment_Slip ID: {paymentSlipId}, linked to Payment_History ID: {paymentHistoryId}");

                                // 🆕 Process OCR for uploaded slip
                                try
                                {
                                    ProcessSlipOCR(paymentSlipId, slipPath);
                                }
                                catch (Exception ocrEx)
                                {
                                    code2.Logs(conn, "Reserve OCR Processing Error",
                                        $"SlipID: {paymentSlipId}, Error: {ocrEx.Message}", "SYSTEM");
                                    // Don't fail the upload if OCR fails
                                }
                            }

                            // Clear session after successful save
                            Session.Remove("PaymentHistoryId");
                        }
                        catch (Exception ex)
                        {
                            code2.Logs(conn, "Payment_Slip Insert Error", ex.Message + " - " + ex.StackTrace, "SYSTEM");
                            // Don't fail if slip upload fails
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Process OCR for uploaded slip
        /// </summary>
        private void ProcessSlipOCR(long slipId, string imageFilePath)
        {
            try
            {
                // Check if OCR is enabled
                bool ocrEnabled = ConfigurationManager.AppSettings["OCR_Enabled"] == "true";
                if (!ocrEnabled)
                {
                    return;
                }

                // Get tesseract data path from web.config
                string tessDataPath = ConfigurationManager.AppSettings["TesseractDataPath"] ??
                    Server.MapPath("~/tessdata");

                var ocrService = new Take_Time_BangPhra.Services.SlipOCRService(tessDataPath, conn);
                var ocrResult = ocrService.ProcessSlip(imageFilePath);

                // Save OCR result to database
                ocrService.SaveOCRResult(slipId, ocrResult);

                // Log result for monitoring
                string logMessage = ocrResult.Success
                    ? $"OCR Success - Amount: {ocrResult.Amount:N2}, Confidence: {ocrResult.Confidence:N2}%"
                    : $"OCR Failed - {ocrResult.ErrorMessage}";

                code2.Logs(conn, "Reserve OCR Processing",
                    $"SlipID: {slipId}, {logMessage}", "SYSTEM");
            }
            catch (Exception ex)
            {
                // Update slip with error status
                var errorParams = new Dictionary<string, object>
                {
                    { "@slipId", slipId },
                    { "@errorMessage", ex.Message },
                    { "@status", "FAILED" }
                };

                // Note: Payment_Slips table does NOT have OCR columns
                // Just log the error
                code2.Logs(conn, "Reserve ProcessSlipOCR Error",
                    $"SlipID: {slipId}, Error: {ex.Message}", "SYSTEM");
            }
        }

        // ✨ MIGRATED: cleantext() has been replaced with ValidationHelper.CleanText()
        // See ValidationHelper class in Take_Time_BangPhra.Class namespace

        public string createReceipt(string Reservation_ID, double Total_Amount,DataTable dtReserve,bool IsDeposit,DateTime docDate,bool etax)
        {
            string status = "Normal";
            string ReceiptID = "";
            if (Total_Amount > 0)
            {
                ReceiptID = _documentHelper.CreateDocumentNumber("Account_Receipt", "REC", docDate);
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
                string customerId = "0";
                try
                {
                    // SECURE: Customer lookup with parameterized query
                    var customerParams = new Dictionary<string, object>
                    {
                        { "@ReservationID", Reservation_ID }
                    };

                    DataTable dtCustomer = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Customer " +
                        "INNER JOIN Reservation ON MobilePhone = Customer_MobilePhone " +
                        "WHERE Reservation.ID = @ReservationID",
                        customerParams);

                    if(dtCustomer.Rows.Count > 0)
                    {
                        customerId = dtCustomer.Rows[0]["ID"].ToString();
                    }
                }
                catch
                {

                }

                // ปรับสัดส่วนรายละเอียดให้ตรงกับยอดรวมก่อนบันทึก
                if (!IsDeposit)
                {
                    AdjustReserveDataToMatch(dtReserve, Total_Amount, Reservation_ID);
                }

                if (IsDeposit == true)
                {
                    code.DatabaseInsert(conn,
                        "INSERT INTO [dbo].[Account_Receipt] " +
                        "(ID,[Reservation_ID],[Created_Date],[Total_Amount],[Vat]," +
                        "[Total_Amount_Exclude_Vat],[IsDeposit],[UseDeposit],Status,Paid_Type," +
                        "Created_By_ID,Etax,Customer_ID) " +
                        "VALUES ('" + ReceiptID + "','" + Reservation_ID + "'," +
                        // Use InvariantCulture to ensure Christian year (2025), NOT Buddhist year (2568)
                        "'" + docDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "'," +
                        Total_Amount + "," + Vat + "," + PriceExcludeVat + "," +
                        "'True','False','Normal',N'" + DropDownList2.SelectedItem.Text + "'," +
                        "N'" + created_By_ID + "','" + CheckBox5.Checked + "','" + customerId + "');");

                    code.DatabaseInsert(conn,
                        "INSERT INTO [dbo].[Account_Receipt_Detail] " +
                        "([Number],[Receipt_ID],[ProductType_ID],[Product_ID]," +
                        "[Product_Data],[Product_Amount],[Product_Unit]," +
                        "[Price_PerPeice],[Price_Amount]) " +
                        "Values ('1','" + ReceiptID + "',1,7," +
                        "N'ค่ามัดจำที่พักของหมายเลขการจอง " + Reservation_ID + " [" + ReceiptID + "]'," +
                        "'1',N'ครั้ง'," + Total_Amount + "," + Total_Amount + ")");
                }
                else
                {
                    code.DatabaseInsert(conn,
                        "INSERT INTO [dbo].[Account_Receipt] " +
                        "(ID,[Reservation_ID],[Created_Date],[Total_Amount],[Vat]," +
                        "[Total_Amount_Exclude_Vat],[IsDeposit],[UseDeposit],Status,Paid_Type," +
                        "Created_By_ID,Etax,Customer_ID) " +
                        "VALUES ('" + ReceiptID + "','" + Reservation_ID + "'," +
                        // Use InvariantCulture to ensure Christian year (2025), NOT Buddhist year (2568)
                        "'" + docDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "'," +
                        Total_Amount + "," + Vat + "," + PriceExcludeVat + "," +
                        "'False','False','Normal',N'" + DropDownList2.SelectedItem.Text + "'," +
                        "N'" + created_By_ID + "','" + CheckBox5.Checked + "','" + customerId + "');");

                    // เพิ่ม Receipt Detail โดยตรวจสอบความถูกต้องก่อน
                    double receiptTotal = 0;

                    for (int i = 0; i < dtReserve.Rows.Count; i++)
                    {
                        double pricePerPiece = Convert.ToDouble(dtReserve.Rows[i]["Price_PerPeice"]);
                        double productAmount = Convert.ToDouble(dtReserve.Rows[i]["Product_Amount"]);

                        // คำนวณ Price_Amount ใหม่ให้แน่ใจว่าถูกต้อง
                        double calculatedAmount = TwoDecimalPoints(pricePerPiece * productAmount);
                        receiptTotal += calculatedAmount;

                        code.DatabaseInsert(conn,
                            "INSERT INTO [dbo].[Account_Receipt_Detail] " +
                            "([Number],[Receipt_ID],[ProductType_ID],[Product_ID]," +
                            "[Product_Data],[Product_Amount],[Product_Unit]," +
                            "[Price_PerPeice],[Price_Amount]) " +
                            "Values ('" + dtReserve.Rows[i]["Number"].ToString() + "'," +
                            "'" + ReceiptID + "'," +
                            dtReserve.Rows[i]["ProductType_ID"].ToString() + "," +
                            dtReserve.Rows[i]["Product_ID"].ToString() + "," +
                            "N'" + dtReserve.Rows[i]["Product_Data"].ToString() + "'," +
                            dtReserve.Rows[i]["Product_Amount"].ToString() + "," +
                            "N'" + dtReserve.Rows[i]["Product_Unit"].ToString() + "'," +
                            pricePerPiece.ToString() + "," +
                            calculatedAmount.ToString() + ")");
                    }

                    // ตรวจสอบว่ายอดรวมตรงกัน
                    if (Math.Abs(receiptTotal - Total_Amount) > 0.01)
                    {
                        code2.Logs(conn, "Receipt Total Mismatch",
                            $"Receipt {ReceiptID}: Expected {Total_Amount}, Calculated {receiptTotal}",
                            "SYSTEM");
                    }
                }

                // 🆕 Record payment to Payment_History when receipt is created
                try
                {
                    string paymentType = IsDeposit ? "DEPOSIT" : "FULL";
                    string paymentMethod = DropDownList2.SelectedItem?.Text ?? "CASH";
                    string paymentNotes = IsDeposit ? "มัดจำ - ออกใบกำกับภาษี" : "ชำระเต็ม - ออกใบกำกับภาษี";

                    int? adminId = null;
                    if (!string.IsNullOrEmpty(created_By_ID) && created_By_ID != "0")
                    {
                        adminId = Convert.ToInt32(created_By_ID);
                    }

                    // Get customer phone from reservation
                    string customerPhone = "";
                    try
                    {
                        // ✅ FIXED: Use parameterized query to prevent SQL Injection
                        var phoneParams = new Dictionary<string, object>
                        {
                            { "@ReservationID", Reservation_ID }
                        };
                        DataTable dtPhone = code2.DatabaseQuerySafe(conn,
                            "SELECT Customer_MobilePhone FROM Reservation WHERE ID = @ReservationID",
                            phoneParams);
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
                        { "@ReservationId", Reservation_ID },
                        { "@PaymentDate", DateTime.Now },  // Use actual payment date (today), not docDate (checkin date)
                        { "@PaymentAmount", (decimal)Total_Amount },
                        { "@PaymentType", paymentType },
                        { "@PaymentMethod", paymentMethod },
                        { "@ReceiptId", ReceiptID },
                        { "@AdminId", adminId ?? (object)DBNull.Value },
                        { "@CustomerPhone", customerPhone },
                        { "@Notes", paymentNotes }
                    };

                    code2.DatabaseInsertSafe(conn, insertPaymentQuery, paymentParams);
                    System.Diagnostics.Debug.WriteLine($"Created Payment_History for Receipt: {ReceiptID}");
                }
                catch (Exception ex)
                {
                    code2.Logs(conn, "Payment_History Insert Error (createReceipt)",
                        ex.Message + " - " + ex.StackTrace, "SYSTEM");
                    // Don't fail receipt creation if payment history fails
                }

                if (CheckBox4.Checked == false)
                {
                    createReport(ReceiptID, status, docDate);
                }

                if (etax == true)
                {
                    // SECURE: Receipt lookup with parameterized query
                    var receiptParams = new Dictionary<string, object>
                    {
                        { "@ReservationID", Reservation_ID }
                    };

                    DataTable dtReceipt = code.DatabaseQuerySafe(conn,
                        "SELECT [ID],[UID] FROM [Account_Receipt] " +
                        "WHERE RESERVATION_ID = @ReservationID " +
                        "ORDER BY ID DESC",
                        receiptParams);

                    string uid = dtReceipt.Rows[0]["UID"].ToString();
                    string path = System.Configuration.ConfigurationSettings.AppSettings["ReceiptFolderPath"].ToString();
                    string pdfpath = "";
                    if (File.Exists(path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + dtReceipt.Rows[0]["ID"].ToString() + "_" + uid + "_etax.pdf"))
                    {
                        pdfpath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + dtReceipt.Rows[0]["ID"].ToString() + "_" + uid + "_etax.pdf";
                    }
                    else 
                    {
                        pdfpath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + dtReceipt.Rows[0]["ID"].ToString() + "_etax.pdf";
                    }

                       

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

                // 🏨 Mark product charges as paid
                if (_roomChargeService != null)
                {
                    MarkProductChargesAsPaid(Convert.ToInt32(Reservation_ID), ReceiptID);
                }
            }

            return ReceiptID;  // ✅ Return Receipt_ID so uploadSlip can use it
        }

        private bool ValidateReceiptDetails(DataTable dtReserve, double expectedTotal)
        {
            double calculatedTotal = 0;

            foreach (DataRow row in dtReserve.Rows)
            {
                double pricePerPiece = Convert.ToDouble(row["Price_PerPeice"]);
                double productAmount = Convert.ToDouble(row["Product_Amount"]);
                calculatedTotal += TwoDecimalPoints(pricePerPiece * productAmount);
            }

            // ตรวจสอบว่าผลรวมตรงกัน (ให้ผิดพลาดได้ 0.01 บาท)
            return Math.Abs(calculatedTotal - expectedTotal) <= 0.01;
        }
        private double CalculateTotalFromReserve(DataTable dtReserve)
        {
            double total = 0;
            foreach (DataRow row in dtReserve.Rows)
            {
                total += Convert.ToDouble(row["Price_Amount"]);
            }
            return TwoDecimalPoints(total);
        }

        /// <summary>
        /// ปรับสัดส่วนรายละเอียดใน dtReserve ให้ตรงกับยอดรวมที่กำหนด
        /// ใช้สำหรับกรณีที่มีการแก้ไขราคาห้องพัก แต่รายละเอียดยังเป็นราคาเดิม
        /// </summary>
        private void AdjustReserveDataToMatch(DataTable dtReserve, double expectedTotal, string reservationId)
        {
            if (dtReserve == null || dtReserve.Rows.Count == 0)
                return;

            // 🏨 คำนวณยอดรวมโดยข้ามสินค้าชาร์จ (ProductType_ID = 3)
            // เพราะสินค้าชาร์จมีราคาแน่นอน ไม่ควรถูก adjust
            double currentTotal = 0;
            double productChargesTotal = 0;
            foreach (DataRow row in dtReserve.Rows)
            {
                double amount = Convert.ToDouble(row["Price_Amount"]);
                string productTypeId = row["ProductType_ID"].ToString();

                if (productTypeId == "3")
                {
                    // สินค้าชาร์จ - เก็บยอดแยก
                    productChargesTotal += amount;
                }
                else
                {
                    // ห้องพัก + อุปกรณ์เช่า - ใช้คำนวณ adjustment
                    currentTotal += amount;
                }
            }

            // ลบยอดสินค้าชาร์จออกจาก expectedTotal
            double expectedTotalExcludingCharges = expectedTotal - productChargesTotal;

            // ถ้ายอดตรงกันอยู่แล้ว (ผิดพลาดไม่เกิน 0.5 บาท) ไม่ต้องปรับ
            if (Math.Abs(currentTotal - expectedTotalExcludingCharges) <= 0.5)
                return;

            // คำนวณอัตราส่วนการปรับ
            double adjustmentRatio = expectedTotalExcludingCharges / currentTotal;

            // Log การปรับสัดส่วน
            code2.Logs(conn, "Receipt Amount Adjustment",
                $"Reservation {reservationId}: Adjusting details from {currentTotal:F2} to {expectedTotalExcludingCharges:F2} (ratio: {adjustmentRatio:F4}), Product Charges: {productChargesTotal:F2} (excluded from adjustment)",
                "SYSTEM");

            double adjustedTotal = 0;
            int lastAdjustableIndex = -1;

            // ปรับแถวที่ไม่ใช่สินค้าชาร์จตามอัตราส่วน
            for (int i = 0; i < dtReserve.Rows.Count; i++)
            {
                DataRow row = dtReserve.Rows[i];
                string productTypeId = row["ProductType_ID"].ToString();

                // 🏨 ข้ามสินค้าชาร์จ (ProductType_ID = 3) ไม่ adjust
                if (productTypeId == "3")
                {
                    continue;
                }

                double originalPricePerPiece = Convert.ToDouble(row["Price_PerPeice"]);
                double originalPriceAmount = Convert.ToDouble(row["Price_Amount"]);

                // ปรับราคาต่อหน่วยและราคารวมตามอัตราส่วน
                double adjustedPricePerPiece = TwoDecimalPoints(originalPricePerPiece * adjustmentRatio);
                double adjustedPriceAmount = TwoDecimalPoints(originalPriceAmount * adjustmentRatio);

                row["Price_PerPeice"] = adjustedPricePerPiece;
                row["Price_Amount"] = adjustedPriceAmount;

                adjustedTotal += adjustedPriceAmount;
                lastAdjustableIndex = i; // Track last adjustable row
            }

            // ปรับส่วนต่างจากการปัดเศษให้กับรายการสุดท้ายที่สามารถ adjust ได้
            double difference = TwoDecimalPoints(expectedTotalExcludingCharges - adjustedTotal);
            if (Math.Abs(difference) > 0.01 && lastAdjustableIndex >= 0)
            {
                DataRow lastRow = dtReserve.Rows[lastAdjustableIndex];
                double lastPriceAmount = Convert.ToDouble(lastRow["Price_Amount"]);
                lastRow["Price_Amount"] = TwoDecimalPoints(lastPriceAmount + difference);

                // ปรับ Price_PerPeice ของรายการสุดท้ายด้วย
                double productAmount = Convert.ToDouble(lastRow["Product_Amount"]);
                if (productAmount > 0)
                {
                    lastRow["Price_PerPeice"] = TwoDecimalPoints((lastPriceAmount + difference) / productAmount);
                }
            }

            // ✅ VALIDATE: ตรวจสอบว่ายอดรวมทั้งหมด (รวม product charges) ตรงกับ expectedTotal
            double finalTotal = 0;
            foreach (DataRow row in dtReserve.Rows)
            {
                finalTotal += Convert.ToDouble(row["Price_Amount"]);
            }
            double totalDifference = TwoDecimalPoints(expectedTotal - finalTotal);
            if (Math.Abs(totalDifference) > 0.5)
            {
                code2.Logs(conn, "Receipt Adjustment Warning",
                    $"Reservation {reservationId}: Final total mismatch! Expected: {expectedTotal:F2}, Actual: {finalTotal:F2}, Difference: {totalDifference:F2}",
                    "SYSTEM");
            }
        }

        private bool ValidateReserveData(DataTable dtReserve, double expectedTotal)
        {
            double calculatedTotal = 0;

            foreach (DataRow row in dtReserve.Rows)
            {
                double pricePerPiece = Convert.ToDouble(row["Price_PerPeice"]);
                double productAmount = Convert.ToDouble(row["Product_Amount"]);
                double priceAmount = Convert.ToDouble(row["Price_Amount"]);

                double calculatedAmount = TwoDecimalPoints(pricePerPiece * productAmount);

                // ตรวจสอบว่า Price_Amount ถูกคำนวณถูกต้อง
                if (Math.Abs(priceAmount - calculatedAmount) > 0.01) // 容许误差 0.01
                {
                    // แก้ไขค่าให้ถูกต้อง
                    row["Price_Amount"] = calculatedAmount;
                }

                calculatedTotal += calculatedAmount;
            }

            // ตรวจสอบว่ายอดรวมตรงกับที่คาดหวัง
            return Math.Abs(calculatedTotal - expectedTotal) <= 0.01;
        }
        public void checkCreateCustomer()
        {
            // ✅ Validate Customer_Type_ID
            int customerTypeId = 1; // Default to 1 (Individual)
            if (!string.IsNullOrEmpty(DropDownList8.SelectedValue))
            {
                int.TryParse(DropDownList8.SelectedValue, out customerTypeId);
                if (customerTypeId == 0) customerTypeId = 1; // Fallback to 1 if parse failed
            }

            // ✅ Validate Address_ID
            int addressId = 0;
            try
            {
                string addressIdString = _addressHelper.GetAddressIdString(
                    TextBox16.Text,
                    DropDownList5.SelectedItem?.Text ?? "",
                    DropDownList6.SelectedItem?.Text ?? "",
                    DropDownList7.SelectedItem?.Text ?? ""
                );

                if (!string.IsNullOrEmpty(addressIdString))
                {
                    int.TryParse(addressIdString, out addressId);
                }
            }
            catch
            {
                addressId = 0; // Fallback to 0 if any error
            }

            // Upsert customer data (insert or update) - ensures no duplicates and always latest data
            // ALWAYS matches by MobilePhone - ensures only 1 record per phone number
            // If customer type changes from Individual to Corporate (or vice versa), it updates the existing record
            code.UpsertCustomer(
                conn,
                TextBox1.Text,  // MobilePhone
                TextBox2.Text,  // Name
                TextBox3.Text,  // NickName
                "",  // ComeFrom
                "",  // Remark
                TextBox2.Text,  // FullName
                ValidationHelper.CleanText(TextBox8.Text),  // Address
                TextBox9.Text,  // IDNumber
                TextBox13.Text,  // Email
                customerTypeId,  // Customer_Type_ID (validated)
                addressId,  // Address_ID (validated)
                TextBox17.Text,  // Address1
                TextBox18.Text  // Branch_Number
            );
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
            DataTable dtbusinessinfo = code.DatabaseQuery(conn, @"SELECT
                -- ⚠️ ระบุ Business_Info columns ทั้งหมด
                Business_Info.ID AS BusinessInfo_ID,
                Business_Info.Business_Type_ID,
                Business_Info.Company_Name,
                Business_Info.Address,
                Business_Info.Address_ID,
                Business_Info.Email,
                Business_Info.LegalEntity_Number,
                Business_Info.Branch_Number,
                Business_Info.Phone_Number,
                Business_Info.Use_Vat,
                Business_Info.Status,
                Business_Info.Address1,
                Customer_Type.Customer_Type,
                Customer_Type.Customer_Code,
                -- ✅ ดึงข้อมูลที่อยู่จาก Address table
                Address.Province AS Province,
                Address.District AS District,
                Address.SubDistrict AS SubDistrict,
                Address.PostalCode AS PostalCode,
                Address.Address_Code
                FROM Business_Info
                LEFT JOIN Customer_Type ON Business_Type_ID = Customer_Type.ID
                LEFT JOIN Address ON Address.ID = Business_Info.Address_ID");

            // SECURE: Receipt detail lookup with parameterized query
            var receiptDetailParams = new Dictionary<string, object>
            {
                { "@ReceiptID", RecNumber }
            };

            DataTable dtReceiptDetail = code.DatabaseQuerySafe(conn,
                "SELECT * FROM [Account_Receipt_Detail] " +
                "INNER JOIN Account_ProductType ON Account_ProductType.ID = ProductType_ID " +
                "WHERE Receipt_ID = @ReceiptID " +
                "ORDER BY Number ASC",
                receiptDetailParams);

            // SECURE: Receipt lookup with parameterized query
            var receiptParams = new Dictionary<string, object>
            {
                { "@ReceiptID", RecNumber }
            };

            DataTable dtReceipt = code.DatabaseQuerySafe(conn,
                "SELECT * FROM [Account_Receipt] " +
                "LEFT JOIN Reservation ON Reservation.ID = Reservation_ID " +
                "WHERE Account_Receipt.ID = @ReceiptID",
                receiptParams);

            string uid = dtReceipt.Rows[0]["UID"].ToString();

            // SECURE: Customer lookup with parameterized query
            var customerParams = new Dictionary<string, object>
            {
                { "@MobilePhone", dtReceipt.Rows[0]["Customer_MobilePhone"].ToString() }
            };

            DataTable dtcustomer = code.DatabaseQuerySafe(conn, @"SELECT
                -- ⚠️ ระบุ Customer columns ทั้งหมด ยกเว้น Province, District, Subdistrict, Postcode (legacy columns)
                Customer.ID,
                Customer.MobilePhone,
                Customer.Name,
                Customer.NickName,
                Customer.ComeFrom,
                Customer.Remark,
                Customer.Status,
                Customer.FullName,
                Customer.Address,
                Customer.Address1,
                Customer.Address_ID,
                Customer.IDNumber,
                Customer.Email,
                Customer.Customer_Type_ID,
                Customer.Branch_Number,
                Customer.TaxID,
                Customer.LastUpdated,
                Customer.LastUpdatedBy_ID,
                Customer.CreatedDate,
                Customer.CreatedBy_ID,
                Customer.IsActive,
                Customer_Type.Customer_Type,
                Customer_Type.Customer_Code,
                -- ✅ ดึงข้อมูลที่อยู่จาก Address table (ไม่ใช่จาก Customer legacy columns)
                Address.Province AS Province,
                Address.District AS District,
                Address.SubDistrict AS SubDistrict,
                Address.PostalCode AS PostalCode,
                Address.Address_Code
                FROM Customer
                LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID
                LEFT JOIN Address ON Address.ID = Customer.Address_ID
                WHERE MobilePhone = @MobilePhone", customerParams);

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
                // SECURE: Admin lookup with parameterized query
                var adminParams = new Dictionary<string, object>
                {
                    { "@UserID", Session["UserID"].ToString() }
                };

                dtCreator = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Admin WHERE ID = @UserID",
                    adminParams);

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
            uid = dtReceipt.Rows[0]["UID"].ToString();
            string pdfpath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + DocNumber +"_"+uid+ ".pdf";

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
                    uid = dtReceipt.Rows[0]["UID"].ToString();
                    string xmlFilePath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + DocNumber +"_"+uid+ ".xml";
                    string xmlString = System.IO.File.ReadAllText(ConfigurationSettings.AppSettings["BaseFolderPath"].ToString() + "\\Resources\\template.xml");
                    xmlString = xmlString.Replace("*invoice_id", DocNumber);
                    xmlString = xmlString.Replace("*invoice_name", "ใบเสร็จรับเงิน/ใบกำกับภาษี");
                    xmlString = xmlString.Replace("*invoice_typecode", "T03");
                    // Use InvariantCulture to ensure Christian year (2025), NOT Buddhist year (2568)
                    xmlString = xmlString.Replace("*invoice_issue_date", code2.ParseDate(dtReceipt.Rows[0]["Created_Date"].ToString()).Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "T00:00:00.000");
                    xmlString = xmlString.Replace("*invoice_purpose", "");
                    xmlString = xmlString.Replace("*invoice_Purpose_code", "");
                    xmlString = xmlString.Replace("*invoice_create_date", code2.ParseDate(dtReceipt.Rows[0]["Created_Date"].ToString()).Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "T00:00:00.000");
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
                        string pdfFilePath = path + "\\" + docDate.Year.ToString()+ "\\" + docDate.Month.ToString() + "\\" + DocNumber +"_"+uid+ ".pdf";
                        string xmlFilePath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + DocNumber + "_"+uid+".xml";

                        string xmlFileName = "ETDA-invoice.xml";


                        string xmlVersion = "1.0";
                        string documentID = DocNumber;
                        string documentOID = "";

                        string outputPath = path + "\\" + docDate.Year.ToString() + "\\" + docDate.Month.ToString() + "\\" + DocNumber +"_"+uid+ "_etax.pdf";

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
            DataTable dtCustomer = code.DatabaseQuery(conn, @"SELECT
                -- ⚠️ ระบุ Customer columns ทั้งหมด ยกเว้น Province, District, Subdistrict, Postcode (legacy columns)
                Customer.ID,
                Customer.MobilePhone,
                Customer.Name,
                Customer.NickName,
                Customer.ComeFrom,
                Customer.Remark,
                Customer.Status,
                Customer.FullName,
                Customer.Address,
                Customer.Address1,
                Customer.Address_ID,
                Customer.IDNumber,
                Customer.Email,
                Customer.Customer_Type_ID,
                Customer.Branch_Number,
                Customer.TaxID,
                Customer.LastUpdated,
                Customer.LastUpdatedBy_ID,
                Customer.CreatedDate,
                Customer.CreatedBy_ID,
                Customer.IsActive,
                Customer_Type.Customer_Type,
                Customer_Type.Customer_Code,
                -- ✅ ดึงข้อมูลที่อยู่จาก Address table (ไม่ใช่จาก Customer legacy columns)
                Address.Province AS Province,
                Address.District AS District,
                Address.SubDistrict AS SubDistrict,
                Address.PostalCode AS PostalCode,
                Address.Address_Code
                FROM [Customer]
                LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID
                LEFT JOIN Address ON Address.ID = Customer.Address_ID
                WHERE MobilePhone = '" + TextBox1.Text + "'");
            
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

                    // SECURE: Customer visit count with parameterized query
                    var visitParams = new Dictionary<string, object>
                    {
                        { "@MobilePhone", TextBox1.Text ?? "" }
                    };

                    DataTable dtVisitCount = code.DatabaseQuerySafe(conn,
                        "SELECT COUNT([Customer_MobilePhone]) AS CountReserved " +
                        "FROM [Reservation] " +
                        "WHERE Customer_MobilePhone = @MobilePhone AND Status = N'เช็คอินแล้ว'",
                        visitParams);

                    Button5.Text = "เคยมาแล้ว " + dtVisitCount.Rows[0][0].ToString() + " ครั้ง";
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
            // SECURE: Reservation calendar lookup with parameterized query
            var calendarParams = new Dictionary<string, object>
            {
                { "@SelectedDate", e.Day.Date }
            };

            DataTable dtReservation = code.DatabaseQuerySafe(conn,
                "SELECT * FROM Reservation " +
                "RIGHT JOIN Reservation_Accommodation ON Reservation.ID = Reservation_Accommodation.Reservation_ID " +
                "WHERE @SelectedDate >= CheckinDate AND @SelectedDate < CheckoutDate",
                calendarParams);

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
                e.Cell.ForeColor = System.Drawing.ColorTranslator.FromHtml("#BC8F8F");
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
                    try
                    {
                        string phone = TextBox1.Text.Trim();

                        // ✅ Save as TEMP file: {phone}.jpg
                        // This will be moved to final filename when Button1 is clicked
                        string tempFilename = phone + ".jpg";

                        // Delete old temp file if exists
                        string tempPath = AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + tempFilename;
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }

                        // Save as temp file
                        string FileSaveWithPath = Server.MapPath("\\Upload\\Slip\\" + tempFilename);
                        FileUpload1.SaveAs(FileSaveWithPath);

                        // ✅ Show uploaded image immediately (from temp file)
                        Image1.ImageUrl = "./Upload/Slip/" + tempFilename;
                        Image1.Visible = true;
                        Image1.DataBind();

                        // ✅ Show success message
                        ClientScript.RegisterStartupScript(this.GetType(), "uploadSuccess",
                            "alert('✅ อัพโหลดสลิปสำเร็จ!\\n\\n📎 ไฟล์: " + FileUpload1.FileName.Replace("'", "\\'") + "\\n✅ แสดงรูปด้านล่างแล้ว\\n\\nกด \"บันทึก\" เพื่อบันทึกข้อมูล');", true);
                    }
                    catch (Exception ex)
                    {
                        ClientScript.RegisterStartupScript(this.GetType(), "uploadError",
                            $"alert('❌ เกิดข้อผิดพลาด: {ex.Message.Replace("'", "\\'")}');", true);

                        code2.Logs(conn, "Button3_Click Error",
                            $"Phone: {TextBox1.Text}, Error: {ex.Message}",
                            Session["User"]?.ToString() ?? "SYSTEM");
                    }
                }
                else
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเลือกไฟล์ที่ต้องการจะอัพโหลดก่อน');", true);
                }
            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาระบุเบอร์โทรศัพท์ก่อน');", true);
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

            // อัพเดทราคาใหม่
            dtAccom.Rows[Convert.ToInt32(e.RowIndex)]["Price"] = txtPrice.Text;

            // คำนวณยอดรวมใหม่ทันที
            RecalculateTotalPrice();

            GridView1.DataSource = dtAccom;
            GridView1.DataBind();
        }

        private void RecalculateTotalPrice()
        {
            DataTable dtAccommodation = (DataTable)Session["dtAccommodation"];
            DataTable dtItems = (DataTable)Session["dtItems"];

            double totalPrice = 0;
            double PriceAccom = 0;
            int PriceItems = 0;
            double PendingCharges = 0;

            // คำนวณราคาห้องพัก
            foreach (GridViewRow row in GridView1.Rows)
            {
                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                if (chk != null && chk.Checked)
                {
                    TextBox txtPeopleStay = (row.Cells[2].FindControl("txtPeopleStay") as TextBox);

                    if (dtAccommodation.Rows[row.RowIndex]["LimitWithPeople"].ToString() == "True")
                    {
                        PriceAccom += Convert.ToDouble(txtPeopleStay.Text) * Convert.ToDouble(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue);
                    }
                    else
                    {
                        PriceAccom += Convert.ToDouble(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue);
                    }
                }
            }

            // คำนวณราคาของเช่า
            foreach (GridViewRow row in GridView2.Rows)
            {
                CheckBox chk = (row.Cells[0].FindControl("chkSelect") as CheckBox);
                if (chk != null && chk.Checked)
                {
                    TextBox txtAmount = (row.Cells[2].FindControl("txtAmount") as TextBox);
                    PriceItems += Convert.ToInt32(txtAmount.Text) * Convert.ToInt32(row.Cells[4].Text) * Convert.ToInt32(DropDownList1.SelectedValue);
                }
            }

            // 🏨 รวมสินค้าค้างชำระ (Pending Product Charges)
            try
            {
                string command = Request.QueryString["command"];
                string id = Request.QueryString["id"];

                if ((command == "edit" || command == "checkin" || command == "rentmore") && !string.IsNullOrEmpty(id))
                {
                    int reservationId = Convert.ToInt32(id);
                    decimal pendingCharges = _roomChargeDA.GetTotalPendingCharges(reservationId);
                    PendingCharges = Convert.ToDouble(pendingCharges);
                }
            }
            catch
            {
                // If error, pending charges = 0 (don't break the calculation)
                PendingCharges = 0;
            }

            totalPrice = PriceAccom + PriceItems + PendingCharges;
            Session["totalPrice"] = totalPrice;
            Session["PriceAccom"] = PriceAccom;
            Session["PriceItems"] = PriceItems;
            Session["PendingCharges"] = PendingCharges;

            TextBox4.Text = totalPrice.ToString();
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
            // CheckBox1 is for accepting terms and conditions on first-time booking
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
            string command = Request.QueryString["command"];

            if(CheckBox2.Checked == true)
            {
                TextBox10.Visible = true;

                // 🆕 For CheckIn mode: Lock payment to remaining amount only
                if (command == "checkin")
                {
                    try
                    {
                        decimal total = Convert.ToDecimal(TextBox4.Text);
                        decimal totalPaid = Convert.ToDecimal(TextBox5.Text);
                        decimal remaining = total - totalPaid;

                        TextBox10.Text = remaining.ToString("0.00");
                        TextBox10.ReadOnly = true;
                        TextBox10.Enabled = false;  // Also disable to prevent any editing
                    }
                    catch
                    {
                        TextBox10.Text = "0";
                        TextBox10.ReadOnly = true;
                        TextBox10.Enabled = false;
                    }
                }
                else
                {
                    // RentMore mode: Allow manual input
                    TextBox10.ReadOnly = false;
                    TextBox10.Enabled = true;
                }
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

                // SECURE: Receipt lookup with parameterized query
                var receiptCheckParams = new Dictionary<string, object>
                {
                    { "@ReservationID", id ?? "" }
                };

                DataTable dtReceipt = code.DatabaseQuerySafe(conn,
                    "SELECT [ID] FROM [Account_Receipt] WHERE RESERVATION_ID = @ReservationID",
                    receiptCheckParams);

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

        public int checkAccomUseCoupon(string AccomID, DateTime datereserve)
        {
            int check = 0;

            // SECURE: RatePlan lookup with parameterized query
            var ratePlanParams = new Dictionary<string, object>
            {
                { "@AccomID", AccomID }
            };

            DataTable dtRatePlan = code.DatabaseQuerySafe(conn,
                "SELECT * FROM Accommodation_RatePlan WHERE Accom_ID = @AccomID",
                ratePlanParams);

            for (int j = 0; j < dtRatePlan.Rows.Count; j++)
            {
                int Start_Month = Convert.ToInt32(dtRatePlan.Rows[j]["Start_Month"].ToString());
                int End_Month = Convert.ToInt32(dtRatePlan.Rows[j]["End_Month"].ToString());
                bool checkMonth = false;
                if (Start_Month < End_Month)
                {
                    for (int k = Start_Month - 1; k < End_Month; k++)
                    {
                        if (Convert.ToInt32(datereserve.Month) == (k + 1))
                        {
                            checkMonth = true;
                        }
                    }
                }
                else if (End_Month < Start_Month)
                {
                    int startmonth = Start_Month;
                    for (int k = Start_Month - 1; k < (End_Month + 12); k++)
                    {
                        if (startmonth > 12)
                        {
                            startmonth = startmonth - 12;
                        }
                        if (Convert.ToInt32(datereserve.Month) == (startmonth))
                        {
                            checkMonth = true;
                        }
                        startmonth++;
                    }
                }
                else if (Start_Month == End_Month)
                {
                    if (Convert.ToInt32(datereserve.Month) == (Start_Month))
                    {
                        checkMonth = true;
                    }
                }

                if (checkMonth == true)
                {
                    bool Holiday = false;
                    DataTable dtHoliday = code.DatabaseQuery(conn, "Select * from Accommodation_Holiday Where [Status] = 1");
                    for (int k = 0; k < dtHoliday.Rows.Count; k++)
                    {
                        if (datereserve == code2.ParseDate((dtHoliday.Rows[k]["Holiday_Date"].ToString())))
                        {
                            Holiday = true;
                        }
                    }
                    // SECURE: RatePlanDayType lookup with parameterized query
                    var ratePlanDayTypeParams = new Dictionary<string, object>
                    {
                        { "@RatePlanID", dtRatePlan.Rows[j]["ID"].ToString() }
                    };

                    DataTable dtRatePlanDayType = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM [Taketime].[dbo].[Accommodation_RatePlan] " +
                        "INNER JOIN Accommodation_DayType ON Accommodation_DayType.ID = DayType_Name_ID " +
                        "WHERE Accommodation_RatePlan.ID = @RatePlanID",
                        ratePlanDayTypeParams);
                    string[] days = dtRatePlanDayType.Rows[0]["Day"].ToString().Split(',');
                    var culture = CultureInfo.CurrentCulture;
                    for (int k = 0; k < days.Length; k++)
                    {
                        string dayss = datereserve.DayOfWeek.ToString();
                        if (days[k].ToLower() == dayss.Substring(0, 3).ToLower() || (days[k].ToLower() == "holiday" && Holiday == true))
                        {
                            string UseCoupon = Session["UseCoupon"].ToString();


                            if (UseCoupon == "Affiliate")
                            {
                                // SECURE: Coupon/Affiliate discount lookup with parameterized query
                                var couponParams = new Dictionary<string, object>
                                {
                                    { "@RatePlanID", dtRatePlan.Rows[j]["ID"].ToString() }
                                };

                                DataTable dtCoupon = code.DatabaseQuerySafe(conn,
                                    "SELECT * FROM [Taketime].[dbo].[Affiliate_Member] " +
                                    "INNER JOIN Affiliate_Discount ON Affiliate_Discount.ID = Affiliate_Member.Affiliate_Discount_ID " +
                                    "INNER JOIN Affiliate_Discount_RatePlan ON Affiliate_Discount_RatePlan.Affiliate_Discount_ID = Affiliate_Member.Affiliate_Discount_ID " +
                                    "WHERE Accommodation_RatePlan_ID = @RatePlanID",
                                    couponParams);
                                if (dtCoupon.Rows.Count >= 1)
                                {
                                    check = Convert.ToInt32(dtCoupon.Rows[0]["ID2"].ToString());
                                }
                                else
                                {
                                    
                                }
                            }
                            else
                            {
                                
                            }

                        }
                    }
                }
            }

            return check;
        }

        /// <summary>
        /// คำนวณราคาห้องพักรวมผู้พักเสริม
        /// </summary>
        /// <param name="AccomID">รหัสห้องพัก</param>
        /// <param name="datereserve">วันที่จอง</param>
        /// <param name="numberOfGuests">จำนวนผู้เข้าพัก</param>
        /// <returns>ราคารวมผู้พักเสริม</returns>
        public int CalculateAccomPriceWithExtraGuests(string AccomID, DateTime datereserve, int numberOfGuests)
        {
            // ดึงราคาพื้นฐานจาก Rate Plan
            int basePrice = Convert.ToInt32(AccomPrice(AccomID, datereserve));

            // ดึงข้อมูลห้องพัก
            DataTable dtAccom = code.DatabaseQuery(conn,
                "SELECT StandardOccupancy, MaxOccupancy, ExtraGuestPrice, LimitWithPeople " +
                "FROM Accommodation WHERE ID = " + AccomID);

            if (dtAccom.Rows.Count == 0)
                return basePrice;

            // ถ้าห้องนี้ไม่คิดราคาตามคน ให้คืนราคาพื้นฐาน
            if (dtAccom.Rows[0]["LimitWithPeople"].ToString() != "True")
                return basePrice;

            int standardOccupancy = Convert.ToInt32(dtAccom.Rows[0]["StandardOccupancy"]);
            int maxOccupancy = Convert.ToInt32(dtAccom.Rows[0]["MaxOccupancy"]);
            int extraGuestPrice = Convert.ToInt32(dtAccom.Rows[0]["ExtraGuestPrice"]);

            // ตรวจสอบจำนวนผู้พัก
            if (numberOfGuests <= standardOccupancy)
            {
                // จำนวนผู้พักไม่เกิน ใช้ราคาพื้นฐาน
                return basePrice * numberOfGuests;
            }
            else if (numberOfGuests <= maxOccupancy)
            {
                // มีผู้พักเสริม
                int extraGuests = numberOfGuests - standardOccupancy;
                int totalPrice = (basePrice * standardOccupancy) + (extraGuests * extraGuestPrice);
                return totalPrice;
            }
            else
            {
                // เกินจำนวนที่รองรับ - ใช้ราคาสูงสุด
                int extraGuests = maxOccupancy - standardOccupancy;
                int totalPrice = (basePrice * standardOccupancy) + (extraGuests * extraGuestPrice);
                return totalPrice;
            }
        }

        public string AccomPrice(string AccomID,DateTime datereserve)
        {
            string Price = "";

            // SECURE: RatePlan lookup with parameterized query
            var ratePlanParams = new Dictionary<string, object>
            {
                { "@AccomID", AccomID }
            };

            DataTable dtRatePlan = code.DatabaseQuerySafe(conn,
                "SELECT * FROM Accommodation_RatePlan WHERE Accom_ID = @AccomID",
                ratePlanParams);

            for (int j = 0; j < dtRatePlan.Rows.Count; j++)
            {
                int Start_Month = Convert.ToInt32(dtRatePlan.Rows[j]["Start_Month"].ToString());
                int End_Month = Convert.ToInt32(dtRatePlan.Rows[j]["End_Month"].ToString());
                bool checkMonth = false;
                if (Start_Month < End_Month)
                {
                    for (int k = Start_Month - 1; k < End_Month; k++)
                    {
                        if (Convert.ToInt32(datereserve.Month) == (k + 1))
                        {
                            checkMonth = true;
                        }
                    }
                }
                else if (End_Month < Start_Month)
                {
                    int startmonth = Start_Month;
                    for (int k = Start_Month - 1; k < (End_Month + 12); k++)
                    {
                        if (startmonth > 12)
                        {
                            startmonth = startmonth - 12;
                        }
                        if (Convert.ToInt32(datereserve.Month) == (startmonth))
                        {
                            checkMonth = true;
                        }
                        startmonth++;
                    }
                }
                else if (Start_Month == End_Month)
                {
                    if (Convert.ToInt32(datereserve.Month) == (Start_Month))
                    {
                        checkMonth = true;
                    }
                }

                if (checkMonth == true)
                {
                    bool Holiday = false;
                    DataTable dtHoliday = code.DatabaseQuery(conn, "Select * from Accommodation_Holiday Where [Status] = 1");
                    for (int k = 0; k < dtHoliday.Rows.Count; k++)
                    {
                        if (datereserve == code2.ParseDate((dtHoliday.Rows[k]["Holiday_Date"].ToString())))
                        {
                            Holiday = true;
                        }
                    }
                    // SECURE: RatePlanDayType lookup with parameterized query
                    var ratePlanDayTypeParams = new Dictionary<string, object>
                    {
                        { "@RatePlanID", dtRatePlan.Rows[j]["ID"].ToString() }
                    };

                    DataTable dtRatePlanDayType = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM [Taketime].[dbo].[Accommodation_RatePlan] " +
                        "INNER JOIN Accommodation_DayType ON Accommodation_DayType.ID = DayType_Name_ID " +
                        "WHERE Accommodation_RatePlan.ID = @RatePlanID",
                        ratePlanDayTypeParams);
                    string[] days = dtRatePlanDayType.Rows[0]["Day"].ToString().Split(',');
                    var culture = CultureInfo.CurrentCulture;
                    for (int k = 0; k < days.Length; k++)
                    {
                        string dayss = datereserve.DayOfWeek.ToString();
                        if (days[k].ToLower() == dayss.Substring(0, 3).ToLower() || (days[k].ToLower() == "holiday" && Holiday == true))
                        {
                            string UseCoupon = Session["UseCoupon"].ToString();

                            if (UseCoupon == "Affiliate")
                            {
                                // SECURE: Coupon/Affiliate discount lookup with parameterized query
                                var couponParams = new Dictionary<string, object>
                                {
                                    { "@RatePlanID", dtRatePlan.Rows[j]["ID"].ToString() }
                                };

                                DataTable dtCoupon = code.DatabaseQuerySafe(conn,
                                    "SELECT * FROM [Taketime].[dbo].[Affiliate_Member] " +
                                    "INNER JOIN Affiliate_Discount ON Affiliate_Discount.ID = Affiliate_Member.Affiliate_Discount_ID " +
                                    "INNER JOIN Affiliate_Discount_RatePlan ON Affiliate_Discount_RatePlan.Affiliate_Discount_ID = Affiliate_Member.Affiliate_Discount_ID " +
                                    "WHERE Accommodation_RatePlan_ID = @RatePlanID",
                                    couponParams);
                                if(dtCoupon.Rows.Count >= 1)
                                {
                                    Price = (Convert.ToInt32(dtRatePlan.Rows[j]["Price"].ToString()) - Convert.ToInt32(dtCoupon.Rows[0]["Discount_Amount"].ToString())).ToString();
                                }
                                else
                                {
                                    Price = dtRatePlan.Rows[j]["Price"].ToString();
                                }
                            }
                            else if(UseCoupon == "Voucher")
                            {
                                DataTable dtVoucher = new DataTable();
                                try
                                {
                                    // SECURE: Voucher lookup with parameterized query
                                    var voucherParams = new Dictionary<string, object>
                                    {
                                        { "@VoucherNumber", TextBox19.Text ?? "" },
                                        { "@RatePlanID", dtRatePlan.Rows[j]["ID"].ToString() },
                                        { "@ExpiredDate", code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToInt32(DropDownList1.SelectedValue) - 1) }
                                    };

                                    dtVoucher = code.DatabaseQuerySafe(conn,
                                        "SELECT * FROM [Taketime].[dbo].[Voucher] " +
                                        "INNER JOIN Voucher_RatePlan_Group ON Voucher_RatePlan_Group.Voucher_Number = Voucher.Voucher_Number " +
                                        "INNER JOIN Accommodation_RatePlan_Group ON Rateplan_GroupID = Accommodation_RatePlan_Group.GroupID " +
                                        "INNER JOIN Accommodation_RatePlan ON Accommodation_RatePlan.ID = Rateplan_ID " +
                                        "WHERE Voucher.Voucher_Number = @VoucherNumber " +
                                        "AND Used_Status = 'False' " +
                                        "AND Accommodation_RatePlan.ID = @RatePlanID " +
                                        "AND Expired_Date >= @ExpiredDate",
                                        voucherParams);

                                }
                                catch
                                {
                                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเลือกวันที่ต้องการจองก่อน');", true);
                                }
                                
                                if (dtVoucher.Rows.Count >= 1)
                                {
                                    Price = Convert.ToInt32(dtVoucher.Rows[0]["PriceTo"].ToString()).ToString();
                                    Session["UseVoucher"] = true;
                                    string oldvalue = "";
                                    try
                                    {
                                        oldvalue = Session["UseVoucherAccomID"].ToString();
                                        Session["UseVoucherAccomID"] += oldvalue +dtVoucher.Rows[0]["Accom_ID"].ToString()+";";
                                    }
                                    catch
                                    {
                                        Session["UseVoucherAccomID"] += dtVoucher.Rows[0]["Accom_ID"].ToString()+";";
                                    }
                                }
                                else
                                {
                                    Price = dtRatePlan.Rows[j]["Price"].ToString();
                                }
                            }
                            else
                            {
                                Price = dtRatePlan.Rows[j]["Price"].ToString();
                            }
                            
                        }
                    }
                }
            }

            try
            {
                if(Price.Length > 0)
                {

                }
                else
                {
                    Price = "";
                }
            }
            catch
            {
                Price = "";
            }
            return Price;
        }

        private DateTime? NormalizeAndParseDate(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return null;

            // Remove any time portion if present
            dateString = dateString.Split(' ')[0];

            DateTime? parsedDate = code2.ParseDate(dateString);

            if (parsedDate.HasValue)
            {
                return parsedDate;
            }

            // Additional manual parsing for common formats
            string[] parts = dateString.Split('-', '/', '.');

            if (parts.Length == 3)
            {
                int day, month, year;

                // Try dd-MM-yyyy format
                if (int.TryParse(parts[0], out day) &&
                    int.TryParse(parts[1], out month) &&
                    int.TryParse(parts[2], out year))
                {
                    // Check if it's a valid date in dd-MM-yyyy format
                    if (IsValidDate(day, month, year))
                        return new DateTime(year, month, day);

                    // Try MM-dd-yyyy format
                    if (IsValidDate(month, day, year))
                        return new DateTime(year, month, day);
                }
            }

            return null;
        }

        // Helper method to validate date components
        private bool IsValidDate(int day, int month, int year)
        {
            if (year < 1900 || year > 2100) return false;
            if (month < 1 || month > 12) return false;
            if (day < 1 || day > 31) return false;

            try
            {
                DateTime testDate = new DateTime(year, month, day);
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected void TextBox12_TextChanged(object sender, EventArgs e)
        {
            // Skip if called from code (not user interaction) to prevent AutoPostBack loop
            if (sender == null && string.IsNullOrWhiteSpace(TextBox12.Text))
            {
                GridView1.Visible = false;
                return;
            }

            // Allow empty TextBox from user interaction - just hide GridView
            if (string.IsNullOrWhiteSpace(TextBox12.Text))
            {
                GridView1.Visible = false;
                Label1.Text = "กรุณาเลือกวันที่เช็คอิน";
                return;
            }

            DateTime? datereserve = code2.ParseDate(TextBox12.Text);

            // Validate date parsing
            if (!datereserve.HasValue)
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert",
                    "alert('รูปแบบวันที่ไม่ถูกต้อง\\nวันที่ที่ได้รับ: " + TextBox12.Text + "\\nกรุณาเลือกวันที่ใหม่');", true);
                TextBox12.Text = "";
                GridView1.Visible = false;
                Label1.Text = "";
                return;
            }

            // Get current date in Thai timezone (support both Windows and Linux)
            DateTime nowThai;
            try
            {
                // Try Windows timezone ID first
                TimeZoneInfo thaiZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                nowThai = TimeZoneInfo.ConvertTime(DateTime.UtcNow, thaiZone);
            }
            catch
            {
                try
                {
                    // Try Linux/Mac timezone ID
                    TimeZoneInfo thaiZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
                    nowThai = TimeZoneInfo.ConvertTime(DateTime.UtcNow, thaiZone);
                }
                catch
                {
                    // Fallback: Add 7 hours to UTC (GMT+7)
                    nowThai = DateTime.UtcNow.AddHours(7);
                }
            }

            // DISABLED: Permission check for past dates
            // Reason: Server time may be incorrect, causing false positives
            // This check was blocking valid future dates when server time was wrong
            /*
            if (Session["permission"] != null && Session["permission"].ToString() == "No")
            {
                if (datereserve.Value.Date < nowThai.Date.AddDays(-1))
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert",
                        "alert('ไม่สามารถจองย้อนหลังได้\\nกรุณาเลือกวันที่ในอนาคต');", true);
                    GridView1.Visible = false;
                    return;
                }
            }
            */

            // Always show GridView when valid date is selected
            GridView1.Visible = true;

            // Check if booking is too far in future (more than 3 months)
            DateTime maxBookingDate = nowThai.AddMonths(3);
            if (datereserve.Value > maxBookingDate &&
                (Session["permission"] == null || Session["permission"].ToString() != "True"))
            {
                string errorMsg = string.Format(
                    "ระบบไม่อนุญาติให้จองเกิน 3 เดือน\\n" +
                    "วันที่ที่คุณเลือก: {0}\\n" +
                    "วันที่สูงสุดที่จองได้: {1}\\n" +
                    "กรุณาติดต่อ Admin หากต้องการจองเกินกำหนด",
                    datereserve.Value.ToString("dd MMMM yyyy", new CultureInfo("th-TH")),
                    maxBookingDate.ToString("dd MMMM yyyy", new CultureInfo("th-TH")));

                ClientScript.RegisterStartupScript(this.GetType(), "myalert",
                    "alert('" + errorMsg + "');", true);
                TextBox12.Text = "";
                GridView1.Visible = false;
                return;
            }

            // Get query parameters
            string command = Request.QueryString["command"];
            string id = Request.QueryString["id"];
            string check = Request.QueryString["check"]?.Replace(" ", "+");

            try
            {
                Label1.Text = "Check-Out: " + datereserve.Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue))
                    .ToString("dd MMMM yyyy");
            }
            catch
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert",
                    "alert('กรุณาติดต่อ Admin กรณีต้องการจองเกิน 3 เดือน');", true);
            }

            // Get all active accommodations
            DataTable dtAccommodation = code.DatabaseQuery(conn,
                "SELECT * FROM Accommodation WHERE Status = 1 ORDER BY OrderID ASC");

            // Add status columns
            if (!dtAccommodation.Columns.Contains("StatusOnDate"))
                dtAccommodation.Columns.Add("StatusOnDate", typeof(string));
            if (!dtAccommodation.Columns.Contains("AvailableAmount"))
                dtAccommodation.Columns.Add("AvailableAmount", typeof(int));

            // Calculate date range
            DateTime checkInDate = datereserve.Value;
            DateTime checkOutDate = checkInDate.AddDays(Convert.ToInt32(DropDownList1.SelectedValue));

            // Get all reservations that overlap with our date range
            string reservationQuery = $@"
SELECT ra.* 
FROM Reservation r
JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
WHERE r.CheckinDate < '{checkOutDate.ToString("yyyy-MM-dd")}' 
AND r.CheckoutDate > '{checkInDate.ToString("yyyy-MM-dd")}'";

            if (command == "edit" || command == "checkin" || command == "rentmore")
            {
                reservationQuery += $" AND r.ID != {id}";
            }

            DataTable dtAllReservations = code.DatabaseQuery(conn, reservationQuery);

            // Create a list to store rows to remove (instead of deleting directly)
            List<DataRow> rowsToRemove = new List<DataRow>();

            // Check availability for each accommodation
            foreach (DataRow accomRow in dtAccommodation.Rows)
            {
                if (accomRow.RowState == DataRowState.Deleted) continue;

                string accomId = accomRow["ID"].ToString();
                bool isLimitWithPeople = accomRow["LimitWithPeople"].ToString() == "True";
                int maxCapacity = Convert.ToInt32(accomRow["People"]);
                int totalReserved = 0;

                // Calculate total reserved for this accommodation
                foreach (DataRow resRow in dtAllReservations.Rows)
                {
                    if (resRow["Accommodation_ID"].ToString() == accomId)
                    {
                        if (isLimitWithPeople)
                        {
                            totalReserved += Convert.ToInt32(resRow["Amount"]);
                        }
                        else
                        {
                            // For non-people limited accommodations, any reservation means fully booked
                            totalReserved = maxCapacity;
                            break;
                        }
                    }
                }

                // Set status and available amount
                if (isLimitWithPeople)
                {
                    int available = maxCapacity - totalReserved;
                    accomRow["AvailableAmount"] = available;
                    accomRow["StatusOnDate"] = available > 0 ?
                        $"Available ({available} left)" : "Fully Booked";

                    // Mark for removal if no availability
                    if (available <= 0)
                    {
                        rowsToRemove.Add(accomRow);
                    }
                    else
                    {
                        accomRow["People"] = accomRow["AvailableAmount"].ToString();
                    }
                }
                else
                {
                    accomRow["AvailableAmount"] = totalReserved > 0 ? 0 : 1;
                    accomRow["StatusOnDate"] = totalReserved > 0 ?
                        "Fully Booked" : "Available";

                    // Mark for removal if booked
                    if (totalReserved > 0)
                    {
                        rowsToRemove.Add(accomRow);
                    }
                }
            }

            // Remove marked rows
            foreach (DataRow row in rowsToRemove)
            {
                dtAccommodation.Rows.Remove(row);
            }

            // Set prices
            // ✅ FIX: For edit modes, try to preserve prices from Session first (user may have edited them)
            // If no Session data, use DB prices
            DataTable dtOldAccommodation = Session["dtAccommodation"] as DataTable;
            bool hasOldPrices = (dtOldAccommodation != null &&
                                 (command == "edit" || command == "checkin" || command == "rentmore"));

            foreach (DataRow accomRow in dtAccommodation.Rows)
            {
                if (accomRow.RowState == DataRowState.Deleted) continue;

                string accomId = accomRow["ID"].ToString();
                bool priceFound = false;

                // Try to get price from Session (preserves manual edits)
                if (hasOldPrices)
                {
                    foreach (DataRow oldRow in dtOldAccommodation.Rows)
                    {
                        if (oldRow["ID"].ToString() == accomId)
                        {
                            // Found in Session - use saved price (may be manually edited)
                            accomRow["Price"] = oldRow["Price"];
                            priceFound = true;
                            break;
                        }
                    }
                }

                // If not found in Session, get from DB
                if (!priceFound)
                {
                    accomRow["Price"] = AccomPrice(accomId, checkInDate);
                }
            }

            // Bind to grid
            GridView1.DataSource = dtAccommodation;
            GridView1.DataBind();
            Session["dtAccommodation"] = dtAccommodation;

            // Items processing
            DataTable dtItems = code.DatabaseQuery(conn, "SELECT * FROM Items WHERE Status = 1 ORDER BY OrderID ASC");

            for (int x = 0; x < Convert.ToInt32(DropDownList1.SelectedValue); x++)
            {
                DataTable dtReservation_Items = code.DatabaseQuery(conn,
                    "Select * From Reservation right join Reservation_Items on Reservation.ID = Reservation_Items.Reservation_ID " +
                    "Where '" + Convert.ToDateTime(TextBox12.Text).AddDays((double)x).ToString("yyyy-MM-dd") + "' >= CheckinDate AND '" +
                    Convert.ToDateTime(TextBox12.Text).AddDays((double)x).ToString("yyyy-MM-dd") + "' < CheckoutDate");

                if (command == "edit" || command == "checkin" || command == "rentmore")
                {
                    DataTable dtItem = code.DatabaseQuery(conn,
                        "SELECT * FROM [Reservation] right join Reservation_Items on Reservation_Items.Reservation_ID = Reservation.ID " +
                        "Where Reservation.ID = " + id + " AND Customer_MobilePhone = '" + check + "'");

                    // ✅ Auto-tick checkbox if reservation has rental items (EDIT/CHECKIN/RENTMORE modes)
                    if (dtItem.Rows.Count > 0)
                    {
                        CheckBox7.Checked = true;
                        Panel2.Visible = true;
                    }

                    for (int i = 0; i < dtReservation_Items.Rows.Count; i++)
                    {
                        for (int j = 0; j < dtItems.Rows.Count; j++)
                        {
                            if (dtItems.Rows[j].RowState == DataRowState.Deleted) continue;

                            try
                            {
                                if (dtReservation_Items.Rows[i]["Reservation_ID"].ToString() == dtItems.Rows[j]["Reservation_ID"].ToString() &&
                                    dtReservation_Items.Rows[i]["Accommodation_ID"].ToString() == dtItems.Rows[j]["Accommodation_ID"].ToString() &&
                                    dtReservation_Items.Rows[i]["Amount"].ToString() == dtItems.Rows[j]["Amount"].ToString())
                                {
                                    // Matching rows found
                                }
                            }
                            catch { }
                        }
                    }
                }

                try
                {
                    if (!dtItems.Columns.Contains("StatusOnDate"))
                    {
                        dtItems.Columns.Add("StatusOnDate");
                    }
                }
                catch { }

                // Create list to store items to remove
                List<DataRow> itemsToRemove = new List<DataRow>();

                for (int j = 0; j < dtItems.Rows.Count; j++)
                {
                    if (dtItems.Rows[j].RowState == DataRowState.Deleted) continue;

                    int checkloop = 0;
                    int totalAmount = 0;

                    for (int i = 0; i < dtReservation_Items.Rows.Count; i++)
                    {
                        if (dtReservation_Items.Rows[i]["Items_ID"].ToString() == dtItems.Rows[j]["ID"].ToString())
                        {
                            checkloop = 1;
                            if (dtItems.Rows[j]["LimitWithAmount"].ToString() == "True" &&
                                (!DBNull.Value.Equals(dtReservation_Items.Rows[i]["Amount"])) &&
                                dtReservation_Items.Rows[i]["Items_ID"].ToString() == dtItems.Rows[j]["ID"].ToString())
                            {
                                totalAmount += Convert.ToInt32(dtReservation_Items.Rows[i]["Amount"].ToString());
                            }

                            try
                            {
                                if (command == "edit" || command == "checkin")
                                {
                                    if (dtItems.Rows[j]["LimitWithAmount"].ToString() == "True" &&
                                        dtReservation_Items.Rows[i]["Reservation_ID"].ToString() == id)
                                    {
                                        totalAmount -= Convert.ToInt32(dtReservation_Items.Rows[i]["Amount"].ToString());
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    if (checkloop == 1 && dtItems.Rows[j]["LimitWithAmount"].ToString() == "False")
                    {
                        dtItems.Rows[j]["StatusOnDate"] = "ไม่ว่าง (Not available)";
                        dtItems.Rows[j]["Amount"] = 0;
                        itemsToRemove.Add(dtItems.Rows[j]);
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
                                itemsToRemove.Add(dtItems.Rows[j]);
                            }
                            else
                            {
                                dtItems.Rows[j]["StatusOnDate"] = "ว่าง (Available)";
                                dtItems.Rows[j]["Amount"] = Convert.ToInt32(dtItems.Rows[j]["Amount"].ToString()) - totalAmount;
                            }
                        }
                    }
                }

                // Remove marked items
                foreach (DataRow row in itemsToRemove)
                {
                    dtItems.Rows.Remove(row);
                }
            }

            GridView2.DataSource = dtItems;
            GridView2.DataBind();
            Session["dtItems"] = dtItems;
        }
        //protected void TextBox12_TextChanged(object sender, EventArgs e)
        //{
        //    DateTime? datereserve = code2.ParseDate(DateTime.Now.ToString("yyyy-MM-dd"));
        //    try
        //    {
        //        datereserve = code2.ParseDate(TextBox12.Text);
        //    }
        //    catch
        //    {
        //        TextBox12.Text = DateTime.Now.ToString("yyyy-MM-dd");
        //    }
        //    if (DateTime.Now > datereserve.Value.AddDays(1) && Session["permission"] == "No")
        //    {
        //        GridView1.Visible = false;
        //    }
        //    else
        //    {
        //        GridView1.Visible = true;
        //        try
        //        {
        //            if (Session["permission"].ToString() == "True")
        //            {

        //            }
        //            else
        //            {
        //                if (code2.ParseDate(TextBox12.Text) > DateTime.Now.AddMonths(3))
        //                {
        //                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ระบบไม่อนุญาติให้จองเกิน 3 เดือน กรุณาติดต่อ Admin');", true);
        //                    TextBox12.Text = "";

        //                }
        //            }
        //        }
        //        catch
        //        {
        //            if (code2.ParseDate(TextBox12.Text) > DateTime.Now.AddMonths(3))
        //            {
        //                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ระบบไม่อนุญาติให้จองเกิน 3 เดือน กรุณาติดต่อ Admin');", true);
        //                TextBox12.Text = "";
        //            }
        //        }



        //        string command = Request.QueryString["command"];
        //        string id = Request.QueryString["id"];
        //        string check = Request.QueryString["check"];
        //        try
        //        {
        //            if (check[0] == ' ')
        //            {
        //                check = "+" + check.Replace(" ", "");
        //            }
        //        }
        //        catch { }
        //        try
        //        {
        //            Label1.Text = "Check-Out: " + code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToDouble(DropDownList1.SelectedValue.ToString())).ToString("dd MMMM yyyy");
        //        }
        //        catch
        //        {
        //            ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาติดต่อ Admin กรณีต้องการจองเกิน 3 เดือน');", true);
        //        }
        //        DataTable dtAccommodation = code.DatabaseQuery(conn, "Select * From Accommodation Where Status = 1 order by OrderID asc");
        //        for (int x = 0; x < Convert.ToInt32(DropDownList1.SelectedValue); x++)
        //        {
        //            DataTable dtReservation = code.DatabaseQuery(conn, "Select * From Reservation right join Reservation_Accommodation on Reservation.ID = Reservation_Accommodation.Reservation_ID Where '" + code2.ParseDate(TextBox12.Text).Value.AddDays((double)x).ToString("yyyy-MM-dd") + "' >= CheckinDate AND '" + code2.ParseDate(TextBox12.Text).Value.AddDays((double)x).ToString("yyyy-MM-dd") + "' < CheckoutDate");

        //            if (command == "edit" || command == "checkin" || command == "rentmore")
        //            {
        //                DataTable dtAccom = code.DatabaseQuery(conn, "SELECT * FROM [Reservation] right join Reservation_Accommodation on Reservation_Accommodation.Reservation_ID = Reservation.ID Where Reservation.ID = " + id + " AND Customer_MobilePhone = '" + check + "'");
        //                for (int i = 0; i < dtReservation.Rows.Count; i++)
        //                {
        //                    for (int j = 0; j < dtAccom.Rows.Count; j++)
        //                    {
        //                        try
        //                        {
        //                            if (dtReservation.Rows[i]["Reservation_ID"].ToString() == dtAccom.Rows[j]["Reservation_ID"].ToString() && dtReservation.Rows[i]["Accommodation_ID"].ToString() == dtAccom.Rows[j]["Accommodation_ID"].ToString() && dtReservation.Rows[i]["Amount"].ToString() == dtAccom.Rows[j]["Amount"].ToString())
        //                            {
        //                                dtReservation.Rows[i].Delete();
        //                            }
        //                        }
        //                        catch { }
        //                    }
        //                }
        //                dtReservation.AcceptChanges();
        //            }


        //            try
        //            {
        //                dtAccommodation.Columns.Add("StatusOnDate");
        //            }
        //            catch
        //            {
        //            }

        //            List<int> rowDelete = new List<int>();
        //            for (int j = 0; j < dtAccommodation.Rows.Count; j++)
        //            {
        //                int checkloop = 0;
        //                int totalAmount = 0;
        //                int ReserveAmount = 0;


        //                for (int i = 0; i < dtReservation.Rows.Count; i++)
        //                {
        //                    if (dtReservation.Rows[i]["Accommodation_ID"].ToString() == dtAccommodation.Rows[j]["ID"].ToString())
        //                    {
        //                        checkloop = 1;
        //                        if (dtAccommodation.Rows[j]["LimitWithPeople"].ToString() == "True" && (!DBNull.Value.Equals(dtReservation.Rows[i]["Amount"])) && dtReservation.Rows[i]["Accommodation_ID"].ToString() == dtAccommodation.Rows[j]["ID"].ToString())
        //                        {
        //                            totalAmount += Convert.ToInt32(dtReservation.Rows[i]["Amount"].ToString());
        //                        }
        //                    }

        //                }
        //                if (checkloop == 1 && dtAccommodation.Rows[j]["LimitWithPeople"].ToString() == "False")
        //                {
        //                    dtAccommodation.Rows[j]["StatusOnDate"] = "ไม่ว่าง (Not available)";
        //                    dtAccommodation.Rows[j].Delete();
        //                }
        //                else
        //                {
        //                    dtAccommodation.Rows[j]["StatusOnDate"] = "ว่าง (Available)";

        //                    if ("True" == "True")
        //                    {
        //                        if (totalAmount >= Convert.ToInt32(dtAccommodation.Rows[j]["People"].ToString()))
        //                        {
        //                            dtAccommodation.Rows[j]["StatusOnDate"] = "ไม่ว่าง (Not available)";
        //                            try
        //                            {
        //                                dtAccommodation.Rows[j]["People"] = Convert.ToInt32(dtAccommodation.Rows[j]["People"].ToString()) - totalAmount;
        //                            }
        //                            catch
        //                            {
        //                                int checkk = 0;
        //                                DataTable dtReserveAmount = code.DatabaseQuery(conn, "SELECT * FROM [Reservation_Accommodation] Where Reservation_ID = " + id);
        //                                for (int l = 0; l < dtReserveAmount.Rows.Count; l++)
        //                                {
        //                                    if (dtReserveAmount.Rows[l]["Accommodation_ID"].ToString() == dtAccommodation.Rows[j]["ID"].ToString())
        //                                    {
        //                                        dtAccommodation.Rows[j]["People"] = dtReserveAmount.Rows[l]["Amount"].ToString();
        //                                        checkk = 1;
        //                                    }
        //                                }
        //                                if (checkk == 0)
        //                                { dtAccommodation.Rows[j].Delete(); }

        //                            }


        //                        }
        //                        else
        //                        {
        //                            dtAccommodation.Rows[j]["StatusOnDate"] = "ว่าง (Available)";
        //                            dtAccommodation.Rows[j]["People"] = Convert.ToInt32(dtAccommodation.Rows[j]["People"].ToString()) - totalAmount;
        //                        }
        //                    }

        //                }

        //            }
        //            dtAccommodation.AcceptChanges();
        //        }


        //        for (int i = 0; i < dtAccommodation.Rows.Count; i++)
        //        {
        //            dtAccommodation.Rows[i]["Price"] = AccomPrice(dtAccommodation.Rows[i]["ID"].ToString(), code2.ParseDate(TextBox12.Text).Value);
        //        }

        //        GridView1.DataSource = dtAccommodation;
        //        GridView1.DataBind();
        //        Session["dtAccommodation"] = dtAccommodation;

        //        DataTable dtItems = code.DatabaseQuery(conn, "Select * From Items Where Status = 1 order by OrderID asc");
        //        for (int x = 0; x < Convert.ToInt32(DropDownList1.SelectedValue); x++)
        //        {
        //            DataTable dtReservation_Items = code.DatabaseQuery(conn, "Select * From Reservation right join Reservation_Items on Reservation.ID = Reservation_Items.Reservation_ID Where '" + code2.ParseDate(TextBox12.Text).Value.AddDays((double)x).ToString("yyyy-MM-dd") + "' >= CheckinDate AND '" + code2.ParseDate(TextBox12.Text).Value.AddDays((double)x).ToString("yyyy-MM-dd") + "' < CheckoutDate");

        //            if (command == "edit" || command == "checkin")
        //            {
        //                DataTable dtItem = code.DatabaseQuery(conn, "SELECT * FROM [Reservation] right join Reservation_Items on Reservation_Items.Reservation_ID = Reservation.ID Where Reservation.ID = " + id + " AND Customer_MobilePhone = '" + check + "'");
        //                for (int i = 0; i < dtReservation_Items.Rows.Count; i++)
        //                {
        //                    for (int j = 0; j < dtItems.Rows.Count; j++)
        //                    {
        //                        try
        //                        {
        //                            if (dtReservation_Items.Rows[i]["Reservation_ID"].ToString() == dtItems.Rows[j]["Reservation_ID"].ToString() && dtReservation_Items.Rows[i]["Accommodation_ID"].ToString() == dtItems.Rows[j]["Accommodation_ID"].ToString() && dtReservation_Items.Rows[i]["Amount"].ToString() == dtItems.Rows[j]["Amount"].ToString())
        //                            {


        //                            }
        //                        }
        //                        catch { }
        //                    }
        //                }
        //                dtReservation_Items.AcceptChanges();
        //            }

        //            try
        //            {
        //                dtItems.Columns.Add("StatusOnDate");
        //            }
        //            catch
        //            {
        //            }


        //            for (int j = 0; j < dtItems.Rows.Count; j++)
        //            {
        //                int checkloop = 0;
        //                int totalAmount = 0;
        //                for (int i = 0; i < dtReservation_Items.Rows.Count; i++)
        //                {
        //                    if (dtReservation_Items.Rows[i]["Items_ID"].ToString() == dtItems.Rows[j]["ID"].ToString())
        //                    {
        //                        checkloop = 1;
        //                        if (dtItems.Rows[j]["LimitWithAmount"].ToString() == "True" && (!DBNull.Value.Equals(dtReservation_Items.Rows[i]["Amount"])) && dtReservation_Items.Rows[i]["Items_ID"].ToString() == dtItems.Rows[j]["ID"].ToString())
        //                        {
        //                            totalAmount += Convert.ToInt32(dtReservation_Items.Rows[i]["Amount"].ToString());
        //                        }
        //                        try
        //                        {
        //                            if (command == "edit" || command == "checkin")
        //                            {
        //                                if (dtItems.Rows[j]["LimitWithAmount"].ToString() == "True" && dtReservation_Items.Rows[i]["Reservation_ID"].ToString() == id)
        //                                {
        //                                    totalAmount -= Convert.ToInt32(dtReservation_Items.Rows[i]["Amount"].ToString());
        //                                }
        //                            }

        //                        }
        //                        catch
        //                        {

        //                        }
        //                    }

        //                }
        //                if (checkloop == 1 && dtItems.Rows[j]["LimitWithAmount"].ToString() == "False")
        //                {
        //                    dtItems.Rows[j]["StatusOnDate"] = "ไม่ว่าง (Not available)";
        //                    dtItems.Rows[j]["Amount"] = 0;
        //                    //dtItems.Rows[j].Delete();
        //                }
        //                else
        //                {
        //                    dtItems.Rows[j]["StatusOnDate"] = "ว่าง (Available)";
        //                    if (dtItems.Rows[j]["LimitWithAmount"].ToString() == "True")
        //                    {
        //                        if (totalAmount >= Convert.ToInt32(dtItems.Rows[j]["Amount"].ToString()))
        //                        {
        //                            dtItems.Rows[j]["StatusOnDate"] = "ไม่ว่าง (Not available)";
        //                            dtItems.Rows[j]["Amount"] = 0;
        //                            //dtItems.Rows[j].Delete();
        //                        }
        //                        else
        //                        {
        //                            dtItems.Rows[j]["StatusOnDate"] = "ว่าง (Available)";
        //                            dtItems.Rows[j]["Amount"] = Convert.ToInt32(dtItems.Rows[j]["Amount"].ToString()) - totalAmount;
        //                        }
        //                    }
        //                }

        //            }
        //            dtItems.AcceptChanges();
        //        }
        //        GridView2.DataSource = dtItems;
        //        GridView2.DataBind();
        //        Session["dtItems"] = dtItems;
        //    }
        //}

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
            // ✅ FIXED: Use parameterized query via refactored getAddress method
            getAddress(postalCode: TextBox16.Text);

        }

        protected void Button7_Click(object sender, EventArgs e)
        {
            TextBox16.Enabled = true;
            TextBox16.Text = string.Empty;
            DropDownList5.Items.Clear();
            DropDownList6.Items.Clear();
            DropDownList7.Items.Clear();
            // ✅ FIXED: No filters - get all addresses
            getAddress();

        }

        // ✅ FIXED: Use parameterized queries to prevent SQL Injection
        public void getAddress(string postalCode = null, string province = null, string district = null, string subDistrict = null)
        {
            string Command = Request.QueryString["Command"];
            string ID = Request.QueryString["ID"];

            // Build WHERE clause based on provided filters
            string whereClause = "";
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(postalCode))
            {
                whereClause = "WHERE PostalCode = @PostalCode";
                parameters.Add("@PostalCode", postalCode);
            }

            if (!string.IsNullOrEmpty(province))
            {
                whereClause += (whereClause.Length > 0 ? " AND " : "WHERE ") + "Province = @Province";
                parameters.Add("@Province", province);
            }

            if (!string.IsNullOrEmpty(district))
            {
                whereClause += (whereClause.Length > 0 ? " AND " : "WHERE ") + "District = @District";
                parameters.Add("@District", district);
            }

            if (!string.IsNullOrEmpty(subDistrict))
            {
                whereClause += (whereClause.Length > 0 ? " AND " : "WHERE ") + "SubDistrict = @SubDistrict";
                parameters.Add("@SubDistrict", subDistrict);
            }

            // Use parameterized queries
            string provinceQuery = $"SELECT DISTINCT [Province] FROM [Address] {whereClause} ORDER BY Province ASC";
            string districtQuery = $"SELECT DISTINCT [District] FROM [Address] {whereClause} ORDER BY District ASC";
            string subDistrictQuery = $"SELECT DISTINCT [SubDistrict] FROM [Address] {whereClause} ORDER BY SubDistrict ASC";

            DataTable dtProvince = code2.DatabaseQuerySafe(conn, provinceQuery, parameters);
            DataTable dtDistrict = code2.DatabaseQuerySafe(conn, districtQuery, parameters);
            DataTable dtSubDistrict = code2.DatabaseQuerySafe(conn, subDistrictQuery, parameters);

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


        // ✨ MIGRATED: CheckAddressID() has been replaced with AddressHelper.GetAddressIdString()
        // See AddressHelper class in Take_Time_BangPhra.Class namespace



        protected void DropDownList6_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (TextBox16.Enabled == false)
            {
                // ✅ FIXED: Use parameterized query via refactored getAddress method
                getAddress(postalCode: TextBox16.Text, district: DropDownList6.SelectedValue);
            }
            else
            {
                // ✅ FIXED: Use parameterized query via refactored getAddress method
                getAddress(district: DropDownList6.SelectedValue);
            }
        }

        protected void DropDownList5_SelectedIndexChanged(object sender, EventArgs e)
        {

            if (TextBox16.Enabled == false)
            {
                // ✅ FIXED: Use parameterized query via refactored getAddress method
                getAddress(postalCode: TextBox16.Text, province: DropDownList5.SelectedValue);
            }
            else
            {
                // ✅ FIXED: Use parameterized query via refactored getAddress method
                getAddress(province: DropDownList5.SelectedValue);
            }
        }

        protected void DropDownList8_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(DropDownList8.SelectedValue == "1")
            {
                CheckBox3.Checked = false; 
                CheckBox4.Checked = false;
                CheckBox3_CheckedChanged(null, null);
                TextBox18.Visible = true;
            }
            else
            {
                TextBox18.Visible = false;
            }
        }

public DataTable CheckReservationAvailability(DateTime checkInDate, DateTime checkOutDate, string accommodationId = null)
{
    DataTable dtAvailableAccommodations = new DataTable();
    
    try
    {
        // Get all active accommodations
        string accomQuery = "SELECT * FROM Accommodation WHERE Status = 1 ORDER BY OrderID ASC";
        if (!string.IsNullOrEmpty(accommodationId))
        {
            accomQuery = $"SELECT * FROM Accommodation WHERE Status = 1 AND ID = {accommodationId} ORDER BY OrderID ASC";
        }
        
        DataTable dtAccommodation = code.DatabaseQuery(conn, accomQuery);

        // Add status column
        dtAccommodation.Columns.Add("StatusOnDate", typeof(string));
        dtAccommodation.Columns.Add("AvailableAmount", typeof(int));

        // Check availability for each day in the date range
        for (DateTime date = checkInDate; date < checkOutDate; date = date.AddDays(1))
        {
            // Get reservations for this date
            DataTable dtReservation = code.DatabaseQuery(conn, 
                $"SELECT * FROM Reservation RIGHT JOIN Reservation_Accommodation " +
                $"ON Reservation.ID = Reservation_Accommodation.Reservation_ID " +
                $"WHERE '{date.ToString("yyyy-MM-dd")}' >= CheckinDate " +
                $"AND '{date.ToString("yyyy-MM-dd")}' < CheckoutDate");

            // Check each accommodation's availability
            for (int j = 0; j < dtAccommodation.Rows.Count; j++)
            {
                int totalReserved = 0;
                string accomId = dtAccommodation.Rows[j]["ID"].ToString();
                bool isLimitWithPeople = dtAccommodation.Rows[j]["LimitWithPeople"].ToString() == "True";

                // Calculate total reserved amount for this accommodation
                foreach (DataRow resRow in dtReservation.Rows)
                {
                    if (resRow["Accommodation_ID"].ToString() == accomId)
                    {
                        if (isLimitWithPeople)
                        {
                            totalReserved += Convert.ToInt32(resRow["Amount"]);
                        }
                        else
                        {
                            // For non-people limited accommodations, any reservation means fully booked
                            totalReserved = Convert.ToInt32(dtAccommodation.Rows[j]["People"]);
                            break;
                        }
                    }
                }

                // Update status
                if (isLimitWithPeople)
                {
                    int available = Convert.ToInt32(dtAccommodation.Rows[j]["People"]) - totalReserved;
                    dtAccommodation.Rows[j]["AvailableAmount"] = available;
                    dtAccommodation.Rows[j]["StatusOnDate"] = available > 0 ? 
                        $"Available ({available} left)" : "Fully Booked";
                }
                else
                {
                    dtAccommodation.Rows[j]["AvailableAmount"] = totalReserved > 0 ? 0 : 1;
                    dtAccommodation.Rows[j]["StatusOnDate"] = totalReserved > 0 ? 
                        "Fully Booked" : "Available";
                }
            }
        }

        dtAvailableAccommodations = dtAccommodation;
    }
    catch (Exception ex)
    {
        // Log error
        // You might want to add error logging here
        throw;
    }

    return dtAvailableAccommodations;
}

        protected void Button8_Click(object sender, EventArgs e)
        {
            if (Button8.Text == "Submit")
            {
                string couponCode = TextBox19.Text.Trim();

                // Validate input
                if (string.IsNullOrEmpty(couponCode))
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('❌ กรุณาใส่รหัสส่วนลด\\n\\nPlease enter a coupon code.');", true);
                    LogCouponAttempt("EMPTY", "Validation", "Empty coupon code");
                    return;
                }

                // Affiliate Code (8 characters)
                if (couponCode.Length == 8)
                {
                    try
                    {
                        LogCouponAttempt(couponCode, "Affiliate", "Validating affiliate code");

                        // 🔒 SECURE: Using parameterized query to prevent SQL Injection
                        var parameters = new Dictionary<string, object> {
                            { "@couponCode", couponCode }
                        };
                        DataTable dtDiscount = code2.DatabaseQuerySafe(conn,
                            "SELECT * FROM [Taketime].[dbo].[Affiliate_Member] WHERE Coupon_Code = @couponCode",
                            parameters);

                        if (dtDiscount.Rows.Count >= 1)
                        {
                            Session["UseCoupon"] = "Affiliate";
                            TextBox19.Enabled = false;
                            TextBox12_TextChanged(null, null);
                            Button8.Text = "Clear";
                            TextBox6.Text += "Affiliate Code: " + couponCode + "\r\n";

                            LogCouponAttempt(couponCode, "Affiliate", "SUCCESS - Affiliate code applied");
                            ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('✅ ใช้รหัส Affiliate สำเร็จ!\\n\\nกรุณาเลือกห้องพักอีกครั้ง ระบบได้ปรับราคาตามส่วนลดเรียบร้อยแล้ว');", true);
                        }
                        else
                        {
                            Button8.Text = "Submit";
                            Session["UseCoupon"] = "false";
                            TextBox19.Text = string.Empty;

                            LogCouponAttempt(couponCode, "Affiliate", "FAILED - Code not found");
                            ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('❌ ไม่พบรหัส Affiliate นี้ในระบบ\\n\\nกรุณาตรวจสอบว่า:\\n- พิมพ์ถูกต้อง (ตัวพิมพ์เล็ก/ใหญ่)\\n- รหัสมี 8 ตัวอักษร\\n- รหัสยังใช้งานได้\\n\\nAffiliate code not found. Please check the code and try again.');", true);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogCouponAttempt(couponCode, "Affiliate", "ERROR - " + ex.Message);
                        ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('❌ เกิดข้อผิดพลาดในการตรวจสอบรหัส Affiliate\\n\\nกรุณาลองใหม่อีกครั้ง หรือติดต่อเจ้าหน้าที่');", true);
                    }
                }
                // Voucher Code (13 characters starting with v/V)
                else if (couponCode.Length == 13 && (couponCode[0] == 'v' || couponCode[0] == 'V'))
                {
                    // Check if date is selected
                    if (string.IsNullOrEmpty(TextBox12.Text))
                    {
                        LogCouponAttempt(couponCode, "Voucher", "FAILED - No check-in date selected");
                        ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('❌ กรุณาเลือกวันที่ Check-In ก่อน\\n\\nPlease select check-in date before applying voucher code.');", true);
                        return;
                    }

                    DataTable dtVoucher = new DataTable();
                    try
                    {
                        LogCouponAttempt(couponCode, "Voucher", "Validating voucher code for date: " + TextBox12.Text);

                        // 🔒 SECURE: Using parameterized query to prevent SQL Injection
                        var voucherParams = new Dictionary<string, object> {
                            { "@voucherNumber", couponCode },
                            { "@expiredDate", code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToInt32(DropDownList1.SelectedValue) - 1).ToString("yyyy-MM-dd") }
                        };
                        dtVoucher = code2.DatabaseQuerySafe(conn,
                            @"SELECT * FROM [Taketime].[dbo].[Voucher]
                              INNER JOIN Voucher_RatePlan_Group ON Voucher_RatePlan_Group.Voucher_Number = Voucher.Voucher_Number
                              INNER JOIN Accommodation_RatePlan_Group ON Rateplan_GroupID = Accommodation_RatePlan_Group.GroupID
                              INNER JOIN Accommodation_RatePlan ON Accommodation_RatePlan.ID = Rateplan_ID
                              WHERE Voucher.Voucher_Number = @voucherNumber
                              AND Used_Status = 'False'
                              AND Expired_Date >= @expiredDate",
                            voucherParams);

                        if (dtVoucher.Rows.Count >= 1)
                        {
                            Session["UseCoupon"] = "Voucher";
                            TextBox19.Enabled = false;
                            TextBox12_TextChanged(null, null);
                            Button8.Text = "Clear";
                            TextBox6.Text += "Voucher No." + couponCode + "\r\n";

                            LogCouponAttempt(couponCode, "Voucher", "SUCCESS - Voucher applied");
                            ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('✅ ใช้ Voucher สำเร็จ!\\n\\nกรุณาเลือกห้องพักอีกครั้ง ระบบได้ปรับราคาตามส่วนลดเรียบร้อยแล้ว');", true);
                        }
                        else
                        {
                            // Check if voucher exists but is used or expired
                            var checkParams = new Dictionary<string, object> {
                                { "@voucherNumber", couponCode }
                            };
                            DataTable dtVoucherCheck = code2.DatabaseQuerySafe(conn,
                                "SELECT Used_Status, Expired_Date FROM [Taketime].[dbo].[Voucher] WHERE Voucher_Number = @voucherNumber",
                                checkParams);

                            Button8.Text = "Submit";
                            Session["UseCoupon"] = "false";
                            TextBox19.Text = string.Empty;

                            if (dtVoucherCheck.Rows.Count >= 1)
                            {
                                string usedStatus = dtVoucherCheck.Rows[0]["Used_Status"].ToString();
                                DateTime expiredDate = Convert.ToDateTime(dtVoucherCheck.Rows[0]["Expired_Date"]);
                                DateTime checkOutDate = code2.ParseDate(TextBox12.Text).Value.AddDays(Convert.ToInt32(DropDownList1.SelectedValue) - 1);

                                if (usedStatus == "True")
                                {
                                    LogCouponAttempt(couponCode, "Voucher", "FAILED - Already used");
                                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('❌ Voucher นี้ถูกใช้งานไปแล้ว\\n\\nVoucher has already been used.');", true);
                                }
                                else if (expiredDate < checkOutDate)
                                {
                                    LogCouponAttempt(couponCode, "Voucher", "FAILED - Expired on " + expiredDate.ToString("yyyy-MM-dd"));
                                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('❌ Voucher นี้หมดอายุแล้ว\\n\\nวันหมดอายุ: " + expiredDate.ToString("dd/MM/yyyy") + "\\nวันที่เลือก Check-Out: " + checkOutDate.ToString("dd/MM/yyyy") + "\\n\\nVoucher has expired.');", true);
                                }
                                else
                                {
                                    LogCouponAttempt(couponCode, "Voucher", "FAILED - Not applicable for selected room type");
                                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('❌ Voucher นี้ไม่สามารถใช้กับห้องพักประเภทที่เลือกได้\\n\\nVoucher is not valid for selected accommodation type.');", true);
                                }
                            }
                            else
                            {
                                LogCouponAttempt(couponCode, "Voucher", "FAILED - Code not found");
                                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('❌ ไม่พบ Voucher นี้ในระบบ\\n\\nกรุณาตรวจสอบว่า:\\n- พิมพ์ถูกต้อง (ตัวพิมพ์เล็ก/ใหญ่)\\n- รหัสขึ้นต้นด้วย v หรือ V\\n- รหัสมี 13 ตัวอักษร\\n\\nVoucher not found. Please check the code and try again.');", true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogCouponAttempt(couponCode, "Voucher", "ERROR - " + ex.Message);
                        ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('❌ เกิดข้อผิดพลาดในการตรวจสอบ Voucher\\n\\nข้อผิดพลาด: " + ex.Message + "\\n\\nกรุณาลองใหม่อีกครั้ง');", true);
                    }
                }
                else
                {
                    // Invalid format
                    string errorMsg = "❌ รูปแบบรหัสส่วนลดไม่ถูกต้อง\\n\\n";
                    errorMsg += "รหัส Affiliate: ต้องมี 8 ตัวอักษร\\n";
                    errorMsg += "รหัส Voucher: ต้องขึ้นต้นด้วย v หรือ V และมี 13 ตัวอักษร\\n\\n";
                    errorMsg += "รหัสที่ใส่: " + couponCode + " (" + couponCode.Length + " ตัวอักษร)\\n\\n";
                    errorMsg += "Invalid coupon format. Please check the code length and format.";

                    LogCouponAttempt(couponCode, "Unknown", "FAILED - Invalid format (length: " + couponCode.Length + ")");
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('" + errorMsg + "');", true);
                }
            }
            else if (Button8.Text == "Clear")
            {
                Button8.Text = "Submit";
                Session["UseCoupon"] = "false";
                string clearedCode = TextBox19.Text;
                TextBox19.Text = string.Empty;
                TextBox19.Enabled = true;
                TextBox12_TextChanged(null, null);

                LogCouponAttempt(clearedCode, "Clear", "Coupon cleared by user");
            }
        }

        /// <summary>
        /// Logs coupon validation attempts for debugging and tracking
        /// </summary>
        private void LogCouponAttempt(string couponCode, string couponType, string result)
        {
            try
            {
                string phoneNumber = string.IsNullOrEmpty(TextBox1.Text) ? "N/A" : TextBox1.Text;
                string checkInDate = string.IsNullOrEmpty(TextBox12.Text) ? "N/A" : TextBox12.Text;
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Coupon: {couponCode} | Type: {couponType} | Phone: {phoneNumber} | Check-in: {checkInDate} | Result: {result}";

                // Log to application log file
                System.IO.File.AppendAllText(
                    Server.MapPath("~/Logs/CouponValidation.log"),
                    logMessage + Environment.NewLine
                );
            }
            catch
            {
                // Fail silently - don't break the user experience if logging fails
            }
        }

        protected void CheckBox7_CheckedChanged(object sender, EventArgs e)
        {
            if(CheckBox7.Checked == true)
            {
                Panel2.Visible = true;
            }
            else
            {
                Panel2.Visible = false;
            }
        }


        // 🆕 Load Payment History for CheckIn/Edit/RentMore modes
        private void LoadPaymentHistory()
        {
            try
            {
                string reservationId = Request.QueryString["id"];
                if (string.IsNullOrEmpty(reservationId))
                {
                    divPaymentHistory.Visible = false;
                    return;
                }

                // Query payment history from Payment_History + Payment_Slips
                string query = @"
                    SELECT
                        ph.PaymentDate,
                        ph.PaymentAmount,
                        ph.PaymentType,
                        ph.PaymentMethod,
                        ph.Status,
                        ph.Receipt_ID as ReceiptNumber,
                        ps.SlipFileURL,
                        a.Username as ProcessedBy
                    FROM Payment_History ph
                    LEFT JOIN Payment_Slips ps ON ph.Receipt_ID = ps.Account_Receipt_ID AND ps.IsActive = 1
                    LEFT JOIN Admin a ON ph.ProcessedBy_AdminID = a.ID
                    WHERE ph.Reservation_ID = @ReservationId
                      AND ph.Status = 'COMPLETED'
                    ORDER BY ph.PaymentDate DESC";

                var parameters = new Dictionary<string, object>
                {
                    { "@ReservationId", reservationId }
                };

                DataTable dtHistory = code2.DatabaseQuerySafe(conn, query, parameters);

                if (dtHistory.Rows.Count > 0)
                {
                    gvPaymentHistory.DataSource = dtHistory;
                    gvPaymentHistory.DataBind();
                    divPaymentHistory.Visible = true;

                    // Hide old slip image when showing GridView
                    Image1.Visible = false;
                }
                else
                {
                    // No payment history - keep showing old slip image
                    divPaymentHistory.Visible = false;
                    Image1.Visible = true;
                }
            }
            catch (Exception ex)
            {
                // Log error but don't break the page
                code2.Logs(conn, "LoadPaymentHistory Error",
                    $"Reservation ID: {Request.QueryString["id"]}, Error: {ex.Message}",
                    "SYSTEM");

                divPaymentHistory.Visible = false;
                Image1.Visible = true;
            }
        }

        // Helper method to generate slip link HTML
        protected string GetSlipLink(object slipFileURL)
        {
            try
            {
                if (slipFileURL == null || slipFileURL == DBNull.Value)
                {
                    return "<span style='color: #95a5a6;'>-</span>";
                }

                string slipPath = slipFileURL.ToString().Trim();
                if (string.IsNullOrEmpty(slipPath))
                {
                    return "<span style='color: #95a5a6;'>-</span>";
                }

                // Remove leading "./" or "/" if present
                slipPath = slipPath.TrimStart('.', '/');

                // Build full URL
                string fullUrl = ResolveUrl("~/" + slipPath);
                return $"<a href='{fullUrl}' target='_blank' style='display: inline-block; padding: 5px 10px; background-color: #3498db; color: white; text-decoration: none; border-radius: 3px; font-size: 12px;'>📄 ดูสลิป</a>";
            }
            catch
            {
                return "<span style='color: #e74c3c;'>ข้อผิดพลาด</span>";
            }
        }

        // 🏨 Load Product Charges for CheckIn/Edit/RentMore modes
        private void LoadProductCharges()
        {
            try
            {
                string reservationId = Request.QueryString["id"];
                if (string.IsNullOrEmpty(reservationId))
                {
                    divProductCharges.Visible = false;
                    return;
                }

                int resId = Convert.ToInt32(reservationId);
                DataTable dtCharges = _roomChargeDA.GetReservationCharges(resId);

                // 🔧 Filter out CANCELLED charges (don't display deleted items)
                DataView dv = dtCharges.DefaultView;
                dv.RowFilter = "Status <> 'CANCELLED'";
                DataTable dtFiltered = dv.ToTable();

                if (dtFiltered.Rows.Count > 0)
                {
                    divProductCharges.Visible = true;
                    gvProductCharges.DataSource = dtFiltered;
                    gvProductCharges.DataBind();

                    // Calculate and display summary
                    decimal totalPending = _roomChargeDA.GetTotalPendingCharges(resId);
                    decimal totalPaid = 0;
                    decimal totalCancelled = 0;

                    foreach (DataRow row in dtFiltered.Rows)
                    {
                        string status = row["Status"].ToString();
                        decimal amount = Convert.ToDecimal(row["TotalAmount"]);

                        if (status == "PAID")
                            totalPaid += amount;
                        else if (status == "CANCELLED")
                            totalCancelled += amount;
                    }

                    lblProductChargesSummary.Text = string.Format(
                        "📊 สรุป: <strong>รอชำระ {0:N2} บาท</strong> | ชำระแล้ว {1:N2} บาท | รวมทั้งหมด {2} รายการ",
                        totalPending, totalPaid, dtFiltered.Rows.Count);
                }
                else
                {
                    divProductCharges.Visible = false;
                }
            }
            catch (Exception ex)
            {
                // Log error but don't break the page
                code2.Logs(conn, "LoadProductCharges Error",
                    $"Reservation ID: {Request.QueryString["id"]}, Error: {ex.Message}",
                    "SYSTEM");

                divProductCharges.Visible = false;
            }
        }

        // 🏨 Handle Product Charge Delete Command
        protected void gvProductCharges_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "DeleteCharge")
            {
                try
                {
                    long chargeId = Convert.ToInt64(e.CommandArgument);
                    int? adminId = Session["UserID"] != null ?
                        Convert.ToInt32(Session["UserID"]) : (int?)null;

                    // Cancel the charge (returns stock and updates reservation total)
                    _roomChargeService.CancelRoomCharge(chargeId, adminId,
                        "ลบโดยผู้ใช้จากหน้า Reserve");

                    // Reload the charges display
                    LoadProductCharges();

                    // Reload reservation data to update totals
                    string command = Request.QueryString["command"];
                    string id = Request.QueryString["id"];
                    string check = Request.QueryString["check"];

                    if (!string.IsNullOrEmpty(id))
                    {
                        // 🔒 SECURE: Using parameterized query
                        var parameters = new Dictionary<string, object>
                        {
                            { "@id", id },
                            { "@phone", check }
                        };

                        DataTable dtReservation = code2.DatabaseQuerySafe(conn,
                            "SELECT * FROM [Reservation] WHERE ID = @id AND Customer_MobilePhone = @phone",
                            parameters);

                        if (dtReservation.Rows.Count > 0)
                        {
                            // Update displayed total price
                            TextBox4.Text = dtReservation.Rows[0]["TotalPrice"].ToString();
                        }
                    }

                    // Log success (removed alert to prevent blocking postback)
                    code2.Logs(conn, "gvProductCharges_RowCommand Success",
                        $"Deleted ChargeID: {chargeId}",
                        adminId?.ToString() ?? "SYSTEM");
                }
                catch (Exception ex)
                {
                    // Log error (removed alert to prevent blocking postback)
                    code2.Logs(conn, "gvProductCharges_RowCommand Error",
                        $"ChargeID: {e.CommandArgument}, Error: {ex.Message}",
                        Session["User"]?.ToString() ?? "SYSTEM");
                }
            }
        }

        // 🏨 Add pending product charges to receipt DataTable
        private void AddProductChargesToReceipt(int reservationId, DataTable dtReserve)
        {
            try
            {
                // Get all pending charges for this reservation
                DataTable dtCharges = _roomChargeDA.GetReservationCharges(reservationId, "PENDING");

                if (dtCharges.Rows.Count > 0)
                {
                    foreach (DataRow charge in dtCharges.Rows)
                    {
                        // Add product charge to receipt with ProductType_ID = 3
                        dtReserve.Rows.Add(
                            dtReserve.Rows.Count + 1,  // Number
                            "",  // Receipt_ID (will be set later)
                            "3",  // ProductType_ID = 3 for product charges
                            charge["Product_ID"],
                            charge["Product_Name"].ToString(),  // Product_Data
                            charge["Quantity"],  // Product_Amount
                            "ชิ้น",  // Product_Unit
                            charge["UnitPrice"],  // Price_PerPeice
                            charge["TotalAmount"]  // Price_Amount
                        );
                    }

                    code2.Logs(conn, "AddProductChargesToReceipt",
                        $"Added {dtCharges.Rows.Count} product charges to receipt for Reservation {reservationId}",
                        Session["User"]?.ToString() ?? "SYSTEM");
                }
            }
            catch (Exception ex)
            {
                code2.Logs(conn, "AddProductChargesToReceipt Error",
                    $"Reservation ID: {reservationId}, Error: {ex.Message}",
                    Session["User"]?.ToString() ?? "SYSTEM");
                // Don't throw - allow receipt creation to continue without product charges
            }
        }

        // 🏨 Mark product charges as paid after receipt is created
        private void MarkProductChargesAsPaid(int reservationId, string receiptId)
        {
            try
            {
                int affectedRows = _roomChargeService.MarkAllChargesAsPaid(reservationId, receiptId);

                if (affectedRows > 0)
                {
                    code2.Logs(conn, "MarkProductChargesAsPaid",
                        $"Marked {affectedRows} charges as paid for Reservation {reservationId}, Receipt {receiptId}",
                        Session["User"]?.ToString() ?? "SYSTEM");
                }
            }
            catch (Exception ex)
            {
                code2.Logs(conn, "MarkProductChargesAsPaid Error",
                    $"Reservation ID: {reservationId}, Receipt ID: {receiptId}, Error: {ex.Message}",
                    Session["User"]?.ToString() ?? "SYSTEM");
                // Don't throw - receipt is already created, this is just supplementary
            }
        }

        // 🔧 Event handler when number of guests is changed
        protected void txtPeopleStay_TextChanged(object sender, EventArgs e)
        {
            // Page_Load will automatically recalculate prices when postback occurs
            // This event handler exists to trigger the postback
            // No additional code needed here - prices are calculated in Page_Load (lines 286-547)
        }
    }
}