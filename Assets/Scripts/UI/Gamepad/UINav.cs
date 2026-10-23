using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Gamepad ile menü gezinmesi.
    ///
    /// Her panel açılırken bir <b>kapsam</b> (scope) açar: kökü, ilk seçilecek
    /// kontrolü ve B'nin ne yapacağı. En üstteki kapsam "odakta"dır:
    ///   • gamepad modunda EventSystem'de hep o kapsamın içinden bir kontrol seçilidir;
    ///   • seçim kapsamın dışına kaçarsa (otomatik gezinme arkadaki HUD butonuna
    ///     atladıysa) son geçerli seçime döner — panel bir "tuzak" gibi çalışır;
    ///   • B, kapsamın geri eylemini çağırır;
    ///   • panel kapanınca (kök pasifleşince) kapsam kendiliğinden düşer ve alttaki
    ///     panelde en son seçili kontrol geri gelir.
    ///
    /// Seçili kontrolün etrafına bir odak çerçevesi çizilir: projedeki birçok
    /// buton yalnızca fare hover'ında renk değiştiriyor, çerçeve olmadan
    /// gamepad oyuncusu neyin seçili olduğunu göremiyordu. Kaydırılabilir
    /// listelerde (Ayarlar, Shape Shop) seçim görünür alana kaydırılır.
    ///
    /// "Manual" kapsam: kendi gamepad girişini okuyan ekranlar (fiziksel kart
    /// seçimi) için. Seçim boş tutulur ve çerçeve çizilmez — A'yı ekranın kendisi işler.
    ///
    /// Kapsam yokken bu bileşen seçime dokunmaz; oyun sahnesinde A'yı
    /// RunController sahiplenir ve seçimi kendisi boşaltır.
    /// </summary>
    public sealed class UINav : MonoBehaviour
    {
        private sealed class Scope
        {
            public GameObject Root;
            public Selectable First;
            public Action     Back;
            public bool       Manual;
            public Selectable Last;
        }

        private static readonly List<Scope> _scopes = new List<Scope>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _scopes.Clear();
            _stableTopRoot = null;
        }

        // ── API ──────────────────────────────────────────────────────────────

        /// <summary>
        /// <paramref name="root"/> altındaki paneli odağa alır. Aynı kök zaten
        /// varsa en üste taşınır; verilen (null olmayan) değerler eskisinin yerine geçer.
        /// </summary>
        /// <param name="first">İlk seçilecek kontrol. Null → kökün altındaki ilk uygun kontrol.</param>
        /// <param name="back">B tuşu. Null → B bu panelde bir şey yapmaz.</param>
        /// <param name="manual">Panel gamepad'i kendisi okuyor; seçim boş tutulur.</param>
        public static void Push(GameObject root, Selectable first = null, Action back = null, bool manual = false)
        {
            if (root == null) return;

            var scope = Find(root);
            if (scope != null) _scopes.Remove(scope);
            else scope = new Scope { Root = root };

            if (first != null) scope.First = first;
            if (back  != null) scope.Back  = back;
            scope.Manual = manual;
            scope.Last   = null;     // yeniden açılan panel baştan başlar
            _scopes.Add(scope);

            if (InputMode.UsingGamepad) Refocus(scope);
        }

        /// <summary>
        /// Kökün kendisi ya da altındaki bir panel zaten kapsamdaysa hiçbir şey
        /// yapmaz, değilse <see cref="Push"/>. Genel panel yöneticileri için:
        /// panelin kendi kontrolcüsü (OnEnable'da) daha ayrıntılı bir kapsam
        /// açtıysa onun yerine geçmesin.
        /// </summary>
        public static void PushIfAbsent(GameObject root, Selectable first = null, Action back = null)
        {
            if (root == null) return;
            foreach (var s in _scopes)
                if (s.Root != null && s.Root.transform.IsChildOf(root.transform)) return;
            Push(root, first, back);
        }

        /// <summary>
        /// Paneli (ve içinde açılmış kapsamları) çıkarır. Kök pasifleşince zaten
        /// kendiliğinden düşer; fade-out süren paneller için hemen çağrılır.
        /// </summary>
        public static void Pop(GameObject root)
        {
            if (root == null) return;
            for (int i = _scopes.Count - 1; i >= 0; i--)
            {
                var r = _scopes[i].Root;
                if (r == null || r.transform.IsChildOf(root.transform)) _scopes.RemoveAt(i);
            }
        }

        /// <summary>Açık bir menü kapsamı var mı? (Oyun sahnesi A'yı yalnızca yokken sahiplenir.)</summary>
        public static bool HasScope
        {
            get { Prune(); return _scopes.Count > 0; }
        }

        /// <summary>
        /// Bu kökün kapsamı en üstte mi — kendi gamepad girişini okuyan ekranlar için.
        /// Bir önceki karenin sonunda da üstte olmalı: pause'u kapatan A, aynı karede
        /// alttaki ekranda bir de kart seçiyordu.
        /// </summary>
        public static bool IsTop(GameObject root)
        {
            Prune();
            return Top != null && Top.Root == root && _stableTopRoot == root;
        }

        private static GameObject _stableTopRoot;   // LateUpdate'te yazılır: önceki karenin sonu

        private static Scope Find(GameObject root)
        {
            for (int i = 0; i < _scopes.Count; i++)
                if (_scopes[i].Root == root) return _scopes[i];
            return null;
        }

        private static Scope Top => _scopes.Count > 0 ? _scopes[_scopes.Count - 1] : null;

        private static void Prune()
        {
            for (int i = _scopes.Count - 1; i >= 0; i--)
            {
                var root = _scopes[i].Root;
                if (root == null || !root.activeInHierarchy) _scopes.RemoveAt(i);
            }
        }

        // ── Döngü ────────────────────────────────────────────────────────────

        private void Update()
        {
            // B: odaktaki panelin geri eylemi. Çağrı paneli kapatıp listeyi
            // değiştirebileceği için önce kapsam alınır.
            if (!InputMode.Pressed(PadButton.East)) return;
            Prune();
            Top?.Back?.Invoke();
        }

        private void LateUpdate()
        {
            Prune();
            var es  = EventSystem.current;
            var top = Top;
            _stableTopRoot = top?.Root;

            if (!InputMode.UsingGamepad || es == null || top == null)
            {
                Ring.Hide();
                return;
            }

            if (top.Manual)
            {
                if (es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
                Ring.Hide();
                return;
            }

            var current = es.currentSelectedGameObject;
            if (!IsValid(current, top))
            {
                Refocus(top);
                current = es.currentSelectedGameObject;
            }

            if (current != null) top.Last = current.GetComponent<Selectable>();
            KeepInView(current);
            Ring.Follow(current);
        }

        private static void Refocus(Scope scope)
        {
            var es = EventSystem.current;
            if (es == null) return;
            if (scope.Manual) { es.SetSelectedGameObject(null); return; }

            Selectable target = null;
            if (IsValid(scope.Last))  target = scope.Last;
            else if (IsValid(scope.First)) target = scope.First;
            else target = FirstInside(scope.Root);

            // Panel henüz fade-in'deyken (CanvasGroup.interactable kapalı) hiçbir kontrol
            // uygun değil: seçim boş kalır, fade bitince bir sonraki karede yapılır.
            es.SetSelectedGameObject(target != null ? target.gameObject : null);
        }

        private static bool IsValid(GameObject go, Scope scope)
        {
            if (go == null || !go.transform.IsChildOf(scope.Root.transform)) return false;
            return IsValid(go.GetComponent<Selectable>());
        }

        private static bool IsValid(Selectable s) =>
            s != null && s.isActiveAndEnabled && s.IsInteractable() &&
            s.navigation.mode != Navigation.Mode.None;

        private static readonly List<Selectable> _buffer = new List<Selectable>();

        /// <summary>
        /// Kökün altında ekranda en üstteki (eşitse en soldaki) uygun kontrol.
        /// Geri / kapat butonları yalnızca başka seçenek yoksa seçilir — panel
        /// açılır açılmaz A'ya basan oyuncu paneli kapatmasın.
        /// </summary>
        private static Selectable FirstInside(GameObject root)
        {
            root.GetComponentsInChildren(false, _buffer);
            Selectable best = null, fallback = null;
            Vector2 bestPos = default;
            foreach (var s in _buffer)
            {
                if (!IsValid(s)) continue;
                string n = s.gameObject.name;
                bool isBack = n.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              n.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isBack) { if (fallback == null) fallback = s; continue; }

                Vector2 p = ScreenCenter((RectTransform)s.transform);
                if (best == null || p.y > bestPos.y + 1f ||
                    (Mathf.Abs(p.y - bestPos.y) <= 1f && p.x < bestPos.x))
                {
                    best = s; bestPos = p;
                }
            }
            _buffer.Clear();
            return best != null ? best : fallback;
        }

        private static Vector2 ScreenCenter(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (canvas.worldCamera != null ? canvas.worldCamera : Camera.main)
                : null;
            return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
        }

        // ── Kaydırma ─────────────────────────────────────────────────────────

        /// <summary>
        /// Seçili kontrol bir ScrollRect içindeyse görünür alana yumuşakça kaydırır.
        /// Her karede çalışır: içerik layout'la yeniden boyutlansa da seçim görünür kalır.
        /// </summary>
        private static void KeepInView(GameObject selected)
        {
            if (selected == null) return;
            var scroll = selected.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null) return;

            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var item     = (RectTransform)selected.transform;

            Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, item);
            Rect   v = viewport.rect;
            const float margin = 24f;

            float dx = 0f, dy = 0f;
            if (scroll.vertical)
            {
                if (b.max.y + margin > v.yMax)      dy = b.max.y + margin - v.yMax;
                else if (b.min.y - margin < v.yMin) dy = b.min.y - margin - v.yMin;
            }
            if (scroll.horizontal)
            {
                if (b.max.x + margin > v.xMax)      dx = b.max.x + margin - v.xMax;
                else if (b.min.x - margin < v.xMin) dx = b.min.x - margin - v.xMin;
            }
            if (Mathf.Abs(dx) < 0.5f && Mathf.Abs(dy) < 0.5f) return;

            float t = Mathf.Clamp01(Time.unscaledDeltaTime * 14f);
            scroll.velocity = Vector2.zero;
            scroll.content.anchoredPosition -= new Vector2(dx, dy) * t;
        }

        // ── Odak çerçevesi ───────────────────────────────────────────────────

        /// <summary>
        /// Seçili kontrolün etrafındaki nabız atan çerçeve. Her şeyin üstündeki
        /// kendi overlay canvas'ında çizilir; dünya-uzayı canvas'lardaki
        /// kontrollerin de ekran dikdörtgeni hesaplanır.
        /// </summary>
        private static class Ring
        {
            private static RectTransform _rect;
            private static Image         _image;
            private static Rect          _current;
            private static readonly Vector3[] _corners = new Vector3[4];

            public static void Hide()
            {
                if (_image != null) _image.enabled = false;
            }

            public static void Follow(GameObject selected)
            {
                if (selected == null || !(selected.transform is RectTransform target)) { Hide(); return; }
                Ensure();

                var canvas = target.GetComponentInParent<Canvas>();
                if (canvas == null) { Hide(); return; }
                Camera cam = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);

                target.GetWorldCorners(_corners);
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, _corners[i]);
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }

                float pad = 6f * Screen.height / 1080f;
                var goal = Rect.MinMaxRect(min.x - pad, min.y - pad, max.x + pad, max.y + pad);

                // Seçim değişince çerçeve yeni hedefe kayarak gider; aynı hedefte animasyonunu izler.
                _current = !_image.enabled
                    ? goal
                    : LerpRect(_current, goal, Mathf.Clamp01(Time.unscaledDeltaTime * 20f));

                _rect.anchoredPosition = _current.position;
                _rect.sizeDelta        = _current.size;

                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
                var c = UiTheme.Accent;
                _image.color   = new Color(c.r, c.g, c.b, pulse);
                _image.enabled = true;
            }

            private static Rect LerpRect(Rect a, Rect b, float t) =>
                Rect.MinMaxRect(Mathf.Lerp(a.xMin, b.xMin, t), Mathf.Lerp(a.yMin, b.yMin, t),
                                Mathf.Lerp(a.xMax, b.xMax, t), Mathf.Lerp(a.yMax, b.yMax, t));

            private static void Ensure()
            {
                if (_rect != null) return;

                var go = new GameObject("[Gamepad Focus Ring]");
                DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;   // zafer ekranı (700) dahil her şeyin üstünde

                var ringGo = new GameObject("Ring", typeof(RectTransform));
                _rect = (RectTransform)ringGo.transform;
                _rect.SetParent(go.transform, false);
                // Ölçeksiz overlay canvas: 1 birim = 1 piksel, sol-alt köşeye göre.
                _rect.anchorMin = _rect.anchorMax = _rect.pivot = Vector2.zero;

                _image = ringGo.AddComponent<Image>();
                _image.sprite        = BuildSprite();
                _image.type          = Image.Type.Sliced;
                _image.raycastTarget = false;
                _image.enabled       = false;
            }

            /// <summary>Yuvarlak köşeli, içi boş çerçeve — kodda üretilir.</summary>
            private static Sprite BuildSprite()
            {
                const int size = 64; const float radius = 16f; const float thickness = 4f; const int border = 24;
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
                    float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + thickness + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
                tex.SetPixels32(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                     SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            }
        }
    }
}
