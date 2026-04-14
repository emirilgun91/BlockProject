using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Tek shape shop kartı.
    ///
    /// Hierarchy:
    ///  ShapeCard (bu script)
    ///   ├── ShapePreview       ← BlockCellView grid'i burada spawn olur
    ///   ├── ShapeName (TMP)
    ///   ├── LockedOverlay      ← kilitliyse aktif
    ///   ├── StatsPanel
    ///   │    ├── ScoreBlock
    ///   │    │    ├── ScoreValue (TMP)
    ///   │    │    ├── ScoreCost  (TMP)
    ///   │    │    └── ScoreUpgradeBtn (Button)
    ///   │    └── WeightBlock
    ///   │         ├── WeightValue  (TMP)
    ///   │         ├── WeightBarFill (Image)
    ///   │         ├── WeightDownBtn (Button)
    ///   │         ├── WeightUpBtn   (Button)
    ///   │         └── WeightCost    (TMP)
    ///   └── UnlockSection      ← kilitliyse aktif
    ///        ├── UnlockCostText (TMP)
    ///        └── UnlockBtn (Button)
    /// </summary>
    public sealed class ShapeShopCardView : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private Image    _statusDot;          // sol üst köşedeki nokta

        [Header("Dot Colors")]
        [SerializeField] private Color _dotUnlockedColor = new Color(0.08f, 0.85f, 0.45f); // yeşil
        [SerializeField] private Color _dotLockedColor   = new Color(0.35f, 0.40f, 0.50f); // gri

        [Header("Preview")]
        [SerializeField] private RectTransform _previewContainer;
        [SerializeField] private GameObject    _cellPrefab;
        [SerializeField] private float         _cellSize = 20f;
        [SerializeField] private float         _cellGap  = 3f;

        [Header("Lock")]
        [SerializeField] private GameObject _lockedOverlay;

        [Header("Stats Panel")]
        [SerializeField] private GameObject _statsPanel;        // tüm stats bloğu

        [Header("Stats — Score")]
        [SerializeField] private TMP_Text _scoreValueText;
        [SerializeField] private TMP_Text _scoreCostText;
        [SerializeField] private Button   _scoreUpgradeBtn;

        [Header("Stats — Weight")]
        [SerializeField] private TMP_Text _weightValueText;
        [SerializeField] private Image    _weightBarFill;
        [SerializeField] private Button   _weightDownBtn;
        [SerializeField] private Button   _weightUpBtn;
        [SerializeField] private TMP_Text _weightCostText;

        [Header("Unlock")]
        [SerializeField] private GameObject _unlockSection;
        [SerializeField] private TMP_Text   _unlockCostText;
        [SerializeField] private Button     _unlockBtn;

        // Runtime
        private ShapeSO _shape;
        private ShapeShopController _controller;

        // ── Public API ───────────────────────────────────────────────────────

        public void Bind(ShapeSO shape, ShapeShopController controller)
        {
            _shape      = shape;
            _controller = controller;

            // Buton listener'ları
            _scoreUpgradeBtn?.onClick.AddListener(OnScoreUpgrade);
            _weightUpBtn?.onClick.AddListener(OnWeightUp);
            _weightDownBtn?.onClick.AddListener(OnWeightDown);
            _unlockBtn?.onClick.AddListener(OnUnlock);

            BuildPreview();
            Refresh();
        }

        /// <summary>Coin veya upgrade değişince dışarıdan çağrılır.</summary>
        public void Refresh()
        {
            if (_shape == null) return;

            bool isUnlocked = _shape.IsUnlocked;
            var  registry   = ShapeUpgradeRegistry.Instance;
            int  coins      = CoinWallet.Instance?.Balance ?? 0;

            // İsim
            if (_nameText != null)
                _nameText.text = _shape.Id.Replace("_", " ").ToUpper();

            // ── Status dot ───────────────────────────────────────────────────
            if (_statusDot != null)
                _statusDot.color = isUnlocked ? _dotUnlockedColor : _dotLockedColor;

            // ── Kilit overlay ────────────────────────────────────────────────
            if (_lockedOverlay != null)
                _lockedOverlay.SetActive(!isUnlocked);

            // ── Stats — sadece unlocked'sa göster ────────────────────────────
            if (_statsPanel != null)
                _statsPanel.SetActive(isUnlocked);

            // ── Unlock section ───────────────────────────────────────────────
            if (_unlockSection != null)
                _unlockSection.SetActive(!isUnlocked);

            if (!isUnlocked)
            {
                if (_unlockCostText != null)
                    _unlockCostText.text = $"{_shape.UnlockCost}";
                if (_unlockBtn != null)
                    _unlockBtn.interactable = CoinWallet.Instance?.CanAfford(_shape.UnlockCost) ?? false;
                return;
            }

            // ── Score ────────────────────────────────────────────────────────
            float tileVal     = registry.GetTileValue(_shape.Id, _shape.BaseTileValue);
            float tileValNext = tileVal + registry.ValuePerUpgradeLevel;
            int   sCost       = registry.GetScoreUpgradeCost(_shape.Id);
            bool  canScore    = registry.CanUpgradeScore(_shape.Id) && coins >= sCost;

            if (_scoreValueText != null)
            {
                if (registry.CanUpgradeScore(_shape.Id))
                    // "5 -> <color=#22DD66><size=115%>10</size></color>"
                    _scoreValueText.text = $"{tileVal:0} <color=#22DD66><size=115%>-> {tileValNext:0}</size></color>";
                else
                    // Max seviye — sadece mevcut değer
                    _scoreValueText.text = $"{tileVal:0}";
            }

            if (_scoreCostText  != null) _scoreCostText.text           = $"{sCost}";
            if (_scoreUpgradeBtn!= null) _scoreUpgradeBtn.interactable = canScore;

            // ── Weight ───────────────────────────────────────────────────────
            int  effectiveW = registry.GetEffectiveWeight(_shape.Id, _shape.BaseWeight);
            int  wUpCost    = registry.GetWeightIncreaseCost(_shape.Id);
            int  wDownCost  = registry.GetWeightDecreaseCost(_shape.Id);
            bool canUp      = registry.CanIncreaseWeight(_shape.Id) && coins >= wUpCost;
            bool canDown    = registry.CanDecreaseWeight(_shape.Id, _shape.BaseWeight) && coins >= wDownCost;

            if (_weightValueText != null)
                _weightValueText.text = $"{effectiveW}";

            if (_weightBarFill != null)
            {
                int max = _shape.BaseWeight + registry.MaxWeightLevel * registry.WeightStep;
                _weightBarFill.fillAmount = Mathf.Clamp01((float)effectiveW / max);
            }

            if (_weightUpBtn   != null) _weightUpBtn.interactable  = canUp;
            if (_weightDownBtn != null) _weightDownBtn.interactable = canDown;

            if (_weightCostText != null)
                _weightCostText.text = $"{wUpCost} / {wDownCost}";
        }

        // ── Button Handlers ──────────────────────────────────────────────────

        private void OnScoreUpgrade()
        {
            var reg  = ShapeUpgradeRegistry.Instance;
            int cost = reg.GetScoreUpgradeCost(_shape.Id);

            if (!CoinWallet.Instance.Spend(cost))
            {
                ShapeShopToast.Instance?.Show("Yetersiz coin!", ToastType.Error);
                return;
            }

            reg.UpgradeScore(_shape.Id);
            _controller.RefreshAll();
            ShapeShopToast.Instance?.Show($"{_nameText.text} baz puanı yükseltildi!", ToastType.Success);
        }

        private void OnWeightUp()
        {
            var reg  = ShapeUpgradeRegistry.Instance;
            int cost = reg.GetWeightIncreaseCost(_shape.Id);

            if (!CoinWallet.Instance.Spend(cost))
            {
                ShapeShopToast.Instance?.Show("Yetersiz coin!", ToastType.Error);
                return;
            }

            reg.IncreaseWeight(_shape.Id);
            _controller.RefreshAll();
            ShapeShopToast.Instance?.Show($"{_nameText.text} çıkma ihtimali arttırıldı!", ToastType.Success);
        }

        private void OnWeightDown()
        {
            var reg  = ShapeUpgradeRegistry.Instance;
            int cost = reg.GetWeightDecreaseCost(_shape.Id);

            if (!CoinWallet.Instance.Spend(cost))
            {
                ShapeShopToast.Instance?.Show("Yetersiz coin!", ToastType.Error);
                return;
            }

            reg.DecreaseWeight(_shape.Id, _shape.BaseWeight);
            _controller.RefreshAll();
            ShapeShopToast.Instance?.Show($"{_nameText.text} çıkma ihtimali azaltıldı.", ToastType.Default);
        }

        private void OnUnlock()
        {
            if (!CoinWallet.Instance.Spend(_shape.UnlockCost))
            {
                ShapeShopToast.Instance?.Show("Yetersiz coin!", ToastType.Error);
                return;
            }

            UnlockRegistry.Instance.UnlockShape(_shape.Id);
            _controller.RefreshAll();
            ShapeShopToast.Instance?.Show($"{_nameText.text} açıldı!", ToastType.Success);
        }

        // ── Preview ──────────────────────────────────────────────────────────

        private void BuildPreview()
        {
            if (_previewContainer == null || _shape.Cells == null) return;

            // Mevcut hücreleri temizle
            foreach (Transform child in _previewContainer)
                Destroy(child.gameObject);

            if (_shape.Cells.Count == 0) return;

            // Bounding box
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in _shape.Cells)
            {
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.y > maxY) maxY = c.y;
            }

            int w = maxX - minX + 1;
            int h = maxY - minY + 1;

            float totalW = w * _cellSize + (w - 1) * _cellGap;
            float totalH = h * _cellSize + (h - 1) * _cellGap;

            foreach (var c in _shape.Cells)
            {
                var go   = Instantiate(_cellPrefab, _previewContainer);
                var rect = go.GetComponent<RectTransform>();

                rect.sizeDelta = new Vector2(_cellSize, _cellSize);
                rect.anchoredPosition = new Vector2(
                    (c.x - minX) * (_cellSize + _cellGap) - totalW * 0.5f + _cellSize * 0.5f,
                    (c.y - minY) * (_cellSize + _cellGap) - totalH * 0.5f + _cellSize * 0.5f
                );

                var img = go.GetComponent<Image>();
                if (img != null) img.color = _shape.BlockColor;
            }
        }
    }
}