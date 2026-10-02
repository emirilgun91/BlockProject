using System.Collections.Generic;
using System.Linq;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Shape Shop ana kontrolcüsü.
    /// Main Menu sahnesinde ve (ShapeShopOverlay ile) oyun sahnelerinde bulunur — prefab: ShapeShopPanel.
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

        private void OnEnable()
        {
            // Her panel açılışında registry'leri yükle
            EnsureRegistries();

            UpdateCoinText(CoinWallet.Instance?.Balance ?? 0);
            if (CoinWallet.Instance != null)
                CoinWallet.Instance.OnBalanceChanged += UpdateCoinText;

            BuildCards();
        }

        private void OnDisable()
        {
            if (CoinWallet.Instance != null)
                CoinWallet.Instance.OnBalanceChanged -= UpdateCoinText;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Oyun sahnesi gibi PanelManager'ın olmadığı yerlerde "geri" basılınca tetiklenir.
        /// Dinleyen varsa panelin kapanmasını o yönetir; yoksa PanelManager kapatır.
        /// </summary>
        public event System.Action BackRequested;

        /// <summary>
        /// Oyuncunun parası en az bir şeye yetiyor mu: kilitli bir şeklin açılması,
        /// skor yükseltmesi ya da ağırlık artırma. Ağırlık AZALTMA sayılmaz — bir
        /// yükseltme değil, ince ayar. Kartlardaki fiyat/yetme kurallarıyla aynı.
        /// </summary>
        public bool HasAffordableUpgrade()
        {
            if (_shapeLibrary == null || CoinWallet.Instance == null) return false;
            EnsureRegistries();

            int coins = CoinWallet.Instance.Balance;
            var reg   = ShapeUpgradeRegistry.Instance;
            foreach (var shape in _shapeLibrary.Shapes)
            {
                if (shape == null) continue;
                if (!shape.IsUnlocked)
                {
                    if (coins >= shape.UnlockCost) return true;
                    continue;
                }
                if (reg.CanUpgradeScore(shape.Id) && coins >= reg.GetScoreUpgradeCost(shape.Id)) return true;
                if (reg.CanIncreaseWeight(shape.Id) && coins >= reg.GetWeightIncreaseCost(shape.Id)) return true;
            }
            return false;
        }

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
            if (BackRequested != null) BackRequested.Invoke();
            else PanelManager.Instance?.CloseCurrentPanel();
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void EnsureRegistries()
        {
            if (_shapeLibrary == null) return;
            ShapeUpgradeRegistry.Instance.Load(_shapeLibrary.Shapes);
            var ids = _shapeLibrary.Shapes
                .Where(s => s != null)
                .Select(s => s.Id);
            UnlockRegistry.Instance?.Init(ids, System.Array.Empty<string>());
        }

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