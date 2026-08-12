using UnityEngine;

[ExecuteAlways]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform rect;
    Rect lastSafeArea = new Rect(0, 0, 0, 0);
    Vector2Int lastScreenSize = new Vector2Int(0, 0);
    ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void Update()
    {
        if (rect == null) rect = GetComponent<RectTransform>();

        if (Screen.safeArea != lastSafeArea ||
            Screen.width != lastScreenSize.x ||
            Screen.height != lastScreenSize.y ||
            Screen.orientation != lastOrientation)
        {
            ApplySafeArea();
        }
    }

    void ApplySafeArea()
    {
        Rect safe = Screen.safeArea;

        lastSafeArea = safe;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastOrientation = Screen.orientation;

        Vector2 anchorMin = safe.position;
        Vector2 anchorMax = safe.position + safe.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
