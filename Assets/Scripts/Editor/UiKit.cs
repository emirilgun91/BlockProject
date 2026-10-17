using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RogueBlockBlast.UI;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// UI kitinin editör tarafı: yuvarlak panel sprite'larını koddan üretir ve
    /// builder'ların (pause, ayarlar, HUD) ortak kullandığı yapı taşlarını verir.
    ///
    /// Sprite'lar <c>Assets/Arts/UI/Kit/</c> altına PNG olarak yazılır, sahneler
    /// onlara referans verir. Her köşe yarıçapı için ayrı dolgu + kenar sprite'ı var,
    /// böylece kenar kalınlığı yarıçapla birlikte ölçeklenmez (her boyutta 2px).
    /// Renkler <see cref="UiTheme"/>'dan gelir; sprite'lar beyazdır, Image boyar.
    ///
    /// Menü: Tools ▸ RogueBlockBlast ▸ UI Kit ▸ Regenerate Sprites
    /// </summary>
    public static class UiKit
    {
        public const string Folder = "Assets/Arts/UI/Kit";
        const float RimWidth = 2f;
        const float GlowSize = 28f;

        public enum Style { Primary, Secondary, Danger, Ghost }

        static TMP_FontAsset _bold, _regular;

        public static TMP_FontAsset Bold    => _bold    ??= FindFont("Exo2-Bold SDF") ?? FindFont("Exo2-SemiBold SDF");
        public static TMP_FontAsset Regular => _regular ??= FindFont("Exo2-Regular SDF") ?? Bold;

        static TMP_FontAsset FindFont(string name) =>
            AssetDatabase.FindAssets($"t:TMP_FontAsset {name}")
                .Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(f => f != null && f.name == name);

        // ── Sprite'lar ───────────────────────────────────────────────────────

        public static Sprite Fill(float radius) => Load($"Kit_Fill_r{(int)radius}", () => Generate(radius, 0f, 0f));
        public static Sprite Rim(float radius)  => Load($"Kit_Rim_r{(int)radius}",  () => Generate(radius, RimWidth, 0f));
        public static Sprite Glow(float radius) => Load($"Kit_Glow_r{(int)radius}", () => Generate(radius, 0f, GlowSize));
        /// <summary>Uçları tam yuvarlak hap; kare boyutta Simple çizilince daire olur.</summary>
        public static Sprite Pill    => Load("Kit_Pill",    () => Generate(32f, 0f, 0f, true));
        public static Sprite PillRim => Load("Kit_PillRim", () => Generate(32f, RimWidth, 0f, true));

        [MenuItem("Tools/RogueBlockBlast/UI Kit/Regenerate Sprites", priority = 55)]
        public static void RegenerateAll()
        {
            foreach (var r in new[] { UiTheme.RadiusWindow, UiTheme.RadiusPanel, UiTheme.RadiusControl, UiTheme.RadiusChip })
            {
                Write($"Kit_Fill_r{(int)r}", Generate(r, 0f, 0f));
                Write($"Kit_Rim_r{(int)r}",  Generate(r, RimWidth, 0f));
                Write($"Kit_Glow_r{(int)r}", Generate(r, 0f, GlowSize));
            }
            Write("Kit_Pill",    Generate(32f, 0f, 0f, true));
            Write("Kit_PillRim", Generate(32f, RimWidth, 0f, true));
            Debug.Log("[UiKit] Sprite'lar yeniden üretildi: " + Folder);
        }

        // ── Skin: üretilmiş görseller ────────────────────────────────────────
        //
        // Assets/Arts/UI/Skin/ altındaki görseller varsa yapı taşları onları
        // kullanır, yoksa koddan çizilen sprite'lara düşer — görseller tek tek
        // gelebilir. Görsellerin kenarı ve parıltısı kendi içinde; tint beyaz.
        // PPU, görselin ekranda kaç kat küçük çizileceğini belirler (kaynaklar
        // 1000-2000 px, UI'da 40-500 px); border 9-slice köşe payıdır (kaynak px,
        // sol/alt/sağ/üst). Süsler köşe payının içinde kalmalı.

        public const string SkinFolder = "Assets/Arts/UI/Skin";

        static readonly (string name, float ppu, Vector4 border)[] SkinImport =
        {
            ("Skin_Window",        200, new Vector4(140, 140, 140, 140)),
            ("Skin_Board",         300, new Vector4(120,  80,  80,  80)),
            ("Skin_Button",        400, new Vector4(140, 100, 100, 100)),
            ("Skin_ButtonPrimary", 400, new Vector4(140, 100, 100, 100)),
            ("Skin_ButtonDanger",  400, new Vector4(140, 100, 100, 100)),
            ("Skin_Track",         400, new Vector4( 33,  33,  33,  33)),
            ("Skin_Fill",          400, new Vector4( 33,  33,  33,  33)),
            ("Skin_Chip",          400, new Vector4( 94,  94,  94,  94)),
            ("Skin_CardSlot",      600, new Vector4( 85,  85,  85,  85)),
            ("Skin_Circle",        100, Vector4.zero),
        };

        /// <summary>Görsel yoksa null — çağıran koddan çizilen hale düşer.</summary>
        public static Sprite Skin(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SkinFolder}/{name}.png");

        public static Sprite SkinFor(Style style) => style switch
        {
            Style.Primary   => Skin("Skin_ButtonPrimary"),
            Style.Secondary => Skin("Skin_Button"),
            Style.Danger    => Skin("Skin_ButtonDanger"),
            _               => null,
        };

        [MenuItem("Tools/RogueBlockBlast/UI Kit/Import Skin Sprites", priority = 57)]
        public static void ImportSkins()
        {
            foreach (var (name, ppu, border) in SkinImport)
            {
                var path = $"{SkinFolder}/{name}.png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
                imp.textureType         = TextureImporterType.Sprite;
                imp.spriteImportMode    = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = ppu;
                imp.spriteBorder        = border;
                imp.mipmapEnabled       = false;
                imp.alphaIsTransparency = true;
                imp.wrapMode            = TextureWrapMode.Clamp;
                imp.filterMode          = FilterMode.Bilinear;
                imp.maxTextureSize      = 2048;
                imp.textureCompression  = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            Debug.Log("[UiKit] Skin görselleri içe aktarıldı: " + SkinFolder);
        }

        static Sprite Load(string name, System.Func<(Texture2D tex, int border)> make)
        {
            var path = $"{Folder}/{name}.png";
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;
            Write(name, make());
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Yuvarlak dikdörtgeni işaretli uzaklık alanından (SDF) çizer: 1px yumuşak kenar.
        /// rim &gt; 0 → yalnızca o kalınlıkta kenar çizgisi; glow &gt; 0 → kutunun dışına
        /// taşan yumuşak hale (içi dolu, çünkü panelin arkasında kalır).
        /// </summary>
        static (Texture2D, int) Generate(float radius, float rim, float glow, bool pill = false)
        {
            int pad    = Mathf.CeilToInt(glow) + 2;
            int border = pad + Mathf.CeilToInt(radius) + 2;
            int size   = border * 2 + 4;                     // ortada 4px esneyen şerit
            float half = size / 2f - pad;                    // kutunun yarı genişliği
            if (pill) radius = half;                         // uçlar tam yarım daire

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px  = new Color32[size * size];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - c) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - c) - (half - radius);
                float d  = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                         + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                float a;
                if (glow > 0f)
                {
                    float k = Mathf.Clamp01(1f - d / glow);
                    a = d <= 0f ? 1f : k * k * k;
                }
                else
                {
                    a = Mathf.Clamp01(0.5f - d);
                    if (rim > 0f) a *= Mathf.Clamp01(0.5f + d + rim);
                }
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return (tex, border);
        }

        static void Write(string name, (Texture2D tex, int border) data)
        {
            Directory.CreateDirectory(Folder);
            var path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, data.tex.EncodeToPNG());
            Object.DestroyImmediate(data.tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType         = TextureImporterType.Sprite;
            imp.spriteImportMode    = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100;
            imp.spriteBorder        = new Vector4(data.border, data.border, data.border, data.border);
            imp.mipmapEnabled       = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode            = TextureWrapMode.Clamp;
            imp.filterMode          = FilterMode.Bilinear;
            imp.textureCompression  = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        // ── Yapı taşları ─────────────────────────────────────────────────────

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public static Image Sliced(RectTransform rt, Sprite sprite, Color color, bool raycast)
        {
            var img = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.type = Image.Type.Sliced;
            img.color = color; img.raycastTarget = raycast;
            img.pixelsPerUnitMultiplier = 1f;
            return img;
        }

        /// <summary>
        /// Dolgu bu objede, ince kenar ilk çocuk ("Rim"). Kenar rengi null → kenar yok.
        /// skin verilirse panel o görselle beyaz tint'te çizilir; "Rim" yine kurulur
        /// ama kapalıdır (görselin kendi kenarı var) — isimle onu arayan kod kırılmasın.
        /// </summary>
        public static Image Panel(RectTransform rt, Color fill, float radius, Color? rim = null, bool raycast = true,
                                  Sprite skin = null)
        {
            var img = skin != null ? Sliced(rt, skin, Color.white, raycast) : Sliced(rt, Fill(radius), fill, raycast);
            if (rim.HasValue || skin != null)
            {
                var r = NewRect("Rim", rt);
                Stretch(r);
                r.SetAsFirstSibling();
                var rimImg = Sliced(r, Rim(radius), rim ?? Color.clear, false);
                rimImg.enabled = skin == null;
                // Panel bir layout grubu taşırsa kenar içerik gibi dizilmesin
                r.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            return img;
        }

        /// <summary>
        /// Çerçeveyi bir renge çeker (ör. onay kutusunda tehlike kırmızısı): kod
        /// çizimiyse kenarı boyar, görselse görseli o renge doğru hafifçe tint'ler.
        /// </summary>
        public static void TintFrame(RectTransform panel, Color c)
        {
            var rim = panel.Find("Rim")?.GetComponent<Image>();
            if (rim != null && rim.enabled) { rim.color = c; return; }
            var img = panel.GetComponent<Image>();
            if (img != null) img.color = Color.Lerp(Color.white, c, 0.45f);
        }

        /// <summary>
        /// Pencere: "Glow" (dışa taşan hale) + "Body" (dolgu + kenar). İçerik Body'ye
        /// girer. Hale Body'nin kardeşi, çünkü çocuk olsaydı panelin üstüne çizilirdi.
        /// </summary>
        public static RectTransform Window(Transform parent, string name, Vector2 size, Color glow)
        {
            var win = NewRect(name, parent);
            win.anchorMin = win.anchorMax = new Vector2(0.5f, 0.5f);
            win.pivot = new Vector2(0.5f, 0.5f);
            win.sizeDelta = size;

            // Görselde parıltı zaten var; Glow objesi yine kurulur (builder'lar isimle
            // buluyor) ama çizmez.
            var skin = Skin("Skin_Window");
            var g = NewRect("Glow", win);
            Stretch(g, -GlowSize, -GlowSize, -GlowSize, -GlowSize);
            Sliced(g, Glow(UiTheme.RadiusWindow), glow, false).enabled = skin == null;

            var body = NewRect("Body", win);
            Stretch(body);
            Panel(body, UiTheme.Surface, UiTheme.RadiusWindow, UiTheme.WithAlpha(UiTheme.Rim, 0.7f), skin: skin);
            return body;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
                                           bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(t, size, color, bold, align);
            t.text = text;
            return t;
        }

        public static Assets.SimpleLocalization.Scripts.TextMeshProGUILocalized LocText(
            string name, Transform parent, string key, float size, Color color,
            bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rt = NewRect(name, parent);
            rt.gameObject.AddComponent<Assets.SimpleLocalization.Scripts.LocalizationKeyHolder>().LocalizationKey = key;
            var t = rt.gameObject.AddComponent<Assets.SimpleLocalization.Scripts.TextMeshProGUILocalized>();
            StyleText(t, size, color, bold, align);
            return t;
        }

        static void StyleText(TMP_Text t, float size, Color color, bool bold, TextAlignmentOptions align)
        {
            t.font = bold ? Bold : Regular;
            t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>
        /// Kit butonu. Etiket locKey varsa yerelleştirilir, yoksa düz metindir.
        /// Renk geçişlerini <see cref="KitButton"/> sürer; Button'ın kendi geçişi kapalı.
        /// </summary>
        public static Button Button(Transform parent, string name, Vector2 size, Style style,
                                    string locKey = null, string label = null, float fontSize = 26f,
                                    float radius = UiTheme.RadiusControl)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = size;

            Color fillN, fillH, rimN, rimH, text;
            switch (style)
            {
                case Style.Primary:
                    fillN = UiTheme.AccentDeep; fillH = Color.Lerp(UiTheme.AccentDeep, UiTheme.Accent, 0.28f);
                    rimN = UiTheme.WithAlpha(UiTheme.Accent, 0.8f); rimH = UiTheme.Accent; text = Color.white;
                    break;
                case Style.Danger:
                    fillN = UiTheme.Raised; fillH = UiTheme.DangerDeep;
                    rimN = UiTheme.WithAlpha(UiTheme.Danger, 0.55f); rimH = UiTheme.Danger; text = UiTheme.Danger;
                    break;
                case Style.Ghost:
                    fillN = UiTheme.WithAlpha(UiTheme.Raised, 0f); fillH = UiTheme.Raised;
                    rimN = UiTheme.WithAlpha(UiTheme.Rim, 0f); rimH = UiTheme.RimSoft; text = UiTheme.TextDim;
                    break;
                default:
                    fillN = UiTheme.Raised; fillH = UiTheme.RaisedHover;
                    rimN = UiTheme.RimSoft; rimH = UiTheme.Rim; text = UiTheme.Text;
                    break;
            }

            // Görsel varsa: renk görselin kendisinde, tint yalnızca parlaklık için
            // (normalde hafif kısık, hover'da tam). Kenar görselin içinde.
            var skin = SkinFor(style);
            if (skin != null)
            {
                fillN = new Color(0.86f, 0.86f, 0.86f, 1f); fillH = Color.white;
                rimN = rimH = Color.clear;
                if (style == Style.Danger) text = UiTheme.Text;   // kırmızı görsel üstünde kırmızı yazı okunmuyor
            }

            var fill = Panel(rt, fillN, radius, rimN, skin: skin);
            var rim  = rt.Find("Rim").GetComponent<Image>();

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = fill;
            btn.transition = Selectable.Transition.None;
            var nav = btn.navigation; nav.mode = Navigation.Mode.Automatic; btn.navigation = nav;

            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x; le.preferredHeight = size.y; le.flexibleWidth = 0;

            TMP_Text t = null;
            if (locKey != null) t = LocText("Label", rt, locKey, fontSize, text, true, TextAlignmentOptions.Center);
            else if (label != null) t = Text("Label", rt, label, fontSize, text, true, TextAlignmentOptions.Center);
            if (t != null)
            {
                Stretch((RectTransform)t.transform, 10, 10);
                t.characterSpacing = 2f;
            }

            rt.gameObject.AddComponent<KitButton>().Configure(fill, rim, t, fillN, fillH, rimN, rimH);
            return btn;
        }

        /// <summary>İnce yatay ayraç.</summary>
        public static RectTransform Divider(Transform parent, string name = "Divider")
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = UiTheme.RimSoft; img.raycastTarget = false;
            return rt;
        }
    }
}
