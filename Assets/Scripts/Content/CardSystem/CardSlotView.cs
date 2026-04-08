using RogueBlockBlast.Content;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Tek kart slot'u — ikon + stack badge + hover tooltip.
    ///
    /// Hierarchy:
    ///  CardSlot (bu script)
    ///   ├── IconImage   (Image)
    ///   ├── RarityBorder (Image — ince border)
    ///   └── StackBadge
    ///        └── StackText (TMP — "x2")
    /// </summary>
    public sealed class CardSlotView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [Header("References")]
        [SerializeField] private Image    _iconImage;
        [SerializeField] private Image    _rarityBorder;
        [SerializeField] private GameObject _stackBadge;
        [SerializeField] private TMP_Text   _stackText;

        // Rarity renkleri — palette ile uyumlu
        private static readonly Color RarityCommon   = HexColor("2E6DA4");
        private static readonly Color RarityUncommon = HexColor("148F77");
        private static readonly Color RarityRare     = HexColor("5B4FCF");
        private static readonly Color RarityEpic     = HexColor("8E44AD");

        private CardSO _card;
        private int    _stackCount;

        // ── Public API ───────────────────────────────────────────────────────

        public void Bind(CardSO card, int stackCount)
        {
            _card       = card;
            _stackCount = stackCount;

            // İkon
            if (_iconImage != null)
            {
                _iconImage.sprite  = card.Icon;
                _iconImage.color   = card.Icon != null
                    ? Color.white
                    : GetRarityColor(card.Rarity) * 0.6f;
            }

            // Rarity border
            if (_rarityBorder != null)
                _rarityBorder.color = GetRarityColor(card.Rarity);

            // Stack badge
            if (_stackBadge != null)
            {
                bool showBadge = stackCount > 1;
                _stackBadge.SetActive(showBadge);
                if (showBadge && _stackText != null)
                    _stackText.text = $"x{stackCount}";
            }
        }

        // ── Hover ────────────────────────────────────────────────────────────
        public void OnPointerEnter(PointerEventData e)
        {
            CardTooltip.Instance?.Show(_card, _stackCount, GetScreenPos());
        }

        public void OnPointerExit(PointerEventData e)
        {
            CardTooltip.Instance?.Hide();
        }

        public void OnPointerMove(PointerEventData e)
        {
            CardTooltip.Instance?.UpdatePosition(GetScreenPos());
        }

        // ── Private ──────────────────────────────────────────────────────────
        private Vector2 GetScreenPos()
        {
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            return Vector2.zero;
        }

        private static Color GetRarityColor(CardRarity rarity) => rarity switch
        {
            CardRarity.Common   => RarityCommon,
            CardRarity.Uncommon => RarityUncommon,
            CardRarity.Rare     => RarityRare,
            CardRarity.Epic     => RarityEpic,
            _                   => RarityCommon,
        };

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}