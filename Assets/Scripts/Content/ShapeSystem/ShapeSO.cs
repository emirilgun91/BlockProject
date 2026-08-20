using System.Collections.Generic;
using RogueBlockBlast.Core;
using UnityEngine;

namespace RogueBlockBlast.Content
{
    [CreateAssetMenu(menuName = "RogueBlockBlast/Shape", fileName = "Shape_")]
    public sealed class ShapeSO : ScriptableObject
    {
        [Header("Identity")]
        public string Id;

        [Header("Cells (Local, R0)")]
        public List<Vector2Int> Cells = new();

        [Header("Spawn Weight")]
        public ShapeRarity Rarity = ShapeRarity.Common;
        [Min(0)]
        public int BaseWeight = 100;

        [Header("Visual")]
        public BlockColorPreset ColorPreset = BlockColorPreset.Teal;
        public Color BlockColor => BlockColorPalette.GetColor(ColorPreset);

        [Header("Score")]
        [Min(1)]
        public int BaseTileValue = 10;

        [Header("Unlock")]
        [Tooltip("True ise başlangıçta kilitli.")]
        public bool LockedByDefault = false;
        [Tooltip("Unlock maliyeti (coin).")]
        public int UnlockCost = 300;

        [Header("Score Upgrade Curves")]
        [Tooltip("Her seviyede tile değerine eklenen puan. Örn: 3,5,7,9\n" +
                 "Boş bırakılırsa registry'deki sabit ValuePerUpgradeLevel kullanılır.")]
        public UpgradeCurve ScoreGain;

        [Tooltip("Her seviyenin coin maliyeti. Örn: 100,200,400,600,800\n" +
                 "Boş bırakılırsa aşağıdaki eski BaseCost + Level×PerLevel formülü kullanılır.")]
        public UpgradeCurve ScoreCost;

        [Header("Upgrade Costs (eski — curve boşsa kullanılır)")]
        [Tooltip("Her score upgrade seviyesinin baz maliyeti.")]
        public int ScoreUpgradeBaseCost = 10;
        [Tooltip("Her seviyede maliyet ne kadar artar.")]
        public int ScoreUpgradeCostPerLevel = 5;
        [Tooltip("Weight artırma maliyeti.")]
        public int WeightIncreaseCost = 60;
        [Tooltip("Weight azaltma maliyeti.")]
        public int WeightDecreaseCost = 40;

        /// <summary>Runtime'da bu shape unlock edilmiş mi?</summary>
        public bool IsUnlocked =>
            !LockedByDefault ||
            (UnlockRegistry.Instance != null && UnlockRegistry.Instance.IsShapeUnlocked(Id));

        /// <summary>Runtime tile değeri — upgrade dahil.</summary>
        public float GetCurrentTileValue() =>
            ShapeUpgradeRegistry.Instance.GetTileValue(Id, BaseTileValue);

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;
        }
    }
}