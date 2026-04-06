using System;
using UnityEngine;

namespace RogueBlockBlast.Content
{
    /// <summary>
    /// Kartın yapabileceği tüm efekt tipleri.
    /// Yeni kart eklemek için sadece buraya enum değeri ekle.
    /// </summary>
    public enum CardEffectType
    {
        // ── Score ────────────────────────────────────────────────────────────
        LineScoreBonus,         // Her line clear başına +Value puan
        ScoreMultiplierBonus,   // Tüm kazanılan skora *Value çarpanı

        // ── Combo ────────────────────────────────────────────────────────────
        ComboDecayImmunity,     // Clear olmadan combo düşmez (Value yoksayılır)
        ComboBonusPerClear,     // Her clear'da combo'ya +Value eklenir

        // ── Pool / Survival ──────────────────────────────────────────────────
        ExtraDeadPoolReroll,    // +Value adet ek reroll hakkı
        PoolLimitBonus,         // Pool limit +Value artar

        // ── Coin ─────────────────────────────────────────────────────────────
        CoinBonusOnMilestone,   // Milestone'da +Value coin ek kazanım

        // ── Board ────────────────────────────────────────────────────────────
        // BoardSizeChange,     // ileride: grid boyutu değişimi
    }

    /// <summary>
    /// Tek bir kart efekti — tip + değer.
    /// CardSO içinde liste halinde tutulur.
    /// </summary>
    [Serializable]
    public struct CardEffect
    {
        [Tooltip("Efektin tipi.")]
        public CardEffectType Type;

        [Tooltip("Efektin sayısal değeri. Anlam tipe göre değişir.")]
        public float Value;

        [Tooltip("UI'da gösterilecek kısa açıklama.")]
        public string Description;
    }
}