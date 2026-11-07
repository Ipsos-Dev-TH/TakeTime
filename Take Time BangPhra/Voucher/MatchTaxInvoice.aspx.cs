using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.Voucher
{
    public partial class MatchTaxInvoice : System.Web.UI.Page
    {
        private string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code = new code();
        private DocumentHelper _documentHelper;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.FinVoucher)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            _documentHelper = new DocumentHelper(conn);

            try
            {
                if (Session["permission"]?.ToString() != "True"
                    || (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
                {
                    Response.Redirect("/Default");
                    return;
                }
            }
            catch
            {
                Response.Redirect("/Default");
                return;
            }

            if (!IsPostBack)
            {
                txtStartDate.Text = DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd");
                txtEndDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
                BindGrid();

                string createdQ = Request.QueryString["created"];
                if (!string.IsNullOrEmpty(createdQ))
                {
                    ShowMessage($"✅ สร้างใบกำกับภาษีสำเร็จ: {createdQ}", "#d4edda", "#155724");
                }
            }
        }

        protected void btnApply_Click(object sender, EventArgs e)
        {
            BindGrid();
        }

        protected void ddlFilter_Changed(object sender, EventArgs e)
        {
            BindGrid();
        }

        private void BindGrid()
        {
            string filter = ddlFilter.SelectedValue ?? "missing";
            DateTime startDate = DateTime.TryParse(txtStartDate.Text, out var sd) ? sd : DateTime.Now.AddMonths(-3);
            DateTime endDate = DateTime.TryParse(txtEndDate.Text, out var ed) ? ed.AddDays(1) : DateTime.Now.AddDays(1);
            string search = (txtSearch.Text ?? "").Trim();

            string statusClause;
            switch (filter)
            {
                case "matched": statusClause = "AND v.Receipt_ID IS NOT NULL AND LEN(v.Receipt_ID) > 0"; break;
                case "all": statusClause = ""; break;
                case "missing":
                default: statusClause = "AND (v.Receipt_ID IS NULL OR LEN(v.Receipt_ID) = 0)"; break;
            }

            string searchClause = string.IsNullOrEmpty(search) ? "" :
                "AND (v.Voucher_Number LIKE @search OR ISNULL(c.FullName, '') LIKE @search)";

            var parameters = new Dictionary<string, object>
            {
                { "@start", startDate },
                { "@end", endDate }
            };
            if (!string.IsNullOrEmpty(search))
                parameters.Add("@search", "%" + search + "%");

            string sql = $@"
                SELECT v.Voucher_Number, v.Created_Date, v.Sell_Price, v.Receipt_ID, v.Customer_ID,
                       ISNULL(c.FullName, '-') AS Customer_Name,
                       ISNULL(c.MobilePhone, '') AS Customer_Phone,
                       ISNULL(c.Email, '') AS Customer_Email
                FROM Voucher v
                LEFT JOIN Customer c ON c.ID = v.Customer_ID
                WHERE v.Created_Date >= @start AND v.Created_Date < @end
                  {statusClause}
                  {searchClause}
                ORDER BY v.Created_Date DESC, v.Voucher_Number DESC";

            DataTable dt = _code.DatabaseQuerySafe(conn, sql, parameters);

            gvVouchers.DataSource = dt;
            gvVouchers.DataBind();

            // Stats (independent of current filter, within date range)
            var statParams = new Dictionary<string, object>
            {
                { "@start", startDate },
                { "@end", endDate }
            };
            DataTable statsDt = _code.DatabaseQuerySafe(conn,
                @"SELECT
                    COUNT(*) AS Total,
                    SUM(CASE WHEN Receipt_ID IS NOT NULL AND LEN(Receipt_ID) > 0 THEN 1 ELSE 0 END) AS Matched,
                    SUM(CASE WHEN Receipt_ID IS NULL OR LEN(Receipt_ID) = 0 THEN 1 ELSE 0 END) AS Missing
                  FROM Voucher
                  WHERE Created_Date >= @start AND Created_Date < @end", statParams);

            if (statsDt?.Rows.Count > 0)
            {
                litTotal.Text = (statsDt.Rows[0]["Total"] ?? "0").ToString();
                litMatched.Text = (statsDt.Rows[0]["Matched"] ?? "0").ToString();
                litMissing.Text = (statsDt.Rows[0]["Missing"] ?? "0").ToString();
            }
        }

        protected void gvVouchers_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;

            var receiptId = (e.Row.DataItem as DataRowView)?["Receipt_ID"]?.ToString();
            bool hasReceipt = !string.IsNullOrEmpty(receiptId);

            var lblStatus = (Label)e.Row.FindControl("lblStatus");
            var btnCreate = (Button)e.Row.FindControl("btnCreateTax");

            if (lblStatus != null)
            {
                if (hasReceipt)
                {
                    lblStatus.Text = "✓ มีแล้ว";
                    lblStatus.CssClass = "badge-status matched";
                }
                else
                {
                    lblStatus.Text = "✗ ขาด";
                    lblStatus.CssClass = "badge-status missing";
                }
            }

            if (btnCreate != null) btnCreate.Visible = !hasReceipt;
        }

        protected void gvVouchers_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "createTax") return;

            string voucherNumber = e.CommandArgument?.ToString();
            if (string.IsNullOrEmpty(voucherNumber))
            {
                ShowMessage("เลข Voucher ไม่ถูกต้อง", "#f8d7da", "#721c24");
                return;
            }

            // Anti-duplicate session lock
            string lockKey = "TaxInvoiceLock_" + voucherNumber;
            if (Session[lockKey] is DateTime lockTime && (DateTime.Now - lockTime).TotalSeconds < 60)
            {
                Response.Redirect(Request.RawUrl, false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            Session[lockKey] = DateTime.Now;

            try
            {
                string newReceiptId = CreateTaxInvoiceForVoucher(voucherNumber);

                if (!string.IsNullOrEmpty(newReceiptId))
                {
                    // Post-Redirect-Get
                    string sep = Request.RawUrl.Contains("?") ? "&" : "?";
                    Response.Redirect(Request.RawUrl + sep + "created=" + newReceiptId, false);
                    Context.ApplicationInstance.CompleteRequest();
                }
                else
                {
                    ShowMessage("ไม่สามารถสร้างใบกำกับภาษี — ตรวจสอบข้อมูล Voucher", "#f8d7da", "#721c24");
                }
            }
            catch (Exception ex)
            {
                _code.Logs(conn, "MatchTaxInvoice", $"CreateTaxInvoice error voucher={voucherNumber}: {ex.Message}", Session["UserName"]?.ToString() ?? "SYSTEM");
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "#f8d7da", "#721c24");
            }
        }

        /// <summary>
        /// Creates an Account_Receipt + Account_Receipt_Detail for the given voucher
        /// and links it back via Voucher.Receipt_ID. Auto-syncs to Nexaacc accounting.
        /// </summary>
        private string CreateTaxInvoiceForVoucher(string voucherNumber)
        {
            // 1. Load voucher data
            var dt = _code.DatabaseQuerySafe(conn,
                @"SELECT v.Voucher_Number, v.Created_Date, v.Sell_Price, v.Receipt_ID, v.Customer_ID,
                         ISNULL(c.FullName, '-') AS Customer_Name
                  FROM Voucher v
                  LEFT JOIN Customer c ON c.ID = v.Customer_ID
                  WHERE v.Voucher_Number = @vn",
                new Dictionary<string, object> { { "@vn", voucherNumber } });

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("ไม่พบ Voucher: " + voucherNumber);

            var v = dt.Rows[0];
            string existingReceipt = v["Receipt_ID"]?.ToString();
            if (!string.IsNullOrEmpty(existingReceipt))
                throw new Exception($"Voucher {voucherNumber} มีใบกำกับภาษีอยู่แล้ว: {existingReceipt}");

            decimal sellPrice = Convert.ToDecimal(v["Sell_Price"]);
            string customerId = v["Customer_ID"]?.ToString() ?? "0";
            DateTime voucherDate = Convert.ToDateTime(v["Created_Date"]);
            string customerName = v["Customer_Name"]?.ToString() ?? "";

            // VAT @ 7% — sellPrice is treated as VAT-inclusive (matches voucher pricing convention)
            decimal totalAmount = sellPrice;
            decimal vatAmount = Math.Round(totalAmount * 7 / 107, 2);
            decimal exclVat = totalAmount - vatAmount;

            // 2. Generate receipt number
            DateTime receiptDate = DateTime.Now;
            string docNum = _documentHelper.CreateDocumentNumber("Account_Receipt", "REC", receiptDate);

            int createdById = 0;
            if (Session["UserID"] != null)
                int.TryParse(Session["UserID"].ToString(), out createdById);

            // 3. Insert Account_Receipt
            var receiptParams = new Dictionary<string, object>
            {
                { "@DocNum", docNum },
                { "@ReservationID", 0 },
                { "@CreatedDate", receiptDate },
                { "@TotalAmount", totalAmount },
                { "@Vat", vatAmount },
                { "@TotalAmountExcludeVat", exclVat },
                { "@PaidType", "โอนเงินเข้าบัญชีบริษัท" },
                { "@CreatedByID", createdById },
                { "@Etax", false },
                { "@CustomerID", customerId }
            };

            _code.DatabaseInsertSafe(conn,
                "INSERT INTO [dbo].[Account_Receipt] " +
                "([ID],[Reservation_ID],[Created_Date],[Total_Amount],[Vat],[Total_Amount_Exclude_Vat],[IsDeposit],[UseDeposit],[Paid_Type],[Status],[Created_By_ID],Etax,Customer_ID) " +
                "VALUES (@DocNum,@ReservationID,@CreatedDate,@TotalAmount,@Vat,@TotalAmountExcludeVat,'False','False',@PaidType,'Normal',@CreatedByID,@Etax,@CustomerID)",
                receiptParams);

            // 4. Insert Account_Receipt_Detail
            var detailParams = new Dictionary<string, object>
            {
                { "@DocNum", docNum },
                { "@ProductAmount", "1" },
                { "@PricePerPiece", sellPrice },
                { "@PriceAmount", totalAmount }
            };

            _code.DatabaseInsertSafe(conn,
                "INSERT INTO [dbo].[Account_Receipt_Detail] " +
                "([Number],[Receipt_ID],[ProductType_ID],[Product_ID],[Product_Data],[Product_Amount],[Product_Unit],[Price_PerPeice],[Price_Amount]) " +
                "VALUES ('1',@DocNum,'1',0,N'Voucher ที่พัก (สร้างย้อนหลัง - " + voucherNumber.Replace("'", "") + ")',@ProductAmount,N'ใบ',@PricePerPiece,@PriceAmount)",
                detailParams);

            // 5. Link Voucher → Receipt
            _code.DatabaseInsertSafe(conn,
                "UPDATE [dbo].[Voucher] SET Receipt_ID = @DocNum WHERE Voucher_Number = @VN",
                new Dictionary<string, object>
                {
                    { "@DocNum", docNum },
                    { "@VN", voucherNumber }
                });

            // 6. Auto-sync to Nexaacc
            try
            {
                var acctConfig = new Integration.AccountingConfig(conn);
                if (acctConfig.IsConfigured && acctConfig.Enabled)
                {
                    var sync = new Integration.AccountingSyncService(conn);
                    string payMethod = "โอนเงินเข้าบัญชีบริษัท";
                    string payAccId = sync.LookupPaidHowAccountId(payMethod);
                    sync.EnqueueReceipt(0, docNum, totalAmount, vatAmount, receiptDate,
                        customerName, isDeposit: false, paymentMethod: payMethod,
                        revenueType: "ROOM_REVENUE", paymentAccountId: payAccId);
                }
            }
            catch (Exception accEx)
            {
                _code.Logs(conn, "MatchTaxInvoice", $"Accounting sync warning (continuing): {accEx.Message}", "SYSTEM");
            }

            _code.Logs(conn, "MatchTaxInvoice",
                $"Created tax invoice {docNum} for voucher {voucherNumber} amount={totalAmount} VAT={vatAmount}",
                Session["UserName"]?.ToString() ?? "SYSTEM");

            return docNum;
        }

        private void ShowMessage(string text, string bgColor, string fgColor)
        {
            lblMessage.Text = text;
            lblMessage.Style["background"] = bgColor;
            lblMessage.Style["color"] = fgColor;
            lblMessage.Style["border"] = "1px solid " + fgColor + "33";
            lblMessage.Visible = true;
        }
    }
}
