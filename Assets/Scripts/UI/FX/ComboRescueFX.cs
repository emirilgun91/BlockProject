using RogueBlockBlast.UI.Juice;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI.FX
{
    /// <summary>
    /// Combo barının üstünde oynayan "kurtarıldın" efektleri.
    ///
    /// Combo Shield ve Soft Landing kartlarının ortak sorunu şuydu: ikisi de tam
    /// olarak KÖTÜ ŞEYİN OLMAMASINI sağlıyor. Olmayan bir şey ekranda görünmez,
    /// oyuncu da kartın çalıştığını hiç fark etmiyordu. Bu yüzden her ikisi de
    /// artık kendi olumlu anını çiziyor — biri kalkan, diğeri fren.
    ///
    /// UI çalışma zamanında kuruluyor: ComboView'a bileşen eklemek yeterli,
    /// prefab düzenlemek gerekmiyor.
    /// </summary>
    public sealed class ComboRescueFX : MonoBehaviour
    {
        private RectTransform _root;
        private Image         _shield;
        private Image         _flash;
        private TMP_Text      _label;
        private TMP_FontAsset _font;

        private static readonly Color ShieldColor = new Color(0.35f, 0.85f, 1f);
        private static readonly Color BrakeColor  = new Color(0.45f, 0.70f, 1f);

        /// <summary>ComboView tarafından çağrılır — hangi panelin üstüne çizileceğini söyler.</summary>
        public void Setup(RectTransform panel, TMP_FontAsset font)
        {
            _font = font;
            if (panel == null) return;

            var go = new GameObject("ComboRescueFX", typeof(RectTransform));
            _root = (RectTransform)go.transform;
            _root.SetParent(panel, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            _root.SetAsLastSibling();

            // Tıklamayı yutmasın — combo paneli altında buton olabilir.
            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable   = false;

            _flash  = NewImage("Flash",  JuiceGraphics.RadialGlow,        1.6f);
            _shield = NewImage("Shield", OverlayFXGraphics.HexShield,     1.0f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var lrt = (RectTransform)labelGo.transform;
            lrt.SetParent(_root, false);
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(260f, 60f);

            _label = labelGo.AddComponent<TextMeshProUGUI>();
            if (_font != null) _label.font = _font;
            _label.alignment     = TextAlignmentOptions.Center;
            _label.enableAutoSizing = false;
            _label.fontSize      = 26f;
            _label.raycastTarget = false;

            _flash.enabled  = false;
            _shield.enabled = false;
            _label.enabled  = false;
        }

        private Image NewImage(string name, Sprite sprite, float sizeFactor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);

            float h = Mathf.Max(_root.rect.height, 60f);
            rt.sizeDelta = Vector2.one * (h * sizeFactor * 1.6f);

            var img = go.AddComponent<Image>();
            img.sprite        = sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        // ── Combo Shield ─────────────────────────────────────────────────────

        /// <summary>
        /// Kalkan dışarıdan hızla oturur, darbeyi alır ve çatlayarak dağılır.
        /// Bar'lar aynı anda kırmızı yerine mavi parlıyor (ComboView tarafında) —
        /// oyuncu "combo düştü" refleksiyle bakmasın, "kalkan tuttu" görsün.
        /// </summary>
        public void PlayShieldSave(string text)
        {
            if (_shield == null) return;

            KillAll();

            _flash.enabled = true;
            _flash.color   = new Color(ShieldColor.r, ShieldColor.g, ShieldColor.b, 0.55f);
            _flash.transform.localScale = Vector3.one * 0.6f;
            DOTween.Sequence()
                .Join(_flash.transform.DOScale(1.5f, 0.35f).SetEase(Ease.OutCubic))
                .Join(_flash.DOFade(0f, 0.35f))
                .OnComplete(() => _flash.enabled = false);

            _shield.enabled = true;
            _shield.color   = new Color(ShieldColor.r, ShieldColor.g, ShieldColor.b, 0f);
            _shield.transform.localScale    = Vector3.one * 2.2f;
            _shield.transform.localRotation = Quaternion.identity;

            DOTween.Sequence()
                // 1) Dışarıdan sertçe oturma — darbenin karşılandığı an.
                .Append(_shield.transform.DOScale(1f, 0.13f).SetEase(Ease.InQuad))
                .Join(_shield.DOFade(1f, 0.10f))
                // 2) Kısa bir sarsıntı: kalkan yükü taşıyor.
                .Append(_shield.transform.DOPunchScale(Vector3.one * 0.14f, 0.22f, 8, 0.6f))
                // 3) Çatlayıp dağılma.
                .Append(_shield.transform.DOScale(1.55f, 0.30f).SetEase(Ease.OutQuad))
                .Join(_shield.DOFade(0f, 0.30f))
                .Join(_shield.transform.DORotate(new Vector3(0f, 0f, 18f), 0.30f))
                .OnComplete(() => _shield.enabled = false);

            ShowLabel(text, ShieldColor, riseDelay: 0.13f);
        }

        // ── Soft Landing ─────────────────────────────────────────────────────

        /// <summary>
        /// Fren: çarpan aşağı doğru bir hamle yapar ama yarı yolda tutulur.
        /// Aşağı-yukarı hareket kasıtlı — "düşüyordu, tutuldu" cümlesini tek bir
        /// jestle anlatan şey bu; sabit bir yazı aynı şeyi söyleyemiyor.
        /// </summary>
        public void PlaySoftLanding(string text, RectTransform multiplierText)
        {
            KillAll();

            if (multiplierText != null)
            {
                var baseAnchored = multiplierText.anchoredPosition;
                multiplierText.DOKill();
                DOTween.Sequence()
                    .Append(multiplierText.DOAnchorPosY(baseAnchored.y - 22f, 0.11f).SetEase(Ease.InQuad))
                    .Append(multiplierText.DOAnchorPosY(baseAnchored.y, 0.34f).SetEase(Ease.OutBack))
                    .OnKill(() => multiplierText.anchoredPosition = baseAnchored);
            }

            if (_flash != null)
            {
                _flash.enabled = true;
                _flash.color   = new Color(BrakeColor.r, BrakeColor.g, BrakeColor.b, 0.45f);
                _flash.transform.localScale = Vector3.one * 1.1f;
                DOTween.Sequence()
                    .Join(_flash.transform.DOScale(1.4f, 0.30f).SetEase(Ease.OutQuad))
                    .Join(_flash.DOFade(0f, 0.30f))
                    .OnComplete(() => _flash.enabled = false);
            }

            ShowLabel(text, BrakeColor, riseDelay: 0.08f);
        }

        // ── Ortak ────────────────────────────────────────────────────────────

        private void ShowLabel(string text, Color color, float riseDelay)
        {
            if (_label == null || string.IsNullOrEmpty(text)) return;

            _label.enabled = true;
            _label.text    = text;
            _label.color   = new Color(color.r, color.g, color.b, 0f);

            var rt = _label.rectTransform;
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.localScale       = Vector3.one * 0.7f;

            DOTween.Sequence()
                .AppendInterval(riseDelay)
                .Append(_label.DOFade(1f, 0.10f))
                .Join(rt.DOScale(1f, 0.22f).SetEase(Ease.OutBack))
                .Join(rt.DOAnchorPosY(46f, 0.55f).SetEase(Ease.OutCubic))
                .AppendInterval(0.35f)
                .Append(_label.DOFade(0f, 0.25f))
                .OnComplete(() => _label.enabled = false);
        }

        private void KillAll()
        {
            if (_flash  != null) { _flash.DOKill();  _flash.transform.DOKill(); }
            if (_shield != null) { _shield.DOKill(); _shield.transform.DOKill(); }
            if (_label  != null) { _label.DOKill();  _label.rectTransform.DOKill(); }
        }

        private void OnDestroy() => KillAll();
    }
}
