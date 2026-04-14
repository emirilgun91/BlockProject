using System.Collections;
using TMPro;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    public enum ToastType { Default, Success, Error }

    /// <summary>
    /// Shop bildirim sistemi.
    /// Hierarchy: ShapeShopToast (bu script + CanvasGroup)
    ///   └── ToastText (TMP)
    /// </summary>
    public sealed class ShapeShopToast : MonoBehaviour
    {
        public static ShapeShopToast Instance { get; private set; }

        [SerializeField] private TMP_Text    _text;
        [SerializeField] private CanvasGroup _cg;

        [Header("Colors")]
        [SerializeField] private Color _colorDefault = Color.white;
        [SerializeField] private Color _colorSuccess = new Color(0.08f, 0.72f, 0.50f);
        [SerializeField] private Color _colorError   = new Color(0.85f, 0.25f, 0.20f);

        [SerializeField] private float _showDuration = 2.2f;
        [SerializeField] private float _fadeDuration = 0.25f;

        private Coroutine _routine;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            if (_cg != null) _cg.alpha = 0f;
        }

        public void Show(string message, ToastType type = ToastType.Default)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(message, type));
        }

        private IEnumerator ShowRoutine(string message, ToastType type)
        {
            _text.text  = message;
            _text.color = type switch
            {
                ToastType.Success => _colorSuccess,
                ToastType.Error   => _colorError,
                _                 => _colorDefault,
            };

            // Fade in
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.deltaTime;
                _cg.alpha = Mathf.Clamp01(t / _fadeDuration);
                yield return null;
            }

            yield return new WaitForSeconds(_showDuration);

            // Fade out
            t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.deltaTime;
                _cg.alpha = 1f - Mathf.Clamp01(t / _fadeDuration);
                yield return null;
            }

            _cg.alpha = 0f;
        }
    }
}