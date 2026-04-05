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
        public string BaseUrl => GetConfig("Nexaacc_BaseUrl", "");
        public string Email => GetConfig("Nexaacc_Email", "");
        public string Password => _code.Derypt(GetConfig("Nexaacc_Password_Encrypted", ""));
        public Guid CompanyId => Guid.TryParse(GetConfig("Nexaacc_CompanyId", ""), out var id) ? id : Guid.Empty;
        public bool Enabled => GetConfig("Nexaacc_Enabled", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        public int SyncIntervalSeconds => int.TryParse(GetConfig("Nexaacc_SyncInterval_Sec", "30"), out var v) ? v : 30;
        public int MaxRetries => int.TryParse(GetConfig("Nexaacc_MaxRetries", "5"), out var v) ? v : 5;
        public int TimeoutSeconds => int.TryParse(GetConfig("Nexaacc_TimeoutSec", "30"), out var v) ? v : 30;

        public bool IsConfigured => !string.IsNullOrEmpty(BaseUrl) && CompanyId != Guid.Empty && Enabled;

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
