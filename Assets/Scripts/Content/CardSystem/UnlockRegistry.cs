using System.Collections.Generic;
using UnityEngine;

namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Shape ve Card unlock state'ini yönetir.
    /// PlayerPrefs ile run'lar arası kalıcı.
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

        // Runtime cache — PlayerPrefs her seferinde çağrılmasın
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
                PlayerPrefs.SetInt(CardKeyPrefix + cardId, 1);
        }

        // ── Shape ────────────────────────────────────────────────────────────

        public bool IsShapeUnlocked(string shapeId)
        {
            if (string.IsNullOrEmpty(shapeId)) return false;
            return _unlockedShapes.Contains(shapeId);
        }

        public void UnlockShape(string shapeId)
        {
            if (string.IsNullOrEmpty(shapeId)) return;
            if (_unlockedShapes.Add(shapeId))
                PlayerPrefs.SetInt(ShapeKeyPrefix + shapeId, 1);
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
            PlayerPrefs.SetInt(MilestoneKeyPrefix + milestoneLabel, 1);
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
            foreach (var id in shapeIds)
                if (PlayerPrefs.GetInt(ShapeKeyPrefix + id, 0) == 1)
                    _unlockedShapes.Add(id);

            _unlockedCards.Clear();
            foreach (var id in cardIds)
                if (PlayerPrefs.GetInt(CardKeyPrefix + id, 0) == 1)
                    _unlockedCards.Add(id);
        }

        public void InitMilestones(IEnumerable<string> milestoneLabels)
        {
            _reachedMilestones.Clear();
            foreach (var label in milestoneLabels)
                if (PlayerPrefs.GetInt(MilestoneKeyPrefix + label, 0) == 1)
                    _reachedMilestones.Add(label);
        }

        /// <summary>Tüm unlock verilerini siler (debug / test için).</summary>
        public void ResetAll(IEnumerable<string> shapeIds, IEnumerable<string> cardIds, IEnumerable<string> milestoneLabels)
        {
            foreach (var id in shapeIds)    PlayerPrefs.DeleteKey(ShapeKeyPrefix + id);
            foreach (var id in cardIds)     PlayerPrefs.DeleteKey(CardKeyPrefix  + id);
            foreach (var l  in milestoneLabels) PlayerPrefs.DeleteKey(MilestoneKeyPrefix + l);

            _unlockedShapes.Clear();
            _unlockedCards.Clear();
            _reachedMilestones.Clear();
        }

        // PlayerPrefs'te key listesi olmadığı için dummy — Init pattern kullanılıyor
        private static IEnumerable<string> GetAllPlayerPrefsKeys(string prefix)
            => System.Array.Empty<string>();
    }
}