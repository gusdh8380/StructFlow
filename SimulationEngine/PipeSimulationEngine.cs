using StructFlow.ParametricCore.Models;
using StructFlow.SimulationEngine.Interfaces;

namespace StructFlow.SimulationEngine
{
    /// <summary>
    /// 파이프 도메인 시뮬레이션 엔진 진입점.
    /// FlowCalculator + StressAnalyzer를 조율하고 최종 SimulationResult를 조립한다.
    /// ISimulationEngine 구현 — 도메인 교체 시 이 클래스만 교체.
    /// </summary>
    public sealed class PipeSimulationEngine : ISimulationEngine
    {
        public SimulationResult Run(DesignSchema schema)
        {
            if (schema == null)
                return SimulationResult.Error("UNKNOWN", "DesignSchema가 null입니다.");

            if (!schema.IsValidated)
                return SimulationResult.Error(schema.Pipe?.Id ?? "UNKNOWN",
                    "유효성 검사를 통과하지 않은 스키마입니다. DataAdapter를 통해 검사 후 전달하세요.");

            try
            {
                var flowResult   = FlowCalculator.Calculate(schema.Pipe);
                var stressResult = StressAnalyzer.Analyze(schema.Pipe, schema.Load);

                var warnings = new List<string>();
                string overallStatus = DetermineOverallStatus(flowResult, stressResult, warnings);

                string summary = BuildSummary(overallStatus, flowResult, stressResult);

                return new SimulationResult
                {
                    PipeId        = schema.Pipe.Id,
                    CalculatedAt  = DateTime.UtcNow.ToString("o"),
                    Flow          = flowResult,
                    Stress        = stressResult,
                    Warnings      = warnings,
                    OverallStatus = overallStatus,
                    Summary       = summary
                };
            }
            catch (Exception ex)
            {
                return SimulationResult.Error(
                    schema.Pipe?.Id ?? "UNKNOWN",
                    $"계산 중 예상치 못한 오류 발생: {ex.Message}");
            }
        }

        private static string DetermineOverallStatus(
            FlowResult flow,
            StressResult stress,
            List<string> warnings)
        {
            // 더 심각한 상태가 전체 상태를 결정
            int flowSeverity   = StatusToSeverity(flow.Status);
            int stressSeverity = StatusToSeverity(stress.Status);

            int maxSeverity = Math.Max(flowSeverity, stressSeverity);

            if (flow.Status == "WARNING" || flow.Status == "DANGER")
                warnings.Add($"유량 상태: {flow.Status}");

            if (stress.Status == "WARNING" || stress.Status == "DANGER")
                warnings.Add($"응력 상태: {stress.Status}");

            return SeverityToStatus(maxSeverity);
        }

        private static int StatusToSeverity(string status) => status switch
        {
            "NORMAL" or "SAFE" => 0,
            "WARNING"          => 1,
            "DANGER"           => 2,
            _                  => 3
        };

        private static string SeverityToStatus(int severity) => severity switch
        {
            0 => "NORMAL",
            1 => "WARNING",
            2 => "DANGER",
            _ => "ERROR"
        };

        private static string BuildSummary(
            string overallStatus,
            FlowResult flow,
            StressResult stress) => overallStatus switch
        {
            "NORMAL"  => $"설계 기준 내 정상 범위입니다. 유속 {flow.VelocityMs:F2} m/s, 안전율 {stress.SafetyFactor:F2}.",
            "WARNING" => $"주의 필요. 유량 상태: {flow.Status}, 응력 안전율: {stress.SafetyFactor:F2} (경계값).",
            "DANGER"  => $"설계 기준 초과. 즉각적인 검토가 필요합니다. 유량: {flow.Status}, 응력: {stress.Status}.",
            _         => "시뮬레이션 오류 — 파라미터를 확인하세요."
        };
    }
}
