// StructFlowUnity/Assets/Scripts/Editor/SceneSetup.cs
// Unity Editor 메뉴 → StructFlow → Setup Scene 실행 시
// StructFlow 시연에 필요한 전체 GameObject 계층 구조를 자동 생성한다.
//
// 생성 구조:
//  [Main Camera]
//  [Directional Light]
//  [StructFlowRoot]
//    ├─ PipeController  (SimulationApiClient + PipeViewController)
//    ├─ PipeBody        (PipeRenderer + MeshFilter + MeshRenderer)
//    └─ Canvas
//         └─ ResultPanel
//              ├─ StatusPanel
//              │    ├─ StatusLabel (Text)
//              │    └─ StatusBar   (Image)
//              ├─ FlowPanel
//              │    ├─ VelocityLabel   (Text)
//              │    ├─ FlowRateLabel   (Text)
//              │    ├─ FillRatioLabel  (Text)
//              │    └─ FillRatioSlider (Slider)
//              ├─ StressPanel
//              │    ├─ SafetyFactorLabel (Text)
//              │    └─ MaxStressLabel    (Text)
//              └─ SummaryPanel
//                   ├─ SummaryLabel  (Text)
//                   └─ WarningLabel  (Text)
//              └─ SimulateButton (Button)

#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using StructFlow.UnityView;
using StructFlow.UnityView.UI;

namespace StructFlow.UnityView.Editor
{
    public static class SceneSetup
    {
        [MenuItem("StructFlow/▶ Setup Scene (자동 씬 생성)", priority = 1)]
        public static void SetupScene()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "StructFlow Scene Setup",
                "현재 씬에 StructFlow 전체 오브젝트 계층을 생성합니다.\n" +
                "기존 'StructFlowRoot' 오브젝트가 있으면 삭제 후 재생성합니다.\n\n계속하시겠습니까?",
                "생성", "취소");

            if (!proceed) return;

            // 기존 루트 삭제
            var existing = GameObject.Find("StructFlowRoot");
            if (existing != null) Object.DestroyImmediate(existing);

            // ── 카메라 & 조명 설정 ──────────────────────────────────────
            SetupCamera();
            SetupLight();

            // ── 루트 오브젝트 ────────────────────────────────────────────
            var root = new GameObject("StructFlowRoot");

            // ── PipeController ───────────────────────────────────────────
            var controllerGo = new GameObject("PipeController");
            controllerGo.transform.SetParent(root.transform, false);

            var apiClient  = controllerGo.AddComponent<SimulationApiClient>();
            var controller = controllerGo.AddComponent<PipeViewController>();

            // ── PipeBody ─────────────────────────────────────────────────
            var pipeBodyGo = new GameObject("PipeBody");
            pipeBodyGo.transform.SetParent(root.transform, false);
            pipeBodyGo.AddComponent<MeshFilter>();
            pipeBodyGo.AddComponent<MeshRenderer>();
            var pipeRenderer = pipeBodyGo.AddComponent<PipeRenderer>();

            // 파이프를 씬 중앙, 카메라가 바라보는 방향으로 배치
            pipeBodyGo.transform.position  = new Vector3(0f, 0f, 0f);
            pipeBodyGo.transform.rotation  = Quaternion.identity;

            // ── Canvas & ResultPanel ─────────────────────────────────────
            var (canvas, resultPanel) = CreateUI(root);

            // ── PipeViewController 참조 주입 (Reflection으로 private field 설정) ──
            SetPrivateField(controller, "apiClient",   apiClient);
            SetPrivateField(controller, "pipeRenderer", pipeRenderer);
            SetPrivateField(controller, "resultPanel",  resultPanel);

            // ── 씬 더티 마킹 & 저장 프롬프트 ─────────────────────────────
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Selection.activeGameObject = controllerGo;
            EditorGUIUtility.PingObject(controllerGo);

            Debug.Log("[StructFlow] Scene setup complete! Play 모드에서 Inspector의 '▶ 시뮬레이션 실행' 버튼을 눌러 테스트하세요.");
            EditorUtility.DisplayDialog("완료",
                "씬 생성이 완료되었습니다.\n\n" +
                "1. SimulationEngine (포트 5000)을 실행하세요.\n" +
                "2. Unity ▶ Play 버튼을 누르세요.\n" +
                "3. Hierarchy > PipeController 선택 후\n" +
                "   Inspector의 '▶ 시뮬레이션 실행' 버튼을 클릭하세요.",
                "확인");
        }

        // ── 카메라 ─────────────────────────────────────────────────────────

        private static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            // 파이프 측면 45° 비스듬히 바라보는 위치
            cam.transform.position = new Vector3(-4f, 2.5f, -4f);
            cam.transform.LookAt(new Vector3(2.5f, 0f, 2.5f));
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        // ── 조명 ──────────────────────────────────────────────────────────

        private static void SetupLight()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light dirLight = lights.FirstOrDefault(l => l.type == LightType.Directional);

            if (dirLight == null)
            {
                var lightGo = new GameObject("Directional Light");
                dirLight = lightGo.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            dirLight.intensity = 1.2f;
            dirLight.color = new Color(1f, 0.95f, 0.85f);
        }

        // ── UI 생성 ───────────────────────────────────────────────────────

        private static (Canvas canvas, ResultPanel panel) CreateUI(GameObject root)
        {
            // Canvas
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(root.transform, false);
            var canvas       = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            // EventSystem (있으면 건너뜀)
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // ResultPanel 루트 (우측 하단)
            var panelGo = CreateRectObject("ResultPanel", canvasGo.transform);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin  = new Vector2(1f, 0f);
            panelRect.anchorMax  = new Vector2(1f, 0f);
            panelRect.pivot      = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-20f, 20f);
            panelRect.sizeDelta  = new Vector2(340f, 520f);

            // 배경 이미지
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.88f);

            var resultPanel = panelGo.AddComponent<ResultPanel>();

            // ── 내부 레이아웃 ──────────────────────────────────────────
            float yOff  = -15f;
            float lineH = 32f;
            float gap   =  8f;

            // 타이틀
            var title = CreateText("TitleLabel", panelGo.transform, "StructFlow 시뮬레이션 결과",
                new Vector2(0f, yOff), new Vector2(320f, 28f), 14, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.color = new Color(0.8f, 0.9f, 1f);
            yOff -= 32f;

            // 구분선
            CreateHRule(panelGo.transform, yOff);
            yOff -= 18f;

            // 전체 상태
            var statusLabel = CreateText("StatusLabel", panelGo.transform, "-- 상태 --",
                new Vector2(0f, yOff), new Vector2(200f, lineH), 13, TextAnchor.MiddleLeft);
            yOff -= lineH + gap;

            // 상태 인디케이터 (색상 바)
            var statusBarGo = CreateRectObject("StatusBar", panelGo.transform);
            var sbRect = statusBarGo.GetComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(0.5f, 1f);
            sbRect.anchorMax = new Vector2(0.5f, 1f);
            sbRect.pivot     = new Vector2(0.5f, 1f);
            sbRect.anchoredPosition = new Vector2(120f, yOff + lineH);
            sbRect.sizeDelta = new Vector2(100f, 20f);
            var statusBar = statusBarGo.AddComponent<Image>();
            statusBar.color = new Color(0.2f, 0.8f, 0.2f);
            yOff -= gap;

            // 유량 섹션
            CreateSectionLabel("유량", panelGo.transform, ref yOff);
            var velocityLabel   = CreateLabelRow("유속",   panelGo.transform, ref yOff, lineH, gap);
            var flowRateLabel   = CreateLabelRow("유량",   panelGo.transform, ref yOff, lineH, gap);
            var fillRatioLabel  = CreateLabelRow("충만율", panelGo.transform, ref yOff, lineH, gap);

            // 충만율 슬라이더
            var sliderGo = CreateRectObject("FillSlider", panelGo.transform);
            var sliderRect = sliderGo.GetComponent<RectTransform>();
            PositionRow(sliderRect, yOff, 280f, 20f);
            var fillSlider = sliderGo.AddComponent<Slider>();
            fillSlider.minValue = 0f;
            fillSlider.maxValue = 1f;
            fillSlider.value    = 0.75f;
            // 슬라이더 배경
            var sliderBg   = CreateRectChild("Background", sliderGo.transform);
            sliderBg.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);
            StretchFull(sliderBg.GetComponent<RectTransform>());
            // 슬라이더 Fill
            var fillArea   = CreateRectChild("Fill Area", sliderGo.transform);
            StretchFull(fillArea.GetComponent<RectTransform>());
            var fillImg    = CreateRectChild("Fill", fillArea.transform);
            fillImg.AddComponent<Image>().color = new Color(0.2f, 0.7f, 0.3f);
            StretchFull(fillImg.GetComponent<RectTransform>());
            fillSlider.fillRect = fillImg.GetComponent<RectTransform>();
            yOff -= 28f + gap;

            // 응력 섹션
            CreateSectionLabel("응력", panelGo.transform, ref yOff);
            var safetyFactorLabel = CreateLabelRow("안전율",   panelGo.transform, ref yOff, lineH, gap);
            var maxStressLabel    = CreateLabelRow("최대응력", panelGo.transform, ref yOff, lineH, gap);

            // 요약 섹션
            CreateHRule(panelGo.transform, yOff);
            yOff -= 18f;
            var summaryLabel = CreateText("SummaryLabel", panelGo.transform, "",
                new Vector2(0f, yOff), new Vector2(320f, 52f), 11, TextAnchor.UpperLeft);
            yOff -= 60f;

            // 경고 레이블
            var warningLabel = CreateText("WarningLabel", panelGo.transform, "",
                new Vector2(0f, yOff), new Vector2(320f, 40f), 11, TextAnchor.UpperLeft);
            warningLabel.color = new Color(1f, 0.8f, 0.2f);
            warningLabel.gameObject.SetActive(false);
            yOff -= 48f;

            // ── 시뮬레이션 실행 버튼 ──────────────────────────────────
            var btnGo = CreateRectObject("SimulateButton", canvasGo.transform);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0f, 0f);
            btnRect.anchorMax = new Vector2(0f, 0f);
            btnRect.pivot     = new Vector2(0f, 0f);
            btnRect.anchoredPosition = new Vector2(20f, 20f);
            btnRect.sizeDelta = new Vector2(200f, 48f);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.45f, 0.85f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.25f, 0.55f, 0.95f);
            btnColors.pressedColor     = new Color(0.10f, 0.35f, 0.70f);
            btn.colors = btnColors;

            var btnLabel = CreateText("BtnLabel", btnGo.transform, "▶  시뮬레이션 실행",
                Vector2.zero, Vector2.zero, 13, TextAnchor.MiddleCenter, FontStyle.Bold);
            var btnLabelRect = btnLabel.GetComponent<RectTransform>();
            btnLabelRect.anchorMin = Vector2.zero;
            btnLabelRect.anchorMax = Vector2.one;
            btnLabelRect.sizeDelta = Vector2.zero;

            // ── ResultPanel 필드 주입 ─────────────────────────────────
            SetPrivateField(resultPanel, "statusLabel",      statusLabel);
            SetPrivateField(resultPanel, "statusIndicator",  statusBar);
            SetPrivateField(resultPanel, "velocityLabel",    velocityLabel);
            SetPrivateField(resultPanel, "flowRateLabel",    flowRateLabel);
            SetPrivateField(resultPanel, "fillRatioLabel",   fillRatioLabel);
            SetPrivateField(resultPanel, "fillRatioSlider",  fillSlider);
            SetPrivateField(resultPanel, "safetyFactorLabel", safetyFactorLabel);
            SetPrivateField(resultPanel, "maxStressLabel",    maxStressLabel);
            SetPrivateField(resultPanel, "summaryLabel",      summaryLabel);
            SetPrivateField(resultPanel, "warningLabel",      warningLabel);

            // ── 버튼 클릭 이벤트는 Play 모드에서 PipeViewController에 연결 ──
            // (serialized UnityEvent는 에디터에서 GetComponent로 찾아 연결)

            return (canvas, resultPanel);
        }

        // ── UI 헬퍼 ───────────────────────────────────────────────────────

        private static Text CreateText(
            string name, Transform parent, string text,
            Vector2 pos, Vector2 size,
            int fontSize = 12,
            TextAnchor align = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal)
        {
            var go   = CreateRectObject(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot     = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var t = go.AddComponent<Text>();
            t.text      = text;
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize  = fontSize;
            t.alignment = align;
            t.fontStyle = style;
            t.color     = Color.white;
            return t;
        }

        private static Text CreateLabelRow(
            string labelText, Transform parent, ref float yOff,
            float lineH, float gap)
        {
            var lbl = CreateText(labelText + "HeaderLbl", parent,
                labelText + ":",
                new Vector2(-70f, yOff), new Vector2(90f, lineH),
                12, TextAnchor.MiddleLeft);
            lbl.color = new Color(0.7f, 0.7f, 0.7f);

            var val = CreateText(labelText + "ValLbl", parent,
                "--",
                new Vector2(95f, yOff), new Vector2(180f, lineH),
                12, TextAnchor.MiddleLeft);
            yOff -= lineH + gap;
            return val;
        }

        private static void CreateSectionLabel(string text, Transform parent, ref float yOff)
        {
            var lbl = CreateText(text + "Section", parent, "[ " + text + " ]",
                new Vector2(0f, yOff), new Vector2(320f, 22f),
                11, TextAnchor.MiddleLeft, FontStyle.Bold);
            lbl.color = new Color(0.6f, 0.85f, 1f);
            yOff -= 26f;
        }

        private static void CreateHRule(Transform parent, float yOff)
        {
            var go   = CreateRectObject("HRule", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot     = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yOff);
            rect.sizeDelta = new Vector2(310f, 1f);
            go.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f);
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var go   = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static GameObject CreateRectChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private static void PositionRow(RectTransform rt, float yOff, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yOff);
            rt.sizeDelta = new Vector2(w, h);
        }

        // ── Reflection 헬퍼 (private [SerializeField] 주입) ──────────────

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
#endif
