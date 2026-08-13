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
    private float cooldownStart;
    private float cooldownDuration;
    private bool coolingDown;

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
        ShowReadyState();
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
        if (!coolingDown || cooldownFill == null)
            return;

        float elapsed = Time.time - cooldownStart;
        float progress = Mathf.Clamp01(elapsed / cooldownDuration);
        cooldownFill.fillAmount = progress;

        if (progress >= 1f)
            ShowReadyState();
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
