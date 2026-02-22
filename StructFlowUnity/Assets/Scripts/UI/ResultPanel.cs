using UnityEngine;
using UnityEngine.UI;
using StructFlow.UnityView;

namespace StructFlow.UnityView.UI
{
    /// <summary>
    /// SimulationEngine API 결과를 화면에 표시하는 uGUI 패널.
    /// PipeViewController.HandleResult() 에서 호출된다.
    /// </summary>
    public class ResultPanel : MonoBehaviour
    {
        [Header("상태 표시")]
        [SerializeField] private Text  statusLabel;
        [SerializeField] private Image statusIndicator;

        [Header("유량 수치")]
        [SerializeField] private Text   velocityLabel;
        [SerializeField] private Text   flowRateLabel;
        [SerializeField] private Text   fillRatioLabel;
        [SerializeField] private Slider fillRatioSlider;

        [Header("응력 수치")]
        [SerializeField] private Text safetyFactorLabel;
        [SerializeField] private Text maxStressLabel;

        [Header("요약 / 경고")]
        [SerializeField] private Text summaryLabel;
        [SerializeField] private Text warningLabel;

        [Header("상태 색상")]
        [SerializeField] private Color colorNormal  = new(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color colorWarning = new(1.0f, 0.75f, 0.0f);
        [SerializeField] private Color colorDanger  = new(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color colorError   = new(0.5f, 0.5f, 0.5f);

        public void Display(SimulationResponse result)
        {
            if (result == null) return;
            ApplyStatus(result.overall_status);
            ApplyFlow(result.flow);
            ApplyStress(result.stress);
            ApplySummary(result.summary, result.warnings);
        }

        public void ShowError(string message)
        {
            ApplyStatus("ERROR");
            Set(summaryLabel, $"Error: {message}");
        }

        public void Clear()
        {
            ApplyStatus("--");
            Set(velocityLabel, "--"); Set(flowRateLabel, "--");
            Set(fillRatioLabel, "--"); Set(safetyFactorLabel, "--");
            Set(maxStressLabel, "--"); Set(summaryLabel, "");
            Set(warningLabel, "");
            if (fillRatioSlider) fillRatioSlider.value = 0f;
        }

        private void ApplyStatus(string status)
        {
            Set(statusLabel, status);
            var c = StatusColor(status);
            if (statusIndicator) statusIndicator.color = c;
            if (statusLabel)     statusLabel.color = c;
        }

        private void ApplyFlow(FlowData flow)
        {
            if (flow == null) { Set(velocityLabel, "N/A"); Set(flowRateLabel, "N/A"); return; }
            Set(velocityLabel,  $"{flow.velocity_ms:F2} m/s");
            Set(flowRateLabel,  $"{flow.flow_rate_m3_s:F4} m3/s");
            Set(fillRatioLabel, $"{flow.fill_ratio * 100f:F0}%  [{flow.status}]");
            if (fillRatioSlider)
            {
                fillRatioSlider.value = flow.fill_ratio;
                var img = fillRatioSlider.fillRect?.GetComponent<Image>();
                if (img) img.color = StatusColor(flow.status);
            }
        }

        private void ApplyStress(StressData stress)
        {
            if (stress == null) { Set(safetyFactorLabel, "N/A"); return; }
            Set(safetyFactorLabel, $"SF {stress.safety_factor:F2}  [{stress.status}]");
            Set(maxStressLabel,    $"{stress.max_stress_kpa:F1} kPa");
            if (safetyFactorLabel) safetyFactorLabel.color = StatusColor(stress.status);
        }

        private void ApplySummary(string summary, string[] warnings)
        {
            // summary field contains Korean text from API — not renderable in WebGL default font.
            // Clear it; status/numbers are already shown in other labels.
            Set(summaryLabel, "");
            if (warningLabel)
            {
                bool has = warnings != null && warnings.Length > 0;
                warningLabel.gameObject.SetActive(has);
                // Warning texts may contain Korean — show count only.
                if (has) Set(warningLabel, $"! Warnings: {warnings.Length}");
            }
        }

        private Color StatusColor(string s) => s switch
        {
            "NORMAL" or "SAFE" => colorNormal,
            "WARNING"          => colorWarning,
            "DANGER"           => colorDanger,
            _                  => colorError
        };

        private static void Set(Text t, string v) { if (t) t.text = v; }
    }
}
