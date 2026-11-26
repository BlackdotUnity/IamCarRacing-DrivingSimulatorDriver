using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > Flying Car > Setup Gameplay Scene
/// Creates (once) the default profile, particle material and Fly button sprites, then adds the
/// FlyingCarManager and the Fly button / status HUD to the open scene. Safe to run repeatedly:
/// anything that already exists is reused, nothing existing is renamed or deleted.
/// </summary>
public static class FlyingCarSetup
{
    const string Root = "Assets/_Scripts/FlyingCar/Generated";
    const string ProfilePath = Root + "/DefaultFlyingProfile.asset";
    const string ParticleTexPath = Root + "/FlyParticle.png";
    const string MaterialPath = Root + "/FlyThruster.mat";
    const string IconPath = Root + "/FlyButtonIcon.png";
    const string GlowPath = Root + "/FlyButtonGlow.png";

    [MenuItem("Tools/Flying Car/Setup Gameplay Scene")]
    public static void SetupScene()
    {
        CreateAssets(out var profile, out var material, out var icon, out var glow);

        var mgr = Object.FindFirstObjectByType<FlyingCarManager>(FindObjectsInactive.Include);
        if (!mgr)
        {
            var go = new GameObject("FlyingCarManager");
            Undo.RegisterCreatedObjectUndo(go, "Create FlyingCarManager");
            mgr = go.AddComponent<FlyingCarManager>();
        }
        var camAdapter = mgr.GetComponent<FlyingCarCameraAdapter>();
        if (!camAdapter) camAdapter = Undo.AddComponent<FlyingCarCameraAdapter>(mgr.gameObject);
        var fx = mgr.GetComponent<FlyingCarEffects>();
        if (!fx) fx = Undo.AddComponent<FlyingCarEffects>(mgr.gameObject);

        Undo.RecordObject(mgr, "Setup FlyingCarManager");
        if (!mgr.defaultProfile) mgr.defaultProfile = profile;
        mgr.cameraAdapter = camAdapter;
        mgr.effects = fx;

        var gm = Object.FindFirstObjectByType<GameMannager>(FindObjectsInactive.Include);
        if (gm && gm.Cars != null && (mgr.preAttachVehicles == null || mgr.preAttachVehicles.Length == 0))
            mgr.preAttachVehicles = (GameObject[])gm.Cars.Clone();
        EditorUtility.SetDirty(mgr);

        Undo.RecordObject(fx, "Setup FlyingCarEffects");
        if (!fx.particleMaterial) fx.particleMaterial = material;
        EditorUtility.SetDirty(fx);

        BuildUI(icon, glow);

        EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
        Selection.activeGameObject = mgr.gameObject;
        Debug.Log("[FlyingCar] Scene setup complete. Save the scene to keep the changes.");
    }

    // ───────────────────────── UI ─────────────────────────

    static void BuildUI(Sprite icon, Sprite glow)
    {
        Transform parent = FindControlsParent();
        if (!parent)
        {
            Debug.LogWarning("[FlyingCar] No Canvas found – Fly button and HUD were not created.");
            return;
        }
        Font font = FindFont();

        // ── Fly button (bottom-right, above the Brake pedal) ──
        if (!Object.FindFirstObjectByType<UIFlyButton>(FindObjectsInactive.Include))
        {
            var btn = NewUI("FlyButton", parent);
            var rt = (RectTransform)btn.transform;
            SetRect(rt, new Vector2(1f, 0f), new Vector2(-341f, 362f), new Vector2(175f, 175f));
            var hit = btn.AddComponent<Image>();           // invisible, generous touch area
            hit.color = new Color(1f, 1f, 1f, 0f);

            var glowGo = NewUI("Glow", btn.transform);
            SetStretch((RectTransform)glowGo.transform, -22f);
            var glowImg = glowGo.AddComponent<Image>();
            glowImg.sprite = glow;
            glowImg.raycastTarget = false;
            glowImg.color = new Color(0.35f, 0.85f, 1f, 0f);

            var iconGo = NewUI("Icon", btn.transform);
            SetStretch((RectTransform)iconGo.transform, 0f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var labelGo = NewUI("Label", btn.transform);
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = new Vector2(0.5f, 0f); lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, 4f);
            lrt.sizeDelta = new Vector2(200f, 44f);
            var label = labelGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 32;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "FLY";
            label.raycastTarget = false;
            labelGo.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);

            var fly = btn.AddComponent<UIFlyButton>();
            fly.background = iconImg;
            fly.glow = glowImg;
            fly.label = label;
        }

        // ── Status HUD (top centre, under the level counter) ──
        if (!Object.FindFirstObjectByType<FlyingCarHUD>(FindObjectsInactive.Include))
        {
            var hudGo = NewUI("FlyStatusHUD", parent);
            SetRect((RectTransform)hudGo.transform, new Vector2(0.5f, 1f), new Vector2(17f, -138f), new Vector2(340f, 74f));
            var hud = hudGo.AddComponent<FlyingCarHUD>();

            var content = NewUI("Content", hudGo.transform);
            SetStretch((RectTransform)content.transform, 0f);
            var bg = content.AddComponent<Image>();
            bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.05f, 0.08f, 0.12f, 0.6f);
            bg.raycastTarget = false;

            var dot = NewUI("StatusDot", content.transform);
            SetRect((RectTransform)dot.transform, new Vector2(0f, 0.5f), new Vector2(26f, 14f), new Vector2(20f, 20f));
            var dotImg = dot.AddComponent<Image>();
            dotImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            dotImg.raycastTarget = false;

            var status = MakeText("Status", content.transform, font, 26, TextAnchor.MiddleLeft,
                new Vector2(0f, 0.5f), new Vector2(46f, 14f), new Vector2(170f, 36f), new Vector2(0f, 0.5f));
            status.text = "DRIVE";

            var alt = MakeText("Altitude", content.transform, font, 22, TextAnchor.MiddleRight,
                new Vector2(1f, 0.5f), new Vector2(-14f, 14f), new Vector2(130f, 36f), new Vector2(1f, 0.5f));
            alt.text = "";

            var barBg = NewUI("EnergyBar", content.transform);
            var barRt = (RectTransform)barBg.transform;
            barRt.anchorMin = new Vector2(0f, 0f); barRt.anchorMax = new Vector2(1f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.offsetMin = new Vector2(16f, 10f); barRt.offsetMax = new Vector2(-70f, 24f);
            var barBgImg = barBg.AddComponent<Image>();
            barBgImg.color = new Color(1f, 1f, 1f, 0.15f);
            barBgImg.raycastTarget = false;

            var fillGo = NewUI("Fill", barBg.transform);
            SetStretch((RectTransform)fillGo.transform, 0f);
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            fill.color = new Color(0.2f, 0.75f, 1f, 1f);
            fill.raycastTarget = false;

            var energy = MakeText("EnergyText", content.transform, font, 20, TextAnchor.MiddleRight,
                new Vector2(1f, 0f), new Vector2(-12f, 17f), new Vector2(60f, 26f), new Vector2(1f, 0.5f));
            energy.text = "100%";

            hud.content = content;
            hud.statusText = status;
            hud.statusDot = dotImg;
            hud.energyFill = fill;
            hud.energyText = energy;
            hud.altitudeText = alt;
        }
    }

    static Transform FindControlsParent()
    {
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            var t = c.transform.Find("GamePlayPanel/Controller Buttons");
            if (t) return t;
        }
        foreach (var c in canvases)
            if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace) return c.transform;
        return null;
    }

    static Font FindFont()
    {
        foreach (var guid in AssetDatabase.FindAssets("FredokaOne-Regular t:Font"))
        {
            var f = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
            if (f) return f;
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void SetStretch(RectTransform rt, float outset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-outset, -outset);
        rt.offsetMax = new Vector2(outset, outset);
    }

    static Text MakeText(string name, Transform parent, Font font, int size, TextAnchor align,
        Vector2 anchor, Vector2 pos, Vector2 sizeDelta, Vector2 pivot)
    {
        var go = NewUI(name, parent);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    // ───────────────────────── Assets ─────────────────────────

    static void CreateAssets(out FlyingCarProfile profile, out Material material, out Sprite icon, out Sprite glow)
    {
        Directory.CreateDirectory(Root);

        profile = AssetDatabase.LoadAssetAtPath<FlyingCarProfile>(ProfilePath);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<FlyingCarProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        if (!File.Exists(ParticleTexPath)) WritePng(ParticleTexPath, DrawSoftDot(64));
        if (!File.Exists(IconPath)) WritePng(IconPath, DrawFlyIcon(256));
        if (!File.Exists(GlowPath)) WritePng(GlowPath, DrawGlowRing(256));
        AssetDatabase.Refresh();
        ConfigureTexture(ParticleTexPath, false);
        ConfigureTexture(IconPath, true);
        ConfigureTexture(GlowPath, true);

        material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (!material)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            material = new Material(s);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleTexPath));
            material.SetColor("_BaseColor", Color.white);
            // Transparent + Additive
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        glow = AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
        AssetDatabase.SaveAssets();
    }

    static void ConfigureTexture(string path, bool sprite)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (!imp) return;
        bool changed = false;
        if (sprite && imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; changed = true; }
        if (imp.mipmapEnabled && sprite) { imp.mipmapEnabled = false; changed = true; }
        if (changed) imp.SaveAndReimport();
    }

    static void WritePng(string path, Texture2D tex)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static Texture2D DrawSoftDot(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }

    static Texture2D DrawGlowRing(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) * 0.5f;
        float ringR = c * 0.78f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Exp(-Mathf.Pow((d - ringR) / (c * 0.11f), 2f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }

    /// <summary>Grey disc + neon-blue hover-car icon, matching the existing RCC/play UI buttons.</summary>
    static Texture2D DrawFlyIcon(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float s = size / 128f;
        float c = (size - 1) * 0.5f;

        // Icon strokes in a 128-unit, y-down design space.
        var segs = new List<Vector4>();
        void Poly(params float[] p) { for (int i = 0; i + 3 < p.Length; i += 2) segs.Add(new Vector4(p[i], p[i + 1], p[i + 2], p[i + 3])); }
        Poly(34, 74, 94, 74, 94, 62, 82, 59, 73, 49, 53, 49, 44, 59, 34, 62, 34, 74); // car body
        Poly(56, 52, 70, 52);                                                        // window line
        Poly(38, 84, 54, 84);                                                        // hover pads
        Poly(74, 84, 90, 84);
        Poly(41, 92, 51, 92);                                                        // thrust streaks
        Poly(77, 92, 87, 92);
        Poly(50, 38, 64, 25, 78, 38);                                                // up chevron
        Poly(55, 44, 64, 35, 73, 44);

        Color core = new Color(0.86f, 0.96f, 1f, 1f);
        Color neon = new Color(0.25f, 0.62f, 1f, 1f);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / c;   // 0 centre .. 1 edge
                if (r > 1.0f) { tex.SetPixel(x, y, Color.clear); continue; }

                // Disc: subtle vertical gradient + light rim, anti-aliased edge.
                float shade = Mathf.Lerp(0.30f, 0.42f, (float)y / size);
                Color col = new Color(shade, shade, shade, 1f);
                float rim = Mathf.Clamp01(1f - Mathf.Abs(r - 0.94f) / 0.04f);
                col = Color.Lerp(col, new Color(0.62f, 0.62f, 0.62f, 1f), rim * 0.8f);
                col.a = Mathf.Clamp01((1f - r) * c);

                // Neon strokes
                Vector2 p = new Vector2(x / s, (size - 1 - y) / s);
                float d = float.MaxValue;
                for (int i = 0; i < segs.Count; i++) d = Mathf.Min(d, SegDist(p, segs[i]));
                float glowA = Mathf.Exp(-d * d / 18f) * 0.55f;
                float coreA = Mathf.Clamp01((2.4f - d) * s * 0.5f);
                col = Color.Lerp(col, neon, glowA);
                col = Color.Lerp(col, core, coreA);

                tex.SetPixel(x, y, col);
            }
        tex.Apply();
        return tex;
    }

    static float SegDist(Vector2 p, Vector4 seg)
    {
        Vector2 a = new Vector2(seg.x, seg.y), b = new Vector2(seg.z, seg.w);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }
}
