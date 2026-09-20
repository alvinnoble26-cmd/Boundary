using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class UIStyle
{
    public static UITheme Theme => UITheme.Current;

    public static void ApplyText(TMP_Text text, float size, Color color, FontStyles style = FontStyles.Normal)
    {
        if (text == null) return;
        UITheme theme = Theme;
        if (theme != null && theme.font != null) text.font = theme.font;
        if (theme != null && theme.fontMaterial != null) text.fontSharedMaterial = theme.fontMaterial;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    public static void ApplySliced(Image image, Sprite sprite, Color color)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
    }

    public static void ApplyButton(Button button, bool primary)
    {
        if (button == null || Theme == null) return;
        Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        ApplySliced(image, Theme.roundedFill, primary ? Theme.accent : Theme.raisedPanel);
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = primary ? new Color(0.86f, 1f, 1f) : new Color(1.08f, 1.08f, 1.08f);
        colors.pressedColor = new Color(0.78f, 0.84f, 0.9f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.45f, 0.48f, 0.55f, 0.55f);
        button.colors = colors;
    }
}
