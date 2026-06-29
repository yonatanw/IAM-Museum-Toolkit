using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class SkyWallFadeIn : MonoBehaviour
{
    [Tooltip("Frames per second used to convert frame values to seconds")]
    public int fps = 30;

    [Tooltip("Delay before the fade starts (frames)")]
    public int startDelayFrames = 0;

    [Tooltip("Duration of the noise dissolve fade-in (frames)")]
    public int fadeInFrames = 60;

    float StartDelay    => startDelayFrames / (float)Mathf.Max(fps, 1);
    float FadeInDuration => fadeInFrames    / (float)Mathf.Max(fps, 1);

    Material _mat;
    float    _elapsed;
    bool     _running;

    void OnEnable()
    {
        _mat     = GetComponent<Renderer>().material;
        _elapsed = -StartDelay;
        _running = true;
        _mat.SetFloat("_DissolveProgress", 0f);
    }

    void Update()
    {
        if (!_running) return;

        _elapsed += Time.deltaTime;
        if (_elapsed < 0f) return;

        float progress = Mathf.Clamp01(_elapsed / FadeInDuration);
        _mat.SetFloat("_DissolveProgress", progress);

        if (progress >= 1f)
            _running = false;
    }

    public void Trigger()
    {
        _elapsed = -StartDelay;
        _running = true;
        _mat.SetFloat("_DissolveProgress", 0f);
    }
}
