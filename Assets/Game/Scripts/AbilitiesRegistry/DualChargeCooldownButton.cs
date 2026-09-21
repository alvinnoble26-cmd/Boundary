using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD ability button visual for a 2-charge ability (Base/platform is the first user). The
/// icon is split down the middle by a thin divider, and each half gets its own independent
/// cooldown "shutter": a dark overlay that covers that half while it's on cooldown and wipes
/// away from the bottom up as that specific charge finishes recharging. The underlying button
/// icon itself is never modified or cropped, so it always reads clearly.
///
/// This mirrors AbilityCooldownButton's look and public-method shape (Initialize/BeginCooldown)
/// but tracks two independent timers instead of one - it is NOT a drop-in replacement, it's
/// meant to be attached instead of AbilityCooldownButton specifically for 2-charge abilities.
///
/// PlayerAbilities starts each half from the Base ability's independently validated charges;
/// this component only owns the visual.
/// </summary>
[DisallowMultipleComponent]
public class DualChargeCooldownButton : MonoBehaviour
{
    public const int ChargeCount = 2;
    public const int LeftCharge = 0;
    public const int RightCharge = 1;

    private static readonly Color CooldownShadeColor = new Color(0.05f, 0.06f, 0.09f, 0.82f);
    // A faint icy tint on the recharge wipe ties this specifically to the Base/frost platform
    // ability, unlike the plain white wipe other single-charge abilities use.
    private static readonly Color RechargeColor = new Color(0.80f, 0.93f, 1f, 1f);
    private static readonly Color DividerColor = new Color(0f, 0f, 0f, 0.65f);

    private Button button;
    private Image baseImage;
    private readonly Image[] halfShade = new Image[ChargeCount];
    private readonly Image[] halfFill = new Image[ChargeCount];
    private readonly float[] cooldownStart = new float[ChargeCount];
    private readonly float[] cooldownDuration = new float[ChargeCount];
    private readonly bool[] coolingDown = new bool[ChargeCount];

    /// <summary>Wires this component to a button and (re)builds its two-half overlay if needed.</summary>
    public void Initialize(Button targetButton)
    {
        if (button == targetButton && halfFill[LeftCharge] != null && halfFill[RightCharge] != null)
            return;

        button = targetButton;
        baseImage = button != null ? button.targetGraphic as Image : null;

        if (baseImage == null)
        {
            Debug.LogWarning($"[{name}] Dual-charge cooldown display requires an Image target graphic.");
            enabled = false;
            return;
        }

        button.transition = Selectable.Transition.None;
        CreateDividerIfNeeded();
        CreateHalfIfNeeded(LeftCharge);
        CreateHalfIfNeeded(RightCharge);
        ShowReadyState(LeftCharge);
        ShowReadyState(RightCharge);
    }

    /// <summary>Starts (or restarts) the cooldown wipe for one charge (0 = left half, 1 = right half).</summary>
    public void BeginCooldown(int charge, float duration)
    {
        if (!ValidCharge(charge) || baseImage == null)
            return;

        if (duration <= 0f)
        {
            ShowReadyState(charge);
            return;
        }

        cooldownStart[charge] = Time.time;
        cooldownDuration[charge] = duration;
        coolingDown[charge] = true;

        halfShade[charge].enabled = true;
        halfFill[charge].fillAmount = 0f;
        halfFill[charge].enabled = true;
    }

    /// <summary>True while that charge is still recharging.</summary>
    public bool IsCoolingDown(int charge) => ValidCharge(charge) && coolingDown[charge];

    /// <summary>Instantly clears a charge's cooldown display (e.g. both charges available again).</summary>
    public void ShowReadyState(int charge)
    {
        if (!ValidCharge(charge))
            return;
        coolingDown[charge] = false;
        if (halfShade[charge] != null)
            halfShade[charge].enabled = false;
        if (halfFill[charge] != null)
        {
            halfFill[charge].fillAmount = 0f;
            halfFill[charge].enabled = false;
        }
    }

    private static bool ValidCharge(int charge) => charge >= 0 && charge < ChargeCount;

    private void Update()
    {
        for (int charge = 0; charge < ChargeCount; charge++)
        {
            if (!coolingDown[charge] || halfFill[charge] == null)
                continue;
            float elapsed = Time.time - cooldownStart[charge];
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, cooldownDuration[charge]));
            halfFill[charge].fillAmount = progress;
            if (progress >= 1f)
                ShowReadyState(charge);
        }
    }

    private void CreateDividerIfNeeded()
    {
        Transform existing = transform.Find("ChargeDivider");
        Image divider = existing != null ? existing.GetComponent<Image>() : null;
        if (divider == null)
        {
            GameObject dividerObject = new GameObject("ChargeDivider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dividerObject.layer = gameObject.layer;
            dividerObject.transform.SetParent(transform, false);
            divider = dividerObject.GetComponent<Image>();
            RectTransform rect = divider.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(2.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
        }
        divider.raycastTarget = false;
        divider.sprite = null;
        divider.color = DividerColor;
        divider.transform.SetAsLastSibling();
    }

    private void CreateHalfIfNeeded(int charge)
    {
        bool isLeft = charge == LeftCharge;
        string shadeName = isLeft ? "LeftCooldownShade" : "RightCooldownShade";
        string fillName = isLeft ? "LeftRechargeFill" : "RightRechargeFill";

        Transform existingShade = transform.Find(shadeName);
        Image shade = existingShade != null ? existingShade.GetComponent<Image>() : null;
        if (shade == null)
        {
            GameObject shadeObject = new GameObject(shadeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadeObject.layer = gameObject.layer;
            shadeObject.transform.SetParent(transform, false);
            shadeObject.transform.SetAsFirstSibling();
            shade = shadeObject.GetComponent<Image>();
            SetHalfRect(shade.rectTransform, isLeft);
        }
        shade.raycastTarget = false;
        shade.sprite = null;
        shade.color = CooldownShadeColor;
        shade.enabled = false;
        halfShade[charge] = shade;

        Transform existingFill = transform.Find(fillName);
        Image fill = existingFill != null ? existingFill.GetComponent<Image>() : null;
        if (fill == null)
        {
            GameObject fillObject = new GameObject(fillName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.layer = gameObject.layer;
            fillObject.transform.SetParent(transform, false);
            fill = fillObject.GetComponent<Image>();
            SetHalfRect(fill.rectTransform, isLeft);
        }
        fill.raycastTarget = false;
        fill.sprite = null;
        fill.color = RechargeColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillClockwise = true;
        fill.fillAmount = 0f;
        fill.enabled = false;
        // Sits above the shade (drawn after it) but below the divider.
        fill.transform.SetSiblingIndex(shade.transform.GetSiblingIndex() + 1);
        halfFill[charge] = fill;
    }

    private static void SetHalfRect(RectTransform rect, bool isLeft)
    {
        rect.anchorMin = isLeft ? new Vector2(0f, 0f) : new Vector2(0.5f, 0f);
        rect.anchorMax = isLeft ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
