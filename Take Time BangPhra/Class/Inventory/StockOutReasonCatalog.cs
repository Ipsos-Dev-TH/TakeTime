using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Take_Time_BangPhra.Inventory
{
    /// <summary>เหตุผลการเบิกสินค้า (แถวของ Stock_Out_Reason — PHASE19_30)</summary>
    public sealed class StockOutReason
    {
        public string Code { get; set; }
        public string NameTh { get; set; }
        /// <summary>คีย์ใน Accounting_Account_Mapping ของบัญชีฝั่งเดบิต (ค่าใช้จ่าย/ถอนใช้ส่วนตัว)</summary>
        public string GlMappingKey { get; set; }
        /// <summary>ถือเป็นการขาย ม.77/1(8) → ลงภาษีขาย 7% ของฐาน (ไม่ออกใบกำกับ)</summary>
        public bool DeemedSaleVat { get; set; }
        /// <summary>MARKET = ราคาตลาด (ราคาขายถอด VAT) | COST = ต้นทุน</summary>
        public string VatBase { get; set; }
        public bool RequiresApproval { get; set; }
        public bool RequiresEvidence { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string HelpTh { get; set; }

        public bool IsMarketBase => !string.Equals(VatBase, "COST", StringComparison.OrdinalIgnoreCase);

        /// <summary>เหตุผลกลุ่ม "เสียหาย/ทำลาย" ที่ต้องมีรายงานตามแนวคำสั่ง ป.79/2541</summary>
        public bool IsDestructionReport =>
            Code == "DAMAGE" || Code == "EXPIRED" || Code == "DESTROYED";

        public StockOutReason Clone() => (StockOutReason)MemberwiseClone();
    }

    /// <summary>
    /// แคตตาล็อกเหตุผลการเบิก — อ่านจาก Stock_Out_Reason (cache 5 นาที).
    /// ยังไม่รัน PHASE19_30 → คืนชุดค่าเริ่มต้นในโค้ด (ตรงกับ seed ของ migration) เพื่อให้หน้าเบิกยังใช้งานได้
    /// </summary>
    public static class StockOutReasonCatalog
    {
        /// <summary>คืนสินค้าผู้ขาย — แถวสร้างโดยหน้าคืนสินค้า (W4, ลงบัญชีเอง ref PRT-…): ไม่แสดงในหน้าเบิก, ไม่ลงบัญชีซ้ำ</summary>
        public const string PurchaseReturnCode = "RETURN_TO_VENDOR";

        /// <summary>เหตุผลที่ระบบอื่นเป็นผู้สร้าง/ลงบัญชี (ไม่ให้เลือกเองในหน้าเบิก)</summary>
        public static bool IsSystemReason(string code) =>
            string.Equals(code, PurchaseReturnCode, StringComparison.OrdinalIgnoreCase);

        /// <summary>เหตุผลที่เลือกได้ในหน้าเบิกสินค้า (เปิดใช้ และไม่ใช่เหตุผลของระบบ)</summary>
        public static IList<StockOutReason> ManualSelectable(string conn) =>
            All(conn).Where(r => r.IsActive && !IsSystemReason(r.Code)).ToList();

        private static readonly object _lock = new object();
        private static List<StockOutReason> _cache;
        private static bool _cacheFromDb;
        private static DateTime _loadedAt = DateTime.MinValue;
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

        /// <summary>ทุกเหตุผล (รวมที่ปิดใช้) เรียงตาม Sort_Order</summary>
        public static IList<StockOutReason> All(string conn)
        {
            lock (_lock)
            {
                if (_cache == null || DateTime.Now - _loadedAt > Ttl)
                {
                    bool fromDb;
                    _cache = Load(conn, out fromDb);
                    _cacheFromDb = fromDb;
                    _loadedAt = DateTime.Now;
                }
                return _cache.Select(r => r.Clone()).ToList();
            }
        }

        /// <summary>เฉพาะเหตุผลที่เปิดใช้</summary>
        public static IList<StockOutReason> Active(string conn) =>
            All(conn).Where(r => r.IsActive).ToList();

        public static StockOutReason Get(string conn, string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            return All(conn).FirstOrDefault(r => string.Equals(r.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>true = อ่านจากตาราง Stock_Out_Reason ได้ (รัน PHASE19_30 แล้ว)</summary>
        public static bool IsFromDatabase(string conn)
        {
            All(conn);
            lock (_lock) return _cacheFromDb;
        }

        public static void Invalidate()
        {
            lock (_lock) { _cache = null; _loadedAt = DateTime.MinValue; }
        }

        private static List<StockOutReason> Load(string conn, out bool fromDb)
        {
            fromDb = false;
            try
            {
                var c = new code();
                var dt = c.DatabaseQuerySafe(conn,
                    @"IF OBJECT_ID('dbo.Stock_Out_Reason','U') IS NOT NULL
                        SELECT Code, Name_TH, Gl_Mapping_Key, Deemed_Sale_Vat, Vat_Base, Requires_Approval,
                               Requires_Evidence, Is_Active, Sort_Order, Help_TH
                          FROM dbo.Stock_Out_Reason
                         ORDER BY Sort_Order, Code", null);
                if (dt != null && dt.Columns.Contains("Code"))
                {
                    fromDb = true;
                    var list = new List<StockOutReason>();
                    foreach (DataRow r in dt.Rows)
                    {
                        list.Add(new StockOutReason
                        {
                            Code = Str(r["Code"]),
                            NameTh = Str(r["Name_TH"]),
                            GlMappingKey = Str(r["Gl_Mapping_Key"]),
                            DeemedSaleVat = Bool(r["Deemed_Sale_Vat"]),
                            VatBase = string.IsNullOrWhiteSpace(Str(r["Vat_Base"])) ? "MARKET" : Str(r["Vat_Base"]).ToUpperInvariant(),
                            RequiresApproval = Bool(r["Requires_Approval"]),
                            RequiresEvidence = Bool(r["Requires_Evidence"]),
                            IsActive = Bool(r["Is_Active"]),
                            SortOrder = r["Sort_Order"] == DBNull.Value ? 100 : Convert.ToInt32(r["Sort_Order"]),
                            HelpTh = Str(r["Help_TH"])
                        });
                    }
                    return list;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("StockOutReasonCatalog.Load: " + ex.Message);
            }
            return Defaults();
        }

        /// <summary>ค่าเริ่มต้น (ตรงกับ seed ใน PHASE19_30) — ใช้เมื่อยังไม่มีตาราง</summary>
        public static List<StockOutReason> Defaults()
        {
            Func<string, string, string, bool, string, bool, bool, int, StockOutReason> mk =
                (code, name, gl, deemed, vb, appr, evid, sort) => new StockOutReason
                {
                    Code = code, NameTh = name, GlMappingKey = gl, DeemedSaleVat = deemed, VatBase = vb,
                    RequiresApproval = appr, RequiresEvidence = evid, IsActive = true, SortOrder = sort,
                    HelpTh = "ค่าเริ่มต้นในโค้ด (ยังไม่รัน PHASE19_30) — ให้ผู้ทำบัญชียืนยัน"
                };
            return new List<StockOutReason>
            {
                mk("DAMAGE", "ของแตก/เสียหาย", "INVENTORY_LOSS", false, "COST", true, true, 10),
                mk("EXPIRED", "หมดอายุ/เน่าเสีย", "INVENTORY_LOSS", false, "COST", true, true, 20),
                mk("DESTROYED", "ทำลายตามขั้นตอน (ป.79/2541)", "INVENTORY_LOSS", false, "COST", true, true, 30),
                mk("LOST_THEFT", "สูญหาย/ขาดจากการนับ", "INVENTORY_LOSS", true, "MARKET", true, true, 40),
                mk("PROMO_SAMPLE", "ตัวอย่างสินค้า/ของแจกส่งเสริมการขาย", "PROMOTION_EXPENSE", false, "MARKET", false, false, 50),
                mk("GIFT_WITH_PURCHASE", "ของแถมพร้อมการขาย (ระบุในใบกำกับ)", "COGS", false, "MARKET", false, false, 60),
                mk("GIVEAWAY", "แจกฟรี/ของรางวัลชิงโชค", "PROMOTION_EXPENSE", true, "MARKET", true, false, 70),
                mk("BUSINESS_GIFT", "ของขวัญ/ของชำร่วยตามประเพณี", "ENTERTAINMENT", false, "MARKET", true, false, 80),
                mk("STAFF_MEAL", "อาหารพนักงานระหว่างงาน", "STAFF_WELFARE", false, "MARKET", false, false, 90),
                mk("STAFF_WELFARE", "สวัสดิการพนักงานอื่น", "STAFF_WELFARE", true, "MARKET", true, false, 100),
                mk("OWNER_USE", "หุ้นส่วน/เจ้าของนำไปใช้ส่วนตัว", "OWNER_DRAWINGS", true, "MARKET", true, false, 110),
                mk("COMPLIMENTARY_GUEST", "ให้ลูกค้าฟรี/ชดเชย", "ENTERTAINMENT", true, "MARKET", true, false, 120),
                mk("INTERNAL_OPERATIONS", "ใช้ในการดำเนินงาน (เช่น ของใช้ในห้องพัก)", "OPERATING_SUPPLIES", false, "COST", false, false, 130),
                mk("DONATION", "บริจาค", "DONATION", true, "MARKET", true, true, 140),
                mk("COUNT_ADJUST", "ปรับยอดจากการตรวจนับ (อธิบายสาเหตุได้)", "COGS_ADJUST", false, "COST", true, false, 150),
                mk(PurchaseReturnCode, "คืนสินค้าผู้ขาย", "PURCHASE_RETURNS", false, "COST", false, false, 900),
            };
        }

        private static string Str(object o) => o == null || o == DBNull.Value ? "" : o.ToString();
        private static bool Bool(object o)
        {
            if (o == null || o == DBNull.Value) return false;
            if (o is bool b) return b;
            string s = o.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
