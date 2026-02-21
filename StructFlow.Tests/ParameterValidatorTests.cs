using StructFlow.ParametricCore.Models;
using StructFlow.ParametricCore.Validators;

namespace StructFlow.Tests;

/// <summary>
/// ParameterValidator 단위 테스트
/// 경계값 분석(BVA) 기반 — 정상/경계/오류 케이스 분리
/// </summary>
public class ParameterValidatorTests
{
    private static DesignSchema ValidSchema() => new()
    {
        Pipe = new PipeParameter
        {
            Id = "PIPE-001",
            DiameterMm = 300,
            LengthM = 50,
            Material = "concrete",
            Slope = 0.005,
            RoughnessCoefficient = 0.013
        },
        Load = new LoadParameter
        {
            SoilDepthM = 2.0,
            TrafficLoadKn = 50.0,
            InternalPressureKpa = 10.0
        },
        Environment = new EnvironmentParameter
        {
            FlowType = "gravity",
            Fluid = "wastewater"
        }
    };

    // ── 정상 케이스 ───────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidSchema_ReturnsSuccess()
    {
        var result = ParameterValidator.Validate(ValidSchema());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    // ── 파이프 파라미터 경계값 ────────────────────────────────────────────

    [Theory]
    [InlineData(100)]   // 최솟값 경계
    [InlineData(300)]   // 표준값
    [InlineData(3000)]  // 최댓값 경계
    public void Validate_DiameterAtBoundary_IsValid(double diameter)
    {
        var schema = ValidSchema();
        schema.Pipe.DiameterMm = diameter;

        var result = ParameterValidator.Validate(schema);

        Assert.True(result.IsValid, $"관경 {diameter}mm는 유효해야 합니다. 오류: {result.FirstError}");
    }

    [Theory]
    [InlineData(99)]    // 하한 미달
    [InlineData(3001)]  // 상한 초과
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_DiameterOutOfRange_IsInvalid(double diameter)
    {
        var schema = ValidSchema();
        schema.Pipe.DiameterMm = diameter;

        var result = ParameterValidator.Validate(schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("diameter_mm"));
    }

    [Theory]
    [InlineData(0.001)]  // 최솟값 경계
    [InlineData(0.005)]  // 표준값
    [InlineData(0.200)]  // 최댓값 경계
    public void Validate_SlopeAtBoundary_IsValid(double slope)
    {
        var schema = ValidSchema();
        schema.Pipe.Slope = slope;

        var result = ParameterValidator.Validate(schema);

        Assert.True(result.IsValid, $"경사 {slope}은 유효해야 합니다. 오류: {result.FirstError}");
    }

    [Theory]
    [InlineData(0.0009)]  // 하한 미달
    [InlineData(0.201)]   // 상한 초과
    [InlineData(0)]
    public void Validate_SlopeOutOfRange_IsInvalid(double slope)
    {
        var schema = ValidSchema();
        schema.Pipe.Slope = slope;

        var result = ParameterValidator.Validate(schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("slope"));
    }

    // ── 재질 검증 ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("concrete")]
    [InlineData("ductile_iron")]
    [InlineData("pvc")]
    [InlineData("steel")]
    [InlineData("hdpe")]
    [InlineData("CONCRETE")]   // 대소문자 무관
    public void Validate_AllowedMaterials_IsValid(string material)
    {
        var schema = ValidSchema();
        schema.Pipe.Material = material;

        var result = ParameterValidator.Validate(schema);

        Assert.True(result.IsValid, $"재질 '{material}'은 유효해야 합니다.");
    }

    [Theory]
    [InlineData("wood")]
    [InlineData("rubber")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_InvalidMaterial_IsInvalid(string? material)
    {
        var schema = ValidSchema();
        schema.Pipe.Material = material!;

        var result = ParameterValidator.Validate(schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("material"));
    }

    // ── 필수 필드 누락 ────────────────────────────────────────────────────

    [Fact]
    public void Validate_MissingPipeId_IsInvalid()
    {
        var schema = ValidSchema();
        schema.Pipe.Id = "";

        var result = ParameterValidator.Validate(schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("id"));
    }

    // ── 환경 파라미터 ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("gravity")]
    [InlineData("pressure")]
    public void Validate_AllowedFlowTypes_IsValid(string flowType)
    {
        var schema = ValidSchema();
        schema.Environment.FlowType = flowType;

        Assert.True(ParameterValidator.Validate(schema).IsValid);
    }

    [Theory]
    [InlineData("wastewater")]
    [InlineData("stormwater")]
    [InlineData("clean_water")]
    public void Validate_AllowedFluids_IsValid(string fluid)
    {
        var schema = ValidSchema();
        schema.Environment.Fluid = fluid;

        Assert.True(ParameterValidator.Validate(schema).IsValid);
    }

    [Fact]
    public void Validate_InvalidFlowType_IsInvalid()
    {
        var schema = ValidSchema();
        schema.Environment.FlowType = "siphon";

        var result = ParameterValidator.Validate(schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("flow_type"));
    }

    // ── 다중 오류 수집 ────────────────────────────────────────────────────

    [Fact]
    public void Validate_MultipleErrors_CollectsAll()
    {
        var schema = ValidSchema();
        schema.Pipe.DiameterMm = -1;
        schema.Pipe.Slope = 99;
        schema.Pipe.Material = "wood";

        var result = ParameterValidator.Validate(schema);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3, $"오류 3개 이상 기대, 실제: {result.Errors.Count}");
    }
}
