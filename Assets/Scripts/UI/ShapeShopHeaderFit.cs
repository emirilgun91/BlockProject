using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Shape Shop başlık satırını panelin gerçek genişliğine uydurur.
    ///
    /// Satırın HorizontalLayoutGroup'u çocuk genişliklerini kontrol etmiyor; başlık
    /// bloğu yatay çözünürlükte (1920) ölçülüp sahneye gömülmüştü. Panel dar bir
    /// canvas'ta (portrait, 1080) açılınca başlık hâlâ ~1450 px geniş kalıyor ve
    /// bakiye göstergesi ekran dışına itiliyordu. Başlık, geri butonu ile bakiye
    /// arasında kalan boşluğu dolduracak şekilde yeniden boyutlanır.
    ///
    /// Header objesine eklenir; çocuklar verilmezse adlarıyla bulunur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShapeShopHeaderFit : MonoBehaviour
    {
        [SerializeField] private RectTransform _back;
        [SerializeField] private RectTransform _title;
        [SerializeField] private RectTransform _coin;

        private HorizontalLayoutGroup _layout;
        private float _lastWidth = -1f;

        private void OnEnable() => Fit();

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled) Fit();
        }

        private void Fit()
        {
            var self = (RectTransform)transform;
            if (_layout == null) _layout = GetComponent<HorizontalLayoutGroup>();
            if (_back  == null) _back  = self.Find("BackButton")   as RectTransform;
            if (_title == null) _title = self.Find("TitleBlock")   as RectTransform;
            if (_coin  == null) _coin  = self.Find("CoinDisplay")  as RectTransform;
            if (_layout == null || _back == null || _title == null || _coin == null) return;

            float width = self.rect.width;
            if (Mathf.Approximately(width, _lastWidth)) return;
            _lastWidth = width;

            float free = width - _layout.padding.left - _layout.padding.right
                       - _layout.spacing * 2f - _back.rect.width - _coin.rect.width;
            _title.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(free, 120f));
            LayoutRebuilder.MarkLayoutForRebuild(self);
        }
    }
}
