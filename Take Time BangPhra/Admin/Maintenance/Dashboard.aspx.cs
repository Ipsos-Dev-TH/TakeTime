using System;
using System.Data;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.Maintenance
{
    public partial class Dashboard : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.OpsMaintenance)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (!Feature.Guard(this, "Maintenance", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            _code = new code();

            if (!IsPostBack)
            {
                LoadDashboardData();
            }
        }

        private void LoadDashboardData()
        {
            try
            {
                // Load counts
                DataTable dtCounts = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        SUM(CASE WHEN Status = 'PENDING' THEN 1 ELSE 0 END) AS Pending,
                        SUM(CASE WHEN Status = 'IN_PROGRESS' THEN 1 ELSE 0 END) AS InProgress,
                        SUM(CASE WHEN Status = 'COMPLETED' AND MONTH(CompletedDate) = MONTH(GETDATE()) THEN 1 ELSE 0 END) AS Completed
                      FROM MaintenanceRequests",
                    null);

                if (dtCounts.Rows.Count > 0)
                {
                    lblPending.Text = (dtCounts.Rows[0]["Pending"] ?? 0).ToString();
                    lblInProgress.Text = (dtCounts.Rows[0]["InProgress"] ?? 0).ToString();
                    lblCompleted.Text = (dtCounts.Rows[0]["Completed"] ?? 0).ToString();
                }

                // Load pending requests
                DataTable dtRequests = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT RequestId, Title, Location, Priority, RequestDate
                      FROM MaintenanceRequests
                      WHERE Status = 'PENDING'
                      ORDER BY
                        CASE Priority WHEN 'HIGH' THEN 1 WHEN 'MEDIUM' THEN 2 ELSE 3 END,
                        RequestDate ASC",
                    null);

                if (dtRequests.Rows.Count > 0)
                {
                    rptRequests.DataSource = dtRequests;
                    rptRequests.DataBind();
                    lblNoRequests.Visible = false;
                }
                else
                {
                    lblNoRequests.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading maintenance data: {ex.Message}");
                lblNoRequests.Visible = true;
            }
        }
    }
}
