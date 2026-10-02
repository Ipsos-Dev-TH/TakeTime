using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// สัตว์เลี้ยงเข้าพัก — ค่าตั้งกลาง + ตัวช่วยคำนวณค่าบริการ (PHASE19 migration 23)
    ///
    /// · สวิตช์ Pet_Enabled / หน่วยคิดเงิน Pet_Fee_Unit (NIGHT = ต่อตัวต่อคืน, STAY = ต่อตัวต่อการเข้าพัก)
    ///   เก็บใน Booking_Policy_Config ผ่าน <see cref="BookingPolicy"/> (ข้อความนโยบาย = BookingPolicy.KeyPet)
    /// · รายห้อง: Accommodation.Pet_Allowed / Pet_Max_Per_Room / Pet_Fee_Per_Pet
    /// · ยังไม่รัน migration 23 (ไม่มีคอลัมน์) → <see cref="Enabled"/> = false เสมอ หน้าจองเหมือนเดิมทุกอย่าง
    /// </summary>
    public static class PetStay
    {
        public const string KeyEnabled = "Pet_Enabled";
        public const string KeyFeeUnit = "Pet_Fee_Unit";
        public const string UnitNight = "NIGHT";
        public const string UnitStay = "STAY";

        /// <summary>Notes ของแถวค่าสัตว์เลี้ยงใน Reservation_Product_Charges ขึ้นต้นด้วยค่านี้ (+ ":NIGHT"/":STAY")</summary>
        public const string ChargeNotePrefix = "PET_FEE";

        private static readonly object _lock = new object();
        private static bool? _schemaReady;
        private static DateTime _schemaCheckedAt = DateTime.MinValue;

        /// <summary>เปิดฟีเจอร์ (สวิตช์เปิด + ฐานข้อมูลพร้อม)</summary>
        public static bool Enabled
        {
            get
            {
                try
                {
                    string v = BookingPolicy.GetSetting(KeyEnabled);
                    if (!(v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))) return false;
                    return SchemaReady;
                }
                catch { return false; }
            }
        }

        /// <summary>หน่วยคิดค่าบริการ — ค่าเริ่มต้น NIGHT (ต่อตัวต่อคืน)</summary>
        public static string FeeUnit
        {
            get
            {
                string v = (BookingPolicy.GetSetting(KeyFeeUnit) ?? "").Trim().ToUpperInvariant();
                return v == UnitStay ? UnitStay : UnitNight;
            }
        }

        public static string UnitLabel(string unit)
        {
            return unit == UnitStay ? "ต่อตัว/การเข้าพัก" : "ต่อตัว/คืน";
        }

        /// <summary>คอลัมน์จาก migration 23 ครบไหม (cache 5 นาที)</summary>
        public static bool SchemaReady
        {
            get
            {
                lock (_lock)
                {
                    if (_schemaReady.HasValue && (DateTime.UtcNow - _schemaCheckedAt).TotalMinutes < 5)
                        return _schemaReady.Value;
                    bool ok = false;
                    try
                    {
                        var cs = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"];
                        if (cs != null)
                        {
                            using (var con = new SqlConnection(cs.ConnectionString))
                            using (var cmd = new SqlCommand(@"
                                SELECT CASE WHEN COL_LENGTH('dbo.Accommodation', 'Pet_Allowed') IS NOT NULL
                                             AND COL_LENGTH('dbo.Accommodation', 'Pet_Max_Per_Room') IS NOT NULL
                                             AND COL_LENGTH('dbo.Accommodation', 'Pet_Fee_Per_Pet') IS NOT NULL
                                             AND COL_LENGTH('dbo.Reservation_Accommodation', 'Room_Pet_Count') IS NOT NULL
                                             AND COL_LENGTH('dbo.Reservation', 'Pet_Count') IS NOT NULL
                                             AND COL_LENGTH('dbo.Reservation', 'Pet_Policy_Accepted_At') IS NOT NULL
                                             AND COL_LENGTH('dbo.Reservation_Product_Charges', 'Pet_Accommodation_ID') IS NOT NULL
                                            THEN 1 ELSE 0 END", con))
                            {
                                con.Open();
                                object o = cmd.ExecuteScalar();
                                ok = o != null && o != DBNull.Value && Convert.ToInt32(o) == 1;
                            }
                        }
                    }
                    catch { ok = false; }
                    _schemaReady = ok;
                    _schemaCheckedAt = DateTime.UtcNow;
                    return ok;
                }
            }
        }

        public static void InvalidateSchema()
        {
            lock (_lock) { _schemaReady = null; }
        }

        /// <summary>ค่าตั้งของห้องหนึ่งห้อง (จากแถว Accommodation — SELECT * มีคอลัมน์ครบเมื่อรัน migration แล้ว)</summary>
        public struct RoomRule
        {
            public bool Allowed;
            public int MaxPets;
            public decimal FeePerPet;
        }

        public static RoomRule ReadRoom(DataRow r)
        {
            var rule = new RoomRule();
            if (r == null || r.Table == null) return rule;
            try
            {
                DataColumnCollection cols = r.Table.Columns;
                if (cols.Contains("Pet_Allowed") && r["Pet_Allowed"] != DBNull.Value)
                {
                    string a = r["Pet_Allowed"].ToString();
                    rule.Allowed = a == "1" || string.Equals(a, "True", StringComparison.OrdinalIgnoreCase);
                }
                if (cols.Contains("Pet_Max_Per_Room") && r["Pet_Max_Per_Room"] != DBNull.Value)
                    rule.MaxPets = Convert.ToInt32(r["Pet_Max_Per_Room"]);
                if (cols.Contains("Pet_Fee_Per_Pet") && r["Pet_Fee_Per_Pet"] != DBNull.Value)
                    rule.FeePerPet = Convert.ToDecimal(r["Pet_Fee_Per_Pet"]);
            }
            catch { }
            // รับสัตว์เลี้ยงแต่ไม่ได้ตั้งจำนวน → ถือว่าได้ 1 ตัว (หน้าตั้งค่าบังคับ ≥ 1 อยู่แล้ว)
            if (rule.Allowed && rule.MaxPets < 1) rule.MaxPets = 1;
            if (rule.FeePerPet < 0) rule.FeePerPet = 0;
            return rule;
        }

        /// <summary>จำนวนหน่วยที่คิดเงิน: NIGHT = ตัว × คืน, STAY = ตัว</summary>
        public static decimal BillableQuantity(int pets, int nights, string unit)
        {
            if (pets <= 0) return 0m;
            if (unit == UnitStay) return pets;
            return pets * (decimal)Math.Max(1, nights);
        }

        public static decimal LineTotal(int pets, int nights, string unit, decimal feePerPet)
        {
            return Math.Round(BillableQuantity(pets, nights, unit) * feePerPet, 2, MidpointRounding.AwayFromZero);
        }

        public static string Money(decimal v)
        {
            return v.ToString("N2", CultureInfo.InvariantCulture);
        }
    }
}
