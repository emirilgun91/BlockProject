using System;
using System.Collections.Generic;
using System.Linq;
using RogueBlockBlast.Content;
using TMPro;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    public sealed class CardSelectionUI : MonoBehaviour
    {
        public static CardSelectionUI Instance { get; private set; }

        [Header("Root & Fade")]
        [SerializeField] private GameObject  _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float       _fadeSpeed = 8f;

        [Header("Card Views")]
        [SerializeField] private CardView[] _cardViews;

        [Header("New Card Badge")]
        [SerializeField] private GameObject _newBadge;

        [Header("Card Reroll")]
        [SerializeField] private GameObject _rerollButton;    // reroll butonu root
        [SerializeField] private TMP_Text   _rerollCountText; // "x2"
        [SerializeField] private AudioClip CardSelected;
        /// <summary>
        /// Opsiyonel dış sunum. Atanmışsa bu bileşen kendi UGUI panelini
        /// açmaz; kart listesini sunucuya devreder. Havuz mantığı (kilit
        /// filtresi, unique, ağırlık, reroll) her iki yolda da burada kalır —
        /// tek kaynak.
        ///
        /// 2.5D prototipi bunu dünya-uzayı fiziksel kartlar için kullanıyor;
        /// bkz. <c>Game.Prototype.PhysicalCardPresenter</c>. Null bırakılırsa
        /// davranış eskisiyle birebir aynıdır.
        /// </summary>
        public ICardPresenter ExternalPresenter { get; set; }

        private Action<CardSO> _onCardPicked;
        private bool           _fadingIn;
        private bool           _isOpen;

        // Reroll state
        private List<CardSO> _currentCardPool;
        private CardSO       _currentNewlyUnlocked;
        private int          _rerollsRemaining;
        // Reroll'da gösterilen kartları exclude etmek için
        private readonly HashSet<string> _shownCardIds = new();
        

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha          = 0f;
                _canvasGroup.interactable   = false;
                _canvasGroup.blocksRaycasts = false;
            }
            _root.SetActive(false);
        }

        private void Update()
        {
            if (!_isOpen || _canvasGroup == null) return;

            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha,
                _fadingIn ? 1f : 0f,
                Time.unscaledDeltaTime * _fadeSpeed
            );

            bool visible = _canvasGroup.alpha > 0.01f;
            _canvasGroup.interactable   = visible && _fadingIn;
            _canvasGroup.blocksRaycasts = visible;

            if (!_fadingIn && _canvasGroup.alpha <= 0.01f)
            {
                _root.SetActive(false);
                _isOpen = false;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Kart seçim ekranını açar.
        /// rerollCount: CardReroll upgrade'inden gelen hak sayısı.
        /// </summary>
        public void Show(
            List<CardSO>   cardPool,
            Action<CardSO> onCardPicked,
            CardSO         newlyUnlockedCard = null,
            int            rerollCount       = 0)
        {
            _onCardPicked         = onCardPicked;
            _currentCardPool      = cardPool;
            _currentNewlyUnlocked = newlyUnlockedCard;
            _rerollsRemaining     = rerollCount;
            _shownCardIds.Clear();

            var available = BuildAvailablePool(cardPool, newlyUnlockedCard);

            if (available.Count == 0)
            {
                onCardPicked?.Invoke(null);
                return;
            }

            ApplyCards(available, newlyUnlockedCard);
            UpdateRerollButton();

            Time.timeScale = 0f;
            Game.GameStateController.LockInput();

            // Dış sunum varsa UGUI paneli hiç açılmaz — sunucu ApplyCards
            // içinde çoktan devreye girdi.
            if (ExternalPresenter != null) return;

            _root.SetActive(true);
            _isOpen   = true;
            _fadingIn = true;

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        public void Skip() => CloseAndResume(null);

        /// <summary>Reroll butonu onClick bağlantısı.</summary>
        public void OnRerollClicked()
        {
            if (_rerollsRemaining <= 0) return;

            _rerollsRemaining--;

            // Şu an gösterilen kartları exclude listesine ekle
            foreach (var view in _cardViews)
            {
                // CardView'dan mevcut kartı al — null kontrolü
                // Reroll'da yeni kart badge gösterme
            }

            var available = BuildAvailablePool(_currentCardPool, null, excludeShown: true);
            ApplyCards(available, null);
            UpdateRerollButton();
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void ApplyCards(List<CardSO> available, CardSO newCard)
        {
            // Shown ID'leri güncelle
            foreach (var c in available)
                if (c != null) _shownCardIds.Add(c.Id);

            // NEW badge
            bool showNewBadge = newCard != null &&
                                available.Count > 0 &&
                                available[0] == newCard;

            if (ExternalPresenter != null)
            {
                ExternalPresenter.Present(
                    available, showNewBadge, OnCardSelected,
                    _rerollsRemaining, OnRerollClicked);
                return;
            }

            if (_newBadge != null)
                _newBadge.SetActive(showNewBadge);

            for (int i = 0; i < _cardViews.Length; i++)
            {
                if (i < available.Count)
                    _cardViews[i].Bind(available[i], OnCardSelected);
                else
                    _cardViews[i].Clear();
            }
        }

        private void UpdateRerollButton()
        {
            if (_rerollButton == null) return;

            bool hasUpgrade = _rerollsRemaining > 0 || CanShowReroll();
            _rerollButton.SetActive(hasUpgrade);

            if (_rerollCountText != null)
                _rerollCountText.text = $"x{_rerollsRemaining}";

            // Butonu disable et — hak bitti
            var btn = _rerollButton.GetComponentInChildren<UnityEngine.UI.Button>();
            if (btn != null) btn.interactable = _rerollsRemaining > 0;
        }

        private bool CanShowReroll()
        {
            // Upgrade seviyesi > 0 ise butonu göster (hak 0 olsa bile görünür ama disabled)
            var reg = Core.UpgradeRegistry.Instance;
            return (reg?.GetLevel("upgrade_card_reroll") ?? 0) > 0;
        }

        private List<CardSO> BuildAvailablePool(
            List<CardSO> allCards,
            CardSO newCard,
            bool excludeShown = false)
        {
            var result = new List<CardSO>();

            if (newCard != null && newCard.IsUnlocked)
                result.Add(newCard);

            var selectedIds = GetSelectedCardIds();

            var pool = allCards
                .Where(c =>
                    c != null &&
                    c.IsUnlocked &&
                    c != newCard &&
                    !(c.IsUnique && selectedIds.Contains(c.Id)) &&
                    !(excludeShown && _shownCardIds.Contains(c.Id)))
                .ToList();

            int remaining = Mathf.Min(_cardViews.Length - result.Count, pool.Count);
            result.AddRange(PickWeightedRandom(pool, remaining));

            return result;
        }

        private HashSet<string> GetSelectedCardIds()
        {
            var ids = new HashSet<string>();
            if (CardInventoryUI.Instance == null) return ids;
            foreach (var id in CardInventoryUI.Instance.GetSelectedCardIds())
                ids.Add(id);
            return ids;
        }

        private void OnCardSelected(CardSO card) => CloseAndResume(card);

        private void CloseAndResume(CardSO card)
        {
            AudioManager.Instance.PlaySFX(CardSelected);

            // Kapanış animasyonu oyunu bloklamaz: timeScale hemen geri gelir,
            // sunucu kendi çıkışını unscaled zamanda oynatır.
            ExternalPresenter?.Dismiss(card);

            _fadingIn      = false;
            Time.timeScale = 1f;
            Game.GameStateController.UnlockInput();

            if (_newBadge != null) _newBadge.SetActive(false);

            _onCardPicked?.Invoke(card);
            _onCardPicked = null;
        }

        private static List<CardSO> PickWeightedRandom(List<CardSO> pool, int count)
        {
            var remaining = new List<CardSO>(pool);
            var result    = new List<CardSO>(count);

            for (int i = 0; i < count && remaining.Count > 0; i++)
            {
                int total = remaining.Sum(c => Mathf.Max(1, c.BaseWeight));
                int roll  = UnityEngine.Random.Range(0, total);
                int cum   = 0;

                for (int j = 0; j < remaining.Count; j++)
                {
                    cum += Mathf.Max(1, remaining[j].BaseWeight);
                    if (roll < cum)
                    {
                        result.Add(remaining[j]);
                        remaining.RemoveAt(j);
                        break;
                    }
                }
            }

            return result;
        }
    }
}