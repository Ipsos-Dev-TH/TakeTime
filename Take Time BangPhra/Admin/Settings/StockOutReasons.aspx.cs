using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Inventory;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// ตั้งค่าเหตุผลการเบิกสินค้า (Stock_Out_Reason — PHASE19_30): ชื่อ, บัญชี (mapping key), ภาษีขายเสมือนขาย
    /// ม.77/1(8) + ฐาน (ราคาตลาด/ต้นทุน), ต้องอนุมัติ, ต้องมีหลักฐาน, เปิด/ปิด, ลำดับ, คำอธิบาย.
    /// ไม่ลบเหตุผล (แถว Product_Out อ้างรหัส) — ใช้ "ปิดใช้งาน" แทน
    /// </summary>
    public partial class StockOutReasons : Page
    {
        private readonly code _code = new code();
        private string Conn => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private static readonly Regex CodeRx = new Regex("^[A-Z][A-Z0-9_]{1,29}$");

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SysAccounting)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            string role = Session["User"]?.ToString();
            if (Session["permission"]?.ToString() != "True" || (role != "Owner" && role != "Admin"))
            {
                Response.Redirect("~/Admin/Login", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            if (!IsPostBack)
            {
                BindGlDropDown(ddlNewGl, "INVENTORY_LOSS");
                RenderRows();
            }
        }

        private bool TableReady()
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(Conn, "SELECT CASE WHEN OBJECT_ID('dbo.Stock_Out_Reason','U') IS NULL THEN 0 ELSE 1 END AS Ok", null);
                return dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["Ok"]) == 1;
            }
            catch { return false; }
        }

        /// <summary>คีย์ mapping ทั้งหมด + รหัสบัญชีที่ผูก (สำหรับ dropdown)</summary>
        private List<KeyValuePair<string, string>> LoadGlKeys()
        {
            var list = new List<KeyValuePair<string, string>>();
            try
            {
                var dt = _code.DatabaseQuerySafe(Conn,
                    @"SELECT TakeTime_Code, ISNULL(Nexaacc_AccountCode,'') AS Code, TakeTime_Description
                        FROM Accounting_Account_Mapping WHERE Is_Active = 1 ORDER BY TakeTime_Code", null);
                if (dt != null)
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        string key = r["TakeTime_Code"]?.ToString() ?? "";
                        if (key.Length == 0) continue;
                        string code = r["Code"]?.ToString() ?? "";
                        list.Add(new KeyValuePair<string, string>(key, key + (code.Length > 0 ? " → " + code : " (ยังไม่ผูก)")));
                    }
                }
            }
            catch { }
            return list;
        }

        private void BindGlDropDown(DropDownList ddl, string selected)
        {
            ddl.Items.Clear();
            foreach (var kv in LoadGlKeys()) ddl.Items.Add(new ListItem(kv.Value, kv.Key));
            if (ddl.Items.FindByValue(selected) == null && !string.IsNullOrEmpty(selected))
                ddl.Items.Insert(0, new ListItem(selected + " (ไม่มีใน mapping)", selected));
            ddl.SelectedValue = selected ?? "";
        }

        private void RenderRows()
        {
            if (!TableReady())
            {
                litRows.Text = "<tr><td colspan='10' style='color:#c62828; padding:16px;'>ยังไม่ได้รัน migration " +
                    "<b>PHASE19_30_Stock_Out_Reasons.sql</b> — รันก่อนแล้วโหลดหน้านี้ใหม่ (ตอนนี้หน้าเบิกใช้ค่าเริ่มต้นในโค้ด)</td></tr>";
                btnSave.Enabled = false; btnAdd.Enabled = false;
                return;
            }
            StockOutReasonCatalog.Invalidate();
            var keys = LoadGlKeys();
            var sb = new StringBuilder();
            foreach (var r in StockOutReasonCatalog.All(Conn))
            {
                string c = r.Code;
                sb.Append("<tr").Append(r.IsActive ? "" : " class='sr-off'").Append(">");
                sb.Append("<td><span class='sr-code'>").Append(H(c)).Append("</span></td>");
                sb.Append("<td><input type='text' maxlength='100' name='n_").Append(H(c)).Append("' value='").Append(H(r.NameTh)).Append("'/></td>");
                sb.Append("<td><select name='gl_").Append(H(c)).Append("'>");
                bool found = false;
                foreach (var kv in keys)
                {
                    bool sel = string.Equals(kv.Key, r.GlMappingKey, StringComparison.OrdinalIgnoreCase);
                    found |= sel;
                    sb.Append("<option value='").Append(H(kv.Key)).Append("'").Append(sel ? " selected" : "").Append(">").Append(H(kv.Value)).Append("</option>");
                }
                if (!found && !string.IsNullOrEmpty(r.GlMappingKey))
                    sb.Append("<option value='").Append(H(r.GlMappingKey)).Append("' selected>").Append(H(r.GlMappingKey)).Append(" (ไม่มีใน mapping)</option>");
                sb.Append("</select></td>");
                sb.Append("<td class='sr-c'><input type='checkbox' name='vat_").Append(H(c)).Append("'").Append(r.DeemedSaleVat ? " checked" : "").Append("/></td>");
                sb.Append("<td><select name='vb_").Append(H(c)).Append("'>")
                  .Append("<option value='MARKET'").Append(r.IsMarketBase ? " selected" : "").Append(">ราคาตลาด</option>")
                  .Append("<option value='COST'").Append(!r.IsMarketBase ? " selected" : "").Append(">ต้นทุน</option></select></td>");
                sb.Append("<td class='sr-c'><input type='checkbox' name='ap_").Append(H(c)).Append("'").Append(r.RequiresApproval ? " checked" : "").Append("/></td>");
                sb.Append("<td class='sr-c'><input type='checkbox' name='ev_").Append(H(c)).Append("'").Append(r.RequiresEvidence ? " checked" : "").Append("/></td>");
                sb.Append("<td class='sr-c'><input type='checkbox' name='ac_").Append(H(c)).Append("'").Append(r.IsActive ? " checked" : "").Append("/></td>");
                sb.Append("<td><input type='text' maxlength='5' name='so_").Append(H(c)).Append("' value='").Append(r.SortOrder).Append("'/></td>");
                sb.Append("<td><textarea maxlength='500' name='h_").Append(H(c)).Append("'>").Append(H(r.HelpTh)).Append("</textarea></td>");
                sb.Append("</tr>");
            }
            litRows.Text = sb.ToString();
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (!TableReady()) { RenderRows(); return; }
            int n = 0;
            try
            {
                foreach (var r in StockOutReasonCatalog.All(Conn))
                {
                    string c = r.Code;
                    string name = (Request.Form["n_" + c] ?? "").Trim();
                    if (name.Length == 0) name = r.NameTh;
                    string gl = (Request.Form["gl_" + c] ?? r.GlMappingKey ?? "").Trim();
                    string vb = Request.Form["vb_" + c] == "COST" ? "COST" : "MARKET";
                    int sort;
                    if (!int.TryParse(Request.Form["so_" + c], out sort)) sort = r.SortOrder;
                    string help = (Request.Form["h_" + c] ?? "").Trim();
                    _code.DatabaseInsertSafe(Conn,
                        @"UPDATE dbo.Stock_Out_Reason
                             SET Name_TH = @n, Gl_Mapping_Key = @gl, Deemed_Sale_Vat = @vat, Vat_Base = @vb,
                                 Requires_Approval = @ap, Requires_Evidence = @ev, Is_Active = @ac, Sort_Order = @so, Help_TH = @h
                           WHERE Code = @c",
                        new Dictionary<string, object>
                        {
                            { "@n", Trunc(name, 100) }, { "@gl", Trunc(gl, 60) }, { "@vat", Chk("vat_" + c) },
                            { "@vb", vb }, { "@ap", Chk("ap_" + c) }, { "@ev", Chk("ev_" + c) }, { "@ac", Chk("ac_" + c) },
                            { "@so", sort }, { "@h", help.Length == 0 ? null : Trunc(help, 500) }, { "@c", c }
                        });
                    n++;
                }
                StockOutReasonCatalog.Invalidate();
                Log("แก้ไขเหตุผลการเบิก " + n + " รายการ");
                ShowMsg("บันทึกแล้ว " + n + " เหตุผล", true);
            }
            catch (Exception ex) { ShowMsg("บันทึกไม่สำเร็จ: " + ex.Message, false); }
            RenderRows();
        }

        protected void btnAdd_Click(object sender, EventArgs e)
        {
            if (!TableReady()) { RenderRows(); return; }
            string code = (txtNewCode.Text ?? "").Trim().ToUpperInvariant();
            string name = (txtNewName.Text ?? "").Trim();
            if (!CodeRx.IsMatch(code)) { ShowMsg("รหัสต้องขึ้นต้นด้วย A-Z และมีแค่ A-Z, 0-9, _ (2-30 ตัว)", false); RenderRows(); return; }
            if (name.Length == 0) { ShowMsg("กรุณาระบุชื่อเหตุผล", false); RenderRows(); return; }
            if (StockOutReasonCatalog.Get(Conn, code) != null && StockOutReasonCatalog.IsFromDatabase(Conn))
            {
                ShowMsg("มีรหัส " + code + " อยู่แล้ว", false); RenderRows(); return;
            }
            try
            {
                int maxSort = StockOutReasonCatalog.All(Conn).Select(r => r.SortOrder).DefaultIfEmpty(0).Max();
                _code.DatabaseInsertSafe(Conn,
                    @"IF NOT EXISTS (SELECT 1 FROM dbo.Stock_Out_Reason WHERE Code = @c)
                      INSERT INTO dbo.Stock_Out_Reason (Code, Name_TH, Gl_Mapping_Key, Deemed_Sale_Vat, Vat_Base, Requires_Approval, Requires_Evidence, Is_Active, Sort_Order, Help_TH)
                      VALUES (@c, @n, @gl, @vat, 'MARKET', @ap, @ev, 1, @so, @h)",
                    new Dictionary<string, object>
                    {
                        { "@c", code }, { "@n", Trunc(name, 100) }, { "@gl", ddlNewGl.SelectedValue },
                        { "@vat", chkNewVat.Checked }, { "@ap", chkNewApproval.Checked }, { "@ev", chkNewEvidence.Checked },
                        { "@so", maxSort + 10 }, { "@h", "เพิ่มโดยผู้ดูแล — ให้ผู้ทำบัญชียืนยันการลงบัญชีและภาษี" }
                    });
                StockOutReasonCatalog.Invalidate();
                Log("เพิ่มเหตุผลการเบิก " + code);
                ShowMsg("เพิ่มเหตุผล " + code + " แล้ว", true);
                txtNewCode.Text = ""; txtNewName.Text = "";
            }
            catch (Exception ex) { ShowMsg("เพิ่มไม่สำเร็จ: " + ex.Message, false); }
            RenderRows();
        }

        private bool Chk(string field) => !string.IsNullOrEmpty(Request.Form[field]);

        private void Log(string msg)
        {
            try { _code.Logs(Conn, "StockOutReasons", msg, Session["UserName"]?.ToString() ?? "ADMIN"); } catch { }
        }

        private void ShowMsg(string msg, bool ok)
        {
            pnlMsg.Visible = true;
            divMsg.Attributes["class"] = "sr-msg " + (ok ? "sr-ok" : "sr-err");
            litMsg.Text = H(msg);
        }

        private static string Trunc(string s, int max) => string.IsNullOrEmpty(s) ? (s ?? "") : (s.Length <= max ? s : s.Substring(0, max));
        private static string H(string s) => HttpUtility.HtmlEncode(s ?? "");
    }
}
