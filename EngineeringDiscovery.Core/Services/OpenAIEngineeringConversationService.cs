using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using System.Diagnostics;

namespace EngineeringDiscovery.Core.Services
{
    public class OpenAIEngineeringConversationService : IEngineeringConversationService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public OpenAIEngineeringConversationService(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                // Running without an API key will throw when attempting to call OpenAI.
            }
        }

        public async Task<string> GetNextQuestionAsync(EngineeringModel model)
        {
            // Diagnostic: log stack trace whenever the OpenAI conversation client is invoked
            try
            {
                Debug.WriteLine("[ED-EP7A] OpenAIEngineeringConversationService.GetNextQuestionAsync invoked. StackTrace:\n" + Environment.StackTrace);
            }
            catch { }

            if (model == null) throw new ArgumentNullException(nameof(model));

            // Build a concise prompt per guidance. The model should return a single concise question.
            var prompt = BuildPrompt(model);
            // If API key is not configured, gracefully fall back to deterministic behavior by returning empty.
            if (string.IsNullOrWhiteSpace(_apiKey)) return string.Empty;

            // Use the current OpenAI Chat Completions API (/v1/chat/completions).
            // No streaming, no function calling. Send a system message that defines EngineOS behavior, then the user prompt.
            var system = BuildSystemPrompt();
            var request = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = prompt }
                },
                max_tokens = 80,
                temperature = 0.0,
                n = 1
            };

            var reqJson = JsonSerializer.Serialize(request);

            try
            {
                using var httpReq = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                httpReq.Content = new StringContent(reqJson, Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(httpReq).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    // Graceful fallback: do not throw, return empty so orchestrator can use deterministic fallback
                    return string.Empty;
                }

                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Chat completions return choices[].message.content
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
                // Preserve graceful fallback semantics: on any error return empty so the orchestrator uses deterministic fallback.
                return string.Empty;
            }
        }

        private string BuildPrompt(EngineeringModel model)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an experienced software architect. Your job is to discover one missing engineering fact by asking exactly one concise engineering question.");
            sb.AppendLine("Do not recommend technologies, do not invent requirements, do not provide architectures, and do not solve the problem. Ask one question whose only purpose is to discover the missing fact.");
            sb.AppendLine();
            sb.AppendLine("Original idea:");
            sb.AppendLine(model.OriginalIdea);
            sb.AppendLine();

            // If the orchestrator marked an active objective and last asked fact, surface it prominently so the AI only asks about that fact
            var activeObj = model.DiscoveryObjectives?.FirstOrDefault(o => o.Status == ObjectiveStatus.Active) ?? model.DiscoveryObjectives?.FirstOrDefault(o => o.Status == ObjectiveStatus.NotStarted);
            if (activeObj != null && !string.IsNullOrWhiteSpace(activeObj.LastAskedFact))
            {
                sb.AppendLine($"Current objective: {activeObj.Name}");
                sb.AppendLine($"Missing fact: {activeObj.LastAskedFact}");
                sb.AppendLine();
            }

            if (model.KnownFacts != null && model.KnownFacts.Count > 0)
            {
                sb.AppendLine("Known facts:");
                foreach (var f in model.KnownFacts)
                {
                    sb.AppendLine($"- {f.Key}: {f.Value}");
                }
                sb.AppendLine();
            }

            if (model.OpenQuestions != null && model.OpenQuestions.Count > 0)
            {
                sb.AppendLine("Open questions:");
                foreach (var q in model.OpenQuestions)
                {
                    sb.AppendLine($"- {q.Question} (reason: {q.Reason})");
                }
                sb.AppendLine();
            }

            if (model.Conversation != null && model.Conversation.Count > 0)
            {
                sb.AppendLine("Conversation history (most recent last):");
                foreach (var c in model.Conversation)
                {
                    sb.AppendLine($"{c.Speaker}: {c.Message}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("Ask exactly one concise engineering question (one sentence). Respond with the question only.");
            return sb.ToString();
        }

        private string BuildSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are EngineOS, an experienced engineering lead performing product discovery.");
            sb.AppendLine("Your role is to help an engineer better understand their product idea by asking exactly one concise engineering question.");
            sb.AppendLine("Always ask only one question. Do not provide designs, recommendations, implementation details, code, or architectures.");
            sb.AppendLine("Do not invent requirements or jump ahead. The question should reduce uncertainty about the product and be the single most valuable next question.");
            sb.AppendLine("Respond with the question only, as a single sentence. Do not add commentary, lists, or bullets.");
            return sb.ToString();
        }
    }
}
