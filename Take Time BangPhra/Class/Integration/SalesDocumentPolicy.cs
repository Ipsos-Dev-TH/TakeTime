using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>ผลการตัดสินว่า "การขายครั้งนี้" ต้องออกเอกสารบน NextAcc แบบไหน (ดู <see cref="SalesDocumentPolicy"/>)</summary>
    public sealed class SalesDocRoute
    {
        /// <summary>IMMEDIATE | DAILY_ROLLUP | OFF</summary>
        public string Mode;
        /// <summary>ชนิดการชำระเงินของแหล่งเงิน (CASH / TRANSFER / QR / CARD / GATEWAY_OTHER / DIRECTOR_LOAN / OTA / OTHER)</summary>
        public string ChannelType;
        /// <summary>เหตุผลอ่านง่าย (กฎข้อไหนตัดสิน) — ใช้ใน log / หน้าตั้งค่า</summary>
        public string Reason;
        public bool IsImmediate => Mode == "IMMEDIATE";
        public bool IsRollup => Mode == "DAILY_ROLLUP";
        public bool IsOff => Mode == "OFF";
    }

    /// <summary>
    /// นโยบายเอกสารขาย (PHASE19_29) — ตอบคำถามเดียว: ขายผ่านช่องทาง X ด้วยแหล่งเงิน Y ต้อง
    ///   IMMEDIATE    = ออกเอกสารรายใบบน NextAcc ทันที (walk-in → ใบกำกับภาษีอย่างย่อขายสด / ผู้ซื้อข้อมูลครบ → เต็มรูป)
    ///   DAILY_ROLLUP = ไม่ออกรายใบ — รวมเข้าเอกสารสรุปรายวัน (ต่อวัน × แหล่งเงิน)
    ///   OFF          = ไม่ส่งเข้า NextAcc เลย
    ///
    /// ลำดับการตัดสิน: ค่าตั้งเฉพาะแหล่งเงิน (Account_Paid_How.Sales_Doc_Mode) → (ช่องทาง, ชนิด) → (ช่องทาง, *)
    ///                → (*, ชนิด) → (*, *) → IMMEDIATE · แถวที่ปิด (Is_Enabled = 0) ถูกข้าม
    ///
    /// ทนทานต่อฐานข้อมูลที่ยังไม่รัน migration: ไม่มีตาราง / ไม่มีค่าตั้ง → <see cref="Enabled"/> = false
    /// (ผู้เรียกใช้พฤติกรรมเดิม). อ่านครั้งเดียวต่อ 60 วินาที (cache ต่อ connection string) — หน้าตั้งค่าเรียก
    /// <see cref="Invalidate"/> หลังบันทึก. ห้ามโยน exception ออกไปหาผู้เรียก (การขายต้องไม่พังเพราะนโยบาย)
    /// </summary>
    public static class SalesDocumentPolicy
    {
        public const string ChPos = "POS", ChRoomService = "ROOM_SERVICE", ChActivity = "ACTIVITY", ChAmenity = "AMENITY",
            ChFrontdeskGateway = "FRONTDESK_GATEWAY", ChDamage = "DAMAGE", ChReservation = "RESERVATION";

        public const string ModeImmediate = "IMMEDIATE", ModeDailyRollup = "DAILY_ROLLUP", ModeOff = "OFF";
        public const string Any = "*";

        public const string CfgEnabled = "Sales_Doc_Policy_Enabled";
        public const string CfgLocalSlipRequired = "Sales_Doc_Local_Slip_Required";
        public const string CfgSendLocalInvoiceEmail = "Sales_Doc_Send_Local_Invoice_Email";

        /// <summary>ช่องทางขายทั้งหมด (ไม่รวม '*') — เรียงตามที่แสดงในหน้าตั้งค่า</summary>
        public static readonly string[] SalesChannels =
            { ChPos, ChRoomService, ChActivity, ChAmenity, ChFrontdeskGateway, ChDamage, ChReservation };

        /// <summary>ชนิดการชำระเงินทั้งหมด (ไม่รวม '*') — ตรงกับ PaymentChannelCatalog.AllTypes</summary>
        public static readonly string[] ChannelTypes =
            { "CASH", "TRANSFER", "QR", "CARD", "GATEWAY_OTHER", "DIRECTOR_LOAN", "OTA", "OTHER" };

        public static readonly string[] Modes = { ModeImmediate, ModeDailyRollup, ModeOff };

        // ── ข้อมูลที่ cache ──────────────────────────────────────────────────────

        /// <summary>กฎ 1 แถวของตาราง Sales_Doc_Policy</summary>
        public sealed class Rule
        {
            public int Id;
            public string SalesChannel;
            public string ChannelType;
            public string Mode;
            public bool Enabled;
            public string Note;
            public string UpdatedBy;
            public DateTime? UpdatedAt;
        }

        /// <summary>แหล่งเงิน 1 แถว (Account_Paid_How) พร้อมชนิดที่ใช้ตัดสิน</summary>
        public sealed class PaidHowInfo
        {
            public int Id;
            public string Name;
            public bool Active;
            /// <summary>ชนิดที่ใช้จริง (Channel_Type ที่ผู้ดูแลจัด หรือเดาจากชื่อ)</summary>
            public string ChannelType;
            /// <summary>true = ชนิดมาจาก Channel_Type (PHASE19_20) · false = เดาจากชื่อ</summary>
            public bool Classified;
            /// <summary>ค่าตั้งเฉพาะแหล่งเงิน (IMMEDIATE | DAILY_ROLLUP | OFF) — null = ใช้ตาราง</summary>
            public string OverrideMode;
        }

        private sealed class Snapshot
        {
            public bool TableReady;
            public bool OverrideColumnReady;
            public bool ChannelTypeColumnReady;
            public readonly Dictionary<string, Rule> Rules = new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<int, PaidHowInfo> PaidHows = new Dictionary<int, PaidHowInfo>();
            public readonly Dictionary<string, string> Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public DateTime LoadedAt;
        }

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Snapshot> _cache = new Dictionary<string, Snapshot>(StringComparer.Ordinal);
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        // ── API หลัก (สัญญา) ─────────────────────────────────────────────────────

        /// <summary>
        /// เปิดใช้นโยบายหรือไม่ = มีตาราง Sales_Doc_Policy + ค่าตั้ง Sales_Doc_Policy_Enabled = '1'
        /// (ไม่มีค่าตั้ง = '0' = พฤติกรรมเดิม)
        /// </summary>
        public static bool Enabled(string conn)
        {
            try
            {
                var s = Load(conn);
                return s.TableReady && Flag(s, CfgEnabled, false);
            }
            catch { return false; }
        }

        /// <summary>
        /// ตัดสินเส้นทางเอกสาร: ค่าตั้งเฉพาะแหล่งเงิน → (ช่องทาง, ชนิด) → (ช่องทาง, *) → (*, ชนิด) → (*, *) → IMMEDIATE.
        /// ไม่โยน exception — ผิดพลาด = IMMEDIATE (ปลอดภัยทางภาษี: ออกเอกสารรายใบ)
        /// </summary>
        public static SalesDocRoute Resolve(string conn, string salesChannel, int paidHowId)
        {
            var route = new SalesDocRoute { Mode = ModeImmediate, ChannelType = "OTHER", Reason = "ค่าเริ่มต้น (ออกเอกสารทันที)" };
            try
            {
                var s = Load(conn);
                string ch = NormalizeChannel(salesChannel);
                string type = ChannelTypeOf(s, paidHowId);
                route.ChannelType = type;

                if (!s.TableReady)
                {
                    route.Reason = "ยังไม่ได้รัน PHASE19_29 (ไม่มีตาราง Sales_Doc_Policy) — ออกเอกสารทันที";
                    return route;
                }

                PaidHowInfo ph;
                if (paidHowId > 0 && s.PaidHows.TryGetValue(paidHowId, out ph) && !string.IsNullOrEmpty(ph.OverrideMode))
                {
                    route.Mode = ph.OverrideMode;
                    route.Reason = "ตั้งเฉพาะแหล่งเงิน \"" + ph.Name + "\" = " + ModeLabel(ph.OverrideMode);
                    return route;
                }

                string[][] order =
                {
                    new[] { ch, type }, new[] { ch, Any }, new[] { Any, type }, new[] { Any, Any }
                };
                foreach (var k in order)
                {
                    Rule r;
                    if (s.Rules.TryGetValue(Key(k[0], k[1]), out r) && r.Enabled && IsMode(r.Mode))
                    {
                        route.Mode = r.Mode;
                        route.Reason = "กฎ (" + ChannelLabel(r.SalesChannel) + " × " + TypeLabel(r.ChannelType) + ") = " + ModeLabel(r.Mode);
                        return route;
                    }
                }
                route.Reason = "ไม่มีกฎที่ตรง (หรือกฎถูกปิด) — ออกเอกสารทันที";
            }
            catch (Exception ex)
            {
                route.Mode = ModeImmediate;
                route.Reason = "อ่านนโยบายไม่ได้ (" + ex.Message + ") — ออกเอกสารทันที";
            }
            return route;
        }

        /// <summary>
        /// ชนิดการชำระเงินของแหล่งเงิน: Account_Paid_How.Channel_Type (PHASE19_20) ถ้าจัดไว้ ไม่งั้นเดาจากชื่อ
        /// ด้วยกฎเดียวกับ PaymentChannelCatalog.InferType · ไม่พบแหล่งเงิน = OTHER
        /// </summary>
        public static string ChannelTypeOf(string conn, int paidHowId)
        {
            try { return ChannelTypeOf(Load(conn), paidHowId); }
            catch { return "OTHER"; }
        }

        /// <summary>ล้าง cache — เรียกหลังบันทึกหน้าตั้งค่า (มีผลกับ worker process นี้ทันที, process อื่นภายใน 60 วิ)</summary>
        public static void Invalidate()
        {
            lock (_lock) { _cache.Clear(); }
        }

        // ── API เสริม (หน้า POS / หน้าตั้งค่า) ──────────────────────────────────

        /// <summary>
        /// ขายที่ไม่ได้ออกเอกสารรายใบบน NextAcc (สรุปรายวัน/ปิด) → POS ต้องพิมพ์ใบกำกับภาษีอย่างย่อเลขเรียงในเครื่อง
        /// ไม่มีค่าตั้ง = ต้องพิมพ์ (ปลอดภัยทางกฎหมาย ม.86)
        /// </summary>
        public static bool LocalSlipRequired(string conn)
        {
            try { return Flag(Load(conn), CfgLocalSlipRequired, true); }
            catch { return true; }
        }

        /// <summary>ส่งอีเมล PDF "[INV]" ของ TakeTime แม้มีเอกสารบน NextAcc แล้วหรือไม่ (ไม่มีค่าตั้ง = ไม่ส่ง)</summary>
        public static bool SendLocalInvoiceEmail(string conn)
        {
            try { return Flag(Load(conn), CfgSendLocalInvoiceEmail, false); }
            catch { return false; }
        }

        /// <summary>มีตาราง Sales_Doc_Policy แล้วหรือยัง (รัน PHASE19_29)</summary>
        public static bool TableReady(string conn)
        {
            try { return Load(conn).TableReady; } catch { return false; }
        }

        /// <summary>มีคอลัมน์ Account_Paid_How.Sales_Doc_Mode แล้วหรือยัง</summary>
        public static bool OverrideColumnReady(string conn)
        {
            try { return Load(conn).OverrideColumnReady; } catch { return false; }
        }

        /// <summary>กฎทั้งหมดในตาราง (สำเนา) — หน้าตั้งค่า</summary>
        public static IList<Rule> GetRules(string conn)
        {
            var list = new List<Rule>();
            try { list.AddRange(Load(conn).Rules.Values); } catch { }
            return list;
        }

        /// <summary>แหล่งเงินทั้งหมด (สำเนา) เรียงตาม ID — หน้าตั้งค่า</summary>
        public static IList<PaidHowInfo> GetPaidHows(string conn)
        {
            var list = new List<PaidHowInfo>();
            try { list.AddRange(Load(conn).PaidHows.Values); } catch { }
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        public static bool IsMode(string m)
        {
            return m == ModeImmediate || m == ModeDailyRollup || m == ModeOff;
        }

        /// <summary>ค่าที่รับจากฟอร์ม → โหมดที่ถูกต้อง หรือ null</summary>
        public static string NormalizeMode(string m)
        {
            string u = (m ?? "").Trim().ToUpperInvariant();
            return IsMode(u) ? u : null;
        }

        public static string NormalizeChannel(string ch)
        {
            string u = (ch ?? "").Trim().ToUpperInvariant();
            return u.Length == 0 ? Any : u;
        }

        public static string NormalizeType(string t)
        {
            string u = (t ?? "").Trim().ToUpperInvariant();
            if (u == Any) return Any;
            return Array.IndexOf(ChannelTypes, u) >= 0 ? u : "OTHER";
        }

        public static string ModeLabel(string mode)
        {
            switch (mode)
            {
                case ModeImmediate: return "ออกเอกสารทันที";
                case ModeDailyRollup: return "สรุปรายวัน";
                case ModeOff: return "ไม่ส่งเข้าบัญชี";
                default: return mode ?? "";
            }
        }

        public static string ChannelLabel(string ch)
        {
            switch ((ch ?? "").ToUpperInvariant())
            {
                case Any: return "ทุกช่องทาง";
                case ChPos: return "ขายหน้าร้าน (POS)";
                case ChRoomService: return "รูมเซอร์วิส";
                case ChActivity: return "กิจกรรม";
                case ChAmenity: return "ของใช้/บริการเสริม";
                case ChFrontdeskGateway: return "จุดรับเงินออนไลน์หน้าเคาน์เตอร์";
                case ChDamage: return "ค่าเสียหาย";
                case ChReservation: return "ใบเสร็จการจอง";
                default: return ch ?? "";
            }
        }

        public static string TypeLabel(string t)
        {
            switch ((t ?? "").ToUpperInvariant())
            {
                case Any: return "ทุกชนิด";
                case "CASH": return "เงินสด";
                case "TRANSFER": return "โอนเงิน";
                case "QR": return "QR / พร้อมเพย์";
                case "CARD": return "บัตร";
                case "GATEWAY_OTHER": return "เกตเวย์อื่น ๆ";
                case "DIRECTOR_LOAN": return "ทดรองกรรมการ";
                case "OTA": return "OTA";
                case "OTHER": return "อื่น ๆ";
                default: return t ?? "";
            }
        }

        // ── ภายใน ───────────────────────────────────────────────────────────────

        private static string Key(string ch, string type)
        {
            return (ch ?? Any).Trim().ToUpperInvariant() + "|" + (type ?? Any).Trim().ToUpperInvariant();
        }

        private static bool Flag(Snapshot s, string key, bool defaultValue)
        {
            string v;
            if (s == null || !s.Config.TryGetValue(key, out v) || string.IsNullOrWhiteSpace(v)) return defaultValue;
            v = v.Trim();
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static string ChannelTypeOf(Snapshot s, int paidHowId)
        {
            PaidHowInfo ph;
            if (s != null && paidHowId > 0 && s.PaidHows.TryGetValue(paidHowId, out ph) && !string.IsNullOrEmpty(ph.ChannelType))
                return ph.ChannelType;
            return "OTHER";
        }

        private static string InferTypeSafe(string name)
        {
            try
            {
                string provider;
                return NormalizeType(Take_Time_BangPhra.Payments.PaymentChannelCatalog.InferType(name, out provider));
            }
            catch
            {
                // กฎสำรองแบบย่อ (กรณีอ่านค่าตั้งเกตเวย์ไม่ได้) — ลำดับเดียวกับ InferType
                string n = (name ?? "").Trim();
                string u = n.ToUpperInvariant();
                if (n.Contains("ทดรอง") || n.Contains("กรรมการ")) return "DIRECTOR_LOAN";
                if (n.Contains("เงินสด") || u.Contains("CASH")) return "CASH";
                if (n.Contains("โอน") || n.Contains("ธนาคาร") || n.Contains("พร้อมเพย์") || u.Contains("BANK") || u.Contains("TRANSFER")) return "TRANSFER";
                if (n.Contains("บัตร") || u.Contains("CARD") || u.Contains("EDC")) return "CARD";
                return "OTHER";
            }
        }

        private static Snapshot Load(string conn)
        {
            string cs = conn ?? "";
            lock (_lock)
            {
                Snapshot cached;
                if (_cache.TryGetValue(cs, out cached) && (DateTime.UtcNow - cached.LoadedAt) < CacheTtl)
                    return cached;
            }

            var s = new Snapshot { LoadedAt = DateTime.UtcNow };
            if (cs.Length > 0)
            {
                try
                {
                    using (var con = new SqlConnection(cs))
                    {
                        con.Open();

                        var probe = Query(con,
                            @"SELECT CASE WHEN OBJECT_ID('dbo.Sales_Doc_Policy', 'U') IS NULL THEN 0 ELSE 1 END AS T,
                                     CASE WHEN COL_LENGTH('dbo.Account_Paid_How', 'Sales_Doc_Mode') IS NULL THEN 0 ELSE 1 END AS O,
                                     CASE WHEN COL_LENGTH('dbo.Account_Paid_How', 'Channel_Type') IS NULL THEN 0 ELSE 1 END AS C,
                                     CASE WHEN OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NULL THEN 0 ELSE 1 END AS K");
                        if (probe.Rows.Count > 0)
                        {
                            s.TableReady = Convert.ToInt32(probe.Rows[0]["T"]) == 1;
                            s.OverrideColumnReady = Convert.ToInt32(probe.Rows[0]["O"]) == 1;
                            s.ChannelTypeColumnReady = Convert.ToInt32(probe.Rows[0]["C"]) == 1;
                            if (Convert.ToInt32(probe.Rows[0]["K"]) == 1)
                            {
                                var cfg = Query(con,
                                    "SELECT ConfigKey, ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey LIKE 'Sales[_]Doc[_]%'");
                                foreach (DataRow r in cfg.Rows)
                                {
                                    string k = Convert.ToString(r["ConfigKey"]);
                                    if (!string.IsNullOrEmpty(k)) s.Config[k] = r["ConfigValue"] == DBNull.Value ? "" : Convert.ToString(r["ConfigValue"]);
                                }
                            }
                        }

                        if (s.TableReady)
                        {
                            var rules = Query(con,
                                "SELECT ID, Sales_Channel, Channel_Type, Doc_Mode, Is_Enabled, Note, Updated_By, Updated_At FROM dbo.Sales_Doc_Policy");
                            foreach (DataRow r in rules.Rows)
                            {
                                var rule = new Rule
                                {
                                    Id = Convert.ToInt32(r["ID"]),
                                    SalesChannel = NormalizeChannel(Convert.ToString(r["Sales_Channel"])),
                                    ChannelType = (Convert.ToString(r["Channel_Type"]) ?? "").Trim().ToUpperInvariant(),
                                    Mode = NormalizeMode(Convert.ToString(r["Doc_Mode"])),
                                    Enabled = r["Is_Enabled"] != DBNull.Value && Convert.ToBoolean(r["Is_Enabled"]),
                                    Note = r["Note"] == DBNull.Value ? null : Convert.ToString(r["Note"]),
                                    UpdatedBy = r["Updated_By"] == DBNull.Value ? null : Convert.ToString(r["Updated_By"]),
                                    UpdatedAt = r["Updated_At"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["Updated_At"])
                                };
                                if (rule.ChannelType.Length == 0) rule.ChannelType = Any;
                                s.Rules[Key(rule.SalesChannel, rule.ChannelType)] = rule;
                            }
                        }

                        string phSql = "SELECT ID, Paid_How, Status"
                            + (s.ChannelTypeColumnReady ? ", Channel_Type" : "")
                            + (s.OverrideColumnReady ? ", Sales_Doc_Mode" : "")
                            + " FROM dbo.Account_Paid_How";
                        DataTable phs = null;
                        try { phs = Query(con, phSql); } catch { phs = null; }
                        if (phs != null)
                        {
                            foreach (DataRow r in phs.Rows)
                            {
                                int id;
                                if (!int.TryParse(Convert.ToString(r["ID"]), out id)) continue;
                                string name = r["Paid_How"] == DBNull.Value ? "" : Convert.ToString(r["Paid_How"]);
                                string st = r["Status"] == DBNull.Value ? "" : Convert.ToString(r["Status"]);
                                string ct = s.ChannelTypeColumnReady && r["Channel_Type"] != DBNull.Value
                                    ? Convert.ToString(r["Channel_Type"]).Trim().ToUpperInvariant() : "";
                                bool classified = ct.Length > 0 && Array.IndexOf(ChannelTypes, ct) >= 0;
                                s.PaidHows[id] = new PaidHowInfo
                                {
                                    Id = id,
                                    Name = name,
                                    Active = st.Equals("True", StringComparison.OrdinalIgnoreCase) || st == "1",
                                    ChannelType = classified ? ct : InferTypeSafe(name),
                                    Classified = classified,
                                    OverrideMode = s.OverrideColumnReady && r["Sales_Doc_Mode"] != DBNull.Value
                                        ? NormalizeMode(Convert.ToString(r["Sales_Doc_Mode"])) : null
                                };
                            }
                        }
                    }
                }
                catch
                {
                    // ฐานข้อมูลไม่พร้อม → snapshot ว่าง = นโยบายปิด (พฤติกรรมเดิม) — cache สั้นกว่าปกติ
                    s.LoadedAt = DateTime.UtcNow - CacheTtl + TimeSpan.FromSeconds(10);
                }
            }

            lock (_lock) { _cache[cs] = s; }
            return s;
        }

        private static DataTable Query(SqlConnection con, string sql)
        {
            var dt = new DataTable();
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.CommandTimeout = 15;
                using (var da = new SqlDataAdapter(cmd)) da.Fill(dt);
            }
            return dt;
        }
    }
}
