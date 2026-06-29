using UnityEditor;
using UnityEngine;

public class BarycentricBaker : EditorWindow
{
    [MenuItem("Tools/Barycentric Baker")]
    public static void Open() => GetWindow<BarycentricBaker>("Barycentric Baker");

    void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Select GameObjects with MeshFilter components.\n" +
            "Bake creates a new mesh with barycentric coordinates in UV2, " +
            "enabling wireframe rendering in the BeamIn shader.",
            MessageType.Info);
        EditorGUILayout.Space(6);

        var selected = Selection.GetFiltered<MeshFilter>(SelectionMode.Deep);
        EditorGUILayout.LabelField($"Selected MeshFilters: {selected.Length}", EditorStyles.boldLabel);

        EditorGUILayout.Space(4);
        GUI.enabled = selected.Length > 0;
        if (GUILayout.Button("Bake Barycentric Coordinates", GUILayout.Height(30)))
            BakeSelected(selected);
        GUI.enabled = true;

        if (selected.Length == 0)
            EditorGUILayout.HelpBox("Select one or more GameObjects in the hierarchy.", MessageType.None);
    }

    static void BakeSelected(MeshFilter[] filters)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
            AssetDatabase.CreateFolder("Assets", "Meshes");
        if (!AssetDatabase.IsValidFolder("Assets/Meshes/Barycentric"))
            AssetDatabase.CreateFolder("Assets/Meshes", "Barycentric");

        int count = 0;
        foreach (var mf in filters)
        {
            if (!mf.sharedMesh) continue;

            Mesh baked = BakeMesh(mf.sharedMesh);
            string path = $"Assets/Meshes/Barycentric/{mf.sharedMesh.name}_Bary.asset";

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(baked, existing);
                Object.DestroyImmediate(baked);
                baked = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(baked, path);
            }

            Undo.RecordObject(mf, "Bake Barycentric");
            mf.sharedMesh = baked;
            EditorUtility.SetDirty(mf);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BarycentricBaker] Baked {count} mesh(es) → Assets/Meshes/Barycentric/");
    }

    static Mesh BakeMesh(Mesh src)
    {
        int[]     srcTris     = src.triangles;
        int       triCount    = srcTris.Length / 3;
        Vector3[] srcVerts    = src.vertices;
        Vector3[] srcNormals  = src.normals;
        Vector4[] srcTangents = src.tangents;
        Vector2[] srcUV1      = src.uv;
        Color[]   srcColors   = src.colors;

        int       vertCount = triCount * 3;
        Vector3[] verts     = new Vector3[vertCount];
        Vector3[] normals   = new Vector3[vertCount];
        Vector4[] tangents  = new Vector4[vertCount];
        Vector2[] uv1       = new Vector2[vertCount];
        Vector2[] uv2       = new Vector2[vertCount];
        Color[]   colors    = new Color[vertCount];
        int[]     tris      = new int[vertCount];

        // each vertex in a triangle gets a unique barycentric coord
        // z = 1 - x - y, so we only store xy in uv2
        Vector2[] bary = { new Vector2(1, 0), new Vector2(0, 1), new Vector2(0, 0) };

        for (int i = 0; i < triCount; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                int si = srcTris[i * 3 + j];
                int di = i * 3 + j;

                verts[di]    = srcVerts[si];
                normals[di]  = srcNormals.Length  > 0 ? srcNormals[si]  : Vector3.up;
                tangents[di] = srcTangents.Length > 0 ? srcTangents[si] : Vector4.zero;
                uv1[di]      = srcUV1.Length      > 0 ? srcUV1[si]      : Vector2.zero;
                colors[di]   = srcColors.Length   > 0 ? srcColors[si]   : Color.white;
                uv2[di]      = bary[j];
                tris[di]     = di;
            }
        }

        var mesh = new Mesh { name = src.name + "_Bary" };
        mesh.indexFormat = vertCount > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices  = verts;
        mesh.normals   = normals;
        mesh.tangents  = tangents;
        mesh.uv        = uv1;
        mesh.uv2       = uv2;
        mesh.colors    = colors;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }
}
