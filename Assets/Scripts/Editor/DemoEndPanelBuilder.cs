using System.Linq;
using Assets.SimpleLocalization.Scripts;
using RogueBlockBlast.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// Demo bitiş panelini oyun sahnesine (SampleScene) üretir.
    /// Menü: Tools ▸ RogueBlockBlast ▸ Demo ▸ Build End Panel
    ///
    /// Üretilen (Gameplay Canvas altında):
    ///   DemoEndPanel → karartma + başlık + metin + 3 buton
    ///
    /// Butonlardan ikisi (<see cref="ExternalLinkButton"/>) URL bekler.
    /// URL boşken <see cref="DemoEndPanel"/> onları gizler, panel yine düzgün
    /// görünür. Adresler geldiğinde Inspector'dan girilir — bu aracı yeniden
    /// çalıştırmak gerekmez.
    ///
    /// Tekrar çalıştırılabilir: eskisini siler, yenisini kurar.
    /// </summary>
    public static class DemoEndPanelBuilder
    {
        static readonly Color Dimmer     = new Color(0.02f, 0.02f, 0.05f, 0.92f);
        static readonly Color WindowBg   = new Color32(0x14, 0x14, 0x28, 0xFF);
        static readonly Color WindowEdge = new Color32(0xF0, 0xBB, 0x4C, 0xFF);
        static readonly Color TitleColor = new Color32(0xFF, 0xD9, 0xA3, 0xFF);
        static readonly Color BodyColor  = new Color32(0xC5, 0xCD, 0xE0, 0xFF);

        static readonly Color BtnNeutral  = new Color32(0x2A, 0x2A, 0x4A, 0xFF);
        static readonly Color BtnWishlist = new Color32(0xF0, 0xBB, 0x4C, 0xFF);
        static readonly Color BtnFeedback = new Color32(0x4F, 0xC0, 0xEB, 0xFF);

        const float WindowW = 900f, WindowH = 620f;
        const float BtnW    = 300f, BtnH   = 84f;
        const float BtnGap  = 24f;

        static TMP_FontAsset _bold, _regular;
        static Sprite _round;

        [MenuItem("Tools/RogueBlockBlast/Demo/Build End Panel", priority = 110)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[DemoEnd] Play modundayken çalıştırılamaz.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("SampleScene.unity"))
                scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.name == "Gameplay Canvas");
            if (canvas == null) { Debug.LogError("[DemoEnd] 'Gameplay Canvas' bulunamadı."); return; }

            LoadAssets();
            Core.Localization.Loc.Init();

            var old = canvas.transform.Find("DemoEndPanel");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var panel = BuildPanel(canvas.transform);

            // Pause menüsünün de üstünde kalsın — demo bittiyse duraklatma anlamsız.
            panel.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                "[DemoEnd] Panel üretildi. Wishlist ve Öneri butonlarının URL'lerini " +
                "DemoEndPanel > WishlistButton / FeedbackButton üzerindeki " +
                "ExternalLinkButton bileşenine girin — boşken gizli kalırlar.");
        }

        // ── Panel ────────────────────────────────────────────────────────────

        static RectTransform BuildPanel(Transform parent)
        {
            // Kök: tam ekran karartma. DemoEndPanel bileşeni BURADA durur ve
            // kapattığı nesne _root çocuğudur — kendi Update'ini kaybetmesin.
            var root = NewRect("DemoEndPanel", parent);
            Stretch(root);

            var controller = root.gameObject.AddComponent<DemoEndPanel>();
            var group      = root.gameObject.AddComponent<CanvasGroup>();

            var content = NewRect("Content", root);
            Stretch(content);
            AddImage(content, Dimmer);

            // ── Pencere ──────────────────────────────────────────────
            var window = NewRect("Window", content);
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
            window.pivot     = new Vector2(0.5f, 0.5f);
            window.sizeDelta = new Vector2(WindowW, WindowH);
            AddImage(window, WindowEdge, _round);

            var inner = NewRect("Inner", window);
            Stretch(inner, 4, 4, 4, 4);
            AddImage(inner, WindowBg, _round);

            // ── Başlık ───────────────────────────────────────────────
            var title = LocText("Title", inner, "DemoEnd.Title", 54f, TitleColor, true,
                                TextAlignmentOptions.Center);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot     = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(48f, 0f);
            titleRect.offsetMax = new Vector2(-48f, -56f);
            titleRect.sizeDelta = new Vector2(titleRect.sizeDelta.x, 76f);

            // ── Gövde metni ──────────────────────────────────────────
            var body = LocText("Body", inner, "DemoEnd.Body", 30f, BodyColor, false,
                               TextAlignmentOptions.Top);
            // Uzun metin — sarma AÇIK olmalı (LocText varsayılanı kapalı).
            body.enableWordWrapping = true;
            body.lineSpacing        = 8f;

            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(64f, 190f);
            bodyRect.offsetMax = new Vector2(-64f, -160f);

            // ── Buton sırası ─────────────────────────────────────────
            var row = NewRect("Buttons", inner);
            row.anchorMin = new Vector2(0.5f, 0f);
            row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot     = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, 56f);
            row.sizeDelta = new Vector2(BtnW * 3f + BtnGap * 2f, BtnH);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing              = BtnGap;
            layout.childAlignment       = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth      = false;
            layout.childControlHeight     = false;

            var menuBtn     = MenuButton(row, "MainMenuButton", "DemoEnd.MainMenu", BtnNeutral);
            var wishlistBtn = MenuButton(row, "WishlistButton", "DemoEnd.Wishlist", BtnWishlist,
                                         new Color32(0x1A, 0x14, 0x06, 0xFF));
            var feedbackBtn = MenuButton(row, "FeedbackButton", "DemoEnd.Feedback", BtnFeedback,
                                         new Color32(0x06, 0x16, 0x1E, 0xFF));

            // Link butonları tıklamayı kendileri yapar.
            AttachLink(wishlistBtn);
            AttachLink(feedbackBtn);

            // ── Referansları bağla ───────────────────────────────────
            var so = new SerializedObject(controller);
            so.FindProperty("_root").objectReferenceValue           = content.gameObject;
            so.FindProperty("_canvasGroup").objectReferenceValue    = group;
            so.FindProperty("_mainMenuButton").objectReferenceValue = menuBtn;
            so.FindProperty("_wishlistButton").objectReferenceValue = wishlistBtn;
            so.FindProperty("_feedbackButton").objectReferenceValue = feedbackBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            content.gameObject.SetActive(false);
            return root;
        }

        /// <summary>
        /// Butona <see cref="ExternalLinkButton"/> ekler. onClick'i BURADA
        /// bağlamayız: bileşen kendi <c>Awake</c>'inde bağlanıyor, ayrıca
        /// editörde eklenen listener serileşmediği için etkisiz kalırdı —
        /// iki kez bağlanma riski de böylece doğmaz.
        ///
        /// URL boş bırakılır; adresler sonra Inspector'dan girilecek.
        /// </summary>
        static void AttachLink(Button button)
        {
            button.gameObject.AddComponent<ExternalLinkButton>();
        }

        // ── Yardımcılar (PauseMenuBuilder ile aynı desen) ────────────────────

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

        static Button MenuButton(Transform parent, string name, string locKey, Color bg,
                                 Color? labelColor = null)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(BtnW, BtnH);
            AddImage(rt, bg, _round);

            var btn = rt.gameObject.AddComponent<Button>();

            var label = LocText("Label", rt, locKey, 28f,
                                labelColor ?? Color.white, true, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 12, 12, 6, 6);

            return btn;
        }
    }
}
