using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Assets.SimpleLocalization.Scripts;
using RogueBlockBlast.UI;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// Ayarlar panelinin tüm UI hiyerarşisini sahnede üretir ve
    /// <see cref="SettingsController"/> alanlarını otomatik bağlar.
    ///
    /// Menü: Tools ▸ RogueBlockBlast ▸ Settings Panel ▸ Rebuild
    ///
    /// Mevcut "Window" alt objesi silinmez — "Window_OLD" olarak pasifleştirilir.
    /// Sonuçtan memnunsan onu elle silebilirsin.
    ///
    /// Etiketler SimpleLocalization anahtarlarına bağlıdır (Settings.* — Settings.csv'de hazır).
    /// Değer metinleri (yüzde, çözünürlük vb.) düz TMP'dir; onları SettingsController doldurur.
    /// </summary>
    public static class SettingsPanelBuilder
    {
        // ── Palet — UiTheme'den ──────────────────────────────────────────────
        static Color Dimmer   => UiTheme.Scrim;
        static Color RowBg    => UiTheme.WithAlpha(UiTheme.Raised, 0.45f);
        static Color Accent   => UiTheme.Accent;
        static Color TextMain => UiTheme.Text;

        const float WindowW = 1040f, WindowH = 920f;
        const float HeaderH = 90f,  FooterH = 104f;
        const float RowH    = 62f,  SectionH = 58f;
        const float CtrlW   = 420f;          // sağdaki kontrol alanı genişliği

        static TMP_FontAsset _fontBold, _fontRegular;

        [MenuItem("Tools/RogueBlockBlast/Settings Panel/Rebuild", priority = 60)]
        public static void Rebuild()
        {
            // Play modunda üretilen objeler Play'den çıkınca yok olur — boşuna çalışma.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[SettingsPanel] Play modundayken çalıştırılamaz. Önce Play'den çık, sonra tekrar dene.");
                return;
            }

            var panel = FindPanel();
            if (panel == null)
            {
                Debug.LogError("[SettingsPanel] MainMenuCanvas/SettingsPanel bulunamadı.");
                return;
            }

            LoadAssets();
            RogueBlockBlast.Core.Localization.Loc.Init();

            // Bir önceki yedeği sil, mevcut Window'u yedek olarak pasifleştir —
            // her çalıştırmada tek bir Window_OLD kalır.
            foreach (Transform child in panel.Cast<Transform>().ToList())
                if (child.name.StartsWith("Window_OLD")) Object.DestroyImmediate(child.gameObject);
            var old = panel.Find("Window");
            if (old != null)
            {
                old.name = "Window_OLD";
                old.gameObject.SetActive(false);
            }

            BuildContents(panel);

            // UI ölçeği ayarının çalışması için Canvas'a uygulayıcıyı ekle
            var canvas = panel.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.GetComponent<CanvasScaler>() != null &&
                canvas.GetComponent<UiScaleApplier>() == null)
                canvas.gameObject.AddComponent<UiScaleApplier>();

            EditorUtility.SetDirty(panel.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            Debug.Log("[SettingsPanel] Panel yeniden üretildi ve SettingsController bağlandı.");
        }

        /// <summary>
        /// Verilen parent altında sıfırdan yeni bir ayarlar paneli üretir.
        /// Oyun sahnesindeki pause menüsü bunu kullanır.
        /// </summary>
        public static RectTransform BuildInto(Transform parent, string name)
        {
            LoadAssets();
            RogueBlockBlast.Core.Localization.Loc.Init();

            var panel = NewRect(name, parent);
            EnsurePanelRoot(panel);
            BuildContents(panel);
            return panel;
        }

        /// <summary>Panel kökü hazırken içeriği kurar ve controller'ı bağlar.</summary>
        static void BuildContents(Transform panel)
        {
            EnsurePanelRoot(panel);

            var window = BuildWindow(panel);
            var refs   = new Refs();

            BuildHeader(window, refs);
            var content = BuildScrollArea(window);
            BuildSections(content, refs);
            BuildFooter(window, refs);

            AssignController(panel, refs);
        }

        // ── Referans toplayıcı ───────────────────────────────────────────────
        class Refs
        {
            public Slider Master, Music, Sfx, Shake, Vfx, UiScale, DragOffset;
            public TMP_Text MasterV, MusicV, SfxV, ShakeV, VfxV, UiScaleV;
            public Toggle Mute, MuteUnfocused, Fullscreen, ShowFps,
                          Ghost, Highlight, Grid, ConfirmQuit, Haptics,
                          ReduceMotion, ReduceFlashing, LargeText;
            public Button LangPrev, LangNext, ResPrev, ResNext, VSync, Fps, Colorblind;
            public TMP_Text LangLabel, ResLabel, VSyncLabel, FpsLabel, ColorblindLabel;
            public Button ResetSettings, ResetProgress, ConfirmYes, ConfirmNo;
            public GameObject ConfirmBox;
            public Button Apply, Cancel, Close;
        }

        // ── Kurulum ──────────────────────────────────────────────────────────
        static Transform FindPanel()
        {
            var all = Resources.FindObjectsOfTypeAll<RectTransform>()
                .FirstOrDefault(r => r.name == "SettingsPanel" && r.gameObject.scene.IsValid());
            return all;
        }

        static void LoadAssets()
        {
            _fontBold    = UiKit.Bold;
            _fontRegular = UiKit.Regular;
        }

        static void EnsurePanelRoot(Transform panel)
        {
            var rt = panel as RectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
            img.color = Dimmer;
            img.raycastTarget = true;      // arkadaki menüye tıklamayı engelle

            if (panel.GetComponent<CanvasGroup>() == null) panel.gameObject.AddComponent<CanvasGroup>();
        }

        // ── Yapı taşları ─────────────────────────────────────────────────────
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image AddImage(RectTransform rt, Color color, Sprite sprite = null, bool raycast = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
            img.raycastTarget = raycast;
            return img;
        }

        static void Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        static TextMeshProUGUI NewText(string name, Transform parent, string text, float size,
                                       Color color, bool bold = false,
                                       TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.font = bold ? _fontBold : _fontRegular;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>Lokalizasyon anahtarına bağlı etiket (dil değişince kendini günceller).</summary>
        static TextMeshProGUILocalized NewLocText(string name, Transform parent, string key, float size,
                                                  Color color, bool bold = false,
                                                  TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rt = NewRect(name, parent);
            var holder = rt.gameObject.AddComponent<LocalizationKeyHolder>();
            holder.LocalizationKey = key;
            var t = rt.gameObject.AddComponent<TextMeshProGUILocalized>();
            t.fontSize = size;
            t.color = color;
            t.font = bold ? _fontBold : _fontRegular;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        static Button NewButton(string name, Transform parent, Vector2 size, UiKit.Style style,
                                string label = null, string locKey = null, float fontSize = 24f)
            => UiKit.Button(parent, name, size, style, locKey, label, fontSize);

        // ── Pencere ──────────────────────────────────────────────────────────
        static RectTransform BuildWindow(Transform panel)
            => UiKit.Window(panel, "Window", new Vector2(WindowW, WindowH), UiTheme.WithAlpha(UiTheme.Accent, 0.12f));

        static void BuildHeader(RectTransform window, Refs refs)
        {
            var header = NewRect("Header", window);
            header.anchorMin = new Vector2(0, 1); header.anchorMax = new Vector2(1, 1);
            header.pivot = new Vector2(0.5f, 1);
            header.offsetMin = new Vector2(0, -HeaderH); header.offsetMax = Vector2.zero;

            var title = NewLocText("Title", header, "Settings.Title", 36f, TextMain, true, TextAlignmentOptions.Center);
            title.characterSpacing = 8f;
            Stretch((RectTransform)title.transform, 90, 90);

            refs.Close = NewButton("CloseButton", header, new Vector2(52, 52), UiKit.Style.Ghost, label: "X", fontSize: 24);
            var crt = (RectTransform)refs.Close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1, 0.5f);
            crt.pivot = new Vector2(1, 0.5f);
            crt.anchoredPosition = new Vector2(-20, 0);

            // Alt ayraç — kenarlardan içeride, ince
            var line = UiKit.Divider(window, "Line");
            line.anchorMin = new Vector2(0, 1); line.anchorMax = new Vector2(1, 1);
            line.pivot = new Vector2(0.5f, 1);
            line.offsetMin = new Vector2(28, -HeaderH - 1); line.offsetMax = new Vector2(-28, -HeaderH);
        }

        static RectTransform BuildScrollArea(RectTransform window)
        {
            var scrollRT = NewRect("ScrollView", window);
            Stretch(scrollRT, 10, 10, HeaderH + 6, FooterH + 6);
            var scroll = scrollRT.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var viewport = NewRect("Viewport", scrollRT);
            Stretch(viewport);
            AddImage(viewport, new Color(1, 1, 1, 0.004f), raycast: true);   // raycast için görünmez zemin
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(6, 0); content.offsetMax = new Vector2(-6, 0);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(4, 4, 8, 16);
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            return content;
        }

        static void BuildFooter(RectTransform window, Refs refs)
        {
            var footer = NewRect("Footer", window);
            footer.anchorMin = new Vector2(0, 0); footer.anchorMax = new Vector2(1, 0);
            footer.pivot = new Vector2(0.5f, 0);
            footer.offsetMin = Vector2.zero; footer.offsetMax = new Vector2(0, FooterH);

            var line = UiKit.Divider(footer, "Line");
            line.anchorMin = new Vector2(0, 1); line.anchorMax = new Vector2(1, 1);
            line.pivot = new Vector2(0.5f, 1);
            line.offsetMin = new Vector2(28, -1); line.offsetMax = new Vector2(-28, 0);

            var row = NewRect("Buttons", footer);
            Stretch(row, 20, 20, 20, 20);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false; h.childForceExpandWidth = false;
            h.childControlHeight = false; h.childForceExpandHeight = false;

            refs.Cancel = NewButton("CancelButton", row, new Vector2(240, 62), UiKit.Style.Secondary, locKey: "Settings.Cancel", fontSize: 24);
            refs.Apply  = NewButton("ApplyButton",  row, new Vector2(240, 62), UiKit.Style.Primary,   locKey: "Settings.Apply",  fontSize: 24);
        }

        // ── Satır tipleri ────────────────────────────────────────────────────
        static RectTransform Section(Transform parent, string locKey)
        {
            var rt = NewRect("Section_" + locKey, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = SectionH; le.minHeight = SectionH;

            var t = NewLocText("Label", rt, locKey, 20f, Accent, true);
            t.characterSpacing = 6f;
            t.alignment = TextAlignmentOptions.BottomLeft;
            Stretch((RectTransform)t.transform, 14, 0, 0, 12);

            var line = UiKit.Divider(rt, "Underline");
            line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(1, 0);
            line.pivot = new Vector2(0.5f, 0);
            line.offsetMin = new Vector2(14, 4); line.offsetMax = new Vector2(-14, 5);
            return rt;
        }

        /// <summary>Etiket + sağda kontrol alanı olan bir satır üretir.</summary>
        static RectTransform Row(Transform parent, string locKey, out RectTransform ctrlArea, float height = RowH)
        {
            var rt = NewRect("Row_" + locKey, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height; le.minHeight = height;
            UiKit.Panel(rt, RowBg, UiTheme.RadiusControl, raycast: false);

            var label = NewLocText("Label", rt, locKey, 23f, TextMain);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = new Vector2(22, 0); lrt.offsetMax = new Vector2(-CtrlW - 20, 0);

            ctrlArea = NewRect("Control", rt);
            ctrlArea.anchorMin = new Vector2(1, 0); ctrlArea.anchorMax = new Vector2(1, 1);
            ctrlArea.pivot = new Vector2(1, 0.5f);
            ctrlArea.sizeDelta = new Vector2(CtrlW, 0);
            ctrlArea.anchoredPosition = new Vector2(-18, 0);
            return rt;
        }

        static Slider SliderRow(Transform parent, string locKey, out TMP_Text valueText,
                                float min = 0f, float max = 1f, bool showPercent = true)
        {
            Row(parent, locKey, out var area);

            valueText = NewText("Value", area, "0", 22f, TextMain, true, TextAlignmentOptions.MidlineRight);
            var vrt = (RectTransform)valueText.transform;
            vrt.anchorMin = new Vector2(1, 0); vrt.anchorMax = new Vector2(1, 1);
            vrt.pivot = new Vector2(1, 0.5f);
            vrt.sizeDelta = new Vector2(70, 0);
            vrt.anchoredPosition = Vector2.zero;

            var srt = NewRect("Slider", area);
            srt.anchorMin = new Vector2(0, 0.5f); srt.anchorMax = new Vector2(1, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.offsetMin = new Vector2(0, -14); srt.offsetMax = new Vector2(-88, 14);

            // İz: koyu hap, ortada dikeyde 10px. İnce kenar bu boyda topaklanıyordu, yok.
            var bg = NewRect("Background", srt);
            Stretch(bg, 0, 0, 9, 9);
            var trackSkin = UiKit.Skin("Skin_Track");
            UiKit.Sliced(bg, trackSkin ?? UiKit.Pill, trackSkin != null ? Color.white : UiTheme.Sunken, false);

            var fillArea = NewRect("Fill Area", srt);
            Stretch(fillArea, 0, 12, 9, 9);
            var fill = NewRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(1, 1);
            fill.sizeDelta = new Vector2(12, 0);
            UiKit.Sliced(fill, UiKit.Skin("Skin_Fill") ?? UiKit.Pill, Accent, false);

            var handleArea = NewRect("Handle Slide Area", srt);
            Stretch(handleArea, 12, 12, 0, 0);
            var handle = NewRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(26, 0);
            var handleImg = UiKit.Sliced(handle, UiKit.Pill, Color.white, true);
            handleImg.type = Image.Type.Simple;
            handleImg.preserveAspect = true;

            var slider = srt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            var c = slider.colors;
            c.normalColor = Color.white; c.highlightedColor = UiTheme.Accent;
            c.pressedColor = UiTheme.Accent; c.selectedColor = Color.white;
            c.fadeDuration = 0.08f;
            slider.colors = c;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min; slider.maxValue = max;
            slider.wholeNumbers = false;
            return slider;
        }

        /// <summary>Kayan anahtar: hap şeklinde iz + topuz. Görseli KitSwitch sürer.</summary>
        static Toggle ToggleRow(Transform parent, string locKey)
        {
            Row(parent, locKey, out var area);

            var trt = NewRect("Toggle", area);
            trt.anchorMin = trt.anchorMax = new Vector2(1, 0.5f);
            trt.pivot = new Vector2(1, 0.5f);
            trt.sizeDelta = new Vector2(66, 36);
            trt.anchoredPosition = Vector2.zero;

            var track = NewRect("Background", trt);
            Stretch(track);
            var trackImg = UiKit.Sliced(track, UiKit.Pill, UiTheme.Sunken, true);
            var rim = NewRect("Rim", track);
            Stretch(rim);
            var rimImg = UiKit.Sliced(rim, UiKit.PillRim, UiTheme.RimSoft, false);

            var knob = NewRect("Knob", trt);
            knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
            knob.pivot = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(26, 26);
            var knobImg = UiKit.Sliced(knob, UiKit.Pill, UiTheme.TextDim, false);
            knobImg.type = Image.Type.Simple;

            var toggle = trt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = trackImg;
            toggle.graphic = null;
            toggle.transition = Selectable.Transition.None;
            toggle.isOn = true;

            trt.gameObject.AddComponent<KitSwitch>().Configure(trackImg, rimImg, knob, knobImg, 15f);
            return toggle;
        }

        /// <summary>Sol/sağ oklu değer seçici (dil, çözünürlük).</summary>
        static void ArrowRow(Transform parent, string locKey, out Button prev, out Button next, out TMP_Text label)
        {
            Row(parent, locKey, out var area);

            prev = NewButton("Prev", area, new Vector2(46, 42), UiKit.Style.Secondary, label: "<", fontSize: 22);
            var prt = (RectTransform)prev.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0, 0.5f);
            prt.pivot = new Vector2(0, 0.5f);
            prt.anchoredPosition = new Vector2(60, 0);

            next = NewButton("Next", area, new Vector2(46, 42), UiKit.Style.Secondary, label: ">", fontSize: 22);
            var nrt = (RectTransform)next.transform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(1, 0.5f);
            nrt.pivot = new Vector2(1, 0.5f);
            nrt.anchoredPosition = Vector2.zero;

            label = NewText("Value", area, "-", 22f, TextMain, true, TextAlignmentOptions.Center);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = new Vector2(116, 0); lrt.offsetMax = new Vector2(-56, 0);
        }

        /// <summary>Tıklayınca sıradaki seçeneğe geçen tek butonlu satır (VSync, FPS, renk körlüğü).</summary>
        static Button CycleRow(Transform parent, string locKey, out TMP_Text label)
        {
            Row(parent, locKey, out var area);

            var btn = NewButton("Cycle", area, new Vector2(260, 44), UiKit.Style.Secondary);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1, 0.5f);
            brt.pivot = new Vector2(1, 0.5f);
            brt.anchoredPosition = Vector2.zero;

            label = NewText("Value", brt, "-", 21f, Accent, true, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform, 8, 8);
            return btn;
        }

        static Button ActionRow(Transform parent, string locKey, UiKit.Style style)
        {
            var rt = NewRect("Row_" + locKey, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = RowH; le.minHeight = RowH;

            var btn = NewButton("Button", rt, new Vector2(420, 50), style, locKey: locKey, fontSize: 21);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            return btn;
        }

        // ── İçerik ───────────────────────────────────────────────────────────
        static void BuildSections(RectTransform content, Refs refs)
        {
            // AUDIO
            Section(content, "Settings.Section.Audio");
            refs.Master = SliderRow(content, "Settings.Audio.Master", out refs.MasterV);
            refs.Music  = SliderRow(content, "Settings.Audio.Music",  out refs.MusicV);
            refs.Sfx    = SliderRow(content, "Settings.Audio.Sfx",    out refs.SfxV);
            refs.Mute            = ToggleRow(content, "Settings.Audio.Mute");
            refs.MuteUnfocused   = ToggleRow(content, "Settings.Audio.MuteUnfocused");

            // DISPLAY
            Section(content, "Settings.Section.Display");
            refs.Fullscreen = ToggleRow(content, "Settings.Display.Fullscreen");
            ArrowRow(content, "Settings.Display.Resolution", out refs.ResPrev, out refs.ResNext, out refs.ResLabel);
            refs.VSync   = CycleRow(content, "Settings.Display.VSync",    out refs.VSyncLabel);
            refs.Fps     = CycleRow(content, "Settings.Display.FpsLimit", out refs.FpsLabel);
            refs.ShowFps = ToggleRow(content, "Settings.Display.ShowFps");

            // GAMEPLAY
            Section(content, "Settings.Section.Gameplay");
            refs.Ghost       = ToggleRow(content, "Settings.Gameplay.Ghost");
            refs.Highlight   = ToggleRow(content, "Settings.Gameplay.Highlight");
            refs.Grid        = ToggleRow(content, "Settings.Gameplay.Grid");
            refs.ConfirmQuit = ToggleRow(content, "Settings.Gameplay.ConfirmQuit");
            refs.Haptics     = ToggleRow(content, "Settings.Gameplay.Haptics");
            refs.DragOffset  = SliderRow(content, "Settings.Gameplay.DragOffset", out _, 0f, 2f);

            // ACCESSIBILITY
            Section(content, "Settings.Section.Accessibility");
            refs.ReduceMotion   = ToggleRow(content, "Settings.Access.ReduceMotion");
            refs.ReduceFlashing = ToggleRow(content, "Settings.Access.ReduceFlashing");
            refs.Shake   = SliderRow(content, "Settings.Access.ScreenShake", out refs.ShakeV);
            refs.Vfx     = SliderRow(content, "Settings.Access.Vfx",         out refs.VfxV);
            refs.Colorblind = CycleRow(content, "Settings.Access.Colorblind", out refs.ColorblindLabel);
            refs.UiScale = SliderRow(content, "Settings.Access.UiScale", out refs.UiScaleV, 0.8f, 1.4f);
            refs.LargeText = ToggleRow(content, "Settings.Access.LargeText");

            // LANGUAGE
            Section(content, "Settings.Section.Language");
            ArrowRow(content, "Settings.Section.Language", out refs.LangPrev, out refs.LangNext, out refs.LangLabel);

            // DATA
            Section(content, "Settings.Section.Data");
            refs.ResetSettings = ActionRow(content, "Settings.Data.ResetSettings", UiKit.Style.Secondary);
            refs.ResetProgress = ActionRow(content, "Settings.Data.ResetProgress", UiKit.Style.Danger);
            BuildConfirmBox(content, refs);
        }

        /// <summary>İlerleme sıfırlama onay kutusu — normalde gizli.</summary>
        static void BuildConfirmBox(RectTransform content, Refs refs)
        {
            var box = NewRect("ResetConfirm", content);
            var le = box.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 160; le.minHeight = 160;
            UiKit.Panel(box, UiTheme.DangerDeep, UiTheme.RadiusControl, UiTheme.WithAlpha(UiTheme.Danger, 0.6f), raycast: false);

            var warn = NewLocText("Warning", box, "Settings.Data.ResetProgress.Warn", 20f,
                                  new Color32(0xFF, 0xB3, 0xB3, 0xFF), false, TextAlignmentOptions.Center);
            var wrt = (RectTransform)warn.transform;
            wrt.anchorMin = new Vector2(0, 1); wrt.anchorMax = new Vector2(1, 1);
            wrt.pivot = new Vector2(0.5f, 1);
            wrt.offsetMin = new Vector2(20, -84); wrt.offsetMax = new Vector2(-20, -12);
            warn.enableWordWrapping = true;

            var row = NewRect("Buttons", box);
            row.anchorMin = new Vector2(0, 0); row.anchorMax = new Vector2(1, 0);
            row.pivot = new Vector2(0.5f, 0);
            row.offsetMin = new Vector2(20, 14); row.offsetMax = new Vector2(-20, 74);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false; h.childForceExpandWidth = false;
            h.childControlHeight = true;

            refs.ConfirmNo  = NewButton("No",  row, new Vector2(200, 50), UiKit.Style.Secondary, locKey: "Dialog.No",  fontSize: 22);
            refs.ConfirmYes = NewButton("Yes", row, new Vector2(200, 50), UiKit.Style.Danger,    locKey: "Dialog.Yes", fontSize: 22);

            refs.ConfirmBox = box.gameObject;
            box.gameObject.SetActive(false);
        }

        // ── Controller bağlama ───────────────────────────────────────────────
        static void AssignController(Transform panel, Refs r)
        {
            var ctrl = panel.GetComponent<SettingsController>() ?? panel.gameObject.AddComponent<SettingsController>();
            var so = new SerializedObject(ctrl);

            void Set(string field, Object value)
            {
                var p = so.FindProperty(field);
                if (p == null) { Debug.LogWarning($"[SettingsPanel] alan yok: {field}"); return; }
                p.objectReferenceValue = value;
            }

            Set("_masterSlider", r.Master);       Set("_masterValueText", r.MasterV);
            Set("_musicSlider",  r.Music);        Set("_musicValueText",  r.MusicV);
            Set("_sfxSlider",    r.Sfx);          Set("_sfxValueText",    r.SfxV);
            Set("_muteToggle",   r.Mute);         Set("_muteUnfocusedToggle", r.MuteUnfocused);

            Set("_langPrevButton", r.LangPrev);   Set("_langNextButton", r.LangNext);
            Set("_langLabel",      r.LangLabel);

            Set("_fullscreenToggle", r.Fullscreen);
            Set("_resPrevButton", r.ResPrev);     Set("_resNextButton", r.ResNext);
            Set("_resLabel",      r.ResLabel);
            Set("_vsyncButton",   r.VSync);       Set("_vsyncLabel", r.VSyncLabel);
            Set("_fpsButton",     r.Fps);         Set("_fpsLabel",   r.FpsLabel);
            Set("_showFpsToggle", r.ShowFps);

            Set("_ghostToggle",       r.Ghost);
            Set("_highlightToggle",   r.Highlight);
            Set("_gridToggle",        r.Grid);
            Set("_confirmQuitToggle", r.ConfirmQuit);
            Set("_hapticsToggle",     r.Haptics);
            Set("_dragOffsetSlider",  r.DragOffset);

            Set("_reduceMotionToggle",   r.ReduceMotion);
            Set("_reduceFlashingToggle", r.ReduceFlashing);
            Set("_screenShakeSlider",    r.Shake);  Set("_screenShakeValueText", r.ShakeV);
            Set("_vfxSlider",            r.Vfx);    Set("_vfxValueText",         r.VfxV);
            Set("_colorblindButton",     r.Colorblind); Set("_colorblindLabel",  r.ColorblindLabel);
            Set("_uiScaleSlider",        r.UiScale); Set("_uiScaleValueText",    r.UiScaleV);
            Set("_largeTextToggle",      r.LargeText);

            Set("_resetSettingsButton",     r.ResetSettings);
            Set("_resetProgressButton",     r.ResetProgress);
            Set("_resetProgressConfirm",    r.ConfirmBox);
            Set("_resetProgressConfirmYes", r.ConfirmYes);
            Set("_resetProgressConfirmNo",  r.ConfirmNo);

            Set("_applyButton",  r.Apply);
            Set("_cancelButton", r.Cancel);
            Set("_closeButton",  r.Close);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrl);
        }
    }
}
