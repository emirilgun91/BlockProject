using RogueBlockBlast.Content;
using RogueBlockBlast.Core.Localization;
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

        [Header("Affordability Colors")]
        [Tooltip("Fiyat yazısının normal rengi.")]
        [SerializeField] private Color _affordableColor   = new Color(1f, 0.84f, 0.35f);
        [Tooltip("Para yetmediğinde fiyat rengi — buton yine tıklanabilir, uyarı çıkar.")]
        [SerializeField] private Color _unaffordableColor = new Color(0.95f, 0.35f, 0.32f);
        [SerializeField] private Color _dotLockedColor   = new Color(0.35f, 0.40f, 0.50f); // gri

        [Header("Preview")]
        [SerializeField] private RectTransform _previewContainer;
        [SerializeField] private GameObject    _cellPrefab;
        [SerializeField] private float         _cellSize = 20f;
        [SerializeField] private float         _cellGap  = 3f;

        [Header("Preview — Background")]
        [Tooltip("Şeklin arkasındaki taban görsel. Rengi şeklin renginin " +
                 "koyu tonuna ayarlanır; sprite'ın kendi alfası korunur.")]
        [SerializeField] private Image _shapeBackground;

        [Tooltip("0 = şekil rengiyle aynı, 1 = siyah. Taban ne kadar koyulaşsın.")]
        [Range(0f, 1f)]
        [SerializeField] private float _backgroundDarken = 0.55f;

        [Header("Preview — Shadow")]
        [Tooltip("Şeklin birebir aynısı, siyah ve büyütülmüş hâlde arkaya çizilir.")]
        [SerializeField] private bool _showShapeShadow = true;

        [Tooltip("Gölgenin ölçeği. Merkezden büyür, yani her yönde eşit taşar.")]
        [SerializeField] private float _shadowScale = 1.1f;

        [Tooltip("Gölgenin kayması (px). Sıfır bırakılırsa kontur gibi durur; " +
                 "aşağı kaydırmak gölge hissi verir.")]
        [SerializeField] private Vector2 _shadowOffset = new Vector2(0f, -4f);

        [SerializeField] private Color _shadowColor = new Color(0f, 0f, 0f, 0.55f);

        [Header("Lock")]
        [SerializeField] private GameObject _lockedOverlay;

        [Header("Stats Panel")]
        [SerializeField] private GameObject _statsPanel;        // tüm stats bloğu

        [Header("Stats — Score")]
        [SerializeField] private TMP_Text _scoreValueText;
        [SerializeField] private TMP_Text _scoreCostText;
        [SerializeField] private Button   _scoreUpgradeBtn;

        [Tooltip("Puan yükseltme efekti. Boş bırakılırsa çalışma anında kurulur; " +
                 "efekt istemiyorsanız bileşeni karttan silin.")]
        [SerializeField] private FX.ShapeScoreUpgradeFX _scoreUpgradeFX;

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
            EnsureScoreUpgradeFX();

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
                _nameText.text = Loc.ToUpper(ContentLocalization.Name(_shape));

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
                    _unlockBtn.interactable = true;   // para yetmezse tıklayınca uyarı çıkar
                if (_unlockCostText != null)
                    _unlockCostText.color = (CoinWallet.Instance?.CanAfford(_shape.UnlockCost) ?? false)
                        ? _affordableColor : _unaffordableColor;
                return;
            }

            // ── Score ────────────────────────────────────────────────────────
            float tileVal     = registry.GetTileValue(_shape.Id, _shape.BaseTileValue);
            // Sonraki seviyenin kazancı eğriden gelir — sabit artış varsayma
            float tileValNext = tileVal + registry.GetNextScoreGain(_shape.Id);
            int   sCost       = registry.GetScoreUpgradeCost(_shape.Id);
            // Buton para yetmese de açık kalır — tıklayınca uyarı çıkar (kapalı buton sebebini anlatmıyor)
            bool  canScore    = registry.CanUpgradeScore(_shape.Id);
            bool  affordScore = coins >= sCost;

            if (_scoreValueText != null)
            {
                if (registry.CanUpgradeScore(_shape.Id))
                    // "5 -> <color=#22DD66><size=115%>10</size></color>"
                    _scoreValueText.text = $"{tileVal:0} <color=#22DD66><size=115%>-> {tileValNext:0}</size></color>";
                else
                    // Max seviye — sadece mevcut değer
                    _scoreValueText.text = $"{tileVal:0}";
            }

            if (_scoreCostText  != null)
            {
                // Max seviyede fiyat anlamsız — UpgradesController ile aynı
                // anahtarı kullanır (Shop.MaxLevel), iki dükkân aynı dili konuşsun.
                // Renk de "yetersiz para" kırmızısına düşmemeli: max bir uyarı değil.
                _scoreCostText.text  = canScore ? $"{sCost}" : Loc.Get("Shop.MaxLevel");
                _scoreCostText.color = !canScore || affordScore
                    ? _affordableColor
                    : _unaffordableColor;
            }
            if (_scoreUpgradeBtn!= null) _scoreUpgradeBtn.interactable = canScore;

            // ── Weight ───────────────────────────────────────────────────────
            int  effectiveW = registry.GetEffectiveWeight(_shape.Id, _shape.BaseWeight);
            int  wUpCost    = registry.GetWeightIncreaseCost(_shape.Id);
            int  wDownCost  = registry.GetWeightDecreaseCost(_shape.Id);
            bool canUp      = registry.CanIncreaseWeight(_shape.Id);
            bool affordUp   = coins >= wUpCost;
            bool canDown    = registry.CanDecreaseWeight(_shape.Id, _shape.BaseWeight);
            bool affordDown = coins >= wDownCost;

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
            {
                _weightCostText.text  = $"{wUpCost} / {wDownCost}";
                _weightCostText.color = (affordUp || affordDown) ? _affordableColor : _unaffordableColor;
            }
        }

        // ── Button Handlers ──────────────────────────────────────────────────

        /// <summary>
        /// Puan yükseltme efektini prefaba dokunmadan hazırlar. Inspector'dan
        /// atanmışsa ona saygı duyar.
        /// </summary>
        private void EnsureScoreUpgradeFX()
        {
            if (_scoreValueText == null) return;

            if (_scoreUpgradeFX == null)
                _scoreUpgradeFX = GetComponent<FX.ShapeScoreUpgradeFX>()
                               ?? gameObject.AddComponent<FX.ShapeScoreUpgradeFX>();

            _scoreUpgradeFX.Bind(_scoreValueText, transform as RectTransform);
        }

        private void OnScoreUpgrade()
        {
            var reg  = ShapeUpgradeRegistry.Instance;
            int cost = reg.GetScoreUpgradeCost(_shape.Id);

            if (!CoinWallet.Instance.Spend(cost))
            {
                ShapeShopToast.Instance?.Show(Loc.Get("Toast.NotEnoughCoins"), ToastType.Error);
                return;
            }

            // Kazanç yükseltmeden ÖNCE okunur — sonrasında bu değer bir sonraki
            // seviyenin kazancını gösterir ve efekt yanlış sayı yazar.
            float gained = reg.GetNextScoreGain(_shape.Id);

            reg.UpgradeScore(_shape.Id);

            // Efekt Refresh'ten SONRA oynatılır: Refresh puan yazısını yeniden
            // kurar ve arada oynatılan tween'i ezerdi.
            _controller.RefreshAll();
            _scoreUpgradeFX?.Play(gained);

            ShapeShopToast.Instance?.Show(Loc.Get("Toast.ScoreUpgraded", _nameText.text), ToastType.Success);
        }

        private void OnWeightUp()
        {
            var reg  = ShapeUpgradeRegistry.Instance;
            int cost = reg.GetWeightIncreaseCost(_shape.Id);

            if (!CoinWallet.Instance.Spend(cost))
            {
                ShapeShopToast.Instance?.Show(Loc.Get("Toast.NotEnoughCoins"), ToastType.Error);
                return;
            }

            reg.IncreaseWeight(_shape.Id);
            _controller.RefreshAll();
            ShapeShopToast.Instance?.Show(Loc.Get("Toast.WeightIncreased", _nameText.text), ToastType.Success);
        }

        private void OnWeightDown()
        {
            var reg  = ShapeUpgradeRegistry.Instance;
            int cost = reg.GetWeightDecreaseCost(_shape.Id);

            if (!CoinWallet.Instance.Spend(cost))
            {
                ShapeShopToast.Instance?.Show(Loc.Get("Toast.NotEnoughCoins"), ToastType.Error);
                return;
            }

            reg.DecreaseWeight(_shape.Id, _shape.BaseWeight);
            _controller.RefreshAll();
            ShapeShopToast.Instance?.Show(Loc.Get("Toast.WeightDecreased", _nameText.text), ToastType.Default);
        }

        private void OnUnlock()
        {
            if (!CoinWallet.Instance.Spend(_shape.UnlockCost))
            {
                ShapeShopToast.Instance?.Show(Loc.Get("Toast.NotEnoughCoins"), ToastType.Error);
                return;
            }

            UnlockRegistry.Instance.UnlockShape(_shape.Id);
            _controller.RefreshAll();
            ShapeShopToast.Instance?.Show(Loc.Get("Toast.ShapeUnlocked", _nameText.text), ToastType.Success);
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

            // Taban görsel şeklin renginin koyu tonunu alır. Sprite'ın kendi
            // alfası korunur — sanatçının verdiği saydamlık bozulmasın.
            if (_shapeBackground != null)
            {
                var baseColor = Color.Lerp(_shape.BlockColor, Color.black, _backgroundDarken);
                _shapeBackground.color = new Color(
                    baseColor.r, baseColor.g, baseColor.b, _shapeBackground.color.a);
            }

            // Gölge önce kurulur ki kardeş sırasında arkada kalsın; UI'da
            // önce eklenen önce çizilir.
            if (_showShapeShadow)
            {
                var shadow = NewPreviewLayer("ShapeShadow");
                shadow.localScale       = Vector3.one * _shadowScale;
                shadow.anchoredPosition = _shadowOffset;

                SpawnCells(shadow, minX, minY, totalW, totalH, _shadowColor);
            }

            var main = NewPreviewLayer("ShapeCells");
            SpawnCells(main, minX, minY, totalW, totalH, _shape.BlockColor);
        }

        /// <summary>
        /// Önizleme katmanı — container'ı birebir dolduran boş bir rect.
        /// Hücre konumları parent'ın merkezine göre hesaplandığı için katmanın
        /// container'la aynı ölçüde ve merkez pivotlu olması şart; aksi hâlde
        /// mevcut yerleşim matematiği kayar.
        /// </summary>
        private RectTransform NewPreviewLayer(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_previewContainer, worldPositionStays: false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot     = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return rect;
        }

        /// <summary>Şeklin hücrelerini verilen katmana, verilen renkte kurar.</summary>
        private void SpawnCells(RectTransform layer, int minX, int minY,
                                float totalW, float totalH, Color color)
        {
            foreach (var c in _shape.Cells)
            {
                var go   = Instantiate(_cellPrefab, layer);
                var rect = go.GetComponent<RectTransform>();

                rect.sizeDelta = new Vector2(_cellSize, _cellSize);
                rect.anchoredPosition = new Vector2(
                    (c.x - minX) * (_cellSize + _cellGap) - totalW * 0.5f + _cellSize * 0.5f,
                    (c.y - minY) * (_cellSize + _cellGap) - totalH * 0.5f + _cellSize * 0.5f
                );

                var img = go.GetComponent<Image>();
                if (img != null) img.color = color;
            }
        }
    }
}