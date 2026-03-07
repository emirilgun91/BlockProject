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
        public int Width = 8;
        public int Height = 8;

        private BoardModel _board;
        private RunModel _run;

        private ScoreSystem _scoreSystem = new ScoreSystem();
        private int _score = 0;
        private int _bestScore;
        
        private List<PieceDefinition> _piecePool = new List<PieceDefinition>(3);
        private int _selectedPoolIndex = -1;
        
        private PieceDefinition _currentPiece;
        private Rotation _currentRot = Rotation.R0;

        private readonly HashSet<Vector2Int> _ghost = new();
        
        private int _freeDeadPoolReroll = 1;
        private int _cardDeadPoolReroll = 0;   // karttan gelen hak
        private const int CardRerollCost = 300;
        
        [SerializeField] private PoolView PoolView;

        private void Start()
        {
            if (MainCamera == null) 
                MainCamera = Camera.main;

            _bestScore = PlayerPrefs.GetInt("BEST_SCORE", 0);

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

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                NewRun();

            _ghost.Clear();

            var cell = BoardView.TryGetCellUnderMouse(MainCamera);
            bool canPlace = false;

            if (cell.HasValue)
            {
                var anchor = cell.Value;

                canPlace = PlacementSystem.CanPlace(_board, _currentPiece, anchor, _currentRot);

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
            
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                SelectPool(0);

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
                SelectPool(1);

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
                SelectPool(2);
            PoolView.Bind(_piecePool, _selectedPoolIndex);
        }
        private void SelectPool(int index)
        {
            if (index < 0 || index >= _piecePool.Count)
                return;

            _selectedPoolIndex = index;
            _currentPiece = _piecePool[index];
            _currentRot = Rotation.R0;
        }
        private void OnValidate()
        {
            if (Width <= 0) Width = 8;
            if (Height <= 0) Height = 8;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void DoPlace(Vector2Int anchor)
        {
            if (!PlacementSystem.CanPlace(_board, _currentPiece, anchor, _currentRot))
                return;

            PlacementSystem.Place(_board, _currentPiece, anchor, _currentRot);

            int cleared = LineClearSystem.ClearLines(_board);

            int gainedScore = _scoreSystem.ResolveAfterPlacement(cleared);
            _score += gainedScore;

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
            {
                HandleDeadPool();
            }
        }

        private void NewRun()
        {
            if (Width <= 0) Width = 8;
            if (Height <= 0) Height = 8;
            _freeDeadPoolReroll = 1;
            _cardDeadPoolReroll = 0;
            _board = new BoardModel(Width, Height);
            _run = new RunModel();

            _scoreSystem = new ScoreSystem();
            _score = 0;

            BoardView.Build(_board);

            GenerateNewPool();
        }

        private void SpawnNextFromPool()
        {
            if (_piecePool.Count == 0)
            {
                GenerateNewPool();
                return;
            }

            if (_selectedPoolIndex < 0 || _selectedPoolIndex >= _piecePool.Count)
                return;

            _currentPiece = _piecePool[_selectedPoolIndex];
            _currentRot = Rotation.R0;
        }

        private void OnGameOver()
        {
            if (_score > _bestScore)
            {
                _bestScore = _score;
                PlayerPrefs.SetInt("BEST_SCORE", _bestScore);
                PlayerPrefs.Save();
            }

            Debug.Log($"GAME OVER. Score: {_score}  Best: {_bestScore}  (Press R to restart)");
        }

        private bool HasAnyValidMoveInPool()
        {
            for (int p = 0; p < _piecePool.Count; p++)
            {
                var piece = _piecePool[p];

                for (int r = 0; r < 4; r++)
                {
                    for (int y = 0; y < _board.Height; y++)
                    {
                        for (int x = 0; x < _board.Width; x++)
                        {
                            if (PlacementSystem.CanPlace(
                                    _board,
                                    piece,
                                    new Vector2Int(x, y),
                                    (Rotation)r))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
        private void HandleDeadPool()
        {
            // Önce ücretsiz hak
            if (_freeDeadPoolReroll > 0)
            {
                _freeDeadPoolReroll--;
                GenerateNewPool();
                return;
            }

            // Kart hakları
            if (_cardDeadPoolReroll > 0)
            {
                if (_score >= CardRerollCost)
                {
                    _cardDeadPoolReroll--;
                    _score -= CardRerollCost;
                    GenerateNewPool();
                    return;
                }
            }

            OnGameOver();
        }
        
        private static Rotation NextRot(Rotation r) => (Rotation)(((int)r + 1) & 3);
        private static Rotation PrevRot(Rotation r) => (Rotation)(((int)r + 3) & 3);
        // ReSharper disable Unity.PerformanceAnalysis
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
            _currentPiece = _piecePool[0];
            _currentRot = Rotation.R0;
            if (!HasAnyValidMoveInPool())
            {
                HandleDeadPool();
            }
        }
    }
}