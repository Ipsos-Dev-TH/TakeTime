using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
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
    /// ภาษีซื้อยังไม่ถึงกำหนด 11640 (CLAUDE.md ข้อ 25 ③) — รายการจาก NextAcc GET document/undue-input-vat (DocumentController.cs:450-455)
    /// + ฟอร์มเติมใบกำกับ → แก้ผู้ติดต่อผู้ขาย (PUT document/contacts/{id} :1486) + POST document/{id}/complete-tax-invoice (:398-412)
    /// ⇒ NextAcc ย้าย 11640→11610 เองเมื่อครบ §86/4 (DocumentService.cs:15943). สิทธิ์ Owner / Admin ที่มีสิทธิ์บัญชี
    /// </summary>
    public partial class UndueInputVat : Page
    {
        private readonly string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string FlashKey = "UndueVatFlash";

        protected bool CanAct;

        protected static string H(object o) => HttpUtility.HtmlEncode(o == null || o == DBNull.Value ? "" : o.ToString());
        protected static string M(object o) { decimal d; return decimal.TryParse(Convert.ToString(o, Inv), NumberStyles.Any, Inv, out d) ? d.ToString("#,##0.00", Inv) : ""; }
        protected static string D(object o) { try { return Convert.ToDateTime(o).ToString("dd/MM/yyyy", Inv); } catch { return ""; } }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.FinReport)) return;
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            if (!NextAccActionAccess.CanAccounting())
            {
                Response.Redirect("~/ReserveTable?denied=" + Perm.SysAccounting, false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            CanAct = true;
            Guid pdf;
            if (Guid.TryParse(Request.QueryString["pdf"], out pdf)) { ServePdf(pdf); return; }
            if (!IsPostBack)
            {
                ShowFlash();
                BindList(true);
            }
        }

        private void Msg(string kind, string text, bool html = false)
        {
            litMsg.Text += "<div class='uv-alert " + H(kind) + "'>" + (html ? text : H(text)) + "</div>";
        }

        private void ShowFlash()
        {
            string f = Session[FlashKey] as string;
            if (string.IsNullOrEmpty(f)) return;
            Session.Remove(FlashKey);
            int i = f.IndexOf('|');
            if (i > 0) Msg(f.Substring(0, i), f.Substring(i + 1));
        }

        private List<NxUndueInputVatSummary> Load()
        {
            var svc = new AccountingSyncService(conn);
            try
            {
                var list = Task.Run(() => svc.LoadUndueInputVatAsync()).GetAwaiter().GetResult();
                if (list == null) Msg("warn", "การเชื่อมต่อนี้เรียก company endpoint ของ NextAcc ไม่ได้ (ตั้ง CompanyId + Nexaacc_Company_Endpoints=1)");
                return list ?? new List<NxUndueInputVatSummary>();
            }
            catch (Exception ex)
            {
                Msg("err", "ดึงรายการจาก NextAcc ไม่ได้: " + ex.Message);
                return new List<NxUndueInputVatSummary>();
            }
        }

        private void BindList(bool rebuildMonths)
        {
            var all = Load();
            if (rebuildMonths)
            {
                string keep = IsPostBack ? ddlMonth.SelectedValue : (Request.QueryString["m"] ?? "");
                ddlMonth.Items.Clear();
                ddlMonth.Items.Add(new ListItem("ทุกเดือน (" + all.Count + ")", ""));
                foreach (var g in all.GroupBy(x => x.DocumentDate.ToString("yyyy-MM", Inv)).OrderByDescending(g => g.Key))
                {
                    var d0 = g.First().DocumentDate;
                    ddlMonth.Items.Add(new ListItem(d0.ToString("MMMM yyyy", new CultureInfo("th-TH")) + " (" + g.Count() + ")", g.Key));
                }
                if (ddlMonth.Items.FindByValue(keep ?? "") != null) ddlMonth.SelectedValue = keep;
            }
            string m = ddlMonth.SelectedValue;
            string show = ddlShow.SelectedValue;
            var rows = all.Where(x => string.IsNullOrEmpty(m) || x.DocumentDate.ToString("yyyy-MM", Inv) == m)
                          .Where(x => show == "all" || (show == "expired" ? x.IsExpired : !x.IsExpired))
                          .OrderBy(x => x.IsExpired).ThenBy(x => x.MonthsLeft).ThenBy(x => x.DocumentDate).ToList();
            rptList.DataSource = rows;
            rptList.DataBind();
            decimal open = all.Where(x => !x.IsExpired && !x.IsForeignService).Sum(x => x.VatAmount);
            int soon = all.Count(x => !x.IsExpired && !x.IsForeignService && x.MonthsLeft <= 1);
            litSummary.Text = "<div class='uv-sum'>ภาษีซื้อรอเคลม (ยังไม่เลยกำหนด): <b>" + M(open) + "</b> บาท · "
                + all.Count(x => !x.IsExpired) + " ใบ" + (soon > 0 ? " · <span class='uv-chip soon'>ใกล้หมดสิทธิ์ " + soon + " ใบ</span>" : "")
                + (rows.Count == 0 ? "<br/><span class='uv-note'>ไม่มีรายการตามตัวกรอง</span>" : "") + "</div>";
        }

        protected string Deadline(object item)
        {
            var x = item as NxUndueInputVatSummary;
            if (x == null) return "";
            if (x.IsForeignService) return "<span class='uv-chip fs'>ภ.พ.36</span>";
            if (x.IsExpired) return "<span class='uv-chip exp'>เลย 6 เดือนแล้ว</span>";
            return x.MonthsLeft <= 1 ? "<span class='uv-chip soon'>เหลือ " + x.MonthsLeft + " เดือน</span>" : "เหลือ " + x.MonthsLeft + " เดือน";
        }

        protected string Missing(object item)
        {
            var x = item as NxUndueInputVatSummary;
            if (x == null || x.MissingFields == null) return "";
            return string.Join("", x.MissingFields.Select(f => "<span class='uv-chip'>" + H(f) + "</span>"));
        }

        protected void Filter_Changed(object sender, EventArgs e)
        {
            pnlForm.Visible = false;
            BindList(sender == btnRefresh);
        }

        protected void rptList_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            Guid id;
            if (e.CommandName != "Fill" || !Guid.TryParse(Convert.ToString(e.CommandArgument), out id)) return;
            OpenForm(id);
            BindList(false);
        }

        private void OpenForm(Guid id)
        {
            var svc = new AccountingSyncService(conn);
            NxUndueDetail det = null;
            try { det = Task.Run(() => svc.LoadUndueDetailAsync(id)).GetAwaiter().GetResult(); }
            catch (Exception ex) { Msg("err", "อ่านเอกสารจาก NextAcc ไม่ได้: " + ex.Message); return; }
            if (det?.Doc == null) { Msg("err", "ไม่พบเอกสาร"); return; }
            hfDocId.Value = id.ToString();
            litFormDoc.Text = H(det.Doc.DocumentNumber) + " · " + H(det.Doc.Contact?.Name);
            txtInvNo.Text = det.Doc.SupplierInvoiceNumber ?? "";
            txtInvDate.Text = det.Doc.SupplierTaxInvoiceDate.HasValue ? det.Doc.SupplierTaxInvoiceDate.Value.ToString("yyyy-MM-dd", Inv) : "";
            txtBranch.Text = det.Doc.SupplierBranchCode ?? det.Contact?.BranchCode ?? "";
            txtTaxId.Text = det.Contact?.TaxId ?? det.Doc.Contact?.TaxId ?? "";
            txtAddress.Text = det.Contact?.Address ?? "";
            litFormInfo.Text = "<div class='uv-note' style='margin-bottom:8px'>ยอดรวม " + M(det.Doc.TotalAmount) + " · VAT " + M(det.Doc.VatAmount)
                + " · วันที่เอกสาร " + D(det.Doc.DocumentDate) + "</div>";
            pnlForm.Visible = true;
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            pnlForm.Visible = false;
            BindList(false);
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            Guid id;
            if (!Guid.TryParse(hfDocId.Value, out id)) { pnlForm.Visible = false; BindList(false); return; }
            DateTime dt;
            DateTime? inv = DateTime.TryParseExact((txtInvDate.Text ?? "").Trim(), "yyyy-MM-dd", Inv, DateTimeStyles.None, out dt) ? (DateTime?)dt : null;
            var svc = new AccountingSyncService(conn);
            NxUndueDetail det = null;
            try { det = Task.Run(() => svc.LoadUndueDetailAsync(id)).GetAwaiter().GetResult(); } catch { }
            bool taxOk = det?.Contact != null && TaxPartyValidator.IsValidThaiTaxId(det.Contact.TaxId);
            bool addrOk = det?.Contact != null && !string.IsNullOrWhiteSpace(det.Contact.Address);
            var errs = TaxPartyValidator.ValidateSupplierInvoice(txtInvNo.Text, inv, txtBranch.Text, txtTaxId.Text, txtAddress.Text, taxOk, addrOk, DateTime.Today);
            if (errs.Count > 0)
            {
                Msg("err", "ตรวจไม่ผ่าน:<br/>• " + string.Join("<br/>• ", errs.Select(H)), true);
                pnlForm.Visible = true;
                BindList(false);
                return;
            }
            NxActionResult r;
            try
            {
                r = Task.Run(() => svc.CompleteSupplierTaxInvoiceAsync(id, txtInvNo.Text, inv.Value, txtBranch.Text, txtTaxId.Text, txtAddress.Text))
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex) { r = NxActionResult.Fail("FAILED", ex.Message); }
            Session[FlashKey] = (r.Ok ? "ok" : r.Status == "BLOCKED" ? "warn" : "err") + "|" + r.Message;
            Response.Redirect(Request.Path + (string.IsNullOrEmpty(ddlMonth.SelectedValue) ? "" : "?m=" + Uri.EscapeDataString(ddlMonth.SelectedValue)), false);
            Context.ApplicationInstance.CompleteRequest();
        }

        /// <summary>PDF เฉพาะเอกสารที่อยู่ในรายการภาษีซื้อค้าง (ไม่เปิดเอกสารอื่นของบริษัทผ่านหน้านี้)</summary>
        private void ServePdf(Guid id)
        {
            var svc = new AccountingSyncService(conn);
            var list = Load();
            if (!list.Any(x => x.Id == id))
            {
                Response.StatusCode = 404;
                Response.Write("ไม่พบเอกสาร");
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            byte[] pdf = null;
            try { pdf = Task.Run(() => svc.DownloadNextAccDocumentPdfAsync(id)).GetAwaiter().GetResult(); } catch { }
            if (pdf == null || pdf.Length < 5)
            {
                Response.StatusCode = 502;
                Response.ContentType = "text/plain; charset=utf-8";
                Response.Write("ดึง PDF จาก NextAcc ไม่ได้");
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            Response.Clear();
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.ContentType = "application/pdf";
            Response.AddHeader("Content-Disposition", "inline; filename=\"" + id.ToString("N").Substring(0, 8) + ".pdf\"");
            Response.BinaryWrite(pdf);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
