using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Select a root GameObject, then Tools > Dissolve > Swap Materials to StandardDissolve
public static class SwapToDissolveShader
{
    const string DissolveShaderName = "Custom/StandardDissolve";

    [MenuItem("Tools/Dissolve/Swap Materials to StandardDissolve")]
    static void SwapSelected()
    {
        var root = Selection.activeGameObject;
        if (root == null) { Debug.LogError("Select a GameObject first."); return; }

        var dissolveShader = Shader.Find(DissolveShaderName);
        if (dissolveShader == null)
        {
            Debug.LogError($"Shader '{DissolveShaderName}' not found — ensure StandardDissolve.shader is in the project.");
            return;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        var seen      = new HashSet<int>();
        int swapped   = 0;
        int skipped   = 0;

        foreach (var r in renderers)
        {
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null || seen.Contains(mat.GetInstanceID())) continue;
                seen.Add(mat.GetInstanceID());

                if (mat.shader == dissolveShader) { skipped++; continue; }

                Undo.RecordObject(mat, "Swap Shader to StandardDissolve");
                mat.shader = dissolveShader;
                EditorUtility.SetDirty(mat);
                swapped++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SwapToDissolveShader] Swapped {swapped} material(s) to '{DissolveShaderName}', {skipped} already on target shader. Root: '{root.name}'");
    }

    [MenuItem("Tools/Dissolve/Swap Materials to StandardDissolve", validate = true)]
    static bool ValidateSwapSelected() => Selection.activeGameObject != null;
}
