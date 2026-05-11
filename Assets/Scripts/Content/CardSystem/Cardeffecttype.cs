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

        // ── Diet Plan ────────────────────────────────────────────────────────
        DietPlanMaxSize,        // Cells.Count >= Value olan şekiller pool'a girmez
        DietPlanScoreFactor,    // Aktifken tüm tile değerleri bu faktörle çarpılır (örn. 0.8)

        // ── Ghost Drop ───────────────────────────────────────────────────────
        GhostDropMaxUses,       // Milestone başına max ücretsiz yerleştirme sayısı

        // ── Soft Landing ─────────────────────────────────────────────────────
        SoftLandingFactor,      // Reset'te mevcut combo bu faktörle çarpılır (örn. 0.5)

        // ── Slow Burn ─────────────────────────────────────────────────────────
        SlowBurnEarlyCount,     // İlk N şeklin tile değeri EarlyFactor ile çarpılır
        SlowBurnEarlyFactor,    // İlk N şeklin skor çarpanı (< 1)
        SlowBurnLateCount,      // PiecesRemaining <= N iken LateFactor uygulanır
        SlowBurnLateFactor,     // Az şekil kalınca skor çarpanı (> 1)

        // ── Hyperfocus ────────────────────────────────────────────────────────
        HyperfocusComboMultiplier,  // BonusPerClear ve BonusAtMaxCharge bu değerle çarpılır
        HyperfocusPenaltyShapes,    // Combo reset olunca pool'dan bu kadar şekil düşer

        // ── Tunnel Vision ─────────────────────────────────────────────────────
        TunnelVisionMultiplier,     // Sadece dikey (sütun) clear'lar skor verir, bu çarpanla

        // ── Line Master ───────────────────────────────────────────────────────
        LineMasterMultiplier,       // Sadece yatay (satır) clear'lar skor verir, bu çarpanla

        // ── Bounty Hunter ─────────────────────────────────────────────────────
        BountyHunterCoinPerClear,   // Her line clear'da kazanılan coin miktarı
        BountyHunterThresholdScale, // Tüm milestone eşiklerine uygulanacak kalıcı çarpan

        // ── Future Investment ─────────────────────────────────────────────────
        FutureInvestmentCurrentScale,  // Mevcut milestone eşiğine uygulanacak çarpan (örn. 1.2)
        FutureInvestmentNextDiscount,  // Başarı halinde sonraki milestone eşiğine indirim (örn. 0.15)

        // ── First Picks ───────────────────────────────────────────────────────
        FirstPicksFreeCount,    // Milestone başına pool tüketmeyen ücretsiz yerleştirme sayısı

        // ── Momentum Shield ───────────────────────────────────────────────────
        MomentumShieldMinMultiplier,  // Game Over'ı engellemek için gereken minimum combo çarpanı

        // ── Hoarder ───────────────────────────────────────────────────────────
        HoarderMinRemaining,    // Bonus için milestone bitiminde kalması gereken min şekil sayısı
        HoarderPoolBonus,       // Sonraki milestone için ek pool kapasitesi
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