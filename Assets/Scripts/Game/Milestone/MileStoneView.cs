using DG.Tweening;
using RogueBlockBlast.Core.Localization;
using RogueBlockBlast.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Milestone progress bar UI.
    ///
    /// Hierarchy (MilestoneBoard altında):
    ///  MilestoneBoard
    ///   ├── Label          (TMP — "NEXT CARD")
    ///   ├── ProgressBar
    ///   │    ├── BG        (Image — arka plan)
    ///   │    └── Fill      (Image — dolan kısım)
    ///   ├── ThresholdText  (TMP — "15000")
    ///   └── PiecesText     (TMP — "28 left")  ← opsiyonel
    ///
    /// RunController'dan:
    ///   milestoneView.Bind(milestoneSystem);
    /// </summary>
    public sealed class MilestoneView : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Progress Bar")]
        [SerializeField] private Image         _fill;
        [SerializeField] private RectTransform _fillBarRect;  // Fill veya ProgressBar rect — punch için
        [SerializeField] private float         _fillDuration = 0.3f;

        [Header("Milestone Pop")]
        [Tooltip("Milestone anında barın dikey şişme oranı — TABAN ölçeğin katı.\n\n" +
                 "Eskiden mutlak değer yazılıyordu (0.0045 / 0.002); o sayılar eski " +
                 "skin'in bar ölçeğine göre elle ayarlanmıştı ve panel yeniden " +
                 "skinlenince anlamlarını yitirdi. Kat olarak tutmak skin'den bağımsız kılar.")]
        [SerializeField] private float _milestonePopScale = 1.25f;

        [Tooltip("Şişmeden sonra dönülecek oran — 1 = taban ölçek.")]
        [SerializeField] private float _milestoneSettleScale = 1f;

        // Taban ölçek ilk kullanımda yakalanır; sahnedeki değer neyse o.
        private Vector3 _fillBarBaseScale = Vector3.one;
        private bool    _fillBarBaseCaptured;

        [Header("Text")]
        [SerializeField] private TMP_Text _thresholdText;
        [SerializeField] private TMP_Text _piecesText;       // opsiyonel

        /// <summary>"N left" sayacının rect'i — arayüz turu işaretlemek için.</summary>
        public RectTransform PiecesRect => _piecesText != null ? _piecesText.rectTransform : null;

        [Header("Colors")]
        [SerializeField] private Color _colorNormal  = new Color(0.91f, 0.64f, 0.19f, 1f); // amber
        [SerializeField] private Color _colorDanger  = new Color(0.75f, 0.24f, 0.17f, 1f); // kırmızı — az kaldı
        [SerializeField] private Color _colorComplete = new Color(0.08f, 0.72f, 0.60f, 1f); // teal — milestone!
        
        [Header("Danger Threshold")]
        [Tooltip("Pool'un yüzde kaçı dolunca renk kırmızıya döner.")]
        [Range(0f, 1f)]
        [SerializeField] private float _dangerRatio = 0.80f;
        
        [Header("New Card Notification")]
        [SerializeField] private TMP_Text _newCardText;        // "New Card Earned!" yazacak TMP
        [SerializeField] private float    _newCardShowDuration = 2.5f;

        [Header("Title")]
        [Tooltip("Panelin 'MILESTONE' başlığı. Sahnede gömülü metin yerine " +
                 "buradan yazılır ki dil değişiminde yenilenebilsin.")]
        [SerializeField] private TMP_Text _titleText;

        [SerializeField] private AudioClip MilestoneReach;
        // ── Runtime ──────────────────────────────────────────────────────────
        private MilestoneSystem _system;
        private Sequence        _milestoneSequence;
        private Sequence        _newCardSequence;

        // Dil değişiminde metinleri yeniden yazabilmek için son durum saklanır.
        // Aksi hâlde başlık ve "N kaldı" bir sonraki yerleştirmeye kadar eski
        // dilde kalırdı — başlık ise hiç güncellenmezdi.
        private MilestoneProgressState _lastState;
        private bool                   _hasState;

        // ── Public API ───────────────────────────────────────────────────────
        public void Bind(MilestoneSystem system)
        {
            if (_system != null)
                _system.OnProgressChanged -= HandleProgressChanged;

            _system = system;
            _system.OnProgressChanged += HandleProgressChanged;

            Loc.OnChanged -= HandleLanguageChanged;
            Loc.OnChanged += HandleLanguageChanged;

            // Başlık ilk durum gelmeden de doğru dilde görünsün.
            if (_titleText != null)
                _titleText.text = Loc.Get("Milestone.Title");
        }

        private void OnDestroy()
        {
            if (_system != null)
                _system.OnProgressChanged -= HandleProgressChanged;

            Loc.OnChanged -= HandleLanguageChanged;

            _milestoneSequence?.Kill();
            _newCardSequence?.Kill();
        }

        // ── Classic Mode ─────────────────────────────────────────────────────
        // Classic'te milestone yok; aynı pano rekoru gösterir. Rekor geçilince
        // başlık "YENİ REKOR" olur ve sayı canlı skoru izler.
        [Header("Classic")]
        [SerializeField] private Color _colorClassicRecord = new Color(0.30f, 0.86f, 0.55f, 1f);

        private bool  _classic;
        private int   _classicBest;
        private int   _classicScore;
        private bool  _classicBeaten;
        private Color _classicNumberColor;

        /// <summary>Panoyu Classic'e çevirir: bar ve kalan parça sayacı gizlenir, rekor yazılır.</summary>
        public void ShowClassic(int bestScore)
        {
            _classic       = true;
            _classicBest   = bestScore;
            _classicScore  = 0;
            _classicBeaten = false;

            // Bar: MilestoneBoard'un doğrudan çocuğu olan kabıyla birlikte gizlenir
            for (var t = _fillBarRect != null ? _fillBarRect : _fill != null ? _fill.transform : null; t != null; t = t.parent)
                if (t.parent == transform) { t.gameObject.SetActive(false); break; }

            // Kalan parça sayacı ve halkası (portrait'te havuzun yanına taşınmış olabilir)
            if (_piecesText != null)
            {
                var ring = _piecesText.transform.parent != null ? _piecesText.transform.parent.Find("Image") : null;
                if (ring != null) ring.gameObject.SetActive(false);
                _piecesText.gameObject.SetActive(false);
            }

            if (_thresholdText != null) _classicNumberColor = _thresholdText.color;

            Loc.OnChanged -= HandleLanguageChanged;
            Loc.OnChanged += HandleLanguageChanged;
            UpdateClassicTexts();
        }

        /// <summary>Her yerleştirmeden sonra; rekor geçildiği an küçük bir kutlama yapar.</summary>
        public void SetClassicScore(int score)
        {
            if (!_classic) return;
            _classicScore = score;
            if (!_classicBeaten && _classicBest > 0 && score > _classicBest)
            {
                _classicBeaten = true;
                if (_thresholdText != null)
                {
                    _thresholdText.transform.DOKill(true);
                    _thresholdText.transform.DOPunchScale(Vector3.one * 0.25f, 0.45f, 6, 0.6f);
                }
            }
            UpdateClassicTexts();
        }

        private void UpdateClassicTexts()
        {
            if (_titleText != null)
                _titleText.text = Loc.Get(_classicBeaten ? "Classic.NewBest" : "Classic.Best");

            if (_thresholdText != null)
            {
                int shown = _classicBeaten ? _classicScore : _classicBest;
                _thresholdText.text  = shown > 0 ? shown.ToString("N0") : "—";
                _thresholdText.color = _classicBeaten ? _colorClassicRecord : _classicNumberColor;
            }
        }

        private void HandleLanguageChanged()
        {
            if (_classic) { UpdateClassicTexts(); return; }
            if (_hasState) UpdateTexts(_lastState);
            else if (_titleText != null) _titleText.text = Loc.Get("Milestone.Title");
        }

        // ── Handler ──────────────────────────────────────────────────────────
        private void HandleProgressChanged(MilestoneProgressState state)
        {
            _lastState = state;
            _hasState  = true;

            UpdateFill(state);
            UpdateTexts(state);
        }

        // ── Fill ─────────────────────────────────────────────────────────────
        private void UpdateFill(MilestoneProgressState state)
        {
            if (_fill == null) return;

            float targetFill = state.ScoreFillRatio;

            // Renk — normal / danger / milestone tamamlandı
            Color targetColor;
            if (state.AllCleared)
                targetColor = _colorComplete;
            else if (targetFill >= _dangerRatio)
                targetColor = _colorDanger;
            else
                targetColor = _colorNormal;

            // Fill amount smooth
            _fill.DOFillAmount(targetFill, _fillDuration).SetEase(Ease.OutQuad);
            _fill.DOColor(targetColor, _fillDuration * 0.5f);
        }

        // ── Texts ─────────────────────────────────────────────────────────────
        private void UpdateTexts(MilestoneProgressState state)
        {
            // Başlık sabit bir etiket ama dil değişince yenilenmeli; bu yüzden
            // sahnede gömülü metin yerine burada yazılıyor.
            if (_titleText != null)
                _titleText.text = Loc.Get("Milestone.Title");

            if (_thresholdText != null)
            {
                // "MAX" üç yerde geçiyor (upgrade paneli, shape shop, burası) —
                // hepsi tek anahtarı paylaşır.
                _thresholdText.text = state.AllCleared
                    ? Loc.Get("Common.Max")
                    : state.NextThreshold.ToString("N0");
            }

            if (_piecesText != null)
            {
                // Sayı ile kelimenin sırası dile göre değişiyor
                // ("{0} kaldı" ama "pozostało {0}"), o yüzden biçim dizesi
                // çeviriden geliyor — string birleştirme yapılmaz.
                _piecesText.text = state.AllCleared
                    ? string.Empty
                    : Loc.Get("Milestone.PiecesLeft", state.PiecesRemaining);
            }
        }

        public void ShowNewCardEarned(string cardName)
        {
            if (_newCardText == null) return;

            _newCardSequence?.Kill();

            // Zengin metin sarmalayıcısı kodda kalır, çeviriye yalnızca cümle
            // girer — CSV'ye rich text koymak çevirmenin bozabileceği bir yüzey açar.
            _newCardText.text  = $"{Loc.Get("Milestone.NewCard")}\n<size=80%>{cardName}</size>";
            _newCardText.alpha = 0f;
            _newCardText.gameObject.SetActive(true);

            _newCardSequence = DOTween.Sequence()
                .Append(_newCardText.DOFade(1f, 0.3f))
                .AppendInterval(_newCardShowDuration)
                .Append(_newCardText.DOFade(0f, 0.4f))
                .AppendCallback(() => _newCardText.gameObject.SetActive(false))
                .SetAutoKill(true);
        }

 
        /// <summary>
        /// Barın taban ölçeğini bir kez yakalar. Animasyon sırasında okunursa
        /// şişmiş hâli taban sanılır ve bar her milestone'da biraz daha büyür.
        /// </summary>
        private void CaptureFillBarBaseScale()
        {
            if (_fillBarBaseCaptured || _fillBarRect == null) return;
            _fillBarBaseScale    = _fillBarRect.localScale;
            _fillBarBaseCaptured = true;
        }

        /// <summary>Milestone sesi — zafer ekranı başlık çakılırken aynısını kullanır.</summary>
        public AudioClip MilestoneReachClip => MilestoneReach;

        public void PlayMilestoneReachedFX()
        {
            AudioManager.Instance.PlaySFX(MilestoneReach);
            if (_fill == null) return;

            // Önceki tüm animasyonları temizle ki çakışma olmasın
            _milestoneSequence?.Kill();
            _fill.DOKill();
            if (_fillBarRect != null) _fillBarRect.DOKill();

            _milestoneSequence = DOTween.Sequence();

            // 1. TAMAMLANMA (ZORUNLU DOLDURMA)
            // Ne kadar kalmış olursa olsun, önce barı hızla %100'e (1f) çek.
            // Bu sayede efekt her zaman bar tam doluyken yaşanır.
            _milestoneSequence.Append(_fill.DOFillAmount(1f, 0.45f).SetEase(Ease.OutQuad));

            // 2. BÜYÜME (POP) VE TEAL'E GEÇİŞ
            // Bar dolduğu an şişme ve Teal renge geçiş başlar.
            if (_fillBarRect != null)
            {
                CaptureFillBarBaseScale();
                _milestoneSequence.Append(
                    _fillBarRect.DOScaleY(_fillBarBaseScale.y * _milestonePopScale, 0.3f)
                                .SetEase(Ease.OutBack, 0.8f));
            }
            else
            {
                _milestoneSequence.AppendInterval(0.3f);
            }
            _milestoneSequence.Join(_fill.DOColor(_colorComplete, 0.2f).SetEase(Ease.OutQuad));

            // 3. ZİRVEDE PARLAMA (BEYAZ BURST)
            _milestoneSequence.Append(_fill.DOColor(Color.white, 0.15f).SetEase(Ease.OutCubic));

            // 4. ASILI KALMA (TADINI ÇIKARMA)
            // Şişkin ve bembeyaz haldeyken çok kısa beklet ki oyuncu "Başardım!" hissini alsın.
            _milestoneSequence.AppendInterval(0.25f);

            // 5. TAHLİYE, KÜÇÜLME VE NORMALE DÖNME
            // Bar sıfıra akar, boyutu küçülür ve rengi aslına döner.
            if (_fillBarRect != null)
            {
                _milestoneSequence.Append(
                    _fillBarRect.DOScaleY(_fillBarBaseScale.y * _milestoneSettleScale, 0.4f)
                                .SetEase(Ease.InOutQuad));
            }
            else
            {
                _milestoneSequence.AppendInterval(0.4f);
            }
    
            _milestoneSequence.Join(_fill.DOFillAmount(0f, 0.5f).SetEase(Ease.InOutSine));
            _milestoneSequence.Join(_fill.DOColor(_colorNormal, 0.4f).SetEase(Ease.InOutQuad));

            _milestoneSequence.SetAutoKill(true);
        }
    }
}