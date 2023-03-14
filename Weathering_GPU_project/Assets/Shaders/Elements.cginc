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
    float rust_buf;
    float dust;
    float chalk;
    float bond;
    float liftingForce;     // ‰ÂŽ‹‰»—p
    uint stat;
    float3 direction;  // ‚Ð‚ÑŠ„‚ê‚Ì•ûŒü (status == 1 ‚Ì‚Æ‚«‚ÉŽQÆ)
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