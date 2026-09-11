using System;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.CRM
{
    /// <summary>
    /// Voucher & สิทธิ์สมาชิก (ฝั่งพนักงาน):
    /// แลกคูปองด้วยโค้ด · ส่วนลดค่าห้องตามวันเข้าพักต่อ tier · แบบคูปอง (template) ·
    /// แจกรายคน/ทั้ง tier · ประวัติ tracking
    /// </summary>
    public partial class MemberVouchers : Page
    {
        private readonly string _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private MemberPortalService _svc;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.CrmLoyalty)) return;
            if (!Feature.Guard(this, "Loyalty", "~/Default")) return;
            if (Session["permission"]?.ToString() != "True") { Response.Redirect("~/Admin/Login"); return; }

            _svc = new MemberPortalService(_conn);
            if (!IsPostBack) BindAll();
        }

        private void BindAll()
        {
            BindDiscounts();
            BindTemplates();
            BindHistory();
        }

        // ── ① แลกคูปอง ──
        protected void btnRedeem_Click(object sender, EventArgs e)
        {
            _svc = _svc ?? new MemberPortalService(_conn);
            short? adminId = null;
            short tmp;
            if (short.TryParse(Session["UserID"]?.ToString(), out tmp)) adminId = tmp;

            var (ok, msg, _) = _svc.RedeemByCode(txtRedeemCode.Text, adminId, txtRedeemNote.Text?.Trim());
            Msg(msg, ok);
            if (ok) { txtRedeemCode.Text = ""; txtRedeemNote.Text = ""; }
            BindAll();
        }

        // ── ② ส่วนลดค่าห้อง ──
        private void BindDiscounts()
        {
            var sb = new StringBuilder();
            var dt = _svc.GetRoomDiscountRules();
            if (dt == null)
            {
                litDiscRows.Text = "<tr><td colspan='3' style='color:#c62828;'>ยังไม่ได้รัน migration PHASE18_25</td></tr>";
                return;
            }
            foreach (DataRow r in dt.Rows)
            {
                int tid = Convert.ToInt32(r["Tier_ID"]);
                sb.Append("<tr>");
                sb.Append($"<td><span style='display:inline-block;width:10px;height:10px;border-radius:50%;background:{r["TierColor"]};margin-right:7px;'></span>"
                          + Server.HtmlEncode(r["TierName"].ToString()) + "</td>");
                sb.Append($"<td><input type='number' name='wd_{tid}' min='0' max='100' step='0.5' value='{Convert.ToDecimal(r["Weekday_Pct"]):0.##}' /></td>");
                sb.Append($"<td><input type='number' name='we_{tid}' min='0' max='100' step='0.5' value='{Convert.ToDecimal(r["Weekend_Pct"]):0.##}' /></td>");
                sb.Append("</tr>");
            }
            litDiscRows.Text = sb.ToString();
        }

        protected void btnSaveDisc_Click(object sender, EventArgs e)
        {
            _svc = _svc ?? new MemberPortalService(_conn);
            try
            {
                int saved = 0;
                foreach (string key in Request.Form.AllKeys)
                {
                    if (key == null) continue;
                    bool wd = key.StartsWith("wd_"), we = key.StartsWith("we_");
                    if (!wd && !we) continue;
                    int tid; decimal pct;
                    if (!int.TryParse(key.Substring(3), out tid)) continue;
                    decimal.TryParse(Request.Form[key], out pct);
                    _svc.SetRoomDiscount(tid, wd ? "WEEKDAY" : "WEEKEND", pct);
                    saved++;
                }
                Msg($"บันทึกส่วนลดแล้ว ({saved / 2} ระดับ) — สมาชิกเห็นบนบัตรทันที", true);
            }
            catch (Exception ex) { Msg("บันทึกไม่สำเร็จ: " + ex.Message, false); }
            BindAll();
        }

        // ── ③ แบบคูปอง ──
        private void BindTemplates()
        {
            var sb = new StringBuilder();
            var dt = _svc.GetTemplates();
            if (dt != null)
                foreach (DataRow r in dt.Rows)
                {
                    bool active = Convert.ToBoolean(r["Is_Active"]);
                    sb.Append("<tr>");
                    sb.Append("<td><b>" + Server.HtmlEncode(r["Name"].ToString()) + "</b>" +
                        "<div style='font-size:12px;color:#90a4ae;'>" +
                        Server.HtmlEncode(r["Description"] == DBNull.Value ? "" : r["Description"].ToString()) + "</div></td>");
                    sb.Append("<td>" + Server.HtmlEncode(r["Code_Prefix"].ToString()) + "</td>");
                    sb.Append("<td>" + r["Valid_Days"] + "</td>");
                    sb.Append("<td>" + r["Redeem_Window_Min"] + "</td>");
                    sb.Append("<td>" + (active ? "<span class='st st-REDEEMED'>ใช้งาน</span>" : "<span class='st st-EXPIRED'>ปิด</span>") + "</td>");
                    sb.Append($"<td><a href='?edit={r["ID"]}'>แก้ไข</a></td>");
                    sb.Append("</tr>");
                }
            litTplRows.Text = sb.ToString();

            // โหมดแก้ไข (?edit=id)
            int editId;
            if (!IsPostBack && int.TryParse(Request.QueryString["edit"], out editId))
            {
                var t = _svc.GetTemplates();
                foreach (DataRow r in t.Rows)
                    if (Convert.ToInt32(r["ID"]) == editId)
                    {
                        hfTplId.Value = editId.ToString();
                        txtTplName.Text = r["Name"].ToString();
                        txtTplDesc.Text = r["Description"] == DBNull.Value ? "" : r["Description"].ToString();
                        txtTplPrefix.Text = r["Code_Prefix"].ToString();
                        txtTplDays.Text = r["Valid_Days"].ToString();
                        txtTplWindow.Text = r["Redeem_Window_Min"].ToString();
                        chkTplActive.Checked = Convert.ToBoolean(r["Is_Active"]);
                    }
            }

            // dropdown แจก
            ddlIssueTpl.Items.Clear();
            var act = _svc.GetTemplates(activeOnly: true);
            if (act != null)
                foreach (DataRow r in act.Rows)
                    ddlIssueTpl.Items.Add(new ListItem(r["Name"].ToString(), r["ID"].ToString()));

            ddlIssueTier.Items.Clear();
            var tiers = new code().DatabaseQuerySafe(_conn,
                "SELECT ID, TierName FROM Loyalty_Tiers WHERE IsActive = 1 ORDER BY DisplayOrder", null);
            if (tiers != null)
                foreach (DataRow r in tiers.Rows)
                    ddlIssueTier.Items.Add(new ListItem(r["TierName"].ToString(), r["ID"].ToString()));
        }

        protected void btnSaveTpl_Click(object sender, EventArgs e)
        {
            _svc = _svc ?? new MemberPortalService(_conn);
            string name = (txtTplName.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name)) { Msg("กรุณาระบุชื่อคูปอง", false); BindAll(); return; }

            int id; int.TryParse(hfTplId.Value, out id);
            int days; int.TryParse(txtTplDays.Text, out days);
            int win; int.TryParse(txtTplWindow.Text, out win);
            try
            {
                _svc.SaveTemplate(id, name, (txtTplDesc.Text ?? "").Trim(), txtTplPrefix.Text,
                    null, days, win, chkTplActive.Checked);
                hfTplId.Value = "0"; txtTplName.Text = ""; txtTplDesc.Text = "";
                Msg("บันทึกแบบคูปองแล้ว", true);
            }
            catch (Exception ex) { Msg("บันทึกไม่สำเร็จ: " + ex.Message, false); }
            BindAll();
        }

        // ── ④ แจก ──
        protected void btnIssueOne_Click(object sender, EventArgs e)
        {
            _svc = _svc ?? new MemberPortalService(_conn);
            int tpl; int.TryParse(ddlIssueTpl.SelectedValue, out tpl);
            var (n, msg) = _svc.IssueToMember(tpl, txtIssuePhone.Text, Session["UserName"]?.ToString());
            Msg(msg, n > 0);
            if (n > 0) txtIssuePhone.Text = "";
            BindAll();
        }

        protected void btnIssueTier_Click(object sender, EventArgs e)
        {
            _svc = _svc ?? new MemberPortalService(_conn);
            int tpl; int.TryParse(ddlIssueTpl.SelectedValue, out tpl);
            int tier; int.TryParse(ddlIssueTier.SelectedValue, out tier);
            var (n, msg) = _svc.IssueToTier(tpl, tier, Session["UserName"]?.ToString());
            Msg(msg, n > 0);
            BindAll();
        }

        // ── ⑤ ประวัติ ──
        private void BindHistory()
        {
            var sb = new StringBuilder();
            var dt = _svc.GetRecentVouchers();
            if (dt != null)
                foreach (DataRow r in dt.Rows)
                {
                    string st = r["Status"].ToString();
                    sb.Append("<tr>");
                    sb.Append("<td><code>" + Server.HtmlEncode(r["Code"].ToString()) + "</code></td>");
                    sb.Append("<td>" + Server.HtmlEncode(r["Name"].ToString()) + "</td>");
                    string cust = r["CustomerName"] == DBNull.Value ? "" : r["CustomerName"].ToString();
                    sb.Append("<td>" + Server.HtmlEncode(cust) + "<div style='font-size:11.5px;color:#90a4ae;'>" +
                              Server.HtmlEncode(r["Customer_MobilePhone"].ToString()) + "</div></td>");
                    sb.Append($"<td><span class='st st-{st}'>{StatusThai(st)}</span></td>");
                    sb.Append("<td>" + Convert.ToDateTime(r["Expiry_Date"]).ToString("dd/MM/yy") + "</td>");
                    string usedInfo = "";
                    if (r["Redeemed_Date"] != DBNull.Value)
                    {
                        usedInfo = Convert.ToDateTime(r["Redeemed_Date"]).ToString("dd/MM HH:mm");
                        if (r["RedeemedBy"] != DBNull.Value) usedInfo += " · " + r["RedeemedBy"];
                        string note = r["Redeem_Note"] == DBNull.Value ? "" : r["Redeem_Note"].ToString();
                        if (!string.IsNullOrEmpty(note)) usedInfo += "<div style='font-size:11.5px;color:#90a4ae;'>" + Server.HtmlEncode(note) + "</div>";
                    }
                    sb.Append("<td>" + usedInfo + "</td>");
                    sb.Append("</tr>");
                }
            litHistory.Text = sb.Length > 0 ? sb.ToString()
                : "<tr><td colspan='6' style='color:#90a4ae; padding:14px;'>ยังไม่มีคูปองในระบบ</td></tr>";
        }

        private static string StatusThai(string st)
        {
            switch (st)
            {
                case "ISSUED": return "แจกแล้ว";
                case "ACTIVATED": return "ลูกค้ากดใช้ (รอแลก)";
                case "REDEEMED": return "แลกแล้ว";
                case "EXPIRED": return "หมดอายุ";
                case "CANCELLED": return "ยกเลิก";
                default: return st;
            }
        }

        private void Msg(string text, bool ok)
        {
            pnlMsg.Visible = true;
            divMsg.Attributes["class"] = "mv-msg " + (ok ? "mv-ok" : "mv-err");
            litMsg.Text = Server.HtmlEncode(text ?? "");
        }
    }
}
