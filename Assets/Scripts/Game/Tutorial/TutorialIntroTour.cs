using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using RogueBlockBlast.Core.Localization;
using RogueBlockBlast.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RogueBlockBlast.Game.Tutorial
{
    /// <summary>
    /// İlk açılışta bir kereliğine gösterilen "arayüz turu": ekranı karartıp ilgili
    /// paneli ışık deliğiyle işaretler, yanında ne olduğunu net bir kartla anlatır.
    ///
    /// Dört durak: şekil havuzu, hedef puan + kalan parça, combo çarpanı, döndürme tuşları.
    /// Banner tabanlı <see cref="TutorialController"/> senaryosundan ayrıdır; o oyunu
    /// oynarken hedef söyler, bu ise oynamadan önce haritayı gösterir. Tur bitince
    /// (ya da atlanınca) <see cref="CompletedKey"/> yazılır ve bir daha çıkmaz.
    ///
    /// Kendi arayüzünü kodda kurar: <see cref="TutorialController"/> bunu çalışma anında
    /// canvas'a ekler, sahnede ayrı kurulum yok — portrait sahne de otomatik kapsanır.
    /// Tur sürerken oyun girdisi kilitlidir (GameStateController); tahtaya yanlışlıkla
    /// parça konmaz.
    /// </summary>
    public sealed class TutorialIntroTour : MonoBehaviour
    {
        public const string CompletedKey = "Tutorial_Intro_Completed";

        public static bool IsCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;
        public static void ResetProgress() => PlayerPrefs.DeleteKey(CompletedKey);

        public event Action Finished;

        /// <summary>Dokunmatik metinler ve döndür butonu (portrait).</summary>
        public bool Touch { get; set; }
        public TMP_FontAsset Font { get; set; }

        // ── Stil ─────────────────────────────────────────────────────────────
        private static readonly Color Dim      = new Color(0.01f, 0.02f, 0.05f, 0.80f);
        private static readonly Color Accent   = new Color(0.35f, 0.85f, 1f, 1f);
        private static readonly Color CardFill = new Color(0.07f, 0.09f, 0.16f, 0.98f);
        private static readonly Color BodyCol  = new Color(0.90f, 0.93f, 0.98f, 1f);
        private static readonly Color DotOff   = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color CapFill  = new Color(0.16f, 0.20f, 0.30f, 1f);

        private const float CardWidth   = 720f;
        private const float HolePadding = 14f;
        private const float EdgeMargin  = 24f;
        private const float CardGap     = 30f;

        // ── Senaryo ──────────────────────────────────────────────────────────
        private sealed class TourStep
        {
            public string TitleKey, TitleFallback, BodyKey, BodyFallback;
            public bool   ShowKeys;
            public Func<List<Rect>> Targets;
        }

        private List<TourStep> _steps;

        // ── Durum ────────────────────────────────────────────────────────────
        private RectTransform _root;
        private CanvasGroup   _rootGroup;
        private HoleGraphic   _dim;
        private readonly List<RectTransform> _frames = new List<RectTransform>();
        private readonly List<Image>         _frameLines = new List<Image>();
        private readonly List<Image>         _frameGlows = new List<Image>();

        private RectTransform _card;
        private CanvasGroup   _cardGroup;
        private TMP_Text      _titleText, _bodyText, _counterText, _nextLabel, _skipLabel, _keysLabel;
        private RectTransform _keysRow;
        private readonly List<Image> _dots = new List<Image>();

        private int  _index = -1;
        private bool _finished;
        private bool _locked;
        private float _startedAt;

        private static Sprite _sprFill, _sprLine, _sprGlow, _sprCircle;

        // ── Yaşam döngüsü ────────────────────────────────────────────────────

        /// <summary>Canvas altına turu kurar ve ilk durağı gösterir.</summary>
        public static TutorialIntroTour Begin(Transform canvas, bool touch, TMP_FontAsset font)
        {
            var go = new GameObject("TutorialIntroTour", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var tour = go.AddComponent<TutorialIntroTour>();
            tour.Touch = touch;
            tour.Font  = font;
            tour.Build();
            tour.Next();
            return tour;
        }

        private void OnDestroy()
        {
            Loc.OnChanged -= RefreshTexts;
            if (_locked) { GameStateController.UnlockInput(); _locked = false; }
        }

        private void Update()
        {
            if (_finished || _root == null) return;

            // Çerçeveler nabız gibi atar.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < _frames.Count; i++)
            {
                if (!_frames[i].gameObject.activeSelf) continue;
                _frameLines[i].color = new Color(Accent.r, Accent.g, Accent.b, 0.80f + 0.20f * pulse);
                _frameGlows[i].color = new Color(Accent.r, Accent.g, Accent.b, 0.35f + 0.45f * pulse);
            }

            // Delikler yeni hedefe doğru akar. Çerçeveler deliği izler.
            _dim.StepHoles(Time.unscaledDeltaTime);
            for (int i = 0; i < _frames.Count; i++)
            {
                bool on = i < _dim.CurrentHoles.Count;
                if (_frames[i].gameObject.activeSelf != on) _frames[i].gameObject.SetActive(on);
                if (on) PlaceFrame(_frames[i], _dim.CurrentHoles[i]);
            }

            var kb = Keyboard.current;
            if (kb != null && Time.unscaledTime - _startedAt > 0.4f &&
                (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                Next();
        }

        // ── Akış ─────────────────────────────────────────────────────────────

        private void Next()
        {
            if (_finished) return;
            _index++;
            if (_index >= _steps.Count) { Finish(); return; }
            ShowStep(_steps[_index]);
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            PlayerPrefs.SetInt(CompletedKey, 1);
            PlayerPrefs.Save();

            if (_locked) { GameStateController.UnlockInput(); _locked = false; }

            _root.DOKill();
            _rootGroup.interactable = false;
            _rootGroup.blocksRaycasts = false;
            _rootGroup.DOFade(0f, 0.25f).SetUpdate(true).OnComplete(() =>
            {
                Finished?.Invoke();
                Destroy(gameObject);
            });
        }

        private void ShowStep(TourStep step)
        {
            _root.SetAsLastSibling();
            _startedAt = Time.unscaledTime;

            RefreshTexts();

            // Hedefler: deliğe dönüştürülür. Hedef yoksa tüm ekran kararır.
            var holes = step.Targets != null ? step.Targets() : new List<Rect>();
            for (int i = 0; i < holes.Count; i++) holes[i] = Inflate(holes[i], HolePadding);
            _dim.SetTargets(holes, animate: _index > 0);

            // Kart içeriği yeni boyut aldıktan sonra yerleştirilir.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_card);
            _card.anchoredPosition = ChooseCardPosition(holes, _card.rect.size);

            for (int i = 0; i < _dots.Count; i++)
                _dots[i].color = i == _index ? Accent : (i < _index ? new Color(Accent.r, Accent.g, Accent.b, 0.5f) : DotOff);

            _card.DOKill();
            _cardGroup.DOKill();
            _cardGroup.alpha = 0f;
            _card.localScale = Vector3.one * 0.93f;
            _cardGroup.DOFade(1f, 0.22f).SetUpdate(true);
            _card.DOScale(1f, 0.34f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void RefreshTexts()
        {
            if (_titleText == null || _index < 0 || _index >= _steps.Count) return;
            var step = _steps[_index];

            _titleText.text = Loc.GetOr(step.TitleKey, step.TitleFallback);
            string body = Loc.GetOr(step.BodyKey, step.BodyFallback);
            if (Touch) body = Loc.GetOr(step.BodyKey + ".Touch", body);
            _bodyText.text = body;

            _counterText.text = $"{_index + 1}/{_steps.Count}";
            bool last = _index == _steps.Count - 1;
            _nextLabel.text = last ? Loc.GetOr("Tutorial.Intro.Done", "Got it")
                                   : Loc.GetOr("Tutorial.Intro.Next", "Next");
            _skipLabel.text = Loc.GetOr("Tutorial.Intro.Skip", "Skip");
            _skipLabel.transform.parent.gameObject.SetActive(!last);

            // Tuş kapakları yalnızca klavye/fare için ve yalnızca döndürme durağında.
            bool keys = step.ShowKeys && !Touch;
            _keysRow.gameObject.SetActive(keys);
            if (keys) _keysLabel.text = Loc.GetOr("Hud.RotateHintWheel", "Rotate  ·  mouse wheel");

            LayoutRebuilder.ForceRebuildLayoutImmediate(_card);
        }

        // ── Yerleşim ─────────────────────────────────────────────────────────

        /// <summary>
        /// Hedefin yanında, hiçbir deliğe binmeyen ve ekrana sığan ilk konum. Önce en büyük
        /// deliğin yanı denenir, sonra diğerlerininki; böylece iki uzak hedefte kart
        /// ikisinin ortasındaki HUD panellerini örtmez.
        /// </summary>
        private Vector2 ChooseCardPosition(List<Rect> holes, Vector2 size)
        {
            Rect bounds = _root.rect;
            if (holes.Count == 0) return Vector2.zero;

            bool portrait = bounds.height > bounds.width;
            float hw = size.x * 0.5f, hh = size.y * 0.5f;

            var byArea = new List<Rect>(holes);
            byArea.Sort((a, b) => (b.width * b.height).CompareTo(a.width * a.height));

            Vector2 best = Vector2.zero;
            float bestOverflow = float.MaxValue;
            foreach (var u in byArea)
            {
                Vector2 right = new Vector2(u.xMax + CardGap + hw, u.center.y);
                Vector2 left  = new Vector2(u.xMin - CardGap - hw, u.center.y);
                Vector2 above = new Vector2(u.center.x, u.yMax + CardGap + hh);
                Vector2 below = new Vector2(u.center.x, u.yMin - CardGap - hh);

                // Alt kenara yakın hedefte kart yukarıya: yoksa alttaki havuz slotlarını örter.
                bool nearBottom = u.center.y < bounds.yMin + bounds.height * 0.3f;

                Vector2[] order = portrait
                    ? new[] { below, above, right, left }
                    : nearBottom ? new[] { above, right, left, below }
                                 : new[] { right, left, above, below };

                foreach (var raw in order)
                {
                    var p = ClampInside(raw, size, bounds);
                    var r = new Rect(p - size * 0.5f, size);

                    bool hit = false;
                    foreach (var h in holes) if (r.Overlaps(Inflate(h, 10f))) { hit = true; break; }
                    if (hit) continue;

                    // Kırpma yalnızca ana eksende sorun: yanlara dizilen kart dikeyde, alta/üste
                    // dizilen kart yatayda ekrana sığması için serbestçe kayabilir.
                    bool sideways = raw == right || raw == left;
                    float overflow = sideways ? Mathf.Abs(raw.x - p.x) : Mathf.Abs(raw.y - p.y);
                    if (overflow < 1f) return p;                   // temiz yerleşim
                    if (overflow < bestOverflow) { bestOverflow = overflow; best = p; }
                }
            }
            return best;
        }

        private static Vector2 ClampInside(Vector2 center, Vector2 size, Rect bounds)
        {
            float minX = bounds.xMin + EdgeMargin + size.x * 0.5f, maxX = bounds.xMax - EdgeMargin - size.x * 0.5f;
            float minY = bounds.yMin + EdgeMargin + size.y * 0.5f, maxY = bounds.yMax - EdgeMargin - size.y * 0.5f;
            return new Vector2(
                minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : bounds.center.x,
                minY <= maxY ? Mathf.Clamp(center.y, minY, maxY) : bounds.center.y);
        }

        private static Rect Inflate(Rect r, float by) =>
            new Rect(r.xMin - by, r.yMin - by, r.width + by * 2f, r.height + by * 2f);

        private static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                            Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        private static void PlaceFrame(RectTransform frame, Rect hole)
        {
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = hole.center;
            frame.sizeDelta = hole.size;
        }

        // ── Hedef çözümleme ──────────────────────────────────────────────────

        private RectTransform CanvasRect => (RectTransform)_root.parent;

        private RectTransform FindByName(string name)
        {
            foreach (var rt in CanvasRect.GetComponentsInChildren<RectTransform>(true))
                if (rt != _root && rt.name == name && rt.gameObject.activeInHierarchy) return rt;
            return null;
        }

        /// <summary>Verilen rect'lerin birleşimi, tur kökünün yerel uzayında.</summary>
        private Rect? LocalBounds(IEnumerable<RectTransform> rects)
        {
            bool any = false;
            float x0 = 0, y0 = 0, x1 = 0, y1 = 0;
            var corners = new Vector3[4];
            foreach (var rt in rects)
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                rt.GetWorldCorners(corners);
                foreach (var c in corners)
                {
                    Vector3 l = _root.InverseTransformPoint(c);
                    if (!any) { x0 = x1 = l.x; y0 = y1 = l.y; any = true; continue; }
                    x0 = Mathf.Min(x0, l.x); x1 = Mathf.Max(x1, l.x);
                    y0 = Mathf.Min(y0, l.y); y1 = Mathf.Max(y1, l.y);
                }
            }
            return any ? Rect.MinMaxRect(x0, y0, x1, y1) : (Rect?)null;
        }

        private static void AddIf(List<Rect> list, Rect? r) { if (r.HasValue) list.Add(r.Value); }

        private List<Rect> PoolTargets()
        {
            // PoolPanel tüm canvas'ı kaplayabilir; yalnızca gerçekten görünen parçalar işaretlenir:
            // havuz slotları ve yeniden çekme butonu.
            var parts = new List<RectTransform>();
            var pool = FindAnyObjectByType<PoolView>();
            if (pool != null)
                foreach (var s in pool.GetComponentsInChildren<PoolSlotView>())
                    parts.Add((RectTransform)s.transform);
            var reroll = FindByName("PoolRerollButton");
            if (reroll != null) parts.Add(reroll);

            var list = new List<Rect>();
            AddIf(list, LocalBounds(parts));
            return list;
        }

        private List<Rect> GoalTargets()
        {
            var list = new List<Rect>();
            var mv = FindAnyObjectByType<MilestoneView>();
            if (mv != null)
            {
                AddIf(list, LocalBounds(new[] { (RectTransform)mv.transform }));
                if (mv.PiecesRect != null) AddIf(list, LocalBounds(new[] { mv.PiecesRect }));
            }
            return MergeOverlapping(list);
        }

        private List<Rect> ComboTargets()
        {
            var list = new List<Rect>();
            var cv = FindAnyObjectByType<ComboView>();
            if (cv != null) AddIf(list, LocalBounds(new[] { (RectTransform)cv.transform }));
            return list;
        }

        private List<Rect> RotateTargets()
        {
            var list = new List<Rect>();
            var rt = Touch ? FindByName("RotateButton") : FindByName("RotateHint");
            if (rt == null) rt = FindByName(Touch ? "RotateHint" : "RotateButton");
            if (rt != null) AddIf(list, LocalBounds(new[] { rt }));
            return list;
        }

        /// <summary>Üst üste binen delikleri tek deliğe katlar (karartma tek parça kalsın).</summary>
        private static List<Rect> MergeOverlapping(List<Rect> src)
        {
            var res = new List<Rect>(src);
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < res.Count && !merged; i++)
                for (int j = i + 1; j < res.Count && !merged; j++)
                {
                    if (!Inflate(res[i], HolePadding * 2f).Overlaps(res[j])) continue;
                    res[i] = Union(res[i], res[j]);
                    res.RemoveAt(j);
                    merged = true;
                }
            }
            return res;
        }

        // ── Kurulum ──────────────────────────────────────────────────────────

        private void Build()
        {
            _steps = new List<TourStep>
            {
                new TourStep
                {
                    TitleKey = "Tutorial.Intro.Pool.Title", TitleFallback = "Your shape pool",
                    BodyKey  = "Tutorial.Intro.Pool.Body",
                    BodyFallback = "Your shape pool is right here. Pick a piece from it and place it on the board.",
                    Targets = PoolTargets,
                },
                new TourStep
                {
                    TitleKey = "Tutorial.Intro.Goal.Title", TitleFallback = "Don't run out of pieces",
                    BodyKey  = "Tutorial.Intro.Goal.Body",
                    BodyFallback = "If you use up your whole pool before reaching the target score, the run ends.",
                    Targets = GoalTargets,
                },
                new TourStep
                {
                    TitleKey = "Tutorial.Intro.Combo.Title", TitleFallback = "Combo multiplier",
                    BodyKey  = "Tutorial.Intro.Combo.Body",
                    BodyFallback = "Every row or column you clear raises your combo multiplier. Every move without a clear lowers it by 1. Watch your combo!",
                    Targets = ComboTargets,
                },
                new TourStep
                {
                    TitleKey = "Tutorial.Intro.Rotate.Title", TitleFallback = "Rotate shapes",
                    BodyKey  = "Tutorial.Intro.Rotate.Body",
                    BodyFallback = "You can rotate shapes with the keys shown here.",
                    ShowKeys = true,
                    Targets = RotateTargets,
                },
            };

            EnsureSprites();

            // Kök: tüm canvas'ı kaplar, girdiyi yutar.
            _root = (RectTransform)transform;
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            _rootGroup = gameObject.AddComponent<CanvasGroup>();

            var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer));
            dimGo.transform.SetParent(_root, false);
            Stretch((RectTransform)dimGo.transform);
            _dim = dimGo.AddComponent<HoleGraphic>();
            _dim.color = Dim;
            _dim.raycastTarget = true;

            // Çerçeve çiftleri (en fazla iki delik).
            for (int i = 0; i < 2; i++)
            {
                var frame = NewRect("Frame" + i, _root);
                frame.gameObject.SetActive(false);

                var glowRt = NewRect("Glow", frame);
                glowRt.anchorMin = Vector2.zero; glowRt.anchorMax = Vector2.one;
                glowRt.offsetMin = new Vector2(-26f, -26f); glowRt.offsetMax = new Vector2(26f, 26f);
                var glow = glowRt.gameObject.AddComponent<Image>();
                glow.sprite = _sprGlow; glow.type = Image.Type.Sliced; glow.raycastTarget = false;

                var lineRt = NewRect("Line", frame);
                Stretch(lineRt);
                var line = lineRt.gameObject.AddComponent<Image>();
                line.sprite = _sprLine; line.type = Image.Type.Sliced; line.raycastTarget = false;

                _frames.Add(frame); _frameLines.Add(line); _frameGlows.Add(glow);
            }

            BuildCard();
            Loc.OnChanged += RefreshTexts;

            if (!_locked) { GameStateController.LockInput(); _locked = true; }
        }

        private void BuildCard()
        {
            _card = NewRect("Card", _root);
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(CardWidth, 100f);
            _cardGroup = _card.gameObject.AddComponent<CanvasGroup>();

            var bg = _card.gameObject.AddComponent<Image>();
            bg.sprite = _sprFill; bg.type = Image.Type.Sliced; bg.color = CardFill;

            var v = _card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(36, 36, 28, 26);
            v.spacing = 16f;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;  v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            var fit = _card.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Çerçeve, kartın üstünde ince bir vurgu çizgisi.
            var edge = NewRect("Edge", _card);
            Stretch(edge);
            edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var edgeImg = edge.gameObject.AddComponent<Image>();
            edgeImg.sprite = _sprLine; edgeImg.type = Image.Type.Sliced;
            edgeImg.color = new Color(Accent.r, Accent.g, Accent.b, 0.55f);
            edgeImg.raycastTarget = false;

            // Başlık satırı: sayaç + başlık
            var head = NewRect("Head", _card);
            var hl = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 14f; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

            _counterText = NewText("Counter", head, 26f, new Color(Accent.r, Accent.g, Accent.b, 0.75f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _counterText.gameObject.AddComponent<LayoutElement>().minWidth = 56f;
            _titleText = NewText("Title", head, 42f, Accent, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _titleText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            _bodyText = NewText("Body", _card, 32f, BodyCol, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _bodyText.enableWordWrapping = true;

            // Tuş kapakları (yalnızca döndürme durağı, yalnızca klavye)
            _keysRow = NewRect("Keys", _card);
            var kl = _keysRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            kl.spacing = 12f; kl.childAlignment = TextAnchor.MiddleLeft;
            kl.childControlWidth = true; kl.childControlHeight = true;
            kl.childForceExpandWidth = false; kl.childForceExpandHeight = false;
            NewKeyCap(_keysRow, "Q");
            NewKeyCap(_keysRow, "E");
            _keysLabel = NewText("KeysLabel", _keysRow, 28f, BodyCol, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _keysLabel.enableWordWrapping = false;

            // Alt satır: noktalar | Atla | İleri
            var foot = NewRect("Foot", _card);
            var fl = foot.gameObject.AddComponent<HorizontalLayoutGroup>();
            fl.spacing = 18f; fl.childAlignment = TextAnchor.MiddleLeft;
            fl.childControlWidth = true; fl.childControlHeight = true;
            fl.childForceExpandWidth = false; fl.childForceExpandHeight = false;
            foot.gameObject.AddComponent<LayoutElement>().minHeight = 72f;

            for (int i = 0; i < _steps.Count; i++)
            {
                var dot = NewRect("Dot" + i, foot);
                var le = dot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = 16f; le.preferredHeight = le.minHeight = 16f;
                var img = dot.gameObject.AddComponent<Image>();
                img.sprite = _sprCircle; img.color = DotOff; img.raycastTarget = false;
                _dots.Add(img);
            }

            var spacer = NewRect("Spacer", foot);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var skip = NewButton("Skip", foot, 150f, 64f, Color.clear, new Color(1f, 1f, 1f, 0.6f), 26f, out _skipLabel);
            skip.onClick.AddListener(Finish);
            var next = NewButton("Next", foot, 220f, 72f, Accent, new Color(0.03f, 0.06f, 0.12f, 1f), 32f, out _nextLabel);
            next.onClick.AddListener(Next);
        }

        private Button NewButton(string name, RectTransform parent, float w, float h, Color fill, Color textColor,
                                 float fontSize, out TMP_Text label)
        {
            var rt = NewRect(name, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = w; le.preferredHeight = le.minHeight = h;

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = _sprFill; img.type = Image.Type.Sliced; img.color = fill;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor     = new Color(0.78f, 0.78f, 0.85f, 1f);
            btn.colors = colors;

            var tr = NewRect("Label", rt);
            Stretch(tr);
            label = tr.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) label.font = Font;
            label.fontSize = fontSize; label.color = textColor; label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return btn;
        }

        private void NewKeyCap(RectTransform parent, string letter)
        {
            var rt = NewRect("Cap" + letter, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 64f; le.preferredHeight = le.minHeight = 64f;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = _sprFill; img.type = Image.Type.Sliced; img.color = CapFill; img.raycastTarget = false;

            var edge = NewRect("Edge", rt);
            Stretch(edge);
            var ei = edge.gameObject.AddComponent<Image>();
            ei.sprite = _sprLine; ei.type = Image.Type.Sliced; ei.color = Accent; ei.raycastTarget = false;

            var tr = NewRect("Letter", rt);
            Stretch(tr);
            var t = tr.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = letter; t.fontSize = 36f; t.color = Color.white; t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        }

        private TMP_Text NewText(string name, RectTransform parent, float size, Color color, FontStyles style, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.fontSize = size; t.color = color; t.fontStyle = style; t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ── Prosedürel sprite'lar ────────────────────────────────────────────

        private static void EnsureSprites()
        {
            if (_sprFill != null) return;
            _sprFill   = MakeRounded(64, 18f, 1f, 0f, 0f, 26);   // dolu yuvarlak dikdörtgen
            _sprLine   = MakeRounded(64, 18f, 1f, 4f, 0f, 26);   // 4 px kenar çizgisi
            _sprGlow   = MakeRounded(96, 18f, 26f, 0f, 8f, 44);  // dış parıltı (kenardan 26 px içeride başlar)
            _sprCircle = MakeCircle(32);
        }

        /// <summary>
        /// İşaretli uzaklık alanıyla yuvarlak dikdörtgen. thickness 0 = dolu, &gt;0 = iç kenar çizgisi,
        /// glow &gt; 0 = kenar çevresinde yumuşak parıltı. margin = şeklin sprite kenarından içeri payı.
        /// </summary>
        private static Sprite MakeRounded(int size, float radius, float margin, float thickness, float glow, int border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float half = size * 0.5f - margin;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = x + 0.5f - size * 0.5f, cy = y + 0.5f - size * 0.5f;
                float qx = Mathf.Abs(cx) - (half - radius), qy = Mathf.Abs(cy) - (half - radius);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                          + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;   // <0 içeride

                float a;
                if (glow > 0f)       a = Mathf.Exp(-(d * d) / (2f * glow * glow));
                else if (thickness > 0f) a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + thickness + 0.5f);
                else                 a = Mathf.Clamp01(0.5f - d);

                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float r = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude - r;
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f));
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ── Delikli karartma ─────────────────────────────────────────────────

        /// <summary>
        /// Tüm rect'i boyayan ama verilen dikdörtgen "delikleri" boş bırakan grafik.
        /// Yatay şeritlere bölerek çizer: her şeritte deliklerin kapladığı x aralıkları
        /// çıkarılır, kalan boşluklar dörtgen olur. Delikler hedefe doğru akar.
        /// </summary>
        private sealed class HoleGraphic : MaskableGraphic
        {
            private readonly List<Rect> _target = new List<Rect>();
            public readonly List<Rect> CurrentHoles = new List<Rect>();

            public void SetTargets(List<Rect> holes, bool animate)
            {
                _target.Clear(); _target.AddRange(holes);

                // Yeni delik sayısı farklıysa fazlalar atılır, eksikler ilk deliğin yerinden doğar.
                while (CurrentHoles.Count > _target.Count) CurrentHoles.RemoveAt(CurrentHoles.Count - 1);
                for (int i = CurrentHoles.Count; i < _target.Count; i++)
                    CurrentHoles.Add(CurrentHoles.Count > 0 ? CurrentHoles[0] : _target[i]);

                if (!animate) { for (int i = 0; i < _target.Count; i++) CurrentHoles[i] = _target[i]; }
                SetVerticesDirty();
            }

            public void StepHoles(float dt)
            {
                float k = 1f - Mathf.Exp(-14f * dt);
                bool changed = false;
                for (int i = 0; i < CurrentHoles.Count && i < _target.Count; i++)
                {
                    var c = CurrentHoles[i]; var t = _target[i];
                    if ((c.min - t.min).sqrMagnitude < 0.01f && (c.max - t.max).sqrMagnitude < 0.01f)
                    {
                        if (c != t) { CurrentHoles[i] = t; changed = true; }
                        continue;
                    }
                    CurrentHoles[i] = Rect.MinMaxRect(
                        Mathf.Lerp(c.xMin, t.xMin, k), Mathf.Lerp(c.yMin, t.yMin, k),
                        Mathf.Lerp(c.xMax, t.xMax, k), Mathf.Lerp(c.yMax, t.yMax, k));
                    changed = true;
                }
                if (changed) SetVerticesDirty();
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                Rect r = rectTransform.rect;

                var ys = new List<float> { r.yMin, r.yMax };
                foreach (var h in CurrentHoles)
                {
                    ys.Add(Mathf.Clamp(h.yMin, r.yMin, r.yMax));
                    ys.Add(Mathf.Clamp(h.yMax, r.yMin, r.yMax));
                }
                ys.Sort();

                for (int s = 0; s + 1 < ys.Count; s++)
                {
                    float y0 = ys[s], y1 = ys[s + 1];
                    if (y1 - y0 < 0.01f) continue;
                    float ym = (y0 + y1) * 0.5f;

                    var spans = new List<Vector2>();
                    foreach (var h in CurrentHoles)
                        if (ym > h.yMin && ym < h.yMax)
                            spans.Add(new Vector2(Mathf.Clamp(h.xMin, r.xMin, r.xMax), Mathf.Clamp(h.xMax, r.xMin, r.xMax)));
                    spans.Sort((a, b) => a.x.CompareTo(b.x));

                    float x = r.xMin;
                    foreach (var sp in spans)
                    {
                        if (sp.x > x) Quad(vh, x, y0, sp.x, y1);
                        x = Mathf.Max(x, sp.y);
                    }
                    if (x < r.xMax) Quad(vh, x, y0, r.xMax, y1);
                }
            }

            private void Quad(VertexHelper vh, float x0, float y0, float x1, float y1)
            {
                int i = vh.currentVertCount;
                var col = (Color32)color;
                vh.AddVert(new Vector3(x0, y0), col, Vector2.zero);
                vh.AddVert(new Vector3(x0, y1), col, Vector2.zero);
                vh.AddVert(new Vector3(x1, y1), col, Vector2.zero);
                vh.AddVert(new Vector3(x1, y0), col, Vector2.zero);
                vh.AddTriangle(i, i + 1, i + 2);
                vh.AddTriangle(i + 2, i + 3, i);
            }
        }
    }
}
