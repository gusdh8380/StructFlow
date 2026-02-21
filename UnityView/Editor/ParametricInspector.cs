// UnityView/Editor/ParametricInspector.cs
// Unity Editor 전용 — 에디터 빌드 내에서만 컴파일된다.
//
// DesignSchema를 Inspector에서 직접 편집 가능하게 노출한다.
// 실시간 유효성 검사 — 범위 벗어나면 빨간색 하이라이트.
// "시뮬레이션 실행" 버튼 → SimulationEngine 호출 → 결과 즉시 반영.

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using StructFlow.DataAdapter;
using StructFlow.ParametricCore.Models;
using StructFlow.ParametricCore.Validators;
using StructFlow.SimulationEngine;

namespace StructFlow.UnityView.Editor
{
    /// <summary>
    /// PipeRenderer 컴포넌트에 붙은 DesignSchema를 편집하는 커스텀 인스펙터.
    /// </summary>
    [CustomEditor(typeof(PipeRenderer))]
    public class ParametricInspector : UnityEditor.Editor
    {
        // 인스펙터에 표시할 DesignSchema (직렬화는 JSON으로 별도 관리)
        private DesignSchema _schema = new();
        private SimulationResult? _lastResult;
        private string? _validationError;

        private static readonly GUIStyle ERROR_STYLE = new(GUI.skin.textField)
        {
            normal = { textColor = Color.red }
        };
        private static readonly GUIStyle SUCCESS_STYLE = new(GUI.skin.label)
        {
            normal = { textColor = Color.green }
        };
        private static readonly GUIStyle WARNING_STYLE = new(GUI.skin.label)
        {
            normal = { textColor = Color.yellow }
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("── StructFlow Parametric Designer ──", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawPipeSection();
            DrawLoadSection();
            DrawEnvironmentSection();

            EditorGUILayout.Space(5);
            DrawValidationStatus();

            EditorGUILayout.Space(10);
            DrawSimulateButton();

            if (_lastResult != null)
            {
                EditorGUILayout.Space(5);
                DrawResultSection();
            }
        }

        // ── 섹션 드로어 ───────────────────────────────────────────────────

        private void DrawPipeSection()
        {
            EditorGUILayout.LabelField("Pipe Parameters", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                _schema.Pipe.Id         = DrawTextField("ID", _schema.Pipe.Id);
                _schema.Pipe.DiameterMm = DrawDouble("Diameter (mm)", _schema.Pipe.DiameterMm, 100, 3000);
                _schema.Pipe.LengthM    = DrawDouble("Length (m)", _schema.Pipe.LengthM, 0, 10000);
                _schema.Pipe.Material   = DrawPopup("Material", _schema.Pipe.Material,
                    new[] { "concrete", "ductile_iron", "pvc", "steel", "hdpe" });
                _schema.Pipe.Slope      = DrawDouble("Slope", _schema.Pipe.Slope, 0.001, 0.2);
                _schema.Pipe.RoughnessCoefficient = DrawDouble("Manning n", _schema.Pipe.RoughnessCoefficient, 0.005, 0.05);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Load Parameters", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                _schema.Load.SoilDepthM          = DrawDouble("Soil Depth (m)", _schema.Load.SoilDepthM, 0, 30);
                _schema.Load.TrafficLoadKn        = DrawDouble("Traffic Load (kN)", _schema.Load.TrafficLoadKn, 0, 500);
                _schema.Load.InternalPressureKpa  = DrawDouble("Internal Pressure (kPa)", _schema.Load.InternalPressureKpa, 0, 1000);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Environment", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                _schema.Environment.FlowType = DrawPopup("Flow Type", _schema.Environment.FlowType,
                    new[] { "gravity", "pressure" });
                _schema.Environment.Fluid = DrawPopup("Fluid", _schema.Environment.Fluid,
                    new[] { "wastewater", "stormwater", "clean_water" });
            }
        }

        private void DrawLoadSection() { /* DrawPipeSection에 통합 */ }
        private void DrawEnvironmentSection() { /* DrawPipeSection에 통합 */ }

        private void DrawValidationStatus()
        {
            var validation = ParameterValidator.Validate(_schema);
            if (validation.IsValid)
            {
                EditorGUILayout.LabelField("✓ 유효성 검사 통과", SUCCESS_STYLE);
                _validationError = null;
            }
            else
            {
                EditorGUILayout.LabelField($"✗ {validation.FirstError}", ERROR_STYLE);
                _validationError = validation.FirstError;
            }
        }

        private void DrawSimulateButton()
        {
            GUI.enabled = _validationError == null;
            if (GUILayout.Button("▶ 시뮬레이션 실행", GUILayout.Height(30)))
            {
                RunSimulation();
            }
            GUI.enabled = true;
        }

        private void DrawResultSection()
        {
            EditorGUILayout.LabelField("── 시뮬레이션 결과 ──", EditorStyles.boldLabel);

            var statusStyle = _lastResult!.OverallStatus switch
            {
                "NORMAL"  => SUCCESS_STYLE,
                "WARNING" => WARNING_STYLE,
                _         => ERROR_STYLE
            };

            EditorGUILayout.LabelField($"전체 상태: {_lastResult.OverallStatus}", statusStyle);

            if (_lastResult.Flow != null)
            {
                EditorGUILayout.LabelField($"유속: {_lastResult.Flow.VelocityMs:F2} m/s");
                EditorGUILayout.LabelField($"유량: {_lastResult.Flow.FlowRateM3S:F4} m³/s");
                EditorGUILayout.LabelField($"충만율: {_lastResult.Flow.FillRatio:P0} [{_lastResult.Flow.Status}]");
            }

            if (_lastResult.Stress != null)
            {
                EditorGUILayout.LabelField($"안전율: {_lastResult.Stress.SafetyFactor:F2} [{_lastResult.Stress.Status}]");
                EditorGUILayout.LabelField($"최대응력: {_lastResult.Stress.MaxStressKpa:F1} kPa");
            }

            if (!string.IsNullOrEmpty(_lastResult.Summary))
                EditorGUILayout.HelpBox(_lastResult.Summary, MessageType.Info);
        }

        // ── 시뮬레이션 실행 ───────────────────────────────────────────────

        private void RunSimulation()
        {
            _schema.IsValidated = true;
            var engine = new PipeSimulationEngine();
            _lastResult = engine.Run(_schema);

            // PipeRenderer에 결과 반영
            if (target is PipeRenderer renderer)
                renderer.Render(_lastResult);

            Repaint();
        }

        // ── 헬퍼 드로어 ───────────────────────────────────────────────────

        private static double DrawDouble(string label, double value, double min, double max)
        {
            bool outOfRange = value < min || value > max;
            if (outOfRange) GUI.color = Color.red;
            double result = EditorGUILayout.DoubleField(label, value);
            GUI.color = Color.white;
            return result;
        }

        private static string DrawTextField(string label, string value) =>
            EditorGUILayout.TextField(label, value ?? string.Empty);

        private static string DrawPopup(string label, string current, string[] options)
        {
            int idx = System.Array.IndexOf(options, current);
            if (idx < 0) idx = 0;
            int newIdx = EditorGUILayout.Popup(label, idx, options);
            return options[newIdx];
        }
    }
}
#endif
