using DG.Tweening;
using RogueBlockBlast.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Portrait mobil kontrolleri: döndür butonu (havuzun sağında) ve duraklat
    /// butonu (sağ üst köşe). Klavye / fare tekerleği / ESC'nin dokunmatik karşılığı.
    ///
    /// Kendi arayüzünü kurar — bileşeni Gameplay Canvas'a eklemek kurulumun
    /// tamamı (RotateHintView ile aynı yaklaşım). İkonlar kodla çizilir: buton
    /// üzerinde metin yok, dolayısıyla çeviri de gerekmez.
    ///
    /// Döndürme <see cref="RunController.RotateCurrentPiece"/> üzerinden gider —
    /// First Picks kilidi gibi kurallar klavyeyle birebir aynı uygulanır.
    /// </summary>
    public sealed class MobileControlsView : MonoBehaviour
    {
        [SerializeField] private RunController        _run;
        [SerializeField] private PauseMenuController  _pause;

        [Header("Rotate")]
        [Tooltip("Havuzun sağında. Anchor sağ-alt, konum piksel (1080×1920 referansında).")]
        [SerializeField] private Vector2 _rotatePosition = new Vector2(-96f, 250f);
        [SerializeField] private float   _rotateSize     = 150f;

        [Header("Pause")]
        [SerializeField] private Vector2 _pausePosition = new Vector2(-64f, -64f);
        [SerializeField] private float   _pauseSize     = 88f;

        [Header("Style")]
        [SerializeField] private Color _buttonColor = new Color(0.10f, 0.12f, 0.22f, 0.92f);
        [SerializeField] private Color _ringColor   = new Color(0.55f, 0.40f, 1f, 1f);
        [SerializeField] private Color _iconColor   = new Color(0.90f, 0.93f, 1f, 1f);

        private RectTransform _rotateRect;

        private static Sprite _circle, _ring, _rotateIcon;

        private void Awake()
        {
            if (_run   == null) _run   = FindAnyObjectByType<RunController>();
            if (_pause == null) _pause = FindAnyObjectByType<PauseMenuController>();
            Build();
        }

        // ── Kurulum ──────────────────────────────────────────────────────────

        private void Build()
        {
            var parent = (RectTransform)transform;

            // Döndür
            _rotateRect = NewButton("RotateButton", parent, new Vector2(1f, 0f), _rotatePosition, _rotateSize,
                                    () => { _run?.RotateCurrentPiece(+1); Punch(_rotateRect); });
            AddIcon(_rotateRect, RotateIcon, 0.62f);

            // Duraklat
            var pauseRect = NewButton("PauseButton", parent, new Vector2(1f, 1f), _pausePosition, _pauseSize,
                                      () => _pause?.Open());
            float barW = _pauseSize * 0.12f, barH = _pauseSize * 0.40f, gap = _pauseSize * 0.10f;
            AddBar(pauseRect, new Vector2(-(gap + barW) * 0.5f, 0f), new Vector2(barW, barH));
            AddBar(pauseRect, new Vector2( (gap + barW) * 0.5f, 0f), new Vector2(barW, barH));
        }

        private RectTransform NewButton(string name, RectTransform parent, Vector2 anchor, Vector2 pos,
                                        float size, UnityEngine.Events.UnityAction onClick)
        {
            var rt = NewRect(name, parent);
            // HUD'ın üstüne ama modal panellerin (kart seçimi ve sonrası: game over,
            // pause, ayarlar) ALTINA: karartmalar butonları örtsün — yoksa karartmanın
            // üstünde parlak kalıyorlardı. En başa koymak da olmaz: PoolPanel'in arka
            // planı döndür butonunun dokunuşlarını yutardı.
            var firstModal = parent.Find("CardScreen");
            rt.SetSiblingIndex(firstModal != null ? firstModal.GetSiblingIndex() : parent.childCount - 1);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);

            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = Circle;
            bg.color  = _buttonColor;

            var ringRect = NewRect("Ring", rt);
            Stretch(ringRect);
            var ring = ringRect.gameObject.AddComponent<Image>();
            ring.sprite = Ring;
            ring.color  = _ringColor;
            ring.raycastTarget = false;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.85f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            // Butonda başlayan dokunuş tahtaya "bırakma" sayılmaz —
            // RunController.PressStartedOnInteractiveUI Selectable'ları eler.
            return rt;
        }

        private void AddIcon(RectTransform parent, Sprite sprite, float scale)
        {
            var rt = NewRect("Icon", parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = parent.sizeDelta * scale;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color  = _iconColor;
            img.raycastTarget = false;
        }

        private void AddBar(RectTransform parent, Vector2 pos, Vector2 size)
        {
            var rt = NewRect("Bar", parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = _iconColor;
            img.raycastTarget = false;
        }

        private static void Punch(RectTransform rt)
        {
            if (rt == null) return;
            rt.DOKill(complete: true);
            rt.DOPunchRotation(new Vector3(0f, 0f, -35f), 0.28f, 6, 0.6f).SetUpdate(true);
        }

        private static RectTransform NewRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ── Prosedürel sprite'lar ────────────────────────────────────────────

        private static Sprite Circle => _circle ??= Make(128, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((1f - r) * 64f);
        });

        private static Sprite Ring => _ring ??= Make(128, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float d = Mathf.Abs(r - 0.93f);
            return Mathf.Clamp01((0.045f - d) * 64f);
        });

        /// <summary>Saat yönünde ok: 300°'lik yay + uçta üçgen ok başı.</summary>
        private static Sprite RotateIcon => _rotateIcon ??= Make(128, (x, y) =>
        {
            float r     = Mathf.Sqrt(x * x + y * y);
            float ang   = Mathf.Atan2(y, x) * Mathf.Rad2Deg;           // -180..180
            float a     = (ang + 360f) % 360f;                          // 0..360
            const float radius = 0.62f, thick = 0.13f;

            // Yay: 60°..360° arası (sağ üstte boşluk)
            float arc = (a >= 70f) ? Mathf.Clamp01((thick - Mathf.Abs(r - radius)) * 40f) : 0f;

            // Ok başı: yayın 70° ucunda, teğet yönünde (saat yönü = açı azalan)
            float tip = 70f * Mathf.Deg2Rad;
            var   c   = new Vector2(Mathf.Cos(tip), Mathf.Sin(tip)) * radius;
            var   tangent = new Vector2(Mathf.Sin(tip), -Mathf.Cos(tip));  // saat yönü
            var   normal  = new Vector2(Mathf.Cos(tip), Mathf.Sin(tip));
            var   p   = new Vector2(x, y) - c;
            float along  = Vector2.Dot(p, tangent);                      // uca doğru +
            float across = Vector2.Dot(p, normal);
            float head   = 0.30f;
            float inHead = (along > -0.02f && along < head &&
                            Mathf.Abs(across) < (head - along) * 0.95f) ? 1f : 0f;

            return Mathf.Max(arc, inHead);
        });

        private delegate float Alpha(float x, float y);

        private static Sprite Make(int size, Alpha fn)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            var px = new Color[size * size];
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float u = (i + 0.5f) / size * 2f - 1f;
                float v = (j + 0.5f) / size * 2f - 1f;
                px[j * size + i] = new Color(1f, 1f, 1f, fn(u, v));
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
