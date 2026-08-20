using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI.FX
{
    /// <summary>
    /// FX katmanlarını runtime'da enjekte etmek için yardımcılar.
    ///
    /// Enjekte edilen her obje:
    ///  - raycastTarget = false  → tıklama/hover davranışını bozmaz
    ///  - LayoutElement.ignoreLayout = true → layout/spacing'i etkilemez
    /// Böylece mevcut arayüzün yerleşimi hiç değişmez.
    /// </summary>
    public static class UIFXOverlay
    {
        /// <summary>
        /// <paramref name="parent"/> altında, parent rect'ini kaplayan bir Image üretir.
        /// <paramref name="padding"/> negatif verilirse rect dışına taşar (dış glow için).
        /// </summary>
        public static Image CreateStretched(RectTransform parent, string name, Sprite sprite, float padding = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, worldPositionStays: false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin        = Vector2.zero;
            rect.anchorMax        = Vector2.one;
            rect.pivot            = new Vector2(0.5f, 0.5f);
            rect.offsetMin        = new Vector2(padding, padding);
            rect.offsetMax        = new Vector2(-padding, -padding);
            rect.localScale       = Vector3.one;
            rect.localRotation    = Quaternion.identity;

            var image = go.GetComponent<Image>();
            image.sprite        = sprite;
            image.type          = Image.Type.Simple;
            image.raycastTarget = false;
            image.color         = new Color(1f, 1f, 1f, 0f);

            MakeLayoutNeutral(go);
            return image;
        }

        /// <summary>
        /// <paramref name="parent"/> altında, kendi boyutu olan (layout'tan bağımsız) bir Image üretir.
        /// </summary>
        public static Image CreateSized(RectTransform parent, string name, Sprite sprite, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, worldPositionStays: false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin        = new Vector2(0.5f, 0.5f);
            rect.anchorMax        = new Vector2(0.5f, 0.5f);
            rect.pivot            = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta        = size;
            rect.localScale       = Vector3.one;

            var image = go.GetComponent<Image>();
            image.sprite        = sprite;
            image.type          = Image.Type.Simple;
            image.raycastTarget = false;
            image.color         = new Color(1f, 1f, 1f, 0f);

            MakeLayoutNeutral(go);
            return image;
        }

        /// <summary>
        /// Parent rect'ini kaplayan, içeriği kırpan bir maske konteyneri üretir.
        /// Shine bandının kartın dışına taşmaması için kullanılır.
        /// </summary>
        public static RectTransform CreateMaskedContainer(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
            go.transform.SetParent(parent, worldPositionStays: false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin  = Vector2.zero;
            rect.anchorMax  = Vector2.one;
            rect.pivot      = new Vector2(0.5f, 0.5f);
            rect.offsetMin  = Vector2.zero;
            rect.offsetMax  = Vector2.zero;
            rect.localScale = Vector3.one;

            MakeLayoutNeutral(go);
            return rect;
        }

        private static void MakeLayoutNeutral(GameObject go)
        {
            var element = go.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
        }
    }
}
