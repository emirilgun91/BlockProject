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

        // ── Shape Card ────────────────────────────────────────────────────────

        [Header("Shape Card")]
        [Tooltip("Bu kart belirli bir şekli etkiliyor mu?")]
        public bool IsShapeCard = false;

        [Tooltip("Etkilenen şeklin Id'si (ShapeSO.Id ile eşleşmeli). IsShapeCard true olduğunda doldur.")]
        public string TargetShapeId = "";

        [Tooltip("Bu kartın şekle uyguladığı etkiler. Birden fazla olabilir (örn. hem score hem weight).")]
        public List<ShapeEffectEntry> ShapeEffects = new List<ShapeEffectEntry>();

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
                // 1. Kart kilidi
                bool cardUnlocked = !LockedByDefault ||
                    (UnlockRegistry.Instance != null &&
                     UnlockRegistry.Instance.IsCardUnlocked(Id));

                if (!cardUnlocked) return false;

                // 2. Shape bağımlılığı (genel)
                if (!string.IsNullOrEmpty(RequiredShapeId))
                {
                    bool shapeUnlocked = UnlockRegistry.Instance != null &&
                                        UnlockRegistry.Instance.IsShapeUnlocked(RequiredShapeId);
                    if (!shapeUnlocked) return false;
                }

                // 3. Shape kart ise hedef şekil erişilebilir olmalı.
                // UnlockRegistry sadece LockedByDefault=true olanları takip eder.
                // LockedByDefault=false shape'ler registry'e hiç girmez → IsShapeUnlocked false döner.
                // Bu yüzden: registry'de varsa unlock'lu, yoksa PlayerPrefs'te hiç kaydedilmemiş
                // demektir — yani baştan açık (LockedByDefault=false). İkisi de geçerli.
                if (IsShapeCard && !string.IsNullOrEmpty(TargetShapeId))
                {
                    var reg = UnlockRegistry.Instance;
                    if (reg == null) return false;

                    // Açıkça unlock edilmiş mi VEYA hiç kilitlenmemiş mi?
                    bool explicitlyUnlocked = reg.IsShapeUnlocked(TargetShapeId);
                    bool neverLocked        = !reg.IsShapeKnownAsLocked(TargetShapeId);

                    if (!explicitlyUnlocked && !neverLocked) return false;
                }

                return true;
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;

            if (IsShapeCard && string.IsNullOrWhiteSpace(TargetShapeId))
                Debug.LogWarning($"[CardSO] '{Id}' IsShapeCard=true ama TargetShapeId boş!", this);
        }
    }
}