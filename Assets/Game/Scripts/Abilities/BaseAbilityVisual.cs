using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Short-lived, client-local presentation for the Base ability's platform: a white-and-blue
/// snowflake magic circle on the ground, plus a faint icy disc over the standable platform.
/// It intentionally contains no gameplay, physics, or networking behavior; BaseAbility and
/// PlayerAbilities own those responsibilities. Call <see cref="Spawn"/> or <see cref="Configure"/>.
/// </summary>
public sealed class BaseAbilityVisual : MonoBehaviour
{
    private static readonly Color FrostWhite = new Color(0.93f, 0.98f, 1f, 1f);
    private static readonly Color IceBlue = new Color(0.42f, 0.78f, 1f, 1f);
    private static readonly Color DeepBlue = new Color(0.18f, 0.5f, 1f, 1f);

    private const int ArmCount = 6;
    private const float FadeInTime = 0.15f;
    private const float FadeOutTime = 0.25f;

    private Material lineMaterial;
    private Material discMaterial;

    private LineRenderer outerRing;
    private LineRenderer innerRing;
    private LineRenderer[] arms;
    private LineRenderer innerHex;
    private LineRenderer[] sparkles;
    private MeshRenderer discRenderer;

    private float radius = BaseAbility.PlatformRadius;
    private float duration = 1f;
    private float startTime;
    private float[] sparklePhase;
    private bool dissolveTriggered;
    private readonly List<LineRenderer> armBranches = new List<LineRenderer>();

    /// <summary>Convenience one-liner: spawns, configures and starts the effect at a world position.</summary>
    public static BaseAbilityVisual Spawn(Vector3 position,
        float platformRadius = BaseAbility.PlatformRadius,
        float lifetime = 1f, bool playCastSound = true)
    {
        GameObject go = new GameObject("Base Ability Snowflake Circle");
        go.transform.position = position;
        BaseAbilityVisual visual = go.AddComponent<BaseAbilityVisual>();
        visual.Configure(platformRadius, lifetime, playCastSound);
        return visual;
    }

    /// <summary>Builds the circle at the given radius and starts its cast -> hold -> dissolve lifecycle.</summary>
    public void Configure(float platformRadius, float lifetime, bool playCastSound = true)
    {
        radius = Mathf.Max(0.3f, platformRadius);
        duration = Mathf.Max(0.05f, lifetime);
        lineMaterial = CreateUnlitMaterial();
        discMaterial = CreateUnlitMaterial();

        BuildDisc();
        outerRing = CreateRing("Outer Ring", radius, 0.045f, IceBlue);
        innerRing = CreateRing("Inner Ring", radius * 0.78f, 0.03f, FrostWhite);
        innerHex = CreatePolygon("Inner Hex", 6, radius * 0.4f, 0.03f, DeepBlue);
        arms = new LineRenderer[ArmCount];
        sparkles = new LineRenderer[ArmCount];
        sparklePhase = new float[ArmCount];
        for (int i = 0; i < ArmCount; i++)
        {
            float angle = i * (360f / ArmCount);
            arms[i] = CreateSnowflakeArm(angle);
            sparkles[i] = CreateSparkle(angle);
            sparklePhase[i] = Random.Range(0f, Mathf.PI * 2f);
        }

        startTime = Time.time;
        if (playCastSound)
            SfxManager.PlayBaseCast(transform.position);
        StartCoroutine(Lifecycle());
    }

    private IEnumerator Lifecycle()
    {
        float dissolveAt = Mathf.Max(0f, duration - Mathf.Min(FadeOutTime, duration * 0.4f));
        while (true)
        {
            float elapsed = Time.time - startTime;
            if (elapsed >= duration)
                break;

            float envelope = Envelope(elapsed, dissolveAt);
            ApplyEnvelope(envelope, elapsed);

            if (!dissolveTriggered && elapsed >= dissolveAt)
            {
                dissolveTriggered = true;
                SfxManager.PlayBaseDissolve(transform.position);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private float Envelope(float elapsed, float dissolveAt)
    {
        float fadeIn = Mathf.Min(FadeInTime, duration * 0.25f);
        if (elapsed < fadeIn)
            return Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.001f, fadeIn));
        if (elapsed >= dissolveAt)
            return Mathf.SmoothStep(1f, 0f, (elapsed - dissolveAt) / FadeOutTime);
        return 1f;
    }

    private void ApplyEnvelope(float envelope, float elapsed)
    {
        float pulse = 0.9f + 0.1f * Mathf.Sin(elapsed * 5f);
        float scale = Mathf.Lerp(0.65f, 1f, envelope) * (dissolveTriggered ? 1f + (1f - envelope) * 0.18f : 1f);
        transform.localScale = Vector3.one * scale;

        SetRingAlpha(outerRing, IceBlue, envelope * pulse);
        SetRingAlpha(innerRing, FrostWhite, envelope * pulse);
        SetRingAlpha(innerHex, DeepBlue, envelope * 0.85f);
        for (int i = 0; i < ArmCount; i++)
            SetRingAlpha(arms[i], FrostWhite, envelope);
        foreach (LineRenderer branch in armBranches)
        {
            if (branch == null) continue;
            Color start = branch.startColor;
            start.a = envelope;
            branch.startColor = start;
            // End color is deliberately kept at alpha 0 (tapered tip) regardless of envelope.
        }

        outerRing.transform.localRotation = Quaternion.Euler(0f, elapsed * 16f, 0f);
        innerHex.transform.localRotation = Quaternion.Euler(0f, elapsed * -26f, 0f);

        for (int i = 0; i < ArmCount; i++)
        {
            float twinkle = 0.35f + 0.65f * Mathf.Clamp01(Mathf.Sin(elapsed * 6f + sparklePhase[i]));
            SetRingAlpha(sparkles[i], FrostWhite, envelope * twinkle);
        }

        if (discRenderer != null)
        {
            Color c = discMaterial.color;
            c = Color.Lerp(new Color(IceBlue.r, IceBlue.g, IceBlue.b, 0f), new Color(IceBlue.r, IceBlue.g, IceBlue.b, 0.22f), envelope);
            discMaterial.color = c;
        }
    }

    private static void SetRingAlpha(LineRenderer line, Color baseColor, float alpha)
    {
        if (line == null)
            return;
        Color c = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(alpha));
        line.startColor = c;
        line.endColor = c;
    }

    // ------------------------------------------------------------------ geometry

    private void BuildDisc()
    {
        GameObject discObject = new GameObject("Frost Disc", typeof(MeshFilter), typeof(MeshRenderer));
        discObject.transform.SetParent(transform, false);
        discObject.transform.localPosition = new Vector3(0f, 0.01f, 0f);

        const int segments = 40;
        Mesh mesh = new Mesh { name = "BaseAbilityFrostDisc" };
        Vector3[] vertices = new Vector3[segments + 1];
        // Both winding orders are included so the disc is visible from above and below
        // regardless of which way this project's convention treats front faces - it's a
        // flat cosmetic decal, so the tiny extra triangle cost doesn't matter.
        int[] triangles = new int[segments * 6];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }
        for (int i = 0; i < segments; i++)
        {
            int next = i + 1 < segments ? i + 2 : 1;
            triangles[i * 6] = 0;
            triangles[i * 6 + 1] = next;
            triangles[i * 6 + 2] = i + 1;
            triangles[i * 6 + 3] = 0;
            triangles[i * 6 + 4] = i + 1;
            triangles[i * 6 + 5] = next;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        discObject.GetComponent<MeshFilter>().mesh = mesh;
        discRenderer = discObject.GetComponent<MeshRenderer>();
        discRenderer.sharedMaterial = discMaterial;
        discMaterial.color = new Color(IceBlue.r, IceBlue.g, IceBlue.b, 0f);
        discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        discRenderer.receiveShadows = false;
    }

    private LineRenderer CreateRing(string ringName, float ringRadius, float width, Color color)
    {
        return CreatePolygon(ringName, 56, ringRadius, width, color);
    }

    private LineRenderer CreatePolygon(string objectName, int sides, float polyRadius, float width, Color color)
    {
        GameObject ringObject = new GameObject(objectName, typeof(LineRenderer));
        ringObject.transform.SetParent(transform, false);
        LineRenderer line = ringObject.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = sides;
        line.widthMultiplier = width;
        line.numCornerVertices = sides >= 24 ? 2 : 0;
        line.numCapVertices = 2;
        line.material = lineMaterial;
        line.startColor = color;
        line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        for (int i = 0; i < sides; i++)
        {
            float angle = i / (float)sides * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * polyRadius, 0.03f, Mathf.Sin(angle) * polyRadius));
        }
        return line;
    }

    /// <summary>One stylized snowflake arm: a spike from the center to near the outer ring with
    /// two pairs of shorter side branches, rotated to <paramref name="angle"/> degrees.</summary>
    private LineRenderer CreateSnowflakeArm(float angle)
    {
        GameObject armObject = new GameObject("Snowflake Arm", typeof(LineRenderer));
        armObject.transform.SetParent(transform, false);
        armObject.transform.localRotation = Quaternion.Euler(0f, angle, 0f);

        LineRenderer line = armObject.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.widthMultiplier = 0.035f;
        line.material = lineMaterial;
        line.startColor = FrostWhite;
        line.endColor = FrostWhite;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        float tip = radius * 0.92f;
        float branchNear = radius * 0.45f;
        float branchFar = radius * 0.68f;
        float branchLen = radius * 0.2f;
        float y = 0.035f;

        // A spike drawn as: center -> near-branch point -> tip -> back to near-branch point
        // (so branches can hang off the same continuous line) -> far-branch point -> tip again
        // would double back oddly, so instead each branch is its own short line off the spine.
        line.positionCount = 3;
        line.SetPosition(0, new Vector3(0f, y, 0f));
        line.SetPosition(1, new Vector3(0f, y, tip * 0.55f));
        line.SetPosition(2, new Vector3(0f, y, tip));

        CreateBranchPair(armObject.transform, branchNear, branchLen * 0.7f, y);
        CreateBranchPair(armObject.transform, branchFar, branchLen, y);
        return line;
    }

    private void CreateBranchPair(Transform parent, float alongSpine, float branchLength, float y)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject branchObject = new GameObject("Snowflake Branch", typeof(LineRenderer));
            branchObject.transform.SetParent(parent, false);
            LineRenderer branch = branchObject.GetComponent<LineRenderer>();
            branch.useWorldSpace = false;
            branch.positionCount = 2;
            branch.widthMultiplier = 0.022f;
            branch.material = lineMaterial;
            branch.startColor = FrostWhite;
            branch.endColor = new Color(FrostWhite.r, FrostWhite.g, FrostWhite.b, 0f);
            branch.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            branch.receiveShadows = false;
            Vector3 basePoint = new Vector3(0f, y, alongSpine);
            Vector3 tipPoint = basePoint + new Vector3(side * branchLength * 0.7f, 0f, branchLength * 0.55f);
            branch.SetPosition(0, basePoint);
            branch.SetPosition(1, tipPoint);
            armBranches.Add(branch);
        }
    }

    /// <summary>A tiny four-point sparkle sitting just past each arm's tip.</summary>
    private LineRenderer CreateSparkle(float angle)
    {
        GameObject sparkleObject = new GameObject("Arm Tip Sparkle", typeof(LineRenderer));
        sparkleObject.transform.SetParent(transform, false);
        Vector3 direction = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
        sparkleObject.transform.localPosition = direction * (radius * 0.98f) + new Vector3(0f, 0.04f, 0f);

        LineRenderer line = sparkleObject.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = 5;
        line.widthMultiplier = 0.02f;
        line.material = lineMaterial;
        line.startColor = FrostWhite;
        line.endColor = FrostWhite;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        const float s = 0.09f;
        // A plus-shaped four-point twinkle, drawn as one connected line through the center.
        line.SetPosition(0, new Vector3(-s, 0f, 0f));
        line.SetPosition(1, new Vector3(0f, 0f, 0f));
        line.SetPosition(2, new Vector3(0f, 0f, s));
        line.SetPosition(3, new Vector3(0f, 0f, 0f));
        line.SetPosition(4, new Vector3(s, 0f, 0f));
        return line;
    }

    private static Material CreateUnlitMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = shader != null ? new Material(shader) { name = "BaseAbilitySnowflakeFX" } : null;
        return material;
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
        if (discMaterial != null) Destroy(discMaterial);
    }
}
