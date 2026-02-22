import type { WebhookResponse } from '../types/simulation';

interface ResultCardProps {
  data: WebhookResponse;
}

const STATUS_CONFIG = {
  NORMAL: { color: '#22c55e', bg: '#f0fdf4', label: '정상', icon: '✅' },
  WARNING: { color: '#f59e0b', bg: '#fffbeb', label: '경고', icon: '⚠️' },
  DANGER:  { color: '#ef4444', bg: '#fef2f2', label: '위험', icon: '🚨' },
  ERROR:   { color: '#6b7280', bg: '#f9fafb', label: '오류', icon: '❌' },
} as const;

export default function ResultCard({ data }: ResultCardProps) {
  const { result, natural_summary } = data;
  const status = result.overall_status;
  const cfg = STATUS_CONFIG[status] ?? STATUS_CONFIG.ERROR;

  return (
    <div className="result-card" style={{ borderColor: cfg.color }}>
      {/* 종합 상태 배너 */}
      <div className="status-banner" style={{ background: cfg.bg, borderColor: cfg.color }}>
        <span className="status-icon">{cfg.icon}</span>
        <div>
          <div className="status-label" style={{ color: cfg.color }}>
            종합 상태: {cfg.label}
          </div>
          <div className="status-summary">{natural_summary}</div>
        </div>
      </div>

      {/* 수치 그리드 */}
      <div className="metrics-grid">
        <MetricItem
          label="유속"
          value={`${result.flow.velocity_ms.toFixed(2)} m/s`}
          status={result.flow.status}
        />
        <MetricItem
          label="유량"
          value={`${result.flow.flow_rate_m3_s.toFixed(4)} m³/s`}
          status={result.flow.status}
        />
        <MetricItem
          label="충만률"
          value={`${(result.flow.fill_ratio * 100).toFixed(0)}%`}
          status={result.flow.status}
          bar={result.flow.fill_ratio}
        />
        <MetricItem
          label="안전율"
          value={result.stress.safety_factor.toFixed(2)}
          status={result.stress.status}
          warn={result.stress.safety_factor < 1.0}
        />
        <MetricItem
          label="최대 응력"
          value={`${result.stress.max_stress_kpa.toFixed(1)} kPa`}
          status={result.stress.status}
        />
      </div>

      {/* 경고 메시지 */}
      {result.warnings.length > 0 && (
        <div className="warnings">
          {result.warnings.map((w, i) => (
            <div key={i} className="warning-item">⚠️ {w}</div>
          ))}
        </div>
      )}

      {/* 계산 시각 */}
      <div className="calc-time">
        계산: {new Date(result.calculated_at).toLocaleString('ko-KR')}
      </div>
    </div>
  );
}

function MetricItem({
  label, value, status, bar, warn,
}: {
  label: string;
  value: string;
  status: string;
  bar?: number;
  warn?: boolean;
}) {
  const color =
    status === 'NORMAL' || status === 'SAFE' ? '#22c55e'
    : status === 'WARNING' ? '#f59e0b'
    : '#ef4444';

  return (
    <div className="metric-item">
      <div className="metric-label">{label}</div>
      <div className="metric-value" style={{ color: warn ? '#ef4444' : color }}>
        {value}
      </div>
      {bar !== undefined && (
        <div className="metric-bar-track">
          <div
            className="metric-bar-fill"
            style={{
              width: `${Math.min(bar * 100, 100)}%`,
              background: color,
            }}
          />
        </div>
      )}
    </div>
  );
}
