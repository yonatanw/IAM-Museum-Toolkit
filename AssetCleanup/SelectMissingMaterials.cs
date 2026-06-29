using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SelectMissingMaterials
{
    [MenuItem("Tools/Select Objects With Missing Materials")]
    static void Select()
    {
        var found = new List<GameObject>();

        foreach (var renderer in Object.FindObjectsOfType<Renderer>())
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null)
                {
                    found.Add(renderer.gameObject);
                    break;
                }
            }
        }

        Selection.objects = found.ToArray();
        Debug.Log($"Found {found.Count} objects with missing materials.");
    }
}
