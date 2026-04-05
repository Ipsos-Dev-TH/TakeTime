using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
// X-Api-Key authentication - no Bearer token needed
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

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
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        private static HttpClient _httpClient;

        private const int MaxRetries = 3;
        private static readonly int[] RetryDelaysMs = { 1000, 3000, 9000 };

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
                _httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds > 0 ? _config.TimeoutSeconds : 30)
                };
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
            var json = _serializer.Serialize(body);
            return await ExecuteWithRetryAsync<TResponse>(HttpMethod.Post, path, json);
        }

        public async Task<TResponse> PutAsync<TRequest, TResponse>(string path, TRequest body)
        {
            var json = _serializer.Serialize(body);
            return await ExecuteWithRetryAsync<TResponse>(HttpMethod.Put, path, json);
        }

        public async Task PostActionAsync(string path)
        {
            await ExecuteWithRetryAsync<object>(HttpMethod.Post, path, null);
        }

        private async Task<T> ExecuteWithRetryAsync<T>(HttpMethod method, string path, string jsonBody)
        {
            EnsureApiKeyConfigured();

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
                    var request = new HttpRequestMessage(method, url);
                    request.Headers.Add("X-Api-Key", _config.ApiKey);

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
                        // Don't retry 4xx errors
                        if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                        {
                            throw new AccountingApiException(
                                $"API error {response.StatusCode}: {responseBody}",
                                (int)response.StatusCode, responseBody);
                        }

                        // 5xx errors: retry
                        throw new HttpRequestException($"Server error {response.StatusCode}: {responseBody}");
                    }

                    if (typeof(T) == typeof(object) && string.IsNullOrWhiteSpace(responseBody))
                        return default(T);

                    return _serializer.Deserialize<T>(responseBody);
                }
                catch (AccountingApiException)
                {
                    throw; // Don't retry client errors
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt >= MaxRetries)
                        break;
                }
            }

            throw new Exception($"Accounting API call failed after {MaxRetries + 1} attempts: {lastException?.Message}", lastException);
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

            // Build diagnostic info for error messages
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
