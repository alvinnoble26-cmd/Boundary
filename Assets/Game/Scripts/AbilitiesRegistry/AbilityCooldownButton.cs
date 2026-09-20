using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AbilityCooldownButton : MonoBehaviour
{
    private static readonly Color ReadyColor = Color.white;
    private static readonly Color CooldownColor = new Color(0.28f, 0.28f, 0.28f, 1f);

    private Button button;
    private Image baseImage;
    private Image cooldownFill;
    private Image readinessGlow;
    private float cooldownStart;
    private float cooldownDuration;
    private bool coolingDown;
    private bool readinessHighlighted;

    public void Initialize(Button targetButton)
    {
        if (button == targetButton && baseImage != null && cooldownFill != null)
            return;

        button = targetButton;
        baseImage = button != null ? button.targetGraphic as Image : null;

        if (baseImage == null)
        {
            Debug.LogWarning($"[{name}] Cooldown display requires an Image target graphic.");
            enabled = false;
            return;
        }

        button.transition = Selectable.Transition.None;
        CreateFillImageIfNeeded();
        CreateReadinessGlowIfNeeded();
        ShowReadyState();
    }

    public void SetReadinessHighlight(bool highlighted)
    {
        readinessHighlighted = highlighted;
        if (readinessGlow != null)
            readinessGlow.enabled = highlighted;
    }

    public void BeginCooldown(float duration)
    {
        if (baseImage == null)
            return;

        if (duration <= 0f)
        {
            ShowReadyState();
            return;
        }

        SyncFillAppearance();
        cooldownStart = Time.time;
        cooldownDuration = duration;
        coolingDown = true;

        baseImage.color = CooldownColor;
        cooldownFill.color = ReadyColor;
        cooldownFill.fillAmount = 0f;
        cooldownFill.enabled = true;
    }

    private void Update()
    {
        if (coolingDown && cooldownFill != null)
        {
            float elapsed = Time.time - cooldownStart;
            float progress = Mathf.Clamp01(elapsed / cooldownDuration);
            cooldownFill.fillAmount = progress;

            if (progress >= 1f)
                ShowReadyState();
        }

        if (readinessHighlighted && readinessGlow != null)
        {
            float pulse = 0.86f + Mathf.Sin(Time.unscaledTime * 9f) * 0.14f;
            readinessGlow.color = new Color(0.92f, 0.38f, 1f, pulse);
            readinessGlow.rectTransform.localScale = Vector3.one *
                (1.4f + Mathf.Sin(Time.unscaledTime * 9f) * 0.14f);
        }
    }

    private void CreateFillImageIfNeeded()
    {
        Transform existing = transform.Find("CooldownFill");
        if (existing != null)
            cooldownFill = existing.GetComponent<Image>();

        if (cooldownFill == null)
        {
            GameObject fillObject = new GameObject("CooldownFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.layer = gameObject.layer;
            fillObject.transform.SetParent(transform, false);
            fillObject.transform.SetAsFirstSibling();
            cooldownFill = fillObject.GetComponent<Image>();

            RectTransform rect = fillObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        cooldownFill.raycastTarget = false;
        cooldownFill.type = Image.Type.Filled;
        cooldownFill.fillMethod = Image.FillMethod.Vertical;
        cooldownFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        cooldownFill.fillClockwise = true;
        SyncFillAppearance();
    }

    private void CreateReadinessGlowIfNeeded()
    {
        Transform existing = transform.Find("ReadinessGlow");
        if (existing != null)
            readinessGlow = existing.GetComponent<Image>();

        if (readinessGlow == null)
        {
            GameObject glowObject = new GameObject("ReadinessGlow", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            glowObject.layer = gameObject.layer;
            glowObject.transform.SetParent(transform, false);
            readinessGlow = glowObject.GetComponent<Image>();
            RectTransform rect = readinessGlow.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        readinessGlow.raycastTarget = false;
        readinessGlow.sprite = baseImage.sprite;
        readinessGlow.material = baseImage.material;
        readinessGlow.preserveAspect = baseImage.preserveAspect;
        readinessGlow.enabled = readinessHighlighted;
        readinessGlow.transform.SetAsFirstSibling();
    }

    private void SyncFillAppearance()
    {
        if (cooldownFill == null || baseImage == null)
            return;

        cooldownFill.sprite = baseImage.sprite;
        cooldownFill.material = baseImage.material;
        cooldownFill.preserveAspect = baseImage.preserveAspect;
    }

    private void ShowReadyState()
    {
        coolingDown = false;

        if (baseImage != null)
            baseImage.color = ReadyColor;

        if (cooldownFill != null)
        {
            cooldownFill.fillAmount = 0f;
            cooldownFill.enabled = false;
        }
    }
}
