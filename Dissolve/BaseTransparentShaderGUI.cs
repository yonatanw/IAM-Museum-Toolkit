using UnityEditor;

public class BaseTransparentShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
    {
        foreach (var prop in props)
            editor.ShaderProperty(prop, prop.displayName);

        EditorGUILayout.Space(4);
        editor.RenderQueueField();
    }
}
