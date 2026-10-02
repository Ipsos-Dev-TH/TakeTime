using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Text;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Mobile
{
    /// <summary>
    /// หน้ายื่นใบลาสำหรับมือถือ — เปิดจากลิงก์ในแชท LINE ได้เลย
    /// (ยังไม่ล็อกอิน → เข้าสู่ระบบด้วย LINE อัตโนมัติผ่าน Line_UserId ที่ผูกไว้)
    /// ใช้ LeaveService เดิมทั้งหมด ไม่มี logic ลาซ้ำซ้อน
    /// </summary>
    public partial class LeaveMobile : Page
    {
        private readonly string _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private readonly code _code = new code();
        private LeaveService _leave;
        private short _adminId;

        protected void Page_Load(object sender, EventArgs e)
        {
            int id = MobileAuth.RequireAdmin(this, _conn);
            if (id <= 0) return;              // กำลัง redirect ไป LINE Login
            _adminId = (short)id;
            _leave = new LeaveService();

            if (!IsPostBack)
            {
                litWho.Text = Server.HtmlEncode(Session["UserName"]?.ToString() ?? "");
                txtStart.Text = DateTime.Today.ToString("yyyy-MM-dd");
                txtEnd.Text = DateTime.Today.ToString("yyyy-MM-dd");
                txtHalfDate.Text = DateTime.Today.ToString("yyyy-MM-dd");
                LoadTypes();
                LoadQuota();
                LoadHistory();
            }
        }

        // ── ตัวเลือกประเภทการลา (ปุ่มใหญ่กดง่าย) ──────────────────────────────────
        private void LoadTypes()
        {
            var sb = new StringBuilder();
            try
            {
                DataTable dt = _leave.GetLeaveTypes();
                bool first = true;
                foreach (DataRow r in dt.Rows)
                {
                    string id = r["ID"].ToString();
                    string name = r["LeaveTypeName"]?.ToString() ?? "";
                    string icon = IconFor(r.Table.Columns.Contains("LeaveTypeCode") ? r["LeaveTypeCode"]?.ToString() : "", name);
                    string cls = first ? "type-btn sel" : "type-btn";
                    if (first) { hfLeaveType.Value = id; first = false; }
                    sb.Append($"<div class='{cls}' onclick=\"pickType(this,'{id}')\">" +
                              $"<i class='fas {icon}'></i>{Server.HtmlEncode(name)}</div>");
                }
                if (dt.Rows.Count == 0) sb.Append("<div class='empty'>ยังไม่ได้ตั้งค่าประเภทการลา</div>");
            }
            catch (Exception ex)
            {
                sb.Append($"<div class='empty'>โหลดประเภทการลาไม่ได้: {Server.HtmlEncode(ex.Message)}</div>");
            }
            litTypes.Text = sb.ToString();
        }

        private static string IconFor(string code, string name)
        {
            string s = (code + " " + name).ToLowerInvariant();
            if (s.Contains("ป่วย") || s.Contains("sick")) return "fa-notes-medical";
            if (s.Contains("กิจ") || s.Contains("personal") || s.Contains("business")) return "fa-user-clock";
            if (s.Contains("พักร้อน") || s.Contains("annual") || s.Contains("vacation")) return "fa-umbrella-beach";
            if (s.Contains("คลอด") || s.Contains("maternity")) return "fa-baby";
            if (s.Contains("บวช") || s.Contains("ordination")) return "fa-place-of-worship";
            if (s.Contains("ทหาร") || s.Contains("military")) return "fa-shield-halved";
            return "fa-calendar-day";
        }

        // ── สิทธิ์วันลาคงเหลือ ────────────────────────────────────────────────────
        private void LoadQuota()
        {
            var sb = new StringBuilder();
            try
            {
                DataTable dt = _leave.GetEmployeeLeaveQuota(_adminId);
                if (dt == null || dt.Rows.Count == 0)
                {
                    litQuota.Text = "<div class='empty'>ยังไม่ได้กำหนดสิทธิ์วันลาปีนี้</div>";
                    return;
                }
                foreach (DataRow r in dt.Rows)
                {
                    decimal remain = Col(r, "RemainingDays");
                    decimal total = Col(r, "TotalDays", Col(r, "QuotaDays"));
                    string name = FirstNonEmpty(r, "LeaveTypeName", "LeaveType");
                    string cls = remain <= 0 ? "q-item low" : "q-item";
                    sb.Append($"<div class='{cls}'><div class='n'>{remain:0.#}</div>" +
                              $"<div class='l'>{Server.HtmlEncode(name)}<br/>จาก {total:0.#} วัน</div></div>");
                }
            }
            catch (Exception ex)
            {
                sb.Append($"<div class='empty'>{Server.HtmlEncode(ex.Message)}</div>");
            }
            litQuota.Text = sb.ToString();
        }

        // ── ประวัติใบลา ───────────────────────────────────────────────────────────
        private void LoadHistory()
        {
            var sb = new StringBuilder();
            try
            {
                DataTable dt = _leave.GetLeaveRequests(_adminId);
                if (dt == null || dt.Rows.Count == 0)
                {
                    litHistory.Text = "<div class='empty'>ยังไม่มีประวัติการลา</div>";
                    return;
                }

                int shown = 0;
                foreach (DataRow r in dt.Rows)
                {
                    if (shown++ >= 15) break;
                    string status = r["Status"]?.ToString() ?? "";
                    string cls = status == "APPROVED" ? "item ap"
                               : status == "REJECTED" ? "item rj"
                               : status == "PENDING" ? "item pd" : "item";
                    string badge = status == "APPROVED" ? "<span class='st s-ap'>อนุมัติแล้ว</span>"
                                 : status == "REJECTED" ? "<span class='st s-rj'>ไม่อนุมัติ</span>"
                                 : status == "PENDING" ? "<span class='st s-pd'>รออนุมัติ</span>"
                                 : "<span class='st s-cn'>ยกเลิก</span>";

                    DateTime s = Convert.ToDateTime(r["StartDate"]);
                    DateTime e2 = Convert.ToDateTime(r["EndDate"]);
                    string range = s.Date == e2.Date ? s.ToString("dd/MM/yyyy") : $"{s:dd/MM/yyyy} - {e2:dd/MM/yyyy}";
                    decimal days = Col(r, "TotalDays");

                    sb.Append($"<div class='{cls}'>");
                    sb.Append($"<b>{Server.HtmlEncode(FirstNonEmpty(r, "LeaveTypeName", "LeaveType"))}</b> {badge}");
                    sb.Append($"<small>{range} · {days:0.#} วัน · เลขที่ {Server.HtmlEncode(r["RequestNumber"]?.ToString())}</small>");
                    string reason = r.Table.Columns.Contains("Reason") ? r["Reason"]?.ToString() : "";
                    if (!string.IsNullOrWhiteSpace(reason))
                        sb.Append($"<small>เหตุผล: {Server.HtmlEncode(reason)}</small>");

                    if (status == "REJECTED" && r.Table.Columns.Contains("RejectedReason")
                        && r["RejectedReason"] != DBNull.Value && !string.IsNullOrWhiteSpace(r["RejectedReason"].ToString()))
                        sb.Append($"<div class='reject-note'><b>เหตุผลที่ไม่อนุมัติ:</b> " +
                                  $"{Server.HtmlEncode(r["RejectedReason"].ToString())}</div>");

                    sb.Append("</div>");
                }
            }
            catch (Exception ex)
            {
                sb.Append($"<div class='empty'>{Server.HtmlEncode(ex.Message)}</div>");
            }
            litHistory.Text = sb.ToString();
        }

        // ── ส่งใบลา ───────────────────────────────────────────────────────────────
        protected void btnSubmit_Click(object sender, EventArgs e)
        {
            try
            {
                if (!byte.TryParse(hfLeaveType.Value, out byte leaveTypeId) || leaveTypeId == 0)
                { Msg("กรุณาเลือกประเภทการลา", false); return; }

                if (string.IsNullOrWhiteSpace(txtReason.Text))
                { Msg("กรุณาระบุเหตุผลการลา", false); return; }

                bool isHalf = hfMode.Value == "HALF";
                DateTime start, end;
                decimal totalDays;

                if (isHalf)
                {
                    if (!DateTime.TryParse(txtHalfDate.Text, out start))
                    { Msg("กรุณาเลือกวันที่ลา", false); return; }
                    end = start;
                    totalDays = 0.5m;
                }
                else
                {
                    if (!DateTime.TryParse(txtStart.Text, out start))
                    { Msg("กรุณาเลือกวันที่เริ่มลา", false); return; }
                    if (!DateTime.TryParse(txtEnd.Text, out end)) end = start;
                    if (end < start) { Msg("วันสิ้นสุดต้องไม่ก่อนวันเริ่มลา", false); return; }
                    totalDays = (decimal)(end - start).TotalDays + 1;
                }

                string docPath = SaveAttachment();

                var result = _leave.CreateLeaveRequest(_adminId, leaveTypeId, start, end,
                    totalDays, txtReason.Text.Trim(), docPath, _adminId);

                if (!result.Success) { Msg(result.Message ?? "ส่งใบลาไม่สำเร็จ", false); return; }

                // ครึ่งวัน: LeaveService เดิมไม่มีพารามิเตอร์นี้ — เขียนเพิ่มหลังสร้าง
                if (isHalf)
                {
                    try
                    {
                        _code.DatabaseInsertSafe(_conn,
                            "UPDATE Leave_Requests SET IsHalfDay = 1, HalfDayPeriod = @p WHERE ID = @id",
                            new Dictionary<string, object>
                            { { "@p", hfHalf.Value }, { "@id", result.ID } });
                    }
                    catch { }
                }

                // แจ้งหัวหน้าทาง LINE (ล้มเหลวไม่กระทบการยื่นใบลา)
                string notifyNote = "";
                try
                {
                    var (sent, detail) = new LeaveLineNotifier(_conn).NotifyNewRequest(result.ID);
                    notifyNote = sent > 0
                        ? $" · แจ้งหัวหน้าทาง LINE แล้ว ({sent} คน)"
                        : " · ยังไม่ได้แจ้งทาง LINE (" + (string.IsNullOrEmpty(detail) ? "หัวหน้ายังไม่ผูกบัญชี" : detail) + ")";
                }
                catch { }

                Msg($"ส่งใบลาเรียบร้อย{notifyNote}", true);

                txtReason.Text = "";
                LoadQuota();
                LoadHistory();
            }
            catch (Exception ex)
            {
                Msg("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        private string SaveAttachment()
        {
            if (!fuDoc.HasFile) return null;
            try
            {
                string ext = Path.GetExtension(fuDoc.FileName).ToLowerInvariant();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
                if (Array.IndexOf(allowed, ext) < 0) throw new Exception("รองรับเฉพาะรูปภาพหรือ PDF");
                if (fuDoc.PostedFile.ContentLength > 8 * 1024 * 1024) throw new Exception("ไฟล์ใหญ่เกิน 8 MB");

                string folder = Server.MapPath("~/Images/LeaveDocs");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string fileName = $"leave_{_adminId}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                fuDoc.SaveAs(Path.Combine(folder, fileName));
                return "/Images/LeaveDocs/" + fileName;
            }
            catch (Exception ex)
            {
                Msg("แนบไฟล์ไม่สำเร็จ: " + ex.Message, false);
                return null;
            }
        }

        private void Msg(string text, bool ok)
        {
            pnlMsg.Visible = true;
            divMsg.Attributes["class"] = "msg " + (ok ? "ok" : "err");
            litMsg.Text = $"<i class='fas {(ok ? "fa-circle-check" : "fa-circle-exclamation")}'></i> {Server.HtmlEncode(text)}";
        }

        private static decimal Col(DataRow r, string name, decimal def = 0)
        {
            if (!r.Table.Columns.Contains(name) || r[name] == DBNull.Value) return def;
            return decimal.TryParse(r[name].ToString(), out var v) ? v : def;
        }

        private static string FirstNonEmpty(DataRow r, params string[] cols)
        {
            foreach (var c in cols)
                if (r.Table.Columns.Contains(c) && r[c] != DBNull.Value && !string.IsNullOrWhiteSpace(r[c].ToString()))
                    return r[c].ToString();
            return "-";
        }
    }
}
