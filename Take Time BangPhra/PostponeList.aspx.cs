using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;


namespace Take_Time_BangPhra
{
    public partial class PostponeList : System.Web.UI.Page
    {
        private readonly string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private RescheduleService _rescheduleService;

        protected void Page_Load(object sender, EventArgs e)
        {
            Page.MaintainScrollPositionOnPostBack = true;
            _rescheduleService = new RescheduleService(conn);

            try
            {
                if (Session["permission"]?.ToString() != "True")
                {
                    Response.Redirect("./Default");
                    return;
                }

                if (!IsPostBack)
                {
                    LoadPostponedReservations();
                }
            }
            catch
            {
                Response.Redirect("./Default");
            }
        }

        private void LoadPostponedReservations()
        {
            try
            {
                // Use RescheduleService for standardized query
                // Also support backward compatibility with placeholder dates
                using (SqlConnection connection = new SqlConnection(conn))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT
                            R.ID,
                            R.Customer_MobilePhone,
                            R.TotalPrice,
                            R.Deposit,
                            R.Remark,
                            R.Created_Date,
                            R.StayDays,
                            ISNULL(R.IsPostponed, 0) AS IsPostponed,
                            R.PostponedDate,
                            ISNULL(R.RescheduleCount, 0) AS RescheduleCount,
                            C.Name,
                            C.NickName,
                            (SELECT COUNT(*) FROM Reservation_Reschedule_History RH
                             WHERE RH.Reservation_ID = R.ID) AS TotalReschedules,
                            (SELECT TOP 1 RH.Reason FROM Reservation_Reschedule_History RH
                             WHERE RH.Reservation_ID = R.ID ORDER BY RH.RescheduledDate DESC) AS LastRescheduleReason
                        FROM [dbo].[Reservation] R
                        INNER JOIN [dbo].[Customer] C ON C.MobilePhone = R.Customer_MobilePhone
                        WHERE R.Status = N'มัดจำแล้ว'
                          AND (
                              R.IsPostponed = 1
                              OR R.CheckinDate = '1990-01-01'
                              OR R.CheckinDate = '0001-01-01'
                          )
                        ORDER BY R.ID DESC", connection))
                    {
                        DataTable dt = new DataTable();
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                        Session["dtShow"] = dt;
                        GridView1.DataSource = dt;
                        GridView1.DataBind();

                        // Update stats
                        lblPostponeCount.Text = dt.Rows.Count.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadPostponedReservations Error: {ex.Message}");
                // Fallback: try without new columns (in case migration not yet applied)
                try
                {
                    _Default code = new _Default();
                    DataTable dt = code.DatabaseQuery(conn,
                        "SELECT * FROM [Taketime].[dbo].[Reservation] INNER JOIN Customer ON Customer.MobilePhone = Reservation.Customer_MobilePhone WHERE (CheckinDate = '1990-01-01' OR CheckinDate = '0001-01-01') AND Reservation.Status = N'มัดจำแล้ว' ORDER BY Reservation.ID DESC");
                    Session["dtShow"] = dt;
                    GridView1.DataSource = dt;
                    GridView1.DataBind();
                    lblPostponeCount.Text = dt.Rows.Count.ToString();
                }
                catch
                {
                    Response.Redirect("./Default");
                }
            }
        }

        protected void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "EditReservation")
            {
                DataTable dtShow = (DataTable)Session["dtShow"];
                int rowIndex = Convert.ToInt32(e.CommandArgument);
                string reservationId = dtShow.Rows[rowIndex]["ID"].ToString();
                string phone = dtShow.Rows[rowIndex]["Customer_MobilePhone"].ToString();
                Response.Redirect("./Reserve?command=edit&id=" + reservationId + "&check=" + phone);
            }
            if (e.CommandName == "CancelPostpone")
            {
                DataTable dtShow = (DataTable)Session["dtShow"];
                int rowIndex = Convert.ToInt32(e.CommandArgument);
                int reservationId = Convert.ToInt32(dtShow.Rows[rowIndex]["ID"]);

                try
                {
                    short? adminId = Session["UserID"] != null ? (short?)Convert.ToInt16(Session["UserID"]) : null;
                    string adminName = Session["User"]?.ToString();

                    // Use RescheduleService for proper status update and history logging
                    _rescheduleService.CancelPostpone(reservationId, adminId, adminName, "ยกเลิกจากหน้ารายการเลื่อนเข้าพัก");

                    ClientScript.RegisterStartupScript(this.GetType(), "success",
                        "alert('ยกเลิกรายการเลื่อนวันเข้าพักเรียบร้อยแล้ว'); window.location.href='/PostponeList';", true);
                }
                catch (Exception ex)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "error",
                        $"alert('เกิดข้อผิดพลาด: {ex.Message.Replace("'", "\\'")}');", true);
                }
            }
        }

        protected void DeleteButton_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            int reservationId = Convert.ToInt32(btn.CommandArgument);

            try
            {
                short? adminId = Session["UserID"] != null ? (short?)Convert.ToInt16(Session["UserID"]) : null;
                string adminName = Session["User"]?.ToString();

                // Use RescheduleService for proper status update and history logging
                _rescheduleService.CancelPostpone(reservationId, adminId, adminName, "ยกเลิกจากหน้ารายการเลื่อนเข้าพัก");

                ClientScript.RegisterStartupScript(this.GetType(), "success",
                    "alert('ยกเลิกรายการเลื่อนวันเข้าพักเรียบร้อยแล้ว'); window.location.href='/PostponeList';", true);
            }
            catch (Exception ex)
            {
                ClientScript.RegisterStartupScript(this.GetType(), "error",
                    $"alert('เกิดข้อผิดพลาด: {ex.Message.Replace("'", "\\'")}');", true);
            }
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadPostponedReservations();
        }
    }
}
