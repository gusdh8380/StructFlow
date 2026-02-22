// StructFlowUnity/Assets/Scripts/Editor/ParametricInspector.cs
// Unity Editor 전용 커스텀 인스펙터.
// PipeViewController에 "▶ 시뮬레이션 실행" 버튼과 파라미터 유효성 힌트를 추가한다.
// 외부 StructFlow C# 라이브러리 참조 없음 — Unity Editor 전용.

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using StructFlow.UnityView;

namespace StructFlow.UnityView.Editor
{
    [CustomEditor(typeof(PipeViewController))]
    public class ParametricInspector : UnityEditor.Editor
    {
        // ── 인스펙터 캐시 ─────────────────────────────────────────────────

        private SerializedProperty _apiClient;
        private SerializedProperty _pipeRenderer;
        private SerializedProperty _resultPanel;
        private SerializedProperty _diameterMm;
        private SerializedProperty _lengthM;
        private SerializedProperty _material;
        private SerializedProperty _slope;
        private SerializedProperty _roughness;
        private SerializedProperty _soilDepthM;
        private SerializedProperty _trafficLoadKn;

        private bool _pipeFoldout   = true;
        private bool _loadFoldout   = true;
        private bool _refFoldout    = false;

        private void OnEnable()
        {
            _apiClient      = serializedObject.FindProperty("apiClient");
            _pipeRenderer   = serializedObject.FindProperty("pipeRenderer");
            _resultPanel    = serializedObject.FindProperty("resultPanel");
            _diameterMm     = serializedObject.FindProperty("diameterMm");
            _lengthM        = serializedObject.FindProperty("lengthM");
            _material       = serializedObject.FindProperty("material");
            _slope          = serializedObject.FindProperty("slope");
            _roughness      = serializedObject.FindProperty("roughness");
            _soilDepthM     = serializedObject.FindProperty("soilDepthM");
            _trafficLoadKn  = serializedObject.FindProperty("trafficLoadKn");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ── 컴포넌트 참조 (접기/펼치기) ─────────────────────────────
            _refFoldout = EditorGUILayout.Foldout(_refFoldout, "컴포넌트 참조", true);
            if (_refFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_apiClient,    new GUIContent("API Client"));
                EditorGUILayout.PropertyField(_pipeRenderer, new GUIContent("Pipe Renderer"));
                EditorGUILayout.PropertyField(_resultPanel,  new GUIContent("Result Panel"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("── StructFlow 파이프 파라미터 ──", EditorStyles.boldLabel);

            // ── 파이프 파라미터 ──────────────────────────────────────────
            _pipeFoldout = EditorGUILayout.Foldout(_pipeFoldout, "파이프", true);
            if (_pipeFoldout)
            {
                EditorGUI.indentLevel++;

                // 직경 — 범위 경고
                float diam = _diameterMm.floatValue;
                if (diam < 150f || diam > 3000f)
                    EditorGUILayout.HelpBox($"직경 {diam}mm: 권장 범위 150~3000mm", MessageType.Warning);
                EditorGUILayout.PropertyField(_diameterMm, new GUIContent("직경 (mm)"));

                EditorGUILayout.PropertyField(_lengthM,    new GUIContent("길이 (m)"));

                // 재질 드롭다운
                string[] mats    = { "concrete", "ductile_iron", "pvc", "steel", "hdpe" };
                int      matIdx  = System.Array.IndexOf(mats, _material.stringValue);
                if (matIdx < 0) matIdx = 0;
                int newMatIdx = EditorGUILayout.Popup("재질", matIdx, mats);
                if (newMatIdx != matIdx) _material.stringValue = mats[newMatIdx];

                // 경사 — 범위 경고
                float slope = _slope.floatValue;
                if (slope < 0.001f || slope > 0.2f)
                    EditorGUILayout.HelpBox($"경사 {slope}: 권장 범위 0.001~0.200", MessageType.Warning);
                EditorGUILayout.PropertyField(_slope, new GUIContent("경사 (무차원)"));

                EditorGUILayout.PropertyField(_roughness, new GUIContent("Manning n (조도계수)"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // ── 하중 파라미터 ────────────────────────────────────────────
            _loadFoldout = EditorGUILayout.Foldout(_loadFoldout, "하중", true);
            if (_loadFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_soilDepthM,    new GUIContent("토피고 (m)"));
                EditorGUILayout.PropertyField(_trafficLoadKn, new GUIContent("교통 하중 (kN)"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // ── 실행 버튼 ────────────────────────────────────────────────
            // Play 모드에서만 활성화 (REST API 호출 필요)
            bool canRun = Application.isPlaying;
            if (!canRun)
            {
                EditorGUILayout.HelpBox(
                    "▶ Play 모드에서 시뮬레이션 실행 버튼을 사용하세요.\n" +
                    "SimulationEngine API(포트 5000)가 실행 중이어야 합니다.",
                    MessageType.Info);
            }

            GUI.enabled = canRun;
            if (GUILayout.Button("▶  시뮬레이션 실행", GUILayout.Height(32)))
            {
                if (target is PipeViewController ctrl)
                    ctrl.RunSimulation();
            }
            GUI.enabled = true;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
