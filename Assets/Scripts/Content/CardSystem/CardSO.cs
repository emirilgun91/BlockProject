using System.Collections.Generic;
using RogueBlockBlast.Core;
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
        public List<CardEffect> Effects = new List<CardEffect>();

        [Header("Spawn Weight")]
        [Min(0)]
        public int BaseWeight = 100;

        [Header("Unique")]
        [Tooltip("True ise bir kez seçildikten sonra kart havuzuna bir daha girmez.")]
        public bool IsUnique = false;

        [Header("Unlock")]
        [Tooltip("True ise başlangıçta kilitli — milestone'da unlock edilir.")]
        public bool LockedByDefault = false;

        /// <summary>Runtime'da bu kart unlock edilmiş mi?</summary>
        public bool IsUnlocked =>
            !LockedByDefault ||
            (UnlockRegistry.Instance != null && UnlockRegistry.Instance.IsCardUnlocked(Id));

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;
        }
    }
}