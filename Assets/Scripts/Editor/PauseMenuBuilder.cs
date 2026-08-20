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
        static readonly Color Dimmer     = new Color(0.02f, 0.02f, 0.05f, 0.86f);
        static readonly Color WindowBg   = new Color32(0x14, 0x14, 0x28, 0xFF);
        static readonly Color WindowEdge = new Color32(0x2F, 0xE6, 0xFF, 0xFF);
        static readonly Color Accent     = new Color32(0x2F, 0xE6, 0xFF, 0xFF);
        static readonly Color BtnNeutral = new Color32(0x2A, 0x2A, 0x4A, 0xFF);
        static readonly Color BtnResume  = new Color32(0x21, 0xC9, 0x7A, 0xFF);
        static readonly Color BtnDanger  = new Color32(0xC7, 0x33, 0x33, 0xFF);

        const float WindowW = 620f, WindowH = 640f;
        const float BtnW = 460f, BtnH = 78f;

        static TMP_FontAsset _bold, _regular;
        static Sprite _round;

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

            LoadAssets();
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

        static void LoadAssets()
        {
            _bold    = FindFont("Eczar-Bold SDF") ?? FindFont("Eczar-SemiBold SDF");
            _regular = FindFont("Eczar-Regular SDF") ?? _bold;
            _round   = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        static TMP_FontAsset FindFont(string name) =>
            AssetDatabase.FindAssets($"t:TMP_FontAsset {name}")
                .Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(f => f != null && f.name == name);

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
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        static TextMeshProGUILocalized LocText(string name, Transform parent, string key, float size,
                                               Color color, bool bold, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent);
            rt.gameObject.AddComponent<LocalizationKeyHolder>().LocalizationKey = key;
            var t = rt.gameObject.AddComponent<TextMeshProGUILocalized>();
            t.fontSize = size; t.color = color; t.font = bold ? _bold : _regular;
            t.alignment = align; t.raycastTarget = false; t.enableWordWrapping = false;
            return t;
        }

        static Button MenuButton(Transform parent, string name, string locKey, Color bg, Color? labelColor = null)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(BtnW, BtnH);
            AddImage(rt, bg, _round);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = rt.GetComponent<Image>();

            var c = btn.colors;
            c.highlightedColor = Color.Lerp(bg, Color.white, 0.22f);
            c.pressedColor     = Color.Lerp(bg, Color.black, 0.25f);
            c.fadeDuration     = 0.08f;
            btn.colors = c;

            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = BtnW; le.preferredHeight = BtnH; le.flexibleWidth = 0;

            var label = LocText("Label", rt, locKey, 30f, labelColor ?? Color.white, true, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform);
            return btn;
        }

        static RectTransform BuildPauseMenu(Transform canvas, GameObject settingsPanel)
        {
            // Kök HER ZAMAN açık kalır — PauseMenuController burada yaşar ve ESC'yi dinler.
            // Açılıp kapanan şey alttaki "Content"; kök kapanırsa Update() durur ve ESC ölür.
            var root = NewRect("PauseMenu", canvas);
            Stretch(root);

            var content = NewRect("Content", root);
            Stretch(content);
            AddImage(content, Dimmer);

            var edge = NewRect("Window", content);
            edge.anchorMin = edge.anchorMax = new Vector2(0.5f, 0.5f);
            edge.pivot = new Vector2(0.5f, 0.5f);
            edge.sizeDelta = new Vector2(WindowW, WindowH);
            AddImage(edge, WindowEdge, _round);

            var body = NewRect("Body", edge);
            Stretch(body, 4, 4, 4, 4);
            AddImage(body, WindowBg, _round);

            var title = LocText("Title", body, "Pause.Title", 44f, Accent, true, TextAlignmentOptions.Center);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.offsetMin = new Vector2(0, -110); trt.offsetMax = new Vector2(0, -30);

            var list = NewRect("Buttons", body);
            list.anchorMin = new Vector2(0.5f, 0); list.anchorMax = new Vector2(0.5f, 1);
            list.pivot = new Vector2(0.5f, 0.5f);
            list.sizeDelta = new Vector2(BtnW, -150);
            list.anchoredPosition = new Vector2(0, -55);
            var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 18; vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = false; vlg.childForceExpandWidth = false;
            vlg.childControlHeight = false; vlg.childForceExpandHeight = false;

            var resume   = MenuButton(list, "ResumeButton",   "Pause.Resume",   BtnResume,
                                      new Color32(0x07, 0x1A, 0x12, 0xFF));
            var settings = MenuButton(list, "SettingsButton", "Pause.Settings", BtnNeutral);
            var mainMenu = MenuButton(list, "MainMenuButton", "Pause.MainMenu", BtnNeutral);
            var quit     = MenuButton(list, "QuitButton",     "Pause.Quit",     BtnDanger);

            // ── Çıkış onay kutusu ────────────────────────────────────────────
            var confirm = NewRect("ConfirmBox", content);
            confirm.anchorMin = confirm.anchorMax = new Vector2(0.5f, 0.5f);
            confirm.pivot = new Vector2(0.5f, 0.5f);
            confirm.sizeDelta = new Vector2(700, 280);
            AddImage(confirm, new Color32(0x3A, 0x14, 0x14, 0xFF), _round);

            var warn = LocText("Warning", confirm, "Dialog.QuitRun.Body", 24f,
                               new Color32(0xFF, 0xD0, 0xD0, 0xFF), false, TextAlignmentOptions.Center);
            warn.enableWordWrapping = true;
            var wrt = (RectTransform)warn.transform;
            wrt.anchorMin = new Vector2(0, 1); wrt.anchorMax = new Vector2(1, 1);
            wrt.pivot = new Vector2(0.5f, 1);
            wrt.offsetMin = new Vector2(30, -150); wrt.offsetMax = new Vector2(-30, -40);

            var crow = NewRect("Buttons", confirm);
            crow.anchorMin = new Vector2(0, 0); crow.anchorMax = new Vector2(1, 0);
            crow.pivot = new Vector2(0.5f, 0);
            crow.offsetMin = new Vector2(30, 30); crow.offsetMax = new Vector2(-30, 110);
            var ch = crow.gameObject.AddComponent<HorizontalLayoutGroup>();
            ch.spacing = 20; ch.childAlignment = TextAnchor.MiddleCenter;
            ch.childControlWidth = false; ch.childForceExpandWidth = false;
            ch.childControlHeight = false;

            var no  = MenuButton(crow, "No",  "Dialog.No",  BtnNeutral);
            var yes = MenuButton(crow, "Yes", "Dialog.Yes", BtnDanger);
            foreach (var b in new[] { no, yes })
            {
                var rt = (RectTransform)b.transform;
                rt.sizeDelta = new Vector2(280, 70);
                var le = b.GetComponent<LayoutElement>();
                le.preferredWidth = 280; le.preferredHeight = 70;
            }
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
