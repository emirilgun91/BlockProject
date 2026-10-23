// Oyuncu modelleri. Hepsi aynı tahta sezgiselini kullanır; fark arama derinliği,
// ağırlıklar, gürültü (insan hatası) ve kart/reroll kararlarındadır.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BalanceSim
{
    public sealed class BotProfile
    {
        public string Name;
        public int Depth = 1;          // 1 = tek parça açgözlü, 3 = havuzun tamamını planla
        public int Beam = 6;
        public float Noise;            // ilk 5 hamle arasından rastgele seçme olasılığı
        public bool UseFit = true;     // sonraki parçaların sığma olasılığını hesaba katar
        public int CardPolicy;         // 0 = rastgele, 1 = tablo
        public float CardNoise;        // tablo yerine rastgele seçme olasılığı
        public bool SmartReroll;
        public float WLine = 3f, WClear = 2f, WScore = 2f, WFilled = 0.15f, WTrans = 0.22f, WHoles = 1.5f,
                     WFit = 10f, WBig = 2.5f, WCombo = 1.2f, WBar = 1.5f;
        public float SecPerMove = 3.5f;
    }

    public static class BoardEval
    {
        public static readonly ulong[] Squares3;
        public static readonly ulong[] Bars4;
        static BoardEval()
        {
            var l = new List<ulong>();
            for (int y = 0; y <= 5; y++)
            for (int x = 0; x <= 5; x++)
            {
                ulong m = 0;
                for (int dy = 0; dy < 3; dy++) for (int dx = 0; dx < 3; dx++) m |= 1UL << ((y + dy) * 8 + x + dx);
                l.Add(m);
            }
            Squares3 = l.ToArray();
            l.Clear();
            for (int y = 0; y < 8; y++)
            for (int x = 0; x <= 4; x++)
            {
                ulong h = 0, v = 0;
                for (int d = 0; d < 4; d++) { h |= 1UL << (y * 8 + x + d); v |= 1UL << ((x + d) * 8 + y); }
                l.Add(h); l.Add(v);
            }
            Bars4 = l.ToArray();
        }

        const ulong Col0 = Run.Col0, Col7 = Run.Col0 << 7, Row0 = 0xFFUL, Row7 = 0xFFUL << 56;

        public static int Transitions(ulong occ)
        {
            ulong e = ~occ;
            int t = BitOperations.PopCount((occ ^ (occ >> 1)) & ~Col7);
            t += BitOperations.PopCount((occ ^ (occ >> 8)) & 0x00FFFFFFFFFFFFFFUL);
            t += BitOperations.PopCount(e & Col0) + BitOperations.PopCount(e & Col7);
            t += BitOperations.PopCount(e & Row0) + BitOperations.PopCount(e & Row7);
            return t;
        }

        public static int Holes(ulong occ)
        {
            ulong e = ~occ;
            return BitOperations.PopCount(e & ~Run.Neighbors(e));
        }

        public static bool AnyFits(ulong occ, ulong[] masks)
        {
            foreach (var m in masks) if ((m & occ) == 0) return true;
            return false;
        }

        public static float FitProbability(ulong occ, List<(ShapeDef s, float p)> dist, bool locked, bool classic = false)
        {
            float f = 0f;
            foreach (var (s, p) in dist)
            {
                if (classic)
                {
                    // Classic: sıradaki parça rastgele bir rotasyonla gelir
                    for (int r = 0; r < 4; r++) if (AnyFits(occ, s.Masks[r])) f += p * 0.25f;
                }
                else if (AnyFits(occ, locked ? s.Masks[0] : s.Distinct)) f += p;
            }
            return f;
        }
    }

    public sealed class Bot : IBot
    {
        readonly BotProfile P;
        readonly Rng R;
        readonly Dictionary<string, float> cardValue;
        readonly List<(ShapeDef s, float p)> dist = new();

        public Bot(BotProfile p, Rng r, Dictionary<string, float> cardValue)
        {
            P = p; R = r; this.cardValue = cardValue;
        }

        struct ComboSnap { public int Ch, Max; public float Mult, Bpc, Bam, Base; }

        static ComboSnap Snap(Combo c) => new() { Ch = c.Charges, Max = c.MaxCharge, Mult = c.Mult, Bpc = c.BonusPerClear, Bam = c.BonusAtMax, Base = c.Base };

        static void Step(ref ComboSnap c, int lines, Combo real)
        {
            if (lines > 0)
            {
                for (int i = 0; i < lines; i++)
                {
                    if (c.Ch < c.Max) c.Ch++;
                    c.Mult += c.Ch >= c.Max ? c.Bam : c.Bpc;
                }
                if (real.HasChain) c.Bpc += real.ChainBonus;
            }
            else if (c.Ch > 0)
            {
                c.Ch--;
                if (c.Ch == 0)
                {
                    float fl = MathF.Max(c.Base, real.Floor);
                    c.Mult = real.SoftActive ? MathF.Max(fl, c.Mult * real.SoftFactor) : fl;
                }
            }
        }

        sealed class Node
        {
            public ulong Cells;
            public float[] Vals;
            public ComboSnap Combo;
            public int Used;          // kullanılmış havuz indeksleri bit maskesi
            public float Acc;         // birikmiş hamle değeri
            public int FirstPiece = -1;
            public ulong FirstMask;
            public float Total;
            public ulong ParentCells, LastMask;
            public float LastEff;
        }

        float need, perPiece;

        float MoveValue(Run r, ref ComboSnap combo, ulong cellsBefore, ulong mask, float eff, float[] vals,
                        out ulong cellsAfter, bool writeVals)
        {
            ulong fixedOcc = r.Phantom | r.Dead;
            ulong cells = cellsBefore | mask;
            ulong occ = cells | fixedOcc;
            ulong rowsMask = 0, colsMask = 0; int lines = 0;
            for (int y = 0; y < 8; y++) if (((occ >> (8 * y)) & 0xFF) == 0xFF) { rowsMask |= 0xFFUL << (8 * y); lines++; }
            for (int x = 0; x < 8; x++) { ulong cm = Run.ColMask(x); if ((occ & cm) == cm) { colsMask |= cm; lines++; } }

            float sum = 0f;
            if (lines > 0)
            {
                ulong U = rowsMask | colsMask;
                var cs = r.CS;
                ulong scoring = cs.HasTunnelVision ? colsMask : cs.HasLineMaster ? rowsMask : U;
                for (ulong m = scoring; m != 0; m &= m - 1)
                {
                    int b = BitOperations.TrailingZeroCount(m);
                    sum += (mask & (1UL << b)) != 0 ? eff : vals[b];
                }
                if (cs.HasTunnelVision) sum *= cs.TunnelVisionMultiplier;
                else if (cs.HasLineMaster) sum *= cs.LineMasterMultiplier;
                if (cs.HasDietPlan) sum *= cs.DietPlanScoreFactor;
                if (cs.HasSelectiveBlindness) sum = lines >= 2 ? 0f : sum * cs.SelBlindFactor;
                ulong clr = U & ~r.Dead;
                cells &= ~clr;
                if (writeVals)
                {
                    for (ulong m = mask; m != 0; m &= m - 1) vals[BitOperations.TrailingZeroCount(m)] = eff;
                    for (ulong m = clr; m != 0; m &= m - 1) vals[BitOperations.TrailingZeroCount(m)] = 0f;
                }
            }
            else if (writeVals)
            {
                for (ulong m = mask; m != 0; m &= m - 1) vals[BitOperations.TrailingZeroCount(m)] = eff;
            }

            float before = combo.Mult;
            Step(ref combo, lines, r.Combo);
            float gained = sum * combo.Mult * r.Global;
            if (r.CS.HasDoubleStrike && lines >= 2) gained *= 1.5f;

            cellsAfter = cells;
            float v = 0f;
            if (lines > 0) v += P.WClear + P.WLine * lines;
            v += P.WScore * MathF.Min(gained, need) / perPiece;
            // combo: çarpanı canlı tutmak gelecekteki her temizliği büyütür
            v += P.WCombo * (combo.Mult - combo.Base) * (combo.Ch > 0 ? 1f : 0f) / MathF.Max(1f, combo.Base);
            if (combo.Mult < before - 0.001f) v -= P.WCombo * (before - combo.Mult);
            return v;
        }

        float BoardValue(Run r, ulong cells)
        {
            ulong occ = cells | r.Phantom | r.Dead;
            float v = 0f;
            v -= P.WFilled * BitOperations.PopCount(cells);
            v -= P.WTrans * BoardEval.Transitions(occ);
            v -= P.WHoles * BoardEval.Holes(occ);
            if (P.UseFit)
            {
                float f = BoardEval.FitProbability(occ, dist, r.CS.RotationLocked, r.C.Classic && !r.C.ClassicRotate);
                v += P.WFit * f * f;
            }
            if (BoardEval.AnyFits(occ, BoardEval.Squares3)) v += P.WBig;
            if (BoardEval.AnyFits(occ, BoardEval.Bars4)) v += P.WBar;
            return v;
        }

        public Move Choose(Run r)
        {
            r.SpawnDistribution(dist);
            need = MathF.Max(1f, r.MS.NextThreshold - r.Score);
            perPiece = MathF.Max(need / MathF.Max(1, r.MS.Remaining), 1f);
            if (r.C.Classic) { need = 1e9f; perPiece = 40f; }   // hedef eşik yok: puan tavansız sayılır

            int n = r.Pool.Count;
            int depth = Math.Min(P.Depth, n);
            var beam = new List<Node> { new Node { Cells = r.Cells, Vals = (float[])r.Val.Clone(), Combo = Snap(r.Combo) } };
            var all = new List<Node>();

            for (int d = 0; d < depth; d++)
            {
                var next = new List<Node>();
                foreach (var node in beam)
                {
                    var seenShape = new HashSet<int>();
                    ulong occ = node.Cells | r.Phantom | r.Dead;
                    for (int pi = 0; pi < n; pi++)
                    {
                        if ((node.Used & (1 << pi)) != 0) continue;
                        var piece = r.Pool[pi];
                        if (!seenShape.Add(piece.S.Idx * 4 + piece.Rot)) continue;
                        float eff = piece.Tile + r.ShapeScoreBonus[piece.S.Idx];
                        foreach (var mask in r.MasksFor(piece))
                        {
                            if ((mask & occ) != 0) continue;
                            var combo = node.Combo;
                            float mv = MoveValue(r, ref combo, node.Cells, mask, eff, node.Vals, out ulong after, false);
                            var child = new Node
                            {
                                Cells = after, Combo = combo, Used = node.Used | (1 << pi),
                                Acc = node.Acc + mv,
                                FirstPiece = node.FirstPiece < 0 ? pi : node.FirstPiece,
                                FirstMask = node.FirstPiece < 0 ? mask : node.FirstMask,
                                Vals = node.Vals,
                                ParentCells = node.Cells, LastMask = mask, LastEff = eff
                            };
                            child.Total = child.Acc + BoardValue(r, after);
                            next.Add(child);
                        }
                    }
                }
                if (next.Count == 0) break;
                next.Sort((a, b) => b.Total.CompareTo(a.Total));
                if (d == depth - 1) { all = next; break; }
                // derin seviyeler için en iyi B düğümün tile değerlerini somutlaştır:
                // son hamlenin hücreleri parçanın değerini alır, temizlenenler 0 olur
                int keep = Math.Min(P.Beam, next.Count);
                beam = new List<Node>(keep);
                for (int i = 0; i < keep; i++)
                {
                    var c = next[i];
                    var vals = (float[])c.Vals.Clone();
                    for (ulong m = c.LastMask; m != 0; m &= m - 1) vals[BitOperations.TrailingZeroCount(m)] = c.LastEff;
                    for (ulong m = (c.ParentCells | c.LastMask) & ~c.Cells; m != 0; m &= m - 1) vals[BitOperations.TrailingZeroCount(m)] = 0f;
                    c.Vals = vals;
                    beam.Add(c);
                }
                all = next;
            }

            if (all.Count == 0) return new Move { Piece = -1 };

            int pick = 0;
            if (P.Noise > 0f && R.Value() < P.Noise)
                pick = R.Range(0, Math.Min(5, all.Count));
            var best = all[pick];
            return new Move { Piece = best.FirstPiece, Mask = best.FirstMask };
        }

        public int PickCard(Run r, List<CardDef> offer, bool canReroll)
        {
            if (P.CardPolicy == 0 || cardValue == null || R.Value() < P.CardNoise)
                return R.Range(0, offer.Count);
            int best = 0; float bv = float.MinValue;
            for (int i = 0; i < offer.Count; i++)
            {
                cardValue.TryGetValue(offer[i].Id, out float v);
                if (Run.IsNoopDuplicate(r, offer[i])) v = -5f;
                if (v > bv) { bv = v; best = i; }
            }
            if (canReroll && bv < 0f) return -1;
            return best;
        }

        public bool WantPoolReroll(Run r)
        {
            int filled = BitOperations.PopCount(r.Occ);
            float need = r.MS.NextThreshold - r.Score;
            if (P.SmartReroll)
            {
                // EnsureMinimumRemaining(6): son parçalarda reroll havuz sayacını 6'ya çeker
                if (r.MS.Remaining <= 2 && need > 0 && r.Pool.Count == 3) return true;
                return filled >= 44;
            }
            return filled >= 48;
        }
    }
}
