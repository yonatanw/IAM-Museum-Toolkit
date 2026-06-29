using UnityEditor;
using UnityEngine;

public class HologramShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
    {
        base.OnGUI(editor, props);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Render Options", EditorStyles.boldLabel);

        var mat = (Material)editor.target;

        Toggle(editor, mat, "Cull Back Faces",
            tooltip:   "Hide interior/back faces — leave off if the mesh has no volume (e.g. a flat plane).",
            propName:  "_Cull",
            onValue:   2f,   // CullMode.Back
            offValue:  0f);  // CullMode.Off

        Toggle(editor, mat, "Frontmost Surface Only",
            tooltip:   "Write depth so overlapping layers of the same hologram mesh occlude each other — only the closest surface draws.",
            propName:  "_ZWrite",
            onValue:   1f,   // ZWrite On
            offValue:  0f);  // ZWrite Off
    }

    static void Toggle(MaterialEditor editor, Material mat,
                       string label, string tooltip,
                       string propName, float onValue, float offValue)
    {
        bool current = mat.GetFloat(propName) > (onValue + offValue) / 2f;
        EditorGUI.BeginChangeCheck();
        bool next = EditorGUILayout.Toggle(new GUIContent(label, tooltip), current);
        if (EditorGUI.EndChangeCheck())
            foreach (Object t in editor.targets)
                ((Material)t).SetFloat(propName, next ? onValue : offValue);
    }
}
