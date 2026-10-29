using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>
    /// ส่งแจ้งเตือนทุกเหตุการณ์ QR สั่งอาหาร (ข้อความจาก <see cref="DiningNotifyText"/>) ไป 3 ทาง — ต้นแบบ AmenityService.NotifyNewRequest:
    ///   1. Telegram/LINE: <c>Notify.Send(Notify.Ev.OrderDining | OrderDiningStatus | DiningCallStaff | DiningBill, html)</c>
    ///      (เปิด/ปิดรายเหตุการณ์ได้ที่ ศูนย์ตั้งค่า → การแจ้งเตือน; seed PHASE19_42 เปิด Telegram ทั้งสี่)
    ///   2. ในระบบ: <c>NotificationService.NotifyAllStaff(title, text, "DINING_ORDER", priority)</c>
    ///   3. กล่องแชทรวม: <c>OmniChannelService.ReceiveMessage("DINING", "table_{tableId}", "โต๊ะ A3 (โซน…)", text, metadata)</c>
    ///      — 1 conversation ต่อโต๊ะ (sp_OmniChannel_AddMessage เปิดใหม่เองเมื่อ RESOLVED) · ปิดโต๊ะ → RESOLVED
    /// ไม่มีทาง throw — แจ้งเตือนพังต้องไม่ทำให้การสั่ง/รับเงินพัง (catch + code.Logs)
    /// </summary>
    public class DiningNotifier
    {
        private readonly string _conn;
        public DiningNotifier() : this(DiningDb.ConnStr) { }
        public DiningNotifier(string connectionString) { _conn = connectionString; }

        // ── เหตุการณ์ ───────────────────────────────────────────────────────

        public void NewOrder(DiningOrder o, DiningTableSession s, string paymentState)
        {
            Dispatch(DiningNotifyText.NewOrder(o, s, paymentState, DiningConfig.OrdersPageUrl(o.Id)), s, o.Id, true);
        }

        public void StatusChanged(DiningOrder o, DiningTableSession s, string oldStatus, string newStatus, string staffName, string reason)
        {
            Dispatch(DiningNotifyText.StatusChanged(o, s, oldStatus, newStatus, staffName, reason, DiningConfig.OrdersPageUrl(o.Id)), s, o.Id, false);
        }

        public void SlipVerified(DiningPayment p, DiningTableSession s, IList<string> orderNos)
        {
            Dispatch(DiningNotifyText.SlipVerified(p, s, orderNos, DiningConfig.BoardUrl()), s, null, true);
        }

        public void SlipPending(DiningPayment p, DiningTableSession s, IList<string> orderNos, string reason)
        {
            Dispatch(DiningNotifyText.SlipPending(p, s, orderNos, reason, DiningConfig.BoardUrl()), s, null, true);
        }

        public void SlipRejected(DiningTableSession s, decimal amount, string reason)
        {
            Dispatch(DiningNotifyText.SlipRejected(s, amount, reason), s, null, true);
        }

        public void CashReceived(DiningPayment p, DiningTableSession s, IList<string> orderNos, string staffName)
        {
            Dispatch(DiningNotifyText.CashReceived(p, s, orderNos, staffName), s, null, false);
        }

        public void RoomCharged(DiningPayment p, DiningTableSession s, string roomLabel, IList<string> orderNos)
        {
            Dispatch(DiningNotifyText.RoomCharged(p, s, roomLabel, orderNos), s, null, true);
        }

        public void RoomChargeLocked(DiningTableSession s, int failures, DateTime lockedUntil)
        {
            Dispatch(DiningNotifyText.RoomChargeLocked(s, failures, lockedUntil), s, null, true);
        }

        public void CallStaff(DiningTableSession s, string message)
        {
            Dispatch(DiningNotifyText.CallStaff(s, message), s, null, true);
        }

        public void BillRequested(DiningTableSession s, decimal outstanding, int orderCount)
        {
            Dispatch(DiningNotifyText.BillRequested(s, outstanding, orderCount), s, null, true);
        }

        /// <summary>ปิดโต๊ะ — แจ้ง + ปิด (RESOLVED) conversation ของโต๊ะในกล่องแชทรวม</summary>
        public void TableClosed(DiningTableSession s, string staffName, string reason, decimal outstanding, bool auto)
        {
            Dispatch(DiningNotifyText.TableClosed(s, staffName, reason, outstanding, auto), s, null, outstanding > 0m);
            ResolveConversation(s);
        }

        // ── ส่ง ────────────────────────────────────────────────────────────

        private void Dispatch(DiningNotifyMessage m, DiningTableSession s, long? orderId, bool toChat)
        {
            if (m == null) return;
            try { Notify.Send(m.Event, m.Html); }
            catch (Exception ex) { DiningDb.Log(_conn, "แจ้งเตือน Telegram ไม่สำเร็จ: " + ex.Message, "SYSTEM"); }

            try
            {
                new NotificationService(_conn).NotifyAllStaff(m.Title, m.Text, m.NotificationType, m.Priority);
            }
            catch (Exception ex) { DiningDb.Log(_conn, "แจ้งเตือนในระบบไม่สำเร็จ: " + ex.Message, "SYSTEM"); }

            if (toChat || orderId.HasValue) PostToChat(m, s, orderId);
        }

        /// <summary>โพสต์เข้ากล่องแชทรวม (ช่องทาง DINING, ผู้ใช้ = table_{id}) — ไม่มีตาราง/ช่องทาง = ข้ามเงียบ</summary>
        private void PostToChat(DiningNotifyMessage m, DiningTableSession s, long? orderId)
        {
            if (s == null || s.TableId <= 0) return;
            try
            {
                if (!DiningDb.TableExists(_conn, "OmniChannel_Channels")) return;
                string meta = "{\"source\":\"DINING\",\"tableId\":" + s.TableId.ToString(CultureInfo.InvariantCulture)
                    + ",\"sessionId\":" + s.Id.ToString(CultureInfo.InvariantCulture)
                    + (orderId.HasValue ? ",\"orderId\":" + orderId.Value.ToString(CultureInfo.InvariantCulture) : "")
                    + ",\"event\":\"" + DiningPricing.JsonEscape(m.Event) + "\"}";
                var r = new OmniChannelService(_conn).ReceiveMessage(DiningConfig.OmniChannel, "table_" + s.TableId,
                    s.Label, m.Text, "TEXT", null, null, null, meta, s.Label, null);
                if (r == null || !r.Success)
                    DiningDb.Log(_conn, "โพสต์เข้ากล่องแชทรวมไม่สำเร็จ: " + (r != null ? r.Error : "?"), "SYSTEM");
            }
            catch (Exception ex) { DiningDb.Log(_conn, "โพสต์เข้ากล่องแชทรวมไม่สำเร็จ: " + ex.Message, "SYSTEM"); }
        }

        /// <summary>ปิด conversation ของโต๊ะ (OPEN/PENDING → RESOLVED)</summary>
        public void ResolveConversation(DiningTableSession s)
        {
            if (s == null || s.TableId <= 0) return;
            try
            {
                if (!DiningDb.TableExists(_conn, "OmniChannel_Conversations")) return;
                var dt = DiningDb.Query(_conn, @"
                    SELECT c.ID FROM OmniChannel_Conversations c
                      JOIN OmniChannel_Contact_Identifiers ci ON ci.ContactID = c.ContactID AND ci.ChannelCode = @ch
                     WHERE ci.PlatformUserId = @u AND c.ChannelCode = @ch AND c.Status IN ('OPEN', 'PENDING')",
                    new Dictionary<string, object> { { "@ch", DiningConfig.OmniChannel }, { "@u", "table_" + s.TableId } });
                var svc = new OmniChannelService(_conn);
                foreach (DataRow r in dt.Rows) svc.UpdateConversationStatus(DiningDb.Long(r["ID"]), "RESOLVED");
            }
            catch (Exception ex) { DiningDb.Log(_conn, "ปิดการสนทนาโต๊ะไม่สำเร็จ: " + ex.Message, "SYSTEM"); }
        }
    }
}
