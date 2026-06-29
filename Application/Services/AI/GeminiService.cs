using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EgyptOnline.Application.Configuration;

namespace EgyptOnline.Services
{
    /// <summary>
    /// Sends a single-turn chat message to the Gemini REST API and returns the reply.
    /// Uses the AiConfig system instruction as guardrail / persona for every request.
    /// </summary>
    public class GeminiService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly AiConfig _aiConfig;

        // Gemini generateContent endpoint (v1beta supports system_instruction)
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

        public GeminiService(HttpClient http, IConfiguration config, AiConfig aiConfig)
        {
            _http = http;
            _apiKey = config["Gemini:ApiKey"]
                      ?? throw new InvalidOperationException("Gemini:ApiKey is not configured.");
            _model = config["Gemini:Model"] ?? "gemini-1.5-flash";
            _aiConfig = aiConfig;
        }

        /// <summary>
        /// Sends a user message to Gemini and returns the assistant reply text.
        /// </summary>
        public async Task<string> ChatAsync(string userMessage, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                throw new ArgumentException("Message cannot be empty.", nameof(userMessage));

            var url = string.Format(BaseUrl, _model, _apiKey);

            // Build the request payload
            // system_instruction sets the persona/guardrails for the whole conversation
            var payload = new
            {
                system_instruction = new
                {
                    parts = new[]
                    {
                        new { text = _aiConfig.BuildSystemInstruction() }
                    }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userMessage } }
                    }
                },
                generationConfig = new
                {
                    temperature = _aiConfig.Temperature,
                    maxOutputTokens = _aiConfig.MaxOutputTokens
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"Gemini API error {(int)response.StatusCode}: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

            // Navigate: candidates[0].content.parts[0].text
            var text = result
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text ?? string.Empty;
        }
    }
}
