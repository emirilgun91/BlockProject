using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using RogueBlockBlast.Core.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RogueBlockBlast.UI
{
    public sealed class BoardView : MonoBehaviour
    {
        [Header("Grid")]
        public Vector2 OriginWorld = Vector2.zero;
        public float   CellSize    = 1f;

        [Header("Prefabs")]
        public TileView TilePrefab;

        [Header("Empty Cell Life")]
        [Tooltip("Boş hücrenin taban rengi.")]
        [SerializeField] private Color _emptyCellColor = new Color32(0x1c, 0x21, 0x32, 0xff);

        [Tooltip("0 = kapalı (varsayılan). Boş hücrelerde çapraz ilerleyen dalganın şiddeti. " +
                 "Tahtanın en büyük ölü yüzeyi 64 aynı koyu kare — bu onu nefes aldırır.")]
        [Range(0f, 1f)]
        [SerializeField] private float _emptyPulseAmount = 0f;

        [SerializeField] private Color _emptyPulseColor    = new Color32(0x2a, 0x35, 0x52, 0xff);
        [SerializeField] private float _emptyPulseSpeed    = 1.2f;
        [Tooltip("Dalganın hücre başına faz farkı — küçük değer uzun dalga.")]
        [SerializeField] private float _emptyPulseWavelength = 0.55f;

        /// <summary>Boş hücre taban rengi — atmosfer bileşenleri runtime'da sürebilir.</summary>
        public Color EmptyCellColor { get => _emptyCellColor; set => _emptyCellColor = value; }

        /// <summary>Dalganın tepe rengi.</summary>
        public Color EmptyPulseColor { get => _emptyPulseColor; set => _emptyPulseColor = value; }

        /// <summary>Dalga şiddeti. 0 = kapalı; varsayılan bu, mevcut sahne etkilenmez.</summary>
        public float EmptyPulseAmount
        {
            get => _emptyPulseAmount;
            set => _emptyPulseAmount = Mathf.Clamp01(value);
        }

        /// <summary>Dalga hızı.</summary>
        public float EmptyPulseSpeed { get => _emptyPulseSpeed; set => _emptyPulseSpeed = value; }

        [Header("Ghost Preview")]
        [Tooltip("Önizlemenin parça rengine ne kadar yaklaştığı (0 = boş hücre, 1 = yerleşmiş blok). " +
                 "1'in altında kalmalı ki önizleme gerçek bloklarla karışmasın.")]
        [Range(0.2f, 0.95f)]
        [SerializeField] private float _ghostStrength    = 0.62f;
        [Tooltip("Nefes alma genliği. ReduceMotion açıkken nabız durur.")]
        [Range(0f, 0.4f)]
        [SerializeField] private float _ghostPulseAmount = 0.16f;
        [SerializeField] private float _ghostPulseSpeed  = 5f;
        [Tooltip("Geçersiz yerleşimde çakışan (dolu) hücrenin rengi.")]
        [SerializeField] private Color _ghostInvalidColor      = new Color(1.00f, 0.18f, 0.16f, 1f);
        [Tooltip("Geçersiz yerleşimde boş hücrelerin rengi — parça renginden bağımsız gri.")]
        [SerializeField] private Color _ghostInvalidEmptyColor = new Color(0.52f, 0.55f, 0.62f, 1f);

        [Header("Intro Animation")]
        [SerializeField] private bool  _playIntroOnBuild  = true;
        [SerializeField] private float _introStagger      = 0.008f;
        [SerializeField] private float _introTileDuration = 0.25f;

        private TileView[,] _tiles;
        private readonly Dictionary<Vector2Int, (OverlayType type, string label)> _overlays = new();

        // ── Overlay ──────────────────────────────────────────────────────────
        public void SetOverlay(int x, int y, OverlayType type, string label = null)
            => _overlays[new Vector2Int(x, y)] = (type, label);

        public void ClearOverlay(int x, int y)
            => _overlays.Remove(new Vector2Int(x, y));

        public void ClearAllOverlays()
            => _overlays.Clear();

        // ── Build ────────────────────────────────────────────────────────────
        public void Build(BoardModel board)
        {
            if (TilePrefab == null) { Debug.LogError("[BoardView] TilePrefab null"); return; }

            _overlays.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _tiles = new TileView[board.Width, board.Height];
            float targetScale = CellSize * 0.95f;

            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                var go = Instantiate(TilePrefab.gameObject, transform);
                go.name               = $"Tile_{x}_{y}";
                go.transform.position = GridToWorldCenter(x, y);
                go.transform.localScale = _playIntroOnBuild
                    ? Vector3.zero
                    : Vector3.one * targetScale;

                var tv = go.GetComponent<TileView>();
                if (tv == null) { Destroy(go); continue; }

                tv.Init();

                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = 10;

                _tiles[x, y] = tv;
            }

            if (_playIntroOnBuild)
                StartCoroutine(PlayIntro(board.Width, board.Height, targetScale));
        }

        // ── Intro ────────────────────────────────────────────────────────────
        private IEnumerator PlayIntro(int width, int height, float targetScale)
        {
            var wait      = new WaitForSeconds(_introStagger);
            int diagonals = width + height - 1;

            for (int d = 0; d < diagonals; d++)
            {
                for (int x = 0; x < width; x++)
                {
                    int y = d - x;
                    if (y < 0 || y >= height) continue;

                    // y=0 board'da alt — görsel olarak üst = height-1-y
                    int vy   = height - 1 - y;
                    var tile = _tiles[x, vy];
                    if (tile == null) continue;

                    tile.transform
                        .DOScale(Vector3.one * targetScale, _introTileDuration)
                        .SetEase(Ease.OutBack, 1.5f);
                    
                }
                
                yield return wait;
            }
            yield return new WaitForSeconds(_introTileDuration + _introStagger * (width + height));
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                _tiles[x, y]?.RefreshBaseScale();
        }

        // ── Mouse Helpers ────────────────────────────────────────────────────
        public bool IsMouseOverBoard(Camera cam)
        {
            if (_tiles == null) return false;
            if (!TryGetBoardPoint(cam, out Vector2 local)) return false;

            float bx = local.x / CellSize;
            float by = local.y / CellSize;

            return bx >= 0 && bx < _tiles.GetLength(0) &&
                   by >= 0 && by < _tiles.GetLength(1);
        }

        public Vector2Int? TryGetCellUnderMouse(Camera cam)
        {
            var raw = GetRawCell(cam);
            if (raw == null) return null;

            int w = _tiles.GetLength(0), h = _tiles.GetLength(1);
            int x = raw.Value.x,         y = raw.Value.y;

            return (x >= 0 && y >= 0 && x < w && y < h)
                ? new Vector2Int(x, y)
                : (Vector2Int?)null;
        }

        public Vector2Int? TryGetClampedCellUnderMouse(Camera cam, PieceDefinition piece, Rotation rot)
        {
            if (_tiles == null || piece == null) return null;

            var raw = GetRawCell(cam);
            if (raw == null) return null;

            int w = _tiles.GetLength(0), h = _tiles.GetLength(1);
            var cells = piece.GetCells(rot);

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var c in cells)
            {
                if (c.x < minX) minX = c.x; if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y; if (c.y > maxY) maxY = c.y;
            }

            int cx = Mathf.Clamp(raw.Value.x, -minX, w - 1 - maxX);
            int cy = Mathf.Clamp(raw.Value.y, -minY, h - 1 - maxY);

            return new Vector2Int(cx, cy);
        }

        // ── Render ───────────────────────────────────────────────────────────

        /// <summary>Pozisyon bonuslu ghost hücrelerinin puan yazısı bu renkte gösterilir.</summary>
        [Header("Positional Bonus")]
        [SerializeField] private Color _bonusValueColor = new Color(1f, 0.85f, 0.25f, 1f);
        [Tooltip("Boş hücrede duran kalıcı kart bonusu rengi (Corner Stone / Center Base).")]
        [SerializeField] private Color _staticBonusColor = new Color(1f, 0.72f, 0.20f, 0.75f);

        /// <summary>
        /// ghostTileValue: ghost hücrelerde gösterilecek puan değeri.
        /// RunController'dan _currentPiece.TileValue + shape bonus geçilir.
        ///
        /// ghostPositionBonus: hücre bazlı pozisyon bonusu (Corner Stone / Center Base).
        /// RunController skorlamada kullandığı aynı fonksiyondan doldurur — gösterilen
        /// sayı ile kazanılan puan asla ayrışmaz.
        /// </summary>
        public void Render(
            BoardModel board,
            ISet<Vector2Int> ghostCells,
            float ghostTileValue = 0f,
            IReadOnlyDictionary<Vector2Int, float> ghostPositionBonus = null,
            IReadOnlyDictionary<Vector2Int, float> staticPositionBonus = null,
            Color? ghostValidOverride = null,
            Color? ghostPieceColor    = null,
            bool   ghostPlaceable     = true,
            ISet<Vector2Int> linePreviewCells = null,
            float  linePreviewStrength = 0f)
        {
            if (_tiles == null) return;

            Color deadZoneTint = new Color(0.28f, 0.05f, 0.05f, 1f);

            // ── Ghost renkleri ───────────────────────────────────────────────
            // Eskiden sabit camgöbeği, %10 alfa: koyu hücrenin üstünde neredeyse
            // görünmüyordu. Artık önizleme parçanın KENDİ renginde ve opak —
            // oyuncu tam olarak neyin nereye ineceğini görür. Dolu bloklardan
            // ayrılsın diye boş hücre rengiyle karıştırılır ve hafifçe nefes alır.
            //
            // Yerleşim geçersizse TÜM önizleme kırmızıya döner (eskiden yalnızca
            // çakışan hücre kırmızıydı, gerisi "geçerli" görünüyordu).
            //
            // Ghost Drop gibi kartlar geçerli önizlemeyi kendi rengiyle boyayabilir:
            // oyuncu bedava yerleştirmeyi TIKLAMADAN ÖNCE görmeli, sonrasında
            // öğrenmesi kartı oynanamaz kılıyordu.
            float pulse = GameSettings.ReduceMotion
                ? 0.5f
                : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _ghostPulseSpeed);
            float ghostMix = _ghostStrength + (pulse - 0.5f) * _ghostPulseAmount;
            float conflictBlink = GameSettings.ReduceMotion
                ? 1f
                : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _ghostPulseSpeed * 2.2f);

            Color pieceCol = ghostPieceColor ?? BlockColorPalette.GhostValid;
            pieceCol.a = 1f;
            Color overrideCol = ghostValidOverride ?? default;
            overrideCol.a = 1f;

            // ── Satır tamamlama ipucu ────────────────────────────────────────
            // Hattın çevresindeki çerçeveyi LineHintOutline çizer; buradaki tek iş,
            // o hatlardaki ghost hücrelerini tam parça rengine yaklaştırmak.
            // Yerleşmiş bloklara dokunulmaz — kendi renginde kalırlar.
            bool previewOn = linePreviewCells != null && linePreviewCells.Count > 0 && linePreviewStrength > 0.001f;

            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                bool filled   = board.IsFilled(x, y);
                bool phantom  = board.IsPhantom(x, y);
                bool deadZone = board.IsDeadZone(x, y);
                bool isGhost  = ghostCells != null && ghostCells.Contains(new Vector2Int(x, y));

                Color color;
                if (isGhost)
                {
                    Color under = filled ? board.GetCellColor(x, y) : EmptyCellAt(x, y);
                    under.a = 1f;

                    if (!ghostPlaceable)
                        // Geçersiz: boş hücreler GRİ (renkten bağımsız "sığmıyor" —
                        // kırmızı parçada kırmızı uyarı parçanın kendi rengiyle
                        // karışıyordu), yalnızca çakışan hücreler parlak kırmızı:
                        // sorunun tam olarak nerede olduğu okunur.
                        // Çakışma hücresi yanıp söner: palette kırmızı blok da var,
                        // sabit kırmızı yerleşmiş bir bloğa benziyordu.
                        color = filled
                            ? Color.Lerp(under, _ghostInvalidColor, Mathf.Lerp(0.55f, 0.95f, conflictBlink))
                            : Color.Lerp(under, _ghostInvalidEmptyColor, 0.5f);
                    else if (ghostValidOverride.HasValue)
                        color = Color.Lerp(under, overrideCol, 0.8f);
                    else
                    {
                        float mix = ghostMix;
                        if (previewOn && linePreviewCells.Contains(new Vector2Int(x, y)))
                            mix = Mathf.Lerp(mix, 0.95f, linePreviewStrength);
                        color = Color.Lerp(under, pieceCol, mix);
                    }
                }
                else if (deadZone)
                    color = deadZoneTint;
                else if (phantom)
                    color = EmptyCellAt(x, y); // overlay handles the visual tint
                else
                    color = filled ? board.GetCellColor(x, y) : EmptyCellAt(x, y);

                _tiles[x, y].SetColor(color);
                _tiles[x, y].IsGhostPreview = isGhost && !filled;

                if (filled && !isGhost && !phantom && !deadZone)
                {
                    _tiles[x, y].SetTileValue(board.GetTileValue(x, y));
                    _tiles[x, y].HideBonusHint();
                }
                else if (!filled && isGhost)
                {
                    // Pozisyon bonusu varsa taban değere eklenir ve vurgulu renkte yazılır
                    float posBonus = 0f;
                    ghostPositionBonus?.TryGetValue(new Vector2Int(x, y), out posBonus);

                    float shown = ghostTileValue + posBonus;
                    if (shown > 0f)
                        _tiles[x, y].SetTileValue(
                            shown,
                            posBonus > 0f ? _bonusValueColor : (Color?)null);
                    else
                        _tiles[x, y].ClearValue();

                    // Ghost hücrede kalıcı ipucu gizlenir — yerleşim önizlemesi öne çıksın
                    _tiles[x, y].HideBonusHint();
                }
                else if (!filled && staticPositionBonus != null &&
                         staticPositionBonus.TryGetValue(new Vector2Int(x, y), out float staticBonus) &&
                         staticBonus > 0f)
                {
                    // Kart kaynaklı kalıcı hücre bonusu — şekil sürüklenmeden de görünür
                    _tiles[x, y].ClearValue();
                    _tiles[x, y].SetBonusHint(staticBonus, _staticBonusColor);
                }
                else
                {
                    _tiles[x, y].ClearValue();
                    _tiles[x, y].HideBonusHint();
                }

                // ── Overlays applied on top each frame ───────────
                _tiles[x, y].ClearOverlay();
            }

            foreach (var kv in _overlays)
            {
                int ox = kv.Key.x, oy = kv.Key.y;
                if (ox < 0 || oy < 0 || ox >= board.Width || oy >= board.Height) continue;
                _tiles[ox, oy].SetOverlay(kv.Value.type, kv.Value.label);
                _tiles[ox, oy].ApplyOverlayVisual(board.IsFilled(ox, oy));
            }
        }

        // ── Accessors ────────────────────────────────────────────────────────
        public Vector3 GetTileWorldPosition(int x, int y) => GridToWorldCenter(x, y);

        public TileView GetTile(int x, int y)
        {
            if (_tiles == null || x < 0 || y < 0 ||
                x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1)) return null;
            return _tiles[x, y];
        }

        /// <summary>
        /// Boş hücrenin o anki rengi. <see cref="_emptyPulseAmount"/> 0 iken
        /// taban renk aynen döner — varsayılan bu, mevcut sahne etkilenmez.
        ///
        /// Dalga çapraz ilerler (x + y), böylece tahtada tek yönlü bir akış
        /// hissi oluşur. <see cref="Time.unscaledTime"/> kullanılır: kart
        /// seçiminde <c>timeScale = 0</c> olsa da tahta nefes almaya devam eder.
        /// </summary>
        private Color EmptyCellAt(int x, int y)
        {
            if (_emptyPulseAmount <= 0.0001f) return _emptyCellColor;

            float phase = (x + y) * _emptyPulseWavelength - Time.unscaledTime * _emptyPulseSpeed;
            float wave  = Mathf.Sin(phase) * 0.5f + 0.5f;

            return Color.Lerp(_emptyCellColor, _emptyPulseColor, wave * _emptyPulseAmount);
        }

        /// <summary>
        /// Farenin tahta üzerindeki SÜREKLİ konumu, hücre biriminde
        /// (0,0 = sol-alt hücrenin sol-alt köşesi; 1.5 = ikinci hücrenin ortası).
        /// Parçayı imlece ortalamak için tamsayı hücre yetmez — parçanın merkezi
        /// yarım hücrelerde de olabilir.
        /// </summary>
        public bool TryGetMouseCellPoint(Camera cam, out Vector2 cellPoint)
        {
            cellPoint = default;
            if (_tiles == null || CellSize <= 0f) return false;
            if (!TryGetBoardPoint(cam, out Vector2 local)) return false;
            cellPoint = local / CellSize;
            return true;
        }

        /// <summary>Tile'ların görünen yüzeyinin dünya z'si (TileSkirt kaldırması dahil).</summary>
        private float SurfaceZ
        {
            get
            {
                var t = _tiles != null && _tiles.Length > 0 ? _tiles[0, 0] : null;
                return t != null ? t.transform.position.z : 0f;
            }
        }

        // ── Private ──────────────────────────────────────────────────────────
        private Vector2Int? GetRawCell(Camera cam)
        {
            if (_tiles == null) return null;
            if (!TryGetBoardPoint(cam, out Vector2 local)) return null;

            return new Vector2Int(
                Mathf.FloorToInt(local.x / CellSize),
                Mathf.FloorToInt(local.y / CellSize)
            );
        }

        /// <summary>
        /// Fare pozisyonunu tahtanın düzlemine (dünya z = 0) düşürür ve
        /// OriginWorld'e göre yerel koordinat döndürür.
        ///
        /// Işın-düzlem kesişimi kullanılır; bu ortografik kamerada eski
        /// <c>ScreenToWorldPoint</c> yaklaşımıyla birebir aynı sonucu verir,
        /// perspektif kamerada ise doğru çalışan tek yöntemdir. 2.5D prototip
        /// (SampleScene) bu sayede tahta seçimini paylaşabiliyor.
        /// </summary>
        private bool TryGetBoardPoint(Camera cam, out Vector2 local)
        {
            local = default;
            // Pointer = fare VEYA dokunma — portrait mobilde Mouse.current null döner.
            var pointer = Pointer.current;
            if (cam == null || pointer == null) return false;

            Vector2 mp    = pointer.position.ReadValue();
            Ray     ray   = cam.ScreenPointToRay(new Vector3(mp.x, mp.y, 0f));
            // Işın, oyuncunun GÖRDÜĞÜ yüzeyle kesiştirilir. 2.5D sahnede TileSkirt
            // tile'ları kameraya doğru kaldırıyor (z ≈ -0.22); z = 0 düzlemi
            // kullanılınca eğik kamerada imleç görünen hücreden ~0.13 hücre
            // kayıyordu. Ortografik / düz sahnede SurfaceZ = 0, sonuç aynı.
            var     plane = new Plane(Vector3.forward, new Vector3(0f, 0f, SurfaceZ));

            if (!plane.Raycast(ray, out float dist)) return false;

            Vector3 hit = ray.GetPoint(dist);
            local = new Vector2(hit.x, hit.y) - OriginWorld;
            return true;
        }

        private Vector3 GridToWorldCenter(int x, int y) => new Vector3(
            OriginWorld.x + (x + 0.5f) * CellSize,
            OriginWorld.y + (y + 0.5f) * CellSize,
            0f
        );
    }
}