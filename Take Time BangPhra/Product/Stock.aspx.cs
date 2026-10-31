using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using System.IO;
using System.Text;
using Take_Time_BangPhra.Class;
using Take_Time_BangPhra.Inventory;

namespace Take_Time_BangPhra.Product
{
    /// <summary>
    /// จัดการสต็อก + บันทึกเบิกสินค้า/ใช้ภายใน (ไม่ใช่การขาย) ตามเหตุผล (Stock_Out_Reason — PHASE19_30):
    /// เลือกเหตุผล (บังคับ) → ต้นทุนจากสินค้า (ไม่ใช้ราคาที่พิมพ์) → แนบหลักฐานเมื่อเหตุผลกำหนด →
    /// รออนุมัติ (Owner/Admin) เมื่อเหตุผลกำหนด → แถว APPROVED เท่านั้นที่ลงบัญชี NextAcc (StockOutService).
    /// ห้ามเขียน Remark = 'ขาย' (POS daily rollup หยิบแถว Remark='ขาย' เป็นยอดขาย)
    /// </summary>
    public partial class Stock : System.Web.UI.Page
    {
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        AddressHelper addressHelper;

        private const string CartKey = "dtStockOut";
        private const string MsgKey = "StockOutMsg";
        private static readonly string[] EvidenceExt = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".pdf" };

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SalesStock)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            addressHelper = new AddressHelper(conn);
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

            // ดาวน์โหลดหลักฐาน (เก็บใต้ App_Data — IIS ไม่เสิร์ฟตรง) ?evidence={Product_Out.ID}
            if (!string.IsNullOrEmpty(Request.QueryString["evidence"]))
            {
                ServeEvidence(Request.QueryString["evidence"]);
                return;
            }

            try
            {
                DataTable dtIn = code.DatabaseQuery(conn, "SELECT Product.ID,Barcode,Category_ID,Product_Name,SUM(Amount) as Amount FROM [Taketime].[dbo].[Product] right join Product_In on Product.ID = Product_In.Product_ID group by Product.ID,Barcode,Category_ID,Product_Name order by Category_ID,Product_Name asc");
                DataTable dtOut = code.DatabaseQuery(conn, "SELECT Product.ID,Barcode,Category_ID,Product_Name,SUM(Amount) as Amount FROM [Taketime].[dbo].[Product] right join Product_Out on Product.ID = Product_Out.Product_ID group by Product.ID,Barcode,Category_ID,Product_Name order by Category_ID,Product_Name asc");
                for(int i = 0;i<dtIn.Rows.Count;i++)
                {
                    for (int j = 0; j < dtOut.Rows.Count; j++)
                    {
                        if (dtIn.Rows[i]["ID"].ToString() == dtOut.Rows[j]["ID"].ToString())
                        {
                            dtIn.Rows[i]["Amount"] = Convert.ToInt32(dtIn.Rows[i]["Amount"].ToString()) - Convert.ToInt32(dtOut.Rows[j]["Amount"].ToString());
                        }
                    }
                }
                // Store in ViewState for sorting
                ViewState["dtStock"] = dtIn;

                GridView2.DataSource = dtIn;
                GridView2.DataBind();

                // Load statistics
                LoadStatistics(dtIn);

                // Load predicted low stock items (will run out in 3-4 weeks based on usage rate)
                LoadPredictedLowStock();

                if (!IsPostBack)
                {
                    Session[CartKey] = NewCart();
                    BindReasons();
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

                    BindStockOutLists();
                    GridView1.DataSource = Cart();
                    GridView1.DataBind();

                    string flash = Session[MsgKey] as string;
                    if (!string.IsNullOrEmpty(flash))
                    {
                        Session.Remove(MsgKey);
                        ShowMsg(flash, flash.Contains("⚠"));
                    }
                }
                RenderReasonData();
            }
            catch (Exception ex) { ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('" + JsEsc(ex.Message) + "');", true); }
        }

        // ── เหตุผล ──────────────────────────────────────────────────────────

        private void BindReasons()
        {
            ddlReason.Items.Clear();
            ddlReason.Items.Add(new ListItem("— เลือกเหตุผลการเบิก —", ""));
            foreach (var r in StockOutReasonCatalog.ManualSelectable(conn))
            {
                string tag = r.DeemedSaleVat ? " • มีภาษีขาย" : "";
                ddlReason.Items.Add(new ListItem(r.NameTh + tag, r.Code));
            }

            if (!StockOutService.SchemaReady(conn))
            {
                litSchemaWarn.Text = "<div class='so-msg so-warn'>ยังไม่ได้รัน migration <b>PHASE19_30_Stock_Out_Reasons.sql</b> — " +
                    "ใช้เหตุผลค่าเริ่มต้นในโค้ด, ยังไม่มีขั้นตอนอนุมัติ/เก็บหลักฐานในฐานข้อมูล และยกเลิกรายการที่ลงบัญชีแล้วไม่ได้</div>";
            }
        }

        /// <summary>ข้อมูลเหตุผลสำหรับ JS (คำอธิบาย/ธง) — encode เป็น JSON ปลอดภัย</summary>
        private void RenderReasonData()
        {
            var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
            var map = new Dictionary<string, object>();
            foreach (var r in StockOutReasonCatalog.ManualSelectable(conn))
            {
                map[r.Code] = new Dictionary<string, object>
                {
                    { "help", r.HelpTh ?? "" }, { "vat", r.DeemedSaleVat }, { "base", r.VatBase ?? "MARKET" },
                    { "ap", r.RequiresApproval }, { "ev", r.RequiresEvidence }, { "gl", r.GlMappingKey ?? "" }
                };
            }
            string json = ser.Serialize(map).Replace("</", "<\\/");
            litReasonData.Text = "<script>window.soReasons = " + json + ";</script>";
        }

        // ── ตะกร้า ──────────────────────────────────────────────────────────

        private static DataTable NewCart()
        {
            var dt = new DataTable();
            foreach (var c in new[] { "ID", "Product_Name", "ReasonCode", "Reason_Name", "Amount", "Cost_PerUnit", "Cost_Source",
                                      "Cost_Total", "Market_PerUnit", "Vat_Est", "Remark", "NeedEvidence" })
                dt.Columns.Add(c);
            return dt;
        }

        private DataTable Cart()
        {
            var dt = Session[CartKey] as DataTable;
            if (dt == null || !dt.Columns.Contains("ReasonCode")) { dt = NewCart(); Session[CartKey] = dt; }
            return dt;
        }

        private void RecalcLine(DataRow row)
        {
            decimal qty = StockOutService.Dec(row["Amount"]);
            decimal cost = StockOutService.Dec(row["Cost_PerUnit"]);
            decimal market = StockOutService.Dec(row["Market_PerUnit"]);
            row["Cost_Total"] = Math.Round(qty * cost, 2).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var r = StockOutReasonCatalog.Get(conn, row["ReasonCode"]?.ToString());
            decimal vat = 0m;
            if (r != null && r.DeemedSaleVat && BusinessUsesVat())
            {
                decimal baseAmt = (r.IsMarketBase && market > 0) ? qty * market / 1.07m : qty * cost;
                vat = Math.Round(Math.Round(baseAmt, 2) * 0.07m, 2, MidpointRounding.AwayFromZero);
            }
            row["Vat_Est"] = vat.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        protected void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            DataTable dtOrder = Cart();
            int idx;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out idx) || idx < 0 || idx >= dtOrder.Rows.Count) return;
            var row = dtOrder.Rows[idx];
            if (e.CommandName == "Add")
            {
                row["Amount"] = (StockOutService.Dec(row["Amount"]) + 1).ToString("0");
                RecalcLine(row);
            }
            else if (e.CommandName == "Reduce")
            {
                decimal amount = StockOutService.Dec(row["Amount"]);
                if (amount > 1) { row["Amount"] = (amount - 1).ToString("0"); RecalcLine(row); }
            }
            else if (e.CommandName == "DeleteItem")
            {
                row.Delete();
                dtOrder.AcceptChanges();
            }
            Session[CartKey] = dtOrder;
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
            TextBox1.Text = string.Empty;
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            try
            {
                string searchText = (TextBox1.Text ?? "").Trim();
                if (searchText.Length == 0)
                {
                    Alert("กรุณาระบุชื่อสินค้าหรือบาร์โค้ด");
                    return;
                }
                string reasonCode = ddlReason.SelectedValue;
                var reason = StockOutReasonCatalog.Get(conn, reasonCode);
                if (reason == null || !reason.IsActive || StockOutReasonCatalog.IsSystemReason(reason.Code))
                {
                    Alert("กรุณาเลือกเหตุผลการเบิก");
                    return;
                }
                int amount;
                if (!int.TryParse(TextBox2.Text, out amount) || amount <= 0)
                {
                    Alert("จำนวนสินค้าต้องเป็นตัวเลขมากกว่า 0");
                    return;
                }

                // SECURE: Product lookup with parameterized query
                // เทียบ Barcode เฉพาะแถวที่มีบาร์โค้ดจริง — สินค้าที่บาร์โค้ดว่าง/NULL ห้ามติดมากับการค้น
                DataTable dtProduct = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM [Taketime].[dbo].[Product] " +
                    "WHERE [Product_Name] = @ProductName " +
                    "   OR (Barcode = @Barcode AND LTRIM(RTRIM(ISNULL(Barcode,''))) <> '')",
                    new Dictionary<string, object> { { "@ProductName", searchText }, { "@Barcode", searchText } });

                if (dtProduct == null || dtProduct.Rows.Count == 0)
                {
                    Alert("ไม่มีรายชื่อสินค้าดังกล่าว");
                    return;
                }

                int productId = Convert.ToInt32(dtProduct.Rows[0]["ID"]);
                var cost = new StockOutService(conn).GetCostInfo(productId);
                string remark = (TextBox4.Text ?? "").Trim();
                if (remark.Length > 300) remark = remark.Substring(0, 300);

                DataTable dtOrder = Cart();
                DataRow existing = dtOrder.Rows.Cast<DataRow>().FirstOrDefault(r =>
                    r["ID"].ToString() == productId.ToString() && r["ReasonCode"].ToString() == reason.Code);
                if (existing != null)
                {
                    existing["Amount"] = (StockOutService.Dec(existing["Amount"]) + amount).ToString("0");
                    if (remark.Length > 0) existing["Remark"] = remark;
                    RecalcLine(existing);
                }
                else
                {
                    var row = dtOrder.NewRow();
                    row["ID"] = productId.ToString();
                    row["Product_Name"] = dtProduct.Rows[0]["Product_Name"].ToString();
                    row["ReasonCode"] = reason.Code;
                    row["Reason_Name"] = reason.NameTh;
                    row["Amount"] = amount.ToString();
                    row["Cost_PerUnit"] = cost.CostPerUnit.ToString("0.00##", System.Globalization.CultureInfo.InvariantCulture);
                    row["Cost_Source"] = cost.CostSourceTh;
                    row["Market_PerUnit"] = cost.MarketPerUnit.ToString("0.00##", System.Globalization.CultureInfo.InvariantCulture);
                    row["Remark"] = remark;
                    row["NeedEvidence"] = reason.RequiresEvidence ? "1" : "0";
                    RecalcLine(row);
                    dtOrder.Rows.Add(row);
                }

                Session[CartKey] = dtOrder;
                GridView1.DataSource = dtOrder;
                GridView1.DataBind();
                TextBox1.Text = string.Empty;
                TextBox2.Text = string.Empty;

                if (cost.CostPerUnit <= 0)
                    ShowMsg("⚠ " + cost.ProductName + " ยังไม่มีต้นทุน (Cost_Price และราคาซื้อ = 0) — บันทึกได้และส่งจำนวนสต็อก แต่จะยังไม่ลงบัญชีมูลค่า" +
                        (reason.DeemedSaleVat ? " (ภาษีขายเสมือนขายยังคิดจากราคาขาย)" : ""), true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Button3_Click Error: {ex.Message}");
                Alert("เกิดข้อผิดพลาด: " + ex.Message);
            }
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            try
            {
                DataTable dtOrder = Cart();
                if (dtOrder.Rows.Count == 0)
                {
                    Alert("ไม่มีรายการสินค้าที่จะบันทึก");
                    return;
                }

                bool needEvidence = dtOrder.Rows.Cast<DataRow>().Any(r => r["NeedEvidence"]?.ToString() == "1");
                string evidencePath = null;
                if (fuEvidence.HasFile)
                {
                    string err;
                    evidencePath = SaveEvidence(out err);
                    if (evidencePath == null) { Alert(err); return; }
                }
                else if (needEvidence)
                {
                    Alert("มีรายการที่เหตุผลกำหนดให้แนบหลักฐาน (เช่น ภาพถ่ายของเสียหาย/บันทึกการทำลาย) — กรุณาเลือกไฟล์ก่อนบันทึก");
                    return;
                }

                string recordedBy = CurrentUserName();
                bool isOwner = string.Equals(Session["User"]?.ToString(), "Owner", StringComparison.OrdinalIgnoreCase);
                var svc = new StockOutService(conn);
                var lines = new List<string>();
                int ok = 0, pending = 0;
                DateTime now = DateTime.Now;

                foreach (DataRow r in dtOrder.Rows)
                {
                    int pid;
                    if (!int.TryParse(r["ID"]?.ToString(), out pid)) continue;
                    decimal qty = StockOutService.Dec(r["Amount"]);
                    string status, message;
                    long id = svc.Record(pid, qty, r["ReasonCode"]?.ToString(), r["Remark"]?.ToString(), evidencePath,
                        recordedBy, isOwner, now, out status, out message);
                    if (id > 0)
                    {
                        ok++;
                        if (status == StockOutService.StatusPending) pending++;
                        lines.Add($"#{id} {r["Product_Name"]} × {qty:N0} ({r["Reason_Name"]}) — {message}");
                    }
                    else
                    {
                        lines.Add($"✖ {r["Product_Name"]} ({r["Reason_Name"]}) — {message}");
                    }
                }

                if (ok > 0)
                {
                    Session[CartKey] = NewCart();
                    string head = $"✅ บันทึกการเบิก {ok} รายการ" + (pending > 0 ? $" (รออนุมัติ {pending})" : "");
                    Session[MsgKey] = head + "\n" + string.Join("\n", lines);
                    Response.Redirect("/Product/Stock", false);
                    Context.ApplicationInstance.CompleteRequest();
                }
                else
                {
                    ShowMsg("⚠ ไม่สามารถบันทึกรายการได้\n" + string.Join("\n", lines), true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Button2_Click Error: {ex.Message}");
                Alert("เกิดข้อผิดพลาด: " + ex.Message);
            }
        }

        /// <summary>
        /// Clear all items from cart
        /// </summary>
        protected void btnClear_Click(object sender, EventArgs e)
        {
            DataTable dtOrder = NewCart();
            Session[CartKey] = dtOrder;
            GridView1.DataSource = dtOrder;
            GridView1.DataBind();
            TextBox1.Text = string.Empty;
            TextBox2.Text = string.Empty;
            TextBox4.Text = string.Empty;
        }

        // ── อนุมัติ / ประวัติ ───────────────────────────────────────────────

        private void BindStockOutLists()
        {
            var svc = new StockOutService(conn);
            try
            {
                var pend = svc.LoadPending();
                pnlPending.Visible = pend != null && pend.Rows.Count > 0;
                gvPending.DataSource = pend;
                gvPending.DataBind();
            }
            catch (Exception ex) { pnlPending.Visible = false; System.Diagnostics.Trace.TraceWarning("Stock pending: " + ex.Message); }
            try
            {
                var hist = svc.LoadHistory(60);
                LoadNaQueue(hist);
                gvHistory.DataSource = hist;
                gvHistory.DataBind();
            }
            catch (Exception ex) { System.Diagnostics.Trace.TraceWarning("Stock history: " + ex.Message); }
        }

        protected void gvPending_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            long id;
            if (!long.TryParse(Convert.ToString(e.CommandArgument), out id)) return;
            var svc = new StockOutService(conn);
            string msg;
            string user = CurrentUserName();
            bool isOwner = string.Equals(Session["User"]?.ToString(), "Owner", StringComparison.OrdinalIgnoreCase);
            if (e.CommandName == "ApproveOut")
            {
                bool done = svc.Approve(id, user, isOwner, out msg);
                FlashAndReload((done ? "✅ อนุมัติ #" + id + " — " : "⚠ ") + msg);
            }
            else if (e.CommandName == "RejectOut")
            {
                bool done = svc.Reject(id, user, hfActionNote.Value, out msg);
                FlashAndReload((done ? "ปฏิเสธ #" + id + " — " : "⚠ ") + msg);
            }
        }

        protected void gvHistory_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            long id;
            if (!long.TryParse(Convert.ToString(e.CommandArgument), out id)) return;
            var svc = new StockOutService(conn);
            string msg;
            if (e.CommandName == "CancelOut")
            {
                bool done = svc.CancelOrDelete(id, CurrentUserName(), hfActionNote.Value, out msg);
                FlashAndReload((done ? "#" + id + " — " : "⚠ ") + msg);
            }
            else if (e.CommandName == "RepostOut")
            {
                msg = svc.PostToNextAcc(id);
                FlashAndReload("#" + id + " — " + msg);
            }
        }

        private void FlashAndReload(string msg)
        {
            Session[MsgKey] = msg;
            Response.Redirect("/Product/Stock", false);
            Context.ApplicationInstance.CompleteRequest();
        }

        // ── template helpers (ใช้ใน markup) ─────────────────────────────────

        protected string EvidenceLink(object id, object path)
        {
            string p = Convert.ToString(path);
            if (string.IsNullOrWhiteSpace(p)) return "";
            return "<a href='/Product/Stock?evidence=" + HttpUtility.UrlEncode(Convert.ToString(id)) + "' target='_blank'>📎 ดู</a>";
        }

        protected string PrintLink(object id, object reasonCode)
        {
            string c = Convert.ToString(reasonCode);
            if (c != "DAMAGE" && c != "EXPIRED" && c != "DESTROYED") return "";
            return "<a href='/Product/StockOutReport?ids=" + HttpUtility.UrlEncode(Convert.ToString(id)) + "' target='_blank'>🖨 รายงาน</a>";
        }

        protected string StatusBadge(object approval, object reversed, object reasonCode)
        {
            string a = Convert.ToString(approval).ToUpperInvariant();
            if (a == StockOutService.StatusRejected) return "<span class='so-badge so-REJECTED'>ปฏิเสธ</span>";
            if (IsTrue(reversed)) return "<span class='so-badge so-REVERSED'>ยกเลิกแล้ว</span>";
            if (string.IsNullOrEmpty(Convert.ToString(reasonCode))) return "<span class='so-badge so-LEGACY'>รายการเดิม</span>";
            if (a == StockOutService.StatusPending) return "<span class='so-badge so-PENDING'>รออนุมัติ</span>";
            return "<span class='so-badge so-APPROVED'>อนุมัติ</span>";
        }

        // ── ป้ายสถานะ NextAcc แบบเดียวทุกหน้า (CLAUDE.md ข้อ 24) — คิว STOCK_OUT_REASON_JOURNAL ของทั้งตาราง query เดียว ──
        private Dictionary<string, Take_Time_BangPhra.Integration.NextAccUi.NaQueueRow> _naQueue =
            new Dictionary<string, Take_Time_BangPhra.Integration.NextAccUi.NaQueueRow>(StringComparer.OrdinalIgnoreCase);

        private void LoadNaQueue(DataTable hist)
        {
            try
            {
                if (hist == null || hist.Rows.Count == 0) return;
                var refs = new List<string>();
                foreach (DataRow r in hist.Rows)
                {
                    long id;
                    if (!long.TryParse(Convert.ToString(r["ID"]), out id)) continue;
                    refs.Add(Take_Time_BangPhra.Integration.AccountingDataMapper.StockOutRef(id));
                    refs.Add(Take_Time_BangPhra.Integration.AccountingDataMapper.LegacyStockOutRef(id));
                }
                _naQueue = Take_Time_BangPhra.Integration.NextAccStatusData.LatestByPayloadKey(conn, "STOCK",
                    new[] { "STOCK_OUT_REASON_JOURNAL" }, "stockRef", refs);
            }
            catch { }
        }

        /// <summary>ป้ายสถานะ NextAcc ของรายการเบิก (คอลัมน์ "บัญชี")</summary>
        protected string NaBadge(object id, object approval, object reversed, object reasonCode, object journalRef)
        {
            long rowId;
            long.TryParse(Convert.ToString(id), out rowId);
            string j = Convert.ToString(journalRef);
            string code = Convert.ToString(reasonCode);
            string a = Convert.ToString(approval).ToUpperInvariant();
            var inp = new Take_Time_BangPhra.Integration.NextAccUi.NaBadgeInput { DocType = "JOURNAL", NotSentLabel = "ยังไม่ลงบัญชี" };
            if (IsTrue(reversed) || j.Contains("|REV:")) { inp.Voided = true; inp.VoidedLabel = "กลับรายการแล้ว"; }
            else if (string.IsNullOrEmpty(code)) { inp.NotApplicable = true; inp.NotApplicableLabel = "รายการเดิม"; inp.NotApplicableReason = "บันทึกก่อนมีเหตุผลการเบิก"; }
            else if (a == StockOutService.StatusRejected) { inp.NotApplicable = true; inp.NotApplicableLabel = "ปฏิเสธ — ไม่ลงบัญชี"; }
            else if (a == StockOutService.StatusPending) { inp.NotApplicable = true; inp.NotApplicableLabel = "รออนุมัติ — ยังไม่ส่ง"; inp.NotApplicableReason = "ลงบัญชี NextAcc เมื่ออนุมัติ"; }
            else if (StockOutReasonCatalog.IsSystemReason(code) && string.IsNullOrWhiteSpace(j))
            { inp.NotApplicable = true; inp.NotApplicableLabel = "ลงบัญชีที่หน้าคืนสินค้า"; }
            Take_Time_BangPhra.Integration.NextAccUi.NaQueueRow q = null;
            if (rowId > 0 && !_naQueue.TryGetValue(Take_Time_BangPhra.Integration.AccountingDataMapper.StockOutRef(rowId), out q))
                _naQueue.TryGetValue(Take_Time_BangPhra.Integration.AccountingDataMapper.LegacyStockOutRef(rowId), out q);
            inp.Queue = q;
            string head = string.IsNullOrWhiteSpace(j) ? "" : j.Split('|')[0].Trim();
            Guid g;
            if (Guid.TryParse(head, out g) && g != Guid.Empty) inp.DocId = g.ToString();
            return Take_Time_BangPhra.Integration.NextAccStatusData.Render(this, conn, inp,
                rowId > 0 ? Take_Time_BangPhra.Integration.AccountingDataMapper.StockOutRef(rowId) : null, null);
        }

        protected string JournalBadge(object journalRef)
        {
            string j = Convert.ToString(journalRef);
            if (string.IsNullOrWhiteSpace(j)) return "<span class='so-sub'>—</span>";
            if (j.Contains("|REV:")) return "<span class='so-sub'>กลับรายการแล้ว</span>";
            return "<span class='so-sub' title='" + HttpUtility.HtmlAttributeEncode(j) + "'>ลงแล้ว</span>";
        }

        protected bool CanRepost(object approval, object reversed, object reasonCode, object journalRef)
        {
            if (IsTrue(reversed)) return false;
            if (string.IsNullOrEmpty(Convert.ToString(reasonCode))) return false;
            if (StockOutReasonCatalog.IsSystemReason(Convert.ToString(reasonCode))) return false;   // คืนผู้ขาย ลงบัญชีที่หน้าคืนสินค้า
            string a = Convert.ToString(approval).ToUpperInvariant();
            if (a.Length > 0 && a != StockOutService.StatusApproved) return false;
            return string.IsNullOrWhiteSpace(Convert.ToString(journalRef));
        }

        /// <summary>แสดงปุ่มยกเลิก: ยังไม่ยกเลิก และไม่ใช่แถวคืนสินค้าผู้ขาย (ยกเลิกที่หน้าคืนสินค้า)</summary>
        protected bool CanCancel(object reversed, object reasonCode) =>
            !IsTrue(reversed) && !StockOutReasonCatalog.IsSystemReason(Convert.ToString(reasonCode));

        protected bool IsTrue(object o)
        {
            if (o == null || o == DBNull.Value) return false;
            if (o is bool) return (bool)o;
            string s = o.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        // ── หลักฐาน ─────────────────────────────────────────────────────────

        private string SaveEvidence(out string error)
        {
            error = null;
            try
            {
                string ext = (Path.GetExtension(fuEvidence.FileName) ?? "").ToLowerInvariant();
                if (Array.IndexOf(EvidenceExt, ext) < 0) { error = "ไฟล์หลักฐานต้องเป็น jpg, png, gif, webp, heic หรือ pdf"; return null; }
                if (fuEvidence.PostedFile.ContentLength > 10 * 1024 * 1024) { error = "ไฟล์หลักฐานต้องไม่เกิน 10MB"; return null; }
                string sub = DateTime.Now.ToString("yyyyMM");
                string folder = Server.MapPath("~/App_Data/StockOutEvidence/" + sub + "/");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string name = "SO_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ext;
                fuEvidence.SaveAs(Path.Combine(folder, name));
                return "App_Data/StockOutEvidence/" + sub + "/" + name;
            }
            catch (Exception ex)
            {
                error = "บันทึกไฟล์หลักฐานไม่สำเร็จ: " + ex.Message;
                return null;
            }
        }

        private void ServeEvidence(string idText)
        {
            long id;
            if (!long.TryParse(idText, out id)) { Response.StatusCode = 404; Response.End(); return; }
            var row = new StockOutService(conn).LoadRow(id);
            string rel = row?.EvidencePath;
            if (string.IsNullOrWhiteSpace(rel) || !rel.StartsWith("App_Data/StockOutEvidence/", StringComparison.OrdinalIgnoreCase) || rel.Contains(".."))
            {
                Response.StatusCode = 404; Response.End(); return;
            }
            string full = Server.MapPath("~/" + rel);
            if (!File.Exists(full)) { Response.StatusCode = 404; Response.End(); return; }
            string ext = Path.GetExtension(full).ToLowerInvariant();
            string mime = ext == ".pdf" ? "application/pdf" : ext == ".png" ? "image/png" : ext == ".gif" ? "image/gif"
                : ext == ".webp" ? "image/webp" : ext == ".heic" ? "image/heic" : "image/jpeg";
            Response.Clear();
            Response.ContentType = mime;
            Response.AddHeader("Content-Disposition", "inline; filename=\"" + Path.GetFileName(full) + "\"");
            Response.AddHeader("X-Content-Type-Options", "nosniff");
            Response.TransmitFile(full);
            Response.End();
        }

        // ── misc ────────────────────────────────────────────────────────────

        private string CurrentUserName()
        {
            string n = Session["UserName"]?.ToString();
            if (string.IsNullOrWhiteSpace(n)) n = Session["username"]?.ToString();
            if (string.IsNullOrWhiteSpace(n)) n = "UserID:" + (Session["UserID"]?.ToString() ?? "?");
            return n;
        }

        private bool BusinessUsesVat()
        {
            try
            {
                var dt = code.DatabaseQuerySafe(conn, "SELECT TOP 1 Use_Vat FROM Business_Info", null);
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Use_Vat"] != DBNull.Value)
                {
                    string v = dt.Rows[0]["Use_Vat"].ToString();
                    return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return false;
        }

        private void ShowMsg(string msg, bool warn)
        {
            pnlStockOutMsg.Visible = true;
            divStockOutMsg.Attributes["class"] = warn ? "so-msg so-warn" : "so-msg";
            litStockOutMsg.Text = HttpUtility.HtmlEncode(msg);
        }

        private void Alert(string msg)
        {
            ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('" + JsEsc(msg) + "');", true);
        }

        private static string JsEsc(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", " ").Replace("\n", "\\n").Replace("</", "<\\/");

        /// <summary>
        /// Handle GridView2 sorting
        /// </summary>
        protected void GridView2_Sorting(object sender, GridViewSortEventArgs e)
        {
            DataTable dtStock = ViewState["dtStock"] as DataTable;
            if (dtStock != null)
            {
                string sortDirection = GetSortDirection(e.SortExpression);
                DataView dv = new DataView(dtStock);
                dv.Sort = e.SortExpression + " " + sortDirection;
                GridView2.DataSource = dv;
                GridView2.DataBind();
            }
        }

        /// <summary>
        /// Toggle sort direction
        /// </summary>
        private string GetSortDirection(string column)
        {
            string sortDirection = "ASC";
            string lastSortExpression = ViewState["SortExpression"] as string;

            if (lastSortExpression != null && lastSortExpression == column)
            {
                string lastSortDirection = ViewState["SortDirection"] as string;
                if (lastSortDirection != null && lastSortDirection == "ASC")
                {
                    sortDirection = "DESC";
                }
            }

            ViewState["SortDirection"] = sortDirection;
            ViewState["SortExpression"] = column;
            return sortDirection;
        }

        /// <summary>
        /// Load stock statistics into stat cards
        /// </summary>
        private void LoadStatistics(DataTable dtStock)
        {
            try
            {
                int totalProducts = dtStock.Rows.Count;
                int normalStock = 0;
                int lowStock = 0;
                int criticalStock = 0;

                foreach (DataRow row in dtStock.Rows)
                {
                    int amount = Convert.ToInt32(row["Amount"]);
                    if (amount < 5)
                    {
                        criticalStock++;
                    }
                    else
                    {
                        normalStock++;
                    }
                }

                // Get predicted low stock count
                string query = @"
                    SELECT COUNT(*) as LowCount FROM (
                        SELECT p.ID,
                            ISNULL((SELECT SUM(Amount) FROM Product_In WHERE Product_ID = p.ID), 0) -
                            ISNULL((SELECT SUM(Amount) FROM Product_Out WHERE Product_ID = p.ID), 0) as CurrentStock,
                            ISNULL((SELECT SUM(Amount) FROM Product_Out
                                    WHERE Product_ID = p.ID
                                    AND DateTime_Out >= DATEADD(WEEK, -8, GETDATE())), 0) / 8.0 as WeeklyUsage
                        FROM Product p WHERE p.Status = 'True'
                    ) AS StockAnalysis
                    WHERE CurrentStock > 0 AND WeeklyUsage > 0 AND (CurrentStock / WeeklyUsage) <= 4";

                DataTable dtLow = code.DatabaseQuery(conn, query);
                if (dtLow != null && dtLow.Rows.Count > 0)
                {
                    lowStock = Convert.ToInt32(dtLow.Rows[0]["LowCount"]);
                }

                lblTotalProducts.Text = totalProducts.ToString();
                lblNormalStock.Text = normalStock.ToString();
                lblLowStock.Text = lowStock.ToString();
                lblCriticalStock.Text = criticalStock.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadStatistics Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Load predicted low stock items based on usage rate
        /// Formula: WeeksRemaining = CurrentStock / WeeklyUsageRate
        /// Show items where WeeksRemaining <= 4 weeks
        /// </summary>
        private void LoadPredictedLowStock()
        {
            try
            {
                // Calculate based on average weekly usage over the last 8 weeks
                // Use subquery with WHERE instead of HAVING (HAVING requires GROUP BY)
                string query = @"
                    SELECT * FROM (
                        SELECT
                            p.Product_Name,
                            -- Current stock level
                            ISNULL((SELECT SUM(Amount) FROM Product_In WHERE Product_ID = p.ID), 0) -
                            ISNULL((SELECT SUM(Amount) FROM Product_Out WHERE Product_ID = p.ID), 0) as CurrentStock,
                            -- Average weekly usage (last 8 weeks)
                            ISNULL((SELECT SUM(Amount) FROM Product_Out
                                    WHERE Product_ID = p.ID
                                    AND DateTime_Out >= DATEADD(WEEK, -8, GETDATE())), 0) / 8.0 as WeeklyUsage,
                            -- Weeks remaining
                            CASE
                                WHEN ISNULL((SELECT SUM(Amount) FROM Product_Out
                                        WHERE Product_ID = p.ID
                                        AND DateTime_Out >= DATEADD(WEEK, -8, GETDATE())), 0) / 8.0 > 0
                                THEN (ISNULL((SELECT SUM(Amount) FROM Product_In WHERE Product_ID = p.ID), 0) -
                                      ISNULL((SELECT SUM(Amount) FROM Product_Out WHERE Product_ID = p.ID), 0)) /
                                     (ISNULL((SELECT SUM(Amount) FROM Product_Out
                                            WHERE Product_ID = p.ID
                                            AND DateTime_Out >= DATEADD(WEEK, -8, GETDATE())), 0) / 8.0)
                                ELSE 999
                            END as WeeksRemaining
                        FROM Product p
                        WHERE p.Status = 'True'
                    ) AS StockAnalysis
                    WHERE CurrentStock > 0
                      AND WeeklyUsage > 0
                      AND WeeksRemaining <= 4
                    ORDER BY WeeksRemaining ASC";

                DataTable dtPredicted = code.DatabaseQuery(conn, query);

                if (dtPredicted != null && dtPredicted.Rows.Count > 0)
                {
                    gvPredictedLowStock.DataSource = dtPredicted;
                    gvPredictedLowStock.DataBind();
                    pnlPredictedLowStock.Visible = true;
                }
                else
                {
                    pnlPredictedLowStock.Visible = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadPredictedLowStock Error: {ex.Message}");
                pnlPredictedLowStock.Visible = false;
            }
        }
    }
}
