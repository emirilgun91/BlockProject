using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Tek upgrade slot'u.
    ///
    /// Hierarchy:
    ///  UpgradeSlot (bu script + Button)
    ///   ├── OwnedBadge (Image)
    ///   │    └── OwnedText (TMP)
    ///   ├── IconArea (Image)
    ///   │    └── IconImage (Image) — sprite veya TMP
    ///   ├── DotsBar (Empty)
    ///   │    └── Dot_1..N (Image)
    ///   └── LockOverlay (Image)
    ///        ├── LockIcon (TMP)
    ///        └── LockCostBadge
    ///             └── LockCostText (TMP)
    /// </summary>
    public sealed class UpgradeSlotView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button     _button;
        [SerializeField] private Image      _iconImage;
        [SerializeField] private GameObject _ownedBadge;
        [SerializeField] private Image[]    _dots;          // Inspector'da 4-5 dot bağla
        [SerializeField] private GameObject _lockOverlay;
        [SerializeField] private TMP_Text   _lockCostText;

        [Header("Dot Colors")]
        [SerializeField] private Color _dotOffColor  = new Color(0.22f, 0.39f, 0.31f, 0.23f);
        [SerializeField] private Color _dotOnColor   = new Color(0.23f, 0.56f, 0.85f);
        [SerializeField] private Color _dotOwnedColor = new Color(0.91f, 0.63f, 0.13f);

        [Header("Slot Colors")]
        [SerializeField] private Image _bgImage;
        [SerializeField] private Color _bgNormal   = new Color(0.04f, 0.07f, 0.13f);
        [SerializeField] private Color _bgSelected = new Color(0.05f, 0.11f, 0.22f);
        [SerializeField] private Color _bgOwned    = new Color(0.04f, 0.10f, 0.08f);

        // Runtime
        private UpgradeSO _upgrade;
        public UpgradeSO  Upgrade => _upgrade;

        public System.Action<UpgradeSlotView> OnSelected;

        // ── Public API ───────────────────────────────────────────────────────

        public void Bind(UpgradeSO upgrade)
        {
            _upgrade = upgrade;
            _button?.onClick.AddListener(() => OnSelected?.Invoke(this));
            Refresh(isSelected: false);
        }

        public void Refresh(bool isSelected)
        {
            if (_upgrade == null) return;

            var  registry   = UpgradeRegistry.Instance;
            int  level      = registry?.GetLevel(_upgrade.Id) ?? 0;
            bool isOwned    = level > 0;
            bool isUnlocked = registry?.IsUnlocked(_upgrade) ?? false;

            // İkon
            if (_iconImage != null && _upgrade.Icon != null)
                _iconImage.sprite = _upgrade.Icon;

            // Owned badge
            if (_ownedBadge != null)
                _ownedBadge.SetActive(isOwned);

            // Lock overlay
            if (_lockOverlay != null)
                _lockOverlay.SetActive(!isUnlocked);

            // Lock cost text
            if (_lockCostText != null)
                _lockCostText.text = $"Stage {_upgrade.UnlockStageIndex}";

            // BG rengi
            if (_bgImage != null)
            {
                _bgImage.color = isSelected ? _bgSelected
                               : isOwned    ? _bgOwned
                               :              _bgNormal;
            }

            // Dots
            RefreshDots(level, isOwned);
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void RefreshDots(int level, bool isOwned)
        {
            if (_dots == null) return;

            for (int i = 0; i < _dots.Length; i++)
            {
                if (_dots[i] == null) continue;

                // MaxLevel'dan fazla dot'u gizle
                bool inRange = i < _upgrade.MaxLevel;
                _dots[i].gameObject.SetActive(inRange);

                if (!inRange) continue;

                bool filled = i < level;
                _dots[i].color = filled
                    ? (isOwned ? _dotOwnedColor : _dotOnColor)
                    : _dotOffColor;
            }
        }
    }
}