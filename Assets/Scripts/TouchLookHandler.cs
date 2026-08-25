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

        SubmitLookDelta(eventData.delta);
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

    public void SubmitLookDelta(Vector2 delta)
    {
        // Kill tiny jitter.
        if (delta.sqrMagnitude < 0.25f)
            delta = Vector2.zero;

        LookDelta = delta;
    }
}

/// <summary>
/// Forwards a drag that starts on the jump button to the look area. The
/// OnScreenButton keeps its pressed state, so this supports hold-to-jump and
/// camera look with the same touch.
/// </summary>
[DisallowMultipleComponent]
public sealed class JumpLookButton : MonoBehaviour, IDragHandler
{
    private TouchLookHandler touchLook;

    public void OnDrag(PointerEventData eventData)
    {
        if (touchLook == null)
            touchLook = FindFirstObjectByType<TouchLookHandler>();

        touchLook?.SubmitLookDelta(eventData.delta);
    }
}
