using System.Text.Json;
using System.Text.RegularExpressions;
using StructFlow.DataAdapter;
using StructFlow.ParametricCore.Models;

namespace StructFlow.LLMConnector
{
    /// <summary>
    /// LLM(Claude) 응답 텍스트에서 JSON을 추출하고 DesignSchema로 변환한다.
    /// LLM이 JSON 외 텍스트를 포함하더라도 JSON 블록만 추출한다.
    /// </summary>
    public static class ParameterParser
    {
        // JSON 블록 추출용 패턴 (마크다운 코드 블록 또는 순수 JSON 객체)
        private static readonly Regex JSON_BLOCK_PATTERN =
            new(@"```(?:json)?\s*(\{[\s\S]*?\})\s*```|(\{[\s\S]*\})", RegexOptions.Compiled);

        /// <summary>
        /// LLM 응답 텍스트로부터 DesignSchema를 파싱한다.
        /// </summary>
        /// <param name="llmResponseText">Claude API content[0].text</param>
        /// <param name="baseSchema">기존 스키마 (병합 기준, null이면 새 스키마)</param>
        /// <returns>(schema, errorMessage) — 파싱 실패 시 schema는 null</returns>
        public static (DesignSchema? schema, string? error) Parse(
            string llmResponseText,
            DesignSchema? baseSchema = null)
        {
            var json = ExtractJson(llmResponseText);
            if (json == null)
                return (null, "LLM 응답에서 JSON을 찾을 수 없습니다.");

            // "reason" 필드 확인 — LLM이 변환 불가 이유를 반환한 경우
            var reason = ExtractReason(json);
            if (reason != null)
            {
                var failSchema = new DesignSchema { LlmParseFailReason = reason };
                return (failSchema, $"LLM 파라미터 추출 실패: {reason}");
            }

            var (merged, validation) = SchemaImporter.MergeFromLlmJson(json, baseSchema);

            if (merged == null)
                return (null, "LLM JSON 역직렬화에 실패했습니다.");

            if (!validation.IsValid)
                return (merged, $"유효성 검사 실패: {string.Join(", ", validation.Errors)}");

            return (merged, null);
        }

        /// <summary>LLM 응답 텍스트에서 JSON 객체 문자열만 추출</summary>
        private static string? ExtractJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // 먼저 마크다운 코드 블록 시도
            var match = JSON_BLOCK_PATTERN.Match(text);
            if (match.Success)
            {
                return match.Groups[1].Success ? match.Groups[1].Value
                     : match.Groups[2].Value;
            }

            // 중괄호로 시작하는 JSON 직접 탐색
            int start = text.IndexOf('{');
            int end   = text.LastIndexOf('}');
            if (start >= 0 && end > start)
                return text[start..(end + 1)];

            return null;
        }

        /// <summary>LLM이 반환한 reason 필드 추출 (변환 불가 신호)</summary>
        private static string? ExtractReason(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("reason", out var reasonElement)
                    && reasonElement.ValueKind == JsonValueKind.String)
                {
                    var value = reasonElement.GetString();
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            catch (JsonException) { /* 무시 */ }
            return null;
        }
    }
}
