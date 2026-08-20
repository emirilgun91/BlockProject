using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using RogueBlockBlast.UI;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// Oyun içi kart envanterinin yerleşimini düzeltir.
    /// Menü: Tools ▸ RogueBlockBlast ▸ Card Inventory ▸ Fix Layout
    ///
    /// Yaptıkları:
    ///  • Paneli 3 sütunluk okunur bir ızgaraya genişletir (20+ kart sığar, taşarsa kaydırılır)
    ///  • CardSlot prefabını temizden kurar — iç içe Canvas'lar ve bozuk RarityBorder
    ///    ölçüleri (-1820 x -980) temizlenir
    ///  • İkon üst kareye, canlı değer yazısı alt şeride oturur; artık üst üste binmezler
    /// </summary>
    public static class CardInventoryLayoutTool
    {
        const float SlotSize   = 68f;   // Cell X
        const float PadLeft    = 12f;
        const float PadRight   = 6f;
        const float PadTop     = 45f;   // üstteki başlık şeridi için pay
        const float PadBottom  = 8f;
        const float Spacing    = 8f;
        const int   Columns    = 3;
        const float BandHeight = 22f;    // CardSlotView.LiveValueBandHeight ile aynı olmalı

        /// <summary>Panelin sol kenardan uzaklığı (px). Tahtaya yaklaştırmak için artır.</summary>
        const float PanelLeftMargin = 258f;

        static readonly Color SlotBg  = new Color32(0x18, 0x1B, 0x2E, 0xFF);
        static readonly Color PanelBg = new Color32(0x0E, 0x11, 0x20, 0xE6);
        static readonly Color BadgeBg = new Color32(0x10, 0x10, 0x1E, 0xF2);

        [MenuItem("Tools/RogueBlockBlast/Card Inventory/Fix Layout", priority = 80)]
        public static void Fix()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Inventory] Play modundayken çalıştırılamaz.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("SampleScene.unity"))
                scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

            var inv = Object.FindFirstObjectByType<CardInventoryUI>(FindObjectsInactive.Include);
            if (inv == null) { Debug.LogError("[Inventory] CardInventoryUI bulunamadı."); return; }

            FixPanel(inv);
            FixSlotPrefab(inv);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Inventory] Yerleşim düzeltildi — panel genişletildi, slot prefabı yeniden kuruldu.");
        }

        // ── Panel ────────────────────────────────────────────────────────────
        static void FixPanel(CardInventoryUI inv)
        {
            var root = (RectTransform)inv.transform;

            float width = Columns * SlotSize + (Columns - 1) * Spacing + PadLeft + PadRight + 8f;
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot     = new Vector2(0f, 0.5f);
            root.sizeDelta = new Vector2(width, -140f);      // üstten/alttan 70'er boşluk
            root.anchoredPosition = new Vector2(PanelLeftMargin, 0f);

            var bg = root.GetComponent<Image>();
            if (bg != null) bg.color = PanelBg;

            var scroll   = root.GetComponent<ScrollRect>();
            var viewport = root.Find("Viewport") as RectTransform;
            var content  = viewport != null ? viewport.Find("Content") as RectTransform : null;
            if (scroll == null || viewport == null || content == null)
            {
                Debug.LogWarning("[Inventory] ScrollRect / Viewport / Content bulunamadı.");
                return;
            }

            Stretch(viewport, 4, 4, 4, 4);

            // Viewport'ta klasik Mask vardı: maskeyi Image'in ALPHA'sından türetiyor,
            // bu yüzden şeffaf zeminle birlikte tüm ikonlar kırpılıp görünmez oluyordu.
            // RectMask2D dikdörtgen sınırla kırpar, grafik alfasına bakmaz.
            foreach (var m in viewport.GetComponents<Mask>()) Object.DestroyImmediate(m);
            if (viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();

            var vpImg = viewport.GetComponent<Image>();
            if (vpImg != null) vpImg.color = new Color(1f, 1f, 1f, 0.01f);   // sadece raycast zemini

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot     = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, content.offsetMin.y);
            content.offsetMax = new Vector2(0f, 0f);

            scroll.horizontal        = false;
            scroll.vertical          = true;
            scroll.movementType      = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport          = viewport;
            scroll.content           = content;

            var sb = root.Find("Scrollbar Vertical") as RectTransform;
            if (sb != null)
            {
                sb.anchorMin = new Vector2(1f, 0f);
                sb.anchorMax = new Vector2(1f, 1f);
                sb.pivot     = new Vector2(1f, 0.5f);
                sb.sizeDelta = new Vector2(8f, 0f);
                sb.anchoredPosition = Vector2.zero;
                scroll.verticalScrollbar = sb.GetComponent<Scrollbar>();
                scroll.verticalScrollbarVisibility =
                    ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            }

            var grid = content.GetComponent<GridLayoutGroup>()
                       ?? content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize        = new Vector2(SlotSize, SlotSize + BandHeight);
            grid.spacing         = new Vector2(Spacing, Spacing);
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            grid.padding         = new RectOffset((int)PadLeft, (int)PadRight, (int)PadTop, (int)PadBottom);
            grid.childAlignment  = TextAnchor.UpperCenter;

            var csf = content.GetComponent<ContentSizeFitter>()
                      ?? content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var so = new SerializedObject(inv);
            so.FindProperty("_columns").intValue       = Columns;
            so.FindProperty("_slotSize").floatValue    = SlotSize;
            so.FindProperty("_slotSpacing").floatValue = Spacing;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(inv);
        }

        // ── Slot prefab ──────────────────────────────────────────────────────
        static void FixSlotPrefab(CardInventoryUI inv)
        {
            var so = new SerializedObject(inv);
            var slot = so.FindProperty("_slotPrefab").objectReferenceValue as CardSlotView;
            if (slot == null) { Debug.LogWarning("[Inventory] Slot prefabı atanmamış."); return; }

            string path = AssetDatabase.GetAssetPath(slot);
            if (string.IsNullOrEmpty(path)) { Debug.LogWarning("[Inventory] Slot prefab yolu yok."); return; }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rt = root.GetComponent<RectTransform>();
                if (rt == null) rt = root.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(SlotSize, SlotSize + BandHeight);

                // Eski içerik tamamen atılır — iç içe Canvas'lar ve bozuk ölçüler orada
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                foreach (var c in root.GetComponents<Canvas>())            Object.DestroyImmediate(c);
                foreach (var c in root.GetComponents<CanvasScaler>())      Object.DestroyImmediate(c);
                foreach (var c in root.GetComponents<GraphicRaycaster>())  Object.DestroyImmediate(c);

                var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

                // Kök zemin — hover algılansın diye raycast hedefi
                var bg = root.GetComponent<Image>();
                if (bg == null) bg = root.AddComponent<Image>();
                bg.sprite = sprite; bg.type = Image.Type.Sliced;
                bg.color = SlotBg; bg.raycastTarget = true;

                // Rarity çerçevesi — ikonun arkasında, üst kareyi kaplar
                var border = NewChild("RarityBorder", rt);
                AnchorTop(border, SlotSize, 0f);
                var borderImg = border.gameObject.AddComponent<Image>();
                borderImg.sprite = sprite; borderImg.type = Image.Type.Sliced;
                borderImg.raycastTarget = false;

                // İkon — üst karenin içinde, kenarlardan pay bırakır
                var icon = NewChild("IconImage", rt);
                AnchorTop(icon, SlotSize, 7f);
                var iconImg = icon.gameObject.AddComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget  = false;

                // Stack rozeti — sağ üst köşe
                var badge = NewChild("StackBadge", rt);
                badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f);
                badge.pivot     = new Vector2(1f, 1f);
                badge.sizeDelta = new Vector2(26f, 20f);
                badge.anchoredPosition = new Vector2(-2f, -2f);
                var badgeImg = badge.gameObject.AddComponent<Image>();
                badgeImg.sprite = sprite; badgeImg.type = Image.Type.Sliced;
                badgeImg.color = BadgeBg; badgeImg.raycastTarget = false;

                var stackText = NewChild("StackText", badge);
                Stretch(stackText, 2, 2, 1, 1);
                var stackTmp = stackText.gameObject.AddComponent<TextMeshProUGUI>();
                stackTmp.text      = "x2";
                stackTmp.fontSize  = 13f;
                stackTmp.fontStyle = FontStyles.Bold;
                stackTmp.alignment = TextAlignmentOptions.Center;
                stackTmp.color     = Color.white;
                stackTmp.raycastTarget = false;

                // Canlı değer — alt şerit, ikonla çakışmaz
                var live = NewChild("LiveValueText", rt);
                live.anchorMin = new Vector2(0f, 0f);
                live.anchorMax = new Vector2(1f, 0f);
                live.pivot     = new Vector2(0.5f, 0f);
                live.offsetMin = new Vector2(2f, 2f);
                live.offsetMax = new Vector2(-2f, BandHeight);
                var liveTmp = live.gameObject.AddComponent<TextMeshProUGUI>();
                liveTmp.fontSize  = 15f;
                liveTmp.fontStyle = FontStyles.Bold;
                liveTmp.alignment = TextAlignmentOptions.Center;
                liveTmp.color     = new Color(1f, 0.86f, 0.35f);
                liveTmp.raycastTarget = false;
                liveTmp.textWrappingMode = TextWrappingModes.NoWrap;
                liveTmp.overflowMode     = TextOverflowModes.Ellipsis;
                liveTmp.enableAutoSizing = true;
                liveTmp.fontSizeMin = 9f;
                liveTmp.fontSizeMax = 15f;

                var view = root.GetComponent<CardSlotView>();
                if (view == null) view = root.AddComponent<CardSlotView>();
                var vso = new SerializedObject(view);
                vso.FindProperty("_iconImage").objectReferenceValue     = iconImg;
                vso.FindProperty("_rarityBorder").objectReferenceValue  = borderImg;
                vso.FindProperty("_stackBadge").objectReferenceValue    = badge.gameObject;
                vso.FindProperty("_stackText").objectReferenceValue     = stackTmp;
                vso.FindProperty("_liveValueText").objectReferenceValue = liveTmp;
                vso.ApplyModifiedPropertiesWithoutUndo();

                badge.gameObject.SetActive(false);   // yalnızca stack > 1 iken açılır

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────
        static RectTransform NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        /// <summary>Hücrenin üst kare bölgesine oturt — alt şerit canlı değer için ayrılır.</summary>
        static void AnchorTop(RectTransform rt, float size, float inset)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(inset, -(size - inset));
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
