using System.Collections.Generic;
using RogueBlockBlast.Content;
using UnityEngine;

namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Shape başına upgrade state'i yönetir.
    /// Maliyetler ShapeSO'dan okunur — Inspector'dan ayarlanabilir.
    /// Weight min: 2, +5/-5 step.
    /// </summary>
    public sealed class ShapeUpgradeRegistry
    {
        public static ShapeUpgradeRegistry Instance { get; } = new ShapeUpgradeRegistry();
        private ShapeUpgradeRegistry() { }

        // ── Global Config ────────────────────────────────────────────────────
        public float ValuePerUpgradeLevel = 5f;
        public int   MaxScoreUpgradeLevel = 10;
        public int   MaxWeightLevel       = 10;   // base + 50 maksimum
        public int   WeightStep           = 5;    // +5 / -5
        public int   WeightMin            = 2;    // minimum weight değeri

        // ── State ────────────────────────────────────────────────────────────
        private readonly Dictionary<string, int> _scoreLevels  = new();
        private readonly Dictionary<string, int> _weightLevels = new();

        // ShapeSO cache — cost okumak için
        private readonly Dictionary<string, ShapeSO> _shapeCache = new();

        // ── Init ─────────────────────────────────────────────────────────────

        public void Load(IEnumerable<ShapeSO> shapes)
        {
            _scoreLevels.Clear();
            _weightLevels.Clear();
            _shapeCache.Clear();

            foreach (var s in shapes)
            {
                if (s == null) continue;
                _shapeCache[s.Id] = s;

                int sl = PlayerPrefs.GetInt(ScorePrefix  + s.Id, 0);
                int wl = PlayerPrefs.GetInt(WeightPrefix + s.Id, 0);
                if (sl != 0) _scoreLevels[s.Id]  = sl;
                if (wl != 0) _weightLevels[s.Id] = wl;
            }
        }

        // ── Score ────────────────────────────────────────────────────────────

        public int GetScoreLevel(string id) =>
            _scoreLevels.TryGetValue(id, out int l) ? l : 0;

        /// <summary>
        /// Upgrade dahil tile değeri.
        /// ScoreGain eğrisi doluysa seviyelerin kümülatif toplamı eklenir (3+5+7 = +15),
        /// boşsa eski sabit artış (level × ValuePerUpgradeLevel) kullanılır.
        /// </summary>
        public float GetTileValue(string id, float baseValue)
        {
            int level = GetScoreLevel(id);
            if (level <= 0) return baseValue;

            if (_shapeCache.TryGetValue(id, out var s) && !s.ScoreGain.IsEmpty)
                return baseValue + s.ScoreGain.GetCumulative(level);

            return baseValue + level * ValuePerUpgradeLevel;
        }

        /// <summary>Bir sonraki seviyede tile değerine eklenecek puan.</summary>
        public float GetNextScoreGain(string id)
        {
            int level = GetScoreLevel(id);
            if (_shapeCache.TryGetValue(id, out var s) && !s.ScoreGain.IsEmpty)
                return s.ScoreGain.GetValue(level + 1);
            return ValuePerUpgradeLevel;
        }

        public bool CanUpgradeScore(string id)
        {
            int level = GetScoreLevel(id);
            if (_shapeCache.TryGetValue(id, out var s) && !s.ScoreGain.IsEmpty)
                return s.ScoreGain.CanUpgrade(level) && level < MaxScoreUpgradeLevel;
            return level < MaxScoreUpgradeLevel;
        }

        /// <summary>Bir sonraki seviyenin coin maliyeti.</summary>
        public int GetScoreUpgradeCost(string id)
        {
            int level = GetScoreLevel(id);
            if (_shapeCache.TryGetValue(id, out var s))
            {
                if (!s.ScoreCost.IsEmpty) return s.ScoreCost.GetValue(level + 1);
                return s.ScoreUpgradeBaseCost + level * s.ScoreUpgradeCostPerLevel;
            }
            return 10 + level * 5;
        }

        /// <summary>Bu shape kaç kez yükseltilebilir — UI'da "3 / 5" göstermek için.</summary>
        public int GetMaxScoreLevel(string id)
        {
            if (_shapeCache.TryGetValue(id, out var s) && !s.ScoreGain.IsEmpty)
            {
                int curveMax = s.ScoreGain.EffectiveMaxLevel;
                return curveMax > 0 ? Mathf.Min(curveMax, MaxScoreUpgradeLevel) : MaxScoreUpgradeLevel;
            }
            return MaxScoreUpgradeLevel;
        }

        public bool UpgradeScore(string id)
        {
            if (!CanUpgradeScore(id)) return false;
            _scoreLevels[id] = GetScoreLevel(id) + 1;
            PlayerPrefs.SetInt(ScorePrefix + id, _scoreLevels[id]);
            return true;
        }

        // ── Weight ───────────────────────────────────────────────────────────

        public int GetWeightLevel(string id) =>
            _weightLevels.TryGetValue(id, out int l) ? l : 0;

        public int GetEffectiveWeight(string id, int baseWeight)
        {
            int level = GetWeightLevel(id);
            // Level pozitifse artış, negatifse azalış — step sabit 5
            return Mathf.Max(WeightMin, baseWeight + level * WeightStep);
        }

        public bool CanIncreaseWeight(string id) =>
            GetWeightLevel(id) < MaxWeightLevel;

        public bool CanDecreaseWeight(string id, int baseWeight)
        {
            // Mevcut weight min'in üzerinde kalacak mı?
            int currentLevel = GetWeightLevel(id);
            int nextWeight   = Mathf.Max(WeightMin,
                baseWeight + (currentLevel - 1) * WeightStep);
            return nextWeight >= WeightMin &&
                   GetEffectiveWeight(id, baseWeight) > WeightMin;
        }

        public int GetWeightIncreaseCost(string id)
        {
            if (_shapeCache.TryGetValue(id, out var s))
                return s.WeightIncreaseCost;
            return 60;
        }

        public int GetWeightDecreaseCost(string id)
        {
            if (_shapeCache.TryGetValue(id, out var s))
                return s.WeightDecreaseCost;
            return 40;
        }

        public bool IncreaseWeight(string id)
        {
            if (!CanIncreaseWeight(id)) return false;
            _weightLevels[id] = GetWeightLevel(id) + 1;
            PlayerPrefs.SetInt(WeightPrefix + id, _weightLevels[id]);
            return true;
        }

        public bool DecreaseWeight(string id, int baseWeight)
        {
            if (!CanDecreaseWeight(id, baseWeight)) return false;
            _weightLevels[id] = GetWeightLevel(id) - 1;
            PlayerPrefs.SetInt(WeightPrefix + id, _weightLevels[id]);
            return true;
        }

        // ── Reset ────────────────────────────────────────────────────────────

        public void ResetAll()
        {
            foreach (var id in _shapeCache.Keys)
            {
                PlayerPrefs.DeleteKey(ScorePrefix  + id);
                PlayerPrefs.DeleteKey(WeightPrefix + id);
            }
            _scoreLevels.Clear();
            _weightLevels.Clear();
        }

        // ── Keys ─────────────────────────────────────────────────────────────

        private const string ScorePrefix  = "ShapeScore_";
        private const string WeightPrefix = "ShapeWeight_";
    }
}