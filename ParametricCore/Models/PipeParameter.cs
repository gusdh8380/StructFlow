using StructFlow.ParametricCore.Interfaces;

namespace StructFlow.ParametricCore.Models
{
    /// <summary>
    /// 하수관로 파이프 기본 설계 파라미터.
    /// 단위 규칙: 길이는 _mm 또는 _m 접미사로 명시.
    /// </summary>
    public class PipeParameter : IDesignParameter
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>관경 (mm)</summary>
        public double DiameterMm { get; set; }

        /// <summary>관 연장 (m)</summary>
        public double LengthM { get; set; }

        /// <summary>관 재질 (concrete, ductile_iron, pvc 등)</summary>
        public string Material { get; set; } = "concrete";

        /// <summary>관로 경사 (무차원, 예: 0.005 = 0.5%)</summary>
        public double Slope { get; set; }

        /// <summary>Manning 조도계수 (콘크리트 기본값: 0.013)</summary>
        public double RoughnessCoefficient { get; set; } = 0.013;

        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                reason = "Id is required.";
                return false;
            }
            if (DiameterMm <= 0 || DiameterMm > 5000)
            {
                reason = $"DiameterMm must be between 0 and 5000. Got: {DiameterMm}";
                return false;
            }
            if (LengthM <= 0 || LengthM > 10000)
            {
                reason = $"LengthM must be between 0 and 10000. Got: {LengthM}";
                return false;
            }
            if (Slope <= 0 || Slope > 1.0)
            {
                reason = $"Slope must be between 0 and 1.0. Got: {Slope}";
                return false;
            }
            if (RoughnessCoefficient <= 0)
            {
                reason = $"RoughnessCoefficient must be positive. Got: {RoughnessCoefficient}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public IDesignParameter WithConservativeDefaults()
        {
            return new PipeParameter
            {
                Id = string.IsNullOrWhiteSpace(Id) ? "PIPE-DEFAULT" : Id,
                DiameterMm = DiameterMm > 0 ? DiameterMm : 300,
                LengthM = LengthM > 0 ? LengthM : 50.0,
                Material = string.IsNullOrWhiteSpace(Material) ? "concrete" : Material,
                Slope = Slope > 0 ? Slope : 0.005,
                RoughnessCoefficient = RoughnessCoefficient > 0 ? RoughnessCoefficient : 0.013
            };
        }
    }

    /// <summary>외부 하중 파라미터 (토압, 교통 하중, 내압)</summary>
    public class LoadParameter
    {
        /// <summary>관상부 토피고 (m)</summary>
        public double SoilDepthM { get; set; } = 2.0;

        /// <summary>교통 하중 (kN)</summary>
        public double TrafficLoadKn { get; set; } = 50.0;

        /// <summary>내부 수압 (kPa)</summary>
        public double InternalPressureKpa { get; set; } = 10.0;
    }

    /// <summary>환경/운영 조건 파라미터</summary>
    public class EnvironmentParameter
    {
        /// <summary>흐름 유형: gravity | pressure</summary>
        public string FlowType { get; set; } = "gravity";

        /// <summary>유체 종류: wastewater | stormwater | clean_water</summary>
        public string Fluid { get; set; } = "wastewater";
    }
}
