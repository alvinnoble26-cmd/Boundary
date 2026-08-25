using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class GameExitButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ICancelHandler
{
    public const float HoldDuration = 1.5f;

    private static readonly Color IdleColor = new Color(0.025f, 0.008f, 0.05f, 0.94f);
    private static readonly Color ArmedColor = new Color(0.30f, 0.06f, 0.42f, 0.96f);

    private enum ExitState
    {
        Idle,
        Armed,
        Leaving
    }

    private ExitState state;
    private Image background;
    private Text label;
    private bool pointerInside;
    private bool holding;
    private int holdingPointerId = int.MinValue;
    private float holdElapsed;

    private void Awake()
    {
        // The Game HUD is scene-authored. These references used to be assigned
        // only when the button was constructed at runtime, so they are null
        // after loading the saved scene on device or in the Editor.
        background = GetComponent<Image>();
        label = GetComponentInChildren<Text>(true);
        if (background != null)
        {
            background.raycastTarget = true;
            background.color = state == ExitState.Armed ? ArmedColor : IdleColor;
        }
        ApplyTopLeftLayout();
    }

    public static void Create(Transform safeArea)
    {
        if (safeArea == null || Application.isBatchMode ||
            SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            return;

        GameObject buttonObject = new GameObject(
            "Game Exit Button",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(GameExitButton));
        buttonObject.layer = 5;
        buttonObject.transform.SetParent(safeArea, false);

        GameExitButton exitButton = buttonObject.GetComponent<GameExitButton>();
        exitButton.ApplyTopLeftLayout();

        exitButton.background = buttonObject.GetComponent<Image>();
        exitButton.background.color = IdleColor;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.layer = 5;
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        exitButton.label = labelObject.GetComponent<Text>();
        exitButton.label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        exitButton.label.fontStyle = FontStyle.Bold;
        exitButton.label.fontSize = 38;
        exitButton.label.alignment = TextAnchor.MiddleCenter;
        exitButton.label.color = Color.white;
        exitButton.label.raycastTarget = false;
        exitButton.label.text = "X";
    }

    private void Update()
    {
        if (!holding || state != ExitState.Armed)
            return;

        holdElapsed += Time.unscaledDeltaTime;
        if (HasCompletedHold(holdElapsed))
            BeginExit();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerInside = true;

        if (state != ExitState.Armed)
            return;

        if (holding && holdingPointerId != eventData.pointerId)
        {
            ResetHold();
            return;
        }

        holding = true;
        holdingPointerId = eventData.pointerId;
        holdElapsed = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (state == ExitState.Idle)
        {
            if (ShouldArmAfterTap(pointerInside))
                Arm();
            pointerInside = false;
            return;
        }

        if (eventData.pointerId == holdingPointerId)
            ResetHold();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (eventData.pointerId == holdingPointerId)
            ResetHold();
    }

    public void OnCancel(BaseEventData eventData)
    {
        pointerInside = false;
        ResetHold();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            ResetHold();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            ResetHold();
    }

    private void OnDisable()
    {
        ResetHold();
    }

    private void Arm()
    {
        if (background == null || label == null)
            return;

        state = ExitState.Armed;
        background.color = ArmedColor;
        label.text = "Hold to exit";
        label.fontSize = 18;
        ((RectTransform)transform).sizeDelta = new Vector2(180f, 64f);
    }

    private void BeginExit()
    {
        if (background == null || label == null)
            return;

        state = ExitState.Leaving;
        holding = false;
        background.raycastTarget = false;
        label.text = "Leaving...";

        if (GameManager.I != null)
            GameManager.I.ExitGameToPlayPanel();
    }

    private void ResetHold()
    {
        holding = false;
        holdingPointerId = int.MinValue;
        holdElapsed = 0f;
    }

    private void ApplyTopLeftLayout()
    {
        if (!(transform is RectTransform rect))
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        if (rect.sizeDelta.sqrMagnitude < 0.01f)
            rect.sizeDelta = new Vector2(64f, 64f);
    }

    public static bool ShouldArmAfterTap(bool releasedInsideButton)
    {
        return releasedInsideButton;
    }

    public static bool HasCompletedHold(float elapsedSeconds)
    {
        return elapsedSeconds >= HoldDuration;
    }
}
