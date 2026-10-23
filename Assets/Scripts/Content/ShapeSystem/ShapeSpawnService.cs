using System.Collections.Generic;
using UnityEngine;
using RogueBlockBlast.Content;
using RogueBlockBlast.Game;

namespace RogueBlockBlast.Core
{
    public static class ShapeSpawnService
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Trailer çekimi için: doluysa sıradaki şekil buradan alınır (TrailerDirector
        /// havuzu önceden planlar). Oyunda hiç doldurulmaz.
        /// </summary>
        public static readonly Queue<ShapeSO> DebugForcedShapes = new Queue<ShapeSO>();
#endif

        /// <param name="filter">Opsiyonel filtre — null ise tüm unlock'lu şekiller dahil edilir.</param>
        public static PieceDefinition GetRandomWeighted(ShapeLibrarySO library, System.Func<ShapeSO, bool> filter = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugForcedShapes.Count > 0)
                return PieceFactory.Create(DebugForcedShapes.Dequeue());
#endif
            if (library == null || library.Shapes == null || library.Shapes.Count == 0)
                return null;

            var upgradeReg = ShapeUpgradeRegistry.Instance;
            var cardReg    = ShapeCardEffectRegistry.Instance;
            int totalWeight = 0;

            for (int i = 0; i < library.Shapes.Count; i++)
            {
                var shape = library.Shapes[i];
                if (!shape.IsUnlocked) continue;
                if (filter != null && !filter(shape)) continue;

                int w = upgradeReg.GetEffectiveWeight(shape.Id, shape.BaseWeight);

                if (cardReg != null && cardReg.HasAnyEffect(shape.Id))
                    w = Mathf.Max(1, w + Mathf.RoundToInt(cardReg.GetWeightDelta(shape.Id)));

                totalWeight += w;
            }

            if (totalWeight <= 0) return null;

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;

            for (int i = 0; i < library.Shapes.Count; i++)
            {
                var shape = library.Shapes[i];
                if (!shape.IsUnlocked) continue;
                if (filter != null && !filter(shape)) continue;

                int w = upgradeReg.GetEffectiveWeight(shape.Id, shape.BaseWeight);

                if (cardReg != null && cardReg.HasAnyEffect(shape.Id))
                    w = Mathf.Max(1, w + Mathf.RoundToInt(cardReg.GetWeightDelta(shape.Id)));

                cumulative += w;
                if (roll < cumulative)
                    return PieceFactory.Create(shape);
            }

            return null;
        }

        // ── Classic Mode ─────────────────────────────────────────────────────

        /// <summary>Tahtaya hiç sığmayan Classic parçası en fazla bu kadar kez yeniden çekilir.</summary>
        public const int ClassicFitRetries = 2;

        /// <summary>
        /// Classic Mode parçası. Oyuncunun ilerlemesine bakmaz: yalnızca başta açık
        /// şekiller, taban ağırlık ve taban tile değeri. Döndürme kapalı olduğu için
        /// parça rastgele bir yönde gelir ve o yön hücrelere işlenir (R0 olarak).
        ///
        /// Tahtaya hiç sığmayan parça <see cref="ClassicFitRetries"/> kez yeniden
        /// çekilir. Bu yardım olmadan run'lar çok kısa sürüyordu (BalanceSim
        /// <c>classic</c>: orta seviye oyuncu medyan ~3 dk). Sığmama hâlâ mümkün,
        /// yalnızca daha seyrek.
        /// </summary>
        public static PieceDefinition GetRandomClassic(ShapeLibrarySO library, BoardModel board)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugForcedShapes.Count > 0)
                return PieceFactory.Create(DebugForcedShapes.Dequeue());
#endif
            var piece = RandomClassicPiece(library);
            for (int i = 0; i < ClassicFitRetries && piece != null && board != null && !FitsAnywhere(board, piece); i++)
                piece = RandomClassicPiece(library);
            return piece;
        }

        private static PieceDefinition RandomClassicPiece(ShapeLibrarySO library)
        {
            if (library == null || library.Shapes == null) return null;

            int totalWeight = 0;
            foreach (var shape in library.Shapes)
                if (shape != null && !shape.LockedByDefault) totalWeight += shape.BaseWeight;
            if (totalWeight <= 0) return null;

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;
            foreach (var shape in library.Shapes)
            {
                if (shape == null || shape.LockedByDefault) continue;
                cumulative += shape.BaseWeight;
                if (roll >= cumulative) continue;

                var probe = new PieceDefinition(shape.Id, shape.Cells, shape.BlockColor, shape.BaseTileValue);
                var cells = new List<Vector2Int>(probe.GetCells((Rotation)Random.Range(0, 4)));
                return new PieceDefinition(shape.Id, cells, shape.BlockColor, shape.BaseTileValue);
            }
            return null;
        }

        private static bool FitsAnywhere(BoardModel board, PieceDefinition piece)
        {
            // Hücreler (0,0) çevresinde ±4'e kadar uzanabiliyor (LongL, döndürülmüş I4)
            for (int y = -4; y < board.Height + 4; y++)
            for (int x = -4; x < board.Width + 4; x++)
                if (PlacementSystem.CanPlace(board, piece, new Vector2Int(x, y), Rotation.R0))
                    return true;
            return false;
        }
    }
}