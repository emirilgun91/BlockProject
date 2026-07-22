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

        // ── Perfect Clear ──────────────────────────────────────────────────────
        PerfectClearCoinReward,   // Tüm tahta boşaldığında kazanılan coin
        PerfectClearComboBoost,   // Tüm tahta boşaldığında combo çarpanına eklenen değer

        // ── Selective Blindness ───────────────────────────────────────────────
        SelectiveBlindnessSingleFactor,  // 1 satır/sütun temizlenince skor çarpanı (örn. 2.0)
        SelectiveBlindnessRemoveCount,   // 1 satır/sütun temizlenince rastgele kaldırılan blok sayısı

        // ── Neon Cable ────────────────────────────────────────────────────────
        NeonCableExplosionScore,  // Patlama alanındaki tile değerleri bu çarpanla puanlanır

        // ── Safe Zone ─────────────────────────────────────────────────────────
        SafeZoneComboFloor,   // Şekil üzerine yerleşince aktifleşen minimum combo çarpanı
        SafeZonePenalty,      // Tile temizlenince kaybedilen pool şekil sayısı

        // ── Decaying Rift ─────────────────────────────────────────────────────
        RiftSpawnInterval,    // Kaç yerleştirmede bir yeni geri sayım tile'ı çıkar
        RiftCountdownStart,   // Geri sayımın başlangıç değeri
        RiftBonusShapes,      // Erken temizlenince kazanılan pool şekil bonusu

        // ── Phantom Cell ──────────────────────────────────────────────────────
        PhantomCellEnabled,   // Sabit değer: 1 — aktifleştirme flag'i

        // ── Corner Stone / Center Base ────────────────────────────────────────
        CornerStoneBonus,     // Tahtanın 4 köşesine denk gelen her tile için +Value puan
        CenterBaseBonus,      // Tahtanın orta 2x2 alanına denk gelen her tile için +Value puan

        // ── Double Strike ─────────────────────────────────────────────────────
        DoubleStrikeBonus,    // 2+ satır/sütun aynı anda temizlenince skor ×1.5 (Value yoksayılır)

        // ── Gambler ───────────────────────────────────────────────────────────
        GamblerRoll,          // %20 ihtimalle skor ×2 veya ×0.5 (Value yoksayılır)

        // ── Patient ───────────────────────────────────────────────────────────
        PatientBonus,         // 3+ clear'sız yerleştirme sonrası gelen clear ×2 (Value yoksayılır)

        // ── Combo Shield ──────────────────────────────────────────────────────
        ComboShield,          // Run başına bir kez combo reset'ini engeller (Value yoksayılır)

        // ── Chain Master ──────────────────────────────────────────────────────
        ChainMaster,          // Ardışık her clear BonusPerClear'a +Value ekler

        // ── Card Collector ────────────────────────────────────────────────────
        CardCollector,        // Envanterdeki her kart global çarpana +Value ekler

        // ── Heavy Load ────────────────────────────────────────────────────────
        HeavyLoad,            // Pool limit +Value, karşılığında global çarpan -0.10
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