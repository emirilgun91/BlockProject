using System.Collections.Generic;
using UnityEngine;

namespace RogueBlockBlast.Content
{
    [CreateAssetMenu(menuName = "RogueBlockBlast/Shape Library", fileName = "ShapeLibrary")]
    public sealed class ShapeLibrarySO : ScriptableObject
    {
        public List<ShapeSO> Shapes = new();
    }
}