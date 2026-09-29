using System.Collections;
using System.Collections.Generic;
using RogueBlockBlast.Core;
using RogueBlockBlast.Core.Settings;
using RogueBlockBlast.Game.Prototype;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Line clear anının partikül ve shader katmanı.
    ///
    /// Kurulum gerekmez: <see cref="Instance"/> ilk çağrıda kendini yaratır,
    /// shader'ları <c>Resources/FX</c>'ten yükler, dokuları kodla üretir.
    /// <see cref="LineClearVFX"/> bunu çağırır — sahneye bir şey eklemek gerekmez.
    ///
    /// Katmanlar (hepsi tahta düzleminde, tile'ların üstünde):
    ///   Hat seviyesi   — enerji ışını (prosedürel shader), hat boyunca kayan
    ///                    kıvılcım çizgileri, şok dalgası halkası, kamera sarsıntısı
    ///   Tile seviyesi  — parlama, yıldız parlaması, kırılan parçalar (tile renginde),
    ///                    kıvılcımlar, havada süzülen korlar
    ///
    /// Aynı anda temizlenen hat sayısı arttıkça her şey büyür: daha çok parça,
    /// daha hızlı kıvılcım, daha geniş halka, daha sert sarsıntı. 3+ hatta ikinci
    /// bir halka ve tahta çapında kor yağmuru eklenir.
    ///
    /// Partiküller önceden kurulmuş beş ParticleSystem'e <c>Emit</c> ile basılır —
    /// temizlik başına Instantiate yok, GC yok.
    ///
    /// Erişilebilirlik: adetler <see cref="GameSettings.VfxIntensity"/> ile ölçeklenir,
    /// sarsıntı <see cref="GameSettings.ScreenShake"/> ile (ReduceMotion → 0).
    /// </summary>
    public sealed class LineClearParticles : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        private static LineClearParticles _instance;

        /// <summary>Sahnede yoksa kendini yaratır. Sahneyle birlikte yok olur.</summary>
        public static LineClearParticles Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindAnyObjectByType<LineClearParticles>();
                if (_instance == null)
                    _instance = new GameObject("LineClearParticles").AddComponent<LineClearParticles>();
                return _instance;
            }
        }

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Render")]
        [Tooltip("Tahta 'Board' layer'ında. Efektler aynı layer'da, tile'ların üstünde çizilir; " +
                 "kart seçimi 'UI' layer'ında kaldığı için onun altında kalır.")]
        [SerializeField] private string _sortingLayer = "Board";
        [SerializeField] private int    _sortingOrder = 400;
        [Tooltip("Tile'lar TileSkirt yüzünden kameraya doğru ~0.22 kalkık. Efektler onların önünde dursun.")]
        [SerializeField] private float  _zOffset      = -0.35f;

        [Header("Intensity (HDR — bloom'u besler)")]
        [SerializeField] private float _sparkIntensity = 2.4f;
        [SerializeField] private float _glowIntensity  = 0.55f;
        [SerializeField] private float _flareIntensity = 1.5f;
        [SerializeField] private float _shardIntensity = 1.25f;
        [SerializeField] private float _emberIntensity = 1.8f;
        [SerializeField] private float _beamIntensity  = 1.35f;
        [SerializeField] private float _ringIntensity  = 1.1f;

        [Header("Timing")]
        [SerializeField] private float _beamOpenTime = 0.14f;
        [SerializeField] private float _beamLifetime = 0.55f;
        [SerializeField] private float _ringLifetime = 0.55f;

        [Header("Shake")]
        [SerializeField] private float _shakeBase    = 0.22f;
        [SerializeField] private float _shakePerLine = 0.14f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private ParticleSystem _shards, _sparks, _glow, _flares, _embers;
        private Material       _beamMat, _ringMat;
        private Mesh           _quad;
        private bool           _built, _broken;

        private readonly List<Object>                _owned     = new List<Object>();
        private readonly Stack<MeshRenderer>         _beamPool  = new Stack<MeshRenderer>();
        private readonly Stack<MeshRenderer>         _ringPool  = new Stack<MeshRenderer>();
        private readonly List<Pending>               _pending   = new List<Pending>();
        private MaterialPropertyBlock                _mpb;
        private BoardCameraRig                       _rig;
        private bool                                 _rigSearched;

        private struct Pending
        {
            public float   Time;
            public Vector3 Pos;
            public Color   Color;
            public bool    Scores;
            public float   Power;
            public Vector2 Axis;   // hattın yönü; parçalar buna dik fırlar
        }

        private static readonly int ID_Color  = Shader.PropertyToID("_Color");
        private static readonly int ID_Reveal = Shader.PropertyToID("_Reveal");
        private static readonly int ID_Fade   = Shader.PropertyToID("_Fade");
        private static readonly int ID_Radius = Shader.PropertyToID("_Radius");
        private static readonly int ID_Width  = Shader.PropertyToID("_Width");

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Hat seviyesindeki efektler: ışınlar, hat kıvılcımları, halka, sarsıntı.
        /// <paramref name="rowScores"/> / <paramref name="colScores"/>: bu hat
        /// temizlendi VE puan veriyor mu. Puan vermeyen hatta ışın çakmaz —
        /// LineClearVFX'in "sessizlik bilgi taşır" kuralıyla aynı.
        /// </summary>
        public void PlayLines(
            bool[] clearedRows, bool[] clearedCols,
            bool[] rowScores,   bool[] colScores,
            IReadOnlyList<TileSnapshot> snapshots,
            BoardView board, int width, int height)
        {
            if (!EnsureBuilt() || board == null) return;

            float cell  = board.CellSize;
            _cellSize   = cell;
            Vector2 org = board.OriginWorld;
            float vi    = Mathf.Max(0f, GameSettings.VfxIntensity);

            int lines = 0, scoringLines = 0;
            for (int y = 0; y < clearedRows.Length; y++) if (clearedRows[y]) { lines++; if (rowScores[y]) scoringLines++; }
            for (int x = 0; x < clearedCols.Length; x++) if (clearedCols[x]) { lines++; if (colScores[x]) scoringLines++; }
            if (lines == 0) return;

            float power = LinePower(lines);

            // Işınlar
            for (int y = 0; y < clearedRows.Length; y++)
            {
                if (!clearedRows[y] || !rowScores[y]) continue;
                Color c = LineColor(snapshots, y, true);
                Vector3 center = new Vector3(org.x + width * cell * 0.5f, org.y + (y + 0.5f) * cell, _zOffset);
                StartCoroutine(BeamRoutine(center, false, width * cell, cell, c, power));
                EmitLineStreaks(center, Vector2.right, width * cell, cell, c, power, vi);
            }
            for (int x = 0; x < clearedCols.Length; x++)
            {
                if (!clearedCols[x] || !colScores[x]) continue;
                Color c = LineColor(snapshots, x, false);
                Vector3 center = new Vector3(org.x + (x + 0.5f) * cell, org.y + height * cell * 0.5f, _zOffset);
                StartCoroutine(BeamRoutine(center, true, height * cell, cell, c, power));
                EmitLineStreaks(center, Vector2.up, height * cell, cell, c, power, vi);
            }

            if (scoringLines == 0) return;

            // Şok dalgası — temizlenen hücrelerin ağırlık merkezinden
            Vector3 centroid = Vector3.zero;
            Color   avg      = Color.black;
            int     n        = 0;
            if (snapshots != null)
            {
                foreach (var s in snapshots)
                {
                    centroid += board.GetTileWorldPosition(s.X, s.Y);
                    avg      += s.Color;
                    n++;
                }
            }
            if (n > 0) { centroid /= n; avg /= n; }
            else       { centroid = new Vector3(org.x + width * cell * 0.5f, org.y + height * cell * 0.5f, 0f); avg = Color.white; }
            centroid.z = _zOffset;

            Color ringCol = Color.Lerp(Saturate(avg), Color.white, 0.3f);
            float maxR    = Mathf.Min(cell * (1.8f + 1.1f * lines), cell * 6.5f);
            StartCoroutine(RingRoutine(centroid, maxR, ringCol, 0f, 1f));

            if (lines >= 3)
            {
                StartCoroutine(RingRoutine(centroid, maxR * 1.35f, Color.Lerp(ringCol, Color.white, 0.4f), 0.09f, 0.7f));
                EmitEmberShower(org, width * cell, height * cell, avg, vi * (lines - 1));
            }

            // Sarsıntı
            Rig()?.AddTrauma(_shakeBase + _shakePerLine * (lines - 1));
        }

        /// <summary>
        /// Tek bir tile'ın patlaması. <paramref name="delay"/> LineClearVFX'in
        /// tile animasyonuyla aynı gecikme — parça, tile'ın kendi flash'ıyla
        /// aynı anda kopar.
        /// </summary>
        public void PlayTile(Vector3 worldPos, Color color, float delay, bool scores,
                             int lineCount, Vector2 lineAxis)
        {
            if (!EnsureBuilt()) return;

            var p = new Pending
            {
                Time   = Time.time + Mathf.Max(0f, delay),
                Pos    = new Vector3(worldPos.x, worldPos.y, _zOffset),
                Color  = color,
                Scores = scores,
                Power  = LinePower(lineCount),
                Axis   = lineAxis,
            };

            if (delay <= 0f) EmitTile(p);
            else             _pending.Add(p);
        }

        // ── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void Update()
        {
            if (_pending.Count == 0) return;
            float now = Time.time;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Time > now) continue;
                EmitTile(_pending[i]);
                _pending.RemoveAt(i);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            foreach (var o in _owned) if (o != null) Destroy(o);
        }

        // ── Tile burst ───────────────────────────────────────────────────────

        private float CellScale => _cellSize > 0f ? _cellSize : 0.85f;
        private float _cellSize = 0.85f;

        private void EmitTile(Pending p)
        {
            float vi   = Mathf.Max(0f, GameSettings.VfxIntensity);
            if (vi <= 0.01f) return;

            float cs   = CellScale;
            float pw   = p.Power;
            Color c    = Saturate(p.Color);
            Color hot  = Color.Lerp(c, Color.white, 0.55f);

            // Puan vermeyen hücre: yalnızca sönük gri toz. Kutlama yok.
            if (!p.Scores)
            {
                int dust = Mathf.RoundToInt(3 * vi);
                for (int i = 0; i < dust; i++)
                    EmitShard(p.Pos, p.Axis, new Color(0.45f, 0.48f, 0.55f), cs, 0.55f, 0.6f);
                return;
            }

            // 1) Parlama — tile'ın yerinde genişleyen yumuşak ışık
            Emit(_glow, p.Pos, Vector3.zero, cs * 1.25f * Mathf.Lerp(1f, 1.2f, pw - 1f), 0.26f, hot, 0f, 0f);

            // 2) Yıldız parlaması — keskin, dönen, kısa
            Emit(_flares, p.Pos + new Vector3(0f, 0f, -0.02f), Vector3.zero,
                 cs * Random.Range(0.9f, 1.2f) * pw, Random.Range(0.26f, 0.34f),
                 Color.Lerp(c, Color.white, 0.75f), Random.Range(0f, 90f), Random.Range(-160f, 160f));

            // 3) Kırılan parçalar — tile'ın kendi renginde, yerçekimiyle düşer
            int shardCount = Mathf.Clamp(Mathf.RoundToInt((4f + 2f * (pw - 1f) * 3f) * vi), 1, 12);
            for (int i = 0; i < shardCount; i++)
            {
                Color sc = Random.value < 0.15f ? Color.Lerp(c, Color.white, 0.6f) : Shade(c, Random.Range(0.8f, 1.15f));
                EmitShard(p.Pos, p.Axis, sc, cs, pw, 1f);
            }

            // 4) Kıvılcımlar — hızlı, uzayan çizgiler, sürtünmeyle yavaşlar
            int sparkCount = Mathf.Clamp(Mathf.RoundToInt((7f + 5f * (pw - 1f) * 2f) * vi), 1, 22);
            for (int i = 0; i < sparkCount; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                dir = (dir + Perp(p.Axis) * Random.Range(-0.6f, 0.6f) + Vector2.up * 0.35f).normalized;
                float speed = Random.Range(3.5f, 8.5f) * pw * (cs / 0.85f);
                Vector3 v = new Vector3(dir.x, dir.y, -Random.Range(0f, 0.15f)) * speed;
                Emit(_sparks, p.Pos, v, cs * Random.Range(0.045f, 0.075f), Random.Range(0.25f, 0.5f),
                     Random.value < 0.5f ? hot : Color.Lerp(hot, Color.white, 0.5f), 0f, 0f);
            }

            // 5) Korlar — yavaşça yükselip sönen noktalar, anı biraz uzatır
            int emberCount = Mathf.RoundToInt(Random.Range(1f, 2.6f) * vi * pw);
            for (int i = 0; i < emberCount; i++)
            {
                Vector3 off = (Vector3)(Random.insideUnitCircle * cs * 0.4f);
                Vector3 v   = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.3f, 1.2f), -Random.Range(0f, 0.4f));
                Emit(_embers, p.Pos + off, v, cs * Random.Range(0.05f, 0.09f), Random.Range(0.9f, 1.6f), hot, 0f, 0f);
            }
        }

        private void EmitShard(Vector3 pos, Vector2 axis, Color color, float cs, float power, float speedMul)
        {
            // Hatta dik yöne ağırlıklı, yukarı meyilli fırlatma
            Vector2 perp = Perp(axis) * (Random.value < 0.5f ? -1f : 1f);
            Vector2 dir  = (perp * 0.8f + axis * Random.Range(-0.7f, 0.7f) + Vector2.up * 0.9f
                            + Random.insideUnitCircle * 0.5f).normalized;
            float speed  = Random.Range(1.8f, 4.2f) * power * speedMul * (cs / 0.85f);
            Vector3 v    = new Vector3(dir.x, dir.y, -Random.Range(0.1f, 0.45f)) * speed;
            Vector3 off  = (Vector3)(Random.insideUnitCircle * cs * 0.3f);

            Emit(_shards, pos + off, v, cs * Random.Range(0.14f, 0.3f), Random.Range(0.55f, 0.95f),
                 color, Random.Range(0f, 360f), Random.Range(-540f, 540f));
        }

        // ── Line-level ───────────────────────────────────────────────────────

        private void EmitLineStreaks(Vector3 center, Vector2 axis, float length, float cs, Color c, float power, float vi)
        {
            int count = Mathf.RoundToInt(16f * power * vi);
            Color hot = Color.Lerp(Saturate(c), Color.white, 0.6f);
            for (int i = 0; i < count; i++)
            {
                float t    = Random.Range(-0.5f, 0.5f);
                float sign = Mathf.Sign(t == 0f ? 1f : t);            // merkezden uçlara doğru kaçar
                Vector3 pos = center + (Vector3)(axis * (t * length)) + (Vector3)(Perp(axis) * Random.Range(-0.15f, 0.15f) * cs);
                Vector3 v   = (Vector3)(axis * sign * Random.Range(5f, 10f) * power + Perp(axis) * Random.Range(-1.2f, 1.2f));
                v.z = -Random.Range(0f, 0.3f);
                Emit(_sparks, pos, v, cs * Random.Range(0.05f, 0.08f), Random.Range(0.18f, 0.32f), hot, 0f, 0f);
            }
        }

        private void EmitEmberShower(Vector2 org, float w, float h, Color c, float amount)
        {
            int count = Mathf.RoundToInt(28f * amount);
            Color hot = Color.Lerp(Saturate(c), Color.white, 0.5f);
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector3(org.x + Random.Range(0f, w), org.y + Random.Range(0f, h), _zOffset);
                var v   = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.6f, 2.0f), -Random.Range(0f, 0.6f));
                Emit(_embers, pos, v, CellScale * Random.Range(0.05f, 0.1f), Random.Range(1.0f, 1.9f), hot, 0f, 0f);
            }
        }

        private IEnumerator BeamRoutine(Vector3 center, bool vertical, float length, float cs, Color c, float power)
        {
            var mr = GetPooled(_beamPool, _beamMat, "Beam");
            var tr = mr.transform;
            tr.position = center;
            tr.rotation = vertical ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;

            Color hdr = Saturate(c) * _beamIntensity;
            float len = length + cs * 0.9f;
            float thickStart = cs * 1.9f * Mathf.Sqrt(power);
            float thickEnd   = cs * 0.35f;

            float t = 0f;
            while (t < _beamLifetime)
            {
                t += Time.deltaTime;
                float open   = Mathf.Clamp01(t / _beamOpenTime);
                float reveal = 1.12f * (1f - (1f - open) * (1f - open) * (1f - open));   // ease-out cubic
                float k      = Mathf.Clamp01((t - _beamOpenTime * 0.6f) / (_beamLifetime - _beamOpenTime * 0.6f));
                float thick  = Mathf.Lerp(thickStart, thickEnd, 1f - (1f - k) * (1f - k));
                float fade   = 1f - k * k;

                tr.localScale = new Vector3(len, thick, 1f);
                _mpb.Clear();
                _mpb.SetColor(ID_Color, hdr);
                _mpb.SetFloat(ID_Reveal, reveal);
                _mpb.SetFloat(ID_Fade, fade);
                mr.SetPropertyBlock(_mpb);
                yield return null;
            }

            Release(_beamPool, mr);
        }

        private IEnumerator RingRoutine(Vector3 center, float maxRadius, Color c, float delay, float strength)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            var mr = GetPooled(_ringPool, _ringMat, "Ring");
            var tr = mr.transform;
            tr.position   = center + new Vector3(0f, 0f, -0.01f);
            tr.rotation   = Quaternion.identity;
            tr.localScale = new Vector3(maxRadius * 2f, maxRadius * 2f, 1f);

            Color hdr = c * (_ringIntensity * strength);
            float t = 0f;
            while (t < _ringLifetime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / _ringLifetime);
                float r = 1f - Mathf.Pow(1f - k, 3f);        // hızlı başlar, yavaşlar

                _mpb.Clear();
                _mpb.SetColor(ID_Color, hdr);
                _mpb.SetFloat(ID_Radius, Mathf.Lerp(0.04f, 0.92f, r));
                _mpb.SetFloat(ID_Width,  Mathf.Lerp(0.07f, 0.025f, k));
                _mpb.SetFloat(ID_Fade,   (1f - k) * (1f - k));
                mr.SetPropertyBlock(_mpb);
                yield return null;
            }

            Release(_ringPool, mr);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static float LinePower(int lines) => Mathf.Min(1f + 0.22f * (Mathf.Max(1, lines) - 1), 1.9f);

        private static Vector2 Perp(Vector2 v) => new Vector2(-v.y, v.x);

        private static Color LineColor(IReadOnlyList<TileSnapshot> snaps, int index, bool row)
        {
            if (snaps == null) return Color.white;
            Color sum = Color.black; int n = 0;
            foreach (var s in snaps)
            {
                if (row ? s.Y != index : s.X != index) continue;
                sum += s.Color; n++;
            }
            return n > 0 ? sum / n : Color.white;
        }

        /// <summary>Rengi en parlak kanalı 1 olacak şekilde yükseltir — koyu palette de efekt canlı kalsın.</summary>
        private static Color Saturate(Color c)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (m < 0.001f) return Color.white;
            return new Color(c.r / m, c.g / m, c.b / m, 1f);
        }

        private static Color Shade(Color c, float k) =>
            new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

        private static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life,
                                 Color color, float rot, float angVel)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position              = pos,
                velocity              = vel,
                startSize             = size,
                startLifetime         = life,
                startColor            = color,
                rotation              = rot,
                angularVelocity       = angVel,
                applyShapeToPosition  = false,
            };
            ps.Emit(ep, 1);
        }

        private BoardCameraRig Rig()
        {
            if (_rig != null || _rigSearched) return _rig;
            _rigSearched = true;
            var cam = Camera.main;
            if (cam != null) _rig = cam.GetComponent<BoardCameraRig>();
            if (_rig == null) _rig = FindAnyObjectByType<BoardCameraRig>();
            return _rig;
        }

        private MeshRenderer GetPooled(Stack<MeshRenderer> pool, Material mat, string name)
        {
            MeshRenderer mr;
            if (pool.Count > 0)
            {
                mr = pool.Pop();
                mr.gameObject.SetActive(true);
                return mr;
            }

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial     = mat;
            mr.sortingLayerName   = _sortingLayer;
            mr.sortingOrder       = _sortingOrder - 5;
            mr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows     = false;
            return mr;
        }

        private static void Release(Stack<MeshRenderer> pool, MeshRenderer mr)
        {
            if (mr == null) return;
            mr.gameObject.SetActive(false);
            pool.Push(mr);
        }

        // ── Build ────────────────────────────────────────────────────────────

        private bool EnsureBuilt()
        {
            if (_built)  return true;
            if (_broken) return false;

            var particleShader = Resources.Load<Shader>("FX/LineClearParticle");
            var beamShader     = Resources.Load<Shader>("FX/LineClearBeam");
            var ringShader     = Resources.Load<Shader>("FX/LineClearRing");
            if (particleShader == null || beamShader == null || ringShader == null)
            {
                Debug.LogWarning("[LineClearParticles] Resources/FX shader'ları bulunamadı — efekt kapalı.");
                _broken = true;
                return false;
            }

            _mpb  = new MaterialPropertyBlock();
            _quad = BuildQuad();
            _owned.Add(_quad);

            var glowTex  = Own(MakeTexture(64, GlowPixel));
            var sparkTex = Own(MakeTexture(32, SparkPixel));
            var shardTex = Own(MakeTexture(32, ShardPixel));
            var flareTex = Own(MakeTexture(128, FlarePixel));

            _beamMat = Own(new Material(beamShader) { name = "LineClearBeam (runtime)" });
            _ringMat = Own(new Material(ringShader) { name = "LineClearRing (runtime)" });

            // Sıra: parçalar en altta (renk), üstüne toplanan ışıklar
            _shards = BuildSystem("Shards", Own(ParticleMat(particleShader, shardTex, _shardIntensity, false)), 0,
                                  ParticleSystemRenderMode.Billboard, gravity: 0.95f, drag: 1.1f,
                                  size: Curve(0f, 1f, 0.75f, 0.9f, 1f, 0f),
                                  alpha: Curve(0f, 1f, 0.7f, 1f, 1f, 0f));

            _glow   = BuildSystem("Glow",   Own(ParticleMat(particleShader, glowTex, _glowIntensity, true)), 1,
                                  ParticleSystemRenderMode.Billboard, gravity: 0f, drag: 0f,
                                  size: Curve(0f, 0.45f, 0.35f, 1f, 1f, 1.15f),
                                  alpha: Curve(0f, 1f, 0.3f, 0.7f, 1f, 0f));

            _embers = BuildSystem("Embers", Own(ParticleMat(particleShader, glowTex, _emberIntensity, true)), 2,
                                  ParticleSystemRenderMode.Billboard, gravity: -0.05f, drag: 0.8f,
                                  size: Curve(0f, 0.6f, 0.2f, 1f, 1f, 0f),
                                  alpha: Curve(0f, 0f, 0.1f, 1f, 1f, 0f), noise: true);

            _sparks = BuildSystem("Sparks", Own(ParticleMat(particleShader, sparkTex, _sparkIntensity, true)), 3,
                                  ParticleSystemRenderMode.Stretch, gravity: 0.4f, drag: 3.2f,
                                  size: Curve(0f, 1f, 0.6f, 0.8f, 1f, 0f),
                                  alpha: Curve(0f, 1f, 0.6f, 1f, 1f, 0f));

            _flares = BuildSystem("Flares", Own(ParticleMat(particleShader, flareTex, _flareIntensity, true)), 4,
                                  ParticleSystemRenderMode.Billboard, gravity: 0f, drag: 0f,
                                  size: Curve(0f, 0.2f, 0.18f, 1f, 1f, 0f),
                                  alpha: Curve(0f, 1f, 0.5f, 1f, 1f, 0f));

            var sr = _sparks.GetComponent<ParticleSystemRenderer>();
            sr.velocityScale = 0.045f;
            sr.lengthScale   = 1.6f;

            _built = true;
            return true;
        }

        private T Own<T>(T o) where T : Object { _owned.Add(o); return o; }

        private static Material ParticleMat(Shader sh, Texture2D tex, float intensity, bool additive)
        {
            var m = new Material(sh) { mainTexture = tex, name = $"LineClear {tex.name} (runtime)" };
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Additive",  additive ? 1f : 0f);
            m.SetFloat("_SrcBlend",  additive ? (float)UnityEngine.Rendering.BlendMode.One
                                              : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend",  additive ? (float)UnityEngine.Rendering.BlendMode.One
                                              : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            return m;
        }

        private ParticleSystem BuildSystem(string name, Material mat, int orderOffset,
                                           ParticleSystemRenderMode mode, float gravity, float drag,
                                           AnimationCurve size, AnimationCurve alpha, bool noise = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake     = false;
            main.loop            = true;
            main.duration        = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode     = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles    = 2500;
            main.startSpeed      = 0f;
            main.gravityModifier = gravity;

            var emission = ps.emission; emission.enabled = false;
            var shape    = ps.shape;    shape.enabled    = false;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size    = new ParticleSystem.MinMaxCurve(1f, size);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            var ak = new GradientAlphaKey[alpha.length];
            for (int i = 0; i < alpha.length; i++) ak[i] = new GradientAlphaKey(alpha.keys[i].value, alpha.keys[i].time);
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, ak);
            col.color = new ParticleSystem.MinMaxGradient(g);

            if (drag > 0f)
            {
                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.limit   = 1000f;
                lim.drag    = drag;
                lim.multiplyDragByParticleSize     = false;
                lim.multiplyDragByParticleVelocity = false;
            }

            if (noise)
            {
                var nz = ps.noise;
                nz.enabled     = true;
                nz.strength    = 0.35f;
                nz.frequency   = 0.8f;
                nz.scrollSpeed = 0.6f;
                nz.quality     = ParticleSystemNoiseQuality.Low;
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial     = mat;
            r.renderMode         = mode;
            r.sortingLayerName   = _sortingLayer;
            r.sortingOrder       = _sortingOrder + orderOffset;
            r.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows     = false;
            r.alignment          = ParticleSystemRenderSpace.View;
            r.minParticleSize    = 0f;
            r.maxParticleSize    = 0.25f;   // ekranın en fazla %25'i — kameraya yaklaşan partikül ekranı kaplamasın

            ps.Play();
            return ps;
        }

        private static AnimationCurve Curve(float t0, float v0, float t1, float v1, float t2, float v2)
        {
            var c = new AnimationCurve(new Keyframe(t0, v0), new Keyframe(t1, v1), new Keyframe(t2, v2));
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "LineClearQuad" };
            m.vertices  = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
            m.uv        = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            // Işın/halka ölçeklendiğinde culling'e takılmasın
            m.bounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
            return m;
        }

        // ── Procedural textures ──────────────────────────────────────────────

        private delegate Color PixelFn(float x, float y);   // x,y ∈ [-1, 1]

        private static Texture2D MakeTexture(int size, PixelFn fn)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name       = fn.Method.Name.Replace("Pixel", ""),
            };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                px[y * size + x] = fn(u, v);
            }
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        private static Color GlowPixel(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Exp(-r * r * 4.2f) * Mathf.Clamp01(1f - r);
            return new Color(1f, 1f, 1f, a);
        }

        private static Color SparkPixel(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01(Mathf.Exp(-r * r * 9f) * 1.3f) * Mathf.Clamp01(1f - r);
            return new Color(1f, 1f, 1f, a);
        }

        /// <summary>Yuvarlatılmış kare; üst-sol kenar parlak, alt-sağ koyu — küçük bir blok kırığı gibi okunur.</summary>
        private static Color ShardPixel(float x, float y)
        {
            const float half = 0.78f, rad = 0.22f;
            float qx = Mathf.Abs(x) - (half - rad), qy = Mathf.Abs(y) - (half - rad);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                          + Mathf.Min(Mathf.Max(qx, qy), 0f) - rad;
            float a = Mathf.Clamp01(-outside * 16f);

            float bevel = Mathf.Clamp01((-x + y) * 0.35f + 0.8f);      // üst-sol aydınlık
            float edge  = Mathf.Clamp01(1f + outside * 5f);            // kenarda ince parlak çerçeve
            float lum   = Mathf.Clamp01(bevel + edge * 0.18f);
            return new Color(lum, lum, lum, a);
        }

        /// <summary>Dört kollu yıldız + yumuşak çekirdek.</summary>
        private static Color FlarePixel(float x, float y)
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float r  = Mathf.Sqrt(x * x + y * y);
            float rayH = Mathf.Exp(-ay * 38f) * Mathf.Clamp01(1f - ax);
            float rayV = Mathf.Exp(-ax * 38f) * Mathf.Clamp01(1f - ay);
            float core = Mathf.Exp(-r * r * 30f);
            float halo = Mathf.Exp(-r * r * 6f) * 0.35f;
            float a = Mathf.Clamp01((rayH + rayV) * 0.9f + core + halo) * Mathf.Clamp01(1f - r * 0.9f);
            return new Color(1f, 1f, 1f, a);
        }
    }
}
