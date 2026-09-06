using RogueBlockBlast.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.Game.Tutorial
{
    /// <summary>
    /// İlk run'da çalışan senaryo tabanlı öğretici.
    ///
    /// Tasarım kararı: modal pencere, el işareti, oyunu durdurma yok. Oyuncu
    /// normal oynar; ekranın üstündeki tek satırlık şerit sıradaki hedefi söyler
    /// ve hedef gerçekleştiğinde kendiliğinden ilerler. Yanlış bir şey yapmak
    /// mümkün değil, bu yüzden "yanlış" durumu da yok — bu, blok-blast gibi
    /// kendini anlatan bir oyunda öğreticinin oyunu kesmemesini sağlıyor.
    ///
    /// Adımlar oyunun gerçek olaylarına bağlı (TutorialEvents). Yani öğretici
    /// oyunu taklit etmiyor, izliyor: oyuncu ipucunu okumadan doğru şeyi yaptıysa
    /// adım yine tamamlanır ve öğretici hızla önden çekilir.
    ///
    /// Sahne kurulumu: Gameplay Canvas'a bu bileşeni ekle, gerisi kodda kurulur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialController : MonoBehaviour
    {
        public const string CompletedKey = "Tutorial_Completed";

        /// <summary>Öğretici bir daha gösterilmesin diye işaretlendi mi?</summary>
        public static bool IsCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;

        /// <summary>Ana menüdeki "ilerlemeyi sıfırla" akışı için.</summary>
        public static void ResetProgress() => PlayerPrefs.DeleteKey(CompletedKey);

        [Header("Behaviour")]
        [Tooltip("AÇIK: öğretici daha önce bitirilmiş olsa da her run'da baştan oynar. " +
                 "Yalnızca geliştirme için.")]
        [SerializeField] private bool _alwaysShow = false;

        [Tooltip("Bir adım tamamlandığında tik işaretinin ekranda kalma süresi (saniye).")]
        [SerializeField] private float _completedHold = 1.1f;

        [Header("Placement")]
        [Tooltip("Canvas'ın üst kenarından uzaklık (referans çözünürlük pikseli).")]
        [SerializeField] private float _topPadding = 40f;

        [Header("Style")]
        [SerializeField] private Color _panelColor = new Color(0.05f, 0.07f, 0.12f, 0.86f);
        [SerializeField] private Color _textColor  = new Color(0.88f, 0.92f, 0.98f, 1f);
        [SerializeField] private Color _doneColor  = new Color(0.35f, 0.92f, 0.55f, 1f);
        [SerializeField] private float _fontSize   = 30f;

        // ── Senaryo ──────────────────────────────────────────────────────────

        private enum Trigger
        {
            Placed,          // herhangi bir parça yerleşti
            Rotated,         // Q / E ile döndürüldü
            LineCleared,     // en az bir satır/sütun temizlendi
            ComboAboveBase,  // combo çarpanı taban değerin üstüne çıktı
            Milestone,       // hedef skora ulaşıldı
            CardPicked,      // kart seçildi
            Timer            // sadece bilgi — süre dolunca geçer
        }

        private readonly struct Step
        {
            public readonly string  Key;
            public readonly string  Fallback;
            public readonly Trigger Trigger;
            public readonly float   Seconds;   // Trigger.Timer için

            public Step(string key, string fallback, Trigger trigger, float seconds = 0f)
            {
                Key = key; Fallback = fallback; Trigger = trigger; Seconds = seconds;
            }
        }

        /// <summary>
        /// Senaryo sırası oyunun öğrenme sırasını izler: önce hamle, sonra hamleyi
        /// şekillendirme (döndürme), sonra ödül kuralı (yalnızca temizlik puan verir),
        /// sonra ekonomi (combo → hedef → kart), en sonda kaybetme koşulu.
        /// Kaybetme koşulunu en sona koymak bilinçli: oyuncu ilk dakikada
        /// "ölebilirim" endişesi yerine oynamayı öğreniyor.
        /// </summary>
        private static readonly Step[] Scenario =
        {
            new Step("Tutorial.Place",     "Pick a piece from the pool and drop it on the board.",              Trigger.Placed),
            new Step("Tutorial.Rotate",    "Press Q or E to rotate the piece before you drop it.",              Trigger.Rotated),
            new Step("Tutorial.Clear",     "Fill a whole row or column — points only come from clears.",        Trigger.LineCleared),
            new Step("Tutorial.Combo",     "Clear again right away: the combo multiplier grows with each one.", Trigger.ComboAboveBase),
            new Step("Tutorial.Milestone", "Reach the target score before you run out of pieces.",              Trigger.Milestone),
            new Step("Tutorial.Card",      "Pick a card — it stays with you for the rest of the run.",          Trigger.CardPicked),
            new Step("Tutorial.DeadPool",  "If no piece fits anywhere, the run ends. Keep a reroll in hand.",   Trigger.Timer, 6f),
        };

        // ── Durum ────────────────────────────────────────────────────────────
        private int   _index = -1;
        private bool  _active;
        private bool  _stepDone;
        private float _holdTimer;
        private float _timerStep;

        private CanvasGroup _group;
        private TMP_Text    _text;
        private Image       _marker;

        // ── Unity ────────────────────────────────────────────────────────────
        private void Start()
        {
            if (!_alwaysShow && IsCompleted) { enabled = false; return; }

            Build();
            Subscribe();
            Advance();
        }

        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            TutorialEvents.OnPiecePlaced      += HandlePlaced;
            TutorialEvents.OnRotated          += HandleRotated;
            TutorialEvents.OnComboChanged     += HandleCombo;
            TutorialEvents.OnMilestoneReached += HandleMilestone;
            TutorialEvents.OnCardPicked       += HandleCardPicked;
            Loc.OnChanged                     += RefreshText;
        }

        private void Unsubscribe()
        {
            TutorialEvents.OnPiecePlaced      -= HandlePlaced;
            TutorialEvents.OnRotated          -= HandleRotated;
            TutorialEvents.OnComboChanged     -= HandleCombo;
            TutorialEvents.OnMilestoneReached -= HandleMilestone;
            TutorialEvents.OnCardPicked       -= HandleCardPicked;
            Loc.OnChanged                     -= RefreshText;
        }

        // ── Akış ─────────────────────────────────────────────────────────────
        private void Advance()
        {
            _index++;
            _stepDone = false;

            if (_index >= Scenario.Length) { Finish(); return; }

            _active    = true;
            _timerStep = Scenario[_index].Trigger == Trigger.Timer ? Scenario[_index].Seconds : 0f;

            if (_marker != null) _marker.color = _textColor;
            RefreshText();
            SetVisible(true);
        }

        private void Complete()
        {
            if (!_active || _stepDone) return;
            _stepDone  = true;
            _holdTimer = _completedHold;
            if (_marker != null) _marker.color = _doneColor;
        }

        private void Finish()
        {
            _active = false;
            SetVisible(false);
            PlayerPrefs.SetInt(CompletedKey, 1);
            PlayerPrefs.Save();
            Unsubscribe();
            enabled = false;
        }

        private void Update()
        {
            if (!_active) return;

            if (_stepDone)
            {
                _holdTimer -= Time.unscaledDeltaTime;
                if (_holdTimer <= 0f) Advance();
                return;
            }

            if (_timerStep > 0f)
            {
                _timerStep -= Time.unscaledDeltaTime;
                if (_timerStep <= 0f) Complete();
            }
        }

        // ── Olay köprüleri ───────────────────────────────────────────────────
        private void HandlePlaced(int cleared)
        {
            if (IsWaitingFor(Trigger.Placed))                            Complete();
            else if (cleared > 0 && IsWaitingFor(Trigger.LineCleared))   Complete();
        }

        private void HandleRotated()
        {
            if (IsWaitingFor(Trigger.Rotated)) Complete();
        }

        private void HandleCombo(float multiplier)
        {
            // Taban çarpan yükseltmelerle değişebildiği için sabit 1f ile
            // karşılaştırmıyoruz — RunController zaten tabanın üstündeyken haber veriyor.
            if (IsWaitingFor(Trigger.ComboAboveBase)) Complete();
        }

        private void HandleMilestone()
        {
            if (IsWaitingFor(Trigger.Milestone)) Complete();
        }

        private void HandleCardPicked()
        {
            if (IsWaitingFor(Trigger.CardPicked)) Complete();
        }

        private bool IsWaitingFor(Trigger t) =>
            _active && !_stepDone &&
            _index >= 0 && _index < Scenario.Length &&
            Scenario[_index].Trigger == t;

        // ── Görsel ───────────────────────────────────────────────────────────
        private void RefreshText()
        {
            if (_text == null || _index < 0 || _index >= Scenario.Length) return;
            var step = Scenario[_index];
            _text.text = Loc.GetOr(step.Key, step.Fallback);
        }

        private void SetVisible(bool on)
        {
            if (_group == null) return;
            _group.alpha = on ? 1f : 0f;
        }

        private void Build()
        {
            var font = FindFont();

            var root = new GameObject("TutorialBanner", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -_topPadding);
            rt.sizeDelta = new Vector2(760f, 64f);

            _group = root.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable   = false;
            _group.alpha          = 0f;

            var bg = root.AddComponent<Image>();
            bg.color = _panelColor;
            bg.raycastTarget = false;

            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.padding        = new RectOffset(20, 24, 10, 10);
            layout.spacing        = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth      = true;
            layout.childControlHeight     = true;

            var fitter = root.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Sol taraftaki küçük gösterge: bekleniyor (soluk) → tamam (yeşil).
            var markerGo = new GameObject("Marker", typeof(RectTransform));
            markerGo.transform.SetParent(rt, false);
            _marker = markerGo.AddComponent<Image>();
            _marker.color = _textColor;
            _marker.raycastTarget = false;
            var markerLe = markerGo.AddComponent<LayoutElement>();
            markerLe.preferredWidth = markerLe.minWidth = 8f;
            markerLe.preferredHeight = markerLe.minHeight = 32f;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(rt, false);
            _text = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null) _text.font = font;
            _text.fontSize           = _fontSize;
            _text.color              = _textColor;
            _text.alignment          = TextAlignmentOptions.MidlineLeft;
            _text.raycastTarget      = false;
            _text.enableWordWrapping = false;
        }

        private TMP_FontAsset FindFont()
        {
            var any = GetComponentInChildren<TMP_Text>(true);
            if (any != null) return any.font;
            var scene = FindObjectOfType<TMP_Text>();
            return scene != null ? scene.font : null;
        }
    }
}
