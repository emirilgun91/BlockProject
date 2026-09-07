using DG.Tweening;
using RogueBlockBlast.UI.FX;
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
        private SpriteRenderer _overlayGlow;   // ikonun ALTINDA — hale / sis
        private SpriteRenderer _overlayRing;   // ikonun ÜSTÜNDE — kalkan çerçevesi
        private float          _iconBaseSize = 1f;
        private float          _overlayPhase;  // hücreye özel faz — tahta tek ağızdan yanıp sönmesin
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
                mr.sortingOrder   = _sr.sortingOrder + 5;
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
            if (_overlayGlow != null) _overlayGlow.enabled = false;
            if (_overlayRing != null) _overlayRing.enabled = false;
        }

        // ── FX'in ihtiyaç duyduğu ölçüler ────────────────────────────────────
        // BoardOverlayFX hücrenin üstünde dünya-uzayı efektleri çizerken tile'ın
        // boyutunu ve sıralamasını bilmek zorunda. Renderer'ı dışarı açmak yerine
        // yalnızca bu üç değeri veriyoruz — FX tarafı tile'ın rengiyle oynayamaz.

        private SpriteRenderer Sr => _sr != null ? _sr : (_sr = GetComponent<SpriteRenderer>());

        public float TileWorldSize   => Mathf.Max(Sr.bounds.size.x, 0.01f);
        public int   FxSortingLayer  => Sr.sortingLayerID;
        public int   FxSortingOrder  => Sr.sortingOrder;
        public Color CurrentColor    => Sr.color;
        public Sprite TileSprite     => Sr.sprite;

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
                if (_overlayGlow != null && _overlayGlow.enabled) _overlayGlow.enabled = false;
                if (_overlayRing != null && _overlayRing.enabled) _overlayRing.enabled = false;
                return;
            }

            EnsureOverlayIcon();

            var tint = GetOverlayTint(_overlayType);

            // Her hücre kendi fazında nefes alsın — aynı anda yanıp sönen iki neon
            // hücresi tahtayı disko ışığına çevirir, kaymış fazda ise canlı durur.
            float t     = Time.time + _overlayPhase;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * PulseSpeed(_overlayType));

            // Zemin: boşken sadece hafif renk ipucu ver — dolu blok gibi görünmesin
            float baseTint = _overlayType is OverlayType.NeonCableA or OverlayType.NeonCableB
                ? (cellFilled ? 0.48f : 0.24f) + 0.10f * pulse   // neon zemini de nabız atsın
                : (cellFilled ? 0.35f : 0.14f);
            _sr.color = Color.Lerp(_sr.color, tint, baseTint);

            _overlayIcon.sprite  = GetOverlayIcon(_overlayType);
            _overlayIcon.enabled = _overlayIcon.sprite != null;

            // Decaying Rift halkasının ortası boş — geri sayım rakamı içinden okunsun diye
            // ikonu biraz büyütüyoruz. Diğerleri hücreyi doldurmasın diye daha küçük.
            float sizeFactor = _overlayType == OverlayType.DecayingRift ? 0.88f : 0.66f;
            float iconAlpha  = cellFilled ? 0.55f : 1f;
            Color iconColor  = tint;
            var   iconLocal  = Vector3.zero;

            switch (_overlayType)
            {
                // ── Neon Cable ───────────────────────────────────────────────
                // Neon tüp mantığı: sabit yüksek parlaklık + nadir, çok kısa bir
                // sönme (Pow ile daralttığımız sinüs tepesi). Sürekli titreme göz
                // yorar; nadir titreme "bu bir neon" der.
                case OverlayType.NeonCableA:
                case OverlayType.NeonCableB:
                {
                    float flicker = 1f - 0.30f * Mathf.Pow(
                        Mathf.Max(0f, Mathf.Sin(t * 21f + _overlayPhase * 5f)), 14f);
                    float bright = (0.80f + 0.20f * pulse) * flicker;

                    // Beyaza doğru çekmek, alpha-blend materyalde HDR olmadan
                    // elde edebileceğimiz en güçlü "parlıyor" sinyali.
                    iconColor  = Color.Lerp(tint, Color.white, 0.55f * bright);
                    iconAlpha  = (cellFilled ? 0.85f : 1f) * bright;
                    sizeFactor = 0.68f + 0.05f * pulse;

                    SetGlow(OverlayFXGraphics.SoftDisc,
                            tint,
                            alpha: (cellFilled ? 0.42f : 0.60f) * bright,
                            scale: _iconBaseSize * (1.55f + 0.30f * pulse),
                            rotation: 0f);
                    HideRing();
                    break;
                }

                // ── Phantom Cell ─────────────────────────────────────────────
                // İki farklı frekansın çarpımı → düzenli bir nabız değil, düzensiz
                // bir "var mı yok mu" nefesi. Sis halesi ters fazda çalışıyor:
                // ikon söndükçe sis kabarıyor, ikon belirdikçe sis çekiliyor.
                case OverlayType.PhantomCell:
                {
                    float breathe = 0.5f + 0.5f * Mathf.Sin(t * 1.7f);
                    float mist    = breathe * (0.62f + 0.38f * Mathf.Sin(t * 0.73f + _overlayPhase));
                    mist = Mathf.Clamp01(0.30f + 0.70f * mist);

                    iconColor  = Color.Lerp(tint, Color.white, 0.25f * mist);
                    iconAlpha  = (cellFilled ? 0.45f : 0.95f) * mist;
                    sizeFactor = 0.64f + 0.04f * (1f - mist);
                    iconLocal  = new Vector3(0f, Mathf.Sin(t * 0.9f) * _iconBaseSize * 0.045f, 0f);

                    SetGlow(OverlayFXGraphics.SoftDisc,
                            tint,
                            alpha: (cellFilled ? 0.18f : 0.30f) + 0.26f * (1f - mist),
                            scale: _iconBaseSize * (1.45f + 0.35f * (1f - mist)),
                            rotation: Mathf.Sin(t * 0.4f) * 25f);
                    HideRing();
                    break;
                }

                // ── Safe Zone ────────────────────────────────────────────────
                // Kalkan hücrenin DIŞINA taşmamalı — komşu hücreleri de koruyormuş
                // gibi okunurdu. Bu yüzden hem halka hem hale tile boyutuyla
                // sınırlı (<= _iconBaseSize) ve nabız yalnızca alt sınırdan büyütüyor.
                case OverlayType.SafeZone:
                {
                    float bright = 0.75f + 0.25f * pulse;

                    iconColor  = Color.Lerp(tint, Color.white, 0.30f * bright);
                    iconAlpha  = (cellFilled ? 0.70f : 1f) * bright;
                    sizeFactor = 0.46f;                       // ikon halkanın içinde kalsın

                    SetRing(OverlayFXGraphics.HexShield,
                            tint,
                            alpha: (cellFilled ? 0.65f : 0.90f) * bright,
                            scale: _iconBaseSize * (0.90f + 0.06f * pulse),   // max 0.96 tile
                            rotation: Time.time * 9f);
                    SetGlow(OverlayFXGraphics.SoftDisc,
                            tint,
                            alpha: 0.14f + 0.10f * pulse,
                            scale: _iconBaseSize * 0.92f,
                            rotation: 0f);
                    break;
                }

                // ── Decaying Rift ────────────────────────────────────────────
                // Geri sayım rakamı tek başına aciliyet taşımıyordu — oyuncu 5'ten
                // 1'e inen bir sayıya bakıp aynı şeyi görüyordu. Son iki adımda
                // nabız hızlanıp hale kabarıyor; rakamı okumadan da fark ediliyor.
                case OverlayType.DecayingRift:
                {
                    int.TryParse(_overlayLabel, out int left);
                    bool  urgent = left > 0 && left <= 2;
                    float speed  = urgent ? (left == 1 ? 9f : 5.5f) : 2.2f;
                    float p      = 0.5f + 0.5f * Mathf.Sin(Time.time * speed + _overlayPhase);

                    iconColor  = Color.Lerp(tint, Color.white, (urgent ? 0.45f : 0.15f) * p);
                    iconAlpha  = (cellFilled ? 0.70f : 1f) * (urgent ? 0.70f + 0.30f * p : 0.9f);
                    sizeFactor = 0.88f + (urgent ? 0.06f * p : 0f);

                    SetGlow(OverlayFXGraphics.SoftDisc,
                            tint,
                            alpha: urgent ? 0.20f + 0.40f * p : 0.16f,
                            scale: _iconBaseSize * (1.10f + (urgent ? 0.45f : 0.15f) * p),
                            rotation: 0f);
                    HideRing();
                    break;
                }

                default:
                    HideGlow();
                    HideRing();
                    break;
            }

            _overlayIcon.color = new Color(iconColor.r, iconColor.g, iconColor.b, iconAlpha);
            _overlayIcon.transform.localScale    = Vector3.one * (_iconBaseSize * sizeFactor);
            _overlayIcon.transform.localPosition = iconLocal;

            // Etiket (rift geri sayımı gibi) ikonun üstünde, overlay renginde okunur
            if (_scoreText != null && !string.IsNullOrEmpty(_overlayLabel))
            {
                _scoreText.text  = _overlayLabel;
                _scoreText.color = Color.Lerp(tint, Color.white, 0.55f);
            }
        }

        private static float PulseSpeed(OverlayType type) => type switch
        {
            OverlayType.NeonCableA or OverlayType.NeonCableB => 5.5f,
            OverlayType.SafeZone                             => 2.6f,
            _                                                => 1.7f,
        };

        private void SetGlow(Sprite sprite, Color tint, float alpha, float scale, float rotation)
        {
            EnsureGlow();
            _overlayGlow.enabled = true;
            _overlayGlow.sprite  = sprite;
            _overlayGlow.color   = new Color(tint.r, tint.g, tint.b, alpha);
            _overlayGlow.transform.localScale    = Vector3.one * scale;
            _overlayGlow.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void SetRing(Sprite sprite, Color tint, float alpha, float scale, float rotation)
        {
            EnsureRing();
            _overlayRing.enabled = true;
            _overlayRing.sprite  = sprite;
            _overlayRing.color   = new Color(tint.r, tint.g, tint.b, alpha);
            _overlayRing.transform.localScale    = Vector3.one * scale;
            _overlayRing.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void HideGlow() { if (_overlayGlow != null) _overlayGlow.enabled = false; }
        private void HideRing() { if (_overlayRing != null) _overlayRing.enabled = false; }

        private void EnsureGlow()
        {
            if (_overlayGlow != null) return;
            _overlayGlow = NewOverlayLayer("OverlayGlow", _sr.sortingOrder + 1);
        }

        private void EnsureRing()
        {
            if (_overlayRing != null) return;
            _overlayRing = NewOverlayLayer("OverlayRing", _sr.sortingOrder + 3);
        }

        private SpriteRenderer NewOverlayLayer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = _sr.sortingLayerID;
            sr.sortingOrder   = order;
            return sr;
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
            _overlayIcon.sortingOrder   = _sr.sortingOrder + 2;

            float tileSize = _sr.bounds.size.x;
            if (tileSize <= 0f) tileSize = 1f;
            _iconBaseSize = tileSize;

            // Faz, hücrenin konumundan türetiliyor: aynı sahnede iki neon hücresi
            // asla senkron yanıp sönmesin, ama karede kare aynı kalsın.
            var p = transform.position;
            _overlayPhase = Mathf.Repeat(p.x * 1.7f + p.y * 2.3f, Mathf.PI * 2f);
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

        /// <summary>Overlay rengini dışarı açar — patlama/kalkan FX'i aynı paleti kullansın.</summary>
        public static Color OverlayTint(OverlayType type) => GetOverlayTint(type);

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