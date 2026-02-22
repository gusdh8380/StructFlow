import { useState } from 'react';
import type { KeyboardEvent } from 'react';

interface InputPanelProps {
  onSubmit: (input: string) => void;
  loading: boolean;
}

const EXAMPLES = [
  '직경 60cm 콘크리트 하수관, 경사 1%, 토피고 2m, 연장 100m',
  '300mm PVC 우수관, 0.5% 경사, 매설깊이 1.5m',
  '800mm 덕타일 주철관, 2% 경사, 토피 3m, 연장 200m',
];

export default function InputPanel({ onSubmit, loading }: InputPanelProps) {
  const [input, setInput] = useState('');

  const handleSubmit = () => {
    if (input.trim() && !loading) onSubmit(input.trim());
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) handleSubmit();
  };

  return (
    <div className="input-panel">
      <div className="input-label">
        <span className="label-icon">✏️</span>
        자연어로 설계 조건을 입력하세요
      </div>

      <textarea
        className="input-textarea"
        value={input}
        onChange={(e) => setInput(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="예: 직경 60cm 콘크리트 하수관, 경사 1%, 토피고 2m, 연장 100m"
        disabled={loading}
        rows={4}
      />

      <div className="input-hint">Ctrl+Enter로 제출</div>

      <button
        className={`submit-btn ${loading ? 'loading' : ''}`}
        onClick={handleSubmit}
        disabled={loading || !input.trim()}
      >
        {loading ? (
          <>
            <span className="spinner" />
            분석 중...
          </>
        ) : (
          <>
            <span>⚡</span>
            시뮬레이션 실행
          </>
        )}
      </button>

      <div className="examples">
        <div className="examples-title">예시 입력</div>
        {EXAMPLES.map((ex, i) => (
          <button
            key={i}
            className="example-btn"
            onClick={() => setInput(ex)}
            disabled={loading}
          >
            {ex}
          </button>
        ))}
      </div>
    </div>
  );
}
