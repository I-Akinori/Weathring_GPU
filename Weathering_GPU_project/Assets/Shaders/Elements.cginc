struct LengthConstraint {
    uint p0;
    uint p1;
    float offset;
    float offset0;
    float stiff;
    float thr;      // if thr < 0, disable the constraint
};

struct BendConstraint {
    uint p0;
    uint p1;
    uint p2;
    uint p3;
    float offset;
    float offset0;
    float stiff;
    float thr;
};

struct Vert {
    float3 pos;
    float3 nor;
    float3 pos0;
    float3 nor0;
    float3 vel;
    float3 ass;
    float3 frc;
    float3 del;
    float2 uv;
    float4 col;
    float cur;

    float rust;
    float rust_buf;
    float dust;
    float chalk;
    float bond;
    float liftingForce;     // 可視化用
    uint stat;         // 0: 分離前, 1: 分離状態, 2: 亀裂端点, 3 ~ 30: (stat - 1) 本の亀裂，32: 無効化
    float3 direction;  // ひび割れの方向 (status == 1 のときに参照)
    uint onEdge;
    uint lowest;  // 0: false, 1: true
    float thickness;
    uint edge;     // HalfEdge
    uint orig;
    float w;
    float localSpeed;
};

struct Edge {
    uint next;   // HalfEdge
    uint prev;   // HalfEdge
    uint pair;   // HalfEdge
    uint face;   // Face
    uint vert;   // Vertex
    uint stat;   // 0: connected, 1: no pair, 2: tore, 3: connected crack

    float angle;
    float liftingForce;
    float bindingForce;
};

struct Face {
    float3 nor;
    uint edge;   // HalfEdge
    uint stat;   // 0: attatched, 1: peeled

    float contraction;
    float tension;
};

struct Vertex {
    float3 pos;
    float3 nor;
    float2 uv;
    float4 col;
};