using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace RogueBlockBlast.UI.FX
{
    /// <summary>
    /// Özel hücre kartlarının (Neon Cable / Phantom Cell / Safe Zone / Selective
    /// Blindness) tek seferlik dünya-uzayı efektleri.
    ///
    /// Neden ayrı bir sınıf: TileView hücrenin KALICI görünümünü her frame yeniden
    /// çiziyor — oraya bir patlama koyarsan bir sonraki Render() onu ezer. Buradaki
    /// efektler tahtanın üstünde kendi geçici GameObject'lerinde yaşıyor, tile'ın
    /// rengine hiç dokunmuyorlar ve bittiklerinde kendilerini yok ediyorlar.
    ///
    /// Sahneye elle eklenmesi gerekmez: ilk kullanımda kendini yaratır.
    /// </summary>
    public sealed class BoardOverlayFX : MonoBehaviour
    {
        private static BoardOverlayFX _instance;

        public static BoardOverlayFX Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[BoardOverlayFX]");
                _instance = go.AddComponent<BoardOverlayFX>();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        // Tahtanın sorting layer'ı — Spawn() her efekt parçasını bu katmana koyar.
        // Yoksa parçalar "Default" katmanında kalıp tahtanın altında kaybolur.
        private int _layerId;

        // ── Neon Cable ───────────────────────────────────────────────────────

        /// <summary>
        /// Neon patlaması: vurulan kablodan patlayan kabloya bir elektrik yayı
        /// koşar, varış noktasında çekirdek + iki şok halkası açılır, 3x3 alan
        /// merkezden dışa doğru dalga halinde sönerken kıvılcımlar saçılır.
        ///
        /// Sıralama kasıtlı: önce yay (nedeni gösterir), sonra patlama (sonucu).
        /// Aynı anda oynatılsaydı oyuncu iki kablo arasındaki bağı göremezdi.
        /// </summary>
        public void PlayNeonExplosion(
            BoardView board, Vector2Int hitCable, Vector2Int center,
            Color hitColor, Color blastColor)
        {
            if (board == null) return;

            _layerId = SortingLayer(board);
            float tile = TileSize(board);
            Vector3 a  = board.GetTileWorldPosition(hitCable.x, hitCable.y);
            Vector3 b  = board.GetTileWorldPosition(center.x,  center.y);
            int order  = SortingOrder(board);

            const float arcTime = 0.16f;

            PlayArc(board, a, b, hitColor, blastColor, arcTime, tile);

            DOVirtual.DelayedCall(arcTime * 0.75f, () =>
            {
                // Yay ile patlama arasında sahne değişmiş olabilir (ana menüye dönüş).
                if (this == null || board == null) return;

                // Çekirdek — beyazdan neon rengine düşen kısa, sert bir flaş.
                var core = Spawn(OverlayFXGraphics.SoftDisc, b, Color.white, tile * 0.35f, order + 4);
                DOTween.Sequence()
                    .Join(core.transform.DOScale(tile * 2.30f, 0.30f).SetEase(Ease.OutCubic))
                    .Join(core.DOColor(new Color(blastColor.r, blastColor.g, blastColor.b, 0f), 0.30f)
                              .SetEase(Ease.InQuad))
                    .OnComplete(() => Destroy(core.gameObject));

                // İki halka farklı hızda: tek halka "daire büyüdü" olur,
                // ikisi birden gerçek bir şok dalgası gibi okunur.
                SpawnRing(b, blastColor, tile * 0.45f, tile * 2.9f, 0.38f, 0.95f, order + 3);
                SpawnRing(b, Color.white,  tile * 0.30f, tile * 1.9f, 0.26f, 0.70f, order + 3);

                SpawnSparks(b, blastColor, tile, count: 16, order + 3);

                // 3x3 hücreler merkezden dışa: önce merkez, sonra artılar,
                // en son köşeler. Patlamanın yayıldığı hissi buradan geliyor.
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int ring  = Mathf.Abs(dx) + Mathf.Abs(dy);      // 0, 1 veya 2
                    var t     = board.GetTile(center.x + dx, center.y + dy);
                    t?.PlayClearFX(ring * 0.055f, Color.Lerp(blastColor, Color.white, 0.5f));
                }
            });
        }

        /// <summary>Zikzaklı elektrik yayı — birkaç kez yeniden çizilerek titriyor.</summary>
        private void PlayArc(
            BoardView board, Vector3 a, Vector3 b, Color from, Color to, float duration, float tile)
        {
            var go = new GameObject("NeonArc");
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace   = true;
            lr.material        = SpriteMaterial;
            lr.sortingLayerID  = SortingLayer(board);
            lr.sortingOrder    = SortingOrder(board) + 5;
            lr.numCapVertices  = 2;
            lr.textureMode     = LineTextureMode.Stretch;

            const int points = 9;
            lr.positionCount = points;
            lr.widthCurve    = AnimationCurve.EaseInOut(0f, tile * 0.16f, 1f, tile * 0.16f);

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(1f, 0f),   new GradientAlphaKey(1f, 1f) });
            lr.colorGradient = grad;

            void Jitter()
            {
                for (int i = 0; i < points; i++)
                {
                    float f = i / (float)(points - 1);
                    Vector3 p = Vector3.Lerp(a, b, f);

                    // Uçlarda sapma sıfır, ortada en yüksek — yay kablolara yapışsın.
                    float spread = Mathf.Sin(f * Mathf.PI) * tile * 0.42f;
                    Vector3 normal = Vector3.Cross((b - a).normalized, Vector3.forward);
                    lr.SetPosition(i, p + normal * Random.Range(-spread, spread));
                }
            }

            Jitter();

            var seq = DOTween.Sequence();
            for (int i = 0; i < 4; i++)
                seq.AppendInterval(duration / 4f).AppendCallback(Jitter);

            seq.Append(DOVirtual.Float(1f, 0f, 0.10f, v =>
                {
                    if (lr == null) return;
                    lr.widthMultiplier = v;
                }))
                .OnComplete(() => { if (go != null) Destroy(go); });
        }

        // ── Phantom Cell ─────────────────────────────────────────────────────

        /// <summary>
        /// Işınlanma: eski hücrede sis dikey bir çizgiye çökerek kaybolur, iki hücre
        /// arasında sönen bir iz kalır, yeni hücrede aynı çizgi açılıp birkaç kez
        /// kararsızca titreştikten sonra oturur.
        ///
        /// "Çizgiye çökme" bilinçli: hücreden hücreye taşınan bir nesne değil,
        /// bir yerde yok olup başka yerde var olan bir şey izlenimi verir.
        /// </summary>
        public void PlayPhantomTeleport(BoardView board, Vector2Int from, Vector2Int to, Color color)
        {
            if (board == null) return;

            _layerId = SortingLayer(board);
            float tile = TileSize(board);
            int   order = SortingOrder(board) + 4;
            Vector3 a = board.GetTileWorldPosition(from.x, from.y);
            Vector3 b = board.GetTileWorldPosition(to.x,   to.y);

            // 1) Çöküş
            var collapse = Spawn(OverlayFXGraphics.SoftDisc, a, color, tile * 0.95f, order);
            collapse.color = new Color(color.r, color.g, color.b, 0.85f);
            DOTween.Sequence()
                .Join(collapse.transform.DOScale(new Vector3(tile * 0.06f, tile * 1.25f, 1f), 0.20f)
                                        .SetEase(Ease.InQuad))
                .Join(collapse.DOFade(0f, 0.20f).SetEase(Ease.InQuad))
                .OnComplete(() => Destroy(collapse.gameObject));

            SpawnRing(a, color, tile * 0.5f, tile * 1.6f, 0.28f, 0.6f, order);

            // 2) İz — iki nokta arasına sıralı sönen tutamlar
            const int wisps = 8;
            for (int i = 0; i < wisps; i++)
            {
                float f = (i + 1) / (float)(wisps + 1);
                Vector3 p = Vector3.Lerp(a, b, f) + (Vector3)(Random.insideUnitCircle * tile * 0.16f);
                var w = Spawn(OverlayFXGraphics.SoftDisc, p, color, tile * 0.45f, order - 1);
                w.color = new Color(color.r, color.g, color.b, 0f);

                DOTween.Sequence()
                    .AppendInterval(0.06f + f * 0.16f)
                    .Append(w.DOFade(0.55f, 0.06f))
                    .Append(w.DOFade(0f, 0.18f))
                    .Join(w.transform.DOScale(tile * 0.15f, 0.24f))
                    .OnComplete(() => Destroy(w.gameObject));
            }

            // 3) Belirme + kararsız titreşim
            var appear = Spawn(OverlayFXGraphics.SoftDisc, b, color,  tile * 0.06f, order);
            appear.transform.localScale = new Vector3(tile * 0.06f, tile * 1.25f, 1f);
            appear.color = new Color(color.r, color.g, color.b, 0f);

            var seq = DOTween.Sequence().AppendInterval(0.24f);
            seq.Append(appear.DOFade(0.9f, 0.06f))
               .Join(appear.transform.DOScale(new Vector3(tile * 1.0f, tile * 1.0f, 1f), 0.14f)
                                     .SetEase(Ease.OutBack));

            // Üç hızlı sönüp yanma — hayaletin "tam oturmadığı" an.
            for (int i = 0; i < 3; i++)
                seq.Append(appear.DOFade(0.2f, 0.045f)).Append(appear.DOFade(0.8f, 0.045f));

            seq.Append(appear.DOFade(0f, 0.16f))
               .Join(appear.transform.DOScale(tile * 1.5f, 0.16f))
               .OnComplete(() => Destroy(appear.gameObject));

            DOVirtual.DelayedCall(0.24f, () =>
                SpawnRing(b, color, tile * 0.3f, tile * 1.8f, 0.30f, 0.85f, order));
        }

        // ── Safe Zone ────────────────────────────────────────────────────────

        /// <summary>
        /// Kalkanın kırılması: çerçeve beyaza patlar, hücre sınırında kalan bir
        /// halka dışarı savrulur ve altıgen sekiz kıymığa ayrılıp uçar.
        ///
        /// Kıymıkların yönü altıgenin köşe açılarından geliyor — rastgele saçılan
        /// parçalar cam kırığı gibi durmuyordu, bu şekilde kalkanın kendisi
        /// parçalanmış gibi okunuyor.
        /// </summary>
        public void PlaySafeZoneBreak(BoardView board, Vector2Int pos, Color color)
        {
            if (board == null) return;

            _layerId = SortingLayer(board);
            float tile  = TileSize(board);
            int   order = SortingOrder(board) + 4;
            Vector3 c   = board.GetTileWorldPosition(pos.x, pos.y);

            // Kırılma anı: kalkan bir kez beyaza kaçar.
            var flash = Spawn(OverlayFXGraphics.HexShield, c, Color.white, tile * 0.92f, order + 1);
            DOTween.Sequence()
                .Append(flash.transform.DOScale(tile * 1.15f, 0.09f).SetEase(Ease.OutQuad))
                .Join(flash.DOFade(0f, 0.14f))
                .OnComplete(() => Destroy(flash.gameObject));

            SpawnRing(c, color, tile * 0.85f, tile * 2.2f, 0.34f, 0.9f, order);

            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f + Random.Range(-8f, 8f);
                var shard = Spawn(OverlayFXGraphics.Shard, c, color, tile * 0.34f, order);
                shard.transform.localRotation = Quaternion.Euler(0f, 0f, -ang);

                Vector3 dir = Quaternion.Euler(0f, 0f, ang) * Vector3.up;
                float dist  = tile * Random.Range(0.85f, 1.5f);

                DOTween.Sequence()
                    .Join(shard.transform.DOMove(c + dir * dist, 0.42f).SetEase(Ease.OutCubic))
                    .Join(shard.transform.DORotate(
                              new Vector3(0f, 0f, -ang + Random.Range(-120f, 120f)), 0.42f))
                    .Join(shard.transform.DOScale(tile * 0.10f, 0.42f).SetEase(Ease.InQuad))
                    .Join(shard.DOFade(0f, 0.42f).SetEase(Ease.InQuad))
                    .OnComplete(() => Destroy(shard.gameObject));
            }
        }

        // ── Selective Blindness ──────────────────────────────────────────────

        /// <summary>
        /// Kaybolan blokların gecikmeli, tek tek gösterimi.
        ///
        /// Tahta modeli çağrı anında zaten güncellendi (oyun mantığı — ölü havuz
        /// kontrolü dahil — hiç değişmesin diye). Burada gördüğün bloklar modelden
        /// kopyalanmış sahte kopyalar: gerçek hücre boşken bile ekranda duruyorlar,
        /// sırası gelince içe çöküp kayboluyorlar. Böylece oyuncu "iki blok gitti"
        /// bilgisini satır temizliğinden ayrı, okunur bir zamanlamada alıyor.
        /// </summary>
        public void PlayBlindnessRemoval(
            BoardView board, IReadOnlyList<(Vector2Int cell, Color color)> removed,
            Color inkColor, float startDelay = 0.5f, float stagger = 0.28f)
        {
            if (board == null || removed == null) return;

            _layerId = SortingLayer(board);
            float tile  = TileSize(board);
            int   order = SortingOrder(board) + 2;

            for (int i = 0; i < removed.Count; i++)
            {
                var (cell, color) = removed[i];
                var tileView = board.GetTile(cell.x, cell.y);
                if (tileView == null) continue;

                Vector3 p = board.GetTileWorldPosition(cell.x, cell.y);

                var ghost = Spawn(tileView.TileSprite, p, color, tile, order);
                // Sahte kopya biraz saydam: üstüne yeni bir parça konursa
                // oyuncu altındakini görüp kafası karışmasın.
                ghost.color = new Color(color.r, color.g, color.b, 0.9f);
                // Sprite'ın kendi boyutu tile ile aynı değil — dünya boyutuna göre ölçekle.
                FitToWorldSize(ghost, tile);

                float delay = startDelay + i * stagger;
                var baseScale = ghost.transform.localScale;

                DOTween.Sequence()
                    .AppendInterval(delay)
                    // Önce bir uyarı titremesi: "bu blok gidiyor".
                    .Append(ghost.DOColor(Color.Lerp(color, inkColor, 0.75f), 0.10f))
                    .Join(ghost.transform.DOScale(baseScale * 1.14f, 0.10f).SetEase(Ease.OutQuad))
                    .AppendCallback(() =>
                    {
                        SpawnRing(p, inkColor, tile * 0.4f, tile * 1.7f, 0.30f, 0.8f, order + 1);
                        SpawnSparks(p, inkColor, tile, count: 7, order + 1);
                    })
                    // Sonra içe çöküş — dışa patlayan line clear'dan kasıtlı olarak farklı.
                    .Append(ghost.transform.DOScale(baseScale * 0.05f, 0.20f).SetEase(Ease.InBack))
                    .Join(ghost.DOFade(0f, 0.20f))
                    .OnComplete(() => { if (ghost != null) Destroy(ghost.gameObject); });
            }
        }

        // ── Perfect Clear ────────────────────────────────────────────────────

        /// <summary>
        /// Boşalan tahtanın merkezinden dışa doğru altın bir dalga.
        ///
        /// Gecikme hücrenin merkeze UZAKLIĞINDAN hesaplanıyor, satır/sütun
        /// sırasından değil: kare bir tahtada satır sırası dalgayı bir perde gibi
        /// gösterir, uzaklık ise gerçek bir halka gibi açar.
        /// </summary>
        public void PlayPerfectClearWave(BoardView board, int width, int height)
        {
            if (board == null) return;

            _layerId = SortingLayer(board);
            float tile  = TileSize(board);
            int   order = SortingOrder(board) + 2;
            var   gold  = new Color(1f, 0.84f, 0.35f);

            float cx = (width  - 1) * 0.5f;
            float cy = (height - 1) * 0.5f;
            float maxDist = Mathf.Sqrt(cx * cx + cy * cy);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width;  x++)
            {
                float dist  = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float delay = dist / Mathf.Max(maxDist, 0.001f) * 0.42f;

                Vector3 p = board.GetTileWorldPosition(x, y);
                var spark = Spawn(OverlayFXGraphics.SoftDisc, p, gold, tile * 0.9f, order);
                spark.color = new Color(gold.r, gold.g, gold.b, 0f);

                DOTween.Sequence()
                    .AppendInterval(delay)
                    .Append(spark.DOFade(0.9f, 0.08f))
                    .Join(spark.transform.DOScale(tile * 1.15f, 0.08f).SetEase(Ease.OutQuad))
                    .Append(spark.DOFade(0f, 0.28f))
                    .Join(spark.transform.DOScale(tile * 0.25f, 0.28f).SetEase(Ease.InQuad))
                    .OnComplete(() => Destroy(spark.gameObject));

                board.GetTile(x, y)?.PlayRippleFX(0.16f, 0.26f);
            }
        }

        // ── Bounty Hunter ────────────────────────────────────────────────────

        /// <summary>
        /// Temizlenen satır/sütunlardan fırlayan altın sikkeler.
        ///
        /// Sikkeler yukarı doğru bir yay çizip söner — coin sayacı ekranın üst
        /// tarafında olduğu için hareket yönü tek başına "bu para oraya gidiyor"
        /// diyor; sayacın ekran konumunu bilmeye gerek kalmıyor.
        /// </summary>
        public void PlayCoinBurst(
            BoardView board, bool[] clearedRows, bool[] clearedCols,
            int width, int height, int coinCount)
        {
            if (board == null || coinCount <= 0) return;

            _layerId = SortingLayer(board);
            float tile  = TileSize(board);
            int   order = SortingOrder(board) + 5;
            var   gold  = new Color(1f, 0.82f, 0.28f);

            // Temizlenen her hattın ortasından çıksınlar — sikkeler tahtaya
            // rastgele saçılsaydı hangi satırın ödediği okunmazdı.
            var origins = new List<Vector3>();
            if (clearedRows != null)
                for (int y = 0; y < clearedRows.Length; y++)
                    if (clearedRows[y]) origins.Add(board.GetTileWorldPosition(width / 2, y));
            if (clearedCols != null)
                for (int x = 0; x < clearedCols.Length; x++)
                    if (clearedCols[x]) origins.Add(board.GetTileWorldPosition(x, height / 2));

            if (origins.Count == 0) return;

            // Çok fazla sikke ekranı kilitler; hat başına en fazla 4 tane.
            int perOrigin = Mathf.Clamp(Mathf.CeilToInt(coinCount / (float)origins.Count), 1, 4);

            for (int o = 0; o < origins.Count; o++)
            for (int i = 0; i < perOrigin;      i++)
            {
                Vector3 start = origins[o] + (Vector3)(Random.insideUnitCircle * tile * 0.5f);

                var coin = Spawn(OverlayFXGraphics.SoftDisc, start, gold, tile * 0.34f, order);
                var ring = Spawn(OverlayFXGraphics.Ring, start, Color.white, tile * 0.34f, order + 1);
                ring.color = new Color(1f, 0.95f, 0.75f, 0.9f);
                ring.transform.SetParent(coin.transform, worldPositionStays: true);

                float dur  = Random.Range(0.55f, 0.75f);
                float rise = tile * Random.Range(2.2f, 3.4f);
                float side = Random.Range(-tile * 0.9f, tile * 0.9f);

                DOTween.Sequence()
                    .AppendInterval(0.10f + (o * perOrigin + i) * 0.045f)
                    // Yükseliş yavaşlayarak, yana kayış sabit hızda: ikisinin
                    // farkı düz bir çizgi yerine gerçek bir atış yayı veriyor.
                    .Join(coin.transform.DOMoveY(start.y + rise, dur).SetEase(Ease.OutQuad))
                    .Join(coin.transform.DOMoveX(start.x + side, dur).SetEase(Ease.Linear))
                    .Join(coin.transform.DOScale(tile * 0.12f, dur).SetEase(Ease.InQuad))
                    .Join(coin.DOFade(0f, dur).SetEase(Ease.InQuad))
                    .Join(ring.DOFade(0f, dur * 0.7f))
                    .OnComplete(() => { if (coin != null) Destroy(coin.gameObject); });
            }
        }

        // ── Decaying Rift ────────────────────────────────────────────────────

        /// <summary>
        /// Geri sayım 0'a indi: hücre içe çöküp ölü bölgeye dönüyor.
        ///
        /// Bu, tahtada KALICI hasar bırakan tek olay ve tamamen sessizce
        /// oluyordu. Kırmızı halkanın içe kapanması + karanlık bir çöküş, hücrenin
        /// artık kullanılamaz olduğunu satır temizliğinden ayırt edilir kılıyor.
        /// </summary>
        public void PlayRiftCollapse(BoardView board, Vector2Int pos)
        {
            if (board == null) return;

            _layerId = SortingLayer(board);
            float tile  = TileSize(board);
            int   order = SortingOrder(board) + 4;
            Vector3 c   = board.GetTileWorldPosition(pos.x, pos.y);
            var rift    = new Color(1f, 0.30f, 0.18f);

            // İçe kapanan halka — diğer tüm efektlerimiz dışa açılıyor, bu kasıtlı
            // olarak tersi: bir şey kazanılmadı, bir şey yutuldu.
            var ring = Spawn(OverlayFXGraphics.ThickRing, c, rift, tile * 2.4f, order);
            ring.color = new Color(rift.r, rift.g, rift.b, 0f);
            DOTween.Sequence()
                .Append(ring.DOFade(1f, 0.10f))
                .Join(ring.transform.DOScale(tile * 0.15f, 0.34f).SetEase(Ease.InCubic))
                .Append(ring.DOFade(0f, 0.10f))
                .OnComplete(() => Destroy(ring.gameObject));

            // Çöküşün ardından kalan karanlık leke.
            var scar = Spawn(OverlayFXGraphics.SoftDisc, c, Color.black, tile * 0.2f, order - 1);
            scar.color = new Color(0.05f, 0.02f, 0.02f, 0f);
            DOTween.Sequence()
                .AppendInterval(0.30f)
                .Append(scar.DOFade(0.85f, 0.10f))
                .Join(scar.transform.DOScale(tile * 1.05f, 0.14f).SetEase(Ease.OutBack))
                .AppendInterval(0.25f)
                .Append(scar.DOFade(0f, 0.35f))
                .OnComplete(() => Destroy(scar.gameObject));

            DOVirtual.DelayedCall(0.34f, () =>
            {
                if (this == null || board == null) return;
                SpawnSparks(c, rift, tile, count: 9, order);
                board.GetTile(pos.x, pos.y)?.PlayClearFX(0f, rift);
            });
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private static Material _spriteMaterial;
        private static Material SpriteMaterial =>
            _spriteMaterial ??= new Material(Shader.Find("Sprites/Default"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

        private SpriteRenderer Spawn(Sprite sprite, Vector3 pos, Color color, float scale, int order)
        {
            var go = new GameObject("FX");
            go.transform.SetParent(transform, false);
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.color          = color;
            sr.sortingLayerID = _layerId;
            sr.sortingOrder   = order;
            return sr;
        }

        private void SpawnRing(
            Vector3 pos, Color color, float fromScale, float toScale,
            float duration, float alpha, int order)
        {
            var ring = Spawn(OverlayFXGraphics.Ring, pos, color, fromScale, order);
            ring.color = new Color(color.r, color.g, color.b, alpha);

            DOTween.Sequence()
                .Join(ring.transform.DOScale(toScale, duration).SetEase(Ease.OutCubic))
                .Join(ring.DOFade(0f, duration).SetEase(Ease.InQuad))
                .OnComplete(() => Destroy(ring.gameObject));
        }

        private void SpawnSparks(Vector3 pos, Color color, float tile, int count, int order)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = i * (360f / count) + Random.Range(-14f, 14f);
                var spark = Spawn(OverlayFXGraphics.Spark, pos,
                                  Color.Lerp(color, Color.white, Random.Range(0f, 0.5f)),
                                  tile * 0.5f, order);
                spark.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
                spark.transform.localScale    = new Vector3(tile * 0.55f, tile * 0.16f, 1f);

                Vector3 dir  = Quaternion.Euler(0f, 0f, ang) * Vector3.right;
                float   dist = tile * Random.Range(0.7f, 1.8f);
                float   dur  = Random.Range(0.24f, 0.40f);

                DOTween.Sequence()
                    .Join(spark.transform.DOMove(pos + dir * dist, dur).SetEase(Ease.OutCubic))
                    .Join(spark.transform.DOScaleX(tile * 0.06f, dur).SetEase(Ease.InQuad))
                    .Join(spark.DOFade(0f, dur).SetEase(Ease.InQuad))
                    .OnComplete(() => Destroy(spark.gameObject));
            }
        }

        /// <summary>Sprite'ın dünya boyutunu hedefe eşitler — PPU'ya bağımlı kalmayalım.</summary>
        private static void FitToWorldSize(SpriteRenderer sr, float worldSize)
        {
            float current = sr.sprite != null ? sr.sprite.bounds.size.x : 1f;
            if (current <= 0.0001f) return;
            sr.transform.localScale = Vector3.one * (worldSize / current);
        }

        private static float TileSize(BoardView board)
        {
            var t = board.GetTile(0, 0);
            return t != null ? t.TileWorldSize : 1f;
        }

        private static int SortingLayer(BoardView board)
        {
            var t = board.GetTile(0, 0);
            return t != null ? t.FxSortingLayer : 0;
        }

        private static int SortingOrder(BoardView board)
        {
            var t = board.GetTile(0, 0);
            return (t != null ? t.FxSortingOrder : 0) + 10;
        }
    }
}
