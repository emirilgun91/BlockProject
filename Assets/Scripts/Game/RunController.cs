using RogueBlockBlast.Core.Localization;
using System;
using System.Collections.Generic;
using DG.Tweening;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using RogueBlockBlast.UI;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using System.Linq;


namespace RogueBlockBlast.Game
{
    public sealed class RunController : MonoBehaviour
    {
        [Header("Content")]
        public ShapeLibrarySO ShapeLibrary;

        [Header("View")]
        public BoardView BoardView;
        public Camera    MainCamera;

        [Header("Config")]
        public int Width  = 8;
        public int Height = 8;

        [Header("UI")]
        [SerializeField] private PoolView          PoolView;
        [SerializeField] private ScoreView         ScoreView;
        [SerializeField] private ComboView         ComboView;
        [SerializeField] private MilestoneView     MilestoneView;

        [Header("UI — Reroll")]
        [SerializeField] private PoolRerollButton _poolRerollButton;

        private int _poolRerollsRemaining  = 0;
        private int _cardRerollsRemaining  = 0;

        [SerializeField] private List<CardSO> CardPool;
        private float _globalScoreMultiplier  = 1f;
        private int   _coinBonusPerMilestone  = 0;

        [Header("Upgrades")]
        [SerializeField] private UpgradeLibrarySO _upgradeLibrary;

        [SerializeField] private AudioClip mainLoopMusic;
        [Header("Milestone")]
        [SerializeField] private MilestoneConfigSO MilestoneConfig;

        [Header("Demo")]
        [Tooltip("AÇIK: bu skor eşiğine sahip milestone'a ulaşınca demo bitiş paneli " +
                 "açılır ve run sona erer. Tam sürümde kapatın.")]
        [SerializeField] private bool _demoBuild = true;

        [Tooltip("Demoyu bitiren milestone'un skor eşiği. MilestoneConfig'te " +
                 "20000 = 'Stage 6'. Eşik üzerinden bakılır çünkü etiketler " +
                 "(Stage 8'den sonra Stage 10'a atlıyor) güvenilir bir sıra vermiyor.")]
        [SerializeField] private int _demoEndScoreThreshold = 20000;

        // ── Core systems ─────────────────────────────────────────────────────
        private BoardModel      _board;
        private RunModel        _run;
        private ScoreSystem     _scoreSystem     = new ScoreSystem();
        private ComboSystem     _comboSystem     = new ComboSystem();

        /// <summary>
        /// Combo sistemi — sahne atmosferi gibi görsel bileşenler duruma abone
        /// olabilsin diye açık. Bu örnek run boyunca değişmez (yalnızca
        /// <c>Reset()</c> edilir), o yüzden bir kez abone olmak yeterlidir.
        /// </summary>
        public ComboSystem Combo => _comboSystem;

        /// <summary>
        /// Milestone sistemi — UI süsleri kalan şekil sayısı gibi duruma abone
        /// olabilsin diye açık. <b>Start() içinde kurulur</b>, o yüzden erken
        /// erişimde null olabilir; abone olan taraf tembel bağlanmalıdır.
        /// </summary>
        public MilestoneSystem Milestone => _milestoneSystem;
        private MilestoneSystem _milestoneSystem;

        // ── Card state ───────────────────────────────────────────────────────
        private readonly RunCardState _cardState = new RunCardState();

        // ── State ────────────────────────────────────────────────────────────
        private int _score = 0;
        private int _coins = 0;
        private float _maxComboReached = 1f;
        private List<PieceDefinition> _piecePool         = new List<PieceDefinition>(3);
        private int                   _selectedPoolIndex = -1;
        private PieceDefinition       _currentPiece;
        private Rotation              _currentRot        = Rotation.R0;
        private int _upgradeRevivesUsed = 0;
        private readonly HashSet<Vector2Int> _ghost = new();
        // Ghost hücrelerinin pozisyon bonusu — her frame yeniden doldurulur (alokasyon yok)
        private readonly Dictionary<Vector2Int, float> _ghostPositionBonus = new();
        private bool _poolDirty = true;

        private int       _freeDeadPoolReroll = 1;
        private int       _cardDeadPoolReroll = 0;
        private const int CardRerollCost      = 300;

        [SerializeField] private LineClearVFX LineClearVFX;
        [Header ("SFX")]
        [SerializeField] private AudioClip GameOverSFX;
        [SerializeField] private AudioClip LineClearSFX;
        [SerializeField] private AudioClip PlacePiece;

        // ── Unity ────────────────────────────────────────────────────────────
        private void Start()
        {
            if (PoolView != null) PoolView.OnSlotClicked = SelectPool;
            UpgradeRegistry.Instance?.Init(_upgradeLibrary);
            ShapeUpgradeRegistry.Instance.Load(ShapeLibrary.Shapes);
            int poolBonus = Mathf.RoundToInt(
                UpgradeRegistry.Instance?.GetEffect(
                    _upgradeLibrary?.Get("upgrade_pool_capacity")) ?? 0f
            );
            _milestoneSystem = new MilestoneSystem(MilestoneConfig, MilestoneConfig.PoolLimit + poolBonus);
            _milestoneSystem.OnMilestoneReached   += HandleMilestoneReached;
            _milestoneSystem.OnPoolLimitExhausted += HandlePoolLimitExhausted;
            MilestoneView?.Bind(_milestoneSystem);

            ComboView?.Bind(_comboSystem);
            _comboSystem.OnComboReset += HandleHyperfocusPenalty;

            if (mainLoopMusic != null)
                AudioManager.Instance.PlayMusic(mainLoopMusic);

            if (UnlockRegistry.Instance != null)
            {
                var shapeIds = ShapeLibrary.Shapes
                    .Where(s => s != null)
                    .Select(s => s.Id);
                var cardIds = CardPool
                    .Where(c => c != null)
                    .Select(c => c.Id);
                var milestoneLabels = MilestoneConfig.Milestones
                    .Select(m => m.Label);

                UnlockRegistry.Instance.Init(shapeIds, cardIds);
                UnlockRegistry.Instance.InitMilestones(milestoneLabels);
                var lockedShapeIds = ShapeLibrary.Shapes
                    .Where(s => s != null && s.LockedByDefault)
                    .Select(s => s.Id);
                UnlockRegistry.Instance.RegisterLockedShapes(lockedShapeIds);
            }

            NewRun();
        }

        private void OnDestroy()
        {
            _comboSystem.OnComboReset -= HandleHyperfocusPenalty;

            if (_milestoneSystem != null)
            {
                _milestoneSystem.OnMilestoneReached   -= HandleMilestoneReached;
                _milestoneSystem.OnPoolLimitExhausted -= HandlePoolLimitExhausted;
            }
        }

        private void Update()
        {
            if (_board == null || _currentPiece == null) return;
            if (!GameStateController.InputAllowed) return;
            bool canRotate = !(_cardState.HasFirstPicks &&
                               _cardState.FirstPicksUsedThisMilestone < _cardState.FirstPicksFreeCount);

            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.wasPressedThisFrame && canRotate) _currentRot = PrevRot(_currentRot);
                if (Keyboard.current.eKey.wasPressedThisFrame && canRotate) _currentRot = NextRot(_currentRot);

                if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectPool(0);
                if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectPool(1);
                if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectPool(2);
            }

            if (Mouse.current != null && canRotate)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll > 0f) _currentRot = NextRot(_currentRot);
                else if (scroll < 0f) _currentRot = PrevRot(_currentRot);
            }

            _ghost.Clear();
            _ghostPositionBonus.Clear();
            if (BoardView.IsMouseOverBoard(MainCamera))
            {
                var cell = BoardView.TryGetClampedCellUnderMouse(MainCamera, _currentPiece, _currentRot);

                if (cell.HasValue)
                {
                    var  anchor   = cell.Value;
                    bool canPlace = PlacementSystem.CanPlace(_board, _currentPiece, anchor, _currentRot);

                    var cells = _currentPiece.GetCells(_currentRot);
                    for (int i = 0; i < cells.Count; i++)
                    {
                        var p = anchor + cells[i];
                        _ghost.Add(p);

                        // Skorlamayla aynı fonksiyon — ghost hareket ettikçe canlı güncellenir
                        if (HasAnyPositionBonus)
                        {
                            float b = GetPositionBonusForCell(p);
                            if (b > 0f) _ghostPositionBonus[p] = b;
                        }
                    }

                    if (Mouse.current != null &&
                        Mouse.current.leftButton.wasPressedThisFrame &&
                        canPlace)
                    {
                        DoPlace(anchor);
                    }
                }
            }

            float ghostTileValue = _currentPiece?.TileValue ?? 0f;
            if (_currentPiece != null)
            {
                float bonus = ShapeCardEffectRegistry.Instance?.GetScoreBonus(_currentPiece.Id) ?? 0f;
                ghostTileValue += bonus;
            }
            BoardView.Render(_board, _ghost, ghostTileValue, _ghostPositionBonus, _staticPositionBonus);

            // ── Kilitlenme güvenlik ağı ───────────────────────────────────────
            // Hamle kontrolü normalde sadece parça yerleştirildiğinde yapılır. Tahtayı
            // DoPlace dışında değiştiren bir şey (yarığın hücre kapatması, phantom
            // taşınması, ileride eklenecek kartlar) son hamleyi yok ederse oyun
            // hamlesiz kalır ama game over tetiklenmez. Burada periyodik olarak
            // doğruluyoruz — hesap ucuz, kilitlenme pahalı.
            CheckForSoftLock();

            if (_poolDirty && PoolView != null)
            {
                PoolView.Bind(_piecePool, _selectedPoolIndex);
                _poolDirty = false;
            }
        }

        // ── Pool ─────────────────────────────────────────────────────────────
        private void SelectPool(int index)
        {
            if (index < 0 || index >= _piecePool.Count) return;
            _selectedPoolIndex = index;
            _currentPiece      = _piecePool[index];
            _currentRot        = Rotation.R0;
            _poolDirty         = true;
        }

        // ── Placement ────────────────────────────────────────────────────────
        private void DoPlace(Vector2Int anchor)
        {
            if (!PlacementSystem.CanPlace(_board, _currentPiece, anchor, _currentRot))
                return;

            // ── Card pre-checks (evaluated before board state changes) ────────
            bool isGhostDrop = _cardState.HasGhostDrop &&
                               _cardState.GhostDropUsesThisMilestone < _cardState.GhostDropMaxUses &&
                               IsGhostDropPlacement(_board, _currentPiece, anchor, _currentRot);

            bool isFirstPick = _cardState.HasFirstPicks &&
                               _cardState.FirstPicksUsedThisMilestone < _cardState.FirstPicksFreeCount;

            bool skipPoolConsume = isGhostDrop || isFirstPick;

            // ── Safe Zone: check coverage before board changes ────────────────
            if (_cardState.HasSafeZone && _cardState.SafeZoneActive)
                CheckSafeZoneCoverage(anchor);

            // ── Place ─────────────────────────────────────────────────────────
            // Shape kartı bonusu tile'ın KENDİ değerine yazılır — ghost önizlemesinde
            // gösterilen sayı ile tahtadaki sayı ve line clear'da uçan sayı aynı olur.
            float shapeBonusPerTile = ShapeCardEffectRegistry.Instance?.GetScoreBonus(_currentPiece.Id) ?? 0f;
            float effectiveTileValue = _currentPiece.TileValue + shapeBonusPerTile;
            PlacementSystem.Place(_board, _currentPiece, anchor, _currentRot, effectiveTileValue);

            // Corner Stone / Center Base bonusu da tile'a yazılır. Bu bonuslar hücre
            // bazlıdır (yalnızca köşe / merkez hücreleri alır), o yüzden yerleştirmeden
            // sonra ilgili hücrelerin değeri güncellenir.
            // Kural (CLAUDE.md): puan YALNIZCA satır temizlenince kazanılır — bu yüzden
            // bonus skora doğrudan eklenmez, tile'ın değerine girip clear'da ödenir.
            ApplyPositionBonusToPlacedCells(anchor, effectiveTileValue);
            FrameFeedbackController.Instance?.OnDrop(_currentPiece.BlockColor);
            BoardFX.PlayPlaceFX(BoardView, _currentPiece, anchor, _currentRot);
            AudioManager.Instance.PlaySFX(PlacePiece);
            RunStatsTracker.Instance?.RecordPlacement();

            // ── Line clear ────────────────────────────────────────────────────
            var (cleared, tileValueSum, clearedRows, clearedCols, snapshots) =
                LineClearSystem.ClearLines(_board);

            if (cleared > 0)
            {
                FrameFeedbackController.Instance?.OnLineClear(cleared);
                if (LineClearVFX != null)
                {
                    LineClearVFX.Play(
                        clearedRows, clearedCols, snapshots,
                        BoardView, _board.Width, _board.Height,
                        onAllArrived: () => ScoreView?.PunchScore()
                    );
                }
                else
                {
                    BoardFX.PlayLineClearFX(BoardView, _board.Width, _board.Height, clearedRows, clearedCols);
                }
                FrameFeedbackController.Instance?.OnDrop(_currentPiece.BlockColor);
                RunStatsTracker.Instance?.RecordClear(cleared, 0);
                _comboSystem.OnLineClear(cleared);
                AudioManager.Instance?.PlaySFX(LineClearSFX);

                // Bounty Hunter: coin per line cleared
                if (_cardState.HasBountyHunter && _cardState.BountyHunterCoinPerClear > 0)
                {
                    int bountyCoins = cleared * _cardState.BountyHunterCoinPerClear;
                    CoinWallet.Instance?.Earn(bountyCoins);
                    _coins += bountyCoins;
                }

                // Phantom Cell: relocate if its row/col was cleared
                if (_cardState.HasPhantomCell && _cardState.PhantomCellActive)
                    CheckPhantomRelocate(clearedRows, clearedCols);

                // Safe Zone: destroy tile + penalty if cleared
                if (_cardState.HasSafeZone && _cardState.SafeZoneActive)
                    CheckSafeZoneCleared(clearedRows, clearedCols);

                // Decaying Rift: early clear bonus
                if (_cardState.HasDecayingRift && _cardState.RiftTileActive)
                    CheckRiftCleared(clearedRows, clearedCols);

                // Perfect Clear: all placed pieces gone?
                if (_cardState.HasPerfectClear && _board.IsAllCellsCleared())
                    HandlePerfectClear();
            }

            // ── Combo ─────────────────────────────────────────────────────────
            _comboSystem.OnPlacement(hadClear: cleared > 0);

            // Chain Master: zincir ilerledi ya da kırıldı → slot değerini tazele.
            // Event tabanlı — Update() içinde polling yok.
            if (_comboSystem.HasChainMaster)
                RefreshCardLiveValues();

            if (_comboSystem.Multiplier > _maxComboReached)
                _maxComboReached = _comboSystem.Multiplier;

            // ── Per-milestone counters: increment BEFORE milestone may reset them ──
            if (isGhostDrop) _cardState.GhostDropUsesThisMilestone++;
            if (isFirstPick) _cardState.FirstPicksUsedThisMilestone++;

            // ── Card Collector: envanter büyüklüğü global çarpana eklenir ──────
            float effectiveGlobalMultiplier = _globalScoreMultiplier;
            if (_cardState.HasCardCollector)
            {
                int cardCount = CardInventoryUI.Instance?.GetSelectedCardIds().Count() ?? 0;
                effectiveGlobalMultiplier += cardCount * _cardState.CardCollectorPerCard;
            }

            // ── Score modifiers ───────────────────────────────────────────────
            float effectiveTileValueSum = tileValueSum;

            if (cleared > 0)
            {
                // Tunnel Vision: only column clears score (takes priority over Line Master)
                if (_cardState.HasTunnelVision)
                {
                    float colSum = 0f;
                    foreach (var snap in snapshots)
                        if (clearedCols[snap.X]) colSum += snap.Value;
                    effectiveTileValueSum = colSum * _cardState.TunnelVisionMultiplier;
                }
                // Line Master: only row clears score
                else if (_cardState.HasLineMaster)
                {
                    float rowSum = 0f;
                    foreach (var snap in snapshots)
                        if (clearedRows[snap.Y]) rowSum += snap.Value;
                    effectiveTileValueSum = rowSum * _cardState.LineMasterMultiplier;
                }

                // Diet Plan: global tile score reduction
                if (_cardState.HasDietPlan)
                    effectiveTileValueSum *= _cardState.DietPlanScoreFactor;

                // Slow Burn: early penalty / late bonus
                if (_cardState.HasSlowBurn)
                {
                    int placed    = _milestoneSystem?.PiecesPlacedInWindow ?? 0;
                    int piecesLeft = _milestoneSystem?.PiecesRemaining     ?? int.MaxValue;
                    if (placed < _cardState.SlowBurnEarlyCount)
                        effectiveTileValueSum *= _cardState.SlowBurnEarlyFactor;
                    else if (piecesLeft <= _cardState.SlowBurnLateCount)
                        effectiveTileValueSum *= _cardState.SlowBurnLateFactor;
                }

                // Selective Blindness: 2+ lines → 0 score; 1 line → ×factor
                if (_cardState.HasSelectiveBlindness)
                {
                    if (cleared >= 2) effectiveTileValueSum = 0f;
                    else effectiveTileValueSum *= _cardState.SelectiveBlindnessSingleFactor;
                }
            }

            // ── Score ─────────────────────────────────────────────────────────
            int gainedScore = _scoreSystem.ResolveAfterPlacement(
                effectiveTileValueSum,
                _comboSystem.Multiplier,
                effectiveGlobalMultiplier
            );

            // NOT: shape kartı bonusu artık ayrıca eklenmiyor — yerleştirmede tile'ın
            // değerine yazıldığı için tileValueSum içinden zaten geliyor. Burada tekrar
            // eklemek çift sayım olurdu.

            // NOT: Corner Stone / Center Base bonusu da tile değerine yazıldığı için
            // burada ayrıca eklenmiyor — clear olmadan puan vermez.

            // Neon Cable: explosion score (may be 0 if no cable was hit)
            if (_cardState.HasNeonCable && cleared > 0)
                gainedScore += HandleNeonCable(clearedRows, clearedCols);

            // Selective Blindness: remove random blocks after 1-line score
            if (_cardState.HasSelectiveBlindness && cleared == 1)
                RemoveRandomFilledBlocks(_cardState.SelectiveBlindnessRemoveCount);

            // ── Son çarpanlar — gainedScore kesinleştikten sonra uygulanır ─────

            // Double Strike: aynı anda 2+ line temizlenince
            if (_cardState.HasDoubleStrike && cleared >= _cardState.DoubleStrikeMinLines)
            {
                gainedScore = Mathf.RoundToInt(gainedScore * _cardState.DoubleStrikeFactor);
                ShowTriggerPopup(anchor,
                    Loc.Get("Popup.DoubleStrike", _cardState.DoubleStrikeFactor.ToString("0.##")),
                    new Color(1f, 0.55f, 0.15f));
            }

            // Gambler: %20 ihtimalle ×2 veya ×0.5
            if (_cardState.HasGambler && UnityEngine.Random.value < _cardState.GamblerChance)
            {
                bool won = UnityEngine.Random.value < 0.5f;
                gainedScore = Mathf.RoundToInt(
                    gainedScore * (won ? _cardState.GamblerWinFactor : _cardState.GamblerLoseFactor));

                ShowTriggerPopup(anchor,
                    won ? Loc.Get("Popup.GambleWon",  _cardState.GamblerWinFactor.ToString("0.##"))
                        : Loc.Get("Popup.GambleLost", _cardState.GamblerLoseFactor.ToString("0.##")),
                    won ? new Color(0.3f, 1f, 0.45f) : new Color(1f, 0.3f, 0.35f));
            }

            // Patient: clear'sız bekleyiş sonrası gelen clear ödüllendirilir
            if (cleared == 0)
            {
                _cardState.PlacementsWithoutClear++;
            }
            else
            {
                if (_cardState.HasPatient &&
                    _cardState.PlacementsWithoutClear >= _cardState.PatientMinPlacements)
                {
                    gainedScore = Mathf.RoundToInt(gainedScore * _cardState.PatientFactor);
                    ShowTriggerPopup(anchor,
                        Loc.Get("Popup.Patience", _cardState.PatientFactor.ToString("0.##")),
                        new Color(0.55f, 0.8f, 1f));
                }
                _cardState.PlacementsWithoutClear = 0;
            }

            _score += gainedScore;

            // ── Milestone: score update (may trigger HandleMilestoneReached) ──
            _milestoneSystem?.OnScoreChanged(_score);

            RunStatsTracker.Instance?.RecordCombo(_comboSystem.Multiplier);

            if (gainedScore != 0)
                ScoreView?.AddScoreGain(_score, gainedScore);
            else
                ScoreView?.SetScore(_score);

            // ── Pool management ───────────────────────────────────────────────
            _piecePool.RemoveAt(_selectedPoolIndex);

            if (_piecePool.Count == 0)
                GenerateNewPool();
            else
            {
                _selectedPoolIndex = 0;
                SpawnNextFromPool();
            }

            // ── Decaying Rift: geri sayım + hücre kapatma ─────────────────────
            // Dead-pool kontrolünden ÖNCE çalışmalı: yarık süresi dolunca bir hücreyi
            // ölü bölgeye çeviriyor ve bu, kalan son hamleyi ortadan kaldırabiliyor.
            // Kontrol önce yapılırsa oyun hamlesiz kalır ama game over tetiklenmez.
            if (_cardState.HasDecayingRift)
                UpdateDecayingRift();

            // ── Dead pool check (tahta değişikliklerinden SONRA) ───────────────
            if (!HasAnyValidMoveInPool())
            {
                HandleDeadPool();
                return;
            }

            int remaining = _milestoneSystem?.PiecesRemaining ?? int.MaxValue;
            FrameFeedbackController.Instance?.OnCritical(remaining);


            // ── Milestone pool counter (skipped for Ghost Drop / First Picks) ─
            if (!skipPoolConsume)
                _milestoneSystem?.OnPiecePlaced();

            UpdateBoardOverlays();
            _poolDirty = true;
        }

        // ── Run ──────────────────────────────────────────────────────────────
        private void NewRun()
        {
            _cardState.Reset();
            _gameOverFired = false;
            ShapeCardEffectRegistry.Instance?.Reset();
            if (Width  <= 0) Width  = 8;
            if (Height <= 0) Height = 8;
            Time.timeScale = 1f;
            GameOverUI.Instance?.Hide();
            Time.timeScale = 1f;
            _upgradeRevivesUsed = 0;
            _freeDeadPoolReroll = 0;
            _cardDeadPoolReroll = 0;
            _score              = 0;
            _coins              = 0;
            _globalScoreMultiplier = 1f;
            _coinBonusPerMilestone = 0;
            CardInventoryUI.Instance?.Clear();   // slotlar yok edilir → canlı değer satırları da gider
            _ghost.Clear();
            _ghostPositionBonus.Clear();
            _board       = new BoardModel(Width, Height);
            _run         = new RunModel();
            _scoreSystem = new ScoreSystem();
            GameStateController.Reset();
            GameOverUI.Instance?.Hide();
            DOTween.SetTweensCapacity(200,125);
            var reg = UpgradeRegistry.Instance;
            int baseMaxCharge  = 3;
            int barExpansion   = Mathf.RoundToInt(
                reg?.GetEffect(_upgradeLibrary?.Get("upgrade_combo_bar")) ?? 0f);
            _comboSystem.SetMaxCharge(baseMaxCharge + barExpansion);

            float startingBonus = reg?.GetEffect(
                _upgradeLibrary?.Get("upgrade_starting_combo")) ?? 0f;
            _comboSystem.SetBaseMultiplier(1f + startingBonus);

            float comboGainBonus = reg?.GetEffect(
                _upgradeLibrary?.Get("upgrade_combo_gain")) ?? 0f;
            _comboSystem.SetBonusPerClear(0.1f + comboGainBonus);

            _comboSystem.Reset();
            if (_milestoneSystem != null)
            {
                int poolBonus = Mathf.RoundToInt(
                    UpgradeRegistry.Instance?.GetEffect(
                        _upgradeLibrary?.Get("upgrade_pool_capacity")) ?? 0f
                );

                _milestoneSystem?.SetPoolLimit(MilestoneConfig.PoolLimit + poolBonus);
                _milestoneSystem?.Reset();
                MilestoneView?.Bind(_milestoneSystem);
            }
            RunStatsTracker.Instance?.Reset();

            ScoreView?.SetScore(0);
            BoardView.Build(_board);
            _poolRerollsRemaining = Mathf.RoundToInt(
                UpgradeRegistry.Instance?.GetEffect(
                    _upgradeLibrary?.Get("upgrade_pool_reroll")) ?? 0f
            );
            _cardRerollsRemaining = Mathf.RoundToInt(
                UpgradeRegistry.Instance?.GetEffect(
                    _upgradeLibrary?.Get("upgrade_card_reroll")) ?? 0f
            );
            bool hasPoolReroll = _poolRerollsRemaining > 0;
            _poolRerollButton?.SetVisible(hasPoolReroll);
            _poolRerollButton?.UpdateCount(_poolRerollsRemaining);
            _poolRerollButton.OnRerollClicked = OnPoolRerollClicked;
            GenerateNewPool();
            _poolDirty = true;
        }

        private void SpawnNextFromPool()
        {
            if (_piecePool.Count == 0) { GenerateNewPool(); return; }
            if (_selectedPoolIndex < 0 || _selectedPoolIndex >= _piecePool.Count) return;
            _currentPiece = _piecePool[_selectedPoolIndex];
            _currentRot   = Rotation.R0;
        }

        private void GenerateNewPool(bool skipValidCheck = false)
        {
            _piecePool.Clear();

            // Diet Plan filters out large shapes
            Func<ShapeSO, bool> spawnFilter = null;
            if (_cardState.HasDietPlan)
            {
                int maxSize = _cardState.DietPlanMaxSize;
                spawnFilter = shape => shape.Cells.Count < maxSize;
            }

            for (int i = 0; i < 3; i++)
            {
                var piece = ShapeSpawnService.GetRandomWeighted(ShapeLibrary, spawnFilter);
                if (piece != null)
                    _piecePool.Add(piece);
            }

            _selectedPoolIndex = 0;
            _currentPiece      = _piecePool[0];
            _currentRot        = Rotation.R0;

            // Neon Cable: assign new positions on each new pool
            if (_cardState.HasNeonCable)
                ResetNeonCablePositions();

            if (!skipValidCheck && !HasAnyValidMoveInPool())
                OnGameOver();

            UpdateBoardOverlays();
            _poolDirty = true;
        }

        // ── Game Over ────────────────────────────────────────────────────────
        private void OnGameOver(GameOverReason reason = GameOverReason.Default)
        {
            // İki kez tetiklenmesin: hem DoPlace hem soft-lock taraması aynı anda
            // yakalayabilir, panel iki defa açılmamalı.
            if (_gameOverFired) return;
            _gameOverFired = true;

            FrameFeedbackController.Instance?.OnGameOver();
            AudioManager.Instance.PlaySFX(GameOverSFX,1f,false);

            float maxCombo = (RunStatsTracker.Instance?.MaxCombo ?? 10) / 10f;
            LastRunPanel.SaveLastRun(_score, _coins, maxCombo);

            if (GameOverAnnouncer.Instance != null)
            {
                GameOverAnnouncer.Instance.Play(reason, () =>
                    GameOverUI.Instance?.Show(_score, _coins));
            }
            else
            {
                GameOverUI.Instance?.Show(_score, _coins);
            }
        }

        private void OnPoolRerollClicked()
        {
            if (_poolRerollsRemaining <= 0) return;
            if (!GameStateController.InputAllowed) return;
            _poolRerollsRemaining--;
            _poolRerollButton?.UpdateCount(_poolRerollsRemaining);
            _milestoneSystem?.EnsureMinimumRemaining(6);
            GenerateNewPool(skipValidCheck: false);
            _poolDirty = true;
        }

        // ── Dead Pool ────────────────────────────────────────────────────────
        private void HandleDeadPool()
        {
            // First Picks döndürmeyi kapattığı için sıkıştıysak reroll/revive ile uğraşma:
            // yeni havuz da aynı kısıtla gelir. Oyunu kilitlemek yerine burada bitir.
            if (IsRotationLocked)
            {
                OnGameOver(GameOverReason.CannotPlace);
                return;
            }

            int reviveLevel = Mathf.RoundToInt(
                UpgradeRegistry.Instance?.GetEffect(
                    _upgradeLibrary?.Get("upgrade_dead_pool_revive")) ?? 0f);

            if (reviveLevel > 0 && _upgradeRevivesUsed < reviveLevel)
            {
                _upgradeRevivesUsed++;
                _milestoneSystem?.EnsureMinimumRemaining(6);
                GenerateNewPool();
                if (!HasAnyValidMoveInPool())
                    OnGameOver(GameOverReason.NoMoves);
                _poolDirty = true;
                return;
            }

            if (_freeDeadPoolReroll > 0)
            {
                _freeDeadPoolReroll--;
                _milestoneSystem?.EnsureMinimumRemaining(6);
                GenerateNewPool();
                if (!HasAnyValidMoveInPool())
                    OnGameOver(GameOverReason.NoMoves);
                return;
            }

            if (_cardDeadPoolReroll > 0 && _score >= CardRerollCost)
            {
                _cardDeadPoolReroll--;
                _score -= CardRerollCost;
                ScoreView?.AddScoreGain(_score, -CardRerollCost);
                _milestoneSystem?.EnsureMinimumRemaining(6);
                GenerateNewPool();
                if (!HasAnyValidMoveInPool())
                    OnGameOver(GameOverReason.NoMoves);
                _poolDirty = true;
                return;
            }

            // Momentum Shield: prevent game over when combo is high enough
            if (_cardState.HasMomentumShield &&
                _comboSystem.Multiplier >= _cardState.MomentumShieldMinMultiplier)
            {
                _comboSystem.ForceResetToBase();
                _milestoneSystem?.EnsureMinimumRemaining(6);
                GenerateNewPool();
                if (!HasAnyValidMoveInPool())
                {
                    OnGameOver(GameOverReason.NoMoves);
                    return;
                }
                _poolDirty = true;
                return;
            }

            OnGameOver();
        }

        // ── Milestone Handlers ───────────────────────────────────────────────
        private void HandleMilestoneReached(int coinReward, MilestoneData data)
        {
            // Reset per-milestone card counters for the new window
            _cardState.OnMilestoneReached();

            // Decaying Rift'in kapattığı hücreler yalnızca o milestone boyunca ölü kalır
            // ("for the remainder of the current milestone") — yeni pencerede tahta temizlenir.
            _board?.ClearAllDeadZones();
            UpdateBoardOverlays();

            // Future Investment: apply pending discount to new current window
            if (_cardState.HasFutureInvestment && _cardState.FutureInvestmentPendingDiscount > 0f)
            {
                _milestoneSystem?.ScaleCurrentWindow(1f - _cardState.FutureInvestmentPendingDiscount);
                _cardState.FutureInvestmentPendingDiscount = 0f;
            }

            // Hoarder: bonus pool capacity if enough shapes remained
            if (_cardState.HasHoarder &&
                _milestoneSystem != null &&
                _milestoneSystem.LastCompletedPiecesRemaining >= _cardState.HoarderMinRemaining)
            {
                _milestoneSystem.SetPoolLimit(_milestoneSystem.EffectivePoolLimit + _cardState.HoarderPoolBonus);
            }

            // ── Original coin reward logic ────────────────────────────────────
            float coinMultiplier = 1f + (UpgradeRegistry.Instance?.GetEffect(
                _upgradeLibrary?.Get("upgrade_coin_gain")) ?? 0f);

            int total = Mathf.RoundToInt((coinReward + _coinBonusPerMilestone) * coinMultiplier);
            CoinWallet.Instance?.Earn(total);
            _coins += total;

            MilestoneView?.PlayMilestoneReachedFX();
            FrameFeedbackController.Instance?.OnMilestone();

            // ── Demo sonu ─────────────────────────────────────────────────────
            // Ödül ve kutlama efekti oynatıldıktan SONRA bakılır: oyuncu son
            // aşamayı kazandığını görsün, panel bunun üstüne gelsin. Kart
            // seçimi hiç açılmaz — run burada biter.
            if (_demoBuild && data.ScoreThreshold >= _demoEndScoreThreshold)
            {
                var demoPanel = DemoEndPanel.Instance;

                if (demoPanel != null)
                {
                    // Bayrak yalnızca panel gerçekten açıldıysa set edilir.
                    // Aksi hâlde run "bitti" sayılır ama hiçbir ekran açılmaz;
                    // oyuncu oynamaya devam eder ve gerçekten öldüğünde
                    // OnGameOver bu bayrak yüzünden sessizce geri döner —
                    // yani Game Over ekranı hiç gelmez.
                    _gameOverFired = true;
                    demoPanel.Show();
                    return;
                }

                Debug.LogError(
                    "[RunController] Demo sonuna ulaşıldı ama sahnede DemoEndPanel yok. " +
                    "Run normal akışına devam ediyor. Paneli kurmak için: " +
                    "Tools ▸ RogueBlockBlast ▸ Demo ▸ Build End Panel");
            }

            CardSO newlyUnlockedCard = null;
            if (UnlockRegistry.Instance != null &&
                UnlockRegistry.Instance.IsFirstMilestoneReach(data.Label))
            {
                var lockedCards = CardPool
                    .Where(c => c != null && c.LockedByDefault && !c.IsUnlocked)
                    .ToList();

                if (lockedCards.Count > 0)
                {
                    int pick = UnityEngine.Random.Range(0, lockedCards.Count);
                    newlyUnlockedCard = lockedCards[pick];
                    UnlockRegistry.Instance.UnlockCard(newlyUnlockedCard.Id);
                    MilestoneView?.ShowNewCardEarned(RogueBlockBlast.Core.Localization.ContentLocalization.Name(newlyUnlockedCard));
                }

                int current = PlayerPrefs.GetInt("MaxMilestoneReached", 0);
                if (_milestoneSystem.CurrentMilestoneIndex > current)
                    PlayerPrefs.SetInt("MaxMilestoneReached", _milestoneSystem.CurrentMilestoneIndex);
            }

            if (CardPool != null && CardPool.Count > 0)
            {
                // Filter out cards that are excluded by active card effects (e.g. mutual exclusions)
                var availableCards = CardPool.Where(c => c != null && !IsCardExcluded(c)).ToList();
                CardSelectionUI.Instance?.Show(availableCards, OnCardPicked, newlyUnlockedCard, _cardRerollsRemaining);
            }
        }

        private void HandlePoolLimitExhausted()
        {
            OnGameOver(GameOverReason.PoolExhausted);
        }

        // ── Valid Move Check ─────────────────────────────────────────────────
        /// <summary>
        /// First Picks aktifken şekiller döndürülemez. O sırada oyuncu her parçayı
        /// yalnızca R0'da yerleştirebilir — havuzda hamle var mı sorusu da buna göre
        /// yanıtlanmalı, yoksa "döndürsen sığar" diye hamle sayılan bir durumda oyun kilitlenir.
        /// </summary>
        private bool IsRotationLocked =>
            _cardState != null &&
            _cardState.HasFirstPicks &&
            _cardState.FirstPicksUsedThisMilestone < _cardState.FirstPicksFreeCount;


        // ── Kilitlenme koruması ──────────────────────────────────────────────

        /// <summary>Soft-lock taraması arası süre (saniye). Her frame taramaya gerek yok.</summary>
        private const float SoftLockCheckInterval = 0.4f;
        private float _nextSoftLockCheck;
        private bool  _gameOverFired;

        /// <summary>
        /// Oyun hamlesiz kaldıysa ama game over tetiklenmediyse yakalar.
        /// Yalnızca oyuncu gerçekten oynayabiliyorken bakar: UI açıkken (kart seçimi,
        /// pause) input kilitli olur ve o sırada tahta zaten değişmez.
        /// </summary>
        private void CheckForSoftLock()
        {
            if (_gameOverFired) return;
            if (_board == null || _piecePool == null || _piecePool.Count == 0) return;
            if (!GameStateController.InputAllowed) return;

            if (Time.unscaledTime < _nextSoftLockCheck) return;
            _nextSoftLockCheck = Time.unscaledTime + SoftLockCheckInterval;

            if (HasAnyValidMoveInPool()) return;

            Debug.LogWarning("[RunController] Hamlesiz durum DoPlace dışında oluştu — dead pool devreye alınıyor.");
            HandleDeadPool();
        }

        // ── Pozisyon bonusu haritası ─────────────────────────────────────────

        /// <summary>
        /// Kart kaynaklı kalıcı hücre bonuslarını (Corner Stone / Center Base) hücre
        /// bazında tutar. BoardView bunu boş hücrelerde "+3" olarak gösterir; oyuncu
        /// şekli sürüklemeden hangi hücrenin ekstra puan verdiğini görür.
        ///
        /// Skorlamayla aynı fonksiyondan (GetPositionBonusForCell) doldurulur —
        /// gösterilen sayı ile kazanılan puanın ayrışması mümkün değil.
        /// </summary>
        private readonly Dictionary<Vector2Int, float> _staticPositionBonus = new();

        private void RebuildStaticPositionBonus()
        {
            _staticPositionBonus.Clear();
            if (_board == null || !HasAnyPositionBonus) return;

            for (int y = 0; y < _board.Height; y++)
            for (int x = 0; x < _board.Width; x++)
            {
                var p = new Vector2Int(x, y);
                float b = GetPositionBonusForCell(p);
                if (b > 0f) _staticPositionBonus[p] = b;
            }
        }
        private bool HasAnyValidMoveInPool()
        {
            int rotationCount = IsRotationLocked ? 1 : 4;   // kilitliyken sadece R0 denenir

            for (int p = 0; p < _piecePool.Count; p++)
            {
                var piece = _piecePool[p];
                for (int r = 0; r < rotationCount; r++)
                for (int y = 0; y < _board.Height; y++)
                for (int x = 0; x < _board.Width; x++)
                {
                    if (PlacementSystem.CanPlace(_board, piece, new Vector2Int(x, y), (Rotation)r))
                        return true;
                }
            }
            return false;
        }

        // ── Ghost Drop helper ────────────────────────────────────────────────
        private bool IsGhostDropPlacement(BoardModel board, PieceDefinition piece, Vector2Int anchor, Rotation rot)
        {
            var cells = piece.GetCells(rot);
            var pieceSet = new HashSet<Vector2Int>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
                pieceSet.Add(anchor + cells[i]);

            for (int i = 0; i < cells.Count; i++)
            {
                var pos = anchor + cells[i];
                if (IsNeighborFilled(board, pos + Vector2Int.up,    pieceSet)) return false;
                if (IsNeighborFilled(board, pos + Vector2Int.down,  pieceSet)) return false;
                if (IsNeighborFilled(board, pos + Vector2Int.left,  pieceSet)) return false;
                if (IsNeighborFilled(board, pos + Vector2Int.right, pieceSet)) return false;
            }
            return true;
        }

        private static bool IsNeighborFilled(BoardModel board, Vector2Int pos, HashSet<Vector2Int> pieceSet)
        {
            if (pieceSet.Contains(pos)) return false;
            return board.IsFilled(pos.x, pos.y);
        }

        // ── Card exclusion ───────────────────────────────────────────────────
        private bool IsCardExcluded(CardSO card)
        {
            foreach (var effect in card.Effects)
            {
                // Tunnel Vision and Line Master are mutually exclusive
                if (effect.Type == CardEffectType.TunnelVisionMultiplier && _cardState.HasLineMaster)  return true;
                if (effect.Type == CardEffectType.LineMasterMultiplier   && _cardState.HasTunnelVision) return true;
            }
            return false;
        }

        // ── Hyperfocus event handler ─────────────────────────────────────────
        private void HandleHyperfocusPenalty()
        {
            if (!_cardState.HasHyperfocus || _cardState.HyperfocusPenaltyShapes <= 0) return;
            _milestoneSystem?.DeductPieces(_cardState.HyperfocusPenaltyShapes);
        }

        // ── Card picked ──────────────────────────────────────────────────────
        private void OnCardPicked(CardSO card)
        {
            if (card == null) return;

            bool hadNeonCable    = _cardState.HasNeonCable;
            bool hadSafeZone     = _cardState.HasSafeZone;
            bool hadPhantomCell  = _cardState.HasPhantomCell;

            CardEffectApplier.Apply(
                card,
                _comboSystem,
                _milestoneSystem,
                ref _cardDeadPoolReroll,
                ref _globalScoreMultiplier,
                ref _coinBonusPerMilestone,
                _cardState
            );

            // Assign board positions for newly activated overlay cards
            if (_cardState.HasNeonCable && !hadNeonCable)
                ResetNeonCablePositions();
            if (_cardState.HasSafeZone && !hadSafeZone)
                AssignSafeZonePosition();
            if (_cardState.HasPhantomCell && !hadPhantomCell)
                AssignPhantomCellPosition();

            _milestoneSystem?.RefreshProgress();
            UpdateBoardOverlays();
            RebuildStaticPositionBonus();   // Corner Stone / Center Base yeni alınmış olabilir
            CardInventoryUI.Instance?.AddCard(card);

            // Envanter değişti → tüm slotlar yenilenir (Card Collector sayısı arttı)
            RefreshCardLiveValues();
        }

        // ── Overlay helpers ──────────────────────────────────────────────────
        private void UpdateBoardOverlays()
        {
            BoardView.ClearAllOverlays();
            if (_cardState.HasNeonCable)
            {
                if (_cardState.NeonCableAValid)
                    BoardView.SetOverlay(_cardState.NeonCablePositionA.x, _cardState.NeonCablePositionA.y, RogueBlockBlast.UI.OverlayType.NeonCableA, "C");
                if (_cardState.NeonCableBValid)
                    BoardView.SetOverlay(_cardState.NeonCablePositionB.x, _cardState.NeonCablePositionB.y, RogueBlockBlast.UI.OverlayType.NeonCableB, "C");
            }
            if (_cardState.HasSafeZone && _cardState.SafeZoneActive)
                BoardView.SetOverlay(_cardState.SafeZonePosition.x, _cardState.SafeZonePosition.y, RogueBlockBlast.UI.OverlayType.SafeZone, "S");
            if (_cardState.HasDecayingRift && _cardState.RiftTileActive)
                BoardView.SetOverlay(_cardState.RiftTilePosition.x, _cardState.RiftTilePosition.y, RogueBlockBlast.UI.OverlayType.DecayingRift, _cardState.RiftCurrentCount.ToString());
            if (_cardState.HasPhantomCell && _cardState.PhantomCellActive)
                BoardView.SetOverlay(_cardState.PhantomCellPosition.x, _cardState.PhantomCellPosition.y, RogueBlockBlast.UI.OverlayType.PhantomCell, "P");
        }

        private Vector2Int PickRandomEmptyCell(HashSet<Vector2Int> exclude = null)
        {
            var empties = _board.GetEmptyCells(exclude);
            if (empties.Count == 0) return new Vector2Int(-1, -1);
            return empties[UnityEngine.Random.Range(0, empties.Count)];
        }

        private HashSet<Vector2Int> GetReservedOverlayPositions()
        {
            var r = new HashSet<Vector2Int>();
            if (_cardState.NeonCableAValid)    r.Add(_cardState.NeonCablePositionA);
            if (_cardState.NeonCableBValid)    r.Add(_cardState.NeonCablePositionB);
            if (_cardState.SafeZoneActive)     r.Add(_cardState.SafeZonePosition);
            if (_cardState.PhantomCellActive)  r.Add(_cardState.PhantomCellPosition);
            if (_cardState.RiftTileActive)     r.Add(_cardState.RiftTilePosition);
            return r;
        }

        // ── Card: Neon Cable ─────────────────────────────────────────────────
        private void ResetNeonCablePositions()
        {
            var reserved = GetReservedOverlayPositions();
            reserved.Remove(_cardState.NeonCablePositionA);
            reserved.Remove(_cardState.NeonCablePositionB);

            var empties = _board.GetEmptyCells(reserved);
            if (empties.Count < 2)
            {
                _cardState.NeonCablePositionA = new Vector2Int(-1, -1);
                _cardState.NeonCablePositionB = new Vector2Int(-1, -1);
                return;
            }
            int iA = UnityEngine.Random.Range(0, empties.Count);
            _cardState.NeonCablePositionA = empties[iA];
            empties.RemoveAt(iA);
            _cardState.NeonCablePositionB = empties[UnityEngine.Random.Range(0, empties.Count)];
        }

        private int HandleNeonCable(bool[] clearedRows, bool[] clearedCols)
        {
            if (!_cardState.NeonCableAValid || !_cardState.NeonCableBValid) return 0;
            var pA = _cardState.NeonCablePositionA;
            var pB = _cardState.NeonCablePositionB;

            bool aHit = clearedRows[pA.y] || clearedCols[pA.x];
            bool bHit = clearedRows[pB.y] || clearedCols[pB.x];

            if (!aHit && !bHit) return 0;

            int bonus = 0;
            if (aHit && !bHit) bonus = ExplodeNeonArea(pB);
            else if (bHit && !aHit) bonus = ExplodeNeonArea(pA);
            // both hit simultaneously: no explosion, just reset

            ResetNeonCablePositions();
            return bonus;
        }

        private int ExplodeNeonArea(Vector2Int center)
        {
            float sum = 0f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int ex = center.x + dx, ey = center.y + dy;
                if (!_board.IsInside(ex, ey)) continue;
                if (_board.IsDeadZone(ex, ey)) continue;
                sum += _board.GetTileValue(ex, ey);
                _board.SetFilled(ex, ey, false);
                BoardView.GetTile(ex, ey)?.PlayClearFX(0f);
            }
            return Mathf.RoundToInt(sum * _cardState.NeonCableExplosionScore
                                       * _comboSystem.Multiplier
                                       * _globalScoreMultiplier);
        }

        // ── Card: Safe Zone ──────────────────────────────────────────────────
        private void AssignSafeZonePosition()
        {
            var pos = PickRandomEmptyCell(GetReservedOverlayPositions());
            _cardState.SafeZonePosition = pos;
        }

        private void CheckSafeZoneCoverage(Vector2Int anchor)
        {
            var cells = _currentPiece.GetCells(_currentRot);
            foreach (var c in cells)
            {
                if (anchor + c == _cardState.SafeZonePosition)
                {
                    _comboSystem.SetComboFloor(_cardState.SafeZoneComboFloor);
                    break;
                }
            }
        }

        private void CheckSafeZoneCleared(bool[] clearedRows, bool[] clearedCols)
        {
            var sp = _cardState.SafeZonePosition;
            if (clearedRows[sp.y] || clearedCols[sp.x])
            {
                _cardState.SafeZonePosition = new Vector2Int(-1, -1);
                _comboSystem.SetComboFloor(0f);
                _milestoneSystem?.DeductPieces(_cardState.SafeZonePenalty);

                // Ceza görünür olmalı: oyuncu 8 şeklin neden gittiğini anlamalı
                BoardView?.GetTile(sp.x, sp.y)?.PlayClearFX(0f);
                FrameFeedbackController.Instance?.OnCritical(0);
                MilestoneView?.ShowNewCardEarned(
                    RogueBlockBlast.Core.Localization.Loc.Get("Card.Card_Safe_Zone.Broken",
                                                              _cardState.SafeZonePenalty));
                UpdateBoardOverlays();
            }
        }

        // ── Card: Decaying Rift ──────────────────────────────────────────────
        private void CheckRiftCleared(bool[] clearedRows, bool[] clearedCols)
        {
            var rp = _cardState.RiftTilePosition;
            if (clearedRows[rp.y] || clearedCols[rp.x])
            {
                _cardState.RiftTilePosition = new Vector2Int(-1, -1);
                _cardState.RiftCurrentCount = 0;
                _milestoneSystem?.AddPieces(_cardState.RiftBonusShapes);
            }
        }

        private void UpdateDecayingRift()
        {
            // Decrement active countdown tile
            if (_cardState.RiftTileActive)
            {
                _cardState.RiftCurrentCount--;
                if (_cardState.RiftCurrentCount <= 0)
                {
                    // Countdown reached 0 — create dead zone
                    var rp = _cardState.RiftTilePosition;
                    _board.AddDeadZone(rp.x, rp.y);
                    _cardState.RiftTilePosition = new Vector2Int(-1, -1);
                    _cardState.RiftCurrentCount = 0;
                }
            }

            // Increment placement counter and maybe spawn new countdown tile
            _cardState.RiftPlacementCounter++;
            if (_cardState.RiftPlacementCounter >= _cardState.RiftSpawnInterval && !_cardState.RiftTileActive)
            {
                _cardState.RiftPlacementCounter = 0;
                var reserved = GetReservedOverlayPositions();
                var pos = PickRandomEmptyCell(reserved);
                if (pos.x >= 0)
                {
                    _cardState.RiftTilePosition = pos;
                    _cardState.RiftCurrentCount = _cardState.RiftCountdownStart;
                }
            }
        }

        // ── Card: Phantom Cell ───────────────────────────────────────────────
        private void AssignPhantomCellPosition()
        {
            var reserved = GetReservedOverlayPositions();
            var pos = PickRandomEmptyCell(reserved);
            if (pos.x < 0) return;
            _cardState.PhantomCellPosition = pos;
            _board.AddPhantom(pos.x, pos.y);
        }

        private void CheckPhantomRelocate(bool[] clearedRows, bool[] clearedCols)
        {
            var pp = _cardState.PhantomCellPosition;
            if (!clearedRows[pp.y] && !clearedCols[pp.x]) return;

            _board.RemovePhantom(pp.x, pp.y);
            var reserved = GetReservedOverlayPositions();
            reserved.Remove(pp);
            var pos = PickRandomEmptyCell(reserved);
            if (pos.x >= 0)
            {
                _cardState.PhantomCellPosition = pos;
                _board.AddPhantom(pos.x, pos.y);
            }
            else
            {
                _cardState.PhantomCellPosition = new Vector2Int(-1, -1);
            }
        }

        // ── Card: Perfect Clear ──────────────────────────────────────────────
        private void HandlePerfectClear()
        {
            if (_cardState.PerfectClearCoinReward > 0)
            {
                CoinWallet.Instance?.Earn(_cardState.PerfectClearCoinReward);
                _coins += _cardState.PerfectClearCoinReward;
            }
            _comboSystem.ForceMaxCharge();
            if (_cardState.PerfectClearComboBoost > 0f)
                _comboSystem.AddMultiplier(_cardState.PerfectClearComboBoost);
        }

        // ── Card live values (in-run kart slotları) ──────────────────────────

        /// <summary>Tetikleme popup'ını yerleştirilen şeklin üzerinde gösterir.</summary>
        private void ShowTriggerPopup(Vector2Int anchor, string text, Color color)
        {
            if (LineClearVFX == null || _currentPiece == null) return;

            // Şeklin merkezini bul — popup oraya çıksın
            var cells = _currentPiece.GetCells(_currentRot);
            if (cells.Count == 0) return;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < cells.Count; i++)
            {
                var p = anchor + cells[i];
                sum += BoardView.GetTileWorldPosition(p.x, p.y);
            }

            LineClearVFX.PlayTriggerPopup(sum / cells.Count, text, color);
        }

        /// <summary>Tüm kart slotlarının canlı değer satırını yeniler.</summary>
        private void RefreshCardLiveValues()
            => CardInventoryUI.Instance?.RefreshLiveValues(FormatCardLiveValue);

        /// <summary>
        /// Kartın anlık katkısı. Değerler efektin kendi kaynağından okunur
        /// (_cardState / _comboSystem / _milestoneSystem) — ayrı sabit yok.
        /// </summary>
        private string FormatCardLiveValue(CardSO card, int stackCount)
        {
            if (card == null || card.Effects == null || card.Effects.Count == 0) return null;

            foreach (var effect in card.Effects)
            {
                switch (effect.Type)
                {
                    // Envanter büyüdükçe değişir
                    case CardEffectType.CardCollector:
                    {
                        int count = CardInventoryUI.Instance?.GetSelectedCardIds().Count() ?? 0;
                        float pct = _cardState.CardCollectorPerCard * count * 100f;
                        return $"+{pct:0.#}% ({count} cards)";
                    }

                    // Zincir ilerledikçe değişir
                    case CardEffectType.ChainMaster:
                        return $"+{_comboSystem.ChainMasterAccumulated:0.00} combo " +
                               $"(chain {_comboSystem.ConsecutiveClears})";

                    // Seçimden sonra sabit — yine de stack görünsün
                    case CardEffectType.ScoreMultiplierBonus:
                    case CardEffectType.LineScoreBonus:
                        return $"+{_cardState.GetAccumulated(effect.Type) * 100f:0.#}% score";

                    case CardEffectType.ComboBonusPerClear:
                        return $"+{_cardState.GetAccumulated(effect.Type):0.00} combo/clear";

                    case CardEffectType.PoolLimitBonus:
                        return $"+{_cardState.GetAccumulated(effect.Type):0} pool " +
                               $"(now {_milestoneSystem?.CurrentPoolLimit ?? 0})";

                    case CardEffectType.HeavyLoad:
                        return $"+{_cardState.GetAccumulated(effect.Type):0} pool / " +
                               $"-{stackCount * 10:0}% score";

                    // Pozisyon kartları — tile başına stack'li değer
                    case CardEffectType.CornerStoneBonus:
                        return $"+{_cardState.CornerStoneBonus:0.#}/corner tile";

                    case CardEffectType.CenterBaseBonus:
                        return $"+{_cardState.CenterBaseBonus:0.#}/center tile";
                }
            }
            return null;
        }

        // ── Card: Corner Stone / Center Base ─────────────────────────────────
        /// <summary>
        /// Yerleştirilen şeklin köşe / merkez hücrelere denk gelen tile'ları için
        /// ham bonus puanı. Combo ve global çarpan çağıran tarafta uygulanır.
        /// </summary>

        /// <summary>
        /// Corner Stone / Center Base bonusunu, yeni konan hücrelerin tile değerine ekler.
        ///
        /// Bonus hücre bazlıdır: yalnızca köşe veya merkez 2x2'ye denk gelen hücreler alır.
        /// Skora doğrudan eklenmez — puan yalnızca satır temizlenince kazanılır
        /// (CLAUDE.md: "Scores are only awarded when lines clear"). Böylece boş hücrede
        /// gösterilen "+3 / +4" ipucu, ghost önizlemesi, tahtadaki değer ve line clear'da
        /// uçan sayı hep aynı olur.
        /// </summary>
        private void ApplyPositionBonusToPlacedCells(Vector2Int anchor, float baseTileValue)
        {
            if (!HasAnyPositionBonus) return;

            var cells = _currentPiece.GetCells(_currentRot);
            for (int i = 0; i < cells.Count; i++)
            {
                var p = anchor + cells[i];
                float bonus = GetPositionBonusForCell(p);
                if (bonus <= 0f) continue;

                _board.SetFilled(p.x, p.y, true, _currentPiece.BlockColor, baseTileValue + bonus);
            }
        }

        /// <summary>
        /// Tek bir hücrenin pozisyon bonusu. Yerleştirmede tile değerine yazan kod,
        /// ghost önizlemesi ve boş hücredeki "+3" ipucu aynı fonksiyonu kullanır —
        /// gösterilen sayı ile kazanılan puanın ayrışması mümkün değil.
        /// </summary>
        private float GetPositionBonusForCell(Vector2Int p)
        {
            float bonus = 0f;
            if (_cardState.HasCornerStone && IsCornerCell(p)) bonus += _cardState.CornerStoneBonus;
            if (_cardState.HasCenterBase  && IsCenterCell(p)) bonus += _cardState.CenterBaseBonus;
            return bonus;
        }

        private bool HasAnyPositionBonus => _cardState.HasCornerStone || _cardState.HasCenterBase;

        /// <summary>Tahtanın 4 köşesi: (0,0), (0,H-1), (W-1,0), (W-1,H-1).</summary>
        private bool IsCornerCell(Vector2Int p)
        {
            return (p.x == 0 || p.x == _board.Width  - 1) &&
                   (p.y == 0 || p.y == _board.Height - 1);
        }

        /// <summary>Merkezdeki 2x2 alan — 8x8 tahtada (3,3), (3,4), (4,3), (4,4).</summary>
        private bool IsCenterCell(Vector2Int p)
        {
            int cx = _board.Width  / 2;
            int cy = _board.Height / 2;
            return (p.x == cx || p.x == cx - 1) &&
                   (p.y == cy || p.y == cy - 1);
        }

        // ── Card: Selective Blindness ────────────────────────────────────────
        private void RemoveRandomFilledBlocks(int count)
        {
            var filled = _board.GetFilledCells();
            for (int i = 0; i < count && filled.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, filled.Count);
                var cell = filled[idx];
                filled.RemoveAt(idx);
                _board.SetFilled(cell.x, cell.y, false);
                BoardView.GetTile(cell.x, cell.y)?.PlayClearFX(0f);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static Rotation NextRot(Rotation r) => (Rotation)(((int)r + 1) & 3);
        private static Rotation PrevRot(Rotation r) => (Rotation)(((int)r + 3) & 3);

        private void OnValidate()
        {
            if (Width  <= 0) Width  = 8;
            if (Height <= 0) Height = 8;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ── Debug API ────────────────────────────────────────────────────────
        // Yalnızca editör ve development build'de derlenir; release'e sızmaz.
        // DebugPanel bu kapıları kullanır — kart uygulaması normal seçim yolunun
        // ta kendisidir (OnCardPicked), yani test ettiğin davranış gerçek davranıştır.

        public System.Collections.Generic.IReadOnlyList<CardSO> DebugCardPool => CardPool;

        public void DebugApplyCard(CardSO card) => OnCardPicked(card);

        public void DebugAddPieces(int count)
        {
            if (count >= 0) _milestoneSystem?.AddPieces(count);
            else            _milestoneSystem?.DeductPieces(-count);
        }

        public void DebugAddScore(int amount)
        {
            _score += amount;
            ScoreView?.SetScore(_score);
            _milestoneSystem?.OnScoreChanged(_score);
        }

        public void DebugRerollPool()
        {
            GenerateNewPool();
            _poolDirty = true;
        }

        public void DebugClearBoard()
        {
            if (_board == null) return;
            for (int y = 0; y < _board.Height; y++)
            for (int x = 0; x < _board.Width; x++)
                _board.SetFilled(x, y, false);
            _board.ClearAllDeadZones();
            UpdateBoardOverlays();
        }

        /// <summary>Tahtayı sadece bir hücre boş kalacak şekilde doldurur — dead pool / kilit testi için.</summary>
        public void DebugFillBoardExceptOne()
        {
            if (_board == null) return;
            for (int y = 0; y < _board.Height; y++)
            for (int x = 0; x < _board.Width; x++)
                if (!(x == 0 && y == 0)) _board.SetFilled(x, y, true, Color.gray, 1f);
        }

        public void DebugForceGameOver() => OnGameOver();

        /// <summary>Panelde gösterilecek canlı durum özeti.</summary>
        public string DebugStateSummary()
        {
            var s = new System.Text.StringBuilder();
            s.AppendLine($"Skor {_score}   Combo x{_comboSystem?.Multiplier:0.00}   Coin {_coins}");
            s.AppendLine($"Havuz kalan {_milestoneSystem?.PiecesRemaining}  /  limit {_milestoneSystem?.EffectivePoolLimit}");
            s.AppendLine($"Milestone #{_milestoneSystem?.CurrentMilestoneIndex}   global x{_globalScoreMultiplier:0.00}");
            s.AppendLine($"Rotasyon kilidi: {IsRotationLocked}   GameOver: {_gameOverFired}");
            s.Append("Aktif: ");
            if (_cardState.HasCornerStone)   s.Append($"CornerStone(+{_cardState.CornerStoneBonus}) ");
            if (_cardState.HasCenterBase)    s.Append($"CenterBase(+{_cardState.CenterBaseBonus}) ");
            if (_cardState.HasFirstPicks)    s.Append($"FirstPicks({_cardState.FirstPicksUsedThisMilestone}/{_cardState.FirstPicksFreeCount}) ");
            if (_cardState.HasDietPlan)      s.Append("DietPlan ");
            if (_cardState.HasGhostDrop)     s.Append($"GhostDrop({_cardState.GhostDropUsesThisMilestone}/{_cardState.GhostDropMaxUses}) ");
            if (_cardState.HasDecayingRift)  s.Append($"Rift(sayaç {_cardState.RiftCurrentCount} @{_cardState.RiftTilePosition}) ");
            if (_cardState.HasSafeZone)      s.Append($"SafeZone@{_cardState.SafeZonePosition} ");
            if (_cardState.HasPhantomCell)   s.Append($"Phantom@{_cardState.PhantomCellPosition} ");
            if (_cardState.HasNeonCable)     s.Append($"NeonCable {_cardState.NeonCablePositionA}<->{_cardState.NeonCablePositionB} ");
            if (_cardState.HasTunnelVision)  s.Append("TunnelVision ");
            if (_cardState.HasLineMaster)    s.Append("LineMaster ");
            if (_cardState.HasSelectiveBlindness) s.Append("SelBlind ");
            if (_cardState.HasDoubleStrike)  s.Append("DoubleStrike ");
            if (_cardState.HasGambler)       s.Append("Gambler ");
            if (_cardState.HasPatient)       s.Append($"Patient({_cardState.PlacementsWithoutClear}) ");
            if (_cardState.HasSlowBurn)      s.Append("SlowBurn ");
            if (_cardState.HasHyperfocus)    s.Append("Hyperfocus ");
            if (_cardState.HasBountyHunter)  s.Append("BountyHunter ");
            if (_cardState.HasCardCollector) s.Append("CardCollector ");
            if (_cardState.HasPerfectClear)  s.Append("PerfectClear ");
            if (_cardState.HasMomentumShield)s.Append("MomentumShield ");
            if (_cardState.HasHoarder)       s.Append("Hoarder ");
            if (_cardState.HasFutureInvestment) s.Append("FutureInvest ");
            return s.ToString();
        }
#endif
    }
}
