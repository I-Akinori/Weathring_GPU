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
        private Stopwatch sw = new Stopwatch();
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
        };
        struct FaceData
        {
            public int Edge;
        };
        struct VertexData
        {
            public Vector3 Pos; // 速度
            public Vector3 Nor; // 法線
        };

        VertexAttributeDescriptor[] layout = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
        };

        int InitVert;
        int InitFace;
        int InitEdge;
        int PBD_Itteration_L;
        int PBD_Itteration_B;
        int TestPBD;
        int PBD_StartFrame;
        int PBD_Update_Pos;
        int PBD_Update_Ass;
        int SearchVertices;
        int SetTriangles;
        int SetVertices;

        const int SIMULATION_BLOCK_SIZE = 256;
        public ComputeShader CS;
        ComputeBuffer _positionBuffer;      // 初期化用
        ComputeBuffer _normalBuffer;
        ComputeBuffer _lengthConstraintBuffer;      // PBD用
        ComputeBuffer _bendConstraintBuffer;
        ComputeBuffer _deltaPosAsInt;
        ComputeBuffer _deltaCount;

        ComputeBuffer _vertBuffer;      // ハーフエッジ構造用
        ComputeBuffer _edgeBuffer;
        ComputeBuffer _faceBuffer;
        ComputeBuffer _vertexBuffer;    // 描画用
        ComputeBuffer _triangleBuffer;

        public ComputeBuffer GetVertBuffer()
        {
            return this._vertBuffer != null ? this._vertBuffer : null;
        }
        void InitIndex() {
            InitVert = CS.FindKernel("InitVert");
            InitFace = CS.FindKernel("InitFace");
            InitEdge = CS.FindKernel("InitEdge");
            PBD_Itteration_L = CS.FindKernel("PBD_Itteration_L");
            PBD_Itteration_B = CS.FindKernel("PBD_Itteration_B");
            TestPBD = CS.FindKernel("TestPBD");
            PBD_StartFrame = CS.FindKernel("PBD_StartFrame");
            PBD_Update_Pos = CS.FindKernel("PBD_Update_Pos");
            PBD_Update_Ass = CS.FindKernel("PBD_Update_Ass");
            SearchVertices = CS.FindKernel("SearchVertices");
            SetTriangles = CS.FindKernel("SetTriangles");
            SetVertices = CS.FindKernel("SetVertices");
        }
        void InitBuffer()
        {
            CS.SetInt("_VertNum", filter.mesh.vertices.Length);
            CS.SetInt("_EdgeNum", filter.mesh.triangles.Length);
            CS.SetInt("_FaceNum", filter.mesh.triangles.Length / 3);
            CS.SetInt("_LConstNum", filter.mesh.triangles.Length);
            CS.SetInt("_BConstNum", 0);

            sw.Start();
            // バッファを初期化
            // 頂点 (ハーフエッジ構造)
            int num = filter.mesh.vertexCount;
            _vertBuffer = new ComputeBuffer(num, Marshal.SizeOf(typeof(VertData)));
            _positionBuffer = new ComputeBuffer(num, Marshal.SizeOf(typeof(Vector3)));
            _positionBuffer.SetData(filter.mesh.vertices);
            _normalBuffer = new ComputeBuffer(num, Marshal.SizeOf(typeof(Vector3)));
            _normalBuffer.SetData(filter.mesh.normals);

            int threadGroupSize;
            // 頂点単位の探索
            
            threadGroupSize = Mathf.CeilToInt((float) num / SIMULATION_BLOCK_SIZE);
            CS.SetBuffer(InitVert, "_PositionBuffer", _positionBuffer);
            CS.SetBuffer(InitVert, "_NormalBuffer", _normalBuffer);
            CS.SetBuffer(InitVert, "_VertBufferRW", _vertBuffer);
            CS.Dispatch(InitVert, threadGroupSize, 1, 1); // ComputeShaderを実行

            UnityEngine.Debug.Log(sw.Elapsed);
            // 頂点 (Mesh用)
            _vertexBuffer = new ComputeBuffer(num, Marshal.SizeOf(typeof(VertexData)));
            
            // 半稜線・面 (ハーフエッジ構造)
            num = filter.mesh.triangles.Length / 3;
            _edgeBuffer = new ComputeBuffer(num * 3, Marshal.SizeOf(typeof(EdgeData)));
            _faceBuffer = new ComputeBuffer(num, Marshal.SizeOf(typeof(FaceData)));

            num = filter.mesh.triangles.Length / 3;
            threadGroupSize = Mathf.CeilToInt((float)num / SIMULATION_BLOCK_SIZE);
            CS.SetBuffer(InitFace, "_FaceBufferRW", _faceBuffer);
            CS.Dispatch(InitFace, threadGroupSize, 1, 1);

            CS.SetBuffer(InitEdge, "_EdgeBufferRW", _edgeBuffer);
            _triangleBuffer = new ComputeBuffer(num * 3, Marshal.SizeOf(typeof(int)));
            _triangleBuffer.SetData(filter.mesh.triangles);
            CS.SetBuffer(InitEdge, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(InitEdge, "_TriangleBufferRW", _triangleBuffer);
            CS.Dispatch(InitEdge, threadGroupSize, 1, 1);

            // 頂点データ更新用intバッファ
            PBD_Itteration_L = CS.FindKernel("PBD_Itteration_L");
            num = filter.mesh.vertexCount;
            _deltaPosAsInt = new ComputeBuffer(num, Marshal.SizeOf(typeof(Vector3Int)));
            CS.SetBuffer(PBD_Itteration_L, "_DeltaPosAsInt", _deltaPosAsInt);
            CS.SetBuffer(SearchVertices, "_DeltaPosAsInt", _deltaPosAsInt);
            CS.SetBuffer(PBD_StartFrame, "_DeltaPosAsInt", _deltaPosAsInt);
            CS.SetBuffer(PBD_Update_Ass, "_DeltaPosAsInt", _deltaPosAsInt);
            _deltaCount = new ComputeBuffer(num, Marshal.SizeOf(typeof(int)));
            CS.SetBuffer(PBD_Itteration_L, "_DeltaCount", _deltaCount);
            CS.SetBuffer(SearchVertices, "_DeltaCount", _deltaCount);
            CS.SetBuffer(PBD_StartFrame, "_DeltaCount", _deltaCount);
            CS.SetBuffer(PBD_Update_Ass, "_DeltaCount", _deltaCount);

            
        }
        void PBD_Simulation() {
            CS.SetInt("_VertNum", filter.mesh.vertices.Length);
            CS.SetInt("_EdgeNum", filter.mesh.triangles.Length);
            CS.SetInt("_FaceNum", filter.mesh.triangles.Length / 3);
            CS.SetInt("_LConstNum", filter.mesh.triangles.Length);
            CS.SetInt("_BConstNum", 0);

            int threadGroupSize;
            // 頂点情報更新
            threadGroupSize = Mathf.CeilToInt((float)filter.mesh.vertices.Length / SIMULATION_BLOCK_SIZE);
            CS.SetFloat("_delta_time", Time.deltaTime);
            CS.SetBuffer(PBD_StartFrame, "_VertBufferRW", _vertBuffer);
            CS.Dispatch(PBD_StartFrame, threadGroupSize, 1, 1);

            // イテレーション
            CS.SetBuffer(PBD_Itteration_L, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(PBD_Itteration_L, "_LengthConstraintBufferRW", _lengthConstraintBuffer);
            CS.SetBuffer(PBD_Update_Ass, "_VertBufferRW", _vertBuffer);

            CS.SetFloat("_delta_time", Time.deltaTime);

            for (int i = 0; i < 10; i++) {
                CS.SetInt("_itr_count", i);

                threadGroupSize = Mathf.CeilToInt((float)filter.mesh.triangles.Length / SIMULATION_BLOCK_SIZE);
                CS.Dispatch(PBD_Itteration_L, threadGroupSize, 1, 1);

                threadGroupSize = Mathf.CeilToInt((float)filter.mesh.vertices.Length / SIMULATION_BLOCK_SIZE);
                CS.Dispatch(PBD_Update_Ass, threadGroupSize, 1, 1);
            }

            // 頂点情報更新
            threadGroupSize = Mathf.CeilToInt((float)filter.mesh.vertices.Length / SIMULATION_BLOCK_SIZE);
            CS.SetBuffer(PBD_Update_Pos, "_VertBufferRW", _vertBuffer);
            CS.Dispatch(PBD_Update_Pos, threadGroupSize, 1, 1);
        }
        void SetPair() { 
            int id, threadGroupSize;

            // 頂点単位の探索
            id = CS.FindKernel("SetPair");
            threadGroupSize = Mathf.CeilToInt((float) filter.mesh.triangles.Length / SIMULATION_BLOCK_SIZE);
            CS.SetInt("_EdgeNum", filter.mesh.triangles.Length);
            CS.SetBuffer(id, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(id, "_EdgeBufferRW", _edgeBuffer);
            //CS.SetBuffer(id, "_TriangleBufferRW", _triangleBuffer);
            CS.Dispatch(id, threadGroupSize, 1, 1); // ComputeShaderを実行
        }
        void SetConstraint() {
            // テスト用PBD制約 (稜線の長さを0.1に)
            int num = filter.mesh.triangles.Length;
            int threadGroupSize = Mathf.CeilToInt((float)num / SIMULATION_BLOCK_SIZE);
            _lengthConstraintBuffer = new ComputeBuffer(num, Marshal.SizeOf(typeof(LengthConstraintData)));
            CS.SetBuffer(TestPBD, "_EdgeBufferRW", _edgeBuffer);
            CS.SetBuffer(TestPBD, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(TestPBD, "_LengthConstraintBufferRW", _lengthConstraintBuffer);
            CS.Dispatch(TestPBD, threadGroupSize, 1, 1);

            var constArr = new LengthConstraintData[num];
            _lengthConstraintBuffer.GetData(constArr);
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
        }
        void OnDestroy()
        {
            // バッファを破棄
            ReleaseBuffer();
        }
        // Start is called before the first frame update
        void Start()
        {
            if (TryGetComponent<MeshFilter>(out var mf))
                filter = mf;
            else
                filter = this.gameObject.AddComponent<MeshFilter>();

            if (TryGetComponent<MeshRenderer>(out var mr))
                renderer = mr;
            else
                renderer = this.gameObject.AddComponent<MeshRenderer>();
            InitIndex();
            InitBuffer();
            //sw.Start();
            SetPair();
            SetConstraint();
        }

        // Update is called once per frame
        void Update()
        {
            int threadGroupSize;

            PBD_Simulation();
            
            // 頂点単位の探索
            threadGroupSize = Mathf.CeilToInt((float)filter.mesh.vertices.Length / SIMULATION_BLOCK_SIZE);
            CS.SetBuffer(SearchVertices, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(SearchVertices, "_EdgeBufferRW", _edgeBuffer);
            CS.SetBuffer(SearchVertices, "_FaceBufferRW", _faceBuffer);
            CS.SetBuffer(SearchVertices, "_TriangleBufferRW", _triangleBuffer);
            //CS.Dispatch(SearchVertices, threadGroupSize, 1, 1); // ComputeShaderを実行
            
            threadGroupSize = Mathf.CeilToInt((float)filter.mesh.vertices.Length / SIMULATION_BLOCK_SIZE);
            CS.SetBuffer(PBD_StartFrame, "_VertBufferRW", _vertBuffer);
            CS.Dispatch(PBD_StartFrame, threadGroupSize, 1, 1);

            CS.Dispatch(SearchVertices, threadGroupSize, 1, 1); // ComputeShaderを実行

            CS.SetBuffer(PBD_Update_Ass, "_VertBufferRW", _vertBuffer);
            CS.Dispatch(PBD_Update_Ass, threadGroupSize, 1, 1);
            CS.SetBuffer(PBD_Update_Pos, "_VertBufferRW", _vertBuffer);
            CS.Dispatch(PBD_Update_Pos, threadGroupSize, 1, 1);

            var vertArr = new VertData[filter.mesh.vertexCount];
            _vertBuffer.GetData(vertArr);

            // 三角形インデックス
            threadGroupSize = Mathf.CeilToInt((float)filter.mesh.triangles.Length / 3 / SIMULATION_BLOCK_SIZE);
            CS.SetBuffer(SetTriangles, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(SetTriangles, "_EdgeBufferRW", _edgeBuffer);
            CS.SetBuffer(SetTriangles, "_FaceBufferRW", _faceBuffer);
            CS.SetBuffer(SetTriangles, "_TriangleBufferRW", _triangleBuffer);
            CS.Dispatch(SetTriangles, threadGroupSize, 1, 1); // ComputeShaderを実行
            var triangleArr = new int[filter.mesh.triangles.Length];
            _triangleBuffer.GetData(triangleArr);
            filter.mesh.triangles = triangleArr;

            // 頂点情報
            threadGroupSize = Mathf.CeilToInt((float)filter.mesh.vertices.Length / SIMULATION_BLOCK_SIZE);
            CS.SetBuffer(SetVertices, "_VertBufferRW", _vertBuffer);
            CS.SetBuffer(SetVertices, "_VertexBufferRW", _vertexBuffer);
            CS.Dispatch(SetVertices, threadGroupSize, 1, 1); // ComputeShaderを実行
            var vertexArr = new VertexData[filter.mesh.vertices.Length];
            _vertexBuffer.GetData(vertexArr);

            filter.mesh.SetVertexBufferParams(filter.mesh.vertices.Length, layout);
            filter.mesh.SetVertexBufferData(vertexArr, 0, 0, filter.mesh.vertices.Length);
            filter.mesh.RecalculateNormals();
            filter.mesh.RecalculateBounds();
            
            var edgeArr = new EdgeData[filter.mesh.triangles.Length];
            _edgeBuffer.GetData(edgeArr);
            var constArr = new LengthConstraintData[filter.mesh.triangles.Length];
            _lengthConstraintBuffer.GetData(constArr);

            for (int i = 0; i < edgeArr.Length; i++) {
                //if (edgeArr[edgeArr[i].Pair].Pair == i)
                //    UnityEngine.Debug.DrawLine(vertArr[edgeArr[i].Vert].Pos, vertArr[edgeArr[i].Vert].Pos + vertArr[edgeArr[i].Vert].Nor);
                if (i >= edgeArr[i].Pair)
                    continue;
                Random.InitState(i);
                var c = Color.HSVToRGB(Random.value, 1, 1);
                Vector3 dir = vertArr[edgeArr[i].Vert].Pos - vertArr[edgeArr[edgeArr[i].Pair].Vert].Pos;
                Vector3 bt = Vector3.Cross(dir, vertArr[edgeArr[i].Vert].Nor).normalized * 0.01f; 
                //UnityEngine.Debug.DrawLine(vertArr[edgeArr[i].Vert].Pos + bt, vertArr[edgeArr[i].Vert].Pos + vertArr[edgeArr[i].Vert].Nor * 0.2f + bt, c);
                //UnityEngine.Debug.DrawLine(vertArr[edgeArr[i].Vert].Pos + bt, vertArr[edgeArr[edgeArr[i].Next].Vert].Pos + bt, c);
                //UnityEngine.Debug.DrawLine(vertArr[edgeArr[edgeArr[i].Pair].Vert].Pos - bt, vertArr[edgeArr[edgeArr[edgeArr[i].Pair].Next].Vert].Pos - bt, c);
            }

            //UnityEngine.Debug.Log(vertexArr[0].Pos);
            //UnityEngine.Debug.Log(filter.mesh.triangles.Length);
            //UnityEngine.Debug.Log(triangleArr.Length);
        }
    } // class
} // namespace
