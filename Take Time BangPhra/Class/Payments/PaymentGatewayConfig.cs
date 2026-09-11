using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace Take_Time_BangPhra.Payments
{
    /// <summary>
    /// ค่าตั้งของระบบรับชำระเงินออนไลน์ — อ่านจากตาราง Payment_Gateway_Config
    ///
    /// หลักการเดียวกับ <see cref="AppCfg"/>: cache สั้น ๆ (30 วิ) แก้จากหน้าเว็บแล้วมีผลทันที
    /// ไม่ต้องแตะ Web.config และไม่ทำให้ App Pool รีสตาร์ท
    ///
    /// ⚠ สำคัญที่สุด: ถ้าตารางยังไม่มี (ยังไม่ได้รัน migration) หรือสวิตช์ปิดอยู่
    /// ทุก property จะคืนค่าที่แปลว่า "ปิด" ⇒ ระบบเดิมทำงานเหมือนเดิมทุกประการ
    /// </summary>
    public static class PaymentGatewayConfig
    {
        private static readonly object _lock = new object();
        private static Dictionary<string, string> _cache;
        private static DateTime _loadedAt = DateTime.MinValue;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
        private static bool _tableMissing;

        private static string ConnStr =>
            ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString;

        // ── อ่านค่าดิบ ────────────────────────────────────────────────────────

        public static string Get(string key, string defaultValue = null)
        {
            try
            {
                var map = GetCache();
                string v;
                if (map != null && map.TryGetValue(key, out v) && !string.IsNullOrEmpty(v)) return v;
            }
            catch { }
            return defaultValue;
        }

        public static int GetInt(string key, int defaultValue)
        {
            int v;
            return int.TryParse(Get(key), out v) ? v : defaultValue;
        }

        public static decimal GetDecimal(string key, decimal defaultValue)
        {
            decimal v;
            return decimal.TryParse(Get(key), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out v) ? v : defaultValue;
        }

        public static bool GetBool(string key, bool defaultValue)
        {
            string s = Get(key);
            if (string.IsNullOrEmpty(s)) return defaultValue;
            bool b;
            if (bool.TryParse(s, out b)) return b;
            return s == "1" || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
                            || s.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        // ── สวิตช์หลัก ────────────────────────────────────────────────────────

        /// <summary>
        /// ระบบชำระเงินออนไลน์เปิดอยู่ไหม — ต้องผ่านทั้ง Feature flag และค่าตั้งในตาราง
        /// ปิดอยู่ = หน้าเดิมทุกหน้าทำงานเหมือนเดิม ไม่มีตัวเลือกใหม่โผล่
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try
                {
                    if (!Feature.On("OnlinePayment")) return false;
                    if (_tableMissing) return false;
                    return GetBool("Payment_Enabled", false);
                }
                catch { return false; }
            }
        }

        /// <summary>เกตเวย์ Payso พร้อมใช้ไหม (เปิด + มีค่าที่จำเป็นครบ)</summary>
        public static bool IsPaysoReady
        {
            get
            {
                if (!IsEnabled) return false;
                if (!GetBool("Payso_Enabled", false)) return false;
                if (string.IsNullOrEmpty(BaseUrl)) return false;
                // ไม่มีกุญแจเลย = ยังตั้งค่าไม่เสร็จ (ยกเว้นตั้งใจใช้ลายเซ็นล้วน)
                if (string.IsNullOrEmpty(ApiKey) && string.IsNullOrEmpty(SecretKey)) return false;
                return true;
            }
        }

        // ── ผู้ให้บริการที่ใช้อยู่ (สลับได้จากหน้าตั้งค่า ไม่ต้อง build ใหม่) ────

        public const string ProviderOmise = "OMISE";
        public const string ProviderPayso = "PAYSO";

        /// <summary>เกตเวย์ที่เลือกใช้อยู่ — OMISE (ค่าเริ่มต้น) หรือ PAYSO</summary>
        public static string ActiveProvider
        {
            get
            {
                string p = (Get("Payment_Provider", ProviderOmise) ?? ProviderOmise).Trim().ToUpperInvariant();
                return p == ProviderPayso ? ProviderPayso : ProviderOmise;
            }
        }

        /// <summary>เกตเวย์ที่เลือกอยู่พร้อมใช้งานจริงไหม</summary>
        public static bool IsGatewayReady
        {
            get
            {
                if (!IsEnabled) return false;
                if (ActiveProvider == ProviderPayso) return IsPaysoReady;
                return GetBool("Omise_Enabled", false) && !string.IsNullOrEmpty(Get("Omise_SecretKey", ""));
            }
        }

        // ── เปิด/ปิดรายช่องทางรับเงิน ────────────────────────────────────────
        // แต่ละจุดที่รับเงินในระบบ (จอง/กิจกรรม/POS/รูมเซอร์วิส/เบิกของ/เช็คเอาท์)
        // ปิดช่องไหน ช่องนั้นก็ไม่เสนอทางจ่ายออนไลน์ — ที่เหลือทำงานตามเดิม
        public static bool ChannelEnabled(string sourceType)
        {
            if (string.IsNullOrEmpty(sourceType)) return true;
            return GetBool("Payment_Channel_" + sourceType.Trim().ToUpperInvariant(), true);
        }

        public static bool IsSandbox =>
            !string.Equals(Get("Payso_Mode", "SANDBOX"), "PRODUCTION", StringComparison.OrdinalIgnoreCase);

        public static string BaseUrl =>
            (IsSandbox ? Get("Payso_BaseUrl_Sandbox") : Get("Payso_BaseUrl_Production"))?.TrimEnd('/');

        public static string MerchantId => Get("Payso_MerchantId", "");
        public static string ApiKey => Get("Payso_ApiKey", "");
        public static string SecretKey => Get("Payso_SecretKey", "");
        public static string WebhookSecret
        {
            get
            {
                string s = Get("Payso_Webhook_Secret");
                return string.IsNullOrEmpty(s) ? SecretKey : s;
            }
        }

        public static int TimeoutSeconds => Math.Max(5, GetInt("Payso_Timeout_Seconds", 30));
        public static int ExpiryMinutes => Math.Max(1, GetInt("Payment_Expiry_Minutes", 30));
        public static decimal MinAmount => GetDecimal("Payment_Min_Amount", 20m);
        public static decimal MaxAmount => GetDecimal("Payment_Max_Amount", 0m);
        public static decimal CardSurchargePercent => GetDecimal("Payment_Card_Surcharge_Pct", 0m);
        public static bool AutoApply => GetBool("Payment_Auto_Apply", true);
        public static bool NotifyStaff => GetBool("Payment_Notify_Staff", true);

        public static bool WebhookVerify => GetBool("Payso_Webhook_Verify", true);
        public static string WebhookSignatureHeader => Get("Payso_Webhook_Sig_Header", "X-Signature");
        public static string WebhookIpAllow => Get("Payso_Webhook_Ip_Allow", "");
        public static bool PollEnabled => GetBool("Payso_Poll_Enabled", true);
        public static int PollMinutes => Math.Max(1, GetInt("Payso_Poll_Minutes", 3));

        // ── วิธีชำระที่เปิดให้ลูกค้าเห็น ──────────────────────────────────────

        public const string MethodManualQr = "MANUAL_QR";
        public const string MethodCard = "CARD";
        public const string MethodQr = "QR";
        public const string MethodInstallment = "INSTALLMENT";

        /// <summary>
        /// วิธีชำระที่ใช้ได้จริงกับยอดนี้ — เรียงตามที่ตั้งไว้
        /// วิธีที่ต้องใช้เกตเวย์จะถูกคัดออกเองถ้าเกตเวย์ยังไม่พร้อม/ยอดไม่เข้าเกณฑ์
        /// </summary>
        public static List<string> AvailableMethods(decimal amount)
        {
            var result = new List<string>();
            string raw = Get("Payment_Methods_Enabled", MethodManualQr) ?? MethodManualQr;

            foreach (string part in raw.Split(','))
            {
                string m = part.Trim().ToUpperInvariant();
                if (m.Length == 0 || result.Contains(m)) continue;

                if (m == MethodManualQr) { result.Add(m); continue; }

                // วิธีที่ต้องผ่านเกตเวย์
                // ⚠ เดิมเช็ค IsPaysoReady ตายตัว — พอสลับมาใช้ Omise ทุกวิธีที่ผ่านเกตเวย์
                // ถูกกรองทิ้งหมด เหลือแต่ MANUAL_QR (อาการ: "วิธีชำระเงินนี้ใช้ไม่ได้กับยอด …")
                if (!IsGatewayReady) continue;
                if (amount > 0)
                {
                    if (MinAmount > 0 && amount < MinAmount) continue;
                    if (MaxAmount > 0 && amount > MaxAmount) continue;
                }
                result.Add(m);
            }
            return result;
        }

        /// <summary>
        /// เหตุผลจริงที่วิธีชำระนี้ใช้ไม่ได้ — คืน null ถ้าใช้ได้
        /// (เดิมตอบว่า "ใช้ไม่ได้กับยอด X" ทุกกรณี ทั้งที่ยอดมักไม่ใช่ปัญหา)
        /// </summary>
        public static string DescribeMethodUnavailable(string method, decimal amount)
        {
            string m = (method ?? "").Trim().ToUpperInvariant();
            if (m.Length == 0) return "ไม่ได้ระบุวิธีชำระเงิน";

            if (AvailableMethods(amount).Contains(m)) return null;

            if (m != MethodManualQr && !IsGatewayReady)
                return ActiveProvider == ProviderOmise
                    ? "เกตเวย์ Omise ยังไม่พร้อม — ต้องเปิด \"เปิดใช้เกตเวย์ Omise\" และใส่ Secret Key"
                    : "เกตเวย์ Payso ยังไม่พร้อม — ต้องเปิดใช้งานและใส่ Base URL + กุญแจ";

            string raw = (Get("Payment_Methods_Enabled", MethodManualQr) ?? "").ToUpperInvariant();
            bool listed = false;
            foreach (string part in raw.Split(','))
                if (part.Trim() == m) { listed = true; break; }
            if (!listed)
                return "ยังไม่ได้เปิดวิธี \"" + MethodName(m) + "\" — เพิ่ม " + m
                     + " ในช่อง \"วิธีชำระที่เปิดให้ลูกค้าเลือก\" (Payment_Methods_Enabled)";

            if (amount > 0 && MinAmount > 0 && amount < MinAmount)
                return "ยอด " + amount.ToString("N2") + " บาท ต่ำกว่าขั้นต่ำที่ตั้งไว้ (" + MinAmount.ToString("N2") + ")";
            if (amount > 0 && MaxAmount > 0 && amount > MaxAmount)
                return "ยอด " + amount.ToString("N2") + " บาท เกินเพดานที่ตั้งไว้ (" + MaxAmount.ToString("N2") + ")";

            return "วิธีชำระเงินนี้ใช้ไม่ได้ในขณะนี้";
        }

        public static string DefaultMethod(decimal amount)
        {
            var avail = AvailableMethods(amount);
            string want = (Get("Payment_Default_Method", MethodManualQr) ?? "").Trim().ToUpperInvariant();
            if (avail.Contains(want)) return want;
            return avail.Count > 0 ? avail[0] : MethodManualQr;
        }

        /// <summary>ชื่อไทยของวิธีชำระ (ใช้ทั้งหน้าลูกค้าและรายงาน)</summary>
        public static string MethodName(string method)
        {
            switch ((method ?? "").ToUpperInvariant())
            {
                case MethodManualQr: return "สแกน QR / โอนเงิน แล้วแนบสลิป";
                case MethodCard: return "บัตรเครดิต / เดบิต";
                case MethodQr: return "QR พร้อมเพย์ (ตัดยอดอัตโนมัติ)";
                case MethodInstallment: return "ผ่อนชำระผ่านบัตร";
                default: return method ?? "";
            }
        }

        public static string MethodIcon(string method)
        {
            switch ((method ?? "").ToUpperInvariant())
            {
                case MethodManualQr: return "fa-qrcode";
                case MethodCard: return "fa-credit-card";
                case MethodQr: return "fa-bolt";
                case MethodInstallment: return "fa-calendar-alt";
                default: return "fa-money-bill";
            }
        }

        /// <summary>ยอดที่ลูกค้าต้องจ่ายจริงหลังบวกค่าธรรมเนียมบัตร (ถ้าตั้งไว้)</summary>
        public static decimal SurchargeFor(string method, decimal amount)
        {
            decimal pct = CardSurchargePercent;
            if (pct <= 0) return 0m;
            string m = (method ?? "").ToUpperInvariant();
            if (m != MethodCard && m != MethodInstallment) return 0m;
            return Math.Round(amount * pct / 100m, 2, MidpointRounding.AwayFromZero);
        }

        // ── เขียนค่า ──────────────────────────────────────────────────────────

        public static void Set(string key, string value, int? adminId = null)
        {
            string stored = value;
            if (!string.IsNullOrEmpty(value) && IsSecretKey(key))
            {
                try { stored = new code().Crypt(value); }
                catch { stored = value; }
            }

            using (var con = new SqlConnection(ConnStr))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
                    IF EXISTS (SELECT 1 FROM Payment_Gateway_Config WHERE Config_Key = @k)
                        UPDATE Payment_Gateway_Config
                           SET Config_Value = @v, Modified_Date = GETDATE(), Modified_By = @by
                         WHERE Config_Key = @k;
                    ELSE
                        INSERT INTO Payment_Gateway_Config (Config_Key, Config_Value, Modified_Date, Modified_By)
                        VALUES (@k, @v, GETDATE(), @by);", con))
                {
                    cmd.Parameters.AddWithValue("@k", key);
                    cmd.Parameters.AddWithValue("@v", string.IsNullOrEmpty(stored) ? (object)DBNull.Value : stored);
                    cmd.Parameters.AddWithValue("@by", (object)adminId ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
            Invalidate();
        }

        public static bool IsSecretKey(string key)
        {
            try
            {
                using (var con = new SqlConnection(ConnStr))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT Is_Secret FROM Payment_Gateway_Config WHERE Config_Key = @k", con))
                    {
                        cmd.Parameters.AddWithValue("@k", key);
                        object o = cmd.ExecuteScalar();
                        return o != null && o != DBNull.Value && Convert.ToBoolean(o);
                    }
                }
            }
            catch { return false; }
        }

        public static void Invalidate()
        {
            lock (_lock) { _cache = null; _loadedAt = DateTime.MinValue; _tableMissing = false; }
        }

        /// <summary>ค่าตั้งทั้งหมดพร้อม metadata สำหรับวาดหน้าตั้งค่า (ค่าลับถูกปิดบัง)</summary>
        public static DataTable GetAllForUi()
        {
            var dt = new DataTable();
            using (var con = new SqlConnection(ConnStr))
            using (var da = new SqlDataAdapter(
                @"SELECT Config_Key, Config_Value, Is_Secret, Category, Display_Name,
                         [Description], Input_Type, Options, Display_Order, Modified_Date
                    FROM Payment_Gateway_Config
                   ORDER BY Display_Order, Config_Key", con))
                da.Fill(dt);

            // ค่าลับแสดงเป็น "ตั้งค่าแล้ว/ยังไม่ตั้ง" เท่านั้น — ไม่ส่งค่าจริงออกหน้าเว็บ
            foreach (DataRow r in dt.Rows)
            {
                bool secret = r["Is_Secret"] != DBNull.Value && Convert.ToBoolean(r["Is_Secret"]);
                if (!secret) continue;
                string v = r["Config_Value"] == DBNull.Value ? null : r["Config_Value"].ToString();
                r["Config_Value"] = string.IsNullOrEmpty(v) ? "" : "••••••••";
            }
            return dt;
        }

        // ── cache ─────────────────────────────────────────────────────────────
        private static Dictionary<string, string> GetCache()
        {
            lock (_lock)
            {
                if (_cache != null && (DateTime.UtcNow - _loadedAt) < CacheTtl) return _cache;

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string cs = ConnStr;
                if (string.IsNullOrEmpty(cs)) { _cache = map; _loadedAt = DateTime.UtcNow; return map; }

                try
                {
                    using (var con = new SqlConnection(cs))
                    {
                        con.Open();
                        using (var cmd = new SqlCommand(
                            "SELECT Config_Key, Config_Value, Is_Secret FROM Payment_Gateway_Config WHERE Config_Value IS NOT NULL", con))
                        using (var rd = cmd.ExecuteReader())
                        {
                            var helper = new code();
                            while (rd.Read())
                            {
                                string k = rd[0] == DBNull.Value ? null : rd[0].ToString();
                                string v = rd[1] == DBNull.Value ? null : rd[1].ToString();
                                bool secret = rd[2] != DBNull.Value && Convert.ToBoolean(rd[2]);
                                if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(v)) continue;
                                if (secret)
                                {
                                    try { v = helper.Derypt(v); } catch { }
                                }
                                map[k] = v;
                            }
                        }
                    }
                    _tableMissing = false;
                }
                catch
                {
                    // ยังไม่ได้รัน migration → ถือว่าปิดทั้งระบบ ระบบเดิมทำงานต่อได้ปกติ
                    _tableMissing = true;
                }

                _cache = map;
                _loadedAt = DateTime.UtcNow;
                return map;
            }
        }
    }
}
