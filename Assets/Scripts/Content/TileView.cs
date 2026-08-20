using DG.Tweening;
using TMPro;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    public enum OverlayType { None, NeonCableA, NeonCableB, SafeZone, DecayingRift, PhantomCell }

    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TileView : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Place FX")]
        [SerializeField] private float _placeScalePunch  = 0.28f;
        [SerializeField] private float _placeDuration    = 0.18f;
        [SerializeField] private int   _placeVibrato     = 1;

        [Header("Clear FX")]
        [SerializeField] private float _clearScaleUp     = 1.18f;
        [SerializeField] private float _clearFadeOut     = 0f;
        [SerializeField] private float _clearDuration    = 0.22f;

        [Header("Restore")]
        [SerializeField] private float _restoreDelay     = 0.22f;

        [Header("Debug")]
        [SerializeField] private TMP_Text _scoreText;  // TilePrefab altındaki ScoreText

        // ── Private ──────────────────────────────────────────────────────────
        private SpriteRenderer _sr;
        private Color          _scoreTextBaseColor = Color.white;
        private Vector3        _baseScale;
        private Tweener        _colorTween;
        private Sequence       _fxSequence;

        // ── Overlay ──────────────────────────────────────────────────────────
        private OverlayType _overlayType  = OverlayType.None;
        private string      _overlayLabel;
        private SpriteRenderer _overlayIcon;
        private float          _iconBaseSize = 1f;
        private TextMeshPro    _bonusLabel;

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_scoreText != null) _scoreTextBaseColor = _scoreText.color;
        }

        private void Start()
        {
            // Fallback — Build() Init'i çağırmadıysa buradan yakala
            if (_baseScale == Vector3.zero)
                _baseScale = transform.localScale;
        }

        /// <summary>
        /// BoardView.Build() tile'ı oluşturduktan hemen sonra bu metodu çağırır.
        /// Scale o an ne ise onu base olarak kilitler.
        /// </summary>
        public void Init()
        {
            _baseScale = transform.localScale;
        }

        private void OnDestroy()
        {
            _fxSequence?.Kill();
            _colorTween?.Kill();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Rengi doğrudan ata (animasyon yok).</summary>
        public void SetColor(Color color)
        {
            _colorTween?.Kill();
            _sr.color = color;

            // Boş hücreye dönünce score text'i temizle
            if (_scoreText != null)
                _scoreText.text = string.Empty;
        }
        public void ClearValue()
        {
            if (_scoreText != null) _scoreText.text = string.Empty;
        }

        /// <summary>
        /// Tile value'yu gösterir.
        /// BoardView.Render() içinde dolu tile'lara çağrılır.
        /// </summary>
        public void SetTileValue(float value)
        {
            if (_scoreText == null) return;
            _scoreText.color = _scoreTextBaseColor;
            _scoreText.text  = value > 0f ? value.ToString("0") : string.Empty;
        }

        /// <summary>
        /// Pozisyon bonuslu ghost hücreler için — değeri vurgulu renkte gösterir.
        /// highlight null ise normal renge döner.
        /// </summary>
        public void SetTileValue(float value, Color? highlight)
        {
            if (_scoreText == null) return;
            _scoreText.color = highlight ?? _scoreTextBaseColor;
            _scoreText.text  = value > 0f ? value.ToString("0") : string.Empty;
        }

        /// <summary>
        /// Boş hücrede duran kalıcı pozisyon bonusu ipucu ("+3").
        /// Corner Stone / Center Base gibi kartlar için: oyuncu şekli sürüklemeden de
        /// hangi hücrenin ekstra puan verdiğini görsün.
        ///
        /// NOT: prefabdaki _scoreText bir Canvas (UGUI) child'ı ve tahta sprite'ının
        /// arkasında kaldığı için ekranda görünmüyor. Bu yüzden ipucu, ikon gibi
        /// çalışma zamanında oluşturulan bir dünya-uzayı TMP etiketiyle çiziliyor.
        /// </summary>
        public void SetBonusHint(float value, Color color)
        {
            if (value <= 0f) { HideBonusHint(); return; }

            EnsureBonusLabel();
            ApplyBonusLabelSize();          // tile boyutu ilk frame'de 0 olabiliyor — her seferinde tazele
            _bonusLabel.text    = "+" + value.ToString("0");
            _bonusLabel.color   = color;
            _bonusLabel.enabled = true;
        }

        public void HideBonusHint()
        {
            if (_bonusLabel != null && _bonusLabel.enabled) _bonusLabel.enabled = false;
        }

        /// <summary>Etiketi hücreye oturt. Punto sabit — autosizing tile'dan tile'a fark yaratıyordu.</summary>
        private void ApplyBonusLabelSize()
        {
            float tileSize = _sr.bounds.size.x;
            if (tileSize <= 0.01f) return;   // henüz hazır değil, bir sonraki frame'de tekrar denenecek

            var rt = _bonusLabel.rectTransform;
            if (Mathf.Approximately(rt.sizeDelta.x, tileSize)) return;   // zaten doğru

            rt.sizeDelta = new Vector2(tileSize, tileSize);
            _bonusLabel.enableAutoSizing = false;
            _bonusLabel.fontSize = tileSize * 4.2f;
        }

        private void EnsureBonusLabel()
        {
            if (_bonusLabel != null) return;

            var go = new GameObject("BonusHint");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            _bonusLabel = go.AddComponent<TextMeshPro>();
            _bonusLabel.alignment = TextAlignmentOptions.Center;
            _bonusLabel.raycastTarget = false;
            _bonusLabel.enableAutoSizing = false;
            if (_scoreText != null && _scoreText.font != null) _bonusLabel.font = _scoreText.font;

            // Blok ve ikonun üstünde çizilsin
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerID = _sr.sortingLayerID;
                mr.sortingOrder   = _sr.sortingOrder + 3;
            }
        }

        /// <summary>
        /// Yerleştirme FX — scale punch.
        /// Renk animasyonu yok — Render() her frame rengi yönetiyor, çakışma olmasın.
        /// </summary>
        public void PlayPlaceFX()
        {
            _fxSequence?.Kill();
            Vector3 current = _baseScale != Vector3.zero ? _baseScale : transform.localScale;

            _fxSequence = DOTween.Sequence()
                .Append(transform
                    .DOScale(current * (1f + _placeScalePunch), _placeDuration * 0.35f)
                    .SetEase(Ease.OutQuad))
                .Append(transform
                    .DOScale(current, _placeDuration * 0.65f)
                    .SetEase(Ease.OutBack))
                .SetAutoKill(true)
                .OnKill(() => transform.localScale = current);
        }

        public void PlayRippleFX(float strength = 0.07f, float duration = 0.18f)
        {
            if (_fxSequence != null && _fxSequence.IsActive()) return;
            Vector3 current = _baseScale != Vector3.zero ? _baseScale : transform.localScale;

            DOTween.Sequence()
                .Append(transform
                    .DOScale(current * (1f + strength), duration * 0.4f)
                    .SetEase(Ease.OutQuad))
                .Append(transform
                    .DOScale(current, duration * 0.6f)
                    .SetEase(Ease.OutQuad))
                .SetAutoKill(true)
                .OnKill(() => transform.localScale = current);
        }

        /// <summary>
        /// Line clear FX — temizlenen hücreler üzerinde çalışır.
        /// Önce hafif büyür, sonra scale+alpha ile solar.
        /// delay: dalga efekti için dışarıdan verilir (x veya y index * offset).
        /// </summary>
        /// <param name="delay">Dalga efekti için gecikme.</param>
        /// <param name="flashColor">Parlama rengi — null ise tile'ın mevcut rengi kullanılır.</param>
        /// <param name="emptyColor">Animasyon sonunda dönülecek boş hücre rengi.</param>
        public void PlayClearFX(float delay = 0f, Color? flashColor = null, Color? emptyColor = null)
        {
            
            _fxSequence?.Kill();

            Color tileColor  = _sr.color;                        // yerleştirilen parçanın rengi
            Color flash      = flashColor ?? tileColor;          // flash → parça rengi veya override
            Color restoreTo  = emptyColor ?? new Color(0x1c / 255f, 0x21 / 255f, 0x32 / 255f, 1f);

            _fxSequence = DOTween.Sequence();
            _fxSequence
                // 1. flash rengi — parlama
                .AppendCallback(() => _sr.color = flash)
                // 2. büyü
                .Append(transform
                    .DOScale(_baseScale * _clearScaleUp, _clearDuration * 0.35f)
                    .SetEase(Ease.OutCubic))
                // 3. parça rengine geri dön + küçül
                .Append(DOTween.Sequence()
                    .Join(_sr.DOColor(tileColor, _clearDuration * 0.30f)
                        .SetEase(Ease.InQuad))
                    .Join(transform
                        .DOScale(_baseScale * 0.85f, _clearDuration * 0.30f)
                        .SetEase(Ease.InQuad)))
                // 4. boş renge fade + scale normale dön
                .Append(DOTween.Sequence()
                    .Join(_sr.DOColor(restoreTo, _clearDuration * 0.35f)
                        .SetEase(Ease.OutQuad))
                    .Join(transform
                        .DOScale(_baseScale, _clearDuration * 0.35f)
                        .SetEase(Ease.OutBack)))
                .SetDelay(delay)
                .SetAutoKill(true)
                .OnKill(() =>
                {
                    transform.localScale = _baseScale;
                    _sr.color = restoreTo;
                })
                .SetUpdate(false);
        }
        public void RefreshBaseScale()
        {
            if (transform.localScale != Vector3.zero)
                _baseScale = transform.localScale;
        }
        // ── Overlay API ──────────────────────────────────────────────────────

        public void SetOverlay(OverlayType type, string label)
        {
            _overlayType  = type;
            _overlayLabel = label;
        }

        public void ClearOverlay()
        {
            _overlayType  = OverlayType.None;
            _overlayLabel = null;

            // İkon da hemen sönmeli: BoardView her frame tüm hücreler için ClearOverlay
            // çağırıp yalnızca overlay'li olanlar için ApplyOverlayVisual çağırıyor.
            // Burada kapatmazsak eski ikon tahtada asılı kalır.
            if (_overlayIcon != null) _overlayIcon.enabled = false;
        }

        /// <summary>
        /// Called by BoardView.Render() AFTER color/value are set.
        ///
        /// Özel hücreler artık renk tintiyle değil, kendi ikonlarıyla gösterilir
        /// (Resources/OverlayIcons). Hücrenin DOLU olup olmaması görsel olarak
        /// ayrışır: boş hücre koyu zemin + parlak ikon, dolu hücre blok rengi +
        /// sönük ikon. Böylece oyuncu "buraya parça konabilir mi" sorusunu
        /// bakar bakmaz anlar.
        /// </summary>
        public void ApplyOverlayVisual(bool cellFilled = false)
        {
            if (_overlayType == OverlayType.None)
            {
                if (_overlayIcon != null && _overlayIcon.enabled) _overlayIcon.enabled = false;
                return;
            }

            EnsureOverlayIcon();

            var tint = GetOverlayTint(_overlayType);

            // Zemin: boşken sadece hafif renk ipucu ver — dolu blok gibi görünmesin
            _sr.color = Color.Lerp(_sr.color, tint, cellFilled ? 0.35f : 0.14f);

            _overlayIcon.sprite  = GetOverlayIcon(_overlayType);
            _overlayIcon.enabled = _overlayIcon.sprite != null;
            _overlayIcon.color   = new Color(tint.r, tint.g, tint.b, cellFilled ? 0.55f : 1f);

            // Decaying Rift halkasının ortası boş — geri sayım rakamı içinden okunsun diye
            // ikonu biraz büyütüyoruz. Diğerleri hücreyi doldurmasın diye daha küçük.
            float sizeFactor = _overlayType == OverlayType.DecayingRift ? 0.88f : 0.66f;
            _overlayIcon.transform.localScale = Vector3.one * (_iconBaseSize * sizeFactor);

            // Etiket (rift geri sayımı gibi) ikonun üstünde, overlay renginde okunur
            if (_scoreText != null && !string.IsNullOrEmpty(_overlayLabel))
            {
                _scoreText.text  = _overlayLabel;
                _scoreText.color = Color.Lerp(tint, Color.white, 0.55f);
            }
        }

        /// <summary>İkon SpriteRenderer'ı yoksa çalışma zamanında oluşturur — prefab düzenlemek gerekmez.</summary>
        private void EnsureOverlayIcon()
        {
            if (_overlayIcon != null) return;

            var go = new GameObject("OverlayIcon");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            _overlayIcon = go.AddComponent<SpriteRenderer>();
            // Blok sprite'ının hemen üstü. Skor yazısı ayrı (daha yüksek) sorting layer'da
            // olduğu için rakam ikonun üzerinde kalmaya devam eder.
            _overlayIcon.sortingLayerID = _sr.sortingLayerID;
            _overlayIcon.sortingOrder   = _sr.sortingOrder + 1;

            float tileSize = _sr.bounds.size.x;
            if (tileSize <= 0f) tileSize = 1f;
            _iconBaseSize = tileSize;
        }

        private static Sprite GetOverlayIcon(OverlayType type)
        {
            string name = type switch
            {
                OverlayType.SafeZone     => "Icon_SafeZone",
                OverlayType.PhantomCell  => "Icon_PhantomCell",
                OverlayType.DecayingRift => "Icon_DecayingRift",
                OverlayType.NeonCableA   => "Icon_NeonCableA",
                OverlayType.NeonCableB   => "Icon_NeonCableB",
                _                        => null,
            };
            if (name == null) return null;

            if (_iconCache.TryGetValue(name, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>("OverlayIcons/" + name);
            if (sprite == null)
                Debug.LogWarning($"[TileView] Overlay ikonu bulunamadı: Resources/OverlayIcons/{name}");
            _iconCache[name] = sprite;
            return sprite;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _iconCache = new();

        private static Color GetOverlayTint(OverlayType type) => type switch
        {
            OverlayType.NeonCableA   => new Color(0f,    1f,   0.75f),
            OverlayType.NeonCableB   => new Color(0.2f, 0.77f,  1f),
            OverlayType.SafeZone     => new Color(0.23f, 1f,   0.48f),
            OverlayType.DecayingRift => new Color(1f,   0.30f, 0.18f),
            OverlayType.PhantomCell  => new Color(0.72f, 0.30f, 1f),
            _                        => Color.white,
        };

        /// <summary>Clear FX sonrası hücreyi boş renge döndürür.</summary>
        public void RestoreEmpty(Color emptyColor)
        {
            _fxSequence?.Kill();
            transform.localScale = _baseScale;

            _colorTween?.Kill();
            _colorTween = _sr
                .DOColor(emptyColor, 0.12f)
                .SetEase(Ease.OutQuad);
        }
    }
}