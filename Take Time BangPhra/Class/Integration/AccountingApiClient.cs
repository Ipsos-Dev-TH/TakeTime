using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private AccountingDataMapper _lazyMapper;
        private AccountingDataMapper Mapper => _lazyMapper ?? (_lazyMapper = new AccountingDataMapper(_connectionString));
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

        public async Task DeleteAsync(string path)
        {
            await ExecuteWithRetryAsync<object>(HttpMethod.Delete, path, null);
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
                    // เลือก auth header ตาม endpoint — ห้ามส่งทั้งคู่:
                    //   /api/integration/* → X-Integration-Key เท่านั้น
                    //     (ถ้าส่ง X-Api-Key ไปด้วย ApiKeyMiddleware ของ NextAcc จะหา int_ key
                    //      ใน ApiKey table ไม่เจอ → reject 401 "Invalid or revoked API key"
                    //      ก่อน request ถึง ExternalIntegrationController)
                    //   อื่นๆ ({company}/*) → X-Api-Key เท่านั้น
                    if (path.StartsWith("/api/integration/"))
                        request.Headers.Add("X-Integration-Key", _config.ApiKey);
                    else
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

                        // 401/403 = auth failure
                        if (status == 401 || status == 403)
                        {
                            string keyPreview = _config.ApiKey.Length > 8
                                ? _config.ApiKey.Substring(0, 4) + "****" + _config.ApiKey.Substring(_config.ApiKey.Length - 4)
                                : "****";
                            string authMsg = $"API Key authentication failed ({status}): {responseBody}. Key: {keyPreview} (length={_config.ApiKey.Length}). กรุณาตรวจสอบ API Key ใน Accounting Integration Settings";
                            // ตั้ง global cooldown เฉพาะ 401 จาก /api/integration/* (auth ของ core sync) เท่านั้น
                            // {company}/* (E-Tax/WHT) ใช้ API Key คนละประเภทกับ Integration Key —
                            // 401 ที่นั่นเป็นเรื่องปกติเมื่อ config ใช้ Integration Key และต้อง "ข้าม" ไป
                            // ไม่ใช่ block การ sync เอกสารหลักทั้งหมด
                            if (path.StartsWith("/api/integration/"))
                                SetAuthFailed(authMsg);
                            throw new AuthenticationFailedException(authMsg);
                        }

                        // Don't retry other 4xx errors (except 408 Timeout and 429 Too Many Requests)
                        if (status >= 400 && status < 500 && status != 408 && status != 429)
                        {
                            string requestPreview = (jsonBody ?? "").Length > 2000 ? jsonBody.Substring(0, 2000) + "..." : jsonBody;
                            throw new AccountingApiException(
                                $"API error {response.StatusCode} [{method.Method} {path}]: {responseBody} | Request: {requestPreview}",
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
                catch (AuthenticationFailedException)
                {
                    throw; // 401/403 — retrying won't fix bad/expired/wrong-type key. Fail fast.
                }
                catch (DnsResolutionException)
                {
                    throw; // DNS issue — retrying the same unresolvable host is pointless
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

        /// <summary>
        /// สร้าง Journal Entry — ใช้ /api/integration/journals (X-Integration-Key auth, auto-Posted)
        /// แปลง legacy CreateJournalEntryRequest (Guid AccountId) → CreateIntegrationJournalRequest (string AccountCode)
        /// ภายใน เพื่อรักษา API surface เดิมของ caller
        /// </summary>
        public async Task<ApiResponse<JournalEntryResponse>> CreateJournalAsync(CreateJournalEntryRequest journal)
        {
            if (journal.Lines == null || journal.Lines.Count == 0)
                throw new ArgumentException("Journal entry must have at least 1 debit/credit line.");

            decimal totalDr = journal.Lines.Sum(l => l.DebitAmount);
            decimal totalCr = journal.Lines.Sum(l => l.CreditAmount);

            // ตรวจ journal ทั้งใบ: ไม่มียอดติดลบ, แต่ละบรรทัดด้านเดียว, ทศนิยม ≤ 2,
            // มี debit+credit ครบ, และ DR=CR — safety net ก่อนยิง NextAcc ทุกใบ
            var jrnlCheck = AccountingArithmeticValidator.ValidateJournalLines(
                journal.Lines.Select(l => (l.DebitAmount, l.CreditAmount)));
            if (!jrnlCheck.IsValid)
            {
                AccountingArithmeticValidator.LogValidationFailure("JOURNAL", journal.Reference, jrnlCheck);
                throw new ArgumentException(
                    $"{jrnlCheck.ErrorMessage} | Ref: {journal.Reference ?? "N/A"}, Lines: {journal.Lines.Count}");
            }

            var integrationReq = Mapper.ConvertJournalToIntegration(journal);
            var flat = await PostAsync<CreateIntegrationJournalRequest, IntegrationSyncResponse>(
                "/api/integration/journals", integrationReq);

            Guid journalId = flat?.journalEntryId ?? flat?.documentId ?? Guid.Empty;
            return new ApiResponse<JournalEntryResponse>
            {
                success = flat?.success ?? false,
                message = flat?.message,
                data = (flat != null && journalId != Guid.Empty) ? new JournalEntryResponse
                {
                    Id = journalId,
                    EntryNumber = flat.documentNumber,
                    EntryDate = journal.EntryDate,
                    JournalType = journal.JournalType,
                    Description = journal.Description,
                    Reference = journal.Reference,
                    Status = 1,
                    TotalDebit = totalDr,
                    TotalCredit = totalCr
                } : null
            };
        }

        /// <summary>
        /// No-op: /api/integration/journals สร้าง journal เป็น Posted อัตโนมัติแล้ว
        /// ทิ้งไว้เพื่อ backward compatibility กับโค้ดเดิม
        /// </summary>
        public Task<ApiResponse<JournalEntryResponse>> PostJournalAsync(Guid entryId)
        {
            return Task.FromResult(new ApiResponse<JournalEntryResponse>
            {
                success = true,
                message = "Already posted by Integration API",
                data = new JournalEntryResponse { Id = entryId, Status = 1 }
            });
        }

        /// <summary>
        /// DEPRECATED: JWT-only endpoint. Process* flow ใช้ ReverseJournalAsync (Integration) แทน
        /// </summary>
        [Obsolete("Use ReverseJournalAsync. JWT-only endpoint — will 401 with Integration Key.")]
        public async Task VoidJournalAsync(Guid entryId)
        {
            await PostActionAsync($"{CompanyPath}/accounting/journals/{entryId}/void");
        }

        /// <summary>
        /// กลับรายการ Journal — ใช้ /api/integration/journals/reverse
        /// </summary>
        public async Task<ApiResponse<JournalEntryResponse>> ReverseJournalAsync(Guid entryId, ReverseJournalEntryRequest request = null)
        {
            var revReq = new CreateIntegrationReverseJournalRequest
            {
                OriginalJournalEntryId = entryId,
                ReversalDate = request?.ReversalDate,
                Description = request?.Description ?? $"กลับรายการ Journal #{entryId}"
            };
            var flat = await PostAsync<CreateIntegrationReverseJournalRequest, IntegrationSyncResponse>(
                "/api/integration/journals/reverse", revReq);

            Guid revId = flat?.journalEntryId ?? flat?.documentId ?? Guid.Empty;
            return new ApiResponse<JournalEntryResponse>
            {
                success = flat?.success ?? false,
                message = flat?.message,
                data = (flat != null && revId != Guid.Empty) ? new JournalEntryResponse
                {
                    Id = revId,
                    EntryNumber = flat.documentNumber,
                    Status = 1,
                    OriginalEntryId = entryId
                } : null
            };
        }

        public async Task DeleteJournalAsync(Guid entryId)
        {
            await ExecuteWithRetryAsync<object>(HttpMethod.Delete, $"{CompanyPath}/accounting/journals/{entryId}", null);
        }

        public async Task<ApiResponse<string>> BatchVoidJournalsAsync(BatchVoidRequest request)
        {
            return await PostAsync<BatchVoidRequest, ApiResponse<string>>(
                $"{CompanyPath}/accounting/journals/batch-void", request);
        }

        public async Task<ApiResponse<string>> BatchPostJournalsAsync()
        {
            return await PostAsync<object, ApiResponse<string>>(
                $"{CompanyPath}/accounting/journals/batch-post", null);
        }

        // Documents (DocumentController)
        public async Task<ApiResponse<DocumentResponse>> CreateDocumentAsync(CreateDocumentRequest document)
        {
            return await PostAsync<CreateDocumentRequest, ApiResponse<DocumentResponse>>(
                $"{CompanyPath}/document", document);
        }

        [Obsolete("Integration endpoints auto-approve documents. JWT-only — will 401 with Integration Key.")]
        public async Task<ApiResponse<DocumentResponse>> ApproveDocumentAsync(Guid documentId)
        {
            return await PostAsync<object, ApiResponse<DocumentResponse>>(
                $"{CompanyPath}/document/{documentId}/approve", null);
        }

        /// <summary>
        /// DEPRECATED: ใช้ /document/{id}/void (JWT-only). Process* flow ใช้ credit note แทนแล้ว
        /// คงไว้สำหรับ admin tools ที่อาจมี API Key (JWT) แยก
        /// </summary>
        [Obsolete("Use ProcessVoidReceipt's credit-note path. JWT-only endpoint — will 401 with Integration Key.")]
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
        // ──────────────────────────────────────────────
        // /api/integration/* คืน flat InboundSyncResponse (ไม่มี data wrapper)
        // helper ด้านล่างแปลง flat → ApiResponse<IntegrationDocumentResponse> ที่ caller เดิมคาดหวัง
        // ──────────────────────────────────────────────

        /// <summary>แปลง flat IntegrationSyncResponse → ApiResponse&lt;IntegrationDocumentResponse&gt;</summary>
        private static ApiResponse<IntegrationDocumentResponse> WrapDoc(IntegrationSyncResponse flat, Guid? id)
        {
            return new ApiResponse<IntegrationDocumentResponse>
            {
                success = flat?.success ?? false,
                message = flat?.message,
                data = (flat != null && id.HasValue && id.Value != Guid.Empty)
                    ? new IntegrationDocumentResponse { Id = id.Value, DocumentNumber = flat.documentNumber }
                    : null
            };
        }

        // ใช้ /api/integration/* (ไม่มี company path — API key ระบุ company อยู่แล้ว)
        public async Task<ApiResponse<IntegrationDocumentResponse>> CreateInvoiceAsync(CreateIntegrationInvoiceRequest invoice)
        {
            if (invoice.Lines == null || invoice.Lines.Count == 0)
                throw new ArgumentException("Invoice must have at least 1 line item.");

            if (!invoice.Lines.Any(l => l.UnitPrice > 0 && l.Quantity > 0))
                throw new ArgumentException("Invoice must have at least 1 line with UnitPrice > 0 and Quantity > 0.");

            EnsureLinesHaveAccountCode(invoice.Lines);
            ValidateDocumentLines(invoice.Lines, "Invoice");
            if (string.IsNullOrEmpty(invoice.ExternalRef) && !string.IsNullOrEmpty(invoice.Reference))
                invoice.ExternalRef = invoice.Reference;

            var flat = await PostAsync<CreateIntegrationInvoiceRequest, IntegrationSyncResponse>(
                "/api/integration/invoices", invoice);
            return WrapDoc(flat, flat?.documentId);
        }

        public async Task<ApiResponse<IntegrationDocumentResponse>> CreateExpenseAsync(CreateIntegrationExpenseRequest expense)
        {
            if (expense.Lines == null || expense.Lines.Count == 0)
                throw new ArgumentException("Expense must have at least 1 line item.");

            EnsureLinesHaveAccountCode(expense.Lines);
            ValidateDocumentLines(expense.Lines, "Expense");
            if (string.IsNullOrEmpty(expense.ExternalRef) && !string.IsNullOrEmpty(expense.Reference))
                expense.ExternalRef = expense.Reference;

            var flat = await PostAsync<CreateIntegrationExpenseRequest, IntegrationSyncResponse>(
                "/api/integration/expenses", expense);
            return WrapDoc(flat, flat?.documentId);
        }

        public async Task<ApiResponse<IntegrationDocumentResponse>> CreateExpenseMultipartAsync(
            CreateIntegrationExpenseRequest expense, List<string> filePaths)
        {
            EnsureApiKeyConfigured();
            if (string.IsNullOrEmpty(_config.BaseUrl))
                throw new Exception("Accounting Base URL is not configured.");

            ValidateDnsResolution(_config.BaseUrl);
            CheckAuthCooldown();
            EnsureLinesHaveAccountCode(expense.Lines);
            ValidateDocumentLines(expense.Lines, "Expense (multipart)");
            if (string.IsNullOrEmpty(expense.ExternalRef) && !string.IsNullOrEmpty(expense.Reference))
                expense.ExternalRef = expense.Reference;

            string url = $"{_config.BaseUrl.TrimEnd('/')}/api/integration/expenses/multipart";

            var savedAttachments = expense.Attachments;
            expense.Attachments = null;

            using (var form = new MultipartFormDataContent())
            {
                var json = JsonConvert.SerializeObject(expense, _jsonSettings);
                form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "expense");

                if (filePaths != null)
                {
                    foreach (var fp in filePaths)
                    {
                        if (!File.Exists(fp)) continue;
                        var fileBytes = File.ReadAllBytes(fp);
                        var fileContent = new ByteArrayContent(fileBytes);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(fp));
                        form.Add(fileContent, "files", Path.GetFileName(fp));
                    }
                }

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("X-Integration-Key", _config.ApiKey);
                request.Content = form;

                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                LogApiCall("POST", "/api/integration/expenses/multipart", "[multipart]", responseBody,
                    (int)response.StatusCode, response.IsSuccessStatusCode, 0);

                expense.Attachments = savedAttachments;

                if (!response.IsSuccessStatusCode)
                    throw new AccountingApiException(
                        $"Multipart expense upload failed: {response.StatusCode} {responseBody}",
                        (int)response.StatusCode, responseBody);

                var flat = JsonConvert.DeserializeObject<IntegrationSyncResponse>(responseBody, _jsonSettings);
                return WrapDoc(flat, flat?.documentId);
            }
        }

        /// <summary>
        /// NextAcc /api/integration/invoices อ่าน AccountCode (string) ไม่ใช่ AccountId (Guid)
        /// เติม AccountCode อัตโนมัติจาก reverse lookup mapping ถ้า caller ไม่ได้ตั้งค่า
        /// </summary>
        private void EnsureLinesHaveAccountCode(List<IntegrationLineRequest> lines)
        {
            if (lines == null || lines.Count == 0) return;
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line.AccountCode) && line.AccountId.HasValue && line.AccountId.Value != Guid.Empty)
                {
                    var code = Mapper.GetAccountCodeFromId(line.AccountId.Value);
                    if (!string.IsNullOrEmpty(code))
                        line.AccountCode = code;
                }
            }
        }

        /// <summary>
        /// ตรวจความถูกต้องของบรรทัดเอกสาร (invoice/expense/credit-note/debit-note) ก่อนยิง NextAcc:
        ///   - Quantity / UnitPrice / line amount ทศนิยมไม่เกิน 2 หลัก
        ///   - VatRate อยู่ในช่วง 0-100
        ///   - ผลรวมสุทธิของทุกบรรทัด > 0 (กันเอกสารยอด 0/ติดลบ)
        /// </summary>
        private void ValidateDocumentLines(List<IntegrationLineRequest> lines, string docContext)
        {
            if (lines == null || lines.Count == 0)
                throw new ArgumentException($"{docContext}: ต้องมีอย่างน้อย 1 บรรทัดรายการ");

            decimal netSum = 0m;
            int lineNo = 0;
            foreach (var line in lines)
            {
                lineNo++;
                decimal amount = line.Quantity * line.UnitPrice - (line.DiscountAmount ?? 0m);

                if (Math.Round(line.Quantity, 4) != line.Quantity)
                    throw new ArgumentException($"{docContext} บรรทัด {lineNo}: จำนวน ({line.Quantity}) มีทศนิยมเกินกำหนด");
                if (Math.Round(line.UnitPrice, 2) != line.UnitPrice)
                    throw new ArgumentException($"{docContext} บรรทัด {lineNo}: ราคาต่อหน่วย ({line.UnitPrice}) มีทศนิยมเกิน 2 หลัก");
                if (line.VatRate < 0 || line.VatRate > 100)
                    throw new ArgumentException($"{docContext} บรรทัด {lineNo}: อัตรา VAT ไม่ถูกต้อง ({line.VatRate}%)");

                netSum += amount;
            }

            if (netSum <= 0)
                throw new ArgumentException(
                    $"{docContext}: ยอดรวมสุทธิของเอกสาร = {netSum:N2} — ต้องมากกว่า 0 (ตรวจส่วนลด/บรรทัดติดลบที่เกินยอด)");
        }

        // Integration Payments (/api/integration/payments)
        public async Task<ApiResponse<IntegrationPaymentResponse>> CreateIntegrationPaymentAsync(CreateIntegrationPaymentRequest payment)
        {
            if (payment.Amount <= 0)
                throw new ArgumentException("Payment amount must be > 0.");

            var flat = await PostAsync<CreateIntegrationPaymentRequest, IntegrationSyncResponse>(
                "/api/integration/payments", payment);
            return new ApiResponse<IntegrationPaymentResponse>
            {
                success = flat?.success ?? false,
                message = flat?.message,
                data = (flat != null && flat.paymentId.HasValue && flat.paymentId.Value != Guid.Empty)
                    ? new IntegrationPaymentResponse { Id = flat.paymentId.Value, PaymentNumber = flat.documentNumber }
                    : null
            };
        }

        // Integration Journals (/api/integration/journals) — ใช้ AccountCode แทน AccountId
        public async Task<ApiResponse<IntegrationDocumentResponse>> CreateIntegrationJournalAsync(CreateIntegrationJournalRequest journal)
        {
            if (journal.Lines == null || journal.Lines.Count == 0)
                throw new ArgumentException("Integration journal must have at least 1 line.");

            var flat = await PostAsync<CreateIntegrationJournalRequest, IntegrationSyncResponse>(
                "/api/integration/journals", journal);
            return WrapDoc(flat, flat?.journalEntryId ?? flat?.documentId);
        }

        // Integration Reverse Journal
        public async Task<ApiResponse<IntegrationDocumentResponse>> ReverseIntegrationJournalAsync(CreateIntegrationReverseJournalRequest request)
        {
            var flat = await PostAsync<CreateIntegrationReverseJournalRequest, IntegrationSyncResponse>(
                "/api/integration/journals/reverse", request);
            return WrapDoc(flat, flat?.journalEntryId ?? flat?.documentId);
        }

        // Integration Daily Summary
        public async Task<ApiResponse<IntegrationDocumentResponse>> CreateDailySummaryAsync(CreateDailySummaryRequest request)
        {
            if (request.Lines == null || request.Lines.Count == 0)
                throw new ArgumentException("Daily summary must have at least 1 line.");

            var flat = await PostAsync<CreateDailySummaryRequest, IntegrationSyncResponse>(
                "/api/integration/daily-summary", request);
            return WrapDoc(flat, flat?.documentId ?? flat?.journalEntryId);
        }

        // Integration Batch — คืน flat InboundBatchResponse {totalProcessed, successCount, errorCount, results[]}
        public async Task<InboundBatchResponse> CreateIntegrationBatchAsync(CreateIntegrationBatchRequest request)
        {
            return await PostAsync<CreateIntegrationBatchRequest, InboundBatchResponse>(
                "/api/integration/batch", request);
        }

        // Integration Customers (/api/integration/customers)
        public async Task<ApiResponse<IntegrationDocumentResponse>> CreateIntegrationCustomerAsync(InboundCustomerRequest customer)
        {
            var flat = await PostAsync<InboundCustomerRequest, IntegrationSyncResponse>(
                "/api/integration/customers", customer);
            return WrapDoc(flat, flat?.contactId);
        }

        // Integration Products (/api/integration/products) — upsert by Code
        public async Task<ApiResponse<IntegrationDocumentResponse>> SyncIntegrationProductAsync(InboundProductRequest product)
        {
            if (string.IsNullOrEmpty(product?.Code))
                throw new ArgumentException("Integration product must have a Code (used as upsert key).");

            var flat = await PostAsync<InboundProductRequest, IntegrationSyncResponse>(
                "/api/integration/products", product);
            // product sync คืน documentNumber = Code, อาจไม่มี Guid → WrapDoc คืน data=null ได้ (caller รองรับ)
            return WrapDoc(flat, flat?.documentId);
        }

        // Integration Credit Notes (/api/integration/credit-notes)
        public async Task<ApiResponse<IntegrationDocumentResponse>> CreateIntegrationCreditNoteAsync(InboundCreditNoteRequest creditNote)
        {
            if (creditNote.Lines == null || creditNote.Lines.Count == 0)
                throw new ArgumentException("Credit note must have at least 1 line item.");

            EnsureLinesHaveAccountCode(creditNote.Lines);
            ValidateDocumentLines(creditNote.Lines, "CreditNote");

            var flat = await PostAsync<InboundCreditNoteRequest, IntegrationSyncResponse>(
                "/api/integration/credit-notes", creditNote);
            return WrapDoc(flat, flat?.documentId);
        }

        // Integration Debit Notes (/api/integration/debit-notes)
        public async Task<ApiResponse<IntegrationDocumentResponse>> CreateIntegrationDebitNoteAsync(InboundDebitNoteRequest debitNote)
        {
            if (debitNote.Lines == null || debitNote.Lines.Count == 0)
                throw new ArgumentException("Debit note must have at least 1 line item.");

            EnsureLinesHaveAccountCode(debitNote.Lines);
            ValidateDocumentLines(debitNote.Lines, "DebitNote");

            var flat = await PostAsync<InboundDebitNoteRequest, IntegrationSyncResponse>(
                "/api/integration/debit-notes", debitNote);
            return WrapDoc(flat, flat?.documentId);
        }

        // Integration Void Document (/api/integration/documents/void)
        public async Task<ApiResponse<IntegrationDocumentResponse>> VoidDocumentViaIntegrationAsync(InboundVoidDocumentRequest request)
        {
            if (!request.DocumentId.HasValue && string.IsNullOrEmpty(request.ExternalRef))
                throw new ArgumentException("VoidDocument requires DocumentId or ExternalRef.");

            var flat = await PostAsync<InboundVoidDocumentRequest, IntegrationSyncResponse>(
                "/api/integration/documents/void", request);
            return WrapDoc(flat, flat?.documentId);
        }

        // Integration Certificates in Lieu (/api/integration/certificates-in-lieu)
        public async Task<ApiResponse<IntegrationDocumentResponse>> CreateCertificateInLieuAsync(InboundCertificateInLieuRequest request)
        {
            if (request.Lines == null || request.Lines.Count == 0)
                throw new ArgumentException("Certificate in lieu must have at least 1 line item.");

            EnsureLinesHaveAccountCode(request.Lines);
            ValidateDocumentLines(request.Lines, "CertificateInLieu");

            var flat = await PostAsync<InboundCertificateInLieuRequest, IntegrationSyncResponse>(
                "/api/integration/certificates-in-lieu", request);
            return WrapDoc(flat, flat?.documentId);
        }

        // Integration Outbound Queries — NextAcc คืน flat (ไม่มี ApiResponse wrapper)
        public async Task<PagedResponse<OutboundDocumentResponse>> GetIntegrationDocumentsAsync(OutboundQueryParams query = null)
        {
            string qs = query != null ? $"?{query.ToQueryString()}" : "";
            return await GetAsync<PagedResponse<OutboundDocumentResponse>>($"/api/integration/documents{qs}");
        }

        public async Task<PagedResponse<OutboundContactResponse>> GetIntegrationContactsAsync(OutboundQueryParams query = null)
        {
            string qs = query != null ? $"?{query.ToQueryString()}" : "";
            return await GetAsync<PagedResponse<OutboundContactResponse>>($"/api/integration/contacts{qs}");
        }

        public async Task<PagedResponse<OutboundPaymentResponse>> GetIntegrationPaymentsAsync(OutboundQueryParams query = null)
        {
            string qs = query != null ? $"?{query.ToQueryString()}" : "";
            return await GetAsync<PagedResponse<OutboundPaymentResponse>>($"/api/integration/payments-list{qs}");
        }

        public async Task<List<OutboundAccountBalanceResponse>> GetIntegrationAccountBalancesAsync()
        {
            return await GetAsync<List<OutboundAccountBalanceResponse>>("/api/integration/account-balances");
        }

        // WHT Certificate (หนังสือรับรองหัก ณ ที่จ่าย)
        public async Task<ApiResponse<WithholdingTaxCertResponse>> AutoGenerateWhtCertAsync(AutoGenerateWhtRequest request)
        {
            return await PostAsync<AutoGenerateWhtRequest, ApiResponse<WithholdingTaxCertResponse>>(
                $"{CompanyPath}/withholding-tax-certs/auto-generate", request);
        }

        public async Task<ApiResponse<BulkGenerateWhtResponse>> BulkGenerateWhtCertsAsync(BulkGenerateWhtRequest request)
        {
            return await PostAsync<BulkGenerateWhtRequest, ApiResponse<BulkGenerateWhtResponse>>(
                $"{CompanyPath}/withholding-tax-certs/bulk-generate", request);
        }

        /// <summary>ดึงรายการใบหัก ณ ที่จ่าย (50 ทวิ) ที่ออกให้กับเอกสารต้นทาง (read-only).
        /// ต้องใช้ API Key (acc_) — Integration Key ใช้ไม่ได้.
        /// NextAcc คืนเป็น paged: { data: { items: [...] } }</summary>
        public async Task<ApiResponse<PagedResponse<WithholdingTaxCertResponse>>> GetWhtCertsByDocumentAsync(Guid documentId)
        {
            return await GetAsync<ApiResponse<PagedResponse<WithholdingTaxCertResponse>>>(
                $"{CompanyPath}/withholding-tax-certs?documentId={documentId}");
        }

        /// <summary>ดึง PDF ใบหัก ณ ที่จ่ายตาม certId. คืน null ถ้าไม่มี/ล้มเหลว.</summary>
        public async Task<byte[]> GetWhtCertPdfAsync(Guid certId)
        {
            EnsureApiKeyConfigured();
            if (string.IsNullOrEmpty(_config.BaseUrl) || certId == Guid.Empty) return null;

            ValidateDnsResolution(_config.BaseUrl);
            CheckAuthCooldown();

            string path = $"{CompanyPath}/withholding-tax-certs/{certId}/pdf";
            string url = $"{_config.BaseUrl.TrimEnd('/')}{path}";

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Add("X-Api-Key", _config.ApiKey);
                var startTime = DateTime.Now;
                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                int durationMs = (int)(DateTime.Now - startTime).TotalMilliseconds;

                if (!response.IsSuccessStatusCode)
                {
                    string errBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    LogApiCall("GET", path, "", errBody, (int)response.StatusCode, false, durationMs);
                    if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                        throw new AuthenticationFailedException(
                            $"wht-cert pdf auth failed ({(int)response.StatusCode}): {errBody}");
                    return null;
                }

                LogApiCall("GET", path, "", "[pdf bytes]", (int)response.StatusCode, true, durationMs);
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        public async Task<ApiResponse<List<IncomeTypeInfo>>> GetIncomeTypesAsync()
        {
            return await GetAsync<ApiResponse<List<IncomeTypeInfo>>>("/api/reference/income-types");
        }

        // E-Tax Invoice (ใบกำกับภาษีอิเล็กทรอนิกส์)
        public async Task<ApiResponse<EtaxInvoiceResponse>> GenerateEtaxAsync(GenerateEtaxRequest request)
        {
            return await PostAsync<GenerateEtaxRequest, ApiResponse<EtaxInvoiceResponse>>(
                $"{CompanyPath}/etax/generate", request);
        }

        public async Task<ApiResponse<EtaxInvoiceResponse>> QuickSubmitEtaxAsync(GenerateEtaxRequest request)
        {
            return await PostAsync<GenerateEtaxRequest, ApiResponse<EtaxInvoiceResponse>>(
                $"{CompanyPath}/etax/quick-submit", request);
        }

        public async Task<ApiResponse<EtaxInvoiceResponse>> SignAndSubmitEtaxAsync(Guid etaxId)
        {
            return await PostAsync<object, ApiResponse<EtaxInvoiceResponse>>(
                $"{CompanyPath}/etax/{etaxId}/sign-and-submit", null);
        }

        public async Task VoidEtaxAsync(Guid etaxId)
        {
            await PostActionAsync($"{CompanyPath}/etax/{etaxId}/void");
        }

        public async Task<ApiResponse<EtaxConfigResponse>> GetEtaxConfigAsync()
        {
            return await GetAsync<ApiResponse<EtaxConfigResponse>>($"{CompanyPath}/etax/config");
        }

        public async Task<ApiResponse<EtaxConfigResponse>> UpdateEtaxConfigAsync(UpdateEtaxConfigRequest request)
        {
            return await PutAsync<UpdateEtaxConfigRequest, ApiResponse<EtaxConfigResponse>>(
                $"{CompanyPath}/etax/config", request);
        }

        public async Task<ApiResponse<object>> GetEtaxConfigStatusAsync()
        {
            return await GetAsync<ApiResponse<object>>($"{CompanyPath}/etax/config-status");
        }

        public async Task<ApiResponse<DocumentEmailLogResponse>> SendEtaxByEmailAsync(Guid etaxId, SendEtaxByEmailRequest request)
        {
            return await PostAsync<SendEtaxByEmailRequest, ApiResponse<DocumentEmailLogResponse>>(
                $"{CompanyPath}/etax/{etaxId}/send-email", request);
        }

        /// <summary>ดึง E-Tax ตาม ID — ใช้ refresh URL PDF/XML สำหรับ fallback download</summary>
        public async Task<ApiResponse<EtaxInvoiceResponse>> GetEtaxAsync(Guid etaxId)
        {
            return await GetAsync<ApiResponse<EtaxInvoiceResponse>>($"{CompanyPath}/etax/{etaxId}");
        }

        /// <summary>
        /// ดาวน์โหลดไฟล์จาก URL (เช่น PDF/XML ของ E-Tax) — ใช้สำหรับ fallback ส่งอีเมลผ่าน SMTP ของ TakeTime เอง.
        /// แนบ X-Api-Key ในกรณีที่ URL อยู่บน NextAcc และต้องการ auth.
        /// Returns: byte[] — content; null ถ้าโหลดไม่สำเร็จ.
        /// </summary>
        public async Task<byte[]> DownloadFileAsync(string url, int timeoutSeconds = 30)
        {
            if (string.IsNullOrEmpty(url)) return null;

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) })
            {
                if (!string.IsNullOrEmpty(_config.ApiKey))
                {
                    client.DefaultRequestHeaders.Add("X-Api-Key", _config.ApiKey);
                }
                try
                {
                    using (var resp = await client.GetAsync(url))
                    {
                        if (!resp.IsSuccessStatusCode) return null;
                        return await resp.Content.ReadAsByteArrayAsync();
                    }
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// สร้าง/ดึง PDF อย่างเป็นทางการของเอกสารจาก NextAcc (เรนเดอร์ตาม template ใน NextAcc)
        /// POST /api/companies/{companyId}/document-templates/generate-pdf
        /// คืน byte[] ของ PDF; null ถ้าเอกสารไม่พบ/ไม่มี template (caller fallback ไปใช้ PDF ระบบเดิม)
        /// ใช้ X-Api-Key — int_ key ผ่าน [Authorize] ได้ผ่าน ApiKeyMiddleware fallback
        /// </summary>
        public async Task<byte[]> GenerateDocumentPdfAsync(Guid documentId, Guid? templateId = null, string watermark = null)
        {
            EnsureApiKeyConfigured();
            if (string.IsNullOrEmpty(_config.BaseUrl))
                throw new Exception("Accounting Base URL is not configured.");
            if (documentId == Guid.Empty) return null;

            ValidateDnsResolution(_config.BaseUrl);
            CheckAuthCooldown();

            string path = $"{CompanyPath}/document-templates/generate-pdf";
            string url = $"{_config.BaseUrl.TrimEnd('/')}{path}";
            var bodyObj = new { DocumentId = documentId, TemplateId = templateId, WatermarkOverride = watermark };
            string jsonBody = JsonConvert.SerializeObject(bodyObj, _jsonSettings);

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Add("X-Api-Key", _config.ApiKey);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var startTime = DateTime.Now;
                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                int durationMs = (int)(DateTime.Now - startTime).TotalMilliseconds;

                if (!response.IsSuccessStatusCode)
                {
                    string errBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    LogApiCall("POST", path, jsonBody, errBody, (int)response.StatusCode, false, durationMs);
                    if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                        throw new AuthenticationFailedException(
                            $"generate-pdf auth failed ({(int)response.StatusCode}): {errBody}");
                    // 404 (ไม่มี template/เอกสาร) → คืน null ให้ caller fallback ไปใช้ PDF ระบบเดิม
                    return null;
                }

                LogApiCall("POST", path, jsonBody, "[pdf bytes]", (int)response.StatusCode, true, durationMs);
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        // Document: Convert Type, Write-off Bad Debt, Void Payment
        public async Task<ApiResponse<DocumentResponse>> ConvertDocumentTypeAsync(Guid documentId, int newDocumentType)
        {
            var body = new { DocumentType = newDocumentType };
            return await PostAsync<object, ApiResponse<DocumentResponse>>(
                $"{CompanyPath}/document/{documentId}/convert", body);
        }

        public async Task VoidPaymentAsync(Guid paymentId)
        {
            await PostActionAsync($"{CompanyPath}/document/payments/{paymentId}/void");
        }

        // Contact: Smart Defaults & Parse Address
        public async Task<ApiResponse<ContactResponse>> GetContactSmartDefaultsAsync(string name)
        {
            string qs = !string.IsNullOrEmpty(name) ? $"?name={Uri.EscapeDataString(name)}" : "";
            return await GetAsync<ApiResponse<ContactResponse>>($"{CompanyPath}/document/contacts/smart-defaults{qs}");
        }

        public async Task<ApiResponse<ContactResponse>> ParseContactAddressAsync(string rawAddress)
        {
            var body = new { Address = rawAddress };
            return await PostAsync<object, ApiResponse<ContactResponse>>(
                $"{CompanyPath}/document/contacts/parse-address", body);
        }

        // ──────────────────────────────────────────────
        // File Attachments (FileAttachmentController)
        // POST /api/companies/{companyId}/attachments/{entityType}/{entityId}
        // ──────────────────────────────────────────────

        public async Task<ApiResponse<FileAttachmentResponse>> UploadAttachmentAsync(
            string entityType, Guid entityId, string filePath)
        {
            EnsureApiKeyConfigured();
            if (string.IsNullOrEmpty(_config.BaseUrl))
                throw new Exception("Accounting Base URL is not configured.");
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Attachment file not found: {filePath}");

            ValidateDnsResolution(_config.BaseUrl);
            CheckAuthCooldown();

            string url = $"{_config.BaseUrl.TrimEnd('/')}{CompanyPath}/attachments/{entityType}/{entityId}";
            string fileName = Path.GetFileName(filePath);
            string contentType = GetContentType(fileName);

            using (var form = new MultipartFormDataContent())
            {
                var fileBytes = File.ReadAllBytes(filePath);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                form.Add(fileContent, "file", fileName);

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                // {company}/attachments/* เป็น JWT endpoint → X-Api-Key
                request.Headers.Add("X-Api-Key", _config.ApiKey);
                request.Content = form;

                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    throw new AccountingApiException(
                        $"Upload attachment failed: {response.StatusCode} {responseBody}",
                        (int)response.StatusCode, responseBody);

                return JsonConvert.DeserializeObject<ApiResponse<FileAttachmentResponse>>(responseBody, _jsonSettings);
            }
        }

        public async Task<ApiResponse<List<FileAttachmentResponse>>> GetAttachmentsAsync(string entityType, Guid entityId)
        {
            return await GetAsync<ApiResponse<List<FileAttachmentResponse>>>(
                $"{CompanyPath}/attachments/{entityType}/{entityId}");
        }

        /// <summary>ดาวน์โหลดเนื้อไฟล์แนบผ่าน attachment Id (ใช้ X-Api-Key). คืน null ถ้าไม่สำเร็จ.
        /// ลองหลาย route ที่ NextAcc อาจใช้ — บาง deployment serve storagePath ตรงๆ ไม่ได้.</summary>
        public async Task<byte[]> DownloadAttachmentByIdAsync(Guid attachmentId)
        {
            if (attachmentId == Guid.Empty || string.IsNullOrEmpty(_config.BaseUrl)) return null;
            string baseUrl = _config.BaseUrl.TrimEnd('/');
            string[] candidates =
            {
                $"{baseUrl}{CompanyPath}/attachments/{attachmentId}/download",
                $"{baseUrl}{CompanyPath}/attachments/{attachmentId}/content",
                $"{baseUrl}{CompanyPath}/attachments/{attachmentId}"
            };
            foreach (var url in candidates)
            {
                byte[] bytes = await DownloadFileAsync(url);
                if (bytes != null && bytes.Length > 0) return bytes;
            }
            return null;
        }

        public async Task DeleteAttachmentAsync(Guid attachmentId)
        {
            await DeleteAsync($"{CompanyPath}/attachments/{attachmentId}");
        }

        // Multipart Invoice Upload (/api/integration/invoices/multipart)
        public async Task<ApiResponse<IntegrationDocumentResponse>> CreateInvoiceMultipartAsync(
            CreateIntegrationInvoiceRequest invoice, List<string> filePaths)
        {
            EnsureApiKeyConfigured();
            if (string.IsNullOrEmpty(_config.BaseUrl))
                throw new Exception("Accounting Base URL is not configured.");

            ValidateDnsResolution(_config.BaseUrl);
            CheckAuthCooldown();
            EnsureLinesHaveAccountCode(invoice.Lines);
            ValidateDocumentLines(invoice.Lines, "Invoice (multipart)");

            string url = $"{_config.BaseUrl.TrimEnd('/')}/api/integration/invoices/multipart";

            using (var form = new MultipartFormDataContent())
            {
                var json = JsonConvert.SerializeObject(invoice, _jsonSettings);
                // NextAcc binds [FromForm] string invoice — ชื่อ field ต้องเป็น "invoice"
                // (เดิมส่งชื่อ "data" → NextAcc ได้ค่า null → 400 "Empty invoice payload")
                form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "invoice");

                if (filePaths != null)
                {
                    foreach (var fp in filePaths)
                    {
                        if (!File.Exists(fp)) continue;
                        var fileBytes = File.ReadAllBytes(fp);
                        var fileContent = new ByteArrayContent(fileBytes);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(fp));
                        form.Add(fileContent, "files", Path.GetFileName(fp));
                    }
                }

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                // /api/integration/invoices/multipart → X-Integration-Key เท่านั้น
                request.Headers.Add("X-Integration-Key", _config.ApiKey);
                request.Content = form;

                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                LogApiCall("POST", "/api/integration/invoices/multipart", "[multipart]", responseBody,
                    (int)response.StatusCode, response.IsSuccessStatusCode, 0);

                if (!response.IsSuccessStatusCode)
                    throw new AccountingApiException(
                        $"Multipart invoice upload failed: {response.StatusCode} {responseBody}",
                        (int)response.StatusCode, responseBody);

                // /api/integration/invoices/multipart คืน flat InboundSyncResponse เช่นเดียวกับ endpoint อื่น
                var flat = JsonConvert.DeserializeObject<IntegrationSyncResponse>(responseBody, _jsonSettings);
                return WrapDoc(flat, flat?.documentId);
            }
        }

        private static string GetContentType(string fileName)
        {
            string ext = (Path.GetExtension(fileName) ?? "").ToLower();
            switch (ext)
            {
                case ".pdf": return "application/pdf";
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".gif": return "image/gif";
                case ".bmp": return "image/bmp";
                case ".webp": return "image/webp";
                case ".xml": return "application/xml";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".xls": return "application/vnd.ms-excel";
                case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                default: return "application/octet-stream";
            }
        }

        // Products (ProductController — JWT-only). Process* flow ใช้ SyncIntegrationProductAsync แทน
        [Obsolete("Use SyncIntegrationProductAsync. JWT-only — will 401 with Integration Key.")]
        public async Task<ApiResponse<ProductResponse>> CreateProductAsync(CreateProductRequest product)
        {
            return await PostAsync<CreateProductRequest, ApiResponse<ProductResponse>>(
                $"{CompanyPath}/product", product);
        }

        [Obsolete("Use SyncIntegrationProductAsync. JWT-only — will 401 with Integration Key.")]
        public async Task<ApiResponse<ProductResponse>> UpdateProductAsync(Guid productId, UpdateProductRequest product)
        {
            return await PutAsync<UpdateProductRequest, ApiResponse<ProductResponse>>(
                $"{CompanyPath}/product/{productId}", product);
        }

        [Obsolete("JWT-only — will 401 with Integration Key. NextAcc Integration ไม่มี stock-adjust endpoint โดยตรง; ใช้ ProcessStockAdjustment ส่ง journal แทน")]
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
        /// <summary>
        /// สร้างข้อความ 401 ที่ชี้สาเหตุชัดเจน — โดยเฉพาะกรณีใช้ API Key ผิดประเภท
        /// NextAcc: Integration Key ขึ้นต้น "int_" / API Key ขึ้นต้น "acc_"
        /// การ sync ทั้งหมดใช้ /api/integration/* ซึ่งต้องใช้ Integration Key เท่านั้น
        /// </summary>
        private string BuildAuthFailMessage(string serverDetail, string keyPreview, string targetUrl)
        {
            string key = _config.ApiKey ?? "";
            string prefix = key.Length >= 4 ? key.Substring(0, 4) : key;

            if (prefix.Equals("acc_", StringComparison.OrdinalIgnoreCase))
            {
                return "❌ ใช้ API Key ผิดประเภท\n\n" +
                    $"API Key ที่กรอก: {keyPreview} — ขึ้นต้นด้วย \"acc_\" = เป็น API Key (สำหรับ {{company}}/* endpoints)\n\n" +
                    "ระบบ sync บัญชีทั้งหมดใช้ /api/integration/* ซึ่งต้องใช้ \"Integration Key\" (ขึ้นต้นด้วย \"int_\")\n\n" +
                    "วิธีแก้:\n" +
                    "  1) เข้า NextAcc → เมนู Integrations (ไม่ใช่ API Keys)\n" +
                    "  2) สร้าง Integration ใหม่ — จะได้ Integration Key ขึ้นต้นด้วย \"int_\"\n" +
                    "  3) คัดลอก Integration Key มากรอกในหน้านี้แทน\n\n" +
                    $"URL ทดสอบ: {targetUrl}";
            }

            string typeNote = prefix.Equals("int_", StringComparison.OrdinalIgnoreCase)
                ? "Key ขึ้นต้น \"int_\" (เป็น Integration Key ที่ถูกประเภท) — "
                : $"Key ขึ้นต้น \"{prefix}\" — ";

            return "❌ Integration Key ไม่ผ่านการตรวจสอบ (401 Unauthorized)\n\n" +
                $"Integration Key: {keyPreview} (ความยาว {key.Length} ตัวอักษร)\n" +
                typeNote + "ตรวจสอบ:\n" +
                "  1) คัดลอก Integration Key จาก NextAcc → Integrations ใหม่อีกครั้ง\n" +
                "  2) Integration ยัง Active อยู่ (ไม่ถูกปิด/ลบ)\n" +
                "  3) IP ของเซิร์ฟเวอร์อยู่ใน whitelist ของ Integration\n" +
                "  4) Company ID ตรงกับ Integration\n\n" +
                $"URL: {targetUrl}\n" +
                (string.IsNullOrEmpty(serverDetail) ? "" : $"รายละเอียดจากเซิร์ฟเวอร์: {serverDetail}");
        }

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
            // ทดสอบผ่าน /api/integration/contacts — endpoint ใช้ X-Integration-Key เดียวกับที่ sync ใช้
            // (เลือก contacts เพราะ query แปลเป็น SQL ได้ปลอดภัย — account-balances มี bug ฝั่ง NextAcc)
            string targetUrl = $"{_config.BaseUrl}/api/integration/contacts";

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var contacts = await GetIntegrationContactsAsync(
                    new OutboundQueryParams { Page = 1, PageSize = 1 }).ConfigureAwait(false);
                sw.Stop();
                int total = contacts?.TotalCount ?? 0;
                return new ConnectionTestResult(true,
                    $"NextAcc Integration API เชื่อมต่อสำเร็จ — Integration Key ใช้งานได้ " +
                    $"(ผู้ติดต่อในระบบ {total} ราย, {sw.ElapsedMilliseconds}ms)");
            }
            catch (DnsResolutionException ex)
            {
                return new ConnectionTestResult(false,
                    $"DNS resolution failed ระหว่าง API call: {ex.Message}\nURL: {targetUrl}");
            }
            catch (AuthenticationFailedException ex)
            {
                return new ConnectionTestResult(false, BuildAuthFailMessage(ex.Message, apiKeyPreview, targetUrl), 401);
            }
            catch (AccountingApiException ex) when (ex.StatusCode == 401)
            {
                return new ConnectionTestResult(false, BuildAuthFailMessage(ex.ResponseBody, apiKeyPreview, targetUrl),
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
                // ผ่าน auth แล้ว (ไม่ใช่ 401/403) — Integration Key ใช้งานได้
                // error อื่น (เช่น 400/500) เป็นปัญหาฝั่ง NextAcc ที่ endpoint ทดสอบ ไม่เกี่ยวกับ key
                return new ConnectionTestResult(true,
                    $"✓ Integration Key ผ่านการตรวจสอบแล้ว — การ sync ใช้งานได้\n\n" +
                    $"หมายเหตุ: endpoint ทดสอบ ({targetUrl}) คืน error {ex.StatusCode} " +
                    $"ซึ่งเป็นปัญหาฝั่งเซิร์ฟเวอร์ NextAcc ไม่เกี่ยวกับ Integration Key\n" +
                    $"รายละเอียด: {ex.ResponseBody}");
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
            catch
            {
                try
                {
                    string detail = $"[{method} {path}] HTTP {httpStatus} | Req: {TruncateForLog(requestBody)} | Res: {TruncateForLog(responseBody)}";
                    _code.Logs(_connectionString, "AccountingSync-API", detail, "SYSTEM");
                }
                catch { }
            }
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
