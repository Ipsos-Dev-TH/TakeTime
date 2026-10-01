using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// ตั้งค่านโยบายการจอง — ข้อกำหนดและเงื่อนไข / ความเป็นส่วนตัว / การคืนเงิน / การยกเลิก
    /// ข้อความแสดงบนหน้าจอง (ลูกค้าติ๊กยอมรับก่อนยืนยัน) และหน้ายืนยันการจอง
    /// เก็บผ่าน <see cref="BookingPolicy"/> (ตาราง Booking_Policy_Config) — เฉพาะ Owner / Admin
    /// </summary>
    public partial class BookingPolicies : Page
    {
        private readonly string _conn =
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.CanAccess(Perm.SysSettings) && !Perm.CanAccess(Perm.WebContent))
            {
                Response.Redirect("~/Default", false);
                System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login", false);
                System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }
            string role = Session["User"]?.ToString();
            if (role != "Owner" && role != "Admin")
            {
                Response.Redirect("~/Default", false);
                System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }

            if (!IsPostBack)
            {
                LoadTexts();
                if (Request.QueryString["saved"] != null)
                {
                    string v = Request.QueryString["saved"];
                    int n;
                    if (int.TryParse(v, out n) && n > 0)
                        Msg("ok", "บันทึกเรียบร้อยแล้ว — นโยบายฉบับที่ <b>" + n + "</b> มีผลกับการจองใหม่ตั้งแต่นี้");
                    else
                        Msg("info", "ไม่มีข้อความเปลี่ยนแปลง — คงฉบับเดิม");
                }
            }
            ShowInfo();
            ShowPreview();
        }

        private void LoadTexts()
        {
            txtTerms.Text = BookingPolicy.Get(BookingPolicy.KeyTerms);
            txtPrivacy.Text = BookingPolicy.Get(BookingPolicy.KeyPrivacy);
            txtRefund.Text = BookingPolicy.Get(BookingPolicy.KeyRefund);
            txtCancel.Text = BookingPolicy.Get(BookingPolicy.KeyCancellation);
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            var values = new Dictionary<string, string>
            {
                { BookingPolicy.KeyTerms, txtTerms.Text ?? "" },
                { BookingPolicy.KeyPrivacy, txtPrivacy.Text ?? "" },
                { BookingPolicy.KeyRefund, txtRefund.Text ?? "" },
                { BookingPolicy.KeyCancellation, txtCancel.Text ?? "" }
            };

            // ป้ายข้อผิดพลาดรอบก่อนถูกเก็บใน ViewState — ล้างก่อนตรวจใหม่
            foreach (string k in BookingPolicy.TextKeys)
            {
                TextBox b = BoxFor(k);
                if (b != null) b.Attributes.Remove("data-as-err");
            }

            // ตรวจครบทุกช่องก่อน แล้วแจ้งทีเดียว (พร้อมไฮไลต์ช่องที่ผิด) — ไม่ต้องกดบันทึกทีละรอบ
            var problems = new List<string>();
            foreach (var kv in values)
            {
                string err = null;
                if (string.IsNullOrWhiteSpace(kv.Value))
                    err = "กรุณากรอก " + BookingPolicy.Title(kv.Key) + " — เว้นว่างไม่ได้";
                else if (kv.Value.Length > 20000)
                    err = BookingPolicy.Title(kv.Key) + " ยาวเกินไป (สูงสุด 20,000 ตัวอักษร ตอนนี้ "
                        + kv.Value.Length.ToString("N0") + ")";
                if (err == null) continue;
                problems.Add(err);
                TextBox box = BoxFor(kv.Key);
                if (box != null) box.Attributes["data-as-err"] = err;
            }
            if (problems.Count > 0)
            {
                var enc = new List<string>();
                foreach (string p in problems) enc.Add(Server.HtmlEncode(p));
                Msg("err", "ยังบันทึกไม่ได้:<br/>• " + string.Join("<br/>• ", enc.ToArray()));
                return;
            }

            string who = Session["UserName"]?.ToString() ?? Session["User"]?.ToString() ?? "Admin";
            int before = BookingPolicy.Version;
            int after;
            try
            {
                after = BookingPolicy.Save(values, who);
            }
            catch (Exception ex)
            {
                Msg("err", "บันทึกไม่สำเร็จ: " + Server.HtmlEncode(ex.Message)
                    + "<br/>ถ้ายังไม่ได้สร้างตาราง ให้รัน <code>Database/PHASE19_Migration_22_Booking_Policies.sql</code> ก่อน");
                return;
            }

            try
            {
                new code().Logs(_conn, "BookingPolicies",
                    after != before
                        ? "บันทึกนโยบายการจอง → ฉบับที่ " + after
                        : "กดบันทึกนโยบายการจอง (ไม่มีข้อความเปลี่ยน, ฉบับที่ " + after + ")",
                    who);
            }
            catch { }

            Response.Redirect("~/Admin/Settings/BookingPolicies?saved=" + (after != before ? after : 0), false);
            System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
        }

        private TextBox BoxFor(string key)
        {
            switch (key)
            {
                case BookingPolicy.KeyTerms: return txtTerms;
                case BookingPolicy.KeyPrivacy: return txtPrivacy;
                case BookingPolicy.KeyRefund: return txtRefund;
                case BookingPolicy.KeyCancellation: return txtCancel;
                default: return null;
            }
        }

        private void ShowInfo()
        {
            DateTime? upd = BookingPolicy.UpdatedAt;
            var sb = new StringBuilder();
            if (!upd.HasValue)
            {
                sb.Append("<div class=\"bp-alert warn\">ยังไม่มีนโยบายในฐานข้อมูล — หน้าจองใช้ข้อความสำรองแบบสั้นอยู่ ")
                  .Append("รัน <code>Database/PHASE19_Migration_22_Booking_Policies.sql</code> แล้วแก้ข้อความให้ตรงนโยบายจริงก่อนเปิดใช้</div>");
            }
            else
            {
                sb.Append("<div class=\"bp-alert info\">ฉบับปัจจุบัน: <b>")
                  .Append(BookingPolicy.Version)
                  .Append("</b> · แก้ไขล่าสุด ")
                  .Append(Server.HtmlEncode(upd.Value.ToString("dd/MM/yyyy HH:mm")))
                  .Append("</div>");
            }

            // ข้อความที่ "บันทึกแล้ว" ยังมีจุดตัวอย่างค้างกี่จุด (แยกรายหัวข้อ ให้รู้ว่าต้องไปแก้ตรงไหน)
            int total = 0;
            var parts = new List<string>();
            foreach (string k in BookingPolicy.TextKeys)
            {
                int n = SettingsUi.CountPlaceholders(BookingPolicy.Get(k));
                if (n <= 0) continue;
                total += n;
                parts.Add("<a href=\"#bp-" + BookingPolicy.Slug(k) + "\">" + Server.HtmlEncode(BookingPolicy.Title(k))
                    + "</a> " + n + " จุด");
            }
            if (total > 0)
                sb.Append("<div class=\"bp-alert warn\">⚠ ฉบับที่บันทึกไว้ยังมีข้อความตัวอย่าง/ช่อง <b>[แก้ไข: …]</b> ค้างอยู่ <b>")
                  .Append(total).Append("</b> จุด (")
                  .Append(string.Join(" · ", parts.ToArray()))
                  .Append(") — ลูกค้าจะเห็นข้อความนี้ตามจริง กรุณาแก้ให้ตรงนโยบายของที่พัก</div>");
            else if (upd.HasValue)
                sb.Append("<div class=\"bp-alert ok\">✓ ไม่มีข้อความตัวอย่างค้างในฉบับที่บันทึกไว้</div>");
            litInfo.Text = sb.ToString();
        }

        private void ShowPreview()
        {
            var sb = new StringBuilder();
            foreach (string k in BookingPolicy.TextKeys)
            {
                sb.Append("<details class=\"bp-prev\"><summary>")
                  .Append(Server.HtmlEncode(BookingPolicy.Title(k)))
                  .Append("</summary><div style=\"margin-top:8px;\">")
                  .Append(SettingsUi.HighlightPlaceholders(BookingPolicy.ToHtml(BookingPolicy.Get(k))))
                  .Append("</div></details>");
            }
            litPreview.Text = sb.ToString();
        }

        private bool _msgWritten;
        private void Msg(string cls, string html)
        {
            // บันทึกไม่ผ่าน = ค่าที่พิมพ์ยังไม่ถูกบันทึก → สคริปต์ส่วนกลางเตือนก่อนออกจากหน้า
            bool err = cls == "err";
            string block = "<div class=\"bp-alert " + cls + "\" role=\"" + (err ? "alert" : "status") + "\""
                + (err ? " data-as-banner=\"err\"" : "") + ">" + html
                + (err ? SettingsUi.StartDirtyMarker : "") + "</div>";
            litMsg.Text = _msgWritten ? litMsg.Text + block : block;
            _msgWritten = true;
        }
    }
}
