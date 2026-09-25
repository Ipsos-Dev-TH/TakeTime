using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class MyPoints : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private LoyaltyService _loyaltyService;
        private code _code;
        private string _guestMobilePhone;

        // Cached for the rewards repeater eligibility checks
        private int _availablePoints = 0;
        private byte _currentTierId = 1;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Loyalty", "~/Guest/Dashboard")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            _guestPortalService = new GuestPortalService(_connectionString);
            _loyaltyService = new LoyaltyService(_connectionString);
            _code = new code();

            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
            }

            // Always refresh the member snapshot (needed for eligibility on postback too)
            LoadMemberSnapshot();

            if (!IsPostBack)
            {
                LoadRewards();
                LoadVouchers();
                LoadTransactions();
            }
        }

        private bool ValidateGuestSession()
        {
            string sessionToken = Request.Cookies["GuestSession"]?.Value ?? Session["GuestSessionToken"]?.ToString();
            if (string.IsNullOrEmpty(sessionToken))
                return false;

            DataTable dtSession = _guestPortalService.ValidateGuestSession(sessionToken);
            if (dtSession.Rows.Count == 0)
                return false;

            _guestMobilePhone = dtSession.Rows[0]["Customer_MobilePhone"].ToString();
            return true;
        }

        private void LoadMemberSnapshot()
        {
            try
            {
                var info = _loyaltyService.GetLoyaltyInfo(_guestMobilePhone);
                if (info != null)
                {
                    _availablePoints = info.AvailablePoints;
                    _currentTierId = info.CurrentTierId;

                    lblRedeemable.Text = info.AvailablePoints.ToString("N0");
                    lblYearly.Text = info.YearlyPoints.ToString("N0");
                    lblYear.Text = (info.PointsYear > 0 ? info.PointsYear : DateTime.Now.Year) + 543 + "";
                    lblTierName.Text = info.TierName ?? "Member";
                    lblProgressNow.Text = info.YearlyPoints.ToString("N0");

                    string tierEn = (info.TierNameEN ?? "").ToLower();
                    tierEmblem.Style["background"] = TierGradient(tierEn);

                    if (info.NextTierMinPoints.HasValue)
                    {
                        lblTierHint.Text = $"อีก {info.PointsToNextTier:N0} แต้มสะสมปีนี้ เพื่อเลื่อนเป็น {info.NextTierName}";
                        lblProgressNext.Text = $"เป้าหมาย {info.NextTierMinPoints.Value:N0} แต้ม ({info.NextTierName})";
                        tierBar.Style["width"] = info.TierProgressPercent + "%";
                    }
                    else
                    {
                        lblTierHint.Text = "คุณอยู่ในระดับสูงสุดแล้ว 🎉";
                        lblProgressNext.Text = "ระดับสูงสุด";
                        tierBar.Style["width"] = "100%";
                    }
                }
                else
                {
                    lblRedeemable.Text = "0";
                    lblYearly.Text = "0";
                    lblYear.Text = (DateTime.Now.Year + 543).ToString();
                    lblTierHint.Text = "เริ่มสะสมแต้มเพื่อเลื่อนระดับสมาชิก";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadMemberSnapshot error: " + ex.Message);
            }
        }

        private void LoadRewards()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID, RewardName, RewardNameEN, Description, Category, PointsCost,
                             MonetaryValue, StockQuantity, MinTierRequired, ImageUrl
                      FROM Loyalty_Rewards
                      WHERE IsActive = 1
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadRewards error: " + ex.Message);
                lblNoRewards.Visible = true;
            }
        }

        private void LoadVouchers()
        {
            try
            {
                DataTable dt = _loyaltyService.GetRedemptions(_guestMobilePhone);
                if (dt != null && dt.Rows.Count > 0)
                {
                    rptVouchers.DataSource = dt;
                    rptVouchers.DataBind();
                    lblNoVouchers.Visible = false;
                }
                else
                {
                    lblNoVouchers.Visible = true;
                }
            }
            catch
            {
                lblNoVouchers.Visible = true;
            }
        }

        private void LoadTransactions()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 30 TransactionType, Points, Description, TransactionDate
                      FROM Loyalty_Transactions
                      WHERE Customer_MobilePhone = @Phone
                      ORDER BY TransactionDate DESC",
                    new System.Collections.Generic.Dictionary<string, object> { { "@Phone", _guestMobilePhone } });

                if (dt.Rows.Count > 0)
                {
                    rptTransactions.DataSource = dt;
                    rptTransactions.DataBind();
                    lblNoTxn.Visible = false;
                }
                else
                {
                    lblNoTxn.Visible = true;
                }
            }
            catch
            {
                lblNoTxn.Visible = true;
            }
        }

        protected void btnUsePoints_Click(object sender, EventArgs e)
        {
            try
            {
                string token = _loyaltyService.GenerateRedemptionToken(_guestMobilePhone, 5);
                lblTokenText.Text = token;
                imgQr.ImageUrl = "https://api.qrserver.com/v1/create-qr-code/?size=260x260&margin=0&data=" +
                                 Server.UrlEncode(token);
                pnlQrModal.CssClass = "qr-modal show";
            }
            catch (Exception ex)
            {
                ShowAlert("ไม่สามารถสร้าง QR ได้: " + ex.Message, "danger");
            }
        }

        protected void btnCloseQr_Click(object sender, EventArgs e)
        {
            pnlQrModal.CssClass = "qr-modal";
        }

        protected void rptRewards_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName != "Redeem") return;

            try
            {
                int rewardId = Convert.ToInt32(e.CommandArgument);
                var result = _loyaltyService.RedeemReward(_guestMobilePhone, rewardId, null);

                if (result.Success)
                {
                    ShowAlert($"<i class='fas fa-check-circle'></i> แลกรางวัล <strong>{result.RewardName}</strong> สำเร็จ! " +
                              $"รหัสรับรางวัลของคุณคือ <strong>{result.RedemptionCode}</strong> — กรุณาแสดงรหัสนี้ที่เคาน์เตอร์", "success");
                }
                else
                {
                    ShowAlert($"<i class='fas fa-exclamation-triangle'></i> {result.Message}", "warning");
                }

                // Refresh everything after a redemption attempt
                LoadMemberSnapshot();
                LoadRewards();
                LoadVouchers();
                LoadTransactions();
            }
            catch (Exception ex)
            {
                ShowAlert("<i class='fas fa-times-circle'></i> เกิดข้อผิดพลาด: " + ex.Message, "danger");
            }
        }

        #region Repeater helpers (called from markup)

        protected bool CanRedeem(object pointsCost, object minTier, object stock)
        {
            int cost = Convert.ToInt32(pointsCost);
            byte tier = minTier != DBNull.Value ? Convert.ToByte(minTier) : (byte)1;
            bool inStock = stock == DBNull.Value || Convert.ToInt32(stock) > 0;
            return _availablePoints >= cost && _currentTierId >= tier && inStock;
        }

        protected string GetRedeemLabel(object pointsCost, object minTier, object stock)
        {
            int cost = Convert.ToInt32(pointsCost);
            byte tier = minTier != DBNull.Value ? Convert.ToByte(minTier) : (byte)1;
            bool inStock = stock == DBNull.Value || Convert.ToInt32(stock) > 0;

            if (!inStock) return "หมดแล้ว";
            if (_currentTierId < tier) return "ระดับยังไม่ถึง";
            if (_availablePoints < cost) return "แต้มไม่พอ";
            return "แลกเลย";
        }

        protected string GetRewardIcon(object category)
        {
            string c = (category?.ToString() ?? "").ToUpper();
            switch (c)
            {
                case "DISCOUNT": return "fas fa-tags";
                case "FREE_NIGHT": return "fas fa-bed";
                case "UPGRADE": return "fas fa-arrow-up";
                case "SERVICE": return "fas fa-spa";
                case "VOUCHER": return "fas fa-ticket-alt";
                case "GIFT": return "fas fa-gift";
                default: return "fas fa-award";
            }
        }

        protected string GetTierRequirement(object minTier)
        {
            byte tier = minTier != DBNull.Value ? Convert.ToByte(minTier) : (byte)1;
            if (tier <= 1) return "ทุกระดับสมาชิกแลกได้";
            return $"ต้องเป็นสมาชิกระดับ {TierNameById(tier)} ขึ้นไป";
        }

        private string TierNameById(byte id)
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TierName FROM Loyalty_Tiers WHERE ID = @Id",
                    new System.Collections.Generic.Dictionary<string, object> { { "@Id", id } });
                if (dt.Rows.Count > 0) return dt.Rows[0]["TierName"].ToString();
            }
            catch { }
            return "สูงขึ้น";
        }

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

        protected string GetTxnIcon(object type)
        {
            switch ((type?.ToString() ?? "").ToUpper())
            {
                case "EARN": case "BONUS": return "fas fa-plus";
                case "REDEEM": return "fas fa-gift";
                case "ADJUST": return "fas fa-sliders-h";
                case "EXPIRE": return "fas fa-hourglass-end";
                default: return "fas fa-circle";
            }
        }

        #endregion

        private string TierGradient(string tierEn)
        {
            if (tierEn.Contains("silver")) return "linear-gradient(135deg,#bdc3c7,#808080)";
            if (tierEn.Contains("gold")) return "linear-gradient(135deg,#f39c12,#daa520)";
            if (tierEn.Contains("platinum")) return "linear-gradient(135deg,#9b59b6,#6a1b9a)";
            if (tierEn.Contains("vip")) return "linear-gradient(135deg,#e74c3c,#c0392b)";
            return "linear-gradient(135deg,#667eea,#764ba2)";
        }

        private void ShowAlert(string message, string type)
        {
            pnlAlert.Visible = true;
            lblAlert.Text = message;
            alertBox.Attributes["class"] = "alert alert-" + type;
        }
    }
}
