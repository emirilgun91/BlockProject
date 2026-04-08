using System.Collections.Generic;
using RogueBlockBlast.Content;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Seçilen kartların ikon listesini yönetir.
    /// 2 sütunlu grid, otomatik büyür, scroll destekler.
    ///
    /// Hierarchy:
    ///  CardInventoryUI (bu script)
    ///   └── ScrollRect
    ///        └── Viewport
    ///             └── Content  ← GridLayoutGroup + ContentSizeFitter buraya
    ///                  └── [CardSlot prefabları spawn olur]
    ///
    /// RunController'da:
    ///   CardInventoryUI.Instance?.AddCard(card);
    ///   CardInventoryUI.Instance?.Clear();
    /// </summary>
    public sealed class CardInventoryUI : MonoBehaviour
    {
        public static CardInventoryUI Instance { get; private set; }

        [Header("References")]
        [SerializeField] private CardSlotView _slotPrefab;
        [SerializeField] private RectTransform _content;     // GridLayoutGroup'un olduğu Content

        [Header("Grid")]
        [SerializeField] private int   _columns    = 2;
        [SerializeField] private float _slotSize   = 40f;
        [SerializeField] private float _slotSpacing = 6f;

        // ── State ────────────────────────────────────────────────────────────
        // CardSO.Id → (CardSO, count, SlotView)
        private readonly Dictionary<string, (CardSO card, int count, CardSlotView slot)>
            _inventory = new();

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            SetupGrid();
        }

        private void SetupGrid()
        {
            if (_content == null) return;

            var grid = _content.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = _content.gameObject.AddComponent<GridLayoutGroup>();

            grid.cellSize        = new Vector2(_slotSize, _slotSize);
            grid.spacing         = new Vector2(_slotSpacing, _slotSpacing);
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = _columns;
            grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment  = TextAnchor.UpperLeft;

            var csf = _content.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = _content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Kart ekler. Aynı kart tekrar seçilirse stack badge güncellenir.
        /// RunController.OnCardPicked içinde çağır.
        /// </summary>
        public void AddCard(CardSO card)
        {
            Debug.Log($"[Inventory] Id:{card.Id} | mevcut keyler:{string.Join(',', _inventory.Keys)}");
       
            if (card == null) return;

            if (_inventory.TryGetValue(card.Id, out var entry))
            {
                // Zaten var — stack artır
                int newCount = entry.count + 1;
                entry.slot.Bind(card, newCount);
                _inventory[card.Id] = (card, newCount, entry.slot);
            }
            else
            {
                // Yeni slot oluştur
                var slot = Instantiate(_slotPrefab, _content);
                slot.transform.localScale = Vector3.one;
                slot.Bind(card, 1);
                _inventory[card.Id] = (card, 1, slot);
            }
        }

        /// <summary>
        /// Run başında tüm slotları temizler.
        /// RunController.NewRun içinde çağır.
        /// </summary>
        public void Clear()
        {
            foreach (var entry in _inventory.Values)
            {
                if (entry.slot != null)
                    Destroy(entry.slot.gameObject);
            }
            _inventory.Clear();

            CardTooltip.Instance?.Hide();
        }

        /// <summary>Aktif kart sayısını döndürür (stack dahil).</summary>
        public int TotalCardCount()
        {
            int total = 0;
            foreach (var e in _inventory.Values) total += e.count;
            return total;
        }
    }
}