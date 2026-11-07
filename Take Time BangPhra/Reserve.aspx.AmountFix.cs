using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web;
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

        /// <summary>
        /// พนักงานแนบสลิปที่ "แนบไว้กับการจองนี้แล้วแต่ยังไม่ได้ลงรับเงิน" (เช่น ลูกค้าแนบตอนจอง รอตรวจ → มัดจำ 0) แล้วกดบันทึก
        /// → อนุมัติสลิปเดิมผ่าน <see cref="PaymentService.ApproveReservationSlip(long,int?)"/> (ลงรับเงินตามยอดธนาคาร + ใบเสร็จ + มัดจำ +
        /// ยืนยันการจอง + คิวบัญชี) แทนการสร้างแถวสลิป/ใบเสร็จใหม่ (ซ้ำกับแถวเดิม) · คืน false เสมอ = ไม่ทำเส้นบันทึกปกติต่อ แล้วโหลดหน้าใหม่
        /// </summary>
        private bool TryAdoptPendingSlip(Take_Time_BangPhra.Payments.SlipCheck.SlipVerifyResult r, decimal typed)
        {
            long slipId = r.PriorUse.SlipId.Value;
            string who = Session["User"]?.ToString() ?? "SYSTEM";
            try
            {
                // ยอดที่ลงรับ: ธนาคารยืนยัน → ยอดนั้น · ไม่งั้นยอดที่พนักงานกรอก (อนุมัติจะตรวจสลิปซ้ำกับยอดนี้อีกชั้น)
                decimal amount = r.BankConfirmed && r.Amount.HasValue && r.Amount.Value > 0m ? r.Amount.Value
                               : r.AmountLocked ? r.AmountLock.Amount.Value : typed;
                if (amount <= 0m)
                {
                    ShowUserError("slipAdopt", "กรอกยอดตามสลิปก่อน แล้วกดบันทึกอีกครั้ง", SlipAmountBox(Request.QueryString["command"] ?? ""));
                    return false;
                }
                var p = new Dictionary<string, object> { { "@id", slipId }, { "@amt", amount } };
                // ยังไม่มียอดที่แจ้ง → ใช้ยอดนี้ · สถานะไม่ใช่ PENDING แต่ไม่เคยลงเงิน (อนุมัติค้างครึ่งทาง) → คืนเป็น PENDING ให้อนุมัติได้
                code2.DatabaseInsertSafe(conn,
                    @"UPDATE Payment_Slips SET Claimed_Amount = @amt
                       WHERE ID = @id AND ISNULL(Claimed_Amount, 0) <= 0
                         AND NOT EXISTS (SELECT 1 FROM Payment_History ph WHERE ph.PaymentSlip_ID = @id)", p);
                code2.DatabaseInsertSafe(conn,
                    @"UPDATE Payment_Slips SET VerificationStatus = 'PENDING', IsVerified = 0
                       WHERE ID = @id AND ISNULL(VerificationStatus, '') NOT IN ('PENDING', 'REJECTED')
                         AND ISNULL(CAST(Account_Receipt_ID AS NVARCHAR(50)), '') IN ('', '0')
                         AND NOT EXISTS (SELECT 1 FROM Payment_History ph WHERE ph.PaymentSlip_ID = @id)",
                    new Dictionary<string, object> { { "@id", slipId } });

                int adminId;   // Login ตั้ง Session["UserID"] (Session["ID"] ไม่มีใครตั้ง)
                if (!int.TryParse(Convert.ToString(Session["UserID"]), out adminId)) int.TryParse(Convert.ToString(Session["ID"]), out adminId);
                PaymentResult pr = new PaymentService(conn).ApproveReservationSlip(slipId, adminId > 0 ? (int?)adminId : null);
                if (pr == null || !pr.Success)
                {
                    string why = pr == null ? "ข้อมูลสลิปไม่ครบ (ไม่มียอดที่แจ้ง/ไม่ผูกการจอง)" : pr.Message;
                    ShowSlipCheck(r);
                    ShowUserError("slipAdopt", "ลงรับเงินจากสลิป #" + slipId + " ไม่สำเร็จ — " + why
                        + "\nลองอนุมัติที่หน้า \"ตรวจสลิป\" หรือแจ้งผู้ดูแลบัญชี", FileUpload1);
                    code2.Logs(conn, "SlipVerify", "Reserve: อนุมัติสลิปเดิม #" + slipId + " ไม่สำเร็จ — " + why, who);
                    return false;
                }
                try { if (System.IO.File.Exists(SfTempSlipPath())) System.IO.File.Delete(SfTempSlipPath()); } catch { }
                code2.Logs(conn, "SlipVerify", "Reserve: ลงรับเงินจากสลิปเดิม #" + slipId + " (แนบไว้แต่ยังไม่ได้ลงเงิน) ยอด "
                    + amount.ToString("N2") + " ใบเสร็จ " + pr.ReceiptId, who);
                string msg = "✅ ลงรับเงินจากสลิป #" + slipId + " แล้ว" + (string.IsNullOrEmpty(pr.ReceiptId) ? "" : " · ใบเสร็จ " + pr.ReceiptId)
                    + "\nยอดมัดจำ/ประวัติการชำระอัปเดตแล้ว (ไม่ได้สร้างสลิปซ้ำ) — ถ้าแก้ข้อมูลอื่นไว้ในหน้านี้ กรุณาแก้อีกครั้ง";
                ClientScript.RegisterStartupScript(GetType(), "slipAdoptOk",
                    "alert(" + HttpUtility.JavaScriptStringEncode(msg, true) + ");window.location.replace(window.location.href);", true);
                return false;
            }
            catch (Exception ex)
            {
                ShowUserError("slipAdopt", "ลงรับเงินจากสลิปเดิมไม่สำเร็จ: " + ex.Message, FileUpload1);
                try { code2.Logs(conn, "SlipVerify", "Reserve TryAdoptPendingSlip #" + slipId + ": " + ex.Message, who); } catch { }
                return false;
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
