using System.Text.Json;
using StructFlow.ParametricCore.Models;
using StructFlow.ParametricCore.Validators;

namespace StructFlow.DataAdapter
{
    /// <summary>
    /// 외부 소스(파일, HTTP body, LLM 응답)로부터 DesignSchema를 가져오고
    /// 유효성 검사까지 수행하는 임포터.
    /// </summary>
    public static class SchemaImporter
    {
        /// <summary>
        /// JSON 문자열로부터 DesignSchema를 파싱하고 유효성을 검사한다.
        /// </summary>
        /// <param name="json">LLM 또는 n8n에서 전달된 JSON 문자열</param>
        /// <param name="applyConservativeDefaults">누락 필드에 보수적 기본값 적용 여부</param>
        /// <returns>(schema, validationResult) 튜플. 파싱 실패 시 schema는 null.</returns>
        public static (DesignSchema? schema, ValidationResult validation) ImportFromJson(
            string json,
            bool applyConservativeDefaults = true)
        {
            var schema = StructFlowJsonSerializer.DeserializeSchema(json);

            if (schema == null)
                return (null, ValidationResult.Failure("JSON 파싱 실패: 유효하지 않은 형식입니다."));

            if (applyConservativeDefaults)
                ApplyConservativeDefaults(schema);

            var validation = ParameterValidator.Validate(schema);
            schema.IsValidated = validation.IsValid;

            return (schema, validation);
        }

        /// <summary>
        /// 파일 경로로부터 DesignSchema를 로드한다.
        /// </summary>
        public static async Task<(DesignSchema? schema, ValidationResult validation)> ImportFromFileAsync(
            string filePath,
            bool applyConservativeDefaults = true)
        {
            if (!File.Exists(filePath))
                return (null, ValidationResult.Failure($"파일을 찾을 수 없습니다: {filePath}"));

            var json = await File.ReadAllTextAsync(filePath);
            return ImportFromJson(json, applyConservativeDefaults);
        }

        /// <summary>
        /// LLM이 반환한 부분 JSON(일부 필드만 포함)과 기존 스키마를 병합한다.
        /// JsonDocument로 실제 JSON에 존재하는 필드만 감지하여 덮어쓴다.
        /// C# 기본값(예: Material = "concrete")이 자동으로 base를 덮어쓰는 버그를 방지한다.
        /// </summary>
        public static (DesignSchema? schema, ValidationResult validation) MergeFromLlmJson(
            string llmJson,
            DesignSchema? baseSchema = null)
        {
            if (!StructFlowJsonSerializer.IsValidJson(llmJson))
                return (null, ValidationResult.Failure("LLM 응답이 유효한 JSON이 아닙니다."));

            var merged = new DesignSchema();
            // base 값을 먼저 복사
            if (baseSchema != null)
            {
                merged.Pipe = new() {
                    Id = baseSchema.Pipe.Id,
                    DiameterMm = baseSchema.Pipe.DiameterMm,
                    LengthM = baseSchema.Pipe.LengthM,
                    Material = baseSchema.Pipe.Material,
                    Slope = baseSchema.Pipe.Slope,
                    RoughnessCoefficient = baseSchema.Pipe.RoughnessCoefficient
                };
                merged.Load = new() {
                    SoilDepthM = baseSchema.Load.SoilDepthM,
                    TrafficLoadKn = baseSchema.Load.TrafficLoadKn,
                    InternalPressureKpa = baseSchema.Load.InternalPressureKpa
                };
                merged.Environment = new() {
                    FlowType = baseSchema.Environment.FlowType,
                    Fluid = baseSchema.Environment.Fluid
                };
            }

            // JsonDocument로 실제 JSON에 존재하는 필드만 병합
            try
            {
                using var doc = JsonDocument.Parse(llmJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("pipe", out var pipe))
                {
                    if (pipe.TryGetProperty("id", out var v) && v.ValueKind == JsonValueKind.String)
                        merged.Pipe.Id = v.GetString()!;
                    if (pipe.TryGetProperty("diameter_mm", out v) && v.ValueKind == JsonValueKind.Number)
                        merged.Pipe.DiameterMm = v.GetDouble();
                    if (pipe.TryGetProperty("length_m", out v) && v.ValueKind == JsonValueKind.Number)
                        merged.Pipe.LengthM = v.GetDouble();
                    if (pipe.TryGetProperty("material", out v) && v.ValueKind == JsonValueKind.String)
                        merged.Pipe.Material = v.GetString()!;
                    if (pipe.TryGetProperty("slope", out v) && v.ValueKind == JsonValueKind.Number)
                        merged.Pipe.Slope = v.GetDouble();
                    if (pipe.TryGetProperty("roughness_coefficient", out v) && v.ValueKind == JsonValueKind.Number)
                        merged.Pipe.RoughnessCoefficient = v.GetDouble();
                }

                if (root.TryGetProperty("load", out var load))
                {
                    if (load.TryGetProperty("soil_depth_m", out var v) && v.ValueKind == JsonValueKind.Number)
                        merged.Load.SoilDepthM = v.GetDouble();
                    if (load.TryGetProperty("traffic_load_kn", out v) && v.ValueKind == JsonValueKind.Number)
                        merged.Load.TrafficLoadKn = v.GetDouble();
                    if (load.TryGetProperty("internal_pressure_kpa", out v) && v.ValueKind == JsonValueKind.Number)
                        merged.Load.InternalPressureKpa = v.GetDouble();
                }

                if (root.TryGetProperty("environment", out var env))
                {
                    if (env.TryGetProperty("flow_type", out var v) && v.ValueKind == JsonValueKind.String)
                        merged.Environment.FlowType = v.GetString()!;
                    if (env.TryGetProperty("fluid", out v) && v.ValueKind == JsonValueKind.String)
                        merged.Environment.Fluid = v.GetString()!;
                }
            }
            catch (JsonException ex)
            {
                return (null, ValidationResult.Failure($"LLM JSON 파싱 오류: {ex.Message}"));
            }

            ApplyConservativeDefaults(merged);

            var validation = ParameterValidator.Validate(merged);
            merged.IsValidated = validation.IsValid;

            return (merged, validation);
        }

        private static void ApplyConservativeDefaults(DesignSchema schema)
        {
            schema.Pipe ??= new();
            schema.Load ??= new();
            schema.Environment ??= new();

            if (string.IsNullOrWhiteSpace(schema.Pipe.Id))
                schema.Pipe.Id = $"PIPE-{DateTime.UtcNow:yyyyMMddHHmmss}";

            if (schema.Pipe.DiameterMm <= 0) schema.Pipe.DiameterMm = 300;
            if (schema.Pipe.LengthM <= 0) schema.Pipe.LengthM = 50.0;
            if (string.IsNullOrWhiteSpace(schema.Pipe.Material)) schema.Pipe.Material = "concrete";
            if (schema.Pipe.Slope <= 0) schema.Pipe.Slope = 0.005;
            if (schema.Pipe.RoughnessCoefficient <= 0) schema.Pipe.RoughnessCoefficient = 0.013;

            if (schema.Load.SoilDepthM <= 0) schema.Load.SoilDepthM = 2.0;
            if (schema.Load.TrafficLoadKn <= 0) schema.Load.TrafficLoadKn = 50.0;
            if (schema.Load.InternalPressureKpa <= 0) schema.Load.InternalPressureKpa = 10.0;

            if (string.IsNullOrWhiteSpace(schema.Environment.FlowType)) schema.Environment.FlowType = "gravity";
            if (string.IsNullOrWhiteSpace(schema.Environment.Fluid)) schema.Environment.Fluid = "wastewater";
        }
    }
}
