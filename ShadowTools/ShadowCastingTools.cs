using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

// ─────────────────────────────────────────────
//  Persistent data  (stores paths, not scene refs)
// ─────────────────────────────────────────────

public class ShadowCasterData : ScriptableObject
{
    public List<string> shadowCasterPaths = new List<string>();
}

// ─────────────────────────────────────────────
//  Editor Window
// ─────────────────────────────────────────────

public class ShadowCastingTools : EditorWindow
{
    private ShadowCasterData      data;
    private List<GameObject>      resolved = new List<GameObject>(); // in-memory working list
    private const string          DataAssetPath = "Assets/Editor/ShadowCasterData.asset";
    private Vector2               scrollPos;

    private static readonly Color HeaderColor  = new Color(0.13f, 0.13f, 0.16f);
    private static readonly Color RowColorA    = new Color(0.20f, 0.20f, 0.22f);
    private static readonly Color RowColorB    = new Color(0.22f, 0.22f, 0.25f);
    private static readonly Color AccentGreen  = new Color(0.30f, 0.80f, 0.40f);
    private static readonly Color AccentRed    = new Color(0.90f, 0.30f, 0.30f);
    private static readonly Color AccentOrange = new Color(1.00f, 0.60f, 0.20f);

    // ── Menu items ────────────────────────────

    [MenuItem("Tools/Shadows/Shadow Caster Manager")]
    public static void OpenWindow()
    {
        var w = GetWindow<ShadowCastingTools>("Shadow Casters");
        w.minSize = new Vector2(420, 320);
    }

    [MenuItem("Tools/Shadows/Disable Cast Shadows on All")]
    static void DisableAllCastShadows()
    {
        var mesh    = FindObjectsOfType<MeshRenderer>();
        var skinned = FindObjectsOfType<SkinnedMeshRenderer>();
        Undo.RecordObjects(mesh,    "Disable Cast Shadows on All");
        Undo.RecordObjects(skinned, "Disable Cast Shadows on All");
        foreach (var r in mesh)    r.shadowCastingMode = ShadowCastingMode.Off;
        foreach (var r in skinned) r.shadowCastingMode = ShadowCastingMode.Off;
        Debug.Log($"[Shadows] Disabled: {mesh.Length} MeshRenderers, {skinned.Length} SkinnedMeshRenderers.");
    }

    [MenuItem("Tools/Shadows/Enable Cast Shadows on Selected + Children")]
    static void EnableSelected()  => SetOnSelected(ShadowCastingMode.On,  "Enable Cast Shadows on Selected + Children");

    [MenuItem("Tools/Shadows/Disable Cast Shadows on Selected + Children")]
    static void DisableSelected() => SetOnSelected(ShadowCastingMode.Off, "Disable Cast Shadows on Selected + Children");

    // ── Lifecycle ─────────────────────────────

    private void OnEnable()
    {
        LoadOrCreateData();
        RefreshFromScene();
    }

    private void OnDisable() => Save();

    private void LoadOrCreateData()
    {
        data = AssetDatabase.LoadAssetAtPath<ShadowCasterData>(DataAssetPath);
        if (data != null) return;
        data = CreateInstance<ShadowCasterData>();
        AssetDatabase.CreateAsset(data, DataAssetPath);
        AssetDatabase.SaveAssets();
    }

    private void RefreshFromScene()
    {
        resolved.Clear();
        var seen = new HashSet<GameObject>();
        foreach (var r in FindObjectsOfType<MeshRenderer>())
        {
            if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
            var root = r.transform.root.gameObject;
            if (seen.Add(root)) resolved.Add(root);
        }
        foreach (var r in FindObjectsOfType<SkinnedMeshRenderer>())
        {
            if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
            var root = r.transform.root.gameObject;
            if (seen.Add(root)) resolved.Add(root);
        }
        Save();
    }

    // Resolve stored path strings back to GameObjects (including inactive)
    private void ResolvePaths()
    {
        resolved.Clear();
        foreach (var path in data.shadowCasterPaths)
            resolved.Add(FindByPath(path));
    }

    // Sync in-memory list back to path strings, then write to disk
    private void Save()
    {
        if (data == null) return;
        data.shadowCasterPaths.Clear();
        foreach (var go in resolved)
            data.shadowCasterPaths.Add(go != null ? GetPath(go) : "");
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }

    // ── GUI ───────────────────────────────────

    private void OnGUI()
    {
        if (data == null) { LoadOrCreateData(); ResolvePaths(); return; }

        DrawHeader();
        EditorGUILayout.Space(6);
        DrawList();
        EditorGUILayout.Space(6);
        DrawApplyButton();
    }

    private void DrawHeader()
    {
        EditorGUI.DrawRect(new Rect(0, 0, position.width, 46), HeaderColor);
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 15,
            alignment = TextAnchor.MiddleLeft,
            normal    = { textColor = Color.white }
        };
        GUI.Label(new Rect(14, 10, position.width - 28, 30), "Shadow Caster Manager", style);
        GUILayout.Space(50);
    }

    private void DrawList()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Shadow Casters", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = AccentOrange;
        if (GUILayout.Button("↺ Refresh", GUILayout.Width(90)))
            RefreshFromScene();
        GUILayout.Space(4);
        GUI.backgroundColor = AccentGreen;
        if (GUILayout.Button("+ Add Selected", GUILayout.Width(120)))
            AddSelected();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        var noteStyle = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.5f, 0.5f, 0.6f) } };
        GUILayout.Label("  Listed objects (and their children) will be the only shadow casters on Apply.", noteStyle);
        EditorGUILayout.Space(4);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        int toRemove = -1;
        for (int i = 0; i < resolved.Count; i++)
        {
            Color bg  = i % 2 == 0 ? RowColorA : RowColorB;
            Rect  row = EditorGUILayout.BeginHorizontal(GUILayout.Height(24));
            EditorGUI.DrawRect(new Rect(row.x, row.y, position.width, 24), bg);
            GUILayout.Space(6);

            if (resolved[i] != null)
            {
                EditorGUI.BeginChangeCheck();
                var newGo = (GameObject)EditorGUILayout.ObjectField(resolved[i], typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                    resolved[i] = newGo;
            }
            else
            {
                var warnStyle = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = AccentOrange } };
                string label = i < data.shadowCasterPaths.Count && !string.IsNullOrEmpty(data.shadowCasterPaths[i])
                    ? $"⚠ Not found: {data.shadowCasterPaths[i]}"
                    : "⚠ Missing reference";
                GUILayout.Label(label, warnStyle);
            }

            GUILayout.FlexibleSpace();
            GUI.backgroundColor = AccentRed;
            if (GUILayout.Button("✕", GUILayout.Width(26))) toRemove = i;
            GUI.backgroundColor = Color.white;
            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();
        }

        if (toRemove >= 0)
        {
            resolved.RemoveAt(toRemove);
            Save();
        }

        if (resolved.Count == 0)
            EditorGUILayout.HelpBox("No objects yet — select objects in the scene and click '+ Add Selected'.", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void DrawApplyButton()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = AccentGreen;
        var style = new GUIStyle(GUI.skin.button)
            { fontSize = 13, fontStyle = FontStyle.Bold, fixedHeight = 36 };
        if (GUILayout.Button("Apply  —  Disable All, Then Enable Listed + Children", style))
            Apply();
        GUI.backgroundColor = Color.white;
        var note = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.5f, 0.7f, 0.5f) } };
        GUILayout.Label("✓ Undoable. Disables shadows on every renderer, then enables only listed objects.", note);
        EditorGUILayout.EndVertical();
    }

    // ── Actions ───────────────────────────────

    private void AddSelected()
    {
        foreach (var go in Selection.gameObjects)
        {
            if (!resolved.Contains(go))
                resolved.Add(go);
        }
        Save();
    }

    private void Apply()
    {
        var allMesh    = FindObjectsOfType<MeshRenderer>();
        var allSkinned = FindObjectsOfType<SkinnedMeshRenderer>();
        Undo.RecordObjects(allMesh,    "Apply Shadow Casters");
        Undo.RecordObjects(allSkinned, "Apply Shadow Casters");

        foreach (var r in allMesh)    r.shadowCastingMode = ShadowCastingMode.Off;
        foreach (var r in allSkinned) r.shadowCastingMode = ShadowCastingMode.Off;

        int enabled = 0;
        foreach (var go in resolved)
        {
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                enabled++;
            }
        }

        Save();
        Debug.Log($"[Shadows] Applied: all disabled, {enabled} renderers enabled across {resolved.Count} listed objects.");
    }

    // ── Path helpers ──────────────────────────

    private static string GetPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null) { path = t.name + "/" + path; t = t.parent; }
        return path;
    }

    // Finds by path including inactive objects
    private static GameObject FindByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // Fast path for active objects
        var go = GameObject.Find(path);
        if (go != null) return go;

        // Fall back to searching all transforms (handles inactive)
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.hideFlags != HideFlags.None) continue;
            if (GetPath(t.gameObject) == path) return t.gameObject;
        }
        return null;
    }

    // ── Static helper ─────────────────────────

    static void SetOnSelected(ShadowCastingMode mode, string undoName)
    {
        var renderers = new HashSet<Renderer>();
        foreach (var go in Selection.gameObjects)
            foreach (var r in go.GetComponentsInChildren<Renderer>(includeInactive: true))
                renderers.Add(r);

        var array = new Renderer[renderers.Count];
        renderers.CopyTo(array);
        Undo.RecordObjects(array, undoName);
        foreach (var r in array) r.shadowCastingMode = mode;
        Debug.Log($"[Shadows] Set to {mode}: {array.Length} renderers.");
    }
}
