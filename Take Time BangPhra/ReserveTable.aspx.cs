using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using iTextSharp;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Security.Cryptography;
using iTextSharp.text.pdf.qrcode;
using System.Text;
using System.Threading.Tasks;

namespace Take_Time_BangPhra
{
    public partial class ReserveTable : Page
    {
        code code2 = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            Page.MaintainScrollPositionOnPostBack = true;
            try
            {
                if (Session["permission"]?.ToString() == "True")
                {
                    if (!IsPostBack)
                    {
                        // Select today's date in the calendar
                        Calendar1.SelectedDate = DateTime.Today;
                        Calendar1.VisibleDate = DateTime.Today;

                        // Trigger the selection changed event to load today's reservations
                        Calendar1_SelectionChanged(sender, e);
                    }
                }
                else
                {
                    Response.Redirect("./Default");
                }
            }
            catch
            {
                Response.Redirect("./Default");
            }
        }

        protected void Calendar1_SelectionChanged(object sender, EventArgs e)
        {
            Label1.Text = Calendar1.SelectedDate.ToString("dd MMMM yyyy");

            DataTable dtReservation = DatabaseQuery(conn,
                @"SELECT * FROM Reservation
                  INNER JOIN Customer ON Customer.MobilePhone = Reservation.Customer_MobilePhone
                  WHERE @SelectedDate >= CheckInDate AND @SelectedDate < CheckOutDate
                  AND (Reservation.Status != N'ยกเลิกคืนเงิน' AND Reservation.Status != N'ยกเลิกไม่คืนเงิน')",
                new SqlParameter("@SelectedDate", Calendar1.SelectedDate.ToString("yyyy-MM-dd")));

            DataTable dtReservation_Accom = DatabaseQuery(conn,
                @"SELECT * FROM Reservation
                  RIGHT JOIN Reservation_Accommodation ON Reservation.ID = Reservation_Accommodation.Reservation_ID
                  INNER JOIN Accommodation ON Accommodation.ID = Reservation_Accommodation.Accommodation_ID
                  WHERE @SelectedDate >= CheckInDate AND @SelectedDate < CheckOutDate
                  ORDER BY Accommodation.orderID ASC",
                new SqlParameter("@SelectedDate", Calendar1.SelectedDate.ToString("yyyy-MM-dd")));

            DataTable dtReservation_Items = DatabaseQuery(conn,
                @"SELECT * FROM Reservation
                  RIGHT JOIN Reservation_Items ON Reservation.ID = Reservation_Items.Reservation_ID
                  INNER JOIN Items ON Items.ID = Reservation_Items.Items_ID
                  WHERE @SelectedDate >= CheckInDate AND @SelectedDate < CheckOutDate
                  ORDER BY Items_ID ASC",
                new SqlParameter("@SelectedDate", Calendar1.SelectedDate.ToString("yyyy-MM-dd")));

            // Query สินค้าที่ชาร์จเข้าห้อง (Room Charges) - แสดงเฉพาะที่ไม่ถูกยกเลิก
            DataTable dtProductCharges = DatabaseQuery(conn,
                @"SELECT r.ID as Reservation_ID,
                         p.Product_Name,
                         rpc.Quantity,
                         rpc.Status
                  FROM Reservation r
                  INNER JOIN Reservation_Product_Charges rpc ON r.ID = rpc.Reservation_ID
                  INNER JOIN Product p ON rpc.Product_ID = p.ID
                  WHERE @SelectedDate >= r.CheckInDate AND @SelectedDate < r.CheckOutDate
                    AND rpc.Status <> 'CANCELLED'
                  ORDER BY rpc.ID ASC",
                new SqlParameter("@SelectedDate", Calendar1.SelectedDate.ToString("yyyy-MM-dd")));

            // Add additional columns if they don't exist
            if (!dtReservation.Columns.Contains("AccomName"))
                dtReservation.Columns.Add("AccomName");
            if (!dtReservation.Columns.Contains("Items"))
                dtReservation.Columns.Add("Items");
            if (!dtReservation.Columns.Contains("Remain"))
                dtReservation.Columns.Add("Remain");
            if (!dtReservation.Columns.Contains("Order"))
                dtReservation.Columns.Add("Order", typeof(int));
            if (!dtReservation.Columns.Contains("CountReserved"))
                dtReservation.Columns.Add("CountReserved");
            if (!dtReservation.Columns.Contains("TotalPriceWithCharges"))
                dtReservation.Columns.Add("TotalPriceWithCharges");

            for (int i = 0; i < dtReservation.Rows.Count; i++)
            {
                dtReservation.Rows[i]["Name"] = $"{dtReservation.Rows[i]["Name"]} - {dtReservation.Rows[i]["NickName"]} - {dtReservation.Rows[i]["Customer_MobilePhone"]}";

                string AccomName = "";
                int orderID = 99;

                for (int j = 0; j < dtReservation_Accom.Rows.Count; j++)
                {
                    if (dtReservation.Rows[i]["ID"].ToString() == dtReservation_Accom.Rows[j]["Reservation_ID"].ToString())
                    {
                        AccomName += dtReservation_Accom.Rows[j]["AccomName"];
                        if (dtReservation_Accom.Rows[j]["LimitWithPeople"].ToString() == "True")
                        {
                            AccomName += $": ({dtReservation_Accom.Rows[j]["Amount"]}คน)";
                        }
                        AccomName += "\r\n";

                        if (Convert.ToInt32(dtReservation_Accom.Rows[j]["OrderID"]) < orderID)
                        {
                            orderID = Convert.ToInt32(dtReservation_Accom.Rows[j]["OrderID"]);
                        }
                    }
                }

                dtReservation.Rows[i]["Order"] = orderID;
                dtReservation.Rows[i]["AccomName"] = AccomName.Trim();

                string Items = "";
                // ของเช่า (Reservation Items)
                for (int j = 0; j < dtReservation_Items.Rows.Count; j++)
                {
                    if (dtReservation.Rows[i]["ID"].ToString() == dtReservation_Items.Rows[j]["Reservation_ID"].ToString())
                    {
                        Items += $"[{dtReservation_Items.Rows[j]["ItemName"]} : ({dtReservation_Items.Rows[j]["Amount"]}ชิ้น)] ";
                    }
                }

                // สินค้าที่ชาร์จเข้าห้อง (Product Charges)
                for (int j = 0; j < dtProductCharges.Rows.Count; j++)
                {
                    if (dtReservation.Rows[i]["ID"].ToString() == dtProductCharges.Rows[j]["Reservation_ID"].ToString())
                    {
                        int quantity = Convert.ToInt32(dtProductCharges.Rows[j]["Quantity"]);
                        Items += $"[{dtProductCharges.Rows[j]["Product_Name"]} : ({quantity}ชิ้น)] ";
                    }
                }

                dtReservation.Rows[i]["Items"] = Items.Trim();

                // Calculate remaining amount (Total Price + Product Charges - Total Paid)
                int reservationId = Convert.ToInt32(dtReservation.Rows[i]["ID"]);

                // 1. Get base total price from Reservation table (same as Reservation_Confirmed and Checkout)
                decimal baseTotalPrice = 0;
                var priceParams = new Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };
                DataTable dtReservationPrice = code2.DatabaseQuerySafe(conn,
                    @"SELECT ISNULL(TotalPrice, 0) as TotalPrice
                      FROM Reservation
                      WHERE ID = @reservationId",
                    priceParams);
                if (dtReservationPrice.Rows.Count > 0 && dtReservationPrice.Rows[0]["TotalPrice"] != DBNull.Value)
                {
                    baseTotalPrice = Convert.ToDecimal(dtReservationPrice.Rows[0]["TotalPrice"]);
                }

                // 2. Get product charges from Reservation_Product_Charges (same as Checkout page)
                decimal productCharges = 0;
                var chargesParams = new Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };
                DataTable dtProductCharges2 = code2.DatabaseQuerySafe(conn,
                    @"SELECT ISNULL(SUM(TotalAmount), 0) as TotalCharges
                      FROM Reservation_Product_Charges
                      WHERE Reservation_ID = @reservationId
                      AND Status <> 'CANCELLED'",
                    chargesParams);
                if (dtProductCharges2.Rows.Count > 0 && dtProductCharges2.Rows[0]["TotalCharges"] != DBNull.Value)
                {
                    productCharges = Convert.ToDecimal(dtProductCharges2.Rows[0]["TotalCharges"]);
                }

                // 3. Calculate total price with charges
                decimal totalPrice = baseTotalPrice + productCharges;

                // 🔧 FIX: Update TotalPrice column to show calculated value (includes ProductCharges)
                dtReservation.Rows[i]["TotalPrice"] = totalPrice;
                dtReservation.Rows[i]["TotalPriceWithCharges"] = totalPrice.ToString("N0");

                // 4. Get total paid from Payment_History (same as Checkout page)
                decimal totalPaid = 0;
                var paidParams = new Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };
                DataTable dtPaid = code2.DatabaseQuerySafe(conn,
                    @"SELECT ISNULL(SUM(PaymentAmount), 0) as TotalPaid
                      FROM Payment_History
                      WHERE Reservation_ID = @reservationId
                      AND Status = 'COMPLETED'",
                    paidParams);
                if (dtPaid.Rows.Count > 0 && dtPaid.Rows[0]["TotalPaid"] != DBNull.Value)
                {
                    totalPaid = Convert.ToDecimal(dtPaid.Rows[0]["TotalPaid"]);
                }

                // 6. If no payment history, fallback to Deposit column (same as Checkout page)
                if (totalPaid == 0)
                {
                    var depositParams = new Dictionary<string, object>
                    {
                        { "@reservationId", reservationId }
                    };
                    DataTable dtDeposit = code2.DatabaseQuerySafe(conn,
                        @"SELECT ISNULL(Deposit, 0) as Deposit
                          FROM Reservation
                          WHERE ID = @reservationId",
                        depositParams);
                    if (dtDeposit.Rows.Count > 0 && dtDeposit.Rows[0]["Deposit"] != DBNull.Value)
                    {
                        totalPaid = Convert.ToDecimal(dtDeposit.Rows[0]["Deposit"]);
                    }
                }

                // 7. Calculate remaining balance
                decimal remainingBalance = totalPrice - totalPaid;

                // 🔧 FIX: Update Deposit column to show actual total paid from Payment_History
                dtReservation.Rows[i]["Deposit"] = totalPaid;
                dtReservation.Rows[i]["Remain"] = remainingBalance.ToString("N0");

                // Get reservation count (include checked-in, checked-out, and completed reservations)
                string mobilePhone = dtReservation.Rows[i]["Customer_MobilePhone"].ToString();
                DataTable dtCount = DatabaseQuery(conn,
                    "SELECT COUNT([Customer_MobilePhone]) as CountReserved FROM [Reservation] WHERE Customer_MobilePhone = @MobilePhone AND Status IN (N'เช็คอินแล้ว', N'เช็คเอาท์แล้ว', N'เสร็จสิ้น')",
                    new SqlParameter("@MobilePhone", mobilePhone));

                dtReservation.Rows[i]["CountReserved"] = dtCount.Rows[0]["CountReserved"].ToString();
            }

            // 🔧 FIX: Sort directly on DefaultView instead of creating new table with ToTable()
            // This ensures all modified values (Deposit, TotalPrice, Remain) are preserved
            dtReservation.DefaultView.Sort = "Order ASC";
            Session["dtShow"] = dtReservation;

            GridView1.DataSource = dtReservation;
            GridView1.DataBind();

            // Update button states
            foreach (GridViewRow row in GridView1.Rows)
            {
                if (row.RowType == DataControlRowType.DataRow)
                {
                    // 🔧 FIX: Get Reservation ID from DataKeys to find correct row after sorting
                    int reservationId = Convert.ToInt32(GridView1.DataKeys[row.RowIndex].Value);

                    // Find the correct row in dtReservation by ID
                    DataRow[] foundRows = dtReservation.Select($"ID = {reservationId}");
                    if (foundRows.Length == 0) continue;

                    DataRow currentRow = foundRows[0];

                    // Update button text for reservation count
                    Button bt6 = row.FindControl("Button6") as Button;
                    if (bt6 != null)
                    {
                        string countReserved = currentRow["CountReserved"].ToString();
                        bt6.Text = countReserved + " ครั้ง";
                    }

                    // 🔒 Check status and update buttons
                    string status = currentRow["Status"].ToString();
                    bool isOwner = Session["User"]?.ToString() == "Owner";

                    if (status == "เสร็จสิ้น")
                    {
                        // Hide all buttons except "รายละเอียด" (Button7)
                        Button btnCheckin = row.FindControl("Button1") as Button;
                        Button btnEdit = row.FindControl("Button2") as Button;
                        Button btnCancelNoRefund = row.FindControl("Button3") as Button;
                        Button btnCancelRefund = row.FindControl("Button4") as Button;
                        Button btnPayMore = row.FindControl("Button8") as Button;
                        Button btnCheckout = row.FindControl("btnCheckout") as Button;

                        if (btnCheckin != null) btnCheckin.Visible = false;
                        if (btnEdit != null) btnEdit.Visible = false;
                        if (btnCancelNoRefund != null) btnCancelNoRefund.Visible = false;
                        if (btnCancelRefund != null) btnCancelRefund.Visible = false;
                        if (btnPayMore != null) btnPayMore.Visible = false;
                        if (btnCheckout != null) btnCheckout.Visible = false;

                        // Button7 (รายละเอียด) and Button6 (ประวัติ) remain visible
                    }
                    else if (status == "เช็คอินแล้ว")
                    {
                        // ✅ สำหรับการจองที่เช็คอินแล้ว - อนุญาตให้แก้ไขได้ทุกคน
                        SetButtonEnabled(row, "Button1", false); // Check-in - ไม่สามารถเช็คอินซ้ำได้
                        // Button2 (แก้ไข) - เปิดให้แก้ไขได้ (ใช้แทนปุ่มเช่าเพิ่ม)
                        SetButtonEnabled(row, "Button3", false); // Cancel no refund - ห้ามยกเลิกหลังเช็คอิน
                        SetButtonEnabled(row, "Button4", false); // Cancel with refund - ห้ามยกเลิกหลังเช็คอิน
                        // Button8 (จ่ายเพิ่ม), btnCheckout (เช็คเอาท์), Button7 (รายละเอียด) ยังใช้งานได้

                        // Owner permissions - สามารถยกเลิกได้แม้หลังเช็คอิน
                        if (isOwner)
                        {
                            SetButtonEnabled(row, "Button3", true);  // Cancel no refund
                            SetButtonEnabled(row, "Button4", true);  // Cancel with refund
                        }
                    }
                    else if (status == "เช็คเอ้าท์แล้ว")
                    {
                        // 🔧 สำหรับการจองที่เช็คเอาท์แล้ว
                        // ทุกคน: เห็นเฉพาะ รายละเอียด และ ประวัติ (ห้ามแก้ไข เพิ่ม ยกเลิก)
                        SetButtonEnabled(row, "Button1", false); // Check-in
                        SetButtonEnabled(row, "Button2", false); // Edit
                        SetButtonEnabled(row, "Button3", false); // Cancel no refund
                        SetButtonEnabled(row, "Button4", false); // Cancel with refund
                        SetButtonEnabled(row, "Button8", false); // Pay more
                        SetButtonEnabled(row, "btnCheckout", false); // Checkout

                        // Button7 (รายละเอียด) และ Button6 (ประวัติ) ยังใช้งานได้สำหรับทุกคน
                    }
                    // สถานะอื่นๆ (มัดจำแล้ว, รอเช็คอิน ฯลฯ): ปุ่มทั้งหมดเปิดใช้งานตามปกติ
                }
            }
        }

        private void SetButtonEnabled(GridViewRow row, string buttonId, bool enabled)
        {
            Button button = row.FindControl(buttonId) as Button;
            if (button != null)
            {
                button.Enabled = enabled;
                if (!enabled)
                {
                    button.CssClass = button.CssClass.Replace("btn-success", "btn-outline-success")
                                                     .Replace("btn-warning", "btn-outline-warning")
                                                     .Replace("btn-danger", "btn-outline-danger")
                                                     .Replace("btn-secondary", "btn-outline-secondary");
                }
            }
        }

        public int DatabaseInsert(string connStr, string cmd, params SqlParameter[] parameters)
        {
            int ID = 0;
            using (SqlConnection connection = new SqlConnection(connStr))
            {
                using (SqlCommand command = new SqlCommand(cmd, connection))
                {
                    cmd = cmd.Replace("&nbsp;", "");
                    command.Parameters.AddRange(parameters);
                    connection.Open();
                    try
                    {
                        object result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            ID = Convert.ToInt32(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log exception if needed
                    }
                    connection.Close();
                }
            }
            return ID;
        }

        public DataTable DatabaseQuery(string connStr, string cmd, params SqlParameter[] parameters)
        {
            DataTable dt = new DataTable();
            using (SqlConnection con = new SqlConnection(connStr))
            {
                try
                {
                    cmd = cmd.Replace("&amp;", "&")
                            .Replace("&#39;", "''")
                            .Replace("&nbsp;", "");

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd, con);
                    adapter.SelectCommand.Parameters.AddRange(parameters);
                    adapter.Fill(dt);
                }
                catch (Exception ex)
                {
                    // Log exception if needed
                }
            }
            return dt;
        }

        protected void Calendar1_DayRender(object sender, DayRenderEventArgs e)
        {
            DataTable dtReservation = DatabaseQuery(conn,
                @"SELECT * FROM Reservation 
                  RIGHT JOIN Reservation_Accommodation ON Reservation.ID = Reservation_Accommodation.Reservation_ID 
                  WHERE @SelectedDate >= CheckinDate AND @SelectedDate < CheckoutDate",
                new SqlParameter("@SelectedDate", e.Day.Date.ToString("yyyy-MM-dd")));

            DataTable dtAccommodation = DatabaseQuery(conn, "SELECT * FROM Accommodation WHERE Status = 1");
            int maxAccommodation = dtAccommodation.Rows.Count;

            for (int j = 0; j < dtAccommodation.Rows.Count; j++)
            {
                for (int i = 0; i < dtReservation.Rows.Count; i++)
                {
                    if (dtReservation.Rows[i]["Accommodation_ID"].ToString() == dtAccommodation.Rows[j]["ID"].ToString() ||
                        dtAccommodation.Rows[j]["LimitWithPeople"].ToString() == "True")
                    {
                        dtAccommodation.Rows.RemoveAt(j);
                        dtAccommodation.AcceptChanges();
                        i = dtReservation.Rows.Count + 1;
                        j = -1;
                        break;
                    }
                }
            }

            if (dtAccommodation.Rows.Count == 0)
            {
                e.Cell.ForeColor = System.Drawing.Color.Red;
                e.Cell.Font.Bold = true;
            }
            else if (dtAccommodation.Rows.Count == maxAccommodation)
            {
                e.Cell.ForeColor = System.Drawing.Color.DarkGreen;
            }
        }

        protected async void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            DataTable dtShow = (DataTable)Session["dtShow"];
            if (dtShow == null) return;

            string commandArg = e.CommandArgument?.ToString();
            if (string.IsNullOrEmpty(commandArg)) return;

            switch (e.CommandName)
            {
                case "Checkin":
                case "EditReservation":
                case "CancelNoRefund":
                case "CancelRefund":
                case "CountReserved":
                case "PayMore":
                case "Checkout":
                    // สำหรับคำสั่งเหล่านี้ใช้ ID การจอง
                    DataTable dtCustomer = DatabaseQuery(conn,
                        @"SELECT Customer.MobilePhone FROM [Reservation]
                          INNER JOIN Customer ON Customer.MobilePhone = Reservation.Customer_MobilePhone
                          WHERE Reservation.ID = @ReservationId",
                        new SqlParameter("@ReservationId", commandArg));

                    if (dtCustomer.Rows.Count == 0) return;

                    string customerPhone = dtCustomer.Rows[0]["MobilePhone"].ToString();

                    if (e.CommandName == "Checkin")
                    {
                        Response.Redirect($"./Reserve?command=checkin&date={Calendar1.SelectedDate:yyyy-MM-dd}&id={commandArg}&check={customerPhone}", false);
                    }
                    else if (e.CommandName == "EditReservation")
                    {
                        Response.Redirect($"./Reserve?command=edit&date={Calendar1.SelectedDate:yyyy-MM-dd}&id={commandArg}&check={customerPhone}", false);
                    }
                    else if (e.CommandName == "PayMore")
                    {
                        Response.Redirect($"./Payment/MakePayment?id={commandArg}", false);
                    }
                    else if (e.CommandName == "Checkout")
                    {
                        Response.Redirect($"./Checkout?id={commandArg}", false);
                    }
                    else if (e.CommandName == "CancelNoRefund")
                    {
                        await CancelReservation(commandArg, false);
                        Response.Redirect("./ReserveTable", false);
                    }
                    else if (e.CommandName == "CancelRefund")
                    {
                        await CancelReservation(commandArg, true);
                        Response.Redirect("./ReserveTable", false);
                    }
                    else if (e.CommandName == "CountReserved")
                    {
                        Response.Redirect($"./CountReserved?telnum={customerPhone}", false);
                    }
                    break;

                case "Detail":
                    // 🔧 FIX: ใช้ Reservation ID โดยตรงแทน DataItemIndex
                    // เพราะหลังจาก sort แล้ว index ไม่ตรงกับ original DataTable
                    string detailReservationId = commandArg;

                    // Get customer phone from database using reservation ID
                    DataTable dtDetailCustomer = DatabaseQuery(conn,
                        @"SELECT Customer_MobilePhone FROM [Reservation] WHERE ID = @ReservationId",
                        new SqlParameter("@ReservationId", detailReservationId));

                    if (dtDetailCustomer.Rows.Count > 0)
                    {
                        string detailCustomerPhone = dtDetailCustomer.Rows[0]["Customer_MobilePhone"].ToString();
                        Response.Redirect($"https://taketimebangphra.com/Reservation_Confirmed?id={detailReservationId}&check={detailCustomerPhone}", false);
                    }
                    break;
            }

            Context.ApplicationInstance.CompleteRequest();
        }

        private async Task CancelReservation(string reservationId, bool refund)
        {
            string status = refund ? "ยกเลิกคืนเงิน" : "ยกเลิกไม่คืนเงิน";

            // 🔧 FIX: Cancel Payment_History records first
            try
            {
                string cancelNote = refund ? $"ยกเลิกจากการยกเลิกการจอง (คืนเงิน) ID: {reservationId}" :
                                            $"ยกเลิกจากการยกเลิกการจอง (ไม่คืนเงิน) ID: {reservationId}";

                DatabaseInsert(conn,
                    @"UPDATE [dbo].[Payment_History]
                      SET Status = 'CANCELLED', Notes = @Notes
                      WHERE Reservation_ID = @ReservationId AND Status = 'COMPLETED'",
                    new SqlParameter("@Notes", cancelNote),
                    new SqlParameter("@ReservationId", reservationId));

                System.Diagnostics.Debug.WriteLine($"✅ Cancelled Payment_History for Reservation {reservationId}");
            }
            catch (Exception phEx)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Failed to cancel Payment_History: {phEx.Message}");
                // Continue - this is non-critical
            }

            // Update reservation status
            string updateCmd = refund ?
                "UPDATE [dbo].[Reservation] SET TotalPrice = 0, Deposit = 0, [Status] = @Status WHERE ID = @ReservationId" :
                "UPDATE [dbo].[Reservation] SET TotalPrice = 0, [Status] = @Status WHERE ID = @ReservationId";

            DatabaseInsert(conn, updateCmd,
                new SqlParameter("@Status", status),
                new SqlParameter("@ReservationId", reservationId));

            // Send Telegram notification
            await SendTelegramNotification(reservationId, refund);

            // Delete related records
            DatabaseInsert(conn, "DELETE FROM [dbo].[Reservation_Accommodation] WHERE Reservation_ID = @ReservationId",
                new SqlParameter("@ReservationId", reservationId));

            DatabaseInsert(conn, "DELETE FROM [dbo].[Reservation_Items] WHERE Reservation_ID = @ReservationId",
                new SqlParameter("@ReservationId", reservationId));

            if (refund)
            {
                ProcessRefund(reservationId);
            }

            // Sync cancellation to accounting system
            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString;
                if (!string.IsNullOrEmpty(connStr))
                {
                    // Get deposit amount before it was zeroed
                    decimal depositAmount = 0;
                    string customerName = "ลูกค้า";
                    string paymentMethod = "CASH";

                    DataTable resData = DatabaseQuery(conn,
                        @"SELECT ISNULL(ph.TotalPaid, 0) AS DepositPaid,
                                 ISNULL(c.Customer_Name, c.NickName) AS Name,
                                 ISNULL(ph.PaymentMethod, 'CASH') AS PayMethod
                          FROM Reservation r
                          LEFT JOIN Customer c ON r.Customer_MobilePhone = c.Customer_MobilePhone
                          LEFT JOIN (SELECT Reservation_ID, SUM(PaymentAmount) AS TotalPaid,
                                     MAX(PaymentMethod) AS PaymentMethod
                                     FROM Payment_History WHERE Status = 'CANCELLED'
                                     GROUP BY Reservation_ID) ph ON ph.Reservation_ID = r.ID
                          WHERE r.ID = @id",
                        new SqlParameter("@id", reservationId));

                    if (resData?.Rows.Count > 0)
                    {
                        depositAmount = resData.Rows[0]["DepositPaid"] != DBNull.Value ? Convert.ToDecimal(resData.Rows[0]["DepositPaid"]) : 0;
                        customerName = resData.Rows[0]["Name"]?.ToString() ?? "ลูกค้า";
                        paymentMethod = resData.Rows[0]["PayMethod"]?.ToString() ?? "CASH";
                    }

                    // Accounting sync disabled — ใช้ manual sync จากหน้าจัดการเอกสารแทน
                }
            }
            catch (Exception accEx)
            {
                System.Diagnostics.Debug.WriteLine($"Accounting sync error: {accEx.Message}");
            }
        }

        private async Task SendTelegramNotification(string reservationId, bool refund)
        {
            try
            {
                DataTable dt = DatabaseQuery(conn,
                    @"SELECT r.ID, r.Customer_MobilePhone, c.Name, c.NickName,
                      ra.Accommodation_ID, a.AccomName, r.CheckinDate, r.CheckoutDate,
                      r.StayDays, r.TotalPrice,
                      ISNULL((SELECT SUM(PaymentAmount)
                              FROM Payment_History
                              WHERE Reservation_ID = r.ID AND Status = 'COMPLETED'), 0) AS TotalPaid
                      FROM [Reservation] r
                      INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                      INNER JOIN Accommodation a ON a.ID = ra.Accommodation_ID
                      INNER JOIN Customer c ON c.MobilePhone = r.Customer_MobilePhone
                      WHERE r.ID = @ReservationId",
                    new SqlParameter("@ReservationId", reservationId));

                if (dt.Rows.Count > 0)
                {
                    string customerName = $"{dt.Rows[0]["Name"]} ({dt.Rows[0]["NickName"]})";
                    string phone = dt.Rows[0]["Customer_MobilePhone"].ToString();
                    DateTime checkinDate = Convert.ToDateTime(dt.Rows[0]["CheckinDate"]);
                    DateTime checkoutDate = Convert.ToDateTime(dt.Rows[0]["CheckoutDate"]);
                    int stayDays = Convert.ToInt32(dt.Rows[0]["StayDays"]);
                    decimal totalPrice = Convert.ToDecimal(dt.Rows[0]["TotalPrice"]);
                    decimal deposit = Convert.ToDecimal(dt.Rows[0]["TotalPaid"]);

                    StringBuilder roomDetails = new StringBuilder();
                    foreach (DataRow row in dt.Rows)
                    {
                        roomDetails.AppendLine($"   • {row["AccomName"]}");
                    }

                    string message = refund ?
                        $@"✅ *ยกเลิกการจอง (คืนเงิน)*

📋 *รายละเอียดการจอง:*
   🆔 หมายเลขการจอง: {reservationId}
   👤 ลูกค้า: {customerName}
   📞 โทรศัพท์: {phone}
   📅 เช็คอิน: {checkinDate:dd MMMM yyyy}
   📅 เช็คเอ้าท์: {checkoutDate:dd MMMM yyyy}
   🕐 จำนวนคืน: {stayDays} คืน
   💰 ราคารวม: {totalPrice:N0} บาท
   💵 มัดจำที่คืน: {deposit:N0} บาท

🏨 *ห้องพักที่ยกเลิก:*
{roomDetails}

💸 *หมายเหตุ:* คืนเงินมัดจำให้ลูกค้าแล้ว" :
                        $@"❌ *ยกเลิกการจอง (ไม่คืนเงิน)*

📋 *รายละเอียดการจอง:*
   🆔 หมายเลขการจอง: {reservationId}
   👤 ลูกค้า: {customerName}
   📞 โทรศัพท์: {phone}
   📅 เช็คอิน: {checkinDate:dd MMMM yyyy}
   📅 เช็คเอ้าท์: {checkoutDate:dd MMMM yyyy}
   🕐 จำนวนคืน: {stayDays} คืน
   💰 ราคารวม: {totalPrice:N0} บาท
   💵 มัดจำ: {deposit:N0} บาท

🏨 *ห้องพักที่ยกเลิก:*
{roomDetails}

⚠️ *หมายเหตุ:* ยกเลิกไม่คืนเงิน";

                    var bot = new TelegramBot2(ConfigurationManager.AppSettings["TelegramTokenTakeTime"]);
                    await bot.SendMessageAsync("-4969611371", message);
                }
            }
            catch (Exception ex)
            {
                // Log error if needed
            }
        }

        private void ProcessRefund(string reservationId)
        {
            string path = ConfigurationManager.AppSettings["ReceiptFolderPath"];
            string Imagespath = ConfigurationManager.AppSettings["ImagesFolderPath"];
            DataTable dtRec = DatabaseQuery(conn,
                "SELECT * FROM Account_Receipt WHERE Status = 'Normal' AND Reservation_ID = @ReservationId",
                new SqlParameter("@ReservationId", reservationId));

            for (int i = 0; i < dtRec.Rows.Count; i++)
            {
                string receiptId = dtRec.Rows[i]["ID"].ToString();

                // ✅ 1. Delete Payment_History records for this receipt
                DatabaseInsert(conn,
                    "DELETE FROM [dbo].[Payment_History] WHERE Receipt_ID = @ReceiptId",
                    new SqlParameter("@ReceiptId", receiptId));

                // ✅ 2. Update receipt status to Cancel
                DatabaseInsert(conn,
                    "UPDATE [dbo].[Account_Receipt] SET [Status] = 'Cancel' WHERE ID = @ReceiptId",
                    new SqlParameter("@ReceiptId", receiptId));

                // ✅ 3. Void ในระบบบัญชี
                try
                {
                    var sync = new Integration.AccountingSyncService(conn);
                    sync.EnqueueVoidReceipt(receiptId);
                }
                catch { }

                // ✅ 4. Stamp "Cancel" on PDF
                string uid = dtRec.Rows[i]["UID"].ToString();
                DateTime createdDate = Convert.ToDateTime(dtRec.Rows[i]["Created_Date"]);

                ProcessReceiptFile(path, Imagespath, receiptId, uid, createdDate);
            }
        }

        private void ProcessReceiptFile(string path, string imagesPath, string receiptId, string uid, DateTime createdDate)
        {
            string inputPdfPath = GetFilePath(path, createdDate, receiptId, uid, "");
            string outputPdfPath = GetFilePath(path, createdDate, receiptId, uid, "_Cancel");

            if (File.Exists(inputPdfPath))
            {
                try
                {
                    using (Stream inputPdfStream = new FileStream(inputPdfPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (Stream inputImageStream = new FileStream(Path.Combine(imagesPath, "Cancel.png"), FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (Stream outputPdfStream = new FileStream(outputPdfPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var reader = new PdfReader(inputPdfStream);
                        var stamper = new PdfStamper(reader, outputPdfStream);
                        var pdfContentByte = stamper.GetOverContent(1);

                        iTextSharp.text.Image image = iTextSharp.text.Image.GetInstance(inputImageStream);
                        image.SetAbsolutePosition(100, 100);
                        pdfContentByte.AddImage(image);
                        stamper.Close();
                    }
                    File.Delete(inputPdfPath);

                    // Try to delete e-tax file if exists
                    string etaxPath = inputPdfPath.Replace(".pdf", "_etax.pdf");
                    if (File.Exists(etaxPath))
                    {
                        File.Delete(etaxPath);
                    }
                }
                catch (Exception ex)
                {
                    // Log file processing error
                }
            }
        }

        private string GetFilePath(string basePath, DateTime date, string receiptId, string uid, string suffix)
        {
            string yearPath = Path.Combine(basePath, date.Year.ToString());
            string monthPath = Path.Combine(yearPath, date.Month.ToString("00"));

            // Create directory if it doesn't exist
            if (!Directory.Exists(monthPath))
            {
                Directory.CreateDirectory(monthPath);
            }

            string fileName = string.IsNullOrEmpty(uid) ?
                $"{receiptId}{suffix}.pdf" :
                $"{receiptId}_{uid}{suffix}.pdf";

            return Path.Combine(monthPath, fileName);
        }

        protected void btnPrint_Click(object sender, EventArgs e)
        {

        }
    }
}