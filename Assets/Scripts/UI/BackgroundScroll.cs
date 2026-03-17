using UnityEngine;

public class BackgroundScroll : MonoBehaviour
{
    public float speedX = -0.01f;
    public float speedY = -0.01f;

    Material mat;
    Vector2 offset;

    void Start()
    {
        mat = GetComponent<SpriteRenderer>().material;
    }

    void Update()
    {
        offset.x += speedX * Time.deltaTime;
        offset.y += speedY * Time.deltaTime;

        mat.mainTextureOffset = offset;
    }
}