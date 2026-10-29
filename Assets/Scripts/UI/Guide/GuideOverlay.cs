using System;
using System.Collections.Generic;
using DG.Tweening;
using RogueBlockBlast.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Meta rehberlerinin (<see cref="ShopUpgradeGuide"/>, <see cref="UpgradesNudge"/>) ekranı:
    /// karartma, hedefin üstünde bir ışık deliği, deliğin çevresinde nabız atan çerçeve ve
    /// yanında bir bilgi kartı (başlık, metin, isteğe bağlı coin hediyesi, butonlar).
    ///
    /// Arayüzünü kodda kurar; görselleri satın alma onay kutusuyla aynıdır
    /// (<see cref="ShopConfirmDialog.Skin"/>), yani pencere ve butonlar mağazayla aynı dili konuşur.
    /// Kendi Canvas'ı vardır (override sorting): paneller hangi alt canvas'ta olursa olsun
    /// hepsinin üstünde çizilir, ölçeği ise bağlandığı canvas'ın CanvasScaler'ından gelir.
    ///
    /// Karartma dört şeritten oluşur ve tıklamaları yutar; delik boş kalır, yani yalnızca
    /// deliğin altındaki kontrol tıklanabilir. Hedef yoksa ekranın tamamı kararır.
    /// Game Over sırasında <c>timeScale = 0</c>: her şey unscaled zamanla çalışır.
    /// </summary>
    public sealed class GuideOverlay : MonoBehaviour
    {
        private const int   SortingOrder = 30000;   // UINav'ın odak çerçevesi (32000) yine üstte
        private const float CardWidth    = 660f;
        private const float HolePadding  = 12f;
        private const float CardGap      = 34f;
        private const float EdgeMargin   = 24f;
        private const float InputGuard   = 0.35f;   // kartı açan tıklamanın ikinci yarısı butona düşmesin

        // Lineer renk uzayı + HDR: 0.82 koyu mağazanın üstünde neredeyse görünmüyordu
        // (ShopConfirmDialog'un 0.94'üyle aynı sebep)
        private static readonly Color DimColor = new Color(0.01f, 0.02f, 0.05f, 0.93f);

        private ShopConfirmDialog.Skin _skin;

        private RectTransform _root;
        private CanvasGroup   _group;
        private RectTransform _top, _bottom, _left, _right, _holeCover;
        private RectTransform _frame, _ripple;
        private Image         _frameLine, _frameGlow, _rippleLine;

        private RectTransform _card;
        private CanvasGroup   _cardGroup;
        private TMP_Text      _title, _body, _coinAmount;
        private RectTransform _coinRow, _coinIcon, _buttonRow;
        private Button        _primary, _secondary;
        private TMP_Text      _primaryLabel, _secondaryLabel;
        private Action        _onPrimary, _onSecondary;
        private float         _cardShownAt;
        private bool          _cardVisible;

        private readonly List<RectTransform> _targets = new List<RectTransform>();
        private Rect  _hole, _holeTarget;
        private bool  _hasHole;
        private bool  _closing;
        private Tween _coinTween;

        private static Sprite _sprLine, _sprGlow, _sprFill;

        public bool IsClosing => _closing;

        /// <summary>Doluyken deliğin içi de tıklanmaz (satın alma sonrası kutlama anı).</summary>
        public bool BlockHole
        {
            set { if (_holeCover != null) _holeCover.gameObject.SetActive(value); }
        }

        // ── Kurulum ──────────────────────────────────────────────────────────

        /// <summary><paramref name="canvasChild"/>'ın kök canvas'ına, her şeyin üstüne kurar.</summary>
        public static GuideOverlay Create(Transform canvasChild, ShopConfirmDialog.Skin skin, string name)
        {
            // includeInactive: hedef panel henüz kapalı olabilir (mağaza rehberden sonra açılıyor)
            var canvas = canvasChild != null ? canvasChild.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null) return null;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.layer = canvas.gameObject.layer;
            var overlay = go.AddComponent<GuideOverlay>();
            overlay._skin = skin;
            overlay.Build(canvas.rootCanvas);
            return overlay;
        }

        private void Build(Canvas rootCanvas)
        {
            EnsureSprites();

            _root = (RectTransform)transform;
            Stretch(_root);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            // Sorting layer kök canvas'ınki: yalnızca order'ı yükseltmek, kök daha üst bir
            // layer'daysa overlay'i onun altında bırakırdı.
            canvas.sortingLayerID  = rootCanvas.sortingLayerID;
            canvas.sortingOrder    = SortingOrder;
            gameObject.AddComponent<GraphicRaycaster>();

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.DOFade(1f, 0.25f).SetUpdate(true);

            _top    = Strip("DimTop");
            _bottom = Strip("DimBottom");
            _left   = Strip("DimLeft");
            _right  = Strip("DimRight");

            _holeCover = NewRect("HoleCover", _root);
            Center(_holeCover);
            _holeCover.gameObject.AddComponent<Image>().color = Color.clear;
            _holeCover.gameObject.SetActive(false);

            // Delik çerçevesi: yumuşak dış parıltı + ince çizgi + dışa yayılan halka
            _frame = NewRect("Frame", _root);
            Center(_frame);
            var glowRt = NewRect("Glow", _frame);
            glowRt.anchorMin = Vector2.zero; glowRt.anchorMax = Vector2.one;
            glowRt.offsetMin = new Vector2(-28f, -28f); glowRt.offsetMax = new Vector2(28f, 28f);
            _frameGlow = SlicedImage(glowRt, _sprGlow);
            var lineRt = NewRect("Line", _frame);
            Stretch(lineRt);
            _frameLine = SlicedImage(lineRt, _sprLine);
            _ripple = NewRect("Ripple", _frame);
            Stretch(_ripple);
            _rippleLine = SlicedImage(_ripple, _sprLine);
            _frame.gameObject.SetActive(false);

            BuildCard();
            ApplyHole();
        }

        private RectTransform Strip(string name)
        {
            var rt = NewRect(name, _root);
            Center(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = DimColor;
            img.raycastTarget = true;
            return rt;
        }

        private void BuildCard()
        {
            _card = NewRect("Card", _root);
            Center(_card);
            _card.sizeDelta = new Vector2(CardWidth, 300f);
            _cardGroup = _card.gameObject.AddComponent<CanvasGroup>();
            _cardGroup.alpha = 0f;

            var bg = Img(_card.gameObject, _skin.Window, Color.white, UiTheme.Surface);
            bg.raycastTarget = true;

            var v = _card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(56, 56, 46, 46);
            v.spacing = 18f;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            _card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _title = Text("Title", _card, 40f, UiTheme.Accent, bold: true);
            _title.enableWordWrapping = true;

            _body = Text("Body", _card, 29f, UiTheme.Text, bold: false);
            _body.enableWordWrapping = true;
            _body.lineSpacing = 6f;

            // Coin hediyesi: [coin] +100
            _coinRow = NewRect("Coins", _card);
            var row = _coinRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 14f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            if (_skin.Coin != null)
            {
                _coinIcon = NewRect("CoinIcon", _coinRow);
                var icon = Img(_coinIcon.gameObject, _skin.Coin, Color.white, Color.white, sliced: false);
                icon.preserveAspect = true;
                var le = _coinIcon.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = 64f;
            }
            _coinAmount = Text("Amount", _coinRow, 60f, UiTheme.Warm, bold: true);
            _coinRow.gameObject.SetActive(false);

            _buttonRow = NewRect("Buttons", _card);
            var br = _buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            br.childAlignment = TextAnchor.MiddleCenter;
            br.spacing = 20f;
            br.childControlWidth = br.childControlHeight = true;
            br.childForceExpandWidth = br.childForceExpandHeight = false;
            br.padding = new RectOffset(0, 0, 10, 0);

            _secondary = BuildButton("Secondary", _skin.Button, primary: false, out _secondaryLabel);
            _primary   = BuildButton("Primary", _skin.ButtonPrimary != null ? _skin.ButtonPrimary : _skin.Button,
                                     primary: true, out _primaryLabel);
            _secondary.onClick.AddListener(() => Press(_onSecondary));
            _primary.onClick.AddListener(() => Press(_onPrimary));
            UINav.LinkRow(_secondary, _primary);

            _card.gameObject.SetActive(false);
        }

        private Button BuildButton(string name, Sprite sprite, bool primary, out TMP_Text label)
        {
            var rt = NewRect(name, _buttonRow);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            // İki buton + boşluk kartın iç genişliğine (660 − 2×56) sığmalı
            le.preferredWidth = le.minWidth = 262f;
            le.preferredHeight = le.minHeight = 80f;

            Color flat = primary ? UiTheme.AccentDeep : UiTheme.Raised;
            var fill = Img(rt.gameObject, sprite, Color.white, flat);
            fill.raycastTarget = true;

            label = Text("Label", rt, 28f, UiTheme.Text, bold: true);
            Stretch(label.rectTransform);
            label.margin = new Vector4(16f, 0f, 16f, 6f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 28f;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = fill;
            btn.transition    = Selectable.Transition.None;

            // Görsel varsa renk görselde: tint yalnızca parlaklık (ShopConfirmDialog ile aynı)
            Color normal = sprite != null ? new Color(0.86f, 0.86f, 0.86f, 1f) : flat;
            Color hover  = sprite != null ? Color.white
                         : primary ? Color.Lerp(UiTheme.AccentDeep, UiTheme.Accent, 0.28f) : UiTheme.RaisedHover;
            rt.gameObject.AddComponent<KitButton>().Configure(fill, null, label, normal, hover, Color.clear, Color.clear);
            return btn;
        }

        // ── API ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Kartı gösterir. <paramref name="primaryKey"/> boşsa buton satırı gizlenir (yalnızca
        /// talimat). <paramref name="coins"/> &gt; 0 ise sayarak artan bir hediye satırı çıkar.
        /// Gamepad: A = birincil buton, B = ikincil (yoksa hiçbir şey).
        /// </summary>
        public void ShowCard(string title, string body, int coins,
                             string primaryKey, Action onPrimary,
                             string secondaryKey = null, Action onSecondary = null)
        {
            if (_closing) return;

            _title.text = title;
            _body.text  = body;
            _onPrimary   = onPrimary;
            _onSecondary = onSecondary;

            bool hasPrimary   = !string.IsNullOrEmpty(primaryKey);
            bool hasSecondary = !string.IsNullOrEmpty(secondaryKey);
            _buttonRow.gameObject.SetActive(hasPrimary || hasSecondary);
            _primary.gameObject.SetActive(hasPrimary);
            _secondary.gameObject.SetActive(hasSecondary);
            if (hasPrimary)   _primaryLabel.text   = Loc.Get(primaryKey);
            if (hasSecondary) _secondaryLabel.text = Loc.Get(secondaryKey);

            _coinTween?.Kill();
            _coinRow.gameObject.SetActive(coins > 0);
            if (coins > 0)
            {
                int shown = 0;
                _coinAmount.text = "+0";
                _coinTween = DOTween.To(() => shown, x => { shown = x; _coinAmount.text = "+" + x.ToString("N0"); },
                                        coins, 0.9f)
                    .SetDelay(0.25f).SetEase(Ease.OutCubic).SetUpdate(true)
                    .OnComplete(() =>
                    {
                        if (_coinIcon != null)
                            _coinIcon.DOPunchScale(Vector3.one * 0.35f, 0.45f, 8, 0.6f).SetUpdate(true);
                    });
            }

            _card.gameObject.SetActive(true);
            _cardVisible = true;
            _cardShownAt = Time.unscaledTime;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_card);
            PlaceCard();

            _card.DOKill();
            _cardGroup.DOKill();
            _cardGroup.alpha = 0f;
            _card.localScale = Vector3.one * 0.92f;
            _cardGroup.DOFade(1f, 0.22f).SetUpdate(true);
            _card.DOScale(1f, 0.34f).SetEase(Ease.OutBack).SetUpdate(true);

            if (hasPrimary || hasSecondary)
                UINav.Push(_card.gameObject, hasPrimary ? _primary : _secondary,
                           hasSecondary ? (Action)(() => Press(_onSecondary)) : null);
            else
                UINav.Pop(_card.gameObject);
        }

        /// <summary>Kartı soldurur; karartma ve delik yerinde kalır.</summary>
        public void HideCard()
        {
            if (!_cardVisible) return;
            _cardVisible = false;
            UINav.Pop(_card.gameObject);
            _coinTween?.Kill();
            _card.DOKill();
            _cardGroup.DOKill();
            _cardGroup.DOFade(0f, 0.15f).SetUpdate(true).OnComplete(() =>
            {
                if (!_cardVisible) _card.gameObject.SetActive(false);
            });
        }

        /// <summary>Işık deliğinin hedefleri; boş → tüm ekran kararır, kart ortada durur.</summary>
        public void SetTargets(params RectTransform[] targets)
        {
            _targets.Clear();
            if (targets != null)
                foreach (var t in targets) if (t != null) _targets.Add(t);
            bool had = _hasHole;
            UpdateHoleTarget();
            if (!had) _hole = _holeTarget;   // ilk delik yerinde doğar, sonrakiler akar
            ApplyHole();
        }

        /// <summary>Solar ve yok olur. Kapsamlar düşer, tıklamalar hemen serbest kalır.</summary>
        public void Close(Action onClosed = null)
        {
            if (_closing) return;
            _closing = true;
            UINav.Pop(_card.gameObject);
            _coinTween?.Kill();
            _group.blocksRaycasts = false;
            _group.interactable   = false;
            _group.DOKill();
            _group.DOFade(0f, 0.22f).SetUpdate(true).OnComplete(() =>
            {
                onClosed?.Invoke();
                if (this != null) Destroy(gameObject);
            });
        }

        // ── Döngü ────────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (_closing) return;

            UpdateHoleTarget();
            if (_hasHole)
            {
                float k = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
                _hole = Rect.MinMaxRect(
                    Mathf.Lerp(_hole.xMin, _holeTarget.xMin, k), Mathf.Lerp(_hole.yMin, _holeTarget.yMin, k),
                    Mathf.Lerp(_hole.xMax, _holeTarget.xMax, k), Mathf.Lerp(_hole.yMax, _holeTarget.yMax, k));
            }
            ApplyHole();

            if (_frame.gameObject.activeSelf)
            {
                float t = Time.unscaledTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);
                var a = UiTheme.Accent;
                _frameLine.color = new Color(a.r, a.g, a.b, 0.80f + 0.20f * pulse);
                _frameGlow.color = new Color(a.r, a.g, a.b, 0.30f + 0.45f * pulse);

                // Halka 1.3 sn'de bir dışa yayılıp söner
                float p = Mathf.Repeat(t, 1.3f) / 1.3f;
                float grow = Mathf.Lerp(0f, 26f, 1f - (1f - p) * (1f - p));
                _ripple.offsetMin = new Vector2(-grow, -grow);
                _ripple.offsetMax = new Vector2(grow, grow);
                _rippleLine.color = new Color(a.r, a.g, a.b, 0.6f * (1f - p));
            }

            if (_cardVisible) PlaceCard();
        }

        private void Press(Action action)
        {
            if (_closing || action == null) return;
            if (Time.unscaledTime - _cardShownAt < InputGuard) return;
            action();
        }

        // ── Delik ────────────────────────────────────────────────────────────

        private void UpdateHoleTarget()
        {
            var corners = new Vector3[4];
            bool any = false;
            float x0 = 0, y0 = 0, x1 = 0, y1 = 0;
            foreach (var t in _targets)
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                t.GetWorldCorners(corners);
                foreach (var c in corners)
                {
                    Vector3 l = _root.InverseTransformPoint(c);
                    if (!any) { x0 = x1 = l.x; y0 = y1 = l.y; any = true; continue; }
                    x0 = Mathf.Min(x0, l.x); x1 = Mathf.Max(x1, l.x);
                    y0 = Mathf.Min(y0, l.y); y1 = Mathf.Max(y1, l.y);
                }
            }
            _hasHole = any;
            if (any) _holeTarget = Rect.MinMaxRect(x0 - HolePadding, y0 - HolePadding, x1 + HolePadding, y1 + HolePadding);
        }

        private void ApplyHole()
        {
            Rect r = _root.rect;
            if (!_hasHole)
            {
                SetRect(_top, r.xMin, r.yMin, r.xMax, r.yMax);
                _bottom.gameObject.SetActive(false);
                _left.gameObject.SetActive(false);
                _right.gameObject.SetActive(false);
                _frame.gameObject.SetActive(false);
                return;
            }

            Rect h = Rect.MinMaxRect(
                Mathf.Clamp(_hole.xMin, r.xMin, r.xMax), Mathf.Clamp(_hole.yMin, r.yMin, r.yMax),
                Mathf.Clamp(_hole.xMax, r.xMin, r.xMax), Mathf.Clamp(_hole.yMax, r.yMin, r.yMax));

            _bottom.gameObject.SetActive(true);
            _left.gameObject.SetActive(true);
            _right.gameObject.SetActive(true);
            SetRect(_top,    r.xMin, h.yMax, r.xMax, r.yMax);
            SetRect(_bottom, r.xMin, r.yMin, r.xMax, h.yMin);
            SetRect(_left,   r.xMin, h.yMin, h.xMin, h.yMax);
            SetRect(_right,  h.xMax, h.yMin, r.xMax, h.yMax);
            SetRect(_holeCover, h.xMin, h.yMin, h.xMax, h.yMax);

            _frame.gameObject.SetActive(true);
            SetRect(_frame, h.xMin, h.yMin, h.xMax, h.yMax);
        }

        private static void SetRect(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchoredPosition = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, x1 - x0), Mathf.Max(0f, y1 - y0));
        }

        // ── Kart yerleşimi ───────────────────────────────────────────────────

        /// <summary>
        /// Delik yoksa ekranın ortası. Varsa deliğin yanında, deliğe binmeyen ve ekrana
        /// sığan ilk konum: dikey ekranda önce alt/üst, yatayda önce sağ/sol.
        /// </summary>
        private void PlaceCard()
        {
            Vector2 size = _card.rect.size;
            if (!_hasHole) { _card.anchoredPosition = Vector2.zero; return; }

            Rect bounds = _root.rect;
            Rect u = _holeTarget;
            float hw = size.x * 0.5f, hh = size.y * 0.5f;
            Vector2 right = new Vector2(u.xMax + CardGap + hw, u.center.y);
            Vector2 left  = new Vector2(u.xMin - CardGap - hw, u.center.y);
            Vector2 above = new Vector2(u.center.x, u.yMax + CardGap + hh);
            Vector2 below = new Vector2(u.center.x, u.yMin - CardGap - hh);

            bool portrait = bounds.height > bounds.width;
            Vector2[] order = portrait ? new[] { below, above, right, left }
                                       : new[] { right, left, below, above };

            Vector2 best = ClampInside(order[0], size, bounds);
            float bestOverflow = float.MaxValue;
            foreach (var raw in order)
            {
                var p = ClampInside(raw, size, bounds);
                var rect = new Rect(p - size * 0.5f, size);
                if (rect.Overlaps(u)) continue;
                bool sideways = raw == right || raw == left;
                float overflow = sideways ? Mathf.Abs(raw.x - p.x) : Mathf.Abs(raw.y - p.y);
                if (overflow < 1f) { best = p; break; }
                if (overflow < bestOverflow) { bestOverflow = overflow; best = p; }
            }
            _card.anchoredPosition = best;
        }

        private static Vector2 ClampInside(Vector2 center, Vector2 size, Rect bounds)
        {
            float minX = bounds.xMin + EdgeMargin + size.x * 0.5f, maxX = bounds.xMax - EdgeMargin - size.x * 0.5f;
            float minY = bounds.yMin + EdgeMargin + size.y * 0.5f, maxY = bounds.yMax - EdgeMargin - size.y * 0.5f;
            return new Vector2(
                minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : bounds.center.x,
                minY <= maxY ? Mathf.Clamp(center.y, minY, maxY) : bounds.center.y);
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private void OnDestroy()
        {
            _coinTween?.Kill();
            if (_card != null) UINav.Pop(_card.gameObject);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Image SlicedImage(RectTransform rt, Sprite sprite)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            return img;
        }

        private static Image Img(GameObject go, Sprite sprite, Color tint, Color flat, bool sliced = true)
        {
            var img = go.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : (sliced ? _sprFill : null);
            img.color  = sprite != null ? tint : flat;
            img.type   = img.sprite != null && sliced && img.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        private TMP_Text Text(string name, Transform parent, float size, Color color, bool bold)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            var font = bold && _skin.BoldFont != null ? _skin.BoldFont : _skin.Font;
            if (font != null) t.font = font;
            t.fontStyle = bold && _skin.BoldFont == null ? FontStyles.Bold : FontStyles.Normal;
            t.fontSize  = size;
            t.color     = color;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            return t;
        }

        // ── Prosedürel sprite'lar (TutorialIntroTour ile aynı yöntem) ────────

        private static void EnsureSprites()
        {
            if (_sprLine != null) return;
            _sprFill = MakeRounded(64, 18f, 1f, 0f, 0f, 26);    // görsel yoksa kart/buton dolgusu
            _sprLine = MakeRounded(64, 18f, 1f, 4f, 0f, 26);    // 4 px kenar çizgisi
            _sprGlow = MakeRounded(96, 18f, 26f, 0f, 8f, 44);   // dış parıltı
        }

        private static Sprite MakeRounded(int size, float radius, float margin, float thickness, float glow, int border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float half = size * 0.5f - margin;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = x + 0.5f - size * 0.5f, cy = y + 0.5f - size * 0.5f;
                float qx = Mathf.Abs(cx) - (half - radius), qy = Mathf.Abs(cy) - (half - radius);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                          + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;   // <0 içeride

                float a;
                if (glow > 0f)           a = Mathf.Exp(-(d * d) / (2f * glow * glow));
                else if (thickness > 0f) a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + thickness + 0.5f);
                else                     a = Mathf.Clamp01(0.5f - d);

                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
