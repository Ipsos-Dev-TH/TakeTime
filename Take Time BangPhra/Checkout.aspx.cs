using System;
using System.Configuration;
using System.Data;
using System.Web.UI;

namespace Take_Time_BangPhra
{
    public partial class Checkout : System.Web.UI.Page
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private CheckoutService checkoutService;
        private PaymentDataAccess paymentDataAccess;
        private code codeInstance = new code();

        protected void Page_Load(object sender, EventArgs e)
        {
            checkoutService = new CheckoutService(connectionString);
            paymentDataAccess = new PaymentDataAccess(connectionString);

            if (!IsPostBack)
            {
                LoadReservationData();
                LoadSecurityHold();
            }
        }

        // ── วงเงินประกันความเสียหาย ──────────────────────────────────────────
        // แสดงเฉพาะเมื่อการจองนี้มีวงเงินกันไว้จริง — ฟีเจอร์ปิด/ไม่มีวงเงิน = มองไม่เห็นเลย

        private void LoadSecurityHold()
        {
            try
            {
                var svc = new Take_Time_BangPhra.Payments.SecurityHoldService(connectionString);
                if (!svc.TableReady()) return;

                var hold = svc.GetOpenHold(GetReservationId());
                if (hold == null) return;

                pnlSecurityHold.Visible = true;
                ViewState["holdId"] = hold.ID;

                if (hold.Status == Take_Time_BangPhra.Payments.HoldStatus.PendingCard)
                {
                    litHoldInfo.Text = "ส่งลิงก์กันวงเงิน " + hold.Amount.ToString("N2")
                        + " บาทให้ลูกค้าแล้ว แต่<b>ยังไม่ได้กรอกบัตร</b> — ตัดค่าเสียหายจากวงเงินไม่ได้";
                    pnlHoldActions.Visible = false;
                    return;
                }

                litHoldInfo.Text = "กันวงเงินไว้ <b>" + hold.Amount.ToString("N2") + " บาท</b>"
                    + (string.IsNullOrEmpty(hold.CardLast4) ? "" : " (บัตร ****" + Server.HtmlEncode(hold.CardLast4) + ")")
                    + (hold.ExpiresAt.HasValue
                        ? " · วงเงินหมดอายุ " + hold.ExpiresAt.Value.ToString("dd/MM/yyyy HH:mm") : "")
                    + "<br/>ไม่มีความเสียหาย → กด \"คืนวงเงิน\" · มีความเสียหาย → กรอกยอดแล้วกด \"ตัดค่าเสียหาย\" "
                    + "(ส่วนที่เหลือคืนลูกค้าอัตโนมัติ)";
            }
            catch { /* ส่วนเสริม — พังต้องไม่กระทบเช็คเอาท์ */ }
        }

        protected void btnCaptureHold_Click(object sender, EventArgs e)
        {
            long holdId = ViewState["holdId"] == null ? 0 : Convert.ToInt64(ViewState["holdId"]);
            decimal amount;
            if (!decimal.TryParse(txtCaptureAmount.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out amount) || amount <= 0)
            {
                litHoldMsg.Text = "<div class='alert alert-danger'>กรุณากรอกยอดค่าเสียหายให้ถูกต้อง</div>";
                LoadSecurityHold();
                return;
            }

            int? adminId = null;
            try { if (Session["UserID"] != null) adminId = Convert.ToInt32(Session["UserID"]); } catch { }

            string msg = new Take_Time_BangPhra.Payments.SecurityHoldService(connectionString)
                .CaptureDamage(holdId, amount, txtCaptureReason.Text.Trim(), adminId);
            litHoldMsg.Text = "<div class='alert alert-info'>" + Server.HtmlEncode(msg) + "</div>";
            LoadSecurityHold();
        }

        protected void btnReleaseHold_Click(object sender, EventArgs e)
        {
            long holdId = ViewState["holdId"] == null ? 0 : Convert.ToInt64(ViewState["holdId"]);
            int? adminId = null;
            try { if (Session["UserID"] != null) adminId = Convert.ToInt32(Session["UserID"]); } catch { }

            string msg = new Take_Time_BangPhra.Payments.SecurityHoldService(connectionString)
                .Release(holdId, adminId);
            litHoldMsg.Text = "<div class='alert alert-info'>" + Server.HtmlEncode(msg) + "</div>";
            LoadSecurityHold();
        }

        private void LoadReservationData()
        {
            int reservationId = GetReservationId();
            if (reservationId == 0)
            {
                ShowError("ไม่พบรหัสการจองที่ระบุ");
                btnCheckout.Enabled = false;
                return;
            }

            try
            {
                // Get reservation details
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };

                string query = @"
                    SELECT
                        r.ID,
                        r.Customer_MobilePhone,
                        c.Name AS CustomerName,
                        r.CheckinDate,
                        r.CheckoutDate,
                        r.TotalPrice,
                        r.Deposit,
                        r.Status
                    FROM Reservation r
                    LEFT JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                    WHERE r.ID = @reservationId";

                DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];

                    // Check if checked in
                    string status = row["Status"]?.ToString();
                    if (status != "เช็คอินแล้ว")
                    {
                        ShowWarning("การจองนี้ยังไม่ได้เช็คอิน หรือถูกยกเลิกแล้ว");
                        btnCheckout.Enabled = false;
                        return;
                    }

                    lblReservationID.Text = row["ID"].ToString();
                    lblCustomerName.Text = row["CustomerName"]?.ToString() ?? "-";
                    lblCustomerPhone.Text = row["Customer_MobilePhone"].ToString();

                    // Get accommodation names from Reservation_Accommodation
                    var accomParams = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@reservationId", reservationId }
                    };
                    string accomQuery = @"
                        SELECT a.AccomName
                        FROM Reservation_Accommodation ra
                        INNER JOIN Accommodation a ON ra.Accommodation_ID = a.ID
                        WHERE ra.Reservation_ID = @reservationId";

                    DataTable dtAccom = codeInstance.DatabaseQuerySafe(connectionString, accomQuery, accomParams);
                    string accomNames = "";
                    foreach (DataRow accomRow in dtAccom.Rows)
                    {
                        accomNames += accomRow["AccomName"].ToString() + ", ";
                    }
                    lblAccommodation.Text = !string.IsNullOrEmpty(accomNames)
                        ? accomNames.TrimEnd(',', ' ')
                        : "-";

                    lblCheckinDate.Text = Convert.ToDateTime(row["CheckinDate"]).ToString("dd/MM/yyyy");
                    lblCheckoutDate.Text = Convert.ToDateTime(row["CheckoutDate"]).ToString("dd/MM/yyyy");

                    // 🔧 Calculate total price including ALL product charges
                    decimal baseTotalPrice = Convert.ToDecimal(row["TotalPrice"]);
                    decimal deposit = row["Deposit"] != DBNull.Value ? Convert.ToDecimal(row["Deposit"]) : 0;

                    // Get product charges from Reservation_Product_Charges
                    decimal productCharges = 0;
                    try
                    {
                        var chargesParams = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "@reservationId", reservationId }
                        };
                        string chargesQuery = @"
                            SELECT ISNULL(SUM(TotalAmount), 0) as TotalCharges
                            FROM Reservation_Product_Charges
                            WHERE Reservation_ID = @reservationId
                            AND Status <> 'CANCELLED'";
                        DataTable dtCharges = codeInstance.DatabaseQuerySafe(connectionString, chargesQuery, chargesParams);
                        if (dtCharges.Rows.Count > 0 && dtCharges.Rows[0]["TotalCharges"] != DBNull.Value)
                        {
                            productCharges = Convert.ToDecimal(dtCharges.Rows[0]["TotalCharges"]);
                        }
                    }
                    catch
                    {
                        // Ignore if table doesn't exist
                    }

                    // Get Room Service charges (CHARGE_TO_ROOM orders)
                    decimal roomServiceCharges = 0;
                    try
                    {
                        var rsParams = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "@reservationId", reservationId }
                        };
                        string rsQuery = @"
                            SELECT ISNULL(SUM(Total_Amount), 0) as TotalCharges
                            FROM Guest_Room_Service_Orders
                            WHERE Reservation_ID = @reservationId
                            AND Payment_Method = 'CHARGE_TO_ROOM'
                            AND Order_Status <> 'CANCELLED'";
                        DataTable dtRS = codeInstance.DatabaseQuerySafe(connectionString, rsQuery, rsParams);
                        if (dtRS.Rows.Count > 0 && dtRS.Rows[0]["TotalCharges"] != DBNull.Value)
                        {
                            roomServiceCharges = Convert.ToDecimal(dtRS.Rows[0]["TotalCharges"]);
                        }
                    }
                    catch
                    {
                        // Ignore if table doesn't exist
                    }

                    // Add room service to product charges
                    productCharges += roomServiceCharges;

                    // Calculate total price with ALL product charges
                    decimal totalPriceWithCharges = baseTotalPrice + productCharges;

                    // Get accurate total paid from Payment_History
                    decimal totalPaid = 0;
                    try
                    {
                        totalPaid = paymentDataAccess.GetTotalPaidAmount(reservationId);

                        // ✅ FIX: Fallback to Deposit if no payment history (same as ReserveTable)
                        if (totalPaid == 0 && deposit > 0)
                        {
                            totalPaid = deposit;
                        }
                    }
                    catch
                    {
                        // Fallback to Deposit if Payment_History not available
                        totalPaid = deposit;
                    }

                    // Calculate remaining balance
                    decimal remainingBalance = totalPriceWithCharges - totalPaid;

                    // Check for pending product charges (for warning message)
                    decimal pendingCharges = 0;
                    try
                    {
                        var pendingParams = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "@reservationId", reservationId }
                        };
                        string pendingQuery = @"
                            SELECT ISNULL(SUM(TotalAmount), 0) as PendingCharges
                            FROM Reservation_Product_Charges
                            WHERE Reservation_ID = @reservationId
                            AND Status = 'PENDING'";
                        DataTable dtPending = codeInstance.DatabaseQuerySafe(connectionString, pendingQuery, pendingParams);
                        if (dtPending.Rows.Count > 0 && dtPending.Rows[0]["PendingCharges"] != DBNull.Value)
                        {
                            pendingCharges = Convert.ToDecimal(dtPending.Rows[0]["PendingCharges"]);
                        }
                    }
                    catch
                    {
                        // Ignore if table doesn't exist
                    }

                    // Check for pending Room Service orders (not yet delivered)
                    decimal pendingRoomService = 0;
                    int pendingRSCount = 0;
                    try
                    {
                        var pendingRSParams = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "@reservationId", reservationId }
                        };
                        string pendingRSQuery = @"
                            SELECT COUNT(*) as OrderCount, ISNULL(SUM(Total_Amount), 0) as PendingAmount
                            FROM Guest_Room_Service_Orders
                            WHERE Reservation_ID = @reservationId
                            AND Payment_Method = 'CHARGE_TO_ROOM'
                            AND Order_Status NOT IN ('DELIVERED', 'CANCELLED')";
                        DataTable dtPendingRS = codeInstance.DatabaseQuerySafe(connectionString, pendingRSQuery, pendingRSParams);
                        if (dtPendingRS.Rows.Count > 0)
                        {
                            pendingRSCount = Convert.ToInt32(dtPendingRS.Rows[0]["OrderCount"]);
                            pendingRoomService = dtPendingRS.Rows[0]["PendingAmount"] != DBNull.Value
                                ? Convert.ToDecimal(dtPendingRS.Rows[0]["PendingAmount"]) : 0;
                        }
                    }
                    catch { }

                    lblTotalPrice.Text = totalPriceWithCharges.ToString("N2");
                    lblPaidAmount.Text = totalPaid.ToString("N2");
                    lblTotalPaid.Text = totalPaid.ToString("N2");
                    lblRemainingBalance.Text = remainingBalance.ToString("N2");

                    // Show pending charges warning if any
                    string warningMessages = "";
                    if (pendingCharges > 0)
                    {
                        warningMessages += $"⚠️ มีสินค้าชาร์จเข้าห้องที่ยังไม่ได้ชำระ: {pendingCharges:N2} บาท<br/>";
                    }
                    if (pendingRSCount > 0)
                    {
                        warningMessages += $"🍽️ มี Room Service {pendingRSCount} รายการที่ยังไม่ได้จัดส่ง (฿{pendingRoomService:N0})<br/>";
                    }
                    if (!string.IsNullOrEmpty(warningMessages))
                    {
                        ShowWarning(warningMessages + "กรุณาตรวจสอบก่อนเช็คเอาท์");
                    }

                    // Check payment status
                    // ✅ STRICT VALIDATION: Must pay FULL amount before checkout
                    if (remainingBalance <= 0)
                    {
                        pnlPaymentComplete.Visible = true;
                        pnlPaymentIncomplete.Visible = false;
                        lblPaymentStatus.Text = "<span class='icon-success'><i class='fa fa-check-circle'></i> ชำระครบแล้ว</span>";
                        btnCheckout.Enabled = true;
                    }
                    else
                    {
                        pnlPaymentComplete.Visible = false;
                        pnlPaymentIncomplete.Visible = true;
                        lblPaymentStatus.Text = "<span class='icon-warning'><i class='fa fa-exclamation-triangle'></i> ยังไม่ครบ</span>";

                        // ✅ Set link to Reserve page in Edit mode
                        lnkGoToReserve.NavigateUrl = $"~/Reserve.aspx?id={reservationId}&command=edit";

                        // 🔒 STRICT: ไม่อนุญาตให้เช็คเอาท์ถ้ายอดไม่ครบ 100%
                        ShowWarning($"⚠️ ไม่สามารถเช็คเอาท์ได้<br/>" +
                                   $"กรุณาชำระเงินให้ครบ 100% ก่อนเช็คเอาท์<br/>" +
                                   $"<strong>ยอดคงเหลือ: {remainingBalance:N2} บาท</strong><br/><br/>" +
                                   $"💡 กรุณาไปชำระเงินที่หน้า Reserve (โหมดแก้ไข)");
                        btnCheckout.Enabled = false;
                    }
                }
                else
                {
                    ShowError("ไม่พบข้อมูลการจอง");
                    btnCheckout.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message);
                btnCheckout.Enabled = false;
            }
        }

        private bool CheckCanCheckout(int reservationId)
        {
            try
            {
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };

                string query = "SELECT dbo.fn_CanCheckout(@reservationId) AS CanCheckout";
                DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, parameters);

                if (dt.Rows.Count > 0)
                {
                    return Convert.ToBoolean(dt.Rows[0]["CanCheckout"]);
                }

                return false;
            }
            catch
            {
                // If function doesn't exist, require full payment
                return false;
            }
        }

        protected void btnCheckout_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            int reservationId = GetReservationId();
            if (reservationId == 0)
            {
                ShowError("ไม่พบรหัสการจอง");
                return;
            }

            // Validate rating
            int rating = 0;
            if (!string.IsNullOrEmpty(hfRating.Value))
            {
                rating = int.Parse(hfRating.Value);
            }

            if (rating == 0)
            {
                ShowError("กรุณาให้คะแนนความพึงพอใจก่อนเช็คเอาท์");
                return;
            }

            try
            {
                string notes = txtNotes.Text.Trim();

                // Get admin ID from session (required)
                if (Session["UserID"] == null)
                {
                    ShowError("ต้องเข้าสู่ระบบด้วยบัญชี Admin เพื่อทำการเช็คเอาท์");
                    return;
                }
                int adminId = Convert.ToInt32(Session["UserID"]);

                // ค่าเสียหาย/ของหาย: อ่านจากช่องกรอกจริง (เดิม hardcode 0 → ค่าเสียหายไม่เคยลงบัญชี)
                // นับเฉพาะเมื่อ checklist ข้อนั้น "ไม่ผ่าน"; ยอดนี้จะถูกแยกจากมัดจำเข้า DAMAGE/OTHER_INCOME
                // ตอนตัดมัดจำ (MapCheckoutToJournal) แทนที่จะนับเป็นรายได้ห้องทั้งก้อน
                decimal damageAmt = 0, missingAmt = 0;
                if (!chkRoomCondition.Checked) decimal.TryParse(txtDamageAmount.Text?.Trim(), out damageAmt);
                if (!chkMissingItems.Checked) decimal.TryParse(txtMissingAmount.Text?.Trim(), out missingAmt);
                if (damageAmt < 0) damageAmt = 0;
                if (missingAmt < 0) missingAmt = 0;

                // Process checkout with checklist data
                var result = checkoutService.ProcessCheckout(
                    reservationId,
                    adminId,
                    roomDamage: !chkRoomCondition.Checked,  // ไม่ผ่าน = มีความเสียหาย
                    damageDescription: !chkRoomCondition.Checked ? "ตรวจพบความเสียหาย" : null,
                    damageCharge: damageAmt,
                    missingItems: !chkMissingItems.Checked, // ไม่ผ่าน = ของหาย
                    missingItemsDescription: !chkMissingItems.Checked ? "อุปกรณ์ไม่ครบ" : null,
                    missingItemsCharge: missingAmt,
                    keyReturned: chkKeyReturn.Checked,
                    cleaningStatus: chkCleaning.Checked ? "GOOD" : "DIRTY",
                    guestSatisfaction: (byte)rating,
                    notes: notes
                );

                if (result.Success)
                {
                    // Accounting sync is handled inside CheckoutService.ProcessCheckout()
                    // which reads TotalPaid from Payment_History for the deposit amount.

                    ShowSuccess($"เช็คเอาท์สำเร็จ!<br/>" +
                               $"รหัสการจอง: {reservationId}<br/>" +
                               $"เวลาเช็คเอาท์: {DateTime.Now:dd/MM/yyyy HH:mm}<br/>" +
                               $"คะแนนความพึงพอใจ: {rating}/5 ดาว<br/><br/>" +
                               $"ขอบคุณที่ใช้บริการ!");

                    // Disable form
                    btnCheckout.Enabled = false;
                    DisableChecklistItems();

                    // Redirect after 1.5 seconds (faster response)
                    Response.AddHeader("REFRESH", "1;URL=ReserveTable.aspx");
                }
                else
                {
                    ShowError("การเช็คเอาท์ล้มเหลว: " + result.Message);
                }
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาด: " + ex.Message);
            }
        }

        private void DisableChecklistItems()
        {
            chkRoomCondition.Enabled = false;
            chkMissingItems.Enabled = false;
            chkKeyReturn.Enabled = false;
            chkCleaning.Enabled = false;
            chkElectrical.Enabled = false;
            chkPersonalItems.Enabled = false;
            txtNotes.Enabled = false;
        }

        private int GetReservationId()
        {
            if (Request.QueryString["id"] != null && int.TryParse(Request.QueryString["id"], out int id))
            {
                return id;
            }
            return 0;
        }

        private void ShowSuccess(string message)
        {
            pnlSuccess.Visible = true;
            pnlError.Visible = false;
            pnlWarning.Visible = false;
            lblSuccess.Text = message;
        }

        private void ShowError(string message)
        {
            pnlError.Visible = true;
            pnlSuccess.Visible = false;
            pnlWarning.Visible = false;
            lblError.Text = message;
        }

        private void ShowWarning(string message)
        {
            pnlWarning.Visible = true;
            pnlSuccess.Visible = false;
            pnlError.Visible = false;
            lblWarning.Text = message;
        }
    }
}
