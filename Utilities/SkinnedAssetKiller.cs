using UnityEngine;

public class SkinnedAssetKiller : MonoBehaviour
{
    // Called by Animation Event — path is relative to this transform (the Animator root).
    // Destroys the child GameObject at that path, freeing all its memory.
    public void Kill(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("[SkinnedAssetKiller] Kill called with empty path — skipped.");
            return;
        }

        Transform target = transform.Find(path);
        if (target == null)
        {
            Debug.LogWarning($"[SkinnedAssetKiller] No child found at path: '{path}'");
            return;
        }

        Debug.Log($"[SkinnedAssetKiller] Killing '{path}'");
        Destroy(target.gameObject);
    }
}
