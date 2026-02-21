using System.Text.Json;
using System.Text.Json.Serialization;
using StructFlow.ParametricCore.Models;
using StructFlow.SimulationEngine;

namespace StructFlow.DataAdapter
{
    /// <summary>
    /// 모든 모듈 간 데이터 이동에 사용하는 JSON 직렬화/역직렬화 유틸리티.
    /// 모든 모듈 간 통신은 이 클래스를 통한 JSON 교환만 허용한다.
    /// </summary>
    public static class StructFlowJsonSerializer
    {
        private static readonly JsonSerializerOptions OPTIONS = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
        };

        // ── DesignSchema ──────────────────────────────────────────────────

        /// <summary>DesignSchema 객체를 JSON 문자열로 직렬화</summary>
        public static string SerializeSchema(DesignSchema schema)
        {
            ArgumentNullException.ThrowIfNull(schema);
            return JsonSerializer.Serialize(schema, OPTIONS);
        }

        /// <summary>JSON 문자열을 DesignSchema 객체로 역직렬화. 실패 시 null 반환.</summary>
        public static DesignSchema? DeserializeSchema(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<DesignSchema>(json, OPTIONS);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // ── SimulationResult ─────────────────────────────────────────────

        /// <summary>SimulationResult 객체를 JSON 문자열로 직렬화</summary>
        public static string SerializeResult(SimulationResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            return JsonSerializer.Serialize(result, OPTIONS);
        }

        /// <summary>JSON 문자열을 SimulationResult 객체로 역직렬화. 실패 시 null 반환.</summary>
        public static SimulationResult? DeserializeResult(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<SimulationResult>(json, OPTIONS);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // ── 범용 ──────────────────────────────────────────────────────────

        /// <summary>임의 타입 직렬화 (LLM 응답 파싱 등에 활용)</summary>
        public static string Serialize<T>(T obj) =>
            JsonSerializer.Serialize(obj, OPTIONS);

        /// <summary>임의 타입 역직렬화. 실패 시 default 반환.</summary>
        public static T? Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(json, OPTIONS);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        /// <summary>JSON 형식이 유효한지 검사만 수행</summary>
        public static bool IsValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
