using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Account
{
    /// <summary>
    /// คืนเงินส่วนที่ลูกค้าจ่ายเกินตอนเช็คเอาท์ (?res={การจอง}&amp;amt={ยอดที่หน้าเช็คเอาท์คำนวณ})
    ///   CheckoutService กันส่วนเกินไว้ในเงินรับล่วงหน้า (ไม่ตัดเป็นรายได้) → หน้านี้ออกใบลดหนี้ของใบเสร็จมัดจำผ่าน
    ///   CreditNoteService.Create (เส้นคืนมัดจำ: NextAcc refund-deposit) — ไม่ยกเลิกการจอง/ไม่แตะสถานะ
    /// ส่วนเกินที่ไม่ได้อยู่ในมัดจำ (จ่ายเกินในใบเสร็จ/ใบกำกับ) → ลิงก์ออกใบลดหนี้จากใบนั้นที่หน้า CreditNotes
    /// </summary>
    public partial class OverpayRefund : Page
    {
        private readonly string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private int ResId
        {
            get { int v; return int.TryParse(Request.QueryString["res"], out v) && v > 0 ? v : 0; }
        }

        private static string H(object o) => HttpUtility.HtmlEncode(o == null ? "" : o.ToString());
        private static string M(decimal d) => d.ToString("#,##0.00", Inv);

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.FinReceipt)) return;
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            if (!CreditNoteGuards.SchemaReady(conn) || !CreditNoteService.Enabled(conn))
            {
                Msg("warn", "ระบบใบลดหนี้ยังไม่เปิด (ยังไม่รัน PHASE19_33 หรือ CreditNote_Enabled ≠ 1) — คืนเงินตามขั้นตอนเดิม");
                pnlForm.Visible = false;
                return;
            }
            if (ResId <= 0)
            {
                Msg("err", "ไม่ระบุเลขการจอง");
                pnlForm.Visible = false;
                return;
            }
            if (!IsPostBack) InitForm();
        }

        private void Msg(string kind, string text)
        {
            litMsg.Text += "<div class='op-alert " + H(kind) + "'>" + H(text) + "</div>";
        }

        private void MsgHtml(string kind, string html)
        {
            litMsg.Text += "<div class='op-alert " + H(kind) + "'>" + html + "</div>";
        }

        /// <summary>การ์ด "สถานะบน NextAcc": ใบเสร็จของการจอง (ใบมัดจำที่จะคืน) + ใบลดหนี้ที่มีอยู่ — ป้ายแบบเดียวทุกหน้า (ข้อ 24).
        /// อ่านอย่างเดียว 3 query; พังต้องไม่ทำให้ฟอร์มคืนเงินพัง</summary>
        private void RenderNaStatus(int res, CreditNoteService.ReservationMoney money)
        {
            try
            {
                var sb = new StringBuilder();
                var ids = money.Receipts.Select(r => r.Id).Where(x => !string.IsNullOrEmpty(x)).ToList();
                var rb = NextAccStatusData.ReceiptBadges(this, conn, ids);
                foreach (var r in money.Receipts)
                {
                    string h;
                    if (!rb.TryGetValue(r.Id ?? "", out h)) continue;
                    sb.Append("<div class='na-stack' style='margin:4px 0;'><b>").Append(H(r.Id)).Append("</b> ")
                      .Append(r.IsDeposit ? "(มัดจำ)" : "(ใบเสร็จ/ใบกำกับ)").Append(" ยอด ").Append(M(r.Total)).Append(" · ").Append(h).Append("</div>");
                }
                if (CreditNoteGuards.SchemaReady(conn))
                {
                    var dt = new code().DatabaseQuerySafe(conn,
                        "SELECT ID, CN_No, Status, CAST(Nexaacc_Doc_Id AS NVARCHAR(50)) AS DocId, Nexaacc_Doc_No, Nexaacc_Path, Sync_Error FROM Credit_Note WHERE Reservation_ID = @r ORDER BY ID",
                        new Dictionary<string, object> { { "@r", res } });
                    var rows = new List<NextAccStatusData.CnRow>();
                    if (dt != null)
                        foreach (DataRow row in dt.Rows)
                            rows.Add(new NextAccStatusData.CnRow
                            {
                                Id = Convert.ToInt64(row["ID"]), CnNo = Convert.ToString(row["CN_No"]), Status = Convert.ToString(row["Status"]),
                                DocId = row["DocId"] == DBNull.Value ? null : Convert.ToString(row["DocId"]), DocNo = Convert.ToString(row["Nexaacc_Doc_No"]),
                                Path = Convert.ToString(row["Nexaacc_Path"]), SyncError = Convert.ToString(row["Sync_Error"])
                            });
                    var cb = NextAccStatusData.CreditNoteBadges(this, conn, rows);
                    foreach (var r in rows)
                    {
                        string h;
                        if (cb.TryGetValue(r.Id, out h))
                            sb.Append("<div class='na-stack' style='margin:4px 0;'>ใบลดหนี้ <b>").Append(H(r.CnNo)).Append("</b> · ").Append(h).Append("</div>");
                    }
                }
                if (sb.Length > 0)
                    litNaStatus.Text = "<div class='op-card'><div style='font-weight:600;margin-bottom:6px;'>สถานะบน NextAcc</div>" + sb + "</div>";
            }
            catch { litNaStatus.Text = ""; }
        }

        private void InitForm()
        {
            int res = ResId;
            var svc = new CreditNoteService(conn);
            CreditNoteService.ReservationMoney money = null;
            try { money = svc.LoadReservationMoney(res); }
            catch (Exception ex) { Msg("err", "อ่านข้อมูลการจองไม่ได้: " + ex.Message); pnlForm.Visible = false; return; }
            if (money == null) { Msg("err", "ไม่พบการจอง #" + res); pnlForm.Visible = false; return; }

            RenderNaStatus(res, money);

            decimal credit = 0m;
            try { var bal = ReservationBalance.Load(conn, res); if (bal != null) credit = bal.Credit; } catch { }
            decimal amt;
            if (!decimal.TryParse(Request.QueryString["amt"], NumberStyles.Any, Inv, out amt) || amt <= 0) amt = credit;
            // หลังอนุมัติใบลดหนี้คืนเงินแล้ว ยอดจ่ายเกินตามสูตรกลางลดลงเอง — ไม่เสนอเกินยอดที่ยังเกินอยู่
            if (credit > 0m && amt > credit) amt = credit;

            var existing = CreditNoteGuards.ActiveForReservation(conn, res);
            if (existing.Count > 0)
                MsgHtml("warn", "การจองนี้มีใบลดหนี้อยู่แล้ว: <b>" + H(string.Join(", ", existing)) + "</b> — ตรวจที่ <a href='"
                    + H(ResolveUrl("~" + CreditNoteGuards.CreditNoteListUrl(res.ToString(Inv)))) + "'>รายการใบลดหนี้</a> ก่อน กันคืนซ้ำ");

            var deposits = money.Receipts.Where(r => r.IsDeposit && r.Creditable > 0.005m).ToList();
            var others = money.Receipts.Where(r => !r.IsDeposit && r.Creditable > 0.005m).ToList();

            litInfo.Text = "<div class='op-alert info'>การจอง <b>#" + res + "</b> · ลูกค้า " + H(string.IsNullOrEmpty(money.CustomerName) ? "-" : money.CustomerName)
                + " · สถานะ " + H(money.Status) + " · ยอดจ่ายเกินตามระบบตอนนี้ <b>" + M(credit) + "</b> บาท</div>";

            if (deposits.Count == 0)
            {
                var sb = new StringBuilder("ไม่มีใบเสร็จมัดจำที่ยังคืนได้ — ส่วนที่จ่ายเกินอยู่ในใบเสร็จ/ใบกำกับ ⇒ ออกใบลดหนี้จากใบนั้น (เลือก \"คิดเงินผิด/เก็บเงินเกิน\"):");
                foreach (var o in others)
                    sb.Append("<br/>• <a href='").Append(H(ResolveUrl("~" + CreditNoteGuards.CreditNoteUrlForReceipt(o.Id)))).Append("'>")
                      .Append(H(o.Id)).Append("</a> ยอด ").Append(M(o.Total)).Append(" (ลดหนี้ได้อีก ").Append(M(o.Creditable)).Append(")");
                if (others.Count == 0) sb.Append("<br/>ไม่พบใบที่ลดหนี้ได้ — ตรวจยอดการจองอีกครั้ง");
                MsgHtml("warn", sb.ToString());
                pnlForm.Visible = false;
                return;
            }

            ddlSource.Items.Clear();
            foreach (var d in deposits)
                ddlSource.Items.Add(new ListItem(d.Id + " · " + d.Date.ToString("dd/MM/yyyy", Inv) + " · คืนได้อีก " + M(d.Creditable), d.Id));
            var best = deposits.FirstOrDefault(d => d.Creditable + 0.005m >= amt) ?? deposits.OrderByDescending(d => d.Creditable).First();
            ddlSource.SelectedValue = best.Id;
            txtAmount.Text = Math.Min(amt, best.Creditable).ToString("0.00", Inv);

            ddlMethod.Items.Clear();
            ddlMethod.Items.Add(new ListItem(CreditNoteService.MethodLabel(CreditNoteService.MTransfer), CreditNoteService.MTransfer));
            ddlMethod.Items.Add(new ListItem(CreditNoteService.MethodLabel(CreditNoteService.MCash), CreditNoteService.MCash));
            if (deposits.Any(d => svc.FindGatewayTxn(d.Id).HasValue))
                ddlMethod.Items.Add(new ListItem(CreditNoteService.MethodLabel(CreditNoteService.MGateway), CreditNoteService.MGateway));

            ddlPaidHow.Items.Clear();
            ddlPaidHow.Items.Add(new ListItem("— เลือกแหล่งเงินที่จ่ายคืน —", ""));
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT ID, Paid_How FROM Account_Paid_How WHERE ISNULL(Status, 'True') = 'True' ORDER BY Paid_How", null);
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        ddlPaidHow.Items.Add(new ListItem(Convert.ToString(r["Paid_How"]), Convert.ToString(r["ID"])));
                Take_Time_BangPhra.Payments.PaymentChannelCatalog.RemoveNonStaffItems(ddlPaidHow.Items, true, false);
            }
            catch { }
            if (best.PaidHowId.HasValue && ddlPaidHow.Items.FindByValue(best.PaidHowId.Value.ToString(Inv)) != null)
                ddlPaidHow.SelectedValue = best.PaidHowId.Value.ToString(Inv);
        }

        protected void btnCreate_Click(object sender, EventArgs e)
        {
            int res = ResId;
            var svc = new CreditNoteService(conn);
            decimal amount;
            if (!decimal.TryParse((txtAmount.Text ?? "").Replace(",", "").Trim(), NumberStyles.Any, Inv, out amount) || amount <= 0)
            { Msg("err", "กรอกยอดคืนให้ถูกต้อง"); return; }
            amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

            CreditNoteService.SourceReceipt src = null;
            try { src = svc.LoadSource(ddlSource.SelectedValue); } catch { }
            if (src == null || !src.IsDeposit || src.ReservationId != res)
            { Msg("err", "ใบต้นทางต้องเป็นใบเสร็จมัดจำของการจอง #" + res); return; }
            if (amount > src.Creditable + 0.01m)
            { Msg("err", "ยอดเกินที่ใบ " + src.Id + " คืนได้อีก (" + M(src.Creditable) + ")"); return; }

            string method = ddlMethod.SelectedValue;
            int ph;
            int? paidHow = int.TryParse(ddlPaidHow.SelectedValue, out ph) && ph > 0 ? (int?)ph : null;
            if (method != CreditNoteService.MGateway && !paidHow.HasValue) { Msg("err", "เลือกแหล่งเงินที่จ่ายคืน"); return; }

            var req = new CreditNoteService.Request
            {
                ReservationId = res,
                SourceReceiptId = src.Id,
                ReasonCode = "WRONG_AMOUNT",
                ReasonText = "คืนเงินที่ลูกค้าจ่ายเกินตอนเช็คเอาท์ การจอง #" + res
                             + (string.IsNullOrWhiteSpace(txtNotes.Text) ? "" : " — " + txtNotes.Text.Trim()),
                CustomerName = src.CustomerName,
                RefundMethod = method,
                RefundPaidHowId = paidHow,
                RefundRef = txtRef.Text,
                Notes = "OVERPAY_CHECKOUT",
                CnDate = DateTime.Today
            };
            CreditNoteService.AddProportionalLines(req, src, amount);

            string message;
            int id;
            // fromCancellation=true: Create รับใบมัดจำเฉพาะโหมดนี้ (เส้นคืนมัดจำ refund-deposit) — หน้านี้ไม่ได้ยกเลิกการจอง
            // (ไม่เรียก CancelReservation, ไม่เปลี่ยนสถานะการจอง/ใบมัดจำ)
            try { id = svc.Create(req, true, out message); }
            catch (Exception ex) { id = -1; message = ex.Message; }
            if (id <= 0) { Msg("err", "สร้างใบลดหนี้ไม่สำเร็จ: " + message); return; }

            try { new code().Logs(conn, "CreditNote", "คืนเงินจ่ายเกินตอนเช็คเอาท์ การจอง #" + res + " ใบ " + src.Id + " ยอด " + M(amount) + " → " + message, CreditNoteService.CurrentUser()); }
            catch { }
            // ข้อความผลลัพธ์ไปขึ้นที่หน้ารายการใบลดหนี้ (Session key เดียวกับหน้า CreditNotes)
            Session["CreditNoteFlash"] = "ok|" + message;
            Response.Redirect(ResolveUrl("~" + CreditNoteGuards.CreditNoteListUrl(res.ToString(Inv))), false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
