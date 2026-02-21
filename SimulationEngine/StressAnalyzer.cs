using StructFlow.ParametricCore.Models;

namespace StructFlow.SimulationEngine
{
    /// <summary>
    /// 하수관로 구조 응력 분석기.
    /// 토압 + 교통 하중 + 내압을 조합하여 최대 응력과 안전율을 계산한다.
    ///
    /// 참고 기준:
    ///   - KS D 4301 (원심력 철근 콘크리트관) 허용응력 기준
    ///   - Marston 토압 이론 (토피고 기반 토압 산정)
    ///
    /// 안전율 판정 (KS D 4301 기준):
    ///   SAFE    : SF >= 2.0
    ///   WARNING : 1.5 <= SF < 2.0
    ///   DANGER  : SF < 1.5
    /// </summary>
    public static class StressAnalyzer
    {
        // KS D 4301 기준 콘크리트관 허용응력 (kPa)
        private const double ALLOWABLE_STRESS_CONCRETE_KPA = 400.0;

        // 재질별 허용응력 맵 (kPa)
        private static readonly Dictionary<string, double> ALLOWABLE_STRESS_MAP =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["concrete"]     = 400.0,
                ["ductile_iron"] = 1500.0,
                ["pvc"]          = 200.0,
                ["steel"]        = 1200.0,
                ["hdpe"]         = 180.0,
            };

        // 흙 단위중량 (kN/m³) — 일반 포화 점성토 기준
        private const double SOIL_UNIT_WEIGHT_KN_M3 = 19.0;

        // Marston 하중 계수 (매설 조건: 도랑식 매설)
        private const double MARSTON_LOAD_FACTOR = 1.5;

        private const double SAFETY_FACTOR_SAFE    = 2.0;
        private const double SAFETY_FACTOR_WARNING = 1.5;

        /// <summary>
        /// 파이프 파라미터와 하중 조건으로 응력을 계산한다.
        /// </summary>
        public static StressResult Analyze(PipeParameter pipe, LoadParameter load)
        {
            double allowableStress = GetAllowableStress(pipe.Material);
            double maxStressKpa    = CalculateMaxStress(pipe, load);
            double safetyFactor    = allowableStress / maxStressKpa;

            string status = DetermineStressStatus(safetyFactor);

            return new StressResult
            {
                MaxStressKpa  = Math.Round(maxStressKpa, 2),
                SafetyFactor  = Math.Round(safetyFactor, 3),
                Status        = status
            };
        }

        /// <summary>
        /// 전체 작용 응력 계산 (단순화된 링 이론 기반).
        ///
        /// 링 응력(hoop stress) 공식:
        ///   σ = (W_total × D) / (2 × t)
        ///   W_total = 토압 + 교통 하중 환산 + 내압
        ///
        /// 관 두께 t는 직경의 10% 가정 (KS 표준 최소 두께 근사).
        /// </summary>
        private static double CalculateMaxStress(PipeParameter pipe, LoadParameter load)
        {
            double diameterM = pipe.DiameterMm / 1000.0;
            double wallThicknessM = diameterM * 0.10; // 관 두께 = 직경의 10% 가정

            // 토압 (kPa) — Marston 공식 간략화: W_e = γ × H × Cd
            // Cd: Marston 계수 (도랑식 매설 기준 1.5 적용)
            double earthPressureKpa = SOIL_UNIT_WEIGHT_KN_M3 * load.SoilDepthM * MARSTON_LOAD_FACTOR;

            // 교통 하중 → 관상부 분포 압력 환산 (kPa)
            // Boussinesq 분포 간략화: q = P / (π × r²) — 영향 반경 = 관경
            double loadDistAreaM2 = Math.PI * Math.Pow(diameterM / 2.0, 2.0);
            double trafficPressureKpa = load.TrafficLoadKn / loadDistAreaM2;

            // 내압 (kPa) — 직접 적용
            double internalPressureKpa = load.InternalPressureKpa;

            // 총 작용 하중 (kPa)
            double totalLoadKpa = earthPressureKpa + trafficPressureKpa + internalPressureKpa;

            // 링 응력: σ = (W × D) / (2 × t)  [kPa × m / m = kPa]
            double hoopStressKpa = (totalLoadKpa * diameterM) / (2.0 * wallThicknessM);

            return hoopStressKpa;
        }

        private static double GetAllowableStress(string material)
        {
            return ALLOWABLE_STRESS_MAP.TryGetValue(material ?? "concrete", out double stress)
                ? stress
                : ALLOWABLE_STRESS_CONCRETE_KPA;
        }

        private static string DetermineStressStatus(double safetyFactor)
        {
            if (safetyFactor >= SAFETY_FACTOR_SAFE)    return "SAFE";
            if (safetyFactor >= SAFETY_FACTOR_WARNING) return "WARNING";
            return "DANGER";
        }
    }
}
