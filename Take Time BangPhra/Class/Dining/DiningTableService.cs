using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>
    /// โซน / โต๊ะ / QR token / session ของโต๊ะ / กระดานสถานะโต๊ะ
    ///   • QR_Token ของโต๊ะ (พิมพ์ติดโต๊ะ) ≠ Session_Token ของแขก (cookie) — ปิดโต๊ะทำให้ลิงก์มือถือเดิมใช้ต่อไม่ได้ แต่ QR ยังใช้กับลูกค้าถัดไป;
    ///     "หมุน QR" ใช้เมื่อ QR หลุดออกนอกร้าน (ต้องพิมพ์ใหม่)
    ///   • โซนเปิด/ปิด: Is_Active + Manual_Mode (OPEN/CLOSED/AUTO ตาม Open_Time..Close_Time ข้ามเที่ยงคืนได้) — ตรรกะเดียวกับ GuestPortalService.IsRoomServiceOpen
    ///   • 1 โต๊ะมี session เปิดได้ 1 ตัว: สแกนซ้ำ/เครื่องที่สองที่โต๊ะเดียวกัน = ต่อ session เดิม (บิลเดียวกัน)
    /// ทุกเมธอดตอบ NOT_READY เมื่อยังไม่รัน PHASE19_42 (ไม่ throw ยกเว้น CRUD ของแอดมินที่โยน ArgumentException เมื่อข้อมูลผิด)
    /// </summary>
    public class DiningTableService
    {
        private readonly string _conn;
        public DiningTableService() : this(DiningDb.ConnStr) { }
        public DiningTableService(string connectionString) { _conn = connectionString; }

        private const string SessionSelect = @"
            SELECT s.ID, s.Table_ID, s.Session_Token, s.Opened_At, s.Closed_At, s.Closed_By, s.Close_Reason, s.Guest_Name, s.Guest_Phone,
                   s.Reservation_ID, s.Pax, s.Status, s.Device_Hash, s.Last_Seen_At, s.Bill_Requested_At, s.Last_Call_At, s.Room_Charge_Locked_Until,
                   t.Code AS T_Code, t.Name_TH AS T_Name, t.Zone_ID, z.Code AS Z_Code, z.Name_TH AS Z_Name,
                   z.Allow_Cash, z.Allow_Transfer, z.Allow_Room_Charge, z.Kitchen_Station
              FROM dbo.Dining_Table_Session s
              JOIN dbo.Dining_Table t ON t.ID = s.Table_ID
              JOIN dbo.Dining_Zone z ON z.ID = t.Zone_ID ";

        /// <summary>ตารางของ PHASE19_42 พร้อมใช้ (เช็กลิสต์หน้าตั้งค่า Admin/Dining/Zones) — cache 60 วิ</summary>
        public bool SchemaReady() { return DiningDb.Ready(_conn); }

        /// <summary>ล้าง cache ความพร้อมของตาราง (หลังรัน migration แล้วกด "ตรวจอีกครั้ง")</summary>
        public static void InvalidateSchemaCache() { DiningDb.InvalidateReady(); }

        // ═════════════════════════════════════════════════════════════════════
        // โซน
        // ═════════════════════════════════════════════════════════════════════

        public List<DiningZone> ListZones(bool includeInactive)
        {
            var list = new List<DiningZone>();
            if (!DiningDb.Ready(_conn)) return list;
            var dt = DiningDb.Query(_conn, @"SELECT z.*, (SELECT COUNT(*) FROM dbo.Dining_Table t WHERE t.Zone_ID = z.ID AND t.Is_Active = 1) AS Table_Count
                                             FROM dbo.Dining_Zone z " + (includeInactive ? "" : "WHERE z.Is_Active = 1 ") + "ORDER BY z.Sort_Order, z.Name_TH");
            foreach (DataRow r in dt.Rows) list.Add(ReadZone(r));
            return list;
        }

        public DiningZone GetZone(int id)
        {
            if (!DiningDb.Ready(_conn)) return null;
            var dt = DiningDb.Query(_conn, "SELECT z.*, 0 AS Table_Count FROM dbo.Dining_Zone z WHERE z.ID = @id", new Dictionary<string, object> { { "@id", id } });
            return dt.Rows.Count == 0 ? null : ReadZone(dt.Rows[0]);
        }

        public DiningZone GetZoneByCode(string code)
        {
            if (!DiningDb.Ready(_conn) || string.IsNullOrWhiteSpace(code)) return null;
            var dt = DiningDb.Query(_conn, "SELECT z.*, 0 AS Table_Count FROM dbo.Dining_Zone z WHERE z.Code = @c", new Dictionary<string, object> { { "@c", code.Trim() } });
            return dt.Rows.Count == 0 ? null : ReadZone(dt.Rows[0]);
        }

        /// <summary>เพิ่ม (Id=0) / แก้โซน — คืน Id · Code ต้องเป็น [A-Za-z0-9_-] 1-20 ตัว ไม่ซ้ำ</summary>
        public int SaveZone(DiningZone z, string updatedBy = null)
        {
            if (z == null) throw new ArgumentException("ไม่มีข้อมูลโซน");
            z.Code = (z.Code ?? "").Trim().ToUpperInvariant();
            if (z.Code.Length == 0 || z.Code.Length > 20) throw new ArgumentException("รหัสโซนต้องมี 1-20 ตัวอักษร");
            foreach (char c in z.Code)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-')) throw new ArgumentException("รหัสโซนใช้ได้เฉพาะ A-Z 0-9 _ -");
            if (string.IsNullOrWhiteSpace(z.NameTh)) throw new ArgumentException("กรุณาระบุชื่อโซน");
            string mode = (z.ManualMode ?? "AUTO").Trim().ToUpperInvariant();
            if (mode != DiningStatus.ZoneMode.Auto && mode != DiningStatus.ZoneMode.Open && mode != DiningStatus.ZoneMode.Closed) mode = DiningStatus.ZoneMode.Auto;
            if (!z.AllowCash && !z.AllowTransfer && !z.AllowRoomCharge) throw new ArgumentException("ต้องเปิดวิธีชำระอย่างน้อย 1 แบบ");

            object dup = DiningDb.Scalar(_conn, "SELECT ID FROM dbo.Dining_Zone WHERE Code = @c AND ID <> @id",
                new Dictionary<string, object> { { "@c", z.Code }, { "@id", z.Id } });
            if (dup != null) throw new ArgumentException("รหัสโซน " + z.Code + " ซ้ำกับโซนอื่น");

            var p = new Dictionary<string, object>
            {
                { "@c", z.Code }, { "@n", DiningDb.DbStr(z.NameTh, 100) }, { "@s", z.SortOrder }, { "@a", z.IsActive },
                { "@ot", DiningDb.Db(z.OpenTime) }, { "@ct", DiningDb.Db(z.CloseTime) }, { "@m", mode }, { "@cm", DiningDb.DbStr(z.ClosedMessage, 300) },
                { "@cash", z.AllowCash }, { "@tr", z.AllowTransfer }, { "@rc", z.AllowRoomCharge }, { "@ks", DiningDb.DbStr(z.KitchenStation, 50) },
                { "@by", DiningDb.DbStr(updatedBy ?? DiningDb.StaffName(), 100) }
            };
            if (z.Id > 0)
            {
                p["@id"] = z.Id;
                DiningDb.Exec(_conn, @"UPDATE dbo.Dining_Zone SET Code=@c, Name_TH=@n, Sort_Order=@s, Is_Active=@a, Open_Time=@ot, Close_Time=@ct, Manual_Mode=@m,
                    Closed_Message=@cm, Allow_Cash=@cash, Allow_Transfer=@tr, Allow_Room_Charge=@rc, Kitchen_Station=@ks, Updated_At=GETDATE(), Updated_By=@by WHERE ID=@id", p);
            }
            else
            {
                z.Id = (int)DiningDb.InsertIdentity(_conn, @"INSERT INTO dbo.Dining_Zone (Code, Name_TH, Sort_Order, Is_Active, Open_Time, Close_Time, Manual_Mode, Closed_Message,
                    Allow_Cash, Allow_Transfer, Allow_Room_Charge, Kitchen_Station, Updated_At, Updated_By)
                    VALUES (@c, @n, @s, @a, @ot, @ct, @m, @cm, @cash, @tr, @rc, @ks, GETDATE(), @by)", p);
            }
            DiningDb.Log(_conn, "บันทึกโซน " + z.Code + " (" + z.NameTh + ")");
            DiningMenuService.Invalidate();
            return z.Id;
        }

        public void SetZoneActive(int zoneId, bool active)
        {
            DiningDb.Exec(_conn, "UPDATE dbo.Dining_Zone SET Is_Active=@a, Updated_At=GETDATE() WHERE ID=@id", new Dictionary<string, object> { { "@id", zoneId }, { "@a", active } });
        }

        /// <summary>โซนเปิดรับออเดอร์ตอนนี้ไหม (pure — ใช้ค่าในโซน) — ปิด = message บอกเหตุผล/เวลาทำการ</summary>
        public static bool IsZoneOpen(DiningZone z, DateTime now, out string message)
        {
            message = null;
            if (z == null) { message = "ไม่พบโซน"; return false; }
            string closedMsg = string.IsNullOrWhiteSpace(z.ClosedMessage) ? "ขณะนี้ปิดรับออเดอร์ กรุณาสั่งในเวลาทำการ" : z.ClosedMessage.Trim();
            if (!z.IsActive) { message = closedMsg; return false; }
            string mode = (z.ManualMode ?? "AUTO").Trim().ToUpperInvariant();
            if (mode == DiningStatus.ZoneMode.Open) return true;
            if (mode == DiningStatus.ZoneMode.Closed) { message = closedMsg; return false; }
            if (!z.OpenTime.HasValue || !z.CloseTime.HasValue) return true;   // AUTO ไม่ตั้งเวลา = เปิดตลอด
            TimeSpan t = now.TimeOfDay, open = z.OpenTime.Value, close = z.CloseTime.Value;
            bool isOpen = open <= close ? (t >= open && t <= close) : (t >= open || t <= close);
            if (!isOpen)
            {
                message = closedMsg + " (เวลาทำการ " + open.ToString(@"hh\:mm") + " - " + close.ToString(@"hh\:mm") + ")";
                return false;
            }
            return true;
        }

        public static bool IsZoneOpen(DiningZone z, out string message) { return IsZoneOpen(z, DateTime.Now, out message); }

        // ═════════════════════════════════════════════════════════════════════
        // โต๊ะ
        // ═════════════════════════════════════════════════════════════════════

        private const string TableSelect = @"SELECT t.*, z.Code AS Z_Code, z.Name_TH AS Z_Name FROM dbo.Dining_Table t JOIN dbo.Dining_Zone z ON z.ID = t.Zone_ID ";

        public List<DiningTable> ListTables(int? zoneId, bool includeInactive)
        {
            var list = new List<DiningTable>();
            if (!DiningDb.Ready(_conn)) return list;
            var p = new Dictionary<string, object>();
            string where = includeInactive ? "WHERE 1=1 " : "WHERE t.Is_Active = 1 ";
            if (zoneId.HasValue && zoneId.Value > 0) { where += "AND t.Zone_ID = @z "; p["@z"] = zoneId.Value; }
            var dt = DiningDb.Query(_conn, TableSelect + where + "ORDER BY z.Sort_Order, z.Name_TH, t.Sort_Order, t.Code", p);
            foreach (DataRow r in dt.Rows) list.Add(ReadTable(r));
            return list;
        }

        public DiningTable GetTable(int id)
        {
            if (!DiningDb.Ready(_conn)) return null;
            var dt = DiningDb.Query(_conn, TableSelect + "WHERE t.ID = @id", new Dictionary<string, object> { { "@id", id } });
            return dt.Rows.Count == 0 ? null : ReadTable(dt.Rows[0]);
        }

        /// <summary>เพิ่ม (Id=0 — ออก QR_Token ให้) / แก้โต๊ะ (ไม่แตะ token) — คืน Id</summary>
        public int SaveTable(DiningTable t)
        {
            if (t == null) throw new ArgumentException("ไม่มีข้อมูลโต๊ะ");
            if (t.ZoneId <= 0) throw new ArgumentException("กรุณาเลือกโซน");
            t.Code = (t.Code ?? "").Trim();
            if (t.Code.Length == 0 || t.Code.Length > 20) throw new ArgumentException("รหัสโต๊ะต้องมี 1-20 ตัวอักษร");
            if (t.Seats <= 0 || t.Seats > 255) t.Seats = 4;
            object dup = DiningDb.Scalar(_conn, "SELECT ID FROM dbo.Dining_Table WHERE Zone_ID = @z AND Code = @c AND ID <> @id",
                new Dictionary<string, object> { { "@z", t.ZoneId }, { "@c", t.Code }, { "@id", t.Id } });
            if (dup != null) throw new ArgumentException("รหัสโต๊ะ " + t.Code + " ซ้ำในโซนนี้");

            var p = new Dictionary<string, object>
            {
                { "@z", t.ZoneId }, { "@c", t.Code }, { "@n", DiningDb.DbStr(t.NameTh, 100) }, { "@seats", (byte)t.Seats }, { "@s", t.SortOrder },
                { "@a", t.IsActive }, { "@note", DiningDb.DbStr(t.Note, 300) }
            };
            if (t.Id > 0)
            {
                p["@id"] = t.Id;
                DiningDb.Exec(_conn, @"UPDATE dbo.Dining_Table SET Zone_ID=@z, Code=@c, Name_TH=@n, Seats=@seats, Sort_Order=@s, Is_Active=@a, Note=@note,
                    Status = CASE WHEN @a = 0 THEN 'DISABLED' WHEN Status = 'DISABLED' THEN 'FREE' ELSE Status END, Updated_At=GETDATE() WHERE ID=@id", p);
            }
            else
            {
                p["@tok"] = DiningTokens.NewToken();
                t.Id = (int)DiningDb.InsertIdentity(_conn, @"INSERT INTO dbo.Dining_Table (Zone_ID, Code, Name_TH, Seats, Sort_Order, QR_Token, QR_Issued_At, QR_Version, Status, Is_Active, Note)
                    VALUES (@z, @c, @n, @seats, @s, @tok, GETDATE(), 1, CASE WHEN @a = 1 THEN 'FREE' ELSE 'DISABLED' END, @a, @note)", p);
            }
            return t.Id;
        }

        /// <summary>สร้างหลายโต๊ะ: {prefix}{from}..{prefix}{to} (เช่น A1..A20) — ข้ามรหัสที่มีอยู่แล้ว; คืนจำนวนที่สร้าง</summary>
        public int BulkCreateTables(int zoneId, string prefix, int from, int to, int seats)
        {
            if (zoneId <= 0) throw new ArgumentException("กรุณาเลือกโซน");
            prefix = (prefix ?? "").Trim();
            if (from < 0 || to < from || to - from > 200) throw new ArgumentException("ช่วงเลขโต๊ะไม่ถูกต้อง (สูงสุด 200 โต๊ะต่อครั้ง)");
            int made = 0;
            for (int i = from; i <= to; i++)
            {
                string code = prefix + i.ToString(CultureInfo.InvariantCulture);
                if (code.Length > 20) continue;
                object exist = DiningDb.Scalar(_conn, "SELECT ID FROM dbo.Dining_Table WHERE Zone_ID=@z AND Code=@c", new Dictionary<string, object> { { "@z", zoneId }, { "@c", code } });
                if (exist != null) continue;
                SaveTable(new DiningTable { ZoneId = zoneId, Code = code, Seats = seats > 0 ? seats : 4, SortOrder = i });
                made++;
            }
            DiningDb.Log(_conn, "สร้างโต๊ะ " + prefix + from + ".." + prefix + to + " ในโซน #" + zoneId + " = " + made + " โต๊ะ");
            return made;
        }

        /// <summary>หมุน QR ของโต๊ะ (token ใหม่, QR_Version+1) — QR เดิมใช้ไม่ได้ทันที ต้องพิมพ์ใหม่; session ที่เปิดอยู่ไม่กระทบ</summary>
        public string RotateTableToken(int tableId)
        {
            string tok = DiningTokens.NewToken();
            int n = DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table SET QR_Token=@t, QR_Issued_At=GETDATE(), QR_Version=QR_Version+1, Updated_At=GETDATE() WHERE ID=@id",
                new Dictionary<string, object> { { "@id", tableId }, { "@t", tok } });
            if (n <= 0) throw new ArgumentException("ไม่พบโต๊ะ");
            DiningDb.Event(_conn, null, null, tableId, DiningStatus.Event.QrRotated, null, "หมุน QR โต๊ะ #" + tableId);
            DiningDb.Log(_conn, "หมุน QR โต๊ะ #" + tableId);
            return tok;
        }

        /// <summary>หมุน QR ทุกโต๊ะในโซน — คืนจำนวน</summary>
        public int RotateZoneTokens(int zoneId)
        {
            int n = 0;
            foreach (var t in ListTables(zoneId, true)) { RotateTableToken(t.Id); n++; }
            return n;
        }

        public void SetTableActive(int tableId, bool active)
        {
            DiningDb.Exec(_conn, @"UPDATE dbo.Dining_Table SET Is_Active=@a, Status = CASE WHEN @a = 0 THEN 'DISABLED' WHEN Status = 'DISABLED' THEN 'FREE' ELSE Status END, Updated_At=GETDATE() WHERE ID=@id",
                new Dictionary<string, object> { { "@id", tableId }, { "@a", active } });
        }

        /// <summary>ลบโต๊ะ — ได้เฉพาะโต๊ะที่ไม่เคยมี session (มีประวัติ = ปิดใช้แทน)</summary>
        public DiningResult DeleteTable(int tableId)
        {
            object has = DiningDb.Scalar(_conn, "SELECT TOP 1 ID FROM dbo.Dining_Table_Session WHERE Table_ID=@id", new Dictionary<string, object> { { "@id", tableId } });
            if (has != null)
            {
                SetTableActive(tableId, false);
                return DiningResult.Fail(DiningError.Validation, "โต๊ะนี้มีประวัติการสั่ง — ปิดใช้งานให้แทนการลบ");
            }
            DiningDb.Exec(_conn, "DELETE FROM dbo.Dining_Table WHERE ID=@id", new Dictionary<string, object> { { "@id", tableId } });
            return DiningResult.Success("ลบโต๊ะแล้ว");
        }

        // ═════════════════════════════════════════════════════════════════════
        // สแกน QR → โต๊ะ → session
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// token จาก QR → โต๊ะ + โซน (ต้อง Is_Active ทั้งคู่) + โซนเปิดอยู่ไหม (ZoneOpen=false ยังคืน Table/Zone ให้หน้าแสดงเวลาทำการ)
        /// </summary>
        public TableResolveResult ResolveTable(string qrToken)
        {
            var r = new TableResolveResult();
            if (!DiningConfig.Enabled) { r.ErrorCode = DiningError.FeatureOff; r.Message = "ระบบสั่งอาหารผ่าน QR ปิดใช้งานอยู่"; return r; }
            if (!DiningDb.Ready(_conn)) { r.ErrorCode = DiningError.NotReady; r.Message = DiningDb.NotReady().Message; return r; }
            if (!DiningTokens.IsValidToken(qrToken)) { r.ErrorCode = DiningError.BadToken; r.Message = "QR ไม่ถูกต้อง กรุณาสแกน QR ที่โต๊ะอีกครั้ง"; return r; }

            var dt = DiningDb.Query(_conn, TableSelect + "WHERE t.QR_Token = @t", new Dictionary<string, object> { { "@t", qrToken } });
            if (dt.Rows.Count == 0) { r.ErrorCode = DiningError.BadToken; r.Message = "QR นี้ใช้ไม่ได้แล้ว (อาจถูกเปลี่ยนใหม่) กรุณาติดต่อพนักงาน"; return r; }
            var t = ReadTable(dt.Rows[0]);
            // constant-time ซ้ำอีกชั้น (DB เทียบแบบ collation แล้ว — กัน case-insensitive collation ให้ token ต่างตัวพิมพ์ผ่าน)
            if (!DiningTokens.ConstantTimeEquals(t.QrToken, qrToken)) { r.ErrorCode = DiningError.BadToken; r.Message = "QR ไม่ถูกต้อง"; return r; }
            r.Table = t;
            r.Zone = GetZone(t.ZoneId);
            if (!t.IsActive || r.Zone == null || !r.Zone.IsActive) { r.ErrorCode = DiningError.TableInactive; r.Message = "โต๊ะนี้ปิดใช้งานชั่วคราว กรุณาติดต่อพนักงาน"; return r; }
            string msg;
            r.ZoneOpen = IsZoneOpen(r.Zone, out msg);
            if (!r.ZoneOpen) { r.ErrorCode = DiningError.ZoneClosed; r.Message = msg; return r; }
            r.Ok = true;
            return r;
        }

        /// <summary>
        /// เปิด session ใหม่ให้โต๊ะ หรือต่อ session ที่เปิดอยู่ (OPEN/BILL_REQUESTED) — คืน Session_Token สำหรับ cookie ของแขก
        /// โต๊ะ → OCCUPIED + Current_Session_ID
        /// </summary>
        public SessionOpenResult OpenOrResumeSession(int tableId, string deviceHash, string clientIp = null)
        {
            var r = new SessionOpenResult();
            if (!DiningDb.Ready(_conn)) { r.ErrorCode = DiningError.NotReady; r.Message = DiningDb.NotReady().Message; return r; }
            var t = GetTable(tableId);
            if (t == null || !t.IsActive) { r.ErrorCode = DiningError.TableInactive; r.Message = "ไม่พบโต๊ะ/โต๊ะปิดใช้งาน"; return r; }

            // ต่อ session เดิม
            var cur = DiningDb.Query(_conn, SessionSelect + "WHERE s.Table_ID = @t AND s.Status IN ('OPEN','BILL_REQUESTED') ORDER BY s.ID DESC",
                new Dictionary<string, object> { { "@t", tableId } });
            if (cur.Rows.Count > 0)
            {
                var s = ReadSession(cur.Rows[0]);
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table_Session SET Last_Seen_At = GETDATE() WHERE ID = @id", new Dictionary<string, object> { { "@id", s.Id } });
                if (!t.CurrentSessionId.HasValue || t.CurrentSessionId.Value != s.Id)
                    DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table SET Current_Session_ID=@s, Status = CASE WHEN Status IN ('FREE','OCCUPIED') THEN 'OCCUPIED' ELSE Status END WHERE ID=@t",
                        new Dictionary<string, object> { { "@t", tableId }, { "@s", s.Id } });
                r.Ok = true; r.Resumed = true; r.Session = s; r.SessionToken = s.SessionToken;
                return r;
            }

            string tok = DiningTokens.NewToken();
            long id = DiningDb.InsertIdentity(_conn, @"INSERT INTO dbo.Dining_Table_Session (Table_ID, Session_Token, Status, Device_Hash, Client_Ip, Opened_At, Last_Seen_At)
                VALUES (@t, @tok, 'OPEN', @dh, @ip, GETDATE(), GETDATE())",
                new Dictionary<string, object> { { "@t", tableId }, { "@tok", tok }, { "@dh", DiningDb.DbStr(deviceHash, 64) }, { "@ip", DiningDb.DbStr(clientIp, 45) } });
            if (id <= 0) { r.ErrorCode = DiningError.Internal; r.Message = "เปิดโต๊ะไม่สำเร็จ"; return r; }
            DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table SET Current_Session_ID=@s, Status='OCCUPIED', Updated_At=GETDATE() WHERE ID=@t",
                new Dictionary<string, object> { { "@t", tableId }, { "@s", id } });
            DiningDb.Event(_conn, null, id, tableId, DiningStatus.Event.SessionOpen, "GUEST", "เปิดโต๊ะ " + t.DisplayName);
            r.Ok = true; r.Session = GetSessionById(id); r.SessionToken = tok;
            return r;
        }

        /// <summary>session จาก token ของแขก — เฉพาะ OPEN/BILL_REQUESTED (ปิดแล้ว = null)</summary>
        public DiningTableSession GetSession(string sessionToken)
        {
            if (!DiningDb.Ready(_conn) || !DiningTokens.IsValidToken(sessionToken)) return null;
            var dt = DiningDb.Query(_conn, SessionSelect + "WHERE s.Session_Token = @t AND s.Status IN ('OPEN','BILL_REQUESTED')",
                new Dictionary<string, object> { { "@t", sessionToken } });
            if (dt.Rows.Count == 0) return null;
            var s = ReadSession(dt.Rows[0]);
            return DiningTokens.ConstantTimeEquals(s.SessionToken, sessionToken) ? s : null;
        }

        /// <summary>session จาก id (ทุกสถานะ) — ฝั่งพนักงาน</summary>
        public DiningTableSession GetSessionById(long id)
        {
            if (!DiningDb.Ready(_conn)) return null;
            var dt = DiningDb.Query(_conn, SessionSelect + "WHERE s.ID = @id", new Dictionary<string, object> { { "@id", id } });
            return dt.Rows.Count == 0 ? null : ReadSession(dt.Rows[0]);
        }

        /// <summary>บันทึกว่าแขกยังใช้งานอยู่ (เรียกจาก poll — กันปิดโต๊ะอัตโนมัติ)</summary>
        public void TouchSession(long sessionId)
        {
            try { DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table_Session SET Last_Seen_At = GETDATE() WHERE ID = @id AND Status <> 'CLOSED'", new Dictionary<string, object> { { "@id", sessionId } }); }
            catch { }
        }

        /// <summary>แขกบันทึกชื่อ/เบอร์ (ไม่บังคับ) ลง session</summary>
        public void SetGuestInfo(long sessionId, string guestName, string guestPhone, int? pax)
        {
            DiningDb.Exec(_conn, @"UPDATE dbo.Dining_Table_Session SET Guest_Name = COALESCE(@n, Guest_Name), Guest_Phone = COALESCE(@p, Guest_Phone), Pax = COALESCE(@x, Pax) WHERE ID=@id",
                new Dictionary<string, object> { { "@id", sessionId }, { "@n", DiningDb.DbStr(guestName, 100) }, { "@p", DiningDb.DbStr(guestPhone, 30) }, { "@x", DiningDb.Db(pax.HasValue ? (object)(byte)Math.Min(255, Math.Max(0, pax.Value)) : null) } });
        }

        // ═════════════════════════════════════════════════════════════════════
        // เช็คบิล / ปิดโต๊ะ
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>ลูกค้ากด "เช็คบิล" → session/โต๊ะ BILL_REQUESTED + แจ้งเตือน (Telegram + กระดาน + แชท)</summary>
        public DiningResult RequestBill(string sessionToken)
        {
            var s = GetSession(sessionToken);
            if (s == null) return DiningResult.Fail(DiningError.SessionClosed, "โต๊ะนี้ปิดแล้ว กรุณาสแกน QR ใหม่");
            var outs = DiningPaymentService.LoadOutstanding(_conn, s.Id);
            bool already = s.Status == DiningStatus.Session.BillRequested && s.BillRequestedAt.HasValue && (DateTime.Now - s.BillRequestedAt.Value).TotalMinutes < 2;
            DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table_Session SET Status='BILL_REQUESTED', Bill_Requested_At=GETDATE(), Last_Seen_At=GETDATE() WHERE ID=@id AND Status <> 'CLOSED'",
                new Dictionary<string, object> { { "@id", s.Id } });
            DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table SET Status='BILL_REQUESTED', Updated_At=GETDATE() WHERE ID=@t AND Status <> 'DISABLED'", new Dictionary<string, object> { { "@t", s.TableId } });
            if (!already)
            {
                DiningDb.Event(_conn, null, s.Id, s.TableId, DiningStatus.Event.BillRequest, "GUEST", "ขอเช็คบิล ยอดค้าง " + outs.Amount.ToString("N2", CultureInfo.InvariantCulture));
                s.Status = DiningStatus.Session.BillRequested;
                try { new DiningNotifier(_conn).BillRequested(s, outs.Amount, outs.OrderIds.Count); } catch { }
            }
            return DiningResult.Success(outs.Amount > 0m
                ? "แจ้งพนักงานแล้ว — ยอดค้าง " + outs.Amount.ToString("N2", CultureInfo.InvariantCulture) + " บาท เลือกวิธีชำระได้ด้านล่าง"
                : "แจ้งพนักงานแล้ว — ไม่มียอดค้างชำระ");
        }

        /// <summary>
        /// ปิดโต๊ะโดยพนักงาน: ทุกออเดอร์ต้อง PAID/CHARGED/CANCELLED (หรือไม่มีออเดอร์) — ยังค้าง = ต้อง force (Owner/Admin + เหตุผล ≥ 5 ตัว)
        /// ออเดอร์ที่ยังไม่เสิร์ฟ (NEW/ACCEPTED/PREPARING) ของ session ที่ปิดด้วย force จะถูกยกเลิกพร้อมเหตุผล · session CLOSED · โต๊ะ FREE · conversation RESOLVED
        /// </summary>
        public DiningResult CloseSession(long sessionId, int staffId, string reason, bool force)
        {
            if (!DiningDb.Ready(_conn)) return DiningDb.NotReady();
            var s = GetSessionById(sessionId);
            if (s == null) return DiningResult.Fail(DiningError.NotFound, "ไม่พบ session ของโต๊ะ");
            if (!s.IsOpen) return DiningResult.Fail(DiningError.SessionClosed, "โต๊ะนี้ปิดไปแล้ว");
            var outs = DiningPaymentService.LoadOutstanding(_conn, s.Id);
            string staffName = DiningDb.StaffName();
            if (outs.Amount > 0m)
            {
                if (!force) return DiningResult.Fail(DiningError.Outstanding, "ยังค้างชำระ " + outs.Amount.ToString("N2", CultureInfo.InvariantCulture) + " บาท — รับเงินก่อน หรือ Owner/Admin ปิดแบบบังคับพร้อมเหตุผล");
                if (!Take_Time_BangPhra.Payments.SlipCheck.SlipVerificationService.CanOverrideCurrentUser())
                    return DiningResult.Fail(DiningError.Forbidden, "ปิดโต๊ะที่ยังค้างชำระได้เฉพาะ Owner/Admin");
                if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
                    return DiningResult.Fail(DiningError.Validation, "กรุณาระบุเหตุผล (อย่างน้อย 5 ตัวอักษร)");
            }
            return CloseCore(s, staffId, staffName, reason, outs.Amount, false);
        }

        private DiningResult CloseCore(DiningTableSession s, int staffId, string staffName, string reason, decimal outstanding, bool auto)
        {
            var p = new Dictionary<string, object> { { "@id", s.Id }, { "@by", staffId > 0 ? (object)staffId : DBNull.Value }, { "@r", DiningDb.DbStr(reason, 300) } };
            int n = DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table_Session SET Status='CLOSED', Closed_At=GETDATE(), Closed_By=@by, Close_Reason=@r WHERE ID=@id AND Status <> 'CLOSED'", p);
            if (n <= 0) return DiningResult.Fail(DiningError.SessionClosed, "โต๊ะนี้ปิดไปแล้ว");
            // ออเดอร์ที่ยังไม่เสิร์ฟของ session นี้ → ยกเลิก (ปิดโต๊ะแล้วครัวไม่ต้องทำต่อ)
            DiningDb.Exec(_conn, @"UPDATE dbo.Dining_Order SET Order_Status='CANCELLED', Cancel_Reason=@r, Cancelled_By=@by, Cancelled_At=GETDATE(), Updated_At=GETDATE()
                                   WHERE Session_ID=@id AND Order_Status IN ('NEW','ACCEPTED','PREPARING') AND Payment_Status = 'UNPAID'",
                new Dictionary<string, object> { { "@id", s.Id }, { "@by", staffId > 0 ? (object)staffId : DBNull.Value }, { "@r", DiningDb.DbStr(auto ? "ปิดโต๊ะอัตโนมัติ" : ("ปิดโต๊ะ: " + (reason ?? "")), 300) } });
            DiningDb.Exec(_conn, "UPDATE dbo.Dining_Table SET Status = CASE WHEN Is_Active = 1 THEN 'FREE' ELSE 'DISABLED' END, Current_Session_ID = NULL, Updated_At=GETDATE() WHERE ID=@t AND ISNULL(Current_Session_ID, 0) = @s",
                new Dictionary<string, object> { { "@t", s.TableId }, { "@s", s.Id } });
            DiningDb.Event(_conn, null, s.Id, s.TableId, auto ? DiningStatus.Event.AutoClose : DiningStatus.Event.SessionClose, auto ? "SYSTEM" : staffName,
                (auto ? "ปิดอัตโนมัติ" : "ปิดโต๊ะ") + (string.IsNullOrWhiteSpace(reason) ? "" : ": " + reason) + (outstanding > 0m ? " (ค้าง " + outstanding.ToString("N2", CultureInfo.InvariantCulture) + ")" : ""));
            DiningDb.Log(_conn, (auto ? "ปิดโต๊ะอัตโนมัติ " : "ปิดโต๊ะ ") + s.Label + (outstanding > 0m ? " ค้าง " + outstanding.ToString("N2", CultureInfo.InvariantCulture) + " เหตุผล: " + reason : ""), auto ? "SYSTEM" : null);
            s.Status = DiningStatus.Session.Closed;
            try { new DiningNotifier(_conn).TableClosed(s, staffName, reason, outstanding, auto); } catch { }
            return DiningResult.Success("ปิดโต๊ะ " + s.Label + " แล้ว");
        }

        /// <summary>
        /// งานเบื้องหลัง (timer): ปิด session ที่จ่ายครบ/ไม่มีออเดอร์ค้าง และไม่มีกิจกรรม (Last_Seen_At + ออเดอร์ล่าสุด) นานกว่า Dining_Session_Idle_Minutes
        /// — คืนจำนวนที่ปิด; ไม่ throw
        /// </summary>
        public int AutoCloseIdleSessions()
        {
            int closed = 0;
            try
            {
                if (!DiningDb.Ready(_conn)) return 0;
                int idle = DiningConfig.IdleMinutes;
                var dt = DiningDb.Query(_conn, @"
                    SELECT TOP 100 s.ID FROM dbo.Dining_Table_Session s
                     WHERE s.Status IN ('OPEN','BILL_REQUESTED')
                       AND s.Last_Seen_At < DATEADD(MINUTE, -@m, GETDATE())
                       AND s.Opened_At < DATEADD(MINUTE, -@m, GETDATE())
                       AND NOT EXISTS (SELECT 1 FROM dbo.Dining_Order o WHERE o.Session_ID = s.ID AND o.Order_Status <> 'CANCELLED' AND o.Payment_Status = 'UNPAID')
                       AND NOT EXISTS (SELECT 1 FROM dbo.Dining_Order o WHERE o.Session_ID = s.ID AND o.Created_At > DATEADD(MINUTE, -@m, GETDATE()))
                       AND NOT EXISTS (SELECT 1 FROM dbo.Dining_Payment p WHERE p.Session_ID = s.ID AND p.Status = 'PENDING_SLIP')",
                    new Dictionary<string, object> { { "@m", idle } });
                foreach (DataRow r in dt.Rows)
                {
                    var s = GetSessionById(DiningDb.Long(r["ID"]));
                    if (s == null || !s.IsOpen) continue;
                    var res = CloseCore(s, 0, "SYSTEM", "ไม่มีกิจกรรม " + idle + " นาที", 0m, true);
                    if (res.Ok) closed++;
                }
            }
            catch (Exception ex) { DiningDb.Log(_conn, "AutoCloseIdleSessions ล้มเหลว: " + ex.Message, "SYSTEM"); }
            return closed;
        }

        // ═════════════════════════════════════════════════════════════════════
        // กระดานสถานะโต๊ะ
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>โต๊ะทุกตัว (ใช้งาน) พร้อม session เปิด / ยอดค้าง / จำนวนออเดอร์ที่ยังไม่เสิร์ฟ / อายุออเดอร์ล่าสุด — เรียงตามโซน</summary>
        public List<TableBoardRow> GetBoard(int? zoneId)
        {
            var list = new List<TableBoardRow>();
            if (!DiningDb.Ready(_conn)) return list;
            var p = new Dictionary<string, object>();
            string where = "WHERE t.Is_Active = 1 ";
            if (zoneId.HasValue && zoneId.Value > 0) { where += "AND t.Zone_ID = @z "; p["@z"] = zoneId.Value; }
            var dt = DiningDb.Query(_conn, @"
                SELECT t.*, z.Code AS Z_Code, z.Name_TH AS Z_Name,
                       s.ID AS S_ID, s.Status AS S_Status, s.Opened_At AS S_Opened, s.Last_Seen_At AS S_Seen, s.Bill_Requested_At AS S_Bill,
                       (SELECT COUNT(*) FROM dbo.Dining_Order o WHERE o.Session_ID = s.ID AND o.Order_Status IN ('NEW','ACCEPTED','PREPARING')) AS Open_Orders,
                       (SELECT COUNT(*) FROM dbo.Dining_Order o WHERE o.Session_ID = s.ID AND o.Order_Status <> 'CANCELLED') AS Total_Orders,
                       ISNULL((SELECT SUM(o.Total_Amount) FROM dbo.Dining_Order o WHERE o.Session_ID = s.ID AND o.Order_Status <> 'CANCELLED' AND o.Payment_Status = 'UNPAID'), 0) AS Outstanding,
                       ISNULL((SELECT SUM(o.Total_Amount) FROM dbo.Dining_Order o WHERE o.Session_ID = s.ID AND o.Order_Status <> 'CANCELLED' AND o.Payment_Status = 'UNPAID' AND o.Pending_Payment_ID IS NOT NULL), 0) AS Pending_Slip,
                       (SELECT MAX(o.Created_At) FROM dbo.Dining_Order o WHERE o.Session_ID = s.ID AND o.Order_Status <> 'CANCELLED') AS Latest_Order
                  FROM dbo.Dining_Table t
                  JOIN dbo.Dining_Zone z ON z.ID = t.Zone_ID
                  LEFT JOIN dbo.Dining_Table_Session s ON s.ID = t.Current_Session_ID AND s.Status <> 'CLOSED'
                " + where + "ORDER BY z.Sort_Order, z.Name_TH, t.Sort_Order, t.Code", p);
            DateTime now = DateTime.Now;
            foreach (DataRow r in dt.Rows)
            {
                var row = new TableBoardRow { Table = ReadTable(r) };
                row.SessionId = DiningDb.NLong(r["S_ID"]);
                row.SessionStatus = DiningDb.Str(r["S_Status"]);
                row.SessionOpenedAt = DiningDb.NDate(r["S_Opened"]);
                row.LastSeenAt = DiningDb.NDate(r["S_Seen"]);
                row.BillRequested = row.SessionStatus == DiningStatus.Session.BillRequested;
                row.OpenOrders = DiningDb.Int(r["Open_Orders"]);
                row.TotalOrders = DiningDb.Int(r["Total_Orders"]);
                row.Outstanding = DiningDb.Dec(r["Outstanding"]);
                row.PendingSlipAmount = DiningDb.Dec(r["Pending_Slip"]);
                row.LatestOrderAt = DiningDb.NDate(r["Latest_Order"]);
                if (row.LatestOrderAt.HasValue) row.LatestOrderAgeMinutes = Math.Max(0, (now - row.LatestOrderAt.Value).TotalMinutes);
                if (!row.SessionId.HasValue && row.Table.Status != DiningStatus.Table.Disabled) row.Table.Status = DiningStatus.Table.Free;
                list.Add(row);
            }
            return list;
        }

        // ═════════════════════════════════════════════════════════════════════
        // readers
        // ═════════════════════════════════════════════════════════════════════

        internal static DiningZone ReadZone(DataRow r)
        {
            return new DiningZone
            {
                Id = DiningDb.Int(r["ID"]), Code = DiningDb.Str(r["Code"]), NameTh = DiningDb.Str(r["Name_TH"]), SortOrder = DiningDb.Int(r["Sort_Order"]),
                IsActive = DiningDb.Bool(r["Is_Active"]), OpenTime = DiningDb.NTime(r["Open_Time"]), CloseTime = DiningDb.NTime(r["Close_Time"]),
                ManualMode = (DiningDb.Str(r["Manual_Mode"]) ?? "AUTO").Trim().ToUpperInvariant(), ClosedMessage = DiningDb.Str(r["Closed_Message"]),
                AllowCash = DiningDb.Bool(r["Allow_Cash"]), AllowTransfer = DiningDb.Bool(r["Allow_Transfer"]), AllowRoomCharge = DiningDb.Bool(r["Allow_Room_Charge"]),
                KitchenStation = DiningDb.Str(r["Kitchen_Station"]), UpdatedAt = DiningDb.NDate(r["Updated_At"]), UpdatedBy = DiningDb.Str(r["Updated_By"]),
                TableCount = r.Table.Columns.Contains("Table_Count") ? DiningDb.Int(r["Table_Count"]) : 0
            };
        }

        internal static DiningTable ReadTable(DataRow r)
        {
            return new DiningTable
            {
                Id = DiningDb.Int(r["ID"]), ZoneId = DiningDb.Int(r["Zone_ID"]), ZoneCode = DiningDb.Str(r["Z_Code"]), ZoneName = DiningDb.Str(r["Z_Name"]),
                Code = DiningDb.Str(r["Code"]), NameTh = DiningDb.Str(r["Name_TH"]), Seats = DiningDb.Int(r["Seats"]), SortOrder = DiningDb.Int(r["Sort_Order"]),
                QrToken = DiningDb.Str(r["QR_Token"]), QrIssuedAt = DiningDb.Date(r["QR_Issued_At"]), QrVersion = DiningDb.Int(r["QR_Version"]),
                Status = DiningDb.Str(r["Status"]) ?? DiningStatus.Table.Free, CurrentSessionId = DiningDb.NLong(r["Current_Session_ID"]),
                IsActive = DiningDb.Bool(r["Is_Active"]), Note = DiningDb.Str(r["Note"])
            };
        }

        internal static DiningTableSession ReadSession(DataRow r)
        {
            return new DiningTableSession
            {
                Id = DiningDb.Long(r["ID"]), TableId = DiningDb.Int(r["Table_ID"]), TableCode = DiningDb.Str(r["T_Code"]), TableName = DiningDb.Str(r["T_Name"]),
                ZoneId = DiningDb.Int(r["Zone_ID"]), ZoneCode = DiningDb.Str(r["Z_Code"]), ZoneName = DiningDb.Str(r["Z_Name"]),
                SessionToken = DiningDb.Str(r["Session_Token"]), OpenedAt = DiningDb.Date(r["Opened_At"]), ClosedAt = DiningDb.NDate(r["Closed_At"]),
                ClosedBy = DiningDb.NInt(r["Closed_By"]), CloseReason = DiningDb.Str(r["Close_Reason"]), GuestName = DiningDb.Str(r["Guest_Name"]),
                GuestPhone = DiningDb.Str(r["Guest_Phone"]), ReservationId = DiningDb.NInt(r["Reservation_ID"]), Pax = DiningDb.NInt(r["Pax"]),
                Status = DiningDb.Str(r["Status"]) ?? DiningStatus.Session.Open, DeviceHash = DiningDb.Str(r["Device_Hash"]), LastSeenAt = DiningDb.Date(r["Last_Seen_At"]),
                BillRequestedAt = DiningDb.NDate(r["Bill_Requested_At"]), LastCallAt = DiningDb.NDate(r["Last_Call_At"]), RoomChargeLockedUntil = DiningDb.NDate(r["Room_Charge_Locked_Until"]),
                AllowCash = DiningDb.Bool(r["Allow_Cash"]), AllowTransfer = DiningDb.Bool(r["Allow_Transfer"]), AllowRoomCharge = DiningDb.Bool(r["Allow_Room_Charge"]),
                KitchenStation = DiningDb.Str(r["Kitchen_Station"])
            };
        }
    }
}
