Shader "Custom/BaseTransparent"
{
    Properties
    {
        _MainTex        ("Albedo",          2D)            = "white" {}
        _Alpha          ("Alpha",           Range(0, 1))   = 1.0
        _ShadowFloor    ("Shadow Floor",    Range(0, 1))   = 0.2
        [NoScaleOffset]
        _MatcapTex      ("Matcap",          2D)            = "black" {}
        _MatcapScale    ("Matcap Scale",    Float)         = 1.0
        _MatcapStrength ("Matcap Strength", Float)         = 1.0
        _MatcapMask     ("Matcap Mask",     2D)            = "white" {}
        _AlphaTex       ("Alpha Texture",   2D)            = "white" {}
        _Columns        ("Columns",         Int)           = 1
        _Rows           ("Rows",            Int)           = 1
        _FPS            ("FPS",             Float)         = 12
        _TotalFrames    ("Total Frames",    Int)           = 1

        [Space(10)]
        [Header(Noise Dissolve)]
        _Threshold      ("Visible Fraction",    Range(0,1))      = 1.0
        _NoiseScale     ("Noise Scale",         Range(0.5,20))   = 4.0
        _EdgeWidth      ("Edge Glow Width",     Range(0.01,0.3)) = 0.08
        [HDR]
        _EdgeColor      ("Edge Glow Color",     Color)           = (0.3,0.9,1,1)
        _EdgeGlow       ("Edge Glow Intensity", Range(0,8))      = 2.0
        _NoiseSpeed     ("Noise Speed",         Range(0,2))      = 0.0
        _NoiseStartTime ("Noise Start Time",    Float)           = 999999

        [Space(10)]
        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull   ("Cull",                Float) = 2
        [Enum(Off, 0, On, 1)]                  _ZWrite ("Frontmost Face Only", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull   [_Cull]
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;    float4 _MainTex_ST;
            sampler2D _AlphaTex;   float4 _AlphaTex_ST;
            sampler2D _MatcapTex;
            sampler2D _MatcapMask; float4 _MatcapMask_ST;

            float _Alpha;
            float _ShadowFloor;
            float _MatcapScale;
            float _MatcapStrength;
            float _Columns;
            float _Rows;
            float _FPS;
            float _TotalFrames;
            float _Threshold;
            float _NoiseScale;
            float _EdgeWidth;
            half4 _EdgeColor;
            float _EdgeGlow;
            float _NoiseSpeed;
            float _NoiseStartTime;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos        : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalVS   : TEXCOORD3;
                float2 maskUV     : TEXCOORD4;
                float3 normalWS   : TEXCOORD5;
                float2 alphaUV    : TEXCOORD6;
                SHADOW_COORDS(2)
            };

            float BHash(float3 p)
            {
                p  = frac(p * float3(443.8975, 397.2973, 491.1871));
                p += dot(p.xyz, p.yzx + 19.19);
                return frac(p.x * p.y * p.z);
            }

            float BNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(BHash(i+float3(0,0,0)), BHash(i+float3(1,0,0)), f.x),
                         lerp(BHash(i+float3(0,1,0)), BHash(i+float3(1,1,0)), f.x), f.y),
                    lerp(lerp(BHash(i+float3(0,0,1)), BHash(i+float3(1,0,1)), f.x),
                         lerp(BHash(i+float3(0,1,1)), BHash(i+float3(1,1,1)), f.x), f.y),
                    f.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos        = UnityObjectToClipPos(v.vertex);
                o.positionWS = mul(unity_ObjectToWorld, v.vertex).xyz;

                float2 stUV  = TRANSFORM_TEX(v.uv, _MainTex);
                float frame  = floor(fmod(_Time.y, _TotalFrames / _FPS) * _FPS);
                float col    = fmod(frame, _Columns);
                float row    = floor(frame / _Columns);
                float2 cell  = float2(1.0 / _Columns, 1.0 / _Rows);
                o.uv         = stUV * cell + float2(col, (_Rows - 1.0) - row) * cell;

                o.normalVS   = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                o.normalWS   = UnityObjectToWorldNormal(v.normal);
                o.maskUV     = TRANSFORM_TEX(v.uv, _MatcapMask);
                o.alphaUV    = TRANSFORM_TEX(v.uv, _AlphaTex);

                TRANSFER_SHADOW(o)
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // Noise dissolve
                float animTime = max(0.0, _Time.y - _NoiseStartTime);
                float3 coord   = i.positionWS * _NoiseScale
                               + animTime * _NoiseSpeed * float3(0.4, 1.0, 0.6);
                float n      = BNoise(coord);
                float cutoff = 1.0 - _Threshold;
                float above  = (n - cutoff) / max(_EdgeWidth, 0.001);
                clip(above);
                float edgeMask = saturate(1.0 - above);

                half4 albedo = tex2D(_MainTex, i.uv);
                half3 colour = albedo.rgb;

                // matcap
                float3 viewDirVS = normalize(mul((float3x3)UNITY_MATRIX_V, normalize(UnityWorldSpaceViewDir(i.positionWS))));
                float3 reflVS    = reflect(-viewDirVS, normalize(i.normalVS));
                float2 matcapUV  = (reflVS.xy * 0.5 + 0.5 - 0.5) * _MatcapScale + 0.5;
                half3  matcap    = tex2D(_MatcapTex, matcapUV).rgb;
                half   mask      = tex2D(_MatcapMask, i.maskUV).r;

                half shadow      = SHADOW_ATTENUATION(i);
                half3 normalWS   = normalize(i.normalWS);
                half  NdotL      = saturate(dot(normalWS, normalize(_WorldSpaceLightPos0.xyz)));
                half  attenuation = max(NdotL * shadow, half(_ShadowFloor));
                colour           *= _LightColor0.rgb * attenuation;
                colour           += matcap * mask * _MatcapStrength;
                colour           += _EdgeColor.rgb * _EdgeGlow * edgeMask;

                half alphaTex  = tex2D(_AlphaTex, i.alphaUV).r;
                half fadeAlpha = saturate(albedo.a * alphaTex * _Alpha);

                return half4(colour, fadeAlpha);
            }
            ENDCG
        }
    }

    CustomEditor "BaseTransparentShaderGUI"
}
