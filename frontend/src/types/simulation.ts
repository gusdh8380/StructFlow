export interface FlowData {
  velocity_ms: number;
  flow_rate_m3_s: number;
  fill_ratio: number;
  status: 'NORMAL' | 'WARNING' | 'DANGER';
}

export interface StressData {
  max_stress_kpa: number;
  safety_factor: number;
  status: 'SAFE' | 'WARNING' | 'DANGER';
}

export interface SimulationResult {
  pipe_id: string;
  calculated_at: string;
  flow: FlowData;
  stress: StressData;
  warnings: string[];
  overall_status: 'NORMAL' | 'WARNING' | 'DANGER' | 'ERROR';
  summary: string;
}

// n8n webhook 응답 전체 구조
export interface WebhookResponse {
  success: boolean;
  result: SimulationResult;
  unity_summary: {
    pipe_id: string;
    calculated_at: string;
    status: string;
    velocity_ms: string;
    flow_rate_m3s: string;
    fill_ratio: string;
    safety_factor: string;
    summary: string;
    warnings: string;
  };
  natural_summary: string;
}
