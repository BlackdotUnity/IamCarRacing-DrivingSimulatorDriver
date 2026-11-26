using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;

/// <summary>
/// Tools > VR > Configure Quest Project (safe to run again).
///  • Android XR: OpenXR loader + Meta XR feature set + Meta Quest support + Touch controller profiles
///  • Quest player settings: Vulkan, Multiview, ARM64/IL2CPP, ASTC, landscape, min API 32
///  • Runs Meta's Project Setup Tool "Fix All" for the remaining recommended settings
/// </summary>
public static class QuestProjectSetup
{
    const BuildTargetGroup Android = BuildTargetGroup.Android;

    [MenuItem("Tools/VR/Configure Quest Project")]
    public static void Configure()
    {
        var log = new StringBuilder();
        ConfigureXRManagement(log);
        ConfigureOpenXRFeatures(log);
        ConfigurePlayer(log);
        AssetDatabase.SaveAssets();
        Debug.Log("[Quest] Configured:\n" + log);
        RunMetaFixAll();
    }

    /// <summary>
    /// Quest GPU budget: the Android quality level ("Mobile") and its URP asset get VR-friendly values.
    /// The PC quality level / PC_RPAsset are untouched.
    /// </summary>
    [MenuItem("Tools/VR/Apply Quest Performance Settings")]
    public static void ApplyPerformance()
    {
        var log = new StringBuilder();
        int androidLevel = 0;
        var qso = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var defaults = qso.FindProperty("m_PerPlatformDefaultQuality");
        for (int i = 0; i < defaults.arraySize; i++)
        {
            var e = defaults.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("first").stringValue == "Android") androidLevel = e.FindPropertyRelative("second").intValue;
        }

        var urp = QualitySettings.GetRenderPipelineAssetAt(androidLevel) as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (urp != null)
        {
            var so = new SerializedObject(urp);
            void Set(string prop, System.Action<SerializedProperty> a) { var p = so.FindProperty(prop); if (p != null) a(p); else log.AppendLine("  (missing " + prop + ")"); }
            Set("m_RenderScale", p => p.floatValue = 1f);                 // sharp image; foveation saves the GPU instead
            Set("m_MSAA", p => p.intValue = 4);                           // 4x MSAA is cheap on Quest's tiled GPU
            Set("m_SupportsHDR", p => p.boolValue = false);
            Set("m_RequireDepthTexture", p => p.boolValue = false);
            Set("m_RequireOpaqueTexture", p => p.boolValue = false);
            Set("m_ShadowDistance", p => p.floatValue = 25f);
            Set("m_ShadowCascadeCount", p => p.intValue = 1);
            Set("m_MainLightShadowmapResolution", p => p.intValue = 1024);
            Set("m_SoftShadowsSupported", p => p.boolValue = false);
            Set("m_AdditionalLightsRenderingMode", p => p.intValue = 2);   // per vertex
            Set("m_AdditionalLightShadowsSupported", p => p.boolValue = false);
            Set("m_UseSRPBatcher", p => p.boolValue = true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            log.AppendLine(urp.name + ": render scale 1, MSAA 4x, no HDR, no depth/opaque textures, 25 m hard shadows, vertex extra lights");
        }

        // Quality level knobs for the Android level.
        int current = QualitySettings.GetQualityLevel();
        QualitySettings.SetQualityLevel(androidLevel, false);
        QualitySettings.vSyncCount = 0;          // XR runtime controls the frame rate
        QualitySettings.pixelLightCount = 1;
        QualitySettings.lodBias = 0.7f;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.SetQualityLevel(current, false);

        PlayerSettings.gpuSkinning = true;
        AssetDatabase.SaveAssets();
        Debug.Log("[Quest] Performance settings applied (quality level " + QualitySettings.names[androidLevel] + "):\n" + log);
    }

    [MenuItem("Tools/VR/Report Meta Project Setup Issues")]
    public static void Report()
    {
        var sb = new StringBuilder("[Quest] Outstanding Meta setup tasks (Android):\n");
        var getTasks = typeof(OVRProjectSetup).GetMethod("GetTasks",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
            null, new[] { typeof(BuildTargetGroup) }, null);
        if (getTasks == null) { Debug.LogWarning("[Quest] OVRProjectSetup.GetTasks not found"); return; }
        int open = 0;
        const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (object t in (System.Collections.IEnumerable)getTasks.Invoke(null, new object[] { Android }))
        {
            var type = t.GetType();
            var isDone = (System.Delegate)type.GetProperty("IsDone", F).GetValue(t);
            bool done = (bool)isDone.DynamicInvoke(Android);
            bool ignored = (bool)type.GetMethod("IsIgnored", F).Invoke(t, new object[] { Android });
            if (done || ignored) continue;
            open++;
            object level = Lambda(type.GetProperty("Level", F).GetValue(t));
            object msg = Lambda(type.GetProperty("Message", F).GetValue(t));
            sb.AppendLine("- [" + level + "] " + msg);
        }
        sb.Insert(0, open + " open. ");
        Debug.Log(sb.ToString());
    }

    /// <summary>Evaluates Meta's OptionalLambdaType&lt;BuildTargetGroup, T&gt; for Android.</summary>
    static object Lambda(object optional)
    {
        if (optional == null) return "";
        var m = optional.GetType().GetMethod("GetValue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        return m != null ? m.Invoke(optional, new object[] { Android }) : optional;
    }

    // ───────────────────────── XR management ─────────────────────────

    static void ConfigureXRManagement(StringBuilder log)
    {
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perBT) || perBT == null)
        {
            perBT = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            System.IO.Directory.CreateDirectory("Assets/XR");
            AssetDatabase.CreateAsset(perBT, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBT, true);
            log.AppendLine("Created XR general settings");
        }

        foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
        {
            if (!perBT.HasSettingsForBuildTarget(group)) perBT.CreateDefaultSettingsForBuildTarget(group);
            if (!perBT.HasManagerSettingsForBuildTarget(group)) perBT.CreateDefaultManagerSettingsForBuildTarget(group);
            var settings = perBT.SettingsForBuildTarget(group);
            settings.InitManagerOnStart = true;
            // Meta's recommended runtime is OpenXR with the Meta XR feature set; drop the legacy Oculus loader.
            XRPackageMetadataStore.RemoveLoader(settings.Manager, "Unity.XR.Oculus.OculusLoader", group);
            XRPackageMetadataStore.AssignLoader(settings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(settings.Manager);
            log.AppendLine(group + " loaders: " + string.Join(", ", settings.Manager.activeLoaders.Select(l => l.GetType().Name)));
        }
        EditorUtility.SetDirty(perBT);
    }

    static void ConfigureOpenXRFeatures(StringBuilder log)
    {
        // Meta XR feature set (enables the features Meta's SDK needs).
        var metaSet = OpenXRFeatureSetManager.GetFeatureSetWithId(Android, "com.meta.openxr.featureset.metaxr");
        if (metaSet != null)
        {
            metaSet.isEnabled = true;
            OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(Android);
            log.AppendLine("Meta XR feature set enabled");
        }
        else log.AppendLine("Meta XR feature set not found (is com.meta.xr.sdk.core installed?)");

        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(Android);
        if (settings == null) { log.AppendLine("No OpenXR settings for Android"); return; }
        string[] wanted =
        {
            "MetaQuestFeature",                       // Meta Quest support
            "OculusTouchControllerProfile",           // Quest 2 / Pro controllers
            "MetaQuestTouchPlusControllerProfile",    // Quest 3 controllers
            "MetaQuestTouchProControllerProfile",
            "MetaXRFeature",                          // Meta XR core feature
        };
        foreach (var f in settings.GetFeatures())
        {
            string n = f.GetType().Name;
            if (wanted.Contains(n) && !f.enabled) { f.enabled = true; log.AppendLine("Enabled OpenXR feature " + n); }
        }
        // Multiview for Quest.
        settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
        EditorUtility.SetDirty(settings);
    }

    // ───────────────────────── player settings ─────────────────────────

    static void ConfigurePlayer(StringBuilder log)
    {
        var android = UnityEditor.Build.NamedBuildTarget.Android;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        if ((int)PlayerSettings.Android.minSdkVersion < 32) PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.stereoRenderingPath = StereoRenderingPath.Instancing;
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        PlayerSettings.SetMobileMTRendering(android, true);
        log.AppendLine("Player: Vulkan, IL2CPP ARM64, min API 32, Linear, Multiview, ASTC");
    }

    static async void RunMetaFixAll()
    {
        try
        {
            await OVRProjectSetup.FixAllAsync(Android);
            Debug.Log("[Quest] Meta Project Setup Tool: Fix All done.");
            Report();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Quest] Meta Fix All failed: " + e.Message);
        }
    }
}
