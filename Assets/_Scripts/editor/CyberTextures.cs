using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// Procedural detail textures for the Cybertruck (the model ships without any): brushed stainless steel,
/// leather grain, soft-touch dashboard grain, rubber and a centre-screen image. All tile seamlessly and are
/// mapped with the box-projected UVs MyCarsSetup gives the truck (1 UV unit = 1 metre).
/// Small (≤512 px, ASTC on Quest) and shared by both Cybertruck prefabs.
/// </summary>
public static class CyberTextures
{
    const int N = 512;

    public static void Generate(string dir)
    {
        if (!AssetDatabase.IsValidFolder(dir))
        {
            var parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
        }
        Save(dir + "/CT_BrushedSteel.png", BrushedSteel(), false);
        Save(dir + "/CT_LeatherGrain.png", Grain(7, 0.86f, 0.14f, 2, 6), false);
        Save(dir + "/CT_SoftTouch.png", Grain(11, 0.9f, 0.1f, 1, 3), false);
        Save(dir + "/CT_Rubber.png", Grain(23, 0.85f, 0.15f, 1, 2), false);
        Save(dir + "/CT_Screen.png", Screen(), true);
        AssetDatabase.Refresh();
    }

    /// Puts the textures on the Cybertruck materials (called after FixCyberMaterials set the colours).
    public static void Apply(string matDir, string texDir)
    {
        // material → texture, metres per tile, colour multiplier
        Set(matDir, "CT_Stainless_Steel", texDir + "/CT_BrushedSteel.png", 1.2f, new Color(0.76f, 0.77f, 0.79f), 0.6f);
        Set(matDir, "CT_Chassis", texDir + "/CT_BrushedSteel.png", 1.2f, new Color(0.72f, 0.73f, 0.75f), 0.5f);
        // Charcoal leather / dark-grey dash: real Cybertruck black interior, lifted enough to read in VR.
        Set(matDir, "CT_Interior_Seat", texDir + "/CT_LeatherGrain.png", 0.25f, new Color(0.31f, 0.31f, 0.33f), 0.45f);
        Set(matDir, "CT_Interior_Dark", texDir + "/CT_SoftTouch.png", 0.4f, new Color(0.16f, 0.16f, 0.17f), 0.3f);
        Set(matDir, "CT_Trim_Dark", texDir + "/CT_SoftTouch.png", 0.5f, new Color(0.1f, 0.1f, 0.11f), 0.3f);
        Set(matDir, "CT_Tire", texDir + "/CT_Rubber.png", 0.2f, new Color(0.07f, 0.07f, 0.075f), 0.18f);
        Set(matDir, "CT_Rim_Cover", texDir + "/CT_SoftTouch.png", 0.3f, new Color(0.13f, 0.13f, 0.14f), 0.45f);
        Set(matDir, "CT_Interior_Metal", texDir + "/CT_BrushedSteel.png", 0.5f, new Color(0.6f, 0.61f, 0.63f), 0.65f);

        // Centre screen: the image fills it and glows a little (readable in the dark cabin).
        var screen = Load(matDir, "CT_Screen");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/CT_Screen.png");
        if (screen != null && tex != null)
        {
            screen.SetTexture("_BaseMap", tex);
            screen.SetTextureScale("_BaseMap", Vector2.one);
            screen.SetColor("_BaseColor", Color.white);
            screen.SetTexture("_EmissionMap", tex);
            screen.SetColor("_EmissionColor", new Color(0.85f, 0.85f, 0.85f));
            screen.EnableKeyword("_EMISSION");
            screen.SetFloat("_Smoothness", 0.9f);
            EditorUtility.SetDirty(screen);
        }

        // Tinted glass with a crisp highlight.
        var glass = Load(matDir, "CT_Glass");
        if (glass != null) { glass.SetColor("_BaseColor", new Color(0.06f, 0.08f, 0.1f, 0.45f)); glass.SetFloat("_Smoothness", 0.97f); glass.SetColor("_SpecColor", new Color(0.9f, 0.9f, 0.9f)); EditorUtility.SetDirty(glass); }
        AssetDatabase.SaveAssets();
    }

    /// Glossy leather yoke: its own material so it reads against the dark dashboard.
    public static Material YokeMaterial(string matDir)
    {
        string path = matDir + "/CT_Yoke.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var src = Load(matDir, "CT_Interior_Seat");
        if (m == null) { m = new Material(src != null ? src : new Material(Shader.Find("Universal Render Pipeline/Simple Lit"))); AssetDatabase.CreateAsset(m, path); }
        else if (src != null) m.CopyPropertiesFromMaterial(src);
        m.name = "CT_Yoke";
        m.SetColor("_BaseColor", new Color(0.16f, 0.16f, 0.17f));
        m.SetFloat("_Smoothness", 0.7f);
        m.SetColor("_SpecColor", new Color(0.45f, 0.45f, 0.45f));
        m.SetTextureScale("_BaseMap", Vector2.one * 6f);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static Material Load(string dir, string name) => AssetDatabase.LoadAssetAtPath<Material>(dir + "/" + name + ".mat");

    static void Set(string matDir, string mat, string texPath, float metresPerTile, Color tint, float smoothness)
    {
        var m = Load(matDir, mat);
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (m == null || t == null) return;
        m.SetTexture("_BaseMap", t);
        m.SetTextureScale("_BaseMap", Vector2.one / metresPerTile);
        var a = m.GetColor("_BaseColor").a;
        tint.a = a;
        m.SetColor("_BaseColor", tint);
        m.SetColor("_Color", tint);
        m.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(m);
    }

    // ───────────────────────── generators (all tile seamlessly) ─────────────────────────

    static Color[] BrushedSteel()
    {
        var rnd = new Random(3);
        var px = new Color[N * N];
        // Every row has its own brightness that drifts slowly along it: long horizontal brush streaks.
        var rowBase = new float[N]; var rowAmp = new float[N]; var rowPhase = new float[N]; var rowFreq = new int[N];
        for (int y = 0; y < N; y++) { rowBase[y] = (float)rnd.NextDouble(); rowAmp[y] = (float)rnd.NextDouble() * 0.5f; rowPhase[y] = (float)(rnd.NextDouble() * Math.PI * 2); rowFreq[y] = 1 + rnd.Next(4); }
        // Soften between rows (wrap-around) so the streaks are fine, not stripy.
        var soft = new float[N];
        for (int y = 0; y < N; y++) soft[y] = (rowBase[(y + N - 1) % N] + 2f * rowBase[y] + rowBase[(y + 1) % N]) / 4f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float drift = rowAmp[y] * Mathf.Sin(2f * Mathf.PI * rowFreq[y] * x / N + rowPhase[y]);
                float v = 0.88f + (soft[y] - 0.5f) * 0.07f + drift * 0.025f; // fine satin brushing, not scratches
                px[y * N + x] = new Color(v, v, v * 1.01f, 1f);
            }
        return px;
    }

    /// Tileable grain: blurred white noise at two scales around `mean`.
    static Color[] Grain(int seed, float mean, float contrast, int fineBlur, int coarseBlur)
    {
        var fine = Blur(Noise(seed), fineBlur);
        var coarse = Blur(Noise(seed * 7 + 1), coarseBlur);
        Normalize(fine); Normalize(coarse);
        var px = new Color[N * N];
        for (int i = 0; i < px.Length; i++)
        {
            float v = mean + ((fine[i] - 0.5f) * 0.7f + (coarse[i] - 0.5f) * 0.3f) * contrast * 2f;
            px[i] = new Color(v, v, v, 1f);
        }
        return px;
    }

    static float[] Noise(int seed)
    {
        var rnd = new Random(seed); var a = new float[N * N];
        for (int i = 0; i < a.Length; i++) a[i] = (float)rnd.NextDouble();
        return a;
    }

    static float[] Blur(float[] a, int r)
    {
        if (r <= 0) return a;
        var tmp = new float[a.Length]; var outp = new float[a.Length];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float s = 0; for (int k = -r; k <= r; k++) s += a[y * N + (x + k + N) % N]; tmp[y * N + x] = s / (2 * r + 1); }
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float s = 0; for (int k = -r; k <= r; k++) s += tmp[((y + k + N) % N) * N + x]; outp[y * N + x] = s / (2 * r + 1); }
        return outp;
    }

    static void Normalize(float[] a)
    {
        float mn = float.MaxValue, mx = float.MinValue;
        foreach (var v in a) { if (v < mn) mn = v; if (v > mx) mx = v; }
        for (int i = 0; i < a.Length; i++) a[i] = (a[i] - mn) / Mathf.Max(1e-5f, mx - mn);
    }

    /// Centre screen: car view on the left, navigation map with a route on the right (landscape, like the real one).
    static Color[] Screen()
    {
        int W = N, H = N;
        var px = new Color[W * H];
        var bg = new Color(0.05f, 0.06f, 0.08f);
        var map = new Color(0.12f, 0.14f, 0.17f);
        var road = new Color(0.28f, 0.31f, 0.36f);
        var route = new Color(0.2f, 0.55f, 1f);
        var white = new Color(0.92f, 0.94f, 0.97f);
        for (int i = 0; i < px.Length; i++) px[i] = bg;
        int split = (int)(W * 0.36f);
        // Map
        Rect(px, W, split + 6, 6, W - 6, H - 6, map);
        var rnd = new Random(5);
        for (int k = 0; k < 9; k++) { int y = 20 + rnd.Next(H - 40); Rect(px, W, split + 6, y, W - 6, y + 3 + rnd.Next(4), road); }
        for (int k = 0; k < 7; k++) { int x = split + 20 + rnd.Next(W - split - 40); Rect(px, W, x, 6, x + 3 + rnd.Next(4), H - 6, road); }
        // Route: up the middle then right
        int rx = split + (W - split) / 2;
        Rect(px, W, rx - 4, 40, rx + 4, H / 2 + 4, route);
        Rect(px, W, rx - 4, H / 2 - 4, W - 40, H / 2 + 4, route);
        Disc(px, W, W - 40, H / 2, 12, new Color(0.95f, 0.25f, 0.25f));
        // You-are-here arrow
        for (int dy = 0; dy < 26; dy++) { int half = dy / 2; Rect(px, W, rx - half, 30 + dy, rx + half + 1, 31 + dy, white); }
        // Left: the truck from above + battery / speed bars
        Rect(px, W, split / 2 - 34, H / 2 - 110, split / 2 + 34, H / 2 + 110, new Color(0.7f, 0.72f, 0.75f));
        Rect(px, W, split / 2 - 26, H / 2 + 10, split / 2 + 26, H / 2 + 70, new Color(0.2f, 0.22f, 0.26f)); // glass roof
        Rect(px, W, 24, H - 60, split - 24, H - 46, new Color(0.25f, 0.27f, 0.3f));
        Rect(px, W, 24, H - 60, (int)(24 + (split - 48) * 0.78f), H - 46, new Color(0.3f, 0.85f, 0.45f)); // battery
        Rect(px, W, 24, 40, split - 24, 52, new Color(0.25f, 0.27f, 0.3f));
        Rect(px, W, 24, 40, (int)(24 + (split - 48) * 0.45f), 52, white); // speed
        Rect(px, W, split, 0, split + 6, H, new Color(0.02f, 0.02f, 0.03f));
        return px;
    }

    static void Rect(Color[] px, int W, int x0, int y0, int x1, int y1, Color c)
    {
        int H = px.Length / W;
        for (int y = Mathf.Max(0, y0); y < Mathf.Min(H, y1); y++) for (int x = Mathf.Max(0, x0); x < Mathf.Min(W, x1); x++) px[y * W + x] = c;
    }

    static void Disc(Color[] px, int W, int cx, int cy, int r, Color c)
    {
        for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++) if (x * x + y * y <= r * r) Rect(px, W, cx + x, cy + y, cx + x + 1, cy + y + 1, c);
    }

    static void Save(string path, Color[] px, bool clamp)
    {
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        t.SetPixels(px); t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.mipmapEnabled = true;
        ti.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        ti.anisoLevel = 2;
        ti.maxTextureSize = N;
        var a = ti.GetPlatformTextureSettings("Android");
        a.overridden = true; a.maxTextureSize = N; a.format = TextureImporterFormat.ASTC_6x6;
        ti.SetPlatformTextureSettings(a);
        ti.SaveAndReimport();
    }
}
