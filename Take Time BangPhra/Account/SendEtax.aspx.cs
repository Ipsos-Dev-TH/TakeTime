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
                    ShowError("ไม่ระบุเลขที่ใบเสร็จ");
                    pnlForm.Visible = false;
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
    }
}
