using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
// X-Api-Key authentication - no Bearer token needed
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// HTTP client for Nexaacc Accounting API with X-Api-Key authentication
    /// and retry logic.
    /// </summary>
    public class AccountingApiClient
    {
        private readonly AccountingConfig _config;
        private readonly code _code = new code();
        private readonly string _connectionString;
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateFormatString = "yyyy-MM-ddTHH:mm:ss",
            NullValueHandling = NullValueHandling.Ignore
        };

        private static HttpClient _httpClient;
        private static readonly object _httpClientLock = new object();

        private const int MaxRetries = 4;
        private static readonly int[] RetryDelaysMs = { 1000, 3000, 9000, 20000 };

        // DNS health tracking — avoid hammering an unresolvable domain on every queue item
        private static DateTime _dnsFailedUntil = DateTime.MinValue;
        private static string _lastDnsError = null;
        private static readonly object _dnsLock = new object();
        private static readonly TimeSpan DnsCooldownPeriod = TimeSpan.FromMinutes(2);

        // Auth failure tracking — avoid hammering API with an invalid key on every queue item
        private static DateTime _authFailedUntil = DateTime.MinValue;
        private static string _lastAuthError = null;
        private static readonly object _authLock = new object();
        private static readonly TimeSpan AuthCooldownPeriod = TimeSpan.FromMinutes(5);

        // Static constructor ensures TLS 1.2 is enabled even before Application_Start.
        // Critical for .NET Framework apps where default protocol may be SSL3/TLS1.0.
        static AccountingApiClient()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
                    | SecurityProtocolType.Tls11
                    | SecurityProtocolType.Tls;
                ServicePointManager.DefaultConnectionLimit = 100;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.UseNagleAlgorithm = false;
            }
            catch { }
        }

        public AccountingApiClient()
        {
            _config = new AccountingConfig();
            _connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
            EnsureHttpClient();
        }

        public AccountingApiClient(AccountingConfig config, string connectionString)
        {
            _config = config;
            _connectionString = connectionString;
            EnsureHttpClient();
        }

        private void EnsureHttpClient()
        {
            if (_httpClient == null)
            {
                lock (_httpClientLock)
                {
                    if (_httpClient == null)
                    {
                        var handler = new HttpClientHandler
                        {
                            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                        };

                        // Use longer timeout for better tolerance of slow network
                        int timeoutSec = _config.TimeoutSeconds > 0 ? _config.TimeoutSeconds : 60;

                        _httpClient = new HttpClient(handler)
                        {
                            Timeout = TimeSpan.FromSeconds(timeoutSec)
                        };
                        _httpClient.DefaultRequestHeaders.ConnectionClose = false;
                    }
                }
            }
        }

        // ──────────────────────────────────────────────
        // Authentication (X-Api-Key header)
        // ──────────────────────────────────────────────

        private void EnsureApiKeyConfigured()
        {
            if (string.IsNullOrEmpty(_config.ApiKey))
                throw new Exception("Accounting API key is not configured. กรุณาตั้งค่า API Key ในหน้า Accounting Integration Settings");
        }

        // ──────────────────────────────────────────────
        // HTTP Methods with Retry
        // ──────────────────────────────────────────────

        public async Task<T> GetAsync<T>(string path)
        {
            return await ExecuteWithRetryAsync<T>(HttpMethod.Get, path, null);
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body)
        {
            var json = JsonConvert.SerializeObject(body, _jsonSettings);
            return await ExecuteWithRetryAsync<TResponse>(HttpMethod.Post, path, json);
        }

        public async Task<TResponse> PutAsync<TRequest, TResponse>(string path, TRequest body)
        {
            var json = JsonConvert.SerializeObject(body, _jsonSettings);
            return await ExecuteWithRetryAsync<TResponse>(HttpMethod.Put, path, json);
        }

        public async Task PostActionAsync(string path)
        {
            await ExecuteWithRetryAsync<object>(HttpMethod.Post, path, null);
        }

        /// <summary>
        /// Pre-flight DNS check — resolves the API hostname once before retry loop.
        /// Prevents wasting all retry attempts on an unresolvable domain.
        /// </summary>
        private void ValidateDnsResolution(string baseUrl)
        {
            // Check cooldown: if DNS recently failed, throw immediately to avoid flooding
            lock (_dnsLock)
            {
                if (DateTime.Now < _dnsFailedUntil)
                {
                    throw new DnsResolutionException(
                        $"DNS resolution skipped (cooldown until {_dnsFailedUntil:HH:mm:ss}): {_lastDnsError}");
                }
            }

            Uri uri;
            try
            {
                uri = new Uri(baseUrl);
            }
            catch (UriFormatException ex)
            {
                throw new DnsResolutionException($"Invalid Base URL format '{baseUrl}': {ex.Message}");
            }

            try
            {
                var addresses = Dns.GetHostAddresses(uri.Host);
                if (addresses == null || addresses.Length == 0)
                {
                    SetDnsFailed($"DNS resolved but returned no addresses for '{uri.Host}'");
                    throw new DnsResolutionException($"DNS resolved but returned no IP addresses for '{uri.Host}'. ตรวจสอบ Nexaacc_BaseUrl ใน Accounting_Integration_Config");
                }

                // DNS OK — clear any previous failure state
                lock (_dnsLock)
                {
                    _dnsFailedUntil = DateTime.MinValue;
                    _lastDnsError = null;
                }
            }
            catch (DnsResolutionException) { throw; }
            catch (SocketException ex)
            {
                string msg = $"Cannot resolve hostname '{uri.Host}': {ex.Message}. ตรวจสอบ Nexaacc_BaseUrl ใน Accounting_Integration_Config — โดเมนอาจผิดหรือ DNS server ไม่พร้อม";
                SetDnsFailed(msg);
                throw new DnsResolutionException(msg);
            }
            catch (Exception ex)
            {
                string msg = $"DNS check failed for '{uri.Host}': {ex.Message}";
                SetDnsFailed(msg);
                throw new DnsResolutionException(msg);
            }
        }

        private void SetDnsFailed(string errorMessage)
        {
            lock (_dnsLock)
            {
                _dnsFailedUntil = DateTime.Now.Add(DnsCooldownPeriod);
                _lastDnsError = errorMessage;
            }
        }

        private void SetAuthFailed(string errorMessage)
        {
            lock (_authLock)
            {
                _authFailedUntil = DateTime.Now.Add(AuthCooldownPeriod);
                _lastAuthError = errorMessage;
            }
        }

        /// <summary>
        /// Clear auth failure state — call when API key is updated.
        /// </summary>
        public static void ClearAuthFailure()
        {
            lock (_authLock)
            {
                _authFailedUntil = DateTime.MinValue;
                _lastAuthError = null;
            }
        }

        /// <summary>
        /// Check if auth is in cooldown due to recent 401 failure.
        /// </summary>
        private void CheckAuthCooldown()
        {
            lock (_authLock)
            {
                if (DateTime.Now < _authFailedUntil)
                {
                    throw new AuthenticationFailedException(
                        $"API Key authentication skipped (cooldown until {_authFailedUntil:HH:mm:ss}): {_lastAuthError}");
                }
            }
        }

        /// <summary>
        /// Quick health check — validates DNS + auth status without full API call.
        /// Used by queue processor to skip entire batch when API is unreachable or auth is failing.
        /// </summary>
        public bool IsApiReachable(out string errorDetail)
        {
            errorDetail = null;
            if (string.IsNullOrEmpty(_config.BaseUrl))
            {
                errorDetail = "Base URL not configured";
                return false;
            }
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                errorDetail = "API Key not configured";
                return false;
            }

            try
            {
                ValidateDnsResolution(_config.BaseUrl);
            }
            catch (DnsResolutionException ex)
            {
                errorDetail = ex.Message;
                return false;
            }

            // Check auth cooldown
            lock (_authLock)
            {
                if (DateTime.Now < _authFailedUntil)
                {
                    string keyPreview = _config.ApiKey.Length > 8
                        ? _config.ApiKey.Substring(0, 4) + "****" + _config.ApiKey.Substring(_config.ApiKey.Length - 4)
                        : "****";
                    errorDetail = $"API Key invalid (cooldown until {_authFailedUntil:HH:mm:ss}). Key: {keyPreview}. กรุณาตรวจสอบ API Key ใน Accounting Integration Settings — {_lastAuthError}";
                    return false;
                }
            }

            return true;
        }

        private async Task<T> ExecuteWithRetryAsync<T>(HttpMethod method, string path, string jsonBody)
        {
            EnsureApiKeyConfigured();

            if (string.IsNullOrEmpty(_config.BaseUrl))
                throw new Exception("Accounting Base URL is not configured.");

            // Pre-flight checks — fail fast on known infrastructure issues
            ValidateDnsResolution(_config.BaseUrl);
            CheckAuthCooldown();

            var url = $"{_config.BaseUrl.TrimEnd('/')}{path}";
            Exception lastException = null;

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(RetryDelaysMs[Math.Min(attempt - 1, RetryDelaysMs.Length - 1)]).ConfigureAwait(false);
                }

                try
                {
                    // Create a fresh request on each attempt (HttpRequestMessage cannot be reused)
                    var request = new HttpRequestMessage(method, url);
                    request.Headers.Add("X-Api-Key", _config.ApiKey);
                    request.Headers.Add("Accept", "application/json");

                    if (jsonBody != null)
                    {
                        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    }

                    var startTime = DateTime.Now;
                    var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                    var durationMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    // Log the API call
                    LogApiCall(method.Method, path, jsonBody, responseBody, (int)response.StatusCode, response.IsSuccessStatusCode, durationMs);

                    if (!response.IsSuccessStatusCode)
                    {
                        int status = (int)response.StatusCode;

                        // 401/403 = auth failure — set cooldown to stop all queue items from retrying
                        if (status == 401 || status == 403)
                        {
                            string keyPreview = _config.ApiKey.Length > 8
                                ? _config.ApiKey.Substring(0, 4) + "****" + _config.ApiKey.Substring(_config.ApiKey.Length - 4)
                                : "****";
                            string authMsg = $"API Key authentication failed ({status}): {responseBody}. Key: {keyPreview} (length={_config.ApiKey.Length}). กรุณาตรวจสอบ API Key ใน Accounting Integration Settings";
                            SetAuthFailed(authMsg);
                            throw new AuthenticationFailedException(authMsg);
                        }

                        // Don't retry other 4xx errors (except 408 Timeout and 429 Too Many Requests)
                        if (status >= 400 && status < 500 && status != 408 && status != 429)
                        {
                            throw new AccountingApiException(
                                $"API error {response.StatusCode}: {responseBody}",
                                status, responseBody);
                        }

                        // 408/429/5xx errors: retry
                        throw new HttpRequestException($"Server error {response.StatusCode}: {responseBody}");
                    }

                    if (typeof(T) == typeof(object) && string.IsNullOrWhiteSpace(responseBody))
                        return default(T);

                    return JsonConvert.DeserializeObject<T>(responseBody, _jsonSettings);
                }
                catch (AccountingApiException)
                {
                    throw; // Don't retry client errors
                }
                catch (TaskCanceledException tcEx)
                {
                    lastException = new Exception($"Request timeout after {_httpClient.Timeout.TotalSeconds}s: {tcEx.Message}", tcEx);
                    if (attempt >= MaxRetries) break;
                }
                catch (HttpRequestException httpEx)
                {
                    string innerMsg = httpEx.InnerException?.Message ?? "";

                    // DNS resolution failure mid-request — set cooldown and stop retrying
                    if (innerMsg.Contains("remote name could not be resolved") ||
                        innerMsg.Contains("No such host") ||
                        httpEx.Message.Contains("remote name could not be resolved"))
                    {
                        SetDnsFailed($"DNS failure during request: {innerMsg}");
                        throw new DnsResolutionException(
                            $"DNS resolution failed for API host: {innerMsg}. ตรวจสอบ Nexaacc_BaseUrl ใน Accounting_Integration_Config",
                            httpEx);
                    }

                    lastException = new Exception($"Network error: {httpEx.Message} | Inner: {innerMsg}", httpEx);
                    if (attempt >= MaxRetries) break;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt >= MaxRetries)
                        break;
                }
            }

            // Build detailed diagnostic message
            string diagnostic = lastException?.Message ?? "unknown error";
            if (lastException?.InnerException != null)
                diagnostic += $" | Inner: {lastException.InnerException.Message}";

            throw new Exception(
                $"Accounting API call failed after {MaxRetries + 1} attempts to {method.Method} {path}: {diagnostic}",
                lastException);
        }

        // ──────────────────────────────────────────────
        // Convenience Methods (Company-scoped)
        // ──────────────────────────────────────────────

        private string CompanyPath => $"/api/companies/{_config.CompanyId}";

        // Chart of Accounts (AccountingController)
        public async Task<ApiResponse<List<AccountResponse>>> GetAccountsAsync()
        {
            return await GetAsync<ApiResponse<List<AccountResponse>>>($"{CompanyPath}/accounting/accounts");
        }

        // Journal Entries (AccountingController)
        public async Task<ApiResponse<JournalEntryResponse>> CreateJournalAsync(CreateJournalEntryRequest journal)
        {
            // Validate lines before sending to avoid cryptic API errors
            if (journal.Lines == null || journal.Lines.Count == 0)
                throw new ArgumentException("Journal entry must have at least 1 debit/credit line.");

            if (!journal.Lines.Any(l => l.DebitAmount > 0) || !journal.Lines.Any(l => l.CreditAmount > 0))
                throw new ArgumentException("Journal entry must have at least 1 debit line and 1 credit line.");

            return await PostAsync<CreateJournalEntryRequest, ApiResponse<JournalEntryResponse>>(
                $"{CompanyPath}/accounting/journals", journal);
        }

        public async Task<ApiResponse<JournalEntryResponse>> PostJournalAsync(Guid entryId)
        {
            return await PostAsync<object, ApiResponse<JournalEntryResponse>>(
                $"{CompanyPath}/accounting/journals/{entryId}/post", null);
        }

        public async Task VoidJournalAsync(Guid entryId)
        {
            await PostActionAsync($"{CompanyPath}/accounting/journals/{entryId}/void");
        }

        // Documents (DocumentController)
        public async Task<ApiResponse<DocumentResponse>> CreateDocumentAsync(CreateDocumentRequest document)
        {
            return await PostAsync<CreateDocumentRequest, ApiResponse<DocumentResponse>>(
                $"{CompanyPath}/document", document);
        }

        public async Task<ApiResponse<DocumentResponse>> ApproveDocumentAsync(Guid documentId)
        {
            return await PostAsync<object, ApiResponse<DocumentResponse>>(
                $"{CompanyPath}/document/{documentId}/approve", null);
        }

        public async Task VoidDocumentAsync(Guid documentId)
        {
            await PostActionAsync($"{CompanyPath}/document/{documentId}/void");
        }

        // Contacts (อยู่ภายใต้ DocumentController ใน Nexaacc)
        public async Task<ApiResponse<ContactResponse>> CreateContactAsync(CreateContactRequest contact)
        {
            return await PostAsync<CreateContactRequest, ApiResponse<ContactResponse>>(
                $"{CompanyPath}/document/contacts", contact);
        }

        public async Task<ApiResponse<ContactResponse>> UpdateContactAsync(Guid contactId, UpdateContactRequest contact)
        {
            return await PutAsync<UpdateContactRequest, ApiResponse<ContactResponse>>(
                $"{CompanyPath}/document/contacts/{contactId}", contact);
        }

        // Payments (อยู่ภายใต้ DocumentController ใน Nexaacc)
        public async Task<ApiResponse<PaymentResponse>> CreatePaymentAsync(CreatePaymentRequest payment)
        {
            return await PostAsync<CreatePaymentRequest, ApiResponse<PaymentResponse>>(
                $"{CompanyPath}/document/payments", payment);
        }

        // Integration Endpoints — สร้างเอกสาร+บันทึกบัญชีในคำสั่งเดียว
        // ใช้ company-scoped path เพราะ Nexaacc ApiKeyMiddleware validate API key ต่อ company
        public async Task<ApiResponse<IntegrationDocumentResponse>> CreateInvoiceAsync(CreateIntegrationInvoiceRequest invoice)
        {
            if (invoice.Lines == null || invoice.Lines.Count == 0)
                throw new ArgumentException("Invoice must have at least 1 line item.");

            if (!invoice.Lines.Any(l => l.UnitPrice > 0 && l.Quantity > 0))
                throw new ArgumentException("Invoice must have at least 1 line with UnitPrice > 0 and Quantity > 0.");

            return await PostAsync<CreateIntegrationInvoiceRequest, ApiResponse<IntegrationDocumentResponse>>(
                $"{CompanyPath}/integration/invoices", invoice);
        }

        public async Task<ApiResponse<IntegrationDocumentResponse>> CreateExpenseAsync(CreateIntegrationExpenseRequest expense)
        {
            if (expense.Lines == null || expense.Lines.Count == 0)
                throw new ArgumentException("Expense must have at least 1 line item.");

            return await PostAsync<CreateIntegrationExpenseRequest, ApiResponse<IntegrationDocumentResponse>>(
                $"{CompanyPath}/integration/expenses", expense);
        }

        // Products (ProductController)
        public async Task<ApiResponse<ProductResponse>> CreateProductAsync(CreateProductRequest product)
        {
            return await PostAsync<CreateProductRequest, ApiResponse<ProductResponse>>(
                $"{CompanyPath}/product", product);
        }

        public async Task<ApiResponse<ProductResponse>> UpdateProductAsync(Guid productId, UpdateProductRequest product)
        {
            return await PutAsync<UpdateProductRequest, ApiResponse<ProductResponse>>(
                $"{CompanyPath}/product/{productId}", product);
        }

        public async Task<ApiResponse<StockMovementResponse>> AdjustStockAsync(StockAdjustmentRequest adjustment)
        {
            return await PostAsync<StockAdjustmentRequest, ApiResponse<StockMovementResponse>>(
                $"{CompanyPath}/product/stock/adjust", adjustment);
        }

        // ──────────────────────────────────────────────
        // Connection Test
        // ──────────────────────────────────────────────

        /// <summary>
        /// Tests the connection to Nexaacc API by fetching the Chart of Accounts.
        /// Returns a structured result with success/failure and a diagnostic message.
        /// </summary>
        public async Task<ConnectionTestResult> TestConnectionAsync()
        {
            if (string.IsNullOrEmpty(_config.BaseUrl))
                return new ConnectionTestResult(false, "ยังไม่ได้ตั้งค่า Base URL");
            if (string.IsNullOrEmpty(_config.ApiKey))
                return new ConnectionTestResult(false, "ยังไม่ได้ตั้งค่า API Key — กรุณากรอก API Key ในหน้า Accounting Integration Settings");
            if (_config.CompanyId == Guid.Empty)
                return new ConnectionTestResult(false, "ยังไม่ได้ตั้งค่า Company ID");

            // DNS pre-check — catch domain issues before attempting API call
            try
            {
                ValidateDnsResolution(_config.BaseUrl);
            }
            catch (DnsResolutionException ex)
            {
                Uri parsedUri = null;
                try { parsedUri = new Uri(_config.BaseUrl); } catch { }
                string host = parsedUri?.Host ?? _config.BaseUrl;
                return new ConnectionTestResult(false,
                    $"ไม่สามารถ resolve DNS ของ '{host}' ได้\n" +
                    $"Base URL ที่ตั้งค่า: {_config.BaseUrl}\n" +
                    $"ปัญหาที่เป็นไปได้:\n" +
                    $"  1) โดเมนผิด — ตรวจสอบว่าเป็น nexaacc.net หรือ nextacc.net\n" +
                    $"  2) โดเมนหมดอายุ\n" +
                    $"  3) DNS server ของ server ไม่สามารถ resolve ได้\n" +
                    $"Error: {ex.Message}");
            }

            // Clear auth cooldown for test — user may have just updated the key
            ClearAuthFailure();

            string apiKeyPreview = _config.ApiKey.Length > 8
                ? _config.ApiKey.Substring(0, 4) + "****" + _config.ApiKey.Substring(_config.ApiKey.Length - 4)
                : new string('*', _config.ApiKey.Length);
            string targetUrl = $"{_config.BaseUrl}/api/companies/{_config.CompanyId}/accounting/accounts";

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await GetAccountsAsync().ConfigureAwait(false);
                sw.Stop();
                return new ConnectionTestResult(true, $"Nexaacc API เชื่อมต่อสำเร็จ — API Key ใช้งานได้ ({sw.ElapsedMilliseconds}ms)");
            }
            catch (DnsResolutionException ex)
            {
                return new ConnectionTestResult(false,
                    $"DNS resolution failed ระหว่าง API call: {ex.Message}\nURL: {targetUrl}");
            }
            catch (AuthenticationFailedException ex)
            {
                return new ConnectionTestResult(false,
                    $"API Key ไม่ถูกต้องหรือหมดอายุ\n" +
                    $"URL: {targetUrl}\n" +
                    $"API Key: {apiKeyPreview} (ความยาว {_config.ApiKey.Length} ตัวอักษร)\n" +
                    $"กรุณาตรวจสอบ:\n" +
                    $"  1) API Key ถูกต้อง — copy จาก Nexaacc dashboard ใหม่\n" +
                    $"  2) Key ยังไม่หมดอายุ\n" +
                    $"  3) IP ของ server อยู่ใน whitelist\n" +
                    $"  4) การเข้ารหัส (encrypt) API Key ถูกต้อง\n" +
                    $"Error: {ex.Message}", 401);
            }
            catch (AccountingApiException ex) when (ex.StatusCode == 401)
            {
                string detail = !string.IsNullOrEmpty(ex.ResponseBody) ? $" | Response: {ex.ResponseBody}" : "";
                return new ConnectionTestResult(false,
                    $"API Key ไม่ถูกต้องหรือหมดอายุ (401 Unauthorized)\n" +
                    $"URL: {targetUrl}\n" +
                    $"API Key: {apiKeyPreview} (ความยาว {_config.ApiKey.Length} ตัวอักษร)\n" +
                    $"กรุณาตรวจสอบ: 1) API Key ถูกต้อง 2) Key ยังไม่หมดอายุ 3) IP ของ server อยู่ใน whitelist{detail}",
                    ex.StatusCode, ex.ResponseBody);
            }
            catch (AccountingApiException ex) when (ex.StatusCode == 403)
            {
                return new ConnectionTestResult(false,
                    $"API Key ไม่มีสิทธิ์เข้าถึง Company นี้ (403 Forbidden) — ตรวจสอบว่า API Key ตรงกับ Company ID: {_config.CompanyId}",
                    ex.StatusCode, ex.ResponseBody);
            }
            catch (AccountingApiException ex) when (ex.StatusCode == 404)
            {
                return new ConnectionTestResult(false,
                    $"ไม่พบ endpoint (404 Not Found)\nURL: {targetUrl}\nตรวจสอบ Base URL และ Company ID",
                    ex.StatusCode, ex.ResponseBody);
            }
            catch (AccountingApiException ex)
            {
                return new ConnectionTestResult(false,
                    $"Nexaacc API Error ({ex.StatusCode}): {ex.ResponseBody}",
                    ex.StatusCode, ex.ResponseBody);
            }
            catch (Exception ex)
            {
                return new ConnectionTestResult(false, $"เชื่อมต่อไม่ได้: {ex.Message}\nURL: {targetUrl}");
            }
        }

        // ──────────────────────────────────────────────
        // Logging
        // ──────────────────────────────────────────────

        private void LogSync(string action, string detail, string response, bool success)
        {
            try
            {
                _code.Logs(_connectionString, $"ACCOUNTING_SYNC_{action}", $"{detail} | Success={success} | {response}", "SYSTEM");
            }
            catch { }
        }

        private void LogApiCall(string method, string path, string requestBody, string responseBody, int httpStatus, bool success, int durationMs)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@action", $"{method} {path}" },
                    { "@request", TruncateForLog(requestBody) },
                    { "@response", TruncateForLog(responseBody) },
                    { "@httpStatus", httpStatus },
                    { "@success", success },
                    { "@durationMs", durationMs }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO Accounting_Sync_Log (Queue_ID, Action, Request_Payload, Response_Payload, HTTP_Status, Success, Duration_Ms, Created_Date)
                      VALUES (NULL, @action, @request, @response, @httpStatus, @success, @durationMs, GETDATE())",
                    parameters);
            }
            catch { }
        }

        private string TruncateForLog(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return value.Length > 4000 ? value.Substring(0, 4000) : value;
        }
    }

    /// <summary>
    /// Exception for non-retryable API errors (4xx).
    /// </summary>
    public class AccountingApiException : Exception
    {
        public int StatusCode { get; }
        public string ResponseBody { get; }

        public AccountingApiException(string message, int statusCode, string responseBody)
            : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }

    /// <summary>
    /// DNS resolution failure — the API hostname cannot be resolved.
    /// Non-retryable at the item level; indicates a configuration or infrastructure issue.
    /// </summary>
    public class DnsResolutionException : Exception
    {
        public DnsResolutionException(string message) : base(message) { }
        public DnsResolutionException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// API Key authentication failure (401/403).
    /// Non-retryable — all queue items will fail with the same error until the key is fixed.
    /// </summary>
    public class AuthenticationFailedException : Exception
    {
        public AuthenticationFailedException(string message) : base(message) { }
    }

    /// <summary>
    /// Result of a connection test to the Nexaacc API.
    /// </summary>
    public class ConnectionTestResult
    {
        public bool Success { get; }
        public string Message { get; }
        public int? HttpStatus { get; }
        public string ResponseBody { get; }

        public ConnectionTestResult(bool success, string message, int? httpStatus = null, string responseBody = null)
        {
            Success = success;
            Message = message;
            HttpStatus = httpStatus;
            ResponseBody = responseBody;
        }
    }
}
