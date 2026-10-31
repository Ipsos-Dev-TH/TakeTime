using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using System.IO;
using Microsoft.Ajax.Utilities;
using Take_Time_BangPhra.Account.Report;
using System.Security.Cryptography;
using ECertificateAPI;
using System.Net.Mail;
using Microsoft.Reporting.WebForms;
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.Product
{
    public partial class In : System.Web.UI.Page
    {
        _Default codeDefault = new _Default();
        Receipt codeReceipt = new Receipt();
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        AddressHelper addressHelper;
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SalesStock)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            addressHelper = new AddressHelper(conn);

            try
            {
                try
                {
                    if (Session["permission"].ToString() == "True" && (Session["User"].ToString() == "Owner" || Session["User"].ToString() == "Admin"))
                    {

                    }
                    else
                    {
                        Response.Redirect("/Default");
                    }
                }
                catch
                {
                    Response.Redirect("/Default");
                }

                if (!IsPostBack)
                {
                    DataTable dtOrder = new DataTable();
                    try
                    {
                        dtOrder.Columns.Add("ID");
                        dtOrder.Columns.Add("Barcode");
                        dtOrder.Columns.Add("Product_Name");
                        dtOrder.Columns.Add("Amount");
                        dtOrder.Columns.Add("Sell_Price");
                        dtOrder.Columns.Add("Price_Total");
                        dtOrder.Columns.Add("Category_ID");
                        Session["dtOrder"] = dtOrder;
                    }
                    catch { }
                    string yourHTMLstring = "<script> var Material_Name = [";
                    DataTable dt = code.DatabaseQuery(conn, "SELECT Distinct(Product_Name) as Material_Name FROM [Taketime].[dbo].[Product] Where [Status] = 'True'");
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        // Properly escape special characters for JavaScript string
                        string productName = dt.Rows[i][0].ToString()
                            .Replace("\\", "\\\\")  // Escape backslashes first
                            .Replace("\"", "\\\"")  // Escape double quotes
                            .Replace("'", "\\'")    // Escape single quotes
                            .Replace("\r", "")      // Remove carriage return
                            .Replace("\n", "")      // Remove newline
                            .Replace(",", "");      // Remove commas
                        yourHTMLstring += "\"" + productName + "\"";
                        if (i < dt.Rows.Count - 1)
                        {
                            yourHTMLstring += ",";
                        }
                    }
                    yourHTMLstring += "];\r\nautocomplete(document.getElementById(\"MainContent_TextBox1\"), Material_Name);</script>";
                    Literal1.Text = yourHTMLstring;

                    LoadPurchaseOptions();
                }
            }
            catch(Exception ex) {

                //Response.Redirect("/Default");  
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('" + ex + "');", true);
            }


        }

        protected void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "Add")
            {
                DataTable dtOrder = (DataTable)Session["dtOrder"];
                int amount = Convert.ToInt32(dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Amount"].ToString());
                amount = amount + 1;
                double total = amount * Convert.ToDouble(dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Sell_Price"].ToString());
                dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Amount"] = amount;
                dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Price_Total"] = total;
                GridView1.DataSource = dtOrder;
                GridView1.DataBind();
                Session["dtOrder"] = dtOrder;
                TextBox1.Text = string.Empty;
                double pricetotal = 0;
                for (int i = 0; i < dtOrder.Rows.Count; i++)
                {
                    pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
                }
                
            }
            else if (e.CommandName == "Reduce")
            {
                DataTable dtOrder = (DataTable)Session["dtOrder"];
                int amount = Convert.ToInt32(dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Amount"].ToString());
                if (amount > 1)
                {
                    amount = amount - 1;
                    double total = amount * Convert.ToDouble(dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Sell_Price"].ToString());
                    dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Amount"] = amount;
                    dtOrder.Rows[Convert.ToInt32(e.CommandArgument)]["Price_Total"] = total;
                    GridView1.DataSource = dtOrder;
                    GridView1.DataBind();
                    Session["dtOrder"] = dtOrder;
                    TextBox1.Text = string.Empty;
                    double pricetotal = 0;
                    for (int i = 0; i < dtOrder.Rows.Count; i++)
                    {
                        pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
                    }
                    
                }
            }
            else if (e.CommandName == "DeleteItem")
            {
                DataTable dtOrder = (DataTable)Session["dtOrder"];
                dtOrder.Rows[Convert.ToInt32(e.CommandArgument)].Delete();
                dtOrder.AcceptChanges();
                GridView1.DataSource = dtOrder;
                GridView1.DataBind();
                Session["dtOrder"] = dtOrder;
                TextBox1.Text = string.Empty;
                double pricetotal = 0;
                for (int i = 0; i < dtOrder.Rows.Count; i++)
                {
                    pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
                }
                
            }
        }
        protected void GridView1_RowEditing(object sender, GridViewEditEventArgs e)
        {
            GridView1.EditIndex = e.NewEditIndex;
            DataTable dtOrder = (DataTable)Session["dtOrder"];
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
        }

        protected void GridView1_RowCancelingEdit(object sender, GridViewCancelEditEventArgs e)
        {
            GridView1.EditIndex = -1;
            DataTable dtOrder = (DataTable)Session["dtOrder"];
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
        }

        protected void GridView1_RowUpdating(object sender, GridViewUpdateEventArgs e)
        {
            DataTable dtOrder = (DataTable)Session["dtOrder"];
            TextBox txtAmount = (TextBox)GridView1.Rows[e.RowIndex].Cells[2].Controls[0];
            TextBox txtPrice = (TextBox)GridView1.Rows[e.RowIndex].Cells[3].Controls[0];
            GridView1.EditIndex = -1;
            dtOrder.Rows[Convert.ToInt32(e.RowIndex)]["Amount"] = txtAmount.Text;
            dtOrder.Rows[Convert.ToInt32(e.RowIndex)]["Sell_Price"] = txtPrice.Text;
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
            Session["dtOrder"] = dtOrder;
            TextBox1.Text = string.Empty;
            double pricetotal = 0;
            for (int i = 0; i < dtOrder.Rows.Count; i++)
            {
                pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
            }
            
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            // ⛔ ช่องค้นหาว่าง = ไม่มีอะไรให้เพิ่ม (เดิมค้นด้วยค่าว่างจะไปแมตช์สินค้าที่ไม่มีบาร์โค้ด
            //    แล้วเพิ่มสินค้านั้นเข้ารายการเองโดยผู้ใช้ไม่ได้สั่ง)
            string searchText = (TextBox1.Text ?? "").Trim();
            if (searchText.Length == 0) return;

            // SECURE: Product lookup with parameterized query
            var productParams = new Dictionary<string, object>
            {
                { "@ProductName", searchText },
                { "@Barcode", searchText }
            };

            // เทียบ Barcode เฉพาะแถวที่มีบาร์โค้ดจริง — สินค้าที่บาร์โค้ดว่าง/NULL ห้ามติดมากับการค้น
            DataTable dtProduct = code.DatabaseQuerySafe(conn,
                "SELECT * FROM [Taketime].[dbo].[Product] " +
                "WHERE [Product_Name] = @ProductName " +
                "   OR (Barcode = @Barcode AND LTRIM(RTRIM(ISNULL(Barcode,''))) <> '')",
                productParams);
            if (dtProduct.Rows.Count > 0)
            {
                DataTable dtOrder = (DataTable)Session["dtOrder"];
                if (dtOrder.Rows.Count == 0)
                {
                    int amount = Convert.ToInt32(TextBox2.Text);
                    double total = amount * Convert.ToDouble(TextBox3.Text);
                    dtOrder.Rows.Add(dtProduct.Rows[0]["ID"].ToString(), dtProduct.Rows[0]["Barcode"].ToString(), dtProduct.Rows[0]["Product_Name"].ToString(), amount, TextBox3.Text, total, dtProduct.Rows[0]["Category_ID"].ToString());

                }
                else
                {
                    int rowid = 0;
                    int checkdup = 0;
                    for (int i = 0; i < dtOrder.Rows.Count; i++)
                    {
                        if (dtOrder.Rows[i]["Product_Name"].ToString() == dtProduct.Rows[0]["Product_Name"].ToString())
                        {
                            checkdup = 1;
                            rowid = i;
                        }

                    }
                    if (checkdup == 0)
                    {
                        int amount = Convert.ToInt32(TextBox2.Text);
                        double total = amount * Convert.ToDouble(TextBox3.Text);
                        dtOrder.Rows.Add(dtProduct.Rows[0]["ID"].ToString(), dtProduct.Rows[0]["Barcode"].ToString(), dtProduct.Rows[0]["Product_Name"].ToString(), amount, TextBox3.Text, total, dtProduct.Rows[0]["Category_ID"].ToString());

                    }
                    else
                    {
                        int amount = Convert.ToInt32(dtOrder.Rows[rowid]["Amount"].ToString());
                        amount = amount + Convert.ToInt32(TextBox2.Text);
                        double total = amount * Convert.ToDouble(TextBox3.Text);
                        dtOrder.Rows[rowid]["Amount"] = amount;
                        dtOrder.Rows[rowid]["Price_Total"] = total;
                    }
                }
                GridView1.DataSource = dtOrder;
                GridView1.DataBind();
                Session["dtOrder"] = dtOrder;
                TextBox1.Text = string.Empty;
                double pricetotal = 0;
                for (int i = 0; i < dtOrder.Rows.Count; i++)
                {
                    pricetotal += Convert.ToDouble(dtOrder.Rows[i]["Price_Total"].ToString());
                }

            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ไม่มีรายชื่อสินค้าดังกล่าว');", true);
            }
        }

        // ──────────────────────────────────────────────
        // เอกสารซื้อ / การชำระ (PHASE19_31)
        // ──────────────────────────────────────────────

        private void LoadPurchaseOptions()
        {
            try
            {
                DataTable dtV = code.DatabaseQuerySafe(conn,
                    "SELECT ID, Name, ISNULL(IDNumber, '') AS IDNumber FROM Vendor WHERE Status = 'True' ORDER BY Name", null);
                if (dtV != null)
                    foreach (DataRow r in dtV.Rows)
                        ddlSupplierVendor.Items.Add(new ListItem(r["Name"].ToString(), r["ID"].ToString()));
            }
            catch { }
            try
            {
                DataTable dtPh = code.DatabaseQuerySafe(conn,
                    "SELECT ID, Paid_How FROM Account_Paid_How WHERE Status = 'True' ORDER BY Paid_How", null);
                if (dtPh != null)
                    foreach (DataRow r in dtPh.Rows)
                        ddlPaidHow.Items.Add(new ListItem(r["Paid_How"].ToString(), r["ID"].ToString()));
            }
            catch { }
            txtSupplierDocDate.Text = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

            var notes = new List<string>();
            bool hasCols = Take_Time_BangPhra.Purchasing.PurchaseService.HasPurchaseColumns(conn);
            if (!hasCols)
                notes.Add("⚠️ ยังไม่ได้รัน migration PHASE19_31 — บันทึกได้เฉพาะจำนวน/ราคา (ข้อมูลผู้ขาย/VAT/สถานะชำระยังไม่ถูกเก็บ)");
            try
            {
                var cfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                if (cfg.IsConfigured && cfg.Enabled)
                {
                    string src = Take_Time_BangPhra.Purchasing.PurchaseService.QtySource(cfg);
                    if (src == Take_Time_BangPhra.Purchasing.PurchaseService.QtySourceNextAccOcr)
                        notes.Add("ℹ️ จำนวนสินค้าที่ซื้อใช้ NextAcc (ใบซื้อ/OCR) เป็นหลัก — หน้านี้จะไม่ส่งจำนวนไป NextAcc; กรอกเลขที่ใบกำกับให้ตรงกับใบใน NextAcc เพื่อให้ระบบจับคู่ไม่รับซ้ำ");
                    if (cfg.IsStockInSkipJournal || cfg.IsVoucherLocal)
                        notes.Add("ℹ️ รับของไม่โพสต์ JE (invoice-only) — ใบสำคัญจ่าย/ใบกำกับที่ผูกการรับของจะเดบิตสินค้าคงเหลือแทน");
                }
            }
            catch { }
            litPurchaseInfo.Text = notes.Count == 0 ? "" :
                "<div style='background:#fff8e1;border:1px solid #ffe082;border-radius:6px;padding:8px 10px;margin-bottom:10px;font-size:13px;'>"
                + string.Join("<br/>", notes.ConvertAll(n => HttpUtility.HtmlEncode(n))) + "</div>";

            if (hasCols)
            {
                var dtB = Take_Time_BangPhra.Purchasing.PurchaseService.ListBatches(conn, false, 30);
                if (dtB != null && dtB.Rows.Count > 0)
                {
                    LoadNaBatchBadges(dtB);
                    gvRecentBatches.DataSource = dtB;
                    gvRecentBatches.DataBind();
                    pnlRecentBatches.Visible = true;
                }
            }
        }

        // ── ป้ายสถานะ NextAcc แบบเดียวทุกหน้า (CLAUDE.md ข้อ 24): JE รับของ (STOCK_IN, stockRef PIN-{สินค้า}-{เวลารับ}) รวมต่อการรับของ ──
        private readonly Dictionary<string, string> _naBatch = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        protected string NaBatchBadge(object batch)
        {
            string h;
            return _naBatch.TryGetValue(Convert.ToString(batch) ?? "", out h) ? h : "";
        }

        /// <summary>2 query: แถว Product_In ของการรับของที่แสดง + คิว STOCK_IN ของทุกแถว → ป้ายรวมต่อการรับของ</summary>
        private void LoadNaBatchBadges(DataTable batches)
        {
            try
            {
                var names = new List<string>();
                var ps = new Dictionary<string, object>();
                int k = 0;
                foreach (DataRow r in batches.Rows)
                {
                    string b = Convert.ToString(r["Batch"]);
                    if (string.IsNullOrEmpty(b) || k >= 200) continue;
                    names.Add("@b" + k); ps["@b" + k] = b; k++;
                }
                if (names.Count == 0) return;
                var lines = code.DatabaseQuerySafe(conn,
                    "SELECT Receipt_Batch, Product_ID, DateTime_In FROM Product_In WHERE Receipt_Batch IN (" + string.Join(",", names) + ")", ps);
                if (lines == null) return;
                // เวลารับของ = DateTime.Now ตอนบันทึก (ทศนิยมวินาทีอาจปัดใน SQL) → ลองวินาทีก่อนหน้าด้วย
                Func<object, DateTime, string> refOf = (pid, t) => "PIN-" + Convert.ToString(pid) + "-" + t.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                var perBatch = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
                var refs = new List<string>();
                foreach (DataRow r in lines.Rows)
                {
                    if (r["DateTime_In"] == DBNull.Value) continue;
                    DateTime t = Convert.ToDateTime(r["DateTime_In"]);
                    string a = refOf(r["Product_ID"], t), b2 = refOf(r["Product_ID"], t.AddSeconds(-1));
                    string batch = Convert.ToString(r["Receipt_Batch"]);
                    List<string[]> l;
                    if (!perBatch.TryGetValue(batch, out l)) perBatch[batch] = l = new List<string[]>();
                    l.Add(new[] { a, b2 });
                    refs.Add(a); refs.Add(b2);
                }
                var q = Take_Time_BangPhra.Integration.NextAccStatusData.LatestByPayloadKey(conn, "STOCK", new[] { "STOCK_IN" }, "stockRef", refs);
                foreach (var kv in perBatch)
                {
                    var badges = new List<Take_Time_BangPhra.Integration.NextAccUi.NaBadge>();
                    string firstRef = null;
                    foreach (var pair in kv.Value)
                    {
                        Take_Time_BangPhra.Integration.NextAccUi.NaQueueRow row;
                        if (!q.TryGetValue(pair[0], out row)) q.TryGetValue(pair[1], out row);
                        if (row == null) continue;
                        if (firstRef == null) firstRef = q.ContainsKey(pair[0]) ? pair[0] : pair[1];
                        badges.Add(Take_Time_BangPhra.Integration.NextAccUi.ResolveBadge(new Take_Time_BangPhra.Integration.NextAccUi.NaBadgeInput { Queue = row }));
                    }
                    if (badges.Count == 0)
                    {
                        _naBatch[kv.Key] = Take_Time_BangPhra.Integration.NextAccStatusData.Render(this, conn, new Take_Time_BangPhra.Integration.NextAccUi.NaBadgeInput
                        {
                            NoQueueMeansNotApplicable = true, NotApplicableLabel = "ไม่มีรายการบัญชี",
                            NotApplicableReason = "ไม่พบรายการส่ง JE รับของ — ตั้งค่าไม่ลง JE ตอนรับของ (ใบสำคัญจ่าย/ใบกำกับลงสินค้าแทน) หรือไม่มีต้นทุน"
                        }, kv.Key, null);
                        continue;
                    }
                    var agg = Take_Time_BangPhra.Integration.NextAccUi.AggregateBadges(badges);
                    // ป้ายรวม: ลิงก์ไปคิวด้วยเลขอ้างอิงแรก (เลขรับของไม่อยู่ใน payload)
                    string queueUrl = agg.State == Take_Time_BangPhra.Integration.NextAccUi.NaState.Sent ? null
                        : ResolveUrl("~" + Take_Time_BangPhra.Integration.NextAccUi.QueueUrl("", q: firstRef));
                    _naBatch[kv.Key] = Take_Time_BangPhra.Integration.NextAccUi.RenderBadge(agg,
                        new Take_Time_BangPhra.Integration.NextAccUi.NaBadgeLinks { QueueUrl = queueUrl });
                }
            }
            catch { }
        }

        protected void ddlSupplierVendor_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ddlSupplierVendor.SelectedValue)) return;
            try
            {
                var dt = code.DatabaseQuerySafe(conn,
                    "SELECT TOP 1 Name, ISNULL(IDNumber, '') AS IDNumber FROM Vendor WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", ddlSupplierVendor.SelectedValue } });
                if (dt != null && dt.Rows.Count > 0)
                {
                    txtSupplierName.Text = dt.Rows[0]["Name"].ToString();
                    txtSupplierTaxId.Text = dt.Rows[0]["IDNumber"].ToString();
                }
            }
            catch { }
        }

        private static decimal ParseDec(string s)
        {
            decimal d;
            return decimal.TryParse((s ?? "").Replace(",", "").Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out d) ? d : 0m;
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            DataTable dtOrder = (DataTable)Session["dtOrder"];
            if (dtOrder == null || dtOrder.Rows.Count == 0)
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ไม่มีข้อมูลนำเข้า');", true);
                return;
            }

            DateTime receiveDate = DateTime.Now;
            bool hasCols = Take_Time_BangPhra.Purchasing.PurchaseService.HasPurchaseColumns(conn);
            string supplierName = (txtSupplierName.Text ?? "").Trim();
            string supplierTaxId = System.Text.RegularExpressions.Regex.Replace(txtSupplierTaxId.Text ?? "", @"[^\d]", "");
            string supplierDocNo = (txtSupplierDocNo.Text ?? "").Trim();
            DateTime docDateParsed;
            DateTime? supplierDocDate = DateTime.TryParse(txtSupplierDocDate.Text, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out docDateParsed) ? (DateTime?)docDateParsed : null;
            string vatMode = ddlVatMode.SelectedValue ?? "NONE";
            string payStatus = ddlPaymentStatus.SelectedValue ?? "UNPAID";
            string paidHowId = ddlPaidHow.SelectedValue ?? "";

            if (hasCols && (payStatus == "PAID" || payStatus == "CREDIT") && supplierName.Length == 0)
            {
                ClientScript.RegisterStartupScript(this.GetType(), "nosupplier",
                    "alert('จ่ายแล้ว/ซื้อเชื่อ ต้องระบุผู้ขาย (ใช้ผูกใบสำคัญจ่ายและเจ้าหนี้)');", true);
                return;
            }
            if (hasCols && payStatus == "PAID" && string.IsNullOrEmpty(paidHowId))
            {
                ClientScript.RegisterStartupScript(this.GetType(), "nopaidhow",
                    "alert('จ่ายแล้ว ต้องเลือกแหล่งเงินที่จ่าย');", true);
                return;
            }

            // ราคาต่อหน่วยสุทธิ (ไม่รวม VAT) + VAT ของทั้งใบ
            var netUnits = new decimal[dtOrder.Rows.Count];
            decimal grossSum = 0m, netSum = 0m;
            for (int i = 0; i < dtOrder.Rows.Count; i++)
            {
                decimal price = ParseDec(dtOrder.Rows[i]["Sell_Price"]?.ToString());
                decimal qty = ParseDec(dtOrder.Rows[i]["Amount"]?.ToString());
                decimal net = vatMode == "INCL" ? Math.Round(price / 1.07m, 4, MidpointRounding.AwayFromZero) : price;
                netUnits[i] = net;
                grossSum += Math.Round(price * qty, 2, MidpointRounding.AwayFromZero);
                netSum += Math.Round(net * qty, 2, MidpointRounding.AwayFromZero);
            }
            decimal vatAmount;
            switch (vatMode)
            {
                case "EXCL": vatAmount = Math.Round(netSum * 0.07m, 2, MidpointRounding.AwayFromZero); break;
                case "INCL": vatAmount = Math.Round(grossSum - netSum, 2, MidpointRounding.AwayFromZero); break;
                case "MANUAL": vatAmount = Math.Round(ParseDec(txtVatAmount.Text), 2, MidpointRounding.AwayFromZero); break;
                default: vatAmount = 0m; break;
            }

            // ผู้ขายไม่มีเลขผู้เสียภาษี 13 หลัก = ไม่ใช่ใบกำกับเต็มรูป §86/4 → ภาษีซื้อเคลมไม่ได้ (§82/5(3))
            // → รวม VAT เข้าต้นทุนสินค้า (ไม่ตั้งภาษีซื้อ) ให้ GRNI/สินค้าคงเหลือ = ยอดที่จ่ายจริง และใบสำคัญจ่ายไม่มี VAT
            bool vatFolded = false;
            if (vatAmount > 0 && netSum > 0 && !Take_Time_BangPhra.Purchasing.PurchaseService.IsThaiTaxId(supplierTaxId))
            {
                decimal factor = (netSum + vatAmount) / netSum;
                for (int i = 0; i < netUnits.Length; i++)
                    netUnits[i] = Math.Round(netUnits[i] * factor, 4, MidpointRounding.AwayFromZero);
                vatAmount = 0m;
                vatFolded = true;
            }

            string batch = Take_Time_BangPhra.Purchasing.PurchaseService.NewReceiptBatchId(receiveDate);
            bool hasVendorCol = Take_Time_BangPhra.Purchasing.PurchaseService.HasColumn(conn, "Product_In", "Vendor_ID");
            int vendorId;
            bool hasVendor = int.TryParse(ddlSupplierVendor.SelectedValue, out vendorId) && vendorId > 0;

            // SECURE: INSERT Product_In with parameterized query
            for (int i = 0; i < dtOrder.Rows.Count; i++)
            {
                int productId = Convert.ToInt32(dtOrder.Rows[i]["ID"]);
                decimal qty = ParseDec(dtOrder.Rows[i]["Amount"]?.ToString());

                // ต้นทุนถัวเฉลี่ยเคลื่อนที่ — คำนวณก่อน insert (onHand ยังไม่รวมการรับนี้)
                if (qty > 0 && netUnits[i] > 0)
                    Take_Time_BangPhra.Inventory.CostService.ApplyReceipt(conn, productId, qty, netUnits[i], false);

                var insertParams = new Dictionary<string, object>
                {
                    { "@DateTimeIn", receiveDate },
                    { "@ProductID", productId },
                    { "@Amount", dtOrder.Rows[i]["Amount"].ToString() },
                    { "@PricePerUnit", netUnits[i] }
                };
                string cols = "[DateTime_In],[Product_ID],[Amount],[PricePerUnit]";
                string vals = "@DateTimeIn,@ProductID,@Amount,@PricePerUnit";
                if (hasCols)
                {
                    cols += ",[Supplier_Name],[Supplier_TaxId],[Supplier_Doc_No],[Supplier_Doc_Date],[Vat_Amount],[Payment_Status],[Receipt_Batch]";
                    vals += ",@SupName,@SupTax,@SupDoc,@SupDate,@Vat,@PayStatus,@Batch";
                    insertParams.Add("@SupName", supplierName.Length == 0 ? (object)DBNull.Value : supplierName);
                    insertParams.Add("@SupTax", supplierTaxId.Length == 0 ? (object)DBNull.Value : supplierTaxId);
                    insertParams.Add("@SupDoc", supplierDocNo.Length == 0 ? (object)DBNull.Value : supplierDocNo);
                    insertParams.Add("@SupDate", supplierDocDate.HasValue ? (object)supplierDocDate.Value.Date : DBNull.Value);
                    insertParams.Add("@Vat", vatAmount);
                    insertParams.Add("@PayStatus", payStatus);
                    insertParams.Add("@Batch", batch);
                }
                if (hasVendorCol && hasVendor)
                {
                    cols += ",[Vendor_ID]";
                    vals += ",@VendorID";
                    insertParams.Add("@VendorID", vendorId);
                }
                code.DatabaseInsertSafe(conn, $"INSERT INTO [dbo].[Product_In] ({cols}) VALUES ({vals})", insertParams);
            }

            // Sync ไป NextAcc: JE รับของ (Dr สินค้าคงเหลือ / Cr GRNI) + จำนวน (qty push) แยกกัน
            //   • SkipJournal / voucher LOCAL → ไม่มี JE แต่ยังส่งจำนวน (เดิมข้ามทั้งคู่)
            //   • Nexaacc_Purchase_Qty_Source=NEXTACC_OCR → ไม่ส่งจำนวน (ใบซื้อ/OCR ใน NextAcc รับเข้าเอง)
            try
            {
                var config = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                if (config.IsConfigured && config.Enabled)
                {
                    bool postJournal = !config.IsVoucherLocal && !config.IsStockInSkipJournal;
                    bool pushQty = Take_Time_BangPhra.Purchasing.PurchaseService.QtySource(config)
                        == Take_Time_BangPhra.Purchasing.PurchaseService.QtySourceTakeTime;
                    var sync = new Take_Time_BangPhra.Integration.AccountingSyncService(conn);
                    string supplierLabel = supplierName.Length > 0 ? supplierName : "ระบบจัดซื้อ TakeTime";
                    if (supplierDocNo.Length > 0) supplierLabel += " ใบ " + supplierDocNo;
                    for (int i = 0; i < dtOrder.Rows.Count; i++)
                    {
                        DataRow row = dtOrder.Rows[i];
                        int productId = Convert.ToInt32(row["ID"]);
                        string productName = row["Product_Name"]?.ToString() ?? "";
                        decimal qty = ParseDec(row["Amount"]?.ToString());
                        sync.EnqueueGoodsReceipt(productId, productName, qty, netUnits[i], receiveDate, supplierLabel,
                            $"PIN-{productId}-{receiveDate:yyyyMMddHHmmss}", postJournal, pushQty);
                    }
                }
            }
            catch (Exception accEx)
            {
                System.Diagnostics.Trace.TraceWarning($"⚠️ Stock-in accounting sync failed: {accEx.Message}");
                // Stock_In already saved — ไม่ throw เพื่อไม่กระทบ UX
            }

            dtOrder.Rows.Clear();
            Session["dtOrder"] = dtOrder;
            string msg = "✅ บันทึกการนำเข้าสินค้าเรียบร้อยแล้ว" + (hasCols ? "\\nเลขที่รับของ: " + batch : "")
                + (vatFolded ? "\\nℹ️ ไม่มีเลขผู้เสียภาษีผู้ขาย → VAT เคลมไม่ได้ ระบบรวม VAT เข้าต้นทุนสินค้าแล้ว" : "");
            string target = "/Product/In";
            if (hasCols && chkOpenVoucher.Checked && (payStatus == "PAID" || payStatus == "CREDIT"))
            {
                target = "/Account/PaymentVoucher?grn=" + HttpUtility.UrlEncode(batch)
                    + (payStatus == "PAID" && !string.IsNullOrEmpty(paidHowId) ? "&ph=" + HttpUtility.UrlEncode(paidHowId) : "")
                    + (payStatus == "CREDIT" ? "&credit=1" : "");
                msg += "\\nกำลังเปิดหน้าใบสำคัญจ่ายที่ผูกการรับของนี้ — ตรวจแล้วกดบันทึก";
            }
            ClientScript.RegisterStartupScript(this.GetType(), "success",
                "alert('" + msg.Replace("'", "\\'") + "'); window.location.href='" + target + "';", true);
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            DataTable dtOrder = new DataTable();
            dtOrder.Columns.Add("ID");
            dtOrder.Columns.Add("Barcode");
            dtOrder.Columns.Add("Product_Name");
            dtOrder.Columns.Add("Amount");
            dtOrder.Columns.Add("Sell_Price");
            dtOrder.Columns.Add("Price_Total");
            dtOrder.Columns.Add("Category_ID");
            Session["dtOrder"] = dtOrder;
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
            TextBox1.Text = string.Empty;
            TextBox2.Text = string.Empty;
            TextBox3.Text = string.Empty;
        }
    }
}