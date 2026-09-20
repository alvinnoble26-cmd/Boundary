using UnityEngine;
using UnityEngine.EventSystems;

public class EditableControlWidget : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public string ControlId { get; private set; }
    public float BaseSize { get; private set; }
    public float Scale { get; private set; } = 1f;

    private RectTransform rect;
    private RectTransform workspace;
    private Vector2 dragOffset;
    private bool canMove = true;

    public void Initialize(string controlId, float baseSize, RectTransform parentWorkspace, bool allowMovement = true)
    {
        ControlId = controlId;
        BaseSize = baseSize;
        workspace = parentWorkspace;
        rect = (RectTransform)transform;
        canMove = allowMovement;
    }

    public void Apply(ControlLayoutSettings.ControlEntry entry)
    {
        if (entry == null)
            return;

        Scale = Mathf.Clamp(entry.scale, 0.55f, 1.8f);
        Vector2 anchor = canMove
            ? new Vector2(Mathf.Clamp01(entry.x), Mathf.Clamp01(entry.y))
            : Vector2.one * 0.5f;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        UpdateSize();
    }

    public ControlLayoutSettings.ControlEntry Capture()
    {
        Vector2 anchor = canMove ? rect.anchorMin : Vector2.one * 0.5f;
        return new ControlLayoutSettings.ControlEntry(ControlId, anchor.x, anchor.y, Scale);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!canMove || workspace == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            workspace,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerPosition);
        dragOffset = (Vector2)rect.localPosition - pointerPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!canMove || workspace == null || workspace.rect.width <= 0f || workspace.rect.height <= 0f)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            workspace,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerPosition);

        Vector2 localPosition = pointerPosition + dragOffset;
        Vector2 normalized = new Vector2(
            (localPosition.x - workspace.rect.xMin) / workspace.rect.width,
            (localPosition.y - workspace.rect.yMin) / workspace.rect.height);

        Vector2 halfNormalizedSize = new Vector2(
            rect.rect.width * 0.5f / workspace.rect.width,
            rect.rect.height * 0.5f / workspace.rect.height);
        normalized.x = Mathf.Clamp(normalized.x, halfNormalizedSize.x, 1f - halfNormalizedSize.x);
        normalized.y = Mathf.Clamp(normalized.y, halfNormalizedSize.y, 1f - halfNormalizedSize.y);

        rect.anchorMin = normalized;
        rect.anchorMax = normalized;
        rect.anchoredPosition = Vector2.zero;
    }

    public void ResizeFromPointerDelta(Vector2 pointerDelta)
    {
        float referenceDelta = Mathf.Abs(pointerDelta.x) >= Mathf.Abs(pointerDelta.y)
            ? pointerDelta.x
            : pointerDelta.y;
        Scale = Mathf.Clamp(Scale + referenceDelta / Mathf.Max(80f, BaseSize), 0.55f, 1.8f);
        UpdateSize();
    }

    private void UpdateSize()
    {
        float size = BaseSize * Scale;
        rect.sizeDelta = new Vector2(size, size);
    }
}
