using UnityEngine;
using UnityEngine.UI;

public sealed class CircleGraphic : MaskableGraphic, ICanvasRaycastFilter
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
