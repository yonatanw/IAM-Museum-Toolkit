using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Two-phase dissolve for Custom/BaseTransparent materials.
//   Reveal — sweeps _Threshold 0 -> settleAt with ease-in-out. Noise already moving.
//   Hold   — pushes settleAt / edgeWidth / holdNoiseSpeed live every frame.
//   Hologram fade — fades _Alpha and _GlowStrength to 0 on assigned hologram renderers.
public class DissolveController : MonoBehaviour
{
    [Tooltip("Leave empty to auto-collect from all children.")]
    public Renderer[] renderers;

    [Header("Reveal")]
    public float startDelay     = 0f;
    public float revealDuration = 2f;
    [Range(0.5f, 4f)]
    public float ease           = 2f;

    [Header("Hold — live controls")]
    [Range(0, 1)]
    [Tooltip("Fraction of the object that is visible (0 = invisible, 0.5 = Dalmatian, 1 = solid)")]
    public float settleAt       = 0.5f;

    [Range(0.01f, 0.4f)]
    [Tooltip("Thickness of the glowing border at the noise edge")]
    public float edgeWidth      = 0.08f;

    [Range(0, 2)]
    [Tooltip("How fast the noise pattern evolves")]
    public float holdNoiseSpeed = 0.3f;

    [Tooltip("Run reveal automatically when the object is enabled.")]
    public bool playOnEnable    = true;

    [Header("Hologram Fade")]
    [Tooltip("Root of the hologram object tree — all child renderers with _GlowStrength will be faded.")]
    public GameObject hologramRoot;
    [Tooltip("Seconds after the reveal starts before the hologram begins fading.")]
    public float hologramFadeDelay    = 0f;
    [Tooltip("How long the hologram takes to fade.")]
    public float hologramFadeDuration = 1f;

    [Header("Hologram — Start Values")]
    [Range(0.1f, 8f)]  public float holoStartFresnelPower   = 2f;
    [Range(0,    1)]   public float holoStartAlpha           = 0.8f;
    public Color                    holoStartTint            = new Color(0f, 0.8f, 1f, 1f);
    [Range(0,    1)]   public float holoStartFlickerStrength = 0.08f;
    [Range(0,    4)]   public float holoStartGlow            = 1.8f;
    [Range(1,    50)]  public float holoStartScanlineScale   = 6f;
    [Range(0,    1)]   public float holoStartScanlineMin     = 0.4f;
    [Range(-5,   5)]   public float holoStartScanlineSpeed  = 0.5f;

    [Header("Hologram — End Values")]
    [Range(0.1f, 8f)]  public float holoEndFresnelPower   = 2f;
    [Range(0,    1)]   public float holoEndAlpha           = 0f;
    public Color                    holoEndTint            = new Color(0f, 0.8f, 1f, 1f);
    [Range(0,    1)]   public float holoEndFlickerStrength = 0f;
    [Range(0,    4)]   public float holoEndGlow            = 0f;
    [Range(1,    50)]  public float holoEndScanlineScale   = 6f;
    [Range(0,    1)]   public float holoEndScanlineMin     = 1f;
    [Range(-5,   5)]   public float holoEndScanlineSpeed   = 0f;

    Material[] _mats;
    Material[] _hologramMats;
    bool       _inHold;
    bool       _holoFading;
    bool       _holoFadeDone;

    void OnEnable()
    {
        GatherMaterials();
        GatherHologramMaterials();
        _inHold = false;
        _holoFading = false;
        _holoFadeDone = false;
        Set("_Threshold",      0f);
        Set("_EdgeWidth",      edgeWidth);
        Set("_NoiseStartTime", Time.time);
        Set("_NoiseSpeed",     holdNoiseSpeed);
        PushHoloStart();
        if (playOnEnable)
        {
            StartCoroutine(RunReveal());
            if (_hologramMats != null && _hologramMats.Length > 0)
                StartCoroutine(FadeHologram());
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();
        _inHold = false;
        _holoFading = false;
        _holoFadeDone = false;
    }

    void Update()
    {
        if (_inHold)
        {
            Set("_Threshold",  settleAt);
            Set("_EdgeWidth",  edgeWidth);
            Set("_NoiseSpeed", holdNoiseSpeed);
        }

        if (!_holoFading && !_holoFadeDone)
            PushHoloStart();
        else if (_holoFadeDone)
            PushHoloEnd();
    }

    public void Trigger()
    {
        StopAllCoroutines();
        GatherMaterials();
        GatherHologramMaterials();
        _inHold = false;
        _holoFading = false;
        _holoFadeDone = false;
        Set("_Threshold",      0f);
        Set("_EdgeWidth",      edgeWidth);
        Set("_NoiseStartTime", Time.time);
        Set("_NoiseSpeed",     holdNoiseSpeed);
        PushHoloStart();
        StartCoroutine(RunReveal());
        if (_hologramMats != null && _hologramMats.Length > 0)
            StartCoroutine(FadeHologram());
    }

    IEnumerator RunReveal()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        float elapsed = 0f;
        while (elapsed < revealDuration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / revealDuration);
            float eased = t < 0.5f
                ? 0.5f * Mathf.Pow(2f * t,         ease)
                : 1f - 0.5f * Mathf.Pow(2f*(1f-t), ease);
            Set("_Threshold", Mathf.Lerp(0f, settleAt, eased));
            yield return null;
        }

        _inHold = true;
    }

    IEnumerator FadeHologram()
    {
        if (startDelay + hologramFadeDelay > 0f)
            yield return new WaitForSeconds(startDelay + hologramFadeDelay);

        _holoFading = true;

        float elapsed = 0f;
        float dur = Mathf.Max(hologramFadeDuration, 0.001f);
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            SetHolo("_FresnelPower",    Mathf.Lerp(holoStartFresnelPower,    holoEndFresnelPower,    t));
            SetHolo("_Alpha",           Mathf.Lerp(holoStartAlpha,           holoEndAlpha,           t));
            SetHoloColor("_Color",      Color.Lerp(holoStartTint,            holoEndTint,            t));
            SetHolo("_FlickerStrength", Mathf.Lerp(holoStartFlickerStrength, holoEndFlickerStrength, t));
            SetHolo("_GlowStrength",    Mathf.Lerp(holoStartGlow,            holoEndGlow,            t));
            SetHolo("_ScanlineScale",   Mathf.Lerp(holoStartScanlineScale,   holoEndScanlineScale,   t));
            SetHolo("_ScanlineMin",     Mathf.Lerp(holoStartScanlineMin,     holoEndScanlineMin,     t));
            SetHolo("_ScanlineSpeed",   Mathf.Lerp(holoStartScanlineSpeed,   holoEndScanlineSpeed,   t));
            yield return null;
        }

        _holoFading = false;
        _holoFadeDone = true;
    }

    void GatherMaterials()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(includeInactive: true);

        var list    = new List<Material>();
        int missing = 0;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mat = r.material;
            if (mat.HasProperty("_Threshold"))
                list.Add(mat);
            else
                missing++;
        }
        _mats = list.ToArray();

        if (_mats.Length == 0)
            Debug.LogError($"[DissolveController] No dissolve materials on '{name}'. {missing} renderer(s) use the wrong shader.", this);
        else if (missing > 0)
            Debug.LogWarning($"[DissolveController] '{name}': {_mats.Length} OK, {missing} skipped (wrong shader).", this);
    }

    void PushHoloStart()
    {
        SetHolo("_FresnelPower",    holoStartFresnelPower);
        SetHolo("_Alpha",           holoStartAlpha);
        SetHoloColor("_Color",      holoStartTint);
        SetHolo("_FlickerStrength", holoStartFlickerStrength);
        SetHolo("_GlowStrength",    holoStartGlow);
        SetHolo("_ScanlineScale",   holoStartScanlineScale);
        SetHolo("_ScanlineMin",     holoStartScanlineMin);
        SetHolo("_ScanlineSpeed",   holoStartScanlineSpeed);
    }

    void PushHoloEnd()
    {
        SetHolo("_FresnelPower",    holoEndFresnelPower);
        SetHolo("_Alpha",           holoEndAlpha);
        SetHoloColor("_Color",      holoEndTint);
        SetHolo("_FlickerStrength", holoEndFlickerStrength);
        SetHolo("_GlowStrength",    holoEndGlow);
        SetHolo("_ScanlineScale",   holoEndScanlineScale);
        SetHolo("_ScanlineMin",     holoEndScanlineMin);
        SetHolo("_ScanlineSpeed",   holoEndScanlineSpeed);
    }

    void SetHolo(string prop, float value)
    {
        if (_hologramMats == null) return;
        foreach (var m in _hologramMats)
            if (m != null && m.HasProperty(prop))
                m.SetFloat(prop, value);
    }

    void SetHoloColor(string prop, Color value)
    {
        if (_hologramMats == null) return;
        foreach (var m in _hologramMats)
            if (m != null && m.HasProperty(prop))
                m.SetColor(prop, value);
    }

    void GatherHologramMaterials()
    {
        if (hologramRoot == null)
        {
            _hologramMats = new Material[0];
            return;
        }

        var list = new List<Material>();
        foreach (var r in hologramRoot.GetComponentsInChildren<Renderer>(includeInactive: true))
        {
            if (r == null) continue;
            var mat = r.material;
            if (mat.HasProperty("_GlowStrength"))
                list.Add(mat);
        }
        _hologramMats = list.ToArray();

        if (_hologramMats.Length == 0)
            Debug.LogWarning($"[DissolveController] No Hologram materials found under '{hologramRoot.name}'.", this);
    }

    void Set(string prop, float value)
    {
        if (_mats == null) return;
        foreach (var m in _mats)
            if (m != null && m.HasProperty(prop))
                m.SetFloat(prop, value);
    }
}
