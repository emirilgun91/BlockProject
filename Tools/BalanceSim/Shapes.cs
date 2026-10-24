// Şekil analizi: her şekil gerçek oyun tahtalarında ne kadar zor / ne kadar işe yarar,
// ve kilitli şekli açmak oyuncuya ne kazandırır.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace BalanceSim
{
    public static partial class Program
    {
        sealed class BoardProbe : IBot
        {
            readonly Bot inner; readonly ConcurrentBag<ulong> bag; readonly Rng rng; readonly float rate;
            public BoardProbe(Bot b, Rng r, ConcurrentBag<ulong> boards, float sampleRate) { inner = b; rng = r; bag = boards; rate = sampleRate; }
            public Move Choose(Run r)
            {
                if (rng.Range(0, 1000) < rate * 1000) bag.Add(r.Occ);
                return inner.Choose(r);
            }
            public bool WantPoolReroll(Run r) => inner.WantPoolReroll(r);
            public int PickCard(Run r, List<CardDef> offer, bool canReroll) => inner.PickCard(r, offer, canReroll);
        }

        static int LinesAfter(ulong occ)
        {
            int n = 0;
            for (int i = 0; i < 8; i++)
            {
                if ((occ & (0xFFUL << (i * 8))) == 0xFFUL << (i * 8)) n++;
                ulong c = Run.ColMask(i);
                if ((occ & c) == c) n++;
            }
            return n;
        }

        public sealed class ShapeRow
        {
            public string Id; public int Cells, Weight, BaseTile, UnlockCost; public bool Locked;
            public double Share, Fit, FitR0, Options, ClearChance, MaxLines, PtsPerSpawn;
        }

        static Profile RefProfile(bool allUnlocked)
        {
            var p = new Profile(G);
            for (int s = 0; s < G.Shapes.Count; s++) { p.ScoreLvl[s] = 3; p.ShapeUnlocked[s] = allUnlocked; }
            p.MaxMilestoneReached = 7;
            return p;
        }

        static void Shapes(int n)
        {
            // 1) Gerçekçi tahtalar: Mid oyuncunun ~10. run'ındaki hâli
            var boards = new ConcurrentBag<ulong>();
            RunManyBot(n, i => (RefProfile(false), new Config(), (ulong)(i * 7919 + 3)), (b, r) => new BoardProbe(b, r, boards, 0.25f), Personas.Mid());
            var B = boards.ToArray();
            Console.WriteLine($"{B.Length} tahta örneği, ortalama doluluk {B.Average(o => BitOperations.PopCount(o)):0.0}/64");

            int totalW = G.Shapes.Sum(s => s.BaseWeight);
            var rows = new ShapeRow[G.Shapes.Count];
            Parallel.For(0, G.Shapes.Count, k =>
            {
                var s = G.Shapes[k];
                long fit = 0, fit0 = 0, clear = 0; double opts = 0, maxL = 0;
                foreach (var occ in B)
                {
                    int ok = 0, best = 0;
                    foreach (var m in s.Distinct)
                    {
                        if ((m & occ) != 0) continue;
                        ok++;
                        int l = LinesAfter(occ | m);
                        if (l > best) best = l;
                    }
                    if (ok > 0) fit++;
                    if (BoardEval.AnyFits(occ, s.Masks[0])) fit0++;
                    if (best > 0) clear++;
                    opts += (double)ok / s.Distinct.Length;
                    maxL += best;
                }
                rows[k] = new ShapeRow
                {
                    Id = s.Id, Cells = s.Size, Weight = s.BaseWeight, BaseTile = s.BaseTile, UnlockCost = s.UnlockCost, Locked = s.Locked,
                    Share = 100.0 * s.BaseWeight / totalW, Fit = 100.0 * fit / B.Length, FitR0 = 100.0 * fit0 / B.Length,
                    Options = 100.0 * opts / B.Length, ClearChance = 100.0 * clear / B.Length, MaxLines = maxL / B.Length,
                    PtsPerSpawn = s.Size * s.BaseTile
                };
            });
            Console.WriteLine($"{"şekil",-12} hücre  ağırlık  pay%  taban  sığar%  R0%  seçenek%  temizler%  ort.satır  puan/parça  kilit");
            foreach (var r in rows.OrderBy(r => r.Fit))
                Console.WriteLine($"{r.Id,-12} {r.Cells,5} {r.Weight,8} {r.Share,5:0.0} {r.BaseTile,6} {r.Fit,7:0.0} {r.FitR0,5:0.0} {r.Options,9:0.0} {r.ClearChance,10:0.0} {r.MaxLines,10:0.00} {r.PtsPerSpawn,11:0} {(r.Locked ? $"{r.UnlockCost}" : "-"),6}");

            Console.WriteLine();
            Console.WriteLine("seviye eğrisi (Shape_Z): " + string.Join(" ", Enumerable.Range(0, 11).Select(l => $"L{l}:{G.Shapes.First(s => s.Id == "Shape_Z").TileAt(l):0}")));
            foreach (var s in G.Shapes.GroupBy(s => string.Join(",", s.Cost)).Select(g => g.First()))
                Console.WriteLine($"maliyet ({s.Id}): " + string.Join(" ", Enumerable.Range(0, 10).Select(l => s.ScoreCostFor(l))) + $"  ağırlık +{s.WInc}/-{s.WDec}");
            Write("shapes.json", rows);
        }
    }
}
namespace BalanceSim
{
    public static partial class Program
    {
        public sealed class WorthRow { public string Id, Persona; public int Cells; public double Without, EqualPay, Neutral, Lo, Hi; }

        /// <summary>
        /// Her şekil için "nötr hücre değeri": şekil oyunun %25'ini oluştururken hücre başına kaç puan
        /// verirse oyuncu, o şekil hiç yokmuş gibi ilerler. Herkes eşit 25 puanla ölçülür (Z'nin Lv3 değeri);
        /// sonuç 25'e oranla verilir. Oran > 1: şekil zor, daha fazla ödemeli; < 1: kolay/faydalı.
        /// </summary>
        static void ShapeWorth(int n, string persona)
        {
            var bp = persona == "Casual" ? Personas.Casual() : persona == "Skilled" ? Personas.Skilled() : Personas.Mid();
            int S = G.Shapes.Count;
            const float V = 25f;
            float[] Tiles(int k, float r) { var t = Enumerable.Repeat(V, S).ToArray(); if (k >= 0) t[k] = V * r; return t; }
            int[] Weights(int k, int w) { var a = G.Shapes.Select(s => s.BaseWeight).ToArray(); if (k >= 0) a[k] = w; return a; }
            double Stage(Config c) => RunMany(n, i => (RefProfile(true), c, bp, (ulong)(i * 6151 + 17))).Average(x => x.Stage);

            double all = Stage(new Config { TileOverride = Tiles(-1, 1), WeightOverride = Weights(-1, 0) });
            Console.WriteLine($"[{bp.Name}] hepsi açık, hepsi {V}: stage {all:0.00}");
            var rows = new List<WorthRow>();
            int totalW = G.Shapes.Sum(s => s.BaseWeight);
            for (int k = 0; k < S; k++)
            {
                var s = G.Shapes[k];
                int boost = (int)Math.Round((totalW - s.BaseWeight) / 3.0);   // şekil = spawn'ların %25'i
                double wo = Stage(new Config { TileOverride = Tiles(-1, 1), WeightOverride = Weights(k, 0) });
                double At(float r) => Stage(new Config { TileOverride = Tiles(k, r), WeightOverride = Weights(k, boost) });
                double eq = At(1f);
                float lo = 0.2f, hi = 8f;
                for (int it = 0; it < 9; it++)
                {
                    float mid = MathF.Sqrt(lo * hi);
                    if (At(mid) > wo) hi = mid; else lo = mid;
                }
                var row = new WorthRow { Id = s.Id, Persona = bp.Name, Cells = s.Size, Without = wo, EqualPay = eq - wo, Neutral = MathF.Sqrt(lo * hi), Lo = lo, Hi = hi };
                rows.Add(row);
                Console.WriteLine($"{s.Id,-12} hücre {s.Size}  yokken {wo,5:0.00} | eşit ücretle %25 iken Δ {row.EqualPay,6:+0.00;-0.00}  nötr oran ×{row.Neutral:0.00} (= {V * row.Neutral:0.0} puan/hücre)");
            }
            Write($"shapeworth_{bp.Name}.json", rows);
        }
    }
}
namespace BalanceSim
{
    public static partial class Program
    {
        /// <summary>Bounty Hunter / Future Investment öneri varyantları: kart ilk seçimde zorlanır.</summary>
        static void CardVariants(int n)
        {
            var bp = Personas.Mid(); bp.CardPolicy = 0;
            Profile Prof() { var p = new Profile(G); foreach (var s in G.Shapes) { p.ShapeUnlocked[s.Idx] = true; p.ScoreLvl[s.Idx] = 3; } p.MaxMilestoneReached = 6; return p; }
            (double st, double coins, double s4) Go(string card, Config c)
            {
                var l = RunManyBot(n, i => (Prof(), c, (ulong)(i * 7727 + 11)), (b, r) => new ForcedBot(b, card), bp);
                return (l.Average(x => x.Stage), l.Average(x => x.Coins), Reach(l, 4));
            }
            var b0 = Go(null, new Config());
            Console.WriteLine($"kartsız: stage {b0.st:0.00}  coin/run {b0.coins:0}  S4 %{b0.s4:0}");
            void Variant(string label, string card, (E t, float v)[] eff, Config c)
            {
                var def = G.Cards.First(x => x.Id == card);
                var keep = def.Effects.ToList();
                def.Effects = eff.Select(e => (e.t, e.v)).ToList();
                var r = Go(card, c);
                def.Effects = keep;
                Console.WriteLine($"{label,-44} Δstage {r.st - b0.st,6:+0.00;-0.00}  coin/run {r.coins,7:0} ({(r.coins / b0.coins - 1) * 100,4:+0;-0}%)  S4 %{r.s4:0}");
            }
            const string BH = "Card_Bounty_Hunter", FI = "Card_Future_Investment";
            Variant("BH bugün: 1 coin/satır, hedefler ×1.2", BH, new[] { (E.BountyHunterCoinPerClear, 1f), (E.BountyHunterThresholdScale, 1.2f) }, new Config());
            foreach (var (pct, sc) in new[] { (0.02f, 1.1f), (0.02f, 1.2f), (0.03f, 1.1f), (0.05f, 1.1f) })
                Variant($"BH: aşama ödülünün %{pct * 100:0}'i/satır, ×{sc}", BH, new[] { (E.BountyHunterCoinPerClear, 1f), (E.BountyHunterThresholdScale, sc) }, new Config { BountyPct = pct });
            foreach (var (cur, next) in new[] { (1.2f, 0.15f), (1.15f, 0.2f), (1.1f, 0.2f), (1.2f, 0.3f), (1.15f, 0.25f), (1.1f, 0.15f) })
                Variant($"FI: bu hedef ×{cur}, sonraki −%{next * 100:0}", FI, new[] { (E.FutureInvestmentCurrentScale, cur), (E.FutureInvestmentNextDiscount, next) }, new Config());
            foreach (var (cur, next) in new[] { (1.2f, 0.15f), (1.2f, 0.2f), (1.2f, 0.25f), (1.15f, 0.2f), (1.25f, 0.3f) })
                Variant($"FI fark: bu aşama +%{(cur - 1) * 100:0}, sonraki −%{next * 100:0}", FI, new[] { (E.FutureInvestmentCurrentScale, cur), (E.FutureInvestmentNextDiscount, next) }, new Config { MilestoneGapScale = true });
            foreach (var (cur, next) in new[] { (1.2f, 0.15f), (1.2f, 0.25f), (1.1f, 0.15f), (1.1f, 0.2f), (1.15f, 0.3f) })
                Variant($"FI hediye: bu aşama +%{(cur - 1) * 100:0}, sonrakine %{next * 100:0} skor", FI, new[] { (E.FutureInvestmentCurrentScale, cur), (E.FutureInvestmentNextDiscount, next) }, new Config { MilestoneGapScale = true, FutureInvestmentGift = true });
        }
    }
}
namespace BalanceSim
{
    public static partial class Program
    {
        /// <summary>Dead Pool Revive ile önerilen milestone geçiş hakkının karşılaştırması (yatırım deneyiyle aynı referans).</summary>
        static void PassVariants(int n)
        {
            var bp = Personas.Mid();
            Profile Base() { var p = new Profile(G); for (int s = 0; s < G.Shapes.Count; s++) p.ScoreLvl[s] = 3; p.MaxMilestoneReached = 7; return p; }
            (double st, double coins, double used) Go(Func<Profile> f, Config c)
            {
                var l = RunMany(n, i => (f(), c, bp, (ulong)(i * 4243 + 9)));
                return (l.Average(x => x.Stage), l.Average(x => x.Coins), l.Average(x => x.PassesUsed + x.RevivesUsed));
            }
            var b = Go(Base, new Config());
            Console.WriteLine($"referans: stage {b.st:0.00} coin {b.coins:0}");
            void Show(string label, (double st, double coins, double used) r) =>
                Console.WriteLine($"{label,-40} Δstage {r.st - b.st,6:+0.00;-0.00}  coin {r.coins,7:0}  kullanım/run {r.used:0.00}");
            var rev = G.Upgrades.First(u => u.Id == "upgrade_dead_pool_revive");
            Show("Dead Pool Revive Lv1", Go(() => { var p = Base(); p.Upg[rev.Idx] = 1; return p; }, new Config()));
            Show("Dead Pool Revive Lv2", Go(() => { var p = Base(); p.Upg[rev.Idx] = 2; return p; }, new Config()));
            foreach (var pct in new[] { 0f, 0.5f, 0.75f, 0.9f })
                Show($"geçiş hakkı ×1, skor ≥ hedefin %{pct * 100:0}'i", Go(Base, new Config { MilestonePasses = 1, PassMinPct = pct }));
            Show("geçiş hakkı ×1, %75, coin de verir", Go(Base, new Config { MilestonePasses = 1, PassMinPct = 0.75f, PassGivesCoins = true }));
            Show("geçiş hakkı ×2, %75", Go(Base, new Config { MilestonePasses = 2, PassMinPct = 0.75f }));
        }
    }
}
namespace BalanceSim
{
    public static partial class Program
    {
        public sealed class ShapeProposal
        {
            public string Id; public bool Locked; public int Cells, Weight;
            public double Ratio; public int OldBase, NewBase, OldLv3, NewLv3, OldLv10, NewLv10;
            public double CostFactor; public int OldCostTotal, NewCostTotal, UnlockCost;
            public int[] Gain, Cost;
        }

        public static List<ShapeProposal> Proposal;

        /// <summary>Önerilen kilit açma fiyatları: getiriye göre kademeli (bugün 350–15000).</summary>
        static readonly Dictionary<string, int> NewUnlock = new() { ["Shape_T"] = 500, ["Shape_C"] = 500, ["Shape_Cross"] = 1000, ["Shape_LongL"] = 1000, ["Shape_6Sq"] = 1500, ["Shape_Dot"] = 3000 };

        /// <summary>
        /// Şekil puanı önerisi (shapeworth sonuçlarından):
        ///  · taban hücre değeri = K × nötr oran (kilitli şekillere %15 prim), en az 5;
        ///    K, açık şekillerin ortalama Lv3 değeri bugünküyle aynı kalacak şekilde seçilir;
        ///  · seviye artışları tabanla orantılı (Z'nin bugünkü eğrisi × taban/8) — sıralama Lv10'da da korunur;
        ///  · seviye fiyatı, yükseltmenin getirisiyle orantılı: ağırlık × hücre × taban (0.5–2 arası).
        /// </summary>
        static void BuildShapeProposal(bool apply, bool quiet = false)
        {
            // Oyuncuların çoğu Casual/Mid: usta oyuncu büyük şekilleri (6Sq, Cross) kolay yerleştirdiği için
            // onun ölçümü bu şekilleri ucuz gösteriyor — ağırlıklı geometrik ortalama.
            var ratios = new Dictionary<string, List<(double r, double w)>>();
            foreach (var (f, w) in new[] { ("shapeworth_Casual.json", 0.4), ("shapeworth_Mid.json", 0.4), ("shapeworth_Skilled.json", 0.2) })
            {
                var path = System.IO.Path.Combine(OutDir, f);
                if (!System.IO.File.Exists(path)) continue;
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    string id = e.GetProperty("Id").GetString();
                    if (!ratios.ContainsKey(id)) ratios[id] = new List<(double, double)>();
                    ratios[id].Add((e.GetProperty("Neutral").GetDouble(), w));
                }
            }
            var Z = G.Shapes.First(s => s.Id == "Shape_Z");
            double[] zGain = Enumerable.Range(1, 10).Select(l => (double)(Z.TileAt(l) - Z.TileAt(l - 1))).ToArray();
            int[] zCost = Enumerable.Range(0, 10).Select(Z.ScoreCostFor).ToArray();
            double R(ShapeDef s) => Math.Exp(ratios[s.Id].Sum(x => x.w * Math.Log(x.r)) / ratios[s.Id].Sum(x => x.w));
            double prem(ShapeDef s) => s.Locked ? 1.3 : 1.0;
            var open = G.Shapes.Where(s => !s.Locked).ToList();
            double oldAvg = open.Sum(s => s.BaseWeight * s.Size * (double)s.TileAt(3)) / open.Sum(s => s.BaseWeight * s.Size);
            int Base(ShapeDef s, double k) => Math.Max(5, (int)Math.Round(k * R(s) * prem(s)));
            double newAvg(double k) => open.Sum(s => s.BaseWeight * s.Size * (Base(s, k) * Z.TileAt(3) / Z.BaseTile)) / open.Sum(s => s.BaseWeight * s.Size);
            double lo = 2, hi = 20;
            for (int i = 0; i < 40; i++) { double m = (lo + hi) / 2; if (newAvg(m) > oldAvg) hi = m; else lo = m; }
            double K = (lo + hi) / 2;

            var list = new List<ShapeProposal>();
            foreach (var s in G.Shapes)
            {
                int b = Base(s, K);
                var gain = zGain.Select(g => Math.Max(1, (int)Math.Round(b * g / Z.BaseTile))).ToArray();
                list.Add(new ShapeProposal { Id = s.Id, Locked = s.Locked, Cells = s.Size, Weight = s.BaseWeight, Ratio = R(s), OldBase = s.BaseTile, NewBase = b, Gain = gain,
                    OldLv3 = (int)s.TileAt(3), NewLv3 = b + gain.Take(3).Sum(), OldLv10 = (int)s.TileAt(10), NewLv10 = b + gain.Sum(),
                    OldCostTotal = Enumerable.Range(0, 10).Sum(s.ScoreCostFor), UnlockCost = NewUnlock.TryGetValue(s.Id, out var uc) ? uc : s.UnlockCost });
            }
            double yieldAvg = list.Where(p => !p.Locked).Average(p => (double)p.Weight * p.Cells * p.NewBase);
            foreach (var p in list)
            {
                p.CostFactor = Math.Clamp(p.Weight * p.Cells * p.NewBase / yieldAvg, 0.5, 2.0);
                p.Cost = zCost.Select(c => Math.Max(50, (int)Math.Round(c * p.CostFactor / 50.0) * 50)).ToArray();
                p.NewCostTotal = p.Cost.Sum();
            }
            Proposal = list;
            if (!quiet) Write("shape_proposal.json", list);
            if (!quiet)
            {
                Console.WriteLine($"K {K:0.00}  açık şekillerin ort. Lv3 değeri {oldAvg:0.0} > {newAvg(K):0.0}");
                Console.WriteLine($"{"şekil",-12} hücre ağırlık  nötr   taban    Lv3       Lv10      fiyat×  seviye fiyatları (toplam)");
                foreach (var p in list)
                    Console.WriteLine($"{p.Id,-12} {p.Cells,5} {p.Weight,7} {p.Ratio,6:0.00} {p.OldBase,3}>{p.NewBase,-3} {p.OldLv3,4}>{p.NewLv3,-4} {p.OldLv10,4}>{p.NewLv10,-5} {p.CostFactor,5:0.00}  {string.Join(" ", p.Cost)} ({p.OldCostTotal}>{p.NewCostTotal}){(p.Locked ? $"  kilit {p.UnlockCost}" : "")}");
            }
            if (!apply) return;
            foreach (var p in list)
            {
                var s = G.Shapes.First(x => x.Id == p.Id);
                s.BaseTile = p.NewBase; s.UnlockCost = p.UnlockCost; s.Gain = p.Gain; s.GainIncrement = 0; s.GainMultiplier = 1f;
                s.Cost = p.Cost; s.CostIncrement = 0; s.CostMultiplier = 1f; s.CostMax = 0;
            }
        }
    }
}
