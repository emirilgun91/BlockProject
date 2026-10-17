using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using RogueBlockBlast.UI;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// HUD dışındaki ekranları UI kitinin görsel diline çevirir: Game Over, kart
    /// seçim paneli, Shape Shop (prefab) ve ana menü.
    ///
    /// HudStyleBuilder gibi yerinde boyar: objeler, isimler, bileşenler ve
    /// referanslar kalır; değişen sprite, renk ve yazı stili. Skin görselleri
    /// (Assets/Arts/UI/Skin) yoksa kodla çizilen kit sprite'larına düşer.
    /// Tekrar çalıştırılabilir.
    ///
    /// Menü: Tools ▸ RogueBlockBlast ▸ UI Kit ▸ Restyle Screens ▸ …
    /// </summary>
    public static class ScreenStyleBuilder
    {
        // Değiştirilen her şey buraya; sahnedeki prefab örneklerinde kayıt için.
        static readonly List<Component> _touched = new List<Component>();

        // ── Game Over + kart seçimi (oyun sahnesi) ───────────────────────────

        [MenuItem("Tools/RogueBlockBlast/UI Kit/Restyle Screens/Game Over + Card Screen (Game Scene)", priority = 58)]
        public static void RestyleGameScreens()
        {
            if (!EnsureScene("Assets/Scenes/SampleScene.unity", out var scene)) return;
            var canvas = Canvas("Gameplay Canvas");
            if (canvas == null) return;
            _touched.Clear();

            GameOver(canvas);
            CardScreen(canvas);
            DemoEnd(canvas);

            Commit(scene);
            Debug.Log("[ScreenStyle] Game Over ve kart seçim ekranı UI kitine çevrildi.");
        }

        static void GameOver(Transform canvas)
        {
            var root = canvas.Find("GameOverUI");
            if (root == null) return;

            Tint(Find(root, "Backdrop"), UiTheme.WithAlpha(UiTheme.Scrim, 0.94f));
            Frame(Find(root, "Panel"), "Skin_Window");

            var center = Find(root, "Panel/CenterSection");
            Txt(Find(center, "RunEnded_Txt"), 30, UiTheme.TextDim, spacing: 8);
            Txt(Find(center, "GameOver_Txt"), 68, UiTheme.Danger, spacing: 4);

            foreach (var box in new[] { "ScoreRow/FinalScoreBx", "ScoreRow/BestScoreBox" })
            {
                var b = Find(center, box);
                Frame(b, "Skin_Board");
                foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.name.EndsWith("Label")) Txt(t.rectTransform, 18, UiTheme.TextDim, spacing: 4);
                    else if (t.name.EndsWith("ScoreText"))
                        Txt(t.rectTransform, t.fontSize, t.name.StartsWith("Final") ? UiTheme.Warm : UiTheme.Text);
                }
            }
            var badge = Find(center, "ScoreRow/FinalScoreBx/NewRecordBadge/NewRecordText");
            Txt(badge, 15, UiTheme.Success, spacing: 2);

            foreach (Transform sb in Find(center, "StatsRow"))
            {
                Frame((RectTransform)sb, "Skin_Board");
                Txt(Find(sb, "StatLabel"), 12, UiTheme.TextDim, spacing: 2);
                Txt(Find(sb, "StatValue"), 22, UiTheme.Text);
            }

            var bottom = Find(center, "BottomSection");
            Btn(Find(bottom, "RetryButton"),    UiKit.Style.Primary,   Color.white);
            Btn(Find(bottom, "UpgradeButton"),  UiKit.Style.Secondary, UiTheme.Warm);   // dikkat çeksin
            Btn(Find(bottom, "MainMenuButton"), UiKit.Style.Secondary, UiTheme.Text);
            // 42px yükseklik buton görselinde ince bir şerit gibi kalıyordu
            foreach (Transform b in bottom)
            {
                var r = (RectTransform)b;
                r.sizeDelta = new Vector2(Mathf.Max(r.sizeDelta.x, 210f), 56f);
                Mark(r);
            }

            // "GAME OVER" duyurusunun arkasındaki karartma
            var announce = canvas.Find("GameOver_Txt");
            if (announce != null) Tint(Find(announce, "Backdrop (1)"), UiTheme.WithAlpha(UiTheme.Scrim, 0.96f));
        }

        /// <summary>
        /// Demo bitiş paneli (DemoEndPanelBuilder kurar). Dış "Window" eski kenar
        /// rengini taşıyordu; görünmez yapılır, çerçeveyi "Inner" pencere görseliyle çizer.
        /// İstek listesi birincil buton — demonun asıl çağrısı o.
        /// </summary>
        static void DemoEnd(Transform canvas)
        {
            var root = canvas.Find("DemoEndPanel");
            if (root == null) return;

            var content = Find(root, "Content");
            Tint(content, UiTheme.WithAlpha(UiTheme.Scrim, 0.94f));
            Tint(Find(content, "Window"), Color.clear);
            var inner = Find(content, "Window/Inner");
            Frame(inner, "Skin_Window");

            Txt(Find(inner, "Title"), 46, UiTheme.Text, spacing: 4);
            Txt(Find(inner, "Body"), 30, UiTheme.WithAlpha(UiTheme.Text, 0.85f), bold: false);

            var row = Find(inner, "Buttons");
            Btn(Find(row, "MainMenuButton"), UiKit.Style.Secondary, UiTheme.Text);
            Btn(Find(row, "WishlistButton"), UiKit.Style.Primary,   Color.white);
            Btn(Find(row, "FeedbackButton"), UiKit.Style.Secondary, UiTheme.Text);

            // Satır, genişliği Image'ın tercih boyundan alıyor; sliced buton görselinin
            // tercih boyu yalnızca köşe payı kadar — butonlar ince bir şeride
            // dönüşüyordu. Boyut sabitlenir.
            foreach (Transform b in row)
            {
                var le = b.GetComponent<LayoutElement>() ?? b.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 260; le.preferredHeight = 72;
                le.minWidth = 260; le.minHeight = 72;
                Mark(le);
            }
        }

        static void CardScreen(Transform canvas)
        {
            var root = canvas.Find("CardScreen");
            if (root == null) return;

            Tint(Find(root, "Backdrop"), UiTheme.WithAlpha(UiTheme.Scrim, 0.86f));
            var panel = Find(root, "Panel");
            Frame(panel, "Skin_Window");
            var pImg = panel.GetComponent<Image>();
            pImg.color = UiTheme.WithAlpha(Color.white, 0.9f);
            Mark(pImg);

            Txt(Find(panel, "BannerText"),  30, UiTheme.Warm,   spacing: 4);
            Txt(Find(panel, "ChooseLabel"), 24, UiTheme.Accent, spacing: 6);

            var reroll = Find(root, "RerollButton");
            if (reroll != null)
            {
                Tint(reroll, UiTheme.Text);
                Txt(Find(reroll, "Text (TMP)"), 16, UiTheme.Warm);
            }
        }

        // ── Shape Shop (prefab — ana menü ve oyun sahnesi aynı prefabı kullanıyor) ──

        [MenuItem("Tools/RogueBlockBlast/UI Kit/Restyle Screens/Shape Shop (Prefabs)", priority = 59)]
        public static void RestyleShapeShop()
        {
            _touched.Clear();
            Edit("Assets/Prefabs/ShapeShopPanel.prefab", ShopPanel);
            Edit("Assets/Prefabs/ShapeCard.prefab", ShopCard);
            Debug.Log("[ScreenStyle] Shape Shop prefabları UI kitine çevrildi.");
        }

        static void ShopPanel(GameObject root)
        {
            var rt = (RectTransform)root.transform;
            Tint(rt, UiTheme.Sunken);   // tam opak: arkadaki menü görünmesin

            var header = Find(rt, "Header");
            Frame(header, "Skin_Board");
            SmallButton(Find(header, "BackButton"), UiKit.Style.Secondary, "<", 40);
            Txt(Find(header, "TitleBlock/TitleText"), 40, UiTheme.Text, spacing: 6);

            var coin = Find(header, "CoinDisplay");
            Frame(coin, "Skin_Board");
            Txt(Find(coin, "CoinBlock/CoinText"), 26, UiTheme.Warm);

            // Izgara deseni yerine düz, hafif bir zemin; kartlar öne çıksın
            var scroll = Find(rt, "Scroll View");
            var sImg = scroll.GetComponent<Image>();
            sImg.sprite = null; sImg.color = Color.clear;
            Mark(sImg);
            foreach (var bar in scroll.GetComponentsInChildren<Scrollbar>(true))
            {
                var bg = bar.GetComponent<Image>();
                if (bg != null) { bg.sprite = UiKit.Pill; bg.type = Image.Type.Sliced; bg.color = UiTheme.WithAlpha(UiTheme.Sunken, 0.8f); Mark(bg); }
                var h = bar.handleRect != null ? bar.handleRect.GetComponent<Image>() : null;
                if (h != null) { h.sprite = UiKit.Pill; h.type = Image.Type.Sliced; h.color = UiTheme.RimSoft; Mark(h); }
            }
        }

        static void ShopCard(GameObject root)
        {
            var rt = (RectTransform)root.transform;
            // Pano görseli: pencere görselinin köşe süsleri 280px kartta şekil adına çarpıyordu
            Frame(rt, "Skin_Board");
            // Şekil rengindeki başlık alanı: yuvarlak köşe, daha koyu ton (rengi
            // ShapeShopCardView çalışma anında verir)
            var bg = Find(rt, "ShapeBackground");
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = UiKit.Fill(UiTheme.RadiusControl); bgImg.type = Image.Type.Sliced;
            Mark(bgImg);

            Txt(Find(rt, "ShapeName"), 20, UiTheme.TextDim, spacing: 4);
            Tint(Find(rt, "Image (1)"), UiTheme.RimSoft);   // ayraç

            foreach (var block in new[] { "StatsPanel/ScoreBlock", "StatsPanel/ScoreBlock/WeightBlock", "StatsPanel/WeightBlock" })
            {
                var b = Find(rt, block);
                if (b == null) continue;
                var img = b.GetComponent<Image>();
                img.sprite = UiKit.Fill(UiTheme.RadiusControl); img.type = Image.Type.Sliced;
                img.color = UiTheme.WithAlpha(UiTheme.Raised, 0.55f);
                Mark(img);
            }

            foreach (var t in rt.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name.EndsWith("Label")) Txt(t.rectTransform, t.fontSize, UiTheme.TextDim, spacing: 2);
                else if (t.name.EndsWith("Value")) Txt(t.rectTransform, t.fontSize, UiTheme.Text);
            }

            // Maliyet çipleri: koyu hap
            foreach (var img in rt.GetComponentsInChildren<Image>(true))
                if (img.sprite != null && img.sprite.name.StartsWith("cost_chip_bg"))
                {
                    img.sprite = UiKit.Pill; img.type = Image.Type.Sliced; img.color = UiTheme.Sunken;
                    Mark(img);
                }

            SmallButton(Find(rt, "StatsPanel/ScoreBlock/ScoreRow/ScoreUpgradeBtn"), UiKit.Style.Secondary, "+", 26);
            SmallButton(Find(rt, "StatsPanel/ScoreBlock/WeightBlock/WeightBtnRow/WeightUpBtn"), UiKit.Style.Secondary, "+", 26);
            SmallButton(Find(rt, "StatsPanel/ScoreBlock/WeightBlock/WeightBtnRow/WeightDownBtn"), UiKit.Style.Danger, "-", 26);

            var track = Find(rt, "StatsPanel/ScoreBlock/WeightBlock/WeightBar");
            if (track != null)
            {
                Tint(track, UiTheme.Sunken);
                Tint(Find(track, "WeightBarFill"), UiTheme.Accent);
            }

            var unlock = Find(rt, "UnlockSection/UnlockBtn");
            if (unlock != null)
            {
                Btn(unlock, UiKit.Style.Primary, Color.white);
                Txt(Find(unlock, "UnlockBtnContent/LockText"), 25, Color.white, spacing: 4);
            }

            var view = root.GetComponent<RogueBlockBlast.UI.ShapeShopCardView>();
            if (view != null)
            {
                var so = new SerializedObject(view);
                void C(string f, Color c) { var p = so.FindProperty(f); if (p != null) p.colorValue = c; }
                C("_dotUnlockedColor", UiTheme.Success);
                C("_dotLockedColor", UiTheme.TextDim);
                C("_affordableColor", UiTheme.Warm);
                C("_unaffordableColor", UiTheme.Danger);
                var d = so.FindProperty("_backgroundDarken"); if (d != null) d.floatValue = 0.7f;   // daha sakin başlık
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ── Ana menü ─────────────────────────────────────────────────────────

        [MenuItem("Tools/RogueBlockBlast/UI Kit/Restyle Screens/Main Menu", priority = 60)]
        public static void RestyleMainMenu()
        {
            if (!EnsureScene("Assets/Scenes/MainMenu.unity", out var scene)) return;
            var canvas = Canvas("MainMenuCanvas");
            if (canvas == null) return;
            _touched.Clear();

            // Zemin: mor resim yerine oyun sahnesiyle aynı koyu lacivert
            var bg = Find(canvas, "BG");
            if (bg != null)
            {
                var img = bg.GetComponent<Image>();
                img.sprite = MenuBackground(); img.type = Image.Type.Simple; img.color = Color.white;
                Mark(img);
            }

            MenuButtons(Find(canvas, "LeftPanel/MenuList"));

            var social = Find(canvas, "LeftPanel/SocialStrip");
            if (social != null)
                foreach (Transform s in social)
                    Btn((RectTransform)s, s.name == "Steam" ? UiKit.Style.Primary : UiKit.Style.Secondary,
                        s.name == "Steam" ? Color.white : UiTheme.Text);

            LastRun(Find(canvas, "RightPanel/PreviewStage/LastRunPanel"));
            // Önizleme tahtasının zemini kaymış duruyordu (hücrelerin sol üstünde yarım
            // koyu bir dikdörtgen); hücreler kendi başına yeterli.
            Tint(Find(canvas, "RightPanel/PreviewStage/PreviewBoard/BG"), Color.clear);
            Upgrades(Find(canvas, "UpgradePanel"));

            Commit(scene);
            Edit("Assets/Prefabs/UpgradeSlot.prefab", UpgradeSlot);
            Debug.Log("[ScreenStyle] Ana menü UI kitine çevrildi.");
        }

        static readonly (string button, string icon, UiKit.Style style)[] MenuItems =
        {
            ("BtnNewRun",    "Icon_NewRun",    UiKit.Style.Primary),
            ("BtnShapeShop", "Icon_ShapeShop", UiKit.Style.Secondary),
            ("BtnUpgrades",  "Icon_Upgrades",  UiKit.Style.Secondary),
            ("BtnSettings",  "Icon_Settings",  UiKit.Style.Secondary),
            ("BtnQuit",      "Icon_Quit",      UiKit.Style.Secondary),
        };

        /// <summary>
        /// Menü butonları: eski görselin yerine kit butonu; görsele gömülü ikon
        /// (Skin/Icon_*.png, eski görsellerden ayrıldı) ayrı bir Image olarak
        /// solda durur. Her butonun kendi rengi ikonda ve sol vurgu çubuğunda kalır.
        /// </summary>
        static void MenuButtons(RectTransform list)
        {
            if (list == null) return;
            foreach (var (name, icon, style) in MenuItems)
            {
                var b = Find(list, name);
                var art = Find(b, "Image");
                if (art == null) continue;

                var img = art.GetComponent<Image>();
                var skin = UiKit.SkinFor(style);
                img.sprite = skin ?? UiKit.Fill(UiTheme.RadiusControl);
                img.type = Image.Type.Sliced; img.preserveAspect = false;
                img.color = skin != null ? Color.white : UiTheme.Raised;
                Mark(img);

                var old = art.Find("Icon");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiKit.SkinFolder}/{icon}.png");
                if (sprite != null)
                {
                    var ic = UiKit.NewRect("Icon", art);
                    ic.anchorMin = ic.anchorMax = new Vector2(0, 0.5f);
                    ic.pivot = new Vector2(0, 0.5f);
                    ic.sizeDelta = new Vector2(44, 44);
                    ic.anchoredPosition = new Vector2(30, 0);

                    // Yazı layout'la diziliyor; ikon layout dışında olduğu için yazının
                    // başlangıcı ikonun sağına alınır
                    var hlg = art.GetComponent<HorizontalLayoutGroup>();
                    if (hlg != null)
                    {
                        hlg.padding = new RectOffset(92, hlg.padding.right, hlg.padding.top, hlg.padding.bottom);
                        Mark(hlg);
                    }
                    var ii = ic.gameObject.AddComponent<Image>();
                    ii.sprite = sprite; ii.preserveAspect = true; ii.raycastTarget = false;
                    // Buton görselinde HorizontalLayoutGroup var; ikon diziye girerse
                    // yazıyı ve coin rozetini sağa itiyor (rozet butondan taşıyordu)
                    ic.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                    ic.SetSiblingIndex(1);   // vurgu çubuğunun önünde, yazının arkasında
                }

                foreach (var t in art.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.name == "CoinText") { t.color = UiTheme.Warm; Mark(t); continue; }
                    t.font = UiKit.Bold; t.color = style == UiKit.Style.Primary ? Color.white : UiTheme.Text;
                    t.characterSpacing = 4f;
                    Mark(t);
                }
                var coinBadge = Find(art, "Header");
                if (coinBadge != null)
                {
                    var ci = coinBadge.GetComponent<Image>();
                    ci.sprite = UiKit.Pill; ci.type = Image.Type.Sliced; ci.color = UiTheme.WithAlpha(UiTheme.Sunken, 0.9f);
                    Mark(ci);
                    // Butonun sağ kenarından taşıyordu: içeride, sağa yaslı
                    coinBadge.anchorMin = coinBadge.anchorMax = new Vector2(1, 0.5f);
                    coinBadge.pivot = new Vector2(1, 0.5f);
                    coinBadge.anchoredPosition = new Vector2(-16, 0);
                    coinBadge.sizeDelta = new Vector2(128, 44);
                    var le = coinBadge.GetComponent<LayoutElement>() ?? coinBadge.gameObject.AddComponent<LayoutElement>();
                    le.ignoreLayout = true;   // konumu layout değil yukarıdaki çapa belirlesin
                    Mark(le);
                    Mark(coinBadge);
                }

                // Hover'da açılan sol vurgu çubuğu: yeni buton görselinin yanında
                // anlamsız bir çizgi gibi kalıyordu. Görünmez; içindeki ok kalır.
                var accent = Find(art, "AccentBar");
                if (accent != null) Tint(accent, Color.clear);

                var view = b.GetComponent<MenuButtonView>();
                if (view != null)
                {
                    var so = new SerializedObject(view);
                    void C(string f, Color c) { var p = so.FindProperty(f); if (p != null) p.colorValue = c; }
                    C("_borderNormal",     new Color(0.86f, 0.86f, 0.86f, 1f));   // görselin parlaklığı
                    C("_borderHighlight",  Color.white);
                    C("_arrowNormalColor", UiTheme.TextDim);
                    C("_arrowHoverColor",  UiTheme.Accent);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Mark(view);
                }
            }
        }

        static void LastRun(RectTransform panel)
        {
            if (panel == null) return;
            Frame(panel, "Skin_Window");

            foreach (var img in panel.GetComponentsInChildren<Image>(true))
            {
                // Frame() eski "Rim" çocuklarını siliyor; listede onlar sonra gelebilir
                if (img == null || img.transform == panel || img.sprite == null) continue;
                var n = img.sprite.name;
                if (n.StartsWith("stat_tile_bg") || n.StartsWith("Skin_Board"))
                {
                    // Kutular sayı yazısının ÇOCUĞU olarak kurulmuş: eski görselin ortası
                    // saydamdı, pano görseli opak — yazının üstüne çizip sayıyı gizliyordu.
                    // Yazının kardeşi yapılıp hemen önüne (arkasında çizilecek) alınır.
                    var tile = img.rectTransform;
                    var text = tile.parent;
                    if (text != null && text.GetComponent<TMP_Text>() != null)
                    {
                        tile.SetParent(text.parent, worldPositionStays: true);
                        var le = tile.GetComponent<LayoutElement>() ?? tile.gameObject.AddComponent<LayoutElement>();
                        le.ignoreLayout = true;
                    }
                    // Grubun en arkasına: yazının da yanındaki ikonun (şimşek, coin) da altında
                    tile.SetAsFirstSibling();
                    Mark(tile);
                    Frame(tile, "Skin_Board");
                }
                else if (n.StartsWith("panel_ambient_glow")) { img.color = Color.clear; Mark(img); }
                else if (n.StartsWith("divider")) { img.color = UiTheme.RimSoft; Mark(img); }
                else if (n.StartsWith("countdown_capsule"))
                {
                    img.sprite = UiKit.Pill; img.type = Image.Type.Sliced; img.color = UiTheme.WithAlpha(UiTheme.Sunken, 0.9f);
                    Mark(img);
                }
            }

            Txt(Find(panel, "TitleText"), 30, UiTheme.TextDim, spacing: 8);
            Txt(Find(panel, "ScoreValue"), 69, UiTheme.Warm);
            foreach (var t in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name.EndsWith("Label")) Txt(t.rectTransform, t.fontSize, UiTheme.TextDim, spacing: 2);
                else if (t.name is "ComboValue" or "CoinsValue") Txt(t.rectTransform, t.fontSize, UiTheme.Text);
                else if (t.name == "CountdownText") Txt(t.rectTransform, t.fontSize, UiTheme.TextDim, bold: false);
            }
        }

        static void Upgrades(RectTransform root)
        {
            if (root == null) return;
            Tint(root, UiTheme.WithAlpha(UiTheme.Scrim, 0.94f));

            var win = Find(root, "Window");
            Frame(win, "Skin_Window");
            Tint(Find(win, "Image"), Color.clear);   // eski kenar parıltısı — görselde var

            var bar = Find(win, "TitleBar");
            Frame(bar, "Skin_Board");
            SmallButton(Find(bar, "LeftGroup/BackButton"), UiKit.Style.Secondary, "<", 40);
            Txt(Find(bar, "LeftGroup/TitleText"), 40, UiTheme.Text, spacing: 6);
            var coin = Find(bar, "LeftGroup/CoinBadge");
            Frame(coin, "Skin_Board");
            Txt(Find(coin, "CoinAmount"), 34, UiTheme.Warm);
            Tint(Find(win, "Seperator"), UiTheme.RimSoft);

            var detail = Find(win, "DetailPanel");
            Frame(detail, "Skin_Board");
            Txt(Find(detail, "DetailText/DetailName"), 33, UiTheme.Text, spacing: 2);
            Txt(Find(detail, "DetailText/DetailDesc"), 26, UiTheme.Accent, bold: false);
            Frame(Find(detail, "DetailActions/PriceRow/Image"), "Skin_Board");
            Btn(Find(detail, "DetailActions/ButtonRow/BuyButton"),  UiKit.Style.Primary,   Color.white);
            Btn(Find(detail, "DetailActions/ButtonRow/SellButton"), UiKit.Style.Secondary, UiTheme.Text);

            var ctrl = root.GetComponent<UpgradesController>();
            if (ctrl != null)
            {
                var so = new SerializedObject(ctrl);
                var a = so.FindProperty("_affordableColor");   if (a != null) a.colorValue = UiTheme.Warm;
                var u = so.FindProperty("_unaffordableColor"); if (u != null) u.colorValue = UiTheme.Danger;
                so.ApplyModifiedPropertiesWithoutUndo();
                Mark(ctrl);
            }
        }

        /// <summary>
        /// Upgrade slotu: pano görseli; UpgradeSlotView zemini duruma göre tint'liyor,
        /// renkler o yüzden "görseli ne kadar aydınlat / hangi tona çek" diye seçildi.
        /// </summary>
        static void UpgradeSlot(GameObject root)
        {
            var rt = (RectTransform)root.transform;
            Frame(rt, "Skin_Board");

            foreach (var img in rt.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite == null) continue;
                var n = img.sprite.name;
                if (n.StartsWith("dot_")) { img.sprite = UiKit.Pill; img.type = Image.Type.Simple; img.preserveAspect = true; }
                else if (n.StartsWith("button_sells")) { img.sprite = UiKit.Pill; img.type = Image.Type.Sliced; img.color = UiTheme.Success; }
                else if (n.StartsWith("Button_buys")) { img.sprite = UiKit.Pill; img.type = Image.Type.Sliced; img.color = UiTheme.Sunken; }
            }
            foreach (var t in rt.GetComponentsInChildren<TMP_Text>(true))
                if (t.name == "CostText") t.color = UiTheme.Warm;

            var view = root.GetComponent<UpgradeSlotView>();
            if (view != null)
            {
                var so = new SerializedObject(view);
                void C(string f, Color c) { var p = so.FindProperty(f); if (p != null) p.colorValue = c; }
                C("_bgNormal",   new Color(0.84f, 0.86f, 0.9f, 1f));
                C("_bgSelected", new Color(0.7f, 1f, 1f, 1f));       // cyan'a çeker
                C("_bgOwned",    new Color(0.82f, 1f, 0.88f, 1f));
                C("_bgLocked",   new Color(0.42f, 0.44f, 0.5f, 1f));
                C("_dotOffColor",    UiTheme.WithAlpha(UiTheme.Rim, 0.3f));
                C("_dotOnColor",     UiTheme.Accent);
                C("_dotOwnedColor",  UiTheme.Warm);
                C("_dotLockedColor", UiTheme.WithAlpha(UiTheme.Rim, 0.15f));
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Ana menü zemini: ortası hafif açık, kenarları koyu lacivert radyal geçiş.</summary>
        static Sprite MenuBackground()
        {
            const string path = UiKit.Folder + "/Kit_MenuBg.png";
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;

            const int w = 512, h = 288;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color inner = new Color32(0x1A, 0x25, 0x44, 0xFF), outer = new Color32(0x06, 0x08, 0x10, 0xFF);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - w * 0.42f) / (w * 0.75f), dy = (y - h * 0.55f) / (h * 0.85f);
                float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                px[y * w + x] = Color.Lerp(inner, outer, t * t * (3f - 2f * t));
            }
            tex.SetPixels(px); tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite; imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// İkonu görsele gömülü küçük butonları (geri, +/-) kit butonuna çevirir ve
        /// ikonun yerine bir yazı karakteri koyar.
        /// </summary>
        static void SmallButton(RectTransform rt, UiKit.Style style, string glyph, float size)
        {
            if (rt == null) return;
            var t = rt.GetComponentInChildren<TMP_Text>(true);
            if (t == null)
            {
                t = UiKit.Text("Glyph", rt, glyph, size, UiTheme.Text, true, TextAlignmentOptions.Center);
                UiKit.Stretch((RectTransform)t.transform);
            }
            else t.text = glyph;
            Btn(rt, style, UiTheme.Text);
            t.fontSize = size;
            t.characterSpacing = 0f;
            // + ve - yazı satırının ortasında değil, glifin kendi ortasında dursun
            // (Center satır metriğine göre hizalıyor, glif yukarı/sağa kayıyordu)
            t.alignment = TextAlignmentOptions.CenterGeoAligned;
            t.margin = Vector4.zero;
            UiKit.Stretch(t.rectTransform);
        }

        static void Edit(string path, System.Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            _touched.Clear();
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        static bool EnsureScene(string path, out UnityEngine.SceneManagement.Scene scene)
        {
            scene = default;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[ScreenStyle] Play modundayken çalıştırılamaz.");
                return false;
            }
            scene = EditorSceneManager.GetActiveScene();
            if (scene.path != path) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return true;
        }

        static Transform Canvas(string name)
        {
            var c = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.name == name);
            if (c == null) Debug.LogError($"[ScreenStyle] '{name}' bulunamadı.");
            return c != null ? c.transform : null;
        }

        static RectTransform Find(Transform parent, string path)
            => parent == null ? null : parent.Find(path) as RectTransform;

        static void Mark(Component c) { if (c != null) _touched.Add(c); }

        /// <summary>Prefab örneği değişikliklerini kaydeder ve sahneyi kirli işaretler.</summary>
        static void Commit(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var c in _touched)
                if (c != null && PrefabUtility.IsPartOfPrefabInstance(c))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            _touched.Clear();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void Tint(RectTransform rt, Color c)
        {
            var img = rt != null ? rt.GetComponent<Image>() : null;
            if (img == null) return;
            img.color = c;
            Mark(img);
        }

        /// <summary>
        /// Var olan Image'ı skin görseline (yoksa kit paneline) çevirir. Kod çizimine
        /// düşerse "Rim" çocuğu eklenir; tekrar çalıştırmada eskisi silinir.
        /// </summary>
        static Image Frame(RectTransform rt, string skin, float ppuMultiplier = 1f)
        {
            if (rt == null) return null;
            var old = rt.Find("Rim");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var img = rt.GetComponent<Image>();
            bool raycast = img == null || img.raycastTarget;
            img = UiKit.Panel(rt, UiTheme.WithAlpha(UiTheme.Surface, 0.92f), UiTheme.RadiusPanel, UiTheme.RimSoft,
                              raycast, UiKit.Skin(skin));
            img.preserveAspect = false;
            img.pixelsPerUnitMultiplier = ppuMultiplier;
            Mark(img);
            var rim = rt.Find("Rim");
            if (rim != null) Mark(rim.GetComponent<Image>());
            return img;
        }

        /// <summary>
        /// Var olan bir Button'u kit butonuna çevirir: skin görseli + renk geçişi
        /// (hover'da aydınlanır). KitButton eklenmez — bu butonların bazılarını
        /// kendi script'leri ölçekle oynatıyor (ör. Game Over'daki Upgrade nabzı).
        /// </summary>
        static void Btn(RectTransform rt, UiKit.Style style, Color labelColor)
        {
            if (rt == null) return;
            var img = rt.GetComponent<Image>();
            var skin = UiKit.SkinFor(style);
            if (skin != null) { img.sprite = skin; img.color = Color.white; }
            else { img.sprite = UiKit.Fill(UiTheme.RadiusControl); img.color = style == UiKit.Style.Primary ? UiTheme.AccentDeep : UiTheme.Raised; }
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            img.pixelsPerUnitMultiplier = 1f;
            Mark(img);

            var btn = rt.GetComponent<Button>();
            if (btn != null)
            {
                btn.transition = Selectable.Transition.ColorTint;
                btn.targetGraphic = img;
                var c = btn.colors;
                c.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
                c.highlightedColor = Color.white;
                c.selectedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
                c.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
                c.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                c.colorMultiplier = 1f;
                c.fadeDuration = 0.08f;
                btn.colors = c;
                Mark(btn);
            }

            foreach (var t in rt.GetComponentsInChildren<TMP_Text>(true))
            {
                t.font = UiKit.Bold;
                t.color = labelColor;
                t.characterSpacing = 2f;
                if (t.fontSize < 18f) t.fontSize = 18f;
                Mark(t);
            }
        }

        static void Txt(RectTransform rt, float size, Color color, float spacing = 0f, bool bold = true)
        {
            var t = rt != null ? rt.GetComponent<TMP_Text>() : null;
            if (t == null) return;
            t.font = bold ? UiKit.Bold : UiKit.Regular;
            t.fontSize = size;
            t.color = color;
            t.characterSpacing = spacing;
            Mark(t);
        }
    }
}
