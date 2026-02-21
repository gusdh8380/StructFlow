using StructFlow.SimulationEngine;

namespace StructFlow.DataAdapter
{
    /// <summary>
    /// SimulationResult를 다양한 포맷으로 내보내는 익스포터.
    /// Unity, REST API 응답, 파일 저장 등 모든 출력 경로를 담당한다.
    /// </summary>
    public static class ResultExporter
    {
        /// <summary>SimulationResult를 JSON 문자열로 변환</summary>
        public static string ToJson(SimulationResult result) =>
            StructFlowJsonSerializer.SerializeResult(result);

        /// <summary>SimulationResult를 파일로 저장 (비동기)</summary>
        public static async Task SaveToFileAsync(SimulationResult result, string filePath)
        {
            var json = ToJson(result);
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>
        /// Unity ResultPanel에 전달하기 위한 경량 요약 딕셔너리 반환.
        /// Unity는 전체 JSON 대신 이 딕셔너리를 파싱하여 UI에 표시한다.
        /// </summary>
        public static Dictionary<string, string> ToUnitySummary(SimulationResult result)
        {
            var flow = result.Flow;
            var stress = result.Stress;

            return new Dictionary<string, string>
            {
                ["pipe_id"]        = result.PipeId,
                ["calculated_at"]  = result.CalculatedAt,
                ["status"]         = result.OverallStatus,
                ["velocity_ms"]    = flow != null ? $"{flow.VelocityMs:F2} m/s" : "N/A",
                ["flow_rate_m3s"]  = flow != null ? $"{flow.FlowRateM3S:F4} m³/s" : "N/A",
                ["fill_ratio"]     = flow != null ? $"{flow.FillRatio:P0}" : "N/A",
                ["flow_status"]    = flow?.Status ?? "N/A",
                ["safety_factor"]  = stress != null ? $"{stress.SafetyFactor:F2}" : "N/A",
                ["stress_status"]  = stress?.Status ?? "N/A",
                ["summary"]        = result.Summary ?? string.Empty,
                ["warnings"]       = result.Warnings.Count > 0
                                        ? string.Join("; ", result.Warnings)
                                        : "없음"
            };
        }

        /// <summary>
        /// 자연어 요약 문자열 생성 (Result Reporter 단계에서 LLM에 전달하거나 직접 표시)
        /// </summary>
        public static string ToNaturalLanguageSummary(SimulationResult result)
        {
            var lines = new List<string>
            {
                $"[{result.PipeId}] 시뮬레이션 결과 — {result.CalculatedAt}",
                $"전체 상태: {result.OverallStatus}"
            };

            if (result.Flow != null)
            {
                lines.Add($"유량: {result.Flow.FlowRateM3S:F4} m³/s | 유속: {result.Flow.VelocityMs:F2} m/s | 충만율: {result.Flow.FillRatio:P0} [{result.Flow.Status}]");
            }

            if (result.Stress != null)
            {
                lines.Add($"최대응력: {result.Stress.MaxStressKpa:F1} kPa | 안전율: {result.Stress.SafetyFactor:F2} [{result.Stress.Status}]");
            }

            if (result.Warnings.Count > 0)
            {
                lines.Add($"경고: {string.Join(", ", result.Warnings)}");
            }

            lines.Add(result.Summary ?? string.Empty);

            return string.Join("\n", lines);
        }
    }
}
