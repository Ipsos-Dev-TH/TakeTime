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
        //
        // หน้า "จัดกลุ่มเอง" แทนที่จะไล่ตามหมวดในฐานข้อมูลตรง ๆ เพื่อให้อ่านเป็นขั้นตอน:
        // เปิดระบบ → ตั้งเกตเวย์ (เห็นเฉพาะเจ้าที่เลือก) → วิธีชำระ → ช่องทาง → เสริม
        // ค่าตั้งของเกตเวย์ที่ไม่ได้เลือกถูกซ่อนด้วย JS (ยัง postback ครบ บันทึกได้ตามปกติ)

        private class UiGroup
        {
            public string Name;          // หัวการ์ด
            public string Note;          // คำอธิบายใต้หัว
            public string Provider;      // "OMISE"/"PAYSO" = แสดงเฉพาะตอนเลือกเจ้านั้น
            public bool Collapsed;       // ขั้นสูง — เริ่มแบบพับไว้
            public bool TwoCol;          // แถวสั้น ๆ เรียงสองคอลัมน์
            public string MasterKey;     // ติ๊กหลักของการ์ด — ปิดอยู่ให้ซ่อนแถวที่เหลือ
        }

        private static readonly UiGroup[] Groups = new UiGroup[]
        {
            new UiGroup { Name = "๑) เปิดระบบและเลือกผู้ให้บริการ",
                Note = "สองค่านี้กำหนดทุกอย่าง — เลือกเกตเวย์เจ้าไหน ด้านล่างจะแสดงเฉพาะการตั้งค่าของเจ้านั้น "
                     + "ปิดสวิตช์เมื่อไหร่ ระบบกลับไปทำงานเหมือนเดิมทุกอย่างทันที" },
            new UiGroup { Name = "๒) ตั้งค่า Omise", Provider = "OMISE",
                Note = "คีย์จาก Omise Dashboard → Keys — ขึ้นต้น _test_ = โหมดทดสอบ ไม่ตัดเงินจริง · "
                     + "อย่าลืมตั้ง Webhook ตาม URL ในการ์ดล่างสุด แล้วกด \"ทดสอบการเชื่อมต่อ\"" },
            new UiGroup { Name = "๒) Payso — การเชื่อมต่อ", Provider = "PAYSO",
                Note = "กุญแจและที่อยู่ของผู้ให้บริการ (จากหน้า Merchant ของ Payso)" },
            new UiGroup { Name = "Payso — รูปแบบคำขอ", Provider = "PAYSO", Collapsed = true,
                Note = "ขั้นสูง — ต้องตรงกับเอกสาร https://api-docs.payso.co แก้ที่นี่ได้เลย ไม่ต้อง build ใหม่" },
            new UiGroup { Name = "Payso — เส้นทาง API", Provider = "PAYSO", Collapsed = true,
                Note = "ขั้นสูง — เส้นทางที่ต่อท้าย Base URL ตรวจกับเอกสารจริงก่อนเปิดใช้งานจริง" },
            new UiGroup { Name = "Payso — อ่านคำตอบ", Provider = "PAYSO", Collapsed = true,
                Note = "ขั้นสูง — บอกระบบว่าคำตอบของเกตเวย์เก็บค่าไว้ที่ฟิลด์ไหน (ใช้ผลจากปุ่มทดสอบมาปรับได้)" },
            new UiGroup { Name = "Payso — การแจ้งกลับ", Provider = "PAYSO", Collapsed = true,
                Note = "ความปลอดภัยของข้อความที่เกตเวย์แจ้งผลกลับมา — ห้ามปิดการตรวจลายเซ็นในระบบจริง" },
            new UiGroup { Name = "๓) วิธีชำระที่ลูกค้าเห็น",
                Note = "ติ๊กเปิดวิธีที่ให้ลูกค้าเลือกได้ พร้อมกติกายอดเงินและค่าธรรมเนียม — "
                     + "วิธีที่ต้องผ่านเกตเวย์จะโผล่ให้ลูกค้าเห็นก็ต่อเมื่อเกตเวย์ในข้อ ๒ พร้อมแล้วเท่านั้น" },
            new UiGroup { Name = "สแกน QR แบบเดิม (โอนแล้วแนบสลิป)",
                Note = "ข้อมูลที่แสดงให้ลูกค้าสแกน/โอนเอง แล้วพนักงานตรวจสลิป — ไม่เกี่ยวกับเกตเวย์ ใช้ได้แม้ปิดเกตเวย์" },
            new UiGroup { Name = "๔) จุดที่เปิดรับจ่ายออนไลน์", TwoCol = true,
                Note = "ปิดจุดไหน จุดนั้นไม่เสนอทางจ่ายออนไลน์ — ที่เหลือทำงานตามเดิม (มีผลเมื่อสวิตช์ใหญ่เปิดอยู่)" },
            new UiGroup { Name = "วงเงินประกันความเสียหาย", MasterKey = "Payment_SecurityHold_Enabled",
                Note = "กันวงเงินบนบัตรแทนการรับโอนเงินประกัน — เงินไม่เข้าไม่ออกจนกว่าจะตัดค่าเสียหายจริง "
                     + "(Omise + บัตรเท่านั้น, วงเงินอยู่ได้ 7 วัน)" },
            new UiGroup { Name = "การบันทึกบัญชีและแจ้งเตือน",
                Note = "พฤติกรรมหลังลูกค้าจ่ายสำเร็จ — การลงระบบอัตโนมัติ แหล่งเงินที่ผูกกับ NextAcc และการแจ้งพนักงาน" },
            new UiGroup { Name = "อื่น ๆ", Note = "" },
        };

        /// <summary>คีย์ไหนอยู่การ์ดไหน — คีย์ที่ไม่เข้าเงื่อนไขใช้หมวดจากฐานข้อมูล</summary>
        private static string GroupOf(string key, string dbCategory)
        {
            switch (key)
            {
                case "Payment_Enabled":
                case "Payment_Provider":
                    return "๑) เปิดระบบและเลือกผู้ให้บริการ";
                case "Payment_Methods_Enabled":
                case "Payment_Default_Method":
                case "Payment_Card_Surcharge_Pct":
                case "Payment_Min_Amount":
                case "Payment_Max_Amount":
                case "Payment_Expiry_Minutes":
                    return "๓) วิธีชำระที่ลูกค้าเห็น";
                case "Payment_Auto_Apply":
                case "Payment_PaidHow_Name":
                case "Payment_Notify_Staff":
                case "Payment_Site_BaseUrl":
                    return "การบันทึกบัญชีและแจ้งเตือน";
            }
            switch (dbCategory)
            {
                case "Omise": return "๒) ตั้งค่า Omise";
                case "Payso — การเชื่อมต่อ": return "๒) Payso — การเชื่อมต่อ";
                case "สแกน QR แบบเดิม": return "สแกน QR แบบเดิม (โอนแล้วแนบสลิป)";
                case "ช่องทางที่เปิดรับเงินออนไลน์": return "๔) จุดที่เปิดรับจ่ายออนไลน์";
                case "Payso — รูปแบบคำขอ":
                case "Payso — เส้นทาง API":
                case "Payso — อ่านคำตอบ":
                case "Payso — การแจ้งกลับ":
                case "วงเงินประกันความเสียหาย":
                    return dbCategory;
            }
            return string.IsNullOrEmpty(dbCategory) ? "อื่น ๆ" : dbCategory;
        }

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

            // จัดแถวเข้าการ์ดตามผังของหน้า (ในการ์ดคงลำดับ Display_Order เดิม)
            var byGroup = new Dictionary<string, List<DataRow>>();
            foreach (DataRow r in dt.Rows)
            {
                string g = GroupOf(r["Config_Key"].ToString(), Str(r["Category"]));
                List<DataRow> list;
                if (!byGroup.TryGetValue(g, out list)) byGroup[g] = list = new List<DataRow>();
                list.Add(r);
            }

            phSettings.Controls.Add(new LiteralControl(BuildStepsHtml(dt)));

            var known = new List<UiGroup>(Groups);
            foreach (string g in byGroup.Keys)          // หมวดแปลกใหม่จาก DB ที่ผังนี้ยังไม่รู้จัก
            {
                bool found = false;
                foreach (UiGroup u in known) if (u.Name == g) { found = true; break; }
                if (!found) known.Add(new UiGroup { Name = g, Note = "" });
            }

            int anchor = 0;
            foreach (UiGroup grp in known)
            {
                List<DataRow> rows;
                if (!byGroup.TryGetValue(grp.Name, out rows) || rows.Count == 0) continue;

                anchor++;
                string attrs = " id=\"pgcat" + anchor + "\""
                    + (grp.Provider != null ? " data-pg-provider=\"" + grp.Provider + "\"" : "")
                    + (grp.MasterKey != null ? " data-pg-master=\"cfg_" + grp.MasterKey + "\"" : "")
                    + (grp.Collapsed ? " data-pg-adv=\"1\"" : "");

                phSettings.Controls.Add(new LiteralControl(
                    "<div class=\"pg-card" + (grp.Collapsed ? " pg-collapsed" : "") + "\"" + attrs + ">"
                    + "<h3 class=\"" + (grp.Collapsed ? "pg-toggle" : "") + "\">" + Server.HtmlEncode(grp.Name)
                    + (grp.Collapsed ? " <span class=\"pg-caret\">▾</span>" : "") + "</h3>"
                    + "<div class=\"sub\">" + Server.HtmlEncode(grp.Note) + "</div>"
                    + "<div class=\"pg-body" + (grp.TwoCol ? " pg-2col" : "") + "\">"));

                foreach (DataRow r in rows)
                {
                    string key = r["Config_Key"].ToString();
                    string name = Str(r["Display_Name"]);
                    if (string.IsNullOrEmpty(name)) name = key;
                    string desc = Str(r["Description"]);
                    string type = Str(r["Input_Type"]);
                    if (string.IsNullOrEmpty(type)) type = "text";
                    string value = Str(r["Config_Value"]);
                    bool secret = r["Is_Secret"] != DBNull.Value && Convert.ToBoolean(r["Is_Secret"]);

                    phSettings.Controls.Add(new LiteralControl(
                        "<div class=\"pg-row\" data-pg-key=\"" + Server.HtmlEncode(key) + "\">"
                        + "<div class=\"pg-label\"><b>" + Server.HtmlEncode(name) + "</b>"
                        + (string.IsNullOrEmpty(desc) ? "" : "<small>" + Server.HtmlEncode(desc) + "</small>")
                        + "<small style=\"color:#b6c0ba\">" + Server.HtmlEncode(key) + "</small>"
                        + "</div><div class=\"pg-input\">"));

                    Control input = key == "Payment_Methods_Enabled"
                        ? MakeMethodChecks(value)
                        : MakeInput(key, type, value, Str(r["Options"]), secret);
                    phSettings.Controls.Add(input);
                    _inputs[key] = input;

                    if (secret)
                        phSettings.Controls.Add(new LiteralControl(
                            "<small style=\"color:#8b978f;font-size:12.3px\">เว้นว่างไว้ = ใช้ค่าเดิม</small>"));

                    phSettings.Controls.Add(new LiteralControl("</div></div>"));
                }

                phSettings.Controls.Add(new LiteralControl("</div></div>"));
            }
        }

        /// <summary>
        /// แถบขั้นตอน ๔ ขั้นบนหัวหน้า — คำนวณจากค่าที่บันทึกอยู่จริง ให้เห็นทันทีว่าค้างขั้นไหน
        /// </summary>
        private string BuildStepsHtml(DataTable dt)
        {
            var val = new Dictionary<string, string>();
            foreach (DataRow r in dt.Rows) val[r["Config_Key"].ToString()] = Str(r["Config_Value"]);
            Func<string, string> v = k => { string s; return val.TryGetValue(k, out s) ? s : ""; };
            Func<string, bool> on = k => v(k) == "1" || v(k).Equals("true", StringComparison.OrdinalIgnoreCase);

            bool featureOn = false;
            try { featureOn = Feature.On("OnlinePayment"); } catch { }
            bool s1 = featureOn && on("Payment_Enabled");

            bool omise = !string.Equals(v("Payment_Provider"), "PAYSO", StringComparison.OrdinalIgnoreCase);
            bool s2;
            if (omise)
                s2 = on("Omise_Enabled") && v("Omise_SecretKey").Length > 0;
            else
                s2 = on("Payso_Enabled")
                     && (v("Payso_BaseUrl_Sandbox").Length > 0 || v("Payso_BaseUrl_Production").Length > 0)
                     && (v("Payso_ApiKey").Length > 0 || v("Payso_SecretKey").Length > 0);

            string methods = (v("Payment_Methods_Enabled") ?? "").ToUpperInvariant();
            bool s3 = methods.Contains("CARD") || methods.Contains("QR");

            bool s4 = false;
            foreach (string k in val.Keys)
                if (k.StartsWith("Payment_Channel_") && on(k)) { s4 = true; break; }

            string[] labels =
            {
                s1 ? "เปิดระบบแล้ว" : "เปิดสวิตช์ (ฟีเจอร์ + หน้านี้)",
                s2 ? "เกตเวย์ " + (omise ? "Omise" : "Payso") + " พร้อม" : "ใส่กุญแจ" + (omise ? " Omise" : " Payso"),
                s3 ? "เปิดวิธีชำระแล้ว" : "เลือกวิธีชำระให้ลูกค้า",
                s4 ? "เปิดจุดรับเงินแล้ว" : "เปิดจุดรับเงิน",
            };
            bool[] done = { s1, s2, s3, s4 };
            bool all = s1 && s2 && s3 && s4;

            var sb = new System.Text.StringBuilder();
            sb.Append("<div class=\"pg-card pg-steps-card\"><div class=\"pg-steps\">");
            for (int i = 0; i < 4; i++)
                sb.Append("<div class=\"pg-step " + (done[i] ? "done" : "todo") + "\">"
                        + "<span class=\"n\">" + (done[i] ? "✓" : (i + 1).ToString()) + "</span>"
                        + "<span>" + Server.HtmlEncode(labels[i]) + "</span></div>");
            sb.Append("</div><div class=\"pg-steps-sum " + (all ? "ok" : "") + "\">"
                    + (all ? "✅ ครบทุกขั้น — ลองของจริงที่หน้า \"ทดสอบเกตเวย์\" ก่อนเปิดให้ลูกค้า"
                           : "ไล่ตั้งค่าตามหมายเลขการ์ดด้านล่างจนแถบนี้ครบทุกขั้น แล้วกด \"ทดสอบการเชื่อมต่อ\"")
                    + "</div></div>");
            return sb.ToString();
        }

        /// <summary>
        /// วิธีชำระ = ติ๊กเลือกเป็นรายวิธี (เดิมเป็นช่องพิมพ์ "CARD,QR,..." — พังง่ายพิมพ์ผิดไม่รู้ตัว)
        /// ค่าเก็บลงคีย์เดิมรูปแบบเดิมทุกประการ ระบบส่วนอื่นไม่ต้องเปลี่ยน
        /// </summary>
        private Panel MakeMethodChecks(string value)
        {
            string cur = "," + (value ?? "").ToUpperInvariant().Replace(" ", "") + ",";
            var pnl = new Panel { ID = "cfg_Payment_Methods_Enabled", CssClass = "pg-methods" };

            string[,] defs =
            {
                { PaymentGatewayConfig.MethodManualQr, "สแกน QR โอนแล้วแนบสลิป (แบบเดิม)", "ไม่ต้องใช้เกตเวย์ พนักงานตรวจสลิปเอง" },
                { PaymentGatewayConfig.MethodCard,     "บัตรเครดิต / เดบิต",               "ผ่านเกตเวย์ ตัดยอดอัตโนมัติ — ต้องมี Public Key (Omise)" },
                { PaymentGatewayConfig.MethodQr,       "PromptPay ผ่านเกตเวย์",            "ลูกค้าสแกนจ่าย ระบบรู้ผลเอง ไม่ต้องตรวจสลิป" },
                { PaymentGatewayConfig.MethodInstallment, "ผ่อนชำระ",                      "ยังไม่เปิดใช้ในระบบ — เว้นไว้ก่อน" },
            };

            for (int i = 0; i < defs.GetLength(0); i++)
            {
                string m = defs[i, 0];
                var cb = new CheckBox
                {
                    ID = "cfg_Method_" + m,
                    CssClass = "pg-chk",
                    Text = " " + defs[i, 1],
                    Checked = cur.Contains("," + m + ","),
                };
                if (m == PaymentGatewayConfig.MethodInstallment && !cb.Checked) cb.Enabled = false;
                pnl.Controls.Add(cb);
                pnl.Controls.Add(new LiteralControl(
                    "<small class=\"pg-mnote\">" + Server.HtmlEncode(defs[i, 2]) + "</small>"));
            }
            return pnl;
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
                var pnl = c as Panel;

                if (cb != null) value = cb.Checked ? "1" : "0";
                else if (ddl != null) value = ddl.SelectedValue;
                else if (tb != null) value = tb.Text;
                else if (pnl != null && key == "Payment_Methods_Enabled")
                {
                    // รวมวิธีที่ติ๊กกลับเป็น "CARD,QR,..." รูปแบบเดิมของคีย์นี้
                    var picked = new List<string>();
                    foreach (Control ch in pnl.Controls)
                    {
                        var mc = ch as CheckBox;
                        if (mc != null && mc.Checked && mc.ID != null && mc.ID.StartsWith("cfg_Method_"))
                            picked.Add(mc.ID.Substring("cfg_Method_".Length));
                    }
                    value = string.Join(",", picked.ToArray());
                }

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
                // ⚠ คำเตือนต้องดูที่ "เกตเวย์ที่เลือกใช้อยู่" ไม่ใช่ Payso ตายตัว
                //   (เดิมเช็ค IsPaysoReady ล้วน ⇒ ตั้ง Omise ไว้จะไม่เตือนอะไรเลย)
                bool omise = PaymentGatewayConfig.ActiveProvider == PaymentGatewayConfig.ProviderOmise;

                if (!PaymentGatewayConfig.IsGatewayReady)
                {
                    Msg("warn", omise
                        ? "เลือกใช้ <b>Omise</b> แต่ยังตั้งค่าไม่ครบ — ต้องเปิด \"เปิดใช้เกตเวย์ Omise\" "
                          + "และใส่ Secret Key (skey_…) ตอนนี้ลูกค้าจะเห็นเฉพาะวิธีเดิม"
                        : "เลือกใช้ <b>Payso</b> แต่ยังตั้งค่าไม่ครบ (Base URL / กุญแจ) — "
                          + "ตอนนี้ลูกค้าจะเห็นเฉพาะวิธีเดิม", true);
                    return;
                }

                if (omise)
                {
                    // บัตรเครดิตต้องมี Public Key ด้วย — Omise.js บนหน้าเว็บใช้ตัวนี้แลก token
                    if (PaymentGatewayConfig.AvailableMethods(0m).Contains(PaymentGatewayConfig.MethodCard)
                        && string.IsNullOrEmpty(PaymentGatewayConfig.Get("Omise_PublicKey", "")))
                    {
                        Msg("err", "เปิดรับ <b>บัตรเครดิต</b> ไว้แต่ยังไม่ได้ใส่ <b>Public Key</b> (pkey_…) — "
                            + "หน้ากรอกบัตรจะขึ้นไม่ได้ เพราะไม่มีกุญแจส่งข้อมูลบัตรเข้า vault ของ Omise", true);
                        return;
                    }
                    if (OmiseGateway.IsTestKey)
                        Msg("warn", "กำลังใช้กุญแจ <b>ทดสอบ (skey_test_…)</b> — จะยังไม่มีการตัดเงินจริง "
                            + "เปลี่ยนเป็นกุญแจ skey_live_… เมื่อพร้อมใช้งานจริง", true);
                    return;
                }

                if (!PaymentGatewayConfig.IsSandbox && !PaymentGatewayConfig.WebhookVerify)
                {
                    Msg("err", "⚠ อยู่ในโหมดใช้งานจริงแต่ <b>ปิดการตรวจลายเซ็นการแจ้งกลับ</b> อยู่ — "
                        + "ใครก็ยิงเข้ามาบอกว่า \"จ่ายแล้ว\" ได้ กรุณาเปิดกลับทันที", true);
                    return;
                }
                if (PaymentGatewayConfig.IsSandbox)
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
