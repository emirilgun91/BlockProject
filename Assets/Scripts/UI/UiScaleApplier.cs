using UnityEngine;
using UnityEngine.UI;
using RogueBlockBlast.Core.Settings;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Arayüz ölçeği ayarını bir CanvasScaler'a uygular.
    /// Her Canvas'ın kök objesine ekle — başka bağlama gerekmez.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    [DisallowMultipleComponent]
    public sealed class UiScaleApplier : MonoBehaviour
    {
        private CanvasScaler _scaler;
        private float _baseScaleFactor = 1f;
        private float _baseReferenceHeight;

        private void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            _baseScaleFactor     = _scaler.scaleFactor;
            _baseReferenceHeight = _scaler.referenceResolution.y;
        }

        private void OnEnable()
        {
            GameSettings.OnAccessibilityChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            GameSettings.OnAccessibilityChanged -= Apply;
        }

        private void Apply()
        {
            if (_scaler == null) return;
            float s = GameSettings.UiScale;

            if (_scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                // Referans yüksekliğini küçültmek UI'ı büyütür.
                var r = _scaler.referenceResolution;
                r.y = _baseReferenceHeight / Mathf.Max(0.01f, s);
                _scaler.referenceResolution = r;
            }
            else
            {
                _scaler.scaleFactor = _baseScaleFactor * s;
            }
        }
    }
}
