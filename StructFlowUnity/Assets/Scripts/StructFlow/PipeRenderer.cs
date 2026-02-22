// StructFlowUnity/Assets/Scripts/StructFlow/PipeRenderer.cs
// Unity 독립 절차적 메시(Procedural Mesh) 파이프 렌더러.
// 외벽 속 빈 원통 + 내부 유체 실린더를 런타임에 생성하고
// SetDiameter / SetLength / UpdateFluidVolume / SetStatus API로 제어한다.
// 외부 StructFlow C# 라이브러리 참조 없음 — Unity 전용.

using UnityEngine;

namespace StructFlow.UnityView
{
    /// <summary>
    /// 절차적 메시로 하수관 단면을 시각화한다.
    /// - 외벽: 속 빈 원통(중공 실린더), 상태별 색상
    /// - 내부: 충만율(fill_ratio)에 비례하는 반투명 유체 실린더
    /// PipeViewController에서 SetDiameter / SetLength / UpdateFluidVolume / SetStatus 를 호출한다.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PipeRenderer : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("파이프 형상")]
        [SerializeField, Range(0.05f, 5f)]   private float diameterM           = 0.6f;
        [SerializeField, Range(0.5f,  20f)]  private float lengthM             = 5f;
        [SerializeField, Range(0.01f, 0.15f)] private float wallThicknessFactor = 0.06f;
        [SerializeField, Range(8, 64)]       private int   segments             = 32;

        [Header("상태 색상 (파이프 외벽)")]
        [SerializeField] private Color colorNormal  = new Color(0.30f, 0.75f, 0.30f, 0.80f);
        [SerializeField] private Color colorWarning = new Color(1.00f, 0.75f, 0.00f, 0.80f);
        [SerializeField] private Color colorDanger  = new Color(0.90f, 0.15f, 0.15f, 0.80f);
        [SerializeField] private Color colorDefault = new Color(0.55f, 0.55f, 0.60f, 0.80f);

        [Header("유체 색상")]
        [SerializeField] private Color colorFluidNormal  = new Color(0.20f, 0.50f, 0.90f, 0.55f);
        [SerializeField] private Color colorFluidWarning = new Color(1.00f, 0.75f, 0.00f, 0.55f);
        [SerializeField] private Color colorFluidDanger  = new Color(0.90f, 0.20f, 0.20f, 0.55f);

        // ── 내부 ─────────────────────────────────────────────────────────

        private MeshFilter   _pipeMeshFilter;
        private MeshRenderer _pipeMeshRenderer;
        private Material     _pipeMaterial;

        private GameObject   _fluidObject;
        private MeshFilter   _fluidMeshFilter;
        private MeshRenderer _fluidMeshRenderer;
        private Material     _fluidMaterial;

        private float  _fillRatio    = 0.75f;
        private string _status       = "NORMAL";

        // ── Unity 수명 주기 ───────────────────────────────────────────────

        private void Awake()
        {
            _pipeMeshFilter   = GetComponent<MeshFilter>();
            _pipeMeshRenderer = GetComponent<MeshRenderer>();

            _pipeMaterial = CreateMaterial(colorDefault);
            _pipeMeshRenderer.material = _pipeMaterial;

            // 유체 오브젝트 자동 생성
            _fluidObject = new GameObject("FluidVolume");
            _fluidObject.transform.SetParent(transform, false);
            _fluidMeshFilter   = _fluidObject.AddComponent<MeshFilter>();
            _fluidMeshRenderer = _fluidObject.AddComponent<MeshRenderer>();
            _fluidMaterial = CreateMaterial(colorFluidNormal);
            _fluidMeshRenderer.material = _fluidMaterial;

            RebuildAll();
        }

        private void OnValidate()
        {
            // Inspector 슬라이더 조작 시 즉시 반영 (Play 모드)
            if (Application.isPlaying) RebuildAll();
        }

        // ── 공개 API (PipeViewController에서 호출) ────────────────────────

        /// <summary>파이프 직경을 미터 단위로 설정하고 메시를 재생성한다.</summary>
        public void SetDiameter(float m)
        {
            diameterM = Mathf.Max(0.05f, m);
            RebuildAll();
        }

        /// <summary>파이프 길이를 미터 단위로 설정하고 메시를 재생성한다.</summary>
        public void SetLength(float m)
        {
            lengthM = Mathf.Max(0.1f, m);
            RebuildAll();
        }

        /// <summary>충만율(0~1)에 따라 내부 유체 실린더 크기를 갱신한다.</summary>
        public void UpdateFluidVolume(float fillRatio)
        {
            _fillRatio = Mathf.Clamp01(fillRatio);
            RebuildFluid();
        }

        /// <summary>시뮬레이션 상태("NORMAL"/"WARNING"/"DANGER")에 따라 파이프 색상을 갱신한다.</summary>
        public void SetStatus(string status)
        {
            _status = status ?? "NORMAL";
            if (_pipeMaterial  != null) _pipeMaterial.color  = PipeColor(_status);
            if (_fluidMaterial != null) _fluidMaterial.color = FluidColor(_status);
        }

        // ── 메시 재생성 ───────────────────────────────────────────────────

        private void RebuildAll()
        {
            if (_pipeMeshFilter == null) return;
            _pipeMeshFilter.mesh = BuildHollowCylinder();
            RebuildFluid();
        }

        private void RebuildFluid()
        {
            if (_fluidMeshFilter == null) return;
            _fluidMeshFilter.mesh = BuildFluidCylinder();
        }

        // ── 속 빈 원통 (외벽) ────────────────────────────────────────────

        /// <summary>
        /// 외벽 파이프 메시: outer 실린더 측면 + inner 실린더 측면 + 앞/뒤 링 캡
        /// </summary>
        private Mesh BuildHollowCylinder()
        {
            float outerR = diameterM * 0.5f;
            float innerR = outerR * (1f - wallThicknessFactor);
            int   seg    = segments;

            // vertex 수: 외벽 측면 (seg+1)*2, 내벽 측면 (seg+1)*2, 앞 링 캡 seg*4, 뒤 링 캡 seg*4
            int sideVerts = (seg + 1) * 2;
            int capVerts  = seg * 4;
            int total     = sideVerts * 2 + capVerts * 2;

            var verts = new Vector3[total];
            var norms = new Vector3[total];
            var uvArr = new Vector2[total];
            var tris  = new System.Collections.Generic.List<int>(seg * 4 * 6);

            int ptr = 0;

            // 1. 외벽 측면 링 (z=0, z=lengthM)
            int outerFront = ptr;
            ptr = FillRing(verts, norms, uvArr, ptr, outerR,  0f,      +1f, seg);
            int outerBack  = ptr;
            ptr = FillRing(verts, norms, uvArr, ptr, outerR, lengthM,  +1f, seg);

            // 2. 내벽 측면 링
            int innerFront = ptr;
            ptr = FillRing(verts, norms, uvArr, ptr, innerR,  0f,      -1f, seg);
            int innerBack  = ptr;
            ptr = FillRing(verts, norms, uvArr, ptr, innerR, lengthM,  -1f, seg);

            // 외벽 측면 quad-strip
            StripQuads(tris, outerFront, outerBack, seg, false);
            // 내벽 측면 quad-strip (winding 반전 — 법선 내부 방향)
            StripQuads(tris, innerBack, innerFront, seg, false);

            // 3. 앞 링 캡 (z=0, 법선 −Z)
            ptr = RingCap(verts, norms, uvArr, tris, ptr, outerR, innerR, 0f,      Vector3.back,    seg);
            // 4. 뒤 링 캡 (z=lengthM, 법선 +Z)
                 RingCap(verts, norms, uvArr, tris, ptr, outerR, innerR, lengthM, Vector3.forward, seg);

            var mesh = new Mesh { name = "PipeMesh" };
            mesh.vertices  = verts;
            mesh.normals   = norms;
            mesh.uv        = uvArr;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── 유체 내부 실린더 ─────────────────────────────────────────────

        /// <summary>
        /// 충만율(fill_ratio)에 따라 내부 유체를 실린더 메시로 표현.
        /// fill_ratio=1.0 → 내벽 꽉 참, 0 → 없음.
        /// 원형관 단면적 근사: r_fluid = r_inner × sqrt(fill_ratio)
        /// </summary>
        private Mesh BuildFluidCylinder()
        {
            float innerR  = (diameterM * 0.5f) * (1f - wallThicknessFactor) * 0.97f;
            float fluidR  = innerR * Mathf.Sqrt(Mathf.Clamp01(_fillRatio));
            int   seg     = segments;

            if (fluidR < 0.001f)
            {
                // 유량 없음 → 빈 메시
                return new Mesh { name = "FluidMesh" };
            }

            int sideVerts = (seg + 1) * 2;
            var verts = new Vector3[sideVerts];
            var norms = new Vector3[sideVerts];
            var uvArr = new Vector2[sideVerts];
            var tris  = new System.Collections.Generic.List<int>(seg * 6);

            int front = 0;
            FillRing(verts, norms, uvArr, 0,      fluidR, 0f,      1f, seg);
            int back  = seg + 1;
            FillRing(verts, norms, uvArr, seg + 1, fluidR, lengthM, 1f, seg);

            StripQuads(tris, front, back, seg, false);

            var mesh = new Mesh { name = "FluidMesh" };
            mesh.vertices  = verts;
            mesh.normals   = norms;
            mesh.uv        = uvArr;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── 메시 헬퍼 ─────────────────────────────────────────────────────

        /// <summary>링 한 줄 정점 채우기. normalDir: +1=외향, -1=내향</summary>
        private static int FillRing(
            Vector3[] verts, Vector3[] norms, Vector2[] uvs,
            int offset, float radius, float z, float normalDir, int seg)
        {
            for (int i = 0; i <= seg; i++)
            {
                float t = (float)i / seg;
                float a = Mathf.PI * 2f * t;
                float x = Mathf.Cos(a) * radius;
                float y = Mathf.Sin(a) * radius;
                verts[offset + i] = new Vector3(x, y, z);
                norms[offset + i] = new Vector3(x, y, 0f).normalized * normalDir;
                uvs  [offset + i] = new Vector2(t, z > 0 ? 1f : 0f);
            }
            return offset + seg + 1;
        }

        /// <summary>두 링 사이를 quad-strip으로 채운다.</summary>
        private static void StripQuads(
            System.Collections.Generic.List<int> tris,
            int ringA, int ringB, int seg, bool flip)
        {
            for (int i = 0; i < seg; i++)
            {
                int a0 = ringA + i, a1 = ringA + i + 1;
                int b0 = ringB + i, b1 = ringB + i + 1;
                if (flip)
                {
                    tris.Add(a0); tris.Add(b1); tris.Add(b0);
                    tris.Add(a0); tris.Add(a1); tris.Add(b1);
                }
                else
                {
                    tris.Add(a0); tris.Add(b0); tris.Add(b1);
                    tris.Add(a0); tris.Add(b1); tris.Add(a1);
                }
            }
        }

        /// <summary>링 캡(앞/뒤면) 메시를 ptr 위치에 쓰고 갱신된 ptr 반환.</summary>
        private static int RingCap(
            Vector3[] verts, Vector3[] norms, Vector2[] uvs,
            System.Collections.Generic.List<int> tris,
            int ptr,
            float outerR, float innerR, float z, Vector3 normal, int seg)
        {
            bool faceForward = (normal.z >= 0);
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * 2f * i / seg;
                float a1 = Mathf.PI * 2f * (i + 1) / seg;

                Vector3 o0 = new Vector3(Mathf.Cos(a0) * outerR, Mathf.Sin(a0) * outerR, z);
                Vector3 o1 = new Vector3(Mathf.Cos(a1) * outerR, Mathf.Sin(a1) * outerR, z);
                Vector3 i0 = new Vector3(Mathf.Cos(a0) * innerR, Mathf.Sin(a0) * innerR, z);
                Vector3 i1 = new Vector3(Mathf.Cos(a1) * innerR, Mathf.Sin(a1) * innerR, z);

                int b = ptr;
                verts[ptr] = o0; norms[ptr] = normal;
                uvs[ptr] = new Vector2(Mathf.Cos(a0) * 0.5f + 0.5f, Mathf.Sin(a0) * 0.5f + 0.5f); ptr++;

                verts[ptr] = o1; norms[ptr] = normal;
                uvs[ptr] = new Vector2(Mathf.Cos(a1) * 0.5f + 0.5f, Mathf.Sin(a1) * 0.5f + 0.5f); ptr++;

                verts[ptr] = i0; norms[ptr] = normal;
                uvs[ptr] = new Vector2(Mathf.Cos(a0) * innerR / outerR * 0.5f + 0.5f,
                                       Mathf.Sin(a0) * innerR / outerR * 0.5f + 0.5f); ptr++;

                verts[ptr] = i1; norms[ptr] = normal;
                uvs[ptr] = new Vector2(Mathf.Cos(a1) * innerR / outerR * 0.5f + 0.5f,
                                       Mathf.Sin(a1) * innerR / outerR * 0.5f + 0.5f); ptr++;

                // o0=b, o1=b+1, i0=b+2, i1=b+3
                if (faceForward)
                {
                    tris.Add(b+0); tris.Add(b+1); tris.Add(b+3);
                    tris.Add(b+0); tris.Add(b+3); tris.Add(b+2);
                }
                else
                {
                    tris.Add(b+0); tris.Add(b+3); tris.Add(b+1);
                    tris.Add(b+0); tris.Add(b+2); tris.Add(b+3);
                }
            }
            return ptr;
        }

        // ── 색상 헬퍼 ─────────────────────────────────────────────────────

        private Color PipeColor(string status) => status switch
        {
            "WARNING" => colorWarning,
            "DANGER"  => colorDanger,
            "NORMAL"  => colorNormal,
            _         => colorDefault
        };

        private Color FluidColor(string status) => status switch
        {
            "WARNING" => colorFluidWarning,
            "DANGER"  => colorFluidDanger,
            _         => colorFluidNormal
        };

        // ── 머티리얼 생성 ─────────────────────────────────────────────────

        private static Material CreateMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
            if (color.a < 1f)
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            mat.color = color;
            return mat;
        }
    }
}
