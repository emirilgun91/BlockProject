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

        // ── Core systems ─────────────────────────────────────────────────────
        private BoardModel      _board;
        private RunModel        _run;
        private ScoreSystem     _scoreSystem     = new ScoreSystem();
        private ComboSystem     _comboSystem     = new ComboSystem();
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
            if (Keyboard.current != null)
            {
                // First Picks blocks rotation while free placements remain
                bool canRotate = !(_cardState.HasFirstPicks &&
                                   _cardState.FirstPicksUsedThisMilestone < _cardState.FirstPicksFreeCount);

                if (Keyboard.current.qKey.wasPressedThisFrame && canRotate) _currentRot = PrevRot(_currentRot);
                if (Keyboard.current.eKey.wasPressedThisFrame && canRotate) _currentRot = NextRot(_currentRot);
                if (Keyboard.current.rKey.wasPressedThisFrame) NewRun();

                if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectPool(0);
                if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectPool(1);
                if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectPool(2);
            }

            _ghost.Clear();
            if (BoardView.IsMouseOverBoard(MainCamera))
            {
                var cell = BoardView.TryGetClampedCellUnderMouse(MainCamera, _currentPiece, _currentRot);

                if (cell.HasValue)
                {
                    var  anchor   = cell.Value;
                    bool canPlace = PlacementSystem.CanPlace(_board, _currentPiece, anchor, _currentRot);

                    var cells = _currentPiece.GetCells(_currentRot);
                    for (int i = 0; i < cells.Count; i++)
                        _ghost.Add(anchor + cells[i]);

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
            BoardView.Render(_board, _ghost, ghostTileValue);

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

            // ── Place ─────────────────────────────────────────────────────────
            PlacementSystem.Place(_board, _currentPiece, anchor, _currentRot);
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
            }

            // ── Combo ─────────────────────────────────────────────────────────
            _comboSystem.OnPlacement(hadClear: cleared > 0);

            if (_comboSystem.Multiplier > _maxComboReached)
                _maxComboReached = _comboSystem.Multiplier;

            // ── Per-milestone counters: increment BEFORE milestone may reset them ──
            if (isGhostDrop) _cardState.GhostDropUsesThisMilestone++;
            if (isFirstPick) _cardState.FirstPicksUsedThisMilestone++;

            // ── Shape Card bonus ──────────────────────────────────────────────
            float shapeBonus = 0f;
            var shapeCardReg = ShapeCardEffectRegistry.Instance;
            if (shapeCardReg != null && shapeCardReg.HasAnyEffect(_currentPiece.Id))
            {
                float bonusPerTile = shapeCardReg.GetScoreBonus(_currentPiece.Id);
                if (bonusPerTile > 0f)
                    shapeBonus = bonusPerTile * _currentPiece.GetCells(_currentRot).Count;
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
            }

            // ── Score ─────────────────────────────────────────────────────────
            int gainedScore = _scoreSystem.ResolveAfterPlacement(
                effectiveTileValueSum,
                _comboSystem.Multiplier,
                _globalScoreMultiplier
            );

            if (shapeBonus > 0f && cleared > 0)
            {
                gainedScore += Mathf.RoundToInt(
                    shapeBonus * _comboSystem.Multiplier * _globalScoreMultiplier);
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

            // ── Dead pool check ───────────────────────────────────────────────
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

            _poolDirty = true;
        }

        // ── Run ──────────────────────────────────────────────────────────────
        private void NewRun()
        {
            _cardState.Reset();
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
            CardInventoryUI.Instance?.Clear();
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
            if (!skipValidCheck && !HasAnyValidMoveInPool())
                OnGameOver();

            _poolDirty = true;
        }

        // ── Game Over ────────────────────────────────────────────────────────
        private void OnGameOver(GameOverReason reason = GameOverReason.Default)
        {
            FrameFeedbackController.Instance?.OnGameOver();
            AudioManager.Instance.PlaySFX(GameOverSFX,1f,false);

            float maxCombo = (RunStatsTracker.Instance?.MaxCombo ?? 10) / 10f;
            LastRunPanel.SaveLastRun(_score, _coins, maxCombo);

            if (GameOverAnnouncer.Instance != null)
            {
                GameOverAnnouncer.Instance.Play(reason, () =>
                    GameOverUI.Instance?.Show(_score));
            }
            else
            {
                GameOverUI.Instance?.Show(_score);
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
                    MilestoneView?.ShowNewCardEarned(newlyUnlockedCard.CardName);
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
        private bool HasAnyValidMoveInPool()
        {
            for (int p = 0; p < _piecePool.Count; p++)
            {
                var piece = _piecePool[p];
                for (int r = 0; r < 4; r++)
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

            CardEffectApplier.Apply(
                card,
                _comboSystem,
                _milestoneSystem,
                ref _cardDeadPoolReroll,
                ref _globalScoreMultiplier,
                ref _coinBonusPerMilestone,
                _cardState
            );
            CardInventoryUI.Instance?.AddCard(card);
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static Rotation NextRot(Rotation r) => (Rotation)(((int)r + 1) & 3);
        private static Rotation PrevRot(Rotation r) => (Rotation)(((int)r + 3) & 3);

        private void OnValidate()
        {
            if (Width  <= 0) Width  = 8;
            if (Height <= 0) Height = 8;
        }
    }
}
