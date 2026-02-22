using System;
using UnityEngine;
using UnityEngine.UI;
using StructFlow.UnityView.UI;

namespace StructFlow.UnityView
{
    /// <summary>
    /// 씬 전체를 조율하는 컨트롤러.
    /// SimulationApiClient → PipeRenderer + ResultPanel 연결.
    /// </summary>
    [DisallowMultipleComponent]
    public class PipeViewController : MonoBehaviour
    {
        [Header("컴포넌트 참조")]
        [SerializeField] private SimulationApiClient apiClient;
        [SerializeField] private PipeRenderer        pipeRenderer;
        [SerializeField] private ResultPanel         resultPanel;

        [Header("테스트 파라미터 (Inspector에서 수정)")]
        [SerializeField] private float  diameterMm    = 600f;
        [SerializeField] private float  lengthM       = 50f;
        [SerializeField] private string material      = "concrete";
        [SerializeField] private float  slope         = 0.01f;
        [SerializeField] private float  roughness     = 0.013f;
        [SerializeField] private float  soilDepthM    = 2f;
        [SerializeField] private float  trafficLoadKn = 50f;

        private void Awake()
        {
            if (apiClient == null) apiClient = GetComponent<SimulationApiClient>();

            apiClient.OnResultReceived += HandleResult;
            apiClient.OnError          += HandleError;
        }

        private void Start()
        {
            // SceneSetup이 생성한 "SimulateButton"을 씬에서 찾아 클릭 이벤트 연결
            var btnGo = GameObject.Find("SimulateButton");
            if (btnGo != null)
            {
                var btn = btnGo.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(RunSimulation);
            }
        }

        /// <summary>
        /// JavaScript(웹 프론트엔드)에서 SendMessage()로 호출.
        /// n8n → SimulationEngine 결과 JSON을 직접 수신하여 씬을 업데이트한다.
        /// </summary>
        /// <param name="json">SimulationResponse 전체 또는 result 필드 JSON 문자열</param>
        public void ReceiveSimulationResult(string json)
        {
            try
            {
                var result = JsonUtility.FromJson<SimulationResponse>(json);
                if (result == null)
                {
                    HandleError("ReceiveSimulationResult: JSON 파싱 결과가 null입니다.");
                    return;
                }
                Debug.Log($"[StructFlow] WebGL 결과 수신: {result.overall_status}");
                HandleResult(result);
            }
            catch (Exception ex)
            {
                HandleError($"ReceiveSimulationResult 파싱 실패: {ex.Message}");
            }
        }

        /// <summary>Inspector 또는 UI 버튼에서 호출</summary>
        public void RunSimulation()
        {
            var schema = BuildSchemaJson();
            Debug.Log($"[StructFlow] Simulating: D={diameterMm}mm, slope={slope}");
            apiClient.Simulate(schema);
        }

        private void HandleResult(SimulationResponse result)
        {
            Debug.Log($"[StructFlow] Result: {result.overall_status}, fill={result.flow?.fill_ratio:P0}");

            // PipeRenderer 업데이트
            if (pipeRenderer != null)
            {
                pipeRenderer.SetDiameter(diameterMm / 1000f);
                pipeRenderer.SetLength(lengthM);
                pipeRenderer.UpdateFluidVolume(result.flow?.fill_ratio ?? 0f);
                pipeRenderer.SetStatus(result.overall_status);
            }

            // ResultPanel 업데이트
            if (resultPanel != null)
            {
                resultPanel.Display(result);
            }
        }

        private void HandleError(string error)
        {
            Debug.LogError($"[StructFlow] API Error: {error}");
            if (resultPanel != null) resultPanel.ShowError(error);
        }

        private string BuildSchemaJson()
        {
            return $@"{{
                ""schema_version"": ""1.0"",
                ""pipe"": {{
                    ""id"": ""PIPE-UNITY"",
                    ""diameter_mm"": {diameterMm},
                    ""length_m"": {lengthM},
                    ""material"": ""{material}"",
                    ""slope"": {slope},
                    ""roughness_coefficient"": {roughness}
                }},
                ""load"": {{
                    ""soil_depth_m"": {soilDepthM},
                    ""traffic_load_kn"": {trafficLoadKn},
                    ""internal_pressure_kpa"": 10.0
                }},
                ""environment"": {{
                    ""flow_type"": ""gravity"",
                    ""fluid"": ""wastewater""
                }},
                ""is_validated"": true
            }}";
        }
    }
}
