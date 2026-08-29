using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// Oyunun tüm yazı fontlarını Exo 2'ye taşır.
    ///
    /// <b>Neden gerekliydi:</b> Eczar font assetleri <c>Static</c> atlas ile ve
    /// yalnızca 97 karakterle üretilmişti — Türkçe <c>ç ğ ı İ ö ş ü</c> bile
    /// yoktu. Üstelik hem <c>TMP Settings</c> hem de tek tek font assetlerinin
    /// fallback tabloları boştu, yani eksik karakterin düşeceği bir yer de yoktu.
    /// Sonuç: Eczar kullanan her yerde (≈510 referans) Türkçe metin kırıktı,
    /// Rusça/Japonca/Korece/Çince ise hiçbir fontta yoktu.
    ///
    /// <b>Çözüm üç parçalı:</b>
    ///   1. Exo 2'den <c>Dynamic</c> atlaslı font assetleri üret. Dynamic olması
    ///      şart — statik atlas aynı tuzağı tekrar kurar.
    ///   2. Fallback zinciri: Exo 2 → Inter. Inter zaten projede ve 2926 glyph
    ///      taşıyor, yani Exo 2'nin kaçırdığını yakalıyor. CJK için zincire
    ///      Noto Sans CJK eklenecek (bkz. menüdeki ayrı komut).
    ///   3. Sahne ve prefablardaki tüm TMP_Text'leri ağırlığına göre eşle.
    ///
    /// Metinlerin hiçbiri özel materyal preset'i (outline/glow) kullanmıyor —
    /// hepsi font assetinin kendi materyalinde. Bu yüzden font ile materyal
    /// birlikte taşınabiliyor, kaybolan efekt olmuyor.
    ///
    /// <b>Çalıştırmadan önce commit edin.</b> Araç bütün sahneleri ve prefabları
    /// açıp kaydeder; geri alma yolu git'tir.
    /// </summary>
    public static class FontMigrationTool
    {
        private const string Exo2SourceDir = "Assets/Resources/Fonts/Exo_2/static";
        private const string InterSourceDir = "Assets/Resources/Fonts/Inter/static";
        private const string OutputDir     = "Assets/TextMesh Pro/Fonts/Exo2";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        /// <summary>
        /// CJK fallback'leri: kaynak TTF → üretilecek asset adı.
        ///
        /// Tek ağırlık (Regular) bilinçli: her CJK fontu 5–10 MB ve üçü birden
        /// build'e ~21 MB ekliyor. TMP kalını sentetik üretebiliyor; demo için
        /// bu takas doğru. Tam sürümde Bold eklenir.
        /// </summary>
        private static readonly (string dir, string ttf, string asset)[] CjkFonts =
        {
            ("Assets/TextMesh Pro/Fonts/Noto_Sans_JP/static", "NotoSansJP-Regular.ttf", "NotoSansJP-Regular SDF"),
            ("Assets/TextMesh Pro/Fonts/Noto_Sans_KR/static", "NotoSansKR-Regular.ttf", "NotoSansKR-Regular SDF"),
            ("Assets/TextMesh Pro/Fonts/Noto_Sans_SC/static", "NotoSansSC-Regular.ttf", "NotoSansSC-Regular SDF"),
        };

        // Üretilecek ağırlıklar. Projede kullanılan eski ağırlıkların karşılığı.
        private static readonly string[] Weights =
        {
            "Black", "ExtraBold", "Bold", "SemiBold", "Medium", "Regular"
        };

        /// <summary>Eski font adı → yeni Exo 2 ağırlığı.</summary>
        private static readonly Dictionary<string, string> WeightMap = new()
        {
            { "Eczar-Bold SDF",                "Bold"      },
            { "Eczar-ExtraBold SDF",           "ExtraBold" },
            { "Eczar-SemiBold SDF",            "SemiBold"  },
            { "Eczar-Medium SDF",              "Medium"    },
            { "Eczar-Regular SDF",             "Regular"   },
            { "Eczar-VariableFont_wght SDF",   "Regular"   },
            { "Barlow-Black SDF",              "Black"     },
            { "Barlow-Bold SDF",               "Bold"      },
            { "Barlow-Medium SDF",             "Medium"    },
            { "LiberationSans SDF",            "Regular"   },
        };

        // ── Menü ─────────────────────────────────────────────────────────────

        [MenuItem("Tools/RogueBlockBlast/Fonts/1 — Create Exo 2 Font Assets", priority = 200)]
        public static void CreateFontAssets()
        {
            if (!EnsureOutputDir()) return;

            int made = 0;
            foreach (var w in Weights)
                if (CreateOne($"Exo2-{w}.ttf", $"Exo2-{w} SDF")) made++;

            // Fallback zincirinin halkaları.
            CreateOne("Inter_18pt-Regular.ttf", "Inter-Regular SDF", InterSourceDir);
            foreach (var (dir, ttf, asset) in CjkFonts) CreateOne(ttf, asset, dir);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[Fonts] {made}/{Weights.Length} Exo 2 asseti + Inter + 3 CJK fallback üretildi → {OutputDir}");
        }

        [MenuItem("Tools/RogueBlockBlast/Fonts/2 — Set Fallback Chain", priority = 201)]
        public static void SetFallbackChain()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogError($"[Fonts] TMP Settings bulunamadı: {TmpSettingsPath}");
                return;
            }

            // Zincir sırası: Inter (Latin/Kiril boşlukları) → CJK fontları.
            // CJK'nın kendi arasındaki sırası runtime'da seçilen dile göre
            // yeniden diziliyor; bkz. CjkFontFallback.
            var chain = new List<TMP_FontAsset>();

            var inter = Load("Inter-Regular SDF");
            if (inter == null)
            {
                Debug.LogError("[Fonts] Inter fallback asseti yok — önce adım 1'i çalıştırın.");
                return;
            }
            chain.Add(inter);

            foreach (var (_, _, assetName) in CjkFonts)
            {
                var cjk = Load(assetName);
                if (cjk != null) chain.Add(cjk);
                else Debug.LogWarning($"[Fonts] {assetName} yok — CJK fallback eksik kalacak.");
            }

            var so   = new SerializedObject(settings);
            var list = so.FindProperty("m_fallbackFontAssets");
            if (list == null)
            {
                Debug.LogError("[Fonts] TMP Settings'te m_fallbackFontAssets alanı yok.");
                return;
            }

            list.ClearArray();
            for (int i = 0; i < chain.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = chain[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[Fonts] Fallback zinciri kuruldu: Exo 2 → " +
                string.Join(" → ", chain.Select(c => c.name)));
        }

        [MenuItem("Tools/RogueBlockBlast/Fonts/3 — Migrate All Text To Exo 2", priority = 202)]
        public static void MigrateAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Fonts] Play modundayken çalıştırılamaz.");
                return;
            }

            var map = BuildRuntimeMap();
            if (map == null) return;

            bool ok = EditorUtility.DisplayDialog(
                "Font Migration",
                "Tüm sahneler ve prefablar açılıp Exo 2'ye taşınacak ve KAYDEDİLECEK.\n\n" +
                "Geri alma yolu git'tir — devam etmeden önce commit ettiğinizden emin olun.",
                "Devam et", "Vazgeç");
            if (!ok) return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int prefabs = MigratePrefabs(map);
            int scenes  = MigrateScenes(map);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Fonts] Taşıma bitti — {prefabs} prefab, {scenes} sahne güncellendi.");
        }

        // ── Font asseti üretimi ──────────────────────────────────────────────

        /// <summary>
        /// TTF'ten Dynamic atlaslı bir TMP font asseti üretir.
        ///
        /// Atlas ve materyal alt-asset olarak eklenmeli; eklenmezse font asseti
        /// kaydedilir ama dokusu kaybolur ve yazılar boş çıkar.
        /// </summary>
        private static bool CreateOne(string ttfName, string assetName, string sourceDir = null)
        {
            sourceDir ??= Exo2SourceDir;

            string ttfPath = Path.Combine(sourceDir, ttfName).Replace('\\', '/');
            var    font    = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);

            if (font == null)
            {
                Debug.LogWarning($"[Fonts] Kaynak font bulunamadı: {ttfPath}");
                return false;
            }

            string outPath = $"{OutputDir}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath) != null)
            {
                Debug.Log($"[Fonts] Zaten var, atlandı: {assetName}");
                return true;
            }

            var asset = TMP_FontAsset.CreateFontAsset(
                font,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (asset == null)
            {
                Debug.LogError($"[Fonts] Font asseti üretilemedi: {ttfName}");
                return false;
            }

            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, outPath);

            if (asset.atlasTextures is { Length: > 0 } && asset.atlasTextures[0] != null)
            {
                asset.atlasTextures[0].name = assetName + " Atlas";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            }

            if (asset.material != null)
            {
                asset.material.name = assetName + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            EditorUtility.SetDirty(asset);
            Debug.Log($"[Fonts] Üretildi: {assetName} (Dynamic)");
            return true;
        }

        // ── Taşıma ───────────────────────────────────────────────────────────

        /// <summary>Eski font asseti → yeni Exo 2 asseti eşlemesi.</summary>
        private static Dictionary<TMP_FontAsset, TMP_FontAsset> BuildRuntimeMap()
        {
            var map = new Dictionary<TMP_FontAsset, TMP_FontAsset>();

            foreach (var pair in WeightMap)
            {
                var oldAsset = FindByName(pair.Key);
                var newAsset = Load($"Exo2-{pair.Value} SDF");

                if (newAsset == null)
                {
                    Debug.LogError($"[Fonts] Exo2-{pair.Value} SDF yok — önce adım 1'i çalıştırın.");
                    return null;
                }

                // Eski asset projede yoksa sorun değil, sadece o eşleme atlanır.
                if (oldAsset != null) map[oldAsset] = newAsset;
            }

            return map;
        }

        private static int MigratePrefabs(Dictionary<TMP_FontAsset, TMP_FontAsset> map)
        {
            var guids   = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            int changed = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("Font Migration", $"Prefab: {path}",
                                                 i / (float)guids.Length);

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (Remap(root.GetComponentsInChildren<TMP_Text>(true), map) > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changed++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            EditorUtility.ClearProgressBar();
            return changed;
        }

        private static int MigrateScenes(Dictionary<TMP_FontAsset, TMP_FontAsset> map)
        {
            // Yalnızca gerçek oyun sahneleri. "Assets" altını taramak
            // _Recovery klasöründeki onlarca çökme yedeğini de açıp kaydederdi —
            // hem gereksiz hem de git'te kafa karıştırıcı bir fark üretirdi.
            var guids   = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
            int changed = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("Font Migration", $"Sahne: {path}",
                                                 i / (float)guids.Length);

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                // Kapalı nesnelerdeki yazılar da taşınmalı — pause menüsü,
                // game over paneli gibi ekranlar sahnede kapalı duruyor.
                var texts = Object.FindObjectsByType<TMP_Text>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (Remap(texts, map) > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    changed++;
                }
            }

            EditorUtility.ClearProgressBar();
            return changed;
        }

        /// <summary>
        /// Yazıların fontunu eşlemeye göre değiştirir ve materyali de yeni
        /// fontunkine çeker. Materyali ayrıca yazmak şart: font değişip materyal
        /// eskide kalırsa yazı yanlış atlasa bakar ve boş/bozuk çıkar.
        /// </summary>
        private static int Remap(IEnumerable<TMP_Text> texts,
                                 Dictionary<TMP_FontAsset, TMP_FontAsset> map)
        {
            int n = 0;

            foreach (var t in texts)
            {
                if (t == null || t.font == null) continue;
                if (!map.TryGetValue(t.font, out var newFont)) continue;

                t.font = newFont;
                t.fontSharedMaterial = newFont.material;

                EditorUtility.SetDirty(t);
                n++;
            }

            return n;
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private static TMP_FontAsset Load(string assetName) =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{OutputDir}/{assetName}.asset");

        private static TMP_FontAsset FindByName(string assetName) =>
            AssetDatabase.FindAssets($"t:TMP_FontAsset {assetName}")
                .Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(a => a != null && a.name == assetName);

        private static bool EnsureOutputDir()
        {
            if (AssetDatabase.IsValidFolder(OutputDir)) return true;

            string parent = "Assets/TextMesh Pro/Fonts";
            if (!AssetDatabase.IsValidFolder(parent))
            {
                Debug.LogError($"[Fonts] Klasör yok: {parent}");
                return false;
            }

            AssetDatabase.CreateFolder(parent, "Exo2");
            return true;
        }
    }
}
