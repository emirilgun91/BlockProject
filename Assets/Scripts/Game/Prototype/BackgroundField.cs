using System;
using RogueBlockBlast.Content;
using UnityEngine;

namespace RogueBlockBlast.Game.Prototype
{
    /// <summary>
    /// Katmanlı, hareketli, komboya tepki veren arka plan.
    ///
    /// Kurgu dört fikre dayanıyor:
    ///
    /// <b>1. Boşluk hissini bitiren şey gradyan, blok değil.</b> Sahnenin arkası
    /// saf siyah olduğu için her şey "void"de yüzüyordu. En arkaya kod içinde
    /// üretilen dikey gradyanlı bir zemin konuyor.
    ///
    /// <b>2. Derinlik bedava.</b> Perspektif kamera, idle sway ve mouse parallax
    /// hazır; blokları farklı uzaklıklara koymak parallax'ı kendiliğinden verir.
    ///
    /// <b>3. Arka plan oyunun kendi dilini konuşur.</b> Elemanlar tek kare değil,
    /// oyunun parçalarına benzeyen küçük şekiller (domino, kare, üçlü, köşe, L)
    /// ve renkleri <see cref="BlockColorPalette"/>'ten geliyor — uydurma bir
    /// palet değil, tahtadaki renklerin ta kendisi.
    ///
    /// <b>4. Alan komboyla renklenip solar.</b> Isı düşükken elemanlar soğuk ve
    /// donuk (griye yakın); ısı yükseldikçe kendi palet renklerine açarlar.
    /// Hepsi turuncuya kaymaz — her eleman kendi rengine açar, böylece yüksek
    /// komboda alan çok renkli olur.
    ///
    /// Isı <see cref="BoardAtmosphere.Heat"/>'ten okunur.
    /// </summary>
    public sealed class BackgroundField : MonoBehaviour
    {
        [Serializable]
        public sealed class Layer
        {
            public string  Name       = "Layer";
            [Tooltip("Kameradan uzaklık. Büyük = daha yavaş parallax, daha küçük görünür.")]
            public float   Distance   = 30f;
            [Tooltip("Eleman sayısı. Her eleman 1–4 hücreli bir şekil.")]
            public int     Count      = 40;
            [Tooltip("Tek hücrenin dünya boyutu aralığı.")]
            public Vector2 CellSize   = new Vector2(0.5f, 1.1f);
            [Tooltip("Yatay sürüklenme hızı (birim/sn). Eksi = sola.")]
            public float   DriftSpeed = -0.10f;
            [Range(0f, 1f)]
            public float   Alpha      = 0.09f;
            [Tooltip("Yanıp sönme hızı. 0 = sabit.")]
            public float   BlinkSpeed = 0.22f;
            [Tooltip("Dönme hızı (derece/sn). Uzak katmanlarda düşük tutun.")]
            public float   SpinSpeed  = 0f;
        }

        [Header("Bağlantılar")]
        [SerializeField] private Camera          _camera;
        [SerializeField] private BoardAtmosphere _atmosphere;

        [Tooltip("Hücre sprite'ı. Boşsa tahtanın tile sprite'ı kullanılır.")]
        [SerializeField] private Sprite _blockSprite;

        [Header("Zemin gradyanı")]
        [SerializeField] private bool  _drawBackdrop     = true;
        [SerializeField] private float _backdropDistance = 60f;
        [SerializeField] private Color _backdropTop      = new Color(0.10f, 0.13f, 0.24f, 1f);
        [SerializeField] private Color _backdropBottom   = new Color(0.02f, 0.03f, 0.07f, 1f);
        [Tooltip("Isı 1'de zemine binen sıcak ton.")]
        [SerializeField] private Color _backdropHotTint  = new Color(0.22f, 0.12f, 0.15f, 1f);

        [Header("Katmanlar")]
        [SerializeField] private Layer[] _layers =
        {
            new Layer { Name = "Far",  Distance = 42f, Count = 40, CellSize = new Vector2(0.35f, 0.7f),
                        DriftSpeed = -0.06f, Alpha = 0.07f, BlinkSpeed = 0.16f, SpinSpeed = 0f },

            new Layer { Name = "Mid",  Distance = 28f, Count = 26, CellSize = new Vector2(0.6f, 1.1f),
                        DriftSpeed = -0.14f, Alpha = 0.09f, BlinkSpeed = 0.24f, SpinSpeed = 1.5f },

            // Tahta kameradan ~16.3 birim uzakta; en yakın katman ondan geride
            // tutulur ki ölçek olarak tahtayla yarışmasın.
            new Layer { Name = "Near", Distance = 20f, Count = 10, CellSize = new Vector2(1.0f, 1.7f),
                        DriftSpeed = -0.26f, Alpha = 0.05f, BlinkSpeed = 0.32f, SpinSpeed = 3f },
        };

        [Header("Renklenme")]
        [Tooltip("Isı 0'da elemanların kayacağı soğuk ton — alan burada donuklaşır.")]
        [SerializeField] private Color _coldTint = new Color(0.30f, 0.42f, 0.72f, 1f);

        [Tooltip("Isı 0'da palet renginin ne kadar soğuğa çekileceği. 1 = tamamen donuk.")]
        [Range(0f, 1f)]
        [SerializeField] private float _coldDesaturation = 0.85f;

        [Tooltip("Isı 1'de parlaklığın katı.")]
        [SerializeField] private float _hotAlphaFactor = 1.9f;

        [Tooltip("Isı 1'de sürüklenme hızının katı.")]
        [SerializeField] private float _hotSpeedFactor = 2.0f;

        [Header("Sıralama")]
        [SerializeField] private string _sortingLayer = "Background";
        [SerializeField] private int    _sortingOrder = -50;

        // ── Şekiller ─────────────────────────────────────────────────────────
        /// <summary>
        /// Oyunun parçalarına benzeyen küçük şekiller. Hücre ofsetleri
        /// (birim = o katmanın hücre boyutu).
        /// </summary>
        private static readonly Vector2Int[][] Shapes =
        {
            new[] { new Vector2Int(0, 0) },                                                        // tek
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) },                                  // domino
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) }, // kare
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },            // üçlü
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },            // köşe
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2) }, // L
        };

        private static readonly BlockColorPreset[] Presets =
        {
            BlockColorPreset.Crimson, BlockColorPreset.Amber, BlockColorPreset.Teal,
            BlockColorPreset.Indigo,  BlockColorPreset.Violet, BlockColorPreset.Slate,
            BlockColorPreset.Greeny,  BlockColorPreset.Pinky,
        };

        // ── Runtime ──────────────────────────────────────────────────────────
        private struct Element
        {
            public Transform        T;
            public SpriteRenderer[] Cells;
            public int              LayerIndex;
            public Color            PaletteColor;
            public Color            ColdColor;
            public float            BlinkPhase;
            public float            SpinOffset;
            public float            HalfExtentX;
        }

        private Element[]      _elements;
        private SpriteRenderer _backdrop;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Start()
        {
            if (_camera == null)     _camera     = Camera.main;
            if (_atmosphere == null) _atmosphere = FindFirstObjectByType<BoardAtmosphere>();

            if (_camera == null)
            {
                Debug.LogError("[Background] Kamera bulunamadı — arka plan kurulmadı.");
                enabled = false;
                return;
            }

            if (_blockSprite == null) _blockSprite = ResolveFallbackSprite();

            if (_drawBackdrop) BuildBackdrop();

            if (_blockSprite != null) BuildLayers();
            else Debug.LogWarning("[Background] Hücre sprite'ı yok — yalnızca zemin gradyanı çizildi.");
        }

        private void OnDestroy()
        {
            // Kod içinde üretilen doku ve sprite GC'ye takılmaz — elle atılır.
            if (_backdrop == null || _backdrop.sprite == null) return;

            var tex = _backdrop.sprite.texture;
            Destroy(_backdrop.sprite);
            if (tex != null) Destroy(tex);
        }

        private void Update()
        {
            float heat = _atmosphere != null ? Mathf.Clamp01(_atmosphere.Heat) : 0f;
            float dt   = Time.unscaledDeltaTime;

            UpdateBackdrop(heat);
            UpdateElements(heat, dt);
        }

        // ── Zemin ────────────────────────────────────────────────────────────

        private void BuildBackdrop()
        {
            var tex = new Texture2D(2, 256, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name       = "BackdropGradient"
            };

            for (int y = 0; y < tex.height; y++)
            {
                float t = y / (float)(tex.height - 1);
                var   c = Color.Lerp(_backdropBottom, _backdropTop, t);
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply();

            var sprite = Sprite.Create(
                tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

            var go = new GameObject("Backdrop");
            go.transform.SetParent(transform, worldPositionStays: false);

            _backdrop = go.AddComponent<SpriteRenderer>();
            _backdrop.sprite = sprite;
            ApplySorting(_backdrop, _sortingOrder - 10);

            PlaceOnCameraPlane(go.transform, _backdropDistance);

            GetFrustumSize(_backdropDistance, out float w, out float h);
            var s = sprite.bounds.size;
            go.transform.localScale = new Vector3(
                w * 1.35f / Mathf.Max(0.0001f, s.x),
                h * 1.35f / Mathf.Max(0.0001f, s.y),
                1f);
        }

        private void UpdateBackdrop(float heat)
        {
            if (_backdrop == null) return;
            _backdrop.color = Color.Lerp(Color.white, _backdropHotTint * 2.2f, heat * 0.35f);
        }

        // ── Elemanlar ────────────────────────────────────────────────────────

        private void BuildLayers()
        {
            if (_layers == null || _layers.Length == 0)
            {
                _elements = Array.Empty<Element>();
                return;
            }

            int total = 0;
            foreach (var l in _layers) total += Mathf.Max(0, l.Count);

            _elements = new Element[total];
            int index = 0;

            var spriteBounds = _blockSprite.bounds.size;

            for (int li = 0; li < _layers.Length; li++)
            {
                var layer = _layers[li];
                GetFrustumSize(layer.Distance, out float w, out float h);

                float halfX = w * 0.6f;   // kenar boşluğu — wrap ekran dışında olsun
                float halfY = h * 0.6f;

                var layerRoot = new GameObject($"Layer_{layer.Name}");
                layerRoot.transform.SetParent(transform, worldPositionStays: false);

                for (int i = 0; i < layer.Count; i++)
                {
                    var shape = Shapes[UnityEngine.Random.Range(0, Shapes.Length)];
                    float cell = UnityEngine.Random.Range(layer.CellSize.x, layer.CellSize.y);

                    var preset  = Presets[UnityEngine.Random.Range(0, Presets.Length)];
                    var palette = BlockColorPalette.GetColor(preset);

                    var root = new GameObject($"El_{i}");
                    root.transform.SetParent(layerRoot.transform, worldPositionStays: false);

                    var cells = new SpriteRenderer[shape.Length];

                    for (int c = 0; c < shape.Length; c++)
                    {
                        var cellGo = new GameObject($"C{c}");
                        cellGo.transform.SetParent(root.transform, worldPositionStays: false);

                        var sr = cellGo.AddComponent<SpriteRenderer>();
                        sr.sprite = _blockSprite;
                        ApplySorting(sr, _sortingOrder + li);

                        // Hücreler arası küçük boşluk — tek blok gibi değil,
                        // parçadan oluşmuş gibi okunsun.
                        cellGo.transform.localPosition = new Vector3(
                            shape[c].x * cell * 1.08f,
                            shape[c].y * cell * 1.08f,
                            0f);

                        cellGo.transform.localScale = new Vector3(
                            cell / Mathf.Max(0.0001f, spriteBounds.x),
                            cell / Mathf.Max(0.0001f, spriteBounds.y),
                            1f);

                        cells[c] = sr;
                    }

                    root.transform.position = CameraPlanePoint(
                        layer.Distance,
                        UnityEngine.Random.Range(-halfX, halfX),
                        UnityEngine.Random.Range(-halfY, halfY));

                    root.transform.rotation = _camera.transform.rotation *
                                              Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

                    _elements[index++] = new Element
                    {
                        T            = root.transform,
                        Cells        = cells,
                        LayerIndex   = li,
                        PaletteColor = palette,
                        ColdColor    = ToCold(palette),
                        BlinkPhase   = UnityEngine.Random.value * Mathf.PI * 2f,
                        SpinOffset   = UnityEngine.Random.Range(0f, 360f),
                        HalfExtentX  = halfX
                    };
                }
            }
        }

        private void UpdateElements(float heat, float dt)
        {
            if (_elements == null || _elements.Length == 0) return;

            Vector3 right = _camera.transform.right;
            float   t     = Time.unscaledTime;

            for (int i = 0; i < _elements.Length; i++)
            {
                ref var e     = ref _elements[i];
                if (e.T == null) continue;
                var layer = _layers[e.LayerIndex];

                // Sürüklenme — kameranın sağ ekseninde, ısıyla hızlanır.
                float speed = layer.DriftSpeed * Mathf.Lerp(1f, _hotSpeedFactor, heat);
                e.T.position += right * (speed * dt);

                // Sınırı aşınca karşı kenardan gir.
                Vector3 local = e.T.position - CameraPlanePoint(layer.Distance, 0f, 0f);
                float   x     = Vector3.Dot(local, right);

                if (x >  e.HalfExtentX) e.T.position -= right * (e.HalfExtentX * 2f);
                if (x < -e.HalfExtentX) e.T.position += right * (e.HalfExtentX * 2f);

                if (layer.SpinSpeed != 0f)
                {
                    e.T.rotation = _camera.transform.rotation *
                                   Quaternion.Euler(0f, 0f, e.SpinOffset + t * layer.SpinSpeed);
                }

                // Yavaş yanıp sönme — "akan doku" değil "yaşayan alan" hissi.
                float blink = layer.BlinkSpeed > 0f
                    ? Mathf.Sin(t * layer.BlinkSpeed + e.BlinkPhase) * 0.5f + 0.5f
                    : 1f;

                // Renklenme: soğukta donuk, ısındıkça kendi palet rengine açar.
                // Hepsi aynı sıcak tona kaymaz — yüksek komboda alan çok renkli olur.
                Color tint = Color.Lerp(e.ColdColor, e.PaletteColor, heat);

                float alpha = layer.Alpha
                              * Mathf.Lerp(0.45f, 1f, blink)
                              * Mathf.Lerp(1f, _hotAlphaFactor, heat);

                var final = new Color(tint.r, tint.g, tint.b, alpha);
                for (int c = 0; c < e.Cells.Length; c++)
                    if (e.Cells[c] != null) e.Cells[c].color = final;
            }
        }

        /// <summary>
        /// Palet renginin "soğuk" hâli: parlaklığı korunur ama rengi tek bir
        /// soğuk tona çekilir. Isı 0'da alan böylece donuk ve tek renge yakın
        /// görünür — renk, kombo geldikçe geri gelir.
        /// </summary>
        private Color ToCold(Color c)
        {
            float lum = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            var   grey = new Color(lum, lum, lum, 1f);
            var   cool = Color.Lerp(grey, _coldTint, 0.7f);
            return Color.Lerp(c, cool, _coldDesaturation);
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private Vector3 CameraPlanePoint(float distance, float offsetX, float offsetY)
        {
            var cam = _camera.transform;
            return cam.position
                   + cam.forward * distance
                   + cam.right   * offsetX
                   + cam.up      * offsetY;
        }

        private void PlaceOnCameraPlane(Transform t, float distance)
        {
            t.position = CameraPlanePoint(distance, 0f, 0f);
            t.rotation = _camera.transform.rotation;
        }

        private void GetFrustumSize(float distance, out float width, out float height)
        {
            height = 2f * distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            width  = height * _camera.aspect;
        }

        private void ApplySorting(SpriteRenderer sr, int order)
        {
            if (!string.IsNullOrEmpty(_sortingLayer))
                sr.sortingLayerName = _sortingLayer;
            sr.sortingOrder = order;
        }

        /// <summary>
        /// Hücre sprite'ı verilmediyse tahtanın kendi tile sprite'ı kullanılır —
        /// arka plan böylece oyunla aynı dilden konuşur.
        /// </summary>
        private Sprite ResolveFallbackSprite()
        {
            var board = FindFirstObjectByType<UI.BoardView>();
            if (board == null || board.TilePrefab == null) return null;

            var sr = board.TilePrefab.GetComponent<SpriteRenderer>();
            return sr != null ? sr.sprite : null;
        }
    }
}
