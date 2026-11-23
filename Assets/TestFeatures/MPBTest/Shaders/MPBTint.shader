// 测试用最简着色器：唯一目的是把 _Color 声明成「逐实例属性」（放进 UNITY_INSTANCING_BUFFER），
// 用来和内置 Standard 着色器对照——Standard 的 _Color 是普通 uniform，塞进 MaterialPropertyBlock
// 会被 Unity 判定为「非 instanced 属性」而禁用 instancing（见 Unity 手册「Add per-instance properties」）。
// 不参与光照、不投阴影，颜色就是 MPB 里给的值，方便直接肉眼看逐实例数据有没有生效。
Shader "MPBTest/Tint"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing        // 有它才有 INSTANCING_ON 变体，材质上的 Enable GPU Instancing 才有意义

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 逐实例数据就放这里：Unity 会把每个渲染器 MPB 里的 _Color 收集成数组，一次 draw call 传下来
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                return UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
            }
            ENDCG
        }
    }
}
