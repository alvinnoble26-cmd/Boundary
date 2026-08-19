using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MenuTextButtonFeedback : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Graphic label;
    private Color normalColor;
    private Vector3 normalScale;
    private bool pressed;

    public void Initialize(Graphic text)
    {
        label = text;
        normalColor = text.color;
        normalScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        ApplyPressedStyle();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        RestoreStyle();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (pressed) ApplyPressedStyle();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        RestoreStyle();
    }

    private void ApplyPressedStyle()
    {
        if (label != null) label.color = Color.Lerp(normalColor, Color.yellow, 0.55f);
        transform.localScale = normalScale * 1.08f;
    }

    private void RestoreStyle()
    {
        if (label != null) label.color = normalColor;
        transform.localScale = normalScale;
    }
}
