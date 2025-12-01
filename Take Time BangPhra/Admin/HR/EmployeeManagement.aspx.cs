using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.HR
{
    public partial class EmployeeManagement : System.Web.UI.Page
    {
        private EmployeeService employeeService;

        protected void Page_Load(object sender, EventArgs e)
        {
            employeeService = new EmployeeService();

            if (!IsPostBack)
            {
                CheckAdminLogin();
                LoadStatistics();
                LoadEmployees();
            }
        }

        #region Authentication

        private void CheckAdminLogin()
        {
            // Check permissions
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("/Default");
                return;
            }

            // Check user role - HR pages should be restricted to Owner and Admin
            string userType = Session["User"]?.ToString();
            if (userType != "Owner" && userType != "Admin")
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

        #region Load Data

        private void LoadStatistics()
        {
            try
            {
                DataTable stats = employeeService.GetHRDashboardStats();

                if (stats != null && stats.Rows.Count > 0)
                {
                    DataRow row = stats.Rows[0];
                    lblTotalEmployees.Text = row["TotalActiveEmployees"].ToString();
                    lblNewEmployees.Text = row["NewEmployeesThisMonth"].ToString();
                    lblExpiringContracts.Text = row["ContractsExpiringWithin30Days"].ToString();
                    lblExpiringDocuments.Text = row["DocumentsExpiringWithin30Days"].ToString();
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดสถิติ: " + ex.Message, "error");
            }
        }

        private void LoadEmployees()
        {
            try
            {
                string searchTerm = txtSearch.Text.Trim();
                string department = ddlDepartment.SelectedValue;

                DataTable dt = employeeService.SearchEmployees(searchTerm, "", department);
                gvEmployees.DataSource = dt;
                gvEmployees.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูลพนักงาน: " + ex.Message, "error");
            }
        }

        #endregion

        #region Event Handlers

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            LoadEmployees();
        }

        protected void btnReset_Click(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            ddlDepartment.SelectedIndex = 0;
            LoadEmployees();
        }

        protected void gvEmployees_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ViewProfile")
            {
                short adminId = Convert.ToInt16(e.CommandArgument);
                Response.Redirect($"EmployeeProfile.aspx?id={adminId}");
            }
        }

        #endregion

        #region Helper Methods

        protected string GetServiceAgeText(object years, object months)
        {
            try
            {
                if (years == DBNull.Value || months == DBNull.Value)
                    return "N/A";

                int totalYears = Convert.ToInt32(years);
                int totalMonths = Convert.ToInt32(months);
                int remainingMonths = totalMonths % 12;

                return $"{totalYears} ปี {remainingMonths} เดือน";
            }
            catch
            {
                return "N/A";
            }
        }

        protected string GetContractStatusBadge(object daysUntilExpiry)
        {
            try
            {
                if (daysUntilExpiry == DBNull.Value)
                    return "<span class='badge badge-success'>ไม่มีกำหนด</span>";

                int days = Convert.ToInt32(daysUntilExpiry);

                if (days < 0)
                    return "<span class='badge badge-danger'>หมดอายุแล้ว</span>";
                else if (days <= 30)
                    return $"<span class='badge badge-danger'>เหลือ {days} วัน</span>";
                else if (days <= 90)
                    return $"<span class='badge badge-warning'>เหลือ {days} วัน</span>";
                else
                    return $"<span class='badge badge-success'>เหลือ {days} วัน</span>";
            }
            catch
            {
                return "<span class='badge badge-success'>N/A</span>";
            }
        }

        private void ShowMessage(string message, string type)
        {
            string script = $"alert('{message.Replace("'", "\\'")}');";
            ClientScript.RegisterStartupScript(this.GetType(), "alert", script, true);
        }

        #endregion
    }
}
