using System.Text;
using System.Text.Json;
using StructFlow.DataAdapter;
using StructFlow.ParametricCore.Models;
using StructFlow.SimulationEngine;

namespace StructFlow.LLMConnector
{
    /// <summary>
    /// n8n Webhook으로부터 자연어 입력을 수신하고, Claude API를 호출하여
    /// DesignSchema를 생성한 뒤 SimulationEngine에 전달하는 파이프라인 진입점.
    ///
    /// 사용 예시 (ASP.NET):
    ///   app.MapPost("/api/design", async (HttpRequest req) =>
    ///       await WebhookReceiver.HandleAsync(req, apiKey));
    /// </summary>
    public static class WebhookReceiver
    {
        private const string CLAUDE_API_URL = "https://api.anthropic.com/v1/messages";
        private const string ANTHROPIC_VERSION = "2023-06-01";

        /// <summary>
        /// n8n Webhook 요청 처리 — 자연어 → 파라미터 → 시뮬레이션 → 결과 반환
        /// </summary>
        /// <param name="naturalLanguageInput">사용자 자연어 입력</param>
        /// <param name="anthropicApiKey">Claude API 키</param>
        /// <param name="modelId">사용할 Claude 모델 ID</param>
        public static async Task<WebhookResponse> HandleAsync(
            string naturalLanguageInput,
            string anthropicApiKey,
            string modelId = "claude-sonnet-4-6")
        {
            if (string.IsNullOrWhiteSpace(naturalLanguageInput))
                return WebhookResponse.Failure("입력 텍스트가 비어 있습니다.");

            // 1. Claude API 호출
            var llmResponseText = await CallClaudeApiAsync(naturalLanguageInput, anthropicApiKey, modelId);
            if (llmResponseText == null)
                return WebhookResponse.Failure("Claude API 호출에 실패했습니다.");

            // 2. LLM 응답 파싱 → DesignSchema
            var (schema, parseError) = ParameterParser.Parse(llmResponseText);
            if (schema == null)
                return WebhookResponse.Failure($"파라미터 파싱 실패: {parseError}");

            if (!schema.IsValidated)
                return WebhookResponse.Failure($"유효성 검사 실패: {schema.LlmParseFailReason ?? parseError}");

            // 3. SimulationEngine 실행
            var engine = new PipeSimulationEngine();
            var result = engine.Run(schema);

            // 4. 결과 반환
            var resultJson   = StructFlowJsonSerializer.SerializeResult(result);
            var schemaJson   = StructFlowJsonSerializer.SerializeSchema(schema);
            var naturalSummary = ResultExporter.ToNaturalLanguageSummary(result);

            return new WebhookResponse
            {
                Success       = result.OverallStatus != "ERROR",
                SchemaJson    = schemaJson,
                ResultJson    = resultJson,
                NaturalSummary = naturalSummary,
                OverallStatus = result.OverallStatus
            };
        }

        private static async Task<string?> CallClaudeApiAsync(
            string input,
            string apiKey,
            string modelId)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", ANTHROPIC_VERSION);

            var requestBody = PromptBuilder.BuildClaudeRequestBody(input, modelId);
            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(CLAUDE_API_URL, content);
                if (!response.IsSuccessStatusCode) return null;

                var responseBody = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseBody);

                // Claude 응답 구조: { "content": [{ "type": "text", "text": "..." }] }
                return doc.RootElement
                          .GetProperty("content")[0]
                          .GetProperty("text")
                          .GetString();
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>Webhook 처리 결과 DTO</summary>
    public class WebhookResponse
    {
        public bool Success { get; set; }
        public string? SchemaJson { get; set; }
        public string? ResultJson { get; set; }
        public string? NaturalSummary { get; set; }
        public string OverallStatus { get; set; } = "ERROR";
        public string? ErrorMessage { get; set; }

        public static WebhookResponse Failure(string message) => new()
        {
            Success = false,
            OverallStatus = "ERROR",
            ErrorMessage = message
        };
    }
}
