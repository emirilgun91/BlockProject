using System.Collections;
using UnityEngine;
using TMPro;

namespace RogueBlockBlast.UI
{
    public sealed class ScoreView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text _scoreNumb;

        [Header("Float Animation")]
        [SerializeField] private float _floatDuration = 0.8f;
        [SerializeField] private float _floatOffsetY = 120f;
        [SerializeField] private float _floatFontSize = 48f;

        public void SetScore(int total)
        {
            if (_scoreNumb != null)
                _scoreNumb.text = total.ToString();
        }
      
        public void AddScoreGain(int newTotal, int gained)
        {
            if (_scoreNumb == null) return;
            _scoreNumb.text = newTotal.ToString();
            
            if (gained == 0) return;

          // if (!gameObject.activeInHierarchy) return;
            StartCoroutine(ScoreBounce());
            StartCoroutine(PlayFloatAnimation(gained));
            
        }
        private IEnumerator ScoreBounce()
        {
            float duration = 0.30f;
            float elapsed = 0f;

            Vector3 start = Vector3.one;
            Vector3 peak = Vector3.one * 1.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (t < 0.5f)
                {
                    float s = t / 0.5f;
                    _scoreNumb.rectTransform.localScale = Vector3.Lerp(start, peak, s);
                }
                else
                {
                    float s = (t - 0.5f) / 0.5f;
                    _scoreNumb.rectTransform.localScale = Vector3.Lerp(peak, start, s);
                }

                yield return null;
            }

            _scoreNumb.rectTransform.localScale = Vector3.one;
        }
        private IEnumerator PlayFloatAnimation(int gained)
        {
            var parent = _scoreNumb.rectTransform.parent as RectTransform;
            if (parent == null) yield break;

            var go = new GameObject("ScoreFloat");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = _scoreNumb.rectTransform.anchorMin;
            rect.anchorMax = _scoreNumb.rectTransform.anchorMax;
            rect.pivot = _scoreNumb.rectTransform.pivot;
            rect.sizeDelta = _scoreNumb.rectTransform.sizeDelta;

            var targetPos = _scoreNumb.rectTransform.anchoredPosition;
            var startPos = targetPos + new Vector2(0f, _floatOffsetY);

            rect.anchoredPosition = startPos;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = gained > 0 ? $"+{gained}" : gained.ToString();
            tmp.font = _scoreNumb.font;
            tmp.fontSize = _floatFontSize;
            tmp.color = _scoreNumb.color;
            tmp.alignment = _scoreNumb.alignment;
            tmp.raycastTarget = false;

            float elapsed = 0f;

            Vector3 startScale = Vector3.zero;
            Vector3 peakScale = Vector3.one * 1.4f;
            Vector3 endScale = Vector3.one;

            rect.localScale = startScale;

            while (elapsed < _floatDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _floatDuration);

                // overshoot easing
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
                float jitter = Random.Range(-20f, 20f);
                startPos = targetPos + new Vector2(jitter, _floatOffsetY);
                // scale pop
                if (t < 0.25f)
                {
                    float s = t / 0.25f;
                    rect.localScale = Vector3.Lerp(startScale, peakScale, s);
                }
                else
                {
                    float s = (t - 0.25f) / 0.75f;
                    rect.localScale = Vector3.Lerp(peakScale, endScale, s);
                }

                // fade
                var c = tmp.color;
                c.a = 1f - t;
                tmp.color = c;
                
                yield return null;
            }

            Destroy(go);
        }
       
    }
}
