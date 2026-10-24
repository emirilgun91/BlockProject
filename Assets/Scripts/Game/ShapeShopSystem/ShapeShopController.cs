using System.Collections.Generic;
using System.Linq;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using RogueBlockBlast.Core.Localization;
using RogueBlockBlast.Core.Settings;
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

        [Header("Purchase Confirm")]
        [Tooltip("Satın alma onay kutusunun görselleri. Boş kalanlar düz renkli kutuya düşer.")]
        [SerializeField] private Sprite        _confirmWindow;
        [SerializeField] private Sprite        _confirmButton;
        [SerializeField] private Sprite        _confirmButtonPrimary;
        [SerializeField] private Sprite        _confirmCheckBox;
        [SerializeField] private Sprite        _confirmCheckFill;
        [SerializeField] private Sprite        _confirmCoin;
        [SerializeField] private TMP_FontAsset _confirmFont;
        [SerializeField] private TMP_FontAsset _confirmBoldFont;

        private readonly List<ShapeShopCardView> _cards = new();
        private ShopConfirmDialog _confirmDialog;

        // ── Unity ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Her panel açılışında registry'leri yükle
            EnsureRegistries();

            UpdateCoinText(CoinWallet.Instance?.Balance ?? 0);
            if (CoinWallet.Instance != null)
                CoinWallet.Instance.OnBalanceChanged += UpdateCoinText;

            BuildCards();

            // Gamepad: B = geri butonu (ana menüde paneli, oyun sahnesinde overlay'i kapatır).
            UINav.Push(gameObject, back: OnBackButton);
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

        /// <summary>
        /// Kartların harcama yapan her butonu buradan geçer. Para yetmiyorsa uyarı,
        /// <see cref="GameSettings.ConfirmPurchase"/> kapalıysa doğrudan satın alma,
        /// açıksa önce onay kutusu ("Bir daha gösterme" ayarı kapatır).
        /// </summary>
        public void RequestPurchase(string headline, string detail, int cost, System.Action buy)
        {
            if (CoinWallet.Instance == null || !CoinWallet.Instance.CanAfford(cost))
            {
                ShapeShopToast.Instance?.Show(Loc.Get("Toast.NotEnoughCoins"), ToastType.Error);
                return;
            }

            if (!GameSettings.ConfirmPurchase) { buy(); return; }

            if (_confirmDialog == null)
                _confirmDialog = ShopConfirmDialog.Create(transform, new ShopConfirmDialog.Skin
                {
                    Window        = _confirmWindow,
                    Button        = _confirmButton,
                    ButtonPrimary = _confirmButtonPrimary,
                    Track         = _confirmCheckBox,
                    Fill          = _confirmCheckFill,
                    Coin          = _confirmCoin,
                    Font          = _confirmFont,
                    BoldFont      = _confirmBoldFont,
                });
            _confirmDialog.Show(headline, detail, cost, buy);
        }

        // ── Navigation ───────────────────────────────────────────────────────

        public void OnBackButton()
        {
            // Onay kutusu açıkken geri butonu önce onu kapatır
            if (_confirmDialog != null && _confirmDialog.IsOpen)
            {
                _confirmDialog.Cancel();
                return;
            }
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
            // Ana menüde RunController yok; kilitli şekilleri mağaza bildirir.
            UnlockRegistry.Instance?.RegisterLockedShapes(_shapeLibrary.Shapes
                .Where(s => s != null && s.LockedByDefault)
                .Select(s => s.Id));
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