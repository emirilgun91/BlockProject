using System;
using UnityEngine;

namespace RogueBlockBlast.Content
{
    /// <summary>
    /// Şekil kartlarının etki tipi.
    /// ScoreBoost  : Şeklin her tile'ının base değerine flat ekleme yapar (run içi geçici).
    /// WeightChange: Şeklin spawn ağırlığını sayısal olarak değiştirir (+/-).
    /// </summary>
    public enum ShapeEffectType
    {
        ScoreBoost,   // tile value += Value (flat, additive)
        WeightChange, // effectiveWeight += Value (pozitif = daha sık, negatif = daha seyrek)
    }

    /// <summary>
    /// Bir şekil kartının tek bir etkisi.
    /// CardSO içinde List olarak tutulur — bir kart birden fazla effect taşıyabilir.
    /// </summary>
    [Serializable]
    public sealed class ShapeEffectEntry
    {
        [Tooltip("Etki tipi: puan artışı mı, ağırlık değişimi mi?")]
        public ShapeEffectType EffectType;

        [Tooltip("ScoreBoost: kaç puan eklenir (örn. 5). WeightChange: ağırlık değişimi (örn. 10 veya -10).")]
        public float Value;
    }
}