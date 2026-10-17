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
    /// Oyun sahnesindeki HUD'ı UI kitinin diline çevirir: Skor, Kombo, Hedef panoları,
    /// kalan parça sayacı, kart envanteri ve havuz slotları.
    ///
    /// Yeniden kurmaz, yerinde boyar: objeler, isimleri ve bileşen referansları
    /// aynen kalır (ComboView, MilestoneView, PortraitSceneBuilder onları isimle
    /// veya referansla buluyor). Değişen: sprite'lar, renkler, yazı stilleri ve
    /// pano içi yerleşim. Tekrar çalıştırılabilir.
    ///
    /// Menü: Tools ▸ RogueBlockBlast ▸ UI Kit ▸ Restyle HUD (Game Scene)
    /// Sonrasında GamePortrait için Build Portrait Scene tekrar çalıştırılmalı.
    /// </summary>
    public static class HudStyleBuilder
    {
        const float Pad = 26f;   // pano içi kenar boşluğu

        [MenuItem("Tools/RogueBlockBlast/UI Kit/Restyle HUD (Game Scene)", priority = 56)]
        public static void Restyle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[HudStyle] Play modundayken çalıştırılamaz.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("SampleScene.unity"))
                scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.name == "Gameplay Canvas");
            if (canvas == null) { Debug.LogError("[HudStyle] 'Gameplay Canvas' bulunamadı."); return; }
            var root = canvas.transform;

            var targets = new[] { "ScoreBoard", "ComboBoard", "MilestoneBoard", "CardInventoryUI", "PoolPanel" }
                .Select(n => Find(root, n)).Where(t => t != null).ToArray();

            Score(Find(root, "ScoreBoard"));
            Combo(Find(root, "ComboBoard"));
            Milestone(Find(root, "MilestoneBoard"));
            Inventory(Find(root, "CardInventoryUI"));
            Pool(Find(root, "PoolPanel"));

            // Panolar prefab örneği: script'le değişen alanlar kaydedilmezse sahne
            // kaydında prefab değerine döner (sprite'lar eski çerçeveye dönüyordu).
            foreach (var t in targets)
            foreach (var c in t.GetComponentsInChildren<Component>(true))
                if (c != null && PrefabUtility.IsPartOfPrefabInstance(c))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(c);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[HudStyle] HUD UI kiti stiline çevrildi.");
        }

        // ── Panolar ──────────────────────────────────────────────────────────

        /// <summary>
        /// Pano zemini: Skin_Board görseli, yoksa yarı saydam yüzey + ince kenar.
        /// Eski kenar silinip yeniden kurulur.
        /// </summary>
        static void Board(RectTransform rt, float ppuMultiplier = 1f, string skin = "Skin_Board")
        {
            var old = rt.Find("Rim");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var img = UiKit.Panel(rt, UiTheme.WithAlpha(UiTheme.Surface, 0.88f), UiTheme.RadiusPanel, UiTheme.RimSoft,
                                  skin: UiKit.Skin(skin));
            img.pixelsPerUnitMultiplier = ppuMultiplier;
            rt.Find("Rim").GetComponent<Image>().pixelsPerUnitMultiplier = ppuMultiplier;
        }

        /// <summary>Panonun sol üstündeki küçük, aralıklı başlık.</summary>
        static void Label(RectTransform rt)
        {
            if (rt == null) return;
            Place(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(Pad, -22), new Vector2(300, 30));
            var t = rt.GetComponent<TMP_Text>();
            t.font = UiKit.Bold; t.fontSize = 20; t.color = UiTheme.TextDim;
            t.characterSpacing = 6; t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }

        static void Score(RectTransform board)
        {
            if (board == null) return;
            Board(board);
            Label(Find(board, "ScoreText"));

            var num = Find(board, "ScoreNumb");
            Place(num, new Vector2(0, 0), new Vector2(0, 0), new Vector2(Pad, 16), new Vector2(448, 84));
            var t = num.GetComponent<TMP_Text>();
            t.font = UiKit.Bold; t.fontSize = 64; t.color = UiTheme.Warm;
            t.alignment = TextAlignmentOptions.BottomLeft;
        }

        static void Combo(RectTransform board)
        {
            if (board == null) return;
            Board(board);
            Label(Find(board, "ComboText"));

            // Çarpan: sağda, büyük, vurgu renginde
            var num = Find(board, "ComboNumText");
            Place(num, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-Pad, -2), new Vector2(170, 90), new Vector2(1, 0.5f));
            var t = num.GetComponent<TMP_Text>();
            t.font = UiKit.Bold; t.fontSize = 56; t.color = UiTheme.Accent;
            t.alignment = TextAlignmentOptions.MidlineRight;
            var beat = num.GetComponent<RogueBlockBlast.UI.Juice.ComboHeartbeat>();
            if (beat != null) SetColor(beat, "_glowColor", UiTheme.Accent);

            // Şarj çubukları: alt solda, büyüyen basamaklar, yuvarlak köşeli
            var container = Find(board, "ChargeContainer");
            Place(container, new Vector2(0, 0), new Vector2(0, 0), new Vector2(Pad, 22), new Vector2(300, 46));
            int i = 0;
            foreach (RectTransform charge in container)
            {
                float h = 16 + i * 7;
                Place(charge, new Vector2(0, 0), new Vector2(0, 0), new Vector2(i * 58, 0), new Vector2(50, h), Vector2.zero);
                charge.localScale = Vector3.one;
                Chip(Find(charge, "BG"), Find(charge, "Fill"), UiTheme.RadiusChip, "Skin_Chip");
                i++;
            }

            var view = board.GetComponent<ComboView>();
            SetColor(view, "_colorEmpty",   UiTheme.Raised);
            SetColor(view, "_colorFilled",  UiTheme.Accent);
            SetColor(view, "_colorMaxGlow", new Color(0.75f, 0.97f, 1f, 1f));
        }

        static void Milestone(RectTransform board)
        {
            if (board == null) return;
            Board(board);
            Label(Find(board, "Text (TMP)"));

            // Hedef skor: sağ üstte
            var target = Find(board, "Milestone_Text");
            Place(target, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Pad, -18), new Vector2(260, 44), new Vector2(1, 1));
            target.localScale = Vector3.one;
            var tt = target.GetComponent<TMP_Text>();
            tt.font = UiKit.Bold; tt.fontSize = 34; tt.color = UiTheme.Text;
            tt.alignment = TextAlignmentOptions.TopRight;

            // İlerleme çubuğu: altta, tam genişlik. Eski kurulum ölçekle (450×60)
            // boyutlanıyordu; artık gerçek rect.
            var bar = Find(board, "Charge_1");
            bar.localScale = Vector3.one;
            bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.offsetMin = new Vector2(Pad, 30); bar.offsetMax = new Vector2(-Pad, 44);
            Chip(Find(bar, "BG"), Find(bar, "Fill"), 0f, "Skin_Fill");
            // FlowingFill açık kalır: dalga katmanları kaynağın fillAmount'una göre daralıyor.
            // Kapatılırsa Awake'te kurulan katmanlar tam genişlikte beyaz kalıyordu.
            var flow = Find(bar, "Fill").GetComponent<RogueBlockBlast.UI.Juice.FlowingFill>();
            if (flow != null) flow.enabled = true;

            var view = board.GetComponent<RogueBlockBlast.UI.MilestoneView>();
            SetColor(view, "_colorNormal",   UiTheme.Progress);
            SetColor(view, "_colorDanger",   UiTheme.Danger);
            SetColor(view, "_colorComplete", UiTheme.Success);

            // Kalan parça sayacı: halka + yazı (konumları korunur, havuzun solunda)
            var ring = Find(board, "Image");
            var count = Find(board, "NCard_Text");
            if (ring != null)
            {
                ring.sizeDelta = new Vector2(120, 120);
                var img = ring.GetComponent<Image>();
                img.type = Image.Type.Simple; img.preserveAspect = true;
                var oldRing = ring.Find("Ring");
                if (oldRing != null) Object.DestroyImmediate(oldRing.gameObject);

                // Skin_Circle dolguyu ve halkayı tek görselde taşıyor; aciliyet rengi
                // (ShapeCounterPulse) o zaman tüm görseli tint'ler.
                Image ringImg;
                var circle = UiKit.Skin("Skin_Circle");
                if (circle != null)
                {
                    img.sprite = circle; img.color = Color.white;
                    ringImg = img;
                }
                else
                {
                    img.sprite = UiKit.Pill;
                    img.color = UiTheme.WithAlpha(UiTheme.Surface, 0.9f);
                    var r = UiKit.NewRect("Ring", ring);
                    UiKit.Stretch(r);
                    ringImg = r.gameObject.AddComponent<Image>();
                    ringImg.sprite = UiKit.PillRim; ringImg.color = UiTheme.Rim; ringImg.raycastTarget = false;
                }

                var pulse = count != null ? count.GetComponent<RogueBlockBlast.UI.Juice.ShapeCounterPulse>() : null;
                if (pulse != null)
                {
                    var so = new SerializedObject(pulse);
                    var f = so.FindProperty("_frame");
                    if (f != null) f.objectReferenceValue = ringImg;
                    var u = so.FindProperty("_urgentColor");
                    if (u != null) u.colorValue = UiTheme.Danger;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            if (count != null)
            {
                count.localScale = Vector3.one;
                count.sizeDelta = new Vector2(104, 70);
                if (ring != null)
                {
                    // halkayla aynı merkez — çapası da aynı olmalı, yoksa konum başka yere göre okunur
                    count.anchorMin = ring.anchorMin; count.anchorMax = ring.anchorMax;
                    count.pivot = ring.pivot;
                    count.anchoredPosition = ring.anchoredPosition;
                }
                var ct = count.GetComponent<TMP_Text>();
                ct.font = UiKit.Bold; ct.fontSize = 22; ct.color = UiTheme.Text;
                ct.alignment = TextAlignmentOptions.Center;
                ct.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        /// <summary>
        /// Arka plan + dolgu çifti. Dolgu kaynağı (Filled Image, View'ler tween'liyor)
        /// kapatılır; "KitFill" çocuğu sliced çizilir ve KitFillMirror onu kaynağa bağlar.
        /// radius 0 → hap şekli. Skin görselleri varsa iz Skin_Track, dolgu fillSkin
        /// (milestone: Skin_Fill, kombo: Skin_Chip); dolgu beyaz, rengi View'den gelir.
        /// </summary>
        static void Chip(RectTransform bg, RectTransform fill, float radius, string fillSkin)
        {
            var sprite = radius > 0 ? UiKit.Fill(radius) : UiKit.Pill;
            var track  = UiKit.Skin("Skin_Track");
            var fillSprite = UiKit.Skin(fillSkin) ?? sprite;

            UiKit.Stretch(bg); bg.localScale = Vector3.one;
            var bgImg = bg.GetComponent<Image>();
            bgImg.type = Image.Type.Sliced;
            if (fillSkin == "Skin_Chip" && fillSprite != sprite)
            {
                // Kombo şarjı: iz, dolgunun aynı görseli sönük tint'le. Skin_Track
                // koyu ve farklı biçimli olduğundan dolu parçanın etrafında siyah
                // bir hap kalıyordu.
                bgImg.sprite = fillSprite;
                bgImg.color  = UiTheme.WithAlpha(UiTheme.Rim, 0.22f);
            }
            else
            {
                bgImg.sprite = track ?? sprite;
                bgImg.color  = track != null ? Color.white : UiTheme.Sunken;
            }

            UiKit.Stretch(fill); fill.localScale = Vector3.one;
            var src = fill.GetComponent<Image>();
            src.enabled = false;

            var old = fill.Find("KitFill");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var k = UiKit.NewRect("KitFill", fill);
            var kImg = k.gameObject.AddComponent<Image>();
            kImg.sprite = fillSprite; kImg.type = Image.Type.Sliced; kImg.raycastTarget = false;

            var mirror = fill.GetComponent<KitFillMirror>() ?? fill.gameObject.AddComponent<KitFillMirror>();
            mirror.Configure(src, kImg, false);
        }

        // ── Envanter ve havuz ────────────────────────────────────────────────

        static void Inventory(RectTransform inv)
        {
            if (inv == null) return;
            // BoardClearanceFitter paneli ~0.46 ölçekliyor; köşe ve kenar o ölçekte de
            // diğer panolarla aynı görünsün diye sprite yoğunluğu yarıya iner.
            // Portrait'te ölçek 1 olduğundan PortraitSceneBuilder bunu 1'e geri alır.
            Board(inv, 0.5f);

            var tip = Find(inv, "CardTooltip/Background");
            if (tip != null)
            {
                var old = tip.Find("Rim");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                UiKit.Panel(tip, UiTheme.Surface, UiTheme.RadiusPanel, UiTheme.Rim, skin: UiKit.Skin("Skin_Window"));
                var effect = Find(tip, "CardEffect");
                if (effect != null) effect.GetComponent<TMP_Text>().color = UiTheme.Warm;
            }
        }

        static void Pool(RectTransform pool)
        {
            if (pool == null) return;
            foreach (var name in new[] { "Slot1", "Slot2", "Slot3" })
            {
                var slot = Find(pool, name);
                if (slot == null) continue;

                var frame = Find(slot, "Image");
                var old = frame.Find("Rim");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                UiKit.Panel(frame, UiTheme.WithAlpha(UiTheme.Surface, 0.88f), UiTheme.RadiusPanel, UiTheme.RimSoft,
                            skin: UiKit.Skin("Skin_Board"));   // havuz slotu pano diliyle aynı

                // Seçim: vurgu renginde kenar, slotun biraz dışında
                var sel = Find(slot, "SelectionFrame");
                sel.sizeDelta = new Vector2(160, 160);
                UiKit.Sliced(sel, UiKit.Rim(UiTheme.RadiusPanel), UiTheme.Accent, false);

                var key = Find(slot, "Key_Txt");
                key.anchoredPosition = new Vector2(0, -16);
                var kt = key.GetComponent<TMP_Text>();
                kt.font = UiKit.Bold; kt.fontSize = 16; kt.color = UiTheme.TextDim;
            }
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        static RectTransform Find(Transform parent, string path) => parent.Find(path) as RectTransform;

        static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            if (rt == null) return;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.pivot = pivot ?? new Vector2(0, aMin.y);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        static void SetColor(Object target, string field, Color c)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[HudStyle] {target.GetType().Name}.{field} yok — atlandı."); return; }
            p.colorValue = c;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
