using System.Collections;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    public sealed class FrameFeedbackController : MonoBehaviour
    {
        public static FrameFeedbackController Instance { get; private set; }

        [SerializeField] private FrameStateConfig _config;
        [SerializeField] private Renderer         _renderer;

        // Shader IDs
        static readonly int ID_GlowColor      = Shader.PropertyToID("_GlowColor");
        static readonly int ID_GlowIntensity  = Shader.PropertyToID("_GlowIntensity");
        static readonly int ID_GlowPulseSpeed = Shader.PropertyToID("_GlowPulseSpeed");
        static readonly int ID_RainbowActive  = Shader.PropertyToID("_RainbowActive");
        static readonly int ID_RainbowSpeed   = Shader.PropertyToID("_RainbowSpeed");
        static readonly int ID_ShockwaveT     = Shader.PropertyToID("_ShockwaveT");
        static readonly int ID_ShockwaveColor = Shader.PropertyToID("_ShockwaveColor");
        static readonly int ID_ShockwaveWidth = Shader.PropertyToID("_ShockwaveWidth");
        static readonly int ID_NoiseAmount    = Shader.PropertyToID("_NoiseAmount");
        static readonly int ID_Alpha          = Shader.PropertyToID("_Alpha");
        static readonly int ID_RainbowSaturation          = Shader.PropertyToID("RainbowSaturation");
        private enum FrameState { Idle, Combo, Critical, LineClear, Milestone, GameOver }

        private FrameState           _currentState  = FrameState.Idle;
        private FrameState           _previousState = FrameState.Idle;
        private MaterialPropertyBlock _mpb;

        private float _currentIntensity;
        private float _comboMultiplier = 1f;

        private Coroutine _pulseLoop;
        private Coroutine _rainbowRoutine;
        private Coroutine _breakRoutine;
        private TweenerCore<float, float, FloatOptions> _shockSequence;

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;

            if (_renderer == null)
                _renderer = GetComponent<Renderer>();

            // MaterialPropertyBlock — SpriteRenderer dahil tüm renderer tipleriyle çalışır
            _mpb = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_mpb);

            // Tüm property'leri başlangıç değerleriyle set et
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ID_GlowColor,      Color.white);
            _mpb.SetFloat(ID_GlowIntensity,  0f);
            _mpb.SetFloat(ID_GlowPulseSpeed, 0f);
            _mpb.SetFloat(ID_RainbowActive,  0f);
            _mpb.SetFloat(ID_RainbowSpeed,   1f);
            _mpb.SetFloat(ID_RainbowSaturation, 1f);
            _mpb.SetFloat(ID_ShockwaveT,     0f);
            _mpb.SetFloat(ID_ShockwaveWidth, 0.08f);
            _mpb.SetFloat(ID_NoiseAmount,    0f);
            _mpb.SetFloat(ID_Alpha,          1f);   // ← kritik
            _renderer.SetPropertyBlock(_mpb);
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
            StopLoops();
        }

        // ── PUBLIC API ───────────────────────────────────────────────────────

        public void OnDrop(Color pieceColor)
            => StartCoroutine(DropPulse(pieceColor));

        public void OnComboChanged(float multiplier)
        {
            _comboMultiplier = multiplier;
            if (_currentState == FrameState.GameOver || _currentState == FrameState.Milestone) return;

            StopLoops();

            if (multiplier >= _config.ComboPlasmaThreshold)      EnterComboPlasma();
            else if (multiplier >= _config.ComboGlowThreshold)   EnterComboGlow();
            else if (multiplier >= _config.ComboWarmThreshold)   EnterComboWarm();
            else SetIdle();
        }

        public void OnComboBreak()
        {
            _comboMultiplier = 1f;
            if (_currentState != FrameState.Combo) return;
            StopLoops();
            _breakRoutine = StartCoroutine(ComboBreak());
        }

        public void OnCritical(int remaining)
        {
            if (_currentState == FrameState.GameOver || _currentState == FrameState.Milestone) return;
            if (remaining <= _config.CriticalThreshold) EnterCritical(remaining);
            else if (_currentState == FrameState.Critical) OnComboChanged(_comboMultiplier);
        }

        public void OnLineClear(int lineCount)
        {
            if (_currentState == FrameState.GameOver) return;
            StopLoops();
            _rainbowRoutine = StartCoroutine(RainbowSpin(lineCount));
        }

        public void OnMilestone(Color? color = null)
        {
            _shockSequence?.Kill();
            SetShockwave(color ?? _config.MilestoneColor);
        }

        public void OnGameOver()
        {
            StopLoops();
            _currentState = FrameState.GameOver;
            StartCoroutine(GameOverSequence());
        }

        // ── STATES ──────────────────────────────────────────────────────────

        private void SetIdle(bool animate = true)
        {
            _previousState = _currentState;
            _currentState  = FrameState.Idle;

            SetProp(ID_RainbowActive,  0f);
            SetProp(ID_GlowPulseSpeed, 0f);
            SetProp(ID_NoiseAmount,    0f);
            SetProp(ID_Alpha,          1f);

            if (!animate)
            {
                SetProp(ID_GlowIntensity, 0f);
                return;
            }

            TweenFloat(ID_GlowIntensity, 0f, _config.IdleFadeDuration);
            TweenFloat(ID_Alpha,         1f, _config.IdleFadeDuration);
        }

        private void EnterComboWarm()
        {
            _currentState = FrameState.Combo;
            SetProp(ID_RainbowActive, 0f);
            SetProp(ID_GlowPulseSpeed, 0f);
            SetProp(ID_NoiseAmount,   0f);
            TweenColor(ID_GlowColor,     _config.ComboWarmColor,     _config.ComboWarmDuration);
            TweenFloat(ID_GlowIntensity, _config.ComboWarmIntensity, _config.ComboWarmDuration);
        }

        private void EnterComboGlow()
        {
            _currentState = FrameState.Combo;
            SetProp(ID_RainbowActive, 0f);
            SetProp(ID_NoiseAmount,   0f);
            TweenColor(ID_GlowColor,      _config.ComboGlowColor,    0.3f);
            TweenFloat(ID_GlowIntensity,  _config.ComboGlowIntensity, 0.3f);
            TweenFloat(ID_GlowPulseSpeed, _config.ComboGlowSpeed,     0.3f);
        }

        private void EnterComboPlasma()
        {
            _currentState = FrameState.Combo;
            SetProp(ID_RainbowActive, 0f);
            SetProp(ID_NoiseAmount,   0f);
            TweenColor(ID_GlowColor,      _config.ComboGlowColor,       0.5f);
            TweenFloat(ID_GlowIntensity,  _config.ComboPlasmaIntensity,  0.5f);
            TweenFloat(ID_GlowPulseSpeed, _config.ComboPlasmaSpeed,      0.5f);
        }

        private IEnumerator ComboBreak()
        {
            SetProp(ID_NoiseAmount, 1.0f);
            for (int i = 0; i < _config.BreakFlickerCount; i++)
            {
                SetProp(ID_GlowIntensity, 0f);
                yield return new WaitForSeconds(_config.BreakFlickerSpeed);
                SetProp(ID_GlowIntensity, _currentIntensity * 0.5f);
                yield return new WaitForSeconds(_config.BreakFlickerSpeed);
            }
            TweenFloat(ID_GlowIntensity, 0f, _config.BreakFadeDuration);
            TweenFloat(ID_NoiseAmount,   0f, _config.BreakFadeDuration);
            yield return new WaitForSeconds(_config.BreakFadeDuration);
            SetIdle();
        }

        private void EnterCritical(int remaining)
        {
            _currentState = FrameState.Critical;
            SetProp(ID_RainbowActive, 0f);
            SetProp(ID_NoiseAmount,   0f);
            float urgency   = 1f - Mathf.Clamp01((float)remaining / _config.CriticalThreshold);
            float intensity = Mathf.Lerp(_config.CriticalMinIntensity, _config.CriticalMaxIntensity, urgency);
            float speed     = Mathf.Lerp(_config.CriticalMinSpeed,     _config.CriticalMaxSpeed,     urgency);
            TweenColor(ID_GlowColor,      _config.CriticalColor, 0.3f);
            TweenFloat(ID_GlowIntensity,  intensity,             0.3f);
            TweenFloat(ID_GlowPulseSpeed, speed,                 0.3f);
        }

        private IEnumerator RainbowSpin(int lineCount)
        {
            _previousState = _currentState;
            _currentState  = FrameState.LineClear;

            float t     = Mathf.Clamp01((float)lineCount / _config.RainbowMaxLines);
            float speed = Mathf.Lerp(_config.RainbowMinSpeed, _config.RainbowMaxSpeed, t);

            SetProp(ID_RainbowActive,  1f);
            SetProp(ID_RainbowSpeed,   speed);
            SetProp(ID_GlowPulseSpeed, 0f);
            TweenFloat(ID_GlowIntensity, _config.RainbowGlowIntensity, 0.1f);

            yield return new WaitForSeconds(_config.RainbowHoldDuration);

            TweenFloat(ID_GlowIntensity, 0f, _config.RainbowFadeDuration);
            yield return new WaitForSeconds(_config.RainbowFadeDuration);

            SetProp(ID_RainbowActive, 0f);
            OnComboChanged(_comboMultiplier);
        }

        private void SetShockwave(Color color)
        {
            _previousState = _currentState;
            _currentState  = FrameState.Milestone;

            SetProp(ID_ShockwaveColor, color);
            SetProp(ID_ShockwaveWidth, _config.MilestoneWidth);
            SetProp(ID_ShockwaveT,     0f);

            _shockSequence = DOTween.To(
                () => GetPropFloat(ID_ShockwaveT),
                v  => SetProp(ID_ShockwaveT, v),
                1f,
                _config.MilestoneDuration
            ).SetEase(Ease.OutCubic)
             .OnComplete(() =>
             {
                 SetProp(ID_ShockwaveT, 0f);
                 _currentState = _previousState;
                 OnComboChanged(_comboMultiplier);
             });
        }

        private IEnumerator DropPulse(Color pieceColor)
        {
            float savedIntensity = _currentIntensity;
            Color savedGlow      = GetPropColor(ID_GlowColor);

            SetProp(ID_GlowColor, pieceColor);
            TweenFloat(ID_GlowIntensity, _config.DropIntensity, _config.DropDuration * 0.3f);
            yield return new WaitForSeconds(_config.DropDuration * 0.3f);

            TweenFloat(ID_GlowIntensity, savedIntensity, _config.DropDuration * 0.7f);
            TweenColor(ID_GlowColor,     savedGlow,       _config.DropDuration * 0.7f);
        }

        private IEnumerator GameOverSequence()
        {
            SetProp(ID_RainbowActive,  0f);
            SetProp(ID_GlowPulseSpeed, 0f);
            SetProp(ID_GlowColor,      _config.GameOverColor);
            TweenFloat(ID_GlowIntensity, _config.GameOverBlastIntensity, _config.GameOverBlastDuration);
            yield return new WaitForSeconds(_config.GameOverBlastDuration);
            TweenFloat(ID_GlowIntensity, 0f, _config.GameOverFadeDuration);
            TweenFloat(ID_NoiseAmount,   2f, _config.GameOverFadeDuration);
            TweenFloat(ID_Alpha,         0f, _config.GameOverFadeDuration);
        }

        // ── MPB HELPERS ──────────────────────────────────────────────────────

        private void SetProp(int id, float value)
        {
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(id, value);
            _renderer.SetPropertyBlock(_mpb);
            if (id == ID_GlowIntensity) _currentIntensity = value;
        }

        private void SetProp(int id, Color value)
        {
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(id, value);
            _renderer.SetPropertyBlock(_mpb);
        }

        private float GetPropFloat(int id)
        {
            _renderer.GetPropertyBlock(_mpb);
            return _mpb.GetFloat(id);
        }

        private Color GetPropColor(int id)
        {
            _renderer.GetPropertyBlock(_mpb);
            return _mpb.GetColor(id);
        }

        private void TweenFloat(int id, float target, float duration)
        {
            float current = GetPropFloat(id);
            DOTween.To(() => current, v => { current = v; SetProp(id, v); }, target, duration)
                   .SetId(this);
        }

        private void TweenColor(int id, Color target, float duration)
        {
            Color current = GetPropColor(id);
            DOTween.To(() => current, v => { current = v; SetProp(id, v); }, target, duration)
                   .SetId(this);
        }

        private void StopLoops()
        {
            if (_pulseLoop      != null) { StopCoroutine(_pulseLoop);      _pulseLoop      = null; }
            if (_rainbowRoutine != null) { StopCoroutine(_rainbowRoutine); _rainbowRoutine = null; }
            if (_breakRoutine   != null) { StopCoroutine(_breakRoutine);   _breakRoutine   = null; }
            _shockSequence?.Kill();
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
            StopLoops();
        }
    }
}