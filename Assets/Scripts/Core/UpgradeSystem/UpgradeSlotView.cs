using DG.Tweening;
using RogueBlockBlast.Core.Localization;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using RogueBlockBlast.UI.FX;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    public sealed class UpgradeSlotView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
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

        private UpgradeSlotFX _fx;
        private int           _lastLevel = -1;   // dot dolum animasyonunu tetiklemek için

        public System.Action<UpgradeSlotView> OnSelected;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            // FX katmanları runtime'da enjekte edilir — prefab kurulumu gerekmez.
            _fx = GetComponent<UpgradeSlotFX>() ?? gameObject.AddComponent<UpgradeSlotFX>();
            _fx.Initialize(_iconImage != null ? (RectTransform)_iconImage.transform : null);
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void Bind(UpgradeSO upgrade)
        {
            _upgrade = upgrade;
            _button?.onClick.AddListener(() => OnSelected?.Invoke(this));
            Refresh(isSelected: false);
        }

        /// <summary>Satın alma sonrası altın flaş + radyal glow.</summary>
        public void PlayPurchaseFeedback() => _fx?.PlayPurchaseFeedback();

        // ── Pointer ──────────────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData e)
        {
            if (_button != null && !_button.interactable) return;
            _fx?.OnHoverEnter();
        }

        public void OnPointerExit(PointerEventData e) => _fx?.OnHoverExit();

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
                _lockCostText.text = Loc.Get("Upgrades.LockedStage", _upgrade.UnlockStageIndex);

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

            // İlk kurulumda animasyon yok; sonrasında yalnızca değişen dot'lar yumuşak geçer.
            bool animate   = _lastLevel >= 0 && _lastLevel != level;
            int  prevLevel = _lastLevel < 0 ? level : _lastLevel;
            _lastLevel = level;

            for (int i = 0; i < _dots.Length; i++)
            {
                if (_dots[i] == null) continue;

                // MaxLevel'dan fazla dot'u gizle
                bool inRange = i < _upgrade.MaxLevel;
                _dots[i].gameObject.SetActive(inRange);

                if (!inRange) continue;

                bool filled = i < level;
                var  target = filled
                    ? (isOwned ? _dotOwnedColor : _dotOnColor)
                    : _dotOffColor;

                var dot = _dots[i];
                dot.DOKill();

                // Bu geçişte yeni dolan dot — kısa bir gecikmeyle sırayla dolsun.
                bool justFilled = animate && filled && i >= prevLevel;

                if (!animate)
                {
                    dot.color = target;
                    continue;
                }

                float delay = justFilled ? (i - prevLevel) * 0.06f : 0f;
                dot.DOColor(target, 0.28f).SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true);

                if (justFilled)
                {
                    var rect = (RectTransform)dot.transform;
                    rect.DOKill();
                    rect.localScale = Vector3.one;
                    rect.DOPunchScale(Vector3.one * 0.22f, 0.32f, 6, 0.6f)
                        .SetDelay(delay).SetUpdate(true);
                }
            }
        }
    }
}