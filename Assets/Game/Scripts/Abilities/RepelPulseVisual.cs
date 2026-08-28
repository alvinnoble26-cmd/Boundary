using UnityEngine;

/// <summary>
/// Short-lived, client-local shockwave animation for the Repel projectile.
/// It intentionally contains no gameplay or networking behavior.
/// </summary>
public sealed class RepelPulseVisual : MonoBehaviour
{
    private static readonly Color ShockRed = new Color(1f, 0.06f, 0.05f, 0.96f);
    private static readonly Color ShockWhite = new Color(1f, 0.92f, 0.7f, 1f);

    private LineRenderer[] rings;
    private LineRenderer[] spokes;
    private float startedAt;
    private float lifetime;

    public void Configure(Material material, float duration)
    {
        lifetime = Mathf.Max(0.05f, duration);
        startedAt = Time.time;
        rings = new[]
        {
            CreateRing("Repel Expanding Inner Ring", material, 1.05f, 0.13f, ShockWhite),
            CreateRing("Repel Expanding Outer Ring", material, 2.1f, 0.075f, ShockRed)
        };
        spokes = new LineRenderer[12];
        for (int index = 0; index < spokes.Length; index++)
            spokes[index] = CreateSpoke(material, index);
    }

    private void Update()
    {
        if (rings == null || spokes == null)
            return;

        float progress = Mathf.Clamp01((Time.time - startedAt) / lifetime);
        float scale = Mathf.Lerp(0.2f, 2.2f, progress);
        transform.localScale = new Vector3(scale, 1f, scale);
        transform.Rotate(0f, 420f * Time.deltaTime, 0f, Space.World);
        float alpha = 1f - progress;

        for (int index = 0; index < rings.Length; index++)
        {
            Color color = index == 0 ? ShockWhite : ShockRed;
            rings[index].startColor = new Color(color.r, color.g, color.b, alpha * color.a);
            rings[index].endColor = new Color(color.r, color.g, color.b, alpha * color.a);
        }
        for (int index = 0; index < spokes.Length; index++)
        {
            Color color = index % 2 == 0 ? ShockWhite : ShockRed;
            spokes[index].startColor = new Color(color.r, color.g, color.b, alpha * color.a);
            spokes[index].endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }

    private LineRenderer CreateRing(string effectName, Material material, float radius, float width,
        Color color)
    {
        GameObject ringObject = new GameObject(effectName, typeof(LineRenderer));
        ringObject.transform.SetParent(transform, false);
        LineRenderer ring = ringObject.GetComponent<LineRenderer>();
        const int points = 48;
        ring.useWorldSpace = false;
        ring.positionCount = points + 1;
        ring.widthMultiplier = width;
        ring.numCornerVertices = 2;
        ring.material = material;
        ring.startColor = color;
        ring.endColor = color;
        for (int index = 0; index <= points; index++)
        {
            float angle = index / (float)points * Mathf.PI * 2f;
            ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, 0.04f,
                Mathf.Sin(angle) * radius));
        }
        return ring;
    }

    private LineRenderer CreateSpoke(Material material, int index)
    {
        GameObject spokeObject = new GameObject("Repel Shockwave Spoke", typeof(LineRenderer));
        spokeObject.transform.SetParent(transform, false);
        LineRenderer spoke = spokeObject.GetComponent<LineRenderer>();
        float angle = index / 12f * Mathf.PI * 2f;
        Vector3 radial = new Vector3(Mathf.Cos(angle), 0.02f, Mathf.Sin(angle));
        spoke.useWorldSpace = false;
        spoke.positionCount = 2;
        spoke.widthMultiplier = 0.045f;
        spoke.material = material;
        spoke.startColor = index % 2 == 0 ? ShockWhite : ShockRed;
        spoke.endColor = new Color(ShockRed.r, ShockRed.g, ShockRed.b, 0f);
        spoke.SetPosition(0, radial * 0.35f);
        spoke.SetPosition(1, radial * 1.55f);
        return spoke;
    }
}
