using StructFlow.DataAdapter;

namespace StructFlow.Tests;

/// <summary>
/// SchemaImporter 단위 테스트
/// LLM JSON 병합, 기본값 적용, 유효성 검사 통합 흐름 검증
/// </summary>
public class SchemaImporterTests
{
    // ── 완전한 JSON 파싱 ─────────────────────────────────────────────────

    [Fact]
    public void ImportFromJson_ValidJson_ReturnsValidSchema()
    {
        var json = """
        {
          "pipe": {
            "id": "PIPE-001",
            "diameter_mm": 300,
            "length_m": 50.0,
            "material": "concrete",
            "slope": 0.005,
            "roughness_coefficient": 0.013
          },
          "load": {
            "soil_depth_m": 2.0,
            "traffic_load_kn": 50.0,
            "internal_pressure_kpa": 10.0
          },
          "environment": {
            "flow_type": "gravity",
            "fluid": "wastewater"
          }
        }
        """;

        var (schema, validation) = SchemaImporter.ImportFromJson(json);

        Assert.NotNull(schema);
        Assert.True(validation.IsValid, $"유효성 검사 실패: {validation.FirstError}");
        Assert.Equal(300, schema!.Pipe.DiameterMm);
    }

    [Fact]
    public void ImportFromJson_EmptyJson_ReturnsFailure()
    {
        var (schema, validation) = SchemaImporter.ImportFromJson("");

        Assert.Null(schema);
        Assert.False(validation.IsValid);
    }

    [Fact]
    public void ImportFromJson_InvalidJson_ReturnsFailure()
    {
        var (schema, validation) = SchemaImporter.ImportFromJson("{ not valid json }");

        Assert.Null(schema);
        Assert.False(validation.IsValid);
    }

    // ── 보수적 기본값 적용 ────────────────────────────────────────────────

    [Fact]
    public void ImportFromJson_MissingFields_AppliesConservativeDefaults()
    {
        // 최소한의 JSON — 대부분 필드 누락
        var json = """{ "pipe": { "id": "PIPE-MIN", "diameter_mm": 400 } }""";

        var (schema, validation) = SchemaImporter.ImportFromJson(json, applyConservativeDefaults: true);

        Assert.NotNull(schema);
        Assert.True(validation.IsValid, $"기본값 적용 후 유효해야 합니다: {validation.FirstError}");
        Assert.Equal(400, schema!.Pipe.DiameterMm);     // 입력값 유지
        Assert.Equal(0.005, schema.Pipe.Slope);          // 기본값 적용
        Assert.Equal("concrete", schema.Pipe.Material);  // 기본값 적용
        Assert.Equal("gravity", schema.Environment.FlowType); // 기본값 적용
    }

    // ── LLM JSON 병합 ─────────────────────────────────────────────────────

    [Fact]
    public void MergeFromLlmJson_PartialJson_MergesWithBase()
    {
        // LLM이 관경과 경사만 반환한 경우
        var llmJson = """
        {
          "pipe": { "diameter_mm": 500, "slope": 0.008 }
        }
        """;

        var (schema, validation) = SchemaImporter.MergeFromLlmJson(llmJson);

        Assert.NotNull(schema);
        Assert.Equal(500, schema!.Pipe.DiameterMm);   // LLM 값 반영
        Assert.Equal(0.008, schema.Pipe.Slope);        // LLM 값 반영
        Assert.Equal("concrete", schema.Pipe.Material); // 기본값 유지
    }

    [Fact]
    public void MergeFromLlmJson_WithBase_OverridesOnlyLlmFields()
    {
        // 기존 스키마에 LLM 부분 JSON을 덮어씀
        var (baseSchema, _) = SchemaImporter.ImportFromJson("""
        {
          "pipe": {
            "id": "PIPE-BASE",
            "diameter_mm": 300,
            "length_m": 100.0,
            "material": "pvc",
            "slope": 0.003,
            "roughness_coefficient": 0.011
          },
          "load": { "soil_depth_m": 3.0, "traffic_load_kn": 80.0, "internal_pressure_kpa": 20.0 },
          "environment": { "flow_type": "pressure", "fluid": "clean_water" }
        }
        """);

        var llmJson = """{ "pipe": { "diameter_mm": 600 } }""";

        var (merged, validation) = SchemaImporter.MergeFromLlmJson(llmJson, baseSchema);

        Assert.NotNull(merged);
        Assert.Equal(600, merged!.Pipe.DiameterMm);   // LLM이 덮어씀
        Assert.Equal("pvc", merged.Pipe.Material);     // base 유지
        Assert.Equal(100.0, merged.Pipe.LengthM);      // base 유지
        Assert.Equal("pressure", merged.Environment.FlowType); // base 유지
    }

    [Fact]
    public void MergeFromLlmJson_InvalidJson_ReturnsFailure()
    {
        var (schema, validation) = SchemaImporter.MergeFromLlmJson("not json");

        Assert.Null(schema);
        Assert.False(validation.IsValid);
    }
}
