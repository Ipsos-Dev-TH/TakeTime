using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>ตัวกรองรายการออเดอร์ฝั่งพนักงาน</summary>
    public sealed class DiningOrderFilter
    {
        public int? ZoneId;
        public int? TableId;
        public long? SessionId;
        /// <summary>NEW|ACCEPTED|PREPARING|SERVED|CANCELLED — null = ทุกสถานะ; "ACTIVE" = ยังไม่เสิร์ฟ/ไม่ยกเลิก</summary>
        public string Status;
        public bool UnpaidOnly;
        public bool PendingSlipOnly;
        /// <summary>เฉพาะออเดอร์ที่สร้างหลังเวลานี้</summary>
        public DateTime? Since;
        /// <summary>ย้อนหลังกี่วัน (เมื่อไม่ระบุ Since) — default 2</summary>
        public int Days = 2;
        public int Max = 200;
    }

    /// <summary>ผล poll ของกระดาน (ออเดอร์ใหม่ + ตัวนับ)</summary>
    public sealed class DiningBoardPoll
    {
        public List<DiningOrder> NewOrders = new List<DiningOrder>();
        /// <summary>ticks ล่าสุดที่ควรส่งกลับมาในรอบถัดไป</summary>
        public long LatestTicks;
        public int NewCount;
        public int PendingSlipCount;
        public int CallStaffCount;
        public int BillRequestedCount;
    }

    /// <summary>
    /// สั่ง / ดูสถานะ / เปลี่ยนสถานะออเดอร์ QR สั่งอาหาร
    ///   • ราคา: server คิดจากเมนูเสมอ (DiningPricing.ComputeQuote) — client ส่งแค่ ProductId/Qty/OptionIds/Note
    ///   • Order_No DN{yyMMdd}-#### ลำดับรายวัน ภายใต้ sp_getapplock · Round_No = รอบที่ n ของ session · Idempotency_Key กันกดซ้ำ
    ///   • ทุกรอบ = Dining_Order ใหม่ Payment_Status UNPAID — ครัวเริ่มทำได้ทันที (เจ้าของเลือกเก็บเงินที่โต๊ะ/ตอนเช็คบิล)
    ///   • แจ้งเตือนทุกเหตุการณ์ผ่าน DiningNotifier (Telegram + ในระบบ + กล่องแชทรวม)
    /// </summary>
    public class DiningOrderService
    {
        private readonly string _conn;
        private readonly DiningTableService _tables;
        private readonly DiningMenuService _menu;

        public DiningOrderService() : this(DiningDb.ConnStr) { }
        public DiningOrderService(string connectionString)
        {
            _conn = connectionString;
            _tables = new DiningTableService(connectionString);
            _menu = new DiningMenuService(connectionString);
        }

        private const string OrderSelect = @"
            SELECT o.*, t.Code AS T_Code, t.Name_TH AS T_Name, z.Name_TH AS Z_Name
              FROM dbo.Dining_Order o
              JOIN dbo.Dining_Table t ON t.ID = o.Table_ID
              JOIN dbo.Dining_Zone z ON z.ID = o.Zone_ID ";

        // ═════════════════════════════════════════════════════════════════════
        // ลูกค้า: quote / สั่ง / สรุป / เรียกพนักงาน
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>คิดราคาตะกร้า (ตรวจ session + โซนเปิด + เมนู/สต๊อก/ตัวเลือก/ขั้นต่ำ) — Errors ว่าง = สั่งได้</summary>
        public DiningQuote Quote(string sessionToken, IList<DiningCartLine> cart)
        {
            DiningTableSession s;
            return QuoteCore(sessionToken, cart, out s);
        }

        private DiningQuote QuoteCore(string sessionToken, IList<DiningCartLine> cart, out DiningTableSession session)
        {
            session = null;
            var q = new DiningQuote();
            if (!DiningConfig.Enabled) { q.Errors.Add("ระบบสั่งอาหารผ่าน QR ปิดใช้งานอยู่"); q.ErrorCode = DiningError.FeatureOff; return q; }
            if (!DiningDb.Ready(_conn)) { q.Errors.Add(DiningDb.NotReady().Message); q.ErrorCode = DiningError.NotReady; return q; }
            session = _tables.GetSession(sessionToken);
            if (session == null) { q.Errors.Add("โต๊ะนี้ปิดแล้ว กรุณาสแกน QR ที่โต๊ะใหม่"); q.ErrorCode = DiningError.SessionClosed; return q; }
            string closed;
            if (!DiningTableService.IsZoneOpen(_tables.GetZone(session.ZoneId), out closed)) { q.Errors.Add(closed); q.ErrorCode = DiningError.ZoneClosed; return q; }
            return DiningPricing.ComputeQuote(cart, _menu.Lookup(), session.ZoneCode, DiningConfig.MinOrder, UsesVat(), DateTime.Now, session.KitchenStation);
        }

        /// <summary>
        /// สั่ง 1 รอบ — สร้าง Dining_Order + รายการ (snapshot) ภายใต้ transaction; idempotent ด้วย idempotencyKey (8-64 ตัว) → คืนออเดอร์เดิม (Duplicate=true)
        /// </summary>
        /// <param name="intendedMethod">วิธีที่ลูกค้าเลือกบนหน้า (CASH/TRANSFER/ROOM_CHARGE/null) — ใช้แค่ในข้อความแจ้งเตือน; การจ่ายจริงบันทึกผ่าน DiningPaymentService</param>
        public PlaceOrderResult PlaceOrder(string sessionToken, IList<DiningCartLine> cart, string guestNote, string guestName, string guestPhone,
            string idempotencyKey, string clientIp, string intendedMethod = null, string sourceChannel = null)
        {
            var r = new PlaceOrderResult();
            try
            {
                DiningTableSession s;
                var q = QuoteCore(sessionToken, cart, out s);
                r.Quote = q;
                if (!q.Ok) { r.ErrorCode = q.ErrorCode ?? DiningError.Validation; r.Message = string.Join("\n", q.Errors.ToArray()); return r; }

                string idem = DiningTokens.SanitizeIdempotencyKey(idempotencyKey);
                if (idem != null)
                {
                    var exist = FindByIdempotency(idem, s.Id);
                    if (exist != null) { r.Ok = true; r.Duplicate = true; r.Order = exist; r.Message = "รับออเดอร์นี้ไว้แล้ว"; return r; }
                }

                // สต๊อกสด (รายการที่ตัดสต๊อก) — cache เมนู 60 วิ อาจล้าสมัย
                foreach (var l in q.Lines)
                {
                    if (!l.TrackStock) continue;
                    decimal live = _menu.LiveStock(l.ProductId);
                    if (live < l.Qty)
                    {
                        r.ErrorCode = DiningError.OutOfStock;
                        r.Message = "\"" + l.Name + "\" เหลือ " + live.ToString("0.##", CultureInfo.InvariantCulture) + " — สั่งได้ไม่เกินนั้น";
                        return r;
                    }
                }

                guestNote = Trunc(guestNote, 500); guestName = Trunc(guestName, 100); guestPhone = Trunc(guestPhone, 30);
                string src = string.Equals(sourceChannel, DiningStatus.Source.Staff, StringComparison.OrdinalIgnoreCase) ? DiningStatus.Source.Staff : DiningStatus.Source.Qr;
                long orderId; string orderNo; int round;
                try
                {
                    InsertOrder(s, q, guestNote, guestName, guestPhone, idem, clientIp, src, out orderId, out orderNo, out round);
                }
                catch (SqlException ex) when (idem != null && (ex.Number == 2601 || ex.Number == 2627))
                {
                    var exist = FindByIdempotency(idem, s.Id);
                    if (exist != null) { r.Ok = true; r.Duplicate = true; r.Order = exist; r.Message = "รับออเดอร์นี้ไว้แล้ว"; return r; }
                    throw;
                }

                // session/โต๊ะ
                var up = new Dictionary<string, object> { { "@id", s.Id }, { "@n", DiningDb.DbStr(guestName, 100) }, { "@p", DiningDb.DbStr(guestPhone, 30) } };
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table_Session SET Last_Seen_At=GETDATE(), Guest_Name=COALESCE(@n, Guest_Name), Guest_Phone=COALESCE(@p, Guest_Phone) WHERE ID=@id", up);
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table SET Status='OCCUPIED', Current_Session_ID=@s, Updated_At=GETDATE() WHERE ID=@t AND Status IN ('FREE','OCCUPIED','NEEDS_STAFF')",
                    new Dictionary<string, object> { { "@t", s.TableId }, { "@s", s.Id } });

                var order = GetOrder(orderId);
                r.Ok = true; r.Order = order; r.Message = "รับออเดอร์ " + orderNo + " แล้ว";
                DiningDb.Event(_conn, orderId, s.Id, s.TableId, DiningStatus.Event.NewOrder, src == DiningStatus.Source.Staff ? DiningDb.StaffName() : "GUEST",
                    orderNo + " รอบ " + round + " ยอด " + q.Total.ToString("N2", CultureInfo.InvariantCulture) + " (" + q.Lines.Count + " รายการ)");
                DiningDb.Log(_conn, "ออเดอร์ใหม่ " + orderNo + " " + s.Label + " ยอด " + q.Total.ToString("N2", CultureInfo.InvariantCulture), src == DiningStatus.Source.Staff ? null : "GUEST");
                try { new DiningNotifier(_conn).NewOrder(order, s, DiningNotifyText.PaymentStateText(intendedMethod, false, false)); } catch { }
                return r;
            }
            catch (Exception ex)
            {
                DiningDb.Log(_conn, "PlaceOrder ล้มเหลว: " + ex.Message, "SYSTEM");
                r.ErrorCode = DiningError.Internal; r.Message = "บันทึกออเดอร์ไม่สำเร็จ กรุณาลองใหม่หรือเรียกพนักงาน";
                return r;
            }
        }

        private void InsertOrder(DiningTableSession s, DiningQuote q, string guestNote, string guestName, string guestPhone, string idem, string clientIp, string src,
            out long orderId, out string orderNo, out int round)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        using (var lk = new SqlCommand("sp_getapplock", con, tx))
                        {
                            lk.CommandType = CommandType.StoredProcedure;
                            lk.Parameters.AddWithValue("@Resource", "Dining_OrderNo");
                            lk.Parameters.AddWithValue("@LockMode", "Exclusive");
                            lk.Parameters.AddWithValue("@LockOwner", "Transaction");
                            lk.Parameters.AddWithValue("@LockTimeout", 10000);
                            var ret = lk.Parameters.Add("@r", SqlDbType.Int); ret.Direction = ParameterDirection.ReturnValue;
                            lk.ExecuteNonQuery();
                            if (Convert.ToInt32(ret.Value) < 0) throw new Exception("ไม่สามารถจองเลขออเดอร์ได้ (lock)");
                        }

                        string prefix = "DN" + DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture) + "-";
                        int next = 1;
                        using (var cmd = new SqlCommand("SELECT MAX(Order_No) FROM dbo.Dining_Order WHERE Order_No LIKE @p + '%'", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@p", prefix);
                            object o = cmd.ExecuteScalar();
                            string last = o == null || o == DBNull.Value ? null : Convert.ToString(o);
                            int n;
                            if (!string.IsNullOrEmpty(last) && last.Length > prefix.Length && int.TryParse(last.Substring(prefix.Length), out n)) next = n + 1;
                        }
                        orderNo = prefix + next.ToString("0000", CultureInfo.InvariantCulture);

                        using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Dining_Order WHERE Session_ID = @s", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@s", s.Id);
                            round = Convert.ToInt32(cmd.ExecuteScalar()) + 1;
                        }

                        using (var cmd = new SqlCommand(@"
                            INSERT INTO dbo.Dining_Order (Order_No, Session_ID, Table_ID, Zone_ID, Round_No, Order_Status, Subtotal, Discount, Total_Amount, Vat_Amount,
                                Payment_Status, Guest_Note, Guest_Name, Guest_Phone, Source_Channel, Client_Ip, Idempotency_Key, Created_At)
                            VALUES (@no, @s, @t, @z, @r, 'NEW', @sub, @disc, @tot, @vat, 'UNPAID', @note, @gn, @gp, @src, @ip, @idem, GETDATE());
                            SELECT CAST(SCOPE_IDENTITY() AS BIGINT)", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@no", orderNo);
                            cmd.Parameters.AddWithValue("@s", s.Id);
                            cmd.Parameters.AddWithValue("@t", s.TableId);
                            cmd.Parameters.AddWithValue("@z", s.ZoneId);
                            cmd.Parameters.AddWithValue("@r", round);
                            cmd.Parameters.AddWithValue("@sub", q.Subtotal);
                            cmd.Parameters.AddWithValue("@disc", q.Discount);
                            cmd.Parameters.AddWithValue("@tot", q.Total);
                            cmd.Parameters.AddWithValue("@vat", q.VatAmount);
                            cmd.Parameters.AddWithValue("@note", DiningDb.DbStr(guestNote, 500));
                            cmd.Parameters.AddWithValue("@gn", DiningDb.DbStr(guestName, 100));
                            cmd.Parameters.AddWithValue("@gp", DiningDb.DbStr(guestPhone, 30));
                            cmd.Parameters.AddWithValue("@src", src);
                            cmd.Parameters.AddWithValue("@ip", DiningDb.DbStr(clientIp, 45));
                            cmd.Parameters.AddWithValue("@idem", DiningDb.Db(idem));
                            orderId = Convert.ToInt64(cmd.ExecuteScalar());
                        }

                        foreach (var l in q.Lines)
                        {
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO dbo.Dining_Order_Item (Order_ID, Product_ID, Product_Name, Qty, Unit_Price, Options_Json, Options_Price, Line_Total, Item_Note, Track_Stock, Kitchen_Status, Station)
                                VALUES (@o, @pid, @name, @qty, @unit, @oj, @op, @lt, @note, @ts, 'QUEUED', @st)", con, tx))
                            {
                                cmd.Parameters.AddWithValue("@o", orderId);
                                cmd.Parameters.AddWithValue("@pid", l.ProductId);
                                cmd.Parameters.AddWithValue("@name", Trunc(l.Name, 200));
                                cmd.Parameters.AddWithValue("@qty", l.Qty);
                                cmd.Parameters.AddWithValue("@unit", l.UnitPrice);
                                cmd.Parameters.AddWithValue("@oj", DiningPricing.OptionsToJson(l.Options));
                                cmd.Parameters.AddWithValue("@op", l.OptionsPrice);
                                cmd.Parameters.AddWithValue("@lt", l.LineTotal);
                                cmd.Parameters.AddWithValue("@note", DiningDb.DbStr(l.Note, 300));
                                cmd.Parameters.AddWithValue("@ts", l.TrackStock);
                                cmd.Parameters.AddWithValue("@st", DiningDb.DbStr(l.Station, 50));
                                cmd.ExecuteNonQuery();
                            }
                        }
                        tx.Commit();
                    }
                    catch { try { tx.Rollback(); } catch { } throw; }
                }
            }
        }

        private DiningOrder FindByIdempotency(string key, long sessionId)
        {
            var dt = DiningDb.Query(_conn, OrderSelect + "WHERE o.Idempotency_Key = @k AND o.Session_ID = @s", new Dictionary<string, object> { { "@k", key }, { "@s", sessionId } });
            if (dt.Rows.Count == 0) return null;
            var o = ReadOrder(dt.Rows[0]);
            LoadItems(new List<DiningOrder> { o });
            return o;
        }

        /// <summary>สรุปทั้ง session สำหรับหน้าสถานะลูกค้า (ออเดอร์ทุกรอบ + รายการ + การชำระ + ยอดค้าง + โซนเปิดไหม)</summary>
        public SessionSummary GetSessionSummary(string sessionToken)
        {
            var sum = new SessionSummary();
            if (!DiningDb.Ready(_conn)) { sum.ErrorCode = DiningError.NotReady; sum.Message = DiningDb.NotReady().Message; return sum; }
            var s = _tables.GetSession(sessionToken);
            if (s == null) { sum.ErrorCode = DiningError.SessionClosed; sum.Message = "โต๊ะนี้ปิดแล้ว กรุณาสแกน QR ที่โต๊ะใหม่"; return sum; }
            sum.Session = s;
            sum.Orders = ListOrders(new DiningOrderFilter { SessionId = s.Id, Max = 500, Days = 30 });
            sum.Orders.Sort((a, b) => a.RoundNo != b.RoundNo ? a.RoundNo.CompareTo(b.RoundNo) : a.Id.CompareTo(b.Id));
            sum.Payments = DiningPaymentService.LoadPayments(_conn, s.Id);
            sum.Outstanding = DiningPaymentService.LoadOutstanding(_conn, s.Id);
            long ticks = Math.Max(s.OpenedAt.Ticks, s.LastSeenAt.Ticks);
            foreach (var o in sum.Orders)
            {
                if (!o.IsCancelled) sum.TotalOrdered += o.TotalAmount;
                if (o.IsSettled) sum.TotalPaid += o.TotalAmount;
                ticks = Math.Max(ticks, (o.UpdatedAt ?? o.CreatedAt).Ticks);
            }
            foreach (var p in sum.Payments) ticks = Math.Max(ticks, (p.UpdatedAt ?? p.CreatedAt).Ticks);
            if (s.BillRequestedAt.HasValue) ticks = Math.Max(ticks, s.BillRequestedAt.Value.Ticks);
            sum.VersionTicks = ticks;
            sum.BillRequested = s.Status == DiningStatus.Session.BillRequested;
            string msg;
            sum.ZoneOpen = DiningTableService.IsZoneOpen(_tables.GetZone(s.ZoneId), out msg);
            sum.ZoneClosedMessage = msg;
            sum.Ok = true;
            return sum;
        }

        /// <summary>ลูกค้าเรียกพนักงาน (ข้อความไม่บังคับ ≤ 200 ตัว) — 1 ครั้ง/นาที ต่อโต๊ะ; โต๊ะ → NEEDS_STAFF; แจ้งเตือน</summary>
        public DiningResult CallStaff(string sessionToken, string message)
        {
            if (!DiningDb.Ready(_conn)) return DiningDb.NotReady();
            var s = _tables.GetSession(sessionToken);
            if (s == null) return DiningResult.Fail(DiningError.SessionClosed, "โต๊ะนี้ปิดแล้ว กรุณาสแกน QR ที่โต๊ะใหม่");
            if (s.LastCallAt.HasValue && (DateTime.Now - s.LastCallAt.Value).TotalSeconds < DiningConfig.CallStaffCooldownSec)
                return DiningResult.Fail(DiningError.RateLimited, "เรียกพนักงานไปแล้วเมื่อสักครู่ — รอสักครู่นะคะ พนักงานกำลังมา");
            int n = DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table_Session SET Last_Call_At=GETDATE(), Last_Seen_At=GETDATE() WHERE ID=@id AND (Last_Call_At IS NULL OR Last_Call_At < DATEADD(SECOND, -@c, GETDATE()))",
                new Dictionary<string, object> { { "@id", s.Id }, { "@c", DiningConfig.CallStaffCooldownSec } });
            if (n <= 0) return DiningResult.Fail(DiningError.RateLimited, "เรียกพนักงานไปแล้วเมื่อสักครู่ — รอสักครู่นะคะ");
            DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table SET Status='NEEDS_STAFF', Updated_At=GETDATE() WHERE ID=@t AND Status IN ('FREE','OCCUPIED')", new Dictionary<string, object> { { "@t", s.TableId } });
            message = Trunc(message, 200);
            DiningDb.Event(_conn, null, s.Id, s.TableId, DiningStatus.Event.CallStaff, "GUEST", message);
            try { new DiningNotifier(_conn).CallStaff(s, message); } catch { }
            return DiningResult.Success("แจ้งพนักงานแล้ว กรุณารอสักครู่");
        }

        /// <summary>พนักงานรับทราบการเรียก — โต๊ะ NEEDS_STAFF กลับเป็น OCCUPIED</summary>
        public void AcknowledgeStaffCall(int tableId)
        {
            DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table SET Status='OCCUPIED', Updated_At=GETDATE() WHERE ID=@t AND Status='NEEDS_STAFF'", new Dictionary<string, object> { { "@t", tableId } });
        }

        // ═════════════════════════════════════════════════════════════════════
        // พนักงาน: รายการ / สถานะ / poll
        // ═════════════════════════════════════════════════════════════════════

        public DiningOrder GetOrder(long orderId)
        {
            if (!DiningDb.Ready(_conn)) return null;
            var dt = DiningDb.Query(_conn, OrderSelect + "WHERE o.ID = @id", new Dictionary<string, object> { { "@id", orderId } });
            if (dt.Rows.Count == 0) return null;
            var o = ReadOrder(dt.Rows[0]);
            LoadItems(new List<DiningOrder> { o });
            return o;
        }

        public DiningOrder GetOrderByNo(string orderNo)
        {
            if (!DiningDb.Ready(_conn) || string.IsNullOrWhiteSpace(orderNo)) return null;
            var dt = DiningDb.Query(_conn, OrderSelect + "WHERE o.Order_No = @n", new Dictionary<string, object> { { "@n", orderNo.Trim() } });
            if (dt.Rows.Count == 0) return null;
            var o = ReadOrder(dt.Rows[0]);
            LoadItems(new List<DiningOrder> { o });
            return o;
        }

        /// <summary>รายการออเดอร์ (พร้อมรายการสินค้า) ตามตัวกรอง — ใหม่สุดก่อน</summary>
        public List<DiningOrder> ListOrders(DiningOrderFilter f)
        {
            var list = new List<DiningOrder>();
            if (!DiningDb.Ready(_conn)) return list;
            f = f ?? new DiningOrderFilter();
            var p = new Dictionary<string, object>();
            var where = new List<string>();
            if (f.SessionId.HasValue) { where.Add("o.Session_ID = @s"); p["@s"] = f.SessionId.Value; }
            if (f.ZoneId.HasValue && f.ZoneId.Value > 0) { where.Add("o.Zone_ID = @z"); p["@z"] = f.ZoneId.Value; }
            if (f.TableId.HasValue && f.TableId.Value > 0) { where.Add("o.Table_ID = @t"); p["@t"] = f.TableId.Value; }
            if (!string.IsNullOrWhiteSpace(f.Status))
            {
                string st = f.Status.Trim().ToUpperInvariant();
                if (st == "ACTIVE") where.Add("o.Order_Status IN ('NEW','ACCEPTED','PREPARING')");
                else { where.Add("o.Order_Status = @st"); p["@st"] = st; }
            }
            if (f.UnpaidOnly) where.Add("o.Payment_Status = 'UNPAID' AND o.Order_Status <> 'CANCELLED'");
            if (f.PendingSlipOnly) where.Add("o.Pending_Payment_ID IS NOT NULL");
            if (f.Since.HasValue) { where.Add("o.Created_At > @since"); p["@since"] = f.Since.Value; }
            else if (!f.SessionId.HasValue) { where.Add("o.Created_At >= DATEADD(DAY, -@d, CAST(GETDATE() AS DATE))"); p["@d"] = Math.Max(0, f.Days); }
            p["@max"] = Math.Max(1, Math.Min(2000, f.Max));
            string sql = OrderSelect.Replace("SELECT o.*", "SELECT TOP (@max) o.*") + (where.Count > 0 ? "WHERE " + string.Join(" AND ", where.ToArray()) + " " : "") + "ORDER BY o.ID DESC";
            var dt = DiningDb.Query(_conn, sql, p);
            foreach (DataRow r in dt.Rows) list.Add(ReadOrder(r));
            LoadItems(list);
            return list;
        }

        /// <summary>poll ของกระดาน: ออเดอร์ NEW ที่สร้างหลัง sinceTicks (0 = ทั้งหมดที่ยัง NEW ใน 2 วัน) + ตัวนับ สลิปรอตรวจ/เรียกพนักงาน/เช็คบิล</summary>
        public DiningBoardPoll GetNewOrdersSince(long sinceTicks)
        {
            var poll = new DiningBoardPoll { LatestTicks = sinceTicks };
            if (!DiningDb.Ready(_conn)) return poll;
            try
            {
                var f = new DiningOrderFilter { Status = DiningStatus.Order.New, Max = 100 };
                if (sinceTicks > 0) f.Since = new DateTime(sinceTicks);
                poll.NewOrders = ListOrders(f);
                foreach (var o in poll.NewOrders) poll.LatestTicks = Math.Max(poll.LatestTicks, o.CreatedAt.Ticks);
                if (poll.LatestTicks == 0) poll.LatestTicks = DateTime.Now.Ticks;
                var dt = DiningDb.Query(_conn, @"
                    SELECT (SELECT COUNT(*) FROM dbo.Dining_Order WHERE Order_Status = 'NEW') AS N,
                           (SELECT COUNT(*) FROM dbo.Dining_Payment WHERE Status = 'PENDING_SLIP') AS P,
                           (SELECT COUNT(*) FROM dbo.Dining_Table WHERE Status = 'NEEDS_STAFF') AS C,
                           (SELECT COUNT(*) FROM dbo.Dining_Table WHERE Status = 'BILL_REQUESTED') AS B");
                if (dt.Rows.Count > 0)
                {
                    poll.NewCount = DiningDb.Int(dt.Rows[0]["N"]);
                    poll.PendingSlipCount = DiningDb.Int(dt.Rows[0]["P"]);
                    poll.CallStaffCount = DiningDb.Int(dt.Rows[0]["C"]);
                    poll.BillRequestedCount = DiningDb.Int(dt.Rows[0]["B"]);
                }
            }
            catch (Exception ex) { DiningDb.Log(_conn, "GetNewOrdersSince ล้มเหลว: " + ex.Message, "SYSTEM"); }
            return poll;
        }

        /// <summary>
        /// เปลี่ยนสถานะออเดอร์ (พนักงาน) — NEW→ACCEPTED→PREPARING→SERVED (ข้ามขั้นได้), ยกเลิกได้เมื่อยังไม่เสิร์ฟ
        ///   • ยกเลิกออเดอร์ที่ชำระแล้ว: ต้อง Owner/Admin + เหตุผล; ลงบัญชีแล้ว (Dining_Payment.Acct_Post_Ref) = บล็อก → ไปหน้าใบลดหนี้;
        ///     ยังไม่ลงบัญชี = ออเดอร์ REFUNDED และลดยอด/ตัดออกจาก Dining_Payment ที่ครอบ (เงินคืนลูกค้าทำนอกระบบ)
        ///   • ลงบิลห้องแล้ว (CHARGED) = บล็อก (ยกเลิกรายการที่หน้าการจอง)
        /// </summary>
        public DiningResult UpdateStatus(long orderId, string newStatus, int staffId, string reason)
        {
            if (!DiningDb.Ready(_conn)) return DiningDb.NotReady();
            var o = GetOrder(orderId);
            if (o == null) return DiningResult.Fail(DiningError.NotFound, "ไม่พบออเดอร์");
            newStatus = (newStatus ?? "").Trim().ToUpperInvariant();
            if (Array.IndexOf(DiningStatus.Order.All, newStatus) < 0) return DiningResult.Fail(DiningError.Validation, "สถานะไม่ถูกต้อง");
            if (!DiningStatus.Order.CanTransition(o.OrderStatus, newStatus))
                return DiningResult.Fail(DiningError.Transition, "เปลี่ยนจาก \"" + DiningStatus.Order.Thai(o.OrderStatus) + "\" เป็น \"" + DiningStatus.Order.Thai(newStatus) + "\" ไม่ได้");

            string staffName = DiningDb.StaffName();
            reason = Trunc(reason, 300);
            var p = new Dictionary<string, object> { { "@id", orderId }, { "@by", staffId > 0 ? (object)staffId : DBNull.Value }, { "@r", DiningDb.DbStr(reason, 300) }, { "@from", o.OrderStatus } };
            string sql; string kitchen;
            if (newStatus == DiningStatus.Order.Cancelled)
            {
                if (o.PaymentStatus == DiningStatus.Pay.Charged)
                    return DiningResult.Fail(DiningError.AccountingPosted, "ออเดอร์นี้ลงบิลห้องพักแล้ว — ยกเลิกรายการที่หน้าการจอง (ค่าใช้จ่ายในห้อง) แทน");
                if (o.PaymentStatus == DiningStatus.Pay.Paid)
                {
                    if (!Take_Time_BangPhra.Payments.SlipCheck.SlipVerificationService.CanOverrideCurrentUser())
                        return DiningResult.Fail(DiningError.Forbidden, "ยกเลิกออเดอร์ที่ชำระแล้วได้เฉพาะ Owner/Admin");
                    if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
                        return DiningResult.Fail(DiningError.Validation, "กรุณาระบุเหตุผลการยกเลิก (อย่างน้อย 5 ตัวอักษร)");
                    var pay = o.PaymentId.HasValue ? DiningPaymentService.LoadPayment(_conn, o.PaymentId.Value) : null;
                    if (pay != null && !string.IsNullOrEmpty(pay.AcctPostRef))
                        return DiningResult.Fail(DiningError.AccountingPosted, "ออเดอร์นี้ลงบัญชีแล้ว (" + pay.AcctPostRef.TrimStart('~') + ") — ออกใบลดหนี้/คืนเงินที่ Account/CreditNotes แทนการยกเลิก");
                    if (pay != null) DiningPaymentService.DetachOrderFromPayment(_conn, pay, o, "ยกเลิกออเดอร์ " + o.OrderNo + ": " + reason);
                    p["@pay"] = DiningStatus.Pay.Refunded;
                }
                else p["@pay"] = o.PaymentStatus;
                sql = @"UPDATE dbo.Dining_Order SET Order_Status='CANCELLED', Cancel_Reason=@r, Cancelled_By=@by, Cancelled_At=GETDATE(), Payment_Status=@pay, Updated_At=GETDATE()
                        WHERE ID=@id AND Order_Status=@from";
                kitchen = DiningStatus.Kitchen.Void;
            }
            else if (newStatus == DiningStatus.Order.Served)
            {
                sql = @"UPDATE dbo.Dining_Order SET Order_Status='SERVED', Served_By=@by, Served_At=GETDATE(), Accepted_By=ISNULL(Accepted_By,@by), Accepted_At=ISNULL(Accepted_At,GETDATE()), Updated_At=GETDATE()
                        WHERE ID=@id AND Order_Status=@from";
                kitchen = DiningStatus.Kitchen.Served;
            }
            else if (newStatus == DiningStatus.Order.Preparing)
            {
                sql = @"UPDATE dbo.Dining_Order SET Order_Status='PREPARING', Accepted_By=ISNULL(Accepted_By,@by), Accepted_At=ISNULL(Accepted_At,GETDATE()), Updated_At=GETDATE()
                        WHERE ID=@id AND Order_Status=@from";
                kitchen = DiningStatus.Kitchen.Cooking;
            }
            else
            {
                sql = @"UPDATE dbo.Dining_Order SET Order_Status='ACCEPTED', Accepted_By=@by, Accepted_At=GETDATE(), Updated_At=GETDATE() WHERE ID=@id AND Order_Status=@from";
                kitchen = DiningStatus.Kitchen.Queued;
            }
            int n = DiningDb.Exec(_conn, sql, p);
            if (n <= 0) return DiningResult.Fail(DiningError.Transition, "สถานะออเดอร์เปลี่ยนไปแล้ว กรุณารีเฟรช");
            DiningDb.Exec(_conn, "UPDATE dbo.Dining_Order_Item SET Kitchen_Status=@k WHERE Order_ID=@id AND Kitchen_Status <> 'VOID'", new Dictionary<string, object> { { "@id", orderId }, { "@k", kitchen } });

            DiningDb.Event(_conn, orderId, o.SessionId, o.TableId, DiningStatus.Event.Status, staffName, o.OrderStatus + "→" + newStatus + (string.IsNullOrEmpty(reason) ? "" : ": " + reason));
            DiningDb.Log(_conn, "ออเดอร์ " + o.OrderNo + " " + DiningStatus.Order.Thai(o.OrderStatus) + " → " + DiningStatus.Order.Thai(newStatus) + (string.IsNullOrEmpty(reason) ? "" : " (" + reason + ")"));
            string old = o.OrderStatus;
            o.OrderStatus = newStatus;
            if (p.ContainsKey("@pay")) o.PaymentStatus = Convert.ToString(p["@pay"]);
            var s = _tables.GetSessionById(o.SessionId);
            try { new DiningNotifier(_conn).StatusChanged(o, s, old, newStatus, staffName, reason); } catch { }
            return DiningResult.Success("อัปเดตเป็น \"" + DiningStatus.Order.Thai(newStatus) + "\" แล้ว");
        }

        // ═════════════════════════════════════════════════════════════════════
        // readers / helpers
        // ═════════════════════════════════════════════════════════════════════

        private static readonly object _vatLock = new object();
        private static bool? _vat;
        private static DateTime _vatAt = DateTime.MinValue;

        /// <summary>Business_Info.Use_Vat (cache 5 นาที)</summary>
        public bool UsesVat()
        {
            lock (_vatLock)
            {
                if (_vat.HasValue && (DateTime.UtcNow - _vatAt).TotalMinutes < 5) return _vat.Value;
            }
            bool v = false;
            try
            {
                var dt = DiningDb.Query(_conn, "SELECT TOP 1 Use_Vat FROM Business_Info");
                if (dt.Rows.Count > 0) v = DiningDb.Bool(dt.Rows[0]["Use_Vat"]);
            }
            catch { }
            lock (_vatLock) { _vat = v; _vatAt = DateTime.UtcNow; }
            return v;
        }

        internal static DiningOrder ReadOrder(DataRow r)
        {
            return new DiningOrder
            {
                Id = DiningDb.Long(r["ID"]), OrderNo = DiningDb.Str(r["Order_No"]), SessionId = DiningDb.Long(r["Session_ID"]), TableId = DiningDb.Int(r["Table_ID"]),
                ZoneId = DiningDb.Int(r["Zone_ID"]), TableCode = DiningDb.Str(r["T_Code"]), TableName = DiningDb.Str(r["T_Name"]), ZoneName = DiningDb.Str(r["Z_Name"]),
                RoundNo = DiningDb.Int(r["Round_No"]), OrderStatus = DiningDb.Str(r["Order_Status"]), Subtotal = DiningDb.Dec(r["Subtotal"]), Discount = DiningDb.Dec(r["Discount"]),
                TotalAmount = DiningDb.Dec(r["Total_Amount"]), VatAmount = DiningDb.Dec(r["Vat_Amount"]), PaymentStatus = DiningDb.Str(r["Payment_Status"]),
                PaymentId = DiningDb.NLong(r["Payment_ID"]), PendingPaymentId = DiningDb.NLong(r["Pending_Payment_ID"]), GuestNote = DiningDb.Str(r["Guest_Note"]),
                GuestName = DiningDb.Str(r["Guest_Name"]), GuestPhone = DiningDb.Str(r["Guest_Phone"]), CancelReason = DiningDb.Str(r["Cancel_Reason"]),
                AcceptedBy = DiningDb.NInt(r["Accepted_By"]), AcceptedAt = DiningDb.NDate(r["Accepted_At"]), ServedBy = DiningDb.NInt(r["Served_By"]), ServedAt = DiningDb.NDate(r["Served_At"]),
                CancelledBy = DiningDb.NInt(r["Cancelled_By"]), CancelledAt = DiningDb.NDate(r["Cancelled_At"]), SourceChannel = DiningDb.Str(r["Source_Channel"]),
                ClientIp = DiningDb.Str(r["Client_Ip"]), IdempotencyKey = DiningDb.Str(r["Idempotency_Key"]), StockPosted = DiningDb.Bool(r["Stock_Posted"]),
                CreatedAt = DiningDb.Date(r["Created_At"]), UpdatedAt = DiningDb.NDate(r["Updated_At"])
            };
        }

        internal void LoadItems(List<DiningOrder> orders)
        {
            if (orders == null || orders.Count == 0) return;
            var map = new Dictionary<long, DiningOrder>();
            var ids = new List<long>();
            foreach (var o in orders) { map[o.Id] = o; ids.Add(o.Id); o.Items.Clear(); }
            for (int i = 0; i < ids.Count; i += 200)
            {
                var chunk = ids.GetRange(i, Math.Min(200, ids.Count - i));
                var p = new Dictionary<string, object>();
                var dt = DiningDb.Query(_conn, "SELECT * FROM dbo.Dining_Order_Item WHERE Order_ID IN (" + DiningDb.InList(chunk, p) + ") ORDER BY Order_ID, ID", p);
                foreach (DataRow r in dt.Rows)
                {
                    DiningOrder o;
                    if (!map.TryGetValue(DiningDb.Long(r["Order_ID"]), out o)) continue;
                    o.Items.Add(ReadItem(r));
                }
            }
        }

        internal static DiningOrderItem ReadItem(DataRow r)
        {
            var it = new DiningOrderItem
            {
                Id = DiningDb.Long(r["ID"]), OrderId = DiningDb.Long(r["Order_ID"]), ProductId = DiningDb.Int(r["Product_ID"]), ProductName = DiningDb.Str(r["Product_Name"]),
                Qty = DiningDb.Dec(r["Qty"]), UnitPrice = DiningDb.Dec(r["Unit_Price"]), OptionsJson = DiningDb.Str(r["Options_Json"]), OptionsPrice = DiningDb.Dec(r["Options_Price"]),
                LineTotal = DiningDb.Dec(r["Line_Total"]), ItemNote = DiningDb.Str(r["Item_Note"]), TrackStock = DiningDb.Bool(r["Track_Stock"]),
                KitchenStatus = DiningDb.Str(r["Kitchen_Status"]), Station = DiningDb.Str(r["Station"])
            };
            it.Options = DiningPricing.OptionsFromJson(it.OptionsJson);
            return it;
        }

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length > max ? s.Substring(0, max) : s;
        }
    }
}
