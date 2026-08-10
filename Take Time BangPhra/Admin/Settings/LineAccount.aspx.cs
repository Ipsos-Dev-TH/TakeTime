using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// หน้าผูกบัญชี LINE ส่วนตัวของผู้ใช้ (LINE Login) + ตั้งค่า channel (เฉพาะ Owner)
    /// เก็บ userId ไว้ให้ระบบส่งแจ้งเตือนเข้าไลน์รายคนได้
    /// </summary>
    public partial class LineAccount : Page
    {
        private readonly string _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private readonly code _code = new code();
        private LineLoginService _svc;

        private int MyAdminId =>
            Session["UserID"] != null && int.TryParse(Session["UserID"].ToString(), out var id) ? id : 0;
        private bool IsOwner => Session["User"]?.ToString() == "Owner";

        protected void Page_Load(object sender, EventArgs e)
        {
            _svc = new LineLoginService(_conn);

            if (Session["permission"]?.ToString() != "True" || MyAdminId <= 0)
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            if (!IsPostBack) LoadAll();
        }

        private void LoadAll()
        {
            LoadMe();
            pnlConfig.Visible = IsOwner;
            pnlTeam.Visible = IsOwner;
            if (IsOwner)
            {
                LoadConfig();
                LoadTeam();
                LoadRequests();
            }
        }

        // ── บัญชีของฉัน ───────────────────────────────────────────────────────────
        private void LoadMe()
        {
            bool configured = _svc.IsConfigured && _svc.IsEnabled;
            pnlNotConfigured.Visible = !configured;

            var me = _svc.GetAdminLineInfo(MyAdminId);
            bool linked = me != null && me["Line_UserId"] != DBNull.Value
                          && !string.IsNullOrWhiteSpace(me["Line_UserId"].ToString());

            if (linked)
            {
                string pic = me["Line_PictureUrl"] != DBNull.Value ? me["Line_PictureUrl"].ToString() : "";
                litAvatar.Text = string.IsNullOrWhiteSpace(pic)
                    ? "<i class='fab fa-line'></i>"
                    : $"<img src='{Server.HtmlEncode(pic)}' alt='' />";
                litMyName.Text = Server.HtmlEncode(me["Line_DisplayName"]?.ToString() ?? "(ไม่ทราบชื่อ)");

                string linkedAt = me["Line_LinkedDate"] != DBNull.Value
                    ? Convert.ToDateTime(me["Line_LinkedDate"]).ToString("dd/MM/yyyy HH:mm") : "-";
                litMyStatus.Text = $"<span class='pill p-on'>ผูกแล้ว</span> " +
                                   $"ID: <code>{Server.HtmlEncode(LineLoginService.Mask(me["Line_UserId"].ToString()))}</code> " +
                                   $"· ผูกเมื่อ {linkedAt}";

                btnLink.Text = "🔄 ผูกใหม่ (เปลี่ยนบัญชี)";
                btnUnlink.Visible = true;
                btnTestMe.Visible = true;
                chkNotify.Checked = me["Line_NotifyEnabled"] == DBNull.Value || ToBool(me["Line_NotifyEnabled"]);
                chkNotify.Enabled = true;
            }
            else
            {
                litAvatar.Text = "<i class='fab fa-line'></i>";
                litMyName.Text = Server.HtmlEncode(Session["UserName"]?.ToString() ?? "ผู้ใช้");
                litMyStatus.Text = "<span class='pill p-off'>ยังไม่ได้ผูกบัญชี</span> — กดปุ่มด้านขวาเพื่อผูกกับ LINE ของคุณ";
                btnLink.Text = "🔗 ผูกบัญชี LINE";
                btnUnlink.Visible = false;
                btnTestMe.Visible = false;
                chkNotify.Checked = false;
                chkNotify.Enabled = false;
            }

            btnLink.Enabled = configured;
        }

        protected void btnLink_Click(object sender, EventArgs e)
        {
            if (!_svc.IsConfigured || !_svc.IsEnabled)
            {
                ShowMsg("ยังตั้งค่า LINE Login ไม่ครบ หรือยังปิดใช้งานอยู่", false);
                return;
            }

            // state กัน CSRF — ผูกกับ session แล้วตรวจตอน callback
            string state = Guid.NewGuid().ToString("N");
            Session["LineLinkState"] = state;
            Response.Redirect(_svc.BuildAuthorizeUrl(state), false);
            Context.ApplicationInstance.CompleteRequest();
        }

        protected void btnUnlink_Click(object sender, EventArgs e)
        {
            _svc.Unlink(MyAdminId);
            ShowMsg("ยกเลิกการผูกบัญชี LINE แล้ว", true);
            LoadAll();
        }

        protected void btnTestMe_Click(object sender, EventArgs e)
        {
            var (ok, msg) = _svc.SendToAdmin(MyAdminId,
                $"🔔 ทดสอบแจ้งเตือนจากระบบ TakeTime\nส่งเมื่อ {DateTime.Now:dd/MM/yyyy HH:mm} น.");
            ShowMsg(ok ? "ส่งข้อความทดสอบแล้ว — ตรวจสอบใน LINE ของคุณ" : "ส่งไม่สำเร็จ: " + msg, ok);
            LoadAll();
        }

        protected void chkNotify_Changed(object sender, EventArgs e)
        {
            _svc.SetNotifyEnabled(MyAdminId, chkNotify.Checked);
            ShowMsg(chkNotify.Checked ? "เปิดรับแจ้งเตือนทาง LINE แล้ว" : "ปิดรับแจ้งเตือนทาง LINE แล้ว", true);
        }

        // ── คำขอผูกบัญชี (Owner) ──────────────────────────────────────────────────
        private void LoadRequests()
        {
            try
            {
                var dt = _svc.GetPendingLinkRequests();
                int n = dt?.Rows.Count ?? 0;
                gvRequests.DataSource = dt;
                gvRequests.DataBind();
                pnlRequests.Visible = n > 0;
                litReqCount.Text = n > 0 ? $"<span class='pill' style='background:#e67e22;'>{n}</span>" : "";
            }
            catch { pnlRequests.Visible = false; }
        }

        protected void gvRequests_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!IsOwner) return;
            if (!long.TryParse(e.CommandArgument?.ToString(), out long reqId) || reqId <= 0) return;

            if (e.CommandName == "ApproveReq")
            {
                var (ok, msg) = _svc.DecideLinkRequest(reqId, true, MyAdminId);
                ShowMsg(msg, ok);
            }
            else if (e.CommandName == "RejectReq")
            {
                var (ok, msg) = _svc.DecideLinkRequest(reqId, false, MyAdminId, "ผู้ดูแลปฏิเสธคำขอ");
                ShowMsg(msg, ok);
            }
            LoadAll();
        }

        protected string ReqLineCell(object item)
        {
            var r = (DataRowView)item;
            string pic = r["Line_PictureUrl"] != DBNull.Value ? r["Line_PictureUrl"].ToString() : "";
            string name = r["Line_DisplayName"] != DBNull.Value ? r["Line_DisplayName"].ToString() : "(ไม่ทราบชื่อ)";
            string img = string.IsNullOrWhiteSpace(pic)
                ? "<i class='fab fa-line' style='color:#06C755;font-size:20px;'></i>"
                : $"<img src='{Server.HtmlEncode(pic)}' style='width:34px;height:34px;border-radius:50%;vertical-align:middle;' />";
            return $"{img} <b>{Server.HtmlEncode(name)}</b>" +
                   $"<div style='font-size:12px;color:#8a9a90;'>{Server.HtmlEncode(LineLoginService.Mask(r["Line_UserId"].ToString()))}</div>";
        }

        // ── ทีม (Owner) ───────────────────────────────────────────────────────────
        private void LoadTeam()
        {
            gvTeam.DataSource = _svc.GetLinkedAdmins();
            gvTeam.DataBind();
        }

        protected void gvTeam_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!IsOwner) return;
            if (!int.TryParse(e.CommandArgument?.ToString(), out int id) || id <= 0) return;

            if (e.CommandName == "TestSend")
            {
                var (ok, msg) = _svc.SendToAdmin(id,
                    $"🔔 ทดสอบแจ้งเตือนจากระบบ TakeTime\nส่งเมื่อ {DateTime.Now:dd/MM/yyyy HH:mm} น.");
                ShowMsg(ok ? "ส่งข้อความทดสอบแล้ว" : "ส่งไม่สำเร็จ: " + msg, ok);
            }
            else if (e.CommandName == "UnlinkUser")
            {
                _svc.Unlink(id);
                ShowMsg("ยกเลิกการผูกบัญชีของผู้ใช้แล้ว", true);
            }
            LoadAll();
        }

        protected void btnBroadcast_Click(object sender, EventArgs e)
        {
            if (!IsOwner) return;
            string text = txtBroadcast.Text.Trim();
            if (string.IsNullOrEmpty(text)) { ShowMsg("กรุณาพิมพ์ข้อความก่อน", false); return; }

            var (sent, failed, detail) = _svc.Broadcast(text);
            ShowMsg($"ส่งสำเร็จ {sent} คน" + (failed > 0 ? $", ล้มเหลว {failed} คน — {detail}" : ""), sent > 0);
            txtBroadcast.Text = "";
            LoadAll();
        }

        // ── ตั้งค่า (Owner) ───────────────────────────────────────────────────────
        private void LoadConfig()
        {
            ddlEnabled.SelectedValue = _svc.IsEnabled ? "1" : "0";
            txtChannelId.Text = _svc.ChannelId;
            txtCallback.Text = string.IsNullOrWhiteSpace(_svc.CallbackUrl)
                ? GuessCallbackUrl() : _svc.CallbackUrl;
            litSecretStatus.Text = string.IsNullOrWhiteSpace(_svc.ChannelSecret)
                ? "<span style='color:#c0392b;font-weight:400;font-size:12px;'>✗ ยังไม่ได้ตั้ง</span>"
                : "<span style='color:#27ae60;font-weight:400;font-size:12px;'>✓ ตั้งไว้แล้ว (เว้นว่าง = คงเดิม)</span>";
        }

        private string GuessCallbackUrl()
        {
            try
            {
                var u = Request.Url;
                return $"{u.Scheme}://{u.Authority}/Admin/LineLinkCallback";
            }
            catch { return "https://taketimebangphra.com/Admin/LineLinkCallback"; }
        }

        protected void btnSaveConfig_Click(object sender, EventArgs e)
        {
            if (!IsOwner) { ShowMsg("เฉพาะ Owner เท่านั้น", false); return; }
            try
            {
                var cfg = new Integration.AccountingConfig(_conn);
                cfg.SetConfig("LineLogin_Enabled", ddlEnabled.SelectedValue == "1" ? "1" : "0");
                cfg.SetConfig("LineLogin_ChannelId", txtChannelId.Text.Trim());
                cfg.SetConfig("LineLogin_CallbackUrl", txtCallback.Text.Trim());

                // secret: บันทึกเฉพาะเมื่อกรอกใหม่ ("-" = ล้าง)
                string sec = txtChannelSecret.Text.Trim();
                if (sec == "-") cfg.SetConfig("LineLogin_ChannelSecret_Encrypted", "");
                else if (!string.IsNullOrEmpty(sec)) cfg.SetConfig("LineLogin_ChannelSecret_Encrypted", _code.Crypt(sec));

                txtChannelSecret.Text = "";
                ShowMsg("บันทึกการตั้งค่า LINE Login แล้ว", true);
                _svc = new LineLoginService(_conn);   // อ่านค่าใหม่
                LoadAll();
            }
            catch (Exception ex) { ShowMsg("บันทึกไม่สำเร็จ: " + ex.Message, false); }
        }

        // ── formatters ────────────────────────────────────────────────────────────
        protected string LineCell(object item)
        {
            var r = (DataRowView)item;
            if (r["Line_UserId"] == DBNull.Value || string.IsNullOrWhiteSpace(r["Line_UserId"].ToString()))
                return "<span class='pill p-off'>ยังไม่ผูก</span>";
            string name = r["Line_DisplayName"] != DBNull.Value ? r["Line_DisplayName"].ToString() : "";
            string at = r["Line_LinkedDate"] != DBNull.Value
                ? Convert.ToDateTime(r["Line_LinkedDate"]).ToString("dd/MM/yyyy") : "";
            return $"<span class='pill p-on'>ผูกแล้ว</span> {Server.HtmlEncode(name)}" +
                   $"<div style='font-size:12px;color:#8a9a90;'>{Server.HtmlEncode(LineLoginService.Mask(r["Line_UserId"].ToString()))} · {at}</div>";
        }

        protected string NotifyCell(object item)
        {
            var r = (DataRowView)item;
            if (r["Line_UserId"] == DBNull.Value) return "-";
            bool on = r["Line_NotifyEnabled"] == DBNull.Value || ToBool(r["Line_NotifyEnabled"]);
            return on ? "<span style='color:#27ae60;'>✓ เปิด</span>" : "<span style='color:#95a5a6;'>ปิด</span>";
        }

        protected bool HasLine(object item)
        {
            var r = (DataRowView)item;
            return r["Line_UserId"] != DBNull.Value && !string.IsNullOrWhiteSpace(r["Line_UserId"].ToString());
        }

        private void ShowMsg(string msg, bool ok)
        {
            pnlMsg.Visible = true;
            string color = ok ? "#1e7e42" : "#c0392b";
            string icon = ok ? "fa-circle-check" : "fa-circle-exclamation";
            litMsg.Text = $"<div style='color:{color};font-weight:600;'><i class='fas {icon}'></i> {Server.HtmlEncode(msg)}</div>";
        }

        private static bool ToBool(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            if (v is bool b) return b;
            string s = v.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
