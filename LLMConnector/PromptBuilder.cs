namespace StructFlow.LLMConnector
{
    /// <summary>
    /// Claude API에 전달할 시스템 프롬프트와 사용자 메시지를 조립한다.
    ///
    /// 프롬프트 원칙 (CLAUDE.md 기준):
    ///   - 항상 JSON만 반환하도록 강제
    ///   - 파라미터 범위 벗어날 경우 null + reason 필드 포함
    ///   - 단위 통일: mm, m, kN, kPa
    ///   - 모호한 입력은 보수적 기본값 사용
    /// </summary>
    public static class PromptBuilder
    {
        public static string BuildSystemPrompt() => @"
You are a structural design parameter extraction assistant for drainage pipe systems.

RULES (MUST FOLLOW):
1. Respond ONLY with valid JSON. No explanation, no markdown, no extra text.
2. Extract pipe design parameters from the user's natural language input.
3. If a value cannot be determined, use null and include a ""reason"" field.
4. Use ONLY these units: diameter in mm, length in m, slope as decimal (0.005 = 0.5%), loads in kN or kPa.
5. For ambiguous inputs, use conservative (safe) defaults.
6. material must be one of: concrete, ductile_iron, pvc, steel, hdpe

OUTPUT SCHEMA:
{
  ""pipe"": {
    ""id"": ""PIPE-001"",
    ""diameter_mm"": <number | null>,
    ""length_m"": <number | null>,
    ""material"": <string | null>,
    ""slope"": <number | null>,
    ""roughness_coefficient"": <number | null>
  },
  ""load"": {
    ""soil_depth_m"": <number | null>,
    ""traffic_load_kn"": <number | null>,
    ""internal_pressure_kpa"": <number | null>
  },
  ""environment"": {
    ""flow_type"": ""gravity"" | ""pressure"",
    ""fluid"": ""wastewater"" | ""stormwater"" | ""clean_water""
  },
  ""reason"": <string | null>
}

CONSERVATIVE DEFAULTS (use when ambiguous):
- diameter_mm: 300
- length_m: 50.0
- material: ""concrete""
- slope: 0.005
- roughness_coefficient: 0.013
- soil_depth_m: 2.0
- traffic_load_kn: 50.0
- internal_pressure_kpa: 10.0
- flow_type: ""gravity""
- fluid: ""wastewater""
".Trim();

        public static string BuildUserMessage(string naturalLanguageInput) =>
            $"Extract pipe design parameters from the following input:\n\n\"{naturalLanguageInput}\"";

        /// <summary>
        /// Claude API 요청 body 조립.
        /// </summary>
        public static object BuildClaudeRequestBody(string naturalLanguageInput, string modelId = "claude-sonnet-4-6") =>
            new
            {
                model = modelId,
                max_tokens = 1024,
                system = BuildSystemPrompt(),
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = BuildUserMessage(naturalLanguageInput)
                    }
                }
            };
    }
}
