using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.Leave
{
    public partial class LeaveReplacement : System.Web.UI.Page
    {
        private LeaveService leaveService;

        protected void Page_Load(object sender, EventArgs e)
        {
            leaveService = new LeaveService();

            if (!IsPostBack)
            {
                CheckAdminLogin();
                InitializeFilters();
                LoadStatistics();
                LoadLeaveRequests();
            }
        }

        #region Authentication

        private void CheckAdminLogin()
        {
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("/Default");
                return;
            }

            string userType = Session["User"]?.ToString();
            if (userType != "Owner" && userType != "Admin" && userType != "Supervisor")
            {
                Response.Redirect("/Default");
                return;
            }
        }

        private short? GetAdminID()
        {
            if (Session["UserID"] != null)
            {
                return Convert.ToInt16(Session["UserID"]);
            }
            return null;
        }

        #endregion

        #region Initialization

        private void InitializeFilters()
        {
            int currentYear = DateTime.Now.Year;
            ddlYear.Items.Clear();

            for (int year = currentYear - 2; year <= currentYear; year++)
            {
                ddlYear.Items.Add(new ListItem((year + 543).ToString(), year.ToString()));
            }

            ddlYear.SelectedValue = currentYear.ToString();
        }

        #endregion

        #region Load Data

        private void LoadStatistics()
        {
            try
            {
                short? adminId = GetAdminID();
                if (!adminId.HasValue) return;

                short year = Convert.ToInt16(ddlYear.SelectedValue);

                // Get all leaves for the year to calculate statistics
                DataTable dt = leaveService.GetLeavesForWorkReplacement(adminId.Value, year, null);

                int approvedCount = dt.Rows.Count;
                int replacedCount = 0;

                foreach (DataRow row in dt.Rows)
                {
                    if (row["IsReplaced"] != DBNull.Value && Convert.ToBoolean(row["IsReplaced"]))
                    {
                        replacedCount++;
                    }
                }

                lblApprovedLeaves.Text = approvedCount.ToString();
                lblReplacedLeaves.Text = replacedCount.ToString();
                lblPendingReplacement.Text = (approvedCount - replacedCount).ToString();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดสถิติ: " + ex.Message, false);
            }
        }

        private void LoadLeaveRequests()
        {
            try
            {
                short? adminId = GetAdminID();
                if (!adminId.HasValue) return;

                short year = Convert.ToInt16(ddlYear.SelectedValue);
                string search = txtSearchEmployee.Text.Trim();

                DataTable dt = leaveService.GetLeavesForWorkReplacement(adminId.Value, year, string.IsNullOrEmpty(search) ? null : search);

                // Filter by replacement status if selected
                string replacementStatus = ddlReplacementStatus.SelectedValue;
                if (!string.IsNullOrEmpty(replacementStatus))
                {
                    bool filterReplaced = replacementStatus == "1";
                    DataView dv = dt.DefaultView;
                    dv.RowFilter = $"IsReplaced = {filterReplaced.ToString().ToUpper()}";
                    dt = dv.ToTable();
                }

                gvLeaveRequests.DataSource = dt;
                gvLeaveRequests.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, false);
            }
        }

        #endregion

        #region Event Handlers

        protected void ddlYear_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadStatistics();
            LoadLeaveRequests();
        }

        protected void ddlReplacementStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadLeaveRequests();
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            LoadLeaveRequests();
        }

        protected void gvLeaveRequests_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            long requestId = Convert.ToInt64(e.CommandArgument);
            short? adminId = GetAdminID();

            if (!adminId.HasValue)
            {
                ShowMessage("ไม่พบข้อมูลผู้ใช้", false);
                return;
            }

            try
            {
                if (e.CommandName == "MarkReplaced")
                {
                    // Store the leave ID and show modal
                    hfSelectedLeaveId.Value = requestId.ToString();
                    ScriptManager.RegisterStartupScript(this, GetType(), "showModal",
                        $"showReplacementModal({requestId});", true);
                }
                else if (e.CommandName == "CancelReplacement")
                {
                    var result = leaveService.CancelWorkReplacement(requestId, adminId.Value);

                    if (result.Success)
                    {
                        ShowMessage(result.Message, true);
                        LoadStatistics();
                        LoadLeaveRequests();
                    }
                    else
                    {
                        ShowMessage(result.Message, false);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        protected void btnConfirmReplacement_Click(object sender, EventArgs e)
        {
            try
            {
                short? adminId = GetAdminID();
                if (!adminId.HasValue)
                {
                    ShowMessage("ไม่พบข้อมูลผู้ใช้", false);
                    return;
                }

                if (string.IsNullOrEmpty(hfSelectedLeaveId.Value))
                {
                    ShowMessage("ไม่พบข้อมูลใบลา", false);
                    return;
                }

                if (string.IsNullOrEmpty(txtReplacementDate.Text))
                {
                    ShowMessage("กรุณาเลือกวันที่ทำงานทดแทน", false);
                    return;
                }

                long requestId = Convert.ToInt64(hfSelectedLeaveId.Value);
                DateTime replacementDate = DateTime.Parse(txtReplacementDate.Text);

                var result = leaveService.MarkLeaveAsReplaced(requestId, adminId.Value, replacementDate);

                if (result.Success)
                {
                    ShowMessage(result.Message, true);
                    hfSelectedLeaveId.Value = "";
                    LoadStatistics();
                    LoadLeaveRequests();
                }
                else
                {
                    ShowMessage(result.Message, false);
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        #endregion

        #region Helper Methods

        protected string GetReplacementBadge(object isReplaced, object replacementDate)
        {
            try
            {
                bool replaced = isReplaced != DBNull.Value && Convert.ToBoolean(isReplaced);

                if (replaced)
                {
                    string date = replacementDate != DBNull.Value
                        ? Convert.ToDateTime(replacementDate).ToString("dd/MM/yyyy")
                        : "";
                    return $"<span class='badge badge-replaced'>ทดแทนแล้ว</span><br/><small style='color:#28a745;'>วันที่ {date}</small>";
                }
                else
                {
                    return "<span class='badge badge-not-replaced'>ยังไม่ทดแทน</span>";
                }
            }
            catch
            {
                return "-";
            }
        }

        private void ShowMessage(string message, bool isSuccess)
        {
            pnlAlert.Visible = true;
            pnlAlert.CssClass = isSuccess ? "alert alert-success" : "alert alert-error";
            pnlAlert.Style["display"] = "block";
            lblAlertMessage.Text = message;
        }

        #endregion
    }
}
