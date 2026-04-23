using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// MainMenu ses yönetimi — müzik + hover/click SFX.
    /// AudioManager singleton'ı kullanır.
    /// </summary>
    public sealed class MainMenuAudio : MonoBehaviour
    {
        public static MainMenuAudio Instance { get; private set; }

        [Header("Music")]
        [SerializeField] private AudioClip _menuMusic;
        [SerializeField] private bool      _playMusicOnStart = true;

        [Header("SFX")]
        [SerializeField] private AudioClip _hoverSFX;
        [SerializeField] private AudioClip _clickSFX;
        [SerializeField] private AudioClip _lockedClickSFX;

        [Header("SFX Volume")]
        [Range(0f, 1f)]
        [SerializeField] private float _hoverVolume = 0.4f;
        [Range(0f, 1f)]
        [SerializeField] private float _clickVolume = 0.7f;

        [Header("Throttle")]
        [Tooltip("Hover sesleri çok hızlı tekrar çalmasın — minimum aralık (saniye)")]
        [SerializeField] private float _hoverThrottle = 0.05f;

        private float _lastHoverTime;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (_playMusicOnStart && _menuMusic != null)
                AudioManager.Instance?.PlayMusic(_menuMusic);
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void PlayHover()
        {
            if (_hoverSFX == null) return;
            if (Time.unscaledTime - _lastHoverTime < _hoverThrottle) return;

            _lastHoverTime = Time.unscaledTime;
            AudioManager.Instance?.PlaySFX(_hoverSFX, _hoverVolume, false);
        }

        public void PlayClick()
        {
            if (_clickSFX == null) return;
            AudioManager.Instance?.PlaySFX(_clickSFX, _clickVolume, false);
        }

        public void PlayLockedClick()
        {
            var clip = _lockedClickSFX != null ? _lockedClickSFX : _clickSFX;
            if (clip == null) return;
            AudioManager.Instance?.PlaySFX(clip, _clickVolume * 0.6f, false);
        }
    }
}