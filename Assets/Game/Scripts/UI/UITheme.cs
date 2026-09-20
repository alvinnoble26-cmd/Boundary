using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "UITheme", menuName = "Entropy Zero/UI Theme")]
public sealed class UITheme : ScriptableObject
{
    private static UITheme current;

    [Header("Typography")]
    public TMP_FontAsset font;
    public float titleSize = 110f;
    public float headerSize = 56f;
    public float bodySize = 32f;
    public float captionSize = 24f;
    public float buttonSize = 40f;
    public float titleCharacterSpacing = 9f;

    [Header("Colors")]
    public Color background = new Color32(10, 13, 20, 255);
    public Color panel = new Color32(18, 24, 38, 230);
    public Color raisedPanel = new Color32(26, 34, 54, 255);
    public Color accent = new Color32(61, 224, 255, 255);
    public Color secondary = new Color32(139, 92, 255, 255);
    public Color danger = new Color32(255, 77, 94, 255);
    public Color success = new Color32(77, 255, 176, 255);
    public Color text = new Color32(232, 236, 245, 255);
    public Color muted = new Color32(138, 148, 168, 255);
    public Color border = new Color(1f, 1f, 1f, 0.08f);

    [Header("Layout")]
    public float cornerRadius = 24f;
    public float spacing = 8f;
    public float outerMargin = 24f;
    public float minimumTouchHeight = 88f;

    [Header("Motion")]
    public float panelDuration = 0.2f;
    public float panelSlideDistance = 24f;
    public float buttonPressScale = 0.96f;
    public float buttonFocusScale = 1.03f;
    public float primaryPulseDuration = 2.4f;

    [Header("Generated Sprites")]
    public Sprite roundedFill;
    public Sprite roundedBorder;
    public Sprite softShadow;
    public Sprite accentGlow;
    public Sprite spaceHorizon;

    public static UITheme Current
    {
        get
        {
            if (current == null)
                current = Resources.Load<UITheme>("UI/UITheme");
            return current;
        }
    }

    public static void SetCurrentForTests(UITheme theme) => current = theme;
}
