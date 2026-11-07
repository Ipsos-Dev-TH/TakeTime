using System;
using System.Configuration;
using System.Data;
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
    /// งาน NextAcc ของใบเสร็จใบเดียว (CLAUDE.md ข้อ 25) — ?receipt={เลขใบ} หรือ ?uid={UID ของ Account_Receipt} · ?pdf={GUID} เปิด PDF
    ///   ① ลูกค้าขอใบกำกับเต็มรูปทีหลัง (issue-full-tax-invoice / แก้ผู้ติดต่อ / ยกเลิก→ออกใหม่ เมื่อยอมรับ)
    ///   ② แก้แหล่งเงินที่เลือกผิด (reclassify-payment-source)
    ///   ③ ส่งอีเมลเอกสารจาก NextAcc (send-email)
    /// ตรรกะทั้งหมดอยู่ที่ AccountingSyncService.NextAccActions.cs + NextAccActions.cs (pure)
    /// </summary>
    public partial class ReceiptNextAcc : Page
    {
        private readonly string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string FlashKey = "ReceiptNextAccFlash";

        private static string H(object o) => HttpUtility.HtmlEncode(o == null || o == DBNull.Value ? "" : o.ToString());
        private static string M(decimal d) => d.ToString("#,##0.00", Inv);

        private string _rn;
        private string ReceiptNo
        {
            get
            {
                if (_rn != null) return _rn;
                string r = (Request.QueryString["receipt"] ?? "").Trim();
                if (r.Length == 0 && !string.IsNullOrWhiteSpace(Request.QueryString["uid"]))
                    r = new AccountingSyncService(conn).NxReceiptIdByUid(Request.QueryString["uid"]) ?? "";
                _rn = r;
                return _rn;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.FinReceipt)) return;
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            Guid pdfId;
            if (Guid.TryParse(Request.QueryString["pdf"], out pdfId)) { ServePdf(pdfId); return; }

            litRn.Text = H(ReceiptNo);
            if (ReceiptNo.Length == 0) { Msg("err", "ไม่ระบุเลขใบเสร็จ"); pnlMain.Visible = false; return; }
            if (!NextAccActionAccess.CanAct())
            {
                Msg("warn", "ต้องเป็นเจ้าของ หรือผู้ดูแลที่มีสิทธิ์ใบเสร็จ/บัญชี จึงสั่งงานบน NextAcc ได้ (ดูข้อมูลได้อย่างเดียว)");
                btnFti.Enabled = btnRcl.Enabled = btnEmail.Enabled = false;
            }
            if (!IsPostBack)
            {
                ShowFlash();
                Bind(true);
            }
        }

        private void Msg(string kind, string text, bool html = false)
        {
            litMsg.Text += "<div class='rx-alert " + H(kind) + "'>" + (html ? text : H(text)) + "</div>";
        }

        private void Flash(string kind, string text) { Session[FlashKey] = kind + "|" + text; }

        private void ShowFlash()
        {
            string f = Session[FlashKey] as string;
            if (string.IsNullOrEmpty(f)) return;
            Session.Remove(FlashKey);
            int i = f.IndexOf('|');
            if (i > 0) Msg(f.Substring(0, i), f.Substring(i + 1));
        }

        private void Reload()
        {
            Response.Redirect(Request.Path + "?receipt=" + Uri.EscapeDataString(ReceiptNo), false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void Bind(bool fillForm)
        {
            var svc = new AccountingSyncService(conn);
            var f = ReceiptPreflight.LoadFacts(conn, ReceiptNo);
            if (f == null) { Msg("err", "ไม่พบใบเสร็จ " + ReceiptNo); pnlMain.Visible = false; return; }
            DocumentResponse doc = null;
            try { doc = Task.Run(() => svc.NxLoadReceiptDocAsync(ReceiptNo)).GetAwaiter().GetResult(); } catch { }
            var buyer = svc.NxReceiptBuyer(ReceiptNo);

            var sb = new StringBuilder("<div class='rx-kv'>");
            Action<string, string> kv = (k, v) => sb.Append("<div>").Append(H(k)).Append("</div><div>").Append(v).Append("</div>");
            kv("วันที่ / ยอด", H(f.Date.ToString("dd/MM/yyyy", Inv)) + " · " + H(M(f.Total)) + " บาท (VAT " + H(M(f.Vat)) + ")"
                + (f.IsDeposit ? " · <span class='rx-chip'>ใบรับมัดจำ</span>" : ""));
            kv("สถานะในระบบ", H(string.IsNullOrEmpty(f.Status) ? "Normal" : f.Status) + (f.ReservationId > 0 ? " · การจอง #" + f.ReservationId : ""));
            kv("แหล่งเงิน", H(f.PaidType));
            kv("ผู้ซื้อในระบบ", buyer == null ? "<i>ยังไม่มี</i>"
                : H(buyer.Name) + (string.IsNullOrEmpty(buyer.TaxId) ? "" : " · " + H(buyer.TaxId)) + (string.IsNullOrEmpty(buyer.Address) ? "" : "<br/><span class='rx-note'>" + H(buyer.Address) + "</span>"));
            if (doc == null)
                kv("เอกสาร NextAcc", svc.NxReceiptDocId(ReceiptNo) == Guid.Empty ? "<i>ยังไม่ได้ส่งเข้า NextAcc</i>" : "<i>อ่านจาก NextAcc ไม่ได้</i>");
            else
            {
                kv("เอกสาร NextAcc", H(doc.DocumentNumber) + " · " + H(AccountingSyncService.NxDocTypeLabel(doc.DocumentType)) + " · "
                    + H(AccountingSyncService.NxDocStatusLabel(doc.Status))
                    + " · <a href='" + H(ResolveUrl("~/Account/ReceiptNextAcc?receipt=" + Uri.EscapeDataString(ReceiptNo) + "&pdf=" + doc.Id)) + "' target='_blank'>📄 PDF</a>");
                kv("หัวกระดาษ (NextAcc)", H(doc.DocumentTitle ?? "-") + (string.IsNullOrEmpty(doc.TaxInvoiceTitleNotice) ? "" : "<br/><span class='rx-note'>" + H(doc.TaxInvoiceTitleNotice) + "</span>"));
                kv("ผู้ซื้อบนเอกสาร", H(doc.Contact?.Name) + (string.IsNullOrEmpty(doc.Contact?.TaxId) ? "" : " · " + H(doc.Contact.TaxId))
                    + (doc.BuyerDeclinedTaxInvoice ? " · <span class='rx-chip'>ไม่ประสงค์รับใบกำกับ</span>" : ""));
                kv("แหล่งเงินบนเอกสาร", H(doc.PaymentAccountName ?? (doc.PaymentAccountId.HasValue ? doc.PaymentAccountId.ToString() : "— (รับชำระแยก)")));
                if (!string.IsNullOrEmpty(doc.EtaxStatus)) kv("e-Tax", H(doc.EtaxStatus));
                if (doc.ReplacedByDocumentId.HasValue)
                    kv("ใบกำกับที่ออกแทน", H(doc.ReplacedByDocumentNumber) + " · <a href='" + H(ResolveUrl("~/Account/ReceiptNextAcc?receipt="
                        + Uri.EscapeDataString(ReceiptNo) + "&pdf=" + doc.ReplacedByDocumentId.Value)) + "' target='_blank'>📄 PDF</a>");
            }
            sb.Append("</div>");
            string block = CreditNoteGuards.VoidBlockReason(conn, ReceiptNo, false);
            if (!string.IsNullOrEmpty(block)) sb.Append("<div class='rx-alert warn' style='margin-top:10px'>").Append(H(block)).Append("</div>");
            litInfo.Text = sb.ToString();

            if (fillForm && buyer != null)
            {
                txtName.Text = buyer.Name ?? "";
                txtTaxId.Text = buyer.TaxId ?? "";
                txtBranch.Text = buyer.BranchCode ?? "";
                txtAddress.Text = buyer.Address ?? "";
                if (buyer.IsJuristic.HasValue) ddlType.SelectedValue = buyer.IsJuristic.Value ? "J" : "I";
            }
            txtCurPaid.Text = f.PaidType ?? "";

            if (fillForm)
            {
                ddlPaidHow.Items.Clear();
                ddlPaidHow.Items.Add(new ListItem("— เลือก —", ""));
                var ph = svc.NxPaidHowList();
                if (ph != null)
                    foreach (DataRow r in ph.Rows)
                    {
                        string acc = Convert.ToString(r["AccId"]);
                        ddlPaidHow.Items.Add(new ListItem(Convert.ToString(r["Paid_How"]) + (string.IsNullOrWhiteSpace(acc) ? " (ยังไม่ผูกบัญชี NextAcc)" : ""),
                            Convert.ToString(r["ID"], Inv)));
                    }
                ddlEmailDoc.Items.Clear();
                if (doc != null)
                {
                    if (doc.ReplacedByDocumentId.HasValue)
                        ddlEmailDoc.Items.Add(new ListItem("ใบกำกับเต็มรูป " + doc.ReplacedByDocumentNumber, doc.ReplacedByDocumentId.Value.ToString()));
                    ddlEmailDoc.Items.Add(new ListItem(doc.DocumentNumber + " (" + AccountingSyncService.NxDocTypeLabel(doc.DocumentType) + ")", doc.Id.ToString()));
                }
                if (buyer != null && !string.IsNullOrWhiteSpace(buyer.Email)) txtEmail.Text = buyer.Email.Trim();
            }
            pnlEmail.Visible = doc != null;
            BindHistory(svc);
        }

        private void BindHistory(AccountingSyncService svc)
        {
            var dt = svc.NxActionHistory(ReceiptNo);
            if (dt == null) { litHistory.Text = "<span class='rx-note'>ยังไม่มีตารางประวัติ (รัน Database/PHASE19_Migration_41_NextAcc_Actions.sql) — ดูประวัติใน log NextAccActions</span>"; return; }
            if (dt.Rows.Count == 0) { litHistory.Text = "<span class='rx-note'>ยังไม่เคยสั่งงาน NextAcc จากหน้านี้</span>"; return; }
            var sb = new StringBuilder("<table class='rx-t'><tr><th>เวลา</th><th>งาน</th><th>สถานะ</th><th>ผล</th><th>โดย</th></tr>");
            foreach (DataRow r in dt.Rows)
            {
                string st = Convert.ToString(r["Status"]);
                string res = H(r["Result_Message"]);
                if (r["Result_Doc_Id"] != DBNull.Value)
                    res += " · <a href='" + H(ResolveUrl("~/Account/ReceiptNextAcc?receipt=" + Uri.EscapeDataString(ReceiptNo) + "&pdf=" + r["Result_Doc_Id"])) + "' target='_blank'>📄 "
                        + H(r["Result_Doc_No"]) + "</a>";
                sb.Append("<tr><td>").Append(H(Convert.ToDateTime(r["Created_At"]).ToString("dd/MM/yyyy HH:mm", Inv))).Append("</td><td>")
                  .Append(H(ActionLabel(Convert.ToString(r["Action"])))).Append("</td><td><span class='rx-chip ").Append(H(st)).Append("'>")
                  .Append(H(StatusLabel(st))).Append("</span></td><td>").Append(res).Append("</td><td>").Append(H(r["Created_By"])).Append("</td></tr>");
            }
            litHistory.Text = sb.Append("</table>").ToString();
        }

        private static string ActionLabel(string a)
        {
            switch (a)
            {
                case "FULL_TAX_INVOICE": return "ใบกำกับเต็มรูป";
                case "RECLASSIFY_SOURCE": return "แก้แหล่งเงิน";
                case "DOC_EMAIL": return "ส่งอีเมล";
                case "CN_ETAX": return "e-Tax ใบลดหนี้";
                default: return a;
            }
        }

        private static string StatusLabel(string s)
        {
            switch (s)
            {
                case "DONE": return "สำเร็จ";
                case "QUEUED": return "รอดำเนินการ";
                case "BLOCKED": return "ทำไม่ได้";
                case "FAILED": return "ไม่สำเร็จ";
                default: return s;
            }
        }

        private void ShowResult(NxActionResult r)
        {
            string kind = r == null ? "err" : r.Ok ? "ok" : r.Status == "QUEUED" ? "info" : r.Status == "BLOCKED" ? "warn" : "err";
            Flash(kind, r == null ? "ไม่มีผลลัพธ์" : (r.Message ?? r.Status));
            Reload();
        }

        protected void btnFti_Click(object sender, EventArgs e)
        {
            if (!NextAccActionAccess.CanAct()) { Msg("err", "ไม่มีสิทธิ์"); return; }
            bool? j = ddlType.SelectedValue == "J" ? true : ddlType.SelectedValue == "I" ? (bool?)false : null;
            FullTaxInvoiceBuyer b;
            var errs = TaxPartyValidator.ValidateBuyer(txtName.Text, txtTaxId.Text, txtBranch.Text, txtAddress.Text, j, txtPhone.Text, out b);
            if (errs.Count > 0)
            {
                Msg("err", "ตรวจข้อมูลผู้ซื้อไม่ผ่าน:<br/>• " + string.Join("<br/>• ", errs.Select(H)), true);
                Bind(false);
                return;
            }
            var svc = new AccountingSyncService(conn);
            NxActionResult r;
            try
            {
                r = Task.Run(() => svc.RequestFullTaxInvoiceAsync(ReceiptNo, b, txtFtiReason.Text, chkRecreate.Checked)).GetAwaiter().GetResult();
            }
            catch (Exception ex) { r = NxActionResult.Fail("FAILED", ex.Message); }
            ShowResult(r);
        }

        protected void btnRcl_Click(object sender, EventArgs e)
        {
            if (!NextAccActionAccess.CanAct()) { Msg("err", "ไม่มีสิทธิ์"); return; }
            int ph;
            if (!int.TryParse(ddlPaidHow.SelectedValue, out ph) || ph <= 0) { Msg("err", "เลือกแหล่งเงินที่ถูกต้อง"); Bind(false); return; }
            var svc = new AccountingSyncService(conn);
            NxActionResult r;
            try { r = Task.Run(() => svc.RequestPaymentSourceReclassAsync(ReceiptNo, ph, txtRclReason.Text)).GetAwaiter().GetResult(); }
            catch (Exception ex) { r = NxActionResult.Fail("FAILED", ex.Message); }
            ShowResult(r);
        }

        protected void btnEmail_Click(object sender, EventArgs e)
        {
            if (!NextAccActionAccess.CanAct()) { Msg("err", "ไม่มีสิทธิ์"); return; }
            Guid docId;
            if (!Guid.TryParse(ddlEmailDoc.SelectedValue, out docId) || !DocBelongsToReceipt(docId)) { Msg("err", "เลือกเอกสาร"); Bind(false); return; }
            var svc = new AccountingSyncService(conn);
            NxActionResult r;
            try { r = Task.Run(() => svc.SendNextAccDocumentEmailAsync(docId, txtEmail.Text, null, null, ReceiptNo, null)).GetAwaiter().GetResult(); }
            catch (Exception ex) { r = NxActionResult.Fail("FAILED", ex.Message); }
            ShowResult(r);
        }

        /// <summary>เปิด/ส่งได้เฉพาะเอกสารของใบนี้: ตัวชี้ปัจจุบัน / ใบแทนของเอกสารนั้น / เอกสารในประวัติงานของใบนี้</summary>
        private bool DocBelongsToReceipt(Guid docId)
        {
            if (docId == Guid.Empty || ReceiptNo.Length == 0) return false;
            var svc = new AccountingSyncService(conn);
            if (svc.NxReceiptDocId(ReceiptNo) == docId) return true;
            if (svc.NxActionOwnsResultDoc(ReceiptNo, docId)) return true;
            try
            {
                var d = Task.Run(() => svc.NxLoadReceiptDocAsync(ReceiptNo)).GetAwaiter().GetResult();
                return d != null && d.ReplacedByDocumentId == docId;
            }
            catch { return false; }
        }

        private void ServePdf(Guid docId)
        {
            if (!DocBelongsToReceipt(docId))
            {
                Response.StatusCode = 404;
                Response.Write("ไม่พบเอกสาร");
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            var svc = new AccountingSyncService(conn);
            byte[] pdf = null;
            try { pdf = Task.Run(() => svc.DownloadNextAccDocumentPdfAsync(docId)).GetAwaiter().GetResult(); } catch { }
            if (pdf == null || pdf.Length < 5)
            {
                Response.StatusCode = 502;
                Response.ContentType = "text/plain; charset=utf-8";
                Response.Write("ดึง PDF จาก NextAcc ไม่ได้ — ลองใหม่ภายหลัง");
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            Response.Clear();
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.ContentType = "application/pdf";
            Response.AddHeader("Content-Disposition", "inline; filename=\"" + docId.ToString("N").Substring(0, 8) + ".pdf\"");
            Response.BinaryWrite(pdf);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
