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
        public Camera MainCamera;

        [Header("Config")]
        public int Width  = 8;
        public int Height = 8;

        private BoardModel _board;
        private RunModel   _run;

        private ScoreSystem _scoreSystem = new ScoreSystem();
        private int         _score       = 0;

        private List<PieceDefinition> _piecePool       = new List<PieceDefinition>(3);
        private int                   _selectedPoolIndex = -1;

        private PieceDefinition _currentPiece;
        private Rotation        _currentRot = Rotation.R0;

        private readonly HashSet<Vector2Int> _ghost = new();
        private bool _poolDirty = true;

        private int        _freeDeadPoolReroll = 1;
        private int        _cardDeadPoolReroll = 0;
        private const int  CardRerollCost      = 300;

        [SerializeField] private PoolView  PoolView;
        [SerializeField] private ScoreView ScoreView;

        // ── Unity ────────────────────────────────────────────────────────────
        private void Start()
        {
            NewRun();
        }

        private void Update()
        {
            if (_board == null || _currentPiece == null)
                return;

            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                _currentRot = PrevRot(_currentRot);

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                _currentRot = NextRot(_currentRot);

            // R → sadece editor/debug için kalsın, GameOverUI Retry zaten sahneyi yeniden yükler
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                NewRun();

            _ghost.Clear();

            var cell = BoardView.TryGetClampedCellUnderMouse(MainCamera, _currentPiece, _currentRot);

            if (cell.HasValue)
            {
                var anchor   = cell.Value;
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

            if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectPool(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectPool(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectPool(2);

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

            // ── Stats: placement ─────────────────────────────────────────────
            RunStatsTracker.Instance?.RecordPlacement();

            int cleared = LineClearSystem.ClearLines(_board);

            // ── Stats: line clear ────────────────────────────────────────────
            if (cleared > 0)
                RunStatsTracker.Instance?.RecordClear(cleared, 0);
                // Not: eğer LineClearSystem row/col ayrı dönüyorsa ikinci parametreyi güncelle

            int gainedScore = _scoreSystem.ResolveAfterPlacement(cleared);
            _score += gainedScore;

            // ── Stats: combo ─────────────────────────────────────────────────
            RunStatsTracker.Instance?.RecordCombo(_scoreSystem.CurrentMultiplier);

            if (gainedScore != 0)
                ScoreView?.AddScoreGain(_score, gainedScore);
            else
                ScoreView?.SetScore(_score);

            // Kullanılan parçayı havuzdan kaldır
            _piecePool.RemoveAt(_selectedPoolIndex);

            if (_piecePool.Count == 0)
                GenerateNewPool();
            else
            {
                _selectedPoolIndex = 0;
                SpawnNextFromPool();
            }

            if (!HasAnyValidMoveInPool())
                HandleDeadPool();

            _poolDirty = true;
        }

        // ── Run ──────────────────────────────────────────────────────────────
        private void NewRun()
        {
            if (Width  <= 0) Width  = 8;
            if (Height <= 0) Height = 8;

            _freeDeadPoolReroll = 1;
            _cardDeadPoolReroll = 0;

            _board = new BoardModel(Width, Height);
            _run   = new RunModel();

            _scoreSystem = new ScoreSystem();
            _score       = 0;

            // ── Stats sıfırla ────────────────────────────────────────────────
            RunStatsTracker.Instance?.Reset();

            ScoreView?.SetScore(0);
            BoardView.Build(_board);

            GenerateNewPool();
            _poolDirty = true;
        }

        private void SpawnNextFromPool()
        {
            if (_piecePool.Count == 0) { GenerateNewPool(); return; }

            if (_selectedPoolIndex < 0 || _selectedPoolIndex >= _piecePool.Count)
                return;

            _currentPiece = _piecePool[_selectedPoolIndex];
            _currentRot   = Rotation.R0;
        }

        // ── Game Over ────────────────────────────────────────────────────────
        private void OnGameOver()
        {
            // Best score artık GameOverUI.Show() içinde PlayerPrefs'e yazılıyor.
            // RunController'da ayrıca kaydetmiyoruz — tek kaynak GameOverUI.
            GameOverUI.Instance?.Show(_score);
        }

        // ── Dead Pool ────────────────────────────────────────────────────────
        private void HandleDeadPool()
        {
            if (_freeDeadPoolReroll > 0)
            {
                _freeDeadPoolReroll--;
                GenerateNewPool();
                return;
            }

            if (_cardDeadPoolReroll > 0 && _score >= CardRerollCost)
            {
                _cardDeadPoolReroll--;
                _score -= CardRerollCost;
                ScoreView?.AddScoreGain(_score, -CardRerollCost);
                GenerateNewPool();
                _poolDirty = true;
                return;
            }

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
                    if (PlacementSystem.CanPlace(
                            _board, piece,
                            new Vector2Int(x, y),
                            (Rotation)r))
                        return true;
                }
            }

            return false;
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

            if (!HasAnyValidMoveInPool())
                HandleDeadPool();

            _poolDirty = true;
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