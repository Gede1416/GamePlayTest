Shader "Boids/GPU Arrow"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard addshadow
        #pragma target 4.5
        #pragma instancing_options procedural:Setup

        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
        StructuredBuffer<float4> _Boids;
        #endif

        void Setup()
        {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            float4 state = _Boids[unity_InstanceID];
            float2 p = state.xy;
            float2 f = normalize(state.zw);
            unity_ObjectToWorld = float4x4(
                f.y, 0, f.x, p.x,
                  0, 1,   0, 0,
               -f.x, 0, f.y, p.y,
                  0, 0,   0, 1);
            unity_WorldToObject = float4x4(
                f.y, 0, -f.x, -p.x * f.y + p.y * f.x,
                  0, 1,    0, 0,
                f.x, 0,  f.y, -dot(p, f),
                  0, 0,    0, 1);
            #endif
        }

        struct Input { float3 worldPos; };
        void surf(Input input, inout SurfaceOutputStandard output)
        {
            output.Albedo = 1;
            output.Smoothness = 0.5;
            output.Alpha = 1;
        }
        ENDCG
    }
}
