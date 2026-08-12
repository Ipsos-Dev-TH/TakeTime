using System;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra
{
    public partial class SiteMaster : MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                if (Session["permission"]?.ToString() == "True")
                {
                    // Show admin panel and logout button
                    pnlAdminNav.Visible = true;
                    btnLogout.Visible = true;
                    hlLogin.Visible = false;

                    // Show employee menu for all logged-in users
                    pnlEmployeeNav.Visible = true;

                    // Show chat notification system
                    pnlChatNotification.Visible = true;

                    // Check if user is Owner to show owner-only menus
                    // Owner sees ALL menus including Hotel Management and System/HR
                    // Owner can see ALL employees without supervisor assignment
                    bool isOwner = Session["User"]?.ToString() == "Owner";
                    pnlHotelMgmt.Visible = isOwner;
                    pnlOwnerOnly.Visible = isOwner;
                }
                else
                {
                    // Hide admin controls
                    pnlAdminNav.Visible = false;
                    pnlHotelMgmt.Visible = false;
                    pnlOwnerOnly.Visible = false;
                    pnlEmployeeNav.Visible = false;
                    pnlChatNotification.Visible = false;
                    btnLogout.Visible = false;
                    hlLogin.Visible = true;
                }
            }
            catch
            {
                // Hide admin controls on error
                pnlAdminNav.Visible = false;
                pnlHotelMgmt.Visible = false;
                pnlOwnerOnly.Visible = false;
                pnlEmployeeNav.Visible = false;
                pnlChatNotification.Visible = false;
                btnLogout.Visible = false;
                hlLogin.Visible = true;
            }

            // ต้องเรียกหลังบล็อกสิทธิ์ด้านบน — ไม่งั้นค่า Visible ของแชท/เมนูถูกบล็อกบนเขียนทับ
            ApplyFeatureToggles();
        }

        /// <summary>
        /// ซ่อนเมนูของโมดูลที่ถูกปิดใน ศูนย์รวมการตั้งค่าระบบ → หมวด "ฟีเจอร์"
        /// (Feature flags — ปิด = เมนูหาย, หน้าโมดูลมี Feature.Guard กันเข้าตรงอีกชั้น)
        /// </summary>
        private void ApplyFeatureToggles()
        {
            try
            {
                phNavActivitiesPub.Visible = Feature.On("Activities");
                phNavActivityMgmt.Visible = Feature.On("Activities");
                phNavAffiliatePub.Visible = Feature.On("Affiliate");
                phNavAffiliateAdmin.Visible = Feature.On("Affiliate");
                phNavGuestPortal.Visible = Feature.On("GuestPortal");
                phNavHousekeeping.Visible = Feature.On("Housekeeping");
                phNavMaintenance.Visible = Feature.On("Maintenance");
                phNavChat.Visible = Feature.On("Chat");
                phNavChatSettings.Visible = Feature.On("Chat");
                phNavRoomService.Visible = Feature.On("RoomService");
                phNavLoyalty.Visible = Feature.On("Loyalty");
                phNavTier.Visible = Feature.On("Loyalty");
                phNavReviews.Visible = Feature.On("Reviews");
                phNavChannelMgr.Visible = Feature.On("ChannelManager");
                phNavDynPricing.Visible = Feature.On("DynamicPricing");
                phNavAI.Visible = Feature.On("AI");
                phNavAIReport.Visible = Feature.On("AI");
                phNavHR.Visible = Feature.On("HR");
                phNavAssets.Visible = Feature.On("Assets");
                phNavWebAnalytics.Visible = Feature.On("WebAnalytics");

                // แชทปิดทั้งโมดูล → กระดิ่งแจ้งเตือนแชทก็ไม่ต้องโชว์
                if (Feature.Off("Chat")) pnlChatNotification.Visible = false;
            }
            catch { /* ตาราง System_Config ยังไม่มี → โชว์ทุกเมนูตาม default เดิม */ }
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            PerformSecureLogout();
        }

        private void PerformSecureLogout()
        {
            try
            {
                // Clear session
                Session.Clear();
                Session.Abandon();

                // Clear authentication cookie
                if (Request.Cookies["ASP.NET_SessionId"] != null)
                {
                    Response.Cookies["ASP.NET_SessionId"].Value = string.Empty;
                    Response.Cookies["ASP.NET_SessionId"].Expires = DateTime.Now.AddMonths(-20);
                }

                // Redirect to home page
                Response.Redirect("~/Default", true);
            }
            catch (Exception ex)
            {
                // Log error and redirect anyway
                System.Diagnostics.Debug.WriteLine($"Logout error: {ex.Message}");
                Response.Redirect("~/Default", true);
            }
        }
    }
}