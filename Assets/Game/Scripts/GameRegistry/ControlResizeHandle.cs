using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ControlResizeHandle : MonoBehaviour, IDragHandler
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
