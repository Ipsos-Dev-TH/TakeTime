using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// Mapping ห้อง OTA (ตาราง MapDataWithSTAAH) — "ชื่อห้องที่ OTA ส่งมา" → ห้องจริงที่ติ๊กไว้ + อาหารเช้า
    ///
    /// เดิมแก้ผ่านหน้าจัดการฐานข้อมูลซึ่งเห็นแค่เลข Accommodation_ID ⇒ ผูกห้องกระจกไว้กับแผน Nordic Tent โดยไม่รู้ตัว
    /// และอาหารเช้ารู้จากข้อความในหมายเหตุเท่านั้น. หน้านี้: แสดงชื่อห้อง + ประเภท, เตือนห้องปนประเภท/ปิดใช้งาน/ยังไม่ระบุอาหารเช้า,
    /// ติ๊กอาหารเช้า (คอลัมน์ Breakfast — PHASE19_44), รายชื่อห้องจาก OTA ที่ยังไม่ map (จาก log ตัวอ่านอีเมล), ทดสอบจับคู่,
    /// สวิตช์ Email_Rsv_MapStrict. ตัวจับคู่จริงอยู่ที่ <see cref="EmailReservationService"/> + <see cref="OtaRoomMap"/>.
    /// </summary>
    public partial class OtaRoomMapping : Page
    {
        private readonly string _conn =
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        private const char KeySep = '\u001f';

        private class Room { public int Id, OrderId; public string Name = "", Type = ""; public bool Active; }
        private class Group
        {
            public string Agency = "", RoomType = "", Note = "";
            public List<int> Ids = new List<int>();
            public List<string> Breakfasts = new List<string>();
            public string Breakfast => OtaRoomMap.Combine(Breakfasts);
        }

        private bool _hasBf, _hasType;
        private string _noteCol = "";
        private Dictionary<int, Room> _rooms = new Dictionary<int, Room>();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login", false);
                HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }
            string role = Session["User"]?.ToString();
            if ((role != "Owner" && role != "Admin") || !(Perm.CanAccess(Perm.SysSettings) || Perm.CanAccess(Perm.SysChannel)))
            {
                Response.Redirect("~/Default", false);
                HttpContext.Current?.ApplicationInstance?.CompleteRequest();
                return;
            }
            if (IsPostBack) { litAlert.Text = ""; litSchema.Text = ""; }
            if (!IsPostBack)
            {
                txtTestIn.Text = DateTime.Today.ToString("yyyy-MM-dd");
                LoadAll(true);
            }
        }

        // ── ข้อมูล ───────────────────────────────────────────────────────────

        private static HashSet<string> Columns(SqlConnection con, string table)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SqlCommand("SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(@t)", con))
            {
                cmd.Parameters.AddWithValue("@t", "dbo." + table);
                using (var rd = cmd.ExecuteReader()) while (rd.Read()) set.Add(rd.GetString(0));
            }
            return set;
        }

        private bool LoadSchema(SqlConnection con)
        {
            var cols = Columns(con, "MapDataWithSTAAH");
            if (cols.Count == 0) return false;
            _hasBf = cols.Contains("Breakfast");
            _noteCol = new[] { "Remark", "Remarks", "Note", "Notes", "Comment", "Description", "Detail", "หมายเหตุ" }
                .FirstOrDefault(cols.Contains) ?? "";
            _hasType = Columns(con, "Accommodation").Contains("Accommodation_Type");

            _rooms.Clear();
            string typeSel = _hasType ? "ISNULL(CONVERT(nvarchar(100), Accommodation_Type), N'')" : "N''";
            using (var cmd = new SqlCommand("SELECT ID, ISNULL(AccomName, N''), " + typeSel +
                ", ISNULL(CONVERT(nvarchar(10), Status), '0'), ISNULL(TRY_CONVERT(int, OrderID), 0) FROM Accommodation", con))
            using (var rd = cmd.ExecuteReader())
                while (rd.Read())
                {
                    string st = rd[3].ToString();
                    var r = new Room
                    {
                        Id = Convert.ToInt32(rd[0]), Name = rd[1].ToString(), Type = rd[2].ToString().Trim(),
                        Active = st == "1" || st.Equals("True", StringComparison.OrdinalIgnoreCase), OrderId = Convert.ToInt32(rd[4])
                    };
                    _rooms[r.Id] = r;
                }
            return true;
        }

        private List<Group> LoadGroups(SqlConnection con)
        {
            var map = new Dictionary<string, Group>(StringComparer.Ordinal);
            string noteSel = _noteCol.Length > 0 ? "ISNULL(CONVERT(nvarchar(400), [" + _noteCol + "]), N'')" : "N''";
            string bfSel = _hasBf ? "CONVERT(nvarchar(20), Breakfast)" : "CAST(NULL AS nvarchar(20))";
            using (var cmd = new SqlCommand("SELECT ISNULL(Agency, N''), ISNULL(ROOM_TYPE, N''), Accommodation_ID, " + noteSel + ", " + bfSel +
                " FROM MapDataWithSTAAH", con))
            using (var rd = cmd.ExecuteReader())
                while (rd.Read())
                {
                    string a = rd[0].ToString(), r = rd[1].ToString();
                    string k = a + KeySep + r;
                    Group g;
                    if (!map.TryGetValue(k, out g)) map[k] = g = new Group { Agency = a, RoomType = r };
                    if (rd[2] != DBNull.Value)
                    {
                        int id = Convert.ToInt32(rd[2]);
                        if (!g.Ids.Contains(id)) g.Ids.Add(id);
                    }
                    string note = rd[3].ToString().Trim();
                    if (note.Length > 0 && g.Note.IndexOf(note, StringComparison.Ordinal) < 0)
                        g.Note = g.Note.Length == 0 ? note : g.Note + " · " + note;
                    g.Breakfasts.Add(rd[4] == DBNull.Value ? null : rd[4].ToString());
                }
            return map.Values.OrderBy(x => x.Agency, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(x => x.RoomType, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private string Cfg(SqlConnection con, string key, string def)
        {
            try
            {
                using (var cmd = new SqlCommand("SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = @k", con))
                {
                    cmd.Parameters.AddWithValue("@k", key);
                    object v = cmd.ExecuteScalar();
                    return v == null || v == DBNull.Value ? def : v.ToString();
                }
            }
            catch { return def; }
        }

        // ── แสดงผล ───────────────────────────────────────────────────────────

        private void LoadAll(bool fillRoomList)
        {
            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    if (!LoadSchema(con))
                    {
                        Alert("err", "ไม่มีตาราง MapDataWithSTAAH ในฐานข้อมูลนี้");
                        return;
                    }
                    if (!_hasBf)
                        litSchema.Text = "<div class=\"om-alert warn\">ยังไม่ได้รัน <b>PHASE19_Migration_44</b> — ช่องติ๊กอาหารเช้ายังบันทึกไม่ได้ "
                                       + "(ระบบยังอ่านอาหารเช้าจากข้อความหมายเหตุแบบเดิม)</div>";
                    bool strict = Cfg(con, "Email_Rsv_MapStrict", "1") != "0";
                    litStrict.Text = strict
                        ? "<span class=\"om-chip ok\">เปิด</span> จัดห้องเฉพาะแถวที่ <b>ช่องทาง + ชื่อห้องเต็ม</b> ตรงกัน — ไม่ตรง = ไม่ลงจองอัตโนมัติ แจ้งให้คนจัด"
                        : "<span class=\"om-chip err\">ปิด</span> ใช้การจับคู่แบบหลวมรุ่นเดิม (ชื่อสั้น / 15 ตัวแรก / ข้ามช่องทาง) — <b>ไม่แนะนำ</b> ห้องอาจผิดประเภท";
                    btnStrict.Text = strict ? "ปิดโหมดเข้ม (ไม่แนะนำ)" : "เปิดโหมดเข้ม (แนะนำ)";
                    btnStrict.CommandArgument = strict ? "0" : "1";
                    btnStrict.OnClientClick = strict
                        ? "return confirm('ปิดโหมดเข้ม = ระบบจะจัดห้องจากชื่อที่คล้ายกันเอง (อาจได้ห้องผิดประเภท) ยืนยัน?');" : "";

                    litPriority.Text = PriorityHtml(Cfg(con, "Email_Rsv_RoomPriority", ""));

                    var groups = LoadGroups(con);
                    RenderGroups(groups);
                    RenderUnmapped(con, groups);
                    if (fillRoomList) FillRoomList();
                    FillAgencies(groups);
                }
            }
            catch (Exception ex)
            {
                Alert("err", "โหลดข้อมูลไม่สำเร็จ: " + ex.Message);
            }
        }

        private string RoomLabel(int id)
        {
            Room r;
            if (!_rooms.TryGetValue(id, out r)) return "#" + id + " (ไม่มีห้องนี้)";
            return r.Name + (r.Type.Length > 0 ? " · " + r.Type : "") + (r.Active ? "" : " (ปิดใช้งาน)");
        }

        private string PriorityHtml(string cfg)
        {
            var ids = (cfg ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => { int v; return int.TryParse(x.Trim(), out v) ? v : -1; }).Where(v => v > 0).ToList();
            if (ids.Count == 0)
                return "ลำดับห้องที่จัดให้ก่อน: เรียงตามลำดับห้อง (OrderID) — ตั้งได้ที่ค่า <code>Email_Rsv_RoomPriority</code>";
            return "ลำดับห้องที่จัดให้ก่อน (<code>Email_Rsv_RoomPriority</code>): "
                 + string.Join(" → ", ids.Select(i => "<b>" + i + "</b> " + H(RoomLabel(i))))
                 + "<br/><small>ใช้เฉพาะห้องที่ติ๊กไว้ใน mapping ของแผนนั้น — ห้องที่ไม่ได้ติ๊กจะไม่ถูกจัดให้แม้อยู่ในลำดับ</small>";
        }

        private List<string> Warnings(Group g, List<Group> all)
        {
            var w = new List<string>();
            var mixed = OtaRoomMap.MixedTypes(g.Ids.Where(_rooms.ContainsKey).Select(i => _rooms[i].Type));
            if (mixed.Count > 0) w.Add("ห้องปนหลายประเภท: " + string.Join(", ", mixed) + " — ตรวจว่าตั้งใจขายแผนนี้ด้วยห้องทุกประเภทนี้");
            int missing = g.Ids.Count(i => !_rooms.ContainsKey(i));
            int off = g.Ids.Count(i => _rooms.ContainsKey(i) && !_rooms[i].Active);
            if (missing > 0) w.Add("ชี้ไปห้องที่ไม่มีอยู่ " + missing + " ห้อง");
            if (off > 0) w.Add("ห้องปิดใช้งาน " + off + " ห้อง (ไม่ถูกจัดให้)");
            if (g.Ids.Count == 0) w.Add("ยังไม่ได้ติ๊กห้อง");
            string bf = g.Breakfast;
            if (bf == OtaRoomMap.BfConflict) w.Add("อาหารเช้าขัดกันในแถวเดียวกัน — บันทึกใหม่ให้เป็นค่าเดียว");
            else if (bf == null) w.Add("ยังไม่ระบุอาหารเช้า");
            string fromName = OtaRoomMap.BreakfastFromText(g.RoomType);
            if (fromName != null && bf != null && bf != OtaRoomMap.BfConflict && fromName != bf)
                w.Add("ชื่อจาก OTA ดูเหมือน \"" + OtaRoomMap.BreakfastLabel(fromName) + "\" แต่ติ๊กไว้ \"" + OtaRoomMap.BreakfastLabel(bf) + "\" — ตรวจกับหน้า OTA");
            if (all.Any(o => !ReferenceEquals(o, g) && OtaRoomMap.NormChannel(o.Agency) == OtaRoomMap.NormChannel(g.Agency)
                             && OtaRoomMap.RoomTypeEquals(o.RoomType, g.RoomType)))
                w.Add("ซ้ำกับอีกแถว (ต่างแค่ช่องว่าง/ตัวพิมพ์) — ห้องของทั้งสองแถวถูกรวมกัน ลบแถวที่ไม่ใช้");
            return w;
        }

        private void RenderGroups(List<Group> groups)
        {
            string filter = (txtFilter.Text ?? "").Trim().ToLowerInvariant();
            bool onlyWarn = chkOnlyWarn.Checked;
            var sb = new StringBuilder();
            int shown = 0, warnCount = 0;
            foreach (var g in groups)
            {
                var w = Warnings(g, groups);
                if (w.Count > 0) warnCount++;
                if (onlyWarn && w.Count == 0) continue;
                if (filter.Length > 0 && (g.Agency + " " + g.RoomType + " " + string.Join(" ", g.Ids.Select(RoomLabel))).ToLowerInvariant().IndexOf(filter, StringComparison.Ordinal) < 0)
                    continue;
                shown++;
                string key = EncodeKey(g.Agency, g.RoomType);
                string bf = g.Breakfast;
                string bfChip = bf == OtaRoomMap.BfIncluded ? "<span class=\"om-chip ok\">รวมอาหารเช้า</span>"
                              : bf == OtaRoomMap.BfExcluded ? "<span class=\"om-chip warn\">ไม่รวมอาหารเช้า</span>"
                              : bf == OtaRoomMap.BfConflict ? "<span class=\"om-chip err\">ขัดกัน</span>"
                              : "<span class=\"om-chip off\">ไม่ระบุ</span>";
                sb.Append("<tr><td data-l=\"ช่องทาง\"><b>").Append(H(g.Agency)).Append("</b></td>")
                  .Append("<td data-l=\"ชื่อห้องจาก OTA\" class=\"om-rt\">").Append(H(g.RoomType)).Append("</td>")
                  .Append("<td data-l=\"อาหารเช้า\">").Append(bfChip).Append("</td><td data-l=\"ห้องที่จัดให้\">");
                foreach (int id in g.Ids.OrderBy(i => _rooms.ContainsKey(i) ? _rooms[i].Type : "").ThenBy(i => i))
                {
                    Room r; bool ok = _rooms.TryGetValue(id, out r) && r.Active;
                    sb.Append("<span class=\"om-room").Append(ok ? "" : " bad").Append("\">").Append(H(RoomLabel(id))).Append("</span> ");
                }
                if (g.Note.Length > 0) sb.Append("<div class=\"om-note\">หมายเหตุ: ").Append(H(g.Note)).Append("</div>");
                foreach (string x in w) sb.Append("<div class=\"om-warn\">⚠ ").Append(H(x)).Append("</div>");
                sb.Append("</td><td data-l=\"\" class=\"om-act\">")
                  .Append("<button type=\"button\" class=\"om-btn sm\" onclick=\"omAct('edit','").Append(key).Append("')\">แก้ไข</button> ")
                  .Append("<button type=\"button\" class=\"om-btn sm danger\" onclick=\"omAct('del','").Append(key)
                  .Append("',").Append(JsStr("ลบ mapping \"" + g.Agency + " | " + g.RoomType + "\" ? ใบจองใหม่ของชื่อนี้จะไม่ลงอัตโนมัติจนกว่าจะ map ใหม่")).Append(")\">ลบ</button>")
                  .Append("</td></tr>");
            }
            litGroups.Text = shown == 0
                ? "<tr><td colspan=\"5\" class=\"om-empty\">ไม่มีรายการ" + (filter.Length > 0 || onlyWarn ? "ตามตัวกรอง" : "") + "</td></tr>"
                : sb.ToString();
            litGroupSummary.Text = "ทั้งหมด " + groups.Count + " แผน · ต้องตรวจ " + warnCount + " แผน";
        }

        /// <summary>ชื่อห้องที่ตัวอ่านอีเมลหาแถวไม่เจอ (log "no mapping") 30 วันล่าสุด — ตัดที่ map แล้วออก</summary>
        private void RenderUnmapped(SqlConnection con, List<Group> groups)
        {
            var found = new Dictionary<string, Tuple<string, string, int, DateTime>>(StringComparer.Ordinal);
            try
            {
                using (var cmd = new SqlCommand(@"SELECT TOP 500 LogDateTime, LogDetail FROM Logs
                    WHERE LogDateTime >= @since AND LogAction = N'EmailReservation' AND LogDetail LIKE N'no mapping%'
                    ORDER BY LogDateTime DESC", con))
                {
                    cmd.CommandTimeout = 15;
                    cmd.Parameters.AddWithValue("@since", DateTime.Now.AddDays(-30));
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                        {
                            string ch, rt;
                            if (!OtaRoomMap.TryParseUnmappedLog(rd[1].ToString(), out ch, out rt)) continue;
                            if (groups.Any(g => OtaRoomMap.ChannelAlike(g.Agency, ch) && OtaRoomMap.RoomTypeEquals(g.RoomType, rt))) continue;
                            string k = OtaRoomMap.NormChannel(ch) + KeySep + OtaRoomMap.Key(rt);
                            DateTime at = Convert.ToDateTime(rd[0]);
                            Tuple<string, string, int, DateTime> cur;
                            found[k] = found.TryGetValue(k, out cur)
                                ? Tuple.Create(cur.Item1, cur.Item2, cur.Item3 + 1, cur.Item4)
                                : Tuple.Create(ch, rt, 1, at);
                        }
                }
            }
            catch (Exception ex)
            {
                litUnmapped.Text = "<div class=\"om-alert warn\">อ่าน log ไม่สำเร็จ: " + H(ex.Message) + "</div>";
                return;
            }
            pnlUnmapped.Visible = found.Count > 0;
            var sb = new StringBuilder("<table class=\"om-table\"><thead><tr><th>ช่องทาง</th><th>ชื่อห้องที่ OTA ส่งมา</th><th>พบ</th><th>ล่าสุด</th><th></th></tr></thead><tbody>");
            foreach (var f in found.Values.OrderByDescending(x => x.Item4))
                sb.Append("<tr><td data-l=\"ช่องทาง\"><b>").Append(H(f.Item1)).Append("</b></td><td data-l=\"ชื่อห้อง\" class=\"om-rt\">").Append(H(f.Item2))
                  .Append("</td><td data-l=\"พบ\">").Append(f.Item3).Append(" ครั้ง</td><td data-l=\"ล่าสุด\">").Append(f.Item4.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))
                  .Append("</td><td class=\"om-act\"><button type=\"button\" class=\"om-btn sm\" onclick=\"omAct('new','")
                  .Append(EncodeKey(f.Item1, f.Item2)).Append("')\">ตั้ง mapping</button></td></tr>");
            litUnmapped.Text = sb.Append("</tbody></table>").ToString();
        }

        private void FillRoomList()
        {
            cblRooms.Items.Clear();
            foreach (var r in _rooms.Values.OrderBy(x => x.Type, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.OrderId).ThenBy(x => x.Id))
            {
                var li = new ListItem(r.Name + (r.Type.Length > 0 ? " · " + r.Type : "") + (r.Active ? "" : " (ปิดใช้งาน)") + " [#" + r.Id + "]",
                                      r.Id.ToString(CultureInfo.InvariantCulture));
                cblRooms.Items.Add(li);
            }
        }

        private void FillAgencies(List<Group> groups)
        {
            var names = groups.Select(g => g.Agency).Where(a => a.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a);
            litAgencies.Text = string.Concat(names.Select(a => "<option value=\"" + H(a) + "\"></option>"));
        }

        // ── การกระทำ ─────────────────────────────────────────────────────────

        protected void btnAct_Click(object sender, EventArgs e)
        {
            string act = hidAct.Value, key = hidKey.Value;
            hidAct.Value = hidKey.Value = "";
            string agency, roomType;
            if (!DecodeKey(key, out agency, out roomType)) { LoadAll(false); return; }

            if (act == "del") { Delete(agency, roomType); return; }

            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                LoadSchema(con);
                FillRoomList();
                var g = act == "edit" ? LoadGroups(con).FirstOrDefault(x => x.Agency == agency && x.RoomType == roomType) : null;
                txtAgency.Text = agency;
                txtRoomType.Text = roomType;
                hidOrig.Value = g != null ? key : "";
                foreach (ListItem li in cblRooms.Items) li.Selected = g != null && g.Ids.Contains(int.Parse(li.Value));
                string bf = g?.Breakfast;
                rblBreakfast.SelectedValue = bf == OtaRoomMap.BfIncluded ? "INCLUDED" : bf == OtaRoomMap.BfExcluded ? "EXCLUDED" : "";
                txtNote.Text = g?.Note ?? "";
                litFormTitle.Text = g != null ? "แก้ไข mapping" : "ตั้ง mapping ใหม่";
            }
            LoadAll(false);
            ScrollTo("omForm");
        }

        protected void btnNew_Click(object sender, EventArgs e)
        {
            ClearForm();
            LoadAll(false);
            ScrollTo("omForm");
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            ClearForm();
            LoadAll(false);
        }

        private void ClearForm()
        {
            txtAgency.Text = txtRoomType.Text = txtNote.Text = hidOrig.Value = "";
            rblBreakfast.SelectedValue = "";
            foreach (ListItem li in cblRooms.Items) li.Selected = false;
            litFormTitle.Text = "ตั้ง mapping ใหม่";
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            string agency = (txtAgency.Text ?? "").Trim();
            string roomType = (txtRoomType.Text ?? "").Trim();
            string note = (txtNote.Text ?? "").Trim();
            string bf = OtaRoomMap.NormalizeBreakfast(rblBreakfast.SelectedValue);
            var ids = cblRooms.Items.Cast<ListItem>().Where(li => li.Selected).Select(li => int.Parse(li.Value)).Distinct().ToList();
            string origAgency = null, origRoomType = null;
            bool editing = DecodeKey(hidOrig.Value, out origAgency, out origRoomType);

            if (agency.Length == 0 || roomType.Length == 0) { Alert("err", "กรอกช่องทาง และชื่อห้องที่ OTA ส่งมา (ทั้งชื่อเต็ม)"); LoadAll(false); return; }
            if (ids.Count == 0) { Alert("err", "ติ๊กห้องที่จะจัดให้อย่างน้อย 1 ห้อง"); LoadAll(false); return; }
            if (note.Length > 400) note = note.Substring(0, 400);

            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    LoadSchema(con);
                    var groups = LoadGroups(con);
                    var dup = groups.FirstOrDefault(g => !(editing && g.Agency == origAgency && g.RoomType == origRoomType)
                        && OtaRoomMap.NormChannel(g.Agency) == OtaRoomMap.NormChannel(agency) && OtaRoomMap.RoomTypeEquals(g.RoomType, roomType));
                    if (dup != null)
                    {
                        Alert("err", "มี mapping ชื่อนี้อยู่แล้ว (" + dup.Agency + " | " + dup.RoomType + ") — กด \"แก้ไข\" ที่แถวนั้นแทน");
                        LoadAll(false);
                        return;
                    }
                    var mixed = OtaRoomMap.MixedTypes(ids.Where(_rooms.ContainsKey).Select(i => _rooms[i].Type));

                    using (var tx = con.BeginTransaction())
                    {
                        if (editing)
                        {
                            // ห้องที่ไม่ได้ติ๊กแล้ว → ลบ · ห้องที่ยังติ๊ก → คงแถวเดิม (คอลัมน์อื่นของแถวไม่แตะ) แล้วเปลี่ยนชื่อ/ช่องทางถ้าแก้
                            Exec(con, tx, "DELETE FROM MapDataWithSTAAH WHERE ISNULL(Agency,N'') = @oa AND ISNULL(ROOM_TYPE,N'') = @or" +
                                 (ids.Count > 0 ? " AND (Accommodation_ID IS NULL OR Accommodation_ID NOT IN (" + string.Join(",", ids) + "))" : ""),
                                 P("@oa", origAgency), P("@or", origRoomType));
                            Exec(con, tx, "UPDATE MapDataWithSTAAH SET Agency = @a, ROOM_TYPE = @r WHERE ISNULL(Agency,N'') = @oa AND ISNULL(ROOM_TYPE,N'') = @or",
                                 P("@a", agency), P("@r", roomType), P("@oa", origAgency), P("@or", origRoomType));
                        }
                        var have = new HashSet<int>();
                        using (var cmd = new SqlCommand("SELECT Accommodation_ID FROM MapDataWithSTAAH WHERE Agency = @a AND ROOM_TYPE = @r AND Accommodation_ID IS NOT NULL", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@a", agency); cmd.Parameters.AddWithValue("@r", roomType);
                            using (var rd = cmd.ExecuteReader()) while (rd.Read()) have.Add(Convert.ToInt32(rd[0]));
                        }
                        string insCols = "Agency, ROOM_TYPE, Accommodation_ID" + (_noteCol.Length > 0 ? ", [" + _noteCol + "]" : "") + (_hasBf ? ", Breakfast" : "");
                        string insVals = "@a, @r, @id" + (_noteCol.Length > 0 ? ", @n" : "") + (_hasBf ? ", @bf" : "");
                        foreach (int id in ids.Where(i => !have.Contains(i)))
                            Exec(con, tx, "INSERT INTO MapDataWithSTAAH (" + insCols + ") VALUES (" + insVals + ")",
                                 P("@a", agency), P("@r", roomType), P("@id", id), P("@n", note), P("@bf", (object)bf ?? DBNull.Value));
                        string set = (_noteCol.Length > 0 ? "[" + _noteCol + "] = @n" : "") + (_hasBf ? (_noteCol.Length > 0 ? ", " : "") + "Breakfast = @bf" : "");
                        if (set.Length > 0)
                            Exec(con, tx, "UPDATE MapDataWithSTAAH SET " + set + " WHERE Agency = @a AND ROOM_TYPE = @r",
                                 P("@a", agency), P("@r", roomType), P("@n", note), P("@bf", (object)bf ?? DBNull.Value));
                        tx.Commit();
                    }
                    try
                    {
                        new code().Logs(_conn, "OtaRoomMapping", (editing ? "แก้ " : "เพิ่ม ") + agency + " | " + roomType + " → ห้อง " + string.Join(",", ids)
                            + " อาหารเช้า=" + (bf ?? "ไม่ระบุ") + (editing && (origAgency != agency || origRoomType != roomType) ? " (เดิม " + origAgency + " | " + origRoomType + ")" : ""),
                            Session["User"]?.ToString());
                    }
                    catch { }
                    Alert(mixed.Count > 0 || bf == null ? "warn" : "ok",
                        "บันทึกแล้ว: " + agency + " | " + roomType + " → " + ids.Count + " ห้อง · " + OtaRoomMap.BreakfastLabel(bf)
                        + (mixed.Count > 0 ? " — ⚠ ห้องปนหลายประเภท (" + string.Join(", ", mixed) + ")" : "")
                        + (bf == null ? " — ⚠ ยังไม่ระบุอาหารเช้า" : "")
                        + ". อีเมลที่ค้างเพราะไม่มี mapping จะถูกลองใหม่เองในรอบถัดไป (ภายใน 72 ชม.)");
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                Alert("err", "บันทึกไม่สำเร็จ: " + ex.Message);
            }
            LoadAll(false);
        }

        private void Delete(string agency, string roomType)
        {
            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    int n;
                    using (var cmd = new SqlCommand("DELETE FROM MapDataWithSTAAH WHERE ISNULL(Agency,N'') = @a AND ISNULL(ROOM_TYPE,N'') = @r", con))
                    {
                        cmd.Parameters.AddWithValue("@a", agency); cmd.Parameters.AddWithValue("@r", roomType);
                        n = cmd.ExecuteNonQuery();
                    }
                    try { new code().Logs(_conn, "OtaRoomMapping", "ลบ " + agency + " | " + roomType + " (" + n + " แถว)", Session["User"]?.ToString()); } catch { }
                    Alert("ok", "ลบ mapping แล้ว " + n + " แถว");
                }
            }
            catch (Exception ex) { Alert("err", "ลบไม่สำเร็จ: " + ex.Message); }
            LoadAll(false);
        }

        protected void btnStrict_Click(object sender, EventArgs e)
        {
            string v = btnStrict.CommandArgument == "0" ? "0" : "1";
            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    Exec(con, null, @"IF EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_MapStrict')
                                         UPDATE Accounting_Integration_Config SET ConfigValue = @v WHERE ConfigKey = 'Email_Rsv_MapStrict'
                                      ELSE INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Email_Rsv_MapStrict', @v)", P("@v", v));
                }
                try { new code().Logs(_conn, "OtaRoomMapping", "Email_Rsv_MapStrict = " + v, Session["User"]?.ToString()); } catch { }
                Alert(v == "1" ? "ok" : "warn", v == "1" ? "เปิดโหมดเข้มแล้ว" : "ปิดโหมดเข้มแล้ว — ระบบจะจับคู่แบบหลวมรุ่นเดิม");
            }
            catch (Exception ex) { Alert("err", "บันทึกไม่สำเร็จ: " + ex.Message); }
            LoadAll(false);
        }

        protected void btnTest_Click(object sender, EventArgs e)
        {
            DateTime ci;
            if (!DateTime.TryParse(txtTestIn.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out ci)) ci = DateTime.Today;
            string ch = (txtTestAgency.Text ?? "").Trim(), rt = (txtTestRoomType.Text ?? "").Trim();
            if (rt.Length == 0) litTest.Text = "<div class=\"om-alert err\">กรอกชื่อห้องที่ OTA ส่งมา (ทั้งชื่อเต็ม)</div>";
            else
            {
                string rep = new EmailReservationService(_conn).Diagnose(ch, rt, ci, ci.AddDays(1), 1, 1);
                litTest.Text = "<div class=\"om-alert " + (rep.StartsWith("✅") ? "ok" : "warn") + "\" style=\"white-space:pre-wrap\">" + H(rep) + "</div>";
            }
            LoadAll(false);
            ScrollTo("omTest");
        }

        protected void btnFilter_Click(object sender, EventArgs e) => LoadAll(false);

        // ── ตัวช่วย ──────────────────────────────────────────────────────────

        private static SqlParameter P(string n, object v) => new SqlParameter(n, v ?? DBNull.Value);

        private static void Exec(SqlConnection con, SqlTransaction tx, string sql, params SqlParameter[] ps)
        {
            using (var cmd = new SqlCommand(sql, con, tx))
            {
                foreach (var p in ps) if (sql.Contains(p.ParameterName)) cmd.Parameters.Add(p);
                cmd.ExecuteNonQuery();
            }
        }

        private static string EncodeKey(string agency, string roomType) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes((agency ?? "") + KeySep + (roomType ?? "")));

        private static bool DecodeKey(string key, out string agency, out string roomType)
        {
            agency = roomType = null;
            if (string.IsNullOrEmpty(key)) return false;
            try
            {
                string s = Encoding.UTF8.GetString(Convert.FromBase64String(key));
                int i = s.IndexOf(KeySep);
                if (i < 0) return false;
                agency = s.Substring(0, i); roomType = s.Substring(i + 1);
                return true;
            }
            catch { return false; }
        }

        private static string H(string s) => HttpUtility.HtmlEncode(s ?? "");
        private static string JsStr(string s) => "'" + HttpUtility.JavaScriptStringEncode(s ?? "") + "'";

        private void Alert(string cls, string msg) =>
            litAlert.Text = "<div class=\"om-alert " + cls + "\">" + H(msg) + "</div>";

        private void ScrollTo(string id) =>
            ClientScript.RegisterStartupScript(GetType(), "omScroll",
                "setTimeout(function(){var e=document.getElementById('" + id + "');if(e)e.scrollIntoView({behavior:'smooth',block:'start'});},50);", true);
    }
}
