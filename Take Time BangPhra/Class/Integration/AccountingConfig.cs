using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// Configuration for Nexaacc Accounting System integration.
    /// Reads from Accounting_Integration_Config table and Web.config.
    /// </summary>
    public class AccountingConfig
    {
        private readonly code _code = new code();
        private readonly string _connectionString;
        private Dictionary<string, string> _configCache;
        private DateTime _cacheExpiry = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public AccountingConfig()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        }

        public AccountingConfig(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Core settings
        public string BaseUrl => SanitizeBaseUrl(GetConfig("Nexaacc_BaseUrl", ""));
        public string RawBaseUrl => GetConfig("Nexaacc_BaseUrl", "");
        public string ApiKey => _code.Derypt(GetConfig("Nexaacc_ApiKey_Encrypted", ""));

        /// <summary>true = key เป็น Integration Key (ขึ้นต้น "int_") — ใช้กับ /api/integration/* ได้
        /// แต่ใช้กับ {company}/* (E-Tax, WHT, chart) ไม่ได้ (ต้องใช้ API Key "acc_")</summary>
        public bool IsIntegrationKey => (ApiKey ?? "").StartsWith("int_", StringComparison.OrdinalIgnoreCase);
        public Guid CompanyId => Guid.TryParse(GetConfig("Nexaacc_CompanyId", ""), out var id) ? id : Guid.Empty;
        public bool Enabled => GetConfig("Nexaacc_Enabled", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        public int SyncIntervalSeconds => int.TryParse(GetConfig("Nexaacc_SyncInterval_Sec", "30"), out var v) ? v : 30;
        public int MaxRetries => int.TryParse(GetConfig("Nexaacc_MaxRetries", "5"), out var v) ? v : 5;
        public int TimeoutSeconds => int.TryParse(GetConfig("Nexaacc_TimeoutSec", "30"), out var v) ? v : 30;

        /// <summary>
        /// JOURNAL_ONLY = บันทึกสมุดบัญชีอย่างเดียว (debit/credit journal entries)
        /// DOCUMENT = สร้างเอกสาร (ใบกำกับภาษี/ใบสำคัญจ่าย) + ระบบสร้าง journal ให้อัตโนมัติ
        /// </summary>
        public string SyncMode => GetConfig("Nexaacc_SyncMode", "DOCUMENT");
        public bool IsDocumentMode => SyncMode.Equals("DOCUMENT", StringComparison.OrdinalIgnoreCase);

        // ──────────────────────────────────────────────
        // Per-document-type sync mode
        // LOCAL = ไม่ส่ง NextAcc (ใช้ระบบของ TakeTime)
        // JOURNAL_ONLY = สร้าง journal entry ใน NextAcc
        // DOCUMENT = สร้างเอกสารเต็มรูปแบบใน NextAcc
        // ──────────────────────────────────────────────

        public string ReceiptSyncMode => GetConfig("Nexaacc_SyncMode_Receipt", SyncMode);
        public string VoucherSyncMode => GetConfig("Nexaacc_SyncMode_Voucher", SyncMode);
        public string PayrollSyncMode => GetConfig("Nexaacc_SyncMode_Payroll", SyncMode);

        public bool IsReceiptLocal => ReceiptSyncMode.Equals("LOCAL", StringComparison.OrdinalIgnoreCase);
        public bool IsReceiptDocumentMode => ReceiptSyncMode.Equals("DOCUMENT", StringComparison.OrdinalIgnoreCase);
        public bool IsVoucherDocumentMode => VoucherSyncMode.Equals("DOCUMENT", StringComparison.OrdinalIgnoreCase);
        public bool IsVoucherLocal => VoucherSyncMode.Equals("LOCAL", StringComparison.OrdinalIgnoreCase);
        public bool IsPayrollDocumentMode => PayrollSyncMode.Equals("DOCUMENT", StringComparison.OrdinalIgnoreCase);
        public bool IsPayrollLocal => PayrollSyncMode.Equals("LOCAL", StringComparison.OrdinalIgnoreCase);

        public bool AttachFiles => GetConfig("Nexaacc_AttachFiles", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

        // ──────────────────────────────────────────────
        // Deposit VAT recognition timing (จุดความรับผิด VAT ของเงินมัดจำ)
        // ──────────────────────────────────────────────

        /// <summary>
        /// เมื่อไหร่ที่ VAT ของเงินมัดจำถูกรับรู้และยิงเข้าบัญชีภาษีขาย (OUTPUT_VAT):
        ///   CHECKOUT (default) — รับรู้ VAT ตอนตัดมัดจำเป็นรายได้ (เช็คเอาท์)
        ///   RECEIPT  — รับรู้ VAT ทันทีที่รับเงินมัดจำ (ตาม ป.รัษฎากร ม.78/1:
        ///              บริการ จุดความรับผิด VAT = วันรับชำระเงิน)
        /// </summary>
        public string DepositVatRecognition => GetConfig("Deposit_Vat_Recognition", "CHECKOUT");

        /// <summary>true = แยก VAT ออกจากมัดจำตั้งแต่ตอนรับเงิน (ยิงเข้า OUTPUT_VAT ทันที)</summary>
        public bool IsDepositVatAtReceipt => DepositVatRecognition.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// (โหมด RECEIPT เท่านั้น) true = พัก VAT ของมัดจำไว้ที่ "ภาษีขายรอเรียกเก็บ/รอรับรู้"
        /// (mapping <c>OUTPUT_VAT_DEFERRED</c>, ปกติ 21913) ตอนรับเงิน แล้วโอนกลับเข้า
        /// "ภาษีขาย" (<c>OUTPUT_VAT</c>, 21911) ตอน check-out → VAT จะไม่ขึ้น ภ.พ.30 จนกว่า
        /// จะ realize ตอนรับรู้รายได้. false (ค่าเริ่มต้น) = เข้า OUTPUT_VAT ทันที (พฤติกรรมเดิม).
        /// ⚠ ต้อง map บัญชี OUTPUT_VAT_DEFERRED ก่อนเปิด ไม่งั้นระบบจะ fallback กลับไป OUTPUT_VAT.
        /// </summary>
        public bool IsDepositOutputVatDeferred => GetConfig("Deposit_Defer_Output_Vat", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Deposit_Defer_Output_Vat", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        // ──────────────────────────────────────────────
        // E-Tax Invoice automation
        // ──────────────────────────────────────────────

        /// <summary>สร้าง E-Tax Invoice อัตโนมัติเมื่อสร้างใบเสร็จในระบบ NextAcc สำเร็จ</summary>
        public bool IsEtaxAutoGenerate => GetConfig("Etax_AutoGenerate", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Etax_AutoGenerate", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>ลงนาม E-Tax อัตโนมัติหลังสร้าง</summary>
        public bool IsEtaxAutoSign => GetConfig("Etax_AutoSign", "1").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Etax_AutoSign", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>ส่งให้กรมสรรพากรอัตโนมัติหลังลงนาม</summary>
        public bool IsEtaxAutoSubmit => GetConfig("Etax_AutoSubmit", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Etax_AutoSubmit", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>ส่งอีเมลใบกำกับภาษีอิเล็กทรอนิกส์ให้ลูกค้าอัตโนมัติเมื่อ E-Tax พร้อมแล้ว</summary>
        public bool IsEtaxAutoSendEmail => GetConfig("Etax_AutoSendEmail", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Etax_AutoSendEmail", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>หัวข้ออีเมล E-Tax (รองรับ {ReceiptNumber}, {GuestName}, {CompanyName})</summary>
        public string EtaxEmailSubject => GetConfig("Etax_EmailSubject",
            "ใบกำกับภาษีอิเล็กทรอนิกส์ {ReceiptNumber}");

        /// <summary>เนื้อหาอีเมล E-Tax (รองรับ {ReceiptNumber}, {GuestName}, {Amount}, {Date})</summary>
        public string EtaxEmailBody => GetConfig("Etax_EmailBody",
            "เรียน {GuestName}\n\nกรุณาดาวน์โหลดใบกำกับภาษีอิเล็กทรอนิกส์ {ReceiptNumber} จากเอกสารแนบ\n\nขอบคุณที่ใช้บริการ");

        /// <summary>แนบ PDF E-Tax ในอีเมล</summary>
        public bool EtaxEmailAttachPdf => GetConfig("Etax_EmailAttachPdf", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>แนบ XML E-Tax ในอีเมล (ลูกค้าธุรกิจอาจต้องการ)</summary>
        public bool EtaxEmailAttachXml => GetConfig("Etax_EmailAttachXml", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// ถ้าส่งอีเมลผ่าน NextAcc ไม่สำเร็จ → ดาวน์โหลด PDF/XML จาก URL ของ NextAcc แล้วส่งผ่าน SMTP ของ TakeTime
        /// </summary>
        public bool EtaxEmailFallback => GetConfig("Etax_EmailFallback", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// บังคับให้ใช้ SMTP ของ TakeTime เท่านั้น ข้าม NextAcc — สำหรับกรณี NextAcc email service ปิดอยู่
        /// </summary>
        public bool EtaxEmailLocalOnly => GetConfig("Etax_EmailLocalOnly", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// ตั้งค่า API ครบแล้วหรือยัง (Base URL, API Key, Company ID)
        /// ไม่รวม Enabled — เพราะ "ตั้งค่าครบ" กับ "เปิด sync" เป็นคนละเรื่อง
        /// </summary>
        public bool IsConfigured => !string.IsNullOrEmpty(BaseUrl) && IsValidUrl(BaseUrl) && !string.IsNullOrEmpty(ApiKey) && CompanyId != Guid.Empty;

        /// <summary>
        /// พร้อม sync อัตโนมัติ = ตั้งค่าครบ + เปิดใช้งาน
        /// </summary>
        public bool IsReadyToSync => IsConfigured && Enabled;

        /// <summary>
        /// Validate that a URL is well-formed and uses HTTPS.
        /// </summary>
        private static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            Uri uri;
            return Uri.TryCreate(url, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }

        /// <summary>
        /// Validate the Base URL and return diagnostic messages.
        /// Returns null if valid; error message if invalid.
        /// </summary>
        public string ValidateBaseUrl()
        {
            string raw = RawBaseUrl;
            if (string.IsNullOrWhiteSpace(raw))
                return "Nexaacc_BaseUrl ยังไม่ได้ตั้งค่า";

            Uri uri;
            if (!Uri.TryCreate(raw, UriKind.Absolute, out uri))
                return $"Nexaacc_BaseUrl format ไม่ถูกต้อง: '{raw}'";

            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                return $"Nexaacc_BaseUrl ต้องเป็น http:// หรือ https:// — ปัจจุบันเป็น: '{uri.Scheme}'";

            if (string.IsNullOrEmpty(uri.Host) || uri.Host == "localhost")
                return $"Nexaacc_BaseUrl host ไม่ถูกต้อง: '{uri.Host}'";

            return null;
        }

        /// <summary>
        /// Strip any trailing API path segments from the base URL to prevent
        /// path duplication when CompanyPath is appended by AccountingApiClient.
        /// Handles: /api/companies/{guid}..., /api/, /api
        /// </summary>
        private static string SanitizeBaseUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;

            // Strip /api/companies/... first (most specific)
            int idx = url.IndexOf("/api/companies", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                url = url.Substring(0, idx);
            }
            else
            {
                // Strip trailing /api to prevent /api/api/companies duplication
                string trimmed = url.TrimEnd('/');
                if (trimmed.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                    url = trimmed.Substring(0, trimmed.Length - 4);
            }

            return url.TrimEnd('/');
        }

        private string GetConfig(string key, string defaultValue)
        {
            EnsureCache();
            return _configCache.ContainsKey(key) ? _configCache[key] : defaultValue;
        }

        private void EnsureCache()
        {
            if (_configCache != null && DateTime.Now < _cacheExpiry)
                return;

            _configCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ConfigKey, ConfigValue FROM Accounting_Integration_Config WHERE ConfigValue IS NOT NULL",
                    null);

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        string configKey = row["ConfigKey"]?.ToString();
                        string configValue = row["ConfigValue"]?.ToString();
                        if (!string.IsNullOrEmpty(configKey))
                        {
                            _configCache[configKey] = configValue ?? "";
                        }
                    }
                }
            }
            catch
            {
                // Config table may not exist yet; use defaults
            }

            _cacheExpiry = DateTime.Now.Add(CacheDuration);
        }

        /// <summary>
        /// Save or update a config value.
        /// </summary>
        public void SetConfig(string key, string value)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@key", key },
                { "@value", value }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"IF EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = @key)
                    UPDATE Accounting_Integration_Config SET ConfigValue = @value, Updated_Date = GETDATE() WHERE ConfigKey = @key
                  ELSE
                    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES (@key, @value)",
                parameters);

            // Invalidate cache
            _cacheExpiry = DateTime.MinValue;
        }
    }
}
