using System;
using System.Collections.Generic;
using System.Linq;
using RogueBlockBlast.Content;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Kart seçim overlay'i.
    /// Milestone'a ulaşınca oyunu duraklatır, 3 kart sunar.
    ///
    /// Hierarchy:
    ///  CardScreen (bu script + CanvasGroup buraya)
    ///   ├── Backdrop
    ///   └── Panel
    ///        ├── MilestoneBanner
    ///        ├── ChooseLabel
    ///        └── CardsContainer
    ///             ├── CardView_0  ← _cardViews[0]
    ///             ├── CardView_1  ← _cardViews[1]
    ///             └── CardView_2  ← _cardViews[2]
    ///
    /// RunController'dan çağır:
    ///   CardSelectionUI.Instance.Show(cardPool, onCardPicked);
    /// </summary>
    public sealed class CardSelectionUI : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        public static CardSelectionUI Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Root & Fade")]
        [SerializeField] private GameObject  _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float       _fadeSpeed = 8f;

        [Header("Card Views — CardsContainer altındaki 3 CardView")]
        [SerializeField] private CardView[] _cardViews;  // 3 eleman

        // ── Runtime ──────────────────────────────────────────────────────────
        private Action<CardSO> _onCardPicked;
        private bool           _fadingIn;
        private bool           _isOpen;

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // Başlangıçta kapalı
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

            // Fade out tamamlandıysa root'u kapat
            if (!_fadingIn && _canvasGroup.alpha <= 0.01f)
            {
                _root.SetActive(false);
                _isOpen = false;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Kart seçim ekranını açar.
        /// cardPool: tüm kartlar (ağırlıklı rastgele 3 tanesi seçilir)
        /// onCardPicked: kart seçilince çağrılır, null = geçildi
        /// </summary>
        public void Show(List<CardSO> cardPool, Action<CardSO> onCardPicked)
        {
            if (cardPool == null || cardPool.Count == 0)
            {
                onCardPicked?.Invoke(null);
                return;
            }

            _onCardPicked = onCardPicked;

            // Ağırlıklı rastgele 3 kart seç
            var picked = PickWeightedRandom(cardPool, Mathf.Min(3, cardPool.Count));

            // CardView'ları bağla
            for (int i = 0; i < _cardViews.Length; i++)
            {
                if (i < picked.Count)
                    _cardViews[i].Bind(picked[i], OnCardSelected);
                else
                    _cardViews[i].Clear();
            }

            // Oyunu dondur ve ekranı aç
            Time.timeScale = 0f;
            Game.GameStateController.LockInput();
            _root.SetActive(true);
            _isOpen   = true;
            _fadingIn = true;

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        /// <summary>Kart seçilmeden geçmek için (opsiyonel continue butonu için).</summary>
        public void Skip() => CloseAndResume(null);

        // ── Private ──────────────────────────────────────────────────────────
        private void OnCardSelected(CardSO card)
        {
            CloseAndResume(card);
        }

        private void CloseAndResume(CardSO card)
        {
            _fadingIn = false;
            Time.timeScale = 1f;
            Game.GameStateController.UnlockInput();
            _onCardPicked?.Invoke(card);
            _onCardPicked = null;
        }

        // ── Weighted Random ──────────────────────────────────────────────────
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