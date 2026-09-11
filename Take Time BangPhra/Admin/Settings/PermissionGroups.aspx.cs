using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// จัดการกลุ่มสิทธิ์ — สร้างกลุ่มเอง กำหนดสิทธิ์รายส่วน (มองเห็น / เข้าใช้งาน) และผูกพนักงาน
    ///
    /// เข้ากันได้กับของเดิม: พนักงานที่ยังไม่ถูกกำหนดกลุ่ม ใช้สิทธิ์ตาม Role เดิม (ดู <see cref="Perm"/>)
    /// การกำหนดกลุ่มจะอัปเดต Admin.Role ให้ตรงกับ Base_Role ของกลุ่มด้วย เพราะยังมีหน้าเก่า
    /// อีกจำนวนมากที่เช็คตำแหน่งจาก Session["User"] โดยตรง
    /// </summary>
    public partial class PermissionGroups : Page
    {
        private readonly code _code = new code();
        private string Conn => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        private int SelectedGroupId
        {
            get { int v; return int.TryParse(Request.QueryString["g"], out v) ? v : 0; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            // เฉพาะ Owner — สิทธิ์คือกุญแจของทั้งระบบ
            if (Session["permission"]?.ToString() != "True" || Session["User"]?.ToString() != "Owner")
            {
                Response.Redirect("~/Default");
                return;
            }
            if (!IsPostBack) Render();
        }

        // ── วาดหน้า ───────────────────────────────────────────────────────────
        private void Render()
        {
            RenderGroupList();

            if (SelectedGroupId <= 0) { pnlEdit.Visible = false; return; }

            DataTable g = Query("SELECT * FROM Permission_Groups WHERE ID = @id", P("@id", SelectedGroupId));
            if (g == null || g.Rows.Count == 0) { pnlEdit.Visible = false; return; }

            pnlEdit.Visible = true;
            litGroupName.Text = Server.HtmlEncode(g.Rows[0]["Group_Name"].ToString());
            txtGroupName.Text = g.Rows[0]["Group_Name"].ToString();
            txtGroupDesc.Text = g.Rows[0]["Description"] == DBNull.Value ? "" : g.Rows[0]["Description"].ToString();
            btnDeleteGroup.Visible = !Convert.ToBoolean(g.Rows[0]["Is_System"]);

            RenderMatrix();
            RenderMembers();
        }

        private void RenderGroupList()
        {
            var sb = new StringBuilder();
            DataTable dt = Query(
                @"SELECT g.ID, g.Group_Name, g.Description, g.Base_Role, g.Is_System,
                         (SELECT COUNT(*) FROM [dbo].[Admin] a WHERE a.Permission_Group_ID = g.ID) AS Members,
                         (SELECT COUNT(*) FROM Permission_Group_Modules m
                           WHERE m.Group_ID = g.ID AND m.Can_Access = 1) AS Allowed
                    FROM Permission_Groups g
                   WHERE g.Is_Active = 1
                   ORDER BY g.Is_System DESC, g.Group_Name", null);

            if (dt == null)
            {
                litGroups.Text = "<div style='color:#c62828;'>ยังไม่ได้สร้างตารางกลุ่มสิทธิ์ — กรุณารัน migration PHASE18_23 ก่อน</div>";
                return;
            }

            foreach (DataRow r in dt.Rows)
            {
                int id = Convert.ToInt32(r["ID"]);
                bool active = id == SelectedGroupId;
                sb.Append($"<a class='pg-g{(active ? " active" : "")}' href='?g={id}'>");
                sb.Append("<div class='n'>" + Server.HtmlEncode(r["Group_Name"].ToString()));
                if (Convert.ToBoolean(r["Is_System"])) sb.Append(" <span class='pg-sys'>มาตรฐาน</span>");
                sb.Append("</div>");
                string desc = r["Description"] == DBNull.Value ? "" : r["Description"].ToString();
                if (!string.IsNullOrEmpty(desc)) sb.Append("<div class='d'>" + Server.HtmlEncode(desc) + "</div>");
                sb.Append($"<div class='m'>👤 {r["Members"]} คน · 🔓 {r["Allowed"]}/{Perm.Catalog.Count} ส่วน · ฐาน {Server.HtmlEncode(r["Base_Role"].ToString())}</div>");
                sb.Append("</a>");
            }
            litGroups.Text = sb.ToString();
        }

        private void RenderMatrix()
        {
            var current = new Dictionary<string, Tuple<bool, bool>>(StringComparer.OrdinalIgnoreCase);
            DataTable dt = Query(
                "SELECT Module_Code, Can_View, Can_Access FROM Permission_Group_Modules WHERE Group_ID = @g",
                P("@g", SelectedGroupId));
            if (dt != null)
                foreach (DataRow r in dt.Rows)
                    current[r["Module_Code"].ToString()] =
                        Tuple.Create(Convert.ToBoolean(r["Can_View"]), Convert.ToBoolean(r["Can_Access"]));

            var sb = new StringBuilder();
            string lastCat = null;
            foreach (var m in Perm.Catalog)
            {
                if (m.Category != lastCat)
                {
                    lastCat = m.Category;
                    sb.Append($"<tr class='cat'><td colspan='3'>{Server.HtmlEncode(m.Category)}</td></tr>");
                }

                bool v = false, a = false;
                Tuple<bool, bool> cur;
                if (current.TryGetValue(m.Code, out cur)) { v = cur.Item1; a = cur.Item2; }

                sb.Append("<tr><td>");
                sb.Append("<div class='pg-mod-name'>" + Server.HtmlEncode(m.Name) + "</div>");
                if (!string.IsNullOrEmpty(m.Note))
                    sb.Append("<div class='pg-mod-note'>" + Server.HtmlEncode(m.Note) + "</div>");
                sb.Append("</td>");
                sb.Append($"<td class='chk'><input type='checkbox' name='v_{m.Code}' value='1'{(v ? " checked" : "")} " +
                          $"onchange=\"if(!this.checked){{var a=document.getElementsByName('a_{m.Code}')[0]; if(a) a.checked=false;}}\" /></td>");
                sb.Append($"<td class='chk'><input type='checkbox' name='a_{m.Code}' value='1'{(a ? " checked" : "")} " +
                          $"onchange=\"if(this.checked){{var v=document.getElementsByName('v_{m.Code}')[0]; if(v) v.checked=true;}}\" /></td>");
                sb.Append("</tr>");
            }
            litMatrix.Text = sb.ToString();
        }

        private void RenderMembers()
        {
            var sb = new StringBuilder();
            DataTable dt = Query(
                @"SELECT ID, Username, ISNULL(FirstName + ' ' + LastName, Username) AS FullName, Role
                    FROM [dbo].[Admin]
                   WHERE Permission_Group_ID = @g AND Status = 1
                   ORDER BY Username", P("@g", SelectedGroupId));

            if (dt == null || dt.Rows.Count == 0)
                sb.Append("<div style='color:#90a4ae; font-size:13.5px;'>ยังไม่มีพนักงานในกลุ่มนี้ — ทุกคนยังใช้สิทธิ์ตามตำแหน่งเดิม</div>");
            else
                foreach (DataRow r in dt.Rows)
                    sb.Append($"<div class='pg-mem'>{Server.HtmlEncode(r["FullName"].ToString())}" +
                              $"<small>{Server.HtmlEncode(r["Username"].ToString())} · {Server.HtmlEncode(r["Role"]?.ToString() ?? "")}</small></div>");
            litMembers.Text = sb.ToString();

            // รายชื่อที่ยังไม่ได้อยู่กลุ่มนี้
            ddlAddMember.Items.Clear();
            DataTable others = Query(
                @"SELECT ID, ISNULL(FirstName + ' ' + LastName, Username) AS FullName, Username, Role
                    FROM [dbo].[Admin]
                   WHERE Status = 1 AND (Permission_Group_ID IS NULL OR Permission_Group_ID <> @g)
                   ORDER BY Username", P("@g", SelectedGroupId));
            if (others != null)
                foreach (DataRow r in others.Rows)
                    ddlAddMember.Items.Add(new System.Web.UI.WebControls.ListItem(
                        $"{r["FullName"]} ({r["Username"]} · {r["Role"]})", r["ID"].ToString()));
        }

        // ── บันทึก ────────────────────────────────────────────────────────────
        protected void btnSavePerm_Click(object sender, EventArgs e)
        {
            if (SelectedGroupId <= 0) return;
            try
            {
                string name = (txtGroupName.Text ?? "").Trim();
                if (string.IsNullOrEmpty(name)) { Msg("กรุณาระบุชื่อกลุ่ม", false); Render(); return; }

                Exec("UPDATE Permission_Groups SET Group_Name = @n, Description = @d, Updated_Date = GETDATE() WHERE ID = @g",
                    P("@n", name), P("@d", (txtGroupDesc.Text ?? "").Trim()), P("@g", SelectedGroupId));

                foreach (var m in Perm.Catalog)
                {
                    bool view = Request.Form["v_" + m.Code] == "1";
                    bool access = Request.Form["a_" + m.Code] == "1";
                    if (access) view = true;   // เข้าใช้งานได้ ต้องเห็นเมนูด้วยเสมอ

                    Exec(@"IF EXISTS (SELECT 1 FROM Permission_Group_Modules WHERE Group_ID = @g AND Module_Code = @m)
                               UPDATE Permission_Group_Modules SET Can_View = @v, Can_Access = @a
                                WHERE Group_ID = @g AND Module_Code = @m;
                           ELSE
                               INSERT INTO Permission_Group_Modules (Group_ID, Module_Code, Can_View, Can_Access)
                               VALUES (@g, @m, @v, @a);",
                        P("@g", SelectedGroupId), P("@m", m.Code), P("@v", view), P("@a", access));
                }

                Perm.Invalidate();
                Msg("บันทึกสิทธิ์เรียบร้อย — มีผลภายใน 1 นาที (หรือทันทีเมื่อผู้ใช้โหลดหน้าใหม่)", true);
            }
            catch (Exception ex) { Msg("บันทึกไม่สำเร็จ: " + ex.Message, false); }
            Render();
        }

        protected void btnAddGroup_Click(object sender, EventArgs e)
        {
            string name = (txtNewGroup.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name)) { Msg("กรุณาระบุชื่อกลุ่ม", false); Render(); return; }
            try
            {
                Exec(@"IF NOT EXISTS (SELECT 1 FROM Permission_Groups WHERE Group_Name = @n)
                       INSERT INTO Permission_Groups (Group_Name, Base_Role, Is_System) VALUES (@n, @r, 0)",
                    P("@n", name), P("@r", ddlNewBaseRole.SelectedValue));
                txtNewGroup.Text = "";
                Msg($"สร้างกลุ่ม \"{name}\" แล้ว — กดเลือกกลุ่มเพื่อกำหนดสิทธิ์", true);
            }
            catch (Exception ex) { Msg("สร้างกลุ่มไม่สำเร็จ: " + ex.Message, false); }
            Render();
        }

        protected void btnDeleteGroup_Click(object sender, EventArgs e)
        {
            if (SelectedGroupId <= 0) return;
            try
            {
                // ปลดพนักงานออกจากกลุ่มก่อน → กลับไปใช้สิทธิ์ตามตำแหน่งเดิม
                Exec("UPDATE [dbo].[Admin] SET Permission_Group_ID = NULL WHERE Permission_Group_ID = @g",
                    P("@g", SelectedGroupId));
                Exec("DELETE FROM Permission_Groups WHERE ID = @g AND Is_System = 0", P("@g", SelectedGroupId));
                Perm.Invalidate();
                Response.Redirect("~/Admin/Settings/PermissionGroups", false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (Exception ex) { Msg("ลบไม่สำเร็จ: " + ex.Message, false); Render(); }
        }

        protected void btnAddMember_Click(object sender, EventArgs e)
        {
            if (SelectedGroupId <= 0 || string.IsNullOrEmpty(ddlAddMember.SelectedValue)) return;
            try
            {
                int adminId = int.Parse(ddlAddMember.SelectedValue);

                // ตั้ง Role ให้ตรงกับฐานของกลุ่มด้วย เพราะหน้าเก่าอีกหลายสิบหน้ายังเช็คตำแหน่งตรง ๆ
                // (ไม่แตะผู้ใช้ที่เป็น Owner — กันลดสิทธิ์เจ้าของโดยไม่ตั้งใจ)
                Exec(@"UPDATE a
                          SET a.Permission_Group_ID = @g,
                              a.Role = CASE WHEN a.Role = 'Owner' THEN a.Role ELSE g.Base_Role END
                         FROM [dbo].[Admin] a
                         JOIN Permission_Groups g ON g.ID = @g
                        WHERE a.ID = @id",
                    P("@g", SelectedGroupId), P("@id", adminId));

                Perm.Invalidate();
                Msg("เพิ่มพนักงานเข้ากลุ่มแล้ว", true);
            }
            catch (Exception ex) { Msg("เพิ่มไม่สำเร็จ: " + ex.Message, false); }
            Render();
        }

        // ── helpers ───────────────────────────────────────────────────────────
        private static KeyValuePair<string, object> P(string k, object v) =>
            new KeyValuePair<string, object>(k, v);

        private DataTable Query(string sql, params KeyValuePair<string, object>[] ps)
        {
            try { return _code.DatabaseQuerySafe(Conn, sql, ToDict(ps)); }
            catch { return null; }
        }

        private void Exec(string sql, params KeyValuePair<string, object>[] ps)
        {
            _code.DatabaseInsertSafe(Conn, sql, ToDict(ps));
        }

        private static Dictionary<string, object> ToDict(KeyValuePair<string, object>[] ps)
        {
            if (ps == null || ps.Length == 0) return null;
            var d = new Dictionary<string, object>();
            foreach (var p in ps) d[p.Key] = p.Value ?? DBNull.Value;
            return d;
        }

        private void Msg(string text, bool ok)
        {
            pnlMsg.Visible = true;
            divMsg.Attributes["class"] = "pg-msg " + (ok ? "pg-ok" : "pg-err");
            litMsg.Text = Server.HtmlEncode(text);
        }
    }
}
