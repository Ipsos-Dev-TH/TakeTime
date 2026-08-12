using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Web;
using System.Web.UI;
using Take_Time_BangPhra;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class Balance : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private code _code;
        private int _reservationId;
        private string _guestMobilePhone;

        protected void Page_Load(object sender, EventArgs e)
        {
            _guestPortalService = new GuestPortalService(_connectionString);
            _code = new code();

            // Check session
            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
            }

            if (!IsPostBack)
            {
                LoadBalance();
            }
        }

        /// <summary>
        /// Validate guest session
        /// </summary>
        private bool ValidateGuestSession()
        {
            string sessionToken = Request.Cookies["GuestSession"]?.Value ?? Session["GuestSessionToken"]?.ToString();

            if (string.IsNullOrEmpty(sessionToken))
            {
                return false;
            }

            DataTable dtSession = _guestPortalService.ValidateGuestSession(sessionToken);

            if (dtSession.Rows.Count == 0)
            {
                return false;
            }

            DataRow session = dtSession.Rows[0];
            _reservationId = Convert.ToInt32(session["Reservation_ID"]);
            _guestMobilePhone = session["Customer_MobilePhone"].ToString();

            return true;
        }

        /// <summary>
        /// Load balance information
        /// </summary>
        private void LoadBalance()
        {
            try
            {
                DataTable dtBalance = _guestPortalService.GetGuestBalance(_reservationId);

                if (dtBalance.Rows.Count == 0)
                {
                    return;
                }

                DataRow balance = dtBalance.Rows[0];

                // Charges
                decimal roomCharge = balance["Room_Charge"] != DBNull.Value
                    ? Convert.ToDecimal(balance["Room_Charge"]) : 0;
                decimal roomServiceCharge = balance["Room_Service_Charges"] != DBNull.Value
                    ? Convert.ToDecimal(balance["Room_Service_Charges"]) : 0;
                decimal totalCharges = balance["Total_Charges"] != DBNull.Value
                    ? Convert.ToDecimal(balance["Total_Charges"]) : 0;
                decimal depositPaid = balance["Deposit_Paid"] != DBNull.Value
                    ? Convert.ToDecimal(balance["Deposit_Paid"]) : 0;
                decimal balanceDue = balance["Balance_Due"] != DBNull.Value
                    ? Convert.ToDecimal(balance["Balance_Due"]) : 0;

                // Display amounts
                lblRoomCharge.Text = roomCharge.ToString("N0");
                lblRoomServiceCharge.Text = roomServiceCharge.ToString("N0");
                lblTotalCharges.Text = totalCharges.ToString("N0");
                lblDepositPaid.Text = depositPaid.ToString("N0");
                lblBalanceDue.Text = balanceDue.ToString("N0");

                // Display dates
                DateTime checkIn = Convert.ToDateTime(balance["CheckIn"]);
                DateTime checkOut = Convert.ToDateTime(balance["CheckOut"]);
                lblCheckIn.Text = checkIn.ToString("dd MMM yyyy");
                lblCheckOut.Text = checkOut.ToString("dd MMM yyyy");

                // Show payment section only if there's a balance due
                if (balanceDue > 0)
                {
                    pnlPayment.Visible = true;
                    pnlPaidInFull.Visible = false;
                    txtPaymentAmount.Text = balanceDue.ToString("0.00");
                }
                else
                {
                    pnlPayment.Visible = false;
                    pnlPaidInFull.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading balance: {ex.Message}");
            }
        }

        /// <summary>
        /// Submit payment
        /// </summary>
        protected void btnSubmitPayment_Click(object sender, EventArgs e)
        {
            try
            {
                decimal paymentAmount;
                if (!decimal.TryParse(txtPaymentAmount.Text, out paymentAmount) || paymentAmount <= 0)
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                        "alert('Please enter a valid payment amount');", true);
                    return;
                }

                if (!filePaymentSlip.HasFile)
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                        "alert('Please upload your payment slip');", true);
                    return;
                }

                // Upload payment slip — ตรวจนามสกุล + สร้างชื่อไฟล์เอง (กัน .aspx web shell / path traversal)
                var slip = UploadHelper.Save(filePaymentSlip, "~/Uploads/PaymentSlips",
                    $"Payment_{_reservationId}", UploadHelper.ImageDoc);
                if (!slip.Success)
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                        $"alert('{slip.Error.Replace("'", "\\'")}');", true);
                    return;
                }
                string paymentSlipPath = slip.WebPath;

                // Record payment (create a payment record - you may need to create a table for this)
                var parameters = new Dictionary<string, object>
                {
                    { "@Reservation_ID", _reservationId },
                    { "@Customer_MobilePhone", _guestMobilePhone },
                    { "@Payment_Amount", paymentAmount },
                    { "@Payment_Slip_Path", paymentSlipPath },
                    { "@Payment_Note", txtPaymentNote.Text.Trim() },
                    { "@Payment_Status", "PENDING_VERIFICATION" } // Staff will verify
                };

                // Create payment record (simplified - you may want to create a proper Payment table)
                string query = @"
                    INSERT INTO Guest_Payment_Records
                    (Reservation_ID, Customer_MobilePhone, Payment_Amount, Payment_Slip_Path,
                     Payment_Note, Payment_Status, Payment_Date)
                    VALUES
                    (@Reservation_ID, @Customer_MobilePhone, @Payment_Amount, @Payment_Slip_Path,
                     @Payment_Note, @Payment_Status, GETDATE())";

                try
                {
                    _code.DatabaseInsertSafe(_connectionString, query, parameters);
                }
                catch
                {
                    // Table might not exist, create it
                    CreatePaymentRecordsTable();
                    _code.DatabaseInsertSafe(_connectionString, query, parameters);
                }

                // Clear form
                txtPaymentAmount.Text = "";
                txtPaymentNote.Text = "";

                // Show success
                ScriptManager.RegisterStartupScript(this, GetType(), "success",
                    "alert('Payment submitted successfully! Our staff will verify your payment shortly. You will receive a confirmation.');",
                    true);

                LoadBalance();
            }
            catch (Exception ex)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                    $"alert('Error submitting payment: {ex.Message}');", true);
            }
        }

        /// <summary>
        /// Create payment records table if it doesn't exist
        /// </summary>
        private void CreatePaymentRecordsTable()
        {
            string createTableQuery = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Guest_Payment_Records')
                BEGIN
                    CREATE TABLE Guest_Payment_Records (
                        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
                        Reservation_ID INT NOT NULL,
                        Customer_MobilePhone NVARCHAR(20) NOT NULL,
                        Payment_Amount DECIMAL(10,2) NOT NULL,
                        Payment_Slip_Path NVARCHAR(500) NULL,
                        Payment_Note NVARCHAR(500) NULL,
                        Payment_Status NVARCHAR(50) NOT NULL DEFAULT 'PENDING_VERIFICATION',
                        Payment_Date DATETIME NOT NULL DEFAULT GETDATE(),
                        Verified_By SMALLINT NULL,
                        Verified_Date DATETIME NULL,
                        FOREIGN KEY (Reservation_ID) REFERENCES Reservation(ID),
                        FOREIGN KEY (Customer_MobilePhone) REFERENCES Customer(MobilePhone)
                    );
                END";

            _code.DatabaseInsertSafe(_connectionString, createTableQuery, null);
        }
    }
}
