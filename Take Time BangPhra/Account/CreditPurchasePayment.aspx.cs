using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Account
{
    /// <summary>
    /// ชำระใบซื้อเชื่อ (PHASE19_31) — ใบสำคัญจ่าย IsCredit=1 (ตั้งเจ้าหนี้: NextAcc Expense Cr เจ้าหนี้ / JOURNAL Cr เจ้าหนี้)
    /// จ่ายจริงที่นี่ → AccountingSyncService.EnqueueCreditVoucherPayment (PAY_CREDIT_VOUCHER — เดิมไม่มีผู้เรียก):
    ///   DOCUMENT: บันทึกรับชำระกับเอกสารเดิม (AutoRecordPaymentForVoucher — แหล่งเงิน override + ลายเซ็นผู้จ่ายจากผู้จัดทำใบเดิม)
    ///   JOURNAL/ไม่พบเอกสาร: JE Dr เจ้าหนี้ / Cr แหล่งเงิน
    /// แล้วบันทึกฝั่ง TakeTime (Account_Payment.Credit_Paid_* + Product_In.Payment_Status = PAID ของการรับของที่ผูก)
    /// ข้อจำกัด: NextAcc ฝั่งคิวกันซ้ำ "หนึ่งการจ่ายต่อเอกสาร" (CREDITPAY-{doc}) → จ่ายครบครั้งเดียว ไม่รองรับจ่ายบางส่วน
    /// </summary>
    public partial class CreditPurchasePayment : Page
    {
        private readonly code db = new code();
        private readonly string conn = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.FinVoucher)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            try
            {
                if (!(Session["permission"]?.ToString() == "True" && (Session["User"]?.ToString() == "Owner" || Session["User"]?.ToString() == "Admin")))
                {
                    Response.Redirect("/Default");
                    return;
                }
            }
            catch { Response.Redirect("/Default"); return; }

            if (!IsPostBack)
            {
                LoadPaidHowOptions();
                LoadNexaaccAccountOptions();
                BindGrid();
                if (Request.QueryString["paid"] == "1")
                    ClientScript.RegisterStartupScript(GetType(), "paidok",
                        "alert('" + ("✅ บันทึกจ่ายชำระแล้ว " + (Request.QueryString["doc"] ?? "") + "\\n" + SyncMessage(Request.QueryString["s"])).Replace("'", "\\'") + "');", true);
            }
        }

        private static string SyncMessage(string s)
        {
            if (s == "queued") return "ส่งเข้าคิว sync ไป NextAcc แล้ว";
            if (s == "off") return "ยังไม่ได้เปิด/ตั้งค่า NextAcc — บันทึกในระบบเท่านั้น";
            return "⚠️ ส่ง sync ไม่สำเร็จ — ตรวจสอบคิว sync";
        }

        private void LoadPaidHowOptions()
        {
            try
            {
                var dt = db.DatabaseQuerySafe(conn, "SELECT Paid_How FROM Account_Paid_How WHERE Status = 'True' ORDER BY Paid_How", null);
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        ddlPayPaidHow.Items.Add(new ListItem(r["Paid_How"].ToString(), r["Paid_How"].ToString()));
            }
            catch { }
        }

        private void LoadNexaaccAccountOptions()
        {
            ddlPayNexaaccAccount.Items.Clear();
            ddlPayNexaaccAccount.Items.Add(new ListItem("— ใช้ตามแหล่งเงิน (mapping) —", ""));
            try
            {
                var dt = db.DatabaseQuerySafe(conn,
                    @"SELECT Nexaacc_AccountId, Account_Code, Account_Name FROM Accounting_Nexaacc_Accounts
                      WHERE ISNULL(Is_Active, 1) = 1 AND (Account_Code LIKE '11%' OR Account_Code LIKE '21%') ORDER BY Account_Code", null);
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        ddlPayNexaaccAccount.Items.Add(new ListItem($"{r["Account_Code"]} {r["Account_Name"]}".Trim(), r["Nexaacc_AccountId"]?.ToString() ?? ""));
            }
            catch { }
        }

        private bool HasPaidCols =>Take_Time_BangPhra.Purchasing.PurchaseService.HasCreditPaidColumns(conn);

        private void BindGrid()
        {
            bool paidCols = HasPaidCols;
            bool linkCol = Take_Time_BangPhra.Purchasing.PurchaseService.HasVoucherLink(conn);
            var notes = new List<string>();
            if (!paidCols)
                notes.Add("⚠️ ยังไม่ได้รัน migration PHASE19_31 — ระบบจะใช้สถานะชำระจาก NextAcc (Nexaacc_Payment_Id) แทนการบันทึกฝั่ง TakeTime");
            try
            {
                var cfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                if (!cfg.IsConfigured || !cfg.Enabled)
                    notes.Add("ℹ️ ยังไม่ได้เปิดเชื่อม NextAcc — บันทึกจ่ายในระบบอย่างเดียว");
                else if (cfg.IsVoucherLocal)
                    notes.Add("ℹ️ ใบสำคัญจ่ายตั้งเป็น LOCAL — จะไม่ส่งการจ่ายไป NextAcc");
            }
            catch { }
            litInfo.Text = notes.Count == 0 ? "" : "<div class='cpp-warn'>" + string.Join("<br/>", notes.ConvertAll(n => HttpUtility.HtmlEncode(n))) + "</div>";

            string paidExpr = paidCols
                ? "CASE WHEN ap.Credit_Paid_Date IS NOT NULL THEN 1 ELSE 0 END"
                : "CASE WHEN ap.Nexaacc_Payment_Id IS NOT NULL THEN 1 ELSE 0 END";
            string paidInfo = paidCols
                ? "CASE WHEN ap.Credit_Paid_Date IS NOT NULL THEN N'จ่ายแล้ว ' + CONVERT(NVARCHAR(10), ap.Credit_Paid_Date, 103) + N' · ' + ISNULL(ap.Credit_Paid_How, N'') ELSE N'ค้างจ่าย' END"
                : "CASE WHEN ap.Nexaacc_Payment_Id IS NOT NULL THEN N'จ่ายแล้ว (NextAcc)' ELSE N'ค้างจ่าย' END";
            string linkExpr = linkCol ? "ap.Linked_Goods_Receipt" : "CAST(NULL AS NVARCHAR(50))";
            string sql = $@"SELECT TOP 300 ap.ID, ap.Created_Date, ISNULL(v.Name, N'') AS Vendor_Name,
                                   ap.Total_Amount, ISNULL(ap.WHT_Amount, 0) AS WHT_Amount,
                                   ap.Nexaacc_Document_Number, {linkExpr} AS Linked_Goods_Receipt,
                                   {paidExpr} AS Is_Paid, {paidInfo} AS Paid_Info
                            FROM Account_Payment ap LEFT JOIN Vendor v ON v.ID = ap.Vendor_ID
                            WHERE ap.IsCredit = 1 AND ISNULL(ap.Status, N'Normal') <> N'Cancel'
                              {(chkShowPaid.Checked ? "" : "AND " + paidExpr + " = 0")}
                            ORDER BY ap.Created_Date ASC, ap.ID ASC";
            try
            {
                gvCredit.DataSource = db.DatabaseQuerySafe(conn, sql, null);
                gvCredit.DataBind();
            }
            catch (Exception ex)
            {
                litInfo.Text += "<div class='cpp-warn'>โหลดรายการไม่สำเร็จ: " + HttpUtility.HtmlEncode(ex.Message) + "</div>";
            }
        }

        protected void chkShowPaid_CheckedChanged(object sender, EventArgs e)
        {
            BindGrid();
        }

        protected void gvCredit_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "PayCredit") return;
            string doc = Convert.ToString(e.CommandArgument);
            var row = LoadVoucher(doc);
            if (row == null) return;

            hfPayDoc.Value = doc;
            lblPayDoc.Text = HttpUtility.HtmlEncode(doc);
            txtPayDate.Text = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            decimal total = row["Total_Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Total_Amount"]);
            decimal wht = row.Table.Columns.Contains("WHT_Amount") && row["WHT_Amount"] != DBNull.Value ? Convert.ToDecimal(row["WHT_Amount"]) : 0m;
            txtPayAmount.Text = total.ToString("0.00", CultureInfo.InvariantCulture);
            string paidHow = row["Paid_How"]?.ToString() ?? "";
            if (ddlPayPaidHow.Items.FindByValue(paidHow) != null) ddlPayPaidHow.SelectedValue = paidHow;

            var info = new List<string>();
            info.Add($"ผู้ขาย: {row["Vendor_Name"]} · ยอดค้างจ่าย {total:N2}" + (wht > 0 ? $" (หัก ณ ที่จ่าย {wht:N2} ไว้แล้วตอนตั้งหนี้ — ออก 50ทวิ ตอนตั้งหนี้)" : ""));
            string nexDoc = row["Nexaacc_Document_Number"]?.ToString();
            info.Add(string.IsNullOrEmpty(nexDoc) ? "⚠️ ยังไม่พบเอกสารตั้งหนี้ใน NextAcc (ยังไม่ซิงค์?) — โหมดเอกสารจะรอจนซิงค์สำเร็จ" : "เอกสารตั้งหนี้ NextAcc: " + nexDoc);
            info.Add("ลายเซ็น \"ผู้จ่ายเงิน\" ใช้ลายเซ็นของผู้จัดทำใบสำคัญจ่ายเดิม (เหมือนการจ่ายใบสำคัญจ่ายปกติ)");

            // คืนสินค้าของการรับของที่ผูกกับใบนี้ (หักเจ้าหนี้ไปแล้วใน JE คืนสินค้า) — ให้ผู้ใช้ตัดสินยอดจ่ายจริง
            decimal returnedNet = ReturnedNetForVoucher(doc);
            if (returnedNet > 0)
                info.Add($"⚠️ มีการคืนสินค้าของการรับของที่ผูกกับใบนี้ สุทธิ {returnedNet:N2} (+VAT ถ้ามี) ลงหักเจ้าหนี้แล้ว — ถ้าผู้ขายหักให้ในการจ่ายนี้ ให้ลดยอดจ่ายตาม");
            litPayInfo.Text = "<div class='cpp-warn'>" + string.Join("<br/>", info.ConvertAll(n => HttpUtility.HtmlEncode(n))) + "</div>";
            pnlPay.Visible = true;
        }

        private DataRow LoadVoucher(string doc)
        {
            try
            {
                var dt = db.DatabaseQuerySafe(conn,
                    @"SELECT ap.*, ISNULL(v.Name, N'') AS Vendor_Name FROM Account_Payment ap LEFT JOIN Vendor v ON v.ID = ap.Vendor_ID
                      WHERE ap.ID = @id AND ap.IsCredit = 1",
                    new Dictionary<string, object> { { "@id", doc ?? "" } });
                return dt != null && dt.Rows.Count > 0 ? dt.Rows[0] : null;
            }
            catch { return null; }
        }

        private decimal ReturnedNetForVoucher(string doc)
        {
            if (!Take_Time_BangPhra.Purchasing.PurchaseService.HasReturnColumns(conn)
                || !Take_Time_BangPhra.Purchasing.PurchaseService.HasPurchaseColumns(conn)) return 0m;
            try
            {
                var dt = db.DatabaseQuerySafe(conn,
                    @"SELECT ISNULL(SUM(CAST(po.Amount AS DECIMAL(18,4)) * CAST(po.PricePerUnit AS DECIMAL(18,4))), 0)
                      FROM Product_Out po
                      WHERE po.Return_Receipt_Batch IN (SELECT DISTINCT Receipt_Batch FROM Product_In WHERE Linked_Voucher_Id = @v)",
                    new Dictionary<string, object> { { "@v", doc } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                    return Math.Round(Convert.ToDecimal(dt.Rows[0][0]), 2);
            }
            catch { }
            return 0m;
        }

        protected void btnCancelPay_Click(object sender, EventArgs e)
        {
            pnlPay.Visible = false;
            hfPayDoc.Value = "";
        }

        protected void btnConfirmPay_Click(object sender, EventArgs e)
        {
            string doc = hfPayDoc.Value;
            var row = LoadVoucher(doc);
            if (row == null) { Alert("ไม่พบใบสำคัญจ่าย"); return; }
            if (HasPaidCols && row.Table.Columns.Contains("Credit_Paid_Date") && row["Credit_Paid_Date"] != DBNull.Value)
            { Alert("ใบนี้บันทึกจ่ายแล้ว"); return; }

            string paidHow = ddlPayPaidHow.SelectedValue;
            if (string.IsNullOrEmpty(paidHow)) { Alert("กรุณาเลือกแหล่งเงินที่จ่าย"); return; }
            DateTime payDate;
            if (!DateTime.TryParse(txtPayDate.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out payDate)) { Alert("วันที่จ่ายไม่ถูกต้อง"); return; }
            decimal amount;
            if (!decimal.TryParse((txtPayAmount.Text ?? "").Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out amount) || amount <= 0)
            { Alert("ยอดจ่ายไม่ถูกต้อง"); return; }
            decimal total = row["Total_Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Total_Amount"]);
            if (amount > total + 0.01m) { Alert($"ยอดจ่าย ({amount:N2}) มากกว่ายอดค้างจ่ายของใบ ({total:N2})"); return; }

            string vendorName = row["Vendor_Name"]?.ToString() ?? "";
            string syncStatus = "off";
            long qid = -1;
            try
            {
                var cfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                if (cfg.IsConfigured && cfg.Enabled && !cfg.IsVoucherLocal)
                {
                    // โหมดเอกสาร: ต้องมีเอกสารตั้งหนี้ใน NextAcc ก่อน ไม่งั้น processor ตกไป JE (Dr เจ้าหนี้/Cr เงินสด)
                    // ขณะที่เอกสาร Expense เดิมยังค้างชำระ = เจ้าหนี้ใน subledger ไม่ตรง GL
                    string respId = row.Table.Columns.Contains("Nexaacc_Response_Id") ? row["Nexaacc_Response_Id"]?.ToString() : null;
                    if (cfg.IsVoucherDocumentMode && string.IsNullOrEmpty(respId))
                    {
                        Alert("ใบตั้งหนี้นี้ยังไม่ซิงค์ไป NextAcc — รอคิว sync สำเร็จก่อน (หน้าคิว Accounting) แล้วค่อยบันทึกจ่าย");
                        return;
                    }
                    string accId = ddlPayNexaaccAccount.SelectedValue;
                    var sync = new Take_Time_BangPhra.Integration.AccountingSyncService(conn);
                    if (string.IsNullOrEmpty(accId)) accId = sync.LookupPaidHowAccountId(paidHow);
                    qid = sync.EnqueueCreditVoucherPayment(doc, amount, paidHow, payDate, vendorName, accId);
                    syncStatus = qid > 0 ? "queued" : "err";
                    if (qid <= 0) { Alert("ส่งคิวจ่ายชำระไม่สำเร็จ — ไม่ได้บันทึก"); return; }
                }
            }
            catch (Exception ex)
            {
                try { db.Logs(conn, "Purchase", $"CreditPurchasePayment enqueue failed doc={doc}: {ex.Message}", "SYSTEM"); } catch { }
                Alert("ส่งคิวจ่ายชำระไม่สำเร็จ: " + ex.Message);
                return;
            }

            // บันทึกฝั่ง TakeTime
            try
            {
                if (HasPaidCols)
                    db.DatabaseInsertSafe(conn,
                        @"UPDATE Account_Payment SET Credit_Paid_Date = @d, Credit_Paid_How = @h, Credit_Paid_By = @u,
                                 Credit_Paid_Queue_Id = @q WHERE ID = @id",
                        new Dictionary<string, object>
                        {
                            { "@d", payDate }, { "@h", paidHow }, { "@u", Session["User"]?.ToString() ?? "" },
                            { "@q", qid > 0 ? (object)qid : DBNull.Value }, { "@id", doc }
                        });
                if (Take_Time_BangPhra.Purchasing.PurchaseService.HasPurchaseColumns(conn))
                    db.DatabaseInsertSafe(conn,
                        "UPDATE Product_In SET Payment_Status = N'PAID' WHERE Linked_Voucher_Id = @id",
                        new Dictionary<string, object> { { "@id", doc } });
                db.Logs(conn, "Purchase", $"Credit voucher paid: doc={doc} amount={amount:N2} via={paidHow} queue={qid}", Session["User"]?.ToString() ?? "SYSTEM");
            }
            catch (Exception ex)
            {
                try { db.Logs(conn, "Purchase", $"CreditPurchasePayment local mark failed doc={doc}: {ex.Message}", "SYSTEM"); } catch { }
            }

            Response.Redirect("/Account/CreditPurchasePayment?paid=1&doc=" + HttpUtility.UrlEncode(doc) + "&s=" + syncStatus, false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void Alert(string msg)
        {
            ClientScript.RegisterStartupScript(GetType(), "cppalert", "alert('" + (msg ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n") + "');", true);
        }
    }
}
