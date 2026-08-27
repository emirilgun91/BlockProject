using RogueBlockBlast.Game;
using RogueBlockBlast.Game.Prototype;
using RogueBlockBlast.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// 2.5D sahne kurulumunu <b>açık olan sahneye</b> uygular.
    ///
    /// <b>Geçmiş:</b> Bu araç önce SampleScene'in kopyasından ayrı bir prototip
    /// sahnesi (SampleScene_25D) üretiyordu. Prototip onaylandıktan sonra o
    /// sahne SampleScene'in üzerine yazıldı ve ana oyun sahnesi oldu; kopyalama
    /// modu kaldırıldı çünkü artık kaynak sahnenin kendisi 2.5D.
    ///
    /// Şimdiki rolü: kurulumu <b>yeniden uygulamak</b>. Tüm adımlar idempotent —
    /// ürettikleri nesneleri önce siler, sonra kurar. Elle yaptığınız ayarlar
    /// (tilt, ışık şiddeti, kart ölçüsü) sıfırlanır; Volume profili korunur.
    ///
    /// Bağımlılıklar (silmeyin, ana sahne kullanıyor):
    ///   • Assets/Prefabs/TilePrefab_25D.prefab    — TilePrefab'in varyantı
    ///   • Assets/Settings/Prototype25D_Volume.asset — post-processing profili
    ///
    /// Kadraj mantığı için <see cref="BoardCameraRig"/> özetine bakın.
    /// </summary>
    public static class Prototype25DSceneBuilder
    {
        private const string CardViewPrefabPath = "Assets/Prefabs/CardView_0.prefab";

        private const string SourceTilePrefab = "Assets/Prefabs/TilePrefab.prefab";
        private const string TargetTilePrefab = "Assets/Prefabs/TilePrefab_25D.prefab";

        private const string VolumeProfilePath = "Assets/Settings/Prototype25D_Volume.asset";

        private const string PedestalName = "Board Pedestal (25D)";
        private const string VolumeName   = "Post FX (25D)";
        private const string KeyLightName      = "Key Light 2D (25D)";
        private const string PhysicalCardsName = "Physical Cards (25D)";
        private const string AtmosphereName    = "Board Atmosphere (25D)";
        private const string FillLightName     = "Fill Light 2D (25D)";
        private const string BackgroundName    = "Background Field (25D)";

        // Skirt varsayılanları — Build her çalıştığında varyanta yazılır ki
        // TileSkirt'teki ayar değişiklikleri mevcut prefaba da yansısın.
        private const float SkirtDepth  = 0.28f;
        private const float SkirtDarken = 0.55f;

        // Işık ayarları. Outer radius tahtanın yüksekliğine yakın tutulur (8×0.85
        // = 6.8): çok büyük yarıçapta düşüş tahtaya yayılmadan biter ve üst-alt
        // farkı okunmaz. Global ışık kısılmazsa tepe ışığı hiç fark edilmez.
        private const float KeyLightIntensity   = 1.5f;
        private const float KeyLightInnerRadius = 2f;
        private const float KeyLightOuterRadius = 9.5f;
        private const float KeyLightFalloff     = 0.7f;
        private const float GlobalLightDimmed   = 0.32f;

        // Fill: alttan, soğuk, kısık. Yarıçap tahtanın tamamını kapsayacak
        // kadar geniş — amaç gradyan yaratmak değil, alt sıraları okunur kılmak.
        private const float FillLightIntensity   = 0.85f;
        private const float FillLightOuterRadius = 11f;

        [MenuItem("Tools/RogueBlockBlast/Scene Setup/Re-apply 2.5D Setup To Open Scene", priority = 90)]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[Scene25D] Açık bir sahne yok.");
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                "2.5D Kurulumu",
                $"Kurulum şu sahneye yeniden uygulanacak:\n\n{scene.name}\n\n" +
                "Kamera, ışıklar, post-processing, kartlar, atmosfer ve arka plan " +
                "yeniden kurulur. Bunlarda elle yaptığınız ayarlar sıfırlanır.\n\n" +
                "Volume profili ve tile varyantı korunur.",
                "Uygula", "Vazgeç");

            if (!ok) return;

            var tileVariant = EnsureTilePrefabVariant();

            int steps = 0;
            steps += SetUpCamera()              ? 1 : 0;
            steps += SetUpBoard(tileVariant)    ? 1 : 0;
            steps += SetUpPedestal(tileVariant) ? 1 : 0;
            steps += SetUpLighting()            ? 1 : 0;
            steps += SetUpPostProcessing()      ? 1 : 0;
            steps += SetUpPhysicalCards()       ? 1 : 0;
            steps += SetUpAtmosphere()          ? 1 : 0;
            steps += SetUpBackground()          ? 1 : 0;

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Scene25D] {scene.name} — {steps}/8 adım uygulandı. " +
                "Sahne kaydedilmedi; kontrol edip Ctrl+S ile kaydedin.");
        }

        // Eskiden burada bir "Delete Scene + Prefab" menüsü vardı. Prototip ana
        // sahne olduktan sonra o menü TilePrefab_25D ve Volume profilini —
        // yani ana sahnenin bağımlılıklarını — silecek hâle geldi, o yüzden
        // kaldırıldı. Kurulumu geri almak artık git'in işi:
        //   git checkout -- Assets/Scenes/SampleScene.unity
        // Düz 2D hâlin dosya yedeği: Backups/SampleScene_Flat2D_*.unity

        // ── Steps ────────────────────────────────────────────────────────────

        /// <summary>
        /// Ortografik kamerayı perspektife çevirir ve rig'i mevcut kadrajı
        /// koruyacak değerlerle doldurur.
        /// </summary>
        private static bool SetUpCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[Prototype25D] Main Camera bulunamadı — kamera adımı atlandı.");
                return false;
            }

            // Rig'in eşleşeceği değerler kameranın mevcut hâlinden okunur;
            // sabit sayı gömmek yerine sahnenin kendi gerçeğini kullanıyoruz.
            float orthoSize = cam.orthographicSize;
            float fov       = cam.fieldOfView;
            Vector3 pos     = cam.transform.position;

            var rig = cam.GetComponent<BoardCameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<BoardCameraRig>();

            rig.AimPoint = new Vector2(pos.x, pos.y);

            var so = new SerializedObject(rig);
            so.FindProperty("_matchOrthographicSize").floatValue = orthoSize;
            so.FindProperty("_fieldOfView").floatValue           = fov;
            so.ApplyModifiedPropertiesWithoutUndo();

            cam.orthographic = false;
            rig.Refresh();

            EditorUtility.SetDirty(cam);
            EditorUtility.SetDirty(rig);

            Debug.Log(
                $"[Prototype25D] Kamera: perspektif, FOV {fov:0.#}, " +
                $"ortho {orthoSize:0.##} eşlemesi, aim ({pos.x:0.##}, {pos.y:0.##}).");
            return true;
        }

        /// <summary>Sahnedeki BoardView'ı skirt'li tile varyantına bağlar.</summary>
        private static bool SetUpBoard(GameObject tileVariant)
        {
            if (tileVariant == null) return false;

            var board = Object.FindFirstObjectByType<BoardView>(FindObjectsInactive.Include);
            if (board == null)
            {
                Debug.LogError("[Prototype25D] BoardView bulunamadı — tile adımı atlandı.");
                return false;
            }

            var tileView = tileVariant.GetComponent<TileView>();
            if (tileView == null)
            {
                Debug.LogError("[Prototype25D] Tile varyantında TileView yok — tile adımı atlandı.");
                return false;
            }

            board.TilePrefab = tileView;
            EditorUtility.SetDirty(board);

            Debug.Log("[Prototype25D] BoardView.TilePrefab → TilePrefab_25D (skirt'li).");
            return true;
        }

        /// <summary>
        /// Tahtanın altına bir zemin koyar — eğimli kamerada tahtanın boşlukta
        /// durmaması bu prototipin görsel farkının yarısı.
        /// </summary>
        private static bool SetUpPedestal(GameObject tileVariant)
        {
            var board = Object.FindFirstObjectByType<BoardView>(FindObjectsInactive.Include);
            if (board == null) return false;

            var run   = Object.FindFirstObjectByType<RunController>(FindObjectsInactive.Include);
            int w     = run != null ? run.Width  : 8;
            int h     = run != null ? run.Height : 8;

            var existing = GameObject.Find(PedestalName);
            if (existing != null) Object.DestroyImmediate(existing);

            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            if (sprite == null)
            {
                Debug.LogWarning("[Prototype25D] Yerleşik zemin sprite'ı alınamadı — zemin atlandı.");
                return false;
            }

            var go = new GameObject(PedestalName);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // Zemin ışığı yakalayan yüzey — tahtanın kendisi çok koyu olduğu için
            // anahtar ışığın düşüşü asıl burada okunuyor. Fazla koyu olursa ışık
            // hiçbir yerde görünmez.
            sr.color = new Color(0.17f, 0.23f, 0.39f, 1f);

            // Sorting layer tile'dan türetilir. Sabit "Default" kullanmak zemini
            // arka planın da arkasına atıyordu: tile'lar "Board" layer'ında, Default
            // ise layer listesinde ilk sırada — yani her şeyin altında.
            var tileSr = tileVariant != null
                ? (tileVariant.GetComponent<SpriteRenderer>()
                   ?? tileVariant.GetComponentInChildren<SpriteRenderer>(true))
                : null;

            if (tileSr != null)
            {
                sr.sortingLayerID = tileSr.sortingLayerID;
                Debug.Log($"[Prototype25D] Zemin sorting layer'ı tile'dan alındı: {sr.sortingLayerName}");
            }
            else
            {
                Debug.LogWarning(
                    "[Prototype25D] Tile'ın SpriteRenderer'ı okunamadı — zemin Default " +
                    "layer'da kaldı ve arka planın arkasında görünmeyebilir.");
            }

            // Tile'lar runtime'da 10, skirt 9 alıyor; zemin ikisinin de altında.
            sr.sortingOrder = -1;

            float boardW = w * board.CellSize;
            float boardH = h * board.CellSize;
            float pad    = board.CellSize * 0.9f;

            Vector2 center = board.OriginWorld + new Vector2(boardW * 0.5f, boardH * 0.5f);

            // Sprite dünya boyutu = bounds.size; ölçek buna göre hesaplanır.
            Vector2 unit = sprite.bounds.size;
            if (unit.x <= 0.0001f || unit.y <= 0.0001f) unit = Vector2.one;

            go.transform.position   = new Vector3(center.x, center.y, 0.05f);
            go.transform.localScale = new Vector3(
                (boardW + pad * 2f) / unit.x,
                (boardH + pad * 2f) / unit.y,
                1f);

            Debug.Log($"[Prototype25D] Zemin kuruldu — {boardW + pad * 2f:0.##} x {boardH + pad * 2f:0.##} birim.");
            return true;
        }

        /// <summary>
        /// Tahtanın üstüne sıcak bir anahtar ışık koyar ve global ışığı kısar.
        ///
        /// <b>Light2D, 3D Light değil.</b> URP 2D Renderer altında
        /// <c>UnityEngine.Light</c> bileşeni hiçbir şey yapmaz — menüdeki
        /// "Light ▸ Point Light" 3D olanı verdiği için kolay karışıyor.
        ///
        /// Anahtar ışık sıfırdan yaratılmaz, sahnedeki <b>Global Light 2D
        /// kopyalanır</b>: bir Light2D'nin hangi sorting layer'ları
        /// aydınlattığı (<c>m_ApplyToSortingLayers</c>) serileştirilmiş bir
        /// alandır ve yeni bir ışıkta yanlış hedefleme yüzünden hiçbir şeyi
        /// aydınlatmaması sık görülen bir tuzak. Kopyalama bu ayarı hazır getirir.
        /// </summary>
        private static bool SetUpLighting()
        {
            // 2D Renderer altında etkisiz olan 3D ışıkları temizle — elle
            // eklenmiş "Point Light" gibi nesneler sahnede kafa karıştırıyor.
            foreach (var stray in Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Debug.LogWarning(
                    $"[Prototype25D] 3D Light kaldırıldı: '{stray.name}' — " +
                    "URP 2D Renderer altında etkisi yok.");
                Object.DestroyImmediate(stray.gameObject);
            }

            var lights = Object.FindObjectsByType<Light2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Light2D global = null;
            foreach (var l in lights)
            {
                if (l.lightType == Light2D.LightType.Global) { global = l; break; }
            }

            if (global == null)
            {
                Debug.LogWarning(
                    "[Prototype25D] Global Light 2D bulunamadı — anahtar ışık atlandı. " +
                    "Sorting layer hedeflemesini ondan miras alıyoruz.");
                return false;
            }

            var stale = GameObject.Find(KeyLightName);
            if (stale != null) Object.DestroyImmediate(stale);

            var key = CreatePointLightFrom(global, KeyLightName);
            var go  = key.gameObject;

            key.color                  = new Color(0.94f, 0.73f, 0.30f, 1f);
            key.intensity              = KeyLightIntensity;
            key.pointLightInnerRadius  = KeyLightInnerRadius;
            key.pointLightOuterRadius  = KeyLightOuterRadius;
            key.falloffIntensity       = KeyLightFalloff;

            // Tahtanın üst kenarının biraz yukarısı.
            var board = Object.FindFirstObjectByType<BoardView>(FindObjectsInactive.Include);
            var run   = Object.FindFirstObjectByType<RunController>(FindObjectsInactive.Include);

            if (board != null)
            {
                int w = run != null ? run.Width  : 8;
                int h = run != null ? run.Height : 8;

                float centerX = board.OriginWorld.x + w * board.CellSize * 0.5f;
                float topY    = board.OriginWorld.y + h * board.CellSize;

                go.transform.position = new Vector3(
                    centerX, topY + board.CellSize * 1.5f, 0f);
            }

            // ── Fill light ───────────────────────────────────────────────
            // Tek başına key light tahtanın altını okunmaz bırakıyordu: ışık
            // üstte (y≈9.3), alt sıra y≈1.7, yani düşüşün %80'i. Global'i
            // yükseltmek gradyanı öldürürdü. Sinematografinin cevabı fill:
            // alttan, soğuk ve kısık. Hem alt sıralar okunur olur hem de
            // sıcak–soğuk karşıtlığı kazanılır.
            var fillStale = GameObject.Find(FillLightName);
            if (fillStale != null) Object.DestroyImmediate(fillStale);

            var fill   = CreatePointLightFrom(global, FillLightName);
            var fillGo = fill.gameObject;

            fill.color                 = new Color(0.31f, 0.75f, 0.92f, 1f);
            fill.intensity             = FillLightIntensity;
            fill.pointLightInnerRadius = 1f;
            fill.pointLightOuterRadius = FillLightOuterRadius;
            fill.falloffIntensity      = 0.5f;

            if (board != null)
            {
                int   wf      = run != null ? run.Width : 8;
                float cxf     = board.OriginWorld.x + wf * board.CellSize * 0.5f;
                float bottomY = board.OriginWorld.y;

                fillGo.transform.position = new Vector3(
                    cxf, bottomY - board.CellSize * 1.5f, 0f);
            }

            // Tepe ışığının okunması için global ışık kısılır — ikisinin dengesi
            // konsept görseldeki "tek sıcak kaynak" etkisini veren şey.
            global.intensity = GlobalLightDimmed;
            EditorUtility.SetDirty(global);

            Debug.Log(
                $"[Prototype25D] Anahtar ışık kuruldu ({go.transform.position}), " +
                $"Global Light 2D yoğunluğu {GlobalLightDimmed}'e çekildi.");
            return true;
        }

        /// <summary>
        /// Şablon ışığın hedeflemesini taşıyan yeni bir Point Light2D üretir.
        ///
        /// Neden <c>Instantiate</c> değil: Global bir ışığı kopyalamak, tipini
        /// Point'e çevirmeden önce sahnede bir an için ikinci bir Global ışık
        /// yaratır ve URP <c>OnEnable</c>'da kaydettiği için
        /// "More than one global light on layer …" uyarısı basar. Sıfırdan
        /// yaratılan ışık zaten Point olduğu için o an hiç oluşmaz.
        ///
        /// Şablondan yalnızca <c>m_ApplyToSortingLayers</c> ve
        /// <c>m_BlendStyleIndex</c> taşınır — bir Light2D'nin hangi sorting
        /// layer'ları aydınlattığı serileştirilmiş bir alandır ve yanlış
        /// hedeflenen ışık sessizce hiçbir şeyi aydınlatmaz.
        /// </summary>
        private static Light2D CreatePointLightFrom(Light2D template, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(template.transform.parent, worldPositionStays: false);

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;

            var src = new SerializedObject(template);
            var dst = new SerializedObject(light);

            CopyIntArray(src, dst, "m_ApplyToSortingLayers");
            CopyInt(src, dst, "m_BlendStyleIndex");

            dst.ApplyModifiedPropertiesWithoutUndo();
            return light;
        }

        private static void CopyIntArray(SerializedObject src, SerializedObject dst, string path)
        {
            var s = src.FindProperty(path);
            var d = dst.FindProperty(path);

            if (s == null || d == null || !s.isArray || !d.isArray)
            {
                Debug.LogWarning(
                    $"[Prototype25D] '{path}' kopyalanamadı — ışık yanlış sorting layer'ı " +
                    "hedefleyebilir ve hiçbir şeyi aydınlatmayabilir.");
                return;
            }

            d.arraySize = s.arraySize;
            for (int i = 0; i < s.arraySize; i++)
                d.GetArrayElementAtIndex(i).intValue = s.GetArrayElementAtIndex(i).intValue;
        }

        private static void CopyInt(SerializedObject src, SerializedObject dst, string path)
        {
            var s = src.FindProperty(path);
            var d = dst.FindProperty(path);
            if (s == null || d == null) return;
            d.intValue = s.intValue;
        }

        /// <summary>
        /// Post-processing'i açar ve prototipe özel bir Volume profili bağlar.
        ///
        /// URP 2D Renderer post-processing'i destekliyor — bu adım için 3D
        /// renderer'a geçmek gerekmiyor. Kamerada varsayılan olarak kapalıydı;
        /// "her şey tek düzey aydınlıkta" hissinin büyük kısmı bundan geliyordu.
        /// </summary>
        private static bool SetUpPostProcessing()
        {
            var cam = Camera.main;
            if (cam == null) return false;

            var camData = cam.GetUniversalAdditionalCameraData();
            if (camData == null)
            {
                Debug.LogWarning("[Prototype25D] Kamera URP verisi okunamadı — post-processing atlandı.");
                return false;
            }

            camData.renderPostProcessing = true;
            camData.antialiasing         = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            EditorUtility.SetDirty(camData);

            var profile = EnsureVolumeProfile();
            if (profile == null) return false;

            var existing = GameObject.Find(VolumeName);
            if (existing != null) Object.DestroyImmediate(existing);

            var go  = new GameObject(VolumeName);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            // Sahnedeki diğer global volume'ların üstünde kalsın.
            vol.priority = 10f;
            vol.profile  = profile;

            Debug.Log("[Prototype25D] Post-processing açıldı — Bloom + Vignette + Color Adjustments.");
            return true;
        }

        /// <summary>
        /// Prototip Volume profilini oluşturur. Zaten varsa dokunmaz — elle
        /// yaptığınız ayarlar yeniden Build'de kaybolmaz.
        /// </summary>
        private static VolumeProfile EnsureVolumeProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (existing != null) return existing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            // Bloom: doygun palet renkleri (#f0bb4c, #4fc0eb, #f04edb) eşiğin
            // üstünde kalıp hafifçe taşsın — tile'lar "yanıyor" gibi okunur.
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true; bloom.threshold.value = 0.9f;
            bloom.intensity.overrideState = true; bloom.intensity.value = 1.0f;
            bloom.scatter.overrideState   = true; bloom.scatter.value   = 0.62f;

            // Vignette: kenarları karartıp gözü tahtaya toplar. Konsept görseldeki
            // "karanlık oda" hissinin en ucuz yarısı.
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState  = true; vignette.intensity.value  = 0.40f;
            vignette.smoothness.overrideState = true; vignette.smoothness.value = 0.55f;

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.overrideState = true; color.postExposure.value = 0.10f;
            color.contrast.overrideState     = true; color.contrast.value     = 10f;
            color.saturation.overrideState   = true; color.saturation.value   = 5f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Prototype25D] Volume profili oluşturuldu: {VolumeProfilePath}");
            return profile;
        }

        /// <summary>
        /// Dünya-uzayı fiziksel kart sunucusunu kurar.
        ///
        /// Kart görselleri yeniden çizilmez — mevcut <c>CardView_0</c> prefabı
        /// World Space Canvas içinde kullanılır. Sunucu kendini
        /// <c>CardSelectionUI.ExternalPresenter</c>'a Start()'ta bağlar;
        /// sahnede yoksa kart seçimi eski UGUI paneliyle çalışmaya devam eder.
        /// </summary>
        private static bool SetUpPhysicalCards()
        {
            var cardViewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardViewPrefabPath);
            if (cardViewPrefab == null)
            {
                Debug.LogWarning(
                    $"[Prototype25D] CardView prefabı bulunamadı ({CardViewPrefabPath}) — " +
                    "fiziksel kartlar atlandı, eski panel kullanılacak.");
                return false;
            }

            var cardView = cardViewPrefab.GetComponent<CardView>();
            if (cardView == null)
            {
                Debug.LogWarning(
                    "[Prototype25D] CardView prefabında CardView bileşeni yok — " +
                    "fiziksel kartlar atlandı.");
                return false;
            }

            var stale = GameObject.Find(PhysicalCardsName);
            if (stale != null) Object.DestroyImmediate(stale);

            var go        = new GameObject(PhysicalCardsName);
            var presenter = go.AddComponent<PhysicalCardPresenter>();

            // Yerleşim kameraya göre hesaplandığı için burada konum yazılmaz —
            // sunucu her Present'te kameranın önünde kuruyor. Yalnızca
            // referanslar bağlanır.
            var so = new SerializedObject(presenter);
            so.FindProperty("_cardViewPrefab").objectReferenceValue = cardView;
            so.FindProperty("_camera").objectReferenceValue         = Camera.main;
            so.FindProperty("_hudGroup").objectReferenceValue       = ResolveHudGroup();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);

            Debug.Log("[Prototype25D] Fiziksel kart sunucusu kuruldu (dünya-uzayı kartlar).");
            return true;
        }

        /// <summary>
        /// Kart seçiminde kısılacak HUD grubunu bulur.
        ///
        /// Hedef, skor/combo göstergelerinin bulunduğu gameplay canvas'ı —
        /// isimden değil, <see cref="ScoreView"/>'ın hangi Canvas'ın altında
        /// olduğundan çözülür. Canvas'ta CanvasGroup yoksa eklenir.
        ///
        /// Neden ayrıca gerekli: dünya perdesi Screen Space Overlay canvas'ını
        /// kapatamaz, overlay her zaman en üstte çizilir.
        /// </summary>
        private static CanvasGroup ResolveHudGroup()
        {
            var score = Object.FindFirstObjectByType<ScoreView>(FindObjectsInactive.Include);
            if (score == null)
            {
                Debug.LogWarning("[Prototype25D] ScoreView bulunamadı — HUD karartması bağlanmadı.");
                return null;
            }

            var canvas = score.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Prototype25D] ScoreView bir Canvas altında değil — HUD karartması bağlanmadı.");
                return null;
            }

            // Kök canvas'ı al — iç içe canvas varsa en üstteki dolduruluyor.
            var root  = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            var group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.gameObject.AddComponent<CanvasGroup>();

            EditorUtility.SetDirty(root);
            Debug.Log($"[Prototype25D] HUD karartması → {root.name}");
            return group;
        }

        /// <summary>
        /// Sahne atmosferini komboya bağlar. Referanslar sahneden çözülür;
        /// taban ışık/post-fx değerleri runtime'da bileşenin kendisi okur.
        /// </summary>
        private static bool SetUpAtmosphere()
        {
            var stale = GameObject.Find(AtmosphereName);
            if (stale != null) Object.DestroyImmediate(stale);

            var go         = new GameObject(AtmosphereName);
            var atmosphere = go.AddComponent<BoardAtmosphere>();

            var run   = Object.FindFirstObjectByType<RunController>(FindObjectsInactive.Include);
            var board = Object.FindFirstObjectByType<BoardView>(FindObjectsInactive.Include);
            var vol   = Object.FindFirstObjectByType<Volume>(FindObjectsInactive.Include);

            Light2D key = null;
            var keyGo = GameObject.Find(KeyLightName);
            if (keyGo != null) key = keyGo.GetComponent<Light2D>();

            var so = new SerializedObject(atmosphere);
            so.FindProperty("_runController").objectReferenceValue = run;
            so.FindProperty("_boardView").objectReferenceValue     = board;
            so.FindProperty("_keyLight").objectReferenceValue      = key;
            so.FindProperty("_volume").objectReferenceValue        = vol;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(atmosphere);

            if (run == null || board == null || key == null || vol == null)
            {
                Debug.LogWarning(
                    "[Prototype25D] Atmosfer bağlandı ama bazı referanslar boş " +
                    $"(run:{run != null} board:{board != null} light:{key != null} volume:{vol != null}). " +
                    "Boş olanlar runtime'da aranacak.");
            }
            else
            {
                Debug.Log("[Prototype25D] Atmosfer komboya bağlandı — ışık, bloom, vignette ve tahta nabzı.");
            }

            return true;
        }

        /// <summary>
        /// Katmanlı parallax arka planı kurar. Mevcut <c>BackgroundPattern</c>
        /// nesnesine dokunulmaz — yeni alan onun önünde/arkasında kendi
        /// katmanlarını kurar; istemezseniz bu nesneyi kapatmanız yeter.
        /// </summary>
        private static bool SetUpBackground()
        {
            var stale = GameObject.Find(BackgroundName);
            if (stale != null) Object.DestroyImmediate(stale);

            var go    = new GameObject(BackgroundName);
            var field = go.AddComponent<BackgroundField>();

            var atmosphere = Object.FindFirstObjectByType<BoardAtmosphere>(FindObjectsInactive.Include);

            var so = new SerializedObject(field);
            so.FindProperty("_camera").objectReferenceValue     = Camera.main;
            so.FindProperty("_atmosphere").objectReferenceValue = atmosphere;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(field);

            Debug.Log("[Prototype25D] Arka plan alanı kuruldu — zemin gradyanı + 3 parallax katmanı.");
            return true;
        }

        // ── Assets ───────────────────────────────────────────────────────────

        /// <summary>
        /// TilePrefab'in skirt'li varyantını oluşturur (varsa yeniden kullanır).
        /// Varyant olduğu için orijinal prefabdaki her değişiklik buraya da akar.
        /// </summary>
        private static GameObject EnsureTilePrefabVariant()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TargetTilePrefab);
            if (existing != null)
            {
                var skirt = existing.GetComponent<TileSkirt>();
                if (skirt == null)
                {
                    Debug.LogWarning(
                        "[Prototype25D] Mevcut tile varyantında TileSkirt yok — " +
                        "elle eklemeniz gerekebilir.");
                }
                else
                {
                    WriteSkirtSettings(skirt);
                }
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceTilePrefab);
            if (source == null)
            {
                Debug.LogError($"[Prototype25D] Kaynak tile prefabı bulunamadı: {SourceTilePrefab}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (instance == null)
            {
                Debug.LogError("[Prototype25D] Tile prefabı sahneye alınamadı.");
                return null;
            }

            WriteSkirtSettings(instance.AddComponent<TileSkirt>());

            var variant = PrefabUtility.SaveAsPrefabAsset(instance, TargetTilePrefab);
            Object.DestroyImmediate(instance);

            if (variant == null)
                Debug.LogError($"[Prototype25D] Tile varyantı yazılamadı: {TargetTilePrefab}");
            else
                Debug.Log($"[Prototype25D] Tile varyantı oluşturuldu: {TargetTilePrefab}");

            return variant;
        }

        /// <summary>Skirt ayarlarını private alanlar üzerinden yazar.</summary>
        private static void WriteSkirtSettings(TileSkirt skirt)
        {
            if (skirt == null) return;

            var so = new SerializedObject(skirt);
            so.FindProperty("_depth").floatValue  = SkirtDepth;
            so.FindProperty("_darken").floatValue = SkirtDarken;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(skirt);
        }
    }
}
