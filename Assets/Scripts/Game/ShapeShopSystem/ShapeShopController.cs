using System.Collections.Generic;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Shape Shop ana kontrolcüsü.
    /// Main Menu sahnesinde bulunur.
    ///
    /// Hierarchy:
    ///  ShapeShop (bu script)
    ///   ├── Header
    ///   │    ├── BackButton
    ///   │    └── CoinText (TMP)
    ///   ├── ScrollView
    ///   │    └── Content   ← kartlar buraya spawn olur
    ///   └── ShapeShopToast
    /// </summary>
    public sealed class ShapeShopController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ShapeLibrarySO _shapeLibrary;

        [Header("UI")]
        [SerializeField] private TMP_Text        _coinText;
        [SerializeField] private RectTransform   _content;         // ScrollView Content
        [SerializeField] private ShapeShopCardView _cardPrefab;

        [Header("Navigation")]
        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        private readonly List<ShapeShopCardView> _cards = new();

        // ── Unity ────────────────────────────────────────────────────────────

        private void Start()
        {
            // Registry'leri yükle
            if (_shapeLibrary != null)
                ShapeUpgradeRegistry.Instance.Load(_shapeLibrary.Shapes);

            // Coin değişince UI güncelle
            if (CoinWallet.Instance != null)
                CoinWallet.Instance.OnBalanceChanged += UpdateCoinText;

            BuildCards();
            UpdateCoinText(CoinWallet.Instance?.Balance ?? 0);
        }

        private void OnDestroy()
        {
            if (CoinWallet.Instance != null)
                CoinWallet.Instance.OnBalanceChanged -= UpdateCoinText;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Tüm kartları yeniden render et — coin veya upgrade değişince.</summary>
        public void RefreshAll()
        {
            UpdateCoinText(CoinWallet.Instance?.Balance ?? 0);
            foreach (var card in _cards)
                card.Refresh();
        }

        // ── Navigation ───────────────────────────────────────────────────────

        public void OnBackButton()
        {
            PanelManager.Instance?.CloseCurrentPanel();
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void BuildCards()
        {
            if (_shapeLibrary == null || _content == null || _cardPrefab == null) return;

            foreach (Transform child in _content)
                Destroy(child.gameObject);
            _cards.Clear();

            // Önce unlocked, sonra locked — HTML gibi sıralı
            var sorted = new List<ShapeSO>(_shapeLibrary.Shapes);
            sorted.Sort((a, b) =>
            {
                bool aUnlocked = a.IsUnlocked;
                bool bUnlocked = b.IsUnlocked;
                if (aUnlocked && !bUnlocked) return -1;
                if (!aUnlocked && bUnlocked)  return 1;
                return 0;
            });

            foreach (var shape in sorted)
            {
                if (shape == null) continue;

                var card = Instantiate(_cardPrefab, _content);
                card.transform.localScale = Vector3.one;
                card.Bind(shape, this);
                _cards.Add(card);
            }
        }

        private void UpdateCoinText(int balance)
        {
            if (_coinText != null)
                _coinText.text = balance.ToString("N0");
        }
    }
}