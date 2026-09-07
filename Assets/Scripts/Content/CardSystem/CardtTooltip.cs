using RogueBlockBlast.Content;
using RogueBlockBlast.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Kart slot'larının üzerine gelinince gösterilen tooltip.
    /// Screen Space Overlay Canvas'ta çalışır.
    ///
    /// Hierarchy:
    ///  CardTooltip (bu script + CanvasGroup)
    ///   ├── Background   (Image)
    ///   ├── CardName     (TMP)
    ///   ├── Description  (TMP)
    ///   └── EffectRow    (opsiyonel — effect listesi için)
    /// </summary>
    public sealed class CardTooltip : MonoBehaviour
    {
        public static CardTooltip Instance { get; private set; }

        [Header("References")]
        [SerializeField] private RectTransform _rect;
        [SerializeField] private CanvasGroup   _canvasGroup;
        [SerializeField] private TMP_Text      _nameText;
        [SerializeField] private TMP_Text      _descText;
        [SerializeField] private TMP_Text      _effectText;   // opsiyonel

        [Header("Settings")]
        [SerializeField] private Vector2 _offset   = new Vector2(12f, -8f);
        [SerializeField] private float   _margin   = 8f;

        private Canvas _canvas;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            _canvas = GetComponentInParent<Canvas>();
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

            // gameObject.SetActive(false) değil — Awake çalışmaz
            _canvasGroup.alpha          = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable   = false;

            Hide();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <param name="liveValue">
        /// Kartın bu run içindeki anlık katkısı (slot altındaki satırın aynısı).
        /// Boşsa hiç yazılmaz. Tooltip kartın NE YAPTIĞINI anlatıyordu ama
        /// NE KAZANDIRDIĞINI anlatmıyordu — oyuncunun asıl merak ettiği ikincisi.
        /// </param>
        public void Show(CardSO card, int stackCount, Vector2 screenPos, string liveValue = null)
        {
            if (card == null) return;

            // İsim — stack varsa badge ekle
            string cardName = ContentLocalization.Name(card);
            _nameText.text = stackCount > 1
                ? $"{cardName}  <size=11><color=#8a93aa>x{stackCount}</color></size>"
                : cardName;

            _descText.text = ContentLocalization.Description(card);

            // Effect özeti
            if (_effectText != null)
            {
                var sb = new System.Text.StringBuilder();

                if (card.Effects != null)
                {
                    for (int i = 0; i < card.Effects.Count; i++)
                    {
                        // Anahtar sırası 1'den başlar; asset'teki metin yedek
                        // olarak geçilir, böylece çevirisi olmayan efekt boş
                        // kalmaz.
                        string line = ContentLocalization.EffectDescription(
                            card, i + 1, card.Effects[i].Description);

                        if (!string.IsNullOrWhiteSpace(line))
                            sb.AppendLine($"▸  {line}");
                    }
                }

                // Efekt listesi dolu ama açıklamaları boş olabilir — şu an tüm
                // kartlarda öyle. Eskiden bu durumda boş bir yazı nesnesi açık
                // kalıyor ve tooltip'te yer kaplıyordu; ölçüte metnin kendisi
                // alınmalı, efekt sayısı değil.
                // Anlık katkı en alta, vurgulu renkte: efekt açıklamaları kuralı
                // anlatır, bu satır o kuralın bu run'daki sonucunu söyler.
                if (!string.IsNullOrWhiteSpace(liveValue))
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append($"<color=#e8c26a>{liveValue}</color>");
                }

                string effects = sb.ToString().TrimEnd();

                _effectText.text = effects;
                _effectText.gameObject.SetActive(effects.Length > 0);
            }

            _canvasGroup.alpha          = 1f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable   = false;
            // gameObject.SetActive(true) yok
            PositionTooltip(screenPos);

            // Sonraki frame'de boyutu biliyoruz — pozisyonu hemen set et
            PositionTooltip(screenPos);
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0f;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        public void UpdatePosition(Vector2 screenPos) => PositionTooltip(screenPos);

        // ── Private ──────────────────────────────────────────────────────────
        private void PositionTooltip(Vector2 screenPos)
        {
            if (_rect == null) return;

            // World Space Canvas için direkt screen pozisyonunu kullan
            // rect.position screen koordinatını kabul eder
            Vector3 worldPos = _canvas.worldCamera != null
                ? _canvas.worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, _canvas.planeDistance))
                : Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));

            _rect.position = worldPos + (Vector3)_offset * 0.01f;
        }
    }
}