using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Payments;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// หน้าจัดการแคตตาล็อกช่องทางชำระเงิน (PaymentChannelCatalog)
    ///
    /// หนึ่งช่องทาง = หนึ่งแถว Account_Paid_How — หน้านี้แก้เฉพาะคอลัมน์แคตตาล็อก
    /// (การมองเห็น / ข้อความแนะนำ / เงื่อนไข / QR / สลิป / ลำดับ / ชนิด / เกตเวย์)
    /// ไม่แตะชื่อแหล่งเงิน สถานะ หรือการผูกบัญชี NextAcc (อยู่หน้า Accounting Integration)
    ///
    /// ตัวควบคุมรายแถวสร้างตอน Page_Init เพื่อรับค่าตอน postback ตามปกติของ WebForms
    /// บันทึกแล้ว redirect กลับมาโหลดใหม่ (สถานะ/ตัวอย่างคำนวณจากค่าล่าสุด)
    /// </summary>
    public partial class PaymentChannelsSettings : Page
    {
        private sealed class RowInputs
        {
            public int Id;
            public TextBox Code, Sort, Qr, Instructions, Conditions;
            public DropDownList Type, Provider, GatewayCode;
            public CheckBox Customer, Staff, Slip;
        }

        private readonly List<RowInputs> _rows = new List<RowInputs>();

        protected void Page_Init(object sender, EventArgs e)
        {
            if (!Perm.CanAccess(Perm.SysPayment) && !Perm.CanAccess(Perm.SysSettings))
            {
                Response.Redirect("~/Default", false);
                System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }
            BuildRows();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            if (!IsPostBack)
            {
                try { txtPolicy.Text = PaymentChannelCatalog.MasterCancellationPolicy; } catch { }
                if (Request.QueryString["saved"] != null)
                    Msg("ok", "บันทึกเรียบร้อยแล้ว — มีผลกับหน้าลูกค้า/พนักงานทันที");
            }
            ShowStatus();
            ShowPreview();
        }

        // ── สถานะเกตเวย์ ─────────────────────────────────────────────────────

        private void ShowStatus()
        {
            var sb = new StringBuilder();
            try
            {
                string prov = PaymentGatewayConfig.ActiveProvider;
                bool online = new OnlinePaymentService().IsAvailable;
                bool ready = online && PaymentGatewayConfig.IsGatewayReady;
                bool payso = prov == PaymentGatewayConfig.ProviderPayso;

                sb.Append("<div class=\"pc-chks\" style=\"margin:4px 0 8px\">")
                  .Append(Tag("เกตเวย์หลัก: " + (payso ? "PaySo" : "Omise (สำรอง)"), ""))
                  .Append(Tag(online ? "ระบบรับชำระออนไลน์: เปิด" : "ระบบรับชำระออนไลน์: ปิด", online ? "live" : "wait"))
                  .Append(Tag(ready ? "เกตเวย์พร้อม" : (payso ? "รอเปิดใช้ PaySo" : "เกตเวย์ยังไม่พร้อม"), ready ? "live" : "wait"))
                  .Append("</div>");

                if (ready)
                {
                    var gw = new OnlinePaymentService().Gateway();
                    var names = new List<string>();
                    foreach (GatewayChannel g in gw.GetAvailableChannels())
                        names.Add(g.Code + (string.IsNullOrEmpty(g.ProviderCode) || g.ProviderCode == g.Code
                            ? "" : " → " + g.ProviderCode));
                    sb.Append("<div class=\"pc-note\">ช่องทางที่เกตเวย์เปิดอยู่: <b>")
                      .Append(Server.HtmlEncode(names.Count == 0 ? "(ไม่มี)" : string.Join(", ", names.ToArray())))
                      .Append("</b> · กันวงเงินบัตร: ").Append(gw.SupportsPreAuth ? "ได้" : "ไม่ได้")
                      .Append(" · คืนเงินผ่านเกตเวย์: ").Append(gw.SupportsRefund ? "ได้" : "ยังไม่ได้ตั้งเส้นทาง")
                      .Append("</div>");
                }
                else
                {
                    sb.Append("<div class=\"pc-alert info\" style=\"margin:6px 0 0\">ลูกค้าจะ<b>ไม่เห็น</b>ช่องทางเกตเวย์ใด ๆ "
                        + "จนกว่า PaySo จะพร้อม (อนุมัติร้านค้า → เปิด \"เปิดใช้เกตเวย์ Payso\" + ใส่กุญแจที่หน้าตั้งค่าเกตเวย์) — "
                        + "ระหว่างนี้แสดงเฉพาะช่องทางโอนที่ติ๊ก \"ลูกค้าเห็น\"</div>");
                }

                if (!PaymentChannelCatalog.CatalogColumnsReady)
                    sb.Append("<div class=\"pc-alert warn\" style=\"margin:10px 0 0\">ยังไม่ได้รัน "
                        + "<b>Database/PHASE19_Migration_20_Payment_Channel_Catalog.sql</b> — ตอนนี้ระบบเดาชนิดจากชื่อแหล่งเงิน "
                        + "(ลูกค้าเห็นเฉพาะแถวที่ชื่อมีคำว่า \"โอน\") และยังบันทึกการตั้งค่าในหน้านี้ไม่ได้</div>");
            }
            catch (Exception ex)
            {
                sb.Append("<div class=\"pc-alert warn\">อ่านสถานะไม่สำเร็จ: " + Server.HtmlEncode(ex.Message) + "</div>");
            }
            litStatus.Text = sb.ToString();
        }

        private void ShowPreview()
        {
            var sb = new StringBuilder("<div class=\"pc-prev\"><div><b>ลูกค้าเห็น (หน้าจองที่พัก)</b><ul>");
            IList<PaymentChannel> cust = PaymentChannelCatalog.ForCustomer(PaymentSource.Reservation);
            if (cust.Count == 0) sb.Append("<li style=\"color:#a12626\">ไม่มีช่องทาง — ติ๊ก \"ลูกค้าเห็น\" ให้ช่องทางโอนอย่างน้อยหนึ่งช่อง</li>");
            foreach (PaymentChannel c in cust)
                sb.Append("<li>").Append(Server.HtmlEncode(c.Name)).Append(" <span class=\"pc-note\">(")
                  .Append(Server.HtmlEncode(c.Code)).Append(")</span></li>");
            sb.Append("</ul></div><div><b>พนักงานเห็น</b><ul>");
            IList<PaymentChannel> staff = PaymentChannelCatalog.ForStaff(PaymentSource.Reservation);
            if (staff.Count == 0) sb.Append("<li>ไม่มี</li>");
            foreach (PaymentChannel c in staff)
                sb.Append("<li>").Append(Server.HtmlEncode(c.Name)).Append(" <span class=\"pc-note\">(")
                  .Append(Server.HtmlEncode(c.Code)).Append(")</span></li>");
            sb.Append("</ul></div></div>");

            PaymentChannel dep = SecurityHoldService.TransferChannel();
            sb.Append("<div class=\"pc-note\" style=\"margin-top:10px\">เงินประกัน (โหมด ")
              .Append(Server.HtmlEncode(SecurityHoldService.Mode)).Append(") รับโอนเข้า: <b>")
              .Append(Server.HtmlEncode(dep == null ? "ยังไม่มีช่องทางโอน" : dep.Name + " (" + dep.Code + ")"))
              .Append("</b></div>");
            litPreview.Text = sb.ToString();
        }

        // ── ฟอร์มรายแถว ──────────────────────────────────────────────────────

        private void BuildRows()
        {
            IList<PaymentChannel> all = PaymentChannelCatalog.AllForAdmin();
            if (all.Count == 0)
            {
                phRows.Controls.Add(new LiteralControl("<div class=\"pc-alert warn\">ไม่พบแหล่งเงินในตาราง Account_Paid_How</div>"));
                return;
            }

            bool canEdit = PaymentChannelCatalog.CatalogColumnsReady;
            foreach (PaymentChannel c in all)
            {
                bool virtualRow = c.PaidHowId <= 0;
                string state;
                if (!c.Active) state = Tag("ปิดใช้ (Status)", "");
                else if (c.IsGateway && !c.GatewayLive) state = Tag(c.Unavailable ?? "ยังไม่พร้อม", "wait");
                else if (c.IsGateway) state = Tag("เกตเวย์พร้อมใช้", "live");
                else state = "";
                if (PaymentChannelCatalog.IsNeverCustomer(c.Type)) state += Tag("ลูกค้าไม่เห็นเด็ดขาด", "never");
                if (virtualRow) state += Tag("ช่องทางเสมือน (ยังไม่มีแถวแหล่งเงิน — รัน PHASE19_20)", "wait");

                phRows.Controls.Add(new LiteralControl(
                    "<div class=\"pc-ch" + (c.Active ? "" : " off") + "\"><div class=\"pc-ch-head\"><b>"
                    + Server.HtmlEncode(c.Name ?? "") + "</b>"
                    + (virtualRow ? "" : "<span class=\"pc-note\">#" + c.PaidHowId + "</span>")
                    + state + "</div>"));

                if (virtualRow || !canEdit)
                {
                    phRows.Controls.Add(new LiteralControl(
                        "<div class=\"pc-note\">รหัส " + Server.HtmlEncode(c.Code) + " · ชนิด " + Server.HtmlEncode(c.Type)
                        + " · ลูกค้าเห็น: " + (c.CustomerVisible ? "ใช่" : "ไม่") + " · พนักงานเห็น: " + (c.StaffVisible ? "ใช่" : "ไม่")
                        + "</div></div>"));
                    continue;
                }

                var ri = new RowInputs { Id = c.PaidHowId };
                string p = "ch_" + c.PaidHowId + "_";

                phRows.Controls.Add(new LiteralControl("<div class=\"pc-grid\">"));

                ri.Code = AddText(p + "code", "รหัสช่องทาง", c.Code, false, "เช่น TRANSFER_KBANK, PAYSO_VISA");
                ri.Type = AddSelect(p + "type", "ชนิด", PaymentChannelCatalog.AllTypes, c.Type, TypeText);
                ri.Provider = AddSelect(p + "prov", "เกตเวย์",
                    new[] { "", PaymentGatewayConfig.ProviderPayso, PaymentGatewayConfig.ProviderOmise }, c.Provider,
                    delegate (string v) { return v.Length == 0 ? "— ไม่ผ่านเกตเวย์ —" : (v == "PAYSO" ? "PaySo (หลัก)" : "Omise (สำรอง)"); });
                ri.GatewayCode = AddSelect(p + "gcc", "วิธีที่ส่งเข้าเกตเวย์",
                    new[] { "", PaymentGatewayConfig.MethodCard, PaymentGatewayConfig.MethodQr, PaymentGatewayConfig.MethodInstallment },
                    c.GatewayChannelCode,
                    delegate (string v) { return v.Length == 0 ? "—" : v + " · " + PaymentGatewayConfig.MethodName(v); });
                ri.Sort = AddText(p + "sort", "ลำดับ (น้อย = ขึ้นก่อน)", c.SortOrder.ToString(CultureInfo.InvariantCulture), false, "");
                ri.Qr = AddText(p + "qr", "รูป QR (URL)", RawOrEmpty(c, "qr"), false, "เช่น /Images/promptpay.png (ว่าง = ใช้ QR แบบเดิม)");

                phRows.Controls.Add(new LiteralControl("<div class=\"pc-f pc-wide\"><div class=\"pc-chks\">"));
                ri.Customer = AddCheck(p + "cust", "ลูกค้าเห็น", c.CustomerVisible);
                if (PaymentChannelCatalog.IsNeverCustomer(c.Type)) { ri.Customer.Checked = false; ri.Customer.Enabled = false; }
                ri.Staff = AddCheck(p + "staff", "พนักงานเห็น", c.StaffVisible);
                ri.Slip = AddCheck(p + "slip", "ต้องแนบสลิป", c.RequiresSlip);
                phRows.Controls.Add(new LiteralControl("</div></div>"));

                ri.Instructions = AddText(p + "ins", "ข้อความแนะนำเมื่อเลือก", RawOrEmpty(c, "ins"), true,
                    "เช่น ชื่อบัญชี / เลขบัญชี / ธนาคาร (ว่าง = ใช้ข้อมูลบัญชีของ QR แบบเดิม)");
                ri.Conditions = AddText(p + "cond", "เงื่อนไขของช่องทางนี้", c.Conditions ?? "", true,
                    "เช่น ชนิดบัตรที่รับ ค่าธรรมเนียม ระยะเวลาคืนเงิน");

                phRows.Controls.Add(new LiteralControl("</div></div>"));
                _rows.Add(ri);
            }
        }

        /// <summary>
        /// ค่าที่ผู้ดูแลตั้งเองจริง ๆ (ไม่รวมค่าสำรองจาก "QR แบบเดิม" ที่แคตตาล็อกเติมให้ตอนอ่าน)
        /// — กันบันทึกค่าสำรองลงแถวโดยไม่ตั้งใจ
        /// </summary>
        private string RawOrEmpty(PaymentChannel c, string field)
        {
            string v = field == "qr" ? c.QrImageUrl : c.Instructions;
            if (string.IsNullOrEmpty(v)) return "";
            if (c.Type != PaymentChannelCatalog.TypeTransfer
                && !(c.Type == PaymentChannelCatalog.TypeQr && !c.IsGateway)) return v;
            string fallbackQr = (PaymentGatewayConfig.Get("ManualQr_Image_Url", "") ?? "").Trim();
            string bank = (PaymentGatewayConfig.Get("ManualQr_Bank_Info", "") ?? "").Trim();
            string note = (PaymentGatewayConfig.Get("ManualQr_Note", "") ?? "").Trim();
            string fallbackIns = (bank + (bank.Length > 0 && note.Length > 0 ? "\n" : "") + note).Trim();
            if (field == "qr" && v == fallbackQr) return "";
            if (field == "ins" && v == fallbackIns) return "";
            return v;
        }

        private static string TypeText(string t)
        {
            switch (t)
            {
                case PaymentChannelCatalog.TypeTransfer: return "โอนเงิน (TRANSFER)";
                case PaymentChannelCatalog.TypeQr: return "QR (QR)";
                case PaymentChannelCatalog.TypeCard: return "บัตร (CARD)";
                case PaymentChannelCatalog.TypeGatewayOther: return "เกตเวย์อื่น (GATEWAY_OTHER)";
                case PaymentChannelCatalog.TypeCash: return "เงินสด (CASH)";
                case PaymentChannelCatalog.TypeDirectorLoan: return "เงินทดรองกรรมการ (DIRECTOR_LOAN)";
                case PaymentChannelCatalog.TypeOta: return "OTA เก็บเงิน (OTA)";
                default: return "อื่น ๆ (OTHER)";
            }
        }

        private TextBox AddText(string id, string label, string value, bool multi, string placeholder)
        {
            phRows.Controls.Add(new LiteralControl("<div class=\"pc-f" + (multi ? " pc-wide" : "") + "\"><label>"
                + Server.HtmlEncode(label) + "</label>"));
            var tb = new TextBox { ID = id, Text = value ?? "" };
            if (multi) { tb.TextMode = TextBoxMode.MultiLine; tb.Rows = 3; }
            if (!string.IsNullOrEmpty(placeholder)) tb.Attributes["placeholder"] = placeholder;
            phRows.Controls.Add(tb);
            phRows.Controls.Add(new LiteralControl("</div>"));
            return tb;
        }

        private DropDownList AddSelect(string id, string label, string[] values, string current, Func<string, string> text)
        {
            phRows.Controls.Add(new LiteralControl("<div class=\"pc-f\"><label>" + Server.HtmlEncode(label) + "</label>"));
            var ddl = new DropDownList { ID = id };
            foreach (string v in values) ddl.Items.Add(new ListItem(text(v), v));
            ListItem sel = ddl.Items.FindByValue((current ?? "").ToUpperInvariant());
            if (sel != null) sel.Selected = true;
            phRows.Controls.Add(ddl);
            phRows.Controls.Add(new LiteralControl("</div>"));
            return ddl;
        }

        private CheckBox AddCheck(string id, string label, bool value)
        {
            var cb = new CheckBox { ID = id, Text = " " + label, Checked = value };
            phRows.Controls.Add(cb);
            return cb;
        }

        // ── บันทึก ────────────────────────────────────────────────────────────

        protected void btnSave_Click(object sender, EventArgs e)
        {
            var problems = new List<string>();
            int? adminId = null;
            try { if (Session["UserID"] != null) adminId = Convert.ToInt32(Session["UserID"]); } catch { }

            // นโยบายยกเลิกหลัก
            try
            {
                string pol = (txtPolicy.Text ?? "").Trim();
                if (pol != (PaymentChannelCatalog.MasterCancellationPolicy ?? "").Trim())
                    PaymentGatewayConfig.Set("Payment_Cancellation_Policy", pol, adminId);
            }
            catch (Exception ex) { problems.Add("นโยบายยกเลิก: " + ex.Message); }

            // รหัสซ้ำกันเองในฟอร์ม (ก่อนถึงฐานข้อมูล)
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (RowInputs ri in _rows)
            {
                string code = (ri.Code.Text ?? "").Trim();
                int other;
                if (code.Length > 0 && seen.TryGetValue(code, out other))
                    problems.Add("รหัส \"" + code + "\" ซ้ำกันระหว่างแถว #" + other + " และ #" + ri.Id);
                else if (code.Length > 0) seen[code] = ri.Id;
            }

            int saved = 0;
            if (problems.Count == 0)
            {
                foreach (RowInputs ri in _rows)
                {
                    int sort;
                    if (!int.TryParse((ri.Sort.Text ?? "").Trim(), out sort)) sort = 100;

                    var ch = new PaymentChannel
                    {
                        PaidHowId = ri.Id,
                        Code = ri.Code.Text,
                        Type = ri.Type.SelectedValue,
                        Provider = ri.Provider.SelectedValue,
                        GatewayChannelCode = ri.GatewayCode.SelectedValue,
                        CustomerVisible = ri.Customer.Checked,
                        StaffVisible = ri.Staff.Checked,
                        RequiresSlip = ri.Slip.Checked,
                        SortOrder = sort,
                        QrImageUrl = ri.Qr.Text,
                        Instructions = ri.Instructions.Text,
                        Conditions = ri.Conditions.Text
                    };

                    if (ch.Provider.Length > 0 && ch.GatewayChannelCode.Length == 0)
                    { problems.Add("แถว #" + ri.Id + ": ช่องทางเกตเวย์ต้องเลือก \"วิธีที่ส่งเข้าเกตเวย์\""); continue; }

                    try
                    {
                        string err = PaymentChannelCatalog.SaveRow(ch);
                        if (err != null) problems.Add("แถว #" + ri.Id + ": " + err);
                        else saved++;
                    }
                    catch (Exception ex) { problems.Add("แถว #" + ri.Id + ": " + ex.Message); }
                }
            }

            PaymentChannelCatalog.Invalidate();

            if (problems.Count > 0)
            {
                Msg("err", "บันทึกแล้ว " + saved + " แถว แต่มีปัญหา:<br/>• "
                    + string.Join("<br/>• ", EncodeAll(problems).ToArray()));
                ShowStatus();
                ShowPreview();
                return;
            }

            Response.Redirect(Request.Path + "?saved=1", false);
            System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
        }

        // ── ตัวช่วย ───────────────────────────────────────────────────────────

        private List<string> EncodeAll(List<string> items)
        {
            var list = new List<string>();
            foreach (string s in items) list.Add(Server.HtmlEncode(s));
            return list;
        }

        private string Tag(string text, string cls)
        {
            return "<span class=\"pc-tag " + cls + "\">" + Server.HtmlEncode(text) + "</span>";
        }

        private bool _msgWritten;
        private void Msg(string cls, string html)
        {
            string block = "<div class=\"pc-alert " + cls + "\">" + html + "</div>";
            litMsg.Text = _msgWritten ? litMsg.Text + block : block;
            _msgWritten = true;
        }
    }
}
