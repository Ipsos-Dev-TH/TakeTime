using System;
using System.Collections.Generic;
using System.Globalization;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>
    /// ตัวแปลงโมเดล Dining → Dictionary สำหรับ JSON ของหน้าพนักงาน (กระดานออเดอร์ / แผนผังโต๊ะ / badge ใน Site.Master)
    /// ไม่แตะ DB — รับโมเดลที่ service อ่านมาแล้ว · ค่าทุกตัวเป็นข้อความล้วน (หน้าเว็บต้อง escapeHtml ก่อนลง DOM)
    /// วันที่ส่งเป็นข้อความ "HH:mm" + ISO เพื่อเลี่ยงรูปแบบ Date ของ JavaScriptSerializer
    /// </summary>
    public static class DiningStaffView
    {
        /// <summary>สถานะการชำระที่หน้าใช้ติดป้าย: UNPAID | PENDING_SLIP | PAID | CHARGED | REFUNDED</summary>
        public const string PayUnpaid = "UNPAID";
        public const string PayPendingSlip = "PENDING_SLIP";

        public static string PayState(DiningOrder o)
        {
            if (o == null) return PayUnpaid;
            if (o.PaymentStatus == DiningStatus.Pay.Unpaid) return o.PendingPaymentId.HasValue ? PayPendingSlip : PayUnpaid;
            return o.PaymentStatus ?? PayUnpaid;
        }

        public static string PayStateThai(string state)
        {
            switch ((state ?? "").ToUpperInvariant())
            {
                case PayUnpaid: return "ค้างจ่าย";
                case PayPendingSlip: return "สลิปรอตรวจ";
                case DiningStatus.Pay.Paid: return "จ่ายแล้ว";
                case DiningStatus.Pay.Charged: return "ลงบิลห้อง";
                case DiningStatus.Pay.Refunded: return "คืนเงินแล้ว";
                default: return state ?? "";
            }
        }

        public static string TableStatusThai(string status)
        {
            switch ((status ?? "").ToUpperInvariant())
            {
                case DiningStatus.Table.Free: return "ว่าง";
                case DiningStatus.Table.Occupied: return "มีลูกค้า";
                case DiningStatus.Table.BillRequested: return "ขอบิล";
                case DiningStatus.Table.NeedsStaff: return "เรียกพนักงาน";
                case DiningStatus.Table.Disabled: return "ปิดใช้งาน";
                default: return status ?? "";
            }
        }

        /// <summary>"3 นาที" / "1 ชม. 12 นาที" / "เมื่อสักครู่"</summary>
        public static string AgeText(double minutes)
        {
            if (minutes < 1) return "เมื่อสักครู่";
            int m = (int)Math.Floor(minutes);
            if (m < 60) return m + " นาที";
            int h = m / 60; m = m % 60;
            if (h < 24) return h + " ชม." + (m > 0 ? " " + m + " นาที" : "");
            return (h / 24) + " วัน";
        }

        /// <summary>"ข้าวผัด x2, ชาเย็น x1" (ตัดที่ max รายการ)</summary>
        public static string ItemsPreview(DiningOrder o, int max)
        {
            if (o == null || o.Items == null || o.Items.Count == 0) return "";
            var parts = new List<string>();
            int n = 0;
            foreach (var it in o.Items)
            {
                if (it.KitchenStatus == DiningStatus.Kitchen.Void) continue;
                if (n++ >= max) { parts.Add("…"); break; }
                parts.Add((it.ProductName ?? "") + " x" + it.Qty.ToString("0.##", CultureInfo.InvariantCulture));
            }
            return string.Join(", ", parts.ToArray());
        }

        public static string Money(decimal v) { return v.ToString("N2", CultureInfo.InvariantCulture); }

        private static string Hm(DateTime? d) { return d.HasValue ? d.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : ""; }
        private static string Iso(DateTime? d) { return d.HasValue ? d.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) : ""; }

        public static Dictionary<string, object> Order(DiningOrder o, DateTime now)
        {
            var d = new Dictionary<string, object>();
            if (o == null) return d;
            d["id"] = o.Id;
            d["orderNo"] = o.OrderNo ?? "";
            d["sessionId"] = o.SessionId;
            d["tableId"] = o.TableId;
            d["zoneId"] = o.ZoneId;
            d["tableCode"] = o.TableCode ?? "";
            d["tableName"] = string.IsNullOrEmpty(o.TableName) ? (o.TableCode ?? "") : o.TableName;
            d["zoneName"] = o.ZoneName ?? "";
            d["round"] = o.RoundNo;
            d["status"] = o.OrderStatus ?? "";
            d["statusTh"] = DiningStatus.Order.Thai(o.OrderStatus);
            string ps = PayState(o);
            d["payState"] = ps;
            d["payStateTh"] = PayStateThai(ps);
            d["paymentId"] = o.PaymentId;
            d["pendingPaymentId"] = o.PendingPaymentId;
            d["total"] = o.TotalAmount;
            d["totalText"] = Money(o.TotalAmount);
            d["vat"] = o.VatAmount;
            d["guestNote"] = o.GuestNote ?? "";
            d["guestName"] = o.GuestName ?? "";
            d["guestPhone"] = o.GuestPhone ?? "";
            d["cancelReason"] = o.CancelReason ?? "";
            d["source"] = o.SourceChannel ?? "";
            d["createdAt"] = Iso(o.CreatedAt);
            d["createdHm"] = Hm(o.CreatedAt);
            d["createdDate"] = o.CreatedAt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            double age = Math.Max(0, (now - o.CreatedAt).TotalMinutes);
            d["ageMin"] = Math.Round(age, 1);
            d["ageText"] = AgeText(age);
            d["acceptedHm"] = Hm(o.AcceptedAt);
            d["servedHm"] = Hm(o.ServedAt);
            d["isOutstanding"] = o.IsOutstanding;
            d["isPayable"] = o.IsPayable;
            d["isSettled"] = o.IsSettled;
            d["canAccept"] = DiningStatus.Order.CanTransition(o.OrderStatus, DiningStatus.Order.Accepted);
            d["canPrepare"] = DiningStatus.Order.CanTransition(o.OrderStatus, DiningStatus.Order.Preparing);
            d["canServe"] = DiningStatus.Order.CanTransition(o.OrderStatus, DiningStatus.Order.Served);
            d["canCancel"] = DiningStatus.Order.CanTransition(o.OrderStatus, DiningStatus.Order.Cancelled) && o.PaymentStatus != DiningStatus.Pay.Charged;
            d["itemsPreview"] = ItemsPreview(o, 3);
            var items = new List<Dictionary<string, object>>();
            if (o.Items != null)
                foreach (var it in o.Items)
                {
                    var x = new Dictionary<string, object>();
                    x["id"] = it.Id;
                    x["productId"] = it.ProductId;
                    x["name"] = it.ProductName ?? "";
                    x["qty"] = it.Qty;
                    x["qtyText"] = it.Qty.ToString("0.##", CultureInfo.InvariantCulture);
                    x["unitPrice"] = it.UnitPrice;
                    x["optionsPrice"] = it.OptionsPrice;
                    x["lineTotal"] = it.LineTotal;
                    x["lineTotalText"] = Money(it.LineTotal);
                    x["options"] = it.OptionsText ?? "";
                    x["note"] = it.ItemNote ?? "";
                    x["kitchen"] = it.KitchenStatus ?? "";
                    x["station"] = it.Station ?? "";
                    items.Add(x);
                }
            d["items"] = items;
            return d;
        }

        public static Dictionary<string, object> Table(TableBoardRow r)
        {
            var d = new Dictionary<string, object>();
            if (r == null || r.Table == null) return d;
            var t = r.Table;
            d["tableId"] = t.Id;
            d["zoneId"] = t.ZoneId;
            d["zoneCode"] = t.ZoneCode ?? "";
            d["zoneName"] = t.ZoneName ?? "";
            d["code"] = t.Code ?? "";
            d["name"] = t.DisplayName ?? "";
            d["seats"] = t.Seats;
            d["status"] = t.Status ?? DiningStatus.Table.Free;
            d["statusTh"] = TableStatusThai(t.Status);
            d["isActive"] = t.IsActive;
            d["qrToken"] = t.QrToken ?? "";
            d["sessionId"] = r.SessionId;
            d["sessionStatus"] = r.SessionStatus ?? "";
            d["openedHm"] = Hm(r.SessionOpenedAt);
            d["openedAt"] = Iso(r.SessionOpenedAt);
            d["billRequested"] = r.BillRequested;
            d["needsStaff"] = t.Status == DiningStatus.Table.NeedsStaff;
            d["openOrders"] = r.OpenOrders;
            d["totalOrders"] = r.TotalOrders;
            d["outstanding"] = r.Outstanding;
            d["outstandingText"] = Money(r.Outstanding);
            d["pendingSlip"] = r.PendingSlipAmount;
            d["pendingSlipText"] = Money(r.PendingSlipAmount);
            d["payable"] = Math.Max(0m, r.Outstanding - r.PendingSlipAmount);
            d["latestHm"] = Hm(r.LatestOrderAt);
            d["ageMin"] = r.LatestOrderAgeMinutes.HasValue ? (object)Math.Round(r.LatestOrderAgeMinutes.Value, 1) : null;
            d["ageText"] = r.LatestOrderAgeMinutes.HasValue ? AgeText(r.LatestOrderAgeMinutes.Value) : "";
            return d;
        }

        /// <summary>รายการชำระ (สลิปรอตรวจ / ประวัติใน session) — slipUrl = path เว็บ ("~/Uploads/…") หน้าต้อง ResolveUrl เอง</summary>
        public static Dictionary<string, object> Payment(DiningPayment p, DiningTableSession s, DateTime now)
        {
            var d = new Dictionary<string, object>();
            if (p == null) return d;
            d["id"] = p.Id;
            d["sessionId"] = p.SessionId;
            d["method"] = p.Method ?? "";
            d["methodTh"] = DiningStatus.Method.Thai(p.Method);
            d["status"] = p.Status ?? "";
            d["amount"] = p.Amount;
            d["amountText"] = Money(p.Amount);
            d["paidHowName"] = p.PaidHowName ?? "";
            d["paidHm"] = Hm(p.PaidAt);
            d["createdHm"] = Hm(p.CreatedAt);
            d["createdDate"] = p.CreatedAt == DateTime.MinValue ? "" : p.CreatedAt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            double age = p.CreatedAt == DateTime.MinValue ? 0 : Math.Max(0, (now - p.CreatedAt).TotalMinutes);
            d["ageText"] = AgeText(age);
            d["slipPath"] = p.SlipPath ?? "";
            d["slipVerify"] = p.SlipVerifyStatus ?? "";
            d["slipBankAmount"] = p.SlipBankAmount;
            d["slipTransRef"] = p.SlipTransRef ?? "";
            d["slipNote"] = p.SlipNote ?? "";
            d["roomChargeRef"] = p.RoomChargeRef ?? "";
            d["coversOrderIds"] = p.CoversOrderIds ?? new List<long>();
            d["acctPostRef"] = p.AcctPostRef ?? "";
            d["isPending"] = p.IsPending;
            d["tableLabel"] = s != null ? s.Label : "";
            d["tableId"] = s != null ? (object)s.TableId : null;
            d["tableName"] = s != null ? (string.IsNullOrEmpty(s.TableName) ? s.TableCode : s.TableName) : "";
            d["zoneName"] = s != null ? (s.ZoneName ?? "") : "";
            return d;
        }

        public static Dictionary<string, object> Session(DiningTableSession s)
        {
            var d = new Dictionary<string, object>();
            if (s == null) return d;
            d["id"] = s.Id;
            d["tableId"] = s.TableId;
            d["tableCode"] = s.TableCode ?? "";
            d["tableName"] = string.IsNullOrEmpty(s.TableName) ? (s.TableCode ?? "") : s.TableName;
            d["zoneId"] = s.ZoneId;
            d["zoneName"] = s.ZoneName ?? "";
            d["label"] = s.Label;
            d["status"] = s.Status ?? "";
            d["isOpen"] = s.IsOpen;
            d["openedHm"] = Hm(s.OpenedAt);
            d["openedDate"] = s.OpenedAt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            d["closedHm"] = Hm(s.ClosedAt);
            d["closeReason"] = s.CloseReason ?? "";
            d["guestName"] = s.GuestName ?? "";
            d["guestPhone"] = s.GuestPhone ?? "";
            d["pax"] = s.Pax;
            d["billRequested"] = s.Status == DiningStatus.Session.BillRequested;
            d["billRequestedHm"] = Hm(s.BillRequestedAt);
            d["allowCash"] = s.AllowCash;
            d["allowTransfer"] = s.AllowTransfer;
            d["allowRoomCharge"] = s.AllowRoomCharge;
            return d;
        }

        public static Dictionary<string, object> Outstanding(OutstandingInfo o)
        {
            var d = new Dictionary<string, object>();
            if (o == null) o = new OutstandingInfo();
            d["amount"] = o.Amount;
            d["amountText"] = Money(o.Amount);
            d["pendingSlip"] = o.PendingSlipAmount;
            d["pendingSlipText"] = Money(o.PendingSlipAmount);
            d["payable"] = o.PayableAmount;
            d["payableText"] = Money(o.PayableAmount);
            d["orderIds"] = o.OrderIds ?? new List<long>();
            d["payableOrderIds"] = o.PayableOrderIds ?? new List<long>();
            d["pendingSlipOrderIds"] = o.PendingSlipOrderIds ?? new List<long>();
            return d;
        }

        public static Dictionary<string, object> Poll(DiningBoardPoll p, DateTime now)
        {
            var d = new Dictionary<string, object>();
            if (p == null) p = new DiningBoardPoll();
            d["latestTicks"] = p.LatestTicks;
            d["newCount"] = p.NewCount;
            d["pendingSlipCount"] = p.PendingSlipCount;
            d["callStaffCount"] = p.CallStaffCount;
            d["billRequestedCount"] = p.BillRequestedCount;
            d["total"] = p.NewCount + p.PendingSlipCount + p.CallStaffCount + p.BillRequestedCount;
            var list = new List<Dictionary<string, object>>();
            if (p.NewOrders != null) foreach (var o in p.NewOrders) list.Add(Order(o, now));
            d["newOrders"] = list;
            return d;
        }

        public static Dictionary<string, object> Result(DiningResult r)
        {
            var d = new Dictionary<string, object>();
            if (r == null) { d["ok"] = false; d["message"] = "ไม่มีผลลัพธ์"; d["code"] = DiningError.Internal; return d; }
            d["ok"] = r.Ok;
            d["message"] = r.Message ?? "";
            d["code"] = r.ErrorCode ?? "";
            return d;
        }
    }
}
