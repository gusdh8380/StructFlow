// UnityView/PipeRenderer.cs
// Unity Engine 의존성: UnityEngine 네임스페이스 (Unity 프로젝트 내에서만 컴파일)
//
// Procedural Mesh 기반 파이프 렌더러.
// diameter_mm, length_m 파라미터를 실시간으로 읽어 메시를 재생성한다.
// fill_ratio에 따라 내부 유체(파란색 볼륨)를 시각화한다.
// status에 따라 파이프 색상을 변경한다: 녹색(NORMAL) / 노란색(WARNING) / 빨간색(DANGER)

#if UNITY_EDITOR || UNITY_RUNTIME
using UnityEngine;
using StructFlow.SimulationEngine;
using StructFlow.UnityView.Interfaces;

namespace StructFlow.UnityView
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PipeRenderer : MonoBehaviour, IViewRenderer
    {
        [Header("파이프 파라미터 (DataAdapter에서 자동 주입)")]
        [SerializeField] private float diameterMm = 300f;
        [SerializeField] private float lengthM    = 50f;

        [Header("렌더링 설정")]
        [SerializeField] private int   segments      = 24;    // 원 분할 수
        [SerializeField] private float wallThickness = 0.05f; // 관 두께 비율 (직경 대비)

        [Header("상태 색상")]
        [SerializeField] private Color colorNormal  = new(0.2f, 0.8f, 0.2f);   // 녹색
        [SerializeField] private Color colorWarning = new(1.0f, 0.8f, 0.0f);   // 노란색
        [SerializeField] private Color colorDanger  = new(0.9f, 0.2f, 0.2f);   // 빨간색
        [SerializeField] private Color colorFluid   = new(0.1f, 0.4f, 0.9f, 0.6f); // 반투명 파란색

        private MeshFilter   _meshFilter;
        private MeshRenderer _meshRenderer;
        private GameObject   _fluidObject;
        private SimulationResult? _lastResult;

        private void Awake()
        {
            _meshFilter   = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        // ── IViewRenderer ────────────────────────────────────────────────

        public void Render(SimulationResult result)
        {
            _lastResult = result;

            if (result.Flow != null)
            {
                // DataAdapter에서 파라미터를 주입받은 값으로 메시 재생성
                RebuildPipeMesh();
                UpdateFluidVolume(result.Flow.FillRatio);
                UpdatePipeColor(result.OverallStatus);
            }
        }

        public void Clear()
        {
            _meshFilter.mesh = null;
            if (_fluidObject != null)
                Destroy(_fluidObject);
        }

        // ── 파이프 메시 생성 ──────────────────────────────────────────────

        /// <summary>
        /// Procedural Mesh로 원형 파이프(중공 실린더)를 생성한다.
        /// 외벽과 내벽을 별도 링으로 구성하여 단면 두께를 표현한다.
        /// </summary>
        [ContextMenu("메시 재생성")]
        public void RebuildPipeMesh()
        {
            float outerRadius = (diameterMm / 1000f) / 2f;  // mm → m 변환
            float innerRadius = outerRadius * (1f - wallThickness);
            float halfLength  = lengthM / 2f;

            var mesh = new Mesh { name = "PipeMesh" };

            int verticesPerRing = segments * 2; // 외벽 + 내벽
            int ringCount       = 2;            // 앞면 + 뒷면
            var vertices  = new Vector3[verticesPerRing * ringCount];
            var normals   = new Vector3[vertices.Length];
            var uvs       = new Vector2[vertices.Length];
            var triangles = new int[segments * 4 * 6]; // 4면(외벽앞/뒤, 내벽앞/뒤) × 6 인덱스

            // 정점 배치
            for (int ring = 0; ring < ringCount; ring++)
            {
                float z = ring == 0 ? -halfLength : halfLength;
                for (int i = 0; i < segments; i++)
                {
                    float angle = 2f * Mathf.PI * i / segments;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    int baseIdx = (ring * verticesPerRing) + (i * 2);

                    // 외벽 정점
                    vertices[baseIdx]     = new Vector3(cos * outerRadius, sin * outerRadius, z);
                    normals[baseIdx]      = new Vector3(cos, sin, 0);
                    uvs[baseIdx]          = new Vector2((float)i / segments, ring);

                    // 내벽 정점
                    vertices[baseIdx + 1] = new Vector3(cos * innerRadius, sin * innerRadius, z);
                    normals[baseIdx + 1]  = new Vector3(-cos, -sin, 0);
                    uvs[baseIdx + 1]      = new Vector2((float)i / segments, ring);
                }
            }

            // 삼각형 인덱스 — 외벽 측면
            int triIdx = 0;
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int a = i * 2;
                int b = next * 2;
                int c = verticesPerRing + i * 2;
                int d = verticesPerRing + next * 2;

                // 외벽 쿼드
                triangles[triIdx++] = a; triangles[triIdx++] = c; triangles[triIdx++] = b;
                triangles[triIdx++] = b; triangles[triIdx++] = c; triangles[triIdx++] = d;

                // 내벽 쿼드 (법선 반전)
                triangles[triIdx++] = a + 1; triangles[triIdx++] = b + 1; triangles[triIdx++] = c + 1;
                triangles[triIdx++] = b + 1; triangles[triIdx++] = d + 1; triangles[triIdx++] = c + 1;
            }

            mesh.vertices  = vertices;
            mesh.normals   = normals;
            mesh.uv        = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            _meshFilter.mesh = mesh;
        }

        // ── 유체 볼륨 시각화 ─────────────────────────────────────────────

        /// <summary>
        /// fill_ratio 값에 따라 파이프 내부 유체를 파란색 볼륨으로 시각화한다.
        /// </summary>
        private void UpdateFluidVolume(double fillRatio)
        {
            if (_fluidObject == null)
            {
                _fluidObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _fluidObject.name = "FluidVolume";
                _fluidObject.transform.SetParent(transform, false);

                var mat = new Material(Shader.Find("Standard"));
                mat.color = colorFluid;
                mat.SetFloat("_Mode", 3); // Transparent
                _fluidObject.GetComponent<MeshRenderer>().material = mat;

                // 충돌체 제거 (시각화 전용)
                Destroy(_fluidObject.GetComponent<Collider>());
            }

            float outerRadius = (diameterMm / 1000f) / 2f;
            float fluidRadius = outerRadius * (float)fillRatio;

            _fluidObject.transform.localScale = new Vector3(
                fluidRadius * 2f,
                lengthM / 2f,  // Unity Cylinder 높이 = scale.y × 2
                fluidRadius * 2f
            );
            _fluidObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // ── 파이프 색상 업데이트 ─────────────────────────────────────────

        private void UpdatePipeColor(string status)
        {
            var color = status switch
            {
                "WARNING" => colorWarning,
                "DANGER"  => colorDanger,
                _         => colorNormal
            };

            if (_meshRenderer.material != null)
                _meshRenderer.material.color = color;
        }

        // ── 에디터 유틸리티 ──────────────────────────────────────────────

        /// <summary>파라미터 변경 시 에디터에서 실시간 미리보기</summary>
        private void OnValidate()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            RebuildPipeMesh();
        }
    }
}
#endif
