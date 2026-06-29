Shader "Custom/Hologram"
{
    Properties
    {
        _MainTex            ("Texture",             2D)            = "white" {}
        _ColorTex           ("Color Texture",       2D)            = "white" {}
        _Color              ("Tint",               Color)         = (0, 0.8, 1, 1)
        _Alpha              ("Alpha",               Range(0, 1))   = 0.8

        [Space(8)]
        [Header(Fresnel Rim)]
        _FresnelPower       ("Power",               Range(0.1, 8)) = 2.0
        _FresnelStrength    ("Strength",            Range(0, 3))   = 1.5

        [Space(8)]
        [Header(Scanlines)]
        _ScanlineScale      ("Spacing (px/line)",   Range(1, 50))  = 6.0
        _ScanlineSpeed      ("Scroll Speed",        Range(-5, 5))  = 0.5
        _ScanlineMin        ("Minimum Brightness",  Range(0, 1))   = 0.4
        _ScanlineSharpness  ("Sharpness",           Range(0, 1))   = 0.7

        [Space(8)]
        [Header(Glitch)]
        _GlitchSpeed        ("Trigger Speed",       Range(0, 30))  = 6.0
        _GlitchStrength     ("Displacement",        Range(0, 0.1)) = 0.015
        _GlitchBandScale    ("Band Scale",          Range(1, 50))  = 12.0
        _GlitchThreshold    ("Threshold (0=always 1=never)", Range(0, 1)) = 0.92

        [Space(8)]
        [Header(Flicker)]
        _FlickerSpeed       ("Speed",               Range(0, 20))  = 8.0
        _FlickerStrength    ("Strength",            Range(0, 1))   = 0.08

        [Space(8)]
        [Header(Output)]
        _GlowStrength       ("Glow",                Range(0, 4))   = 1.8

        // Controlled via HologramShaderGUI checkboxes — not shown in default inspector
        [HideInInspector] _Cull   ("Cull",   Float) = 2   // 0=Off  2=Back
        [HideInInspector] _ZWrite ("ZWrite", Float) = 0   // 0=Off  1=On
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" }

        Pass
        {
            Blend   SrcAlpha One        // additive — classic hologram glow
            Cull    [_Cull]             // checkbox: Cull Back Faces
            ZWrite  [_ZWrite]           // checkbox: Frontmost Surface Only
            ZTest   Always

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;    float4 _MainTex_ST;
            sampler2D _ColorTex;   float4 _ColorTex_ST;
            float4 _Color;
            float  _Alpha;
            float  _FresnelPower;
            float  _FresnelStrength;
            float  _ScanlineScale;
            float  _ScanlineSpeed;
            float  _ScanlineMin;
            float  _ScanlineSharpness;
            float  _GlitchSpeed;
            float  _GlitchStrength;
            float  _GlitchBandScale;
            float  _GlitchThreshold;
            float  _FlickerSpeed;
            float  _FlickerStrength;
            float  _GlowStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos        : SV_POSITION;
                float2 uv         : TEXCOORD0;  // _MainTex-transformed
                float2 rawUV      : TEXCOORD1;  // raw mesh UV, for _ColorTex
                float3 normalWS   : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float4 screenPos  : TEXCOORD4;
            };

            float hash(float x) { return frac(sin(x * 127.1 + 311.7) * 43758.5453); }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos        = UnityObjectToClipPos(v.vertex);
                o.uv         = TRANSFORM_TEX(v.uv, _MainTex);
                o.rawUV      = v.uv;
                o.positionWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normalWS   = UnityObjectToWorldNormal(v.normal);
                o.screenPos  = ComputeScreenPos(o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // Glitch: random horizontal UV displacement in world-space bands
                float glitchT    = floor(_Time.y * _GlitchSpeed);
                float glitchFire = step(_GlitchThreshold, hash(glitchT * 3.7));
                float glitchBand = floor(i.positionWS.y * _GlitchBandScale);
                float glitchOff  = (hash(glitchT + glitchBand * 31.7) - 0.5)
                                   * _GlitchStrength * glitchFire;

                // _MainTex: alpha/mask only
                float2 uv  = i.uv + float2(glitchOff, 0);
                half   mask = tex2D(_MainTex, uv).a;

                // _ColorTex: color only, with its own tiling/offset
                float2 colorUV = TRANSFORM_TEX(i.rawUV, _ColorTex) + float2(glitchOff, 0);
                half3  colorTex = tex2D(_ColorTex, colorUV).rgb;

                // Fresnel rim
                float3 N      = normalize(i.normalWS);
                float3 V      = normalize(UnityWorldSpaceViewDir(i.positionWS));
                float  fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                // Scanlines — horizontal in screen space, scrolling upward
                float screenY   = (i.screenPos.y / i.screenPos.w) * _ScreenParams.y;
                float phase     = frac(screenY / _ScanlineScale
                                       + _Time.y * _ScanlineSpeed);
                float raw       = sin(phase * UNITY_TWO_PI) * 0.5 + 0.5;
                float sharpened = saturate((raw - 0.5) / max(1.0 - _ScanlineSharpness, 0.01) + 0.5);
                float scanline  = lerp(_ScanlineMin, 1.0, sharpened);

                // Subtle brightness flicker
                float flicker = 1.0 - hash(floor(_Time.y * _FlickerSpeed)) * _FlickerStrength;

                half alpha = saturate(
                    (mask * _Alpha + fresnel * _FresnelStrength) * scanline * flicker);

                half3 col = _Color.rgb * colorTex * _GlowStrength
                          * (1.0 + fresnel * _FresnelStrength * 0.3);

                return half4(col, alpha);
            }
            ENDCG
        }
    }

    CustomEditor "HologramShaderGUI"
}
