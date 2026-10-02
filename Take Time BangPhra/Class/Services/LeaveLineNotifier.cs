using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// แจ้งเตือนขั้นตอนการลาผ่าน LINE ส่วนตัว (ใช้ userId ที่ผูกไว้ด้วย LINE Login)
    ///  • พนักงานยื่นใบลา → ส่งหาหัวหน้าพร้อมลิงก์กดอนุมัติ/ปฏิเสธจากมือถือ
    ///  • หัวหน้าตัดสิน → ส่งผลกลับหาพนักงาน (พร้อมเหตุผลถ้าถูกปฏิเสธ)
    /// ทุกอย่างทำแบบ best-effort — ส่งไม่ได้ต้องไม่ทำให้การยื่น/อนุมัติล้มเหลว
    /// </summary>
    public class LeaveLineNotifier
    {
        private readonly string _conn;
        private readonly code _code = new code();
        private readonly LineLoginService _line;

        public LeaveLineNotifier(string connectionString)
        {
            _conn = connectionString;
            _line = new LineLoginService(connectionString);
        }

        private string BaseUrl
        {
            get
            {
                try
                {
                    var req = System.Web.HttpContext.Current?.Request;
                    if (req != null) return $"{req.Url.Scheme}://{req.Url.Authority}";
                }
                catch { }
                return "https://taketimebangphra.com";
            }
        }

        /// <summary>ส่งใบลาใหม่ให้หัวหน้าทุกคนของพนักงานคนนี้ พร้อมลิงก์อนุมัติ</summary>
        public (int Sent, string Detail) NotifyNewRequest(long requestId)
        {
            try
            {
                var r = GetRequest(requestId);
                if (r == null) return (0, "ไม่พบใบลา");

                var supervisors = _code.DatabaseQuerySafe(_conn,
                    @"SELECT DISTINCT A.ID, A.Username, A.Line_UserId
                        FROM Employee_Supervisor ES
                        INNER JOIN [dbo].[Admin] A ON A.ID = ES.Supervisor_AdminID
                       WHERE ES.Employee_AdminID = @emp AND A.Status = 1
                         AND A.Line_UserId IS NOT NULL AND A.Line_NotifyEnabled = 1",
                    new Dictionary<string, object> { { "@emp", Convert.ToInt32(r["Admin_ID"]) } });

                if (supervisors == null || supervisors.Rows.Count == 0)
                    return (0, "ไม่มีหัวหน้าที่ผูกบัญชี LINE ไว้");

                string url = $"{BaseUrl}/Mobile/LeaveApprove?id={requestId}";
                var sb = new StringBuilder();
                sb.AppendLine("📋 มีใบลารออนุมัติ");
                sb.AppendLine();
                sb.AppendLine($"ผู้ขอลา: {r["EmployeeName"]}");
                sb.AppendLine($"ประเภท: {r["LeaveTypeName"]}");
                sb.AppendLine($"วันที่: {FmtRange(r)}");
                sb.AppendLine($"จำนวน: {Convert.ToDecimal(r["TotalDays"]):0.#} วัน");
                sb.AppendLine($"เหตุผล: {Trim(r["Reason"]?.ToString(), 200)}");
                if (r["DeductSalary"] != DBNull.Value && ToBool(r["DeductSalary"]))
                    sb.AppendLine($"⚠️ ลานี้หักเงิน {Convert.ToDecimal(r["DeductionAmount"]):N2} บาท");
                sb.AppendLine();
                sb.AppendLine("👉 กดอนุมัติ/ปฏิเสธที่นี่:");
                sb.Append(url);

                int sent = 0;
                var errs = new List<string>();
                foreach (DataRow s in supervisors.Rows)
                {
                    var (ok, msg) = _line.PushText(s["Line_UserId"].ToString(), sb.ToString());
                    if (ok) sent++;
                    else errs.Add($"{s["Username"]}: {msg}");
                }

                _code.Logs(_conn, "LeaveLine",
                    $"แจ้งใบลา #{requestId} ({r["RequestNumber"]}) ให้หัวหน้า {sent} คน" +
                    (errs.Count > 0 ? " | ล้มเหลว: " + string.Join("; ", errs) : ""), "SYSTEM");
                return (sent, string.Join("; ", errs));
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "LeaveLine", $"NotifyNewRequest({requestId}) error: {ex.Message}", "SYSTEM");
                return (0, ex.Message);
            }
        }

        /// <summary>แจ้งผลการพิจารณากลับไปหาผู้ขอลา</summary>
        public (bool Sent, string Detail) NotifyDecision(long requestId, bool approved, string reason, string deciderName)
        {
            try
            {
                var r = GetRequest(requestId);
                if (r == null) return (false, "ไม่พบใบลา");

                var emp = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 Line_UserId, Line_NotifyEnabled FROM [dbo].[Admin]
                       WHERE ID = @id AND Status = 1",
                    new Dictionary<string, object> { { "@id", Convert.ToInt32(r["Admin_ID"]) } });
                if (emp == null || emp.Rows.Count == 0
                    || emp.Rows[0]["Line_UserId"] == DBNull.Value
                    || string.IsNullOrWhiteSpace(emp.Rows[0]["Line_UserId"].ToString()))
                    return (false, "ผู้ขอลายังไม่ได้ผูกบัญชี LINE");
                if (emp.Rows[0]["Line_NotifyEnabled"] != DBNull.Value && !ToBool(emp.Rows[0]["Line_NotifyEnabled"]))
                    return (false, "ผู้ขอลาปิดรับแจ้งเตือน");

                var sb = new StringBuilder();
                sb.AppendLine(approved ? "✅ ใบลาได้รับการอนุมัติ" : "❌ ใบลาไม่ได้รับการอนุมัติ");
                sb.AppendLine();
                sb.AppendLine($"เลขที่: {r["RequestNumber"]}");
                sb.AppendLine($"ประเภท: {r["LeaveTypeName"]}");
                sb.AppendLine($"วันที่: {FmtRange(r)} ({Convert.ToDecimal(r["TotalDays"]):0.#} วัน)");
                if (!string.IsNullOrWhiteSpace(deciderName))
                    sb.AppendLine($"โดย: {deciderName}");
                if (!approved && !string.IsNullOrWhiteSpace(reason))
                {
                    sb.AppendLine();
                    sb.AppendLine($"เหตุผล: {Trim(reason, 400)}");
                }
                sb.AppendLine();
                sb.Append($"ดูรายละเอียด: {BaseUrl}/Mobile/Leave");

                var (ok, msg) = _line.PushText(emp.Rows[0]["Line_UserId"].ToString(), sb.ToString());
                _code.Logs(_conn, "LeaveLine",
                    $"แจ้งผลใบลา #{requestId} ({(approved ? "อนุมัติ" : "ปฏิเสธ")}) → ผู้ขอลา: {(ok ? "สำเร็จ" : msg)}", "SYSTEM");
                return (ok, msg);
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "LeaveLine", $"NotifyDecision({requestId}) error: {ex.Message}", "SYSTEM");
                return (false, ex.Message);
            }
        }

        /// <summary>ส่งลิงก์หน้าขอลาให้พนักงาน (ใช้ปุ่ม "ส่งลิงก์ขอลาทาง LINE" ในหน้า Admin)</summary>
        public (bool Sent, string Detail) SendRequestLink(int adminId)
        {
            string text = "📝 ยื่นใบลาออนไลน์\n\nกดลิงก์ด้านล่างเพื่อกรอกใบลา (เข้าระบบด้วย LINE อัตโนมัติ)\n"
                        + $"{BaseUrl}/Mobile/Leave";
            return _line.SendToAdmin(adminId, text);
        }

        // ── helpers ───────────────────────────────────────────────────────────────
        private DataRow GetRequest(long requestId)
        {
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT LR.ID, LR.RequestNumber, LR.Admin_ID, LR.StartDate, LR.EndDate,
                         LR.TotalDays, LR.Reason, LR.Status, LR.DeductSalary, LR.DeductionAmount,
                         LR.IsHalfDay, LR.HalfDayPeriod,
                         LT.LeaveTypeName,
                         ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName
                    FROM Leave_Requests LR
                    INNER JOIN Leave_Types LT ON LT.ID = LR.LeaveType_ID
                    INNER JOIN [dbo].[Admin] A ON A.ID = LR.Admin_ID
                   WHERE LR.ID = @id",
                new Dictionary<string, object> { { "@id", requestId } });
            return dt?.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        private static string FmtRange(DataRow r)
        {
            DateTime s = Convert.ToDateTime(r["StartDate"]);
            DateTime e = Convert.ToDateTime(r["EndDate"]);
            string range = s.Date == e.Date ? s.ToString("dd/MM/yyyy") : $"{s:dd/MM/yyyy} - {e:dd/MM/yyyy}";
            if (r.Table.Columns.Contains("IsHalfDay") && r["IsHalfDay"] != DBNull.Value && ToBool(r["IsHalfDay"]))
            {
                string period = r["HalfDayPeriod"]?.ToString() == "AFTERNOON" ? "ครึ่งบ่าย" : "ครึ่งเช้า";
                range += $" ({period})";
            }
            return range;
        }

        private static string Trim(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length > max ? s.Substring(0, max) + "..." : s);

        private static bool ToBool(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            if (v is bool b) return b;
            string s = v.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
