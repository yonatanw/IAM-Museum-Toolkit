using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class MaterialFadeBaker : EditorWindow
{
    const  string ShaderName = "Custom/BaseTransparent";
    static string JsonPath   => Path.Combine(Application.dataPath, "Editor", "MaterialFadeBakerLog.json");

    FadeBakerJson _json;
    AnimationClip _clip;
    Vector2       _scroll;

    [MenuItem("Tools/Material Fade Baker")]
    static void Open() => GetWindow<MaterialFadeBaker>("Material Fade Baker");

    void OnEnable()
    {
        LoadJson();
        if (!string.IsNullOrEmpty(_json.clipGUID))
        {
            string p = AssetDatabase.GUIDToAssetPath(_json.clipGUID);
            if (!string.IsNullOrEmpty(p))
                _clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
        }
        Refresh();
    }

    void LoadJson()
    {
        _json = new FadeBakerJson();
        if (!File.Exists(JsonPath)) return;
        try   { _json = JsonUtility.FromJson<FadeBakerJson>(File.ReadAllText(JsonPath)); }
        catch { _json = new FadeBakerJson(); }
        if (_json == null) _json = new FadeBakerJson();
    }

    void SaveJson() => File.WriteAllText(JsonPath, JsonUtility.ToJson(_json, true));

    void Refresh()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null) { Debug.LogWarning($"[FadeBaker] Shader '{ShaderName}' not found."); return; }

        var shaderMatPaths = AssetDatabase.FindAssets("t:Material")
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Where(p => { var m = AssetDatabase.LoadAssetAtPath<Material>(p); return m != null && m.shader == shader; })
            .ToHashSet();

        foreach (var matPath in shaderMatPaths)
        {
            if (_json.entries.Any(e => e.materialPath == matPath)) continue;
            _json.entries.Add(new MaterialFadeEntry
            {
                materialPath = matPath,
                hasFadeIn    = false,
                fadeInStart  = 0,
                fadeInEnd    = 30,
                fadeOutStart = 60,
                fadeOutEnd   = 90,
            });
        }

        _json.entries.RemoveAll(e => !shaderMatPaths.Contains(e.materialPath));
        SaveJson();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Material Fade Baker", EditorStyles.boldLabel);
        if (GUILayout.Button("↺ Refresh", GUILayout.Width(80))) Refresh();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        _clip = (AnimationClip)EditorGUILayout.ObjectField("Target Clip", _clip, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck())
        {
            _json.clipGUID = _clip != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_clip)) : "";
            SaveJson();
        }

        EditorGUILayout.Space(8);

        if (_json.entries.Count == 0)
            EditorGUILayout.HelpBox($"No materials found using shader '{ShaderName}'.", MessageType.Info);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        for (int i = 0; i < _json.entries.Count; i++)
        {
            var    entry = _json.entries[i];
            var    mat   = AssetDatabase.LoadAssetAtPath<Material>(entry.materialPath);
            string label = mat != null ? mat.name : $"(missing) {entry.materialPath}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            GUI.enabled = i > 0;
            if (GUILayout.Button("▲", GUILayout.Width(22)))
            {
                var tmp = _json.entries[i]; _json.entries[i] = _json.entries[i - 1]; _json.entries[i - 1] = tmp;
                SaveJson();
                EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
                break;
            }
            GUI.enabled = i < _json.entries.Count - 1;
            if (GUILayout.Button("▼", GUILayout.Width(22)))
            {
                var tmp = _json.entries[i]; _json.entries[i] = _json.entries[i + 1]; _json.entries[i + 1] = tmp;
                SaveJson();
                EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
                break;
            }
            GUI.enabled = true;
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                _json.entries.RemoveAt(i);
                SaveJson();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            entry.hasFadeIn = EditorGUILayout.Toggle("Has Fade In", entry.hasFadeIn);
            if (entry.hasFadeIn)
            {
                entry.fadeInStart = EditorGUILayout.IntField("  Fade In Start (frame)", entry.fadeInStart);
                entry.fadeInEnd   = EditorGUILayout.IntField("  Fade In End (frame)",   entry.fadeInEnd);
            }
            entry.fadeOutStart = EditorGUILayout.IntField("Fade Out Start (frame)", entry.fadeOutStart);
            entry.fadeOutEnd   = EditorGUILayout.IntField("Fade Out End (frame)",   entry.fadeOutEnd);
            entry.renderQueue  = EditorGUILayout.IntField("Render Queue",           entry.renderQueue);
            if (EditorGUI.EndChangeCheck()) SaveJson();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);
        GUI.enabled = _clip != null && _json.entries.Count > 0;
        if (GUILayout.Button("Bake Into Clip", GUILayout.Height(30))) Bake();
        GUI.enabled = true;
    }

    void Bake()
    {
        if (_clip == null) return;
        float fps = _clip.frameRate;
        int   ok  = 0;
        var logLines = new List<string>();

        foreach (var entry in _json.entries)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(entry.materialPath);
            if (mat == null) { Debug.LogWarning($"[FadeBaker] Material not found: {entry.materialPath}"); continue; }

            int rendererCount = 0;
            foreach (var renderer in FindObjectsOfType<Renderer>())
            {
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    if (renderer.sharedMaterials[i] != mat) continue;

                    var    animator     = renderer.GetComponentInParent<Animator>();
                    string relativePath = animator != null ? RelativePath(animator.gameObject, renderer.gameObject) : ScenePath(renderer.gameObject);
                    string prop         = i == 0 ? "material._Alpha" : $"materials[{i}]._Alpha";

                    var binding = new EditorCurveBinding
                    {
                        path         = relativePath,
                        type         = renderer.GetType(),
                        propertyName = prop,
                    };

                    var keys = new List<Keyframe>();
                    if (entry.hasFadeIn)
                    {
                        keys.Add(new Keyframe(entry.fadeInStart / fps, 0f));
                        keys.Add(new Keyframe(entry.fadeInEnd   / fps, 1f));
                    }
                    else
                    {
                        keys.Add(new Keyframe(0f, 1f));
                    }

                    if (!keys.Any(k => Mathf.Approximately(k.time, entry.fadeOutStart / fps)))
                        keys.Add(new Keyframe(entry.fadeOutStart / fps, 1f));
                    keys.Add(new Keyframe(entry.fadeOutEnd / fps, 0f));
                    keys = keys.OrderBy(k => k.time).ToList();

                    var curve = new AnimationCurve(keys.ToArray());
                    for (int ki = 0; ki < curve.length; ki++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode (curve, ki, AnimationUtility.TangentMode.Auto);
                        AnimationUtility.SetKeyRightTangentMode(curve, ki, AnimationUtility.TangentMode.Auto);
                    }

                    AnimationUtility.SetEditorCurve(_clip, binding, null); // clear old keys
                    AnimationUtility.SetEditorCurve(_clip, binding, curve);
                    rendererCount++;
                }
            }

            if (rendererCount > 0)
            {
                mat.renderQueue = entry.renderQueue;
                EditorUtility.SetDirty(mat);
                ok++;
                string fadeInStr = entry.hasFadeIn ? $"fadeIn: {entry.fadeInStart}→{entry.fadeInEnd}, " : "";
                logLines.Add($"  {mat.name} ({rendererCount} renderer(s)) — {fadeInStr}fadeOut: {entry.fadeOutStart}→{entry.fadeOutEnd}, queue: {entry.renderQueue}");
            }
        }

        EditorUtility.SetDirty(_clip);
        AssetDatabase.SaveAssets();

        _json.log.Add(new FadeLogEntry
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            clipName  = _clip.name,
            lines     = logLines,
        });
        SaveJson();

        Debug.Log($"[FadeBaker] Baked {ok} material(s) into '{_clip.name}'.");
    }

    static string ScenePath(GameObject go)
    {
        string path = go.name;
        var t = go.transform.parent;
        while (t != null) { path = t.name + "/" + path; t = t.parent; }
        return path;
    }

    static string RelativePath(GameObject root, GameObject target)
    {
        if (root == target) return "";
        string path = target.name;
        var t = target.transform.parent;
        while (t != null && t.gameObject != root) { path = t.name + "/" + path; t = t.parent; }
        return path;
    }
}
