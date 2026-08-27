using System;
using System.Collections.Generic;
using RogueBlockBlast.Content;
using RogueBlockBlast.UI;
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
    /// Prototip kapsamı: reroll butonu ve "yeni kart" rozeti henüz dünya
    /// uzayında çizilmiyor.
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
            Debug.Log("[PhysicalCards] Dünya-uzayı kart sunucusu bağlandı.");
        }

        private void OnDestroy()
        {
            if (CardSelectionUI.Instance != null &&
                ReferenceEquals(CardSelectionUI.Instance.ExternalPresenter, this))
            {
                CardSelectionUI.Instance.ExternalPresenter = null;
            }
        }

        private void Update()
        {
            UpdateHover();
            UpdateDim();
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
            ClearImmediate();
            _onPicked = onPicked;

            if (cards == null || cards.Count == 0 || _camera == null) return;

            ResolveCardMetrics();

            EnsureDim();
            if (_hudGroup != null) _hudBaseAlpha = _hudGroup.alpha;
            _dimTarget = 1f;

            if (rerollsRemaining > 0)
            {
                Debug.Log(
                    $"[PhysicalCards] {rerollsRemaining} reroll hakkı var ama dünya-uzayı " +
                    "reroll butonu henüz yok (prototip kapsamı dışı).");
            }

            Vector3 dispenser = FanOrigin - CamUp * _dispenserDrop;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;

                var physical = Spawn(card, i, cards.Count, dispenser);
                if (physical == null) continue;

                _cards.Add(physical);
                _cardData.Add(card);
            }
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

            // Perde kartlarla birlikte çekilir — kartlar uçarken oyun geri gelir.
            _dimTarget = 0f;
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

            _canvasScale = targetHeight / _cardSizePx.y;
            _slotSpacing = _cardSizePx.x * _canvasScale * _fanSpacingFactor;
        }

        // ── Kart üretimi ─────────────────────────────────────────────────────

        private PhysicalCard Spawn(CardSO card, int index, int total, Vector3 dispenser)
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
                delay: index * _dealStagger,
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

        private void UpdateHover()
        {
            if (_cards.Count == 0 || EventSystem.current == null) return;
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
