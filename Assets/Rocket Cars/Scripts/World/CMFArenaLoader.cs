using UnityEngine;
using System.IO;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Loads Rocket League arena collision meshes from .cmf files.
/// CMF format: [uint32 triCount][uint32 vertCount][uint32[] indices][float[] vertices]
/// Coordinates are in Bullet space (50x smaller than UU).
/// </summary>
public class CMFArenaLoader : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Scale factor: Bullet units to Unity units. Bullet = UU/50, so bulletToUnity = S * 50")]
    public float BulletToUnity = 0.625f; // 0.0125 * 50

    [Tooltip("Layer to assign to generated collision meshes")]
    public string CollisionLayer = "Env";

    [Tooltip("Parent object for generated meshes (uses this object if empty)")]
    public Transform MeshParent;

    [Header("Debug")]
    public bool ShowMeshGizmos = false;
    public Color GizmoColor = new Color(0f, 1f, 0f, 0.1f);

    private List<GameObject> _generatedObjects = new List<GameObject>();

    /// <summary>
    /// Load a single CMF file and create a MeshCollider from it.
    /// </summary>
    public GameObject LoadCMF(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[CMFLoader] File not found: {filePath}");
            return null;
        }

        byte[] data = File.ReadAllBytes(filePath);
        if (data.Length < 8)
        {
            Debug.LogError($"[CMFLoader] File too small: {filePath}");
            return null;
        }

        int offset = 0;

        // Read header
        uint triCount = ReadUInt32(data, ref offset);
        uint vertCount = ReadUInt32(data, ref offset);

        int expectedSize = 8 + (int)(triCount * 3 * 4) + (int)(vertCount * 3 * 4);
        if (data.Length < expectedSize)
        {
            Debug.LogError($"[CMFLoader] File size mismatch. Expected {expectedSize}, got {data.Length}");
            return null;
        }

        // Read indices
        int indexCount = (int)(triCount * 3);
        int[] indices = new int[indexCount];
        for (int i = 0; i < indexCount; i++)
            indices[i] = (int)ReadUInt32(data, ref offset);

        // Read vertices (Bullet space -> Unity space)
        // Bullet/RL: X=Forward, Y=Left, Z=Up
        // Unity: X=Right, Y=Up, Z=Forward
        Vector3[] vertices = new Vector3[(int)vertCount];
        for (int i = 0; i < vertCount; i++)
        {
            float bx = ReadFloat(data, ref offset);
            float by = ReadFloat(data, ref offset);
            float bz = ReadFloat(data, ref offset);

            // Convert from RL/Bullet coordinates to Unity
            // RL: X=Forward, Y=Right, Z=Up -> Unity: X=Right, Y=Up, Z=Forward
            vertices[i] = new Vector3(by, bz, bx) * BulletToUnity;
        }

        // Build mesh
        Mesh mesh = new Mesh();
        mesh.name = Path.GetFileNameWithoutExtension(filePath);

        if (vertCount > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Create GameObject with MeshCollider
        string objName = "Arena_" + mesh.name;
        GameObject go = new GameObject(objName);
        go.transform.SetParent(MeshParent != null ? MeshParent : transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.isStatic = true;

        int layer = LayerMask.NameToLayer(CollisionLayer);
        if (layer >= 0)
            go.layer = layer;
        else
            Debug.LogWarning($"[CMFLoader] Layer '{CollisionLayer}' not found, using default");

        MeshCollider mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex = false; // arena meshes are concave

        // Optionally add MeshFilter + MeshRenderer for debug visualization
        if (ShowMeshGizmos)
        {
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Standard"));
            mr.material.color = GizmoColor;
        }

        _generatedObjects.Add(go);

        Debug.Log($"[CMFLoader] Loaded {mesh.name}: {triCount} tris, {vertCount} verts, bounds={mesh.bounds}");
        return go;
    }

    /// <summary>
    /// Load all CMF files from a folder.
    /// </summary>
    public void LoadFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"[CMFLoader] Folder not found: {folderPath}");
            return;
        }

        string[] files = Directory.GetFiles(folderPath, "*.cmf");
        Debug.Log($"[CMFLoader] Found {files.Length} CMF files in {folderPath}");

        foreach (string file in files)
            LoadCMF(file);
    }

    /// <summary>
    /// Remove all generated collision meshes.
    /// </summary>
    public void ClearAll()
    {
        foreach (var go in _generatedObjects)
        {
            if (go != null)
            {
                if (Application.isPlaying)
                    Destroy(go);
                else
                    DestroyImmediate(go);
            }
        }
        _generatedObjects.Clear();
        Debug.Log("[CMFLoader] Cleared all arena meshes");
    }

    // --- Binary readers ---

    private static uint ReadUInt32(byte[] data, ref int offset)
    {
        uint val = (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        offset += 4;
        return val;
    }

    private static float ReadFloat(byte[] data, ref int offset)
    {
        float val = System.BitConverter.ToSingle(data, offset);
        offset += 4;
        return val;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CMFArenaLoader))]
public class CMFArenaLoaderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CMFArenaLoader loader = (CMFArenaLoader)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Load CMF File(s)...", GUILayout.Height(30)))
        {
            string path = EditorUtility.OpenFilePanel("Select CMF File", "", "cmf");
            if (!string.IsNullOrEmpty(path))
                loader.LoadCMF(path);
        }

        if (GUILayout.Button("Load Folder of CMFs...", GUILayout.Height(30)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Folder with CMF Files", "", "");
            if (!string.IsNullOrEmpty(path))
                loader.LoadFolder(path);
        }

        EditorGUILayout.Space(5);

        GUI.color = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("Clear All Arena Meshes", GUILayout.Height(25)))
        {
            loader.ClearAll();
        }
        GUI.color = Color.white;
    }
}
#endif
