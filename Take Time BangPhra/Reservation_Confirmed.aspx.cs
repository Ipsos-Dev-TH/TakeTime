using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;

namespace Take_Time_BangPhra
{
    public partial class Reservation_Confirmed : System.Web.UI.Page
    {
        code code2 = new code();
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected async void Page_Load(object sender, EventArgs e)
        {
            Page.MaintainScrollPositionOnPostBack = true;
            try
            {
                string id = Request.QueryString["id"];
                string check = Request.QueryString["check"];

                var accomParams = new Dictionary<string, object> { { "@ReservationID", id }, { "@CustomerPhone", check } };
                DataTable dtReservationAccommodation = code2.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Reservation] left join Customer on Customer.MobilePhone = Customer_MobilePhone " +
                    "right join Reservation_Accommodation on Reservation.ID = Reservation_Accommodation.Reservation_ID " +
                    "inner join Accommodation on Accommodation.ID = Accommodation_ID " +
                    "where Reservation.ID = @ReservationID AND Customer_MobilePhone = @CustomerPhone order by Accommodation.OrderID asc",
                    accomParams);

                // 🔍 Check if data exists
                if (dtReservationAccommodation == null || dtReservationAccommodation.Rows.Count == 0)
                {
                    Label10.Text = "ไม่พบข้อมูลการจอง - กรุณาตรวจสอบรหัสการจองและเบอร์โทรศัพท์";
                    code2.Logs(conn, "Reservation_Confirmed - No Data",
                        $"ID: {id}, Check: {check} - No reservation data found",
                        "SYSTEM");
                    return;
                }

                // Load payment slips from Payment_History
                LoadPaymentSlips(id, check);

                // Load receipts
                LoadReceipts(id);

                // Set basic information
                Label1.Text = id;
                Label2.Text = dtReservationAccommodation.Rows[0]["Name"].ToString();
                Label3.Text = dtReservationAccommodation.Rows[0]["NickName"].ToString();
                Label4.Text = check;
                Label5.Text = DateTime.Parse(dtReservationAccommodation.Rows[0]["CheckinDate"].ToString()).ToString("dd MMMM yyyy");
                Label6.Text = DateTime.Parse(dtReservationAccommodation.Rows[0]["CheckoutDate"].ToString()).ToString("dd MMMM yyyy");
                Label7.Text = dtReservationAccommodation.Rows[0]["StayDays"].ToString() + " คืน";

                // Set accommodation details with number of guests
                var resParams = new Dictionary<string, object> { { "@ReservationID", id } };
                DataTable dtReservation = code2.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Reservation] right join Reservation_Accommodation on Reservation.ID = Reservation_Accommodation.Reservation_ID " +
                    "inner join Accommodation on Accommodation.ID = Accommodation_ID " +
                    "where Reservation.ID = @ReservationID order by Accommodation.OrderID asc",
                    resParams);

                string Accom = "";
                for (int i = 0; i < dtReservation.Rows.Count; i++)
                {
                    int price = Convert.ToInt32(dtReservation.Rows[i]["Price"].ToString());
                    string peopleInfo = "";

                    // แสดงจำนวนผู้เข้าพักสำหรับแต่ละห้อง
                    if (dtReservation.Rows[i]["LimitWithPeople"].ToString() == "True")
                    {
                        peopleInfo = $" (จำนวนผู้เข้าพัก: {dtReservation.Rows[i]["Amount"].ToString()} คน)";
                    }
                    else
                    {
                        // ถ้าไม่ได้จำกัดจำนวนคนตาม Amount ให้ใช้ค่าเริ่มต้นหรือดึงจาก Reservation
                        peopleInfo = " (จำนวนผู้เข้าพัก: 2 คน)"; // หรือดึงจากฟิลด์อื่นที่เหมาะสม
                    }

                    Accom += $"• {dtReservation.Rows[i]["AccomName"].ToString()}{peopleInfo} - ฿{price:n0} บาท\r\n";
                }
                Label8.Text = Accom;

                // Set rent items
                string Items = "";
                var itemsParams = new Dictionary<string, object> { { "@ReservationID", id } };
                DataTable dtReservationItems = code2.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Reservation] right join Reservation_Items on Reservation.ID = Reservation_Items.Reservation_ID " +
                    "inner join Items on Items.ID = Items_ID where Reservation.ID = @ReservationID",
                    itemsParams);

                if (dtReservationItems.Rows.Count > 0)
                {
                    Items += "📦 ของเช่า:\r\n";
                    for (int i = 0; i < dtReservationItems.Rows.Count; i++)
                    {
                        int price = Convert.ToInt32(dtReservationItems.Rows[i]["Price"].ToString()) * Convert.ToInt32(dtReservationItems.Rows[i]["Amount"].ToString());
                        Items += $"• {dtReservationItems.Rows[i]["ItemName"].ToString()} ({dtReservationItems.Rows[i]["Amount"].ToString()} ชิ้น) - ฿{price:n0} บาท\r\n";
                    }
                }

                // Add product charges (room charges)
                var chargesParams = new Dictionary<string, object> { { "@ReservationID", id } };
                DataTable dtProductCharges = code2.DatabaseQuerySafe(conn,
                    "SELECT PC.*, P.Product_Name, PC.Quantity, PC.UnitPrice, PC.TotalAmount, PC.Status, PC.ChargedDate " +
                    "FROM Reservation_Product_Charges PC " +
                    "INNER JOIN Product P ON PC.Product_ID = P.ID " +
                    "WHERE PC.Reservation_ID = @ReservationID AND PC.Status <> 'CANCELLED' " +
                    "ORDER BY PC.ChargedDate",
                    chargesParams);

                if (dtProductCharges.Rows.Count > 0)
                {
                    if (Items.Length > 0) Items += "\r\n";
                    Items += "🛒 สินค้าชาร์จเข้าห้อง:\r\n";
                    for (int i = 0; i < dtProductCharges.Rows.Count; i++)
                    {
                        string productName = dtProductCharges.Rows[i]["Product_Name"].ToString();
                        decimal quantity = Convert.ToDecimal(dtProductCharges.Rows[i]["Quantity"]);
                        decimal totalAmount = Convert.ToDecimal(dtProductCharges.Rows[i]["TotalAmount"]);
                        string status = dtProductCharges.Rows[i]["Status"].ToString();
                        string statusIcon = status == "PAID" ? "✅" : "⏳";
                        string statusText = status == "PAID" ? "ชำระแล้ว" : "รอชำระ";

                        Items += $"• {productName} ({quantity:n0} ชิ้น) - ฿{totalAmount:n0} บาท {statusIcon} {statusText}\r\n";
                    }
                }

                Label9.Text = string.IsNullOrEmpty(Items) ? "ไม่มีรายการ" : Items;

                // Set payment information
                // 🔧 Calculate total price including product charges

                // 1. Get base total price from Reservation
                decimal baseTotalPrice = 0;
                var priceParams = new Dictionary<string, object> { { "@ReservationID", id } };
                DataTable dtReservationPrice = code2.DatabaseQuerySafe(conn,
                    "SELECT TotalPrice FROM Reservation WHERE ID = @ReservationID", priceParams);
                if (dtReservationPrice.Rows.Count > 0 && dtReservationPrice.Rows[0]["TotalPrice"] != DBNull.Value)
                {
                    baseTotalPrice = Convert.ToDecimal(dtReservationPrice.Rows[0]["TotalPrice"]);
                }

                // 2. Get product charges from Reservation_Product_Charges
                decimal productCharges = 0;
                var charges2Params = new Dictionary<string, object> { { "@ReservationID", id } };
                DataTable dtProductCharges2 = code2.DatabaseQuerySafe(conn,
                    @"SELECT ISNULL(SUM(TotalAmount), 0) as TotalCharges
                       FROM Reservation_Product_Charges
                       WHERE Reservation_ID = @ReservationID
                       AND Status <> 'CANCELLED'", charges2Params);
                if (dtProductCharges2.Rows.Count > 0 && dtProductCharges2.Rows[0]["TotalCharges"] != DBNull.Value)
                {
                    productCharges = Convert.ToDecimal(dtProductCharges2.Rows[0]["TotalCharges"]);
                }

                // 3. Calculate total price with charges
                decimal totalPrice = baseTotalPrice + productCharges;

                // 5. Get total paid from Payment_History
                decimal totalPaid = 0;
                var paidParams = new Dictionary<string, object> { { "@ReservationID", id } };
                DataTable dtPaid = code2.DatabaseQuerySafe(conn,
                    @"SELECT ISNULL(SUM(PaymentAmount), 0) as TotalPaid
                       FROM Payment_History
                       WHERE Reservation_ID = @ReservationID
                       AND Status = 'COMPLETED'", paidParams);
                if (dtPaid.Rows.Count > 0 && dtPaid.Rows[0]["TotalPaid"] != DBNull.Value)
                {
                    totalPaid = Convert.ToDecimal(dtPaid.Rows[0]["TotalPaid"]);
                }

                // 6. If no payment history, fallback to Deposit column
                if (totalPaid == 0)
                {
                    var depositParams = new Dictionary<string, object> { { "@ReservationID", id } };
                    DataTable dtDeposit = code2.DatabaseQuerySafe(conn,
                        "SELECT ISNULL(Deposit, 0) as Deposit FROM Reservation WHERE ID = @ReservationID", depositParams);
                    if (dtDeposit.Rows.Count > 0 && dtDeposit.Rows[0]["Deposit"] != DBNull.Value)
                    {
                        totalPaid = Convert.ToDecimal(dtDeposit.Rows[0]["Deposit"]);
                    }
                }

                // 7. Calculate remaining balance
                decimal remainingBalance = totalPrice - totalPaid;

                Label11.Text = totalPrice.ToString("n0");
                Label12.Text = totalPaid.ToString("n0");
                Label13.Text = remainingBalance.ToString("n0");
                Label14.Text = dtReservationAccommodation.Rows[0]["Remark"].ToString();

                Label10.Text = "ยืนยันการจองสำเร็จ ✓";
            }
            catch (Exception ex)
            {
                Label10.Text = "ยืนยันการจองผิดพลาด: " + ex.Message;

                // Log detailed error for debugging
                code2.Logs(conn, "Reservation_Confirmed Error",
                    $"ID: {Request.QueryString["id"]}, Check: {Request.QueryString["check"]}, Error: {ex.Message}, StackTrace: {ex.StackTrace}",
                    "SYSTEM");

                // Show detailed error in development
                System.Diagnostics.Debug.WriteLine($"Reservation_Confirmed Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }

        private void LoadPaymentSlips(string reservationId, string customerPhone)
        {
            try
            {
                // Query payment slips from Payment_History + Payment_Slips
                string query = @"
                    SELECT
                        ph.PaymentDate,
                        ph.PaymentAmount,
                        ph.PaymentType,
                        ph.PaymentMethod,
                        ps.SlipFileURL,
                        ps.FileName,
                        ph.Reservation_ID
                    FROM Payment_History ph
                    LEFT JOIN Payment_Slips ps ON ph.Receipt_ID = ps.Account_Receipt_ID AND ps.IsActive = 1
                    WHERE ph.Reservation_ID = @ReservationId
                      AND ph.Status = 'COMPLETED'
                    ORDER BY ph.PaymentDate DESC";

                var parameters = new Dictionary<string, object>
                {
                    { "@ReservationId", reservationId }
                };

                DataTable dtSlips = code2.DatabaseQuerySafe(conn, query, parameters);

                if (dtSlips.Rows.Count > 0)
                {
                    // 🆕 Generate SlipFileURL for old records that don't have Payment_Slips
                    // Note: New records use pattern {ReservationID}_{Phone}_{PaymentHistoryId}.jpg (already in DB)
                    //       Old records use pattern {ReservationID}_{Phone}.jpg (generated here)
                    foreach (DataRow row in dtSlips.Rows)
                    {
                        if (row["SlipFileURL"] == DBNull.Value || string.IsNullOrWhiteSpace(row["SlipFileURL"].ToString()))
                        {
                            // Generate OLD pattern for backward compatibility: Upload/Slip/{ReservationID}_{Phone}.jpg
                            string generatedPath = $"Upload/Slip/{reservationId}_{customerPhone}.jpg";
                            string fullPath = Server.MapPath("~/" + generatedPath);

                            // Check if file exists before setting path
                            if (File.Exists(fullPath))
                            {
                                row["SlipFileURL"] = generatedPath;
                                row["FileName"] = $"{reservationId}_{customerPhone}.jpg";
                            }
                        }
                    }

                    // Show slip count
                    lblSlipCount.Text = $"💳 มีการโอนเงินทั้งหมด {dtSlips.Rows.Count} ครั้ง";
                    lblSlipCount.Visible = true;

                    // Bind to repeater
                    rptPaymentSlips.DataSource = dtSlips;
                    rptPaymentSlips.DataBind();
                    rptPaymentSlips.Visible = true;

                    // Hide old image control
                    Image1.Visible = false;
                }
                else
                {
                    // Fallback to old slip image if no Payment_History records found
                    lblSlipCount.Text = "💳 ใช้รูปสลิปจากระบบเดิม";
                    lblSlipCount.Visible = true;
                    rptPaymentSlips.Visible = false;

                    Image1.ImageUrl = "./Upload/Slip/" + reservationId + "_" + customerPhone + ".jpg";
                    Image1.Visible = true;
                    Image1.DataBind();
                }
            }
            catch (Exception ex)
            {
                // Log error and fallback to old image
                lblSlipCount.Text = "⚠️ ไม่สามารถโหลดสลิปได้ แสดงรูปจากระบบเดิม";
                lblSlipCount.Visible = true;
                rptPaymentSlips.Visible = false;

                Image1.ImageUrl = "./Upload/Slip/" + reservationId + "_" + customerPhone + ".jpg";
                Image1.Visible = true;
                Image1.DataBind();

                code2.Logs(conn, "LoadPaymentSlips Error", ex.Message + " - " + ex.StackTrace, "SYSTEM");
            }
        }



        private void LoadReceipts(string reservationId)
        {
            try
            {
                // Query all receipts for this reservation
                string query = @"
                    SELECT ID, UID, Created_Date, Total_Amount, Status
                    FROM Account_Receipt
                    WHERE Reservation_ID = @ReservationId
                      AND Status = 'Normal'
                    ORDER BY Created_Date DESC";

                var parameters = new Dictionary<string, object>
                {
                    { "@ReservationId", reservationId }
                };

                DataTable dtReceipts = code2.DatabaseQuerySafe(conn, query, parameters);

                if (dtReceipts.Rows.Count > 0)
                {
                    // Show receipt links panel
                    pnlReceiptLinks.Visible = true;

                    // Bind to repeater
                    rptReceipts.DataSource = dtReceipts;
                    rptReceipts.DataBind();
                }
                else
                {
                    // No receipts found - hide panel
                    pnlReceiptLinks.Visible = false;
                }
            }
            catch (Exception ex)
            {
                // Log error and hide panel
                pnlReceiptLinks.Visible = false;

                code2.Logs(conn, "LoadReceipts Error", ex.Message + " - " + ex.StackTrace, "SYSTEM");
            }
        }

        // Helper method for generating receipt PDF URL
        protected string GetReceiptPDFUrl(object receiptId, object uid, object createdDate)
        {
            try
            {
                string id = receiptId?.ToString() ?? "";
                string receiptUID = uid?.ToString() ?? "";
                DateTime created = Convert.ToDateTime(createdDate);

                string year = created.Year.ToString();
                string month = created.Month.ToString("00");

                return $"/Documents/Receipt/{year}/{month}/{id}_{receiptUID}.pdf";
            }
            catch
            {
                return "#";
            }
        }

        private void GenerateAndDownloadReceipt(string reservationId)
        {
            // Implement PDF receipt generation and download
            // This is just a placeholder - you'll need to implement your PDF generation logic
            /*
            string filePath = GenerateReceiptPDF(reservationId);
            if (File.Exists(filePath))
            {
                Response.ContentType = "application/pdf";
                Response.AppendHeader("Content-Disposition", "attachment; filename=Receipt_" + reservationId + ".pdf");
                Response.TransmitFile(filePath);
                Response.End();
            }
            */
        }
    }
}