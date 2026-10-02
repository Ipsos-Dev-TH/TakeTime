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
                // เบอร์ต่างประเทศ "+852…" ใน query string ที่ไม่ได้ encode → '+' กลายเป็นช่องว่าง
                // (แบบเดียวกับ Reserve.aspx) คืนเป็น '+' ก่อนใช้ค้นหา ไม่งั้นหาการจองไม่เจอ
                if (!string.IsNullOrEmpty(check)) check = check.Replace(" ", "+");

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
                    Label10.CssClass = "error-badge";
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
                // ใบที่เลื่อนเข้าพัก (วันแทน 1990-01-01 / ว่าง) — ไม่โชว์ "01 มกราคม 1990" ให้ลูกค้างง
                DateTime? confIn = ToDateOrNull(dtReservationAccommodation.Rows[0]["CheckinDate"]);
                DateTime? confOut = ToDateOrNull(dtReservationAccommodation.Rows[0]["CheckoutDate"]);
                bool datesPending = !confIn.HasValue || RescheduleService.IsPlaceholderDate(confIn);
                Label5.Text = datesPending ? "ยังไม่กำหนด (เลื่อนเข้าพัก)" : confIn.Value.ToString("dd MMMM yyyy");
                Label6.Text = datesPending || !confOut.HasValue ? "ยังไม่กำหนด" : confOut.Value.ToString("dd MMMM yyyy");
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
                    decimal price = 0m;
                    decimal.TryParse(dtReservation.Rows[i]["Price"].ToString(), out price);

                    // ห้องคิดตามคน: Amount = จำนวนผู้เข้าพัก, Price = ต่อคน/คืน
                    // ห้องปกติ: Amount = จำนวนคืน (ไม่ได้เก็บจำนวนคน) → ไม่แสดงจำนวนคน (เดิมขึ้น "2 คน" ตายตัว ซึ่งไม่จริง)
                    if (dtReservation.Rows[i]["LimitWithPeople"].ToString() == "True")
                    {
                        Accom += $"• {dtReservation.Rows[i]["AccomName"]} — {dtReservation.Rows[i]["Amount"]} คน × ฿{price:n0} ต่อคน/คืน\r\n";
                    }
                    else
                    {
                        Accom += $"• {dtReservation.Rows[i]["AccomName"]} — ฿{price:n0} ต่อคืน\r\n";
                    }
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

                // Add product charges (room charges) — LEFT JOIN: ค่าบริการที่ไม่ใช่สินค้าในสต๊อก (ค่าสัตว์เลี้ยง/กิจกรรม,
                // Product_ID = NULL) ต้องแสดงด้วย (เดิม INNER JOIN ตัดทิ้ง แต่ยอดรวมด้านซ้ายนับรวมแล้ว → ลูกค้าเห็นยอดไม่ตรงรายการ)
                var chargesParams = new Dictionary<string, object> { { "@ReservationID", id } };
                DataTable dtProductCharges = code2.DatabaseQuerySafe(conn,
                    "SELECT ISNULL(P.Product_Name, PC.Product_Name) AS ChargeName, PC.Quantity, PC.TotalAmount, PC.Status, PC.Notes, PC.ChargedDate " +
                    "FROM Reservation_Product_Charges PC " +
                    "LEFT JOIN Product P ON PC.Product_ID = P.ID " +
                    "WHERE PC.Reservation_ID = @ReservationID AND PC.Status <> 'CANCELLED' " +
                    "ORDER BY PC.ChargedDate",
                    chargesParams);

                if (dtProductCharges.Rows.Count > 0)
                {
                    if (Items.Length > 0) Items += "\r\n";
                    Items += "🛒 ค่าบริการ / สินค้าชาร์จเข้าห้อง:\r\n";
                    for (int i = 0; i < dtProductCharges.Rows.Count; i++)
                    {
                        string productName = dtProductCharges.Rows[i]["ChargeName"].ToString();
                        decimal quantity = dtProductCharges.Rows[i]["Quantity"] == DBNull.Value ? 0m : Convert.ToDecimal(dtProductCharges.Rows[i]["Quantity"]);
                        decimal totalAmount = dtProductCharges.Rows[i]["TotalAmount"] == DBNull.Value ? 0m : Convert.ToDecimal(dtProductCharges.Rows[i]["TotalAmount"]);
                        string status = dtProductCharges.Rows[i]["Status"].ToString();
                        string statusIcon = status == "PAID" ? "✅" : "⏳";
                        string statusText = status == "PAID" ? "ชำระแล้ว" : "รอชำระ";
                        string chargeNotes = Convert.ToString(dtProductCharges.Rows[i]["Notes"]);
                        bool isPetFee = !string.IsNullOrEmpty(chargeNotes)
                            && chargeNotes.StartsWith(PetStay.ChargeNotePrefix, StringComparison.Ordinal);

                        if (isPetFee)
                            Items += $"• 🐾 {productName} - ฿{totalAmount:n0} บาท {statusIcon} {statusText}\r\n";
                        else
                            Items += $"• {productName} ({quantity:n0} ชิ้น) - ฿{totalAmount:n0} บาท {statusIcon} {statusText}\r\n";
                    }
                }

                Label9.Text = string.IsNullOrEmpty(Items) ? "ไม่มีรายการ" : Items;

                // Set payment information
                // 🔧 ยอดเงิน — สูตรกลาง ReservationBalance (ตรงกับตารางรายวัน/หน้ารายการจอง/เช็คเอาท์)
                // ค่าห้อง + ค่าใช้จ่ายในห้อง, ยอดรับแล้ว = Payment_History (ไม่มีแถว → Deposit),
                // Channel Collect: ค่าห้องถือว่า OTA จ่ายแล้ว
                int resIdNum;
                int.TryParse(id, out resIdNum);
                ReservationBalance bal = resIdNum > 0 ? ReservationBalance.Load(conn, resIdNum) : null;
                if (bal == null)
                {
                    DataRow r0 = dtReservationAccommodation.Rows[0];
                    decimal baseTotal = r0["TotalPrice"] != DBNull.Value ? Convert.ToDecimal(r0["TotalPrice"]) : 0m;
                    decimal dep = r0["Deposit"] != DBNull.Value ? Convert.ToDecimal(r0["Deposit"]) : 0m;
                    bal = ReservationBalance.Compute(resIdNum, ReservationBalance.ModeNone, baseTotal, 0m, 0m, 0m, 0, dep);
                }

                decimal totalPrice = bal.Total;
                decimal totalPaid = bal.Received;
                decimal remainingBalance = bal.Due;

                Label11.Text = totalPrice.ToString("n0");
                Label12.Text = totalPaid.ToString("n0");
                Label13.Text = remainingBalance.ToString("n0");
                string remarkText = dtReservationAccommodation.Rows[0]["Remark"].ToString();
                Label14.Text = string.IsNullOrWhiteSpace(remarkText) ? "—" : remarkText;

                Label10.Text = "ยืนยันการจองสำเร็จ ✓";

                // สถานะการชำระเงิน + ขั้นตอนถัดไป (ส่วนเสริม — ล้มก็ไม่กระทบหน้ายืนยัน)
                bool hasPets = false;
                try
                {
                    hasPets = dtReservationAccommodation.Columns.Contains("Pet_Count")
                        && dtReservationAccommodation.Rows[0]["Pet_Count"] != DBNull.Value
                        && Convert.ToInt32(dtReservationAccommodation.Rows[0]["Pet_Count"]) > 0;
                }
                catch { hasPets = false; }
                LoadNextSteps(resIdNum, check, bal, datesPending ? (DateTime?)null : confIn, hasPets);

                // นโยบายการจอง + ฉบับที่ลูกค้ายอมรับ (ส่วนเสริม — ล้มก็ไม่กระทบหน้ายืนยัน)
                LoadPolicies(dtReservationAccommodation.Rows[0]);

                // 🐾 สัตว์เลี้ยงเข้าพัก: จำนวนต่อห้อง ค่าบริการ การยอมรับนโยบาย (ส่วนเสริม — ล้มก็ไม่กระทบ)
                LoadPetInfo(dtReservationAccommodation);
            }
            catch (Exception ex)
            {
                Label10.Text = "ยืนยันการจองผิดพลาด: " + ex.Message;
                Label10.CssClass = "error-badge";

                // Log detailed error for debugging
                code2.Logs(conn, "Reservation_Confirmed Error",
                    $"ID: {Request.QueryString["id"]}, Check: {Request.QueryString["check"]}, Error: {ex.Message}, StackTrace: {ex.StackTrace}",
                    "SYSTEM");

                // Show detailed error in development
                System.Diagnostics.Debug.WriteLine($"Reservation_Confirmed Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }

        private static DateTime? ToDateOrNull(object v)
        {
            if (v == null || v == DBNull.Value) return null;
            DateTime d;
            if (v is DateTime) return (DateTime)v;
            return DateTime.TryParse(Convert.ToString(v), out d) ? (DateTime?)d : null;
        }

        /// <summary>
        /// การ์ด "สถานะการชำระเงิน" + "ขั้นตอนถัดไป" สำหรับลูกค้า:
        /// · ชำระออนไลน์ (สถานะ รอชำระเงิน) → ปุ่มชำระต่อ + เวลาที่ห้องถูกกันไว้
        /// · โอนเอง → ได้รับสลิปแล้ว/รอเจ้าหน้าที่ตรวจ (Payment_Slips.VerificationStatus) / ตรวจแล้ว / ไม่ผ่าน
        /// · ชำระครบ / ยกเลิก / ยังไม่พบการชำระ
        /// อ่านไม่ได้ส่วนไหน = ข้ามส่วนนั้น (หน้าแสดงต่อได้เสมอ)
        /// </summary>
        private void LoadNextSteps(int reservationId, string phone, ReservationBalance bal, DateTime? checkinDate, bool hasPets)
        {
            try
            {
                if (litNextSteps == null || reservationId <= 0 || bal == null) return;
                var p = new Dictionary<string, object> { { "@id", reservationId } };

                string status = "";
                try
                {
                    DataTable dtSt = code2.DatabaseQuerySafe(conn, "SELECT Status FROM Reservation WHERE ID = @id", p);
                    if (dtSt != null && dtSt.Rows.Count > 0) status = Convert.ToString(dtSt.Rows[0]["Status"]).Trim();
                }
                catch { }

                string slipStatus = "";
                try
                {
                    DataTable dtSlip = code2.DatabaseQuerySafe(conn,
                        @"SELECT TOP 1 VerificationStatus FROM Payment_Slips
                           WHERE Reservation_ID = @id AND IsActive = 1
                           ORDER BY UploadedDate DESC", p);
                    if (dtSlip != null && dtSlip.Rows.Count > 0)
                        slipStatus = Convert.ToString(dtSlip.Rows[0]["VerificationStatus"]).Trim().ToUpperInvariant();
                }
                catch { slipStatus = ""; }

                bool pendingOnline = status == Take_Time_BangPhra.Payments.BookingPayment.PendingStatus;
                bool cancelled = status.IndexOf("ยกเลิก", StringComparison.Ordinal) >= 0;
                string cls, title, body = "", action = "";

                if (cancelled)
                {
                    cls = "rc-off"; title = "การจองนี้ถูกยกเลิกแล้ว";
                    body = "หากมีข้อสงสัยเรื่องการคืนเงิน กรุณาติดต่อรีสอร์ตพร้อมแจ้งรหัสการจอง #" + reservationId;
                }
                else if (pendingOnline)
                {
                    int holdMin = 60;
                    bool payOn = false;
                    try { holdMin = Take_Time_BangPhra.Payments.BookingPayment.HoldMinutes; } catch { }
                    try { payOn = Take_Time_BangPhra.Payments.BookingPayment.IsEnabled; } catch { }
                    cls = "rc-wait"; title = "⏳ รอชำระเงินออนไลน์ (Awaiting online payment)";
                    body = "ห้องถูกกันไว้ให้ชั่วคราวประมาณ " + holdMin + " นาทีนับจากเวลาจอง — หากไม่ชำระภายในเวลา การจองจะถูกยกเลิกอัตโนมัติ"
                        + "<br/>ชำระสำเร็จแล้วการจองจะยืนยันทันที ไม่ต้องส่งสลิป";
                    if (payOn)
                    {
                        string payUrl = Take_Time_BangPhra.Payments.BookingPayment.PayUrl(reservationId, phone, 0m);
                        action = "<a class=\"rc-pay-btn\" href=\"" + HttpUtility.HtmlAttributeEncode(payUrl) + "\">💳 ชำระเงินตอนนี้ (Pay now)</a>";
                    }
                }
                else if (bal.Total > 0m && bal.Due <= 0m)
                {
                    cls = "rc-ok"; title = "✅ ชำระครบแล้ว (Fully paid)";
                    body = "ไม่มียอดคงเหลือ — แสดงรหัสการจองตอนเช็คอินได้เลย";
                }
                else if (slipStatus == "PENDING")
                {
                    cls = "rc-info"; title = "📨 ได้รับสลิปแล้ว — รอเจ้าหน้าที่ตรวจสอบ (Slip received, pending verification)";
                    body = "เจ้าหน้าที่จะตรวจสอบยอดโอนและยืนยันการจองให้ ไม่ต้องจองซ้ำ — หากมีข้อสงสัยติดต่อรีสอร์ตพร้อมรหัสการจอง #" + reservationId;
                }
                else if (slipStatus == "REJECTED")
                {
                    cls = "rc-bad"; title = "⚠ สลิปไม่ผ่านการตรวจสอบ (Slip could not be verified)";
                    body = "กรุณาติดต่อรีสอร์ตพร้อมรหัสการจอง #" + reservationId + " เพื่อส่งหลักฐานการโอนอีกครั้ง";
                }
                else if (slipStatus == "APPROVED" || bal.Received > 0m || bal.IsChannelCollect)
                {
                    cls = "rc-ok"; title = "✅ ได้รับชำระแล้ว ฿" + bal.Received.ToString("n0") + " (Payment received)";
                    body = bal.IsChannelCollect ? "ค่าห้องชำระผ่านช่องทางที่จอง (OTA) แล้ว" : "เจ้าหน้าที่ได้รับเงินมัดจำของคุณแล้ว";
                }
                else
                {
                    cls = "rc-wait"; title = "ยังไม่พบการชำระเงิน (No payment yet)";
                    body = "หากโอนแล้ว กรุณาส่งสลิปให้รีสอร์ตพร้อมรหัสการจอง #" + reservationId + " เพื่อยืนยันการจอง";
                }

                var sb = new System.Text.StringBuilder();
                sb.Append("<div class=\"rc-status ").Append(cls).Append("\"><h2>").Append(Server.HtmlEncode(title)).Append("</h2>");
                if (body.Length > 0) sb.Append("<p>").Append(body).Append("</p>");
                sb.Append(action).Append("</div>");

                // ขั้นตอนถัดไป
                var steps = new List<string>();
                if (!cancelled)
                {
                    if (bal.Due > 0m && !pendingOnline)
                        steps.Add("ยอดคงเหลือ <b>฿" + bal.Due.ToString("n0") + "</b> ชำระเมื่อเช็คอิน (Balance due at check-in)");
                    if (checkinDate.HasValue)
                        steps.Add("เช็คอินวันที่ <b>" + Server.HtmlEncode(checkinDate.Value.ToString("dd MMMM yyyy")) + "</b>");
                    else
                        steps.Add("ยังไม่กำหนดวันเข้าพัก — ติดต่อรีสอร์ตเพื่อกำหนดวันใหม่");
                    steps.Add("บันทึกหน้านี้ไว้ (ปุ่มด้านล่าง) และแสดงรหัสการจอง <b>#" + reservationId + "</b> ตอนเช็คอิน");
                    if (hasPets)
                        steps.Add("🐾 นำสัตว์เลี้ยงมาตามจำนวนที่แจ้ง และปฏิบัติตามนโยบายสัตว์เลี้ยงด้านล่าง");
                }
                if (steps.Count > 0)
                {
                    sb.Append("<div class=\"rc-next\"><b>ขั้นตอนถัดไป (Next steps)</b><ol>");
                    foreach (string s in steps) sb.Append("<li>").Append(s).Append("</li>");
                    sb.Append("</ol></div>");
                }

                litNextSteps.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                try { litNextSteps.Text = ""; } catch { }
                try { code2.Logs(conn, "Reservation_Confirmed Next Steps Error", ex.Message, "SYSTEM"); } catch { }
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
                // Query all receipts for this reservation (IsDeposit → เลือกป้ายชื่อ ใบเสร็จ/ใบกำกับ)
                string query = @"
                    SELECT ID, UID, Created_Date, Total_Amount, Status, ISNULL(IsDeposit, 0) AS IsDeposit
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

        /// <summary>
        /// 🐾 สัตว์เลี้ยงเข้าพัก (PHASE19 migration 23): จำนวนต่อห้อง, ค่าบริการ (อยู่ในยอดรวมด้านบนแล้ว —
        /// เป็นรายการค่าใช้จ่ายในห้อง), เวลา/ฉบับที่ลูกค้ายอมรับนโยบาย + ข้อความนโยบายสัตว์เลี้ยง
        /// แสดงเฉพาะใบที่มีสัตว์เลี้ยง (ยังไม่รัน migration / ไม่มีสัตว์เลี้ยง = ซ่อน)
        /// </summary>
        private void LoadPetInfo(DataTable rows)
        {
            try
            {
                if (rows == null || rows.Rows.Count == 0 || !rows.Columns.Contains("Pet_Count")) return;
                DataRow res = rows.Rows[0];
                int total = res["Pet_Count"] == DBNull.Value ? 0 : Convert.ToInt32(res["Pet_Count"]);
                if (total <= 0) return;

                var sb = new System.Text.StringBuilder();
                sb.Append("<div style=\"font-size:0.95em; line-height:1.65;\">");

                // จำนวนต่อห้อง (แถวละห้องจากการ join Reservation_Accommodation)
                if (rows.Columns.Contains("Room_Pet_Count"))
                {
                    foreach (DataRow r in rows.Rows)
                    {
                        int n = r["Room_Pet_Count"] == DBNull.Value ? 0 : Convert.ToInt32(r["Room_Pet_Count"]);
                        if (n <= 0) continue;
                        sb.Append("<div>• ").Append(Server.HtmlEncode(Convert.ToString(r["AccomName"])))
                          .Append(": <b>").Append(n).Append(" ตัว</b></div>");
                    }
                }
                sb.Append("<div style=\"margin-top:4px;\">รวม <b>").Append(total).Append(" ตัว</b>");
                if (rows.Columns.Contains("Pet_Fee_Total") && res["Pet_Fee_Total"] != DBNull.Value)
                {
                    decimal fee = Convert.ToDecimal(res["Pet_Fee_Total"]);
                    if (fee > 0m)
                        sb.Append(" · ค่าบริการสัตว์เลี้ยง <b>").Append(fee.ToString("N2")).Append(" บาท</b>")
                          .Append(" <span style=\"color:#999;\">(รวมอยู่ในราคารวมแล้ว)</span>");
                }
                sb.Append("</div>");

                if (rows.Columns.Contains("Pet_Notes") && res["Pet_Notes"] != DBNull.Value
                    && !string.IsNullOrWhiteSpace(res["Pet_Notes"].ToString()))
                {
                    sb.Append("<div style=\"color:#666;\">รายละเอียด: ").Append(Server.HtmlEncode(res["Pet_Notes"].ToString())).Append("</div>");
                }

                if (rows.Columns.Contains("Pet_Policy_Accepted_At") && res["Pet_Policy_Accepted_At"] != DBNull.Value)
                {
                    DateTime acceptedAt = Convert.ToDateTime(res["Pet_Policy_Accepted_At"]);
                    string ver = rows.Columns.Contains("Pet_Policy_Version") && res["Pet_Policy_Version"] != DBNull.Value
                        ? res["Pet_Policy_Version"].ToString() : "-";
                    sb.Append("<div style=\"background:#e8f5e9; color:#2e7d32; border-radius:4px; padding:5px 8px; margin:6px 0;\">")
                      .Append("✅ ผู้จองยอมรับนโยบายการนำสัตว์เลี้ยงเข้าพักแล้ว เมื่อ ")
                      .Append(Server.HtmlEncode(acceptedAt.ToString("dd/MM/yyyy HH:mm")))
                      .Append(" น. (ฉบับที่ ").Append(Server.HtmlEncode(ver)).Append(")</div>");
                }

                string policy = BookingPolicy.Get(BookingPolicy.KeyPet);
                if (!string.IsNullOrWhiteSpace(policy))
                {
                    sb.Append("<details style=\"margin:4px 0; background:#fafafa; border:1px solid #eee; border-radius:4px; padding:5px 8px;\">")
                      .Append("<summary style=\"cursor:pointer; font-weight:bold; color:#5d4037;\">")
                      .Append(Server.HtmlEncode(BookingPolicy.Title(BookingPolicy.KeyPet)))
                      .Append(" <span style=\"font-weight:normal; color:#999;\">(")
                      .Append(Server.HtmlEncode(BookingPolicy.TitleEn(BookingPolicy.KeyPet)))
                      .Append(" · ฉบับปัจจุบัน ").Append(BookingPolicy.PetVersion)
                      .Append(")</span></summary><div style=\"margin-top:4px; color:#555;\">")
                      .Append(BookingPolicy.ToHtml(policy))
                      .Append("</div></details>");
                }
                sb.Append("</div>");

                litPetInfo.Text = sb.ToString();
                pnlPetInfo.Visible = true;
            }
            catch (Exception ex)
            {
                pnlPetInfo.Visible = false;
                try { code2.Logs(conn, "Reservation_Confirmed Pet Info Error", ex.Message, "SYSTEM"); } catch { }
            }
        }

        /// <summary>
        /// แสดงนโยบายการจอง (ยกเลิก / คืนเงิน / เงื่อนไข / ความเป็นส่วนตัว) + วันเวลาและฉบับที่ลูกค้ายอมรับ
        /// ข้อความแปลงผ่าน BookingPolicy.ToHtml (encode ก่อนเสมอ) — แทรก HTML/สคริปต์ไม่ได้
        /// </summary>
        private void LoadPolicies(DataRow res)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("<div style=\"font-size:0.95em; line-height:1.65;\">");

                // ใบจองที่ลูกค้ายอมรับเงื่อนไขไว้ (คอลัมน์จาก PHASE19 migration 22 — ไม่มีคอลัมน์ = ข้าม)
                try
                {
                    if (res != null && res.Table.Columns.Contains("Policy_Accepted_At")
                        && res["Policy_Accepted_At"] != DBNull.Value)
                    {
                        DateTime acceptedAt = Convert.ToDateTime(res["Policy_Accepted_At"]);
                        string ver = res.Table.Columns.Contains("Policy_Accepted_Version")
                                     && res["Policy_Accepted_Version"] != DBNull.Value
                            ? res["Policy_Accepted_Version"].ToString() : "-";
                        sb.Append("<div style=\"background:#e8f5e9; color:#2e7d32; border-radius:4px; padding:5px 8px; margin-bottom:6px;\">")
                          .Append("✅ ผู้จองยอมรับเงื่อนไขและนโยบายแล้ว เมื่อ ")
                          .Append(Server.HtmlEncode(acceptedAt.ToString("dd/MM/yyyy HH:mm")))
                          .Append(" น. (ฉบับที่ ").Append(Server.HtmlEncode(ver)).Append(")</div>");
                    }
                }
                catch { }

                // นโยบายการยกเลิก = นโยบายหลัก เปิดไว้ให้เห็นเลย ที่เหลือพับไว้
                foreach (string key in new[] { BookingPolicy.KeyCancellation, BookingPolicy.KeyRefund,
                                               BookingPolicy.KeyTerms, BookingPolicy.KeyPrivacy })
                {
                    string text = BookingPolicy.Get(key);
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    bool open = key == BookingPolicy.KeyCancellation;
                    sb.Append("<details").Append(open ? " open" : "")
                      .Append(" style=\"margin:4px 0; background:#fafafa; border:1px solid #eee; border-radius:4px; padding:5px 8px;\">")
                      .Append("<summary style=\"cursor:pointer; font-weight:bold; color:#5d4037;\">")
                      .Append(Server.HtmlEncode(BookingPolicy.Title(key)))
                      .Append(" <span style=\"font-weight:normal; color:#999;\">(")
                      .Append(Server.HtmlEncode(BookingPolicy.TitleEn(key)))
                      .Append(")</span></summary><div style=\"margin-top:4px; color:#555;\">")
                      .Append(BookingPolicy.ToHtml(text))
                      .Append("</div></details>");
                }

                sb.Append("<div style=\"color:#999; margin-top:4px;\">นโยบายฉบับปัจจุบัน: ")
                  .Append(BookingPolicy.Version).Append("</div></div>");

                litPolicies.Text = sb.ToString();
                pnlPolicies.Visible = true;
            }
            catch (Exception ex)
            {
                pnlPolicies.Visible = false;
                try { code2.Logs(conn, "Reservation_Confirmed Policies Error", ex.Message, "SYSTEM"); } catch { }
            }
        }

        // ลิงก์เอกสาร: ผ่าน handler ที่เสิร์ฟ "เอกสารทางการจาก NextAcc" ก่อน (ใบเสร็จมัดจำ/ใบกำกับภาษี
        // ที่ออกจริงในโหมด DOCUMENT) แล้วค่อย fallback PDF ที่ระบบ render เอง
        protected string GetReceiptPDFUrl(object receiptId, object uid, object createdDate)
        {
            try
            {
                string id = receiptId?.ToString() ?? "";
                if (string.IsNullOrEmpty(id)) return "#";
                return "/API/ViewReceiptDoc.ashx?doc=" + HttpUtility.UrlEncode(id);
            }
            catch
            {
                return "#";
            }
        }

        // ป้ายชื่อเอกสารตามชนิดจริง: มัดจำ = ใบเสร็จรับเงิน / รับชำระ = ใบกำกับภาษี
        protected string GetReceiptDocLabel(object isDeposit)
        {
            try
            {
                bool dep = isDeposit != DBNull.Value && Convert.ToBoolean(isDeposit);
                return dep ? "ใบเสร็จรับเงิน (มัดจำ)" : "ใบกำกับภาษี";
            }
            catch { return "เอกสาร"; }
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