using UnityEngine;
using System.Collections.Generic;

public class ShapeView : MonoBehaviour
{
    [SerializeField] private GameObject blockPrefab;

    private readonly List<GameObject> blocks = new();

    public void Build(Vector2Int[] cells, float cellSize)
    {
        Clear();
        Vector2 center = Vector2.zero;
        foreach (var c in cells)
        {
            var b = Instantiate(blockPrefab, transform);

            b.transform.localPosition =
                new Vector3(c.x * cellSize, c.y * cellSize, 0);

            blocks.Add(b);
            center += c;

            center /= cells.Length;

            b.transform.localPosition =
                new Vector3((c.x - center.x) * cellSize,
                    (c.y - center.y) * cellSize,
                    0);
        }
    }

    private void Clear()
    {
        foreach (var b in blocks)
            Destroy(b);

        blocks.Clear();
    }
    
}