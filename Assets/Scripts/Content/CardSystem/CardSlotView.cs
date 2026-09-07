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

        [Tooltip("Kartın anlık katkısını gösteren satır. Boş bırakılırsa runtime'da oluşturulur.")]
        [SerializeField] private TMP_Text   _liveValueText;

        [Header("Icon Fit")]
        [Tooltip("AÇIK (varsayılan): ikon slotun tamamını (alt şerit hariç) doldurur.\n\n" +
                 "Prefabda ikon yatayda esniyor ama dikeyde 62px'e kilitliydi; slot " +
                 "büyüdüğünde ikon büyümüyor, minicik kalıyordu.")]
        [SerializeField] private bool _fitIconToSlot = true;

        [Tooltip("İkonun kenar boşluğu (px).")]
        [SerializeField] private float _iconPadding = 7f;

        // Rarity renkleri — palette ile uyumlu
        private static readonly Color RarityCommon   = HexColor("2E6DA4");
        private static readonly Color RarityUncommon = HexColor("148F77");
        private static readonly Color RarityRare     = HexColor("5B4FCF");
        private static readonly Color RarityEpic     = HexColor("8E44AD");

        private CardSO _card;
        private int    _stackCount;

        // ── Public API ───────────────────────────────────────────────────────

        private void Awake() => FitIcon();

        /// <summary>
        /// İkonu slotun tamamına yayar — altta canlı değer şeridine yer bırakarak.
        ///
        /// Prefabda ikon <c>anchorMin(0,1) / anchorMax(1,1)</c> ve
        /// <c>sizeDelta(-14, 62)</c> ile duruyordu: yatayda esniyor ama dikeyde
        /// 62px'e kilitli. Slot büyüdüğünde ikon büyümüyordu. Burada iki eksende
        /// de esnetilir; <c>preserveAspect</c> ile de bozulması engellenir.
        /// </summary>
        private void FitIcon()
        {
            if (!_fitIconToSlot || _iconImage == null) return;

            var rect = _iconImage.rectTransform;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot     = new Vector2(0.5f, 0.5f);

            float pad = Mathf.Max(0f, _iconPadding);
            rect.offsetMin = new Vector2(pad, LiveValueBandHeight);
            rect.offsetMax = new Vector2(-pad, -pad);

            // Slot kare olmayabilir; ikon çarpılmasın.
            _iconImage.preserveAspect = true;
        }

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

        /// <summary>Bu slotun bağlı olduğu kart — envanterin canlı değer yenilemesi için.</summary>
        public CardSO Card => _card;

        /// <summary>
        /// Kartın anlık katkısını yazar (örn. "+8% (4 cards)").
        /// null/boş → satır gizlenir. Değer daima efektin okuduğu
        /// flag/registry'den gelir, burada hesap yapılmaz.
        /// </summary>
        public void SetLiveValue(string text)
        {
            // Tooltip de aynı metni gösteriyor — tek kaynak, iki görünüm.
            _liveValue = text;

            EnsureLiveValueText();
            if (_liveValueText == null) return;

            bool show = !string.IsNullOrEmpty(text);
            _liveValueText.gameObject.SetActive(show);
            if (show) _liveValueText.text = text;
        }

        /// <summary>
        /// Prefab'da referans yoksa slotun ALT ŞERİDİNDE okunur bir TMP satırı üretir.
        /// Hücre yüksekliği ikon + bu şerit olacak şekilde ayarlanır (CardInventoryUI),
        /// böylece yazı ikonun üstüne binmez.
        /// </summary>
        private void EnsureLiveValueText()
        {
            if (_liveValueText != null || _liveValueCreated) return;
            _liveValueCreated = true;

            var go = new GameObject("LiveValueText", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot     = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, LiveValueBandHeight);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize      = 15f;
            tmp.fontStyle     = FontStyles.Bold;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.color         = new Color(1f, 0.86f, 0.35f);
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode  = TextOverflowModes.Ellipsis;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 9f;
            tmp.fontSizeMax = 15f;

            _liveValueText = tmp;
        }

        /// <summary>Hücrenin altında canlı değer yazısına ayrılan yükseklik (px).</summary>
        public const float LiveValueBandHeight = 22f;

        private bool _liveValueCreated;
        private string _liveValue;

        // ── Hover ────────────────────────────────────────────────────────────
        public void OnPointerEnter(PointerEventData e)
        {
            CardTooltip.Instance?.Show(_card, _stackCount, GetScreenPos(), _liveValue);
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