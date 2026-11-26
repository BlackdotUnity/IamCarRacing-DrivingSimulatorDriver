using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Coin / key icons and the "KEY PACK" store card, drawn in code (the project had no standalone icons).</summary>
public static class EconomyIcons
{
    const string Dir = "Assets/_Scripts/Economy/Generated";

    public static Sprite Coin() => Ensure("CoinIcon.png", 128, DrawCoin);
    public static Sprite Key() => Ensure("KeyIcon.png", 128, (px, s) => DrawKey(px, s, s, new Vector2(s * 0.5f, s * 0.5f), s * 0.42f, 0f));

    /// <summary>"Remove Ads" store card redrawn as a key card.</summary>
    public static Sprite KeyPackCard() => CoverAdsSign("Assets/UI/Store/remove-ads.png", "KeyPackCard.png", 0.5f, 0.51f, 0.36f, true);

    /// <summary>"Ultimate Mega Pack" card without the "no ads" sign (the game has no ads now). Star, not a key: the pack gives no keys.</summary>
    public static Sprite MegaPackCard() => CoverAdsSign("Assets/UI/Store/unlock-all-thimng-or-game 1.png", "MegaPackCard2.png", 0.293f, 0.527f, 0.19f, false);

    /// <summary>Main-menu "Unlock every thing" banner without the "no ads" sign.</summary>
    public static Sprite UnlockBanner() => CoverAdsSign("Assets/UI/main ui/Unlock-evry-thng 1.png", "UnlockBanner.png", 0.783f, 0.525f, 0.165f, false);

    /// <summary>Paints a glow over the "no ads" sign (centre / radius as fractions, y from the top) and draws a key or a star.</summary>
    static Sprite CoverAdsSign(string src, string file, float cx, float cyFromTop, float radius, bool key)
    {
        string path = Dir + "/" + file;
        if (!File.Exists(path))
        {
            if (!File.Exists(src)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(src));
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels();

            Vector2 c = new Vector2(w * cx, h * (1f - cyFromTop));
            float r = w * radius;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float k = Mathf.Clamp01((r + 6f - d) / 6f);
                    if (k <= 0f) continue;
                    float glow = Mathf.Clamp01(1f - d / (r + 6f));
                    Color g = Color.Lerp(new Color(1f, 0.82f, 0.2f), new Color(1f, 0.98f, 0.78f), glow);
                    int i = y * w + x;
                    px[i] = Color.Lerp(px[i], new Color(g.r, g.g, g.b, Mathf.Max(px[i].a, 1f)), k);
                }
            if (key) DrawKey(px, w, h, c, r * 0.95f, -35f);
            else DrawStar(px, w, h, c, r * 0.8f);
            tex.SetPixels(px);
            tex.Apply();
            Save(tex, path);
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ───────────────────────── drawing ─────────────────────────

    static Sprite Ensure(string file, int size, System.Action<Color[], int> draw)
    {
        string path = Dir + "/" + file;
        if (!File.Exists(path))
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            draw(px, size);
            tex.SetPixels(px);
            tex.Apply();
            Save(tex, path);
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void Save(Texture2D tex, string path)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
    }

    static void Blend(Color[] px, int w, int x, int y, Color c, float a)
    {
        if (a <= 0f || x < 0 || y < 0 || x >= w || y >= px.Length / w) return;
        int i = y * w + x;
        Color d = px[i];
        float outA = a + d.a * (1f - a);
        if (outA <= 0f) return;
        px[i] = new Color((c.r * a + d.r * d.a * (1f - a)) / outA, (c.g * a + d.g * d.a * (1f - a)) / outA, (c.b * a + d.b * d.a * (1f - a)) / outA, outA);
    }

    static void DrawCoin(Color[] px, int s)
    {
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        float r = s * 0.46f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.Distance(p, c);
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (a <= 0f) continue;
                float shade = Mathf.Clamp01(0.55f + (p.y - c.y) / (2f * r) * 0.6f);
                Color col = Color.Lerp(new Color(0.85f, 0.5f, 0.05f), new Color(1f, 0.85f, 0.25f), shade);
                if (d > r * 0.8f) col *= 0.82f;                                      // rim
                if (Mathf.Abs(d - r * 0.62f) < s * 0.025f) col = new Color(0.8f, 0.5f, 0.05f); // inner ring
                col.a = 1f;
                Blend(px, s, x, y, col, a);
            }
        // Star in the middle.
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - c;
                float ang = Mathf.Atan2(p.y, p.x) + Mathf.PI / 2f;
                float star = Mathf.Lerp(s * 0.13f, s * 0.3f, Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2.5f)), 3f));
                float a = Mathf.Clamp01(star - p.magnitude + 0.5f);
                Blend(px, s, x, y, new Color(1f, 0.96f, 0.7f, 1f), a);
            }
    }

    /// <summary>Gold key: ring head, shaft and two teeth. `size` ≈ half length, rotated by `angle` degrees.</summary>
    static void DrawKey(Color[] px, int w, int h, Vector2 center, float size, float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
        Color gold = new Color(1f, 0.78f, 0.12f), dark = new Color(0.6f, 0.35f, 0.02f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 q = new Vector2(x + 0.5f, y + 0.5f) - center;
                // Rotate into key space: +x along the shaft.
                Vector2 p = new Vector2(q.x * cs + q.y * sn, -q.x * sn + q.y * cs) / size;
                float dHead = Mathf.Abs(Vector2.Distance(p, new Vector2(-0.58f, 0f)) - 0.27f) - 0.13f;   // ring
                float dShaft = Box(p - new Vector2(0.25f, 0f), new Vector2(0.62f, 0.085f));
                float dTooth1 = Box(p - new Vector2(0.62f, -0.17f), new Vector2(0.07f, 0.12f));
                float dTooth2 = Box(p - new Vector2(0.4f, -0.14f), new Vector2(0.06f, 0.09f));
                float d = Mathf.Min(Mathf.Min(dHead, dShaft), Mathf.Min(dTooth1, dTooth2));
                float px1 = 1f / size;
                float outline = Mathf.Clamp01((px1 * 2.5f - d) / px1);
                float fill = Mathf.Clamp01((-d) / px1);
                if (outline > 0f) Blend(px, w, x, y, dark, outline);
                if (fill > 0f)
                {
                    Color col = Color.Lerp(gold, new Color(1f, 0.95f, 0.6f), Mathf.Clamp01(0.5f + p.y * 1.4f));
                    Blend(px, w, x, y, col, Mathf.Clamp01(fill * 1.5f - 0.2f));
                }
            }
    }

    static void DrawStar(Color[] px, int w, int h, Vector2 c, float r)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - c;
                float ang = Mathf.Atan2(p.y, p.x) - Mathf.PI / 2f;
                float edge = Mathf.Lerp(r * 0.42f, r, Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2.5f)), 3f));
                float d = edge - p.magnitude;
                Blend(px, w, x, y, new Color(0.75f, 0.42f, 0.02f), Mathf.Clamp01(d + 2.5f));
                if (d > 0f) Blend(px, w, x, y, Color.Lerp(new Color(1f, 0.75f, 0.1f), new Color(1f, 0.97f, 0.7f), Mathf.Clamp01(0.5f + p.y / (2f * r))), Mathf.Clamp01(d));
            }
    }

    static float Box(Vector2 p, Vector2 half)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x) - half.x, Mathf.Abs(p.y) - half.y);
        return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
    }
}
