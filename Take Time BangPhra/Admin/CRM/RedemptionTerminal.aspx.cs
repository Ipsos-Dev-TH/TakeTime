// ===========================================================================
// RedemptionTerminal.aspx.cs
// Staff-operated reward redemption terminal.
// Staff selects a reward and scans the customer's QR (or searches by phone)
// to redeem points on the customer's behalf.
// ===========================================================================

using System;
using System.Configuration;
using System.Data;
using System.Web.Services;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.CRM
{
    public partial class RedemptionTerminal : System.Web.UI.Page
    {
        private static string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
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
                LoadRewards();
        }

        private void LoadRewards()
        {
            try
            {
                var codeHelper = new code();
                DataTable dt = codeHelper.DatabaseQuerySafe(ConnStr,
                    @"SELECT ID, RewardName, Description, PointsCost, MinTierRequired
                      FROM Loyalty_Rewards
                      WHERE IsActive = 1 AND (StockQuantity IS NULL OR StockQuantity > 0)
                      ORDER BY DisplayOrder, PointsCost", null);

                if (dt.Rows.Count > 0)
                {
                    rptRewards.DataSource = dt;
                    rptRewards.DataBind();
                    lblNoRewards.Visible = false;
                }
                else
                {
                    lblNoRewards.Visible = true;
                }
            }
            catch
            {
                lblNoRewards.Visible = true;
            }
        }

        // -------- Shared guard --------
        private static bool IsStaff()
        {
            var ctx = System.Web.HttpContext.Current;
            return ctx?.Session != null
                && ctx.Session["permission"]?.ToString() == "True"
                && (ctx.Session["User"]?.ToString() == "Owner" || ctx.Session["User"]?.ToString() == "Admin");
        }

        private static short CurrentAdminId()
        {
            var ctx = System.Web.HttpContext.Current;
            return ctx?.Session?["UserID"] != null ? Convert.ToInt16(ctx.Session["UserID"]) : (short)1;
        }

        // -------- AJAX WebMethods --------

        [WebMethod]
        public static object ResolveToken(string token)
        {
            if (!IsStaff()) return new { valid = false, message = "ไม่มีสิทธิ์เข้าถึง" };

            var svc = new LoyaltyService(ConnStr);
            var info = svc.ResolveRedemptionToken(token);
            if (!info.Valid)
                return new { valid = false, message = info.Message };

            return new
            {
                valid = true,
                phone = info.CustomerPhone,
                name = info.CustomerName,
                tier = info.TierName,
                tierId = (int)info.CurrentTierId,
                points = info.AvailablePoints
            };
        }

        [WebMethod]
        public static object ResolveMember(string phone)
        {
            if (!IsStaff()) return new { valid = false, message = "ไม่มีสิทธิ์เข้าถึง" };

            try
            {
                var svc = new LoyaltyService(ConnStr);
                var info = svc.GetLoyaltyInfo(phone);
                if (info == null)
                    return new { valid = false, message = "ไม่พบสมาชิกจากเบอร์นี้" };

                string name = phone;
                var codeHelper = new code();
                DataTable dt = codeHelper.DatabaseQuerySafe(ConnStr,
                    "SELECT Name FROM Customer WHERE MobilePhone = @Phone",
                    new System.Collections.Generic.Dictionary<string, object> { { "@Phone", phone } });
                if (dt.Rows.Count > 0) name = dt.Rows[0]["Name"].ToString();

                return new
                {
                    valid = true,
                    phone = phone,
                    name = name,
                    tier = info.TierName,
                    tierId = (int)info.CurrentTierId,
                    points = info.AvailablePoints
                };
            }
            catch (Exception ex)
            {
                return new { valid = false, message = ex.Message };
            }
        }

        [WebMethod]
        public static object RedeemByToken(string token, int rewardId)
        {
            if (!IsStaff()) return new { success = false, message = "ไม่มีสิทธิ์เข้าถึง" };

            var svc = new LoyaltyService(ConnStr);
            var result = svc.RedeemRewardByToken(token, rewardId, CurrentAdminId());
            return new
            {
                success = result.Success,
                message = result.Message,
                code = result.RedemptionCode,
                rewardName = result.RewardName,
                newBalance = result.NewBalance
            };
        }

        [WebMethod]
        public static object RedeemByMember(string phone, int rewardId)
        {
            if (!IsStaff()) return new { success = false, message = "ไม่มีสิทธิ์เข้าถึง" };

            var svc = new LoyaltyService(ConnStr);
            var result = svc.RedeemReward(phone, rewardId, CurrentAdminId());
            return new
            {
                success = result.Success,
                message = result.Message,
                code = result.RedemptionCode,
                rewardName = result.RewardName,
                newBalance = result.NewBalance
            };
        }
    }
}
