Shader "Custom/StandardDissolve"
{
    Properties
    {
        _Color          ("Color",              Color)         = (1,1,1,1)
        _MainTex        ("Albedo",             2D)            = "white" {}
        _Glossiness     ("Smoothness",         Range(0,1))    = 0.5
        _Metallic       ("Metallic",           Range(0,1))    = 0.0
        _BumpMap        ("Normal Map",         2D)            = "bump" {}

        [Space(8)]
        [Header(Matcap)]
        [NoScaleOffset]
        _MatcapTex      ("Matcap Texture",     2D)            = "black" {}
        [NoScaleOffset]
        _MatcapMask     ("Matcap Mask",        2D)            = "white" {}
        _MatcapScale    ("Matcap Scale",       Float)         = 1.0
        _MatcapStrength ("Matcap Strength",    Float)         = 0.0

        [Space(8)]
        [Header(Noise Mask)]
        _Threshold      ("Visible Fraction",   Range(0,1))    = 0.5
        _NoiseScale     ("Noise Scale",        Range(0.1,20)) = 3.0
        _EdgeWidth      ("Edge Glow Width",    Range(0.01,0.4)) = 0.08
        [HDR]
        _EdgeColor      ("Edge Glow Color",    Color)         = (0.3,0.9,1,1)
        _EdgeGlow       ("Edge Glow Intensity",Range(0,8))    = 3.0

        [Space(8)]
        [Header(Animation)]
        _NoiseSpeed     ("Noise Speed",        Range(0,2))    = 0.0
        _NoiseStartTime ("Noise Start Time",   Float)         = 999999
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _MatcapTex;
        sampler2D _MatcapMask;

        half4   _Color;
        half    _Glossiness;
        half    _Metallic;
        half    _MatcapScale;
        half    _MatcapStrength;
        float   _Threshold;
        float   _NoiseScale;
        float   _EdgeWidth;
        half4   _EdgeColor;
        float   _EdgeGlow;
        float   _NoiseSpeed;
        float   _NoiseStartTime;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float3 worldPos;
            float3 normalVS;
            INTERNAL_DATA
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.normalVS = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
        }

        float DHash(float3 p)
        {
            p  = frac(p * float3(443.8975, 397.2973, 491.1871));
            p += dot(p.xyz, p.yzx + 19.19);
            return frac(p.x * p.y * p.z);
        }

        float DNoise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(
                lerp(lerp(DHash(i+float3(0,0,0)), DHash(i+float3(1,0,0)), f.x),
                     lerp(DHash(i+float3(0,1,0)), DHash(i+float3(1,1,0)), f.x), f.y),
                lerp(lerp(DHash(i+float3(0,0,1)), DHash(i+float3(1,0,1)), f.x),
                     lerp(DHash(i+float3(0,1,1)), DHash(i+float3(1,1,1)), f.x), f.y),
                f.z);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Noise dissolve
            float  animTime = max(0.0, _Time.y - _NoiseStartTime);
            float3 coord    = IN.worldPos * _NoiseScale
                            + animTime * _NoiseSpeed * float3(0.4, 1.0, 0.6);
            float n      = DNoise(coord);
            float cutoff = 1.0 - _Threshold;
            float above  = (n - cutoff) / max(_EdgeWidth, 0.001);
            clip(above);
            float edgeMask = saturate(1.0 - above);

            // Base
            half4 c      = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo     = c.rgb;
            o.Normal     = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            o.Metallic   = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha      = c.a;

            // Matcap - identical approach to BaseTransparent
            float3 viewDirVS = normalize(mul((float3x3)UNITY_MATRIX_V,
                                   normalize(_WorldSpaceCameraPos.xyz - IN.worldPos)));
            float3 reflVS    = reflect(-viewDirVS, normalize(IN.normalVS));
            float2 matcapUV  = (reflVS.xy * 0.5 + 0.5 - 0.5) * _MatcapScale + 0.5;
            half3  matcap    = tex2D(_MatcapTex,  matcapUV).rgb;
            half   mcMask    = tex2D(_MatcapMask, IN.uv_MainTex).r;

            o.Emission = matcap * mcMask * _MatcapStrength
                       + _EdgeColor.rgb * _EdgeGlow * edgeMask;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
