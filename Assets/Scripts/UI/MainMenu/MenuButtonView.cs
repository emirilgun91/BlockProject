using DG.Tweening;
using RogueBlockBlast.Core.Localization;
using RogueBlockBlast.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
namespace RogueBlockBlast.UI
{
    /// <summary>
    /// MainMenu'deki tek buton.
    ///
    /// Yetenekler:
    /// - Hover: sağa kayma + sol accent bar açılması + border rengi
    /// - Lock state: üstte overlay + "Milestone X" yazısı + interactable false
    /// - Coming soon badge
    /// - Click SFX + hover SFX
    ///
    /// Hierarchy (her buton aynı):
    ///  BtnXxx (bu script + Image + Button)
    ///   ├── AccentBar (Image)          ← sol kenar, scaleY 0→1 hover
    ///   ├── TextBlock
    ///   │    ├── LabelText (TMP)
    ///   │    └── SubText   (TMP)
    ///   ├── LockOverlay (opsiyonel)
    ///   │    └── LockText (TMP)
    ///   └── ArrowText (TMP)
    /// </summary>
    public sealed class MenuButtonView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("References")]
        [SerializeField] private RectTransform _rect;
        [SerializeField] private Image         _bgImage;
        [SerializeField] private RectTransform _accentBar;
        [SerializeField] private TMP_Text      _labelText;
        [SerializeField] private Image         _arrowImage;

        [Header("Lock")]
        [SerializeField] private GameObject _lockOverlay;
        [SerializeField] private TMP_Text   _lockText;
        [SerializeField] private bool       _isLockable;
        [Tooltip("Kaçıncı milestone geçilince açılır (0 = hep açık).")]
        [SerializeField] private int _unlockMilestoneIndex = 0;

        [Header("Coming Soon")]
        [SerializeField] private bool _isComingSoon;

        [Header("Hover Animation")]
        [SerializeField] private float _hoverOffsetX   = 8f;
        [SerializeField] private float _hoverDuration  = 0.2f;
        [Header ("SFX")]
        [SerializeField] private AudioClip Click;
        [SerializeField] private AudioClip Hover;
        [Header("Colors")]
        [SerializeField] private Color _borderNormal     = new Color(0.15f, 0.39f, 0.66f, 0.65f);
        [SerializeField] private Color _borderHighlight  = new Color(0.22f, 0.51f, 0.78f, 0.85f);
        [SerializeField] private Color _arrowNormalColor = new Color(0.42f, 0.53f, 0.66f);
        [SerializeField] private Color _arrowHoverColor  = new Color(0.38f, 0.69f, 0.94f);

        // Event
        public System.Action OnClicked;

        // Runtime
        private Vector3 _originalPos;
        private bool    _isLocked;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            _originalPos = _rect.anchoredPosition;
            
            // Accent bar başlangıçta gizli
            if (_accentBar != null)
            {
                var scale = _accentBar.localScale;
                scale.y = 0f;
                _accentBar.localScale = scale;
            }
           
        }

        private IEnumerator Start()
        {
            if (_rect == null) 
                _rect = GetComponent<RectTransform>();
            yield return new WaitForEndOfFrame(); 

            _originalPos = _rect.anchoredPosition; 
            
            EvaluateLockState();
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void SetLabel(string label)
        {
            if (_labelText != null) _labelText.text = label;
        }

        public void RefreshLock() => EvaluateLockState();

        // ── Lock ─────────────────────────────────────────────────────────────

        private void EvaluateLockState()
        {
            if (_isComingSoon)
            {
                _isLocked = true;
                if (_lockOverlay != null) _lockOverlay.SetActive(true);
                if (_lockText    != null) _lockText.text = Loc.Get("Common.ComingSoon");
                return;
            }

            if (!_isLockable)
            {
                _isLocked = false;
                if (_lockOverlay != null) _lockOverlay.SetActive(false);
                return;
            }

            // Milestone kontrolü — PlayerPrefs'ten en yüksek geçilen milestone
            int maxReached = PlayerPrefs.GetInt("MaxMilestoneReached", 0);
            _isLocked = maxReached < _unlockMilestoneIndex;

            if (_lockOverlay != null) _lockOverlay.SetActive(_isLocked);
            if (_isLocked && _lockText != null)
                _lockText.text = $"MILESTONE {_unlockMilestoneIndex} GEREKLİ";
        }

        // ── Pointer Events ───────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData e)
        {
           
            if (_isLocked) return;
            if (_accentBar != null) _accentBar.DOKill();
            if (_bgImage != null) _bgImage.DOKill();
            if (_arrowImage != null) _arrowImage.DOKill();
            // Sağa kayma
            _rect.DOAnchorPosX(_originalPos.x + _hoverOffsetX, _hoverDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);

            // Accent bar aç
            if (_accentBar != null)
                _accentBar.DOScaleY(1f, _hoverDuration).SetEase(Ease.OutCubic).SetUpdate(true);

            // Border highlight
            if (_bgImage != null)
                _bgImage.DOColor(_borderHighlight, _hoverDuration).SetUpdate(true);

            // Arrow rengi
            if (_arrowImage != null)
                _arrowImage.DOColor(_arrowHoverColor, _hoverDuration).SetUpdate(true);

            AudioManager.Instance?.PlaySFX(Hover);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (_isLocked) return;
            if (_accentBar != null) _accentBar.DOKill();
            if (_bgImage != null) _bgImage.DOKill();
            if (_arrowImage != null) _arrowImage.DOKill();
            _rect.DOAnchorPosX(_originalPos.x, _hoverDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);

            if (_accentBar != null)
                _accentBar.DOScaleY(0f, _hoverDuration).SetEase(Ease.OutCubic).SetUpdate(true);

            if (_bgImage != null)
                _bgImage.DOColor(_borderNormal, _hoverDuration).SetUpdate(true);

            if (_arrowImage != null)
                _arrowImage.DOColor(_arrowNormalColor, _hoverDuration).SetUpdate(true);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (_isLocked) return;

            AudioManager.Instance?.PlaySFX(Click);
            OnClicked?.Invoke();
        }

        private void OnDisable()
        {
            if (_rect != null)
                _rect.DOKill();
            if (_accentBar != null)
                _accentBar.DOKill();
        }
    }
}