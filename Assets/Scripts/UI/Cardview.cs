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

        [Header("Hover Animation")]
        [SerializeField] private float _hoverLift  = 18f;
        [SerializeField] private float _hoverSpeed = 10f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private CardSO         _data;
        private Action<CardSO> _onSelect;
        private Vector3        _basePos;
        private float          _targetY;
        private float          _hoverOffsetY;
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
            _descText.text = ContentLocalization.Description(_data);

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

        private static Color GetRarityColor(CardRarity rarity) => rarity switch
        {
            CardRarity.Common   => new Color(0.17f, 0.43f, 0.64f, 0.5f),
            CardRarity.Uncommon => new Color(0.08f, 0.56f, 0.47f, 0.5f),
            CardRarity.Rare     => new Color(0.36f, 0.31f, 0.81f, 0.5f),
            CardRarity.Epic     => new Color(0.56f, 0.27f, 0.68f, 0.5f),
            _                   => Color.grey
        };
    }
}