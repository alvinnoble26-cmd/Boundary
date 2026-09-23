using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The on-screen half of ability hit feedback. Both sides of a hit are shown
/// in the ability's own accent colour so the victim and the attacker read the
/// same event at a glance:
/// <list type="bullet">
/// <item>the victim gets a coloured screen-edge vignette, a "HIT BY" tag and an
/// arrow pointing at whoever hit them;</item>
/// <item>the attacker gets a punch-out hit marker with the damage dealt.</item>
/// </list>
/// Built procedurally in the style of <see cref="BullseyeRingHitFeedback"/>, so
/// it needs no prefab, no scene wiring and no authored art.
/// </summary>
public sealed class AbilityHitFeedback : MonoBehaviour
{
    private const float TakenDuration = 0.52f;
    private const float DealtDuration = 0.40f;
    private const float AttackFraction = 0.16f;

    /// <summary>
    /// Ceiling on the whole overlay's opacity. Hit feedback has to be read at
    /// a glance while the arena is still being fought in, so it is deliberately
    /// a tint over the fight rather than a layer in front of it.
    /// </summary>
    private const float TakenPeakAlpha = 0.70f;
    private const float DealtPeakAlpha = 0.70f;

    // Sit just below BullseyeRingHitFeedback (31900) and BullseyeScreenFeedback
    // (32000) so Bullseye's authored payoff still reads on top of the generic
    // treatment when both fire for the same hit.
    private const int TakenSortingOrder = 31700;
    private const int DealtSortingOrder = 31750;

    private static AbilityHitFeedback activeTaken;
    private static AbilityHitFeedback activeDealt;
    private static Texture2D vignetteTexture;
    private static Texture2D softBarTexture;
    private static Texture2D tickTexture;
    private static Texture2D arrowTexture;

    private CanvasGroup group;
    private Text damageText;
    private Text titleText;
    private Image vignette;
    private Image arrowImage;
    private RectTransform arrow;
    private RectTransform[] ticks;
    private float startedAt;
    private float duration;
    private float accumulatedDamage;
    private bool isDealt;
    private bool isCritical;

    /// <summary>
    /// How strongly this hit is allowed to dress the screen, from the
    /// ability's <see cref="AbilityFeedbackProfile.Prominence"/>. Drives both
    /// the overlay's opacity and its physical size, so an ability that damages
    /// continuously - Void above all - stays a hint at the edge of vision
    /// instead of a panel parked over the arena.
    /// </summary>
    private float alphaScale = 1f;
    private float sizeScale = 1f;
    private float peakAlpha = 1f;

    // A Bullseye center hit - landed on the opponent's own revealed outline,
    // not just the outer ring - gets a gold accent and its own callout
    // instead of the ability's ordinary accent colour, on both screens.
    private static readonly Color CriticalColor = new Color(1f, 0.86f, 0.35f, 1f);
    private const string CriticalCallout = "BULLSEYE!";

    /// <summary>Shown to the player who was hit.</summary>
    public static void ShowTaken(AbilityFeedbackProfile profile, float damage, Vector3? sourcePosition,
        bool critical = false)
    {
        if (IsHeadless())
            return;

        if (activeTaken != null)
        {
            activeTaken.Refresh(profile, damage, sourcePosition, critical);
            return;
        }

        GameObject host = new GameObject("Ability Hit Taken");
        activeTaken = host.AddComponent<AbilityHitFeedback>();
        activeTaken.BuildTaken(profile, damage, sourcePosition, critical);
    }

    /// <summary>Shown to the player whose ability landed.</summary>
    public static void ShowDealt(AbilityFeedbackProfile profile, float damage, bool critical = false)
    {
        if (IsHeadless())
            return;

        if (activeDealt != null)
        {
            activeDealt.Refresh(profile, damage, null, critical);
            return;
        }

        GameObject host = new GameObject("Ability Hit Dealt");
        activeDealt = host.AddComponent<AbilityHitFeedback>();
        activeDealt.BuildDealt(profile, damage, critical);
    }

    private void ApplyProminence(AbilityFeedbackProfile profile)
    {
        float prominence = Mathf.Clamp01(profile.Prominence);
        alphaScale = Mathf.Lerp(0.42f, 1f, prominence);
        sizeScale = Mathf.Lerp(0.58f, 1f, prominence);
    }

    private void BuildTaken(AbilityFeedbackProfile profile, float damage, Vector3? sourcePosition,
        bool critical)
    {
        isDealt = false;
        isCritical = critical;
        duration = TakenDuration;
        ApplyProminence(profile);
        peakAlpha = TakenPeakAlpha * alphaScale;
        Canvas canvas = CreateCanvas(TakenSortingOrder);

        // The vignette carries the ability's own colour, at an opacity that
        // reads as a tint rather than a wash.
        Color tint = isCritical ? Color.Lerp(profile.Accent, Color.white, 0.3f) : profile.Accent;
        tint.a = isCritical ? 0.74f : 0.66f;
        // Pushing the gradient past the screen edge narrows the coloured frame
        // without needing a second texture: the less prominent the ability, the
        // more of its ramp is bled off-screen.
        float bleed = Mathf.Lerp(230f, 40f, Mathf.Clamp01(profile.Prominence));
        vignette = CreateStretchedImage(canvas.transform, "Vignette", GetVignetteTexture(), tint, bleed);

        // A dark plate keeps the ability name legible over a bright arena.
        Image plate = CreateImage(canvas.transform, "Tag Plate", GetSoftBarTexture(),
            new Color(0.02f, 0.02f, 0.05f, 0.42f));
        SetRect(plate.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -96f * sizeScale),
            new Vector2(324f, 54f) * sizeScale);

        Color titleColor = isCritical ? CriticalColor : profile.Accent;
        titleText = CreateText(plate.transform,
            isCritical ? CriticalCallout : "HIT BY " + profile.DisplayName,
            Scaled(isCritical ? 25 : 22), titleColor);
        SetRect(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f * sizeScale),
            new Vector2(316f, 26f) * sizeScale);

        damageText = CreateText(plate.transform, FormatDamage(damage), Scaled(18),
            new Color(1f, 0.93f, 0.95f, 0.85f));
        SetRect(damageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -12f * sizeScale),
            new Vector2(316f, 24f) * sizeScale);

        Color arrowColor = profile.Accent;
        arrowColor.a = 0.95f;
        // Void hits never show the triangle pointer toward the attacker.
        if (profile.DisplayName != "VOID")
        {
            arrowImage = CreateImage(canvas.transform, "Source Arrow", GetArrowTexture(), arrowColor);
            arrow = arrowImage.rectTransform;
            SetRect(arrow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f) * sizeScale);
            PointArrowAt(sourcePosition);
        }

        accumulatedDamage = damage;
        startedAt = Time.unscaledTime;
        StartCoroutine(Run());
    }

    private void BuildDealt(AbilityFeedbackProfile profile, float damage, bool critical)
    {
        isDealt = true;
        isCritical = critical;
        duration = DealtDuration;
        ApplyProminence(profile);
        peakAlpha = DealtPeakAlpha * alphaScale;
        Canvas canvas = CreateCanvas(DealtSortingOrder);

        Color accent = isCritical ? CriticalColor : profile.Accent;

        // Four ticks that punch outward from the centre - the classic read for
        // "that landed" without covering the arena.
        ticks = new RectTransform[4];
        for (int index = 0; index < ticks.Length; index++)
        {
            Image tick = CreateImage(canvas.transform, "Tick " + index, GetTickTexture(), accent);
            ticks[index] = tick.rectTransform;
            SetRect(ticks[index], new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(20f, 5f) * sizeScale);
            ticks[index].localRotation = Quaternion.Euler(0f, 0f, 45f + index * 90f);
        }

        titleText = CreateText(canvas.transform, isCritical ? CriticalCallout : profile.DisplayName,
            Scaled(isCritical ? 17 : 14), accent);
        SetRect(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -62f * sizeScale),
            new Vector2(240f, 20f) * sizeScale);

        damageText = CreateText(canvas.transform, FormatDamage(damage), Scaled(isCritical ? 26 : 22), accent);
        SetRect(damageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f * sizeScale),
            new Vector2(240f, 28f) * sizeScale);

        accumulatedDamage = damage;
        startedAt = Time.unscaledTime;
        StartCoroutine(Run());
    }

    /// <summary>
    /// Restarts the envelope instead of spawning a second overlay. Sustained
    /// damage - Hollow's beam, standing in a black hole - therefore reads as
    /// one continuous hit whose number climbs, not a stack of canvases.
    /// </summary>
    private void Refresh(AbilityFeedbackProfile profile, float damage, Vector3? sourcePosition, bool critical)
    {
        // Once a hit is confirmed critical it stays critical for this
        // accumulation window - the panel already opened before the real
        // damage (and therefore the center/ring distinction) was knowable.
        if (critical)
            isCritical = true;

        accumulatedDamage += damage;
        startedAt = Time.unscaledTime;

        // A refresh can come from a different ability than the one that opened
        // the panel, so every coloured part is re-tinted here - otherwise a
        // Bullseye landing during a Void would still flash Void's blue.
        ApplyProminence(profile);
        peakAlpha = (isDealt ? DealtPeakAlpha : TakenPeakAlpha) * alphaScale;

        Color accent = isCritical ? CriticalColor : profile.Accent;

        if (vignette != null)
        {
            Color tint = isCritical ? Color.Lerp(profile.Accent, Color.white, 0.3f) : profile.Accent;
            tint.a = isCritical ? 0.74f : 0.66f;
            vignette.color = tint;
        }

        if (arrowImage != null)
        {
            Color arrowColor = profile.Accent;
            arrowColor.a = 0.95f;
            arrowImage.color = arrowColor;
        }

        if (ticks != null)
        {
            for (int index = 0; index < ticks.Length; index++)
            {
                if (ticks[index] == null)
                    continue;
                Image tick = ticks[index].GetComponent<Image>();
                if (tick != null)
                    tick.color = accent;
            }
        }

        if (titleText != null)
        {
            titleText.text = isCritical
                ? CriticalCallout
                : (isDealt ? profile.DisplayName : "HIT BY " + profile.DisplayName);
            titleText.color = accent;
        }

        if (damageText != null)
        {
            damageText.text = FormatDamage(accumulatedDamage);
            if (isDealt)
                damageText.color = accent;
        }

        if (!isDealt)
        {
            if (profile.DisplayName == "VOID")
            {
                if (arrow != null)
                    arrow.gameObject.SetActive(false);
            }
            else
                PointArrowAt(sourcePosition);
        }
    }

    private IEnumerator Run()
    {
        while (true)
        {
            float elapsed = Time.unscaledTime - startedAt;
            if (elapsed >= duration)
                break;

            float progress = elapsed / duration;
            float envelope = progress < AttackFraction
                ? progress / AttackFraction
                : 1f - Mathf.SmoothStep(0f, 1f, (progress - AttackFraction) / (1f - AttackFraction));
            group.alpha = envelope * peakAlpha;

            if (isDealt)
            {
                float punch = Mathf.Lerp(12f, 27f, Mathf.SmoothStep(0f, 1f, progress)) * sizeScale;
                for (int index = 0; index < ticks.Length; index++)
                {
                    if (ticks[index] == null)
                        continue;
                    float angle = 45f + index * 90f;
                    Vector2 direction = new Vector2(
                        Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                    ticks[index].anchoredPosition = direction * punch;
                }

                if (damageText != null)
                    damageText.rectTransform.anchoredPosition =
                        new Vector2(0f, Mathf.Lerp(60f, 80f, progress) * sizeScale);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (activeTaken == this) activeTaken = null;
        if (activeDealt == this) activeDealt = null;
    }

    private void PointArrowAt(Vector3? sourcePosition)
    {
        if (arrow == null)
            return;

        Camera camera = Camera.main;
        if (!sourcePosition.HasValue || camera == null)
        {
            arrow.gameObject.SetActive(false);
            return;
        }

        // Keep the arrow on a ring around the centre, rotated towards whoever
        // hit you, so it works whether they are on-screen or behind you.
        Vector3 viewport = camera.WorldToViewportPoint(sourcePosition.Value);
        Vector2 offset = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
        if (viewport.z < 0f)
            offset = -offset;

        if (offset.sqrMagnitude < 0.0001f)
        {
            arrow.gameObject.SetActive(false);
            return;
        }

        arrow.gameObject.SetActive(true);
        Vector2 direction = offset.normalized;
        arrow.anchoredPosition = direction * 165f * sizeScale;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Canvas CreateCanvas(int sortingOrder)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;
        return canvas;
    }

    private static string FormatDamage(float damage)
    {
        // Knockback-only abilities deal no health damage; showing "0" would
        // read as a failed hit, so the tag carries the name alone.
        return damage >= 0.5f ? Mathf.RoundToInt(damage).ToString() : string.Empty;
    }

    private static Image CreateImage(Transform parent, string imageName, Texture2D texture, Color color)
    {
        GameObject imageObject = new GameObject(imageName, typeof(RectTransform), typeof(Image));
        imageObject.layer = 5;
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f));
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Stretches an image over the whole screen. <paramref name="bleed"/>
    /// pushes its edges outside the screen, which moves the outer part of a
    /// gradient off-view and so thins the band that remains visible.
    /// </summary>
    private static Image CreateStretchedImage(Transform parent, string imageName, Texture2D texture,
        Color color, float bleed = 0f)
    {
        Image image = CreateImage(parent, imageName, texture, color);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-bleed, -bleed);
        rect.offsetMax = new Vector2(bleed, bleed);
        return image;
    }

    /// <summary>Font sizes track the overlay's size, with a legible floor.</summary>
    private int Scaled(int size) => Mathf.Max(11, Mathf.RoundToInt(size * sizeScale));

    private static Text CreateText(Transform parent, string value, int size, Color color)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.layer = 5;
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Texture2D GetVignetteTexture()
    {
        if (vignetteTexture != null)
            return vignetteTexture;

        const int size = 128;
        vignetteTexture = NewTexture(size, "Ability Hit Vignette");
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDistance = center.magnitude;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float normalized = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
            // Clear through the middle so the fight stays readable while hit.
            // The ramp starts late and rises steeply, which keeps the colour
            // hugging the screen edge instead of creeping towards the action.
            float alpha = Mathf.Clamp01((normalized - 0.55f) / 0.45f);
            vignetteTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
        }
        vignetteTexture.Apply(false, true);
        return vignetteTexture;
    }

    private static Texture2D GetSoftBarTexture()
    {
        if (softBarTexture != null)
            return softBarTexture;

        const int size = 64;
        softBarTexture = NewTexture(size, "Ability Hit Plate");
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float edgeY = Mathf.Min(y, size - 1 - y) / (size * 0.5f);
            softBarTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(edgeY * 3.2f)));
        }
        softBarTexture.Apply(false, true);
        return softBarTexture;
    }

    private static Texture2D GetTickTexture()
    {
        if (tickTexture != null)
            return tickTexture;

        const int size = 32;
        tickTexture = NewTexture(size, "Ability Hit Tick");
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float alongX = x / (float)(size - 1);
            float edgeY = Mathf.Min(y, size - 1 - y) / (size * 0.5f);
            // Tapered towards the outer end for a struck, bladed look.
            float alpha = Mathf.Clamp01(edgeY * 2.6f) * Mathf.Lerp(1f, 0.15f, alongX);
            tickTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tickTexture.Apply(false, true);
        return tickTexture;
    }

    private static Texture2D GetArrowTexture()
    {
        if (arrowTexture != null)
            return arrowTexture;

        const int size = 64;
        arrowTexture = NewTexture(size, "Ability Hit Arrow");
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float normalizedY = y / (float)(size - 1);
            float halfWidth = Mathf.Lerp(0.42f, 0.02f, normalizedY);
            float offsetX = Mathf.Abs(x / (float)(size - 1) - 0.5f);
            float alpha = offsetX <= halfWidth && normalizedY > 0.22f ? 1f : 0f;
            arrowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        arrowTexture.Apply(false, true);
        return arrowTexture;
    }

    private static Texture2D NewTexture(int size, string textureName)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    private static bool IsHeadless()
    {
        return Application.isBatchMode ||
               SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }
}
