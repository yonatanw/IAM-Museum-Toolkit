using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BeamInController))]
public class BeamInControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var c = (BeamInController)target;
        int safeFps = Mathf.Max(c.fps, 1);

        float startSec = c.startDelayFrames / (float)safeFps;
        float sweepSec = c.sweepFrames      / (float)safeFps;
        float fadeSec  = c.fadeFrames       / (float)safeFps;
        float totalSec = startSec + sweepSec + fadeSec;

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            $"Offset  {c.startDelayFrames}f = {startSec:0.00}s\n" +
            $"Sweep   {c.sweepFrames}f = {sweepSec:0.00}s\n" +
            $"Fade    {c.fadeFrames}f = {fadeSec:0.00}s\n" +
            $"Total   {c.startDelayFrames + c.sweepFrames + c.fadeFrames}f = {totalSec:0.00}s",
            MessageType.None);

        EditorGUILayout.Space(4);

        GUI.enabled = c.beamRoot != null;
        if (GUILayout.Button("Collect Beam Renderers", GUILayout.Height(28)))
        {
            Undo.RecordObject(c, "Collect Beam Renderers");
            c.CollectBeamRenderers();
            EditorUtility.SetDirty(c);
        }
        GUI.enabled = true;
        if (c.beamRoot == null)
            EditorGUILayout.HelpBox("Assign a Beam Root to enable.", MessageType.Info);

        EditorGUILayout.Space(2);

        GUI.enabled = c.buildingRoot != null;
        if (GUILayout.Button("Collect Real Renderers", GUILayout.Height(28)))
        {
            Undo.RecordObject(c, "Collect Real Renderers");
            c.CollectRenderers();
            EditorUtility.SetDirty(c);
        }
        GUI.enabled = true;
        if (c.buildingRoot == null)
            EditorGUILayout.HelpBox("Assign a Building Root to enable.", MessageType.Info);
    }
}
