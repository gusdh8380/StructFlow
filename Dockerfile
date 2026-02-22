# ──────────────────────────────────────────────────────────────────
# StructFlow SimulationEngine API — Multi-stage Dockerfile
# Build context: 프로젝트 루트 (StructFlow/)
# ──────────────────────────────────────────────────────────────────

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# csproj 먼저 복사 → 레이어 캐시 최적화 (소스 변경 시 restore 재실행 방지)
COPY SimulationApi/SimulationApi.csproj SimulationApi/

# csproj가 ../ParametricCore 등을 상대 경로로 참조하므로 모듈 디렉토리 전체 복사
COPY ParametricCore/  ParametricCore/
COPY DataAdapter/     DataAdapter/
COPY SimulationEngine/ SimulationEngine/
COPY LLMConnector/    LLMConnector/

RUN dotnet restore SimulationApi/SimulationApi.csproj

# 소스 복사 후 배포 빌드
COPY SimulationApi/ SimulationApi/
RUN dotnet publish SimulationApi/SimulationApi.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Runtime (SDK 없이 런타임만 포함 — 이미지 크기 최소화)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 비루트 사용자로 실행 (보안)
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

# 포트 5000에서 수신
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:5000/api/health || exit 1

ENTRYPOINT ["dotnet", "SimulationApi.dll"]
