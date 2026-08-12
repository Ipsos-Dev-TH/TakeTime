using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.Housekeeping
{
    public partial class Dashboard : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private HousekeepingService _housekeepingService;
        private code _code;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Housekeeping", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            // Check admin permission
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            _housekeepingService = new HousekeepingService(_connectionString);
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
                // Load status counts
                var statusCounts = GetStatusCounts();
                lblCleanCount.Text = statusCounts.Clean.ToString();
                lblDirtyCount.Text = statusCounts.Dirty.ToString();
                lblInspectingCount.Text = statusCounts.Inspecting.ToString();
                lblOccupiedCount.Text = statusCounts.Occupied.ToString();

                // Load dirty rooms
                DataTable dtDirty = GetRoomsByStatus("DIRTY");
                if (dtDirty.Rows.Count > 0)
                {
                    rptDirtyRooms.DataSource = dtDirty;
                    rptDirtyRooms.DataBind();
                    lblNoDirtyRooms.Visible = false;
                }
                else
                {
                    lblNoDirtyRooms.Visible = true;
                }

                // Load all rooms
                DataTable dtAll = GetAllRooms();
                rptAllRooms.DataSource = dtAll;
                rptAllRooms.DataBind();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading housekeeping data: {ex.Message}");
            }
        }

        private (int Clean, int Dirty, int Inspecting, int Occupied) GetStatusCounts()
        {
            int clean = 0, dirty = 0, inspecting = 0, occupied = 0;

            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        ISNULL(HousekeepingStatus, 'VACANT_CLEAN') AS Status,
                        COUNT(*) AS Count
                      FROM Accommodation
                      WHERE Status = 1
                      GROUP BY HousekeepingStatus",
                    null);

                foreach (DataRow row in dt.Rows)
                {
                    string status = row["Status"].ToString().ToUpper();
                    int count = Convert.ToInt32(row["Count"]);

                    if (status.Contains("CLEAN")) clean += count;
                    else if (status.Contains("DIRTY")) dirty += count;
                    else if (status.Contains("INSPECT")) inspecting += count;
                    else if (status.Contains("OCCUPIED")) occupied += count;
                }
            }
            catch { }

            return (clean, dirty, inspecting, occupied);
        }

        private DataTable GetRoomsByStatus(string status)
        {
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ID, AccomName, HousekeepingStatus
                  FROM Accommodation
                  WHERE Status = 1
                  AND (HousekeepingStatus LIKE @Status OR (HousekeepingStatus IS NULL AND @Status = 'CLEAN'))
                  ORDER BY AccomName",
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@Status", "%" + status + "%" }
                });
        }

        private DataTable GetAllRooms()
        {
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ID, AccomName, ISNULL(HousekeepingStatus, 'VACANT_CLEAN') AS HousekeepingStatus
                  FROM Accommodation
                  WHERE Status = 1
                  ORDER BY AccomName",
                null);
        }

        protected void rptDirtyRooms_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName == "MarkClean")
            {
                int roomId = Convert.ToInt32(e.CommandArgument);
                UpdateRoomStatus(roomId, "VACANT_CLEAN");
                LoadDashboardData();
            }
        }

        protected void rptAllRooms_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            int roomId = Convert.ToInt32(e.CommandArgument);

            if (e.CommandName == "SetClean")
            {
                UpdateRoomStatus(roomId, "VACANT_CLEAN");
            }
            else if (e.CommandName == "SetDirty")
            {
                UpdateRoomStatus(roomId, "VACANT_DIRTY");
            }

            LoadDashboardData();
        }

        private void UpdateRoomStatus(int roomId, string status)
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Accommodation SET HousekeepingStatus = @Status WHERE ID = @ID",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@Status", status },
                        { "@ID", roomId }
                    });

                // Log status change
                _code.Logs(_connectionString, "Housekeeping Status Change",
                    $"Room ID {roomId} changed to {status}",
                    Session["User"]?.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating room status: {ex.Message}");
            }
        }

        protected string GetStatusClass(object status)
        {
            string s = status?.ToString().ToUpper() ?? "";
            if (s.Contains("CLEAN")) return "clean";
            if (s.Contains("DIRTY")) return "dirty";
            if (s.Contains("INSPECT")) return "inspecting";
            if (s.Contains("OCCUPIED")) return "occupied";
            return "clean";
        }

        protected string GetStatusText(object status)
        {
            string s = status?.ToString().ToUpper() ?? "";
            if (s.Contains("DIRTY")) return "รอทำความสะอาด";
            if (s.Contains("INSPECT")) return "รอตรวจสอบ";
            if (s.Contains("OCCUPIED")) return "มีผู้เข้าพัก";
            return "สะอาด";
        }
    }
}
