using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.CRM
{
    /// <summary>
    /// จัดการสมาชิกครบวงจร: ค้นหา → โปรไฟล์ → สมัคร/ต่ออายุ (เก็บค่าสมัคร + ลงรายได้อัตโนมัติ)
    /// → ตัดสิทธิ์คูปองรายคน → ประวัติการใช้งาน/การชำระ → ตั้งค่าค่าสมัครต่อระดับ (Owner)
    /// </summary>
    public partial class MemberHub : Page
    {
        private readonly string _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private readonly code _codeHelper = new code();
        private MemberPortalService _svc;

        private string SelectedPhone
        {
            get => ViewState["MHPhone"] as string;
            set => ViewState["MHPhone"] = value;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.CrmLoyalty)) return;
            if (!Feature.Guard(this, "Loyalty", "~/Default")) return;
            if (Session["permission"]?.ToString() != "True") { Response.Redirect("~/Admin/Login"); return; }

            _svc = new MemberPortalService(_conn);

            if (!IsPostBack)
            {
                string qp = Request.QueryString["phone"];
                if (!string.IsNullOrEmpty(qp)) { SelectedPhone = qp; BindMember(); }
                BindTierConfig();
            }
        }

        // ── ① ค้นหา ──────────────────────────────────────────────────────────
        protected void btnSearch_Click(object sender, EventArgs e)
        {
            string q = (txtSearch.Text ?? "").Trim();
            if (q.Length < 2) { Msg("พิมพ์อย่างน้อย 2 ตัวอักษร", false); return; }

            var dt = _codeHelper.DatabaseQuerySafe(_conn,
                @"SELECT TOP 20 cl.Customer_MobilePhone, c.Name, t.TierName, t.TierColor, cl.Membership_Expiry
                    FROM Customer_Loyalty cl
                    JOIN Loyalty_Tiers t ON t.ID = cl.CurrentTier_ID
                    LEFT JOIN Customer c ON c.MobilePhone = cl.Customer_MobilePhone
                   WHERE cl.Customer_MobilePhone LIKE @q OR c.Name LIKE @q
                   ORDER BY cl.LastUpdated DESC",
                new Dictionary<string, object> { { "@q", "%" + q + "%" } });

            var sb = new StringBuilder();
            if (dt != null && dt.Rows.Count > 0)
            {
                foreach (DataRow r in dt.Rows)
                {
                    string phone = r["Customer_MobilePhone"].ToString();
                    string exp = r["Membership_Expiry"] == DBNull.Value ? "ตลอดชีพ"
                        : Convert.ToDateTime(r["Membership_Expiry"]).ToString("dd/MM/yyyy");
                    sb.Append($"<a class='mh-result' href='?phone={Server.UrlEncode(phone)}'>");
                    sb.Append($"<b>{Server.HtmlEncode(r["Name"] == DBNull.Value ? phone : r["Name"].ToString())}</b> · {Server.HtmlEncode(phone)} ");
                    sb.Append($"<span class='tierpill' style='background:{r["TierColor"]}; font-size:11px;'>{Server.HtmlEncode(r["TierName"].ToString())}</span>");
                    sb.Append($" <span style='color:#90a4ae; font-size:12px;'>หมดอายุ {exp}</span></a>");
                }
            }
            else
            {
                // ยังไม่เป็นสมาชิก — ถ้าเป็นเบอร์โทร ให้เปิดโปรไฟล์เปล่าเพื่อสมัครได้เลย
                string digits = q.Replace("-", "").Replace(" ", "");
                bool isPhone = digits.Length >= 9 && long.TryParse(digits, out _);
                sb.Append("<div style='color:#90a4ae; font-size:13.5px; margin-top:8px;'>ไม่พบสมาชิก");
                if (isPhone)
                    sb.Append($" — <a href='?phone={Server.UrlEncode(digits)}'>สมัครสมาชิกใหม่ให้เบอร์ {Server.HtmlEncode(digits)} →</a>");
                sb.Append("</div>");
            }
            litSearchResults.Text = sb.ToString();
        }

        // ── ② โปรไฟล์ + bind ทุกส่วน ─────────────────────────────────────────
        private void BindMember()
        {
            string phone = SelectedPhone;
            if (string.IsNullOrEmpty(phone)) { pnlMember.Visible = false; return; }
            pnlMember.Visible = true;

            var card = _svc.GetCard(phone);
            bool isMember = card != null;

            string name = "-", tierName = "ยังไม่เป็นสมาชิก", tierColor = "#90a4ae";
            if (isMember)
            {
                name = card["CustomerName"] == DBNull.Value || card["CustomerName"].ToString() == ""
                    ? "-" : card["CustomerName"].ToString();
                tierName = card["TierName"].ToString();
                tierColor = card["TierColor"] == DBNull.Value ? "#8D6E63" : card["TierColor"].ToString();
                litSince.Text = card["MemberSince"] == DBNull.Value ? "-"
                    : Convert.ToDateTime(card["MemberSince"]).ToString("dd/MM/yyyy");
                litPoints.Text = Convert.ToInt32(card["AvailablePoints"] == DBNull.Value ? 0 : card["AvailablePoints"]).ToString("N0");
                bool expired = _svc.IsMembershipExpired(card);
                litExpiry.Text = (card["Membership_Expiry"] == DBNull.Value ? "ตลอดชีพ"
                    : Convert.ToDateTime(card["Membership_Expiry"]).ToString("dd/MM/yyyy"))
                    + (expired ? " <span class='st st-off'>หมดอายุ</span>" : "");
                if (card["Membership_Expiry"] != DBNull.Value)
                    txtExpiry.Text = Convert.ToDateTime(card["Membership_Expiry"]).ToString("yyyy-MM-dd");
            }
            else
            {
                litSince.Text = "-"; litPoints.Text = "-"; litExpiry.Text = "-";
            }

            // สถานะ PIN
            var pin = _codeHelper.DatabaseQuerySafe(_conn,
                "SELECT Member_PIN_Hash FROM Customer_Loyalty WHERE Customer_MobilePhone = @p",
                new Dictionary<string, object> { { "@p", phone } });
            litPinState.Text = (pin != null && pin.Rows.Count > 0 && pin.Rows[0][0] != DBNull.Value)
                ? "<span class='st st-on'>ตั้งแล้ว</span>" : "<span class='st st-hold'>ยังไม่ตั้ง (ใช้เลขท้ายเบอร์)</span>";

            litName.Text = Server.HtmlEncode(name);
            litPhone.Text = Server.HtmlEncode(phone);
            litTier.Text = Server.HtmlEncode(tierName);
            spanTier.Style["background"] = tierColor;
            divAvatar.Style["background"] = tierColor;
            litInitial.Text = Server.HtmlEncode(name.Length > 0 && name != "-" ? name.Substring(0, 1) : "?");

            BindEnroll();
            BindMatrix(phone);
            BindUsage(phone);
        }

        // ── ③ สมัคร/ต่ออายุ ──────────────────────────────────────────────────
        private void BindEnroll()
        {
            ddlEnrollTier.Items.Clear();
            var fees = _svc.GetTierFees();
            if (fees != null)
                foreach (DataRow r in fees.Rows)
                {
                    int months = Convert.ToInt32(r["Duration_Months"]);
                    string label = $"{r["TierName"]} — ฿{Convert.ToDecimal(r["Signup_Fee"]):N0}" +
                                   (months > 0 ? $" / {months} เดือน" : " / ตลอดชีพ");
                    ddlEnrollTier.Items.Add(new ListItem(label, r["ID"].ToString()));
                }
            ddlEnrollTier_Changed(null, null);

            ddlPaidHow.Items.Clear();
            var ph = _codeHelper.DatabaseQuerySafe(_conn,
                "SELECT Paid_How FROM Account_Paid_How WHERE Status = 'True' ORDER BY ID", null);
            if (ph != null)
                foreach (DataRow r in ph.Rows)
                    ddlPaidHow.Items.Add(r["Paid_How"].ToString());
            if (ddlPaidHow.Items.Count == 0) ddlPaidHow.Items.Add("เงินสด");
        }

        protected void ddlEnrollTier_Changed(object sender, EventArgs e)
        {
            var fees = _svc?.GetTierFees() ?? new MemberPortalService(_conn).GetTierFees();
            foreach (DataRow r in fees.Rows)
                if (r["ID"].ToString() == ddlEnrollTier.SelectedValue)
                {
                    txtEnrollFee.Text = Convert.ToDecimal(r["Signup_Fee"]).ToString("0.##");
                    int months = Convert.ToInt32(r["Duration_Months"]);
                    divEnrollHint.InnerHtml =
                        $"ต่ออายุ = นับต่อจากวันหมดอายุเดิมที่ยังเหลือ · อายุระดับนี้ {(months > 0 ? months + " เดือน" : "ตลอดชีพ")} · " +
                        "ยอด > 0 จะออกใบรับเงิน MBR-xxx และส่งลงบัญชี NextAcc เป็น \"รายได้ค่าสมัครสมาชิก\" อัตโนมัติ";
                }
        }

        protected void btnEnroll_Click(object sender, EventArgs e)
        {
            _svc = _svc ?? new MemberPortalService(_conn);
            int tier; int.TryParse(ddlEnrollTier.SelectedValue, out tier);
            decimal fee; decimal.TryParse(txtEnrollFee.Text, out fee);
            var (ok, msg) = _svc.Enroll(SelectedPhone, tier, fee, ddlPaidHow.SelectedValue,
                Session["UserName"]?.ToString());
            Msg(msg, ok);
            BindMember();
        }

        protected void btnResetPin_Click(object sender, EventArgs e)
        {
            _svc = _svc ?? new MemberPortalService(_conn);
            _svc.ResetPin(SelectedPhone);
            Msg("รีเซ็ต PIN แล้ว — สมาชิกล็อกอินด้วยเลขท้ายเบอร์ 4 ตัว แล้วระบบบังคับตั้งใหม่", true);
            BindMember();
        }

        protected void btnSetExpiry_Click(object sender, EventArgs e)
        {
            _svc = _svc ?? new MemberPortalService(_conn);
            DateTime d;
            DateTime? expiry = DateTime.TryParse(txtExpiry.Text, out d) ? (DateTime?)d : null;
            _svc.SetMembershipExpiry(SelectedPhone, expiry);
            Msg("บันทึกวันหมดอายุแล้ว", true);
            BindMember();
        }

        // ── ④ สิทธิ์คูปองรายคน ────────────────────────────────────────────────
        private void BindMatrix(string phone)
        {
            var sb = new StringBuilder();
            var dt = _svc.GetMemberVoucherMatrix(phone);
            if (dt != null)
                foreach (DataRow r in dt.Rows)
                {
                    if (!Convert.ToBoolean(r["Is_Active"])) continue;
                    int id = Convert.ToInt32(r["ID"]);
                    bool excluded = Convert.ToInt32(r["Excluded"]) == 1;
                    int held = Convert.ToInt32(r["Held"]);
                    sb.Append("<tr>");
                    sb.Append("<td><b>" + Server.HtmlEncode(r["Name"].ToString()) + "</b>" +
                              "<div style='font-size:12px;color:#90a4ae;'>" +
                              Server.HtmlEncode(r["Description"] == DBNull.Value ? "" : r["Description"].ToString()) + "</div></td>");
                    sb.Append("<td>" + (held > 0 ? held + " ใบ" : "-") + "</td>");
                    sb.Append($"<td style='text-align:center;'><input type='checkbox' name='excl_{id}' value='1'{(excluded ? " checked" : "")} />" +
                              $"<input type='hidden' name='exclrow_{id}' value='1' /></td>");
                    sb.Append($"<td><a href='?phone={Server.UrlEncode(phone)}&issue={id}' " +
                              "onclick=\"return confirm('แจกคูปองนี้ให้สมาชิก 1 ใบ?');\">🎁 แจก 1 ใบ</a></td>");
                    sb.Append("</tr>");
                }
            litVoucherMatrix.Text = sb.Length > 0 ? sb.ToString()
                : "<tr><td colspan='4' style='color:#90a4ae;'>ยังไม่มีแบบคูปอง — สร้างได้ที่หน้า Voucher & สิทธิ์สมาชิก</td></tr>";

            // ?issue=templateId — แจกทันที (ลิงก์จากตาราง)
            int issueId;
            if (!IsPostBack && int.TryParse(Request.QueryString["issue"], out issueId))
            {
                var (n, msg) = _svc.IssueToMember(issueId, phone, Session["UserName"]?.ToString());
                Msg(msg, n > 0);
                if (n > 0) BindUsage(phone);
            }
        }

        protected void btnSaveMatrix_Click(object sender, EventArgs e)
        {
            _svc = _svc ?? new MemberPortalService(_conn);
            string phone = SelectedPhone;
            int changed = 0;
            foreach (string key in Request.Form.AllKeys)
            {
                if (key == null || !key.StartsWith("exclrow_")) continue;
                int id;
                if (!int.TryParse(key.Substring(8), out id)) continue;
                bool wantExcluded = Request.Form["excl_" + id] == "1";
                _svc.SetExclusion(phone, id, wantExcluded, Session["UserName"]?.ToString());
                changed++;
            }
            Msg($"บันทึกสิทธิ์คูปองแล้ว ({changed} รายการ) — ใบที่ยังไม่ใช้ของคูปองที่ถูกตัดสิทธิ์ถูกยกเลิกให้แล้ว", true);
            BindMember();
        }

        // ── ⑤ การใช้งาน ──────────────────────────────────────────────────────
        private void BindUsage(string phone)
        {
            var sb = new StringBuilder();
            var dt = _svc.GetMemberVouchers(phone);
            if (dt != null)
                foreach (DataRow r in dt.Rows)
                {
                    string st = r["Status"].ToString();
                    sb.Append("<tr>");
                    sb.Append("<td><code>" + Server.HtmlEncode(r["Code"].ToString()) + "</code></td>");
                    sb.Append("<td>" + Server.HtmlEncode(r["Name"].ToString()) + "</td>");
                    sb.Append("<td>" + VoucherStatusThai(st) + "</td>");
                    sb.Append("<td>" + Convert.ToDateTime(r["Expiry_Date"]).ToString("dd/MM/yy") + "</td>");
                    sb.Append("<td>" + (r["Redeemed_Date"] == DBNull.Value ? "-"
                        : Convert.ToDateTime(r["Redeemed_Date"]).ToString("dd/MM HH:mm")) + "</td>");
                    sb.Append("<td>" + (st == "ISSUED" || st == "ACTIVATED"
                        ? $"<a href='?phone={Server.UrlEncode(phone)}&cancel={r["ID"]}' " +
                          "onclick=\"return confirm('ยกเลิก voucher ใบนี้?');\" style='color:#c62828;'>ยกเลิก</a>"
                        : "") + "</td>");
                    sb.Append("</tr>");
                }
            litUsage.Text = sb.Length > 0 ? sb.ToString()
                : "<tr><td colspan='6' style='color:#90a4ae;'>ยังไม่มีคูปอง</td></tr>";

            long cancelId;
            if (!IsPostBack && long.TryParse(Request.QueryString["cancel"], out cancelId))
            {
                if (_svc.CancelVoucher(cancelId, Session["UserName"]?.ToString()))
                    Msg("ยกเลิก voucher แล้ว", true);
            }

            var pay = _svc.GetMemberPayments(phone);
            var sb2 = new StringBuilder();
            if (pay != null)
                foreach (DataRow r in pay.Rows)
                {
                    sb2.Append("<tr>");
                    sb2.Append("<td>" + Convert.ToDateTime(r["Created_Date"]).ToString("dd/MM/yy HH:mm") + "</td>");
                    string act = r["Action_Type"].ToString() == "NEW" ? "สมัครใหม่"
                        : r["Action_Type"].ToString() == "UPGRADE" ? "อัปเกรด" : "ต่ออายุ";
                    sb2.Append("<td>" + act + " → " + Server.HtmlEncode(r["TierName"].ToString()) + "</td>");
                    sb2.Append("<td>฿" + Convert.ToDecimal(r["Amount"]).ToString("N2") + "</td>");
                    sb2.Append("<td>" + Server.HtmlEncode(r["Paid_How"] == DBNull.Value ? "-" : r["Paid_How"].ToString()) + "</td>");
                    sb2.Append("<td>" + Server.HtmlEncode(r["Receipt_Ref"] == DBNull.Value ? "-" : r["Receipt_Ref"].ToString()) + "</td>");
                    sb2.Append("<td>" + Server.HtmlEncode(r["Created_By"] == DBNull.Value ? "-" : r["Created_By"].ToString()) + "</td>");
                    sb2.Append("</tr>");
                }
            litPayments.Text = sb2.Length > 0 ? sb2.ToString()
                : "<tr><td colspan='6' style='color:#90a4ae;'>ยังไม่มีการชำระค่าสมาชิก</td></tr>";
        }

        private static string VoucherStatusThai(string st)
        {
            switch (st)
            {
                case "ISSUED": return "<span class='st st-hold'>พร้อมใช้</span>";
                case "ACTIVATED": return "<span class='st st-hold'>กดใช้แล้ว (รอแลก)</span>";
                case "REDEEMED": return "<span class='st st-on'>ใช้แล้ว</span>";
                case "EXPIRED": return "<span class='st st-off'>หมดอายุ</span>";
                case "CANCELLED": return "<span class='st st-off'>ยกเลิก</span>";
                default: return st;
            }
        }

        // ── ⑥ ตั้งค่าระดับ (Owner) ────────────────────────────────────────────
        private void BindTierConfig()
        {
            if (Session["User"]?.ToString() != "Owner") { pnlTierConfig.Visible = false; return; }
            pnlTierConfig.Visible = true;

            var sb = new StringBuilder();
            var fees = _svc.GetTierFees();
            if (fees != null)
                foreach (DataRow r in fees.Rows)
                {
                    int id = Convert.ToInt32(r["ID"]);
                    sb.Append("<tr>");
                    sb.Append($"<td><span style='display:inline-block;width:10px;height:10px;border-radius:50%;background:{r["TierColor"]};margin-right:7px;'></span>"
                              + Server.HtmlEncode(r["TierName"].ToString()) + "</td>");
                    sb.Append($"<td><input type='number' name='fee_{id}' min='0' step='1' value='{Convert.ToDecimal(r["Signup_Fee"]):0.##}' style='width:110px;' /></td>");
                    sb.Append($"<td><input type='number' name='mon_{id}' min='0' step='1' value='{r["Duration_Months"]}' style='width:90px;' /></td>");
                    sb.Append("</tr>");
                }
            litTierFees.Text = sb.ToString();
        }

        protected void btnSaveTierFees_Click(object sender, EventArgs e)
        {
            _svc = _svc ?? new MemberPortalService(_conn);
            foreach (string key in Request.Form.AllKeys)
            {
                if (key == null || !key.StartsWith("fee_")) continue;
                int id;
                if (!int.TryParse(key.Substring(4), out id)) continue;
                decimal fee; decimal.TryParse(Request.Form["fee_" + id], out fee);
                int mon; int.TryParse(Request.Form["mon_" + id], out mon);
                _svc.SaveTierFee(id, fee, mon);
            }
            Msg("บันทึกค่าสมัคร/อายุสมาชิกแล้ว", true);
            BindTierConfig();
            if (!string.IsNullOrEmpty(SelectedPhone)) BindMember();
        }

        private void Msg(string text, bool ok)
        {
            pnlMsg.Visible = true;
            divMsg.Attributes["class"] = "mh-msg " + (ok ? "mh-ok" : "mh-err");
            litMsg.Text = Server.HtmlEncode(text ?? "");
        }
    }
}
