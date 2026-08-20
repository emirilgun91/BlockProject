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
        // ── Palet ────────────────────────────────────────────────────────────
        static readonly Color Dimmer      = new Color(0.02f, 0.02f, 0.05f, 0.82f);
        static readonly Color WindowBg    = new Color32(0x14, 0x14, 0x28, 0xFF);
        static readonly Color WindowEdge  = new Color32(0x2F, 0xE6, 0xFF, 0xFF);
        static readonly Color RowBg       = new Color32(0x1B, 0x1B, 0x33, 0xFF);
        static readonly Color RowBgAlt    = new Color32(0x21, 0x21, 0x3D, 0xFF);
        static readonly Color Accent      = new Color32(0x2F, 0xE6, 0xFF, 0xFF);
        static readonly Color AccentDim   = new Color32(0x1E, 0x6E, 0x85, 0xFF);
        static readonly Color TextMain    = new Color32(0xE6, 0xEA, 0xF7, 0xFF);
        static readonly Color TextDim     = new Color32(0x8A, 0x93, 0xAA, 0xFF);
        static readonly Color BtnNeutral  = new Color32(0x2A, 0x2A, 0x4A, 0xFF);
        static readonly Color BtnApply    = new Color32(0x21, 0xC9, 0x7A, 0xFF);
        static readonly Color BtnCancel   = new Color32(0x8F, 0x45, 0xAD, 0xFF);
        static readonly Color BtnDanger   = new Color32(0xC7, 0x33, 0x33, 0xFF);

        const float WindowW = 1040f, WindowH = 920f;
        const float HeaderH = 86f,  FooterH = 100f;
        const float RowH    = 62f,  SectionH = 54f;
        const float CtrlW   = 420f;          // sağdaki kontrol alanı genişliği

        static TMP_FontAsset _fontBold, _fontRegular;
        static Sprite _sprRound, _sprBg, _sprKnob, _sprCheck;
        static int _rowIndex;

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

            // Eski Window'u sakla — silme, pasifleştir
            var old = panel.Find("Window");
            if (old != null)
            {
                old.name = "Window_OLD";
                old.gameObject.SetActive(false);
            }
            var older = panel.Find("Window_OLD (1)");
            if (older != null) Object.DestroyImmediate(older.gameObject);

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
            _fontBold    = FindFont("Eczar-Bold SDF")    ?? FindFont("Eczar-SemiBold SDF");
            _fontRegular = FindFont("Eczar-Regular SDF") ?? _fontBold;

            _sprRound = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _sprBg    = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            _sprKnob  = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _sprCheck = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        }

        static TMP_FontAsset FindFont(string name) =>
            AssetDatabase.FindAssets($"t:TMP_FontAsset {name}")
                .Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(f => f != null && f.name == name);

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

        static Button NewButton(string name, Transform parent, Vector2 size, Color bg,
                                string label = null, string locKey = null, float fontSize = 24f,
                                Color? labelColor = null)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = size;
            AddImage(rt, bg, _sprRound);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = rt.GetComponent<Image>();

            var colors = btn.colors;
            colors.highlightedColor = Color.Lerp(bg, Color.white, 0.20f);
            colors.pressedColor     = Color.Lerp(bg, Color.black, 0.25f);
            colors.disabledColor    = new Color(bg.r, bg.g, bg.b, 0.35f);
            colors.fadeDuration     = 0.08f;
            btn.colors = colors;

            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x; le.preferredHeight = size.y;
            le.flexibleWidth = 0;

            if (locKey != null)
            {
                var t = NewLocText("Label", rt, locKey, fontSize, labelColor ?? Color.white, true, TextAlignmentOptions.Center);
                Stretch((RectTransform)t.transform);
            }
            else if (label != null)
            {
                var t = NewText("Label", rt, label, fontSize, labelColor ?? Color.white, true, TextAlignmentOptions.Center);
                Stretch((RectTransform)t.transform);
            }
            return btn;
        }

        // ── Pencere ──────────────────────────────────────────────────────────
        static RectTransform BuildWindow(Transform panel)
        {
            // Dış çerçeve (kenarlık efekti) + iç gövde
            var edge = NewRect("Window", panel);
            edge.anchorMin = edge.anchorMax = new Vector2(0.5f, 0.5f);
            edge.pivot = new Vector2(0.5f, 0.5f);
            edge.sizeDelta = new Vector2(WindowW, WindowH);
            edge.anchoredPosition = Vector2.zero;
            AddImage(edge, WindowEdge, _sprRound);

            var body = NewRect("Body", edge);
            Stretch(body, 4, 4, 4, 4);
            AddImage(body, WindowBg, _sprRound);
            return body;
        }

        static void BuildHeader(RectTransform window, Refs refs)
        {
            var header = NewRect("Header", window);
            header.anchorMin = new Vector2(0, 1); header.anchorMax = new Vector2(1, 1);
            header.pivot = new Vector2(0.5f, 1);
            header.offsetMin = new Vector2(0, -HeaderH); header.offsetMax = Vector2.zero;
            AddImage(header, new Color32(0x0F, 0x0F, 0x1E, 0xFF), _sprRound);

            var title = NewLocText("Title", header, "Settings.Title", 40f, Accent, true, TextAlignmentOptions.Center);
            Stretch((RectTransform)title.transform, 90, 90);

            refs.Close = NewButton("CloseButton", header, new Vector2(56, 56), BtnNeutral, label: "X", fontSize: 28);
            var crt = (RectTransform)refs.Close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1, 0.5f);
            crt.pivot = new Vector2(1, 0.5f);
            crt.anchoredPosition = new Vector2(-16, 0);

            // Alt ayraç
            var line = NewRect("Line", window);
            line.anchorMin = new Vector2(0, 1); line.anchorMax = new Vector2(1, 1);
            line.pivot = new Vector2(0.5f, 1);
            line.offsetMin = new Vector2(0, -HeaderH - 2); line.offsetMax = new Vector2(0, -HeaderH);
            AddImage(line, AccentDim, raycast: false);
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
            AddImage(footer, new Color32(0x0F, 0x0F, 0x1E, 0xFF), _sprRound);

            var row = NewRect("Buttons", footer);
            Stretch(row, 20, 20, 18, 18);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false; h.childForceExpandWidth = false;
            h.childControlHeight = true; h.childForceExpandHeight = true;

            refs.Cancel = NewButton("CancelButton", row, new Vector2(240, 64), BtnCancel, locKey: "Settings.Cancel", fontSize: 26);
            refs.Apply  = NewButton("ApplyButton",  row, new Vector2(240, 64), BtnApply,  locKey: "Settings.Apply",  fontSize: 26,
                                    labelColor: new Color32(0x07, 0x1A, 0x12, 0xFF));
        }

        // ── Satır tipleri ────────────────────────────────────────────────────
        static RectTransform Section(Transform parent, string locKey)
        {
            var rt = NewRect("Section_" + locKey, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = SectionH; le.minHeight = SectionH;

            var t = NewLocText("Label", rt, locKey, 26f, Accent, true);
            Stretch((RectTransform)t.transform, 12, 0, 12, 0);

            var line = NewRect("Underline", rt);
            line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(1, 0);
            line.pivot = new Vector2(0.5f, 0);
            line.offsetMin = new Vector2(12, 4); line.offsetMax = new Vector2(-12, 6);
            AddImage(line, AccentDim, raycast: false);

            _rowIndex = 0;
            return rt;
        }

        /// <summary>Etiket + sağda kontrol alanı olan bir satır üretir.</summary>
        static RectTransform Row(Transform parent, string locKey, out RectTransform ctrlArea, float height = RowH)
        {
            var rt = NewRect("Row_" + locKey, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height; le.minHeight = height;
            AddImage(rt, (_rowIndex++ % 2 == 0) ? RowBg : RowBgAlt, _sprRound, raycast: false);

            var label = NewLocText("Label", rt, locKey, 23f, TextMain);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = new Vector2(20, 0); lrt.offsetMax = new Vector2(-CtrlW - 20, 0);

            ctrlArea = NewRect("Control", rt);
            ctrlArea.anchorMin = new Vector2(1, 0); ctrlArea.anchorMax = new Vector2(1, 1);
            ctrlArea.pivot = new Vector2(1, 0.5f);
            ctrlArea.sizeDelta = new Vector2(CtrlW, 0);
            ctrlArea.anchoredPosition = new Vector2(-16, 0);
            return rt;
        }

        static Slider SliderRow(Transform parent, string locKey, out TMP_Text valueText,
                                float min = 0f, float max = 1f, bool showPercent = true)
        {
            Row(parent, locKey, out var area);

            valueText = NewText("Value", area, "0", 22f, Accent, true, TextAlignmentOptions.MidlineRight);
            var vrt = (RectTransform)valueText.transform;
            vrt.anchorMin = new Vector2(1, 0); vrt.anchorMax = new Vector2(1, 1);
            vrt.pivot = new Vector2(1, 0.5f);
            vrt.sizeDelta = new Vector2(70, 0);
            vrt.anchoredPosition = Vector2.zero;

            var srt = NewRect("Slider", area);
            srt.anchorMin = new Vector2(0, 0.5f); srt.anchorMax = new Vector2(1, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.offsetMin = new Vector2(0, -9); srt.offsetMax = new Vector2(-84, 9);

            var bg = NewRect("Background", srt);
            Stretch(bg);
            AddImage(bg, new Color32(0x0D, 0x0D, 0x1A, 0xFF), _sprBg);

            var fillArea = NewRect("Fill Area", srt);
            Stretch(fillArea, 6, 12, 0, 0);
            var fill = NewRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(1, 1);
            fill.sizeDelta = new Vector2(10, 0);
            AddImage(fill, Accent, _sprBg);

            var handleArea = NewRect("Handle Slide Area", srt);
            Stretch(handleArea, 6, 6, 0, 0);
            var handle = NewRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(26, 26);
            AddImage(handle, Color.white, _sprKnob);

            var slider = srt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min; slider.maxValue = max;
            slider.wholeNumbers = false;
            return slider;
        }

        static Toggle ToggleRow(Transform parent, string locKey)
        {
            Row(parent, locKey, out var area);

            var trt = NewRect("Toggle", area);
            trt.anchorMin = trt.anchorMax = new Vector2(1, 0.5f);
            trt.pivot = new Vector2(1, 0.5f);
            trt.sizeDelta = new Vector2(46, 46);
            trt.anchoredPosition = Vector2.zero;

            var bg = NewRect("Background", trt);
            Stretch(bg);
            AddImage(bg, new Color32(0x0D, 0x0D, 0x1A, 0xFF), _sprRound);

            var check = NewRect("Checkmark", bg);
            Stretch(check, 6, 6, 6, 6);
            var checkImg = AddImage(check, Accent, _sprCheck, raycast: false);

            var toggle = trt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.graphic = checkImg;
            toggle.isOn = true;
            return toggle;
        }

        /// <summary>Sol/sağ oklu değer seçici (dil, çözünürlük).</summary>
        static void ArrowRow(Transform parent, string locKey, out Button prev, out Button next, out TMP_Text label)
        {
            Row(parent, locKey, out var area);

            prev = NewButton("Prev", area, new Vector2(48, 44), BtnNeutral, label: "<", fontSize: 24);
            var prt = (RectTransform)prev.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0, 0.5f);
            prt.pivot = new Vector2(0, 0.5f);
            prt.anchoredPosition = new Vector2(60, 0);

            next = NewButton("Next", area, new Vector2(48, 44), BtnNeutral, label: ">", fontSize: 24);
            var nrt = (RectTransform)next.transform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(1, 0.5f);
            nrt.pivot = new Vector2(1, 0.5f);
            nrt.anchoredPosition = Vector2.zero;

            label = NewText("Value", area, "-", 22f, Accent, true, TextAlignmentOptions.Center);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = new Vector2(116, 0); lrt.offsetMax = new Vector2(-56, 0);
        }

        /// <summary>Tıklayınca sıradaki seçeneğe geçen tek butonlu satır (VSync, FPS, renk körlüğü).</summary>
        static Button CycleRow(Transform parent, string locKey, out TMP_Text label)
        {
            Row(parent, locKey, out var area);

            var btn = NewButton("Cycle", area, new Vector2(260, 46), BtnNeutral);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1, 0.5f);
            brt.pivot = new Vector2(1, 0.5f);
            brt.anchoredPosition = Vector2.zero;

            label = NewText("Value", brt, "-", 22f, Accent, true, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform, 8, 8);
            return btn;
        }

        static Button ActionRow(Transform parent, string locKey, Color color)
        {
            var rt = NewRect("Row_" + locKey, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = RowH; le.minHeight = RowH;

            var btn = NewButton("Button", rt, new Vector2(420, 50), color, locKey: locKey, fontSize: 22);
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
            refs.ResetSettings = ActionRow(content, "Settings.Data.ResetSettings", BtnNeutral);
            refs.ResetProgress = ActionRow(content, "Settings.Data.ResetProgress", BtnDanger);
            BuildConfirmBox(content, refs);
        }

        /// <summary>İlerleme sıfırlama onay kutusu — normalde gizli.</summary>
        static void BuildConfirmBox(RectTransform content, Refs refs)
        {
            var box = NewRect("ResetConfirm", content);
            var le = box.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 160; le.minHeight = 160;
            AddImage(box, new Color32(0x3A, 0x14, 0x14, 0xFF), _sprRound, raycast: false);

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

            refs.ConfirmNo  = NewButton("No",  row, new Vector2(200, 50), BtnNeutral, locKey: "Dialog.No",  fontSize: 22);
            refs.ConfirmYes = NewButton("Yes", row, new Vector2(200, 50), BtnDanger,  locKey: "Dialog.Yes", fontSize: 22);

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
