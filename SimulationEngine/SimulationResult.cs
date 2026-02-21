namespace StructFlow.SimulationEngine
{
    /// <summary>
    /// SimulationEngine이 반환하는 최종 계산 결과.
    /// 에러 발생 시에도 예외를 던지지 않고 이 객체에 status="ERROR" + reason을 담아 반환한다.
    /// </summary>
    public class SimulationResult
    {
        public string PipeId { get; set; } = string.Empty;
        public string CalculatedAt { get; set; } = DateTime.UtcNow.ToString("o");

        public FlowResult? Flow { get; set; }
        public StressResult? Stress { get; set; }

        public List<string> Warnings { get; set; } = new();

        /// <summary>전체 상태: NORMAL | WARNING | DANGER | ERROR</summary>
        public string OverallStatus { get; set; } = "NORMAL";

        /// <summary>자연어 요약 (Result Reporter 단계에서 표시)</summary>
        public string? Summary { get; set; }

        /// <summary>에러 발생 시 원인 설명</summary>
        public string? ErrorReason { get; set; }

        public static SimulationResult Error(string pipeId, string reason) => new()
        {
            PipeId = pipeId,
            OverallStatus = "ERROR",
            ErrorReason = reason,
            Summary = $"시뮬레이션 오류: {reason}"
        };
    }

    /// <summary>Manning 공식 기반 유량 계산 결과</summary>
    public class FlowResult
    {
        /// <summary>유속 (m/s)</summary>
        public double VelocityMs { get; set; }

        /// <summary>유량 (m³/s)</summary>
        public double FlowRateM3S { get; set; }

        /// <summary>충만율 — 관 단면 대비 유수 단면 비율 (0~1)</summary>
        public double FillRatio { get; set; }

        /// <summary>NORMAL | WARNING | DANGER</summary>
        public string Status { get; set; } = "NORMAL";
    }

    /// <summary>구조 응력 분석 결과</summary>
    public class StressResult
    {
        /// <summary>최대 응력 (kPa)</summary>
        public double MaxStressKpa { get; set; }

        /// <summary>안전율 = 허용응력 / 실제응력</summary>
        public double SafetyFactor { get; set; }

        /// <summary>SAFE | WARNING | DANGER</summary>
        public string Status { get; set; } = "SAFE";
    }
}
