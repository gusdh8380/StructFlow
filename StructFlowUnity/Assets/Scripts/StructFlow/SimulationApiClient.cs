using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace StructFlow.UnityView
{
    /// <summary>
    /// SimulationEngine REST API와 통신하는 클라이언트.
    /// POST /api/simulate → SimulationResult JSON 수신
    /// </summary>
    public class SimulationApiClient : MonoBehaviour
    {
        [Header("API 설정")]
        public string apiBaseUrl = "http://localhost:5000";

        public event Action<SimulationResponse> OnResultReceived;
        public event Action<string>             OnError;

        /// <summary>설계 스키마 JSON을 SimulationEngine에 전송</summary>
        public void Simulate(string schemaJson)
        {
            StartCoroutine(PostSimulate(schemaJson));
        }

        private IEnumerator PostSimulate(string schemaJson)
        {
            var url     = $"{apiBaseUrl}/api/simulate";
            var body    = Encoding.UTF8.GetBytes(schemaJson);
            var request = new UnityWebRequest(url, "POST")
            {
                uploadHandler   = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                OnError?.Invoke($"API 오류: {request.error}\n{request.downloadHandler.text}");
                yield break;
            }

            try
            {
                var result = JsonUtility.FromJson<SimulationResponse>(
                    request.downloadHandler.text);
                OnResultReceived?.Invoke(result);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"JSON 파싱 실패: {ex.Message}");
            }
        }
    }

    // ── JSON 응답 모델 ─────────────────────────────────────────────
    [Serializable]
    public class SimulationResponse
    {
        public string pipe_id;
        public string calculated_at;
        public FlowData   flow;
        public StressData stress;
        public string[] warnings;
        public string overall_status;
        public string summary;
    }

    [Serializable]
    public class FlowData
    {
        public float velocity_ms;
        public float flow_rate_m3_s;
        public float fill_ratio;
        public string status;
    }

    [Serializable]
    public class StressData
    {
        public float max_stress_kpa;
        public float safety_factor;
        public string status;
    }
}
