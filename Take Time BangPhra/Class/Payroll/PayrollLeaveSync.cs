using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Take_Time_BangPhra.Payroll
{
    /// <summary>ใบลาที่หักเงินหนึ่งใบในรอบเงินเดือน</summary>
    public sealed class PayrollLeaveItem
    {
        public long Id { get; set; }
        public string RequestNumber { get; set; }
        public DateTime StartDate { get; set; }
        public decimal Days { get; set; }
        /// <summary>ใบลาของเดือนก่อนที่รอบเดือนนั้นจ่ายไปแล้ว — ยกมาหักในรอบนี้</summary>
        public bool Carried { get; set; }
    }

    /// <summary>ผลรวมใบลาที่ต้องหักของพนักงานหนึ่งคนในรอบหนึ่ง</summary>
    public sealed class PayrollLeaveCalc
    {
        public List<PayrollLeaveItem> Items { get; } = new List<PayrollLeaveItem>();
        public decimal InMonthDays => Items.Where(i => !i.Carried).Sum(i => i.Days);
        public decimal CarriedDays => Items.Where(i => i.Carried).Sum(i => i.Days);
        public decimal TotalDays => Items.Sum(i => i.Days);
        public decimal Deduction { get; set; }
    }

    /// <summary>
    /// ใบลาหักเงิน ↔ รอบเงินเดือน — ตัวเดียวที่ตัดสินว่าใบลาใบไหนหักในรอบไหน (สร้างรอบ / คำนวณใหม่ / อนุมัติใบลา / ทำงานทดแทน).
    /// <para>เดิมยอดหักลาคิดเฉพาะตอน "สร้างรอบ" กับปุ่ม "คำนวณใหม่" ⇒ ใบลาที่อนุมัติหลังสร้างรอบ (เช่น ลาวันที่ 27 ยื่น/อนุมัติวันที่ 30)
    /// ไม่เข้าเงินเดือนจนกว่าจะมีคนกดคำนวณใหม่ และถ้ารอบเดือนนั้นทำจ่ายแล้วจะไม่ถูกหักเลย.</para>
    /// <para>ตอนนี้: อนุมัติ/ทำงานทดแทน/ยกเลิกทดแทน → <see cref="OnLeaveChanged"/> คำนวณยอดลาของรอบที่ยังไม่ทำจ่ายให้ทันที ·
    /// รอบเดือนของใบลาทำจ่ายแล้ว → ยกไปหักในรอบถัดไปที่ยังไม่ทำจ่าย (ต้องมีคอลัมน์ <c>Leave_Requests.Payroll_Record_ID</c> — PHASE19_43;
    /// ไม่มีคอลัมน์ = หักเฉพาะเดือนของใบลาเหมือนเดิม + หน้าเงินเดือนเตือนให้แก้รายคน).</para>
    /// <para>ยอดหักต่อเดือนใช้สูตรเดิม <see cref="HRConfiguration.CalculateLeaveDeduction"/> (เงินเดือน ÷ วันในเดือน × วันลา) —
    /// ใบที่ยกมาคิดด้วยจำนวนวันของเดือนที่ลาจริง ⇒ ตรงกับยอด "ลานี้หักเงิน" ที่ผู้อนุมัติเห็น.</para>
    /// </summary>
    public static class PayrollLeaveSync
    {
        /// <summary>ยกมาจากเดือนที่จ่ายแล้วได้ไม่เกินกี่เดือน (กันใบเก่ามาก ๆ ถูกหักทีเดียวหลังรัน migration)</summary>
        public const int CarryLookbackMonths = 6;

        private static string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString;

        // ──────────────────────────────────────────────────────────────
        // ส่วนคำนวณ (pure)
        // ──────────────────────────────────────────────────────────────

        /// <summary>ยอดหักลาของรอบ year/month: ใบของเดือนนี้คิดรวมแบบเดิม · ใบที่ยกมาคิดตามจำนวนวันของเดือนที่ลา</summary>
        public static decimal CalcDeduction(decimal baseSalary, IEnumerable<PayrollLeaveItem> items, int year, int month)
        {
            var list = (items ?? Enumerable.Empty<PayrollLeaveItem>()).ToList();
            decimal ded = HRConfiguration.CalculateLeaveDeduction(baseSalary, list.Where(i => !i.Carried).Sum(i => i.Days), DateTime.DaysInMonth(year, month));
            foreach (var g in list.Where(i => i.Carried).GroupBy(i => new { i.StartDate.Year, i.StartDate.Month }))
                ded += HRConfiguration.CalculateLeaveDeduction(baseSalary, g.Sum(i => i.Days), DateTime.DaysInMonth(g.Key.Year, g.Key.Month));
            return ded;
        }

        /// <summary>วันทำงาน = วันในเดือน − วันลาของเดือนนี้ (ใบที่ยกมาไม่ลดวันทำงานของเดือนนี้)</summary>
        public static int WorkDays(int year, int month, decimal inMonthDays)
        {
            int w = DateTime.DaysInMonth(year, month) - (int)Math.Floor(inMonthDays);
            return w < 0 ? 0 : w;
        }

        /// <summary>ประกันสังคมจากรายได้หลังหัก (สูตรเดียวกับปุ่มคำนวณใหม่)</summary>
        public static decimal SocialSecurity(decimal totalEarnings, decimal leaveDeduction, decimal tax, decimal other, int year)
        {
            decimal ssBase = totalEarnings - (leaveDeduction + tax + other);
            if (ssBase < 0) ssBase = 0;
            return HRConfiguration.CalculateSocialSecurityByYear(ssBase, year + 543);
        }

        public static string ThaiMonth(int year, int month)
        {
            string[] m = { "", "ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค." };
            return (month >= 1 && month <= 12 ? m[month] : month.ToString(CultureInfo.InvariantCulture)) + " " + (year + 543).ToString(CultureInfo.InvariantCulture);
        }

        // ──────────────────────────────────────────────────────────────
        // schema
        // ──────────────────────────────────────────────────────────────

        private static bool? _hasClaimCol;
        private static DateTime _hasClaimColAt;

        /// <summary>มีคอลัมน์ Leave_Requests.Payroll_Record_ID (PHASE19_43) — แคช 60 วินาที</summary>
        public static bool HasClaimColumn(SqlConnection conn, SqlTransaction tx)
        {
            if (_hasClaimCol.HasValue && (DateTime.UtcNow - _hasClaimColAt).TotalSeconds < 60) return _hasClaimCol.Value;
            using (var cmd = new SqlCommand("SELECT CASE WHEN COL_LENGTH('dbo.Leave_Requests','Payroll_Record_ID') IS NULL THEN 0 ELSE 1 END", conn, tx))
                _hasClaimCol = Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            _hasClaimColAt = DateTime.UtcNow;
            return _hasClaimCol.Value;
        }

        // ──────────────────────────────────────────────────────────────
        // อ่าน/ผูกใบลากับรอบ
        // ──────────────────────────────────────────────────────────────

        // ใบที่ยังไม่มีรอบไหนรับไป (ไม่มีค่า หรือชี้รอบที่ถูกลบไปแล้ว) — -1 = ใบเก่าก่อน migration ไม่ยกมาหัก
        private const string Unclaimed = "(LR.Payroll_Record_ID IS NULL OR (LR.Payroll_Record_ID > 0 AND NOT EXISTS (SELECT 1 FROM Payroll_Records X WHERE X.ID = LR.Payroll_Record_ID)))";

        /// <summary>
        /// ใบลาที่ต้องหักในรอบ year/month ของพนักงาน: ใบของเดือนนี้ที่ยังไม่มีรอบอื่นรับ + ใบที่รอบนี้รับไว้แล้ว + (มีคอลัมน์) ใบของเดือนก่อน
        /// ไม่เกิน <see cref="CarryLookbackMonths"/> เดือนที่รอบเดือนนั้นทำจ่ายแล้วและยังไม่ถูกหัก. <paramref name="recordId"/> 0 = รอบที่กำลังสร้าง.
        /// </summary>
        public static PayrollLeaveCalc Load(SqlConnection conn, SqlTransaction tx, int adminId, int year, int month, long recordId, decimal baseSalary)
        {
            var calc = new PayrollLeaveCalc();
            bool claim = HasClaimColumn(conn, tx);
            var monthStart = new DateTime(year, month, 1);
            string sql = claim
                ? @"SELECT LR.ID, LR.RequestNumber, LR.StartDate, LR.TotalDays,
                           CASE WHEN LR.StartDate >= @MonthStart AND LR.StartDate < @NextMonth THEN 0 ELSE 1 END AS Carried
                    FROM Leave_Requests LR
                    WHERE LR.Admin_ID = @AdminID AND LR.Status = 'APPROVED'
                      AND ISNULL(LR.DeductSalary, 0) = 1 AND ISNULL(LR.IsReplaced, 0) = 0
                      AND (
                            (LR.StartDate >= @MonthStart AND LR.StartDate < @NextMonth AND (" + Unclaimed + @" OR LR.Payroll_Record_ID IN (@RecordID, -1)))
                         OR (@RecordID > 0 AND LR.Payroll_Record_ID = @RecordID)
                         OR (LR.StartDate < @MonthStart AND LR.StartDate >= @Lookback AND " + Unclaimed + @"
                             AND EXISTS (SELECT 1 FROM Payroll_Records PR
                                         INNER JOIN Payroll_Periods PP ON PP.ID = PR.PayrollPeriod_ID
                                         WHERE PR.Admin_ID = LR.Admin_ID AND PP.[Year] = YEAR(LR.StartDate) AND PP.[Month] = MONTH(LR.StartDate)
                                           AND ISNULL(PR.VoucherGenerated, 0) = 1))
                          )
                    ORDER BY LR.StartDate, LR.ID"
                : @"SELECT LR.ID, LR.RequestNumber, LR.StartDate, LR.TotalDays, 0 AS Carried
                    FROM Leave_Requests LR
                    WHERE LR.Admin_ID = @AdminID AND LR.Status = 'APPROVED'
                      AND ISNULL(LR.DeductSalary, 0) = 1 AND ISNULL(LR.IsReplaced, 0) = 0
                      AND LR.StartDate >= @MonthStart AND LR.StartDate < @NextMonth
                    ORDER BY LR.StartDate, LR.ID";
            using (var cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@AdminID", adminId);
                cmd.Parameters.AddWithValue("@MonthStart", monthStart);
                cmd.Parameters.AddWithValue("@NextMonth", monthStart.AddMonths(1));
                if (claim)
                {
                    cmd.Parameters.AddWithValue("@RecordID", recordId);
                    cmd.Parameters.AddWithValue("@Lookback", monthStart.AddMonths(-CarryLookbackMonths));
                }
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        calc.Items.Add(new PayrollLeaveItem
                        {
                            Id = Convert.ToInt64(r["ID"]),
                            RequestNumber = r["RequestNumber"]?.ToString(),
                            StartDate = Convert.ToDateTime(r["StartDate"]),
                            Days = r["TotalDays"] == DBNull.Value ? 0m : Convert.ToDecimal(r["TotalDays"]),
                            Carried = Convert.ToInt32(r["Carried"]) == 1
                        });
            }
            calc.Deduction = CalcDeduction(baseSalary, calc.Items, year, month);
            return calc;
        }

        /// <summary><see cref="Load"/> + <see cref="Claim"/> ด้วย connection ของตัวเอง (ปุ่มคำนวณใหม่หน้าเงินเดือน)</summary>
        public static PayrollLeaveCalc LoadAndClaim(int adminId, int year, int month, long recordId, decimal baseSalary)
        {
            using (var conn = new SqlConnection(ConnStr))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    var calc = Load(conn, tx, adminId, year, month, recordId, baseSalary);
                    Claim(conn, tx, recordId, calc);
                    tx.Commit();
                    return calc;
                }
            }
        }

        /// <summary>ผูกใบลาที่หักกับรอบ (รอบนี้รับไว้) และปล่อยใบที่รอบนี้เคยรับแต่ไม่หักแล้ว (ทำงานทดแทน/ไม่อนุมัติ) — ไม่มีคอลัมน์ = ไม่ทำอะไร</summary>
        public static void Claim(SqlConnection conn, SqlTransaction tx, long recordId, PayrollLeaveCalc calc)
        {
            if (recordId <= 0 || !HasClaimColumn(conn, tx)) return;
            string ids = string.Join(",", calc.Items.Select(i => i.Id.ToString(CultureInfo.InvariantCulture)));
            string sql = "UPDATE Leave_Requests SET Payroll_Record_ID = NULL WHERE Payroll_Record_ID = @R"
                         + (ids.Length > 0 ? " AND ID NOT IN (" + ids + ");" : ";")
                         + (ids.Length > 0 ? " UPDATE Leave_Requests SET Payroll_Record_ID = @R WHERE ID IN (" + ids + ");" : "");
            using (var cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@R", recordId);
                cmd.ExecuteNonQuery();
            }
        }

        // ──────────────────────────────────────────────────────────────
        // คำนวณยอดลาของรอบที่ยังไม่ทำจ่าย
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// คำนวณเฉพาะส่วนที่ขึ้นกับวันลา (วันลา/วันทำงาน/หักลา/ประกันสังคม) ของรอบที่ยังไม่ทำจ่าย — เงินเดือน/OT/โบนัส/หักอื่นที่แก้ไว้คงเดิม.
        /// คืน null เมื่อรอบทำจ่ายแล้ว/ไม่พบ ไม่งั้นคืนผล (ยอดหักใหม่)
        /// </summary>
        public static PayrollLeaveCalc RecalcRecord(SqlConnection conn, SqlTransaction tx, long recordId)
        {
            int adminId, year, month, periodId; decimal baseSalary, earnings, tax, other; bool paid;
            using (var cmd = new SqlCommand(@"
                SELECT PR.Admin_ID, PR.PayrollPeriod_ID, PP.[Year], PP.[Month], ISNULL(PR.VoucherGenerated, 0) AS Paid,
                       CASE WHEN ISNULL(PR.BaseSalary, 0) = 0 THEN ISNULL(ES.MonthlySalary, 0) ELSE PR.BaseSalary END AS BaseSalary,
                       ISNULL(PR.OTAmount, 0) AS OTAmount, ISNULL(PR.BonusAmount, 0) AS BonusAmount, ISNULL(PR.AllowanceAmount, 0) AS AllowanceAmount,
                       ISNULL(PR.Tax, 0) AS Tax, ISNULL(PR.OtherDeductions, 0) AS OtherDeductions
                FROM Payroll_Records PR
                INNER JOIN Payroll_Periods PP ON PP.ID = PR.PayrollPeriod_ID
                LEFT JOIN Employee_Salary ES ON ES.Admin_ID = PR.Admin_ID AND ES.IsActive = 1
                WHERE PR.ID = @R", conn, tx))
            {
                cmd.Parameters.AddWithValue("@R", recordId);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    adminId = Convert.ToInt32(r["Admin_ID"]);
                    periodId = Convert.ToInt32(r["PayrollPeriod_ID"]);
                    year = Convert.ToInt32(r["Year"]);
                    month = Convert.ToInt32(r["Month"]);
                    paid = Convert.ToBoolean(r["Paid"]);
                    baseSalary = Convert.ToDecimal(r["BaseSalary"]);
                    earnings = baseSalary + Convert.ToDecimal(r["OTAmount"]) + Convert.ToDecimal(r["BonusAmount"]) + Convert.ToDecimal(r["AllowanceAmount"]);
                    tax = Convert.ToDecimal(r["Tax"]);
                    other = Convert.ToDecimal(r["OtherDeductions"]);
                }
            }
            if (paid) return null;

            var calc = Load(conn, tx, adminId, year, month, recordId, baseSalary);
            decimal ss = SocialSecurity(earnings, calc.Deduction, tax, other, year);
            using (var cmd = new SqlCommand(@"
                UPDATE Payroll_Records
                SET LeaveDays = @LeaveDays, WorkDays = @WorkDays, LeaveDeduction = @LeaveDeduction, SocialSecurity = @SS, UpdatedDate = GETDATE()
                WHERE ID = @R AND ISNULL(VoucherGenerated, 0) = 0;
                UPDATE Payroll_Periods
                SET TotalEmployees = (SELECT COUNT(*) FROM Payroll_Records WHERE PayrollPeriod_ID = @P),
                    TotalGrossPay = (SELECT ISNULL(SUM(TotalEarnings), 0) FROM Payroll_Records WHERE PayrollPeriod_ID = @P),
                    TotalDeductions = (SELECT ISNULL(SUM(TotalDeductions), 0) FROM Payroll_Records WHERE PayrollPeriod_ID = @P),
                    TotalNetPay = (SELECT ISNULL(SUM(NetSalary), 0) FROM Payroll_Records WHERE PayrollPeriod_ID = @P)
                WHERE ID = @P;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@LeaveDays", calc.TotalDays);
                cmd.Parameters.AddWithValue("@WorkDays", WorkDays(year, month, calc.InMonthDays));
                cmd.Parameters.AddWithValue("@LeaveDeduction", calc.Deduction);
                cmd.Parameters.AddWithValue("@SS", ss);
                cmd.Parameters.AddWithValue("@R", recordId);
                cmd.Parameters.AddWithValue("@P", periodId);
                cmd.ExecuteNonQuery();
            }
            Claim(conn, tx, recordId, calc);
            return calc;
        }

        // ──────────────────────────────────────────────────────────────
        // อนุมัติ/ทดแทน/ยกเลิกทดแทน → อัปเดตเงินเดือนทันที
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// เรียกหลังสถานะใบลาเปลี่ยน (commit แล้ว) — คำนวณรอบที่เกี่ยวข้องที่ยังไม่ทำจ่ายใหม่ แล้วคืนข้อความภาษาไทยสำหรับผู้อนุมัติ
        /// (ว่าง = ใบนี้ไม่หักเงิน). ไม่โยน exception — ล้มเหลว = log + ข้อความให้กดคำนวณใหม่ที่หน้าเงินเดือน
        /// </summary>
        public static string OnLeaveChanged(long leaveRequestId)
        {
            try
            {
                using (var conn = new SqlConnection(ConnStr))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        string msg = OnLeaveChangedCore(conn, tx, leaveRequestId);
                        tx.Commit();
                        if (!string.IsNullOrEmpty(msg)) Log("ใบลา #" + leaveRequestId + ": " + msg);
                        return msg;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("ใบลา #" + leaveRequestId + " คำนวณเงินเดือนไม่สำเร็จ: " + ex.Message);
                return "อัปเดตเงินเดือนอัตโนมัติไม่สำเร็จ — เปิดหน้าเงินเดือนแล้วกด \"ปรับยอดลา\"";
            }
        }

        private static string OnLeaveChangedCore(SqlConnection conn, SqlTransaction tx, long leaveId)
        {
            bool claim = HasClaimColumn(conn, tx);
            int adminId; DateTime start; bool deduct, counts; long claimedBy = 0;
            using (var cmd = new SqlCommand(
                "SELECT Admin_ID, StartDate, Status, ISNULL(DeductSalary,0) AS DeductSalary, ISNULL(IsReplaced,0) AS IsReplaced"
                + (claim ? ", ISNULL(Payroll_Record_ID, 0) AS Claimed" : ", 0 AS Claimed")
                + " FROM Leave_Requests WHERE ID = @ID", conn, tx))
            {
                cmd.Parameters.AddWithValue("@ID", leaveId);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return "";
                    adminId = Convert.ToInt32(r["Admin_ID"]);
                    start = Convert.ToDateTime(r["StartDate"]);
                    deduct = Convert.ToBoolean(r["DeductSalary"]);
                    counts = deduct && string.Equals(r["Status"]?.ToString(), "APPROVED", StringComparison.OrdinalIgnoreCase) && !Convert.ToBoolean(r["IsReplaced"]);
                    claimedBy = Convert.ToInt64(r["Claimed"]);
                }
            }
            if (!deduct) return "";

            // รอบที่ใบนี้เคยถูกหักไว้ (ยังไม่จ่าย) — ไม่หักแล้ว (ทดแทน) ต้องคำนวณใหม่เพื่อคืนยอด
            var touched = new List<long>();
            string note = "";
            if (claimedBy > 0)
            {
                var rc = RecordInfo(conn, tx, claimedBy);
                if (rc != null && !rc.Paid) { RecalcRecord(conn, tx, claimedBy); touched.Add(claimedBy); }
                else if (rc != null && rc.Paid)
                    return counts ? "" : "ใบลานี้ถูกหักไปแล้วในเงินเดือนรอบ " + ThaiMonth(rc.Year, rc.Month) + " (ทำจ่ายแล้ว) — ถ้าต้องคืนเงิน แก้รายคนที่หน้าเงินเดือน";
            }
            if (!counts)
                return touched.Count > 0 ? "คืนยอดหักลาในเงินเดือนรอบที่ยังไม่ทำจ่ายแล้ว" : "";

            // รอบเดือนของใบลา
            var own = RecordFor(conn, tx, adminId, start.Year, start.Month);
            if (own != null && !own.Paid)
            {
                var c = RecalcRecord(conn, tx, own.Id);
                return "หักในเงินเดือนรอบ " + ThaiMonth(own.Year, own.Month) + " แล้ว (หักลารวม " + (c?.Deduction ?? 0m).ToString("N2", CultureInfo.InvariantCulture) + " บาท)";
            }
            if (own == null)
                return "ยังไม่ได้สร้างรอบเงินเดือน " + ThaiMonth(start.Year, start.Month) + " — จะหักเมื่อสร้างรอบ";

            // รอบเดือนของใบลาทำจ่ายแล้ว → ยกไปรอบถัดไปที่ยังไม่ทำจ่าย
            if (!claim)
                return "เงินเดือนรอบ " + ThaiMonth(own.Year, own.Month) + " ทำจ่ายแล้ว — ระบบยังยกไปหักรอบถัดไปไม่ได้ (ยังไม่ได้รัน PHASE19_43) ต้องแก้รายคนที่หน้าเงินเดือน";
            var next = NextUnpaidRecord(conn, tx, adminId, start.Year, start.Month);
            if (next == null)
                return "เงินเดือนรอบ " + ThaiMonth(own.Year, own.Month) + " ทำจ่ายแล้ว — จะยกไปหักในรอบถัดไปเมื่อสร้างรอบ";
            var cn = RecalcRecord(conn, tx, next.Id);
            bool included = cn != null && cn.Items.Any(i => i.Id == leaveId);
            return included
                ? "เงินเดือนรอบ " + ThaiMonth(own.Year, own.Month) + " ทำจ่ายแล้ว — ยกไปหักในรอบ " + ThaiMonth(next.Year, next.Month) + " แล้ว"
                : "เงินเดือนรอบ " + ThaiMonth(own.Year, own.Month) + " ทำจ่ายแล้ว — ใบลาเก่ากว่า " + CarryLookbackMonths + " เดือน ระบบไม่ยกมาหักอัตโนมัติ (แก้รายคน)";
        }

        private sealed class RecInfo { public long Id; public int Year, Month; public bool Paid; }

        private static RecInfo RecordInfo(SqlConnection conn, SqlTransaction tx, long recordId)
        {
            using (var cmd = new SqlCommand(@"SELECT PR.ID, PP.[Year], PP.[Month], ISNULL(PR.VoucherGenerated,0) AS Paid
                FROM Payroll_Records PR INNER JOIN Payroll_Periods PP ON PP.ID = PR.PayrollPeriod_ID WHERE PR.ID = @R", conn, tx))
            {
                cmd.Parameters.AddWithValue("@R", recordId);
                return ReadRec(cmd);
            }
        }

        private static RecInfo RecordFor(SqlConnection conn, SqlTransaction tx, int adminId, int year, int month)
        {
            using (var cmd = new SqlCommand(@"SELECT TOP 1 PR.ID, PP.[Year], PP.[Month], ISNULL(PR.VoucherGenerated,0) AS Paid
                FROM Payroll_Records PR INNER JOIN Payroll_Periods PP ON PP.ID = PR.PayrollPeriod_ID
                WHERE PR.Admin_ID = @A AND PP.[Year] = @Y AND PP.[Month] = @M ORDER BY PR.ID DESC", conn, tx))
            {
                cmd.Parameters.AddWithValue("@A", adminId);
                cmd.Parameters.AddWithValue("@Y", year);
                cmd.Parameters.AddWithValue("@M", month);
                return ReadRec(cmd);
            }
        }

        private static RecInfo NextUnpaidRecord(SqlConnection conn, SqlTransaction tx, int adminId, int year, int month)
        {
            using (var cmd = new SqlCommand(@"SELECT TOP 1 PR.ID, PP.[Year], PP.[Month], ISNULL(PR.VoucherGenerated,0) AS Paid
                FROM Payroll_Records PR INNER JOIN Payroll_Periods PP ON PP.ID = PR.PayrollPeriod_ID
                WHERE PR.Admin_ID = @A AND ISNULL(PR.VoucherGenerated,0) = 0 AND (PP.[Year] * 12 + PP.[Month]) > @YM
                ORDER BY PP.[Year], PP.[Month], PR.ID", conn, tx))
            {
                cmd.Parameters.AddWithValue("@A", adminId);
                cmd.Parameters.AddWithValue("@YM", year * 12 + month);
                return ReadRec(cmd);
            }
        }

        private static RecInfo ReadRec(SqlCommand cmd)
        {
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                return new RecInfo { Id = Convert.ToInt64(r["ID"]), Year = Convert.ToInt32(r["Year"]), Month = Convert.ToInt32(r["Month"]), Paid = Convert.ToBoolean(r["Paid"]) };
            }
        }

        // ──────────────────────────────────────────────────────────────
        // หน้าเงินเดือน: ตรวจยอดลาที่ไม่ตรง + ปรับทั้งรอบ
        // ──────────────────────────────────────────────────────────────

        /// <summary>ยอดลาที่บันทึกในรอบไม่ตรงกับใบลาที่อนุมัติ (แถวละคน)</summary>
        public sealed class Mismatch
        {
            public long RecordId; public string EmployeeName; public bool Paid;
            public decimal RecordedDays, ExpectedDays, RecordedDeduction, ExpectedDeduction;
            public List<PayrollLeaveItem> Items = new List<PayrollLeaveItem>();
            /// <summary>รอบจ่ายแล้ว: ใบที่ยังไม่ถูกหักจะยกไปรอบนี้ (null = ยังไม่มีรอบถัดไป / ไม่มีคอลัมน์)</summary>
            public string CarryTo;
        }

        /// <summary>รายคนในรอบที่ยอดลา/ยอดหักลาไม่ตรงใบลาที่อนุมัติ — อ่านอย่างเดียว</summary>
        public static List<Mismatch> FindMismatches(int periodId)
        {
            var list = new List<Mismatch>();
            using (var conn = new SqlConnection(ConnStr))
            {
                conn.Open();
                int year = 0, month = 0;
                var rows = new List<Tuple<long, int, string, bool, decimal, decimal, decimal>>();
                using (var cmd = new SqlCommand(@"
                    SELECT PR.ID, PR.Admin_ID, PR.EmployeeName, ISNULL(PR.VoucherGenerated,0) AS Paid, ISNULL(PR.LeaveDays,0) AS LeaveDays,
                           ISNULL(PR.LeaveDeduction,0) AS LeaveDeduction,
                           CASE WHEN ISNULL(PR.BaseSalary, 0) = 0 THEN ISNULL(ES.MonthlySalary, 0) ELSE PR.BaseSalary END AS BaseSalary,
                           PP.[Year], PP.[Month]
                    FROM Payroll_Records PR
                    INNER JOIN Payroll_Periods PP ON PP.ID = PR.PayrollPeriod_ID
                    LEFT JOIN Employee_Salary ES ON ES.Admin_ID = PR.Admin_ID AND ES.IsActive = 1
                    WHERE PR.PayrollPeriod_ID = @P", conn))
                {
                    cmd.Parameters.AddWithValue("@P", periodId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            year = Convert.ToInt32(r["Year"]); month = Convert.ToInt32(r["Month"]);
                            rows.Add(Tuple.Create(Convert.ToInt64(r["ID"]), Convert.ToInt32(r["Admin_ID"]), r["EmployeeName"]?.ToString(),
                                Convert.ToBoolean(r["Paid"]), Convert.ToDecimal(r["LeaveDays"]), Convert.ToDecimal(r["LeaveDeduction"]), Convert.ToDecimal(r["BaseSalary"])));
                        }
                }
                if (rows.Count == 0) return list;
                bool claim = HasClaimColumn(conn, null);
                foreach (var row in rows)
                {
                    var calc = Load(conn, null, row.Item2, year, month, row.Item1, row.Item7);
                    if (row.Item4)
                    {
                        // จ่ายแล้ว: สนใจเฉพาะใบของเดือนนี้ที่ยังไม่ถูกหัก (ยอดที่บันทึกน้อยกว่าใบลา)
                        decimal inMonth = calc.InMonthDays;
                        if (inMonth <= row.Item5 + 0.001m) continue;
                        var m = new Mismatch
                        {
                            RecordId = row.Item1, EmployeeName = row.Item3, Paid = true, RecordedDays = row.Item5, ExpectedDays = inMonth,
                            RecordedDeduction = row.Item6, ExpectedDeduction = HRConfiguration.CalculateLeaveDeduction(row.Item7, inMonth, DateTime.DaysInMonth(year, month))
                        };
                        m.Items.AddRange(calc.Items.Where(i => !i.Carried));
                        if (claim)
                        {
                            var nx = NextUnpaidRecord(conn, null, row.Item2, year, month);
                            if (nx != null) m.CarryTo = ThaiMonth(nx.Year, nx.Month);
                        }
                        list.Add(m);
                        continue;
                    }
                    if (Math.Abs(calc.TotalDays - row.Item5) <= 0.001m && Math.Abs(calc.Deduction - row.Item6) <= 0.01m) continue;
                    var mm = new Mismatch
                    {
                        RecordId = row.Item1, EmployeeName = row.Item3, Paid = false, RecordedDays = row.Item5, ExpectedDays = calc.TotalDays,
                        RecordedDeduction = row.Item6, ExpectedDeduction = calc.Deduction
                    };
                    mm.Items.AddRange(calc.Items);
                    list.Add(mm);
                }
            }
            return list;
        }

        /// <summary>ปรับยอดลา (เฉพาะส่วนที่ขึ้นกับวันลา) ของทุกคนในรอบที่ยังไม่ทำจ่าย — คืนจำนวนคนที่ปรับ</summary>
        public static int SyncPeriod(int periodId)
        {
            int n = 0;
            using (var conn = new SqlConnection(ConnStr))
            {
                conn.Open();
                var ids = new List<long>();
                using (var cmd = new SqlCommand("SELECT ID FROM Payroll_Records WHERE PayrollPeriod_ID = @P AND ISNULL(VoucherGenerated,0) = 0", conn))
                {
                    cmd.Parameters.AddWithValue("@P", periodId);
                    using (var r = cmd.ExecuteReader()) while (r.Read()) ids.Add(Convert.ToInt64(r["ID"]));
                }
                foreach (long id in ids)
                    using (var tx = conn.BeginTransaction())
                    {
                        if (RecalcRecord(conn, tx, id) != null) n++;
                        tx.Commit();
                    }
            }
            return n;
        }

        /// <summary>HTML แถบเตือนหน้าเงินเดือน (encode ทุกค่า) — ว่าง = ตรงทั้งหมด</summary>
        public static string RenderMismatchHtml(List<Mismatch> list)
        {
            if (list == null || list.Count == 0) return "";
            Func<string, string> H = System.Web.HttpUtility.HtmlEncode;
            var sb = new StringBuilder();
            bool anyUnpaid = list.Any(m => !m.Paid);
            sb.Append("<div class=\"alert alert-warning\" style=\"margin:10px 0;\"><b>⚠ ยอดหักลาไม่ตรงกับใบลาที่อนุมัติ (")
              .Append(list.Count).Append(" คน)</b>");
            if (anyUnpaid) sb.Append(" — กดปุ่ม <b>\"ปรับยอดลาให้ตรงใบลา\"</b> (ไม่แตะเงินเดือน/OT/โบนัส/หักอื่นที่แก้ไว้)");
            sb.Append("<ul style=\"margin:6px 0 0 18px;padding:0;\">");
            foreach (var m in list)
            {
                sb.Append("<li><b>").Append(H(m.EmployeeName ?? "")).Append("</b>: ในรอบ ")
                  .Append(m.RecordedDays.ToString("0.##", CultureInfo.InvariantCulture)).Append(" วัน/หัก ").Append(m.RecordedDeduction.ToString("N2", CultureInfo.InvariantCulture))
                  .Append(" · ตามใบลา ").Append(m.ExpectedDays.ToString("0.##", CultureInfo.InvariantCulture)).Append(" วัน/หัก ").Append(m.ExpectedDeduction.ToString("N2", CultureInfo.InvariantCulture));
                if (m.Items.Count > 0)
                    sb.Append(" (").Append(H(string.Join(", ", m.Items.Select(i => (i.RequestNumber ?? ("#" + i.Id)) + " " + i.StartDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                        + (i.Carried ? " ยกมาจาก " + ThaiMonth(i.StartDate.Year, i.StartDate.Month) : ""))))).Append(")");
                if (m.Paid)
                    sb.Append(" — <i>รอบนี้ทำจ่ายแล้ว ").Append(m.CarryTo != null
                        ? "ส่วนที่ยังไม่หักยกไปหักรอบ " + H(m.CarryTo) + " (เปิดรอบนั้นแล้วกด \"ปรับยอดลาให้ตรงใบลา\" ถ้ายอดยังไม่ขึ้น)"
                        : "ส่วนที่ยังไม่หักจะยกไปหักรอบถัดไปเมื่อสร้างรอบ (ต้องรัน PHASE19_43) หรือแก้ ✏ รายคน").Append("</i>");
                sb.Append("</li>");
            }
            return sb.Append("</ul></div>").ToString();
        }

        private static void Log(string msg)
        {
            try { new Take_Time_BangPhra.code().Logs(ConnStr, "PayrollLeave", msg, "System"); } catch { }
        }
    }
}
