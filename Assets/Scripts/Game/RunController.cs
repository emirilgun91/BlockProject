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
 
// Reroll hakları — run başında upgrade'den okunur
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
        //SFX
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
            if (mainLoopMusic != null)
            {
                AudioManager.Instance.PlayMusic(mainLoopMusic);
            }
            if (UnlockRegistry.Instance != null)
            {
                // Shape ID'lerini topla
                var shapeIds = ShapeLibrary.Shapes
                    .Where(s => s != null)
                    .Select(s => s.Id);
 
                // Card ID'lerini topla
                var cardIds = CardPool
                    .Where(c => c != null)
                    .Select(c => c.Id);
 
                // Milestone label'larını topla
                var milestoneLabels = MilestoneConfig.Milestones
                    .Select(m => m.Label);
 
                UnlockRegistry.Instance.Init(shapeIds, cardIds);
                UnlockRegistry.Instance.InitMilestones(milestoneLabels);
                var lockedShapeIds = ShapeLibrary.Shapes
                    .Where(s => s != null && s.LockedByDefault)
                    .Select(s => s.Id);
                UnlockRegistry.Instance.RegisterLockedShapes(lockedShapeIds);
            }
            if (ShapeLibrary != null)
            {
                var shapeIds = ShapeLibrary.Shapes
                    .Where(s => s != null)
                    .Select(s => s.Id);
            }
           
            NewRun();
        }

        private void OnDestroy()
        {
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
                if (Keyboard.current.qKey.wasPressedThisFrame) _currentRot = PrevRot(_currentRot);
                if (Keyboard.current.eKey.wasPressedThisFrame) _currentRot = NextRot(_currentRot);
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

            PlacementSystem.Place(_board, _currentPiece, anchor, _currentRot);
            FrameFeedbackController.Instance?.OnDrop(_currentPiece.BlockColor);
            // FX: yerleştirme punch
            BoardFX.PlayPlaceFX(BoardView, _currentPiece, anchor, _currentRot);
            AudioManager.Instance.PlaySFX(PlacePiece);
            // Stats
            RunStatsTracker.Instance?.RecordPlacement();

            // Line clear
            var (cleared, tileValueSum, clearedRows, clearedCols, snapshots) =
                LineClearSystem.ClearLines(_board);
       
            if (cleared > 0)
            {
                FrameFeedbackController.Instance?.OnLineClear(cleared);
                if (LineClearVFX != null)
                {
                    LineClearVFX.Play(
                        clearedRows,
                        clearedCols,
                        snapshots,
                        BoardView,
                        _board.Width,
                        _board.Height,
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
            }
                
            // Combo: placement bildirimi (clear yoksa charge düşer)
            _comboSystem.OnPlacement(hadClear: cleared > 0);
            
            if (_comboSystem.Multiplier > _maxComboReached)
                _maxComboReached = _comboSystem.Multiplier;
            
            // Shape Card bonus hesapla
            float shapeBonus = 0f;
            var shapeCardReg = ShapeCardEffectRegistry.Instance;
            if (shapeCardReg != null && shapeCardReg.HasAnyEffect(_currentPiece.Id))
            {
                float bonusPerTile = shapeCardReg.GetScoreBonus(_currentPiece.Id);
                if (bonusPerTile > 0f)
                    shapeBonus = bonusPerTile * _currentPiece.GetCells(_currentRot).Count;
            }

// Normal skor — line clear yoksa tileValueSum=0, sorun yok
            int gainedScore = _scoreSystem.ResolveAfterPlacement(
                tileValueSum,
                _comboSystem.Multiplier,
                _globalScoreMultiplier
            );

// Shape bonus ayrı ekleniyor — tileValueSum=0 engelini aşar
            if (shapeBonus > 0f)
            {
                gainedScore += Mathf.RoundToInt(
                    shapeBonus * _comboSystem.Multiplier * _globalScoreMultiplier);
            }
            _score += gainedScore;

            // Milestone: score güncelle
            _milestoneSystem?.OnScoreChanged(_score);

            // Stats: combo
            RunStatsTracker.Instance?.RecordCombo(_comboSystem.Multiplier);

            // Score UI
            if (gainedScore != 0)
                ScoreView?.AddScoreGain(_score, gainedScore);
            else
                ScoreView?.SetScore(_score);

            // Pool güncelle
            _piecePool.RemoveAt(_selectedPoolIndex);
            
            if (_piecePool.Count == 0)
                GenerateNewPool();
            else
            {
                _selectedPoolIndex = 0;
                SpawnNextFromPool();
            }

            // Dead pool kontrolü
            if (!HasAnyValidMoveInPool())
            { 
                HandleDeadPool(); 
                return;
            }
            int remaining = _milestoneSystem?.PiecesRemaining ?? int.MaxValue;
            FrameFeedbackController.Instance?.OnCritical(remaining);

            // Milestone: piece sayacı — pool işlemleri bittikten sonra
            _milestoneSystem?.OnPiecePlaced();

            _poolDirty = true;
        }

        // ── Run ──────────────────────────────────────────────────────────────
        private void NewRun()
        {   
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
            int baseMaxCharge  = 3; // ComboSystem default
            int barExpansion   = Mathf.RoundToInt(
                reg?.GetEffect(_upgradeLibrary?.Get("upgrade_combo_bar")) ?? 0f);
            _comboSystem.SetMaxCharge(baseMaxCharge + barExpansion);
 
            // StartingCombo: base multiplier 1.0 → 1.1 → 1.2 → 1.3
            float startingBonus = reg?.GetEffect(
                _upgradeLibrary?.Get("upgrade_starting_combo")) ?? 0f;
            _comboSystem.SetBaseMultiplier(1f + startingBonus);
 
            // ComboGainBoost: BonusPerClear 0.1 → 0.13 → ...
            // Reset çağrısı BonusPerClear'ı sıfırlamaz, SetBonusPerClear ile base set ediyoruz
            float comboGainBonus = reg?.GetEffect(
                _upgradeLibrary?.Get("upgrade_combo_gain")) ?? 0f;
            _comboSystem.SetBonusPerClear(0.1f + comboGainBonus); // 0.1 base + upgrade bonus

            _comboSystem.Reset();
            if (_milestoneSystem != null)
            {
                // Pool limit'i güncelle (upgrade değişmiş olabilir)
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
            // Pool reroll butonu
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

            for (int i = 0; i < 3; i++)
            {
                var piece = ShapeSpawnService.GetRandomWeighted(ShapeLibrary);
                if (piece != null)
                    _piecePool.Add(piece);
            }

            _selectedPoolIndex = 0;
            _currentPiece      = _piecePool[0];
            _currentRot        = Rotation.R0;
            if(!skipValidCheck && !HasAnyValidMoveInPool())
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
 
            // Mevcut pool'u temizle, yenisini üret
            _milestoneSystem?.EnsureMinimumRemaining(6);
            GenerateNewPool(skipValidCheck: false);
            
            _poolDirty = true;
        }
        // ── Dead Pool ────────────────────────────────────────────────────────
        private void HandleDeadPool()
        {
            // DeadPoolRevive upgrade kontrolü
            int reviveLevel = Mathf.RoundToInt(
                UpgradeRegistry.Instance?.GetEffect(
                    _upgradeLibrary?.Get("upgrade_dead_pool_revive")) ?? 0f);
 
            if (reviveLevel > 0 && _upgradeRevivesUsed < reviveLevel)
            {
                _upgradeRevivesUsed++;
                Debug.Log($"[DeadPool] Upgrade revive kullanıldı ({_upgradeRevivesUsed}/{reviveLevel})");
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

            OnGameOver();
        }

        // ── Milestone Handlers ───────────────────────────────────────────────
        private void HandleMilestoneReached(int coinReward, MilestoneData data)
        {
            // CoinGainBoost — %4 per level, level başına 0.04
            float coinMultiplier = 1f + (UpgradeRegistry.Instance?.GetEffect(
                _upgradeLibrary?.Get("upgrade_coin_gain")) ?? 0f);
 
            int total = Mathf.RoundToInt((coinReward + _coinBonusPerMilestone) * coinMultiplier);
 
            CoinWallet.Instance?.Earn(total);
            _coins += total;
 
            Debug.Log($"[Milestone] {data.Label} → +{total} coin (x{coinMultiplier:0.00})");
 
            MilestoneView?.PlayMilestoneReachedFX();
            FrameFeedbackController.Instance?.OnMilestone();
 
            // İlk kez bu milestone'a ulaşıldı mı?
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
 
                // MaxMilestoneReached güncelle — buton lock kontrolü için
                int current = PlayerPrefs.GetInt("MaxMilestoneReached", 0);
                if (_milestoneSystem.CurrentMilestoneIndex > current)
                    PlayerPrefs.SetInt("MaxMilestoneReached", _milestoneSystem.CurrentMilestoneIndex);
            }
 
            if (CardPool != null && CardPool.Count > 0)
                CardSelectionUI.Instance?.Show(CardPool, OnCardPicked, newlyUnlockedCard, _cardRerollsRemaining );
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

        // ── Helpers ──────────────────────────────────────────────────────────
        private static Rotation NextRot(Rotation r) => (Rotation)(((int)r + 1) & 3);
        private static Rotation PrevRot(Rotation r) => (Rotation)(((int)r + 3) & 3);
        private void OnCardPicked(CardSO card)
        {
            if (card == null) return;
            
            CardEffectApplier.Apply(
                card,
                _comboSystem,
                _milestoneSystem,
                ref _cardDeadPoolReroll,
                ref _globalScoreMultiplier,
                ref _coinBonusPerMilestone
            );
            CardInventoryUI.Instance?.AddCard(card);
            Debug.Log($"[Card] Seçildi: {card.CardName}");
        }
        private void OnValidate()
        {
            if (Width  <= 0) Width  = 8;
            if (Height <= 0) Height = 8;
        }
    }
}