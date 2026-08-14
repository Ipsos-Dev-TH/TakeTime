using System;
using System.Configuration;
using System.Web.UI;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Account
{
    public partial class SendEtax : System.Web.UI.Page
    {
        private readonly string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.FinReceipt)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            // สิทธิ์เดียวกับหน้าเอกสาร (Owner/Admin)
            if (!(Session["permission"]?.ToString() == "True"
                  && (Session["User"]?.ToString() == "Owner" || Session["User"]?.ToString() == "Admin")))
            {
                Response.Redirect("/Default");
                return;
            }

            if (!IsPostBack)
            {
                string receipt = (Request.QueryString["receipt"] ?? "").Trim();
                if (string.IsNullOrEmpty(receipt))
                {
                    // เข้าเมนูตรง ๆ (ไม่ได้มาจากหน้าเอกสาร) → แสดง "รายการ e-Tax ที่ออกแล้ว"
                    // ให้เลือกส่งได้เลย แทนที่จะขึ้น "ไม่ระบุเลขที่ใบเสร็จ" แล้วจบ
                    pnlForm.Visible = false;
                    pnlList.Visible = true;
                    BindEtaxList(null);
                    return;
                }

                try
                {
                    var svc = new AccountingSyncService(conn);
                    var info = svc.GetEtaxComposeInfo(receipt);
                    if (!info.HasEtax)
                    {
                        ShowError(info.Message ?? "ใบนี้ยังไม่มี e-Tax");
                        pnlForm.Visible = false;
                        return;
                    }

                    ViewState["receipt"] = receipt;
                    litReceipt.Text = Server.HtmlEncode(receipt);
                    litGuest.Text = Server.HtmlEncode(string.IsNullOrEmpty(info.GuestName) ? "-" : info.GuestName);
                    litAmount.Text = info.Amount.ToString("N2") + " บาท";
                    txtTo.Text = info.ToEmail ?? "";
                    txtCc.Text = info.CcEmail ?? "";
                    txtSubject.Text = info.Subject ?? "";
                    txtBody.Text = info.Body ?? "";
                    chkPdf.Checked = info.AttachPdf;
                    chkXml.Checked = info.AttachXml;

                    // ลิงก์ดูใบก่อนส่ง (เปิด PDF จากหน้าเอกสาร)
                    lnkPreview.NavigateUrl = "/API/ViewReceiptDoc.ashx?doc=" + Server.UrlEncode(receipt);
                    lnkPreview.Visible = true;
                }
                catch (Exception ex)
                {
                    ShowError("โหลดข้อมูลไม่สำเร็จ: " + ex.Message);
                    pnlForm.Visible = false;
                }
            }
        }

        protected void btnSend_Click(object sender, EventArgs e)
        {
            string receipt = ViewState["receipt"]?.ToString() ?? (Request.QueryString["receipt"] ?? "").Trim();
            if (string.IsNullOrEmpty(receipt)) { ShowError("ไม่ระบุเลขที่ใบเสร็จ"); return; }

            string to = (txtTo.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(to) || !to.Contains("@")) { ShowError("กรุณาระบุอีเมลผู้รับให้ถูกต้อง"); return; }

            try
            {
                Server.ScriptTimeout = 300;
                var svc = new AccountingSyncService(conn);
                var task = System.Threading.Tasks.Task.Run(() =>
                    svc.SendEtaxEmailComposedAsync(receipt, to, (txtCc.Text ?? "").Trim(),
                        txtSubject.Text, txtBody.Text, chkPdf.Checked, chkXml.Checked));

                (bool success, string message) result = (false, "หมดเวลา");
                if (task.Wait(60000)) result = task.Result;

                if (result.success)
                {
                    pnlForm.Visible = false;
                    litMsg.Text = "<div class='msg-ok'>✅ " + Server.HtmlEncode(result.message) +
                        "<br/><a href='/Account/CheckDocument_New'>← กลับหน้าเอกสาร</a></div>";
                }
                else
                {
                    ShowError("ส่งไม่สำเร็จ: " + result.message);
                }
            }
            catch (Exception ex)
            {
                ShowError("ส่งไม่สำเร็จ: " + ex.Message);
            }
        }

        private void ShowError(string message)
        {
            litMsg.Text = "<div class='msg-err'>⚠ " + Server.HtmlEncode(message ?? "") + "</div>";
        }
    
        /// <summary>รายการ e-Tax ที่ออกแล้ว — กดส่งอีเมลได้จากที่นี่</summary>
        private void BindEtaxList(string search)
        {
            var svc = new AccountingSyncService(conn);
            var dt = svc.GetEtaxSendableList(100, search);
            var sb = new System.Text.StringBuilder();

            if (dt != null)
            {
                foreach (System.Data.DataRow r in dt.Rows)
                {
                    string rc = r["Receipt_Number"]?.ToString() ?? "";
                    bool sent = r["EmailSent"] != DBNull.Value && Convert.ToInt32(r["EmailSent"]) == 1;
                    string email = r["CustomerEmail"] == DBNull.Value ? "" : r["CustomerEmail"].ToString();

                    sb.Append("<tr style='border-bottom:1px solid #f0f3f5;'>");
                    sb.Append("<td style='padding:8px 10px;'><b>" + Server.HtmlEncode(rc) + "</b></td>");
                    sb.Append("<td style='padding:8px 10px;'>" +
                        (r["Reservation_ID"] == DBNull.Value ? "-" : "#" + r["Reservation_ID"]) + "</td>");
                    sb.Append("<td style='padding:8px 10px;'>" +
                        Server.HtmlEncode(r["GuestName"] == DBNull.Value ? "-" : r["GuestName"].ToString()) +
                        (string.IsNullOrEmpty(email) ? "<div style='font-size:11.5px;color:#e65100;'>ยังไม่มีอีเมล</div>"
                            : "<div style='font-size:11.5px;color:#90a4ae;'>" + Server.HtmlEncode(email) + "</div>") + "</td>");
                    sb.Append("<td style='padding:8px 10px; text-align:right;'>" +
                        (r["Amount"] == DBNull.Value ? "-" : Convert.ToDecimal(r["Amount"]).ToString("N2")) + "</td>");
                    sb.Append("<td style='padding:8px 10px;'>" +
                        (r["Created_Date"] == DBNull.Value ? "-" : Convert.ToDateTime(r["Created_Date"]).ToString("dd/MM/yy HH:mm")) + "</td>");
                    sb.Append("<td style='padding:8px 10px; text-align:center;'>" + (sent
                        ? "<span style='background:#e8f5e9;color:#1e7e42;padding:3px 9px;border-radius:11px;font-size:11px;font-weight:700;'>ส่งแล้ว</span>"
                        : "<span style='background:#fff3e0;color:#e65100;padding:3px 9px;border-radius:11px;font-size:11px;font-weight:700;'>ยังไม่ส่ง</span>") + "</td>");
                    // สถานะจากกรมสรรพากร (มาจากอีเมลตอบกลับ — PHASE18_28)
                    bool rdOk = dt.Columns.Contains("Rd_Confirmed_Date") && r["Rd_Confirmed_Date"] != DBNull.Value;
                    sb.Append("<td style='padding:8px 10px; text-align:center;'>" + (rdOk
                        ? "<span style='background:#e8f5e9;color:#1e7e42;padding:3px 9px;border-radius:11px;font-size:11px;font-weight:700;' title='"
                          + Convert.ToDateTime(r["Rd_Confirmed_Date"]).ToString("dd/MM/yyyy HH:mm") + "'>✅ สรรพากรรับแล้ว</span>"
                        : "<span style='background:#eceff1;color:#78909c;padding:3px 9px;border-radius:11px;font-size:11px;'>รอยืนยัน</span>") + "</td>");
                    sb.Append("<td style='padding:8px 10px; text-align:right;'><a class='btn btn-primary btn-sm' href='?receipt=" +
                        Server.UrlEncode(rc) + "'>" + (sent ? "ส่งอีกครั้ง" : "✉ ส่งอีเมล") + "</a></td>");
                    sb.Append("</tr>");
                }
            }

            litEtaxRows.Text = sb.Length > 0 ? sb.ToString()
                : "<tr><td colspan='8' style='padding:16px; color:#90a4ae;'>ยังไม่มีเอกสาร e-Tax ที่ออกแล้ว " +
                  "— e-Tax จะถูกสร้างอัตโนมัติเมื่อออกใบเสร็จที่ติ๊ก \"ต้องการ e-Tax\" และ NextAcc ประมวลผลเสร็จ</td></tr>";
        }

        protected void btnSearchEtax_Click(object sender, EventArgs e)
        {
            pnlForm.Visible = false;
            pnlList.Visible = true;
            BindEtaxList(txtSearchEtax.Text);
        }
}
}
