using System;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Web.UI;
using System.Web.UI.WebControls;
using OfficeOpenXml;

namespace Take_Time_BangPhra.Payment
{
    public partial class PaymentHistory : System.Web.UI.Page
    {
        // เดิมอ่าน connection string ชื่อ "ATATB" ซึ่งไม่มีใน Web.config (หน้า NullReference ตั้งแต่สร้าง) → ใช้ตัวหลักของระบบ
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private PaymentDataAccess paymentDataAccess;
        private code codeInstance = new code();

        protected void Page_Load(object sender, EventArgs e)
        {
            // ข้อมูลการเงินของลูกค้า — ต้องล็อกอินและมีสิทธิ์โมดูลใบเสร็จ (เดิมไม่มีการกันเลย)
            if (!Perm.Guard(this, Perm.FinReceipt)) return;

            paymentDataAccess = new PaymentDataAccess(connectionString);
            btnTabOta.Visible = IsOwnerOrAdmin;

            // ข้อความผลลัพธ์แสดงครั้งเดียวต่อการกด (ไม่ค้างข้าม postback)
            pnlSuccess.Visible = false;
            pnlError.Visible = false;
            litOtaNotice.Text = "";

            if (!IsPostBack)
            {
                // Check if reservationId is passed via QueryString
                string reservationIdParam = Request.QueryString["reservationId"];
                if (!string.IsNullOrEmpty(reservationIdParam))
                {
                    // Auto-fill the reservation ID textbox
                    txtReservationID.Text = reservationIdParam;
                }

                LoadPaymentHistory();
                LoadSummary();

                if (string.Equals(Request.QueryString["view"], "ota", StringComparison.OrdinalIgnoreCase) && IsOwnerOrAdmin)
                    ShowOtaView();
                else
                    ShowMainView();
            }
        }

        private void LoadPaymentHistory()
        {
            try
            {
                var parameters = new System.Collections.Generic.Dictionary<string, object>();
                string whereClause = "WHERE 1=1";

                // Filter by Reservation ID
                if (!string.IsNullOrEmpty(txtReservationID.Text))
                {
                    whereClause += " AND ph.Reservation_ID = @reservationId";
                    parameters.Add("@reservationId", int.Parse(txtReservationID.Text));
                }

                // Filter by Phone
                if (!string.IsNullOrEmpty(txtPhoneNumber.Text))
                {
                    whereClause += " AND ph.PaidBy_CustomerPhone LIKE @phone";
                    parameters.Add("@phone", "%" + txtPhoneNumber.Text + "%");
                }

                // Filter by Date Range
                if (!string.IsNullOrEmpty(txtStartDate.Text))
                {
                    whereClause += " AND CAST(ph.PaymentDate AS DATE) >= @startDate";
                    parameters.Add("@startDate", DateTime.Parse(txtStartDate.Text));
                }

                if (!string.IsNullOrEmpty(txtEndDate.Text))
                {
                    whereClause += " AND CAST(ph.PaymentDate AS DATE) <= @endDate";
                    parameters.Add("@endDate", DateTime.Parse(txtEndDate.Text));
                }

                // Filter by Status
                if (!string.IsNullOrEmpty(ddlStatus.SelectedValue))
                {
                    whereClause += " AND ph.Status = @status";
                    parameters.Add("@status", ddlStatus.SelectedValue);
                }

                string query = $@"
                    SELECT
                        ph.ID,
                        ph.Reservation_ID,
                        ph.PaymentDate,
                        ph.PaymentAmount,
                        ph.PaymentType,
                        ph.PaymentMethod,
                        ph.Receipt_ID,
                        ph.PaymentSlip_ID,
                        ph.RemainingBalance,
                        ph.PaidBy_CustomerPhone AS Customer_MobilePhone,
                        ph.Status
                    FROM Payment_History ph
                    {whereClause}
                    ORDER BY ph.PaymentDate DESC";

                DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, parameters);

                if (dt.Rows.Count > 0)
                {
                    gvPaymentHistory.DataSource = dt;
                    gvPaymentHistory.DataBind();
                    pnlEmpty.Visible = false;
                }
                else
                {
                    gvPaymentHistory.DataSource = null;
                    gvPaymentHistory.DataBind();
                    pnlEmpty.Visible = true;
                }
            }
            catch (Exception ex)
            {
                // Handle error - possibly Payment_History table doesn't exist yet
                pnlEmpty.Visible = true;
                gvPaymentHistory.DataSource = null;
                gvPaymentHistory.DataBind();
            }
        }

        private void LoadSummary()
        {
            try
            {
                var parameters = new System.Collections.Generic.Dictionary<string, object>();

                string query = @"
                    SELECT
                        COUNT(*) AS TotalRecords,
                        ISNULL(SUM(PaymentAmount), 0) AS TotalAmount,
                        SUM(CASE WHEN Status = 'PENDING' THEN 1 ELSE 0 END) AS PendingCount,
                        SUM(CASE WHEN Status = 'COMPLETED' THEN 1 ELSE 0 END) AS CompletedCount
                    FROM Payment_History";

                DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    lblTotalRecords.Text = row["TotalRecords"].ToString();
                    lblTotalAmount.Text = Convert.ToDecimal(row["TotalAmount"]).ToString("N2");
                    lblPendingRecords.Text = row["PendingCount"].ToString();
                    lblCompletedRecords.Text = row["CompletedCount"].ToString();
                }
            }
            catch
            {
                // If table doesn't exist, show zeros
                lblTotalRecords.Text = "0";
                lblTotalAmount.Text = "0.00";
                lblPendingRecords.Text = "0";
                lblCompletedRecords.Text = "0";
            }
        }

        protected void btnFilter_Click(object sender, EventArgs e)
        {
            LoadPaymentHistory();
            LoadSummary();
        }

        protected void btnExport_Click(object sender, EventArgs e)
        {
            try
            {
                // Build query with filters
                var parameters = new System.Collections.Generic.Dictionary<string, object>();
                string whereClause = "WHERE 1=1";

                if (!string.IsNullOrEmpty(txtReservationID.Text))
                {
                    whereClause += " AND ph.Reservation_ID = @reservationId";
                    parameters.Add("@reservationId", int.Parse(txtReservationID.Text));
                }

                if (!string.IsNullOrEmpty(txtPhoneNumber.Text))
                {
                    whereClause += " AND ph.PaidBy_CustomerPhone LIKE @phone";
                    parameters.Add("@phone", "%" + txtPhoneNumber.Text + "%");
                }

                if (!string.IsNullOrEmpty(txtStartDate.Text))
                {
                    whereClause += " AND CAST(ph.PaymentDate AS DATE) >= @startDate";
                    parameters.Add("@startDate", DateTime.Parse(txtStartDate.Text));
                }

                if (!string.IsNullOrEmpty(txtEndDate.Text))
                {
                    whereClause += " AND CAST(ph.PaymentDate AS DATE) <= @endDate";
                    parameters.Add("@endDate", DateTime.Parse(txtEndDate.Text));
                }

                if (!string.IsNullOrEmpty(ddlStatus.SelectedValue))
                {
                    whereClause += " AND ph.Status = @status";
                    parameters.Add("@status", ddlStatus.SelectedValue);
                }

                string query = $@"
                    SELECT
                        ph.ID AS [รหัส],
                        ph.Reservation_ID AS [เลขที่การจอง],
                        ph.PaymentDate AS [วันที่ชำระ],
                        ph.PaymentAmount AS [จำนวนเงิน],
                        ph.PaymentType AS [ประเภท],
                        ph.PaymentMethod AS [ช่องทาง],
                        ph.Receipt_ID AS [เลขที่ใบเสร็จ],
                        ph.RemainingBalance AS [ยอดคงเหลือ],
                        ph.PaidBy_CustomerPhone AS [เบอร์ลูกค้า],
                        CASE ph.Status
                            WHEN 'COMPLETED' THEN N'สำเร็จ'
                            WHEN 'PENDING' THEN N'รอดำเนินการ'
                            WHEN 'CANCELLED' THEN N'ยกเลิก'
                            WHEN 'REFUNDED' THEN N'คืนเงินแล้ว'
                            WHEN 'OTA_RECLASS' THEN N'OTA เก็บ (ไม่ใช่เงินสดรับ)'
                            ELSE ph.Status
                        END AS [สถานะ]
                    FROM Payment_History ph
                    {whereClause}
                    ORDER BY ph.PaymentDate DESC";

                DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, parameters);

                if (dt.Rows.Count == 0)
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "alert", "alert('ไม่พบข้อมูลสำหรับส่งออก');", true);
                    return;
                }

                // Export using EPPlus
                using (var package = new OfficeOpenXml.ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("ประวัติการชำระเงิน");

                    // Add title
                    worksheet.Cells[1, 1].Value = "รายงานประวัติการชำระเงิน";
                    worksheet.Cells[1, 1, 1, 10].Merge = true;
                    worksheet.Cells[1, 1].Style.Font.Size = 16;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    // Add export date
                    worksheet.Cells[2, 1].Value = $"ส่งออกเมื่อ: {DateTime.Now:dd/MM/yyyy HH:mm}";
                    worksheet.Cells[2, 1, 2, 10].Merge = true;

                    // Load data starting from row 4
                    worksheet.Cells[4, 1].LoadFromDataTable(dt, true);

                    // Format header row
                    using (var headerRange = worksheet.Cells[4, 1, 4, dt.Columns.Count])
                    {
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(79, 129, 189));
                        headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    }

                    // Format currency columns
                    worksheet.Column(4).Style.Numberformat.Format = "#,##0.00";
                    worksheet.Column(8).Style.Numberformat.Format = "#,##0.00";

                    // Format date column
                    worksheet.Column(3).Style.Numberformat.Format = "dd/mm/yyyy hh:mm";

                    // Auto-fit columns
                    worksheet.Cells.AutoFitColumns();

                    // Add borders
                    using (var dataRange = worksheet.Cells[4, 1, dt.Rows.Count + 4, dt.Columns.Count])
                    {
                        dataRange.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        dataRange.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        dataRange.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        dataRange.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    }

                    // Generate file
                    string fileName = $"PaymentHistory_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    byte[] fileBytes = package.GetAsByteArray();

                    Response.Clear();
                    Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    Response.AddHeader("Content-Disposition", $"attachment; filename={fileName}");
                    Response.BinaryWrite(fileBytes);
                    Response.End();
                }
            }
            catch (Exception ex)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "alert", $"alert('เกิดข้อผิดพลาด: {ex.Message.Replace("'", "\\'")}');", true);
            }
        }

        protected void gvPaymentHistory_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ViewSlip")
            {
                if (long.TryParse(e.CommandArgument.ToString(), out long slipId))
                {
                    // Redirect to slip viewer
                    string slipUrl = GetPaymentSlipUrl(slipId);
                    if (!string.IsNullOrEmpty(slipUrl))
                    {
                        Response.Redirect(slipUrl);
                    }
                }
            }
            else if (e.CommandName == "ViewReceipt")
            {
                string receiptId = e.CommandArgument.ToString();
                // Redirect to receipt viewer or generate PDF
                Response.Redirect($"~/ViewReceipt.aspx?id={receiptId}");
            }
        }

        private string GetPaymentSlipUrl(long slipId)
        {
            try
            {
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@slipId", slipId }
                };

                string query = "SELECT SlipFileURL FROM Payment_Slips WHERE ID = @slipId";
                DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, parameters);

                if (dt.Rows.Count > 0)
                {
                    return dt.Rows[0]["SlipFileURL"].ToString();
                }
            }
            catch
            {
                // Handle error
            }

            return null;
        }

        protected string GetStatusClass(string status)
        {
            switch (status)
            {
                case "COMPLETED":
                    return "badge-success";
                case "PENDING":
                    return "badge-warning";
                case "CANCELLED":
                    return "badge-danger";
                default:
                    return "badge-info";
            }
        }

        protected string GetStatusText(string status)
        {
            switch (status)
            {
                case "COMPLETED":
                    return "สำเร็จ";
                case "PENDING":
                    return "รอดำเนินการ";
                case "CANCELLED":
                    return "ยกเลิก";
                case "REFUNDED":
                    return "คืนเงินแล้ว";
                case OtaReclassStatus:
                    return "OTA เก็บ (ไม่ใช่เงินสดรับ)";
                default:
                    return status;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // ⚠ เงินสดของใบ OTA ที่ควรตรวจ
        //
        // หน้าเช็คอินรุ่นเก่าบังคับให้บันทึกเงินค่าห้องของใบ OTA แบบ Channel Collect (OTA เก็บเงินไปแล้ว)
        // เป็นแถว Payment_History COMPLETED (ค่าเริ่มต้น "เงินสด") — บางแถวไม่มีใบเสร็จ, บางแถวออกใบเสร็จ
        // ที่ sync เข้า NextAcc เป็นเงินสดรับ ⇒ รายงานเงินสดเกินจริง และแถวนี้ "กลบ" ยอดอัปเกรดทีหลัง
        //
        // หลักการ: ไม่ล้างอัตโนมัติ — หน้านี้แค่รวบรายการที่น่าสงสัย ติ๊กล่วงหน้าเฉพาะที่ชัดมาก
        // และไม่มีอะไรเปลี่ยนจนผู้จัดการ/เจ้าของกดยืนยันเอง (Owner/Admin เท่านั้น)
        //   · ยืนยันว่าเป็นเงิน OTA (แถวไม่มีใบเสร็จ) → Status = OTA_RECLASS (รายงานทุกตัวกรอง COMPLETED จึงหลุดออก)
        //   · ย้อนกลับ → Status = COMPLETED
        //   · แถวมีใบเสร็จที่ sync แล้ว → ไม่ void ใบเสร็จ แต่ส่ง JE Dr ลูกหนี้ OTA / Cr เงินสด (คิว OTA_CASH_RECLASS)
        //   · เงินสดรับจริง → เปลี่ยนการจองเป็น "เก็บเงินหน้างาน" (แถวเงินคง COMPLETED)
        // ═══════════════════════════════════════════════════════════════════

        private const string OtaReclassStatus = "OTA_RECLASS";

        private string CurrentRole { get { return Session["User"]?.ToString() ?? ""; } }

        private bool IsOwner { get { return string.Equals(CurrentRole, "Owner", StringComparison.OrdinalIgnoreCase); } }

        private bool IsOwnerOrAdmin
        {
            get
            {
                return Session["permission"]?.ToString() == "True"
                    && (IsOwner || string.Equals(CurrentRole, "Admin", StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>ส่ง JE เข้าบัญชี = เจ้าของ หรือผู้ดูแลที่มีสิทธิ์ "ตั้งค่าบัญชี & ภาษี" (ฝ่ายบัญชี)</summary>
        private bool CanPostOtaJournal
        {
            get { return IsOwnerOrAdmin && (IsOwner || Perm.CanAccess(Perm.SysAccounting)); }
        }

        private string CurrentUserName
        {
            get
            {
                string u = Session["UserName"]?.ToString();
                if (string.IsNullOrWhiteSpace(u)) u = CurrentRole;
                return string.IsNullOrWhiteSpace(u) ? "?" : u.Trim();
            }
        }

        private bool OtaDoneView { get { return ddlOtaMode.SelectedValue == "DONE"; } }

        protected void btnTabMain_Click(object sender, EventArgs e)
        {
            LoadPaymentHistory();
            LoadSummary();
            ShowMainView();
        }

        protected void btnTabOta_Click(object sender, EventArgs e)
        {
            if (!RequireOtaAccess()) return;
            ShowOtaView();
        }

        protected void ddlOtaMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!RequireOtaAccess()) return;
            ShowOtaView();
        }

        protected void btnOtaRefresh_Click(object sender, EventArgs e)
        {
            if (!RequireOtaAccess()) return;
            ShowOtaView();
        }

        private void ShowMainView()
        {
            pnlMainView.Visible = true;
            pnlOtaView.Visible = false;
            btnTabMain.CssClass = "tab-btn active";
            btnTabOta.CssClass = "tab-btn";
        }

        private void ShowOtaView()
        {
            pnlMainView.Visible = false;
            pnlOtaView.Visible = true;
            btnTabMain.CssClass = "tab-btn";
            btnTabOta.CssClass = "tab-btn active";
            BindOtaGrid();
        }

        private bool RequireOtaAccess()
        {
            if (IsOwnerOrAdmin) return true;
            ShowMainView();
            ShowError("หน้านี้สำหรับเจ้าของ/ผู้ดูแล (Owner/Admin) เท่านั้น");
            return false;
        }

        private void ShowSuccess(string text)
        {
            pnlSuccess.Visible = true;
            lblSuccess.Text = Server.HtmlEncode(text).Replace("\n", "<br/>");
        }

        private void ShowError(string text)
        {
            pnlError.Visible = true;
            lblError.Text = Server.HtmlEncode(text).Replace("\n", "<br/>");
        }

        private void BindOtaGrid()
        {
            bool done = OtaDoneView;
            btnOtaConfirm.Visible = !done;
            btnOtaReclassJe.Visible = !done && CanPostOtaJournal;
            btnOtaHotel.Visible = !done;
            btnOtaUndo.Visible = done;

            DataTable dt;
            try
            {
                dt = BuildOtaCandidates(done);
            }
            catch (Exception ex)
            {
                gvOta.DataSource = null;
                gvOta.DataBind();
                litOtaSummary.Text = "";
                ShowError("โหลดรายการไม่สำเร็จ: " + ex.Message);
                return;
            }

            DataView dv = dt.DefaultView;
            dv.Sort = "CheckedOut ASC, PaymentDate DESC";
            gvOta.DataSource = dv;
            gvOta.DataBind();

            int total = dt.Rows.Count, ticked = 0, withReceipt = 0, inHouse = 0;
            decimal sum = 0m;
            foreach (DataRow r in dt.Rows)
            {
                if ((bool)r["PreTick"]) ticked++;
                if ((bool)r["HasReceipt"]) withReceipt++;
                if (!(bool)r["CheckedOut"]) inHouse++;
                sum += (decimal)r["Amount"];
            }
            litOtaSummary.Text = done
                ? $"แถวที่ปรับเป็น \"OTA เก็บ\" แล้ว <b>{total:N0}</b> รายการ รวม <b>{sum:N2}</b> บาท — ติ๊กแถวที่ต้องการย้อนกลับ"
                : $"พบ <b>{total:N0}</b> รายการ รวม <b>{sum:N2}</b> บาท (ยังไม่เช็คเอาท์ {inHouse:N0} · มีใบเสร็จ {withReceipt:N0}) — " +
                  $"ติ๊กล่วงหน้า <b>{ticked:N0}</b> รายการที่เข้าเงื่อนไขครบทุกข้อ. ยังไม่มีอะไรเปลี่ยนจนกว่าจะกดปุ่มยืนยัน";
        }

        /// <summary>
        /// รายการที่ควรตรวจ (done=false) หรือที่ปรับแล้ว (done=true) — query สด ไม่ต้องมี migration
        ///   · Payment_History COMPLETED ของการจอง OTA ที่ OTA เก็บเงิน (หรือระบบเดาโหมดไม่ได้ = UNKNOWN)
        ///   · บันทึก ณ/หลังวันเช็คอิน และ (ยอด ≈ ยอด OTA หรือหมายเหตุขึ้นต้น "เช็คอิน")
        /// ติ๊กล่วงหน้าเฉพาะแถวที่เข้าครบ: ไม่มีใบเสร็จ + เงินสด + หมายเหตุ "เช็คอิน…" + ไม่มีสลิป +
        /// โหมดไม่ได้มาจากการเดา + |ยอด − ยอด OTA| ≤ RoundingTolerance
        /// </summary>
        private DataTable BuildOtaCandidates(bool done)
        {
            var result = new DataTable();
            result.Columns.Add("PhId", typeof(long));
            result.Columns.Add("ResId", typeof(int));
            result.Columns.Add("PaymentDate", typeof(DateTime));
            result.Columns.Add("Amount", typeof(decimal));
            result.Columns.Add("Method", typeof(string));
            result.Columns.Add("RecordedBy", typeof(string));
            result.Columns.Add("ReceiptId", typeof(string));
            result.Columns.Add("HasReceipt", typeof(bool));
            result.Columns.Add("ReceiptTotal", typeof(decimal));
            result.Columns.Add("ReceiptState", typeof(string));
            result.Columns.Add("ReceiptSynced", typeof(bool));
            result.Columns.Add("ReclassState", typeof(string));
            result.Columns.Add("Guest", typeof(string));
            result.Columns.Add("OtaChannel", typeof(string));
            result.Columns.Add("OtaBookingId", typeof(string));
            result.Columns.Add("OtaAmount", typeof(decimal));
            result.Columns.Add("Mode", typeof(string));
            result.Columns.Add("ModeSource", typeof(string));
            result.Columns.Add("ResStatus", typeof(string));
            result.Columns.Add("CheckedOut", typeof(bool));
            result.Columns.Add("Notes", typeof(string));
            result.Columns.Add("PreTick", typeof(bool));
            result.Columns.Add("Hint", typeof(string));

            string phStatus = done ? OtaReclassStatus : "COMPLETED";
            string afterCheckin = done ? "" : " AND CAST(ph.PaymentDate AS DATE) >= CAST(r.CheckinDate AS DATE)";
            var prm = new System.Collections.Generic.Dictionary<string, object> { { "@st", phStatus } };

            // คอลัมน์ OTA_* (PHASE18_12) อาจยังไม่มีในฐานข้อมูลเก่า → ลองแบบมีก่อน แล้วถอยเป็นแบบไม่มี
            string otaCols = @"r.OTA_Channel, r.OTA_Booking_ID,
                       COALESCE(NULLIF(LTRIM(RTRIM(r.OTA_Guest_Name)), N''), cu.FullName, cu.Name, r.Customer_MobilePhone) AS Guest,
                       CASE WHEN ISNULL((SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config
                                         WHERE ConfigKey = 'Email_Rsv_TotalSource'), 'AMOUNT') = 'REFSELL'
                                 AND ISNULL(r.OTA_Gross_Amount, 0) > 0 THEN r.OTA_Gross_Amount
                            WHEN ISNULL(r.OTA_Net_Amount, 0) > 0 THEN r.OTA_Net_Amount
                            WHEN ISNULL(r.OTA_Gross_Amount, 0) > 0 THEN r.OTA_Gross_Amount
                            ELSE NULL END AS SqlOtaAmount";
            string plainCols = @"CAST(NULL AS nvarchar(50)) AS OTA_Channel, CAST(NULL AS nvarchar(100)) AS OTA_Booking_ID,
                       COALESCE(cu.FullName, cu.Name, r.Customer_MobilePhone) AS Guest,
                       CAST(NULL AS decimal(18,2)) AS SqlOtaAmount";
            string sqlTemplate = @"
                SELECT TOP 5000
                       ph.ID AS PhId, ph.Reservation_ID AS ResId, ph.PaymentDate, ph.PaymentAmount,
                       ph.PaymentMethod, ph.Receipt_ID, ph.Notes,
                       CASE WHEN ph.PaymentSlip_ID IS NOT NULL THEN 1
                            WHEN (ph.Receipt_ID IS NULL OR LTRIM(RTRIM(ph.Receipt_ID)) = '')
                                 AND EXISTS (SELECT 1 FROM Payment_Slips ps
                                              WHERE ps.Reservation_ID = ph.Reservation_ID
                                                AND ps.Account_Receipt_ID IS NULL AND ps.IsActive = 1) THEN 1
                            ELSE 0 END AS HasSlip,
                       COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(a.FirstName, N'') + N' ' + ISNULL(a.LastName, N''))), N''), a.Username) AS RecordedBy,
                       ar.Total_Amount AS ReceiptTotal,
                       r.Status AS ResStatus,
                       {COLS}
                  FROM Payment_History ph
                  JOIN Reservation r ON r.ID = ph.Reservation_ID
                  LEFT JOIN [dbo].[Admin] a ON a.ID = ph.ProcessedBy_AdminID
                  LEFT JOIN Account_Receipt ar ON ar.ID = ph.Receipt_ID
                  OUTER APPLY (SELECT TOP 1 c.FullName, c.Name FROM Customer c
                                WHERE c.MobilePhone = r.Customer_MobilePhone) cu
                 WHERE ph.Status = @st
                   AND ph.PaymentAmount > 0
                   AND ISNULL(ph.PaymentType, '') <> 'REFUND'" + afterCheckin + @"
                   {OTAFILTER}
                 ORDER BY ph.PaymentDate DESC";

            // คัดเบื้องต้นเฉพาะการจองที่มีร่องรอย OTA (กว้างกว่าเงื่อนไขจริง — ตัดสินขั้นสุดท้ายด้วย ReservationBalance.IsOta)
            // เพื่อไม่ให้แถวเก็บเงินหน้างานปกติจำนวนมากดันรายการ OTA เก่าหลุดเพดาน TOP
            string otaFilter = @"AND (ISNULL(LTRIM(RTRIM(r.OTA_Channel)), N'') <> N''
                        OR ISNULL(LTRIM(RTRIM(r.OTA_Booking_ID)), N'') <> N''
                        OR ISNULL(LTRIM(RTRIM(r.OTA_Payment_Type)), N'') <> N''
                        OR r.Remark LIKE N'%Collect%')";
            string plainFilter = @"AND r.Remark LIKE N'%Collect%'";

            DataTable raw;
            try
            {
                raw = codeInstance.DatabaseQuerySafe(connectionString,
                    sqlTemplate.Replace("{COLS}", otaCols).Replace("{OTAFILTER}", done ? "" : otaFilter), prm);
            }
            catch
            {
                raw = codeInstance.DatabaseQuerySafe(connectionString,
                    sqlTemplate.Replace("{COLS}", plainCols).Replace("{OTAFILTER}", done ? "" : plainFilter), prm);
            }
            if (raw == null || raw.Rows.Count == 0) return result;

            // ยอดของการจอง + โหมดเก็บเงิน — สูตรกลางของระบบ (ReservationBalance)
            var ids = new System.Collections.Generic.HashSet<int>();
            foreach (DataRow r in raw.Rows)
                if (r["ResId"] != DBNull.Value) ids.Add(Convert.ToInt32(r["ResId"]));
            if (ids.Count == 0) return result;
            // id เป็นตัวเลขจากฐานข้อมูล (แปลงเป็น int แล้ว) — ปลอดภัยที่จะต่อเป็นรายการ IN
            string idList = string.Join(",", ids);
            var balances = ReservationBalance.LoadMany(connectionString, "r.ID IN (" + idList + ")", null);

            decimal tol = ReservationBalance.RoundingTolerance;
            Take_Time_BangPhra.Integration.AccountingSyncService sync = null;
            try { sync = new Take_Time_BangPhra.Integration.AccountingSyncService(connectionString); }
            catch { sync = null; }

            foreach (DataRow r in raw.Rows)
            {
                int resId = Convert.ToInt32(r["ResId"]);
                ReservationBalance b;
                if (!balances.TryGetValue(resId, out b) || b == null) continue;

                bool channel = b.CollectMode == ReservationBalance.ModeChannel;
                bool unknown = b.IsCollectUnknown || b.CollectMode == ReservationBalance.ModeUnknown;

                decimal amount = Convert.ToDecimal(r["PaymentAmount"]);
                string notes = r["Notes"] == DBNull.Value ? "" : r["Notes"].ToString();
                bool notesCheckin = notes.TrimStart().StartsWith("เช็คอิน", StringComparison.Ordinal);
                decimal ota = b.OtaAmount >= 0m ? b.OtaAmount
                    : (r["SqlOtaAmount"] != DBNull.Value ? Convert.ToDecimal(r["SqlOtaAmount"]) : -1m);
                bool amountMatch = ota > 0m && Math.Abs(amount - ota) <= tol;

                if (!done)
                {
                    // เฉพาะการจอง OTA ที่ OTA เก็บเงิน (หรือเดาไม่ได้) + ลักษณะของแถวเงินสดปลอมจากหน้าเช็คอิน
                    if (!b.IsOta) continue;
                    if (!channel && !unknown) continue;
                    if (!amountMatch && !notesCheckin) continue;
                }

                string receiptId = r["Receipt_ID"] == DBNull.Value ? "" : r["Receipt_ID"].ToString().Trim();
                bool hasReceipt = receiptId.Length > 0;
                string method = r["PaymentMethod"] == DBNull.Value ? "" : r["PaymentMethod"].ToString().Trim();
                bool isCash = IsCashMethod(method);
                bool hasSlip = r["HasSlip"] != DBNull.Value && Convert.ToInt32(r["HasSlip"]) == 1;
                string resStatus = r["ResStatus"] == DBNull.Value ? "" : r["ResStatus"].ToString();
                bool checkedOut = resStatus == "เช็คเอาท์แล้ว" || resStatus == "เสร็จสิ้น";

                bool preTick = !done && !hasReceipt && isCash && notesCheckin && !hasSlip
                               && channel && !unknown && amountMatch;

                var why = new System.Collections.Generic.List<string>();
                if (!done && !preTick)
                {
                    if (hasReceipt) why.Add("มีใบเสร็จ");
                    if (!isCash) why.Add("ไม่ใช่เงินสด");
                    if (!notesCheckin) why.Add("หมายเหตุไม่ใช่ \"เช็คอิน…\"");
                    if (hasSlip) why.Add("มีสลิปแนบ");
                    if (unknown || !channel) why.Add("โหมดเก็บเงินมาจากการเดา");
                    if (!amountMatch) why.Add(ota > 0m ? "ยอดไม่ตรงยอด OTA" : "ไม่มียอด OTA ให้เทียบ");
                }

                string receiptState = "";
                bool receiptSynced = false;
                string reclassState = "";
                if (hasReceipt)
                {
                    if (sync != null)
                    {
                        try
                        {
                            receiptSynced = sync.IsReceiptSyncedForOtaReclass(receiptId, out receiptState);
                            long qid;
                            string qs = sync.GetOtaCashReclassQueueState(receiptId, out qid);
                            if (!string.IsNullOrEmpty(qs)) reclassState = $"คิวปรับบัญชี #{qid}: {qs}";
                        }
                        catch (Exception exS) { receiptState = "ตรวจสถานะไม่ได้: " + exS.Message; }
                    }
                    else receiptState = "ตรวจสถานะ NextAcc ไม่ได้";
                }

                var nr = result.NewRow();
                nr["PhId"] = Convert.ToInt64(r["PhId"]);
                nr["ResId"] = resId;
                nr["PaymentDate"] = Convert.ToDateTime(r["PaymentDate"]);
                nr["Amount"] = amount;
                nr["Method"] = method;
                nr["RecordedBy"] = r["RecordedBy"] == DBNull.Value ? "-" : r["RecordedBy"].ToString();
                nr["ReceiptId"] = receiptId;
                nr["HasReceipt"] = hasReceipt;
                nr["ReceiptTotal"] = r["ReceiptTotal"] == DBNull.Value ? 0m : Convert.ToDecimal(r["ReceiptTotal"]);
                nr["ReceiptState"] = receiptState;
                nr["ReceiptSynced"] = receiptSynced;
                nr["ReclassState"] = reclassState;
                nr["Guest"] = r["Guest"] == DBNull.Value ? "" : r["Guest"].ToString();
                nr["OtaChannel"] = r["OTA_Channel"] == DBNull.Value ? "" : r["OTA_Channel"].ToString();
                nr["OtaBookingId"] = r["OTA_Booking_ID"] == DBNull.Value ? "" : r["OTA_Booking_ID"].ToString();
                nr["OtaAmount"] = ota;
                nr["Mode"] = unknown ? ReservationBalance.ModeUnknown : b.CollectMode;
                nr["ModeSource"] = Convert.ToString((object)b.CollectSource) ?? "";
                nr["ResStatus"] = resStatus;
                nr["CheckedOut"] = checkedOut;
                nr["Notes"] = notes;
                nr["PreTick"] = preTick;
                nr["Hint"] = preTick ? "เข้าเงื่อนไขครบ" : string.Join(" · ", why);
                result.Rows.Add(nr);
            }
            return result;
        }

        private static bool IsCashMethod(string method)
        {
            string m = (method ?? "").Trim();
            return m == "เงินสด" || string.Equals(m, "CASH", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>แถวที่ติ๊กในตาราง (PhId) — ค่าที่ใช้จริงดึงจากฐานข้อมูลใหม่เสมอ ไม่เชื่อค่าบนหน้าจอ</summary>
        private System.Collections.Generic.List<long> GetTickedOtaIds()
        {
            var ids = new System.Collections.Generic.List<long>();
            foreach (GridViewRow row in gvOta.Rows)
            {
                if (row.RowType != DataControlRowType.DataRow) continue;
                var chk = row.FindControl("chkOta") as CheckBox;
                if (chk == null || !chk.Checked) continue;
                object key = gvOta.DataKeys[row.RowIndex]?.Value;
                long id;
                if (key != null && long.TryParse(key.ToString(), out id)) ids.Add(id);
            }
            return ids;
        }

        /// <summary>รายการปัจจุบัน (คำนวณใหม่จากฐานข้อมูล) เฉพาะแถวที่ติ๊ก — แถวที่หลุดเงื่อนไขไปแล้วจะไม่ถูกแตะ</summary>
        private System.Collections.Generic.List<DataRow> GetTickedOtaRows(bool done, out int missing)
        {
            missing = 0;
            var ticked = GetTickedOtaIds();
            var rows = new System.Collections.Generic.List<DataRow>();
            if (ticked.Count == 0) return rows;

            DataTable current = BuildOtaCandidates(done);
            var map = new System.Collections.Generic.Dictionary<long, DataRow>();
            foreach (DataRow r in current.Rows) map[(long)r["PhId"]] = r;
            foreach (long id in ticked)
            {
                DataRow r;
                if (map.TryGetValue(id, out r)) rows.Add(r);
                else missing++;
            }
            return rows;
        }

        private static string BuildNoteTag(string text)
        {
            string tag = " [" + text + "]";
            return tag.Length > 250 ? tag.Substring(0, 249) + "]" : tag;
        }

        /// <summary>1) ยืนยัน: เป็นเงินที่ OTA เก็บ (ไม่ได้รับเงินสด) — เฉพาะแถวที่ไม่มีใบเสร็จ</summary>
        protected void btnOtaConfirm_Click(object sender, EventArgs e)
        {
            if (!RequireOtaAccess()) return;
            try
            {
                int missing;
                var rows = GetTickedOtaRows(false, out missing);
                if (rows.Count == 0 && missing == 0) { ShowError("ยังไม่ได้ติ๊กรายการ"); ShowOtaView(); return; }

                int ok = 0, skipped = 0;
                var notes = new System.Collections.Generic.List<string>();
                string user = CurrentUserName;
                string tag = BuildNoteTag($"OTA-RECLASS {DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)} by {user}: เงิน OTA เก็บ ไม่ใช่เงินสดรับ");
                foreach (DataRow r in rows)
                {
                    long phId = (long)r["PhId"];
                    if ((bool)r["HasReceipt"])
                    {
                        skipped++;
                        notes.Add($"#{phId}: มีใบเสร็จ {r["ReceiptId"]} — ใช้ปุ่ม \"ส่งกลับรายการบัญชี\" แทน");
                        continue;
                    }
                    int n = codeInstance.DatabaseInsertSafe(connectionString,
                        @"UPDATE Payment_History
                             SET Status = 'OTA_RECLASS',
                                 Notes = LEFT(ISNULL(Notes, N''), 500 - LEN(@tag)) + @tag,
                                 UpdatedDate = GETDATE()
                           WHERE ID = @id AND Status = 'COMPLETED'
                             AND (Receipt_ID IS NULL OR LTRIM(RTRIM(Receipt_ID)) = '')",
                        new System.Collections.Generic.Dictionary<string, object> { { "@id", phId }, { "@tag", tag } });
                    if (n > 0)
                    {
                        ok++;
                        codeInstance.Logs(connectionString, "OTA-Cash-Reclass",
                            $"Payment_History #{phId} การจอง #{r["ResId"]} {Convert.ToDecimal(r["Amount"]):N2} ({r["Method"]}) → OTA_RECLASS (ไม่มีใบเสร็จ)",
                            user);
                    }
                    else
                    {
                        skipped++;
                        notes.Add($"#{phId}: สถานะเปลี่ยนไปแล้ว — ไม่แตะ");
                    }
                }
                if (missing > 0) notes.Add($"{missing} รายการไม่อยู่ในเงื่อนไขแล้ว — ไม่แตะ");
                ShowSuccess($"ปรับเป็น \"เงิน OTA เก็บ\" {ok} รายการ" + (skipped > 0 ? $" · ข้าม {skipped}" : "")
                    + (notes.Count > 0 ? "\n" + string.Join("\n", notes) : ""));
            }
            catch (Exception ex)
            {
                ShowError("ทำรายการไม่สำเร็จ: " + ex.Message);
            }
            ShowOtaView();
        }

        /// <summary>2) ย้อนกลับ OTA_RECLASS → COMPLETED (เฉพาะแถวไม่มีใบเสร็จ — แถวที่ส่ง JE แล้วต้องกลับใน NextAcc ก่อน)</summary>
        protected void btnOtaUndo_Click(object sender, EventArgs e)
        {
            if (!RequireOtaAccess()) return;
            try
            {
                int missing;
                var rows = GetTickedOtaRows(true, out missing);
                if (rows.Count == 0 && missing == 0) { ShowError("ยังไม่ได้ติ๊กรายการ"); ShowOtaView(); return; }

                int ok = 0, skipped = 0;
                var notes = new System.Collections.Generic.List<string>();
                string user = CurrentUserName;
                string tag = BuildNoteTag($"OTA-RECLASS-UNDO {DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)} by {user}: ย้อนกลับเป็นเงินรับ");
                foreach (DataRow r in rows)
                {
                    long phId = (long)r["PhId"];
                    if ((bool)r["HasReceipt"])
                    {
                        skipped++;
                        notes.Add($"#{phId}: ใบเสร็จ {r["ReceiptId"]} ถูกส่ง JE ลูกหนี้ OTA เข้า NextAcc แล้ว — ต้องกลับรายการ JE ใน NextAcc ก่อน (ไม่ย้อนที่นี่)");
                        continue;
                    }
                    int n = codeInstance.DatabaseInsertSafe(connectionString,
                        @"UPDATE Payment_History
                             SET Status = 'COMPLETED',
                                 Notes = LEFT(ISNULL(Notes, N''), 500 - LEN(@tag)) + @tag,
                                 UpdatedDate = GETDATE()
                           WHERE ID = @id AND Status = 'OTA_RECLASS'
                             AND (Receipt_ID IS NULL OR LTRIM(RTRIM(Receipt_ID)) = '')",
                        new System.Collections.Generic.Dictionary<string, object> { { "@id", phId }, { "@tag", tag } });
                    if (n > 0)
                    {
                        ok++;
                        codeInstance.Logs(connectionString, "OTA-Cash-Reclass",
                            $"Payment_History #{phId} การจอง #{r["ResId"]} {Convert.ToDecimal(r["Amount"]):N2} ← ย้อนกลับเป็น COMPLETED",
                            user);
                    }
                    else
                    {
                        skipped++;
                        notes.Add($"#{phId}: สถานะเปลี่ยนไปแล้ว — ไม่แตะ");
                    }
                }
                if (missing > 0) notes.Add($"{missing} รายการไม่พบแล้ว — ไม่แตะ");
                ShowSuccess($"ย้อนกลับ {ok} รายการ" + (skipped > 0 ? $" · ข้าม {skipped}" : "")
                    + (notes.Count > 0 ? "\n" + string.Join("\n", notes) : ""));
            }
            catch (Exception ex)
            {
                ShowError("ทำรายการไม่สำเร็จ: " + ex.Message);
            }
            ShowOtaView();
        }

        /// <summary>
        /// 3) แถวที่มีใบเสร็จ sync แล้ว — ไม่ void ใบเสร็จ; เข้าคิว JE Dr ลูกหนี้ OTA / Cr เงินสด (OTA_CASH_RECLASS)
        /// สถานะแถว → OTA_RECLASS เมื่อ JE สำเร็จ (ทำโดยคิว ไม่ใช่ที่หน้านี้)
        /// </summary>
        protected void btnOtaReclassJe_Click(object sender, EventArgs e)
        {
            if (!RequireOtaAccess()) return;
            if (!CanPostOtaJournal)
            {
                ShowError("การส่งรายการบัญชีสำหรับเจ้าของ/ฝ่ายบัญชีเท่านั้น");
                ShowOtaView();
                return;
            }
            try
            {
                int missing;
                var rows = GetTickedOtaRows(false, out missing);
                if (rows.Count == 0 && missing == 0) { ShowError("ยังไม่ได้ติ๊กรายการ"); ShowOtaView(); return; }

                var sync = new Take_Time_BangPhra.Integration.AccountingSyncService(connectionString);
                decimal tol = ReservationBalance.RoundingTolerance;
                int ok = 0, skipped = 0;
                var notes = new System.Collections.Generic.List<string>();
                string user = CurrentUserName;
                foreach (DataRow r in rows)
                {
                    long phId = (long)r["PhId"];
                    string receiptId = r["ReceiptId"].ToString();
                    decimal amount = (decimal)r["Amount"];
                    if (!(bool)r["HasReceipt"])
                    {
                        skipped++;
                        notes.Add($"#{phId}: ไม่มีใบเสร็จ — ใช้ปุ่ม \"ยืนยัน: เป็นเงินที่ OTA เก็บ\" แทน");
                        continue;
                    }
                    if (!(bool)r["ReceiptSynced"])
                    {
                        skipped++;
                        notes.Add($"#{phId}: ใบเสร็จ {receiptId} — {r["ReceiptState"]} (ไม่ต้อง/ยังส่ง JE ไม่ได้)");
                        continue;
                    }
                    decimal receiptTotal = (decimal)r["ReceiptTotal"];
                    if (receiptTotal > 0m && amount > receiptTotal + tol)
                    {
                        skipped++;
                        notes.Add($"#{phId}: ยอด {amount:N2} มากกว่ายอดใบเสร็จ {receiptTotal:N2} — ตรวจมือ");
                        continue;
                    }
                    long qid = sync.EnqueueOtaCashReclass(phId, (int)r["ResId"], receiptId, amount, DateTime.Today,
                        r["OtaChannel"].ToString(), r["OtaBookingId"].ToString(), (DateTime)r["PaymentDate"], user);
                    if (qid > 0)
                    {
                        ok++;
                        notes.Add($"#{phId}: ใบเสร็จ {receiptId} {amount:N2} → คิว #{qid}");
                        codeInstance.Logs(connectionString, "OTA-Cash-Reclass",
                            $"Payment_History #{phId} การจอง #{r["ResId"]} ใบเสร็จ {receiptId} {amount:N2} → enqueue OTA_CASH_RECLASS #{qid} (Dr ลูกหนี้ OTA / Cr เงินสด)",
                            user);
                    }
                    else
                    {
                        skipped++;
                        notes.Add($"#{phId}: เข้าคิวไม่ได้ (ยังไม่ได้ตั้งค่า NextAcc?)");
                    }
                }
                if (missing > 0) notes.Add($"{missing} รายการไม่อยู่ในเงื่อนไขแล้ว — ไม่แตะ");
                ShowSuccess($"ส่งเข้าคิวปรับบัญชี {ok} รายการ" + (skipped > 0 ? $" · ข้าม {skipped}" : "")
                    + " — สถานะแถวจะเปลี่ยนเป็น \"OTA เก็บ\" เมื่อ NextAcc รับ JE แล้ว"
                    + (notes.Count > 0 ? "\n" + string.Join("\n", notes) : ""));
            }
            catch (Exception ex)
            {
                ShowError("ทำรายการไม่สำเร็จ: " + ex.Message);
            }
            ShowOtaView();
        }

        /// <summary>
        /// 4) เงินสดนี้รับจริง → เปลี่ยนการจองเป็นเก็บเงินหน้างาน (HOTEL) ; แถวเงินคง COMPLETED
        /// แถวที่ไม่มีใบเสร็จ → แจ้งให้ออกใบเสร็จ (รายได้/VAT ต้องรับรู้ผ่านใบเสร็จ)
        /// </summary>
        protected void btnOtaHotel_Click(object sender, EventArgs e)
        {
            if (!RequireOtaAccess()) return;
            try
            {
                int missing;
                var rows = GetTickedOtaRows(false, out missing);
                if (rows.Count == 0 && missing == 0) { ShowError("ยังไม่ได้ติ๊กรายการ"); ShowOtaView(); return; }

                string user = CurrentUserName;
                var byRes = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<DataRow>>();
                foreach (DataRow r in rows)
                {
                    int resId = (int)r["ResId"];
                    if (!byRes.ContainsKey(resId)) byRes[resId] = new System.Collections.Generic.List<DataRow>();
                    byRes[resId].Add(r);
                }

                int ok = 0, failed = 0;
                var notes = new System.Collections.Generic.List<string>();
                var needReceipt = new System.Text.StringBuilder();
                foreach (var kv in byRes)
                {
                    var phIds = new System.Collections.Generic.List<string>();
                    foreach (DataRow r in kv.Value) phIds.Add("#" + r["PhId"]);
                    string reason = "ตรวจเงินสดใบ OTA: ยืนยันว่ารับเงินสดจริง (Payment_History " + string.Join(", ", phIds) + ")";

                    var res = ReservationBalance.SetCollectMode(connectionString, kv.Key, ReservationBalance.ModeHotel, "STAFF", user, reason);
                    if (res.Ok)
                    {
                        ok++;
                        codeInstance.Logs(connectionString, "OTA-Cash-Reclass",
                            $"การจอง #{kv.Key} → เก็บเงินหน้างาน (HOTEL) — {reason}", user);
                        foreach (DataRow r in kv.Value)
                        {
                            if ((bool)r["HasReceipt"]) continue;
                            string resId = kv.Key.ToString();
                            needReceipt.Append("<li>การจอง #").Append(resId).Append(" ยอด ")
                                .Append(Convert.ToDecimal(r["Amount"]).ToString("N2"))
                                .Append(" — <a href=\"").Append(ResolveUrl("~/Reserve?command=edit&id=" + resId))
                                .Append("\">เปิดการจองเพื่อออกใบเสร็จ</a> · <a href=\"").Append(ResolveUrl("~/Account/Receipt"))
                                .Append("\">หน้าออกใบเสร็จ</a></li>");
                        }
                    }
                    else
                    {
                        failed++;
                        notes.Add($"การจอง #{kv.Key}: " + res.Message);
                    }
                }
                if (missing > 0) notes.Add($"{missing} รายการไม่อยู่ในเงื่อนไขแล้ว — ไม่แตะ");
                ShowSuccess($"เปลี่ยนเป็นเก็บเงินหน้างาน {ok} การจอง" + (failed > 0 ? $" · ไม่สำเร็จ {failed}" : "")
                    + (notes.Count > 0 ? "\n" + string.Join("\n", notes) : ""));
                if (needReceipt.Length > 0)
                    litOtaNotice.Text = "<div class=\"alert alert-warning\"><b>ยังไม่มีใบเสร็จ</b> — เงินสดที่รับจริงต้องออกใบเสร็จ " +
                        "เพื่อรับรู้รายได้/ภาษีขาย (แถวเงินยังเป็น COMPLETED ตามเดิม):<ul>" + needReceipt + "</ul></div>";
            }
            catch (Exception ex)
            {
                ShowError("ทำรายการไม่สำเร็จ: " + ex.Message);
            }
            ShowOtaView();
        }
    }
}
