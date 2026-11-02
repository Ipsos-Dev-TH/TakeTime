using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Integration;
using Take_Time_BangPhra.Payments;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// แก้ยอดรับเงินที่บันทึกผิด (หน้าแก้ไข/เช็คอินการจอง) — ส่วนเสริมใต้ "ประวัติการชำระเงิน":
    /// เตือนเมื่อยอดที่บันทึก ≠ ยอดบนสลิปที่ระบบอ่านได้ · Owner/Admin บัญชี (<see cref="ReceiptPreflight.CanOverrideCurrentUser"/>) แก้ยอดได้
    /// ผ่าน <see cref="PaymentAmountCorrection.Correct"/> (ใบ + สมุดรับเงิน + มัดจำการจอง + ประวัติ + NextAcc) · สลิปเก่าที่ยังไม่เคยตรวจ = ปุ่ม "อ่านยอดจากสลิป".
    /// ช่องยอดเดิม (TextBox5) ยังปิดเหมือนเดิม — ยอดที่บันทึกแล้วต้องแก้ผ่านทางนี้เท่านั้น (มีเหตุผล + log).
    /// </summary>
    public partial class Reserve
    {
        private void AmountFixLoad(int reservationId)
        {
            try
            {
                string command = Request.QueryString["command"] ?? "";
                bool show = reservationId > 0 && (command == "edit" || command == "checkin");
                pnlAmountFix.Visible = false;
                litAmountFixWarn.Text = "";
                if (!show) return;

                var rows = PaymentAmountCorrection.ListForReservation(conn, reservationId);
                var warn = new StringBuilder();
                foreach (var r in rows.Where(x => x.Mismatch))
                    warn.Append("<div class=\"af-warn\">").Append(PaymentAmountCorrection.MismatchHtml(r)).Append("</div>");
                litAmountFixWarn.Text = warn.ToString();

                bool canFix = ReceiptPreflight.CanOverrideCurrentUser();
                if (!canFix)
                {
                    // พนักงานทั่วไป: เห็นคำเตือนอย่างเดียว + บอกว่าใครแก้ได้
                    if (warn.Length > 0) { pnlAmountFix.Visible = true; pnlAmountFixForm.Visible = false; }
                    return;
                }
                if (rows.Count == 0) return;
                pnlAmountFix.Visible = true;
                pnlAmountFixForm.Visible = true;
                if (!IsPostBack || ddlAmountFixReceipt.Items.Count == 0)
                {
                    ddlAmountFixReceipt.Items.Clear();
                    foreach (var r in rows)
                    {
                        string label = r.ReceiptId + " · " + (r.PaymentDate.HasValue ? r.PaymentDate.Value.ToString("dd/MM/yyyy") : "") + " · "
                            + (r.IsDeposit ? "มัดจำ" : r.PaymentType) + " · บันทึก " + r.Recorded.ToString("N2")
                            + (r.SlipAmount.HasValue ? " · สลิป " + r.SlipAmount.Value.ToString("N2") : (r.SlipId > 0 ? " · สลิปยังไม่ได้อ่านยอด" : " · ไม่มีสลิป"))
                            + (r.Mismatch ? " ⚠" : "");
                        ddlAmountFixReceipt.Items.Add(new ListItem(label, r.ReceiptId));
                    }
                    // เลือกแถวที่ไม่ตรงก่อน + เติมยอดสลิปให้
                    var first = rows.FirstOrDefault(x => x.Mismatch) ?? rows[0];
                    ddlAmountFixReceipt.SelectedValue = first.ReceiptId;
                    txtAmountFixNew.Text = first.SlipAmount.HasValue ? first.SlipAmount.Value.ToString("0.00", CultureInfo.InvariantCulture) : "";
                }
                var sel = rows.FirstOrDefault(x => x.ReceiptId == ddlAmountFixReceipt.SelectedValue);
                btnAmountFixReadSlip.Visible = sel != null && sel.SlipId > 0 && !sel.SlipAmount.HasValue;
            }
            catch (Exception ex)
            {
                pnlAmountFix.Visible = false;
                try { new code().Logs(conn, "PaymentAmountFix", "AmountFixLoad: " + ex.Message, Session["User"]?.ToString()); } catch { }
            }
        }

        private int AmountFixReservationId()
        {
            int id; return int.TryParse(Request.QueryString["id"], out id) ? id : 0;
        }

        protected void ddlAmountFixReceipt_SelectedIndexChanged(object sender, EventArgs e)
        {
            int rid = AmountFixReservationId();
            var sel = PaymentAmountCorrection.ListForReservation(conn, rid).FirstOrDefault(x => x.ReceiptId == ddlAmountFixReceipt.SelectedValue);
            txtAmountFixNew.Text = sel != null && sel.SlipAmount.HasValue ? sel.SlipAmount.Value.ToString("0.00", CultureInfo.InvariantCulture) : "";
            litAmountFixResult.Text = "";
            AmountFixLoad(rid);
        }

        protected void btnAmountFixReadSlip_Click(object sender, EventArgs e)
        {
            int rid = AmountFixReservationId();
            var sel = PaymentAmountCorrection.ListForReservation(conn, rid).FirstOrDefault(x => x.ReceiptId == ddlAmountFixReceipt.SelectedValue);
            if (sel == null) return;
            string note;
            decimal? amt = PaymentAmountCorrection.ReadSlipAmount(conn, sel.SlipId, out note);
            if (amt.HasValue)
            {
                txtAmountFixNew.Text = amt.Value.ToString("0.00", CultureInfo.InvariantCulture);
                litAmountFixResult.Text = "<div class=\"af-ok\">อ่านสลิปแล้ว: " + Server.HtmlEncode(note)
                    + (PaymentAmountCorrection.Differs(amt.Value, sel.Recorded) ? " — <b>ไม่ตรงกับที่บันทึก " + sel.Recorded.ToString("N2") + "</b> ใส่ยอดสลิปให้แล้ว ตรวจแล้วกดบันทึก" : " — ตรงกับที่บันทึกแล้ว") + "</div>";
            }
            else litAmountFixResult.Text = "<div class=\"af-warn\">" + Server.HtmlEncode(note) + " — เปิดรูปสลิปดูแล้วกรอกยอดเอง</div>";
            AmountFixLoad(rid);
        }

        protected void btnAmountFixSave_Click(object sender, EventArgs e)
        {
            int rid = AmountFixReservationId();
            if (!ReceiptPreflight.CanOverrideCurrentUser()) { litAmountFixResult.Text = "<div class=\"af-warn\">เฉพาะเจ้าของ/ผู้ดูแลบัญชี</div>"; return; }
            decimal amt;
            if (!decimal.TryParse((txtAmountFixNew.Text ?? "").Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out amt) || amt <= 0m)
            {
                litAmountFixResult.Text = "<div class=\"af-warn\">กรอกยอดที่ถูกต้อง (ตามสลิป) ก่อน</div>"; return;
            }
            int adminId = 0; int.TryParse(Convert.ToString(Session["UserID"]), out adminId);
            var res = PaymentAmountCorrection.Correct(conn, ddlAmountFixReceipt.SelectedValue, amt, txtAmountFixReason.Text, adminId);
            litAmountFixResult.Text = "<div class=\"" + (res.Success ? "af-ok" : "af-warn") + "\">" + Server.HtmlEncode(res.Message ?? "")
                + (string.IsNullOrEmpty(res.SyncMessage) ? "" : "<br/>NextAcc: " + Server.HtmlEncode(res.SyncMessage)) + "</div>";
            if (res.Success)
            {
                txtAmountFixReason.Text = "";
                ddlAmountFixReceipt.Items.Clear();
                LoadPaymentHistory();   // โหลดสมุดรับเงิน/ยอดใหม่ (เรียก AmountFixLoad ซ้ำเอง)
                try
                {
                    // ช่องยอดที่โชว์ (ปิดแก้) + บรรทัดยอดคงเหลือ ให้ตรงกับที่แก้
                    decimal paid = PaymentAmountCorrection.ListForReservation(conn, rid).Sum(x => x.Recorded);
                    TextBox5.Text = paid.ToString("0.00", CultureInfo.InvariantCulture);
                    decimal total; if (decimal.TryParse(TextBox4.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out total))
                    { Label7.Visible = true; Label7.Text = "ยอดเงินส่วนที่เหลือที่จะต้องชำระตอนเช็คอิน = " + (total - paid).ToString("N2") + " บาท"; }
                }
                catch { }
            }
        }
    }
}
