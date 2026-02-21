# CLAUDE.md — StructFlow

## 프로젝트 개요
StructFlow는 자연어 입력을 구조 설계 파라미터로 변환하고, 시뮬레이션 및 3D 시각화까지 연결하는 AI 기반 설계 자동화 파이프라인 프로젝트입니다.

- **목적**: 부강테크 포트폴리오 — 설계 소프트웨어 구조 재설계 + LLM 자동화 + 확장 가능 아키텍처 시연
- **도메인**: 하수관로/배수 파이프 구조 설계 (확장 가능한 Parametric 도메인)
- **핵심 가치**: 설계 철학 + 모듈 연결성 + AI 자동화를 하나의 흐름으로 보여주는 것

## 전체 파이프라인
```
[자연어 입력]
      ↓
[n8n Webhook + LLM Prompt Processor]
      ↓
[Parameter Schema Generator] → JSON Schema
      ↓
[SimulationEngine (C#)] → 유체/구조 계산
      ↓
[Unity View Layer] → 3D 시각화 + 커스텀 인스펙터
      ↓
[Result Reporter] → JSON 결과 + 자연어 요약
```

## 모듈 구조
```
StructFlow/
├── ParametricCore/          # 설계 파라미터 모델 정의 및 유효성 검사
│   ├── Models/
│   │   ├── PipeParameter.cs
│   │   ├── DesignSchema.cs
│   │   └── ValidationResult.cs
│   ├── Validators/
│   │   └── ParameterValidator.cs
│   └── Interfaces/
│       └── IDesignParameter.cs
│
├── DataAdapter/             # JSON 입출력, 외부 포맷 변환
│   ├── JsonSerializer.cs
│   ├── SchemaImporter.cs
│   └── ResultExporter.cs
│
├── SimulationEngine/        # 물리/구조 계산 엔진
│   ├── FlowCalculator.cs
│   ├── StressAnalyzer.cs
│   ├── SimulationResult.cs
│   └── Interfaces/
│       └── ISimulationEngine.cs
│
├── LLMConnector/            # n8n → LLM → 파라미터 변환
│   ├── WebhookReceiver.cs
│   ├── PromptBuilder.cs
│   └── ParameterParser.cs
│
├── UnityView/               # 3D 렌더링 + 에디터 UI
│   ├── PipeRenderer.cs
│   ├── Editor/
│   │   └── ParametricInspector.cs
│   ├── UI/
│   │   └── ResultPanel.cs
│   └── Interfaces/
│       └── IViewRenderer.cs
│
└── n8n/
    └── workflow_structflow.json
```

## 모듈 책임 분리 원칙

| 모듈 | 단일 책임 | 입력 | 출력 |
|------|-----------|------|------|
| ParametricCore | 파라미터 모델 정의 + 유효성 검사 | raw JSON | DesignSchema 객체 |
| DataAdapter | 포맷 변환 (JSON ↔ 내부 모델) | 파일/스트림 | DesignSchema / JSON |
| SimulationEngine | 계산 로직만 담당 (UI 없음) | DesignSchema | SimulationResult |
| LLMConnector | 자연어 → 파라미터 변환만 담당 | 자연어 문자열 | JSON Schema |
| UnityView | 시각화만 담당 (계산 로직 없음) | SimulationResult | 3D 씬 + UI |

**핵심 원칙**: 각 모듈은 서로를 직접 참조하지 않습니다. DataAdapter를 통해서만 데이터가 이동합니다.

## 코드 작성 규칙
- **언어**: C# (Unity/SimulationEngine), JavaScript (n8n 커스텀 노드)
- **네이밍**: PascalCase (클래스/메서드), camelCase (변수), UPPER_SNAKE (상수)
- **단위**: 모든 길이는 `_mm` 또는 `_m` 접미사로 명시
- **에러 처리**: 계산 실패 시 예외 던지지 않고 `SimulationResult.status = "ERROR"` + reason 반환
- **모듈 간 통신**: 직접 참조 금지, 반드시 JSON 직렬화된 DesignSchema를 통해서만 이동
- **주석**: 계산 공식에는 반드시 출처/수식 주석 추가

## SimulationEngine 계산 공식

### Manning 공식 (유량 계산)
```
Q = (1/n) × A × R^(2/3) × S^(1/2)

Q  = 유량 (m³/s)
n  = Manning 조도계수 (콘크리트: 0.013)
A  = 유수 단면적 (m²)
R  = 동수반경 (m)
S  = 수로 경사
```
출처: KDS 57 17 00 (하수도 설계 기준)

### 안전율 계산
```
Safety Factor = 허용응력 / 실제응력

SAFE   : SF >= 2.0
WARNING: 1.5 <= SF < 2.0
DANGER : SF < 1.5
```
출처: KS D 4301 (원심력 철근 콘크리트관)

## LLM 시스템 프롬프트 원칙
- 항상 JSON만 반환하도록 강제 (respond only in JSON)
- 파라미터 범위 벗어날 경우 null 반환 + reason 필드 포함
- 단위 통일: mm, m, kN, kPa
- 모호한 입력은 보수적 기본값 사용 (use conservative defaults)

## 확장 전략
```
현재: PipeParameter → FlowCalculator → PipeRenderer
         ↓ 인터페이스만 교체
확장1: BeamParameter → StructuralAnalyzer → BeamRenderer  (철골 보 설계)
확장2: RoadParameter → DrainageCalculator → RoadRenderer  (도로 배수 설계)
확장3: BuildingParameter → LoadCalculator → FloorRenderer (건축 하중 설계)
```
IDesignParameter, ISimulationEngine, IViewRenderer 인터페이스로 추상화되어 있어 도메인만 교체하면 파이프라인은 그대로 유지됩니다.

## 개발 순서 (MVP 기준 3주)

### 1주차 — 코어 레이어
- [ ] PipeParameter C# 모델 클래스 작성
- [ ] ParameterValidator 유효성 검사 로직
- [ ] JsonSerializer / DataAdapter 구현
- [ ] FlowCalculator Manning 공식 구현
- [ ] 단위 테스트 작성

### 2주차 — LLM 연결
- [ ] n8n Webhook 트리거 설정
- [ ] Claude API 프롬프트 설계 + 테스트
- [ ] JSON 파싱 + 유효성 검사 노드
- [ ] SimulationEngine REST API 엔드포인트

### 3주차 — Unity 시각화
- [ ] PipeRenderer Procedural Mesh 구현
- [ ] ParametricInspector 커스텀 에디터
- [ ] ResultPanel UI 구현
- [ ] 전체 파이프라인 E2E 테스트

## 참고 자료
- Manning 공식: KDS 57 17 00 (하수도 설계 기준)
- 파이프 허용응력: KS D 4301 (원심력 철근 콘크리트관)
- Unity Procedural Mesh: Unity Docs — ProceduralMesh
- n8n 공식 문서: n8n.io/docs
