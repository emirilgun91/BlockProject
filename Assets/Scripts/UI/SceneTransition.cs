using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Sahneler arası siyah fade geçişi.
    /// DontDestroyOnLoad — her sahnede çalışır.
    ///
    /// Kullanım:
    ///   SceneTransition.Instance.LoadScene("MainMenu");
    ///   SceneTransition.Instance.LoadScene("SampleScene", onFadeComplete: () => Debug.Log("ready"));
    ///
    /// Hierarchy:
    ///  SceneTransition (bu script)
    ///   └── TransitionCanvas (Canvas — Sort Order 999)
    ///        └── FadeImage (Image — siyah, full stretch)
    /// </summary>
    public sealed class SceneTransition : MonoBehaviour
    {
        public static SceneTransition Instance { get; private set; }

        [Header("References")]
        [SerializeField] private CanvasGroup _fadeGroup;

        [Header("Timing")]
        [SerializeField] private float _fadeOutDuration = 0.3f; // siyaha solar
        [SerializeField] private float _fadeInDuration  = 0.35f; // açılır

        private bool _isTransitioning;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Başlangıçta şeffaf
            if (_fadeGroup != null)
            {
                _fadeGroup.alpha          = 0f;
                _fadeGroup.blocksRaycasts = false;
            }
        }

        private void Start()
        {
            // Sahne açılışında fade in
            StartCoroutine(FadeIn());
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Fade out → sahne yükle → fade in.
        /// </summary>
        public void LoadScene(string sceneName, Action onFadeComplete = null)
        {
            if (_isTransitioning) return;
            StartCoroutine(TransitionRoutine(sceneName, onFadeComplete));
        }

        // ── Private ──────────────────────────────────────────────────────────

        private IEnumerator TransitionRoutine(string sceneName, Action onFadeComplete)
        {
            _isTransitioning = true;
            // Fade out — siyaha solar
            yield return StartCoroutine(FadeOut());
            onFadeComplete?.Invoke();
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
    
            // EĞER SAHNE YOKSA BURADA asyncLoad NULL DÖNER
            if (asyncLoad == null)
            {
                _fadeGroup.alpha = 0f;
                _fadeGroup.blocksRaycasts = false;
                _isTransitioning = false;
                yield break; 
            }

            yield return asyncLoad;

            // Kısa bekleme — sahne settle olsun
            yield return new WaitForSecondsRealtime(0.05f);

            // Fade in — açılır
            yield return StartCoroutine(FadeIn());

            _isTransitioning = false;
        }

        private IEnumerator FadeOut()
        {
            if (_fadeGroup == null) yield break;

            _fadeGroup.blocksRaycasts = true;

            float t = 0f;
            while (t < _fadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                _fadeGroup.alpha = Mathf.Clamp01(t / _fadeOutDuration);
                yield return null;
            }
            _fadeGroup.alpha = 1f;
        }

        private IEnumerator FadeIn()
        {
            if (_fadeGroup == null) yield break;

            _fadeGroup.alpha = 1f;

            float t = 0f;
            while (t < _fadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                _fadeGroup.alpha = 1f - Mathf.Clamp01(t / _fadeInDuration);
                yield return null;
            }

            _fadeGroup.alpha          = 0f;
            _fadeGroup.blocksRaycasts = false;
        }
    }
}