using System.Collections.Generic;
using System.Linq;
using RogueBlockBlast.Game;
using RogueBlockBlast.Game.Prototype;
using RogueBlockBlast.Game.Tutorial;
using RogueBlockBlast.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// Portrait (dikey, mobil) oyun sahnesini üretir:
    /// <c>Assets/Scenes/SampleScene.unity</c> → <c>Assets/Scenes/GamePortrait.unity</c>.
    ///
    /// Kopyalayıp dikey düzene çevirir; SampleScene'e dokunmaz. Tekrar çalıştırmak
    /// portrait sahneyi SampleScene'in güncel hâlinden baştan üretir — portrait
    /// sahnede elle yaptığın düzen değişiklikleri kaybolur (diğer builder'larla aynı
    /// kural). Kalıcı düzen değişikliğini buraya yaz.
    ///
    /// Düzen (1080×1920 referans):
    ///   üst      duraklat butonu · tutorial satırı
    ///            Combo | Score | Milestone  (yan yana)
    ///   orta     tahta (PortraitCameraFit genişliğe sığdırır)
    ///   alt      kart envanteri (tek satır, yatay kayar)
    ///            havuz slotları · reroll · döndür butonu
    /// </summary>
    public static class PortraitSceneBuilder
    {
        public const string SourcePath = "Assets/Scenes/SampleScene.unity";
        public const string TargetPath = "Assets/Scenes/GamePortrait.unity";
        public const string MainMenuPath = "Assets/Scenes/MainMenu.unity";

        static readonly Vector2 RefResolution = new Vector2(1080f, 1920f);

        [MenuItem("Tools/RogueBlockBlast/Scene Setup/Build Portrait Scene")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            // 1) Kopyala (varsa baştan)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetPath) != null)
                AssetDatabase.DeleteAsset(TargetPath);
            if (!AssetDatabase.CopyAsset(SourcePath, TargetPath))
            {
                Debug.LogError($"[Portrait] {SourcePath} kopyalanamadı.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);

            // 2) Canvas
            var canvasGo = GameObject.Find("Gameplay Canvas");
            if (canvasGo == null) { Debug.LogError("[Portrait] Gameplay Canvas bulunamadı."); return; }
            // Canvas prefab örneğiyse portrait sahnede bağlantısını kopar: bileşen
            // silmek (RotateHintView, BoardClearanceFitter) prefab örneğinde yasak ve
            // portrait düzeni prefab'a geri akmamalı. Landscape sahne etkilenmez.
            if (PrefabUtility.IsPartOfPrefabInstance(canvasGo))
                PrefabUtility.UnpackPrefabInstance(
                    PrefabUtility.GetOutermostPrefabInstanceRoot(canvasGo),
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var canvas = (RectTransform)canvasGo.transform;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RefResolution;
            // Genişliğe eşle: telefonlar 9:16 … 9:21 arası — fazla yükseklik ortada boşluk olur, taşma olmaz.
            scaler.matchWidthOrHeight  = 0f;

            // 3) HUD — üstte yan yana
            PlaceTop(Find(canvas, "ComboBoard"),     new Vector2(-352f, -205f), 0.64f);
            PlaceTop(Find(canvas, "ScoreBoard"),     new Vector2(   0f, -205f), 0.64f);
            PlaceTop(Find(canvas, "MilestoneBoard"), new Vector2( 352f, -205f), 0.64f);

            // 4) Havuz — altta ortalı, büyük dokunma hedefleri
            var pool = Find(canvas, "PoolPanel");
            if (pool != null)
            {
                SetRect(pool, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 330f));

                // Landscape'te slotları bir HorizontalLayoutGroup diziyor; o, aşağıda
                // yazılan konumları eziyor ve havuza taşınan sayacı (ve aktifleşince
                // reroll butonunu) slotların yanına diziyordu. Portrait'te konumlar açık.
                var hlg = pool.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null) Object.DestroyImmediate(hlg);
                string[] slots = { "Slot1", "Slot2", "Slot3" };
                for (int i = 0; i < slots.Length; i++)
                {
                    var s = Find(pool, slots[i]);
                    if (s == null) continue;
                    SetRect(s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2((i - 1) * 232f, 0f), new Vector2(200f, 200f));
                }
                var reroll = Find(pool, "PoolRerollButton");
                if (reroll != null)
                    SetRect(reroll, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(-440f, -100f), new Vector2(84f, 84f));

                // Kalan parça sayacı ("28 left") landscape'te MilestoneBoard'un çocuğu ve
                // ondan (-1063, -322) uzakta — portrait'te bu ofset onu tahtanın üstüne
                // düşürüyordu. Havuzun soluna taşınır; iki parça (halka + yazı) birlikte,
                // aralarındaki konum korunarak.
                var milestone = Find(canvas, "MilestoneBoard");
                if (milestone != null)
                {
                    var ring  = Find(milestone, "Image");
                    var count = Find(milestone, "NCard_Text");
                    var counterPos = new Vector2(-440f, 30f);
                    if (ring != null)
                    {
                        ring.SetParent(pool, worldPositionStays: false);
                        SetRect(ring, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                counterPos, new Vector2(125f, 125f));
                        ring.localScale = Vector3.one;
                    }
                    if (count != null)
                    {
                        // Landscape oranı korunur: halka 1.0, yazı 0.63 ölçek, aynı merkez
                        count.SetParent(pool, worldPositionStays: false);
                        SetRect(count, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                counterPos, count.sizeDelta);
                        count.localScale = Vector3.one * 0.63f;
                    }

                    // "Yeni kart" bildirimi: HUD'ın altında, ortada
                    var newCard = Find(milestone, "NewCardText");
                    if (newCard != null)
                    {
                        newCard.SetParent(canvas, worldPositionStays: false);
                        SetRect(newCard, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                                new Vector2(0f, -330f), newCard.sizeDelta);
                        newCard.localScale = Vector3.one;
                    }
                }
            }

            // 5) Kart envanteri — havuzun üstünde tek satır, yatay kayar
            var inv = Find(canvas, "CardInventoryUI");
            if (inv != null)
            {
                var fitter = inv.GetComponent<BoardClearanceFitter>();
                if (fitter != null) Object.DestroyImmediate(fitter);   // tahtanın solunda yer yok — fitter landscape'e özgü

                SetRect(inv, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 338f), new Vector2(-48f, 150f));
                inv.localScale = Vector3.one;

                var ui = inv.GetComponent<CardInventoryUI>();
                if (ui != null) { ui.HorizontalStrip = true; EditorUtility.SetDirty(ui); }

                var vbar = Find(inv, "Scrollbar Vertical");
                if (vbar != null) vbar.gameObject.SetActive(false);

                var viewport = Find(inv, "Viewport");
                if (viewport != null)
                {
                    viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
                    viewport.offsetMin = viewport.offsetMax = Vector2.zero;
                }
            }

            // 6) Modal paneller — dar ekrana sığsın
            ScaleToFit(Find(canvas, "CardScreen/Panel"),   RefResolution.x * 0.94f);
            ScaleToFit(Find(canvas, "GameOverUI/Panel"),   RefResolution.x * 0.94f);
            ScaleToFit(Find(canvas, "GameOver_Txt"),       RefResolution.x * 0.94f);
            // Ayarlar: dikeyde bol yer var — pencere uzar, kaydırmadan daha çok satır görünür
            var settingsWindow = Find(canvas, "SettingsPanel/Window");
            if (settingsWindow != null) settingsWindow.sizeDelta = new Vector2(settingsWindow.sizeDelta.x, 1560f);
            ScaleToFit(settingsWindow, RefResolution.x * 0.94f);

            // Mobilde "masaüstüne çık" anlamsız (iOS izin vermez); portrait sahnede gizli
            var pauseCtrl = Object.FindAnyObjectByType<PauseMenuController>(FindObjectsInactive.Include);
            if (pauseCtrl != null)
            {
                var quit = new SerializedObject(pauseCtrl).FindProperty("_quitButton")?.objectReferenceValue as Button;
                if (quit != null) quit.gameObject.SetActive(false);
            }

            // 7) Klavye ipucu → dokunmatik kontroller
            var rotateHint = canvasGo.GetComponent<RotateHintView>();
            if (rotateHint != null) Object.DestroyImmediate(rotateHint);
            if (canvasGo.GetComponent<MobileControlsView>() == null) canvasGo.AddComponent<MobileControlsView>();

            var tutorial = canvasGo.GetComponent<TutorialController>();
            if (tutorial != null) { tutorial.TouchText = true; EditorUtility.SetDirty(tutorial); }

            // 8) Sürükle-bırak yerleştirme
            var run = Object.FindAnyObjectByType<RunController>();
            if (run != null) { run.DragToPlace = true; EditorUtility.SetDirty(run); }

            // 9) Kamera — tahtayı dikey ekranın genişliğine sığdır
            var cam = Camera.main;
            if (cam != null)
            {
                var fit = cam.GetComponent<PortraitCameraFit>() ?? cam.gameObject.AddComponent<PortraitCameraFit>();
                fit.Apply(RefResolution.x / RefResolution.y);
                EditorUtility.SetDirty(fit);
                EditorUtility.SetDirty(cam.GetComponent<BoardCameraRig>());
            }

            // 10) Dokunmatikte "hover" yok — dokunma anında tetikleniyor. Kart yukarı
            //     kalkınca parmağın altındaki SELECT butonu kayıyor ve bırakma başka
            //     yere düşüyor: tıklama kayboluyordu. Kalkma kapalı, hafif büyüme kalır
            //     (merkezden büyüdüğü için buton parmağın altında kalır).
            var presenter = Object.FindAnyObjectByType<PhysicalCardPresenter>();
            if (presenter != null)
            {
                var so = new SerializedObject(presenter);
                SetFloat(so, "_hoverLift",  0f);
                SetFloat(so, "_hoverScale", 1.05f);
                SetFloat(so, "_maxFanScreenWidth", 0.90f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 11) Kamera paralaksı fareye göre kayıyordu — dokunmatikte parmağın
            //     konumuna göre kadrajın (ve kart yelpazesinin) kayması anlamsız.
            var rig = cam != null ? cam.GetComponent<BoardCameraRig>() : null;
            if (rig != null)
            {
                var so = new SerializedObject(rig);
                var p = so.FindProperty("_mouseParallax");
                if (p != null) p.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AddToBuildSettings(TargetPath);

            // Ana menü: ayrı sahne değil — build'de ilk sahne olduğu için telefon önce
            // onu açar. PortraitMenuLayout ekran dikeyken aynı sahneyi yeniden dizer,
            // yataya dönünce orijinal düzeni geri yükler (landscape davranışı değişmez).
            var menu = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            var menuCanvas = GameObject.Find("MainMenuCanvas");
            if (menuCanvas != null && menuCanvas.GetComponent<PortraitMenuLayout>() == null)
            {
                menuCanvas.AddComponent<PortraitMenuLayout>();
                EditorSceneManager.MarkSceneDirty(menu);
                EditorSceneManager.SaveScene(menu);
            }
            else if (menuCanvas == null) Debug.LogWarning("[Portrait] MainMenuCanvas bulunamadı — ana menü portrait düzeni eklenmedi.");

            EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);
            Debug.Log($"[Portrait] {TargetPath} üretildi ve Build Settings'e eklendi.");
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        static RectTransform Find(Transform root, string path)
        {
            var t = root.Find(path);
            if (t == null) Debug.LogWarning($"[Portrait] '{path}' bulunamadı — atlandı.");
            return t as RectTransform;
        }

        static void PlaceTop(RectTransform rt, Vector2 pos, float scale)
        {
            if (rt == null) return;
            // Pivot değişince anchoredPosition kayar; konumu pivotla birlikte yaz.
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one * scale;
        }

        static void SetFloat(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.floatValue = value;
            else Debug.LogWarning($"[Portrait] {so.targetObject.GetType().Name}.{name} bulunamadı — atlandı.");
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        /// <summary>Genişliği maxWidth'i aşan paneli orantılı küçültür (içerik prefab ölçüsünde kalır).</summary>
        static void ScaleToFit(RectTransform rt, float maxWidth)
        {
            if (rt == null) return;
            float w = rt.rect.width > 1f ? rt.rect.width : rt.sizeDelta.x;
            if (w <= maxWidth) return;
            rt.localScale = Vector3.one * (maxWidth / w);
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
