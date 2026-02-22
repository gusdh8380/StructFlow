import { useEffect, useRef, useState } from 'react';
import { Unity, useUnityContext } from 'react-unity-webgl';
import type { SimulationResult } from '../types/simulation';

interface UnityViewerProps {
  result: SimulationResult | null;
  onUnityReady?: (sendFn: (go: string, method: string, val: string) => void) => void;
}

// Unity WebGL 빌드 경로 (public/unity/ 에 빌드 파일 위치)
const UNITY_CONFIG = {
  loaderUrl:      '/unity/Build/StructFlow.loader.js',
  dataUrl:        '/unity/Build/StructFlow.data.gz',
  frameworkUrl:   '/unity/Build/StructFlow.framework.js.gz',
  codeUrl:        '/unity/Build/StructFlow.wasm.gz',
};

export default function UnityViewer({ result, onUnityReady }: UnityViewerProps) {
  const [buildReady, setBuildReady] = useState(false);
  const sentRef = useRef<string | null>(null);

  // Unity 빌드 파일이 존재하는지 확인
  useEffect(() => {
    fetch(UNITY_CONFIG.loaderUrl, { method: 'HEAD' })
      .then(r => setBuildReady(r.ok))
      .catch(() => setBuildReady(false));
  }, []);

  if (!buildReady) {
    return <UnityPlaceholder />;
  }

  return (
    <UnityCanvas
      result={result}
      onUnityReady={onUnityReady}
      sentRef={sentRef}
    />
  );
}

function UnityCanvas({
  result,
  onUnityReady,
  sentRef,
}: {
  result: SimulationResult | null;
  onUnityReady?: (fn: (go: string, m: string, v: string) => void) => void;
  sentRef: React.MutableRefObject<string | null>;
}) {
  const { unityProvider, sendMessage, isLoaded, loadingProgression } = useUnityContext(UNITY_CONFIG);

  // Unity 로드 완료 시 sendMessage 함수를 상위에 전달
  useEffect(() => {
    if (isLoaded && onUnityReady) {
      onUnityReady(sendMessage);
    }
  }, [isLoaded, onUnityReady, sendMessage]);

  // 결과가 바뀔 때마다 Unity에 전달
  useEffect(() => {
    if (!isLoaded || !result) return;
    const json = JSON.stringify(result);
    if (sentRef.current === json) return; // 중복 전송 방지
    sentRef.current = json;
    sendMessage('PipeController', 'ReceiveSimulationResult', json);
  }, [isLoaded, result, sendMessage, sentRef]);

  return (
    <div className="unity-canvas-wrapper">
      {!isLoaded && (
        <div className="unity-loading">
          <div className="unity-loading-bar">
            <div
              className="unity-loading-fill"
              style={{ width: `${Math.round(loadingProgression * 100)}%` }}
            />
          </div>
          <div className="unity-loading-text">
            Unity 로딩 중... {Math.round(loadingProgression * 100)}%
          </div>
        </div>
      )}
      <Unity
        unityProvider={unityProvider}
        style={{
          width: '100%',
          height: '100%',
          visibility: isLoaded ? 'visible' : 'hidden',
        }}
      />
    </div>
  );
}

function UnityPlaceholder() {
  return (
    <div className="unity-placeholder">
      <div className="placeholder-inner">
        <div className="placeholder-icon">🏗️</div>
        <div className="placeholder-title">Unity 3D 시뮬레이션</div>
        <div className="placeholder-desc">
          Unity WebGL 빌드를 준비 중입니다.
        </div>
        <div className="placeholder-steps">
          <div className="step">1. Unity Hub에서 StructFlowUnity 프로젝트 열기</div>
          <div className="step">2. File → Build Settings → WebGL → Switch Platform</div>
          <div className="step">3. Build → 출력: <code>frontend/public/unity</code></div>
        </div>
        <div className="placeholder-hint">
          빌드 후 새로고침하면 3D 렌더링이 표시됩니다.
        </div>
      </div>
    </div>
  );
}
