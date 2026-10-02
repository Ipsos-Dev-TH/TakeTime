using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Mobile
{
    /// <summary>
    /// หน้าอนุมัติ/ปฏิเสธใบลาสำหรับมือถือ — หัวหน้ากดจากลิงก์ในแชท LINE ได้ทันที
    /// ตรวจสิทธิ์จริงด้วย LeaveService.CanApproveLeaveFor (ลิงก์ที่ถูกส่งต่อก็กดแทนกันไม่ได้)
    /// </summary>
    public partial class LeaveApprove : Page
    {
        private readonly string _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private readonly code _code = new code();
        private LeaveService _leave;
        private short _adminId;

        private long RequestId
        {
            get
            {
                if (ViewState["ReqId"] != null) return Convert.ToInt64(ViewState["ReqId"]);
                long.TryParse(Request.QueryString["id"], out long v);
                return v;
            }
            set { ViewState["ReqId"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            int id = MobileAuth.RequireAdmin(this, _conn);
            if (id <= 0) return;              // กำลัง redirect ไป LINE Login
            _adminId = (short)id;
            _leave = new LeaveService();

            if (!IsPostBack)
            {
                litWho.Text = Server.HtmlEncode(Session["UserName"]?.ToString() ?? "");
                RequestId = RequestId;        // ตรึงค่าจาก query ลง ViewState
                LoadRequest();
                LoadPending();
            }
        }

        // ── รายละเอียดใบลา ────────────────────────────────────────────────────────
        private void LoadRequest()
        {
            if (RequestId <= 0)
            {
                Msg("ไม่ได้ระบุใบลา — เลือกจากรายการด้านล่างได้เลย", false);
                return;
            }

            DataRow r = GetRequest(RequestId);
            if (r == null) { Msg("ไม่พบใบลานี้", false); return; }

            short empId = Convert.ToInt16(r["Admin_ID"]);
            if (empId != _adminId && !_leave.CanApproveLeaveFor(_adminId, empId))
            {
                Msg("คุณไม่มีสิทธิ์อนุมัติใบลาของพนักงานคนนี้", false);
                return;
            }
            if (empId == _adminId)
            {
                Msg("ไม่สามารถอนุมัติใบลาของตัวเองได้", false);
                return;
            }

            string status = r["Status"]?.ToString() ?? "";
            litStatus.Text = status == "APPROVED" ? "<span class='st s-ap'>อนุมัติแล้ว</span>"
                           : status == "REJECTED" ? "<span class='st s-rj'>ไม่อนุมัติ</span>"
                           : status == "PENDING" ? "<span class='st s-pd'>รออนุมัติ</span>" : "";

            DateTime s = Convert.ToDateTime(r["StartDate"]);
            DateTime e2 = Convert.ToDateTime(r["EndDate"]);
            string range = s.Date == e2.Date ? s.ToString("dd/MM/yyyy") : $"{s:dd/MM/yyyy} – {e2:dd/MM/yyyy}";
            if (r.Table.Columns.Contains("IsHalfDay") && ToBool(r["IsHalfDay"]))
                range += r["HalfDayPeriod"]?.ToString() == "AFTERNOON" ? " (ครึ่งบ่าย)" : " (ครึ่งเช้า)";

            var sb = new StringBuilder();
            sb.Append(Kv("ผู้ขอลา", $"<span class='big'>{Server.HtmlEncode(r["EmployeeName"]?.ToString())}</span>"));
            sb.Append(Kv("ตำแหน่ง", Server.HtmlEncode(Str(r, "EmployeePosition"))));
            sb.Append(Kv("ประเภท", Server.HtmlEncode(Str(r, "LeaveTypeName"))));
            sb.Append(Kv("วันที่ลา", $"<span class='big'>{range}</span>"));
            sb.Append(Kv("จำนวน", $"{Dec(r, "TotalDays"):0.#} วัน"));
            sb.Append(Kv("เหตุผล", Server.HtmlEncode(Str(r, "Reason"))));
            sb.Append(Kv("ยื่นเมื่อ", r["CreatedDate"] != DBNull.Value
                ? Convert.ToDateTime(r["CreatedDate"]).ToString("dd/MM/yyyy HH:mm") : "-"));
            sb.Append(Kv("เลขที่", Server.HtmlEncode(Str(r, "RequestNumber"))));

            string doc = Str(r, "MedicalCertPath");
            if (!string.IsNullOrWhiteSpace(doc))
                sb.Append(Kv("เอกสารแนบ",
                    $"<a class='doc-link' href='{Server.HtmlEncode(doc)}' target='_blank'>" +
                    "<i class='fas fa-paperclip'></i> เปิดดูเอกสาร</a>"));

            if (status == "REJECTED" && !string.IsNullOrWhiteSpace(Str(r, "RejectedReason")))
                sb.Append(Kv("เหตุผลที่ไม่อนุมัติ", Server.HtmlEncode(Str(r, "RejectedReason"))));

            litDetail.Text = sb.ToString();
            pnlDetail.Visible = true;

            // เตือนสิ่งที่ควรรู้ก่อนตัดสิน
            var warns = new StringBuilder();
            if (ToBool(r["DeductSalary"]))
                warns.Append($"<div class='warn-box'><i class='fas fa-money-bill-wave'></i> " +
                             $"ลานี้<b>หักเงิน {Dec(r, "DeductionAmount"):N2} บาท</b></div>");

            int overlap = CountOverlap(empId, s, e2, RequestId);
            if (overlap > 0)
                warns.Append($"<div class='warn-box'><i class='fas fa-users'></i> " +
                             $"ช่วงวันเดียวกันมีพนักงานคนอื่น<b>ลาอยู่แล้ว {overlap} คน</b> — ตรวจสอบกำลังคนก่อนอนุมัติ</div>");

            litWarn.Text = warns.ToString();

            // ตัดสินได้เฉพาะใบที่ยังรออนุมัติ
            pnlActions.Visible = status == "PENDING";
            if (status != "PENDING" && status != "")
                Msg($"ใบลานี้ถูก{(status == "APPROVED" ? "อนุมัติ" : status == "REJECTED" ? "ปฏิเสธ" : "ปิด")}ไปแล้ว", true);
        }

        private void LoadPending()
        {
            try
            {
                DataTable dt = _leave.GetPendingLeaveRequestsForSupervisor(_adminId);
                if (dt == null || dt.Rows.Count == 0) { pnlPending.Visible = false; return; }

                var sb = new StringBuilder();
                int n = 0;
                foreach (DataRow r in dt.Rows)
                {
                    long id = Convert.ToInt64(r["ID"]);
                    if (id == RequestId) continue;
                    if (n++ >= 10) break;
                    DateTime s = Convert.ToDateTime(r["StartDate"]);
                    DateTime e2 = Convert.ToDateTime(r["EndDate"]);
                    string range = s.Date == e2.Date ? s.ToString("dd/MM") : $"{s:dd/MM}-{e2:dd/MM}";
                    sb.Append($"<div class='pending-item'><b>{Server.HtmlEncode(Str(r, "EmployeeName"))}</b> · " +
                              $"{Server.HtmlEncode(Str(r, "LeaveTypeName"))}" +
                              $"<small>{range} · {Dec(r, "TotalDays"):0.#} วัน</small>" +
                              $"<a class='doc-link' href='LeaveApprove?id={id}'>" +
                              "<i class='fas fa-arrow-right'></i> เปิดพิจารณา</a></div>");
                }
                if (n > 0) { litPending.Text = sb.ToString(); pnlPending.Visible = true; }
            }
            catch { pnlPending.Visible = false; }
        }

        // ── การตัดสิน ─────────────────────────────────────────────────────────────
        protected void btnApprove_Click(object sender, EventArgs e)
        {
            if (!EnsureCanDecide(out DataRow r)) return;
            try
            {
                bool ok = _leave.ApproveLeaveRequest(RequestId, _adminId);
                if (!ok) { Msg("อนุมัติไม่สำเร็จ กรุณาลองใหม่", false); return; }

                NotifyResult(true, null);
                Msg("อนุมัติใบลาเรียบร้อย — แจ้งผู้ขอลาทาง LINE แล้ว", true);
            }
            catch (Exception ex) { Msg("เกิดข้อผิดพลาด: " + ex.Message, false); }
            Refresh();
        }

        protected void btnShowReject_Click(object sender, EventArgs e)
        {
            pnlReject.Visible = true;
            pnlActions.Visible = false;
        }

        protected void btnCancelReject_Click(object sender, EventArgs e)
        {
            pnlReject.Visible = false;
            pnlActions.Visible = true;
        }

        protected void btnReject_Click(object sender, EventArgs e)
        {
            string reason = txtReject.Text.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                Msg("กรุณาระบุเหตุผลที่ไม่อนุมัติ เพื่อแจ้งกลับผู้ขอลา", false);
                pnlReject.Visible = true;
                return;
            }
            if (!EnsureCanDecide(out DataRow r)) return;

            try
            {
                bool ok = _leave.RejectLeaveRequest(RequestId, reason, _adminId);
                if (!ok) { Msg("บันทึกการไม่อนุมัติไม่สำเร็จ", false); return; }

                NotifyResult(false, reason);
                Msg("บันทึกการไม่อนุมัติแล้ว — แจ้งผู้ขอลาทาง LINE พร้อมเหตุผลแล้ว", true);
                txtReject.Text = "";
                pnlReject.Visible = false;
            }
            catch (Exception ex) { Msg("เกิดข้อผิดพลาด: " + ex.Message, false); }
            Refresh();
        }

        /// <summary>ตรวจซ้ำก่อนตัดสินจริง — กันกดค้าง/กดซ้ำหลังมีคนตัดสินไปแล้ว</summary>
        private bool EnsureCanDecide(out DataRow r)
        {
            r = GetRequest(RequestId);
            if (r == null) { Msg("ไม่พบใบลานี้", false); return false; }

            short empId = Convert.ToInt16(r["Admin_ID"]);
            if (empId == _adminId) { Msg("ไม่สามารถอนุมัติใบลาของตัวเองได้", false); return false; }
            if (!_leave.CanApproveLeaveFor(_adminId, empId))
            { Msg("คุณไม่มีสิทธิ์อนุมัติใบลาของพนักงานคนนี้", false); return false; }

            string status = r["Status"]?.ToString() ?? "";
            if (status != "PENDING")
            {
                Msg($"ใบลานี้ถูกดำเนินการไปแล้ว ({status})", false);
                pnlActions.Visible = false;
                pnlReject.Visible = false;
                return false;
            }
            return true;
        }

        private void NotifyResult(bool approved, string reason)
        {
            try
            {
                new LeaveLineNotifier(_conn).NotifyDecision(
                    RequestId, approved, reason, Session["UserName"]?.ToString());
            }
            catch { /* แจ้งเตือนล้มเหลวต้องไม่ทำให้การอนุมัติล้มเหลว */ }
        }

        private void Refresh()
        {
            litDetail.Text = ""; litWarn.Text = "";
            pnlDetail.Visible = false; pnlActions.Visible = false;
            LoadRequest();
            LoadPending();
        }

        // ── data ──────────────────────────────────────────────────────────────────
        private DataRow GetRequest(long id)
        {
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT LR.ID, LR.RequestNumber, LR.Admin_ID, LR.StartDate, LR.EndDate, LR.TotalDays,
                         LR.Reason, LR.Status, LR.DeductSalary, LR.DeductionAmount, LR.MedicalCertPath,
                         LR.RejectedReason, LR.CreatedDate, LR.IsHalfDay, LR.HalfDayPeriod,
                         LT.LeaveTypeName,
                         ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                         A.Role AS EmployeePosition
                    FROM Leave_Requests LR
                    INNER JOIN Leave_Types LT ON LT.ID = LR.LeaveType_ID
                    INNER JOIN [dbo].[Admin] A ON A.ID = LR.Admin_ID
                   WHERE LR.ID = @id",
                new Dictionary<string, object> { { "@id", id } });
            return dt?.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        /// <summary>คนอื่นที่ลาทับช่วงเดียวกัน (อนุมัติแล้ว) — ช่วยหัวหน้าตัดสินใจเรื่องกำลังคน</summary>
        private int CountOverlap(short empId, DateTime start, DateTime end, long excludeId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT COUNT(*) AS Cnt FROM Leave_Requests
                       WHERE Status = 'APPROVED' AND Admin_ID <> @emp AND ID <> @ex
                         AND StartDate <= @e AND EndDate >= @s",
                    new Dictionary<string, object>
                    { { "@emp", empId }, { "@ex", excludeId }, { "@s", start.Date }, { "@e", end.Date } });
                return dt?.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["Cnt"]) : 0;
            }
            catch { return 0; }
        }

        // ── helpers ───────────────────────────────────────────────────────────────
        private static string Kv(string k, string v) =>
            $"<div class='kv'><div class='k'>{k}</div><div class='v'>{v}</div></div>";

        private void Msg(string text, bool ok)
        {
            pnlMsg.Visible = true;
            divMsg.Attributes["class"] = "msg " + (ok ? "ok" : "err");
            litMsg.Text = $"<i class='fas {(ok ? "fa-circle-check" : "fa-circle-exclamation")}'></i> {Server.HtmlEncode(text)}";
        }

        private static string Str(DataRow r, string col) =>
            r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? r[col].ToString() : "";

        private static decimal Dec(DataRow r, string col)
        {
            if (!r.Table.Columns.Contains(col) || r[col] == DBNull.Value) return 0m;
            return decimal.TryParse(r[col].ToString(), out var v) ? v : 0m;
        }

        private static bool ToBool(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            if (v is bool b) return b;
            string s = v.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
