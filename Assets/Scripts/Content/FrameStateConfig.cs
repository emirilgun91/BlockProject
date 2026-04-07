using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Her frame state'inin görsel parametreleri.
    /// Inspector'dan tweakle — kod değişmez.
    /// </summary>
    [CreateAssetMenu(menuName = "RogueBlockBlast/FrameStateConfig", fileName = "FrameStateConfig")]
    public sealed class FrameStateConfig : ScriptableObject
    {
        [Header("Idle")]
        public Color  IdleColor         = Color.white;
        public float  IdleFadeDuration  = 0.3f;

        [Header("Drop Pulse")]
        public float  DropIntensity     = 1.5f;
        public float  DropDuration      = 0.20f;

        [Header("Combo — Warm (x1.8+)")]
        public Color  ComboWarmColor    = new Color(1.0f, 0.80f, 0.10f);
        public float  ComboWarmIntensity = 0.5f;
        public float  ComboWarmDuration  = 0.4f;

        [Header("Combo — Glow (x3.0+)")]
        public Color  ComboGlowColor    = new Color(1.0f, 0.70f, 0.0f);
        public float  ComboGlowIntensity = 1.5f;
        public float  ComboGlowSpeed     = 1.5f;

        [Header("Combo — Plasma (x5.0+)")]
        public float  ComboPlasmaIntensity = 3.5f;
        public float  ComboPlasmaSpeed     = 3.0f;

        [Header("Combo Break")]
        public int    BreakFlickerCount  = 4;
        public float  BreakFlickerSpeed  = 0.04f;
        public float  BreakFadeDuration  = 0.25f;

        [Header("Critical (pool <= 5)")]
        public Color  CriticalColor         = Color.red;
        public float  CriticalMinIntensity  = 1.0f;
        public float  CriticalMaxIntensity  = 2.5f;
        public float  CriticalMinSpeed      = 1.0f;
        public float  CriticalMaxSpeed      = 4.0f;
        public int    CriticalThreshold     = 5;

        [Header("Milestone Shockwave")]
        public Color  MilestoneColor        = new Color(0.1f, 0.9f, 0.6f);
        public float  MilestoneDuration     = 0.6f;
        public float  MilestoneWidth        = 0.08f;

        [Header("Line Clear Rainbow")]
        public float  RainbowMinSpeed       = 0.5f;
        public float  RainbowMaxSpeed       = 4.0f;
        public float  RainbowGlowIntensity  = 2.0f;
        public float  RainbowHoldDuration   = 0.8f;
        public float  RainbowFadeDuration   = 0.3f;
        public int    RainbowMaxLines       = 4;

        [Header("Game Over")]
        public Color  GameOverColor         = new Color(0.85f, 0.1f, 0.1f);
        public float  GameOverBlastIntensity = 5.0f;
        public float  GameOverBlastDuration  = 0.15f;
        public float  GameOverFadeDuration   = 1.2f;

        [Header("Combo Thresholds")]
        public float ComboWarmThreshold   = 1.8f;
        public float ComboGlowThreshold   = 3.0f;
        public float ComboPlasmaThreshold = 5.0f;
    }
}