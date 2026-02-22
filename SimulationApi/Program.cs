using System.Text.Json;
using System.Text.Json.Serialization;
using StructFlow.DataAdapter;
using StructFlow.ParametricCore.Models;
using StructFlow.SimulationEngine;

// ── 앱 빌드 ──────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    opts.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

// ── 헬스체크 ─────────────────────────────────────────────────────────────────

app.MapGet("/", () => Results.Ok(new
{
    service = "StructFlow SimulationEngine API",
    version = "1.0.0",
    status  = "running",
    endpoints = new[] { "POST /api/simulate", "POST /api/validate", "GET /api/health" }
}));

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
}));

// ── POST /api/simulate ───────────────────────────────────────────────────────
// n8n 워크플로우의 "SimulationEngine API 호출" 노드가 이 엔드포인트를 사용한다.
//
// Request Body : DesignSchema JSON
// Response     : SimulationResult JSON
// 에러         : 예외 없이 SimulationResult.overall_status = "ERROR" 반환

app.MapPost("/api/simulate", async (HttpRequest request) =>
{
    string body;
    using (var reader = new StreamReader(request.Body))
        body = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(body))
        return Results.BadRequest(new { error = "Request body is empty." });

    // DataAdapter를 통해 역직렬화 (직접 참조 금지 원칙 준수)
    var schema = StructFlowJsonSerializer.DeserializeSchema(body);

    if (schema == null)
        return Results.BadRequest(new { error = "Invalid DesignSchema JSON." });

    // is_validated 가 false이면 자동으로 재검사
    if (!schema.IsValidated)
    {
        var (revalidated, validation) = SchemaImporter.ImportFromJson(body, applyConservativeDefaults: false);
        if (revalidated == null || !validation.IsValid)
        {
            var errorResult = SimulationResult.Error(
                schema.Pipe?.Id ?? "UNKNOWN",
                $"유효성 검사 실패: {string.Join(", ", validation.Errors)}");
            return Results.UnprocessableEntity(errorResult);
        }
        schema = revalidated;
    }

    var engine = new PipeSimulationEngine();
    var result = engine.Run(schema);

    return Results.Ok(result);
});

// ── POST /api/validate ───────────────────────────────────────────────────────
// DesignSchema JSON을 받아 유효성 검사만 수행 (시뮬레이션 없음).
// n8n 개발 중 파라미터 점검용.

app.MapPost("/api/validate", async (HttpRequest request) =>
{
    string body;
    using (var reader = new StreamReader(request.Body))
        body = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(body))
        return Results.BadRequest(new { error = "Request body is empty." });

    var (schema, validation) = SchemaImporter.ImportFromJson(body);

    return Results.Ok(new
    {
        is_valid = validation.IsValid,
        errors   = validation.Errors,
        schema
    });
});

// ── 실행 ─────────────────────────────────────────────────────────────────────

app.Run();
// StructFlow v1.0 — IAM 최소 권한 배포 검증 (2026-02-22)
