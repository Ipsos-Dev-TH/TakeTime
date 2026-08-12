using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using System.Globalization;

namespace Take_Time_BangPhra.Admin
{
    public partial class AffiliateManagement : System.Web.UI.Page
    {
        private AffiliateService affiliateService;
        private string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Affiliate", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            affiliateService = new AffiliateService();

            // Security check
            if (!CheckOwnerAccess())
            {
                Response.Redirect("/Default");
                return;
            }

            if (!IsPostBack)
            {
                LoadDashboardStats();
                LoadAffiliateMembers();
                InitializeFilters();
                txtPaymentDate.Text = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
        }

        #region Security

        private bool CheckOwnerAccess()
        {
            try
            {
                if (Session["permission"]?.ToString() == "True" && Session["User"]?.ToString() == "Owner")
                {
                    return true;
                }
            }
            catch { }
            return false;
        }

        #endregion

        #region Dashboard

        private void LoadDashboardStats()
        {
            try
            {
                DataTable stats = affiliateService.GetDashboardStats();
                if (stats.Rows.Count > 0)
                {
                    lblTotalAffiliates.Text = stats.Rows[0]["TotalAffiliates"].ToString();
                    lblPendingCommissions.Text = stats.Rows[0]["PendingCommissions"].ToString();
                    lblPendingAmount.Text = string.Format("{0:N0}", stats.Rows[0]["PendingAmount"]);
                    lblTotalPaidAmount.Text = string.Format("{0:N0}", stats.Rows[0]["TotalPaidAmount"]);
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดสถิติ: " + ex.Message, false);
            }
        }

        #endregion

        #region Tab Navigation

        protected void btnTabMembers_Click(object sender, EventArgs e)
        {
            SetActiveTab("members");
            LoadAffiliateMembers();
        }

        protected void btnTabCommissions_Click(object sender, EventArgs e)
        {
            SetActiveTab("commissions");
            LoadCommissions();
        }

        protected void btnTabCreatePayment_Click(object sender, EventArgs e)
        {
            SetActiveTab("createpayment");
        }

        protected void btnTabHistory_Click(object sender, EventArgs e)
        {
            SetActiveTab("history");
            LoadPaymentHistory();
        }

        private void SetActiveTab(string tabName)
        {
            hdnActiveTab.Value = tabName;

            // Reset all tabs
            btnTabMembers.CssClass = "tab-btn";
            btnTabCommissions.CssClass = "tab-btn";
            btnTabCreatePayment.CssClass = "tab-btn";
            btnTabHistory.CssClass = "tab-btn";

            pnlMembers.CssClass = "tab-content";
            pnlCommissions.CssClass = "tab-content";
            pnlCreatePayment.CssClass = "tab-content";
            pnlHistory.CssClass = "tab-content";

            // Set active tab
            switch (tabName)
            {
                case "members":
                    btnTabMembers.CssClass = "tab-btn active";
                    pnlMembers.CssClass = "tab-content active";
                    break;
                case "commissions":
                    btnTabCommissions.CssClass = "tab-btn active";
                    pnlCommissions.CssClass = "tab-content active";
                    break;
                case "createpayment":
                    btnTabCreatePayment.CssClass = "tab-btn active";
                    pnlCreatePayment.CssClass = "tab-content active";
                    break;
                case "history":
                    btnTabHistory.CssClass = "tab-btn active";
                    pnlHistory.CssClass = "tab-content active";
                    break;
            }
        }

        #endregion

        #region Filters Initialization

        private void InitializeFilters()
        {
            // Load affiliates for dropdowns
            DataTable affiliates = affiliateService.GetAffiliateMembersForDropdown();

            // Commission filter
            ddlFilterAffiliate.Items.Clear();
            ddlFilterAffiliate.Items.Add(new ListItem("ทั้งหมด", ""));
            foreach (DataRow row in affiliates.Rows)
            {
                ddlFilterAffiliate.Items.Add(new ListItem(
                    row["CouponCode"].ToString() + " - " + row["VendorName"].ToString(),
                    row["CouponCode"].ToString()));
            }

            // Create payment dropdown
            ddlSelectAffiliate.Items.Clear();
            ddlSelectAffiliate.Items.Add(new ListItem("---โปรดเลือก---", ""));
            foreach (DataRow row in affiliates.Rows)
            {
                ddlSelectAffiliate.Items.Add(new ListItem(
                    row["CouponCode"].ToString() + " - " + row["VendorName"].ToString(),
                    row["CouponCode"].ToString()));
            }

            // History filter
            ddlHistoryAffiliate.Items.Clear();
            ddlHistoryAffiliate.Items.Add(new ListItem("ทั้งหมด", ""));
            foreach (DataRow row in affiliates.Rows)
            {
                ddlHistoryAffiliate.Items.Add(new ListItem(
                    row["CouponCode"].ToString() + " - " + row["VendorName"].ToString(),
                    row["CouponCode"].ToString()));
            }

            // Year filters
            int currentYear = DateTime.Now.Year;
            ddlFilterYear.Items.Clear();
            ddlFilterYear.Items.Add(new ListItem("ทั้งหมด", "0"));
            for (int y = currentYear; y >= currentYear - 5; y--)
            {
                ddlFilterYear.Items.Add(new ListItem(y.ToString(), y.ToString()));
            }

            ddlHistoryYear.Items.Clear();
            ddlHistoryYear.Items.Add(new ListItem("ทั้งหมด", "0"));
            for (int y = currentYear; y >= currentYear - 5; y--)
            {
                ddlHistoryYear.Items.Add(new ListItem(y.ToString(), y.ToString()));
            }
        }

        #endregion

        #region Tab 1: Affiliate Members

        private void LoadAffiliateMembers(string search = "")
        {
            try
            {
                DataTable members = affiliateService.GetAffiliateMembers(search);
                gvMembers.DataSource = members;
                gvMembers.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, false);
            }
        }

        protected void btnSearchMember_Click(object sender, EventArgs e)
        {
            LoadAffiliateMembers(txtSearchMember.Text.Trim());
        }

        protected void gvMembers_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleStatus")
            {
                int affiliateId = Convert.ToInt32(e.CommandArgument);
                if (affiliateService.ToggleAffiliateStatus(affiliateId))
                {
                    ShowMessage("เปลี่ยนสถานะสำเร็จ", true);
                    LoadAffiliateMembers(txtSearchMember.Text.Trim());
                    LoadDashboardStats();
                }
                else
                {
                    ShowMessage("เกิดข้อผิดพลาดในการเปลี่ยนสถานะ", false);
                }
            }
        }

        #endregion

        #region Tab 2: Commission Tracking

        private void LoadCommissions()
        {
            try
            {
                string couponCode = ddlFilterAffiliate.SelectedValue;
                string status = ddlFilterStatus.SelectedValue;
                int year = 0;
                int.TryParse(ddlFilterYear.SelectedValue, out year);

                DataTable commissions = affiliateService.GetAffiliateReservations(couponCode, status, year, 0);
                gvCommissions.DataSource = commissions;
                gvCommissions.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, false);
            }
        }

        protected void ddlFilterAffiliate_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadCommissions();
        }

        protected void ddlFilterStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadCommissions();
        }

        protected void ddlFilterYear_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadCommissions();
        }

        #endregion

        #region Tab 3: Create Payment Voucher

        protected void ddlSelectAffiliate_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(ddlSelectAffiliate.SelectedValue))
            {
                LoadPendingPayments(ddlSelectAffiliate.SelectedValue);
                pnlPendingToPay.Visible = true;
                lblSelectedAffiliate.Text = ddlSelectAffiliate.SelectedItem.Text;
            }
            else
            {
                pnlPendingToPay.Visible = false;
            }
        }

        private void LoadPendingPayments(string couponCode)
        {
            try
            {
                DataTable pending = affiliateService.GetPendingCommissions(couponCode);
                gvPendingPayments.DataSource = pending;
                gvPendingPayments.DataBind();

                // Calculate totals
                decimal total = 0;
                int count = 0;
                foreach (DataRow row in pending.Rows)
                {
                    total += Convert.ToDecimal(row["Commission"]);
                    count++;
                }

                lblPaymentCount.Text = count.ToString();
                lblPaymentTotal.Text = string.Format("{0:N2}", total);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดรายการ: " + ex.Message, false);
            }
        }

        protected void btnCreatePaymentVoucher_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(ddlPaymentMethod.SelectedValue))
                {
                    ShowMessage("กรุณาเลือกวิธีจ่ายเงิน", false);
                    return;
                }

                if (string.IsNullOrEmpty(ddlSelectAffiliate.SelectedValue))
                {
                    ShowMessage("กรุณาเลือก Affiliate", false);
                    return;
                }

                // Collect selected items
                System.Collections.Generic.List<int> selectedIds = new System.Collections.Generic.List<int>();
                decimal totalAmount = 0;

                foreach (GridViewRow row in gvPendingPayments.Rows)
                {
                    CheckBox chk = (CheckBox)row.FindControl("chkSelect");
                    if (chk != null && chk.Checked)
                    {
                        int id = Convert.ToInt32(gvPendingPayments.DataKeys[row.RowIndex].Value);
                        selectedIds.Add(id);

                        // Get commission from cell
                        decimal commission = Convert.ToDecimal(DataBinder.Eval(
                            ((DataTable)gvPendingPayments.DataSource).Rows[row.RowIndex], "Commission"));
                        totalAmount += commission;
                    }
                }

                if (selectedIds.Count == 0)
                {
                    ShowMessage("กรุณาเลือกรายการที่ต้องการจ่าย", false);
                    return;
                }

                // Create payment using the existing PaymentAffiliate logic
                // For now, redirect to the old page with pre-selected affiliate
                Response.Redirect("/Admin/PaymentAffiliate?affiliate=" + ddlSelectAffiliate.SelectedValue);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        #endregion

        #region Tab 4: Payment History

        private void LoadPaymentHistory()
        {
            try
            {
                string couponCode = ddlHistoryAffiliate.SelectedValue;
                int year = 0;
                int.TryParse(ddlHistoryYear.SelectedValue, out year);

                DataTable history = affiliateService.GetPaymentHistory(couponCode, year);
                gvHistory.DataSource = history;
                gvHistory.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดประวัติ: " + ex.Message, false);
            }
        }

        protected void ddlHistoryAffiliate_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadPaymentHistory();
        }

        protected void ddlHistoryYear_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadPaymentHistory();
        }

        protected void gvHistory_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ViewVoucher")
            {
                string paymentId = e.CommandArgument.ToString();
                // Open PDF in new window
                string script = string.Format("window.open('/Account/Report/{0}.pdf', '_blank');", paymentId);
                ClientScript.RegisterStartupScript(this.GetType(), "openPdf", script, true);
            }
        }

        #endregion

        #region Helper Methods

        private void ShowMessage(string message, bool isSuccess)
        {
            pnlMessage.Visible = true;
            pnlMessage.CssClass = isSuccess ? "alert alert-success" : "alert alert-error";
            lblMessage.Text = message;
        }

        #endregion
    }
}
