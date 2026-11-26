using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Tools > Cars > ... : puts the ten "MyCar" models (MainMenu MyCar1..MyCar10) into the whole game.
///  1. Build Cybertruck Model  – MyCar10 (rigged, 1/100 scale, grey materials) → static, car-sized, coloured, optimized prefab.
///  2. Build Drivable MyCars   – RCC prefabs Assets/PreFebs/MyCar3..10 from the Car1 template (MyCar1/2 = Car1/Car2 prefabs, same models).
///  3. Use MyCars In Scenes    – MainMenu garage list, GamePlay Cars[] / Arrow[] / FlyingCarManager, Splash save-data car list.
/// Safe to run again.
/// </summary>
public static class MyCarsSetup
{
    const string CyberFbx = "Assets/02- Tesla.Cybertruck.Rigged/edefb12c69a54178a1a299f92443621e.fbx";
    const string CyberMatDir = "Assets/02- Tesla.Cybertruck.Rigged/Materials";
    const string CyberOutDir = "Assets/Res/Cars/Cybertruck";
    const string CyberPrefab = CyberOutDir + "/Cybertruck.prefab";
    const float CyberLength = 5.0f; // same class as the other garage cars (4.2 – 4.9 m)

    const string TemplatePrefab = "Assets/PreFebs/Car1.prefab";
    const string OutDir = "Assets/PreFebs";
    const string AE = "Assets/Res/Cars/AE_CARS/SAMOCHODY/";

    const string MainMenuScene = "Assets/Scenes/MainMenu.unity";
    const string GamePlayScene = "Assets/Scenes/GamePlay.unity";
    const string SplashScene = "Assets/Scenes/Splash.unity";

    // Display model of every MyCar (index 0 = MyCar1).
    static readonly string[] Models =
    {
        AE + "Car_18/Full Prefab/Prefab.prefab",
        AE + "Car_28/Full Prefab/Prefab.prefab",
        AE + "Car_58/Full Prefab/Prefab.prefab",
        AE + "Car_55/Full Prefab/Prefab.prefab",
        AE + "Car_53/Full Prefab/Prefab.prefab",
        AE + "Car_51/Full Prefab/Prefab.prefab",
        AE + "Car_48/Full Prefab/Prefab.prefab",
        AE + "Car_36/Full Prefab/Prefab.prefab",
        AE + "Car_34/Full Prefab/Prefab.prefab",
        CyberPrefab,
    };

    // Drivable prefab of every MyCar. MyCar1/2 use the existing Car1/Car2 prefabs (same models, already tuned).
    static string DrivablePath(int i) => i == 0 ? "Assets/PreFebs/Car1.prefab" : i == 1 ? "Assets/PreFebs/Car2.prefab" : OutDir + "/MyCar" + (i + 1) + ".prefab";

    static readonly (string name, int price)[] Shop =
    {
        ("Rookie", 0), ("Swift", 10000), ("Storm", 25000), ("Nitro", 50000), ("Legend", 70000),
        ("Titan", 95000), ("Viper", 120000), ("Phantom", 150000), ("Blaze", 180000), ("Cyber", 220000),
    };

    [MenuItem("Tools/Cars/Setup MyCars (all steps)")]
    public static void All()
    {
        BuildCybertruck();
        BuildDrivableCars();
        UseInScenes();
    }

    // ───────────────────────────── 1. Cybertruck ─────────────────────────────

    struct Part { public Material mat; public Vector3[] v; public Vector3[] n; public Vector2[] uv; public int[] tri; public bool steer; }

    static readonly string[] WheelMats = { "CT_Tire", "CT_Rim_Cover", "CT_Rim_Trim", "CT_Brake_Disc" };

    [MenuItem("Tools/Cars/1 Build Cybertruck Model")]
    public static void BuildCybertruck()
    {
        FixCyberMaterials();
        EnsureFolder(CyberOutDir);

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(CyberFbx);
        var inst = (GameObject)Object.Instantiate(src);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        inst.transform.localScale = Vector3.one;

        // Bake the skinned meshes (bind pose) into root space.
        var parts = new List<Part>();
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            var m = smr.transform.localToWorldMatrix;
            var v = baked.vertices.Select(p => m.MultiplyPoint3x4(p)).ToArray();
            var n = baked.normals.Length == v.Length ? baked.normals.Select(p => m.MultiplyVector(p).normalized).ToArray() : new Vector3[v.Length];
            var uv = baked.uv.Length == v.Length ? baked.uv : new Vector2[v.Length];
            // Vertices driven by the rig's steering-wheel bone = the yoke (cut out as its own turning mesh).
            int steerBone = Array.FindIndex(smr.bones, b => b != null && b.name.Contains("SteeringWheel"));
            var bw = steerBone >= 0 ? smr.sharedMesh.boneWeights : null;
            bool IsSteer(int i) => bw != null && i < bw.Length && bw[i].boneIndex0 == steerBone && bw[i].weight0 > 0.5f;
            for (int s = 0; s < baked.subMeshCount; s++)
            {
                var mats = smr.sharedMaterials;
                var mat = mats[Mathf.Min(s, mats.Length - 1)];
                var all3 = baked.GetTriangles(s);
                var rest = new List<int>(); var yoke = new List<int>();
                for (int t = 0; t < all3.Length; t += 3)
                {
                    int votes = (IsSteer(all3[t]) ? 1 : 0) + (IsSteer(all3[t + 1]) ? 1 : 0) + (IsSteer(all3[t + 2]) ? 1 : 0);
                    var dst = votes >= 2 ? yoke : rest;
                    dst.Add(all3[t]); dst.Add(all3[t + 1]); dst.Add(all3[t + 2]);
                }
                if (rest.Count > 0) parts.Add(new Part { mat = mat, v = v, n = n, uv = uv, tri = rest.ToArray() });
                if (yoke.Count > 0) parts.Add(new Part { mat = mat, v = v, n = n, uv = uv, tri = yoke.ToArray(), steer = true });
            }
            Object.DestroyImmediate(baked);
        }
        Object.DestroyImmediate(inst);

        // Size, facing and ground.
        var all = Encapsulate(parts.SelectMany(p => p.tri.Select(t => p.v[t])));
        bool zLong = all.size.z >= all.size.x;
        float k = CyberLength / (zLong ? all.size.z : all.size.x);
        var head = parts.Where(p => p.mat != null && p.mat.name == "CT_Light_Head_LED").ToList();
        var headC = head.Count > 0 ? Encapsulate(head.SelectMany(p => p.tri.Select(t => p.v[t]))).center : all.center + Vector3.forward;
        // Rotation that makes the headlights point to +Z.
        Vector3 toHead = headC - all.center; toHead.y = 0;
        float yaw = zLong ? (toHead.z >= 0 ? 0f : 180f) : (toHead.x >= 0 ? -90f : 90f);
        Quaternion face = Quaternion.Euler(0f, yaw, 0f);
        Vector3 Xf(Vector3 p) => face * ((p - all.center) * k);
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            p.v = p.v.Select(Xf).ToArray();
            p.n = p.n.Select(x => face * x).ToArray();
            parts[i] = p;
        }
        // (parts sharing one baked vertex array were transformed once each – they hold their own copies now)
        float ground = parts.Min(p => p.tri.Length == 0 ? float.MaxValue : p.tri.Min(t => p.v[t].y));
        for (int i = 0; i < parts.Count; i++) { var p = parts[i]; p.v = p.v.Select(x => x - Vector3.up * ground).ToArray(); parts[i] = p; }

        // Texture coordinates: the model's UVs belong to an atlas that was never shipped, so detail textures
        // (brushed steel, leather, rubber) get box-projected UVs at real-world scale instead (1 UV = 1 m),
        // and the centre screen gets 0..1 across its own face.
        for (int i = 0; i < parts.Count; i++) parts[i] = WithDetailUVs(parts[i]);
        CyberTextures.Generate(CyberOutDir + "/Textures");
        FixCyberMaterials();
        var yokeMat = CyberTextures.YokeMaterial(CyberMatDir);
        for (int i = 0; i < parts.Count; i++) if (parts[i].steer) { var p = parts[i]; p.mat = yokeMat; parts[i] = p; }

        // Body = everything but the wheels; wheels split per corner.
        var bodyParts = parts.Where(p => !IsWheelMat(p.mat) && !p.steer).ToList();
        var steerParts = parts.Where(p => p.steer).ToList();
        var wheelParts = parts.Where(p => IsWheelMat(p.mat)).ToList();
        string[] slots = { "Wheel_L", "Wheel_R", "Wheel_Back_L", "Wheel_Back_R" }; // AE naming: front L/R, back L/R
        var wheelTris = new List<Part>[4];
        for (int w = 0; w < 4; w++) wheelTris[w] = new List<Part>();
        foreach (var p in wheelParts)
        {
            var per = new List<int>[4];
            for (int w = 0; w < 4; w++) per[w] = new List<int>();
            for (int t = 0; t < p.tri.Length; t += 3)
            {
                var c = (p.v[p.tri[t]] + p.v[p.tri[t + 1]] + p.v[p.tri[t + 2]]) / 3f;
                int w = (c.z >= 0 ? 0 : 2) + (c.x >= 0 ? 1 : 0);
                per[w].Add(p.tri[t]); per[w].Add(p.tri[t + 1]); per[w].Add(p.tri[t + 2]);
            }
            for (int w = 0; w < 4; w++) if (per[w].Count > 0) wheelTris[w].Add(new Part { mat = p.mat, v = p.v, n = p.n, uv = p.uv, tri = per[w].ToArray() });
        }

        // Updated in place when it exists, so scene instances keep their links and overrides.
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(CyberPrefab) != null;
        var root = exists ? PrefabUtility.LoadPrefabContents(CyberPrefab) : new GameObject("Cybertruck");
        var body = MakeMeshObject("Body_Cyber", root.transform, bodyParts, Vector3.zero, CyberOutDir + "/Cybertruck_Body.asset");
        body.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        for (int w = 0; w < 4; w++)
        {
            var tire = wheelTris[w].Where(p => p.mat.name == "CT_Tire").ToList();
            var center = Encapsulate((tire.Count > 0 ? tire : wheelTris[w]).SelectMany(p => p.tri.Select(t => p.v[t]))).center;
            MakeMeshObject(slots[w], root.transform, wheelTris[w], center, CyberOutDir + "/Cybertruck_" + slots[w] + ".asset");
        }
        // Yoke: own mesh named like the other cars' interior wheel ("Steering_wheel"), pivot at its centre,
        // so VRCarRig puts the VR grab ring on it and turns it with the player's steering.
        if (steerParts.Count > 0)
        {
            var center = Encapsulate(steerParts.SelectMany(p => p.tri.Select(t => p.v[t]))).center;
            MakeMeshObject("Steering_wheel", root.transform, steerParts, center, CyberOutDir + "/Cybertruck_Steering_wheel.asset");
        }
        else Debug.LogWarning("[Cars] Cybertruck: no steering-wheel bone found, yoke stays in the body.");
        PrefabUtility.SaveAsPrefabAsset(root, CyberPrefab);
        if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        SyncCyberIntoDrivable();
        Debug.Log("[Cars] Cybertruck model built: " + CyberPrefab);
    }

    /// The drivable MyCar10 holds a copy of the Cybertruck model: give it the separate yoke / body materials too.
    static void SyncCyberIntoDrivable()
    {
        string path = DrivablePath(Models.Length - 1);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return; // built later by step 2
        var cyber = AssetDatabase.LoadAssetAtPath<GameObject>(CyberPrefab);
        var srcBody = cyber.transform.Find("Body_Cyber");
        var srcSteer = cyber.transform.Find("Steering_wheel");
        var root = PrefabUtility.LoadPrefabContents(path);
        var body = root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Body_Cyber");
        if (body != null)
        {
            body.sharedMaterials = srcBody.GetComponent<MeshRenderer>().sharedMaterials;
            if (srcSteer != null)
            {
                var model = body.transform.parent;
                var steer = model.Find("Steering_wheel");
                if (steer == null) { steer = new GameObject("Steering_wheel").transform; steer.SetParent(model, false); }
                steer.localPosition = srcSteer.localPosition; steer.localRotation = srcSteer.localRotation;
                var mf = steer.GetComponent<MeshFilter>(); if (mf == null) mf = steer.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = srcSteer.GetComponent<MeshFilter>().sharedMesh;
                var mr = steer.GetComponent<MeshRenderer>(); if (mr == null) mr = steer.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterials = srcSteer.GetComponent<MeshRenderer>().sharedMaterials;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    static bool IsWheelMat(Material m) => m != null && WheelMats.Contains(m.name);

    /// Box projection per vertex (dominant normal axis), metres → UV. Steel streaks run along the car (Z) on the sides.
    static Part WithDetailUVs(Part p)
    {
        var uv = new Vector2[p.v.Length];
        bool screen = p.mat != null && p.mat.name == "CT_Screen";
        Bounds sb = default;
        if (screen) sb = Encapsulate(p.tri.Select(t => p.v[t]));
        var used = new HashSet<int>(p.tri);
        for (int i = 0; i < uv.Length; i++)
        {
            if (!used.Contains(i)) { uv[i] = p.uv[i]; continue; }
            var v = p.v[i];
            if (screen)
            {
                // Faces the driver (looks along +Z): x → u, y → v across the screen's own size.
                uv[i] = new Vector2(Mathf.InverseLerp(sb.min.x, sb.max.x, v.x), Mathf.InverseLerp(sb.min.y, sb.max.y, v.y));
                continue;
            }
            var n = p.n[i];
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ax >= ay && ax >= az) uv[i] = new Vector2(v.z, v.y);      // sides
            else if (ay >= az) uv[i] = new Vector2(v.z, v.x);              // top / bottom
            else uv[i] = new Vector2(v.x, v.y);                            // front / back
        }
        p.uv = uv;
        return p;
    }

    static Bounds Encapsulate(IEnumerable<Vector3> pts)
    {
        bool first = true; var b = new Bounds();
        foreach (var p in pts) { if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p); }
        return b;
    }

    /// One static mesh (one sub-mesh per material, vertices compacted, relative to <paramref name="pivot"/>), stored as an asset.
    static GameObject MakeMeshObject(string name, Transform parent, List<Part> parts, Vector3 pivot, string assetPath)
    {
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
        var subs = new List<List<int>>(); var mats = new List<Material>();
        foreach (var group in parts.GroupBy(p => p.mat))
        {
            var idx = new List<int>();
            foreach (var p in group)
            {
                var remap = new Dictionary<int, int>();
                foreach (var t in p.tri)
                {
                    if (!remap.TryGetValue(t, out int ni))
                    {
                        ni = verts.Count; remap[t] = ni;
                        verts.Add(p.v[t] - pivot); norms.Add(p.n[t]); uvs.Add(p.uv[t]);
                    }
                    idx.Add(ni);
                }
            }
            subs.Add(idx); mats.Add(group.Key);
        }
        var mesh = new Mesh { name = name, indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs);
        mesh.subMeshCount = subs.Count;
        for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
        mesh.RecalculateBounds();
        MeshUtility.Optimize(mesh);
        MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Medium);
        mesh.UploadMeshData(false);

        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(existing); }
        else AssetDatabase.CreateAsset(mesh, assetPath);
        // Not CPU-readable in builds (half the memory); nothing reads these meshes at runtime.
        var mso = new SerializedObject(mesh);
        var readable = mso.FindProperty("m_IsReadable");
        if (readable != null) { readable.boolValue = false; mso.ApplyModifiedPropertiesWithoutUndo(); }

        var existingT = parent.Find(name);
        var go = existingT != null ? existingT.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pivot;
        go.transform.localRotation = Quaternion.identity;
        var mf = go.GetComponent<MeshFilter>(); if (mf == null) mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>(); if (mr == null) mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mats.ToArray();
        mr.lightProbeUsage = LightProbeUsage.BlendProbes;
        mr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        return go;
    }

    // Colours of the Cybertruck (the model ships without textures – only flat grey materials).
    struct Look { public Color c; public float smooth; public bool transparent; public Color emission; }
    static Look L(float r, float g, float b, float smooth, float a = 1f) => new Look { c = new Color(r, g, b, a), smooth = smooth, transparent = a < 1f };
    static Look E(float r, float g, float b, float glow) => new Look { c = new Color(r, g, b), smooth = 0.6f, emission = new Color(r, g, b) * glow };

    static readonly Dictionary<string, Look> CyberLooks = new Dictionary<string, Look>
    {
        { "CT_Stainless_Steel",       L(0.70f, 0.71f, 0.72f, 0.55f) },
        { "CT_Chassis",               L(0.66f, 0.67f, 0.68f, 0.45f) },
        { "CT_Chrome",                L(0.85f, 0.86f, 0.88f, 0.90f) },
        { "CT_Trim_Dark",             L(0.07f, 0.07f, 0.08f, 0.25f) },
        { "CT_Trim_Gloss_Black",      L(0.02f, 0.02f, 0.025f, 0.85f) },
        { "CT_Glass",                 L(0.04f, 0.05f, 0.06f, 0.95f, 0.55f) },
        { "CT_Light_Lens_Clear",      L(0.90f, 0.92f, 0.95f, 0.95f, 0.30f) },
        { "CT_Interior_Dark",         L(0.05f, 0.05f, 0.055f, 0.20f) },
        { "CT_Interior_Metal",        L(0.45f, 0.46f, 0.48f, 0.60f) },
        { "CT_Interior_Seat",         L(0.10f, 0.10f, 0.11f, 0.25f) },
        { "CT_Screen",                E(0.05f, 0.08f, 0.12f, 1.0f) },
        { "CT_Light_Head_LED",        E(1.00f, 1.00f, 1.00f, 1.5f) },
        { "CT_Light_Indicator_Amber", E(1.00f, 0.45f, 0.00f, 1.0f) },
        { "CT_Light_Tail_Red",        E(0.80f, 0.02f, 0.02f, 1.2f) },
        { "CT_Ambient_Light_Red",     E(0.90f, 0.05f, 0.05f, 1.0f) },
        { "CT_Brake_Disc",            L(0.35f, 0.35f, 0.36f, 0.50f) },
        { "CT_Rim_Cover",             L(0.09f, 0.09f, 0.10f, 0.40f) },
        { "CT_Rim_Trim",              L(0.20f, 0.20f, 0.21f, 0.55f) },
        { "CT_Tire",                  L(0.035f, 0.035f, 0.04f, 0.12f) },
    };

    [MenuItem("Tools/Cars/Fix Cybertruck Materials")]
    public static void FixCyberMaterials()
    {
        var simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { CyberMatDir }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || !CyberLooks.TryGetValue(mat.name, out var look)) continue;
            mat.shader = simpleLit; // same shader as the other garage cars (cheap on Android / Quest)
            mat.SetColor("_BaseColor", look.c);
            mat.SetColor("_Color", look.c);
            mat.SetTexture("_BaseMap", null);
            mat.SetFloat("_Smoothness", look.smooth);
            mat.SetColor("_SpecColor", Color.Lerp(new Color(0.1f, 0.1f, 0.1f), new Color(0.6f, 0.6f, 0.6f), look.smooth));
            mat.SetFloat("_SpecularHighlights", 1f);
            mat.EnableKeyword("_SPECULAR_COLOR");
            mat.enableInstancing = true;

            if (look.emission.maxColorComponent > 0f)
            {
                mat.SetColor("_EmissionColor", look.emission);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            if (look.transparent)
            {
                mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.SetShaderPassEnabled("DepthOnly", false);
                mat.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.SetFloat("_SrcBlend", (float)BlendMode.One); mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); mat.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Geometry;
                mat.SetShaderPassEnabled("DepthOnly", true);
                mat.SetShaderPassEnabled("ShadowCaster", true);
            }
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
        CyberTextures.Apply(CyberMatDir, CyberOutDir + "/Textures"); // detail textures on top of the colours (once generated)
    }

    // ───────────────────────────── 2. Drivable prefabs ─────────────────────────────

    [MenuItem("Tools/Cars/2 Build Drivable MyCars")]
    public static void BuildDrivableCars()
    {
        for (int i = 2; i < Models.Length; i++)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models[i]);
            if (model == null) { Debug.LogError("[Cars] Missing model " + Models[i]); continue; }
            BuildDrivable("MyCar" + (i + 1), model, electric: Models[i] == CyberPrefab, DrivablePath(i));
        }
        AssetDatabase.SaveAssets();
    }

    static bool IsWheelName(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("wheel") && !n.Contains("steer") && !n.Contains("stering");
    }

    static void BuildDrivable(string name, GameObject model, bool electric, string path)
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefab);
        var car = (GameObject)Object.Instantiate(template);
        car.name = name;
        car.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var rt = car.transform;

        // Template reference points (Car1) for the parts we move with the body.
        var tVisual = rt.Find("Car1");
        var tBody = Encapsulate(tVisual.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => Corners(r.bounds)));

        // New body.
        for (int c = tVisual.childCount - 1; c >= 0; c--) Object.DestroyImmediate(tVisual.GetChild(c).gameObject);
        tVisual.name = "Model";
        tVisual.localPosition = Vector3.zero; tVisual.localRotation = Quaternion.identity;
        var vis = (GameObject)Object.Instantiate(model, tVisual, false);
        vis.name = model.name == "Prefab" ? System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(model)))) : model.name;
        vis.transform.localPosition = Vector3.zero; vis.transform.localRotation = Quaternion.identity;

        var wheels = vis.GetComponentsInChildren<MeshRenderer>(true).Where(r => IsWheelName(r.name)).ToList();
        if (wheels.Count != 4) { Debug.LogError($"[Cars] {name}: expected 4 wheels, found {wheels.Count}"); Object.DestroyImmediate(car); return; }

        // Same wheel ground / axle centre as the template: wheel bottom at y=-0.80, axle midpoint at z=0.12.
        var wb = wheels.Select(w => w.bounds).ToList();
        float srcGround = wb.Min(b => b.min.y);
        var byZ = wb.OrderByDescending(b => b.center.z).ToList();
        float axleMid = ((byZ[0].center.z + byZ[1].center.z) / 2f + (byZ[2].center.z + byZ[3].center.z) / 2f) / 2f;
        float midX = wb.Average(b => b.center.x);
        tVisual.localPosition = new Vector3(-midX, -0.80f - srcGround, 0.12f - axleMid);

        // Wheel pivots at the wheel centres (RCC turns these).
        var wheelRoot = rt.Find("Wheel Transforms");
        for (int c = wheelRoot.childCount - 1; c >= 0; c--) Object.DestroyImmediate(wheelRoot.GetChild(c).gameObject);
        var ordered = wheels.OrderByDescending(w => w.bounds.center.z).ToList();
        var front = ordered.Take(2).OrderBy(w => w.bounds.center.x).ToList();
        var rear = ordered.Skip(2).OrderBy(w => w.bounds.center.x).ToList();
        var slot = new (string pivot, string collider, string rccTransform, string rccCollider, MeshRenderer r)[]
        {
            ("Wheel_FL", "Wheel_FL", "FrontLeftWheelTransform", "FrontLeftWheelCollider", front[0]),
            ("Wheel_FR", "Wheel_FR", "FrontRightWheelTransform", "FrontRightWheelCollider", front[1]),
            ("Wheel_Back_L", "Wheel_RL", "RearLeftWheelTransform", "RearLeftWheelCollider", rear[0]),
            ("Wheel_Back_R", "Wheel_RR", "RearRightWheelTransform", "RearRightWheelCollider", rear[1]),
        };
        var rcc = car.GetComponent<RCC_CarControllerV3>();
        var so = new SerializedObject(rcc);
        float minRadius = float.MaxValue;
        foreach (var s in slot)
        {
            var b = s.r.bounds;
            var pivot = new GameObject(s.pivot).transform;
            pivot.SetParent(wheelRoot, false);
            pivot.position = b.center;
            s.r.transform.SetParent(pivot, true);

            var wcT = rt.Find("Wheel Colliders/" + s.collider);
            wcT.position = b.center;
            var wc = wcT.GetComponent<WheelCollider>();
            wc.radius = b.size.y / 2f;
            minRadius = Mathf.Min(minRadius, wc.radius);
            var rwc = wcT.GetComponent<RCC_WheelCollider>();
            var wso = new SerializedObject(rwc);
            wso.FindProperty("wheelModel").objectReferenceValue = pivot;
            wso.ApplyModifiedPropertiesWithoutUndo();

            so.FindProperty(s.rccTransform).objectReferenceValue = pivot;
            so.FindProperty(s.rccCollider).objectReferenceValue = rwc;
        }
        // Same road speed per engine RPM as the template, whatever the wheel size (small wheels would hit the
        // rev limiter in 1st gear before the shift point). Wheel force per torque stays the same too.
        var tRadius = template.transform.Find("Wheel Colliders/Wheel_RL").GetComponent<WheelCollider>().radius;
        var rearRadius = (rt.Find("Wheel Colliders/Wheel_RL").GetComponent<WheelCollider>().radius + rt.Find("Wheel Colliders/Wheel_RR").GetComponent<WheelCollider>().radius) / 2f;
        var ratio = so.FindProperty("finalRatio");
        if (ratio != null) ratio.floatValue = template.GetComponent<RCC_CarControllerV3>().finalRatio * rearRadius / tRadius;

        var fuel = so.FindProperty("fuelConsumptionRate"); // GamePlay scene override on every car
        if (fuel != null) fuel.floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Body bounds (without wheels) → collider, exhaust, camera points.
        var bodyR = tVisual.GetComponentsInChildren<MeshRenderer>(true).Where(r => !wheels.Contains(r)).ToList();
        var body = Encapsulate(bodyR.SelectMany(r => Corners(r.bounds)));
        float groundY = -0.80f;

        var col = rt.Find("Collider");
        PhysicsMaterial pm = null;
        foreach (var mc in col.GetComponents<MeshCollider>()) { if (pm == null) pm = mc.sharedMaterial; Object.DestroyImmediate(mc); }
        foreach (var bc in col.GetComponents<BoxCollider>()) { if (pm == null) pm = bc.sharedMaterial; Object.DestroyImmediate(bc); }
        var mf = col.GetComponent<MeshFilter>(); if (mf != null) Object.DestroyImmediate(mf);
        col.localPosition = Vector3.zero; col.localRotation = Quaternion.identity; col.localScale = Vector3.one;
        var box = col.gameObject.AddComponent<BoxCollider>();
        box.sharedMaterial = pm;
        float bottom = groundY + 0.35f;
        box.center = new Vector3(body.center.x, (bottom + body.max.y) / 2f, body.center.z);
        box.size = new Vector3(body.size.x * 0.97f, body.max.y - bottom, body.size.z * 0.98f);

        float dTop = body.max.y - tBody.max.y, dFront = body.max.z - tBody.max.z;
        Shift(rt.Find("HoodCamera"), new Vector3(0, dTop, dFront));
        Shift(rt.Find("Arrow"), new Vector3(0, dTop, 0));
        Shift(rt.Find("PlayerIcon"), new Vector3(0, dTop, 0));
        var getOut = rt.Find("Get Out Pos");
        if (getOut != null) getOut.localPosition = new Vector3(body.min.x - 0.4f, getOut.localPosition.y, getOut.localPosition.z);
        var exhausts = rt.Find("Exhausts");
        if (exhausts != null)
        {
            if (electric) exhausts.gameObject.SetActive(false); // the Cybertruck has no exhaust
            else
                foreach (Transform ex in exhausts)
                    ex.position = new Vector3(Mathf.Clamp(ex.position.x, body.min.x + 0.2f, body.max.x - 0.2f), groundY + 0.3f, body.min.z + 0.05f);
        }

        // GamePlay scene overrides of the old cars, baked into the prefab.
        var icon = rt.Find("PlayerIcon"); if (icon != null) icon.gameObject.layer = 14;
        var mini = rt.Find("MinimapView (Camera)");
        if (mini != null && mini.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
            mini.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

        // VR driver eye point.
        var oldSeat = rt.Find("VRSeat"); if (oldSeat != null) Object.DestroyImmediate(oldSeat.gameObject);
        var steer = VRCarRig.FindInteriorWheel(rcc);
        Vector3 eye;
        if (electric) eye = new Vector3(-0.38f, 0.55f, 0.30f); // Cybertruck: measured from its driver seat (no separate steering-wheel mesh)
        else if (steer != null) eye = rt.InverseTransformPoint(steer.bounds.center) + new Vector3(0f, 0.36f, -0.5f);
        else
        {
            var fl = slot[0].r.bounds.center; var rl = slot[2].r.bounds.center;
            eye = new Vector3(fl.x * 0.45f, groundY + 1.12f + Mathf.Max(0f, body.max.y - tBody.max.y) * 0.5f, Mathf.Lerp(rl.z, fl.z, 0.42f));
        }
        var seat = new GameObject("VRSeat").transform;
        seat.SetParent(rt, false); seat.localPosition = eye;

        PrefabUtility.SaveAsPrefabAsset(car, path);
        Object.DestroyImmediate(car);
        Debug.Log($"[Cars] {name} built → {path} (wheel radius {minRadius:0.00}, body {body.size})");
    }

    static void Shift(Transform t, Vector3 d) { if (t != null) t.localPosition += d; }

    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
    }

    // ───────────────────────────── 3. Scenes ─────────────────────────────

    [MenuItem("Tools/Cars/3 Use MyCars In Scenes")]
    public static void UseInScenes()
    {
        EditorSceneManager.SaveOpenScenes();
        string previous = EditorSceneManager.GetActiveScene().path;
        SetupMainMenu();
        SetupGamePlay();
        SetupSplash();
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    static void SetupMainMenu()
    {
        var scene = EditorSceneManager.OpenScene(MainMenuScene, OpenSceneMode.Single);
        var mm = Object.FindFirstObjectByType<MainMenuMannager>(FindObjectsInactive.Include);
        var roots = scene.GetRootGameObjects().ToList();
        var cars = new GameObject[Models.Length];
        for (int i = 0; i < Models.Length; i++)
        {
            string n = "MyCar" + (i + 1);
            var go = roots.FirstOrDefault(r => r.name == n);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models[i]);
            var src = go != null ? PrefabUtility.GetCorrespondingObjectFromSource(go) : null;
            if (go == null || src != model)
            {
                // Missing, or the raw (unscaled, rigged) model → replace with the garage display model.
                int sib = go != null ? go.transform.GetSiblingIndex() : roots.Count;
                var fresh = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                fresh.name = n;
                fresh.transform.SetSiblingIndex(sib);
                if (go != null) Object.DestroyImmediate(go);
                go = fresh;
            }
            go.transform.localScale = Vector3.one;
            cars[i] = go;
        }

        var studio = GameObject.Find("Car Studio");
        VRModeManager.ParkShowcaseCars(cars, studio != null ? studio.transform : null);
        foreach (var c in cars) { c.SetActive(false); EditorUtility.SetDirty(c); }

        // Old garage cars → removed (their prefabs stay in the project).
        var old = mm.Cars ?? new GameObject[0];
        mm.Cars = cars;
        foreach (var o in old) if (o != null && !cars.Contains(o) && o.name.StartsWith("Car")) Object.DestroyImmediate(o);
        EditorUtility.SetDirty(mm);

        var preview = Object.FindFirstObjectByType<GarageCarPreview>(FindObjectsInactive.Include);
        if (preview != null && preview.cars != null && preview.cars.Length > 0) { preview.cars = cars; EditorUtility.SetDirty(preview); }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Cars] MainMenu garage now shows MyCar1..MyCar10.");
    }

    static readonly Vector3 GameplayCarPos = new Vector3(176.55f, 1.77f, 1758.40f);

    static void SetupGamePlay()
    {
        var scene = EditorSceneManager.OpenScene(GamePlayScene, OpenSceneMode.Single);
        var gm = Object.FindFirstObjectByType<GameMannager>(FindObjectsInactive.Include);
        var roots = scene.GetRootGameObjects().ToList();
        var old = gm.Cars ?? new GameObject[0];
        var cars = new GameObject[Models.Length];
        for (int i = 0; i < Models.Length; i++)
        {
            string n = "MyCar" + (i + 1);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DrivablePath(i));
            // Reuse an existing instance of the right prefab (MyCar1/2 = the old Car1/Car2 objects).
            var go = roots.FirstOrDefault(r => r.name == n && PrefabUtility.GetCorrespondingObjectFromSource(r) == prefab)
                  ?? old.FirstOrDefault(o => o != null && PrefabUtility.GetCorrespondingObjectFromSource(o) == prefab);
            if (go == null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.transform.SetPositionAndRotation(GameplayCarPos, Quaternion.identity);
                int sib = old.Length > 0 && old[old.Length - 1] != null ? old[old.Length - 1].transform.GetSiblingIndex() + 1 : go.transform.GetSiblingIndex();
                go.transform.SetSiblingIndex(sib);
            }
            go.name = n;
            go.SetActive(false);
            cars[i] = go;
        }
        gm.Cars = cars;

        // Arrow[] follows Cars[] (the "Arrow" child of each car).
        var arrowField = typeof(GameMannager).GetField("Arrow");
        if (arrowField != null && arrowField.FieldType.IsArray)
        {
            var et = arrowField.FieldType.GetElementType();
            var arr = Array.CreateInstance(et, cars.Length);
            for (int i = 0; i < cars.Length; i++)
            {
                var a = cars[i].transform.Find("Arrow");
                if (a == null) continue;
                arr.SetValue(et == typeof(GameObject) ? (object)a.gameObject : et == typeof(Transform) ? a : a.GetComponent(et), i);
            }
            arrowField.SetValue(gm, arr);
        }
        EditorUtility.SetDirty(gm);

        var fcm = Object.FindFirstObjectByType<FlyingCarManager>(FindObjectsInactive.Include);
        if (fcm != null) { fcm.preAttachVehicles = cars; EditorUtility.SetDirty(fcm); }

        foreach (var o in old) if (o != null && !cars.Contains(o)) Object.DestroyImmediate(o);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Cars] GamePlay now drives MyCar1..MyCar10.");
    }

    static void SetupSplash()
    {
        var scene = EditorSceneManager.OpenScene(SplashScene, OpenSceneMode.Single);
        var oa = Object.FindFirstObjectByType<OverAllGameMannager>(FindObjectsInactive.Include);
        if (oa == null) { Debug.LogWarning("[Cars] Splash: OverAllGameMannager not found"); return; }
        var list = oa.SaveData.Cars;
        for (int i = 0; i < Shop.Length; i++)
        {
            if (i >= list.Count) list.Add(new CarData { CarFuel = 100f });
            list[i].CarName = Shop[i].name;
            list[i].CarPrice = Shop[i].price;
            if (i == 0) list[i].isUnlocked = true;
        }
        while (list.Count > Shop.Length) list.RemoveAt(list.Count - 1);
        EditorUtility.SetDirty(oa);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Cars] Save data now has " + list.Count + " cars.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
