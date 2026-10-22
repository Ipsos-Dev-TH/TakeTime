using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// นโยบายเอกสารขาย (PHASE19_29) — เลือกว่าการขายแต่ละช่องทาง × ชนิดการชำระเงิน จะ
    ///   ออกเอกสารบน NextAcc ทันทีรายใบ / รวมเอกสารสรุปรายวัน / ไม่ส่งเข้าบัญชี
    /// + ตั้งเฉพาะรายแหล่งเงิน (Account_Paid_How.Sales_Doc_Mode) + สวิตช์ใบกำกับภาษีอย่างย่อในเครื่อง / อีเมล PDF ในเครื่อง
    /// เก็บที่ตาราง Sales_Doc_Policy + Accounting_Integration_Config — ตัวอ่าน: <see cref="SalesDocumentPolicy"/>
    /// เฉพาะ Owner / Admin ที่มีสิทธิ์บัญชี
    /// </summary>
    public partial class SalesDocPolicy : Page
    {
        private readonly code _code = new code();
        private string Conn => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        private const string AnyToken = "ANY";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SysAccounting)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login", false);
                HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }
            string role = Session["User"]?.ToString();
            if (role != "Owner" && role != "Admin")
            {
                Response.Redirect("~/Default", false);
                HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }

            if (!IsPostBack)
            {
                SalesDocumentPolicy.Invalidate();   // หน้าตั้งค่าแสดงค่าล่าสุดเสมอ
                LoadSwitches();
                if (Request.QueryString["saved"] != null)
                    Msg("ok", "บันทึกนโยบายเอกสารขายแล้ว — มีผลกับการขายครั้งถัดไปทันที (เครื่องอื่นภายใน 1 นาที)");
                Render();
            }
        }

        // ── แสดงผล ─────────────────────────────────────────────────────────────

        private void LoadSwitches()
        {
            var cfg = new AccountingConfig(Conn);
            chkEnabled.Checked = cfg.GetConfigValue(SalesDocumentPolicy.CfgEnabled, "0") == "1";
            chkLocalSlip.Checked = cfg.GetConfigValue(SalesDocumentPolicy.CfgLocalSlipRequired, "1") != "0";
            chkLocalEmail.Checked = cfg.GetConfigValue(SalesDocumentPolicy.CfgSendLocalInvoiceEmail, "0") == "1";
        }

        private void Render()
        {
            bool tableReady = SalesDocumentPolicy.TableReady(Conn);
            bool overrideReady = SalesDocumentPolicy.OverrideColumnReady(Conn);
            var cfg = new AccountingConfig(Conn);

            // สถานะ
            var st = new StringBuilder();
            st.Append("<div class='sd-status'>");
            st.Append(tableReady ? SettingsUi.Chip("ตารางนโยบายพร้อม (PHASE19_29)", "ok")
                                 : SettingsUi.Chip("ยังไม่ได้รัน PHASE19_Migration_29_Sales_Doc_Policy.sql", "err"));
            st.Append(' ');
            bool on = SalesDocumentPolicy.Enabled(Conn);
            st.Append(on ? SettingsUi.Chip("นโยบายเปิดใช้งาน", "ok") : SettingsUi.Chip("นโยบายปิด — พฤติกรรมเดิม", "off"));
            st.Append(' ');
            bool acct = false;
            try { acct = cfg.IsConfigured && cfg.Enabled; } catch { }
            st.Append(acct ? SettingsUi.Chip("เชื่อม NextAcc แล้ว", "ok") : SettingsUi.Chip("ยังไม่ได้เชื่อม/ปิดการเชื่อม NextAcc", "warn"));
            st.Append(' ');
            bool receiptDoc = false;
            try { receiptDoc = cfg.IsReceiptDocumentMode; } catch { }
            st.Append(receiptDoc ? SettingsUi.Chip("ใบเสร็จโหมด DOCUMENT", "ok")
                                 : SettingsUi.Chip("ใบเสร็จไม่ใช่โหมด DOCUMENT — เอกสารจะลงเป็นรายการบัญชี (JE) แทน", "warn"));
            st.Append(' ');
            bool legacyRollup = false;
            try { legacyRollup = cfg.IsPosDailyRollupEnabled; } catch { }
            st.Append(SettingsUi.Chip("Nexaacc_PosDailyRollup = " + (legacyRollup ? "1" : "0")
                + (on ? " (นโยบายเปิด → รวบตามนโยบายแม้เป็น 0)" : ""), "info"));
            st.Append("</div>");
            litStatus.Text = st.ToString();

            if (!tableReady)
            {
                litMatrix.Text = "<div class='sd-empty'>รัน migration <b>Database/PHASE19_Migration_29_Sales_Doc_Policy.sql</b> ก่อน แล้วโหลดหน้านี้ใหม่ — ระหว่างนี้ระบบใช้พฤติกรรมเดิม</div>";
                litOverrides.Text = "";
                litPreview.Text = "";
                litJsData.Text = "";
                btnSave.Enabled = false;
                return;
            }
            btnSave.Enabled = true;

            var rules = new Dictionary<string, SalesDocumentPolicy.Rule>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in SalesDocumentPolicy.GetRules(Conn))
                rules[Tok(r.SalesChannel) + "__" + Tok(r.ChannelType)] = r;

            litMatrix.Text = BuildMatrix(rules);
            litOverrides.Text = BuildOverrides(overrideReady);
            litPreview.Text = BuildServerPreview();
            litJsData.Text = BuildJsData();
        }

        private static string Tok(string v)
        {
            string u = (v ?? "").Trim().ToUpperInvariant();
            return u == SalesDocumentPolicy.Any || u.Length == 0 ? AnyToken : u;
        }

        private static string FromTok(string t)
        {
            return t == AnyToken ? SalesDocumentPolicy.Any : t;
        }

        private static string ModeSelect(string name, string selected, string inheritLabel)
        {
            var sb = new StringBuilder();
            sb.Append("<select class='sd-sel' name='").Append(name).Append("' data-sd-mode='1'>");
            sb.Append("<option value=''").Append(string.IsNullOrEmpty(selected) ? " selected" : "").Append(">")
              .Append(HttpUtility.HtmlEncode(inheritLabel)).Append("</option>");
            foreach (string m in SalesDocumentPolicy.Modes)
                sb.Append("<option value='").Append(m).Append("'").Append(m == selected ? " selected" : "").Append(">")
                  .Append(HttpUtility.HtmlEncode(SalesDocumentPolicy.ModeLabel(m))).Append("</option>");
            sb.Append("</select>");
            return sb.ToString();
        }

        private string BuildMatrix(Dictionary<string, SalesDocumentPolicy.Rule> rules)
        {
            var channels = new List<string> { AnyToken };
            channels.AddRange(SalesDocumentPolicy.SalesChannels);
            var types = new List<string> { AnyToken };
            types.AddRange(SalesDocumentPolicy.ChannelTypes);

            var sb = new StringBuilder();
            sb.Append("<div class='sd-scroll'><table class='sd-t'><thead><tr><th class='sd-corner'>ช่องทางขาย ↓ / ชนิดการชำระ →</th>");
            foreach (string t in types)
                sb.Append("<th>").Append(HttpUtility.HtmlEncode(SalesDocumentPolicy.TypeLabel(FromTok(t)))).Append("</th>");
            sb.Append("</tr></thead><tbody>");
            foreach (string ch in channels)
            {
                sb.Append("<tr><th class='sd-rowh'>").Append(HttpUtility.HtmlEncode(SalesDocumentPolicy.ChannelLabel(FromTok(ch))));
                if (ch == SalesDocumentPolicy.ChReservation)
                    sb.Append("<div class='sd-sub'>ใบต่อการเข้าพัก — แนะนำ \"ทันที\" เสมอ</div>");
                else if (ch == AnyToken)
                    sb.Append("<div class='sd-sub'>ค่าตั้งกลาง ใช้เมื่อช่องทางไม่ได้ตั้งเอง</div>");
                sb.Append("</th>");
                foreach (string t in types)
                {
                    string key = ch + "__" + t;
                    SalesDocumentPolicy.Rule r;
                    rules.TryGetValue(key, out r);
                    string mode = r != null ? r.Mode : null;
                    bool en = r == null || r.Enabled;
                    sb.Append("<td class='sd-cell").Append(mode != null ? " sd-set" : "").Append(r != null && !r.Enabled ? " sd-dis" : "").Append("'");
                    if (r != null)
                    {
                        string tip = (r.Note ?? "") + (r.UpdatedBy != null ? " · แก้ล่าสุด " + r.UpdatedBy : "")
                            + (r.UpdatedAt.HasValue ? " " + r.UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "");
                        sb.Append(" title='").Append(HttpUtility.HtmlAttributeEncode(tip.Trim())).Append("'");
                    }
                    sb.Append(">");
                    sb.Append(ModeSelect("sdm__" + key, mode, "— ตามกฎกลาง —"));
                    sb.Append("<label class='sd-en'><input type='checkbox' name='sde__").Append(key).Append("' value='1'")
                      .Append(en ? " checked" : "").Append(" data-sd-mode='1' /> ใช้กฎนี้</label>");
                    sb.Append("</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        private string BuildOverrides(bool overrideReady)
        {
            var phs = SalesDocumentPolicy.GetPaidHows(Conn);
            var sb = new StringBuilder();
            if (!overrideReady)
                sb.Append("<div class='sd-empty'>ยังไม่มีคอลัมน์ Account_Paid_How.Sales_Doc_Mode (รัน PHASE19_29) — ตั้งรายแหล่งเงินไม่ได้</div>");
            sb.Append("<div class='sd-scroll'><table class='sd-t sd-ov'><thead><tr><th>แหล่งเงิน (Account_Paid_How)</th><th>ชนิด</th><th>สถานะ</th><th>ตั้งเฉพาะแหล่งเงินนี้ (ทุกช่องทางขาย)</th></tr></thead><tbody>");
            if (phs.Count == 0)
                sb.Append("<tr><td colspan='4' class='sd-empty'>ไม่พบแหล่งเงิน</td></tr>");
            foreach (var ph in phs)
            {
                sb.Append("<tr").Append(ph.Active ? "" : " class='sd-inactive'").Append("><td><b>").Append(HttpUtility.HtmlEncode(ph.Name))
                  .Append("</b> <span class='sd-sub'>#").Append(ph.Id).Append("</span></td>");
                sb.Append("<td>").Append(SettingsUi.Chip(SalesDocumentPolicy.TypeLabel(ph.ChannelType), ph.Classified ? "info" : "warn"));
                if (!ph.Classified) sb.Append("<div class='sd-sub'>เดาจากชื่อ — จัดชนิดได้ที่หน้า ช่องทางชำระเงิน</div>");
                sb.Append("</td>");
                sb.Append("<td>").Append(ph.Active ? SettingsUi.Chip("ใช้งาน", "ok") : SettingsUi.Chip("ปิด", "off")).Append("</td>");
                sb.Append("<td>");
                if (overrideReady) sb.Append(ModeSelect("sdo__" + ph.Id, ph.OverrideMode, "— ใช้ตารางด้านบน —"));
                else sb.Append("-");
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        /// <summary>ตัวอย่างจากค่าที่บันทึกแล้ว (ตัวตัดสินจริง) — สคริปต์ในหน้าคำนวณใหม่ทันทีเมื่อแก้ค่า</summary>
        private string BuildServerPreview()
        {
            bool on = SalesDocumentPolicy.Enabled(Conn);
            bool slip = SalesDocumentPolicy.LocalSlipRequired(Conn);
            var sb = new StringBuilder("<ul class='sd-prev' id='sdPreviewList'>");
            foreach (var ph in SalesDocumentPolicy.GetPaidHows(Conn))
            {
                if (!ph.Active) continue;
                var r = SalesDocumentPolicy.Resolve(Conn, SalesDocumentPolicy.ChPos, ph.Id);
                sb.Append("<li>ถ้าขายหน้าร้านด้วย <b>").Append(HttpUtility.HtmlEncode(ph.Name)).Append("</b> (")
                  .Append(HttpUtility.HtmlEncode(SalesDocumentPolicy.TypeLabel(ph.ChannelType))).Append(") → ")
                  .Append(HttpUtility.HtmlEncode(on ? Describe(r.Mode, ph, slip) : "นโยบายปิด: พฤติกรรมเดิม"
                      + " (ติ๊กออกใบกำกับ = รายใบ · ไม่ติ๊ก = รวบรายวันเมื่อเปิด Nexaacc_PosDailyRollup)"))
                  .Append("<div class='sd-sub'>").Append(HttpUtility.HtmlEncode(r.Reason)).Append("</div></li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static string Describe(string mode, SalesDocumentPolicy.PaidHowInfo ph, bool slip)
        {
            switch (mode)
            {
                case SalesDocumentPolicy.ModeDailyRollup:
                    return "ไม่ออกเอกสารรายใบ — รวมเข้าเอกสารสรุปยอดขายรายวัน POSDAY-{วัน}-" + ph.Id
                        + " (ใบกำกับภาษีขายสดยอดรวม เข้าบัญชีของ \"" + ph.Name + "\")"
                        + (slip ? " · POS พิมพ์ใบกำกับภาษีอย่างย่อเลขเรียงให้ลูกค้าทุกบิล" : " · ⚠ ไม่มีใบให้ลูกค้า (ปิดการพิมพ์ใบในเครื่อง)");
                case SalesDocumentPolicy.ModeOff:
                    return "ไม่ส่งเข้าระบบบัญชีเลย"
                        + (slip ? " · POS พิมพ์ใบกำกับภาษีอย่างย่อให้ลูกค้า" : " · ⚠ ไม่มีใบให้ลูกค้า และไม่มีรายได้ใน NextAcc");
                default:
                    return "ออกใบเสร็จรับเงิน/ใบกำกับภาษีอย่างย่อ (ขายสด ผู้ซื้อไม่ประสงค์รับใบกำกับ) บน NextAcc ทันทีรายใบ"
                        + " · ลูกค้าขอใบเต็มรูป → พนักงานติ๊ก \"ขอใบกำกับเต็มรูป/ระบุชื่อ\"";
            }
        }

        private string BuildJsData()
        {
            var list = new List<object>();
            foreach (var ph in SalesDocumentPolicy.GetPaidHows(Conn))
            {
                if (!ph.Active) continue;
                list.Add(new Dictionary<string, object>
                {
                    { "id", ph.Id }, { "name", ph.Name ?? "" }, { "type", ph.ChannelType ?? "OTHER" },
                    { "typeLabel", SalesDocumentPolicy.TypeLabel(ph.ChannelType) }
                });
            }
            string json = new JavaScriptSerializer().Serialize(list);
            // กัน </script> ในชื่อแหล่งเงิน
            json = json.Replace("</", "<\\/");
            return "<script>window.SD_PAIDHOWS = " + json + ";</script>";
        }

        // ── บันทึก ──────────────────────────────────────────────────────────────

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (!SalesDocumentPolicy.TableReady(Conn)) { Render(); return; }
            string user = Session["UserName"]?.ToString() ?? Session["User"]?.ToString() ?? "ADMIN";
            if (user.Length > 100) user = user.Substring(0, 100);
            int changed = 0;
            try
            {
                var cfg = new AccountingConfig(Conn);
                changed += SetFlag(cfg, SalesDocumentPolicy.CfgEnabled, chkEnabled.Checked);
                changed += SetFlag(cfg, SalesDocumentPolicy.CfgLocalSlipRequired, chkLocalSlip.Checked);
                changed += SetFlag(cfg, SalesDocumentPolicy.CfgSendLocalInvoiceEmail, chkLocalEmail.Checked);

                var channels = new List<string> { AnyToken };
                channels.AddRange(SalesDocumentPolicy.SalesChannels);
                var types = new List<string> { AnyToken };
                types.AddRange(SalesDocumentPolicy.ChannelTypes);

                foreach (string ch in channels)
                {
                    foreach (string t in types)
                    {
                        string key = ch + "__" + t;
                        string mode = SalesDocumentPolicy.NormalizeMode(Request.Form["sdm__" + key]);
                        bool en = Request.Form["sde__" + key] == "1";
                        var p = new Dictionary<string, object>
                        {
                            { "@ch", FromTok(ch) }, { "@t", FromTok(t) }, { "@m", (object)mode ?? DBNull.Value },
                            { "@en", en }, { "@u", user }
                        };
                        if (mode == null)
                        {
                            changed += _code.DatabaseInsertSafe(Conn,
                                "DELETE FROM dbo.Sales_Doc_Policy WHERE Sales_Channel = @ch AND Channel_Type = @t", p);
                        }
                        else
                        {
                            changed += _code.DatabaseInsertSafe(Conn,
                                @"IF EXISTS (SELECT 1 FROM dbo.Sales_Doc_Policy WHERE Sales_Channel = @ch AND Channel_Type = @t)
                                      UPDATE dbo.Sales_Doc_Policy SET Doc_Mode = @m, Is_Enabled = @en, Updated_By = @u, Updated_At = GETDATE()
                                       WHERE Sales_Channel = @ch AND Channel_Type = @t
                                         AND (Doc_Mode <> @m OR Is_Enabled <> @en)
                                  ELSE
                                      INSERT INTO dbo.Sales_Doc_Policy (Sales_Channel, Channel_Type, Doc_Mode, Is_Enabled, Updated_By, Updated_At)
                                      VALUES (@ch, @t, @m, @en, @u, GETDATE())", p);
                        }
                    }
                }

                if (SalesDocumentPolicy.OverrideColumnReady(Conn))
                {
                    foreach (var ph in SalesDocumentPolicy.GetPaidHows(Conn))
                    {
                        string field = "sdo__" + ph.Id;
                        if (Request.Form[field] == null) continue;   // ไม่ได้แสดงในฟอร์ม
                        string mode = SalesDocumentPolicy.NormalizeMode(Request.Form[field]);
                        changed += _code.DatabaseInsertSafe(Conn,
                            @"UPDATE dbo.Account_Paid_How SET Sales_Doc_Mode = @m
                               WHERE ID = @id AND ISNULL(Sales_Doc_Mode, '') <> ISNULL(@m, '')",
                            new Dictionary<string, object> { { "@m", (object)mode ?? DBNull.Value }, { "@id", ph.Id } });
                    }
                }

                SalesDocumentPolicy.Invalidate();
                if (changed > 0)
                    _code.Logs(Conn, "SalesDocPolicy", $"บันทึกนโยบายเอกสารขาย — เปลี่ยน {changed} รายการ " +
                        $"(เปิด={(chkEnabled.Checked ? 1 : 0)} ใบในเครื่อง={(chkLocalSlip.Checked ? 1 : 0)} อีเมลในเครื่อง={(chkLocalEmail.Checked ? 1 : 0)})", user);
                Response.Redirect("~/Admin/Settings/SalesDocPolicy?saved=1", false);
                HttpContext.Current?.ApplicationInstance?.CompleteRequest();
            }
            catch (Exception ex)
            {
                SalesDocumentPolicy.Invalidate();
                Msg("err", "บันทึกไม่สำเร็จ: " + HttpUtility.HtmlEncode(ex.Message) + SettingsUi.StartDirtyMarker);
                Render();
            }
        }

        private static int SetFlag(AccountingConfig cfg, string key, bool on)
        {
            string want = on ? "1" : "0";
            if (cfg.GetConfigValue(key, null) == want) return 0;
            cfg.SetConfig(key, want);
            return 1;
        }

        private void Msg(string cls, string html)
        {
            litMsg.Text = "<div class='sd-alert " + cls + "'>" + html + "</div>";
        }
    }
}
