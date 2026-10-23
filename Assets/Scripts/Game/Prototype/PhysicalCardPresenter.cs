using System;
using System.Collections.Generic;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core.Localization;
using RogueBlockBlast.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RogueBlockBlast.Game.Prototype
{
    /// <summary>
    /// Kart seçimini dünya-uzayında fiziksel kartlar olarak sunar.
    ///
    /// <b>Kart görselleri sıfırdan çizilmez.</b> Mevcut <c>CardView_0</c>
    /// prefabı bir World Space Canvas içine konur; ikon, isim, açıklama, rarity
    /// çerçevesi ve tıklama (Button + GraphicRaycaster) olduğu gibi gelir.
    ///
    /// Havuz mantığına karışmaz: hangi kartların çıkacağına
    /// <see cref="CardSelectionUI"/> karar verir. Kilit/unique/ağırlık kuralları
    /// tek yerde kalır.
    ///
    /// <b>Yerleşim kameraya göredir, dünya koordinatına göre değil.</b>
    /// Eğimli kamerada sabit bir dünya konumu vermek kartları hem ekranda
    /// yukarı kaçırıyor hem de kameraya yaklaştırdığı için devleştiriyordu.
    /// Yelpaze artık kameranın önünde <see cref="_cameraDistance"/> birimde
    /// kuruluyor ve kameranın sağ/yukarı eksenlerinde diziliyor; kadraj ne
    /// olursa olsun boyut ve konum sabit kalıyor.
    ///
    /// Reroll butonu ekran uzayında, yelpazenin altında çizilir (Card Reroll
    /// yükseltmesi). Prototip kapsamı: "yeni kart" rozeti henüz yok.
    /// </summary>
    public sealed class PhysicalCardPresenter : MonoBehaviour, ICardPresenter
    {
        [Header("Kaynaklar")]
        [Tooltip("Mevcut CardView_0 prefabı — yeniden çizilmez, aynen kullanılır.")]
        [SerializeField] private CardView _cardViewPrefab;

        [Tooltip("Boşsa Camera.main kullanılır.")]
        [SerializeField] private Camera _camera;

        [Header("Yerleşim (kameraya göre)")]
        [Tooltip("Yelpazenin kameradan uzaklığı. Kart boyutu buna ve Canvas Scale'e bağlı.")]
        [SerializeField] private float _cameraDistance = 8f;

        [Tooltip("Ekran merkezine göre dikey kayma. Eksi = aşağı.")]
        [SerializeField] private float _verticalOffset = -0.35f;

        [Tooltip("Kart yüksekliğinin ekran yüksekliğine oranı. Ölçek buradan " +
                 "hesaplanır — mesafe veya FOV değişse de kart aynı büyüklükte kalır.")]
        [Range(0.15f, 0.8f)]
        [SerializeField] private float _cardScreenHeight = 0.45f;

        [Tooltip("Kartlar arası aralık, kart genişliğinin katı olarak. 1.0 = bitişik.")]
        [SerializeField] private float _fanSpacingFactor = 1.1f;
        [Tooltip("Yelpazenin ekran genişliğinin en fazla ne kadarını kaplayacağı. " +
                 "Portrait'te kart boyunu bu belirler.")]
        [SerializeField] private float _maxFanScreenWidth = 0.94f;

        [Tooltip("Yelpazenin kavisi — uçtaki kartlar bu kadar alçalır.")]
        [SerializeField] private float _fanArc = 0.12f;

        [Tooltip("Uçtaki kartların eğimi (derece).")]
        [SerializeField] private float _fanTilt = 6f;


        [Header("Dağıtım")]
        [Tooltip("Kartların çıktığı nokta — yelpazenin bu kadar altı.")]
        [SerializeField] private float _dispenserDrop = 5f;

        [SerializeField] private float _dealDuration = 0.55f;
        [SerializeField] private float _dealStagger  = 0.11f;
        [SerializeField] private float _dealArc      = 0.9f;
        [SerializeField] private float _dealSpin     = 200f;

        [Header("Hover")]
        [SerializeField] private float _hoverLift     = 0.35f;
        [SerializeField] private float _hoverScale    = 1.12f;
        [SerializeField] private float _swayAmplitude = 0.02f;

        [Header("Çıkış")]
        [Tooltip("Seçilen kartın uçacağı yön — kamera uzayında sol/yukarı kayma.")]
        [SerializeField] private Vector2 _pickedDrift = new Vector2(-4f, 1.5f);

        [Header("Karartma")]
        [Tooltip("Seçim açıkken tahta ve arka planı karart. Kartlar perdenin önünde kalır.")]
        [SerializeField] private bool _dimWorld = true;

        [SerializeField] private Color _dimColor = new Color(0.02f, 0.03f, 0.06f, 1f);

        [Range(0f, 1f)]
        [Tooltip("Perdenin en koyu hâli. 1 = arkası tamamen kapanır.")]
        [SerializeField] private float _dimAlpha = 0.78f;

        [SerializeField] private float _dimFadeSpeed = 6f;

        [Tooltip("Opsiyonel: seçim açıkken kısılacak HUD. Perde HUD'ı kapatamaz " +
                 "çünkü Screen Space Overlay her zaman en üstte çizilir — bu yüzden " +
                 "HUD ayrıca soldurulur.")]
        [SerializeField] private CanvasGroup _hudGroup;

        [Range(0f, 1f)]
        [SerializeField] private float _hudDimmedAlpha = 0.22f;

        [Header("Sıralama")]
        [Tooltip("Kartların çizileceği sorting layer. Tahta 'Board' layer'ında; " +
                 "kartlar ondan sonra gelen bir layer'da olmalı yoksa tahtanın arkasında kalır.")]
        [SerializeField] private string _sortingLayer = "UI";

        [SerializeField] private int _sortingOrder = 100;

        // ── Runtime ──────────────────────────────────────────────────────────
        private readonly List<PhysicalCard> _cards    = new();
        private readonly List<CardSO>       _cardData = new();
        private Action<CardSO>              _onPicked;
        private PhysicalCard                _hovered;

        // Prefabın kendi ölçüsünden türetilir — tahmin edilmez.
        private Vector2 _cardSizePx;
        private float   _canvasScale;
        private float   _slotSpacing;

        // ── Karartma ─────────────────────────────────────────────────────────
        private Image _dimImage;
        private float _dimCurrent;
        private float _dimTarget;
        private float _hudBaseAlpha = 1f;

        // ── Kamera bazı ──────────────────────────────────────────────────────
        private Vector3 CamRight   => _camera.transform.right;
        private Vector3 CamUp      => _camera.transform.up;
        private Vector3 CamForward => _camera.transform.forward;

        /// <summary>Yelpazenin merkezi — kameranın tam önünde.</summary>
        private Vector3 FanOrigin =>
            _camera.transform.position
            + CamForward * _cameraDistance
            + CamUp      * _verticalOffset;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Start()
        {
            if (_camera == null) _camera = Camera.main;

            if (_cardViewPrefab == null || _camera == null)
            {
                Debug.LogError(
                    "[PhysicalCards] CardView prefabı veya kamera yok — sunucu devre dışı, " +
                    "kart seçimi eski UGUI paneline düşecek.");
                return;
            }

            if (CardSelectionUI.Instance == null)
            {
                Debug.LogError("[PhysicalCards] CardSelectionUI bulunamadı — sunucu bağlanamadı.");
                return;
            }

            CardSelectionUI.Instance.ExternalPresenter = this;
            InputMode.Changed += RefreshRerollLabel;
            Debug.Log("[PhysicalCards] Dünya-uzayı kart sunucusu bağlandı.");
        }

        private void OnDestroy()
        {
            InputMode.Changed -= RefreshRerollLabel;
            if (CardSelectionUI.Instance != null &&
                ReferenceEquals(CardSelectionUI.Instance.ExternalPresenter, this))
            {
                CardSelectionUI.Instance.ExternalPresenter = null;
            }

            if (_rerollCanvas != null) Destroy(_rerollCanvas.gameObject);
        }

        private void Update()
        {
            UpdateGamepad();
            UpdateHover();
            UpdateDim();
            UpdateRerollButton();
        }

        /// <summary>
        /// Perde her zaman geri açılabilmeli. Bileşen kapanır ya da yok edilirse
        /// HUD kısık kalmasın diye burada da geri verilir.
        /// </summary>
        private void OnDisable() => RestoreHud();

        // ── Karartma ─────────────────────────────────────────────────────────

        private void UpdateDim()
        {
            _dimCurrent = Mathf.MoveTowards(
                _dimCurrent, _dimTarget, Time.unscaledDeltaTime * _dimFadeSpeed);

            if (_dimImage != null)
            {
                _dimImage.color = new Color(
                    _dimColor.r, _dimColor.g, _dimColor.b, _dimCurrent * _dimAlpha);

                // Tamamen saydamken çizim yapma.
                _dimImage.enabled = _dimCurrent > 0.002f;
            }

            if (_hudGroup != null)
                _hudGroup.alpha = Mathf.Lerp(_hudBaseAlpha, _hudDimmedAlpha, _dimCurrent);
        }

        /// <summary>
        /// Perdeyi kameranın çocuğu olarak kurar — kamera sallandığında kenar
        /// açılmasın diye. Sıralama kartların bir altında: tahtanın (Board
        /// layer) önünde, kartların arkasında.
        /// </summary>
        private void EnsureDim()
        {
            if (!_dimWorld || _dimImage != null || _camera == null) return;

            var go = new GameObject("Selection Dim");
            go.transform.SetParent(_camera.transform, worldPositionStays: false);

            // Kartların hemen arkası.
            float distance = _cameraDistance * 1.12f;
            go.transform.localPosition = new Vector3(0f, 0f, distance);
            go.transform.localRotation = Quaternion.identity;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;

            if (!string.IsNullOrEmpty(_sortingLayer))
                canvas.sortingLayerName = _sortingLayer;
            canvas.sortingOrder = _sortingOrder - 1;

            float height = 2f * distance *
                           Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float width  = height * _camera.aspect;

            var rect = go.GetComponent<RectTransform>();
            // Kenar payı: kamera sway'inde perde kadrajdan çıkmasın.
            rect.sizeDelta  = new Vector2(width, height) * 1.4f;
            rect.localScale = Vector3.one;

            // Sprite verilmeyen bir Image düz renk dolgu çizer — doku gerekmez.
            _dimImage = go.AddComponent<Image>();
            _dimImage.color         = new Color(_dimColor.r, _dimColor.g, _dimColor.b, 0f);
            _dimImage.raycastTarget = false;
            _dimImage.enabled       = false;
        }

        private void RestoreHud()
        {
            if (_hudGroup != null) _hudGroup.alpha = _hudBaseAlpha;
        }

        // ── ICardPresenter ───────────────────────────────────────────────────

        public void Present(
            IReadOnlyList<CardSO> cards,
            bool                  showNewBadge,
            Action<CardSO>        onPicked,
            int                   rerollsRemaining,
            Action                onReroll)
        {
            Vector3 dispenser = _camera != null ? FanOrigin - CamUp * _dispenserDrop : Vector3.zero;

            // Reroll: eski el dağıtıcıya geri döner, yenisi arkasından gelir.
            // ClearImmediate ile silmek kartları bir karede yok ediyordu — oyuncu
            // elin değiştiğini değil, ekranın titrediğini görüyordu.
            bool isReroll = _cards.Count > 0;
            if (isReroll)
            {
                foreach (var c in _cards) c?.Leave(dispenser, shrink: false, duration: 0.3f);
                _cards.Clear();
                _cardData.Clear();
                _hovered = null;
            }
            else ClearImmediate();

            _onPicked = onPicked;

            if (cards == null || cards.Count == 0 || _camera == null) return;

            ResolveCardMetrics();

            EnsureDim();
            if (!isReroll && _hudGroup != null) _hudBaseAlpha = _hudGroup.alpha;
            _dimTarget = 1f;

            // Hak bitince buton kaybolmaz, sönük kalır: oyuncu neden basamadığını görsün.
            if (rerollsRemaining > 0 || (isReroll && _rerollShown))
                ShowRerollButton(rerollsRemaining, onReroll, punch: isReroll);
            else
                HideRerollButton();

            float dealDelay = isReroll ? 0.22f : 0f;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;

                var physical = Spawn(card, i, cards.Count, dispenser, dealDelay);
                if (physical == null) continue;

                _cards.Add(physical);
                _cardData.Add(card);
            }

            // Gamepad: ilk açılışta ortadaki kart odakta; reroll'da oyuncunun baktığı yuva korunur.
            if (!isReroll) _padIndex = _cards.Count / 2;
            _padIndex = Mathf.Clamp(_padIndex, 0, Mathf.Max(0, _cards.Count - 1));
            UINav.Push(gameObject, manual: true);
        }

        public void Dismiss(CardSO picked)
        {
            Vector3 pickedTarget = FanOrigin
                                   + CamRight * _pickedDrift.x
                                   + CamUp    * _pickedDrift.y;

            Vector3 dispenser = FanOrigin - CamUp * _dispenserDrop;

            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                if (c == null) continue;

                bool isPicked = picked != null && i < _cardData.Count && _cardData[i] == picked;

                if (isPicked) c.Leave(pickedTarget, shrink: true,  duration: 0.5f);
                else          c.Leave(dispenser,    shrink: false, duration: 0.35f);
            }

            _cards.Clear();
            _cardData.Clear();
            _hovered  = null;
            _onPicked = null;
            HideRerollButton();
            UINav.Pop(gameObject);

            // Perde kartlarla birlikte çekilir — kartlar uçarken oyun geri gelir.
            _dimTarget = 0f;
        }

        // ── Reroll butonu ────────────────────────────────────────────────────
        // Ekran uzayında, yelpazenin altında. Sunucunun kendi çocuğu değil:
        // ClearImmediate sunucunun altındaki her şeyi siliyor.

        private Canvas      _rerollCanvas;
        private CanvasGroup _rerollGroup;
        private Button      _rerollButton;
        private TMP_Text    _rerollLabel;
        private RectTransform _rerollRect;
        private Action      _onReroll;
        private bool        _rerollShown;
        private float       _rerollAlpha, _rerollPunch;

        private void EnsureRerollButton()
        {
            if (_rerollCanvas != null) return;

            var go = new GameObject("Card Reroll Button");
            _rerollCanvas = go.AddComponent<Canvas>();
            _rerollCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _rerollCanvas.sortingOrder = 500;   // HUD ve karartmanın üstünde
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _rerollGroup = go.AddComponent<CanvasGroup>();

            var sprites = RerollSprites.Get();

            var btnGo = new GameObject("Reroll", typeof(RectTransform));
            _rerollRect = (RectTransform)btnGo.transform;
            _rerollRect.SetParent(go.transform, false);
            // Konum her karede yelpazeye göre verilir (PlaceRerollButton): sabit bir
            // ekran oranı portrait'te üstteki HUD panellerine biniyordu.
            _rerollRect.anchorMin = _rerollRect.anchorMax = new Vector2(0.5f, 0.5f);
            _rerollRect.pivot     = new Vector2(0.5f, 0.5f);
            _rerollRect.sizeDelta = new Vector2(380f, 76f);

            var fill = btnGo.AddComponent<Image>();
            fill.sprite = sprites.fill; fill.type = Image.Type.Sliced;

            var rimGo = new GameObject("Rim", typeof(RectTransform));
            var rimRect = (RectTransform)rimGo.transform;
            rimRect.SetParent(_rerollRect, false);
            rimRect.anchorMin = Vector2.zero; rimRect.anchorMax = Vector2.one;
            rimRect.offsetMin = rimRect.offsetMax = Vector2.zero;
            var rim = rimGo.AddComponent<Image>();
            rim.sprite = sprites.rim; rim.type = Image.Type.Sliced; rim.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(_rerollRect, false);
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24f, 0f); labelRect.offsetMax = new Vector2(-24f, 0f);
            _rerollLabel = labelGo.AddComponent<TextMeshProUGUI>();
            _rerollLabel.fontSize = 30f;
            _rerollLabel.fontStyle = FontStyles.Bold;
            _rerollLabel.alignment = TextAlignmentOptions.Center;
            _rerollLabel.color = UiTheme.Text;
            _rerollLabel.enableWordWrapping = false;
            _rerollLabel.overflowMode = TextOverflowModes.Ellipsis;
            _rerollLabel.raycastTarget = false;

            _rerollButton = btnGo.AddComponent<Button>();
            _rerollButton.transition    = Selectable.Transition.None;
            _rerollButton.targetGraphic = fill;
            _rerollButton.onClick.AddListener(OnRerollPressed);

            btnGo.AddComponent<KitButton>().Configure(
                fill, rim, _rerollLabel,
                UiTheme.AccentDeep, UiTheme.WithAlpha(UiTheme.Accent, 0.32f),
                UiTheme.WithAlpha(UiTheme.Accent, 0.75f), UiTheme.Accent);

            go.SetActive(false);
        }

        private void ShowRerollButton(int remaining, Action onReroll, bool punch)
        {
            EnsureRerollButton();
            _onReroll    = onReroll;
            _rerollShown = true;
            _rerollCanvas.gameObject.SetActive(true);
            _rerollButton.interactable = remaining > 0;
            _rerollRemaining = remaining;
            RefreshRerollLabel();
            if (punch) _rerollPunch = 1f;
        }

        private int _rerollRemaining;

        /// <summary>Gamepad kullanılırken etiketin başında tuşu (Y) yazar.</summary>
        private void RefreshRerollLabel()
        {
            if (_rerollLabel == null) return;
            string text = $"↻  {Loc.Get("Upgrade.upgrade_card_reroll.Name")}  ×{_rerollRemaining}";
            if (InputMode.UsingGamepad) text = $"{InputMode.GlyphRich(PadButton.North)}   {text}";
            _rerollLabel.text = text;

            // Genişlik metne göre: çevirilerin uzunluğu dilden dile çok değişiyor
            float w = _rerollLabel.GetPreferredValues(_rerollLabel.text).x;
            _rerollRect.sizeDelta = new Vector2(Mathf.Clamp(w + 72f, 260f, 900f), _rerollRect.sizeDelta.y);
        }

        private void HideRerollButton()
        {
            _rerollShown = false;
            _onReroll    = null;
            if (_rerollButton != null) _rerollButton.interactable = false;
        }

        private void OnRerollPressed()
        {
            // Kartlar havadayken basılırsa yeni el eskisinin üstüne düşüyordu.
            foreach (var c in _cards)
                if (c != null && !c.IsInteractive) return;
            _onReroll?.Invoke();
        }

        private void UpdateRerollButton()
        {
            if (_rerollCanvas == null) return;
            _rerollAlpha = Mathf.MoveTowards(_rerollAlpha, _rerollShown ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            _rerollGroup.alpha = _rerollAlpha;
            _rerollGroup.blocksRaycasts = _rerollShown;
            if (!_rerollShown && _rerollAlpha <= 0f) _rerollCanvas.gameObject.SetActive(false);
            PlaceRerollButton();

            // Basınca kısa bir sıçrama: hakkın harcandığı hissedilsin
            _rerollPunch = Mathf.MoveTowards(_rerollPunch, 0f, Time.unscaledDeltaTime * 4f);
            // (ölçeği KitButton yönetiyor, o yüzden sallanma rotasyonla)
            float wobble = Mathf.Sin(_rerollPunch * Mathf.PI * 3f) * 7f * _rerollPunch;
            _rerollRect.localRotation = Quaternion.Euler(0f, 0f, wobble);
        }

        /// <summary>
        /// Butonu orta kartın üst kenarının hemen üstüne koyar (hover kalkışı dahil).
        /// Kartlar kameraya göre dizildiği için bu nokta her en-boy oranında
        /// yelpaze ile üstteki HUD arasında kalır.
        /// </summary>
        private void PlaceRerollButton()
        {
            if (_rerollRect == null || _camera == null || _canvasScale <= 0f) return;

            float cardHeight = _cardSizePx.y * _canvasScale;
            Vector3 top = FanOrigin + CamUp * (cardHeight * 0.5f + _hoverLift);
            Vector3 sp  = _camera.WorldToScreenPoint(top);

            var canvasRect = (RectTransform)_rerollCanvas.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 local))
                return;

            float h = _rerollRect.sizeDelta.y;
            float y = local.y + h * 0.5f + 24f;
            float maxY = canvasRect.rect.height * 0.5f - h * 0.5f - 16f;   // ekranın üstünden taşmasın
            _rerollRect.anchoredPosition = new Vector2(0f, Mathf.Min(y, maxY));
        }

        /// <summary>Yuvarlak buton dolgusu ve kenarı — kod içinde üretilir, asset gerekmez.</summary>
        private static class RerollSprites
        {
            private static Sprite _fill, _rim;

            public static (Sprite fill, Sprite rim) Get()
            {
                if (_fill == null) { _fill = Rounded(0f); _rim = Rounded(3f); }
                return (_fill, _rim);
            }

            private static Sprite Rounded(float thickness)
            {
                const int size = 96; const float radius = 40f; const int border = 46;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                float half = size * 0.5f - 1f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = x + 0.5f - size * 0.5f, cy = y + 0.5f - size * 0.5f;
                    float qx = Mathf.Abs(cx) - (half - radius), qy = Mathf.Abs(cy) - (half - radius);
                    float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                              + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = thickness > 0f
                        ? Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + thickness + 0.5f)
                        : Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
                tex.SetPixels32(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                     SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            }
        }

        // ── Ölçü ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Kart ölçüsünü prefabın kendisinden okur ve dünya ölçeğini istenen
        /// ekran oranından hesaplar.
        ///
        /// Ölçü <b>asla tahmin edilmez.</b> Prefabın çocukları nokta anchor
        /// kullanıyor (CardBody, DescriptionText kendi sabit boyutlarında) —
        /// köke yanlış bir sizeDelta yazmak onları küçültmez, taşırır. İlk
        /// sürümde 420x560 varsayılmıştı ve metin kartın dışına akıyordu.
        /// </summary>
        private void ResolveCardMetrics()
        {
            var prefabRect = _cardViewPrefab.GetComponent<RectTransform>();
            _cardSizePx = prefabRect != null ? prefabRect.sizeDelta : new Vector2(685f, 1016f);

            if (_cardSizePx.x <= 1f || _cardSizePx.y <= 1f)
            {
                _cardSizePx = new Vector2(685f, 1016f);
                Debug.LogWarning("[PhysicalCards] Kart prefabının ölçüsü okunamadı — varsayılana düşüldü.");
            }

            // Odak düzleminde görünen dikey yükseklik.
            float visibleHeight = 2f * _cameraDistance *
                                  Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            float targetHeight = visibleHeight * _cardScreenHeight;

            // Dar (portrait) ekranda yelpaze genişliği de sınırlanır: 3 kartın
            // toplam genişliği = kart × (2·aralık + 1). Yalnızca yüksekliğe göre
            // ölçeklemek portrait'te uç kartları ekran dışına itiyordu.
            // Landscape'te yükseklik zaten daha kısıtlayıcı — davranış değişmez.
            float visibleWidth = visibleHeight * _camera.aspect;
            float fanWidthInCards = 2f * _fanSpacingFactor + 1f;
            float maxHeightByWidth = visibleWidth * _maxFanScreenWidth / fanWidthInCards
                                     * (_cardSizePx.y / _cardSizePx.x);
            targetHeight = Mathf.Min(targetHeight, maxHeightByWidth);

            _canvasScale = targetHeight / _cardSizePx.y;
            _slotSpacing = _cardSizePx.x * _canvasScale * _fanSpacingFactor;
        }

        // ── Kart üretimi ─────────────────────────────────────────────────────

        private PhysicalCard Spawn(CardSO card, int index, int total, Vector3 dispenser, float baseDelay = 0f)
        {
            var root = new GameObject($"Card_{index}_{card.Id}");
            root.transform.SetParent(transform, worldPositionStays: false);

            // ── Canvas ───────────────────────────────────────────────
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(root.transform, worldPositionStays: false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;

            // Tahta "Board" sorting layer'ında; kartlar ondan sonraki bir
            // layer'da olmazsa tahtanın arkasında kalır.
            if (!string.IsNullOrEmpty(_sortingLayer))
                canvas.sortingLayerName = _sortingLayer;
            canvas.sortingOrder = _sortingOrder;

            canvasGo.AddComponent<GraphicRaycaster>();

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta     = _cardSizePx;
            canvasRect.localScale    = Vector3.one * _canvasScale;
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;

            var group = canvasGo.AddComponent<CanvasGroup>();

            // ── Gölge: kartın arkasındaki kardeş ─────────────────────
            // Aynı canvas içinde olduğu için sıralama otomatik; ayrı bir
            // SpriteRenderer olsaydı sorting layer'ı elle yönetmek gerekirdi.
            var shadowGo = new GameObject("Shadow", typeof(RectTransform));
            shadowGo.transform.SetParent(canvasGo.transform, worldPositionStays: false);

            var shadowRect = shadowGo.GetComponent<RectTransform>();
            shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.pivot     = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = _cardSizePx;

            var shadowImage = shadowGo.AddComponent<Image>();
            shadowImage.color        = new Color(0f, 0f, 0f, 0.55f);
            shadowImage.raycastTarget = false;

            // ── Kart yüzü: mevcut prefab, değiştirilmeden ────────────
            var view     = Instantiate(_cardViewPrefab, canvasGo.transform);
            var viewRect = view.GetComponent<RectTransform>();
            if (viewRect != null)
            {
                // Yalnızca ortalanır — sizeDelta'ya DOKUNULMAZ. Prefabın
                // çocukları nokta anchor kullandığı için köke başka bir boyut
                // yazmak onları küçültmez, kartın dışına taşırır.
                viewRect.anchorMin        = viewRect.anchorMax = new Vector2(0.5f, 0.5f);
                viewRect.pivot            = new Vector2(0.5f, 0.5f);
                viewRect.anchoredPosition = Vector2.zero;
                viewRect.localScale       = Vector3.one;
            }

            var physical = root.AddComponent<PhysicalCard>();

            physical.Configure(
                group, shadowRect, shadowImage,
                _hoverLift, _hoverScale, _swayAmplitude);

            physical.SetBasis(CamUp, -CamForward);
            physical.SetSlot(SlotPosition(index, total), SlotRotation(index, total), 1f);

            // Seçim callback'i mevcut CardView üzerinden — Button ve
            // IPointerClickHandler prefabda zaten bağlı.
            view.Bind(card, OnCardClicked);

            physical.Deal(
                dispenser,
                delay: baseDelay + index * _dealStagger,
                duration: _dealDuration,
                arcHeight: _dealArc,
                spinDegrees: _dealSpin);

            return physical;
        }

        private void OnCardClicked(CardSO card)
        {
            // Uçuş bitmeden tıklama kabul edilmez.
            for (int i = 0; i < _cards.Count; i++)
            {
                if (i < _cardData.Count && _cardData[i] == card)
                {
                    if (_cards[i] != null && !_cards[i].IsInteractive) return;
                    break;
                }
            }

            _onPicked?.Invoke(card);
        }

        // ── Yelpaze ──────────────────────────────────────────────────────────

        private Vector3 SlotPosition(int index, int total)
        {
            float offset = index - (total - 1) * 0.5f;

            return FanOrigin
                   + CamRight * (offset * _slotSpacing)
                   - CamUp    * (offset * offset * _fanArc);
        }

        /// <summary>
        /// Kart kameraya bakar, üstüne yelpaze eğimi biner. Dünya rotasyonu
        /// kullanılsaydı kartlar eğimli kamerada yamuk görünürdü.
        /// </summary>
        private Quaternion SlotRotation(int index, int total)
        {
            var facing = Quaternion.LookRotation(CamForward, CamUp);
            if (total <= 1) return facing;

            float offset = index - (total - 1) * 0.5f;
            return facing * Quaternion.Euler(0f, 0f, -offset * _fanTilt);
        }

        // ── Hover ────────────────────────────────────────────────────────────

        // ── Gamepad ──────────────────────────────────────────────────────────
        // Kartlar EventSystem'de seçilmez (UINav "manual" kapsam): sol/sağ odaktaki
        // kartı değiştirir, odak hover kalkışıyla gösterilir, A seçer, Y yeniler.

        private int _padIndex;
        private readonly InputMode.RepeatState _padRepeat = new InputMode.RepeatState();

        private void UpdateGamepad()
        {
            // Pause menüsü kart seçiminin üstünde açıksa A onun butonlarına aittir.
            if (_cards.Count == 0 || !InputMode.UsingGamepad || !UINav.IsTop(gameObject)) return;

            int step = _padRepeat.Step().x;
            if (step != 0) _padIndex = (_padIndex + step + _cards.Count) % _cards.Count;
            _padIndex = Mathf.Clamp(_padIndex, 0, _cards.Count - 1);

            if (InputMode.Pressed(PadButton.South) && _padIndex < _cardData.Count)
                OnCardClicked(_cardData[_padIndex]);
            else if (InputMode.Pressed(PadButton.North) && _rerollShown &&
                     _rerollButton != null && _rerollButton.interactable)
                OnRerollPressed();
        }

        private void UpdateHover()
        {
            if (_cards.Count == 0 || EventSystem.current == null) return;

            if (InputMode.UsingGamepad)
            {
                var focused = _cards[Mathf.Clamp(_padIndex, 0, _cards.Count - 1)];
                if (ReferenceEquals(focused, _hovered)) return;
                _hovered?.SetHovered(false);
                _hovered = focused;
                _hovered?.SetHovered(true);
                return;
            }

            if (Mouse.current == null) return;

            PhysicalCard under = null;

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            };

            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);

            foreach (var hit in hits)
            {
                var card = hit.gameObject.GetComponentInParent<PhysicalCard>();
                if (card != null) { under = card; break; }
            }

            if (ReferenceEquals(under, _hovered)) return;

            _hovered?.SetHovered(false);
            _hovered = under;
            _hovered?.SetHovered(true);
        }

        // ── Temizlik ─────────────────────────────────────────────────────────

        private void ClearImmediate()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _cards.Clear();
            _cardData.Clear();
            _hovered = null;
        }
    }
}
