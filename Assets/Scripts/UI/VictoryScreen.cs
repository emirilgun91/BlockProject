using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core.Localization;
using RogueBlockBlast.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Son aşama geçilince açılan zafer ekranı. Run burada biter.
    ///
    /// Sahneye bir şey eklemek gerekmez: <see cref="Show"/> kendi Canvas'ını kurar,
    /// sprite'larını kodda üretir. Bu yüzden SampleScene ile GamePortrait aynı
    /// koddan beslenir ve dikey ekranda da yerleşim bozulmaz.
    ///
    /// Akış zamana yayılır, çünkü aynı anda olan iki şey oyuncuya tek bir gürültü
    /// olarak ulaşır:
    ///   0.0 s  beyaz flaş + tahtada dalga + kamera sarsıntısı (oyun hâlâ akıyor)
    ///   0.9 s  oyun durur, perde iner, ışınlar döner
    ///   1.1 s  başlık harf harf çakılır (logo renkleriyle)
    ///   1.9 s  konfeti topları, istatistikler sırayla sayar
    ///   son    yeni rekor damgası, run'ın kartları, butonlar
    /// Her an bir tıklama ya da tuş animasyonu sonuna atlatır.
    /// </summary>
    public sealed class VictoryScreen : MonoBehaviour
    {
        public struct Stats
        {
            public int    Score, Lines, Pieces, Coins, Stages;
            public float  MaxCombo, Seconds;
            public bool   NewRecord;
            public List<CardSO> Cards;
        }

        /// <summary>Açıkken pause menüsü ESC'ye tepki vermez.</summary>
        public static bool IsShowing { get; private set; }

        // Logo harf renkleri (ana menü logosu) — zafer markanın kendi renkleriyle kutlanır
        private static readonly Color[] LetterColors =
        {
            Hex(0x60B0F0), Hex(0x7848D0), Hex(0x5DDA6A), Hex(0xFFC040)
        };
        private static readonly Color Gold = Hex(0xFFC040);

        private Stats  _stats;
        private Action _onNewRun, _onMainMenu;
        private AudioClip _slamClip;

        private Canvas        _canvas;
        private RectTransform _root;
        private Image         _flash, _backdrop, _rays, _glow;
        private RectTransform _titleRow, _column;
        private float         _openedAt;
        private readonly List<TMP_Text> _letters = new();
        private TMP_Text      _subtitle;
        private CanvasGroup   _statsGroup, _cardsGroup, _buttonsGroup;
        private readonly List<(TMP_Text value, Func<float, string> fmt, float target)> _rows = new();
        private RectTransform _stamp;
        private readonly List<RectTransform> _cardIcons = new();
        private Button        _newRunButton, _menuButton;
        private Sequence      _seq;
        private bool          _finished;

        // ── Açılış ───────────────────────────────────────────────────────────

        public static VictoryScreen Show(Stats stats, Action onNewRun, Action onMainMenu, AudioClip slamClip = null)
        {
            var go = new GameObject("[VictoryScreen]");
            var v  = go.AddComponent<VictoryScreen>();
            v._stats = stats; v._onNewRun = onNewRun; v._onMainMenu = onMainMenu; v._slamClip = slamClip;
            IsShowing = true;
            v.Build();
            v.Play();
            return v;
        }

        private void OnDestroy()
        {
            IsShowing = false;
            _seq?.Kill();
            DOTween.Kill(this);
        }

        // ── Kurulum ──────────────────────────────────────────────────────────

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 700;    // HUD, kart ekranı ve reroll butonunun üstünde
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)transform;

            _backdrop = FullImage("Backdrop", new Color(0.02f, 0.03f, 0.07f, 0f));
            _backdrop.raycastTarget = true;   // arkadaki tahtaya tıklama geçmesin

            _rays = NewImage("Rays", _root, VictorySprites.Rays, WithA(Gold, 0f));
            Size(_rays.rectTransform, 2200f, 2200f);
            _glow = NewImage("Glow", _root, FX.UIFXSprites.RadialGlow, WithA(Gold, 0f));
            Size(_glow.rectTransform, 1300f, 900f);

            // İçerik sütunu: dar ekranda (portrait) sığacak kadar
            var column = NewRect("Column", _root);
            _column = column;
            Size(column, 920f, 1000f);
            var vlg = column.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 18f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            BuildTitle(column);
            _subtitle = NewText("Subtitle", column, 40f, new Color(0.88f, 0.92f, 1f, 1f), FontStyles.Bold);
            _subtitle.text = Loc.Get("Victory.Subtitle", _stats.Stages);
            Pref(_subtitle.rectTransform, 56f);
            _subtitle.alpha = 0f;

            BuildStats(column);
            BuildCards(column);
            BuildButtons(column);

            _flash = FullImage("Flash", new Color(1f, 1f, 1f, 0f));
            _flash.raycastTarget = false;

            _confettiLayer = NewRect("Confetti", _root);
            Stretch(_confettiLayer);

            // Sığmayan dar ekranlarda sütunu küçült (yerleşim bittikten sonra ölçülür)
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(column);
            float h = LayoutUtility.GetPreferredHeight(column);
            var canvasRect = _root.rect;
            float fit = Mathf.Min(1f, (canvasRect.height - 60f) / Mathf.Max(1f, h), (canvasRect.width - 48f) / 920f);
            column.localScale = Vector3.one * fit;
        }

        private void BuildTitle(RectTransform parent)
        {
            _titleRow = NewRect("Title", parent);
            Pref(_titleRow, 190f);
            var hlg = _titleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 4f;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;

            string title = Loc.ToUpper(Loc.Get("Victory.Title"));
            var e = StringInfo.GetTextElementEnumerator(title);
            int i = 0;
            while (e.MoveNext())
            {
                string ch = e.GetTextElement();
                var t = NewText("L" + i, _titleRow, 168f, LetterColors[i % LetterColors.Length], FontStyles.Bold);
                t.text = ch;
                t.enableWordWrapping = false;
                t.outlineWidth = 0.18f;
                t.outlineColor = new Color32(8, 10, 24, 255);
                t.alpha = 0f;
                _letters.Add(t);
                i++;
            }

            // Uzun çeviriler (ZWYCIĘSTWO) satıra sığsın
            LayoutRebuilder.ForceRebuildLayoutImmediate(_titleRow);
            float w = LayoutUtility.GetPreferredWidth(_titleRow);
            if (w > 900f) foreach (var l in _letters) l.fontSize *= 900f / w;
        }

        private void BuildStats(RectTransform parent)
        {
            var panel = NewRect("Stats", parent);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.sprite = VictorySprites.Panel; bg.type = Image.Type.Sliced;
            bg.color  = new Color(0.07f, 0.09f, 0.16f, 0.94f);
            var rim = NewImage("Rim", panel, VictorySprites.PanelRim, WithA(Gold, 0.55f));
            rim.type = Image.Type.Sliced; Stretch(rim.rectTransform);
            rim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // kenar çizgisi satır değil
            _statsGroup = panel.gameObject.AddComponent<CanvasGroup>();
            _statsGroup.alpha = 0f;

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(44, 44, 26, 26);
            vlg.spacing = 4f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            AddRow(panel, Loc.Get("GameOver.FinalScore"), _stats.Score, v => Mathf.RoundToInt(v).ToString("N0"), Gold, 62f, 76f);
            AddRow(panel, Loc.Get("GameOver.MaxCombo"),  _stats.MaxCombo, v => "x" + v.ToString("0.0"), LetterColors[0], 40f, 52f);
            AddRow(panel, Loc.Get("GameOver.Lines"),     _stats.Lines, v => Mathf.RoundToInt(v).ToString("N0"), Color.white, 40f, 52f);
            AddRow(panel, Loc.Get("GameOver.Pieces"),    _stats.Pieces, v => Mathf.RoundToInt(v).ToString("N0"), Color.white, 40f, 52f);
            AddRow(panel, Loc.Get("Victory.RunTime"),    _stats.Seconds, FormatTime, Color.white, 40f, 52f);
            AddRow(panel, Loc.Get("GameOver.Coin"),      _stats.Coins, v => "+" + Mathf.RoundToInt(v).ToString("N0"), Gold, 40f, 52f);

            // Yeni rekor damgası: istatistiklerin sağ üst köşesine çapraz basılır
            _stamp = NewRect("Stamp", panel);
            var le = _stamp.gameObject.AddComponent<LayoutElement>(); le.ignoreLayout = true;
            _stamp.anchorMin = _stamp.anchorMax = new Vector2(1f, 1f);
            _stamp.anchoredPosition = new Vector2(-70f, -8f);
            Size(_stamp, 300f, 70f);
            var sbg = _stamp.gameObject.AddComponent<Image>();
            sbg.sprite = VictorySprites.Panel; sbg.type = Image.Type.Sliced; sbg.color = UiTheme.Danger;
            var st = NewText("Text", _stamp, 32f, Color.white, FontStyles.Bold);
            Stretch(st.rectTransform);
            st.text = Loc.ToUpper(Loc.Get("GameOver.NewRecord"));
            st.enableAutoSizing = true; st.fontSizeMin = 18f; st.fontSizeMax = 32f;
            _stamp.localRotation = Quaternion.Euler(0f, 0f, 9f);
            _stamp.gameObject.SetActive(false);
        }

        private void AddRow(RectTransform parent, string label, float target, Func<float, string> fmt, Color valueColor,
                            float size, float height)
        {
            var row = NewRect("Row", parent);
            Pref(row, height);
            var l = NewText("Label", row, 28f, new Color(0.62f, 0.68f, 0.82f, 1f), FontStyles.Bold);
            l.text = Loc.ToUpper(label);
            l.alignment = TextAlignmentOptions.MidlineLeft;
            l.characterSpacing = 4f;
            Stretch(l.rectTransform);
            var v = NewText("Value", row, size, valueColor, FontStyles.Bold);
            v.alignment = TextAlignmentOptions.MidlineRight;
            v.text = fmt(0f);
            Stretch(v.rectTransform);
            _rows.Add((v, fmt, target));
        }

        private void BuildCards(RectTransform parent)
        {
            var cards = _stats.Cards ?? new List<CardSO>();
            var block = NewRect("Cards", parent);
            Pref(block, cards.Count == 0 ? 0f : 120f);
            _cardsGroup = block.gameObject.AddComponent<CanvasGroup>();
            _cardsGroup.alpha = 0f;
            if (cards.Count == 0) return;

            var vlg = block.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f; vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

            var label = NewText("Label", block, 24f, new Color(0.62f, 0.68f, 0.82f, 1f), FontStyles.Bold);
            label.text = Loc.ToUpper(Loc.Get("Victory.Build"));
            label.characterSpacing = 4f;
            Pref(label.rectTransform, 30f);

            var row = NewRect("Icons", block);
            Pref(row, 76f);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 10f;
            hlg.childControlWidth = false; hlg.childControlHeight = false;

            int shown = Mathf.Min(cards.Count, 11);
            float size = cards.Count > 9 ? 62f : 72f;
            for (int i = 0; i < shown; i++)
            {
                var c = cards[i];
                var slot = NewRect("Card", row);
                Size(slot, size, size);
                var frame = slot.gameObject.AddComponent<Image>();
                frame.sprite = VictorySprites.Panel; frame.type = Image.Type.Sliced;
                frame.color = CardRarityPalette.Get(c.Rarity);
                var inner = NewImage("Inner", slot, VictorySprites.Panel, new Color(0.05f, 0.06f, 0.11f, 1f));
                inner.type = Image.Type.Sliced; Stretch(inner.rectTransform, 4f);
                if (c.Icon != null)
                {
                    var ic = NewImage("Icon", slot, c.Icon, Color.white);
                    ic.preserveAspect = true; Stretch(ic.rectTransform, 8f);
                }
                slot.localScale = Vector3.zero;
                _cardIcons.Add(slot);
            }
            if (cards.Count > shown)
            {
                var more = NewText("More", row, 30f, Color.white, FontStyles.Bold);
                more.text = "+" + (cards.Count - shown);
                Size(more.rectTransform, size, size);
                more.rectTransform.localScale = Vector3.zero;
                _cardIcons.Add(more.rectTransform);
            }
        }

        private void BuildButtons(RectTransform parent)
        {
            var row = NewRect("Buttons", parent);
            Pref(row, 92f);
            _buttonsGroup = row.gameObject.AddComponent<CanvasGroup>();
            _buttonsGroup.alpha = 0f; _buttonsGroup.interactable = false; _buttonsGroup.blocksRaycasts = false;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 24f;
            hlg.childControlWidth = false; hlg.childControlHeight = false;

            _newRunButton = MakeButton(row, Loc.Get("GameOver.NewRun"), primary: true, () => Leave(_onNewRun));
            _menuButton   = MakeButton(row, Loc.Get("GameOver.MainMenu"), primary: false, () => Leave(_onMainMenu));
        }

        private Button MakeButton(RectTransform parent, string text, bool primary, Action onClick)
        {
            var rt = NewRect("Button", parent);
            Size(rt, 340f, 84f);
            var fill = rt.gameObject.AddComponent<Image>();
            fill.sprite = VictorySprites.Panel; fill.type = Image.Type.Sliced;
            var rim = NewImage("Rim", rt, VictorySprites.PanelRim, Color.white);
            rim.type = Image.Type.Sliced; Stretch(rim.rectTransform);
            var label = NewText("Label", rt, 32f, Color.white, FontStyles.Bold);
            label.text = Loc.ToUpper(text);
            label.enableAutoSizing = true; label.fontSizeMin = 18f; label.fontSizeMax = 32f;
            Stretch(label.rectTransform, 14f);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = fill;
            btn.onClick.AddListener(() => onClick());

            Color accent = primary ? UiTheme.Accent : UiTheme.Rim;
            rt.gameObject.AddComponent<KitButton>().Configure(
                fill, rim, label,
                primary ? UiTheme.AccentDeep : UiTheme.Raised,
                primary ? WithA(UiTheme.Accent, 0.35f) : UiTheme.RaisedHover,
                WithA(accent, 0.8f), accent);
            return btn;
        }

        // ── Akış ─────────────────────────────────────────────────────────────

        private void Play()
        {
            GameStateControllerLock(true);
            _openedAt = Time.unscaledTime;
            float intensity = GameSettings.ReduceMotion ? 0.4f : 1f;

            _seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);

            // 0: flaş — oyun hâlâ akıyor, tahta dalgası ve sarsıntı RunController'dan
            _seq.Append(_flash.DOFade(0.85f * intensity * GameSettings.FlashStrength, 0.07f));
            _seq.Append(_flash.DOFade(0f, 0.55f).SetEase(Ease.OutQuad));

            // 1: perde iner, oyun durur
            _seq.Insert(0.55f, _backdrop.DOFade(0.88f, 0.45f));
            _seq.InsertCallback(0.9f, () => Time.timeScale = 0f);
            _seq.Insert(0.75f, _glow.DOFade(0.55f, 0.6f));
            _seq.Insert(0.75f, _rays.DOFade(0.16f, 0.9f));

            // 2: başlık harf harf çakılır
            float t0 = 1.1f;
            for (int i = 0; i < _letters.Count; i++)
            {
                var l  = _letters[i];
                var rt = l.rectTransform;
                float at = t0 + i * 0.075f;
                float rot = (i % 2 == 0 ? -1f : 1f) * UnityEngine.Random.Range(14f, 30f);
                rt.localScale = Vector3.one * 3.2f;
                rt.localRotation = Quaternion.Euler(0f, 0f, rot);
                _seq.Insert(at, DOTween.To(() => l.alpha, a => l.alpha = a, 1f, 0.12f));
                _seq.Insert(at, rt.DOScale(1f, 0.32f).SetEase(Ease.OutBack, 2.2f));
                _seq.Insert(at, rt.DOLocalRotate(Vector3.zero, 0.32f).SetEase(Ease.OutBack));
                int idx = i;
                _seq.InsertCallback(at + 0.22f, () => LetterLanded(idx, intensity));
            }
            float titleEnd = t0 + _letters.Count * 0.075f + 0.3f;

            // Başlık oturunca hafif nefes: harfler sırayla zıplar (bitmeyen dalga)
            _seq.InsertCallback(titleEnd + 0.2f, StartTitleWave);

            _seq.Insert(titleEnd - 0.1f, DOTween.To(() => _subtitle.alpha, a => _subtitle.alpha = a, 1f, 0.4f));
            _subtitle.rectTransform.localScale = Vector3.one * 0.85f;
            _seq.Insert(titleEnd - 0.1f, _subtitle.rectTransform.DOScale(1f, 0.4f).SetEase(Ease.OutCubic));

            // 3: konfeti topları + istatistikler
            _seq.InsertCallback(titleEnd, () => ConfettiCannons(intensity));
            _seq.Insert(titleEnd + 0.1f, _statsGroup.DOFade(1f, 0.3f));
            _statsGroup.transform.localScale = Vector3.one * 0.92f;
            _seq.Insert(titleEnd + 0.1f, _statsGroup.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack));

            float rowAt = titleEnd + 0.35f;
            foreach (var r in _rows)
            {
                var row = r;
                float dur = row.target > 1000 ? 0.85f : 0.45f;
                _seq.Insert(rowAt, DOTween.To(() => 0f, v => row.value.text = row.fmt(v), row.target, dur).SetEase(Ease.OutCubic));
                _seq.InsertCallback(rowAt + dur, () => Punch(row.value.rectTransform, 0.18f));
                rowAt += dur * 0.6f + 0.12f;
            }
            float statsEnd = rowAt + 0.25f;

            if (_stats.NewRecord)
            {
                _seq.InsertCallback(statsEnd, () =>
                {
                    _stamp.gameObject.SetActive(true);
                    _stamp.localScale = Vector3.one * 2.4f;
                    _stamp.DOScale(1f, 0.22f).SetEase(Ease.InQuad).SetUpdate(true).SetTarget(this)
                          .OnComplete(() => { Punch(_stamp, 0.12f); Shake(8f * intensity); });
                    PlaySlam(1.15f);
                });
                statsEnd += 0.35f;
            }

            // 4: kartlar ve butonlar
            _seq.Insert(statsEnd, _cardsGroup.DOFade(1f, 0.25f));
            for (int i = 0; i < _cardIcons.Count; i++)
                _seq.Insert(statsEnd + 0.05f + i * 0.06f, _cardIcons[i].DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2.5f));
            float end = statsEnd + 0.25f + _cardIcons.Count * 0.06f;

            _seq.Insert(end, _buttonsGroup.DOFade(1f, 0.3f));
            _buttonsGroup.transform.localScale = Vector3.one * 0.9f;
            _seq.Insert(end, _buttonsGroup.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
            _seq.OnComplete(Finish);
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            Time.timeScale = 0f;
            _buttonsGroup.alpha = 1f;
            _buttonsGroup.interactable = true;
            _buttonsGroup.blocksRaycasts = true;
            EventSystem.current?.SetSelectedGameObject(_newRunButton.gameObject);
        }

        /// <summary>Animasyonu sonuna atlatır (tıklama / tuş).</summary>
        private void Skip()
        {
            if (_finished || _seq == null) return;
            _seq.Complete(true);
            foreach (var l in _letters) { l.alpha = 1f; l.rectTransform.localScale = Vector3.one; l.rectTransform.localRotation = Quaternion.identity; }
            foreach (var r in _rows) r.value.text = r.fmt(r.target);
            foreach (var c in _cardIcons) c.localScale = Vector3.one;
            if (_stats.NewRecord) { _stamp.gameObject.SetActive(true); _stamp.localScale = Vector3.one; }
            _cardsGroup.alpha = 1f; _statsGroup.alpha = 1f; _subtitle.alpha = 1f;
            _flash.color = new Color(1f, 1f, 1f, 0f);
            Finish();
        }

        private void Leave(Action next)
        {
            if (!_finished) return;
            _buttonsGroup.interactable = false;
            GameStateControllerLock(false);
            Time.timeScale = 1f;
            next?.Invoke();
        }

        private void Update()
        {
            _rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 6f);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 1.6f) * 0.04f;
            _glow.rectTransform.localScale = new Vector3(pulse, pulse, 1f);

            UpdateConfetti(Time.unscaledDeltaTime);

            bool any =
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame ||
                                              Keyboard.current.spaceKey.wasPressedThisFrame ||
                                              Keyboard.current.escapeKey.wasPressedThisFrame)) ||
                (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                                             Gamepad.current.startButton.wasPressedThisFrame)) ||
                (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);

            // Son hamleyi yapan tıklama ya da peş peşe basılan tuşlar gösteriyi
            // ilk anda yutmasın: atlama, perde indikten sonra kabul edilir.
            if (!_finished) { if (any && Time.unscaledTime - _openedAt > 1.3f) Skip(); return; }

            // Açıkken arada bir konfeti yağar: ekran ölü kalmasın
            _rainTimer -= Time.unscaledDeltaTime;
            if (_rainTimer <= 0f && !GameSettings.ReduceMotion)
            {
                _rainTimer = 0.09f;
                SpawnRain();
            }

            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) Leave(_onMainMenu);
        }

        // ── Efekt parçaları ──────────────────────────────────────────────────

        private void LetterLanded(int i, float intensity)
        {
            var rt = _letters[i].rectTransform;
            Punch(rt, 0.22f);
            Shake(5f * intensity);
            if (i == 0 || i == _letters.Count - 1) PlaySlam(1f + i * 0.02f);

            // Harfin oturduğu yerden küçük kıvılcım patlaması
            Vector2 at = (Vector2)_root.InverseTransformPoint(rt.position);
            for (int k = 0; k < 7; k++)
            {
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float sp  = UnityEngine.Random.Range(260f, 620f);
                SpawnPiece(at, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp, LetterColors[i % LetterColors.Length], 0.55f, small: true);
            }
        }

        private void StartTitleWave()
        {
            if (GameSettings.ReduceMotion) return;
            for (int i = 0; i < _letters.Count; i++)
            {
                var rt = _letters[i].rectTransform;
                // ölçekle: harfler yerleşim grubunda, konumlarını grup yönetiyor
                rt.DOScale(1.07f, 0.55f).SetEase(Ease.InOutSine)
                  .SetLoops(-1, LoopType.Yoyo).SetDelay(i * 0.09f).SetUpdate(true).SetTarget(this);
            }
        }

        private void PlaySlam(float pitchHint)
        {
            if (_slamClip != null) AudioManager.Instance?.PlaySFX(_slamClip);
        }

        private void Punch(RectTransform rt, float amount)
        {
            rt.DOKill(complete: true);
            rt.DOPunchScale(Vector3.one * amount, 0.3f, 8, 0.6f).SetUpdate(true).SetTarget(this);
        }

        private void Shake(float pixels)
        {
            if (pixels <= 0f) return;
            float s = pixels * GameSettings.ScreenShake;
            if (s <= 0.01f) return;
            _column.DOKill(complete: true);
            _column.DOShakeAnchorPos(0.25f, s, 30, 90f, false, true).SetUpdate(true).SetTarget(this);
        }

        // ── Konfeti ──────────────────────────────────────────────────────────
        // Havuzlu UI dikdörtgenleri; fizik unscaled zamanla (oyun durmuşken akar).

        private RectTransform _confettiLayer;
        private struct Piece { public RectTransform Rt; public Image Img; public Vector2 Pos, Vel; public float Rot, Spin, Life, MaxLife, Flutter; public bool Alive; }
        private readonly List<Piece> _pieces = new();
        private float _rainTimer;

        private void ConfettiCannons(float intensity)
        {
            var r = _root.rect;
            int n = Mathf.RoundToInt(70 * intensity * Mathf.Max(0.3f, GameSettings.VfxIntensity));
            for (int side = 0; side < 2; side++)
            {
                Vector2 origin = new Vector2(side == 0 ? r.xMin + 40f : r.xMax - 40f, r.yMin + 40f);
                for (int i = 0; i < n; i++)
                {
                    float ang = Mathf.Deg2Rad * (side == 0 ? UnityEngine.Random.Range(55f, 80f) : UnityEngine.Random.Range(100f, 125f));
                    float sp  = UnityEngine.Random.Range(900f, 1700f) * (r.height / 1080f);
                    SpawnPiece(origin, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp,
                               LetterColors[UnityEngine.Random.Range(0, LetterColors.Length)], UnityEngine.Random.Range(2.2f, 3.4f));
                }
            }
        }

        private void SpawnRain()
        {
            var r = _root.rect;
            var at = new Vector2(UnityEngine.Random.Range(r.xMin, r.xMax), r.yMax + 20f);
            SpawnPiece(at, new Vector2(UnityEngine.Random.Range(-60f, 60f), UnityEngine.Random.Range(-160f, -60f)),
                       LetterColors[UnityEngine.Random.Range(0, LetterColors.Length)], 6f);
        }

        private void SpawnPiece(Vector2 pos, Vector2 vel, Color color, float life, bool small = false)
        {
            int slot = -1;
            for (int i = 0; i < _pieces.Count; i++) if (!_pieces[i].Alive) { slot = i; break; }
            if (slot < 0)
            {
                if (_pieces.Count >= 260) return;
                var rt = NewRect("p", _confettiLayer);
                var img = rt.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                _pieces.Add(new Piece { Rt = rt, Img = img });
                slot = _pieces.Count - 1;
            }
            var p = _pieces[slot];
            p.Alive = true; p.Pos = pos; p.Vel = vel; p.Life = 0f; p.MaxLife = life;
            p.Rot = UnityEngine.Random.Range(0f, 360f); p.Spin = UnityEngine.Random.Range(-540f, 540f);
            p.Flutter = UnityEngine.Random.Range(0f, 10f);
            p.Img.color = color;
            p.Rt.sizeDelta = small ? new Vector2(10f, 10f) : new Vector2(UnityEngine.Random.Range(12f, 18f), UnityEngine.Random.Range(20f, 30f));
            p.Rt.gameObject.SetActive(true);
            _pieces[slot] = p;
        }

        private void UpdateConfetti(float dt)
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                if (!p.Alive) continue;
                p.Life += dt;
                if (p.Life >= p.MaxLife) { p.Alive = false; p.Rt.gameObject.SetActive(false); _pieces[i] = p; continue; }
                p.Vel.y -= 1500f * dt;                          // yerçekimi
                p.Vel *= 1f - 1.6f * dt;                         // hava direnci: kâğıt gibi süzülür
                p.Vel.x += Mathf.Sin((p.Life + p.Flutter) * 7f) * 160f * dt;
                p.Pos += p.Vel * dt;
                p.Rot += p.Spin * dt;
                p.Rt.anchoredPosition = p.Pos;
                // Dönerken incelir — kâğıdın kenarı görünüyormuş gibi
                float flip = Mathf.Abs(Mathf.Cos((p.Life + p.Flutter) * 9f));
                p.Rt.localScale = new Vector3(0.25f + 0.75f * flip, 1f, 1f);
                p.Rt.localRotation = Quaternion.Euler(0f, 0f, p.Rot);
                var c = p.Img.color; c.a = Mathf.Clamp01((p.MaxLife - p.Life) / 0.5f); p.Img.color = c;
                _pieces[i] = p;
            }
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private static void GameStateControllerLock(bool locked)
        {
            if (locked) Game.GameStateController.LockInput();
            else        Game.GameStateController.Reset();
        }

        private static string FormatTime(float seconds)
        {
            int s = Mathf.RoundToInt(seconds);
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }

        private Image FullImage(string name, Color c)
        {
            var img = NewImage(name, _root, null, c);
            Stretch(img.rectTransform);
            return img;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color c)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = c; img.raycastTarget = false;
            return img;
        }

        private static TMP_Text NewText(string name, Transform parent, float size, Color c, FontStyles style)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = size; t.color = c; t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        private static void Size(RectTransform rt, float w, float h) => rt.sizeDelta = new Vector2(w, h);

        private static void Pref(RectTransform rt, float h)
        {
            var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h; le.minHeight = h;
        }

        private static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static Color WithA(Color c, float a) { c.a = a; return c; }

        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        /// <summary>Kod içinde üretilen sprite'lar — asset gerekmez.</summary>
        private static class VictorySprites
        {
            private static Sprite _panel, _rim, _rays;
            public static Sprite Panel    => _panel ??= Rounded(0f);
            public static Sprite PanelRim => _rim   ??= Rounded(3f);
            public static Sprite Rays     => _rays  ??= BuildRays();

            private static Sprite Rounded(float thickness)
            {
                const int size = 96; const float radius = 26f; const int border = 34;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                float half = size * 0.5f - 1f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = x + 0.5f - size * 0.5f, cy = y + 0.5f - size * 0.5f;
                    float qx = Mathf.Abs(cx) - (half - radius), qy = Mathf.Abs(cy) - (half - radius);
                    float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                              + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = thickness > 0f
                        ? Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + thickness + 0.5f)
                        : Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
                tex.SetPixels32(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                     SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            }

            /// <summary>Merkezden dışa 16 yumuşak ışın; kenarlara doğru söner.</summary>
            private static Sprite BuildRays()
            {
                const int size = 512; const int rays = 16;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.5f);
                    float ang = Mathf.Atan2(dy, dx);
                    float band = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * rays * 0.5f)), 6f);
                    float fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f);
                    byte a = (byte)Mathf.RoundToInt(band * fade * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
