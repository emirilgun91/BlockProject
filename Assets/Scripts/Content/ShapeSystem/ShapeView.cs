using UnityEngine;
using System.Collections.Generic;

public class ShapeView : MonoBehaviour
{
    [SerializeField] private GameObject blockPrefab;

    private readonly List<GameObject> blocks = new();

    public void Build(Vector2Int[] cells, float cellSize)
    {
        Clear();
        if (cells == null || cells.Length == 0)
            return;

        // Önce şeklin merkezini hesapla
        Vector2 center = Vector2.zero;
        for (int i = 0; i < cells.Length; i++)
        {
            center += cells[i];
        }
        center /= cells.Length;

        // Ardından blokları merkeze göre yerleştir
        foreach (var c in cells)
        {
            var b = Instantiate(blockPrefab, transform);

            b.transform.localPosition = new Vector3(
                (c.x - center.x) * cellSize,
                (c.y - center.y) * cellSize,
                0);

            blocks.Add(b);
        }
    }

    private void Clear()
    {
        foreach (var b in blocks)
            Destroy(b);

        blocks.Clear();
    }
    
}