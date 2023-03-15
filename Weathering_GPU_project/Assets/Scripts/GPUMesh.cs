using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;

namespace Weathering_GPU
{
    public class GPUMesh : MonoBehaviour
    {
        private MeshFilter filter;
        private MeshRenderer renderer;
        [SerializeField] private Material mainMat;
        [SerializeField] private Material subMat;
        [SerializeField] private Material baseMat;
        private Stopwatch sw = new Stopwatch();
        private int vertNum;
        private int edgeNum;
        private int faceNum;
        private int conLNum;
        private int conBNum;
        private int vertGS;
        private int edgeGS;
        private int faceGS;
        private int conLGS;
        private int conBGS;
        
        private GameObject baseObject;
        private MeshFilter baseFilter;
        private MeshRenderer baseRenderer;

        //private int count = 0;
        struct LengthConstraintData
        {
            public int P0; // 端点1
            public int P1; // 端点2
            public float Offset;
            public float Stiff;
            public float Thr;
        };
        struct BendConstraintData
        {
            public int P0; // 端点1
            public int P1; // 端点2
            public int P2; // 端点3
            public int P3; // 端点4
            public float Offset;
            public float Stiff;
            public float Thr;
        };
        struct VertData
        {
            public Vector3 Pos; // 位置
            public Vector3 Nor; // 法線
            public Vector3 Pos0; // 初期位置
            public Vector3 Nor0; // 初期法線
            public Vector3 Vel; // (疑似) 速度
            public Vector3 Ass; // 推定位置
            public Vector3 Frc; // 力
            public Vector3 Del; // 変位
            public Vector2 uv;
            public Color Col;
            public float Cur; // 曲率

            public float Rust;
            public float Rust_Buf;
            public float Dust;
            public float Chalk;
            public float Bond;
            public float LiftingForce;     // 可視化用
            public uint Status;
            public Vector3 Direction;  // ひび割れの方向 (status == 1 のときに参照)
            public int OnEdge;
            public int Lowest;
            public float Thickness;
           
            public int Edge;
            public float Weight;
        };
        struct EdgeData
        {
            public int Next;
            public int Prev;
            public int Pair;
            public int Face;
            public int Vert;
            public uint Stat;

            public float Angle;
            public float LiftingForce;
            public float BindingForce;
        };
        struct FaceData
        {
            public int Edge;
            public uint Stat;

            public float Contraction;
        };
        struct VertexData
        {
            public Vector3 Pos; // 位置
            public Vector3 Nor; // 法線
            public Vector2 UV;
            public Vector4 Col;
        };

        VertexAttributeDescriptor[] layout = new[]
        {
            // Position, Normal, Tangent, TexCoord ... の順番を守らないとバグる．しかもColorを使うとバグるのでTexCoordを使う必要があるとのこと
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            //new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4), Colorは使えない
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
        };

        int InitVert;
        int InitFace;
        int InitEdge;
        int SetBase;
        int SetPair;
        int ComputeVertCurvature;
        int ComputeEdgeCurvature;
        int PBD_Itteration_L;
        int PBD_Itteration_B;
        int AccumulateDust;
        int SetRustBuffer;
        int PropagateRust;

        int TestPBD;
        int PBD_UpdateConstraint;
        int PBD_StartFrame;
        int PBD_Update_Pos;
        int PBD_Update_Ass;
        int SearchVertices;
        int SetTriangles;
        int SetVertices;

        int SetVertColor_Normal;
        int SetVertColor_Separate;
        int SetVertColor_Tear;

        int SetVertForces;
        int UpdateLiftingForce;
        int UpdateVertForces;
        int UpdateBindingForce;

        int SeparateVertex;
        int TearEdge;

        int count = 0;
        const int SIMULATION_BLOCK_SIZE = 256;
        public ComputeShader CS;
        GraphicsBuffer _positionBuffer;      // 初期化用
        GraphicsBuffer _normalBuffer;
        GraphicsBuffer _basePositionBuffer;      // 基板用
        GraphicsBuffer _baseNormalBuffer;
        GraphicsBuffer _uvBuffer;
        GraphicsBuffer _lengthConstraintBuffer;      // PBD用
        GraphicsBuffer _bendConstraintBuffer;
        GraphicsBuffer _deltaPosAsInt;
        GraphicsBuffer _deltaCount;
        GraphicsBuffer _liftingForceAsInt;
        GraphicsBuffer _liftingForceCount;

        GraphicsBuffer _vertBuffer;      // ハーフエッジ構造用
        GraphicsBuffer _edgeBuffer;
        GraphicsBuffer _faceBuffer;
        GraphicsBuffer _vertexBuffer;    // 描画用
        GraphicsBuffer _triangleBuffer;

        GraphicsBuffer _lockProcessBuffer;     // 1フレームで1度しか認めない処理
        GraphicsBuffer _vertCounterBuffer;           // GPU側での変更を許す頂点の数

        void InitNums() {
            //vertNum = filter.mesh.triangles.Length; 
            vertNum = filter.mesh.vertices.Length;
            edgeNum = filter.mesh.triangles.Length;
            faceNum = filter.mesh.triangles.Length / 3;
            conLNum = filter.mesh.triangles.Length;
            conBNum = filter.mesh.triangles.Length;
        }
        void UpdateGroupSize()
        {
            vertGS = Mathf.CeilToInt((float)vertNum / SIMULATION_BLOCK_SIZE);
            edgeGS = Mathf.CeilToInt((float)edgeNum / SIMULATION_BLOCK_SIZE);
            faceGS = Mathf.CeilToInt((float)faceNum / SIMULATION_BLOCK_SIZE);
            conLGS = Mathf.CeilToInt((float)conLNum / SIMULATION_BLOCK_SIZE);
            conBGS = Mathf.CeilToInt((float)conBNum / SIMULATION_BLOCK_SIZE);
        }
        void InitIndex() {
            InitVert = CS.FindKernel("InitVert");
            InitFace = CS.FindKernel("InitFace");
            InitEdge = CS.FindKernel("InitEdge");
            SetBase = CS.FindKernel("SetBase");
            SetPair = CS.FindKernel("SetPair");
            ComputeVertCurvature = CS.FindKernel("ComputeVertCurvature");
            ComputeEdgeCurvature = CS.FindKernel("ComputeEdgeCurvature");
            AccumulateDust = CS.FindKernel("AccumulateDust");
            SetRustBuffer = CS.FindKernel("SetRustBuffer");
            PropagateRust = CS.FindKernel("PropagateRust");
            PBD_UpdateConstraint = CS.FindKernel("PBD_UpdateConstraint");
            PBD_Itteration_L = CS.FindKernel("PBD_Itteration_L");
            PBD_Itteration_B = CS.FindKernel("PBD_Itteration_B");
            TestPBD = CS.FindKernel("TestPBD");
            PBD_StartFrame = CS.FindKernel("PBD_StartFrame");
            PBD_Update_Pos = CS.FindKernel("PBD_Update_Pos");
            PBD_Update_Ass = CS.FindKernel("PBD_Update_Ass");
            SearchVertices = CS.FindKernel("SearchVertices");
            SetTriangles = CS.FindKernel("SetTriangles");
            SetVertices = CS.FindKernel("SetVertices");
            SetVertForces = CS.FindKernel("SetVertForces");
            SetVertColor_Normal = CS.FindKernel("SetVertColor_Normal");
            SetVertColor_Separate = CS.FindKernel("SetVertColor_Separate");
            SetVertColor_Tear = CS.FindKernel("SetVertColor_Tear");            
            UpdateLiftingForce = CS.FindKernel("UpdateLiftingForce");
            UpdateVertForces = CS.FindKernel("UpdateVertForces");
            UpdateBindingForce = CS.FindKernel("UpdateBindingForce");
            SeparateVertex = CS.FindKernel("SeparateVertex");
            TearEdge = CS.FindKernel("TearEdge");
        }
        void InitBuffer()
        {
            CS.SetInt("_VertNum", vertNum);
            CS.SetInt("_EdgeNum", edgeNum);
            CS.SetInt("_FaceNum", faceNum);
            CS.SetInt("_LConstNum", conLNum);
            CS.SetInt("_BConstNum", conBNum);

            sw.Start();
            // バッファを初期化
            // 頂点の数は最大で三角形の3倍＝edgeNumなのでその分のバッファだけ確保しておく
            // 頂点 (ハーフエッジ構造)
            _vertBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, edgeNum, Marshal.SizeOf(typeof(VertData)));
            //UnityEngine.Debug.Log(filter.mesh.GetVertexBuffer(0).target);
            //_vertBuffer = filter.mesh.GetVertexBuffer(0);
            _positionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, edgeNum, Marshal.SizeOf(typeof(Vector3)));
            _positionBuffer.SetData(filter.mesh.vertices);
            _normalBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, edgeNum, Marshal.SizeOf(typeof(Vector3)));
            _normalBuffer.SetData(filter.mesh.normals);
            _uvBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, edgeNum, Marshal.SizeOf(typeof(Vector2)));
            _uvBuffer.SetData(filter.mesh.uv);

            _basePositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertNum, Marshal.SizeOf(typeof(Vector3)));
            _baseNormalBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertNum, Marshal.SizeOf(typeof(Vector3)));
            
            CS.SetBuffer(InitVert, "_PositionBuffer", _positionBuffer);
            CS.SetBuffer(InitVert, "_NormalBuffer", _normalBuffer);
            CS.SetBuffer(InitVert, "_UVBuffer", _uvBuffer);
            CS.SetBuffer(InitVert, "_VertBufferRW", _vertBuffer);
            CS.Dispatch(InitVert, vertGS, 1, 1); // ComputeShaderを実行

            UnityEngine.Debug.Log(sw.Elapsed);
            // 頂点 (Mesh用)
            _vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, edgeNum, Marshal.SizeOf(typeof(VertexData)));
            
            // 半稜線・面 (ハーフエッジ構造)
            _edgeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, edgeNum, Marshal.SizeOf(typeof(EdgeData)));
            _faceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, faceNum, Marshal.SizeOf(typeof(FaceData)));

            CS.SetBuffer(InitFace, "_FaceBufferRW", _faceBuffer);
            CS.Dispatch(InitFace, faceGS, 1, 1);

            CS.SetBuffer(InitEdge, "_EdgeBufferRW", _edgeBuffer);
            _triangleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, faceNum * 3, Marshal.SizeOf(typeof(int)));
            _triangleBuffer.SetData(filter.mesh.triangles);
            CS.SetBuffer(InitEdge, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(InitEdge, "_TriangleBufferRW", _triangleBuffer);
            CS.Dispatch(InitEdge, faceGS, 1, 1);

            // 頂点データ更新用intバッファ
            PBD_Itteration_L = CS.FindKernel("PBD_Itteration_L");
            _deltaPosAsInt = new GraphicsBuffer(GraphicsBuffer.Target.Structured, edgeNum, Marshal.SizeOf(typeof(Vector3Int)));
            _deltaCount = new GraphicsBuffer(GraphicsBuffer.Target.Structured, edgeNum, Marshal.SizeOf(typeof(int)));
            
            _liftingForceAsInt = new GraphicsBuffer(GraphicsBuffer.Target.Structured, edgeNum, Marshal.SizeOf(typeof(int)));
            _liftingForceCount = new GraphicsBuffer(GraphicsBuffer.Target.Structured, edgeNum, Marshal.SizeOf(typeof(int)));

            _lengthConstraintBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, conLNum, Marshal.SizeOf(typeof(LengthConstraintData)));
            _bendConstraintBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, conBNum, Marshal.SizeOf(typeof(BendConstraintData)));

            _lockProcessBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, edgeNum, Marshal.SizeOf(typeof(uint)));
            _vertCounterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, Marshal.SizeOf(typeof(uint)));
            _vertCounterBuffer.SetData(new int[] { vertNum });
            // バッファのセット
            // InitEdge


            // SetTriangles
            CS.SetBuffer(SetTriangles, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(SetTriangles, "_EdgeBufferRW", _edgeBuffer);
            CS.SetBuffer(SetTriangles, "_FaceBufferRW", _faceBuffer);
            CS.SetBuffer(SetTriangles, "_TriangleBufferRW", _triangleBuffer);

            // SetBase
            CS.SetBuffer(SetBase, "_BasePositionBuffer", _basePositionBuffer);
            CS.SetBuffer(SetBase, "_BaseNormalBuffer", _baseNormalBuffer);

            // SetPair
            CS.SetBuffer(SetPair, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(SetPair, "_EdgeBufferRW", _edgeBuffer);

            // SearchVertices
            CS.SetBuffer(SearchVertices, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(SearchVertices, "_EdgeBufferRW", _edgeBuffer);
            CS.SetBuffer(SearchVertices, "_FaceBufferRW", _faceBuffer);
            CS.SetBuffer(SearchVertices, "_TriangleBufferRW", _triangleBuffer);
            CS.SetBuffer(SearchVertices, "_DeltaPosAsInt", _deltaPosAsInt);
            CS.SetBuffer(SearchVertices, "_DeltaCount", _deltaCount);

            // SetVertColor_
            CS.SetBuffer(SetVertColor_Normal, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(SetVertColor_Separate, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(SetVertColor_Tear, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(SetVertColor_Tear, "_EdgeBufferRW", _edgeBuffer);
            CS.SetBuffer(SetVertColor_Tear, "_FaceBufferRW", _faceBuffer);

            // ComputeVertCurvature
            CS.SetBuffer(ComputeVertCurvature, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(ComputeVertCurvature, "_EdgeBufferRW", _edgeBuffer);

            // ComputeEdgeCurvature
            CS.SetBuffer(ComputeEdgeCurvature, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(ComputeEdgeCurvature, "_EdgeBufferRW", _edgeBuffer);

            // PBD_Update_Constraint
            CS.SetBuffer(PBD_StartFrame, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(PBD_UpdateConstraint, "_VertBufferRW", _vertBuffer);

            // PBD_StartFrame
            CS.SetBuffer(PBD_StartFrame, "_DeltaCount", _deltaCount);
            CS.SetBuffer(PBD_StartFrame, "_DeltaPosAsInt", _deltaPosAsInt);

            // PBD_Ittereation_L
            CS.SetBuffer(PBD_Itteration_L, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(PBD_Itteration_L, "_LengthConstraintBufferRW", _lengthConstraintBuffer);
            CS.SetBuffer(PBD_Itteration_L, "_DeltaCount", _deltaCount);
            CS.SetBuffer(PBD_Itteration_L, "_DeltaPosAsInt", _deltaPosAsInt);

            // PBD_Ittereation_B
            CS.SetBuffer(PBD_Itteration_B, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(PBD_Itteration_B, "_BendConstraintBufferRW", _bendConstraintBuffer);
            CS.SetBuffer(PBD_Itteration_B, "_DeltaCount", _deltaCount);
            CS.SetBuffer(PBD_Itteration_B, "_DeltaPosAsInt", _deltaPosAsInt);

            // PBD_SetConstraint
            CS.SetBuffer(PBD_UpdateConstraint, "_LengthConstraintBufferRW", _lengthConstraintBuffer);

            // PBD_Update_Ass
            CS.SetBuffer(PBD_Update_Ass, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(PBD_Update_Ass, "_DeltaCount", _deltaCount);
            CS.SetBuffer(PBD_Update_Ass, "_DeltaPosAsInt", _deltaPosAsInt);

            // PBD_Update_Pos
            CS.SetBuffer(PBD_Update_Pos, "_VertBufferRW", _vertBuffer);

            // AccumulateDust
            CS.SetBuffer(AccumulateDust, "_VertBufferRW", _vertBuffer);

            // SetRustBuffer
            CS.SetBuffer(SetRustBuffer, "_VertBufferRW", _vertBuffer);

            // PropagateRust
            CS.SetBuffer(PropagateRust, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(PropagateRust, "_EdgeBufferRW", _edgeBuffer);

            // SetVertForces
            CS.SetBuffer(SetVertForces, "_VertBufferRW", _vertBuffer); 
            CS.SetBuffer(SetVertForces, "_LiftingForceCount", _liftingForceCount);
            CS.SetBuffer(SetVertForces, "_LiftingForceAsInt", _liftingForceAsInt);

            // UpdateLiftingForce
            CS.SetBuffer(UpdateLiftingForce, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(UpdateLiftingForce, "_EdgeBufferRW", _edgeBuffer);
            CS.SetBuffer(UpdateLiftingForce, "_FaceBufferRW", _faceBuffer);
            CS.SetBuffer(UpdateLiftingForce, "_LiftingForceAsInt", _liftingForceAsInt);
            CS.SetBuffer(UpdateLiftingForce, "_LiftingForceCount", _liftingForceCount);
            CS.SetBuffer(UpdateLiftingForce, "_LiftingForceAsInt", _liftingForceAsInt);
            CS.SetBuffer(UpdateLiftingForce, "_LiftingForceCount", _liftingForceCount);
            CS.SetBuffer(UpdateLiftingForce, "_BendConstraintBufferRW", _bendConstraintBuffer);

            // UpdateVertFoces
            CS.SetBuffer(UpdateVertForces, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(UpdateVertForces, "_EdgeBufferRW", _edgeBuffer);
            CS.SetBuffer(UpdateVertForces, "_LiftingForceAsInt", _liftingForceAsInt);
            CS.SetBuffer(UpdateVertForces, "_LiftingForceCount", _liftingForceCount);

            // UpdateBindingForce
            CS.SetBuffer(UpdateBindingForce, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(UpdateBindingForce, "_EdgeBufferRW", _edgeBuffer);
            CS.SetBuffer(UpdateBindingForce, "_FaceBufferRW", _faceBuffer);

            // SetVertices
            CS.SetBuffer(SetVertices, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(SetVertices, "_VertexBufferRW", _vertexBuffer);

            // SeparateVertex
            CS.SetBuffer(SeparateVertex, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(SeparateVertex, "_EdgeBufferRW", _edgeBuffer);
            CS.SetBuffer(SeparateVertex, "_FaceBufferRW", _faceBuffer);
            CS.SetBuffer(SeparateVertex, "_LengthConstraintBufferRW", _lengthConstraintBuffer);
            CS.SetBuffer(SeparateVertex, "_BendConstraintBufferRW", _bendConstraintBuffer);

            // TearEdge
            CS.SetBuffer(TearEdge, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(TearEdge, "_EdgeBufferRW", _edgeBuffer);
            CS.SetBuffer(TearEdge, "_FaceBufferRW", _faceBuffer);
            CS.SetBuffer(TearEdge, "_LengthConstraintBufferRW", _lengthConstraintBuffer);
            CS.SetBuffer(TearEdge, "_BendConstraintBufferRW", _bendConstraintBuffer);
            CS.SetBuffer(TearEdge, "_LockProcess", _lockProcessBuffer);
            CS.SetBuffer(TearEdge, "_VertCounter", _vertCounterBuffer);

        }
        void PBD_Simulation() {
            CS.SetInt("_VertNum", vertNum);
            CS.SetInt("_EdgeNum", edgeNum);
            CS.SetInt("_FaceNum", faceNum);
            CS.SetInt("_LConstNum", conLNum);
            CS.SetInt("_BConstNum", conBNum);

            // 頂点情報更新
            CS.SetFloat("_delta_time", Time.deltaTime * 0 + 0.2f);
            CS.Dispatch(PBD_StartFrame, vertGS, 1, 1);

            // イテレーション

            for (int i = 0; i < 32; i++) {
                CS.SetInt("_itr_count", i);

                CS.Dispatch(PBD_Itteration_L, conLGS, 1, 1);
                CS.Dispatch(PBD_Itteration_B, conBGS, 1, 1);

                CS.Dispatch(PBD_Update_Ass, vertGS, 1, 1);
            }

            // 頂点情報更新
            CS.Dispatch(PBD_Update_Pos, vertGS, 1, 1);
        }
        void SetPairEdge() { 
            // 頂点単位の探索
            CS.SetInt("_EdgeNum", edgeNum);
            //CS.SetBuffer(SetPair, "_TriangleBufferRW", _triangleBuffer);
            CS.Dispatch(SetPair, edgeGS, 1, 1); // ComputeShaderを実行
        }
        void SetConstraint() {
            // テスト用PBD制約 (稜線の長さを0.1に)
            
            CS.SetBuffer(TestPBD, "_EdgeBufferRW", _edgeBuffer);
            CS.SetBuffer(TestPBD, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(TestPBD, "_LengthConstraintBufferRW", _lengthConstraintBuffer);
            CS.SetBuffer(TestPBD, "_BendConstraintBufferRW", _bendConstraintBuffer);
            CS.Dispatch(TestPBD, conLGS, 1, 1);

            var constArr = new LengthConstraintData[conLNum];
            _lengthConstraintBuffer.GetData(constArr);
        }
        private void InteractiveControl() {
            var drawpoint = Vector4.zero;
            if ((Input.GetMouseButton(0) || Input.GetMouseButton(1)) && !CamToggle._cam) {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    drawpoint = hit.point;
                    drawpoint.w = DrawingRangeSlider.DrawingRange;
                }
            }
            CS.SetVector("_hit_point", drawpoint);
            if (CamToggle._cam)
            {
                CS.SetInt("_control_separation", 0);
                CS.SetInt("_control_tear", 0);
            }
            else {
                if (Input.GetMouseButton(0))
                {
                    if (WeaToggle._wea)
                        CS.SetInt("_control_separation", 1);
                    else
                        CS.SetInt("_control_separation", 2);
                }
                if (Input.GetMouseButton(1))
                {
                    if (WeaToggle._wea)
                        CS.SetInt("_control_tear", 1);
                    else
                        CS.SetInt("_control_tear", 2);

                }
                if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
                {
                    CS.SetInt("_control_separation", 0);
                    CS.SetInt("_control_tear", 0);
                }
            }
            

            switch (DropdownControl.vis_mode) {
                case 0:
                    renderer.material = mainMat;
                    CS.Dispatch(SetVertColor_Normal, vertGS, 1, 1);
                    break;
                case 1:
                    renderer.material = subMat;
                    CS.Dispatch(SetVertColor_Separate, vertGS, 1, 1);
                    break;
                default:
                    renderer.material = subMat;
                    CS.Dispatch(SetVertColor_Tear, vertGS, 1, 1);
                    break;
            } 
        }
        private void UpdateVertNum()
        {
            var countArr = new uint[1];
            _vertCounterBuffer.GetData(countArr);
            vertNum = (int) countArr[0];
            vertGS = Mathf.CeilToInt((float)vertNum / SIMULATION_BLOCK_SIZE);
            CS.SetInt("_VertNum", vertNum);
        }
        void ReleaseBuffer() {
            if (_positionBuffer != null)
            {
                _positionBuffer.Release();
                _positionBuffer = null;
            }
            if (_normalBuffer != null)
            {
                _normalBuffer.Release();
                _normalBuffer = null;
            }
            if (_uvBuffer != null)
            {
                _uvBuffer.Release();
                _uvBuffer = null;
            }
            if (_edgeBuffer != null)
            {
                _edgeBuffer.Release();
                _edgeBuffer = null;
            }
            if (_vertBuffer != null)
            {
                _vertBuffer.Release();
                _vertBuffer = null;
            }
            if (_edgeBuffer != null)
            {
                _edgeBuffer.Release();
                _edgeBuffer = null;
            }
            if (_faceBuffer != null)
            {
                _faceBuffer.Release();
                _faceBuffer = null;
            }
            if (_vertexBuffer != null)
            {
                _vertexBuffer.Release();
                _vertexBuffer = null;
            }
            if (_triangleBuffer != null)
            {
                _triangleBuffer.Release();
                _triangleBuffer = null;
            }
            if (_lengthConstraintBuffer != null)
            {
                _lengthConstraintBuffer.Release();
                _lengthConstraintBuffer = null;
            }
            if (_bendConstraintBuffer != null)
            {
                _bendConstraintBuffer.Release();
                _bendConstraintBuffer = null;
            }
            if (_deltaPosAsInt != null)
            {
                _deltaPosAsInt.Release();
                _deltaPosAsInt = null;
            }
            if (_deltaCount != null)
            {
                _deltaCount.Release();
                _deltaCount = null;
            }
            if (_liftingForceAsInt != null)
            {
                _liftingForceAsInt.Release();
                _liftingForceAsInt = null;
            }
            if (_liftingForceCount != null)
            {
                _liftingForceCount.Release();
                _liftingForceCount = null;
            }
            if (_lockProcessBuffer != null)
            {
                _lockProcessBuffer.Release();
                _lockProcessBuffer = null;
            }
            if (_vertCounterBuffer != null)
            {
                _vertCounterBuffer.Release();
                _vertCounterBuffer = null;
            }
        }
        void OnDestroy()
        {
            // バッファを破棄
            ReleaseBuffer();
        }
        void SetBaseMesh() {
            UnityEngine.Debug.Log("SetBaseMesh");
            baseObject = Instantiate(this.gameObject);
            Destroy(baseObject.GetComponent<Weathering_GPU.GPUMesh>());
            baseObject.name = "base";
            baseFilter = baseObject.GetComponent<MeshFilter>();
            baseRenderer = baseObject.GetComponent<MeshRenderer>();
            baseRenderer.material = baseMat;

            UnityEngine.Debug.Log(_basePositionBuffer);
            _basePositionBuffer.SetData(filter.mesh.vertices);
            _baseNormalBuffer.SetData(filter.mesh.normals);

            CS.Dispatch(SetBase, vertGS, 1, 1);
            var vertices = new Vector3[baseFilter.mesh.vertexCount];
            _basePositionBuffer.GetData(vertices);
            baseFilter.mesh.vertices = vertices;
        }
        // Start is called before the first frame update
        void Start()
        {
            if (TryGetComponent<MeshFilter>(out var mf))
            {
                var triangles = new int[mf.mesh.triangles.Length];
                System.Array.Copy(mf.mesh.triangles, triangles, mf.mesh.triangles.Length);
                mf.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mf.mesh.triangles = triangles;
                filter = mf;
            }
            else
            {
                filter = this.gameObject.AddComponent<MeshFilter>();
            }
            if (TryGetComponent<MeshRenderer>(out var mr))
                renderer = mr;
            else
                renderer = this.gameObject.AddComponent<MeshRenderer>();
            

            InitNums();
            InitIndex();
            UpdateGroupSize();
            InitBuffer();
            //sw.Start();
            SetPairEdge();
            CS.Dispatch(ComputeEdgeCurvature, edgeGS, 1, 1);
            SetConstraint();
            SetBaseMesh();

            CS.SetVector("_gravity", new Vector4(0, -1, 0, 0));
        }

        // Update is called once per frame
        void Update()
        {
            // GPUで頂点が追加された場合、vertNum以上のIDに対して参照されうるのおでedgeNumで固定する
            _lockProcessBuffer.SetData(new uint[edgeNum]);

            CS.Dispatch(ComputeVertCurvature, vertGS, 1, 1);
            CS.Dispatch(ComputeEdgeCurvature, edgeGS, 1, 1);
            CS.Dispatch(AccumulateDust, vertGS, 1, 1);
            CS.Dispatch(SetRustBuffer, vertGS, 1, 1);
            CS.Dispatch(PropagateRust, vertGS, 1, 1);

            //CS.SetFloat("length_ratio", 1.25f - 0.25f * Mathf.Cos(Time.time * 0.5f));
            //CS.Dispatch(PBD_UpdateConstraint, filter.mesh.triangles.Length, 1, 1); // ComputeShaderを実行

            PBD_Simulation();

            // 揚力計算 (事前にComputeEdgeCurvatureを実行すること)
            CS.Dispatch(SetVertForces, vertGS, 1, 1);
            CS.Dispatch(UpdateLiftingForce, edgeGS, 1, 1);
            CS.Dispatch(UpdateVertForces, vertGS, 1, 1);
            CS.Dispatch(UpdateBindingForce, faceGS, 1, 1);
            CS.Dispatch(SeparateVertex, vertGS, 1, 1);
            CS.Dispatch(TearEdge, edgeGS, 1, 1);

            // 頂点単位の探索
            //CS.Dispatch(SearchVertices, vertGS, 1, 1); // ComputeShaderを実行

            // GPU側で変更された頂点数の反映
            UpdateVertNum();

            var vertArr = new VertData[vertNum];
            _vertBuffer.GetData(vertArr);

            InteractiveControl();
            // 頂点情報
            CS.Dispatch(SetVertices, vertGS, 1, 1); // ComputeShaderを実行
            var vertexArr = new VertexData[vertNum];
            _vertexBuffer.GetData(vertexArr);
            //UnityEngine.Debug.Log(vertexArr[100].UV);
            //UnityEngine.Debug.Log(vertexArr[100].Col);

            filter.mesh.SetVertexBufferParams(vertNum, layout);
            filter.mesh.SetVertexBufferData(vertexArr, 0, 0, vertNum);

            /*
            var posArr = new Vector3[vertNum];
            var norArr = new Vector3[vertNum];
            var colArr = new Color[vertNum];
            var uvArr = new Vector2[vertNum];

            for (int i = 0; i < vertexArr.Length; i++) {
                posArr[i] = vertexArr[i].Pos;
                norArr[i] = vertexArr[i].Nor;
                colArr[i] = vertexArr[i].Col;
                uvArr[i] = vertexArr[i].UV;
            }
            */
            //UnityEngine.Debug.Log(posArr[100]);

            //filter.mesh.Set
            //filter.mesh.vertices = posArr;
            //filter.mesh.normals = norArr;
            //filter.mesh.colors = colArr;
            //filter.mesh.uv = uvArr;

            // 三角形インデックス
            CS.Dispatch(SetTriangles, faceGS, 1, 1); // ComputeShaderを実行
            var triangleArr = new int[faceNum * 3];
            _triangleBuffer.GetData(triangleArr);
            filter.mesh.SetIndexBufferData(triangleArr, 0, 0, faceNum * 3);

            //filter.mesh.triangles = triangleArr;

            filter.mesh.RecalculateNormals();
            filter.mesh.RecalculateBounds();

            var edgeArr = new EdgeData[filter.mesh.triangles.Length];
            _edgeBuffer.GetData(edgeArr);
            var constArr = new BendConstraintData[filter.mesh.triangles.Length];
            _bendConstraintBuffer.GetData(constArr);
            var lockArr = new uint[vertNum];
            _lockProcessBuffer.GetData(lockArr);
            //UnityEngine.Debug.Log(filter.mesh.normals[norArr.Length - 1]);

            //UnityEngine.Debug.Log(count++);
            //posArr = null;
            //norArr = null;
            //colArr = null;
            //uvArr = null;
            vertArr = null;
            edgeArr = null;
            constArr = null;
            triangleArr = null;
        }
    } // class
} // namespace
