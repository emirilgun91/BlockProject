using RogueBlockBlast.Core;
using RogueBlockBlast.Core.Settings;
using RogueBlockBlast.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI.Juice
{
    /// <summary>
    /// Milestone barına "kazandım" hissi katar.
    ///
    /// Asıl fikir: dolan bir bar tek başına ne kadar kazanıldığını söylemez.
    /// Burada <b>kazanılan dilim</b> gösterilir.
    ///
    /// Nasıl: barın <i>arkasına</i> ikinci bir dolgu konur ve puan gelir gelmez
    /// <b>anında</b> yeni değere atlar. Gerçek dolgu ise her zamanki gibi
    /// yumuşak animasyonla ona doğru büyür. Aradaki fark — yani tam olarak yeni
    /// kazanılan miktar — parlak görünür ve gerçek dolgu onu yutarken kapanır.
    /// Maskeleme gerekmez; çizim sırası işi kendiliğinden yapar.
    ///
    /// Buna barın ucunda ilerlemeyi takip eden bir hale eşlik eder. Etkinin
    /// şiddeti <b>kazanç miktarıyla</b> ölçeklenir: küçük kazanç sessiz kalır,
    /// büyük kazanç belirgin olur. "Abartısız ama belirgin" dengesi buradan gelir.
    ///
    /// Bar ölçeğine dokunulmaz — <see cref="MilestoneView"/> milestone anında
    /// <c>_fillBarRect</c>'i DOTween ile punch'lıyor, ikinci bir yazıcı onu ezerdi.
    /// </summary>
    public sealed class MilestoneGain : MonoBehaviour
    {
        [Header("Hedef")]
        [Tooltip("Milestone barının dolgu Image'ı. Boşsa bu nesnedeki Image.")]
        [SerializeField] private Image _fill;

        [Header("Kazanç parlaması")]
        [Tooltip("Kazanılan dilimin ne kadar parlayacağı.")]
        [SerializeField] private Color _flashColor = new Color(1f, 0.95f, 0.72f, 1f);

        [Tooltip("Parlamanın sönme süresi. Barın dolum süresinden biraz uzun olmalı " +
                 "ki dolgu dilimi yutarken parlama hâlâ görünsün.")]
        [SerializeField] private float _flashFade = 0.55f;

        [Header("Uç halesi")]
        [SerializeField] private bool  _showEdgeGlow  = true;
        [SerializeField] private Color _edgeColor     = new Color(1f, 0.86f, 0.45f, 1f);
        [Tooltip("Halenin çapı, barın yüksekliğine oran olarak.")]
        [SerializeField] private float _edgeSizeFactor = 2.4f;
        [SerializeField] private float _edgeIdleAlpha  = 0.20f;
        [SerializeField] private float _edgePeakAlpha  = 0.62f;

        [Header("Boşta nefes")]
        [Tooltip("Hiçbir şey olmazken de barın ucu çok hafif nefes alır — " +
                 "sabit duran bir bar ölü görünüyor. Küçük tutun.")]
        [SerializeField] private float _idleBreath      = 0.07f;
        [SerializeField] private float _idleBreathSpeed = 0.65f;

        [Header("Şiddet")]
        [Tooltip("Bu kadarlık bir kazanç (bar oranı) etkiyi tam güce çıkarır. " +
                 "Küçük tutmak her puanı abartır, büyük tutmak etkiyi köreltir.")]
        [SerializeField] private float _gainForFullPunch = 0.18f;

        [Tooltip("Etkinin sönme hızı.")]
        [SerializeField] private float _decaySpeed = 2.6f;

        private RunController _run;
        private bool          _bound;

        private Image         _flash;
        private RectTransform _edge;
        private Image         _edgeImage;

        private float _lastTarget = -1f;
        private float _punch;          // 0..1, kazançta 1'e sıçrar, söner
        private float _flashAlpha;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_fill == null) _fill = GetComponent<Image>();
            if (_fill == null)
            {
                Debug.LogWarning("[MilestoneGain] Dolgu Image'ı yok — devre dışı.");
                enabled = false;
                return;
            }

            BuildFlash();
            if (_showEdgeGlow) BuildEdge();
        }

        private void OnDestroy() => Unbind();

        private void Update()
        {
            if (!_bound) TryBind();

            float dt  = Time.unscaledDeltaTime;
            float vfx = Mathf.Clamp01(GameSettings.VfxIntensity);

            _punch      = Mathf.MoveTowards(_punch, 0f, dt * _decaySpeed);
            _flashAlpha = Mathf.MoveTowards(
                _flashAlpha, 0f, dt / Mathf.Max(0.01f, _flashFade));

            ApplyFlash(vfx);
            ApplyEdge(vfx);
        }

        // ── Bağlanma ─────────────────────────────────────────────────────────

        private void TryBind()
        {
            if (_run == null) _run = FindFirstObjectByType<RunController>();

            var milestone = _run != null ? _run.Milestone : null;
            if (milestone == null) return;

            milestone.OnProgressChanged += HandleProgress;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound || _run == null || _run.Milestone == null) return;
            _run.Milestone.OnProgressChanged -= HandleProgress;
            _bound = false;
        }

        /// <summary>
        /// Hedef oran <see cref="MilestoneView"/> ile aynı kaynaktan okunur
        /// (<c>ScoreFillRatio</c>), böylece parlama gerçek dolguyla birebir
        /// hizalanır.
        /// </summary>
        private void HandleProgress(MilestoneProgressState state)
        {
            float target = Mathf.Clamp01(state.ScoreFillRatio);

            // İlk çağrı referans alınır — açılışta parlama yaşanmasın.
            if (_lastTarget < 0f)
            {
                _lastTarget = target;
                if (_flash != null) _flash.fillAmount = target;
                return;
            }

            float gain = target - _lastTarget;
            _lastTarget = target;

            // Milestone geçişinde bar sıfırlanır; düşüş parlama sayılmaz.
            if (gain <= 0.0005f)
            {
                if (_flash != null) _flash.fillAmount = target;
                return;
            }

            // Kazanılan dilim: parlama anında yeni değere atlar, gerçek dolgu
            // ona doğru yumuşakça büyür. Arada kalan fark parlak görünür.
            if (_flash != null) _flash.fillAmount = target;

            float strength = Mathf.Clamp01(gain / Mathf.Max(0.001f, _gainForFullPunch));

            // Taban 0.45 fazla yüksekti: en küçük puan bile barı yakıyordu.
            // Küçük kazanç artık fark edilir ama sessiz kalır.
            _punch      = Mathf.Max(_punch, strength);
            _flashAlpha = Mathf.Max(_flashAlpha, Mathf.Lerp(0.22f, 0.75f, strength));
        }

        // ── Uygulama ─────────────────────────────────────────────────────────

        private void ApplyFlash(float vfx)
        {
            if (_flash == null) return;

            float a = _flashAlpha * vfx;
            if (GameSettings.ReduceMotion) a *= 0.6f;   // kalır ama daha sakin

            _flash.color   = new Color(_flashColor.r, _flashColor.g, _flashColor.b, a);
            _flash.enabled = a > 0.004f;
        }

        private void ApplyEdge(float vfx)
        {
            if (_edge == null || _edgeImage == null) return;

            float f = _fill.fillAmount;

            // Boş ve dolu uçlarda hale anlamsız.
            bool visible = f > 0.01f && f < 0.999f && vfx > 0.001f;
            _edgeImage.enabled = visible;
            if (!visible) return;

            // Anchor'ı doldurma oranına yazmak, barın kendi yerleşiminden
            // bağımsız olarak ucu tam yakalar.
            _edge.anchorMin = new Vector2(f, 0.5f);
            _edge.anchorMax = new Vector2(f, 0.5f);
            _edge.anchoredPosition = Vector2.zero;

            // Boşta çok hafif nefes — kazanç sırasında susar, punch öne çıksın.
            float breath = 0f;
            if (!GameSettings.ReduceMotion && _idleBreath > 0f)
            {
                breath = Mathf.Sin(Time.unscaledTime * _idleBreathSpeed * Mathf.PI * 2f)
                         * _idleBreath * (1f - _punch);
            }

            float height = _fill.rectTransform.rect.height;
            float size   = Mathf.Max(6f, height * _edgeSizeFactor);
            float pop    = GameSettings.ReduceMotion ? 0f : _punch * 0.28f;

            _edge.sizeDelta = Vector2.one * (size * (1f + pop + breath * 0.5f));

            float alpha = (Mathf.Lerp(_edgeIdleAlpha, _edgePeakAlpha, _punch) + breath) * vfx;
            _edgeImage.color = new Color(
                _edgeColor.r, _edgeColor.g, _edgeColor.b, Mathf.Max(0f, alpha));
        }

        // ── Kurulum ──────────────────────────────────────────────────────────

        /// <summary>
        /// Parlama katmanı dolgunun <b>kardeşi</b> olmalı ve ondan önce
        /// çizilmeli. Çocuğu olsaydı dolgunun üstüne biner ve kazanılan dilim
        /// yerine tüm barı parlatırdı.
        /// </summary>
        private void BuildFlash()
        {
            var parent = _fill.rectTransform.parent as RectTransform;
            if (parent == null) return;

            var go = new GameObject("GainFlash", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.SetSiblingIndex(_fill.rectTransform.GetSiblingIndex());

            var rect = go.GetComponent<RectTransform>();
            var src  = _fill.rectTransform;

            rect.anchorMin        = src.anchorMin;
            rect.anchorMax        = src.anchorMax;
            rect.pivot            = src.pivot;
            rect.anchoredPosition = src.anchoredPosition;
            rect.sizeDelta        = src.sizeDelta;
            rect.localScale       = src.localScale;

            _flash = go.AddComponent<Image>();

            // Dolgunun şeklini birebir taşı — yoksa parlama farklı biçimde kırpılır.
            _flash.sprite         = _fill.sprite;
            _flash.type           = _fill.type;
            _flash.fillMethod     = _fill.fillMethod;
            _flash.fillOrigin     = _fill.fillOrigin;
            _flash.fillClockwise  = _fill.fillClockwise;
            _flash.preserveAspect = _fill.preserveAspect;
            _flash.fillAmount     = _fill.fillAmount;

            _flash.raycastTarget = false;
            _flash.enabled       = false;
        }

        private void BuildEdge()
        {
            var go = new GameObject("GainEdgeGlow", typeof(RectTransform));
            go.transform.SetParent(_fill.rectTransform, worldPositionStays: false);
            go.transform.SetAsLastSibling();

            _edge = go.GetComponent<RectTransform>();
            _edge.pivot = new Vector2(0.5f, 0.5f);

            go.AddComponent<LayoutElement>().ignoreLayout = true;

            _edgeImage = go.AddComponent<Image>();
            _edgeImage.sprite        = JuiceGraphics.RadialGlow;
            _edgeImage.raycastTarget = false;
            _edgeImage.color         = new Color(_edgeColor.r, _edgeColor.g, _edgeColor.b, _edgeIdleAlpha);
        }
    }
}
