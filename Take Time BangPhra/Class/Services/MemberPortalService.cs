using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// Member Portal — สมาชิกล็อกอินดูบัตร/สิทธิ์/voucher ของตัวเอง (PHASE18_25)
    ///
    /// ต่อยอด Loyalty เดิม (Loyalty_Tiers / Customer_Loyalty) ไม่แตะแต้ม/การคำนวณ tier:
    ///   • ล็อกอิน: เบอร์โทร + PIN 6 หลัก — ครั้งแรกยังไม่มี PIN ใช้ "เลขท้ายเบอร์ 4 ตัว"
    ///     แล้วระบบบังคับตั้ง PIN ใหม่ทันที (ล็อก 10 นาทีเมื่อผิดครบ 5 ครั้ง)
    ///   • ส่วนลดค่าห้องตามวันเข้าพักต่อ tier: WEEKDAY / WEEKEND
    ///     (เสาร์-อาทิตย์ หรือวันที่อยู่ในตาราง Accommodation_HolidayPrice = วันหยุด/เทศกาล)
    ///   • Voucher: แจกจาก template → สมาชิกกดใช้ (ACTIVATED + โค้ดมีอายุตาม Redeem_Window)
    ///     → พนักงานแลกด้วยโค้ด (REDEEMED) — สถานะครบสำหรับ tracking
    /// </summary>
    public class MemberPortalService
    {
        private readonly string _conn;
        private readonly code _code = new code();
        private static readonly Random _rng = new Random();

        public MemberPortalService(string connectionString) { _conn = connectionString; }

        // ═══════════════════════ ล็อกอินสมาชิก ═══════════════════════

        public class MemberLoginResult
        {
            public bool Success;
            public bool MustSetPin;      // ล็อกอินด้วย PIN เริ่มต้น → บังคับตั้งใหม่
            public string Error;
            public string Phone;
        }

        /// <summary>ล็อกอินด้วยเบอร์ + PIN — ยังไม่เคยตั้ง PIN ให้ใช้เลขท้ายเบอร์ 4 ตัวแล้วบังคับตั้งใหม่</summary>
        public MemberLoginResult Login(string phone, string pin)
        {
            phone = SanitizePhone(phone);
            pin = (pin ?? "").Trim();
            if (phone.Length < 9 || pin.Length < 4)
                return new MemberLoginResult { Error = "กรุณากรอกเบอร์โทรและรหัส PIN ให้ถูกต้อง" };

            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT cl.Customer_MobilePhone, cl.Member_PIN_Hash, cl.Pin_Fail_Count, cl.Pin_Locked_Until
                    FROM Customer_Loyalty cl WHERE cl.Customer_MobilePhone = @p",
                P("@p", phone));
            if (dt == null || dt.Rows.Count == 0)
                return new MemberLoginResult { Error = "ไม่พบสมาชิกของเบอร์นี้ — สมัครสมาชิกได้ที่เคาน์เตอร์ค่ะ" };

            DataRow r = dt.Rows[0];
            if (r["Pin_Locked_Until"] != DBNull.Value && Convert.ToDateTime(r["Pin_Locked_Until"]) > DateTime.Now)
                return new MemberLoginResult { Error = "ใส่รหัสผิดหลายครั้ง กรุณาลองใหม่ภายหลัง หรือติดต่อเคาน์เตอร์" };

            string hash = r["Member_PIN_Hash"] == DBNull.Value ? null : r["Member_PIN_Hash"].ToString();
            bool ok, firstTime = string.IsNullOrEmpty(hash);
            if (firstTime)
            {
                // ยังไม่ตั้ง PIN → ยอมรับเลขท้ายเบอร์ 4 ตัว (แล้วบังคับตั้งใหม่ทันทีในหน้า)
                ok = phone.Length >= 4 && pin == phone.Substring(phone.Length - 4);
            }
            else
            {
                ok = SecurityHelper.VerifyPassword(pin, hash);
            }

            if (!ok)
            {
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Customer_Loyalty
                         SET Pin_Fail_Count = ISNULL(Pin_Fail_Count, 0) + 1,
                             Pin_Locked_Until = CASE WHEN ISNULL(Pin_Fail_Count, 0) + 1 >= 5
                                                     THEN DATEADD(MINUTE, 10, GETDATE()) ELSE Pin_Locked_Until END
                       WHERE Customer_MobilePhone = @p", P("@p", phone));
                return new MemberLoginResult { Error = "เบอร์โทรหรือรหัส PIN ไม่ถูกต้อง" };
            }

            _code.DatabaseInsertSafe(_conn,
                "UPDATE Customer_Loyalty SET Pin_Fail_Count = 0, Pin_Locked_Until = NULL WHERE Customer_MobilePhone = @p",
                P("@p", phone));
            return new MemberLoginResult { Success = true, MustSetPin = firstTime, Phone = phone };
        }

        /// <summary>ตั้ง/เปลี่ยน PIN (6 หลักแนะนำ อย่างน้อย 4)</summary>
        public (bool ok, string msg) SetPin(string phone, string newPin)
        {
            newPin = (newPin ?? "").Trim();
            if (newPin.Length < 4 || newPin.Length > 8 || !long.TryParse(newPin, out _))
                return (false, "PIN ต้องเป็นตัวเลข 4-8 หลัก");
            phone = SanitizePhone(phone);
            if (phone.Length >= 4 && newPin == phone.Substring(phone.Length - 4))
                return (false, "PIN ใหม่ต้องไม่ใช่เลขท้ายเบอร์โทร");
            int n = _code.DatabaseInsertSafe(_conn,
                "UPDATE Customer_Loyalty SET Member_PIN_Hash = @h WHERE Customer_MobilePhone = @p",
                P("@h", SecurityHelper.HashPassword(newPin), "@p", phone));
            return n > 0 ? (true, "ตั้งรหัส PIN เรียบร้อย") : (false, "ไม่พบสมาชิก");
        }

        /// <summary>พนักงานรีเซ็ต PIN (กลับไปใช้เลขท้ายเบอร์ 4 ตัวชั่วคราว)</summary>
        public void ResetPin(string phone)
        {
            _code.DatabaseInsertSafe(_conn,
                @"UPDATE Customer_Loyalty SET Member_PIN_Hash = NULL, Pin_Fail_Count = 0, Pin_Locked_Until = NULL
                   WHERE Customer_MobilePhone = @p", P("@p", SanitizePhone(phone)));
        }

        // ═══════════════════════ ข้อมูลบัตร/สิทธิ์ ═══════════════════════

        /// <summary>ข้อมูลบัตรสมาชิก: ชื่อ tier สี รูปบัตร แต้ม วันหมดอายุ ฯลฯ (null = ไม่พบ)</summary>
        public DataRow GetCard(string phone)
        {
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT cl.Customer_MobilePhone, cl.TotalPoints, cl.AvailablePoints, cl.MemberSince,
                         cl.Membership_Expiry, cl.CurrentTier_ID,
                         t.TierName, t.TierNameEN, t.TierColor, t.Card_Image_Path, t.DiscountPercent,
                         c.Name AS CustomerName
                    FROM Customer_Loyalty cl
                    JOIN Loyalty_Tiers t ON t.ID = cl.CurrentTier_ID
                    LEFT JOIN Customer c ON c.MobilePhone = cl.Customer_MobilePhone
                   WHERE cl.Customer_MobilePhone = @p", P("@p", SanitizePhone(phone)));
            return dt != null && dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public bool IsMembershipExpired(DataRow card) =>
            card != null && card["Membership_Expiry"] != DBNull.Value
            && Convert.ToDateTime(card["Membership_Expiry"]).Date < DateTime.Today;

        /// <summary>สิทธิ์ของ tier (จากตาราง Loyalty_Tier_Benefits เดิม)</summary>
        public DataTable GetTierBenefits(int tierId)
        {
            try
            {
                return _code.DatabaseQuerySafe(_conn,
                    @"SELECT BenefitName, Description FROM Loyalty_Tier_Benefits
                       WHERE Tier_ID = @t AND ISNULL(IsActive, 1) = 1 ORDER BY ID", P("@t", tierId));
            }
            catch { return null; }
        }

        // ── ส่วนลดค่าห้องตามวันเข้าพัก ──

        /// <summary>กติกาส่วนลดของทุก tier (สำหรับหน้าจัดการ + โชว์บนบัตร)</summary>
        public DataTable GetRoomDiscountRules()
        {
            try
            {
                return _code.DatabaseQuerySafe(_conn,
                    @"SELECT t.ID AS Tier_ID, t.TierName, t.TierColor,
                             ISNULL(w.Discount_Pct, 0) AS Weekday_Pct,
                             ISNULL(e.Discount_Pct, 0) AS Weekend_Pct
                        FROM Loyalty_Tiers t
                        LEFT JOIN Loyalty_Tier_Room_Discounts w
                               ON w.Tier_ID = t.ID AND w.Day_Type = 'WEEKDAY' AND w.Is_Active = 1
                        LEFT JOIN Loyalty_Tier_Room_Discounts e
                               ON e.Tier_ID = t.ID AND e.Day_Type = 'WEEKEND' AND e.Is_Active = 1
                       WHERE t.IsActive = 1
                       ORDER BY t.DisplayOrder", null);
            }
            catch { return null; }
        }

        public void SetRoomDiscount(int tierId, string dayType, decimal pct)
        {
            if (dayType != "WEEKDAY" && dayType != "WEEKEND") return;
            if (pct < 0m) pct = 0m; if (pct > 100m) pct = 100m;
            _code.DatabaseInsertSafe(_conn,
                @"IF EXISTS (SELECT 1 FROM Loyalty_Tier_Room_Discounts WHERE Tier_ID = @t AND Day_Type = @d)
                      UPDATE Loyalty_Tier_Room_Discounts
                         SET Discount_Pct = @v, Is_Active = 1, Updated_Date = GETDATE()
                       WHERE Tier_ID = @t AND Day_Type = @d;
                  ELSE
                      INSERT INTO Loyalty_Tier_Room_Discounts (Tier_ID, Day_Type, Discount_Pct)
                      VALUES (@t, @d, @v);",
                P("@t", tierId, "@d", dayType, "@v", pct));
        }

        /// <summary>วันไหนถือเป็น "วันหยุด": เสาร์-อาทิตย์ หรือมีในตารางราคาวันหยุด/เทศกาล</summary>
        public bool IsWeekendOrHoliday(DateTime date)
        {
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) return true;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 1 FROM Accommodation_HolidayPrice WHERE CAST(DateNewPrice AS DATE) = @d",
                    P("@d", date.Date));
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>% ส่วนลดค่าห้องของสมาชิกสำหรับวันเช็คอินที่ระบุ (0 = ไม่มี/ไม่ใช่สมาชิก/หมดอายุ)</summary>
        public decimal GetRoomDiscountPct(string phone, DateTime checkinDate)
        {
            try
            {
                var card = GetCard(phone);
                if (card == null || IsMembershipExpired(card)) return 0m;
                string dayType = IsWeekendOrHoliday(checkinDate) ? "WEEKEND" : "WEEKDAY";
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT Discount_Pct FROM Loyalty_Tier_Room_Discounts
                       WHERE Tier_ID = @t AND Day_Type = @d AND Is_Active = 1",
                    P("@t", Convert.ToInt32(card["CurrentTier_ID"]), "@d", dayType));
                return dt != null && dt.Rows.Count > 0 ? Convert.ToDecimal(dt.Rows[0][0]) : 0m;
            }
            catch { return 0m; }
        }

        // ═══════════════════════ Voucher ═══════════════════════

        public DataTable GetTemplates(bool activeOnly = false)
        {
            return _code.DatabaseQuerySafe(_conn,
                "SELECT * FROM Member_Voucher_Templates " +
                (activeOnly ? "WHERE Is_Active = 1 " : "") + "ORDER BY ID DESC", null);
        }

        public void SaveTemplate(int id, string name, string desc, string prefix, int? tierId,
            int validDays, int windowMin, bool active)
        {
            prefix = new string((prefix ?? "VC").ToUpperInvariant()
                .Replace(" ", "").ToCharArray()).Trim();
            if (prefix.Length == 0) prefix = "VC";
            if (prefix.Length > 8) prefix = prefix.Substring(0, 8);
            if (validDays < 1) validDays = 90;
            if (windowMin < 5) windowMin = 60;

            if (id > 0)
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Member_Voucher_Templates
                         SET Name = @n, Description = @d, Code_Prefix = @pf, Tier_ID = @tier,
                             Valid_Days = @vd, Redeem_Window_Min = @wm, Is_Active = @a
                       WHERE ID = @id",
                    P("@n", name, "@d", desc, "@pf", prefix, "@tier", (object)tierId ?? DBNull.Value, "@vd", validDays, "@wm", windowMin, "@a", active, "@id", id));
            else
                _code.DatabaseInsertSafe(_conn,
                    @"INSERT INTO Member_Voucher_Templates
                          (Name, Description, Code_Prefix, Tier_ID, Valid_Days, Redeem_Window_Min, Is_Active)
                      VALUES (@n, @d, @pf, @tier, @vd, @wm, @a)",
                    P("@n", name, "@d", desc, "@pf", prefix, "@tier", (object)tierId ?? DBNull.Value, "@vd", validDays, "@wm", windowMin, "@a", active));
        }

        /// <summary>แจก voucher ให้สมาชิก 1 คน — คืน (จำนวนที่แจก, ข้อความ)</summary>
        public (int issued, string msg) IssueToMember(int templateId, string phone, string issuedBy)
        {
            phone = SanitizePhone(phone);
            var t = _code.DatabaseQuerySafe(_conn,
                "SELECT * FROM Member_Voucher_Templates WHERE ID = @id AND Is_Active = 1", P("@id", templateId));
            if (t == null || t.Rows.Count == 0) return (0, "ไม่พบ template หรือถูกปิดใช้งาน");
            var m = _code.DatabaseQuerySafe(_conn,
                "SELECT TOP 1 1 FROM Customer_Loyalty WHERE Customer_MobilePhone = @p", P("@p", phone));
            if (m == null || m.Rows.Count == 0) return (0, $"เบอร์ {phone} ไม่ได้เป็นสมาชิก");

            // ถูกตัดสิทธิ์ template นี้เป็นรายคนไว้ (หน้า จัดการสมาชิก) → ไม่แจก
            try
            {
                var ex = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 1 FROM Member_Voucher_Exclusions WHERE Customer_MobilePhone = @p AND Template_ID = @t",
                    P("@p", phone, "@t", templateId));
                if (ex != null && ex.Rows.Count > 0)
                    return (0, $"เบอร์ {phone} ถูกตั้งค่า \"ไม่ให้สิทธิ์\" คูปองนี้ไว้ — เอาติ๊กออกได้ที่หน้า จัดการสมาชิก");
            }
            catch { }

            string code = GenerateCode(t.Rows[0]["Code_Prefix"].ToString());
            int validDays = Convert.ToInt32(t.Rows[0]["Valid_Days"]);
            _code.DatabaseInsertSafe(_conn,
                @"INSERT INTO Member_Vouchers (Template_ID, Customer_MobilePhone, Code, Expiry_Date, Issued_By)
                  VALUES (@t, @p, @c, @e, @by)",
                P("@t", templateId, "@p", phone, "@c", code, "@e", DateTime.Today.AddDays(validDays), "@by", issuedBy));
            return (1, $"แจกให้ {phone} แล้ว (โค้ด {code})");
        }

        /// <summary>แจกทั้ง tier — สมาชิกที่ยังไม่หมดอายุและยังไม่เคยได้ template นี้ (กันแจกซ้ำ)</summary>
        public (int issued, string msg) IssueToTier(int templateId, int tierId, string issuedBy)
        {
            var t = _code.DatabaseQuerySafe(_conn,
                "SELECT * FROM Member_Voucher_Templates WHERE ID = @id AND Is_Active = 1", P("@id", templateId));
            if (t == null || t.Rows.Count == 0) return (0, "ไม่พบ template หรือถูกปิดใช้งาน");

            var members = _code.DatabaseQuerySafe(_conn,
                @"SELECT cl.Customer_MobilePhone FROM Customer_Loyalty cl
                   WHERE cl.CurrentTier_ID = @tier
                     AND (cl.Membership_Expiry IS NULL OR cl.Membership_Expiry >= CAST(GETDATE() AS DATE))
                     AND NOT EXISTS (SELECT 1 FROM Member_Vouchers v
                                      WHERE v.Customer_MobilePhone = cl.Customer_MobilePhone
                                        AND v.Template_ID = @tid
                                        AND v.Status IN ('ISSUED','ACTIVATED'))
                     AND NOT EXISTS (SELECT 1 FROM Member_Voucher_Exclusions x
                                      WHERE x.Customer_MobilePhone = cl.Customer_MobilePhone
                                        AND x.Template_ID = @tid)",
                P("@tier", tierId, "@tid", templateId));
            if (members == null || members.Rows.Count == 0) return (0, "ไม่มีสมาชิกที่ต้องแจกเพิ่ม (ทุกคนมี voucher นี้ค้างอยู่แล้ว)");

            int validDays = Convert.ToInt32(t.Rows[0]["Valid_Days"]);
            string prefix = t.Rows[0]["Code_Prefix"].ToString();
            int n = 0;
            foreach (DataRow r in members.Rows)
            {
                _code.DatabaseInsertSafe(_conn,
                    @"INSERT INTO Member_Vouchers (Template_ID, Customer_MobilePhone, Code, Expiry_Date, Issued_By)
                      VALUES (@t, @p, @c, @e, @by)",
                    P("@t", templateId, "@p", r[0].ToString(), "@c", GenerateCode(prefix), "@e", DateTime.Today.AddDays(validDays), "@by", issuedBy));
                n++;
            }
            return (n, $"แจกแล้ว {n} คน");
        }

        /// <summary>voucher ของสมาชิก (พร้อมชื่อ/เงื่อนไข template) — mark หมดอายุให้ระหว่างทาง</summary>
        public DataTable GetMemberVouchers(string phone)
        {
            phone = SanitizePhone(phone);
            // หมดอายุ: ทั้งตัว voucher และโค้ดที่กดใช้แล้วแต่ปล่อยเกินหน้าต่างเวลา → กลับเป็น ISSUED ให้กดใหม่ได้
            _code.DatabaseInsertSafe(_conn,
                @"UPDATE Member_Vouchers SET Status = 'EXPIRED'
                   WHERE Customer_MobilePhone = @p AND Status IN ('ISSUED','ACTIVATED') AND Expiry_Date < CAST(GETDATE() AS DATE);
                  UPDATE Member_Vouchers SET Status = 'ISSUED', Activated_Date = NULL, Activation_Expiry = NULL
                   WHERE Customer_MobilePhone = @p AND Status = 'ACTIVATED' AND Activation_Expiry < GETDATE();",
                P("@p", phone));

            return _code.DatabaseQuerySafe(_conn,
                @"SELECT v.ID, v.Code, v.Status, v.Issued_Date, v.Expiry_Date, v.Activation_Expiry,
                         v.Redeemed_Date, t.Name, t.Description, t.Redeem_Window_Min
                    FROM Member_Vouchers v
                    JOIN Member_Voucher_Templates t ON t.ID = v.Template_ID
                   WHERE v.Customer_MobilePhone = @p
                   ORDER BY CASE v.Status WHEN 'ACTIVATED' THEN 0 WHEN 'ISSUED' THEN 1 ELSE 2 END,
                            v.Expiry_Date", P("@p", phone));
        }

        /// <summary>สมาชิกกด "ใช้คูปอง" → เปิดโค้ดพร้อมหน้าต่างเวลา — คืน (ok, code, หมดเวลาเมื่อ, msg)</summary>
        public (bool ok, string codeText, DateTime windowEnd, string msg) Activate(long voucherId, string phone)
        {
            phone = SanitizePhone(phone);
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT v.Code, v.Status, v.Expiry_Date, v.Activation_Expiry, t.Redeem_Window_Min
                    FROM Member_Vouchers v JOIN Member_Voucher_Templates t ON t.ID = v.Template_ID
                   WHERE v.ID = @id AND v.Customer_MobilePhone = @p",
                P("@id", voucherId, "@p", phone));
            if (dt == null || dt.Rows.Count == 0) return (false, null, DateTime.MinValue, "ไม่พบ voucher");

            DataRow r = dt.Rows[0];
            string status = r["Status"].ToString();
            if (status == "ACTIVATED" && r["Activation_Expiry"] != DBNull.Value
                && Convert.ToDateTime(r["Activation_Expiry"]) > DateTime.Now)
                return (true, r["Code"].ToString(), Convert.ToDateTime(r["Activation_Expiry"]), "โค้ดยังใช้งานได้");
            if (status == "REDEEMED") return (false, null, DateTime.MinValue, "voucher นี้ถูกใช้ไปแล้ว");
            if (status == "EXPIRED" || Convert.ToDateTime(r["Expiry_Date"]).Date < DateTime.Today)
                return (false, null, DateTime.MinValue, "voucher หมดอายุแล้ว");

            int window = Convert.ToInt32(r["Redeem_Window_Min"]);
            DateTime end = DateTime.Now.AddMinutes(window);
            _code.DatabaseInsertSafe(_conn,
                @"UPDATE Member_Vouchers
                     SET Status = 'ACTIVATED', Activated_Date = GETDATE(), Activation_Expiry = @end
                   WHERE ID = @id AND Customer_MobilePhone = @p AND Status = 'ISSUED'",
                P("@end", end, "@id", voucherId, "@p", phone));
            return (true, r["Code"].ToString(), end, "แสดงโค้ดนี้ให้พนักงาน");
        }

        /// <summary>พนักงานแลกด้วยโค้ด — ตรวจสถานะ+เวลา แล้วปิดเป็น REDEEMED</summary>
        public (bool ok, string msg, DataRow info) RedeemByCode(string codeText, short? adminId, string note)
        {
            codeText = (codeText ?? "").Trim().ToUpperInvariant();
            if (codeText.Length < 4) return (false, "กรุณากรอกโค้ด", null);

            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT v.ID, v.Status, v.Expiry_Date, v.Activation_Expiry, v.Customer_MobilePhone,
                         t.Name, t.Description, c.Name AS CustomerName
                    FROM Member_Vouchers v
                    JOIN Member_Voucher_Templates t ON t.ID = v.Template_ID
                    LEFT JOIN Customer c ON c.MobilePhone = v.Customer_MobilePhone
                   WHERE v.Code = @c", P("@c", codeText));
            if (dt == null || dt.Rows.Count == 0) return (false, "ไม่พบโค้ดนี้ในระบบ", null);

            DataRow r = dt.Rows[0];
            string status = r["Status"].ToString();
            if (status == "REDEEMED") return (false, "โค้ดนี้ถูกใช้ไปแล้ว", r);
            if (status == "EXPIRED" || Convert.ToDateTime(r["Expiry_Date"]).Date < DateTime.Today)
                return (false, "voucher หมดอายุแล้ว", r);
            if (status != "ACTIVATED")
                return (false, "ลูกค้ายังไม่ได้กด \"ใช้คูปอง\" ในมือถือ — ให้ลูกค้ากดใช้ก่อนแล้วลองใหม่", r);
            if (r["Activation_Expiry"] != DBNull.Value && Convert.ToDateTime(r["Activation_Expiry"]) < DateTime.Now)
                return (false, "โค้ดหมดเวลา — ให้ลูกค้ากด \"ใช้คูปอง\" ใหม่อีกครั้ง", r);

            int n = _code.DatabaseInsertSafe(_conn,
                @"UPDATE Member_Vouchers
                     SET Status = 'REDEEMED', Redeemed_Date = GETDATE(),
                         Redeemed_By_AdminID = @a, Redeem_Note = @note
                   WHERE ID = @id AND Status = 'ACTIVATED'",
                P("@a", (object)adminId ?? DBNull.Value, "@note", note ?? "", "@id", Convert.ToInt64(r["ID"])));
            if (n <= 0) return (false, "แลกไม่สำเร็จ (โค้ดเพิ่งถูกใช้จากเครื่องอื่น)", r);

            try
            {
                _code.Logs(_conn, "MemberVoucher",
                    $"แลก voucher {codeText} ({r["Name"]}) ของ {r["Customer_MobilePhone"]}"
                    + (string.IsNullOrEmpty(note) ? "" : $" — {note}"), adminId?.ToString() ?? "SYSTEM");
            }
            catch { }
            return (true, $"แลกสำเร็จ: {r["Name"]} — คุณ{r["CustomerName"]} ({r["Customer_MobilePhone"]})", r);
        }

        /// <summary>ประวัติ voucher ล่าสุด (tracking ฝั่งพนักงาน)</summary>
        public DataTable GetRecentVouchers(int limit = 60)
        {
            return _code.DatabaseQuerySafe(_conn,
                @"SELECT TOP (@n) v.Code, v.Status, v.Issued_Date, v.Expiry_Date, v.Redeemed_Date,
                         v.Redeem_Note, v.Customer_MobilePhone, t.Name,
                         c.Name AS CustomerName, a.Username AS RedeemedBy
                    FROM Member_Vouchers v
                    JOIN Member_Voucher_Templates t ON t.ID = v.Template_ID
                    LEFT JOIN Customer c ON c.MobilePhone = v.Customer_MobilePhone
                    LEFT JOIN [dbo].[Admin] a ON a.ID = v.Redeemed_By_AdminID
                   ORDER BY v.ID DESC", P("@n", limit));
        }

        // ═══════════════ สมัคร/ต่ออายุสมาชิก (เก็บค่าสมัคร + ลงรายได้) ═══════════════

        /// <summary>
        /// สมัคร/ต่ออายุ/อัปเกรดสมาชิก — อัปเดต tier+วันหมดอายุ, บันทึกการชำระ,
        /// และถ้ามีค่าสมัคร > 0: สร้างใบรับเงิน MBR-xxx + ส่งลงบัญชี NextAcc
        /// (รายได้ค่าสมาชิก MEMBER_FEE_REVENUE — ไม่ตั้ง mapping ก็ fallback บัญชีรายได้เริ่มต้น)
        /// </summary>
        public (bool ok, string msg) Enroll(string phone, int tierId, decimal amount, string paidHow, string staffName)
        {
            phone = SanitizePhone(phone);
            if (phone.Length < 9) return (false, "เบอร์โทรไม่ถูกต้อง");

            var tier = _code.DatabaseQuerySafe(_conn,
                @"SELECT TierName, ISNULL(Signup_Fee, 0) AS Fee, ISNULL(Duration_Months, 12) AS Months
                    FROM Loyalty_Tiers WHERE ID = @t AND IsActive = 1", P("@t", tierId));
            if (tier == null || tier.Rows.Count == 0) return (false, "ไม่พบระดับสมาชิกนี้");
            int months = Convert.ToInt32(tier.Rows[0]["Months"]);
            string tierName = tier.Rows[0]["TierName"].ToString();
            if (amount < 0m) amount = 0m;

            // สถานะเดิม (อ่านก่อนสร้างแถว เพื่อจำแนก NEW/RENEW/UPGRADE ให้ถูก)
            var cur = _code.DatabaseQuerySafe(_conn,
                "SELECT CurrentTier_ID, Membership_Expiry FROM Customer_Loyalty WHERE Customer_MobilePhone = @p",
                P("@p", phone));
            bool existed = cur != null && cur.Rows.Count > 0;
            int oldTier = existed ? Convert.ToInt32(cur.Rows[0]["CurrentTier_ID"]) : 0;
            DateTime? oldExpiry = existed && cur.Rows[0]["Membership_Expiry"] != DBNull.Value
                ? (DateTime?)Convert.ToDateTime(cur.Rows[0]["Membership_Expiry"]) : null;
            bool stillActive = existed && (oldExpiry == null || oldExpiry.Value.Date >= DateTime.Today);

            string action = !existed || !stillActive ? "NEW"
                : tierId != oldTier ? "UPGRADE" : "RENEW";

            // ให้แน่ใจว่ามีแถวลูกค้า + แถวสมาชิก
            _code.DatabaseInsertSafe(_conn,
                @"IF NOT EXISTS (SELECT 1 FROM Customer WHERE MobilePhone = @p)
                      INSERT INTO Customer (MobilePhone, Name) VALUES (@p, N'สมาชิกใหม่');
                  IF NOT EXISTS (SELECT 1 FROM Customer_Loyalty WHERE Customer_MobilePhone = @p)
                      INSERT INTO Customer_Loyalty (Customer_MobilePhone, CurrentTier_ID) VALUES (@p, @t);",
                P("@p", phone, "@t", tierId));

            DateTime? newExpiry;
            if (months <= 0) newExpiry = null;   // ตลอดชีพ
            else
            {
                DateTime baseDate = (action == "RENEW" && oldExpiry.HasValue && oldExpiry.Value.Date >= DateTime.Today)
                    ? oldExpiry.Value.Date : DateTime.Today;   // ต่ออายุ = นับต่อจากวันหมดอายุเดิมที่ยังเหลือ
                newExpiry = baseDate.AddMonths(months);
            }

            _code.DatabaseInsertSafe(_conn,
                @"UPDATE Customer_Loyalty
                     SET CurrentTier_ID = @t, Membership_Expiry = @e, LastUpdated = GETDATE()
                   WHERE Customer_MobilePhone = @p",
                P("@t", tierId, "@e", (object)newExpiry ?? DBNull.Value, "@p", phone));

            // บันทึกการชำระ + ลงรายได้ (เฉพาะเมื่อเก็บเงินจริง)
            string receiptRef = null;
            long payId = _code.DatabaseInsertReturnSafe(_conn,
                @"INSERT INTO Membership_Payments
                      (Customer_MobilePhone, Tier_ID, Action_Type, Amount, Paid_How, Old_Expiry, New_Expiry, Created_By)
                  VALUES (@p, @t, @a, @amt, @how, @oe, @ne, @by);
                  SELECT SCOPE_IDENTITY();",
                P("@p", phone, "@t", tierId, "@a", action, "@amt", amount,
                  "@how", paidHow ?? "", "@oe", (object)oldExpiry ?? DBNull.Value,
                  "@ne", (object)newExpiry ?? DBNull.Value, "@by", staffName ?? ""));

            if (amount > 0m)
            {
                receiptRef = "MBR-" + payId;
                CreateMemberFeeReceipt(receiptRef, amount, paidHow,
                    $"ค่าสมัครสมาชิก {tierName} ({ActionThai(action)}) — {phone}");
                _code.DatabaseInsertSafe(_conn,
                    "UPDATE Membership_Payments SET Receipt_Ref = @r WHERE ID = @id",
                    P("@r", receiptRef, "@id", payId));
            }

            try
            {
                _code.Logs(_conn, "Membership",
                    $"{ActionThai(action)}สมาชิก {phone} → {tierName} ค่าสมัคร {amount:N2} " +
                    $"หมดอายุ {(newExpiry.HasValue ? newExpiry.Value.ToString("dd/MM/yyyy") : "ตลอดชีพ")}" +
                    (receiptRef != null ? $" (ใบรับเงิน {receiptRef})" : ""), staffName ?? "SYSTEM");
            }
            catch { }

            return (true, $"{ActionThai(action)}สำเร็จ — ระดับ {tierName}, หมดอายุ " +
                (newExpiry.HasValue ? newExpiry.Value.ToString("dd/MM/yyyy") : "ตลอดชีพ") +
                (amount > 0m ? $" · เก็บเงิน ฿{amount:N2} ลงบัญชีแล้ว ({receiptRef})" : " · ไม่มีค่าสมัคร"));
        }

        private static string ActionThai(string a) =>
            a == "NEW" ? "สมัคร" : a == "UPGRADE" ? "อัปเกรด" : "ต่ออายุ";

        /// <summary>ใบรับเงินค่าสมาชิก + ส่งเข้า NextAcc — โครงเดียวกับใบสรุปรายวัน (idempotent ด้วย ref)</summary>
        private void CreateMemberFeeReceipt(string receiptRef, decimal amount, string paidHow, string lineText)
        {
            try
            {
                bool useVat = false;
                try
                {
                    var v = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 Use_Vat FROM Business_Info", null);
                    if (v?.Rows.Count > 0 && v.Rows[0][0] != DBNull.Value)
                    {
                        string x = v.Rows[0][0].ToString();
                        useVat = x == "1" || x.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch { }
                decimal exVat = useVat ? Math.Round(amount / 1.07m, 2, MidpointRounding.AwayFromZero) : amount;
                decimal vat = useVat ? (amount - exVat) : 0m;

                _code.DatabaseInsertSafe(_conn,
                    "IF NOT EXISTS (SELECT 1 FROM [dbo].[Account_Receipt] WHERE [ID] = @ID) " +
                    "INSERT INTO [dbo].[Account_Receipt] " +
                    "([ID],[Reservation_ID],[Created_Date],[Total_Amount],[Vat],[Total_Amount_Exclude_Vat]," +
                    "[IsDeposit],[UseDeposit],[Paid_Type],[Status],[Created_By_ID],[Etax],[Customer_ID]) " +
                    "VALUES (@ID,'0',GETDATE(),@T,@V,@X,0,0,@PT,'Normal',0,0,0)",
                    P("@ID", receiptRef, "@T", amount, "@V", vat, "@X", exVat, "@PT", paidHow ?? "เงินสด"));
                _code.DatabaseInsertSafe(_conn,
                    "IF NOT EXISTS (SELECT 1 FROM [dbo].[Account_Receipt_Detail] WHERE [Receipt_ID] = @R) " +
                    "INSERT INTO [dbo].[Account_Receipt_Detail] " +
                    "([Number],[Receipt_ID],[ProductType_ID],[Product_ID],[Product_Data],[Product_Amount],[Product_Unit],[Price_PerPeice],[Price_Amount]) " +
                    "VALUES (1,@R,'0','0',@D,1,N'รายการ',@T,@T)",
                    P("@R", receiptRef, "@D", lineText, "@T", amount));

                // ส่งเข้า NextAcc — revenueType MEMBER_FEE_REVENUE (mapper fallback บัญชีรายได้เริ่มต้นถ้ายังไม่ map)
                var sync = new Take_Time_BangPhra.Integration.AccountingSyncService(_conn);
                string accId = sync.LookupPaidHowAccountId(paidHow);
                sync.EnqueueReceipt(0, receiptRef, amount, 0, DateTime.Today, lineText,
                    isDeposit: false, paymentMethod: paidHow ?? "เงินสด",
                    revenueType: "MEMBER_FEE_REVENUE", paymentAccountId: accId);
            }
            catch (Exception ex)
            {
                try { _code.Logs(_conn, "Membership", $"ลงบัญชีค่าสมาชิก {receiptRef} ไม่สำเร็จ: {ex.Message}", "SYSTEM"); }
                catch { }
            }
        }

        public DataTable GetTierFees()
        {
            return _code.DatabaseQuerySafe(_conn,
                @"SELECT ID, TierName, TierColor, ISNULL(Signup_Fee, 0) AS Signup_Fee,
                         ISNULL(Duration_Months, 12) AS Duration_Months
                    FROM Loyalty_Tiers WHERE IsActive = 1 ORDER BY DisplayOrder", null);
        }

        public void SaveTierFee(int tierId, decimal fee, int months)
        {
            if (fee < 0m) fee = 0m; if (months < 0) months = 0;
            _code.DatabaseInsertSafe(_conn,
                "UPDATE Loyalty_Tiers SET Signup_Fee = @f, Duration_Months = @m WHERE ID = @t",
                P("@f", fee, "@m", months, "@t", tierId));
        }

        public DataTable GetMemberPayments(string phone)
        {
            return _code.DatabaseQuerySafe(_conn,
                @"SELECT TOP 30 mp.*, t.TierName FROM Membership_Payments mp
                    JOIN Loyalty_Tiers t ON t.ID = mp.Tier_ID
                   WHERE mp.Customer_MobilePhone = @p ORDER BY mp.ID DESC",
                P("@p", SanitizePhone(phone)));
        }

        public void SetMembershipExpiry(string phone, DateTime? expiry)
        {
            _code.DatabaseInsertSafe(_conn,
                "UPDATE Customer_Loyalty SET Membership_Expiry = @e WHERE Customer_MobilePhone = @p",
                P("@e", (object)expiry ?? DBNull.Value, "@p", SanitizePhone(phone)));
        }

        // ═══════════════ ตัดสิทธิ์ voucher รายคน + ยกเลิกใบที่แจกแล้ว ═══════════════

        /// <summary>template ทั้งหมด + สถานะของสมาชิกคนนี้ (ถูกตัดสิทธิ์? มีใบค้าง?)</summary>
        public DataTable GetMemberVoucherMatrix(string phone)
        {
            return _code.DatabaseQuerySafe(_conn,
                @"SELECT t.ID, t.Name, t.Description, t.Is_Active,
                         CASE WHEN x.ID IS NULL THEN 0 ELSE 1 END AS Excluded,
                         (SELECT COUNT(*) FROM Member_Vouchers v
                           WHERE v.Customer_MobilePhone = @p AND v.Template_ID = t.ID
                             AND v.Status IN ('ISSUED','ACTIVATED')) AS Held
                    FROM Member_Voucher_Templates t
                    LEFT JOIN Member_Voucher_Exclusions x
                           ON x.Template_ID = t.ID AND x.Customer_MobilePhone = @p
                   ORDER BY t.ID DESC",
                P("@p", SanitizePhone(phone)));
        }

        /// <summary>ติ๊ก "ไม่ให้สิทธิ์" — excluded=true จะยกเลิกใบที่ยังไม่ใช้ของ template นั้นด้วย</summary>
        public void SetExclusion(string phone, int templateId, bool excluded, string staffName)
        {
            phone = SanitizePhone(phone);
            if (excluded)
            {
                _code.DatabaseInsertSafe(_conn,
                    @"IF NOT EXISTS (SELECT 1 FROM Member_Voucher_Exclusions WHERE Customer_MobilePhone = @p AND Template_ID = @t)
                          INSERT INTO Member_Voucher_Exclusions (Customer_MobilePhone, Template_ID, Created_By)
                          VALUES (@p, @t, @by);
                      UPDATE Member_Vouchers SET Status = 'CANCELLED'
                       WHERE Customer_MobilePhone = @p AND Template_ID = @t AND Status IN ('ISSUED','ACTIVATED');",
                    P("@p", phone, "@t", templateId, "@by", staffName ?? ""));
            }
            else
            {
                _code.DatabaseInsertSafe(_conn,
                    "DELETE FROM Member_Voucher_Exclusions WHERE Customer_MobilePhone = @p AND Template_ID = @t",
                    P("@p", phone, "@t", templateId));
            }
        }

        /// <summary>ยกเลิก voucher รายใบ (เฉพาะที่ยังไม่ถูกใช้)</summary>
        public bool CancelVoucher(long voucherId, string staffName)
        {
            int n = _code.DatabaseInsertSafe(_conn,
                @"UPDATE Member_Vouchers SET Status = 'CANCELLED',
                         Redeem_Note = LEFT(N'ยกเลิกโดย ' + @by, 300)
                   WHERE ID = @id AND Status IN ('ISSUED','ACTIVATED')",
                P("@by", staffName ?? "", "@id", voucherId));
            return n > 0;
        }

        // ═══════════════════════ helpers ═══════════════════════

        private string GenerateCode(string prefix)
        {
            // ตัดตัวที่อ่านสับสน (0/O, 1/I/L) — พนักงานพิมพ์ตามลูกค้าอ่านได้ไม่ผิด
            const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var sb = new StringBuilder(prefix).Append('-');
                lock (_rng) { for (int i = 0; i < 5; i++) sb.Append(chars[_rng.Next(chars.Length)]); }
                string codeText = sb.ToString();
                var dup = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 1 FROM Member_Vouchers WHERE Code = @c", P("@c", codeText));
                if (dup == null || dup.Rows.Count == 0) return codeText;
            }
            return prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        }

        private static string SanitizePhone(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return "";
            var sb = new StringBuilder();
            foreach (char c in p) if (char.IsDigit(c)) sb.Append(c);
            string s = sb.ToString();
            if (s.StartsWith("66") && s.Length > 9) s = "0" + s.Substring(2);
            return s;
        }

        private static Dictionary<string, object> P(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[kv[i].ToString()] = kv[i + 1] ?? DBNull.Value;
            return d;
        }
    }
}
