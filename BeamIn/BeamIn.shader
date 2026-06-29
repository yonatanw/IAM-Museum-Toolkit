Shader "Custom/BeamIn"
{
    Properties
    {
        [Header(Appearance)]
        _GlowColor          ("Glow Color",          Color)         = (0.3, 0.6, 1.0, 1)
        _EdgeColor          ("Edge Color",          Color)         = (0.8, 0.9, 1.0, 1)
        _EdgeBrightness     ("Edge Brightness",     Range(1, 20))  = 8.0
        _EdgeWidth          ("Edge Width",          Range(0, 0.5)) = 0.05
        _SettleWidth        ("Settle Width",        Range(0, 1))   = 0.3

        [Space(10)]
        [Header(Noise)]
        _NoiseScale         ("Sweep Noise Scale",   Range(0.5, 50))= 5.0
        _NoiseStrength      ("Noise Strength",      Range(0, 1))   = 0.4
        _FadeNoiseScale     ("Fade Noise Scale",    Range(0.5, 50))= 3.0

        [Space(10)]
        [Header(Glow Animation)]
        _GlowNoiseScale     ("Glow Noise Scale",    Range(0.5, 50))= 4.0
        _GlowNoiseSpeed     ("Glow Noise Speed",    Range(0, 5))   = 1.0
        _GlowNoiseStrength  ("Glow Noise Strength", Range(0, 2))   = 0.5
        _GlowTimeOffset     ("Glow Time Offset",    Float)         = 0.0

        [Space(10)]
        [Header(Timing)]
        _SweepEase          ("Sweep Ease",          Range(0.5, 4)) = 1.5
        _FadeEase           ("Fade Ease",           Range(0.5, 4)) = 1.5

        [Space(10)]
        [Header(Bounds)]
        _BeamBottom         ("Beam Bottom Y",       Float)         = -0.5
        _BeamTop            ("Beam Top Y",          Float)         =  0.5

        [HideInInspector] _SweepProgress ("Sweep Progress", Range(0,1)) = 0.0
        [HideInInspector] _FadeProgress  ("Fade Progress",  Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Offset -1, -1
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float4 _GlowColor;
            float4 _EdgeColor;
            float  _EdgeBrightness;
            float  _EdgeWidth;
            float  _SettleWidth;
            float  _NoiseScale;
            float  _NoiseStrength;
            float  _FadeNoiseScale;
            float  _SweepEase;
            float  _FadeEase;
            float  _GlowNoiseScale;
            float  _GlowNoiseSpeed;
            float  _GlowNoiseStrength;
            float  _GlowTimeOffset;
            float  _BeamBottom;
            float  _BeamTop;
            float  _SweepProgress;
            float  _FadeProgress;

            struct appdata { float4 vertex : POSITION; };
            struct v2f     { float4 pos : SV_POSITION; float3 positionWS : TEXCOORD0; };

            float hash(float3 p)
            {
                p = frac(p * float3(443.8975, 397.2973, 491.1871));
                p += dot(p.xyz, p.yzx + 19.19);
                return frac(p.x * p.y * p.z);
            }

            float noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(hash(i+float3(0,0,0)), hash(i+float3(1,0,0)), f.x),
                         lerp(hash(i+float3(0,1,0)), hash(i+float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash(i+float3(0,0,1)), hash(i+float3(1,0,1)), f.x),
                         lerp(hash(i+float3(0,1,1)), hash(i+float3(1,1,1)), f.x), f.y),
                    f.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos         = UnityObjectToClipPos(v.vertex);
                o.positionWS  = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float normY = saturate((i.positionWS.y - _BeamBottom) / max(_BeamTop - _BeamBottom, 0.001));

                // ── Phase 1: sweep reveal (bottom → top) ──────────────────────
                float sweepT    = pow(saturate(_SweepProgress), _SweepEase);
                float nSweep    = noise(i.positionWS * _NoiseScale);
                float dissolveY = normY - nSweep * _NoiseStrength;

                if (dissolveY > sweepT)
                    return half4(0, 0, 0, 0);

                // ── Phase 2: fade dissolve (bottom → top, different noise) ────
                float fadeT        = pow(saturate(_FadeProgress), _FadeEase);
                float nFade        = noise(i.positionWS * _FadeNoiseScale);
                float fadeLine     = fadeT * (1.0 + _NoiseStrength) - _NoiseStrength;
                float fadeDissolve = normY - nFade * _NoiseStrength;
                float alpha        = (fadeT <= 0.0) ? 1.0 : smoothstep(fadeLine - 0.04, fadeLine + 0.04, fadeDissolve);

                // ── Sweep edge glow ───────────────────────────────────────────
                float distFromFront = sweepT - dissolveY;
                float sparkle = 1.0 - saturate(distFromFront / max(_EdgeWidth,   0.001));
                float settle  = 1.0 - saturate(distFromFront / max(_SettleWidth, 0.001));

                // ── Animated glow color noise ─────────────────────────────────
                float  t       = (_Time.y + _GlowTimeOffset) * _GlowNoiseSpeed;
                float3 tOffset = float3(t, 0, t * 0.7);
                float  nGlow   = noise(i.positionWS * _GlowNoiseScale + tOffset);
                half3  glowCol = _GlowColor.rgb + _GlowColor.rgb * (half)(nGlow * _GlowNoiseStrength);

                half3 colour = lerp(glowCol, _EdgeColor.rgb, (half)settle);
                colour += _EdgeColor.rgb * (half)sparkle * _EdgeBrightness;

                return half4(colour, (half)alpha);
            }
            ENDCG
        }
    }
}
