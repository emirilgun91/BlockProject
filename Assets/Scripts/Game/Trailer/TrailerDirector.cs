#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using RogueBlockBlast.Game.Prototype;
using RogueBlockBlast.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace RogueBlockBlast.Game.Trailer
{
    /// <summary>
    /// Trailer çekimi için otomatik oyuncu. Oyunu GERÇEK yollardan oynar: sanal fare
    /// tahtaya gider, ghost görünür, tıklama RunController.Update'ten DoPlace'e iner;
    /// kart seçimi fiziksel karta tıklanarak yapılır. Bu yüzden ekrandaki puan,
    /// milestone, kalan şekil sayısı ve eldeki kart sayısı her zaman birbiriyle
    /// tutarlıdır — hiçbir değer elle yazılmaz.
    ///
    /// Akış: (1) FastForward — görünmeden, hızlı yerleştirmelerle hedef milestone'a
    /// kadar oynar (her milestone'da gerçek kart seçimi). (2) Perform — kayıt açılır,
    /// fare ile insan hızında oynar. Havuzlar <see cref="ShapeSpawnService.DebugForcedShapes"/>
    /// ile, ağırlıklı rastgele örneklerin en iyisi seçilerek yönlendirilir; şekiller
    /// yine oyuncunun açık şekillerinden ve gerçek ağırlıklardan gelir.
    ///
    /// Kayıt: <see cref="TakeStarted"/> / <see cref="TakeEnded"/> olaylarını editördeki
    /// TrailerRecorderBridge dinler (Unity Recorder).
    /// </summary>
    public sealed class TrailerDirector : MonoBehaviour
    {
        [Serializable]
        public sealed class TakeSpec
        {
            public string Name = "take";
            public int    Seed = 1;
            /// <summary>Kayıt başlamadan önce görünmeden ulaşılacak milestone sayısı (= eldeki kart sayısı).</summary>
            public int    FastForwardMilestones = 0;
            /// <summary>Kayıt sırasında ulaşılacak milestone sayısı; 0 → yalnızca süre sınırı.</summary>
            public int    RecordMilestones = 1;
            public float  MaxRecordSeconds = 60f;
            /// <summary>Son milestone'da kart seçildikten sonra kayda devam süresi.</summary>
            public float  TailSeconds = 2.5f;
            /// <summary>Sırayla seçilmesi istenen kart Id'leri (sunulmuşsa). Boşsa tercih listesi.</summary>
            public string[] CardPlan = Array.Empty<string>();
            public bool   ShowCursor = true;
            /// <summary>1 = normal insan hızı; büyüdükçe fare hareketleri ve beklemeler kısalır.</summary>
            public float  Pace = 1f;
            /// <summary>Planlayıcı çok-satırlı temizlemeye ne kadar oynasın (gösteri).</summary>
            public float  Spectacle = 1.5f;
            /// <summary>FastForward sonunda tahtayı tamamen temizle (temiz açılış planı).</summary>
            public bool   ClearBoardAfterFF = false;
            public bool   Record = true;
            /// <summary>Sayı biçimi kültürü (boş → sistem). Ekrandaki "x1.6" / "1,500" bundan gelir.</summary>
            public string Culture = "en-US";
            /// <summary>Hata ayıklama: kurulumdan sonra bekle, oynama.</summary>
            public bool   Idle = false;
            /// <summary>Başlarken RunController.NewRun çağır (temiz tahta, giriş animasyonu).</summary>
            public bool   RestartRun = true;
            /// <summary>Kayıtta oyun müziğini sustur (SFX kalır).</summary>
            public bool   MuteMusic = true;
            /// <summary>Kayıtta kötü oyna (temizleme yapma) → şekiller biter → Game Over.</summary>
            public bool   Lose = false;
            /// <summary>Lose: kayıt başlamadan kalan şekil sayısı bu değere inene dek görünmeden oyna.</summary>
            public int    LoseLeft = 5;
            /// <summary>Game Over sonrası Upgrade Now → Shape Shop: kilitli bir şekli aç, bir upgrade al.</summary>
            public bool   Shop = false;
            /// <summary>İlk havuza bu şekli koy (ör. Shop'ta yeni açılan şekil).</summary>
            public string ForceFirstShape = "";
            /// <summary>Her kart seçiminde sunulacağı garanti edilen kart (sırayla). CardPlan boşsa bunları seçer.</summary>
            public string[] OfferPlan = Array.Empty<string>();
            /// <summary>Ana menü take'i: Upgrades paneli açılır, bu upgrade Id'leri sırayla satın alınır.</summary>
            public string[] MenuUpgrades = Array.Empty<string>();
            /// <summary>Clutch: kayıt, eşiğe ClutchGap kala ve ClutchLeft şekil kala başlar.</summary>
            public bool   Clutch = false;
            public int    ClutchLeft = 3;
            public int    ClutchGap = 220;
        }

        public static event Action<string> TakeStarted;
        public static event Action         TakeEnded;

        public static TrailerDirector Instance { get; private set; }
        public static bool  IsRunning => Instance != null && Instance._running;
        public static string LastReport = "";

        public TakeSpec Spec = new TakeSpec();
        public string   Phase = "idle";

        /// <summary>Kart tercih sırası — görsel olarak güçlü, oyunu bozmayan kartlar önde.</summary>
        public static readonly string[] CardPreference =
        {
            "Card_Neon_Cable", "Cart_Line_Master", "Card_Double_Strike", "Card_Chain_Master",
            "Card_Corner_Stone", "Card_Center_Base", "Card_Bounty_Hunter", "Card_Momentum",
            "Card_Combo_Shield", "Card_Momentum_Shield", "Card_Score_Multiplier", "Card_Wide_Pool",
            "Card_Card_Collector", "Card_Perfect_Clear", "Card_Heavy_Load", "Card_Soft_Landing",
        };

        static readonly HashSet<string> CardBlacklist = new HashSet<string>
        {
            "Card_Selective_Blindness", "Card_Decaying_Rift", "Diet_plan", "Card_First_Picks",
            "Cart_Tunnel_Vision", "Card_Safe_Zone", "Card_Phantom_Cell", "Card_Hyperfocus",
            "Card_Slow_Burn", "Card_Gambler", "Card_Patient", "Card_Future_Investment",
        };

        // ── Başlatma ─────────────────────────────────────────────────────────

        /// <summary>MCP / editör: play mode'da çağrılır. Spec JSON (JsonUtility).</summary>
        public static string Begin(string specJson)
        {
            if (!Application.isPlaying) return "not playing";
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("[TrailerDirector]");
            var d  = go.AddComponent<TrailerDirector>();
            if (!string.IsNullOrEmpty(specJson)) JsonUtility.FromJsonOverwrite(specJson, d.Spec);
            return "started " + d.Spec.Name;
        }

        public static string Abort()
        {
            if (Instance == null) return "none";
            Destroy(Instance.gameObject);
            return "aborted";
        }

        // ── Reflection ───────────────────────────────────────────────────────

        RunController _rc;
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static FieldInfo Fld(string n) => typeof(RunController).GetField(n, F);
        static readonly FieldInfo fBoard    = Fld("_board");
        static readonly FieldInfo fPool     = Fld("_piecePool");
        static readonly FieldInfo fSel      = Fld("_selectedPoolIndex");
        static readonly FieldInfo fRot      = Fld("_currentRot");
        static readonly FieldInfo fCur      = Fld("_currentPiece");
        static readonly FieldInfo fScore    = Fld("_score");
        static readonly FieldInfo fOver     = Fld("_gameOverFired");
        static readonly FieldInfo fDirty    = Fld("_poolDirty");
        static readonly FieldInfo fDrag     = Fld("_dragToPlace");
        static readonly MethodInfo mSelect  = typeof(RunController).GetMethod("SelectPool", F);
        static readonly MethodInfo mPlace   = typeof(RunController).GetMethod("DoPlace", F);
        static readonly MethodInfo mNewRun  = typeof(RunController).GetMethod("NewRun", F);
        static readonly MethodInfo mGenPool = typeof(RunController).GetMethod("GenerateNewPool", F);
        static readonly PropertyInfo pRotLock = typeof(RunController).GetProperty("IsRotationLocked", F);

        BoardModel            Board   => (BoardModel)fBoard.GetValue(_rc);
        List<PieceDefinition> Pool    => (List<PieceDefinition>)fPool.GetValue(_rc);
        int                   SelIdx  => (int)fSel.GetValue(_rc);
        Rotation              CurRot  => (Rotation)fRot.GetValue(_rc);
        int                   Score   => (int)fScore.GetValue(_rc);
        bool                  Over    => (bool)fOver.GetValue(_rc);
        bool                  RotLock => (bool)pRotLock.GetValue(_rc);

        static readonly FieldInfo fCsOpen = typeof(CardSelectionUI).GetField("_isOpen", F);
        static readonly MethodInfo mCsPick = typeof(CardSelectionUI).GetMethod("OnCardSelected", F);
        static readonly FieldInfo fPcCards = typeof(PhysicalCardPresenter).GetField("_cards", F);
        static readonly FieldInfo fPcData  = typeof(PhysicalCardPresenter).GetField("_cardData", F);
        static readonly FieldInfo fPcCam   = typeof(PhysicalCardPresenter).GetField("_camera", F);
        static readonly FieldInfo fPoolSlots = typeof(PoolView).GetField("_slots", F);
        static readonly FieldInfo fRcPoolView = Fld("PoolView");

        // ── Durum ────────────────────────────────────────────────────────────

        bool  _running;
        Mouse _mouse;
        readonly List<InputDevice> _disabled = new();
        Vector2 _mousePos;
        bool  _mouseDown;
        float _scroll;
        RectTransform _cursorRt;
        System.Random _rng;
        int   _cardsPickedTotal;
        int   _milestonesAtStart;
        float _performClock;
        int   _cardPlanIndex;
        readonly System.Text.StringBuilder _log = new();

        void Awake()  { Instance = this; }

        System.Globalization.CultureInfo _prevCulture;

        void Start()
        {
            // İngilizce trailer: sayı biçimi sistem kültüründen (tr-TR → "x1,6", "1.500") gelmesin
            _prevCulture = System.Globalization.CultureInfo.CurrentCulture;
            if (!string.IsNullOrEmpty(Spec.Culture))
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(Spec.Culture);
            _rng = new System.Random(Spec.Seed);
            UnityEngine.Random.InitState(Spec.Seed);
            StartCoroutine(Main());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_origCardPool != null && _rc != null) fCardPool.SetValue(_rc, _origCardPool);
            if (_prevCulture != null) System.Globalization.CultureInfo.CurrentCulture = _prevCulture;
            RestoreAudio();
            ShapeSpawnService.DebugForcedShapes.Clear();
            if (_mouse != null)
            {
                InputSystem.RemoveDevice(_mouse);
                InputSystem.settings.backgroundBehavior = _prevBg;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = _prevEditorBehavior;
#endif
            }
            foreach (var d in _disabled) if (d != null && d.added) InputSystem.EnableDevice(d);
            if (_cursorRt != null) Destroy(_cursorRt.root.gameObject);
            if (_running) { _running = false; TakeEnded?.Invoke(); }
        }

        void Log(string s)
        {
            string line = $"[Trailer:{Spec.Name}] {s}";
            _log.AppendLine(line);
            LastReport = _log.ToString();
            Debug.Log(line);
        }

        string StateLine()
        {
            var ms = _rc.Milestone;
            int inv = CardInventoryUI.Instance != null ? CardInventoryUI.Instance.GetSelectedCardIds().Count() : -1;
            return $"score={Score} milestone#{ms?.CurrentMilestoneIndex} next={ms?.NextMilestone?.ScoreThreshold} " +
                   $"left={ms?.PiecesRemaining}/{ms?.EffectivePoolLimit} invCards={inv} picked={_cardsPickedTotal} " +
                   $"combo=x{_rc.Combo?.Multiplier:0.00}";
        }

        // ── Ana akış ─────────────────────────────────────────────────────────

        IEnumerator Main()
        {
            if (Spec.MenuUpgrades != null && Spec.MenuUpgrades.Length > 0) { yield return MenuUpgradesTake(); Destroy(gameObject); yield break; }
            float t0 = Time.realtimeSinceStartup;
            while ((_rc = FindFirstObjectByType<RunController>()) == null || fBoard.GetValue(_rc) == null || Pool.Count == 0)
            {
                if (Time.realtimeSinceStartup - t0 > 10f) { Log("RunController bulunamadı"); Destroy(gameObject); yield break; }
                yield return null;
            }

            fDrag.SetValue(_rc, false);
            SetupMouse();

            // Temiz run: HUD metinleri (combo "x1.1", eşik "1,500") yeni kültürle baştan çizilsin,
            // tahta giriş animasyonu oynasın, seed tekrarlanabilir olsun.
            if (Spec.RestartRun)
            {
                UnityEngine.Random.InitState(Spec.Seed);
                mNewRun.Invoke(_rc, null);
                // Giriş animasyonu bitmeden oynamak karoları bozuyordu; bitmesini bekle
                yield return Wait(3.0f);
                if (!string.IsNullOrEmpty(Spec.ForceFirstShape)) ForceFirstPool(Spec.ForceFirstShape);
            }
            Log("hazır " + StateLine());

            // (1) FastForward
            Phase = "fastforward";
            if (Spec.FastForwardMilestones > 0)
            {
                int frames = 0;
                while (_rc.Milestone.CurrentMilestoneIndex < Spec.FastForwardMilestones || CardSelectionOpen())
                {
                    if (Over) { Log("FF sırasında oyun bitti! " + StateLine() + "\n" + StatLine() + "\n" + _trace); Phase = "failed"; yield break; }
                    if (CardSelectionOpen()) { FastPickCard(); yield return null; yield return null; continue; }
                    if (!GameStateController.InputAllowed) { yield return null; continue; }

                    if (!FastPlaceOne()) { Log("FF hamle yok " + StateLine()); Phase = "failed"; yield break; }
                    frames++;
                    yield return null;
                    yield return null;
                }
                Log($"FF bitti ({frames} hamle) " + StateLine() + "\n" + StatLine());
                if (Spec.ClearBoardAfterFF) _rc.DebugClearBoard();
                yield return Wait(2.5f);
            }

            // (1a) Clutch: puanı eşiğin hemen altına getir, sonra temizlemeden şekil harca —
            // kayıt "son birkaç şekille milestone" anından başlar
            if (Spec.Clutch)
            {
                while (!Over)
                {
                    if (CardSelectionOpen()) { FastPickCard(); yield return null; yield return null; continue; }
                    if (!GameStateController.InputAllowed) { yield return null; continue; }
                    int target = _rc.Milestone.NextMilestone?.ScoreThreshold ?? int.MaxValue;
                    if (Score >= target - Spec.ClutchGap) break;
                    if (!FastPlaceOne()) break;
                    yield return null; yield return null;
                }
                while (!Over && _rc.Milestone.PiecesRemaining > Spec.ClutchLeft)
                {
                    if (CardSelectionOpen()) { FastPickCard(); yield return null; yield return null; continue; }
                    if (!GameStateController.InputAllowed) { yield return null; continue; }
                    var bad = BadMove(out _);
                    if (bad == null) break;
                    mSelect.Invoke(_rc, new object[] { bad.Value.PoolIndex });
                    fRot.SetValue(_rc, bad.Value.Rot);
                    mPlace.Invoke(_rc, new object[] { bad.Value.Anchor });
                    yield return null; yield return null; yield return null;
                }
                Log("clutch hazır " + StateLine());
                yield return Wait(2.5f);
            }

            // (1b) Kaybetme: görünmeden kötü oynayıp kalan şekli LoseLeft'e indir
            if (Spec.Lose)
            {
                while (!Over && _rc.Milestone.PiecesRemaining > Spec.LoseLeft)
                {
                    if (CardSelectionOpen()) { FastPickCard(); yield return null; yield return null; continue; }
                    if (!GameStateController.InputAllowed) { yield return null; continue; }
                    var bad = BadMove(out _);
                    if (bad == null) break;
                    mSelect.Invoke(_rc, new object[] { bad.Value.PoolIndex });
                    fRot.SetValue(_rc, bad.Value.Rot);
                    mPlace.Invoke(_rc, new object[] { bad.Value.Anchor });
                    yield return null; yield return null; yield return null;
                }
                Log("kaybetme hazır " + StateLine());
                yield return Wait(2.5f);
            }

            // (2) Perform
            Phase = "perform";
            _milestonesAtStart = _rc.Milestone.CurrentMilestoneIndex;
            CreateCursor();
            _mousePos = new Vector2(Screen.width * 0.55f, Screen.height * 0.45f);
            yield return Wait(0.3f);

            // Hata ayıklama: fare/imleç kurulu, oyun oynanmaz — DebugMouse ile elle sür
            while (Spec.Idle) { Phase = "idle"; yield return null; }

            if (Spec.Record) { _running = true; TakeStarted?.Invoke(Spec.Name); }
            Log("KAYIT BAŞLADI " + StateLine() + " bozukKaro=" + CountBrokenTiles());
            _performClock = 0f;
            yield return Wait(1.5f);   // Recorder ilk kareleri geç yakalıyor; açılış kaybolmasın

            while (true)
            {
                if (Over) { Log("oyun bitti " + StateLine()); if (Spec.Shop) yield return PostGameShop(); else yield return Wait(Spec.TailSeconds); break; }
                int reached = _rc.Milestone.CurrentMilestoneIndex - _milestonesAtStart;
                if (Spec.RecordMilestones > 0 && reached >= Spec.RecordMilestones && !CardSelectionOpen())
                {
                    yield return Wait(Spec.TailSeconds);
                    break;
                }
                if (_performClock > Spec.MaxRecordSeconds) break;

                if (CardSelectionOpen()) { yield return PerformPickCard(); continue; }
                if (!GameStateController.InputAllowed) { yield return null; continue; }

                yield return PerformPlaceOne();
            }

            Log("KAYIT BİTTİ " + StateLine() + " bozukKaro=" + CountBrokenTiles());
            if (_running) { _running = false; TakeEnded?.Invoke(); }
            Phase = "done";
            yield return Wait(0.5f);
            Destroy(gameObject);
        }

        bool _audioGuarded;
#if UNITY_EDITOR
        bool _prevEditorMute;
#endif

        /// <summary>
        /// Kayıtta ses: editör "Mute Audio" Recorder'a sessizlik yazdırıyordu; odak kaybında
        /// SettingsBootstrap master'ı 0'a çekiyordu. Çekim boyunca ikisine karşı her kare
        /// korunur; MuteMusic açıksa müzik susturulur (kurguda tek parça müzik döşenir).
        /// </summary>
        void GuardAudio()
        {
            var am = AudioManager.Instance;
            if (!_audioGuarded)
            {
                _audioGuarded = true;
#if UNITY_EDITOR
                _prevEditorMute = UnityEditor.EditorUtility.audioMasterMute;
                UnityEditor.EditorUtility.audioMasterMute = false;
#endif
            }
            if (am == null) return;
            am.SetMasterVolume(Mathf.Max(0.5f, Core.Settings.GameSettings.EffectiveMaster));
            if (Spec.MuteMusic) am.SetMusicVolume(0f);
        }

        void RestoreAudio()
        {
            if (!_audioGuarded) return;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.audioMasterMute = _prevEditorMute;
#endif
            Core.Settings.GameSettings.ApplyAudio();
        }

        void Update()
        {
            _performClock += Time.unscaledDeltaTime;
            GuardAudio();
            if (_mouse == null) return;

            var st = new MouseState { position = _mousePos, scroll = new Vector2(0f, _scroll) };
            if (_mouseDown) st.buttons = 1;
            InputSystem.QueueStateEvent(_mouse, st);
            _scroll = 0f;

            if (_cursorRt != null) _cursorRt.anchoredPosition = _mousePos;
        }

        /// <summary>Hata ayıklama: imleci ekran noktasına koy / bas. Durum satırı döner.</summary>
        public string DebugMouse(float x, float y, bool down)
        {
            _mousePos = new Vector2(x, y); _mouseDown = down;
            string cell = _rc.BoardView.TryGetMouseCellPoint(_rc.MainCamera, out var cp) ? cp.ToString("0.00") : "none";
            return $"pointer={UnityEngine.InputSystem.Pointer.current?.name} pos={UnityEngine.InputSystem.Pointer.current?.position.ReadValue()} cell={cp} cam={_rc.MainCamera.transform.position} ts={Time.timeScale}";
        }

        public Vector2 DebugCellScreen(int x, int y) => CellScreen(x, y);

        public string DebugCards()
        {
            var cs = CardSelectionUI.Instance;
            var pc = cs?.ExternalPresenter as PhysicalCardPresenter;
            if (pc == null) return "no presenter";
            var cards = (List<PhysicalCard>)fPcCards.GetValue(pc);
            var cam = (Camera)fPcCam.GetValue(pc);
            var s = $"open={CardSelectionOpen()} cam={cam?.name} ";
            foreach (var c in cards)
            {
                if (c == null) continue;
                var rts = c.GetComponentsInChildren<RectTransform>();
                Vector3 ctr = c.transform.position;
                var btn = c.GetComponentInChildren<Button>();
                if (btn != null) { var r = (RectTransform)btn.transform; ctr = r.TransformPoint(r.rect.center); }
                s += $"[{c.name} root={cam.WorldToScreenPoint(c.transform.position)} btn={cam.WorldToScreenPoint(ctr)} inter={c.IsInteractive}] ";
            }
            return s;
        }

        IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // ── Sanal fare ve imleç ──────────────────────────────────────────────

        InputSettings.BackgroundBehavior _prevBg;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode _prevEditorBehavior;
#endif

        void SetupMouse()
        {
            // Editör odakta değilken (MCP, başka pencere) Input System olayları düşürüyordu:
            // sanal fare (0,0)'da kalıyor, tıklamalar kayboluyordu. Çekim boyunca odaktan bağımsız.
            var settings = InputSystem.settings;
            _prevBg = settings.backgroundBehavior;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            _prevEditorBehavior = settings.editorInputBehaviorInPlayMode;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            Application.runInBackground = true;

            foreach (var d in InputSystem.devices.ToList())
                if (d is Mouse && d.enabled) { InputSystem.DisableDevice(d); _disabled.Add(d); }
            _mouse = InputSystem.AddDevice<Mouse>("TrailerMouse");
            _mouse.MakeCurrent();
        }

        void CreateCursor()
        {
            if (!Spec.ShowCursor) return;
            var go = new GameObject("[TrailerCursor]", typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var img = new GameObject("Arrow", typeof(RectTransform), typeof(RawImage));
            img.transform.SetParent(go.transform, false);
            _cursorRt = (RectTransform)img.transform;
            _cursorRt.anchorMin = _cursorRt.anchorMax = Vector2.zero;
            _cursorRt.pivot     = new Vector2(0.06f, 0.96f);
            float s = Screen.height / 1080f;
            _cursorRt.sizeDelta = new Vector2(44f, 44f) * s;
            var raw = img.GetComponent<RawImage>();
            raw.texture = BuildArrowTexture(128);
            raw.raycastTarget = false;
        }

        /// <summary>Klasik ok imleci: beyaz dolgu, koyu kenar, yumuşak gölge — kodda çizilir.</summary>
        static Texture2D BuildArrowTexture(int size)
        {
            var poly = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 17f), new Vector2(4.2f, 13.2f), new Vector2(7.2f, 20f),
                new Vector2(9.6f, 19f), new Vector2(6.7f, 12.3f), new Vector2(12.2f, 12.3f),
            };
            float scale = size / 22f;
            for (int i = 0; i < poly.Length; i++) poly[i] = new Vector2(poly[i].x * scale + size * 0.04f, poly[i].y * scale + size * 0.03f);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float edge = 1.6f * scale;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // y aşağı doğru (ok ucu sol üstte)
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = SignedDist(p, poly);                    // içeride negatif
                var shadowP = p - new Vector2(1.2f, 1.6f) * scale * 0.5f;
                float ds = SignedDist(shadowP, poly);
                float aFill  = Mathf.Clamp01(0.5f - (d + edge) );
                float aShape = Mathf.Clamp01(0.5f - d);
                float aShadow = Mathf.Clamp01(1f - Mathf.Max(0f, ds) / (2.2f * scale)) * 0.35f;
                var fill = Color.Lerp(new Color(0.06f, 0.08f, 0.14f, 1f), Color.white, aFill);
                Color c = fill; c.a = aShape;
                // gölgeyi altına karıştır
                float outA = c.a + aShadow * (1f - c.a);
                Color o = outA > 0f ? new Color(c.r * c.a / outA, c.g * c.a / outA, c.b * c.a / outA, outA) : Color.clear;
                px[(size - 1 - y) * size + x] = o;
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static float SignedDist(Vector2 p, Vector2[] poly)
        {
            float md = float.MaxValue; bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];
                if (((b.y > p.y) != (a.y > p.y)) && (p.x < (a.x - b.x) * (p.y - b.y) / (a.y - b.y) + b.x)) inside = !inside;
                Vector2 ab = a - b; float t = Mathf.Clamp01(Vector2.Dot(p - b, ab) / ab.sqrMagnitude);
                md = Mathf.Min(md, (b + ab * t - p).magnitude);
            }
            return inside ? -md : md;
        }

        IEnumerator MoveMouse(Func<Vector2> target, float duration)
        {
            Vector2 start = _mousePos;
            Vector2 end0  = target();
            // hafif kavis: yol normaline rastgele sapma
            Vector2 dir   = end0 - start;
            Vector2 nrm   = new Vector2(-dir.y, dir.x).normalized;
            float   bend  = ((float)_rng.NextDouble() - 0.5f) * 0.25f * dir.magnitude;
            duration /= Spec.Pace;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float e = u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) / 2f;
                Vector2 end = target();
                Vector2 mid = Vector2.Lerp(start, end, 0.5f) + nrm * bend;
                Vector2 a = Vector2.Lerp(start, mid, e), b = Vector2.Lerp(mid, end, e);
                _mousePos = Vector2.Lerp(a, b, e);
                yield return null;
            }
            _mousePos = target();
        }

        IEnumerator Click()
        {
            _mouseDown = true;
            yield return null;
            yield return null;
            _mouseDown = false;
            yield return null;
        }

        float MoveTime(Vector2 to)
        {
            float dist = (to - _mousePos).magnitude / Mathf.Max(1f, Screen.height);
            return Mathf.Lerp(0.22f, 0.5f, Mathf.Clamp01(dist / 0.8f));
        }

        Vector2 CellScreen(int cx, int cy)
        {
            var bv  = _rc.BoardView;
            var cam = _rc.MainCamera;
            bv.TryGetWorldBox(out _, out var max);
            float z = bv.GetTile(0, 0) != null ? bv.GetTile(0, 0).transform.position.z : 0f;
            var w = new Vector3(bv.OriginWorld.x + (cx + 0.5f) * bv.CellSize, bv.OriginWorld.y + (cy + 0.5f) * bv.CellSize, z);
            return cam.WorldToScreenPoint(w);
        }

        Vector2 PoolSlotScreen(int index)
        {
            var pv = (PoolView)fRcPoolView.GetValue(_rc);
            var slots = (List<PoolSlotView>)fPoolSlots.GetValue(pv);
            var rt = (RectTransform)slots[index].transform;
            var canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
        }

        // ── Kart seçimi ──────────────────────────────────────────────────────

        bool CardSelectionOpen()
        {
            var cs = CardSelectionUI.Instance;
            if (cs == null) return false;
            if (cs.ExternalPresenter is PhysicalCardPresenter pc)
                return ((List<CardSO>)fPcData.GetValue(pc)).Count > 0 && Time.timeScale == 0f && !GameStateController.InputAllowed;
            return (bool)fCsOpen.GetValue(cs);
        }

        int ChooseCard(IList<CardSO> offered)
        {
            var plan = Spec.CardPlan.Length > 0 ? Spec.CardPlan : Spec.OfferPlan;
            if (_cardPlanIndex < plan.Length)
            {
                string want = plan[_cardPlanIndex];
                for (int i = 0; i < offered.Count; i++) if (offered[i] != null && offered[i].Id == want) return i;
            }
            var owned = CardInventoryUI.Instance != null ? new HashSet<string>(CardInventoryUI.Instance.GetSelectedCardIds()) : new HashSet<string>();
            int best = -1, bestRank = int.MaxValue;
            for (int i = 0; i < offered.Count; i++)
            {
                var c = offered[i]; if (c == null) continue;
                int rank = Array.IndexOf(CardPreference, c.Id);
                if (rank < 0) rank = CardBlacklist.Contains(c.Id) ? 1000 : 100;
                if (owned.Contains(c.Id)) rank += 50;         // aynı kartı ikinci kez alma (envanter çeşitli görünsün)
                if (rank < bestRank) { bestRank = rank; best = i; }
            }
            return Mathf.Max(0, best);
        }

        void FastPickCard()
        {
            var cs = CardSelectionUI.Instance;
            var pc = cs.ExternalPresenter as PhysicalCardPresenter;
            var data = pc != null ? (List<CardSO>)fPcData.GetValue(pc) : null;
            if (data == null || data.Count == 0) return;
            int i = ChooseCard(data);
            var card = data[i];
            _cardPlanIndex++;
            _cardsPickedTotal++;
            Log($"kart (FF): {card.Id}  sunulan=[{string.Join(",", data.Select(c => c?.Id))}]");
            mCsPick.Invoke(cs, new object[] { card });
        }

        IEnumerator PerformPickCard()
        {
            var cs = CardSelectionUI.Instance;
            var pc = cs.ExternalPresenter as PhysicalCardPresenter;
            if (pc == null) { FastPickCard(); yield break; }

            var cards = (List<PhysicalCard>)fPcCards.GetValue(pc);
            var data  = (List<CardSO>)fPcData.GetValue(pc);
            var cam   = (Camera)fPcCam.GetValue(pc);

            // dağıtım animasyonu bitsin
            float guard = 0f;
            while (cards.Any(c => c != null && !c.IsInteractive) && guard < 4f) { guard += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.35f);

            int pick = ChooseCard(data);
            Func<int, Vector2> cardPos = i => cards[i] != null ? (Vector2)cam.WorldToScreenPoint(cards[i].transform.position) : _mousePos;
            // Tıklanabilir yer kartın SELECT butonu (gövde raycast almıyor); hover kartı
            // kaldırdığı için konum her karede yeniden okunur.
            Func<int, Vector2> buttonPos = i =>
            {
                var btn = cards[i] != null ? cards[i].GetComponentInChildren<Button>() : null;
                if (btn == null) return cardPos(i);
                var r = (RectTransform)btn.transform;
                return cam.WorldToScreenPoint(r.TransformPoint(r.rect.center));
            };

            // önce başka bir karta göz at (karar anı), sonra seçilene geç
            int other = data.Count > 1 ? (pick == 0 ? 1 : 0) : pick;
            if (other != pick)
            {
                yield return MoveMouse(() => cardPos(other), 0.42f);
                yield return Wait(0.55f / Spec.Pace);
            }
            yield return MoveMouse(() => cardPos(pick), 0.38f);
            yield return Wait(0.6f / Spec.Pace);
            yield return MoveMouse(() => buttonPos(pick), 0.25f);
            yield return Wait(0.18f / Spec.Pace);

            var card = data[pick];
            Log($"kart: {card.Id}  sunulan=[{string.Join(",", data.Select(c => c?.Id))}]");
            yield return Click();

            float t = 0f;
            while (CardSelectionOpen() && t < 1.0f) { t += Time.unscaledDeltaTime; yield return null; }
            if (CardSelectionOpen())
            {
                Log("tıklama karta ulaşmadı → doğrudan seçim");
                mCsPick.Invoke(cs, new object[] { card });
            }
            _cardPlanIndex++;
            _cardsPickedTotal++;
            yield return Wait(0.7f / Spec.Pace);
        }

        // ── Yerleştirme ──────────────────────────────────────────────────────

        int _statPlacements, _statClears, _statResets; float _statMaxMult;
        readonly System.Text.StringBuilder _trace = new();

        void Stat(int predictedLines, float multBefore, int scoreBefore)
        {
            _statPlacements++;
            float mult = _rc.Combo?.Multiplier ?? 1f;
            bool cleared = Score > scoreBefore || mult > multBefore;
            if (cleared) _statClears++;
            if (mult < multBefore - 0.001f) _statResets++;
            _statMaxMult = Mathf.Max(_statMaxMult, mult);
            _trace.Append(predictedLines).Append(cleared ? "c" : "-").Append(_rc.Combo?.Charges ?? 0).Append(' ');
        }

        string StatLine() =>
            $"yerleştirme={_statPlacements} temizleyen={_statClears} comboKaybı={_statResets} maxÇarpan=x{_statMaxMult:0.0}";

        bool FastPlaceOne()
        {
            var plan = PlanBest(Pool, Board, RotLock, 40, out int lines);
            if (plan == null) return false;
            var m = plan.Value;
            PrepareNextPool(m);
            float mb = _rc.Combo?.Multiplier ?? 1f; int sb = Score;
            mSelect.Invoke(_rc, new object[] { m.PoolIndex });
            fRot.SetValue(_rc, m.Rot);
            mPlace.Invoke(_rc, new object[] { m.Anchor });
            Stat(lines, mb, sb);
            return true;
        }

        IEnumerator PerformPlaceOne()
        {
            int lines = 0;
            var plan = Spec.Lose ? BadMove(out lines) : PlanBest(Pool, Board, RotLock, 60, out lines);
            if (plan == null)
            {
                // hamle yok: oyun kendi dead-pool mantığını çalıştırsın
                yield return Wait(0.5f);
                yield break;
            }
            var m = plan.Value;

            // havuzdan seçim gerekiyorsa slota git, bas
            if (m.PoolIndex != SelIdx)
            {
                Vector2 slot = PoolSlotScreen(m.PoolIndex);
                yield return MoveMouse(() => PoolSlotScreen(m.PoolIndex), MoveTime(slot) * 0.9f);
                yield return Wait(0.05f / Spec.Pace);
                yield return Click();
                float g = 0f;
                while (SelIdx != m.PoolIndex && g < 0.4f) { g += Time.unscaledDeltaTime; yield return null; }
                if (SelIdx != m.PoolIndex) mSelect.Invoke(_rc, new object[] { m.PoolIndex });
            }

            var piece = Pool[m.PoolIndex];
            var target = AnchorRefCell(piece, m.Rot, m.Anchor);
            Vector2 tgt = CellScreen(target.x, target.y);

            // döndürme fare yolda ilerlerken yapılır
            int steps = ((int)m.Rot - (int)CurRot + 4) & 3;
            float scrollDir = steps == 3 ? -1f : 1f;
            if (steps == 3) steps = 1;
            var move = StartCoroutine(MoveMouse(() => CellScreen(target.x, target.y), MoveTime(tgt)));
            yield return Wait(0.12f / Spec.Pace);
            for (int i = 0; i < steps; i++)
            {
                _scroll = 120f * scrollDir;
                yield return Wait(0.11f / Spec.Pace);
            }
            yield return move;
            if (CurRot != m.Rot) fRot.SetValue(_rc, m.Rot);

            // ghost görünsün; büyük temizlemelerde biraz daha bekle (izleyici okusun)
            float dwell = 0.10f + 0.06f * (float)_rng.NextDouble() + (lines >= 2 ? 0.22f : 0f);
            yield return Wait(dwell / Spec.Pace);

            // imlecin gerçekten hedef hücrede olduğundan emin ol; değilse yeniden otur
            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (!_rc.BoardView.TryGetMouseCellPoint(_rc.MainCamera, out var cp)) break;
                var under = new Vector2Int(Mathf.FloorToInt(cp.x), Mathf.FloorToInt(cp.y));
                if (under == target) break;
                Log($"imleç sapması: hedef {target} imleç {under} (deneme {attempt})");
                _mousePos = CellScreen(target.x, target.y);
                yield return Wait(0.08f);
            }

            PrepareNextPool(m);
            int before = Score;
            int poolCountBefore = Pool.Count;
            yield return Click();

            bool placed = Pool.Count != poolCountBefore || Score != before;
            if (!placed)
            {
                Log("tıklama yerleştirmedi → doğrudan DoPlace");
                mPlace.Invoke(_rc, new object[] { m.Anchor });
            }

            float post = lines > 0 ? 0.38f + 0.16f * lines : 0.06f;
            yield return Wait(post / Spec.Pace);
        }

        /// <summary>ResolvePlacementAnchor'ın tersi: parça kutusunun "orta" hücresi.</summary>
        static Vector2Int AnchorRefCell(PieceDefinition p, Rotation r, Vector2Int anchor)
        {
            var cells = p.GetCells(r);
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in cells)
            {
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
            }
            return anchor + new Vector2Int(Mathf.FloorToInt((minX + maxX + 1) * 0.5f), Mathf.FloorToInt((minY + maxY + 1) * 0.5f));
        }

        // ── Kaybetme / Game Over / Shop ──────────────────────────────────────

        /// <summary>Temizleme yapmayan, mevcut bloklara yaslanan doğal görünümlü bir hamle.</summary>
        Move? BadMove(out int lines)
        {
            lines = 0;
            var pool = Pool; var board = Board;
            var cp   = CurrentCombo();
            RefreshCardAwareness(); RigOffer();
            var root = SimBoard.From(board, _rc.Combo);
            int rots = RotLock ? 1 : 4;
            Move? best = null; float bestV = float.MinValue; int bestLines = 0;
            for (int pi = 0; pi < pool.Count; pi++)
            for (int r = 0; r < rots; r++)
            foreach (var (anchor, mask) in Masks(pool[pi], (Rotation)r))
            {
                if ((root.Filled & mask) != 0) continue;
                var b = root.Clone();
                b.Place(mask, PieceValue(pool[pi]), cp, out int l);
                // komşu dolu hücre sayısı: bloklara yaslanan hamleler doğal görünür
                int touch = PopCount(((mask << 1) | (mask >> 1) | (mask << 8) | (mask >> 8)) & root.Filled);
                // tahta çok dolunca temizlemeye izin ver: run "hamle yok" ile değil "şekil bitti" ile bitsin
                float clearW = PopCount(root.Filled) > 34 ? 40f * l : (l > 0 ? -1000f : 0f);
                float v = clearW + touch * 2f + (float)_rng.NextDouble() * 6f;
                if (pi != SelIdx) v -= 1.5f;
                if (v > bestV) { bestV = v; bestLines = l; best = new Move { PoolIndex = pi, Rot = (Rotation)r, Anchor = anchor }; }
            }
            lines = bestLines;
            return best;
        }

        /// <summary>İlk havuzu verilen şekil + iki ağırlıklı örnekle yeniden üret.</summary>
        void ForceFirstPool(string shapeId)
        {
            var shape = _rc.ShapeLibrary.Shapes.FirstOrDefault(s => s != null && s.Id == shapeId);
            if (shape == null || !shape.IsUnlocked) { Log($"ForceFirstShape: {shapeId} yok ya da kilitli"); return; }
            var shapes = WeightedShapes();
            ShapeSpawnService.DebugForcedShapes.Clear();
            ShapeSpawnService.DebugForcedShapes.Enqueue(SampleShape(shapes));
            ShapeSpawnService.DebugForcedShapes.Enqueue(shape);
            ShapeSpawnService.DebugForcedShapes.Enqueue(SampleShape(shapes));
            mGenPool.Invoke(_rc, new object[] { false });
            Log($"ilk havuz: {string.Join(",", Pool.Select(p => p.Id))}");
        }

        static Vector2 UiScreenPos(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
        }

        static bool OnScreen(Vector2 p) => p.x > 40 && p.y > 40 && p.x < Screen.width - 40 && p.y < Screen.height - 40;

        IEnumerator ClickUi(Button btn, float dwell = 0.25f)
        {
            var rt = (RectTransform)btn.transform;
            yield return MoveMouse(() => UiScreenPos(rt), MoveTime(UiScreenPos(rt)));
            yield return Wait(dwell / Spec.Pace);
            yield return Click();
            yield return Wait(0.1f);
        }

        static readonly FieldInfo fGoUpgrade = typeof(GameOverUI).GetField("_upgradeButton", F);
        static readonly FieldInfo fShopUnlock = typeof(ShapeShopCardView).GetField("_unlockBtn", F);
        static readonly FieldInfo fShopScore  = typeof(ShapeShopCardView).GetField("_scoreUpgradeBtn", F);

        /// <summary>
        /// Game Over panelini okut → Upgrade Now → Shape Shop'ta kartlara göz gezdir →
        /// kilitli bir şekli aç → bir skor upgrade'i al. Coin bakiyesi gerçek, tutarlı kalır.
        /// </summary>
        IEnumerator PostGameShop()
        {
            yield return Wait(3.0f);   // Game Over özeti okunsun
            var go  = GameOverUI.Instance;
            var upg = go != null ? fGoUpgrade.GetValue(go) as Button : null;
            if (upg == null || !upg.gameObject.activeInHierarchy) { Log("Upgrade Now görünmüyor (coin yetmiyor?)"); yield return Wait(2f); yield break; }
            yield return ClickUi(upg, 0.45f);
            yield return Wait(1.3f);

            var cards = FindObjectsByType<ShapeShopCardView>(FindObjectsSortMode.None)
                .Where(c => c.isActiveAndEnabled)
                .OrderByDescending(c => UiScreenPos((RectTransform)c.transform).y)
                .ThenBy(c => UiScreenPos((RectTransform)c.transform).x)
                .ToList();
            Log($"shop kartları: {cards.Count}");

            // göz gezdir: ekrandaki iki karta uğra
            foreach (var c in cards.Where(c => OnScreen(UiScreenPos((RectTransform)c.transform))).Take(2))
            {
                var rt = (RectTransform)c.transform;
                yield return MoveMouse(() => UiScreenPos(rt), 0.4f);
                yield return Wait(0.45f / Spec.Pace);
            }

            Button Active(ShapeShopCardView c, FieldInfo fi)
            {
                var b = fi.GetValue(c) as Button;
                return b != null && b.gameObject.activeInHierarchy && b.interactable ? b : null;
            }

            // önce görünen bir şeklin puanını yükselt
            var scoreCard = cards.FirstOrDefault(c => fShopUnlock.GetValue(c) is Button ub0 && !ub0.gameObject.activeInHierarchy
                && Active(c, fShopScore) != null && OnScreen(UiScreenPos((RectTransform)Active(c, fShopScore).transform)));
            if (scoreCard != null)
            {
                Log($"shop: skor upgrade {scoreCard.name}");
                yield return ClickUi(Active(scoreCard, fShopScore), 0.35f);
                yield return Wait(1.4f);
            }

            var unlockCard = cards.FirstOrDefault(c => Active(c, fShopUnlock) != null);
            if (unlockCard != null)
            {
                var ub = Active(unlockCard, fShopUnlock);
                // ekran dışındaysa listeyi kaydır (tekerlekle kaydırmış gibi)
                var scroll = unlockCard.GetComponentInParent<ScrollRect>();
                if (scroll != null && !OnScreen(UiScreenPos((RectTransform)ub.transform)))
                {
                    yield return MoveMouse(() => new Vector2(Screen.width * 0.5f, Screen.height * 0.45f), 0.35f);
                    float from = scroll.verticalNormalizedPosition, t = 0f;
                    while (t < 0.9f && !OnScreen(UiScreenPos((RectTransform)ub.transform) + new Vector2(0, -120f)))
                    {
                        t += Time.unscaledDeltaTime;
                        scroll.verticalNormalizedPosition = Mathf.Lerp(from, 0f, Mathf.SmoothStep(0f, 1f, t / 0.9f));
                        yield return null;
                    }
                    yield return Wait(0.3f);
                }
                var rt = (RectTransform)unlockCard.transform;
                yield return MoveMouse(() => UiScreenPos(rt), 0.4f);
                yield return Wait(0.5f / Spec.Pace);
                Log($"shop: kilit açılıyor {unlockCard.name}");
                yield return ClickUi(ub, 0.3f);
                yield return Wait(2.2f);
            }
            else Log("shop: açılabilir kilitli şekil yok");

            yield return Wait(1.0f);
        }

        // ── Ana menü: Upgrades ───────────────────────────────────────────────

        static readonly FieldInfo fUcBuy   = typeof(UpgradesController).GetField("_buyButton", F);

        IEnumerator MenuUpgradesTake()
        {
            // menü metinleri kültür değişmeden çizildi ("28.450") — sahneyi yeniden yükle
            DontDestroyOnLoad(gameObject);
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            yield return Wait(2.0f);
            SetupMouse();
            yield return Wait(1.0f);
            CreateCursor();
            _mousePos = new Vector2(Screen.width * 0.5f, Screen.height * 0.4f);

            if (Spec.Record) { _running = true; TakeStarted?.Invoke(Spec.Name); }
            Log("KAYIT BAŞLADI (menü) coin=" + CoinWallet.Instance?.Balance);
            yield return Wait(1.8f);

            var uc = FindObjectsByType<UpgradesController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (uc == null) { Log("UpgradesController yok"); yield break; }
            if (!uc.gameObject.activeInHierarchy || (uc.GetComponentInParent<CanvasGroup>()?.alpha ?? 1f) < 0.5f)
            {
                // menü düğmesi MenuButtonView ("BtnUpgrades"), Button değil — işaretçiyle tıkla
                var btnGo = GameObject.Find("BtnUpgrades");
                if (btnGo != null)
                {
                    var brt = (RectTransform)btnGo.transform;
                    yield return MoveMouse(() => UiScreenPos(brt), 0.5f);
                    yield return Wait(0.35f);
                    yield return Click();
                    yield return Wait(0.6f);
                }
                var cg = uc.GetComponentInParent<CanvasGroup>(true);
                if (cg == null || cg.alpha < 0.5f || !uc.gameObject.activeInHierarchy)
                {
                    var pm = FindFirstObjectByType<PanelManager>();
                    Log("menü düğmesi paneli açmadı → PanelManager.OpenPanel");
                    pm?.OpenPanel(cg);
                }
                yield return Wait(1.4f);
            }

            var buy = fUcBuy.GetValue(uc) as Button;
            foreach (var id in Spec.MenuUpgrades)
            {
                var slot = FindObjectsByType<UpgradeSlotView>(FindObjectsSortMode.None).FirstOrDefault(s => s.Upgrade != null && s.Upgrade.Id == id);
                if (slot == null) { Log("slot yok: " + id); continue; }
                var srt = (RectTransform)slot.transform;
                yield return MoveMouse(() => UiScreenPos(srt), 0.45f);
                yield return Wait(0.35f / Spec.Pace);
                yield return Click();
                yield return Wait(1.1f / Spec.Pace);   // detay okunsun
                if (buy != null && buy.interactable)
                {
                    int before = CoinWallet.Instance?.Balance ?? 0;
                    yield return ClickUi(buy, 0.35f);
                    Log($"upgrade alındı: {id} coin {before} → {CoinWallet.Instance?.Balance} lvl={UpgradeRegistry.Instance?.GetLevel(id)}");
                    yield return Wait(1.5f / Spec.Pace);
                }
                else Log("satın alınamıyor: " + id);
            }
            yield return Wait(Spec.TailSeconds);
            Log("KAYIT BİTTİ (menü)");
            if (_running) { _running = false; TakeEnded?.Invoke(); }
        }

        /// <summary>Tahtadaki her karo görünür boyutta mı (intro / FX ölçek hatasına karşı).</summary>
        int CountBrokenTiles()
        {
            var bv = _rc.BoardView; int broken = 0;
            float target = bv.CellSize * 0.95f;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                var t = bv.GetTile(x, y);
                if (t == null || t.transform.localScale.x < target * 0.8f) broken++;
            }
            return broken;
        }

        // ── Havuz yönlendirme ────────────────────────────────────────────────

        /// <summary>
        /// Yerleştirilecek parça havuzun sonuncusuysa, yerleştirmeden sonraki tahtaya
        /// göre bir sonraki üçlüyü seç ve kuyruğa koy — GenerateNewPool onu çeker.
        /// </summary>
        void PrepareNextPool(Move m)
        {
            ShapeSpawnService.DebugForcedShapes.Clear();
            if (Spec.Lose) return;   // kaybeden run: havuzlar gerçek rastgele
            if (Pool.Count != 1) return;

            var cp = CurrentCombo();
            var sb = SimBoard.From(Board, _rc.Combo);
            var pm = Masks(Pool[m.PoolIndex], m.Rot).First(x => x.anchor == m.Anchor).mask;
            sb.Place(pm, PieceValue(Pool[m.PoolIndex]), cp, out _);

            var shapes = WeightedShapes();
            if (shapes.Count == 0) return;

            ShapeSO[] best = null; float bestVal = float.MinValue;
            int samples = 40;
            for (int s = 0; s < samples; s++)
            {
                var triple = new ShapeSO[3];
                for (int i = 0; i < 3; i++) triple[i] = SampleShape(shapes);
                var pieces = triple.Select(PieceFactory.Create).ToList();
                float v = BeamValue(pieces, sb, false, 10, cp);
                v += (float)_rng.NextDouble() * 40f;              // biraz çeşitlilik
                if (v > bestVal) { bestVal = v; best = triple; }
            }
            if (best != null) foreach (var s in best) ShapeSpawnService.DebugForcedShapes.Enqueue(s);
        }

        List<(ShapeSO shape, int w)> WeightedShapes()
        {
            var list = new List<(ShapeSO, int)>();
            var lib = _rc.ShapeLibrary;
            var up  = ShapeUpgradeRegistry.Instance;
            var cr  = ShapeCardEffectRegistry.Instance;
            foreach (var s in lib.Shapes)
            {
                if (s == null || !s.IsUnlocked) continue;
                int w = up != null ? up.GetEffectiveWeight(s.Id, s.BaseWeight) : s.BaseWeight;
                if (cr != null && cr.HasAnyEffect(s.Id)) w = Mathf.Max(1, w + Mathf.RoundToInt(cr.GetWeightDelta(s.Id)));
                if (w > 0) list.Add((s, w));
            }
            return list;
        }

        ShapeSO SampleShape(List<(ShapeSO shape, int w)> list)
        {
            int total = 0; foreach (var e in list) total += e.w;
            int roll = _rng.Next(total), cum = 0;
            foreach (var e in list) { cum += e.w; if (roll < cum) return e.shape; }
            return list[list.Count - 1].shape;
        }

        // ── Planlayıcı (8×8 bitboard) ────────────────────────────────────────

        public struct Move { public int PoolIndex; public Rotation Rot; public Vector2Int Anchor; }

        /// <summary>Combo parametreleri — her planlamada gerçek ComboSystem'den okunur.</summary>
        struct ComboParams { public int MaxCharge; public float PerClear, AtMax, Base; }

        sealed class SimBoard
        {
            public ulong Filled;   // dolu + kalıcı engel (dead zone / phantom)
            public ulong Fixed;    // temizlenmeyen hücreler
            public int   Charges;
            public float Mult;

            // Hücre değerleri tembel kopyalanır: çocuk düğüm yalnızca genişletilirken
            // kendi dizisini üretir (beam'e girmeyen on binlerce aday kopyalanmaz).
            float[]  _v;
            SimBoard _parent;
            ulong    _pMask, _pClear;
            float    _pVal;

            public static SimBoard From(BoardModel b, ComboSystem combo)
            {
                var s = new SimBoard { Charges = combo?.Charges ?? 0, Mult = combo?.Multiplier ?? 1f, _v = new float[64] };
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    int   i   = y * 8 + x;
                    ulong bit = 1UL << i;
                    if (b.IsFilled(x, y)) s.Filled |= bit;
                    if (b.IsDeadZone(x, y) || b.IsPhantom(x, y)) s.Fixed |= bit;
                    s._v[i] = b.GetTileValue(x, y);
                }
                return s;
            }

            public SimBoard Clone() => new SimBoard { Filled = Filled, Fixed = Fixed, Charges = Charges, Mult = Mult, _parent = this };

            float[] Values()
            {
                if (_v != null) return _v;
                var v = (float[])_parent.Values().Clone();
                for (int i = 0; i < 64; i++)
                {
                    ulong bit = 1UL << i;
                    if ((_pMask & bit) != 0) v[i] = _pVal;
                    if ((_pClear & bit) != 0 && (Fixed & bit) == 0) v[i] = 0f;
                }
                _v = v; _parent = null;
                return v;
            }

            /// <summary>Yerleştir + temizle + combo (yalnızca Clone() sonrası). Dönen değer:
            /// kazanılan puanın tahmini (temizlenen hücre değerleri × çarpan) ya da combo kaybı cezası.</summary>
            public ulong LastClear;

            public float Place(ulong mask, float pieceVal, ComboParams cp, out int lines)
            {
                var pv = _parent != null ? _parent.Values() : Values();
                Filled |= mask;
                ulong clear = 0; lines = 0;
                for (int i = 0; i < 8; i++)
                {
                    if ((Filled & Row[i]) == Row[i]) { clear |= Row[i]; lines++; }
                    if ((Filled & Col[i]) == Col[i]) { clear |= Col[i]; lines++; }
                }
                Filled &= ~(clear & ~Fixed);
                LastClear = clear;

                if (_parent != null) { _pMask = mask; _pVal = pieceVal; _pClear = clear; }
                else
                {
                    for (int i = 0; i < 64; i++)
                    {
                        ulong bit = 1UL << i;
                        if ((mask & bit) != 0) _v[i] = pieceVal;
                    }
                }

                if (lines > 0)
                {
                    float sum = 0f;
                    for (ulong c = clear; c != 0; c &= c - 1)
                    {
                        int i = TrailingZeros(c);
                        sum += (mask & (1UL << i)) != 0 ? pieceVal : pv[i];
                    }
                    if (_parent == null)
                        for (int i = 0; i < 64; i++) if ((clear & (1UL << i)) != 0 && (Fixed & (1UL << i)) == 0) _v[i] = 0f;

                    float gained = sum * Mult * 0.3f;   // puan, çarpan artmadan önceki değerle
                    for (int i = 0; i < lines; i++)
                    {
                        if (Charges < cp.MaxCharge) Charges++;
                        Mult += Charges >= cp.MaxCharge ? cp.AtMax : cp.PerClear;
                    }
                    return gained;
                }
                if (Charges > 0)
                {
                    Charges--;
                    if (Charges == 0)
                    {
                        float lost = Mult - cp.Base;
                        Mult = cp.Base;
                        return -(40f + lost * 260f);    // combo düştü: biriken çarpan gitti
                    }
                }
                return 0f;
            }

            static int TrailingZeros(ulong x)
            {
                int n = 0;
                while ((x & 1UL) == 0) { x >>= 1; n++; }
                return n;
            }
        }

        static float PieceValue(PieceDefinition p) =>
            p.TileValue + (ShapeCardEffectRegistry.Instance?.GetScoreBonus(p.Id) ?? 0f);

        static readonly ulong[] Row = Enumerable.Range(0, 8).Select(y => 0xFFUL << (y * 8)).ToArray();
        static readonly ulong[] Col = Enumerable.Range(0, 8).Select(x => 0x0101010101010101UL << x).ToArray();

        readonly Dictionary<(string, Rotation), List<(Vector2Int anchor, ulong mask)>> _maskCache = new();

        List<(Vector2Int anchor, ulong mask)> Masks(PieceDefinition p, Rotation r)
        {
            if (_maskCache.TryGetValue((p.Id, r), out var cached)) return cached;
            var cells = p.GetCells(r);
            var list = new List<(Vector2Int, ulong)>();
            for (int ay = -8; ay < 8; ay++)
            for (int ax = -8; ax < 8; ax++)
            {
                ulong m = 0; bool ok = true;
                foreach (var c in cells)
                {
                    int x = ax + c.x, y = ay + c.y;
                    if (x < 0 || y < 0 || x >= 8 || y >= 8) { ok = false; break; }
                    m |= 1UL << (y * 8 + x);
                }
                if (ok) list.Add((new Vector2Int(ax, ay), m));
            }
            _maskCache[(p.Id, r)] = list;
            return list;
        }

        sealed class Node
        {
            public SimBoard B; public int Used; public float Acc; public Move First; public bool HasFirst;
        }

        // ── Kart etkisi farkındalığı + sunulacak kart planı ────────────────────

        static readonly FieldInfo fCardState = Fld("_cardState");
        static readonly FieldInfo fCardPool  = Fld("CardPool");
        const ulong CornerBits = (1UL << 0) | (1UL << 7) | (1UL << 56) | (1UL << 63);
        ulong _neonBits;
        bool  _cornerStone;
        List<CardSO> _origCardPool;

        void RefreshCardAwareness()
        {
            var cs = fCardState.GetValue(_rc) as RunCardState;
            _neonBits = 0; _cornerStone = false;
            if (cs == null) return;
            if (cs.HasNeonCable)
            {
                if (cs.NeonCableAValid) _neonBits |= 1UL << (cs.NeonCablePositionA.y * 8 + cs.NeonCablePositionA.x);
                if (cs.NeonCableBValid) _neonBits |= 1UL << (cs.NeonCablePositionB.y * 8 + cs.NeonCablePositionB.x);
            }
            _cornerStone = cs.HasCornerStone;
        }

        /// <summary>
        /// OfferPlan: sıradaki kart seçiminde istenen kart gerçekten sunulsun diye kart
        /// havuzu o seçim için [istenen + iki rastgele] olarak daraltılır. Seçim yine
        /// gerçek ekrandan yapılır; eldeki kart sayısı = ulaşılan milestone sayısı.
        /// </summary>
        void RigOffer()
        {
            if (Spec.OfferPlan == null || Spec.OfferPlan.Length == 0) return;
            if (_origCardPool == null) _origCardPool = new List<CardSO>((List<CardSO>)fCardPool.GetValue(_rc));
            if (_cardsPickedTotal >= Spec.OfferPlan.Length) { fCardPool.SetValue(_rc, _origCardPool); return; }
            string want = Spec.OfferPlan[_cardsPickedTotal];
            var planned = _origCardPool.FirstOrDefault(c => c != null && c.Id == want);
            if (planned == null) return;
            var owned = CardInventoryUI.Instance != null ? new HashSet<string>(CardInventoryUI.Instance.GetSelectedCardIds()) : new HashSet<string>();
            var others = _origCardPool.Where(c => c != null && c != planned && c.IsUnlocked && !c.IsShapeCard && !owned.Contains(c.Id))
                                      .OrderBy(_ => _rng.Next()).Take(2);
            fCardPool.SetValue(_rc, new List<CardSO> { planned }.Concat(others).ToList());
        }

        ComboParams CurrentCombo()
        {
            var c = _rc.Combo;
            return new ComboParams
            {
                MaxCharge = c?.MaxCharge ?? 3,
                PerClear  = c?.BonusPerClear ?? 0.1f,
                AtMax     = c?.BonusAtMaxCharge ?? 0.2f,
                Base      = c?.BaseMultiplier ?? 1f,
            };
        }

        Move? PlanBest(List<PieceDefinition> pool, BoardModel board, bool rotLock, int beam, out int firstLines)
        {
            firstLines = 0;
            var cp   = CurrentCombo();
            RefreshCardAwareness(); RigOffer();
            var root = new Node { B = SimBoard.From(board, _rc.Combo) };
            Beam(pool, root, rotLock, beam, cp, out var bestNode, preferIndex: SelIdx);
            if (bestNode == null || !bestNode.HasFirst) return null;
            // ilk hamlenin temizlediği satır sayısı (bekleme süresi için)
            var piece = pool[bestNode.First.PoolIndex];
            var mask = Masks(piece, bestNode.First.Rot).First(x => x.anchor == bestNode.First.Anchor).mask;
            var sb = root.B.Clone(); sb.Place(mask, PieceValue(piece), cp, out firstLines);
            return bestNode.First;
        }

        float BeamValue(List<PieceDefinition> pool, SimBoard b, bool rotLock, int beam, ComboParams cp)
        {
            Beam(pool, new Node { B = b.Clone() }, rotLock, beam, cp, out var best, -1);
            return best?.Acc ?? -5000f;
        }

        float Beam(List<PieceDefinition> pool, Node root, bool rotLock, int beam, ComboParams cp, out Node best, int preferIndex)
        {
            var frontier = new List<Node> { root };
            int rots = rotLock ? 1 : 4;
            int fullUsed = (1 << pool.Count) - 1;
            // gösteri ağırlığı yalnızca kayıtta; ileri sarma güvenli oynasın
            float spect = Spec != null && Phase == "perform" ? Spec.Spectacle : 1.5f;

            for (int depth = 0; depth < pool.Count; depth++)
            {
                var next = new List<Node>(frontier.Count * 64);
                foreach (var n in frontier)
                {
                    if (n.Used == fullUsed) { next.Add(n); continue; }
                    bool expanded = false;
                    for (int pi = 0; pi < pool.Count; pi++)
                    {
                        if ((n.Used & (1 << pi)) != 0) continue;
                        // aynı şekil tekrarını iki kez açma
                        bool dup = false;
                        for (int pj = 0; pj < pi; pj++)
                            if ((n.Used & (1 << pj)) == 0 && pool[pj].Id == pool[pi].Id) { dup = true; break; }
                        if (dup) continue;

                        for (int r = 0; r < rots; r++)
                        {
                            foreach (var (anchor, mask) in Masks(pool[pi], (Rotation)r))
                            {
                                if ((n.B.Filled & mask) != 0) continue;
                                var b = n.B.Clone();
                                float step = b.Place(mask, PieceValue(pool[pi]), cp, out int lines);
                                // gösteri: çok satırlı temizleme ekranda daha iyi okunur
                                if (lines >= 2) step += 30f * spect * lines * (lines - 1);
                                // kart etkisini tetikleyen hamleler (trailer: etki ekranda görünsün)
                                if ((b.LastClear & _neonBits) != 0) step += 260f * spect;
                                if (_cornerStone) step += PopCount(mask & CornerBits) * 25f;
                                if (depth == 0 && preferIndex >= 0 && pi != preferIndex) step -= 3f;
                                next.Add(new Node
                                {
                                    B = b, Used = n.Used | (1 << pi), Acc = n.Acc + step,
                                    First = n.HasFirst ? n.First : new Move { PoolIndex = pi, Rot = (Rotation)r, Anchor = anchor },
                                    HasFirst = true,
                                });
                                expanded = true;
                            }
                        }
                    }
                    if (!expanded)
                    {
                        // kalan parça sığmıyor: çok ağır ceza ama dalı koru
                        next.Add(new Node { B = n.B, Used = fullUsed, Acc = n.Acc - 3000f, First = n.First, HasFirst = n.HasFirst });
                    }
                }
                if (next.Count == 0) break;
                frontier = next.OrderByDescending(c => c.Acc + Eval(c.B, cp)).Take(beam).ToList();
            }

            best = null; float bestV = float.MinValue;
            foreach (var n in frontier)
            {
                float v = n.Acc + Eval(n.B, cp);
                if (v > bestV) { bestV = v; best = n; }
            }
            if (best != null) best = new Node { B = best.B, Acc = bestV, First = best.First, HasFirst = best.HasFirst };
            return bestV;
        }

        static bool Fits(ulong filled, int w, int h)
        {
            for (int ay = 0; ay + h <= 8; ay++)
            for (int ax = 0; ax + w <= 8; ax++)
            {
                ulong m = 0;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) m |= 1UL << ((ay + y) * 8 + ax + x);
                if ((filled & m) == 0) return true;
            }
            return false;
        }

        static float Eval(SimBoard b, ComboParams cp)
        {
            ulong f = b.Filled;
            int filled = PopCount(f);
            float v = (64 - filled) * 3f;

            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                if ((f & (1UL << (y * 8 + x))) != 0) continue;
                int blocked = 0;
                if (x == 0 || (f & (1UL << (y * 8 + x - 1))) != 0) blocked++;
                if (x == 7 || (f & (1UL << (y * 8 + x + 1))) != 0) blocked++;
                if (y == 0 || (f & (1UL << ((y - 1) * 8 + x))) != 0) blocked++;
                if (y == 7 || (f & (1UL << ((y + 1) * 8 + x))) != 0) blocked++;
                if (blocked == 4) v -= 28f;
                else if (blocked == 3) v -= 7f;
            }

            // pürüzlülük: satır/sütun boyunca dolu↔boş geçişleri
            int trans = 0;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 7; x++)
            {
                bool a = (f & (1UL << (y * 8 + x))) != 0, c = (f & (1UL << (y * 8 + x + 1))) != 0;
                if (a != c) trans++;
                bool d = (f & (1UL << (x * 8 + y))) != 0, e = (f & (1UL << ((x + 1) * 8 + y))) != 0;
                if (d != e) trans++;
            }
            v -= trans * 2.2f;

            // hayatta kalma: büyük şekiller sığıyor mu
            if (!Fits(f, 3, 3)) v -= 120f;
            if (!Fits(f, 5, 1)) v -= 50f;
            if (!Fits(f, 1, 5)) v -= 50f;
            if (!Fits(f, 2, 2)) v -= 300f;

            // kuruluma ödül: 6–7 dolu satır/sütun (yaklaşan büyük temizleme)
            for (int i = 0; i < 8; i++)
            {
                int rc = PopCount(f & Row[i]), cc = PopCount(f & Col[i]);
                if (rc >= 6 && rc < 8) v += 5f;
                if (cc >= 6 && cc < 8) v += 5f;
            }

            // combo tamponu: şarj, temizlemesiz hamlelere karşı sigorta — çarpan büyüdükçe değerli
            v += b.Charges * (12f + (b.Mult - cp.Base) * 45f);
            return v;
        }

        static int PopCount(ulong x)
        {
            x -= (x >> 1) & 0x5555555555555555UL;
            x = (x & 0x3333333333333333UL) + ((x >> 2) & 0x3333333333333333UL);
            x = (x + (x >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((x * 0x0101010101010101UL) >> 56);
        }
    }
}
#endif
