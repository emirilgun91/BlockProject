// Balance simülasyonu — komutlar:
//   baseline <runs>              yükseltmesiz zorluk eğrisi (persona başına)
//   cards <runs>                 kart etki analizi (her kart ilk seçimde zorlanır)
//   meta <players> <maxRuns>     tam meta ilerleme (coin → mağaza → sonraki run)
//   classic <runs> [rotate|all|nofit]  Classic Mode tek oyun dağılımları (başarım eşikleri)
//   all                          hepsini çalıştırır, out/ altına JSON yazar
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BalanceSim
{
    public static class Personas
    {
        public static BotProfile Casual() => new()
        {
            Name = "Casual", Depth = 1, Noise = 0.20f, UseFit = false, CardPolicy = 0, SmartReroll = false,
            WLine = 3.594f, WClear = 0.974f, WScore = 0.771f, WFilled = 0.242f, WTrans = 0.2f, WHoles = 1.0f, WBig = 1.5f, WBar = 1.5f, WCombo = 0.4f, SecPerMove = 3.0f
        };
        public static BotProfile Mid() => new()
        {
            Name = "Mid", Depth = 1, Noise = 0.06f, UseFit = true, CardPolicy = 1, CardNoise = 0.35f,
            SmartReroll = false, SecPerMove = 3.5f,
            WLine = 3.594f, WClear = 0.974f, WScore = 0.771f, WFilled = 0.242f, WTrans = 0.27f, WHoles = 1.34f, WFit = 11.701f, WBig = 2.837f, WCombo = 1.617f, WBar = 3.189f
        };
        public static BotProfile Skilled() => new()
        {
            Name = "Skilled", Depth = 3, Beam = 6, Noise = 0f, UseFit = true, CardPolicy = 1, CardNoise = 0f,
            WLine = 4.865f, WClear = 2.236f, WScore = 2.232f, WFilled = 0.259f, WTrans = 0.22f, WHoles = 1.101f, WFit = 15.121f, WBig = 8.111f, WCombo = 0.669f, WBar = 2.215f,
            SmartReroll = true, SecPerMove = 4.0f
        };
        public static BotProfile[] All() => new[] { Casual(), Mid(), Skilled() };
    }

    public static partial class Program
    {
        public static GameData G;
        public static string OutDir;
        public static Dictionary<string, float> CardValues = DefaultCardValues();

        // İlk tahmin (uzman sezgisi). `cards` deneyi ölçülen etkiyle bunu günceller.
        static Dictionary<string, float> DefaultCardValues() => new()
        {
            ["Card_Score_Multiplier"] = 6, ["Card_Momentum"] = 6, ["Card_Wide_Pool"] = 8, ["Card_Heavy_Load"] = 6,
            ["Card_Chain_Master"] = 7, ["Card_Card_Collector"] = 5, ["Card_Soft_Landing"] = 7, ["Card_Combo_Shield"] = 4,
            ["Card_Double_Strike"] = 6, ["Card_Momentum_Shield"] = 5, ["Card_Hoarder"] = 5, ["Card_Perfect_Clear"] = 3,
            ["Card_Gambler"] = 3, ["Card_Patient"] = 2, ["Card_Corner_Stone"] = 2, ["Card_Center_Base"] = 2,
            ["Card_Ghost_Drop"] = 4, ["Card_First_Picks"] = 4, ["Diet_plan"] = 3, ["Card_Future_Investment"] = 2,
            ["Card_Neon_Cable"] = 3, ["Card_Phantom_Cell"] = 4, ["Card_Safe_Zone"] = 3, ["Card_Decaying_Rift"] = 1,
            ["Card_Slow_Burn"] = 3, ["Card_Hyperfocus"] = 3, ["Cart_Line_Master"] = 4, ["Cart_Tunnel_Vision"] = 4,
            ["Card_Selective_Blindness"] = 1, ["Card_Bounty_Hunter"] = 0,
        };

        static float CardValue(string id) => CardValues.TryGetValue(id, out var v) ? v : 3f; // şekil kartları

        public static int Main(string[] args)
        {
            string root = FindRoot();
            G = GameData.Load(Path.Combine(root, "Assets"));
            OutDir = Path.Combine(root, "Tools", "BalanceSim", "out");
            Directory.CreateDirectory(OutDir);
            foreach (var c in G.Cards) if (c.IsShape && !CardValues.ContainsKey(c.Id)) CardValues[c.Id] = 3f;
            // `cards` deneyi çalıştıysa bot'lar kartları ölçülen etkiye göre seçer
            var cardsJson = Path.Combine(OutDir, "cards.json");
            if (File.Exists(cardsJson) && args.FirstOrDefault() != "cards")
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(cardsJson));
                foreach (var e in doc.RootElement.EnumerateArray())
                    CardValues[e.GetProperty("Id").GetString()] = (float)e.GetProperty("DeltaStage").GetDouble();
            }

            // `prop <komut> ...`: şekil puanı önerisini uygulayıp komutu onunla koşturur
            if (args.FirstOrDefault() == "prop") { BuildShapeProposal(apply: true); args = args.Skip(1).ToArray(); }
            string cmd = args.Length > 0 ? args[0] : "all";
            int a1 = args.Length > 1 && int.TryParse(args[1], out var t1) ? t1 : 0;
            int a2 = args.Length > 2 && int.TryParse(args[2], out var t2) ? t2 : 0;
            var sw = Stopwatch.StartNew();
            switch (cmd)
            {
                case "data": DumpData(); break;
                case "smoke": Smoke(a1 > 0 ? a1 : 50); break;
                case "baseline": Baseline(a1 > 0 ? a1 : 1000, false); break;
                case "cards": Cards(a1 > 0 ? a1 : 300); break;
                case "meta": Meta(a1 > 0 ? a1 : 40, a2 > 0 ? a2 : 1500, args.Length > 3 ? args[3] : "full"); break;
                case "bugs": Bugs(a1 > 0 ? a1 : 600); break;
                case "trace": Trace(args.Length > 1 ? args[1] : "Mid", a2 > 0 ? a2 : 1, args.Length > 3 && args[3] == "demo"); break;
                case "invest": Investments(a1 > 0 ? a1 : 1000); break;
                case "fidelity": Fidelity(a1 > 0 ? a1 : 8); break;
                case "export": Export(); break;
                case "chain": Chain(a1 > 0 ? a1 : 800); break;
                case "scenarios": Scenarios(a1 > 0 ? a1 : 100, args.Length > 2 ? args[2] : null); break;
                case "tune": Tune(args.Length > 1 ? args[1] : "Skilled", a2 > 0 ? a2 : 300); break;
                case "shapes": Shapes(a1 > 0 ? a1 : 400); break;
                case "cardvar": CardVariants(a1 > 0 ? a1 : 3000); break;
                case "passvar": PassVariants(a1 > 0 ? a1 : 3000); break;
                case "proposal": BuildShapeProposal(apply: false); break;
                case "shapeworth": ShapeWorth(a1 > 0 ? a1 : 2000, args.Length > 2 ? args[2] : "Mid"); break;
                case "classic": Classic(a1 > 0 ? a1 : 1000, args.Length > 2 ? args[2] : ""); break;
            }
            Console.WriteLine($"[done {cmd} in {sw.Elapsed.TotalSeconds:0.0}s]");
            return 0;
        }

        static string FindRoot()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "Assets"))) d = d.Parent;
            return d?.FullName ?? Directory.GetCurrentDirectory();
        }

        static void Write(string name, object o)
        {
            File.WriteAllText(Path.Combine(OutDir, name), JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }));
        }

        static void DumpData()
        {
            Console.WriteLine($"PoolLimit {G.PoolLimit}, milestones {G.Milestones.Count}");
            foreach (var s in G.Shapes)
                Console.WriteLine($"{s.Id,-12} size {s.Size} w {s.BaseWeight,3} tile {s.BaseTile,3} locked {s.Locked} unlock {s.UnlockCost,5} lvl10 tile {s.TileAt(10)} costs {string.Join(",", Enumerable.Range(0, 10).Select(s.ScoreCostFor))} rots {s.Distinct.Length}");
            foreach (var u in G.Upgrades)
                Console.WriteLine($"{u.Id,-26} stage {u.UnlockStage} max {u.MaxLevel} per {u.PerLevel} costs {string.Join(",", u.Costs)}");
            foreach (var c in G.Cards)
                Console.WriteLine($"{c.Id,-26} r{c.Rarity} w{c.BaseWeight,3} u{(c.Unique ? 1 : 0)} req '{c.RequiredShapeId}' tgt '{c.TargetShapeId}' {string.Join(" ", c.Effects.Select(e => $"{e.type}={e.value}"))} {(c.IsShape ? $"shape +{c.ShapeScore} w{c.ShapeWeight}" : "")}");
        }

        public static Bot MakeBot(BotProfile p, Rng r) => new(p, r, CardValues);

        static void Smoke(int n)
        {
            foreach (var bp in Personas.All())
            {
                var sw = Stopwatch.StartNew();
                var res = new ConcurrentBag<RunStats>();
                Parallel.For(0, n, i =>
                {
                    var rng = new Rng((ulong)(i * 7919 + 17));
                    var prof = new Profile(G);
                    var run = new Run(G, prof, new Config { Demo = false }, rng);
                    res.Add(run.Play(MakeBot(bp, rng)));
                });
                var l = res.ToList();
                Console.WriteLine($"{bp.Name,-8} n={n} stage avg {l.Average(x => x.Stage):0.00} max {l.Max(x => x.Stage)} " +
                                  $"score avg {l.Average(x => x.Score):0} placements {l.Average(x => x.Placements):0} " +
                                  $"coins {l.Average(x => x.Coins):0} reasons {string.Join(",", l.GroupBy(x => x.Reason).Select(g => $"{g.Key}:{g.Count()}"))} " +
                                  $"{sw.Elapsed.TotalMilliseconds / n:0.0}ms/run");
            }
        }

        // ── Baseline: yükseltmesiz zorluk eğrisi ────────────────────────────
        public sealed class StageCurve
        {
            public string Persona; public int Runs;
            public double[] ReachPct;            // P(stage >= k)
            public double[] PassGivenReach;      // P(stage >= k | stage >= k-1)
            public double[] MedianPlacementsForStage;
            public Dictionary<string, int> Reasons;
            public double AvgStage, AvgScore, AvgPlacements, AvgCoins, AvgMinutes, FreshPoolDeathPct, P90Stage;
            public double[] StageHistogram;
        }

        static StageCurve Curve(string persona, List<RunStats> l, BotProfile bp)
        {
            int K = G.Milestones.Count;
            var c = new StageCurve
            {
                Persona = persona, Runs = l.Count,
                ReachPct = new double[K + 1], PassGivenReach = new double[K + 1],
                MedianPlacementsForStage = new double[K + 1], StageHistogram = new double[K + 1],
                Reasons = l.GroupBy(x => x.Reason.ToString()).ToDictionary(g => g.Key, g => g.Count()),
                AvgStage = l.Average(x => x.Stage), AvgScore = l.Average(x => x.Score),
                AvgPlacements = l.Average(x => x.Placements), AvgCoins = l.Average(x => x.Coins),
                AvgMinutes = l.Average(x => RunSeconds(x, bp)) / 60.0,
                FreshPoolDeathPct = 100.0 * l.Count(x => x.DiedOnFreshPool) / l.Count,
                P90Stage = l.Select(x => x.Stage).OrderBy(x => x).ElementAt((int)(l.Count * 0.9))
            };
            for (int k = 0; k <= K; k++)
            {
                c.ReachPct[k] = 100.0 * l.Count(x => x.Stage >= k) / l.Count;
                c.StageHistogram[k] = 100.0 * l.Count(x => x.Stage == k) / l.Count;
                int prev = l.Count(x => x.Stage >= k - 1);
                c.PassGivenReach[k] = k == 0 ? 100 : prev == 0 ? 0 : 100.0 * l.Count(x => x.Stage >= k) / prev;
                var pl = l.Where(x => x.StagePlacements.Count >= k && k > 0).Select(x => (double)x.StagePlacements[k - 1]).OrderBy(x => x).ToList();
                c.MedianPlacementsForStage[k] = pl.Count == 0 ? 0 : pl[pl.Count / 2];
            }
            return c;
        }

        public static double RunSeconds(RunStats s, BotProfile bp) => s.Placements * bp.SecPerMove + s.CardPicks * 10 + 20;

        static List<RunStats> RunMany(int n, Func<int, (Profile, Config, BotProfile, ulong)> setup)
        {
            var res = new RunStats[n];
            Parallel.For(0, n, i =>
            {
                var (prof, cfg, bp, seed) = setup(i);
                var rng = new Rng(seed);
                res[i] = new Run(G, prof, cfg, rng).Play(MakeBot(bp, rng));
            });
            return res.ToList();
        }

        static void Baseline(int n, bool quiet)
        {
            var curves = new List<StageCurve>();
            foreach (var bp in Personas.All())
            {
                var l = RunMany(n, i => (new Profile(G), new Config(), bp, (ulong)(i * 104729 + 3)));
                var c = Curve(bp.Name, l, bp);
                curves.Add(c);
                Console.WriteLine($"{bp.Name,-8} avgStage {c.AvgStage:0.00} p90 {c.P90Stage} score {c.AvgScore:0} moves {c.AvgPlacements:0} coins {c.AvgCoins:0} min {c.AvgMinutes:0.0} freshDeath {c.FreshPoolDeathPct:0}%");
                Console.WriteLine("   reach% " + string.Join(" ", c.ReachPct.Take(12).Select((v, k) => $"S{k}:{v:0.#}")));
                Console.WriteLine("   pass%  " + string.Join(" ", c.PassGivenReach.Take(12).Select((v, k) => $"S{k}:{v:0}")));
                Console.WriteLine("   moves  " + string.Join(" ", c.MedianPlacementsForStage.Take(12).Select((v, k) => $"S{k}:{v:0}")));
                Console.WriteLine("   why    " + string.Join(" ", c.Reasons.Select(kv => $"{kv.Key}:{kv.Value}")));
            }
            Write("baseline.json", curves);
        }

        // ── Classic Mode: tek oyun dağılımları (başarım eşiklerini kalibre etmek için) ──
        public sealed class ClassicCurve
        {
            public string Persona; public int Runs, CapHits;
            public Dist Score, Lines, Minutes, Placements, MaxCombo;
            public Dictionary<string, double> ReachPct = new();   // eşik → tek oyunda ulaşma %
        }

        static void Classic(int n, string variant)
        {
            int[] scoreMarks = { 2500, 5000, 7500, 10000, 15000, 20000, 25000, 30000, 40000, 50000, 75000, 100000, 150000, 200000 };
            int[] lineMarks = { 25, 50, 75, 100, 150, 200 };
            int[] minuteMarks = { 5, 10, 15, 20, 30 };
            var curves = new List<ClassicCurve>();
            foreach (var bp in Personas.All())
            {
                var l = RunMany(n, i => (new Profile(G), new Config { Classic = true, MaxPlacements = 20000, ClassicRotate = variant.Contains("rotate"), ClassicAllShapes = variant.Contains("all"), ClassicFitRetries = variant.Contains("nofit") ? 0 : 2 }, bp, (ulong)(i * 7919 + 11)));
                var mins = l.Select(s => RunSeconds(s, bp) / 60.0).ToList();
                var c = new ClassicCurve
                {
                    Persona = bp.Name, Runs = l.Count, CapHits = l.Count(s => s.Reason == Reason.Cap),
                    Score = D(l.Select(s => (double)s.Score), l.Count),
                    Lines = D(l.Select(s => (double)s.Lines), l.Count),
                    Minutes = D(mins, l.Count),
                    Placements = D(l.Select(s => (double)s.Placements), l.Count),
                    MaxCombo = D(l.Select(s => (double)s.MaxCombo), l.Count),
                };
                foreach (int m in scoreMarks) c.ReachPct[$"score{m}"] = 100.0 * l.Count(s => s.Score >= m) / l.Count;
                foreach (int m in lineMarks) c.ReachPct[$"lines{m}"] = 100.0 * l.Count(s => s.Lines >= m) / l.Count;
                foreach (int m in minuteMarks) c.ReachPct[$"min{m}"] = 100.0 * mins.Count(x => x >= m) / l.Count;
                curves.Add(c);

                Console.WriteLine($"{bp.Name,-8} score p25 {c.Score.P25:0} med {c.Score.Median:0} p75 {c.Score.P75:0} p90 {c.Score.P90:0} | " +
                                  $"lines med {c.Lines.Median:0} p90 {c.Lines.P90:0} | min med {c.Minutes.Median:0.0} p90 {c.Minutes.P90:0.0} | " +
                                  $"combo med x{c.MaxCombo.Median:0.0} | cap {c.CapHits}");
                Console.WriteLine("   score% " + string.Join(" ", scoreMarks.Select(m => $"{m / 1000.0:0.#}k:{c.ReachPct[$"score{m}"]:0.#}")));
                Console.WriteLine("   lines% " + string.Join(" ", lineMarks.Select(m => $"{m}:{c.ReachPct[$"lines{m}"]:0.#}")));
                Console.WriteLine("   min%   " + string.Join(" ", minuteMarks.Select(m => $"{m}:{c.ReachPct[$"min{m}"]:0.#}")));
            }
            Write(string.IsNullOrEmpty(variant) ? "classic.json" : $"classic_{variant}.json", curves);
        }

        // ── Doğruluk: hamle kaydı → Unity'de gerçek sınıflarla tekrar oynatılır ──
        sealed class RecordingBot : IBot
        {
            readonly Bot inner; public readonly List<string> Lines; public Run R;
            public RecordingBot(Bot b, List<string> lines) { inner = b; Lines = lines; }
            public Move Choose(Run r)
            {
                var m = inner.Choose(r);
                if (m.Piece < 0) return m;
                var p = r.Pool[m.Piece];
                // maskeden rotasyon + anchor çıkar (PieceDefinition.RotateCell ile aynı)
                for (int rot = 0; rot < 4; rot++)
                for (int ay = -8; ay < 16; ay++)
                for (int ax = -8; ax < 16; ax++)
                {
                    ulong mk = 0; bool ok = true;
                    for (int i = 0; i < p.S.Size && ok; i++)
                    {
                        int x = p.S.CX[i], y = p.S.CY[i], rx, ry;
                        switch (rot) { case 1: rx = y; ry = -x; break; case 2: rx = -x; ry = -y; break; case 3: rx = -y; ry = x; break; default: rx = x; ry = y; break; }
                        int X = ax + rx, Y = ay + ry;
                        if (X < 0 || Y < 0 || X >= 8 || Y >= 8) ok = false; else mk |= 1UL << (Y * 8 + X);
                    }
                    if (ok && mk == m.Mask)
                    {
                        pending = $"{p.S.Id};{p.Tile.ToString(System.Globalization.CultureInfo.InvariantCulture)};{rot};{ax};{ay}";
                        return m;
                    }
                }
                throw new Exception("mask not found");
            }
            public string pending;
            public bool WantPoolReroll(Run r) => false;
            public int PickCard(Run r, List<CardDef> o, bool c) => 0;
        }

        static void Fidelity(int runs)
        {
            var lines = new List<string> { "run;shape;tile;rot;ax;ay;score;mult;charges;milestone;placed" };
            var bp = Personas.Mid();
            for (int k = 0; k < runs; k++)
            {
                var rng = new Rng((ulong)(k * 13 + 1));
                var prof = new Profile(G);
                for (int s = 0; s < G.Shapes.Count; s++) prof.ScoreLvl[s] = 2 + k % 4;
                var run = new Run(G, prof, new Config(), rng);
                var rb = new RecordingBot(MakeBot(bp, rng), lines);
                run.NewRun();
                while (!run.GameOver && run.Stats.Placements < 400)
                {
                    var mv = rb.Choose(run);
                    if (mv.Piece < 0) break;
                    run.DoPlace(mv.Piece, mv.Mask);
                    lines.Add($"{k};{rb.pending};{run.Score};{run.Combo.Mult.ToString("R", System.Globalization.CultureInfo.InvariantCulture)};{run.Combo.Charges};{run.MS.Index};{run.MS.Placed}");
                    // kart seçimi bilerek atlanıyor: yalnızca çekirdek kurallar karşılaştırılıyor
                }
            }
            File.WriteAllLines(Path.Combine(OutDir, "fidelity.csv"), lines);
            Console.WriteLine($"{lines.Count - 1} placements recorded");
        }

        // ── Yatırım getirisi: upgrade / şekil açma / skor seviyesi ───────────
        public sealed class Roi { public string Item; public int Cost; public double AvgStage, DeltaStage, AvgCoins, DeltaCoins, NoMovesPct, StagePer10k, Ci95; }

        static void Investments(int n)
        {
            var bp = Personas.Mid();
            // Referans: oyuncunun ~10. run civarındaki hâli (tüm skor Lv3, Stage 7 açılmış)
            Profile Base()
            {
                var p = new Profile(G);
                for (int s = 0; s < G.Shapes.Count; s++) p.ScoreLvl[s] = 3;
                p.MaxMilestoneReached = 7;
                return p;
            }
            var list = new List<Roi>();
            List<RunStats> Go(Func<Profile> f) => RunMany(n, i => (f(), new Config(), bp, (ulong)(i * 4243 + 9)));
            var b = Go(Base);
            double b0 = b.Average(x => x.Stage), c0 = b.Average(x => x.Coins);
            Roi Make(string name, int cost, List<RunStats> l)
            {
                double a = l.Average(x => x.Stage);
                double sd = Math.Sqrt(l.Average(x => (x.Stage - a) * (x.Stage - a)));
                return new Roi
                {
                    Item = name, Cost = cost, AvgStage = a, DeltaStage = a - b0, AvgCoins = l.Average(x => x.Coins),
                    DeltaCoins = l.Average(x => x.Coins) - c0, NoMovesPct = 100.0 * l.Count(x => x.Reason == Reason.NoMoves) / l.Count,
                    StagePer10k = cost > 0 ? (a - b0) / cost * 10000 : 0, Ci95 = 1.96 * sd / Math.Sqrt(l.Count) * Math.Sqrt(2)
                };
            }
            list.Add(Make("(referans)", 0, b));
            foreach (var u in G.Upgrades)
            {
                int cost = Enumerable.Range(1, u.MaxLevel).Sum(u.CostFor);
                list.Add(Make($"upg:{u.Id} (max {u.MaxLevel})", cost, Go(() => { var p = Base(); p.Upg[u.Idx] = u.MaxLevel; return p; })));
                int c1 = u.CostFor(1);
                list.Add(Make($"upg:{u.Id} (Lv1)", c1, Go(() => { var p = Base(); p.Upg[u.Idx] = 1; return p; })));
            }
            foreach (var s in G.Shapes.Where(s => s.Locked))
                list.Add(Make($"unlock:{s.Id}", s.UnlockCost, Go(() => { var p = Base(); p.ShapeUnlocked[s.Idx] = true; return p; })));
            list.Add(Make("unlock:hepsi", G.Shapes.Where(s => s.Locked).Sum(s => s.UnlockCost), Go(() => { var p = Base(); foreach (var s in G.Shapes) p.ShapeUnlocked[s.Idx] = true; return p; })));
            foreach (int lv in new[] { 4, 5, 6, 8, 10 })
            {
                int cost = G.Shapes.Where(s => !s.Locked).Sum(s => Enumerable.Range(3, lv - 3).Sum(s.ScoreCostFor));
                list.Add(Make($"score: açık şekiller Lv3→Lv{lv}", cost, Go(() => { var p = Base(); foreach (var s in G.Shapes) p.ScoreLvl[s.Idx] = lv; return p; })));
            }
            foreach (var r in list)
                Console.WriteLine($"{r.Item,-44} cost {r.Cost,7}  stage {r.AvgStage,5:0.00} Δ {r.DeltaStage,6:+0.00;-0.00} ±{r.Ci95:0.00}  coins {r.AvgCoins,8:0}  noMoves {r.NoMovesPct,4:0}%  Δstage/10k {r.StagePer10k,6:0.00}");
            Write("investments.json", list);
        }

        // ── Rapor için sıkıştırılmış veri ───────────────────────────────────
        static void Export()
        {
            T Read<T>(string f) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(OutDir, f)),
                new JsonSerializerOptions { IncludeFields = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
            double R(double v) => double.IsNaN(v) || double.IsInfinity(v) ? -1 : Math.Round(v, 2);
            double[] A(double[] a, int n) => a.Take(n).Select(R).ToArray();
            object Dd(Dist d) => d == null ? null : new { med = R(d.Median), p10 = R(d.P10), p90 = R(d.P90), mean = R(d.Mean) };
            var bl = Read<List<StageCurve>>("baseline.json");
            var sf = Read<List<SetupSummary>>("summary_full.json");
            var sd = Read<List<SetupSummary>>("summary_demo.json");
            var cards = Read<List<CardImpact>>("cards.json");
            var inv = Read<List<Roi>>("investments.json");
            object Setup(SetupSummary s) => new
            {
                p = s.Persona, pol = s.Policy,
                stage = A(s.MedianStageByRun, 60), coins = A(s.MedianCoinsByRun, 60), combo = A(s.MedianComboByRun, 60),
                att = Enumerable.Range(1, G.Milestones.Count).Select(k => Dd(s.Attempts[k])).ToArray(),
                reach = Enumerable.Range(1, G.Milestones.Count).Select(k => Dd(s.FirstReachRun[k])).ToArray(),
                reachH = A(s.FirstReachHours, G.Milestones.Count + 1),
                complete = Dd(s.RunsComplete), hours = Dd(s.HoursComplete),
                allShapes = Dd(s.RunsAllShapes), allUpg = Dd(s.RunsAllUpgrades), allScore = Dd(s.RunsAllScoreMax),
                demoDone = Dd(s.RunsDemoDone), demoH = Dd(s.HoursDemoDone), shopDone = Dd(s.RunsDemoShopDone), shopH = Dd(s.HoursDemoShopDone),
                dry = Dd(s.LongestDrySpell), jump = Dd(s.BiggestJump), share = Dd(s.BestRunCoinShare), s10 = Dd(s.RunsBeforeBreakout),
                reasons = s.ReasonPct.ToDictionary(kv => kv.Key, kv => R(kv.Value)), runMin = R(s.MedianRunMinutes),
                noop = R(s.NoopOfferPct), fresh = R(s.FreshPoolDeathPct)
            };
            var data = new
            {
                labels = G.Milestones.Select(m => m.Label).ToArray(),
                thresholds = G.Milestones.Select(m => m.Threshold).ToArray(),
                rewards = G.Milestones.Select(m => m.Coin).ToArray(),
                baseline = bl.Select(c => new { p = c.Persona, pass = A(c.PassGivenReach, 10), reach = A(c.ReachPct, 10), avg = R(c.AvgStage), min = R(c.AvgMinutes), reasons = c.Reasons }).ToArray(),
                full = sf.Select(Setup).ToArray(),
                demo = sd.Select(Setup).ToArray(),
                cards = cards.Select(c => new { id = c.Id, d = R(c.DeltaStage), ci = R(c.Ci95), s4 = R(c.ReachS4), ds4 = R(c.DeltaReachS4) }).ToArray(),
                inv = inv.Select(r => new { item = r.Item, cost = r.Cost, d = R(r.DeltaStage), ci = R(r.Ci95), nm = R(r.NoMovesPct), per10k = R(r.StagePer10k), coins = R(r.AvgCoins) }).ToArray()
            };
            File.WriteAllText(Path.Combine(OutDir, "report_data.json"), JsonSerializer.Serialize(data));
            Console.WriteLine("report_data.json written");
        }

        // ── Chain Master: nereye kadar büyüyor, sınırlarsak ne olur ──────────
        sealed class ChainBot : IBot
        {
            readonly Bot inner; bool first = true;
            public ChainBot(Bot b) { inner = b; }
            public Move Choose(Run r) => inner.Choose(r);
            public bool WantPoolReroll(Run r) => inner.WantPoolReroll(r);
            public int PickCard(Run r, List<CardDef> offer, bool canReroll)
            {
                // ilk seçimde zorla, sonra her teklif edildiğinde al (yığan oyuncu)
                if (first) { first = false; offer[0] = G.Cards.First(c => c.Id == "Card_Chain_Master"); return 0; }
                int i = offer.FindIndex(c => c.Id == "Card_Chain_Master");
                return i >= 0 ? i : inner.PickCard(r, offer, canReroll);
            }
        }

        public sealed class ChainRow { public string Variant, Persona; public int Level; public double AvgStage, ReachFinal, AccP50, AccP90, AccMax, StageNoChain; }

        static void Chain(int n)
        {
            var rows = new List<ChainRow>();
            var variants = new (string name, Func<Config> cfg)[]
            {
                ("sınırsız (24 Ekim öncesi)", () => new Config { ChainMode = 0, MaxChargeFollowsBpc = false }),
                ("zincir kırılınca sıfırla", () => new Config { ChainMode = 1, MaxChargeFollowsBpc = false }),
                ("toplam +0.5 tavan", () => new Config { ChainCap = 0.5f, MaxChargeFollowsBpc = false }),
                ("toplam +1.0 tavan", () => new Config { MaxChargeFollowsBpc = false }),
                ("oyundaki: +1.0 tavan + dolu bar BPC+0.1", () => new Config()),
            };
            foreach (int lvl in new[] { 3, 8 })
            foreach (var bp in new[] { Personas.Mid(), Personas.Skilled() })
            {
                Profile Prof() { var p = new Profile(G); foreach (var s in G.Shapes) p.ScoreLvl[s.Idx] = lvl; p.MaxMilestoneReached = 7; return p; }
                var none = RunMany(n, i => (Prof(), new Config(), bp, (ulong)(i * 97 + lvl)));
                double s0 = none.Average(x => x.Stage);
                foreach (var (name, cfg) in variants)
                {
                    var l = RunManyBot(n, i => (Prof(), cfg(), (ulong)(i * 97 + lvl)), (b, r) => new ChainBot(b), bp);
                    var acc = l.Select(x => (double)x.ChainAcc).OrderBy(x => x).ToList();
                    var row = new ChainRow
                    {
                        Variant = name, Persona = bp.Name, Level = lvl, AvgStage = l.Average(x => x.Stage), StageNoChain = s0,
                        ReachFinal = 100.0 * l.Count(x => x.Stage >= G.Milestones.Count) / l.Count,
                        AccP50 = acc[acc.Count / 2], AccP90 = acc[(int)(acc.Count * 0.9)], AccMax = acc[^1]
                    };
                    rows.Add(row);
                    Console.WriteLine($"Lv{lvl} {bp.Name,-7} {name,-34} stage {row.AvgStage,5:0.00} (kartsız {s0:0.00})  final %{row.ReachFinal,4:0}  bonus p50 +{row.AccP50:0.00} p90 +{row.AccP90:0.00} max +{row.AccMax:0.00}");
                }
            }
            Write("chain.json", rows);
        }

        // ── Denge senaryoları: tam meta ilerleme, öneri paketleri ───────────
        public sealed class ScenarioRow { public string Name, Persona; public double RunsMed, HoursMed, HoursP10, HoursP90, Att3, Att5, Att8, Att12, Jump, CoinShare, FirstS10; }

        static int[] CoinCurve(double first, double growth) =>
            Enumerable.Range(0, G.Milestones.Count).Select(i => (int)(Math.Round(first * Math.Pow(growth, i) / 10.0) * 10)).ToArray();

        static void Scenarios(int players, string only = null)
        {
            var scen = new (string name, Func<Config> cfg, float price)[]
            {
                ("A0: 24 Ekim öncesi (Chain Master sınırsız)", () => new Config { ChainMode = 0, MaxChargeFollowsBpc = false }, 1f),
                ("A: oyundaki hâl", () => new Config(), 1f),
                ("B: combo azalan getiri (K=4)", () => new Config { ComboDimK = 4f }, 1f),
                ("C: coin eğrisi 150×1.22^n", () => new Config { CoinOverride = CoinCurve(150, 1.22) }, 1f),
                ("D: B + C", () => new Config { ComboDimK = 4f, CoinOverride = CoinCurve(150, 1.22) }, 1f),
                ("E: D + skor yükseltme ×0.5", () => new Config { ComboDimK = 4f, CoinOverride = CoinCurve(150, 1.22), GainScale = 0.5f }, 1f),
                ("F: E + Chain Master sıfırlanır + dolu bar BPC'yi izler", () => new Config { ComboDimK = 4f, CoinOverride = CoinCurve(150, 1.22), GainScale = 0.5f, ChainMode = 1, MaxChargeFollowsBpc = true }, 1f),
                ("G: C + Chain Master toplam +1.0 tavan + dolu bar BPC'yi izler", () => new Config { CoinOverride = CoinCurve(150, 1.22), ChainMode = 2, ChainCap = 1.0f, MaxChargeFollowsBpc = true }, 1f),
            };
            MetaSetups = new List<(BotProfile, string)> { (Personas.Casual(), "cheapest"), (Personas.Mid(), "cheapest"), (Personas.Skilled(), "cheapest") };
            var rows = new List<ScenarioRow>();
            foreach (var (name, cfg, price) in scen)
            {
                if (only != null && !name.StartsWith(only)) continue;
                MakeCfg = cfg; PriceScale = price; MetaTag = name;
                Console.WriteLine($"### {name}  coins: {string.Join(",", (cfg().CoinOverride ?? G.Milestones.Select(m => m.Coin).ToArray()).Take(26))}");
                Meta(players, 4000, "full");
                foreach (var s in LastSummary)
                    rows.Add(new ScenarioRow
                    {
                        Name = name, Persona = s.Persona, RunsMed = s.RunsComplete.Median, HoursMed = s.HoursComplete.Median,
                        HoursP10 = s.HoursComplete.P10, HoursP90 = s.HoursComplete.P90,
                        Att3 = s.Attempts[3].Mean, Att5 = s.Attempts[5].Mean, Att8 = s.Attempts[8].Mean, Att12 = s.Attempts[12].Mean,
                        Jump = s.BiggestJump.Median, CoinShare = s.BestRunCoinShare.Median, FirstS10 = s.RunsBeforeBreakout.Median
                    });
            }
            MakeCfg = () => new Config(); PriceScale = 1f; MetaTag = ""; MetaSetups = null;
            Console.WriteLine();
            foreach (var r in rows)
                Console.WriteLine($"{r.Name,-56} {r.Persona,-7} runs {r.RunsMed,5:0} hours {r.HoursMed,5:0.0} [{r.HoursP10:0.0}-{r.HoursP90:0.0}]  att S3 {r.Att3:0.0} S5 {r.Att5:0.0} S8 {r.Att8:0.0} S12 {r.Att12:0.0}  jump {r.Jump:0} share %{r.CoinShare:0} S10@{r.FirstS10:0}");
            Write("scenarios.json", rows);
        }

        // ── Meta özet ───────────────────────────────────────────────────────
        static double Pct(List<double> s, double q)
        {
            if (s.Count == 0) return double.NaN;
            s = s.OrderBy(x => x).ToList();
            return s[Math.Min(s.Count - 1, (int)Math.Floor(q * (s.Count - 1) + 0.5))];
        }

        public sealed class Dist { public double Mean, P10, P25, Median, P75, P90; public int N, Missing; }
        static Dist D(IEnumerable<double> v, int total)
        {
            var l = v.Where(x => x > 0 && !double.IsNaN(x)).ToList();
            return new Dist
            {
                N = l.Count, Missing = total - l.Count, Mean = l.Count == 0 ? double.NaN : l.Average(),
                P10 = Pct(l, 0.1), P25 = Pct(l, 0.25), Median = Pct(l, 0.5), P75 = Pct(l, 0.75), P90 = Pct(l, 0.9)
            };
        }

        public sealed class SetupSummary
        {
            public string Persona, Policy; public int Players;
            public Dist RunsDemoShopDone, HoursDemoShopDone, RunsComplete, HoursComplete, RunsFinalStage, HoursFinalStage, RunsAllShapes, RunsAllUpgrades, RunsAllScoreMax, RunsDemoDone, HoursDemoDone;
            public Dist[] FirstReachRun;      // stage k ilk geçiş run'ı
            public Dist[] Attempts;           // stage k'yı geçmek için gereken run sayısı (k-1 geçildikten sonra)
            public double[] MedianStageByRun, MedianCoinsByRun, MedianComboByRun, MedianMinutesByRun;
            public double[] FirstReachHours;  // stage k'ya ulaşma süresi (medyan saat)
            public Dist LongestDrySpell;
            public Dictionary<string, double> ReasonPct;
            public double NoopOfferPct, AllNoopOfferPct, NoopPickPct, FreshPoolDeathPct, DeathWithReviveUnusedPct;
            public double MedianRunMinutes;
            public Dist BiggestJump, BestRunCoinShare, RunsBeforeBreakout;
            public Dictionary<string, double> MedianPurchaseRun;   // kalem → medyan alım run'ı
        }

        static void Summarize(List<PlayerLog> all, bool demo)
        {
            int K = G.Milestones.Count;
            var res = new List<SetupSummary>();
            foreach (var grp in all.GroupBy(l => (l.Persona, l.Policy)))
            {
                var L = grp.ToList(); int n = L.Count;
                var s = new SetupSummary
                {
                    Persona = grp.Key.Persona, Policy = grp.Key.Policy, Players = n,
                    RunsComplete = D(L.Select(l => (double)l.RunsComplete), n),
                    HoursComplete = D(L.Select(l => l.HoursComplete), n),
                    RunsFinalStage = D(L.Select(l => (double)l.RunsFinalStage), n),
                    HoursFinalStage = D(L.Select(l => l.HoursFinalStage), n),
                    RunsAllShapes = D(L.Select(l => (double)l.RunsAllShapes), n),
                    RunsAllUpgrades = D(L.Select(l => (double)l.RunsAllUpgrades), n),
                    RunsAllScoreMax = D(L.Select(l => (double)l.RunsAllScoreMax), n),
                    RunsDemoDone = D(L.Select(l => (double)l.RunsDemoDone), n),
                    RunsDemoShopDone = D(L.Select(l => (double)l.RunsDemoShopDone), n),
                    HoursDemoShopDone = D(L.Select(l => l.HoursDemoShopDone), n),
                    HoursDemoDone = D(L.Select(l => l.HoursDemoDone), n),
                    FirstReachRun = new Dist[K + 1], Attempts = new Dist[K + 1], FirstReachHours = new double[K + 1],
                    LongestDrySpell = D(L.Select(l => (double)Math.Max(l.LongestDrySpell, 0) + 0.0001), n),
                };
                for (int k = 1; k <= K; k++)
                {
                    s.FirstReachRun[k] = D(L.Select(l => (double)l.FirstReachRun[k]), n);
                    s.Attempts[k] = D(L.Where(l => l.FirstReachRun[k] > 0 && (k == 1 || l.FirstReachRun[k - 1] > 0))
                                       .Select(l => (double)(l.FirstReachRun[k] - (k == 1 ? 0 : l.FirstReachRun[k - 1]) + (k == 1 ? 0 : 1))), n);
                    s.FirstReachHours[k] = Pct(L.Where(l => l.FirstReachRun[k] > 0)
                        .Select(l => l.MinutesPerRun.Take(l.FirstReachRun[k]).Sum() / 60.0 + l.FirstReachRun[k] * 15 / 3600.0).ToList(), 0.5);
                }
                int maxR = 80;
                double Med(Func<PlayerLog, int, double?> f, int r) => Pct(L.Select(l => f(l, r)).Where(x => x.HasValue).Select(x => x.Value).ToList(), 0.5);
                s.MedianStageByRun = Enumerable.Range(0, maxR).Select(r => Med((l, i) => i < l.StagePerRun.Count ? l.StagePerRun[i] : (double)K, r)).ToArray();
                s.MedianCoinsByRun = Enumerable.Range(0, maxR).Select(r => Med((l, i) => i < l.CoinsPerRun.Count ? l.CoinsPerRun[i] : null, r)).ToArray();
                s.MedianComboByRun = Enumerable.Range(0, maxR).Select(r => Med((l, i) => i < l.MaxComboPerRun.Count ? l.MaxComboPerRun[i] : null, r)).ToArray();
                s.MedianMinutesByRun = Enumerable.Range(0, maxR).Select(r => Med((l, i) => i < l.MinutesPerRun.Count ? l.MinutesPerRun[i] : null, r)).ToArray();
                int totalRuns = L.Sum(l => l.Runs);
                s.ReasonPct = L.SelectMany(l => l.Reasons).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => 100.0 * g.Sum(kv => kv.Value) / totalRuns);
                s.NoopOfferPct = 100.0 * L.Sum(l => l.OffersNoop) / Math.Max(1, L.Sum(l => l.Offers));
                s.AllNoopOfferPct = 100.0 * L.Sum(l => l.AllNoopOffers) / Math.Max(1, L.Sum(l => l.Picks));
                s.NoopPickPct = 100.0 * L.Sum(l => l.PicksNoop) / Math.Max(1, L.Sum(l => l.Picks));
                s.FreshPoolDeathPct = 100.0 * L.Sum(l => l.FreshPoolDeaths) / totalRuns;
                s.DeathWithReviveUnusedPct = 100.0 * L.Sum(l => l.DeathsWithReviveUnused) / totalRuns;
                s.MedianRunMinutes = Pct(L.SelectMany(l => l.MinutesPerRun).ToList(), 0.5);
                double Jump(PlayerLog l) { int best = 0, j = 0; foreach (var st in l.StagePerRun) { j = Math.Max(j, st - best); best = Math.Max(best, st); } return j; }
                s.BiggestJump = D(L.Select(Jump), n);
                s.BestRunCoinShare = D(L.Select(l => 100.0 * l.CoinsPerRun.Max() / Math.Max(1, l.CoinsTotal)), n);
                s.RunsBeforeBreakout = D(L.Select(l => (double)(l.StagePerRun.FindIndex(st => st >= 10) + 1)), n);
                Console.WriteLine($"   biggestJump med {s.BiggestJump.Median} bestRunCoinShare med {s.BestRunCoinShare.Median:0}% firstStage10Run med {s.RunsBeforeBreakout.Median}");
                s.MedianPurchaseRun = L.SelectMany(l => l.Purchases.Select(p => p.Split(':', 2)).GroupBy(p => p[1]).Select(g => (item: g.Key, run: int.Parse(g.First()[0]))))
                    .GroupBy(x => x.item).ToDictionary(g => g.Key, g => Pct(g.Select(x => (double)x.run).ToList(), 0.5));
                res.Add(s);

                Console.WriteLine($"== {s.Persona}/{s.Policy} (n={n})");
                Console.WriteLine($"   complete: runs med {s.RunsComplete.Median} [p10 {s.RunsComplete.P10} – p90 {s.RunsComplete.P90}] hours med {s.HoursComplete.Median:0.0} [{s.HoursComplete.P10:0.0}–{s.HoursComplete.P90:0.0}] missing {s.RunsComplete.Missing}");
                Console.WriteLine($"   finalStage runs med {s.RunsFinalStage.Median}  allShapes {s.RunsAllShapes.Median}  allUpg {s.RunsAllUpgrades.Median}  allScore {s.RunsAllScoreMax.Median}  demoDone {s.RunsDemoDone.Median} ({s.HoursDemoDone.Median:0.0}h) demoShopDone {s.RunsDemoShopDone.Median} ({s.HoursDemoShopDone.Median:0.0}h, missing {s.RunsDemoShopDone.Missing})");
                Console.WriteLine("   attempts(med) " + string.Join(" ", Enumerable.Range(1, K).Select(k => $"S{k}:{s.Attempts[k].Median:0}")));
                Console.WriteLine("   firstReach(med run) " + string.Join(" ", Enumerable.Range(1, K).Select(k => $"S{k}:{s.FirstReachRun[k].Median:0}")));
                Console.WriteLine($"   noopOffer {s.NoopOfferPct:0.0}%  allNoop {s.AllNoopOfferPct:0.0}%  noopPick {s.NoopPickPct:0.0}%  freshPoolDeath {s.FreshPoolDeathPct:0.0}%  reviveUnused {s.DeathWithReviveUnusedPct:0.0}%  dry {s.LongestDrySpell.Median:0}  runMin {s.MedianRunMinutes:0.0}");
                Console.WriteLine("   reasons " + string.Join(" ", s.ReasonPct.Select(kv => $"{kv.Key}:{kv.Value:0.0}%")));
            }
            LastSummary = res;
            if (MetaTag == "") Write(demo ? "summary_demo.json" : "summary_full.json", res);
        }

        // ── Tek oyuncunun run run dökümü ────────────────────────────────────
        static void Trace(string persona, int seed, bool demo)
        {
            var bp = Personas.All().First(x => x.Name == persona);
            var rng = new Rng((ulong)(seed * 2654435761L + 1));
            var bot = MakeBot(bp, rng);
            var p = new Profile(G);
            var items = Catalog("cheapest");
            var log = new PlayerLog { FirstReachRun = new int[G.Milestones.Count + 1] };
            for (int run = 1; run <= 400; run++)
            {
                var st = new Run(G, p, new Config { Demo = demo }, rng).Play(bot);
                p.Coins += st.Coins;
                int before = log.Purchases.Count;
                Shop(p, items, "cheapest", run, log);
                var bought = log.Purchases.Skip(before).Select(x => x.Split(':', 2)[1]).ToList();
                Console.WriteLine($"run {run,3}: stage {st.Stage,2} score {st.Score,10} moves {st.Placements,4} maxCombo x{st.MaxCombo,6:0.0} coins +{st.Coins,8} bank {p.Coins,8} {st.Reason,-13} " +
                                  $"perStage[{string.Join(",", st.StagePlacements)}] cards[{string.Join(",", st.Picked.Select(c => c.Replace("Card_", "")))}] buy[{string.Join(",", bought.GroupBy(b => b).Select(g => g.Count() > 1 ? $"{g.Key}x{g.Count()}" : g.Key))}]");
                if (Complete(p) && st.Stage >= G.Milestones.Count) break;
                if (demo && st.Reason == Reason.DemoEnd && run > 30) break;
            }
        }

        // ── Bot ağırlık ayarı: tepe tırmanma, ortak rastgele tohumlar ───────
        static readonly string[] WNames = { "WLine", "WClear", "WScore", "WFilled", "WTrans", "WHoles", "WFit", "WBig", "WCombo", "WBar" };
        static float[] GetW(BotProfile p) => new[] { p.WLine, p.WClear, p.WScore, p.WFilled, p.WTrans, p.WHoles, p.WFit, p.WBig, p.WCombo, p.WBar };
        static void SetW(BotProfile p, float[] w)
        {
            p.WLine = w[0]; p.WClear = w[1]; p.WScore = w[2]; p.WFilled = w[3]; p.WTrans = w[4];
            p.WHoles = w[5]; p.WFit = w[6]; p.WBig = w[7]; p.WCombo = w[8]; p.WBar = w[9];
        }

        static void Tune(string persona, int n)
        {
            var bp = Personas.All().First(x => x.Name == persona);
            // Orta ilerleme profili: bot'un skor ve hayatta kalma dengesini aynı anda zorlar
            Profile Prof()
            {
                var p = new Profile(G);
                for (int s = 0; s < G.Shapes.Count; s++) p.ScoreLvl[s] = 3;
                return p;
            }
            double Fit(float[] w)
            {
                var b = Personas.All().First(x => x.Name == persona); SetW(b, w);
                var l = RunMany(n, i => (Prof(), new Config(), b, (ulong)(i * 6151 + 7)));
                return l.Average(x => x.Stage) + l.Average(x => Math.Log10(1 + x.Score)) * 0.05;
            }
            var best = GetW(bp); double bf = Fit(best);
            Console.WriteLine($"start {bf:0.000} " + string.Join(" ", best.Select((v, i) => $"{WNames[i]}={v:0.##}")));
            var rng = new Rng(99);
            for (int it = 0; it < 14; it++)
            {
                float scale = it < 7 ? 0.6f : 0.3f;
                for (int c = 0; c < 6; c++)
                {
                    var w = (float[])best.Clone();
                    for (int k = 0; k < w.Length; k++)
                        if (rng.Value() < 0.5f) w[k] *= MathF.Exp((rng.Value() * 2 - 1) * scale);
                    double f = Fit(w);
                    if (f > bf) { bf = f; best = w; Console.WriteLine($"it{it} {bf:0.000} " + string.Join(" ", best.Select((v, i) => $"{WNames[i]}={v:0.##}"))); }
                }
            }
            Console.WriteLine("BEST " + string.Join(", ", best.Select((v, i) => $"{WNames[i]} = {v:0.###}f")));
        }

        // ── Kart etki analizi ───────────────────────────────────────────────
        sealed class ForcedBot : IBot
        {
            readonly Bot inner; readonly string forced; bool done;
            public ForcedBot(Bot b, string id) { inner = b; forced = id; }
            public Move Choose(Run r) => inner.Choose(r);
            public bool WantPoolReroll(Run r) => inner.WantPoolReroll(r);
            public int PickCard(Run r, List<CardDef> offer, bool canReroll)
            {
                if (!done && forced != null)
                {
                    done = true;
                    // teklif edilen 3'ün ilkini zorlanan kartla değiştir
                    var c = G.Cards.First(x => x.Id == forced);
                    offer[0] = c;
                    return 0;
                }
                return inner.PickCard(r, offer, canReroll);
            }
        }

        public sealed class CardImpact { public string Id; public double AvgStage, DeltaStage, AvgScore, ReachS4, ReachS6, DeltaReachS4, Ci95; }

        static void Cards(int n)
        {
            // Orta seviye bir profil: ilk 3 stage'i güvenle geçen, kartların fark yarattığı bölge.
            var bp = Personas.Mid();
            bp.CardPolicy = 0; // sonraki seçimler rastgele: yalnızca zorlanan kartın etkisi ölçülür
            Profile MakeProf()
            {
                var p = new Profile(G);
                foreach (var s in G.Shapes) { p.ShapeUnlocked[s.Idx] = true; p.ScoreLvl[s.Idx] = 3; }
                p.MaxMilestoneReached = 6;
                return p;
            }
            var cfg = new Config();
            List<RunStats> RunWith(string id) =>
                RunManyBot(n, i => (MakeProf(), cfg, (ulong)(i * 7727 + 11)), (b, r) => new ForcedBot(b, id), bp);

            var baseL = RunWith(null);
            double b0 = baseL.Average(x => x.Stage), b4 = Reach(baseL, 4);
            Console.WriteLine($"baseline avgStage {b0:0.00} reachS4 {b4:0.0}% reachS6 {Reach(baseL, 6):0.0}%");
            var list = new List<CardImpact> { new() { Id = "(none/random)", AvgStage = b0, AvgScore = baseL.Average(x => x.Score), ReachS4 = b4, ReachS6 = Reach(baseL, 6) } };
            foreach (var c in G.Cards)
            {
                var l = RunWith(c.Id);
                double avg = l.Average(x => x.Stage);
                double sd = Math.Sqrt(l.Average(x => (x.Stage - avg) * (x.Stage - avg)));
                var ci = new CardImpact
                {
                    Id = c.Id, AvgStage = avg, DeltaStage = avg - b0, AvgScore = l.Average(x => x.Score),
                    ReachS4 = Reach(l, 4), ReachS6 = Reach(l, 6), DeltaReachS4 = Reach(l, 4) - b4,
                    Ci95 = 1.96 * sd / Math.Sqrt(l.Count) * Math.Sqrt(2)
                };
                list.Add(ci);
                Console.WriteLine($"{c.Id,-26} Δstage {ci.DeltaStage,6:+0.00;-0.00} ±{ci.Ci95:0.00}  reachS4 {ci.ReachS4,5:0.0}%  ΔS4 {ci.DeltaReachS4,6:+0.0;-0.0}");
            }
            Write("cards.json", list);
        }

        static double Reach(List<RunStats> l, int k) => 100.0 * l.Count(x => x.Stage >= k) / l.Count;

        static List<RunStats> RunManyBot(int n, Func<int, (Profile, Config, ulong)> setup, Func<Bot, Rng, IBot> wrap, BotProfile bp)
        {
            var res = new RunStats[n];
            Parallel.For(0, n, i =>
            {
                var (prof, cfg, seed) = setup(i);
                var rng = new Rng(seed);
                res[i] = new Run(G, prof, cfg, rng).Play(wrap(MakeBot(bp, rng), rng));
            });
            return res.ToList();
        }

        // ── Hata etkisi ─────────────────────────────────────────────────────
        static void Bugs(int n)
        {
            var bp = Personas.Mid();
            Profile Prof()
            {
                var p = new Profile(G);
                p.MaxMilestoneReached = 7;
                foreach (var s in G.Shapes) { p.ShapeUnlocked[s.Idx] = true; p.ScoreLvl[s.Idx] = 6; }
                p.Upg[G.U("upgrade_dead_pool_revive")] = 2;
                return p;
            }
            var asIs = RunMany(n, i => (Prof(), new Config { Legacy = true }, bp, (ulong)(i * 31 + 5)));
            var fixd = RunMany(n, i => (Prof(), new Config(), bp, (ulong)(i * 31 + 5)));
            double freshAs = 100.0 * asIs.Count(x => x.DiedOnFreshPool) / n;
            double unused = 100.0 * asIs.Count(x => x.RevivesUnused) / n;
            Console.WriteLine($"Revive x2 — as-is: avgStage {asIs.Average(x => x.Stage):0.00}, revives used/run {asIs.Average(x => x.RevivesUsed):0.00}, died on fresh pool {freshAs:0}%, died with revive unused {unused:0}%");
            Console.WriteLine($"Revive x2 — fixed: avgStage {fixd.Average(x => x.Stage):0.00}, revives used/run {fixd.Average(x => x.RevivesUsed):0.00}");
            var noRev = RunMany(n, i => { var p = Prof(); p.Upg[G.U("upgrade_dead_pool_revive")] = 0; return (p, new Config(), bp, (ulong)(i * 31 + 5)); });
            Console.WriteLine($"No revive: avgStage {noRev.Average(x => x.Stage):0.00}");
            Write("bugs.json", new
            {
                AsIsAvgStage = asIs.Average(x => x.Stage), FixedAvgStage = fixd.Average(x => x.Stage), NoReviveAvgStage = noRev.Average(x => x.Stage),
                AsIsRevivesUsed = asIs.Average(x => x.RevivesUsed), FixedRevivesUsed = fixd.Average(x => x.RevivesUsed),
                FreshPoolDeathPct = freshAs, DiedWithReviveUnusedPct = unused
            });
        }

        // ── Meta ilerleme ───────────────────────────────────────────────────
        public sealed class PlayerLog
        {
            public string Persona, Policy; public int Seed;
            public int[] FirstReachRun;          // stage k ilk kez hangi run'da geçildi (1 tabanlı), 0 = hiç
            public int RunsAllShapes, RunsAllUpgrades, RunsAllScoreMax, RunsComplete, RunsFinalStage, RunsDemoDone;
            public double HoursComplete, HoursFinalStage, HoursDemoDone;
            public List<int> StagePerRun = new();
            public List<int> CoinsPerRun = new();
            public List<int> MovesPerRun = new();
            public List<double> MinutesPerRun = new();
            public List<string> Purchases = new();   // "run:item"
            public List<float> MaxComboPerRun = new();
            public Dictionary<string, int> Reasons = new();
            public int Offers, OffersNoop, AllNoopOffers, PicksNoop, Picks, FreshPoolDeaths, DeathsWithReviveUnused;
            public int LongestDrySpell;              // yeni stage da yok, alışveriş de yok
            public long CoinsTotal;
            public List<long> SpentPerRun = new();
            public int RunsDemoShopDone; public double HoursDemoShopDone;
            public int Runs;
            public double Hours;
        }

        abstract class Item { public string Name; public abstract int Cost(Profile p); public abstract bool Available(Profile p); public abstract void Buy(Profile p); public int Class; }

        sealed class UnlockShape : Item
        {
            public ShapeDef S;
            public override int Cost(Profile p) => (int)(S.UnlockCost * PriceScale);
            public override bool Available(Profile p) => S.Locked && !p.ShapeUnlocked[S.Idx];
            public override void Buy(Profile p) => p.ShapeUnlocked[S.Idx] = true;
        }
        sealed class ScoreUp : Item
        {
            public ShapeDef S;
            public override int Cost(Profile p) => (int)(S.ScoreCostFor(p.ScoreLvl[S.Idx]) * PriceScale);
            public override bool Available(Profile p) => p.ShapeAvailable(S) && p.ScoreLvl[S.Idx] < 10;
            public override void Buy(Profile p) => p.ScoreLvl[S.Idx]++;
        }
        sealed class Upg : Item
        {
            public UpgradeDef U;
            public override int Cost(Profile p) => (int)(U.CostFor(p.Upg[U.Idx] + 1) * PriceScale);
            public override bool Available(Profile p) => p.UpgradeOpen(U) && p.Upg[U.Idx] < U.MaxLevel;
            public override void Buy(Profile p) => p.Upg[U.Idx]++;
        }

        static List<Item> Catalog(string policy)
        {
            var items = new List<Item>();
            var hard = new HashSet<string> { "Shape_Cross", "Shape_6Sq", "Shape_LongL" };
            var core = new HashSet<string> { "upgrade_pool_capacity", "upgrade_starting_combo", "upgrade_combo_gain", "upgrade_pool_reroll" };
            foreach (var s in G.Shapes)
            {
                if (s.Locked) items.Add(new UnlockShape { S = s, Name = "unlock:" + s.Id, Class = policy == "smart" && hard.Contains(s.Id) ? 2 : 0 });
                items.Add(new ScoreUp { S = s, Name = "score:" + s.Id, Class = policy == "smart" && hard.Contains(s.Id) ? 2 : 0 });
            }
            foreach (var u in G.Upgrades)
                items.Add(new Upg { U = u, Name = "upg:" + u.Id, Class = policy == "smart" ? (core.Contains(u.Id) ? 0 : 1) : 0 });
            return items;
        }

        static bool Complete(Profile p) =>
            G.Shapes.All(s => !s.Locked || p.ShapeUnlocked[s.Idx]) &&
            G.Upgrades.All(u => p.Upg[u.Idx] >= u.MaxLevel) &&
            G.Shapes.All(s => p.ScoreLvl[s.Idx] >= 10);

        static int Shop(Profile p, List<Item> items, string policy, int run, PlayerLog log)
        {
            int bought = 0;
            while (true)
            {
                var avail = items.Where(i => i.Available(p)).ToList();
                if (avail.Count == 0) break;
                Item pick;
                if (policy == "smart")
                {
                    // en düşük sınıftaki en ucuz kalem için para biriktir
                    int cls = avail.Min(i => i.Class);
                    pick = avail.Where(i => i.Class == cls).OrderBy(i => i.Cost(p)).First();
                }
                else pick = avail.OrderBy(i => i.Cost(p)).First();
                int cost = pick.Cost(p);
                if (p.Coins < cost) break;
                p.Coins -= cost; pick.Buy(p); bought++;
                log.Purchases.Add($"{run}:{pick.Name}");
            }
            return bought;
        }

        // Senaryo deneyleri bunları değiştirir; varsayılan = oyundaki hâl
        public static Func<Config> MakeCfg = () => new Config();
        public static float PriceScale = 1f;
        public static List<(BotProfile bp, string policy)> MetaSetups;
        public static string MetaTag = "";
        public static List<SetupSummary> LastSummary;

        static void Meta(int players, int maxRuns, string mode)
        {
            bool demo = mode == "demo";
            var logs = new ConcurrentBag<PlayerLog>();
            var setups = MetaSetups ?? new List<(BotProfile bp, string policy)>
            {
                (Personas.Casual(), "cheapest"), (Personas.Mid(), "cheapest"),
                (Personas.Skilled(), "cheapest"), (Personas.Skilled(), "smart")
            };
            int K = G.Milestones.Count;
            int doneCount = 0;
            var jobs = setups.SelectMany(s => Enumerable.Range(0, players).Select(i => (s.bp, s.policy, i))).ToList();
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, job =>
            {
                var (bp, policy, i) = job;
                var rng = new Rng((ulong)(i * 2654435761L + bp.Name.Length * 97 + policy.Length));
                var bot = MakeBot(bp, rng);
                var p = new Profile(G);
                var cfg = MakeCfg(); cfg.Demo = demo;
                var items = Catalog(policy);
                var log = new PlayerLog { Persona = bp.Name, Policy = policy, Seed = i, FirstReachRun = new int[K + 1] };
                double seconds = 0; int dry = 0; int bestStage = 0;
                for (int run = 1; run <= maxRuns; run++)
                {
                    var st = new Run(G, p, cfg, rng).Play(bot);
                    p.Coins += st.Coins; log.CoinsTotal += st.Coins;
                    seconds += RunSeconds(st, bp) + 15; // mağaza / menü
                    log.StagePerRun.Add(st.Stage); log.CoinsPerRun.Add(st.Coins); log.MovesPerRun.Add(st.Placements);
                    log.MaxComboPerRun.Add(st.MaxCombo);
                    log.Reasons[st.Reason.ToString()] = log.Reasons.GetValueOrDefault(st.Reason.ToString()) + 1;
                    log.Offers += st.Offers; log.OffersNoop += st.OffersNoop; log.AllNoopOffers += st.AllNoopOffers; log.PicksNoop += st.PicksNoop; log.Picks += st.CardPicks;
                    if (st.DiedOnFreshPool) log.FreshPoolDeaths++; if (st.RevivesUnused) log.DeathsWithReviveUnused++;
                    log.MinutesPerRun.Add(RunSeconds(st, bp) / 60.0);
                    bool newStage = st.Stage > bestStage;
                    for (int k = 1; k <= st.Stage; k++) if (log.FirstReachRun[k] == 0) log.FirstReachRun[k] = run;
                    bestStage = Math.Max(bestStage, st.Stage);
                    int bought = Shop(p, items, policy, run, log);
                    if (!newStage && bought == 0) { dry++; log.LongestDrySpell = Math.Max(log.LongestDrySpell, dry); } else dry = 0;

                    if (log.RunsAllShapes == 0 && G.Shapes.All(s => !s.Locked || p.ShapeUnlocked[s.Idx])) log.RunsAllShapes = run;
                    if (log.RunsAllUpgrades == 0 && G.Upgrades.All(u => p.Upg[u.Idx] >= u.MaxLevel)) log.RunsAllUpgrades = run;
                    if (log.RunsAllScoreMax == 0 && G.Shapes.All(s => p.ScoreLvl[s.Idx] >= 10)) log.RunsAllScoreMax = run;
                    if (log.RunsFinalStage == 0 && st.Stage >= K) { log.RunsFinalStage = run; log.HoursFinalStage = seconds / 3600; }
                    if (demo && log.RunsDemoDone == 0 && st.Reason == Reason.DemoEnd) { log.RunsDemoDone = run; log.HoursDemoDone = seconds / 3600; }
                    log.Runs = run; log.Hours = seconds / 3600;
                    if (Complete(p))
                    {
                        log.RunsComplete = run; log.HoursComplete = seconds / 3600;
                        if (log.RunsFinalStage > 0 || demo) break;
                    }
                    log.SpentPerRun.Add(log.CoinsTotal - p.Coins);
                    if (demo && log.RunsDemoDone > 0 && items.All(it => !it.Available(p))) { log.RunsDemoShopDone = run; log.HoursDemoShopDone = seconds / 3600; break; }
                }
                logs.Add(log);
                int d = Interlocked.Increment(ref doneCount);
                Console.WriteLine($"  [{d}/{jobs.Count}] {bp.Name}/{policy}#{i}: runs {log.Runs} complete@{log.RunsComplete} final@{log.RunsFinalStage} best {bestStage} hours {log.Hours:0.0}");
            });
            var all = logs.OrderBy(l => l.Persona).ThenBy(l => l.Policy).ThenBy(l => l.Seed).ToList();
            if (MetaTag == "") Write(demo ? "meta_demo.json" : "meta_full.json", all);
            Summarize(all, demo);
        }
    }
}
