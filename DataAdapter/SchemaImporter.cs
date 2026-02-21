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
        /// LLM이 채운 필드만 덮어쓰고 나머지는 기존 값을 유지한다.
        /// </summary>
        public static (DesignSchema? schema, ValidationResult validation) MergeFromLlmJson(
            string llmJson,
            DesignSchema? baseSchema = null)
        {
            if (!StructFlowJsonSerializer.IsValidJson(llmJson))
                return (null, ValidationResult.Failure("LLM 응답이 유효한 JSON이 아닙니다."));

            var llmPartial = StructFlowJsonSerializer.DeserializeSchema(llmJson);
            if (llmPartial == null)
                return (null, ValidationResult.Failure("LLM 응답 역직렬화에 실패했습니다."));

            var merged = baseSchema ?? new DesignSchema();

            // 파이프 파라미터 — LLM이 명시적으로 값을 준 필드만 덮어쓰기
            if (llmPartial.Pipe != null)
            {
                if (llmPartial.Pipe.DiameterMm > 0) merged.Pipe.DiameterMm = llmPartial.Pipe.DiameterMm;
                if (llmPartial.Pipe.LengthM > 0) merged.Pipe.LengthM = llmPartial.Pipe.LengthM;
                if (!string.IsNullOrWhiteSpace(llmPartial.Pipe.Material)) merged.Pipe.Material = llmPartial.Pipe.Material;
                if (llmPartial.Pipe.Slope > 0) merged.Pipe.Slope = llmPartial.Pipe.Slope;
                if (llmPartial.Pipe.RoughnessCoefficient > 0) merged.Pipe.RoughnessCoefficient = llmPartial.Pipe.RoughnessCoefficient;
                if (!string.IsNullOrWhiteSpace(llmPartial.Pipe.Id)) merged.Pipe.Id = llmPartial.Pipe.Id;
            }

            if (llmPartial.Load != null)
            {
                if (llmPartial.Load.SoilDepthM > 0) merged.Load.SoilDepthM = llmPartial.Load.SoilDepthM;
                if (llmPartial.Load.TrafficLoadKn > 0) merged.Load.TrafficLoadKn = llmPartial.Load.TrafficLoadKn;
                if (llmPartial.Load.InternalPressureKpa > 0) merged.Load.InternalPressureKpa = llmPartial.Load.InternalPressureKpa;
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
