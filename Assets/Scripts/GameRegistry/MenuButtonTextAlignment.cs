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
    private static readonly string[] MainStackButtons =
    {
        "HOST", "JOIN", "PracticeButton", "BackButton (4)"
    };

    private void Awake()
    {
        CenterMainMenuStack();

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

    public void CenterMainMenuStack()
    {
        Transform menu = FindDescendant(transform, "MuiltiplayerMenu");
        if (menu is RectTransform menuRect)
        {
            Vector2 menuPosition = menuRect.anchoredPosition;
            menuPosition.x = 0f;
            menuRect.anchoredPosition = menuPosition;
        }

        foreach (string buttonName in MainStackButtons)
        {
            Transform candidate = menu != null ? menu.Find(buttonName) : null;
            if (!(candidate is RectTransform rect))
                continue;

            rect.anchorMin = Vector2.one * 0.5f;
            rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            Vector2 position = rect.anchoredPosition;
            position.x = 0f;
            rect.anchoredPosition = position;
        }
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root == null)
            return null;
        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }
}
