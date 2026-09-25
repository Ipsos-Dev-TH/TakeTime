// ===========================================================================
// MembershipManagement.aspx.cs
// Admin member management for the restructured dual-pool loyalty system.
// Search members, view/adjust points, manage reward redemptions.
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Configuration;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.CRM
{
    public partial class MembershipManagement : System.Web.UI.Page
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
                LoadTierFilter();
                LoadStatistics();
                LoadMembers();
            }
        }

        private short CurrentAdminId
        {
            get { return Session["UserID"] != null ? Convert.ToInt16(Session["UserID"]) : (short)1; }
        }

        #region Statistics

        private void LoadStatistics()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        (SELECT COUNT(*) FROM Customer_Loyalty) AS TotalMembers,
                        (SELECT ISNULL(SUM(AvailablePoints),0) FROM Customer_Loyalty) AS TotalAvailable,
                        (SELECT COUNT(*) FROM Loyalty_Redemptions WHERE Status = 'PENDING') AS PendingRedemptions,
                        (SELECT ISNULL(SUM(ABS(Points)),0) FROM Loyalty_Transactions
                           WHERE TransactionType = 'REDEEM'
                             AND MONTH(TransactionDate) = MONTH(GETDATE())
                             AND YEAR(TransactionDate) = YEAR(GETDATE())) AS RedeemedMonth", null);

                if (dt.Rows.Count > 0)
                {
                    lblTotalMembers.Text = Convert.ToInt32(dt.Rows[0]["TotalMembers"]).ToString("N0");
                    lblTotalAvailable.Text = Convert.ToInt32(dt.Rows[0]["TotalAvailable"]).ToString("N0");
                    lblPendingRedemptions.Text = Convert.ToInt32(dt.Rows[0]["PendingRedemptions"]).ToString("N0");
                    lblRedeemedMonth.Text = Convert.ToInt32(dt.Rows[0]["RedeemedMonth"]).ToString("N0");
                }
            }
            catch (Exception ex)
            {
                ShowAlert("โหลดสถิติไม่สำเร็จ: " + ex.Message, "error");
            }
        }

        private void LoadTierFilter()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ID, TierName FROM Loyalty_Tiers WHERE IsActive = 1 ORDER BY DisplayOrder", null);
                ddlTierFilter.DataSource = dt;
                ddlTierFilter.DataTextField = "TierName";
                ddlTierFilter.DataValueField = "ID";
                ddlTierFilter.DataBind();
                ddlTierFilter.Items.Insert(0, new ListItem("-- ทุกระดับ --", ""));
            }
            catch { }
        }

        #endregion

        #region Members

        private void LoadMembers()
        {
            try
            {
                byte? tierId = null;
                if (!string.IsNullOrEmpty(ddlTierFilter.SelectedValue))
                    tierId = Convert.ToByte(ddlTierFilter.SelectedValue);

                DataTable dt = _loyaltyService.GetMembers(txtSearch.Text.Trim(), tierId);

                if (dt != null && dt.Rows.Count > 0)
                {
                    gvMembers.DataSource = dt;
                    gvMembers.DataBind();
                    lblNoMembers.Visible = false;
                }
                else
                {
                    gvMembers.DataSource = null;
                    gvMembers.DataBind();
                    lblNoMembers.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowAlert("โหลดรายชื่อสมาชิกไม่สำเร็จ: " + ex.Message, "error");
            }
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            LoadMembers();
        }

        protected void gvMembers_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ViewMember")
            {
                OpenMemberDetail(e.CommandArgument.ToString());
            }
        }

        private void OpenMemberDetail(string phone)
        {
            try
            {
                hfSelectedPhone.Value = phone;
                var info = _loyaltyService.GetLoyaltyInfo(phone);

                if (info != null)
                {
                    lblDetailTier.Text = info.TierName ?? "Member";
                    lblDetailYearly.Text = info.YearlyPoints.ToString("N0");
                    lblDetailAvailable.Text = info.AvailablePoints.ToString("N0");
                }

                // Member name
                DataTable dtName = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Name FROM Customer WHERE MobilePhone = @Phone",
                    new Dictionary<string, object> { { "@Phone", phone } });
                lblMemberName.Text = (dtName.Rows.Count > 0 ? dtName.Rows[0]["Name"].ToString() : phone) + " (" + phone + ")";

                LoadMemberHistory(phone);

                txtAdjustPoints.Text = "";
                txtAdjustReason.Text = "";
                pnlMemberModal.CssClass = "modal show";
            }
            catch (Exception ex)
            {
                ShowAlert("เปิดข้อมูลสมาชิกไม่สำเร็จ: " + ex.Message, "error");
            }
        }

        private void LoadMemberHistory(string phone)
        {
            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT TOP 20 TransactionType, Points, Description, TransactionDate
                  FROM Loyalty_Transactions
                  WHERE Customer_MobilePhone = @Phone
                  ORDER BY TransactionDate DESC",
                new Dictionary<string, object> { { "@Phone", phone } });
            gvMemberHistory.DataSource = dt;
            gvMemberHistory.DataBind();
        }

        protected void btnAdjust_Click(object sender, EventArgs e)
        {
            try
            {
                string phone = hfSelectedPhone.Value;
                if (string.IsNullOrEmpty(phone)) return;

                if (!int.TryParse(txtAdjustPoints.Text, out int points) || points == 0)
                {
                    ShowAlert("กรุณาระบุจำนวนคะแนนที่ถูกต้อง (ไม่เท่ากับ 0)", "error");
                    pnlMemberModal.CssClass = "modal show";
                    return;
                }

                var result = _loyaltyService.AdjustPoints(phone, points,
                    string.IsNullOrEmpty(txtAdjustReason.Text) ? "ปรับคะแนนโดยแอดมิน" : txtAdjustReason.Text,
                    CurrentAdminId, chkAffectTier.Checked);

                if (result.Success)
                {
                    ShowAlert($"ปรับคะแนนสำเร็จ ({(points > 0 ? "+" : "")}{points:N0}) คงเหลือ {result.NewBalance:N0} แต้ม", "success");
                    OpenMemberDetail(phone);
                    LoadStatistics();
                    LoadMembers();
                }
                else
                {
                    ShowAlert("ปรับคะแนนไม่สำเร็จ: " + result.Message, "error");
                    pnlMemberModal.CssClass = "modal show";
                }
            }
            catch (Exception ex)
            {
                ShowAlert("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void btnCloseModal_Click(object sender, EventArgs e)
        {
            pnlMemberModal.CssClass = "modal";
        }

        #endregion

        #region Redemptions

        private void LoadRedemptions()
        {
            try
            {
                string status = ddlRedemptionStatus.SelectedValue;
                DataTable dt = _loyaltyService.GetRedemptions(null, string.IsNullOrEmpty(status) ? null : status);

                if (dt != null && dt.Rows.Count > 0)
                {
                    gvRedemptions.DataSource = dt;
                    gvRedemptions.DataBind();
                    lblNoRedemptions.Visible = false;
                }
                else
                {
                    gvRedemptions.DataSource = null;
                    gvRedemptions.DataBind();
                    lblNoRedemptions.Visible = true;
                }
            }
            catch (Exception ex)
            {
                ShowAlert("โหลดรายการแลกรางวัลไม่สำเร็จ: " + ex.Message, "error");
            }
        }

        protected void ddlRedemptionStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadRedemptions();
        }

        protected void gvRedemptions_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            long id = Convert.ToInt64(e.CommandArgument);

            if (e.CommandName == "Fulfill")
            {
                if (_loyaltyService.UpdateRedemptionStatus(id, "FULFILLED", CurrentAdminId))
                    ShowAlert("ทำเครื่องหมายว่าจ่ายรางวัลแล้ว", "success");
                else
                    ShowAlert("อัปเดตสถานะไม่สำเร็จ", "error");
            }
            else if (e.CommandName == "Cancel")
            {
                if (_loyaltyService.UpdateRedemptionStatus(id, "CANCELLED", CurrentAdminId, "ยกเลิกโดยแอดมิน — คืนคะแนน"))
                    ShowAlert("ยกเลิกและคืนคะแนนให้สมาชิกแล้ว", "success");
                else
                    ShowAlert("ยกเลิกไม่สำเร็จ", "error");
            }

            LoadStatistics();
            LoadRedemptions();
        }

        #endregion

        #region Tabs

        protected void btnTabMembers_Click(object sender, EventArgs e)
        {
            btnTabMembers.CssClass = "tab-btn active";
            btnTabRedemptions.CssClass = "tab-btn";
            pnlMembers.Visible = true;
            pnlRedemptions.Visible = false;
            LoadMembers();
        }

        protected void btnTabRedemptions_Click(object sender, EventArgs e)
        {
            btnTabMembers.CssClass = "tab-btn";
            btnTabRedemptions.CssClass = "tab-btn active";
            pnlMembers.Visible = false;
            pnlRedemptions.Visible = true;
            LoadRedemptions();
        }

        #endregion

        protected string GetStatusLabel(object status)
        {
            switch ((status?.ToString() ?? "").ToUpper())
            {
                case "PENDING": return "รอรับรางวัล";
                case "FULFILLED": return "รับแล้ว";
                case "CANCELLED": return "ยกเลิก";
                case "EXPIRED": return "หมดอายุ";
                default: return status?.ToString() ?? "";
            }
        }

        private void ShowAlert(string message, string type)
        {
            pnlAlert.Visible = true;
            lblAlert.Text = message;
            alertBox.Attributes["class"] = "alert " + (type == "success" ? "alert-success" : "alert-error");
        }
    }
}
