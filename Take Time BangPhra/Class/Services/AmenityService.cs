using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// เบิกของใช้ในห้อง (Amenities) จาก Guest Portal
    ///
    /// กติกาค่าใช้จ่ายตั้งได้ 3 แบบต่อรายการ:
    ///   • ฟรีเสมอ                    (Is_Free = 1)
    ///   • ฟรี N ชิ้นแรกต่อการเข้าพัก  (Is_Free = 0, Free_Quota_Per_Stay > 0) เกินนั้นคิดตามราคา
    ///   • คิดเงินทุกชิ้น             (Is_Free = 0, Free_Quota_Per_Stay = 0)
    ///
    /// ⚠ ราคาคิดที่ฝั่งเซิร์ฟเวอร์เสมอ ไม่เชื่อตัวเลขที่ส่งมาจากหน้าเว็บ —
    ///   ไม่งั้นแก้ค่าใน devtools แล้วเบิกของราคาแพงเป็นฟรีได้
    /// โควตาฟรีนับจากใบเบิกเดิมของ "การเข้าพักครั้งนี้" (Reservation_ID) ที่ยังไม่ถูกยกเลิก
    /// </summary>
    public class AmenityService
    {
        private readonly string _conn;
        private readonly code _code = new code();

        public AmenityService(string connectionString)
        {
            _conn = connectionString;
        }

        private static bool? _hasTables;

        /// <summary>รันไมเกรชัน PHASE19_02 แล้วหรือยัง — ยังไม่รัน หน้าเว็บจะซ่อนส่วนนี้แทนที่จะพัง</summary>
        public bool IsReady
        {
            get
            {
                if (_hasTables.HasValue) return _hasTables.Value;
                try
                {
                    var dt = _code.DatabaseQuerySafe(_conn,
                        @"SELECT COUNT(*) AS N FROM INFORMATION_SCHEMA.TABLES
                          WHERE TABLE_NAME IN ('Guest_Amenity_Item','Guest_Amenity_Request','Guest_Amenity_Request_Item')",
                        null);
                    _hasTables = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["N"]) >= 3;
                }
                catch { _hasTables = false; }
                return _hasTables.Value;
            }
        }

        public static void ResetSchemaCache() { _hasTables = null; }

        // ── รายการของใช้ ─────────────────────────────────────────────────────────

        public DataTable GetItems(bool activeOnly = true)
        {
            if (!IsReady) return new DataTable();
            try
            {
                string where = activeOnly ? "WHERE Status = 'True'" : "";
                return _code.DatabaseQuerySafe(_conn,
                    $@"SELECT ID, Name, Description, Category, Image_Path, Icon,
                              Is_Free, Price, Free_Quota_Per_Stay, Unit, Max_Per_Request,
                              Sort_Order, Status
                       FROM Guest_Amenity_Item {where}
                       ORDER BY Sort_Order, Name", null);
            }
            catch { return new DataTable(); }
        }

        public DataRow GetItem(int id)
        {
            foreach (DataRow r in GetItems(false).Rows)
                if (Convert.ToInt32(r["ID"]) == id) return r;
            return null;
        }

        public int SaveItem(AmenityItemInput input)
        {
            if (!IsReady || input == null) return 0;
            var p = new Dictionary<string, object>
            {
                { "@name", (input.Name ?? "").Trim() },
                { "@desc", (object)input.Description ?? DBNull.Value },
                { "@cat", (object)input.Category ?? DBNull.Value },
                { "@img", (object)input.ImagePath ?? DBNull.Value },
                { "@icon", (object)input.Icon ?? DBNull.Value },
                { "@free", input.IsFree ? 1 : 0 },
                { "@price", input.Price },
                { "@quota", input.FreeQuotaPerStay },
                { "@unit", (object)input.Unit ?? DBNull.Value },
                { "@max", input.MaxPerRequest <= 0 ? 5 : input.MaxPerRequest },
                { "@ord", input.SortOrder },
                { "@st", input.Active ? "True" : "False" }
            };

            if (input.Id > 0)
            {
                p.Add("@id", input.Id);
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Guest_Amenity_Item
                      SET Name = @name, Description = @desc, Category = @cat, Image_Path = @img, Icon = @icon,
                          Is_Free = @free, Price = @price, Free_Quota_Per_Stay = @quota, Unit = @unit,
                          Max_Per_Request = @max, Sort_Order = @ord, Status = @st
                      WHERE ID = @id", p);
                return input.Id;
            }

            _code.DatabaseInsertSafe(_conn,
                @"INSERT INTO Guest_Amenity_Item
                  (Name, Description, Category, Image_Path, Icon, Is_Free, Price,
                   Free_Quota_Per_Stay, Unit, Max_Per_Request, Sort_Order, Status)
                  VALUES (@name, @desc, @cat, @img, @icon, @free, @price,
                          @quota, @unit, @max, @ord, @st)", p);
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 ID FROM Guest_Amenity_Item ORDER BY ID DESC", null);
                return dt?.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["ID"]) : 0;
            }
            catch { return 0; }
        }

        public bool DeleteItem(int id)
        {
            if (!IsReady) return false;
            try
            {
                // ซ่อนแทนการลบ — ใบเบิกเก่ายังอ้างชื่อ/ราคาที่บันทึกไว้ในบรรทัดของมันเอง
                _code.DatabaseInsertSafe(_conn,
                    "UPDATE Guest_Amenity_Item SET Status = 'False' WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                return true;
            }
            catch { return false; }
        }

        // ── โควตาฟรีที่ใช้ไปแล้วในการเข้าพักนี้ ───────────────────────────────────

        /// <summary>จำนวนที่เบิกไปแล้วต่อรายการ ของการเข้าพักนี้ (ไม่นับใบที่ยกเลิก)</summary>
        public Dictionary<int, int> GetUsedQuantities(long reservationId)
        {
            var used = new Dictionary<int, int>();
            if (!IsReady) return used;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT i.Item_ID, SUM(i.Quantity) AS Qty
                      FROM Guest_Amenity_Request_Item i
                      JOIN Guest_Amenity_Request r ON r.ID = i.Request_ID
                      WHERE r.Reservation_ID = @res AND r.Status <> 'CANCELLED' AND i.Item_ID IS NOT NULL
                      GROUP BY i.Item_ID",
                    new Dictionary<string, object> { { "@res", reservationId } });
                foreach (DataRow r in dt.Rows)
                    used[Convert.ToInt32(r["Item_ID"])] = Convert.ToInt32(r["Qty"]);
            }
            catch { }
            return used;
        }

        /// <summary>
        /// คิดราคาของรายการหนึ่งบรรทัด ตามกติกาและโควตาที่เหลือ
        /// คืน (จำนวนที่ได้ฟรี, จำนวนที่คิดเงิน, ยอดรวม)
        /// </summary>
        public static (int FreeQty, int PaidQty, decimal Subtotal) PriceLine(
            bool isFree, decimal price, int freeQuota, int alreadyUsed, int qty)
        {
            if (qty <= 0) return (0, 0, 0m);
            if (isFree) return (qty, 0, 0m);

            int remainingFree = Math.Max(0, freeQuota - alreadyUsed);
            int freeQty = Math.Min(remainingFree, qty);
            int paidQty = qty - freeQty;
            return (freeQty, paidQty, paidQty * price);
        }

        // ── ใบเบิก ───────────────────────────────────────────────────────────────

        /// <summary>
        /// สร้างใบเบิก — คิดราคาใหม่ทั้งหมดจากฐานข้อมูล ไม่ใช้ราคาที่ส่งมาจากหน้าเว็บ
        /// </summary>
        public AmenityRequestResult CreateRequest(long reservationId, string mobilePhone, short? accommodationId,
            Dictionary<int, int> quantities, string note)
        {
            var result = new AmenityRequestResult();
            if (!IsReady) { result.Error = "ระบบเบิกของใช้ยังไม่พร้อม (ยังไม่ได้รันไมเกรชัน PHASE19_02)"; return result; }
            if (quantities == null || quantities.Count == 0) { result.Error = "ยังไม่ได้เลือกของที่ต้องการเบิก"; return result; }

            var items = GetItems();
            var used = GetUsedQuantities(reservationId);
            var lines = new List<AmenityLine>();
            decimal total = 0m;

            foreach (var kv in quantities)
            {
                if (kv.Value <= 0) continue;
                DataRow item = null;
                foreach (DataRow r in items.Rows)
                    if (Convert.ToInt32(r["ID"]) == kv.Key) { item = r; break; }
                if (item == null) continue;              // ปิดใช้งาน/ถูกลบระหว่างที่หน้าเว็บเปิดค้าง

                int maxPer = ToInt(item["Max_Per_Request"], 5);
                int qty = Math.Min(kv.Value, maxPer <= 0 ? 5 : maxPer);

                bool isFree = ToBool(item["Is_Free"]);
                decimal price = ToDec(item["Price"]);
                int quota = ToInt(item["Free_Quota_Per_Stay"], 0);
                int alreadyUsed = used.ContainsKey(kv.Key) ? used[kv.Key] : 0;

                var priced = PriceLine(isFree, price, quota, alreadyUsed, qty);
                total += priced.Subtotal;

                lines.Add(new AmenityLine
                {
                    ItemId = kv.Key,
                    Name = item["Name"].ToString(),
                    Quantity = qty,
                    FreeQty = priced.FreeQty,
                    UnitPrice = price,
                    Subtotal = priced.Subtotal
                });
            }

            if (lines.Count == 0) { result.Error = "ไม่พบรายการที่เบิกได้"; return result; }

            string number = "AM" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999);
            string payMethod = total > 0 ? "CHARGE_TO_ROOM" : "FREE";

            try
            {
                var head = new Dictionary<string, object>
                {
                    { "@num", number },
                    { "@res", reservationId },
                    { "@phone", (object)mobilePhone ?? DBNull.Value },
                    { "@acc", accommodationId.HasValue ? (object)accommodationId.Value : DBNull.Value },
                    { "@note", (object)note ?? DBNull.Value },
                    { "@total", total },
                    { "@pay", payMethod }
                };
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"INSERT INTO Guest_Amenity_Request
                      (Request_Number, Reservation_ID, Customer_MobilePhone, Accommodation_ID, Note, Total_Amount, Payment_Method)
                      VALUES (@num, @res, @phone, @acc, @note, @total, @pay);
                      SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", head);
                result.RequestId = dt.Rows.Count > 0 ? Convert.ToInt64(dt.Rows[0][0]) : 0;
            }
            catch (Exception ex)
            {
                result.Error = "บันทึกใบเบิกไม่สำเร็จ: " + ex.Message;
                return result;
            }

            if (result.RequestId <= 0) { result.Error = "บันทึกใบเบิกไม่สำเร็จ"; return result; }

            foreach (var l in lines)
            {
                try
                {
                    _code.DatabaseInsertSafe(_conn,
                        @"INSERT INTO Guest_Amenity_Request_Item
                          (Request_ID, Item_ID, Item_Name, Quantity, Free_Qty, Unit_Price, Subtotal)
                          VALUES (@req, @item, @name, @qty, @free, @price, @sub)",
                        new Dictionary<string, object>
                        {
                            { "@req", result.RequestId }, { "@item", l.ItemId }, { "@name", l.Name },
                            { "@qty", l.Quantity }, { "@free", l.FreeQty },
                            { "@price", l.UnitPrice }, { "@sub", l.Subtotal }
                        });
                }
                catch { }
            }

            result.RequestNumber = number;
            result.TotalAmount = total;
            result.Lines = lines;
            result.Ok = true;
            return result;
        }

        public DataTable GetRequests(long reservationId)
        {
            if (!IsReady) return new DataTable();
            try
            {
                return _code.DatabaseQuerySafe(_conn,
                    @"SELECT ID, Request_Number, Note, Total_Amount, Payment_Method, Status,
                             Requested_Date, Completed_Date
                      FROM Guest_Amenity_Request
                      WHERE Reservation_ID = @res
                      ORDER BY Requested_Date DESC",
                    new Dictionary<string, object> { { "@res", reservationId } });
            }
            catch { return new DataTable(); }
        }

        /// <summary>ใบเบิกฝั่งพนักงาน — ค่าเริ่มต้นแสดงเฉพาะที่ยังไม่จบงาน</summary>
        public DataTable GetStaffRequests(string status = null)
        {
            if (!IsReady) return new DataTable();
            try
            {
                string where = string.IsNullOrWhiteSpace(status)
                    ? "WHERE r.Status IN ('PENDING','ACCEPTED')"
                    : "WHERE r.Status = @st";
                var p = string.IsNullOrWhiteSpace(status)
                    ? null
                    : new Dictionary<string, object> { { "@st", status } };

                return _code.DatabaseQuerySafe(_conn,
                    $@"SELECT r.ID, r.Request_Number, r.Reservation_ID, r.Customer_MobilePhone,
                              r.Note, r.Total_Amount, r.Payment_Method, r.Status,
                              r.Requested_Date, r.Completed_Date,
                              -- คอลัมน์ชื่อห้องจริงคือ AccomName (ไม่ใช่ Accommodation_Name)
                              ISNULL(a.AccomName, CAST(r.Accommodation_ID AS NVARCHAR(20))) AS RoomName,
                              STUFF((SELECT ', ' + i.Item_Name + ' x' + CAST(i.Quantity AS NVARCHAR(10))
                                     FROM Guest_Amenity_Request_Item i
                                     WHERE i.Request_ID = r.ID
                                     FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS ItemSummary
                       FROM Guest_Amenity_Request r
                       LEFT JOIN Accommodation a ON a.ID = r.Accommodation_ID
                       {where}
                       ORDER BY r.Requested_Date DESC", p);
            }
            catch { return new DataTable(); }
        }

        public DataTable GetRequestItems(long requestId)
        {
            if (!IsReady) return new DataTable();
            try
            {
                return _code.DatabaseQuerySafe(_conn,
                    @"SELECT Item_Name, Quantity, Free_Qty, Unit_Price, Subtotal
                      FROM Guest_Amenity_Request_Item WHERE Request_ID = @id ORDER BY ID",
                    new Dictionary<string, object> { { "@id", requestId } });
            }
            catch { return new DataTable(); }
        }

        public bool UpdateStatus(long requestId, string status, short? staffId = null)
        {
            if (!IsReady) return false;
            try
            {
                bool done = status == "DELIVERED" || status == "CANCELLED";
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Guest_Amenity_Request
                      SET Status = @st,
                          Staff_ID = ISNULL(@staff, Staff_ID),
                          Completed_Date = CASE WHEN @done = 1 THEN GETDATE() ELSE Completed_Date END
                      WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@st", status }, { "@id", requestId },
                        { "@staff", staffId.HasValue ? (object)staffId.Value : DBNull.Value },
                        { "@done", done ? 1 : 0 }
                    });
                return true;
            }
            catch { return false; }
        }

        // ── แจ้งเตือนพนักงาน ─────────────────────────────────────────────────────

        /// <summary>
        /// แจ้งเตือนเมื่อมีใบเบิกใหม่ — เข้า Telegram (กลุ่มเดียวกับที่ระบบใช้อยู่)
        /// และแจ้งในระบบให้พนักงานทุกคน. ล้มเหลวไม่กระทบการบันทึกใบเบิก
        /// </summary>
        public void NotifyNewRequest(AmenityRequestResult req, string roomName, string guestName)
        {
            if (req == null || !req.Ok) return;

            var sb = new StringBuilder();
            sb.AppendLine("🛎️ <b>มีคำขอเบิกของใช้ใหม่</b>");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(roomName)) sb.AppendLine("🚪 ห้อง: " + roomName);
            if (!string.IsNullOrWhiteSpace(guestName)) sb.AppendLine("👤 ผู้เข้าพัก: " + guestName);
            sb.AppendLine("🧾 เลขที่: " + req.RequestNumber);
            sb.AppendLine();
            foreach (var l in req.Lines)
            {
                string qty = "x" + l.Quantity;
                string money = l.Subtotal > 0
                    ? " — " + l.Subtotal.ToString("N0") + " บาท" + (l.FreeQty > 0 ? " (ฟรี " + l.FreeQty + ")" : "")
                    : " — ฟรี";
                sb.AppendLine("• " + l.Name + " " + qty + money);
            }
            sb.AppendLine();
            sb.AppendLine(req.TotalAmount > 0
                ? "💰 รวม " + req.TotalAmount.ToString("N0") + " บาท (คิดเข้าห้องพัก)"
                : "🎁 ไม่มีค่าใช้จ่าย");
            sb.AppendLine();
            sb.AppendLine("🕐 " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));

            string text = sb.ToString();

            try
            {
                var bot = new TelegramService();
                bot.SendMessageAsync(AppCfg.Get("TelegramChatId", "-4969611371"), text)
                   .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "Amenity", "แจ้งเตือน Telegram ไม่สำเร็จ: " + ex.Message, "SYSTEM");
            }

            try
            {
                var notify = new NotificationService(_conn);
                notify.NotifyAllStaff(
                    "คำขอเบิกของใช้ใหม่" + (string.IsNullOrWhiteSpace(roomName) ? "" : " — ห้อง " + roomName),
                    text.Replace("<b>", "").Replace("</b>", ""),
                    "AMENITY_REQUEST",
                    "NORMAL");
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "Amenity", "แจ้งเตือนในระบบไม่สำเร็จ: " + ex.Message, "SYSTEM");
            }

            try { _code.Logs(_conn, "Amenity", "ใบเบิกใหม่ " + req.RequestNumber + " ห้อง " + roomName, "GUEST"); }
            catch { }
        }

        // ── helper ───────────────────────────────────────────────────────────────

        public static string StatusText(string status)
        {
            switch ((status ?? "").ToUpperInvariant())
            {
                case "PENDING": return "รอรับเรื่อง";
                case "ACCEPTED": return "กำลังจัดของ";
                case "DELIVERED": return "ส่งแล้ว";
                case "CANCELLED": return "ยกเลิก";
                default: return status;
            }
        }

        /// <summary>ข้อความอธิบายเงื่อนไขค่าใช้จ่าย สำหรับแสดงบนการ์ด</summary>
        public static string PriceLabel(bool isFree, decimal price, int quota, int alreadyUsed, string unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) unit = "ชิ้น";
            if (isFree) return "ฟรี";
            if (quota > 0)
            {
                int left = Math.Max(0, quota - alreadyUsed);
                return left > 0
                    ? "ฟรีอีก " + left + " " + unit + " · เกินนั้น " + price.ToString("N0") + " บาท"
                    : price.ToString("N0") + " บาท/" + unit + " (ใช้สิทธิ์ฟรีครบแล้ว)";
            }
            return price.ToString("N0") + " บาท/" + unit;
        }

        private static bool ToBool(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            if (v is bool b) return b;
            string s = v.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static int ToInt(object v, int def)
        {
            if (v == null || v == DBNull.Value) return def;
            int r;
            return int.TryParse(v.ToString(), out r) ? r : def;
        }

        private static decimal ToDec(object v)
        {
            if (v == null || v == DBNull.Value) return 0m;
            decimal r;
            return decimal.TryParse(v.ToString(), out r) ? r : 0m;
        }
    }

    public class AmenityItemInput
    {
        public int Id;
        public string Name, Description, Category, ImagePath, Icon, Unit;
        public bool IsFree = true;
        public decimal Price;
        public int FreeQuotaPerStay, MaxPerRequest = 5, SortOrder;
        public bool Active = true;
    }

    public class AmenityLine
    {
        public int ItemId;
        public string Name;
        public int Quantity, FreeQty;
        public decimal UnitPrice, Subtotal;
    }

    public class AmenityRequestResult
    {
        public bool Ok;
        public long RequestId;
        public string RequestNumber;
        public decimal TotalAmount;
        public string Error;
        public List<AmenityLine> Lines = new List<AmenityLine>();
    }
}
