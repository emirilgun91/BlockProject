using System.Collections.Generic;
using RogueBlockBlast.Core.Localization;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using RogueBlockBlast.UI.FX;
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
        [Header("Affordability Colors")]
        [Tooltip("Fiyat yazısının normal rengi.")]
        [SerializeField] private Color _affordableColor   = new Color(1f, 0.84f, 0.35f);
        [Tooltip("Para yetmediğinde fiyat yazısının rengi — buton yine tıklanabilir kalır.")]
        [SerializeField] private Color _unaffordableColor = new Color(0.95f, 0.35f, 0.32f);


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
        private CoinCounterFX   _coinFX;

        // ── Unity ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Her panel açılışında registry'yi yükle
            UpgradeRegistry.Instance?.Init(_library);

            // FX bileşenlerini bağla — prefab'da kurulum gerektirmez.
            _coinFX = CoinCounterFX.Attach(_coinText);
            PremiumButtonFX.Attach(_buyButton);

            BuildSlots();
            _coinFX?.SetImmediate(CoinWallet.Instance?.Balance ?? 0);
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
            // Anahtar değil, çevirisi gösterilmeli
            if (_detailName != null) _detailName.text = ContentLocalization.Name(upgrade);
            // Anahtar değil, çevirisi gösterilmeli
            if (_detailDesc != null) _detailDesc.text = ContentLocalization.Description(upgrade);

            // Buy price — bir sonraki seviyenin maliyeti
            int buyCost  = maxed ? 0 : upgrade.GetCostForLevel(level + 1);
            int sellCost = level > 0 ? upgrade.GetCostForLevel(level) / 2 : 0;

            if (_buyPriceText  != null)
                _buyPriceText.text  = maxed ? Loc.Get("Common.Max") : buyCost.ToString("N0");

            if (_sellPriceText != null)
                _sellPriceText.text = level > 0 ? sellCost.ToString("N0") : "—";

            // Butonlar
            // Buton, PARA YETMESE DE açık kalır: tıklayınca "yetersiz coin" uyarısı çıkar.
            // Kapalı buton oyuncuya neden tıklayamadığını anlatmıyordu.
            bool canBuyEver = registry?.CanUpgradeIgnoringCoins(upgrade) ?? false;
            bool canAfford  = registry?.CanAffordUpgrade(upgrade) ?? false;
            bool canSell    = registry?.CanSell(upgrade) ?? false;

            if (_buyButton  != null) _buyButton.interactable  = canBuyEver;
            if (_sellButton != null) _sellButton.interactable = canSell;

            // Fiyat yazısı parayı yetmiyorsa kırmızıya döner — tıklamadan önce belli olsun
            if (_buyPriceText != null && canBuyEver)
                _buyPriceText.color = canAfford ? _affordableColor : _unaffordableColor;
        }

        private void ShowEmptyDetail()
        {
            if (_detailIcon  != null) _detailIcon.sprite = _emptyIconSprite;
            if (_detailName  != null) _detailName.text   = Loc.GetOr("Upgrades.Empty.Title", _emptyNameText);
            if (_detailDesc  != null) _detailDesc.text   = Loc.GetOr("Upgrades.Empty.Desc", _emptyDescText);
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
                ShapeShopToast.Instance?.Show(Loc.Get("Toast.NotEnoughCoins"), ToastType.Error);
                return;
            }

            // Lokalizasyon: upgrade.NameKey yerine lokalize isim kullan
            ShapeShopToast.Instance?.Show(Loc.Get("Toast.UpgradeBought", ContentLocalization.Name(upgrade)), ToastType.Success);
            RefreshAll();

            // Satın alınan kartta kısa altın flaş + radyal glow (dot'lar Refresh'te dolar).
            _selectedSlot.PlayPurchaseFeedback();
        }

        public void OnSellClicked()
        {
            if (_selectedSlot == null) return;
            var upgrade = _selectedSlot.Upgrade;

            bool success = UpgradeRegistry.Instance?.Sell(upgrade) ?? false;

            if (!success)
            {
                ShapeShopToast.Instance?.Show(Loc.Get("Toast.SellFailed"), ToastType.Error);
                return;
            }

            ShapeShopToast.Instance?.Show(Loc.Get("Toast.UpgradeSold", ContentLocalization.Name(upgrade)), ToastType.Default);
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
            // Sayı anında değil, yumuşak count-up ile ilerler; artışta altın shine oynar.
            if (_coinFX != null) { _coinFX.SetValue(balance); return; }

            if (_coinText != null)
                _coinText.text = balance.ToString("N0");
        }
    }
}