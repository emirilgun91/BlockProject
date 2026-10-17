using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Assets.SimpleLocalization.Scripts;
using RogueBlockBlast.UI;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// Oyun sahnesine (SampleScene) ESC ile açılan duraklatma menüsünü üretir.
    /// Menü: Tools ▸ RogueBlockBlast ▸ Pause Menu ▸ Build In Game Scene
    ///
    /// Üretilenler (Gameplay Canvas altında):
    ///   PauseMenu      → karartma + Resume/Settings/Main Menu/Quit + çıkış onayı
    ///   SettingsPanel  → ayarlar panelinin oyun içi kopyası (SettingsPanelBuilder ile)
    ///
    /// Tekrar çalıştırılabilir: eskisini siler, yenisini kurar.
    /// </summary>
    public static class PauseMenuBuilder
    {
        const float WindowW = 560f, WindowH = 640f;
        const float BtnW = 420f, BtnH = 68f;

        [MenuItem("Tools/RogueBlockBlast/Pause Menu/Build In Game Scene", priority = 70)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[PauseMenu] Play modundayken çalıştırılamaz.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("SampleScene.unity"))
                scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.name == "Gameplay Canvas");
            if (canvas == null) { Debug.LogError("[PauseMenu] 'Gameplay Canvas' bulunamadı."); return; }

            RogueBlockBlast.Core.Localization.Loc.Init();

            // Eskisini temizle — bu araç tekrar çalıştırılabilir olmalı
            var oldPause = canvas.transform.Find("PauseMenu");
            if (oldPause != null) Object.DestroyImmediate(oldPause.gameObject);
            var oldSettings = canvas.transform.Find("SettingsPanel");
            if (oldSettings != null) Object.DestroyImmediate(oldSettings.gameObject);

            // 1) Ayarlar panelinin oyun içi kopyası
            var settingsPanel = SettingsPanelBuilder.BuildInto(canvas.transform, "SettingsPanel");
            settingsPanel.gameObject.SetActive(false);

            // 2) Pause menüsü
            var pause = BuildPauseMenu(canvas.transform, settingsPanel.gameObject);

            // En üstte çizilsinler
            settingsPanel.SetAsLastSibling();
            pause.SetAsLastSibling();
            settingsPanel.SetSiblingIndex(pause.GetSiblingIndex());   // ayarlar pause'un hemen üstünde

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[PauseMenu] Duraklatma menüsü ve oyun içi ayarlar paneli üretildi.");
        }

        static RectTransform BuildPauseMenu(Transform canvas, GameObject settingsPanel)
        {
            // Kök HER ZAMAN açık kalır — PauseMenuController burada yaşar ve ESC'yi dinler.
            // Açılıp kapanan şey alttaki "Content"; kök kapanırsa Update() durur ve ESC ölür.
            var root = UiKit.NewRect("PauseMenu", canvas);
            UiKit.Stretch(root);

            var content = UiKit.NewRect("Content", root);
            UiKit.Stretch(content);
            var scrim = content.gameObject.AddComponent<Image>();
            scrim.color = UiTheme.Scrim;

            var body = UiKit.Window(content, "Window", new Vector2(WindowW, WindowH),
                                    UiTheme.WithAlpha(UiTheme.Accent, 0.16f));

            // Pencere içeriğine göre boylanır: geri bildirim butonu (URL boşken
            // gizli) veya portrait'te çıkış butonu kapanınca boşluk kalmaz.
            // Window, Body'nin tercih edilen yüksekliğini alır; Glow layout dışı.
            var window = (RectTransform)body.parent;
            var wv = window.gameObject.AddComponent<VerticalLayoutGroup>();
            wv.childControlWidth = true; wv.childForceExpandWidth = true;
            wv.childControlHeight = true; wv.childForceExpandHeight = false;
            window.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            window.Find("Glow").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var list = body;
            var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 40, 48);
            vlg.spacing = 14; vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false; vlg.childForceExpandWidth = false;
            vlg.childControlHeight = false; vlg.childForceExpandHeight = false;

            var title = UiKit.LocText("Title", body, "Pause.Title", 40f, UiTheme.Text, true, TextAlignmentOptions.Center);
            title.characterSpacing = 8f;
            ((RectTransform)title.transform).sizeDelta = new Vector2(WindowW - 60, 56);

            // Başlığın altında kısa vurgu çizgisi, ardından butonlara nefes payı
            var accent = UiKit.NewRect("TitleAccent", body);
            accent.sizeDelta = new Vector2(56, 4);
            UiKit.Sliced(accent, UiKit.Pill, UiTheme.Accent, false);
            UiKit.NewRect("Spacer", body).sizeDelta = new Vector2(1, 14);

            var btnSize  = new Vector2(BtnW, BtnH);
            var resume   = UiKit.Button(list, "ResumeButton",   btnSize, UiKit.Style.Primary,   "Pause.Resume");
            var settings = UiKit.Button(list, "SettingsButton", btnSize, UiKit.Style.Secondary, "Pause.Settings");
            var mainMenu = UiKit.Button(list, "MainMenuButton", btnSize, UiKit.Style.Secondary, "Pause.MainMenu");

            // Geri bildirim: tıklamayı ExternalLinkButton kendi Awake'inde
            // bağlar — burada onClick'e dokunulmaz. URL boşken
            // PauseMenuController butonu gizler; adres sonradan girilecek.
            var feedback = UiKit.Button(list, "FeedbackButton", btnSize, UiKit.Style.Secondary, "Pause.Feedback");
            feedback.gameObject.AddComponent<RogueBlockBlast.UI.ExternalLinkButton>();

            var quit     = UiKit.Button(list, "QuitButton",     btnSize, UiKit.Style.Danger,    "Pause.Quit");

            // ── Çıkış onay kutusu ────────────────────────────────────────────
            // Pencerenin üstünde ayrı bir kutu; kırmızı hale "geri dönüşü yok" der.
            var confirmWin = UiKit.Window(content, "ConfirmBox", new Vector2(640, 280),
                                          UiTheme.WithAlpha(UiTheme.Danger, 0.18f));
            var confirm = (RectTransform)confirmWin.parent;
            UiKit.TintFrame(confirmWin, UiTheme.WithAlpha(UiTheme.Danger, 0.6f));

            var warn = UiKit.LocText("Warning", confirmWin, "Dialog.QuitRun.Body", 24f,
                                     UiTheme.Text, false, TextAlignmentOptions.Center);
            warn.textWrappingMode = TextWrappingModes.Normal;
            var wrt = (RectTransform)warn.transform;
            wrt.anchorMin = new Vector2(0, 1); wrt.anchorMax = new Vector2(1, 1);
            wrt.pivot = new Vector2(0.5f, 1);
            wrt.offsetMin = new Vector2(36, -160); wrt.offsetMax = new Vector2(-36, -36);

            var crow = UiKit.NewRect("Buttons", confirmWin);
            crow.anchorMin = new Vector2(0, 0); crow.anchorMax = new Vector2(1, 0);
            crow.pivot = new Vector2(0.5f, 0);
            crow.offsetMin = new Vector2(30, 30); crow.offsetMax = new Vector2(-30, 98);
            var ch = crow.gameObject.AddComponent<HorizontalLayoutGroup>();
            ch.spacing = 16; ch.childAlignment = TextAnchor.MiddleCenter;
            ch.childControlWidth = false; ch.childForceExpandWidth = false;
            ch.childControlHeight = false;

            var no  = UiKit.Button(crow, "No",  new Vector2(250, 64), UiKit.Style.Secondary, "Dialog.No");
            var yes = UiKit.Button(crow, "Yes", new Vector2(250, 64), UiKit.Style.Danger,    "Dialog.Yes");
            confirm.gameObject.SetActive(false);

            // ── Controller ───────────────────────────────────────────────────
            var ctrl = root.gameObject.AddComponent<PauseMenuController>();
            var so = new SerializedObject(ctrl);
            void Set(string f, Object v)
            {
                var p = so.FindProperty(f);
                if (p == null) { Debug.LogWarning("[PauseMenu] alan yok: " + f); return; }
                p.objectReferenceValue = v;
            }
            Set("_root", content.gameObject);
            Set("_settingsPanel", settingsPanel);
            Set("_resumeButton", resume);
            Set("_settingsButton", settings);
            Set("_mainMenuButton", mainMenu);
            Set("_feedbackButton", feedback);
            Set("_quitButton", quit);
            Set("_confirmBox", confirm.gameObject);
            Set("_confirmYes", yes);
            Set("_confirmNo", no);
            so.FindProperty("_mainMenuSceneName").stringValue = "MainMenu";
            so.ApplyModifiedPropertiesWithoutUndo();

            content.gameObject.SetActive(false);   // kök açık kalır, sadece içerik gizlenir
            return root;
        }
    }
}
