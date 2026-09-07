using RogueBlockBlast.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// "Q / E ile döndür" ipucu. Kendi görselini kodda kurar — sahnede tek bir
    /// GameObject'e eklemek yeterli, alt nesne bağlamak gerekmez.
    ///
    /// Döndürme oyunun en kritik girdisiydi ve hiçbir yerde yazmıyordu: yeni oyuncu
    /// parçayı çeviremeyeceğini sanıp ölü havuza düşüyor. İpucu ekranın sol altında,
    /// tahtanın ve havuz slotlarının dışında duruyor.
    ///
    /// Tuşa basınca ilgili kapak bir an parlıyor — ipucu "bu tuş gerçekten çalışıyor"
    /// geri bildirimini de veriyor, ayrı bir öğretici adıma gerek kalmıyor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RotateHintView : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Canvas'ın sol-alt köşesinden uzaklık (referans çözünürlük pikseli).")]
        [SerializeField] private Vector2 _padding = new Vector2(36f, 32f);

        [Header("Style")]
        [SerializeField] private Color _chipColor  = new Color(0.06f, 0.08f, 0.13f, 0.78f);
        [SerializeField] private Color _capColor   = new Color(0.16f, 0.20f, 0.30f, 1f);
        [SerializeField] private Color _capFlash   = new Color(0.35f, 0.85f, 1f, 1f);
        [SerializeField] private Color _textColor  = new Color(0.82f, 0.87f, 0.95f, 1f);
        [SerializeField] private float _fontSize   = 26f;

        [Header("Behaviour")]
        [Tooltip("Fare tekerleğinin de döndürdüğünü yazar.")]
        [SerializeField] private bool _mentionScrollWheel = true;

        private const float CapSize   = 40f;
        private const float FlashTime = 0.18f;

        [SerializeField] private Color _capLockedColor = new Color(1f, 0.42f, 0.40f, 1f);

        private Image     _capQ, _capE;
        private Image     _slashQ, _slashE;
        private bool      _locked;
        private int       _freeLeft = -1;
        private TMP_Text  _label;
        private float     _flashQ, _flashE;

        private void Start()
        {
            Build();

            // Kilit durumu Build()'den önce gelmiş olabilir (RunController'ın ilk
            // yerleştirmesi bu Start'tan önce çalışabiliyor) — kapaklar yeni
            // oluştuğu için görseli burada bir kez daha uygula.
            if (_locked) ApplyLockVisual();

            Refresh();
            Loc.OnChanged += Refresh;
        }

        private void OnDestroy() => Loc.OnChanged -= Refresh;

        // ── Görsel kurulum ───────────────────────────────────────────────────
        private void Build()
        {
            var font = FindFont();

            var root = NewRect("RotateHint", (RectTransform)transform);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = _padding;
            root.sizeDelta = new Vector2(300f, 56f);

            var bg = root.gameObject.AddComponent<Image>();
            bg.color = _chipColor;
            bg.raycastTarget = false;

            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding        = new RectOffset(12, 16, 8, 8);
            layout.spacing        = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth      = true;
            layout.childControlHeight     = true;

            var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            _capQ = BuildCap(root, "Q", font);
            _capE = BuildCap(root, "E", font);

            var labelRect = NewRect("Label", root);
            _label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) _label.font = font;
            _label.fontSize             = _fontSize;
            _label.color                = _textColor;
            _label.alignment            = TextAlignmentOptions.MidlineLeft;
            _label.raycastTarget        = false;
            _label.enableWordWrapping   = false;
            labelRect.gameObject.AddComponent<LayoutElement>().minHeight = CapSize;
        }

        private Image BuildCap(RectTransform parent, string key, TMP_FontAsset font)
        {
            var capRect = NewRect("Cap_" + key, parent);
            var img = capRect.gameObject.AddComponent<Image>();
            img.color = _capColor;
            img.raycastTarget = false;

            var le = capRect.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth  = CapSize;
            le.preferredHeight = CapSize;
            le.minWidth        = CapSize;
            le.minHeight       = CapSize;

            var textRect = NewRect("Text", capRect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;

            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text          = key;
            text.fontSize      = _fontSize;
            text.fontStyle     = FontStyles.Bold;
            text.color         = _textColor;
            text.alignment     = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            return img;
        }

        private static RectTransform NewRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>
        /// Sahnede zaten kullanılan TMP fontunu ödünç alır. Böylece ipucu
        /// oyunun geri kalanıyla aynı yazı tipiyle çıkar ve fallback zinciri
        /// (Türkçe / CJK) kendiliğinden geçerli olur.
        /// </summary>
        private TMP_FontAsset FindFont()
        {
            var any = GetComponentInChildren<TMP_Text>(true);
            if (any != null) return any.font;

            var scene = FindObjectOfType<TMP_Text>();
            return scene != null ? scene.font : null;
        }

        // ── Metin ────────────────────────────────────────────────────────────
        private void Refresh()
        {
            if (_label == null) return;

            // First Picks döndürmeyi kapatıyor. İpucu chip'i normal görünmeye devam
            // ederse oyuncu Q/E'ye basıp hiçbir şey olmamasını bug sanıyor — kilit
            // tuşların üstünde gösterilmeli, kart metninde değil.
            if (_locked)
            {
                _label.text = _freeLeft > 0
                    ? Loc.Get("Hud.RotateLockedFree", _freeLeft)
                    : Loc.GetOr("Hud.RotateLocked", "Rotation locked");
                return;
            }

            _label.text = _mentionScrollWheel
                ? Loc.GetOr("Hud.RotateHintWheel", "Rotate  ·  mouse wheel")
                : Loc.GetOr("Hud.RotateHint",      "Rotate");
        }

        // ── Döndürme kilidi (First Picks) ────────────────────────────────────

        /// <summary>
        /// Döndürme kilidini ve kalan bedava şekil sayısını gösterir.
        /// RunController her yerleştirmeden sonra çağırır; durum değişmediyse
        /// hiçbir iş yapılmaz.
        /// </summary>
        public void SetRotationLocked(bool locked, int freeRemaining)
        {
            if (_locked == locked && _freeLeft == freeRemaining) return;

            _locked   = locked;
            _freeLeft = freeRemaining;

            ApplyLockVisual();
            Refresh();
        }

        private void ApplyLockVisual()
        {
            EnsureSlash(ref _slashQ, _capQ);
            EnsureSlash(ref _slashE, _capE);

            if (_slashQ != null) _slashQ.enabled = _locked;
            if (_slashE != null) _slashE.enabled = _locked;

            var capTint = _locked ? _capLockedColor : _capColor;
            if (_capQ != null) _capQ.color = capTint;
            if (_capE != null) _capE.color = capTint;
            if (_label != null) _label.color = _locked ? _capLockedColor : _textColor;
        }

        /// <summary>Tuş kapağının üzerine çapraz bir çizgi koyar — "bu tuş çalışmıyor".</summary>
        private void EnsureSlash(ref Image slash, Image cap)
        {
            if (slash != null || cap == null) return;

            var go = new GameObject("Slash", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(cap.rectTransform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(CapSize * 1.15f, 3.5f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -45f);

            slash = go.AddComponent<Image>();
            slash.sprite        = RogueBlockBlast.UI.FX.OverlayFXGraphics.Pixel;
            slash.color         = _capLockedColor;
            slash.raycastTarget = false;
            slash.enabled       = false;
        }

        // ── Basış geri bildirimi ─────────────────────────────────────────────
        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) _flashQ = FlashTime;
                if (kb.eKey.wasPressedThisFrame) _flashE = FlashTime;
            }

            if (_locked) return;   // kilitliyken tuşa basmak parlamamalı

            Tick(ref _flashQ, _capQ);
            Tick(ref _flashE, _capE);
        }

        private void Tick(ref float timer, Image cap)
        {
            if (cap == null || timer <= 0f) return;
            timer = Mathf.Max(0f, timer - Time.unscaledDeltaTime);
            cap.color = Color.Lerp(_capColor, _capFlash, timer / FlashTime);
        }
    }
}
