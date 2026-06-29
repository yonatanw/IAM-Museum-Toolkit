using UnityEditor;
using UnityEngine;

public class ApplyMaterialToTree : EditorWindow
{
    GameObject root;
    Material   material;

    [MenuItem("Tools/Apply Material To Tree")]
    static void Open() => GetWindow<ApplyMaterialToTree>("Apply Material To Tree");

    void OnGUI()
    {
        root     = (GameObject)EditorGUILayout.ObjectField("Root Object", root,     typeof(GameObject), true);
        material = (Material)  EditorGUILayout.ObjectField("Material",    material, typeof(Material),   false);

        EditorGUI.BeginDisabledGroup(root == null || material == null);
        if (GUILayout.Button("Apply to All Children"))
            Apply();
        EditorGUI.EndDisabledGroup();
    }

    void Apply()
    {
        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        Undo.RecordObjects(renderers, "Apply Material To Tree");

        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = material;
            r.sharedMaterials = mats;
        }

        Debug.Log($"Applied {material.name} to {renderers.Length} renderer(s) under {root.name}.");
    }
}
