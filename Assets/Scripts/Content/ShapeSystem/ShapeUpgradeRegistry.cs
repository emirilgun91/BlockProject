using System.Collections.Generic;
using UnityEngine;

namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Her shape'in upgrade seviyesini tutar.
    /// Singleton — sahnede bir objeye ekle veya RunController'dan erişilir.
    ///
    /// PlayerPrefs ile kaydedilir → run'lar arası kalıcı.
    /// </summary>
    public sealed class ShapeUpgradeRegistry
    {
        // ── Singleton ────────────────────────────────────────────────────────
        public static ShapeUpgradeRegistry Instance { get; } = new ShapeUpgradeRegistry();
        private ShapeUpgradeRegistry() { }

        // ── Config ───────────────────────────────────────────────────────────
        /// <summary>Her upgrade seviyesi başına tile değerine eklenen puan.</summary>
        public float ValuePerUpgradeLevel = 5f;

        /// <summary>Maksimum upgrade seviyesi.</summary>
        public int MaxUpgradeLevel = 10;

        // ── State ────────────────────────────────────────────────────────────
        private readonly Dictionary<string, int> _levels = new();

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Bir shape'in upgrade seviyesini döndürür.</summary>
        public int GetLevel(string shapeId)
        {
            return _levels.TryGetValue(shapeId, out int lvl) ? lvl : 0;
        }

        /// <summary>
        /// Bir shape'in tile değerini hesaplar.
        /// baseTileValue + (upgradeLevel * ValuePerUpgradeLevel)
        /// </summary>
        public float GetTileValue(string shapeId, float baseTileValue)
        {
            return baseTileValue + GetLevel(shapeId) * ValuePerUpgradeLevel;
        }

        /// <summary>Upgrade yapar. Coin kontrolü dışarıda yapılır.</summary>
        public bool Upgrade(string shapeId)
        {
            int current = GetLevel(shapeId);
            if (current >= MaxUpgradeLevel) return false;

            _levels[shapeId] = current + 1;
            Save();
            return true;
        }

        /// <summary>Upgrade maliyetini döndürür. İleride tablo ile değiştirilebilir.</summary>
        public int GetUpgradeCost(string shapeId)
        {
            int level = GetLevel(shapeId);
            return 10 + level * 5;   // 10, 15, 20, 25 ...
        }

        // ── Persistence ──────────────────────────────────────────────────────
        private const string SavePrefix = "ShapeUpgrade_";

        public void Save()
        {
            foreach (var kv in _levels)
                PlayerPrefs.SetInt(SavePrefix + kv.Key, kv.Value);
            PlayerPrefs.Save();
        }

        public void Load(IEnumerable<string> shapeIds)
        {
            _levels.Clear();
            foreach (var id in shapeIds)
            {
                int lvl = PlayerPrefs.GetInt(SavePrefix + id, 0);
                if (lvl > 0) _levels[id] = lvl;
            }
        }

        /// <summary>Tüm upgrade'leri sıfırlar (debug / reset için).</summary>
        public void ResetAll(IEnumerable<string> shapeIds)
        {
            foreach (var id in shapeIds)
                PlayerPrefs.DeleteKey(SavePrefix + id);
            _levels.Clear();
        }
    }
}