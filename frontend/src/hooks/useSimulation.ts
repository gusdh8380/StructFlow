import { useState, useRef } from 'react';
import type { WebhookResponse, SimulationResult } from '../types/simulation';

interface UseSimulationReturn {
  result: WebhookResponse | null;
  loading: boolean;
  error: string | null;
  submit: (input: string) => Promise<SimulationResult | null>;
}

export function useSimulation(): UseSimulationReturn {
  const [result, setResult] = useState<WebhookResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Unity 인스턴스 참조 (UnityViewer에서 설정)
  const unityRef = useRef<{ sendMessage: (go: string, method: string, value: string) => void } | null>(null);

  const submit = async (input: string): Promise<SimulationResult | null> => {
    if (!input.trim()) return null;

    setLoading(true);
    setError(null);

    try {
      const res = await fetch('/webhook/structflow/design', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ input }),
      });

      if (!res.ok) {
        throw new Error(`서버 오류: ${res.status} ${res.statusText}`);
      }

      const data: WebhookResponse = await res.json();
      setResult(data);

      // Unity WebGL에 결과 전달 (Unity가 로드된 경우)
      if (unityRef.current && data.result) {
        unityRef.current.sendMessage(
          'PipeController',
          'ReceiveSimulationResult',
          JSON.stringify(data.result)
        );
      }

      return data.result;
    } catch (err) {
      const message = err instanceof Error ? err.message : '알 수 없는 오류가 발생했습니다.';
      setError(message);
      return null;
    } finally {
      setLoading(false);
    }
  };

  return { result, loading, error, submit };
}
