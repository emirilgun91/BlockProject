using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Attached to each cell GameObject that represents one block on the board or in a preview.
    /// Call SetPiece() after spawning — it applies the correct palette color automatically.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BlockCellView : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Glow")]
        [Tooltip("Optional second SpriteRenderer on a child for the bloom/glow layer.")]
        [SerializeField] private SpriteRenderer _glowRenderer;

        [Tooltip("Alpha of the glow layer (0 = off).")]
        [Range(0f, 1f)]
        [SerializeField] private float _glowAlpha = 0.3f;

        // ── Private ──────────────────────────────────────────────────────────
        private SpriteRenderer _sr;

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Sets this cell to display a placed piece color.</summary>
        public void SetPiece(PieceDefinition piece) => ApplyColor(piece.BlockColor);

        /// <summary>Sets this cell to display a placed piece color directly.</summary>
        public void SetColor(Color color) => ApplyColor(color);

        /// <summary>
        /// Sets this cell to ghost mode.
        /// valid=true  → teal tint (can place)
        /// valid=false → crimson tint (blocked)
        /// </summary>
        public void SetGhost(bool valid)
        {
            var ghostColor = BlockColorPalette.GetGhostColor(valid);
            _sr.color = ghostColor;

            if (_glowRenderer != null)
                _glowRenderer.enabled = false; // no glow on ghost
        }

        /// <summary>Hides the cell (pool slot empty or ghost cleared).</summary>
        public void Hide()
        {
            _sr.color = Color.clear;

            if (_glowRenderer != null)
                _glowRenderer.enabled = false;
        }

        // ── Private ──────────────────────────────────────────────────────────
        private void ApplyColor(Color baseColor)
        {
            _sr.color = baseColor;

            if (_glowRenderer != null)
            {
                _glowRenderer.enabled = true;
                var glowColor = baseColor;
                glowColor.a = _glowAlpha;
                _glowRenderer.color = glowColor;
            }
        }
    }
}