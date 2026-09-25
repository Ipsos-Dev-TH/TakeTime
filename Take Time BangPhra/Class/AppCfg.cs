using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

/// <summary>
/// ศูนย์กลางการอ่านค่าตั้งค่าระบบ — อ่านจากตาราง System_Config ก่อน ถ้าไม่มีค่อยใช้ Web.config
/// (ตั้งใจวางไว้ที่ global namespace เพื่อให้ทุกไฟล์เรียกใช้ได้โดยไม่ต้องเพิ่ม using)
///
/// เหตุผลที่ทำ fallback: ระบบที่ยังไม่ได้ตั้งค่าใน DB จะทำงานเหมือนเดิมทุกประการ
/// และการแก้ค่าผ่านหน้าเว็บมีผลทันทีโดยไม่ต้องแก้ Web.config (ซึ่งทำให้ App Pool รีสตาร์ท
/// ผู้ใช้ทุกคนหลุด session)
/// </summary>
public static class AppCfg
{
    private static readonly object _lock = new object();
    private static Dictionary<string, string> _cache;      // ค่าที่ตั้งใน DB (ถอดรหัสแล้ว)
    private static DateTime _loadedAt = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    /// <summary>คีย์ที่เก็บแบบเข้ารหัสใน DB</summary>
    private static readonly HashSet<string> SecretKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "linechannelaccesstokentaketime",
        "TelegramTokenTakeTime",
        "Email_Password_From",
        "GooglePlacesApiKey",
        "TaxInvoiceApiKey"
    };

    private static string ConnStr =>
        ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString;

    /// <summary>อ่านค่า — DB ก่อน ถ้าไม่มี/ว่าง ใช้ Web.config, ไม่มีทั้งคู่คืน defaultValue</summary>
    public static string Get(string key, string defaultValue = null)
    {
        try
        {
            var map = GetCache();
            if (map != null && map.TryGetValue(key, out string v) && !string.IsNullOrEmpty(v))
                return v;
        }
        catch { /* DB มีปัญหา → ตกไปใช้ Web.config */ }

        string web = ConfigurationManager.AppSettings[key];
        return string.IsNullOrEmpty(web) ? defaultValue : web;
    }

    public static int GetInt(string key, int defaultValue)
    {
        return int.TryParse(Get(key), out int v) ? v : defaultValue;
    }

    public static bool GetBool(string key, bool defaultValue)
    {
        string s = Get(key);
        if (string.IsNullOrEmpty(s)) return defaultValue;
        if (bool.TryParse(s, out bool b)) return b;
        return s == "1" || s.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSecret(string key) => SecretKeys.Contains(key);

    /// <summary>ค่าถูกตั้งใน DB แล้วหรือยัง (ใช้แสดงสถานะในหน้าตั้งค่า)</summary>
    public static bool IsSetInDb(string key)
    {
        try
        {
            var map = GetCache();
            return map != null && map.TryGetValue(key, out string v) && !string.IsNullOrEmpty(v);
        }
        catch { return false; }
    }

    /// <summary>บันทึกค่า (เข้ารหัสอัตโนมัติถ้าเป็นคีย์ลับ) — ส่ง null/ว่าง = กลับไปใช้ Web.config</summary>
    public static void Set(string key, string value, short? adminId = null)
    {
        string stored = value;
        if (!string.IsNullOrEmpty(value) && IsSecret(key))
        {
            try { stored = new Take_Time_BangPhra.code().Crypt(value); }
            catch { stored = value; }
        }

        using (var con = new SqlConnection(ConnStr))
        {
            con.Open();
            using (var cmd = new SqlCommand(@"
                IF EXISTS (SELECT 1 FROM System_Config WHERE ConfigKey = @k)
                    UPDATE System_Config
                       SET ConfigValue = @v, ModifiedDate = GETDATE(), ModifiedBy_AdminID = @by
                     WHERE ConfigKey = @k;
                ELSE
                    INSERT INTO System_Config (ConfigKey, ConfigValue, ModifiedDate, ModifiedBy_AdminID)
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

    public static void Invalidate()
    {
        lock (_lock) { _cache = null; _loadedAt = DateTime.MinValue; }
    }

    /// <summary>รายการตั้งค่าทั้งหมดพร้อม metadata (สำหรับวาดหน้าตั้งค่า)</summary>
    public static DataTable GetAllForUi()
    {
        var dt = new DataTable();
        using (var con = new SqlConnection(ConnStr))
        using (var da = new SqlDataAdapter(
            @"SELECT ConfigKey, ConfigValue, Category, DisplayName, Description,
                     IsSecret, InputType, DisplayOrder, ModifiedDate
                FROM System_Config
               ORDER BY Category, DisplayOrder, ConfigKey", con))
            da.Fill(dt);
        return dt;
    }

    // ── cache ────────────────────────────────────────────────────────────────
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
                        "SELECT ConfigKey, ConfigValue FROM System_Config WHERE ConfigValue IS NOT NULL", con))
                    using (var rd = cmd.ExecuteReader())
                    {
                        var helper = new Take_Time_BangPhra.code();
                        while (rd.Read())
                        {
                            string k = rd[0]?.ToString();
                            string v = rd[1] == DBNull.Value ? null : rd[1].ToString();
                            if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(v)) continue;
                            if (SecretKeys.Contains(k))
                            {
                                try { v = helper.Derypt(v); } catch { }
                            }
                            map[k] = v;
                        }
                    }
                }
            }
            catch
            {
                // ตาราง System_Config ยังไม่มี (ยังไม่รัน migration) → ใช้ Web.config ล้วน
            }

            _cache = map;
            _loadedAt = DateTime.UtcNow;
            return map;
        }
    }
}
