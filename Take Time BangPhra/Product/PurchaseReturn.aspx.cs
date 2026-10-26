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
                pnlCnMode.Visible = Vcs.CnPrimary();
                if (pnlCnMode.Visible) LoadUnlinkedCns();
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
            if (pnlCnMode.Visible) LoadUnlinkedCns();
        }

        // ── PHASE19_39: ใบลดหนี้ผู้ขาย = ตัวจริง ─────────────────────────────

        private VendorCnService _vcs;
        private VendorCnService Vcs => _vcs ?? (_vcs = new VendorCnService(conn));

        /// <summary>ใบลดหนี้จากหน้า OCR ที่ยังไม่ผูกการคืน (ผู้ขายของการรับของนี้ก่อน)</summary>
        private void LoadUnlinkedCns()
        {
            string keep = ddlLinkCn.SelectedValue;
            ddlLinkCn.Items.Clear();
            ddlLinkCn.Items.Add(new ListItem("— เลือกใบลดหนี้ที่บันทึกแล้ว —", ""));
            var info = CurrentBatch();
            foreach (var c in Vcs.ListUnlinkedCns(info?.SupplierTaxId, info?.SupplierName))
                ddlLinkCn.Items.Add(new ListItem(
                    $"{c.DocNo ?? "-"} · ใบผู้ขาย {c.SupplierDocNo} · {c.SupplierName} · {c.Total:N2}" + (c.DocDate.HasValue ? " · " + c.DocDate.Value.ToString("dd/MM/yy", CultureInfo.InvariantCulture) : ""),
                    c.ScanId.ToString()));
            if (!string.IsNullOrEmpty(keep) && ddlLinkCn.Items.FindByValue(keep) != null) ddlLinkCn.SelectedValue = keep;
            ddlLinkCn.Enabled = rblCnMode.SelectedValue == ReturnCnChoice.Link;
            litCnHint.Text = ddlLinkCn.Items.Count <= 1 && rblCnMode.SelectedValue == ReturnCnChoice.Link
                ? Box("ยังไม่มีใบลดหนี้ที่บันทึกจากหน้า OCR และยังไม่ผูกการคืน — เลือก \"รอใบลดหนี้\" แล้วผูกตอนสแกนใบ", "#fff3e0") : "";
        }

        protected void rblCnMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            litPreview.Text = "";
            LoadUnlinkedCns();
        }

        /// <summary>ใบลดหนี้ที่เลือก (LINK): ยอดบรรทัดที่ลงบัญชีสินค้าคงเหลือ + เลข/ID เอกสาร</summary>
        private bool TryGetLinkedCn(out Guid scanId, out Guid? docId, out string docNo, out string supplierDocNo, out decimal invNet, out decimal total)
        {
            docId = null; docNo = null; supplierDocNo = null; invNet = 0m; total = 0m;
            if (!Guid.TryParse(ddlLinkCn.SelectedValue ?? "", out scanId)) return false;
            var st = Vcs.LoadCnState(scanId, out docId, out docNo);
            if (st == null || !docId.HasValue) return false;
            supplierDocNo = st.SupplierDocNo;
            invNet = VendorCnService.InventoryNet(st, Vcs.MappedCode("INVENTORY"));
            total = Take_Time_BangPhra.Integration.OcrReview.OcrReviewMath.CalcTotals(st.Lines).Total;
            return true;
        }

        /// <summary>ตัดสินโหมดใบลดหนี้ของการคืนนี้ (null = นโยบายปิด/ยังไม่รัน PHASE19_39 → เดิม)</summary>
        private ReturnCnDecision DecideCn(PurchaseReturnPlan plan)
        {
            if (!pnlCnMode.Visible || !Vcs.CnPrimary()) return null;
            return VendorCnPolicy.Decide(plan.CounterKey, rblCnMode.SelectedValue, plan.VatReversal);
        }

        protected void btnCloseNoCn_Click(object sender, EventArgs e)
        {
            string r = ddlPendingReturn.SelectedValue;
            if (string.IsNullOrEmpty(r)) { Alert("เลือกการคืนที่จะปิด"); return; }
            Alert(Vcs.CloseWithoutCn(r, Session["User"]?.ToString() ?? "")
                ? "ปิดรายการ " + r + " แล้ว — JE ที่ลงไว้เป็นรายการจริง (ภาษีซื้อคงเดิม)"
                : "ปิดไม่ได้ — รายการนี้ไม่ได้อยู่ในสถานะรอใบลดหนี้แล้ว");
            BindRecent();
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
                    // PHASE19_39: สถานะใบลดหนี้ผู้ขายของแต่ละการคืน (ไม่มีตาราง = ว่าง)
                    dt.Columns.Add("Cn_Info", typeof(string));
                    var refs = new List<string>();
                    foreach (DataRow r in dt.Rows) refs.Add(r["Purchase_Return_Ref"]?.ToString());
                    var links = Vcs.LoadMany(refs);
                    foreach (DataRow r in dt.Rows)
                    {
                        ReturnCnLink l;
                        if (!links.TryGetValue(r["Purchase_Return_Ref"]?.ToString() ?? "", out l)) { r["Cn_Info"] = ""; continue; }
                        r["Cn_Info"] = l.CnStatus == ReturnCnStatus.Linked
                            ? "✓ " + (l.CnDocNo ?? "") + (string.IsNullOrEmpty(l.CnSupplierDocNo) ? "" : " (ใบผู้ขาย " + l.CnSupplierDocNo + ")")
                              + (l.RevRef != null ? " · กลับ " + l.RevRef : "") + (l.DifRef != null ? " · ปรับต้นทุน " + (l.DifAmount ?? 0m).ToString("N2") : "")
                            : l.CnStatus == ReturnCnStatus.Pending
                                ? "⏳ รอใบลดหนี้ (" + Math.Max(0, (int)(DateTime.Now - l.CreatedAt).TotalDays) + " วัน) · VAT " + l.VatAmount.ToString("N2")
                                : (VendorCnPolicy.IsBilledCounter(l.CounterKey) ? "ไม่มีใบลดหนี้ (ลงครบที่หน้านี้)" : "ไม่ต้องมี (ยังไม่วางบิล)");
                    }
                    gvRecent.DataSource = dt;
                    gvRecent.DataBind();
                    pnlRecent.Visible = true;
                }
                var pending = Vcs.ListPending(100);
                pnlPendingClose.Visible = pending.Count > 0;
                ddlPendingReturn.Items.Clear();
                ddlPendingReturn.Items.Add(new ListItem("— เลือกการคืนที่รอใบลดหนี้ —", ""));
                foreach (var p in pending)
                    ddlPendingReturn.Items.Add(new ListItem($"{p.ReturnRef} · {p.SupplierName} · {p.PurchaseNet:N2} + VAT {p.VatAmount:N2} · {p.CreatedAt:dd/MM/yy}", p.ReturnRef));
                if (pending.Count > 0) pnlRecent.Visible = true;
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

        /// <summary>
        /// ปรับแผน JE ตามโหมดใบลดหนี้ (PHASE19_39). คืนข้อความผิดพลาด หรือ null.
        ///   PENDING: ไม่กลับ VAT (คืนเงินสด: VAT ที่ได้คืนพักเจ้าหนี้ — vatHoldKey) · LINK: ไม่มี JE PRT (difAmount = JE ปรับต้นทุน) · NONE/ยังไม่วางบิล: เดิม
        /// </summary>
        private string ApplyCnDecision(PurchaseReturnPlan plan, ReturnCnDecision d, out string vatHoldKey, out decimal difAmount)
        {
            vatHoldKey = null; difAmount = 0m;
            if (d == null) return null;
            if (d.Error != null) return d.Error;
            if (d.PrtJournal == ReturnPrtJournal.None)
            {
                Guid scan; Guid? docId; string docNo, sdn; decimal invNet, total;
                if (!TryGetLinkedCn(out scan, out docId, out docNo, out sdn, out invNet, out total))
                    return "เลือกใบลดหนี้ที่บันทึกจากหน้า OCR แล้ว (หรือเลือก \"รอใบลดหนี้\")";
                difAmount = VendorCnPolicy.CostDifference(invNet, plan.CarryingCost);
                plan.Explanation = d.Explanation + $" · ใบลดหนี้ {docNo}: บรรทัดลงบัญชีสินค้า {invNet:N2} vs ต้นทุนเฉลี่ยที่ตัด {plan.CarryingCost:N2}";
                return null;
            }
            if (d.PrtJournal == ReturnPrtJournal.Pending)
            {
                if (d.HoldVatOnPayable) vatHoldKey = "ACCOUNTS_PAYABLE";
                else plan.VatReversal = 0m;
                plan.Explanation = d.Explanation + " · " + plan.Explanation;
                return null;
            }
            plan.Explanation = d.Explanation + " · " + plan.Explanation;   // FULL: JE เดิมทุกประการ
            return null;
        }

        private string RenderPlan(PurchaseReturnPlan plan, ReturnCnDecision d = null, string vatHoldKey = null, decimal difAmount = 0m)
        {
            if (d != null && d.PrtJournal == ReturnPrtJournal.None)
            {
                var sx = new System.Text.StringBuilder();
                sx.Append("<div style='background:#e3f2fd;border:1px solid #90caf9;border-radius:6px;padding:10px;margin-top:10px;font-size:13px;'><b>")
                  .Append(HttpUtility.HtmlEncode(plan.Explanation)).Append("</b><br/>ไม่มี JE คืนสินค้า (ใบลดหนี้ลงเจ้าหนี้/ภาษีซื้อไว้แล้ว) · ตัดจำนวนสต๊อก (TakeTime + NextAcc) ");
                if (Math.Abs(difAmount) >= 0.005m)
                    sx.Append("· JE ปรับต้นทุน PRT-…-DIF: ").Append(difAmount > 0
                        ? $"Dr สินค้าคงเหลือ {difAmount:N2} / Cr ส่งคืนสินค้า {difAmount:N2}"
                        : $"Dr ส่งคืนสินค้า {-difAmount:N2} / Cr สินค้าคงเหลือ {-difAmount:N2}");
                else sx.Append("· ต้นทุนตรงราคาซื้อ ไม่ต้องปรับ");
                return sx.Append("</div>").ToString();
            }
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
                if (plan.VatReversal > 0) row(vatHoldKey != null ? "Cr เจ้าหนี้ (VAT ที่ได้คืน รอใบลดหนี้ — §82/10)" : "Cr ภาษีซื้อ (กลับรายการ)", 0, plan.VatReversal);
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
            var plan = BuildPlan(info, picked, false, null);
            var d = DecideCn(plan);
            string hold; decimal dif;
            string derr = ApplyCnDecision(plan, d, out hold, out dif);
            if (derr != null) { Alert(derr); return; }
            litPreview.Text = RenderPlan(plan, d, hold, dif);
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
            // PHASE19_39: โหมดใบลดหนี้ — ตรวจก่อนตัดสต๊อก (เลือก "ผูก" แต่ยังไม่เลือกใบ / ยังไม่วางบิล = หยุด)
            var cnDecision = DecideCn(plan);
            string vatHoldKey; decimal difAmount;
            string cnErr = ApplyCnDecision(plan, cnDecision, out vatHoldKey, out difAmount);
            if (cnErr != null) { Alert(cnErr); return; }

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

            string paidHowSel = plan.CounterKey == "CASH" ? ddlRefundPaidHow.SelectedValue : null;
            string paidHowAccIdSel = null;
            string user = Session["User"]?.ToString() ?? "";
            try
            {
                var cfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                if (cfg.IsConfigured && cfg.Enabled)
                {
                    var sync = new Take_Time_BangPhra.Integration.AccountingSyncService(conn);
                    paidHowAccIdSel = string.IsNullOrEmpty(paidHowSel) ? null : sync.LookupPaidHowAccountId(paidHowSel);
                    // ผูกใบลดหนี้ที่บันทึกแล้ว (LINK) = ไม่มี JE คืนสินค้า — คู่บัญชี NONE ⇒ ส่งแค่จำนวนสต๊อก (qty push) ตามเดิม
                    string counterForQueue = cnDecision != null && cnDecision.PrtJournal == ReturnPrtJournal.None ? "NONE" : plan.CounterKey;
                    sync.EnqueuePurchaseReturn(returnRef, info.Batch, info.SupplierName, cnNo, returnDate,
                        counterForQueue, plan.PurchaseNet, plan.CarryingCost, plan.VatReversal,
                        paidHowSel, paidHowAccIdSel, outLines, true, reason, vatHoldKey);
                }
            }
            catch (Exception ex)
            {
                try { db.Logs(conn, "Purchase", $"PurchaseReturn sync enqueue failed ref={returnRef}: {ex.Message}", "SYSTEM"); } catch { }
            }

            // PHASE19_39: บันทึกสถานะใบลดหนี้ของการคืนนี้ (+ ผูกทันทีถ้าเลือกใบที่บันทึกแล้ว → JE ปรับต้นทุน PRT-…-DIF)
            if (cnDecision != null)
            {
                try
                {
                    decimal vatPortion = info.NetTotal > 0 && info.VatAmount > 0
                        ? Math.Round(info.VatAmount * plan.PurchaseNet / info.NetTotal, 2, MidpointRounding.AwayFromZero) : 0m;
                    bool linkNow = cnDecision.PrtJournal == ReturnPrtJournal.None;
                    Vcs.RecordReturn(new ReturnCnLink
                    {
                        ReturnRef = returnRef, Batch = info.Batch, SupplierName = info.SupplierName, SupplierTaxId = info.SupplierTaxId,
                        ReturnDate = returnDate, PurchaseNet = plan.PurchaseNet, CarryingCost = plan.CarryingCost, VatAmount = vatPortion,
                        CounterKey = plan.CounterKey, PaidHow = paidHowSel, PaidHowAccountId = paidHowAccIdSel,
                        CnStatus = linkNow ? ReturnCnStatus.Pending : cnDecision.CnStatus, PrtJournal = cnDecision.PrtJournal
                    }, user);
                    Guid scan; Guid? cnDocId; string cnDocNo, cnSdn; decimal invNet, cnTotal;
                    if (linkNow && TryGetLinkedCn(out scan, out cnDocId, out cnDocNo, out cnSdn, out invNet, out cnTotal))
                    {
                        string difRef = Math.Abs(difAmount) >= 0.005m ? VendorCnPolicy.CostDiffRef(returnRef) : null;
                        if (Vcs.MarkLinked(returnRef, scan, cnDocId, cnDocNo, cnSdn, invNet, cnTotal, null, difRef, difAmount, ReturnPrtJournal.None, user)
                            && difRef != null)
                        {
                            var cfg2 = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                            if (cfg2.IsConfigured && cfg2.Enabled)
                                new Take_Time_BangPhra.Integration.AccountingSyncService(conn)
                                    .EnqueuePurchaseReturnCostDiff(returnRef, difAmount, returnDate, info.SupplierName, cnDocNo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    try { db.Logs(conn, "VendorCn", $"PurchaseReturn link record failed ref={returnRef}: {ex.Message}", "SYSTEM"); } catch { }
                }
            }
            try
            {
                db.Logs(conn, "Purchase", $"Purchase return {returnRef} batch={info.Batch} net={plan.PurchaseNet:N2} carrying={plan.CarryingCost:N2} vat={plan.VatReversal:N2} counter={plan.CounterKey}"
                    + (cnDecision != null ? $" cn={cnDecision.CnStatus}/{cnDecision.PrtJournal}" + (vatHoldKey != null ? " vatHold=" + vatHoldKey : "") : ""),
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
