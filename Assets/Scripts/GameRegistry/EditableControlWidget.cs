using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CircleGraphic : MaskableGraphic, ICanvasRaycastFilter
{
    [SerializeField, Range(16, 128)] private int segments = 64;

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect drawingRect = GetPixelAdjustedRect();
        Vector2 center = drawingRect.center;
        float radius = Mathf.Min(drawingRect.width, drawingRect.height) * 0.5f;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = center;
        vertex.uv0 = new Vector2(0.5f, 0.5f);
        vertexHelper.AddVert(vertex);

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vertex.position = center + direction * radius;
            vertex.uv0 = direction * 0.5f + Vector2.one * 0.5f;
            vertexHelper.AddVert(vertex);
        }

        for (int i = 1; i <= segments; i++)
            vertexHelper.AddTriangle(0, i, i + 1);
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, eventCamera, out Vector2 localPoint))
            return false;

        Rect rect = rectTransform.rect;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        return (localPoint - rect.center).sqrMagnitude <= radius * radius;
    }
}

public class EditableControlWidget : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public string ControlId { get; private set; }
    public float BaseSize { get; private set; }
    public float Scale { get; private set; } = 1f;

    private RectTransform rect;
    private RectTransform workspace;
    private Vector2 dragOffset;

    public void Initialize(string controlId, float baseSize, RectTransform parentWorkspace)
    {
        ControlId = controlId;
        BaseSize = baseSize;
        workspace = parentWorkspace;
        rect = (RectTransform)transform;
    }

    public void Apply(ControlLayoutSettings.ControlEntry entry)
    {
        if (entry == null)
            return;

        Scale = Mathf.Clamp(entry.scale, 0.55f, 1.8f);
        Vector2 anchor = new Vector2(Mathf.Clamp01(entry.x), Mathf.Clamp01(entry.y));
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        UpdateSize();
    }

    public ControlLayoutSettings.ControlEntry Capture()
    {
        Vector2 anchor = rect.anchorMin;
        return new ControlLayoutSettings.ControlEntry(ControlId, anchor.x, anchor.y, Scale);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (workspace == null)
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
        if (workspace == null || workspace.rect.width <= 0f || workspace.rect.height <= 0f)
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

public class ControlResizeHandle : MonoBehaviour, IDragHandler
{
    private EditableControlWidget widget;

    public void Initialize(EditableControlWidget target)
    {
        widget = target;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (widget != null)
            widget.ResizeFromPointerDelta(eventData.delta);
    }
}
