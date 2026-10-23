using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Pool yenileme butonu.
    /// RunController'dan Bind() ile hak sayısı verilir.
    /// 
    /// Hierarchy:
    ///  PoolRerollButton (bu script + Button)
    ///   ├── Icon (TMP — "🔄")
    ///   └── CountText (TMP — "x2")
    /// </summary>
    public sealed class PoolRerollButton : MonoBehaviour
    {
        [SerializeField] private Button   _button;
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private CanvasGroup _canvasGroup;

        public System.Action OnRerollClicked;

        private void Awake()
        {
            _button?.onClick.AddListener(() => OnRerollClicked?.Invoke());
            BuildPadGlyph();
            RefreshPadGlyph();
            InputMode.Changed += RefreshPadGlyph;
        }

        private void OnDestroy() => InputMode.Changed -= RefreshPadGlyph;

        // ── Gamepad ──────────────────────────────────────────────────────────
        // Gamepad'de buton Y ile basılır (RunController); köşedeki rozet bunu söyler.

        private GameObject _padGlyph;
        private TMP_Text   _padGlyphText;

        private void BuildPadGlyph()
        {
            var go = new GameObject("PadGlyph", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-6f, -6f);
            rt.sizeDelta = new Vector2(38f, 38f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.09f, 0.9f);
            bg.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textGo.transform;
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            _padGlyphText = textGo.AddComponent<TextMeshProUGUI>();
            if (_countText != null) _padGlyphText.font = _countText.font;
            _padGlyphText.fontSize      = 26f;
            _padGlyphText.alignment     = TextAlignmentOptions.Center;
            _padGlyphText.raycastTarget = false;
            _padGlyph = go;
        }

        private void RefreshPadGlyph()
        {
            if (_padGlyph == null) return;
            _padGlyph.SetActive(InputMode.UsingGamepad);
            _padGlyphText.text = InputMode.GlyphRich(PadButton.North);
        }

        /// <summary>Kalan hak sayısını güncelle.</summary>
        public void UpdateCount(int remaining)
        {
            bool hasRolls = remaining > 0;

            if (_countText != null)
                _countText.text = $"x{remaining}";

            if (_button != null)
                _button.interactable = hasRolls;

            if (_canvasGroup != null)
                _canvasGroup.alpha = hasRolls ? 1f : 0.35f;
        }

        /// <summary>Upgrade yoksa gizle.</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}