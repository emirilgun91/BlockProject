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
        [Tooltip("KAPALI (varsayılan): Inspector'daki GridLayoutGroup ayarların olduğu gibi kalır — " +
                 "hücre boyutu, padding, sütun sayısı elle ayarlanır ve script hiçbirine dokunmaz.\n" +
                 "AÇIK: aşağıdaki değerler her açılışta grid'e yazılır (elle yaptığın ayarlar ezilir).")]
        [SerializeField] private bool  _applyGridFromScript = false;

        [SerializeField] private int   _columns     = 3;
        [SerializeField] private float _slotSize    = 68f;
        [SerializeField] private float _slotSpacing = 8f;

        [Header("Auto Fit")]
        [Tooltip("AÇIK (varsayılan): hücre boyutu ve sütun sayısı panelin GERÇEK " +
                 "genişliğinden hesaplanır ve slotlar paneli düzgün doldurur.\n\n" +
                 "Sabit hücre boyutu, panel yeniden skinlendiğinde ikonların minicik " +
                 "kalmasına yol açıyordu. Auto-fit bunu kendiliğinden düzeltir.")]
        [SerializeField] private bool _autoFit = true;

        [Tooltip("Hedeflenen slot kenarı (px). Sütun sayısı buna en yakın düşecek " +
                 "şekilde seçilir, sonra hücre paneli tam dolduracak biçimde büyütülür.")]
        [SerializeField] private float _autoFitTargetSlot = 86f;

        [SerializeField] private float _autoFitSpacing = 10f;

        [Tooltip("Sütun sayısı sınırı. Panel çok genişse gereksiz sütun açılmasın.")]
        [SerializeField] private int _autoFitMaxColumns = 4;

        [Tooltip("Hücre kenarının izin verilen aralığı (px).")]
        [SerializeField] private Vector2 _autoFitSlotRange = new Vector2(48f, 140f);

        [Tooltip("Kenar boşluğu (px) — sol/sağ/üst.")]
        [SerializeField] private float _autoFitPadding = 12f;

        // Panel genişliği değiştiğinde yeniden hesaplamak için son ölçü.
        private float _lastFitWidth = -1f;

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

        /// <summary>
        /// Grid ayarlarını uygular — YALNIZCA _applyGridFromScript açıksa.
        ///
        /// Varsayılan olarak kapalı: Inspector'da elle girdiğin cell size / padding /
        /// sütun sayısı korunur. (Eskiden bu metot her Awake'te üzerine yazıyordu.)
        /// ContentSizeFitter her durumda garanti edilir; o olmadan liste büyüdükçe
        /// kaydırma çalışmaz.
        /// </summary>
        private void SetupGrid()
        {
            if (_content == null) return;

            var csf = _content.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = _content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            if (_autoFit) { ApplyAutoFit(force: true); return; }

            if (!_applyGridFromScript) return;

            var grid = _content.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = _content.gameObject.AddComponent<GridLayoutGroup>();

            grid.cellSize        = new Vector2(_slotSize, _slotSize + CardSlotView.LiveValueBandHeight);
            grid.spacing         = new Vector2(_slotSpacing, _slotSpacing);
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = _columns;
            grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment  = TextAnchor.UpperCenter;
        }

        private void LateUpdate()
        {
            if (_autoFit) ApplyAutoFit(force: false);
        }

        /// <summary>
        /// Hücre boyutunu ve sütun sayısını panelin gerçek genişliğinden hesaplar.
        ///
        /// Sabit hücre boyutu yazmak, panel yeniden skinlendiğinde ikonların
        /// minicik kalmasına yol açıyordu. Burada tersi yapılır: önce hedef slot
        /// boyutuna en yakın sütun sayısı seçilir, sonra hücre paneli <b>tam
        /// dolduracak</b> şekilde büyütülür. Kalan boşluk sıfıra iner.
        ///
        /// Genişlik değişmedikçe hiçbir şey yazılmaz — her frame layout
        /// tetiklemenin maliyeti bu kontrolle ödenmez.
        /// </summary>
        private void ApplyAutoFit(bool force)
        {
            if (_content == null) return;

            float width = _content.rect.width;

            // Layout henüz çalışmamışsa ölçü güvenilmez; bir sonraki frame'e bırak.
            if (width < 1f) return;
            if (!force && Mathf.Abs(width - _lastFitWidth) < 0.5f) return;

            _lastFitWidth = width;

            var grid = _content.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = _content.gameObject.AddComponent<GridLayoutGroup>();

            float pad       = Mathf.Max(0f, _autoFitPadding);
            float spacing   = Mathf.Max(0f, _autoFitSpacing);
            float available = width - pad * 2f;

            if (available <= 1f) return;

            // Hedef boyuta en yakın sütun sayısı.
            int maxCols = Mathf.Max(1, _autoFitMaxColumns);
            int columns = Mathf.Clamp(
                Mathf.RoundToInt((available + spacing) / (_autoFitTargetSlot + spacing)),
                1, maxCols);

            // O sütun sayısıyla paneli tam dolduran hücre kenarı.
            float cell = (available - (columns - 1) * spacing) / columns;
            cell = Mathf.Clamp(cell, _autoFitSlotRange.x, _autoFitSlotRange.y);

            grid.cellSize        = new Vector2(cell, cell + CardSlotView.LiveValueBandHeight);
            grid.spacing         = new Vector2(spacing, spacing);
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
            // Yatayda ortalar — hücreler paneli tam doldurmasa da simetrik durur.
            grid.childAlignment  = TextAnchor.UpperCenter;
            grid.padding         = new RectOffset((int)pad, (int)pad, (int)pad, (int)pad);
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Kart ekler. Aynı kart tekrar seçilirse stack badge güncellenir.
        /// RunController.OnCardPicked içinde çağır.
        /// </summary>
        public void AddCard(CardSO card)
        {
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

        /// <summary>
        /// Tüm slotların canlı değer satırını yeniler.
        /// format(card, stackCount) → gösterilecek metin; null dönerse satır gizlenir.
        /// Formatlama RunController'da yapılır — değerler skorlamanın okuduğu
        /// flag/registry'den gelir, burada ikinci bir hesap yok.
        ///
        /// Çağrı noktaları: kart seçimi, envanter değişimi, line clear (Chain Master).
        /// Update() içinde polling YOK.
        /// </summary>
        public void RefreshLiveValues(System.Func<CardSO, int, string> format)
        {
            if (format == null) return;

            foreach (var entry in _inventory.Values)
            {
                if (entry.slot == null || entry.card == null) continue;
                entry.slot.SetLiveValue(format(entry.card, entry.count));
            }
        }

        /// <summary>Aktif kart sayısını döndürür (stack dahil).</summary>
        public int TotalCardCount()
        {
            int total = 0;
            foreach (var e in _inventory.Values) total += e.count;
            return total;
        }

        /// <summary>Seçilmiş kart ID'lerini döndürür — unique filtre için.</summary>
        public IEnumerable<string> GetSelectedCardIds() => _inventory.Keys;

        /// <summary>
        /// Bir kartın slot RectTransform'u — kart tetiklendiğinde efektin nereye
        /// uçacağını bilmesi için. Kart envanterde yoksa null döner.
        ///
        /// Neden efektin hedefi slot: kazanılan şeyin nereden geldiğini gösteren
        /// en doğrudan sinyal, o şeyi veren kartın üstünde bitmesi.
        /// </summary>
        public RectTransform GetSlotRect(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            if (!_inventory.TryGetValue(cardId, out var entry)) return null;
            if (entry.slot == null) return null;
            return entry.slot.transform as RectTransform;
        }
    }
}