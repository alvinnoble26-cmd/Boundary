using UnityEngine;
using UnityEngine.EventSystems;

public class TouchLookHandler : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public Vector2 LookDelta { get; private set; }
    private bool dragging;

    public void OnPointerDown(PointerEventData eventData)
    {
        dragging = true;
        LookDelta = Vector2.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging) return;

        // Kill tiny jitter
        var d = eventData.delta;
        if (d.sqrMagnitude < 0.25f) d = Vector2.zero; // tweak if needed

        LookDelta = d;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        dragging = false;
        LookDelta = Vector2.zero;
    }

    public Vector2 ConsumeLookDelta()
    {
        // The camera consumes each pointer delta exactly once. Clearing this in
        // LateUpdate made look input depend on script execution order: on some
        // frames the UI cleared it before the camera could read it.
        Vector2 value = LookDelta;
        LookDelta = Vector2.zero;
        return value;
    }
}
