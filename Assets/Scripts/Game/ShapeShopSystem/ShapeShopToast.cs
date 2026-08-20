using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
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

        [SerializeField] private float _showDuration = 2.4f;
        [SerializeField] private float _fadeDuration = 0.25f;

        private Coroutine _routine;
        private Vector2   _baseAnchoredPos;
        private bool      _basePosCaptured;

        // Sahnede birden fazla toast var (her panelin kendi kopyası). Paneller kapalı
        // başladığı için hangisinin Awake'i önce çalışacağı belirsiz; tekini singleton
        // yapıp diğerini yok etmek, panel kapanınca Instance'ı ölü bırakıyordu.
        // Bunun yerine hepsini kaydediyoruz ve çağrı anında AÇIK olanı seçiyoruz.
        private static readonly List<ShapeShopToast> _all = new();

        private void Awake()
        {
            if (!_all.Contains(this)) _all.Add(this);
            if (Instance == null) Instance = this;

            if (_cg != null) _cg.alpha = 0f;
            if (_text != null && !_basePosCaptured)
            {
                _baseAnchoredPos = ((RectTransform)_text.transform).anchoredPosition;
                _basePosCaptured = true;
            }
        }

        private void OnDestroy()
        {
            _all.Remove(this);
            if (Instance == this)
                Instance = _all.FirstOrDefault(t => t != null);
        }

        /// <summary>
        /// Bildirimi gösterir. Bu kopya kapalıysa (paneli kapalıysa) sahnedeki
        /// açık olan toast'a devreder — çağıran tarafın hangi panelin açık olduğunu
        /// bilmesi gerekmez.
        /// </summary>
        public void Show(string message, ToastType type = ToastType.Default)
        {
            var target = ResolveUsable();
            if (target == null)
            {
                Debug.LogWarning($"[Toast] Gösterilemedi (açık toast yok): {message}", this);
                return;
            }

            if (target != this) { target.Show(message, type); return; }

            // Kendi objesi kapalıysa aç — üst panel açık olduğu sürece sorun değil
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(message, type));
        }

        /// <summary>Hiyerarşide gerçekten aktif olan ilk toast — coroutine ancak onda çalışır.</summary>
        private ShapeShopToast ResolveUsable()
        {
            if (IsUsable(this)) return this;
            return _all.FirstOrDefault(IsUsable);
        }

        private static bool IsUsable(ShapeShopToast t)
        {
            if (t == null) return false;
            // Kendisi kapalı ama ebeveyni açıksa kullanılabilir — Show() onu açar
            return t.gameObject.activeInHierarchy ||
                   (t.transform.parent != null && t.transform.parent.gameObject.activeInHierarchy);
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

            // Hatalar daha uzun kalsın — oyuncu neden olmadığını okuyabilsin
            float hold = type == ToastType.Error ? _showDuration * 1.4f : _showDuration;

            // Giriş: aşağıdan yukarı kayarak + hafif büyüyerek gelir; hata ise sarsılır
            var rt = (RectTransform)_text.transform;
            Vector2 basePos = _baseAnchoredPos;
            rt.anchoredPosition = basePos + new Vector2(0f, -30f);
            rt.localScale = Vector3.one * 0.85f;

            _cg.DOKill();
            rt.DOKill();

            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(_cg.DOFade(1f, _fadeDuration).SetUpdate(true))
               .Join(rt.DOAnchorPos(basePos, _fadeDuration * 1.4f).SetEase(Ease.OutBack).SetUpdate(true))
               .Join(rt.DOScale(1f, _fadeDuration * 1.4f).SetEase(Ease.OutBack).SetUpdate(true));

            if (type == ToastType.Error)
                seq.Append(rt.DOShakeAnchorPos(0.28f, new Vector2(14f, 0f), 12, 90f)
                             .SetUpdate(true));

            yield return seq.WaitForCompletion();
            yield return new WaitForSecondsRealtime(hold);

            // Çıkış
            var outSeq = DOTween.Sequence().SetUpdate(true);
            outSeq.Append(_cg.DOFade(0f, _fadeDuration).SetUpdate(true))
                  .Join(rt.DOAnchorPos(basePos + new Vector2(0f, 24f), _fadeDuration).SetUpdate(true));
            yield return outSeq.WaitForCompletion();

            _cg.alpha = 0f;
            rt.anchoredPosition = basePos;
            rt.localScale = Vector3.one;
        }
    }
}
