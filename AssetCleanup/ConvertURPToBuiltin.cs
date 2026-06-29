using UnityEngine;
using UnityEditor;

public class ConvertURPToBuiltin
{
    [MenuItem("Tools/Convert URP Materials to Built-in Standard")]
    static void Convert()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material");
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null) continue;

            // When URP is uninstalled, materials that used URP shaders have a missing
            // shader reference — detect this via SerializedObject
            SerializedObject so = new SerializedObject(mat);
            SerializedProperty shaderProp = so.FindProperty("m_Shader");
            bool shaderMissing = shaderProp.objectReferenceValue == null
                                 && shaderProp.objectReferenceInstanceIDValue != 0;
            bool isURPShader = mat.shader != null && mat.shader.name.Contains("Universal Render Pipeline");

            if (!shaderMissing && !isURPShader) continue;

            Texture baseMap   = mat.HasProperty("_BaseMap")      ? mat.GetTexture("_BaseMap")    : null;
            Color   baseColor = mat.HasProperty("_BaseColor")     ? mat.GetColor("_BaseColor")    : Color.white;
            float   metallic  = mat.HasProperty("_Metallic")      ? mat.GetFloat("_Metallic")     : 0f;
            float   smooth    = mat.HasProperty("_Smoothness")    ? mat.GetFloat("_Smoothness")   : 0.5f;
            Texture bumpMap   = mat.HasProperty("_BumpMap")       ? mat.GetTexture("_BumpMap")    : null;
            Color   emission  = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor"): Color.black;

            mat.shader = Shader.Find("Standard");

            if (baseMap != null) mat.SetTexture("_MainTex", baseMap);
            mat.SetColor("_Color", baseColor);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smooth);
            if (bumpMap != null) mat.SetTexture("_BumpMap", bumpMap);
            mat.SetColor("_EmissionColor", emission);

            EditorUtility.SetDirty(mat);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Converted {count} URP materials to Standard.");
    }
}
