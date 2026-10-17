using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// UI kiti butonu: hover'da hafif büyür, dolgu ve kenar rengi yumuşakça geçer,
    /// basınca içe çöker. Button'ın kendi geçişi kapalıdır (transition None); renkleri
    /// bu bileşen sürer, çünkü dolgu ve kenar iki ayrı Image ve ColorTint yalnızca
    /// birini boyayabiliyor.
    ///
    /// Pause ve Game Over <c>timeScale = 0</c> iken açık olduğundan her şey
    /// <see cref="Time.unscaledDeltaTime"/> ile ilerler.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class KitButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Image    _fill;
        [SerializeField] private Image    _rim;
        [SerializeField] private TMP_Text _label;

        [SerializeField] private Color _fillNormal = Color.white;
        [SerializeField] private Color _fillHover  = Color.white;
        [SerializeField] private Color _rimNormal  = Color.white;
        [SerializeField] private Color _rimHover   = Color.white;

        [SerializeField] private float _hoverScale = 1.03f;
        [SerializeField] private float _pressScale = 0.97f;
        [SerializeField] private float _speed      = 16f;

        private Button _button;
        private bool   _hover, _down, _selected;
        private float  _h;                      // 0 = normal, 1 = hover
        private Color  _labelBase;

        public void Configure(Image fill, Image rim, TMP_Text label,
                              Color fillNormal, Color fillHover, Color rimNormal, Color rimHover)
        {
            _fill = fill; _rim = rim; _label = label;
            _fillNormal = fillNormal; _fillHover = fillHover;
            _rimNormal  = rimNormal;  _rimHover  = rimHover;
            Apply(0f, true);
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_label != null) _labelBase = _label.color;
        }

        private void OnEnable()
        {
            _hover = _down = _selected = false;
            _h = 0f;
            transform.localScale = Vector3.one;
            Apply(0f, IsInteractable());
        }

        private void Update()
        {
            bool live = IsInteractable();
            bool lit  = live && (_hover || _selected);
            _h = Mathf.MoveTowards(_h, lit ? 1f : 0f, Time.unscaledDeltaTime * _speed * 0.6f);

            float target = !live ? 1f : _down ? _pressScale : lit ? _hoverScale : 1f;
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-_speed * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);

            Apply(_h, live);
        }

        private void Apply(float h, bool live)
        {
            float dim = live ? 1f : 0.4f;
            if (_fill != null) _fill.color = Fade(Color.Lerp(_fillNormal, _fillHover, h), dim);
            if (_rim  != null) _rim.color  = Fade(Color.Lerp(_rimNormal,  _rimHover,  h), dim);
            if (_label != null)
            {
                if (_labelBase.a <= 0f) _labelBase = _label.color;
                _label.color = Fade(_labelBase, dim);
            }
        }

        private static Color Fade(Color c, float k) { c.a *= k; return c; }

        private bool IsInteractable()
        {
            if (_button == null) _button = GetComponent<Button>();
            return _button != null && _button.IsInteractable();
        }

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e)  { _hover = false; _down = false; }
        public void OnPointerDown(PointerEventData e)  { if (e.button == PointerEventData.InputButton.Left) _down = true; }
        public void OnPointerUp(PointerEventData e)    => _down = false;
        public void OnSelect(BaseEventData e)          => _selected = true;
        public void OnDeselect(BaseEventData e)        => _selected = false;
    }
}
