using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// MainMenu'deki sağ taraftaki 6x6 preview board.
    ///
    /// İşleyiş:
    /// 1. Start'ta 36 hücreyi spawn et
    /// 2. Köşelere statik dekoratif L-bracket'ler yerleştir
    /// 3. Shape listesinden sırayla aç/kapat (sonsuz döngü)
    ///
    /// Hierarchy:
    ///  PreviewBoard (bu script)
    ///   └── [36 PreviewCellView — otomatik spawn]
    /// </summary>
    public sealed class PreviewBoardController : MonoBehaviour
    {
        // ── Config ───────────────────────────────────────────────────────────
        private const int ROWS = 6;
        private const int COLS = 6;

        [Header("Cell")]
        [SerializeField] private PreviewCellView _cellPrefab;
        [SerializeField] private Transform       _cellContainer;   // boş bırak → transform

        [Header("Animation Timing")]
        [SerializeField] private float _staggerInDelay  = 0.07f;
        [SerializeField] private float _staggerOutDelay = 0.04f;
        [SerializeField] private float _holdDuration    = 1.2f;
        [SerializeField] private float _betweenShapes   = 0.4f;

        [Header("Shape Colors")]
        [SerializeField] private Color _colorCyan   = new Color(0.19f, 0.72f, 0.91f);
        [SerializeField] private Color _colorPink   = new Color(0.88f, 0.19f, 0.69f);
        [SerializeField] private Color _colorGold   = new Color(0.91f, 0.63f, 0.13f);
        [SerializeField] private Color _colorPurple = new Color(0.47f, 0.28f, 0.82f);
        [SerializeField] private Color _colorGreen  = new Color(0.19f, 0.78f, 0.25f);
        [SerializeField] private Color _colorOrange = new Color(0.91f, 0.41f, 0.13f);
        [SerializeField] private Color _colorRed    = new Color(0.82f, 0.19f, 0.19f);

        // Runtime
        private PreviewCellView[,] _cells;
        private Coroutine          _loopRoutine;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Start()
        {
            if (_cellContainer == null) _cellContainer = transform;
            BuildCells();
            SetupCorners();
            StartLoop();
        }

        private void OnDisable() => StopLoop();

        // ── Public API ───────────────────────────────────────────────────────

        public void StartLoop()
        {
            StopLoop();
            _loopRoutine = StartCoroutine(ShapeLoop());
        }

        public void StopLoop()
        {
            if (_loopRoutine != null)
            {
                StopCoroutine(_loopRoutine);
                _loopRoutine = null;
            }
        }

        // ── Cell Setup ───────────────────────────────────────────────────────

        private void BuildCells()
        {
            _cells = new PreviewCellView[ROWS, COLS];

            // Mevcut child'ları temizle (eski Cell_XX'ler varsa)
            for (int i = _cellContainer.childCount - 1; i >= 0; i--)
                DestroyImmediate(_cellContainer.GetChild(i).gameObject);

            for (int r = 0; r < ROWS; r++)
            {
                for (int c = 0; c < COLS; c++)
                {
                    var cell = Instantiate(_cellPrefab, _cellContainer);
                    cell.transform.localScale = Vector3.one;
                    cell.name = $"Cell_{r}_{c}";
                    cell.SetEmpty(instant: true);
                    _cells[r, c] = cell;
                }
            }
        }

        private void SetupCorners()
        {
            // Sol-üst bracket (cyan)
            _cells[0, 0].SetAsCorner(_colorCyan);
            _cells[0, 1].SetAsCorner(_colorCyan);
            _cells[1, 0].SetAsCorner(_colorCyan);

            // Sağ-üst bracket (pink)
            _cells[0, 5].SetAsCorner(_colorPink);
            _cells[0, 4].SetAsCorner(_colorPink);
            _cells[1, 5].SetAsCorner(_colorPink);

            // Sol-alt bracket (gold)
            _cells[5, 0].SetAsCorner(_colorGold);
            _cells[5, 1].SetAsCorner(_colorGold);
            _cells[4, 0].SetAsCorner(_colorGold);

            // Sağ-alt bracket (purple)
            _cells[5, 5].SetAsCorner(_colorPurple);
            _cells[5, 4].SetAsCorner(_colorPurple);
            _cells[4, 5].SetAsCorner(_colorPurple);
        }

        // ── Shape Loop ───────────────────────────────────────────────────────

        private IEnumerator ShapeLoop()
        {
            var shapes = BuildShapeList();
            int index  = 0;

            while (true)
            {
                yield return PlayShape(shapes[index]);
                yield return new WaitForSeconds(_betweenShapes);
                index = (index + 1) % shapes.Count;
            }
        }

        private IEnumerator PlayShape(PreviewShape shape)
        {
            // Filled hücreleri topla — corner'ları atla
            var filled = new List<Vector2Int>();
            for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
            {
                if (shape.Mask[r, c] && !_cells[r, c].IsCornerCell)
                    filled.Add(new Vector2Int(r, c));
            }

            // Aç — stagger
            foreach (var p in filled)
            {
                _cells[p.x, p.y].PopIn(shape.Color);
                yield return new WaitForSeconds(_staggerInDelay);
            }

            // Bekle
            yield return new WaitForSeconds(_holdDuration);

            // Kapat — reverse stagger
            for (int i = filled.Count - 1; i >= 0; i--)
            {
                var p = filled[i];
                _cells[p.x, p.y].PopOut();
                yield return new WaitForSeconds(_staggerOutDelay);
            }
        }

        // ── Shape Data ───────────────────────────────────────────────────────

        private List<PreviewShape> BuildShapeList()
        {
            var list = new List<PreviewShape>
            {
                // Z
                new PreviewShape(_colorRed, new int[,] {
                    {0,0,0,0,0,0},
                    {0,1,1,0,0,0},
                    {0,0,1,1,0,0},
                    {0,0,0,0,0,0},
                    {0,0,0,0,0,0},
                    {0,0,0,0,0,0},
                }),
                // T
                new PreviewShape(_colorCyan, new int[,] {
                    {0,0,0,0,0,0},
                    {0,0,0,0,0,0},
                    {0,1,1,1,0,0},
                    {0,0,1,0,0,0},
                    {0,0,1,0,0,0},
                    {0,0,0,0,0,0},
                }),
                // Square 2x2
                new PreviewShape(_colorGreen, new int[,] {
                    {0,0,0,0,0,0},
                    {0,0,1,1,0,0},
                    {0,0,1,1,0,0},
                    {0,0,0,0,0,0},
                    {0,0,0,0,0,0},
                    {0,0,0,0,0,0},
                }),
                // L
                new PreviewShape(_colorOrange, new int[,] {
                    {0,0,0,0,0,0},
                    {0,1,0,0,0,0},
                    {0,1,0,0,0,0},
                    {0,1,0,0,0,0},
                    {0,1,1,1,0,0},
                    {0,0,0,0,0,0},
                }),
                // Cross
                new PreviewShape(_colorPurple, new int[,] {
                    {0,0,0,0,0,0},
                    {0,0,1,0,0,0},
                    {0,1,1,1,0,0},
                    {0,0,1,0,0,0},
                    {0,0,0,0,0,0},
                    {0,0,0,0,0,0},
                }),
                // I4
                new PreviewShape(_colorPink, new int[,] {
                    {0,0,0,0,0,0},
                    {0,0,0,0,0,0},
                    {0,1,1,1,1,0},
                    {0,0,0,0,0,0},
                    {0,0,0,0,0,0},
                    {0,0,0,0,0,0},
                }),
                // Big 3x3
                new PreviewShape(_colorGold, new int[,] {
                    {0,0,0,0,0,0},
                    {0,1,1,1,0,0},
                    {0,1,1,1,0,0},
                    {0,1,1,1,0,0},
                    {0,0,0,0,0,0},
                    {0,0,0,0,0,0},
                }),
            };

            return list;
        }

        // ── Helper struct ────────────────────────────────────────────────────

        private struct PreviewShape
        {
            public Color  Color;
            public bool[,] Mask;

            public PreviewShape(Color color, int[,] mask)
            {
                Color = color;
                int rows = mask.GetLength(0);
                int cols = mask.GetLength(1);
                Mask = new bool[rows, cols];
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        Mask[r, c] = mask[r, c] == 1;
            }
        }
    }
}