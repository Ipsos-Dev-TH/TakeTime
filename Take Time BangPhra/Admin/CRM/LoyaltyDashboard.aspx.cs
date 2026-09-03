// ===========================================================================
// LoyaltyDashboard.aspx.cs
// Loyalty Program Dashboard - Manage rewards, view tier performance, track transactions
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using Take_Time_BangPhra.Class;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.CRM
{
    public partial class LoyaltyDashboard : System.Web.UI.Page
    {
        private readonly string _connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;
        private LoyaltyService _loyaltyService;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.CrmLoyalty)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (!Feature.Guard(this, "Loyalty", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            _code = new code();
            _loyaltyService = new LoyaltyService(_connectionString);

            // Check permissions
            try
            {
                if (Session["permission"]?.ToString() != "True" ||
                    (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
                {
                    Response.Redirect("/Default");
                    return;
                }
            }
            catch
            {
                Response.Redirect("/Default");
                return;
            }

            if (!IsPostBack)
            {
                LoadDashboard();
            }
        }

        private void LoadDashboard()
        {
            LoadStatistics();
            LoadTierBreakdown();
            LoadRecentTransactions();
            LoadRewards();
            LoadRewardCategories();
            LoadTierPerformance();
            LoadMonthlyTrends();
        }

        #region Statistics

        private void LoadStatistics()
        {
            try
            {
                // Total members
                DataTable dtTotal = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT COUNT(*) FROM Customer_Loyalty", null);
                int totalMembers = dtTotal.Rows.Count > 0 ? Convert.ToInt32(dtTotal.Rows[0][0]) : 0;
                lblTotalMembers.Text = totalMembers.ToString("N0");

                // New members this month
                DataTable dtNewMonth = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) FROM Customer_Loyalty
                      WHERE MONTH(MemberSince) = MONTH(GETDATE())
                        AND YEAR(MemberSince) = YEAR(GETDATE())", null);
                int newThisMonth = dtNewMonth.Rows.Count > 0 ? Convert.ToInt32(dtNewMonth.Rows[0][0]) : 0;
                lblMembersChange.Text = newThisMonth.ToString("N0");

                // Active members (had transaction in last 90 days)
                DataTable dtActive = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(DISTINCT Customer_MobilePhone) FROM Loyalty_Transactions
                      WHERE TransactionDate >= DATEADD(DAY, -90, GETDATE())", null);
                int activeMembers = dtActive.Rows.Count > 0 ? Convert.ToInt32(dtActive.Rows[0][0]) : 0;
                lblActiveMembers.Text = activeMembers.ToString("N0");
                decimal activePercent = totalMembers > 0 ? (decimal)activeMembers / totalMembers * 100 : 0;
                lblActivePercent.Text = activePercent.ToString("N1");

                // Points earned this month
                DataTable dtEarned = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ISNULL(SUM(Points), 0) FROM Loyalty_Transactions
                      WHERE TransactionType = 'EARN'
                        AND MONTH(TransactionDate) = MONTH(GETDATE())
                        AND YEAR(TransactionDate) = YEAR(GETDATE())", null);
                int pointsEarned = dtEarned.Rows.Count > 0 ? Convert.ToInt32(dtEarned.Rows[0][0]) : 0;
                lblPointsEarned.Text = pointsEarned.ToString("N0");

                // Points earned last month for comparison
                DataTable dtEarnedLast = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ISNULL(SUM(Points), 0) FROM Loyalty_Transactions
                      WHERE TransactionType = 'EARN'
                        AND MONTH(TransactionDate) = MONTH(DATEADD(MONTH, -1, GETDATE()))
                        AND YEAR(TransactionDate) = YEAR(DATEADD(MONTH, -1, GETDATE()))", null);
                int pointsEarnedLast = dtEarnedLast.Rows.Count > 0 ? Convert.ToInt32(dtEarnedLast.Rows[0][0]) : 0;
                decimal earnedChange = pointsEarnedLast > 0 ? ((decimal)(pointsEarned - pointsEarnedLast) / pointsEarnedLast * 100) : 0;
                lblPointsEarnedChange.Text = Math.Abs(earnedChange).ToString("N1");

                // Points redeemed this month
                DataTable dtRedeemed = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ISNULL(SUM(Points), 0) FROM Loyalty_Transactions
                      WHERE TransactionType = 'REDEEM'
                        AND MONTH(TransactionDate) = MONTH(GETDATE())
                        AND YEAR(TransactionDate) = YEAR(GETDATE())", null);
                int pointsRedeemed = dtRedeemed.Rows.Count > 0 ? Convert.ToInt32(dtRedeemed.Rows[0][0]) : 0;
                lblPointsRedeemed.Text = Math.Abs(pointsRedeemed).ToString("N0");

                // Redemption rate
                decimal redemptionRate = pointsEarned > 0 ? (decimal)Math.Abs(pointsRedeemed) / pointsEarned * 100 : 0;
                lblRedemptionRate.Text = redemptionRate.ToString("N1");
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading statistics: " + ex.Message, "error");
            }
        }

        #endregion

        #region Tier Breakdown

        private void LoadTierBreakdown()
        {
            try
            {
                DataTable dtBreakdown = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        LT.ID,
                        LT.TierName,
                        LT.TierNameEN,
                        LT.MinPoints,
                        LT.PointsMultiplier,
                        LT.DiscountPercent,
                        LT.TierColor,
                        COUNT(CL.Customer_MobilePhone) AS MemberCount,
                        CAST(COUNT(CL.Customer_MobilePhone) * 100.0 / NULLIF((SELECT COUNT(*) FROM Customer_Loyalty), 0) AS DECIMAL(5,2)) AS MemberPercent
                      FROM Loyalty_Tiers LT
                      LEFT JOIN Customer_Loyalty CL ON CL.CurrentTier_ID = LT.ID
                      WHERE LT.IsActive = 1
                      GROUP BY LT.ID, LT.TierName, LT.TierNameEN, LT.MinPoints, LT.PointsMultiplier,
                               LT.DiscountPercent, LT.TierColor, LT.DisplayOrder
                      ORDER BY LT.DisplayOrder", null);

                int totalCount = 0;
                foreach (DataRow row in dtBreakdown.Rows)
                {
                    totalCount += Convert.ToInt32(row["MemberCount"]);
                }
                lblTotalMembersBreakdown.Text = totalCount.ToString("N0");

                rptTierBreakdown.DataSource = dtBreakdown;
                rptTierBreakdown.DataBind();
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading tier breakdown: " + ex.Message, "error");
            }
        }

        protected string GetTierIcon(object tierNameEN)
        {
            string tier = tierNameEN?.ToString() ?? "";
            switch (tier.ToUpper())
            {
                case "MEMBER":
                    return "<i class='fa fa-user'></i>";
                case "SILVER":
                    return "<i class='fa fa-certificate'></i>";
                case "GOLD":
                    return "<i class='fa fa-star'></i>";
                case "PLATINUM":
                    return "<i class='fa fa-gem'></i>";
                case "VIP":
                    return "<i class='fa fa-crown'></i>";
                default:
                    return "<i class='fa fa-trophy'></i>";
            }
        }

        #endregion

        #region Recent Transactions

        private void LoadRecentTransactions()
        {
            try
            {
                DataTable dtTransactions = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 20
                        LT.TransactionType,
                        LT.Points,
                        LT.Description,
                        LT.Customer_MobilePhone,
                        LT.TransactionDate
                      FROM Loyalty_Transactions LT
                      ORDER BY LT.TransactionDate DESC", null);

                if (dtTransactions.Rows.Count > 0)
                {
                    rptRecentTransactions.DataSource = dtTransactions;
                    rptRecentTransactions.DataBind();
                    lblNoTransactions.Visible = false;
                }
                else
                {
                    lblNoTransactions.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading transactions: " + ex.Message, "error");
            }
        }

        protected string FormatTransactionType(object transactionType)
        {
            string type = transactionType?.ToString() ?? "";
            switch (type.ToUpper())
            {
                case "EARN":
                    return "Points Earned";
                case "REDEEM":
                    return "Points Redeemed";
                case "EXPIRE":
                    return "Points Expired";
                case "ADJUST":
                    return "Manual Adjustment";
                default:
                    return type;
            }
        }

        protected string GetTransactionClass(object transactionType)
        {
            string type = transactionType?.ToString() ?? "";
            return type.ToUpper() == "EARN" || type.ToUpper() == "ADJUST" ? "positive" : "negative";
        }

        protected string GetTransactionSign(object transactionType)
        {
            string type = transactionType?.ToString() ?? "";
            return type.ToUpper() == "EARN" || type.ToUpper() == "ADJUST" ? "+" : "-";
        }

        #endregion

        #region Rewards Management

        private void LoadRewards()
        {
            try
            {
                // Check if Loyalty_Rewards table exists first
                DataTable dtCheck = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Loyalty_Rewards'", null);

                if (dtCheck.Rows.Count == 0)
                {
                    lblNoRewards.Visible = true;
                    return;
                }

                DataTable dtRewards = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        ID,
                        RewardName,
                        RewardNameEN,
                        Description,
                        PointsCost AS PointsRequired,
                        StockQuantity AS AvailableQuantity,
                        CreatedDate AS ValidFrom,
                        DATEADD(DAY, ISNULL(ValidityDays, 365), CreatedDate) AS ValidTo,
                        IsActive,
                        ISNULL(Category, 'General') AS CategoryName
                      FROM Loyalty_Rewards
                      ORDER BY DisplayOrder, RewardName", null);

                if (dtRewards.Rows.Count > 0)
                {
                    rptRewards.DataSource = dtRewards;
                    rptRewards.DataBind();
                    lblNoRewards.Visible = false;
                }
                else
                {
                    lblNoRewards.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading rewards: " + ex.Message, "error");
                lblNoRewards.Visible = true;
            }
        }

        private void LoadRewardCategories()
        {
            // Loyalty_Rewards.Category is a free VARCHAR code, not an FK.
            ddlRewardCategory.Items.Clear();
            ddlRewardCategory.Items.Add(new ListItem("ส่วนลด (Discount)", "DISCOUNT"));
            ddlRewardCategory.Items.Add(new ListItem("พักฟรี (Free Night)", "FREE_NIGHT"));
            ddlRewardCategory.Items.Add(new ListItem("อัพเกรดห้อง (Upgrade)", "UPGRADE"));
            ddlRewardCategory.Items.Add(new ListItem("บริการ (Service)", "SERVICE"));
            ddlRewardCategory.Items.Add(new ListItem("บัตรกำนัล (Voucher)", "VOUCHER"));
            ddlRewardCategory.Items.Add(new ListItem("ของขวัญ (Gift)", "GIFT"));
        }

        protected void btnAddReward_Click(object sender, EventArgs e)
        {
            lblModalTitle.Text = "Add New Reward";
            ClearRewardForm();
            hfEditingRewardId.Value = "0";
            pnlRewardModal.CssClass = "form-modal show";
        }

        protected void btnSaveReward_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtRewardName.Text) || string.IsNullOrEmpty(txtPointsRequired.Text))
                {
                    ShowAlert("Please fill in all required fields", "error");
                    return;
                }

                int rewardId = Convert.ToInt32(hfEditingRewardId.Value);

                // Derive validity (days) from the date range if provided
                int validityDays = 365;
                if (!string.IsNullOrEmpty(txtValidFrom.Text) && !string.IsNullOrEmpty(txtValidTo.Text))
                {
                    int diff = (Convert.ToDateTime(txtValidTo.Text) - Convert.ToDateTime(txtValidFrom.Text)).Days;
                    if (diff > 0) validityDays = diff;
                }

                var parameters = new Dictionary<string, object>
                {
                    { "@RewardName", txtRewardName.Text },
                    { "@RewardNameEN", txtRewardNameEN.Text },
                    { "@Description", txtRewardDescription.Text },
                    { "@Category", ddlRewardCategory.SelectedValue },
                    { "@PointsCost", Convert.ToInt32(txtPointsRequired.Text) },
                    { "@StockQuantity", string.IsNullOrEmpty(txtAvailableQuantity.Text) ? DBNull.Value : (object)Convert.ToInt32(txtAvailableQuantity.Text) },
                    { "@ValidityDays", validityDays }
                };

                if (rewardId == 0)
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"INSERT INTO Loyalty_Rewards
                          (RewardName, RewardNameEN, Description, Category, PointsCost,
                           StockQuantity, MinTierRequired, ValidityDays, IsActive, DisplayOrder, CreatedDate)
                          VALUES (@RewardName, @RewardNameEN, @Description, @Category, @PointsCost,
                                  @StockQuantity, 1, @ValidityDays, 1, 999, GETDATE())", parameters);
                    ShowAlert("Reward added successfully!", "success");
                }
                else
                {
                    parameters["@ID"] = rewardId;
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Loyalty_Rewards SET
                          RewardName = @RewardName,
                          RewardNameEN = @RewardNameEN,
                          Description = @Description,
                          Category = @Category,
                          PointsCost = @PointsCost,
                          StockQuantity = @StockQuantity,
                          ValidityDays = @ValidityDays
                          WHERE ID = @ID", parameters);
                    ShowAlert("Reward updated successfully!", "success");
                }

                pnlRewardModal.CssClass = "form-modal";
                LoadRewards();
            }
            catch (Exception ex)
            {
                ShowAlert("Error saving reward: " + ex.Message, "error");
            }
        }

        protected void btnCancelReward_Click(object sender, EventArgs e)
        {
            pnlRewardModal.CssClass = "form-modal";
        }

        protected void rptRewards_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            int rewardId = Convert.ToInt32(e.CommandArgument);

            if (e.CommandName == "EditReward")
            {
                LoadRewardForEdit(rewardId);
            }
            else if (e.CommandName == "ToggleStatus")
            {
                ToggleRewardStatus(rewardId);
            }
        }

        private void LoadRewardForEdit(int rewardId)
        {
            try
            {
                var parameters = new Dictionary<string, object> { { "@ID", rewardId } };
                DataTable dtReward = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM Loyalty_Rewards WHERE ID = @ID", parameters);

                if (dtReward.Rows.Count > 0)
                {
                    DataRow row = dtReward.Rows[0];
                    lblModalTitle.Text = "Edit Reward";
                    txtRewardName.Text = row["RewardName"].ToString();
                    txtRewardNameEN.Text = row["RewardNameEN"] != DBNull.Value ? row["RewardNameEN"].ToString() : "";
                    txtRewardDescription.Text = row["Description"] != DBNull.Value ? row["Description"].ToString() : "";

                    if (row["Category"] != DBNull.Value)
                    {
                        ListItem li = ddlRewardCategory.Items.FindByValue(row["Category"].ToString());
                        if (li != null) ddlRewardCategory.ClearSelection();
                        if (li != null) li.Selected = true;
                    }

                    txtPointsRequired.Text = row["PointsCost"].ToString();

                    if (row["StockQuantity"] != DBNull.Value)
                        txtAvailableQuantity.Text = row["StockQuantity"].ToString();

                    int validityDays = row["ValidityDays"] != DBNull.Value ? Convert.ToInt32(row["ValidityDays"]) : 365;
                    txtValidFrom.Text = DateTime.Now.ToString("yyyy-MM-dd");
                    txtValidTo.Text = DateTime.Now.AddDays(validityDays).ToString("yyyy-MM-dd");

                    hfEditingRewardId.Value = rewardId.ToString();
                    pnlRewardModal.CssClass = "form-modal show";
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading reward: " + ex.Message, "error");
            }
        }

        private void ToggleRewardStatus(int rewardId)
        {
            try
            {
                var parameters = new Dictionary<string, object> { { "@ID", rewardId } };
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Loyalty_Rewards SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE ID = @ID",
                    parameters);

                ShowAlert("Reward status updated!", "success");
                LoadRewards();
            }
            catch (Exception ex)
            {
                ShowAlert("Error updating reward status: " + ex.Message, "error");
            }
        }

        private void ClearRewardForm()
        {
            txtRewardName.Text = "";
            txtRewardNameEN.Text = "";
            txtRewardDescription.Text = "";
            ddlRewardCategory.SelectedIndex = 0;
            txtPointsRequired.Text = "";
            txtAvailableQuantity.Text = "";
            txtValidFrom.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtValidTo.Text = DateTime.Now.AddMonths(6).ToString("yyyy-MM-dd");
        }

        #endregion

        #region Reports

        private void LoadTierPerformance()
        {
            try
            {
                DataTable dtPerformance = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        LT.TierName,
                        COUNT(DISTINCT CL.Customer_MobilePhone) AS MemberCount,
                        ISNULL(SUM(CASE WHEN LTR.TransactionType = 'EARN' THEN LTR.Points ELSE 0 END), 0) AS TotalPointsEarned,
                        ISNULL(SUM(CASE WHEN LTR.TransactionType = 'REDEEM' THEN ABS(LTR.Points) ELSE 0 END), 0) AS TotalPointsRedeemed,
                        ISNULL(AVG(CL.TotalPoints), 0) AS AvgPointsBalance,
                        ISNULL(SUM(R.TotalPrice), 0) AS TotalRevenue,
                        CASE
                            WHEN COUNT(DISTINCT CL.Customer_MobilePhone) > 0
                            THEN ISNULL(SUM(R.TotalPrice), 0) / COUNT(DISTINCT CL.Customer_MobilePhone)
                            ELSE 0
                        END AS AvgRevenuePerMember
                      FROM Loyalty_Tiers LT
                      LEFT JOIN Customer_Loyalty CL ON CL.CurrentTier_ID = LT.ID
                      LEFT JOIN Loyalty_Transactions LTR ON LTR.Customer_MobilePhone = CL.Customer_MobilePhone
                      LEFT JOIN Reservation R ON R.Customer_MobilePhone = CL.Customer_MobilePhone
                      WHERE LT.IsActive = 1
                      GROUP BY LT.ID, LT.TierName, LT.DisplayOrder
                      ORDER BY LT.DisplayOrder", null);

                gvTierPerformance.DataSource = dtPerformance;
                gvTierPerformance.DataBind();
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading tier performance: " + ex.Message, "error");
            }
        }

        private void LoadMonthlyTrends()
        {
            try
            {
                DataTable dtTrends = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 12
                        DATEFROMPARTS(YEAR(MonthDate), MONTH(MonthDate), 1) AS Month,
                        NewMembers,
                        PointsEarned,
                        PointsRedeemed,
                        CASE
                            WHEN PointsEarned > 0
                            THEN CAST(PointsRedeemed * 100.0 / PointsEarned AS DECIMAL(10,2))
                            ELSE 0
                        END AS RedemptionRate
                      FROM (
                        SELECT
                            DATEFROMPARTS(YEAR(LT.TransactionDate), MONTH(LT.TransactionDate), 1) AS MonthDate,
                            COUNT(DISTINCT CASE WHEN CL.MemberSince >= DATEFROMPARTS(YEAR(LT.TransactionDate), MONTH(LT.TransactionDate), 1)
                                                  AND CL.MemberSince < DATEADD(MONTH, 1, DATEFROMPARTS(YEAR(LT.TransactionDate), MONTH(LT.TransactionDate), 1))
                                                THEN CL.Customer_MobilePhone END) AS NewMembers,
                            SUM(CASE WHEN LT.TransactionType = 'EARN' THEN LT.Points ELSE 0 END) AS PointsEarned,
                            SUM(CASE WHEN LT.TransactionType = 'REDEEM' THEN ABS(LT.Points) ELSE 0 END) AS PointsRedeemed
                        FROM Loyalty_Transactions LT
                        LEFT JOIN Customer_Loyalty CL ON CL.Customer_MobilePhone = LT.Customer_MobilePhone
                        WHERE LT.TransactionDate >= DATEADD(MONTH, -12, GETDATE())
                        GROUP BY DATEFROMPARTS(YEAR(LT.TransactionDate), MONTH(LT.TransactionDate), 1)
                      ) AS MonthlyData
                      ORDER BY Month DESC", null);

                gvMonthlyTrends.DataSource = dtTrends;
                gvMonthlyTrends.DataBind();
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading monthly trends: " + ex.Message, "error");
            }
        }

        #endregion

        #region Tab Navigation

        protected void btnTabRewards_Click(object sender, EventArgs e)
        {
            ShowTab(1);
        }

        protected void btnTabReports_Click(object sender, EventArgs e)
        {
            ShowTab(2);
        }

        private void ShowTab(int tabNumber)
        {
            btnTabRewards.CssClass = "tab-button";
            btnTabReports.CssClass = "tab-button";

            pnlRewards.Visible = false;
            pnlReports.Visible = false;

            switch (tabNumber)
            {
                case 1:
                    btnTabRewards.CssClass = "tab-button active";
                    pnlRewards.Visible = true;
                    LoadRewards();
                    break;
                case 2:
                    btnTabReports.CssClass = "tab-button active";
                    pnlReports.Visible = true;
                    LoadTierPerformance();
                    LoadMonthlyTrends();
                    break;
            }
        }

        #endregion

        #region Helper Methods

        private void ShowAlert(string message, string type)
        {
            pnlAlert.Visible = true;
            lblAlertMessage.Text = message;

            string cssClass = "alert";
            switch (type.ToLower())
            {
                case "success":
                    cssClass += " alert-success";
                    break;
                case "error":
                    cssClass += " alert-error";
                    break;
            }

            alertBox.Attributes["class"] = cssClass;
        }

        #endregion
    }
}
