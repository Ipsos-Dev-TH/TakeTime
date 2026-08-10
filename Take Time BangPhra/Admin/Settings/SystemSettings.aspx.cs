using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// ศูนย์รวมการตั้งค่าระบบ — จัดการค่าที่เดิมต้องแก้ใน Web.config (Token/API Key/SMTP/Path)
    /// อ่าน-เขียนผ่าน AppCfg (DB ก่อน → Web.config เป็น fallback) จึงไม่กระทบระบบเดิม
    /// </summary>
    public partial class SystemSettings : Page
    {
        private static readonly Dictionary<string, string> CategoryTitle = new Dictionary<string, string>
        {
            { "LINE",     "LINE Official Account" },
            { "TELEGRAM", "Telegram" },
            { "EMAIL",    "อีเมล (SMTP)" },
            { "API",      "API ภายนอก" },
            { "PATH",     "ที่เก็บไฟล์บนเซิร์ฟเวอร์" },
            { "GENERAL",  "ทั่วไป" }
        };

        private static readonly Dictionary<string, string> CategoryIcon = new Dictionary<string, string>
        {
            { "LINE", "fa-comment-dots" }, { "TELEGRAM", "fa-paper-plane" },
            { "EMAIL", "fa-envelope" }, { "API", "fa-key" },
            { "PATH", "fa-folder-open" }, { "GENERAL", "fa-gear" }
        };

        private static readonly Dictionary<string, string> CategoryNote = new Dictionary<string, string>
        {
            { "LINE",     "Token ที่ใช้ส่งข้อความทั้งระบบ (แจ้งเตือน, รายงานรายวัน, ใบลา)" },
            { "TELEGRAM", "แจ้งเตือนภายในทีม — Chat ID เดิมฝังอยู่ในโค้ด ตอนนี้ตั้งที่นี่ได้แล้ว" },
            { "EMAIL",    "ใช้ส่งใบเสร็จ / e-Tax / แจ้งลูกค้า (Gmail ต้องใช้ App Password)" },
            { "API",      "คีย์ของบริการภายนอก" },
            { "PATH",     "path จริงบนเซิร์ฟเวอร์ — ระวังตั้งผิดแล้วบันทึกไฟล์ไม่ได้" }
        };

        private short? AdminId =>
            Session["UserID"] != null && short.TryParse(Session["UserID"].ToString(), out var v) ? v : (short?)null;

        protected void Page_Load(object sender, EventArgs e)
        {
            // ค่าเหล่านี้กระทบทั้งระบบ → เฉพาะ Owner
            if (Session["permission"]?.ToString() != "True" || Session["User"]?.ToString() != "Owner")
            {
                Response.Redirect("~/Default");
                return;
            }
            if (!IsPostBack) Render();
        }

        // ── วาดฟอร์มจาก metadata ในตาราง ──────────────────────────────────────────
        private void Render()
        {
            var sb = new StringBuilder();
            DataTable dt;
            try { dt = AppCfg.GetAllForUi(); }
            catch (Exception ex)
            {
                litGroups.Text = $"<div class='ss-card'>ยังไม่ได้สร้างตาราง System_Config " +
                                 $"— กรุณารัน migration PHASE18_18 ก่อน<br/><small>{Server.HtmlEncode(ex.Message)}</small></div>";
                return;
            }

            string currentCat = null;
            foreach (DataRow r in dt.Rows)
            {
                string cat = r["Category"]?.ToString() ?? "GENERAL";
                if (cat != currentCat)
                {
                    if (currentCat != null) sb.Append("</div>");
                    currentCat = cat;
                    string title = CategoryTitle.ContainsKey(cat) ? CategoryTitle[cat] : cat;
                    string icon = CategoryIcon.ContainsKey(cat) ? CategoryIcon[cat] : "fa-gear";
                    string note = CategoryNote.ContainsKey(cat) ? CategoryNote[cat] : "";
                    sb.Append($"<div class='ss-card'><h3><i class='fas {icon}'></i> {Server.HtmlEncode(title)}</h3>");
                    if (!string.IsNullOrEmpty(note)) sb.Append($"<div class='sub'>{Server.HtmlEncode(note)}</div>");
                }

                string key = r["ConfigKey"].ToString();
                bool secret = ToBool(r["IsSecret"]);
                bool inDb = r["ConfigValue"] != DBNull.Value && !string.IsNullOrEmpty(r["ConfigValue"].ToString());
                string label = r["DisplayName"] != DBNull.Value && !string.IsNullOrWhiteSpace(r["DisplayName"].ToString())
                    ? r["DisplayName"].ToString() : key;
                string desc = r["Description"]?.ToString() ?? "";
                string type = r["InputType"]?.ToString() ?? "text";

                // ค่าที่ระบบใช้อยู่จริงตอนนี้ (ลับ = ไม่แสดง)
                string current = secret ? "" : (AppCfg.Get(key) ?? "");
                string webVal = System.Configuration.ConfigurationManager.AppSettings[key];

                string badge = inDb
                    ? "<span class='src src-db'>ตั้งใน DB</span>"
                    : (!string.IsNullOrEmpty(webVal)
                        ? "<span class='src src-web'>Web.config</span>"
                        : "<span class='src src-none'>ยังไม่ตั้ง</span>");

                string input;
                if (type == "bool")
                {
                    string cur = (AppCfg.Get(key) ?? "").ToLowerInvariant();
                    input = $"<select name='cfg_{key}'>" +
                            $"<option value=''{(inDb ? "" : " selected")}>— ใช้ค่าเดิม —</option>" +
                            $"<option value='true'{(cur == "true" && inDb ? " selected" : "")}>true</option>" +
                            $"<option value='false'{(cur == "false" && inDb ? " selected" : "")}>false</option>" +
                            "</select>";
                }
                else if (secret)
                {
                    string ph = inDb ? "•••••••• (มีค่าแล้ว — เว้นว่าง = คงเดิม, \"-\" = ล้าง)" : "ยังไม่ได้ตั้ง";
                    input = $"<input type='password' name='cfg_{key}' autocomplete='new-password' placeholder='{ph}' />";
                }
                else
                {
                    string t = type == "number" ? "number" : "text";
                    input = $"<input type='{t}' name='cfg_{key}' value='{Server.HtmlEncode(current)}' " +
                            $"placeholder='{Server.HtmlEncode(webVal ?? "")}' />";
                }

                sb.Append("<div class='row'>");
                sb.Append($"<div class='lbl'>{Server.HtmlEncode(label)}<small>{Server.HtmlEncode(desc)}</small>" +
                          $"<small><code>{Server.HtmlEncode(key)}</code></small></div>");
                sb.Append($"<div>{input}</div>");
                sb.Append($"<div>{badge}</div>");
                sb.Append("</div>");
            }
            if (currentCat != null) sb.Append("</div>");
            litGroups.Text = sb.ToString();
        }

        // ── บันทึก ────────────────────────────────────────────────────────────────
        protected void btnSave_Click(object sender, EventArgs e)
        {
            int saved = 0, cleared = 0;
            try
            {
                DataTable dt = AppCfg.GetAllForUi();
                foreach (DataRow r in dt.Rows)
                {
                    string key = r["ConfigKey"].ToString();
                    string posted = Request.Form["cfg_" + key];
                    if (posted == null) continue;
                    posted = posted.Trim();

                    bool secret = ToBool(r["IsSecret"]);
                    bool inDb = r["ConfigValue"] != DBNull.Value && !string.IsNullOrEmpty(r["ConfigValue"].ToString());

                    if (posted == "-")                       // ล้างค่าใน DB → กลับไปใช้ Web.config
                    {
                        if (inDb) { AppCfg.Set(key, null, AdminId); cleared++; }
                        continue;
                    }

                    if (secret)
                    {
                        // ค่าลับ: เว้นว่าง = คงเดิม (ไม่ทับด้วยค่าว่าง)
                        if (string.IsNullOrEmpty(posted)) continue;
                        AppCfg.Set(key, posted, AdminId); saved++;
                    }
                    else
                    {
                        string currentDb = inDb ? r["ConfigValue"].ToString() : "";
                        if (posted == currentDb) continue;   // ไม่เปลี่ยน
                        if (string.IsNullOrEmpty(posted))
                        {
                            if (inDb) { AppCfg.Set(key, null, AdminId); cleared++; }
                        }
                        else { AppCfg.Set(key, posted, AdminId); saved++; }
                    }
                }

                AppCfg.Invalidate();
                new code().Logs(
                    System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString,
                    "SystemSettings", $"บันทึกการตั้งค่า: อัปเดต {saved} ค่า, ล้าง {cleared} ค่า",
                    Session["UserName"]?.ToString() ?? "SYSTEM");

                Msg($"บันทึกแล้ว — อัปเดต {saved} ค่า" + (cleared > 0 ? $", ล้างกลับไปใช้ Web.config {cleared} ค่า" : ""), true);
            }
            catch (Exception ex) { Msg("บันทึกไม่สำเร็จ: " + ex.Message, false); }

            Render();
        }

        // ── ทดสอบ ─────────────────────────────────────────────────────────────────
        protected void btnTestTelegram_Click(object sender, EventArgs e)
        {
            try
            {
                string token = AppCfg.Get("TelegramTokenTakeTime");
                string chatId = AppCfg.Get("TelegramChatId", "-4969611371");
                if (string.IsNullOrEmpty(token)) { Res("ยังไม่ได้ตั้ง Telegram Bot Token", false); return; }

                var bot = new TelegramBot2(token);
                bot.SendMessageAsync(chatId,
                    $"🔔 ทดสอบจากศูนย์รวมการตั้งค่า TakeTime\n{DateTime.Now:dd/MM/yyyy HH:mm} น.")
                   .GetAwaiter().GetResult();
                Res($"ส่ง Telegram สำเร็จ (chat {chatId})", true);
            }
            catch (Exception ex) { Res("ส่งไม่สำเร็จ: " + ex.Message, false); }
            Render();
        }

        protected void btnTestLine_Click(object sender, EventArgs e)
        {
            try
            {
                string token = AppCfg.Get("linechannelaccesstokentaketime");
                if (string.IsNullOrEmpty(token)) { Res("ยังไม่ได้ตั้ง LINE Channel Access Token", false); return; }

                var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("https://api.line.me/v2/bot/info");
                req.Method = "GET";
                req.Timeout = 15000;
                req.Headers.Add("Authorization", "Bearer " + token);
                using (var resp = (System.Net.HttpWebResponse)req.GetResponse())
                using (var rd = new System.IO.StreamReader(resp.GetResponseStream()))
                {
                    string body = rd.ReadToEnd();
                    Res("Token ใช้งานได้ — " + (body.Length > 200 ? body.Substring(0, 200) : body), true);
                }
            }
            catch (System.Net.WebException wex)
            {
                Res("Token ใช้ไม่ได้: " + wex.Message + " (401 = token ผิด/หมดอายุ)", false);
            }
            catch (Exception ex) { Res("ตรวจไม่สำเร็จ: " + ex.Message, false); }
            Render();
        }

        protected void btnTestEmail_Click(object sender, EventArgs e)
        {
            string to = txtTestEmailTo.Text.Trim();
            if (string.IsNullOrEmpty(to)) { Res("กรุณากรอกอีเมลปลายทางก่อน", false); Render(); return; }
            try
            {
                string host = AppCfg.Get("SMTP");
                int port = AppCfg.GetInt("SMTP_Port", 587);
                string from = AppCfg.Get("Email_From");
                string pwd = AppCfg.Get("Email_Password_From");
                bool ssl = AppCfg.GetBool("SMTP_EnableSsl", true);
                if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(from))
                { Res("ยังตั้งค่า SMTP/อีเมลผู้ส่งไม่ครบ", false); Render(); return; }

                using (var msg = new System.Net.Mail.MailMessage(from, to))
                using (var client = new System.Net.Mail.SmtpClient(host, port))
                {
                    msg.Subject = "ทดสอบการส่งอีเมล — TakeTime";
                    msg.Body = $"ส่งจากศูนย์รวมการตั้งค่าระบบ เมื่อ {DateTime.Now:dd/MM/yyyy HH:mm} น.";
                    client.EnableSsl = ssl;
                    client.Credentials = new System.Net.NetworkCredential(from, pwd);
                    client.Timeout = 20000;
                    client.Send(msg);
                }
                Res($"ส่งอีเมลทดสอบไปที่ {to} แล้ว", true);
            }
            catch (Exception ex) { Res("ส่งไม่สำเร็จ: " + (ex.InnerException ?? ex).Message, false); }
            Render();
        }

        // ── helpers ───────────────────────────────────────────────────────────────
        private void Msg(string text, bool ok)
        {
            pnlMsg.Visible = true;
            string color = ok ? "#1e7e42" : "#c0392b";
            litMsg.Text = $"<div style='color:{color};font-weight:600;'>" +
                          $"<i class='fas {(ok ? "fa-circle-check" : "fa-circle-exclamation")}'></i> " +
                          $"{Server.HtmlEncode(text)}</div>";
        }

        private void Res(string text, bool ok)
        {
            divRes.Attributes["class"] = "res " + (ok ? "ok" : "err");
            litRes.Text = $"<i class='fas {(ok ? "fa-circle-check" : "fa-circle-exclamation")}'></i> {Server.HtmlEncode(text)}";
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
