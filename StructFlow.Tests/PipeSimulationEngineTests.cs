using StructFlow.DataAdapter;
using StructFlow.ParametricCore.Models;
using StructFlow.SimulationEngine;

namespace StructFlow.Tests;

/// <summary>
/// PipeSimulationEngine 통합 단위 테스트
/// Flow + Stress 조합 결과 및 에러 처리 검증
/// </summary>
public class PipeSimulationEngineTests
{
    private readonly PipeSimulationEngine _engine = new();

    private static DesignSchema ValidatedSchema(
        double diameterMm = 300,
        double slope = 0.005,
        double soilDepthM = 2.0,
        double trafficLoadKn = 50.0) => new()
    {
        IsValidated = true,
        Pipe = new PipeParameter
        {
            Id = "ENGINE-TEST",
            DiameterMm = diameterMm,
            LengthM = 50,
            Material = "concrete",
            Slope = slope,
            RoughnessCoefficient = 0.013
        },
        Load = new LoadParameter
        {
            SoilDepthM = soilDepthM,
            TrafficLoadKn = trafficLoadKn,
            InternalPressureKpa = 10.0
        },
        Environment = new EnvironmentParameter
        {
            FlowType = "gravity",
            Fluid = "wastewater"
        }
    };

    // ── 기본 반환값 ───────────────────────────────────────────────────────

    [Fact]
    public void Run_ValidSchema_ReturnsPipeId()
    {
        var result = _engine.Run(ValidatedSchema());

        Assert.Equal("ENGINE-TEST", result.PipeId);
    }

    [Fact]
    public void Run_ValidSchema_ReturnsFlowAndStress()
    {
        var result = _engine.Run(ValidatedSchema());

        Assert.NotNull(result.Flow);
        Assert.NotNull(result.Stress);
    }

    [Fact]
    public void Run_ValidSchema_CalculatedAtIsSet()
    {
        var result = _engine.Run(ValidatedSchema());

        Assert.False(string.IsNullOrEmpty(result.CalculatedAt));
    }

    [Fact]
    public void Run_ValidSchema_SummaryIsSet()
    {
        var result = _engine.Run(ValidatedSchema());

        Assert.False(string.IsNullOrEmpty(result.Summary));
    }

    // ── 에러 처리 (예외 없이 ERROR 상태 반환) ────────────────────────────

    [Fact]
    public void Run_NullSchema_ReturnsErrorStatus()
    {
        var result = _engine.Run(null!);

        Assert.Equal("ERROR", result.OverallStatus);
        Assert.NotNull(result.ErrorReason);
    }

    [Fact]
    public void Run_NotValidatedSchema_ReturnsErrorStatus()
    {
        var schema = ValidatedSchema();
        schema.IsValidated = false;  // 검사 미통과 상태

        var result = _engine.Run(schema);

        Assert.Equal("ERROR", result.OverallStatus);
    }

    // ── 전체 상태 판정 로직 ──────────────────────────────────────────────

    [Fact]
    public void Run_OverallStatus_IsOneOfValidValues()
    {
        var result = _engine.Run(ValidatedSchema());

        Assert.Contains(result.OverallStatus, new[] { "NORMAL", "WARNING", "DANGER", "ERROR" });
    }

    // ── DataAdapter 직렬화 왕복 테스트 ───────────────────────────────────

    [Fact]
    public void Run_Result_SerializableAndDeserializable()
    {
        var result = _engine.Run(ValidatedSchema());

        // JSON 직렬화
        var json = StructFlowJsonSerializer.SerializeResult(result);
        Assert.False(string.IsNullOrEmpty(json));

        // 역직렬화
        var deserialized = StructFlowJsonSerializer.DeserializeResult(json);
        Assert.NotNull(deserialized);
        Assert.Equal(result.PipeId, deserialized!.PipeId);
        Assert.Equal(result.OverallStatus, deserialized.OverallStatus);
    }

    // ── 시나리오: 완전한 자연어 → 파라미터 → 시뮬레이션 흐름 ─────────────

    [Fact]
    public void Run_FromJsonRoundTrip_ProducesConsistentResult()
    {
        // DataAdapter를 통한 JSON 왕복 후에도 동일한 결과가 나오는지 확인
        var original = ValidatedSchema();
        var json = StructFlowJsonSerializer.SerializeSchema(original);
        var restored = StructFlowJsonSerializer.DeserializeSchema(json);
        restored!.IsValidated = true;

        var result1 = _engine.Run(original);
        var result2 = _engine.Run(restored);

        Assert.Equal(result1.OverallStatus, result2.OverallStatus);
        Assert.Equal(result1.Flow?.VelocityMs, result2.Flow?.VelocityMs);
        Assert.Equal(result1.Stress?.SafetyFactor, result2.Stress?.SafetyFactor);
    }
}
