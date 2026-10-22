using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Purchasing;

namespace Take_Time_BangPhra.Product
{
    /// <summary>
    /// คืนสินค้าให้ผู้ขาย / ใบลดหนี้จากผู้ขาย (PHASE19_31).
    ///   • ตัดจำนวน: Product_Out (OutType RETURN_TO_VENDOR, Purchase_Return_Ref, Return_Receipt_Batch) + qty push OUT
    ///   • JE PURCHASE_RETURN: Dr GRNI/เจ้าหนี้/แหล่งเงิน / Cr สินค้าคงเหลือ (ค่าเฉลี่ย) / Cr ภาษีซื้อ (ถ้ากลับ) / ผลต่าง PURCHASE_RETURNS
    ///   • idempotent: เลขที่คืน (PRT-…) สร้างครั้งเดียวต่อฟอร์ม (ViewState) — กดซ้ำ/รีเฟรชไม่บันทึกซ้ำ; คิว/JE ใช้ ref เดียวกัน
    /// </summary>
    public partial class PurchaseReturn : Page
    {
        private readonly code db = new code();
        private readonly string conn = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SalesStock)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
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
                ViewState["ReturnRef"] = PurchaseService.NewReturnRef(DateTime.Now);
                txtReturnDate.Text = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                LoadPaidHow();
                bool ready = PurchaseService.HasPurchaseColumns(conn) && PurchaseService.HasReturnColumns(conn);
                if (!ready)
                {
                    litInfo.Text = Box("⚠️ ยังไม่ได้รัน migration PHASE19_31 — ยังคืนสินค้าผ่านหน้านี้ไม่ได้", "#fff3e0");
                    btnSave.Enabled = false;
                    btnPreview.Enabled = false;
                    return;
                }
                LoadBatches(Request.QueryString["batch"]);
                BindLines();
                BindRecent();
                if (Request.QueryString["saved"] == "1")
                    ClientScript.RegisterStartupScript(GetType(), "savedok",
                        "alert('" + ("✅ บันทึกคืนสินค้าแล้ว " + (Request.QueryString["ref"] ?? "")).Replace("'", "\\'") + "');", true);
            }
        }

        private static string Box(string text, string bg) =>
            "<div style='background:" + bg + ";border:1px solid #ffe0b2;border-radius:6px;padding:8px 10px;margin:8px 0;font-size:13px;'>"
            + text + "</div>";

        private void LoadPaidHow()
        {
            try
            {
                var dt = db.DatabaseQuerySafe(conn, "SELECT Paid_How FROM Account_Paid_How WHERE Status = 'True' ORDER BY Paid_How", null);
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        ddlRefundPaidHow.Items.Add(new ListItem(r["Paid_How"].ToString(), r["Paid_How"].ToString()));
            }
            catch { }
        }

        private void LoadBatches(string preselect)
        {
            ddlBatch.Items.Clear();
            ddlBatch.Items.Add(new ListItem("— เลือกการรับของ —", ""));
            var dt = PurchaseService.ListBatches(conn, false, 300);
            if (dt != null)
                foreach (DataRow r in dt.Rows)
                {
                    DateTime at = r["Received_At"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["Received_At"]);
                    decimal net = r["Net_Total"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Net_Total"]);
                    ddlBatch.Items.Add(new ListItem(
                        $"{r["Batch"]} · {at:dd/MM/yy} · {r["Supplier_Name"]} · ใบ {r["Supplier_Doc_No"]} · {net:N2}", r["Batch"].ToString()));
                }
            if (!string.IsNullOrEmpty(preselect) && ddlBatch.Items.FindByValue(preselect) != null)
                ddlBatch.SelectedValue = preselect;
        }

        protected void ddlBatch_SelectedIndexChanged(object sender, EventArgs e)
        {
            litPreview.Text = "";
            BindLines();
        }

        private ReceiptBatchInfo CurrentBatch() =>
            string.IsNullOrEmpty(ddlBatch.SelectedValue) ? null : PurchaseService.GetBatch(conn, ddlBatch.SelectedValue);

        private void BindLines()
        {
            var info = CurrentBatch();
            var dt = new DataTable();
            dt.Columns.Add("ProductId", typeof(int));
            dt.Columns.Add("ProductName", typeof(string));
            dt.Columns.Add("Qty", typeof(decimal));
            dt.Columns.Add("ReturnedQty", typeof(decimal));
            dt.Columns.Add("ReturnableQty", typeof(decimal));
            dt.Columns.Add("UnitCost", typeof(decimal));
            litBatchInfo.Text = "";
            if (info != null)
            {
                foreach (var l in info.Lines)
                    dt.Rows.Add(l.ProductId, l.ProductName, l.Qty, l.ReturnedQty, l.ReturnableQty, l.UnitCost);
                string st = info.PaymentStatus == "PAID" ? "จ่ายแล้ว" : info.PaymentStatus == "CREDIT" ? "ซื้อเชื่อ" : "ยังไม่วางบิล/ยังไม่จ่าย";
                litBatchInfo.Text = Box(HttpUtility.HtmlEncode(
                    $"ผู้ขาย {info.SupplierName} (เลขภาษี {info.SupplierTaxId}) · ใบ {info.SupplierDocNo} · สุทธิ {info.NetTotal:N2} · VAT {info.VatAmount:N2} · {st}"
                    + (info.IsLinked ? $" · ผูกใบสำคัญจ่าย {info.LinkedVoucherId}" : " · ยังไม่ผูกใบสำคัญจ่าย")), "#e8f5e9");
                // กลับภาษีซื้อ: ค่าเริ่มต้น = เคยเคลมแล้ว (ผูกใบสำคัญจ่าย + มี VAT + ผู้ขายมีเลขภาษี)
                chkVatReversal.Checked = info.IsLinked && info.VatLikelyClaimable;
            }
            gvLines.DataSource = dt;
            gvLines.DataBind();
        }

        private void BindRecent()
        {
            try
            {
                var dt = db.DatabaseQuerySafe(conn,
                    @"SELECT TOP 30 po.Purchase_Return_Ref, MAX(po.Return_Receipt_Batch) AS Return_Receipt_Batch,
                             MIN(po.DateTime_Out) AS Returned_At, COUNT(*) AS Items,
                             CAST(SUM(CAST(po.Amount AS DECIMAL(18,4)) * CAST(po.PricePerUnit AS DECIMAL(18,4))) AS DECIMAL(18,2)) AS Net_Total,
                             MAX(po.Remark) AS Reason
                      FROM Product_Out po WHERE po.Purchase_Return_Ref IS NOT NULL
                      GROUP BY po.Purchase_Return_Ref ORDER BY MIN(po.DateTime_Out) DESC", null);
                if (dt != null && dt.Rows.Count > 0)
                {
                    gvRecent.DataSource = dt;
                    gvRecent.DataBind();
                    pnlRecent.Visible = true;
                }
            }
            catch { }
        }

        /// <summary>อ่านจำนวนที่คืนจาก grid + ตรวจไม่เกินที่คืนได้/คงเหลือ. คืนข้อความผิดพลาด หรือ null</summary>
        private string CollectLines(ReceiptBatchInfo info, List<Tuple<ReceiptLine, decimal>> picked)
        {
            if (info == null) return "กรุณาเลือกการรับของ";
            var byId = new Dictionary<int, ReceiptLine>();
            foreach (var l in info.Lines) byId[l.ProductId] = l;
            for (int i = 0; i < gvLines.Rows.Count; i++)
            {
                var tb = gvLines.Rows[i].FindControl("txtReturnQty") as TextBox;
                decimal q;
                if (tb == null || !decimal.TryParse((tb.Text ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out q) || q <= 0) continue;
                int pid = Convert.ToInt32(gvLines.DataKeys[i].Value);
                ReceiptLine line;
                if (!byId.TryGetValue(pid, out line)) continue;
                if (q > line.ReturnableQty + 0.0001m)
                    return $"{line.ProductName}: คืนได้อีกไม่เกิน {line.ReturnableQty:N2}";
                decimal onHand = Take_Time_BangPhra.Inventory.CostService.OnHand(conn, pid);
                if (q > onHand + 0.0001m)
                    return $"{line.ProductName}: คงเหลือในสต๊อก {onHand:N2} น้อยกว่าจำนวนที่คืน";
                picked.Add(Tuple.Create(line, q));
            }
            return picked.Count == 0 ? "กรุณาใส่จำนวนที่คืนอย่างน้อย 1 รายการ" : null;
        }

        private PurchaseReturnPlan BuildPlan(ReceiptBatchInfo info, List<Tuple<ReceiptLine, decimal>> picked, bool applyCost,
            List<Take_Time_BangPhra.Integration.AccountingSyncService.PurchaseReturnLine> outLines)
        {
            decimal purchaseNet = 0m, carrying = 0m;
            foreach (var p in picked)
            {
                decimal unitCarry = applyCost
                    ? Take_Time_BangPhra.Inventory.CostService.ApplyReturn(conn, p.Item1.ProductId, p.Item2, p.Item1.UnitCost)
                    : Take_Time_BangPhra.Inventory.CostService.CurrentAverage(conn, p.Item1.ProductId);
                if (unitCarry <= 0) unitCarry = p.Item1.UnitCost;
                purchaseNet += Math.Round(p.Item2 * p.Item1.UnitCost, 2, MidpointRounding.AwayFromZero);
                carrying += Math.Round(p.Item2 * unitCarry, 2, MidpointRounding.AwayFromZero);
                if (outLines != null)
                    outLines.Add(new Take_Time_BangPhra.Integration.AccountingSyncService.PurchaseReturnLine
                    {
                        ProductId = p.Item1.ProductId, ProductName = p.Item1.ProductName, Quantity = p.Item2, CarryingUnitCost = unitCarry
                    });
            }

            string clearingKey = "GRNI";
            bool voucherLocal = false;
            try
            {
                var cfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                clearingKey = PurchaseService.ClearingAccountKey(cfg);
                voucherLocal = cfg.IsVoucherLocal;
            }
            catch { }
            var plan = PurchaseService.PlanReturn(clearingKey, info.IsLinked, ddlSettlement.SelectedValue,
                purchaseNet, carrying, info.NetTotal, info.VatAmount, chkVatReversal.Checked);
            if (voucherLocal)
            {
                plan.CounterKey = "NONE";
                plan.Explanation = "ใบสำคัญจ่ายตั้งเป็น LOCAL (รับของ/ใบซื้อไม่ได้ลงบัญชี NextAcc) → ตัดจำนวนอย่างเดียว ไม่มี JE";
            }
            return plan;
        }

        private string RenderPlan(PurchaseReturnPlan plan)
        {
            string counterLabel = plan.CounterKey == "CASH" ? "แหล่งเงิน (" + ddlRefundPaidHow.SelectedValue + ")"
                : plan.CounterKey == "ACCOUNTS_PAYABLE" ? "เจ้าหนี้การค้า" : plan.CounterKey == "GRNI" ? "GRNI พักรับของ" : "-";
            var sb = new System.Text.StringBuilder();
            sb.Append("<div style='background:#f1f8e9;border:1px solid #c5e1a5;border-radius:6px;padding:10px;margin-top:10px;font-size:13px;'>");
            sb.Append("<b>").Append(HttpUtility.HtmlEncode(plan.Explanation)).Append("</b>");
            if (plan.PostsJournal)
            {
                sb.Append("<table style='margin-top:6px;border-collapse:collapse;'>");
                Action<string, decimal, decimal> row = (acc, dr, cr) =>
                    sb.Append("<tr><td style='padding:2px 12px 2px 0'>").Append(HttpUtility.HtmlEncode(acc)).Append("</td><td style='text-align:right;padding:2px 12px'>")
                      .Append(dr > 0 ? dr.ToString("N2") : "").Append("</td><td style='text-align:right;padding:2px 12px'>").Append(cr > 0 ? cr.ToString("N2") : "").Append("</td></tr>");
                sb.Append("<tr><th style='text-align:left'>บัญชี</th><th>เดบิต</th><th>เครดิต</th></tr>");
                row("Dr " + counterLabel, plan.CounterAmount, 0);
                row("Cr สินค้าคงเหลือ (ต้นทุนเฉลี่ย)", 0, plan.CarryingCost);
                if (plan.VatReversal > 0) row("Cr ภาษีซื้อ (กลับรายการ)", 0, plan.VatReversal);
                if (plan.Difference > 0) row("Cr ส่งคืนสินค้า (ผลต่าง)", 0, plan.Difference);
                if (plan.Difference < 0) row("Dr ส่งคืนสินค้า (ผลต่าง)", -plan.Difference, 0);
                sb.Append("</table>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        protected void btnPreview_Click(object sender, EventArgs e)
        {
            var info = CurrentBatch();
            var picked = new List<Tuple<ReceiptLine, decimal>>();
            string err = CollectLines(info, picked);
            if (err != null) { Alert(err); return; }
            litPreview.Text = RenderPlan(BuildPlan(info, picked, false, null));
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (!PurchaseService.HasPurchaseColumns(conn) || !PurchaseService.HasReturnColumns(conn)) { Alert("ยังไม่ได้รัน migration PHASE19_31"); return; }
            string returnRef = ViewState["ReturnRef"] as string;
            if (string.IsNullOrEmpty(returnRef)) { returnRef = PurchaseService.NewReturnRef(DateTime.Now); ViewState["ReturnRef"] = returnRef; }

            // กันบันทึกซ้ำ (ดับเบิลคลิก/รีโพสต์ฟอร์มเดิม)
            try
            {
                var dtDup = db.DatabaseQuerySafe(conn, "SELECT TOP 1 1 FROM Product_Out WHERE Purchase_Return_Ref = @r",
                    new Dictionary<string, object> { { "@r", returnRef } });
                if (dtDup != null && dtDup.Rows.Count > 0) { Alert("การคืน " + returnRef + " บันทึกไปแล้ว"); return; }
            }
            catch { }

            var info = CurrentBatch();
            var picked = new List<Tuple<ReceiptLine, decimal>>();
            string err = CollectLines(info, picked);
            if (err != null) { Alert(err); return; }
            if (ddlSettlement.SelectedValue == "REFUND" && string.IsNullOrEmpty(ddlRefundPaidHow.SelectedValue))
            { Alert("ผู้ขายคืนเงิน: กรุณาเลือกแหล่งเงินที่รับคืน"); return; }
            DateTime returnDate;
            if (!DateTime.TryParse(txtReturnDate.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out returnDate)) returnDate = DateTime.Now;
            else returnDate = returnDate.Date.Add(DateTime.Now.TimeOfDay);
            string reason = (txtReason.Text ?? "").Trim();
            string cnNo = (txtSupplierCnNo.Text ?? "").Trim();

            var outLines = new List<Take_Time_BangPhra.Integration.AccountingSyncService.PurchaseReturnLine>();
            var plan = BuildPlan(info, picked, true, outLines);

            bool hasOutType = PurchaseService.HasColumn(conn, "Product_Out", "OutType");
            bool hasReason = PurchaseService.HasColumn(conn, "Product_Out", "Reason");
            bool hasRollupRef = PurchaseService.HasColumn(conn, "Product_Out", "Pos_Rollup_Ref");
            bool hasOutReason = PurchaseService.HasColumn(conn, "Product_Out", "Out_Reason_Code");
            bool hasRecordedBy = PurchaseService.HasColumn(conn, "Product_Out", "Recorded_By");
            string remark = ("คืนผู้ขาย " + returnRef + (reason.Length > 0 ? " - " + reason : ""));
            if (remark.Length > 200) remark = remark.Substring(0, 200);

            foreach (var p in picked)
            {
                // Remark ไม่ใช่ 'ขาย' → POS daily rollup ไม่หยิบ; Pos_Rollup_Ref กันอีกชั้น
                string cols = "[DateTime_Out],[Product_ID],[Amount],[PricePerUnit],[Account_Receipt_ID],[Remark],[Purchase_Return_Ref],[Return_Receipt_Batch]";
                string vals = "@d,@pid,@amt,@price,'0',@remark,@ref,@batch";
                var prm = new Dictionary<string, object>
                {
                    { "@d", returnDate }, { "@pid", p.Item1.ProductId }, { "@amt", p.Item2 }, { "@price", p.Item1.UnitCost },
                    { "@remark", remark }, { "@ref", returnRef }, { "@batch", info.Batch }
                };
                if (hasOutType) { cols += ",[OutType]"; vals += ",N'RETURN_TO_VENDOR'"; }
                if (hasReason) { cols += ",[Reason]"; vals += ",@reason"; prm["@reason"] = reason.Length > 0 ? (object)reason : DBNull.Value; }
                if (hasRollupRef) { cols += ",[Pos_Rollup_Ref]"; vals += ",N'RETURN'"; }
                if (hasOutReason) { cols += ",[Out_Reason_Code]"; vals += ",N'RETURN_TO_VENDOR'"; }
                if (hasRecordedBy) { cols += ",[Recorded_By]"; vals += ",@by"; prm["@by"] = Session["User"]?.ToString() ?? ""; }
                db.DatabaseInsertSafe(conn, $"INSERT INTO [dbo].[Product_Out] ({cols}) VALUES ({vals})", prm);
            }

            try
            {
                var cfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                if (cfg.IsConfigured && cfg.Enabled)
                {
                    var sync = new Take_Time_BangPhra.Integration.AccountingSyncService(conn);
                    string paidHow = plan.CounterKey == "CASH" ? ddlRefundPaidHow.SelectedValue : null;
                    string paidHowAccId = string.IsNullOrEmpty(paidHow) ? null : sync.LookupPaidHowAccountId(paidHow);
                    sync.EnqueuePurchaseReturn(returnRef, info.Batch, info.SupplierName, cnNo, returnDate,
                        plan.CounterKey, plan.PurchaseNet, plan.CarryingCost, plan.VatReversal,
                        paidHow, paidHowAccId, outLines, true, reason);
                }
            }
            catch (Exception ex)
            {
                try { db.Logs(conn, "Purchase", $"PurchaseReturn sync enqueue failed ref={returnRef}: {ex.Message}", "SYSTEM"); } catch { }
            }
            try
            {
                db.Logs(conn, "Purchase", $"Purchase return {returnRef} batch={info.Batch} net={plan.PurchaseNet:N2} carrying={plan.CarryingCost:N2} vat={plan.VatReversal:N2} counter={plan.CounterKey}",
                    Session["User"]?.ToString() ?? "SYSTEM");
            }
            catch { }

            ViewState["ReturnRef"] = null;
            Response.Redirect("/Product/PurchaseReturn?saved=1&ref=" + HttpUtility.UrlEncode(returnRef) + "&batch=" + HttpUtility.UrlEncode(info.Batch), false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void Alert(string msg)
        {
            ClientScript.RegisterStartupScript(GetType(), "pralert", "alert('" + (msg ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n") + "');", true);
        }
    }
}
