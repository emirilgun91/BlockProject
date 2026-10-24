using System;
using DG.Tweening;
using RogueBlockBlast.Core.Localization;
using RogueBlockBlast.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Shape Shop satın alma onayı: "L PUAN GELİŞTİRMESİ · 500 · Onayla / İptal Et / Bir daha gösterme".
    ///
    /// Arayüzünü kendisi kurar (prefab'a dokunmaz): <see cref="ShapeShopController"/> ilk
    /// satın almada mağazanın köküne ekler, görselleri (pencere, buton, coin ikonu) ona verir.
    /// Prefab ana menüde de oyun sahnesinin Upgrade Now overlay'inde de aynı olduğundan
    /// iki yerde de çalışır.
    ///
    /// "Bir daha gösterme" yalnızca Onayla ile kaydedilir: işaretleyip İptal'e basan
    /// oyuncunun bir sonraki tıklaması sorusuz para harcamasın. Geri açmak için
    /// Ayarlar ▸ Oynanış ▸ Mağazada Satın Almayı Onayla.
    ///
    /// Game Over sırasında <c>timeScale = 0</c>; tüm tween'ler SetUpdate(true).
    /// </summary>
    public sealed class ShopConfirmDialog : MonoBehaviour
    {
        /// <summary>Görseller — hepsi opsiyonel, yoksa düz renkli kutuya düşer.</summary>
        public struct Skin
        {
            public Sprite Window, Button, ButtonPrimary, Track, Fill, Coin;
            public TMP_FontAsset Font, BoldFont;
        }

        // Açılışı tetikleyen tıklamanın ikinci yarısı (çift tık, A'yı basılı tutma) hemen satın almasın
        private const float InputGuard = 0.25f;

        private static int _closedFrame = -1;

        /// <summary>
        /// Açık mı — ya da bu karede mi kapandı. ESC'yi okuyan diğer ekranlar (overlay)
        /// aynı ESC ile bir de kendilerini kapatmasın diye bakar.
        /// </summary>
        public static bool BlocksEscape => _openCount > 0 || Time.frameCount == _closedFrame;
        private static int _openCount;

        private CanvasGroup   _group;
        private RectTransform _window;
        private TMP_Text      _headline, _detail, _price;
        private Toggle        _dontAsk;
        private Button        _confirm, _cancel;

        private Action _onConfirm;
        private bool   _open;
        private float  _openedAt;

        // ── Public API ───────────────────────────────────────────────────────

        public static ShopConfirmDialog Create(Transform parent, Skin skin)
        {
            var go = new GameObject("PurchaseConfirm", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var dialog = go.AddComponent<ShopConfirmDialog>();
            dialog.Build(skin);
            go.SetActive(false);
            return dialog;
        }

        public void Show(string headline, string detail, int cost, Action onConfirm)
        {
            _onConfirm = onConfirm;
            bool hasDetail = !string.IsNullOrEmpty(detail);
            _headline.text = headline;
            _headline.rectTransform.anchoredPosition = new Vector2(0f, hasDetail ? -78f : -100f);
            _detail.text   = detail ?? string.Empty;
            _detail.gameObject.SetActive(hasDetail);
            _price.text    = cost.ToString("N0");
            _dontAsk.SetIsOnWithoutNotify(false);

            transform.SetAsLastSibling();          // kartların ve toast'un üstünde
            gameObject.SetActive(true);
            if (!_open) _openCount++;
            _open     = true;
            _openedAt = Time.unscaledTime;

            _group.DOKill();
            _window.DOKill();
            _group.alpha          = 0f;
            _group.interactable   = true;
            _group.blocksRaycasts = true;
            _window.localScale    = Vector3.one * 0.9f;
            _group.DOFade(1f, 0.16f).SetUpdate(true);
            _window.DOScale(1f, 0.26f).SetEase(Ease.OutBack).SetUpdate(true);

            UINav.LinkRow(_cancel, _confirm);
            LinkToggle();
            UINav.Push(gameObject, _confirm, Cancel);
        }

        public bool IsOpen => _open;

        public void Cancel() => Close(false);

        // ── Unity ────────────────────────────────────────────────────────────

        // Enter bilerek yok: odak İptal'deyken EventSystem'in submit'iyle yarışıp satın alabilirdi.
        private void Update()
        {
            var kb = Keyboard.current;
            if (_open && kb != null && kb.escapeKey.wasPressedThisFrame) Cancel();
        }

        private void OnDisable()
        {
            // Mağaza bu açıkken kapanırsa (sahne değişimi) sayaç takılı kalmasın
            if (_open) { _open = false; _openCount--; }
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void OnConfirm()
        {
            if (!_open || Time.unscaledTime - _openedAt < InputGuard) return;
            if (_dontAsk.isOn) GameSettings.ConfirmPurchase = false;
            Close(true);
        }

        private void Close(bool confirmed)
        {
            if (!_open) return;
            _open = false;
            _openCount--;
            _closedFrame = Time.frameCount;

            var action = _onConfirm;
            _onConfirm = null;

            UINav.Pop(gameObject);
            _group.interactable   = false;
            _group.blocksRaycasts = false;
            _group.DOKill();
            _window.DOKill();
            _window.DOScale(0.94f, 0.12f).SetUpdate(true);
            _group.DOFade(0f, 0.12f).SetUpdate(true).OnComplete(() => gameObject.SetActive(false));

            // Satın alma kapanış animasyonunu beklemez: coin sayısı ve kart hemen güncellensin
            if (confirmed) action?.Invoke();
        }

        /// <summary>Kutucuk butonların üstünde: aşağı → Onayla, butonlardan yukarı → kutucuk.</summary>
        private void LinkToggle()
        {
            var t = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = _confirm };
            _dontAsk.navigation = t;
            foreach (var b in new[] { _cancel, _confirm })
            {
                var nav = b.navigation;
                nav.selectOnUp = _dontAsk;
                b.navigation = nav;
            }
        }

        // ── Build ────────────────────────────────────────────────────────────

        private void Build(Skin skin)
        {
            var root = (RectTransform)transform;
            Stretch(root);
            _group = gameObject.AddComponent<CanvasGroup>();

            // Arkadaki mağazayı karart ve tıklamaları yut
            var scrim = gameObject.AddComponent<Image>();
            // Lineer renk uzayı + HDR: temanın 0.82'si zaten koyu mağazada fark edilmiyor
            scrim.color = UiTheme.WithAlpha(UiTheme.Scrim, 0.94f);

            _window = NewRect("Window", root);
            _window.sizeDelta = new Vector2(660f, 410f);
            Img(_window.gameObject, skin.Window, Color.white, UiTheme.Surface).raycastTarget = true;

            _headline = Text("Headline", _window, skin, 34f, UiTheme.Text, true);
            Place(_headline.rectTransform, 0f, -78f, 560f, 48f);
            _headline.enableAutoSizing = true;
            _headline.fontSizeMin = 22f;
            _headline.fontSizeMax = 34f;

            _detail = Text("Detail", _window, skin, 24f, UiTheme.TextDim, false);
            Place(_detail.rectTransform, 0f, -122f, 560f, 32f);

            // Fiyat: [coin] 500 — ortalı, genişliği yazıya göre
            var price = NewRect("Price", _window);
            Place(price, 0f, -174f, 400f, 52f);
            var row = price.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 10f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            if (skin.Coin != null)
            {
                var icon = NewRect("CoinIcon", price);
                var img = Img(icon.gameObject, skin.Coin, Color.white, Color.white, sliced: false);
                img.preserveAspect = true;
                var le = icon.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = 42f;
            }
            _price = Text("Amount", price, skin, 40f, UiTheme.Warm, true);

            _dontAsk = BuildToggle(_window, skin);

            _cancel  = BuildButton("Cancel",  _window, skin.Button, skin, "Shop.Confirm.No",  -132f, primary: false);
            _confirm = BuildButton("Confirm", _window, skin.ButtonPrimary != null ? skin.ButtonPrimary : skin.Button,
                                   skin, "Shop.Confirm.Yes", 132f, primary: true);
            _cancel.onClick.AddListener(Cancel);
            _confirm.onClick.AddListener(OnConfirm);
        }

        private Toggle BuildToggle(RectTransform parent, Skin skin)
        {
            // [kutucuk] Bir daha gösterme — satır içeriği kadar geniş ve ortalı; tamamı
            // tıklanır (görünmez Image ışın hedefi)
            var rowRt = NewRect("DontAsk", parent);
            Place(rowRt, 0f, -238f, 0f, 44f);
            var hit = rowRt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var row = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 14f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            rowRt.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var box = NewRect("Box", rowRt);
            var boxLe = box.gameObject.AddComponent<LayoutElement>();
            boxLe.preferredWidth = boxLe.preferredHeight = 34f;
            var boxImg = Img(box.gameObject, skin.Track, Color.white, UiTheme.Sunken);
            // Ray görseli koyu pencerede kayboluyor: kit'in çelik kenarı
            var rim = box.gameObject.AddComponent<Outline>();
            rim.effectColor    = UiTheme.Rim;
            rim.effectDistance = new Vector2(2f, -2f);

            var check = NewRect("Check", box);
            check.sizeDelta = new Vector2(20f, 20f);
            var checkImg = Img(check.gameObject, skin.Fill, UiTheme.Accent, UiTheme.Accent);

            var label = LocText("Label", rowRt, skin, "Shop.Confirm.DontAsk", 24f, UiTheme.TextDim, false);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            var toggle = rowRt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImg;
            toggle.graphic       = checkImg;
            toggle.transition    = Selectable.Transition.ColorTint;
            var colors = toggle.colors;
            colors.highlightedColor = colors.selectedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.colorMultiplier  = 1.4f;
            toggle.colors = colors;
            toggle.isOn = false;
            return toggle;
        }

        private static Button BuildButton(string name, RectTransform parent, Sprite sprite, Skin skin,
                                          string key, float x, bool primary)
        {
            var rt = NewRect(name, parent);
            Place(rt, x, -324f, 240f, 76f);

            Color flat = primary ? UiTheme.AccentDeep : UiTheme.Raised;
            var fill = Img(rt.gameObject, sprite, Color.white, flat);
            var label = LocText("Label", rt, skin, key, 28f, UiTheme.Text, true);
            Stretch(label.rectTransform);
            label.margin = new Vector4(16f, 0f, 16f, 6f);   // görselin alt kenarı kalın
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 28f;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = fill;
            btn.transition    = Selectable.Transition.None;

            // Görsel varsa renk görselde: tint yalnızca parlaklık (UiKit.Button ile aynı kural)
            Color normal = sprite != null ? new Color(0.86f, 0.86f, 0.86f, 1f) : flat;
            Color hover  = sprite != null ? Color.white
                         : primary ? Color.Lerp(UiTheme.AccentDeep, UiTheme.Accent, 0.28f) : UiTheme.RaisedHover;
            rt.gameObject.AddComponent<KitButton>().Configure(fill, null, label, normal, hover, Color.clear, Color.clear);
            return btn;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>Üst kenara göre yerleşim: y, pencerenin tepesinden aşağı (negatif).</summary>
        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static Image Img(GameObject go, Sprite sprite, Color tint, Color flat, bool sliced = true)
        {
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color  = sprite != null ? tint : flat;
            img.type   = sprite != null && sliced && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text Text(string name, Transform parent, Skin skin, float size, Color color, bool bold)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            // Kalın font yoksa TMP'nin sahte kalını
            var font = bold && skin.BoldFont != null ? skin.BoldFont : skin.Font;
            if (font != null) t.font = font;
            t.fontStyle = bold && skin.BoldFont == null ? FontStyles.Bold : FontStyles.Normal;
            t.fontSize  = size;
            t.color     = color;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.overflowMode  = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        private static TMP_Text LocText(string name, Transform parent, Skin skin, string key,
                                        float size, Color color, bool bold)
        {
            var t = Text(name, parent, skin, size, color, bold);
            t.gameObject.AddComponent<LocRefresh>().Key = key;
            return t;
        }

        /// <summary>Sabit etiket, dil değişince kendini yeniler.</summary>
        private sealed class LocRefresh : MonoBehaviour
        {
            public string Key;
            private TMP_Text _text;

            private void OnEnable()
            {
                if (_text == null) _text = GetComponent<TMP_Text>();
                Apply();
                Loc.OnChanged += Apply;
            }

            private void OnDisable() => Loc.OnChanged -= Apply;

            private void Apply()
            {
                if (_text != null && !string.IsNullOrEmpty(Key)) _text.text = Loc.Get(Key);
            }
        }
    }
}
