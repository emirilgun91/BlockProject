using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// Tek tıkla Steam'e yüklenecek player build'leri: <c>Builds/macOS/Blockflow.app</c>
    /// (Intel + Apple Silicon) ve <c>Builds/Windows/Blockflow.exe</c>. SteamPipe betikleri
    /// (<c>Steam/SteamPipe/</c>) bu iki klasörü okur.
    ///
    /// macOS build'i Windows'tan Mono ile alınabiliyor (IL2CPP Mac + Xcode ister). Mimari
    /// ayarı Mac modülünün <c>UnityEditor.OSXStandalone.UserBuildSettings</c>'inde; modül
    /// kurulu değilken bu dosya derlensin diye ona yansıma ile ulaşılıyor.
    /// Menü: Tools ▸ RogueBlockBlast ▸ Build
    /// </summary>
    public static class PlatformBuildMenu
    {
        // Mac'te .app'in CFBundleIdentifier'ı. Şablondan kalan "com.DefaultCompany.2D-URP" yerine.
        public const string BundleId = "com.designerdadgames.blockflow";

        private const string ExeName = "Blockflow";

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string MacDir      => Path.Combine(ProjectRoot, "Builds", "macOS");
        private static string WinDir      => Path.Combine(ProjectRoot, "Builds", "Windows");

        [MenuItem("Tools/RogueBlockBlast/Build/macOS (Intel + Apple Silicon)", priority = 300)]
        private static void BuildMac()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
            {
                EditorUtility.DisplayDialog("macOS build",
                    "Mac Build Support (Mono) modülü bu editörde yok. Unity Hub ▸ Installs ▸ " +
                    Application.unityVersion + " ▸ Add modules ile kur ve Unity'yi yeniden başlat.", "Tamam");
                return;
            }
            if (!SetMacUniversal()) return;

            Build(BuildTarget.StandaloneOSX, Path.Combine(MacDir, ExeName + ".app"), MacDir);
        }

        [MenuItem("Tools/RogueBlockBlast/Build/Windows (x64)", priority = 301)]
        private static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, Path.Combine(WinDir, ExeName + ".exe"), WinDir);
        }

        [MenuItem("Tools/RogueBlockBlast/Build/Open Builds Folder", priority = 320)]
        private static void OpenBuildsFolder()
        {
            string dir = Path.Combine(ProjectRoot, "Builds");
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        private static void Build(BuildTarget target, string locationPath, string outDir)
        {
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone) != BundleId)
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Build] Bundle ID → {BundleId}");
            }

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] Build Settings'te açık sahne yok.");
                return;
            }

            // Eski build'in kalıntısı (silinmiş bir dosya) depot'a girmesin diye klasör boşaltılır.
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            // BuildPlayer aktif hedefi değiştirir; Windows'ta çalışmaya devam edilsin diye geri alınır.
            var prevTarget = EditorUserBuildSettings.activeBuildTarget;
            var prevGroup  = EditorUserBuildSettings.selectedBuildTargetGroup;

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes           = scenes,
                    locationPathName = locationPath,
                    target           = target,
                    targetGroup      = BuildTargetGroup.Standalone,
                    options          = BuildOptions.None,
                });
            }
            finally
            {
                if (EditorUserBuildSettings.activeBuildTarget != prevTarget)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(prevGroup, prevTarget);
            }

            var sum = report.summary;
            if (sum.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Build] {target} başarısız: {sum.result}, {sum.totalErrors} hata.");
                return;
            }

            string problems = target == BuildTarget.StandaloneOSX ? CheckMacBundle(locationPath) : CheckWindows(outDir);
            Debug.Log($"[Build] {target} hazır: {locationPath} ({sum.totalSize / (1024f * 1024f):0.0} MB, " +
                      $"{sum.totalTime.TotalSeconds:0} s)" + (problems.Length > 0 ? "\n" + problems : ""));
            if (problems.Length > 0) Debug.LogWarning("[Build] " + problems);
            EditorUtility.RevealInFinder(locationPath);
        }

        // UserBuildSettings.architecture = OSArchitecture.x64ARM64 (Mac modülü kuruluyken var).
        private static bool SetMacUniversal()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.OSXStandalone.UserBuildSettings", false))
                .FirstOrDefault(t => t != null);
            var prop = type?.GetProperty("architecture");
            if (prop == null || !prop.PropertyType.IsEnum)
            {
                EditorUtility.DisplayDialog("macOS build",
                    "Mac modülünün mimari ayarı bulunamadı. Modül yeni kurulduysa Unity'yi yeniden başlat.", "Tamam");
                return false;
            }
            prop.SetValue(null, Enum.Parse(prop.PropertyType, "x64ARM64"));
            return true;
        }

        // steam_api.bundle .app'in içine girmediyse Mac'te Steam (ve achievement'lar) çalışmaz;
        // steam_appid.txt girdiyse RestartAppIfNecessary devre dışı kalır, oyun Steam'siz açılır.
        private static string CheckMacBundle(string app)
        {
            string problems = "";
            if (!File.Exists(Path.Combine(app, "Contents", "MacOS", ExeName)))
                problems += $"Contents/MacOS/{ExeName} yok. ";
            // macOS'un yazımı "PlugIns"; Windows'ta büyük/küçük harf fark etmez ama Mac'te eder.
            if (!Directory.Exists(Path.Combine(app, "Contents", "PlugIns", "steam_api.bundle")))
                problems += "Contents/PlugIns/steam_api.bundle yok (Steam çalışmaz). ";
            if (Directory.GetFiles(app, "steam_appid.txt", SearchOption.AllDirectories).Length > 0)
                problems += "steam_appid.txt .app'in içinde, gönderilmemeli. ";
            return problems;
        }

        private static string CheckWindows(string dir)
        {
            string problems = "";
            if (!File.Exists(Path.Combine(dir, ExeName + "_Data", "Plugins", "x86_64", "steam_api64.dll")))
                problems += "steam_api64.dll yok (Steam çalışmaz). ";
            if (File.Exists(Path.Combine(dir, "steam_appid.txt")))
                problems += "steam_appid.txt build klasöründe, gönderilmemeli. ";
            return problems;
        }
    }
}
