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
        
        
        [SerializeField] private List<CardSO> CardPool;
        private float _globalScoreMultiplier  = 1f;
        private int   _coinBonusPerMilestone  = 0;
        
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

        private List<PieceDefinition> _piecePool         = new List<PieceDefinition>(3);
        private int                   _selectedPoolIndex = -1;
        private PieceDefinition       _currentPiece;
        private Rotation              _currentRot        = Rotation.R0;

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
        // ── Unity ────────────────────────────────────────────────────────────
        private void Start()
        {   
            _milestoneSystem = new MilestoneSystem(MilestoneConfig);
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
            }
            if (ShapeLibrary != null)
            {
                var shapeIds = ShapeLibrary.Shapes
                    .Where(s => s != null)
                    .Select(s => s.Id);
 
                ShapeUpgradeRegistry.Instance.Load(ShapeLibrary.Shapes);
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
            BoardView.Render(_board, _ghost);

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

            // Skor — multiplier ComboSystem'den
            int gainedScore = _scoreSystem.ResolveAfterPlacement(
                tileValueSum,
                _comboSystem.Multiplier,
                _globalScoreMultiplier
            );
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

            // ─── DÜZELTME ───────────────────────────────────────────────────
            // Critical feedback'i OnPiecePlaced'TEN ÖNCE ver.
            // Böylece PiecesRemaining henüz 0'a düşmeden doğru değeri okuruz.
            // OnPiecePlaced içinde PiecesRemaining 0'a düşerse OnPoolLimitExhausted
            // → HandlePoolLimitExhausted → OnGameOver zinciri tetiklenir ve
            // FrameFeedbackController zaten GameOver state'ine geçer — Critical
            // o noktada zaten irrelevant olur.
            // ────────────────────────────────────────────────────────────────
            int remaining = _milestoneSystem?.PiecesRemaining ?? int.MaxValue;
            FrameFeedbackController.Instance?.OnCritical(remaining);

            // Milestone: piece sayacı — pool işlemleri bittikten sonra
            _milestoneSystem?.OnPiecePlaced();

            _poolDirty = true;
        }

        // ── Run ──────────────────────────────────────────────────────────────
        private void NewRun()
        {
            if (Width  <= 0) Width  = 8;
            if (Height <= 0) Height = 8;
            Time.timeScale = 1f;
            GameOverUI.Instance?.Hide(); 
            Time.timeScale = 1f;

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
            _comboSystem.Reset();
            _milestoneSystem?.Reset();

            RunStatsTracker.Instance?.Reset();

            ScoreView?.SetScore(0);
            BoardView.Build(_board);

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

        // ── Dead Pool ────────────────────────────────────────────────────────
        private void HandleDeadPool()
        {
          
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
            // Coin'i kalıcı wallet'a ekle
            int total = coinReward + _coinBonusPerMilestone;
            CoinWallet.Instance?.Earn(total);
            _coins += total;  // local tracking için de tut
 
            Debug.Log($"[Milestone] {data.Label} → +{total} coin | Wallet: {CoinWallet.Instance?.Balance}");
 
            MilestoneView?.PlayMilestoneReachedFX();
            FrameFeedbackController.Instance?.OnMilestone();
 
            // İlk kez bu milestone'a ulaşıldı mı?
            CardSO newlyUnlockedCard = null;
 
            if (UnlockRegistry.Instance != null &&
                UnlockRegistry.Instance.IsFirstMilestoneReach(data.Label))
            {
                // Kilitli kartlardan rastgele birini unlock et
                var lockedCards = CardPool
                    .Where(c => c != null && c.LockedByDefault && !c.IsUnlocked)
                    .ToList();
 
                if (lockedCards.Count > 0)
                {
                    int pick = UnityEngine.Random.Range(0, lockedCards.Count);
                    newlyUnlockedCard = lockedCards[pick];
                    UnlockRegistry.Instance.UnlockCard(newlyUnlockedCard.Id);
 
                    Debug.Log($"[Unlock] Yeni kart açıldı: {newlyUnlockedCard.CardName}");
 
                    // MilestoneView'da "New Card Earned!" göster
                    MilestoneView?.ShowNewCardEarned(newlyUnlockedCard.CardName);
                }
            }
 
            // Kart seçim ekranını aç — yeni kart en sola, NEW badge ile
            if (CardPool != null && CardPool.Count > 0)
            {
                CardSelectionUI.Instance?.Show(CardPool, OnCardPicked, newlyUnlockedCard);
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