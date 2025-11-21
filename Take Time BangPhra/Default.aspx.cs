using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Take_Time_BangPhra
{
    public partial class _Default2 : Page
    {
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        async protected void Page_Load(object sender, EventArgs e)
        {
            this.MaintainScrollPositionOnPostBack = true;
            string selecteddate = "";
            Uri myUri = new Uri(HttpContext.Current.Request.Url.AbsoluteUri);
            selecteddate = HttpUtility.ParseQueryString(myUri.Query).Get("selecteddate");
            Page.MaintainScrollPositionOnPostBack = true;
            if (!IsPostBack)
            {
                try
                {
                    // SECURE: Log access with parameterized query
                    var logParams = new Dictionary<string, object>
                    {
                        { "@AccessDateTime", DateTime.Now.ToString() },
                        { "@DeviceName", System.Net.Dns.GetHostEntry(HttpContext.Current.Request.UserHostName.ToString()).HostName },
                        { "@DeviceIP", HttpContext.Current.Request.UserHostName.ToString() },
                        { "@Browser", HttpContext.Current.Request.Browser.Browser }
                    };
                    DatabaseInsertSafe(conn,
                        code.AdaptSql("INSERT INTO [dbo].[Logs_Access] ([AccessDateTime],[DeviceName],[DeviceIP],Browser) " +
                        "VALUES(@AccessDateTime,@DeviceName,@DeviceIP,@Browser)"),
                        logParams);
                }
                catch
                {
                    // SECURE: Fallback log access with parameterized query
                    var fallbackLogParams = new Dictionary<string, object>
                    {
                        { "@AccessDateTime", DateTime.Now.ToString() },
                        { "@DeviceName", HttpContext.Current.Request.UserHostName.ToString() },
                        { "@DeviceIP", HttpContext.Current.Request.UserHostName.ToString() },
                        { "@Browser", HttpContext.Current.Request.Browser.Browser }
                    };
                    DatabaseInsertSafe(conn,
                        code.AdaptSql("INSERT INTO [dbo].[Logs_Access] ([AccessDateTime],[DeviceName],[DeviceIP],Browser) " +
                        "VALUES(@AccessDateTime,@DeviceName,@DeviceIP,@Browser)"),
                        fallbackLogParams);
                }
                if(selecteddate != "" && selecteddate != null)
                {
                    Calendar1.SelectedDate = Convert.ToDateTime(selecteddate);
                    Calendar1.DataBind();
                    Calendar1_SelectionChanged(null, null);
                }
                else
                {
                    Calendar1.SelectedDate = DateTime.Now.Date;
                    Calendar1.DataBind();
                    Calendar1_SelectionChanged(null, null);
                }
                // SECURE: Get recent reviews with parameterized query
                var reviewParams = new Dictionary<string, object>
                {
                    { "@WeekAgoDate", DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd") }
                };
                DataTable dtReviews = DatabaseQuerySafe(conn,
                    code.AdaptSql("SELECT * FROM Reviews WHERE [Date] > @WeekAgoDate"),
                    reviewParams);

                string jsonResponse = "";
                if ( dtReviews.Rows.Count > 0)
                {
                    jsonResponse = dtReviews.Rows[0]["json"].ToString();
                }
                else
                {
                    jsonResponse = await FetchGoogleReviews();

                    // SECURE: Insert new review with parameterized query
                    var insertReviewParams = new Dictionary<string, object>
                    {
                        { "@ReviewDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                        { "@JsonData", jsonResponse }
                    };
                    DatabaseInsertSafe(conn,
                        code.AdaptSql("INSERT INTO [dbo].[Reviews] ([Date],[json]) VALUES (@ReviewDate,@JsonData)"),
                        insertReviewParams);
                }
                 
            // Assign the response to a Literal as a JavaScript variable
            Literal1.Text = $"<script>var jsoninput = {jsonResponse};</script>";

            }
            try
            {
                if (Session["permission"].ToString() == "True")
                {
                }
                else
                {
                    Session["permission"] = "No";
                }
            }
            catch
            {
                Session["permission"] = "No";
            }
        }

        private async Task<string> FetchGoogleReviews()
        {
            string apiUrl = "https://maps.googleapis.com/maps/api/place/details/json?placeid=ChIJvUgTD9nLAjERMgFSAIuRHJw&key=AIzaSyDKULLtZZUAqQmgbW9kaTy_SPt4o-Jcp8U&language=th";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();
                    return content; // Return raw JSON response
                }
            }
            catch (Exception ex)
            {
                // Return an error message if API call fails
                return $"{{\"error\": \"{ex.Message}\"}}";
            }
        }

        protected void Calendar1_SelectionChanged(object sender, EventArgs e)
        {
            Label1.Visible = true;

            if (GridView1.SelectedIndex >= 0)
            {
                
                Response.Redirect("./Default.aspx?selecteddate="+ Calendar1.SelectedDate.ToString("yyyy-MM-dd"));
                
            }
            if (DateTime.Now > Calendar1.SelectedDate.AddDays(1) && Session["permission"] == "No")
            {
                GridView1.Visible = false;
                
            }
            else
            {
                GridView1.Visible = true;
                Label1.Text = Calendar1.SelectedDate.ToString("dd MMMM yyyy");

                // SECURE: Get reservations with parameterized query
                var reservationParams = new Dictionary<string, object>
                {
                    { "@SelectedDate", Calendar1.SelectedDate.ToString("yyyy-MM-dd") }
                };
                DataTable dtReservation = DatabaseQuerySafe(conn,
                    code.AdaptSql("SELECT * FROM Reservation RIGHT JOIN Reservation_Accommodation ON Reservation.ID = Reservation_Accommodation.Reservation_ID " +
                    "INNER JOIN Accommodation ON Accommodation.ID = Reservation_Accommodation.Accommodation_ID " +
                    "WHERE @SelectedDate >= CheckinDate AND @SelectedDate < CheckoutDate"),
                    reservationParams);
                DataTable dtAccommodation = DatabaseQuery(conn, code.AdaptSql("Select * From Accommodation Where Status = 1 order by OrderID asc"));
                try
                {
                    dtAccommodation.Columns.Add("StatusOnDate");
                }
                catch
                {
                }

                for (int j = 0; j < dtAccommodation.Rows.Count; j++)
                {
                    int check = 0;
                    int totalAmount = 0;
                    for (int i = 0; i < dtReservation.Rows.Count; i++)
                    {
                        if (dtReservation.Rows[i]["Accommodation_ID"].ToString() == dtAccommodation.Rows[j]["ID"].ToString())
                        {
                            check = 1;
                        }
                        if (dtReservation.Rows[i]["LimitWithPeople"].ToString() == "True" && (!DBNull.Value.Equals(dtReservation.Rows[i]["Amount"])) && dtReservation.Rows[i]["Accommodation_ID"].ToString() == dtAccommodation.Rows[j]["ID"].ToString())
                        {
                            totalAmount += Convert.ToInt32(dtReservation.Rows[i]["Amount"].ToString());
                        }
                    }
                    if (check == 1)
                    {
                        dtAccommodation.Rows[j]["StatusOnDate"] = "ไม่ว่าง (Not available)";
                    }
                    else
                    {
                        dtAccommodation.Rows[j]["StatusOnDate"] = "ว่าง (Available)";
                    }

                    if (dtAccommodation.Rows[j]["LimitWithPeople"].ToString() == "True")
                    {
                        if (totalAmount >= Convert.ToInt32(dtAccommodation.Rows[j]["People"].ToString()))
                        {
                            dtAccommodation.Rows[j]["StatusOnDate"] = "ไม่ว่าง (Not available)";
                            //dtAccommodation.Rows[j]["People"] = Convert.ToInt32(dtAccommodation.Rows[j]["People"].ToString()) - totalAmount;
                        }
                        else
                        {
                            dtAccommodation.Rows[j]["StatusOnDate"] = "ว่าง (Available)";
                            dtAccommodation.Rows[j]["People"] = Convert.ToInt32(dtAccommodation.Rows[j]["People"].ToString()) - totalAmount;
                        }
                    }
                }
                dtAccommodation.AcceptChanges();
                Session["dtAccom"] = dtAccommodation;
                GridView1.DataSource = dtAccommodation;
                GridView1.DataBind();

                foreach (GridViewRow row in GridView1.Rows)
                {
                    //Button btSelect = (Button)row.Cells[0].Controls[0];
                    //btSelect.Enabled = false;

                    if (row.Cells[2].Text == "ว่าง (Available)")
                    {
                        row.BackColor = System.Drawing.ColorTranslator.FromHtml("#8D9F7F");
                        //row.ForeColor = System.Drawing.Color.White;
                    }
                    else
                    {
                        row.BackColor = System.Drawing.ColorTranslator.FromHtml("#BC8F8F");
                        Button btReserve = (Button)row.Cells[4].Controls[0];
                        btReserve.Enabled = false;
                        //row.ForeColor = System.Drawing.Color.Black;
                    }
                }
            }
            
        }

        public int DatabaseInsert(string connStr, string cmd)
        {
            int ID = 0;
            using (SqlConnection connection = new SqlConnection(connStr))
            {
                using (SqlCommand command = new SqlCommand(cmd, connection))
                {
                    cmd = cmd.Replace("&nbsp;", "");
                    connection.Open();
                    try
                    {
                        ID = Convert.ToInt32(command.ExecuteScalar().ToString());
                    }
                    catch
                    {
                        //command.ExecuteNonQuery();
                    }
                    connection.Close();
                }
            }
            return ID;
        }

        public DataTable DatabaseQuery(string connStr, string cmd)
        {
            DataTable dt = new DataTable();

            using (SqlConnection con = new SqlConnection(connStr))
            {

                try
                {
                    cmd = cmd.Replace("&amp;", "&");
                    cmd = cmd.Replace("&#39;", "''");
                    cmd = cmd.Replace("&nbsp;", "");

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd, con);
                    adapter.Fill(dt);
                }
                catch { }
            }
            return dt;
        }

        /// <summary>
        /// Upserts customer data - delegates to code class's UpsertCustomer method
        /// This method exists in _Default2 class for compatibility
        /// </summary>
        public long UpsertCustomer(
            string connStr,
            string mobilePhone,
            string name,
            string nickName,
            string comeFrom,
            string remark,
            string fullName,
            string address,
            string idNumber,
            string email,
            int customerTypeID,
            int addressID,
            string address1,
            string branchNumber)
        {
            // Delegate to the code class's UpsertCustomer method
            return code.UpsertCustomer(
                connStr,
                mobilePhone,
                name,
                nickName,
                comeFrom,
                remark,
                fullName,
                address,
                idNumber,
                email,
                customerTypeID,
                addressID,
                address1,
                branchNumber
            );
        }

        public string createDocNumber(string conn, string tablename, string doctype,string Year,string Month,string Day)
        {
            // SECURE: Whitelist validation for table names
            string[] allowedTables = { "Account_Receipt", "Account_Payment", "Reservation" };
            if (!allowedTables.Contains(tablename))
            {
                throw new ArgumentException("Invalid table name");
            }

            string output = "";
            Year = Year.Substring(Year.Length - 2);

            if(Convert.ToInt32(Month) < 10)
            {
                Month = "0" + Month;
            }

            if (Convert.ToInt32(Day) < 10)
            {
                Day = "0" + Day;
            }

            // SECURE: Parameterized query for document number lookup
            var docParams = new Dictionary<string, object>
            {
                { "@Pattern", doctype + Year + Month + Day + "%" }
            };
            DataTable dt = DatabaseQuerySafe(conn,
                code.AdaptSql("SELECT TOP 1 ID FROM [" + tablename + "] WHERE ID LIKE @Pattern ORDER BY ID DESC"),
                docParams);
            int Number = 0;
            string NumberStr = "";
            if (dt.Rows.Count > 0)
            {
                string lastnumber = dt.Rows[0][0].ToString().Substring(dt.Rows[0][0].ToString().Length - 3,3);
                Number = Convert.ToInt32(lastnumber);
                Number = Number + 1;
                if (Number < 10)
                {
                    NumberStr = "00" + Number.ToString();
                }
                else if (Number < 100)
                {
                    NumberStr = "0" + Number.ToString();

                }
                else if (Number < 1000)
                {
                    NumberStr = Number.ToString();
                }
            }
            else
            {
                NumberStr = "001";
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
            output = doctype + Year + Month + Day + NumberStr;
            return output;
        }

        protected void Calendar1_DayRender(object sender, DayRenderEventArgs e)
        {
            if (GridView1.SelectedIndex >= 0)
            {
                if(DateTime.Now.AddDays(-1) <= e.Day.Date)
                {
                    int checkhave = 0;
                    int countPeople = 0;

                    // SECURE: Get reservations with parameterized query
                    var dayReservationParams = new Dictionary<string, object>
                    {
                        { "@DayDate", e.Day.Date.ToString("yyyy-MM-dd") }
                    };
                    DataTable dtReservation = DatabaseQuerySafe(conn,
                        code.AdaptSql("SELECT * FROM Reservation RIGHT JOIN Reservation_Accommodation ON Reservation.ID = Reservation_Accommodation.Reservation_ID " +
                        "INNER JOIN Accommodation ON Accommodation.ID = Accommodation_ID " +
                        "WHERE @DayDate >= CheckinDate AND @DayDate < CheckoutDate"),
                        dayReservationParams);

                    // SECURE: Get accommodation with parameterized query
                    var accomParams = new Dictionary<string, object>
                    {
                        { "@AccomName", GridView1.SelectedRow.Cells[1].Text }
                    };
                    DataTable dtAccommodation = DatabaseQuerySafe(conn,
                        code.AdaptSql("SELECT * FROM Accommodation WHERE Status = 1 AND AccomName = @AccomName"),
                        accomParams);
                    for (int i = 0; i < dtReservation.Rows.Count; i++)
                    {
                        if( (dtReservation.Rows[i]["AccomName"].ToString() == GridView1.SelectedRow.Cells[1].Text && dtReservation.Rows[i]["LimitWithPeople"].ToString().ToLower() == "false") )
                        {
                            checkhave = 1;
                        }
                        else if((dtReservation.Rows[i]["AccomName"].ToString() == GridView1.SelectedRow.Cells[1].Text && dtReservation.Rows[i]["LimitWithPeople"].ToString().ToLower() == "true"))
                        {
                            countPeople += Convert.ToInt32(dtReservation.Rows[i]["Amount"].ToString());
                        }
                    }
                    if(checkhave == 1 || Convert.ToInt32(dtAccommodation.Rows[0]["People"].ToString()) <= countPeople)
                    {
                        e.Cell.BackColor = System.Drawing.ColorTranslator.FromHtml("#BC8F8F");
                    }
                    else
                    {

                        e.Cell.BackColor = System.Drawing.ColorTranslator.FromHtml("#8D9F7F");
                    }
                }
                else
                {
                    e.Cell.ForeColor = System.Drawing.Color.Transparent;
                    
                }
                

            }
            else
            {
                if (DateTime.Now.AddDays(-1) > e.Day.Date && Session["permission"] == "No")
                {
                    e.Cell.ForeColor = System.Drawing.Color.Transparent;
                }
                else
                {

                    if (e.Day.Date == Calendar1.SelectedDate)
                    {

                    }
                    else
                    {
                        e.Cell.BackColor = System.Drawing.Color.Transparent;
                    }

                    // SECURE: Get reservations with parameterized query
                    var elseReservationParams = new Dictionary<string, object>
                    {
                        { "@DayDate", e.Day.Date.ToString("yyyy-MM-dd") }
                    };
                    DataTable dtReservation = DatabaseQuerySafe(conn,
                        code.AdaptSql("SELECT * FROM Reservation RIGHT JOIN Reservation_Accommodation ON Reservation.ID = Reservation_Accommodation.Reservation_ID " +
                        "WHERE @DayDate >= CheckinDate AND @DayDate < CheckoutDate"),
                        elseReservationParams);
                    DataTable dtAccommodation = DatabaseQuery(conn, code.AdaptSql("Select * From Accommodation Where Status = 1 order by ID asc"));
                    int maxAccommodation = dtAccommodation.Rows.Count;
                    int totalAmount = 0;

                    for (int j = 0; j < dtAccommodation.Rows.Count; j++)
                    {
                        for (int i = 0; i < dtReservation.Rows.Count; i++)
                        {
                            if (dtAccommodation.Rows[j]["ID"].ToString() == dtReservation.Rows[i]["Accommodation_ID"].ToString() || dtAccommodation.Rows[j]["LimitWithPeople"].ToString() == "True")
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
            }

        }

        protected void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "Reserve")
            {
                DataTable dt = (DataTable)Session["dtAccom"];
                Response.Redirect("./Reserve?command=reserve&date=" + Calendar1.SelectedDate.ToString("yyyy-MM-dd") + "&accom=" + dt.Rows[Convert.ToInt32(e.CommandArgument)]["ID"].ToString());
            }
        }

        protected void GridView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Label1.Visible = false;

            if (GridView1.SelectedRow.Cells[1].Text.Contains("กระโจม"))
            {
                //Image1.Visible = true;
                //Image2.Visible = false;
                //Image3.Visible = false;
                //Image4.Visible = false;
                //Image5.Visible = false;
                //Image6.Visible = false;
                //Image7.Visible = false;


                //Image1.DataBind();
                //Image2.DataBind();
                //Image3.DataBind();
                //Image4.DataBind();
                //Image5.DataBind();
                //Image6.DataBind();
                //Image7.DataBind();
            }
            else if (GridView1.SelectedRow.Cells[1].Text.Contains("กระจก"))
            {
                //Image1.Visible = false;
                //Image2.Visible = true;
                //Image3.Visible = false;
                //Image4.Visible = false;
                //Image5.Visible = false;
                //Image6.Visible = false;
                //Image7.Visible = false;


                //Image1.DataBind();
                //Image2.DataBind();
                //Image3.DataBind();
                //Image4.DataBind();
                //Image5.DataBind();
                //Image6.DataBind();
                //Image7.DataBind();
            }
            else if (GridView1.SelectedRow.Cells[1].Text.Contains("วิลล่า"))
            {
                //Image1.Visible = false;
                //Image2.Visible = false;
                //Image3.Visible = true;
                //Image4.Visible = false;
                //Image5.Visible = false;
                //Image6.Visible = false;
                //Image7.Visible = false;


                //Image1.DataBind();
                //Image2.DataBind();
                //Image3.DataBind();
                //Image4.DataBind();
                //Image5.DataBind();
                //Image6.DataBind();
                //Image7.DataBind();
            }
            else if (GridView1.SelectedRow.Cells[1].Text.Contains("แคมป์ปิ้ง"))
            {
                //Image1.Visible = false;
                //Image2.Visible = false;
                //Image3.Visible = false;
                //Image4.Visible = false;
                //Image5.Visible = true;
                //Image6.Visible = false;
                //Image7.Visible = false;


                //Image1.DataBind();
                //Image2.DataBind();
                //Image3.DataBind();
                //Image4.DataBind();
                //Image5.DataBind();
                //Image6.DataBind();
                //Image7.DataBind();
            }
            else if (GridView1.SelectedRow.Cells[1].Text.Contains("โคซี่"))
            {
                //Image1.Visible = false;
                //Image2.Visible = false;
                //Image3.Visible = false;
                //Image4.Visible = false;
                //Image5.Visible = false;
                //Image6.Visible = true;
                //Image7.Visible = false;


                //Image1.DataBind();
                //Image2.DataBind();
                //Image3.DataBind();
                //Image4.DataBind();
                //Image5.DataBind();
                //Image6.DataBind();
                //Image7.DataBind();
            }
            else if (GridView1.SelectedRow.Cells[1].Text.Contains("นอร์ดิก"))
            {
                //Image1.Visible = false;
                //Image2.Visible = false;
                //Image3.Visible = false;
                //Image4.Visible = false;
                //Image5.Visible = false;
                //Image6.Visible = false;
                //Image7.Visible = true;


                //Image1.DataBind();
                //Image2.DataBind();
                //Image3.DataBind();
                //Image4.DataBind();
                //Image5.DataBind();
                //Image6.DataBind();
                //Image7.DataBind();
            }
            else
            {
                //Image1.Visible = false;
                //Image2.Visible = false;
                //Image3.Visible = false;
                //Image4.Visible = true;
                //Image5.Visible = false;
                //Image6.Visible = false;
                //Image7.Visible = false;


                //Image1.DataBind();
                //Image2.DataBind();
                //Image3.DataBind();
                //Image4.DataBind();
                //Image5.DataBind();
                //Image6.DataBind();
                //Image7.DataBind();
            }

            //Calendar1.VisibleDate = DateTime.Today;
            Calendar1.SelectedDates.Clear();
            foreach (GridViewRow row in GridView1.Rows)
            {
                if(GridView1.SelectedIndex == row.RowIndex)
                {
                    row.BackColor = System.Drawing.Color.Yellow;
                    row.Cells[2].Text = "";
                }
                else
                {
                    row.BackColor = System.Drawing.Color.Transparent;
                    row.Cells[2].Text = "";

                }
            }
        }
    }
}