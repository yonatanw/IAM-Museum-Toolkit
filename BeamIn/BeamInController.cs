using UnityEngine;
using UnityEngine.Rendering;

public class BeamInController : MonoBehaviour
{
    [Tooltip("All renderers that have the BeamIn shader")]
    public Renderer[] beamRenderers;

    [Tooltip("Root to search for beam renderers — used by Collect Beam Renderers")]
    public Transform beamRoot;

    [Tooltip("The renderer(s) showing the real final material — hidden during phase 1, revealed at phase 2")]
    public Renderer[] realRenderers;

    [Tooltip("Root to search for real renderers — used by Collect Real Renderers")]
    public Transform buildingRoot;

    [Header("Timing")]
    [Tooltip("Frames per second used to convert all frame values to seconds")]
    public int fps = 30;

    [Tooltip("Delay before the effect starts (frames)")]
    public int startDelayFrames = 0;

    [Tooltip("Duration of the sweep reveal phase (frames)")]
    public int sweepFrames = 36;

    [Tooltip("Duration of the fade-out phase (frames)")]
    public int fadeFrames = 36;

    [Tooltip("How many frames before the sweep ends the fade begins")]
    public int overlapFrames = 0;

    [Tooltip("Sweep easing power")]
    [Range(0.5f, 4f)]
    public float sweepEase = 1.5f;

    [Tooltip("Fade easing power")]
    [Range(0.5f, 4f)]
    public float fadeEase = 1.5f;

    [Tooltip("At what fade progress (0-1) shadow casting is re-enabled on real renderers")]
    [Range(0f, 1f)]
    public float shadowCastingRestoreAt = 0.9f;

    [Header("Beam Bounds (world Y)")]
    public float beamBottom = -0.5f;
    public float beamTop    =  0.5f;

    [Header("Appearance")]
    public Color glowColor       = new Color(0.3f, 0.6f, 1.0f, 1f);
    public Color edgeColor       = new Color(0.8f, 0.9f, 1.0f, 1f);

    [Range(1f, 20f)]
    public float edgeBrightness  = 8f;

    [Range(0f, 0.5f)]
    public float edgeWidth       = 0.05f;

    [Range(0f, 1f)]
    public float settleWidth     = 0.3f;

    [Header("Noise")]
    [Range(0.5f, 50f)]
    public float noiseScale      = 5f;

    [Range(0f, 1f)]
    public float noiseStrength   = 0.4f;

    [Range(0.5f, 50f)]
    public float fadeNoiseScale  = 3f;

    [Header("Glow Animation")]
    [Range(0.5f, 50f)]
    public float glowNoiseScale    = 4f;

    [Range(0f, 5f)]
    public float glowNoiseSpeed    = 1f;

    [Range(0f, 2f)]
    public float glowNoiseStrength = 0.5f;

    public float glowTimeOffset    = 0f;

    float StartDelay    => startDelayFrames / (float)Mathf.Max(fps, 1);
    float SweepDuration => sweepFrames      / (float)Mathf.Max(fps, 1);
    float FadeDuration  => fadeFrames       / (float)Mathf.Max(fps, 1);
    float FadeStartTime => SweepDuration - overlapFrames / (float)Mathf.Max(fps, 1);

    [ContextMenu("Collect Beam Renderers from Beam Root")]
    public void CollectBeamRenderers()
    {
        if (beamRoot == null) { Debug.LogWarning("BeamInController: assign Beam Root first."); return; }
        beamRenderers = beamRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
        Debug.Log($"BeamInController: collected {beamRenderers.Length} beam renderer(s) from {beamRoot.name}.");
    }

    [ContextMenu("Collect Real Renderers from Building Root")]
    public void CollectRenderers()
    {
        if (buildingRoot == null) { Debug.LogWarning("BeamInController: assign Building Root first."); return; }
        var beamSet  = new System.Collections.Generic.HashSet<Renderer>(beamRenderers ?? new Renderer[0]);
        var all      = buildingRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
        var filtered = System.Array.FindAll(all, r => !beamSet.Contains(r));
        realRenderers = filtered;
        Debug.Log($"BeamInController: collected {realRenderers.Length} real renderer(s) from {buildingRoot.name}.");
    }

    Material[] _beamMats;
    float _elapsed;
    bool _running;
    bool _realRevealed;
    bool _shadowsRestored;
    ShadowCastingMode[] _originalShadowModes;

    void OnEnable()
    {
        if (beamRenderers == null || beamRenderers.Length == 0)
        {
            Debug.LogWarning("BeamInController: no Beam Renderers assigned.", this);
            return;
        }
        _beamMats = new Material[beamRenderers.Length];
        for (int i = 0; i < beamRenderers.Length; i++)
            if (beamRenderers[i])
                _beamMats[i] = beamRenderers[i].material;

        CacheOriginalShadowModes();
        Restart();
    }

    void Update()
    {
        if (!_running) return;

        _elapsed += Time.deltaTime;
        if (_elapsed < 0f) return;

        float sweepProgress = Mathf.Clamp01(_elapsed / SweepDuration);
        float fadeProgress  = Mathf.Clamp01((_elapsed - FadeStartTime) / FadeDuration);

        SetBeamFloat("_SweepProgress", sweepProgress);
        SetBeamFloat("_FadeProgress",  fadeProgress);

        if (!_realRevealed && sweepProgress >= 1f)
        {
            SetRealRenderers(true, shadowsOn: false);
            _realRevealed = true;
        }

        if (_realRevealed && !_shadowsRestored && fadeProgress >= shadowCastingRestoreAt)
        {
            RestoreShadowModes();
            _shadowsRestored = true;
        }

        if (fadeProgress >= 1f)
        {
            _running = false;
            SetBeamRenderersEnabled(false);
        }
    }

    public void Trigger() => Restart();

    void Restart()
    {
        if (beamRenderers == null || beamRenderers.Length == 0) return;

        SetBeamFloat("_SweepProgress",    0f);
        SetBeamFloat("_FadeProgress",     0f);
        SetBeamFloat("_BeamBottom",       beamBottom);
        SetBeamFloat("_BeamTop",          beamTop);
        SetBeamFloat("_EdgeBrightness",   edgeBrightness);
        SetBeamFloat("_EdgeWidth",        edgeWidth);
        SetBeamFloat("_SettleWidth",      settleWidth);
        SetBeamFloat("_NoiseScale",       noiseScale);
        SetBeamFloat("_NoiseStrength",    noiseStrength);
        SetBeamFloat("_FadeNoiseScale",   fadeNoiseScale);
        SetBeamFloat("_SweepEase",        sweepEase);
        SetBeamFloat("_FadeEase",         fadeEase);
        SetBeamFloat("_GlowNoiseScale",   glowNoiseScale);
        SetBeamFloat("_GlowNoiseSpeed",   glowNoiseSpeed);
        SetBeamFloat("_GlowNoiseStrength",glowNoiseStrength);
        SetBeamFloat("_GlowTimeOffset",   glowTimeOffset);
        SetBeamColor("_GlowColor",        glowColor);
        SetBeamColor("_EdgeColor",        edgeColor);

        _elapsed         = -StartDelay;
        _running         = true;
        _realRevealed    = false;
        _shadowsRestored = false;
        SetBeamRenderersEnabled(true);
        SetRealRenderers(false, shadowsOn: false);
    }

    void SetBeamFloat(string prop, float value)
    {
        if (_beamMats == null) return;
        foreach (var m in _beamMats)
            if (m != null) m.SetFloat(prop, value);
    }

    void SetBeamColor(string prop, Color value)
    {
        if (_beamMats == null) return;
        foreach (var m in _beamMats)
            if (m != null) m.SetColor(prop, value);
    }

    void SetBeamRenderersEnabled(bool active)
    {
        if (beamRenderers == null) return;
        foreach (var r in beamRenderers)
            if (r) r.enabled = active;
    }

    void CacheOriginalShadowModes()
    {
        if (realRenderers == null) return;
        _originalShadowModes = new ShadowCastingMode[realRenderers.Length];
        for (int i = 0; i < realRenderers.Length; i++)
            if (realRenderers[i])
                _originalShadowModes[i] = realRenderers[i].shadowCastingMode;
    }

    void SetRealRenderers(bool active, bool shadowsOn)
    {
        if (realRenderers == null) return;
        for (int i = 0; i < realRenderers.Length; i++)
        {
            if (!realRenderers[i]) continue;
            realRenderers[i].enabled = active;
            realRenderers[i].shadowCastingMode = shadowsOn
                ? (_originalShadowModes != null ? _originalShadowModes[i] : ShadowCastingMode.On)
                : ShadowCastingMode.Off;
        }
    }

    void RestoreShadowModes()
    {
        if (realRenderers == null || _originalShadowModes == null) return;
        for (int i = 0; i < realRenderers.Length; i++)
            if (realRenderers[i])
                realRenderers[i].shadowCastingMode = _originalShadowModes[i];
    }
}
