using RogueBlockBlast.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Ayarlar ▸ Show FPS. Oyun açılırken kendini kurar ve sahneler arasında
    /// yaşar; ayar kapalıyken hiçbir şey çizmez. Değer yarım saniyede bir
    /// güncellenir — her kare değişen bir sayı okunmuyordu.
    /// </summary>
    public sealed class FpsCounter : MonoBehaviour
    {
        private TMP_Text _text;
        private GameObject _panel;
        private float _accum;
        private int   _frames;
        private float _timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("[FpsCounter]");
            DontDestroyOnLoad(go);
            go.AddComponent<FpsCounter>();
        }

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;    // her şeyin üstünde
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            _panel = new GameObject("Panel", typeof(RectTransform));
            var prt = (RectTransform)_panel.transform;
            prt.SetParent(transform, false);
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(12f, -12f);
            prt.sizeDelta = new Vector2(150f, 40f);
            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            var tgo = new GameObject("Text", typeof(RectTransform));
            var trt = (RectTransform)tgo.transform;
            trt.SetParent(prt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 0f); trt.offsetMax = new Vector2(-10f, 0f);
            _text = tgo.AddComponent<TextMeshProUGUI>();
            _text.fontSize = 24f;
            _text.alignment = TextAlignmentOptions.MidlineLeft;
            _text.raycastTarget = false;
            _text.text = "-- FPS";

            _panel.SetActive(GameSettings.ShowFps);
        }

        private void Update()
        {
            bool show = GameSettings.ShowFps;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;

            _accum  += Time.unscaledDeltaTime;
            _frames += 1;
            _timer  += Time.unscaledDeltaTime;
            if (_timer < 0.5f) return;

            float fps = _frames / Mathf.Max(0.0001f, _accum);
            _text.text  = $"{Mathf.RoundToInt(fps)} FPS";
            _text.color = fps >= 55f ? new Color(0.45f, 1f, 0.6f) : fps >= 30f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.4f);
            _accum = 0f; _frames = 0; _timer = 0f;
        }
    }
}
