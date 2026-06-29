using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility that finds all AnimationClip assets and disables looping.
/// Run via menu: Tools > Disable All Animation Looping
/// </summary>
public class DisableAnimationLooping
{
    [MenuItem("Tools/Disable All Animation Looping")]
    public static void DisableLoopOnAllClips()
    {
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
        int modified = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clip == null)
                continue;

            // Handle clips imported from models (FBX, etc.)
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                ModelImporterClipAnimation[] clipAnimations = importer.clipAnimations;
                if (clipAnimations.Length == 0)
                    clipAnimations = importer.defaultClipAnimations;

                bool changed = false;
                foreach (var clipAnim in clipAnimations)
                {
                    if (clipAnim.loopTime)
                    {
                        clipAnim.loopTime = false;
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.clipAnimations = clipAnimations;
                    importer.SaveAndReimport();
                    modified++;
                    Debug.Log($"Disabled looping on imported clip: {path}");
                }
            }
            else
            {
                // Handle standalone AnimationClip assets
                SerializedObject so = new SerializedObject(clip);
                SerializedProperty loopTimeProp = so.FindProperty("m_AnimationClipSettings.m_LoopTime");

                if (loopTimeProp != null && loopTimeProp.boolValue)
                {
                    loopTimeProp.boolValue = false;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(clip);
                    modified++;
                    Debug.Log($"Disabled looping on clip: {path}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Done. Modified {modified} animation clip(s).");
    }
}
