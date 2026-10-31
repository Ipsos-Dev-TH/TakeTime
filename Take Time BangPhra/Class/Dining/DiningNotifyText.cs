using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>ข้อความแจ้งเตือน 1 เหตุการณ์ (HTML สำหรับ Telegram + ข้อความล้วนสำหรับในระบบ/แชท)</summary>
    public sealed class DiningNotifyMessage
    {
        /// <summary>รหัสเหตุการณ์ Notify.Ev (ORDER_DINING / ORDER_DINING_STATUS / DINING_CALL_STAFF / DINING_BILL)</summary>
        public string Event;
        public string Title;
        /// <summary>Telegram HTML (&lt;b&gt; เท่านั้น — ข้อความลูกค้าถูก escape แล้ว)</summary>
        public string Html;
        /// <summary>ข้อความล้วน (ถอดแท็ก) — NotificationService / กล่องแชทรวม</summary>
        public string Text;
        /// <summary>HIGH | NORMAL</summary>
        public string Priority = "NORMAL";
        /// <summary>ชนิดแจ้งเตือนในระบบ (NotificationService) — DINING_ORDER เสมอ</summary>
        public string NotificationType = "DINING_ORDER";
    }

    /// <summary>
    /// ประกอบข้อความไทยของทุกเหตุการณ์ QR สั่งอาหาร — <b>บริสุทธิ์</b> (ไม่มี DB/HttpContext/Notify) ทดสอบด้วย mono ได้
    /// ทุกข้อความจากลูกค้า (ชื่อ/หมายเหตุ/ตัวเลือก/ข้อความเรียกพนักงาน) ถูก escape ด้วย <see cref="E"/> ก่อนลง HTML
    /// (Telegram parse_mode HTML — &lt; &gt; &amp; ในข้อความดิบทำให้ส่งไม่ผ่าน/แทรกแท็กได้)
    /// รูปแบบตามต้นแบบ AmenityService.NotifyNewRequest (:453) — หัวเรื่อง + รายการ + ยอด + เวลา + ลิงก์
    /// </summary>
    public static class DiningNotifyText
    {
        // ตรงกับ Notify.Ev (Class/Notify.cs) — ใส่ซ้ำที่นี่เพื่อให้ไฟล์บริสุทธิ์
        public const string EvOrder = "ORDER_DINING";
        public const string EvOrderStatus = "ORDER_DINING_STATUS";
        public const string EvCallStaff = "DINING_CALL_STAFF";
        public const string EvBill = "DINING_BILL";

        /// <summary>escape สำหรับ Telegram HTML (เหมือน Notify.E)</summary>
        public static string E(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        public static string StripHtml(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = Regex.Replace(s, "<[^>]+>", "");
            return t.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&amp;", "&");
        }

        private static string Money(decimal v) { return v.ToString("N2", CultureInfo.InvariantCulture); }
        private static string Stamp(DateTime t) { return t.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture); }

        private static string TableLabel(DiningTableSession s)
        {
            if (s == null) return "โต๊ะ ?";
            return E(s.Label);
        }

        /// <summary>บรรทัดรายการ "• ข้าวผัด x2 — 120.00 (เผ็ดมาก) ✎ ไม่ใส่ผัก"</summary>
        public static string ItemLine(DiningOrderItem it)
        {
            var sb = new StringBuilder();
            sb.Append("• ").Append(E(it.ProductName)).Append(" x").Append(it.Qty.ToString("0.##", CultureInfo.InvariantCulture));
            sb.Append(" — ").Append(Money(it.LineTotal)).Append(" บาท");
            string opt = it.OptionsText;
            if (!string.IsNullOrEmpty(opt)) sb.Append(" (").Append(E(opt)).Append(')');
            if (!string.IsNullOrWhiteSpace(it.ItemNote)) sb.Append(" ✎ ").Append(E(it.ItemNote.Trim()));
            return sb.ToString();
        }

        private static DiningNotifyMessage Finish(string ev, string title, StringBuilder html, string priority)
        {
            string h = html.ToString().TrimEnd();
            return new DiningNotifyMessage { Event = ev, Title = title, Html = h, Text = StripHtml(h), Priority = priority };
        }

        // ── ออเดอร์ใหม่ ──────────────────────────────────────────────────────

        /// <param name="paymentState">เช่น "💵 เงินสด — ชำระที่โต๊ะ" / "✅ โอนแล้ว ตรวจผ่าน" / "⏳ สลิปรอตรวจ" (ข้อความไทย ไม่มี HTML)</param>
        public static DiningNotifyMessage NewOrder(DiningOrder o, DiningTableSession s, string paymentState, string link)
        {
            var sb = new StringBuilder();
            sb.Append("🍽️ <b>ออเดอร์ใหม่ ").Append(TableLabel(s)).Append(" รอบ ").Append(o.RoundNo).Append("</b>\n");
            sb.Append("🧾 เลขที่: ").Append(E(o.OrderNo)).Append('\n');
            if (!string.IsNullOrWhiteSpace(o.GuestName)) sb.Append("👤 ลูกค้า: ").Append(E(o.GuestName.Trim())).Append('\n');
            sb.Append('\n');
            if (o.Items != null) foreach (var it in o.Items) sb.Append(ItemLine(it)).Append('\n');
            if (!string.IsNullOrWhiteSpace(o.GuestNote)) sb.Append("📝 หมายเหตุ: ").Append(E(o.GuestNote.Trim())).Append('\n');
            sb.Append('\n');
            sb.Append("💰 รวม <b>").Append(Money(o.TotalAmount)).Append("</b> บาท");
            if (!string.IsNullOrWhiteSpace(paymentState)) sb.Append(" · ").Append(E(paymentState));
            sb.Append('\n');
            sb.Append("🕐 ").Append(Stamp(o.CreatedAt == DateTime.MinValue ? DateTime.Now : o.CreatedAt)).Append('\n');
            if (!string.IsNullOrEmpty(link)) sb.Append("🔗 ").Append(E(link)).Append('\n');
            return Finish(EvOrder, "ออเดอร์ใหม่ " + (s != null ? s.Label : "") + " #" + o.OrderNo, sb, "HIGH");
        }

        // ── เปลี่ยนสถานะ ──────────────────────────────────────────────────────

        public static DiningNotifyMessage StatusChanged(DiningOrder o, DiningTableSession s, string oldStatus, string newStatus, string staffName, string reason, string link)
        {
            string icon;
            switch ((newStatus ?? "").ToUpperInvariant())
            {
                case DiningStatus.Order.Accepted: icon = "👍"; break;
                case DiningStatus.Order.Preparing: icon = "👨‍🍳"; break;
                case DiningStatus.Order.Served: icon = "✅"; break;
                case DiningStatus.Order.Cancelled: icon = "❌"; break;
                default: icon = "ℹ️"; break;
            }
            var sb = new StringBuilder();
            sb.Append(icon).Append(" <b>").Append(E(DiningStatus.Order.Thai(newStatus))).Append("</b> — ").Append(TableLabel(s))
              .Append(" #").Append(E(o.OrderNo)).Append('\n');
            sb.Append("จาก ").Append(E(DiningStatus.Order.Thai(oldStatus))).Append(" → ").Append(E(DiningStatus.Order.Thai(newStatus)));
            if (!string.IsNullOrWhiteSpace(staffName)) sb.Append(" โดย ").Append(E(staffName));
            sb.Append('\n');
            if (!string.IsNullOrWhiteSpace(reason)) sb.Append("เหตุผล: ").Append(E(reason.Trim())).Append('\n');
            sb.Append("💰 ").Append(Money(o.TotalAmount)).Append(" บาท · ").Append(E(DiningStatus.Pay.Thai(o.PaymentStatus))).Append('\n');
            sb.Append("🕐 ").Append(Stamp(DateTime.Now)).Append('\n');
            if (!string.IsNullOrEmpty(link)) sb.Append("🔗 ").Append(E(link)).Append('\n');
            return Finish(EvOrderStatus, DiningStatus.Order.Thai(newStatus) + " #" + o.OrderNo, sb,
                newStatus == DiningStatus.Order.Cancelled ? "HIGH" : "NORMAL");
        }

        // ── การชำระ ──────────────────────────────────────────────────────────

        private static void AppendCovered(StringBuilder sb, IList<string> orderNos)
        {
            if (orderNos == null || orderNos.Count == 0) return;
            var esc = new List<string>();
            foreach (string n in orderNos) esc.Add(E(n));
            sb.Append("🧾 ออเดอร์: ").Append(string.Join(", ", esc.ToArray())).Append('\n');
        }

        public static DiningNotifyMessage SlipVerified(DiningPayment p, DiningTableSession s, IList<string> orderNos, string link)
        {
            var sb = new StringBuilder();
            sb.Append("✅ <b>โอนเงินแล้ว — ตรวจสลิปผ่าน</b> ").Append(TableLabel(s)).Append('\n');
            sb.Append("💰 ").Append(Money(p.Amount)).Append(" บาท");
            if (p.SlipBankAmount.HasValue && p.SlipBankAmount.Value != p.Amount) sb.Append(" (สลิป ").Append(Money(p.SlipBankAmount.Value)).Append(')');
            if (!string.IsNullOrWhiteSpace(p.PaidHowName)) sb.Append(" → ").Append(E(p.PaidHowName));
            sb.Append('\n');
            if (!string.IsNullOrEmpty(p.SlipTransRef)) sb.Append("อ้างอิง: ").Append(E(p.SlipTransRef)).Append('\n');
            AppendCovered(sb, orderNos);
            sb.Append("🕐 ").Append(Stamp(DateTime.Now)).Append('\n');
            if (!string.IsNullOrEmpty(link)) sb.Append("🔗 ").Append(E(link)).Append('\n');
            return Finish(EvOrderStatus, "รับโอน " + Money(p.Amount) + " บาท " + (s != null ? s.Label : ""), sb, "NORMAL");
        }

        public static DiningNotifyMessage SlipPending(DiningPayment p, DiningTableSession s, IList<string> orderNos, string reason, string link)
        {
            var sb = new StringBuilder();
            sb.Append("⏳ <b>สลิปรอพนักงานตรวจ</b> ").Append(TableLabel(s)).Append('\n');
            sb.Append("💰 ").Append(Money(p.Amount)).Append(" บาท\n");
            if (!string.IsNullOrWhiteSpace(reason)) sb.Append("ระบบตรวจไม่ผ่าน: ").Append(E(reason)).Append('\n');
            AppendCovered(sb, orderNos);
            sb.Append("กรุณาตรวจกับบัญชีธนาคารแล้วกด \"ยืนยันรับเงิน\" บนกระดาน\n");
            if (!string.IsNullOrEmpty(link)) sb.Append("🔗 ").Append(E(link)).Append('\n');
            return Finish(EvOrderStatus, "สลิปรอตรวจ " + (s != null ? s.Label : ""), sb, "HIGH");
        }

        public static DiningNotifyMessage SlipRejected(DiningTableSession s, decimal amount, string reason)
        {
            var sb = new StringBuilder();
            sb.Append("⚠️ <b>ลูกค้าแนบสลิป ไม่รับ</b> ").Append(TableLabel(s)).Append('\n');
            sb.Append("💰 ยอดที่ต้องชำระ ").Append(Money(amount)).Append(" บาท\n");
            sb.Append("สาเหตุ: ").Append(E(reason ?? "-")).Append('\n');
            sb.Append("ลูกค้าได้รับข้อความให้ติดต่อพนักงาน\n");
            return Finish(EvOrderStatus, "สลิปไม่ผ่าน " + (s != null ? s.Label : ""), sb, "HIGH");
        }

        public static DiningNotifyMessage CashReceived(DiningPayment p, DiningTableSession s, IList<string> orderNos, string staffName)
        {
            var sb = new StringBuilder();
            sb.Append("💵 <b>รับเงินสดแล้ว</b> ").Append(TableLabel(s)).Append('\n');
            sb.Append("💰 ").Append(Money(p.Amount)).Append(" บาท");
            if (!string.IsNullOrWhiteSpace(p.PaidHowName)) sb.Append(" → ").Append(E(p.PaidHowName));
            if (!string.IsNullOrWhiteSpace(staffName)) sb.Append(" · โดย ").Append(E(staffName));
            sb.Append('\n');
            AppendCovered(sb, orderNos);
            sb.Append("🕐 ").Append(Stamp(DateTime.Now)).Append('\n');
            return Finish(EvOrderStatus, "รับเงินสด " + Money(p.Amount) + " บาท " + (s != null ? s.Label : ""), sb, "NORMAL");
        }

        public static DiningNotifyMessage RoomCharged(DiningPayment p, DiningTableSession s, string roomLabel, IList<string> orderNos)
        {
            var sb = new StringBuilder();
            sb.Append("🏨 <b>ลงบิลห้องพัก</b> ").Append(TableLabel(s)).Append('\n');
            sb.Append("💰 ").Append(Money(p.Amount)).Append(" บาท → ห้อง ").Append(E(roomLabel ?? "?"));
            if (p.ReservationId.HasValue) sb.Append(" (การจอง #").Append(p.ReservationId.Value).Append(')');
            sb.Append('\n');
            AppendCovered(sb, orderNos);
            sb.Append("ยอดจะรวมในใบเสร็จตอนเช็คเอาท์\n");
            sb.Append("🕐 ").Append(Stamp(DateTime.Now)).Append('\n');
            return Finish(EvBill, "ลงบิลห้อง " + E(roomLabel ?? "") + " " + Money(p.Amount) + " บาท", sb, "NORMAL");
        }

        public static DiningNotifyMessage RoomChargeLocked(DiningTableSession s, int failures, DateTime lockedUntil)
        {
            var sb = new StringBuilder();
            sb.Append("🚫 <b>ยืนยันเบอร์โทรลงบิลห้องผิดครบ ").Append(failures).Append(" ครั้ง</b> ").Append(TableLabel(s)).Append('\n');
            sb.Append("ล็อกการลงบิลห้องของโต๊ะนี้ถึง ").Append(Stamp(lockedUntil)).Append('\n');
            sb.Append("โปรดตรวจสอบที่โต๊ะ — อาจมีผู้พยายามลงบิลห้องของคนอื่น\n");
            return Finish(EvBill, "ล็อกลงบิลห้อง " + (s != null ? s.Label : ""), sb, "HIGH");
        }

        // ── เรียกพนักงาน / เช็คบิล / ปิดโต๊ะ ───────────────────────────────────

        public static DiningNotifyMessage CallStaff(DiningTableSession s, string message)
        {
            var sb = new StringBuilder();
            sb.Append("🔔 <b>ลูกค้าเรียกพนักงาน</b> ").Append(TableLabel(s)).Append('\n');
            if (!string.IsNullOrWhiteSpace(message)) sb.Append("💬 ").Append(E(message.Trim())).Append('\n');
            sb.Append("🕐 ").Append(Stamp(DateTime.Now)).Append('\n');
            return Finish(EvCallStaff, "เรียกพนักงาน " + (s != null ? s.Label : ""), sb, "HIGH");
        }

        public static DiningNotifyMessage BillRequested(DiningTableSession s, decimal outstanding, int orderCount)
        {
            var sb = new StringBuilder();
            sb.Append("🧾 <b>ขอเช็คบิล</b> ").Append(TableLabel(s)).Append('\n');
            sb.Append("💰 ยอดค้าง <b>").Append(Money(outstanding)).Append("</b> บาท (").Append(orderCount).Append(" ออเดอร์)\n");
            sb.Append(outstanding > 0m ? "ลูกค้าเลือกวิธีชำระบนมือถือได้ (เงินสดที่โต๊ะ / โอน / ลงบิลห้อง)\n" : "ชำระครบแล้ว — ปิดโต๊ะได้\n");
            sb.Append("🕐 ").Append(Stamp(DateTime.Now)).Append('\n');
            return Finish(EvBill, "เช็คบิล " + (s != null ? s.Label : ""), sb, "HIGH");
        }

        public static DiningNotifyMessage TableClosed(DiningTableSession s, string staffName, string reason, decimal outstanding, bool auto)
        {
            var sb = new StringBuilder();
            sb.Append(auto ? "🔒 <b>ปิดโต๊ะอัตโนมัติ</b> " : "🔒 <b>ปิดโต๊ะ</b> ").Append(TableLabel(s)).Append('\n');
            if (!auto && !string.IsNullOrWhiteSpace(staffName)) sb.Append("โดย ").Append(E(staffName)).Append('\n');
            if (!string.IsNullOrWhiteSpace(reason)) sb.Append("เหตุผล: ").Append(E(reason.Trim())).Append('\n');
            if (outstanding > 0m) sb.Append("⚠️ ปิดทั้งที่ยังค้างชำระ ").Append(Money(outstanding)).Append(" บาท\n");
            sb.Append("🕐 ").Append(Stamp(DateTime.Now)).Append('\n');
            return Finish(EvBill, "ปิดโต๊ะ " + (s != null ? s.Label : ""), sb, outstanding > 0m ? "HIGH" : "NORMAL");
        }

        /// <summary>ข้อความสถานะการจ่ายสั้น ๆ สำหรับใส่ใน NewOrder (ตามวิธีที่ลูกค้าเลือกตอนสั่ง)</summary>
        public static string PaymentStateText(string method, bool verified, bool pending)
        {
            switch ((method ?? "").ToUpperInvariant())
            {
                case DiningStatus.Method.Cash: return "💵 เงินสด — ชำระที่โต๊ะ";
                case DiningStatus.Method.Transfer:
                    if (verified) return "✅ โอนแล้ว ตรวจสลิปผ่าน";
                    if (pending) return "⏳ โอนแล้ว สลิปรอตรวจ";
                    return "💳 โอนเงิน — ยังไม่ส่งสลิป";
                case DiningStatus.Method.RoomCharge: return "🏨 ลงบิลห้องพัก";
                default: return "ยังไม่ชำระ (จ่ายตอนเช็คบิล)";
            }
        }
    }
}
