using System.Collections.Generic;
using RogueBlockBlast.Content;
using UnityEngine;

namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Run içinde aktif olan şekil kartı etkilerini tutar.
    /// DontDestroyOnLoad — RunController.NewRun() içinde Reset() çağrılmalı.
    ///
    /// Kullanım:
    ///   ShapeCardEffectRegistry.Instance.GetScoreBonus("shape_l")   → flat puan
    ///   ShapeCardEffectRegistry.Instance.GetWeightDelta("shape_l")  → ağırlık delta
    /// </summary>
    public sealed class ShapeCardEffectRegistry : MonoBehaviour
    {
        public static ShapeCardEffectRegistry Instance { get; private set; }

        // shapeId → bu şekle ait aktif etkiler listesi
        private readonly Dictionary<string, List<ShapeEffectEntry>> _effects = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Register / Reset ─────────────────────────────────────────────────

        /// <summary>
        /// Bir şekil kartı seçildiğinde çağrılır.
        /// Aynı kart birden fazla kez seçilirse etkiler birikerek eklenir.
        /// </summary>
        public void Register(CardSO card)
        {
            if (card == null || !card.IsShapeCard) return;
            if (string.IsNullOrEmpty(card.TargetShapeId)) return;
            if (card.ShapeEffects == null || card.ShapeEffects.Count == 0) return;

            string id = card.TargetShapeId;

            if (!_effects.ContainsKey(id))
                _effects[id] = new List<ShapeEffectEntry>();

            foreach (var entry in card.ShapeEffects)
            {
                // Yeni bir entry ekle — referans değil kopyasını ekle (SO değişirse etkilenmesin)
                _effects[id].Add(new ShapeEffectEntry
                {
                    EffectType = entry.EffectType,
                    Value      = entry.Value
                });
            }

            Debug.Log($"[ShapeCardEffectRegistry] Registered '{card.Id}' → shape:'{id}' " +
                      $"| ScoreBonus:{GetScoreBonus(id):F0} WeightDelta:{GetWeightDelta(id):F0}");
        }

        /// <summary>
        /// Yeni run başladığında tüm etkileri temizle.
        /// RunController.NewRun() içinden çağrılmalı.
        /// </summary>
        public void Reset()
        {
            _effects.Clear();
        }

        // ── Queries ───────────────────────────────────────────────────────────

        /// <summary>
        /// Belirli bir şeklin toplam flat score bonus'u.
        /// PlacementSystem veya ScoreSystem tarafından tile value'ya eklenir.
        /// </summary>
        public float GetScoreBonus(string shapeId)
        {
            if (string.IsNullOrEmpty(shapeId) || !_effects.TryGetValue(shapeId, out var list))
                return 0f;

            float total = 0f;
            foreach (var e in list)
                if (e.EffectType == ShapeEffectType.ScoreBoost)
                    total += e.Value;

            return total;
        }

        /// <summary>
        /// Belirli bir şeklin toplam ağırlık deltası.
        /// ShapeSpawnService tarafından base weight'e eklenir.
        /// </summary>
        public float GetWeightDelta(string shapeId)
        {
            if (string.IsNullOrEmpty(shapeId) || !_effects.TryGetValue(shapeId, out var list))
                return 0f;

            float total = 0f;
            foreach (var e in list)
                if (e.EffectType == ShapeEffectType.WeightChange)
                    total += e.Value;

            return total;
        }

        /// <summary>
        /// Bu şekle ait herhangi bir etki var mı? (erken çıkış optimizasyonu)
        /// </summary>
        public bool HasAnyEffect(string shapeId) =>
            !string.IsNullOrEmpty(shapeId) && _effects.ContainsKey(shapeId);
    }
}