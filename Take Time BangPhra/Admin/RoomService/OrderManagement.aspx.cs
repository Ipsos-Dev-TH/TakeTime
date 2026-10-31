using System;
using System.Data;
using System.Web.UI;
using System.Web.Services;
using System.Collections.Generic;

namespace Take_Time_BangPhra.Admin.RoomService
{
    public partial class OrderManagement : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.OpsRoomService)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (!Feature.Guard(this, "RoomService", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            _code = new code();

            // Check admin login
            // ตรวจสิทธิ์แบบเดียวกับหน้าผู้ดูแลอื่น ๆ (เดิมเช็คแค่ว่ามีชื่อผู้ใช้ใน session
            // ไม่ได้เช็คสิทธิ์จริง และใช้คีย์คนละตัวกับทั้งระบบ)
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            if (!IsPostBack)
            {
                LoadOrderList();
                UpdatePendingBadge();
            }
            pnlRsBackfill.Visible = CanBackfillPaid();
            if (pnlRsBackfill.Visible && !IsPostBack)
            {
                txtBackfillFrom.Text = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");
                txtBackfillTo.Text = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
            }
        }

        /// <summary>ปุ่มรับเงินย้อนหลัง: Owner/Admin + เปิดนโยบายเอกสารขาย (ปิดนโยบาย = นับทุกออเดอร์อยู่แล้ว ไม่ต้องใช้)</summary>
        private bool CanBackfillPaid()
        {
            string role = Session["User"]?.ToString();
            if (role != "Owner" && role != "Admin") return false;
            return Take_Time_BangPhra.Integration.ChannelPosting.PolicyEnabled(_connectionString);
        }

        /// <summary>
        /// ทำเครื่องหมายรับเงินย้อนหลังแบบกลุ่ม (ครั้งเดียวหลังเปิดนโยบายเอกสารขาย): ออเดอร์เงินสด/โอนที่ส่งของแล้ว
        /// ยังไม่ PAID และยังไม่ลงบัญชี (Acct_Post_Ref IS NULL) ในช่วงวันสั่ง → PAID + Paid_Date = วันสั่ง (เดิมว่าง)
        /// RevenuePostingService จะออกใบตามนโยบายรอบถัดไป (วันที่สรุปไปแล้ว = ใบเสริม -S{n}). Owner/Admin เท่านั้น
        /// </summary>
        protected void btnBackfillPaid_Click(object sender, EventArgs e)
        {
            if (!CanBackfillPaid()) return;
            DateTime from, to;
            if (!DateTime.TryParse(txtBackfillFrom.Text, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out from)
                || !DateTime.TryParse(txtBackfillTo.Text, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out to))
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "bfErr", "alert('กรุณาเลือกช่วงวันที่ให้ถูกต้อง');", true);
                return;
            }
            if (to < from) { var t = from; from = to; to = t; }
            try
            {
                bool hasPaidDate = Take_Time_BangPhra.Integration.ChannelPosting.HasColumn(_connectionString, "Guest_Room_Service_Orders", "Paid_Date");
                int n = _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Guest_Room_Service_Orders SET Payment_Status = 'PAID'" +
                    (hasPaidDate ? ", Paid_Date = COALESCE(Paid_Date, Order_Date)" : "") +
                    " WHERE Payment_Method IN ('CASH','TRANSFER')" +
                    " AND ISNULL(Payment_Status,'') NOT IN ('PAID','CHARGED')" +
                    " AND Order_Status NOT IN ('CANCELLED','PENDING')" +
                    " AND Acct_Post_Ref IS NULL" +
                    " AND Order_Date >= @f AND Order_Date < @t",
                    new Dictionary<string, object> { { "@f", from.Date }, { "@t", to.Date.AddDays(1) } });
                try
                {
                    _code.Logs(_connectionString, "RoomService",
                        $"ทำเครื่องหมายรับเงินย้อนหลัง {from:yyyy-MM-dd}..{to:yyyy-MM-dd}: {n} ออเดอร์ (เงินสด/โอน)",
                        Session["username"]?.ToString() ?? "Unknown");
                }
                catch { }
                ScriptManager.RegisterStartupScript(this, GetType(), "bfDone",
                    "alert('ตั้งรับเงินแล้ว " + n + " ออเดอร์ — รายได้จะเข้าบัญชีรอบถัดไปตามนโยบายเอกสารขาย');", true);
            }
            catch (Exception ex)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "bfEx",
                    "alert('เกิดข้อผิดพลาด: " + ex.Message.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", " ") + "');", true);
            }
        }

        /// <summary>
        /// Load list of all orders
        /// </summary>
        private void LoadOrderList()
        {
            try
            {
                DataTable dtOrders = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        o.ID,
                        o.Order_Number,
                        o.Reservation_ID,
                        o.Order_Date,
                        o.Total_Amount,
                        o.Payment_Method,
                        o.Payment_Status,
                        o.Order_Status,
                        ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'ไม่ระบุห้อง') AS RoomName,
                        c.Name AS GuestName
                      FROM Guest_Room_Service_Orders o
                      INNER JOIN Reservation r ON o.Reservation_ID = r.ID
                      INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                      WHERE o.Order_Date >= DATEADD(DAY, -7, GETDATE())
                      ORDER BY
                        CASE o.Order_Status
                            WHEN 'PENDING' THEN 1
                            WHEN 'CONFIRMED' THEN 2
                            WHEN 'PREPARING' THEN 3
                            ELSE 4
                        END,
                        o.Order_Date DESC",
                    null);

                if (dtOrders.Rows.Count > 0)
                {
                    rptOrderList.DataSource = dtOrders;
                    rptOrderList.DataBind();
                    pnlNoOrders.Visible = false;
                }
                else
                {
                    pnlNoOrders.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadOrderList error: " + ex.Message);
                pnlNoOrders.Visible = true;
            }
        }

        /// <summary>
        /// Update pending order badge
        /// </summary>
        private void UpdatePendingBadge()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT COUNT(*) AS PendingCount FROM Guest_Room_Service_Orders WHERE Order_Status = 'PENDING'",
                    null);

                int pendingCount = dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["PendingCount"]) : 0;

                if (pendingCount > 0)
                {
                    pnlPendingBadge.Visible = true;
                    lblPendingCount.Text = pendingCount.ToString();
                }
                else
                {
                    pnlPendingBadge.Visible = false;
                }
            }
            catch { }
        }

        /// <summary>
        /// Get status display text
        /// </summary>
        protected string GetStatusText(string status)
        {
            switch (status)
            {
                case "PENDING": return "รอรับออเดอร์";
                case "CONFIRMED": return "กำลังจัดเตรียม";
                case "DELIVERED": return "จัดส่งแล้ว";
                case "CANCELLED": return "ยกเลิก";
                default: return status;
            }
        }

        /// <summary>
        /// Load selected order
        /// </summary>
        protected void btnLoadOrder_Click(object sender, EventArgs e)
        {
            string selectedOrderId = hfSelectedOrder.Value;

            if (string.IsNullOrEmpty(selectedOrderId))
            {
                return;
            }

            LoadOrderDetail(Convert.ToInt64(selectedOrderId));
        }

        /// <summary>
        /// Load order detail
        /// </summary>
        /// <summary>
        /// ลิงก์ให้ลูกค้าจ่ายออเดอร์นี้เอง — โผล่เฉพาะออเดอร์ที่ยังค้างจ่ายจริง ๆ
        ///
        /// ฝั่งรับเงิน (Pay.aspx รองรับ src=ROOMSERVICE + ApplyToRoomServiceOrder)
        /// มีมาตั้งแต่ต้นแล้ว แต่ไม่เคยมีหน้าไหนสร้างลิงก์ให้ ⇒ ช่องทางนี้ตายมาตลอด
        ///
        /// เงียบสนิทถ้าปิดฟีเจอร์/ปิดช่องทางรูมเซอร์วิส — หน้าเดิมทำงานเหมือนเดิม
        /// </summary>
        private void ShowOnlinePayLink(long orderId, decimal amount,
            string paymentStatus, string orderStatus, string phone)
        {
            try
            {
                if (pnlRsPay == null) return;
                pnlRsPay.Visible = false;

                string ps = (paymentStatus ?? "").ToUpperInvariant();
                if (ps == "PAID" || ps == "CHARGED") return;      // จ่ายแล้ว/คิดเข้าห้องแล้ว
                if ((orderStatus ?? "").ToUpperInvariant() == "CANCELLED") return;
                if (amount <= 0) return;

                var svc = new Take_Time_BangPhra.Payments.OnlinePaymentService(_connectionString);
                if (svc.AvailableMethods(amount,
                        Take_Time_BangPhra.Payments.PaymentSource.RoomService).Count == 0) return;

                txtRsPayLink.Text = Take_Time_BangPhra.Payments.PaymentUrls.SiteBase()
                    + "/Payment/Pay?src=" + Take_Time_BangPhra.Payments.PaymentSource.RoomService
                    + "&id=" + orderId
                    + "&ph=" + Uri.EscapeDataString(phone ?? "");
                pnlRsPay.Visible = true;
            }
            catch
            {
                // ยังไม่ได้ติดตั้ง/ตั้งค่า → ไม่แสดงอะไร หน้าเดิมทำงานปกติ
                try { pnlRsPay.Visible = false; } catch { }
            }
        }

        private void LoadOrderDetail(long orderId)
        {
            try
            {
                // Get order info
                DataTable dtOrder = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        o.*,
                        ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'ไม่ระบุห้อง') AS RoomName,
                        c.Name AS GuestName,
                        c.MobilePhone AS Phone
                      FROM Guest_Room_Service_Orders o
                      INNER JOIN Reservation r ON o.Reservation_ID = r.ID
                      INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                      WHERE o.ID = @OrderId",
                    new Dictionary<string, object> { { "@OrderId", orderId } });

                if (dtOrder.Rows.Count > 0)
                {
                    DataRow order = dtOrder.Rows[0];

                    lblOrderNumber.Text = order["Order_Number"].ToString();
                    lblRoomName.Text = order["RoomName"].ToString();
                    lblGuestName.Text = order["GuestName"].ToString();
                    lblPhone.Text = order["Phone"].ToString();
                    lblOrderTime.Text = Convert.ToDateTime(order["Order_Date"]).ToString("dd/MM/yyyy HH:mm");
                    lblStatus.Text = GetStatusText(order["Order_Status"].ToString());
                    lblPayment.Text = GetPaymentText(order["Payment_Method"].ToString(), order["Payment_Status"].ToString());
                    lblDeliveryNote.Text = string.IsNullOrEmpty(order["Delivery_Instructions"].ToString()) ? "-" : order["Delivery_Instructions"].ToString();
                    // ยอดรวม + แยกให้เห็นว่ามีค่าบริการรวมอยู่เท่าไหร่ (คอลัมน์จาก PHASE18_21)
                    decimal totalAmt = Convert.ToDecimal(order["Total_Amount"]);
                    decimal svcAmt = 0m;
                    if (order.Table.Columns.Contains("Service_Charge") && order["Service_Charge"] != DBNull.Value)
                        svcAmt = Convert.ToDecimal(order["Service_Charge"]);
                    lblTotalAmount.Text = svcAmt > 0m
                        ? $"{totalAmt:N0} <small style='color:#7a8794; font-weight:400;'>(ค่าสินค้า {(totalAmt - svcAmt):N0} + ค่าบริการ {svcAmt:N0})</small>"
                        : totalAmt.ToString("N0");

                    // Update button visibility based on status
                    string status = order["Order_Status"].ToString();
                    btnClaim.Visible = status == "PENDING";
                    btnDelivered.Visible = status == "CONFIRMED";
                    btnCancel.Visible = status != "DELIVERED" && status != "CANCELLED";
                    // เงินสด/โอน: รายได้เข้าบัญชีเมื่อพนักงานยืนยันว่ารับเงินแล้วจริง (เก็บเงินสด/ตรวจสลิป) เท่านั้น
                    string payMethod = Convert.ToString(order["Payment_Method"]);
                    string payStatus = Convert.ToString(order["Payment_Status"]);
                    btnMarkPaid.Visible = (payMethod == "CASH" || payMethod == "TRANSFER")
                        && payStatus != "PAID" && payStatus != "CHARGED" && status != "CANCELLED";

                    ShowOnlinePayLink(orderId, totalAmt,
                        Convert.ToString(order["Payment_Status"]), status,
                        Convert.ToString(order["Phone"]));
                }

                // Get order items
                DataTable dtItems = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM Guest_Room_Service_Items WHERE Order_ID = @OrderId",
                    new Dictionary<string, object> { { "@OrderId", orderId } });

                rptOrderItems.DataSource = dtItems;
                rptOrderItems.DataBind();

                pnlOrderDetail.Visible = true;
                pnlSelectOrder.Visible = false;

                // Reload list to update counts
                LoadOrderList();
                UpdatePendingBadge();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadOrderDetail error: " + ex.Message);
            }
        }

        /// <summary>
        /// Get payment display text
        /// </summary>
        private string GetPaymentText(string method, string status)
        {
            string methodText;
            switch (method)
            {
                case "CHARGE_TO_ROOM": methodText = "ชาร์จเข้าห้อง"; break;
                case "TRANSFER": methodText = "โอนเงิน"; break;
                case "CASH": methodText = "เงินสด"; break;
                default: methodText = method; break;
            }

            string statusText;
            switch (status)
            {
                case "PENDING": statusText = "รอชำระ"; break;
                case "PAID": statusText = "ชำระแล้ว"; break;
                case "CHARGED": statusText = "ชาร์จแล้ว"; break;
                default: statusText = status; break;
            }

            return methodText + " (" + statusText + ")";
        }

        /// <summary>
        /// Claim order (confirm)
        /// </summary>
        protected void btnClaim_Click(object sender, EventArgs e)
        {
            UpdateOrderStatus("CONFIRMED");
        }

        /// <summary>
        /// Mark as delivered
        /// </summary>
        protected void btnDelivered_Click(object sender, EventArgs e)
        {
            UpdateOrderStatus("DELIVERED");
        }

        /// <summary>
        /// ยืนยันรับเงิน (เงินสดเก็บแล้ว / ตรวจสลิปโอนแล้ว) → Payment_Status = PAID + Paid_Date
        /// ใบสรุปรายได้รูมเซอร์วิส (RevenuePostingService) นับเฉพาะออเดอร์ที่ PAID — ยังไม่ยืนยัน = ยังไม่ลงบัญชี
        /// </summary>
        protected void btnMarkPaid_Click(object sender, EventArgs e)
        {
            string orderId = hfSelectedOrder.Value;
            if (string.IsNullOrEmpty(orderId)) return;
            try
            {
                // 🔎 ตรวจสลิปโอนก่อนบันทึกรับเงิน (PHASE19_34 — ปิดสวิตช์ = แบบเดิม)
                string slipBlock = CheckOrderSlip(Convert.ToInt64(orderId));
                if (slipBlock != null)
                {
                    LoadOrderDetail(Convert.ToInt64(orderId));
                    ScriptManager.RegisterStartupScript(this, GetType(), "slipBlock",
                        "alert('" + System.Web.HttpUtility.JavaScriptStringEncode(slipBlock) + "');", true);
                    return;
                }

                bool hasPaidDate =Take_Time_BangPhra.Integration.ChannelPosting.HasColumn(_connectionString, "Guest_Room_Service_Orders", "Paid_Date");
                int n = _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Guest_Room_Service_Orders SET Payment_Status = 'PAID'" + (hasPaidDate ? ", Paid_Date = GETDATE()" : "") +
                    " WHERE ID = @OrderId AND Payment_Method IN ('CASH','TRANSFER')" +
                    " AND ISNULL(Payment_Status,'') NOT IN ('PAID','CHARGED') AND Order_Status <> 'CANCELLED'",
                    new Dictionary<string, object> { { "@OrderId", Convert.ToInt64(orderId) } });
                try
                {
                    _code.Logs(_connectionString, "RoomService",
                        $"ยืนยันรับเงินออเดอร์ #{orderId} ({(n > 0 ? "สำเร็จ" : "ไม่มีการเปลี่ยนแปลง")})",
                        Session["username"]?.ToString() ?? "Unknown");
                }
                catch { }
                LoadOrderDetail(Convert.ToInt64(orderId));
                ScriptManager.RegisterStartupScript(this, GetType(), "paidUpdate",
                    n > 0 ? "alert('บันทึกรับเงินแล้ว — รายได้จะเข้าบัญชีตามนโยบายเอกสารขาย');"
                          : "alert('ออเดอร์นี้ชำระแล้ว/ไม่ใช่เงินสดหรือโอน');", true);
            }
            catch (Exception ex)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "error",
                    "alert('เกิดข้อผิดพลาด: " + ex.Message.Replace("'", "\\'") + "');", true);
            }
        }

        /// <summary>
        /// ออเดอร์โอนที่มีสลิป → ตรวจยอดกับสลิป ก่อน PAID — คืนข้อความหยุด หรือ null = ไปต่อ
        /// ไม่ผ่าน (ยอดไม่ตรง/ผู้รับผิด): Owner/Admin กด "ยืนยันรับเงิน" ซ้ำภายใน 10 นาที = ยืนยัน · สลิปซ้ำ = หยุดเสมอ
        /// ตรวจไม่ได้ = ไปต่อ (นโยบาย: มีปัญหาให้ข้ามการตรวจ)
        /// </summary>
        private string CheckOrderSlip(long orderId)
        {
            try
            {
                if (!Take_Time_BangPhra.Payments.SlipCheck.SlipVerificationService.IsEnabled) return null;
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Payment_Method, Payment_Slip_Path, Total_Amount FROM Guest_Room_Service_Orders WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", orderId } });
                if (dt == null || dt.Rows.Count == 0) return null;
                DataRow o = dt.Rows[0];
                string path = o["Payment_Slip_Path"] == DBNull.Value ? null : o["Payment_Slip_Path"].ToString();
                if (Convert.ToString(o["Payment_Method"]) != "TRANSFER" || string.IsNullOrEmpty(path)) return null;
                decimal amount = o["Total_Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(o["Total_Amount"]);

                var ctx = new Take_Time_BangPhra.Payments.SlipCheck.SlipVerifyContext
                {
                    SourceType = "ROOM_SERVICE", SourceId = orderId,
                    User = Take_Time_BangPhra.Payments.SlipCheck.SlipVerificationService.CurrentUserName()
                };
                // ยอดตายตัว (ยอดออเดอร์) + ยอดล็อกตามสลิป (PHASE19_36): โอนเกิน = ผ่าน (รับเกิน) · ขาด = ชำระไม่ครบ · อ่านไม่ได้ = แบบเดิม
                var r = Take_Time_BangPhra.Payments.SlipCheck.SlipVerificationService.VerifyFileFixedTotal(path, amount > 0m ? (decimal?)amount : null, ctx);
                if (r.IsBlocking)
                {
                    bool canOv = Take_Time_BangPhra.Payments.SlipCheck.SlipVerificationService.CanOverrideCurrentUser();
                    bool confirmed = r.CanOverride && canOv
                        && Take_Time_BangPhra.Payments.SlipCheck.SlipVerificationService.ConsumeSecondClick("RS-" + orderId);
                    if (!confirmed)
                    {
                        Take_Time_BangPhra.Payments.SlipCheck.SlipVerificationService.NotifyIfSerious(r, null, null, null, ctx.User);
                        return "ตรวจสลิปไม่ผ่าน: " + r.Message
                            + (r.CanOverride && canOv ? "\nตรวจกับบัญชีธนาคารแล้วกด \"ยืนยันรับเงินแล้ว\" อีกครั้งภายใน 10 นาทีเพื่อยืนยัน" : "");
                    }
                    Take_Time_BangPhra.Payments.SlipCheck.SlipVerificationService.NotifyIfSerious(r, null, null,
                        "ยืนยันซ้ำที่หน้าจัดการรูมเซอร์วิส (ออเดอร์ #" + orderId + ")", ctx.User);
                }
                Take_Time_BangPhra.Payments.SlipCheck.SlipVerificationService.TryLog(ctx, r, true);   // กันใช้สลิปเดิมซ้ำ
                _code.Logs(_connectionString, "RoomService", "ตรวจสลิปออเดอร์ #" + orderId + ": " + r.ShortText()
                    + (r.AmountLocked ? " · " + r.AmountLock.BadgeText() : ""),
                    Session["username"]?.ToString() ?? "Unknown");
            }
            catch (Exception ex)
            {
                try { _code.Logs(_connectionString, "RoomService", "ตรวจสลิปออเดอร์ #" + orderId + " ไม่ได้ (ข้าม): " + ex.Message, "SYSTEM"); } catch { }
            }
            return null;
        }

        /// <summary>
        /// Cancel order
        /// </summary>
        protected void btnCancel_Click(object sender, EventArgs e)
        {
            UpdateOrderStatus("CANCELLED");
        }

        /// <summary>
        /// Update order status
        /// </summary>
        private void UpdateOrderStatus(string newStatus)
        {
            string orderId = hfSelectedOrder.Value;
            if (string.IsNullOrEmpty(orderId)) return;

            try
            {
                string staffName = Session["username"] != null ? Session["username"].ToString() : "Unknown";
                short staffId = Session["admin_id"] != null ? Convert.ToInt16(Session["admin_id"]) : (short)0;

                string updateQuery = "UPDATE Guest_Room_Service_Orders SET Order_Status = @Status";

                var parameters = new Dictionary<string, object>
                {
                    { "@OrderId", Convert.ToInt64(orderId) },
                    { "@Status", newStatus }
                };

                if (newStatus == "CONFIRMED")
                {
                    updateQuery += ", Confirmed_By = @StaffId, Confirmed_Date = GETDATE()";
                    parameters.Add("@StaffId", staffId);
                }
                else if (newStatus == "DELIVERED")
                {
                    updateQuery += ", Delivered_By = @StaffId, Delivered_Date = GETDATE()";
                    parameters.Add("@StaffId", staffId);
                }

                updateQuery += " WHERE ID = @OrderId";

                _code.DatabaseInsertSafe(_connectionString, updateQuery, parameters);

                // Reload order detail
                LoadOrderDetail(Convert.ToInt64(orderId));

                // Show success message
                string statusText = GetStatusText(newStatus);
                ScriptManager.RegisterStartupScript(this, GetType(), "statusUpdate",
                    "alert('อัพเดทสถานะเป็น \"" + statusText + "\" เรียบร้อย');", true);
            }
            catch (Exception ex)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "error",
                    "alert('เกิดข้อผิดพลาด: " + ex.Message.Replace("'", "\\'") + "');", true);
            }
        }

        /// <summary>
        /// Web method to get pending orders for notification
        /// </summary>
        [WebMethod]
        public static object GetPendingOrders()
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                var code = new code();

                DataTable dtOrders = null;

                // Try with fn_GetReservationRoomNames first
                try
                {
                    dtOrders = code.DatabaseQuerySafe(connectionString,
                        @"SELECT
                            o.ID AS OrderId,
                            o.Order_Number,
                            o.Total_Amount,
                            ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'ไม่ระบุห้อง') AS RoomName,
                            c.Name AS GuestName,
                            (SELECT TOP 3 Product_Name + ' x' + CAST(Quantity AS NVARCHAR)
                             FROM Guest_Room_Service_Items i
                             WHERE i.Order_ID = o.ID
                             FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)') AS ItemsPreview
                          FROM Guest_Room_Service_Orders o
                          INNER JOIN Reservation r ON o.Reservation_ID = r.ID
                          INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                          WHERE o.Order_Status = 'PENDING'
                          ORDER BY o.Order_Date DESC",
                        null);
                }
                catch
                {
                    // Fallback without fn_GetReservationRoomNames
                    dtOrders = code.DatabaseQuerySafe(connectionString,
                        @"SELECT
                            o.ID AS OrderId,
                            o.Order_Number,
                            o.Total_Amount,
                            N'ห้องพัก' AS RoomName,
                            c.Name AS GuestName,
                            N'' AS ItemsPreview
                          FROM Guest_Room_Service_Orders o
                          INNER JOIN Reservation r ON o.Reservation_ID = r.ID
                          INNER JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                          WHERE o.Order_Status = 'PENDING'
                          ORDER BY o.Order_Date DESC",
                        null);
                }

                var orders = new List<object>();
                foreach (DataRow row in dtOrders.Rows)
                {
                    string itemsPreview = row["ItemsPreview"] != null ? row["ItemsPreview"].ToString() : "";
                    if (itemsPreview.Length > 50) itemsPreview = itemsPreview.Substring(0, 50) + "...";

                    orders.Add(new
                    {
                        orderId = Convert.ToInt64(row["OrderId"]),
                        orderNumber = row["Order_Number"].ToString(),
                        roomName = row["RoomName"].ToString(),
                        guestName = row["GuestName"].ToString(),
                        itemsPreview = itemsPreview,
                        totalAmount = Convert.ToDecimal(row["Total_Amount"]).ToString("N0")
                    });
                }

                return new { success = true, orders = orders };
            }
            catch
            {
                return new { success = false, orders = new object[0] };
            }
        }

        /// <summary>
        /// Web method to claim an order
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object ClaimOrder(long orderId)
        {
            try
            {
                var context = System.Web.HttpContext.Current;
                short staffId = (context != null && context.Session["admin_id"] != null) ? Convert.ToInt16(context.Session["admin_id"]) : (short)0;

                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                var code = new code();

                code.DatabaseInsertSafe(connectionString,
                    @"UPDATE Guest_Room_Service_Orders
                      SET Order_Status = 'CONFIRMED', Confirmed_By = @StaffId, Confirmed_Date = GETDATE()
                      WHERE ID = @OrderId AND Order_Status = 'PENDING'",
                    new Dictionary<string, object>
                    {
                        { "@OrderId", orderId },
                        { "@StaffId", staffId }
                    });

                return new { success = true };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }
}
