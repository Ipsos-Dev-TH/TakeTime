using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Services;
using System.Web.UI;
using Take_Time_BangPhra.Dining;
using Take_Time_BangPhra.Payments;
using Take_Time_BangPhra.Payments.SlipCheck;

namespace Take_Time_BangPhra.Admin.Dining
{
    /// <summary>
    /// กระดานออเดอร์ QR สั่งอาหาร (ทีม B) — การ์ดต่อออเดอร์ + ตัวกรอง + เสียง/poll + รับเงินสด/ตรวจสลิป/ปิดโต๊ะ + พิมพ์ kitchen ticket
    ///   • ข้อมูลทั้งหมดส่งเป็น JSON (DiningStaffView) ให้ Scripts/dining-staff.js วาด — ทุกค่าผ่าน escapeHtml ฝั่ง JS
    ///   • WebMethod ทุกตัวตรวจ Feature + Perm.OpsDining ซ้ำเอง (static — ไม่ผ่าน Page_Load); หน้า Tables.aspx เรียก WebMethod ของหน้านี้ร่วมกัน
    ///   • กฎสิทธิ์ (Owner/Admin + เหตุผล) อยู่ใน service ตาม README_API — หน้าแค่ส่ง reason/force
    ///   • Telegram/ในระบบ/แชท service ยิงเองทุกเหตุการณ์ — หน้านี้ไม่เรียก Notify
    /// </summary>
    public partial class Orders : Page
    {
        /// <summary>เวอร์ชัน css/js (cache-bust) — เปลี่ยนเมื่อแก้ dining-staff.css/js</summary>
        public const string AssetVersion = "20261029a";

        protected string BootJson = "{}";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, DiningConfig.FeatureName)) return;
            if (!Perm.Guard(this, Perm.OpsDining)) return;
            if (Session["permission"]?.ToString() != "True") { Response.Redirect("~/Admin/Login"); return; }
            BootJson = new JavaScriptSerializer().Serialize(BuildBoot(this, "board"));
        }

        // ═════════════════════════════════════════════════════════════════════
        // boot (ข้อมูลตั้งต้นของหน้า — ใช้ร่วมกับ Tables.aspx)
        // ═════════════════════════════════════════════════════════════════════

        internal static Dictionary<string, object> BuildBoot(Page page, string mode)
        {
            var boot = new Dictionary<string, object>();
            boot["mode"] = mode;
            boot["api"] = page.ResolveUrl("~/Admin/Dining/Orders.aspx");
            boot["root"] = page.ResolveUrl("~/");
            boot["ordersUrl"] = page.ResolveUrl("~/Admin/Dining/Orders");
            boot["tablesUrl"] = page.ResolveUrl("~/Admin/Dining/Tables");
            boot["zonesUrl"] = page.ResolveUrl("~/Admin/Dining/Zones");
            boot["menuUrl"] = page.ResolveUrl("~/Admin/Dining/Menu");
            boot["creditNotesUrl"] = page.ResolveUrl("~/Account/CreditNotes");
            boot["pollSec"] = DiningConfig.StaffPollSec;
            boot["canOverride"] = SlipVerificationService.CanOverrideCurrentUser();
            boot["staffName"] = DiningDb.StaffName() ?? "";
            boot["ready"] = DiningDb.Ready(DiningDb.ConnStr);

            var ts = new DiningTableService();
            var zones = new List<Dictionary<string, object>>();
            foreach (var z in ts.ListZones(false))
            {
                string msg; bool open = DiningTableService.IsZoneOpen(z, out msg);
                var d = new Dictionary<string, object>();
                d["id"] = z.Id; d["code"] = z.Code ?? ""; d["name"] = z.NameTh ?? ""; d["open"] = open; d["openMsg"] = msg ?? "";
                d["allowCash"] = z.AllowCash; d["allowTransfer"] = z.AllowTransfer; d["allowRoomCharge"] = z.AllowRoomCharge;
                zones.Add(d);
            }
            boot["zones"] = zones;
            var tables = new List<Dictionary<string, object>>();
            foreach (var t in ts.ListTables(null, false))
            {
                var d = new Dictionary<string, object>();
                d["id"] = t.Id; d["zoneId"] = t.ZoneId; d["code"] = t.Code ?? ""; d["name"] = t.DisplayName ?? "";
                tables.Add(d);
            }
            boot["tables"] = tables;
            boot["cashChannels"] = CashChannels();

            var q = page.Request.QueryString;
            var filter = new Dictionary<string, object>();
            filter["zoneId"] = ParseInt(q["zone"]);
            filter["tableId"] = ParseInt(q["table"]);
            filter["status"] = string.IsNullOrEmpty(q["status"]) ? "ACTIVE" : q["status"].Trim().ToUpperInvariant();
            filter["unpaid"] = q["unpaid"] == "1";
            filter["pendingSlip"] = q["slip"] == "1";
            filter["attention"] = q["attn"] == "1";
            boot["filter"] = filter;
            long focus; long.TryParse(q["o"] ?? "", out focus);
            boot["focusOrderId"] = focus;
            return boot;
        }

        /// <summary>แหล่งเงินชนิด CASH ที่พนักงานเลือกได้ (PaymentChannelCatalog.ForStaff("DINING")) — ว่าง = ให้ service เลือกแถว CASH แรกเอง (paidHowId 0)</summary>
        private static List<Dictionary<string, object>> CashChannels()
        {
            var list = new List<Dictionary<string, object>>();
            try
            {
                foreach (var c in PaymentChannelCatalog.ForStaff(DiningConfig.SalesChannel))
                {
                    if (c == null || c.PaidHowId <= 0) continue;
                    if (!string.Equals(c.Type, "CASH", StringComparison.OrdinalIgnoreCase)) continue;
                    var d = new Dictionary<string, object>();
                    d["id"] = c.PaidHowId; d["name"] = string.IsNullOrEmpty(c.Name) ? (c.PaidHowName ?? "") : c.Name;
                    list.Add(d);
                }
            }
            catch { }
            return list;
        }

        private static int ParseInt(string s)
        {
            int v; return int.TryParse(s ?? "", out v) ? v : 0;
        }

        // ═════════════════════════════════════════════════════════════════════
        // guard สำหรับ WebMethod (static — ไม่ผ่าน Page_Load)
        // ═════════════════════════════════════════════════════════════════════

        private static bool Allowed()
        {
            try
            {
                var ctx = HttpContext.Current;
                if (ctx == null || ctx.Session == null) return false;
                if (ctx.Session["permission"]?.ToString() != "True") return false;
                if (!DiningConfig.Enabled) return false;
                return Perm.CanAccess(Perm.OpsDining);
            }
            catch { return false; }
        }

        private static Dictionary<string, object> Deny()
        {
            var d = new Dictionary<string, object>();
            d["ok"] = false; d["code"] = DiningError.Forbidden; d["message"] = "ไม่มีสิทธิ์ หรือหมดเวลาใช้งาน — กรุณาเข้าสู่ระบบใหม่";
            return d;
        }

        private static Dictionary<string, object> Err(Exception ex)
        {
            var d = new Dictionary<string, object>();
            d["ok"] = false; d["code"] = DiningError.Internal; d["message"] = "เกิดข้อผิดพลาด: " + ex.Message;
            try { DiningDb.Log(DiningDb.ConnStr, "กระดานพนักงาน: " + ex.Message, null); } catch { }
            return d;
        }

        private static string ClientIp()
        {
            try { return HttpContext.Current?.Request?.UserHostAddress; } catch { return null; }
        }

        private static string Clean(string s, int max)
        {
            if (s == null) return null;
            s = s.Trim();
            return s.Length > max ? s.Substring(0, max) : s;
        }

        // ═════════════════════════════════════════════════════════════════════
        // กระดานออเดอร์
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// โหลดกระดานตามตัวกรอง — status: ACTIVE (ยังไม่เสิร์ฟ) | NEW | ACCEPTED | PREPARING | SERVED | CANCELLED | ALL ·
        /// attention = เฉพาะโต๊ะที่ขอบิล/เรียกพนักงาน · focusOrderId = ออเดอร์จากลิงก์ Telegram (แนบมาด้วยเสมอแม้ไม่เข้าตัวกรอง)
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object LoadBoard(int zoneId, int tableId, string status, bool unpaid, bool pendingSlip, bool attention, long focusOrderId)
        {
            if (!Allowed()) return Deny();
            try
            {
                var now = DateTime.Now;
                var svc = new DiningOrderService();
                var f = new DiningOrderFilter { Days = 2, Max = 300, UnpaidOnly = unpaid, PendingSlipOnly = pendingSlip };
                if (zoneId > 0) f.ZoneId = zoneId;
                if (tableId > 0) f.TableId = tableId;
                string st = (status ?? "ACTIVE").Trim().ToUpperInvariant();
                if (st != "ALL" && st != "") f.Status = st;
                var orders = svc.ListOrders(f);

                // สถานะโต๊ะ + ยอดค้างต่อ session (สำหรับป้าย/ปุ่มระดับโต๊ะ)
                var board = new DiningTableService().GetBoard(zoneId > 0 ? (int?)zoneId : null);
                var byTable = new Dictionary<int, Dictionary<string, object>>();
                var attentionTables = new HashSet<int>();
                foreach (var r in board)
                {
                    if (r.Table == null) continue;
                    byTable[r.Table.Id] = DiningStaffView.Table(r);
                    if (r.BillRequested || r.Table.Status == DiningStatus.Table.NeedsStaff) attentionTables.Add(r.Table.Id);
                }
                if (attention) orders.RemoveAll(o => !attentionTables.Contains(o.TableId));

                bool hasFocus = false;
                var list = new List<Dictionary<string, object>>();
                foreach (var o in orders)
                {
                    if (focusOrderId > 0 && o.Id == focusOrderId) hasFocus = true;
                    list.Add(DiningStaffView.Order(o, now));
                }
                if (focusOrderId > 0 && !hasFocus)
                {
                    var fo = svc.GetOrder(focusOrderId);
                    if (fo != null)
                    {
                        list.Insert(0, DiningStaffView.Order(fo, now));
                        if (!byTable.ContainsKey(fo.TableId))
                            foreach (var r in new DiningTableService().GetBoard(null))
                                if (r.Table != null && r.Table.Id == fo.TableId) { byTable[fo.TableId] = DiningStaffView.Table(r); break; }
                    }
                }

                var slips = new List<Dictionary<string, object>>();
                var pays = new DiningPaymentService();
                var ts = new DiningTableService();
                var sessCache = new Dictionary<long, DiningTableSession>();
                foreach (var p in pays.ListPendingSlips())
                {
                    DiningTableSession s;
                    if (!sessCache.TryGetValue(p.SessionId, out s)) { s = ts.GetSessionById(p.SessionId); sessCache[p.SessionId] = s; }
                    if (zoneId > 0 && s != null && s.ZoneId != zoneId) continue;
                    if (tableId > 0 && s != null && s.TableId != tableId) continue;
                    slips.Add(DiningStaffView.Payment(p, s, now));
                }

                var poll = svc.GetNewOrdersSince(0);
                var res = new Dictionary<string, object>();
                res["ok"] = true;
                res["ready"] = DiningDb.Ready(DiningDb.ConnStr);
                res["orders"] = list;
                res["tables"] = byTable;
                res["pendingSlips"] = slips;
                res["counts"] = DiningStaffView.Poll(new DiningBoardPoll { NewCount = poll.NewCount, PendingSlipCount = poll.PendingSlipCount, CallStaffCount = poll.CallStaffCount, BillRequestedCount = poll.BillRequestedCount, LatestTicks = poll.LatestTicks }, now);
                res["serverTime"] = now.ToString("HH:mm:ss");
                return res;
            }
            catch (Exception ex) { return Err(ex); }
        }

        /// <summary>poll ออเดอร์ใหม่ + ตัวนับ (ส่ง latestTicks กลับทุกรอบ; 0 = ทั้งหมดที่ยัง NEW)</summary>
        [WebMethod(EnableSession = true)]
        public static object Poll(long sinceTicks)
        {
            if (!Allowed()) return Deny();
            try
            {
                var d = DiningStaffView.Poll(new DiningOrderService().GetNewOrdersSince(sinceTicks), DateTime.Now);
                d["ok"] = true;
                return d;
            }
            catch (Exception ex) { return Err(ex); }
        }

        /// <summary>เปลี่ยนสถานะออเดอร์ (ACCEPTED|PREPARING|SERVED|CANCELLED) — กฎ Owner/Admin + เหตุผล อยู่ใน service</summary>
        [WebMethod(EnableSession = true)]
        public static object SetStatus(long orderId, string status, string reason)
        {
            if (!Allowed()) return Deny();
            try
            {
                var r = new DiningOrderService().UpdateStatus(orderId, status, DiningDb.StaffId(), Clean(reason, 300));
                var d = DiningStaffView.Result(r);
                var o = new DiningOrderService().GetOrder(orderId);
                if (o != null) d["order"] = DiningStaffView.Order(o, DateTime.Now);
                return d;
            }
            catch (Exception ex) { return Err(ex); }
        }

        /// <summary>รับเงินสดทั้งยอดค้างที่จ่ายได้ของ session (paidHowId 0 = แหล่งเงิน CASH แถวแรก)</summary>
        [WebMethod(EnableSession = true)]
        public static object RecordCash(long sessionId, int paidHowId)
        {
            if (!Allowed()) return Deny();
            try
            {
                var r = new DiningPaymentService().RecordCash(sessionId, null, paidHowId, DiningDb.StaffId());
                var d = DiningStaffView.Result(r);
                if (r.Ok && r.Payment != null) { d["paymentId"] = r.Payment.Id; d["amountText"] = DiningStaffView.Money(r.Payment.Amount); d["coveredOrderIds"] = r.CoveredOrderIds; }
                return d;
            }
            catch (Exception ex) { return Err(ex); }
        }

        /// <summary>ยืนยันสลิปที่รอตรวจ — ไม่ผ่าน = Owner/Admin ระบุเหตุผล ≥5 ตัว หรือกดซ้ำใน 10 นาที (service ตัดสิน)</summary>
        [WebMethod(EnableSession = true)]
        public static object ApproveSlip(long paymentId, string reason)
        {
            if (!Allowed()) return Deny();
            try
            {
                var r = new DiningPaymentService().ApprovePendingSlip(paymentId, DiningDb.StaffId(), Clean(reason, 300));
                var d = DiningStaffView.Result(r);
                d["canOverride"] = SlipVerificationService.CanOverrideCurrentUser();
                return d;
            }
            catch (Exception ex) { return Err(ex); }
        }

        [WebMethod(EnableSession = true)]
        public static object RejectSlip(long paymentId, string reason)
        {
            if (!Allowed()) return Deny();
            try
            {
                reason = Clean(reason, 300);
                if (string.IsNullOrWhiteSpace(reason)) return DiningStaffView.Result(DiningResult.Fail(DiningError.Validation, "กรุณาระบุเหตุผลที่ปฏิเสธสลิป"));
                return DiningStaffView.Result(new DiningPaymentService().RejectPendingSlip(paymentId, DiningDb.StaffId(), reason));
            }
            catch (Exception ex) { return Err(ex); }
        }

        /// <summary>ปิดโต๊ะ — ค้างชำระต้อง force (Owner/Admin + เหตุผล ≥5 ตัว; service ตรวจ)</summary>
        [WebMethod(EnableSession = true)]
        public static object CloseTable(long sessionId, string reason, bool force)
        {
            if (!Allowed()) return Deny();
            try { return DiningStaffView.Result(new DiningTableService().CloseSession(sessionId, DiningDb.StaffId(), Clean(reason, 300), force)); }
            catch (Exception ex) { return Err(ex); }
        }

        /// <summary>รับทราบการเรียกพนักงาน (โต๊ะ NEEDS_STAFF → OCCUPIED)</summary>
        [WebMethod(EnableSession = true)]
        public static object AckCall(int tableId)
        {
            if (!Allowed()) return Deny();
            try
            {
                new DiningOrderService().AcknowledgeStaffCall(tableId);
                return DiningStaffView.Result(DiningResult.Success("รับทราบแล้ว"));
            }
            catch (Exception ex) { return Err(ex); }
        }

        // ═════════════════════════════════════════════════════════════════════
        // แผนผังโต๊ะ / รายละเอียด session (ใช้โดย Tables.aspx และแผงข้างของกระดาน)
        // ═════════════════════════════════════════════════════════════════════

        [WebMethod(EnableSession = true)]
        public static object LoadTables(int zoneId)
        {
            if (!Allowed()) return Deny();
            try
            {
                var list = new List<Dictionary<string, object>>();
                foreach (var r in new DiningTableService().GetBoard(zoneId > 0 ? (int?)zoneId : null)) list.Add(DiningStaffView.Table(r));
                var poll = new DiningOrderService().GetNewOrdersSince(0);
                var res = new Dictionary<string, object>();
                res["ok"] = true;
                res["ready"] = DiningDb.Ready(DiningDb.ConnStr);
                res["tables"] = list;
                res["counts"] = DiningStaffView.Poll(new DiningBoardPoll { NewCount = poll.NewCount, PendingSlipCount = poll.PendingSlipCount, CallStaffCount = poll.CallStaffCount, BillRequestedCount = poll.BillRequestedCount, LatestTicks = poll.LatestTicks }, DateTime.Now);
                res["serverTime"] = DateTime.Now.ToString("HH:mm:ss");
                return res;
            }
            catch (Exception ex) { return Err(ex); }
        }

        /// <summary>สรุป session ของโต๊ะ: ออเดอร์ทุกรอบ + การชำระ (รวมสลิปรอตรวจ) + ยอดค้าง</summary>
        [WebMethod(EnableSession = true)]
        public static object SessionDetail(long sessionId)
        {
            if (!Allowed()) return Deny();
            try
            {
                var now = DateTime.Now;
                var ts = new DiningTableService();
                var s = ts.GetSessionById(sessionId);
                if (s == null) return DiningStaffView.Result(DiningResult.Fail(DiningError.NotFound, "ไม่พบ session ของโต๊ะ"));
                var pays = new DiningPaymentService();
                var orders = new List<Dictionary<string, object>>();
                foreach (var o in new DiningOrderService().ListOrders(new DiningOrderFilter { SessionId = sessionId, Max = 200 })) orders.Add(DiningStaffView.Order(o, now));
                orders.Reverse();   // เรียงรอบเก่า → ใหม่
                var payments = new List<Dictionary<string, object>>();
                foreach (var p in pays.ListPayments(sessionId)) payments.Add(DiningStaffView.Payment(p, s, now));
                var res = new Dictionary<string, object>();
                res["ok"] = true;
                res["session"] = DiningStaffView.Session(s);
                res["orders"] = orders;
                res["payments"] = payments;
                res["outstanding"] = DiningStaffView.Outstanding(pays.Outstanding(sessionId));
                var t = ts.GetTable(s.TableId);
                res["tableStatus"] = t != null ? t.Status : "";
                res["tableStatusTh"] = DiningStaffView.TableStatusThai(t != null ? t.Status : "");
                res["orderUrl"] = t != null ? DiningQr.BuildOrderUrl(t.QrToken) : "";
                res["statusUrl"] = DiningQr.BuildStatusUrl(s.SessionToken);
                return res;
            }
            catch (Exception ex) { return Err(ex); }
        }

        /// <summary>
        /// พนักงานเปิดโต๊ะแทนลูกค้า (หรือต่อ session ที่เปิดอยู่) — คืนลิงก์หน้าสั่งอาหาร /Order/{token} ให้พนักงานคีย์ออเดอร์จากเครื่องตัวเอง
        /// (หน้าลูกค้าของทีม A ต่อ session เดิมของโต๊ะให้อัตโนมัติ; sourceChannel=STAFF เป็นเรื่องของหน้านั้น/handler)
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object OpenForGuest(int tableId)
        {
            if (!Allowed()) return Deny();
            try
            {
                var ts = new DiningTableService();
                var t = ts.GetTable(tableId);
                if (t == null) return DiningStaffView.Result(DiningResult.Fail(DiningError.NotFound, "ไม่พบโต๊ะ"));
                var r = ts.OpenOrResumeSession(tableId, "staff", ClientIp());
                var d = DiningStaffView.Result(r);
                if (r.Ok && r.Session != null)
                {
                    d["message"] = r.Resumed ? "โต๊ะนี้เปิดอยู่แล้ว — ต่อ session เดิม" : "เปิดโต๊ะ " + t.DisplayName + " แล้ว";
                    d["sessionId"] = r.Session.Id;
                    d["resumed"] = r.Resumed;
                    d["orderUrl"] = DiningQr.BuildOrderUrl(t.QrToken);
                    d["statusUrl"] = DiningQr.BuildStatusUrl(r.SessionToken);
                }
                return d;
            }
            catch (Exception ex) { return Err(ex); }
        }
    }
}
