using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Core.Services
{
    // Minimal temporary spike service to call OpenAI GPT-5.6 Luna via HTTP
    public sealed class LunaConversationService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public LunaConversationService(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
        }

        public async Task<string> SendMessageAsync(string userMessage, string systemPrompt)
        {
            if (string.IsNullOrWhiteSpace(_apiKey)) return string.Empty;

            // Use only parameters supported by the Luna chat-completions compatibility layer.
            // Removed unsupported 'max_output_tokens'. Keep only model and messages to maximize compatibility.
            var request = new
            {
                model = "gpt-5.6-luna",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                }
            };

            var reqJson = JsonSerializer.Serialize(request);

            try
            {
                using var httpReq = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                httpReq.Content = new StringContent(reqJson, Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(httpReq).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[Luna] HTTP {resp.StatusCode}: {body}");
                    }
                    catch { }
                    return string.Empty;
                }
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content))
                    {
                        return content.GetString()?.Trim() ?? string.Empty;
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
