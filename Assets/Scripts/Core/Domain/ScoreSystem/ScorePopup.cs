using System.Collections;
using TMPro;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    public sealed class ScorePopup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text _text;

        [Header("Animation")]
        [SerializeField] private float _riseHeight   = 40f;
        [SerializeField] private float _riseDuration = 0.25f;
        [SerializeField] private float _flyDuration  = 0.45f;
        [SerializeField] private float _scatterRange = 20f;

        private RectTransform _rect;
        private Coroutine     _routine;
        private Canvas        _canvas;

        private void Awake()
        {
            _rect   = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            if (_text == null)
                _text = GetComponentInChildren<TMP_Text>();
        }

        public void Launch(
            Vector3       worldPos,
            RectTransform targetRect,
            Camera        cam,
            float         value,
            Color         color,
            float         delay    = 0f,
            System.Action onArrive = null)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Animate(worldPos, targetRect, cam, value, color, delay, onArrive));
        }

        public void Cancel()
        {
            if (_routine != null) StopCoroutine(_routine);
            ScorePopupPool.Instance?.Return(this);
        }

        private IEnumerator Animate(
            Vector3       worldPos,
            RectTransform targetRect,
            Camera        cam,
            float         value,
            Color         color,
            float         delay,
            System.Action onArrive)
        {
            gameObject.SetActive(true);
            _text.text  = $"+{value:0}";
            _text.color = new Color(color.r, color.g, color.b, 0f);

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            // Başlangıç pozisyonu — çalışan manuel formül
            Vector2 startCanvas = ToCanvasPos(cam.WorldToScreenPoint(worldPos));
            startCanvas += new Vector2(
                Random.Range(-_scatterRange, _scatterRange),
                Random.Range(-_scatterRange * 0.3f, _scatterRange * 0.3f)
            );
            _rect.anchoredPosition = startCanvas;

            // 1. Tile üzerinde yükseliş
            float   elapsed    = 0f;
            Vector2 riseTarget = startCanvas + new Vector2(0f, _riseHeight);

            while (elapsed < _riseDuration)
            {
                elapsed += Time.deltaTime;
                float t    = Mathf.Clamp01(elapsed / _riseDuration);
                float ease = 1f - (1f - t) * (1f - t);

                _rect.anchoredPosition = Vector2.Lerp(startCanvas, riseTarget, ease);

                var c = _text.color;
                c.a = Mathf.Clamp01(t * 3f);
                _text.color = c;

                yield return null;
            }

            // World Space Canvas için gerçek kamera ile screen koordinatına çevir
            Vector2 targetScreenPos = RectTransformUtility.WorldToScreenPoint(cam, targetRect.position);
            Vector2 flyTarget       = ToCanvasPos(targetScreenPos);

            Vector2 flyStart = _rect.anchoredPosition;
            elapsed = 0f;

            while (elapsed < _flyDuration)
            {
                elapsed += Time.deltaTime;
                float t    = Mathf.Clamp01(elapsed / _flyDuration);
                float ease = t * t;

                _rect.anchoredPosition = Vector2.Lerp(flyStart, flyTarget, ease);
                _rect.localScale       = Vector3.one * Mathf.Lerp(1f, 0.3f, ease);

                var c = _text.color;
                c.a = Mathf.Clamp01(1f - (t - 0.7f) / 0.3f);
                _text.color = c;

                yield return null;
            }

            onArrive?.Invoke();
            yield return new WaitForSeconds(0.05f);
            _rect.localScale = Vector3.one;
            ScorePopupPool.Instance?.Return(this);
        }
        
        private Vector2 ToCanvasPos(Vector2 screenPos)
        {
            if (_canvas == null) return screenPos;
            var     canvasRect  = _canvas.transform as RectTransform;
            Vector2 canvasSize  = canvasRect.sizeDelta;
            Vector2 screenSize  = new Vector2(Screen.width, Screen.height);
            Vector2 scaleFactor = canvasSize / screenSize;
            return (screenPos - screenSize * 0.5f) * scaleFactor;
        }
    }
}