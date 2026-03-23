using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Globalization;


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

            // Handle AJAX request for reschedule history
            if (Request.QueryString["action"] == "gethistory")
            {
                HandleGetHistory();
                return;
            }

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
                // Use RescheduleService query with backward compatibility
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
                        "SELECT *, 0 AS RescheduleCount, NULL AS LastRescheduleReason FROM [Taketime].[dbo].[Reservation] INNER JOIN Customer ON Customer.MobilePhone = Reservation.Customer_MobilePhone WHERE (CheckinDate = '1990-01-01' OR CheckinDate = '0001-01-01') AND Reservation.Status = N'มัดจำแล้ว' ORDER BY Reservation.ID DESC");
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
        }

        /// <summary>
        /// Cancel with reason (triggered from modal via hidden button)
        /// </summary>
        protected void btnCancelWithReason_Click(object sender, EventArgs e)
        {
            try
            {
                int reservationId = Convert.ToInt32(hdnCancelReservationId.Value);
                string reason = hdnCancelReason.Value;

                if (reservationId <= 0)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "error",
                        "alert('ไม่พบข้อมูลการจอง');", true);
                    return;
                }

                short? adminId = Session["UserID"] != null ? (short?)Convert.ToInt16(Session["UserID"]) : null;
                string adminName = Session["User"]?.ToString();

                _rescheduleService.CancelPostpone(reservationId, adminId, adminName,
                    !string.IsNullOrEmpty(reason) ? reason : "ยกเลิกจากหน้ารายการเลื่อนเข้าพัก");

                ClientScript.RegisterStartupScript(this.GetType(), "success",
                    "alert('ยกเลิกรายการเลื่อนวันเข้าพักเรียบร้อยแล้ว'); window.location.href='/PostponeList';", true);
            }
            catch (Exception ex)
            {
                ClientScript.RegisterStartupScript(this.GetType(), "error",
                    $"alert('เกิดข้อผิดพลาด: {ex.Message.Replace("'", "\\'")}');", true);
            }
        }

        /// <summary>
        /// Fallback DeleteButton_Click (kept for backward compatibility)
        /// </summary>
        protected void DeleteButton_Click(object sender, EventArgs e)
        {
            // This is now handled by btnCancelWithReason_Click via modal
            // Kept as fallback if called directly
            Button btn = (Button)sender;
            int reservationId = Convert.ToInt32(btn.CommandArgument);

            try
            {
                short? adminId = Session["UserID"] != null ? (short?)Convert.ToInt16(Session["UserID"]) : null;
                string adminName = Session["User"]?.ToString();

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

        /// <summary>
        /// Handle AJAX request for reschedule history
        /// </summary>
        private void HandleGetHistory()
        {
            Response.ContentType = "application/json";
            Response.Clear();

            try
            {
                int reservationId = Convert.ToInt32(Request.QueryString["rid"]);
                DataTable history = _rescheduleService.GetRescheduleHistory(reservationId);

                var items = new System.Text.StringBuilder("[");
                bool first = true;

                foreach (DataRow row in history.Rows)
                {
                    if (!first) items.Append(",");
                    first = false;

                    string type = row["RescheduleType"]?.ToString() ?? "";
                    string reason = (row["Reason"] != DBNull.Value ? row["Reason"].ToString() : "")
                        .Replace("\\", "\\\\").Replace("\"", "\\\"");
                    string admin = (row["RescheduledBy_Name"] != DBNull.Value ? row["RescheduledBy_Name"].ToString() : "")
                        .Replace("\\", "\\\\").Replace("\"", "\\\"");
                    string date = row["RescheduledDate"] != DBNull.Value
                        ? Convert.ToDateTime(row["RescheduledDate"]).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "";

                    string oldDate = "";
                    if (row["OldCheckinDate"] != DBNull.Value)
                    {
                        DateTime od = Convert.ToDateTime(row["OldCheckinDate"]);
                        if (od.Year > 1990)
                            oldDate = od.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                    }

                    string newDate = "";
                    if (row["NewCheckinDate"] != DBNull.Value)
                    {
                        DateTime nd = Convert.ToDateTime(row["NewCheckinDate"]);
                        if (nd.Year > 1990)
                            newDate = nd.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                    }

                    items.Append($"{{\"Type\":\"{type}\",\"Reason\":\"{reason}\",\"Admin\":\"{admin}\",\"Date\":\"{date}\",\"OldDate\":\"{oldDate}\",\"NewDate\":\"{newDate}\"}}");
                }

                items.Append("]");
                Response.Write(items.ToString());
            }
            catch (Exception ex)
            {
                Response.Write($"[]");
                System.Diagnostics.Debug.WriteLine($"GetHistory Error: {ex.Message}");
            }

            Response.End();
        }
    }
}
