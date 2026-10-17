using System.Collections.Generic;
using RogueBlockBlast.Core.Save;
using UnityEngine;

namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Shape ve Card unlock state'ini yönetir.
    /// SaveStore (save.json) ile run'lar arası kalıcı.
    ///
    /// Kullanım:
    ///   UnlockRegistry.Instance.IsCardUnlocked(id)
    ///   UnlockRegistry.Instance.UnlockCard(id)
    ///   UnlockRegistry.Instance.IsShapeUnlocked(id)
    ///   UnlockRegistry.Instance.UnlockShape(id)
    /// </summary>
    public sealed class UnlockRegistry : MonoBehaviour
    {
        public static UnlockRegistry Instance { get; private set; }

        private const string CardKeyPrefix  = "Unlocked_Card_";
        private const string ShapeKeyPrefix = "Unlocked_Shape_";

        // Milestone'ların ilk kez geçilip geçilmediği
        private const string MilestoneKeyPrefix = "MilestoneFirstReach_";

        // Runtime cache — SaveStore her seferinde çağrılmasın
        private readonly HashSet<string> _unlockedCards  = new();
        private readonly HashSet<string> _unlockedShapes = new();
        private readonly HashSet<string> _reachedMilestones = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadAll();
        }

        /// <summary>
        /// Runtime cache'i boşaltır — kayıt dışarıdan silindiğinde
        /// (ör. ilerleme sıfırlama) çağrılır. Sonrasında Init ile yeniden yüklenmeli.
        /// </summary>
        public void ClearRuntimeCache()
        {
            _unlockedCards.Clear();
            _unlockedShapes.Clear();
            _reachedMilestones.Clear();
            _lockedShapes.Clear();
        }

        // ── Card ─────────────────────────────────────────────────────────────

        public bool IsCardUnlocked(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            return _unlockedCards.Contains(cardId);
        }

        public void UnlockCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            if (_unlockedCards.Add(cardId))
                SaveStore.SetInt(CardKeyPrefix + cardId, 1);
        }

        // Kilitli olarak tanınan shape ID'leri (LockedByDefault=true olanlar Init'te eklenir)
        private readonly HashSet<string> _lockedShapes = new();

        // ── Shape ────────────────────────────────────────────────────────────

        public bool IsShapeUnlocked(string shapeId)
        {
            if (string.IsNullOrEmpty(shapeId)) return false;
            return _unlockedShapes.Contains(shapeId);
        }

        /// <summary>
        /// Bu shape daha önce "kilitli" olarak Init'e geçildi mi?
        /// False dönerse shape hiç kilitlenmemiş demektir — baştan açık kabul edilir.
        /// </summary>
        public bool IsShapeKnownAsLocked(string shapeId)
        {
            if (string.IsNullOrEmpty(shapeId)) return false;
            return _lockedShapes.Contains(shapeId);
        }

        public void UnlockShape(string shapeId)
        {
            if (string.IsNullOrEmpty(shapeId)) return;
            if (!_unlockedShapes.Add(shapeId)) return;
            SaveStore.SetInt(ShapeKeyPrefix + shapeId, 1);
            SaveStore.Save();

            Achievements.AchievementService.Unlock(Achievements.AchievementIds.FirstShapeUnlock);
            if (AllLockedShapesUnlocked())
                Achievements.AchievementService.Unlock(Achievements.AchievementIds.AllShapes);
        }

        /// <summary>Başlangıçta kilitli olan şekillerin hepsi açıldı mı?</summary>
        private bool AllLockedShapesUnlocked()
        {
            if (_lockedShapes.Count == 0) return false;
            foreach (var id in _lockedShapes)
                if (!_unlockedShapes.Contains(id)) return false;
            return true;
        }

        // ── Milestone first reach ────────────────────────────────────────────

        /// <summary>
        /// Bu milestone ilk kez mi geçiliyor?
        /// Evet ise true döner ve "geçildi" olarak işaretler.
        /// </summary>
        public bool IsFirstMilestoneReach(string milestoneLabel)
        {
            if (_reachedMilestones.Contains(milestoneLabel)) return false;

            _reachedMilestones.Add(milestoneLabel);
            SaveStore.SetInt(MilestoneKeyPrefix + milestoneLabel, 1);
            return true;
        }

        // ── Save / Load ──────────────────────────────────────────────────────

        private void LoadAll()
        {
            // Card'lar
            _unlockedCards.Clear();
            foreach (string key in GetAllPlayerPrefsKeys(CardKeyPrefix))
                _unlockedCards.Add(key.Substring(CardKeyPrefix.Length));

            // Shape'ler
            _unlockedShapes.Clear();
            foreach (string key in GetAllPlayerPrefsKeys(ShapeKeyPrefix))
                _unlockedShapes.Add(key.Substring(ShapeKeyPrefix.Length));

            // Milestone'lar
            _reachedMilestones.Clear();
            foreach (string key in GetAllPlayerPrefsKeys(MilestoneKeyPrefix))
                _reachedMilestones.Add(key.Substring(MilestoneKeyPrefix.Length));
        }

        /// <summary>
        /// PlayerPrefs key'lerini prefix'e göre bulmak için.
        /// Unity'de tüm key'leri listeleme API'si yok —
        /// bu yüzden known ID'leri dışarıdan geçirme yöntemi daha güvenli.
        /// Alternatif: Init(shapeIds, cardIds) pattern.
        /// </summary>
        public void Init(
            IEnumerable<string> shapeIds,
            IEnumerable<string> cardIds)
        {
            _unlockedShapes.Clear();
            _lockedShapes.Clear();

            foreach (var id in shapeIds)
                if (SaveStore.GetInt(ShapeKeyPrefix + id, 0) == 1)
                    _unlockedShapes.Add(id);

            _unlockedCards.Clear();
            foreach (var id in cardIds)
                if (SaveStore.GetInt(CardKeyPrefix + id, 0) == 1)
                    _unlockedCards.Add(id);
        }

        /// <summary>
        /// Hangi shape'lerin LockedByDefault=true olduğunu bildirir.
        /// RunController.Start() içinde Init'ten hemen sonra çağrılmalı.
        /// </summary>
        public void RegisterLockedShapes(IEnumerable<string> lockedShapeIds)
        {
            _lockedShapes.Clear();
            foreach (var id in lockedShapeIds)
                _lockedShapes.Add(id);
        }

        public void InitMilestones(IEnumerable<string> milestoneLabels)
        {
            _reachedMilestones.Clear();
            foreach (var label in milestoneLabels)
                if (SaveStore.GetInt(MilestoneKeyPrefix + label, 0) == 1)
                    _reachedMilestones.Add(label);
        }

        /// <summary>Tüm unlock verilerini siler (debug / test için).</summary>
        public void ResetAll(IEnumerable<string> shapeIds, IEnumerable<string> cardIds, IEnumerable<string> milestoneLabels)
        {
            foreach (var id in shapeIds)    SaveStore.DeleteKey(ShapeKeyPrefix + id);
            foreach (var id in cardIds)     SaveStore.DeleteKey(CardKeyPrefix  + id);
            foreach (var l  in milestoneLabels) SaveStore.DeleteKey(MilestoneKeyPrefix + l);

            _unlockedShapes.Clear();
            _unlockedCards.Clear();
            _reachedMilestones.Clear();
        }

        // PlayerPrefs'te key listesi olmadığı için dummy — Init pattern kullanılıyor
        private static IEnumerable<string> GetAllPlayerPrefsKeys(string prefix)
            => System.Array.Empty<string>();
    }
}