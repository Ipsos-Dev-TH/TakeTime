using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text.RegularExpressions;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Payments;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// หน้าตั้งค่าระบบรับชำระเงินออนไลน์ — ทุกค่าที่ระบบใช้คุยกับเกตเวย์อยู่ที่นี่ที่เดียว
    ///
    /// ตั้งใจให้ "แก้สัญญา API ได้จากหน้าเว็บ" เพราะรายละเอียดของเกตเวย์ (เส้นทาง ชื่อฟิลด์
    /// รูปแบบลายเซ็น) ต้องตรงกับเอกสารจริงเป๊ะ ๆ เดาไม่ได้ และไม่ควรต้อง build ใหม่ทุกครั้ง
    /// ที่ผู้ให้บริการปรับเวอร์ชัน
    ///
    /// ตัวควบคุมถูกสร้างจากตาราง Payment_Gateway_Config ตอน Page_Init เพื่อให้ค่าที่กรอก
    /// ส่งกลับมาได้ตามปกติของ WebForms
    /// </summary>
    public partial class PaymentGatewaySettings : Page
    {
        private readonly string _conn =
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        private DataTable _cfg;
        private readonly Dictionary<string, Control> _inputs = new Dictionary<string, Control>();

        protected void Page_Init(object sender, EventArgs e)
        {
            if (!Perm.CanAccess(Perm.SysPayment) && !Perm.CanAccess(Perm.SysSettings))
            {
                Response.Redirect("~/Default", false);
                System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }
            BuildSettingsUi();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            litWebhookUrl.Text = Server.HtmlEncode(PaymentUrls.WebhookUrl());
            litReturnUrl.Text = Server.HtmlEncode(PaymentUrls.ReturnUrl());
            litPayUrl.Text = Server.HtmlEncode(PaymentUrls.SiteBase() + "/Payment/Pay?src=RESERVATION&id=<เลขที่จอง>&ph=<เบอร์ลูกค้า>");

            if (!IsPostBack)
            {
                ShowStateWarning();
                BindTxn();
            }
        }

        // ── วาดฟอร์มตั้งค่า ───────────────────────────────────────────────────

        private void BuildSettingsUi()
        {
            DataTable dt;
            try { dt = PaymentGatewayConfig.GetAllForUi(); }
            catch
            {
                phSettings.Controls.Add(new LiteralControl(
                    "<div class=\"pg-card\"><div class=\"pg-alert warn\">"
                    + "ยังไม่ได้ติดตั้งตารางของระบบชำระเงิน — กรุณารันไฟล์ "
                    + "<b>Database/PHASE19_Migration_05_Online_Payment.sql</b> ก่อน "
                    + "(ระหว่างนี้ระบบเดิมทำงานตามปกติทุกอย่าง)</div></div>"));
                return;
            }

            _cfg = dt;
            string currentCategory = null;
            PlaceHolder body = null;

            foreach (DataRow r in dt.Rows)
            {
                string key = r["Config_Key"].ToString();
                string cat = Str(r["Category"]);
                if (string.IsNullOrEmpty(cat)) cat = "อื่น ๆ";

                if (cat != currentCategory)
                {
                    currentCategory = cat;
                    phSettings.Controls.Add(new LiteralControl(
                        "<div class=\"pg-card\"><h3>" + Server.HtmlEncode(cat) + "</h3>"
                        + "<div class=\"sub\">" + Server.HtmlEncode(CategoryNote(cat)) + "</div>"));
                    body = new PlaceHolder();
                    phSettings.Controls.Add(body);
                    phSettings.Controls.Add(new LiteralControl("</div>"));
                }

                string name = Str(r["Display_Name"]);
                if (string.IsNullOrEmpty(name)) name = key;
                string desc = Str(r["Description"]);
                string type = Str(r["Input_Type"]);
                if (string.IsNullOrEmpty(type)) type = "text";
                string value = Str(r["Config_Value"]);
                bool secret = r["Is_Secret"] != DBNull.Value && Convert.ToBoolean(r["Is_Secret"]);

                body.Controls.Add(new LiteralControl(
                    "<div class=\"pg-row\"><div class=\"pg-label\"><b>" + Server.HtmlEncode(name) + "</b>"
                    + (string.IsNullOrEmpty(desc) ? "" : "<small>" + Server.HtmlEncode(desc) + "</small>")
                    + "<small style=\"color:#b6c0ba\">" + Server.HtmlEncode(key) + "</small>"
                    + "</div><div class=\"pg-input\">"));

                Control input = MakeInput(key, type, value, Str(r["Options"]), secret);
                body.Controls.Add(input);
                _inputs[key] = input;

                if (secret)
                    body.Controls.Add(new LiteralControl(
                        "<small style=\"color:#8b978f;font-size:12.3px\">เว้นว่างไว้ = ใช้ค่าเดิม</small>"));

                body.Controls.Add(new LiteralControl("</div></div>"));
            }
        }

        private Control MakeInput(string key, string type, string value, string options, bool secret)
        {
            string id = "cfg_" + key;

            if (type == "bool")
            {
                var cb = new CheckBox();
                cb.ID = id;
                cb.CssClass = "pg-chk";
                cb.Text = " เปิดใช้งาน";
                cb.Checked = value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return cb;
            }

            if (type == "select")
            {
                var ddl = new DropDownList();
                ddl.ID = id;
                foreach (string o in (options ?? "").Split(','))
                {
                    string v = o.Trim();
                    if (v.Length == 0) continue;
                    ddl.Items.Add(new ListItem(v, v));
                }
                ListItem sel = ddl.Items.FindByValue(value ?? "");
                if (sel != null) sel.Selected = true;
                return ddl;
            }

            var tb = new TextBox();
            tb.ID = id;
            if (type == "textarea") { tb.TextMode = TextBoxMode.MultiLine; tb.Rows = 6; }
            else if (secret) { tb.TextMode = TextBoxMode.Password; tb.Attributes["placeholder"] = "เว้นว่าง = ใช้ค่าเดิม"; }
            else if (type == "number") tb.Attributes["inputmode"] = "decimal";

            // ค่าลับไม่ส่งค่าจริงออกหน้าเว็บ
            tb.Text = secret ? "" : (value ?? "");
            return tb;
        }

        private static string CategoryNote(string category)
        {
            switch (category)
            {
                case "ทั่วไป":
                    return "สวิตช์หลักและวิธีชำระที่ลูกค้าเห็น — ปิด \"เปิดรับชำระเงินออนไลน์\" แล้วระบบกลับไปเหมือนเดิมทุกอย่าง";
                case "สแกน QR แบบเดิม":
                    return "ข้อมูลที่แสดงให้ลูกค้าสแกน/โอน แล้วแนบสลิป — ไม่เกี่ยวกับเกตเวย์";
                case "Omise":
                    return "คีย์จาก Omise Dashboard → Keys — ขึ้นต้น _test_ = โหมดทดสอบ ไม่ตัดเงินจริง · อย่าลืมตั้ง Webhook ตาม URL ด้านบน";
                case "วงเงินประกันความเสียหาย":
                    return "กันวงเงินบนบัตรแทนการรับโอนเงินประกัน — เงินไม่เข้าไม่ออกจนกว่าจะตัดค่าเสียหายจริง (Omise + บัตรเท่านั้น, วงเงินอยู่ได้ 7 วัน)";
                case "ช่องทางที่เปิดรับเงินออนไลน์":
                    return "ปิดช่องไหน ช่องนั้นไม่เสนอจ่ายออนไลน์ — ที่เหลือทำงานตามเดิม (มีผลเมื่อสวิตช์ใหญ่เปิดอยู่)";
                case "Payso — การเชื่อมต่อ":
                    return "กุญแจและที่อยู่ของผู้ให้บริการ (จากหน้า Merchant ของ Payso)";
                case "Payso — รูปแบบคำขอ":
                    return "ต้องตรงกับเอกสาร https://api-docs.payso.co — แก้ที่นี่ได้เลย ไม่ต้อง build ใหม่";
                case "Payso — เส้นทาง API":
                    return "เส้นทางที่ต่อท้าย Base URL — ตรวจกับเอกสารจริงก่อนเปิดใช้งานจริง";
                case "Payso — อ่านคำตอบ":
                    return "บอกระบบว่าคำตอบของเกตเวย์เก็บค่าไว้ที่ฟิลด์ไหน (ใช้ผลจากปุ่มทดสอบด้านล่างมาปรับได้)";
                case "Payso — การแจ้งกลับ":
                    return "ความปลอดภัยของข้อความที่เกตเวย์แจ้งผลกลับมา — ห้ามปิดการตรวจลายเซ็นในระบบจริง";
                default: return "";
            }
        }

        // ── บันทึก ────────────────────────────────────────────────────────────

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (_cfg == null) { Msg("err", "ยังไม่ได้ติดตั้งตารางของระบบชำระเงิน"); return; }

            int saved = 0;
            var problems = new List<string>();
            int? adminId = null;
            try { if (Session["UserID"] != null) adminId = Convert.ToInt32(Session["UserID"]); }
            catch { }

            foreach (DataRow r in _cfg.Rows)
            {
                string key = r["Config_Key"].ToString();
                bool secret = r["Is_Secret"] != DBNull.Value && Convert.ToBoolean(r["Is_Secret"]);

                Control c;
                if (!_inputs.TryGetValue(key, out c)) continue;

                string value = null;
                var cb = c as CheckBox;
                var ddl = c as DropDownList;
                var tb = c as TextBox;

                if (cb != null) value = cb.Checked ? "1" : "0";
                else if (ddl != null) value = ddl.SelectedValue;
                else if (tb != null) value = tb.Text;

                if (value == null) continue;

                // ค่าลับที่เว้นว่าง = ไม่แตะของเดิม
                if (secret && string.IsNullOrWhiteSpace(value)) continue;

                string err = Validate(key, value);
                if (err != null) { problems.Add(err); continue; }

                try { PaymentGatewayConfig.Set(key, value.Trim(), adminId); saved++; }
                catch (Exception ex) { problems.Add(key + ": " + ex.Message); }
            }

            PaymentGatewayConfig.Invalidate();

            if (problems.Count > 0)
                Msg("err", "บันทึกแล้ว " + saved + " ค่า แต่มีปัญหา:<br/>• " + string.Join("<br/>• ", problems.ToArray()));
            else
                Msg("ok", "บันทึกการตั้งค่าเรียบร้อยแล้ว (" + saved + " ค่า) — มีผลทันทีภายใน 30 วินาที");

            ShowStateWarning();
            BindTxn();
        }

        /// <summary>ตรวจค่าที่กรอกก่อนบันทึก — กันตั้งค่าที่ทำให้ระบบพังเงียบ ๆ</summary>
        private static string Validate(string key, string value)
        {
            string v = (value ?? "").Trim();

            // แม่แบบคำขอ: เป็น JSON ที่ "มีตัวแปร {{...}} คั่นอยู่" — ตัวแปรที่เป็นตัวเลข
            // (เช่น "amount": {{amount}}) ทำให้ JSON ดิบ parse ไม่ผ่านตามธรรมชาติ
            // ⚠ เดิมตรวจแบบ parse ตรง ๆ ⇒ บันทึกแม่แบบที่ถูกต้องไม่ได้เลยสักครั้ง
            // ⇒ แทนค่าจำลองก่อนแล้วค่อยตรวจ: ตัวแปรในเครื่องหมายคำพูด → "x", ตัวแปรเปล่า → 0
            if (key.EndsWith("_Template"))
            {
                if (v.Length == 0) return null;
                string probe = Regex.Replace(v, "\"\\{\\{[^}]*\\}\\}\"", "\"x\"");
                probe = Regex.Replace(probe, "\\{\\{[^}]*\\}\\}", "0");
                try { Newtonsoft.Json.Linq.JToken.Parse(probe); }
                catch (Exception ex)
                {
                    return key + " ยังไม่ใช่ JSON ที่ถูกต้อง — ตรวจวงเล็บ/จุลภาค/เครื่องหมายคำพูด "
                         + "(ตัวแปร {{...}} ใส่ได้ตามปกติ ระบบเข้าใจอยู่แล้ว) · รายละเอียด: " + ex.Message;
                }
            }
            else if (key.EndsWith("_Map"))
            {
                if (v.Length == 0) return null;
                try { Newtonsoft.Json.Linq.JToken.Parse(v); }
                catch (Exception ex)
                {
                    return key + " ต้องเป็น JSON ที่ถูกต้อง · รายละเอียด: " + ex.Message;
                }
            }

            if (key == "Payso_BaseUrl_Sandbox" || key == "Payso_BaseUrl_Production" || key == "Payment_Site_BaseUrl")
            {
                if (v.Length == 0) return null;
                if (!v.StartsWith("http://") && !v.StartsWith("https://"))
                    return key + " ต้องขึ้นต้นด้วย http:// หรือ https://";
            }

            if (key == "Payment_Methods_Enabled" && v.Length == 0)
                return "ต้องเปิดวิธีชำระอย่างน้อยหนึ่งวิธี";

            return null;
        }

        // ── ทดสอบ / โหลดใหม่ ──────────────────────────────────────────────────

        protected void btnTest_Click(object sender, EventArgs e)
        {
            PaymentGatewayConfig.Invalidate();
            try
            {
                // ทดสอบเจ้าที่ "เลือกอยู่จริง" (Payment_Provider) ไม่ใช่ Payso ตายตัว
                var gw = new OnlinePaymentService(_conn).Gateway();
                pnlTest.Visible = true;
                litTest.Text = Server.HtmlEncode(gw.TestConnection());
            }
            catch (Exception ex)
            {
                pnlTest.Visible = true;
                litTest.Text = Server.HtmlEncode("ทดสอบไม่สำเร็จ: " + ex);
            }
            ShowStateWarning();
            BindTxn();
        }

        protected void btnReload_Click(object sender, EventArgs e)
        {
            PaymentGatewayConfig.Invalidate();
            Response.Redirect(Request.RawUrl, false);
            System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
        }

        // ── รายการชำระเงิน ────────────────────────────────────────────────────

        private void BindTxn()
        {
            try
            {
                var store = new PaymentTransactionStore(_conn);
                if (!store.TablesReady()) { gvTxn.DataSource = null; gvTxn.DataBind(); return; }
                gvTxn.DataSource = store.Search(DateTime.Today.AddDays(-60), DateTime.Today, "", "", "", 100);
                gvTxn.DataBind();
            }
            catch
            {
                gvTxn.DataSource = null;
                gvTxn.DataBind();
            }
        }

        protected void gvTxn_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            int id;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out id)) return;

            if (e.CommandName == "StartRefund")
            {
                var store = new PaymentTransactionStore(_conn);
                PaymentTransaction t = store.GetById(id);
                if (t == null) { Msg("err", "ไม่พบรายการ"); return; }

                decimal already = store.GetRefundedAmount(t.ID);
                decimal refundable = t.TotalPayable - already;

                ViewState["refundId"] = t.ID;
                pnlRefund.Visible = true;
                litRefundInfo.Text = Server.HtmlEncode(t.TxnRef) + " · "
                    + Server.HtmlEncode(PaymentSource.Thai(t.SourceType) + " " + (t.SourceId ?? "")) + " · ยอด "
                    + t.TotalPayable.ToString("N2") + " บาท"
                    + (already > 0 ? " (คืนไปแล้ว " + already.ToString("N2") + ")" : "")
                    + (t.AppliedAt.HasValue
                        ? "<br/><span style=\"color:#a12626\">⚠ รายการนี้ถูกบันทึกเข้าระบบแล้ว — คืนเงินแล้วต้องปรับใบเสร็จ/ยอดการจองด้วย</span>"
                        : "");
                txtRefundAmount.Text = refundable.ToString("0.##");
                txtRefundReason.Text = "";
                BindTxn();
                return;
            }

            if (e.CommandName != "CheckStatus") return;

            try
            {
                var svc = new OnlinePaymentService(_conn);
                PaymentTransaction txn = svc.Store.GetById(id);
                string note = svc.RefreshStatus(txn);
                Msg("ok", "ผลการตรวจสอบ: " + Server.HtmlEncode(note ?? ""));
            }
            catch (Exception ex)
            {
                Msg("err", "ตรวจสถานะไม่สำเร็จ: " + Server.HtmlEncode(ex.Message));
            }
            BindTxn();
        }

        protected void btnDoRefund_Click(object sender, EventArgs e)
        {
            long id = ViewState["refundId"] == null ? 0 : Convert.ToInt64(ViewState["refundId"]);
            decimal amount;
            if (id <= 0) { Msg("err", "ไม่พบรายการที่จะคืน"); pnlRefund.Visible = false; return; }
            if (!decimal.TryParse(txtRefundAmount.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out amount) || amount <= 0)
            {
                Msg("err", "กรุณากรอกยอดคืนให้ถูกต้อง");
                pnlRefund.Visible = true;
                return;
            }

            int? adminId = null;
            try { if (Session["UserID"] != null) adminId = Convert.ToInt32(Session["UserID"]); } catch { }

            string result = new OnlinePaymentService(_conn)
                .RefundTransaction(id, amount, txtRefundReason.Text.Trim(), adminId);

            bool ok = result.StartsWith("คืนเงิน") && result.Contains("สำเร็จ");
            Msg(ok ? "ok" : "err", Server.HtmlEncode(result));
            pnlRefund.Visible = !ok;
            BindTxn();
        }

        protected void btnCancelRefund_Click(object sender, EventArgs e)
        {
            pnlRefund.Visible = false;
            ViewState["refundId"] = null;
        }

        protected bool ShowRefund(object status, object provider)
        {
            string s = Convert.ToString(status) ?? "";
            string p = Convert.ToString(provider) ?? "";
            return s == PaymentStatus.Paid && p != "MANUAL_QR" && p != "CASH";
        }

        // ── ข้อความสถานะ ──────────────────────────────────────────────────────

        private void ShowStateWarning()
        {
            try
            {
                if (!Feature.On("OnlinePayment"))
                {
                    Msg("warn", "สวิตช์ฟีเจอร์ <b>รับชำระเงินออนไลน์</b> ยังปิดอยู่ — ระบบทำงานเหมือนเดิมทุกอย่าง "
                        + "เปิดได้ที่ ศูนย์ตั้งค่า → ตั้งค่าระบบ → หมวด \"ฟีเจอร์\"", true);
                    return;
                }
                if (!PaymentGatewayConfig.GetBool("Payment_Enabled", false))
                {
                    Msg("warn", "ยังไม่ได้เปิด <b>เปิดรับชำระเงินออนไลน์</b> ในหน้านี้ — ลูกค้ายังไม่เห็นตัวเลือกใหม่", true);
                    return;
                }
                if (PaymentGatewayConfig.GetBool("Payso_Enabled", false) && !PaymentGatewayConfig.IsPaysoReady)
                {
                    Msg("warn", "เปิดใช้ Payso ไว้แต่ยังตั้งค่าไม่ครบ (Base URL / กุญแจ) — "
                        + "ตอนนี้ลูกค้าจะเห็นเฉพาะวิธีเดิม", true);
                    return;
                }
                if (PaymentGatewayConfig.IsPaysoReady && !PaymentGatewayConfig.IsSandbox
                    && !PaymentGatewayConfig.WebhookVerify)
                {
                    Msg("err", "⚠ อยู่ในโหมดใช้งานจริงแต่ <b>ปิดการตรวจลายเซ็นการแจ้งกลับ</b> อยู่ — "
                        + "ใครก็ยิงเข้ามาบอกว่า \"จ่ายแล้ว\" ได้ กรุณาเปิดกลับทันที", true);
                    return;
                }
                if (PaymentGatewayConfig.IsPaysoReady && PaymentGatewayConfig.IsSandbox)
                    Msg("warn", "กำลังใช้โหมด <b>ทดสอบ (Sandbox)</b> — จะยังไม่มีการตัดเงินจริง", true);
            }
            catch { }
        }

        /// <summary>
        /// แสดงข้อความบนหัวหน้า — ข้อความแรกของ "รอบคำขอนี้" ล้างของเดิมเสมอ
        /// (ViewState เก็บข้อความรอบก่อนไว้ ⇒ เคยเห็นคำเตือนเดียวกันซ้อนกันสองอัน)
        /// </summary>
        private bool _msgWritten;
        private void Msg(string cls, string html, bool append = false)
        {
            string block = "<div class=\"pg-alert " + cls + "\">" + html + "</div>";
            litMsg.Text = (_msgWritten && append) ? litMsg.Text + block : block;
            _msgWritten = true;
        }

        private static string Str(object o)
        {
            return o == null || o == DBNull.Value ? "" : o.ToString();
        }

        // ── ตัวช่วยของ GridView ───────────────────────────────────────────────

        protected string MethodText(object m)
        {
            return Server.HtmlEncode(PaymentGatewayConfig.MethodName(Convert.ToString(m)));
        }

        protected string SourceText(object type, object id)
        {
            return Server.HtmlEncode(PaymentSource.Thai(Convert.ToString(type)) + " " + Convert.ToString(id));
        }

        protected string AmountText(object amount, object surcharge)
        {
            decimal a = amount == null || amount == DBNull.Value ? 0m : Convert.ToDecimal(amount);
            decimal s = surcharge == null || surcharge == DBNull.Value ? 0m : Convert.ToDecimal(surcharge);
            string txt = "฿" + (a + s).ToString("N2");
            if (s > 0) txt += " <small style=\"color:#8b978f\">(+ค่าธรรมเนียม " + s.ToString("N2") + ")</small>";
            return txt;
        }

        protected string StatusPill(object status)
        {
            string s = Convert.ToString(status) ?? "";
            return "<span class=\"pill " + Server.HtmlEncode(s) + "\">"
                 + Server.HtmlEncode(PaymentStatus.Thai(s)) + "</span>";
        }

        protected bool ShowCheck(object status)
        {
            string s = Convert.ToString(status) ?? "";
            return s == PaymentStatus.Pending || s == PaymentStatus.Initiated;
        }
    }
}
