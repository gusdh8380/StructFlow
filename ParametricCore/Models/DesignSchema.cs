namespace StructFlow.ParametricCore.Models
{
    /// <summary>
    /// 모든 설계 파라미터를 하나로 묶는 루트 스키마.
    /// DataAdapter를 통해 JSON으로 직렬화/역직렬화되어 모듈 간 이동한다.
    /// </summary>
    public class DesignSchema
    {
        /// <summary>스키마 버전 (파이프라인 호환성 관리)</summary>
        public string SchemaVersion { get; set; } = "1.0";

        /// <summary>생성 타임스탬프 (ISO 8601)</summary>
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");

        /// <summary>파이프 기본 파라미터</summary>
        public PipeParameter Pipe { get; set; } = new();

        /// <summary>하중 파라미터</summary>
        public LoadParameter Load { get; set; } = new();

        /// <summary>환경 파라미터</summary>
        public EnvironmentParameter Environment { get; set; } = new();

        /// <summary>LLM이 파라미터를 채울 수 없었을 때 반환하는 이유</summary>
        public string? LlmParseFailReason { get; set; }

        /// <summary>설계 유량 (m³/s). null이면 PipeSimulationEngine이 만관의 75%로 자동 설정.</summary>
        public double? DesignFlowM3S { get; set; } = null;

        /// <summary>스키마 전체 유효성 여부 (DataAdapter가 검사 후 세팅)</summary>
        public bool IsValidated { get; set; } = false;
    }
}
