using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Account
{
    /// <summary>
    /// ใบลดหนี้ / คืนเงินลูกค้า (PHASE19_33) — รายการ · ออกใบลดหนี้ manual · ยกเลิกการจอง + คืนเงิน (?cancelRes=) ·
    /// บันทึกคืนเงินภายหลัง (?refund=) · พิมพ์ (?print=) · PDF จาก NextAcc (?pdf=)
    /// ตรรกะทั้งหมดอยู่ที่ <see cref="CreditNoteService"/> + AccountingSyncService.CreditNote.cs
    /// </summary>
    public partial class CreditNotes : Page
    {
        private readonly string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string FlashKey = "CreditNoteFlash";

        protected string Mode = "list";
        protected bool CanApprove;

        protected string TabCss(string m) => Mode == m ? "on" : "";

        // ป้ายสถานะ NextAcc แบบเดียวทุกหน้า (CLAUDE.md ข้อ 24) — คำนวณทีเดียวตอน BindList (query คิวเดียว)
        private Dictionary<long, string> _naBadges = new Dictionary<long, string>();
        protected string NaBadge(object id)
        {
            long v;
            string h;
            return long.TryParse(Convert.ToString(id, Inv), out v) && _naBadges.TryGetValue(v, out h) ? h : "";
        }
        protected static string H(object o) => HttpUtility.HtmlEncode(o == null || o == DBNull.Value ? "" : o.ToString());
        protected static string M(object o) => Dec(o).ToString("#,##0.00", Inv);
        protected static string Q(object o) => Dec(o).ToString("0.##", Inv);
        protected static string D(object o)
        {
            if (o == null || o == DBNull.Value) return "";
            try { return Convert.ToDateTime(o).ToString("dd/MM/yyyy", Inv); } catch { return o.ToString(); }
        }
        private static decimal Dec(object o)
        {
            if (o == null || o == DBNull.Value) return 0m;
            decimal d;
            return decimal.TryParse(Convert.ToString(o, Inv).Replace(",", ""), NumberStyles.Any, Inv, out d) ? d : 0m;
        }
        private static bool TryMoney(string s, out decimal d)
        {
            d = 0m;
            if (string.IsNullOrWhiteSpace(s)) return false;
            return decimal.TryParse(s.Trim().Replace(",", ""), NumberStyles.Any, Inv, out d);
        }

        private CreditNoteService Svc => new CreditNoteService(conn);

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.FinReceipt)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            CanApprove = CreditNoteService.CanApproveCurrentUser();

            int id;
            if (int.TryParse(Request.QueryString["pdf"], out id) && CreditNoteService.TableReady(conn)) { ServePdf(id); return; }
            if (int.TryParse(Request.QueryString["print"], out id) && CreditNoteService.TableReady(conn)) { ShowPrint(id); return; }

            if (!CreditNoteService.TableReady(conn))
            {
                ShowDisabled("ยังไม่ได้รัน migration <b>Database/PHASE19_Migration_33_Credit_Notes.sql</b> — ระหว่างนี้ปุ่มยกเลิกการจองแบบเดิมยังใช้งานได้ตามปกติ");
                return;
            }
            if (!CreditNoteService.Enabled(conn))
            {
                ShowDisabled("ระบบใบลดหนี้ปิดอยู่ (<code>CreditNote_Enabled</code> ≠ 1 ในตาราง Accounting_Integration_Config) — เปิดเพื่อใช้งาน; ปิด = ใช้ปุ่มยกเลิกการจองแบบเดิม");
                return;
            }

            if (!string.IsNullOrEmpty(Request.QueryString["cancelRes"])) Mode = "cancel";
            else if (!string.IsNullOrEmpty(Request.QueryString["refund"])) Mode = "refund";
            else if (!string.IsNullOrEmpty(Request.QueryString["new"])) Mode = "new";
            pnlList.Visible = Mode == "list";
            pnlNew.Visible = Mode == "new";
            pnlCancel.Visible = Mode == "cancel";
            pnlRefundLater.Visible = Mode == "refund";

            if (!IsPostBack)
            {
                ShowFlash();
                if (Mode == "list")
                {
                    txtFrom.Text = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1).ToString("yyyy-MM-dd", Inv);
                    txtTo.Text = DateTime.Today.ToString("yyyy-MM-dd", Inv);
                    txtSearch.Text = Request.QueryString["q"] ?? "";
                    if (!string.IsNullOrEmpty(txtSearch.Text)) txtFrom.Text = DateTime.Today.AddYears(-2).ToString("yyyy-MM-dd", Inv);
                    BindList();
                }
                else if (Mode == "new") InitNew();
                else if (Mode == "cancel") InitCancel();
                else if (Mode == "refund") InitRefundLater();
            }
        }

        private void ShowDisabled(string html)
        {
            pnlDisabled.Visible = true;
            pnlEnabled.Visible = false;
            litDisabled.Text = "<div class='cn-alert warn'>" + html + "</div>";
        }

        private void Flash(string kind, string msg)
        {
            Session[FlashKey] = kind + "|" + msg;
        }

        private void ShowFlash()
        {
            string f = Session[FlashKey] as string;
            if (string.IsNullOrEmpty(f)) return;
            Session.Remove(FlashKey);
            int i = f.IndexOf('|');
            Msg(i > 0 ? f.Substring(0, i) : "info", i > 0 ? f.Substring(i + 1) : f);
        }

        private void Msg(string kind, string msg)
        {
            litMsg.Text += "<div class='cn-alert " + H(kind) + "'>" + H(msg) + "</div>";
        }

        private void Go(string url)
        {
            Response.Redirect(url, false);
            Context.ApplicationInstance.CompleteRequest();
        }

        // ── แหล่งเงิน ─────────────────────────────────────────────────────────

        private void LoadPaidHow(DropDownList ddl, int? select = null)
        {
            ddl.Items.Clear();
            ddl.Items.Add(new ListItem("— เลือกแหล่งเงินที่จ่ายคืน —", ""));
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT ID, Paid_How FROM Account_Paid_How WHERE ISNULL(Status, 'True') = 'True' ORDER BY Paid_How", null);
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        ddl.Items.Add(new ListItem(Convert.ToString(r["Paid_How"]), Convert.ToString(r["ID"])));
                Take_Time_BangPhra.Payments.PaymentChannelCatalog.RemoveNonStaffItems(ddl.Items, true, false);
            }
            catch { }
            if (select.HasValue && ddl.Items.FindByValue(select.Value.ToString(Inv)) != null)
                ddl.SelectedValue = select.Value.ToString(Inv);
        }

        private static int? SelectedInt(DropDownList ddl)
        {
            int v;
            return int.TryParse(ddl.SelectedValue, out v) && v > 0 ? (int?)v : null;
        }

        private string SaveSlip(FileUpload fu, out string error)
        {
            error = null;
            if (fu == null || !fu.HasFile) return null;
            try
            {
                string ext = Path.GetExtension(fu.FileName ?? "").ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".pdf") { error = "สลิปรองรับเฉพาะ JPG, PNG หรือ PDF"; return null; }
                if (fu.PostedFile.ContentLength > 8 * 1024 * 1024) { error = "ไฟล์สลิปใหญ่เกิน 8 MB"; return null; }
                string folder = Server.MapPath("~/Images/RefundSlips");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string name = "CN_" + DateTime.Now.ToString("yyyyMMddHHmmss", Inv) + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ext;
                fu.SaveAs(Path.Combine(folder, name));
                return "/Images/RefundSlips/" + name;
            }
            catch (Exception ex) { error = "อัปโหลดสลิปไม่สำเร็จ: " + ex.Message; return null; }
        }

        // ── รายการ ───────────────────────────────────────────────────────────

        private void BindList()
        {
            DateTime from, to;
            if (!DateTime.TryParse(txtFrom.Text, Inv, DateTimeStyles.None, out from)) from = DateTime.Today.AddMonths(-1);
            if (!DateTime.TryParse(txtTo.Text, Inv, DateTimeStyles.None, out to)) to = DateTime.Today;
            from = AccountingSyncService.NormalizeEra(from);
            to = AccountingSyncService.NormalizeEra(to);
            DataTable dt = null;
            try { dt = Svc.List(from, to, ddlStatus.SelectedValue, txtSearch.Text); }
            catch (Exception ex) { Msg("err", "อ่านรายการไม่ได้: " + ex.Message); }
            _naBadges = new Dictionary<long, string>();
            if (dt != null && dt.Rows.Count > 0)
            {
                try
                {
                    var rows = new List<NextAccStatusData.CnRow>();
                    foreach (DataRow row in dt.Rows)
                        rows.Add(new NextAccStatusData.CnRow
                        {
                            Id = Convert.ToInt64(row["ID"]), CnNo = Convert.ToString(row["CN_No"]), Status = Convert.ToString(row["Status"]),
                            DocId = row["Nexaacc_Doc_Id"] == DBNull.Value ? null : Convert.ToString(row["Nexaacc_Doc_Id"]),
                            DocNo = Convert.ToString(row["Nexaacc_Doc_No"]), Path = Convert.ToString(row["Nexaacc_Path"]),
                            SyncError = Convert.ToString(row["Sync_Error"])
                        });
                    _naBadges = NextAccStatusData.CreditNoteBadges(this, conn, rows);
                }
                catch { _naBadges = new Dictionary<long, string>(); }
            }
            rptList.DataSource = dt;
            rptList.DataBind();
            if (dt == null || dt.Rows.Count == 0) { litListFoot.Text = "<div class='cn-note' style='padding:10px'>ไม่มีใบลดหนี้ในช่วง/เงื่อนไขนี้</div>"; return; }
            decimal g = 0, v = 0, r = 0, k = 0;
            int n = 0, draft = 0, err = 0;
            foreach (DataRow row in dt.Rows)
            {
                string st = Convert.ToString(row["Status"]);
                if (st == CreditNoteService.StDraft) draft++;
                if (st == CreditNoteService.StApproved && !string.IsNullOrEmpty(Convert.ToString(row["Sync_Error"]))) err++;
                if (st == CreditNoteService.StVoided) continue;
                n++; g += Dec(row["Amount_Gross"]); v += Dec(row["Vat_Amount"]); r += Dec(row["Refund_Amount"]); k += Dec(row["Retained_Amount"]);
            }
            litListFoot.Text = "<div class='cn-note' style='padding:10px'>รวม (ไม่นับใบยกเลิก) " + n + " ใบ · ลดหนี้ <b>" + M(g) + "</b> · VAT " + M(v)
                + " · คืนเงิน <b>" + M(r) + "</b> · ยึด " + M(k)
                + (draft > 0 ? " · <span class='cn-chip DRAFT'>รออนุมัติ " + draft + "</span>" : "")
                + (err > 0 ? " · <span class='cn-chip ERR'>ส่ง NextAcc ไม่สำเร็จ " + err + " (ดูคิวที่หน้า Accounting Integration)</span>" : "") + "</div>";
        }

        protected void btnSearch_Click(object sender, EventArgs e) { BindList(); }

        protected void rptList_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            int id;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out id)) return;
            string m;
            switch (e.CommandName)
            {
                case "Approve":
                    if (!CanApprove) { Flash("err", "ไม่มีสิทธิ์อนุมัติใบลดหนี้ (Owner / Admin ที่มีสิทธิ์ตั้งค่าบัญชี)"); break; }
                    Flash(Svc.Approve(id, out m) ? "ok" : "err", m);
                    break;
                case "Void":
                    if (!CanApprove) { Flash("err", "ไม่มีสิทธิ์ยกเลิกใบลดหนี้"); break; }
                    Flash(Svc.Void(id, hfActReason.Value, out m) ? "ok" : "err", m);
                    break;
                case "Refund":
                    Go(ResolveUrl("~/Account/CreditNotes?refund=" + id));
                    return;
            }
            Go(Request.RawUrl);
        }

        // ── ออกใบลดหนี้ manual ────────────────────────────────────────────────

        private void InitNew()
        {
            ddlReason.Items.Clear();
            foreach (string c in CreditNoteService.ReasonCodes)
            {
                if (c == "CANCEL_PARTIAL") continue;
                ddlReason.Items.Add(new ListItem(CreditNoteService.ReasonLabel(c), c));
            }
            LoadPaidHow(ddlRefundPaidHow);
            SearchSources();
            string src = Request.QueryString["src"];
            if (!string.IsNullOrWhiteSpace(src)) LoadSourceIntoForm(src.Trim());
        }

        private void SearchSources()
        {
            ddlSource.Items.Clear();
            try
            {
                var list = Svc.SearchSources(txtSrcSearch.Text, DateTime.Today.AddDays(-30), DateTime.Today, 80);
                foreach (var s in list)
                {
                    string text = s.Id + " · " + s.Date.ToString("dd/MM/yyyy", Inv) + " · " + (string.IsNullOrEmpty(s.CustomerName) ? "-" : s.CustomerName)
                                  + " · " + s.Total.ToString("#,##0.00", Inv)
                                  + (s.IsDeposit ? " (มัดจำ — คืนผ่านยกเลิกการจอง)" : " · ลดหนี้ได้อีก " + s.Creditable.ToString("#,##0.00", Inv));
                    ddlSource.Items.Add(new ListItem(text, s.Id));
                }
                if (list.Count == 0) ddlSource.Items.Add(new ListItem("— ไม่พบใบเสร็จ —", ""));
            }
            catch (Exception ex) { Msg("err", "ค้นหาไม่ได้: " + ex.Message); }
        }

        protected void btnSrcSearch_Click(object sender, EventArgs e) { SearchSources(); pnlSrc.Visible = false; }

        protected void btnLoadSource_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ddlSource.SelectedValue)) { Msg("warn", "เลือกใบต้นทางก่อน"); return; }
            LoadSourceIntoForm(ddlSource.SelectedValue);
        }

        private void LoadSourceIntoForm(string receiptId)
        {
            CreditNoteService.SourceReceipt src = null;
            try { src = Svc.LoadSource(receiptId); }
            catch (Exception ex) { Msg("err", "อ่านใบต้นทางไม่ได้: " + ex.Message); return; }
            if (src == null) { Msg("err", "ไม่พบใบ " + receiptId); return; }
            if (src.IsDeposit)
            {
                Msg("warn", "ใบ " + src.Id + " เป็นใบเสร็จมัดจำ — คืนมัดจำผ่าน ตารางการจอง → ยกเลิกคืนเงิน → \"คืนเงินบางส่วน / ออกใบลดหนี้ (ละเอียด)\"");
                pnlSrc.Visible = false;
                return;
            }
            hfSrcId.Value = src.Id;
            var sb = new StringBuilder("<div class='cn-alert info'>");
            sb.Append("ใบ <b>").Append(H(src.Id)).Append("</b> วันที่ ").Append(src.Date.ToString("dd/MM/yyyy", Inv))
              .Append(" · ลูกค้า ").Append(H(string.IsNullOrEmpty(src.CustomerName) ? "-" : src.CustomerName))
              .Append(src.ReservationId > 0 ? " · การจอง #" + src.ReservationId : "")
              .Append(" · ยอด <b>").Append(M(src.Total)).Append("</b> (VAT ").Append(M(src.Vat)).Append(")")
              .Append(" · รับชำระด้วย ").Append(H(string.IsNullOrEmpty(src.PaidType) ? "-" : src.PaidType))
              .Append(src.Credited > 0 ? " · ลดหนี้ไปแล้ว " + M(src.Credited) : "")
              .Append(" · <b>ลดหนี้ได้อีก ").Append(M(src.Creditable)).Append("</b>");
            if (src.Status != "" && !src.Status.Equals("Normal", StringComparison.OrdinalIgnoreCase))
                sb.Append("<br/>⚠ สถานะใบ: ").Append(H(src.Status));
            sb.Append("</div>");
            if (Svc.FindGatewayTxn(src.Id).HasValue)
                sb.Append("<div class='cn-note'>ใบนี้รับเงินผ่านเกตเวย์ — เลือก \"คืนผ่านเกตเวย์\" เพื่อคืนเข้าบัตร/บัญชีเดิมของลูกค้าได้</div>");
            litSrcInfo.Text = sb.ToString();
            rptLines.DataSource = src.Lines;
            rptLines.DataBind();
            if (src.PaidHowId.HasValue && ddlRefundPaidHow.Items.FindByValue(src.PaidHowId.Value.ToString(Inv)) != null)
                ddlRefundPaidHow.SelectedValue = src.PaidHowId.Value.ToString(Inv);
            pnlSrc.Visible = true;
        }

        protected void btnCreate_Click(object sender, EventArgs e)
        {
            var svc = Svc;
            var src = svc.LoadSource(hfSrcId.Value);
            if (src == null) { Msg("err", "ไม่พบใบต้นทาง — เลือกใหม่"); return; }
            var req = new CreditNoteService.Request
            {
                ReservationId = src.ReservationId,
                SourceReceiptId = src.Id,
                ReasonCode = ddlReason.SelectedValue,
                ReasonText = txtReasonText.Text,
                CustomerName = src.CustomerName,
                RefundRef = txtRefundRef.Text,
                CnDate = DateTime.Today
            };
            decimal whole;
            if (TryMoney(txtWholeAmount.Text, out whole) && whole > 0)
            {
                if (whole > src.Creditable + 0.01m) { Msg("err", "ยอดเกินที่ลดหนี้ได้อีก (" + M(src.Creditable) + ")"); pnlSrc.Visible = true; return; }
                CreditNoteService.AddProportionalLines(req, src, whole);
            }
            else
            {
                foreach (RepeaterItem it in rptLines.Items)
                {
                    var hf = it.FindControl("hfNo") as HiddenField;
                    var ta = it.FindControl("txtLineAmt") as TextBox;
                    var tq = it.FindControl("txtLineQty") as TextBox;
                    var cr = it.FindControl("chkRestock") as CheckBox;
                    int no;
                    decimal amt, qty;
                    if (hf == null || ta == null || !int.TryParse(hf.Value, out no)) continue;
                    if (!TryMoney(ta.Text, out amt) || amt <= 0) continue;
                    var sl = src.Lines.FirstOrDefault(x => x.No == no);
                    if (sl == null) continue;
                    if (amt > sl.Amount + 0.01m) { Msg("err", "บรรทัด " + no + ": ลดหนี้ " + M(amt) + " เกินยอดเดิม " + M(sl.Amount)); pnlSrc.Visible = true; return; }
                    if (!TryMoney(tq?.Text, out qty) || qty <= 0) qty = sl.Qty;
                    if (qty > sl.Qty) qty = sl.Qty;
                    req.Lines.Add(new CreditNoteService.Line
                    {
                        SourceLineNo = sl.No, ProductId = sl.ProductId, ProductTypeId = sl.ProductTypeId,
                        Description = sl.Description, Unit = sl.Unit, Qty = qty, Amount = amt, UnitCost = sl.UnitCost,
                        RestockQty = cr != null && cr.Checked && sl.Restockable ? qty : 0m
                    });
                }
            }
            if (req.Lines.Count == 0) { Msg("err", "ใส่ยอดลดหนี้อย่างน้อย 1 บรรทัด (หรือยอดลดหนี้ทั้งใบ)"); pnlSrc.Visible = true; return; }
            if (req.Lines.Any(l => l.RestockQty > 0) && req.ReasonCode != "GOODS_RETURN") req.ReasonCode = "GOODS_RETURN";

            string method = ddlRefundMethod.SelectedValue;
            if (chkRefundLater.Checked) { method = CreditNoteService.MNone; req.RefundPending = true; }
            req.RefundMethod = method;
            req.RefundPaidHowId = SelectedInt(ddlRefundPaidHow);
            string slipErr;
            req.RefundSlipPath = SaveSlip(fuRefundSlip, out slipErr);
            if (slipErr != null) { Msg("err", slipErr); pnlSrc.Visible = true; return; }

            string m;
            int id = svc.Create(req, false, out m);
            if (id <= 0) { Msg("err", m); pnlSrc.Visible = true; return; }
            Flash("ok", m);
            Go(ResolveUrl("~/Account/CreditNotes?q=" + HttpUtility.UrlEncode(src.Id)));
        }

        // ── ยกเลิกการจอง + คืนเงิน ─────────────────────────────────────────────

        private void InitCancel()
        {
            int resId;
            if (!int.TryParse(Request.QueryString["cancelRes"], out resId) || resId <= 0) { Msg("err", "เลขการจองไม่ถูกต้อง"); btnCancelConfirm.Enabled = false; return; }
            hfCancelRes.Value = resId.ToString(Inv);
            CreditNoteService.ReservationMoney money = null;
            try { money = Svc.LoadReservationMoney(resId); }
            catch (Exception ex) { Msg("err", "อ่านข้อมูลการจองไม่ได้: " + ex.Message); }
            if (money == null) { btnCancelConfirm.Enabled = false; return; }

            string st = money.Status ?? "";
            var svc = Svc;
            var chk = svc.CheckCancellable(money);
            bool blocked = !chk.Allowed;
            var sb = new StringBuilder();
            sb.Append("<p>การจอง <b>#").Append(resId).Append("</b> · ลูกค้า <b>").Append(H(money.CustomerName)).Append("</b> · สถานะ ").Append(H(st)).Append("</p>");
            if (blocked) sb.Append("<div class='cn-alert err'>").Append(H(chk.Message)).Append("</div>");
            else if (chk.SettleOnly) sb.Append("<div class='cn-alert warn'>").Append(H(chk.Message)).Append("</div>");
            if (money.IsChannelCollect || money.CollectMode == ReservationBalance.ModeUnknown)
                sb.Append("<div class='cn-alert info'>ใบ OTA (")
                  .Append(money.IsChannelCollect ? "Channel Collect — OTA เก็บค่าห้องไปแล้ว" : "ยังไม่ระบุว่าใครเก็บเงิน")
                  .Append("): ค่าห้องที่ OTA เก็บ <b>OTA เป็นผู้คืนลูกค้าตามนโยบาย OTA</b> — โรงแรมไม่คืนจากบัญชีโรงแรม. ")
                  .Append("ยอดด้านล่างคือเงินที่โรงแรมรับเองเท่านั้น</div>");
            string rev = (money.OtaRevenueRef ?? "").Trim();
            if (rev.StartsWith("OTADOC-", StringComparison.OrdinalIgnoreCase) || rev.StartsWith("OTA-", StringComparison.OrdinalIgnoreCase))
                sb.Append("<div class='cn-alert warn'>รายได้ค่าห้อง OTA ของการจองนี้ลงบัญชีแล้ว (").Append(H(rev))
                  .Append(") — ถ้า OTA ยกเลิก/คืนเงินลูกค้า ให้ยกเลิกเอกสาร OTA ที่ Admin → Accounting Integration → \"ยกเลิกเอกสาร OTA\" (ไม่ใช่ใบลดหนี้หน้านี้)</div>");
            foreach (string ex in money.Excluded)
                sb.Append("<div class='cn-note'>ไม่นำมาคืน: ").Append(H(ex)).Append("</div>");
            sb.Append("<div class='cn-scroll'><table class='cn-t' style='min-width:760px'><tr><th>ใบเสร็จ</th><th>วันที่</th><th>ชนิด</th><th>รับชำระด้วย</th>")
              .Append("<th class='num'>ยอดใบ</th><th class='num'>หักไปใบอื่น/ลดหนี้แล้ว</th><th class='num'>คืนได้</th><th>NextAcc</th></tr>");
            var sync = new AccountingSyncService(conn);
            foreach (var r in money.Receipts)
            {
                bool gw = svc.FindGatewayTxn(r.Id).HasValue;
                sb.Append("<tr><td>").Append(H(r.Id)).Append("</td><td>").Append(r.Date.ToString("dd/MM/yyyy", Inv)).Append("</td><td>")
                  .Append(r.IsDeposit ? "มัดจำ (คืนมัดจำ/ยึด)" : r.IsFolio ? "ใบเสร็จ/ใบกำกับ (ใบลดหนี้)" : "ใบอื่นที่ผูกการจอง (ไม่รวมโดยปริยาย)")
                  .Append("</td><td>").Append(H(r.PaidType)).Append(gw ? " · เกตเวย์" : "")
                  .Append("</td><td class='num'>").Append(M(r.Total)).Append("</td><td class='num'>").Append(M(r.Total - r.Creditable))
                  .Append("</td><td class='num'><b>").Append(M(r.Creditable)).Append("</b></td><td>");
                string state;
                sync.IsReceiptSyncedForOtaReclass(r.Id, out state);
                sb.Append(H(state)).Append("</td></tr>");
            }
            if (money.Receipts.Count == 0) sb.Append("<tr><td colspan='8'>ไม่มีใบเสร็จที่รับเงินไว้ (ยกเลิกได้โดยไม่มีการคืนเงิน)</td></tr>");
            sb.Append("<tr><td colspan='6' style='text-align:right'><b>รวมเงินที่คืนได้ (ใบของการจอง)</b></td><td class='num'><b>").Append(M(money.FolioCreditable)).Append("</b></td><td></td></tr>");
            bool hasLinked = money.TotalCreditable - money.FolioCreditable > 0.005m;
            if (hasLinked)
                sb.Append("<tr><td colspan='6' style='text-align:right'>รวมใบอื่นที่ผูกการจอง (ติ๊กเพื่อรวม)</td><td class='num'>").Append(M(money.TotalCreditable)).Append("</td><td></td></tr>");
            sb.Append("</table></div>");
            litCancelInfo.Text = sb.ToString();

            chkIncludeLinked.Visible = hasLinked;
            decimal def = chkIncludeLinked.Checked && hasLinked ? money.TotalCreditable : money.FolioCreditable;
            txtCancelRefund.Text = def.ToString("0.00", Inv);
            txtCancelRefund.Attributes["data-total"] = money.FolioCreditable.ToString("0.00", Inv);
            txtCancelRefund.Attributes["data-total-all"] = money.TotalCreditable.ToString("0.00", Inv);
            btnCancelConfirm.Text = chk.SettleOnly ? "ยืนยันบันทึกคืน/ยึดเงินค้าง (การจองยกเลิกไว้แล้ว)" : "ยืนยันยกเลิกการจอง + บันทึกคืนเงิน";
            var latest = money.Receipts.LastOrDefault();
            LoadPaidHow(ddlCancelPaidHow, latest != null ? latest.PaidHowId : null);
            if (!money.HasGateway)
            {
                var gwItem = ddlCancelMethod.Items.FindByValue(CreditNoteService.MGateway);
                if (gwItem != null) ddlCancelMethod.Items.Remove(gwItem);
            }
            btnCancelConfirm.Enabled = !blocked;
        }

        protected void btnCancelConfirm_Click(object sender, EventArgs e)
        {
            int resId;
            if (!int.TryParse(hfCancelRes.Value, out resId)) { Msg("err", "เลขการจองไม่ถูกต้อง"); return; }
            decimal refund;
            if (!TryMoney(txtCancelRefund.Text, out refund) || refund < 0) { Msg("err", "ยอดคืนไม่ถูกต้อง"); InitCancel(); return; }
            string slipErr;
            string slip = SaveSlip(fuCancelSlip, out slipErr);
            if (slipErr != null) { Msg("err", slipErr); InitCancel(); return; }
            var res = Svc.CancelReservation(resId, refund, ddlRetainedAs.SelectedValue, refund > 0 ? ddlCancelMethod.SelectedValue : CreditNoteService.MNone,
                SelectedInt(ddlCancelPaidHow), txtCancelRef.Text, slip, txtCancelReason.Text, chkIncludeLinked.Checked);
            if (!res.Ok) { Msg("err", res.Message); InitCancel(); return; }
            Flash(res.DraftCount > 0 ? "warn" : "ok", res.Message);
            Go(ResolveUrl("~/Account/CreditNotes?q=" + resId));
        }

        // ── บันทึกคืนเงินภายหลัง ─────────────────────────────────────────────

        private void InitRefundLater()
        {
            int id;
            if (!int.TryParse(Request.QueryString["refund"], out id)) { Msg("err", "ไม่พบใบลดหนี้"); btnLaterSave.Enabled = false; return; }
            var rec = Svc.Load(id);
            if (rec == null || rec.RefundStatus != CreditNoteService.RsPending) { Msg("warn", "ใบนี้ไม่ได้ค้างคืนเงิน"); btnLaterSave.Enabled = false; return; }
            hfRefundCn.Value = id.ToString(Inv);
            lblRefundCn.Text = H(rec.CnNo) + " · " + M(rec.AmountGross) + " บาท · " + H(rec.CustomerName);
            int? ph = null;
            try { var src = Svc.LoadSource(rec.SourceReceiptId); if (src != null) ph = src.PaidHowId; } catch { }
            LoadPaidHow(ddlLaterPaidHow, ph);
        }

        protected void btnLaterSave_Click(object sender, EventArgs e)
        {
            int id;
            if (!int.TryParse(hfRefundCn.Value, out id)) return;
            string slipErr;
            string slip = SaveSlip(fuLaterSlip, out slipErr);
            if (slipErr != null) { Msg("err", slipErr); return; }
            string m;
            bool ok = Svc.RecordRefund(id, ddlLaterMethod.SelectedValue, SelectedInt(ddlLaterPaidHow), txtLaterRef.Text, slip, out m);
            if (!ok) { Msg("err", m); return; }
            Flash("ok", m);
            Go(ResolveUrl("~/Account/CreditNotes"));
        }

        // ── พิมพ์ / PDF ──────────────────────────────────────────────────────

        private void ShowPrint(int id)
        {
            pnlMain.Visible = false;
            pnlPrint.Visible = true;
            var rec = Svc.Load(id);
            litPrint.Text = rec == null ? "<div class='cn-alert err'>ไม่พบใบลดหนี้</div>" : Svc.RenderPrintHtml(rec);
        }

        private void ServePdf(int id)
        {
            var rec = Svc.Load(id);
            if (rec == null || !rec.NexaaccDocId.HasValue)
            {
                Flash("warn", "ใบนี้ยังไม่มีเอกสารบน NextAcc — ใช้ปุ่มพิมพ์ (ฉบับ TakeTime) แทน");
                Go(ResolveUrl("~/Account/CreditNotes"));
                return;
            }
            byte[] pdf = null;
            try
            {
                var sync = new AccountingSyncService(conn);
                Guid docId = rec.NexaaccDocId.Value;
                pdf = Task.Run(() => sync.DownloadCreditNotePdfAsync(docId)).GetAwaiter().GetResult();
            }
            catch { pdf = null; }
            if (pdf == null || pdf.Length == 0)
            {
                Flash("err", "ดึง PDF จาก NextAcc ไม่สำเร็จ — ลองใหม่ หรือพิมพ์ฉบับ TakeTime");
                Go(ResolveUrl("~/Account/CreditNotes"));
                return;
            }
            Response.Clear();
            Response.ContentType = "application/pdf";
            Response.AddHeader("Content-Disposition", "inline; filename=\"" + (rec.NexaaccDocNo ?? rec.CnNo).Replace("\"", "") + ".pdf\"");
            Response.BinaryWrite(pdf);
            Response.Flush();
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
