using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using RogueBlockBlast.Core.Settings;
using RogueBlockBlast.Core.Localization;
using Assets.SimpleLocalization.Scripts;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// Editör kısayolları — ayar / kayıt / lokalizasyon işleri için.
    /// Menü: Tools ▸ RogueBlockBlast
    /// </summary>
    public static class SettingsMenuItems
    {
        [MenuItem("Tools/RogueBlockBlast/Save Data/Reset Progress (keep settings)", priority = 0)]
        private static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog(
                    "İlerlemeyi sıfırla",
                    "Blockcoin, upgrade ve unlock verisi silinecek. Ayarlar ve dil korunur.\nBu geri alınamaz.",
                    "Sıfırla", "Vazgeç"))
                return;

            SaveDataService.ResetProgress();
        }

        [MenuItem("Tools/RogueBlockBlast/Save Data/Reset Settings Only", priority = 1)]
        private static void ResetSettings()
        {
            GameSettings.ResetToDefaults();
            Debug.Log("[Settings] Varsayılanlara döndürüldü.");
        }

        [MenuItem("Tools/RogueBlockBlast/Save Data/Delete EVERYTHING (PlayerPrefs)", priority = 2)]
        private static void DeleteAll()
        {
            if (!EditorUtility.DisplayDialog(
                    "Tüm PlayerPrefs silinsin mi?",
                    "İlerleme VE ayarlar dahil her şey silinir. Bu geri alınamaz.",
                    "Hepsini sil", "Vazgeç"))
                return;

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[SaveData] Tüm PlayerPrefs silindi.");
        }

        [MenuItem("Tools/RogueBlockBlast/Localization/Reload Table", priority = 20)]
        private static void ReloadLocalization()
        {
            Loc.Reload();
            Debug.Log($"[Localization] Tablo yeniden yüklendi. Diller: {string.Join(", ", Loc.AvailableLanguages)}");
        }

        [MenuItem("Tools/RogueBlockBlast/Localization/Validate Keys In Project", priority = 21)]
        private static void ValidateKeys()
        {
            Loc.Reload();

            var missing = new List<string>();
            int checkedCount = 0;

            foreach (var (key, path, objName) in CollectKeys())
            {
                checkedCount++;
                if (!Loc.HasKey(key))
                    missing.Add($"{key}   ←   {path} / {objName}");
            }

            if (missing.Count == 0)
                Debug.Log($"[Localization] {checkedCount} anahtar tarandı, hepsi tabloda mevcut.");
            else
                Debug.LogWarning($"[Localization] {missing.Count} eksik anahtar:\n" + string.Join("\n", missing));
        }

        [MenuItem("Tools/RogueBlockBlast/Localization/Log Unused Keys", priority = 22)]
        private static void LogUnusedKeys()
        {
            Loc.Reload();

            var used = new HashSet<string>();
            foreach (var (key, _, _) in CollectKeys()) used.Add(key);

            Debug.Log($"[Localization] Prefab/sahnelerde {used.Count} anahtar kullanılıyor:\n" +
                      string.Join("\n", used.OrderBy(k => k)) +
                      "\nNot: koddan Loc.Get(...) ile çağrılanlar bu listede görünmez.");
        }

        /// <summary>
        /// Projedeki tüm prefab ve sahnelerde kullanılan lokalizasyon anahtarlarını toplar.
        /// Hem LocalizationKeyHolder (TextMeshProGUILocalized'ın kullandığı) hem de
        /// eski LocalizedText bileşenlerini tarar.
        /// </summary>
        private static IEnumerable<(string key, string path, string objName)> CollectKeys()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab")
                .Concat(AssetDatabase.FindAssets("t:Scene"))
                .Distinct();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is not GameObject go) continue;

                    foreach (var h in go.GetComponentsInChildren<LocalizationKeyHolder>(true))
                        if (!string.IsNullOrEmpty(h.LocalizationKey))
                            yield return (h.LocalizationKey, path, h.name);

                    foreach (var lt in go.GetComponentsInChildren<LocalizedText>(true))
                        if (!string.IsNullOrEmpty(lt.LocalizationKey))
                            yield return (lt.LocalizationKey, path, lt.name);
                }
            }
        }
    }
}
