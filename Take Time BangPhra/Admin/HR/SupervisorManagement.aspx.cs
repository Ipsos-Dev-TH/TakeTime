using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.HR
{
    public partial class SupervisorManagement : System.Web.UI.Page
    {
        private SupervisorService supervisorService;

        protected void Page_Load(object sender, EventArgs e)
        {
            supervisorService = new SupervisorService();

            if (!IsPostBack)
            {
                CheckAdminLogin();
                LoadStatistics();
                LoadDropdowns();
                LoadRelationships();
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

            // Only Admin and Owner can manage supervisor relationships
            string userType = Session["User"]?.ToString();
            if (userType != "Owner" && userType != "Admin")
            {
                Response.Redirect("/Default");
                return;
            }
        }

        #endregion

        #region Load Data

        private void LoadStatistics()
        {
            try
            {
                DataTable dt = supervisorService.GetAllSupervisorRelationships();

                if (dt != null)
                {
                    lblTotalRelationships.Text = dt.Rows.Count.ToString();

                    // Count unique employees and supervisors
                    var employees = new System.Collections.Generic.HashSet<int>();
                    var supervisors = new System.Collections.Generic.HashSet<int>();

                    foreach (DataRow row in dt.Rows)
                    {
                        employees.Add(Convert.ToInt32(row["Employee_AdminID"]));
                        supervisors.Add(Convert.ToInt32(row["Supervisor_AdminID"]));
                    }

                    lblEmployeesWithSupervisor.Text = employees.Count.ToString();
                    lblTotalSupervisors.Text = supervisors.Count.ToString();
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดสถิติ: " + ex.Message, "error");
            }
        }

        private void LoadDropdowns()
        {
            try
            {
                DataTable dt = supervisorService.GetAllActiveEmployees();

                if (dt != null)
                {
                    // Load employee dropdown
                    ddlEmployee.Items.Clear();
                    ddlEmployee.Items.Add(new ListItem("-- เลือกพนักงาน --", ""));

                    // Load supervisor dropdown
                    ddlSupervisor.Items.Clear();
                    ddlSupervisor.Items.Add(new ListItem("-- เลือกหัวหน้างาน --", ""));

                    foreach (DataRow row in dt.Rows)
                    {
                        string text = row["Name"].ToString();
                        if (row["Role"] != DBNull.Value && !string.IsNullOrEmpty(row["Role"].ToString()))
                        {
                            text += " (" + row["Role"].ToString() + ")";
                        }
                        string value = row["ID"].ToString();

                        ddlEmployee.Items.Add(new ListItem(text, value));
                        ddlSupervisor.Items.Add(new ListItem(text, value));
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดรายชื่อพนักงาน: " + ex.Message, "error");
            }
        }

        private void LoadRelationships()
        {
            try
            {
                DataTable dt = supervisorService.GetAllSupervisorRelationships();

                // Filter by search term if provided
                string searchTerm = txtSearch.Text.Trim();
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    string sanitizedSearchTerm = searchTerm.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
                    DataView dv = dt.DefaultView;
                    dv.RowFilter = $"EmployeeName LIKE '*{sanitizedSearchTerm}*' OR SupervisorName LIKE '*{sanitizedSearchTerm}*' OR EmployeeUsername LIKE '*{sanitizedSearchTerm}*' OR SupervisorUsername LIKE '*{sanitizedSearchTerm}*'";
                    dt = dv.ToTable();
                }

                gvRelationships.DataSource = dt;
                gvRelationships.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, "error");
            }
        }

        #endregion

        #region Event Handlers

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            LoadRelationships();
        }

        protected void btnReset_Click(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            LoadRelationships();
        }

        protected void btnSaveRelationship_Click(object sender, EventArgs e)
        {
            try
            {
                // Validate
                if (string.IsNullOrEmpty(ddlEmployee.SelectedValue))
                {
                    ShowMessage("กรุณาเลือกพนักงาน", "error");
                    ShowAddModal();
                    return;
                }

                if (string.IsNullOrEmpty(ddlSupervisor.SelectedValue))
                {
                    ShowMessage("กรุณาเลือกหัวหน้างาน", "error");
                    ShowAddModal();
                    return;
                }

                short employeeId = Convert.ToInt16(ddlEmployee.SelectedValue);
                short supervisorId = Convert.ToInt16(ddlSupervisor.SelectedValue);

                if (employeeId == supervisorId)
                {
                    ShowMessage("ไม่สามารถกำหนดตัวเองเป็นหัวหน้างานได้", "error");
                    ShowAddModal();
                    return;
                }

                bool isPrimary = chkIsPrimary.Checked;
                bool canApproveLeave = chkCanApproveLeave.Checked;
                bool canViewSalary = chkCanViewSalary.Checked;
                string notes = txtNotes.Text.Trim();

                var result = supervisorService.AssignSupervisor(
                    employeeId, supervisorId, isPrimary, canApproveLeave, canViewSalary, notes);

                if (result.Success)
                {
                    ShowMessage(result.Message, "success");
                    ClearForm();
                    LoadStatistics();
                    LoadRelationships();
                }
                else
                {
                    ShowMessage(result.Message, "error");
                    ShowAddModal();
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void gvRelationships_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "DeleteRelation")
            {
                try
                {
                    string[] args = e.CommandArgument.ToString().Split(',');
                    short employeeId = Convert.ToInt16(args[0]);
                    short supervisorId = Convert.ToInt16(args[1]);

                    var result = supervisorService.RemoveSupervisor(employeeId, supervisorId);

                    if (result.Success)
                    {
                        ShowMessage(result.Message, "success");
                        LoadStatistics();
                        LoadRelationships();
                    }
                    else
                    {
                        ShowMessage(result.Message, "error");
                    }
                }
                catch (Exception ex)
                {
                    ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
                }
            }
        }

        #endregion

        #region Helper Methods

        private void ClearForm()
        {
            ddlEmployee.SelectedIndex = 0;
            ddlSupervisor.SelectedIndex = 0;
            chkIsPrimary.Checked = false;
            chkCanApproveLeave.Checked = true;
            chkCanViewSalary.Checked = false;
            txtNotes.Text = "";
        }

        private void ShowAddModal()
        {
            ScriptManager.RegisterStartupScript(this, GetType(), "showAddModal", "showAddModal();", true);
        }

        private void ShowMessage(string message, string type)
        {
            pnlMessage.Visible = true;
            lblMessage.Text = message;

            if (type == "success")
            {
                pnlMessage.CssClass = "alert alert-success";
            }
            else
            {
                pnlMessage.CssClass = "alert alert-error";
            }
        }

        #endregion
    }
}
