using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RogueBlockBlast.Core.Localization;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Game Over UI açılmadan önce çalışan animasyon.
    ///
    /// Hierarchy:
    ///  GameOver_Txt  (TMP — bu script buraya)
    ///  Backdrop      (Image — opsiyonel, tam ekran karartma)
    ///
    /// RunController'da OnGameOver() içinde:
    ///   GameOverAnnouncer.Instance.Play(reason, () => GameOverUI.Instance.Show(_score));
    /// </summary>
    public sealed class GameOverAnnouncer : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        public static GameOverAnnouncer Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("References")]
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Image    _backdrop;        // opsiyonel — tam ekran karartma
        [Tooltip("Sebebi açıklayan alt satır. Boş bırakılırsa çalışma zamanında oluşturulur.")]
        [SerializeField] private TMP_Text _subtitle;

        [Header("Timing")]
        [SerializeField] private float _holdDuration  = 2.0f;   // sebep ekranda kalma süresi
        [SerializeField] private float _fadeInSpeed   = 0.25f;  // backdrop fade süresi
        [SerializeField] private float _punchDuration = 0.45f;  // metin punch süresi

        [Header("Visuals")]
        [SerializeField] private Color _backdropColor  = new Color(0f, 0f, 0f, 0.75f);
        [SerializeField] private Color _textColor      = new Color(0.94f, 0.27f, 0.17f, 1f); // crimson
        [SerializeField] private Color _subtitleColor  = new Color(0.93f, 0.85f, 0.62f, 1f);

        // ── Runtime ──────────────────────────────────────────────────────────
        private Vector3  _textBaseScale;
        private Sequence _sequence;

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // _text referansı Inspector'dan bağlı değilse kendisi bul
            if (_text == null)
                _text = GetComponent<TMP_Text>();

            // Başlangıçta kesin gizle
            if (_text != null)
            {
                _textBaseScale = _text.transform.localScale;
                var c = _text.color;
                c.a = 0f;
                _text.color = c;
            }

            if (_backdrop != null)
            {
                var c = _backdropColor;
                c.a = 0f;
                _backdrop.color   = c;
                _backdrop.enabled = false;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Animasyonu başlatır.
        /// onComplete: animasyon bitince çağrılır (GameOverUI.Show buraya gelir)
        /// </summary>
        public void Play(GameOverReason reason, System.Action onComplete)
        {
            _sequence?.Kill(complete: false);
            Game.GameStateController.LockInput();

            string message = GetMessage(reason);

            if (_text != null)
            {
                _text.text  = message;
                _text.color = new Color(_textColor.r, _textColor.g, _textColor.b, 0f);
                _text.transform.localScale = _textBaseScale * 0.1f;
            }

            // Sebebin ALTINA insan diliyle açıklama — oyuncu neden kaybettiğini okusun
            EnsureSubtitle();
            if (_subtitle != null)
            {
                _subtitle.text  = GetExplanation(reason);
                _subtitle.color = new Color(_subtitleColor.r, _subtitleColor.g, _subtitleColor.b, 0f);
            }

            _sequence = DOTween.Sequence().SetUpdate(true); // timeScale bağımsız

            // 1. Backdrop fade in
            if (_backdrop != null)
            {
                _backdrop.enabled = true;
                _sequence.Append(
                    _backdrop.DOColor(_backdropColor, _fadeInSpeed)
                             .SetEase(Ease.OutQuad)
                             .SetUpdate(true)
                );
            }
            else
            {
                _sequence.AppendInterval(_fadeInSpeed);
            }

            // 2. Metin punch scale ile gelir
            if (_text != null)
            {
                _sequence
                    .Append(_text.DOFade(1f, _punchDuration * 0.3f)
                                 .SetEase(Ease.OutQuad)
                                 .SetUpdate(true))
                    .Join(_text.transform
                               .DOScale(_textBaseScale * 1.15f, _punchDuration * 0.5f)
                               .SetEase(Ease.OutBack)
                               .SetUpdate(true))
                    .Append(_text.transform
                                 .DOScale(_textBaseScale, _punchDuration * 0.5f)
                                 .SetEase(Ease.OutBounce)
                                 .SetUpdate(true));
            }

            // 2b. Sebep vurgusu: başlık hafifçe sarsılır, açıklama altından belirir
            if (_text != null)
                _sequence.Join(_text.transform
                                    .DOShakeRotation(0.35f, new Vector3(0f, 0f, 6f), 10, 90f)
                                    .SetUpdate(true));

            if (_subtitle != null)
            {
                _sequence.Append(_subtitle.DOFade(1f, 0.25f).SetEase(Ease.OutQuad).SetUpdate(true));
                var srt = _subtitle.rectTransform;
                float baseY = srt.anchoredPosition.y;
                srt.anchoredPosition = new Vector2(srt.anchoredPosition.x, baseY - 18f);
                _sequence.Join(srt.DOAnchorPosY(baseY, 0.3f).SetEase(Ease.OutBack).SetUpdate(true));
            }

            // 3. Ekranda bekle — oyuncunun okumasına yetecek kadar
            _sequence.AppendInterval(_holdDuration);

            // 4. Fade out ve callback
            _sequence
                .Append(_text != null
                    ? _text.DOFade(0f, 0.25f).SetUpdate(true)
                    : DOTween.Sequence().AppendInterval(0f));

            if (_subtitle != null)
                _sequence.Join(_subtitle.DOFade(0f, 0.25f).SetUpdate(true));

            _sequence.AppendCallback(() =>
                {
                    Hide();
                    onComplete?.Invoke();
                });
        }

        // ── Private ──────────────────────────────────────────────────────────
        private void Hide()
        {
            if (_text != null)
            {
                var c = _text.color;
                c.a = 0f;
                _text.color = c;
                _text.transform.localScale = _textBaseScale;
            }

            if (_backdrop != null)
            {
                _backdrop.enabled = false;
                var c = _backdropColor; c.a = 0f;
                _backdrop.color = c;
            }
        }

        /// <summary>
        /// Alt açıklama satırını prefabda yoksa çalışma zamanında üretir —
        /// sahneyi elle düzenlemeye gerek kalmaz.
        /// </summary>
        private void EnsureSubtitle()
        {
            if (_subtitle != null || _text == null) return;

            var go = new GameObject("GameOverSubtitle", typeof(RectTransform));
            go.transform.SetParent(_text.transform.parent, false);

            var rt = (RectTransform)go.transform;
            var src = _text.rectTransform;
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot     = src.pivot;
            rt.sizeDelta = new Vector2(Mathf.Max(src.sizeDelta.x, 900f), 80f);
            rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, -110f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font      = _text.font;
            tmp.fontSize  = Mathf.Max(22f, _text.fontSize * 0.38f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            tmp.color = new Color(_subtitleColor.r, _subtitleColor.g, _subtitleColor.b, 0f);

            _subtitle = tmp;
        }

        private static string GetExplanation(GameOverReason reason) => reason switch
        {
            GameOverReason.NoMoves       => Loc.GetOr("GameOver.Explain.NoMoves",
                "None of your shapes fit on the board anymore."),
            GameOverReason.PoolExhausted => Loc.GetOr("GameOver.Explain.PoolExhausted",
                "You ran out of shapes before reaching the milestone."),
            GameOverReason.CannotPlace   => Loc.GetOr("GameOver.Explain.CannotPlace",
                "First Picks blocked rotation and nothing fits as drawn."),
            _                            => Loc.GetOr("GameOver.Explain.Default",
                "Your run has ended."),
        };

        private static string GetMessage(GameOverReason reason) => reason switch
        {
            GameOverReason.NoMoves       => Loc.GetOr("GameOver.Reason.NoMoves",      "NO MOVES LEFT"),
            GameOverReason.PoolExhausted => Loc.GetOr("GameOver.Reason.PoolExhausted", "POOL EXHAUSTED"),
            GameOverReason.CannotPlace   => Loc.GetOr("GameOver.Reason.CannotPlace",   "SHAPE CANNOT BE PLACED"),
            _                            => Loc.GetOr("GameOver.Reason.Default",       "GAME OVER")
        };
    }

    /// <summary>Game over sebebi — mesaj ve ileride farklı efekt için.</summary>
    public enum GameOverReason
    {
        NoMoves,        // board'da boşluk yok
        PoolExhausted,  // pool limit doldu
        CannotPlace,    // döndürme kilitliyken (First Picks) hiçbir parça sığmıyor
        Default         // genel
    }
}