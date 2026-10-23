// Oyun verisini doğrudan Unity asset'lerinden okur (Assets/GameData). Asset'lerde
// bir değer değişince simülasyon yeniden derlenmeden onu kullanır.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BalanceSim
{
    public enum E
    {
        LineScoreBonus = 0, ScoreMultiplierBonus, ComboDecayImmunity, ComboBonusPerClear, ExtraDeadPoolReroll,
        PoolLimitBonus, CoinBonusOnMilestone, DietPlanMaxSize, DietPlanScoreFactor, GhostDropMaxUses,
        SoftLandingFactor, SlowBurnEarlyCount, SlowBurnEarlyFactor, SlowBurnLateCount, SlowBurnLateFactor,
        HyperfocusComboMultiplier, HyperfocusPenaltyShapes, TunnelVisionMultiplier, LineMasterMultiplier,
        BountyHunterCoinPerClear, BountyHunterThresholdScale, FutureInvestmentCurrentScale,
        FutureInvestmentNextDiscount, FirstPicksFreeCount, MomentumShieldMinMultiplier, HoarderMinRemaining,
        HoarderPoolBonus, PerfectClearCoinReward, PerfectClearComboBoost, SelectiveBlindnessSingleFactor,
        SelectiveBlindnessRemoveCount, NeonCableExplosionScore, SafeZoneComboFloor, SafeZonePenalty,
        RiftSpawnInterval, RiftCountdownStart, RiftBonusShapes, PhantomCellEnabled, CornerStoneBonus,
        CenterBaseBonus, DoubleStrikeBonus, GamblerRoll, PatientBonus, ComboShield, ChainMaster, CardCollector,
        HeavyLoad
    }

    public sealed class ShapeDef
    {
        public int Idx;
        public string Id;
        public int[] CX, CY;
        public int BaseWeight, BaseTile, UnlockCost, WInc, WDec;
        public bool Locked;
        public int[] Gain = Array.Empty<int>();   // ScoreGain.Steps
        public int[] Cost = Array.Empty<int>();   // ScoreCost.Steps
        public int GainIncrement, CostIncrement, CostMax;
        public float GainMultiplier = 1f, CostMultiplier = 1f;
        public int Size => CX.Length;

        public ulong[][] Masks = new ulong[4][];  // rotasyon başına tüm geçerli konumlar
        public ulong[] Distinct;                 // 4 rotasyonun tekilleştirilmiş birleşimi

        // UpgradeCurve.GetValue
        static int CurveValue(int[] steps, float mult, int inc, int max, int level)
        {
            if (level < 1) return 0;
            int v;
            if (steps.Length > 0)
            {
                if (level <= steps.Length) v = steps[level - 1];
                else
                {
                    v = steps[^1];
                    for (int i = steps.Length; i < level; i++) v = (int)Math.Round(v * (mult > 0 ? mult : 1f)) + inc;
                }
            }
            else
            {
                v = inc > 0 ? inc : 0;
                for (int i = 1; i < level; i++) v = (int)Math.Round(v * (mult > 0 ? mult : 1f)) + inc;
            }
            if (max > 0) v = Math.Min(v, max);
            return v;
        }

        public float TileAt(int level)
        {
            int sum = 0;
            for (int i = 1; i <= level; i++) sum += CurveValue(Gain, GainMultiplier, GainIncrement, 0, i);
            return BaseTile + sum;
        }

        /// <summary>level → level+1 maliyeti (ShapeUpgradeRegistry.GetScoreUpgradeCost).</summary>
        public int ScoreCostFor(int level) => CurveValue(Cost, CostMultiplier, CostIncrement, CostMax, level + 1);

        public void BuildMasks()
        {
            var all = new HashSet<ulong>();
            for (int r = 0; r < 4; r++)
            {
                var list = new List<ulong>();
                int n = Size;
                var rx = new int[n]; var ry = new int[n];
                for (int i = 0; i < n; i++)
                {
                    int x = CX[i], y = CY[i];
                    // PieceDefinition.RotateCell
                    switch (r)
                    {
                        case 1: rx[i] = y; ry[i] = -x; break;
                        case 2: rx[i] = -x; ry[i] = -y; break;
                        case 3: rx[i] = -y; ry[i] = x; break;
                        default: rx[i] = x; ry[i] = y; break;
                    }
                }
                for (int ay = -8; ay < 16; ay++)
                for (int ax = -8; ax < 16; ax++)
                {
                    ulong m = 0; bool ok = true;
                    for (int i = 0; i < n && ok; i++)
                    {
                        int x = ax + rx[i], y = ay + ry[i];
                        if (x < 0 || y < 0 || x >= 8 || y >= 8) ok = false;
                        else m |= 1UL << (y * 8 + x);
                    }
                    if (ok) { list.Add(m); all.Add(m); }
                }
                Masks[r] = list.ToArray();
            }
            Distinct = all.ToArray();
        }
    }

    public sealed class CardDef
    {
        public int Idx;
        public string Id;
        public int Rarity, BaseWeight;
        public bool Unique, Locked, IsShape;
        public int UnlockAtStage;
        public string RequiredShapeId = "", TargetShapeId = "";
        public List<(E type, float value)> Effects = new();
        public float ShapeScore, ShapeWeight;
        public bool Has(E e) => Effects.Any(f => f.type == e);
    }

    public sealed class UpgradeDef
    {
        public int Idx;
        public string Id;
        public int UnlockStage, MaxLevel;
        public int[] Costs;
        public float PerLevel;
        public int CostFor(int level) => Costs.Length == 0 ? 0 : Costs[Math.Clamp(level - 1, 0, Costs.Length - 1)];
    }

    public sealed class MilestoneDef { public int Threshold, Coin; public string Label; }

    public sealed class GameData
    {
        public List<ShapeDef> Shapes = new();
        public List<CardDef> Cards = new();
        public List<UpgradeDef> Upgrades = new();
        public List<MilestoneDef> Milestones = new();
        public int PoolLimit;
        public Dictionary<string, int> ShapeIndex = new();
        public Dictionary<string, int> UpgradeIndex = new();

        public int U(string id) => UpgradeIndex[id];

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static int[] Hex(string s)
        {
            s = s.Trim();
            var r = new int[s.Length / 8];
            for (int i = 0; i < r.Length; i++)
            {
                uint v = 0;
                for (int b = 0; b < 4; b++)
                    v |= (uint)Convert.ToByte(s.Substring(i * 8 + b * 2, 2), 16) << (8 * b);
                r[i] = (int)v;
            }
            return r;
        }

        static List<string> Body(string path)
        {
            var lines = File.ReadAllLines(path).ToList();
            int i = lines.FindIndex(l => l.StartsWith("MonoBehaviour:"));
            return lines.Skip(i + 1).ToList();
        }

        static string Val(List<string> body, string key)
        {
            var l = body.FirstOrDefault(x => x.StartsWith("  " + key + ":"));
            return l == null ? "" : l.Substring(l.IndexOf(':') + 1).Trim();
        }

        /// <summary>"  Block:" altındaki "    Key: v" değeri.</summary>
        static string SubVal(List<string> body, string block, string key)
        {
            int i = body.FindIndex(x => x.TrimEnd() == "  " + block + ":");
            if (i < 0) return "";
            for (int j = i + 1; j < body.Count && body[j].StartsWith("    "); j++)
            {
                var t = body[j].Trim();
                if (t.StartsWith(key + ":")) return t.Substring(t.IndexOf(':') + 1).Trim();
            }
            return "";
        }

        static int I(string s) => string.IsNullOrEmpty(s) ? 0 : int.Parse(s, Inv);
        static float F(string s) => string.IsNullOrEmpty(s) ? 0f : float.Parse(s, Inv);

        public static GameData Load(string assets)
        {
            var g = new GameData();
            string gd = Path.Combine(assets, "GameData");

            // Milestones
            var mb = Body(Path.Combine(gd, "Progression", "MilestoneConfig.asset"));
            g.PoolLimit = I(Val(mb, "PoolLimit"));
            for (int i = 0; i < mb.Count; i++)
            {
                var t = mb[i].Trim();
                if (t.StartsWith("- ScoreThreshold:"))
                {
                    g.Milestones.Add(new MilestoneDef
                    {
                        Threshold = I(t.Split(':')[1]),
                        Coin = I(mb[i + 1].Split(':')[1]),
                        Label = mb[i + 2].Substring(mb[i + 2].IndexOf(':') + 1).Trim()
                    });
                }
            }

            // Shapes — kütüphanedeki sırayla (spawn sırası ağırlıklı seçimde önemli)
            var libGuids = Regex.Matches(File.ReadAllText(Path.Combine(gd, "Shapes", "ShapeLibrary.asset")), @"guid: ([0-9a-f]+)")
                                .Select(m => m.Groups[1].Value).ToList();
            var metaByGuid = Directory.GetFiles(Path.Combine(gd, "Shapes"), "*.asset.meta")
                .ToDictionary(f => Regex.Match(File.ReadAllText(f), @"guid: ([0-9a-f]+)").Groups[1].Value,
                              f => f.Substring(0, f.Length - 5));
            foreach (var guid in libGuids)
            {
                if (!metaByGuid.TryGetValue(guid, out var path)) continue;
                var b = Body(path);
                var s = new ShapeDef { Idx = g.Shapes.Count, Id = Val(b, "Id") };
                var cells = new List<(int, int)>();
                int ci = b.FindIndex(x => x.TrimEnd() == "  Cells:");
                for (int j = ci + 1; j < b.Count && b[j].StartsWith("  - {x:"); j++)
                {
                    var m = Regex.Match(b[j], @"x: (-?\d+), y: (-?\d+)");
                    cells.Add((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)));
                }
                s.CX = cells.Select(c => c.Item1).ToArray();
                s.CY = cells.Select(c => c.Item2).ToArray();
                s.BaseWeight = I(Val(b, "BaseWeight"));
                s.BaseTile = I(Val(b, "BaseTileValue"));
                s.Locked = Val(b, "LockedByDefault") == "1";
                s.UnlockCost = I(Val(b, "UnlockCost"));
                s.WInc = I(Val(b, "WeightIncreaseCost"));
                s.WDec = I(Val(b, "WeightDecreaseCost"));
                var gs = SubVal(b, "ScoreGain", "Steps");
                s.Gain = string.IsNullOrEmpty(gs) ? Array.Empty<int>() : Hex(gs);
                s.GainMultiplier = F(SubVal(b, "ScoreGain", "Multiplier"));
                s.GainIncrement = I(SubVal(b, "ScoreGain", "Increment"));
                var cs = SubVal(b, "ScoreCost", "Steps");
                s.Cost = string.IsNullOrEmpty(cs) ? Array.Empty<int>() : Hex(cs);
                s.CostMultiplier = F(SubVal(b, "ScoreCost", "Multiplier"));
                s.CostIncrement = I(SubVal(b, "ScoreCost", "Increment"));
                s.CostMax = I(SubVal(b, "ScoreCost", "MaxValue"));
                s.BuildMasks();
                g.ShapeIndex[s.Id] = s.Idx;
                g.Shapes.Add(s);
            }

            // Cards (sahnedeki CardPool tüm Cards klasörünü içeriyor)
            foreach (var f in Directory.GetFiles(Path.Combine(gd, "Cards"), "*.asset").OrderBy(x => x))
            {
                var b = Body(f);
                var c = new CardDef { Idx = g.Cards.Count, Id = Val(b, "Id") };
                c.Rarity = I(Val(b, "Rarity"));
                c.BaseWeight = I(Val(b, "BaseWeight"));
                c.Unique = Val(b, "IsUnique") == "1";
                c.Locked = Val(b, "LockedByDefault") == "1";
                c.UnlockAtStage = I(Val(b, "UnlockAtStage"));
                c.IsShape = Val(b, "IsShapeCard") == "1";
                c.RequiredShapeId = Val(b, "RequiredShapeId");
                c.TargetShapeId = Val(b, "TargetShapeId");
                string section = "";
                for (int j = 0; j < b.Count; j++)
                {
                    var l = b[j];
                    if (Regex.IsMatch(l, @"^  [A-Za-z_]+:")) section = l.Trim().TrimEnd(':').Split(':')[0];
                    var t = l.Trim();
                    if (section == "Effects" && t.StartsWith("- Type:"))
                        c.Effects.Add(((E)I(t.Split(':')[1]), F(b[j + 1].Split(':')[1])));
                    if (section == "ShapeEffects" && t.StartsWith("- EffectType:"))
                    {
                        int et = I(t.Split(':')[1]);
                        float v = F(b[j + 1].Split(':')[1]);
                        if (et == 0) c.ShapeScore += v; else c.ShapeWeight += v;
                    }
                }
                g.Cards.Add(c);
            }

            // Upgrades
            foreach (var f in Directory.GetFiles(Path.Combine(gd, "Upgrades"), "Upgrade_*.asset").OrderBy(x => x))
            {
                var b = Body(f);
                var u = new UpgradeDef
                {
                    Idx = g.Upgrades.Count,
                    Id = Val(b, "Id"),
                    UnlockStage = I(Val(b, "UnlockStageIndex")),
                    MaxLevel = I(Val(b, "MaxLevel")),
                    Costs = Hex(Val(b, "CostPerLevel")),
                    PerLevel = F(Val(b, "EffectValuePerLevel"))
                };
                g.UpgradeIndex[u.Id] = u.Idx;
                g.Upgrades.Add(u);
            }
            return g;
        }
    }
}
