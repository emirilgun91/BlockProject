using UnityEngine;

namespace RogueBlockBlast.UI
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TileView : MonoBehaviour
    {
        private SpriteRenderer _sr;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        public void SetColor(Color c) => _sr.color = c;
    }
}