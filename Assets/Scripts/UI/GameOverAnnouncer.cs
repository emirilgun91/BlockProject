using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Timing")]
        [SerializeField] private float _holdDuration  = 1.2f;   // metin ekranda kalma süresi
        [SerializeField] private float _fadeInSpeed   = 0.25f;  // backdrop fade süresi
        [SerializeField] private float _punchDuration = 0.45f;  // metin punch süresi

        [Header("Visuals")]
        [SerializeField] private Color _backdropColor  = new Color(0f, 0f, 0f, 0.75f);
        [SerializeField] private Color _textColor      = new Color(0.94f, 0.27f, 0.17f, 1f); // crimson

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

            // 3. Ekranda bekle
            _sequence.AppendInterval(_holdDuration);

            // 4. Fade out ve callback
            _sequence
                .Append(_text != null
                    ? _text.DOFade(0f, 0.2f).SetUpdate(true)
                    : DOTween.Sequence().AppendInterval(0f))
                .AppendCallback(() =>
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

        private static string GetMessage(GameOverReason reason) => reason switch
        {
            GameOverReason.NoMoves      => "NO MOVES LEFT",
            GameOverReason.PoolExhausted => "POOL EXHAUSTED",
            _                           => "GAME OVER"
        };
    }

    /// <summary>Game over sebebi — mesaj ve ileride farklı efekt için.</summary>
    public enum GameOverReason
    {
        NoMoves,        // board'da boşluk yok
        PoolExhausted,  // pool limit doldu
        Default         // genel
    }
}