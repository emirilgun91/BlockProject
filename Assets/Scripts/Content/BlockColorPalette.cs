using UnityEngine;

namespace RogueBlockBlast.Content
{
    /// <summary>
    /// The 6 dark-roguelike block colors from the design palette.
    /// Pick one per ShapeSO in the Inspector — no manual hex entry needed.
    /// </summary>
    public enum BlockColorPreset
    {
        Crimson,   // #C0392B  — aggressive, rare pieces
        Amber,     // #D68910  — warm, common pieces
        Teal,      // #148F77  — cool, line-clear synergy
        Indigo,    // #5B4FCF  — deep purple-blue
        Violet,    // #8E44AD  — magic / special
        Slate, // #2E6DA4  — neutral blue
        Pinky,
        Greeny,
    }

    /// <summary>
    /// Converts a BlockColorPreset to its Unity Color.
    /// Single source of truth — change hex here, every piece updates.
    /// </summary>
    public static class BlockColorPalette
    {
        // ── Dark roguelike palette ──────────────────────────────────────────
        private static readonly Color Crimson = HexToColor("f0744e");
        private static readonly Color Amber   = HexToColor("c73333");
        private static readonly Color Teal    = HexToColor("f0bb4c");
        private static readonly Color Indigo  = HexToColor("5b4ef0");
        private static readonly Color Violet  = HexToColor("8652d0");
        private static readonly Color Slate   = HexToColor("4fc0eb");
        private static readonly Color Pinky = HexToColor("f04edb");
        private static readonly Color Greeny = HexToColor("4bbe4f");
        // Ghost tint alphas
        public const float GhostValidAlpha   = 0.10f;
        public const float GhostInvalidAlpha = 0.10f;

        // Ghost base colors (same teal/crimson, just faded)
        public static readonly Color GhostValid   = new Color(20f/255f, 143f/255f, 119f/255f, GhostValidAlpha);
        public static readonly Color GhostInvalid = new Color(192f/255f, 57f/255f, 43f/255f, GhostInvalidAlpha);

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>Returns the solid Color for a preset.</summary>
        public static Color GetColor(BlockColorPreset preset) => preset switch
        {
            BlockColorPreset.Crimson => Crimson,
            BlockColorPreset.Amber   => Amber,
            BlockColorPreset.Teal    => Teal,
            BlockColorPreset.Indigo  => Indigo,
            BlockColorPreset.Violet  => Violet,
            BlockColorPreset.Slate   => Slate,
            BlockColorPreset.Pinky  => Pinky,
            BlockColorPreset.Greeny  => Greeny,
            _                        => Color.white,
        };

        /// <summary>
        /// Returns a tinted ghost color.
        /// valid=true  → Teal  at GhostValidAlpha
        /// valid=false → Crimson at GhostInvalidAlpha
        /// </summary>
        public static Color GetGhostColor(bool valid) =>
            valid ? GhostValid : GhostInvalid;

        // ── Helper ──────────────────────────────────────────────────────────
        private static Color HexToColor(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}