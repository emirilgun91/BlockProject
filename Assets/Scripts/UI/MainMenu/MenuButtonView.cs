using DG.Tweening;
using RogueBlockBlast.Core.Localization;
using RogueBlockBlast.Core;
using TMPro;
using RogueBlockBlast.Core.Save;
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
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
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
        private bool    _originalPosReady;
        private bool    _isLocked;

        /// <summary>
        /// Hover'ın döndüğü taban konum, layout kurulduktan sonra bir kez okunur.
        /// Awake'te okunan değer VerticalLayoutGroup yerleştirmeden önceki x = 0'dı.
        /// Gamepad'le menüye dönülünce UINav New Run'ı aynı karede seçiyor ve hover
        /// bu yanlış tabana (+8) gidiyordu: buton sola kayık kalıyordu. Start'ta
        /// okumak da yetmez, o sırada hover animasyonu sürüyor olabilir.
        /// </summary>
        private void EnsureOriginalPos()
        {
            if (_originalPosReady) return;
            if (_rect.parent is RectTransform parent) LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            _originalPos      = _rect.anchoredPosition;
            _originalPosReady = true;
        }

        // ── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();

            // Accent bar başlangıçta gizli
            if (_accentBar != null)
            {
                var scale = _accentBar.localScale;
                scale.y = 0f;
                _accentBar.localScale = scale;
            }

            // Gamepad gezinmesi için bir Selectable gerekir; bu butonlar yalnızca
            // pointer olaylarıyla çalışıyordu ve EventSystem onları hiç seçemiyordu.
            // Görsel geri bildirimi hover animasyonu verir (OnSelect), geçiş efekti yok.
            if (!TryGetComponent<Selectable>(out _))
                gameObject.AddComponent<Selectable>().transition = Selectable.Transition.None;
        }

        private IEnumerator Start()
        {
            if (_rect == null) 
                _rect = GetComponent<RectTransform>();
            yield return new WaitForEndOfFrame();

            EnsureOriginalPos();

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
            ResolveLockState();

            // Kilitli buton gamepad gezinmesinde atlanır.
            if (TryGetComponent<Selectable>(out var selectable)) selectable.interactable = !_isLocked;
        }

        private void ResolveLockState()
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

            // Milestone kontrolü — SaveStore'dan en yüksek geçilen milestone
            int maxReached = SaveStore.GetInt("MaxMilestoneReached", 0);
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
            EnsureOriginalPos();
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
            EnsureOriginalPos();
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

        // ── Gamepad ──────────────────────────────────────────────────────────
        // Seçim, hover'la aynı görünür. Yalnızca gamepad'de: fareyle tıklanan buton
        // da seçili kalır ve fare çekilince hover animasyonu geri sarılmalı.

        public void OnSelect(BaseEventData e)
        {
            if (InputMode.UsingGamepad) OnPointerEnter(null);
        }

        public void OnDeselect(BaseEventData e) => OnPointerExit(null);

        public void OnSubmit(BaseEventData e) => OnPointerClick(null);

        private void OnDisable()
        {
            if (_rect != null)
                _rect.DOKill();
            if (_accentBar != null)
                _accentBar.DOKill();
        }
    }
}