import { useCallback, useRef, useState } from 'react';
import InputPanel from './components/InputPanel';
import ResultCard from './components/ResultCard';
import FormulaPanel from './components/FormulaPanel';
import UnityViewer from './components/UnityViewer';
import { useSimulation } from './hooks/useSimulation';
import type { SimulationResult } from './types/simulation';
import './App.css';

export default function App() {
  const { result, loading, error, submit } = useSimulation();
  const unitySendRef = useRef<((go: string, method: string, val: string) => void) | null>(null);
  const [latestResult, setLatestResult] = useState<SimulationResult | null>(null);

  const handleSubmit = useCallback(async (input: string) => {
    const res = await submit(input);
    if (res) setLatestResult(res);
  }, [submit]);

  const handleUnityReady = useCallback((sendFn: (go: string, m: string, v: string) => void) => {
    unitySendRef.current = sendFn;
    // Unity 준비 완료 시 마지막 결과가 있으면 바로 전달
    if (latestResult) {
      sendFn('PipeController', 'ReceiveSimulationResult', JSON.stringify(latestResult));
    }
  }, [latestResult]);

  return (
    <div className="app">
      {/* 헤더 */}
      <header className="app-header">
        <div className="header-inner">
          <div className="header-logo">
            <span className="logo-icon">🏗️</span>
            <span className="logo-text">StructFlow</span>
            <span className="logo-badge">AI 구조 설계</span>
          </div>
          <div className="header-subtitle">
            자연어로 입력 → Claude AI 파라미터 추출 → 구조 시뮬레이션
          </div>
        </div>
      </header>

      {/* 메인 2-column 레이아웃 */}
      <main className="app-main">
        {/* 좌측: 입력 + 결과 */}
        <section className="left-panel">
          <InputPanel onSubmit={handleSubmit} loading={loading} />

          {error && (
            <div className="error-box">
              <span>❌</span> {error}
            </div>
          )}

          {result && <ResultCard data={result} />}
        </section>

        {/* 우측: Unity 3D */}
        <section className="right-panel">
          <div className="unity-panel-header">
            <span>🧊</span> 3D 파이프 시뮬레이션
          </div>
          <div className="unity-panel-body">
            <UnityViewer
              result={latestResult}
              onUnityReady={handleUnityReady}
            />
          </div>
        </section>
      </main>

      {/* 전체 너비 하단 수식 설명 패널 */}
      <section className="formula-section">
        {result
          ? <FormulaPanel data={result} />
          : (
            <div className="formula-empty">
              <span className="formula-empty-icon">📐</span>
              <p className="formula-empty-title">계산 근거 &amp; 공학 수식</p>
              <p className="formula-empty-desc">
                시뮬레이션을 실행하면 Manning 공식, 충만율, 구조 안전율 등<br />
                상세한 계산 근거와 수치가 여기에 표시됩니다.
              </p>
            </div>
          )
        }
      </section>
    </div>
  );
}
