using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// ตั้งค่าสัตว์เลี้ยงเข้าพัก — สวิตช์ฟีเจอร์, หน่วยคิดเงิน, ห้องที่รับ/จำนวนสูงสุด/ค่าบริการต่อตัว
    /// และข้อความนโยบายสัตว์เลี้ยง (มีเลขฉบับ + ประวัติ) — เฉพาะ Owner / Admin
    ///
    /// ช่องกรอกรายห้องสร้างตอน Page_Init (แบบหน้าเงินประกัน) เพราะ WebForms ต้องมีตัวควบคุม
    /// อยู่ก่อนถึงจะรับค่าที่ส่งกลับมาตอน postback ได้
    /// </summary>
    public partial class PetStaySettings : Page
    {
        private readonly string _conn =
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        private sealed class RoomInputs
        {
            public string Name;
            public CheckBox Allowed;
            public TextBox Max;
            public TextBox Fee;
        }

        private readonly Dictionary<int, RoomInputs> _rooms = new Dictionary<int, RoomInputs>();
        private bool _authorized;
        private bool _colMissing;

        protected void Page_Init(object sender, EventArgs e)
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
            _authorized = true;
            PetStay.InvalidateSchema();
            BuildRooms();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!_authorized) return;

            if (!IsPostBack)
            {
                LoadCentral();
                if (Request.QueryString["saved"] != null)
                {
                    int n;
                    int.TryParse(Request.QueryString["saved"], out n);
                    Msg("ok", "บันทึกเรียบร้อยแล้ว"
                        + (n > 0 ? " — นโยบายสัตว์เลี้ยงขึ้นเป็นฉบับที่ <b>" + n + "</b>" : "")
                        + " (มีผลกับการจองใหม่ตั้งแต่นี้)");
                }
            }
            ShowInfo();
            ShowPreview();
        }

        private void LoadCentral()
        {
            chkEnabled.Checked = (BookingPolicy.GetSetting(PetStay.KeyEnabled) ?? "") == "1";
            ListItem unit = ddlFeeUnit.Items.FindByValue(PetStay.FeeUnit);
            if (unit != null) { ddlFeeUnit.ClearSelection(); unit.Selected = true; }
            txtPetPolicy.Text = BookingPolicy.Get(BookingPolicy.KeyPet);
        }

        // ── รายห้อง ───────────────────────────────────────────────────────────

        private void BuildRooms()
        {
            DataTable dt = LoadRooms();
            if (dt == null)
            {
                phRooms.Controls.Add(new LiteralControl("<div class=\"ps-alert warn\">อ่านรายการห้องพักไม่สำเร็จ</div>"));
                return;
            }
            if (_colMissing)
            {
                phRooms.Controls.Add(new LiteralControl(
                    "<div class=\"ps-alert warn\">ยังไม่มีคอลัมน์สัตว์เลี้ยงในตาราง Accommodation — รัน "
                    + "<b>Database/PHASE19_Migration_23_Pet_Stay.sql</b> ก่อน แล้วโหลดหน้านี้ใหม่</div>"));
                return;
            }
            if (dt.Rows.Count == 0)
            {
                phRooms.Controls.Add(new LiteralControl("<div class=\"ps-alert info\">ยังไม่มีห้องพักที่เปิดใช้งานในระบบ</div>"));
                return;
            }

            phRooms.Controls.Add(new LiteralControl(
                "<div style=\"overflow-x:auto\"><table class=\"ps-rooms tt-no-touch\"><thead><tr>"
                + "<th>ห้องพัก</th><th style=\"width:90px\">รับสัตว์เลี้ยง</th>"
                + "<th style=\"width:150px\">สูงสุด (ตัว/ห้อง)</th>"
                + "<th style=\"width:170px\">ค่าบริการต่อตัว (บาท)</th></tr></thead><tbody>"));

            foreach (DataRow r in dt.Rows)
            {
                int id = Convert.ToInt32(r["ID"]);
                string name = Convert.ToString(r["AccomName"]);
                PetStay.RoomRule rule = PetStay.ReadRoom(r);
                int rawMax = r["Pet_Max_Per_Room"] == DBNull.Value ? 0 : Convert.ToInt32(r["Pet_Max_Per_Room"]);

                phRooms.Controls.Add(new LiteralControl(
                    "<tr><td data-th=\"ห้องพัก\"><b>" + Server.HtmlEncode(name ?? "") + "</b></td><td data-th=\"รับสัตว์เลี้ยง\">"));
                var chk = new CheckBox { ID = "petok_" + id, Checked = rule.Allowed };
                chk.InputAttributes["aria-label"] = "รับสัตว์เลี้ยง — " + (name ?? "");
                phRooms.Controls.Add(chk);

                phRooms.Controls.Add(new LiteralControl("</td><td data-th=\"สูงสุด (ตัว/ห้อง)\">"));
                var tbMax = new TextBox { ID = "petmax_" + id, Text = rawMax > 0 ? rawMax.ToString(CultureInfo.InvariantCulture) : "" };
                tbMax.Attributes["inputmode"] = "numeric";
                tbMax.Attributes["placeholder"] = "เช่น 2";
                // ตรวจฝั่งเบราว์เซอร์ (ฝั่งเซิร์ฟเวอร์ตรวจซ้ำตอนบันทึกเสมอ): จำนวนเต็ม 0–50, ติ๊ก "รับ" แล้วต้อง ≥ 1
                tbMax.Attributes["data-as-num"] = "int";
                tbMax.Attributes["data-as-min"] = "0";
                tbMax.Attributes["data-as-max"] = "50";
                tbMax.Attributes["data-as-rowreq"] = "1";
                tbMax.Attributes["data-as-rowmin"] = "1";
                tbMax.Attributes["data-as-label"] = "จำนวนสูงสุด";
                phRooms.Controls.Add(tbMax);

                phRooms.Controls.Add(new LiteralControl("</td><td data-th=\"ค่าบริการต่อตัว\">"));
                var tbFee = new TextBox { ID = "petfee_" + id, Text = rule.FeePerPet.ToString("0.##", CultureInfo.InvariantCulture) };
                tbFee.Attributes["inputmode"] = "decimal";
                tbFee.Attributes["placeholder"] = "0";
                tbFee.Attributes["data-as-num"] = "money";
                tbFee.Attributes["data-as-min"] = "0";
                tbFee.Attributes["data-as-max"] = "99999999";
                tbFee.Attributes["data-as-label"] = "ค่าบริการต่อตัว";
                phRooms.Controls.Add(tbFee);

                phRooms.Controls.Add(new LiteralControl("</td></tr>"));
                _rooms[id] = new RoomInputs { Name = name, Allowed = chk, Max = tbMax, Fee = tbFee };
            }
            phRooms.Controls.Add(new LiteralControl("</tbody></table></div>"));
        }

        private DataTable LoadRooms()
        {
            var c = new code();
            try
            {
                return c.DatabaseQuerySafe(_conn,
                    "SELECT ID, AccomName, Pet_Allowed, Pet_Max_Per_Room, Pet_Fee_Per_Pet FROM Accommodation "
                    + "WHERE Status = 1 ORDER BY OrderID, AccomName", null);
            }
            catch
            {
                _colMissing = true;
                try
                {
                    return c.DatabaseQuerySafe(_conn,
                        "SELECT ID, AccomName FROM Accommodation WHERE Status = 1 ORDER BY OrderID, AccomName", null);
                }
                catch { return null; }
            }
        }

        // ── บันทึก ────────────────────────────────────────────────────────────

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (!_authorized) return;
            // ป้ายข้อผิดพลาดรอบก่อนถูกเก็บใน ViewState — ล้างก่อนตรวจใหม่
            txtPetPolicy.Attributes.Remove("data-as-err");
            foreach (KeyValuePair<int, RoomInputs> kv0 in _rooms)
            {
                kv0.Value.Max.Attributes.Remove("data-as-err");
                kv0.Value.Fee.Attributes.Remove("data-as-err");
            }
            var problems = new List<string>();
            string who = Session["UserName"]?.ToString() ?? Session["User"]?.ToString() ?? "Admin";

            string policy = txtPetPolicy.Text ?? "";
            if (chkEnabled.Checked && string.IsNullOrWhiteSpace(policy))
                FieldError(problems, txtPetPolicy, null, "เปิดใช้ฟีเจอร์แล้วต้องมีข้อความนโยบายสัตว์เลี้ยง (ลูกค้าต้องติ๊กยอมรับ)");
            if (policy.Length > 20000)
                FieldError(problems, txtPetPolicy, null, "นโยบายสัตว์เลี้ยงยาวเกินไป (สูงสุด 20,000 ตัวอักษร)");
            if (chkEnabled.Checked && _colMissing)
                problems.Add("ยังเปิดใช้ไม่ได้ — ต้องรัน Database/PHASE19_Migration_23_Pet_Stay.sql ก่อน");

            // ตรวจรายห้องทั้งหมดก่อนบันทึกอะไร
            var updates = new List<KeyValuePair<int, object[]>>();
            if (!_colMissing)
            {
                foreach (KeyValuePair<int, RoomInputs> kv in _rooms)
                {
                    RoomInputs ri = kv.Value;
                    string label = "ห้อง " + (ri.Name ?? ("#" + kv.Key));
                    bool allowed = ri.Allowed.Checked;

                    int max = 0;
                    string rawMax = (ri.Max.Text ?? "").Trim();
                    if (rawMax.Length > 0 && (!int.TryParse(rawMax, NumberStyles.Integer, CultureInfo.InvariantCulture, out max) || max < 0 || max > 50))
                    { FieldError(problems, ri.Max, label, "จำนวนสูงสุดต้องเป็นจำนวนเต็ม 0–50"); continue; }
                    if (allowed && max < 1)
                    { FieldError(problems, ri.Max, label, "รับสัตว์เลี้ยงแล้วต้องระบุจำนวนสูงสุดอย่างน้อย 1 ตัว"); continue; }

                    decimal fee = 0m;
                    string rawFee = (ri.Fee.Text ?? "").Trim().Replace(",", "");
                    if (rawFee.Length > 0 && (!decimal.TryParse(rawFee, NumberStyles.Number, CultureInfo.InvariantCulture, out fee) || fee < 0 || fee > 99999999m))
                    { FieldError(problems, ri.Fee, label, "ค่าบริการต่อตัวต้องเป็นจำนวนเงินที่ไม่ติดลบ"); continue; }

                    updates.Add(new KeyValuePair<int, object[]>(kv.Key, new object[] { allowed, max, Math.Round(fee, 2) }));
                }
            }

            if (problems.Count > 0)
            {
                Msg("err", "มีปัญหา:<br/>• " + string.Join("<br/>• ", EncodeAll(problems)));
                return;
            }

            int policyVer = 0;
            int before = BookingPolicy.PetVersion;
            try
            {
                BookingPolicy.SaveSettings(new Dictionary<string, string>
                {
                    { PetStay.KeyEnabled, chkEnabled.Checked ? "1" : "0" },
                    { PetStay.KeyFeeUnit, ddlFeeUnit.SelectedValue == PetStay.UnitStay ? PetStay.UnitStay : PetStay.UnitNight }
                }, who);

                if (!string.IsNullOrWhiteSpace(policy))
                {
                    int after = BookingPolicy.SavePetPolicy(policy, who);
                    if (after != before) policyVer = after;
                }

                var c = new code();
                foreach (KeyValuePair<int, object[]> u in updates)
                {
                    c.DatabaseInsertSafe(_conn,
                        "UPDATE Accommodation SET Pet_Allowed = @a, Pet_Max_Per_Room = @m, Pet_Fee_Per_Pet = @f WHERE ID = @id",
                        new Dictionary<string, object>
                        {
                            { "@a", u.Value[0] }, { "@m", u.Value[1] }, { "@f", u.Value[2] }, { "@id", u.Key }
                        });
                }
            }
            catch (Exception ex)
            {
                Msg("err", "บันทึกไม่สำเร็จ: " + Server.HtmlEncode(ex.Message)
                    + "<br/>ตรวจว่ารัน <code>Database/PHASE19_Migration_22_Booking_Policies.sql</code> และ "
                    + "<code>PHASE19_Migration_23_Pet_Stay.sql</code> แล้ว");
                return;
            }

            try
            {
                new code().Logs(_conn, "PetStaySettings",
                    "บันทึกตั้งค่าสัตว์เลี้ยงเข้าพัก: เปิด=" + (chkEnabled.Checked ? "1" : "0")
                    + " หน่วย=" + ddlFeeUnit.SelectedValue + " ห้อง=" + updates.Count
                    + (policyVer > 0 ? " · นโยบายสัตว์เลี้ยงฉบับที่ " + policyVer : ""), who);
            }
            catch { }

            Response.Redirect("~/Admin/Settings/PetStay?saved=" + policyVer, false);
            System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
        }

        // ── แสดงผล ───────────────────────────────────────────────────────────

        private void ShowInfo()
        {
            var sb = new StringBuilder();
            if (_colMissing || !PetStay.SchemaReady)
            {
                sb.Append("<div class=\"ps-alert warn\">ฐานข้อมูลยังไม่พร้อม — รัน <code>Database/PHASE19_Migration_23_Pet_Stay.sql</code> ")
                  .Append("(ระหว่างนี้หน้าจองไม่แสดงส่วนสัตว์เลี้ยง แม้จะติ๊กเปิดใช้)</div>");
            }
            else
            {
                bool on = PetStay.Enabled;
                sb.Append("<div class=\"ps-alert ").Append(on ? "ok" : "info").Append("\">สถานะ: <b>")
                  .Append(on ? "เปิดใช้งาน" : "ปิดอยู่").Append("</b> · คิดค่าบริการ ")
                  .Append(Server.HtmlEncode(PetStay.UnitLabel(PetStay.FeeUnit)))
                  .Append(" · นโยบายสัตว์เลี้ยงฉบับที่ <b>").Append(BookingPolicy.PetVersion).Append("</b></div>");
            }
            int ph = SettingsUi.CountPlaceholders(BookingPolicy.Get(BookingPolicy.KeyPet));
            if (ph > 0)
                sb.Append("<div class=\"ps-alert warn\">⚠ นโยบายสัตว์เลี้ยงที่บันทึกไว้ยังมีข้อความตัวอย่าง/ช่อง <b>[แก้ไข: …]</b> ")
                  .Append(ph).Append(" จุด — ลูกค้าจะเห็นตามจริง กรุณาแก้ให้ตรงกฎของที่พักก่อนเปิดใช้</div>");
            litInfo.Text = sb.ToString();
        }

        private void ShowPreview()
        {
            litPreview.Text = "<div class=\"ps-prev\">"
                + SettingsUi.HighlightPlaceholders(BookingPolicy.ToHtml(BookingPolicy.Get(BookingPolicy.KeyPet))) + "</div>";
        }

        /// <summary>เก็บข้อผิดพลาด + ติดป้ายที่ช่องนั้น (สคริปต์ส่วนกลางแสดงข้อความใต้ช่องและไฮไลต์)</summary>
        private static void FieldError(List<string> problems, WebControl field, string label, string message)
        {
            problems.Add(string.IsNullOrEmpty(label) ? message : label + ": " + message);
            if (field != null) field.Attributes["data-as-err"] = message;
        }

        private string[] EncodeAll(List<string> items)
        {
            var arr = new string[items.Count];
            for (int i = 0; i < items.Count; i++) arr[i] = Server.HtmlEncode(items[i]);
            return arr;
        }

        private bool _msgWritten;
        private void Msg(string cls, string html)
        {
            // บันทึกไม่ผ่าน = ค่าที่พิมพ์ยังไม่ถูกบันทึก → สคริปต์ส่วนกลางเตือนก่อนออกจากหน้า
            bool err = cls == "err";
            string block = "<div class=\"ps-alert " + cls + "\" role=\"" + (err ? "alert" : "status") + "\""
                + (err ? " data-as-banner=\"err\"" : "") + ">" + html
                + (err ? SettingsUi.StartDirtyMarker : "") + "</div>";
            litMsg.Text = _msgWritten ? litMsg.Text + block : block;
            _msgWritten = true;
        }
    }
}
