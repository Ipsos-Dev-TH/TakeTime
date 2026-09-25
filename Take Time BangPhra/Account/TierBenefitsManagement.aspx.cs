// ===========================================================================
// TierBenefitsManagement.aspx.cs
// Admin page for managing loyalty tier benefits and product category discounts
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

namespace Take_Time_BangPhra.Account
{
    public partial class TierBenefitsManagement : System.Web.UI.Page
    {
        private readonly string _connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;
        private TierBenefitsService _tierBenefitsService;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Loyalty", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            _code = new code();
            _tierBenefitsService = new TierBenefitsService(_connectionString);

            // Check permissions
            try
            {
                if (Session["permission"].ToString() != "True" ||
                    (Session["User"].ToString() != "Owner" && Session["User"].ToString() != "Admin"))
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
                LoadStatistics();
                LoadTiersAndBenefits();
                LoadDropDowns();
                LoadCategoryDiscounts();
                LoadUsageStatistics();
            }
        }

        #region Statistics

        private void LoadStatistics()
        {
            try
            {
                // Total members
                DataTable dtMembers = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT COUNT(*) FROM Customer_Loyalty",
                    null);
                lblTotalMembers.Text = dtMembers.Rows[0][0].ToString();

                // Active benefits
                DataTable dtBenefits = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT COUNT(*) FROM Loyalty_Tier_Benefits WHERE IsActive = 1",
                    null);
                lblActiveBenefits.Text = dtBenefits.Rows[0][0].ToString();

                // Category discounts
                DataTable dtDiscounts = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT COUNT(*) FROM Loyalty_Product_Category_Discounts WHERE IsActive = 1",
                    null);
                lblCategoryDiscounts.Text = dtDiscounts.Rows[0][0].ToString();

                // Total discounts given
                DataTable dtTotalDiscounts = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ISNULL(SUM(DiscountAmount), 0) FROM Loyalty_Benefit_Usage " +
                    "WHERE CAST(UsageDate AS DATE) >= DATEADD(MONTH, -1, GETDATE())",
                    null);
                decimal totalDiscounts = Convert.ToDecimal(dtTotalDiscounts.Rows[0][0]);
                lblTotalDiscounts.Text = totalDiscounts.ToString("N2");
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading statistics: " + ex.Message, "error");
            }
        }

        #endregion

        #region Tab 1: Tiers & Benefits

        private void LoadTiersAndBenefits()
        {
            try
            {
                DataTable dtTiers = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT *, DiscountPercent AS AccommodationDiscountPercent FROM Loyalty_Tiers WHERE IsActive = 1 ORDER BY DisplayOrder",
                    null);

                rptTiers.DataSource = dtTiers;
                rptTiers.DataBind();
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading tiers: " + ex.Message, "error");
            }
        }

        protected void rptTiers_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            if (e.Item.ItemType == ListItemType.Item || e.Item.ItemType == ListItemType.AlternatingItem)
            {
                DataRowView drv = (DataRowView)e.Item.DataItem;
                int tierId = Convert.ToInt32(drv["ID"]);

                Repeater rptBenefits = (Repeater)e.Item.FindControl("rptBenefits");
                if (rptBenefits != null)
                {
                    try
                    {
                        var parameters = new Dictionary<string, object>
                        {
                            { "@TierId", tierId }
                        };

                        DataTable dtBenefits = _code.DatabaseQuerySafe(_connectionString,
                            "SELECT * FROM Loyalty_Tier_Benefits " +
                            "WHERE Tier_ID = @TierId AND IsActive = 1 " +
                            "ORDER BY DisplayOrder",
                            parameters);

                        rptBenefits.DataSource = dtBenefits;
                        rptBenefits.DataBind();
                    }
                    catch (Exception ex)
                    {
                        ShowAlert($"Error loading benefits for tier {tierId}: " + ex.Message, "error");
                    }
                }
            }
        }

        protected string GetBenefitDisplayValue(object discountType, object discountValue,
            object maxDiscountAmount, object timeValue, object quantityValue)
        {
            try
            {
                string type = discountType?.ToString() ?? "";

                switch (type)
                {
                    case "PERCENTAGE":
                        decimal percent = discountValue != DBNull.Value ? Convert.ToDecimal(discountValue) : 0;
                        string result = $"{percent:N2}% discount";
                        if (maxDiscountAmount != DBNull.Value && Convert.ToDecimal(maxDiscountAmount) > 0)
                        {
                            result += $" (max ฿{Convert.ToDecimal(maxDiscountAmount):N0})";
                        }
                        return result;

                    case "FIXED_AMOUNT":
                        decimal amount = discountValue != DBNull.Value ? Convert.ToDecimal(discountValue) : 0;
                        return $"฿{amount:N2} discount";

                    case "FREE":
                        if (timeValue != DBNull.Value && Convert.ToInt32(timeValue) > 0)
                        {
                            return $"{Convert.ToInt32(timeValue)} hours";
                        }
                        else if (quantityValue != DBNull.Value && Convert.ToInt32(quantityValue) > 0)
                        {
                            return $"{Convert.ToInt32(quantityValue)} items";
                        }
                        return "Complimentary";

                    default:
                        return "Available";
                }
            }
            catch
            {
                return "N/A";
            }
        }

        #endregion

        #region Tab 2: Category Discounts

        private void LoadDropDowns()
        {
            try
            {
                // Load Tiers
                DataTable dtTiers = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ID, TierName FROM Loyalty_Tiers WHERE IsActive = 1 ORDER BY DisplayOrder",
                    null);
                ddlTier.DataSource = dtTiers;
                ddlTier.DataTextField = "TierName";
                ddlTier.DataValueField = "ID";
                ddlTier.DataBind();
                ddlTier.Items.Insert(0, new ListItem("-- Select Tier --", "0"));

                // Load Product Categories
                DataTable dtCategories = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ID, Category_Name FROM Product_Category WHERE Status = 'True' ORDER BY Category_Name",
                    null);
                ddlProductCategory.DataSource = dtCategories;
                ddlProductCategory.DataTextField = "Category_Name";
                ddlProductCategory.DataValueField = "ID";
                ddlProductCategory.DataBind();
                ddlProductCategory.Items.Insert(0, new ListItem("-- Select Category --", "0"));
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading dropdowns: " + ex.Message, "error");
            }
        }

        private void LoadCategoryDiscounts()
        {
            try
            {
                DataTable dtDiscounts = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        LPCD.ID,
                        LT.TierName,
                        PC.Category_Name AS CategoryName,
                        LPCD.DiscountPercent,
                        LPCD.MaxDiscountAmount,
                        LPCD.MinPurchaseAmount,
                        LPCD.IsActive,
                        LPCD.ValidFrom,
                        LPCD.ValidTo
                      FROM Loyalty_Product_Category_Discounts LPCD
                      INNER JOIN Loyalty_Tiers LT ON LT.ID = LPCD.Tier_ID
                      INNER JOIN Product_Category PC ON PC.ID = LPCD.ProductCategory_ID
                      ORDER BY LT.DisplayOrder, PC.Category_Name",
                    null);

                gvCategoryDiscounts.DataSource = dtDiscounts;
                gvCategoryDiscounts.DataBind();
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading category discounts: " + ex.Message, "error");
            }
        }

        protected void btnSaveCategoryDiscount_Click(object sender, EventArgs e)
        {
            try
            {
                // Validate inputs
                if (ddlTier.SelectedValue == "0")
                {
                    ShowAlert("Please select a tier", "error");
                    return;
                }

                if (ddlProductCategory.SelectedValue == "0")
                {
                    ShowAlert("Please select a product category", "error");
                    return;
                }

                if (string.IsNullOrEmpty(txtDiscountPercent.Text))
                {
                    ShowAlert("Please enter discount percent", "error");
                    return;
                }

                byte tierId = Convert.ToByte(ddlTier.SelectedValue);
                long categoryId = Convert.ToInt64(ddlProductCategory.SelectedValue);
                decimal discountPercent = Convert.ToDecimal(txtDiscountPercent.Text);

                decimal? maxDiscountAmount = null;
                if (!string.IsNullOrEmpty(txtMaxDiscount.Text))
                {
                    maxDiscountAmount = Convert.ToDecimal(txtMaxDiscount.Text);
                }

                decimal? minPurchaseAmount = null;
                if (!string.IsNullOrEmpty(txtMinPurchase.Text))
                {
                    minPurchaseAmount = Convert.ToDecimal(txtMinPurchase.Text);
                }

                DateTime? validFrom = null;
                if (!string.IsNullOrEmpty(txtValidFrom.Text))
                {
                    validFrom = Convert.ToDateTime(txtValidFrom.Text);
                }

                DateTime? validTo = null;
                if (!string.IsNullOrEmpty(txtValidTo.Text))
                {
                    validTo = Convert.ToDateTime(txtValidTo.Text);
                }

                short? adminId = Session["UserID"] != null ? (short?)Convert.ToInt16(Session["UserID"]) : null;

                bool success = _tierBenefitsService.UpsertProductCategoryDiscount(
                    tierId,
                    categoryId,
                    discountPercent,
                    maxDiscountAmount,
                    minPurchaseAmount,
                    validFrom,
                    validTo,
                    adminId);

                if (success)
                {
                    ShowAlert("Category discount saved successfully!", "success");
                    ClearCategoryDiscountForm();
                    LoadCategoryDiscounts();
                    LoadStatistics();
                }
                else
                {
                    ShowAlert("Failed to save category discount", "error");
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error saving category discount: " + ex.Message, "error");
            }
        }

        protected void btnCancelEdit_Click(object sender, EventArgs e)
        {
            ClearCategoryDiscountForm();
        }

        private void ClearCategoryDiscountForm()
        {
            ddlTier.SelectedIndex = 0;
            ddlProductCategory.SelectedIndex = 0;
            txtDiscountPercent.Text = "";
            txtMaxDiscount.Text = "";
            txtMinPurchase.Text = "";
            txtValidFrom.Text = "";
            txtValidTo.Text = "";
            hfEditingId.Value = "0";
        }

        protected void gvCategoryDiscounts_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            int id = Convert.ToInt32(e.CommandArgument);

            if (e.CommandName == "EditDiscount")
            {
                // Load discount data for editing
                var parameters = new Dictionary<string, object> { { "@ID", id } };
                DataTable dtDiscount = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT * FROM Loyalty_Product_Category_Discounts WHERE ID = @ID",
                    parameters);

                if (dtDiscount.Rows.Count > 0)
                {
                    DataRow row = dtDiscount.Rows[0];
                    ddlTier.SelectedValue = row["Tier_ID"].ToString();
                    ddlProductCategory.SelectedValue = row["ProductCategory_ID"].ToString();
                    txtDiscountPercent.Text = row["DiscountPercent"].ToString();

                    if (row["MaxDiscountAmount"] != DBNull.Value)
                        txtMaxDiscount.Text = row["MaxDiscountAmount"].ToString();

                    if (row["MinPurchaseAmount"] != DBNull.Value)
                        txtMinPurchase.Text = row["MinPurchaseAmount"].ToString();

                    if (row["ValidFrom"] != DBNull.Value)
                        txtValidFrom.Text = Convert.ToDateTime(row["ValidFrom"]).ToString("yyyy-MM-dd");

                    if (row["ValidTo"] != DBNull.Value)
                        txtValidTo.Text = Convert.ToDateTime(row["ValidTo"]).ToString("yyyy-MM-dd");

                    hfEditingId.Value = id.ToString();
                }
            }
            else if (e.CommandName == "ToggleStatus")
            {
                // Toggle active status
                try
                {
                    var parameters = new Dictionary<string, object> { { "@ID", id } };
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Loyalty_Product_Category_Discounts
                          SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END
                          WHERE ID = @ID",
                        parameters);

                    ShowAlert("Status updated successfully!", "success");
                    LoadCategoryDiscounts();
                    LoadStatistics();
                }
                catch (Exception ex)
                {
                    ShowAlert("Error toggling status: " + ex.Message, "error");
                }
            }
        }

        #endregion

        #region Tab 3: Usage Statistics

        private void LoadUsageStatistics()
        {
            try
            {
                // Default to last 30 days
                if (string.IsNullOrEmpty(txtStatsFromDate.Text))
                {
                    txtStatsFromDate.Text = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                }

                if (string.IsNullOrEmpty(txtStatsToDate.Text))
                {
                    txtStatsToDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
                }

                var parameters = new Dictionary<string, object>
                {
                    { "@FromDate", Convert.ToDateTime(txtStatsFromDate.Text) },
                    { "@ToDate", Convert.ToDateTime(txtStatsToDate.Text).AddDays(1) }
                };

                DataTable dtStats = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        BenefitType,
                        COUNT(*) AS UsageCount,
                        SUM(DiscountAmount) AS TotalDiscountAmount,
                        AVG(DiscountAmount) AS AvgDiscountAmount,
                        COUNT(DISTINCT Customer_MobilePhone) AS UniqueCustomers
                      FROM Loyalty_Benefit_Usage
                      WHERE UsageDate >= @FromDate AND UsageDate < @ToDate
                      GROUP BY BenefitType
                      ORDER BY TotalDiscountAmount DESC",
                    parameters);

                gvUsageStatistics.DataSource = dtStats;
                gvUsageStatistics.DataBind();
            }
            catch (Exception ex)
            {
                ShowAlert("Error loading usage statistics: " + ex.Message, "error");
            }
        }

        protected void btnLoadStats_Click(object sender, EventArgs e)
        {
            LoadUsageStatistics();
        }

        #endregion

        #region Tab Navigation

        protected void btnTabTiers_Click(object sender, EventArgs e)
        {
            ShowTab(1);
        }

        protected void btnTabCategoryDiscounts_Click(object sender, EventArgs e)
        {
            ShowTab(2);
        }

        protected void btnTabUsageStatistics_Click(object sender, EventArgs e)
        {
            ShowTab(3);
        }

        private void ShowTab(int tabNumber)
        {
            // Reset all tab buttons
            btnTabTiers.CssClass = "tab-button";
            btnTabCategoryDiscounts.CssClass = "tab-button";
            btnTabUsageStatistics.CssClass = "tab-button";

            // Hide all panels
            pnlTiersBenefits.Visible = false;
            pnlCategoryDiscounts.Visible = false;
            pnlUsageStatistics.Visible = false;

            // Show selected tab
            switch (tabNumber)
            {
                case 1:
                    btnTabTiers.CssClass = "tab-button active";
                    pnlTiersBenefits.Visible = true;
                    LoadTiersAndBenefits();
                    break;
                case 2:
                    btnTabCategoryDiscounts.CssClass = "tab-button active";
                    pnlCategoryDiscounts.Visible = true;
                    LoadCategoryDiscounts();
                    break;
                case 3:
                    btnTabUsageStatistics.CssClass = "tab-button active";
                    pnlUsageStatistics.Visible = true;
                    LoadUsageStatistics();
                    break;
            }
        }

        #endregion

        #region Helpers

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
                case "info":
                    cssClass += " alert-info";
                    break;
            }

            alertBox.Attributes["class"] = cssClass;
        }

        #endregion
    }
}
