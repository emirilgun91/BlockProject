using System.Collections.Generic;
using UnityEngine;
using RogueBlockBlast.Content;
using RogueBlockBlast.Game;

namespace RogueBlockBlast.Core
{
    public static class ShapeSpawnService
    {
        public static PieceDefinition GetRandomWeighted(ShapeLibrarySO library)
        {
            if (library == null || library.Shapes == null || library.Shapes.Count == 0)
                return null;

            int totalWeight = 0;

            for (int i = 0; i < library.Shapes.Count; i++)
                totalWeight += library.Shapes[i].BaseWeight;

            int roll = Random.Range(0, totalWeight);

            int cumulative = 0;

            for (int i = 0; i < library.Shapes.Count; i++)
            {
                cumulative += library.Shapes[i].BaseWeight;

                if (roll < cumulative)
                    return PieceFactory.Create(library.Shapes[i]);
                if (totalWeight <= 0)
                    return null;
            }

            return null;
        }
    }
}