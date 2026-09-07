using System.Collections.Generic;
using RogueBlockBlast.Core;
using RogueBlockBlast.Game;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Line clear sırasında tüm efektleri orkestre eder.
    /// BoardFX.PlayLineClearFX() yerine bu çağrılır.
    ///
    /// RunController'da:
    ///   [SerializeField] private LineClearVFX LineClearVFX;
    ///   LineClearVFX.Play(board, clearedRows, clearedCols, boardView, cam, scoreboardScreenPos, onAllArrived);
    /// </summary>
    public sealed class LineClearVFX : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("References")]
        [SerializeField] private LineFlashEffect _lineFlash;
        [SerializeField] private Camera          _cam;

        [Header("Score Popup")]
        [Tooltip("ScoreBoard RectTransform — popupların uçacağı hedef.")]
        [SerializeField] private RectTransform   _scoreboardTarget;

        [Header("Timing")]
        [SerializeField] private float _tileWaveDelay   = 0.03f;   // tile'lar arası gecikme
        [SerializeField] private float _popupWaveDelay  = 0.025f;  // popup'lar arası gecikme

        [Header("Particle")]
        [SerializeField] private ParticleSystem _tileBurstPrefab;  // tile patlama prefabı
        [SerializeField] private AudioClip lineCountsfx;
        [SerializeField] private AudioClip ScorePopupSfx;
        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Line Master / Tunnel Vision hangi ekseni puanlıyor.
        /// Both = kısıtlama yok (normal oyun).
        /// </summary>
        public enum ScoringAxis { Both, RowsOnly, ColsOnly }

        // Puanlayan eksen altın, puanlamayan eksen soluk gri temizlenir.
        private static readonly Color AxisScoringFlash = new Color(1f,   0.82f, 0.32f, 1f);
        private static readonly Color AxisDeadFlash    = new Color(0.42f, 0.46f, 0.55f, 1f);

        public void Play(
            bool[]              clearedRows,
            bool[]              clearedCols,
            List<TileSnapshot>  snapshots,
            BoardView           boardView,
            int                 boardWidth,
            int                 boardHeight,
            System.Action       onAllArrived = null,
            ScoringAxis         scoringAxis  = ScoringAxis.Both)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                onAllArrived?.Invoke();
                return;
            }

            // Shuffle — curcuna hissi
            var tiles = new List<TileSnapshot>(snapshots);
            for (int i = tiles.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
            }

            int totalTiles   = tiles.Count;
            int arrivedCount = 0;

            int lineCount = 0;
            for (int i = 0; i < clearedRows.Length; i++) if (clearedRows[i]) lineCount++;
            for (int i = 0; i < clearedCols.Length; i++) if (clearedCols[i]) lineCount++;

            // Ses
            AudioManager.Instance?.PlaySFX(lineCountsfx);

            // Line flash
            if (_lineFlash != null)
            {
                var origin   = boardView.OriginWorld;
                var cellSize = boardView.CellSize;

                // Puan vermeyen eksende çizgi parlaması da yok: sessizlik burada
                // bilgi taşıyor — "bu satır temizlendi ama sana bir şey vermedi".
                for (int y = 0; y < clearedRows.Length; y++)
                    if (clearedRows[y] && scoringAxis != ScoringAxis.ColsOnly)
                        _lineFlash.FlashRow(origin, y, cellSize, boardWidth, y * 0.02f);

                for (int x = 0; x < clearedCols.Length; x++)
                    if (clearedCols[x] && scoringAxis != ScoringAxis.RowsOnly)
                        _lineFlash.FlashColumn(origin, x, cellSize, boardHeight, x * 0.02f);
            }

            // Her tile için efekt
            for (int i = 0; i < tiles.Count; i++)
            {
                var   tile  = tiles[i];
                float delay = i * _popupWaveDelay;

                Vector3 worldPos = boardView.GetTileWorldPosition(tile.X, tile.Y);

                // Line Master / Tunnel Vision: bu hücre puan veren eksende mi?
                // Bir hücre hem satırda hem sütunda olabilir — puanlayan eksende
                // olması yeterli, RunController'ın toplama mantığı da böyle.
                bool scores = scoringAxis switch
                {
                    ScoringAxis.RowsOnly => clearedRows[tile.Y],
                    ScoringAxis.ColsOnly => clearedCols[tile.X],
                    _                    => true,
                };

                // Kısıtlama varken renk kuralı anlatıyor: altın = puan, gri = boşuna.
                Color flash = scoringAxis == ScoringAxis.Both
                    ? tile.Color
                    : (scores ? AxisScoringFlash : AxisDeadFlash);

                // Particle burst
                if (scores) SpawnBurst(worldPos, flash, delay);

                // TileView clear animasyonu
                boardView.GetTile(tile.X, tile.Y)?.PlayClearFX(delay, flash);

                // Score popup — worldPos ve RectTransform hedef geçilir.
                // Puan vermeyen eksende popup UÇURULMAZ: eskiden uçuyordu ve
                // skora eklenmeyen bir sayıyı gösterdiği için doğrudan yalan
                // söylüyordu.
                if (ScorePopupPool.Instance != null && tile.Value > 0f && scores)
                {
                    var popup = ScorePopupPool.Instance.Get();
                    popup.Launch(
                        worldPos,
                        _scoreboardTarget,
                        _cam,
                        tile.Value,
                        flash,
                        delay,
                        onArrive: () =>
                        {
                            AudioManager.Instance?.PlayScoreSFX(ScorePopupSfx);
                            arrivedCount++;
                            if (arrivedCount >= totalTiles)
                                onAllArrived?.Invoke();
                        }
                    );
                }
                else
                {
                    arrivedCount++;
                    if (arrivedCount >= totalTiles)
                        onAllArrived?.Invoke();
                }
            }
        }

        /// <summary>
        /// Kart tetikleme popup'ı — Double Strike / Gambler / Patient gibi anlık
        /// efektler için. Mevcut ScorePopup pool'unu kullanır, yeni sistem yok.
        /// Tetiklenme başına tek popup, bloklamaz, kendiliğinden solar.
        /// </summary>
        public void PlayTriggerPopup(Vector3 worldPos, string text, Color color)
        {
            if (ScorePopupPool.Instance == null || _cam == null) return;
            if (string.IsNullOrEmpty(text)) return;

            var popup = ScorePopupPool.Instance.Get();
            popup.LaunchFloatingText(worldPos, _cam, text, color);
        }

        // ── Private ──────────────────────────────────────────────────────────
        private void SpawnBurst(Vector3 worldPos, Color color, float delay)
        {
            if (_tileBurstPrefab == null) return;

            var ps = Instantiate(_tileBurstPrefab, worldPos, Quaternion.identity);

            // Rengi tile'a göre ayarla
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color * 0.8f, color);

            if (delay > 0f)
            {
                ps.gameObject.SetActive(false);
                StartCoroutine(DelayedPlay(ps, delay));
            }
            else
            {
                ps.Play();
                Destroy(ps.gameObject, ps.main.duration + 0.5f);
            }
        }

        private System.Collections.IEnumerator DelayedPlay(ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (ps != null)
            {
                ps.gameObject.SetActive(true);
                ps.Play();
                Destroy(ps.gameObject, ps.main.duration + 0.5f);
            }
        }

    }
}