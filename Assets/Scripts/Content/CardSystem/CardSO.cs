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

        [Tooltip("Bu kart hangi shape unlock edilince kullanılabilir olur. Boş bırakılırsa shape bağımlılığı yok.")]
        public string RequiredShapeId = "";

        /// <summary>
        /// Runtime'da bu kart kullanılabilir mi?
        /// İki koşulun ikisi de sağlanmalı:
        /// 1. Kart kendisi unlock edilmiş (LockedByDefault false veya UnlockRegistry'de var)
        /// 2. RequiredShapeId varsa o shape unlock edilmiş olmalı
        /// </summary>
        public bool IsUnlocked
        {
            get
            {
                // Kart kilidi
                bool cardUnlocked = !LockedByDefault ||
                    (UnlockRegistry.Instance != null &&
                     UnlockRegistry.Instance.IsCardUnlocked(Id));

                if (!cardUnlocked) return false;

                // Shape bağımlılığı
                if (!string.IsNullOrEmpty(RequiredShapeId))
                {
                    bool shapeUnlocked = UnlockRegistry.Instance != null &&
                                        UnlockRegistry.Instance.IsShapeUnlocked(RequiredShapeId);
                    if (!shapeUnlocked) return false;
                }

                return true;
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;
        }
    }
}