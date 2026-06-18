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
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra
{
    public partial class _Default2 : Page
    {
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        DocumentHelper documentHelper;
        async protected void Page_Load(object sender, EventArgs e)
        {
            // Initialize DocumentHelper
            documentHelper = new DocumentHelper(conn);

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
                        { "@AccessDateTime", DateTime.Now },
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
                        { "@AccessDateTime", DateTime.Now },
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

                // 📢 โปรโมชั่น/โฆษณา — เด้ง popup หน้าแรก (ไม่ให้ล้มหน้าถ้ามี error)
                try { RenderPromotionPopup(); } catch { }

                // 🔄 Google Reviews - ดึงข้อมูลใหม่ทุก 7 วัน
                string jsonResponse = "";
                try
                {
                    // ดึงข้อมูล review ล่าสุด (TOP 1)
                    DataTable dtReviews = DatabaseQuery(conn,
                        code.AdaptSql("SELECT TOP 1 * FROM Reviews ORDER BY [Date] DESC"));

                    bool needRefresh = true;
                    const int CACHE_DAYS = 7;  // Refresh every 7 days

                    if (dtReviews.Rows.Count > 0)
                    {
                        DateTime lastFetchDate = Convert.ToDateTime(dtReviews.Rows[0]["Date"]);
                        DateTime cacheExpiryDate = lastFetchDate.AddDays(CACHE_DAYS);
                        DateTime now = DateTime.Now;

                        System.Diagnostics.Debug.WriteLine($"[Google Reviews] Last fetch: {lastFetchDate:yyyy-MM-dd HH:mm}");
                        System.Diagnostics.Debug.WriteLine($"[Google Reviews] Cache expires: {cacheExpiryDate:yyyy-MM-dd HH:mm}");
                        System.Diagnostics.Debug.WriteLine($"[Google Reviews] Current time: {now:yyyy-MM-dd HH:mm}");

                        // ✅ ใช้ cache เฉพาะเมื่อยังไม่หมดอายุ + เป็น JSON รีวิวที่ valid (status OK, มี result)
                        // กันกรณีเคย cache คำตอบ error ของ Google (เช่น OVER_QUERY_LIMIT/REQUEST_DENIED)
                        // ไว้ → หน้าแรกจะไม่มีรีวิวจนกว่าจะครบ 7 วัน
                        string cachedJson = dtReviews.Rows[0]["json"]?.ToString() ?? "";
                        if (lastFetchDate <= now && now < cacheExpiryDate && IsValidReviewJson(cachedJson))
                        {
                            jsonResponse = cachedJson;
                            needRefresh = false;
                            double daysUntilExpiry = (cacheExpiryDate - now).TotalDays;
                            System.Diagnostics.Debug.WriteLine($"[Google Reviews] ✅ Using cached data (expires in {daysUntilExpiry:F1} days)");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[Google Reviews] ⏰ Cache expired or invalid date, need refresh");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[Google Reviews] ⚠️ No cached data found, need to fetch");
                    }

                    if (needRefresh)
                    {
                        System.Diagnostics.Debug.WriteLine("[Google Reviews] 🔄 Fetching new data from Google API...");
                        jsonResponse = await FetchGoogleReviews();

                        // บันทึกเฉพาะเมื่อได้ JSON รีวิวที่ valid (status OK + มี result) เท่านั้น
                        // ไม่ทับ cache ที่ดีด้วยคำตอบ error ของ Google (status != OK) หรือ exception
                        if (IsValidReviewJson(jsonResponse))
                        {
                            // ลบข้อมูลเก่าทั้งหมด แล้ว insert ใหม่
                            DatabaseQuery(conn, code.AdaptSql("DELETE FROM Reviews"));

                            var insertReviewParams = new Dictionary<string, object>
                            {
                                { "@ReviewDate", DateTime.Now },
                                { "@JsonData", jsonResponse }
                            };
                            DatabaseInsertSafe(conn,
                                code.AdaptSql("INSERT INTO [dbo].[Reviews] ([Date],[json]) VALUES (@ReviewDate,@JsonData)"),
                                insertReviewParams);

                            System.Diagnostics.Debug.WriteLine("[Google Reviews] ✅ New data saved to cache");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[Google Reviews] ❌ API invalid response: {jsonResponse}");
                            // ใช้ cache เก่าได้เฉพาะถ้ายัง valid — ไม่งั้นปล่อย reviews ว่างให้ frontend แสดง "ไม่มีรีวิว"
                            string oldJson = dtReviews.Rows.Count > 0 ? dtReviews.Rows[0]["json"]?.ToString() ?? "" : "";
                            jsonResponse = IsValidReviewJson(oldJson) ? oldJson : "{\"result\":{\"reviews\":[]}}";
                            System.Diagnostics.Debug.WriteLine("[Google Reviews] ⚠️ Kept old cache / empty (did not overwrite good data)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Google Reviews] ❌ Exception: {ex.Message}");
                    jsonResponse = "{\"error\": \"Unable to load reviews\"}";
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

        /// <summary>
        /// ตรวจว่า JSON ที่ได้จาก Google Places เป็นคำตอบที่ใช้ได้จริง:
        /// parse ได้ + status = OK (หรือไม่มี status) + มี result. ใช้กันการ cache คำตอบ error
        /// (OVER_QUERY_LIMIT / REQUEST_DENIED / INVALID_REQUEST / NOT_FOUND / {"error":...})
        /// ทับข้อมูลรีวิวที่ดีไว้.
        /// </summary>
        private static bool IsValidReviewJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var o = JObject.Parse(json);
                if (o["error"] != null) return false;
                string status = o["status"]?.ToString();
                if (!string.IsNullOrEmpty(status) && !string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                    return false;
                return o["result"] != null;   // reviews อาจว่างได้ถ้าสถานที่ไม่มีรีวิว
            }
            catch { return false; }
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

        /// <summary>
        /// สร้าง popup โปรโมชั่นหน้าแรกจากรายการที่ active + ตั้งให้เด้งหน้าแรก
        /// แสดงครั้งเดียวต่อวันต่อเบราว์เซอร์ (เก็บสถานะใน localStorage)
        /// </summary>
        private void RenderPromotionPopup()
        {
            var svc = new PromotionService(conn);
            DataTable dt;
            try { dt = svc.GetActiveHomepagePromotions(); }
            catch { svc.EnsureTableExists(); return; }   // ยังไม่ได้รัน migration → ข้ามเงียบๆ

            if (dt == null || dt.Rows.Count == 0) return;

            var sb = new System.Text.StringBuilder();
            sb.Append(@"<div id='promoOverlay' class='promo-overlay' style='display:none;'>");
            sb.Append(@"<div class='promo-modal'>");
            sb.Append(@"<button type='button' class='promo-close' onclick='closePromo()'>&times;</button>");
            sb.Append(@"<div class='promo-track'>");

            foreach (DataRow r in dt.Rows)
            {
                string title = Server.HtmlEncode(r["Title"]?.ToString() ?? "");
                string desc = r["Description"] == DBNull.Value ? "" : Server.HtmlEncode(r["Description"].ToString()).Replace("\n", "<br/>");
                string img = r["Image_Url"] == DBNull.Value ? "" : r["Image_Url"].ToString();
                string link = r["Link_Url"] == DBNull.Value ? "" : r["Link_Url"].ToString();

                sb.Append("<div class='promo-slide'>");
                string inner = "";
                if (!string.IsNullOrEmpty(img))
                    inner += $"<img src='{Server.HtmlEncode(img)}' alt='{title}' class='promo-img'/>";
                inner += "<div class='promo-body'>";
                if (!string.IsNullOrEmpty(title)) inner += $"<h3 class='promo-title'>{title}</h3>";
                if (!string.IsNullOrEmpty(desc)) inner += $"<p class='promo-desc'>{desc}</p>";
                inner += "</div>";

                if (!string.IsNullOrEmpty(link))
                    sb.Append($"<a href='{Server.HtmlEncode(link)}' class='promo-link'>{inner}</a>");
                else
                    sb.Append(inner);
                sb.Append("</div>");
            }

            sb.Append("</div></div></div>");

            // CSS + JS (แสดงครั้งเดียวต่อวัน)
            sb.Append(@"
<style>
.promo-overlay{position:fixed;inset:0;background:rgba(0,0,0,.6);z-index:99999;display:flex;align-items:center;justify-content:center;padding:16px;}
.promo-modal{position:relative;background:#fff;border-radius:12px;max-width:460px;width:100%;max-height:90vh;overflow:auto;box-shadow:0 10px 40px rgba(0,0,0,.3);}
.promo-close{position:absolute;top:8px;right:12px;background:rgba(0,0,0,.45);color:#fff;border:none;width:34px;height:34px;border-radius:50%;font-size:22px;line-height:1;cursor:pointer;z-index:2;}
.promo-close:hover{background:rgba(0,0,0,.7);}
.promo-track{display:flex;overflow-x:auto;scroll-snap-type:x mandatory;-webkit-overflow-scrolling:touch;}
.promo-slide{flex:0 0 100%;scroll-snap-align:start;}
.promo-link{text-decoration:none;color:inherit;display:block;}
.promo-img{width:100%;display:block;border-radius:12px 12px 0 0;object-fit:cover;}
.promo-body{padding:16px 20px;}
.promo-title{margin:0 0 8px;color:#d35400;font-size:20px;font-weight:700;}
.promo-desc{margin:0;color:#5D4037;font-size:14px;line-height:1.6;}
</style>
<script>
(function(){
  try{
    var key='ttPromoSeen';
    var today=new Date().toISOString().slice(0,10);
    if(localStorage.getItem(key)===today) return;
    var ov=document.getElementById('promoOverlay');
    if(!ov) return;
    setTimeout(function(){ ov.style.display='flex'; }, 800);
    window.closePromo=function(){
      ov.style.display='none';
      try{ localStorage.setItem(key, today); }catch(e){}
    };
    ov.addEventListener('click',function(e){ if(e.target===ov) window.closePromo(); });
  }catch(e){}
})();
</script>");

            ClientScript.RegisterStartupScript(GetType(), "promoPopup", sb.ToString(), false);
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
        /// SECURE: Execute SQL query with parameterized values to prevent SQL Injection
        /// Delegates to the code class's DatabaseQuerySafe method
        /// </summary>
        /// <param name="connStr">Connection string</param>
        /// <param name="query">SQL query with @param1, @param2 placeholders</param>
        /// <param name="parameters">Dictionary of parameters: key = "@param1", value = actual value</param>
        /// <returns>DataTable with results</returns>
        public DataTable DatabaseQuerySafe(string connStr, string query, Dictionary<string, object> parameters = null)
        {
            return code.DatabaseQuerySafe(connStr, query, parameters);
        }

        /// <summary>
        /// SECURE: Execute INSERT/UPDATE/DELETE with parameterized values to prevent SQL Injection
        /// Delegates to the code class's DatabaseInsertSafe method
        /// </summary>
        /// <param name="connStr">Connection string</param>
        /// <param name="query">SQL command with @param1, @param2 placeholders</param>
        /// <param name="parameters">Dictionary of parameters</param>
        /// <returns>Number of rows affected</returns>
        public int DatabaseInsertSafe(string connStr, string query, Dictionary<string, object> parameters = null)
        {
            return code.DatabaseInsertSafe(connStr, query, parameters);
        }

        /// <summary>
        /// SECURE: Execute INSERT and return the new ID with parameterized values to prevent SQL Injection
        /// Delegates to the code class's DatabaseInsertReturnSafe method
        /// </summary>
        /// <param name="connStr">Connection string</param>
        /// <param name="query">INSERT query with @param placeholders. Must include SCOPE_IDENTITY() or RETURNING</param>
        /// <param name="parameters">Dictionary of parameters</param>
        /// <returns>New record ID</returns>
        public int DatabaseInsertReturnSafe(string connStr, string query, Dictionary<string, object> parameters = null)
        {
            return code.DatabaseInsertReturnSafe(connStr, query, parameters);
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