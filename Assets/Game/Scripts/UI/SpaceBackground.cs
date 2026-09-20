using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SpaceBackground : MonoBehaviour
{
    [SerializeField] private RectTransform starField;
    [SerializeField] private RectTransform horizonGlow;
    [SerializeField] private Graphic horizonGraphic;

    private void Update()
    {
        float time = Time.unscaledTime;
        if (starField != null)
            starField.anchoredPosition = new Vector2(Mathf.Sin(time * 0.055f) * 16f, Mathf.Cos(time * 0.043f) * 10f);
        if (horizonGlow != null)
            horizonGlow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.035f) * 2.5f);
        if (horizonGraphic != null)
        {
            Color color = horizonGraphic.color;
            color.a = Mathf.Lerp(0.055f, 0.095f, (Mathf.Sin(time * 0.32f) + 1f) * 0.5f);
            horizonGraphic.color = color;
        }
    }

    public void Configure(RectTransform stars, RectTransform horizon, Graphic glow)
    {
        starField = stars;
        horizonGlow = horizon;
        horizonGraphic = glow;
    }
}
