using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps every menu label centered inside its button without altering the
/// menu's established visual scale or calculating text geometry at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuButtonTextAlignment : MonoBehaviour
{
    private void Awake()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                continue;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            label.margin = Vector4.zero;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }
    }
}
