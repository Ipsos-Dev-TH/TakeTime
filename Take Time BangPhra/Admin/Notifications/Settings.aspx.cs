using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.Notifications
{
    /// <summary>
    /// ตั้งค่าการแจ้งเตือน — เลือกได้ทีละเหตุการณ์ × ทีละช่องทาง (Telegram / LINE)
    ///
    /// ⚠ หน้านี้เดิมเป็น "เปลือก": toggle ทุกตัวเป็น onclick="this.classList.toggle('active')"
    /// ล้วน ๆ ไม่มีการบันทึก ไม่มีใครอ่านค่า ปุ่มทดสอบไม่ทำอะไร และป้าย "เชื่อมต่อแล้ว"
    /// เขียนตายไว้ในหน้า — ผู้ดูแลกดปิดแล้วเข้าใจว่าปิดได้จริงทั้งที่ยังส่งอยู่
    ///
    /// ตอนนี้อ่าน/เขียนตาราง Notification_Rules จริง และทุกจุดที่ส่งแจ้งเตือนในระบบ
    /// ผ่านคลาสกลาง <see cref="global::Notify"/> ซึ่งเช็คกฎเหล่านี้ก่อนส่งเสมอ
    ///
    /// ตัวควบคุมถูกสร้างตอน Page_Init เพื่อให้ค่าที่กดส่งกลับมาได้ตามปกติของ WebForms
    /// </summary>
    public partial class Settings : Page
    {
        private readonly string _conn =
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        // ตัวควบคุมรายเหตุการณ์: key = "CODE|CHANNEL"
        private readonly Dictionary<string, CheckBox> _checks = new Dictionary<string, CheckBox>();
        private readonly Dictionary<string, TextBox> _targets = new Dictionary<string, TextBox>();
        private bool _tableMissing;

        protected void Page_Init(object sender, EventArgs e)
        {
            BuildEventsUi();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SysSettings)) return;
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            if (!IsPostBack)
            {
                LoadGlobals();
                LoadRuleValues();
                BindLog();
            }

            ShowTokenStatus();
            if (_tableMissing)
                Msg("warn", "ยังไม่ได้ติดตั้งตารางการแจ้งเตือน — ให้ผู้ดูแลระบบรันไฟล์ "
                    + "<b>Database/PHASE19_Migration_07_Notification_Rules.sql</b> ก่อน "
                    + "(ระหว่างนี้ระบบยังแจ้งเตือนตามเดิมทุกอย่าง)");
        }

        // ── วาดรายการเหตุการณ์ ────────────────────────────────────────────────

        private void BuildEventsUi()
        {
            string currentCat = null;
            PlaceHolder body = null;

            foreach (var ev in global::Notify.Catalog)
            {
                if (ev.Category != currentCat)
                {
                    currentCat = ev.Category;
                    phEvents.Controls.Add(new LiteralControl(
                        "<div class=\"nt-grp\"><div class=\"cat\">" + Server.HtmlEncode(currentCat) + "</div>"));
                    body = new PlaceHolder();
                    phEvents.Controls.Add(body);
                    phEvents.Controls.Add(new LiteralControl("</div>"));
                }

                body.Controls.Add(new LiteralControl(
                    "<div class=\"ev\"><div class=\"ev-name\"><b>" + Server.HtmlEncode(ev.Name) + "</b>"
                    + (ev.Urgent ? "<span class=\"urgent\">ด่วน</span>" : "")
                    + "<small>" + Server.HtmlEncode(ev.Note) + "</small></div>"));

                AddChannelCell(body, ev.Code, global::Notify.ChannelTelegram, "Telegram", "chat id เฉพาะเรื่องนี้");
                AddChannelCell(body, ev.Code, global::Notify.ChannelLine, "LINE", "userId / groupId เฉพาะเรื่องนี้");

                body.Controls.Add(new LiteralControl("</div>"));
            }
        }

        private void AddChannelCell(PlaceHolder body, string code, string channel, string label, string placeholder)
        {
            body.Controls.Add(new LiteralControl("<div class=\"ev-ch\"><div class=\"top\">"));

            var cb = new CheckBox { ID = "chk_" + channel + "_" + code };
            body.Controls.Add(cb);
            _checks[Key(code, channel)] = cb;

            body.Controls.Add(new LiteralControl("<span>" + label + "</span></div>"));

            var tb = new TextBox { ID = "tgt_" + channel + "_" + code };
            tb.Attributes["placeholder"] = placeholder;
            body.Controls.Add(tb);
            _targets[Key(code, channel)] = tb;

            body.Controls.Add(new LiteralControl("</div>"));
        }

        private static string Key(string code, string channel) { return code + "|" + channel; }

        // ── อ่านค่า ───────────────────────────────────────────────────────────

        private void LoadGlobals()
        {
            chkTelegram.Checked = AppCfg.GetBool("Notify_Telegram_Enabled", true);
            chkLine.Checked = AppCfg.GetBool("Notify_Line_Enabled", false);
            txtTgTarget.Text = AppCfg.Get("TelegramChatId", "") ?? "";
            txtLineTarget.Text = AppCfg.Get("Notify_Line_Target", "") ?? "";
            txtQuietFrom.Text = AppCfg.Get("Notify_QuietHours_From", "") ?? "";
            txtQuietTo.Text = AppCfg.Get("Notify_QuietHours_To", "") ?? "";
            chkQuietUrgent.Checked = AppCfg.GetBool("Notify_QuietHours_AllowUrgent", true);
        }

        private void LoadRuleValues()
        {
            var enabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT Event_Code, Channel, Enabled, Target FROM Notification_Rules", con))
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                        {
                            string k = Key(Convert.ToString(rd[0]), Convert.ToString(rd[1]));
                            enabled[k] = rd[2] != DBNull.Value && Convert.ToBoolean(rd[2]);
                            target[k] = rd[3] == DBNull.Value ? "" : Convert.ToString(rd[3]);
                        }
                }
            }
            catch { _tableMissing = true; }

            foreach (var kv in _checks)
            {
                bool on;
                kv.Value.Checked = enabled.TryGetValue(kv.Key, out on) && on;
            }
            foreach (var kv in _targets)
            {
                string t;
                kv.Value.Text = target.TryGetValue(kv.Key, out t) ? (t ?? "") : "";
            }
        }

        // ── บันทึก ────────────────────────────────────────────────────────────

        protected void btnSave_Click(object sender, EventArgs e)
        {
            int? adminId = null;
            try { if (Session["UserID"] != null) adminId = Convert.ToInt32(Session["UserID"]); }
            catch { }

            // ตรวจรูปแบบเวลาก่อน — ใส่ผิดแล้วเงียบทั้งระบบเป็นเรื่องใหญ่
            string qf = (txtQuietFrom.Text ?? "").Trim();
            string qt = (txtQuietTo.Text ?? "").Trim();
            if (!ValidTime(qf) || !ValidTime(qt))
            {
                Msg("err", "ช่วงเวลาเงียบต้องเป็นรูปแบบ HH:mm เช่น 22:00 (หรือปล่อยว่างทั้งคู่)");
                return;
            }
            if (string.IsNullOrEmpty(qf) != string.IsNullOrEmpty(qt))
            {
                Msg("err", "ช่วงเวลาเงียบต้องใส่ทั้งเวลาเริ่มและเวลาสิ้นสุด หรือปล่อยว่างทั้งคู่");
                return;
            }

            try
            {
                AppCfg.Set("Notify_Telegram_Enabled", chkTelegram.Checked ? "1" : "0", ToShort(adminId));
                AppCfg.Set("Notify_Line_Enabled", chkLine.Checked ? "1" : "0", ToShort(adminId));
                AppCfg.Set("TelegramChatId", (txtTgTarget.Text ?? "").Trim(), ToShort(adminId));
                AppCfg.Set("Notify_Line_Target", (txtLineTarget.Text ?? "").Trim(), ToShort(adminId));
                AppCfg.Set("Notify_QuietHours_From", qf, ToShort(adminId));
                AppCfg.Set("Notify_QuietHours_To", qt, ToShort(adminId));
                AppCfg.Set("Notify_QuietHours_AllowUrgent", chkQuietUrgent.Checked ? "1" : "0", ToShort(adminId));
            }
            catch (Exception ex)
            {
                Msg("err", "บันทึกค่าช่องทางไม่สำเร็จ: " + Server.HtmlEncode(ex.Message));
                return;
            }

            int saved = 0;
            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    foreach (var ev in global::Notify.Catalog)
                        foreach (string ch in new[] { global::Notify.ChannelTelegram, global::Notify.ChannelLine })
                        {
                            string k = Key(ev.Code, ch);
                            CheckBox cb; TextBox tb;
                            if (!_checks.TryGetValue(k, out cb)) continue;
                            _targets.TryGetValue(k, out tb);

                            using (var cmd = new SqlCommand(@"
                                IF EXISTS (SELECT 1 FROM Notification_Rules WHERE Event_Code = @e AND Channel = @c)
                                    UPDATE Notification_Rules
                                       SET Enabled = @on, Target = @t, Modified_Date = GETDATE(), Modified_By = @by
                                     WHERE Event_Code = @e AND Channel = @c;
                                ELSE
                                    INSERT INTO Notification_Rules (Event_Code, Channel, Enabled, Target, Modified_Date, Modified_By)
                                    VALUES (@e, @c, @on, @t, GETDATE(), @by);", con))
                            {
                                string t = tb == null ? "" : (tb.Text ?? "").Trim();
                                cmd.Parameters.AddWithValue("@e", ev.Code);
                                cmd.Parameters.AddWithValue("@c", ch);
                                cmd.Parameters.AddWithValue("@on", cb.Checked);
                                cmd.Parameters.AddWithValue("@t", t.Length == 0 ? (object)DBNull.Value : t);
                                cmd.Parameters.AddWithValue("@by", (object)adminId ?? DBNull.Value);
                                cmd.ExecuteNonQuery();
                                saved++;
                            }
                        }
                }
            }
            catch (Exception ex)
            {
                _tableMissing = true;
                Msg("err", "บันทึกกฎการแจ้งเตือนไม่สำเร็จ — อาจยังไม่ได้ติดตั้งตาราง ("
                    + Server.HtmlEncode(ex.Message) + ")");
                return;
            }

            global::Notify.Invalidate();
            AppCfg.Invalidate();

            var warn = new System.Text.StringBuilder();
            if (!chkTelegram.Checked && !chkLine.Checked)
                warn.Append("<br/>⚠ ปิดทั้งสองช่องทาง — จะไม่มีการแจ้งเตือนออกไปเลย");
            if (chkLine.Checked && string.IsNullOrWhiteSpace(txtLineTarget.Text))
                warn.Append("<br/>⚠ เปิด LINE ไว้แต่ยังไม่ได้ใส่ปลายทาง — ยังส่งไม่ได้");
            if (chkTelegram.Checked && string.IsNullOrWhiteSpace(txtTgTarget.Text))
                warn.Append("<br/>⚠ เปิด Telegram ไว้แต่ยังไม่ได้ใส่ chat id — ยังส่งไม่ได้");

            Msg(warn.Length > 0 ? "warn" : "ok",
                "บันทึกเรียบร้อยแล้ว (" + saved + " รายการ) — มีผลทันทีภายใน 30 วินาที" + warn);
            BindLog();
        }

        private static short? ToShort(int? v)
        {
            if (!v.HasValue) return null;
            if (v.Value < short.MinValue || v.Value > short.MaxValue) return null;
            return (short)v.Value;
        }

        private static bool ValidTime(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;
            TimeSpan t;
            return TimeSpan.TryParseExact(s.Trim(), @"hh\:mm",
                       System.Globalization.CultureInfo.InvariantCulture, out t)
                || TimeSpan.TryParse(s.Trim(), out t);
        }

        // ── ปุ่มลัด ───────────────────────────────────────────────────────────

        protected void btnAllTgOn_Click(object sender, EventArgs e) { SetAllTelegram(true); }
        protected void btnAllTgOff_Click(object sender, EventArgs e) { SetAllTelegram(false); }

        private void SetAllTelegram(bool on)
        {
            foreach (var ev in global::Notify.Catalog)
            {
                CheckBox cb;
                if (_checks.TryGetValue(Key(ev.Code, global::Notify.ChannelTelegram), out cb)) cb.Checked = on;
            }
            Msg("warn", on
                ? "ติ๊กเปิด Telegram ทุกเรื่องแล้ว — <b>กดบันทึก</b> เพื่อให้มีผลจริง"
                : "ติ๊กปิด Telegram ทุกเรื่องแล้ว — <b>กดบันทึก</b> เพื่อให้มีผลจริง");
        }

        // ── ทดสอบ ─────────────────────────────────────────────────────────────

        protected void btnTestTg_Click(object sender, EventArgs e)
        {
            pnlTest.Visible = true;
            litTest.Text = Server.HtmlEncode(
                global::Notify.TestChannel(global::Notify.ChannelTelegram, (txtTgTarget.Text ?? "").Trim()));
            BindLog();
        }

        protected void btnTestLine_Click(object sender, EventArgs e)
        {
            pnlTest.Visible = true;
            litTest.Text = Server.HtmlEncode(
                global::Notify.TestChannel(global::Notify.ChannelLine, (txtLineTarget.Text ?? "").Trim()));
            BindLog();
        }

        // ── สถานะ / บันทึก ────────────────────────────────────────────────────

        private void ShowTokenStatus()
        {
            string tg = AppCfg.Get("TelegramTokenTakeTime", "") ?? "";
            litTgToken.Text = tg.Length > 0
                ? "<span class=\"nt-status on\">ตั้งค่าแล้ว</span>"
                : "<span class=\"nt-status off\">ยังไม่ได้ตั้ง — ไปที่ ตั้งค่าระบบ</span>";

            string line = "";
            try { line = global::Notify.LineToken() ?? ""; } catch { }
            litLineToken.Text = line.Length > 0
                ? "<span class=\"nt-status on\">ตั้งค่าแล้ว</span>"
                : "<span class=\"nt-status off\">ยังไม่ได้ตั้ง — ไปที่ ตั้งค่าช่องทางแชท</span>";
        }

        private void BindLog()
        {
            try
            {
                var dt = new DataTable();
                using (var con = new SqlConnection(_conn))
                using (var da = new SqlDataAdapter(@"
                    SELECT TOP 40 LogDateTime, CAST(LogDetail AS NVARCHAR(400)) AS LogDetail
                      FROM Logs
                     WHERE LogAction = 'Notify'
                       AND LogDateTime >= DATEADD(DAY, -7, GETDATE())
                     ORDER BY LogDateTime DESC", con))
                    da.Fill(dt);
                gvLog.DataSource = dt;
                gvLog.DataBind();
            }
            catch
            {
                gvLog.DataSource = null;
                gvLog.DataBind();
            }
        }

        private void Msg(string cls, string html)
        {
            litMsg.Text = "<div class=\"nt-alert " + cls + "\">" + html + "</div>";
        }
    }
}
