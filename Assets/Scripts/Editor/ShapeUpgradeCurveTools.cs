using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// Shape upgrade eğrilerini toplu ayarlamak ve önizlemek için editör araçları.
    /// Menü: Tools ▸ RogueBlockBlast ▸ Shape Upgrades
    ///
    /// Tek tek her ShapeSO'nun Inspector'ından da düzenleyebilirsin — bu araçlar
    /// 14 şeklin hepsine aynı eğriyi basmak istediğinde zaman kazandırır.
    /// </summary>
    public static class ShapeUpgradeCurveTools
    {
        // Varsayılan şablon — istediğin gibi değiştir.
        // İki listenin uzunluğu EŞİT olmalı: kaç seviye varsa o kadar maliyet gerekir.
        static readonly int[] DefaultGain = { 3, 5, 7, 9, 11 };
        static readonly int[] DefaultCost = { 100, 200, 400, 600, 800 };
        const int DefaultGainMax = 11;     // kazanç tavanı (0 = sınırsız)
        const int DefaultCostMax = 800;    // maliyet tavanı (0 = sınırsız)

        [MenuItem("Tools/RogueBlockBlast/Shape Upgrades/Apply Default Curves To All Shapes", priority = 40)]
        private static void ApplyDefaults()
        {
            var shapes = LoadShapes();
            if (shapes.Length == 0) { Debug.LogWarning("[ShapeCurves] Hiç ShapeSO bulunamadı."); return; }

            if (!EditorUtility.DisplayDialog(
                    "Eğrileri uygula",
                    $"{shapes.Length} şeklin ScoreGain ve ScoreCost eğrileri şu değerlerle EZİLECEK:\n\n" +
                    $"Kazanç : {string.Join(", ", DefaultGain)}   (tavan {DefaultGainMax})\n" +
                    $"Maliyet: {string.Join(", ", DefaultCost)}   (tavan {DefaultCostMax})\n\n" +
                    "Devam edilsin mi?",
                    "Uygula", "Vazgeç"))
                return;

            foreach (var s in shapes)
            {
                Undo.RecordObject(s, "Apply Upgrade Curves");
                s.ScoreGain = new UpgradeCurve
                {
                    Steps = (int[])DefaultGain.Clone(),
                    Multiplier = 1f,
                    Increment = 2,                 // liste bitince +2 devam eder
                    MaxValue = DefaultGainMax,
                    MaxLevel = DefaultGain.Length,
                };
                s.ScoreCost = new UpgradeCurve
                {
                    Steps = (int[])DefaultCost.Clone(),
                    Multiplier = 1f,
                    Increment = 200,
                    MaxValue = DefaultCostMax,
                    MaxLevel = DefaultCost.Length,
                };
                EditorUtility.SetDirty(s);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShapeCurves] {shapes.Length} şekle eğri uygulandı.");
        }

        [MenuItem("Tools/RogueBlockBlast/Shape Upgrades/Preview Curves", priority = 41)]
        private static void Preview()
        {
            var shapes = LoadShapes();
            var reg = ShapeUpgradeRegistry.Instance;
            reg.Load(shapes);

            var sb = new StringBuilder("[ShapeCurves] Yükseltme tablosu (seviye: tile değeri / maliyet)\n");
            foreach (var s in shapes)
            {
                int max = s.ScoreGain.IsEmpty ? reg.MaxScoreUpgradeLevel : s.ScoreGain.EffectiveMaxLevel;
                if (max <= 0) max = 5;

                sb.Append($"\n{s.Id,-14} baz {s.BaseTileValue,3}");
                float running = s.BaseTileValue;
                int totalCost = 0;
                for (int lvl = 1; lvl <= max; lvl++)
                {
                    int gain = s.ScoreGain.IsEmpty ? Mathf.RoundToInt(reg.ValuePerUpgradeLevel) : s.ScoreGain.GetValue(lvl);
                    int cost = s.ScoreCost.IsEmpty
                        ? s.ScoreUpgradeBaseCost + (lvl - 1) * s.ScoreUpgradeCostPerLevel
                        : s.ScoreCost.GetValue(lvl);
                    running += gain;
                    totalCost += cost;
                    sb.Append($"  |  L{lvl}: {running:0} (+{gain}) / {cost}c");
                }
                sb.Append($"   →  toplam {totalCost}c");
            }
            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/RogueBlockBlast/Shape Upgrades/Clear Curves (revert to old formula)", priority = 42)]
        private static void ClearCurves()
        {
            if (!EditorUtility.DisplayDialog("Eğrileri temizle",
                    "Tüm şekillerin ScoreGain/ScoreCost eğrileri boşaltılacak; eski sabit artış " +
                    "ve BaseCost + Level×PerLevel formülüne dönülür.", "Temizle", "Vazgeç"))
                return;

            var shapes = LoadShapes();
            foreach (var s in shapes)
            {
                Undo.RecordObject(s, "Clear Upgrade Curves");
                s.ScoreGain = default;
                s.ScoreCost = default;
                EditorUtility.SetDirty(s);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShapeCurves] {shapes.Length} şeklin eğrileri temizlendi.");
        }

        private static ShapeSO[] LoadShapes() =>
            AssetDatabase.FindAssets("t:ShapeSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<ShapeSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                .OrderBy(s => s.Id)
                .ToArray();
    }
}
