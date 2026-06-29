using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class UnusedTextureCleaner : EditorWindow
{
    [MenuItem("Tools/Clean Unused Textures in Folder")]
    static void Open() => GetWindow<UnusedTextureCleaner>("Unused Texture Cleaner");

    List<string> _unused = new List<string>();
    string       _folder = "";
    Vector2      _scroll;
    bool         _scanned;

    void OnGUI()
    {
        EditorGUILayout.Space(4);
        GUILayout.Label("Unused Texture Cleaner", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(_folder == "" ? "No folder selected" : _folder, EditorStyles.miniLabel);
        if (GUILayout.Button("Choose Folder", GUILayout.Width(110))) PickFolder();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        GUI.enabled = _folder != "";
        if (GUILayout.Button("Scan")) Scan();
        GUI.enabled = true;

        if (!_scanned) return;

        EditorGUILayout.Space(6);

        if (_unused.Count == 0)
        {
            EditorGUILayout.HelpBox("No unused textures found in the selected folder.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"Found {_unused.Count} unused texture(s):", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        foreach (var path in _unused)
            EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6);
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button($"Delete All {_unused.Count} Textures", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Delete Unused Textures",
                $"Permanently delete {_unused.Count} texture(s)?\nThis cannot be undone.",
                "Delete All", "Cancel"))
            {
                DeleteAll();
            }
        }
        GUI.backgroundColor = Color.white;
    }

    void PickFolder()
    {
        string abs = EditorUtility.OpenFolderPanel("Select Folder to Search", "Assets", "");
        if (string.IsNullOrEmpty(abs)) return;

        string projectPath = Application.dataPath;
        if (!abs.StartsWith(projectPath))
        {
            EditorUtility.DisplayDialog("Error", "Please select a folder inside the Assets directory.", "OK");
            return;
        }

        _folder  = "Assets" + abs.Substring(projectPath.Length);
        _scanned = false;
        _unused.Clear();
    }

    void Scan()
    {
        // Collect all textures referenced by any material in the project
        var usedTextures = new HashSet<string>();

        var allMaterials = AssetDatabase.FindAssets("t:Material", new[] { "Assets" })
            .Select(g => AssetDatabase.GUIDToAssetPath(g));

        foreach (var matPath in allMaterials)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) continue;

            var so = new SerializedObject(mat);
            var prop = so.GetIterator();
            while (prop.NextVisible(true))
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                var obj = prop.objectReferenceValue;
                if (obj is Texture)
                    usedTextures.Add(AssetDatabase.GetAssetPath(obj));
            }
        }

        // Find all textures in the selected folder not in the used set
        _unused = AssetDatabase.FindAssets("t:Texture", new[] { _folder })
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Where(p => !string.IsNullOrEmpty(p) && !usedTextures.Contains(p))
            .OrderBy(p => p)
            .ToList();

        _scanned = true;
        Repaint();
    }

    void DeleteAll()
    {
        foreach (var path in _unused)
            AssetDatabase.DeleteAsset(path);

        AssetDatabase.Refresh();
        int count = _unused.Count;
        _unused.Clear();
        _scanned = false;
        Debug.Log($"[UnusedTextureCleaner] Deleted {count} texture(s).");
        EditorUtility.DisplayDialog("Done", $"Deleted {count} texture(s).", "OK");
    }
}
