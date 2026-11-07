using System;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra
{
    public partial class SiteMaster : MasterPage
    {
        /// <summary>เปิด poll/badge QR สั่งอาหารใน JS ของ master (= เมนู phNavDining มองเห็น)</summary>
        protected bool ShowDiningPoll;

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
                    if (pnlFinanceNav.Visible) ApplyFinanceMenuCounts();
                    try { phNavNextAccHub.Visible = pnlFinanceNav.Visible && Integration.ReceiptPreflight.CanOverrideCurrentUser(); } catch { }
                    pnlCrmNav.Visible = Perm.CanView(Perm.CrmCustomer) || Perm.CanView(Perm.CrmLoyalty)
                                        || Perm.CanView(Perm.CrmReview) || Perm.CanView(Perm.CrmAffiliate);
                    pnlHotelMgmt.Visible = Perm.CanView(Perm.MgtDashboard) || Perm.CanView(Perm.MgtReport)
                                           || Perm.CanView(Perm.MgtChannel);
                    pnlOwnerOnly.Visible = Perm.CanView(Perm.HrEmployee) || Perm.CanView(Perm.HrLeave)
                                           || Perm.CanView(Perm.HrPayroll) || Perm.CanView(Perm.HrAsset);
                    pnlSettingsNav.Visible = Perm.CanView(Perm.SysSettings);
                    phNavSettingsOwner.Visible = isOwner;

                    // PWA: ลิงก์ manifest + แผ่นชวนติดตั้งแอพ — เฉพาะผู้ดูแลที่ล็อกอิน
                    // (ลูกค้าไม่เห็นปุ่ม "ติดตั้ง Take Time Admin" ในเบราว์เซอร์)
                    phPwaHead.Visible = true;
                    phPwaInstall.Visible = true;
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
                phPwaHead.Visible = false;
                phPwaInstall.Visible = false;
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
        /// คีย์ผู้ใช้สำหรับจำค่า "ไม่ต้องแจ้งเตือนอีก" ของแผ่นชวนติดตั้งแอพ (localStorage แยกตามผู้ดูแลแต่ละคน)
        /// — ใช้ใน markup ผ่าน &lt;%: %&gt; (HTML-encode แล้ว)
        /// </summary>
        protected string PwaUserKey
        {
            get
            {
                try
                {
                    string u = Session["UserName"]?.ToString();
                    if (string.IsNullOrWhiteSpace(u)) u = Session["UserID"]?.ToString();
                    return string.IsNullOrWhiteSpace(u) ? "admin" : u.Trim().ToLowerInvariant();
                }
                catch { return "admin"; }
            }
        }

        /// <summary>
        /// แชทลูกค้าลอย — โชว์เฉพาะผู้เยี่ยมชม (ไม่ล็อกอิน) และเมื่อช่องทาง WEBCHAT เปิด + ฟีเจอร์ Chat เปิด
        /// พนักงานที่ล็อกอินไม่เห็น (ใช้กล่องแชทรวมแทน)
        /// </summary>
        private void ApplyPublicChatWidget()
        {
            try
            {
                // แสดงกับทุกคนรวมพนักงานที่ล็อกอินอยู่ — เดิมซ่อนเมื่อล็อกอิน ทำให้เจ้าของ/พนักงาน
                // เปิดหน้าแรกแล้วไม่เห็นปุ่มแชท (ต้องออกจากระบบก่อนถึงจะเห็น) ซึ่งสับสนและทดสอบยาก
                if (Feature.Off("Chat")) { phPublicChat.Visible = false; return; }

                string conn = System.Configuration.ConfigurationManager
                    .ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT TOP 1 IsEnabled FROM OmniChannel_Channels WHERE ChannelCode = 'WEBCHAT'", null);

                bool rowExists = dt != null && dt.Rows.Count > 0;
                phPublicChat.Visible = rowExists && Convert.ToBoolean(dt.Rows[0][0]);

                // ช่วยไล่ปัญหา "ทำไมไม่เห็นปุ่มแชท" — บอกเหตุผลใน log ให้ผู้ดูแล (วันละครั้งพอ)
                if (!phPublicChat.Visible) LogWidgetHidden(conn, rowExists
                    ? "ช่องทาง WEBCHAT ถูกปิดอยู่ — เปิดที่ Admin → Chat → ตั้งค่าช่องทาง → แชทหน้าเว็บ"
                    : "ยังไม่มีช่องทาง WEBCHAT ในระบบ — รัน migration PHASE18_Migration_22_WebChat_TikTok.sql");
            }
            catch (Exception ex)
            {
                phPublicChat.Visible = false;
                try
                {
                    string conn = System.Configuration.ConfigurationManager
                        .ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                    LogWidgetHidden(conn, "อ่านค่าช่องทางแชทไม่สำเร็จ: " + ex.Message);
                }
                catch { }
            }
        }

        // ── ตัวเลขงานค้างในเมนูการเงิน (แคชทั้งแอป 5 นาที — เมนูอยู่ทุกหน้า ห้าม query ทุกคำขอ) ──
        private static readonly object _finCountLock = new object();
        private static DateTime _finCountAt = DateTime.MinValue;
        private static int _finCppPending, _finPrtPending, _finHubWork;

        /// <summary>ใบซื้อเชื่อค้างจ่าย (Account_Payment.IsCredit ยังไม่จ่าย) + การคืนสินค้ารอใบลดหนี้ผู้ขาย
        /// (Purchase_Return_Cn_Link.Cn_Status = PENDING) — ตาราง/คอลัมน์ไม่มี = 0 · พังต้องไม่ทำให้เมนูพัง</summary>
        private void ApplyFinanceMenuCounts()
        {
            try
            {
                if ((DateTime.Now - _finCountAt).TotalMinutes >= 5)
                {
                    lock (_finCountLock)
                    {
                        if ((DateTime.Now - _finCountAt).TotalMinutes >= 5)
                        {
                            _finCountAt = DateTime.Now;   // ตั้งก่อน — DB ช้า/พังก็ไม่ถามซ้ำทุกคำขอ
                            string conn = System.Configuration.ConfigurationManager
                                .ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                            var db = new code();
                            _finCppPending = CountOrZero(db, conn,
                                @"IF COL_LENGTH('dbo.Account_Payment', 'Credit_Paid_Date') IS NOT NULL
                                      EXEC(N'SELECT COUNT(*) FROM dbo.Account_Payment WHERE IsCredit = 1 AND ISNULL(Status, N''Normal'') <> N''Cancel'' AND Credit_Paid_Date IS NULL')
                                  ELSE
                                      EXEC(N'SELECT COUNT(*) FROM dbo.Account_Payment WHERE IsCredit = 1 AND ISNULL(Status, N''Normal'') <> N''Cancel'' AND Nexaacc_Payment_Id IS NULL')");
                            // ศูนย์บัญชี NextAcc: แถวคิวที่ต้องมีคนทำ (DATA/CONFIG/POLICY/DUPLICATE)
                            _finHubWork = CountOrZero(db, conn,
                                @"IF COL_LENGTH('dbo.Accounting_Sync_Queue', 'Error_Class') IS NOT NULL
                                      EXEC(N'SELECT COUNT(*) FROM dbo.Accounting_Sync_Queue WHERE Status = N''FAILED'' AND Error_Class IN (N''DATA'', N''CONFIG'', N''POLICY'', N''DUPLICATE'')')
                                  ELSE SELECT 0");
                            _finPrtPending = CountOrZero(db, conn,
                                @"IF OBJECT_ID(N'dbo.Purchase_Return_Cn_Link', N'U') IS NOT NULL
                                      EXEC(N'SELECT COUNT(*) FROM dbo.Purchase_Return_Cn_Link WHERE Cn_Status = N''PENDING''')
                                  ELSE SELECT 0");
                        }
                    }
                }
                litNavCppBadge.Text = MenuCountBadge(_finCppPending, "ใบซื้อเชื่อค้างจ่าย");
                litNavPrtBadge.Text = MenuCountBadge(_finPrtPending, "การคืนสินค้าที่รอใบลดหนี้จากผู้ขาย");
                litNavHubBadge.Text = MenuCountBadge(_finHubWork, "งานบัญชีที่ต้องมีคนทำ (แก้ข้อมูล/ตั้งค่า/ใบลดหนี้/ตรวจซ้ำ)");
            }
            catch { }
        }

        private static int CountOrZero(code db, string conn, string sql)
        {
            try
            {
                var dt = db.DatabaseQuerySafe(conn, sql, null);
                return dt != null && dt.Rows.Count > 0 && dt.Columns.Count > 0 && dt.Rows[0][0] != DBNull.Value
                    ? Convert.ToInt32(dt.Rows[0][0]) : 0;
            }
            catch { return 0; }
        }

        private static string MenuCountBadge(int n, string title)
        {
            if (n <= 0) return "";
            return " <span title=\"" + HttpUtility.HtmlAttributeEncode(title) + "\" style=\"display:inline-block;min-width:18px;padding:0 6px;margin-left:4px;"
                 + "border-radius:9px;background:#e74c3c;color:#fff;font-size:11px;line-height:18px;text-align:center;\">" + (n > 99 ? "99+" : n.ToString()) + "</span>";
        }

        private static DateTime _lastChatWidgetLog = DateTime.MinValue;
        private static void LogWidgetHidden(string conn, string reason)
        {
            if ((DateTime.Now - _lastChatWidgetLog).TotalHours < 24) return;
            _lastChatWidgetLog = DateTime.Now;
            try { new code().Logs(conn, "PublicChatWidget", "ไม่แสดงปุ่มแชทหน้าเว็บ: " + reason, "SYSTEM"); }
            catch { }
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
                // QR สั่งอาหาร (PHASE19_42): เมนู + badge/poll ทั่วระบบ (NotificationCheck?action=dining) เฉพาะผู้มีสิทธิ์
                phNavDining.Visible = Feature.On(Take_Time_BangPhra.Dining.DiningConfig.FeatureName) && Perm.CanView(Perm.OpsDining);
                ShowDiningPoll = phNavDining.Visible;
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