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
        [Tooltip("True ise başlangıçta kilitli — coin ile unlock edilebilir.")]
        public bool LockedByDefault = false;

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