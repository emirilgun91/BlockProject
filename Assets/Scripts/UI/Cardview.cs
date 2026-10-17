using System;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Tek kart UI'ı.
    ///
    /// Hierarchy (Prefab):
    ///  CardView_0               ← bu script buraya
    ///   ├── IconArea  (Image)   ← _iconImage
    ///   └── CardBody
    ///        ├── CardNameText    (TMP)    ← _nameText
    ///        ├── DescriptionText (TMP)    ← _descText
    ///        └── SelectButton   (Button)  ← _selectButton
    /// </summary>
    public sealed class CardView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("References")]
        [SerializeField] private Image    _iconImage;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _descText;
        [SerializeField] private Button   _selectButton;

        [Header("Rarity Frame")]
        [Tooltip("Kartın arka plan/çerçeve Image'ı. Boşsa bu GameObject'teki Image kullanılır.")]
        [SerializeField] private Image  _frameImage;
        [Tooltip("Boş bırakılan rarity prefabdaki varsayılan çerçeveyi korur.")]
        [SerializeField] private Sprite _frameCommon;
        [SerializeField] private Sprite _frameUncommon;
        [SerializeField] private Sprite _frameRare;
        [SerializeField] private Sprite _frameEpic;

        [Header("Hover Animation")]
        [SerializeField] private float _hoverLift  = 18f;
        [SerializeField] private float _hoverSpeed = 10f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private CardSO         _data;
        private Action<CardSO> _onSelect;
        private Vector3        _basePos;
        private float          _targetY;
        private float          _hoverOffsetY;
        private Sprite         _defaultFrame;
        private bool           _frameResolved;
        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            _selectButton.onClick.AddListener(OnSelectClicked);
        }

        private void Update()
        {
            var pos = transform.localPosition;
            pos.y = Mathf.Lerp(pos.y, _hoverOffsetY, Time.unscaledDeltaTime * _hoverSpeed);
            transform.localPosition = pos;
        }

        // ── Public API ───────────────────────────────────────────────────────
        public void Bind(CardSO card, Action<CardSO> onSelect)
        {
            _data        = card;
            _onSelect    = onSelect;
            _hoverOffsetY = 0f;
            gameObject.SetActive(true);
 
            // Pozisyonu Layout Group'a bırak — kendimiz sıfırlıyoruz
            var pos = transform.localPosition;
            pos.y = 0f;
            transform.localPosition = pos;
 
            Render();
        }

        public void Clear()
        {
            _data     = null;
            _onSelect = null;
            gameObject.SetActive(false);
        }

        // ── Hover ────────────────────────────────────────────────────────────
        public void OnPointerEnter(PointerEventData _)  => _hoverOffsetY = _hoverLift;
        public void OnPointerExit(PointerEventData _)   => _hoverOffsetY = 0f;
        public void OnPointerClick(PointerEventData _)  => OnSelectClicked();

        // ── Private ──────────────────────────────────────────────────────────
        private void Render()
        {
            if (_data == null) return;

            _nameText.text = ContentLocalization.Name(_data);
            // Değerler (+6, 20%, 2.5x…) kalın ve renkli — okunabilirlik
            _descText.text = ValueHighlighter.Highlight(ContentLocalization.Description(_data));

            ApplyFrame(_data.Rarity);

            if (_iconImage != null)
            {
                if (_data.Icon != null)
                {
                    _iconImage.sprite = _data.Icon;
                    _iconImage.color  = Color.white;
                }
                else
                {
                    _iconImage.sprite = null;
                    _iconImage.color  = GetRarityColor(_data.Rarity);
                }
            }
        }

        private void OnSelectClicked() => _onSelect?.Invoke(_data);

        /// <summary>
        /// Rarity'ye göre çerçeve sprite'ını seçer. Kart havuzdan tekrar
        /// kullanıldığı için varsayılan çerçeve ilk seferde saklanır; o rarity'ye
        /// sprite atanmamışsa ona dönülür (bir önceki kartın çerçevesi kalmaz).
        /// </summary>
        private void ApplyFrame(CardRarity rarity)
        {
            if (!_frameResolved)
            {
                _frameResolved = true;
                if (_frameImage == null) _frameImage = GetComponent<Image>();
                if (_frameImage != null) _defaultFrame = _frameImage.sprite;
            }
            if (_frameImage == null) return;

            Sprite frame = rarity switch
            {
                CardRarity.Common   => _frameCommon,
                CardRarity.Uncommon => _frameUncommon,
                CardRarity.Rare     => _frameRare,
                CardRarity.Epic     => _frameEpic,
                _                   => null
            };
            _frameImage.sprite = frame != null ? frame : _defaultFrame;
        }

        private static Color GetRarityColor(CardRarity rarity)
        {
            var c = CardRarityPalette.Get(rarity);
            c.a = 0.5f;
            return c;
        }
    }
}