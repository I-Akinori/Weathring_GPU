struct LengthConstraint {
    int p0;
    int p1;
    float offset;
    float stiff;
    float thr;      // if thr < 0, disable the constraint
};

struct BendConstraint {
    int p0;
    int p1;
    int p2;
    int p3;
    float offset;
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
    float dust;
    float bond;
    float liftingForce;     // 可視化用
    float bindingForce;     // liftingForceは合計，bindingForceは平均をとる
    uint stat;
    float3 direction;  // ひび割れの方向 (status == 1 のときに参照)
    int onEdge;
    int lowest;  // 0: false, 1: true
    float thickness;
    int edge;     // HalfEdge
    float w;
};

struct Edge {
    int next;   // HalfEdge
    int prev;   // HalfEdge
    int pair;   // HalfEdge
    int face;   // Face
    int vert;   // Vertex
    uint stat;   // 0: connected, 1: tore, 2: no pair

    float angle;
    float liftingForce;
    float bindingForce;
};

struct Face {
    int edge;   // HalfEdge
    uint stat;   // 0: attatched, 1: peeled

    float contraction;
};

struct Vertex {
    float3 pos;
    float3 nor;
    float2 uv;
    float4 col;
};