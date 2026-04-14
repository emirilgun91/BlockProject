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

        public float GetTileValue(string id, float baseValue) =>
            baseValue + GetScoreLevel(id) * ValuePerUpgradeLevel;

        public bool CanUpgradeScore(string id) =>
            GetScoreLevel(id) < MaxScoreUpgradeLevel;

        public int GetScoreUpgradeCost(string id)
        {
            int level = GetScoreLevel(id);
            if (_shapeCache.TryGetValue(id, out var s))
                return s.ScoreUpgradeBaseCost + level * s.ScoreUpgradeCostPerLevel;
            return 10 + level * 5;
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