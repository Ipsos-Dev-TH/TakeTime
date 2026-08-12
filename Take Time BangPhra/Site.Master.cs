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
                    // สิทธิ์ตามบทบาท:
                    //   Staff  = งานประจำวัน / บริการลูกค้า / ขายหน้าร้าน (3 คอลัมน์แรก)
                    //   Admin  = + การเงิน&บัญชี + ลูกค้า&การตลาด
                    //   Owner  = + รายงาน + บุคคล + ตั้งค่า
                    // เดิม Staff เห็นเมนูบัญชี/ใบสำคัญจ่าย/ข้อมูลลูกค้าทั้งหมดเท่ากับ Admin
                    string role = Session["User"]?.ToString() ?? "";
                    bool isOwner = role == "Owner";
                    bool isAdminOrOwner = isOwner || role == "Admin";

                    pnlFinanceNav.Visible = isAdminOrOwner;
                    pnlCrmNav.Visible = isAdminOrOwner;
                    pnlHotelMgmt.Visible = isOwner;
                    pnlOwnerOnly.Visible = isOwner;
                    // ตั้งค่า: Admin เห็น "ศูนย์ตั้งค่า" (หน้าจะกรองรายการ Owner-only ให้เอง)
                    // Owner เห็นทางลัดหน้าที่เป็น Owner-only เพิ่ม
                    pnlSettingsNav.Visible = isAdminOrOwner;
                    phNavSettingsOwner.Visible = isOwner;
                }
                else
                {
                    // Hide admin controls
                    pnlAdminNav.Visible = false;
                    pnlFinanceNav.Visible = false;
                    pnlSettingsNav.Visible = false;
                    pnlCrmNav.Visible = false;
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
                pnlFinanceNav.Visible = false;
                pnlSettingsNav.Visible = false;
                pnlCrmNav.Visible = false;
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
                phNavRoomService.Visible = Feature.On("RoomService");
                phNavLoyalty.Visible = Feature.On("Loyalty");
                phNavReviews.Visible = Feature.On("Reviews");
                phNavAIReport.Visible = Feature.On("AI");
                phNavChannelMgr.Visible = Feature.On("ChannelManager");
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