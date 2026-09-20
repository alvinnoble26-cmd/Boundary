using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ContainedInstabilityBackground : MonoBehaviour
{
    [SerializeField] private RectTransform firstArc;
    [SerializeField] private RectTransform secondArc;
    [SerializeField] private Graphic firstGraphic;
    [SerializeField] private Graphic secondGraphic;

    private void Update()
    {
        float t = Time.unscaledTime;
        if (firstArc != null) firstArc.localRotation = Quaternion.Euler(0f, 0f, t * 1.4f);
        if (secondArc != null) secondArc.localRotation = Quaternion.Euler(0f, 0f, -t * 0.85f);
        if (firstGraphic != null)
        {
            Color color = firstGraphic.color;
            color.a = Mathf.Lerp(0.035f, 0.07f, (Mathf.Sin(t * 0.55f) + 1f) * 0.5f);
            firstGraphic.color = color;
        }
        if (secondGraphic != null)
        {
            Color color = secondGraphic.color;
            color.a = Mathf.Lerp(0.02f, 0.05f, (Mathf.Sin(t * 0.41f + 1.7f) + 1f) * 0.5f);
            secondGraphic.color = color;
        }
    }

    public void Configure(RectTransform first, Graphic firstImage, RectTransform second, Graphic secondImage)
    {
        firstArc = first;
        firstGraphic = firstImage;
        secondArc = second;
        secondGraphic = secondImage;
    }
}
