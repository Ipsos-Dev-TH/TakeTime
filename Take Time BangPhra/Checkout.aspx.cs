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
            }
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

                    lblTotalPrice.Text = totalPriceWithCharges.ToString("N2");
                    lblPaidAmount.Text = totalPaid.ToString("N2");
                    lblTotalPaid.Text = totalPaid.ToString("N2");
                    lblRemainingBalance.Text = remainingBalance.ToString("N2");

                    // Show pending charges warning if any
                    if (pendingCharges > 0)
                    {
                        ShowWarning($"⚠️ มีสินค้าชาร์จเข้าห้องที่ยังไม่ได้ชำระ: {pendingCharges:N2} บาท<br/>" +
                                   $"กรุณาชำระยอดคงเหลือก่อนเช็คเอาท์");
                    }

                    // Check payment status
                    // ✅ STRICT VALIDATION: Must pay FULL amount before checkout
                    if (remainingBalance <= 0)
                    {
                        pnlPaymentComplete.Visible = true;
                        pnlPaymentIncomplete.Visible = false;
                        pnlPaymentForm.Visible = false;
                        lblPaymentStatus.Text = "<span class='icon-success'><i class='fa fa-check-circle'></i> ชำระครบแล้ว</span>";
                        btnCheckout.Enabled = true;
                    }
                    else
                    {
                        pnlPaymentComplete.Visible = false;
                        pnlPaymentIncomplete.Visible = true;
                        pnlPaymentForm.Visible = true;
                        lblPaymentStatus.Text = "<span class='icon-warning'><i class='fa fa-exclamation-triangle'></i> ยังไม่ครบ</span>";
                        lblPaymentRequired.Text = remainingBalance.ToString("N2");

                        // 🔒 STRICT: ไม่อนุญาตให้เช็คเอาท์ถ้ายอดไม่ครบ 100%
                        ShowWarning($"⚠️ ไม่สามารถเช็คเอาท์ได้<br/>" +
                                   $"กรุณาชำระเงินให้ครบ 100% ก่อนเช็คเอาท์<br/>" +
                                   $"<strong>ยอดคงเหลือ: {remainingBalance:N2} บาท</strong><br/><br/>" +
                                   $"💡 สามารถชำระเงินได้ในฟอร์มด้านล่าง");
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

                // Process checkout with checklist data
                var result = checkoutService.ProcessCheckout(
                    reservationId,
                    adminId,
                    roomDamage: !chkRoomCondition.Checked,  // ไม่ผ่าน = มีความเสียหาย
                    damageDescription: !chkRoomCondition.Checked ? "ตรวจพบความเสียหาย" : null,
                    damageCharge: 0,
                    missingItems: !chkMissingItems.Checked, // ไม่ผ่าน = ของหาย
                    missingItemsDescription: !chkMissingItems.Checked ? "อุปกรณ์ไม่ครบ" : null,
                    missingItemsCharge: 0,
                    keyReturned: chkKeyReturn.Checked,
                    cleaningStatus: chkCleaning.Checked ? "GOOD" : "DIRTY",
                    guestSatisfaction: (byte)rating,
                    notes: notes
                );

                if (result.Success)
                {
                    ShowSuccess($"เช็คเอาท์สำเร็จ!<br/>" +
                               $"รหัสการจอง: {reservationId}<br/>" +
                               $"เวลาเช็คเอาท์: {DateTime.Now:dd/MM/yyyy HH:mm}<br/>" +
                               $"คะแนนความพึงพอใจ: {rating}/5 ดาว<br/><br/>" +
                               $"ขอบคุณที่ใช้บริการ!");

                    // Disable form
                    btnCheckout.Enabled = false;
                    DisableChecklistItems();

                    // Redirect after 3 seconds
                    Response.AddHeader("REFRESH", "3;URL=ReserveTable.aspx");
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

        protected void btnSavePayment_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            int reservationId = GetReservationId();
            if (reservationId == 0)
            {
                ShowError("ไม่พบรหัสการจอง");
                return;
            }

            try
            {
                decimal paymentAmount = Convert.ToDecimal(txtPaymentAmount.Text);
                string paymentMethod = ddlPaymentMethod.SelectedValue;
                string notes = txtPaymentNotes.Text.Trim();

                if (paymentAmount <= 0)
                {
                    ShowError("กรุณากรอกยอดเงินให้ถูกต้อง");
                    return;
                }

                // Get admin ID from session
                int? adminId = null;
                if (Session["UserID"] != null)
                {
                    adminId = Convert.ToInt32(Session["UserID"]);
                }

                // Get customer phone
                string customerPhone = lblCustomerPhone.Text;

                // Create payment record using PaymentService
                var paymentService = new PaymentService();

                // Determine if this is full payment or deposit
                decimal remainingBalance = decimal.Parse(lblPaymentRequired.Text);
                bool isFullPayment = paymentAmount >= remainingBalance;
                string paymentType = isFullPayment ? "FULL" : "DEPOSIT";

                // Insert into Payment_History
                var paymentParams = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@ReservationId", reservationId },
                    { "@PaymentAmount", paymentAmount },
                    { "@PaymentType", paymentType },
                    { "@PaymentMethod", paymentMethod },
                    { "@AdminId", adminId ?? (object)DBNull.Value },
                    { "@CustomerPhone", customerPhone },
                    { "@Notes", notes }
                };

                string insertPaymentQuery = @"
                    INSERT INTO [dbo].[Payment_History] (
                        Reservation_ID,
                        PaymentDate,
                        PaymentAmount,
                        PaymentType,
                        PaymentMethod,
                        Receipt_ID,
                        ProcessedBy_AdminID,
                        PaidBy_CustomerPhone,
                        Status,
                        Notes,
                        CreatedDate,
                        UpdatedDate
                    ) VALUES (
                        @ReservationId,
                        GETDATE(),
                        @PaymentAmount,
                        @PaymentType,
                        @PaymentMethod,
                        NULL,
                        @AdminId,
                        @CustomerPhone,
                        'COMPLETED',
                        @Notes,
                        GETDATE(),
                        GETDATE()
                    )";

                codeInstance.DatabaseInsertSafe(connectionString, insertPaymentQuery, paymentParams);

                // Handle file upload if provided
                if (fuPaymentSlip.HasFile)
                {
                    try
                    {
                        string fileName = System.IO.Path.GetFileName(fuPaymentSlip.FileName);
                        string fileExtension = System.IO.Path.GetExtension(fileName).ToLower();

                        // Validate file type
                        if (fileExtension == ".jpg" || fileExtension == ".jpeg" || fileExtension == ".png" || fileExtension == ".pdf")
                        {
                            // Validate file size (5MB)
                            if (fuPaymentSlip.PostedFile.ContentLength <= 5242880)
                            {
                                string folderPath = Server.MapPath("~/Uploads/PaymentSlips/");
                                if (!System.IO.Directory.Exists(folderPath))
                                {
                                    System.IO.Directory.CreateDirectory(folderPath);
                                }

                                string uniqueFileName = $"{reservationId}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                                string filePath = System.IO.Path.Combine(folderPath, uniqueFileName);
                                fuPaymentSlip.SaveAs(filePath);
                            }
                            else
                            {
                                ShowWarning("ไฟล์มีขนาดใหญ่เกิน 5MB");
                            }
                        }
                        else
                        {
                            ShowWarning("ไฟล์ต้องเป็น JPG, PNG หรือ PDF เท่านั้น");
                        }
                    }
                    catch (Exception exFile)
                    {
                        // Log file upload error but don't fail the payment
                        System.Diagnostics.Debug.WriteLine($"File upload error: {exFile.Message}");
                    }
                }

                ShowSuccess($"บันทึกการชำระเงินสำเร็จ!<br/>" +
                           $"ยอดชำระ: {paymentAmount:N2} บาท<br/>" +
                           $"วิธีการชำระ: {ddlPaymentMethod.SelectedItem.Text}<br/><br/>" +
                           $"กำลังโหลดข้อมูลใหม่...");

                // Reload data after 2 seconds
                Response.AddHeader("REFRESH", "2;URL=Checkout.aspx?id=" + reservationId);
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
