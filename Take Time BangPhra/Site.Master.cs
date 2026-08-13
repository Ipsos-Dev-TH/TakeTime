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
                    // สิทธิ์มาจาก "กลุ่มสิทธิ์" ของผู้ใช้ (Perm) — ถ้ายังไม่ถูกกำหนดกลุ่ม
                    // Perm จะย้อนไปใช้ค่าเริ่มต้นตาม Role เดิมให้เอง (Owner/Admin/Staff)
                    string role = Session["User"]?.ToString() ?? "";
                    bool isOwner = role == "Owner";

                    // การเงิน / ลูกค้า — เห็นเมื่อมีสิทธิ์ดูอย่างน้อยหนึ่งส่วนในคอลัมน์นั้น
                    pnlFinanceNav.Visible = Perm.CanView(Perm.FinReceipt) || Perm.CanView(Perm.FinVoucher)
                                            || Perm.CanView(Perm.FinReport);
                    pnlCrmNav.Visible = Perm.CanView(Perm.CrmCustomer) || Perm.CanView(Perm.CrmLoyalty)
                                        || Perm.CanView(Perm.CrmReview) || Perm.CanView(Perm.CrmAffiliate);
                    pnlHotelMgmt.Visible = Perm.CanView(Perm.MgtDashboard) || Perm.CanView(Perm.MgtReport)
                                           || Perm.CanView(Perm.MgtChannel);
                    pnlOwnerOnly.Visible = Perm.CanView(Perm.HrEmployee) || Perm.CanView(Perm.HrLeave)
                                           || Perm.CanView(Perm.HrPayroll) || Perm.CanView(Perm.HrAsset);
                    pnlSettingsNav.Visible = Perm.CanView(Perm.SysSettings);
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
            ApplyPublicChatWidget();
        }

        /// <summary>
        /// แชทลูกค้าลอย — โชว์เฉพาะผู้เยี่ยมชม (ไม่ล็อกอิน) และเมื่อช่องทาง WEBCHAT เปิด + ฟีเจอร์ Chat เปิด
        /// พนักงานที่ล็อกอินไม่เห็น (ใช้กล่องแชทรวมแทน)
        /// </summary>
        private void ApplyPublicChatWidget()
        {
            try
            {
                if (Session["permission"]?.ToString() == "True") { phPublicChat.Visible = false; return; }
                if (Feature.Off("Chat")) { phPublicChat.Visible = false; return; }

                string conn = System.Configuration.ConfigurationManager
                    .ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT TOP 1 IsEnabled FROM OmniChannel_Channels WHERE ChannelCode = 'WEBCHAT'", null);
                phPublicChat.Visible = dt != null && dt.Rows.Count > 0 && Convert.ToBoolean(dt.Rows[0][0]);
            }
            catch { phPublicChat.Visible = false; }  // ตาราง/คอลัมน์ยังไม่มี → ไม่โชว์
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
                phNavActivityMgmt.Visible = Feature.On("Activities") && Perm.CanView(Perm.OpsActivity);
                phNavAffiliatePub.Visible = Feature.On("Affiliate");
                phNavAffiliateAdmin.Visible = Feature.On("Affiliate") && Perm.CanView(Perm.CrmAffiliate);
                phNavGuestPortal.Visible = Feature.On("GuestPortal");
                phNavMemberPub.Visible = Feature.On("Loyalty");
                // เมนูจะขึ้นก็ต่อเมื่อ "ฟีเจอร์เปิด" และ "กลุ่มสิทธิ์ให้เห็น" พร้อมกัน
                phNavHousekeeping.Visible = Feature.On("Housekeeping") && Perm.CanView(Perm.OpsHousekeeping);
                phNavMaintenance.Visible = Feature.On("Maintenance") && Perm.CanView(Perm.OpsMaintenance);
                phNavChat.Visible = Feature.On("Chat") && Perm.CanView(Perm.OpsChat);
                phNavRoomService.Visible = Feature.On("RoomService") && Perm.CanView(Perm.OpsRoomService);
                phNavLoyalty.Visible = Feature.On("Loyalty") && Perm.CanView(Perm.CrmLoyalty);
                phNavReviews.Visible = Feature.On("Reviews") && Perm.CanView(Perm.CrmReview);
                phNavAIReport.Visible = Feature.On("AI") && Perm.CanView(Perm.MgtReport);
                phNavChannelMgr.Visible = Feature.On("ChannelManager") && Perm.CanView(Perm.MgtChannel);
                phNavHR.Visible = Feature.On("HR") && Perm.CanView(Perm.HrEmployee);
                phNavAssets.Visible = Feature.On("Assets") && Perm.CanView(Perm.HrAsset);
                phNavWebAnalytics.Visible = Feature.On("WebAnalytics") && Perm.CanView(Perm.MgtReport);

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