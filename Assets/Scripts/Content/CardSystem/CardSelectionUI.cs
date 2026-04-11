using System;
using System.Collections.Generic;
using System.Linq;
using RogueBlockBlast.Content;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Kart seçim overlay'i.
    ///
    /// Değişiklikler:
    /// - Kilitli kartlar gösterilmez
    /// - Zaten seçilmiş kartlar gösterilmez (unique kart)
    /// - Yeni unlock edilen kart en sola gelir + NEW badge
    ///
    /// Show() çağrısında newlyUnlockedCard parametresi varsa
    /// o kart listeye eklenir ve NEW badge gösterilir.
    /// </summary>
    public sealed class CardSelectionUI : MonoBehaviour
    {
        public static CardSelectionUI Instance { get; private set; }

        [Header("Root & Fade")]
        [SerializeField] private GameObject  _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float       _fadeSpeed = 8f;

        [Header("Card Views")]
        [SerializeField] private CardView[] _cardViews;  // 3 eleman

        [Header("New Card Badge — CardView_0 üzerinde")]
        [SerializeField] private GameObject _newBadge;   // "NEW" yazan obje, default inactive

        private Action<CardSO> _onCardPicked;
        private bool           _fadingIn;
        private bool           _isOpen;

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
        /// newlyUnlockedCard: bu run'da yeni açılan kart — en sola gelir, NEW badge alır.
        /// </summary>
        public void Show(
            List<CardSO>   cardPool,
            Action<CardSO> onCardPicked,
            CardSO         newlyUnlockedCard = null)
        {
            _onCardPicked = onCardPicked;

            // Seçilebilecek kartları filtrele
            var available = BuildAvailablePool(cardPool, newlyUnlockedCard);

            if (available.Count == 0)
            {
                // Seçilecek kart kalmadı — direkt geç
                onCardPicked?.Invoke(null);
                return;
            }

            // NEW badge — sadece yeni kart varsa ve ilk slota gelecekse
            bool showNewBadge = newlyUnlockedCard != null &&
                                available.Count > 0 &&
                                available[0] == newlyUnlockedCard;

            if (_newBadge != null)
                _newBadge.SetActive(showNewBadge);

            // CardView'ları bağla
            for (int i = 0; i < _cardViews.Length; i++)
            {
                if (i < available.Count)
                    _cardViews[i].Bind(available[i], OnCardSelected);
                else
                    _cardViews[i].Clear();
            }

            Time.timeScale = 0f;
            Game.GameStateController.LockInput();
            _root.SetActive(true);
            _isOpen   = true;
            _fadingIn = true;

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        public void Skip() => CloseAndResume(null);

        // ── Private ──────────────────────────────────────────────────────────

        /// <summary>
        /// Gösterilecek kart listesini oluşturur:
        /// 1. newlyUnlockedCard varsa en başa ekle
        /// 2. Unlock edilmiş kartları al
        /// 3. Zaten seçilmiş kartları çıkar (unique)
        /// 4. Ağırlıklı rastgele 2 kart daha seç (toplam max 3)
        /// </summary>
        private List<CardSO> BuildAvailablePool(List<CardSO> allCards, CardSO newCard)
        {
            var result = new List<CardSO>();

            // Yeni kart en başa
            if (newCard != null && newCard.IsUnlocked)
                result.Add(newCard);

            // Seçilmiş kart ID'leri
            var selectedIds = GetSelectedCardIds();

            // Kalan havuz:
            // - Unlock edilmiş
            // - newCard değil
            // - Unique kartsa ve zaten seçildiyse çıkar
            // - Unique değilse seçilmiş olsa bile tekrar gelebilir
            var pool = allCards
                .Where(c =>
                    c != null &&
                    c.IsUnlocked &&
                    c != newCard &&
                    !(c.IsUnique && selectedIds.Contains(c.Id)))
                .ToList();

            // Kaç slot doluyor?
            int remaining = Mathf.Min(_cardViews.Length - result.Count, pool.Count);
            var picked    = PickWeightedRandom(pool, remaining);
            result.AddRange(picked);

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