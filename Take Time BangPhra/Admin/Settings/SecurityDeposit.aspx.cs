using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Payments;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// หน้าตั้งค่าเงินประกันความเสียหาย — รวมค่ากลางกับวงเงินรายห้องไว้ที่เดียว
    ///
    /// เดิมวงเงินรายห้อง (Accommodation.Security_Deposit_Amount) แก้ได้ทางเดียวคือ
    /// เข้าไปแก้ตารางตรง ๆ ผ่านหน้าจัดการฐานข้อมูล ซึ่งไม่ควรเป็นวิธีปกติของงานที่
    /// พนักงานต้องทำเป็นประจำ
    ///
    /// ช่องกรอกรายห้องถูกสร้างตอน Page_Init เพราะ WebForms ต้องมีตัวควบคุมอยู่ก่อน
    /// ถึงจะรับค่าที่ส่งกลับมาตอน postback ได้
    /// </summary>
    public partial class SecurityDepositSettings : Page
    {
        private readonly string _conn =
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        private DataTable _rooms;
        private readonly Dictionary<int, TextBox> _roomInputs = new Dictionary<int, TextBox>();
        private bool _colMissing;

        protected void Page_Init(object sender, EventArgs e)
        {
            if (!Perm.CanAccess(Perm.SysPayment) && !Perm.CanAccess(Perm.SysSettings))
            {
                Response.Redirect("~/Default", false);
                System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }
            BuildRooms();
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
                LoadCentral();
                if (Request.QueryString["saved"] != null)
                {
                    int n;
                    int.TryParse(Request.QueryString["saved"], out n);
                    Msg("ok", "บันทึกเรียบร้อยแล้ว — ค่ากลาง"
                        + (n > 0 ? " + วงเงินรายห้อง " + n + " ห้อง" : "")
                        + " (มีผลกับรายการที่สร้างใหม่ตั้งแต่นี้)");
                }
                ShowReadiness();
                ShowHolds();
            }
        }

        // ── ค่ากลาง ───────────────────────────────────────────────────────────

        private void LoadCentral()
        {
            try
            {
                chkEnabled.Checked = PaymentGatewayConfig.GetBool("Payment_SecurityHold_Enabled", false);
                txtDefault.Text = PaymentGatewayConfig.GetDecimal("Payment_SecurityHold_Default", 1000m)
                    .ToString("0.##", CultureInfo.InvariantCulture);
                txtWarnHours.Text = PaymentGatewayConfig.GetInt("Payment_SecurityHold_WarnHours", 24)
                    .ToString(CultureInfo.InvariantCulture);
            }
            catch
            {
                Msg("warn", "ยังไม่ได้ติดตั้งตารางของระบบชำระเงิน — กรุณารัน "
                    + "<b>Database/PHASE19_Migration_05_Online_Payment.sql</b> และ "
                    + "<b>PHASE19_Migration_09_Omise_Security_Hold.sql</b> ก่อน");
            }
        }

        /// <summary>บอกให้ชัดว่าอะไรพร้อม/ไม่พร้อม แทนที่จะให้ไปไล่เดาเอาเองทีละหน้า</summary>
        private void ShowReadiness()
        {
            bool feature = false, holdOn = false, gwReady = false, cardOn = false;
            try { feature = Feature.On("OnlinePayment") && PaymentGatewayConfig.GetBool("Payment_Enabled", false); }
            catch { }
            try { holdOn = PaymentGatewayConfig.GetBool("Payment_SecurityHold_Enabled", false); } catch { }
            try { gwReady = PaymentGatewayConfig.IsGatewayReady; } catch { }
            try { cardOn = PaymentGatewayConfig.AvailableMethods(0m).Contains(PaymentGatewayConfig.MethodCard); }
            catch { }

            var sb = new StringBuilder();
            sb.Append("<div class=\"sd-steps\">");
            Step(sb, feature, "เปิดรับชำระเงินออนไลน์", "ยังไม่เปิดระบบชำระเงินออนไลน์");
            Step(sb, holdOn, "เปิดใช้เงินประกันแล้ว", "ยังไม่เปิดใช้เงินประกัน (ติ๊กด้านล่าง)");
            Step(sb, gwReady, "เกตเวย์พร้อม", "เกตเวย์ยังไม่พร้อม (ใส่กุญแจ)");
            Step(sb, cardOn, "เปิดรับบัตรแล้ว", "ยังไม่เปิดวิธีจ่ายด้วยบัตร");
            sb.Append("</div>");

            if (feature && holdOn && gwReady && cardOn)
                sb.Append("<div class=\"sd-alert ok\" style=\"margin:12px 0 0\">"
                        + "พร้อมใช้งานครบ — สร้างลิงก์วางวงเงินได้จากหน้าจุดรับเงิน</div>");
            else
                sb.Append("<div class=\"sd-alert info\" style=\"margin:12px 0 0\">"
                        + "ข้อที่ยังไม่ครบ ทำให้<b>กันวงเงินบนบัตรไม่ได้</b> — แต่ "
                        + "<b>รับเงินประกันเป็นเงินสดยังทำได้ตามปกติ</b> "
                        + "(บันทึกไว้ในระบบเดียวกัน เช็คเอาท์แล้วคืนหรือหักได้เหมือนกัน)<br/>"
                        + "ข้อที่เกี่ยวกับเกตเวย์/วิธีชำระ ตั้งได้ที่หน้า <b>รับชำระเงินออนไลน์</b></div>");

            if (_colMissing)
                sb.Append("<div class=\"sd-alert warn\" style=\"margin:12px 0 0\">"
                        + "ยังไม่มีคอลัมน์ <b>Accommodation.Security_Deposit_Amount</b> — "
                        + "รัน <b>Database/PHASE19_Migration_11_Security_Deposit_Process.sql</b> "
                        + "ถึงจะตั้งวงเงินรายห้องได้ (ระหว่างนี้ใช้ค่ากลางกับทุกห้อง)</div>");

            litReady.Text = sb.ToString();
        }

        private static void Step(StringBuilder sb, bool done, string okText, string todoText)
        {
            sb.Append("<div class=\"sd-step " + (done ? "done" : "") + "\"><span class=\"n\">")
              .Append(done ? "✓" : "!").Append("</span><span>")
              .Append(System.Web.HttpUtility.HtmlEncode(done ? okText : todoText))
              .Append("</span></div>");
        }

        // ── รายห้อง ───────────────────────────────────────────────────────────

        private void BuildRooms()
        {
            _rooms = LoadRooms();
            if (_rooms == null)
            {
                phRooms.Controls.Add(new LiteralControl(
                    "<div class=\"sd-alert warn\">อ่านรายการห้องพักไม่สำเร็จ</div>"));
                return;
            }
            if (_rooms.Rows.Count == 0)
            {
                phRooms.Controls.Add(new LiteralControl(
                    "<div class=\"sd-alert info\">ยังไม่มีห้องพักที่เปิดใช้งานในระบบ</div>"));
                return;
            }

            phRooms.Controls.Add(new LiteralControl(
                "<div style=\"overflow-x:auto\"><table class=\"sd-rooms\"><thead><tr>"
                + "<th>ห้องพัก</th><th style=\"width:170px\">วงเงินประกัน (บาท)</th>"
                + "<th style=\"width:190px\">ผลที่ใช้จริง</th></tr></thead><tbody>"));

            decimal fallback = 1000m;
            try { fallback = PaymentGatewayConfig.GetDecimal("Payment_SecurityHold_Default", 1000m); }
            catch { }

            foreach (DataRow r in _rooms.Rows)
            {
                int id = Convert.ToInt32(r["ID"]);
                string name = Convert.ToString(r["AccomName"]);
                string cur = "";
                bool hasOwn = false;
                if (!_colMissing && r.Table.Columns.Contains("Security_Deposit_Amount")
                    && r["Security_Deposit_Amount"] != DBNull.Value)
                {
                    cur = Convert.ToDecimal(r["Security_Deposit_Amount"]).ToString("0.##", CultureInfo.InvariantCulture);
                    hasOwn = true;
                }

                phRooms.Controls.Add(new LiteralControl(
                    "<tr><td data-th=\"ห้องพัก\"><b>" + Server.HtmlEncode(name ?? "") + "</b></td>"
                    + "<td data-th=\"วงเงินประกัน\">"));

                var tb = new TextBox { ID = "room_" + id, Text = cur };
                tb.Attributes["inputmode"] = "decimal";
                tb.Attributes["placeholder"] = "ใช้ค่ากลาง";
                if (_colMissing) tb.Enabled = false;
                phRooms.Controls.Add(tb);
                _roomInputs[id] = tb;

                string effect = hasOwn
                    ? "<span class=\"sd-use own\">" + Convert.ToDecimal(r["Security_Deposit_Amount"]).ToString("N2")
                      + " บาท</span>"
                    : "<span class=\"sd-use\">ค่ากลาง " + fallback.ToString("N2") + " บาท</span>";

                phRooms.Controls.Add(new LiteralControl(
                    "</td><td data-th=\"ผลที่ใช้จริง\">" + effect + "</td></tr>"));
            }

            phRooms.Controls.Add(new LiteralControl("</tbody></table></div>"));
        }

        /// <summary>อ่านห้องพัก — เผื่อกรณียังไม่ได้รัน migration ที่เพิ่มคอลัมน์วงเงิน</summary>
        private DataTable LoadRooms()
        {
            var c = new code();
            try
            {
                return c.DatabaseQuerySafe(_conn,
                    "SELECT ID, AccomName, Security_Deposit_Amount FROM Accommodation "
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
            var problems = new List<string>();
            int? adminId = null;
            try { if (Session["UserID"] != null) adminId = Convert.ToInt32(Session["UserID"]); } catch { }

            // ค่ากลาง
            decimal def;
            if (!TryMoney(txtDefault.Text, out def) || def < 0)
                problems.Add("วงเงินแนะนำต้องเป็นตัวเลขไม่ติดลบ");

            int warn;
            if (!int.TryParse((txtWarnHours.Text ?? "").Trim(), out warn) || warn < 1 || warn > 168)
                problems.Add("เวลาเตือนล่วงหน้าต้องเป็นจำนวนชั่วโมงระหว่าง 1–168 (7 วัน)");

            if (problems.Count == 0)
            {
                try
                {
                    PaymentGatewayConfig.Set("Payment_SecurityHold_Enabled", chkEnabled.Checked ? "1" : "0", adminId);
                    PaymentGatewayConfig.Set("Payment_SecurityHold_Default",
                        def.ToString("0.##", CultureInfo.InvariantCulture), adminId);
                    PaymentGatewayConfig.Set("Payment_SecurityHold_WarnHours",
                        warn.ToString(CultureInfo.InvariantCulture), adminId);
                    PaymentGatewayConfig.Invalidate();
                }
                catch (Exception ex) { problems.Add("บันทึกค่ากลางไม่สำเร็จ: " + ex.Message); }
            }

            // รายห้อง
            int roomsSaved = 0;
            if (!_colMissing)
            {
                var c = new code();
                foreach (KeyValuePair<int, TextBox> kv in _roomInputs)
                {
                    string raw = (kv.Value.Text ?? "").Trim();
                    object val;
                    if (raw.Length == 0) val = DBNull.Value;
                    else
                    {
                        decimal v;
                        if (!TryMoney(raw, out v) || v < 0)
                        { problems.Add("ห้อง #" + kv.Key + ": \"" + raw + "\" ไม่ใช่จำนวนเงินที่ถูกต้อง"); continue; }
                        val = v;
                    }

                    try
                    {
                        c.DatabaseInsertSafe(_conn,
                            "UPDATE Accommodation SET Security_Deposit_Amount = @v WHERE ID = @id",
                            new Dictionary<string, object> { { "@v", val }, { "@id", kv.Key } });
                        roomsSaved++;
                    }
                    catch (Exception ex) { problems.Add("ห้อง #" + kv.Key + ": " + ex.Message); }
                }
            }

            if (problems.Count > 0)
            {
                // ค้างค่าที่พิมพ์ไว้ให้แก้ต่อได้ ไม่ redirect ทิ้ง
                Msg("err", "มีปัญหา:<br/>• " + string.Join("<br/>• ", problems.ToArray()));
                ShowReadiness();
                ShowHolds();
                return;
            }

            // โหลดหน้าใหม่หลังบันทึก — คอลัมน์ "ผลที่ใช้จริง" ถูกวาดตั้งแต่ Page_Init
            // จากค่าก่อนบันทึก ถ้าไม่โหลดใหม่จะยังโชว์ของเก่า (และกันกดรีเฟรชแล้วบันทึกซ้ำ)
            Response.Redirect(Request.Path + "?saved=" + roomsSaved, false);
            System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
        }

        protected void btnBulk_Click(object sender, EventArgs e)
        {
            decimal v;
            if (!TryMoney(txtBulk.Text, out v) || v < 0)
            {
                Msg("err", "กรุณากรอกจำนวนเงินที่จะเติมให้ทุกห้อง");
                ShowReadiness(); ShowHolds();
                return;
            }
            string s = v.ToString("0.##", CultureInfo.InvariantCulture);
            foreach (KeyValuePair<int, TextBox> kv in _roomInputs) kv.Value.Text = s;
            txtBulk.Text = "";
            Msg("warn", "เติมค่าให้ทุกห้องในหน้าจอแล้ว — <b>ยังไม่ได้บันทึก</b> "
                      + "ตรวจดูก่อนแล้วกด \"บันทึกทั้งหมด\"");
            ShowReadiness(); ShowHolds();
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            foreach (KeyValuePair<int, TextBox> kv in _roomInputs) kv.Value.Text = "";
            Msg("warn", "ล้างช่องรายห้องในหน้าจอแล้ว (ทุกห้องจะกลับไปใช้ค่ากลาง) — "
                      + "<b>ยังไม่ได้บันทึก</b> กด \"บันทึกทั้งหมด\" เพื่อยืนยัน");
            ShowReadiness(); ShowHolds();
        }

        // ── วงเงินที่ยังค้าง ──────────────────────────────────────────────────

        private void ShowHolds()
        {
            try
            {
                var c = new code();
                DataTable dt = c.DatabaseQuerySafe(_conn, @"
                    SELECT TOP 50 h.Hold_Ref, h.Reservation_ID, h.Amount, h.Provider,
                           h.Card_Last4, h.Held_At, h.Expires_At, h.[Status]
                      FROM Payment_Security_Holds h
                     WHERE h.[Status] IN ('HELD','PENDING_CARD')
                     ORDER BY h.ID DESC", null);

                if (dt == null || dt.Rows.Count == 0)
                {
                    litHolds.Text = "<div class=\"sd-alert info\">ยังไม่มีวงเงินที่ค้างอยู่</div>";
                    return;
                }

                var sb = new StringBuilder();
                sb.Append("<div style=\"overflow-x:auto\"><table class=\"sd-rooms\"><thead><tr>"
                        + "<th>อ้างอิง</th><th>การจอง</th><th>ยอด</th><th>วิธี</th>"
                        + "<th>สถานะ</th><th>หมดอายุ</th></tr></thead><tbody>");

                foreach (DataRow r in dt.Rows)
                {
                    bool cash = string.Equals(Convert.ToString(r["Provider"]), "CASH",
                        StringComparison.OrdinalIgnoreCase);
                    string last4 = Convert.ToString(r["Card_Last4"]);
                    string exp = r["Expires_At"] == DBNull.Value
                        ? "—"
                        : Convert.ToDateTime(r["Expires_At"]).ToString("dd/MM/yy HH:mm");

                    sb.Append("<tr><td data-th=\"อ้างอิง\">")
                      .Append(Server.HtmlEncode(Convert.ToString(r["Hold_Ref"])))
                      .Append("</td><td data-th=\"การจอง\">#")
                      .Append(Convert.ToString(r["Reservation_ID"]))
                      .Append("</td><td data-th=\"ยอด\">")
                      .Append(Convert.ToDecimal(r["Amount"]).ToString("N2"))
                      .Append("</td><td data-th=\"วิธี\">")
                      .Append(cash ? "เงินสด" : ("บัตร" + (string.IsNullOrEmpty(last4) ? "" : " ****" + last4)))
                      .Append("</td><td data-th=\"สถานะ\">")
                      .Append(Server.HtmlEncode(HoldStatus.Thai(Convert.ToString(r["Status"]))))
                      .Append("</td><td data-th=\"หมดอายุ\">").Append(cash ? "—" : exp)
                      .Append("</td></tr>");
                }
                sb.Append("</tbody></table></div>");
                litHolds.Text = sb.ToString();
            }
            catch
            {
                litHolds.Text = "<div class=\"sd-alert warn\">ยังไม่ได้ติดตั้งตารางวงเงินประกัน "
                    + "(<b>PHASE19_Migration_09</b>)</div>";
            }
        }

        // ── ตัวช่วย ───────────────────────────────────────────────────────────

        /// <summary>รับตัวเลขที่คนพิมพ์จริง — มีจุลภาคคั่นหลักพันได้ ("2,000")</summary>
        private static bool TryMoney(string s, out decimal value)
        {
            value = 0m;
            string t = (s ?? "").Trim().Replace(",", "");
            if (t.Length == 0) return false;
            return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private bool _msgWritten;
        private void Msg(string cls, string html)
        {
            string block = "<div class=\"sd-alert " + cls + "\">" + html + "</div>";
            litMsg.Text = _msgWritten ? litMsg.Text + block : block;
            _msgWritten = true;
        }
    }
}
