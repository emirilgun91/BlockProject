using System.Collections.Generic;
using UnityEngine;
using RogueBlockBlast.Content;
using RogueBlockBlast.Game;

namespace RogueBlockBlast.Core
{
    public static class ShapeSpawnService
    {
        /// <param name="filter">Opsiyonel filtre — null ise tüm unlock'lu şekiller dahil edilir.</param>
        public static PieceDefinition GetRandomWeighted(ShapeLibrarySO library, System.Func<ShapeSO, bool> filter = null)
        {
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
    }
}