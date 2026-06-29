using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class DuplicateImageMerger : EditorWindow
{
    static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tiff", ".bmp", ".gif", ".exr", ".hdr" };
    static readonly string[] SerializedExtensions = { ".mat", ".unity", ".prefab", ".asset", ".controller", ".anim", ".overrideController", ".shadergraph" };

    class ImageEntry
    {
        public string AssetPath;
        public string AbsPath;
        public string Guid;
        public int RefCount;
    }

    class DuplicateGroup
    {
        public string FileName;
        public long FileSize;
        public List<ImageEntry> Entries = new();
        public int WinnerIndex;
    }

    List<DuplicateGroup> _groups = new();
    Vector2 _scroll;
    bool _scanned;
    string _status = "";

    [MenuItem("Tools/Duplicate Image Merger")]
    public static void Open() => GetWindow<DuplicateImageMerger>("Duplicate Image Merger");

    void OnGUI()
    {
        EditorGUILayout.Space(8);

        if (GUILayout.Button("Scan for Duplicates", GUILayout.Height(30)))
            Scan();

        if (!_scanned) return;

        EditorGUILayout.Space(4);

        if (_groups.Count == 0)
        {
            EditorGUILayout.HelpBox("No duplicate images found.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"Found {_groups.Count} duplicate group(s) — {_groups.Sum(g => g.Entries.Count - 1)} file(s) to remove", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var g in _groups)
            DrawGroup(g);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Dry Run (log to Console)", GUILayout.Height(28)))
                DryRun();

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("Merge All", GUILayout.Height(28)))
            {
                int toDelete = _groups.Sum(g => g.Entries.Count - 1);
                if (EditorUtility.DisplayDialog("Confirm Merge",
                    $"Update all references and delete {toDelete} duplicate file(s)?\n\nThis cannot be undone.", "Merge", "Cancel"))
                    MergeAll();
            }
            GUI.backgroundColor = prev;
        }

        if (!string.IsNullOrEmpty(_status))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(_status, MessageType.Info);
        }
    }

    void DrawGroup(DuplicateGroup group)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"{group.FileName}   ({group.FileSize / 1024f:F1} KB)", EditorStyles.boldLabel);

        for (int i = 0; i < group.Entries.Count; i++)
        {
            var e = group.Entries[i];
            bool keep = i == group.WinnerIndex;

            using (new EditorGUILayout.HorizontalScope())
            {
                var prevColor = GUI.contentColor;
                GUI.contentColor = keep ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.4f, 0.4f);
                EditorGUILayout.LabelField(keep ? "KEEP  " : "REMOVE", GUILayout.Width(56));
                GUI.contentColor = prevColor;

                EditorGUILayout.LabelField(e.AssetPath, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField($"{e.RefCount} refs", GUILayout.Width(58));

                if (!keep)
                {
                    if (GUILayout.Button("Keep this", GUILayout.Width(74)))
                        group.WinnerIndex = i;
                }
                else
                {
                    GUILayout.Space(78);
                }
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    void Scan()
    {
        _groups.Clear();
        _scanned = false;
        _status = "";

        string dataPath = NormalizePath(Application.dataPath);

        // Collect all image files, skip .fbm folders
        var allImages = new List<string>();
        foreach (var ext in ImageExtensions)
            allImages.AddRange(
                Directory.GetFiles(Application.dataPath, "*" + ext, SearchOption.AllDirectories)
                    .Select(NormalizePath)
                    .Where(p => !p.Contains(".fbm/")));

        // Group by (lower filename, filesize) — keep only groups with duplicates
        var byKey = allImages
            .GroupBy(p => (name: Path.GetFileName(p).ToLowerInvariant(), size: new FileInfo(p).Length))
            .Where(g => g.Count() > 1)
            .ToList();

        if (byKey.Count == 0) { _scanned = true; return; }

        // Load all serialized file contents once for fast reference counting
        EditorUtility.DisplayProgressBar("Duplicate Image Merger", "Loading serialized files…", 0f);
        var serialized = new List<(string path, string text)>();
        foreach (var ext in SerializedExtensions)
            foreach (var f in Directory.GetFiles(Application.dataPath, "*" + ext, SearchOption.AllDirectories))
            {
                try { serialized.Add((f, File.ReadAllText(f))); } catch { }
            }

        for (int gi = 0; gi < byKey.Count; gi++)
        {
            var kv = byKey[gi];
            EditorUtility.DisplayProgressBar("Duplicate Image Merger", $"Counting refs for {kv.Key.name}…", (float)gi / byKey.Count);

            var group = new DuplicateGroup { FileName = kv.Key.name, FileSize = kv.Key.size };

            foreach (var absPath in kv)
            {
                string assetPath = "Assets" + absPath.Substring(dataPath.Length);
                string guid = ReadGuid(absPath);
                int refCount = string.IsNullOrEmpty(guid) ? 0
                    : serialized.Sum(sf => CountOccurrences(sf.text, guid));

                group.Entries.Add(new ImageEntry
                {
                    AssetPath = assetPath,
                    AbsPath = absPath,
                    Guid = guid,
                    RefCount = refCount,
                });
            }

            // Most refs wins; tie-break: alphabetical path
            group.WinnerIndex = group.Entries
                .Select((e, i) => (e, i))
                .OrderByDescending(x => x.e.RefCount)
                .ThenBy(x => x.e.AssetPath, StringComparer.OrdinalIgnoreCase)
                .First().i;

            _groups.Add(group);
        }

        EditorUtility.ClearProgressBar();
        _scanned = true;
    }

    void DryRun()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Duplicate Image Merger — Dry Run ===\n");

        foreach (var group in _groups)
        {
            var winner = group.Entries[group.WinnerIndex];
            sb.AppendLine($"Group: {group.FileName} ({group.FileSize / 1024f:F1} KB)");
            sb.AppendLine($"  KEEP   {winner.AssetPath}  [{winner.RefCount} refs]");

            foreach (var loser in group.Entries.Where((_, i) => i != group.WinnerIndex))
                sb.AppendLine($"  REMOVE {loser.AssetPath}  [{loser.RefCount} refs → rerouted to winner guid]");

            sb.AppendLine();
        }

        Debug.Log(sb.ToString());
        _status = "Dry run logged to Console.";
    }

    void MergeAll()
    {
        int filesUpdated = 0;
        int deleted = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var group in _groups)
            {
                var winner = group.Entries[group.WinnerIndex];
                if (string.IsNullOrEmpty(winner.Guid)) continue;

                foreach (var loser in group.Entries.Where((_, i) => i != group.WinnerIndex))
                {
                    if (string.IsNullOrEmpty(loser.Guid)) continue;

                    filesUpdated += ReplaceGuidInProject(loser.Guid, winner.Guid);
                    AssetDatabase.DeleteAsset(loser.AssetPath);
                    deleted++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        _status = $"Done — {filesUpdated} serialized file(s) updated, {deleted} duplicate(s) deleted.";
        Debug.Log("[DuplicateImageMerger] " + _status);

        _groups.Clear();
        _scanned = false;
    }

    int ReplaceGuidInProject(string fromGuid, string toGuid)
    {
        int count = 0;
        foreach (var ext in SerializedExtensions)
            foreach (var file in Directory.GetFiles(Application.dataPath, "*" + ext, SearchOption.AllDirectories))
            {
                try
                {
                    string text = File.ReadAllText(file);
                    if (!text.Contains(fromGuid)) continue;
                    File.WriteAllText(file, text.Replace(fromGuid, toGuid));
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DuplicateImageMerger] Could not update {file}: {ex.Message}");
                }
            }
        return count;
    }

    static string ReadGuid(string absPath)
    {
        string metaPath = absPath + ".meta";
        if (!File.Exists(metaPath)) return null;
        foreach (var line in File.ReadLines(metaPath))
        {
            var m = Regex.Match(line, @"^guid:\s*([0-9a-f]{32})");
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    static int CountOccurrences(string text, string pattern)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        { count++; index += pattern.Length; }
        return count;
    }

    static string NormalizePath(string p) => p.Replace('\\', '/');
}
