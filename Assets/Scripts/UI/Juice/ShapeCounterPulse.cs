using RogueBlockBlast.Core;
using RogueBlockBlast.Core.Settings;
using RogueBlockBlast.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI.Juice
{
    /// <summary>
    /// Şekil sayacının ("27 left") nabız gibi büyüyüp küçülmesi.
    /// Kalan şekil sayısı eşiğin altına inince nabız hızlanır ve renk kızarır.
    ///
    /// Sayı metinden okunmaz — <see cref="MilestoneProgressState.PiecesRemaining"/>
    /// üzerinden alınır. Metin ayrıştırmak yerelleştirmeye ("27 left" / "27 kaldı")
    /// bağımlı olurdu.
    ///
    /// <see cref="RunController"/> milestone sistemini <c>Start()</c> içinde
    /// kurduğu için abonelik tembel yapılır: sistem hazır olana kadar her frame
    /// denenir, bağlanınca bırakılır.
    /// </summary>
    public sealed class ShapeCounterPulse : MonoBehaviour
    {
        [Header("Hedef")]
        [Tooltip("Ölçeklenecek nesne — YALNIZCA yazının kendisi olmalı.\n\n" +
                 "Çerçeveye ya da panele bağlanırsa tüm UI nefes alır; ayrıca " +
                 "panellerin pivotu genelde merkezde olmadığı için ölçeklenme " +
                 "kayma gibi görünür.")]
        [SerializeField] private RectTransform _target;

        [Tooltip("Aciliyette rengi değişecek yazı.")]
        [SerializeField] private TMP_Text _text;

        [Tooltip("Opsiyonel: aciliyette rengi değişecek çerçeve. Ölçeklenmez, " +
                 "yalnızca rengi değişir. Boş bırakılabilir.")]
        [SerializeField] private Graphic _frame;

        [Header("Nabız")]
        [SerializeField] private float _amplitude     = 0.04f;
        [SerializeField] private float _speedCalm     = 0.45f;
        [SerializeField] private float _speedUrgent   = 1.8f;
        [Tooltip("Aciliyette genliğin katı.")]
        [SerializeField] private float _urgentAmplitudeFactor = 1.6f;

        [Header("Aciliyet")]
        [Tooltip("Kalan şekil bu sayının altına inince aciliyet başlar.")]
        [SerializeField] private int _urgentThreshold = 10;

        [SerializeField] private Color _urgentColor = new Color(0.90f, 0.28f, 0.24f, 1f);

        [Tooltip("Aciliyete geçişin yumuşaması — ani sıçrama olmasın.")]
        [SerializeField] private float _urgencyDamping = 5f;

        private RunController _run;
        private bool          _bound;

        private Vector3 _baseScale    = Vector3.one;
        private Vector2 _basePosition = Vector2.zero;
        private Color   _baseTextColor;
        private Color   _baseFrameColor;

        private float _urgency;        // 0..1, yumuşatılmış
        private float _urgencyTarget;
        private float _phase;

        private void Awake()
        {
            if (_target == null) _target = transform as RectTransform;
            if (_target != null)
            {
                _baseScale    = _target.localScale;
                _basePosition = _target.anchoredPosition;
            }

            if (_text  != null) _baseTextColor  = _text.color;
            if (_frame != null) _baseFrameColor = _frame.color;
        }

        private void OnDestroy() => Unbind();

        private void Update()
        {
            if (!_bound) TryBind();

            float dt = Time.unscaledDeltaTime;

            _urgency = Mathf.Lerp(_urgency, _urgencyTarget, 1f - Mathf.Exp(-_urgencyDamping * dt));

            ApplyColor();
            ApplyScale(dt);
        }

        // ── Bağlanma ─────────────────────────────────────────────────────────

        private void TryBind()
        {
            if (_run == null) _run = FindFirstObjectByType<RunController>();

            // MilestoneSystem RunController.Start() içinde kuruluyor — hazır
            // olana kadar sessizce beklenir.
            var milestone = _run != null ? _run.Milestone : null;
            if (milestone == null) return;

            milestone.OnProgressChanged += HandleProgress;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound || _run == null || _run.Milestone == null) return;
            _run.Milestone.OnProgressChanged -= HandleProgress;
            _bound = false;
        }

        private void HandleProgress(MilestoneProgressState state)
        {
            if (_urgentThreshold <= 0) { _urgencyTarget = 0f; return; }

            // Eşiğin altında doğrusal artış: eşikte 0, sıfır şekilde 1.
            float t = 1f - Mathf.Clamp01(state.PiecesRemaining / (float)_urgentThreshold);
            _urgencyTarget = t;
        }

        // ── Uygulama ─────────────────────────────────────────────────────────

        private void ApplyScale(float dt)
        {
            if (_target == null) return;

            float vfx = Mathf.Clamp01(GameSettings.VfxIntensity);

            if (GameSettings.ReduceMotion || vfx <= 0.001f)
            {
                SetScale(1f);
                return;
            }

            float speed = Mathf.Lerp(_speedCalm, _speedUrgent, _urgency);
            _phase += dt * speed;

            float amp = _amplitude * Mathf.Lerp(1f, _urgentAmplitudeFactor, _urgency) * vfx;
            SetScale(1f + Mathf.Sin(_phase * Mathf.PI * 2f) * amp);
        }

        /// <summary>
        /// Ölçeği uygular ve pivot merkezde değilse konumu telafi eder.
        ///
        /// <c>localScale</c> pivota göre ölçekler; pivot köşedeyse nesne
        /// büyürken o köşeden uzağa doğru kayar ve ekranda "yer değiştiriyor"
        /// gibi görünür. Kaymayı geri alıp merkeze göre ölçeklenmiş etkisini
        /// veriyoruz.
        /// </summary>
        private void SetScale(float s)
        {
            _target.localScale = _baseScale * s;

            Vector2 pivotOffset = new Vector2(0.5f, 0.5f) - _target.pivot;
            if (pivotOffset.sqrMagnitude < 0.0001f)
            {
                _target.anchoredPosition = _basePosition;
                return;
            }

            Vector2 size  = _target.rect.size;
            Vector2 drift = new Vector2(pivotOffset.x * size.x, pivotOffset.y * size.y) * (s - 1f);

            _target.anchoredPosition = _basePosition + drift;
        }

        private void ApplyColor()
        {
            if (_text != null)
                _text.color = Color.Lerp(_baseTextColor, _urgentColor, _urgency);

            if (_frame != null)
                _frame.color = Color.Lerp(_baseFrameColor, _urgentColor, _urgency);
        }
    }
}
