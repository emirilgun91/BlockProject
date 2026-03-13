using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ParticleSystem))]
public sealed class MouseTrailVelocity : MonoBehaviour
{
    [Header("Size")]
    public float sizeMultiplier = 0.01f;
    public float maxSize = 0.25f;

    [Header("Emission")]
    public float lowEmission = 5f;
    public float highEmission = 30f;

    Camera _cam;
    ParticleSystem _ps;

    ParticleSystem.MainModule _main;
    ParticleSystem.EmissionModule _emission;

    float _smoothVelocity;

    void Awake()
    {
        _ps = GetComponent<ParticleSystem>();

        _main = _ps.main;
        _emission = _ps.emission;

        _cam = Camera.main;

        // başlangıçta particle üretmesin
        SetEmission(0);
    }

    void Update()
    {
        if (Mouse.current == null || _cam == null)
        {
            SetEmission(0);
            return;
        }

        bool pointerOnUI =
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();

        if (!Mouse.current.leftButton.isPressed || pointerOnUI)
        {
            SetEmission(0);
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 delta = Mouse.current.delta.ReadValue();

        float rawVelocity = delta.magnitude;

        _smoothVelocity = Mathf.Lerp(
            _smoothVelocity,
            rawVelocity,
            Time.deltaTime * 10f
        );

        Vector3 world = _cam.ScreenToWorldPoint(
            new Vector3(mousePos.x, mousePos.y, 5f)
        );

        transform.position = world;

        AdjustParticles(_smoothVelocity);
    }

    void AdjustParticles(float speed)
    {
        float size = Mathf.Clamp(
            speed * sizeMultiplier,
            0.05f,
            maxSize
        );

        _main.startSize = size;

        float intensity = Mathf.Clamp(
            speed * 0.02f,
            0.4f,
            1f
        );

        _main.startColor = new Color(
            1f,
            intensity,
            intensity,
            1f
        );

        SetEmission(speed < 1f ? lowEmission : highEmission);
    }

    void SetEmission(float rate)
    {
        _emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
        _emission.rateOverDistance = 0;
    }
}