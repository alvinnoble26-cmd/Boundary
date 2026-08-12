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

    void LateUpdate()
    {
        // Important: consume the delta each frame so it doesn't "stick"
        LookDelta = Vector2.zero;
    }
}
