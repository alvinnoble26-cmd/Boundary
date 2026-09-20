using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasScaler))]
public sealed class ResponsiveCanvasMatch : MonoBehaviour
{
    [SerializeField] private float portraitMatch = 0.82f;
    [SerializeField] private float landscapeMatch = 0.5f;
    private CanvasScaler scaler;

    private void Awake() => Apply();
    private void OnRectTransformDimensionsChange() => Apply();

    private void Apply()
    {
        if (scaler == null) scaler = GetComponent<CanvasScaler>();
        if (scaler == null || Screen.width <= 0 || Screen.height <= 0) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = Screen.height > Screen.width ? portraitMatch : landscapeMatch;
    }
}
