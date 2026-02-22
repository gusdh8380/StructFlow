import type { WebhookResponse } from '../types/simulation';

interface FormulaPanelProps {
  data: WebhookResponse;
}

export default function FormulaPanel({ data }: FormulaPanelProps) {
  const { result } = data;
  const flow   = result.flow;
  const stress = result.stress;

  const allowableKpa = (stress.safety_factor * stress.max_stress_kpa).toFixed(1);
  const fillPct      = (flow.fill_ratio * 100).toFixed(0);

  const flowColor   = flow.status   === 'NORMAL' ? '#22c55e' : flow.status   === 'WARNING' ? '#f59e0b' : '#ef4444';
  const stressColor = stress.status === 'SAFE'   ? '#22c55e' : stress.status === 'WARNING' ? '#f59e0b' : '#ef4444';

  return (
    <div className="formula-panel">
      <div className="formula-panel-header">
        <span>📐</span> 계산 근거 &amp; 공학 수식
      </div>

      {/* ── 1. Manning 공식 ─────────────────────────────────── */}
      <div className="formula-section">
        <div className="formula-section-title">🌊 유속 — Manning 공식</div>

        <div className="formula-eq">
          V = <span className="frac"><span>(1/n)</span></span>
          &nbsp;·&nbsp;R<sup>2/3</sup>&nbsp;·&nbsp;S<sup>1/2</sup>
        </div>

        <div className="formula-vars-grid">
          <div className="fvar"><code>V</code><span>유속</span><strong style={{ color: flowColor }}>{flow.velocity_ms.toFixed(2)} m/s</strong></div>
          <div className="fvar"><code>n</code><span>조도계수</span><strong>0.013 (콘크리트)</strong></div>
          <div className="fvar"><code>R</code><span>동수반경</span><strong>A / P</strong></div>
          <div className="fvar"><code>S</code><span>경사</span><strong>0.010 (1%)</strong></div>
        </div>

        <div className="formula-interp">
          <span className="interp-icon">💡</span>
          <span>
            유속 <strong>{flow.velocity_ms.toFixed(2)} m/s</strong>는&nbsp;
            {flow.velocity_ms >= 0.6 && flow.velocity_ms <= 3.0
              ? <span className="ok">허용 범위(0.6 ~ 3.0 m/s) 내 ✓</span>
              : flow.velocity_ms < 0.6
              ? <span className="warn">최소 유속 0.6 m/s 미달 — 퇴적 위험</span>
              : <span className="danger">최대 유속 3.0 m/s 초과 — 관 침식 위험</span>
            }
          </span>
        </div>
      </div>

      {/* ── 2. 유량 & 충만율 ─────────────────────────────────── */}
      <div className="formula-section">
        <div className="formula-section-title">💧 유량 &amp; 충만율</div>

        <div className="formula-eq">Q = A · V</div>

        <div className="formula-vars-grid">
          <div className="fvar"><code>Q</code><span>유량</span><strong>{flow.flow_rate_m3_s.toFixed(4)} m³/s</strong></div>
          <div className="fvar"><code>A</code><span>유수단면적</span><strong>π(D/2)² × η</strong></div>
          <div className="fvar"><code>η</code><span>충만율</span><strong style={{ color: flowColor }}>{fillPct}%</strong></div>
          <div className="fvar"><code>D</code><span>관 직경</span><strong>설계 입력값</strong></div>
        </div>

        {/* 충만율 바 */}
        <div className="fill-ratio-bar">
          <div className="fill-ratio-labels">
            <span>0%</span>
            <span className="fill-rec">권장 상한 80%</span>
            <span>100%</span>
          </div>
          <div className="fill-track">
            <div className="fill-fill" style={{ width: `${fillPct}%`, background: flowColor }} />
            <div className="fill-limit" />
          </div>
          <div className="fill-ratio-value" style={{ left: `${fillPct}%` }}>
            {fillPct}%
          </div>
        </div>

        <div className="formula-interp">
          <span className="interp-icon">💡</span>
          <span>
            충만율 <strong>{fillPct}%</strong>
            {flow.fill_ratio <= 0.8
              ? <span className="ok"> — 설계 기준(≤80%) 충족 ✓</span>
              : <span className="danger"> — 설계 기준 초과, 월류(Overflow) 위험</span>
            }
          </span>
        </div>
      </div>

      {/* ── 3. 구조 안전율 (Marston) ────────────────────────── */}
      <div className="formula-section">
        <div className="formula-section-title">⚙️ 구조 안전율 — Marston 하중 이론</div>

        <div className="formula-eq">
          SF = σ<sub>허용</sub> / σ<sub>실제</sub>
        </div>

        <div className="formula-vars-grid">
          <div className="fvar">
            <code>SF</code><span>안전율</span>
            <strong style={{ color: stressColor }}>{stress.safety_factor.toFixed(3)}</strong>
          </div>
          <div className="fvar">
            <code>σ<sub>실</sub></code><span>실제 응력</span>
            <strong style={{ color: stressColor }}>{stress.max_stress_kpa.toFixed(1)} kPa</strong>
          </div>
          <div className="fvar">
            <code>σ<sub>허</sub></code><span>허용 응력</span>
            <strong>{allowableKpa} kPa</strong>
          </div>
          <div className="fvar">
            <code>기준</code><span>안전 조건</span>
            <strong>SF ≥ 1.0</strong>
          </div>
        </div>

        {/* Marston 하중 구성 */}
        <div className="load-breakdown">
          <div className="load-breakdown-title">하중 구성 (Marston 공식 기반)</div>
          <div className="load-items">
            <div className="load-item">
              <span className="load-name">토압 하중 W<sub>e</sub></span>
              <span className="load-formula">= C<sub>d</sub> · γ · B²</span>
              <span className="load-desc">흙 단위중량 × 매설깊이 × 관경</span>
            </div>
            <div className="load-item">
              <span className="load-name">교통 하중 W<sub>L</sub></span>
              <span className="load-formula">HS-20 기준</span>
              <span className="load-desc">도로 활하중 분산 적용</span>
            </div>
            <div className="load-item">
              <span className="load-name">관내 수압 P</span>
              <span className="load-formula">= γ<sub>w</sub> · h</span>
              <span className="load-desc">내부 유체 정수압</span>
            </div>
          </div>
        </div>

        <div className="sf-gauge">
          <div className="sf-track">
            <div className="sf-danger-zone" />
            <div className="sf-safe-zone" />
            <div
              className="sf-marker"
              style={{ left: `${Math.min(stress.safety_factor / 2 * 100, 100)}%` }}
            >
              <div className="sf-marker-line" />
              <div className="sf-marker-label" style={{ color: stressColor }}>
                SF {stress.safety_factor.toFixed(2)}
              </div>
            </div>
            <div className="sf-threshold">
              <span>1.0</span>
            </div>
          </div>
          <div className="sf-track-labels">
            <span style={{ color: '#ef4444' }}>위험 (SF &lt; 1.0)</span>
            <span style={{ color: '#22c55e' }}>안전 (SF ≥ 1.0)</span>
          </div>
        </div>

        {stress.status === 'DANGER' && (
          <div className="formula-recommend">
            <div className="recommend-title">🔧 설계 개선 권고</div>
            <ul>
              <li><strong>관경 증가</strong> — 직경을 키우면 단면 이차모멘트(I) 증가 → 응력 감소</li>
              <li><strong>고강도 관재 사용</strong> — 철근콘크리트관(RCP) Class 상향 또는 강관 검토</li>
              <li><strong>매설깊이 감소</strong> — 토피고를 줄이면 토압 하중(W<sub>e</sub>) 감소</li>
              <li><strong>되메우기 방법 변경</strong> — 모래 기초 처리로 하중 분산 개선</li>
            </ul>
          </div>
        )}
        {stress.status === 'WARNING' && (
          <div className="formula-recommend warn">
            <div className="recommend-title">⚠️ 주의 — 정밀 검토 필요</div>
            <ul>
              <li>안전율이 기준에 근접. 관재 품질 및 시공 조건 재확인 필요</li>
            </ul>
          </div>
        )}
      </div>

      {/* ── 4. 설계 기준 요약 ────────────────────────────────── */}
      <div className="formula-section formula-section-last">
        <div className="formula-section-title">📋 KDS 기준 요약</div>
        <div className="standards-grid">
          <div className="std-item">
            <span className="std-label">최소 유속</span>
            <span className="std-value">0.6 m/s</span>
            <span className="std-reason">자정 작용, 퇴적 방지</span>
          </div>
          <div className="std-item">
            <span className="std-label">최대 유속</span>
            <span className="std-value">3.0 m/s</span>
            <span className="std-reason">관 내면 침식 방지</span>
          </div>
          <div className="std-item">
            <span className="std-label">설계 충만율</span>
            <span className="std-value">≤ 80%</span>
            <span className="std-reason">여유 유량 확보</span>
          </div>
          <div className="std-item">
            <span className="std-label">최소 안전율</span>
            <span className="std-value">SF ≥ 1.0</span>
            <span className="std-reason">구조 파괴 방지</span>
          </div>
        </div>
      </div>
    </div>
  );
}
