using System.Collections.Generic;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using RogueBlockBlast.UI;
using UnityEngine;
using UnityEngine.InputSystem;

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

        // ── Unity ────────────────────────────────────────────────────────────
        private void Start()
        {
            _milestoneSystem = new MilestoneSystem(MilestoneConfig);
            _milestoneSystem.OnMilestoneReached   += HandleMilestoneReached;
            _milestoneSystem.OnPoolLimitExhausted += HandlePoolLimitExhausted;
            MilestoneView?.Bind(_milestoneSystem);
            ComboView?.Bind(_comboSystem);
            
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

            // FX: yerleştirme punch
            BoardFX.PlayPlaceFX(BoardView, _currentPiece, anchor, _currentRot);

            // Stats
            RunStatsTracker.Instance?.RecordPlacement();

            // Line clear
            var (cleared, clearedRows, clearedCols) = LineClearSystem.ClearLines(_board);

            if (cleared > 0)
            {
                BoardFX.PlayLineClearFX(BoardView, _board.Width, _board.Height, clearedRows, clearedCols);
                RunStatsTracker.Instance?.RecordClear(cleared, 0);
                _comboSystem.OnLineClear(cleared);
            }

            // Combo: placement bildirimi (clear yoksa charge düşer)
            _comboSystem.OnPlacement(hadClear: cleared > 0);

            // Skor — multiplier ComboSystem'den
            int gainedScore = _scoreSystem.ResolveAfterPlacement(cleared, _comboSystem.Multiplier);
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
            { HandleDeadPool(); 
                return;
            }

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
            // Game over ekranı açıksa kapat, timeScale sıfırla
            Time.timeScale = 1f;

            _freeDeadPoolReroll = 1;
            _cardDeadPoolReroll = 0;
            _score              = 0;
            _coins              = 0;

            _board       = new BoardModel(Width, Height);
            _run         = new RunModel();
            _scoreSystem = new ScoreSystem();

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

        private void GenerateNewPool()
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
            
            _poolDirty = true;
        }

        // ── Game Over ────────────────────────────────────────────────────────
        private void OnGameOver()
        {
          
            GameOverUI.Instance?.Show(_score);
        }

        // ── Dead Pool ────────────────────────────────────────────────────────
        private void HandleDeadPool()
        {
          
            if (_freeDeadPoolReroll > 0)
            {
                _freeDeadPoolReroll--;
                _milestoneSystem?.EnsureMinimumRemaining(6);
                GenerateNewPool();
                return;
            }

            if (_cardDeadPoolReroll > 0 && _score >= CardRerollCost)
            {
                _cardDeadPoolReroll--;  
                _score -= CardRerollCost;
                ScoreView?.AddScoreGain(_score, -CardRerollCost);
                _milestoneSystem?.EnsureMinimumRemaining(6);
                GenerateNewPool();
                _poolDirty = true;
                return;
            }

            OnGameOver();
        }

        // ── Milestone Handlers ───────────────────────────────────────────────
        private void HandleMilestoneReached(int coinReward, MilestoneData data)
        {
            _coins += coinReward;
            Debug.Log($"[Milestone] {data.Label} reached! +{coinReward} coins → total: {_coins}");

            MilestoneView?.PlayMilestoneReachedFX();

            // Kart seçim ekranı — kart sistemi hazır olunca aç:
            // CardSelectionUI.Instance.Show(allCards, OnCardPicked);
        }

        private void HandlePoolLimitExhausted()
        {
            OnGameOver();
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

        private void OnValidate()
        {
            if (Width  <= 0) Width  = 8;
            if (Height <= 0) Height = 8;
        }
    }
}