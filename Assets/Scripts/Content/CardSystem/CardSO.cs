using System.Collections.Generic;
using UnityEngine;

namespace RogueBlockBlast.Content
{
    [CreateAssetMenu(menuName = "RogueBlockBlast/Card", fileName = "Card_")]
    public sealed class CardSO : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string CardName;

        [Header("Description")]
        [TextArea(2, 3)]
        public string Description;

        [Header("Visual")]
        public Sprite Icon;
        public CardRarity Rarity = CardRarity.Common;

        [Header("Effects")]
        [Tooltip("Bu kartın uyguladığı efektler. Birden fazla olabilir.")]
        public List<CardEffect> Effects = new List<CardEffect>();

        [Header("Spawn Weight")]
        [Min(0)]
        public int BaseWeight = 100;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;
        }
    }
}