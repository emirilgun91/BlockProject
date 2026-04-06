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
        public int BaseWeight     = 100;

        [Header("Visual")]
        [Tooltip("Palette rengi.")]
        public BlockColorPreset ColorPreset = BlockColorPreset.Teal;
        public Color BlockColor => BlockColorPalette.GetColor(ColorPreset);

        [Header("Score")]
        [Tooltip("Her tile'ın base puan değeri. Upgrade ile artar.")]
        [Min(1)]
        public int BaseTileValue = 10;

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