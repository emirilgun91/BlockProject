using System.Collections.Generic;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Upgrades paneli ana kontrolcüsü.
    ///
    /// Hierarchy:
    ///  UpgradesPanel (CanvasGroup)
    ///   └── Window
    ///        ├── TitleBar
    ///        │    └── CoinAmount (TMP)
    ///        ├── ItemGrid (GridLayoutGroup)
    ///        │    └── [UpgradeSlot prefabları]
    ///        ├── Separator
    ///        └── DetailPanel
    ///             ├── DetailIcon (Image)
    ///             ├── DetailName (TMP)
    ///             ├── DetailDesc (TMP)
    ///             ├── BuyPriceText (TMP)
    ///             ├── SellPriceText (TMP)
    ///             ├── BuyButton (Button)
    ///             └── SellButton (Button)
    /// </summary>
    public sealed class UpgradesController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private UpgradeLibrarySO _library;
        [SerializeField] private TMP_Text _coinText;

        [Header("Grid")]
        [SerializeField] private RectTransform   _gridContent;
        [SerializeField] private UpgradeSlotView _slotPrefab;

        [Header("Detail Panel")]
        [SerializeField] private Image    _detailIcon;
        [SerializeField] private TMP_Text _detailName;
        [SerializeField] private TMP_Text _detailDesc;
        [SerializeField] private TMP_Text _buyPriceText;
        [SerializeField] private TMP_Text _sellPriceText;
        [SerializeField] private Button   _buyButton;
        [SerializeField] private Button   _sellButton;

        [Header("Detail — empty state")]
        [SerializeField] private Sprite   _emptyIconSprite;
        [SerializeField] private string   _emptyNameText = "Bir öğe seçin";
        [SerializeField] private string   _emptyDescText = "Detayları görmek için yukarıdan bir güçlendirici seçin.";

        // Runtime
        private readonly List<UpgradeSlotView> _slots = new();
        private UpgradeSlotView _selectedSlot;

        // ── Unity ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Her panel açılışında registry'yi yükle
            UpgradeRegistry.Instance?.Init(_library);

            BuildSlots();
            UpdateCoinText(CoinWallet.Instance?.Balance ?? 0);
            ShowEmptyDetail();

            if (CoinWallet.Instance != null)
                CoinWallet.Instance.OnBalanceChanged += UpdateCoinText;
        }

        private void OnDisable()
        {
            if (CoinWallet.Instance != null)
                CoinWallet.Instance.OnBalanceChanged -= UpdateCoinText;
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void BuildSlots()
        {
            if (_library == null || _gridContent == null || _slotPrefab == null) return;

            // Temizle
            foreach (Transform child in _gridContent)
                Destroy(child.gameObject);
            _slots.Clear();
            _selectedSlot = null;

            foreach (var upgrade in _library.Upgrades)
            {
                if (upgrade == null) continue;

                var slot = Instantiate(_slotPrefab, _gridContent);
                slot.transform.localScale = Vector3.one;
                slot.OnSelected = OnSlotSelected;
                slot.Bind(upgrade);
                _slots.Add(slot);
            }
        }

        // ── Selection ────────────────────────────────────────────────────────

        private void OnSlotSelected(UpgradeSlotView slot)
        {
            _selectedSlot = slot;

            // Tüm slot'ları güncelle
            foreach (var s in _slots)
                s.Refresh(isSelected: s == slot);

            ShowDetail(slot.Upgrade);
        }

        // ── Detail ───────────────────────────────────────────────────────────

        private void ShowDetail(UpgradeSO upgrade)
        {
            if (upgrade == null) { ShowEmptyDetail(); return; }

            var  registry = UpgradeRegistry.Instance;
            int  level    = registry?.GetLevel(upgrade.Id) ?? 0;
            bool maxed    = level >= upgrade.MaxLevel;

            // Icon
            if (_detailIcon != null)
                _detailIcon.sprite = upgrade.Icon;

            // Lokalizasyon: bu iki satırı kendi sisteminle değiştir
            if (_detailName != null) _detailName.text = upgrade.NameKey;
            if (_detailDesc != null) _detailDesc.text = upgrade.DescriptionKey;

            // Buy price — bir sonraki seviyenin maliyeti
            int buyCost  = maxed ? 0 : upgrade.GetCostForLevel(level + 1);
            int sellCost = level > 0 ? upgrade.GetCostForLevel(level) / 2 : 0;

            if (_buyPriceText  != null)
                _buyPriceText.text  = maxed ? "MAX" : buyCost.ToString("N0");

            if (_sellPriceText != null)
                _sellPriceText.text = level > 0 ? sellCost.ToString("N0") : "—";

            // Butonlar
            bool canBuy  = registry?.CanUpgrade(upgrade) ?? false;
            bool canSell = registry?.CanSell(upgrade) ?? false;

            if (_buyButton  != null) _buyButton.interactable  = canBuy;
            if (_sellButton != null) _sellButton.interactable = canSell;
        }

        private void ShowEmptyDetail()
        {
            if (_detailIcon  != null) _detailIcon.sprite = _emptyIconSprite;
            if (_detailName  != null) _detailName.text   = _emptyNameText;
            if (_detailDesc  != null) _detailDesc.text   = _emptyDescText;
            if (_buyPriceText  != null) _buyPriceText.text  = "—";
            if (_sellPriceText != null) _sellPriceText.text = "—";
            if (_buyButton   != null) _buyButton.interactable  = false;
            if (_sellButton  != null) _sellButton.interactable = false;
        }

        // ── Buy / Sell ────────────────────────────────────────────────────────

        public void OnBuyClicked()
        {
            if (_selectedSlot == null) return;
            var upgrade = _selectedSlot.Upgrade;

            bool success = UpgradeRegistry.Instance?.Upgrade(upgrade) ?? false;

            if (!success)
            {
                ShapeShopToast.Instance?.Show("Yetersiz coin!", ToastType.Error);
                return;
            }

            // Lokalizasyon: upgrade.NameKey yerine lokalize isim kullan
            ShapeShopToast.Instance?.Show($"{upgrade.NameKey} yükseltildi!", ToastType.Success);
            RefreshAll();
        }

        public void OnSellClicked()
        {
            if (_selectedSlot == null) return;
            var upgrade = _selectedSlot.Upgrade;

            bool success = UpgradeRegistry.Instance?.Sell(upgrade) ?? false;

            if (!success)
            {
                ShapeShopToast.Instance?.Show("İade edilemedi!", ToastType.Error);
                return;
            }

            ShapeShopToast.Instance?.Show($"{upgrade.NameKey} iade edildi.", ToastType.Default);
            RefreshAll();
        }

        // ── Refresh ──────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            UpdateCoinText(CoinWallet.Instance?.Balance ?? 0);

            foreach (var s in _slots)
                s.Refresh(isSelected: s == _selectedSlot);

            if (_selectedSlot != null)
                ShowDetail(_selectedSlot.Upgrade);
        }

        private void UpdateCoinText(int balance)
        {
            if (_coinText != null)
                _coinText.text = balance.ToString("N0");
        }
    }
}