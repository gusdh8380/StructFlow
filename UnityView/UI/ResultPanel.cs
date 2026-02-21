// UnityView/UI/ResultPanel.cs
// Unity UI (uGUI) 기반 결과 패널.
// SimulationResult를 받아 상태, 유량, 응력 수치를 화면에 표시한다.
// 계산 로직 없음 — 시각화 전용.

#if UNITY_EDITOR || UNITY_RUNTIME
using UnityEngine;
using UnityEngine.UI;
using StructFlow.DataAdapter;
using StructFlow.SimulationEngine;
using StructFlow.UnityView.Interfaces;

namespace StructFlow.UnityView.UI
{
    /// <summary>
    /// Runtime UI 패널 — SimulationResult 수치를 Text/Image로 표시한다.
    /// Canvas 하위에 배치하여 사용한다.
    /// </summary>
    public class ResultPanel : MonoBehaviour, IViewRenderer
    {
        [Header("상태 표시")]
        [SerializeField] private Text   statusLabel;
        [SerializeField] private Image  statusIndicator;

        [Header("유량 수치")]
        [SerializeField] private Text   velocityLabel;
        [SerializeField] private Text   flowRateLabel;
        [SerializeField] private Text   fillRatioLabel;
        [SerializeField] private Slider fillRatioSlider;

        [Header("응력 수치")]
        [SerializeField] private Text   safetyFactorLabel;
        [SerializeField] private Text   maxStressLabel;

        [Header("요약")]
        [SerializeField] private Text   summaryLabel;
        [SerializeField] private Text   warningLabel;

        [Header("상태 색상")]
        [SerializeField] private Color colorNormal  = new(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color colorWarning = new(1.0f, 0.8f, 0.0f);
        [SerializeField] private Color colorDanger  = new(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color colorError   = new(0.5f, 0.5f, 0.5f);

        // ── IViewRenderer ────────────────────────────────────────────────

        public void Render(SimulationResult result)
        {
            if (result == null) return;

            UpdateStatusDisplay(result.OverallStatus);
            UpdateFlowDisplay(result.Flow);
            UpdateStressDisplay(result.Stress);
            UpdateSummary(result);
        }

        public void Clear()
        {
            SetText(statusLabel, "--");
            SetText(velocityLabel, "--");
            SetText(flowRateLabel, "--");
            SetText(fillRatioLabel, "--");
            SetText(safetyFactorLabel, "--");
            SetText(maxStressLabel, "--");
            SetText(summaryLabel, string.Empty);
            SetText(warningLabel, string.Empty);

            if (fillRatioSlider != null) fillRatioSlider.value = 0;
            if (statusIndicator != null) statusIndicator.color = colorError;
        }

        // ── 섹션 업데이트 ─────────────────────────────────────────────────

        private void UpdateStatusDisplay(string status)
        {
            SetText(statusLabel, status);

            var color = status switch
            {
                "NORMAL"  => colorNormal,
                "WARNING" => colorWarning,
                "DANGER"  => colorDanger,
                _         => colorError
            };

            if (statusIndicator != null) statusIndicator.color = color;
            if (statusLabel != null)     statusLabel.color = color;
        }

        private void UpdateFlowDisplay(FlowResult? flow)
        {
            if (flow == null)
            {
                SetText(velocityLabel, "N/A");
                SetText(flowRateLabel, "N/A");
                SetText(fillRatioLabel, "N/A");
                if (fillRatioSlider != null) fillRatioSlider.value = 0;
                return;
            }

            SetText(velocityLabel, $"{flow.VelocityMs:F2} m/s");
            SetText(flowRateLabel, $"{flow.FlowRateM3S:F4} m³/s");
            SetText(fillRatioLabel, $"{flow.FillRatio:P0}  [{flow.Status}]");

            if (fillRatioSlider != null)
                fillRatioSlider.value = (float)flow.FillRatio;

            // 충만율 슬라이더 색상 반영
            if (fillRatioSlider != null)
            {
                var fillColor = flow.Status switch
                {
                    "WARNING" => colorWarning,
                    "DANGER"  => colorDanger,
                    _         => colorNormal
                };
                var fill = fillRatioSlider.fillRect?.GetComponent<Image>();
                if (fill != null) fill.color = fillColor;
            }
        }

        private void UpdateStressDisplay(StressResult? stress)
        {
            if (stress == null)
            {
                SetText(safetyFactorLabel, "N/A");
                SetText(maxStressLabel, "N/A");
                return;
            }

            SetText(safetyFactorLabel, $"{stress.SafetyFactor:F2}  [{stress.Status}]");
            SetText(maxStressLabel, $"{stress.MaxStressKpa:F1} kPa");

            if (safetyFactorLabel != null)
            {
                safetyFactorLabel.color = stress.Status switch
                {
                    "WARNING" => colorWarning,
                    "DANGER"  => colorDanger,
                    _         => colorNormal
                };
            }
        }

        private void UpdateSummary(SimulationResult result)
        {
            SetText(summaryLabel, result.Summary ?? string.Empty);

            if (warningLabel != null)
            {
                bool hasWarnings = result.Warnings.Count > 0;
                warningLabel.gameObject.SetActive(hasWarnings);
                if (hasWarnings)
                    SetText(warningLabel, "⚠ " + string.Join("\n⚠ ", result.Warnings));
            }
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────

        private static void SetText(Text label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
#endif
