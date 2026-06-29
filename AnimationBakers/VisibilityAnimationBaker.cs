using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;

// Data classes live in VisibilityBakerData.cs (filename must match class name for ScriptableObject)

public class VisibilityAnimationBaker : EditorWindow
{
    // ── Persistent object data ────────────────
    private VisibilityBakerData data;
    private const string DataAssetPath = "Assets/VisibilityBakerData.asset";

    // ── Clip stored in EditorPrefs (survives clip deletion gracefully) ──
    private AnimationClip _targetClip;
    private const string  ClipPrefKey = "VisibilityBaker_ClipGUID";

    private Vector2 scrollPos;

    // Colors
    private static readonly Color HeaderColor  = new Color(0.13f, 0.13f, 0.16f);
    private static readonly Color RowColorA    = new Color(0.20f, 0.20f, 0.22f);
    private static readonly Color RowColorB    = new Color(0.22f, 0.22f, 0.25f);
    private static readonly Color AccentGreen  = new Color(0.30f, 0.80f, 0.40f);
    private static readonly Color AccentRed    = new Color(0.90f, 0.30f, 0.30f);
    private static readonly Color AccentBlue   = new Color(0.30f, 0.60f, 1.00f);
    private static readonly Color AccentOrange = new Color(1.00f, 0.60f, 0.20f);

    // ─────────────────────────────────────────
    [MenuItem("Tools/Visibility Animation Baker")]
    public static void OpenWindow()
    {
        var w = GetWindow<VisibilityAnimationBaker>("Visibility Baker");
        w.minSize = new Vector2(620, 420);
    }

    private void OnEnable()
    {
        LoadOrCreateData();
        LoadClipFromPrefs();
        ResolveEntryPaths();
    }

    private void OnDisable() => SaveData();

    // ── Data loading ──────────────────────────

    private void LoadOrCreateData()
    {
        data = AssetDatabase.LoadAssetAtPath<VisibilityBakerData>(DataAssetPath);
        if (data != null) return;

        data = CreateInstance<VisibilityBakerData>();
        AssetDatabase.CreateAsset(data, DataAssetPath);
        AssetDatabase.SaveAssets();
    }

    // ── Clip: save/load via GUID so deletion just gives null, not corruption ──

    private void LoadClipFromPrefs()
    {
        string guid = EditorPrefs.GetString(ClipPrefKey, "");
        if (string.IsNullOrEmpty(guid)) return;

        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (!string.IsNullOrEmpty(path))
            _targetClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }

    private void SaveData()
    {
        if (data == null) return;
        foreach (var entry in data.entries)
            if (entry.targetObject != null)
                entry.scenePath = GetScenePath(entry.targetObject);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }

    private void ResolveEntryPaths()
    {
        if (data == null) return;
        foreach (var entry in data.entries)
        {
            if (entry.targetObject != null) continue;
            if (string.IsNullOrEmpty(entry.scenePath)) continue;
            entry.targetObject = FindByScenePath(entry.scenePath);
        }
    }

    private void RefreshFromClip()
    {
        if (_targetClip == null) return;
        data.entries.Clear();
        var animators = FindObjectsOfType<Animator>(true);
        foreach (var binding in AnimationUtility.GetCurveBindings(_targetClip))
        {
            if (binding.propertyName != "m_IsActive") continue;
            GameObject found = null;
            foreach (var anim in animators)
            {
                Transform t = string.IsNullOrEmpty(binding.path)
                    ? anim.transform
                    : anim.transform.Find(binding.path);
                if (t != null) { found = t.gameObject; break; }
            }
            if (found == null)
                found = FindByScenePath(binding.path);
            if (found == null) continue;

            var entry = new VisibilityObjectEntry
            {
                targetObject = found,
                objectName   = found.name,
                scenePath    = GetScenePath(found)
            };

            // Read existing on/off ranges from the curve
            var curve = AnimationUtility.GetEditorCurve(_targetClip, binding);
            int pendingOn = -1;
            for (int k = 0; k < curve.length; k++)
            {
                float prev  = k > 0 ? curve[k - 1].value : 0f;
                float val   = curve[k].value;
                int   frame = Mathf.RoundToInt(curve[k].time * data.frameRate);
                if (prev < 0.5f && val > 0.5f)
                    pendingOn = frame;
                else if (prev > 0.5f && val < 0.5f && pendingOn >= 0)
                {
                    entry.ranges.Add(new VisibilityRange { frameOn = pendingOn, frameOff = frame });
                    pendingOn = -1;
                }
            }

            data.entries.Add(entry);
        }
        SaveData();
    }

    private static string GetScenePath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null) { path = t.name + "/" + path; t = t.parent; }
        return path;
    }

    private static GameObject FindByScenePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.hideFlags != HideFlags.None) continue;
            if (GetScenePath(t.gameObject) == path) return t.gameObject;
        }
        return null;
    }

    private void SaveClipToPrefs()
    {
        if (_targetClip == null)
        {
            EditorPrefs.DeleteKey(ClipPrefKey);
            return;
        }
        string path = AssetDatabase.GetAssetPath(_targetClip);
        string guid = AssetDatabase.AssetPathToGUID(path);
        EditorPrefs.SetString(ClipPrefKey, guid);
    }

    // ── Main GUI ──────────────────────────────

    private void OnGUI()
    {
        if (data == null) { LoadOrCreateData(); return; }

        DrawHeader();
        DrawGlobalSettings();
        EditorGUILayout.Space(6);
        DrawObjectList();
        EditorGUILayout.Space(6);
        DrawBakeButton();

        if (GUI.changed)
            EditorUtility.SetDirty(data);
    }

    // ── Header ────────────────────────────────

    private void DrawHeader()
    {
        EditorGUI.DrawRect(new Rect(0, 0, position.width, 46), HeaderColor);
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 15,
            alignment = TextAnchor.MiddleLeft,
            normal    = { textColor = Color.white }
        };
        GUI.Label(new Rect(14, 10, position.width - 28, 30), "🎬  Visibility Animation Baker", style);
        GUILayout.Space(50);
    }

    // ── Global Settings ───────────────────────

    private void DrawGlobalSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        // Clip field — stored in EditorPrefs, not in the ScriptableObject
        EditorGUI.BeginChangeCheck();
        var newClip = (AnimationClip)EditorGUILayout.ObjectField(
            "Target Animation Clip", _targetClip, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck())
        {
            _targetClip = newClip;
            SaveClipToPrefs();          // persist GUID independently
        }

        // Small note so intent is clear
        var noteStyle = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.5f, 0.6f, 0.5f) } };
        GUILayout.Label("  ↑ Changing or deleting this clip will never affect your object list below.", noteStyle);

        EditorGUILayout.Space(4);

        // Frame rate — stored in ScriptableObject
        EditorGUI.BeginChangeCheck();
        int newFps = EditorGUILayout.IntField("Frame Rate (fps)", data.frameRate);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(data, "Change FPS");
            data.frameRate = Mathf.Max(1, newFps);
            EditorUtility.SetDirty(data);
        }

        EditorGUILayout.EndVertical();
    }

    // ── Object List ───────────────────────────

    private void DrawObjectList()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Visibility Objects", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        GUI.backgroundColor = AccentOrange;
        if (GUILayout.Button("↺ Refresh", GUILayout.Width(90)))
            RefreshFromClip();
        GUILayout.Space(4);
        GUI.backgroundColor = AccentGreen;
        if (GUILayout.Button("+ Add Object", GUILayout.Width(110)))
        {
            Undo.RecordObject(data, "Add Entry");
            data.entries.Add(new VisibilityObjectEntry());
            SaveData();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        DrawColumnHeaders();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        int toRemove = -1;
        int moveFrom = -1;
        int moveTo   = -1;
        for (int i = 0; i < data.entries.Count; i++)
        {
            int action = DrawObjectRow(data.entries[i], i, data.entries.Count);
            if (action == 2) toRemove = i;
            else if (action == -1) { moveFrom = i; moveTo = i - 1; }
            else if (action ==  1) { moveFrom = i; moveTo = i + 1; }
        }

        if (toRemove >= 0)
        {
            Undo.RecordObject(data, "Remove Entry");
            data.entries.RemoveAt(toRemove);
            SaveData();
        }
        else if (moveFrom >= 0)
        {
            Undo.RecordObject(data, "Reorder Entry");
            var tmp = data.entries[moveFrom];
            data.entries[moveFrom] = data.entries[moveTo];
            data.entries[moveTo]   = tmp;
            SaveData();
        }

        if (data.entries.Count == 0)
            EditorGUILayout.HelpBox("No objects yet — click '+ Add Object' to begin.", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void DrawColumnHeaders()
    {
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(position.width, 22), HeaderColor);
        GUILayout.Space(-22);
        EditorGUILayout.BeginHorizontal();
        var s = new GUIStyle(EditorStyles.miniBoldLabel)
            { normal = { textColor = new Color(0.7f, 0.7f, 0.8f) } };
        GUILayout.Space(10);
        GUILayout.Label("Object",                                    s, GUILayout.Width(160));
        GUILayout.Label("Visibility Ranges  (Frame ON → Frame OFF)", s, GUILayout.ExpandWidth(true));
        GUILayout.Label("",                                          s, GUILayout.Width(70));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }

    // Returns: -1 = move up, 0 = no action, 1 = move down, 2 = remove
    private int DrawObjectRow(VisibilityObjectEntry entry, int index, int total)
    {
        int    action = 0;
        Color bg = index % 2 == 0 ? RowColorA : RowColorB;
        EditorGUILayout.BeginVertical();

        // ── Object field row ──────────────────
        Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(26));
        EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, position.width, rowRect.height + 2), bg);
        GUILayout.Space(6);

        EditorGUI.BeginChangeCheck();
        var newObj = (GameObject)EditorGUILayout.ObjectField(
            entry.targetObject, typeof(GameObject), true, GUILayout.Width(160));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(data, "Set Object");
            entry.targetObject = newObj;
            if (newObj != null) { entry.objectName = newObj.name; entry.scenePath = GetScenePath(newObj); }
            SaveData();
        }

        // Warn if reference was lost but name is known
        if (entry.targetObject == null && !string.IsNullOrEmpty(entry.objectName))
        {
            var warn = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = AccentOrange } };
            GUILayout.Label($"⚠ {entry.objectName} (missing)", warn, GUILayout.Width(160));
        }

        GUILayout.FlexibleSpace();

        GUI.backgroundColor = AccentBlue;
        if (GUILayout.Button("+ Range", GUILayout.Width(70)))
        {
            Undo.RecordObject(data, "Add Range");
            entry.ranges.Add(new VisibilityRange { frameOn = 0, frameOff = 30 });
            SaveData();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(4);
        GUI.enabled = index > 0;
        if (GUILayout.Button("▲", GUILayout.Width(22))) action = -1;
        GUI.enabled = index < total - 1;
        if (GUILayout.Button("▼", GUILayout.Width(22))) action =  1;
        GUI.enabled = true;

        GUILayout.Space(4);
        GUI.backgroundColor = AccentRed;
        if (GUILayout.Button("✕", GUILayout.Width(26))) action = 2;
        GUI.backgroundColor = Color.white;
        GUILayout.Space(4);

        EditorGUILayout.EndHorizontal();

        // ── Range rows ────────────────────────
        int rangeToRemove = -1;
        for (int r = 0; r < entry.ranges.Count; r++)
        {
            var range = entry.ranges[r];
            Rect rr = EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
            EditorGUI.DrawRect(new Rect(rr.x, rr.y, position.width, 22),
                new Color(bg.r - 0.03f, bg.g - 0.03f, bg.b - 0.03f));

            GUILayout.Space(176);

            var onStyle  = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = AccentGreen } };
            var offStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = AccentRed   } };
            var dimStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.5f, 0.5f, 0.6f) } };

            GUILayout.Label("ON:", onStyle, GUILayout.Width(24));
            EditorGUI.BeginChangeCheck();
            int newOn = EditorGUILayout.IntField(range.frameOn, GUILayout.Width(58));
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(data, "Edit Frame"); range.frameOn = Mathf.Max(0, newOn); EditorUtility.SetDirty(data); }

            GUILayout.Space(8);
            GUILayout.Label("OFF:", offStyle, GUILayout.Width(30));
            EditorGUI.BeginChangeCheck();
            int newOff = EditorGUILayout.IntField(range.frameOff, GUILayout.Width(58));
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(data, "Edit Frame"); range.frameOff = Mathf.Max(range.frameOn + 1, newOff); EditorUtility.SetDirty(data); }

            int dur = range.frameOff - range.frameOn;
            GUILayout.Label($"({dur} fr)", dimStyle, GUILayout.Width(52));

            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(0.55f, 0.18f, 0.18f);
            if (GUILayout.Button("−", GUILayout.Width(22))) rangeToRemove = r;
            GUI.backgroundColor = Color.white;
            GUILayout.Space(4);

            EditorGUILayout.EndHorizontal();
        }

        if (rangeToRemove >= 0)
        {
            Undo.RecordObject(data, "Remove Range");
            entry.ranges.RemoveAt(rangeToRemove);
            SaveData();
        }

        if (entry.ranges.Count == 0)
        {
            var s = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.45f, 0.45f, 0.45f) } };
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(176);
            GUILayout.Label("No ranges — click '+ Range'", s);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(3);
        EditorGUILayout.EndVertical();
        return action;
    }

    // ── Bake Button ───────────────────────────

    private void DrawBakeButton()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (_targetClip == null)
            EditorGUILayout.HelpBox("Assign a Target Animation Clip in Settings above.", MessageType.Warning);
        else if (data.entries.Count == 0)
            EditorGUILayout.HelpBox("Add at least one object with ranges before baking.", MessageType.Warning);
        else
        {
            GUI.backgroundColor = AccentGreen;
            var style = new GUIStyle(GUI.skin.button)
                { fontSize = 13, fontStyle = FontStyle.Bold, fixedHeight = 36 };
            if (GUILayout.Button("⬇  Bake Visibility into Clip", style))
                BakeVisibility();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(2);
            var note = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.5f, 0.7f, 0.5f) } };
            GUILayout.Label("✓ Non-destructive — only writes m_IsActive curves, other curves untouched.", note);
        }

        EditorGUILayout.EndVertical();
    }

    // ── Bake Logic ────────────────────────────

    private void BakeVisibility()
    {
        AnimationClip clip = _targetClip;
        int fps = data.frameRate;

        int baked = 0;
        var errors  = new List<string>();
        var summary = new List<string>();

        foreach (var entry in data.entries)
        {
            if (entry.targetObject == null)
            {
                errors.Add($"'{entry.objectName}' — GameObject reference missing.");
                continue;
            }
            if (entry.ranges.Count == 0) continue;

            string path       = GetRelativePath(entry.targetObject, out bool foundAnimator);
            string name       = entry.targetObject.name;

            if (!foundAnimator)
            {
                errors.Add($"'{name}' — no Animator found in hierarchy. The curve will be written but may never be evaluated.");
            }
            else if (path == "")
            {
                errors.Add($"'{name}' — Animator is on the object itself (path is empty). You cannot deactivate an Animator root through its own clip. Move the Animator to a parent.");
                continue;
            }

            // Build frame→value map
            var keyMap = new Dictionary<int, float>();
            keyMap[0] = 0f; // default invisible at frame 0

            foreach (var range in entry.ranges)
            {
                int fOn  = Mathf.Max(0, range.frameOn);
                int fOff = Mathf.Max(fOn + 1, range.frameOff);

                if (fOn > 0) keyMap[fOn - 1] = 0f; // one frame before ON: guarantee off just before snap
                keyMap[fOn]      = 1f;
                keyMap[fOff - 1] = 1f;
                keyMap[fOff]     = 0f;
            }

            var frames = new List<int>(keyMap.Keys);
            frames.Sort();

            var kfs = new Keyframe[frames.Count];
            for (int k = 0; k < frames.Count; k++)
                kfs[k] = new Keyframe(frames[k] / (float)fps, keyMap[frames[k]], 0f, 0f);

            var curve = new AnimationCurve(kfs);
            for (int k = 0; k < curve.length; k++)
            {
                AnimationUtility.SetKeyLeftTangentMode (curve, k, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Constant);
            }

            clip.SetCurve(path, typeof(GameObject), "m_IsActive", null);
            clip.SetCurve(path, typeof(GameObject), "m_IsActive", curve);
            baked++;

            string pathLabel = string.IsNullOrEmpty(path) ? "(root)" : path;
            summary.Add($"  {name}  →  \"{pathLabel}\"");
            Debug.Log($"[VisibilityBaker] '{name}' baked at path '{pathLabel}' in clip '{clip.name}'");
        }

        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        string msg = $"Baked {baked} object(s) into '{clip.name}'.\n\n"
                   + string.Join("\n", summary);
        if (errors.Count > 0)
            msg += $"\n\n⚠ Warnings / skipped:\n" + string.Join("\n", errors);

        AppendVisibilityLog(clip.name, data.entries);

        EditorUtility.DisplayDialog("Bake Complete", msg, "OK");
        Debug.Log("[VisibilityBaker] " + msg);
    }

    // ── Log ───────────────────────────────────

    static string VisLogPath => Path.Combine(Application.dataPath, "Editor", "VisibilityBakerLog.json");

    static void AppendVisibilityLog(string clipName, List<VisibilityObjectEntry> entries)
    {
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            if (entry.ranges.Count == 0) continue;
            string name   = entry.targetObject != null ? entry.targetObject.name : entry.objectName;
            string ranges = string.Join(", ", entry.ranges.ConvertAll(r => $"ON:{r.frameOn}→OFF:{r.frameOff}"));
            lines.Add($"  {name} — {ranges}");
        }

        // Read existing log
        var logList = new List<string>();
        if (File.Exists(VisLogPath))
        {
            try { logList = new List<string>(File.ReadAllLines(VisLogPath)); }
            catch { }
        }

        logList.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm}] clip: {clipName}");
        logList.AddRange(lines);
        logList.Add("");

        File.WriteAllLines(VisLogPath, logList);
    }

    // ── Path Resolution ───────────────────────

    private string GetRelativePath(GameObject obj, out bool foundAnimator)
    {
        Transform current = obj.transform;
        var parts = new List<string>();

        while (current != null)
        {
            if (current.GetComponent<Animator>() != null)
            {
                foundAnimator = true;
                parts.Reverse();
                return string.Join("/", parts);
            }
            parts.Add(current.name);
            current = current.parent;
        }

        // No Animator found — fall back to full scene path minus root name
        foundAnimator = false;
        parts.Reverse();
        if (parts.Count > 1) parts.RemoveAt(0);
        return string.Join("/", parts);
    }
}
