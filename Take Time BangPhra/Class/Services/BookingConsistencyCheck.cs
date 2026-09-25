using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// ตรวจความสอดคล้องของข้อมูลการจอง (วันละครั้ง ก่อนรายงานเช้า 08:00 ทาง LINE)
    ///
    /// ทำไมต้องมี: รายงานเช้าเคยโชว์การจอง OTA แบบ Channel Collect ว่า "ค้างชำระ" เพราะนับ
    /// เฉพาะ Payment_History (ขา intake OTA เขียน Reservation.Deposit แต่ไม่เขียน Payment_History)
    /// และขาเช็คอินเดิมบังคับให้พนักงานบันทึกเงินที่ OTA เก็บไปแล้วเป็น "เงินสด" — ข้อมูลเพี้ยนแบบนี้
    /// ควรมีคนรู้ก่อนรายงานเช้าออก ไม่ใช่รู้ตอนแขกยืนอยู่หน้าเคาน์เตอร์
    ///
    /// ตรวจการจองที่ยังไม่ยกเลิก เช็คเอาท์ตั้งแต่วันนี้ และเช็คอินภายใน 14 วันข้างหน้า:
    ///   ก) Channel Collect แต่มีเงินสดใน Payment_History (บันทึกเงิน OTA เป็นเงินสด)
    ///      — หมวดนี้หมวดเดียวย้อนดูใบที่เช็คเอาท์ไปแล้ว CashBacklogDays วันด้วย (งานค้างแก้)
    ///      แถวที่ถูกย้ายไปสถานะ OTA_RECLASS (หน้าตรวจเงินสดของใบ OTA) ไม่นับ
    ///   ข) OTA แบบเก็บหน้างาน รับเงินแล้ว (Payment_History ยังไม่ผูกใบเสร็จ) และยังไม่มีใบเสร็จที่ใช้งานอยู่
    ///   ค) OTA ที่ยังไม่ชัดว่าใครเก็บเงิน และเข้าพักภายใน 3 วัน
    ///   ง) เศษปัด: ยอดรวม − ยอดรับ ต่างเกินเกณฑ์ปัดเศษ (Balance_Rounding_Tolerance) แต่ไม่เกิน 10 บาท
    ///   จ) เบอร์ลูกค้าผิดรูปแบบ (OTA_UNKNOWN / ใช้ PIN เป็นเบอร์ / เบอร์ต่างประเทศเพี้ยน)
    ///   ฉ) มัดจำ ≠ ยอดใน Payment_History ก่อนเช็คอิน
    ///
    /// "ต้องเก็บเงินหน้างาน" ไม่อยู่ในรายงานนี้แล้ว — ซ้ำกับตารางรายวันที่ส่งเข้า LINE อีก 30 นาทีถัดมา
    ///
    /// ส่งผ่าน <c>Notify.Send(Notify.Ev.BookingConsistency, …)</c> เฉพาะวันที่พบปัญหา
    /// ไม่พบอะไรเลย = บันทึก log อย่างเดียว ไม่ส่ง
    /// </summary>
    public static class BookingConsistencyCheck
    {
        private const string LastRunKey = "Booking_Check_LastRun";
        private const int MaxRowsPerCategory = 10;
        private const decimal RoundingResidueMax = 10m;
        private const int HorizonDays = 14;
        /// <summary>หมวด "บันทึกเงิน OTA เป็นเงินสด" ย้อนดูใบที่เช็คเอาท์ไปแล้วกี่วัน (งานค้างที่ยังไม่ได้แก้)</summary>
        private const int CashBacklogDays = 60;
        /// <summary>หมวด "ยังไม่ชัดใครเก็บเงิน" เตือนเมื่อเข้าพักภายในกี่วัน</summary>
        private const int UnknownCollectDays = 3;

        // กันยิง query config ทุก 30 วิ ของ timer — ตรวจเงื่อนไขอย่างมากนาทีละครั้ง
        private static readonly object _probeLock = new object();
        private static DateTime _nextProbe = DateTime.MinValue;
        private static string _doneDay;

        // ── ตัวเรียกจาก timer ─────────────────────────────────────────────────

        /// <summary>
        /// รันวันละครั้งเมื่อถึงเวลา (ค่าตั้งต้น = 30 นาทีก่อนเวลาส่งรายงานเช้าทาง LINE)
        /// จองสิทธิ์ "รันของวันนี้" แบบ atomic ก่อน (UPDATE … @@ROWCOUNT) กันหลาย worker ยิงซ้ำ
        /// ไม่มีทาง throw
        /// </summary>
        public static void RunIfDue(string conn)
        {
            try
            {
                if (string.IsNullOrEmpty(conn)) return;

                DateTime now = DateTime.Now;
                string today = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

                lock (_probeLock)
                {
                    if (_doneDay == today) return;
                    if (now < _nextProbe) return;
                    _nextProbe = now.AddMinutes(1);
                }

                if (Cfg(conn, "Booking_Check_Enabled", "1") != "1") return;
                if (now.TimeOfDay < DueTime(conn)) return;

                if (Cfg(conn, LastRunKey, "") == today) { _doneDay = today; return; }
                if (!TryClaimToday(conn, today)) { _doneDay = today; return; }
                _doneDay = today;

                int findings;
                string report = Run(conn, out findings);

                var c = new code();
                if (findings <= 0)
                {
                    c.Logs(conn, "BookingCheck", "ตรวจความสอดคล้องการจอง: ไม่พบปัญหา (ไม่ส่งแจ้งเตือน)\n" + report, "SYSTEM");
                    return;
                }

                EnsureNotifyRule(conn);
                bool sent = global::Notify.Send(global::Notify.Ev.BookingConsistency, global::Notify.E(report));
                c.Logs(conn, "BookingCheck",
                    "ตรวจความสอดคล้องการจอง: พบ " + findings + " รายการ — " + (sent ? "ส่งแจ้งเตือนแล้ว" : "ไม่ได้ส่ง (ช่องทางปิด/ช่วงเวลาเงียบ)")
                    + "\n" + report, "SYSTEM");
            }
            catch (Exception ex)
            {
                try { new code().Logs(conn, "BookingCheck", "RunIfDue error: " + ex.Message, "SYSTEM"); } catch { }
            }
        }

        /// <summary>
        /// เวลาเริ่มตรวจของวัน: Booking_Check_Time (HH:mm) ถ้าตั้งไว้ ไม่งั้น = เวลาส่งรายงานเช้า
        /// (Line_DailyReport_SendTime ค่าตั้งต้น 08:00) ลบ 30 นาที
        /// </summary>
        private static TimeSpan DueTime(string conn)
        {
            TimeSpan t;
            if (TryParseTime(Cfg(conn, "Booking_Check_Time", ""), out t)) return t;

            TimeSpan send;
            if (!TryParseTime(Cfg(conn, "Line_DailyReport_SendTime", "08:00"), out send))
                send = new TimeSpan(8, 0, 0);

            TimeSpan due = send - TimeSpan.FromMinutes(30);
            return due < TimeSpan.Zero ? TimeSpan.Zero : due;
        }

        /// <summary>จองสิทธิ์รันของวันนี้ (คืน true = เราได้สิทธิ์) — upsert + เช็คในคำสั่งเดียว กัน race</summary>
        private static bool TryClaimToday(string conn, string today)
        {
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    @"IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config
                                      WHERE ConfigKey = 'Booking_Check_LastRun')
                          INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
                          VALUES ('Booking_Check_LastRun', '', N'วันที่ตรวจความสอดคล้องการจองล่าสุด (ระบบตั้งเอง)');

                      UPDATE Accounting_Integration_Config SET ConfigValue = @today
                       WHERE ConfigKey = 'Booking_Check_LastRun'
                         AND ISNULL(ConfigValue, '') <> @today;

                      SELECT @@ROWCOUNT AS Claimed;",
                    new Dictionary<string, object> { { "@today", today } });
                return dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["Claimed"]) > 0;
            }
            catch (Exception ex)
            {
                // จองสิทธิ์ไม่ได้ = ไม่รัน (ปลอดภัยกว่าเสี่ยงส่งรัว)
                try { new code().Logs(conn, "BookingCheck", "TryClaimToday error: " + ex.Message, "SYSTEM"); } catch { }
                return false;
            }
        }

        /// <summary>
        /// เหตุการณ์ใหม่ยังไม่มีแถวใน Notification_Rules (ไมเกรชันเดิม seed เฉพาะเหตุการณ์รุ่นแรก)
        /// ⇒ Notify จะถือว่า "ปิด" → เติมแถวตั้งต้นให้ (Telegram เปิด, LINE ปิด) ครั้งแรกเท่านั้น
        /// ถ้าผู้ดูแลไปปิดในหน้าตั้งค่าแล้ว แถวมีอยู่แล้ว จึงไม่ถูกเปิดกลับ
        /// </summary>
        private static void EnsureNotifyRule(string conn)
        {
            try
            {
                int n = new code().DatabaseInsertSafe(conn,
                    @"IF OBJECT_ID('dbo.Notification_Rules', 'U') IS NOT NULL
                      BEGIN
                          IF NOT EXISTS (SELECT 1 FROM Notification_Rules WHERE Event_Code = @e AND Channel = 'TELEGRAM')
                              INSERT INTO Notification_Rules (Event_Code, Channel, Enabled, Modified_Date)
                              VALUES (@e, 'TELEGRAM', 1, GETDATE());
                          IF NOT EXISTS (SELECT 1 FROM Notification_Rules WHERE Event_Code = @e AND Channel = 'LINE')
                              INSERT INTO Notification_Rules (Event_Code, Channel, Enabled, Modified_Date)
                              VALUES (@e, 'LINE', 0, GETDATE());
                      END",
                    new Dictionary<string, object> { { "@e", global::Notify.Ev.BookingConsistency } });
                if (n > 0) global::Notify.Invalidate();
            }
            catch { }
        }

        // ── ตัวตรวจ (ใช้กับปุ่มในหน้า Admin ได้) ───────────────────────────────

        private sealed class ResInfo
        {
            public int Id;
            public string Name, Phone, Channel, OtaBookingId, OtaPaymentType, Status;
            public DateTime? CheckIn, CheckOut;
        }

        private sealed class Category
        {
            public string Title;
            public string Hint;   // บรรทัดบอกว่าแก้ที่ไหน — แสดงเมื่อมีรายการ
            public readonly List<string> Rows = new List<string>();
            public string Error;
            public Category(string title, string hint = null) { Title = title; Hint = hint; }
        }

        /// <summary>
        /// ตรวจทั้งหมดแล้วคืนรายงานภาษาไทย (ข้อความล้วน) — findings = จำนวนรายการที่พบรวมทุกหมวด
        /// แต่ละหมวดมี try/catch ของตัวเอง หมวดหนึ่งพังไม่ทำให้รายงานทั้งฉบับพัง
        /// </summary>
        public static string Run(string conn, out int findings)
        {
            findings = 0;
            DateTime today = DateTime.Today;
            decimal tol = ReservationBalance.RoundingTolerance;

            const string whereSql =
                "r.CheckoutDate >= @bcToday AND r.CheckinDate < @bcHorizon AND ISNULL(r.Status, N'') NOT LIKE N'ยกเลิก%'";
            // เฉพาะหมวด "บันทึกเงิน OTA เป็นเงินสด": รวมใบที่เช็คเอาท์ไปแล้วย้อนหลัง (งานค้างที่ยังไม่ได้แก้)
            const string whereCashSql =
                "r.CheckoutDate >= @bcBacklog AND r.CheckinDate < @bcHorizon AND ISNULL(r.Status, N'') NOT LIKE N'ยกเลิก%'";

            var catCash = new Category("บันทึกเงิน OTA เป็นเงินสด (รวมใบที่ออกไปแล้วย้อนหลัง " + CashBacklogDays + " วัน)",
                "ตรวจ/แก้ที่ หน้าประวัติการชำระ → แท็บ ⚠ เงินสดของใบ OTA");
            var catNoReceipt = new Category("รับเงินแล้วแต่ยังไม่ออกใบเสร็จ (OTA เก็บหน้างาน)");
            var catUnknown = new Category("⚪ ยังไม่ชัดใครเก็บเงิน — เข้าภายใน " + UnknownCollectDays + " วัน");
            var catRound = new Category("เศษปัด (ยอดรวมกับยอดรับต่างเกิน " + Money(tol) + " แต่ไม่เกิน 10 บาท)");
            var catPhone = new Category("เบอร์ลูกค้าผิดรูปแบบ");
            var catDeposit = new Category("มัดจำไม่ตรงกับประวัติรับเงิน (ก่อนเช็คอิน)");
            var cats = new[] { catCash, catNoReceipt, catUnknown, catRound, catPhone, catDeposit };

            string loadError = null;
            var infos = new List<ResInfo>();
            Dictionary<int, ReservationBalance> bal = null;

            // ── ข้อมูลการจอง (ชื่อ / เบอร์ / ช่องทาง) ─────────────────────────
            try
            {
                infos = LoadInfos(conn, whereSql, today);
            }
            catch (Exception ex) { loadError = "โหลดรายการจองไม่สำเร็จ: " + ex.Message; }

            // ── ยอดเงินของแต่ละใบ (คลาสกลางเดียวกับรายงานเช้า) ─────────────────
            string balError = null;
            try
            {
                bal = ReservationBalance.LoadMany(conn, whereSql, Params(today));
            }
            catch (Exception ex) { balError = "คำนวณยอดเงินไม่สำเร็จ: " + ex.Message; }
            if (bal == null) bal = new Dictionary<int, ReservationBalance>();

            // ── ก) Channel Collect แต่มีเงินสดใน Payment_History (รวมใบที่ออกไปแล้ว) ──
            try
            {
                // ช่วงเวลาของหมวดนี้กว้างกว่าหมวดอื่น → โหลดยอด/ข้อมูลใบของช่วงนี้แยก
                Dictionary<int, ReservationBalance> balCash = ReservationBalance.LoadMany(conn, whereCashSql, Params(today));
                List<ResInfo> infosCash;
                try { infosCash = LoadInfos(conn, whereCashSql, today); }
                catch { infosCash = infos; }   // โหลดชื่อไม่ได้ก็ยังรายงานเลขใบได้

                // Status = 'COMPLETED' ตัดแถวที่หน้าตรวจเงินสดของใบ OTA ย้ายไปเป็น OTA_RECLASS แล้วอยู่แล้ว
                // (เขียนเงื่อนไข <> 'OTA_RECLASS' ไว้ด้วยให้เจตนาชัด)
                var dt = new code().DatabaseQuerySafe(conn,
                    @"SELECT ph.Reservation_ID, SUM(ph.PaymentAmount) AS CashAmt, COUNT(*) AS Cnt
                        FROM Payment_History ph
                        INNER JOIN Reservation r ON r.ID = ph.Reservation_ID
                       WHERE " + whereCashSql + @"
                         AND ph.Status = 'COMPLETED'
                         AND ph.Status <> 'OTA_RECLASS'
                         AND ph.PaymentMethod IS NOT NULL
                         AND (UPPER(ph.PaymentMethod) LIKE N'%CASH%' OR ph.PaymentMethod LIKE N'%เงินสด%')
                       GROUP BY ph.Reservation_ID", Params(today));

                foreach (DataRow row in Rows(dt))
                {
                    int id = Convert.ToInt32(row["Reservation_ID"]);
                    ReservationBalance b;
                    if (!balCash.TryGetValue(id, out b) || b == null || !b.IsChannelCollect) continue;
                    decimal cash = Dec(row["CashAmt"]);
                    // ลูกค้าจ่ายของเสริม/ค่าชาร์จเป็นเงินสดเองได้ปกติ — ธงนี้หมายถึง "ลงเงิน OTA เป็นเงินสด"
                    // (หน้าเช็คอินรุ่นเก่าบังคับลงเต็มยอดค่าห้อง) → เตือนเฉพาะเมื่อเงินสดครอบคลุมยอด OTA
                    decimal otaPart = b.OtaAmount >= 0 ? b.OtaAmount : b.RoomTotal;
                    if (cash + tol < otaPart) continue;
                    int cnt = row["Cnt"] == DBNull.Value ? 0 : Convert.ToInt32(row["Cnt"]);
                    ResInfo info = Find(infosCash, id);
                    string departed = info != null && info.CheckOut.HasValue && info.CheckOut.Value.Date < today
                        ? " · ออกไปแล้ว " + info.CheckOut.Value.ToString("dd/MM", CultureInfo.InvariantCulture) : "";
                    catCash.Rows.Add(Line(info, id, b,
                        "เงินสด " + Money(cash) + " (" + cnt + " รายการ)" + departed));
                }
            }
            catch (Exception ex) { catCash.Error = ex.Message; }

            // ── ข) OTA เก็บหน้างาน: รับเงินแล้วแต่ยังไม่ออกใบเสร็จ ───────────────
            try
            {
                if (balError != null) throw new Exception(balError);
                var dt = new code().DatabaseQuerySafe(conn,
                    @"SELECT ph.Reservation_ID, SUM(ph.PaymentAmount) AS Amt, COUNT(*) AS Cnt
                        FROM Payment_History ph
                        INNER JOIN Reservation r ON r.ID = ph.Reservation_ID
                       WHERE " + whereSql + @"
                         AND ph.Status = 'COMPLETED'
                         AND (ph.Receipt_ID IS NULL OR ph.Receipt_ID = '')
                         AND NOT EXISTS (SELECT 1 FROM Account_Receipt ar
                                          WHERE ar.Reservation_ID = r.ID
                                            AND (ar.Status = 'Normal' OR ar.Status IS NULL))
                       GROUP BY ph.Reservation_ID", Params(today));

                foreach (DataRow row in Rows(dt))
                {
                    int id = Convert.ToInt32(row["Reservation_ID"]);
                    ReservationBalance b;
                    if (!bal.TryGetValue(id, out b) || b == null) continue;
                    if (!b.IsOta || b.CollectMode != ReservationBalance.ModeHotel) continue;
                    decimal amt = Dec(row["Amt"]);
                    if (amt <= tol) continue;
                    int cnt = row["Cnt"] == DBNull.Value ? 0 : Convert.ToInt32(row["Cnt"]);
                    catNoReceipt.Rows.Add(Line(Find(infos, id), id, b,
                        "รับแล้ว " + Money(amt) + " (" + cnt + " รายการ) ยังไม่มีใบเสร็จ"));
                }
            }
            catch (Exception ex) { catNoReceipt.Error = ex.Message; }

            // ── ค) OTA ยังไม่ชัดใครเก็บเงิน — เข้าภายใน 3 วัน ───────────────────
            try
            {
                if (balError != null) throw new Exception(balError);
                if (loadError != null) throw new Exception(loadError);
                DateTime lastDay = today.AddDays(UnknownCollectDays);
                foreach (var info in infos)
                {
                    if (!info.CheckIn.HasValue) continue;
                    DateTime ci = info.CheckIn.Value.Date;
                    if (ci < today || ci > lastDay) continue;

                    ReservationBalance b;
                    if (!bal.TryGetValue(info.Id, out b) || b == null) continue;
                    if (!b.IsOta || !b.IsCollectUnknown) continue;

                    string when = ci == today ? "เข้าวันนี้"
                        : ci == today.AddDays(1) ? "เข้าพรุ่งนี้"
                        : "เข้า " + ci.ToString("dd/MM", CultureInfo.InvariantCulture);
                    catUnknown.Rows.Add(Line(info, info.Id, b, when + " — เช็คอีเมลจอง/OTA ว่าใครเก็บเงินก่อนแขกมาถึง"));
                }
            }
            catch (Exception ex) { catUnknown.Error = ex.Message; }

            // ── ง) เศษปัด ──────────────────────────────────────────────────────
            try
            {
                if (balError != null) throw new Exception(balError);
                foreach (var kv in bal)
                {
                    var b = kv.Value;
                    // Channel Collect: ค่าห้องเป็นเงิน OTA / ยังไม่ชัดใครเก็บ: มีหมวดของตัวเอง — เทียบมัดจำไม่มีความหมาย
                    if (b == null || b.IsChannelCollect || b.IsCollectUnknown) continue;
                    decimal paid = b.LedgerRows > 0 ? b.PaidLedger : b.Deposit;
                    decimal diff = b.Total - paid;
                    decimal abs = Math.Abs(diff);
                    // ต่างไม่เกินเกณฑ์ปัดเศษ = ระบบถือว่าครบแล้ว ไม่ต้องรายงาน
                    if (abs <= tol || abs > RoundingResidueMax) continue;

                    catRound.Rows.Add(Line(Find(infos, kv.Key), kv.Key, b,
                        "ต่าง " + Money(diff) + (b.LedgerRows > 0 ? " (เทียบประวัติรับเงิน)" : " (เทียบมัดจำ)")));
                }
            }
            catch (Exception ex) { catRound.Error = ex.Message; }

            // ── จ) เบอร์ลูกค้าผิดรูปแบบ ─────────────────────────────────────────
            try
            {
                if (loadError != null) throw new Exception(loadError);
                foreach (var info in infos)
                {
                    string why = PhoneProblem(info);
                    if (why == null) continue;
                    ReservationBalance b;
                    bal.TryGetValue(info.Id, out b);
                    catPhone.Rows.Add(Line(info, info.Id, b, why + " [" + (info.Phone ?? "") + "]"));
                }
            }
            catch (Exception ex) { catPhone.Error = ex.Message; }

            // ── ฉ) มัดจำ ≠ ประวัติรับเงิน ก่อนเช็คอิน ────────────────────────────
            try
            {
                if (balError != null) throw new Exception(balError);
                if (loadError != null) throw new Exception(loadError);
                foreach (var info in infos)
                {
                    if (!IsBeforeCheckIn(info.Status)) continue;
                    ReservationBalance b;
                    if (!bal.TryGetValue(info.Id, out b) || b == null || b.IsChannelCollect) continue;
                    if (b.LedgerRows <= 0) continue;
                    if (Math.Abs(b.Deposit - b.PaidLedger) <= tol) continue;

                    catDeposit.Rows.Add(Line(info, info.Id, b,
                        "มัดจำ " + Money(b.Deposit) + " ≠ ประวัติ " + Money(b.PaidLedger)));
                }
            }
            catch (Exception ex) { catDeposit.Error = ex.Message; }

            // ── ประกอบรายงาน ───────────────────────────────────────────────────
            foreach (var cat in cats) findings += cat.Rows.Count;

            var sb = new StringBuilder();
            sb.AppendLine("🧾 ตรวจความสอดคล้องการจอง " + today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            sb.AppendLine("ช่วงเช็คอินถึง " + today.AddDays(HorizonDays).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                + " · ตรวจ " + (infos.Count > 0 ? infos.Count : bal.Count) + " ใบ · พบ " + findings + " รายการ");
            if (loadError != null) sb.AppendLine("⚠ " + loadError);
            if (balError != null) sb.AppendLine("⚠ " + balError);

            int no = 0;
            foreach (var cat in cats)
            {
                no++;
                sb.AppendLine();
                sb.AppendLine(no + ") " + cat.Title + ": " + cat.Rows.Count
                    + (cat.Error != null ? " (⚠ ตรวจไม่สำเร็จ: " + cat.Error + ")" : ""));
                if (cat.Rows.Count > 0 && !string.IsNullOrEmpty(cat.Hint))
                    sb.AppendLine("  👉 " + cat.Hint);
                int shown = 0;
                foreach (string r in cat.Rows)
                {
                    if (shown >= MaxRowsPerCategory) break;
                    sb.AppendLine("  " + r);
                    shown++;
                }
                if (cat.Rows.Count > shown)
                    sb.AppendLine("  … และอีก " + (cat.Rows.Count - shown) + " ใบ");
            }

            return sb.ToString().TrimEnd();
        }

        // ── โหลดข้อมูล ────────────────────────────────────────────────────────

        private static List<ResInfo> LoadInfos(string conn, string whereSql, DateTime today)
        {
            var c = new code();

            // คอลัมน์จาก migration รุ่นหลัง (Channel Manager / email intake) — บางฐานยังไม่มี
            bool hasBooking = false, hasPayType = false, hasChannel = false, hasGuest = false;
            var cols = c.DatabaseQuerySafe(conn,
                @"SELECT CASE WHEN COL_LENGTH('dbo.Reservation', 'OTA_Booking_ID')   IS NULL THEN 0 ELSE 1 END AS HasBooking,
                         CASE WHEN COL_LENGTH('dbo.Reservation', 'OTA_Payment_Type') IS NULL THEN 0 ELSE 1 END AS HasPayType,
                         CASE WHEN COL_LENGTH('dbo.Reservation', 'OTA_Channel')      IS NULL THEN 0 ELSE 1 END AS HasChannel,
                         CASE WHEN COL_LENGTH('dbo.Reservation', 'OTA_Guest_Name')   IS NULL THEN 0 ELSE 1 END AS HasGuest", null);
            if (cols != null && cols.Rows.Count > 0)
            {
                hasBooking = Convert.ToInt32(cols.Rows[0]["HasBooking"]) == 1;
                hasPayType = Convert.ToInt32(cols.Rows[0]["HasPayType"]) == 1;
                hasChannel = Convert.ToInt32(cols.Rows[0]["HasChannel"]) == 1;
                hasGuest = Convert.ToInt32(cols.Rows[0]["HasGuest"]) == 1;
            }

            string sql =
                "SELECT r.ID, r.Customer_MobilePhone, r.CheckinDate, r.CheckoutDate, r.Status, cu.Name AS CustName"
                + (hasBooking ? ", CAST(r.OTA_Booking_ID AS NVARCHAR(200)) AS OtaBookingId" : ", CAST(NULL AS NVARCHAR(200)) AS OtaBookingId")
                + (hasPayType ? ", CAST(r.OTA_Payment_Type AS NVARCHAR(100)) AS OtaPaymentType" : ", CAST(NULL AS NVARCHAR(100)) AS OtaPaymentType")
                + (hasChannel ? ", CAST(r.OTA_Channel AS NVARCHAR(100)) AS OtaChannel" : ", CAST(NULL AS NVARCHAR(100)) AS OtaChannel")
                + (hasGuest ? ", CAST(r.OTA_Guest_Name AS NVARCHAR(200)) AS OtaGuestName" : ", CAST(NULL AS NVARCHAR(200)) AS OtaGuestName")
                + @" FROM Reservation r
                     OUTER APPLY (SELECT TOP 1 c.Name FROM Customer c WHERE c.MobilePhone = r.Customer_MobilePhone) cu
                    WHERE " + whereSql + @"
                    ORDER BY r.CheckinDate, r.ID";

            var list = new List<ResInfo>();
            foreach (DataRow row in Rows(c.DatabaseQuerySafe(conn, sql, Params(today))))
            {
                var info = new ResInfo
                {
                    Id = Convert.ToInt32(row["ID"]),
                    Phone = Str(row["Customer_MobilePhone"]).Trim(),
                    Status = Str(row["Status"]).Trim(),
                    OtaBookingId = Str(row["OtaBookingId"]).Trim(),
                    OtaPaymentType = Str(row["OtaPaymentType"]).Trim(),
                    Channel = Str(row["OtaChannel"]).Trim()
                };
                string name = Str(row["CustName"]).Trim();
                if (name.Length == 0) name = Str(row["OtaGuestName"]).Trim();
                info.Name = name;
                if (row["CheckinDate"] != DBNull.Value) info.CheckIn = Convert.ToDateTime(row["CheckinDate"]);
                if (row["CheckoutDate"] != DBNull.Value) info.CheckOut = Convert.ToDateTime(row["CheckoutDate"]);
                list.Add(info);
            }
            return list;
        }

        private static Dictionary<string, object> Params(DateTime today)
        {
            return new Dictionary<string, object>
            {
                { "@bcToday", today },
                { "@bcHorizon", today.AddDays(HorizonDays + 1) },  // CheckinDate < วันที่ (วันนี้+14)+1 = ถึงสิ้นวันที่ 14
                { "@bcBacklog", today.AddDays(-CashBacklogDays) }  // ใช้เฉพาะหมวดเงินสดของใบ OTA (query อื่นไม่อ้างถึง)
            };
        }

        // ── กฎย่อย ────────────────────────────────────────────────────────────

        private static readonly Regex ParenRx = new Regex(@"\(([^)]*)\)", RegexOptions.Compiled);

        /// <summary>คืนเหตุผลถ้าเบอร์ผิดรูปแบบ, null = ปกติ</summary>
        private static string PhoneProblem(ResInfo info)
        {
            string phone = info.Phone ?? "";
            if (phone.Length == 0) return null;

            if (string.Equals(phone, "OTA_UNKNOWN", StringComparison.OrdinalIgnoreCase))
                return "ไม่มีเบอร์ (OTA_UNKNOWN)";

            // PIN ในวงเล็บของเลขจอง OTA ถูกใช้เป็นเบอร์ลูกค้า
            if (!string.IsNullOrEmpty(info.OtaBookingId))
            {
                string phoneDigits = Digits(phone);
                if (phoneDigits.Length > 0)
                {
                    foreach (Match m in ParenRx.Matches(info.OtaBookingId))
                    {
                        string inner = m.Groups[1].Value.Trim();
                        if (inner.Length > 0 && (inner == phone || Digits(inner) == phoneDigits))
                            return "ใช้ PIN ของเลขจองเป็นเบอร์";
                    }
                }
            }

            // เบอร์ต่างประเทศที่ถูกเติม 0 นำหน้าแทนรหัสประเทศ (เช่น 0447… ที่ควรเป็น +44 7…)
            // เบอร์ไทยยาว 9–10 หลัก ⇒ ยาว ≥ 11 และขึ้นต้น 0 (แต่ไม่ใช่ 00 ซึ่งเป็น prefix โทรต่างประเทศ)
            if (phone.Length >= 11 && phone[0] == '0' && phone[1] != '0')
            {
                char d2 = phone[1];
                if ((d2 >= '1' && d2 <= '5') || (d2 >= '7' && d2 <= '9'))
                    return "เบอร์ต่างประเทศเพี้ยน";
            }
            return null;
        }

        /// <summary>ยังไม่เช็คอิน: สถานะ "มัดจำแล้ว" หรือยังไม่ใช่สถานะเช็คอิน/เช็คเอาท์/ชำระครบ</summary>
        private static bool IsBeforeCheckIn(string status)
        {
            string s = status ?? "";
            if (s == "มัดจำแล้ว") return true;
            if (s.StartsWith("เช็คอิน", StringComparison.Ordinal)) return false;
            if (s.StartsWith("เช็คเอาท์", StringComparison.Ordinal)) return false;
            if (s.StartsWith("เช็คเอ้าท์", StringComparison.Ordinal)) return false;
            if (s.StartsWith("ชำระเงิน", StringComparison.Ordinal)) return false;
            return true;
        }

        // ── จัดรูปแบบ ─────────────────────────────────────────────────────────

        /// <summary>
        /// "#ID ชื่อ ช่องทาง ยอด/รับ/ค้าง — หมายเหตุ" — เลขใบจองขึ้นต้นบรรทัดเสมอ (ค้นในหน้าตารางจองได้ทันที)
        /// ระบบไม่มีค่าตั้ง URL เว็บไซต์กลาง (งานเบื้องหลังไม่มี HttpContext ให้เดา) จึงไม่แนบลิงก์
        /// </summary>
        private static string Line(ResInfo info, int id, ReservationBalance b, string note)
        {
            string name = info != null && !string.IsNullOrEmpty(info.Name) ? info.Name : "-";
            string channel = info != null && !string.IsNullOrEmpty(info.Channel) ? info.Channel
                : (b != null && b.IsChannelCollect ? "OTA" : "ตรง");
            if (info != null && !string.IsNullOrEmpty(info.OtaPaymentType))
                channel += "/" + info.OtaPaymentType;
            string money = b == null ? "ยอด -/รับ -/ค้าง -"
                : "ยอด " + Money(b.Total) + "/รับ " + Money(b.Received) + "/ค้าง " + Money(b.Due);
            return "#" + id + " " + name + " " + channel + " " + money
                + (string.IsNullOrEmpty(note) ? "" : " — " + note);
        }

        private static ResInfo Find(List<ResInfo> infos, int id)
        {
            if (infos == null) return null;
            foreach (var i in infos) if (i.Id == id) return i;
            return null;
        }

        private static string Money(decimal v)
        {
            return v.ToString("#,0.##", CultureInfo.InvariantCulture);
        }

        private static string Digits(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) if (ch >= '0' && ch <= '9') sb.Append(ch);
            return sb.ToString();
        }

        private static decimal Dec(object o)
        {
            if (o == null || o == DBNull.Value) return 0m;
            try { return Convert.ToDecimal(o, CultureInfo.InvariantCulture); }
            catch { return 0m; }
        }

        private static string Str(object o)
        {
            return o == null || o == DBNull.Value ? "" : Convert.ToString(o, CultureInfo.InvariantCulture);
        }

        private static IEnumerable<DataRow> Rows(DataTable dt)
        {
            if (dt == null) yield break;
            foreach (DataRow r in dt.Rows) yield return r;
        }

        private static bool TryParseTime(string hhmm, out TimeSpan t)
        {
            t = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(hhmm)) return false;
            string s = hhmm.Trim();
            if (TimeSpan.TryParseExact(s, @"hh\:mm", CultureInfo.InvariantCulture, out t)) return true;
            if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out t)) return t >= TimeSpan.Zero && t < TimeSpan.FromDays(1);
            return false;
        }

        private static string Cfg(string conn, string key, string def)
        {
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = @k",
                    new Dictionary<string, object> { { "@k", key } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                {
                    string v = dt.Rows[0][0].ToString().Trim();
                    return v.Length == 0 ? def : v;
                }
            }
            catch { }
            return def;
        }
    }
}
