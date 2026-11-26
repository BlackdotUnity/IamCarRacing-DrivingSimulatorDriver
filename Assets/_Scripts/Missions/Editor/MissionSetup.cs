using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > Missions > Setup Missions (safe to run again):
///  • Resources/Missions/MissionDatabase.asset with all 100 missions (only created if missing)
///  • MissionBeam / MissionSolid materials
///  • MissionRunner in GamePlay.unity with all prefab references
///  • Chapter selector bar in MainMenu.unity's level panel
///  • Keeps exponential fog shader variants so night / storm missions work on device
/// </summary>
public static class MissionSetup
{
    const string GeneratedDir = "Assets/_Scripts/Missions/Generated";
    const string DatabasePath = "Assets/Resources/Missions/MissionDatabase.asset";
    const string GamePlayScene = "Assets/Scenes/GamePlay.unity";
    const string MainMenuScene = "Assets/Scenes/MainMenu.unity";
    const string Pack = "Assets/Complete Vehicle Pack V2/Prefabs/";

    [MenuItem("Tools/Missions/Setup Missions")]
    public static void SetupAll()
    {
        Directory.CreateDirectory(GeneratedDir);
        EnsureDatabase(false);
        var beam = EnsureBeamMaterial();
        var solid = EnsureSolidMaterial();
        KeepFogVariants();

        EditorSceneManager.SaveOpenScenes();
        string previous = EditorSceneManager.GetActiveScene().path;

        var gp = EditorSceneManager.OpenScene(GamePlayScene, OpenSceneMode.Single);
        SetupGamePlay(beam, solid);
        EditorSceneManager.MarkSceneDirty(gp);
        EditorSceneManager.SaveScene(gp);

        var mm = EditorSceneManager.OpenScene(MainMenuScene, OpenSceneMode.Single);
        SetupMainMenu();
        EditorSceneManager.MarkSceneDirty(mm);
        EditorSceneManager.SaveScene(mm);

        if (!string.IsNullOrEmpty(previous) && previous != MainMenuScene) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        AssetDatabase.SaveAssetIfDirty(beam);
        AssetDatabase.SaveAssetIfDirty(solid);
        Debug.Log("[Missions] Setup complete: database, materials, GamePlay runner and MainMenu chapter selector.");
    }

    [MenuItem("Tools/Missions/Rebuild Mission Database (overwrites edits)")]
    public static void RebuildDatabase()
    {
        if (!EditorUtility.DisplayDialog("Rebuild Mission Database",
            "Replace every mission in MissionDatabase.asset with the defaults from MissionCatalog.cs?\nInspector edits will be lost.", "Rebuild", "Cancel")) return;
        EnsureDatabase(true);
    }

    // ───────────────────────── assets ─────────────────────────

    static MissionDatabase EnsureDatabase(bool overwrite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
        var db = AssetDatabase.LoadAssetAtPath<MissionDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<MissionDatabase>();
            db.missions = MissionCatalog.Build();
            AssetDatabase.CreateAsset(db, DatabasePath);
        }
        else if (overwrite || db.missions == null || db.missions.Count == 0)
        {
            db.missions = MissionCatalog.Build();
            EditorUtility.SetDirty(db);
        }
        // Save only this asset (SaveAssets would also try to save dirty package prefabs).
        AssetDatabase.SaveAssetIfDirty(db);
        return db;
    }

    static Material EnsureBeamMaterial()
    {
        string path = GeneratedDir + "/MissionBeam.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Scripts/FlyingCar/Generated/FlyThruster.mat");
        if (template != null)
        {
            mat = new Material(template);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
        }
        else
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
        }
        mat.name = "MissionBeam";
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.enableInstancing = true;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material EnsureSolidMaterial()
    {
        string path = GeneratedDir + "/MissionSolid.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = "MissionSolid" };
        mat.SetColor("_BaseColor", Color.white);
        mat.enableInstancing = true;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>URP strips fog variants that no scene uses; night / storm missions turn fog on at runtime.</summary>
    static void KeepFogVariants()
    {
        var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (gs == null || gs.Length == 0) return;
        var so = new SerializedObject(gs[0]);
        var strip = so.FindProperty("m_FogStripping");
        var exp2 = so.FindProperty("m_FogKeepExp2");
        if (strip == null || exp2 == null) return;
        strip.intValue = 1; // Custom
        exp2.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject P(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    // ───────────────────────── GamePlay ─────────────────────────

    static void SetupGamePlay(Material beam, Material solid)
    {
        var runner = Object.FindFirstObjectByType<MissionRunner>(FindObjectsInactive.Include);
        if (runner == null) runner = new GameObject("MissionRunner").AddComponent<MissionRunner>();

        var p = runner.prefabs ?? new MissionPrefabs();
        p.beamMaterial = beam;
        p.solidMaterial = solid;
        p.cars = new[]
        {
            P(Pack + "Sedan_5A.prefab"), P(Pack + "Sedan_6A.prefab"), P(Pack + "Compact_3A.prefab"),
            P(Pack + "Compact_4A.prefab"), P(Pack + "Hybrid_1A.prefab"), P(Pack + "SUV_1B.prefab"), P(Pack + "PickUp_3A.prefab"),
        };
        p.taxi = P(Pack + "Taxi_2A.prefab");
        p.van = P(Pack + "Van_2A.prefab");
        p.truck = P(Pack + "Truck_1A.prefab");
        p.bus = P(Pack + "Bus_2A.prefab");
        p.police = P(Pack + "SUV_1A.prefab");
        p.armored = P(Pack + "Truck_1B.prefab");
        p.train = P(Pack + "Bus_2C.prefab");
        p.crate = P(Pack + "Crate_1A.prefab");
        p.helicopter = P(Pack + "Helicopter_1A.prefab");
        p.cone = P("Assets/Enviornment/PREFEB/trafic cone.prefab");
        p.barrier = P("Assets/Enviornment/PREFEB/road barrier.prefab");
        runner.prefabs = p;
        EditorUtility.SetDirty(runner);
    }

    // ───────────────────────── MainMenu ─────────────────────────

    static void SetupMainMenu()
    {
        var mm = Object.FindFirstObjectByType<MainMenuMannager>(FindObjectsInactive.Include);
        if (mm == null || mm.LevelPannel == null) { Debug.LogWarning("[Missions] MainMenuMannager / LevelPannel not found."); return; }

        var panel = mm.LevelPannel.transform;
        var existing = panel.Find("ChapterSelector");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // Style from the existing header title.
        Font font = null;
        Sprite titleSprite = null;
        Color titleColor = new Color(0.06f, 0.06f, 0.06f);
        var header = panel.Find("Header/Title");
        if (header != null)
        {
            var img = header.GetComponent<Image>();
            if (img != null) titleSprite = img.sprite;
            var t = header.GetComponentInChildren<Text>(true);
            if (t != null) { font = t.font; titleColor = t.color; }
        }
        if (font == null) font = AssetDatabase.LoadAssetAtPath<Font>(FindFont());

        var bar = new GameObject("ChapterSelector", typeof(RectTransform));
        bar.transform.SetParent(panel, false);
        var rt = (RectTransform)bar.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -132f);
        rt.sizeDelta = new Vector2(1100f, 96f);
        var scroll = panel.Find("Scroll View");
        if (scroll != null) bar.transform.SetSiblingIndex(scroll.GetSiblingIndex() + 1);

        // Title plate
        var plate = NewUI("Title", bar.transform, new Vector2(0f, 10f), new Vector2(820f, 70f));
        var plateImg = plate.AddComponent<Image>();
        plateImg.sprite = titleSprite;
        plateImg.type = titleSprite != null && titleSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        if (titleSprite == null) plateImg.color = new Color(1f, 0.8f, 0.2f);
        var title = NewText("TitleText", plate.transform, font, 34, titleColor);
        Stretch((RectTransform)title.transform);

        // Info line
        var info = NewText("InfoText", bar.transform, font, 22, new Color(0.75f, 0.95f, 1f));
        var irt = (RectTransform)info.transform;
        irt.anchoredPosition = new Vector2(0f, -52f);
        irt.sizeDelta = new Vector2(820f, 30f);
        info.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);

        // Arrows
        var arrow = EnsureArrowSprite();
        var prev = NewButton("PrevChapter", bar.transform, new Vector2(-480f, 10f), arrow, true);
        var next = NewButton("NextChapter", bar.transform, new Vector2(480f, 10f), arrow, false);

        var sel = bar.AddComponent<ChapterSelectorUI>();
        sel.prevButton = prev;
        sel.nextButton = next;
        sel.titleText = title;
        sel.infoText = info;
        EditorUtility.SetDirty(mm);
    }

    static string FindFont()
    {
        foreach (var g in AssetDatabase.FindAssets("FredokaOne t:Font"))
            return AssetDatabase.GUIDToAssetPath(g);
        return "";
    }

    static GameObject NewUI(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return go;
    }

    static Text NewText(string name, Transform parent, Font font, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Button NewButton(string name, Transform parent, Vector2 pos, Sprite sprite, bool flip)
    {
        var go = NewUI(name, parent, pos, new Vector2(92f, 92f));
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        if (flip) go.transform.localScale = new Vector3(-1f, 1f, 1f);
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        b.colors = colors;
        return b;
    }

    /// <summary>Round neon button with a right-pointing chevron, drawn in code.</summary>
    static Sprite EnsureArrowSprite()
    {
        string path = GeneratedDir + "/ChapterArrow.png";
        if (!File.Exists(path))
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color[S * S];
            Vector2 c = new Vector2(S / 2f, S / 2f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float disc = Mathf.Clamp01(60f - d);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 57f) / 3f);
                    Color col = Color.Lerp(new Color(0.05f, 0.1f, 0.2f, 0.92f * disc), new Color(0.2f, 0.9f, 1f, 1f), ring);
                    // Chevron
                    float u = (x - c.x) / 30f, v = (y - c.y) / 30f;
                    float chev = Mathf.Abs(v) - (0.9f - u);
                    float arm = Mathf.Abs(chev) < 0.28f && u > -0.35f && u < 0.95f ? 1f : 0f;
                    col = Color.Lerp(col, Color.white, arm * disc);
                    col.a = Mathf.Max(col.a * disc, ring);
                    px[y * S + x] = col;
                }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
