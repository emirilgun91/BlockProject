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
        
        public ShapeRarity Rarity = ShapeRarity.Common;
        [Min(0)]
        public int BaseWeight = 100;
        [Header("Visual")]
        public BlockColorPreset ColorPreset = BlockColorPreset.Teal;
        public Color BlockColor => BlockColorPalette.GetColor(ColorPreset);  // computed, not stored
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;
        
            // Basit temizlik: duplicate cell varsa kaldır.
            // (Serialize listesinde otomatik normalize etmiyoruz; editörde kontrol etmen yeterli.)
        }
    }
}