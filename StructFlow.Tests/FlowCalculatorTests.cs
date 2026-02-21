using StructFlow.ParametricCore.Models;
using StructFlow.SimulationEngine;

namespace StructFlow.Tests;

/// <summary>
/// FlowCalculator (Manning 공식) 단위 테스트
/// 기준: KDS 57 17 00
/// </summary>
public class FlowCalculatorTests
{
    // 표준 테스트 파이프: D=300mm, n=0.013, S=0.005
    private static PipeParameter StandardPipe() => new()
    {
        Id = "TEST",
        DiameterMm = 300,
        LengthM = 50,
        Material = "concrete",
        Slope = 0.005,
        RoughnessCoefficient = 0.013
    };

    // ── 만관 유속 검증 ────────────────────────────────────────────────────

    [Fact]
    public void Calculate_StandardPipe_VelocityInReasonableRange()
    {
        // D=300mm 콘크리트관 S=0.005 → 실무 기대값 약 0.9~1.1 m/s
        var result = FlowCalculator.Calculate(StandardPipe());

        Assert.InRange(result.VelocityMs, 0.6, 3.0);
    }

    [Fact]
    public void Calculate_StandardPipe_FlowRatePositive()
    {
        var result = FlowCalculator.Calculate(StandardPipe());

        Assert.True(result.FlowRateM3S > 0, "유량은 양수여야 합니다.");
    }

    [Fact]
    public void Calculate_NoDesignFlow_FillRatioIsOne()
    {
        // designFlowM3S 미입력 시 만관(fill=1.0) 기준
        var result = FlowCalculator.Calculate(StandardPipe());

        Assert.Equal(1.0, result.FillRatio, precision: 2);
    }

    // ── 경사 증가 시 유속 증가 확인 ──────────────────────────────────────

    [Fact]
    public void Calculate_SteepSlope_HigherVelocityThanGentleSlope()
    {
        var gentlePipe = StandardPipe(); gentlePipe.Slope = 0.002;
        var steepPipe  = StandardPipe(); steepPipe.Slope  = 0.020;

        var gentle = FlowCalculator.Calculate(gentlePipe);
        var steep  = FlowCalculator.Calculate(steepPipe);

        Assert.True(steep.VelocityMs > gentle.VelocityMs,
            "경사가 급할수록 유속이 커야 합니다 (Manning 공식).");
    }

    // ── 관경 증가 시 유량 증가 확인 ──────────────────────────────────────

    [Fact]
    public void Calculate_LargerDiameter_HigherFlowRate()
    {
        var small = StandardPipe(); small.DiameterMm = 200;
        var large = StandardPipe(); large.DiameterMm = 600;

        var smallResult = FlowCalculator.Calculate(small);
        var largeResult = FlowCalculator.Calculate(large);

        Assert.True(largeResult.FlowRateM3S > smallResult.FlowRateM3S,
            "관경이 클수록 유량이 커야 합니다.");
    }

    // ── 충만율 기반 상태 판정 ─────────────────────────────────────────────

    [Fact]
    public void Calculate_LowDesignFlow_StatusNormal()
    {
        var pipe = StandardPipe();
        var fullResult = FlowCalculator.Calculate(pipe);
        // 설계 유량을 만관의 50%로 설정
        double designFlow = fullResult.FlowRateM3S * 0.50;

        var result = FlowCalculator.Calculate(pipe, designFlow);

        Assert.Equal("NORMAL", result.Status);
        Assert.True(result.FillRatio < 0.80);
    }

    [Fact]
    public void Calculate_HighDesignFlow_StatusWarningOrDanger()
    {
        var pipe = StandardPipe();
        var fullResult = FlowCalculator.Calculate(pipe);
        // 설계 유량을 만관의 90%로 설정 → 충만율 경고
        double designFlow = fullResult.FlowRateM3S * 0.90;

        var result = FlowCalculator.Calculate(pipe, designFlow);

        Assert.True(result.Status is "WARNING" or "DANGER",
            "충만율 80% 초과면 WARNING 또는 DANGER 상태여야 합니다.");
    }

    // ── Manning 공식 수치 검증 ────────────────────────────────────────────

    [Fact]
    public void Calculate_ManningFormula_VelocityMatchesExpected()
    {
        // D=500mm, n=0.013, S=0.010
        // R = D/4 = 0.125 m
        // V = (1/0.013) × 0.125^(2/3) × 0.010^(1/2)
        //   = 76.923 × 0.25 × 0.1 = 1.923 m/s (근사)
        var pipe = new PipeParameter
        {
            Id = "CALC-TEST",
            DiameterMm = 500,
            LengthM = 100,
            Material = "concrete",
            Slope = 0.010,
            RoughnessCoefficient = 0.013
        };

        var result = FlowCalculator.Calculate(pipe);

        // 만관 기준이므로 velocity factor 적용됨, 오차 허용 ±20%
        double expectedV = (1.0 / 0.013)
                           * Math.Pow(0.125, 2.0 / 3.0)
                           * Math.Pow(0.010, 0.5);
        Assert.InRange(result.VelocityMs, expectedV * 0.80, expectedV * 1.20);
    }
}
