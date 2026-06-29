using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnusedMaterialCleaner
{
    [MenuItem("Tools/Clean Unused Materials in Open Scene")]
    static void Run()
    {
        // Ask user to pick a folder
        string folder = EditorUtility.OpenFolderPanel("Select Folder to Search", "Assets", "");
        if (string.IsNullOrEmpty(folder)) return;

        // Convert absolute path to relative Assets/ path
        string projectPath = Application.dataPath; // .../Assets
        if (!folder.StartsWith(projectPath))
        {
            EditorUtility.DisplayDialog("Error", "Please select a folder inside the Assets directory.", "OK");
            return;
        }
        string relativePath = "Assets" + folder.Substring(projectPath.Length);

        // Collect all materials used by renderers in the open scene
        var used = new HashSet<string>();

        var renderers = Object.FindObjectsOfType<Renderer>();
        foreach (var r in renderers)
            foreach (var mat in r.sharedMaterials)
                if (mat != null)
                    used.Add(AssetDatabase.GetAssetPath(mat));

        // Find all material assets in the selected folder
        var allMaterials = AssetDatabase.FindAssets("t:Material", new[] { relativePath })
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        var unused = allMaterials.Where(p => !used.Contains(p)).ToList();

        if (unused.Count == 0)
        {
            EditorUtility.DisplayDialog("Clean Unused Materials", "No unused materials found.", "OK");
            return;
        }

        int deleted = 0;
        int skipped = 0;

        foreach (var path in unused)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Skip Unity default/built-in materials
            if (IsBuiltIn(mat, path)) continue;

            bool confirm = EditorUtility.DisplayDialog(
                "Delete Unused Material?",
                $"{path}\n\nShader: {mat.shader.name}\n\nDelete this material?",
                "Delete", "Skip"
            );

            if (confirm)
            {
                AssetDatabase.DeleteAsset(path);
                deleted++;
            }
            else
            {
                skipped++;
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Clean Unused Materials",
            $"Done.\nDeleted: {deleted}\nSkipped: {skipped}",
            "OK"
        );
    }

    static bool IsBuiltIn(Material mat, string path)
    {
        // Skip materials with no valid asset path (built-in)
        if (string.IsNullOrEmpty(path)) return true;
        // Skip Unity default materials by name
        string[] builtInNames = { "Default-Material", "Default-Diffuse", "Default-Line",
                                   "Default-Particle", "Default-Skybox", "Default-Terrain",
                                   "Sprites-Default", "Sprites-Mask", "UI/Default" };
        return builtInNames.Any(n => mat.name == n || path.Contains(n));
    }
}
