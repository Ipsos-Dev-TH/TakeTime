using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Take_Time_BangPhra
{
    public class TelegramBot2
    {
        private readonly string _botToken;
        private readonly HttpClient _httpClient;

        public TelegramBot2(string botToken)
        {
            _botToken = botToken;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task SendMessageAsync(string chatId, string message)
        {
            try
            {
                // Simple HTTP implementation without RestSharp
                string url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

                var payload = new
                {
                    chat_id = chatId,
                    text = message,
                    parse_mode = "HTML"
                };

                string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Trace.TraceError($"Telegram API error: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Telegram send error: {ex.Message}");
                // Don't throw - telegram failure shouldn't break the main reservation process
            }
        }

        public async Task SendMediaAsync(string chatId, string caption, byte[] fileBytes, string filename, string contentType)
        {
            try
            {
                // Simple implementation for media upload
                string url = $"https://api.telegram.org/bot{_botToken}/sendDocument";

                using (var form = new MultipartFormDataContent())
                {
                    form.Add(new StringContent(chatId), "chat_id");
                    form.Add(new StringContent(caption), "caption");

                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                    form.Add(fileContent, "document", filename);

                    var response = await _httpClient.PostAsync(url, form);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Trace.TraceError($"Telegram media API error: {response.StatusCode} - {errorContent}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Telegram media send error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}