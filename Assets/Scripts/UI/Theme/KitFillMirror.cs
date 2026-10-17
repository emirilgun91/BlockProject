using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Doluluk çubuklarını yuvarlak uçlu çizer.
    ///
    /// <c>ComboView</c> ve <c>MilestoneView</c> bir Filled Image'ın
    /// <c>fillAmount</c> ve rengini tween'liyor. Filled Image sliced çizilemediği
    /// için yuvarlak sprite gerilip köşeleri eziliyordu. Kaynak Image kapalı
    /// (enabled = false) bırakılır — tween'ler alanlarını yine günceller — ve bu
    /// bileşen onun değerlerini sliced bir hedefe aktarır: genişlik = fillAmount,
    /// renk aynen.
    /// </summary>
    [ExecuteAlways]
    public sealed class KitFillMirror : MonoBehaviour
    {
        [SerializeField] private Image _source;
        [SerializeField] private Image _target;
        [SerializeField] private bool  _vertical;

        public void Configure(Image source, Image target, bool vertical)
        {
            _source = source; _target = target; _vertical = vertical;
            Sync();
        }

        private void LateUpdate() => Sync();

        private void Sync()
        {
            if (_source == null || _target == null) return;

            float a = Mathf.Clamp01(_source.fillAmount);
            var rt = _target.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = _vertical ? new Vector2(1f, a) : new Vector2(a, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            _target.color   = _source.color;
            _target.enabled = a > 0.001f;
        }
    }
}
