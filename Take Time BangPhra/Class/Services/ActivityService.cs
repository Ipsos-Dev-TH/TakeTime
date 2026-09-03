using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// กิจกรรมในที่พัก: ตั้งค่ากิจกรรม, จองช่วงเวลา (กันชนกัน/เต็มโควตา), คิดราคา,
    /// และชำระเงิน 2 ทาง — ชาร์จเข้าห้อง (ไปรวมจ่ายตอนเช็คเอาท์) หรือ โอนแล้วแนบสลิป (รออนุมัติ).
    /// ใช้ร่วมกันทั้งหน้าเว็บสาธารณะ / Guest Portal / หน้า Admin.
    /// </summary>
    public class ActivityService
    {
        private readonly string _conn;
        private readonly code _code = new code();

        public ActivityService(string connectionString)
        {
            _conn = connectionString;
        }

        // ── ข้อมูลกิจกรรม ─────────────────────────────────────────────────────────

        /// <summary>กิจกรรมทั้งหมด (สำหรับหน้า Admin)</summary>
        public DataTable GetAllActivities(bool activeOnly = false)
        {
            string where = activeOnly ? "WHERE IsActive = 1" : "";
            return _code.DatabaseQuerySafe(_conn,
                $@"SELECT * FROM Property_Activities {where}
                   ORDER BY Category, DisplayOrder, ActivityName", null);
        }

        /// <summary>กิจกรรมที่แสดงต่อผู้ใช้ — channel: WEBSITE (หน้าแรก) หรือ PORTAL (ผู้เข้าพัก)</summary>
        public DataTable GetVisibleActivities(string channel, string category = null)
        {
            string visibleCol = channel == "WEBSITE" ? "ShowOnWebsite" : "ShowInPortal";
            var p = new Dictionary<string, object>();
            string catFilter = "";
            if (!string.IsNullOrEmpty(category))
            {
                catFilter = " AND Category = @cat";
                p["@cat"] = category;
            }
            return _code.DatabaseQuerySafe(_conn,
                $@"SELECT a.*,
                          (SELECT COUNT(*) FROM Property_Activity_Images i WHERE i.Activity_ID = a.ID) AS ImageCount
                     FROM Property_Activities a
                    WHERE a.IsActive = 1 AND a.{visibleCol} = 1 {catFilter}
                    ORDER BY a.Category, a.DisplayOrder, a.ActivityName", p);
        }

        public DataRow GetActivity(int activityId)
        {
            var dt = _code.DatabaseQuerySafe(_conn,
                "SELECT * FROM Property_Activities WHERE ID = @id",
                new Dictionary<string, object> { { "@id", activityId } });
            return dt?.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public DataTable GetActivityImages(int activityId)
        {
            return _code.DatabaseQuerySafe(_conn,
                @"SELECT ID, ImagePath, Caption, DisplayOrder
                    FROM Property_Activity_Images
                   WHERE Activity_ID = @id ORDER BY DisplayOrder, ID",
                new Dictionary<string, object> { { "@id", activityId } });
        }

        public int SaveActivity(Dictionary<string, object> f, int? activityId, short? adminId)
        {
            var p = new Dictionary<string, object>
            {
                { "@name", f.Get("ActivityName") },
                { "@shortDesc", f.Get("ShortDescription") },
                { "@desc", f.Get("Description") },
                { "@category", f.Get("Category", "ON_PROPERTY") },
                { "@image", f.Get("ImagePath") },
                { "@price", f.GetDecimal("Price") },
                { "@pricingMode", f.Get("PricingMode", "FREE") },
                { "@bookable", f.GetBool("IsBookable") ? 1 : 0 },
                { "@capacity", Math.Max(1, f.GetInt("Capacity", 1)) },
                { "@openTime", f.Get("OpenTime") },
                { "@closeTime", f.Get("CloseTime") },
                { "@slotMin", Math.Max(15, f.GetInt("SlotMinutes", 60)) },
                { "@maxSlots", Math.Max(1, f.GetInt("MaxSlotsPerBooking", 4)) },
                { "@advanceDays", Math.Max(0, f.GetInt("AdvanceBookingDays", 14)) },
                { "@maxPart", f.GetInt("MaxParticipants", 0) },
                { "@requireApproval", f.GetBool("RequireApproval") ? 1 : 0 },
                { "@showWeb", f.GetBool("ShowOnWebsite") ? 1 : 0 },
                { "@showPortal", f.GetBool("ShowInPortal") ? 1 : 0 },
                { "@duration", f.Get("Duration") },
                { "@location", f.Get("Location") },
                { "@contact", f.Get("ContactInfo") },
                { "@mapUrl", f.Get("MapUrl") },
                { "@icon", f.Get("IconClass") },
                { "@rules", f.Get("Rules") },
                { "@order", f.GetInt("DisplayOrder", 0) },
                { "@active", f.GetBool("IsActive", true) ? 1 : 0 }
            };

            if (activityId.HasValue && activityId.Value > 0)
            {
                p["@id"] = activityId.Value;
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Property_Activities SET
                        ActivityName=@name, ShortDescription=@shortDesc, Description=@desc, Category=@category,
                        ImagePath=CASE WHEN @image = '' THEN ImagePath ELSE @image END,
                        Price=@price, PricingMode=@pricingMode, IsBookable=@bookable, Capacity=@capacity,
                        OpenTime=NULLIF(@openTime,''), CloseTime=NULLIF(@closeTime,''), SlotMinutes=@slotMin,
                        MaxSlotsPerBooking=@maxSlots, AdvanceBookingDays=@advanceDays,
                        MaxParticipants=NULLIF(@maxPart,0), RequireApproval=@requireApproval,
                        ShowOnWebsite=@showWeb, ShowInPortal=@showPortal, Duration=@duration,
                        Location=@location, ContactInfo=@contact, MapUrl=@mapUrl, IconClass=@icon,
                        Rules=@rules, DisplayOrder=@order, IsActive=@active, LastUpdated=GETDATE()
                      WHERE ID=@id", p);
                return activityId.Value;
            }

            p["@admin"] = adminId.HasValue ? (object)adminId.Value : DBNull.Value;
            var dt = _code.DatabaseQuerySafe(_conn,
                @"INSERT INTO Property_Activities
                    (ActivityName, ShortDescription, Description, Category, ImagePath, Price, PricingMode,
                     IsBookable, Capacity, OpenTime, CloseTime, SlotMinutes, MaxSlotsPerBooking,
                     AdvanceBookingDays, MaxParticipants, RequireApproval, ShowOnWebsite, ShowInPortal,
                     Duration, Location, ContactInfo, MapUrl, IconClass, Rules, DisplayOrder, IsActive,
                     CreatedBy_AdminID, CreatedDate, LastUpdated)
                  VALUES
                    (@name, @shortDesc, @desc, @category, @image, @price, @pricingMode,
                     @bookable, @capacity, NULLIF(@openTime,''), NULLIF(@closeTime,''), @slotMin, @maxSlots,
                     @advanceDays, NULLIF(@maxPart,0), @requireApproval, @showWeb, @showPortal,
                     @duration, @location, @contact, @mapUrl, @icon, @rules, @order, @active,
                     @admin, GETDATE(), GETDATE());
                  SELECT CAST(SCOPE_IDENTITY() AS INT) AS NewID;", p);
            return dt?.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["NewID"]) : 0;
        }

        public void DeleteActivity(int activityId)
        {
            // มีการจองอยู่ → ปิดการใช้งานแทนการลบ (รักษาประวัติ/ยอดค้างชำระ)
            var used = _code.DatabaseQuerySafe(_conn,
                "SELECT TOP 1 ID FROM Activity_Bookings WHERE Activity_ID = @id",
                new Dictionary<string, object> { { "@id", activityId } });
            if (used?.Rows.Count > 0)
                _code.DatabaseInsertSafe(_conn,
                    "UPDATE Property_Activities SET IsActive = 0, LastUpdated = GETDATE() WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", activityId } });
            else
                _code.DatabaseInsertSafe(_conn,
                    "DELETE FROM Property_Activities WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", activityId } });
        }

        public void AddActivityImage(int activityId, string imagePath, string caption, int order)
        {
            _code.DatabaseInsertSafe(_conn,
                @"INSERT INTO Property_Activity_Images (Activity_ID, ImagePath, Caption, DisplayOrder)
                  VALUES (@id, @path, @cap, @ord)",
                new Dictionary<string, object>
                {
                    { "@id", activityId }, { "@path", imagePath },
                    { "@cap", (object)caption ?? DBNull.Value }, { "@ord", order }
                });
        }

        public void DeleteActivityImage(int imageId)
        {
            _code.DatabaseInsertSafe(_conn,
                "DELETE FROM Property_Activity_Images WHERE ID = @id",
                new Dictionary<string, object> { { "@id", imageId } });
        }

        // ── ตารางเวลา / ที่ว่าง ────────────────────────────────────────────────────

        public class TimeSlot
        {
            public TimeSpan Start, End;
            public int Booked;          // จองไปแล้วกี่คิว
            public int Capacity;        // รองรับได้กี่คิว
            public bool IsPast;
            public bool Available => !IsPast && Booked < Capacity;
            public string Label => $"{Start:hh\\:mm} - {End:hh\\:mm}";
            public int Remaining => Math.Max(0, Capacity - Booked);
        }

        /// <summary>ช่วงเวลาทั้งวันของกิจกรรม พร้อมจำนวนที่จองไปแล้ว (ใช้วาดตารางให้ผู้ใช้เลือก)</summary>
        public List<TimeSlot> GetDaySlots(int activityId, DateTime date)
        {
            var slots = new List<TimeSlot>();
            var act = GetActivity(activityId);
            if (act == null) return slots;

            int slotMin = ToInt(act["SlotMinutes"], 60);
            int capacity = Math.Max(1, ToInt(act["Capacity"], 1));
            TimeSpan open = act["OpenTime"] != DBNull.Value ? (TimeSpan)act["OpenTime"] : new TimeSpan(8, 0, 0);
            TimeSpan close = act["CloseTime"] != DBNull.Value ? (TimeSpan)act["CloseTime"] : new TimeSpan(21, 0, 0);
            if (close <= open) close = open.Add(TimeSpan.FromHours(12));

            var booked = _code.DatabaseQuerySafe(_conn,
                @"SELECT StartTime, EndTime, Participants FROM Activity_Bookings
                   WHERE Activity_ID = @id AND BookingDate = @d
                     AND Status IN ('PENDING','CONFIRMED')",
                new Dictionary<string, object> { { "@id", activityId }, { "@d", date.Date } });

            for (var t = open; t.Add(TimeSpan.FromMinutes(slotMin)) <= close; t = t.Add(TimeSpan.FromMinutes(slotMin)))
            {
                var slotStart = t;
                var slotEnd = t.Add(TimeSpan.FromMinutes(slotMin));
                int used = 0;
                if (booked != null)
                    foreach (DataRow r in booked.Rows)
                    {
                        var bs = (TimeSpan)r["StartTime"];
                        var be = (TimeSpan)r["EndTime"];
                        if (bs < slotEnd && be > slotStart) used++;   // ทับซ้อนกัน
                    }

                slots.Add(new TimeSlot
                {
                    Start = slotStart,
                    End = slotEnd,
                    Booked = used,
                    Capacity = capacity,
                    IsPast = date.Date < DateTime.Today
                             || (date.Date == DateTime.Today && slotStart <= DateTime.Now.TimeOfDay)
                });
            }
            return slots;
        }

        /// <summary>ตรวจว่าช่วงเวลานี้ยังจองได้ไหม (ใช้ก่อนบันทึกจริงเสมอ)</summary>
        public (bool Ok, string Message) CheckAvailability(int activityId, DateTime date, TimeSpan start, TimeSpan end, long? excludeBookingId = null)
        {
            var act = GetActivity(activityId);
            if (act == null) return (false, "ไม่พบกิจกรรมนี้");
            if (!ToBool(act["IsActive"])) return (false, "กิจกรรมนี้ปิดให้บริการอยู่");
            if (!ToBool(act["IsBookable"])) return (false, "กิจกรรมนี้ไม่ต้องจองเวลา ใช้บริการได้เลย");
            if (end <= start) return (false, "เวลาสิ้นสุดต้องมากกว่าเวลาเริ่ม");

            if (date.Date < DateTime.Today) return (false, "จองย้อนหลังไม่ได้");
            if (date.Date == DateTime.Today && start <= DateTime.Now.TimeOfDay)
                return (false, "ช่วงเวลานี้ผ่านไปแล้ว");

            int advance = ToInt(act["AdvanceBookingDays"], 14);
            if ((date.Date - DateTime.Today).TotalDays > advance)
                return (false, $"จองล่วงหน้าได้ไม่เกิน {advance} วัน");

            if (act["OpenTime"] != DBNull.Value && start < (TimeSpan)act["OpenTime"])
                return (false, $"เปิดให้บริการเวลา {((TimeSpan)act["OpenTime"]):hh\\:mm} น.");
            if (act["CloseTime"] != DBNull.Value && end > (TimeSpan)act["CloseTime"])
                return (false, $"ปิดให้บริการเวลา {((TimeSpan)act["CloseTime"]):hh\\:mm} น.");

            int slotMin = ToInt(act["SlotMinutes"], 60);
            int maxSlots = ToInt(act["MaxSlotsPerBooking"], 4);
            double mins = (end - start).TotalMinutes;
            if (slotMin > 0 && mins > slotMin * maxSlots)
                return (false, $"จองต่อเนื่องได้ไม่เกิน {maxSlots} ช่วง ({slotMin * maxSlots / 60.0:0.#} ชั่วโมง)");

            int capacity = Math.Max(1, ToInt(act["Capacity"], 1));
            var p = new Dictionary<string, object>
            {
                { "@id", activityId }, { "@d", date.Date },
                { "@s", start }, { "@e", end },
                { "@exclude", excludeBookingId ?? 0 }
            };
            var overlap = _code.DatabaseQuerySafe(_conn,
                @"SELECT COUNT(*) AS Cnt FROM Activity_Bookings
                   WHERE Activity_ID = @id AND BookingDate = @d
                     AND Status IN ('PENDING','CONFIRMED')
                     AND ID <> @exclude
                     AND StartTime < @e AND EndTime > @s", p);
            int used = overlap?.Rows.Count > 0 ? Convert.ToInt32(overlap.Rows[0]["Cnt"]) : 0;
            if (used >= capacity)
                return (false, capacity == 1
                    ? "ช่วงเวลานี้ถูกจองแล้ว กรุณาเลือกเวลาอื่น"
                    : $"ช่วงเวลานี้เต็มแล้ว (รองรับ {capacity} คิว)");

            return (true, "ว่าง");
        }

        /// <summary>คิดราคาตามรูปแบบที่ตั้งไว้</summary>
        public decimal CalculatePrice(DataRow act, TimeSpan start, TimeSpan end, int participants)
        {
            string mode = act["PricingMode"]?.ToString() ?? "FREE";
            decimal price = act["Price"] != DBNull.Value ? Convert.ToDecimal(act["Price"]) : 0m;
            if (mode == "FREE" || price <= 0) return 0m;

            switch (mode)
            {
                case "PER_HOUR":
                    decimal hours = (decimal)(end - start).TotalHours;
                    return Math.Round(price * hours, 2);
                case "PER_PERSON":
                    return Math.Round(price * Math.Max(1, participants), 2);
                default: // PER_SESSION
                    return price;
            }
        }

        // ── การจอง ────────────────────────────────────────────────────────────────

        public class BookingRequest
        {
            public int ActivityId;
            public int? ReservationId;
            public string CustomerPhone, GuestName, Notes, SlipUrl;
            public byte? AccommodationId;
            public DateTime Date;
            public TimeSpan Start, End;
            public int Participants = 1;
            public string PaymentMethod = "NONE";   // NONE / ROOM_CHARGE / TRANSFER / CASH
            public string BookedVia = "PORTAL";
            public short? AdminId;
        }

        public class BookingResult
        {
            public bool Success;
            public long BookingId;
            public decimal Amount;
            public string Message;
            public string Status, PaymentStatus;
        }

        /// <summary>สร้างการจอง + ผูกการชำระเงินตามช่องทางที่เลือก (idempotent ระดับช่วงเวลา)</summary>
        public BookingResult CreateBooking(BookingRequest req)
        {
            var res = new BookingResult();
            try
            {
                var act = GetActivity(req.ActivityId);
                if (act == null) { res.Message = "ไม่พบกิจกรรมนี้"; return res; }

                var (ok, msg) = CheckAvailability(req.ActivityId, req.Date, req.Start, req.End);
                if (!ok) { res.Message = msg; return res; }

                int maxPart = ToInt(act["MaxParticipants"], 0);
                if (maxPart > 0 && req.Participants > maxPart)
                { res.Message = $"จองได้สูงสุด {maxPart} คนต่อครั้ง"; return res; }

                decimal amount = CalculatePrice(act, req.Start, req.End, req.Participants);
                string pricingMode = act["PricingMode"]?.ToString() ?? "FREE";
                decimal unitPrice = act["Price"] != DBNull.Value ? Convert.ToDecimal(act["Price"]) : 0m;
                decimal hours = (decimal)(req.End - req.Start).TotalHours;

                // ตรวจความสมเหตุสมผลของช่องทางจ่าย
                string payMethod = amount <= 0 ? "NONE" : (req.PaymentMethod ?? "NONE");
                if (amount > 0 && payMethod == "NONE") payMethod = "CASH";
                if (payMethod == "ROOM_CHARGE" && (!req.ReservationId.HasValue || req.ReservationId.Value <= 0))
                { res.Message = "ชาร์จเข้าห้องได้เฉพาะผู้ที่กำลังเข้าพัก"; return res; }

                string payStatus = amount <= 0 ? "WAIVED"
                    : payMethod == "TRANSFER" ? (string.IsNullOrEmpty(req.SlipUrl) ? "UNPAID" : "PENDING_VERIFY")
                    : "UNPAID";

                // ต้องอนุมัติก่อน หรือ โอนแล้วรอตรวจสลิป → PENDING
                bool requireApproval = ToBool(act["RequireApproval"]);
                string status = (requireApproval || payStatus == "PENDING_VERIFY") ? "PENDING" : "CONFIRMED";

                var p = new Dictionary<string, object>
                {
                    { "@act", req.ActivityId },
                    { "@res", (object)req.ReservationId ?? DBNull.Value },
                    { "@phone", (object)req.CustomerPhone ?? DBNull.Value },
                    { "@guest", (object)req.GuestName ?? DBNull.Value },
                    { "@accom", (object)req.AccommodationId ?? DBNull.Value },
                    { "@date", req.Date.Date },
                    { "@start", req.Start },
                    { "@end", req.End },
                    { "@part", Math.Max(1, req.Participants) },
                    { "@mode", pricingMode },
                    { "@unit", unitPrice },
                    { "@hours", hours },
                    { "@total", amount },
                    { "@status", status },
                    { "@payMethod", payMethod },
                    { "@payStatus", payStatus },
                    { "@slip", (object)req.SlipUrl ?? DBNull.Value },
                    { "@slipDate", string.IsNullOrEmpty(req.SlipUrl) ? (object)DBNull.Value : DateTime.Now },
                    { "@notes", (object)req.Notes ?? DBNull.Value },
                    { "@via", req.BookedVia ?? "PORTAL" },
                    { "@admin", (object)req.AdminId ?? DBNull.Value }
                };

                var dt = _code.DatabaseQuerySafe(_conn,
                    @"INSERT INTO Activity_Bookings
                        (Activity_ID, Reservation_ID, Customer_MobilePhone, GuestName, Accommodation_ID,
                         BookingDate, StartTime, EndTime, Participants, PricingMode, UnitPrice, Hours,
                         TotalAmount, Status, PaymentMethod, PaymentStatus, SlipFileURL, SlipUploadedDate,
                         Notes, BookedVia, CreatedBy_AdminID, CreatedDate)
                      VALUES
                        (@act, @res, @phone, @guest, @accom, @date, @start, @end, @part, @mode, @unit,
                         @hours, @total, @status, @payMethod, @payStatus, @slip, @slipDate,
                         @notes, @via, @admin, GETDATE());
                      SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS NewID;", p);

                if (dt == null || dt.Rows.Count == 0) { res.Message = "บันทึกการจองไม่สำเร็จ"; return res; }
                long bookingId = Convert.ToInt64(dt.Rows[0]["NewID"]);

                // ชาร์จเข้าห้อง → ลงค่าใช้จ่ายเข้าการจองห้องพัก (ไปรวมจ่ายตอนเช็คเอาท์)
                if (payMethod == "ROOM_CHARGE" && amount > 0)
                {
                    long chargeId = CreateRoomChargeForBooking(bookingId, req.ReservationId.Value,
                        act["ActivityName"]?.ToString(), amount, req.AdminId,
                        $"กิจกรรม {act["ActivityName"]} {req.Date:dd/MM/yyyy} {req.Start:hh\\:mm}-{req.End:hh\\:mm}");
                    if (chargeId > 0)
                        _code.DatabaseInsertSafe(_conn,
                            "UPDATE Activity_Bookings SET Charge_ID = @c WHERE ID = @id",
                            new Dictionary<string, object> { { "@c", chargeId }, { "@id", bookingId } });
                }

                _code.Logs(_conn, "ActivityBooking",
                    $"จอง #{bookingId} {act["ActivityName"]} {req.Date:yyyy-MM-dd} {req.Start:hh\\:mm}-{req.End:hh\\:mm} " +
                    $"ยอด {amount:N2} ({payMethod}/{payStatus}) res={req.ReservationId}", "SYSTEM");

                res.Success = true;
                res.BookingId = bookingId;
                res.Amount = amount;
                res.Status = status;
                res.PaymentStatus = payStatus;
                res.Message = BuildConfirmMessage(status, payMethod, payStatus, amount);
                return res;
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "ActivityBooking", "CreateBooking error: " + ex.Message, "SYSTEM");
                res.Message = "เกิดข้อผิดพลาด: " + ex.Message;
                return res;
            }
        }

        private static string BuildConfirmMessage(string status, string payMethod, string payStatus, decimal amount)
        {
            if (amount <= 0)
                return status == "PENDING"
                    ? "ส่งคำขอจองแล้ว รอเจ้าหน้าที่ยืนยัน"
                    : "จองสำเร็จ! ไม่มีค่าใช้จ่าย";
            switch (payMethod)
            {
                case "ROOM_CHARGE":
                    return $"จองสำเร็จ! ค่าบริการ {amount:N2} บาท ถูกบันทึกเข้าห้องพัก จ่ายรวมตอนเช็คเอาท์";
                case "TRANSFER":
                    return payStatus == "PENDING_VERIFY"
                        ? $"ส่งคำขอจองแล้ว (ยอด {amount:N2} บาท) — รอเจ้าหน้าที่ตรวจสอบสลิป"
                        : $"จองไว้แล้ว (ยอด {amount:N2} บาท) — กรุณาโอนเงินและแนบสลิปเพื่อยืนยัน";
                default:
                    return $"จองสำเร็จ! ค่าบริการ {amount:N2} บาท ชำระที่เคาน์เตอร์";
            }
        }

        /// <summary>ลงค่ากิจกรรมเป็นค่าใช้จ่ายในห้อง (Product_ID = NULL — ไม่ใช่สินค้าในสต๊อก)</summary>
        private long CreateRoomChargeForBooking(long bookingId, int reservationId, string activityName,
            decimal amount, short? adminId, string notes)
        {
            try
            {
                return _code.DatabaseInsertReturnSafe(_conn,
                    @"INSERT INTO Reservation_Product_Charges
                        (Reservation_ID, Product_ID, Product_Name, Quantity, UnitPrice, TotalAmount,
                         ChargeType, ChargedBy_AdminID, Notes, Status, IsPaid, StockDeducted, Activity_Booking_ID)
                      VALUES
                        (@res, NULL, @name, 1, @amount, @amount,
                         'ROOM_CHARGE', @admin, @notes, 'PENDING', 0, 0, @booking);
                      SELECT SCOPE_IDENTITY();",
                    new Dictionary<string, object>
                    {
                        { "@res", reservationId },
                        { "@name", "กิจกรรม: " + (activityName ?? "-") },
                        { "@amount", amount },
                        { "@admin", (object)adminId ?? DBNull.Value },
                        { "@notes", (object)notes ?? DBNull.Value },
                        { "@booking", bookingId }
                    });
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "ActivityBooking",
                    $"CreateRoomChargeForBooking failed (booking={bookingId}): {ex.Message}", "SYSTEM");
                return 0;
            }
        }

        // ── จัดการการจอง ──────────────────────────────────────────────────────────

        public DataTable GetBookings(DateTime? from = null, DateTime? to = null, string status = null,
            int? activityId = null, int? reservationId = null)
        {
            var p = new Dictionary<string, object>();
            var w = new List<string>();
            if (from.HasValue) { w.Add("b.BookingDate >= @from"); p["@from"] = from.Value.Date; }
            if (to.HasValue) { w.Add("b.BookingDate <= @to"); p["@to"] = to.Value.Date; }
            if (!string.IsNullOrEmpty(status)) { w.Add("b.Status = @st"); p["@st"] = status; }
            if (activityId.HasValue) { w.Add("b.Activity_ID = @act"); p["@act"] = activityId.Value; }
            if (reservationId.HasValue) { w.Add("b.Reservation_ID = @res"); p["@res"] = reservationId.Value; }
            string where = w.Count > 0 ? "WHERE " + string.Join(" AND ", w) : "";

            return _code.DatabaseQuerySafe(_conn,
                $@"SELECT b.*, a.ActivityName, a.IconClass, a.Location,
                          acc.AccomName AS Accommodation_Name
                     FROM Activity_Bookings b
                     INNER JOIN Property_Activities a ON a.ID = b.Activity_ID
                     LEFT JOIN Accommodation acc ON acc.ID = b.Accommodation_ID
                     {where}
                    ORDER BY b.BookingDate DESC, b.StartTime DESC", p);
        }

        public DataTable GetBookingsForReservation(int reservationId)
        {
            return _code.DatabaseQuerySafe(_conn,
                @"SELECT b.*, a.ActivityName, a.IconClass
                    FROM Activity_Bookings b
                    INNER JOIN Property_Activities a ON a.ID = b.Activity_ID
                   WHERE b.Reservation_ID = @res AND b.Status <> 'CANCELLED'
                   ORDER BY b.BookingDate DESC, b.StartTime DESC",
                new Dictionary<string, object> { { "@res", reservationId } });
        }

        public DataRow GetBooking(long bookingId)
        {
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT b.*, a.ActivityName, a.IconClass, a.Location
                    FROM Activity_Bookings b
                    INNER JOIN Property_Activities a ON a.ID = b.Activity_ID
                   WHERE b.ID = @id",
                new Dictionary<string, object> { { "@id", bookingId } });
            return dt?.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        /// <summary>ยกเลิกการจอง + ยกเลิกค่าใช้จ่ายที่ชาร์จเข้าห้องด้วย (ถ้ายังไม่จ่าย)</summary>
        public (bool Ok, string Message) CancelBooking(long bookingId, string reason, short? adminId)
        {
            var b = GetBooking(bookingId);
            if (b == null) return (false, "ไม่พบการจองนี้");
            if (b["Status"].ToString() == "CANCELLED") return (false, "การจองนี้ถูกยกเลิกไปแล้ว");
            if (b["PaymentStatus"].ToString() == "PAID")
                return (false, "การจองนี้ชำระเงินแล้ว — กรุณาติดต่อเจ้าหน้าที่เพื่อคืนเงิน");

            _code.DatabaseInsertSafe(_conn,
                @"UPDATE Activity_Bookings
                     SET Status = 'CANCELLED', CancelledDate = GETDATE(),
                         CancelledBy_AdminID = @admin, CancelReason = @reason
                   WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@id", bookingId },
                    { "@admin", (object)adminId ?? DBNull.Value },
                    { "@reason", (object)reason ?? DBNull.Value }
                });

            // ค่าใช้จ่ายที่ชาร์จเข้าห้อง → ยกเลิกด้วย (ถ้ายังไม่ถูกจ่าย)
            if (b["Charge_ID"] != DBNull.Value)
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Reservation_Product_Charges
                         SET Status = 'CANCELLED', CancelledBy_AdminID = @admin, CancelledDate = GETDATE()
                       WHERE ID = @cid AND IsPaid = 0",
                    new Dictionary<string, object>
                    {
                        { "@cid", Convert.ToInt64(b["Charge_ID"]) },
                        { "@admin", (object)adminId ?? DBNull.Value }
                    });

            _code.Logs(_conn, "ActivityBooking", $"ยกเลิกการจอง #{bookingId}: {reason}", "SYSTEM");
            return (true, "ยกเลิกการจองแล้ว");
        }

        /// <summary>แนบสลิปโอนเงิน (ผู้เข้าพักทำเองได้จาก Portal) → เข้าสถานะรอตรวจสอบ</summary>
        public (bool Ok, string Message) AttachSlip(long bookingId, string slipUrl)
        {
            if (string.IsNullOrEmpty(slipUrl)) return (false, "ไม่พบไฟล์สลิป");
            _code.DatabaseInsertSafe(_conn,
                @"UPDATE Activity_Bookings
                     SET SlipFileURL = @slip, SlipUploadedDate = GETDATE(),
                         PaymentMethod = 'TRANSFER', PaymentStatus = 'PENDING_VERIFY',
                         RejectionReason = NULL
                   WHERE ID = @id AND PaymentStatus <> 'PAID'",
                new Dictionary<string, object> { { "@slip", slipUrl }, { "@id", bookingId } });
            return (true, "อัปโหลดสลิปแล้ว รอเจ้าหน้าที่ตรวจสอบ");
        }

        /// <summary>เจ้าหน้าที่อนุมัติ/ปฏิเสธสลิป หรือยืนยันการจองที่รออนุมัติ</summary>
        public (bool Ok, string Message) ReviewBooking(long bookingId, bool approve, string reason, short? adminId)
        {
            var b = GetBooking(bookingId);
            if (b == null) return (false, "ไม่พบการจองนี้");

            if (approve)
            {
                bool hasSlip = b["SlipFileURL"] != DBNull.Value && !string.IsNullOrEmpty(b["SlipFileURL"].ToString());
                decimal amount = Convert.ToDecimal(b["TotalAmount"]);
                string newPayStatus = amount <= 0 ? "WAIVED" : (hasSlip ? "PAID" : b["PaymentStatus"].ToString());

                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Activity_Bookings
                         SET Status = 'CONFIRMED', PaymentStatus = @ps,
                             VerifiedBy_AdminID = @admin, VerifiedDate = GETDATE(), RejectionReason = NULL
                       WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@id", bookingId }, { "@ps", newPayStatus },
                        { "@admin", (object)adminId ?? DBNull.Value }
                    });
                return (true, "ยืนยันการจองแล้ว" + (newPayStatus == "PAID" ? " (บันทึกว่าชำระเงินแล้ว)" : ""));
            }

            _code.DatabaseInsertSafe(_conn,
                @"UPDATE Activity_Bookings
                     SET Status = 'CANCELLED', PaymentStatus = CASE WHEN PaymentStatus = 'PENDING_VERIFY'
                                                                    THEN 'UNPAID' ELSE PaymentStatus END,
                         RejectionReason = @reason, VerifiedBy_AdminID = @admin, VerifiedDate = GETDATE(),
                         CancelledDate = GETDATE()
                   WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@id", bookingId }, { "@reason", (object)reason ?? DBNull.Value },
                    { "@admin", (object)adminId ?? DBNull.Value }
                });
            return (true, "ปฏิเสธการจองแล้ว");
        }

        /// <summary>บันทึกว่าชำระเงินแล้ว (เช่น จ่ายสดที่เคาน์เตอร์)</summary>
        public void MarkPaid(long bookingId, short? adminId)
        {
            _code.DatabaseInsertSafe(_conn,
                @"UPDATE Activity_Bookings SET PaymentStatus = 'PAID',
                         VerifiedBy_AdminID = @admin, VerifiedDate = GETDATE() WHERE ID = @id",
                new Dictionary<string, object>
                { { "@id", bookingId }, { "@admin", (object)adminId ?? DBNull.Value } });
        }

        /// <summary>จำนวนรายการที่รอเจ้าหน้าที่ดำเนินการ (โชว์ badge หน้า Admin)</summary>
        public int GetPendingCount()
        {
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT COUNT(*) AS Cnt FROM Activity_Bookings
                   WHERE Status = 'PENDING' OR PaymentStatus = 'PENDING_VERIFY'", null);
            return dt?.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["Cnt"]) : 0;
        }

        // ── helpers ───────────────────────────────────────────────────────────────
        private static int ToInt(object v, int def)
        {
            if (v == null || v == DBNull.Value) return def;
            return int.TryParse(v.ToString(), out var i) ? i : def;
        }
        private static bool ToBool(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            if (v is bool b) return b;
            string s = v.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class ActivityFormExtensions
    {
        public static string Get(this Dictionary<string, object> d, string key, string def = "")
        {
            return d.ContainsKey(key) && d[key] != null ? d[key].ToString() : def;
        }
        public static int GetInt(this Dictionary<string, object> d, string key, int def)
        {
            return d.ContainsKey(key) && int.TryParse(d[key]?.ToString(), out var v) ? v : def;
        }
        public static decimal GetDecimal(this Dictionary<string, object> d, string key)
        {
            return d.ContainsKey(key) && decimal.TryParse(d[key]?.ToString(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }
        public static bool GetBool(this Dictionary<string, object> d, string key, bool def = false)
        {
            if (!d.ContainsKey(key) || d[key] == null) return def;
            string s = d[key].ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "on";
        }
    }
}
