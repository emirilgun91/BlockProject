using System.Collections.Generic;
using DG.Tweening;
using RogueBlockBlast.Core.Settings;
using RogueBlockBlast.UI.Juice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI.FX
{
    /// <summary>
    /// Run ölçeğindeki büyük anların tam ekran gösterimi: Momentum Shield'in
    /// oyunu kurtarması ve Perfect Clear.
    ///
    /// Neden tam ekran: bu iki olay tek bir hücrede değil, RUN'ın tamamında
    /// oluyor. Tahtanın bir köşesinde patlayan bir efekt "burada bir şey oldu"
    /// der; oysa söylenmesi gereken "oyunun gidişatı az önce değişti".
    ///
    /// Kendi Canvas'ını kurar — sahneye eklemek gerekmez. Canvas her zaman açık
    /// ama tıklamayı geçirir (raycast kapalı), oyunu bloklamaz.
    /// </summary>
    public sealed class ScreenEventFX : MonoBehaviour
    {
        private static ScreenEventFX _instance;

        public static ScreenEventFX Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[ScreenEventFX]");
                _instance = go.AddComponent<ScreenEventFX>();
                return _instance;
            }
        }

        private RectTransform _root;
        private Image         _vignette;   // tam ekran renk perdesi
        private Image         _burst;      // merkezdeki hale
        private Image         _shield;
        private Image[]       _rings;
        private TMP_Text      _title;
        private TMP_Text      _subtitle;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            Build();
        }

        // ── Momentum Shield ──────────────────────────────────────────────────

        /// <summary>
        /// "Run kurtarıldı" anı. Ekran önce kararır (kayıp hissi kurulur), sonra
        /// kalkan araya girer ve perde camgöbeğine döner. Sıralama kasıtlı:
        /// kurtarmanın değeri, önce kaybın gösterilmesinden geliyor.
        /// </summary>
        public void PlayRunSaved(string title, string subtitle)
        {
            if (_root == null) return;
            KillAll();

            var save = new Color(0.35f, 0.85f, 1f);

            // 1) Kısa karartma — "bitiyordu".
            _vignette.enabled = true;
            _vignette.color   = new Color(0.02f, 0.02f, 0.06f, 0f);
            DOTween.Sequence()
                .Append(_vignette.DOFade(0.55f, 0.10f))
                .Append(_vignette.DOColor(new Color(save.r * 0.25f, save.g * 0.3f, save.b * 0.35f, 0.42f), 0.14f))
                .AppendInterval(0.45f)
                .Append(_vignette.DOFade(0f, 0.40f))
                .OnComplete(() => _vignette.enabled = false);

            // 2) Kalkan dışarıdan çakılır, yükü taşır, çatlar.
            _shield.enabled = true;
            _shield.color   = new Color(save.r, save.g, save.b, 0f);
            _shield.transform.localScale    = Vector3.one * 3.2f;
            _shield.transform.localRotation = Quaternion.identity;

            DOTween.Sequence()
                .AppendInterval(0.10f)
                .Append(_shield.transform.DOScale(1f, 0.16f).SetEase(Ease.InQuad))
                .Join(_shield.DOFade(1f, 0.12f))
                .AppendCallback(() => Shake(0.35f))
                .Append(_shield.transform.DOPunchScale(Vector3.one * 0.10f, 0.30f, 9, 0.7f))
                .AppendInterval(0.20f)
                .Append(_shield.transform.DOScale(1.6f, 0.35f).SetEase(Ease.OutQuad))
                .Join(_shield.DOFade(0f, 0.35f))
                .Join(_shield.transform.DORotate(new Vector3(0f, 0f, 14f), 0.35f))
                .OnComplete(() => _shield.enabled = false);

            RingPulse(save, delay: 0.24f, count: 2);
            ShowText(title, subtitle, save, delay: 0.22f);
        }

        // ── Perfect Clear ────────────────────────────────────────────────────

        /// <summary>
        /// Tahtanın tamamen boşalması — oyunun en büyük anı. Beyaz bir flaş,
        /// merkezden açılan üç altın halka ve altın bir perde.
        ///
        /// Burada karartma YOK: Momentum Shield'in aksine bu bir kurtarma değil,
        /// saf bir ödül. Aynı jesti kullanmak ikisini birbirine karıştırırdı.
        /// </summary>
        public void PlayPerfectClear(string title, string subtitle)
        {
            if (_root == null) return;
            KillAll();

            var gold = new Color(1f, 0.84f, 0.35f);

            _vignette.enabled = true;
            _vignette.color   = new Color(1f, 1f, 1f, 0f);
            DOTween.Sequence()
                .Append(_vignette.DOFade(0.75f, 0.06f))              // sert beyaz flaş
                .Append(_vignette.DOColor(new Color(gold.r, gold.g * 0.85f, gold.b * 0.4f, 0.30f), 0.20f))
                .AppendInterval(0.40f)
                .Append(_vignette.DOFade(0f, 0.45f))
                .OnComplete(() => _vignette.enabled = false);

            _burst.enabled = true;
            _burst.color   = new Color(1f, 1f, 1f, 0.9f);
            _burst.transform.localScale = Vector3.one * 0.2f;
            DOTween.Sequence()
                .Join(_burst.transform.DOScale(2.6f, 0.55f).SetEase(Ease.OutCubic))
                .Join(_burst.DOColor(new Color(gold.r, gold.g, gold.b, 0f), 0.55f))
                .OnComplete(() => _burst.enabled = false);

            Shake(0.7f);
            RingPulse(gold, delay: 0.05f, count: 3);
            ShowText(title, subtitle, gold, delay: 0.10f);
        }

        // ── Coin uçuşu (Bounty Hunter) ───────────────────────────────────────

        /// <summary>
        /// Tahtadaki noktalardan çıkıp bir UI hedefine (kart slotu) uçan sikkeler.
        ///
        /// İlk sürüm sikkeleri dünya uzayında çiziyordu: tile boyutuna bağlı
        /// oldukları için ekranda nokta kadar kalıyor ve hiçbir yere gitmiyorlardı.
        /// Burada sikkeler UI katmanında, piksel boyutlu ve hedefe kilitli —
        /// "bu para o kartın kazandırdığı para" cümlesini hareketin kendisi kuruyor.
        /// </summary>
        public void PlayCoinFlight(
            IReadOnlyList<Vector3> worldOrigins, Camera cam, RectTransform target, int coinCount)
        {
            if (_root == null || cam == null || worldOrigins == null || worldOrigins.Count == 0) return;
            if (coinCount <= 0) return;

            Vector2 targetLocal;
            if (target != null)
            {
                Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(null, target.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _root, targetScreen, null, out targetLocal);
            }
            else
            {
                // Kart henüz envanterde değilse (ilk tetikleme) yukarı doğru savrulsun.
                targetLocal = new Vector2(0f, _root.rect.height * 0.45f);
            }

            int coins = Mathf.Clamp(coinCount, 1, 12);
            for (int i = 0; i < coins; i++)
            {
                Vector3 origin = worldOrigins[i % worldOrigins.Count];
                Vector2 screen = cam.WorldToScreenPoint(origin);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _root, screen, null, out Vector2 startLocal);

                startLocal += Random.insideUnitCircle * 28f;

                var coin = CenterImage("Coin", JuiceGraphics.RadialGlow, 34f);
                var core = CenterImage("CoinCore", OverlayFXGraphics.Ring, 26f);
                core.rectTransform.SetParent(coin.rectTransform, false);
                core.rectTransform.anchoredPosition = Vector2.zero;
                core.color = new Color(1f, 0.98f, 0.85f, 0.95f);

                coin.color = new Color(1f, 0.82f, 0.28f, 1f);
                var rt = coin.rectTransform;
                rt.anchoredPosition = startLocal;
                rt.localScale       = Vector3.zero;

                // Yol iki aşamalı: önce yukarı doğru serbest bir sıçrama, sonra
                // hedefe hızlanan bir çekiliş. Tek düz çizgi "ışınlandı" gibi
                // duruyordu; sıçrama sikkeye ağırlık veriyor.
                Vector2 hop = startLocal + new Vector2(Random.Range(-70f, 70f), Random.Range(90f, 150f));

                float delay = i * 0.05f;
                DOTween.Sequence()
                    .AppendInterval(delay)
                    .Append(rt.DOScale(1f, 0.14f).SetEase(Ease.OutBack))
                    .Join(rt.DOAnchorPos(hop, 0.26f).SetEase(Ease.OutQuad))
                    .Append(rt.DOAnchorPos(targetLocal, 0.42f).SetEase(Ease.InCubic))
                    .Join(rt.DOScale(0.45f, 0.42f).SetEase(Ease.InQuad))
                    .Append(coin.DOFade(0f, 0.10f))
                    .OnComplete(() =>
                    {
                        if (coin == null) return;
                        // Varış darbesi: slot bir an parlasın ki sikkelerin
                        // nereye gittiği gözden kaçmasın.
                        if (target != null)
                        {
                            target.DOKill();
                            target.localScale = Vector3.one;
                            target.DOPunchScale(Vector3.one * 0.16f, 0.24f, 8, 0.7f)
                                  .OnKill(() => { if (target != null) target.localScale = Vector3.one; });
                        }
                        Destroy(coin.gameObject);
                    });
            }
        }

        // ── Ortak parçalar ───────────────────────────────────────────────────

        private void RingPulse(Color color, float delay, int count)
        {
            for (int i = 0; i < count && i < _rings.Length; i++)
            {
                var ring = _rings[i];
                ring.enabled = true;
                ring.color   = new Color(color.r, color.g, color.b, 0.85f);
                ring.transform.localScale = Vector3.one * 0.25f;

                float d = delay + i * 0.09f;
                DOTween.Sequence()
                    .AppendInterval(d)
                    .Append(ring.transform.DOScale(2.2f + i * 0.5f, 0.55f).SetEase(Ease.OutCubic))
                    .Join(ring.DOFade(0f, 0.55f).SetEase(Ease.InQuad))
                    .OnComplete(() => ring.enabled = false);
            }
        }

        private void ShowText(string title, string subtitle, Color color, float delay)
        {
            SetupLine(_title, title, color, delay, riseTo: 96f, scaleFrom: 0.6f);
            SetupLine(_subtitle, subtitle,
                      Color.Lerp(color, Color.white, 0.5f), delay + 0.10f,
                      riseTo: 30f, scaleFrom: 0.85f);
        }

        private static void SetupLine(
            TMP_Text label, string text, Color color, float delay, float riseTo, float scaleFrom)
        {
            if (label == null) return;
            if (string.IsNullOrEmpty(text)) { label.enabled = false; return; }

            label.enabled = true;
            label.text    = text;
            label.color   = new Color(color.r, color.g, color.b, 0f);

            var rt = label.rectTransform;
            rt.anchoredPosition = new Vector2(0f, riseTo - 26f);
            rt.localScale       = Vector3.one * scaleFrom;

            DOTween.Sequence()
                .AppendInterval(delay)
                .Append(label.DOFade(1f, 0.12f))
                .Join(rt.DOScale(1f, 0.30f).SetEase(Ease.OutBack))
                .Join(rt.DOAnchorPosY(riseTo, 0.45f).SetEase(Ease.OutCubic))
                .AppendInterval(0.60f)
                .Append(label.DOFade(0f, 0.30f))
                .OnComplete(() => label.enabled = false);
        }

        /// <summary>Erişilebilirlik: "Hareketi Azalt" açıkken ScreenShake 0 döner ve atlanır.</summary>
        private void Shake(float strength)
        {
            float s = GameSettings.ScreenShake;
            if (s <= 0f || _root == null) return;

            _root.DOKill();
            _root.anchoredPosition = Vector2.zero;
            _root.DOShakeAnchorPos(0.35f, new Vector2(22f, 14f) * strength * s, 12, 90f)
                 .SetEase(Ease.OutQuad)
                 .OnComplete(() => _root.anchoredPosition = Vector2.zero);
        }

        private void KillAll()
        {
            if (_vignette != null) _vignette.DOKill();
            if (_burst    != null) { _burst.DOKill();  _burst.transform.DOKill(); }
            if (_shield   != null) { _shield.DOKill(); _shield.transform.DOKill(); }
            if (_rings != null)
                foreach (var r in _rings)
                    if (r != null) { r.DOKill(); r.transform.DOKill(); }
            if (_title    != null) { _title.DOKill();    _title.rectTransform.DOKill(); }
            if (_subtitle != null) { _subtitle.DOKill(); _subtitle.rectTransform.DOKill(); }
        }

        private void OnDestroy() => KillAll();

        // ── Kurulum ──────────────────────────────────────────────────────────

        private void Build()
        {
            var canvasGo = new GameObject("EventCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;          // her şeyin üstünde

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            var cg = canvasGo.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable   = false;

            var rootGo = new GameObject("Root", typeof(RectTransform));
            _root = (RectTransform)rootGo.transform;
            _root.SetParent(canvasGo.transform, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            _vignette = FullScreenImage("Vignette");
            _burst    = CenterImage("Burst",  JuiceGraphics.RadialGlow,      900f);
            _shield   = CenterImage("Shield", OverlayFXGraphics.HexShield,   420f);

            _rings = new Image[3];
            for (int i = 0; i < _rings.Length; i++)
                _rings[i] = CenterImage("Ring" + i, OverlayFXGraphics.Ring, 520f);

            _title    = NewLabel("Title",    64f, FontStyles.Bold);
            _subtitle = NewLabel("Subtitle", 32f, FontStyles.Normal);

            _vignette.enabled = false;
            _burst.enabled    = false;
            _shield.enabled   = false;
            foreach (var r in _rings) r.enabled = false;
            _title.enabled    = false;
            _subtitle.enabled = false;
        }

        private Image FullScreenImage(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root, false);
            // Sarsıntı sırasında kenarlardan boşluk görünmesin diye taşırıyoruz.
            rt.anchorMin = new Vector2(-0.1f, -0.1f);
            rt.anchorMax = new Vector2( 1.1f,  1.1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private Image CenterImage(string name, Sprite sprite, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * size;

            var img = go.AddComponent<Image>();
            img.sprite         = sprite;
            img.raycastTarget  = false;
            img.preserveAspect = true;
            return img;
        }

        private TMP_Text NewLabel(string name, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1200f, 100f);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.alignment       = TextAlignmentOptions.Center;
            label.fontSize        = size;
            label.fontStyle       = style;
            label.raycastTarget   = false;
            label.enableAutoSizing = false;
            return label;
        }
    }
}
