using StructFlow.ParametricCore.Models;

namespace StructFlow.ParametricCore.Validators
{
    /// <summary>
    /// DesignSchema 전체를 검사하는 유효성 검사기.
    /// 각 모듈 파라미터별 범위/형식 오류를 수집하여 ValidationResult로 반환한다.
    /// </summary>
    public static class ParameterValidator
    {
        // 허용 재질 목록
        private static readonly HashSet<string> ALLOWED_MATERIALS =
            new(StringComparer.OrdinalIgnoreCase) { "concrete", "ductile_iron", "pvc", "steel", "hdpe" };

        private static readonly HashSet<string> ALLOWED_FLOW_TYPES =
            new(StringComparer.OrdinalIgnoreCase) { "gravity", "pressure" };

        private static readonly HashSet<string> ALLOWED_FLUIDS =
            new(StringComparer.OrdinalIgnoreCase) { "wastewater", "stormwater", "clean_water" };

        public static ValidationResult Validate(DesignSchema schema)
        {
            var errors = new List<string>();

            ValidatePipe(schema.Pipe, errors);
            ValidateLoad(schema.Load, errors);
            ValidateEnvironment(schema.Environment, errors);

            return errors.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure(errors);
        }

        private static void ValidatePipe(PipeParameter pipe, List<string> errors)
        {
            if (pipe == null)
            {
                errors.Add("Pipe parameter block is missing.");
                return;
            }

            if (string.IsNullOrWhiteSpace(pipe.Id))
                errors.Add("pipe.id is required.");

            // KDS 57 17 00: 최소 관경 100mm, 실무 상한 3000mm
            if (pipe.DiameterMm < 100 || pipe.DiameterMm > 3000)
                errors.Add($"pipe.diameter_mm must be 100–3000 mm. Got: {pipe.DiameterMm}");

            if (pipe.LengthM <= 0 || pipe.LengthM > 10000)
                errors.Add($"pipe.length_m must be 0–10000 m. Got: {pipe.LengthM}");

            if (!ALLOWED_MATERIALS.Contains(pipe.Material ?? string.Empty))
                errors.Add($"pipe.material must be one of [{string.Join(", ", ALLOWED_MATERIALS)}]. Got: '{pipe.Material}'");

            // KDS 기준: 최소 경사 0.001 (0.1%), 최대 0.2 (20%)
            if (pipe.Slope < 0.001 || pipe.Slope > 0.2)
                errors.Add($"pipe.slope must be 0.001–0.2. Got: {pipe.Slope}");

            // Manning n 값: 콘크리트 0.011–0.015, 타 재질 포함 상한 0.05
            if (pipe.RoughnessCoefficient < 0.005 || pipe.RoughnessCoefficient > 0.05)
                errors.Add($"pipe.roughness_coefficient must be 0.005–0.05. Got: {pipe.RoughnessCoefficient}");
        }

        private static void ValidateLoad(LoadParameter load, List<string> errors)
        {
            if (load == null)
            {
                errors.Add("Load parameter block is missing.");
                return;
            }

            if (load.SoilDepthM < 0 || load.SoilDepthM > 30)
                errors.Add($"load.soil_depth_m must be 0–30 m. Got: {load.SoilDepthM}");

            if (load.TrafficLoadKn < 0 || load.TrafficLoadKn > 500)
                errors.Add($"load.traffic_load_kn must be 0–500 kN. Got: {load.TrafficLoadKn}");

            if (load.InternalPressureKpa < 0 || load.InternalPressureKpa > 1000)
                errors.Add($"load.internal_pressure_kpa must be 0–1000 kPa. Got: {load.InternalPressureKpa}");
        }

        private static void ValidateEnvironment(EnvironmentParameter env, List<string> errors)
        {
            if (env == null)
            {
                errors.Add("Environment parameter block is missing.");
                return;
            }

            if (!ALLOWED_FLOW_TYPES.Contains(env.FlowType ?? string.Empty))
                errors.Add($"environment.flow_type must be one of [{string.Join(", ", ALLOWED_FLOW_TYPES)}]. Got: '{env.FlowType}'");

            if (!ALLOWED_FLUIDS.Contains(env.Fluid ?? string.Empty))
                errors.Add($"environment.fluid must be one of [{string.Join(", ", ALLOWED_FLUIDS)}]. Got: '{env.Fluid}'");
        }
    }
}
