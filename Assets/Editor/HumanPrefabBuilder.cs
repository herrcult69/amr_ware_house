using UnityEditor;
using UnityEngine;

// Builds Assets/Prefabs/Human.prefab in code. Only the two legs keep colliders: the lidar scans at ~0.08 m,
// so legs must reach the floor and sit on Layer 0 (Default, inside the scan obstacleMask).
public static class HumanPrefabBuilder
{
    const string PrefabPath = "Assets/Prefabs/Human.prefab";
    const string MaterialFolder = "Assets/Materials";

    [MenuItem("Tools/AMR/Create Human Obstacle")]
    public static void Create()
    {
        Material skin = GetMaterial("Human_Skin", new Color(0.85f, 0.65f, 0.5f));
        Material cloth = GetMaterial("Human_Cloth", new Color(0.2f, 0.35f, 0.7f));

        var root = new GameObject("Human");
        root.layer = 0;
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        var walker = root.AddComponent<HumanWalker>();

        // Unity capsule primitive = r 0.5, h 2.0; scale (2r, h/2, 2r) gives the wanted size.
        walker.legLeft = MakeLeg(root.transform, "LegL", -0.12f, cloth);
        walker.legRight = MakeLeg(root.transform, "LegR", 0.12f, cloth);
        MakeBody(root.transform, "Torso", PrimitiveType.Capsule, new Vector3(0f, 1.20f, 0f), new Vector3(0.40f, 0.35f, 0.40f), cloth);
        MakeBody(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.70f, 0f), Vector3.one * 0.24f, skin);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("[HumanPrefabBuilder] Saved " + PrefabPath + ". Drop it across the aisle; set Path End in the inspector.");
    }

    static Transform MakeLeg(Transform parent, string name, float x, Material mat)
    {
        // Keeps its default CapsuleCollider: r 0.08, h 0.85, centre y 0.425 -> reaches the floor.
        var leg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        leg.name = name;
        leg.layer = 0;
        leg.transform.SetParent(parent, false);
        leg.transform.localPosition = new Vector3(x, 0.425f, 0f);
        leg.transform.localScale = new Vector3(0.16f, 0.425f, 0.16f);
        leg.GetComponent<Renderer>().sharedMaterial = mat;
        return leg.transform;
    }

    static void MakeBody(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.layer = 0;
        Object.DestroyImmediate(go.GetComponent<Collider>());   // looks only
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material GetMaterial(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        mat = new Material(sh) { color = color };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
