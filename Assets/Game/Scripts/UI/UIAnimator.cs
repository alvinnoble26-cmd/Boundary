using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class UIAnimator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private bool animatePanelOnEnable;
    [SerializeField] private bool animateButton;
    [SerializeField] private bool pulsePrimary;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 restingPosition;
    private Vector3 restingScale;
    private Coroutine panelRoutine;
    private Coroutine scaleRoutine;
    private Coroutine pulseRoutine;
    private bool pointerDown;
    private bool focused;

    public void ConfigurePanel(bool enabled = true) => animatePanelOnEnable = enabled;

    public void ConfigureButton(bool primary)
    {
        animateButton = true;
        pulsePrimary = primary;
        if (isActiveAndEnabled && primary)
            StartPrimaryPulse();
    }

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        restingScale = transform.localScale;
        if (rectTransform != null)
            restingPosition = rectTransform.anchoredPosition;
        if (animatePanelOnEnable)
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (rectTransform == null)
            Awake();
        if (animatePanelOnEnable)
        {
            if (panelRoutine != null) StopCoroutine(panelRoutine);
            panelRoutine = StartCoroutine(AnimatePanelIn());
        }
        if (pulsePrimary)
            StartPrimaryPulse();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        panelRoutine = scaleRoutine = pulseRoutine = null;
        transform.localScale = restingScale;
        if (rectTransform != null)
            rectTransform.anchoredPosition = restingPosition;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator AnimatePanelIn()
    {
        UITheme theme = UITheme.Current;
        float duration = theme != null ? theme.panelDuration : 0.2f;
        float distance = theme != null ? theme.panelSlideDistance : 24f;
        canvasGroup ??= GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        rectTransform.anchoredPosition = restingPosition - Vector2.up * distance;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
            canvasGroup.alpha = t;
            rectTransform.anchoredPosition = Vector2.Lerp(restingPosition - Vector2.up * distance, restingPosition, t);
            yield return null;
        }
        rectTransform.anchoredPosition = restingPosition;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        panelRoutine = null;
    }

    public void HideAndDeactivate()
    {
        StopAllCoroutines();
        canvasGroup ??= GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData) { pointerDown = true; AnimateScale(TargetPressScale()); }
    public void OnPointerUp(PointerEventData eventData) { pointerDown = false; AnimateScale(focused ? TargetFocusScale() : 1f); }
    public void OnPointerEnter(PointerEventData eventData) { focused = true; if (!pointerDown) AnimateScale(TargetFocusScale()); }
    public void OnPointerExit(PointerEventData eventData) { focused = false; pointerDown = false; AnimateScale(1f); }
    public void OnSelect(BaseEventData eventData) { focused = true; if (!pointerDown) AnimateScale(TargetFocusScale()); }
    public void OnDeselect(BaseEventData eventData) { focused = false; if (!pointerDown) AnimateScale(1f); }

    private float TargetPressScale() => UITheme.Current != null ? UITheme.Current.buttonPressScale : 0.96f;
    private float TargetFocusScale() => UITheme.Current != null ? UITheme.Current.buttonFocusScale : 1.03f;

    private void AnimateScale(float multiplier)
    {
        if (!animateButton || !isActiveAndEnabled) return;
        if (scaleRoutine != null) StopCoroutine(scaleRoutine);
        scaleRoutine = StartCoroutine(ScaleTo(restingScale * multiplier, 0.08f));
    }

    private IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.LerpUnclamped(start, target, 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f));
            yield return null;
        }
        transform.localScale = target;
        scaleRoutine = null;
    }

    private void StartPrimaryPulse()
    {
        if (!isActiveAndEnabled || !pulsePrimary) return;
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulsePrimary());
    }

    private IEnumerator PulsePrimary()
    {
        float duration = UITheme.Current != null ? UITheme.Current.primaryPulseDuration : 2.4f;
        while (true)
        {
            if (!pointerDown && !focused && scaleRoutine == null)
            {
                float wave = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / duration) + 1f) * 0.5f;
                transform.localScale = restingScale * Mathf.Lerp(1f, 1.018f, wave);
            }
            yield return null;
        }
    }
}
