using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web.UI;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// หน้า "ตารางการจองรายวัน" สำหรับ render เป็นรูปส่งเข้า LINE โดยเฉพาะ
    /// (แยกจาก DisplayToday เดิม เพื่อไม่กระทบหน้าที่ใช้แสดงบนจอ)
    /// เพิ่มข้อมูลที่ทีมหน้างานต้องใช้: เข้า/ออกวันนี้, จำนวนครั้งที่เคยมาพัก, ยอดค้างชำระ, ช่องทางจอง
    /// รองรับ ?date=yyyy-MM-dd เพื่อดูย้อนหลัง/ล่วงหน้า
    /// </summary>
    public partial class DailyBoard : Page
    {
        private readonly string _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private readonly code _code = new code();

        protected void Page_Load(object sender, EventArgs e)
        {
            DateTime day = DateTime.Today;
            if (!string.IsNullOrEmpty(Request.QueryString["date"])
                && DateTime.TryParse(Request.QueryString["date"], out var d)) day = d.Date;

            try { litBoard.Text = BuildBoard(day); }
            catch (Exception ex)
            {
                litBoard.Text = $"<div class='empty'>โหลดข้อมูลไม่สำเร็จ<br/><span style='font-size:14px'>{Server.HtmlEncode(ex.Message)}</span></div>";
            }
        }

        private string BuildBoard(DateTime day)
        {
            var p = new Dictionary<string, object> { { "@d", day.ToString("yyyy-MM-dd") } };

            // คอลัมน์เสริมที่มาจาก migration รุ่นหลัง (Channel Manager / email intake) — บาง
            // ฐานข้อมูลยังไม่ได้รัน จึงต้องตรวจก่อนใส่ใน SELECT ไม่งั้นทั้งหน้าพัง
            string otaCols = "";
            if (HasColumn("Reservation", "OTA_Channel")) otaCols += ", r.OTA_Channel";
            if (HasColumn("Reservation", "OTA_Booking_ID")) otaCols += ", r.OTA_Booking_ID";

            // การจองที่ "มีผู้พักอยู่" ในวันนี้
            DataTable dt = _code.DatabaseQuerySafe(_conn,
                $@"SELECT r.ID, r.Customer_MobilePhone, r.CheckinDate, r.CheckoutDate, r.StayDays,
                          r.TotalPrice, r.Deposit, r.Remark, r.Reserve_By, r.Status{otaCols},
                          c.Name, c.NickName
                     FROM Reservation r
                     INNER JOIN Customer c ON c.MobilePhone = r.Customer_MobilePhone
                    WHERE @d >= r.CheckinDate AND @d < r.CheckoutDate
                      AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')", p);

            if (dt == null || dt.Rows.Count == 0)
                return Header(day, 0, 0, 0, 0, 0) +
                       "<div class='empty'>ไม่มีผู้เข้าพักในวันนี้</div>";

            // ห้องพักต่อการจอง
            DataTable accom = _code.DatabaseQuerySafe(_conn,
                @"SELECT ra.Reservation_ID, a.AccomName, a.OrderID, ra.Amount, a.LimitWithPeople
                    FROM Reservation r
                    INNER JOIN Reservation_Accommodation ra ON ra.Reservation_ID = r.ID
                    INNER JOIN Accommodation a ON a.ID = ra.Accommodation_ID
                   WHERE @d >= r.CheckinDate AND @d < r.CheckoutDate
                   ORDER BY a.OrderID", p);

            // ของเช่าต่อการจอง
            DataTable items = SafeQuery(
                @"SELECT ri.Reservation_ID, i.ItemName, ri.Amount
                    FROM Reservation r
                    INNER JOIN Reservation_Items ri ON ri.Reservation_ID = r.ID
                    INNER JOIN Items i ON i.ID = ri.Items_ID
                   WHERE @d >= r.CheckinDate AND @d < r.CheckoutDate", p);

            // จำนวนครั้งที่เคยมาพัก (นับเฉพาะที่เช็คเอาท์ไปแล้ว — ไม่รวมครั้งปัจจุบัน)
            DataTable visits = _code.DatabaseQuerySafe(_conn,
                @"SELECT Customer_MobilePhone, COUNT(*) AS Visits
                    FROM Reservation
                   WHERE CheckoutDate <= @d
                     AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                   GROUP BY Customer_MobilePhone", p);

            // ค่าใช้จ่ายในห้อง + ยอดที่ชำระแล้ว (รวมทีเดียว ไม่ query ต่อแถว)
            DataTable charges = SafeQuery(
                @"SELECT rpc.Reservation_ID, SUM(rpc.TotalAmount) AS Charges
                    FROM Reservation_Product_Charges rpc
                    INNER JOIN Reservation r ON r.ID = rpc.Reservation_ID
                   WHERE @d >= r.CheckinDate AND @d < r.CheckoutDate AND rpc.Status <> 'CANCELLED'
                   GROUP BY rpc.Reservation_ID", p);

            DataTable paid = SafeQuery(
                @"SELECT ph.Reservation_ID, SUM(ph.PaymentAmount) AS Paid
                    FROM Payment_History ph
                    INNER JOIN Reservation r ON r.ID = ph.Reservation_ID
                   WHERE @d >= r.CheckinDate AND @d < r.CheckoutDate AND ph.Status = 'COMPLETED'
                   GROUP BY ph.Reservation_ID", p);

            var visitMap = MapByKey(visits, "Customer_MobilePhone", "Visits");
            var chargeMap = MapById(charges, "Reservation_ID", "Charges");
            var paidMap = MapById(paid, "Reservation_ID", "Paid");

            // สร้างแถว + จัดเรียงตามลำดับห้อง
            var rows = new List<BoardRow>();
            int roomCount = 0, checkIn = 0, checkOut = 0;
            decimal dueTotal = 0;

            foreach (DataRow r in dt.Rows)
            {
                int resId = Convert.ToInt32(r["ID"]);
                var row = new BoardRow { ResId = resId, Order = 9999 };

                // ห้องพัก
                var names = new List<string>();
                foreach (DataRow a in Rows(accom))
                {
                    if (Convert.ToInt32(a["Reservation_ID"]) != resId) continue;
                    string nm = a["AccomName"].ToString();
                    if (a["LimitWithPeople"] != DBNull.Value && a["LimitWithPeople"].ToString() == "True")
                        nm += $" ({a["Amount"]} คน)";
                    names.Add(nm);
                    roomCount++;
                    int ord = a["OrderID"] != DBNull.Value ? Convert.ToInt32(a["OrderID"]) : 9999;
                    if (ord < row.Order) row.Order = ord;
                }
                row.Rooms = names.Count > 0 ? string.Join(" · ", names) : "-";

                // ของเช่า
                var it = new List<string>();
                foreach (DataRow i in Rows(items))
                    if (Convert.ToInt32(i["Reservation_ID"]) == resId)
                        it.Add($"{i["ItemName"]} ×{i["Amount"]}");
                row.Items = it.Count > 0 ? string.Join(", ", it) : "-";

                // ผู้เข้าพัก
                string name = r["Name"]?.ToString() ?? "";
                string nick = r["NickName"]?.ToString() ?? "";
                row.Guest = string.IsNullOrWhiteSpace(nick) ? name : $"{name} ({nick})";
                row.Phone = r["Customer_MobilePhone"]?.ToString() ?? "";

                // วันเข้า-ออก + สถานะของวันนี้
                DateTime ci = Convert.ToDateTime(r["CheckinDate"]);
                DateTime co = Convert.ToDateTime(r["CheckoutDate"]);
                row.CheckIn = ci; row.CheckOut = co;
                row.Nights = r["StayDays"] != DBNull.Value ? Convert.ToInt32(r["StayDays"]) : (int)(co - ci).TotalDays;
                row.IsArrival = ci.Date == day;
                row.IsDeparture = co.Date == day.AddDays(1);   // ออกพรุ่งนี้เช้า = คืนสุดท้าย
                if (row.IsArrival) checkIn++;
                if (row.IsDeparture) checkOut++;

                // เงิน
                decimal total = r["TotalPrice"] != DBNull.Value ? Convert.ToDecimal(r["TotalPrice"]) : 0m;
                decimal extra = chargeMap.ContainsKey(resId) ? chargeMap[resId] : 0m;
                decimal pd = paidMap.ContainsKey(resId) ? paidMap[resId] : 0m;
                row.Total = total + extra;
                row.Extra = extra;
                row.Paid = pd;
                row.Due = row.Total - pd;
                if (row.Due > 0) dueTotal += row.Due;

                // จำนวนครั้งที่เคยมา
                row.PastVisits = visitMap.ContainsKey(row.Phone) ? visitMap[row.Phone] : 0;

                // ช่องทาง / หมายเหตุ (OTA_Channel อาจไม่มีคอลัมน์ในบางฐานข้อมูล)
                string ota = dt.Columns.Contains("OTA_Channel") && r["OTA_Channel"] != DBNull.Value
                    ? r["OTA_Channel"].ToString() : "";
                row.Channel = !string.IsNullOrWhiteSpace(ota) ? ota : (r["Reserve_By"]?.ToString() ?? "");
                row.Remark = r["Remark"]?.ToString() ?? "";
                row.Status = r["Status"]?.ToString() ?? "";

                rows.Add(row);
            }

            rows.Sort((a, b) => a.Order.CompareTo(b.Order));

            var sb = new StringBuilder();
            sb.Append(Header(day, rows.Count, roomCount, checkIn, checkOut, dueTotal));
            sb.Append("<table class='main'><tr>");
            sb.Append("<th>ห้องพัก</th><th>ผู้เข้าพัก</th><th>วันนี้</th><th>เข้า - ออก</th>");
            sb.Append("<th>เคยมา</th><th>ของเช่า</th><th class='num'>ยอดรวม</th><th class='num'>รับแล้ว</th>");
            sb.Append("<th class='num'>คงเหลือ</th><th>ช่องทาง / หมายเหตุ</th>");
            sb.Append("</tr>");

            bool alt = false;
            foreach (var row in rows)
            {
                sb.Append(alt ? "<tr class='alt'>" : "<tr>");
                alt = !alt;

                sb.Append($"<td><span class='room'>{E(row.Rooms)}</span></td>");
                sb.Append($"<td><span class='guest'>{E(row.Guest)}</span><div class='sub'>{E(row.Phone)}</div></td>");

                // ป้ายสถานะวันนี้ — สิ่งที่ทีมหน้างานต้องเห็นก่อนเพื่อน
                string tag = row.IsArrival ? "<span class='tag t-in'>เข้าวันนี้</span>"
                           : row.IsDeparture ? "<span class='tag t-out'>ออกพรุ่งนี้</span>"
                           : "<span class='tag t-stay'>พักต่อ</span>";
                sb.Append($"<td>{tag}</td>");

                sb.Append($"<td>{row.CheckIn:dd/MM} - {row.CheckOut:dd/MM}<div class='sub'>{row.Nights} คืน</div></td>");

                // จำนวนครั้งที่เคยมาพัก
                string visitCell = row.PastVisits > 0
                    ? $"<span class='tag t-vip'>ครั้งที่ {row.PastVisits + 1}</span><div class='sub'>เคยมา {row.PastVisits} ครั้ง</div>"
                    : "<span class='tag t-new'>ลูกค้าใหม่</span>";
                sb.Append($"<td>{visitCell}</td>");

                sb.Append($"<td>{E(row.Items)}");
                if (row.Extra > 0) sb.Append($"<div class='sub'>ค่าใช้จ่ายในห้อง {row.Extra:N0}</div>");
                sb.Append("</td>");

                sb.Append($"<td class='num'>{row.Total:N0}</td>");
                sb.Append($"<td class='num'>{row.Paid:N0}</td>");
                sb.Append(row.Due > 0
                    ? $"<td class='num due'>{row.Due:N0}</td>"
                    : "<td class='num paid'>ครบแล้ว</td>");

                string note = E(row.Channel);
                if (!string.IsNullOrWhiteSpace(row.Remark))
                    note += $"<div class='sub'>{E(Shorten(row.Remark, 70))}</div>";
                sb.Append($"<td>{note}</td>");

                sb.Append("</tr>");
            }

            sb.Append("</table>");
            sb.Append($"<div class='foot'>สร้างเมื่อ {DateTime.Now:dd/MM/yyyy HH:mm} น. · Take Time Nature Resort</div>");
            return sb.ToString();
        }

        private string Header(DateTime day, int bookings, int rooms, int checkIn, int checkOut, decimal due)
        {
            string thaiDate;
            try { thaiDate = day.ToString("dddd d MMMM yyyy", new CultureInfo("th-TH")); }
            catch { thaiDate = day.ToString("dd/MM/yyyy"); }

            var sb = new StringBuilder();
            sb.Append("<div class='hd'>");
            sb.Append($"<div class='t1'>ตารางการจอง · {E(thaiDate)}</div>");
            sb.Append("<div class='t2'>สรุปผู้เข้าพักประจำวัน</div>");
            sb.Append("</div>");

            sb.Append("<table class='kpi'><tr>");
            sb.Append($"<td><div class='n'>{bookings}</div><div class='l'>การจอง</div></td>");
            sb.Append($"<td><div class='n'>{rooms}</div><div class='l'>ห้องที่มีผู้พัก</div></td>");
            sb.Append($"<td><div class='n'>{checkIn}</div><div class='l'>เข้าวันนี้</div></td>");
            sb.Append($"<td><div class='n'>{checkOut}</div><div class='l'>ออกพรุ่งนี้</div></td>");
            sb.Append(due > 0
                ? $"<td class='warn'><div class='n'>{due:N0}</div><div class='l'>ยอดค้างชำระ</div></td>"
                : "<td><div class='n'>0</div><div class='l'>ยอดค้างชำระ</div></td>");
            sb.Append("</tr></table>");
            return sb.ToString();
        }

        // ── helpers ───────────────────────────────────────────────────────────────
        private class BoardRow
        {
            public int ResId, Order, Nights, PastVisits;
            public string Rooms, Guest, Phone, Items, Channel, Remark, Status;
            public DateTime CheckIn, CheckOut;
            public bool IsArrival, IsDeparture;
            public decimal Total, Paid, Due, Extra;
        }

        private static Dictionary<string, int> MapByKey(DataTable dt, string keyCol, string valCol)
        {
            var m = new Dictionary<string, int>();
            if (dt == null) return m;
            foreach (DataRow r in dt.Rows)
            {
                string k = r[keyCol]?.ToString();
                if (string.IsNullOrEmpty(k)) continue;
                m[k] = r[valCol] != DBNull.Value ? Convert.ToInt32(r[valCol]) : 0;
            }
            return m;
        }

        private static Dictionary<int, decimal> MapById(DataTable dt, string keyCol, string valCol)
        {
            var m = new Dictionary<int, decimal>();
            if (dt == null) return m;
            foreach (DataRow r in dt.Rows)
            {
                if (r[keyCol] == DBNull.Value) continue;
                m[Convert.ToInt32(r[keyCol])] = r[valCol] != DBNull.Value ? Convert.ToDecimal(r[valCol]) : 0m;
            }
            return m;
        }

        /// <summary>ตรวจว่ามีคอลัมน์นี้จริงไหม — กันหน้าพังเมื่อฐานข้อมูลยังไม่ได้รัน migration บางตัว</summary>
        private bool HasColumn(string table, string column)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_NAME = @t AND COLUMN_NAME = @c",
                    new Dictionary<string, object> { { "@t", table }, { "@c", column } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>query ที่พึ่งตารางเสริม — ถ้าตารางยังไม่มีให้คืน null แทนการโยน error</summary>
        private DataTable SafeQuery(string sql, Dictionary<string, object> p)
        {
            try { return _code.DatabaseQuerySafe(_conn, sql, p); }
            catch { return null; }
        }

        /// <summary>วนแถวได้เสมอแม้ตารางเป็น null (ตารางเสริมที่ยังไม่มีในฐานข้อมูล)</summary>
        private static IEnumerable<DataRow> Rows(DataTable dt)
        {
            if (dt == null) yield break;
            foreach (DataRow r in dt.Rows) yield return r;
        }

        private static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length > max ? s.Substring(0, max) + "..." : s;
        }

        private string E(string s) => Server.HtmlEncode(s ?? "");
    }
}
